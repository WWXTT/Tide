using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tide.HotUpdate
{
    /// <summary>
    /// YGOMonstersRig 资源加载器（2026-10-18 打包换装：StreamingAssets 专包 → YooAsset MonsterRig 组）：
    /// 寻址约定不变（打包侧规则 YooCollectRules.RigModelAddress）——prefab=模型名、预览图=名#preview
    /// （多切片取 rect 面积最大者，2026-10-16 定案）、名单 TextAsset=#names。真机数据源=Yoo 热更包
    /// （TIDE_HYBRID_YOO 未定义时整档编译掉）；编辑器兜底 AssetDatabase 直读源目录（改模型即所见，
    /// 免重打包——真机内容以包为准，发布前须重跑 Tools/打包/YooAsset资源打包 对齐）。
    /// 失败不静默：真机名单空打 Error；调用方回落占位（MonsterModelResolver 占位胶囊/卡面占位图）。
    /// </summary>
    public static class MonsterRigAssets
    {
        private const string RigRootEditor = "Assets/Art/YGOMonstersRig"; // 编辑器兜底直读根（同 editor 工具）
        /// <summary>名单地址（双源：editor 规则 RigModelAddress——改一处须两处一起改）</summary>
        public const string NamesAddress = "#names";
        /// <summary>预览图地址后缀（双源：editor 规则 RigModelAddress）</summary>
        public const string PreviewSuffix = "#preview";

#if TIDE_HYBRID_YOO
        private static readonly List<YooAsset.AssetHandle> _keepAlive = new List<YooAsset.AssetHandle>();
        private static readonly Dictionary<string, YooAsset.SubAssetsHandle> _previewHandles
            = new Dictionary<string, YooAsset.SubAssetsHandle>();
#endif

        private static string[] _names;
        private static readonly Dictionary<string, Sprite> _previewCache = new Dictionary<string, Sprite>();

        /// <summary>模型 prefab；null=未命中，调用方回落占位。</summary>
        public static GameObject LoadPrefab(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
#if TIDE_HYBRID_YOO
            try
            {
                if (YooAsset.YooAssets.TryGetPackage(HotUpdateAssets.DefaultPackageName, out var package))
                {
                    // 无效地址静默探测（2026-10-21 终根因：同名档拿卡名当地址——未命中属常态，
                    // 直接 LoadAssetAsync 会触发 Yoo 内部 LogError（真机仅噪音；编辑器离线包
                    // 虚拟句柄 WaitForAsyncComplete 直接炸断单位对账链——段零召唤物全没的根因）。
                    // IsLocationValid 前置=HotUpdateAssets.LoadViaYoo 同款口径）。
                    if (!package.IsLocationValid(name)) return null;
                    var handle = package.LoadAssetAsync<GameObject>(name);
                    handle.WaitForAsyncComplete();
                    if (handle.Status == YooAsset.EOperationStatus.Succeeded
                        && handle.AssetObject is GameObject viaYoo)
                    {
                        _keepAlive.Add(handle);
                        return viaYoo;
                    }
                    handle.Dispose();
                }
            }
            catch (Exception)
            {
                // 包未初始化（编辑器直启 Main），走兜底
            }
#endif
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                RigRootEditor + "/" + name + "/" + name + ".prefab");
#else
            return null;
#endif
        }

        /// <summary>预览图子精灵（Multiple 模式）：取 rect 面积最大者。2026-10-16 定案（用户：
        /// 「下有 _0/_1 类似情况一律显示 _0」）：多切片表的其余切片是自动切片碎片（8×8 级），
        /// "首个 Sprite"依赖 API 未定义的返回序不可靠；且 17 张多切片表中 GreenPhantomKing/Tyhone/
        /// WingsOfWickedFlame 的 _0 本身是碎片、真图在 _1/_2，按面积取恰好=其余 14 表的 _0 与这 3 表的真图。
        /// null=未命中。结果按名缓存（含失败，防逐卡重试）。</summary>
        public static Sprite LoadPreviewSprite(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (_previewCache.TryGetValue(name, out var cached)) return cached;
            Sprite best = null;
#if TIDE_HYBRID_YOO
            try
            {
                if (YooAsset.YooAssets.TryGetPackage(HotUpdateAssets.DefaultPackageName, out var package))
                {
                    // 无效地址静默探测（同 LoadPrefab：预览地址未命中属常态，勿触发 Yoo LogError）
                    if (!package.IsLocationValid(name + PreviewSuffix)) { _previewCache[name] = null; return null; }
                    var handle = package.LoadSubAssetsAsync<Sprite>(name + PreviewSuffix);
                    handle.WaitForAsyncComplete();
                    if (handle.Status == YooAsset.EOperationStatus.Succeeded)
                    {
                        _previewHandles[name] = handle; // 句柄保活（子资产生命周期=进程）
                        long bestArea = -1;
                        foreach (var s in handle.GetSubAssetObjects<Sprite>())
                        {
                            var area = (long)s.rect.width * (long)s.rect.height;
                            if (area <= bestArea) continue;
                            bestArea = area;
                            best = s;
                        }
                    }
                    else
                        handle.Dispose();
                }
            }
            catch (Exception)
            {
                // 包未初始化（编辑器直启 Main），走兜底
            }
#endif
#if UNITY_EDITOR
            if (best == null)
            {
                long bestArea = -1;
                foreach (var asset in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(
                    RigRootEditor + "/" + name + "/" + name + ".png"))
                    if (asset is Sprite sprite)
                    {
                        var area = (long)sprite.rect.width * (long)sprite.rect.height;
                        if (area > bestArea) { bestArea = area; best = sprite; }
                    }
            }
#endif
            _previewCache[name] = best;
            return best;
        }

        /// <summary>模型程序动画目录配置文本（Configs 组 TextAsset，地址=文件名；
        /// MonsterRigCatalog 在真机的数据源）。null=包缺失或未收录（json 现由 git 托管，
        /// 见 MonsterRigCatalog.ConfigRelativePath）。</summary>
        public static string CatalogText()
            => HotUpdateAssets.LoadText(
                "MonsterRigCatalog", "Configs/MonsterRigCatalog.json");

        /// <summary>可选模型名（=prefab 名=源目录名，Ordinal 排序）。打包侧只收 prefab+预览图齐全的目录，
        /// 名单即全部可选。返回的数组为共享实例，调用方勿改写。</summary>
        public static string[] Names()
        {
            if (_names != null) return _names;
            var list = new List<string>();
#if TIDE_HYBRID_YOO
            try
            {
                if (YooAsset.YooAssets.TryGetPackage(HotUpdateAssets.DefaultPackageName, out var package))
                {
                    // 无效地址静默探测（同 LoadPrefab：包内无 #names 时只报自家友好 Error，勿叠 Yoo LogError）
                    if (package.IsLocationValid(NamesAddress))
                    {
                        var handle = package.LoadAssetAsync<TextAsset>(NamesAddress);
                        handle.WaitForAsyncComplete();
                        if (handle.Status == YooAsset.EOperationStatus.Succeeded && handle.AssetObject is TextAsset ta)
                        {
                            _keepAlive.Add(handle);
                            foreach (var line in ta.text.Split('\n'))
                            {
                                var n = line.Trim();
                                if (n.Length > 0) list.Add(n);
                            }
                        }
                        else
                        {
                            handle.Dispose();
                        }
                    }
                    if (list.Count == 0)
                        Debug.LogError("[MonsterRigAssets] 包内名单缺失（地址 " + NamesAddress + "）——重跑 Tools/打包/YooAsset资源打包");
                }
            }
            catch (Exception)
            {
                // 包未初始化（编辑器直启 Main），走兜底
            }
#endif
#if UNITY_EDITOR
            if (list.Count == 0)
            {
                // 兜底直扫源目录（同打包器口径：prefab+同名png 齐全才算）
                foreach (var dir in System.IO.Directory.GetDirectories(RigRootEditor))
                {
                    var n = System.IO.Path.GetFileName(dir);
                    if (!System.IO.File.Exists(RigRootEditor + "/" + n + "/" + n + ".prefab")) continue;
                    if (!System.IO.File.Exists(RigRootEditor + "/" + n + "/" + n + ".png")) continue;
                    list.Add(n);
                }
            }
#endif
            list.Sort(StringComparer.Ordinal);
            _names = list.ToArray();
            if (_names.Length == 0)
                Debug.LogError("[MonsterRigAssets] 怪兽模型名单为空（包未打包或为空）");
            return _names;
        }
    }
}
