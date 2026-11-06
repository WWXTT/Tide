using System;
using CardCore;
using CardCore.Attribute;

namespace SynergyUI
{
    /// <summary>UI 颜色过滤维度（红蓝绿灰 + 全部）。内部英文，展示中文。
    /// 白/黑成员已删（2026-10-29 P4 色序清尾——2026-10-28 P2 白/黑 ManaType 席位删除后
    /// 无数据可命中）；预制体残烘的 chip-Black/chip-White 节点由两屏 Bind 时失活。</summary>
    public enum UIColor
    {
        All,
        Red,
        Blue,
        Green,
        Gray,
    }

    /// <summary>
    /// 颜色分类助手 —— 把原子效果 / 关键词归到红蓝绿灰，供三个界面的颜色过滤条使用。
    ///
    /// 原子效果颜色由配置表驱动（复用 ElementAffinities.GetAffinityForEffect，读
    /// AttributeValueConfig.json 的 EffectColor）；关键词颜色读合成定义的 color 字段
    /// （同样源自原子表 EffectColor，关键词不单独开表）。
    /// </summary>
    public static class ColorFilter
    {
        /// <summary>原子效果 → UIColor（取不到颜色归为灰）。</summary>
        public static UIColor OfAtomic(AtomicEffectType type)
        {
            var affinity = ElementAffinities.GetAffinityForEffect(type);
            return FromManaType(affinity.PrimaryColor);
        }

        /// <summary>关键词 id → UIColor（读合成定义的 color，源自原子表 EffectColor；缺省灰）。</summary>
        public static UIColor OfKeyword(string keywordId)
        {
            var def = CardLoader.GetKeywordDefinition(keywordId);
            return FromColorName(def?.color);
        }

        /// <summary>UIColor → 对应法力颜色（All/Gray → Gray）。</summary>
        public static ManaType ToManaType(UIColor color)
        {
            return color switch
            {
                UIColor.Red => ManaType.Red,
                UIColor.Blue => ManaType.Blue,
                UIColor.Green => ManaType.Green,
                _ => ManaType.Gray,
            };
        }

        /// <summary>中文显示名。</summary>
        public static string DisplayName(UIColor color)
        {
            return color switch
            {
                UIColor.All => "全部",
                UIColor.Red => "红",
                UIColor.Blue => "蓝",
                UIColor.Green => "绿",
                UIColor.Gray => "灰",
                _ => color.ToString(),
            };
        }

        /// <summary>过滤判定：filter==All 时全通过，否则要求相等。</summary>
        public static bool Matches(UIColor filter, UIColor item)
        {
            return filter == UIColor.All || filter == item;
        }

        private static UIColor FromManaType(ManaType mana)
        {
            return mana switch
            {
                ManaType.Red => UIColor.Red,
                ManaType.Blue => UIColor.Blue,
                ManaType.Green => UIColor.Green,
                _ => UIColor.Gray, // 灰/任意及未来扩色落灰（白/黑席位已删——2026-10-29 P4 成员随删）
            };
        }

        /// <summary>表行 Tags 色名（2026-10-06 中文化：追加中文色名，词表=ManaTypeNames）→ UIColor。
        /// 2026-09-14：原子库圆点直读表行颜色（此前经 ElementAffinity 间接取色，含兜底失真）。</summary>
        public static UIColor OfColorName(string colorName) => FromColorName(colorName);

        /// <summary>表行 Tags 整串（多标签 CSV，如「伤害产出族,红」）→ UIColor：取首个色名标签；
        /// 无色名标签（纯族标签/空）落 Gray。2026-10-06 修复：此前整串直传反查必失败落灰。</summary>
        public static UIColor OfTagsCsv(string tagsCsv)
        {
            if (!string.IsNullOrEmpty(tagsCsv))
                foreach (var tok in tagsCsv.Split(','))
                    if (ManaTypeNames.TryParseZh(tok.Trim(), out var m))
                        return FromManaType(m);
            return UIColor.Gray;
        }

        private static UIColor FromColorName(string colorName)
        {
            if (string.IsNullOrEmpty(colorName))
            {
                return UIColor.Gray;
            }
            if (Enum.TryParse<ManaType>(colorName, true, out var mana))
            {
                return FromManaType(mana);
            }
            // 中文色名（五席全词表——2026-10-06 修复只认红蓝绿的隐患；白/黑席位已删随之失效）。
            // 词表外（如族标签「伤害产出族」）落 Gray。
            return CardCore.ManaTypeNames.TryParseZh(colorName, out var zh)
                ? FromManaType(zh)
                : UIColor.Gray;
        }
    }
}
