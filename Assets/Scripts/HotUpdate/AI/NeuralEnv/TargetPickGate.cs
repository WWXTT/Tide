using System;
using System.Collections.Generic;
using CardCore;
using CardCore.Attribute;

namespace CardCore.AI.NeuralEnv
{
    /// <summary>A6（2026-11-24 回到一期训练目标·逐目标单选）多步拾取计划：
    /// 一个（卡×模式×效果）的模型侧目标选择序列规格——由 LAE 在展开期构建，
    /// 单步行（剩余恰 1 步）不建计划（旧行为直发），多步行/含可变数量 spec 才建。
    /// 计划序 = CollectSpecPlan 收集序 = 引擎预选队列消费序（提交行 Targets 顺序单源，
    /// 提示③：顺序错 = 结算错）。拾取期间引擎状态冻结（pick 步不 Apply），候选快照有效。</summary>
    public sealed class TargetPickPlan
    {
        /// <summary>spec 档型：Forced=引擎代选占位（AutoTake 前 N / 全取，不占模型选择步）；
        /// Fixed=固定 k 步（收满自动前移）；Variable=可变数量（k≥1，须显式提交行收口）。</summary>
        public enum SpecKind { Forced, Fixed, Variable }

        /// <summary>计划中的一步：一个 CollectSpecPlan spec 的模型侧档型 + 候选快照。</summary>
        public sealed class Spec
        {
            public SpecKind Kind;
            /// <summary>Fixed 需选数（≥1；Variable/Forced 不用）。</summary>
            public int K;
            /// <summary>候选快照（引擎 ResolveCandidates 返回序 / prefix 共享序——
            /// 提交行规范化排序的单源：已选按此序重排 = 旧组合行序）。</summary>
            public List<Entity> Candidates;
            /// <summary>动态计价原子位（-3 动态数量才用；-1=非动态）。</summary>
            public int AtomIndex = -1;
            /// <summary>是否动态数量（-3：Counts 重建 + base×N 可付性参与存在性检查）。</summary>
            public bool Dynamic;
            /// <summary>Forced：引擎代选占位目标（AutoTake 前 N / 全取候选）。</summary>
            public List<Entity> ForcedTargets;
            /// <summary>可变数量档（Variable=true 时有效）：k 下限恒 1（旧子集行 k=1..N 口径，
            /// 「选 0 个不提交」）。</summary>
            public bool Variable => Kind == SpecKind.Variable;
        }

        /// <summary>计划序 spec 表（含 Forced 占位——提交行按此序拼接）。</summary>
        public List<Spec> Specs = new List<Spec>();
        /// <summary>Fixed 档需选总数（首步行 [10]；0=纯 Forced/含可变）。</summary>
        public int FixedTotal;
        /// <summary>含可变数量 spec（首步行/闸内候选行 [10] 恒 -1=开放步数）。</summary>
        public bool HasVariable;
        /// <summary>含动态数量 spec（提交行 Counts 重建 + 可付性存在性检查）。</summary>
        public bool HasDynamic;
        /// <summary>Counts 向量长（def.Effects.Count；header 口径 0=不用）。</summary>
        public int AtomCount;
        /// <summary>可付性单源（DynamicCountAffordable；null=不检查）。</summary>
        public EffectDefinition Def;
        /// <summary>首个模型 spec 是否参照角色（Reference 镜像：该 spec 候选行 Reference=候选）。</summary>
        public bool FirstSpecIsReference;
        /// <summary>调试键（卡|效果 Id）。</summary>
        public string Key;

        /// <summary>首个模型选择 spec 下标（-1=无）。</summary>
        public int FirstModelSpecIndex()
        {
            for (int i = 0; i < Specs.Count; i++)
                if (Specs[i].Kind != SpecKind.Forced) return i;
            return -1;
        }

        /// <summary>按已选快照算 Fixed 档剩余需选总数（闸内候选行 [10]）。</summary>
        public int FixedRemaining(List<List<Entity>> picked)
        {
            int sum = 0;
            for (int i = 0; i < Specs.Count; i++)
            {
                if (Specs[i].Kind != SpecKind.Fixed) continue;
                int got = i < picked.Count && picked[i] != null ? picked[i].Count : 0;
                sum += Math.Max(0, Specs[i].K - got);
            }
            return sum;
        }
    }

    /// <summary>A6（2026-11-24 回到一期训练目标·逐目标单选）目标拾取闸：多步拾取序列的
    /// 驱动层状态机——**引擎零参与**（与 HandLimitDiscardGate 的引擎挂起不同）：拾取步
    /// 只入闸不 Apply，状态冻结；收满（Fixed）/提交行（Variable）后由
    /// <see cref="BuildFinal"/> 按计划序规范化拼全集塞回 TideAction.Targets/DynamicCounts，
    /// 走原 Apply（或 D2d 停靠泵续跑）——「同目标集同结算」的根基（提示⑦：脚本/玩家路径
    /// 不经本闸）。行发射在 LegalActionEnumerator.EnumeratePickSteps（独立枚举口，
    /// 镜像 EnumerateDiscardChoices：不折叠/不摘除/无兜底行）。
    /// 清理点：提交完成 / 驱动 Reset/Dispose / 终局 / 越界与强收兜底（漏一个就会把上一张卡
    /// 的已选带到下一张——提示④）。</summary>
    public static class TargetPickGate
    {
        private static TargetPickPlan _plan;
        private static TideAction _proto;                    // 首步行拷贝（交互/落格/召唤计划/参照随行）
        private static List<List<Entity>> _picked;           // 每 spec 已选（计划序）
        private static bool[] _specDone;                     // Variable 显式提交 / Fixed 收满
        private static ResponseOption _parked;               // 来源=D2d 停靠表时的候选对象（null=主表）
        private static bool _completed;

        /// <summary>拾取序列进行中（BuildResult 据此切换闸表枚举）。</summary>
        public static bool HasPending => _plan != null;

        /// <summary>序列已收口（BuildFinal 前置判据）。</summary>
        public static bool Completed => _completed;

        public static TargetPickPlan PendingPlan => _plan;
        public static TideAction Prototype => _proto;
        public static List<List<Entity>> Picked => _picked;

        /// <summary>当前拾取 spec 下标：首个未完成模型 spec（Fixed 未收满 / Variable 未提交）；
        /// -1=无（序列已收口）。</summary>
        public static int CurrentSpecIndex
        {
            get
            {
                for (int i = 0; i < _plan.Specs.Count; i++)
                {
                    var s = _plan.Specs[i];
                    if (s.Kind == TargetPickPlan.SpecKind.Forced) continue;
                    bool done = _specDone[i] || (s.Kind == TargetPickPlan.SpecKind.Fixed
                        && _picked[i].Count >= s.K);
                    if (!done) return i;
                }
                return -1;
            }
        }

        /// <summary>开闸（驱动 Step：模型选中首步行 PickPlan!=null）。首步行的目标即第一拾取。
        /// v22 S2a 拆出 <see cref="OpenCore"/>（选项行 Targets=null 不走本口）。</summary>
        public static void Open(TideAction firstRow, ResponseOption parkedOption)
        {
            if (!OpenCore(firstRow, parkedOption)) return;
            if (firstRow.Targets == null || firstRow.Targets.Count == 0)
                return; // 防御：非计划行/无目标行不开闸
            var t = firstRow.Targets[0];
            int cur = CurrentSpecIndex;
            if (cur >= 0 && !AlreadyPicked(cur, t)) _picked[cur].Add(t);
            RecomputeCompleted();
        }

        /// <summary>开闸初始化（v22 S2a 拆出，Open/OpenAndAutoComplete 共用）：proto 拷贝自首行/
        /// 选项行（Targets/PickPlan 清空、PicksRemaining=1），拾取状态清零。plan 缺失=false。</summary>
        private static bool OpenCore(TideAction firstRow, ResponseOption parkedOption)
        {
            var plan = firstRow.PickPlan;
            if (plan == null) return false;
            _plan = plan;
            _proto = firstRow; // struct 拷贝
            _proto.Targets = null;
            _proto.PickPlan = null;
            _proto.PicksRemaining = 1;
            _parked = parkedOption;
            _picked = new List<List<Entity>>();
            _specDone = new bool[plan.Specs.Count];
            foreach (var s in plan.Specs) _picked.Add(new List<Entity>());
            _completed = false;
            return true;
        }

        /// <summary>v22 S2a（时域/粒度修复，docs/二期训练架构_实施计划.md §4）：选项提交行
        /// （PickMode=1，Targets=null）的一次性完成——开闸后按计划序用**候选快照序**自动选满：
        /// Fixed=前 K 个候选；Variable=全选（动态可付不满足时从尾裁剪，留 ≥1 交引擎终审）。
        /// 候选序=计划构建时的 HeroSkillTargetPolicy 共享序（牺牲/蛰伏 OrderSacrifice/OrderDormancy
        /// 与脚本 GreenRampStrategy 同源）——**不新写选目标策略**，「选哪几张」=共享序前缀。
        /// BuildFinal 后调用方必须 <see cref="ConsumeParked"/> 取停靠来源并清闸。
        /// 返回开闸失败防御：原行直发（引擎终审拒绝 → 驱动重观测）。</summary>
        public static TideAction OpenAndAutoComplete(GameCore core, Player me, TideAction optionRow,
            ResponseOption parkedOption)
        {
            if (!OpenCore(optionRow, parkedOption)) return optionRow;
            for (int i = 0; i < _plan.Specs.Count; i++)
            {
                var s = _plan.Specs[i];
                if (s.Kind == TargetPickPlan.SpecKind.Forced || s.Candidates == null) continue;
                if (s.Kind == TargetPickPlan.SpecKind.Fixed)
                {
                    for (int k = 0; k < s.K && k < s.Candidates.Count; k++)
                        _picked[i].Add(s.Candidates[k]); // 共享序前 K（候选不足=全取，BuildFinal 交引擎）
                }
                else // Variable：全选；动态计价不可付时从尾裁剪（非动态恒可付不裁）
                {
                    foreach (var c in s.Candidates) _picked[i].Add(c);
                    while (_picked[i].Count > 1
                           && !CompletionExists(core, me, _plan, _picked, i, 0))
                        _picked[i].RemoveAt(_picked[i].Count - 1);
                }
            }
            RecomputeCompleted();
            return BuildFinal(core, me);
        }

        /// <summary>拾取步（闸内候选行）：本步只收 1 个目标，不 Apply、不推进。</summary>
        public static void Pick(TideAction row)
        {
            int cur = CurrentSpecIndex;
            var t = row.Targets != null && row.Targets.Count > 0 ? row.Targets[0] : null;
            if (cur < 0 || t == null || AlreadyPicked(cur, t)) { Reset(); return; } // 防御：异常步弃闸
            _picked[cur].Add(t);
            RecomputeCompleted();
        }

        /// <summary>提交行（[10]=0、Targets=已选集合）：锁定当前 Variable spec 的数量。
        /// 非末位 spec=进入下一 spec；末位=序列收口（Completed）。</summary>
        public static void Submit()
        {
            int cur = CurrentSpecIndex;
            if (cur >= 0 && _plan.Specs[cur].Variable && _picked[cur].Count >= 1)
                _specDone[cur] = true;
            RecomputeCompleted();
        }

        /// <summary>构造最终动作（提交/收满后）：proto + 按计划序规范化拼全集 + Counts 重建。
        /// 调用即收口清闸；配对 <see cref="ConsumeParked"/> 取停靠来源（null=主表）。</summary>
        public static TideAction BuildFinal(GameCore core, Player me)
        {
            var final = _proto;
            var targets = CanonicalSoFar();
            final.Targets = targets;
            final.Target = targets.Count > 0 ? targets[0] : null;
            final.DynamicCounts = BuildCounts(_plan, _picked);
            final.PicksRemaining = 1;
            final.PickPlan = null;
            // 索引特征字段重建（提交后日志/仪表消费；Apply 只读 Targets/DynamicCounts）
            if (targets.Count > 0 && targets[0] is Card tc)
            {
                final.TargetIndex = TideObservation.IndexOfCard(core, me, tc);
                final.TargetKindCode = 1;
            }
            else if (targets.Count > 0)
            {
                final.TargetIndex = -1;
                final.TargetKindCode = ReferenceEquals(targets[0], me) ? 3 : 2;
            }
            else { final.TargetIndex = -1; final.TargetKindCode = 0; }
            // A6 契约：次目标维（TargetIndex2/3）已随组合行退役删除——[8]/[9] 恒 -1
            return final;
        }

        /// <summary>取走停靠来源并清闸（BuildFinal 后调用一次；null=主表来源走 Apply）。</summary>
        public static ResponseOption ConsumeParked()
        {
            var p = _parked;
            Reset();
            return p;
        }

        /// <summary>规范化已选拼接（提交行 Targets 与 BuildFinal 共用）：计划序，Forced 占位
        /// 在位、各 spec 已选按候选序重排（= 旧组合行序，提示③）。</summary>
        public static List<Entity> CanonicalSoFar()
        {
            var list = new List<Entity>();
            for (int i = 0; i < _plan.Specs.Count; i++)
            {
                var s = _plan.Specs[i];
                if (s.Kind == TargetPickPlan.SpecKind.Forced)
                {
                    if (s.ForcedTargets != null) list.AddRange(s.ForcedTargets);
                    continue;
                }
                list.AddRange(OrderedByCandidates(s, _picked[i]));
            }
            return list;
        }

        /// <summary>当前已选的 Counts 向量（提交行 [5] 费用可见用；null=无动态）。</summary>
        public static List<int> BuildCountsForCurrent() => BuildCounts(_plan, _picked);

        public static bool AlreadyPicked(int specIndex, Entity e)
        {
            if (specIndex < 0 || specIndex >= _picked.Count || e == null) return false;
            foreach (var p in _picked[specIndex]) if (ReferenceEquals(p, e)) return true;
            return false;
        }

        /// <summary>整体复位（驱动 Reset/Dispose/终局/越界与强收兜底）。</summary>
        public static void Reset()
        {
            _plan = null;
            _proto = default;
            _picked = null;
            _specDone = null;
            _parked = null;
            _completed = false;
        }

        /// <summary>可付完成存在性（动态数量预检，替代旧的整行 RemoveAll——A6 后 TargetSet
        /// 不再携带 Counts）：给定已选快照，当前 spec 再收 1 个（mode=1，候选行）或定格
        /// （mode=0，提交行）后，是否存在 base×N 可付的完成。后续动态 spec k 取
        /// [picked, N]（当前）/ [1, N]（未开始，旧子集行 k≥1 口径），笛卡尔积 ≤4096 组合查
        /// DynamicCountAffordable（单源 LAE 同款）。无动态 spec 恒 true（旧口径：Counts=null
        /// 不预滤）。</summary>
        public static bool CompletionExists(GameCore core, Player me, TargetPickPlan plan,
            List<List<Entity>> picked, int currentSpec, int mode)
        {
            if (plan == null || !plan.HasDynamic || plan.Def == null) return true;
            var idxs = new List<int>();
            var lo = new List<int>();
            var hi = new List<int>();
            for (int i = 0; i < plan.Specs.Count; i++)
            {
                var s = plan.Specs[i];
                if (!s.Dynamic) continue;
                int cur = i < picked.Count && picked[i] != null ? picked[i].Count : 0;
                int l, h;
                if (i == currentSpec && mode == 1) { l = cur + 1; h = s.Candidates.Count; }
                else if (i == currentSpec) { l = cur; h = cur; }
                else if (i > currentSpec) { l = 1; h = s.Candidates.Count; }
                else { l = cur; h = cur; }
                if (l > h) return false;
                idxs.Add(i); lo.Add(l); hi.Add(h);
            }
            if (idxs.Count == 0) return true;
            // 防御上限：组合积超 4096 时从最宽区间起收窄（欠近似——正常计划 ≤3 动态 spec×≤10 候选）
            while (ProductOf(hi, lo) > 4096)
            {
                int widest = 0;
                for (int i = 1; i < hi.Count; i++)
                    if (hi[i] - lo[i] > hi[widest] - lo[widest]) widest = i;
                if (hi[widest] <= lo[widest]) break;
                hi[widest]--;
            }
            // 里程表枚举各动态 spec 的 k 组合
            var counts = new List<int>(plan.AtomCount);
            for (int i = 0; i < plan.AtomCount; i++) counts.Add(0);
            var k = new List<int>(lo);
            while (true)
            {
                for (int i = 0; i < idxs.Count; i++)
                    if (idxs[i] < counts.Count) counts[idxs[i]] = k[i];
                if (DynamicCountAffordable(core, me, plan.Def, counts)) return true;
                int p = idxs.Count - 1;
                while (p >= 0 && k[p] == hi[p]) p--;
                if (p < 0) return false;
                k[p]++;
                for (int i = p + 1; i < idxs.Count; i++) k[i] = lo[i];
            }
        }

        private static long ProductOf(List<int> hi, List<int> lo)
        {
            long p = 1;
            for (int i = 0; i < hi.Count; i++) p *= hi[i] - lo[i] + 1;
            return p;
        }

        private static void RecomputeCompleted()
        {
            _completed = _plan != null && CurrentSpecIndex < 0;
        }

        private static List<Entity> OrderedByCandidates(TargetPickPlan.Spec s, List<Entity> picked)
        {
            if (picked == null || picked.Count <= 1) return picked == null
                ? new List<Entity>() : new List<Entity>(picked);
            var ordered = new List<Entity>(picked.Count);
            if (s.Candidates != null)
                foreach (var c in s.Candidates)
                    foreach (var p in picked)
                        if (ReferenceEquals(c, p)) { ordered.Add(c); break; }
            if (ordered.Count != picked.Count) return new List<Entity>(picked); // 防御：不在候选内按原序
            return ordered;
        }

        private static List<int> BuildCounts(TargetPickPlan plan, List<List<Entity>> picked)
        {
            if (!plan.HasDynamic) return null;
            var counts = new List<int>(plan.AtomCount);
            for (int i = 0; i < plan.AtomCount; i++) counts.Add(0);
            for (int i = 0; i < plan.Specs.Count; i++)
            {
                var s = plan.Specs[i];
                if (s.Dynamic && s.AtomIndex >= 0 && s.AtomIndex < counts.Count && picked[i] != null)
                    counts[s.AtomIndex] = picked[i].Count;
            }
            return counts;
        }

        /// <summary>动态数量粗检（与 LAE 旧 ExpandCardTargets 预滤同式——base×N 总费 ≤ 可用总量）。
        /// 提示：精确逐色可付性仍由 Apply 时引擎终审。</summary>
        private static bool DynamicCountAffordable(GameCore core, Player me, EffectDefinition def,
            IReadOnlyList<int> counts)
        {
            if (counts == null || def == null) return true;
            var cost = CostDerivationService.DeriveElementCosts(def, 0, counts);
            if (cost == null || cost.Total <= 0f) return true;
            return cost.Total <= core.ElementPool.GetTotalAvailableMana(me) + 0.001f;
        }
    }
}
