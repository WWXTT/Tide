// 热更管线（HybridCLR+YooAsset）解除期：由 TIDE_HYBRID_YOO 宏整体启停，恢复步骤见项目根《热更插件解除与恢复.md》
#if TIDE_HYBRID_YOO
using System.IO;
using YooAsset.Editor;

namespace Tide.HotUpdateTools
{
    /// <summary>
    /// Assets/Art 运行时资产过滤（2026-10-18 YooAsset 打包换装，替代旧 ArtStaticBundleBuilder 白名单）：
    /// 扩展名白名单收录 prefab/png/mat/asset/shader/shadergraph/vfx；YGOMonstersRig/（MonsterRig 专组）
    /// 与 AIModels/（AIModel 专组）子树出局。白名单外扩展（.cginc/.vfxblock 等）不作为主资产——
    /// 它们仍会作为依赖被引用链自动收进包。
    /// </summary>
    [DisplayName("收集运行时美术资产（白名单+排除专组子树）")]
    public class CollectRuntimeArt : IAssetFilterRule
    {
        private static readonly string[] ExcludedFolders =
        {
            "Assets/Art/YGOMonstersRig/",
            "Assets/Art/AIModels/",
        };

        public string FindAssetType { get { return EAssetFilterType.All.ToString(); } }

        public bool IsCollectAsset(AssetFilterRuleData data)
        {
            foreach (var folder in ExcludedFolders)
            {
                if (data.AssetPath.StartsWith(folder, System.StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            switch (Path.GetExtension(data.AssetPath).ToLowerInvariant())
            {
                case ".prefab":
                case ".png":
                case ".mat":
                case ".asset":
                case ".shader":
                case ".shadergraph":
                case ".vfx":
                    return true;
                default:
                    return false;
            }
        }
    }

    /// <summary>
    /// YGOMonstersRig 专用寻址（镜像旧 MonsterRigBundleBuilder 的包内地址约定，运行时零改动）：
    /// prefab→模型名（MonsterRigAssets.LoadPrefab 按名取）、预览图→名#preview（LoadSubAssetsAsync
    /// 多切片取最大矩形的入口）、_manifest.txt→#names（模型名单 TextAsset）。
    /// 双源常量：Tide.HotUpdate.MonsterRigAssets 的 PreviewSuffix/NamesAddress——改一处须两处一起改。
    /// </summary>
    [DisplayName("定位地址: 模型库专用（名/名#preview/#names）")]
    public class RigModelAddress : IAddressRule
    {
        public string GetAssetAddress(AddressRuleData data)
        {
            var fileName = Path.GetFileNameWithoutExtension(data.AssetPath);
            switch (Path.GetExtension(data.AssetPath).ToLowerInvariant())
            {
                case ".png":
                    return fileName + Tide.HotUpdate.MonsterRigAssets.PreviewSuffix;
                case ".txt":
                    return fileName == "_manifest"
                        ? Tide.HotUpdate.MonsterRigAssets.NamesAddress
                        : fileName;
                default:
                    return fileName; // prefab 及其余
            }
        }
    }

    /// <summary>
    /// YGOMonstersRig 完整性过滤（镜像旧 MonsterRigBundleBuilder 收录口径）：「目录名.prefab + 同名.png
    /// 齐全」才算可用模型，残缺目录整组出局；_manifest.txt（名单）恒收。防止名单与可加载模型不一致。
    /// </summary>
    [DisplayName("收集模型库完整目录（prefab+同名png齐全）")]
    public class CollectRigComplete : IAssetFilterRule
    {
        public string FindAssetType { get { return EAssetFilterType.All.ToString(); } }

        public bool IsCollectAsset(AssetFilterRuleData data)
        {
            var path = data.AssetPath;
            var fileName = Path.GetFileNameWithoutExtension(path);
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".txt":
                    return fileName == "_manifest";
                case ".prefab":
                case ".png":
                    var dir = Path.GetDirectoryName(path) ?? "";
                    var sibling = dir.Replace('\\', '/') + "/" + fileName;
                    var need = Path.GetExtension(path).ToLowerInvariant() == ".prefab" ? ".png" : ".prefab";
                    return File.Exists(sibling + need);
                default:
                    return false;
            }
        }
    }
}
    /// <summary>
    /// 相对路径寻址（ArtRuntime 组专用，2026-10-18 撞名审计定案）：Assets/Art 下存在同名异扩展
    /// 主资产（BattleHex.asset 网格 vs BattleHex.prefab、BattlePriorityPanel.mat vs .shader、
    /// BlackHole.mat vs .shader）——文件名寻址三组撞址，Art 主资产一律按「去 Assets/ 前缀的相对路径」
    /// 定位（YooAsset 禁止地址以 Assets/ 开头，见 BundleCollector 地址校验）。
    /// 运行时 HotUpdateAssets.Load 先按同变换后的相对路径、再按文件名双试，调用点零改动——
    /// 双源变换：Tide.HotUpdate.HotUpdateAssets.Load 的 StripAssetsPrefix，改一处须两处一起改。
    /// </summary>
    [DisplayName("定位地址: 相对路径（去Assets/前缀）")]
    public class AddressByRelativePath : IAddressRule
    {
        public string GetAssetAddress(AddressRuleData data)
        {
            return data.AssetPath.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase)
                ? data.AssetPath.Substring("Assets/".Length)
                : data.AssetPath;
        }
    }
#endif
