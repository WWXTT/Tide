using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Tide.HotUpdate
{
    /// <summary>
    /// 热更侧统一资产加载器（2026-09-29 Art/Configs/UI/Scripts 热更区划分配套）。
    /// 档位序（前者优先）：
    ///   ① YooAsset 热更包（TIDE_HYBRID_YOO 未定义时整档编译掉）——Launch 流程/真机主源
    ///     （2026-10-18 打包换装：替代原 StreamingAssets 内置包档，收集器见 Tools/热更/2）；
    ///   ② 编辑器 AssetDatabase——直启 Main 的开发流程（即改即见）；
    ///   ③ null——调用方负责报错/占位。
    /// 句柄保活：成功路径的 AssetHandle 缓存不 Dispose（资产生命周期=进程，域重载自然回收）。
    /// WebGL：WaitForAsyncComplete 不可用，WebPlayMode 需改异步——WebGL 属后续里程碑。
    /// </summary>
    public static class HotUpdateAssets
    {
        /// <summary>资源包名。与 Tide.Launch.Launch.PackageName 同值——Launch 编入
        /// Assembly-CSharp，热更程序集无法反向引用只能双源常量：改包名两处一起改。</summary>
        public const string DefaultPackageName = "DefaultPackage";

#if TIDE_HYBRID_YOO
        private static readonly List<YooAsset.AssetHandle> _keepAlive = new List<YooAsset.AssetHandle>();
#endif

        /// <summary>按 YooAsset 地址加载资产（AddressByFileName=文件名不含扩展名）。
        /// editorAssetPath 为编辑器兜底的完整资产路径（"Assets/..."），仅编辑器生效。</summary>
        public static T Load<T>(string address, string editorAssetPath) where T : Object
        {
#if TIDE_HYBRID_YOO
            // YooAsset：包未初始化/收集器未配置时抛异常或返回失败句柄，捕获后回落，
            // 不阻断编辑器直启流程（同 OnnxTidePolicy 口径）。
            try
            {
                YooAsset.ResourcePackage package;
                if (YooAsset.YooAssets.TryGetPackage(DefaultPackageName, out package))
                {
                    // 双址试载（2026-10-18 撞名审计定案）：ArtRuntime 组=相对路径寻址（去 Assets/ 前缀，
                    // BattleHex 网格/预制体等同名异扩展防撞——双源变换：editor 侧 YooCollectRules.AddressByRelativePath），
                    // 其余组=文件名寻址——先相对路径再 address
                    T viaYoo = LoadViaYoo<T>(package, StripAssetsPrefix(editorAssetPath)) ?? LoadViaYoo<T>(package, address);
                    if (viaYoo != null) return viaYoo;
                }
            }
            catch (System.Exception)
            {
                // 未配置 YooAsset（编辑器直启 Main），走兜底
            }
#endif

#if UNITY_EDITOR
            var viaEditor = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(editorAssetPath);
            if (viaEditor != null) return viaEditor; // 编辑器保 prefab/贴图即改即见
#endif
            return null; // 调用方负责报错/占位（真机未走 Launch 流程=打包缺失，重跑 Tools/打包/YooAsset资源打包）
        }

#if TIDE_HYBRID_YOO
        /// <summary>Assets/ 前缀剥离（双源变换：editor 侧 YooCollectRules.AddressByRelativePath）。</summary>
        private static string StripAssetsPrefix(string assetPath)
        {
            return assetPath != null && assetPath.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase)
                ? assetPath.Substring("Assets/".Length)
                : assetPath;
        }

        /// <summary>单址试载：地址不在包内先静默返回 null（IsLocationValid 不落日志；直接
        /// LoadAssetAsync 会对无效地址打内部 LogError——双址试载第一发 miss 属常态，如
        /// TutorialAudio 的文件名寻址组试相对路径）；失败返回 null 并释放句柄；
        /// 成功句柄进 _keepAlive（生命周期=进程）。</summary>
        private static T LoadViaYoo<T>(YooAsset.ResourcePackage package, string location) where T : Object
        {
            if (!package.IsLocationValid(location)) return null;
            var handle = package.LoadAssetAsync<T>(location);
            handle.WaitForAsyncComplete();
            if (handle.Status == YooAsset.EOperationStatus.Succeeded
                && handle.AssetObject is T asset)
            {
                _keepAlive.Add(handle);
                return asset;
            }
            handle.Dispose();
            return null;
        }
#endif

        /// <summary>文本配置（Configs）加载：YooAsset TextAsset → 编辑器 File 直读。
        /// editorJsonRelativePath 为 dataPath 相对路径（如 "Configs/AttributeValueConfig.json"）；
        /// 不走资产缓存，保持"直改 JSON 后 Reload 免重启"的直读语义。</summary>
        public static string LoadText(string address, string editorJsonRelativePath)
        {
#if TIDE_HYBRID_YOO
            try
            {
                YooAsset.ResourcePackage package;
                if (YooAsset.YooAssets.TryGetPackage(DefaultPackageName, out package))
                {
                    var handle = package.LoadAssetAsync<TextAsset>(address);
                    handle.WaitForAsyncComplete();
                    if (handle.Status == YooAsset.EOperationStatus.Succeeded
                        && handle.AssetObject is TextAsset viaYoo)
                    {
                        _keepAlive.Add(handle);
                        return viaYoo.text;
                    }
                    handle.Dispose();
                }
            }
            catch (System.Exception)
            {
                // 未配置 YooAsset（编辑器直启 Main），走兜底
            }
#endif

#if UNITY_EDITOR
            string path = Path.Combine(Application.dataPath, editorJsonRelativePath);
            return File.Exists(path) ? File.ReadAllText(path) : null;
#else
            return null;
#endif
        }
    }
}
