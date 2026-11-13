using System.Collections.Generic;
using System.Linq;
using System.Text;
using CardCore;

namespace SynergyUI
{
    /// <summary>
    /// 功能内容哈希 —— 用于效果库与卡表去重。
    ///
    /// 只哈希「功能内容」（决定效果/卡牌行为的字段），刻意排除名称、标签、描述等
    /// 非区分性展示信息：名字不同但功能相同的两条目，视为重复（覆盖）。
    ///
    /// 复用 CardCore.MurmurHash3.Hash32，产物为稳定 8 位大写 hex 串。
    /// </summary>
    public static class ContentHasher
    {
        /// <summary>
        /// 效果内容哈希（2026-09-16 三层推导链定案）：**文本描述为基底**（header.DisplayName，
        /// 与原子表 `TextHash` 同口径——注意 2026-11-05 起原子表 **ID 已冻结**，TextHash 只是查重/漂移检测列，
        /// 不再是身份来源）+ 附加参数（时点/激活/编排/代价/steps 原子引用）。
        /// Description 是动态渲染文案不作身份；graph.name 是展示别名不参与。
        /// </summary>
        public static string HashEffect(EffectGraphData graph)
        {
            var sb = new StringBuilder();
            AppendEffect(sb, graph);
            return MurmurHash3.Hash32(sb.ToString()).ToString("X8");
        }

        /// <summary>
        /// 卡内嵌效果 → 效果 id（投影单一真相）：与 EffectLibrarySerializer.EffectIdsOf 的
        /// upsert 投影同构（EffectGraphData(fx.DisplayName){header,steps} → HashEffect）——
        /// HashCard 的 FX 段与卡表 effectIds 引用天然一致。
        /// </summary>
        public static string HashEffectOf(CardEffectData fx)
        {
            if (fx == null) return "∅";
            return HashEffect(new EffectGraphData(fx.DisplayName)
            {
                header = fx,
                steps = fx.Steps != null ? new List<EffectStepData>(fx.Steps) : new List<EffectStepData>()
            });
        }

        /// <summary>
        /// 卡牌内容哈希（2026-09-16 三层推导链定案）：**文本描述为基底**（CardName）+
        /// 附加参数（类型/身材/等级/箭头/关键词/光环）+ **效果 id 序列**（原子id→效果id→卡id
        /// 依赖链——不再内联展开效果内容，效果内容变更经效果 id 传导）。
        /// 费用不参与哈希——配置改费不改 ID（费用为运行时推导口径，见 CardCostService）。
        /// </summary>
        public static string HashCard(CardData card)
        {
            var sb = new StringBuilder();
            AppendCard(sb, card, includeName: true);
            return MurmurHash3.Hash32(sb.ToString()).ToString("X8");
        }

        /// <summary>卡牌行为封印（2026-11-13 牌库查重）：与 HashCard 同一组合，差异——
        /// ①**跳过 NM（CardName）段**：名称不参与功能身份（类注释口径：名字不同但功能相同 = 重复）；
        /// ②**补 AB 段（攻/守/充能资格三件套）**：功能字段但 HashCard 历史未含（ID 冻结不改口径），
        /// 只差攻/守资格的同身材卡不是「一模一样」（火灵·攻/守实测同封印的误报源）；
        /// ③**补 D（结界耐久）/SPD（法术提速）段**：同样是功能字段且 HashCard 未含——耐久 2/4 的
        /// 两张碑不是同一张卡（2026-11-13 全库清重复时补：否则会把「同效果不同耐久」误判成重复）。
        /// HashCard 口径零改动（卡 id / 存量身份不漂移）；SealCard 不落盘，仅查重比对用。</summary>
        public static string SealCard(CardData card)
        {
            var sb = new StringBuilder();
            AppendCard(sb, card, includeName: false);
            sb.Append("D:").Append(card.Durability).Append('|');
            sb.Append("SPD:").Append(card.SpeedBonus).Append('|');
            sb.Append("AB:").Append(card.AttackAbility ? 1 : 0).Append(',')
              .Append(card.GuardAbility ? 1 : 0).Append(',')
              .Append(card.ChargeAbility ? 1 : 0).Append('|');
            return MurmurHash3.Hash32(sb.ToString()).ToString("X8");
        }

        private static void AppendCard(StringBuilder sb, CardData card, bool includeName)
        {
            if (includeName)
                sb.Append("NM:").Append(card.CardName ?? "").Append('|');
            sb.Append("ST:").Append((int)card.Supertype).Append('|');
            sb.Append("P:").Append(card.Power ?? 0).Append('|');
            sb.Append("L:").Append(card.Life ?? 0).Append('|');
            sb.Append("AR:").Append((int)card.ArrowDirections).Append('|');
            // 场地标记（2026-10-29 P8）：卡面定义参与身份——改标记=换卡 ID；false 不写段（存量卡 id 零漂移）
            if (card.IsField) sb.Append("FLD:1|");

            sb.Append("KW:");
            foreach (var kw in (card.Keywords ?? new List<string>()).OrderBy(k => k))
            {
                sb.Append(kw).Append(',');
            }
            sb.Append('|');

            // 连接光环声明影响功能（三轨制 2026-09-09；2026-11-05 效果光环：effectRef 非空才
            // 追加 '#id'——存量卡 id 零漂移，P2-2 domain 先例同款）
            var auras = card.LinkAuras;
            if (auras != null && auras.Count > 0)
            {
                sb.Append("LA:");
                foreach (var aura in auras)
                {
                    if (aura == null) continue;
                    sb.Append(aura.stat ?? "").Append('=').Append(aura.value)
                      .Append('#').Append(aura.keyword ?? "");
                    if (!string.IsNullOrEmpty(aura.effectRef))
                        sb.Append('#').Append(aura.effectRef);
                    sb.Append(',');
                }
                sb.Append('|');
            }

            //（卡级代价哈希段 PC/PCR 已随 2026-11-08 代价效果级化整删——代价只挂效果级，
            //  经 HashEffect 的 AppendCosts(h.Costs) 参与效果 id；卡级段本就条件写出（空代价不写段），
            //  删除对全部存量卡 id 零漂移。）

            // FX 段 = 效果 id 序列（推导链中间层）：效果文本/参数/原子引用/效果级代价变更 → 效果 id 变 → 卡 id 变
            sb.Append("FX:");
            if (card.Effects != null)
            {
                foreach (var fx in card.Effects)
                {
                    sb.Append(HashEffectOf(fx)).Append(';');
                }
            }
        }

        /// <summary>效果行为封印（G2 内容包容器 2026-11-08）：与 HashEffect 同一组合，
        /// **跳过 NM（DisplayName）段**——seal 只盖行为（时点/激活/编排/代价/光环/步骤），
        /// 不盖名字与风味文案（地基计划 §5：改文案 seal 不变、改 body seal 变）。
        /// HashEffect 口径零改动（ContentSig/效果 id 不漂移）。</summary>
        public static string SealEffect(EffectGraphData graph)
        {
            var sb = new StringBuilder();
            AppendEffect(sb, graph, includeName: false);
            return MurmurHash3.Hash32(sb.ToString()).ToString("X8");
        }

        /// <summary>SealEffect 的 CardEffectData 便捷口（与 HashEffectOf 同构——瘦 DTO 往返后
        /// 由注册表按条目计算封印用此口；名字段被跳过，DTO.name 与 header.DisplayName 均不参与）。</summary>
        public static string SealEffectOf(CardEffectData fx)
        {
            if (fx == null) return "∅";
            return SealEffect(new EffectGraphData(fx.DisplayName)
            {
                header = fx,
                steps = fx.Steps != null ? new List<EffectStepData>(fx.Steps) : new List<EffectStepData>()
            });
        }

        private static void AppendEffect(StringBuilder sb, EffectGraphData graph, bool includeName = true)
        {
            if (graph == null)
            {
                return;
            }
            // header 取文本基底（DisplayName——身份层，与原子表同口径）+ 功能字段（时点/激活/编排/代价）；
            // Description 是动态渲染文案、Tags 是展示标签，均不作身份。
            // includeName=false = SealEffect（G2 内容包）：跳过名字段——seal 只盖行为不盖文案。
            var h = graph.header;
            if (h != null)
            {
                if (includeName)
                    sb.Append("NM:").Append(h.DisplayName ?? "").Append('|');
                sb.Append("TT:").Append(h.TriggerTiming).Append('|');
                sb.Append("AT:").Append(h.ActivationType).Append('|');
                AppendOrchestration(sb, h, graph.steps);
                AppendCosts(sb, h.Costs);
                // 光环段（2026-09-23 效果级）：箭头/光环条目随效果哈希——空声明不写段（存量效果 id 零漂移）
                if (h.ArrowDirections != 0 || (h.LinkAuras != null && h.LinkAuras.Count > 0))
                {
                sb.Append("AUR:").Append(h.ArrowDirections).Append(';');
                foreach (var aura in h.LinkAuras)
                {
                    if (aura == null) continue;
                    sb.Append(aura.stat ?? "").Append('=').Append(aura.value)
                      .Append('#').Append(aura.keyword ?? "");
                    if (!string.IsNullOrEmpty(aura.effectRef))
                        sb.Append('#').Append(aura.effectRef); // 2026-11-05 效果光环：非空才写=存量零漂移
                    sb.Append(',');
                }
                sb.Append('|');
                }
            }
            sb.Append("STEPS:");
            if (graph.steps != null)
            {
                foreach (var step in graph.steps)
                {
                    AppendStep(sb, step);
                    sb.Append(';');
                }
            }
        }

        /// <summary>
        /// 效果级编排字段（2026-10-05 两槽定案）：选择编排（SelectionMode/TargetCount/RandomTarget）、
        /// 持续/落区/触发上限/速度。此前这些字段均不参与哈希——合成器可编辑它们后去重会失真。
        /// AE: 段条件追加：扁平形态（AtomicEffects 非空且 steps 空）不写段，保旧哈希尽量少漂移。
        /// RT: 段条件追加（2026-09-16）：仅 RandomTarget 置位时写——现网数据零漂移。
        /// 引擎通道 EK 段已随载荷化退役——槽级 branch 载荷随原子入哈希（AppendAtomic）。
        /// </summary>
        private static void AppendOrchestration(StringBuilder sb, CardEffectData h, List<EffectStepData> graphSteps)
        {
            // 2026-09-14 收缩：SM 去 DynamicTargetCount 段（并入 TargetCount=-1）、DU 去 DurationValue 段（ForTurns 退役）
            sb.Append("SM:").Append(h.SelectionMode).Append('/')
              .Append(h.TargetCount).Append('|');
            sb.Append("DU:").Append(h.Duration).Append('/')
              .Append(h.SummonDropZone).Append('/');
            sb.Append("TL:").Append(h.TriggerLimitPerTurn).Append('|');
            sb.Append("BS:").Append(h.BaseSpeed).Append('|');
            if (h.RandomTarget != 0)
                sb.Append("RT:1|");
            // TK 段（2026-10-04 效果级作用范围）：功能字段参与哈希；空声明不写段（存量效果 id 零漂移）
            if (h.TargetKinds != null && h.TargetKinds.Count > 0)
                sb.Append("TK:").Append(string.Join(",", h.TargetKinds.Distinct().OrderBy(k => k))).Append('|');
            // AE 段单源化（2026-09-14 v2）：仅 steps 为空（引擎形态——奖励原子唯一承载）时计入；
            // steps 形态的 AtomicEffects 是旧投影冗余，不再参与哈希（否则同一效果两种存储两套 id）
            if ((h.AtomicEffects != null && h.AtomicEffects.Count > 0)
                && (graphSteps == null || graphSteps.Count == 0))
            {
                sb.Append("AE:");
                foreach (var atom in h.AtomicEffects) AppendAtomic(sb, atom);
                sb.Append('|');
            }
        }

        private static void AppendStep(StringBuilder sb, EffectStepData step)
        {
            if (step == null)
            {
                return;
            }
            sb.Append('k').Append(step.kind).Append(':');
            if (step.kind == 0)
            {
                AppendAtomic(sb, step.atomic);
            }
            else if (step.kind == 2)
            {
                // 抉择：逐模式递归哈希子步骤（顺序敏感）；label 是展示名，不参与——仅取影响功能的字段
                sb.Append("choice{");
                if (step.choices != null)
                {
                    foreach (var c in step.choices)
                    {
                        sb.Append("m{");
                        if (c?.steps != null)
                        {
                            foreach (var s in c.steps) AppendStep(sb, s);
                        }
                        sb.Append('}');
                    }
                }
                sb.Append('}');
            }
            else
            {
                // 产出条件门（2026-09-14 补：conditionId/参数是功能字段——此前只哈希旧 condition 字段）
                sb.Append("gid:").Append(step.conditionId ?? "").Append('/')
                  .Append(step.conditionParam).Append('/')
                  .Append(step.conditionStringParam ?? "").Append(';');
                AppendCondition(sb, step.condition);
                sb.Append("then{");
                if (step.thenSteps != null)
                {
                    foreach (var a in step.thenSteps) AppendAtomic(sb, a);
                }
                sb.Append("}else{");
                if (step.elseSteps != null)
                {
                    foreach (var a in step.elseSteps) AppendAtomic(sb, a);
                }
                sb.Append('}');
            }
        }

        private static void AppendAtomic(StringBuilder sb, AtomicEffectEntry a)
        {
            if (a == null)
            {
                return;
            }
            // 2026-09-14 彻底引用化：原子=表行引用+增量——哈希直接组引用形态（不查表，确定性；
            // 表行内容变化不改变卡/效果 id——表是平衡层，引用是身份层）。
            // 2026-10-05 两槽定案：槽级 branch 载荷是功能字段（条件+Then 奖励），随原子入哈希。
            sb.Append('[')
              .Append(a.refId).Append(',')
              .Append(a.value).Append(',')
              .Append(a.str).Append(',')
              .Append(string.Join(",", (a.kinds ?? new System.Collections.Generic.List<int>()).Distinct().OrderBy(k => k)))
              .Append(",amp=").Append(a.amp.ToString("R", System.Globalization.CultureInfo.InvariantCulture))
              .Append(']');
            // 授予域 token（P2-2 2026-11-04）：非空才入哈希（按值省略——domain 空的旧卡零漂移）；
            // 同一关键词不同域（己方/双方）= 不同段 → 不同卡 ID。
            if (!string.IsNullOrEmpty(a.domain)) sb.Append(",dom=").Append(a.domain);
            // 动态数值（步6 2026-11-08）：非默认才入哈希（按值省略——存量零漂移）；同 refId 不同
            // 表达式=不同内容哈希（防「同 ID 异值」撞指纹）。计价不读它们（定价恒按 value 名义值）。
            if (a.valueMax != 0) sb.Append(",vmax=").Append(a.valueMax);
            if (!string.IsNullOrEmpty(a.valueExpr)) sb.Append(",vexpr=").Append(a.valueExpr);
            AppendBranch(sb, a.branch);
        }

        /// <summary>槽级分支载荷哈希（两槽定案）：条件三族（有限分支/产出条件/引擎）+ Then 奖励原子（顺序敏感）。
        /// 幽灵 settle 由调用侧剥离（IsPhantom）——未定义值不进哈希。</summary>
        private static void AppendBranch(StringBuilder sb, BranchEntryData b)
        {
            if (b == null) return;
            sb.Append("br{").Append(b.settle).Append('/')
              .Append(b.gateId ?? "").Append('#')
              .Append(b.outcomeId ?? "").Append('/').Append(b.condParam).Append('/').Append(b.condStr ?? "")
              .Append('#').Append(b.engine).Append('/').Append(b.engineParam);
            if (b.tactivate != -1) sb.Append(";tact:").Append(b.tactivate); // 奖励发动方式（-1=随主干·不写段）
            if (!string.IsNullOrEmpty(b.effectId)) sb.Append(";fx:").Append(b.effectId); // 奖励效果引用（2026-11-07 分支奖励效果化——非空才写=存量零漂移）
            sb.Append(";then[");
            if (b.then != null)
            {
                foreach (var r in b.then) AppendAtomic(sb, r);
            }
            sb.Append("]}");
        }

        private static void AppendCondition(StringBuilder sb, ActivationConditionData c)
        {
            if (c == null)
            {
                return;
            }
            sb.Append('(')
              .Append(c.Type).Append(',')
              .Append(c.Value).Append(',')
              .Append(c.Value2).Append(',')
              .Append(c.StringValue).Append(',')
              .Append(c.Negate ? 1 : 0)
              .Append(')');
        }

        private static void AppendCosts(StringBuilder sb, List<CostEntry> costs)
        {
            sb.Append("CO:");
            if (costs != null)
            {
                foreach (var c in costs)
                {
                    if (c == null) continue;
                    sb.Append('<')
                      .Append(c.CostType).Append(',')
                      .Append(c.Value).Append(',')
                      .Append(c.ManaType).Append(',')
                      .Append(c.TurnDuration);
                    // Payload 原子（2026-09-14 补）：代价栏效果是功能字段——此前漏哈希。
                    // 2026-10-16 组装效果代价：逐原子 ",p=" 追加（n=1 与旧单条格式字节一致——
                    // 存量卡 ID 稳定；n>1 依序追加，装载端 AllPayloads 折叠口径同源）
                    foreach (var pa in c.AllPayloads())
                    {
                        sb.Append(",p=");
                        AppendAtomic(sb, pa);
                    }
                    sb.Append('>');
                }
            }
            sb.Append('|');
        }
    }
}
