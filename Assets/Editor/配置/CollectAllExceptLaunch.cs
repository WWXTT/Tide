using System.IO;
using UnityEngine;
using YooAsset.Editor;

namespace Tide.HotUpdateTools
{
    /// <summary>
    /// 收集所有资源但排除 Launch 启动场景。
    /// Launch 是 AOT 构建场景（EditorBuildSettings 直打进包），若再被 Scene 收集组收进 bundle 会双打包。
    /// </summary>
    [DisplayName("收集所有但排除Launch场景")]
    public class CollectAllExceptLaunch : IAssetFilterRule
    {
        public string FindAssetType { get { return EAssetFilterType.All.ToString(); } }

        public bool IsCollectAsset(AssetFilterRuleData data)
        {
            return Path.GetFileName(data.AssetPath) != "Launch.unity";
        }
    }
}
