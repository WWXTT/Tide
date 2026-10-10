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
        /// <summary>初始生命 30（2026-09-14 抵消退役后无单局兑换预算概念——流失扣上限的总量自然受 30 封顶）；
        /// Reset 跨局复位同用此值（上限被 LifeUp/流失改动后回初始）。</summary>
        public const int InitialLife = 30;

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

            // 临时复制卡订阅已删（2026-10-10 微缩/放大退役）：回响走 EchoCopyHandler 结算期
            // 直调 TempCopyRules.CreateTemporaryCopy，无事件订阅；回合末移除仍由 OnTurnEnded 承担。

            // 资源台账（P0c）：必须在 TurnStart/TurnEnd 订阅之后创建，
            // 保证开行时读到的回合数/地牌槽上限已是本回合新值、封行前已收到全部产出事件
            _subSystems.Register(new ResourceLedger(elementPool));

            // 对局史计数服务（P2a）：卡条件/AI 的单一计数源
            _subSystems.Register(new MatchStatsService());

            // 角色亡语（2026-09-15 定案，判负效果化）：每个角色注册内置亡语
            // OnRoleDeath → 宣告对手获得胜利（宣判即终局）；硬判负 LifeZero 退为兜底
            //（亡语被全拦时 CheckLifeGameOver 收尾）。RegisterEffect 幂等（同源+同 Id）重跑安全；
            // TriggerLimitPerTurn 默认 -1（无限，不受触发上限闸门）；效果可再给角色追加/改写亡语。
            var roleDeathrattle = new EffectDefinition
            {
                Id = "ROLE_DEATHRATTLE",
                DisplayName = "角色亡语：对手获得胜利",
                TriggerTiming = TriggerTiming.OnRoleDeath,
                ActivationType = EffectActivationType.Mandatory, // 强制触发：纯强制批合成双 Pass 直接结算（无响应窗口，宣判即终局）；与自动桶同批时后入居栈顶先结算
                ElementCostPrepaid = true,                        // 内置效果无费用
                Effects = new System.Collections.Generic.List<AtomicEffectInstance>
                {
                    new AtomicEffectInstance { Type = AtomicEffectType.DeclareVictory }
                },
            };
            triggerEngine.RegisterEffect(roleDeathrattle, _player1, _player1);
            triggerEngine.RegisterEffect(roleDeathrattle, _player2, _player2);
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

            // 持续指示物消退（2026-09-16 统一档定案：限时指示物一律持续到**持有者**回合结束）：
            // 只结算回合方侧——自己回合末清自己的（冻结/毒素/易损/紊乱/限时属性层），
            // 对手的要等对手回合末；突袭的"不能以玩家为目标"限制随自己回合末解除
            Attribute.CounterRules.OnTurnEnd(e.TurnPlayer, ZoneManager);

            // 窥渊仪典（2026-10-04 时机改版：回合开始→回合结束）：随机展示+锁定必须排在
            // 指示物倒数之后——同回合末新挂的锁不被 ③ 块吞层。组合根显式调（不走事件订阅：
            // 订阅序相对本方法随局数漂移，先后无保证）。
            RuleAuraSystem.RevealAndLockAtTurnEnd(e.TurnPlayer);

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

        /// <summary>初始手牌总量。</summary>
        public const int OpeningHandSize = 6;

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

            // 英雄技能耐久回充（2026-10-10 耐久池定案）：技能=单主动结界的耐久池——
            // 发动 −1+元素费现付、归零休眠不销毁、回合开始 +1（封顶初始耐久，RechargeSkill）；
            // 旧「横置一回合一次闸门（Untap 重置）」随结界全区域耐久经济退役；引用丢失时 lazy 回填。
            HeroSkillSystem.RechargeSkill(this, player);

            // 规则扩展点（OCP）：回合开始自动化拦截（ITurnStartInterceptor 声明跳过准备阶段——
            // 抽牌、地牌槽（元素浓度上限）推进、横置重置、场上卡准备阶段结算全跳；
            // 引擎簿记（栈优先权/全局回合计数/每回合一次计数）不在跳过范围——那是时钟不是结算）。
            // 2026-10-02 D2：SkipTurn 原子的 "Standby" 档（相位级跳过，代码侧表达）同口接入。
            if (RuleHooks.ShouldSkipTurnStartAutomation(player)
                || (TurnEngine != null && TurnEngine.ConsumePendingStandbySkip(player)))
            {
                PublishEvent(new StandbySkippedEvent { Player = player, TurnNumber = e.TurnNumber });
                return;
            }

            // 重置元素池：地牌槽曲线按全局回合数推进 + 回合玩家地牌解除横置（准备阶段）
            ElementPool.OnTurnStart(player, e.TurnNumber);

            // 随从重置（定案：在地牌重置之后跟着重置）——战斗状态/关键词维护/横置恢复
            foreach (var card in ZoneManager.GetCards(player, Zone.Battlefield))
            {
                // 攻击台账清零（纯统计——横置即上限，唯一动作门槛是横置态本身）
                card.AttacksThisTurn = 0;

                // 再生（2026-09-13 定案）：回合开始恢复**全部**生命（原固定 +2）
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
                    PublishEvent(new UntapEvent { UntappedEntity = card });
                }
            }

            // 再生（角色侧 2026-10-08 引擎统一）：回合玩家角色持有再生——回合开始恢复全部生命，
            // 与卡侧同口径。再生表行已拉黑「不可作为连接光环」（太强+持续语义冲突，2026-10-09
            // 角色通道开放后仍不可经光环投角色）——本路径为物化授予等通道保留（休眠）；
            // 表上撤掉拉黑即经光环可达。
            int roleRegen = player.GetMaxLife() - player.Life;
            if (roleRegen > 0 && player.HasKeyword(KeywordRules.Regeneration))
            {
                player.Heal(roleRegen);
                PublishEvent(new Attribute.HealEvent { Target = player, Amount = roleRegen });
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
        /// <param name="lockDeckOrder">锁牌库顺序（教学固定局）：跳过双方洗牌，牌库顺序=传入列表顺序——
        /// 摸牌恒取牌库顶，故列表前 OpeningHandSize 张即起手、之后即逐回合摸牌序列。缺省 false 行为不变。</param>
        /// <param name="skipOpeningDraw">跳过起手抽牌（教学预设场面 2026-10-06）：手牌完全交由调用方注入，
        /// 引擎不补到 OpeningHandSize。缺省 false 行为不变。</param>
        /// <param name="deferStart">不自动 StartGame（教学预设场面 2026-10-06）：调用方在 StartGame 前
        /// 完成场面注入（TutorialScenarioSeeder）+ 回合起跳预置（TurnEngine.PrepareScenarioStart）。
        /// 缺省 false 行为不变。</param>
        /// <param name="p2LandCapBonus">P2 地牌槽加成（挑战模式 2026-10-06 难度 N 单值联动）：
        /// P2 曲线换 ChallengeLandCurve(bonus)=[1+bonus..9+bonus]，开局即有额外槽、封顶同步抬高；
        /// P1 保持标准曲线。缺省 0 双方对称，行为不变。</param>
        /// <param name="p2ExtraOpeningDraws">P2 起手额外抽牌数（挑战模式）：对称起手填完后追加，
        /// 手牌无上限截断（HAND_SIZE_LIMIT 无强制点，已核实）。缺省 0 行为不变。</param>
        /// <param name="skillCardId1">P1 英雄技能标记卡 ID（2026-10-07 卡牌化改版）：从牌库抽出
        /// 落 FieldZone；未标记/找不到/不合格（非结界或效果数≠1）= 本局无技能。缺省 null。</param>
        /// <param name="skillCardId2">P2 英雄技能标记卡 ID，同上。</param>
        public void InitGame(List<Card> deck1, List<Card> deck2, int? rngSeed = null, bool lockDeckOrder = false,
            bool skipOpeningDraw = false, bool deferStart = false,
            int p2LandCapBonus = 0, int p2ExtraOpeningDraws = 0,
            string skillCardId1 = null, string skillCardId2 = null)
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

            // 地牌槽曲线注入（固定 [1..9]：起始 1，回合开始 +1，最大 9；
            // 卡组涌现曲线 DeckCurveCompiler 保留为分析工具，不再作为局内执行依据）
            // 挑战模式（2026-10-06）：P2 按难度换上移曲线（开局即有额外槽），P1 恒标准曲线
            ElementPool.SetCurve(_player1, ResourceCurve.StandardLandCurve(), "InitGame:固定地牌槽曲线");
            ElementPool.SetCurve(_player2,
                p2LandCapBonus > 0 ? ResourceCurve.ChallengeLandCurve(p2LandCapBonus) : ResourceCurve.StandardLandCurve(),
                p2LandCapBonus > 0 ? $"InitGame:挑战曲线(+{p2LandCapBonus})" : "InitGame:固定地牌槽曲线");

            // 英雄技能（2026-10-07 卡牌化改版）：填牌库前先抽出构筑标记的结界
            //（牌库相应少一张；未标记/不合格=无技能——旧三色自动指派已随卡牌化退役）
            var skill1 = HeroSkillSystem.ExtractSkillCard(deck1, skillCardId1);
            var skill2 = HeroSkillSystem.ExtractSkillCard(deck2, skillCardId2);

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

            // 开始游戏（deferStart：教学预设场面注入器接管——seed 完成后调用方自行 StartGame）
            if (!deferStart)
                StartGame();
        }

        /// <summary>
        /// 从CardData列表初始化游戏
        /// </summary>
        public void InitGame(List<CardData> deck1Data, List<CardData> deck2Data, int copiesPerCard = 1,
            int? rngSeed = null, bool lockDeckOrder = false,
            bool skipOpeningDraw = false, bool deferStart = false,
            int p2LandCapBonus = 0, int p2ExtraOpeningDraws = 0,
            string skillCardId1 = null, string skillCardId2 = null)
        {
            var deck1 = CardLoader.BuildDeck(deck1Data, copiesPerCard);
            var deck2 = CardLoader.BuildDeck(deck2Data, copiesPerCard);
            InitGame(deck1, deck2, rngSeed, lockDeckOrder, skipOpeningDraw, deferStart,
                p2LandCapBonus, p2ExtraOpeningDraws, skillCardId1, skillCardId2);
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
            // 2) 玩家状态回满——生命/疲劳计数恢复初始
            // 3) 上限/指示物/关键词复位（2026-09-20 修复）：原 Life=Max 会把回血溢出（LifeUp）
            //    抬高、流失压低的上限原样带进新局——连跑多局开局出现 33/40 血即此；
            //    角色残留的指示物与关键词轨一并清空，神佑（构造态默认）随后重新注入
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
                EntityEffectExtensions.AddKeyword(player, DeathRules.DivineProtection, KeywordLane.Status);
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

            // 守护配对（2026-10-08 配对制改版）：入场弹选保护目标/离场断链——组合根登记（幂等，
            // 先退再订防多局重复订阅）+ 配对表跨局不残留（被守护者引用跨局失效）
            Attribute.GuardianRules.EnsureSubscribed();
            Attribute.GuardianRules.Reset();

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
