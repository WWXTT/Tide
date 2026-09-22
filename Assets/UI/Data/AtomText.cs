using System;
using System.Collections.Generic;
using CardCore;
using CardCore.Attribute;
using UnityEngine;

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
        /// <summary>TargetKind 中文（2026-09-22 公共化：合成器目标域下拉与 {target} 渲染单一来源）。</summary>
        public static readonly Dictionary<TargetKind, string> TargetKindZhMap = new Dictionary<TargetKind, string>
        {
            { TargetKind.Self, "自己" },
            { TargetKind.OwnLivingUnit, "己方单位" },
            { TargetKind.EnemyLivingUnit, "对方单位" },
            { TargetKind.OwnNonLivingUnit, "己方无生命单位" },
            { TargetKind.EnemyNonLivingUnit, "对方无生命单位" },
            { TargetKind.OwnHand, "己方手牌" },
            { TargetKind.EnemyHand, "对方手牌" },
            { TargetKind.OwnDeck, "己方牌库" },
            { TargetKind.EnemyDeck, "对方牌库" },
            { TargetKind.OwnGraveyard, "己方墓地" },
            { TargetKind.EnemyGraveyard, "对方墓地" },
            { TargetKind.OwnExile, "己方除外区" },
            { TargetKind.EnemyExile, "对方除外区" },
            { TargetKind.OwnElementPool, "己方元素池" },
            { TargetKind.EnemyElementPool, "对方元素池" },
            { TargetKind.OwnActivation, "己方发动区" },
            { TargetKind.EnemyActivation, "对方发动区" },
        };

        /// <summary>TargetKind → 中文名（无映射回退枚举名）。</summary>
        public static string TargetKindZhOf(TargetKind k)
            => TargetKindZhMap.TryGetValue(k, out var zh) ? zh : k.ToString();

        /// <summary>{target} 占位的名词：单值实例域 → 中文名；null（表默认）/多值 → 「目标」。</summary>
        public static string TargetNoun(AtomicEffectEntry atom)
        {
            if (atom?.kinds != null && atom.kinds.Count == 1)
                return TargetKindZhOf((TargetKind)atom.kinds[0]);
            return "目标";
        }

        /// <summary>渲染单原子描述。cfg 为空（无表行 fallback 原子）时回退 refId。</summary>
        public static string Render(AtomicEffectConfig cfg, AtomicEffectEntry atom, CardEffectData header)
        {
            if (atom == null || string.IsNullOrEmpty(atom.refId)) return "原子效果";
            string tpl = cfg != null && !string.IsNullOrEmpty(cfg.Description) ? cfg.Description : atom.refId;

            int v = atom.value;
            int span = atom.amp > 0f ? Mathf.RoundToInt(Math.Abs(v) * atom.amp) : 0;
            string number = span > 0 ? $"{Mathf.Max(0, v - span)}至{v + span}" : v.ToString();
            // {target} → 实例域名次（2026-09-22 五轮：单值域显作用对象；表默认/多值保持「目标」）
            string body = tpl.Replace("{value}", number).Replace("{target}", TargetNoun(atom));
            if (span > 0) body = "随机 " + body;
            if (header != null && header.RandomTarget != 0)
                body = "随机目标·" + body;
            return body;
        }

        /// <summary>按引用取表行渲染（便捷重载——refId 直查）。</summary>
        public static string Render(AtomicEffectEntry atom, CardEffectData header)
            => Render(AtomicEffectTable.GetByHashId(atom?.refId), atom, header);

        /// <summary>效果整体预览：并列=逐原子分号连接；自由分支=主干模板+奖励；有限分支=主干+门+奖励。</summary>
        public static string RenderEffectSummary(EffectGraphData graph)
        {
            if (graph?.header == null) return "";
            var h = graph.header;
            if (h.EngineKind != (int)BranchEngineKind.None)
            {
                string trunk = TrunkText((BranchEngineKind)h.EngineKind, h.EngineParam);
                string reward = h.AtomicEffects != null && h.AtomicEffects.Count > 0
                    ? RenderAtomEntry(h.AtomicEffects[0])
                    : "（未设奖励）";
                return $"{trunk} → 奖励：{reward}";
            }
            var parts = new System.Collections.Generic.List<string>();
            if (graph.steps != null)
            {
                foreach (var s in graph.steps)
                {
                    if (s == null) continue;
                    if (s.kind == 0 && s.atomic != null) parts.Add(RenderAtomEntry(s.atomic));
                    else if (s.kind == 1)
                    {
                        string gate = string.IsNullOrEmpty(s.conditionId) ? "?" : s.conditionId;
                        string reward = s.thenSteps != null && s.thenSteps.Count > 0
                            ? RenderAtomEntry(s.thenSteps[0]) : "（未设奖励）";
                        parts.Add($"[{gate}]→{reward}");
                    }
                    else if (s.kind == 2) parts.Add($"抉择（{s.choices?.Count ?? 0} 模式）");
                }
            }
            return string.Join("；", parts);
        }

        /// <summary>条目级渲染（refId → 表行 → Render；行缺失回退 refId）。</summary>
        public static string RenderAtomEntry(AtomicEffectEntry atom)
        {
            if (atom == null || string.IsNullOrEmpty(atom.refId)) return "原子";
            return Render(AtomicEffectTable.GetByHashId(atom.refId), atom, null);
        }

        /// <summary>引擎主干显示文本（header 通道无 AtomicEffectEntry——按引擎+参数生成）。</summary>
        public static string TrunkText(BranchEngineKind engine, int param)
        {
            switch (engine)
            {
                case BranchEngineKind.Clash:
                    return $"拼点：牌库顶费用 > 对手 + {param} 时执行奖励（机制费 {param} 灰）";
                case BranchEngineKind.LuckRoll:
                    return $"运势：2d6 两点均 > {param} 时执行奖励（机制费 {param} 灰）";
                case BranchEngineKind.Countdown:
                    return param > 0
                        ? $"倒计时 {param} 回合，归零执行奖励并重置"
                        : "倒计时：按奖励推导费自动换算回合（1费=1回合）";
                default:
                    return "";
            }
        }
    }
}
