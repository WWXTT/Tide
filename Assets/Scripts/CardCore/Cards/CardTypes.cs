using System;
using System.Collections.Generic;
using UnityEngine;


namespace CardCore
{
    /// <summary>
    /// 效果表数据结构
    /// 用于 ScriptableObject 配置效果数据
    /// </summary>
    [Serializable]
    public class Effect_table
    {
        /// <summary>
        /// 效果缩写/ID
        /// </summary>
        [Tooltip("效果缩写，用于唯一标识效果")]
        public string Effect_Abbreviation;

        /// <summary>
        /// 是否为主动效果
        /// </summary>
        [Tooltip("是否为主动效果")]
        public bool Initiative;

        /// <summary>
        /// 效果参数
        /// </summary>
        [Tooltip("效果参数值")]
        public float Parameters;

        /// <summary>
        /// 效果速度
        /// </summary>
        [Tooltip("效果速度")]
        public EffectSpeed EffctSpeed;

        /// <summary>
        /// 法力类型
        /// </summary>
        [Tooltip("对应的法力类型")]
        public ManaType Mana_type;

        /// <summary>
        /// 效果描述
        /// </summary>
        [Tooltip("效果的详细描述")]
        [TextArea(3, 10)]
        public string Effect_Description;
    }

    // ============================================ 效果相关接口 ============================================

    /// <summary>
    /// 具有效果列表的接口（编辑器配置用）
    /// </summary>
    public interface IHasEffects
    {
        List<Effect_table> Effects { get; set; }
    }

    /// <summary>
    /// 具有运行时效果列表的接口（游戏逻辑用）
    /// </summary>
    public interface IHasRuntimeEffects
    {
        List<IEffect> RuntimeEffects { get; set; }
    }

    // ============================================ 卡牌属性接口 ============================================

    /// <summary>
    /// 具有立绘的接口
    /// </summary>
    public interface IHasIllustration
    {
        string Illustration { get; set; }
    }

    /// <summary>
    /// 具有名称的接口
    /// </summary>
    public interface IHasName
    {
        string CardName { get; set; }
    }

    // ============================================ 战斗属性接口 ============================================

    /// <summary>
    /// 具有生命值的接口
    /// </summary>
    public interface IHasLife
    {
        int Life { get; set; }
    }

    /// <summary>
    /// 具有攻击力的接口
    /// </summary>
    public interface IHasPower
    {
        int Power { get; set; }
    }

    /// <summary>
    /// 具有费用的接口
    /// </summary>
    public interface IHasCost
    {
        Dictionary<int, float> Cost { get; set; }
    }

    /// <summary>
    /// 具有关键词的接口
    /// </summary>
    public interface IHasKeywords
    {
        HashSet<string> Keywords { get; set; }

        void AddKeyword(string keywordId);
        void RemoveKeyword(string keywordId);
        bool HasKeyword(string keywordId);
    }

    /// <summary>
    /// 具有横置状态的接口
    /// </summary>
    public interface ITappable
    {
        bool IsTapped { get; set; }
    }

    // ============================================ 效果接口 ============================================

    /// <summary>
    /// 组合效果接口
    /// </summary>
    public interface IEffect { }

    /// <summary>
    /// 原子效果接口
    /// </summary>
    public interface IAtomicEffect : IEffect { }

    // ============================================ 扩展卡牌属性接口（三游戏统一） ============================================

    /// <summary>
    /// 具有子类型的接口（种族/职业）
    /// </summary>
    public interface IHasSubtypes
    {
        CardSubtype Subtypes { get; }
        bool HasSubtype(CardSubtype subtype) => (Subtypes & subtype) != 0;
    }

    /// <summary>
    /// 具有等级/阶级/连接值的接口（YGO 怪兽）
    /// </summary>
    public interface IHasLevel
    {
        int? Level { get; }
        int? Rank { get; }
        int? LinkRating { get; }
    }

    /// <summary>
    /// 具有颜色认同的接口（MTG 颜色饼）
    /// </summary>
    public interface IHasColorIdentity
    {
        ColorIdentity ColorIdentity { get; }
    }

    /// <summary>
    /// 额外卡组卡牌接口
    /// </summary>
    public interface IIsExtraDeck
    {
        bool IsExtraDeck { get; }
        SummonMethod DefaultSummonMethod { get; }
    }

    /// <summary>
    /// 具有来源信息的接口
    /// </summary>
    public interface IHasSourceGame
    {
        SourceGame SourceGame { get; }
        string SourceArchetype { get; }
    }

    /// <summary>
    /// 具有超类型的接口
    /// </summary>
    public interface IHasSupertype
    {
        Cardtype Supertype { get; set; }
    }
}
