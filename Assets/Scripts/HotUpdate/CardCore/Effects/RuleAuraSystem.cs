using System.Collections.Generic;
using System.Linq;
using CardCore.Attribute;

namespace CardCore
{
    /// <summary>
    /// 规则光环系统（规则轴路线 B，2026-10-03 用户定案）：
    /// ModifyGameRule 原子（str=规则短名）挂在结界卡的登场效果上——载体入场结算时 Activate，
    /// 此后该规则**对双方同时生效**（规则修改是非对称优势的对称面——CR 613.10 口径）。
    ///
    /// - 全局唯一槽：任一新规则光环激活时，旧光环的载体直送墓地（新的登场把旧的送墓——用户定案）。
    /// - 持续永久 + 载体可被摧毁/无效：活性=实时查询（载体在战场且存活）——载体送墓/被摧毁
    ///   （结界耐久归零 Smashed，V9.c 管线）/被效果无效（不入场）即失效，无需注销逻辑
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

        /// <summary>战斗改写仪典的持有者侧命中查询（2026-10-05 定案，仅持有者生效）：
        /// 当前激活的改写光环存活、且 source 是**光环控制者**的卡 → 返回对应改写关键词 id
        ///（施加/审计复用 KeywordRules 口径）；否则 null。固定序=毒&gt;冻&gt;眠&gt;疫由常量唯一性天然保证
        ///（全局唯一槽同一时刻至多一条改写光环）。</summary>
        public static string HolderRewriteFor(Card source)
        {
            if (_active == null || source == null || !IsActive(_active.RuleId)) return null;
            var owner = source.GetController();
            if (owner == null || !ReferenceEquals(owner, _active.Controller)) return null;
            switch (_active.RuleId)
            {
                case RuleAuraComponents.CombatToxin: return Attribute.KeywordRules.PoisonSting;
                case RuleAuraComponents.CombatFreeze: return Attribute.KeywordRules.IceCrystal;
                case RuleAuraComponents.CombatSleep: return Attribute.KeywordRules.Nightmare;
                case RuleAuraComponents.CombatVenom: return Attribute.KeywordRules.Pathogen;
                default: return null;
            }
        }

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
                Detail = $"规则光环激活：{ruleId}（{RuleAuraScopeZh(ruleId)}；新光环登场会把旧光环载体送墓）",
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

        /// <summary>窥渊回合末结算（GameCore.OnTurnEnded 于 CounterRules.OnTurnEnd 之后显式调）：
        /// 2026-10-04 时机改版（回合开始→回合结束）——随机展示+锁定排在指示物倒数之后，
        /// 同回合末新挂的锁不被 ③ 块吞层。</summary>
        public static void RevealAndLockAtTurnEnd(Player turnPlayer)
            => RuleAuraComponents.RevealAndLockAtTurnEnd(turnPlayer);

        /// <summary>规则光环作用域中文（播报/UI 共用）：改写仪典四条=仅持有者生效；其余=对双方生效。</summary>
        public static string RuleAuraScopeZh(string ruleId)
            => RuleAuraComponents.IsHolderScoped(ruleId) ? "仅持有者生效" : "对双方生效";
    }

    /// <summary>
    /// 七条规则组件（规则轴首批=全部旧仪式，2026-10-03 定案；疾风改为「每个玩家连续进行两个回合」）。
    /// 状态无关惯例（同旧 RitualAuras / RuleHooks 注释约定）：钩子进程内一次注册，
    /// 活性实时查询 RuleAuraSystem.IsActive——规则开关即载体存亡，无逐条启停。
    /// 规则短名 = 原子实例 str 的合法值，与原子表 7 行（EffectType=ModifyGameRule）一一对应。
    /// </summary>
    public static class RuleAuraComponents
    {
        // ---- 规则短名（str 合法值——与原子表规则光环族行对应；2026-10-04 光环改造后 9 行） ----
        public const string ElementConversion = "ElementConversion"; // 三相仪典
        public const string BloodPact = "BloodPact";                 // 血偿仪典（2026-10-04 改造：己方回合角色受伤→对手角色承担）
        public const string HealOverflow = "HealOverflow";           // 丰盈仪典（2026-10-04 改造：回复溢出→生命上限+1）
        public const string DamageCap = "DamageCap";                 // 离散仪典（2026-10-04 蚕褪改版：单次伤害>5→5、<5→3；短名承袭存量卡数据）
        public const string LockRevealed = "LockRevealed";           // 窥渊仪典
        public const string GraveyardPlay = "GraveyardPlay";         // 归土仪典
        public const string CastSpeedUp = "CastSpeedUp";             // 疾风仪典（2026-10-04 改造：从手牌使用的卡发动速度+1）
        public const string DoubleTurn = "DoubleTurn";               // 轮回仪典（2026-10-04 承接原疾风：双人连两回合）
        public const string HandLimitNoFatigue = "HandLimitNoFatigue"; // 纳川仪典

        // ---- 战斗改写仪典（2026-10-05 定案：四条改写从有限分支改写门迁唯一光环；仅持有者生效——
        // 与既有 9 条"对双方生效"不同，首个单侧语义：只改写光环控制者的生物造成的战斗伤害。
        // 命中查询收口 RuleAuraSystem.HolderRewriteFor；施加口径复用 KeywordRules.ApplyRewriteCounter。）----
        public const string CombatToxin = "CombatToxin";             // 毒蚀仪典（毒素指示物）
        public const string CombatFreeze = "CombatFreeze";           // 霜蚀仪典（冻结指示物）
        public const string CombatSleep = "CombatSleep";             // 眠蚀仪典（沉睡指示物）
        public const string CombatVenom = "CombatVenom";             // 疫蚀仪典（剧毒指示物）

        /// <summary>是否"仅持有者生效"的规则光环（战斗改写仪典四条；其余=对双方生效）。
        /// UI 标签与播报文案按此区分。</summary>
        public static bool IsHolderScoped(string ruleId)
            => ruleId == CombatToxin || ruleId == CombatFreeze
               || ruleId == CombatSleep || ruleId == CombatVenom;

        private static bool _registered;

        // 三相：各玩家各纯色的累计消耗（满 3 兑换；Player 键随局重置回收）
        private static readonly Dictionary<Player, Dictionary<ManaType, int>> _spendCounters
            = new Dictionary<Player, Dictionary<ManaType, int>>();
        // 窥渊（2026-10-04 原子化+时机改版）：锁定不走本类回合级 HashSet——回合结束（指示物倒数之后）
        // 赋予对手被展示卡「锁定」指示物×1（LockCounter，层数=剩余回合，持有者回合末倒数；
        // 手牌区同样结算——CounterRules 持有者侧结算域含手牌）。锁定独立于光环存续
        //（载体离场不清既有指示物，自然倒数到归零）。
        // 归土：本回合已用掉墓地出牌配额的玩家（回合结束清）
        private static readonly HashSet<Player> _graveQuotaUsed = new HashSet<Player>();
        // 疾风：连击跟踪（最近回合玩家 + 其连续回合数）
        private static Player _doubleTurnLastPlayer;
        private static int _doubleTurnConsecutive;

        /// <summary>卡是否被锁定（不可使用）——出牌限制与 UI 共用查询口。
        /// 2026-10-04 原子化：读锁定指示物（LockCounter&gt;0）——不再要求光环活跃
        ///（锁定独立于载体存续，持有者回合末逐层倒数）。</summary>
        public static bool IsLockedThisTurn(Card card)
            => card != null && card.GetCounterCount(Attribute.CounterRules.LockCounter) > 0;

        /// <summary>从手牌使用的卡发动速度加成（疾风仪典 2026-10-04 改造）：+1 经
        /// SpeedCalculator.GetCardCastSpeed 单源作用（出牌/响应出牌/AI 预检/速度门同口径）。</summary>
        public static int CardCastSpeedBonus => RuleAuraSystem.IsActive(CastSpeedUp) ? 1 : 0;

        public static void EnsureRegistered()
        {
            if (_registered) return;
            _registered = true;

            // 三相：己方消耗累计（出牌支付与效果元素费两路发布的 ElementPoolPayEvent 都算）
            EventManager.Instance.Subscribe<ElementPoolPayEvent>(OnElementPaid);
            // 回合结束清配额 + 疾风第二回合授予（同订阅分发）；
            // 窥渊展示/锁定 2026-10-04 时机改版后不经此——由 GameCore.OnTurnEnded 在指示物倒数后显式调
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
            _graveQuotaUsed.Clear();
            _doubleTurnLastPlayer = null;
            _doubleTurnConsecutive = 0;
        }

        /// <summary>光环切换（Activate 转发）：疾风连击计数从头数（激活当下所在回合=该玩家第 1 回合）。</summary>
        public static void OnAuraChanged()
        {
            _doubleTurnLastPlayer = null;
            _doubleTurnConsecutive = 0;
        }

        /// <summary>对局重挂：状态无关替代件（ReplacementEngine.ClearAll 之后由 GameCore.Reset 调）。
        /// 2026-10-04 光环改造：离散（原蚕褪：伤害二值离散）+ 血偿（己方回合角色受伤转对手承担）入列。</summary>
        public static void OnGameReset()
        {
            var engine = GameCore.Instance?.ReplacementEngine;
            if (engine == null) return;
            engine.RegisterReplacementEffect(new DamageCapReplacement(), typeof(DamageEvent));
            engine.RegisterReplacementEffect(new BloodPactDamageRedirectReplacement(), typeof(DamageEvent));
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

        // ============ 窥渊仪典（2026-10-04 时机改版）：每回合结束，随机展示对手一张手牌 + 被展示的卡一回合锁定 ============
        // 触发口=GameCore.OnTurnEnded 组合根显式调，位于 CounterRules.OnTurnEnd 指示物倒数**之后**——
        // 同回合末新挂的锁不被 ③ 块倒数吞层。不走事件订阅：订阅序相对 GameCore.OnTurnEnded 随局数漂移，先后无保证。

        internal static void RevealAndLockAtTurnEnd(Player turnPlayer)
        {
            if (!RuleAuraSystem.IsActive(LockRevealed)) return;
            var locker = turnPlayer;
            var victim = locker?.Opponent;
            if (victim == null) return;
            var zm = GameCore.Instance?.ZoneManager;
            if (zm == null) return;

            var carrier = RuleAuraSystem.Active?.Carrier;

            // ① 随机展示对手一张**未展示**手牌（自给展示源——RevealCard 原子同款口径，
            //    二值不叠层；全展示/空手 = 无新展示，锁定照常对既有被展示卡生效）
            var unrevealed = zm.GetCards(victim, Zone.Hand)
                .Where(c => c != null && c.IsAlive && c.GetCounterCount(Attribute.CounterRules.ExposedCounter) == 0)
                .ToList();
            if (unrevealed.Count > 0)
            {
                var pick = unrevealed[GameRng.Next(0, unrevealed.Count)];
                pick.AddCounters(Attribute.CounterRules.ExposedCounter, 1, carrier);
                EventManager.Instance.Publish(new CounterChangedEvent
                {
                    Target = pick,
                    CounterType = Attribute.CounterRules.ExposedCounter,
                    Amount = 1,
                    Source = carrier,
                });
                EventManager.Instance.Publish(new RevealCardsEvent
                {
                    Player = victim,
                    Cards = new List<Card> { pick },
                    Source = carrier,
                });
            }

            // ② 赋予对手被展示的卡一回合锁定指示物（含①刚展示的——同回合末展示并锁定）。
            // 被展示的卡**全部**锁定（无选择窗口——指示物即机制）。
            // 层数=剩余回合：持有者整个回合锁使用，持有者回合末倒数归零（倒数先于本挂层——新锁完整活一回合）。
            var revealed = RevealRules.GetExposedCards(zm, victim, Zone.Hand);
            if (revealed.Count == 0) return;

            foreach (var card in revealed)
            {
                card.AddCounters(Attribute.CounterRules.LockCounter, 1, carrier);
                EventManager.Instance.Publish(new CounterChangedEvent
                {
                    Target = card,
                    CounterType = Attribute.CounterRules.LockCounter,
                    Amount = 1,
                    Source = carrier,
                });
            }
        }

        // ============ 回合结束：清配额 + 疾风（每个玩家连续进行两个回合） ============
        // 窥渊锁定不再在此清（2026-10-04 原子化）：指示物经 CounterRules.OnTurnEnd ③ 块
        // 持有者回合末逐层倒数，自然消退。

        private static void OnTurnEnd(TurnEndEvent e)
        {
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
        // 2026-10-04 原子化：读锁定指示物（独立于光环存续——光环死了锁照常倒数到归零）

        private sealed class LockedCardRestriction : IPlayRestriction
        {
            public bool CanPlay(GameCore core, Player player, Card card, Zone fromZone)
                => card == null || card.GetCounterCount(Attribute.CounterRules.LockCounter) == 0;
        }

        // ============ 纳川仪典：手牌上限 15 ============

        private sealed class HandLimitModifier : IHandLimitModifier
        {
            public int Modify(Player player, int currentLimit)
                => RuleAuraSystem.IsActive(HandLimitNoFatigue) ? 15 : currentLimit;
        }

        // ============ 离散仪典（原蚕褪，2026-10-04 改版）：单次伤害二值离散——
        // 超过 5 改为 5、低于 5 改为 3（恰为 3/5 不动；生物与角色同门） ============

        private sealed class DamageCapReplacement : ReplacementEffectBase
        {
            public const int CapValue = 5;
            public const int FloorValue = 3;

            public DamageCapReplacement() : base(typeof(DamageEvent), "RuleAuraDamageCap") { }

            public override bool CanReplace(IGameEvent e)
                => e is DamageEvent d
                   && d.Amount > 0
                   && d.Amount != CapValue && d.Amount != FloorValue // 恰 3/5 不改写；1/2/4→3、6+→5
                   && (d.Target is Player
                       || (d.Target is Card dc && dc.IsLivingUnit())) // 生物（活体单位）与角色同门
                   && RuleAuraSystem.IsActive(DamageCap);

            public override IGameEvent CreateReplacement(IGameEvent originalEvent, Effect sourceEffect)
                => originalEvent is DamageEvent d
                    ? new DamageEvent
                    {
                        Source = d.Source,
                        Target = d.Target,
                        Amount = d.Amount > CapValue ? CapValue : FloorValue,
                    }
                    : null;
        }

        // ============ 血偿仪典：己方回合中，回合方角色受到的伤害改由对手角色承担（2026-10-04 改造） ============
        // 旧语义（自己支付的生命代价改由对手支付——LifeLossHandler 转嫁）已退役；
        // 新语义=DamageEvent 替代件：任意来源伤害（战斗+效果），受伤者=当前回合玩家角色 → 同额改由其对手承担。

        private sealed class BloodPactDamageRedirectReplacement : ReplacementEffectBase
        {
            public BloodPactDamageRedirectReplacement() : base(typeof(DamageEvent), "RuleAuraBloodPactRedirect") { }

            public override bool CanReplace(IGameEvent e)
            {
                if (!RuleAuraSystem.IsActive(BloodPact)) return false;
                if (!(e is DamageEvent d) || d.Amount <= 0) return false;
                // 己方回合判定：受伤者=当前回合玩家的角色（对手回合打我=不转移，回合对称）
                var core = GameCore.Instance;
                return d.Target is Player victim
                       && core?.TurnEngine?.TurnPlayer != null
                       && ReferenceEquals(victim, core.TurnEngine.TurnPlayer);
            }

            public override IGameEvent CreateReplacement(IGameEvent originalEvent, Effect sourceEffect)
                => originalEvent is DamageEvent d && d.Target is Player victim
                    ? new DamageEvent { Source = d.Source, Target = victim.Opponent, Amount = d.Amount }
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
