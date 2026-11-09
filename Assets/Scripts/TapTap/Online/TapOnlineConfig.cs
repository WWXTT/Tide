namespace SynergyUI.Online
{
    /// <summary>
    /// TapTap 联机配置（2026-10-23 接入）。ClientId/ClientToken 从开发者中心 →
    /// 你的应用 →「SDK 配置/下载」获取；填入后真机才能登录联机。
    /// RoomType 是房间列表的检索键（≤32 字节）——全游戏统一一个类型，建房/查列表共用。
    /// </summary>
    public static class TapOnlineConfig
    {
        // 2026-10-23 填入开发者后台凭证（Server Secret 只给服务端用，绝不进客户端）
        public const string ClientId = "rzydrvcs98zy4ny7jy";
        public const string ClientToken = "lMxeCcm1hIyEXzUOjv9fKXzJn2EBL09bYe4D1MhK";

        /// <summary>房间类型（房间列表按它过滤——只显示本游戏的房间）。</summary>
        public const string RoomType = "tide-duel";

        /// <summary>配置就绪（占位符未替换=未配置，UI 拦截并提示）。</summary>
        public static bool IsConfigured =>
            ClientId != "FILL_ME_CLIENT_ID" && ClientToken != "FILL_ME_CLIENT_TOKEN";
    }
}
