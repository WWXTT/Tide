using System;
using System.Linq;
using System.Reflection;
#if !UNITY_EDITOR && !UNITY_WEBGL
using Cysharp.Threading.Tasks;
using HybridCLR;
using YooAsset;
#endif
using UnityEngine;

namespace Tide.Launch
{
    /// <summary>
    /// 热更程序集加载。
    /// 编辑器：Tide.HotUpdate 由 Unity 编译，直接反射调用入口；
    /// 真机：先补 AOT 泛型元数据，再 Assembly.Load 热更 DLL。
    /// </summary>
    public static class HotUpdateLoader
    {
        /// <summary>
        /// 需要补 AOT 泛型元数据的程序集列表。
        /// 必须与编辑器工具写入 HybridCLRSettings.patchAOTAssemblies 的列表一致（HotUpdateSetupTool 同步读取此处）。
        /// </summary>
        public static readonly string[] PatchAotAssemblies = { "mscorlib", "System", "System.Core", "netstandard" };

        public const string HotAssemblyName = "Tide.HotUpdate";

        /// <summary>资源寻址地址（.dll.bytes 文件经 AddressByFileName 规则保留 .dll 段）</summary>
        public const string HotDllLocation = "Tide.HotUpdate.dll";

#if UNITY_EDITOR
        public static bool InvokeEditorEntry()
        {
            var assembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == HotAssemblyName);
            if (assembly == null)
            {
                PatchConsole.Error($"编辑器下未找到 {HotAssemblyName} 程序集，请检查 Assets/Scripts/HotUpdate/Tide.HotUpdate.asmdef");
                return false;
            }
            return InvokeEntry(assembly);
        }
#elif !UNITY_WEBGL
        public static async UniTask<bool> LoadDeviceAsync(ResourcePackage package)
        {
            // 1. AOT 泛型补充：缺失不致命（只影响未实例化过的泛型组合），逐个尝试
            foreach (string aotName in PatchAotAssemblies)
            {
                AssetHandle aotHandle = package.LoadAssetAsync<TextAsset>(aotName + ".dll");
                while (!aotHandle.IsDone)
                    await UniTask.Yield();
                if (aotHandle.Status != EOperationStatus.Succeeded)
                {
                    PatchConsole.Warn($"AOT 补充 {aotName} 未打包，跳过（迁移大量泛型代码前务必补齐）");
                    continue;
                }
                var err = RuntimeApi.LoadMetadataForAOTAssembly(((TextAsset)aotHandle.AssetObject).bytes, HomologousImageMode.SuperSet);
                if (err != LoadImageErrorCode.OK)
                    PatchConsole.Warn($"AOT 补充 {aotName} 加载警告：{err}");
                aotHandle.Dispose();
            }

            // 2. 加载热更程序集本体
            PatchConsole.Info("加载热更程序集…");
            AssetHandle dllHandle = package.LoadAssetAsync<TextAsset>(HotDllLocation);
            while (!dllHandle.IsDone)
                await UniTask.Yield();
            if (dllHandle.Status != EOperationStatus.Succeeded)
            {
                PatchConsole.Error($"热更 DLL 加载失败：{HotDllLocation}");
                return false;
            }
            Assembly hotAssembly = Assembly.Load(((TextAsset)dllHandle.AssetObject).bytes);
            dllHandle.Dispose();
            return InvokeEntry(hotAssembly);
        }
#endif

        private static bool InvokeEntry(Assembly assembly)
        {
            Type type = assembly.GetType("Tide.HotUpdate.HotUpdateEntry", throwOnError: false);
            MethodInfo method = type?.GetMethod("Start", BindingFlags.Public | BindingFlags.Static);
            if (method == null)
            {
                PatchConsole.Error($"未找到 {assembly.GetName().Name} 的 HotUpdateEntry.Start 入口");
                return false;
            }
            method.Invoke(null, null);
            return true;
        }
    }
}
