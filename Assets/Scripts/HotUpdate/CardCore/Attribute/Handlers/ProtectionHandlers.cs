using System;
using System.Collections.Generic;

namespace CardCore.Attribute.Handlers
{
    /// <summary>
    /// 保护投放（延时保护 3 行族；2026-10-31 阶段D EffectType 唯一化）：**档位改从行类型派生**
    /// ——每档一型（ProtectionHalve/ProtectionNull/ProtectionLifeFloor → ProtectionMode），
    /// str=Mode 解析口径退役（表 EffectType 列未翻新期间行仍=GrantProtection，按 str 回落过渡；
    /// 旧卡带 str 新链路忽略）。（目标=己方单位含角色，数量档 1/N/全 由效果级 SelectionMode 承载。）
    /// 施加=对目标附加 {value} 层保护指示物（层=持续到自己第 N 个回合结束，持有者回合结束 −1
    /// 归零，换区清；Entity 级角色可持有）——同档叠加层数延长持续（AddCounters 累计）。
    /// 效果/指示物分离定案延续。（全体档「Mode|All」已删〔2026-10-30〕——旧 str 段多余部分忽略。）
    /// </summary>
    public class GrantProtectionHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.GrantProtection;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            if (!TryResolveMode(effect, out var mode))
            {
                TideLog.Warn($"[GrantProtectionHandler] 档位不可解析（类型 {effect.Type}，str '{effect.StringValue}'），空转");
                return;
            }

            var counterId = ProtectionRules.CounterIdOf(mode);
            // 2026-10-31 晚定案：一次发动按 value 给层（层=持续回合，≤0/未填=1）
            int layers = CounterRules.ResolveLayers(counterId, context.GetValueAfterModifiers(effect.Value));

            foreach (var target in context.Targets)
            {
                if (target == null || !target.IsAlive) continue;
                int actualDelta = target.AddCounters(counterId, layers, context.Source);
                PublishEvent(new CounterChangedEvent
                {
                    Target = target,
                    CounterType = counterId,
                    Amount = actualDelta,
                    Source = context.Source,
                });
            }
        }

        /// <summary>档位解析（阶段D：类型派生优先；旧共享型 GrantProtection 回落 str=Mode——
        /// 段多余部分（旧「|All」全体段）忽略，按单体降级）。</summary>
        internal static bool TryResolveMode(AtomicEffectInstance effect, out ProtectionMode mode)
        {
            switch (effect.Type)
            {
                case AtomicEffectType.ProtectionHalve: mode = ProtectionMode.Halve; return true;
                case AtomicEffectType.ProtectionNull: mode = ProtectionMode.Null; return true;
                case AtomicEffectType.ProtectionLifeFloor: mode = ProtectionMode.Floor; return true;
                default: return TryParse(effect.StringValue, out mode);
            }
        }

        /// <summary>旧 str 解析（仅未翻新行/旧实例数据走到；模式段非法即 false）。</summary>
        internal static bool TryParse(string str, out ProtectionMode mode)
        {
            mode = default;
            if (string.IsNullOrEmpty(str)) return false;
            var parts = str.Split('|');
            return Enum.TryParse(parts[0], true, out mode);
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect)
            => TryResolveMode(effect, out var mode)
                ? $"附加一层{ProtectionRules.ModeZh(mode)}指示物（持有者回合结束−1）"
                : "延时保护";
    }
}
