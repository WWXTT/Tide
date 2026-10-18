// 热更管线（HybridCLR+YooAsset）解除期：由 TIDE_HYBRID_YOO 宏整体启停，恢复步骤见项目根《热更插件解除与恢复.md》
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
            if (Path.GetFileName(data.AssetPath) == "Launch.unity")
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
