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
        NegateActivation,
        Silence,
        RedirectTarget,
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
        GrantArmor,
        GrantFirstStrike,
        GrantDisarm,
        AdditionalEnergy,
        GrantVigilance,
        GrantRegeneration,
        GrantGrowth,

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
        Poison,
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

        // ============ 反制原子（使用时点/响应窗口——指向发动区） ============
        KnockDown,

        // ============ 衍生物生成（原 PutToBattlefield 枚举位已收编至此） ============
        SummonToken,

        // ============ 资源族（地牌指示物转化） ============
        Mine,

        // ============ 妨害族（三轨制同期新增） ============
        /// <summary>无效指示物（蓝3）：目标非启动式能力无法发动（拦触发式+光环；换区清除）。
        /// 枚举只可尾部追加（AEI_Type 按 int 位序序列化）。</summary>
        AddNullify,

        // ============ 战斗底盘（2026-09-10 攻击/守卫效果化：1速/2速主动效果，各 1 灰，不占槽） ============
        /// <summary>攻击：1 速主动效果（横置发动）。附带竖直参战（结算按竖直，结算完恢复横置）。
        /// 生物默认自带（CardData.NoAttack 可退除）。战斗流程由 CombatSystem 消费，不走常规原子执行。</summary>
        Attack,
        /// <summary>守卫：2 速响应拦截——友方被指为攻击目标时横置自身、转移目标；结算按横置（单向受伤）。
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
        /// <summary>回响（2026-09-11，蓝）：瞬间法术专用关键词——使用时（宣言时点）获得一张本体完全复制
        /// （属性/费用/效果不变）的临时卡，复制也带回响（可连锁，回合结束移除兜底）。被反制时复制已入手。</summary>
        GrantEcho,
        /// <summary>发现（2026-09-11，蓝2）：从牌库中随机展示 {value} 张牌（默认 3），从中选一张加入手牌。
        /// 炉石发现池=全收藏，本项目卡池难以估计——收窄为牌库内。未选中的牌留在牌库原位。
        /// 结算期选择走 TargetSelectionService（AI/无头自动选首张）。</summary>
        DiscoverCard,
        /// <summary>守护（2026-09-11，白1，关键词型）：登场时选择一个己方单位成为被守护者——
        /// 其获得被守护者指示物（来源=第一个守护者），施加者获得守护者指示物。
        /// 被守护者受到的伤害改由第一个守护者承受（多守护者仅第一个触发改写；单跳不链式）。
        /// 改写在 KeywordRules.ApplyDamage 咽喉；守护者离场/死亡=保护失效。</summary>
        GrantGuardian,
        /// <summary>类型伤害（2026-09-13，红3，固有全域原子）：对可选范围内**全部**有生命单位造成 value 伤害——
        /// 不弹选择窗口、不可随机（converter 强制 SelectionMode=Full + 装载校验拒 Manual/Random）；
        /// 范围溢价已含在 BaseCost——计价数量恒 ×1（不走 Full 期望 4）。</summary>
        SweepDamage,
        /// <summary>全体治疗（2026-09-13，绿2，固有全域原子）：对可选范围内**全部**有生命单位恢复 value 生命——
        /// 同 SweepDamage：强制 Full、禁随机、计价数量 ×1（2费全体回1）。</summary>
        SweepHeal,
        /// <summary>摒弃（2026-09-13，黑2，edict 原子）：作用对象=双方角色（filter "Player"）——
        /// 持有者自行选择一个**己方场上无生命单位**（结界等非生物持久物）直送墓地（DestroyReason.Abandoned）。
        /// 与牺牲（生物/效果死亡）成对；豁免帷幕（选择权在目标方——帷幕只约束对手的选择）。</summary>
        Abandon,
        /// <summary>冰晶（2026-09-13 改写定案，蓝2）：造成战斗伤害时，改为对目标添加一个冻结指示物（伤害不发生）</summary>
        GrantIceCrystal,
        /// <summary>梦魇（2026-09-13 改写定案，黑2）：造成战斗伤害时，改为对目标添加一个沉睡指示物（伤害不发生）</summary>
        GrantNightmare,
        /// <summary>病原体（2026-09-13 改写定案，绿5）：造成战斗伤害时，改为对目标添加一个剧毒指示物（伤害不发生）</summary>
        GrantPathogen,
        /// <summary>禁魔石（2026-09-13 改写定案，白3）：受到的非战斗伤害变为 0</summary>
        GrantSpellban,
    }

    #region 效果分类扩展方法

    /// <summary>
    /// 效果类型扩展方法
    /// </summary>
    public static class AtomicEffectTypeExtensions
    {
       

        /// <summary>
        /// 获取效果描述模板（使用英文枚举作为 key）
        /// </summary>
        public static string GetEffectDescription(AtomicEffectType effectType, int value = 0)
        {
            
            return "";
        }
    }

    #endregion

    #region 原子效果基类

    /// <summary>
    /// 原子效果基类
    /// </summary>
    [Serializable]
    public abstract class AtomicEffectBase : IAtomicEffect
    {
        /// <summary>效果类型</summary>
        public abstract AtomicEffectType EffectType { get; }

        /// <summary>效果数值</summary>
        public int Value;

        /// <summary>效果修正器</summary>
        public List<EffectModifier> Modifiers { get; set; } = new List<EffectModifier>();

        /// <summary>
        /// 执行效果
        /// </summary>
        public abstract void Execute(EffectExecutionContext context);

        /// <summary>
        /// 获取效果描述
        /// </summary>
        public virtual string GetDescription()
        {
            return AtomicEffectTypeExtensions.GetEffectDescription(EffectType, Value);
        }
    }

    /// <summary>
    /// 效果修正器
    /// </summary>
    [Serializable]
    public class EffectModifier
    {
        public int Apply(int baseValue) => baseValue;
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

        // ---- 组合层编排属性（2026-09-10 重构 P1：自 EffectDefinition 复制进上下文，参照 ModeIndex 先例）----
        /// <summary>持续（效果级唯一真相；原子 handler 从此读，不再读 effect.Duration）。</summary>
        public DurationType Duration;
        /// <summary>ForTurns 持续的回合数 N。</summary>
        public int DurationValue;
        /// <summary>SummonToken 落区（战场/手牌/牌库）。</summary>
        public Zone SummonDropZone;

        /// <summary>元素池系统</summary>
        public ElementPoolSystem ElementPool { get; set; }

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

    #endregion
}
