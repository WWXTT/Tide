// 热更管线（HybridCLR+YooAsset）解除期：由 TIDE_HYBRID_YOO 宏整体启停；恢复=开宏+HybridCLR enable+补包三步（见 查缺补漏.md「待办总账」第 1 条；编辑器菜单 Tools/热更/1-5）
#if TIDE_HYBRID_YOO
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HybridCLR.Editor;
using HybridCLR.Editor.Commands;
using HybridCLR.Editor.Settings;
using Tide.Launch;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using YooAsset.Editor;

namespace Tide.HotUpdateTools
{
    // Launch 类与 Tide.Launch 命名空间同名：外层命名空间成员会把裸 Launch 解析成命名空间，
    // 别名必须写在命名空间声明内才能解析到类。
    using Launch = Tide.Launch.Launch;

    /// <summary>
    /// 热更管线工具。首次接入按菜单编号 1→5 顺序执行一次；
    /// 之后改热更代码只需重跑第 4 步。
    /// </summary>
    public static class HotUpdateSetupTool
    {
        private const string HotDllFolder = "Assets/HotUpdateDlls";
        private const string AotDllFolder = "Assets/AotDlls";
        private const string LaunchScenePath = "Assets/Scenes/Launch.unity";

        [MenuItem("Tools/热更/1. 配置 HybridCLR 程序集")]
        public static void SetupHybridClr()
        {
            var settings = HybridCLRSettings.LoadOrCreate();
            settings.enable = true; // 2026-10-18 热更管线恢复：处理流程整体打开
            // 2026-10-24 TapTap 预编译隔离：第二热更程序集 Tide.TapTap（源码目录在=联机风味全量；
            // 本地单机风味源码被藏（~ 目录）=只列主程序集。风味切换菜单在 YooPackBuilder 同步改写此处）
            settings.hotUpdateAssemblies = Directory.Exists("Assets/Scripts/TapTap")
                ? new[] { HotUpdateLoader.HotAssemblyName, HotUpdateLoader.OptionalTapAssemblyName }
                : new[] { HotUpdateLoader.HotAssemblyName };
            settings.patchAOTAssemblies = HotUpdateLoader.PatchAotAssemblies;
            HybridCLRSettings.Save();
            AssetDatabase.SaveAssets();
            Debug.Log($"[热更配置] HybridCLR：hotUpdateAssemblies=[{string.Join(",", settings.hotUpdateAssemblies)}] patchAOTAssemblies=[{string.Join(",", HotUpdateLoader.PatchAotAssemblies)}]");

            if (!Directory.Exists(SettingsUtil.LocalIl2CppDir))
                Debug.LogWarning("[热更配置] HybridCLR 本地 il2cpp 未安装：请执行菜单 HybridCLR/Installer → Install（打包前置与真机构建必需）");
        }

        [MenuItem("Tools/热更/2. 配置 YooAsset 资源收集器")]
        public static void SetupCollector()
        {
            var setting = BundleCollectorSettingData.Setting;
            var package = setting.Packages.FirstOrDefault(p => p.PackageName == Launch.PackageName);
            if (package == null)
            {
                package = new BundleCollectorPackage { PackageName = Launch.PackageName, PackageDesc = "Tide 默认资源包" };
                setting.Packages.Add(package);
            }

            // 启动/热更代码全部按地址寻址（"Main"、"Tide.HotUpdate.dll"…），
            // Addressable 关闭时清单 Address 字段为空、只能按完整资源路径定位
            package.EnableAddressable = true;

            // 全量重建组：存量配置的路径/规则/收集类型变更（如 Art 拆分）靠重跑本菜单追平
            package.Groups.Clear();
            AddGroup(package, "HotUpdateDll", HotDllFolder);
            AddGroup(package, "AotDll", AotDllFolder);
            AddGroup(package, "Scene", "Assets/Scenes", "CollectAllExceptLaunch");
            AddGroup(package, "Configs", "Assets/Configs");
            AddGroup(package, "CardData", "Assets/CardData");
            AddGroup(package, "TutorialData", "Assets/TutorialData");
            AddGroup(package, "AIModel", "Assets/Art/AIModels");
            // Assets/Art 运行时资产（prefab/贴图/材质/shader/vfx）：白名单过滤，模型库与 AIModels 子树出局（各有专组）；
            // 相对路径寻址（去 Assets/ 前缀）——BattleHex 网格/预制体等同名异扩展在文件名寻址下撞址（2026-10-18 审计）
            AddGroup(package, "ArtRuntime", "Assets/Art", "CollectRuntimeArt",
                ECollectorType.MainAssetCollector, "AddressByRelativePath");
            // 模型库：专用寻址（prefab=名、png=名#preview、_manifest.txt=#names）+ 完整性过滤（残缺目录整组出局）
            AddGroup(package, "MonsterRig", "Assets/Art/YGOMonstersRig", "CollectRigComplete",
                ECollectorType.MainAssetCollector, "RigModelAddress");

            BundleCollectorSettingData.SaveFile();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[热更配置] YooAsset 收集器：DefaultPackage（Addressable）← HotUpdateDll/AotDll/Scene/Configs/CardData/TutorialData/AIModel/ArtRuntime/MonsterRig 九组（2026-10-18 热更恢复+数据迁移；规则见 YooCollectRules，ArtRuntime=完整路径寻址防同名撞址）");
        }

        [MenuItem("Tools/热更/3. 生成 Launch 场景")]
        public static void CreateLaunchScene()
        {
            EnsureFolder("Assets/Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var launchGo = new GameObject("Launch");
            launchGo.AddComponent<Launch>();

            var camGo = new GameObject("Camera");
            var camera = camGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.orthographic = true;

            EditorSceneManager.SaveScene(scene, LaunchScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(LaunchScenePath, true) };
            Debug.Log($"[热更配置] Launch 场景已生成并设为唯一构建场景（{LaunchScenePath}）；Main 场景改由 YooAsset 打包");
        }

        [MenuItem("Tools/热更/4. 编译热更DLL并拷入Assets")]
        public static void CompileHotUpdateDlls()
        {
            CompileDllCommand.CompileDllActiveBuildTargetRelease();

            var target = EditorUserBuildSettings.activeBuildTarget;
            string hotSrcDir = Path.Combine(SettingsUtil.HotUpdateDllsRootOutputDir, target.ToString());
            string hotSrc = Path.Combine(hotSrcDir, HotUpdateLoader.HotAssemblyName + ".dll");
            if (!File.Exists(hotSrc))
            {
                Debug.LogError($"[热更编译] 未找到编译产物：{hotSrc}");
                return;
            }
            CopyAsBytes(hotSrc, HotDllFolder, HotUpdateLoader.HotAssemblyName + ".dll.bytes");

            // 可选热更程序集（2026-10-24 TapTap 预编译隔离）：产出即拷（联机风味）；
            // 未产出（本地单机风味——源码被藏/SDK 被摘）时下方的陈旧清扫会删掉旧 .bytes，
            // 防止单机包经 HotUpdateDll 组带出上一轮的 TapTap DLL。
            string tapSrc = Path.Combine(hotSrcDir, HotUpdateLoader.OptionalTapAssemblyName + ".dll");
            bool tapProduced = File.Exists(tapSrc);
            if (tapProduced)
                CopyAsBytes(tapSrc, HotDllFolder, HotUpdateLoader.OptionalTapAssemblyName + ".dll.bytes");

            // 陈旧清扫：HotUpdateDll 目录里凡不在本轮预期集的 .dll.bytes 一律删
            //（单机风味删 Tide.TapTap.dll.bytes；退役/改名程序集残影同防）
            var expected = new HashSet<string> { HotUpdateLoader.HotAssemblyName + ".dll.bytes" };
            if (tapProduced) expected.Add(HotUpdateLoader.OptionalTapAssemblyName + ".dll.bytes");
            foreach (string bytesFile in Directory.GetFiles(HotDllFolder, "*.dll.bytes"))
            {
                string fileName = Path.GetFileName(bytesFile);
                if (expected.Contains(fileName)) continue;
                AssetDatabase.DeleteAsset(HotDllFolder + "/" + fileName);
                Debug.Log($"[热更编译] 清理陈旧热更 DLL：{fileName}（不在本轮产出集）");
            }

            string aotSrcDir = Path.Combine(SettingsUtil.AssembliesPostIl2CppStripDir, target.ToString());
            foreach (string aotName in HotUpdateLoader.PatchAotAssemblies)
            {
                string aotSrc = Path.Combine(aotSrcDir, aotName + ".dll");
                if (File.Exists(aotSrc))
                    CopyAsBytes(aotSrc, AotDllFolder, aotName + ".dll.bytes");
                else
                    Debug.LogWarning($"[热更编译] 裁剪后 AOT 程序集缺失：{aotSrc}（先跑一次第 5 步或一次真机构建）");
            }

            AssetDatabase.Refresh();
            Debug.Log($"[热更编译] 完成（{target}）：热更 DLL → {HotDllFolder}，AOT 补充 → {AotDllFolder}");
        }

        [MenuItem("Tools/热更/5. 打包前置生成（AOT泛型补充/link.xml）")]
        public static void PrebuildGenerate()
        {
            if (!Directory.Exists(SettingsUtil.LocalIl2CppDir))
            {
                Debug.LogError("[热更前置] HybridCLR 本地 il2cpp 未安装，请先执行菜单 HybridCLR/Installer → Install");
                return;
            }
            PrebuildCommand.GenerateAll();
            Debug.Log("[热更前置] GenerateAll 完成（生成后请重跑第 4 步拷贝 AOT DLL）");
        }

        private static void AddGroup(BundleCollectorPackage package, string groupName, string collectPath, string filterRuleName = "CollectAll", ECollectorType collectorType = ECollectorType.MainAssetCollector, string addressRuleName = "AddressByFileName")
        {
            EnsureFolder(collectPath);
            var group = new BundleCollectorGroup { GroupName = groupName, GroupDesc = groupName };
            group.Collectors.Add(new BundleCollector
            {
                CollectPath = collectPath,
                CollectorGUID = AssetDatabase.AssetPathToGUID(collectPath),
                CollectorType = collectorType,
                AddressRuleName = addressRuleName,
                PackRuleName = "PackDirectory",
                FilterRuleName = filterRuleName,
            });
            package.Groups.Add(group);
        }

        private static void CopyAsBytes(string srcFile, string destFolder, string destFileName)
        {
            EnsureFolder(destFolder);
            File.Copy(srcFile, Path.Combine(destFolder, destFileName), overwrite: true);
        }

        private static void EnsureFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder))
                return;
            string[] parts = assetFolder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif
