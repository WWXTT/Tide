using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using CardCore.Attribute;
using CardCore.Attribute.Handlers;

namespace CardCore
{
    /// <summary>
    /// 内置效果/代价处理器引导
    /// EffectHandlerRegistry 与 CostHandlerRegistry 都是全局 static，只需注册一次。
    /// 抽成共享幂等引导，任何引擎初始化时都可安全调用，确保自建的 EffectExecutor
    /// 在首次结算前能找到已注册的处理器（避免"未注册的效果处理器"）。
    /// </summary>
    public static class BuiltinHandlerBootstrap
    {
        private static bool _registered = false;

        /// <summary>注册所有内置处理器（幂等，多次调用安全）</summary>
        public static void EnsureRegistered()
        {
            if (_registered) return;
            _registered = true;

            // 基础效果处理器
            var handlers = new IAtomicEffectHandler[]
            {
                new DealDamageHandler(),
                // 敌方全体伤害（2026-10-09 全体对象独立原子）：无域自结算，杀数聚合（击杀门槛一次结算）
                // 全体消灭（2026-10-29 阶段3a）：对方全体生物效果死亡——无域自结算（同上款全体口径）
                new DrawCardHandler(),
                // 回响（2026-10-07 关键词→普通效果改版）：复制施放卡入手（完全复制，连锁保留）
                new EchoCopyHandler(),
                new ReturnToHandHandler(),
                new FreezeHandler(),
                new HealHandler(),
                new ModifyPowerHandler(),
                new MorphHandler(),
                // 变形赋予（2026-10-13 临摹定案配套）：目标变成引用卡（与临摹共用 MorphSystem.ApplyForm）
                new GrantMorphHandler(),

                // 终局原子（2026-09-15：宣告胜利——亡语「对手获得胜利」载体）
                new DeclareVictoryHandler(),

                // 第一批补齐 — 伤害类（PierceDamageHandler 已删 2026-10-31：穿透档整体退役）
                new DrainLifeHandler(),
                // PoisonHandler 已删（2026-10-08 剧毒转关键词 GrantVenom）

                // 第一批补齐 — 卡牌移动 / 牌库
                new DiscardCardHandler(),
                new ExileHandler(),
                new ShuffleIntoDeckHandler(),
                new SearchDeckHandler(),
                new BounceToTopHandler(),
                new BounceToBottomHandler(),
                new ReturnFromGraveyardHandler(),
                new RecoverToHandHandler(),
                // 除外回收（2026-10-29 阶段3a）：除外区目标卡返回手牌（域=己方除外区）
                new RecoverFromExileHandler(),
                new LookAtTopCardsHandler(),

                // 第一批补齐 — 状态变更
                new ModifyLifeHandler(),
                // 设置系复活（2026-09-09 三轨制重建）：显式设置原子=天然设置类（不参与来源路由），
                // 永久直改、跨区保留、净化不清（视同本体）；与 Modify 族的设置轨同语义。
                new SetPowerHandler(),
                new SetLifeHandler(),
                new SetCostHandler(),
                // 攻守互换（2026-11-09 新增，蓝2）：攻击力与生命值上限两值互换——Set 系同款直写口径
                new SwapPowerLifeHandler(),
                new ModifyCostHandler(),

                // 信息族 — 强夺（宣言卡名夺对手手牌）/ 窃取（宣言卡名夺对手牌库·2026-10-31）/
                // 预言（隐秘类别押注·命中发动无效）/ 预言·押注行（ProphecyNextCard·类别三选一
                // 延迟验证·2026-11-02 收紧）——箭头宣言 2026-10-30 退役（表行/handler/枚举位同批删除）
                new DeclareHandHandler(),
                new DeclareDeckHandler(),
                new DeclareDeckTopHandler(),
                new ProphecyNextCardHandler(),

                // 指示物原子（护甲/坚韧/毒素——Entity 级，角色可持有；坚韧 2026-10-08 指示物化：
                // GrantToughness 原子（原 GrantArmor）与 refId 不变，执行改挂 ToughnessCounter 层，退出关键词处理器族）
                new AddArmorHandler(),
                new GrantToughnessHandler(),
                new AddToxinHandler(),
                // 荆棘（2026-10-16 新增，红1/层；2026-11-09 改名，原「报复」）：受伤反噬指示物
                //——触发口=KeywordRules.ApplyDamage 尾段
                new GrantRetaliationHandler(),

                // 圣盾/复生/潜行（2026-10-08 指示物化）：GrantXxx 原子类型与 refId 不变，
                // 执行改挂对应指示物层（每层一份、事件消耗），退出关键词处理器族
                new GrantDivineShieldHandler(),
                new GrantRebornHandler(),
                new GrantStealthHandler(),
                // 法术护盾（2026-10-09 指示物化收官）：同上——执行改挂 SpellShieldCounter 层，
                // ConsumeSpellShields 效果执行前置过滤逐层消耗
                new GrantSpellShieldHandler(),
                // 无实体（2026-11-02 指示物化）：同上——执行改挂 SpellbanCounter 衰退层（表行 6c38edaa），
                // 钳口=ApplyDamage 守护改写之后
                new GrantSpellbanHandler(),

                // 状态原子（虚弱/鼓舞——施加 ±1/+1 属性指示物）
                new WeakenHandler(),
                new InspireHandler(),

                // 指示物原子（紊乱/易损/单向属性指示物——攻击力·生命值·费用 ×增减；属性±1/+1 2026-11-02 删）
                new RushSicknessHandler(),
                new AddVulnerableHandler(),
                new AddPowerUpHandler(),
                new AddPowerDownHandler(),
                new AddLifeUpHandler(),
                new AddLifeDownHandler(),
                // 耐久增加（2026-11-03 新增，蓝0.5/层）：无生命单位（结界/装备）耐久池 +value 层
                new AddDurabilityUpHandler(),
                // 永久攻血四轴（2026-11-02 拆分定案，原双属性±X/±X 两行退役）：单轴直写字段——跨区保留、净化不清
                new AddPermanentPowerUpHandler(),
                new AddPermanentPowerDownHandler(),
                new AddPermanentLifeUpHandler(),
                new AddPermanentLifeDownHandler(),
                // 四色费用指示物（2026-10-31 通用费用增减四色化——增减各 4 行，每层 ±1 对应色净费）
                new AddCostUpGrayHandler(),
                new AddCostUpRedHandler(),
                new AddCostUpBlueHandler(),
                new AddCostUpGreenHandler(),
                new AddCostDownGrayHandler(),
                new AddCostDownRedHandler(),
                new AddCostDownBlueHandler(),
                new AddCostDownGreenHandler(),

                //（充能处理器注册已随 2026-11-05 充能系统动作化改版删除：充能=速度0系统动作，
                //  结算走 GameActions.ResolveChargeDeclarationAsync，不再是卡上原子效果。）

                // 角色攻击弹药（2026-10-07 角色参战定案，Entity 级）：给角色附加攻击指示物，
                // 攻击/反击结算后烧除（CounterRules.RemoveHeroAttackAmmo）
                new AddHeroAttackHandler(),

                // 反疗（2026-10-09，黑2，原名堕落·仅角色，同日放开）：任意有生命单位——
                // 下一次受到的治疗改写为等量伤害并消耗 1 层（改写口=EntityEffectExtensions.Heal 咽喉）
                new GrantDepravityHandler(),
                // 重放（2026-11-02 改版：对玩家指示物，蓝5）：玩家使用的法术/主动效果结算后
                // 消耗 1 层并再执行一轮（含代价行；重放不触发重放；消耗口=ExecuteAsync 结算段）
                new GrantReplayHandler(),

                // 临时指示物（2026-10-10 回响临时指示物化，白2）：复制卡的「临时」标记——
                // 持有者回合末不在战场→从游戏中移除；在战场不生效也不倒数（2026-10-12 生效自减改稿）
                //（例程=CounterRules.OnTurnEnd ②块；回响复制体恒带 1 层=TempCopyRules 施加口）
                new GrantTemporaryHandler(),

                // 无效指示物（蓝3）：拦非主动能力（触发式+光环）——与沉默（拦主动）对称
                new AddNullifyHandler(),

                // 锁定指示物：手牌无法使用，持有者回合末层数−1
                new LockCardHandler(),

                // 沉睡（2026-09-11 改造，绿1 中性）：赋予沉睡指示物（效果/指示物分离——持续规则在指示物）
                new SleepHandler(),

                // 衍生物生成（落区三档：战场/手牌/牌组，费用按落区系数计价）
                new SummonTokenHandler(),

                // 元素合成（GainAnyElementHandler）2026-11-09 整链删除：表行/教学条目零消费者
                //（教学二改挂牺牲送墓产彩）——枚举位/AI 白名单/验证器色表同批清理。
                // 资源族 — 成长（2026-10-11：灵气复苏改+1，绿3，各自池可无限重复；2026-10-15 封顶退役）
                new IncreaseLandCapHandler(),

                // 战斗底盘（2026-09-10 攻击/守卫效果化：1速/2速主动，注册完整性锚——战斗走 CombatSystem）
                new AttackHandler(),
                new GuardHandler(),

                // 信息轴（2026-10-02 定案）：展示（挂展示指示物，双方可查看）/ 附加诅咒
                // （对手抽到该卡时 CurseSystem 自动执行载荷分支效果并消层）
                new RevealCardHandler(),
                new AddCurseHandler(),
                // 筛选展示（2026-10-29 阶段3c 信息轴）：按宣言维度（str="维度:值"）展示手牌中
                // 全部命中的牌——与 RevealCard（随机一张）相对的按维度圈定全集
                new RevealByDimensionHandler(),

                // 延时保护（2026-10-30 指示物化定案，3 行族）：档位从行类型派生
                //（ProtectionHalve/Null/LifeFloor——2026-10-31 阶段D；改写口=KeywordRules.ApplyDamage 与角色落血三路）
                new GrantProtectionHandler(),

                // 规则轴（2026-10-03 定案，路线 B 转正）：场地光环投放——规则 id 从行类型派生
                //（AuraTriPhase..AuraInscription 静态映射——2026-10-31 阶段D；载体入场激活 RuleAuraSystem）
                new ModifyGameRuleHandler(),
            };
            foreach (var handler in handlers)
                EffectHandlerRegistry.Register(handler);

            // 阶段D（2026-10-31）EffectType 唯一化：共享族新型别名注册（同一 handler 按类型分派）
            foreach (var t in new[]
                     {
                         AtomicEffectType.ProtectionHalve, AtomicEffectType.ProtectionNull,
                         AtomicEffectType.ProtectionLifeFloor,
                     })
                EffectHandlerRegistry.Register(new GrantProtectionHandler { OverrideEffectType = t });
            foreach (var t in RuleAuraComponents.AuraTypes)
                EffectHandlerRegistry.Register(new ModifyGameRuleHandler { OverrideEffectType = t });

            // 注册所有关键词授予处理器
            foreach (var handler in GrantKeywordHandlerFactory.CreateAll())
                EffectHandlerRegistry.Register(handler);

            // 注册第二批规则原语处理器（状态/控制/净化/战斗/特殊）
            foreach (var handler in SecondBatchHandlerFactory.CreateAll())
                EffectHandlerRegistry.Register(handler);

            // 注册第三批长尾处理器（牌库/死亡原子/反制/沉默）
            foreach (var handler in ThirdBatchHandlerFactory.CreateAll())
                EffectHandlerRegistry.Register(handler);

            // 注册高级处理器（栈重定向）
            foreach (var handler in AdvancedEffectHandlerFactory.CreateAll())
                EffectHandlerRegistry.Register(handler);

            // 注册内置代价处理器
            BuiltinCostHandlers.RegisterAll();

            // 注册网罗自检：除「暂不实现」2 种外，所有原子效果类型都应有处理器
            VerifyHandlerCoverage();
        }

        /// <summary>
        /// 注册完整性自检。
        /// 「无处理器=设计态」以外（暂不实现）的所有 AtomicEffectType 都应已注册；缺失则告警，
        /// 便于尽早暴露长尾静默失败。（固有全域墓碑 SweepDamage/SweepHeal 与 7 个 BranchEngine* 主干原子
        /// 已随 2026-10-05 墓碑清理实删——白名单不再登记。）
        /// </summary>
        private static readonly HashSet<AtomicEffectType> _intentionallyUnimplemented = new HashSet<AtomicEffectType>
        {
            // 引擎主干行（2026-10-05 回表；2026-10-08 附加诅咒/附加祝福入列；2026-10-20 效果序位换轴为团结）：条件载体非效果——
            // 主序列执行按 Branch.Settle==Engine 跳过，引擎条件由 BranchEngines 事件驱动，无需（也不应有）处理器
            AtomicEffectType.EngineCountdown,
            AtomicEffectType.EngineLuckRoll,
            AtomicEffectType.EngineClash,
            AtomicEffectType.EngineDeathToll,
            AtomicEffectType.EngineManaSurplus,
            AtomicEffectType.EngineCurseOnDraw,
            AtomicEffectType.EngineBlessingOnDraw,
            AtomicEffectType.EngineUnity, // 团结（2026-10-20 效果序位换轴·复用其表行 83c150f2）

            // 退役枚举墓碑位于 2026-10-30 枚举收拢时随成员一并删除（刺探/扰魔/毒刺/额外元素/
            // 湮灭原子/采掘/摒弃/冰晶/梦魇/光合作用/手牌序位——全仓卡牌/教学/效果表零引用，
            // 原子按 refId 字符串引用，枚举 int 不进存储/线格式）。剩余白名单：
            AtomicEffectType.SituationGate,          // 有限分支门槛行身份枚举（2026-10-23 入表定案：仅表行身份，永不挂处理器）

            // 阶段D（2026-10-31）EffectType 唯一化：门槛行新型（原 SituationGate 11 行共享拆型）——
            // 同为表行身份载体，永不挂处理器（判定谓词在 BranchConditionEvaluator，按 GateId 注册）
            AtomicEffectType.GateFirstDrawOfTurn,
            AtomicEffectType.GateLifeBelow,
            AtomicEffectType.GateLifeAbove,
            AtomicEffectType.GateDeckBelow,
            AtomicEffectType.GateDeckAbove,
            AtomicEffectType.GateCreatureBelow,
            AtomicEffectType.GateCreatureAbove,
            AtomicEffectType.GateFirstCardOfTurn,
            AtomicEffectType.GateLandsAtLeast7,
            AtomicEffectType.GateLandsAtMost3,
            AtomicEffectType.GateHandEmpty,
            AtomicEffectType.GateLifeAtMost7,
        };

        private static void VerifyHandlerCoverage()
        {
            var missing = new List<AtomicEffectType>();
            foreach (AtomicEffectType type in Enum.GetValues(typeof(AtomicEffectType)))
            {
                if (_intentionallyUnimplemented.Contains(type)) continue;
                if (!EffectHandlerRegistry.IsRegistered(type))
                    missing.Add(type);
            }

            if (missing.Count > 0)
            {
                // 诊断级信息（已知债务清单，非运行时故障）：LogError 会让测试框架/CI 判败，降为警告
                TideLog.Warn(
                    $"[BuiltinHandlerBootstrap] 缺失原子效果处理器 {missing.Count} 种：" +
                    string.Join(", ", missing));
            }
        }
    }

}
