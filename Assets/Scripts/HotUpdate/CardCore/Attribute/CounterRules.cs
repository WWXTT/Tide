using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCore.Attribute
{
    /// <summary>
    /// 指示物极性（定案）：正面=增益/资源，负面=减益/妨害。
    /// </summary>
    public enum CounterPolarity
    {
        /// <summary>正面（增益/资源类：护甲、+1/+1 等）</summary>
        Positive,
        /// <summary>负面（减益/妨害类：毒素、冻结、沉默、突袭紊乱、-1/-1 等）</summary>
        Negative,
    }

    /// <summary>
    /// 指示物分类（2026-10-08 终版定案；同日晚补裁决：换区口径按型翻转）——主轴，注册行必须显式选择：
    /// Resident 常驻=不随时间衰退，**换区清**（技能计数/耐久等消费型由各自事件消耗）；
    /// Decay 自然衰退=**层即剩余回合**，持有者回合结束 −1 层、归零即消失，**换区清**（战场减益不跟卡走）；
    /// Exception 生效自减=生效时自减消耗（护甲吸收/圣盾挡伤/复生替死/潜行失效/毒素发作减半/
    /// 角色攻击烧光/诅咒祝福抽到消层），**换区不清**（跨区存活——诅咒须活过牌库→手牌正依赖此档）；
    /// System 系统=引擎主干（BranchEngines）内部投放与消耗，不给玩家用（倒计时——换区清，
    /// 重入场经引擎兜底重挂）。
    /// 【Duration 轴已全灭（2026-10-08）】"持续时间"概念由层数全面取代；换区清与否由分类表达
    /// （Resident/Decay/System 清，Exception 不清），对应原子表 Tags 行为三型。
    /// </summary>
    public enum CounterClass
    {
        /// <summary>常驻：不随时间衰退；换区清（消费型由事件消耗：技能计数/耐久）</summary>
        Resident,
        /// <summary>自然衰退：层=剩余回合，持有者回合末 −1，归零消失，换区清</summary>
        Decay,
        /// <summary>生效自减：生效时自减消耗，换区不清（护甲/圣盾/复生/潜行/毒素/角色攻击/诅咒/祝福）</summary>
        Exception,
        /// <summary>系统：引擎主干内部驱动，不给玩家用（倒计时）；换区清（引擎重挂兜底）</summary>
        System,
    }

    /// <summary>属性指示物对应的字段类别（附加时即时回写、清除时反向回写）。
    /// 定案：攻/血指示物为**单向粒度**（增加/减少各一，单独成行方便后续取用，如分类净化/对消）；
    /// 每层效果固定（±1），计数恒正，方向由类别承载。</summary>
    public enum StatCounterKind
    {
        None,
        /// <summary>攻击力增加（每层 +1 攻，仅场上）</summary>
        PowerUp,
        /// <summary>攻击力减少（每层 −1 攻，仅场上）</summary>
        PowerDown,
        /// <summary>生命值增加（每层 +1 上限与当前，仅场上）</summary>
        LifeUp,
        /// <summary>生命值减少（每层 −1 上限，归零交 SBA，仅场上）</summary>
        LifeDown,
        /// <summary>费用增加（每层 +1 费，仅手牌生效，离手消失）</summary>
        CostUp,
        /// <summary>费用减少（每层 −1 费，仅手牌生效，离手消失）</summary>
        CostDown,
        /// <summary>+1/+1 属性增加（历史 id）</summary>
        PlusOnePlusOne,
        /// <summary>-1/-1 属性减少（历史 id，SBA 与 +1/+1 对消）</summary>
        MinusOneMinusOne,
    }

    /// <summary>
    /// 指示物层语义：一"层"在该指示物上意味着什么（Decay 类恒为 Clock——层即持续）：
    /// Strength=强度（每层一份效果：属性层每层 ±1、护甲每层吸收 1 点、毒素每层 1 点伤害基数）；
    /// Clock=时钟（层=剩余回合：冻结/易损/紊乱/沉睡/锁定）；
    /// Display=纯显示（层仅累计展示，效果与层数无关的门槛指示物：沉默/展示类）；
    /// Reading=读数（层本身就是数值：角色攻击力、技能发动计数）。
    /// </summary>
    public enum CounterLayerRole
    {
        /// <summary>强度：每层一份效果（可叠加生效）</summary>
        Strength,
        /// <summary>时钟：层=剩余回合（倒数消耗）</summary>
        Clock,
        /// <summary>显示：层仅累计展示，效果与层数无关</summary>
        Display,
        /// <summary>读数：层本身就是数值</summary>
        Reading,
    }

    /// <summary>
    /// 指示物消退驱动（机制轴；主分类见 CounterClass）：谁来推进消退/消耗——
    /// None=无自动消退（Resident 常驻）；TurnEndTickDown=持有者回合末逐层倒数（=Decay 类，
    /// ③块按此遍历）；External=外部系统驱动（例外例程、引擎主干、伤害管线吸收/装备消耗）。
    /// </summary>
    public enum CounterTickPolicy
    {
        /// <summary>无自动消退（常驻）</summary>
        None,
        /// <summary>持有者回合末逐层倒数（归零解除）</summary>
        TurnEndTickDown,
        /// <summary>外部系统驱动消退/消耗</summary>
        External,
    }

    /// <summary>指示物规格：id → 极性 + 分类 + 层语义 + 消退驱动 + 重置拦截 + 属性回写类别。
    /// 注册行即行为全景（2026-10-08 终版：Duration 轴删除，持续=层）。</summary>
    public class CounterSpec
    {
        public string Id;
        public CounterPolarity Polarity;
        /// <summary>分类主轴（Resident/Decay/Exception/System）</summary>
        public CounterClass Class = CounterClass.Resident;
        /// <summary>层语义（Strength/Clock/Display/Reading；Decay 类恒 Clock）</summary>
        public CounterLayerRole LayerRole = CounterLayerRole.Display;
        /// <summary>消退驱动（None/TurnEndTickDown/External）</summary>
        public CounterTickPolicy TickPolicy = CounterTickPolicy.None;
        /// <summary>持有期间无法重置（Decay 类专属标记：冻结/沉睡；倒数统一在回合末③块）</summary>
        public bool BlocksUntap;
        /// <summary>播报显示名（消退播报用）</summary>
        public string DisplayName;
        /// <summary>属性回写类别（None=非属性指示物）</summary>
        public StatCounterKind StatKind = StatCounterKind.None;
    }

    /// <summary>
    /// 指示物统一管理（2026-10-08 终版：四分类 + 层即持续 + 换区口径按型）。
    ///
    /// 【设计原则】
    /// - 时间行为只有两种：Decay 类=持有者回合末 −1 层（层=剩余回合，归零消失）；其余=不降层。
    ///   Duration 概念已由层数全面取代（UntilEndOfTurn 整清/永久档/三轨永久层均已退役）。
    /// - 换区口径按型（2026-10-08 晚补裁决）：Resident/Decay/System **换区清**（战场减益与常驻层
    ///   不跟卡走；倒计时引擎重挂兜底）；Exception 生效自减**换区不清**（护甲/圣盾/复生/潜行/毒素/
    ///   坚韧/易损/角色攻击/诅咒/祝福跨区存活——诅咒活过牌库→手牌正依赖此档；
    ///   坚韧/易损 2026-10-09 自 Resident/Decay 迁入：生效=受伤拦减/放大，随后层数减半 floor）。
    /// - 清除口收敛为四：换区 ClearAll（清非 Exception）｜净化 PurgeAll（全清）｜
    ///   解减益 ClearNegative（极性口径）｜Decay 自然归零。
    /// - 消耗型（Exception 全员 + Resident 的技能计数/耐久）由各自事件消耗（TickPolicy=External 标注归属）。
    /// - 重置拦截经 RuleHooks.IUntapBlockRule 接入（BlocksUntap 标记：冻结/沉睡持有期间无法重置），
    ///   其余指示物不改回合规则（回合开始横置重置照常）。
    /// - 新指示物 = 此处登记一条（OCP：不改引擎热点）；未登记 id 保守视为 正面/常驻换区清。
    /// </summary>
    public static class CounterRules
    {
        // ---- 指示物 id 常量（新指示物 = 一条常量 + 一条 spec）----
        /// <summary>毒素（生效自减类）：无持续时间、只有层数——持有者每回合结束受到=层数的伤害，
        /// 随后层数减半（向下取整，0.5→0）。**换区不清**、净化可清。施加口=AddToxinHandler/毒刺改写/毒蚀光环。</summary>
        public const string ToxinCounter = "Toxin";
        /// <summary>沉默：持有者不可发动主动效果（换区清）</summary>
        public const string SilenceCounter = "Silence";
        /// <summary>
        /// 无效（2026-09-09 定案，蓝3）：目标的**非启动式**能力无法发动——拦全部触发式（含登场 OnPlay）
        /// + 拦光环静态能力（连接箭头来源被无效压制，是唯一能压光环的口）；
        /// 坚韧/圣盾等伤害管线被动与再生等回合维护不是「能力发动」，不拦。换区清除。
        /// </summary>
        public const string NullifyCounter = "Nullify";
        /// <summary>
        /// 易损（2026-10-09 生效自减定案，同毒素档，表行 e83505b8/AddVulnerable）：无持续时间、只有层数——
        /// 每次受到伤害时每层使受到的伤害 +1，生效后层数减半（向下取整，1 层生效一次即清零）。
        /// 放大+减半口=KeywordRules.ApplyDamage 第 0 步；换区不清（生效自减档）、净化/解减益可清。
        /// </summary>
        public const string VulnerableCounter = "Vulnerable";
        /// <summary>
        /// 坚韧（2026-10-09 生效自减改版，表行 2b1e3700/GrantToughness）：每次受到伤害时每层使伤害 −1，
        /// 实际拦到伤害即生效——生效后层数减半（向下取整，易损的正面镜像）；可叠加。
        /// Entity 级（角色可持有——旧关键词形态 is Card 死线随之消解）；换区不清（生效自减档）、净化可清。
        /// 施加口=GrantToughnessHandler；拦减+减半口=KeywordRules.ApplyPreventionLayers 第 3 层。
        /// </summary>
        public const string ToughnessCounter = "Toughness";
        /// <summary>
        /// 圣盾（2026-10-08 指示物化，表行 c8624e6a/GrantDivineShield）：每层抵挡一次任意伤害
        ///（战斗+效果，吸收的是易损放大后的量）并消耗 1 层；可叠加（每层一份）。
        /// Entity 级（角色可持有）；穿透伤害全越（KeywordRules.ApplyPreventionLayers 三层一并跳过）。
        /// 挡下口=ApplyPreventionLayers 第 1 层；换区不清（生效自减档）、净化可清。
        /// </summary>
        public const string DivineShieldCounter = "DivineShield";
        /// <summary>
        /// 复生（2026-10-08 指示物化，表行 94f6a32d/GrantReborn）：每层一次死亡替代——
        /// 不进墓不离场、生命变 1、横置，消耗 1 层；湮灭不可救。
        /// 消耗口=KeywordRules.TryReborn（死亡路径送墓前调用）；换区不清（生效自减档）、净化可清。
        /// </summary>
        public const string RebornCounter = "Reborn";
        /// <summary>
        /// 潜行（2026-10-08 指示物化，表行 8f84641e/GrantStealth）：持有层数 &gt;0 期间不可被
        /// 攻击/效果指定（三指定口：CombatSystem.CanAttackTarget / EffectTargeting.CanTarget /
        /// TargetFilterSystem.ExcludeUnselectable，与隐密 Concealed 关键词同查）；
        /// 攻击宣言/发动效果/实际受到伤害各消耗 1 层（逐份撤口径，承自旧台账消耗型语义）；
        /// 换区不清（生效自减档）、净化可清。
        /// 隐密（Concealed）仍是关键词——不因生效消耗，是潜行的持续版。
        /// </summary>
        public const string StealthCounter = "Stealth";
        /// <summary>
        /// 法术护盾（2026-10-09 指示物化，表行 7270df35/GrantSpellShield）：每层抵消一次对手
        /// 效果对自身的作用——该效果执行时被移出目标列表并消耗 1 层（候选/查询阶段不消耗）。
        /// 施加口=GrantSpellShieldHandler；消耗口=KeywordRules.ConsumeSpellShields
        ///（EffectHandlerRegistry.PrepareForExecution 前置过滤）；换区不清（生效自减档）、净化可清。
        /// </summary>
        public const string SpellShieldCounter = "SpellShield";
        /// <summary>耐久（2026-09-13 装备系统）：武器/效果装备的使用期限——反伤/主动攻击/转移/主动效果各 -1，
        /// 归零销毁入墓（Smash 同款直毁）。正面常驻（净化可削——对位手段）。</summary>
        public const string DurabilityCounter = "Durability";
        /// <summary>
        /// 角色攻击（2026-10-07 角色参战定案·弹药原子，例外类）：层数=角色攻击力读数（GetPower(Player) 直读）。
        /// 角色攻击或反击结算后**全部移除**（RemoveHeroAttackAmmo）——攻击与反击共用同一份弹药，
        /// 弹药即闸门；持有期间常驻（计价档=与生物攻击力增加同价；角色不换区，
        /// 实际只经攻击/反击消耗与净化清除）。
        /// </summary>
        public const string HeroAttackCounter = "HeroAttack";
        /// <summary>攻击力增加指示物（每层 +1 攻，仅场上）</summary>
        public const string PowerUpCounter = "PowerUp";
        /// <summary>攻击力减少指示物（每层 −1 攻，仅场上）</summary>
        public const string PowerDownCounter = "PowerDown";
        /// <summary>生命值增加指示物（每层 +1 上限与当前，仅场上）</summary>
        public const string LifeUpCounter = "LifeUp";
        /// <summary>生命值减少指示物（每层 −1 上限，仅场上）</summary>
        public const string LifeDownCounter = "LifeDown";
        /// <summary>费用增加指示物（每层 +1 费，仅手牌，离手消失）</summary>
        public const string CostUpCounter = "CostUp";
        /// <summary>费用减少指示物（每层 −1 费，仅手牌，离手消失）</summary>
        public const string CostDownCounter = "CostDown";
        /// <summary>技能发动计数（2026-09-22 升级=抉择式条件分支定案）：挂在技能卡上，每发动 +1；
        /// 升级门读它（此前已发动 ≥7 次 → 走升级档）。常驻——随技能卡存续，
        /// 换技能卡=新卡计数归零；净化可清（极端交互，接受）。</summary>
        public const string SkillUseCounter = "SkillUse";
        /// <summary>+1/+1（历史 id 保留；2026-10-08 永久档退役后与换区清层同语义）</summary>
        public const string PlusOneCounter = "+1/+1";
        /// <summary>-1/-1（历史 id，SBA 对消）</summary>
        public const string MinusOneCounter = "-1/-1";
        // ---- 三轨制·生物轨永久档已退役（2026-10-08 Duration 轴删除）：四永久 id 删除，
        //     生物赋属性一律落换区清层（跨区永久只剩魔法/设置轨直写）----

        /// <summary>展示：被展示的卡持续暴露——双方可点击对应区域查看（网络快照/事件流不隐藏），
        /// 可作筛选条件（TargetFilter "Exposed"）与「本回合不可使用/送墓/换费用减免」类设计的挂点。
        /// 换区清除（用户定案：离开被展示时所在区域即失效）。</summary>
        public const string ExposedCounter = "Exposed";

        // ---- 系统指示物（2026-10-08 定案：引擎主干行用到的指示物归系统，不设玩家可组合的指示物表行
        //      ——投放/触发/消耗全部由引擎（BranchEngines/CurseSystem）内部驱动。诅咒/祝福后经
        //      2026-10-08 晚补裁决移入 Exception 生效自减（换区不清——跨区存活是机制必需），系统类仅剩倒计时）----
        /// <summary>倒计时：引擎主干·倒计时的计数层——控制者回合开始 −1，归零触发奖励并重置。
        /// 仅由引擎投放（BranchEngines.OnEnterBattlefield），无玩家表行；换区清（引擎重挂兜底）。</summary>
        public const string CountdownCounter = "Countdown";
        /// <summary>诅咒（2026-10-08 引擎主干化；生效自减类）：由引擎主干·附加诅咒施放时投放到对手牌库的卡上——
        /// 跨区存活（须活过牌库→手牌的换区清除），对手抽到该卡时 CurseSystem 自动执行诅咒分支效果
        /// 并消层（一次性）。无玩家表行。</summary>
        public const string CurseCounter = "Curse";
        /// <summary>祝福（2026-10-08；生效自减类）：同诅咒，作用面=自己牌库——由引擎主干·附加祝福投放，
        /// 抽到该卡时执行祝福分支效果并消层（一次性）。无玩家表行。</summary>
        public const string BlessingCounter = "Blessing";
        /// <summary>锁定（2026-10-04 窥渊仪典原子化定案，衰退类）：层数=剩余回合，持有者回合结束 −1
        ///（手牌区与场上同样倒数——持有者侧结算域含手牌）；持有期间该牌无法使用（PlayCard/响应出牌/
        /// 苏醒立约同门，LockedCardRestriction+CommitAwaken）。归零解锁。</summary>
        public const string LockCounter = "Lock";
        /// <summary>
        /// 反疗（2026-10-09，黑2，表行 6b9c4b90/GrantDepravity；原名堕落·仅角色，同日用户改名放开）：
        /// 任意有生命单位（生物+角色，双方）可持——该目标下一次受到的治疗改写为等量伤害并消耗 1 层
        ///（改写口=EntityEffectExtensions.Heal 咽喉，伤害光源=施加方 GetCounterSource）。
        /// 生效自减档（换区不清）；多层=逐次各拦一次治疗；满血可选性=TargetFilterSystem.Damaged
        /// 豁免口。净化可清（生物侧 ClearNegative 负面极性；角色无净化通道——只能被消耗）。
        /// </summary>
        public const string DepravityCounter = "Depravity";
        /// <summary>
        /// 重放（2026-10-09，白4，表行 b70a8a42/GrantReplay）：挂场上单位——该单位效果发动结算后
        /// 自动消耗 1 层并再次发动一次（重放轮不再消耗层——「重放不触发重放」；
        /// 消耗口=EffectExecutor.ExecuteAsync 结算段）。生效自减档（换区不清——离场休眠、
        /// 再入场（含预挂手牌/牌库的卡登场）仍可生效）。
        /// </summary>
        public const string ReplayCounter = "Replay";

        private static readonly Dictionary<string, CounterSpec> _registry =
            new Dictionary<string, CounterSpec>();

        static CounterRules()
        {
            // ---- 生效自减（Exception：生效时自减消耗，**换区不清**——2026-10-08 晚补裁决统一口径）----
            // 护甲：伤害管线逐点吸收（每层 1 点）
            Register(new CounterSpec { Id = KeywordRules.ArmorCounter, Polarity = CounterPolarity.Positive, Class = CounterClass.Exception, LayerRole = CounterLayerRole.Strength, TickPolicy = CounterTickPolicy.External, DisplayName = "护甲" });
            // 圣盾/复生/潜行（2026-10-08 指示物化，生效自减类）：每层一份效果、由各自事件消耗 1 层
            //（圣盾挡伤/复生替死/潜行失效口各 −1；External），换区不清、净化可清
            Register(new CounterSpec { Id = DivineShieldCounter, Polarity = CounterPolarity.Positive, Class = CounterClass.Exception, LayerRole = CounterLayerRole.Strength, TickPolicy = CounterTickPolicy.External, DisplayName = "圣盾" });
            Register(new CounterSpec { Id = RebornCounter, Polarity = CounterPolarity.Positive, Class = CounterClass.Exception, LayerRole = CounterLayerRole.Strength, TickPolicy = CounterTickPolicy.External, DisplayName = "复生" });
            Register(new CounterSpec { Id = StealthCounter, Polarity = CounterPolarity.Positive, Class = CounterClass.Exception, LayerRole = CounterLayerRole.Strength, TickPolicy = CounterTickPolicy.External, DisplayName = "潜行" });
            // 法术护盾（2026-10-09 指示物化，生效自减类收官）：每层抵消一次对手效果的作用
            //（消耗口=ConsumeSpellShields 效果执行前置过滤）；关键词族消耗型成员就此清零
            Register(new CounterSpec { Id = SpellShieldCounter, Polarity = CounterPolarity.Positive, Class = CounterClass.Exception, LayerRole = CounterLayerRole.Strength, TickPolicy = CounterTickPolicy.External, DisplayName = "法术护盾" });
            // 坚韧（2026-10-09 生效自减改版，自 Resident 迁入）：受伤每层 −1——实际拦到伤害即生效，
            // 生效后层数减半（floor）；拦减口=KeywordRules.ApplyPreventionLayers 第 3 层；换区不清、净化可清
            Register(new CounterSpec { Id = ToughnessCounter, Polarity = CounterPolarity.Positive, Class = CounterClass.Exception, LayerRole = CounterLayerRole.Strength, TickPolicy = CounterTickPolicy.External, DisplayName = "坚韧" });
            // 毒素：层=伤害基数——持有者每回合末受到=层数的伤害，随后层数减半（floor）。
            // 例程在 OnTurnEnd ①（ProcessToxinException）；换区不清、净化可清
            Register(new CounterSpec { Id = ToxinCounter, Polarity = CounterPolarity.Negative, Class = CounterClass.Exception, LayerRole = CounterLayerRole.Strength, TickPolicy = CounterTickPolicy.External, DisplayName = "毒素" });
            // 易损（2026-10-09 生效自减改版，自 Decay 迁入，同毒素档）：受伤每层 +1 → 生效后层数减半
            //（floor）；无回合末倒数；放大+减半口=KeywordRules.ApplyDamage 第 0 步；换区不清、净化/解减益可清
            Register(new CounterSpec { Id = VulnerableCounter, Polarity = CounterPolarity.Negative, Class = CounterClass.Exception, LayerRole = CounterLayerRole.Strength, TickPolicy = CounterTickPolicy.External, DisplayName = "易损" });
            // 角色攻击：弹药读数，攻击/反击结算后全烧（RemoveHeroAttackAmmo）
            Register(new CounterSpec { Id = HeroAttackCounter, Polarity = CounterPolarity.Positive, Class = CounterClass.Exception, LayerRole = CounterLayerRole.Reading, TickPolicy = CounterTickPolicy.External, DisplayName = "角色攻击" });
            // 诅咒/祝福（生效自减·引擎投放——无玩家表行；抽到触发一次性消层；换区不清是跨区存活必需）
            Register(new CounterSpec { Id = CurseCounter, Polarity = CounterPolarity.Negative, Class = CounterClass.Exception, LayerRole = CounterLayerRole.Display, TickPolicy = CounterTickPolicy.External, DisplayName = "诅咒" });
            Register(new CounterSpec { Id = BlessingCounter, Polarity = CounterPolarity.Positive, Class = CounterClass.Exception, LayerRole = CounterLayerRole.Display, TickPolicy = CounterTickPolicy.External, DisplayName = "祝福" });
            // 反疗（2026-10-09，黑2，原名堕落·仅角色，同日放开为任意有生命单位）：
            // 下一次受到的治疗改写为等量伤害并消耗 1 层（改写口=EntityEffectExtensions.Heal 咽喉）；
            // 换区不清、生物侧净化可清（角色无净化通道）
            Register(new CounterSpec { Id = DepravityCounter, Polarity = CounterPolarity.Negative, Class = CounterClass.Exception, LayerRole = CounterLayerRole.Strength, TickPolicy = CounterTickPolicy.External, DisplayName = "反疗" });
            // 重放（2026-10-09，白4）：单位效果发动结算后消耗 1 层并再次发动一次（重放不触发重放；
            // 消耗口=EffectExecutor.ExecuteAsync）；换区不清（离场休眠、再入场仍生效）
            Register(new CounterSpec { Id = ReplayCounter, Polarity = CounterPolarity.Positive, Class = CounterClass.Exception, LayerRole = CounterLayerRole.Strength, TickPolicy = CounterTickPolicy.External, DisplayName = "重放" });

            // ---- 常驻（Resident：不随时间衰退，换区清；消费型标 External 注明归属）----
            //（坚韧 2026-10-09 迁入 Exception 生效自减——受伤拦减后层数减半，见上区块）
            Register(new CounterSpec { Id = PlusOneCounter, Polarity = CounterPolarity.Positive, Class = CounterClass.Resident, LayerRole = CounterLayerRole.Strength, TickPolicy = CounterTickPolicy.None, DisplayName = "+1/+1", StatKind = StatCounterKind.PlusOnePlusOne });
            Register(new CounterSpec { Id = MinusOneCounter, Polarity = CounterPolarity.Negative, Class = CounterClass.Resident, LayerRole = CounterLayerRole.Strength, TickPolicy = CounterTickPolicy.None, DisplayName = "-1/-1", StatKind = StatCounterKind.MinusOneMinusOne });
            Register(new CounterSpec { Id = SilenceCounter, Polarity = CounterPolarity.Negative, Class = CounterClass.Resident, LayerRole = CounterLayerRole.Display, TickPolicy = CounterTickPolicy.None, DisplayName = "沉默" });
            Register(new CounterSpec { Id = NullifyCounter, Polarity = CounterPolarity.Negative, Class = CounterClass.Resident, LayerRole = CounterLayerRole.Display, TickPolicy = CounterTickPolicy.None, DisplayName = "无响应" });
            // 耐久（2026-09-13 装备系统）：常驻层——使用消耗（EquipRules.LoseDurability），归零销毁
            Register(new CounterSpec { Id = DurabilityCounter, Polarity = CounterPolarity.Positive, Class = CounterClass.Resident, LayerRole = CounterLayerRole.Strength, TickPolicy = CounterTickPolicy.External, DisplayName = "耐久" });
            Register(new CounterSpec { Id = PowerUpCounter, Polarity = CounterPolarity.Positive, Class = CounterClass.Resident, LayerRole = CounterLayerRole.Strength, TickPolicy = CounterTickPolicy.None, DisplayName = "攻击力增加", StatKind = StatCounterKind.PowerUp });
            Register(new CounterSpec { Id = PowerDownCounter, Polarity = CounterPolarity.Negative, Class = CounterClass.Resident, LayerRole = CounterLayerRole.Strength, TickPolicy = CounterTickPolicy.None, DisplayName = "攻击力减少", StatKind = StatCounterKind.PowerDown });
            Register(new CounterSpec { Id = LifeUpCounter, Polarity = CounterPolarity.Positive, Class = CounterClass.Resident, LayerRole = CounterLayerRole.Strength, TickPolicy = CounterTickPolicy.None, DisplayName = "生命值增加", StatKind = StatCounterKind.LifeUp });
            Register(new CounterSpec { Id = LifeDownCounter, Polarity = CounterPolarity.Negative, Class = CounterClass.Resident, LayerRole = CounterLayerRole.Strength, TickPolicy = CounterTickPolicy.None, DisplayName = "生命值减少", StatKind = StatCounterKind.LifeDown });
            // 费用层定案：层带颜色（将来按色增减各色分量），P1 恒作用于灰色分量——
            // 出牌链唯一口径 GameActions.GetCardCost 在此套层（预检/付费/pending 三处同源）；
            // 进发动区不清（发动区豁免），结算离开发动区与离手时清除。
            Register(new CounterSpec { Id = CostUpCounter, Polarity = CounterPolarity.Negative, Class = CounterClass.Resident, LayerRole = CounterLayerRole.Strength, TickPolicy = CounterTickPolicy.None, DisplayName = "费用增加", StatKind = StatCounterKind.CostUp });
            Register(new CounterSpec { Id = CostDownCounter, Polarity = CounterPolarity.Positive, Class = CounterClass.Resident, LayerRole = CounterLayerRole.Strength, TickPolicy = CounterTickPolicy.None, DisplayName = "费用减少", StatKind = StatCounterKind.CostDown });
            // 技能发动计数（2026-09-22）：常驻（随技能卡存续；换技能卡=新卡归零）
            Register(new CounterSpec { Id = SkillUseCounter, Polarity = CounterPolarity.Positive, Class = CounterClass.Resident, LayerRole = CounterLayerRole.Reading, TickPolicy = CounterTickPolicy.None, DisplayName = "技能发动计数" });
            // 展示：负面（信息暴露/使用限制挂点）；换区清（离开当前区域即失效）
            Register(new CounterSpec { Id = ExposedCounter, Polarity = CounterPolarity.Negative, Class = CounterClass.Resident, LayerRole = CounterLayerRole.Display, TickPolicy = CounterTickPolicy.None, DisplayName = "展示" });

            // ---- 自然衰退（Decay：层=剩余回合，持有者回合末 −1 归零消失，**换区清**——战场减益不跟卡走）----
            // 冻结：持有期间无法重置（BlocksUntap）；层=持续回合（叠加=延长，2026-10-08 层即持续定案）
            Register(new CounterSpec { Id = KeywordRules.FreezeCounter, Polarity = CounterPolarity.Negative, Class = CounterClass.Decay, LayerRole = CounterLayerRole.Clock, TickPolicy = CounterTickPolicy.TurnEndTickDown, BlocksUntap = true, DisplayName = "冻结" });
            // 突袭紊乱（长档已并入：同一机制不同量——层=回合数，教学长档 15 层即 15 回合）
            Register(new CounterSpec { Id = KeywordRules.RushSicknessCounter, Polarity = CounterPolarity.Negative, Class = CounterClass.Decay, LayerRole = CounterLayerRole.Clock, TickPolicy = CounterTickPolicy.TurnEndTickDown, DisplayName = "突袭紊乱" });
            // 沉睡：持有期间无法重置+效果无效；层=持续回合，倒数在回合末（2026-10-08 从回合开始挪回合末，
            // 苏醒特判随之退役——层归零后下回合开始自然恢复重置）
            Register(new CounterSpec { Id = KeywordRules.SleepCounter, Polarity = CounterPolarity.Negative, Class = CounterClass.Decay, LayerRole = CounterLayerRole.Clock, TickPolicy = CounterTickPolicy.TurnEndTickDown, BlocksUntap = true, DisplayName = "沉睡" });
            //（易损 2026-10-09 迁入 Exception 生效自减——受伤放大后层数减半，见上区块）
            // 锁定：层=剩余回合，手牌区同样倒数（窥渊只锁手牌——被锁卡无法打出故不换区，与换区清无冲突）
            Register(new CounterSpec { Id = LockCounter, Polarity = CounterPolarity.Negative, Class = CounterClass.Decay, LayerRole = CounterLayerRole.Clock, TickPolicy = CounterTickPolicy.TurnEndTickDown, DisplayName = "锁定" });

            // ---- 系统（System：引擎主干投放/消耗，不给玩家用；换区清——引擎重挂兜底）----
            Register(new CounterSpec { Id = CountdownCounter, Polarity = CounterPolarity.Positive, Class = CounterClass.System, LayerRole = CounterLayerRole.Clock, TickPolicy = CounterTickPolicy.External, DisplayName = "倒计时" });

            // ---- 剧毒指示物已退役（2026-10-08 转关键词 Venom：消灭受到其战斗伤害的生物，
            //     施加口 GrantVenom/落定点 KeywordRules.ApplyDamage 尾部）----
        }

        /// <summary>登记指示物规格（新指示物=新登记，OCP）。</summary>
        public static void Register(CounterSpec spec)
        {
            if (spec == null || string.IsNullOrEmpty(spec.Id)) return;
            _registry[spec.Id] = spec;
        }

        // ==================== 耐久公共路径（2026-10-02 结界实装定案） ====================

        /// <summary>
        /// 耐久消耗公共路径：结界与装备一套耐久语义（-N 层并播报；归零 → 直送墓 +
        /// CardDestroyEvent(Smashed)——「无生命直毁」惯例，不经死亡决策表）。
        /// 原实现自 EquipRules.LoseDurability 迁入（装备侧调用点转发至此）。
        /// 无耐久层（cur ≤ 0，如持续型装备）不消耗。
        /// </summary>
        public static void LoseDurability(CardCore.GameCore core, Card durable, int amount, string reason)
        {
            if (core?.ZoneManager == null || durable == null || amount <= 0) return;
            int cur = durable.GetCounterCount(DurabilityCounter);
            if (cur <= 0) return;

            durable.AddCounters(DurabilityCounter, -Math.Min(amount, cur));
            EventManager.Instance.Publish(new KeywordAppliedEvent
            {
                Target = durable,
                Keyword = DurabilityCounter,
                Detail = $"耐久 -{amount}（{reason}；剩余 {Math.Max(0, cur - amount)}）",
            });

            if (durable.GetCounterCount(DurabilityCounter) <= 0)
            {
                var owner = durable.GetOwner() ?? durable.GetController();
                if (owner != null)
                {
                    var from = durable.GetZone();
                    if (from != Zone.Graveyard)
                        core.ZoneManager.MoveCard(durable, owner, from, Zone.Graveyard);
                }
                EventManager.Instance.Publish(new CardDestroyEvent
                {
                    DestroyedCard = durable,
                    Reason = DestroyReason.Smashed, // 耐久耗尽=摧毁口径（无生命直毁）
                });
                EventManager.Instance.Publish(new KeywordAppliedEvent
                {
                    Target = durable,
                    Keyword = DurabilityCounter,
                    Detail = "耐久归零——销毁入墓",
                });
            }
        }

        /// <summary>查规格；未登记保守视为 正面/常驻（换区清——与既有行为一致）。</summary>
        public static CounterSpec Find(string id)
            => _registry.TryGetValue(id, out var spec) ? spec
               : new CounterSpec { Id = id, Polarity = CounterPolarity.Positive, DisplayName = id };

        public static bool IsNegative(string id) => Find(id).Polarity == CounterPolarity.Negative;

        // ==================== 属性指示物（加时回写 / 清时反写） ====================

        /// <summary>
        /// 附加属性指示物并即时回写字段。攻/血为单向粒度（计数恒正、方向由类别承载——
        /// 正值走增加类、负值走减少类由调用方路由）；费用保持带符号净量。
        /// 加时即写、清除（换区/净化）时反向回写——数值=字段+指示物，战斗直读字段的路径零改动。
        /// source = 施加方（指示物来源定案）：随计数登记，削减类归零标死时作为死亡来源归因。
        /// </summary>
        public static void AddStatCounter(Card card, string id, int amount, Entity source = null)
        {
            if (card == null || amount == 0) return;
            var spec = Find(id);
            if (spec.StatKind == StatCounterKind.None) return;
            card.AddCounters(id, amount, source);
            ApplyStatDelta(card, spec.StatKind, amount, source);
            EventManager.Instance.Publish(new CounterChangedEvent
            {
                Target = card,
                CounterType = id,
                Amount = amount,
                Source = source,
            });
        }

        /// <summary>属性增量回写（每层效果固定 ±1；n=层数恒正。削减类归零标死，送墓交 SBA——来源=施加方）。
        /// internal（2026-09-09 三轨制）：设置轨（魔法卡=永久直改）复用此直写实现——不挂计数层。</summary>
        internal static void ApplyStatDelta(Card card, StatCounterKind kind, int n, Entity source = null)
        {
            switch (kind)
            {
                case StatCounterKind.PowerUp:
                    card._power += n;
                    break;
                case StatCounterKind.PowerDown:
                    card._power -= n;
                    break;
                case StatCounterKind.LifeUp:
                    card._maxLife += n;
                    card._life += n;
                    break;
                case StatCounterKind.LifeDown:
                    card._maxLife -= n;
                    if (card._maxLife < 0) card._maxLife = 0;
                    if (card._life > card._maxLife) card._life = card._maxLife;
                    if (card._life + GameBoard.LinkAuraSystem.GetLifeBonus(card) <= 0) // 有效生命归零（含光环）
                    {
                        // 削减归零标死（送墓交 SBA）——死亡来源=减益施加方（指示物来源定案）
                        card._pendingDeathSource = source;
                        card.IsAlive = false;
                    }
                    break;
                case StatCounterKind.CostUp:
                    card._costModifier += n;
                    break;
                case StatCounterKind.CostDown:
                    card._costModifier -= n;
                    break;
                case StatCounterKind.PlusOnePlusOne:
                    card._power += n;
                    card._maxLife += n;
                    card._life += n;
                    break;
                case StatCounterKind.MinusOneMinusOne:
                    card._power -= n;
                    card._maxLife -= n;
                    if (card._maxLife < 0) card._maxLife = 0;
                    if (card._life > card._maxLife) card._life = card._maxLife;
                    if (card._life + GameBoard.LinkAuraSystem.GetLifeBonus(card) <= 0) // 有效生命归零（含光环）
                    {
                        card._pendingDeathSource = source; // 同 LifeDown：减益施加方归因
                        card.IsAlive = false;
                    }
                    break;
            }
        }

        /// <summary>按当前计数反向回写属性指示物（不清计数——按规格 StatKind 驱动）。</summary>
        private static void RevertStat(Card card, string id, StatCounterKind kind)
        {
            int n = card.GetCounterCount(id);
            if (n == 0) return;
            switch (kind)
            {
                case StatCounterKind.PowerUp:
                    card._power -= n;
                    break;
                case StatCounterKind.PowerDown:
                    card._power += n;
                    break;
                case StatCounterKind.LifeUp:
                    card._maxLife -= n;
                    if (card._life > card._maxLife) card._life = card._maxLife;
                    break;
                case StatCounterKind.LifeDown:
                    card._maxLife += n;
                    break;
                case StatCounterKind.CostUp:
                    card._costModifier -= n;
                    break;
                case StatCounterKind.CostDown:
                    card._costModifier += n;
                    break;
                case StatCounterKind.PlusOnePlusOne:
                    card._power -= n;
                    card._maxLife -= n;
                    if (card._life > card._maxLife) card._life = card._maxLife;
                    break;
                case StatCounterKind.MinusOneMinusOne:
                    card._power += n;
                    card._maxLife += n;
                    break;
            }
        }

        // ==================== 清除口（四口定案：换区 / 净化 / 解减益 / 衰退自然归零） ====================

        /// <summary>
        /// 换区清除（ZoneContainer.Move 调用；发动区豁免由调用方保证）：清**非 Exception** 类
        ///（Resident 常驻 / Decay 衰减 / System 倒计时——属性层先反向回写；战场减益不跟卡走）；
        /// **Exception 生效自减不清**（护甲/圣盾/复生/潜行/毒素/坚韧/易损/角色攻击/诅咒/祝福跨区存活
        /// ——诅咒活过牌库→手牌正依赖此档；坚韧/易损 2026-10-09 迁入，减半消耗在伤害管线内）。
        /// </summary>
        public static void ClearAll(Card card) => ClearCounters(card, purgeAll: false);

        /// <summary>
        /// 清除核心（分类驱动）：purgeAll=false 为换区口径（保留 Exception）；
        /// purgeAll=true 为净化口径（全清——效果级移除是跨区类的唯一强清口）。
        /// 被清的属性层先反向回写。
        /// </summary>
        private static void ClearCounters(Card card, bool purgeAll)
        {
            if (card == null || card._counters.Count == 0) return;

            var removed = new List<string>();
            foreach (var kv in card._counters)
            {
                var spec = Find(kv.Key);
                if (!purgeAll && spec.Class == CounterClass.Exception)
                    continue; // 生效自减类跨区存活
                RevertStat(card, kv.Key, spec.StatKind);
                removed.Add(kv.Key);
            }

            if (removed.Count == 0) return;
            foreach (var id in removed)
            {
                card._counters.Remove(id);
            }
            EventManager.Instance.Publish(new CounterChangedEvent
            {
                Target = card,
                CounterType = "*",
                Amount = 0,
                Source = null,
            });
        }

        /// <summary>
        /// 净化口径：清除目标全部指示物（属性先反向回写，含 Decay/System——效果级移除是
        /// 跨区类的唯一强清口，这正是统一指示物的交互点）。与清空关键词配套，由 PurifyHandler 调用。
        /// </summary>
        public static void PurgeAll(Entity entity)
        {
            if (entity is Card card) ClearCounters(card, purgeAll: true);
            else if (entity != null)
            {
                // Player：无属性回写，直接清计数（毒素/护甲可指向玩家）
                entity._counters.Clear();
                EventManager.Instance.Publish(new CounterChangedEvent
                {
                    Target = entity,
                    CounterType = "*",
                    Amount = 0,
                    Source = null,
                });
            }
        }

        /// <summary>
        /// 烧除角色攻击弹药（2026-10-07 角色参战定案，例外类）：角色攻击或反击结算后调用——
        /// 全量移除角色攻击指示物（攻击与反击共用同一份弹药，烧完即止，弹药即闸门）。
        /// 读数为 0 时无操作（无弹药不反击、不烧）。
        /// </summary>
        public static void RemoveHeroAttackAmmo(Player player)
        {
            if (player == null) return;
            int amount = player.GetCounterCount(HeroAttackCounter);
            if (amount <= 0) return;
            player.RemoveCounters(HeroAttackCounter, amount);
            EventManager.Instance.Publish(new CounterChangedEvent
            {
                Target = player,
                CounterType = HeroAttackCounter,
                Amount = -amount,
                Source = null,
            });
        }

        // ==================== 回合结束处理 ====================

        /// <summary>
        /// 回合结束处理（由 GameCore.OnTurnEnd 调用）。只结算**持有者侧**（回合方玩家+战场+手牌卡；
        /// 对手实体上的指示物等对手自己回合末）：
        /// ① 毒素例外例程（每回合末受到=层数的伤害，随后层数减半 floor）；
        /// ③ 衰退族逐层倒数（Class=Decay：层=剩余回合，持有者回合末 −1，归零解除——冻结/紊乱/
        ///    沉睡/锁定（易损 2026-10-09 迁生效自减档，已退出倒数）；窥渊挂锁必须排在倒数之后，
        ///    时序契约见 GameCore.OnTurnEnded）。
        /// </summary>
        public static void OnTurnEnd(Player turnPlayer, ZoneManager zoneManager)
        {
            if (turnPlayer == null) return;

            // ── ① 毒素例外例程（持有者侧） ──
            foreach (var entity in AllEntities(turnPlayer, zoneManager))
            {
                ProcessToxinException(entity);
            }

            // ── ③ 衰退族逐层倒数（spec 驱动）：TickPolicy=TurnEndTickDown（=Decay 类），
            //    层=剩余回合，持有者回合结束 −1，归零解除（新同类指示物=登记即用） ──
            var tickDownIds = _registry.Values
                .Where(s => s.TickPolicy == CounterTickPolicy.TurnEndTickDown)
                .Select(s => (s.Id, s.DisplayName))
                .ToList();
            foreach (var card in AllEntities(turnPlayer, zoneManager).OfType<Card>())
            {
                foreach (var (id, displayName) in tickDownIds)
                    TickLayeredCounter(card, id, displayName);
            }
        }

        /// <summary>
        /// 毒素例外例程（2026-10-08 定案：无持续时间、只有层数）——持有者每回合结束受到
        /// 等于层数的伤害（null 来源——毒素自身不是伤害来源实体，吸血/系命无从触发），
        /// 随后层数减半（向下取整：1 层余 0、3 层余 1）。伤害过程中的叠层（毒蚀光环）不影响
        /// 本次基数（先快照再结算）。
        /// </summary>
        private static void ProcessToxinException(Entity entity)
        {
            int layers = entity.GetCounterCount(ToxinCounter);
            if (layers <= 0 || !entity.IsAlive) return;
            KeywordRules.ApplyDamage(null, entity, layers, false);
            int halved = layers / 2; // 向下取整（0.5 → 0）
            entity.AddCounters(ToxinCounter, -(layers - halved));
            EventManager.Instance.Publish(new KeywordAppliedEvent
            {
                Target = entity,
                Keyword = ToxinCounter,
                Detail = $"毒素发作（{layers} 层，受到 {layers} 点伤害，余 {halved} 层）",
            });
        }

        /// <summary>层=剩余回合计数的衰退指示物通用倒数（持有者回合结束 −1，归零解除并播报）。</summary>
        private static void TickLayeredCounter(Card card, string counterId, string displayName)
        {
            int layers = card.GetCounterCount(counterId);
            if (layers <= 0) return;
            card.AddCounters(counterId, -1);
            EventManager.Instance.Publish(new KeywordAppliedEvent
            {
                Target = card,
                Keyword = counterId,
                Detail = layers - 1 > 0
                    ? $"{displayName}倒数（持有者回合结束，余 {layers - 1} 回合）"
                    : $"{displayName}解除（持有者回合结束，层数归零）",
            });
        }

        /// <summary>回合方玩家 + 回合方战场/手牌卡（持有者侧结算域：指示物只在持有者回合末
        /// 发作/倒数——对手侧实体等对手回合末，此域不含对手；手牌区纳入=锁定/沉睡在手牌
        /// 「像在场上一样」倒数）。ToList 物化快照：结算中移卡（容器列表变更）会炸惰性遍历。</summary>
        private static IEnumerable<Entity> AllEntities(Player turnPlayer, ZoneManager zoneManager)
        {
            var snapshot = new List<Entity> { turnPlayer };
            if (zoneManager != null)
            {
                foreach (var card in zoneManager.GetCards(turnPlayer, Zone.Battlefield).ToList())
                    if (card != null) snapshot.Add(card);
                foreach (var card in zoneManager.GetCards(turnPlayer, Zone.Hand).ToList())
                    if (card != null) snapshot.Add(card);
            }
            return snapshot;
        }

        /// <summary>清空目标全部负面指示物（解减益类效果的统一口径；横置状态不在此恢复）。</summary>
        public static void ClearNegative(Card card)
        {
            if (card == null) return;
            var negatives = card._counters
                .Where(kv => kv.Value > 0 && IsNegative(kv.Key))
                .Select(kv => kv.Key)
                .ToList();
            foreach (var id in negatives)
                card.AddCounters(id, -card.GetCounterCount(id));
        }
    }

    /// <summary>
    /// 重置拦截（2026-09-13 定案；2026-10-08 终版改 BlocksUntap 标记驱动）：持有 BlocksUntap
    /// 指示物（冻结/沉睡）的卡持有期间**无法重置**。拦而不扣——衰退族倒数统一在 OnTurnEnd
    ///（持有者回合末 −1 层）；层归零后下回合开始 BlocksUntap 自然为 false，走正常重置。
    /// 登记于 GameCore.Reset（组合根例外，幂等）——重置循环经 RuleHooks 只认接口；
    /// 新增同类指示物=CounterSpec 勾 BlocksUntap，本规则零改动。
    /// </summary>
    public sealed class SleepFreezeUntapBlockRule : IUntapBlockRule
    {
        public static readonly SleepFreezeUntapBlockRule Instance = new SleepFreezeUntapBlockRule();

        private SleepFreezeUntapBlockRule() { }

        public bool BlocksUntap(Card card)
            => card != null
               && card._counters.Any(kv => kv.Value > 0 && CounterRules.Find(kv.Key).BlocksUntap);

        public void OnUntapBlocked(GameCore core, Card card)
        {
            // 拦而不扣：保持横置；倒数在 CounterRules.OnTurnEnd（持有者回合末）
        }
    }
}
