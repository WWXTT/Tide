using System;
using CardCore;
using CardCore.Attribute;

namespace SynergyUI
{
    /// <summary>UI 颜色过滤维度（红蓝绿灰 + 全部）。内部英文，展示中文。</summary>
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
    /// AttributeValueConfig.json 的 EffectColor）；关键词颜色读 KeywordsConfig 的 color 字段。
    /// </summary>
    public static class ColorFilter
    {
        /// <summary>原子效果 → UIColor（取不到颜色归为灰）。</summary>
        public static UIColor OfAtomic(AtomicEffectType type)
        {
            var affinity = ElementAffinities.GetAffinityForEffect(type);
            return FromManaType(affinity.PrimaryColor);
        }

        /// <summary>关键词 id → UIColor（读 KeywordsConfig.color；缺省灰）。</summary>
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
                _ => UIColor.Gray,
            };
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
            return colorName switch
            {
                "红" => UIColor.Red,
                "蓝" => UIColor.Blue,
                "绿" => UIColor.Green,
                _ => UIColor.Gray,
            };
        }
    }
}
