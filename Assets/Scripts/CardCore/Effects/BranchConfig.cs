using System;
using System.Collections.Generic;

namespace CardCore
{
    /// <summary>
    /// 分支子条件类型
    /// </summary>
    public enum BranchSubConditionType
    {
        /// <summary>目标是否为特定种族/颜色</summary>
        TargetIsRaceOrColor,
        /// <summary>目标属性值大于/小于</summary>
        TargetAttributeCompare,
    }

    /// <summary>
    /// 分支子条件选项
    /// </summary>
    [Serializable]
    public class BranchSubCondition
    {
        public string Id;
        public BranchSubConditionType Type;
        public string DisplayName;
        public string Description;
        public string ParameterHint;
    }

    /// <summary>
    /// 分支条件定义。三族条件统一用此结构承载（Kind 判别）：
    /// OutcomeGate（伤害/治疗/信息→门槛达成走 then 免费奖励），
    /// Drawback（抽牌→减费缺陷，CostReduction 生效，不走 then/else），
    /// FilterPrecision（检索→筛选维度，见 BranchFilterTier）。
    /// </summary>
    [Serializable]
    public class BranchCondition
    {
        public string Id;
        public BranchConditionKind Kind = BranchConditionKind.OutcomeGate;
        public string DisplayName;
        public string Description;

        /// <summary>条件默认数值参数（如门槛）</summary>
        public int Param;
        /// <summary>条件默认字符串参数（如预言类型）</summary>
        public string StringParam;
        /// <summary>Drawback 专用：挂载此缺陷对原子费用的减免（下限 0）</summary>
        public int CostReduction;

        public List<BranchSubCondition> SubConditions = new List<BranchSubCondition>();
    }

    /// <summary>
    /// FilterPrecision 维度档：检索原子按筛选维度计费（确切单卡=3 / 类型+种族=2 / 单一维度=1）。
    /// </summary>
    [Serializable]
    public class BranchFilterTier
    {
        public string Id;
        public string DisplayName;
        public int Cost;
    }

    /// <summary>
    /// 分支配置（每个原子效果类型可定义可用条件集合）
    /// </summary>
    [Serializable]
    public class BranchConfig
    {
        public string Id;
        public string EffectTypeName;
        public string DisplayName;
        public List<BranchCondition> Conditions = new List<BranchCondition>();
        public int MaxChainedEffects = 2;
        public bool AllowNesting = false;
    }
}
