using System;
using System.Collections.Generic;
using System.IO;

namespace CardCore
{
    /// <summary>
    /// 效果库（2026-09-14 效果引用化 v2·大修）：Tide/Effects.json 单文件——**瘦格式**
    /// （EffectSlimDto：原子=表行 ID 引用+增量，见 Effects/EffectSlim.cs 头注）。
    /// 卡表（Cards.json）只存 effectIds 引用。效果 id = ContentHasher.HashEffect（单源化口径：
    /// AE 段仅 steps 空时计入）。旧内嵌格式（TestDecks）向后兼容。
    /// </summary>
    public static class EffectsLibrary
    {
        private const string ConfigRelative = "Card/Effects.json";
        // 教学独立效果表（2026-10-16 定案：教学效果不再与正式表混用；2026-10-17 分家：
        // 文件随教学卡挪至 StreamingAssets/Tutorial/）：同格式副表，
        // EnsureLoaded 时合并（重复 id 保正式表首条——与主表内重复 id 同口径）。
        private const string TutorialConfigRelative = "Tutorial/TutorialEffects.json";
        // 训练独立效果表（2026-11-03 训练池分家）：训练卡的 effectIds 引用在此解析——
        // 文件在 Assets/TrainingData，随开发仓库走（不进热更包、不下发玩家设备）。
        // 合并口径同教学副表：重复 id 保先装表（主表>教学>训练）。
        private const string TrainingConfigRelative = "TrainingData/TrainingEffects.json";

        [Serializable]
        private class Wrapper { public List<EffectSlimDto> items; }

        private static Dictionary<string, EffectSlimDto> _byId;
        private static List<EffectSlimDto> _items;

        // 缓存新鲜度记账（2026-10-21 破釜沉舟代价排查）：本库曾无任何失效检查——效果表直写
        // （手改 costs/锚价/教学副表）后旧静态缓存跨 Play 沿用（合成器 Reload 为唯一手动口），
        // 表现为「改了表却不生效」。与 CardCatalog 同盯 mtime；两文件**独立**记账（主/副表戳
        // 分开比对，避免「以主表戳比对副表」的恒过期缺陷）。高频入口按 2 秒节流防查盘 IO。
        private static string _mainPath;
        private static DateTime _mainStampUtc = DateTime.MinValue;
        private static DateTime _tutorialStampUtc = DateTime.MinValue;
        // 训练副表 mtime 独立记账（2026-11-03，同教学副表口径）：缺文件=玩家构建常态。
        private static DateTime _trainingStampUtc = DateTime.MinValue;
        private static DateTime _lastStaleCheckUtc = DateTime.MinValue;

        private static void EnsureLoaded()
        {
            if (_byId == null)
            {
                LoadAll();
                return;
            }
            if ((DateTime.UtcNow - _lastStaleCheckUtc).TotalSeconds < 2) return;
            _lastStaleCheckUtc = DateTime.UtcNow;
            if (CacheIsStale()) Reload();
        }

        /// <summary>缓存是否过期：主表消失/路径变/任一 mtime 变。读失败按未过期（读失败≠数据变）。</summary>
        private static bool CacheIsStale()
        {
            try
            {
                string path = SynergyUI.CardDataPaths.FileIn("Effects.json");
                if (path != _mainPath || !File.Exists(path)) return true;
                if (File.GetLastWriteTimeUtc(path) != _mainStampUtc) return true;
                string tpath = SynergyUI.CardDataPaths.TutorialFileIn("TutorialEffects.json");
                if (File.Exists(tpath) && File.GetLastWriteTimeUtc(tpath) != _tutorialStampUtc) return true;
                string rpath = SynergyUI.CardDataPaths.TrainingFileIn("TrainingEffects.json");
                if (File.Exists(rpath) && File.GetLastWriteTimeUtc(rpath) != _trainingStampUtc) return true;
                return false;
            }
            catch
            {
                return false;
            }
        }

        private static void LoadAll()
        {
            _byId = new Dictionary<string, EffectSlimDto>();
            _items = new List<EffectSlimDto>();
            try
            {
                // 路径收口（2026-09-24）：经 SynergyUI.CardDataPaths——编辑器 StreamingAssets / 玩家 persistentData
                SynergyUI.CardDataPaths.EnsureBootstrap();
                string path = SynergyUI.CardDataPaths.FileIn("Effects.json");
                _mainPath = path;
                _mainStampUtc = DateTime.MinValue; // 先记「未读到文件」态——文件出现后 mtime 必 ≠ MinValue，自动恢复
                if (!File.Exists(path))
                {
                    TideLog.Warn($"[EffectsLibrary] 效果库不存在: {path}（卡表 effectIds 引用将解析为空）");
                    return;
                }
                var wrapper = TideJson.FromJson<Wrapper>(File.ReadAllText(path));
                if (wrapper?.items != null)
                {
                    foreach (var it in wrapper.items)
                    {
                        if (it == null || string.IsNullOrEmpty(it.id)) continue;
                        if (_byId.ContainsKey(it.id)) continue; // 重复 id 保首条
                        _byId[it.id] = it;
                        _items.Add(it);
                    }
                }
                _mainStampUtc = File.GetLastWriteTimeUtc(path); // 解析成功才记账：坏 JSON 不落缓存态，下次调用重试
                MergeTutorialEffects(); // 教学副表合并（2026-10-16）
                MergeTrainingEffects(); // 训练副表合并（2026-11-03）：训练卡 effectIds 引用解析
            }
            catch (Exception e)
            {
                TideLog.Warn($"[EffectsLibrary] 加载 {ConfigRelative} 失败: {e.Message}");
            }
        }

        /// <summary>教学效果副表合并：重复 id 保正式表（先装主表，教学表写错可查）。
        /// 文件缺失=无教学效果，静默 Warn 兜底。</summary>
        private static void MergeTutorialEffects()
        {
            try
            {
                string tpath = SynergyUI.CardDataPaths.TutorialFileIn("TutorialEffects.json");
                if (!File.Exists(tpath))
                {
                    TideLog.Warn($"[EffectsLibrary] 教学效果表未找到: {tpath}");
                    return;
                }
                var wrapper = TideJson.FromJson<Wrapper>(File.ReadAllText(tpath));
                if (wrapper?.items != null)
                {
                    foreach (var it in wrapper.items)
                    {
                        if (it == null || string.IsNullOrEmpty(it.id)) continue;
                        if (_byId.ContainsKey(it.id)) continue; // 重复 id 保首条（正式表）
                        _byId[it.id] = it;
                        _items.Add(it);
                    }
                }
                _tutorialStampUtc = File.GetLastWriteTimeUtc(tpath); // 解析成功才记账（与主表同口径）
            }
            catch (Exception e)
            {
                TideLog.Warn($"[EffectsLibrary] 加载 {TutorialConfigRelative} 失败: {e.Message}");
            }
        }

        /// <summary>训练效果副表合并（2026-11-03 训练池分家）：重复 id 保先装表（主表>教学>训练）。
        /// 文件缺失=玩家构建常态（训练表不随包下发），静默返回——与教学表 Warn 兜底不同口径。</summary>
        private static void MergeTrainingEffects()
        {
            try
            {
                string rpath = SynergyUI.CardDataPaths.TrainingFileIn("TrainingEffects.json");
                if (!File.Exists(rpath)) return;
                var wrapper = TideJson.FromJson<Wrapper>(File.ReadAllText(rpath));
                if (wrapper?.items != null)
                {
                    foreach (var it in wrapper.items)
                    {
                        if (it == null || string.IsNullOrEmpty(it.id)) continue;
                        if (_byId.ContainsKey(it.id)) continue; // 重复 id 保首条（先装表）
                        _byId[it.id] = it;
                        _items.Add(it);
                    }
                }
                _trainingStampUtc = File.GetLastWriteTimeUtc(rpath); // 解析成功才记账（与主表同口径）
            }
            catch (Exception e)
            {
                TideLog.Warn($"[EffectsLibrary] 加载 {TrainingConfigRelative} 失败: {e.Message}");
            }
        }

        /// <summary>强制重读（合成器「读取效果表」按钮用）。</summary>
        public static void Reload()
        {
            _byId = null;
            _items = null;
            EnsureLoaded();
        }

        /// <summary>全部效果条目（按文件序）。</summary>
        public static List<EffectSlimDto> GetAll()
        {
            EnsureLoaded();
            return _items;
        }

        /// <summary>按 id 取条目（无则 null）。</summary>
        public static EffectSlimDto Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            EnsureLoaded();
            return _byId.TryGetValue(id, out var it) ? it : null;
        }

        /// <summary>按 id 解析出卡内效果（瘦 DTO → CardEffectData；原子引用逐个还原，缺行告警剔除）。
        /// 缺行告警**按 id 去重**（2026-11-05 日志降噪）：卡表批量装载会重复解析同一批卡，
        /// 旧口径每次刷一行，一次回归上千行噪声；同 id 只报首次。</summary>
        public static CardEffectData Resolve(string id)
        {
            var it = Get(id);
            if (it == null)
            {
                if (_warnedMissing.Add(id ?? ""))
                    TideLog.Warn($"[EffectsLibrary] 效果引用缺失: {id}（Effects.json 无此条）");
                return null;
            }
            return EffectSlim.ToCardEffect(it);
        }

        /// <summary>缺行告警去重账（进程级；同 id 只报首次）。</summary>
        private static readonly HashSet<string> _warnedMissing = new HashSet<string>();
    }
}
