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
        public SummonEngine SummonEngine => _subSystems.Get<SummonEngine>();
        public DelayedEffectScheduler DelayedEffectScheduler => _subSystems.Get<DelayedEffectScheduler>();
        public ResourceLedger ResourceLedger => _subSystems.Get<ResourceLedger>();

        /// <summary>对局史计数服务（P2a）：三方共用查询（仪式差值化/卡条件 Custom/AI）</summary>
        public MatchStatsService MatchStats => _subSystems.Get<MatchStatsService>();

        #endregion

        private GameCore()
        {
            Initialize();
        }

        /// <summary>
        /// 初始化所有子系统并注册到注册表
        /// </summary>
        private void Initialize()
        {
            // 初始化玩家
            // 初始生命 30：使流失抵消触顶可达（14 费 × 2 命 = 28 ≤ 30），四类抵消上限 14+6+6+5=31 构成单局额外资源预算
            _player1 = new Player("Player 1", 30);
            _player2 = new Player("Player 2", 30);
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
            sbaEngine.RegisterChecker(new CharacteristicChangeChecker());
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

            // 初始化召唤引擎
            var summonEngine = new SummonEngine(this);
            _subSystems.Register(summonEngine);

            // 初始化延迟效果调度器（DelayedEffect handler が登録、回合/相位终了で解决）
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

            // 资源台账（P0c）：必须在 TurnStart/TurnEnd 订阅之后创建，
            // 保证开行时读到的回合数/地牌槽上限已是本回合新值、封行前已收到全部产出事件
            _subSystems.Register(new ResourceLedger(elementPool));

            // 对局史计数服务（P2a）：须先于 RitualSystem.EnsureRuntime（Reset 内）订阅——
            // 同一事件先过服务计数、后过仪式 tracker 差值判达标，顺序即正确性
            _subSystems.Register(new MatchStatsService());
        }

        /// <summary>
        /// 回合结束：结束本回合玩家的「直到回合结束」持续效果
        /// </summary>
        private void OnTurnEnded(TurnEndEvent e)
        {
            // 结束阶段：未横置地牌自动横置，产 1 灰色元素入 bank（先于台账封行）
            ElementPool.OnTurnEnd(e.TurnPlayer, ZoneManager);

            DurationTracker.OnTurnEnd(e.TurnPlayer, e.TurnNumber);
            TextChangeLayer.OnTurnEnd(e.TurnPlayer, e.TurnNumber);
            // 延迟效果解决含异步原子效果（await UI）→ 事件回调为 void，故 fire-and-forget
            DelayedEffectScheduler.OnTurnEnd(e.TurnPlayer).Forget();

            // 持续指示物消退（定案：正面/负面统一登记于 CounterRules）：
            // UntilEndOfTurn 类（冻结/突袭紊乱）在归属玩家回合结束清零——突袭的"不能以玩家为目标"限制随之解除
            Attribute.CounterRules.OnTurnEnd(e.TurnPlayer, ZoneManager);

            // 手牌上限：超出部分由玩家选弃（AI/超时自动弃先头）
            EnforceHandLimitAsync(e.TurnPlayer).Forget();
        }

        /// <summary>初始手牌总量（含仪式：仪式先占位，再抽牌补满至此数；超出的仪式留牌库正常抽）。</summary>
        private const int OpeningHandSize = 6;

        private async UniTask EnforceHandLimitAsync(Player player)
        {
            if (player == null) return;

            var hand = ZoneManager.GetCards(player, Zone.Hand);
            if (hand == null) return;

            // 手牌上限：规则扩展点（OCP）修改链（基值 7，如纳川仪典提升到 15）
            int over = hand.Count - RuleHooks.GetHandLimit(player);
            if (over <= 0) return;

            var chosen = await TargetSelectionService.RequestAsync(new TargetSelectionRequest
            {
                Candidates = hand.Cast<Entity>().ToList(),
                MinCount = over,
                MaxCount = over,
                Chooser = player,
                Title = "手牌上限",
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
        /// 回合开始（准备阶段）：重置栈优先权与每回合使用计数 → 重置元素池（地牌重置）→ 随从横置恢复（定案：地牌之后）→ 抽1张
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

            // 规则扩展点（OCP）：回合开始自动化拦截（如节奏轴仪式跳过准备阶段——
            // 抽牌、地牌槽（元素浓度上限）推进、横置重置、场上卡准备阶段结算全跳；
            // 引擎簿记（栈优先权/全局回合计数/每回合一次计数）不在跳过范围——那是时钟不是结算）。
            if (RuleHooks.ShouldSkipTurnStartAutomation(player))
            {
                PublishEvent(new StandbySkippedEvent { Player = player, TurnNumber = e.TurnNumber });
                return;
            }

            // 重置元素池：地牌槽曲线按全局回合数推进 + 回合玩家地牌解除横置（准备阶段）
            ElementPool.OnTurnStart(player, e.TurnNumber);

            // 随从重置（定案：在地牌重置之后跟着重置）——战斗状态/关键词维护/横置恢复
            foreach (var card in ZoneManager.GetCards(player, Zone.Battlefield))
            {
                // 攻击次数 / 警戒额度：回合开始重置
                card.AttacksThisTurn = 0;
                card._vigilanceUsedThisTurn = false;

                // 再生：回合开始自动回复 2 点生命（固定值，不消耗）
                if (card.IsAlive && card.HasKeyword(KeywordRules.Regeneration) && card.GetLife() < card.GetMaxLife())
                {
                    card.Heal(2);
                    PublishEvent(new Attribute.HealEvent { Target = card, Amount = 2 });
                }

                // 成长：回合开始 +1/+1 指示物（固定值）
                if (card.IsAlive && card.HasKeyword(KeywordRules.Growth))
                {
                    card.AddCounters("+1/+1", 1);
                    Attribute.Handlers.HandlerHelpers.ApplyCounterStat(card, "+1/+1", 1);
                    EventManager.Instance.Publish(new KeywordAppliedEvent
                    {
                        Target = card,
                        Keyword = KeywordRules.Growth,
                        Detail = "成长：+1/+1"
                    });
                }

                // 横置恢复（定案）：回合规则照常——冻结/紊乱等负面指示物不修改重置
                // （其持续与消退走 CounterRules 统一管理，回合结束清理）
                if (card.IsTapped())
                {
                    card.Untap();
                    PublishEvent(new UntapEvent { UntappedEntity = card });
                }
            }

            // 抽一张牌（回合抽 = 本回合首次抽牌，抽卡时点对触发可见）
            ZoneManagerExtensions.DrawCard(ZoneManager, player, firstDrawOfTurn: true);
        }

        #region 游戏生命周期

        /// <summary>
        /// 初始化游戏：加载卡组、洗牌、发起手、开始游戏
        /// </summary>
        /// <param name="deck1">玩家1的卡牌实例列表（牌库）</param>
        /// <param name="deck2">玩家2的卡牌实例列表（牌库）</param>
        public void InitGame(List<Card> deck1, List<Card> deck2)
        {
            if (deck1 == null || deck2 == null)
                throw new ArgumentNullException("卡组不能为null");

            // 重置游戏状态
            Reset();

            // 重置本局代价抵消计数（单局上限随对局生命周期）
            _player1.ResetOffsetUsage();
            _player2.ResetOffsetUsage();

            // 地牌槽曲线注入（固定 [1..9]：起始 1，回合开始 +1，最大 9；
            // 卡组涌现曲线 DeckCurveCompiler 保留为分析工具，不再作为局内执行依据）
            ElementPool.SetCurve(_player1, ResourceCurve.StandardLandCurve(), "InitGame:固定地牌槽曲线");
            ElementPool.SetCurve(_player2, ResourceCurve.StandardLandCurve(), "InitGame:固定地牌槽曲线");

            // 将卡牌加入牌库区域，设置控制者
            foreach (var card in deck1)
            {
                card.SetController(_player1);
                ZoneManager.GetZoneContainer(_player1).Add(card, Zone.Deck);
            }
            foreach (var card in deck2)
            {
                card.SetController(_player2);
                ZoneManager.GetZoneContainer(_player2).Add(card, Zone.Deck);
            }

            // 洗牌
            ZoneManagerExtensions.ShuffleDeck(ZoneManager, _player1);
            ZoneManagerExtensions.ShuffleDeck(ZoneManager, _player2);

            // 仪式占初始手牌位：仪式先占位（总量不超过初始手牌数），再抽牌补满；超出的仪式留牌库正常抽
            MoveRitualsToOpeningHand(_player1);
            MoveRitualsToOpeningHand(_player2);

            // 起手抽牌补满至初始手牌总量（仪式占位后剩余的空位）
            for (int i = ZoneManager.GetCards(_player1, Zone.Hand).Count; i < OpeningHandSize; i++)
            {
                ZoneManagerExtensions.DrawCard(ZoneManager, _player1);
            }
            for (int i = ZoneManager.GetCards(_player2, Zone.Hand).Count; i < OpeningHandSize; i++)
            {
                ZoneManagerExtensions.DrawCard(ZoneManager, _player2);
            }

            // 开始游戏
            StartGame();
        }

        /// <summary>
        /// 仪式卡占初始手牌位：将牌库中的仪式卡逐一移入手牌，直至初始手牌总量上限；超出的仪式留牌库正常抽。
        /// （规则定案：初始手牌 6 张含仪式——携带多张仪式时也挤占抽牌位，不以任何形式突破总量。）
        /// </summary>
        private void MoveRitualsToOpeningHand(Player player)
        {
            int slots = OpeningHandSize - ZoneManager.GetCards(player, Zone.Hand).Count;
            if (slots <= 0) return;

            var rituals = ZoneManager.GetCards(player, Zone.Deck)
                .Where(c => RitualSystem.IsRitual(c))
                .ToList();
            foreach (var ritual in rituals)
            {
                if (slots <= 0) break;
                ZoneManager.MoveCard(ritual, player, Zone.Deck, Zone.Hand);
                slots--;
            }
        }

        /// <summary>
        /// 从CardData列表初始化游戏
        /// </summary>
        public void InitGame(List<CardData> deck1Data, List<CardData> deck2Data, int copiesPerCard = 1)
        {
            var deck1 = CardLoader.BuildDeck(deck1Data, copiesPerCard);
            var deck2 = CardLoader.BuildDeck(deck2Data, copiesPerCard);
            InitGame(deck1, deck2);
        }

        /// <summary>
        /// 开始游戏
        /// </summary>
        public void StartGame()
        {
            if (!_stateManager.StartGame())
                return;

            PublishEvent(new GameStartEvent
            {
                FirstPlayer = _player1,
                SecondPlayer = _player2
            });

            TurnEngine.StartNewTurn(_player1);
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
        /// </summary>
        internal void CheckLifeGameOver()
        {
            if (IsGameOver) return;
            if (Player2 != null && Player2.Life <= 0)
                PublishGameOverOnce(Player1, GameOverReason.LifeZero);
            else if (Player1 != null && Player1.Life <= 0)
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
        }

        /// <summary>
        /// 结束游戏
        /// </summary>
        public void EndGame(Player winner, GameOverReason reason)
            => PublishGameOverOnce(winner, reason);

        /// <summary>
        /// 游戏主循环
        /// </summary>
        public void Update()
        {
            if (!_stateManager.CanPerformGameActions)
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
            // 2) 玩家状态回满——生命/疲劳/抵消计数恢复初始
            foreach (var player in new[] { _player1, _player2 })
            {
                var container = ZoneManager.GetZoneContainer(player);
                if (container == null) continue;
                foreach (Zone zone in Enum.GetValues(typeof(Zone)))
                    container.Clear(zone);

                player.Life = player.MaxHealth;
                player.ResetOffsetUsage();
                player.IsAI = false;
            }

            TurnEngine.Initialize(_player1);
            StackEngine.Initialize(_player1);

            LayerEngine.ClearAll();
            SBAEngine.ClearHistory();
            ReplacementEngine.ClearAll();
            TriggerEngine.ClearAll();
            ElementPool.Reset();
            ResourceLedger.ClearAll();
            if (MatchStats != null) MatchStats.ClearAll(); // 对局史计数跨局不残留（先清再挂仪式订阅）
            MatchLogService.EnsureStarted();               // 对局日志全局钩子（幂等；AnyPublished 收口）
            MatchLogService.ClearAll();                    // 战报缓冲跨局不残留
            ControlChangeLayer.ClearAll();
            TextChangeLayer.ClearAll();
            CopyEffectsEngine.ClearAll();
            DurationTracker.ClearAll();
            ProphecySystem.Reset(); // 待验证预言跨局不残留
            RitualSystem.Reset();   // 仪式任务与光环跨局不残留
            RitualSystem.EnsureRuntime();  // 仪式运行时订阅（任务计数+奖励驱动），开局即挂载（展示记录等不漏采）
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
        /// 统一发布路径（按运行时类型分发）：替代检查 → EventManager → Trigger/Layer。
        /// 泛型入口与 ElementPool 等子系统的事件路由（Action&lt;IGameEvent&gt;）共用，
        /// 路由层拿不到静态类型，必须以 e.GetType() 为键（PublishDynamic）分发。
        /// replacementsApplied=true 时跳过替代检查（调用方已在管线前消费过替代，如伤害管线），
        /// 只走总线 + Trigger/Layer——防替代二次套用。
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
                        TriggerEngine.OnEvent(finalEvent);
                        LayerEngine?.OnEvent(finalEvent);
                        return;
                    }
                }
            }

            EventManager.Instance.PublishDynamic(e);
            TriggerEngine.OnEvent(e);
            LayerEngine?.OnEvent(e);
        }

        #endregion
    }
}
