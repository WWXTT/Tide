// 热更管线（HybridCLR+YooAsset）解除期：由 TIDE_HYBRID_YOO 宏整体启停；恢复=开宏+HybridCLR enable+补包三步（见 查缺补漏.md「待办总账」第 1 条；编辑器菜单 Tools/热更/1-5）
#if TIDE_HYBRID_YOO
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
        /// 2026-10-24 移除 netstandard：工程 API 档位=.NET Framework，il2cpp 裁剪产物里根本没有
        /// netstandard 门面 dll（拷贝步骤 File.Exists 恒 miss，真机端只落一条 YooAsset 内部 LogError
        /// + 兜底 Warn）——四件套原本照 HybridCLR 文档（.NET Standard 档示例）抄来，本档位补它无意义。
        /// 若未来切 .NET Standard 2.1 档再把它加回（届时裁剪产物会自动产出）。
        /// </summary>
        public static readonly string[] PatchAotAssemblies = { "mscorlib", "System", "System.Core" };

        public const string HotAssemblyName = "Tide.HotUpdate";

        /// <summary>
        /// 可选热更程序集（2026-10-24 TapTap 预编译隔离）：联机（TapTap）风味随包打进、
        /// 在主程序集之后加载（Tide.TapTap 引用 Tide.HotUpdate，先装依赖再装本体；只装域——
        /// HotUpdateEntry 里 TapBridge 扫域发现 TapModule）。本地单机风味不打包=缺席，
        /// IsLocationValid 探测后静默跳过——联机/云存档/登录功能整体退位，玩法不受影响。
        /// </summary>
        public const string OptionalTapAssemblyName = "Tide.TapTap";

        // AOT 泛型预实例化兜底（2026-10-18 真机实录）：热更侧 BattleScreen.ShowTipPromptAsync 为
        // async UniTask<bool?>，HybridCLR 泛型分析器对 AsyncUniTaskMethodBuilder<Nullable<bool>> 漏扫
        // （AOTGenericReferences 同方法只记出 Nullable<byte>），真机点投降弹 MissingMethodException。
        // AOT 侧显式引用一次强制 il2cpp 生成该组合；若日后分析器命中，此行属无害冗余，勿删。
        // 二次实录（同日 21:20 教学一守卫窗口）：UniTask<引用类型>（ResponseOption 响应窗口弹窗、
        // List<int> Region 选卡）在 AOT 上统一解析到 <object> 共享形态，而 AOT 侧没有任何
        // async UniTask<引用类型> 时该共享形态不生成——首个此类调用真机 MissingMethodException，
        // 响应窗弹不出（无「不连锁」按钮、守卫恒灰、AI 回合中断）。<object> 一行覆盖全族引用类型；
        // int/int? 为值类型不共享须逐组合补。UniTaskCompletionSource<引用类型>（守卫 _guardTcs）同规则。
        static HotUpdateLoader()
        {
            _ = Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<bool?>.Create();
            _ = Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<int>.Create();
            _ = Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<int?>.Create();
            _ = Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<object>.Create();
            // (string,string) 元组（2026-10-24 木桩面板缺陷排查）：DeckPickStageAsync/PickAsync 为
            // async UniTask<(string,string)>——ValueTuple 是值类型不共享，此前无锚、分析器清单亦未记
            //（旧包可用疑为 il2cpp 隐式覆盖，无显式保障），开局段四必经路径显式锚定兜底（无害冗余原则同上）。
            _ = Cysharp.Threading.Tasks.CompilerServices.AsyncUniTaskMethodBuilder<(string, string)>.Create();
            _ = new Cysharp.Threading.Tasks.UniTaskCompletionSource<object>();
            // AOT 类型保留锚（2026-10-24 Local 风味 TypeLoad 实录）：热更侧 PrivacyConsent 等用 PlayerPrefs，
            // Local 风味摘 TapSDK 后 AOT 闭包无引擎类型引用则被裁（真机 TypeLoadException 卡启动屏）——
            // 此行显式引用兜底，与 stripEngineCode=0 双保险。
            _ = UnityEngine.PlayerPrefs.HasKey("tide.aot.type.anchor");
        }

        /// <summary>资源寻址地址（.dll.bytes 文件经 AddressByFileName 规则保留 .dll 段）</summary>
        public const string HotDllLocation = "Tide.HotUpdate.dll";

        /// <summary>可选热更程序集地址（Tide.TapTap.dll.bytes，同上寻址规则）。</summary>
        public const string TapDllLocation = "Tide.TapTap.dll";

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
                // 前置静默探测（2026-10-24）：地址不在包内直接跳过——IsLocationValid 不落日志，
                // 直接 LoadAssetAsync 会对无效地址打内部 LogError（同 HotUpdateAssets.LoadViaYoo 口径，
                // 防热更/4 拷贝空窗期等缺件场景再产噪音）
                if (!package.IsLocationValid(aotName + ".dll"))
                {
                    PatchConsole.Warn($"AOT 补充 {aotName} 未入包，跳过（迁移大量泛型代码前务必补齐）");
                    continue;
                }
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

            // 2.5 可选热更程序集（Tide.TapTap）：联机风味加载（供 TapBridge 扫域安装），
            // 单机风味不打包=缺席静默跳过。地址探测用 IsLocationValid（防无效地址内部 LogError）
            if (package.IsLocationValid(TapDllLocation))
            {
                AssetHandle tapHandle = package.LoadAssetAsync<TextAsset>(TapDllLocation);
                while (!tapHandle.IsDone)
                    await UniTask.Yield();
                if (tapHandle.Status == EOperationStatus.Succeeded)
                {
                    Assembly.Load(((TextAsset)tapHandle.AssetObject).bytes);
                    PatchConsole.Info("可选热更程序集 Tide.TapTap 已加载（TapTap 联机风味）");
                }
                else
                {
                    PatchConsole.Warn("Tide.TapTap.dll 在包内但加载失败——联机/云存档将缺席");
                }
                tapHandle.Dispose();
            }
            else
            {
                PatchConsole.Info("Tide.TapTap 未打包（本地单机风味）——TapTap 登录/云存档/联机面板缺席");
            }

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
#endif
