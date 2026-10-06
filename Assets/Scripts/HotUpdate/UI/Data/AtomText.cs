using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.Attribute;

namespace SynergyUI
{
    /// <summary>
    /// 原子描述动态渲染（2026-09-14 合成器重做）——槽内摘要 / 检查器实时描述 / 卡级挂载预览共用。
    ///
    /// 模板源 = 原子表 DisplayName 列（装载后落在 config.Description——注意 config.DisplayName
    /// 是 EnumName 列的中文短名，见 AtomicEffectTable.BuildConfig）。模板含 {value} 占位：
    ///   - 普通渲染：{value} → Value；
    ///   - 数值随机（RandomAmplitude&gt;0）：前缀「随机 」+ {value} → 区间文本
    ///     （span=round(|Value|×幅度)，3 伤 ±100% → "随机 对目标造成0至6点伤害"）；
    ///   - 目标随机（header.RandomTarget，2026-09-16 自 SelectionMode 移出为正交标志）：前缀「随机目标·」。
    /// 计价/构筑读名义 Value 不变——此处只做展示层渲染（口径对齐 2026-09-13 两个随机定案）。
    ///
    /// {target} 占位（2026-09-22 五轮）：按实例域 kinds 推导——null/多值 → 「目标」；
    /// 单值 → TargetKind 中文名（"对己方单位造成1点伤害"）——目标域收窄后描述实时反映作用对象。
    /// </summary>
    public static class AtomText
    {
        /// <summary>TargetKind → 中文名（2026-10-06 单一来源迁 CardCore：TargetKindRules.ZhNameOf——
        /// 表列 TargetKinds 中文 CSV 与 UI 显示同源，防两套口径）。</summary>
        public static string TargetKindZhOf(TargetKind k)
            => TargetKindRules.ZhNameOf(k);

        /// <summary>{target} 占位的名词：单值实例域 → 中文名；null（表默认）/多值 → 「目标」。
        /// 例外（2026-10-04 关键词行修复）：关键词行（MountKinds 含 Keyword 位）的 kinds=[Self]
        /// 是「本体挂自己」存储态（合成器 AtomZh 两态命名、运行时自授予同源），而该行模板的
        /// {target} 是叙事名次——受击对手或字面「目标」（"不会成为效果{target}"），不随域渲染，
        /// 否则出"不会成为攻击和效果的自己"病句——回退「目标」。非关键词行（如沉睡 0,1,2 域）
        /// 收窄到 Self 仍显「自己」，那是真实选择。</summary>
        public static string TargetNoun(AtomicEffectConfig cfg, AtomicEffectEntry atom)
        {
            if (atom?.kinds != null && atom.kinds.Count == 1)
            {
                bool keywordSelf = (TargetKind)atom.kinds[0] == TargetKind.Self
                    && cfg != null && MountKindExtensions.ParseCsv(cfg.MountKinds).Contains(MountKind.Keyword);
                if (!keywordSelf)
                    return TargetKindZhOf((TargetKind)atom.kinds[0]);
            }
            return "目标";
        }

        /// <summary>效果级作用范围版（2026-10-04 相同目标定案）：header.TargetKinds 单值优先——
        /// 并列全体原子共享同一作用范围，{target} 按效果级域渲染（关键词行 [Self] 存储态例外同口径）；
        /// 未声明/多值回落实例域口径。</summary>
        public static string TargetNoun(AtomicEffectConfig cfg, AtomicEffectEntry atom, CardEffectData header)
        {
            if (header?.TargetKinds != null && header.TargetKinds.Count == 1)
            {
                bool keywordSelf = (TargetKind)header.TargetKinds[0] == TargetKind.Self
                    && cfg != null && MountKindExtensions.ParseCsv(cfg.MountKinds).Contains(MountKind.Keyword);
                return keywordSelf ? "目标" : TargetKindZhOf((TargetKind)header.TargetKinds[0]);
            }
            return TargetNoun(cfg, atom);
        }

        /// <summary>渲染单原子描述。cfg 为空（无表行 fallback 原子）时回退 refId。</summary>
        public static string Render(AtomicEffectConfig cfg, AtomicEffectEntry atom, CardEffectData header)
        {
            if (atom == null || string.IsNullOrEmpty(atom.refId)) return "原子效果";
            string tpl = cfg != null && !string.IsNullOrEmpty(cfg.Description) ? cfg.Description : atom.refId;

            int v = atom.value;
            int span = atom.amp > 0f ? TideMath.RoundToInt(Math.Abs(v) * atom.amp) : 0;
            string number = span > 0 ? $"{Math.Max(0, v - span)}至{v + span}" : v.ToString();
            // {target} → 实例域名次（2026-09-22 五轮：单值域显作用对象；表默认/多值保持「目标」；
            // 2026-10-04 补：关键词行 [Self] 存储态回退「目标」，见 TargetNoun；
            // 2026-10-04 相同目标定案：header 效果级作用范围单值优先）
            string body = tpl.Replace("{value}", number).Replace("{target}", TargetNoun(cfg, atom, header));
            if (span > 0) body = "随机 " + body;
            if (header != null && header.RandomTarget != 0)
                body = "随机目标·" + body;
            return body;
        }

        /// <summary>按引用取表行渲染（便捷重载——refId 直查）。</summary>
        public static string Render(AtomicEffectEntry atom, CardEffectData header)
            => Render(AtomicEffectTable.GetByHashId(atom?.refId), atom, header);

        /// <summary>效果整体预览（两槽定案）：并列主序列=逐原子分号连接，原子带槽级 branch 载荷时
        /// 追加分支后缀（产出条件=「，如果…，奖励」自然句；引擎/局面门=「[条件]→奖励」记法）；
        /// 遗留 kind=1 门步骤与抉择步骤照旧渲染。</summary>
        public static string RenderEffectSummary(EffectGraphData graph)
        {
            if (graph?.header == null) return "";
            var h = graph.header;
            var parts = new System.Collections.Generic.List<string>();
            if (graph.steps != null)
            {
                foreach (var s in graph.steps)
                {
                    if (s == null) continue;
                    if (s.kind == 0 && s.atomic != null)
                    {
                        string body = RenderAtomEntry(s.atomic, h);
                        var suffix = BranchSuffix(s.atomic);
                        parts.Add(suffix.Length > 0 ? $"{body}{suffix}" : body);
                    }
                    else if (s.kind == 1)
                    {
                        // 遗留门步骤：条件中文（ComposerCatalog 同源；诅咒门 Then=抽到时的专属载荷）
                        var spec = ComposerCatalog.OutcomeConditions.FirstOrDefault(g => g.Id == s.conditionId)
                                   ?? ComposerCatalog.SituationGates.FirstOrDefault(g => g.Id == s.conditionId);
                        string gate = spec != null ? ComposerCatalog.GateLabel(spec)
                            : (string.IsNullOrEmpty(s.conditionId) ? "?" : s.conditionId);
                        string reward = s.thenSteps != null && s.thenSteps.Count > 0
                            ? RenderAtomEntry(s.thenSteps[0]) : "（未设奖励）";
                        parts.Add($"[{gate}]→{reward}");
                    }
                    else if (s.kind == 2) parts.Add($"抉择（{s.choices?.Count ?? 0} 模式）");
                }
            }
            return string.Join("；", parts);
        }

        /// <summary>槽级 branch 载荷的后缀文本（两槽定案）：无载荷返回空串。
        /// 产出条件（Outcome）=自然句式「，如果消灭了目标，{奖励}」（2026-10-05 文本表述定案）；
        /// 引擎/局面门沿用「[条件]→奖励」记法（引擎参数与对赌逆转是结构信息，句式承载不了）。</summary>
        public static string BranchSuffix(AtomicEffectEntry atom)
        {
            var b = atom?.branch;
            if (b == null) return "";
            string reward = b.then != null && b.then.Count > 0
                ? RenderAtomEntry(b.then[0]) : "（未设奖励）";
            string cond;
            switch ((BranchSettleKind)b.settle)
            {
                case BranchSettleKind.Engine:
                    cond = TrunkText((BranchEngineKind)b.engine, b.engineParam);
                    break;
                case BranchSettleKind.Outcome:
                    var oc = ComposerCatalog.OutcomeConditions.FirstOrDefault(g => g.Id == b.outcomeId);
                    string clause;
                    if (oc != null)
                        clause = string.IsNullOrEmpty(oc.IfClause) ? oc.DisplayName.TrimEnd('时') : oc.IfClause;
                    else
                        clause = string.IsNullOrEmpty(b.outcomeId) ? "？" : b.outcomeId;
                    return $"，如果{clause}，{reward}";
                default:
                    if (b.gateId == ComposerCatalog.CurseGateId) return "→附加诅咒（抽到该卡时执行专属载荷）";
                    var sc = ComposerCatalog.SituationGates.FirstOrDefault(g => g.Id == b.gateId);
                    cond = sc != null ? ComposerCatalog.GateLabel(sc)
                        : (string.IsNullOrEmpty(b.gateId) ? "?" : b.gateId);
                    cond += "，未达成→逆转为代价"; // 局面门对赌（2026-10-05）：奖励逆转作用区域强制执行
                    break;
            }
            return $"[{cond}]→{reward}";
        }

        /// <summary>条目级渲染（refId → 表行 → Render；行缺失回退 refId）。</summary>
        public static string RenderAtomEntry(AtomicEffectEntry atom)
        {
            if (atom == null || string.IsNullOrEmpty(atom.refId)) return "原子";
            return Render(AtomicEffectTable.GetByHashId(atom.refId), atom, null);
        }

        /// <summary>条目级渲染·效果级作用范围版（2026-10-04 相同目标定案）：并列主序列原子传
        /// header——{target} 按效果级域渲染；奖励原子目标各自解析，不传 header（用单参重载）。</summary>
        public static string RenderAtomEntry(AtomicEffectEntry atom, CardEffectData header)
        {
            if (atom == null || string.IsNullOrEmpty(atom.refId)) return "原子";
            return Render(AtomicEffectTable.GetByHashId(atom.refId), atom, header);
        }

        /// <summary>引擎主干显示文本（header 通道无 AtomicEffectEntry——按引擎+参数生成）。</summary>
        public static string TrunkText(BranchEngineKind engine, int param)
        {
            switch (engine)
            {
                case BranchEngineKind.Clash:
                    return $"拼点：随机生物攻击力差额 ≥ 奖励锚价合计时执行奖励（零计价·门槛制）";
                case BranchEngineKind.LuckRoll:
                    return $"运势：2d6 两点均 > {param} 时执行奖励（零计价·概率门槛）";
                case BranchEngineKind.Countdown:
                    return param > 0
                        ? $"倒计时 {param} 回合，归零执行奖励并重置"
                        : "倒计时：按奖励推导费自动换算回合（1费=1回合）";
                case BranchEngineKind.DeathToll:
                    return $"死亡计数：本回合双方合计 {param} 个生物死亡时执行奖励（预算 {param}）";
                case BranchEngineKind.ManaSurplus:
                    return $"元素充盈：出牌付费后 bank 最多色 > {param} 时执行奖励（每次达标都触发，预算 {param}）";
                case BranchEngineKind.NthHandCard:
                    return $"手牌序位：此卡为本回合从手牌使用的第 {param} 张卡时执行奖励（预算 {param}）";
                default:
                    return "";
            }
        }
    }
}
