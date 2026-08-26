namespace CardCore
{
    /// <summary>
    /// 游戏状态枚举
    /// 表示游戏的当前运行状态
    /// </summary>
    public enum GameState
    {
        /// <summary>游戏未开始</summary>
        NotStarted,
        /// <summary>游戏正在进行</summary>
        Running,
        /// <summary>游戏暂停</summary>
        Paused,
        /// <summary>游戏已结束</summary>
        Ended
    }
}