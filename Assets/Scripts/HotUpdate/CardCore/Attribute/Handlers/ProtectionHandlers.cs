using System;
using System.Collections.Generic;

namespace CardCore.Attribute.Handlers
{
    /// <summary>
    /// 保护投放（GrantProtection 原子，2026-10-30 指示物化定案后的 3 行族）：
    /// str="Mode"（目标=己方单位含角色，数量档 1/N/全 由效果级 SelectionMode 承载）。
    /// 施加=对目标附加 {value} 层保护指示物（层=持续到自己第 N 个回合结束，持有者回合结束 −1
    /// 归零，换区清；Entity 级角色可持有）——同档叠加层数延长持续（AddCounters 累计）。
    /// 表行不带 str——合成器 EffectComposerScreen.ProtectionIds 按 EnumName 写入；
    /// 手写 Effects.json 须显式写 str。效果/指示物分离定案延续。
    /// （全体档「Mode|All」已删〔2026-10-30 用户定案：指示物没有全体的说法，只作用于挂载单位〕——
    /// 旧 str 段多余部分忽略，按单体档降级结算。）
    /// </summary>
    public class GrantProtectionHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.GrantProtection;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            if (!TryParse(effect.StringValue, out var mode))
            {
                TideLog.Warn($"[GrantProtectionHandler] str 非法（应为 Mode，实得 '{effect.StringValue}'），空转");
                return;
            }

            // 2026-10-31 指示物定案：每次发动恒加一层（层=持续回合；叠加=延长）
            int layers = 1;
            var counterId = ProtectionRules.CounterIdOf(mode);

            foreach (var target in context.Targets)
            {
                if (target == null || !target.IsAlive) continue;
                target.AddCounters(counterId, layers, context.Source);
                PublishEvent(new CounterChangedEvent
                {
                    Target = target,
                    CounterType = counterId,
                    Amount = layers,
                    Source = context.Source,
                });
            }
        }

        /// <summary>str 解析（模式段非法即 false——空转告警由调用方统一发；
        /// 段多余部分（旧「|All」全体段）忽略——2026-10-30 全体档退役按单体降级）。</summary>
        internal static bool TryParse(string str, out ProtectionMode mode)
        {
            mode = default;
            if (string.IsNullOrEmpty(str)) return false;
            var parts = str.Split('|');
            return Enum.TryParse(parts[0], true, out mode);
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect)
            => TryParse(effect.StringValue, out var mode)
                ? $"附加一层{ProtectionRules.ModeZh(mode)}指示物（持有者回合结束−1）"
                : "延时保护";
    }
}
