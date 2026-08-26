using System;
using System.Collections.Generic;

namespace CardCore
{
    /// <summary>
    /// 法力/元素类型
    /// </summary>
    public enum ManaType
    {
        Gray = 0,
        Red = 1,
        Blue = 2,
        Green = 3,
        White = 4,
        Black = 5,
    }

    /// <summary>
    /// 效果速度
    /// </summary>
    public enum EffectSpeed
    {
        /// <summary>自由时点 - 可以随时发动</summary>
        自由时点 = 0,
        /// <summary>可选诱发 - 可选择是否发动</summary>
        可选诱发 = 1,
        /// <summary>强制诱发 - 必须发动</summary>
        强制诱发 = 2,
    }

    /// <summary>
    /// 卡牌超类型 —— 覆盖三游戏所有卡牌种类
    /// </summary>
    public enum Cardtype
    {
        /// <summary>生物 / 随从 / 怪兽</summary>
        Creature = 0,
        /// <summary>瞬间 / 巫术 / 法术 / 通常魔法</summary>
        Spell = 1,
        /// <summary>结界 / 永续魔法 / 装备魔法</summary>
        Enchantment = 2,
        /// <summary>神器 / 武器</summary>
        Artifact = 3,
        /// <summary>地 / 元素源</summary>
        Land = 4,
        /// <summary>鹏洛客</summary>
        Planeswalker = 5,
        /// <summary>英雄卡 (HS)</summary>
        Hero = 6,
        /// <summary>地标 (HS)</summary>
        Location = 7,
        /// <summary>陷阱 (YGO)</summary>
        Trap = 8,
        /// <summary>场地魔法 / 领域</summary>
        Field = 9,
    }

    /// <summary>
    /// 卡牌子类型（种族/职业/怪兽种类）
    /// 使用 Flags 支持多子类型组合
    /// </summary>
    [Flags]
    public enum CardSubtype
    {
        None = 0,

        // ===== 通用种族（跨游戏） =====
        Beast = 1 << 0,      // 野兽
        Dragon = 1 << 1,     // 龙
        Warrior = 1 << 2,    // 战士
        Mage = 1 << 3,       // 法师
        Demon = 1 << 4,      // 恶魔
        Undead = 1 << 5,     // 亡灵
        Elf = 1 << 6,        // 精灵
        Human = 1 << 7,      // 人类
        Mech = 1 << 8,       // 机械
        Elemental = 1 << 9,  // 元素
        Beastman = 1 << 10,  // 兽人
        Insect = 1 << 11,    // 昆虫
        Plant = 1 << 12,     // 植物
        Fish = 1 << 13,      // 鱼
        Bird = 1 << 14,      // 鸟
        Giant = 1 << 15,     // 巨人

        // ===== YGO 特有子类型 =====
        Tuner = 1 << 20,     // 协调者
        Fusion = 1 << 21,    // 融合
        Synchro = 1 << 22,   // 同调
        Xyz = 1 << 23,       // 超量
        Link = 1 << 24,      // 连接
        Pendulum = 1 << 25,  // 灵摆
        Toon = 1 << 26,      // 卡通
        Spirit = 1 << 27,    // 灵魂
        Gemini = 1 << 28,    // 二重
        Flip = 1 << 29,      // 反转
        Union = 1 << 30,     // 联合
        // 注意：1 << 31 会溢出为负数，需要小心

        // ===== 魔法/陷阱子类型 =====
        QuickPlay = 1 << 16,   // 速攻魔法
        Continuous = 1 << 17,  // 永续
        Equip = 1 << 18,       // 装备
        Ritual = 1 << 19,      // 仪式
        Normal = 1 << 29,      // 通常
        Counter = unchecked((int)0x80000000),  // 反制（使用最高位，long存储）

        // ===== 常用组合 =====
        AllCreatureTypes = Beast | Dragon | Warrior | Mage | Demon | Undead | Elf | Human | Mech | Elemental,
        AllExtraDeckTypes = Fusion | Synchro | Xyz | Link | Pendulum,
    }

    /// <summary>
    /// 召唤方式
    /// </summary>
    public enum SummonMethod
    {
        /// <summary>标准费用打出</summary>
        Normal = 0,
        /// <summary>效果特殊召唤</summary>
        Special = 1,
        /// <summary>融合召唤</summary>
        Fusion = 2,
        /// <summary>同调召唤（需要协调者+非协调者，等级匹配）</summary>
        Synchro = 3,
        /// <summary>超量召唤（同等级素材叠加）</summary>
        Xyz = 4,
        /// <summary>连接召唤（素材数=连接值）</summary>
        Link = 5,
        /// <summary>仪式召唤</summary>
        Ritual = 6,
        /// <summary>灵摆召唤</summary>
        Pendulum = 7,
    }

    /// <summary>
    /// 来源游戏
    /// </summary>
    public enum SourceGame
    {
        Custom = 0,
        MTG = 1,
        Hearthstone = 2,
        YuGiOh = 3,
    }

    /// <summary>
    /// 稀有度
    /// </summary>
    public enum Rarity
    {
        Common = 0,      // 普通
        Uncommon = 1,    // 非普通
        Rare = 2,        // 稀有
        Epic = 3,        // 史诗
        Legendary = 4,   // 传奇
    }

    /// <summary>
    /// 战斗形态（YGO 攻击/守备表示）
    /// </summary>
    public enum BattlePosition
    {
        /// <summary>攻击表示</summary>
        Attack = 0,
        /// <summary>守备表示</summary>
        Defense = 1,
        /// <summary>里侧攻击（极少见）</summary>
        FaceDownAttack = 2,
        /// <summary>里侧守备</summary>
        FaceDownDefense = 3,
    }

    /// <summary>
    /// 六边形方向（用于链接系统）
    /// </summary>
    [Flags]
    public enum HexDirection
    {
        None = 0,
        Up = 1 << 0,
        Down = 1 << 1,
        UpperLeft = 1 << 2,
        UpperRight = 1 << 3,
        LowerLeft = 1 << 4,
        LowerRight = 1 << 5,
        All = Up | Down | UpperLeft | UpperRight | LowerLeft | LowerRight,
    }
}
