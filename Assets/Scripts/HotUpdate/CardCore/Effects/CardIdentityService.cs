using System;
using System.Collections.Generic;
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
        ///   - 哈希直接锚定冻结 ID（2026-11-05 ID 冻结收口：拆除原子表指纹全局盐）——原子表内容
        ///     编辑/新增行不再换血训练身份；行为语义大改按冻结纪律走「新建行（新 ID）、旧行退役」，
        ///     与原子表 ID 台账同口径。内容漂移检测归 TextHash/hash 指纹列（工具链），不进身份。
    /// 属性（力量/生命/费用）不进身份：obs 已有实时特征（TideObservation 卡特征第 3/4/5 维），不重复。
    /// Id/DisplayName/Description/choices.label 是别名与展示，不参与（EffectChoiceData.label
    /// 注释「不参与哈希」的既有约定）。
    ///
    /// 稳定性口径：Steps/原子序列保留书写序（= 执行序）；条件/代价/关键词/tag/光环
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

        // 2026-11-05 ID 冻结收口：TableFingerprint 整表盐及其缓存一并拆除——身份哈希锚定冻结 ID，
        // 原子表内容编辑（含新增无人引用的行）不再触发训练身份换血。

        /// <summary>EffectType 下标冻结注册表（名→1 基下标，追加式）：首次访问按当前表枚举名排序
        /// 播种（与历史排序口径零漂移），此后新枚举名一律追加 max+1、永不重排——修复「新增行 →
        /// 字母序在后的类型全体移位」。分配经 TideCardIndex 清单（type 行）持久化跨进程续排。</summary>
        private static Dictionary<string, int> _typeRegistry;
        private static int _typeMaxIndex;
        private static bool _typeOverflowWarned;
        private static bool _typeAdopted;

        /// <summary>EffectType 下标容量上界（Python N_EFFECT_TYPES=256；0=无/未入表/溢出）。</summary>
        internal const int MaxTypeIndex = 255;

        /// <summary>表侧缓存失效（2026-11-05 口径收窄）：ID 冻结后身份哈希与表内容解耦，Reload
        /// 换表不再触发任何换血——类型注册表是冻结分配，**不清**（跨 Reload 存活，追加式不重排）。
        /// 保留方法给既有调用点（SpecMasterVerifier / EffectComposerScreen.SwitchTable）。幂等。</summary>
        public static void InvalidateTableCache()
        {
            // 保留注册表：见字段注释。语义从「清缓存待重算」收窄为占位，防未来误把冻结分配当缓存清掉。
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

        /// <summary>EffectType → 冻结下标（1 基；0 = 无/未入表/溢出）。追加式注册表：首访按枚举名
        /// 排序播种（与历史排序口径零漂移），新枚举名追加 max+1 永不重排——新增原子行不再挪动
        /// 任何已分配下标（2026-11-05 ID 冻结收口；分配经 TideCardIndex 清单 type 行持久化）。</summary>
        internal static int AtomTypeIndex(string effectType)
        {
            if (string.IsNullOrEmpty(effectType)) return 0;
            var reg = EnsureTypeRegistry();
            if (reg.TryGetValue(effectType, out int idx)) return idx;

            int next = _typeMaxIndex + 1;
            if (next > MaxTypeIndex)
            {
                if (!_typeOverflowWarned)
                {
                    _typeOverflowWarned = true;
                    TideLog.Error($"[CardIdentity] EffectType 下标溢出（>{MaxTypeIndex}，Python N_EFFECT_TYPES=256）：{effectType} 记 0——泛化通路降级，请扩容重导出");
                }
                return 0;
            }
            reg[effectType] = next;
            _typeMaxIndex = next;
            return next;
        }

        /// <summary>注册表取用：未初始化时按当前表枚举名排序播种（首访 = 历史排序口径，零漂移）。</summary>
        private static Dictionary<string, int> EnsureTypeRegistry()
        {
            if (_typeRegistry != null) return _typeRegistry;
            var names = AtomicEffectTable.GetAll()
                .Select(cfg => cfg.EnumName)
                .Where(n => !string.IsNullOrEmpty(n))
                .Distinct()
                .OrderBy(n => n, StringComparer.Ordinal) // GetAll 走字典 Values 序不稳，必须排序
                .ToList();
            var map = new Dictionary<string, int>(names.Count);
            for (int i = 0; i < names.Count; i++) map[names[i]] = i + 1;
            _typeRegistry = map;
            _typeMaxIndex = names.Count;
            return map;
        }

        /// <summary>清单载入侧 Adoption（TideCardIndex.ConfigureManifest 的 type 行）：清单分配是
        /// 权威——首次 Adoption 以清单为基重建（当前表新名按排序批追加 max+1），修复「装载期先播种、
        /// 清单后绑定」的时序错位；此后只补缺不重排（追加式铁律）。装载期按播种号暂存的下标由
        /// TideCardIndex.Register 里的 EnsureIdentity 重跑刷新。</summary>
        internal static void AdoptTypeIndexes(IReadOnlyDictionary<string, int> adopted)
        {
            if (adopted == null || adopted.Count == 0) return;
            if (_typeAdopted)
            {
                var reg = EnsureTypeRegistry();
                foreach (var kv in adopted)
                {
                    if (string.IsNullOrEmpty(kv.Key) || kv.Value < 1 || kv.Value > MaxTypeIndex) continue;
                    if (reg.ContainsKey(kv.Key)) continue; // 已冻结分配不重排
                    reg[kv.Key] = kv.Value;
                    if (kv.Value > _typeMaxIndex) _typeMaxIndex = kv.Value;
                }
                return;
            }
            _typeAdopted = true;

            var rebuilt = new Dictionary<string, int>(adopted.Count + 16);
            int max = 0;
            foreach (var kv in adopted)
            {
                if (string.IsNullOrEmpty(kv.Key) || kv.Value < 1 || kv.Value > MaxTypeIndex) continue;
                rebuilt[kv.Key] = kv.Value;
                if (kv.Value > max) max = kv.Value;
            }
            // 清单里没有的当前表名 = 清单落盘后新增 → 排序批追加（确定性，与装载时序无关）
            var missing = AtomicEffectTable.GetAll()
                .Select(cfg => cfg.EnumName)
                .Where(n => !string.IsNullOrEmpty(n) && !rebuilt.ContainsKey(n))
                .Distinct()
                .OrderBy(n => n, StringComparer.Ordinal);
            foreach (var n in missing)
            {
                if (max >= MaxTypeIndex) break;
                rebuilt[n] = ++max;
            }
            _typeRegistry = rebuilt;
            _typeMaxIndex = max;
        }

        /// <summary>注册表快照（TideCardIndex.SaveManifest 落盘 type 行用；取用即播种）。</summary>
        internal static IReadOnlyDictionary<string, int> TypeIndexSnapshot()
            => EnsureTypeRegistry();

        /// <summary>原子参数块（AtomParamDim 维，幅度 Clamp 到观测口径 [-99, 999]）。null 原子记全 0。
        /// 2026-09-10 目标域模型后 6 维：[0]Value [1]Mana总量 [2]Mana色数 [3]kind数 [4]最小kind [5]最大kind。</summary>
        private static void AppendAtomParams(List<float> parms, AtomicEffectEntry a)
        {
            if (a == null)
            {
                for (int i = 0; i < AtomParamDim; i++) parms.Add(0f);
                return;
            }
            // 2026-09-14 彻底引用化：参数向量随字段重构（ManaList 删除——两维归零；amp 入向量）
            var kinds = (a.kinds ?? new List<int>()).Distinct().OrderBy(k => k).ToList();
            parms.Add(Clamp(a.value));
            parms.Add(0f); // 原 mana 总量（ManaList 已删）
            parms.Add(0f); // 原 mana 条数
            parms.Add(Clamp(kinds.Count));
            parms.Add(kinds.Count > 0 ? kinds[0] : 0f);
            parms.Add(kinds.Count > 0 ? kinds[kinds.Count - 1] : 0f);
            parms.Add(Clamp(a.amp));
        }

        private static float Clamp(float v) => v < -99f ? -99f : (v > 999f ? 999f : v);

        // ===================================================== 原子哈希（身份共享单元） =====================================================

        /// <summary>原子 = 原子表条目（锚定冻结行 ID）+ 实例参数与目标覆盖——
        /// 「造成伤害 a4b823fc + target + value」这类单元，同原子跨卡共享 embedding 行。
        /// 2026-11-05 ID 冻结收口：拆整表指纹盐——身份 = 冻结 refId + 实例参数，表内容编辑不再换血。</summary>
        internal static ulong AtomHash(AtomicEffectEntry a)
        {
            if (a == null) return NullAtomHash; // 非 0：0 是 TideCardIndex 的「未登记」信号

            var sb = new StringBuilder();
            AppendAtom(sb, a);
            return Fnv1a(sb.ToString());
        }

        /// <summary>null 原子身份哨兵（全部 null 原子共一行；非 0——理由见 AtomHash）。</summary>
        private const ulong NullAtomHash = 0x6E756C6C61746F6DUL;

        /// <summary>原子的全部可变参数（2026-09-10 目标域模型：EffectType+Value+ID+Mana+TargetKinds；
        /// 编排属性已上移组合层——进 StructureHash 的效果级字段）。表行内容（费用/极性/域）不入哈希——
        /// 身份锚定冻结 refId，内容漂移检测归指纹列（工具链）。</summary>
        private static void AppendAtom(StringBuilder sb, AtomicEffectEntry a)
        {
            // 2026-09-14 彻底引用化：原子=表行引用+增量（与 ContentHasher 同形态口径）
            sb.Append(a.refId ?? string.Empty).Append('|')
              .Append(a.value).Append('|')
              .Append(a.str ?? string.Empty).Append('|')
              .Append(TargetKindRules.Format(a.kinds ?? new List<int>())).Append('|')
              .Append(a.amp.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('\n');
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
                    types.Add(AtomTypeIndex(AtomicEffectTable.GetByHashId(a?.refId)?.EnumName));
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
                    types.Add(AtomTypeIndex(AtomicEffectTable.GetByHashId(s.atomic?.refId)?.EnumName));
                    AppendAtomParams(parms, s.atomic);
                    break;
                case 1:
                    if (s.thenSteps != null)
                        foreach (var a in s.thenSteps)
                        {
                            atoms.Add(AtomHash(a));
                            types.Add(AtomTypeIndex(AtomicEffectTable.GetByHashId(a?.refId)?.EnumName));
                            AppendAtomParams(parms, a);
                        }
                    if (s.elseSteps != null)
                        foreach (var a in s.elseSteps)
                        {
                            atoms.Add(AtomHash(a));
                            types.Add(AtomTypeIndex(AtomicEffectTable.GetByHashId(a?.refId)?.EnumName));
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
                      .Append(e.Duration).Append('|').Append(e.SummonDropZone).Append('|').Append(e.SelectionMode).Append('|').Append(e.TargetCount).Append('|').Append(e.RandomTarget).Append('\n');
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
            return Fnv1a(sb.ToString()); // 2026-11-05 ID 冻结收口：拆整表盐（结构身份锚定冻结编排）
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
        /// 供重推导工具一键回写（Tools/重推导测试卡表费用与ID）。</summary>
        public static string ContentId(CardData card)
        {
            var sb = new StringBuilder();
            sb.Append(card.Supertype).Append('|').Append((int)card.ArrowDirections).Append('\n');
            sb.Append("P").Append(card.Power ?? int.MinValue).Append('|')
              .Append(card.Life ?? int.MinValue).Append('\n');
            if (card.Cost != null && !card.Cost.IsZero)
            {
                // 2026-10-04 位置数组化：序数序无损拼串（ToString=枚举名:R 值）
                sb.Append('C').Append(card.Cost.ToString()).Append('\n');
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
              .Append(e.Duration).Append('|').Append(e.SummonDropZone).Append('|').Append(e.SelectionMode).Append('|').Append(e.TargetCount).Append('|').Append(e.RandomTarget).Append('\n');
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
                // 2026-11-05 效果光环：effectRef 非空才追加段尾——存量折叠串字节不变（身份零漂移），
                // 防仅 effectRef 不同的两卡观测身份碰撞
                var parts = card.LinkAuras
                    .Select(l => l == null ? string.Empty
                        : $"{l.stat ?? string.Empty}|{l.value}|{l.keyword ?? string.Empty}"
                          + (string.IsNullOrEmpty(l.effectRef) ? "" : $"|{l.effectRef}"))
                    .OrderBy(p => p, StringComparer.Ordinal);
                sb.Append('A').Append(string.Join(";", parts)).Append('\n');
            }
            return Fnv1a(sb.ToString()); // 2026-11-05 ID 冻结收口：拆整表盐
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

        // Mix(hash, salt) 辅助已随 2026-11-05 整表盐拆除退役。
    }
}
