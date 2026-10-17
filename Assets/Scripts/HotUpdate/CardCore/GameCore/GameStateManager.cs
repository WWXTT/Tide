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
        /// 终局撤销（2026-10-17 教学考验「打输重来」定案）：Ended→Running——教学局败局原地重铺
        /// （midTransition 重注入+生命重置）续打，不走新开局。语义上终局本应不可逆，此口仅供
        /// 教学重开链路使用（调用方负责场面/生命的完整重置），非教学局禁止使用。
        /// </summary>
        public bool ResumeFromEnded()
        {
            if (CurrentState != GameState.Ended)
                return false;

            TransitionTo(GameState.Running);
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
