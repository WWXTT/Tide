using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCore
{
    /// <summary>
    /// 回合阶段信息
    /// </summary>
    public class PhaseInfo
    {
        public PhaseType Phase { get; set; }
        public Player ActivePlayer { get; set; }
        public DateTime StartTime { get; set; }
        public PhaseState State { get; set; } = PhaseState.Active;
    }

    /// <summary>
    /// 阶段状态
    /// </summary>
    public enum PhaseState
    {
        /// <summary>
        /// 阶段未开始
        /// </summary>
        NotStarted,

        /// <summary>
        /// 阶段进行中
        /// </summary>
        Active,

        /// <summary>
        /// 阶段结束
        /// </summary>
        Ending,

        /// <summary>
        /// 阶段已完成
        /// </summary>
        Completed
    }

    /// <summary>
    /// 跳过档位（2026-10-02 D2 定案）：Whole=整回合作废顺延对手；Standby=只跳准备阶段自动化
    /// （抽牌/横置重置/地牌槽推进，与 ITurnStartInterceptor 同口径）；Main=只跳主要阶段
    /// （准备阶段后直进结束阶段，无出牌/攻击窗口）；End=只跳结束阶段（2026-10-29 阶段3a：
    /// 进入结束阶段即直接折返下一回合——不发 TurnEndEvent，该阶段系统行为一并跳过）。
    /// 相位级跳过在代码侧表达（SkipTurn 原子 str 参数），不占原子表行——2026-10-29 阶段3a
    /// 起三相位行入表（str 由合成器按行 DisplayName 写入）。
    /// </summary>
    public enum TurnSkipMode
    {
        Whole,
        Standby,
        Main,
        End
    }

    /// <summary>
    /// 回合引擎
    /// 负责阶段状态机、优先权窗口生成、主动玩家轮换、阶段开始/结束触发
    /// </summary>
    public class TurnEngine
    {
        private PhaseInfo _currentPhase;
        private Player _turnPlayer;
        private int _turnNumber = 0;

        // 运行时接线（由 GameCore 注入）：事件路由 + 栈空查询
        private GameCore _gameCore;
        private Func<bool> _isStackEmpty;

        // 额外回合 / 跳过回合（由原子效果 TakeExtraTurn / SkipTurn 驱动）
        private int _extraTurnsForCurrent = 0;
        private readonly HashSet<Player> _skipNextTurn = new HashSet<Player>();
        // 相位级跳过（2026-10-02 D2）：待定标记一次性消费
        private readonly HashSet<Player> _skipNextStandby = new HashSet<Player>();
        private readonly HashSet<Player> _skipNextMain = new HashSet<Player>();
        // 跳过结束阶段档（2026-10-29 阶段3a）：OnEndPhaseStarted 消费——进入即折返下一回合
        private readonly HashSet<Player> _skipNextEnd = new HashSet<Player>();
        private List<PhaseType> _phaseOrder = new List<PhaseType>
        {
            PhaseType.Standby,
            PhaseType.Main,
            PhaseType.End
        };

        /// <summary>
        /// 当前阶段
        /// </summary>
        public PhaseInfo CurrentPhase => _currentPhase;

        /// <summary>
        /// 当前回合玩家
        /// </summary>
        public Player TurnPlayer => _turnPlayer;

        /// <summary>
        /// 回合数
        /// </summary>
        public int TurnNumber => _turnNumber;

        /// <summary>
        /// 初始化回合引擎
        /// </summary>
        public void Initialize(Player startingPlayer)
        {
            _turnPlayer = startingPlayer;
            // 从 0 起：StartNewTurn 先自增再发事件 → 首个 TurnStartEvent 携带 1（先手首回合=1，
            // 与地牌槽曲线「先手首回合上限 1」对齐；曾误置 1 导致全局回合数整体 off-by-one）
            _turnNumber = 0;
            _currentPhase = null;
        }

        /// <summary>
        /// 注入运行时依赖：通过 GameCore 统一发布事件（使 Trigger/Layer 引擎可观察到回合/阶段事件），
        /// 并提供栈空查询以守卫阶段推进/结束。
        /// </summary>
        public void AttachRuntime(GameCore gameCore, Func<bool> isStackEmpty)
        {
            _gameCore = gameCore;
            _isStackEmpty = isStackEmpty;
        }

        /// <summary>
        /// 开始新回合（2026-10-26 回合精准切分定案）：
        /// ① 置位回合玩家/回合号
        /// ② 快照：GameCore 采集回合起点全量快照（池子边界——跳过阶段装配/新回合/重开本回合【未做】的基准）
        /// ③ 发布 TurnStartEvent（**纯系统区**：栈优先权重置/每回合限流翻章/全局回合计数推进/
        ///    英雄技能解横置等时钟簿记——可能触发效果时点的内容一律不在此）
        /// ④ 进入准备阶段：相位对象先行（自动化在其内执行，CurrentPhase=Standby），PhaseStartEvent 后发
        /// ⑤ GameCore.RunStandbyAutomation（跳过闸→地牌/单位重置→重置账目→抽牌→倒计时引擎）
        /// ⑥ 发布 PhaseStartEvent(Standby)——#33 准备阶段时点锚：保持在自动化+抽牌**之后**，
        ///    现有准备阶段时点卡（含运势引擎路由）零语义翻转；战报〔准备阶段〕行由 StandbyResetEvent 承载
        /// ⑦ AdvanceFromStandby（准备→主自动推进保留 2026-09-24 口径；主→结束仅 IntentEndTurn 驱动）
        /// 对外事件流序与旧实现完全一致（TurnStartEvent → 自动化事件流 → PhaseStart(Standby) → Main）。
        /// </summary>
        public void StartNewTurn(Player player)
        {
            _turnPlayer = player;
            _turnNumber++;

            // ① 快照（池子边界）：纯只读；未接线 GameCore（独立测试）跳过
            _gameCore?.CaptureTurnStartSnapshot();

            // ② 系统区：发布回合开始事件（时钟簿记订阅者在此同步跑完）
            PublishEvent(new TurnStartEvent
            {
                TurnPlayer = player,
                TurnNumber = _turnNumber
            });

            // ③④ 准备阶段：相位对象先行，自动化在其内执行（#33 锚=自动化后的事件发布，见类注）
            EnterPhase(PhaseType.Standby, announce: false);
            if (_gameCore != null)
                _gameCore.RunStandbyAutomation(player, _turnNumber);
            AnnounceCurrentPhase();

            // 2026-09-24 回合推进口径（保留）：准备阶段纯自动、完成即推进主阶段——
            // 原"停等玩家 IntentSkipStandby"口径废除（跳过准备阶段改走跳过闸，不占相位窗口）；
            // 主要→结束仅由 IntentEndTurn（未来加操作超时）驱动，驱动器不得自由滑相位。
            AdvanceFromStandby();
        }

        /// <summary>回合快照导出（internal：TurnStartSnapshot 采集用）——额外回合计数与四组跳过标记按座位。</summary>
        internal (int extraTurns, int[] whole, int[] standby, int[] main, int[] end) CaptureTurnState(GameCore core)
        {
            int[] SeatsOf(System.Collections.Generic.HashSet<Player> set)
            {
                var seats = new System.Collections.Generic.List<int>(set.Count);
                foreach (var p in set)
                {
                    if (ReferenceEquals(p, core?.Player1)) seats.Add(0);
                    else if (ReferenceEquals(p, core?.Player2)) seats.Add(1);
                }
                return seats.ToArray();
            }
            return (_extraTurnsForCurrent, SeatsOf(_skipNextTurn), SeatsOf(_skipNextStandby),
                SeatsOf(_skipNextMain), SeatsOf(_skipNextEnd));
        }

        /// <summary>
        /// 开始指定阶段（标准口径：进入即发布 PhaseStartEvent——Main/End 及普通流沿用）
        /// </summary>
        public void StartPhase(PhaseType phaseType) => EnterPhase(phaseType, announce: true);

        /// <summary>
        /// 进入阶段：相位对象先行建立（CurrentPhase 即时生效）；announce=false 时不发 PhaseStartEvent——
        /// 2026-10-26 精准切分口径：准备阶段自动化在相位内、事件后发（AnnounceCurrentPhase 补发），
        /// #33 时点锚保持在自动化之后。事件与 OnPhaseStarted 钩子的发布序与旧 StartPhase 一致。
        /// </summary>
        private void EnterPhase(PhaseType phaseType, bool announce)
        {
            _currentPhase = new PhaseInfo
            {
                Phase = phaseType,
                ActivePlayer = _turnPlayer,
                StartTime = DateTime.Now,
                State = PhaseState.Active
            };
            if (announce)
                AnnounceCurrentPhase();
        }

        /// <summary>发布当前阶段的开始事件 + 阶段开始钩子（EnterPhase(announce:false) 的补发口）。</summary>
        private void AnnounceCurrentPhase()
        {
            // 触发阶段开始事件
            PublishEvent(new PhaseStartEvent
            {
                Phase = _currentPhase.Phase,
                ActivePlayer = _turnPlayer
            });

            // 根据阶段类型执行特定逻辑
            OnPhaseStarted(_currentPhase.Phase);
        }

        /// <summary>
        /// 阶段开始时的处理
        /// </summary>
        private void OnPhaseStarted(PhaseType phaseType)
        {
            switch (phaseType)
            {
                case PhaseType.Standby:
                    OnStandbyPhaseStarted();
                    break;
                case PhaseType.Main:
                    OnMainPhaseStarted();
                    break;
                case PhaseType.End:
                    OnEndPhaseStarted();
                    break;
            }
        }

        /// <summary>
        /// 准备阶段：纯自动阶段，无玩家操作。
        /// 2026-10-26 精准切分：地牌/单位重置、重置账目、抽 1 张、倒计时引擎由
        /// GameCore.RunStandbyAutomation 在相位对象先行后执行（StartNewTurn ④⑤），PhaseStartEvent
        /// 在其完成后发布（#33 锚）。
        /// 推进：StartNewTurn 末尾直调 AdvanceFromStandby（2026-09-24 口径：准备→主自动推进）；
        /// GameActions.SkipElementPool 走同一条入口，现仅为兼容保留（此时相位已不在 Standby，守卫直接返回）。
        /// </summary>
        private void OnStandbyPhaseStarted()
        {
        }

        /// <summary>
        /// 准备阶段完成，进入主阶段
        /// 现行驱动：StartNewTurn 末尾直调；GameActions.SkipElementPool 为兼容入口
        /// （地牌放置已移至主阶段，不再驱动阶段推进）
        /// </summary>
        public void AdvanceFromStandby()
        {
            if (_currentPhase?.Phase != PhaseType.Standby)
                return;
            EndCurrentPhase();
            // 跳过主要阶段（2026-10-02 D2）：待定标记一次性消费——准备阶段后直进结束阶段，
            // 本回合无出牌/攻击窗口（阶段事件照发，玩家侧表现为主阶段被跳过）。
            if (_turnPlayer != null && _skipNextMain.Remove(_turnPlayer))
            {
                StartPhase(PhaseType.End);
                return;
            }
            GoToNextPhase();
        }

        /// <summary>
        /// 主阶段：玩家可出牌、攻击、发动效果
        /// </summary>
        private void OnMainPhaseStarted()
        {
            // 主阶段等待玩家操作，不做自动推进
        }

        /// <summary>
        /// 结束阶段：SBA检查 → 手牌上限 → 清理临时效果 → 切换玩家
        /// 跳过结束阶段档（2026-10-29 阶段3a）：待定标记一次性消费——进入即直接 GoToNextPhase
        /// 折返下一回合（End→Standby 折返的回合轮换/额外回合/整回合跳过照常结算），
        /// **不发布 TurnEndEvent**（指示物倒数、弃牌上限、结束触发、终局检查等该阶段系统行为一并跳过；
        /// PhaseStartEvent(End) 已由 EnterPhase 照发——表现层可见阶段闪过）。
        /// </summary>
        private void OnEndPhaseStarted()
        {
            if (_turnPlayer != null && _skipNextEnd.Remove(_turnPlayer))
            {
                GoToNextPhase();
                return;
            }

            // 由 GameCore 调用 SBA 检查和清理
            // 这里只发布事件和推进

            PublishEvent(new TurnEndEvent
            {
                TurnPlayer = _turnPlayer,
                TurnNumber = _turnNumber
            });

            CheckGameOver();
            // 主动玩家轮换由 GoToNextPhase 在 End→Standby 折返时统一处理（StartNewTurn(_turnPlayer.Opponent)）。
            // 不在此处预先切换 _turnPlayer，否则会与折返时的 .Opponent 叠加导致回合不交替，
            // 且会污染随后 EndCurrentPhase 发布的 PhaseEndEvent.ActivePlayer。
        }

        /// <summary>
        /// 玩家主动结束回合。
        /// 返回是否真实结束（2026-10-20 教学双推进修复）：阶段非 Main/非 Active/栈未空时静默拒绝
        /// 并返回 false——调用方（GameActions.EndTurn）据此**不发教学探针**：旧口径拒绝时探针照发，
        /// 同帧双击/回合翻转前重入会让 OnTutorialTurnEnded 连发两次，end 步一击双进
        ///（实测段七 end#1+end#2 连推→换场与考验武装提前→段七 AI 回合必杀直伤提前落地）。
        /// </summary>
        public bool EndTurn()
        {
            if (_currentPhase?.Phase != PhaseType.Main)
                return false;
            if (_currentPhase?.State != PhaseState.Active)
                return false;
            // 栈未结算完毕时不能结束回合
            if (_isStackEmpty != null && !_isStackEmpty())
                return false;

            EndCurrentPhase();
            GoToNextPhase();
            return true;
        }

        /// <summary>
        /// 检查是否可以进入下一阶段
        /// </summary>
        public void CheckPhaseTransition()
        {
            // 只有阶段激活且没有优先权问题时才能推进
            if (_currentPhase?.State != PhaseState.Active)
                return;

            // 栈不为空时不能进入下一阶段
            if (_isStackEmpty != null && !_isStackEmpty())
                return;

            EndCurrentPhase();
            GoToNextPhase();
        }

        /// <summary>
        /// 结束当前阶段
        /// </summary>
        public void EndCurrentPhase()
        {
            if (_currentPhase == null)
                return;

            _currentPhase.State = PhaseState.Ending;

            // 触发阶段结束事件
            PublishEvent(new PhaseEndEvent
            {
                Phase = _currentPhase.Phase,
                ActivePlayer = _turnPlayer
            });

            // 根据阶段类型执行特定逻辑
            OnPhaseEnded(_currentPhase.Phase);
        }

        /// <summary>
        /// 阶段结束时的处理
        /// </summary>
        private void OnPhaseEnded(PhaseType phaseType)
        {
            // 主阶段结束时清理
            if (phaseType == PhaseType.Main)
            {
                // 清理战斗状态
            }
        }

        /// <summary>
        /// 进入下一阶段
        /// </summary>
        public void GoToNextPhase()
        {
            if (_currentPhase == null)
            {
                StartPhase(PhaseType.Standby);
                return;
            }

            int currentIndex = _phaseOrder.IndexOf(_currentPhase.Phase);
            int nextIndex = (currentIndex + 1) % _phaseOrder.Count;
            PhaseType nextPhase = _phaseOrder[nextIndex];

            // 特殊情况：结束阶段后直接开始下一回合的准备阶段
            if (nextPhase == PhaseType.Standby)
            {
                StartNewTurn(ResolveNextTurnPlayer());
            }
            else
            {
                StartPhase(nextPhase);
            }
        }

        /// <summary>
        /// 授予额外回合（仅支持当前回合玩家叠加额外回合）
        /// </summary>
        public void GrantExtraTurn(Player player)
        {
            if (player != null && player == _turnPlayer)
                _extraTurnsForCurrent++;
        }

        /// <summary>
        /// 标记某玩家跳过其下一个回合（整回合）
        /// </summary>
        public void SkipNextTurnFor(Player player)
        {
            SkipNextTurnFor(player, TurnSkipMode.Whole);
        }

        /// <summary>
        /// 标记某玩家跳过其下一个回合的指定档位（2026-10-02 D2：Whole/Standby/Main；2026-10-29 阶段3a：End）。
        /// Standby 档由 GameCore.OnTurnStarted 经 ConsumePendingStandbySkip 消费（跳准备阶段自动化）；
        /// Main 档在 AdvanceFromStandby 消费（准备阶段后直进结束阶段）；
        /// End 档在 OnEndPhaseStarted 消费（进入即折返，不发 TurnEndEvent）。
        /// </summary>
        public void SkipNextTurnFor(Player player, TurnSkipMode mode)
        {
            if (player == null) return;
            switch (mode)
            {
                case TurnSkipMode.Standby: _skipNextStandby.Add(player); break;
                case TurnSkipMode.Main: _skipNextMain.Add(player); break;
                case TurnSkipMode.End: _skipNextEnd.Add(player); break;
                default: _skipNextTurn.Add(player); break;
            }
        }

        /// <summary>消费"跳过准备阶段自动化"待定标记（GameCore.OnTurnStarted 调用，与 ITurnStartInterceptor 同口）。</summary>
        public bool ConsumePendingStandbySkip(Player player)
            => player != null && _skipNextStandby.Remove(player);

        /// <summary>
        /// 教学预设场面回合起跳（2026-10-06）：把下一回合钉到 turnNumber、并让 first 的回合开始
        /// 跳过准备阶段自动化（ConsumePendingStandbySkip 口径——不抽回合牌/不推进地牌槽曲线/不解横置，
        /// StartNewTurn 仍正常落到 Main，玩家直接站在起跳回合的操作环节）。
        /// 时序契约：InitGame(deferStart=true) 后、StartGame 前调用，且先于本调用完成场面注入——
        /// 被跳过的推进量（GlobalTurnIndex/地牌横置/每回合计数）由注入器自行预置。
        /// </summary>
        public void PrepareScenarioStart(int turnNumber, Player first)
        {
            if (turnNumber < 1) turnNumber = 1;
            _turnNumber = turnNumber - 1; // StartNewTurn 先自增再发事件 → 首个 TurnStartEvent 携带 turnNumber
            if (first != null)
                SkipNextTurnFor(first, TurnSkipMode.Standby);
        }

        /// <summary>
        /// 计算 End→Standby 折返时的下一位回合玩家：
        /// 优先消耗当前玩家的额外回合，再按被跳过标记顺延到对手。
        /// </summary>
        private Player ResolveNextTurnPlayer()
        {
            Player next;
            if (_extraTurnsForCurrent > 0)
            {
                _extraTurnsForCurrent--;
                next = _turnPlayer;
            }
            else
            {
                next = _turnPlayer.Opponent;
            }

            // 跳过回合：被标记者的回合作废，顺延到其对手
            while (next != null && _skipNextTurn.Contains(next))
            {
                _skipNextTurn.Remove(next);
                next = next.Opponent;
            }
            return next;
        }

        /// <summary>
        /// 检查游戏是否结束（经 GameCore 统一发布口：只发一次；双亡先到先得）
        /// </summary>
        private void CheckGameOver()
        {
            if (_gameCore != null)
            {
                if (_turnPlayer != null && _turnPlayer.Life <= 0)
                    _gameCore.PublishGameOverOnce(_turnPlayer.Opponent, GameOverReason.LifeZero);
                else if (_turnPlayer?.Opponent != null && _turnPlayer.Opponent.Life <= 0)
                    _gameCore.PublishGameOverOnce(_turnPlayer, GameOverReason.LifeZero);
                return;
            }

            // 未接线（独立测试）退化路径：维持原直发行为
            if (_turnPlayer != null && _turnPlayer.Life <= 0)
            {
                PublishEvent(new GameOverEvent
                {
                    Winner = _turnPlayer.Opponent,
                    Loser = _turnPlayer,
                    Reason = GameOverReason.LifeZero,
                    TotalTurns = _turnNumber
                });
            }

            if (_turnPlayer?.Opponent != null && _turnPlayer.Opponent.Life <= 0)
            {
                PublishEvent(new GameOverEvent
                {
                    Winner = _turnPlayer,
                    Loser = _turnPlayer.Opponent,
                    Reason = GameOverReason.LifeZero,
                    TotalTurns = _turnNumber
                });
            }
        }

        /// <summary>
        /// 检查是否可以进行战斗行动
        /// </summary>
        public bool CanCombatAction()
        {
            return _currentPhase?.Phase == PhaseType.Main &&
                   _currentPhase?.State == PhaseState.Active;
        }

        /// <summary>
        /// 检查是否可以发动效果
        /// </summary>
        public bool CanActivateEffect()
        {
            return _currentPhase?.State == PhaseState.Active;
        }

        /// <summary>
        /// 获取当前回合数
        /// </summary>
        public int GetCurrentTurnNumber()
        {
            return _turnNumber;
        }

        /// <summary>
        /// 发布事件：优先经由 GameCore 统一路由（保证 Trigger/Layer 引擎能观察到），
        /// 未接线时退化为直接走事件总线（便于独立测试）。
        /// </summary>
        private void PublishEvent<T>(T e) where T : IGameEvent
        {
            if (_gameCore != null)
                _gameCore.PublishEvent(e);
            else
                EventManager.Instance.Publish(e);
        }
    }

    /// <summary>
    /// 回合引擎扩展方法
    /// </summary>
    public static class TurnEngineExtensions
    {
        /// <summary>
        /// 检查是否为某玩家的回合
        /// </summary>
        public static bool IsTurnPlayer(this TurnEngine engine, Player player)
        {
            return engine.TurnPlayer == player;
        }

        /// <summary>
        /// 获取下一阶段类型
        /// </summary>
        public static PhaseType GetNextPhase(this TurnEngine engine)
        {
            if (engine.CurrentPhase == null)
                return PhaseType.Standby;

            var phaseOrder = new List<PhaseType>
            {
                PhaseType.Standby,
                PhaseType.Main,
                PhaseType.End
            };

            int currentIndex = phaseOrder.IndexOf(engine.CurrentPhase.Phase);
            int nextIndex = (currentIndex + 1) % phaseOrder.Count;
            return phaseOrder[nextIndex];
        }
    }
}
