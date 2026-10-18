using System.Collections.Generic;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>
    /// 卡牌立绘按名称解析（2026-10-13 立绘随卡类型定案）：卡数据只存名字（Cards.json illustration 列），
    /// 渲染侧按名称两级回落找图——与战场 3D 模型按名称找（MonsterModelResolver）同口径。
    ///   ① Assets/Art/Battle3D/Icons/{name}.png——UI 立绘（Single 模式直载；2026-10-15 目录重构自 Art/UI/Sprites 迁入）；
    ///   ② Assets/Art/YGOMonstersRig/{name}/{name}.png——生物模型预览图（2026-10-17 起=StreamingAssets
    ///      bundle 内 Multiple 子精灵，MonsterRigAssets.LoadPreviewSprite；编辑器 AssetDatabase 直读已退役）。
    /// 结果按名缓存（含失败 null，防逐卡重试）。
    /// 2026-10-15：法术/结界无合适立绘（缺省空白不放图）——本库只服务生物立绘，缺省 Title 常量已退役。
    /// </summary>
    public static class CardArtLibrary
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static string UiSpritePath(string name) => $"Assets/Art/Battle3D/Icons/{name}.png";

        /// <summary>按名称取立绘 Sprite；null=两级都未命中（调用方保持占位）。</summary>
        public static Sprite Load(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (Cache.TryGetValue(name, out var cached)) return cached;
            var sprite = Tide.HotUpdate.HotUpdateAssets.Load<Sprite>(name, UiSpritePath(name));
            if (sprite == null)
                sprite = Tide.HotUpdate.MonsterRigAssets.LoadPreviewSprite(name);
            Cache[name] = sprite;
            return sprite;
        }
    }
}
