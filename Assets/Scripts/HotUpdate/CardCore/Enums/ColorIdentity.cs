using System;
using System.Collections.Generic;

namespace CardCore
{
    /// <summary>
    /// 颜色认同（主色+副色）
    /// 用于表示卡牌的颜色归属，支持 HS 职业的跨色特性
    /// </summary>
    [Serializable]
    public struct ColorIdentity : IEquatable<ColorIdentity>
    {
        /// <summary>主色</summary>
        public ManaType Primary;

        /// <summary>副色（可为空）</summary>
        public ManaType? Secondary;

        public ColorIdentity(ManaType primary, ManaType? secondary = null)
        {
            Primary = primary;
            Secondary = secondary;
        }

        /// <summary>是否包含指定颜色</summary>
        public bool Contains(ManaType color)
        {
            return Primary == color || (Secondary.HasValue && Secondary.Value == color);
        }

        /// <summary>是否为纯色（无副色）</summary>
        public bool IsMonoColor => !Secondary.HasValue;

        /// <summary>颜色数量</summary>
        public int ColorCount => IsMonoColor ? 1 : (Primary == Secondary.Value ? 1 : 2);

        public bool Equals(ColorIdentity other) =>
            Primary == other.Primary && Secondary == other.Secondary;

        public override bool Equals(object obj) => obj is ColorIdentity ci && Equals(ci);

        public override int GetHashCode() => HashCode.Combine(Primary, Secondary);

        public override string ToString()
        {
            if (IsMonoColor) return Primary.ToString();
            return $"{Primary}+{Secondary}";
        }

        public static bool operator ==(ColorIdentity left, ColorIdentity right) =>
            left.Equals(right);

        public static bool operator !=(ColorIdentity left, ColorIdentity right) =>
            !left.Equals(right);

        // ===== HS 职业颜色映射 =====

        /// <summary>圣骑士：白色（秩序）+ 绿色（成长）</summary>
        public static ColorIdentity Paladin => new ColorIdentity(ManaType.White, ManaType.Green);

        /// <summary>牧师：白色（治疗）+ 黑色（操控）</summary>
        public static ColorIdentity Priest => new ColorIdentity(ManaType.White, ManaType.Black);

        /// <summary>法师：蓝色（知识）</summary>
        public static ColorIdentity Mage => new ColorIdentity(ManaType.Blue);

        /// <summary>潜行者：蓝色（操控）+ 黑色（暗杀）</summary>
        public static ColorIdentity Rogue => new ColorIdentity(ManaType.Blue, ManaType.Black);

        /// <summary>术士：黑色（代价）+ 红色（破坏）</summary>
        public static ColorIdentity Warlock => new ColorIdentity(ManaType.Black, ManaType.Red);

        /// <summary>死亡骑士：黑色（死亡）+ 白色（秩序）</summary>
        public static ColorIdentity DeathKnight => new ColorIdentity(ManaType.Black, ManaType.White);

        /// <summary>战士：红色（攻击）+ 绿色（坚韧）</summary>
        public static ColorIdentity Warrior => new ColorIdentity(ManaType.Red, ManaType.Green);

        /// <summary>猎人：红色（攻击）+ 绿色（野兽）</summary>
        public static ColorIdentity Hunter => new ColorIdentity(ManaType.Red, ManaType.Green);

        /// <summary>恶魔猎手：红色（攻击）+ 黑色（恶魔）</summary>
        public static ColorIdentity DemonHunter => new ColorIdentity(ManaType.Red, ManaType.Black);

        /// <summary>德鲁伊：绿色（成长）+ 蓝色（变形）</summary>
        public static ColorIdentity Druid => new ColorIdentity(ManaType.Green, ManaType.Blue);

        /// <summary>萨满：绿色（图腾）+ 红色（元素）</summary>
        public static ColorIdentity Shaman => new ColorIdentity(ManaType.Green, ManaType.Red);

        /// <summary>中立：灰色</summary>
        public static ColorIdentity Neutral => new ColorIdentity(ManaType.Gray);

        // ===== YGO 属性 → 颜色映射 =====

        /// <summary>光 → 白色（秩序、保护）</summary>
        public static ColorIdentity Light => new ColorIdentity(ManaType.White);

        /// <summary>暗 → 黑色（牺牲、坟墓场）</summary>
        public static ColorIdentity Dark => new ColorIdentity(ManaType.Black);

        /// <summary>火 → 红色（攻击性、伤害）</summary>
        public static ColorIdentity Fire => new ColorIdentity(ManaType.Red);

        /// <summary>水 → 蓝色（操控、控制）</summary>
        public static ColorIdentity Water => new ColorIdentity(ManaType.Blue);

        /// <summary>地 → 绿色（成长、稳定）</summary>
        public static ColorIdentity Earth => new ColorIdentity(ManaType.Green);

        /// <summary>风 → 蓝色+灰色（操控、通用）</summary>
        public static ColorIdentity Wind => new ColorIdentity(ManaType.Blue, ManaType.Gray);
    }
}
