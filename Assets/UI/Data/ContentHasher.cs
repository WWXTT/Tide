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
        /// 与原子表 ID=sha256(DisplayName) 同口径）+ 附加参数（时点/激活/编排/代价/steps 原子引用）。
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
            sb.Append("NM:").Append(card.CardName ?? "").Append('|');
            sb.Append("ST:").Append((int)card.Supertype).Append('|');
            sb.Append("SUB:").Append((int)card.Subtype).Append('|');
            sb.Append("P:").Append(card.Power ?? 0).Append('|');
            sb.Append("L:").Append(card.Life ?? 0).Append('|');
            sb.Append("LV:").Append(card.Level ?? -1).Append('|');
            sb.Append("AR:").Append((int)card.ArrowDirections).Append('|');

            sb.Append("KW:");
            foreach (var kw in (card.Keywords ?? new List<string>()).OrderBy(k => k))
            {
                sb.Append(kw).Append(',');
            }
            sb.Append('|');

            // 连接光环声明影响功能（三轨制 2026-09-09）
            var auras = card.LinkAuras;
            if (auras != null && auras.Count > 0)
            {
                sb.Append("LA:");
                foreach (var aura in auras)
                {
                    if (aura == null) continue;
                    sb.Append(aura.stat ?? "").Append('=').Append(aura.value)
                      .Append('#').Append(aura.keyword ?? "").Append(',');
                }
                sb.Append('|');
            }

            // FX 段 = 效果 id 序列（推导链中间层）：效果文本/参数/原子引用变更 → 效果 id 变 → 卡 id 变
            sb.Append("FX:");
            if (card.Effects != null)
            {
                foreach (var fx in card.Effects)
                {
                    sb.Append(HashEffectOf(fx)).Append(';');
                }
            }

            return MurmurHash3.Hash32(sb.ToString()).ToString("X8");
        }

        private static void AppendEffect(StringBuilder sb, EffectGraphData graph)
        {
            if (graph == null)
            {
                return;
            }
            // header 取文本基底（DisplayName——身份层，与原子表同口径）+ 功能字段（时点/激活/编排/代价）；
            // Description 是动态渲染文案、Tags 是展示标签，均不作身份。
            var h = graph.header;
            if (h != null)
            {
                sb.Append("NM:").Append(h.DisplayName ?? "").Append('|');
                sb.Append("TT:").Append(h.TriggerTiming).Append('|');
                sb.Append("AT:").Append(h.ActivationType).Append('|');
                AppendOrchestration(sb, h, graph.steps);
                AppendCosts(sb, h.Costs);
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
        /// 效果级编排字段（2026-09-14 合成器重做扩展）：引擎通道（EngineKind/EngineParam+奖励原子）、
        /// 选择编排（SelectionMode/TargetCount/RandomTarget）、持续/落区/触发上限/速度。
        /// 此前这些字段均不参与哈希——合成器可编辑它们后去重会失真（同名不同功能视为重复）。
        /// AE: 段条件追加：非引擎效果（AtomicEffects 空）不写段，保旧哈希尽量少漂移。
        /// RT: 段条件追加（2026-09-16）：仅 RandomTarget 置位时写——现网数据零漂移；
        /// Drawbacks（DB 段）已随减费归入代价体系退役。
        /// </summary>
        private static void AppendOrchestration(StringBuilder sb, CardEffectData h, List<EffectStepData> graphSteps)
        {
            sb.Append("EK:").Append(h.EngineKind).Append('/').Append(h.EngineParam).Append('|');
            // 2026-09-14 收缩：SM 去 DynamicTargetCount 段（并入 TargetCount=-1）、DU 去 DurationValue 段（ForTurns 退役）
            sb.Append("SM:").Append(h.SelectionMode).Append('/')
              .Append(h.TargetCount).Append('|');
            sb.Append("DU:").Append(h.Duration).Append('/')
              .Append(h.SummonDropZone).Append('/');
            sb.Append("TL:").Append(h.TriggerLimitPerTurn).Append('|');
            sb.Append("BS:").Append(h.BaseSpeed).Append('|');
            if (h.RandomTarget != 0)
                sb.Append("RT:1|");
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
            // 表行内容变化不改变卡/效果 id——表是平衡层，引用是身份层）
            sb.Append('[')
              .Append(a.refId).Append(',')
              .Append(a.value).Append(',')
              .Append(a.str).Append(',')
              .Append(string.Join(",", (a.kinds ?? new System.Collections.Generic.List<int>()).Distinct().OrderBy(k => k)))
              .Append(",amp=").Append(a.amp.ToString("R", System.Globalization.CultureInfo.InvariantCulture))
              .Append(']');
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
                    // Payload 原子（2026-09-14 补）：代价栏效果是功能字段——此前漏哈希
                    if (c.payload != null && !string.IsNullOrEmpty(c.payload.refId))
                    {
                        sb.Append(",p=");
                        AppendAtomic(sb, c.payload);
                    }
                    sb.Append('>');
                }
            }
            sb.Append('|');
        }
    }
}
