using System.Collections.Generic;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>
    /// 卡牌立绘按名称解析（2026-10-13 立绘随卡类型定案）：卡数据只存名字（Cards.json illustration 列），
    /// 渲染侧按名称两级回落找图——与战场 3D 模型按名称找（MonsterModelResolver）同口径。
    ///   ① 模型绑定立绘：MonsterRigAssets.LoadPreviewSprite(name)——与 3D 模型同目录
    ///      YGOMonstersRig/{name}/{name}.png（2026-11-15 修正：此前先查 Art/Icons，导致卡面图和
    ///      战场模型来自不同文件夹；模型同名命中而卡面落 ICON/占位时两者不一致）；
    ///   ② 兜底 UI 立绘：Assets/Art/Icons/{name}.png（少数非模型卡/旧卡仍走 UI 图标）。
    /// 结果按名缓存（含失败 null，防逐卡重试）。
    /// 2026-10-15：法术/结界无合适立绘（缺省空白不放图）——本库只服务生物立绘，缺省 Title 常量已退役。
    /// </summary>
    public static class CardArtLibrary
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static string UiSpritePath(string name) => $"Assets/Art/Icons/{name}.png";

        /// <summary>按名称取立绘 Sprite；null=两级都未命中（调用方保持占位）。
        /// 先取模型同目录预览图（与 MonsterModelResolver 同源），再回落 UI 图标。
        /// tryBareAddress=false（2026-10-21）：图标组=相对路径寻址，裸名第二发永不合法命中——
        /// 而生物立绘名与 rig prefab 裸名地址同名（PetitAngel 等），按 Sprite 误载 prefab 会触发
        /// Yoo LogError（真机噪音/编辑器离线卡死）。命中回落交给 LoadPreviewSprite 的 #preview。</summary>
        public static Sprite Load(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (Cache.TryGetValue(name, out var cached)) return cached;
            // ① 模型同目录预览图（卡面=模型同一文件夹，名称单一来源）
            var sprite = Tide.HotUpdate.MonsterRigAssets.LoadPreviewSprite(name);
            // ② UI 图标兜底（非模型卡/旧卡）
            if (sprite == null)
                sprite = Tide.HotUpdate.HotUpdateAssets.Load<Sprite>(name, UiSpritePath(name), tryBareAddress: false);
            Cache[name] = sprite;
            return sprite;
        }
    }
}
