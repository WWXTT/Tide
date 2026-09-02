using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CardCore.Attribute;
using UnityEngine;

namespace CardCore
{
    // ======================================== 配置 DTO（Configs/RitualConfig.json） ========================================

    /// <summary>仪式定义：id 对应卡表卡 ID（supertype=Enchantment、subtype 含 Ritual）。</summary>
    [Serializable]
    public class RitualDefinition
    {
        public string id;
        public string nameZh;
        public string axis;          // 优势轴标签（信息/生存/规则/节奏/场面/资源，设计词汇，引擎不消费）
        public string description;      // 说明书文本（任务与奖励对双方公开）
        public RitualTaskDef task;
        public RitualAuraDef aura;
    }

    /// <summary>
    /// 竞速任务定义。kind 词汇与实现组件见 RitualTrackers.cs（一 kind 一类，OCP）：
    /// ColorStreak / LifePaidAccum / HealOverflowAccum / RevealAccum /
    /// MillSelfAccum / SkipStandbyCount / NonDrawDrawAccum。
    /// </summary>
    [Serializable]
    public class RitualTaskDef
    {
        public string kind;
        public int target;     // 各 kind 的达标阈值（ColorStreak=连续达标回合数）
        public int window;     // ColorStreak=回看窗口（前 N 回合），其他 kind 无效
    }

    /// <summary>奖励定义。effectId 与实现组件见 RitualAuras.cs（一 effectId 一类，OCP）。</summary>
    [Serializable]
    public class RitualAuraDef
    {
        public string effectId;   // ElementConversion / BloodPact / DamageCap / LockRevealed / GraveyardPlay / ExtraTurnThenSelfDestruct / HandLimitAndNoFatigue
        public int value;         // 参数化数值（DamageCap=5、HandLimitAndNoFatigue=15 等；缺省 0 = 组件内定值）
    }

    [Serializable]
    public class RitualConfigWrapper
    {
        public List<RitualDefinition> rituals;
    }

    // ======================================== 运行时状态 ========================================

    /// <summary>进行中的仪式（全局唯一槽位）——双方竞速，先达标者完成。进度状态由各任务跟踪器自持。</summary>
    public class ActiveRitual
    {
        public Card Card;                 // 场上的仪式卡（占打出者的格）
        public Player Owner;              // 打出者（回手归属）
        public RitualDefinition Definition;
    }

    /// <summary>已完成仪式的永久光环（完成者独享；卡在场占格、不可摧毁、不受其他卡效果影响）。</summary>
    public class CompletedRitualAura
    {
        public Card Card;
        public Player Completer;
        public RitualDefinition Definition;

        /// <summary>ElementConversion：完成者的同纯色消耗累计（满 3 转化，余数保留）。</summary>
        public readonly Dictionary<ManaType, int> SpendCounters = new Dictionary<ManaType, int>();
    }

    /// <summary>
    /// 仪式系统内核（OCP 收敛后只管生命周期与注册表）：
    /// 配置加载 / 激活-顶掉-破坏-完成流转 / 任务跟踪器与光环组件注册表。
    /// 任务计数与奖励行为分别在 RitualTrackers.cs / RitualAuras.cs，经组件登记扩展。
    ///
    /// 定案规则：
    /// - 仪式卡占卡组位，开局占初始手牌位（GameCore.InitGame 洗牌后仪式先占位、抽牌补满至总量 6；超出的仪式留牌库可抽）。
    /// - 0 费打出（CardCostService.EnsureCost 对 Ritual 豁免保持空 Cost），占 1 个 Battlefield 格，任务开始，可被破坏。
    /// - 任务竞速：双方各自行为各自计数，先达标者完成——对手进度作废、完成者独享光环。
    /// - 全局仅 1 个进行中任务：后发动者顶掉先发者（先发者进度作废并回手/入墓）。
    /// - 破坏 → 回手牌（手牌满上限则直接入墓），全部进度清空；完成态由 Indestructible+Untargetable 挡住。
    /// - 光环封顶：占 1 个非生物格 + 改 1 条规则，不得有资源类每回合净增长（例外：疾风为一次性奖励，自毁）。
    /// </summary>
    public static class RitualSystem
    {
        private const string ConfigRelativePath = "Configs/RitualConfig.json";

        private static readonly ManaType[] PureColors = { ManaType.Red, ManaType.Blue, ManaType.Green };

        private static ActiveRitual _active;
        private static readonly List<CompletedRitualAura> _completed = new List<CompletedRitualAura>();
        private static Dictionary<string, RitualDefinition> _definitions;
        private static bool _subscribed;

        private static readonly Dictionary<string, IRitualTaskTracker> _trackers = new Dictionary<string, IRitualTaskTracker>();
        private static readonly Dictionary<string, IRitualAura> _auras = new Dictionary<string, IRitualAura>();

        /// <summary>当前进行中的仪式任务（全局唯一；null=无任务）。</summary>
        public static ActiveRitual Active => _active;

        /// <summary>已完成并常驻的光环（可与进行中任务并存）。</summary>
        public static IReadOnlyList<CompletedRitualAura> CompletedAuras => _completed;

        // ======================================== 组件注册表（扩展点） ========================================

        /// <summary>登记任务跟踪器（RitualComponents.EnsureRegistered 调用）。</summary>
        public static void RegisterTracker(IRitualTaskTracker tracker)
        {
            if (tracker != null) _trackers[tracker.Kind] = tracker;
        }

        /// <summary>登记光环组件（RitualComponents.EnsureRegistered 调用）。</summary>
        public static void RegisterAura(IRitualAura aura)
        {
            if (aura != null) _auras[aura.EffectId] = aura;
        }

        // ======================================== 识别与定义 ========================================

        public static bool IsRitual(Card card)
            => card is CardWrapper w && w.GetData() != null && (w.GetData().Subtype & CardSubtype.Ritual) != 0;

        public static bool IsActiveRitual(Card card) => _active != null && _active.Card == card;

        public static RitualDefinition GetDefinition(Card card)
        {
            if (!IsRitual(card)) return null;
            LoadDefinitions();
            var data = ((CardWrapper)card).GetData();
            return _definitions != null && _definitions.TryGetValue(data.ID, out var def) ? def : null;
        }

        private static void LoadDefinitions()
        {
            if (_definitions != null) return;
            _definitions = new Dictionary<string, RitualDefinition>();
            try
            {
                string path = Path.Combine(Application.dataPath, ConfigRelativePath);
                if (!File.Exists(path))
                {
                    Debug.LogWarning($"[RitualSystem] 配置文件不存在: {path}，仪式不可用");
                    return;
                }
                string raw = File.ReadAllText(path).TrimStart();
                string wrapped = raw.StartsWith("[") ? "{\"rituals\":" + raw + "}" : raw; // 裸数组包一层（同 CostOffsetService 惯例）
                var parsed = JsonUtility.FromJson<RitualConfigWrapper>(wrapped);
                if (parsed?.rituals == null) return;
                foreach (var def in parsed.rituals)
                {
                    if (def == null || string.IsNullOrEmpty(def.id)) continue;
                    _definitions[def.id] = def;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RitualSystem] 加载 {ConfigRelativePath} 失败: {e.Message}");
            }
        }

        // ======================================== 生命周期 ========================================

        /// <summary>
        /// 仪式入场（经 CardPutToBattlefieldEvent 事件驱动，见 EnsureSubscribed）：
        /// 成为全局唯一任务，顶掉先发者。
        /// </summary>
        public static void OnPlayed(Card card, Player player)
        {
            var def = GetDefinition(card);
            if (def == null || player == null) return;
            EnsureRuntime();

            // 全局仅 1 个进行中任务：后发动者顶掉先发者（进度作废 + 回手冲突规则）
            if (_active != null && _active.Card != card)
                DisplaceActive();

            _active = new ActiveRitual { Card = card, Owner = player, Definition = def };

            // 通知任务跟踪器初始化进度（ColorStreak 等自持状态、自接入当前回合）
            if (_trackers.TryGetValue(def.task?.kind ?? "", out var tracker))
                tracker.OnStarted(_active);
        }

        /// <summary>进行中仪式被破坏（DestroyHandler 分支调用）：回手/入墓 + 全部进度清空。返回 true=已处理。</summary>
        public static bool OnDestroyed(Card card, ZoneManager zoneManager)
        {
            if (!IsActiveRitual(card)) return false;
            EndActive();
            ReturnToOwnerHand(card, zoneManager);
            return true;
        }

        /// <summary>清空全部仪式状态（新对局 Reset 调用）。</summary>
        public static void Reset()
        {
            if (_active != null) EndActive();
            foreach (var aura in _completed.ToList())
                RemoveCompleted(aura);
            RitualComponents.OnGameReset();
        }

        /// <summary>
        /// 挂载仪式运行时（组件登记 + 入场事件订阅）。GameCore.Reset 调用——
        /// 对局开始即挂载，保证开局起的行为采集（展示记录、非抽牌入手等）不漏。
        /// </summary>
        public static void EnsureRuntime()
        {
            RitualComponents.EnsureRegistered();
            EnsureSubscribed();
        }

        /// <summary>移除一个已完成光环（自毁类奖励结清时调用；触发组件 OnRemoved）。</summary>
        public static void RemoveCompleted(CompletedRitualAura aura)
        {
            if (aura == null || !_completed.Remove(aura)) return;
            if (_auras.TryGetValue(aura.Definition?.aura?.effectId ?? "", out var component))
                component.OnRemoved(aura);
        }

        /// <summary>
        /// 完成判定（任务跟踪器达标时调用，先到先得）：
        /// 完成者独享光环、对手进度作废、任务槽清空（光环独立存续）。
        /// </summary>
        internal static void Complete(ActiveRitual ritual, Player completer)
        {
            if (ritual == null || completer == null || _active != ritual) return;

            EndActive(); // 任务槽清空 + 跟踪器进度作废（对手进度一并作废）

            var aura = new CompletedRitualAura { Card = ritual.Card, Completer = completer, Definition = ritual.Definition };
            _completed.Add(aura);

            // 完成态：不可摧毁 + 不受其他卡效果影响（destroy 路径与目标校验均检查这两个关键词）
            ritual.Card.AddKeyword(KeywordRules.Indestructible);
            ritual.Card.AddKeyword(KeywordRules.Untargetable);

            if (_auras.TryGetValue(ritual.Definition?.aura?.effectId ?? "", out var component))
                component.OnCompleted(aura);

            var completed = new RitualCompletedEvent { Card = ritual.Card, Completer = completer, RitualId = ritual.Definition.id };
            if (GameCore.Instance != null) GameCore.Instance.PublishEvent(completed);
            else EventManager.Instance.Publish(completed);
        }

        // ======================================== 内部流转 ========================================

        /// <summary>结束当前任务槽：通知跟踪器进度作废并清空槽位（完成/顶掉/破坏共用）。</summary>
        private static void EndActive()
        {
            var old = _active;
            _active = null;
            if (old?.Definition == null) return;
            if (_trackers.TryGetValue(old.Definition.task?.kind ?? "", out var tracker))
                tracker.OnEnded(old);
        }

        /// <summary>回手统一出口：手牌≥上限 → 直接入墓地；否则回手并发 CardReturnToHandEvent。破坏与被顶掉共用。</summary>
        private static void ReturnToOwnerHand(Card card, ZoneManager zoneManager)
        {
            if (card == null || zoneManager == null) return;
            var owner = card.GetController();
            if (owner == null) return;

            bool handFull = zoneManager.GetCards(owner, Zone.Hand).Count >= RuleHooks.GetHandLimit(owner);
            zoneManager.MoveCard(card, owner, Zone.Battlefield, handFull ? Zone.Graveyard : Zone.Hand);
            if (!handFull)
                EventManager.Instance.Publish(new CardReturnToHandEvent { Card = card, Source = card });
        }

        private static void DisplaceActive()
        {
            var old = _active;
            EndActive(); // 进度随槽位清空作废
            if (old?.Card == null) return;
            ReturnToOwnerHand(old.Card, GameCore.Instance?.ZoneManager);
        }

        // ======================================== 事件订阅 ========================================

        private static void EnsureSubscribed()
        {
            if (_subscribed) return;
            _subscribed = true;

            // 仪式入场激活走事件驱动（OCP：GameActions 不点名本系统）：
            // TryMoveToBattlefield 发布 CardPutToBattlefieldEvent，仅仪式卡触发激活
            EventManager.Instance.Subscribe<CardPutToBattlefieldEvent>(OnCardPutToBattlefield);
        }

        private static void OnCardPutToBattlefield(CardPutToBattlefieldEvent e)
        {
            if (IsRitual(e?.Card))
                OnPlayed(e.Card, e.Controller);
        }

        // ======================================== 工具 ========================================

        /// <summary>卡的纯色费用集合（红/蓝/绿，灰不计）。</summary>
        public static HashSet<ManaType> GetPureCostColors(Card card)
        {
            var set = new HashSet<ManaType>();
            if (card is IHasCost hasCost && hasCost.Cost != null)
            {
                foreach (var kv in hasCost.Cost)
                {
                    if (kv.Value > 0 && PureColors.Contains((ManaType)kv.Key))
                        set.Add((ManaType)kv.Key);
                }
            }
            return set;
        }
    }
}
