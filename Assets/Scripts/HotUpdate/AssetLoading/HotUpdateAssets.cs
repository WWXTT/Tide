using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Tide.HotUpdate
{
    /// <summary>
    /// 热更侧统一资产加载器（2026-09-29 Art/Configs/UI/Scripts 热更区划分配套）。
    /// 三级回落（镜像 OnnxTidePolicy.LoadModelAsset 先例）：
    ///   ① YooAsset 热更包——Launch 流程/真机的唯一来源；
    ///   ② 编辑器 AssetDatabase / File 直读——直启 Main 的开发流程（YooAsset 未初始化）；
    ///   ③ null——调用方负责报错。
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
                    var handle = package.LoadAssetAsync<T>(address);
                    handle.WaitForAsyncComplete();
                    if (handle.Status == YooAsset.EOperationStatus.Succeeded
                        && handle.AssetObject is T viaYoo)
                    {
                        _keepAlive.Add(handle);
                        return viaYoo;
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
            return UnityEditor.AssetDatabase.LoadAssetAtPath<T>(editorAssetPath);
#else
            return null;
#endif
        }

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
