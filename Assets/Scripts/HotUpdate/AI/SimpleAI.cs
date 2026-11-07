using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.AI;
using CardCore.Attribute;

namespace SynergyUI
{
    /// <summary>
    /// 通用脚本 AI（卡组无关，一期：动作耗尽）。策略模式（2026-09-21）：决策偏好经 AiStrategy 注入——
    /// 缺省 GeneralAiStrategy = 本类原通用行为；红快攻/绿慢速/蓝控制见 AiStrategy 派生。
    /// 每回合穷举可用动作并执行，直到无进展——费用花完、手牌打空、无可攻击单位、无可发动效果。
    /// 所有动作都走 GameActions / BattleController 既有入口；可行性由引擎校验（失败即静默跳过，卡留手不付费）。
    /// 清理对手场面作为通用目标偏好（非针对特定卡组）：攻击按解场收益决策（2026-11-01）——
    /// 单位价值=(攻+血)/2、攻击成本按 1 费，收益 &gt; 0 的换子无条件做；不赚再按策略倾向分化
    /// （红=打脸 / 绿=留守卫 / 蓝=看情况）。有害类原子的目标也优先对方单位
    /// （威胁降序——解场优先，2026-09-13 用户裁决）、有益类优先己方；无偏好回落引擎自动解析（targets=null）。
    /// 当前回合玩家即可驱动（AI 对 AI 验证用；BattleScreen 仍只对 P2 调用）。
    /// </summary>
    public sealed class SimpleAI : IAiTurnDriver
    {
        private const int MaxActionRounds = 64;   // 动作耗尽循环硬上限（保险，防效果自我循环）
        private const int MaxSettleAttempts = 32; // 排干栈重试上限（保险）

        /// <summary>人机对局最小动作间隔（毫秒）——表现层演完后的人为节拍下限，
        /// 保证出牌类瞬时动作（无长动画）也有可读节奏。只作用于注入了 PacingWait 的驱动。
        /// 2026-10-16 用户定案：AI 每个操作给 1 秒表演时间（400→1000）。</summary>
        private const int PacingMinimumMs = 1000;

        /// <summary>出牌舞台时长（毫秒，2026-10-16 用户定案）：声明成功后先亮
        /// （UI 层出左上角卡面+指向箭头）这么久再结算。只作用于注入了 PacingWait 的驱动。</summary>
        private const int CastStageMs = 1000;

        private readonly AiStrategy _strategy;

        /// <summary>注入策略（null=通用 GeneralAiStrategy，行为与策略化之前完全一致）。</summary>
        public SimpleAI(AiStrategy strategy = null) => _strategy = strategy ?? AiStrategy.General;

        /// <summary>注入策略只读口（2026-11-02 响应窗任务A）：BattleController/AiBattleDriver 开局
        /// 把驱动策略按玩家登记进 AiStrategyRegistry（响应窗按玩家查表决策）。</summary>
        public AiStrategy Strategy => _strategy;

        /// <summary>人机对局节奏（2026-10-14 用户定案）：非 null=电脑每个动作后等表现层把
        /// 当前这一件事演完（回调内部=等 BattleFieldView 视觉在途门归零）+ 最小间隔拍——
        /// 人类可逐步看清电脑操作。null=全速（AI-vs-AI/无头——完全不管渲染层）。
        /// 双人对局无 AI 不涉及。由 BattleScreen 在人类在场的分支注入。</summary>
        public Func<Cysharp.Threading.Tasks.UniTask> PacingWait { get; set; }

        /// <summary>出牌舞台开演回调（2026-10-16 人机节奏）：声明成功后、舞台等待前调用——
        /// UI 层亮出对手所出的卡（左上角卡面）与指向目标的箭头。参数=所出卡+声明期已选目标
        /// （targets 可能 null/空=无指向）。与 PacingWait 同批注入；null（无头/AI 互打）=无 UI 舞台。</summary>
        public Action<Card, List<Entity>> OnCastStaged;

        /// <summary>出牌舞台收幕回调：舞台等待结束（结算前）调用——UI 层隐藏卡面/箭头渐隐。</summary>
        public Action OnCastStageEnd;

        /// <summary>节奏等待：等门放行 + 最小动作间隔（未注入 PacingWait 时同步直通，
        /// 无头 TakeTurn 同步路径不受影响）。Delay 分流：Unity=UniTask.Delay（player loop 计时）；
        /// netstandard（TideServer deckstats 编译）无 Delay 成员——Task.Delay 等价，且该分支
        /// 仅在 PacingWait 非空（人类在场局）才可达，无头恒不走。</summary>
        private async Cysharp.Threading.Tasks.UniTask PaceAsync()
        {
            var wait = PacingWait;
            if (wait == null) return;
            await wait();
#if UNITY_EDITOR || UNITY_2020_1_OR_NEWER
            await Cysharp.Threading.Tasks.UniTask.Delay(PacingMinimumMs);
#else
            await System.Threading.Tasks.Task.Delay(PacingMinimumMs);
#endif
        }

        /// <summary>舞台保持原语：PresentationHold 拦住帧泵自动双 Pass（GameLoopController
        /// .ProcessPriority 无候选即抢先结算——裸等会被它下一帧排干），等视觉在途 + CastStageMs。
        /// 先 hold 再等：结算时序与旧同帧排干完全一致（触发式仍随排干一起落）。
        /// 未注入 PacingWait 同步直通；finally 保证终局/异常不漏还原。
        /// 2026-10-19 改引用计数口：结算事件会让逆结算回放导演也持有闸，bool 直写会互清。</summary>
        private async Cysharp.Threading.Tasks.UniTask StageHoldAsync(GameCore core)
        {
            var wait = PacingWait;
            if (wait == null) return;
            core.AcquirePresentationHold();
            try
            {
                await wait();
#if UNITY_EDITOR || UNITY_2020_1_OR_NEWER
                await Cysharp.Threading.Tasks.UniTask.Delay(CastStageMs);
#else
                await System.Threading.Tasks.Task.Delay(CastStageMs); // 同 PaceAsync：无头不可达
#endif
            }
            finally { core.ReleasePresentationHold(); }
        }

        /// <summary>出牌舞台总装（2026-10-16 用户定案）：声明成功 → UI 亮卡面/箭头（OnCastStaged）
        /// → hold+等待 CastStageMs → 收幕（OnCastStageEnd）→ 调用方再 SettleStack 结算。
        /// 无头/AI 互打（无 PacingWait）零舞台零等待，与旧同帧结算一致。</summary>
        private async Cysharp.Threading.Tasks.UniTask StageCastAsync(GameCore core, Card card, List<Entity> targets)
        {
            if (PacingWait == null) return;
            OnCastStaged?.Invoke(card, targets);
            try { await StageHoldAsync(core); }
            finally { OnCastStageEnd?.Invoke(); }
        }

        /// <summary>有害类原子：目标偏好对方单位 &gt; 对方玩家（解场优先——2026-09-13 用户裁决，原削血优先反转）</summary>
        private static readonly HashSet<AtomicEffectType> HarmfulAtoms = new HashSet<AtomicEffectType>
        {
            // 伤害族（解场优先——穿透档已删 2026-10-31）
            AtomicEffectType.DealDamage, AtomicEffectType.DealCombatDamage, AtomicEffectType.LifeLoss,
            AtomicEffectType.DrainLife,
            // 参照族直伤（2026-11-03 单目标改版：弹选目标=受害者——活体炸弹/护盾猛击）
            AtomicEffectType.BurstDamage, AtomicEffectType.ShieldSlam,
            // 指示物妨害族（毒素：回合末持续毒伤；易损：受伤+1/层；剧毒已转关键词 GrantVenom）
            AtomicEffectType.GrantVenom, AtomicEffectType.AddToxin, AtomicEffectType.AddVulnerable,
            // 单向属性削弱（攻击力/生命值/费用增加四色——目标偏好对方；属性±1/+1 2026-11-02 删）
            AtomicEffectType.AddPowerDown, AtomicEffectType.AddLifeDown,
            AtomicEffectType.AddCostUpGray, AtomicEffectType.AddCostUpRed,
            AtomicEffectType.AddCostUpBlue, AtomicEffectType.AddCostUpGreen,
            AtomicEffectType.Weaken, AtomicEffectType.RushSickness,
            // 破坏/移除族
            AtomicEffectType.Exile, AtomicEffectType.DiscardCard, AtomicEffectType.MillCard,
            AtomicEffectType.BounceToTop, AtomicEffectType.BounceToBottom,
            AtomicEffectType.Smash, // 摧毁：打对方地牌（无生命值单位）
            AtomicEffectType.SealUnit, // 封印：对方单位封为对手地牌（解场；域=敌方带费生物，滤口已收窄）
            // 死亡原子族（牺牲目标己方友军，目标偏好对该原子无实际影响；湮灭已删 2026-10-13）
            AtomicEffectType.Sacrifice, AtomicEffectType.Devour,
            // 妨碍/压制族
            AtomicEffectType.Tap, AtomicEffectType.Freeze, AtomicEffectType.Silence,
            AtomicEffectType.Purify, AtomicEffectType.AddNullify,
            // 两层无效（2026-10-04 定案）：发动无效（净零成本）/效果无效（扣费照付）
            AtomicEffectType.NegateEffect, AtomicEffectType.NegateActivation,
            // 夺取控制族
            AtomicEffectType.GainControl,
        };

        /// <summary>有益类原子：目标偏好己方单位 &gt; 己方玩家</summary>
        private static readonly HashSet<AtomicEffectType> BeneficialAtoms = new HashSet<AtomicEffectType>
        {
            // 治疗恢复族
            AtomicEffectType.Heal,
            AtomicEffectType.ReturnToHand, AtomicEffectType.SummonToken,
            AtomicEffectType.ReturnFromGraveyard, AtomicEffectType.RecoverToHand,
            // 资源族（2026-10-11：Mine 已删；2026-10-23：Photosynthesis 已换 GainAnyElement）
            AtomicEffectType.Untap, AtomicEffectType.AddArmor, AtomicEffectType.GainAnyElement,
            // 参照族增益（2026-11-03 单目标改版：弹选目标=护甲落点——活体护甲；入集才走策略偏好排序）
            AtomicEffectType.LivingArmor,
            // 强化族（GrantX 关键词与数值强化；单向属性增益——攻击力/生命值/±1/费用减少）
            AtomicEffectType.ModifyPower, AtomicEffectType.ModifyLife,
            // 设置系复活（2026-09-09 三轨制重建）——有益直改
            AtomicEffectType.SetPower, AtomicEffectType.SetLife, AtomicEffectType.SetCost,
            AtomicEffectType.AddPowerUp, AtomicEffectType.AddLifeUp,
            AtomicEffectType.AddCostDownGray, AtomicEffectType.AddCostDownRed,
            AtomicEffectType.AddCostDownBlue, AtomicEffectType.AddCostDownGreen,
            AtomicEffectType.Inspire,
            AtomicEffectType.GrantDoubleStrike,
            AtomicEffectType.GrantSpellShield,
            AtomicEffectType.GrantLifesteal,
            AtomicEffectType.GrantStealth, AtomicEffectType.GrantConcealed, AtomicEffectType.GrantTaunt,
            AtomicEffectType.GrantDivineShield, AtomicEffectType.GrantOverwhelm, AtomicEffectType.GrantToughness,
            AtomicEffectType.GrantRetaliation,
            AtomicEffectType.GrantFirstStrike,
            AtomicEffectType.GrantVigilance, AtomicEffectType.GrantRegeneration,
            AtomicEffectType.GrantReborn, AtomicEffectType.GrantIndestructible,
            AtomicEffectType.GrantLifelink,
            AtomicEffectType.DiscoverCard, AtomicEffectType.GrantGuardian,
            // 展开族
            AtomicEffectType.TakeExtraTurn,
        };

        /// <summary>同步入口（纯 AI 对局/无头：无人类响应者，全链同步完成）。</summary>
        public void TakeTurn(BattleController ctrl)
            => TakeTurnAsync(ctrl).GetAwaiter().GetResult();

        /// <summary>异步入口（UI 对局：战斗窗口可暂停等人类响应弹窗）。</summary>
        public async Cysharp.Threading.Tasks.UniTask TakeTurnAsync(BattleController ctrl)
        {
            var core = ctrl.Core;
            var me = ctrl.TurnPlayer;
            if (core == null || me == null || me != core.TurnEngine.TurnPlayer) return;

            // AI 目标排序钩子（2026-11-01）：本回合的自动目标选择按策略排序
            //（解横置选最高攻 / 代价选低攻或已横置 / 加攻集中同一生物——红快攻专用；
            // 代价候选>1 的对手选择段先经 AiStrategy.DispatchOrder 按对手视角重排，2026-11-02）。
            // 不还原：AI 轮次串行，下一回合由下一个 AI 覆盖；人类 chooser 不走该分支。
            TargetSelectionService.AiOrder = req => AiStrategy.DispatchOrder(_strategy, core, req?.Chooser ?? me, req);

            // 0. 准备阶段：跳过产元素阶段进入主阶段
            GameActions.SkipElementPool(core, me);

            // 1. 资源准备：补地到槽上限（费用最高的生物优先——打不出的当地产元素）。
            //    2026-10-11 现取现用：无回合初横置步骤——付费时系统按账单自动横置。
            PlayLands(core, me);

            // 1.5 先手一波（2026-11-04 交替定案）：策略要求时，先用**现成单位**打一轮，再进入出牌循环。
            //   旧结构=「铺满→打」，对手一张 AOE/回应费就能把整回合的量一波清掉（一点伤害打不出）；
            //   打成「打—铺—打」后，对手的回应费只能吃掉第一波，后续铺场照样出手。
            //   门槛在策略侧（默认关）——只有战斗前置更划算的局面才开。
            if (!core.IsGameOver && _strategy.WantEarlyCombat(core, me))
            {
                await DoCombatAsync(core, me);
                if (core.IsGameOver) { SettleStack(core); return; }
            }

            // 2. 动作耗尽主循环：各类动作每轮各试一遍（非短路 |），任一成功即继续轮询；
            //    出口 = 无进展：费用花完 / 手牌打空 / 无可发动效果
            for (int i = 0; i < MaxActionRounds && !core.IsGameOver; i++)
            {
                // 出牌/技能源已 async 化（含出牌舞台等待）；`|` 非短路——每轮仍全试一遍再判进展
                //（墓地出牌臂已随 2026-10-30 归土改版整删——归土=回合末自动复活，无 AI 动作位）
                // 2026-11-01：绿套·成长期技能先于出牌（策略钩子 SkillBeforeCards）——1 费回春藤
                // 会把绿地花光，技能（绿 3）就发不动了。
                bool progress = DrainVoluntaryQueue(core)
                              | ActivateBattlefieldAbilities(core, me);
                progress |= _strategy.SkillBeforeCards(core, me)
                    ? (await UseHeroSkillAsync(core, me) | await PlayBestAffordableCardAsync(core, me))
                    : (await PlayBestAffordableCardAsync(core, me) | await UseHeroSkillAsync(core, me));
                if (!progress) break;
                await PaceAsync(); // 人机节奏：本轮动作落地后等表现层演完再做下一个动作
            }

            // 2.5 充能（2026-11-05 系统动作化改版，低优先级）：动作耗尽后把手牌各卡宣一次充能
            //     （速度0系统动作·免费落层——一张卡一回合一次；颜色由引擎结算期反问代选=最大余量色）。
            ChargeIdleElements(core, me);

            // 3. 战前排干栈：让本回合发动/触发的效果先落地（增益类才影响攻击结算）
            SettleStack(core);

            // 4. 战斗（2026-09-16 逐攻击开窗）：每次宣言→响应窗口（人类对手=发动弹窗）→结算→下一攻
            if (!core.IsGameOver)
                await DoCombatAsync(core, me);

            // 4.5 战斗间再动（2026-09-21 激励时机修复 + 2026-11-01 技能优先）：
            //     先发动英雄技能（红=重整：解最高攻攻击者的横置，立刻再打一波），再补牌
            //     （激励/火灵等只有在攻击者横置后才有候选）；随后第二波攻击让解横置者再动一次。
            if (!core.IsGameOver)
            {
                bool actedAfterCombat = await UseHeroSkillAsync(core, me);
                for (int i = 0; i < 3; i++)
                {
                    if (!await PlayBestAffordableCardAsync(core, me)) break;
                    actedAfterCombat = true;
                    await PaceAsync(); // 人机节奏：战后补牌逐张可读
                }
                if (actedAfterCombat)
                    await DoCombatAsync(core, me);
            }

            // 5. 收尾：排干栈（栈未空时 EndTurn 会被栈空守卫静默拒绝）→ 结束回合。
            //    游戏已结束则不再 EndTurn（避免"★ 游戏结束"之后又出现阶段推进）
            SettleStack(core);
            if (!core.IsGameOver)
                GameActions.EndTurn(core, me);
        }

        // ======================================== 资源准备 ========================================

        /// <summary>充能（2026-11-05 系统动作化改版，低优先级）：主循环无进展后，把手牌各卡宣一次
        /// 充能（速度0系统动作·**免费落层**——一张卡一回合一次，CanCharge 闸内拒；上限跨回合逐补，
        /// 颜色由引擎结算期反问代选=最大余量色）。失败即静默跳过——不阻塞回合推进。
        /// 对手回合响应档已随改版退役（充能=速度0系统动作，0 速无法接链）。</summary>
        private static void ChargeIdleElements(GameCore core, Player me)
        {
            if (core.TurnEngine.TurnPlayer != me
                || core.TurnEngine.CurrentPhase?.Phase != PhaseType.Main) return;
            var hand = core.ZoneManager.GetCards(me, Zone.Hand) ?? new List<Card>();
            // S3b：剔施放中的卡（不可二次声明/不可弃）
            hand = hand.Where(c => !core.StackEngine.IsPendingCast(c)).ToList();
            foreach (var card in hand)
            {
                // 充能=速度0系统动作须空栈——宣一张结算一张（结算期反问代选色+落层）再宣下一张
                if (GameActions.DeclareCharge(core, me, card, out _))
                    SettleStack(core);
            }
        }

        /// <summary>
        /// 补地（定案策略）：只放"暂时打不出来"的生物（能付起的留手打出）——
        /// 付不起的里挑总费用最高的先放：作为地牌持续产元素（2026-10-11 锁色定案：不耗竭，
        /// 缺省锁主色）。全部付得起时不放（保护手里的关键词/高费随从上场面）。
        /// 地牌资格（正式三型，非衍生物/临时卡）由引擎权威校验，此处预检跳过无效尝试。
        /// </summary>
        private void PlayLands(GameCore core, Player me)
        {
            var lands = (core.ZoneManager.GetCards(me, Zone.Hand) ?? new List<Card>())
                .Where(c => !core.StackEngine.IsPendingCast(c)) // S3b：剔施放中的卡
                .Where(ElementPoolSystem.CanServeAsLand)
                .ToList();
            // 打不出的卡按策略地牌优先级排序（红：血偿等直伤打不出去时直接当地占坑；
            // 默认=总费用降序——原口径；绿：WantLand 可主动把付得起的牌也铺下去凑色）。
            var unaffordable = lands
                                    .Where(c => !CanPlayCard(core, me, c, Zone.Hand)
                                                || _strategy.WantLand(core, me, c))
                                    .Where(c => _strategy.ShouldLand(core, me, c))
                                    .OrderByDescending(c => _strategy.LandPriority(core, me, c, TotalCost))
                                    .ToList();
            // 兜底：手牌有生物但全部付得起——不放（留手打出，最大化场面交互）
            if (unaffordable.Count == 0) return;
            foreach (var card in unaffordable)
            {
                // 各自地牌槽上限（2026-10-11：个人曲线；缺省锁色=主色份额最多色）
                if (!core.ElementPool.CanAddLand(me)) break;
                // 2026-11-03：ShouldLand 允许按**当前地数**动态判定（红套「操控地≤3」奖励门）——
                // 快照列表是按放地前的地数筛的，前面每放一张都可能改变判定，故逐张重问。
                if (!_strategy.ShouldLand(core, me, card)) continue;
                // 2026-11-01 2 费空过修复：快照里打不出，但铺了前面的地之后已经付得起 → 留手打出，
                // 不再继续当地（例外：策略 WantLand 主动铺地，如绿套凑绿地）。
                if (CanPlayCard(core, me, card, Zone.Hand) && !_strategy.WantLand(core, me, card))
                    continue;
                var lc = _strategy.LandColorChoice(core, me, card);
                GameActions.AddToElementPool(core, me, card, lc);
            }
        }

        // ======================================== 动作耗尽循环的动作源 ========================================

        /// <summary>英雄技能（2026-10-07 卡牌化：技能=构筑标记的结界卡）：在场/未横置/未沉默/
        /// 费用可付由 HeroSkillSystem.ActivateAsync 统一守卫（含 lazy 回填）——此处只做策略筛选。
        /// 2026-10-16 async 化：await 替代同步阻塞；技能非出牌——舞台仅 hold 1 秒节奏，无卡面/箭头。</summary>
        private async Cysharp.Threading.Tasks.UniTask<bool> UseHeroSkillAsync(GameCore core, Player me)
        {
            if (!_strategy.WantHeroSkill(core, me))
                return false;

            var ok = await GameActions.ActivateHeroSkill(core, me);
            if (ok)
            {
                await StageHoldAsync(core);
                SettleStack(core);
            }
            return ok;
        }

        /// <summary>发动引擎已入队的可选待发效果（触发式可选项，"把能发动的都发动"）。
        /// 控制器过滤（2026-10-20）：待发桶双方共享且 GetActivatableVoluntaryEffects 无视激活方——
        /// 不滤会把玩家掷骰入队的 BRANCH_REWARD 当自己的发射（目标被 AI 代选）。</summary>
        private static bool DrainVoluntaryQueue(GameCore core)
        {
            bool any = false;
            var me = core.StackEngine.CurrentPriorityHolder;
            // 快照遍历：发动（出队入栈）本身会改动待发队列
            var pending = new List<PendingEffect>(core.StackEngine.GetActivatableVoluntaryEffects(me));
            foreach (var effect in pending)
            {
                if (effect.Controller != me) continue; // 只发动自己队列里的待发
                if (core.StackEngine.PlayerActivateVoluntary(effect))
                    any = true;
            }
            return any;
        }

        /// <summary>枚举场上卡的主动能力并逐个发动（CanActivate 预检 + 引擎权威校验）。</summary>
        private static bool ActivateBattlefieldAbilities(GameCore core, Player me)
        {
            bool any = false;
            var battlefield = new List<Card>(core.ZoneManager.GetCards(me, Zone.Battlefield) ?? new List<Card>());
            var executor = core.StackEngine.GetExecutor();
            var phase = core.TurnEngine.CurrentPhase?.Phase ?? PhaseType.Main;
            foreach (var source in battlefield)
            {
                foreach (var def in GetEffectDefinitions(source))
                {
                    if (!def.IsActivatedEffect) continue; // 只管主动；触发式由引擎事件驱动
                    if (!executor.CanActivate(def, source, me, me, phase, core.TurnEngine.TurnNumber)) continue;
                    if (GameActions.ActivateEffect(core, me, def, source))
                        any = true;
                }
            }
            return any;
        }

        /// <summary>出一张最优的牌：优先级经策略 CardPlayScore（通用=有害原子优先 → 总费用降序——大费先出，资源花光）。
        /// 抉择卡先选模式（ChooseMode——机器版「满足才能使用」），全模式不可用换下一张。
        /// 出牌即上栈（使用时点）：舞台亮 CastStageMs（卡面+指向箭头）→ 排干——cast 结算付费后
        /// bank 已更新，下一轮预检不失真。</summary>
        private async Cysharp.Threading.Tasks.UniTask<bool> PlayBestAffordableCardAsync(GameCore core, Player me)
        {
            var hand = core.ZoneManager.GetCards(me, Zone.Hand) ?? new List<Card>();
            // S3b：剔施放中的卡（不可二次声明/不可弃）
            hand = hand.Where(c => !core.StackEngine.IsPendingCast(c)).ToList();
            var ordered = hand
                .Where(c => CanPlayCard(core, me, c, Zone.Hand))
                .Where(c => !_strategy.ShouldHold(core, me, c)) // 攒牌闸（2026-10-31 红快攻斩杀线）：刻意留手
                .OrderByDescending(c => _strategy.CardPlayScore(core, me, c, ContainsHarmfulAtom, TotalCost))
                .ToList();
            foreach (var card in ordered)
            {
                int mode = ChooseMode(core, me, card);
                if (mode < 0) continue; // 抉择卡全模式不可发动 → 换下一张
                // 策略钉目标（2026-11-04 用户定案）：血偿只打 >4 血、连击只贴 ≥4 攻的可攻击单位——
                // 通用择优够不到这些口径。null=不接管（回落通用），空表=本次不满足就不打。
                var pinned = _strategy.PickSpellTargets(core, me, card, mode);
                if (pinned != null && pinned.Count == 0) continue;
                // 失败静默重试下一张（引擎契约：不付费、卡留手）
                var targets = pinned ?? ChooseTargets(core, me, card, mode);
                if (GameActions.PlayCard(core, me, card, targets, Zone.Hand, mode))
                {
                    await StageCastAsync(core, card, targets); // 出牌舞台：亮 1 秒（卡面+箭头）再结算
                    SettleStack(core); // cast 已上栈：双 Pass 排干（AI 无响应 → 立即结算付费）
                    return true;
                }
            }
            return false;
        }

        // 墓地出牌（TryGraveyardPlayAsync）已随 2026-10-30 归土改版整删——归土=回合末自动复活，无出牌动作位。

        // ======================================== 出牌辅助 ========================================

        /// <summary>出牌预检（引擎无公开 CanPlay，自行组合规则钩子 + 支付力 + 组合域目标预检）。
        /// 抉择卡按「任一模式可付」判（声明值=最大模式费，单值预检会漏掉便宜模式；
        /// 目标逐模式判定在 ChooseMode/ModeHasValidTarget）。非抉择卡接
        /// TargetDomainService.HasPlayableTargets（与引擎 PlayCard/CanActivate 同口径——
        /// 组合域候选空不可发动，2026-09-10 目标域模型）。</summary>
        private static bool CanPlayCard(GameCore core, Player me, Card card, Zone fromZone)
        {
            if (!RuleHooks.CanPlay(core, me, card, fromZone)) return false;
            if (card is CardWrapper modal && CostDerivationService.GetModeCount(modal.GetData()) >= 2)
            {
                int modes = CostDerivationService.GetModeCount(modal.GetData());
                for (int i = 0; i < modes; i++)
                    if (GameActions.CanAffordCast(core, me, card, i)) return true;
                return false;
            }
            if (!TargetDomainService.HasPlayableTargets(core, me, card, 0)) return false;
            // 2026-11-04 修复（TideSim 抓出）：法术的 CardData.Cost 是**推导缓存**（主动效果锚价不进这里，
            // 通常是 0），实际施放价只在 GameActions.GetCardCost 才带锚价。旧口用 hc.Cost.IsZero 跳过
            // 支付力检查，导致 3 费法术在 2 地时被当成「能打」——既不进 PlayLands 的「打不出→当地」列表，
            // 尝试施放又必然失败，红套会永远卡在 2 地、3 费线全废。这里改用真实费用判定。
            var castCost = GameActions.GetCardCost(card, 0);
            if (castCost != null && !castCost.IsZero
                && !GameActions.CanAffordCast(core, me, card, 0)) return false;
            return true;
        }

        /// <summary>
        /// 抉择模式选择（机器版「满足才能使用」定案）：逐模式条件过滤（费用可付 + 目标可得），
        /// 再按策略 ModeScore 打分（通用=有害原子优先 → 大费先出）。
        /// 返回 -1 = 全模式不可发动。非抉择卡恒 0。
        /// </summary>
        private int ChooseMode(GameCore core, Player me, Card card)
        {
            if (!(card is CardWrapper wrapper)) return 0;
            int modeCount = CostDerivationService.GetModeCount(wrapper.GetData());
            if (modeCount <= 1) return 0;

            int best = -1;
            float bestScore = float.NegativeInfinity;
            for (int i = 0; i < modeCount; i++)
            {
                var cost = GameActions.GetCardCost(card, i);
                if (!GameActions.CanAffordCast(core, me, card, i)) continue;
                if (!ModeHasValidTarget(core, me, card, i)) continue;

                var atomic = FirstTargetingAtomic(card, i, out _);
                bool harmful = atomic != null && HarmfulAtoms.Contains(atomic.Type);
                float total = cost.Total;

                float score = _strategy.ModeScore(harmful, total);
                if (best < 0 || score > bestScore)
                {
                    best = i;
                    bestScore = score;
                }
            }
            return best;
        }

        /// <summary>该模式是否有可选目标（组合域口径：def 预计算域交集 + 组合 filter；域空恒真）。</summary>
        private static bool ModeHasValidTarget(GameCore core, Player me, Card card, int modeIndex)
        {
            var def = FirstDomainEffect(card, modeIndex);
            if (def == null) return true; // 无目标效果（域空）

            var domain = DomainOfMode(def, modeIndex);
            var candidates = CardCore.Attribute.EffectHandlerRegistry.ResolveCandidates(domain, def.TargetFilter, BuildTargetCtx(core, me, card));
            return candidates.Count > 0;
        }

        /// <summary>
        /// 通用目标选择（2026-09-10 组合域口径）：候选集 = def 预计算域 + 组合 filter（与引擎
        /// ResolveCompositionTargetsAsync 同源）；按 SelectionMode 六值三维出目标（2026-09-16）——
        /// 全取档全取、选一/选多按数量 + 有害/有益偏好经策略择优（域={Self} 的选一=取源卡自身）。
        /// 无偏好返回 null 交引擎自动解析。
        /// v8（2026-11-02 原子口径统一）：逐原子口径（PerAtomTargets）按 CollectAtomTargetSpecs 单源
        /// 规格逐条预选——数量=每原子 effCount（>1 压过选一模式，预选填满 quota，不再依赖引擎
        /// 欠额补足插队），排序偏好按各原子有害/有益逐条择优，返回序=原子执行序（预选队列有序消费）。
        /// </summary>
        private List<Entity> ChooseTargets(GameCore core, Player me, Card card, int modeIndex = 0)
        {
            var def = FirstDomainEffect(card, modeIndex);
            if (def == null) return null; // 无需选目标 → 引擎自动

            var ctx = BuildTargetCtx(core, me, card);

            if (def.PerAtomTargets)
            {
                var picked = new List<Entity>();
                foreach (var spec in CardCore.Attribute.EffectHandlerRegistry.CollectAtomTargetSpecs(def, ctx))
                {
                    if (spec.AutoTake || spec.EffCount == -1) continue; // 引擎自动取/运行时自选——不预选
                    IEnumerable<Entity> specOrdered;
                    if (spec.Atom != null && HarmfulAtoms.Contains(spec.Atom.Type))
                        specOrdered = _strategy.OrderHarmfulTargets(core, me, spec.Candidates);
                    else if (spec.Atom != null && BeneficialAtoms.Contains(spec.Atom.Type))
                        specOrdered = _strategy.OrderBeneficialTargets(core, me, spec.Candidates);
                    else
                        specOrdered = spec.Candidates; // 无偏好：保持引擎序（等价自动解析）
                    picked.AddRange(specOrdered.Take(Math.Max(1, spec.EffCount)));
                }
                return picked.Count > 0 ? picked : null;
            }

            var domain = DomainOfMode(def, modeIndex);
            var candidates = CardCore.Attribute.EffectHandlerRegistry.ResolveCandidates(domain, def.TargetFilter, ctx);
            if (candidates.Count == 0) return null;

            switch (def.SelectionMode)
            {
                // 全取档（Whole/WholeUnion，2026-09-16 六值迁移）：全域全取
                case SelectionMode.Whole:
                case SelectionMode.WholeUnion:
                    return candidates.ToList();
            }

            // 选一/选多（含域={Self} 的 Single——候选=[源卡]，通用路径 Take 即源卡自身）：
            // AI 机器选择——数量按 def.TargetCount（未声明回落 1），偏好按首个带域原子
            //（None 已在 FirstDomainEffect 跳过，不会走到这里；RandomTarget 不影响 AI 代替选取口径）
            int need = SelectionModeRules.IsPickOne(def.SelectionMode) ? 1
                : (def.TargetCount > 0 ? def.TargetCount : 1);
            var atomic = FirstTargetingAtomic(card, modeIndex, out _); // 仅作有害/有益偏好参考
            IEnumerable<Entity> ordered;
            if (atomic != null && HarmfulAtoms.Contains(atomic.Type))
            {
                // 有害：目标偏好经策略（通用=对方存活单位威胁降序 > 对方玩家 > 己方垫底——解场优先）
                ordered = _strategy.OrderHarmfulTargets(core, me, candidates);
            }
            else if (atomic != null && BeneficialAtoms.Contains(atomic.Type))
            {
                // 有益：目标偏好经策略（通用=己方单位 > 己方玩家）
                ordered = _strategy.OrderBeneficialTargets(core, me, candidates);
            }
            else
            {
                ordered = candidates; // 无偏好：保持引擎序（等价自动解析）
            }
            return ordered.Take(need).ToList();
        }

        /// <summary>目标解析上下文（镜像引擎来源归因 2026-09-09 定案：法术效果来源=角色、
        /// 场上卡效果来源=该卡——三轨判轨与目标过滤都按 Source 取义，模拟必须与真实执行同轨）。</summary>
        private static EffectExecutionContext BuildTargetCtx(GameCore core, Player me, Card card)
        {
            return new EffectExecutionContext
            {
                Source = card.IsSpellCard() ? (Entity)me : card,
                Controller = me,
                ZoneManager = core.ZoneManager,
                ElementPool = core.ElementPool,
                ModeIndex = 0,
            };
        }

        /// <summary>第一个带组合域的非主动效果定义（域 = TargetDomain / ChoiceDomains[mode]
        /// 预计算交集——目标域模型口径，取代旧「首个带域原子」编译级近似）。
        /// None 模式（区域自结算类）跳过——AI 不为其选目标（2026-09-11）。
        /// v8（2026-11-02）：None 过滤对逐原子口径（PerAtomTargets）豁免——选择参数在原子上，
        /// header None 不代表无交互原子（与 LAE.FirstDomainEffect 同口径）。
        /// 法术分支（2026-11-02 逐原子验收补齐）：法术施放效果按 P12 全是主动档
        ///（GetCastableEffectDefinitions 口径），旧「跳过一切主动」使法术恒 null 走引擎
        /// 自动选首（无策略偏好）——镜像 LAE：IsSpellCard → castable[modeIndex]。</summary>
        private static EffectDefinition FirstDomainEffect(Card card, int modeIndex)
        {
            if (CardExtensions.IsSpellCard(card))
            {
                // P12：modeIndex 索引非充能可施放列表（充能不算法术模式——与引擎
                // SpellCastAnchor/ResolveSpellEffects 同序，ChooseMode 同源）
                var castable = GameActions.GetCastableEffectDefinitions(card);
                if (castable.Count == 0) return null;
                var spellDef = castable[Math.Clamp(modeIndex, 0, castable.Count - 1)];
                if (spellDef == null) return null;
                if (spellDef.SelectionMode == SelectionMode.None && !spellDef.PerAtomTargets) return null;
                return spellDef.TargetDomain != null && spellDef.TargetDomain.Count > 0 ? spellDef : null;
            }
            foreach (var def in GetEffectDefinitions(card))
            {
                if (def.IsActivatedEffect) continue; // 施放即结算的才会随出牌自动跑
                if (def.SelectionMode == SelectionMode.None && !def.PerAtomTargets) continue; // 区域自结算：不选目标
                var domain = DomainOfMode(def, modeIndex);
                if (domain != null && domain.Count > 0) return def;
            }
            return null;
        }

        /// <summary>def 的组合域（ChoiceDomains 已随 2026-10-31 P12 抉择退役恒 null——恒 TargetDomain）。</summary>
        private static List<int> DomainOfMode(EffectDefinition def, int modeIndex)
        {
            return def.TargetDomain;
        }

        /// <summary>该卡第一个带域原子（仅作有害/有益目标偏好的参考；域/数量/模式判定已由
        /// FirstDomainEffect 的组合域口径承担）。modeIndex：抉择卡按所选模式扫描（主序列+
        /// 所选 choice 内原子，跳过分支奖励）。
        /// 无表行 fallback 原子（BounceToTop/Bottom 等读心预言先例）：converter 已把实例域
        ///（条目声明 ?? 表级默认）存进 atomic.TargetKinds——实例带域同样参与偏好判定，
        /// 否则这类原子目标偏好失效回落引擎序（候选首位常=自己，观测到自指空放）。</summary>
        private static AtomicEffectInstance FirstTargetingAtomic(Card card, int modeIndex, out AtomicEffectConfig cfg)
        {
            cfg = null;
            foreach (var def in GetEffectDefinitions(card))
            {
                if (def.IsActivatedEffect) continue; // 施放即结算的才会随出牌自动跑

                if (def.Steps != null && def.Steps.Count > 0)
                {
                    foreach (var atomic in CardEffectConverter.EnumerateMainSequenceAtoms(def.Steps, modeIndex))
                    {
                        var c = AtomicEffectTable.GetByType(atomic.Type);
                        if (HasTargetDomain(atomic, c))
                        {
                            cfg = c;
                            return atomic;
                        }
                    }
                    continue;
                }

                foreach (var atomic in def.Effects)
                {
                    var c = AtomicEffectTable.GetByType(atomic.Type);
                    if (HasTargetDomain(atomic, c))
                    {
                        cfg = c;
                        return atomic;
                    }
                }
            }
            return null;
        }

        /// <summary>原子是否带目标域：表行域非空，或无表行但实例域非空（fallback 原子）。</summary>
        private static bool HasTargetDomain(AtomicEffectInstance atomic, AtomicEffectConfig cfg)
            => (cfg != null && cfg.GetTargetKindList().Count > 0)
            || (atomic?.TargetKinds != null && atomic.TargetKinds.Count > 0);

        /// <summary>卡效果转译（CardData → EffectDefinition 列表）；非 CardWrapper 或无效果返回空。</summary>
        private static List<EffectDefinition> GetEffectDefinitions(Card card)
        {
            if (!(card is CardWrapper wrapper)) return new List<EffectDefinition>();
            var data = wrapper.GetData();
            if (data?.Effects == null || data.Effects.Count == 0) return new List<EffectDefinition>();
            return CardEffectConverter.ConvertAll(data.Effects, data.ID);
        }

        /// <summary>是否含非主动的有害原子（出牌优先级依据）。Steps/抉择卡扫主序列与全部模式。</summary>
        private static bool ContainsHarmfulAtom(Card card)
        {
            foreach (var def in GetEffectDefinitions(card))
            {
                if (def.IsActivatedEffect) continue;
                foreach (var atomic in def.Effects)
                    if (HarmfulAtoms.Contains(atomic.Type)) return true;
                if (def.Steps == null) continue;
                foreach (var step in def.Steps)
                {
                    if (step == null) continue;
                    if (step.Kind == RuntimeStepKind.Atomic && step.Atomic != null
                        && HarmfulAtoms.Contains(step.Atomic.Type)) return true;
                    // Choice 步骤扫描已随 2026-10-31 P12 抉择退役删除（converter 不再生成）
                }
            }
            return false;
        }

        /// <summary>总费用（排序键）：抉择卡取最大模式费（保持「大费先出」直觉）。</summary>
        private static float TotalCost(Card card)
        {
            if (card is CardWrapper modal && CostDerivationService.HasChoiceEffect(modal.GetData()))
            {
                float max = 0f;
                int modes = CostDerivationService.GetModeCount(modal.GetData());
                for (int i = 0; i < modes; i++)
                {
                    float total = GameActions.GetCardCost(card, i).Total;
                    if (total > max) max = total;
                }
                return max;
            }
            return card is IHasCost hc && hc.Cost != null ? hc.Cost.Total : 0f;
        }

        // ======================================== 战斗（2026-09-16 逐攻击开窗） ========================================

        /// <summary>逐攻击开窗：每个可攻击单位按策略选目标宣言（解场收益优先，见 AiStrategy.PickAttackTarget；
        /// 速度0上栈）→ 响应窗口（SettleResponseWindowAsync：对方人类=发动弹窗/AI=守卫启发/无头=自动双 Pass）
        /// → 结算 → 下一攻。攻击资格由引擎权威判定（横置/零攻/无攻资格卡）。</summary>
        private async Cysharp.Threading.Tasks.UniTask DoCombatAsync(GameCore core, Player me)
        {
            var opp = me.Opponent;
            if (opp == null) return;
            var battlefield = new List<Card>(core.ZoneManager.GetCards(me, Zone.Battlefield) ?? new List<Card>());
            // 攻击顺序（2026-11-01 解场定案）：按单位价值「(攻+血)/2」升序——便宜单位先动，
            // 正收益换子优先由它吃下（少亏身材），贵单位留给打脸/斩杀；小单位先撞守卫也能让
            // 拦截者先横置（守卫拦截后横置，后续攻击不再被同一守卫拦）。
            battlefield.Sort((a, b) => AttackOrderValue(core, a).CompareTo(AttackOrderValue(core, b)));
            foreach (var unit in battlefield)
            {
                if (core.IsGameOver) return;
                if (!core.CombatSystem.CanDeclareAttack(unit, me)) continue;
                var target = _strategy.PickAttackTarget(core, me, unit, opp); // 攻击决策经策略
                if (target == null) continue;
                // 攻击前一拍（2026-11-04 用户定案）：连击/血偿跟这一击一起用（只是操作顺序）——
                // 先按要求把牌打出去并结算，再宣言攻击（打不动/无牌则原样继续）。
                var preTargets = new List<Entity>();
                var preCard = _strategy.PickAttackTimeSpell(core, me, unit, target, preTargets);
                if (preCard != null && GameActions.PlayCard(core, me, preCard, preTargets, Zone.Hand, 0))
                {
                    SettleStack(core);
                    if (core.IsGameOver) return;
                    // 血偿可能把原定目标打掉 → 重新择目标（重选不到就跳过这一攻）
                    target = _strategy.PickAttackTarget(core, me, unit, opp);
                    if (target == null) continue;
                }
                if (!GameActions.DeclareAttack(core, me, unit, target)) continue;
                await GameActions.SettleResponseWindowAsync(core); // 窗口+结算（含级联触发/SBA 轮）
                await PaceAsync(); // 人机节奏：等本次攻击演出（走位/交击/死亡漩涡）演完再宣言下一攻
            }

            // 角色攻击（2026-10-10 角色参战遗留收口）：弹药>0 即宣言——攻击力=HeroAttackCounter 层数，
            // 结算后引擎烧光弹药（CombatSystem）；目标走角色专用启发式（PickAttackTarget 收 Card 不通用）。
            if (!core.IsGameOver && core.CombatSystem.CanDeclareAttack(me, me))
            {
                var heroTarget = PickHeroAttackTarget(core, me, opp);
                if (heroTarget != null && GameActions.DeclareAttack(core, me, me, heroTarget))
                {
                    await GameActions.SettleResponseWindowAsync(core);
                    await PaceAsync(); // 人机节奏：角色攻击演出同样等演完
                }
            }
        }

        /// <summary>攻击顺序估值（升序用）：与 AiStrategy.UnitValue 同口径（不除 2，比较等价）——
        /// 便宜单位先出手，先吃正收益换子（2026-11-01）。</summary>
        private static float AttackOrderValue(GameCore core, Card unit)
            => core.LayerEngine.CalculatePower(unit) + unit.GetLife();

        /// <summary>角色攻击目标（镜像 PickAttackTarget 清场口径，弹药一次烧光的简化版）：
        /// 致死打脸 ＞ 弹药打得死的最高威胁随从（打不死不白送）＞ 打脸兜底。</summary>
        private Entity PickHeroAttackTarget(GameCore core, Player me, Player opp)
        {
            var combat = core.CombatSystem;
            int power = me.GetPower(); // Player 分支=弹药层数钳非负
            if (power >= opp.Life && combat.CanAttackTarget(me, opp, me)) return opp;

            var oppField = core.ZoneManager.GetCards(opp, Zone.Battlefield) ?? new List<Card>();
            Card pick = null;
            int pickThreat = int.MinValue;
            foreach (var enemy in oppField)
            {
                if (!enemy.IsAlive || !combat.CanAttackTarget(me, enemy, me)) continue;
                if (power < enemy.GetLife()) continue; // 打不死——不白送弹药，交还打脸
                int threat = core.LayerEngine.CalculatePower(enemy);
                if (pick == null || threat > pickThreat)
                {
                    pick = enemy;
                    pickThreat = threat;
                }
            }
            if (pick != null) return pick;
            return combat.CanAttackTarget(me, opp, me) ? (Entity)opp : null;
        }

        // 攻击目标决策已上移 AiStrategy.PickAttackTarget（2026-09-21 策略模式；
        // 力量口径同前——LayerEngine 实时值，权威结算仍在引擎）。

        // ======================================== 收尾 ========================================

        /// <summary>排干栈：双 Pass 触发结算（AI 自动目标为同步路径，通常即时排干）。带重试上限保险。
        /// 引擎统一实现见 GameActions.DrainStack（验证器/UI 快进与 AI 共用）。</summary>
        private static void SettleStack(GameCore core)
        {
            GameActions.DrainStack(core, MaxSettleAttempts);
        }
    }
}


