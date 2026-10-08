using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCore
{
    /// <summary>
    /// 原子效果类型（英文枚举作为 key）
    /// 实际顺序：代码中先实现一个原子效果，然后在表中记录该效果，填写基准费用和颜色
    /// 表行三分类（EffectTier）：Atom=可编辑参数的原子效果 / Keyword=授关键词 / Counter=附指示物
    /// </summary>
    public enum AtomicEffectType
    {
        // ============ 伤害与治疗 ============
        DealDamage,
        DealCombatDamage,
        LifeLoss,
        Heal,
        PierceDamage,
        DrainLife,

        // ============ 卡牌移动 ============
        DrawCard,
        DiscardCard,
        MillCard,
        ReturnToHand,
        Exile,
        ShuffleIntoDeck,
        SearchDeck,
        BounceToTop,
        BounceToBottom,

        // ============ 状态变更 ============
        Tap,
        Untap,
        ModifyPower,
        ModifyLife,
        SetPower,
        SetLife,
        SetCost,
        Freeze,
        Purify,
        Weaken,
        Inspire,
        Smash,

        // ============ 控制相关 ============
        GainControl,
        /// <summary>效果无效（2026-10-04 两层无效定案，承接旧 NegateActivation 标记实现）：
        /// 发动照常、效果不结算——施放=扣费不返还入墓；启动式=扣费+横置不重置；触发式=计入发动次数；
        /// 强制桶受管制（效果跳过）。枚举位承袭旧 NegateActivation（只可改名不可挪位）。</summary>
        NegateEffect,
        Silence,
        RedirectTarget,
        /// <summary>刺探——2026-10-08 表行并入占卜（LookAtTopCards 实例域锁 {8} 承接对手侧）：
        /// 枚举保序不删（只可尾部追加）；handler 已删（2026-10-08 无墓碑清理——无表行无调用者）。</summary>
        ScryCards,

        // ============ 保护相关（关键词） ============
        GrantDoubleStrike,
        GrantCannotBeTargeted,
        GrantSpellShield,

        // ============ 特殊效果 ============
        Morph,

        // ============ 反规则效果（暂不实现白名单，无表行） ============
        ModifyGameRule,
        OverrideRestriction,

        // ============ 移动补充 ============
        ReturnFromGraveyard,
        RecoverToHand,
        LookAtTopCards,

        // ============ 状态补充 ============
        ChangeOwner,
        ModifyCost,

        // ============ 保护补充（关键词） ============
        GrantPoisonSting,
        GrantLifesteal,
        GrantStealth,
        GrantTaunt,
        GrantDivineShield,
        GrantOverwhelm,
        /// <summary>坚韧（2026-10-08 指示物化；同日更名——原 GrantArmor 与护甲 AddArmor 命名倒挂纠正）：
        /// 对目标附加 {value} 层坚韧指示物（ToughnessCounter）——每层使每次受到的伤害 −1，不随受伤消耗。</summary>
        GrantToughness,
        GrantFirstStrike,
        GrantDisarm,
        /// <summary>额外元素（原光合作用/蓄能）。2026-10-02 裁决：表行退役——凭空产元素违背费用规则；
        /// AdditionalEnergyHandler 保留代码侧（枚举只可尾部追加不可删），写进表才生效。</summary>
        AdditionalEnergy,
        GrantVigilance,
        GrantRegeneration,
        // GrantGrowth（成长）已删（2026-10-08 机制删除 + 全量底层改造不留墓碑）

        // ============ 控制补充 ============
        TakeExtraTurn,
        SkipTurn,

        // ============ 信息族（宣言/预言——有限域押注，产出命中与否） ============
        DeclareHand,
        DeclareDeckTop,
        DeclareArrow,
        ProphecyNextCard,

        // ============ 关键词补充 + 指示物原子 ============
        GrantReborn,
        GrantIndestructible,
        GrantLifelink,
        AddArmor,
        AddToxin,
        RushSickness,
        AddVulnerable,
        AddPowerUp,
        AddPowerDown,
        AddLifeUp,
        AddLifeDown,
        AddPlusOne,
        AddMinusOne,
        AddCostUp,
        AddCostDown,

        // ============ 死亡原子（DeathRules 死因已有，补原子层） ============
        Sacrifice,
        Devour,
        Annihilate,

        // ============ 反制原子（使用时点/响应窗口——指向发动区；2026-10-04 两层无效定案） ============
        /// <summary>发动无效（原打落 KnockDown 承接改名，枚举位不变）：无效化**发动**本身——净零成本。
        /// 施放=扣费返还（发动区送墓，消费点不付费中止）；启动式=不扣费+横置不重置；触发式=不计发动次数
        /// （入栈记账退还）；强制桶不受管制（不可标记）。表行「发动无效」。</summary>
        NegateActivation,

        // ============ 衍生物生成（原 PutToBattlefield 枚举位已收编至此） ============
        SummonToken,

        // ============ 资源族（地牌指示物转化） ============
        Mine,

        // ============ 妨害族（三轨制同期新增） ============
        /// <summary>无效指示物（蓝3）：目标非启动式能力无法发动（拦触发式+光环；换区清除）。
        /// 枚举只可尾部追加（AEI_Type 按 int 位序序列化）。</summary>
        AddNullify,

        // ============ 战斗底盘（2026-09-10 攻击/守卫效果化：1速/2速主动效果，各 1 灰，不占槽） ============
        /// <summary>攻击：速度0主动效果（2026-09-16；结算期横置支付）。附带竖直参战（结算按竖直，结算完恢复横置）。
        /// 生物默认自带（CardData.NoAttack 可退除）。战斗流程由 CombatSystem 消费，不走常规原子执行。</summary>
        Attack,
        /// <summary>守卫：速度1响应拦截（2026-09-16）——友方被指为攻击目标时横置自身、转移目标；结算按横置（单向受伤）。
        /// 生物默认自带（CardData.NoGuard 可退除）。</summary>
        Guard,

        // ============ 状态原子（2026-09-11 沉睡改造） ============
        /// <summary>沉睡（绿1 中性）：赋予目标沉睡指示物——沉睡期间无法重置、效果无效。
        /// 自我沉睡（登场 SelectionMode=Self）走灰费豁免：打出不扣灰费，指示物数量=豁免的灰元素数。
        /// 效果/指示物分离定案：效果=赋予指示物，指示物本身承载持续规则。枚举只可尾部追加。</summary>
        Sleep,
        /// <summary>微缩（2026-09-11，蓝）：赋予目标单位微缩——其控制者使用任意卡时，将一张「同效果」
        /// 的临时卡加入手牌（1/1、费用1灰；回合结束时从手牌移除）。临时卡自身不再触发微缩/放大（防自复制链），
        /// 也不可作地牌。登场效果宿主须具备属性（无属性=构筑期拦截）。</summary>
        GrantMiniature,
        /// <summary>放大（2026-09-11，红）：同微缩，临时卡为 10/10、费用10灰。</summary>
        GrantMagnify,
        /// <summary>回响（2026-10-07 关键词→普通效果改版，蓝2）：将此卡的临时复制加入手牌——完全复制
        ///（属性/费用/效果不变），复制带效果栏（连锁天然保留），回合结束移除兜底。
        /// 时点=结算期（旧宣言时点关键词形态退役——两层无效将对复制生效：发动无效=无复制）。</summary>
        EchoCopy,
        /// <summary>发现（2026-09-11，蓝2）：从牌库中随机展示 {value} 张牌（默认 3），从中选一张加入手牌。
        /// 炉石发现池=全收藏，本项目卡池难以估计——收窄为牌库内。未选中的牌留在牌库原位。
        /// 结算期选择走 TargetSelectionService（AI/无头自动选首张）。</summary>
        DiscoverCard,
        /// <summary>守护（2026-10-08 配对制改版，白1；表行 MountKinds=关键词,不可作为连接光环）：
        /// 登场/授予时弹选一个己方目标（单位或角色，TargetSelectionService——AI/无头自动选首候选），
        /// 其受到的伤害改写为守护者自身承受（GuardianRules 配对表；改写在 KeywordRules.ApplyDamage
        /// 咽喉，单跳不链式 guardRerouted）。无限次直到守护者死亡/离场——离场断链，
        /// 墓地复活=新入场重新弹选（旧关系不残留）；复生原地留场配对保持。</summary>
        GrantGuardian,
        // 固有全域原子（SweepDamage/SweepHeal）已随 2026-10-05 墓碑清理实删——
        // 全域语义由组合期 TargetKinds+全取档表达（2026-09-21 退役定案，此番连墓碑一并清除）。
        /// <summary>摒弃（2026-09-13，黑2，edict 原子）：作用对象=双方角色（filter "Player"）——
        /// 持有者自行选择一个**己方场上无生命单位**（结界等非生物持久物）直送墓地（DestroyReason.Abandoned）。
        /// 与牺牲（生物/效果死亡）成对；豁免帷幕（选择权在目标方——帷幕只约束对手的选择）。</summary>
        Abandon,
        /// <summary>冰晶（2026-09-13 改写定案，蓝2）：造成战斗伤害时，改为对目标添加一个冻结指示物（伤害不发生）。
        /// 2026-10-02 裁决：表行退役——战斗伤害改冻结走分支组合（拦截式改写门 DmgRewriteFreeze），
        /// 印刷关键词路径保留（KeywordRules 改写链 + GrantKeywordHandlerFactory.Specs）。</summary>
        GrantIceCrystal,
        /// <summary>梦魇（2026-09-13 改写定案，黑2）：造成战斗伤害时，改为对目标添加一个沉睡指示物（伤害不发生）。
        /// 2026-10-02 裁决：表行退役——战斗伤害改沉睡走分支组合（DmgRewriteSleep），印刷关键词路径保留。</summary>
        GrantNightmare,
        /// <summary>禁魔石（2026-09-13 改写定案，白3）：受到的非战斗伤害变为 0</summary>
        GrantSpellban,

        // 自由分支主干原子（BranchEngine*，7 枚举）已随 2026-10-05 两槽定案实删——
        // 引擎条件不再是原子（槽级 BranchPayload.EngineKind 载荷声明），表内 6 行主干行同步删除。
        // 枚举中段墓碑（SweepDamage/SweepHeal）同批实删——效果库空置期整体破弃存储形状，无存量 int 依赖。

        /// <summary>宣告胜利（2026-09-15，终局原子）：效果控制者的对手获得游戏胜利——
        /// 亡语「对手获得胜利」等终局效果载体。枚举只可尾部追加。</summary>
        DeclareVictory,

        /// <summary>展示：为目标（隐藏区卡：己/对方手牌·牌库）挂「展示」指示物——持续暴露、双方可查看，
        /// 换区即失效。可作筛选条件（TargetFilter "Exposed"）与后续费用减免挂点。枚举只可尾部追加。</summary>
        RevealCard,
        /// <summary>附加诅咒：为对手的卡挂「诅咒」指示物（Exception 生效自减档——换区不清）并登记载荷
        /// （str=Effects.json 条目 id，SummonToken 模板同款引用先例）；对手抽到该卡时 CurseSystem
        /// 自动执行载荷分支效果并消层（一次性）。
        /// 2026-10-08 表行退役（指示物档删除——自由分支·引擎主干行 EngineCurseOnDraw 接棒，CurseCounter
        /// 随之归系统指示物）；handler 保留代码侧，无表行不生效（手写数据兼容，AdditionalEnergy 同款先例）。
        /// 枚举只可尾部追加。</summary>
        AddCurse,
        /// <summary>锁定（2026-10-04 窥渊仪典原子化，蓝）：为一张手牌挂「锁定」指示物×{value}回合——
        /// 持有期间该牌无法使用（打出/响应出牌/苏醒立约同门）；持有者回合结束层数−1
        /// （手牌区与场上同样倒数，CounterRules 持有者侧结算域），归零解锁。枚举只可尾部追加。</summary>
        LockCard,
        /// <summary>隐密（2026-10-04，蓝5——潜行的持续版）：不可被攻击/效果指定，且**不因
        /// 发动效果或受到伤害失效**（潜行蓝1 两个失效口都豁免）。枚举只可尾部追加。</summary>
        GrantConcealed,
        // 赋予主干（BranchEngineGrant）已随 2026-10-05 两槽定案解体实删——
        // 无条件赋予=无分支槽原子（关键词∪指示物）+效果级持续档计价（Grant 梯沿用）。

        // ---- 引擎主干行（2026-10-05 晚间回表定案：玩家直接在原子表选引擎，填槽即自由分支）----
        // 各行为"条件载体"而非效果：零锚价（ManaList=null）、零域、MountKinds=引擎主干（EngineTrunk）；
        // 不入主序列执行（ExecuteFlatAsync/枚举口按 Branch.Settle==Engine 跳过）、无处理器（白名单登记）；
        // 引擎条件与参数存槽级 BranchPayload（Settle=Engine），行本身只作填充入口与身份锚。
        // 2026-10-08 附加诅咒/附加祝福入列（八行）——引擎用到的指示物（诅咒/倒计时/祝福）归系统，
        // 不设玩家可组合的指示物表行。
        /// <summary>引擎主干·倒计时：回合开始计数-1，归零触发奖励并重置（延迟即付费）。枚举只可尾部追加。</summary>
        EngineCountdown,
        /// <summary>引擎主干·运势：回合开始掷 2d6，双＞x 触发奖励。枚举只可尾部追加。</summary>
        EngineLuckRoll,
        /// <summary>引擎主干·拼点：双方各随机取样生物比攻击力，差额≥门槛（=奖励锚价）触发奖励。枚举只可尾部追加。</summary>
        EngineClash,
        /// <summary>引擎主干·死亡计数：双方生物死亡累计≥x 触发奖励（预算=x）。枚举只可尾部追加。</summary>
        EngineDeathToll,
        /// <summary>引擎主干·元素充盈：己方元素池最多色＞x 触发奖励（预算=x）。枚举只可尾部追加。</summary>
        EngineManaSurplus,
        /// <summary>引擎主干·手牌序位：本回合第 x 张使用的手牌发动时触发奖励（预算=x）。枚举只可尾部追加。</summary>
        EngineNthHandCard,

        /// <summary>角色攻击力增加（2026-10-07 角色参战定案，弹药原子）：给己方角色附加 {value} 层
        /// 角色攻击指示物（HeroAttackCounter）——角色攻击力读数=层数；**攻击或反击结算后全部移除**
        ///（攻击与反击共用同一份弹药，烧完即止——弹药即闸门，无专用次数计数器）。
        /// 一般效果原子（可挂主动/被动/奖励任意时机，"回合开始"只是示例挂载）；不走光环。
        /// 枚举只可尾部追加。</summary>
        AddHeroAttack,

        /// <summary>引擎主干·附加诅咒（2026-10-08 自由分支化）：此卡施放结算时给对手牌库随机 x 张卡
        /// 各挂 1 层「诅咒」指示物并登记 Then 载荷（CurseSystem）——对手抽到该卡时执行诅咒分支效果
        /// 并消层（一次性；触发与引擎卡此后去向无关）。x∈[1,3]=附加张数；延迟与不确定性即代价
        /// （Then 预算不设上限）。枚举只可尾部追加。</summary>
        EngineCurseOnDraw,
        /// <summary>引擎主干·附加祝福（2026-10-08）：同附加诅咒，作用面=自己牌库、指示物=「祝福」——
        /// 抽到该卡时执行祝福分支效果并消层（一次性）。枚举只可尾部追加。</summary>
        EngineBlessingOnDraw,

        /// <summary>剧毒（2026-10-08 指示物转关键词·落定追加式）：获得剧毒——受到其战斗伤害的
        /// 生物被消灭（战斗伤害照常结算、实际落定 &gt;0 触发；对角色照常落血；不灭/神佑可拦）。
        /// 表行 20c06d52（原 Poison 原子行）改挂本枚举。枚举只可尾部追加。</summary>
        GrantVenom,

        /// <summary>永久属性增加（2026-10-08 来源分轨退役定案）：+{value}/+{value} 双属性**直写字段**
        ///（复用 CounterRules.ApplyStatDelta 直写分支，不挂计数层）——本局游戏永久、跨区保留、
        /// 净化不清（视同本体），与来源无关。临时层（换区清）用 AddPlusOne。表行 9256b41a。
        /// 枚举只可尾部追加。</summary>
        AddPermanentPlusOne,
        /// <summary>永久属性减少（2026-10-08）：−{value}/−{value} 直写字段（有效生命归零标死交 SBA），
        /// 跨区保留、净化不清，与来源无关。表行 e5da6dd9。枚举只可尾部追加。</summary>
        AddPermanentMinusOne,
    }

    /// <summary>
    /// 单步原子效果的产出结果，供 per-target 条件分支（OutcomeGate）评估。
    /// 以「单个目标」为粒度：每对一个目标执行原子前调用 Reset()，结算时由 handler 写入。
    /// </summary>
    public class EffectOutcome
    {
        /// <summary>受伤/治疗前的生命快照（供 Overkill / Overheal 计算）</summary>
        public int TargetLifeBefore;

        /// <summary>本步实际造成的总伤害</summary>
        public int DamageDealt;

        /// <summary>本步受影响的目标</summary>
        public List<Entity> AffectedTargets = new List<Entity>();

        /// <summary>本步因伤害/归零而死的目标</summary>
        public List<Entity> KilledTargets = new List<Entity>();

        /// <summary>本步实际回复量</summary>
        public int HealApplied;

        /// <summary>治疗溢出量 = 申请治疗 − 实际回复，下限 0</summary>
        public int OverhealAmount;

        /// <summary>受影响目标中是否存在存活者</summary>
        public bool AnySurvived;

        /// <summary>信息族：宣言/预言是否命中（宣言族即时写入；预言族由 ProphecySystem 在验证时刻另行结算）</summary>
        public bool DeclareHit;

        /// <summary>信息族：本步的宣言原文（"维度:值"编码，见 ProphecyDimension）——延迟验证的押注凭据</summary>
        public string Declaration;

        /// <summary>改写族（2026-09-22 拦截式改写门）：本步伤害被改写为指示物（伤害未发生）——
        /// 下游门（如 DmgKillsTarget）据此/凭空产出自然判否；AffectedTargets 照记（指示物已施加）。</summary>
        public bool Rewritten;

        /// <summary>本步是否有目标死亡</summary>
        public bool AnyKilled => KilledTargets.Count > 0;

        public void Reset()
        {
            TargetLifeBefore = 0;
            DamageDealt = 0;
            HealApplied = 0;
            OverhealAmount = 0;
            AnySurvived = false;
            DeclareHit = false;
            Declaration = null;
            Rewritten = false;
            AffectedTargets.Clear();
            KilledTargets.Clear();
        }

        /// <summary>记录一次伤害结算（调用前由 handler 快照 lifeBefore，造成伤害后再调用）。</summary>
        public void RecordDamage(Entity target, int lifeBefore, int dmg)
        {
            if (target == null) return;
            TargetLifeBefore = lifeBefore;
            DamageDealt += dmg;
            AffectedTargets.Add(target);
            if (!target.IsAlive) KilledTargets.Add(target);
            else AnySurvived = true;
        }

        /// <summary>记录一次治疗结算（调用前由 handler 快照 lifeBefore，传入申请治疗量）。</summary>
        public void RecordHeal(Entity target, int lifeBefore, int requested)
        {
            if (target == null) return;
            TargetLifeBefore = lifeBefore;
            int applied = target.GetLife() - lifeBefore;
            if (applied < 0) applied = 0;
            HealApplied += applied;
            int overheal = requested - applied;
            if (overheal > 0) OverhealAmount += overheal;
            AffectedTargets.Add(target);
        }
    }

    /// <summary>
    /// 效果执行上下文
    /// </summary>
    public class EffectExecutionContext
    {
        /// <summary>效果来源实体</summary>
        public Entity Source { get; set; }

        /// <summary>效果控制者</summary>
        public Player Controller { get; set; }

        /// <summary>目标列表</summary>
        public List<Entity> Targets { get; set; } = new List<Entity>();

        /// <summary>主要目标</summary>
        public Entity PrimaryTarget => Targets.Count > 0 ? Targets[0] : null;

        /// <summary>触发事件</summary>
        public IGameEvent TriggeringEvent { get; set; }

        /// <summary>区域管理器</summary>
        public ZoneManager ZoneManager { get; set; }

        /// <summary>抉择模式索引（来自 EffectInstance.ModeIndex；执行引擎遇 Choice 步骤按此分派）</summary>
        public int ModeIndex;

        // ---- 组合层编排属性（2026-09-10 重构 P1：自 EffectDefinition 复制进上下文，参照 ModeIndex 先例；
        //      2026-09-14 收缩：DurationValue 删除——持续档收缩后回合数走指示物自减，效果级不再携带）----
        /// <summary>持续（效果级唯一真相；原子 handler 从此读，不再读 effect.Duration）。</summary>
        public DurationType Duration;
        /// <summary>生效次数档（2026-10-07 深夜：随执行下发——Grant 关键词把它传进台账 Limit，
        /// 0=未声明→按 1；-1=无限。主构造口 EffectExecutionEngine 复制；旁路上下文缺省 0）。</summary>
        public int TriggerLimitPerTurn;
        /// <summary>SummonToken 落区（战场/手牌/牌库）。</summary>
        public Zone SummonDropZone;

        /// <summary>元素池系统</summary>
        public ElementPoolSystem ElementPool { get; set; }

        /// <summary>施放中的宿主卡（2026-09-22 定案）：出牌结算链路上的卡实例——
        /// 魔法卡效果 Source=角色（来源归因定案），状态门（如「本回合准备阶段抽到的卡」）
        /// 需要卡身份时先读本字段、退回 Source as Card。仅 GameActions 施放路径填充。</summary>
        public Card CastCard { get; set; }

        /// <summary>上一步原子效果的产出结果（per-target，由分支步骤读取）</summary>
        public EffectOutcome LastOutcome { get; set; } = new EffectOutcome();

        /// <summary>
        /// 获取效果数值修正后的值
        /// </summary>
        public int GetValueAfterModifiers(int baseValue)
        {
            return baseValue;
        }
    }
}
