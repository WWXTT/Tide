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
        /// <summary>原子参数浮点布局（v19 2026-11-20 绿套补盲扩维；观测参数槽 / Python ATOM_PARAM_DIM 对齐，逐原子 **9** 维）：
        /// [0]value（引擎主干原子=解析后门槛 x：显式 value&gt;1 优先 → engineParam&gt;0 → Then/effectId 锚价推导兜底；
        ///          普通原子（含开 deathToll 增幅的原子）=本体声明值）
        /// [1]settleKind（0=无分支 / 1=有限分支 Gate / 2=产出条件 Outcome / 3=事件引擎 Engine——分支形态可见）
        /// [2]branchParam（settle=2 → condParam；settle=3 → engineParam；无分支=0。死亡计数 2026-11-20
        ///          原子属性化后不走分支——增幅开关=原子 deathToll 布尔，计数实时值在卡特征 [152] 死亡计数维）
        /// [3]branchBudget（settle=1 → 门槛行 Budget 列（灰，奖励锚价上限）；其余=0）
        /// [4]kinds.Count [5]kinds[0] [6]kinds[last] [7]amp（沿用 2026-09-14 目标域模型四槽）
        /// [8]costTotal（该原子所属效果 e.Costs 合计全价 ≤3——「代价进观测」的直接落点，同效果原子共值冗余无害）
        /// 幅度由 AppendAtomParams 统一 Clamp（与观测数值特征同口径 [-99,999]）。
        /// 与类型下标构成「参数数值通路」：EffectType 嵌入行跨参数共享 + 参数线性投影 →
        /// 「造成5伤」可从「造成4伤」插值迁移（精确哈希行另走通路，双保险；见 TideObservation [41..]）。
        /// 历史：2026-09-10 目标域模型缩 6 维，2026-09-14 amp 回加后写入器实写 7 值与 dim=6 错位
        ///（第 2+ 原子参数块整体偏移一格——观测侧按 stride 6 切片读到前原子尾巴）；v19 全量重键一并消灭。</summary>
        public const int AtomParamDim = 9; // v19 2026-11-20：6→9（settleKind/branchParam/branchBudget/costTotal 四新槽；删恒 0 的 Mana 两维）

        // 2026-11-05 ID 冻结收口：TableFingerprint 整表盐及其缓存一并拆除——身份哈希锚定冻结 ID，
        // 原子表内容编辑（含新增无人引用的行）不再触发训练身份换血。
        //
        // P-1（2026-11-19 C 方案，docs/AI动作空间补全_修改计划.md §0.4）：EffectType 下标改
        // **稳定 ID 派生**——index = 1 + SplitMix64(FNV1a64(枚举名)) % (N_EFFECT_TYPES - 1)。
        // 与池/生成顺序/manifest 无关，main/train 跨进程天然同号（旧追加式注册表+manifest type 行
        // 分配已废弃，card_identity_manifest.json 仅存档不参与）。0 = hash 0/无/空名；
        // 未入表不再返回 0——直接落稳定桶。

        /// <summary>EffectType 下标容量上界（P-1：Python N_EFFECT_TYPES=4096；0=无/空名）。
        /// 旧值 255（256 池 manifest 追加式）已随 2026-11-19 稳定派生废弃。
        /// public：编辑器验证器（StableIndexVerifier/SpecMaster A2）跨程序集对账取用。</summary>
        public const int MaxTypeIndex = 4095;

        /// <summary>表侧缓存失效（P-1 后恒 no-op）：稳定派生无注册表/缓存可清——
        /// 保留方法给既有调用点（SpecMasterVerifier / EffectComposerScreen.SwitchTable）。幂等。</summary>
        public static void InvalidateTableCache()
        {
            // P-1：追加式注册表已拆，本口仅为兼容保留（无行为）。
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

        // ---- P-1（2026-11-19 C 方案）稳定派生原语：与 Python tide_features.stable_index 同式（双侧逐位对拍） ----

        /// <summary>SplitMix64（标准常数）——64 位混叠，P-1 稳定下标的派生核。
        /// Python 侧 tide_features.splitmix64 同式（&amp; 0xFFFFFFFFFFFFFFFF 掩码）。</summary>
        public static ulong SplitMix64(ulong seed)
        {
            ulong z = seed + 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        /// <summary>FNV-1a 64（逐 UTF-16 码元——EffectType 枚举名/效果 Id 均为 ASCII，与
        /// Python 逐 ord(ch) 同值）。空串/Null → 0（=无）。</summary>
        public static ulong Fnv1a64(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0UL;
            ulong h = 0xCBF29CE484222325UL;
            foreach (var ch in s)
            {
                h ^= (ulong)ch;
                h *= 0x100000001B3UL;
            }
            return h;
        }

        /// <summary>稳定下标（P-1 §0.4.2 单源公式）：index = 1 + SplitMix64(seed) % (pool - 1)；
        /// seed 0 → 0（无）。范围恒 [0, pool-1]——未登记不再返回 0，直接落稳定桶。</summary>
        public static int StableIndex(ulong seed, int pool)
            => seed == 0UL ? 0 : 1 + (int)(SplitMix64(seed) % (ulong)(pool - 1));

        /// <summary>EffectType → 稳定下标（1 基；0 = 无/空名）。P-1：由枚举名 FNV-1a 64 直接
        /// 派生（池 4096）——与池/生成顺序/manifest 无关，main/train 跨进程同号；
        /// 旧「追加式注册表 + manifest type 行」分配已废弃（2026-11-19）。
        /// public：编辑器验证器（StableIndexVerifier）跨程序集对拍取用。</summary>
        public static int AtomTypeIndex(string effectType)
            => StableIndex(Fnv1a64(effectType), MaxTypeIndex + 1);

        /// <summary>引擎分支奖励摘要（v20 2026-11-20 计划 §3.4，**只读观测——不改 AtomHash/ContentId**，
        /// 身份哈希仍 v19 口径）：首个 settle=Engine 分支的载荷概况——
        /// count=branch.then 原子数 + effectId 引用计 1（钳 3）；type0=首 then 原子 AtomTypeIndex
        ///（effectId 引用=0；无=0）；value0=首 then 原子 value（钳 99；无=0）。
        /// 消费：TideObservation 卡特征 [154..156]（团结/倒计时/充盈卡「达标后给什么」可见）。</summary>
        public static void EngineRewardOf(CardData data, out int count, out int type0, out float value0)
        {
            count = 0; type0 = 0; value0 = 0f;
            if (data?.Effects == null) return;
            foreach (var fx in data.Effects)
            {
                if (fx == null) continue;
                // 步骤形态（合成器/现行主形态）与扁平 AtomicEffects（旧档/手写）双扫——
                // 与 CollectAtoms 的两形态遍历同覆盖（2026-11-20 验收通用性修复）。
                if (fx.Steps != null)
                    foreach (var s in fx.Steps)
                    {
                        var b = s?.atomic?.branch;
                        if (EngineRewardOfEntry(b, ref count, ref type0, ref value0)) return;
                    }
                if (fx.AtomicEffects != null)
                    foreach (var a in fx.AtomicEffects)
                    {
                        var b = a?.branch;
                        if (EngineRewardOfEntry(b, ref count, ref type0, ref value0)) return;
                    }
            }
        }

        /// <summary>单条目引擎奖励摘要（EngineRewardOf 内核）：命中真引擎分支填摘要返 true。</summary>
        private static bool EngineRewardOfEntry(BranchEntryData b, ref int count, ref int type0, ref float value0)
        {
            if (b == null || BranchEntryRules.IsPhantom(b)) return false;
            if (b.settle != (int)BranchSettleKind.Engine) return false;
            int n = (b.then?.Count ?? 0) + (string.IsNullOrEmpty(b.effectId) ? 0 : 1);
            count = Math.Min(n, 3);
            var first = b.then != null && b.then.Count > 0 ? b.then[0] : null;
            if (first != null)
            {
                type0 = AtomTypeIndex(Attribute.AtomicEffectTable.GetByHashId(first.refId)?.EnumName);
                value0 = Math.Min(Math.Max(first.value, 0), 99);
            }
            return true; // 首个引擎分支即止（多效果卡摘要口径——计划「先 4 维」）
        }

        /// <summary>原子参数块（AtomParamDim 维，幅度 Clamp 到观测口径 [-99, 999]）。null 原子记全 0。
        /// v19 2026-11-20 九维布局见 <see cref="AtomParamDim"/> 头注释（value/settleKind/branchParam/
        /// branchBudget/kinds 三槽/amp/costTotal）。costTotal=所属效果 Costs 合计（同效果原子共值）。</summary>
        private static void AppendAtomParams(List<float> parms, AtomicEffectEntry a, float costTotal)
        {
            if (a == null)
            {
                for (int i = 0; i < AtomParamDim; i++) parms.Add(0f);
                return;
            }
            var branch = BranchEntryRules.IsPhantom(a.branch) ? null : a.branch;
            var kinds = (a.kinds ?? new List<int>()).Distinct().OrderBy(k => k).ToList();
            // [0] value：普通原子（含开 deathToll 增幅的原子——2026-11-20 原子属性化，value 仍是本体值）
            // =声明值；引擎主干原子（settle=Engine；DeathToll 分支已退役，排除子句为旧档残留防御）
            // =解析后门槛 x——显式 value>1 优先，其次 engineParam，缺省按 Then/effectId 锚价推导
            //（与运行时判定同源），模型由此可读「攒几费触发 / 倒计时几回合」。
            bool engineTrunk = branch != null && branch.settle == (int)BranchSettleKind.Engine
                               && (BranchEngineKind)branch.engine != BranchEngineKind.DeathToll;
            float v0 = a.value;
            if (engineTrunk && a.value <= 1) v0 = EngineThresholdX(branch);
            parms.Add(Clamp(v0));
            parms.Add(Clamp(branch?.settle ?? 0));
            parms.Add(Clamp(BranchParamOf(branch)));
            parms.Add(Clamp(BranchBudgetOf(branch)));
            parms.Add(Clamp(kinds.Count));
            parms.Add(kinds.Count > 0 ? kinds[0] : 0f);
            parms.Add(kinds.Count > 0 ? kinds[kinds.Count - 1] : 0f);
            parms.Add(Clamp(a.amp));
            parms.Add(Clamp(costTotal));
        }

        /// <summary>[2] branchParam：settle=Outcome → condParam / settle=Engine → engineParam；
        /// 无分支/有限分支=0。幽灵分支按无分支处理。死亡计数不走分支（2026-11-20 原子属性化——
        /// 增幅开关=原子 deathToll 布尔，计数实时值在卡特征 [152] 死亡计数维，不再兜底满值）。</summary>
        private static float BranchParamOf(BranchEntryData branch)
        {
            if (BranchEntryRules.IsPhantom(branch)) return 0f;
            if (branch.settle == (int)BranchSettleKind.Outcome) return branch.condParam;
            if (branch.settle == (int)BranchSettleKind.Engine && branch.engineParam > 0)
                return branch.engineParam;
            return 0f;
        }

        /// <summary>[3] branchBudget：settle=Gate（有限分支）→ 门槛行 Budget 列（灰，奖励锚价上限
        ///——原子表_系统 门槛行预算），gateId 查表；其余/查不到行=0。</summary>
        private static float BranchBudgetOf(BranchEntryData branch)
            => branch != null && branch.settle == (int)BranchSettleKind.Gate
               ? Attribute.AtomicEffectTable.GateBudgetOf(branch.gateId)
               : 0f;

        /// <summary>引擎门槛 x 解析（观测 [0] 与原子身份哈希共用，与 BranchEngines 运行时判定同源）：
        /// engineParam&gt;0 用声明值；否则倒计时=<see cref="CostDerivationService.CountdownTurnsOf"/>
        ///（Then/效果引用费 1:1 换算回合）、拼抢/元素充盈=<see cref="CostDerivationService.EngineRewardThreshold"/>
        ///（Then 锚价推导）；其余引擎（运势/团结等 x 恒手填）无推导=0。死亡计数不经本口
        ///（增幅标记，门槛落 [2]）。hostTiming 缺省占位——两推导口现行均不消费时机。</summary>
        internal static int EngineThresholdX(BranchEntryData branch)
        {
            if (BranchEntryRules.IsPhantom(branch) || branch.settle != (int)BranchSettleKind.Engine)
                return 0;
            if (branch.engineParam > 0) return branch.engineParam;
            var then = new List<AtomicEffectInstance>();
            if (branch.then != null)
                foreach (var e in branch.then)
                {
                    var inst = CardEffectConverter.ConvertAtomForUI(e);
                    if (inst != null) then.Add(inst);
                }
            switch ((BranchEngineKind)branch.engine)
            {
                case BranchEngineKind.Countdown:
                    return CostDerivationService.CountdownTurnsOf(0, then, branch.effectId);
                case BranchEngineKind.Clash:
                case BranchEngineKind.ManaSurplus:
                    return CostDerivationService.EngineRewardThreshold(then, TriggerTiming.OnStandbyPhase);
                default:
                    return 0;
            }
        }

        private static float Clamp(float v) => v < -99f ? -99f : (v > 999f ? 999f : v);

        // ===================================================== 原子哈希（身份共享单元） =====================================================

        /// <summary>原子 = 原子表条目（锚定冻结行 ID）+ 实例参数与目标覆盖 + 分支身份段（v19）——
        /// 「造成伤害 a4b823fc + target + value」这类单元，同原子跨卡共享 embedding 行；
        /// 同原子不同分支变体（settle/gate/outcome/engine/门槛 x）各自一行。
        /// 2026-11-05 ID 冻结收口：拆整表指纹盐——身份 = 冻结 refId + 实例参数，表内容编辑不再换血。</summary>
        internal static ulong AtomHash(AtomicEffectEntry a)
        {
            if (a == null) return NullAtomHash; // 非 0：0 是 TideCardIndex 的「未登记」信号

            var sb = new StringBuilder();
            AppendAtom(sb, a);
            sb.Append("B").Append(BranchIdentity(a.branch)).Append('\n'); // v19 分支身份段
            return Fnv1a(sb.ToString());
        }

        /// <summary>null 原子身份哨兵（全部 null 原子共一行；非 0——理由见 AtomHash）。</summary>
        private const ulong NullAtomHash = 0x6E756C6C61746F6DUL;

        /// <summary>原子的全部可变参数（2026-09-10 目标域模型：EffectType+Value+ID+Mana+TargetKinds；
        /// 编排属性已上移组合层——进 StructureHash 的效果级字段）。表行内容（费用/极性/域）不入哈希——
        /// 身份锚定冻结 refId，内容漂移检测归指纹列（工具链）。
        /// **不含分支段**（v19 2026-11-20 起本方法同时被 ContentId 复用——卡表 hash 列/效果 hash 列
        /// 全量锚定此串字节，分支身份只经 <see cref="AtomHash"/> 尾追 B 段，ContentId 路径保持字节不变）。</summary>
        private static void AppendAtom(StringBuilder sb, AtomicEffectEntry a)
        {
            // 2026-09-14 彻底引用化：原子=表行引用+增量（与 ContentHasher 同形态口径）
            sb.Append(a.refId ?? string.Empty).Append('|')
              .Append(a.value).Append('|')
              .Append(a.str ?? string.Empty).Append('|')
              .Append(TargetKindRules.Format(a.kinds ?? new List<int>())).Append('|')
              .Append(a.amp.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('\n');
        }

        /// <summary>分支身份段（v19 2026-11-20 绿套补盲，只进 AtomHash 不进 ContentId）：
        /// settle / gateId / outcomeId / engine / condParam / engineParam + 引擎解析门槛 x
        ///（<see cref="EngineThresholdX"/>——派生含值，「充盈→抽1」与「充盈→2伤」门槛不同即不同行）。
        /// 无分支/幽灵分支=哨兵 "-"。同原子不同分支变体由此各自一行 embedding（池 16384 富余）；
        /// StructureHash 已含条件与代价，口径不重复。</summary>
        private static string BranchIdentity(BranchEntryData b)
        {
            if (BranchEntryRules.IsPhantom(b)) return "-";
            int x = b.settle == (int)BranchSettleKind.Engine ? EngineThresholdX(b) : 0;
            return string.Join("|", b.settle, b.gateId ?? string.Empty, b.outcomeId ?? string.Empty,
                b.engine, b.condParam, b.engineParam, x);
        }

        /// <summary>按执行序展平一张卡全部效果树的原子（观测槽位按此顺序编码）。
        /// 三个平行列表同序追加：精确哈希 / EffectType 下标 / 参数块。
        /// v19（2026-11-20 绿套补盲）：效果级 Costs 合计（costTotal）随效果穿进每个原子的参数块 [8]
        /// ——同一效果的原子共值（代价进观测，冗余无害）。</summary>
        private static void CollectAtoms(CardEffectData e, List<ulong> atoms, List<int> types, List<float> parms)
        {
            if (e == null) return;
            float costTotal = 0f;
            if (e.Costs != null)
                foreach (var c in e.Costs)
                    if (c != null) costTotal += c.Value; // 全价合计（CostEntry.Value，口径同 AppendCosts）
            if (e.Steps != null && e.Steps.Count > 0)
            {
                foreach (var s in e.Steps) CollectStepAtoms(s, atoms, types, parms, costTotal);
            }
            else if (e.AtomicEffects != null)
            {
                foreach (var a in e.AtomicEffects)
                {
                    atoms.Add(AtomHash(a));
                    types.Add(AtomTypeIndex(AtomicEffectTable.GetByHashId(a?.refId)?.EnumName));
                    AppendAtomParams(parms, a, costTotal);
                }
            }
        }

        private static void CollectStepAtoms(EffectStepData s, List<ulong> atoms, List<int> types, List<float> parms,
            float costTotal)
        {
            if (s == null) return;
            switch (s.kind)
            {
                case 0:
                    atoms.Add(AtomHash(s.atomic));
                    types.Add(AtomTypeIndex(AtomicEffectTable.GetByHashId(s.atomic?.refId)?.EnumName));
                    AppendAtomParams(parms, s.atomic, costTotal);
                    break;
                case 1:
                    if (s.thenSteps != null)
                        foreach (var a in s.thenSteps)
                        {
                            atoms.Add(AtomHash(a));
                            types.Add(AtomTypeIndex(AtomicEffectTable.GetByHashId(a?.refId)?.EnumName));
                            AppendAtomParams(parms, a, costTotal);
                        }
                    if (s.elseSteps != null)
                        foreach (var a in s.elseSteps)
                        {
                            atoms.Add(AtomHash(a));
                            types.Add(AtomTypeIndex(AtomicEffectTable.GetByHashId(a?.refId)?.EnumName));
                            AppendAtomParams(parms, a, costTotal);
                        }
                    break;
                case 2:
                    if (s.choices != null)
                        foreach (var mode in s.choices)
                            if (mode?.steps != null)
                                foreach (var ms in mode.steps) CollectStepAtoms(ms, atoms, types, parms, costTotal);
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
