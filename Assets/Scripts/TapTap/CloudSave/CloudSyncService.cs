using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using CardCore;
using TapSDK.CloudSave;
using TapSDK.Core;
using UnityEngine;

namespace CloudSave
{
    /// <summary>
    /// 云存档同步服务（2026-10-23 定案：自动+手动兜底）。策略：
    /// · 启动（登录成功后）：拉云端列表比对 savedAt——无云端且本地有玩家数据=上传；
    ///   云端较新=处女档免询问自动恢复，否则经 <see cref="OnRestoreSuggested"/> 请 UI 询问；
    ///   本地较新=上传。
    /// · 设备识别（2026-11-12 定案）：云端档案 extra 带 deviceId（本机=Unity deviceUniqueIdentifier，
    ///   取不到回落本安装 GUID）；启动对账发现云端设备≠本机=设备变更——**不自动上传也不自动下载**，
    ///   经 OnRestoreSuggested(DeviceChanged) 交玩家选「使用云端 / 使用当前设备」。
    /// · 运行期：宿主 MonoBehaviour 每 <see cref="TapSdkConfig.AutoUploadPollSeconds"/> 秒查
    ///   mtime 脏判定（不改各存档管理器的写盘代码），脏且非处女档即防抖上传；
    ///   OnApplicationPause/Quit 冲刷一次。
    /// · 一切异常只落日志/状态文本，绝不阻断玩法（本地优先）。
    /// </summary>
    public static class CloudSyncService
    {
        private static string _archiveUuid;
        private static string _archiveFileId;
        private static long _cloudSavedAt;
        private static string _cloudDeviceId; // 云端档案归属设备（extra.deviceId；旧档案为空）
        private static DateTime _lastSyncAt;
        private static bool _busy;
        private static float _failCooldownUntil;
        private static bool _pendingRestorePrompt;
        private static RestorePromptReason _pendingReason;
        private static bool _promptRaised;        // 本次会话已发起过恢复询问（防双 Init 重复弹窗）
        private static long _promptedCloudSavedAt; // 上次询问对应的云端 savedAt（云端变新才重新问）

        /// <summary>恢复询问的起因（UI 文案分流：云端较新 / 设备变更）。</summary>
        public enum RestorePromptReason
        {
            None = 0,
            CloudNewer = 1,
            DeviceChanged = 2,
        }

        /// <summary>待答复询问的起因（仅 PendingRestorePrompt=true 时有效）。</summary>
        public static RestorePromptReason PendingReason => _pendingReason;

        /// <summary>云端档案归属设备 id（空=旧档案未带设备识别）。</summary>
        public static string CloudDeviceId => _cloudDeviceId;

        /// <summary>当前本机设备 id（UI 文案可显示短码）。</summary>
        public static string LocalDeviceId => SaveBundleCodec.EnsureDeviceId();

        /// <summary>状态/账号/云端档案引用变化（主菜单入口与面板的刷新接线点，主线程触发）。</summary>
        public static event Action OnStateChanged;

        /// <summary>云端较新且本地非处女档——请 UI 询问是否恢复（无 UI 订阅时挂起待主菜单）。</summary>
        public static event Action<long> OnRestoreSuggested;

        /// <summary>云端存档已还原落盘（管理器单例已重置——UI 需重订事件并刷新门槛）。</summary>
        public static event Action OnRestored;

        public static bool Busy => _busy;
        public static bool PendingRestorePrompt => _pendingRestorePrompt;
        public static bool HasCloudArchive => !string.IsNullOrEmpty(_archiveUuid);

        /// <summary>最近一次发现的云端存档 savedAt（恢复询问文案的时间展示用）。</summary>
        public static long SuggestedCloudSavedAt => _cloudSavedAt;

        /// <summary>主菜单云入口状态文本（一栏：平台/凭证/登录/同步态）。</summary>
        public static string StatusText
        {
            get
            {
                if (!TapSdkConfig.CloudSaveSupported) return "编辑器/PC 未启用";
                if (!TapSdkConfig.IsCredentialsReady) return "凭证未配置";
                if (_busy) return "同步中…";
                if (!TapAccountService.LoggedIn) return "未登录";
                if (_lastSyncAt == default) return "已登录";
                return _lastSyncAt.Date == DateTime.Today
                    ? $"已同步 {_lastSyncAt:HH:mm}"
                    : $"已同步 {_lastSyncAt:MM-dd HH:mm}";
            }
        }

        /// <summary>启动同步（Bootstrap 登录成功后/手动登录后调用；幂等由 _busy 闸承担）。</summary>
        public static async UniTask StartupSyncAsync()
        {
            if (!TapSdkConfig.CloudSaveSupported || !TapSdkConfig.IsCredentialsReady) return;
            if (!TapAccountService.LoggedIn || _busy) return;

            _busy = true;
            Notify();
            try
            {
                List<ArchiveData> list = await CloudTasks.AwaitOnMain(TapTapCloudSave.GetArchiveList(), 30f);
                ArchiveData own = list?.FirstOrDefault(a => a != null && a.Name == TapSdkConfig.ArchiveName);
                if (own == null)
                {
                    Debug.Log("[CloudSync] 云端无存档——本地非处女档时上传");
                    if (!SaveBundleCodec.IsPristineLocal())
                        await UploadCoreAsync("startup_no_cloud");
                }
                else
                {
                    _archiveUuid = own.Uuid;
                    _archiveFileId = own.FileId;
                    _cloudSavedAt = ParseExtraSavedAt(own.Extra);
                    _cloudDeviceId = ParseExtraDeviceId(own.Extra);
                    long local = SaveBundleCodec.ReadLocalSavedAt();
                    string localDevice = SaveBundleCodec.EnsureDeviceId();
                    bool deviceChanged = !string.IsNullOrEmpty(_cloudDeviceId)
                        && !string.IsNullOrEmpty(localDevice)
                        && !string.Equals(_cloudDeviceId, localDevice, StringComparison.Ordinal);
                    string dismissKey = DevicePromptKey(_cloudDeviceId, _cloudSavedAt);
                    Debug.Log($"[CloudSync] 云端 savedAt={_cloudSavedAt} 本地 savedAt={local}"
                        + $" 云端设备={ShortId(_cloudDeviceId)} 本机设备={ShortId(localDevice)}"
                        + $"（云端改于 {own.ModifiedTime}）");

                    if (deviceChanged && !SaveBundleCodec.IsDevicePromptDismissed(dismissKey))
                    {
                        // 2026-11-12 定案：设备变更一律不自动上传/下载——交玩家选「当前数据/云端数据」
                        Debug.Log("[CloudSync] 设备变更——不自动同步，等待玩家选择使用当前还是云端数据");
                        RaiseRestorePrompt(RestorePromptReason.DeviceChanged);
                    }
                    else if (deviceChanged)
                    {
                        // 本设备已答复过「使用当前设备」：只补传本地较新的情况；云端较新也不自动覆盖本地
                        if (!SaveBundleCodec.IsPristineLocal() && local > _cloudSavedAt)
                            await UploadCoreAsync("device_dismissed_local_newer");
                    }
                    else if (_cloudSavedAt > local)
                    {
                        if (SaveBundleCodec.IsPristineLocal())
                        {
                            Debug.Log("[CloudSync] 处女档+云端较新——免询问自动恢复");
                            await RestoreCoreAsync();
                        }
                        else
                        {
                            RaiseRestorePrompt(RestorePromptReason.CloudNewer); // 无 UI 订阅（如对局中）时挂起，主菜单 OnEnter 补问
                        }
                    }
                    else if (local > _cloudSavedAt)
                    {
                        await UploadCoreAsync("startup_local_newer");
                    }
                }
                _lastSyncAt = DateTime.Now;
            }
            catch (TapException ex)
            {
                Debug.LogWarning($"[CloudSync] 启动同步失败 TapException code={ex.Code}：{ex.Message}（下次入口/手动重试）");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CloudSync] 启动同步失败：{ex.Message}（下次入口/手动重试）");
            }
            finally
            {
                _busy = false;
                Notify();
            }
        }

        /// <summary>打包上传（手动按钮/自动轮询共用；启动对账走 UploadCoreAsync）。成功=true。</summary>
        public static async UniTask<bool> UploadAsync(string reason)
        {
            if (!TapSdkConfig.CloudSaveSupported || !TapSdkConfig.IsCredentialsReady) return false;
            if (!TapAccountService.LoggedIn)
            {
                Debug.Log("[CloudSync] 未登录，跳过上传");
                return false;
            }
            if (_busy)
            {
                Debug.Log($"[CloudSync] 同步进行中，跳过上传（{reason}）");
                return false;
            }
            if (Time.unscaledTime < _failCooldownUntil) return false; // 弱网冷却，防每轮重试刷屏

            _busy = true;
            Notify();
            try { return await UploadCoreAsync(reason); }
            finally { _busy = false; Notify(); }
        }

        /// <summary>上传主体（不做 _busy 闸）：StartupSync 已持锁时直调本口。
        /// 2026-11-12 修：原实现 StartupSync 持 _busy 后又调公开 UploadAsync，被闸挡住——
        /// 「启动无云端→上传」「本地较新→上传」「处女档自动恢复」三条分支实际全是死路。</summary>
        private static async UniTask<bool> UploadCoreAsync(string reason)
        {
            try
            {
                string stagingPath = SaveBundleCodec.Pack(out long savedAt);
                if (stagingPath == null) return false;

                var metadata = new ArchiveMetadata(
                    TapSdkConfig.ArchiveName,
                    $"潮汐进度 {DateTime.Now:MM-dd HH:mm}（{CountFiles()} 文件）",
                    ExtraJson(savedAt),
                    archivePlaytime: 0);
                ArchiveData result = string.IsNullOrEmpty(_archiveUuid)
                    ? await CloudTasks.AwaitOnMain(TapTapCloudSave.CreateArchive(metadata, stagingPath, null), 60f)
                    : await CloudTasks.AwaitOnMain(TapTapCloudSave.UpdateArchive(_archiveUuid, metadata, stagingPath, null), 60f);

                _archiveUuid = result.Uuid;
                _archiveFileId = result.FileId;
                _cloudSavedAt = savedAt;
                _cloudDeviceId = SaveBundleCodec.EnsureDeviceId();
                _lastSyncAt = DateTime.Now;
                Debug.Log($"[CloudSync] 上传成功（{reason}）uuid={_archiveUuid} savedAt={savedAt} 设备={ShortId(_cloudDeviceId)}");
                Notify();
                return true;
            }
            catch (TapException ex)
            {
                _failCooldownUntil = Time.unscaledTime + TapSdkConfig.UploadFailCooldownSeconds;
                Debug.LogWarning($"[CloudSync] 上传失败 TapException code={ex.Code}：{ex.Message}（{TapSdkConfig.UploadFailCooldownSeconds:F0}s 后允许重试）");
                return false;
            }
            catch (Exception ex)
            {
                _failCooldownUntil = Time.unscaledTime + TapSdkConfig.UploadFailCooldownSeconds;
                Debug.LogWarning($"[CloudSync] 上传失败：{ex.Message}");
                return false;
            }
        }

        /// <summary>下载还原（手动/询问确认共用；启动对账走 RestoreCoreAsync）：落盘→管理器重置→OnRestored。</summary>
        public static async UniTask<bool> RestoreFromCloudAsync()
        {
            if (!TapSdkConfig.CloudSaveSupported || string.IsNullOrEmpty(_archiveUuid))
            {
                Debug.Log("[CloudSync] 无云端档案引用，跳过还原");
                return false;
            }
            if (_busy) return false;

            _busy = true;
            _pendingRestorePrompt = false;
            Notify();
            try { return await RestoreCoreAsync(); }
            finally { _busy = false; Notify(); }
        }

        /// <summary>还原主体（不做 _busy 闸）：StartupSync 已持锁时直调本口（同 UploadCoreAsync 的修复口径）。</summary>
        private static async UniTask<bool> RestoreCoreAsync()
        {
            string restoredFromDevice = _cloudDeviceId; // 还原前记下来源设备（还原后云端归属改记本机）
            long restoredFromSavedAt = _cloudSavedAt;
            try
            {
                byte[] data = await CloudTasks.AwaitOnMain(
                    TapTapCloudSave.GetArchiveData(_archiveUuid, _archiveFileId), 60f);
                int written = SaveBundleCodec.Unpack(data);
                if (written < 0) return false;

                // 全量重载（2026-11-09 定案）：落盘后静态缓存（内容包/卡目录/效果库/词条表）与
                // 教学/挑战/元素解锁进度单例确定性失效——不再依赖 mtime 盯盘的节流刷新；
                // UI 层经 OnRestored 清栈回启动页整树重建，双保险保证读到新数据。
                SynergyUI.GameDataReload.ReloadAllCaches();
                // 还原后本机接管这份档案：Unpack 已把本地指纹的 deviceId 写成本机；再记「该云端档案
                // 已答复过」，防下次启动因云端 extra 仍挂旧设备 id 而重复弹窗（首次上传后云端自然改记本机）。
                SaveBundleCodec.MarkDevicePromptDismissed(DevicePromptKey(restoredFromDevice, restoredFromSavedAt));
                _cloudDeviceId = SaveBundleCodec.EnsureDeviceId();
                _lastSyncAt = DateTime.Now;
                Debug.Log($"[CloudSync] 云存档已还原（{written} 文件）——管理器已重置");
                OnRestored?.Invoke();
                Notify();
                return true;
            }
            catch (TapException ex)
            {
                Debug.LogWarning($"[CloudSync] 还原失败 TapException code={ex.Code}：{ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CloudSync] 还原失败：{ex.Message}");
                return false;
            }
        }

        /// <summary>主菜单对挂起的恢复询问给出的答复（false=使用当前设备数据）。
        /// 设备变更场景：false 时本地有玩家数据就上传覆盖云端（云端归属改记本机）；处女档不上传。</summary>
        public static void AnswerRestorePrompt(bool accept)
        {
            var reason = _pendingReason;
            _pendingRestorePrompt = false;
            _pendingReason = RestorePromptReason.None;
            if (accept)
            {
                RestoreFromCloudAsync().Forget();
                return;
            }
            if (reason == RestorePromptReason.DeviceChanged)
            {
                SaveBundleCodec.MarkDevicePromptDismissed(DevicePromptKey(_cloudDeviceId, _cloudSavedAt));
                if (!SaveBundleCodec.IsPristineLocal())
                    UploadAsync("device_changed_keep_local").Forget();
                else
                    Debug.Log("[CloudSync] 设备变更：本机为处女档——保留云端，不上传空进度");
            }
        }

        /// <summary>退出登录：清云端档案引用（换账号后 StartupSync 重新发现）。</summary>
        public static void OnLoggedOut()
        {
            _archiveUuid = null;
            _archiveFileId = null;
            _cloudSavedAt = 0;
            _cloudDeviceId = null;
            _lastSyncAt = default;
            _pendingRestorePrompt = false;
            _pendingReason = RestorePromptReason.None;
            _promptRaised = false;
            _promptedCloudSavedAt = 0;
            Notify();
        }

        // ======================================== 宿主驱动 ========================================

        /// <summary>启用宿主（Bootstrap/主菜单首次交互时调用）：常驻隐藏 GameObject 承载
        /// 轮询（mtime 脏→防抖上传）与 OnApplicationPause/Quit 冲刷。</summary>
        public static void EnsureHost()
        {
            if (!TapSdkConfig.CloudSaveSupported || !TapSdkConfig.IsCredentialsReady) return;
            CloudSyncHost.Ensure();
        }

        private static void Notify() => OnStateChanged?.Invoke();

        /// <summary>发起恢复询问（设备变更/云端较新共用）：挂起标记 + 会话级去重（双 Init 会跑两遍对账）。</summary>
        private static void RaiseRestorePrompt(RestorePromptReason reason)
        {
            if (_promptRaised && _promptedCloudSavedAt == _cloudSavedAt) return; // 同一份云端档案本次会话已问过
            _promptRaised = true;
            _promptedCloudSavedAt = _cloudSavedAt;
            _pendingRestorePrompt = true;
            _pendingReason = reason;
            OnRestoreSuggested?.Invoke(_cloudSavedAt);
        }

        /// <summary>云端 extra：savedAt + 归属设备 id（设备识别 2026-11-12）。</summary>
        private static string ExtraJson(long savedAt)
        {
            string device = SaveBundleCodec.EnsureDeviceId();
            return string.IsNullOrEmpty(device)
                ? $"{{\"savedAt\":{savedAt}}}"
                : $"{{\"savedAt\":{savedAt},\"deviceId\":\"{device}\"}}";
        }

        private static long ParseExtraSavedAt(string extra)
        {
            if (string.IsNullOrEmpty(extra)) return 0;
            try
            {
                var mirror = TideJson.FromJson<ExtraMirror>(extra);
                return mirror?.savedAt ?? 0;
            }
            catch { return 0; }
        }

        private static string ParseExtraDeviceId(string extra)
        {
            if (string.IsNullOrEmpty(extra)) return "";
            try { return TideJson.FromJson<ExtraMirror>(extra)?.deviceId ?? ""; }
            catch { return ""; }
        }

        /// <summary>「已答复使用当前设备」的键：同一台来源设备+同一份云端时间戳才复用答复。</summary>
        private static string DevicePromptKey(string deviceId, long savedAt)
            => string.IsNullOrEmpty(deviceId) ? "" : deviceId + "@" + savedAt;

        private static string ShortId(string id)
            => string.IsNullOrEmpty(id) ? "无" : (id.Length <= 8 ? id : id.Substring(0, 8));

        private static int CountFiles()
        {
            int count = 0;
            foreach (string root in new[] { "TutorialProgress.json", "ChallengeProgress.json" }) // 2026-11-09：ElementUnlock 出云
                if (System.IO.File.Exists(System.IO.Path.Combine(TidePaths.PersistentDataPath, root))) count++;
            if (System.IO.Directory.Exists(SynergyUI.CardDataPaths.CardDir))
                count += System.IO.Directory.GetFiles(SynergyUI.CardDataPaths.CardDir, "*.json").Length;
            if (System.IO.Directory.Exists(CardCore.EncounterStore.Dir))
                count += System.IO.Directory.GetFiles(CardCore.EncounterStore.Dir, "*.json").Length;
            return count;
        }

        [Serializable]
        private class ExtraMirror
        {
            public long savedAt;
            public string deviceId; // 2026-11-12：档案归属设备（旧档案无此字段=空=不判设备变更）
        }

        // ======================================== 轮询宿主 ========================================

        /// <summary>隐藏常驻 MonoBehaviour（DontDestroyOnLoad）：自动上传轮询 + 暂停/退出冲刷。
        /// Android 后台化走 OnApplicationPause(true)（OnApplicationQuit 不保证有网络窗口）。</summary>
        private sealed class CloudSyncHost : MonoBehaviour
        {
            private static CloudSyncHost _instance;
            private float _pollTimer;

            public static CloudSyncHost Ensure()
            {
                if (_instance != null) return _instance;
                var go = new GameObject("CloudSyncService.Host");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<CloudSyncHost>();
                return _instance;
            }

            private void Update()
            {
                _pollTimer += Time.unscaledDeltaTime;
                if (_pollTimer < TapSdkConfig.AutoUploadPollSeconds) return;
                _pollTimer = 0f;
                TryAutoUpload().Forget();
            }

            private async UniTaskVoid TryAutoUpload()
            {
                if (_busy || !TapAccountService.LoggedIn || !SaveBundleCodec.IsLocalDirty()) return;
                if (SaveBundleCodec.IsPristineLocal()) return; // 处女档不上云（防空进度污染云端）
                await UploadAsync("auto_dirty");
            }

            private void OnApplicationPause(bool pauseStatus)
            {
                if (pauseStatus) TryAutoUpload().Forget(); // 后台化冲刷（尽力而为，失败下次启动对账）
            }

            private void OnApplicationQuit()
            {
                if (SaveBundleCodec.IsLocalDirty() && TapAccountService.LoggedIn && !_busy)
                    UploadAsync("quit_flush").Forget();
            }
        }
    }
}
