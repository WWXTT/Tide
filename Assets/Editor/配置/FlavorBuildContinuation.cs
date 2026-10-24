// 自动出包续跑（2026-10-24 一键出包自动切风味升级）：出包菜单在风味不符时先切环境并挂起（EditorPrefs），
// 编译重载后由本类重臂，等环境稳定（isCompiling/isUpdating 双静默 + 包列表往返收敛）且三处一致时自动出包。
// 演练开关 EditorPrefs "Tide.AutoBuild.DryRun"=真时末步只打日志不出包（闭环验证不动真包）。
#if TIDE_HYBRID_YOO
using System;
using UnityEditor;
using UnityEngine;

namespace Tide.HotUpdateTools
{
    [InitializeOnLoad]
    internal static class FlavorBuildContinuation
    {
        private const string PendingFlavorKey = "Tide.PendingBuildFlavor";  // ""=无挂起；Local/TapTap
        private const string PendingSinceKey = "Tide.PendingBuildSinceUtc"; // 挂起时刻（陈旧守卫用）
        private const string DryRunKey = "Tide.AutoBuild.DryRun";
        private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan StableFor = TimeSpan.FromSeconds(2);

        private static DateTime _idleSince;
        private static bool _listCheckDone;
        private static UnityEditor.PackageManager.Requests.Request<UnityEditor.PackageManager.PackageCollection> _listReq;
        private static int _envRetries;

        static FlavorBuildContinuation()
        {
            EditorApplication.delayCall += ResumeIfPending;
        }

        /// <summary>出包菜单挂起（风味切换已提交；编译重载后自动续跑，本轮会话未触发重载也能续）。</summary>
        internal static void ArmPendingBuild(YooPackBuilder.BuildFlavor flavor)
        {
            EditorPrefs.SetString(PendingFlavorKey, flavor.ToString());
            EditorPrefs.SetString(PendingSinceKey, DateTime.UtcNow.ToString("o"));
            ResetWave();
            _envRetries = 0;
            EditorApplication.delayCall += ResumeIfPending;
            Debug.Log("[自动出包] 风味切换已提交——编译重载完成后自动出包（无需再点菜单）；"
                + "如需中止：Tools/打包/自动出包/取消挂起");
        }

        [MenuItem("Tools/打包/自动出包/取消挂起（不出包）")]
        public static void CancelPending()
        {
            if (EditorPrefs.GetString(PendingFlavorKey, "") == "")
            {
                Debug.Log("[自动出包] 当前无挂起任务");
                return;
            }
            Clear("已手动取消挂起——不会自动出包（环境保持当前风味，可用风味切换菜单自愈）", false);
        }

        private static void ResumeIfPending()
        {
            if (EditorPrefs.GetString(PendingFlavorKey, "") == "") return;
            DateTime since;
            if (!DateTime.TryParse(EditorPrefs.GetString(PendingSinceKey, ""), null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out since)
                || DateTime.UtcNow - since > StaleAfter)
            {
                Clear("挂起态缺失或过期（>30 分钟）——已清除，请手动出包（环境异常时用风味切换菜单自愈）", true);
                return;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                ResetWave();
                Rearm();
                return;
            }
            if (_idleSince == default(DateTime)) { _idleSince = DateTime.UtcNow; Rearm(); return; }
            if (DateTime.UtcNow - _idleSince < StableFor) { Rearm(); return; }
            if (!_listCheckDone)
            {
                // 包列表往返：确认 UPM resolve 已收敛（resolve 尾部可能再触发一轮编译，故完成后再压一轮稳定窗）
                if (_listReq == null) { _listReq = UnityEditor.PackageManager.Client.List(); Rearm(); return; }
                if (!_listReq.IsCompleted) { Rearm(); return; }
                _listCheckDone = true;
                _idleSince = default(DateTime);
                Rearm();
                return;
            }
            var flavor = EditorPrefs.GetString(PendingFlavorKey) == YooPackBuilder.BuildFlavor.Local.ToString()
                ? YooPackBuilder.BuildFlavor.Local
                : YooPackBuilder.BuildFlavor.TapTap;
            if (!YooPackBuilder.FlavorEnvironmentMatches(flavor))
            {
                if (++_envRetries > 40)
                {
                    Clear("风味环境迟迟未一致（重试上限 40 轮）——挂起清除；请查上方日志或手动跑风味切换菜单后再出包", true);
                    return;
                }
                ResetWave(); // 可能还有下一轮编译波（UPM 分阶段导入），重头再等
                Rearm();
                return;
            }
            if (EditorPrefs.GetBool(DryRunKey, false))
            {
                Clear("演练：预检通过（" + flavor + " 风味三处一致）——按演练开关跳过实际出包，切换→重载→续跑闭环验证完成", false);
                return;
            }
            bool ok = YooPackBuilder.BuildPlayerPackageCore(flavor);
            Clear(ok ? "出包完成（" + flavor + "）" : "出包失败（" + flavor + "）——详见上方日志；挂起已清除", !ok);
        }

        private static void Rearm() => EditorApplication.delayCall += ResumeIfPending;

        private static void ResetWave()
        {
            _idleSince = default(DateTime);
            _listCheckDone = false;
            _listReq = null;
        }

        private static void Clear(string message, bool error)
        {
            EditorPrefs.DeleteKey(PendingFlavorKey);
            EditorPrefs.DeleteKey(PendingSinceKey);
            ResetWave();
            _envRetries = 0;
            if (error) Debug.LogError("[自动出包] " + message);
            else Debug.Log("[自动出包] " + message);
        }
    }
}
#endif
