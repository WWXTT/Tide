namespace CloudSave
{
    /// <summary>
    /// TapTap SDK / 云存档配置（2026-10-23 云存档接入）。
    /// 凭证来自开发者中心（应用须与出包包名+签名一致并开通云存档服务）——
    /// 留空时整链停用（仅打日志），填入后无需其他改动。
    /// </summary>
    public static class TapSdkConfig
    {
        /// <summary>云端存档名（仅英文/数字/下划线/中划线，≤60 字节——服务端约束）。</summary>
        public const string ArchiveName = "tide_save";

        /// <summary>凭证就绪（单源=联机侧 TapOnlineConfig，2026-10-23 双会话并存定案：
        /// 用户只填那一处，联机与云存档同时生效）。</summary>
        public static bool IsCredentialsReady => SynergyUI.Online.TapOnlineConfig.IsConfigured;

        /// <summary>开发者中心 Client ID（取自联机侧配置单源）。</summary>
        public static string ClientId => SynergyUI.Online.TapOnlineConfig.ClientId;

        /// <summary>开发者中心 Client Token（取自联机侧配置单源）。</summary>
        public static string ClientToken => SynergyUI.Online.TapOnlineConfig.ClientToken;

        /// <summary>单存档文件服务上限（10MB；超限放弃上传并报错）。</summary>
        public const int MaxArchiveBytes = 10 * 1024 * 1024;

        /// <summary>自动上传轮询间隔（秒）。创建/更新共享限频 60 次/分钟，此间隔足够安全。</summary>
        public const float AutoUploadPollSeconds = 5f;

        /// <summary>上传失败后的冷却（秒），防弱网下每轮重试刷屏。</summary>
        public const float UploadFailCooldownSeconds = 60f;

        /// <summary>云存档支持的平台（2026-10-23 一期仅 Android 真机；PC 走二期启动校验后再开）。
        /// 编辑器关闭——SDK 登录在编辑器内不可靠，验收一律走真机。</summary>
        public static bool CloudSaveSupported
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }
    }
}
