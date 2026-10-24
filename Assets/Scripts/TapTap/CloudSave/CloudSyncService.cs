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
        private static DateTime _lastSyncAt;
        private static bool _busy;
        private static float _failCooldownUntil;
        private static bool _pendingRestorePrompt;

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
                        await UploadAsync("startup_no_cloud");
                }
                else
                {
                    _archiveUuid = own.Uuid;
                    _archiveFileId = own.FileId;
                    _cloudSavedAt = ParseExtraSavedAt(own.Extra);
                    long local = SaveBundleCodec.ReadLocalSavedAt();
                    Debug.Log($"[CloudSync] 云端 savedAt={_cloudSavedAt} 本地 savedAt={local}（云端改于 {own.ModifiedTime}）");

                    if (_cloudSavedAt > local)
                    {
                        if (SaveBundleCodec.IsPristineLocal())
                        {
                            Debug.Log("[CloudSync] 处女档+云端较新——免询问自动恢复");
                            await RestoreFromCloudAsync();
                        }
                        else
                        {
                            _pendingRestorePrompt = true; // 无 UI 订阅（如对局中）时挂起，主菜单 OnEnter 补问
                            OnRestoreSuggested?.Invoke(_cloudSavedAt);
                        }
                    }
                    else if (local > _cloudSavedAt)
                    {
                        await UploadAsync("startup_local_newer");
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

        /// <summary>打包上传（手动按钮与自动/启动链共用）。成功=true。</summary>
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
                _lastSyncAt = DateTime.Now;
                Debug.Log($"[CloudSync] 上传成功（{reason}）uuid={_archiveUuid} savedAt={savedAt}");
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
            finally
            {
                _busy = false;
                Notify();
            }
        }

        /// <summary>下载还原（自动/询问确认/手动按钮共用）：落盘→管理器重置→OnRestored。成功=true。</summary>
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
            try
            {
                byte[] data = await CloudTasks.AwaitOnMain(
                    TapTapCloudSave.GetArchiveData(_archiveUuid, _archiveFileId), 60f);
                int written = SaveBundleCodec.Unpack(data);
                if (written < 0) return false;

                // 进度管理器惰性单例整体重置——下次访问按新落盘数据重建（DeckSerializer 等
                // 文件型序列化器无内存态，无需重置）。旧实例事件订阅随之失效，UI 在 OnRestored 重订。
                TutorialProgressManager.ResetForCloudRestore();
                ChallengeProgressManager.ResetForCloudRestore();
                ElementUnlockManager.ResetForCloudRestore();
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
            finally
            {
                _busy = false;
                Notify();
            }
        }

        /// <summary>主菜单对挂起的恢复询问给出的答复（false=本轮不再问，保留云端不动作）。</summary>
        public static void AnswerRestorePrompt(bool accept)
        {
            _pendingRestorePrompt = false;
            if (accept) RestoreFromCloudAsync().Forget();
        }

        /// <summary>退出登录：清云端档案引用（换账号后 StartupSync 重新发现）。</summary>
        public static void OnLoggedOut()
        {
            _archiveUuid = null;
            _archiveFileId = null;
            _cloudSavedAt = 0;
            _lastSyncAt = default;
            _pendingRestorePrompt = false;
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

        private static string ExtraJson(long savedAt) => $"{{\"savedAt\":{savedAt}}}";

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

        private static int CountFiles()
        {
            int count = 0;
            foreach (string root in new[] { "TutorialProgress.json", "ChallengeProgress.json", "ElementUnlock.json" })
                if (System.IO.File.Exists(System.IO.Path.Combine(TidePaths.PersistentDataPath, root))) count++;
            if (System.IO.Directory.Exists(SynergyUI.CardDataPaths.CardDir))
                count += System.IO.Directory.GetFiles(SynergyUI.CardDataPaths.CardDir, "*.json").Length;
            return count;
        }

        [Serializable]
        private class ExtraMirror
        {
            public long savedAt;
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
