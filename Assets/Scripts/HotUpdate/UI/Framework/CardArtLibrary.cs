using System.Collections.Generic;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>
    /// 卡牌立绘按名称解析（2026-10-13 立绘随卡类型定案）：卡数据只存名字（Cards.json illustration 列），
    /// 渲染侧按名称两级回落找图——与战场 3D 模型按名称找（MonsterModelResolver）同口径。
    ///   ① Assets/Art/Battle3D/Icons/{name}.png——UI 立绘（Single 模式直载；2026-10-15 目录重构自 Art/UI/Sprites 迁入）；
    ///   ② Assets/Art/YGOMonstersRig/{name}/{name}.png——生物模型预览图（烘焙产出 Multiple 模式，
    ///      主资产是 Texture2D，须取子精灵）。
    /// 结果按名缓存（含失败 null，防逐卡重试）。
    /// 2026-10-15：法术/结界无合适立绘（缺省空白不放图）——本库只服务生物立绘，缺省 Title 常量已退役。
    /// </summary>
    public static class CardArtLibrary
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static string UiSpritePath(string name) => $"Assets/Art/Battle3D/Icons/{name}.png";
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

        /// <summary>生物模型预览图：Multiple 模式子精灵（编辑器 LoadAllAssetsAtPath）。
        /// 2026-10-16 定案（用户：「下有 _0/__1 类似情况一律显示 _0」）：取 rect 面积最大的子精灵——
        /// 多切片表的其余切片是自动切片碎片（8×8 级），"首个 Sprite"依赖 API 未定义的返回序不可靠；
        /// 且 17 张多切片表中 GreenPhantomKing/Tyhone/WingsOfWickedFlame 的 _0 本身是碎片、真图在
        /// _1/_2，按面积取恰好=其余 14 表的 _0 与这 3 表的真图（LoadAssetAtPath&lt;Sprite&gt; 对
        /// Multiple 主资产返回 null）。</summary>
        private static Sprite LoadCreaturePreview(string name)
        {
#if UNITY_EDITOR
            Sprite best = null;
            long bestArea = -1;
            foreach (var asset in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(CreaturePreviewPath(name)))
                if (asset is Sprite sprite)
                {
                    var area = (long)sprite.rect.width * (long)sprite.rect.height;
                    if (area > bestArea) { bestArea = area; best = sprite; }
                }
            return best;
#endif
            return null;
        }
    }
}
