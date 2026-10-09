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
    ///   - 数值随机（RandomAmplitude&gt;0）：{value} → 区间文本「x至x」即随机语义
    ///     （span=round(|Value|×幅度)，3 伤 ±100% → "对目标造成0至6点伤害"——2026-10-09 句式定案不加前缀）；
    ///   - 目标随机（header.RandomTarget，2026-09-16 自 SelectionMode 移出为正交标志）：
    ///     正文第一个「目标」前插「随机」（"对随机目标造成3点伤害"；域名次冠名次前）。
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
        /// 例外（2026-10-04 关键词行修复，2026-10-07 扩指示物行）：关键词/指示物行的 kinds=[Self]
        /// 是「本体挂自己」存储态（合成器 AtomZh 两态命名、运行时自授予同源），而该行模板的
        /// {target} 是叙事名次——受击对手或字面「目标」（"不会成为效果{target}"），不随域渲染，
        /// 否则出"不会成为攻击和效果的自己"病句——回退「目标」。非授予类行（如沉睡复合域收窄到 Self）
        /// 仍显「自己」，那是真实选择。</summary>
        public static string TargetNoun(AtomicEffectConfig cfg, AtomicEffectEntry atom)
        {
            if (atom?.kinds != null && atom.kinds.Count == 1)
            {
                var m = MountKindExtensions.ParseCsv(cfg?.MountKinds ?? "");
                bool selfStorage = (TargetKind)atom.kinds[0] == TargetKind.Self
                    && (m.Contains(MountKind.Keyword) || m.Contains(MountKind.Counter));
                if (!selfStorage)
                    return TargetKindZhOf((TargetKind)atom.kinds[0]);
            }
            return "目标";
        }

        /// <summary>效果级作用范围版（2026-10-04 相同目标定案）：header.TargetKinds 单值优先——
        /// 并列全体原子共享同一作用范围，{target} 按效果级域渲染（关键词/指示物行 [Self] 存储态例外同口径）；
        /// 未声明/多值回落实例域口径。</summary>
        public static string TargetNoun(AtomicEffectConfig cfg, AtomicEffectEntry atom, CardEffectData header)
        {
            if (header?.TargetKinds != null && header.TargetKinds.Count == 1)
            {
                var m = MountKindExtensions.ParseCsv(cfg?.MountKinds ?? "");
                bool selfStorage = (TargetKind)header.TargetKinds[0] == TargetKind.Self
                    && (m.Contains(MountKind.Keyword) || m.Contains(MountKind.Counter));
                return selfStorage ? "目标" : TargetKindZhOf((TargetKind)header.TargetKinds[0]);
            }
            return TargetNoun(cfg, atom);
        }

        /// <summary>渲染单原子描述。cfg 为空（无表行 fallback 原子）时回退 refId。</summary>
        public static string Render(AtomicEffectConfig cfg, AtomicEffectEntry atom, CardEffectData header)
            => RenderCore(cfg, atom, header, false);

        /// <summary>奖励原子渲染（2026-10-08 定案）：Then 奖励结算在合法范围重新弹选目标
        /// （ExecuteThenRewardsAsync→ResolveRewardTargetsAsync，2026-10-07 晚定案），不沿用主干目标——
        /// {target} 名次冠「新的」与主干目标区分（"对新的目标造成2点伤害"）；模板无 {target} 不受影响。</summary>
        public static string RenderReward(AtomicEffectConfig cfg, AtomicEffectEntry atom)
            => RenderCore(cfg, atom, null, true);

        private static string RenderCore(AtomicEffectConfig cfg, AtomicEffectEntry atom, CardEffectData header,
            bool newTargetNoun)
        {
            if (atom == null || string.IsNullOrEmpty(atom.refId)) return "原子效果";
            string tpl = cfg != null && !string.IsNullOrEmpty(cfg.Description) ? cfg.Description : atom.refId;

            int v = atom.value;
            int span = atom.amp > 0f ? TideMath.RoundToInt(Math.Abs(v) * atom.amp) : 0;
            string number = span > 0 ? $"{Math.Max(0, v - span)}至{v + span}" : v.ToString();
            // {target} → 实例域名次（2026-09-22 五轮：单值域显作用对象；表默认/多值保持「目标」；
            // 2026-10-04 补：关键词行 [Self] 存储态回退「目标」，见 TargetNoun；
            // 2026-10-04 相同目标定案：header 效果级作用范围单值优先；
            // 2026-10-08 奖励口径：名次冠「新的」——奖励目标合法范围重选，非主干目标）
            string noun = TargetNoun(cfg, atom, header);
            if (newTargetNoun) noun = "新的" + noun;
            string body = tpl.Replace("{value}", number).Replace("{target}", noun);
            // {衍生物} → 模板卡名（2026-10-09 衍生物召唤配套）：SummonToken 的 str 指向真实生物卡——
            // 未填/未解析显「未指定模板」/原样 ID；表行括注「{衍生物}=字符串参数指向…」整段折叠为卡名
            if (body.Contains("{衍生物}"))
            {
                var tplCard = CardCatalog.GetById(atom.str);
                string tokenName = tplCard != null ? tplCard.CardName
                    : string.IsNullOrEmpty(atom.str) ? "未指定模板" : atom.str;
                body = body.Replace("{衍生物}=字符串参数指向一张真实生物卡，实例=该卡全参数复制", tokenName)
                           .Replace("{衍生物}", tokenName);
            }
            // 数值随机（RandomAmplitude&gt;0）：区间文本「x至x」已表达随机语义（2026-10-09 句式定案）——不再冠「随机」前缀
            // 目标随机（2026-10-09 下沉每原子+句式定案）：条目声明优先（1=随机抽/0=弹窗），
            // 未声明回落效果级 RandomTarget；奖励渲染（header=null）=条目声明直接生效。
            // 渲染=在正文第一个「目标」前插「随机」（"对随机目标造成3点伤害"）；域名次（己方单位等）
            // 无「目标」字样时冠在名次前（"对随机己方单位…"）；无名次可冠的行不加（原文照旧）
            bool rand = atom.rand != -1 ? atom.rand == 1
                : (header != null && header.RandomTarget != 0);
            if (rand)
            {
                int idx = body.IndexOf("目标", StringComparison.Ordinal);
                if (idx < 0 && noun.Length > 0) idx = body.IndexOf(noun, StringComparison.Ordinal);
                if (idx >= 0) body = body.Insert(idx, "随机");
            }
            return body;
        }

        /// <summary>按引用取表行渲染（便捷重载——refId 直查）。</summary>
        public static string Render(AtomicEffectEntry atom, CardEffectData header)
            => Render(AtomicEffectTable.GetByHashId(atom?.refId), atom, header);

        /// <summary>效果整体预览（两槽定案）：并列主序列=逐原子分号连接，原子带槽级 branch 载荷时
        /// 追加分支后缀（产出条件=「，如果…，奖励」自然句；引擎=正文短式「自由分支·{名}{x}」+「→奖励」；
        /// 局面门=「[条件]→奖励」记法）；遗留 kind=1 门步骤与抉择步骤照旧渲染。</summary>
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
                        // 引擎主干行短式正文（2026-10-09 八引擎定案「自由分支·{名}{x}」）；非引擎行照表行模板
                        string body = EngineShortBody(s.atomic) ?? RenderAtomEntry(s.atomic, h);
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
                            ? RenderRewardAtomEntry(s.thenSteps[0]) : "（未设奖励）";
                        parts.Add($"[{gate}]→{reward}");
                    }
                    else if (s.kind == 2) parts.Add($"抉择（{s.choices?.Count ?? 0} 模式）");
                }
            }
            return string.Join("；", parts);
        }

        /// <summary>槽级 branch 载荷的后缀文本（两槽定案）：无载荷返回空串。
        /// 产出条件（Outcome）=自然句式「，如果消灭了目标，{奖励}」（2026-10-05 文本表述定案）；
        /// 引擎（2026-10-09 短式化）=「→{奖励}」——引擎身份+参数在正文「自由分支·{名}{x}」（EngineShortBody）；
        /// 局面门沿用「[条件]→奖励」记法（对赌逆转是结构信息，句式承载不了）。</summary>
        public static string BranchSuffix(AtomicEffectEntry atom)
        {
            var b = atom?.branch;
            if (BranchEntryRules.IsPhantom(b)) return ""; // 含 null；JsonUtility 幽灵分支（settle=0）不当真分支
            string reward = b.then != null && b.then.Count > 0
                ? RenderRewardAtomEntry(b.then[0]) : "（未设奖励）";
            string cond;
            switch ((BranchSettleKind)b.settle)
            {
                case BranchSettleKind.Engine:
                    // 引擎身份+参数已在正文短式（自由分支·{名}{x}）——后缀只接奖励
                    return $"→{reward}";
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

        /// <summary>条目级奖励渲染（2026-10-08「新的目标」定案）：分支 Then 奖励统一走此口径——
        /// 产出条件/局面门/引擎/遗留门步骤的奖励文案与运行时弹选语义对齐（合法范围另选目标）。</summary>
        public static string RenderRewardAtomEntry(AtomicEffectEntry atom)
        {
            if (atom == null || string.IsNullOrEmpty(atom.refId)) return "原子";
            return RenderReward(AtomicEffectTable.GetByHashId(atom.refId), atom);
        }

        /// <summary>引擎主干行短式正文（2026-10-09 八引擎统一定案）：「自由分支·{中文名}{x}」——
        /// 拼点 x=奖励锚价推导门槛、倒计时声明 0 时 x=按奖励费换算回合（两者随 Then 奖励实时变，
        /// 公式经 CostDerivationService 与运行时判定同源）；其余引擎 x=engineParam（EngineParamRange 钳制显示）。
        /// 非引擎行/引擎身份未定义返回 null（调用方回退表行长模板——原子库列表行长文案保留完整玩法说明）。</summary>
        private static string EngineShortBody(AtomicEffectEntry atom)
        {
            var cfg = AtomicEffectTable.GetByHashId(atom.refId);
            if (!ComposerCatalog.IsEngineTrunkRow(cfg)) return null;
            var kind = BranchEngineKind.None;
            if (atom.branch != null && Enum.IsDefined(typeof(BranchEngineKind), atom.branch.engine))
                kind = (BranchEngineKind)atom.branch.engine;
            if (kind == BranchEngineKind.None
                && Enum.TryParse<AtomicEffectType>(cfg.EnumName, out var t))
                kind = ComposerCatalog.EngineKindOf(t);
            if (kind == BranchEngineKind.None) return null;

            var then = atom.branch?.then;
            int x;
            switch (kind)
            {
                case BranchEngineKind.Clash:
                    x = CostDerivationService.ClashThreshold(ToInstances(then));
                    break;
                case BranchEngineKind.Countdown:
                    x = CostDerivationService.CountdownTurnsOf(atom.branch?.engineParam ?? 0, ToInstances(then));
                    break;
                default:
                    ComposerCatalog.EngineParamRange(kind, out var mn, out var mx);
                    x = Math.Clamp(atom.branch?.engineParam ?? mn, mn, mx);
                    break;
            }
            return $"自由分支·{ComposerCatalog.EngineZhOf(kind)}{x}";
        }

        /// <summary>UI 奖励原子 → 运行时实例（与 EffectComposerScreen.RewardCost 同口径：
        /// ConvertAtomForUI 逐原子转换，行缺失/转换失败剔除）。</summary>
        private static List<AtomicEffectInstance> ToInstances(List<AtomicEffectEntry> then)
            => then?.Select(CardEffectConverter.ConvertAtomForUI).Where(i => i != null).ToList()
               ?? new List<AtomicEffectInstance>();
    }
}
