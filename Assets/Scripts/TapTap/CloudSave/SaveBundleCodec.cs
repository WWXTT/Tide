using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CardCore;
using SynergyUI;
using UnityEngine;

namespace CloudSave
{
    /// <summary>
    /// 云存档打包/还原（2026-10-23）：把散落 persistentDataPath 的多文件 JSON 进度
    /// （教程/挑战/元素解锁 + Card/ 卡组与自建卡效果）收成单个 envelope JSON 上传（≤10MB），
    /// 下载后逐文件写回。Tutorial/ 种子目录不进云（随包只读，EnsureBootstrap 自会刷新）。
    /// 目录自守护：编辑器/TideServer 的 CardDir 是仓库源（Assets/CardData），一律拒打包拒还原。
    /// </summary>
    public static class SaveBundleCodec
    {
        private const int Schema = 1;
        private static readonly string[] RootFiles = { "TutorialProgress.json", "ChallengeProgress.json", "ElementUnlock.json" };
        private static string CloudDir => Path.Combine(TidePaths.PersistentDataPath, ".cloud");
        private static string StagingPath => Path.Combine(CloudDir, "staging_bundle.json");
        private static string LocalMetaPath => Path.Combine(CloudDir, "local_meta.json");

        /// <summary>玩家卡组目录是否为持久化目录（仓库源=编辑器/TideServer，云存档禁区）。</summary>
        private static bool CardDirIsPersistent =>
            CardDataPaths.CardDir.StartsWith(TidePaths.PersistentDataPath, StringComparison.OrdinalIgnoreCase);

        // ======================================== 打包 ========================================

        /// <summary>
        /// 收集本地进度打包写入 staging 文件。返回 staging 路径；不可打包/超限返回 null（已落日志）。
        /// savedAt=本次打包时刻（unix 秒），同时写入本地指纹 local_meta（还原/云端比对用）。
        /// </summary>
        public static string Pack(out long savedAt)
        {
            savedAt = 0;
            if (!CardDirIsPersistent)
            {
                Debug.LogWarning($"[CloudSync] CardDir 非持久化目录（{CardDataPaths.CardDir}），跳过打包");
                return null;
            }

            try
            {
                var files = new Dictionary<string, string>();
                foreach (string root in RootFiles)
                {
                    string path = Path.Combine(TidePaths.PersistentDataPath, root);
                    if (File.Exists(path)) files[root] = File.ReadAllText(path);
                }
                if (Directory.Exists(CardDataPaths.CardDir))
                {
                    foreach (string file in Directory.GetFiles(CardDataPaths.CardDir, "*.json"))
                        files["Card/" + Path.GetFileName(file)] = File.ReadAllText(file);
                }
                if (files.Count == 0)
                {
                    Debug.LogWarning("[CloudSync] 无可打包的进度文件，跳过");
                    return null;
                }

                savedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var bundle = new SaveBundle { schema = Schema, savedAt = savedAt, files = files };
                string json = TideJson.ToJson(bundle, false);
                if (System.Text.Encoding.UTF8.GetByteCount(json) > TapSdkConfig.MaxArchiveBytes)
                {
                    Debug.LogError($"[CloudSync] 打包体积 {System.Text.Encoding.UTF8.GetByteCount(json)} 超服务上限 {TapSdkConfig.MaxArchiveBytes}——放弃上传");
                    return null;
                }

                Directory.CreateDirectory(CloudDir);
                File.WriteAllText(StagingPath, json);
                WriteLocalSavedAt(savedAt);
                return StagingPath;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CloudSync] 打包失败：{ex.Message}");
                return null;
            }
        }

        // ======================================== 还原 ========================================

        /// <summary>
        /// 云端字节流还原到本地：逐文件覆盖写回；Card/ 内云端没有的本地卡组文件删除
        /// （云端是权威快照——设备 B 删过的卡组不该复活）。返回写回文件数；失败返回 -1。
        /// </summary>
        public static int Unpack(byte[] data)
        {
            if (!CardDirIsPersistent)
            {
                Debug.LogWarning($"[CloudSync] CardDir 非持久化目录（{CardDataPaths.CardDir}），拒绝还原");
                return -1;
            }
            try
            {
                string json = System.Text.Encoding.UTF8.GetString(data);
                var bundle = TideJson.FromJson<SaveBundle>(json);
                if (bundle == null || bundle.files == null || bundle.schema != Schema)
                {
                    Debug.LogError("[CloudSync] 云端存档格式不符（schema 不匹配或解析失败），放弃还原");
                    return -1;
                }

                int written = 0;
                foreach (var kv in bundle.files)
                {
                    // 路径白名单：根三文件 + Card/ 平铺一层——拒绝任何越界相对路径（../ 等）
                    string dest;
                    if (RootFiles.Contains(kv.Key))
                        dest = Path.Combine(TidePaths.PersistentDataPath, kv.Key);
                    else if (kv.Key.StartsWith("Card/", StringComparison.Ordinal) && !kv.Key.Contains(".."))
                        dest = Path.Combine(CardDataPaths.CardDir, Path.GetFileName(kv.Key));
                    else
                    {
                        Debug.LogWarning($"[CloudSync] 云端存档含未知条目 {kv.Key}，跳过");
                        continue;
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(dest));
                    File.WriteAllText(dest, kv.Value);
                    written++;
                }

                // 权威快照收尾：删掉云端没有的本地卡组文件（Cards/Effects 表是种子补缺语义，不删）
                if (Directory.Exists(CardDataPaths.CardDir))
                {
                    foreach (string file in Directory.GetFiles(CardDataPaths.CardDir, "*.json"))
                    {
                        string rel = "Card/" + Path.GetFileName(file);
                        if (!bundle.files.ContainsKey(rel))
                        {
                            File.Delete(file);
                            Debug.Log($"[CloudSync] 还原删除云端不存在的本地卡组文件：{Path.GetFileName(file)}");
                        }
                    }
                }

                WriteLocalSavedAt(bundle.savedAt); // 本地指纹对齐云端，防还原后立即反向上传覆盖
                Debug.Log($"[CloudSync] 还原完成：{written} 文件，云端指纹 savedAt={bundle.savedAt}");
                return written;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CloudSync] 还原失败：{ex.Message}");
                return -1;
            }
        }

        // ======================================== 本地指纹 ========================================

        /// <summary>本地指纹（最近一次打包/还原的 savedAt；0=从未打包）。</summary>
        public static long ReadLocalSavedAt()
        {
            try
            {
                if (!File.Exists(LocalMetaPath)) return 0;
                var meta = TideJson.FromJson<LocalMeta>(File.ReadAllText(LocalMetaPath));
                return meta?.savedAt ?? 0;
            }
            catch { return 0; }
        }

        private static void WriteLocalSavedAt(long savedAt)
        {
            // settledAt=本地落盘对齐时刻（打包/还原完成时取当下）——脏判定用 mtime>settledAt，
            // 而非 mtime>savedAt：还原后文件 mtime=还原时刻，必然晚于云端 savedAt，用 savedAt
            // 比对会把刚还原的内容误判为本地新改动而触发无意义的反向上传。
            Directory.CreateDirectory(CloudDir);
            File.WriteAllText(LocalMetaPath, TideJson.ToJson(new LocalMeta
            {
                savedAt = savedAt,
                settledAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            }, false));
        }

        /// <summary>本地存档是否有上次打包/还原之后的改动（mtime 与 settledAt 比对；自动上传轮询用）。</summary>
        public static bool IsLocalDirty()
        {
            long settledAt = 0;
            try
            {
                if (File.Exists(LocalMetaPath))
                    settledAt = TideJson.FromJson<LocalMeta>(File.ReadAllText(LocalMetaPath))?.settledAt ?? 0;
            }
            catch { settledAt = 0; }

            foreach (string root in RootFiles)
            {
                string path = Path.Combine(TidePaths.PersistentDataPath, root);
                if (File.Exists(path) && new DateTimeOffset(File.GetLastWriteTimeUtc(path)).ToUnixTimeSeconds() > settledAt) return true;
            }
            if (Directory.Exists(CardDataPaths.CardDir))
            {
                foreach (string file in Directory.GetFiles(CardDataPaths.CardDir, "*.json"))
                    if (new DateTimeOffset(File.GetLastWriteTimeUtc(file)).ToUnixTimeSeconds() > settledAt) return true;
            }
            return false;
        }

        // ======================================== 处女档判定 ========================================

        /// <summary>
        /// 处女档=本机看不出任何玩家痕迹（无教学完成/进行中、无挑战战绩、无用户卡组、
        /// 自建卡表与种子一致）。仅此时允许免询问自动恢复云端——换机/重装场景一步到位；
        /// 有任何痕迹一律走询问（防误覆盖）。文件级直读（不经管理器，避免触发首次建档副作用）。
        /// </summary>
        public static bool IsPristineLocal()
        {
            try
            {
                var tp = ReadJsonQuiet<TutorialMirror>(Path.Combine(TidePaths.PersistentDataPath, "TutorialProgress.json"));
                if (tp?.completedTutorials != null && tp.completedTutorials.Count > 0) return false;
                if (tp != null && !string.IsNullOrEmpty(tp.currentTutorial)) return false;

                var cp = ReadJsonQuiet<ChallengeMirror>(Path.Combine(TidePaths.PersistentDataPath, "ChallengeProgress.json"));
                if (cp?.records != null && cp.records.Any(r => r != null && (r.wins > 0 || r.losses > 0))) return false;

                if (Directory.Exists(CardDataPaths.CardDir))
                {
                    foreach (string file in Directory.GetFiles(CardDataPaths.CardDir, "*.json"))
                    {
                        string name = Path.GetFileName(file);
                        if (name == "Cards.json" || name == "Effects.json") continue;
                        return false; // 存在任一用户卡组文件
                    }
                }

                // 自建卡/效果表与随包种子一致（空壳）——内容比较，防"只建过卡没建卡组"被误判处女档
                if (!MatchesSeed("Cards.json") || !MatchesSeed("Effects.json")) return false;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CloudSync] 处女档判定异常（按非处女处理，走询问）：{ex.Message}");
                return false;
            }
        }

        private static bool MatchesSeed(string fileName)
        {
            string local = Path.Combine(CardDataPaths.CardDir, fileName);
            if (!File.Exists(local)) return true; // 缺文件=种子引导未跑过或空环境，不构成玩家痕迹
            string seed = CardDataPaths.PackTextReader?.Invoke("CardData/" + fileName);
            if (string.IsNullOrEmpty(seed)) return true; // 取不到种子（宿主未装配）——无从比较，放行
            return File.ReadAllText(local) == seed;
        }

        private static T ReadJsonQuiet<T>(string path) where T : class
        {
            if (!File.Exists(path)) return null;
            try { return TideJson.FromJson<T>(File.ReadAllText(path)); }
            catch { return null; }
        }

        // ======================================== 数据形状 ========================================

        [Serializable]
        private class SaveBundle
        {
            public int schema;
            public long savedAt;
            public Dictionary<string, string> files;
        }

        [Serializable]
        private class LocalMeta
        {
            public long savedAt;   // 云端身份指纹（最近打包/还原的 savedAt）
            public long settledAt; // 本地落盘对齐时刻（脏判定基准；0=从未对齐）
        }

        // 私有数据类的字段镜像（Newtonsoft 按名映射；只取判定所需字段）
        [Serializable]
        private class TutorialMirror
        {
            public List<string> completedTutorials;
            public string currentTutorial;
        }

        [Serializable]
        private class ChallengeMirror
        {
            public List<ChallengeRowMirror> records;
        }

        [Serializable]
        private class ChallengeRowMirror
        {
            public int wins;
            public int losses;
        }
    }
}
