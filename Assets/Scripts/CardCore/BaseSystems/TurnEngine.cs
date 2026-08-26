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
            _turnNumber = 1;
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
        /// 开始新回合
        /// </summary>
        public void StartNewTurn(Player player)
        {
            _turnPlayer = player;
            _turnNumber++;

            // 触发回合开始事件
            PublishEvent(new TurnStartEvent
            {
                TurnPlayer = player,
                TurnNumber = _turnNumber
            });

            // 开始第一个阶段（准备阶段）
            StartPhase(PhaseType.Standby);
        }

        /// <summary>
        /// 开始指定阶段
        /// </summary>
        public void StartPhase(PhaseType phaseType)
        {
            _currentPhase = new PhaseInfo
            {
                Phase = phaseType,
                ActivePlayer = _turnPlayer,
                StartTime = DateTime.Now,
                State = PhaseState.Active
            };

            // 触发阶段开始事件
            PublishEvent(new PhaseStartEvent
            {
                Phase = phaseType,
                ActivePlayer = _turnPlayer
            });

            // 根据阶段类型执行特定逻辑
            OnPhaseStarted(phaseType);
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
        /// 准备阶段：重置一回合一次 → 抽1张 → 等待玩家放元素池
        /// </summary>
        private void OnStandbyPhaseStarted()
        {
            // 横置恢复 / 抽牌 / 元素池与「每回合一次」计数重置由 GameCore.OnTurnStarted 统一处理
            // （订阅 TurnStartEvent，在准备阶段开始前完成）。
            // 玩家在准备阶段的操作（放元素池）由 GameActions 处理，完成后调用 AdvanceFromStandby()。
        }

        /// <summary>
        /// 准备阶段完成，进入主阶段
        /// 由 GameActions.AddToElementPool 或 GameActions.SkipElementPool 调用
        /// </summary>
        public void AdvanceFromStandby()
        {
            if (_currentPhase?.Phase != PhaseType.Standby)
                return;
            EndCurrentPhase();
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
        /// </summary>
        private void OnEndPhaseStarted()
        {
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
        /// 玩家主动结束回合
        /// </summary>
        public void EndTurn()
        {
            if (_currentPhase?.Phase != PhaseType.Main)
                return;
            if (_currentPhase?.State != PhaseState.Active)
                return;
            // 栈未结算完毕时不能结束回合
            if (_isStackEmpty != null && !_isStackEmpty())
                return;

            EndCurrentPhase();
            GoToNextPhase();
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
        /// 标记某玩家跳过其下一个回合
        /// </summary>
        public void SkipNextTurnFor(Player player)
        {
            if (player != null)
                _skipNextTurn.Add(player);
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
        /// 检查游戏是否结束
        /// </summary>
        private void CheckGameOver()
        {
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
