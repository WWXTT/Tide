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
        public string description;      // 说明书文本（任务与奖励对双方公开）
        public RitualTaskDef task;
        public RitualAuraDef aura;
    }

    /// <summary>竞速任务定义。kind：ColorStreak（颜色连击断言）/ LifePaidAccum（生命代价累计）。</summary>
    [Serializable]
    public class RitualTaskDef
    {
        public string kind;
        public int target;     // ColorStreak=连续达标回合数；LifePaidAccum=累计生命值
        public int window;     // ColorStreak=回看窗口（前 N 回合），其他 kind 无效
    }

    [Serializable]
    public class RitualAuraDef
    {
        public string effectId;   // RitualEffects 内的光环处理器 id（ElementConversion / BloodPact）
    }

    [Serializable]
    public class RitualConfigWrapper
    {
        public List<RitualDefinition> rituals;
    }

    // ======================================== 运行时状态 ========================================

    /// <summary>进行中的仪式（全局唯一槽位）——双方竞速，先达标者完成。</summary>
    public class ActiveRitual
    {
        public Card Card;                 // 场上的仪式卡（占打出者的格）
        public Player Owner;              // 打出者（回手归属）
        public RitualDefinition Definition;

        /// <summary>ColorStreak：每玩家连续达标回合数（断言失败清零）。</summary>
        public readonly Dictionary<Player, int> Streak = new Dictionary<Player, int>();

        /// <summary>LifePaidAccum：每玩家累计支付的生命值。</summary>
        public readonly Dictionary<Player, int> LifePaid = new Dictionary<Player, int>();

        /// <summary>ColorStreak：每玩家近 window 回合的用卡纯色集合（新→旧）。</summary>
        public readonly Dictionary<Player, List<HashSet<ManaType>>> RecentColors = new Dictionary<Player, List<HashSet<ManaType>>>();

        /// <summary>当前回合已用纯色（CardPlayEvent 累计，只记回合玩家自己的出牌）。</summary>
        public HashSet<ManaType> TurnColors = new HashSet<ManaType>();

        /// <summary>当前回合玩家（TurnStart 记录）。</summary>
        public Player TurnPlayer;
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
    /// 仪式系统（仿 ProphecySystem 的静态子系统挂载惯例：懒订阅 + GameCore.Reset 点名清理）。
    ///
    /// 定案规则：
    /// - 仪式卡占卡组位，开局固定入手但不占起手数（GameCore.InitGame 洗牌后先抽仪式；每玩家仅 1 张入手，多副本留牌库可抽）。
    /// - 0 费打出（CardCostService.EnsureCost 对 Ritual 豁免保持空 Cost），占 1 个 Battlefield 格，任务开始，可被破坏。
    /// - 任务竞速：双方各自行为各自计数，先达标者完成——对手进度作废、完成者独享光环。
    /// - 全局仅 1 个进行中任务：后发动者顶掉先发者（先发者进度作废并回手/入墓）。
    /// - 破坏 → 回手牌（手牌≥7 则直接入墓），全部进度清空；完成态由 Indestructible+Untargetable 挡住。
    /// - 光环封顶：占 1 非生物格 + 改 1 条规则，不得有资源类每回合净增长。
    /// </summary>
    public static class RitualSystem
    {
        private const string ConfigRelativePath = "Configs/RitualConfig.json";

        /// <summary>回手牌的手牌上限（与 GameCore.HandLimit 同源，语义：仪式占普通手牌位）。</summary>
        public const int HandLimit = 7;

        private static readonly ManaType[] PureColors = { ManaType.Red, ManaType.Blue, ManaType.Green };

        private static ActiveRitual _active;
        private static readonly List<CompletedRitualAura> _completed = new List<CompletedRitualAura>();
        private static Dictionary<string, RitualDefinition> _definitions;
        private static bool _subscribed;

        /// <summary>当前进行中的仪式任务（全局唯一；null=无任务）。</summary>
        public static ActiveRitual Active => _active;

        /// <summary>已完成并常驻的光环（可与进行中任务并存）。</summary>
        public static IReadOnlyList<CompletedRitualAura> CompletedAuras => _completed;

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

        /// <summary>打出仪式（PlayCard 永续入场后调用）：成为全局唯一任务，顶掉先发者。</summary>
        public static void OnPlayed(Card card, Player player)
        {
            var def = GetDefinition(card);
            if (def == null || player == null) return;
            EnsureSubscribed();

            // 全局仅 1 个进行中任务：后发动者顶掉先发者（进度作废 + 回手冲突规则）
            if (_active != null && _active.Card != card)
                DisplaceActive();

            _active = new ActiveRitual { Card = card, Owner = player, Definition = def };
        }

        /// <summary>进行中仪式被破坏（DestroyHandler 分支调用）：回手/入墓 + 全部进度清空。返回 true=已处理。</summary>
        public static bool OnDestroyed(Card card, ZoneManager zoneManager)
        {
            if (!IsActiveRitual(card)) return false;
            ReturnToOwnerHand(card, zoneManager);
            _active = null;
            return true;
        }

        /// <summary>清空全部仪式状态（新对局 Reset 调用）。</summary>
        public static void Reset()
        {
            _active = null;
            _completed.Clear();
        }

        // ======================================== 内部流转 ========================================

        /// <summary>回手统一出口：手牌≥上限 → 直接入墓地；否则回手并发 CardReturnToHandEvent。破坏与被顶掉共用。</summary>
        private static void ReturnToOwnerHand(Card card, ZoneManager zoneManager)
        {
            if (card == null || zoneManager == null) return;
            var owner = card.GetController();
            if (owner == null) return;

            bool handFull = zoneManager.GetCards(owner, Zone.Hand).Count >= HandLimit;
            zoneManager.MoveCard(card, owner, Zone.Battlefield, handFull ? Zone.Graveyard : Zone.Hand);
            if (!handFull)
                EventManager.Instance.Publish(new CardReturnToHandEvent { Card = card, Source = card });
        }

        private static void DisplaceActive()
        {
            var old = _active;
            _active = null; // 进度随槽位清空作废
            if (old?.Card == null) return;
            ReturnToOwnerHand(old.Card, GameCore.Instance?.ZoneManager);
        }

        /// <summary>完成判定（先到先得）：完成者独享光环、对手进度作废、任务槽清空（光环独立存续）。</summary>
        private static void TryComplete(ActiveRitual ritual, Player completer)
        {
            if (ritual == null || completer == null || _active != ritual) return;

            var aura = new CompletedRitualAura { Card = ritual.Card, Completer = completer, Definition = ritual.Definition };
            _completed.Add(aura);
            _active = null;

            // 完成态：不可摧毁 + 不受其他卡效果影响（destroy 路径与目标校验均检查这两个关键词）
            ritual.Card.AddKeyword(KeywordRules.Indestructible);
            ritual.Card.AddKeyword(KeywordRules.Untargetable);

            var completed = new RitualCompletedEvent { Card = ritual.Card, Completer = completer, RitualId = ritual.Definition.id };
            if (GameCore.Instance != null) GameCore.Instance.PublishEvent(completed);
            else EventManager.Instance.Publish(completed);
        }

        // ======================================== 事件订阅 ========================================

        private static void EnsureSubscribed()
        {
            if (_subscribed) return;
            _subscribed = true;
            EventManager.Instance.Subscribe<TurnStartEvent>(OnTurnStart);
            EventManager.Instance.Subscribe<CardPlayEvent>(OnCardPlayed);
            EventManager.Instance.Subscribe<TurnEndEvent>(OnTurnEnd);
            EventManager.Instance.Subscribe<LifePaymentCostEvent>(OnLifePaid);
            EventManager.Instance.Subscribe<ElementPoolPayEvent>(OnElementPaid);
        }

        private static void OnTurnStart(TurnStartEvent e)
        {
            if (_active?.Definition?.task?.kind != "ColorStreak") return;
            _active.TurnPlayer = e.TurnPlayer;
            _active.TurnColors = new HashSet<ManaType>();
        }

        private static void OnCardPlayed(CardPlayEvent e)
        {
            // 口径：「使用的卡」只认 PlayCard 打出的卡（发 CardPlayEvent）；放地牌入元素池不算。
            // 颜色只看纯色（红/蓝/绿），灰不计；多色卡按其包含的全部纯色计；只记回合玩家自己的出牌。
            if (_active?.Definition?.task?.kind != "ColorStreak") return;
            if (e.Player == null || e.Player != _active.TurnPlayer || !(e.PlayedCard is Card card)) return;

            foreach (var color in GetPureCostColors(card))
                _active.TurnColors.Add(color);
        }

        private static void OnTurnEnd(TurnEndEvent e)
        {
            if (_active?.Definition?.task?.kind != "ColorStreak") return;
            if (e.TurnPlayer == null || e.TurnPlayer != _active.TurnPlayer) return;

            var task = _active.Definition.task;
            int window = Math.Max(1, task.window);
            var colors = _active.TurnColors;

            if (!_active.RecentColors.TryGetValue(e.TurnPlayer, out var history))
            {
                history = new List<HashSet<ManaType>>();
                _active.RecentColors[e.TurnPlayer] = history;
            }

            // 断言：本回合用卡纯色非空，且与前 window 回合的用卡纯色不重复。空回合视为断言失败（没完成"用卡"行为）。
            bool pass = colors.Count > 0 && history.Take(window).All(s => !s.Overlaps(colors));

            _active.Streak.TryGetValue(e.TurnPlayer, out var streak);
            streak = pass ? streak + 1 : 0;
            _active.Streak[e.TurnPlayer] = streak;

            // 无论断言成败，本回合实际用过的颜色入历史（事实记录）
            if (colors.Count > 0)
            {
                history.Insert(0, new HashSet<ManaType>(colors));
                if (history.Count > window) history.RemoveAt(window);
            }

            if (streak >= task.target)
                TryComplete(_active, e.TurnPlayer);
        }

        private static void OnLifePaid(LifePaymentCostEvent e)
        {
            if (_active?.Definition?.task?.kind != "LifePaidAccum" || e?.Player == null) return;

            _active.LifePaid.TryGetValue(e.Player, out var paid);
            paid += e.Amount;
            _active.LifePaid[e.Player] = paid;

            if (paid >= _active.Definition.task.target)
                TryComplete(_active, e.Player);
        }

        private static void OnElementPaid(ElementPoolPayEvent e)
        {
            RitualEffects.OnElementPaid(e);
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
