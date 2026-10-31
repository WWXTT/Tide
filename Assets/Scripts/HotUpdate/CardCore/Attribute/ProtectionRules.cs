using System;

namespace CardCore.Attribute
{
    /// <summary>保护档位（GrantProtection 原子族的 str 段；2026-10-30 指示物化定案）。</summary>
    public enum ProtectionMode
    {
        /// <summary>伤害减半（取整向上：3→2、1→1）。</summary>
        Halve,
        /// <summary>免疫（原全免——变为 0：击杀/吸血/系命/报复/剧毒全链不触发）。</summary>
        Null,
        /// <summary>生命锁：落血后生命值不低于 1（不拦消灭/摧毁——非落血路径）。</summary>
        Floor,
    }

    /// <summary>
    /// 保护改写单源（2026-10-30 指示物化改版：状态机整体退役，持续/衰减/换区清/净化全走
    /// CounterRules 指示物体系——HalveShield/NullShield/LifeFloor 三条 Decay 指示物：
    /// 层=持续到自己第 N 个回合结束，持有者回合结束 −1 归零，换区清；Entity 级角色可持有）。
    ///
    /// 消费口（签名承袭状态机版，调用方零改动）：
    /// · KeywordRules.ApplyDamage（守护改写之后、易损/防护层之前）——Halve/Null 改写伤害，
    ///   穿透伤害照挡（「伤害」语义）；Null 归 0 提前出管线；多档并挂取最强（Null 压 Halve）。
    /// · 落血三路（ApplyDamage 角色分支 / LifeLossHandler 流失）——Floor 落血后钳 1。
    /// 施加口=GrantProtectionHandler（目标=己方单位含角色；2026-10-31 指示物定案：每次发动恒加一层、
    /// 叠加=延长；全体档已删 2026-10-30——指示物只作用于挂载单位）。
    /// </summary>
    public static class ProtectionRules
    {
        /// <summary>档位 → 指示物 id（GrantProtectionHandler 施加用）。</summary>
        public static string CounterIdOf(ProtectionMode mode)
            => mode == ProtectionMode.Halve ? CounterRules.HalveShieldCounter
             : mode == ProtectionMode.Null ? CounterRules.NullShieldCounter
             : CounterRules.LifeFloorCounter;

        /// <summary>伤害改写（NullShield→0、HalveShield→ceil(amount/2)；Floor/无层原样返回。
        /// 多档并挂取最强：免疫压减半）。</summary>
        public static int ModifyDamage(Entity target, int amount)
        {
            if (amount <= 0 || target == null) return amount;
            if (target.GetCounterCount(CounterRules.NullShieldCounter) > 0) return 0;
            if (target.GetCounterCount(CounterRules.HalveShieldCounter) > 0)
                return (amount + 1) / 2; // 取整向上：3→2、1→1
            return amount;
        }

        /// <summary>生命钳（LifeFloor=max(1, life)；无层原样返回）。</summary>
        public static int ClampLife(Entity target, int life)
            => target != null && target.GetCounterCount(CounterRules.LifeFloorCounter) > 0
                ? Math.Max(1, life)
                : life;

        internal static string ModeZh(ProtectionMode mode)
            => mode == ProtectionMode.Halve ? "伤害减半"
             : mode == ProtectionMode.Null ? "免疫" : "生命锁";
    }
}
