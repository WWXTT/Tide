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

        /// <summary>目标种类集（表 TargetKinds 列：逗号分隔 TargetKind 序号；空 = 无目标原子）。
        /// 2026-09-10 目标域模型：取代旧 TargetType+分区 filter token——组合层取成员原子域的交集。</summary>
        public string TargetKinds;

        /// <summary>域内属性过滤（逗号分隔属性 token：Creature/Player/Untapped/Tapped/Damaged/Power&gt;N 等；
        /// 分区与归属 token 已并入 TargetKinds 序号，此列只剩属性细化）</summary>
        public string TargetFilter;

        /// <summary>极性（2026-09-10 定案）：-1=对对手释放有益（伤害/削弱类）；+1=对己方释放有益（治疗/增益类）；
        /// 0=中性。表级列（EffectType 固有语义，实例不可覆盖）。配合 TargetKind 域侧别做错边折价（CostDerivation）。</summary>
        public float Polarity;

        /// <summary>默认触发时机（仅对触发式效果有效）</summary>
        public string DefaultTriggerTiming;

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
        /// 获取目标种类集（解析 TargetKinds 列；空集 = 无目标原子，不参与组合交集约束）
        /// </summary>
        public List<int> GetTargetKindList()
            => CardCore.TargetKindRules.Parse(TargetKinds);

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

    // EffectTargetType / EffectTargetScope 已删除（2026-09-10 目标域模型）：
    // 取代者 = TargetKind 序号集合（Effects/TargetKind.cs）+ 组合层 SelectionMode。
    // 旧→新迁移映射见 Config/m1_schema_migration.py 与 Config/target_kinds_review.csv。

    // 效果持续时间类型 EffectDurationType 已删除：
    // 与运行时 DurationType（Enums/Zones.cs）合并为单一真相源，
    // 消除 CardEffectConverter 曾以 int 强转跨枚举导致的错位映射
    // （旧表侧 Permanent=4 会被误转为运行时 WhileCondition=4）。
    // 旧 JSON 里的表侧枚举名经 AtomicEffectTable.ParseDurationType 别名归一。

    // EffectTier 三分类枚举已删除（2026-09-10：零消费——关键词认 Grant 前缀、指示物认 CounterRules spec、合成编辑性不看档）。

    #endregion
}
