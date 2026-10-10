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
    /// <summary>规则光环作用范围（2026-10-07 极性门控定案）：存 ModifyGameRule 原子步 entry.value；
    /// 己方=仅光环控制者一侧，对方=仅其对手一侧，双方=两侧（缺省 1=双方，兼容存量仪典步 value=1）。
    /// 合成器选项按表行 Polarity 门控：+1 只 {己方,双方}、-1 只 {对方,双方}、0 三选；
    /// 计价沿用双方让利口径（CostDerivation.IsSymmetricBothSides）：双方 ×0.5、单侧全价。</summary>
    public enum RuleAuraScope
    {
        Own = 0,      // 仅己方（光环控制者一侧）
        Both = 1,     // 双方（缺省）
        Opponent = 2, // 仅对方（光环控制者的对手一侧）
    }

    public static class RuleAuraSystem
    {
        /// <summary>当前激活的规则光环（查询口：UI/战报展示；null=无）。</summary>
        public sealed class ActiveRuleAura
        {
            public string RuleId;
            public Card Carrier;
            public Player Controller;
            public int Scope = (int)RuleAuraScope.Both; // RuleAuraScope 序（激活时由原子步 Value 传入）
        }

        private static readonly System.Collections.Generic.Dictionary<string, ActiveRuleAura> _active
            = new System.Collections.Generic.Dictionary<string, ActiveRuleAura>();
        private static bool _registered;

        /// <summary>当前激活的规则光环（查询口：验证夹具/战报展示；每条仪典各一槽——异名共存）。</summary>
        public static System.Collections.Generic.IReadOnlyList<ActiveRuleAura> ActiveRules
            => _active.Values.ToList();

        /// <summary>指定仪典的活跃载体（null=未激活/已失效；窥渊展示源等按规则取载体）。</summary>
        public static Card CarrierOf(string ruleId)
            => IsActive(ruleId) ? _active[ruleId].Carrier : null;

        /// <summary>指定仪典的活跃控制者（null=未激活；舍身转投目标=其 Opponent）。</summary>
        public static Player ControllerOf(string ruleId)
            => IsActive(ruleId) ? _active[ruleId].Controller : null;

        /// <summary>槽位活性（ruleId 匹配 + 载体在战场且存活——离场/死亡自然失效，无需显式移除）。</summary>
        private static bool Matches(ActiveRuleAura s, string ruleId)
            => s != null && s.RuleId == ruleId && s.Carrier != null && s.Carrier.IsAlive
               && s.Carrier.GetZone() == Zone.Battlefield;

        /// <summary>规则是否生效（实时查询；2026-10-07 唯一性改版：异名共存——按 ruleId 各自独立槽）。</summary>
        public static bool IsActive(string ruleId)
            => !string.IsNullOrEmpty(ruleId)
               && _active.TryGetValue(ruleId, out var s) && Matches(s, ruleId);

        /// <summary>作用范围命中（2026-10-07 范围化）：规则活跃且 p 一侧在生效范围内。
        /// 各规则消费点按「规则作用于谁」传对应玩家——三相=支付者、轮回=回合玩家、
        /// 离散/丰盈=受击/受疗方、血偿=受伤回合方、疾风=出牌者、负面光环族=受咒方。</summary>
        public static bool ScopeHits(string ruleId, Player p)
        {
            if (p == null || !IsActive(ruleId)) return false;
            var s = _active[ruleId];
            var scope = (RuleAuraScope)s.Scope;
            if (scope == RuleAuraScope.Both) return true;
            var ctrl = s.Controller;
            return ctrl == null ? false
                : scope == RuleAuraScope.Opponent
                    ? ReferenceEquals(p, ctrl.Opponent)
                    : ReferenceEquals(p, ctrl);
        }

        /// <summary>战斗伤害改写命中查询（2026-10-07 舍身仪典定案——原毒/冻/眠/疫四改写映射随
        /// 负面光环化退役，改写管线收敛为舍身单映射）：舍身光环存活、且 source 一侧在生效范围
        ///（ScopeHits）→ 返回 CombatRedirect；否则 null。消费方：KeywordRules.ApplyDamage 战斗分支
        ///（改为对光环控制者的对手角色等量伤害）。</summary>
        public static string HolderRewriteFor(Card source)
        {
            if (source == null || !IsActive(RuleAuraComponents.CombatRedirect)) return null;
            var owner = source.GetController();
            if (!ScopeHits(RuleAuraComponents.CombatRedirect, owner)) return null;
            return RuleAuraComponents.CombatRedirect;
        }

        /// <summary>
        /// 激活规则光环（ModifyGameRuleHandler 调；2026-10-07 唯一性改版：**同名禁止、异名共存**——
        /// 同 ruleId 光环已活跃（载体在场）→ 拒绝激活（告警空转，不送墓任何旧光环）；
        /// 异名 → 各自独立槽登记。scope=RuleAuraScope 序（缺省双方）。
        /// 打出侧拦截见 GameActions 同名仪典闸；直投路径（教学 Seeder）由本拒绝兜底。
        /// </summary>
        public static void Activate(string ruleId, Card carrier, Player controller,
            int scope = (int)RuleAuraScope.Both)
        {
            if (string.IsNullOrEmpty(ruleId) || carrier == null) return;

            // 清扫失效槽（载体离场/死亡残留——活性本就可推导，清扫防字典膨胀）
            System.Collections.Generic.List<string> dead = null;
            foreach (var kv in _active)
                if (!Matches(kv.Value, kv.Key))
                    (dead ??= new System.Collections.Generic.List<string>()).Add(kv.Key);
            if (dead != null)
                foreach (var k in dead) _active.Remove(k);

            if (IsActive(ruleId))
            {
                TideLog.Warn($"[RuleAuraSystem] 同名规则光环已在场（{ruleId}）——不重复激活"
                             + "（2026-10-07 唯一性定案：同名禁止、异名共存，不再送墓替换）");
                return;
            }

            _active[ruleId] = new ActiveRuleAura { RuleId = ruleId, Carrier = carrier, Controller = controller, Scope = scope };
            RuleAuraComponents.OnAuraChanged();

            EventManager.Instance.Publish(new KeywordAppliedEvent
            {
                Target = carrier,
                Keyword = "规则光环",
                Detail = $"规则光环激活：{ruleId}（{RuleAuraScopeZh(scope)}；同名在场时不可再打出，载体离场即失效）",
                Source = controller,
            });
        }

        /// <summary>同名仪典打出闸（GameActions.PlayCard/PlayCardInResponse 消费，2026-10-07 唯一性定案）：
        /// 卡的任一原子步 str 指向当前活跃规则光环（同名在场）→ true=禁止打出；异名不受限。</summary>
        public static bool BlocksDuplicatePlay(Card card)
        {
            var data = (card as CardWrapper)?.GetData();
            if (data?.Effects == null || _active.Count == 0) return false;
            foreach (var fx in data.Effects)
            {
                if (fx == null) continue;
                if (fx.AtomicEffects != null)
                    foreach (var a in fx.AtomicEffects)
                        if (a != null && IsActive(a.str)) return true;
                if (fx.Steps != null)
                    foreach (var s in fx.Steps)
                        if (s?.kind == 0 && s.atomic != null && IsActive(s.atomic.str)) return true;
            }
            return false;
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
            _active.Clear();
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

        /// <summary>规则光环作用范围中文（播报/UI 共用，2026-10-07 范围化定案）：按激活时选择的范围。
        ///（原改写仪典"仅持有者生效"硬编码口径退役——运行时一律 ScopeHits 按范围判定。）</summary>
        public static string RuleAuraScopeZh(int scope)
            => scope == (int)RuleAuraScope.Own ? "仅己方生效"
             : scope == (int)RuleAuraScope.Opponent ? "仅对方生效" : "对双方生效";

        /// <summary>旧签名（按规则 id 报文）——合成器 AutoName 存量调用兼容；
        /// 无范围数据时按改写族=己方、其余=双方（与范围化前行为一致）。新代码走 RuleAuraScopeZh(int)。</summary>
        public static string RuleAuraScopeZh(string ruleId)
            => RuleAuraComponents.IsHolderScoped(ruleId) ? "仅己方生效" : "对双方生效";
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

        // ---- 三负面光环仪典（2026-10-07 负面化定案：原"战斗伤害改写为指示物"语义退役，
        // 改持续型挂层——受光环影响的生物按各自触发点叠对应指示物；钩子在本类，代码侧实现）：
        // 毒蚀=受到伤害后叠毒素；霜蚀=攻击后冻结；眠蚀=启动式主动发动后叠沉睡（仅启动式——用户定案）。
        // 疫蚀仪典已删（2026-10-09 剧毒指示物删除后无独立存在意义）。
        // 改写管线收敛为舍身仪典（CombatRedirect）单映射。----
        public const string CombatToxin = "CombatToxin";             // 毒蚀仪典（受到伤害→毒素）
        public const string CombatFreeze = "CombatFreeze";           // 霜蚀仪典（攻击后→冻结）
        public const string CombatSleep = "CombatSleep";             // 眠蚀仪典（启动式发动→沉睡）

        // ---- 战斗伤害改写仪典（2026-10-07 舍身定案：受光环影响的生物造成战斗伤害时，
        // 改为对光环控制者的对手角色等量伤害——战斗伤害不发生。唯一改写映射，见 HolderRewriteFor。）----
        public const string CombatRedirect = "CombatRedirect";       // 舍身仪典（伤害转投对手角色）

        // ---- 治疗改写仪典（2026-10-09，黑8，表行 9dfed4f8，用户命名「暗牧仪典」）：
        // 受光环影响一方的单位与角色受到的治疗改写为等量伤害
        //（改写口=EntityEffectExtensions.Heal 咽喉→TryRewriteHealAsDamage；
        // 范围按受疗方一侧判 ScopeHits——同丰盈/离散的"受疗方"口径）。配套的实体级姊妹机制=
        // 反疗指示物（DepravityCounter，任意有生命单位·消耗层），
        // 共用 IsHealInverted/TryRewriteHealAsDamage。----
        public const string HealInversion = "HealInversion";         // 暗牧仪典（治疗转伤害）

        /// <summary>是否三负面光环族（毒/冻/眠）。缺省范围与极性驱动（负→对方）；
        /// 运行时判定走 ScopeHits，不再读本口（保留供展示/分类）。</summary>
        public static bool IsHolderScoped(string ruleId)
            => ruleId == CombatToxin || ruleId == CombatFreeze
               || ruleId == CombatSleep;

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

        /// <summary>从手牌使用的卡发动速度加成（疾风仪典 2026-10-04 改造；2026-10-07 范围化：
        /// 按出牌者一侧判命中）：+1 经 SpeedCalculator.GetCardCastSpeed 单源作用
        ///（出牌/响应出牌/AI 预检/速度门同口径）。</summary>
        public static int CardCastSpeedBonus(Player caster)
            => RuleAuraSystem.ScopeHits(CastSpeedUp, caster) ? 1 : 0;

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
            // 三负面光环（2026-10-07 负面化定案）：毒蚀走 DamageEvent 分流、
            // 霜蚀走 AttackResolvedEvent（CombatSystem 攻击结算完成发布）、
            // 眠蚀走 EffectExecutionEngine 启动式结算点直调（OnActivatedForSleepAura）
            EventManager.Instance.Subscribe<DamageEvent>(OnDamageForNegativeAuras);
            EventManager.Instance.Subscribe<AttackResolvedEvent>(OnAttackResolvedForFreeze);
        }

        // ============ 三负面光环（2026-10-07 负面化定案：持续型挂层，代码侧实现） ============

        /// <summary>毒蚀钩（DamageEvent）：受光环影响的生物受到伤害 → 受击者叠 1 层毒素
        ///（每回合末受=层数的伤害后减半；毒素自身的回合末伤害会再触发叠层——文本字面语义，
        /// 递增螺旋受减半衰减钳制）。疫蚀分支已删（2026-10-09 剧毒指示物删除后随行退役）。</summary>
        private static void OnDamageForNegativeAuras(DamageEvent e)
        {
            if (!(e?.Target is Card toxinTarget) || !toxinTarget.IsAlive || e.Amount <= 0) return;
            var owner = toxinTarget.GetController();
            if (!RuleAuraSystem.ScopeHits(CombatToxin, owner)) return;
            var carrier = RuleAuraSystem.CarrierOf(CombatToxin);
            toxinTarget.AddCounters(Attribute.CounterRules.ToxinCounter, 1, carrier);
            EventManager.Instance.Publish(new KeywordAppliedEvent
            {
                Target = toxinTarget,
                Keyword = CombatToxin,
                Detail = $"毒蚀光环：{EffectText.Name(toxinTarget)} 受到伤害 → 叠加一层毒素（每回合末受毒伤后减半）",
                Source = carrier,
            });
        }

        /// <summary>霜蚀钩（AttackResolvedEvent）：受光环影响的生物攻击结算后冻结 1 层
        ///（横置+封锁重置，持有者回合末消退——攻击后横置等于锁过下一回合的重置）。</summary>
        private static void OnAttackResolvedForFreeze(AttackResolvedEvent e)
        {
            if (!(e?.Attacker is Card attacker) || !attacker.IsAlive) return;
            var owner = attacker.GetController();
            if (!RuleAuraSystem.ScopeHits(CombatFreeze, owner)) return;
            var carrier = RuleAuraSystem.CarrierOf(CombatFreeze);
            attacker.Freeze(1, carrier);
            EventManager.Instance.Publish(new KeywordAppliedEvent
            {
                Target = attacker,
                Keyword = CombatFreeze,
                Detail = $"霜蚀光环：{EffectText.Name(attacker)} 攻击后冻结（横置锁，持有者回合末消退）",
                Source = carrier,
            });
        }

        /// <summary>眠蚀钩（EffectExecutionEngine 启动式结算完成点直调，2026-10-07 用户定案）：
        /// 受光环影响的生物**仅启动式主动发动**后叠 1 层沉睡（触发式/登场不算——
        /// 沉睡层自身拦截后续启动式发动+跳过重置，形成自我累积迟滞）。</summary>
        public static void OnActivatedForSleepAura(Card activator)
        {
            if (activator == null || !activator.IsAlive) return;
            var owner = activator.GetController();
            if (!RuleAuraSystem.ScopeHits(CombatSleep, owner)) return;
            var carrier = RuleAuraSystem.CarrierOf(CombatSleep);
            activator.AddCounters(Attribute.KeywordRules.SleepCounter, 1, carrier);
            EventManager.Instance.Publish(new KeywordAppliedEvent
            {
                Target = activator,
                Keyword = CombatSleep,
                Detail = $"眠蚀光环：{EffectText.Name(activator)} 启动式发动 → 叠加一层沉睡（拦截后续启动式+跳过重置）",
                Source = carrier,
            });
        }

        // ============ 反疗改写（2026-10-09 暗牧仪典 HealInversion + 反疗指示物 DepravityCounter 共用单源） ============

        /// <summary>反疗命中查询（只读）：目标持反疗层（任意有生命单位）或暗牧光环活跃且受疗方一侧
        /// 在范围内。消费方：TargetFilterSystem.DamagedFilter（满血候选豁免）与引擎错边豁免快照
        ///（执行前取——改写会消耗反疗层，执行后查不回）。改写本体见 TryRewriteHealAsDamage。</summary>
        public static bool IsHealInverted(Entity target)
        {
            if (target == null || !target.IsAlive) return false;
            if (target.GetCounterCount(Attribute.CounterRules.DepravityCounter) > 0)
                return true;
            var side = target is Player p ? p : (target as Card)?.GetController();
            return side != null && RuleAuraSystem.ScopeHits(HealInversion, side);
        }

        // 重入闸：改写产生的伤害若再触发治疗（吸血等回环），嵌套治疗不再改写——封死自递归
        private static bool _inHealRewrite;

        /// <summary>反疗改写口（EntityEffectExtensions.Heal 咽喉最前调用）：治疗改写为等量伤害。
        /// ① 反疗指示物（实体级优先，任意有生命单位）：消耗 1 层，伤害光源=施加方（GetCounterSource）；
        /// ② 暗牧仪典（光环级）：受疗方一侧 ScopeHits 命中，伤害光源=光环载体。
        /// 伤害走 KeywordRules.ApplyDamage 全管线（圣盾/护甲/坚韧/易损/离散伤害帽自然参与）；
        /// 与丰盈溢出同侧时反疗优先（治疗不发生、无溢出）。命中返回 true（调用方短路）。幂等安全：
        /// 反疗层>0 恒改写一次，光环持续期间恒改写（无层可耗）。</summary>
        public static bool TryRewriteHealAsDamage(Entity target, int amount)
        {
            if (_inHealRewrite || target == null || !target.IsAlive || amount <= 0) return false;

            if (target.GetCounterCount(Attribute.CounterRules.DepravityCounter) > 0)
            {
                var src = target.GetCounterSource(Attribute.CounterRules.DepravityCounter);
                target.RemoveCounters(Attribute.CounterRules.DepravityCounter, 1);
                EventManager.Instance.Publish(new KeywordAppliedEvent
                {
                    Target = target,
                    Keyword = Attribute.CounterRules.DepravityCounter,
                    Detail = $"反疗发作：{EffectText.Name(target)} 受到的治疗改写为 {amount} 点伤害（消耗 1 层）",
                    Source = src,
                });
                _inHealRewrite = true;
                try { target.TakeDamage(amount, src); }
                finally { _inHealRewrite = false; }
                return true;
            }

            var side = target is Player p2 ? p2 : (target as Card)?.GetController();
            if (side != null && RuleAuraSystem.ScopeHits(HealInversion, side))
            {
                var carrier = RuleAuraSystem.CarrierOf(HealInversion);
                EventManager.Instance.Publish(new KeywordAppliedEvent
                {
                    Target = target,
                    Keyword = HealInversion,
                    Detail = $"暗牧光环：{EffectText.Name(target)} 受到的治疗改写为 {amount} 点伤害",
                    Source = carrier,
                });
                _inHealRewrite = true;
                try { target.TakeDamage(amount, carrier); }
                finally { _inHealRewrite = false; }
                return true;
            }

            return false;
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
            if (!RuleAuraSystem.ScopeHits(ElementConversion, e.Player)) return;

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
                    Source = RuleAuraSystem.CarrierOf(ElementConversion),
                });
            }
        }

        // ============ 窥渊仪典（2026-10-04 时机改版）：每回合结束，随机展示对手一张手牌 + 被展示的卡一回合锁定 ============
        // 触发口=GameCore.OnTurnEnded 组合根显式调，位于 CounterRules.OnTurnEnd 指示物倒数**之后**——
        // 同回合末新挂的锁不被 ③ 块倒数吞层。不走事件订阅：订阅序相对 GameCore.OnTurnEnded 随局数漂移，先后无保证。

        internal static void RevealAndLockAtTurnEnd(Player turnPlayer)
        {
            if (!RuleAuraSystem.ScopeHits(LockRevealed, turnPlayer)) return;
            var locker = turnPlayer;
            var victim = locker?.Opponent;
            if (victim == null) return;
            var zm = GameCore.Instance?.ZoneManager;
            if (zm == null) return;

            var carrier = RuleAuraSystem.CarrierOf(LockRevealed);

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

            if (!RuleAuraSystem.ScopeHits(DoubleTurn, e.TurnPlayer))
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
            // 生效范围（2026-10-07 范围化——缺省双方）：范围内的玩家可用地来源（配额各自独立——TryBeginUse 扣）
            public bool CanUse(Player player) => RuleAuraSystem.ScopeHits(GraveyardPlay, player);
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
                => RuleAuraSystem.ScopeHits(HandLimitNoFatigue, player) ? 15 : currentLimit;
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
                   && RuleAuraSystem.ScopeHits(DamageCap,
                       d.Target is Player tp ? tp : (d.Target as Card)?.GetController()); // 受击方一侧在范围内

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
                // 己方回合判定：受伤者=当前回合玩家的角色（对手回合打我=不转移，回合对称）；
                // 范围化（2026-10-07）：受伤回合方一侧须在生效范围内（缺省双方=旧行为）
                var core = GameCore.Instance;
                return d.Target is Player victim
                       && RuleAuraSystem.ScopeHits(BloodPact, victim)
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
                   && RuleAuraSystem.ScopeHits(HandLimitNoFatigue, f.Player);

            public override IGameEvent CreateReplacement(IGameEvent originalEvent, Effect sourceEffect)
                => originalEvent is FatigueEvent f
                    ? new FatigueEvent { Player = f.Player, Damage = 0 }
                    : null;
        }
    }
}
