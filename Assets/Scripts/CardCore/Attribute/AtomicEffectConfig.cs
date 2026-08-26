using System;
using System.Collections.Generic;

namespace CardCore.Attribute
{
    /// <summary>
    /// 原子效果属性配置
    /// 对应 #Attribute.xlsm 数据表的一行
    /// Luban生成后会替换为cfg命名空间下的类
    /// </summary>
    [Serializable]
    public class AtomicEffectConfig
    {
        /// <summary>效果唯一ID</summary>
        public int Id;

        /// <summary>枚举名称（对应 AtomicEffectType）</summary>
        public string EnumName;

        /// <summary>中文显示名</summary>
        public string DisplayName;

        /// <summary>效果描述模板</summary>
        public string Description;

        /// <summary>基准费用（价值评估用，每点效果值对应的费用权重）</summary>
        public float BaseCost;

        /// <summary>费用乘数（不同目标范围对费用的影响）</summary>
        public float CostMultiplier;

        /// <summary>作用目标类型</summary>
        public EffectTargetType TargetType;

        /// <summary>目标筛选条件（逗号分隔，如 "Minion,Untapped,Damaged"）</summary>
        public string TargetFilter;

        /// <summary>目标数量（0=全部, -1=任意, >0=指定数量）</summary>
        public int TargetCount;

        /// <summary>作用范围</summary>
        public EffectTargetScope TargetScope;

        /// <summary>持续时间</summary>
        public EffectDurationType DurationType;

        /// <summary>默认触发时机（仅对触发式效果有效）</summary>
        public string DefaultTriggerTiming;

        /// <summary>默认发动类型</summary>
        public EffectActivationType ActivationType;

        /// <summary>默认发动条件（逗号分隔）</summary>
        public string DefaultConditions;

        /// <summary>是否可叠加</summary>
        public bool Stackable;

        /// <summary>结算优先级（0-100，值越高越先结算）</summary>
        public int Priority;

        /// <summary>效果标签（逗号分隔）</summary>
        public string Tags;

        /// <summary>可选触发时机（逗号分隔的 TriggerTiming 枚举名，空表示无触发配置）</summary>
        public string AvailableTriggerTimings;

        /// <summary>可选目标范围（逗号分隔的 EffectTargetScope 枚举名，空则使用 TargetScope）</summary>
        public string AvailableTargetScopes;

        /// <summary>分支配置ID（逗号分隔，引用 BranchConfigTable）</summary>
        public string BranchConfigs;

        /// <summary>关联效果枚举名（逗号分隔，常搭配出现的效果）</summary>
        public string RelatedEffects;

        /// <summary>设计备注</summary>
        public string Notes;

        /// <summary>
        /// 获取关联效果类型列表
        /// </summary>
        public List<string> GetRelatedEffectList()
        {
            if (string.IsNullOrEmpty(RelatedEffects)) return new List<string>();
            var list = new List<string>();
            foreach (var s in RelatedEffects.Split(','))
            {
                var trimmed = s.Trim();
                if (!string.IsNullOrEmpty(trimmed)) list.Add(trimmed);
            }
            return list;
        }

        /// <summary>
        /// 获取标签列表
        /// </summary>
        public List<string> GetTagList()
        {
            if (string.IsNullOrEmpty(Tags)) return new List<string>();
            var list = new List<string>();
            foreach (var s in Tags.Split(','))
            {
                var trimmed = s.Trim();
                if (!string.IsNullOrEmpty(trimmed)) list.Add(trimmed);
            }
            return list;
        }

        /// <summary>
        /// 获取可选触发时机列表
        /// </summary>
        public List<string> GetAvailableTriggerTimingList()
        {
            if (string.IsNullOrEmpty(AvailableTriggerTimings)) return new List<string>();
            var list = new List<string>();
            foreach (var s in AvailableTriggerTimings.Split(','))
            {
                var trimmed = s.Trim();
                if (!string.IsNullOrEmpty(trimmed)) list.Add(trimmed);
            }
            return list;
        }

        /// <summary>
        /// 获取可选目标范围列表
        /// </summary>
        public List<string> GetAvailableTargetScopeList()
        {
            if (string.IsNullOrEmpty(AvailableTargetScopes)) return new List<string>();
            var list = new List<string>();
            foreach (var s in AvailableTargetScopes.Split(','))
            {
                var trimmed = s.Trim();
                if (!string.IsNullOrEmpty(trimmed)) list.Add(trimmed);
            }
            return list;
        }

        /// <summary>
        /// 获取分支配置ID列表
        /// </summary>
        public List<string> GetBranchConfigIdList()
        {
            if (string.IsNullOrEmpty(BranchConfigs)) return new List<string>();
            var list = new List<string>();
            foreach (var s in BranchConfigs.Split(','))
            {
                var trimmed = s.Trim();
                if (!string.IsNullOrEmpty(trimmed)) list.Add(trimmed);
            }
            return list;
        }
    }

    #region 属性枚举定义

    /// <summary>
    /// 效果目标类型
    /// </summary>
    public enum EffectTargetType
    {
        /// <summary>自身</summary>
        Self,
        /// <summary>指定目标</summary>
        Target,
        /// <summary>所有敌方</summary>
        AllEnemies,
        /// <summary>所有友方</summary>
        AllAllies,
        /// <summary>全部（双方）</summary>
        All,
        /// <summary>随机</summary>
        Random,
        /// <summary>拥有者</summary>
        Owner,
        /// <summary>控制者</summary>
        Controller,
        /// <summary>对手</summary>
        Opponent,
        /// <summary>无目标（全局效果）</summary>
        None
    }

    /// <summary>
    /// 效果目标范围
    /// </summary>
    public enum EffectTargetScope
    {
        /// <summary>单一目标</summary>
        Single,
        /// <summary>范围效果（AoE）</summary>
        AoE,
        /// <summary>连锁效果</summary>
        Chain,
        /// <summary>扩散效果</summary>
        Spread,
        /// <summary>全局效果</summary>
        Global
    }

    /// <summary>
    /// 效果持续时间类型
    /// </summary>
    public enum EffectDurationType
    {
        /// <summary>瞬间（一次性）</summary>
        Instant,
        /// <summary>到回合结束</summary>
        UntilEndOfTurn,
        /// <summary>到下一回合</summary>
        UntilNextTurn,
        /// <summary>到阶段结束</summary>
        UntilEndOfPhase,
        /// <summary>永久</summary>
        Permanent,
        /// <summary>直到条件满足</summary>
        UntilCondition,
        /// <summary>指定回合数</summary>
        ForTurns
    }

    #endregion
}
