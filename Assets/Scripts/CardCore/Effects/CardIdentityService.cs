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
        /// <summary>原子参数浮点布局（观测参数槽 / Python ATOM_PARAM_DIM 对齐，逐原子 7 维）：
        /// [0]Value [1]Value2 [2]ManaTypeParam [3]Duration [4]DurationValue [5]TargetCountOverride
        /// [6]DynamicTargetCount。幅度由 AppendAtomParams 统一 Clamp（与观测数值特征同口径）。
        /// 与类型下标构成「参数数值通路」：EffectType 嵌入行跨参数共享 + 参数线性投影 →
        /// 「造成5伤」可从「造成4伤」插值迁移（精确哈希行另走通路，双保险；见 TideObservation [23..]）。</summary>
        public const int AtomParamDim = 6; // 2026-09-10 目标域模型：Value/Mana总量/Mana色数/kind数/min/max

        private static ulong? _tableFingerprint;
        private static Dictionary<string, int> _typeIndexMap;

        /// <summary>原子表整表指纹（行为字段按 EnumName 排序后哈希；GetAll 走字典 Values 序不稳，必须排序。Id 是装载期自增，排除）。</summary>
        public static ulong TableFingerprint()
        {
            if (_tableFingerprint.HasValue) return _tableFingerprint.Value;

            var sb = new StringBuilder();
            foreach (var cfg in AtomicEffectTable.GetAll().OrderBy(c => c.EnumName, StringComparer.Ordinal))
            {
                // 2026-09-10 目标域模型：TargetKinds/SelectionMode 取代 TargetType/Scope；持续列已删（上移组合层）
                sb.Append(cfg.EnumName).Append('|')
                  .Append(cfg.BaseCost.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                  .Append(TargetKindRules.Format(cfg.GetTargetKindList())).Append('|')
                  .Append(cfg.TargetFilter ?? string.Empty).Append('|')
                  .Append(cfg.Polarity.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
            }
            _tableFingerprint = Fnv1a(sb.ToString());
            return _tableFingerprint.Value;
        }

        /// <summary>推导并写回卡的内容身份（EnsureCost 顶部调用；幂等，重算便宜）。token 等未经装载管线的卡保持空身份（观测侧记 0）。
        /// 身份拆散到原子级：原子哈希数组（跨效果按执行序展平）+ 组合结构哈希 + 关键词/tag/光环组合哈希；
        /// 另附 EffectType 下标数组与参数浮点块（与哈希同序），供观测的参数数值泛化通路。</summary>
        public static void EnsureIdentity(CardData card)
        {
            if (card == null) return;

            var atoms = new List<ulong>();
            var types = new List<int>();
            var parms = new List<float>();
            if (card.Effects != null)
                foreach (var e in card.Effects)
                    CollectAtoms(e, atoms, types, parms);
            card.SetIdentity(atoms.ToArray(), StructureHash(card), CompositionHash(card),
                types.ToArray(), parms.ToArray());
        }

        // ===================================================== 参数数值通路（EffectType 下标 + 参数块） =====================================================

        /// <summary>EffectType → 原子表内序号（英文枚举名 Ordinal 排序，1 基）。表冻结 → 稳定；
        /// 表变更全体身份换血（表指纹已混入全部哈希），序号随之换血本该如此。0 = 无/未入表。</summary>
        internal static int AtomTypeIndex(string effectType)
        {
            if (string.IsNullOrEmpty(effectType)) return 0;
            return TypeIndexMap().TryGetValue(effectType, out int idx) ? idx : 0;
        }

        private static Dictionary<string, int> TypeIndexMap()
        {
            if (_typeIndexMap != null) return _typeIndexMap;
            var names = AtomicEffectTable.GetAll()
                .Select(cfg => cfg.EnumName)
                .Where(n => !string.IsNullOrEmpty(n))
                .OrderBy(n => n, StringComparer.Ordinal) // GetAll 走字典 Values 序不稳，必须排序
                .ToList();
            var map = new Dictionary<string, int>(names.Count);
            for (int i = 0; i < names.Count; i++) map[names[i]] = i + 1;
            _typeIndexMap = map;
            return map;
        }

        /// <summary>原子参数块（AtomParamDim 维，幅度 Clamp 到观测口径 [-99, 999]）。null 原子记全 0。
        /// 2026-09-10 目标域模型后 6 维：[0]Value [1]Mana总量 [2]Mana色数 [3]kind数 [4]最小kind [5]最大kind。</summary>
        private static void AppendAtomParams(List<float> parms, AtomicEffectEntry a)
        {
            if (a == null)
            {
                for (int i = 0; i < AtomParamDim; i++) parms.Add(0f);
                return;
            }
            var kinds = (a.TargetKinds ?? new List<int>()).Distinct().OrderBy(k => k).ToList();
            var mana = a.ManaList ?? new List<ManaAmountEntry>();
            parms.Add(Clamp(a.Value));
            parms.Add(Clamp(mana.Sum(m => m.amount)));
            parms.Add(Clamp(mana.Count));
            parms.Add(Clamp(kinds.Count));
            parms.Add(kinds.Count > 0 ? kinds[0] : 0f);
            parms.Add(kinds.Count > 0 ? kinds[kinds.Count - 1] : 0f);
        }

        private static float Clamp(float v) => v < -99f ? -99f : (v > 999f ? 999f : v);

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

        /// <summary>原子的全部可变参数（2026-09-10 目标域模型：EffectType+Value+ID+Mana+TargetKinds；
        /// 编排属性已上移组合层——进 StructureHash 的效果级字段）。表级默认经整表指纹全局覆盖。</summary>
        private static void AppendAtom(StringBuilder sb, AtomicEffectEntry a)
        {
            sb.Append(a.EffectType ?? string.Empty).Append('|')
              .Append(a.Value).Append('|')
              .Append(a.ID ?? string.Empty).Append('|')
              .Append(FormatMana(a.ManaList)).Append('|')
              .Append(TargetKindRules.Format(a.TargetKinds ?? new List<int>())).Append('\n');
        }

        /// <summary>Mana 规范串（按 ManaType 排序的 type:amount；空 = "-"）。</summary>
        private static string FormatMana(List<ManaAmountEntry> list)
        {
            if (list == null || list.Count == 0) return "-";
            return string.Join(";", list.OrderBy(m => m.manaType).Select(m => $"{m.manaType}:{m.amount.ToString("R", CultureInfo.InvariantCulture)}"));
        }

        /// <summary>按执行序展平一张卡全部效果树的原子（观测槽位按此顺序编码）。
        /// 三个平行列表同序追加：精确哈希 / EffectType 下标 / 参数块——哈希计算保持逐字节不变
        /// （历史清单与既有分配不受本次扩展影响）。</summary>
        private static void CollectAtoms(CardEffectData e, List<ulong> atoms, List<int> types, List<float> parms)
        {
            if (e == null) return;
            if (e.Steps != null && e.Steps.Count > 0)
            {
                foreach (var s in e.Steps) CollectStepAtoms(s, atoms, types, parms);
            }
            else if (e.AtomicEffects != null)
            {
                foreach (var a in e.AtomicEffects)
                {
                    atoms.Add(AtomHash(a));
                    types.Add(AtomTypeIndex(a?.EffectType));
                    AppendAtomParams(parms, a);
                }
            }
        }

        private static void CollectStepAtoms(EffectStepData s, List<ulong> atoms, List<int> types, List<float> parms)
        {
            if (s == null) return;
            switch (s.kind)
            {
                case 0:
                    atoms.Add(AtomHash(s.atomic));
                    types.Add(AtomTypeIndex(s.atomic?.EffectType));
                    AppendAtomParams(parms, s.atomic);
                    break;
                case 1:
                    if (s.thenSteps != null)
                        foreach (var a in s.thenSteps)
                        {
                            atoms.Add(AtomHash(a));
                            types.Add(AtomTypeIndex(a?.EffectType));
                            AppendAtomParams(parms, a);
                        }
                    if (s.elseSteps != null)
                        foreach (var a in s.elseSteps)
                        {
                            atoms.Add(AtomHash(a));
                            types.Add(AtomTypeIndex(a?.EffectType));
                            AppendAtomParams(parms, a);
                        }
                    break;
                case 2:
                    if (s.choices != null)
                        foreach (var mode in s.choices)
                            if (mode?.steps != null)
                                foreach (var ms in mode.steps) CollectStepAtoms(ms, atoms, types, parms);
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
                      .Append(e.Duration).Append('|').Append(e.DurationValue).Append('|').Append(e.SummonDropZone).Append('|').Append(e.SelectionMode).Append('|').Append(e.TargetCount).Append('|').Append(e.DynamicTargetCount ? 1 : 0).Append('|').Append(string.Join(",", (e.Drawbacks ?? new List<string>()).OrderBy(d => d, StringComparer.Ordinal))).Append('\n');
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

        // ===================================================== 卡内容 ID（重推导工具回写用） =====================================================

        /// <summary>卡内容 ID（16 位十六进制小写）：全部内容字段的 FNV-1a——属性/费用/关键词/
        /// tag/光环/效果全文（时点/条件/代价/Steps 骨架 + 原子全参数）。排除纯展示字段
        /// （cardName、效果 DisplayName/Description、effect.Id 内嵌卡号、Illustration——
        /// 这几个随便改名不动 ID）与卡 id 自身（避免循环）。同内容卡（跨文件）同 ID；
        /// 无序集合（关键词/tag/光环/条件/代价）规范化排序后哈希，手抖调行序不变 ID。
        /// 与身份哈希的分工：身份拆散到原子级供 embedding 共享；内容 ID 是整卡别名，
        /// 供重推导工具一键回写（Tools/重推导测试卡表费用）。</summary>
        public static string ContentId(CardData card)
        {
            var sb = new StringBuilder();
            sb.Append(card.Supertype).Append('|').Append((int)card.Subtype).Append('|')
              .Append(card.Level ?? -1).Append('|').Append(card.Rank ?? -1).Append('|')
              .Append(card.LinkRating ?? -1).Append('|').Append((int)card.ArrowDirections).Append('\n');
            sb.Append("P").Append(card.Power ?? int.MinValue).Append('|')
              .Append(card.Life ?? int.MinValue).Append('\n');
            if (card.Cost != null && card.Cost.Count > 0)
            {
                var parts = card.Cost.OrderBy(kv => kv.Key)
                    .Select(kv => $"{kv.Key}:{kv.Value.ToString("0.###", CultureInfo.InvariantCulture)}");
                sb.Append('C').Append(string.Join(";", parts)).Append('\n');
            }
            if (card.Keywords != null && card.Keywords.Count > 0)
                sb.Append('K').Append(string.Join(",", card.Keywords.OrderBy(k => k, StringComparer.Ordinal))).Append('\n');
            if (card.Tags != null && card.Tags.Count > 0)
                sb.Append('T').Append(string.Join(",", card.Tags.OrderBy(t => t, StringComparer.Ordinal))).Append('\n');
            if (card.LinkAuras != null && card.LinkAuras.Count > 0)
            {
                var parts = card.LinkAuras
                    .Select(l => l == null ? string.Empty : $"{l.stat}|{l.value}|{l.keyword}")
                    .OrderBy(p => p, StringComparer.Ordinal);
                sb.Append('A').Append(string.Join(";", parts)).Append('\n');
            }
            if (card.Effects != null)
            {
                sb.Append("E").Append(card.Effects.Count).Append('\n'); // 效果序列 = 书写序（执行序），保留
                foreach (var e in card.Effects) AppendEffectForId(sb, e);
            }
            return Fnv1a(sb.ToString()).ToString("x16");
        }

        /// <summary>效果全文（除 Id/DisplayName/Description 三个展示位）：时点/发动/条件/代价/Tags + Steps 骨架与原子。</summary>
        private static void AppendEffectForId(StringBuilder sb, CardEffectData e)
        {
            if (e == null) { sb.Append("e;\n"); return; }
            sb.Append("X").Append(e.TriggerTiming).Append('|')
              .Append(e.ActivationType).Append('|')
              .Append(e.BaseSpeed).Append('|')
              .Append(e.IsOptional ? 1 : 0).Append('|')
              .Append(e.Duration).Append('|').Append(e.DurationValue).Append('|').Append(e.SummonDropZone).Append('|').Append(e.SelectionMode).Append('|').Append(e.TargetCount).Append('|').Append(e.DynamicTargetCount ? 1 : 0).Append('|').Append(string.Join(",", (e.Drawbacks ?? new List<string>()).OrderBy(d => d, StringComparer.Ordinal))).Append('\n');
            AppendConditions(sb, "AC", e.ActivationConditions);
            AppendConditions(sb, "TC", e.TriggerConditions);
            AppendCosts(sb, e.Costs);
            if (e.Tags != null && e.Tags.Count > 0)
                sb.Append("t").Append(string.Join(",", e.Tags.OrderBy(t => t, StringComparer.Ordinal))).Append('\n');
            if (e.Steps != null && e.Steps.Count > 0)
            {
                sb.Append("S").Append(e.Steps.Count).Append('\n');
                foreach (var s in e.Steps) AppendStepForId(sb, s);
            }
            else if (e.AtomicEffects != null)
            {
                sb.Append("F").Append(e.AtomicEffects.Count).Append('\n');
                foreach (var a in e.AtomicEffects) AppendAtom(sb, a);
            }
        }

        /// <summary>Steps 全文（骨架 + 各支原子全参数；choices.label 是展示名不进）。</summary>
        private static void AppendStepForId(StringBuilder sb, EffectStepData s)
        {
            if (s == null) { sb.Append("s;\n"); return; }
            switch (s.kind)
            {
                case 0:
                    sb.Append("k0\n");
                    if (s.atomic != null) AppendAtom(sb, s.atomic);
                    break;
                case 1:
                    sb.Append("k1").Append(s.conditionId ?? string.Empty).Append('|')
                      .Append(s.conditionParam).Append('|')
                      .Append(s.conditionStringParam ?? string.Empty).Append('\n');
                    if (s.thenSteps != null)
                    {
                        sb.Append('t').Append(s.thenSteps.Count).Append('\n');
                        foreach (var a in s.thenSteps) AppendAtom(sb, a);
                    }
                    if (s.elseSteps != null)
                    {
                        sb.Append('e').Append(s.elseSteps.Count).Append('\n');
                        foreach (var a in s.elseSteps) AppendAtom(sb, a);
                    }
                    break;
                case 2:
                    sb.Append("k2c").Append(s.choices?.Count ?? 0).Append('\n');
                    if (s.choices != null)
                        foreach (var mode in s.choices)
                        {
                            sb.Append("m").Append(mode?.steps?.Count ?? 0).Append('\n');
                            if (mode?.steps != null)
                                foreach (var ms in mode.steps) AppendStepForId(sb, ms);
                        }
                    break;
            }
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
