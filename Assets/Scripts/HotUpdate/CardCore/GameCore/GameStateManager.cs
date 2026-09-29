using System;

namespace CardCore
{
    /// <summary>
    /// 游戏状态管理器
    /// 负责游戏状态（NotStarted/Running/Paused/Ended）的转换和状态守卫
    /// </summary>
    public class GameStateManager
    {
        public GameState CurrentState { get; private set; } = GameState.NotStarted;

        /// <summary>
        /// 状态变更事件
        /// </summary>
        public event Action<GameState, GameState> OnStateChanged;

        /// <summary>
        /// 开始游戏
        /// </summary>
        public bool StartGame()
        {
            if (CurrentState != GameState.NotStarted)
                return false;

            TransitionTo(GameState.Running);
            return true;
        }

        /// <summary>
        /// 暂停游戏
        /// </summary>
        public bool Pause()
        {
            if (CurrentState != GameState.Running)
                return false;

            TransitionTo(GameState.Paused);
            return true;
        }

        /// <summary>
        /// 恢复游戏
        /// </summary>
        public bool Resume()
        {
            if (CurrentState != GameState.Paused)
                return false;

            TransitionTo(GameState.Running);
            return true;
        }

        /// <summary>
        /// 结束游戏
        /// </summary>
        public bool EndGame()
        {
            if (CurrentState == GameState.Ended)
                return false;

            TransitionTo(GameState.Ended);
            return true;
        }

        /// <summary>
        /// 重置状态到 NotStarted
        /// </summary>
        public void Reset()
        {
            TransitionTo(GameState.NotStarted);
        }

        /// <summary>
        /// 是否可以进行游戏操作（Running 状态）
        /// </summary>
        public bool CanPerformGameActions => CurrentState == GameState.Running;

        private void TransitionTo(GameState newState)
        {
            var oldState = CurrentState;
            CurrentState = newState;
            OnStateChanged?.Invoke(oldState, newState);
        }
    }
}
