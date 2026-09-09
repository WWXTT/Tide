using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CardCore.Attribute;

namespace CardCore
{
    /// <summary>
    /// 卡牌内容身份推导服务 —— 与 CardCostService.EnsureCost 同管线（费用重推算时身份一并重推算）。
    ///
    /// 身份拆散到「原子级」（共享单元 = 原子表条目 + 实例参数，如 造成伤害 a4b823fc + target + value）：
    ///   - 原子哈希数组：跨效果按执行序展平，每个原子 = EffectType（锚定冻结原子表）+ 全部可变参数
    ///     与目标覆盖。同原子跨卡共享 embedding 行——「造成 4 点伤害」在火球/闪电/任何卡上是同一行。
    ///   - 组合结构哈希：各效果时点/发动字段 + 条件 + 代价 + Steps 骨架（原子为占位符，只留
    ///     kind 0 原子/1 分支/2 抉择、分支条件、各支原子数）——「怎么组合」独立成行。
    ///   - 关键词 + 卡 tag + 连接光环 一条组合哈希（关键词即原子 Grant*，与原子表同源）。
    ///   - 全部哈希混入原子表指纹：AttributeValueConfig / AtomicEffectTable 整表写进基底——
    ///     表冻结（当前进度定案）→ 身份稳定；表一旦变更 → 全体身份同步换血（行为基准变了，本该如此）。
    /// 属性（力量/生命/费用）不进身份：obs 已有实时特征（TideObservation 卡特征第 3/4/5 维），不重复。
    /// Id/DisplayName/Description/choices.label 是别名与展示，不参与（EffectChoiceData.label
    /// 注释「不参与哈希」的既有约定）。
    ///
    /// 稳定性口径：Steps/原子序列保留书写序（= 执行序）；条件/代价/Drawback/关键词/tag/光环
    /// 是无序叠加语义 → 规范化排序后再哈希（手抖调换行序不变身份）。
    /// </summary>
    public static class CardIdentityService
    {
        private static ulong? _tableFingerprint;

        /// <summary>原子表整表指纹（行为字段按 EnumName 排序后哈希；GetAll 走字典 Values 序不稳，必须排序。Id 是装载期自增，排除）。</summary>
        public static ulong TableFingerprint()
        {
            if (_tableFingerprint.HasValue) return _tableFingerprint.Value;

            var sb = new StringBuilder();
            foreach (var cfg in AtomicEffectTable.GetAll().OrderBy(c => c.EnumName, StringComparer.Ordinal))
            {
                sb.Append(cfg.EnumName).Append('|')
                  .Append(cfg.BaseCost.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                  .Append((int)cfg.TargetType).Append('|')
                  .Append(cfg.TargetFilter ?? string.Empty).Append('|')
                  .Append(cfg.TargetCount).Append('|')
                  .Append((int)cfg.TargetScope).Append('|')
                  .Append((int)cfg.DurationType).Append('|')
                  .Append(cfg.Turns).Append('|')
                  .Append((int)cfg.ActivationType).Append('|')
                  .Append((int)cfg.EffectTier).Append('\n');
            }
            _tableFingerprint = Fnv1a(sb.ToString());
            return _tableFingerprint.Value;
        }

        /// <summary>推导并写回卡的内容身份（EnsureCost 顶部调用；幂等，重算便宜）。token 等未经装载管线的卡保持空身份（观测侧记 0）。
        /// 身份拆散到原子级：原子哈希数组（跨效果按执行序展平）+ 组合结构哈希 + 关键词/tag/光环组合哈希。</summary>
        public static void EnsureIdentity(CardData card)
        {
            if (card == null) return;

            var atoms = new List<ulong>();
            if (card.Effects != null)
                foreach (var e in card.Effects)
                    CollectAtomHashes(e, atoms);
            card.SetIdentity(atoms.ToArray(), StructureHash(card), CompositionHash(card));
        }

        // ===================================================== 原子哈希（身份共享单元） =====================================================

        /// <summary>原子 = 原子表条目（EffectType 锚定冻结表）+ 实例参数与目标覆盖——
        /// 「造成伤害 a4b823fc + target + value」这类单元，同原子跨卡共享 embedding 行。</summary>
        internal static ulong AtomHash(AtomicEffectEntry a)
        {
            if (a == null) return Mix(0UL, TableFingerprint());

            var sb = new StringBuilder();
            AppendAtom(sb, a);
            return Mix(Fnv1a(sb.ToString()), TableFingerprint());
        }

        /// <summary>原子的全部可变参数与目标覆盖（用户填写面全量进哈希）。目标默认值来自原子表（冻结，经整表指纹全局覆盖）。</summary>
        private static void AppendAtom(StringBuilder sb, AtomicEffectEntry a)
        {
            sb.Append(a.EffectType ?? string.Empty).Append('|')
              .Append(a.Value).Append('|')
              .Append(a.Value2).Append('|')
              .Append(a.StringValue ?? string.Empty).Append('|')
              .Append(a.ManaTypeParam).Append('|')
              .Append(a.ZoneParam).Append('|')
              .Append(a.Duration).Append('|')
              .Append(a.DurationValue).Append('|')
              .Append(a.TargetTypeOverride).Append('|')
              .Append(a.TargetFilterOverride ?? string.Empty).Append('|')
              .Append(a.TargetCountOverride).Append('|')
              .Append(a.TargetScopeOverride).Append('|')
              .Append(a.DynamicTargetCount ? 1 : 0).Append('\n');
            if (a.Drawbacks != null && a.Drawbacks.Count > 0)
                sb.Append('d').Append(string.Join(",", a.Drawbacks.OrderBy(d => d, StringComparer.Ordinal))).Append('\n');
        }

        /// <summary>按执行序展平一张卡全部效果树的原子（观测槽位 [15..] 按此顺序编码）。</summary>
        private static void CollectAtomHashes(CardEffectData e, List<ulong> atoms)
        {
            if (e == null) return;
            if (e.Steps != null && e.Steps.Count > 0)
            {
                foreach (var s in e.Steps) CollectStepAtoms(s, atoms);
            }
            else if (e.AtomicEffects != null)
            {
                foreach (var a in e.AtomicEffects) atoms.Add(AtomHash(a));
            }
        }

        private static void CollectStepAtoms(EffectStepData s, List<ulong> atoms)
        {
            if (s == null) return;
            switch (s.kind)
            {
                case 0:
                    atoms.Add(AtomHash(s.atomic));
                    break;
                case 1:
                    if (s.thenSteps != null) foreach (var a in s.thenSteps) atoms.Add(AtomHash(a));
                    if (s.elseSteps != null) foreach (var a in s.elseSteps) atoms.Add(AtomHash(a));
                    break;
                case 2:
                    if (s.choices != null)
                        foreach (var mode in s.choices)
                            if (mode?.steps != null)
                                foreach (var ms in mode.steps) CollectStepAtoms(ms, atoms);
                    break;
            }
        }

        // ===================================================== 组合结构哈希（怎么组合） =====================================================

        /// <summary>组合结构 = 各效果的时点/发动字段 + 条件 + 代价 + Steps 骨架（原子全为占位符，只留
        /// kind/分支条件/各支原子数/抉择模式数）。「OnPlay 平铺 2 原子」与「OnPlay 平铺 1 原子」是
        /// 不同结构；结构相同的两张卡共享结构行，原子差异由原子槽承载。</summary>
        internal static ulong StructureHash(CardData card)
        {
            var sb = new StringBuilder();
            if (card.Effects != null)
                foreach (var e in card.Effects)
                {
                    if (e == null) { sb.Append("e;\n"); continue; }
                    sb.Append("T").Append(e.TriggerTiming).Append('|')
                      .Append(e.ActivationType).Append('|')
                      .Append(e.BaseSpeed).Append('|')
                      .Append(e.IsOptional ? 1 : 0).Append('|')
                      .Append(e.Duration).Append('\n');
                    AppendConditions(sb, "AC", e.ActivationConditions);
                    AppendConditions(sb, "TC", e.TriggerConditions);
                    AppendCosts(sb, e.Costs);
                    if (e.Steps != null && e.Steps.Count > 0)
                    {
                        sb.Append("S").Append(e.Steps.Count).Append('\n'); // Steps 顺序 = 执行序，保留
                        foreach (var s in e.Steps) AppendStepSkeleton(sb, s);
                    }
                    else
                    {
                        sb.Append("F").Append(e.AtomicEffects?.Count ?? 0).Append('\n'); // 扁平退化
                    }
                }
            return Mix(Fnv1a(sb.ToString()), TableFingerprint());
        }

        /// <summary>Steps 骨架（组合方式的枚举）：kind 0=原子占位 / 1=分支（条件+两支原子数）/ 2=抉择（模式数+各模式骨架递归）。</summary>
        private static void AppendStepSkeleton(StringBuilder sb, EffectStepData s)
        {
            if (s == null) { sb.Append("s;\n"); return; }
            switch (s.kind)
            {
                case 0:
                    sb.Append("k0\n"); // 原子占位（内容在原子槽/原子哈希）
                    break;
                case 1:
                    sb.Append("k1").Append(s.conditionId ?? string.Empty).Append('|')
                      .Append(s.conditionParam).Append('|')
                      .Append(s.conditionStringParam ?? string.Empty).Append('\n')
                      .Append('t').Append(s.thenSteps?.Count ?? 0).Append('\n')
                      .Append('e').Append(s.elseSteps?.Count ?? 0).Append('\n');
                    break;
                case 2:
                    sb.Append("k2c").Append(s.choices?.Count ?? 0).Append('\n');
                    if (s.choices != null)
                        foreach (var mode in s.choices)
                        {
                            sb.Append("m").Append(mode?.steps?.Count ?? 0).Append('\n'); // label 是展示名，不参与哈希
                            if (mode?.steps != null)
                                foreach (var ms in mode.steps) AppendStepSkeleton(sb, ms); // 模式内可含分支（不可再嵌 Choice）
                        }
                    break;
            }
        }

        private static void AppendConditions(StringBuilder sb, string tag, List<ActivationConditionData> conditions)
        {
            if (conditions == null || conditions.Count == 0) return;
            var parts = conditions
                .Select(c => c == null ? string.Empty
                    : $"{c.Type}|{c.Value}|{c.Value2}|{c.StringValue ?? string.Empty}|{(c.Negate ? 1 : 0)}")
                .OrderBy(p => p, StringComparer.Ordinal); // 合取无序 → 排序稳定
            sb.Append(tag).Append(string.Join(";", parts)).Append('\n');
        }

        private static void AppendCosts(StringBuilder sb, List<CostEntry> costs)
        {
            if (costs == null || costs.Count == 0) return;
            var parts = costs
                .Select(c => c == null ? string.Empty : $"{c.CostType}|{c.Value}|{c.ManaType}|{c.TurnDuration}")
                .OrderBy(p => p, StringComparer.Ordinal); // 可叠加无序 → 排序稳定
            sb.Append('C').Append(string.Join(";", parts)).Append('\n');
        }

        // ===================================================== 组合哈希（关键词/tag/光环） =====================================================

        internal static ulong CompositionHash(CardData card)
        {
            var sb = new StringBuilder();
            if (card.Keywords != null && card.Keywords.Count > 0)
                sb.Append('K').Append(string.Join(",", card.Keywords.OrderBy(k => k, StringComparer.Ordinal))).Append('\n');
            if (card.Tags != null && card.Tags.Count > 0)
                sb.Append('T').Append(string.Join(",", card.Tags.OrderBy(t => t, StringComparer.Ordinal))).Append('\n');
            if (card.LinkAuras != null && card.LinkAuras.Count > 0)
            {
                var parts = card.LinkAuras
                    .Select(l => l == null ? string.Empty : $"{l.stat ?? string.Empty}|{l.value}|{l.keyword ?? string.Empty}")
                    .OrderBy(p => p, StringComparer.Ordinal);
                sb.Append('A').Append(string.Join(";", parts)).Append('\n');
            }
            return Mix(Fnv1a(sb.ToString()), TableFingerprint());
        }

        // ===================================================== 哈希辅助 =====================================================

        private static ulong Fnv1a(string s)
        {
            unchecked
            {
                ulong h = 14695981039346656037UL;
                foreach (char ch in s)
                {
                    h ^= (byte)(ch & 0xFF); h *= 1099511628211UL;
                    h ^= (byte)(ch >> 8);   h *= 1099511628211UL;
                }
                return h;
            }
        }

        private static ulong Mix(ulong hash, ulong salt)
        {
            unchecked
            {
                for (int i = 0; i < 8; i++)
                {
                    hash ^= (salt >> (8 * i)) & 0xFFUL;
                    hash *= 1099511628211UL;
                }
                return hash;
            }
        }
    }
}
