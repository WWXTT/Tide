using System;

namespace CardCore
{
    /// <summary>暂停原因（2026-11-21 通用暂停系统·战场遮罩/高亮/暂停计划 §5·v0.4）：
    /// 谁发起的暂停——UI 侧 BattlePauseBus 据此分流提示文案与恢复口。
    /// None=无暂停在位（Resume/Reset 清零）。注：联机断线**不走暂停**（v0.4 §5.4 定案
    /// 「断开就结束」——NetBattleChannel.EndByDisconnect 判负结算），暂停调用方只有教学。</summary>
    public enum GamePauseReason
    {
        None,
        /// <summary>教学解说步（v0.3/v0.4 定案：默认关，按步由用户点名挂）。</summary>
        Tutorial
    }

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

        /// <summary>跨实例静态状态中继（2026-11-21 通用暂停系统）：与实例事件同拍发射。
        /// GameCore.ResetInstance 会换整个 GameCore（含本管理器实例），UI 侧表现层
        ///（BattlePauseBus）挂实例事件会随换代失联——挂本静态口跨局/跨实例稳定。
        /// 仅表现层消费；逻辑层内部一律走实例事件。</summary>
        public static event Action<GameState, GameState> StateChangedRelay;

        /// <summary>本次暂停的原因与提示文案（静态=随 Pause(reason) 写入、Resume/Reset 清零；
        /// 供表现层（BattlePauseBus）读取分流。CardCore 不消费，纯透传。）</summary>
        public static GamePauseReason PauseReason { get; private set; } = GamePauseReason.None;
        public static string PauseHint { get; private set; }

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
        /// 暂停游戏（无原因版=兼容旧调用，等同 reason=None——UI 侧按通用暂停提示）
        /// </summary>
        public bool Pause() => Pause(GamePauseReason.None, null);

        /// <summary>暂停游戏（2026-11-21 通用暂停系统）：reason/hint 供表现层分流
        ///（OpponentLost→「等待重连」提示层；Tutorial→解说步暂停横幅）。</summary>
        public bool Pause(GamePauseReason reason, string hint)
        {
            if (CurrentState != GameState.Running)
                return false;

            PauseReason = reason;
            PauseHint = hint;
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

            PauseReason = GamePauseReason.None;
            PauseHint = null;
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

            PauseReason = GamePauseReason.None;
            PauseHint = null;
            TransitionTo(GameState.Running);
            return true;
        }

        /// <summary>
        /// 重置状态到 NotStarted
        /// </summary>
        public void Reset()
        {
            PauseReason = GamePauseReason.None;
            PauseHint = null;
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
            StateChangedRelay?.Invoke(oldState, newState);
        }
    }
}
