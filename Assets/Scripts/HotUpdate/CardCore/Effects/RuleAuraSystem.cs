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
    /// 2026-10-29 定案：固定双方规则（IsFixedBothRule——许可 4 条+禁止 4 条）UI 不提供单侧选择，
    /// 改写族保留三档；计价去对称让利（规则光环表价即实价，不再 ×0.5）。</summary>
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

        /// <summary>槽位活性（ruleId 匹配 + 载体在战场且存活——离场/死亡自然失效，无需显式移除；
        /// 2026-10-31 失效改名定案：光环域整体退出压制——载体被「失效」不压光环，
        /// 旧「载体 NullifyCounter 闸」已删，本判定不再读指示物）。</summary>
        private static bool Matches(ActiveRuleAura s, string ruleId)
            => s != null && s.RuleId == ruleId && s.Carrier != null && s.Carrier.IsAlive
               && s.Carrier.GetZone() == Zone.Battlefield;

        /// <summary>规则是否生效（实时查询；2026-10-07 唯一性改版：异名共存——按 ruleId 各自独立槽）。</summary>
        public static bool IsActive(string ruleId)
            => !string.IsNullOrEmpty(ruleId)
               && _active.TryGetValue(ruleId, out var s) && Matches(s, ruleId);

        /// <summary>槽位被活载体占据（失效不压光环后与 Matches 同口径；载体离场/死亡才释放槽位）。</summary>
        private static bool SlotOccupied(string ruleId)
            => _active.TryGetValue(ruleId, out var s) && s.Carrier != null && s.Carrier.IsAlive
               && s.Carrier.GetZone() == Zone.Battlefield;

        /// <summary>作用范围命中（2026-10-07 范围化）：规则活跃且 p 一侧在生效范围内。
        /// 各规则消费点按「规则作用于谁」传对应玩家——三相=支付者、轮回=回合玩家、
        /// 离散/丰盈=受击/受疗方、血偿=受伤回合方、负面光环族=受咒方。</summary>
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

            if (SlotOccupied(ruleId))
            {
                TideLog.Warn($"[RuleAuraSystem] 同一规则全局唯一（双方不能各挂一份；载体离场/死亡才释放槽位）：{ruleId}");
                return;
            }

            _active[ruleId] = new ActiveRuleAura { RuleId = ruleId, Carrier = carrier, Controller = controller, Scope = scope };

            EventManager.Instance.Publish(new KeywordAppliedEvent
            {
                Target = carrier,
                Keyword = "场地光环",
                Detail = $"场地光环激活：{ruleId}（{RuleAuraScopeZh(scope)}；同名在场时不可再打出，载体离场即失效）",
                Source = controller,
            });
        }

        /// <summary>同名仪典打出闸（GameActions.PlayCard/PlayCardInResponse 消费，2026-10-07 唯一性定案）：
        /// 卡的任一光环投放原子步指向当前活跃规则光环（同名在场）→ true=禁止打出；异名不受限。
        /// 2026-10-31 阶段D：规则 id 改从行类型派生（RuleIdOfAtom）；表 EffectType 列未翻新
        /// 期间回落 str=规则短名（空 str 守卫保留——非规则原子不进 SlotOccupied 字典键）。</summary>
        public static bool BlocksDuplicatePlay(Card card)
        {
            var data = (card as CardWrapper)?.GetData();
            if (data?.Effects == null || _active.Count == 0) return false;
            foreach (var fx in data.Effects)
            {
                if (fx == null) continue;
                if (fx.AtomicEffects != null)
                    foreach (var a in fx.AtomicEffects)
                    {
                        var rid = RuleIdOfAtom(a);
                        if (!string.IsNullOrEmpty(rid) && SlotOccupied(rid)) return true;
                    }
                if (fx.Steps != null)
                    foreach (var s in fx.Steps)
                        if (s?.kind == 0)
                        {
                            var rid = RuleIdOfAtom(s.atomic);
                            if (!string.IsNullOrEmpty(rid) && SlotOccupied(rid)) return true;
                        }
            }
            return false;
        }

        /// <summary>原子条目的规则短名（阶段D 唯一化）：行类型经 RuleAuraComponents.RuleIdOfType
        /// 派生优先；表 EffectType 未翻新行（仍=ModifyGameRule）回落 str=规则短名；
        /// 行缺失（旧数据）沿用 str。非光环条目返回 null。</summary>
        private static string RuleIdOfAtom(AtomicEffectEntry a)
        {
            if (a == null) return null;
            var row = Attribute.AtomicEffectTable.GetByHashId(a.refId);
            if (row != null && System.Enum.TryParse<AtomicEffectType>(row.EnumName, out var t))
            {
                var rid = RuleAuraComponents.RuleIdOfType(t);
                if (rid != null) return rid;
                return t == AtomicEffectType.ModifyGameRule ? a.str : null;
            }
            return a.str;
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

        /// <summary>归土回合末复活（GameCore.OnTurnEnded 于延迟死亡/窥渊之后显式调——2026-10-30 改版）：
        /// 回合结束时从自己墓地复活一张费用≤己方绿色地牌数的生物（自动取费用最高者；容量满留墓地）。</summary>
        public static void ReviveAtTurnEnd(Player turnPlayer)
            => RuleAuraComponents.ReviveAtTurnEnd(turnPlayer);

        /// <summary>规则光环作用范围中文（播报/UI 共用，2026-10-07 范围化定案）：按激活时选择的范围。
        ///（原改写仪典"仅持有者生效"硬编码口径退役——运行时一律 ScopeHits 按范围判定；
        /// 旧按规则 id 报文的 string 重载已随 2026-10-30 枚举收拢删除——调用方一律传 int。）</summary>
        public static string RuleAuraScopeZh(int scope)
            => scope == (int)RuleAuraScope.Own ? "仅己方生效"
             : scope == (int)RuleAuraScope.Opponent ? "仅对方生效" : "对双方生效";
    }

    /// <summary>
    /// 七条规则组件（规则轴首批=全部旧仪式，2026-10-03 定案）。
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
        public const string GraveyardPlay = "GraveyardPlay";         // 归土仪典（2026-10-30 改版：回合结束时从
                                                                     // 自己墓地复活一张费用≤己方绿色地牌数的生物——
                                                                     // 旧「墓地视手牌中使用」整链退役；短名承袭存量卡数据 str）
        // 疾风仪典（CastSpeedUp）2026-10-31 整链删除——常量/类型映射/IsFixedBothRule 名单/
        // OnBoardActiveSpeedBonus 算速口（PendingEffect.Create 与 PushAttackDeclaration 两消费点）同批退役。
        // 轮回/时光仪典（DoubleTurn：双人连两回合）已删（2026-10-29 阶段3a）——额外回合改走
        // TakeExtraTurn 原子行（7da143c9，可叠加），表行/常量/映射/OnTurnEnd 结算块同批退役。
        // 纳川仪典 2026-10-30 改版：手牌上限不再固定 15，改 +自己蓝色地牌数（动态）。
        // 短名承袭不动（存量卡数据 str 兼容）。涌泉仪典（疲劳免疫）同日晚表行删除——
        // 常量与替代件按 AdditionalEnergy 先例代码侧保留：无表行不生效，教学三课直挂专用。
        public const string HandLimitNoFatigue = "HandLimitNoFatigue"; // 纳川仪典（手牌上限+自己蓝色地牌数）
        public const string FatigueImmunity = "FatigueImmunity";       // 教学直挂兼容（原涌泉仪典·表行已删，免疫抽牌疲劳）

        // ---- 负面光环仪典（2026-10-07 负面化定案：原"战斗伤害改写为指示物"语义退役，
        // 改持续型挂层——受光环影响的生物按各自触发点叠对应指示物；钩子在本类，代码侧实现）：
        // 毒蚀=受到伤害后叠毒素；霜蚀=攻击后冻结。
        // 疫蚀仪典已删（2026-10-09 剧毒指示物删除后无独立存在意义）。
        // 眠蚀仪典（CombatSleep）已删（2026-10-31 用户定案：主动发动叠沉睡整链移除——
        // 常量/枚举位/映射/OnActivatedForSleepAura 钩/引擎结算点直调/合成器映射同批退役）。
        // 舍身仪典（CombatRedirect 战斗伤害改写）已删（2026-10-16 关键词化：舍身改 GrantMartyrdom
        // 关键词+MartyrdomRules 配对，改写口=KeywordRules.ApplyDamage；仪典改写映射清零。
        // 2026-10-30 战斗法师改版：GrantBattleMage 无状态散射——MartyrdomRules 配对表亦退役）。----
        public const string CombatToxin = "CombatToxin";             // 毒蚀仪典（受到伤害→毒素）
        public const string CombatFreeze = "CombatFreeze";           // 霜蚀仪典（攻击后→冻结）

        // ---- 治疗改写仪典（2026-10-09 引入，黑8，表行 79ef61f3；2026-10-30 用户更名「放血仪典」，
        // 历名暗牧仪典/惩戒仪典）：受光环影响一方的单位与角色受到的治疗改写为等量伤害
        //（改写口=EntityEffectExtensions.Heal 咽喉→TryRewriteHealAsDamage；
        // 范围按受疗方一侧判 ScopeHits——同丰盈/离散的"受疗方"口径）。配套的实体级姊妹机制=
        // 反疗指示物（DepravityCounter，任意有生命单位·消耗层），
        // 共用 IsHealInverted/TryRewriteHealAsDamage。----
        public const string HealInversion = "HealInversion";         // 放血仪典（治疗转伤害）

        // ---- 禁令族（2026-10-29）：行为限制，受光环影响方按 ScopeHits 判命中。
        // 2026-10-30 表行改版：禁祭令/禁非生物令表行删除（常量/闸门同批退役）；
        // 除禁攻/禁守/禁发/禁地外的禁令全部移出 IsFixedBothRule——作用面改为
        // 受光环影响的一方（极性 -1 钉对方单侧）。----
        public const string NoAttackAura = "NoAttackAura";     // 禁攻令
        public const string NoGuardAura = "NoGuardAura";       // 禁守令
        public const string NoActivateAura = "NoActivateAura"; // 禁发令
        public const string NoLandAura = "NoLandAura";         // 禁地令
        public const string NoHealAura = "NoHealAura";           // 禁疗令
        public const string NoSearchAura = "NoSearchAura";       // 禁寻令
        public const string NoSpellAura = "NoSpellAura";         // 禁魔令
        public const string NoCreatureAura = "NoCreatureAura";   // 禁召令（2026-10-31 改版：属性>己方灰色地牌数的生物禁放）
        public const string NoDrawAura = "NoDrawAura";           // 禁抽令

        // ---- 语料原生禁令族（2026-10-29 阶段2：MTG 出处的行为限制全局化） ----
        public const string NoLowCostAura = "NoLowCostAura";       // 禁低费令（梦魇异音布瑟拉：mana value ≤3）
        public const string NoHighCostAura = "NoHighCostAura";     // 禁高费令（俄佐立变节者拉温妮改版：费用>对手地牌数不能施放）
        // 禁指示物令已删（2026-10-31 用户定案：指示物承载了大部分游戏规则，不能禁止）——
        // 表行 d436b741/常量 NoCounterAura/AddCounters 闸/合成器映射整链移除。
        // 禁扰令已删（2026-10-31 用户定案：弃改版整链退役）——
        // 表行 1bb1ea6d/常量 OwnTurnCastAura/枚举位 AuraNoInterfere/合成器映射/
        // PlayCardInResponse 与响应手牌候选两道旧闸整链移除。

        // ---- 信息轴光环（2026-10-29 阶段3c，百闻牌参考）：两条都是改写族（范围可选、不进
        // IsFixedBothRule）。占卜加成=受影响一方的占卜数量+1（消费口=LookAtTopCardsHandler
        // / DeckArrangeHelper.PeekTop extra 参数，按控制者判 ScopeHits——谁发动占卜谁吃加成）；
        // 灵视=使用已展示手牌者受2伤且其对手回2（消费口=本类 CardPlayEvent 订阅）。----
        public const string ScryBonusAura = "ScryBonusAura";           // 占卜加成
        public const string RevealedPunishAura = "RevealedPunishAura"; // 灵视（已展示惩罚）

        // ---- 阶段3b 改写光环（2026-10-29）：延迟死亡/牌库顶当手牌——改写族（范围三档可选，
        // 不入 IsFixedBothRule 固定双方名单）。延迟死亡=受影响单位的死亡改写为下个回合结束时死亡
        //（改写口=DeathRules.TryKill 头部 + 本类回合末消费 OnTurnEndDelayedDeaths）；
        // 牌库顶当手牌=受影响一方可将牌库顶的卡当手牌使用（授权口=TopDeckPlaySource/IPlaySource，
        // PlayCard fromZone=Deck 路径由此放行；UI=手牌扇末尾虚拟手牌，本地局 BattleScreen 消费）。----
        public const string DelayedDeathAura = "DelayedDeathAura";     // 延迟死亡
        public const string TopDeckAsHandAura = "TopDeckAsHandAura";   // 牌库顶当手牌

        // ---- 铭刻（2026-10-31 新增，绿6 表行 759e17c5）：只作用于场上单位（战场卡——生物+结界）：
        // 受光环影响一方的场上单位被添加指示物时，该次添加层数+1——角色/手牌/牌库的指示物
        // 不受影响；消费口=EntityEffectExtensions.AddCounters 咽喉（引擎/关键词/效果 handler
        // 全部加层入口共口）。系统指示物不加深（特殊裁定终稿：系统指示物不受影响——
        // 倒计时/死亡计数等引擎主干层不沾）。改写族：极性 0、范围三档可选（缺省双方）。----
        public const string InscriptionAura = "InscriptionAura";       // 铭刻（单位加层+1）

        // ---- 阶段D·EffectType 唯一化（2026-10-31）：场地光环行类型 → 规则短名静态映射 ----
        // 源=上方常量，与原子表光环族 26 行一一对应（枚举名↔短名↔中文行名见各常量行注）；
        // ModifyGameRuleHandler 投放、CostDerivation 计价、MorphSystem/合成器行识别、
        // DeckUniqueSlots/同名打出闸 一律经本映射取规则 id，彻底脱离 str=规则短名。
        // 禁指示物令（NoCounterAura）/疾风（CastSpeedUp）/禁扰令（OwnTurnCastAura·AuraNoInterfere）
        // 行与常量均 2026-10-31 删——不在映射内。
        // 表 EffectType 列未翻新期间（行仍=ModifyGameRule）：各消费口按 str 回落（过渡，见各口注释）。
        private static readonly Dictionary<AtomicEffectType, string> _typeRuleIds
            = new Dictionary<AtomicEffectType, string>
        {
            { AtomicEffectType.AuraTriPhase, ElementConversion },
            { AtomicEffectType.AuraSharedPain, BloodPact },
            { AtomicEffectType.AuraOverflowLife, HealOverflow },
            { AtomicEffectType.AuraDamageClamp, DamageCap },
            { AtomicEffectType.AuraPeekLock, LockRevealed },
            { AtomicEffectType.AuraGraveRevive, GraveyardPlay },
            { AtomicEffectType.AuraHandLimit, HandLimitNoFatigue },
            { AtomicEffectType.AuraToxin, CombatToxin },
            { AtomicEffectType.AuraFreeze, CombatFreeze },
            { AtomicEffectType.AuraBleed, HealInversion },
            { AtomicEffectType.AuraNoAttack, NoAttackAura },
            { AtomicEffectType.AuraNoGuard, NoGuardAura },
            { AtomicEffectType.AuraNoActivate, NoActivateAura },
            { AtomicEffectType.AuraNoLand, NoLandAura },
            { AtomicEffectType.AuraNoHeal, NoHealAura },
            { AtomicEffectType.AuraNoSearch, NoSearchAura },
            { AtomicEffectType.AuraNoSpell, NoSpellAura },
            { AtomicEffectType.AuraNoSummon, NoCreatureAura },
            { AtomicEffectType.AuraNoDraw, NoDrawAura },
            { AtomicEffectType.AuraNoLowCost, NoLowCostAura },
            { AtomicEffectType.AuraNoHighCost, NoHighCostAura },
            { AtomicEffectType.AuraScryBonus, ScryBonusAura },
            { AtomicEffectType.AuraRevealPunish, RevealedPunishAura },
            { AtomicEffectType.AuraDelayedDeath, DelayedDeathAura },
            { AtomicEffectType.AuraTopDeckAsHand, TopDeckAsHandAura },
            { AtomicEffectType.AuraInscription, InscriptionAura },
        };

        /// <summary>是否场地光环行类型（阶段D 新型——投放/识别口用）。</summary>
        public static bool IsAuraType(AtomicEffectType type)
            => _typeRuleIds.ContainsKey(type);

        /// <summary>类型 → 规则短名（非光环新型返回 null——含旧共享型 ModifyGameRule）。</summary>
        public static string RuleIdOfType(AtomicEffectType type)
            => _typeRuleIds.TryGetValue(type, out var rid) ? rid : null;

        /// <summary>是否按场地光环口径计价/识别（新型 ∪ 旧共享型 ModifyGameRule——
        /// 表 EffectType 列未翻新期间的过渡合集；翻表后可收缩为 IsAuraType）。</summary>
        public static bool IsAuraLike(AtomicEffectType type)
            => type == AtomicEffectType.ModifyGameRule || _typeRuleIds.ContainsKey(type);

        /// <summary>光环行新型全集（handler 别名注册/验证器枚举用）。</summary>
        public static System.Collections.Generic.IReadOnlyCollection<AtomicEffectType> AuraTypes => _typeRuleIds.Keys;

        /// <summary>是否负面光环族（毒/冻；眠蚀已删 2026-10-31）。缺省范围与极性驱动（负→对方）；
        /// 运行时判定走 ScopeHits，不再读本口（保留供展示/分类）。</summary>
        public static bool IsHolderScoped(string ruleId)
            => ruleId == CombatToxin || ruleId == CombatFreeze;

        /// <summary>固定双方规则（2026-10-30 表行改版收缩；疾风 2026-10-31 删后）：许可 3（三相/归土/纳川）+
        /// 禁止 4（禁攻/禁守/禁发/禁地）——UI 不提供 己方/对方 选择。
        /// 其余禁令族（禁疗/禁寻/禁魔/禁召/禁抽/禁低费/禁高费）改
        /// 「受光环影响的一方」：移出本名单随极性钉单侧（-1=仅对方，单侧档计价 ×1.5）。
        /// 改写族（苦痛/丰盈/离散/窥渊/毒蚀/霜蚀/放血）照旧三档。</summary>
        public static bool IsFixedBothRule(string ruleId)
            => ruleId == ElementConversion || ruleId == GraveyardPlay
            || ruleId == HandLimitNoFatigue
            || ruleId == NoAttackAura || ruleId == NoGuardAura
            || ruleId == NoActivateAura || ruleId == NoLandAura;

        private static bool _registered;

        // 三相：各玩家各纯色的累计消耗（满 3 兑换；Player 键随局重置回收）
        private static readonly Dictionary<Player, Dictionary<ManaType, int>> _spendCounters
            = new Dictionary<Player, Dictionary<ManaType, int>>();
        // 窥渊（2026-10-04 原子化+时机改版）：锁定不走本类回合级 HashSet——回合结束（指示物倒数之后）
        // 赋予对手被展示卡「锁定」指示物×1（LockCounter，层数=剩余回合，持有者回合末倒数；
        // 手牌区同样结算——CounterRules 持有者侧结算域含手牌）。锁定独立于光环存续
        //（载体离场不清既有指示物，自然倒数到归零）。
        // 归土配额（墓地出牌每回合一用）已随 2026-10-30 改版整删——新效果无逐回合状态
        // 禁抽令：按「玩家 × 全局回合」计数的抽牌闸（自然抽/效果抽同口）
        private static readonly Dictionary<Player, int> _drawTurn = new Dictionary<Player, int>();
        private static readonly Dictionary<Player, int> _drawCounts = new Dictionary<Player, int>();

        /// <summary>卡是否被锁定（不可使用）——出牌限制与 UI 共用查询口。
        /// 2026-10-04 原子化：读锁定指示物（LockCounter&gt;0）——不再要求光环活跃
        ///（锁定独立于载体存续，持有者回合末逐层倒数）。</summary>
        public static bool IsLockedThisTurn(Card card)
            => card != null && card.GetCounterCount(Attribute.CounterRules.LockCounter) > 0;

        /// <summary>禁抽令：每回合至多 1 张（自然抽/效果抽同口；被拦的抽不发事件、不疲劳）。</summary>
        public static bool TryConsumeDraw(Player player)
        {
            if (player == null) return true;
            int turn = GameCore.Instance?.TurnEngine?.TurnNumber ?? 0;
            if (!_drawTurn.TryGetValue(player, out var t) || t != turn)
            {
                _drawTurn[player] = turn;
                _drawCounts[player] = 0;
            }
            _drawCounts.TryGetValue(player, out var n);
            if (RuleAuraSystem.ScopeHits(NoDrawAura, player) && n >= 1) return false;
            _drawCounts[player] = n + 1;
            return true;
        }

        public static void EnsureRegistered()
        {
            if (_registered) return;
            _registered = true;

            // 三相：己方消耗累计（出牌支付与效果元素费两路发布的 ElementPoolPayEvent 都算）
            EventManager.Instance.Subscribe<ElementPoolPayEvent>(OnElementPaid);
            // 窥渊展示/锁定与归土复活均不经事件订阅——由 GameCore.OnTurnEnded 在指示物倒数后显式调
            //（订阅序相对 GameCore.OnTurnEnded 随局数漂移，先后无保证）
            // 牌库顶当手牌（2026-10-29 阶段3b）：牌库作为出牌来源（不限额——牌库顶是流动窗口）
            RuleHooks.RegisterPlaySource(new TopDeckPlaySource());
            // 窥渊：锁定经出牌限制钩子接入 PlayCard/PlayCardInResponse
            RuleHooks.RegisterPlayRestriction(new LockedCardRestriction());
            // 禁魔/禁召令（2026-10-29 1b）：出牌类型闸（同批注册——PlayCard/PlayCardInResponse 同门；
            // 禁召 2026-10-31 改版=属性>己方灰色地牌数的生物禁放，闸体见 NoCreaturePlayRestriction）
            RuleHooks.RegisterPlayRestriction(new NoSpellPlayRestriction());
            RuleHooks.RegisterPlayRestriction(new NoCreaturePlayRestriction());
            // 语料原生禁令（2026-10-29 阶段2）：低费/高费 两道出牌闸
            //（禁非生物令 2026-10-30 表行删除，闸门同批退役）
            RuleHooks.RegisterPlayRestriction(new NoLowCostPlayRestriction());
            RuleHooks.RegisterPlayRestriction(new NoHighCostPlayRestriction());
            // 纳川：手牌上限 +自己蓝色地牌数（修改链，实时查询；2026-10-30 改版前=固定 15）
            RuleHooks.RegisterHandLimitModifier(new HandLimitModifier());
            // 负面光环（2026-10-07 负面化定案）：毒蚀走 DamageEvent 分流、
            // 霜蚀走 AttackResolvedEvent（CombatSystem 攻击结算完成发布）；
            // 眠蚀（EffectExecutionEngine 主动结算点直调）已删 2026-10-31
            EventManager.Instance.Subscribe<DamageEvent>(OnDamageForNegativeAuras);
            EventManager.Instance.Subscribe<AttackResolvedEvent>(OnAttackResolvedForFreeze);
            // 灵视（2026-10-29 阶段3c 信息轴）：使用已展示手牌时惩罚——锚 CardPlayEvent
            //（使用宣言时点、付费前，卡仍在手牌=ExposedCounter 尚未因换区清除，
            // 恰是「使用已展示的手牌」的判定窗口；离开手牌后不追溯）
            EventManager.Instance.Subscribe<CardPlayEvent>(OnCardPlayedForRevealedPunish);
        }

        // ============ 灵视仪典（2026-10-29 阶段3c 信息轴）：使用已展示的手牌 → 使用者受2伤、其对手回2 ============

        /// <summary>灵视钩（CardPlayEvent，使用宣言时点付费前）：作用范围决定谁被惩罚——
        /// ScopeHits(使用者) 未命中一侧不受罚（缺省 Polarity=-1 落「仅对方」=只罚对手使用已展示手牌；
        /// 选双方则互相惩罚/回血）。伤害走 KeywordRules.ApplyDamage 全管线（可被护盾/坚韧/离散挡），
        /// 回血走 EntityEffectExtensions.Heal 咽喉（禁疗/反疗/丰盈自然参与）。</summary>
        private static void OnCardPlayedForRevealedPunish(CardPlayEvent e)
        {
            if (!(e?.PlayedCard is Card played) || !Attribute.RevealRules.IsExposed(played)) return;
            var user = e.Player;
            if (user == null || user.Opponent == null) return;
            if (!RuleAuraSystem.ScopeHits(RevealedPunishAura, user)) return;

            var carrier = RuleAuraSystem.CarrierOf(RevealedPunishAura);
            EventManager.Instance.Publish(new KeywordAppliedEvent
            {
                Target = user,
                Keyword = RevealedPunishAura,
                Detail = $"灵视光环：{EffectText.Name(user)} 使用已展示的手牌「{EffectText.Name(played)}」→ 受 2 点伤害，其对手回复 2 点生命",
                Source = carrier,
            });
            Attribute.KeywordRules.ApplyDamage(carrier, user, 2, isCombat: false);
            user.Opponent.Heal(2);
        }

        // ============ 负面光环（2026-10-07 负面化定案：持续型挂层，代码侧实现） ============

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

        // 眠蚀钩（OnActivatedForSleepAura：主动发动结算后叠沉睡）已删 2026-10-31——
        // 与 EffectExecutionEngine 主动结算点直调、CombatSleep 常量/枚举位/映射同批退役。

        // ============ 反疗改写（2026-10-09 放血仪典 HealInversion + 反疗指示物 DepravityCounter 共用单源） ============

        /// <summary>反疗命中查询（只读）：目标持反疗层（任意有生命单位）或暗牧光环活跃且受疗方一侧
        /// 在范围内。消费方：TargetFilterSystem.DamagedFilter（满血候选豁免）与引擎错边豁免快照
        ///（执行前取——改写会消耗反疗层，执行后查不回）。改写本体见 TryRewriteHealAsDamage。</summary>
        public static bool IsHealInverted(Entity target)
        {
            if (target == null || !target.IsAlive) return false;
            // 效果口径读数（2026-10-31 绝缘体裁定）：放血层对绝缘体单位不生效
            if (target.GetEffectiveCounterCount(Attribute.CounterRules.DepravityCounter) > 0)
                return true;
            var side = target is Player p ? p : (target as Card)?.GetController();
            return side != null && RuleAuraSystem.ScopeHits(HealInversion, side);
        }

        // 重入闸：改写产生的伤害若再触发治疗（吸血等回环），嵌套治疗不再改写——封死自递归
        private static bool _inHealRewrite;

        /// <summary>反疗改写口（EntityEffectExtensions.Heal 咽喉最前调用）：治疗改写为等量伤害。
        /// ① 反疗指示物（实体级优先，任意有生命单位）：消耗 1 层，伤害光源=施加方（GetCounterSource）；
        /// ② 放血仪典（光环级）：受疗方一侧 ScopeHits 命中，伤害光源=光环载体。
        /// 伤害走 KeywordRules.ApplyDamage 全管线（圣盾/护甲/坚韧/易损/离散伤害帽自然参与）；
        /// 与丰盈溢出同侧时反疗优先（治疗不发生、无溢出）。命中返回 true（调用方短路）。幂等安全：
        /// 反疗层>0 恒改写一次，光环持续期间恒改写（无层可耗）。</summary>
        public static bool TryRewriteHealAsDamage(Entity target, int amount)
        {
            if (_inHealRewrite || target == null || !target.IsAlive || amount <= 0) return false;

            // 效果口径读数（2026-10-31 绝缘体裁定）：放血层对绝缘体单位不改写、不消耗
            if (target.GetEffectiveCounterCount(Attribute.CounterRules.DepravityCounter) > 0)
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
                    Detail = $"放血光环：{EffectText.Name(target)} 受到的治疗改写为 {amount} 点伤害",
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
            _drawTurn.Clear();
            _drawCounts.Clear();
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

        /// <summary>红/蓝/绿各 +1 入**临时钱包**（2026-10-11 现取现用：直写 TempElements + 发 AddManaEvent；
        /// 三色不受黑白钳约束，回合末随钱包清空）。</summary>
        private static void GrantTrinity(GameCore core, Player player)
        {
            var wallet = core.ElementPool.GetPool(player).TempElements;
            foreach (var color in new[] { ManaType.Red, ManaType.Blue, ManaType.Green })
            {
                wallet.TryGetValue(color, out var cur);
                wallet[color] = cur + 1;
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
                int actualDelta = pick.AddCounters(Attribute.CounterRules.ExposedCounter, 1, carrier);
                EventManager.Instance.Publish(new CounterChangedEvent
                {
                    Target = pick,
                    CounterType = Attribute.CounterRules.ExposedCounter,
                    Amount = actualDelta,
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
                int actualDelta = card.AddCounters(Attribute.CounterRules.LockCounter, 1, carrier);
                EventManager.Instance.Publish(new CounterChangedEvent
                {
                    Target = card,
                    CounterType = Attribute.CounterRules.LockCounter,
                    Amount = actualDelta,
                    Source = carrier,
                });
            }
        }

        // ============ 归土仪典（2026-10-30 改版）：回合结束时，从自己墓地复活一张费用不大于
        // 己方绿色地牌数量的生物（旧「每回合一次，墓地视手牌中使用」IPlaySource 配额链整删——
        // 窥渊锁定不再有独立清口：指示物经 CounterRules.OnTurnEnd ③ 块持有者回合末逐层倒数） ============
        // 触发口=GameCore.OnTurnEnded 组合根显式调（同窥渊——订阅序相对 GameCore.OnTurnEnded
        // 随局数漂移，先后无保证），排指示物倒数/延迟死亡之后——同回合末刚入墓的生物可被复活。
        // 固定双方规则：双方各在自己回合末结算一次。候选=墓地生物且运行时费（mode 0）≤己方
        // 绿色地牌数（0 费生物无绿地也可复活——费用不大于含等号）；自动取费用最高者（平局取
        // 墓地序首位——无选择窗口，同窥渊「指示物即机制」口径）；入场经容量闸门：满则留在墓地。

        internal static void ReviveAtTurnEnd(Player turnPlayer)
        {
            if (!RuleAuraSystem.ScopeHits(GraveyardPlay, turnPlayer)) return;
            var core = GameCore.Instance;
            var zm = core?.ZoneManager;
            if (zm == null) return;

            // 己方绿色地牌数（元素池内锁色=绿的地牌——同纳川蓝色地牌计数口径）
            int greenLands = core.ElementPool.GetPool(turnPlayer)?.PooledCards
                ?.Count(pc => pc != null && pc.LockedColor == ManaType.Green) ?? 0;

            Card best = null;
            float bestCost = -1f;
            foreach (var c in zm.GetCards(turnPlayer, Zone.Graveyard))
            {
                if (!(c is IHasSupertype ht) || ht.Supertype != Cardtype.Creature) continue;
                var data = (c as CardWrapper)?.GetData();
                if (data == null) continue;
                float cost = CardCostService.GetRuntimeCost(data, 0).Total;
                if (cost > greenLands || cost <= bestCost) continue;
                best = c;
                bestCost = cost;
            }
            if (best == null) return;

            if (!zm.TryMoveToBattlefield(best, turnPlayer, Zone.Graveyard))
                return; // 入场容量满：卡留在墓地（来源即墓地，无移动）
            best.SetController(turnPlayer);
            EventManager.Instance.Publish(new KeywordAppliedEvent
            {
                Target = best,
                Keyword = GraveyardPlay,
                Detail = $"归土光环：{EffectText.Name(turnPlayer)} 回合结束——从墓地复活「{EffectText.Name(best)}」（费用 {bestCost:0.#} ≤ 绿色地牌 {greenLands}）",
                Source = RuleAuraSystem.CarrierOf(GraveyardPlay),
            });
        }

        // ============ 牌库顶当手牌（2026-10-29 阶段3b，蓝12 表行 5d1a7907）：牌库作为出牌来源 ============
        // 受光环影响的一方可将牌库顶的卡当作手牌使用：PlayCard(fromZone: Deck) 经本来源授权放行
        //（不限额——与墓地 1 次/回合相对，牌库顶是流动窗口：打出后顶牌自动换新）；打出后照常
        // 结算/入墓/入场，牌库空则无从打出（UI 不显示虚拟手牌）。虚拟手牌视图=本地局
        // BattleScreen.RefreshFans 末尾一张（不计手牌上限、不可弃/放地、对手不可见）。

        private sealed class TopDeckPlaySource : IPlaySource
        {
            public Zone SourceZone => Zone.Deck;
            public bool CanUse(Player player) => RuleAuraSystem.ScopeHits(TopDeckAsHandAura, player);
            public bool TryBeginUse(Player player) => CanUse(player);
        }

        // ============ 窥渊锁定：出牌限制（PlayCard / PlayCardInResponse 同门） ============
        // 2026-10-04 原子化：读锁定指示物（独立于光环存续——光环死了锁照常倒数到归零）

        private sealed class LockedCardRestriction : IPlayRestriction
        {
            public bool CanPlay(GameCore core, Player player, Card card, Zone fromZone)
                => card == null || card.GetCounterCount(Attribute.CounterRules.LockCounter) == 0;
        }

        // ============ 禁魔/禁召令（2026-10-29 1b）：出牌类型闸——禁魔=不能施放法术；
        // 禁召 2026-10-31 改版=不能施放属性大于己方灰色地牌数的生物（旧=全禁生物） ============

        private sealed class NoSpellPlayRestriction : IPlayRestriction
        {
            public bool CanPlay(GameCore core, Player player, Card card, Zone fromZone)
                => !(RuleAuraSystem.ScopeHits(NoSpellAura, player)
                     && card is CardWrapper w && w.GetData() is CardData d && d.Supertype == Cardtype.Spell);
        }

        private sealed class NoCreaturePlayRestriction : IPlayRestriction
        {
            // 2026-10-31 改版（旧=全禁生物）：攻击或生命大于己方灰色地牌数的生物禁放（印刷值），
            // 等于放行（同归土"费用≤绿地数含等号"口径；0 灰地=全禁极限档，平滑承袭旧效果）；
            // 灰地计数=元素池内 LockedColor==Gray 的地牌（同纳川蓝地/归土绿地口径）。
            public bool CanPlay(GameCore core, Player player, Card card, Zone fromZone)
            {
                if (!RuleAuraSystem.ScopeHits(NoCreatureAura, player)) return true;
                if (!(card is CardWrapper w2) || !(w2.GetData() is CardData d2)
                    || d2.Supertype != Cardtype.Creature) return true;
                int grayLands = core?.ElementPool?.GetPool(player)?.PooledCards
                    ?.Count(pc => pc != null && pc.LockedColor == ManaType.Gray) ?? 0;
                return (d2.Power ?? 0) <= grayLands && (d2.Life ?? 0) <= grayLands;
            }
        }

        // ============ 语料原生禁令（2026-10-29 阶段2）：低费/高费/非生物 三道出牌闸 ============
        // 模式费取 mode 0 运行时推导费（多模式卡近似——先按首模式判；后续要精确再改 PlayCard 的 modeIndex 口）。

        private sealed class NoLowCostPlayRestriction : IPlayRestriction
        {
            public bool CanPlay(GameCore core, Player player, Card card, Zone fromZone)
            {
                if (!RuleAuraSystem.ScopeHits(NoLowCostAura, player)) return true;
                if (!(card is CardWrapper w) || !(w.GetData() is CardData d)) return true;
                return CardCostService.GetRuntimeCost(d, 0).Total > 3f;
            }
        }

        private sealed class NoHighCostPlayRestriction : IPlayRestriction
        {
            public bool CanPlay(GameCore core, Player player, Card card, Zone fromZone)
            {
                if (!RuleAuraSystem.ScopeHits(NoHighCostAura, player)) return true;
                if (!(card is CardWrapper w) || !(w.GetData() is CardData d)) return true;
                // 2026-10-29 修正口径：阈值=对手池内地牌张数（不限非生物、不看己方地数——拉温妮改版）
                var opp = player?.Opponent;
                int oppLands = opp != null ? (core?.ElementPool?.GetPool(opp)?.PooledCards?.Count ?? 0) : 0;
                return CardCostService.GetRuntimeCost(d, 0).Total <= oppLands;
            }
        }

        // ============ 纳川仪典（2026-10-30 改版）：手牌上限 + 自己蓝色地牌数（动态实时查询）。
        // 固定双方规则——双方光环各自按各自蓝色地牌数提升（疲劳免疫已随涌泉表行删除退役）。 ============

        private sealed class HandLimitModifier : IHandLimitModifier
        {
            public int Modify(Player player, int currentLimit)
            {
                if (!RuleAuraSystem.ScopeHits(HandLimitNoFatigue, player)) return currentLimit;
                var pool = GameCore.Instance?.ElementPool?.GetPool(player);
                int blueLands = pool?.PooledCards?.Count(pc => pc != null && pc.LockedColor == ManaType.Blue) ?? 0;
                return currentLimit + blueLands;
            }
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

        // ============ 涌泉仪典疲劳免疫（2026-10-30 从纳川剥离；同日晚表行删除）：
        // 免疫空库抽牌疲劳（替代件——逐局重挂；范围三档可选，缺省双方）。无表行不生效——
        // 代码侧保留专供教学三课直挂（ScriptedAi phantom 直调 Activate，AdditionalEnergy 先例）。 ============

        private sealed class FatigueImmunityReplacement : ReplacementEffectBase
        {
            public FatigueImmunityReplacement() : base(typeof(FatigueEvent), "RuleAuraFatigueImmunity") { }

            public override bool CanReplace(IGameEvent e)
                => e is FatigueEvent f
                   && f.Damage > 0
                   && f.Player != null
                   && RuleAuraSystem.ScopeHits(FatigueImmunity, f.Player);

            public override IGameEvent CreateReplacement(IGameEvent originalEvent, Effect sourceEffect)
                => originalEvent is FatigueEvent f
                    ? new FatigueEvent { Player = f.Player, Damage = 0 }
                    : null;
        }
    }
}
