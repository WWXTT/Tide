// 热更管线（HybridCLR+YooAsset）解除期：由 TIDE_HYBRID_YOO 宏整体启停；恢复=开宏+HybridCLR enable+补包三步（见 查缺补漏.md「待办总账」第 1 条；编辑器菜单 Tools/热更/1-5）
#if TIDE_HYBRID_YOO
using System.IO;
using UnityEngine;
using YooAsset.Editor;

namespace Tide.HotUpdateTools
{
    /// <summary>
    /// 收集所有资源但排除 Launch 启动场景。
    /// Launch 是 AOT 构建场景（EditorBuildSettings 直打进包），若再被 Scene 收集组收进 bundle 会双打包。
    /// 另排除三处独立分发/不应进热更包的目录（2026-10-18 包体去重）：
    /// YGOMonstersRig=模型库走 MonsterRigBundleBuilder 独立 bundle（StreamingAssets/MonsterRig）；
    /// Samples=引擎包示例；StreamingAssets=独立 bundle 输出地（收集会二次装箱）。
    /// 再排除 LightingData.asset（2026-10-23 502 修复）：烘焙光照数据是 editor-only 资产，
    /// Unity 拒收进 AssetBundle——Main 场景烘焙后生成 Assets/Scenes/Main/LightingData.asset，
    /// 被本组当主资产收进 assets_scenes_main.bundle，拒收后 bundle 为空不产出，打包 501/502。
    /// </summary>
    [DisplayName("收集所有但排除Launch场景")]
    public class CollectAllExceptLaunch : IAssetFilterRule
    {
        private static readonly string[] ExcludedFolders =
        {
            "Assets/Art/YGOMonstersRig/",
            "Assets/Samples/",
            "Assets/StreamingAssets/",
        };

        public string FindAssetType { get { return EAssetFilterType.All.ToString(); } }

        public bool IsCollectAsset(AssetFilterRuleData data)
        {
            var fileName = Path.GetFileName(data.AssetPath);
            if (fileName == "Launch.unity" || fileName == "LightingData.asset")
                return false;
            foreach (var folder in ExcludedFolders)
            {
                if (data.AssetPath.StartsWith(folder, System.StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            return true;
        }
    }
}
#endif
