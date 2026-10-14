using System.Collections.Generic;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>
    /// 卡牌立绘按名称解析（2026-10-13 立绘随卡类型定案）：卡数据只存名字（Cards.json illustration 列），
    /// 渲染侧按名称两级回落找图——与战场 3D 模型按名称找（MonsterModelResolver）同口径。
    ///   ① Assets/Art/UI/Sprites/{name}.png——UI 立绘（Title 缺省图等，Single 模式直载）；
    ///   ② Assets/Art/YGOMonstersRig/{name}/{name}.png——生物模型预览图（烘焙产出 Multiple 模式，
    ///      主资产是 Texture2D，须取子精灵）。
    /// 结果按名缓存（含失败 null，防逐卡重试）。
    /// </summary>
    public static class CardArtLibrary
    {
        /// <summary>缺省立绘名（法术/结界固定展示；Sprites 目录 Title.png）。</summary>
        public const string DefaultTitle = "Title";

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static string UiSpritePath(string name) => $"Assets/Art/UI/Sprites/{name}.png";
        public static string CreaturePreviewPath(string name) => $"Assets/Art/YGOMonstersRig/{name}/{name}.png";

        /// <summary>按名称取立绘 Sprite；null=两级都未命中（调用方保持占位）。</summary>
        public static Sprite Load(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (Cache.TryGetValue(name, out var cached)) return cached;
            var sprite = Tide.HotUpdate.HotUpdateAssets.Load<Sprite>(name, UiSpritePath(name));
            if (sprite == null)
                sprite = LoadCreaturePreview(name);
            Cache[name] = sprite;
            return sprite;
        }

        /// <summary>生物模型预览图：Multiple 模式子精灵（编辑器 LoadAllAssetsAtPath 取首个 Sprite；
        /// LoadAssetAtPath&lt;Sprite&gt; 对 Multiple 主资产返回 null）。</summary>
        private static Sprite LoadCreaturePreview(string name)
        {
#if UNITY_EDITOR
            foreach (var asset in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(CreaturePreviewPath(name)))
                if (asset is Sprite sprite) return sprite;
#endif
            return null;
        }
    }
}
