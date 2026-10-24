using UnityEngine;

namespace SynergyUI
{
    /// <summary>
    /// 隐私同意状态（2026-10-24 TapTap 上架合规）：PlayerPrefs 版本化存档。
    /// 版本号与线上隐私政策更新日期联动——政策重大变更时升版本号即强制全体重弹同意；
    /// 用户同意后直到版本升级不再打扰。同意前任何第三方 SDK 不得初始化
    ///（SDK 闸见 HotUpdateEntry.Start / PrivacyConsentDialog 同意回调两处）。
    /// </summary>
    public static class PrivacyConsent
    {
        /// <summary>政策版本（线上更新日期 2026-10-24）。改政策=改此串，全体玩家重弹。</summary>
        public const string PolicyVersion = "20261024";

        private const string PrefKey = "tide.privacy.consent";

        /// <summary>当前政策版本下是否已同意（持久化于本机，卸载重装才清零）。</summary>
        public static bool IsGranted =>
            PlayerPrefs.GetString(PrefKey, "") == PolicyVersion;

        /// <summary>记录同意（玩家点击弹窗「同意」的唯一写入口）。</summary>
        public static void Grant()
        {
            PlayerPrefs.SetString(PrefKey, PolicyVersion);
            PlayerPrefs.Save();
            Debug.Log($"[PrivacyConsent] 已同意隐私政策 v{PolicyVersion}");
        }
    }
}
