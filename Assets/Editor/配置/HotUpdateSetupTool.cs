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
        private const string LaunchScenePath = "Assets/Launch/Launch.unity";

        [MenuItem("Tide/热更/1. 配置 HybridCLR 程序集")]
        public static void SetupHybridClr()
        {
            var settings = HybridCLRSettings.LoadOrCreate();
            settings.hotUpdateAssemblies = new[] { HotUpdateLoader.HotAssemblyName };
            settings.patchAOTAssemblies = HotUpdateLoader.PatchAotAssemblies;
            HybridCLRSettings.Save();
            AssetDatabase.SaveAssets();
            Debug.Log($"[热更配置] HybridCLR：hotUpdateAssemblies=[{HotUpdateLoader.HotAssemblyName}] patchAOTAssemblies=[{string.Join(",", HotUpdateLoader.PatchAotAssemblies)}]");

            if (!Directory.Exists(SettingsUtil.LocalIl2CppDir))
                Debug.LogWarning("[热更配置] HybridCLR 本地 il2cpp 未安装：请执行菜单 HybridCLR/Installer → Install（打包前置与真机构建必需）");
        }

        [MenuItem("Tide/热更/2. 配置 YooAsset 资源收集器")]
        public static void SetupCollector()
        {
            var setting = BundleCollectorSettingData.Setting;
            var package = setting.Packages.FirstOrDefault(p => p.PackageName == Launch.PackageName);
            if (package == null)
            {
                package = new BundleCollectorPackage { PackageName = Launch.PackageName, PackageDesc = "Tide 默认资源包" };
                setting.Packages.Add(package);
            }

            EnsureGroup(package, "HotUpdateDll", HotDllFolder);
            EnsureGroup(package, "AotDll", AotDllFolder);
            EnsureGroup(package, "Scene", "Assets/Scenes");
            EnsureGroup(package, "AIModel", "Assets/AIModels");

            BundleCollectorSettingData.SaveFile();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[热更配置] YooAsset 收集器：DefaultPackage ← HotUpdateDll/AotDll/Scene/AIModel 四组（其余资源组后续按需追加）");
        }

        [MenuItem("Tide/热更/3. 生成 Launch 场景")]
        public static void CreateLaunchScene()
        {
            EnsureFolder("Assets/Launch");

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

        [MenuItem("Tide/热更/4. 编译热更DLL并拷入Assets")]
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

        [MenuItem("Tide/热更/5. 打包前置生成（AOT泛型补充/link.xml）")]
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

        private static void EnsureGroup(BundleCollectorPackage package, string groupName, string collectPath)
        {
            EnsureFolder(collectPath);
            var group = package.Groups.FirstOrDefault(g => g.GroupName == groupName);
            if (group == null)
            {
                group = new BundleCollectorGroup { GroupName = groupName, GroupDesc = groupName };
                package.Groups.Add(group);
            }
            if (group.Collectors.Count == 0)
            {
                group.Collectors.Add(new BundleCollector
                {
                    CollectPath = collectPath,
                    CollectorGUID = AssetDatabase.AssetPathToGUID(collectPath),
                    CollectorType = ECollectorType.MainAssetCollector,
                    AddressRuleName = "AddressByFileName",
                    PackRuleName = "PackDirectory",
                    FilterRuleName = "CollectAll",
                });
            }
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
