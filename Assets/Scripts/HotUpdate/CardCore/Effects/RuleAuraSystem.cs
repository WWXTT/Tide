using System.Collections.Generic;
using System.Linq;
using CardCore.Attribute;
using Cysharp.Threading.Tasks;

namespace CardCore
{
    /// <summary>
    /// 规则光环系统（规则轴路线 B，2026-10-03 用户定案）：
    /// ModifyGameRule 原子（str=规则短名）挂在结界卡的登场效果上——载体入场结算时 Activate，
    /// 此后该规则**对双方同时生效**（规则修改是非对称优势的对称面——CR 613.10 口径）。
    ///
    /// - 全局唯一槽：任一新规则光环激活时，旧光环的载体直送墓地（新的登场把旧的送墓——用户定案）。
    /// - 持续永久 + 载体可被摧毁/无效：活性=实时查询（载体在战场且存活）——载体送墓/被摧毁
    ///   （结界耐久归零 Smashed，V9.c 管线）/被无效发动（不入场）即失效，无需注销逻辑
    ///   （RuleHooks「状态无关件实时查询」惯例，同旧 RitualAuras 设计）。
    /// - 无方向箭头：不是 LinkAura（连接光环），规则光环不占连接位、不写 arrows。
    /// - 局重置回收（GameCore.Reset 组合根：槽位/各规则状态清空 + 替代件重挂——
    ///   ReplacementEngine.ClearAll 之后）。
    /// </summary>
    public static class RuleAuraSystem
    {
        /// <summary>当前激活的规则光环（查询口：UI/战报展示；null=无）。</summary>
        public sealed class ActiveRuleAura
        {
            public string RuleId;
            public Card Carrier;
            public Player Controller;
        }

        private static ActiveRuleAura _active;
        private static bool _registered;

        public static ActiveRuleAura Active => _active;

        /// <summary>规则是否生效（实时查询：槽位匹配 + 载体在战场且存活）。</summary>
        public static bool IsActive(string ruleId)
            => _active != null
               && !string.IsNullOrEmpty(ruleId)
               && _active.RuleId == ruleId
               && _active.Carrier != null
               && _active.Carrier.IsAlive
               && _active.Carrier.GetZone() == Zone.Battlefield;

        /// <summary>
        /// 激活规则光环（ModifyGameRuleHandler 调）：全局唯一——旧载体送墓（DestroyReason.Destroyed）
        /// 再登记新槽。同一载体重复激活不触发替换（幂等重登记）。
        /// </summary>
        public static void Activate(string ruleId, Card carrier, Player controller)
        {
            if (string.IsNullOrEmpty(ruleId) || carrier == null) return;

            var old = _active;
            if (old != null && old.Carrier != null && !ReferenceEquals(old.Carrier, carrier))
            {
                var oldOwner = old.Carrier.GetController();
                var core = GameCore.Instance;
                if (core?.ZoneManager != null && oldOwner != null
                    && old.Carrier.GetZone() == Zone.Battlefield)
                    core.ZoneManager.MoveCard(old.Carrier, oldOwner, Zone.Battlefield, Zone.Graveyard);
                EventManager.Instance.Publish(new CardDestroyEvent
                {
                    DestroyedCard = old.Carrier,
                    Reason = DestroyReason.Destroyed,
                    Source = carrier
                });
            }

            _active = new ActiveRuleAura { RuleId = ruleId, Carrier = carrier, Controller = controller };
            RuleAuraComponents.OnAuraChanged();

            EventManager.Instance.Publish(new KeywordAppliedEvent
            {
                Target = carrier,
                Keyword = "规则光环",
                Detail = $"规则光环激活：{ruleId}（对双方生效；新光环登场会把旧光环载体送墓）",
                Source = controller,
            });
        }

        /// <summary>组合根登记（GameCore.Reset 调，幂等）：规则组件一次性挂各自钩子（进程 lifetime）。</summary>
        public static void EnsureRegistered()
        {
            if (_registered) return;
            _registered = true;
            RuleAuraComponents.EnsureRegistered();
        }

        /// <summary>局重置（GameCore.Reset 调）：槽位与各规则状态跨局不残留。</summary>
        public static void Reset()
        {
            _active = null;
            RuleAuraComponents.Reset();
        }

        /// <summary>对局重挂（ReplacementEngine.ClearAll 之后调，GameCore.Reset 内时序在后）：
        /// 状态无关替代件（伤害帽/疲劳免疫）逐局重注册。</summary>
        public static void OnGameReset()
            => RuleAuraComponents.OnGameReset();
    }

    /// <summary>
    /// 七条规则组件（规则轴首批=全部旧仪式，2026-10-03 定案；疾风改为「每个玩家连续进行两个回合」）。
    /// 状态无关惯例（同旧 RitualAuras / RuleHooks 注释约定）：钩子进程内一次注册，
    /// 活性实时查询 RuleAuraSystem.IsActive——规则开关即载体存亡，无逐条启停。
    /// 规则短名 = 原子实例 str 的合法值，与原子表 7 行（EffectType=ModifyGameRule）一一对应。
    /// </summary>
    public static class RuleAuraComponents
    {
        // ---- 规则短名（str 合法值——与原子表 7 行对应） ----
        public const string ElementConversion = "ElementConversion"; // 三相仪典
        public const string BloodPact = "BloodPact";                 // 血偿仪典
        public const string DamageCap = "DamageCap";                 // 丰盈仪典
        public const string LockRevealed = "LockRevealed";           // 窥渊仪典
        public const string GraveyardPlay = "GraveyardPlay";         // 归土仪典
        public const string DoubleTurn = "DoubleTurn";               // 疾风仪典（改：双人连两回合）
        public const string HandLimitNoFatigue = "HandLimitNoFatigue"; // 纳川仪典

        private static bool _registered;

        // 三相：各玩家各纯色的累计消耗（满 3 兑换；Player 键随局重置回收）
        private static readonly Dictionary<Player, Dictionary<ManaType, int>> _spendCounters
            = new Dictionary<Player, Dictionary<ManaType, int>>();
        // 窥渊：本回合被锁定的卡（回合结束清）
        private static readonly HashSet<Card> _lockedThisTurn = new HashSet<Card>();
        // 窥渊：锁定的合法回合计数（人类异步提示跨回合竞态弃权用）
        private static int _lockTurnNumber = -1;
        // 归土：本回合已用掉墓地出牌配额的玩家（回合结束清）
        private static readonly HashSet<Player> _graveQuotaUsed = new HashSet<Player>();
        // 疾风：连击跟踪（最近回合玩家 + 其连续回合数）
        private static Player _doubleTurnLastPlayer;
        private static int _doubleTurnConsecutive;

        /// <summary>卡是否本回合被窥渊锁定（不可使用）——出牌限制与 UI 共用查询口。</summary>
        public static bool IsLockedThisTurn(Card card)
            => card != null && RuleAuraSystem.IsActive(LockRevealed) && _lockedThisTurn.Contains(card);

        /// <summary>血偿转嫁是否生效（LifeLossHandler「流失自己=支付」口查询）。</summary>
        public static bool BloodPactRedirectActive => RuleAuraSystem.IsActive(BloodPact);

        public static void EnsureRegistered()
        {
            if (_registered) return;
            _registered = true;

            // 三相：己方消耗累计（出牌支付与效果元素费两路发布的 ElementPoolPayEvent 都算）
            EventManager.Instance.Subscribe<ElementPoolPayEvent>(OnElementPaid);
            // 窥渊：回合开始锁定；回合结束清锁/清配额 + 疾风第二回合授予（同订阅分发）
            EventManager.Instance.Subscribe<TurnStartEvent>(OnTurnStart);
            EventManager.Instance.Subscribe<TurnEndEvent>(OnTurnEnd);
            // 归土：墓地作为出牌来源（PlayCard 来源区配额——TryBeginUse 扣，回合结束清）
            RuleHooks.RegisterPlaySource(new GraveyardPlaySource());
            // 窥渊：锁定经出牌限制钩子接入 PlayCard/PlayCardInResponse
            RuleHooks.RegisterPlayRestriction(new LockedCardRestriction());
            // 纳川：手牌上限 7→15（修改链，实时查询）
            RuleHooks.RegisterHandLimitModifier(new HandLimitModifier());
        }

        public static void Reset()
        {
            _spendCounters.Clear();
            _lockedThisTurn.Clear();
            _graveQuotaUsed.Clear();
            _doubleTurnLastPlayer = null;
            _doubleTurnConsecutive = 0;
            _lockTurnNumber = -1;
        }

        /// <summary>光环切换（Activate 转发）：疾风连击计数从头数（激活当下所在回合=该玩家第 1 回合）。</summary>
        public static void OnAuraChanged()
        {
            _doubleTurnLastPlayer = null;
            _doubleTurnConsecutive = 0;
        }

        /// <summary>对局重挂：状态无关替代件（ReplacementEngine.ClearAll 之后由 GameCore.Reset 调）。</summary>
        public static void OnGameReset()
        {
            var engine = GameCore.Instance?.ReplacementEngine;
            if (engine == null) return;
            engine.RegisterReplacementEffect(new DamageCapReplacement(), typeof(DamageEvent));
            engine.RegisterReplacementEffect(new FatigueImmunityReplacement(), typeof(FatigueEvent));
        }

        // ============ 三相仪典：每消耗 3 点同色纯元素 → 红/蓝/绿各 1（双方各自计数） ============

        private static void OnElementPaid(ElementPoolPayEvent e)
        {
            var core = GameCore.Instance;
            if (core == null || e?.Player == null || e.PaidCost == null) return;
            if (!RuleAuraSystem.IsActive(ElementConversion)) return;

            if (!_spendCounters.TryGetValue(e.Player, out var perColor))
            {
                perColor = new Dictionary<ManaType, int>();
                _spendCounters[e.Player] = perColor;
            }
            foreach (var kv in e.PaidCost)
            {
                var color = (ManaType)kv.Key;
                if (color != ManaType.Red && color != ManaType.Blue && color != ManaType.Green) continue;

                perColor.TryGetValue(color, out var count);
                count += (int)kv.Value;
                while (count >= 3)
                {
                    count -= 3;
                    GrantTrinity(core, e.Player);
                }
                perColor[color] = count;
            }
        }

        /// <summary>红/蓝/绿各 +1 入 bank（AddManaHandler 先例：直写 AvailableMana + 发 AddManaEvent）。</summary>
        private static void GrantTrinity(GameCore core, Player player)
        {
            var bank = core.ElementPool.GetPool(player).AvailableMana;
            foreach (var color in new[] { ManaType.Red, ManaType.Blue, ManaType.Green })
            {
                bank.TryGetValue(color, out var cur);
                bank[color] = cur + 1;
                EventManager.Instance.Publish(new AddManaEvent
                {
                    Player = player,
                    ManaType = color,
                    Amount = 1,
                    Source = RuleAuraSystem.Active?.Carrier,
                });
            }
        }

        // ============ 窥渊仪典：每回合开始，回合玩家锁定对手一张已展示的卡至回合结束 ============

        private static void OnTurnStart(TurnStartEvent e)
        {
            if (!RuleAuraSystem.IsActive(LockRevealed)) return;
            var locker = e.TurnPlayer;
            var victim = locker?.Opponent;
            if (victim == null) return;

            var candidates = RevealRules.GetExposedCards(GameCore.Instance?.ZoneManager, victim, Zone.Hand);
            if (candidates.Count == 0) return;

            if (locker.IsAI || TargetSelectionService.Current == null)
            {
                _lockTurnNumber = e.TurnNumber;
                _lockedThisTurn.Add(candidates[0]); // AI/无头：锁第一张
                return;
            }

            PromptLockAsync(locker, candidates, e.TurnNumber).Forget();
        }

        private static async UniTask PromptLockAsync(Player locker, List<Card> candidates, int turnNumber)
        {
            var labels = new List<string> { "不指定" };
            labels.AddRange(candidates.Select(c => c is IHasName named ? named.CardName : c.ToString()));
            int idx = await TargetSelectionService.RequestOneIndexAsync(
                locker, labels, "锁定对手一张已展示的卡（本回合不可使用）");
            if (_lockTurnNumber != turnNumber) return; // 提示跨回合：弃权
            if (idx >= 1 && idx <= candidates.Count)
                _lockedThisTurn.Add(candidates[idx - 1]);
        }

        // ============ 回合结束：清锁/清配额 + 疾风（每个玩家连续进行两个回合） ============

        private static void OnTurnEnd(TurnEndEvent e)
        {
            _lockedThisTurn.Clear();
            _graveQuotaUsed.Clear();

            if (!RuleAuraSystem.IsActive(DoubleTurn))
            {
                _doubleTurnLastPlayer = null;
                _doubleTurnConsecutive = 0;
                return;
            }

            // 当前结束者未连满 2 回合 → 授予一次额外回合（GrantExtraTurn 只对当前回合玩家生效；
            // TurnEndEvent 在 ResolveNextTurnPlayer 之前发布——先授予后折返，AABB 交替成立）
            if (e.TurnPlayer == _doubleTurnLastPlayer) _doubleTurnConsecutive++;
            else
            {
                _doubleTurnLastPlayer = e.TurnPlayer;
                _doubleTurnConsecutive = 1;
            }
            if (_doubleTurnConsecutive < 2)
                GameCore.Instance?.TurnEngine?.GrantExtraTurn(e.TurnPlayer);
        }

        // ============ 归土仪典：每回合一次，墓地视手牌中使用 ============

        private sealed class GraveyardPlaySource : IPlaySource
        {
            public Zone SourceZone => Zone.Graveyard;
            // 对双方生效：规则活着=双方可用地来源（配额各自独立——TryBeginUse 扣）
            public bool CanUse(Player player) => RuleAuraSystem.IsActive(GraveyardPlay);
            public bool TryBeginUse(Player player)
                => CanUse(player) && !_graveQuotaUsed.Contains(player) && _graveQuotaUsed.Add(player);
        }

        // ============ 窥渊锁定：出牌限制（PlayCard / PlayCardInResponse 同门） ============

        private sealed class LockedCardRestriction : IPlayRestriction
        {
            public bool CanPlay(GameCore core, Player player, Card card, Zone fromZone)
                => !_lockedThisTurn.Contains(card); // 锁定集只在规则活跃时有内容（回合结束全清）
        }

        // ============ 纳川仪典：手牌上限 15 ============

        private sealed class HandLimitModifier : IHandLimitModifier
        {
            public int Modify(Player player, int currentLimit)
                => RuleAuraSystem.IsActive(HandLimitNoFatigue) ? 15 : currentLimit;
        }

        // ============ 丰盈仪典：角色单次伤害封顶 5（替代件——逐局重挂） ============

        private sealed class DamageCapReplacement : ReplacementEffectBase
        {
            public const int CapValue = 5;

            public DamageCapReplacement() : base(typeof(DamageEvent), "RuleAuraDamageCap") { }

            public override bool CanReplace(IGameEvent e)
                => e is DamageEvent d
                   && d.Amount > CapValue
                   && d.Target is Player
                   && RuleAuraSystem.IsActive(DamageCap);

            public override IGameEvent CreateReplacement(IGameEvent originalEvent, Effect sourceEffect)
                => originalEvent is DamageEvent d
                    ? new DamageEvent { Source = d.Source, Target = d.Target, Amount = CapValue }
                    : null;
        }

        // ============ 纳川仪典：免疫空库抽牌疲劳（替代件——逐局重挂） ============

        private sealed class FatigueImmunityReplacement : ReplacementEffectBase
        {
            public FatigueImmunityReplacement() : base(typeof(FatigueEvent), "RuleAuraFatigueImmunity") { }

            public override bool CanReplace(IGameEvent e)
                => e is FatigueEvent f
                   && f.Damage > 0
                   && f.Player != null
                   && RuleAuraSystem.IsActive(HandLimitNoFatigue);

            public override IGameEvent CreateReplacement(IGameEvent originalEvent, Effect sourceEffect)
                => originalEvent is FatigueEvent f
                    ? new FatigueEvent { Player = f.Player, Damage = 0 }
                    : null;
        }
    }
}
