using System;
using System.Collections.Generic;
using System.Linq;
using CardCore.Attribute;
using Cysharp.Threading.Tasks;

namespace CardCore
{
    /// <summary>
    /// 游戏核心 — 轻量级门面（Facade）
    /// 负责初始化和组合各子系统，不处理规则细节
    /// 业务逻辑委托给 GameStateManager、GameLoopController 等专职管理器
    /// </summary>
    public class GameCore
    {
        private static GameCore _instance;
        public static GameCore Instance => _instance ??= new GameCore();

        /// <summary>丢弃单例（跨 Play 残留治理，2026-10-14）：工程关了域重载时本单例跨 Play 存活，
        /// 而构造期事件订阅（TurnStart/TurnEnd/PhaseEnd 三条核心自动化）挂在订阅时的 EventManager
        /// 总线实例上——若只 ResetInstance 事件总线而本单例残留，新对局的引擎事件将无人消费
        /// （回合推进但生物不解横置、不抽牌、AI 空转直结束）。由 Unity 侧引导（UIBootstrap）进 Play 时
        /// 与 EventManager.ResetInstance 成对调用；无头侧（TideServer）自管生命周期不调用。</summary>
        public static void ResetInstance() => _instance = null;

        // 子系统注册表
        private readonly SubSystemRegistry _subSystems = new SubSystemRegistry();

        // 核心管理器（职责分离后）
        private GameStateManager _stateManager;
        private GameLoopController _loopController;

        private Player _player1;
        private Player _player2;

        #region 公共属性 — 子系统访问

        public GameState State => _stateManager.CurrentState;
        public Player Player1 => _player1;
        public Player Player2 => _player2;
        public GameStateManager StateManager => _stateManager;
        public SubSystemRegistry SubSystems => _subSystems;

        public TurnEngine TurnEngine => _subSystems.Get<TurnEngine>();
        public StackEngine StackEngine => _subSystems.Get<StackEngine>();
        public LayerEngine LayerEngine => _subSystems.Get<LayerEngine>();
        public StateBasedActions SBAEngine => _subSystems.Get<StateBasedActions>();
        public ReplacementEngine ReplacementEngine => _subSystems.Get<ReplacementEngine>();
        public TriggerEngine TriggerEngine => _subSystems.Get<TriggerEngine>();
        public ZoneManager ZoneManager => _subSystems.Get<ZoneManager>();
        public ElementPoolSystem ElementPool => _subSystems.Get<ElementPoolSystem>();
        public ControlChangeLayer ControlChangeLayer => _subSystems.Get<ControlChangeLayer>();
        public TextChangeLayer TextChangeLayer => _subSystems.Get<TextChangeLayer>();
        public CopyEffectsEngine CopyEffectsEngine => _subSystems.Get<CopyEffectsEngine>();
        public ContinuousEffectDurationTracker DurationTracker => _subSystems.Get<ContinuousEffectDurationTracker>();
        public CombatSystem CombatSystem => _subSystems.Get<CombatSystem>();
        public DelayedEffectScheduler DelayedEffectScheduler => _subSystems.Get<DelayedEffectScheduler>();
        public ResourceLedger ResourceLedger => _subSystems.Get<ResourceLedger>();

        /// <summary>对局史计数服务（P2a）：卡条件 Custom / AI 共用查询</summary>
        public MatchStatsService MatchStats => _subSystems.Get<MatchStatsService>();

        #endregion

        private GameCore()
        {
            Initialize();
        }

        /// <summary>
        /// 初始化所有子系统并注册到注册表
        /// </summary>
        /// <summary>初始生命（2026-10-13 迁表：ValueSystemConfig GameRules.InitialLife；2026-09-14 抵消退役后
        /// 无单局兑换预算概念——流失扣上限的总量自然受初始值封顶）；Reset 跨局复位同用此值。</summary>
        public static int InitialLife
            => (int)ValueSystemConfigManager.Instance.GetOrCreateConfig().GameRulesConfig.InitialLife;

        private void Initialize()
        {
            // 初始化玩家
            _player1 = new Player("Player 1", InitialLife);
            _player2 = new Player("Player 2", InitialLife);
            _player1.Opponent = _player2;
            _player2.Opponent = _player1;

            // 初始化区域管理器
            var zoneManager = new ZoneManager();
            zoneManager.InitializePlayer(_player1);
            zoneManager.InitializePlayer(_player2);
            _subSystems.Register(zoneManager);

            // 初始化元素池
            var elementPool = new ElementPoolSystem();
            elementPool.InitializePlayer(_player1);
            elementPool.InitializePlayer(_player2);
            // 曲线事件（CurveShiftEvent）经 GameCore 统一路由，保证 Trigger/Layer 可见
            elementPool.AttachEventRouter(e => PublishEventRouted(e));
            _subSystems.Register(elementPool);

            // 初始化效果执行器和栈引擎
            var executor = new EffectExecutor(zoneManager, elementPool);
            var stackEngine = new StackEngine(executor);
            stackEngine.Initialize(_player1);
            _subSystems.Register(stackEngine);

            // 注册全局内置效果/代价处理器（幂等）+ 预热原子效果配置表，
            // 确保本核心自建的 executor 能在首次结算前找到处理器与配置
            BuiltinHandlerBootstrap.EnsureRegistered();
            _ = CardCore.Attribute.AtomicEffectTable.Count;

            // 初始化回合引擎
            var turnEngine = new TurnEngine();
            turnEngine.Initialize(_player1);
            // 接线：事件经 GameCore 统一路由 + 栈空守卫阶段推进/结束
            turnEngine.AttachRuntime(this, () => stackEngine.IsEmpty);
            _subSystems.Register(turnEngine);

            // 初始化层引擎
            var layerEngine = new LayerEngine();
            _subSystems.Register(layerEngine);

            // 初始化状态动作系统
            var sbaEngine = new StateBasedActions();
            sbaEngine.Initialize(this);
            sbaEngine.RegisterChecker(new ZeroLifeChecker());
            sbaEngine.RegisterChecker(new ZeroToughnessChecker());
            sbaEngine.RegisterChecker(new ZoneChangeChecker());
            // CharacteristicChangeChecker（±1/+1 成对湮灭）已删（2026-10-13 裁定：湮灭丢失效果来源——
            // 两类计数层允许混挂，来源归因随层保留）
            // TextChangeChecker 暂不注册：当前 Card 模型无文本字段可对比（见 SBACheckers.cs）。
            _subSystems.Register(sbaEngine);

            // 初始化替代引擎
            var replacementEngine = new ReplacementEngine();
            _subSystems.Register(replacementEngine);

            // 初始化触发引擎
            var triggerEngine = new TriggerEngine(stackEngine);
            // 注入区域系统以启用触发条件（intervening "if"）校验
            triggerEngine.AttachConditionContext(zoneManager);
            _subSystems.Register(triggerEngine);

            // 初始化控制变更层
            var controlChangeLayer = new ControlChangeLayer();
            controlChangeLayer.InitializePlayer(_player1);
            controlChangeLayer.InitializePlayer(_player2);
            _subSystems.Register(controlChangeLayer);

            // 初始化文本变更层
            var textChangeLayer = new TextChangeLayer();
            _subSystems.Register(textChangeLayer);

            // 初始化复制效果系统
            var copyEffectsEngine = new CopyEffectsEngine();
            _subSystems.Register(copyEffectsEngine);

            // 初始化持续效果时长追踪
            var durationTracker = new ContinuousEffectDurationTracker();
            _subSystems.Register(durationTracker);

            // 初始化战斗系统（接入层引擎，使战斗按计算后的当前力量结算）
            var combatSystem = new CombatSystem(zoneManager);
            combatSystem.AttachLayerEngine(layerEngine);
            _subSystems.Register(combatSystem);


            // 初始化延迟效果调度器（由 DelayedEffect handler 登记，回合/阶段结束时结算）
            var delayedEffectScheduler = new DelayedEffectScheduler();
            _subSystems.Register(delayedEffectScheduler);

            // 初始化状态管理器
            _stateManager = new GameStateManager();

            // 初始化循环控制器
            _loopController = new GameLoopController(
                turnEngine, stackEngine, triggerEngine,
                sbaEngine, layerEngine, combatSystem,
                durationTracker, controlChangeLayer, textChangeLayer);

            // 回合开始接线：重置栈优先权持有者 + 清零「每回合一次」使用计数
            // （TurnEngine.StartNewTurn 发布 TurnStartEvent，此处统一消费）
            EventManager.Instance.Subscribe<TurnStartEvent>(OnTurnStarted);

            // 回合/阶段结束接线：驱动持续效果时长追踪的失效
            // （UntilEndOfTurn / UntilLeaveBattlefield 为事件驱动失效，非挂钟到期）。
            // TurnEnd 订阅须先于资源台账：本核心结束阶段的自动产灰要先于台账封行，否则灰色产出被漏记
            EventManager.Instance.Subscribe<TurnEndEvent>(OnTurnEnded);
            EventManager.Instance.Subscribe<PhaseEndEvent>(OnPhaseEnded);

            // 临时复制卡订阅已删（2026-10-10 微缩/放大退役）：回响走 EchoCopyHandler 结算期
            // 直调 TempCopyRules.CreateTemporaryCopy，无事件订阅；回合末移除仍由 OnTurnEnded 承担。

            // 资源台账（P0c）：必须在 TurnStart/TurnEnd 订阅之后创建，
            // 保证开行时读到的回合数/地牌槽上限已是本回合新值、封行前已收到全部产出事件
            _subSystems.Register(new ResourceLedger(elementPool));

            // 对局史计数服务（P2a）：卡条件/AI 的单一计数源
            _subSystems.Register(new MatchStatsService());

            // 角色亡语注入（ROLE_DEATHRATTLE，2026-09-15）已删（2026-10-13）：
            // 生命归零检测由 SBA（ZeroLifeChecker→ExecuteZeroLife 宣判）+ CheckLifeGameOver 兜底
            // 两层覆盖，注入亡语属冗余层；效果侧宣判走 DeclareVictory 原子（表可达）。
        }

        /// <summary>
        /// 回合结束：结束本回合玩家的「直到回合结束」持续效果
        /// </summary>
        private void OnTurnEnded(TurnEndEvent e)
        {
            // 2026-10-11 现取现用定案：临时钱包清空（任一回合结束，效果授予元素即逝）；
            // 旧「未横置地牌自动横置产色+耗尽清理」已随指示物退役（先于台账封行）
            ElementPool.OnTurnEnd(e.TurnPlayer);

            DurationTracker.OnTurnEnd(e.TurnPlayer, e.TurnNumber);
            TextChangeLayer.OnTurnEnd(e.TurnPlayer, e.TurnNumber);
            // 延迟效果解决含异步原子效果（await UI）→ 事件回调为 void，故 fire-and-forget
            DelayedEffectScheduler.OnTurnEnd(e.TurnPlayer).Forget();

            // 持续指示物消退（2026-09-16 统一档定案：限时指示物一律持续到**持有者**回合结束）：
            // 只结算回合方侧——自己回合末清自己的（冻结/毒素/易损/紊乱/限时属性层），
            // 对手的要等对手回合末；突袭的"不能以玩家为目标"限制随自己回合末解除
            Attribute.CounterRules.OnTurnEnd(e.TurnPlayer, ZoneManager);

            // 延迟死亡（2026-10-29 阶段3b）：到点条目执行死亡（指示物倒数之后——毒素等回合末
            // 死亡同轮登记的条目ScheduledTurn=本回合，须等下个回合末；离开战场/已死的条目撤销）
            Attribute.DeathRules.OnTurnEndDelayedDeaths(e.TurnNumber);

            // 延时保护（2026-10-30 指示物化）：回合末−1 层走 CounterRules ③块通用倒数，无独立清口

            // 窥渊仪典（2026-10-04 时机改版：回合开始→回合结束）：随机展示+锁定必须排在
            // 指示物倒数之后——同回合末新挂的锁不被 ③ 块吞层。组合根显式调（不走事件订阅：
            // 订阅序相对本方法随局数漂移，先后无保证）。
            RuleAuraSystem.RevealAndLockAtTurnEnd(e.TurnPlayer);

            // 归土仪典（2026-10-30 改版：墓地视手牌→回合末复活）：回合结束时从自己墓地复活一张
            // 费用≤己方绿色地牌数的生物。排延迟死亡/窥渊之后——同回合末刚入墓的生物可被复活。
            RuleAuraSystem.ReviveAtTurnEnd(e.TurnPlayer);

            // Temp 轨关键词回合末到期（2026-09-13 定案：生物赋予的关键词固定持续 1 回合——
            // 魔法 Setting 轨与光环照旧不经此口）：每个回合末清双方战场 Temp 授予
            foreach (var pl in new[] { _player1, _player2 })
            {
                if (pl == null) continue;
                foreach (var c in ZoneManager.GetCards(pl, Zone.Battlefield).ToList())
                    Attribute.KeywordRules.ClearZoneKeywords(c);
            }

            // 临时卡（2026-10-10 指示物化）：PurgeTemporaryHandCards 退役——回合末口统一为
            // CounterRules.OnTurnEnd ②临时例程（全区域扫描，见上方调用）——不在战场的挂标卡
            // 从游戏中移除；「先移除临时卡、再看手牌上限弃牌」时序不变（例程已先行）

            // 手牌上限：超出部分由玩家选弃（AI/超时自动弃先头）
            EnforceHandLimitAsync(e.TurnPlayer).Forget();
        }

        /// <summary>初始手牌总量（2026-10-13 迁表：ValueSystemConfig GameRules.OpeningHandSize）。</summary>
        public static int OpeningHandSize
            => (int)ValueSystemConfigManager.Instance.GetOrCreateConfig().GameRulesConfig.OpeningHandSize;

        /// <summary>后手开局护甲补偿层数（2026-10-30 迁表：ValueSystemConfig GameRules.SecondPlayerArmorBonus；
        /// 后手角色开局 +N 层护甲指示物。注入点=开局流程层（BattleController/NetRoom——InitGame 内含
        /// Reset 清角色指示物，注入必须在 StartGame 之后），引擎本体不自动给）。</summary>
        public static int SecondPlayerArmorBonus
            => (int)ValueSystemConfigManager.Instance.GetOrCreateConfig().GameRulesConfig.SecondPlayerArmorBonus;

        /// <summary>装备入场接线委托（2026-09-24：Reset 重订阅用缓存实例——lambda 无法退订，
        /// 每局 new 会随 SubscribeCore 累积，装备入场效果同进程多局重复触发）。</summary>
        private readonly Action<CardPutToBattlefieldEvent> _equipEnterHandler =
            e => EquipRules.OnEnterBattlefield(e?.Card);

        private async UniTask EnforceHandLimitAsync(Player player)
        {
            if (player == null) return;

            var hand = ZoneManager.GetCards(player, Zone.Hand);
            if (hand == null) return;

            // 手牌上限：规则扩展点（OCP）修改链（基值 7）
            int limit = RuleHooks.GetHandLimit(player);
            int over = hand.Count - limit;
            if (over <= 0) return;

            var chosen = await TargetSelectionService.RequestAsync(new TargetSelectionRequest
            {
                Candidates = hand.Cast<Entity>().ToList(),
                MinCount = over,
                MaxCount = over,
                Chooser = player,
                Title = $"回合结束，手牌超出{limit}张，请选择丢弃",
                Hint = $"弃掉 {over} 张手牌",
                AllowCancel = false,
            });

            foreach (var entity in chosen)
            {
                if (entity is Card card)
                {
                    ZoneManager.MoveCard(card, player, Zone.Hand, Zone.Graveyard);
                    EventManager.Instance.Publish(new CardDiscardEvent
                    {
                        Player = player,
                        Card = card,
                        Source = null,
                    });
                }
            }
        }

        /// <summary>
        /// 阶段结束：结束「直到离场/阶段结束」类持续效果
        /// </summary>
        private void OnPhaseEnded(PhaseEndEvent e)
        {
            DurationTracker.OnPhaseEnd(e.Phase);
            TextChangeLayer.OnPhaseEnd(e.Phase);
            DelayedEffectScheduler.OnPhaseEnd().Forget();
        }

        /// <summary>
        /// 回合开始（2026-10-26 精准切分：**纯系统区**——只有时钟簿记；游戏自动化已迁
        /// RunStandbyAutomation 在准备阶段内执行）：栈优先权/每回合限流翻章/全局回合计数推进/技能解横置。
        /// </summary>
        private void OnTurnStarted(TurnStartEvent e)
        {
            StackEngine.OnTurnStart(e.TurnPlayer);
            StackEngine.GetExecutor().OnNewTurn(e.TurnNumber);

            // 持续效果时长追踪：推进全局回合计数（UntilNextTurn/ForTurns 到期戳的创建基准）
            DurationTracker.OnTurnStart(e.TurnNumber);
            TextChangeLayer.OnTurnStart(e.TurnNumber);

            var player = e.TurnPlayer;
            if (player == null)
                return;

            // 英雄技能回合重置（2026-10-15 改版：技能=法术、每回合一次=横置闸）：
            // 时钟口径——跳过准备阶段也执行（位置同旧 RechargeSkill），故留系统区；
            // 解横置结果记账，供准备阶段重置账目（StandbyResetEvent.SkillReset）携带。
            _skillResetAtTurnStart = HeroSkillSystem.ResetSkillForTurn(this, player);
        }

        /// <summary>系统区技能解横置记账（OnTurnStarted 写、RunStandbyAutomation 读——同一次 StartNewTurn 流内）。</summary>
        private bool _skillResetAtTurnStart;

        /// <summary>
        /// 准备阶段自动化（2026-10-26 回合精准切分：自 TurnStartEvent 订阅迁入准备阶段——由
        /// TurnEngine.StartNewTurn 在相位对象先行后、PhaseStartEvent(Standby) 发布前调用；合成
        /// TurnStartEvent 的验证器夹具须显式补调本口）。对外事件流序与旧实现一致。
        /// 跳过闸（ITurnStartInterceptor / SkipTurn「Standby」档）在内：命中发 StandbySkippedEvent 并整段跳过
        /// ——跳过的推进量（地牌槽/横置/抽牌/倒计时）由注入方自行预置（教学直入 PrepareScenarioStart 契约）。
        /// </summary>
        public void RunStandbyAutomation(Player player, int turnNumber)
        {
            if (player == null) return;

            if (RuleHooks.ShouldSkipTurnStartAutomation(player)
                || (TurnEngine != null && TurnEngine.ConsumePendingStandbySkip(player)))
            {
                PublishEvent(new StandbySkippedEvent { Player = player, TurnNumber = turnNumber });
                return;
            }

            // 重置元素池：地牌槽曲线按全局回合数推进 + 回合玩家地牌解除横置（准备阶段）
            int capBefore = ElementPool.GetLandCap(player);
            int untappedLands = ElementPool.OnTurnStart(player, turnNumber);
            int capAfter = ElementPool.GetLandCap(player);

            // 资源台账开行（原 TurnStartEvent 订阅随迁——「开行时上限已是本回合新值」契约保持：
            // 开行读数=个人回合数/上限/钱包期初/未横置地牌数，均不依赖其后的单位循环与抽牌）
            ResourceLedger?.OpenTurnRow(player);

            // 随从重置（定案：在地牌重置之后跟着重置）——战斗状态/关键词维护/横置恢复
            int untappedUnits = 0;
            foreach (var card in ZoneManager.GetCards(player, Zone.Battlefield))
            {
                // 攻击台账清零（纯统计——横置即上限，唯一动作门槛是横置态本身）
                card.AttacksThisTurn = 0;

                // 再生（2026-09-13 定案；2026-10-26 文案定稿「己方准备阶段恢复自身全部生命值」）
                if (card.IsAlive && card.HasKeyword(KeywordRules.Regeneration) && card.GetLife() < card.GetMaxLife())
                {
                    int regenAmount = card.GetMaxLife() - card.GetLife();
                    card.Heal(regenAmount);
                    PublishEvent(new Attribute.HealEvent { Target = card, Amount = regenAmount });
                }

                // 横置恢复（2026-09-13 定案）：冻结/沉睡期间均**无法重置**——
                // 谁被禁、被禁期间状态如何推进（沉睡扣层/苏醒重置）由 RuleHooks.IUntapBlockRule
                // 注册方自理（OCP：重置循环不点名具体指示物）。
                if (RuleHooks.BlocksUntap(card))
                {
                    RuleHooks.OnUntapBlocked(this, card);
                }
                else if (card.IsTapped())
                {
                    card.Untap();
                    PublishEvent(new UntapEvent { UntappedEntity = card, IsTurnStartReset = true });
                    untappedUnits++;
                }
            }

            // 再生角色侧通道已删（2026-10-13 裁定：再生=生物关键词，不可作为光环——
            // 表行 MountKinds 本就拉黑光环投递，物化授予休眠通道随之退役）；卡侧关键词行为保留。

            // 准备阶段重置账目（2026-10-26 战报可见性）：先于回合抽牌发布——〔准备阶段〕行与重置明细
            // 由此事件承载，抽牌行随之呈现为准备阶段内容。
            PublishEvent(new StandbyResetEvent
            {
                Player = player,
                UntappedLands = untappedLands,
                UntappedUnits = untappedUnits,
                SkillReset = _skillResetAtTurnStart,
                LandCap = capAfter,
                LandCapAdvanced = capAfter > capBefore,
            });

            // 抽一张牌（回合抽 = 本回合首次抽牌，抽卡时点对触发可见；CurseSystem 祝福/诅咒挂载随行）
            ZoneManagerExtensions.DrawCard(ZoneManager, player, firstDrawOfTurn: true);

            // 倒计时引擎（2026-10-26 随迁准备阶段——推翻 2026-10-20「钉回合开始」定案：倒计时归零发奖
            // 是可触发效果时点的游戏内容；递减→归零发奖→重挂+发射账翻章，保持在抽牌之后的现行序）
            BranchEngines.RunCountdownTick(player);
        }

        // ======================================== 回合起点快照（池子边界） ========================================

        /// <summary>最近一次回合起点快照（StartNewTurn 采集；重开本回合【未做】的边界基准）。</summary>
        public TurnStartSnapshot CurrentTurnStartSnapshot { get; private set; }

        /// <summary>快照环形历史容量（近 N 个回合起点——按序可追）。</summary>
        public const int TurnStartSnapshotHistoryCapacity = 8;

        private readonly Queue<TurnStartSnapshot> _turnStartSnapshotHistory = new Queue<TurnStartSnapshot>();

        /// <summary>采集回合起点全量快照（TurnEngine.StartNewTurn 边界调用；纯只读零事件）。</summary>
        public void CaptureTurnStartSnapshot()
        {
            CurrentTurnStartSnapshot = TurnStartSnapshot.Capture(this);
            if (CurrentTurnStartSnapshot == null) return;
            _turnStartSnapshotHistory.Enqueue(CurrentTurnStartSnapshot);
            while (_turnStartSnapshotHistory.Count > TurnStartSnapshotHistoryCapacity)
                _turnStartSnapshotHistory.Dequeue();
        }

        #region 游戏生命周期

        /// <summary>
        /// 初始化游戏：加载卡组、洗牌、发起手、开始游戏
        /// </summary>
        /// <param name="deck1">玩家1的卡牌实例列表（牌库）</param>
        /// <param name="deck2">玩家2的卡牌实例列表（牌库）</param>
        /// <param name="lockDeckOrder">锁牌库顺序（教学固定局）：跳过双方洗牌，牌库顺序=传入列表顺序——
        /// 摸牌恒取牌库顶，故列表前 OpeningHandSize 张即起手、之后即逐回合摸牌序列。缺省 false 行为不变。</param>
        /// <param name="skipOpeningDraw">跳过起手抽牌（教学预设场面 2026-10-06）：手牌完全交由调用方注入，
        /// 引擎不补到 OpeningHandSize。缺省 false 行为不变。</param>
        /// <param name="deferStart">不自动 StartGame（教学预设场面 2026-10-06）：调用方在 StartGame 前
        /// 完成场面注入（TutorialScenarioSeeder）+ 回合起跳预置（TurnEngine.PrepareScenarioStart）。
        /// 缺省 false 行为不变。</param>
        /// <param name="p2LandCapBonus">P2 地牌槽加成（挑战模式 2026-10-06 难度 N 单值联动）：
        /// P2 曲线换 ChallengeLandCurve(bonus)=[1+bonus..10+bonus]（全局回合索引，双方同步 +1 基础上抬），
        /// 开局即有额外槽、封顶同步抬高，无终钳（2026-10-15：MaxStackedLandCap 硬顶退役）；
        /// P1 保持标准曲线。缺省 0 双方对称，行为不变。</param>
        /// <param name="p2ExtraOpeningDraws">P2 起手额外抽牌数（挑战模式）：对称起手填完后追加，
        /// 手牌无上限截断（HAND_SIZE_LIMIT 无强制点，已核实）。缺省 0 行为不变。</param>
        /// <param name="skillCardId1">P1 英雄技能卡 ID（2026-10-15 改版：任选一张法术，卡池生成落
        /// FieldZone——不占卡组张数、不进牌库）；未标记/查无此卡/不合格（非法术或效果非 1~3 主动）
        /// = 本局无技能。缺省 null。</param>
        /// <param name="skillCardId2">P2 英雄技能卡 ID，同上。</param>
        /// <param name="startingPlayer">本局先手（对局准备流程硬币注入，2026-10-17）：null=Player1 历史现状；
        /// 仅在非 deferStart 分支经内部 StartGame 消费（教学预设场面走 PrepareScenarioStart 自带先手）。</param>
        public void InitGame(List<Card> deck1, List<Card> deck2, int? rngSeed = null, bool lockDeckOrder = false,
            bool skipOpeningDraw = false, bool deferStart = false,
            int p2LandCapBonus = 0, int p2ExtraOpeningDraws = 0,
            string skillCardId1 = null, string skillCardId2 = null,
            Player startingPlayer = null)
        {
            if (deck1 == null || deck2 == null)
                throw new ArgumentNullException("卡组不能为null");

            // 对局随机种子（2026-09-13 两个随机定案）：钉种子 → 同种子同随机序列
            // （验证器/网络对拍/回放用；缺省不重播，沿用服务内既有状态）
            // M3 对拍收口（2026-09-23）：引擎有两条独立随机流——GameRng（效果随机）+
            // ZoneContainer._rng（洗牌专用，static 独立 Reseed）——只重播前者时起手洗牌
            // 仍不确定，同种子对拍必炸；现双流同种子重播。
            if (rngSeed.HasValue)
            {
                GameRng.Reseed(rngSeed.Value);
                ZoneContainer.Reseed(rngSeed.Value);
            }

            // 重置游戏状态
            Reset();

            // 重置本局疲劳计数
            _player1.ResetFatigueCount();
            _player2.ResetFatigueCount();

            // 地牌槽曲线注入（固定 [1..10]：起始 1，每次回合开始双方同步 +1，自然封顶 10——2026-10-15 全局口径回归；
            // 卡组涌现曲线 DeckCurveCompiler 保留为分析工具，不再作为局内执行依据）
            // 挑战模式（2026-10-06）：P2 按难度换上移曲线（开局即有额外槽），P1 恒标准曲线
            ElementPool.SetCurve(_player1, ResourceCurve.StandardLandCurve(), "InitGame:固定地牌槽曲线");
            ElementPool.SetCurve(_player2,
                p2LandCapBonus > 0 ? ResourceCurve.ChallengeLandCurve(p2LandCapBonus) : ResourceCurve.StandardLandCurve(),
                p2LandCapBonus > 0 ? $"InitGame:挑战曲线(+{p2LandCapBonus})" : "InitGame:固定地牌槽曲线");

            // 英雄技能（2026-10-15 改版：技能=任选一张法术，卡池生成不占卡组张数/不进牌库；
            // 合成卡组兜底回落牌库抽取；未标记/查无此卡/不合格=无技能）
            var skill1 = HeroSkillSystem.CreateSkillCard(deck1, skillCardId1);
            var skill2 = HeroSkillSystem.CreateSkillCard(deck2, skillCardId2);

            // 将卡牌加入牌库区域，设置控制者（2026-10-22 额外区配套：tags 含「衍生物」的模板卡
            // 摘出落 Zone.Extra——照英雄技能卡摘出先例，不进牌库不会被抽到，纯识别/引用面）
            foreach (var card in deck1)
            {
                card.SetController(_player1);
                var data1 = card is CardWrapper wrapper1 ? wrapper1.GetData() : null;
                ZoneManager.GetZoneContainer(_player1)
                    .Add(card, data1 != null && data1.IsTokenTemplate ? Zone.Extra : Zone.Deck);
            }
            foreach (var card in deck2)
            {
                card.SetController(_player2);
                var data2 = card is CardWrapper wrapper2 ? wrapper2.GetData() : null;
                ZoneManager.GetZoneContainer(_player2)
                    .Add(card, data2 != null && data2.IsTokenTemplate ? Zone.Extra : Zone.Deck);
            }

            // 技能卡落 FieldZone（英雄技能栏）——落位不触发入场事件、不初始化耐久
            HeroSkillSystem.AssignSkill(this, _player1, skill1);
            HeroSkillSystem.AssignSkill(this, _player2, skill2);

            // 洗牌（教学固定局 lockDeckOrder：跳过洗牌——牌库顺序=传入列表顺序，摸牌恒取牌库顶，
            // 列表前 OpeningHandSize 张即起手、之后即逐回合摸牌序列）
            if (!lockDeckOrder)
            {
                ZoneManagerExtensions.ShuffleDeck(ZoneManager, _player1);
                ZoneManagerExtensions.ShuffleDeck(ZoneManager, _player2);
            }

            // 起手抽牌（教学预设场面 skipOpeningDraw：手牌完全交由调用方注入，不补满）
            if (!skipOpeningDraw)
            {
                for (int i = ZoneManager.GetCards(_player1, Zone.Hand).Count; i < OpeningHandSize; i++)
                {
                    ZoneManagerExtensions.DrawCard(ZoneManager, _player1);
                }
                for (int i = ZoneManager.GetCards(_player2, Zone.Hand).Count; i < OpeningHandSize; i++)
                {
                    ZoneManagerExtensions.DrawCard(ZoneManager, _player2);
                }
                // 挑战模式（2026-10-06）：P2 起手额外抽 p2ExtraOpeningDraws 张（难度 N=+N）
                for (int i = 0; i < p2ExtraOpeningDraws; i++)
                {
                    ZoneManagerExtensions.DrawCard(ZoneManager, _player2);
                }
            }

            // 开始游戏（deferStart：教学预设场面注入器接管——seed 完成后调用方自行 StartGame；
            // startingPlayer=对局准备流程的硬币先手，null=Player1 历史现状）
            if (!deferStart)
                StartGame(startingPlayer);
        }

        /// <summary>
        /// 从CardData列表初始化游戏
        /// tokenBindings1/2（P9b 2026-10-29）：卡组期衍生物槽绑定随装载盖到双方卡实例
        /// （CardLoader.BuildDeck→DeckTokenSupport.Apply）；null=无绑定（旧数据固定绑定/教程）。
        /// </summary>
        public void InitGame(List<CardData> deck1Data, List<CardData> deck2Data, int copiesPerCard = 1,
            int? rngSeed = null, bool lockDeckOrder = false,
            bool skipOpeningDraw = false, bool deferStart = false,
            int p2LandCapBonus = 0, int p2ExtraOpeningDraws = 0,
            string skillCardId1 = null, string skillCardId2 = null,
            Player startingPlayer = null,
            List<SynergyUI.DeckTokenBinding> tokenBindings1 = null,
            List<SynergyUI.DeckTokenBinding> tokenBindings2 = null)
        {
            var deck1 = CardLoader.BuildDeck(deck1Data, copiesPerCard, tokenBindings1);
            var deck2 = CardLoader.BuildDeck(deck2Data, copiesPerCard, tokenBindings2);
            InitGame(deck1, deck2, rngSeed, lockDeckOrder, skipOpeningDraw, deferStart,
                p2LandCapBonus, p2ExtraOpeningDraws, skillCardId1, skillCardId2, startingPlayer);
        }

        /// <summary>
        /// 开始游戏。startingPlayer=本局先手（null=Player1，历史现状）——对局准备流程的硬币
        /// 结果由此注入；GameStartEvent 的 FirstPlayer/SecondPlayer 与回合起跳同源使用它。
        /// </summary>
        public void StartGame(Player startingPlayer = null)
        {
            if (!_stateManager.StartGame())
                return;

            var first = startingPlayer == _player2 ? _player2 : _player1;
            PublishEvent(new GameStartEvent
            {
                FirstPlayer = first,
                SecondPlayer = first == _player1 ? _player2 : _player1
            });

            TurnEngine.StartNewTurn(first);
        }

        /// <summary>
        /// 暂停游戏
        /// </summary>
        public void Pause() => _stateManager.Pause();

        /// <summary>
        /// 恢复游戏
        /// </summary>
        public void Resume() => _stateManager.Resume();

        /// <summary>游戏是否已结束（Ended 状态）。AI / 驱动器 / UI 可据此短路。</summary>
        public bool IsGameOver => _stateManager.CurrentState == GameState.Ended;

        /// <summary>
        /// 生命判负检查（LifeZero）：在每次连锁（栈 / 战斗）结算完成后调用一次。
        /// PublishGameOverOnce 内含 Ended 守卫，全局只发一次，多点调用安全；
        /// 结束阶段 TurnEngine.CheckGameOver 保留为最终兜底。
        /// 角色亡语定案（2026-09-15）：已宣判（RoleDeathEvent 已发）且亡语在途（有待发触发式）
        /// 的玩家跳过——终局交给亡语宣告（默认=对手获得胜利）；亡语排干仍未终局时
        /// 下一轮回到这里兜底判负 LifeZero。
        /// </summary>
        internal void CheckLifeGameOver()
        {
            if (IsGameOver) return;
            bool deathrattlesInFlight = StackEngine.HasPendingEffects; // 亡语/触发式在途
            var sba = SBAEngine;
            if (Player2 != null && Player2.Life <= 0
                && (sba == null || !sba.IsProclaimed(Player2) || !deathrattlesInFlight))
                PublishGameOverOnce(Player1, GameOverReason.LifeZero);
            else if (Player1 != null && Player1.Life <= 0
                && (sba == null || !sba.IsProclaimed(Player1) || !deathrattlesInFlight))
                PublishGameOverOnce(Player2, GameOverReason.LifeZero);
        }

        /// <summary>
        /// 游戏结束统一发布口：全局只发一次（GameStateManager.Ended 状态守卫），
        /// TotalTurns 在此统一补全（疲劳 / SBA 路径原先漏赋值）。
        /// 所有 GameOverEvent 发布点（疲劳 / 回合引擎 / SBA / EndGame）必须经此，
        /// 禁止再直发 PublishEvent(new GameOverEvent…)。
        /// </summary>
        internal void PublishGameOverOnce(Player winner, GameOverReason reason)
        {
            if (winner == null) return;
            if (!_stateManager.EndGame())
                return;

            PublishEvent(new GameOverEvent
            {
                Winner = winner,
                Loser = winner.Opponent,
                Reason = reason,
                TotalTurns = TurnEngine.TurnNumber
            });
            RuleHooks.OnTutorialGameOver?.Invoke(winner); // 教学探针（2026-10-16 victory 步完成信号；非教学局为 null 直过）
        }

        /// <summary>
        /// 结束游戏
        /// </summary>
        public void EndGame(Player winner, GameOverReason reason)
            => PublishGameOverOnce(winner, reason);

        /// <summary>表现层演出暂停闸（2026-10-16 AI 出牌舞台；2026-10-19 改引用计数）：持有数>0=
        /// Update 本帧直接返回——帧泵的自动双 Pass（GameLoopController.ProcessPriority）不得在
        /// 演出期抢先结算栈。多持有方（SimpleAI 出牌舞台/逆结算步进回放导演）各自 Acquire/Release，
        /// 互不清对方的闸（旧 bool 口径：AI finally 一刀清零会误放回放导演的闸）。只延迟泵的调用
        /// 时机，不改任何响应/结算决策逻辑；无头/AI 互打（无 PacingWait/无 UI 导演）永不置位。</summary>
        public int PresentationHoldCount { get; private set; }

        public void AcquirePresentationHold() => PresentationHoldCount++;

        public void ReleasePresentationHold() => PresentationHoldCount = System.Math.Max(0, PresentationHoldCount - 1);

        /// <summary>
        /// 游戏主循环
        /// </summary>
        public void Update()
        {
            if (!_stateManager.CanPerformGameActions || PresentationHoldCount > 0)
                return;

            _loopController.Update();
        }

        /// <summary>
        /// 结算栈（异步：原子效果可能 await UI）。
        /// </summary>
        public UniTask ResolveStack()
        {
            return _loopController.ResolveStack();
        }

        #endregion

        #region 重置

        /// <summary>
        /// 重置游戏
        /// </summary>
        public void Reset()
        {
            _stateManager.Reset();

            // 跨局不残留（曾缺失，AI 对战/验证器同域连开多局时暴露）：
            // 1) 清空双方全部区域容器——上一局的卡牌不得带入新局
            // 2) 玩家状态回满——生命/疲劳计数恢复初始
            // 3) 上限/指示物/关键词复位（2026-09-20 修复）：原 Life=Max 会把回血溢出（LifeUp）
            //    抬高、流失压低的上限原样带进新局——连跑多局开局出现 33/40 血即此；
            //    角色残留的指示物与关键词轨一并清空
            foreach (var player in new[] { _player1, _player2 })
            {
                var container = ZoneManager.GetZoneContainer(player);
                if (container == null) continue;
                foreach (Zone zone in Enum.GetValues(typeof(Zone)))
                    container.Clear(zone);

                player.ResetVitals(InitialLife);
                player._counters.Clear();
                player._counterSources.Clear();
                player._keywords.Clear();
                player._keywordGrants.Clear();
                player.ResetFatigueCount();
                player.IsAI = false;

                // 英雄技能跨局不残留（2026-10-07 卡牌化）：技能卡已随上方区域 Clear 消失，
                // 只复位运行时引用（旧 HEROSKILL_ 前缀清理随三色硬编码技能退役删除）
                player.HeroSkillCard = null;
            }

            TurnEngine.Initialize(_player1);
            StackEngine.Initialize(_player1);
            // 效果执行器使用跟踪器跨局回收（2026-10-03 修复）：EffectExecutor.Reset 此前无调用方，
            // 限流计数只靠 OnNewTurn「回合号变化」兜底——新局回合号从 1 重来会撞上上一局同号计数，
            // 同名效果（同进程多局夹具/无头驱动）在新局首次触发即被误拦
            StackEngine.GetExecutor()?.Reset();

            LayerEngine.ClearAll();
            SBAEngine.ClearHistory();
            ReplacementEngine.ClearAll();
            TriggerEngine.ClearAll();
            ElementPool.Reset();
            ResourceLedger.ClearAll();
            if (MatchStats != null) MatchStats.ClearAll(); // 对局史计数跨局不残留
            MatchLogService.EnsureStarted();               // 对局日志全局钩子（幂等；AnyPublished 收口）
            MatchLogService.ClearAll();                    // 战报缓冲跨局不残留
            Network.NetEventProjector.EnsureStarted();     // 网络事件流投影（M1：与日志同款 AnyPublished 收口）
            Network.NetEventProjector.ClearAll();          // 事件缓冲跨局不残留
            ControlChangeLayer.ClearAll();
            TextChangeLayer.ClearAll();
            CopyEffectsEngine.ClearAll();
            DurationTracker.ClearAll();
            ProphecySystem.Reset(); // 待验证预言跨局不残留

            // 回合开始重置拦截（2026-09-13 定案：冻结/沉睡无法重置）——组合根登记（幂等）
            RuleHooks.RegisterUntapBlockRule(SleepFreezeUntapBlockRule.Instance);

            // 动态分支引擎（2026-09-13 分支体系正规化：倒计时/运势/拼点）——组合根登记（幂等）
            BranchEngines.EnsureRegistered();

            // 诅咒系统（2026-10-02 信息轴定案：抽到被诅咒卡时自动执行载荷分支并消层）
            // ——组合根登记（幂等）+ 载荷注册表跨局不残留（CurseSystem 在 CardCore 根命名空间，同 BranchEngines）
            CurseSystem.EnsureRegistered();
            CurseSystem.Reset();

            // 规则光环系统（2026-10-03 规则轴定案：ModifyGameRule 原子投放的全局唯一光环，
            // 对双方生效、载体离场失效）——组合根登记（幂等）+ 槽位/状态跨局不残留
            // + 状态无关替代件重挂（时序在 ReplacementEngine.ClearAll 之后）
            RuleAuraSystem.EnsureRegistered();
            RuleAuraSystem.Reset();
            RuleAuraSystem.OnGameReset();

            // 延时保护（2026-10-30 指示物化）：无独立局重置口——指示物随 CounterRules 清；延迟死亡待死条目同批清
            Attribute.DeathRules.ResetDelayedDeaths();

            // 守护配对（2026-10-08 配对制改版）：入场弹选保护目标/离场断链——组合根登记（幂等，
            // 先退再订防多局重复订阅）+ 配对表跨局不残留（被守护者引用跨局失效）
            Attribute.GuardianRules.EnsureSubscribed();
            Attribute.GuardianRules.Reset();

            // 舍身配对接线已删（2026-10-30 战斗法师改版）：无状态关键词——散射内联
            // KeywordRules.ApplyDamage 咽喉，MartyrdomRules 配对表/订阅/跨局清档整套退役。

            // 装备入场耐久初始化（2026-09-13 起；2026-10-07 武器反伤扩展口随武器系统退役删除，
            // 角色参战改走 HeroAttackCounter 弹药原子）——组合根（幂等）
            // 订阅缓存委托（2026-09-24 泄漏修复）：lambda 每局 new 一个实例无法退订，
            // SubscribeCore 不去重 → 同进程连开多局时装备入场效果重复触发（先退再订，幂等）
            EventManager.Instance.Unsubscribe(_equipEnterHandler);
            EventManager.Instance.Subscribe(_equipEnterHandler);

        }

        #endregion

        #region 查询

        public Player GetCurrentTurnPlayer() => TurnEngine.TurnPlayer;

        public Player GetOpponent(Player player) => player.Opponent;

        #endregion

        #region 事件发布

        /// <summary>
        /// 发布事件到事件总线并通知子系统
        /// internal：供 GameActions 等同程序集逻辑统一走此路径，
        /// 保证 TriggerEngine/LayerEngine 都能观察到（修复事件绕过子系统的双引擎问题）
        /// </summary>
        internal void PublishEvent<T>(T e) where T : IGameEvent => PublishEventRouted(e);

        /// <summary>
        /// 统一发布路径（按运行时类型分发）：替代检查 → EventManager → BranchEngines → Trigger/Layer。
        /// 泛型入口与 ElementPool 等子系统的事件路由（Action&lt;IGameEvent&gt;）共用，
        /// 路由层拿不到静态类型，必须以 e.GetType() 为键（PublishDynamic）分发。
        /// replacementsApplied=true 时跳过替代检查（调用方已在管线前消费过替代，如伤害管线），
        /// 只走总线 + Trigger/Layer——防替代二次套用。
        /// BranchEngines.OnRoutedEvent（2026-10-16 引擎解绑）置于总线之后、Trigger 之前：
        /// 倒计时递减/置待发在总线订阅内同步完成，本钩子随即按声明时机发射（声明=回合开始时
        /// 同一事件内发奖，与旧「归零发奖」可观察行为一致；引擎发射先于普通触发上栈，沿旧时序）。
        /// </summary>
        internal void PublishEventRouted(IGameEvent e, bool replacementsApplied = false)
        {
            // 替代效果（Replacement）在事件「发生前」拦截：若存在可替代当前事件类型的效果，
            // 用替代后的最终事件继续派发。替代可能产出不同的具体类型（如 CardDestroyEvent → CardBanishEvent），
            // 此时必须按运行时类型分发（PublishDynamic），否则以静态类型 T 为键会漏掉真实订阅者。
            if (!replacementsApplied)
            {
                var repl = ReplacementEngine;
                if (repl != null && repl.HasReplacementEffect(e.GetType()))
                {
                    var ctx = repl.CheckReplacements(e);
                    var finalEvent = ctx.GetFinalEvent();
                    if (!ReferenceEquals(finalEvent, e))
                    {
                        EventManager.Instance.PublishDynamic(finalEvent);
                        BranchEngines.OnRoutedEvent(finalEvent);
                        TriggerEngine.OnEvent(finalEvent);
                        LayerEngine?.OnEvent(finalEvent);
                        return;
                    }
                }
            }

            EventManager.Instance.PublishDynamic(e);
            BranchEngines.OnRoutedEvent(e);
            TriggerEngine.OnEvent(e);
            LayerEngine?.OnEvent(e);
        }

        #endregion
    }
}
