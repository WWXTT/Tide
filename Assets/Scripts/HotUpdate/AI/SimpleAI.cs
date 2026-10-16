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
    /// 清理对手场面作为通用目标偏好（非针对特定卡组）：攻击优先击杀对方随从（优换 > 换子），
    /// 无击杀机会才打脸（打脸被守卫拦由引擎守卫转移承担——帷幕不拦攻击）；有害类原子的目标也优先对方单位
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
        /// 无头 TakeTurn 同步路径不受影响）。</summary>
        private async Cysharp.Threading.Tasks.UniTask PaceAsync()
        {
            var wait = PacingWait;
            if (wait == null) return;
            await wait();
            await Cysharp.Threading.Tasks.UniTask.Delay(PacingMinimumMs);
        }

        /// <summary>舞台保持原语：PresentationHold 拦住帧泵自动双 Pass（GameLoopController
        /// .ProcessPriority 无候选即抢先结算——裸等会被它下一帧排干），等视觉在途 + CastStageMs。
        /// 先 hold 再等：结算时序与旧同帧排干完全一致（触发式仍随排干一起落）。
        /// 未注入 PacingWait 同步直通；finally 保证终局/异常不漏还原。</summary>
        private async Cysharp.Threading.Tasks.UniTask StageHoldAsync(GameCore core)
        {
            var wait = PacingWait;
            if (wait == null) return;
            core.PresentationHold = true;
            try
            {
                await wait();
                await Cysharp.Threading.Tasks.UniTask.Delay(CastStageMs);
            }
            finally { core.PresentationHold = false; }
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
            // 伤害族（穿透伤害也偏好打脸——越过多层防护直击）
            AtomicEffectType.DealDamage, AtomicEffectType.DealCombatDamage, AtomicEffectType.LifeLoss,
            AtomicEffectType.DrainLife, AtomicEffectType.PierceDamage,
            // 指示物妨害族（毒素：回合末持续毒伤；易损：受伤+1/层；剧毒已转关键词 GrantVenom）
            AtomicEffectType.GrantVenom, AtomicEffectType.AddToxin, AtomicEffectType.AddVulnerable,
            // 单向属性削弱（攻击力/生命值/±1/费用增加——目标偏好对方）
            AtomicEffectType.AddPowerDown, AtomicEffectType.AddLifeDown, AtomicEffectType.AddMinusOne,
            AtomicEffectType.AddCostUp, AtomicEffectType.Weaken, AtomicEffectType.RushSickness,
            // 破坏/移除族
            AtomicEffectType.Exile, AtomicEffectType.DiscardCard, AtomicEffectType.MillCard,
            AtomicEffectType.BounceToTop, AtomicEffectType.BounceToBottom,
            AtomicEffectType.Smash, // 摧毁：打对方地牌（无生命值单位）
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
            // 资源族（2026-10-11：Mine 已删、AdditionalEnergy 已换 Photosynthesis）
            AtomicEffectType.Untap, AtomicEffectType.AddArmor, AtomicEffectType.GrantPhotosynthesis,
            // 强化族（GrantX 关键词与数值强化；单向属性增益——攻击力/生命值/±1/费用减少）
            AtomicEffectType.ModifyPower, AtomicEffectType.ModifyLife,
            // 设置系复活（2026-09-09 三轨制重建）——有益直改
            AtomicEffectType.SetPower, AtomicEffectType.SetLife, AtomicEffectType.SetCost,
            AtomicEffectType.AddPowerUp, AtomicEffectType.AddLifeUp, AtomicEffectType.AddPlusOne,
            AtomicEffectType.AddCostDown, AtomicEffectType.Inspire,
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

            // 0. 准备阶段：跳过产元素阶段进入主阶段
            GameActions.SkipElementPool(core, me);

            // 1. 资源准备：补地到槽上限（费用最高的生物优先——打不出的当地产元素）。
            //    2026-10-11 现取现用：无回合初横置步骤——付费时系统按账单自动横置。
            PlayLands(core, me);

            // 2. 动作耗尽主循环：各类动作每轮各试一遍（非短路 |），任一成功即继续轮询；
            //    出口 = 无进展：费用花完 / 手牌打空 / 无可发动效果
            for (int i = 0; i < MaxActionRounds && !core.IsGameOver; i++)
            {
                // 出牌/技能源已 async 化（含出牌舞台等待）；`|` 非短路——每轮仍全试一遍再判进展
                bool progress = DrainVoluntaryQueue(core)
                              | ActivateBattlefieldAbilities(core, me)
                              | await PlayBestAffordableCardAsync(core, me)
                              | await TryGraveyardPlayAsync(core, me)
                              | await UseHeroSkillAsync(core, me);
                if (!progress) break;
                await PaceAsync(); // 人机节奏：本轮动作落地后等表现层演完再做下一个动作
            }

            // 3. 战前排干栈：让本回合发动/触发的效果先落地（增益类才影响攻击结算）
            SettleStack(core);

            // 4. 战斗（2026-09-16 逐攻击开窗）：每次宣言→响应窗口（人类对手=发动弹窗）→结算→下一攻
            if (!core.IsGameOver)
                await DoCombatAsync(core, me);

            // 4.5 战斗间补牌（2026-09-21 激励时机修复）：激励（Untap·filter=Tapped）只有在攻击者
            //     横置后打出才有候选——第一波攻击完打出激励（红策略指向攻击力最高者）解横置，
            //     随后第二波攻击让被激励者再动一次（CanDeclareAttack 只放行已解横置者）。
            if (!core.IsGameOver)
            {
                bool playedAfterCombat = false;
                for (int i = 0; i < 3; i++)
                {
                    if (!await PlayBestAffordableCardAsync(core, me)) break;
                    playedAfterCombat = true;
                    await PaceAsync(); // 人机节奏：战后补牌逐张可读
                }
                if (playedAfterCombat)
                    await DoCombatAsync(core, me);
            }

            // 5. 收尾：排干栈（栈未空时 EndTurn 会被栈空守卫静默拒绝）→ 结束回合。
            //    游戏已结束则不再 EndTurn（避免"★ 游戏结束"之后又出现阶段推进）
            SettleStack(core);
            if (!core.IsGameOver)
                GameActions.EndTurn(core, me);
        }

        // ======================================== 资源准备 ========================================

        /// <summary>
        /// 补地（定案策略）：只放"暂时打不出来"的生物（能付起的留手打出）——
        /// 付不起的里挑总费用最高的先放：作为地牌持续产元素（2026-10-11 锁色定案：不耗竭，
        /// 缺省锁主色）。全部付得起时不放（保护手里的关键词/高费随从上场面）。
        /// 地牌资格（正式三型，非衍生物/临时卡）由引擎权威校验，此处预检跳过无效尝试。
        /// </summary>
        private static void PlayLands(GameCore core, Player me)
        {
            var lands = (core.ZoneManager.GetCards(me, Zone.Hand) ?? new List<Card>())
                .Where(ElementPoolSystem.CanServeAsLand)
                .ToList();
            var unaffordable = lands.Where(c => !CanPlayCard(core, me, c, Zone.Hand))
                                    .OrderByDescending(TotalCost)
                                    .ToList();
            // 兜底：手牌有生物但全部付得起——不放（留手打出，最大化场面交互）
            if (unaffordable.Count == 0) return;
            foreach (var card in unaffordable)
            {
                // 各自地牌槽上限（2026-10-11：个人曲线；缺省锁色=主色份额最多色）
                if (!core.ElementPool.CanAddLand(me)) break;
                GameActions.AddToElementPool(core, me, card);
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

        /// <summary>发动引擎已入队的可选待发效果（触发式可选项，"把能发动的都发动"）。</summary>
        private static bool DrainVoluntaryQueue(GameCore core)
        {
            bool any = false;
            // 快照遍历：发动（出队入栈）本身会改动待发队列
            var pending = new List<PendingEffect>(core.StackEngine.GetActivatableVoluntaryEffects(core.StackEngine.CurrentPriorityHolder));
            foreach (var effect in pending)
            {
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
            var ordered = hand
                .Where(c => CanPlayCard(core, me, c, Zone.Hand))
                .OrderByDescending(c => _strategy.CardPlayScore(core, me, c, ContainsHarmfulAtom, TotalCost))
                .ToList();
            foreach (var card in ordered)
            {
                int mode = ChooseMode(core, me, card);
                if (mode < 0) continue; // 抉择卡全模式不可发动 → 换下一张
                // 失败静默重试下一张（引擎契约：不付费、卡留手）
                var targets = ChooseTargets(core, me, card, mode);
                if (GameActions.PlayCard(core, me, card, targets, Zone.Hand, mode))
                {
                    await StageCastAsync(core, card, targets); // 出牌舞台：亮 1 秒（卡面+箭头）再结算
                    SettleStack(core); // cast 已上栈：双 Pass 排干（AI 无响应 → 立即结算付费）
                    return true;
                }
            }
            return false;
        }

        /// <summary>墓地出牌（经 IPlaySource 授权；预检后再调，避免 TryBeginUse 先烧配额）。
        /// 出完同样先舞台后排干。</summary>
        private async Cysharp.Threading.Tasks.UniTask<bool> TryGraveyardPlayAsync(GameCore core, Player me)
        {
            var graveyard = core.ZoneManager.GetCards(me, Zone.Graveyard) ?? new List<Card>();
            foreach (var card in graveyard)
            {
                if (!CanPlayCard(core, me, card, Zone.Graveyard)) continue;
                int mode = ChooseMode(core, me, card);
                if (mode < 0) continue;
                var targets = ChooseTargets(core, me, card, mode);
                if (GameActions.PlayCardFromGraveyard(core, me, card, targets, mode))
                {
                    await StageCastAsync(core, card, targets);
                    SettleStack(core);
                    return true;
                }
            }
            return false;
        }

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
            if (card is IHasCost hc && hc.Cost != null && !hc.Cost.IsZero
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
        /// </summary>
        private List<Entity> ChooseTargets(GameCore core, Player me, Card card, int modeIndex = 0)
        {
            var def = FirstDomainEffect(card, modeIndex);
            if (def == null) return null; // 无需选目标 → 引擎自动

            var ctx = BuildTargetCtx(core, me, card);
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
        /// None 模式（区域自结算类）跳过——AI 不为其选目标（2026-09-11）。</summary>
        private static EffectDefinition FirstDomainEffect(Card card, int modeIndex)
        {
            foreach (var def in GetEffectDefinitions(card))
            {
                if (def.IsActivatedEffect) continue; // 施放即结算的才会随出牌自动跑
                if (def.SelectionMode == SelectionMode.None) continue; // 区域自结算：不选目标
                var domain = DomainOfMode(def, modeIndex);
                if (domain != null && domain.Count > 0) return def;
            }
            return null;
        }

        /// <summary>def 在指定模式的组合域（抉择 per-mode 优先，回落主序列域）。</summary>
        private static List<int> DomainOfMode(EffectDefinition def, int modeIndex)
        {
            return def.ChoiceDomains != null && modeIndex >= 0 && modeIndex < def.ChoiceDomains.Length
                ? def.ChoiceDomains[modeIndex]
                : def.TargetDomain;
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
                    if (step.Kind == RuntimeStepKind.Choice && step.Choices != null)
                    {
                        foreach (var seq in step.Choices)
                        {
                            if (seq == null) continue;
                            foreach (var s in seq)
                                if (s != null && s.Kind == RuntimeStepKind.Atomic && s.Atomic != null
                                    && HarmfulAtoms.Contains(s.Atomic.Type)) return true;
                        }
                    }
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

        /// <summary>逐攻击开窗：每个可攻击单位按策略选目标宣言（通用=清场优先；速度0上栈）→
        /// 响应窗口（SettleResponseWindowAsync：对方人类=发动弹窗/AI=守卫启发/无头=自动双 Pass）
        /// → 结算 → 下一攻。攻击资格由引擎权威判定（横置/零攻/NoAttack）。</summary>
        private async Cysharp.Threading.Tasks.UniTask DoCombatAsync(GameCore core, Player me)
        {
            var opp = me.Opponent;
            if (opp == null) return;
            var battlefield = new List<Card>(core.ZoneManager.GetCards(me, Zone.Battlefield) ?? new List<Card>());
            foreach (var unit in battlefield)
            {
                if (core.IsGameOver) return;
                if (!core.CombatSystem.CanDeclareAttack(unit, me)) continue;
                var target = _strategy.PickAttackTarget(core, me, unit, opp); // 攻击决策经策略（通用=清场优先）
                if (target == null) continue;
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
