using System.Collections.Generic;
using CardCore.Attribute;

namespace CardCore.Effects
{
    // =====================================================================================
    // 值浮字规则（2026-11-13《战场数值表现统一修改计划》P2·§3.3/§3.4/附录 B）：
    // 「短标签 + 有符号数」与绿/红/中性极性的唯一单源——本地实时/联机/观战/回放四端共用
    //（CardCore 无 Unity 依赖；表现层只消费拼好的 label 与 polarity，不各自推导）。
    //
    // 极性（§3.4，不做「正数=绿」机械判断）：
    //   增益 =（正极性指示物 且 增量>0）或（负极性指示物 且 增量<0）→ +1 绿
    //   减益 =（正极性指示物 且 增量<0）或（负极性指示物 且 增量>0）→ -1 红
    //   中性 = 增量 0（调用方零增量直接不出）；正负极性读 CounterRules.IsNegative 单源。
    //   例：护甲被吸收（正极性 −N）=红；毒素被净化（负极性 −N）=绿。
    // =====================================================================================
    public static class ValueFloatRules
    {
        /// <summary>指示物 → 浮字短标签（附录 B）。P/L/±1 族走专用文案（现役 P +N/L +N/±N/±N 沿用）。</summary>
        private static readonly Dictionary<string, string> Labels = new Dictionary<string, string>
        {
            { KeywordRules.ArmorCounter, "护甲" },
            { CounterRules.DivineShieldCounter, "圣盾" },
            { CounterRules.ToughnessCounter, "坚韧" },
            { CounterRules.ToxinCounter, "毒素" },
            { KeywordRules.FreezeCounter, "冻结" },
            { CounterRules.VulnerableCounter, "易损" },
            { CounterRules.DurabilityCounter, "耐久" },
            { CounterRules.RebornCounter, "复生" },
            { CounterRules.StealthCounter, "潜行" },
            { CounterRules.SpellShieldCounter, "法术护盾" },
            { CounterRules.RetaliationCounter, "荆棘" },
            { CounterRules.DepravityCounter, "反疗" },
            { CounterRules.SilenceCounter, "沉默" },
            { CounterRules.NullifyCounter, "失效" },
            { CounterRules.CurseCounter, "诅咒" },
            { CounterRules.BlessingCounter, "祝福" },
            { CounterRules.ReplayCounter, "重放" },
            { CounterRules.TempCounter, "临时" },
            { CounterRules.OverloadCounter, "过载" },
            { CounterRules.CostDownGrayCounter, "费" },
            { CounterRules.CostDownRedCounter, "费" },
            { CounterRules.CostDownBlueCounter, "费" },
            { CounterRules.CostDownGreenCounter, "费" },
            { CounterRules.ExposedCounter, "展示" },
            { CounterRules.MorphStateCounter, "变形" },
            { CounterRules.HalveShieldCounter, "减半盾" },
            { CounterRules.NullShieldCounter, "全免盾" },
            { CounterRules.LifeFloorCounter, "生命锁" },
            { CounterRules.DeathTollCounter, "死亡计数" },
        };

        /// <summary>标签查询：短标签表 → 未列出回落 CounterSpec.DisplayName（长度 &gt; 4 取前 4 字）；
        /// 无规格（未知 id）回落 id 原文。P/L/±1 族返回 null（调用方走专用文案）。</summary>
        public static string LabelOf(string counterType)
        {
            if (IsStatFamily(counterType)) return null;
            if (Labels.TryGetValue(counterType, out var label)) return label;
            var spec = CounterRules.Find(counterType);
            if (spec == null || string.IsNullOrEmpty(spec.DisplayName)) return counterType;
            return spec.DisplayName.Length > 4 ? spec.DisplayName.Substring(0, 4) : spec.DisplayName;
        }

        /// <summary>P/L/±1 属性族（沿用现役文案：P +N / L -N / +N/+N / -N/-N）。</summary>
        public static bool IsStatFamily(string counterType)
            => counterType == CounterRules.PowerUpCounter
               || counterType == CounterRules.PowerDownCounter
               || counterType == CounterRules.LifeUpCounter
               || counterType == CounterRules.LifeDownCounter
               || counterType == CounterRules.PlusOneCounter
               || counterType == CounterRules.MinusOneCounter
               || counterType == CounterRules.HeroAttackCounter;

        /// <summary>属性族文案（P +N / L -N / +N/+N / -N/-N / P +N·角色攻击弹药）；
        /// 非属性族返回 null。Down 族事件 Amount 恒正（层数），文案侧取反映向。</summary>
        public static string StatLabel(string counterType, int amount)
        {
            if (counterType == null) return null;
            if (counterType == CounterRules.PowerUpCounter || counterType == CounterRules.HeroAttackCounter)
                return "P " + Sign(amount);
            if (counterType == CounterRules.PowerDownCounter)
                return "P " + Sign(-amount);
            if (counterType == CounterRules.LifeUpCounter)
                return "L " + Sign(amount);
            if (counterType == CounterRules.LifeDownCounter)
                return "L " + Sign(-amount);
            if (counterType == CounterRules.PlusOneCounter)
                return Sign(amount) + "/" + Sign(amount);
            if (counterType == CounterRules.MinusOneCounter)
                return Sign(-amount) + "/" + Sign(-amount);
            return null;
        }

        /// <summary>指示物浮字构造（CounterChangedEvent → label + polarity）。
        /// amount=实际增量（带符号；0 由调用方拦截不出）。</summary>
        public static bool TryBuild(string counterType, int amount, out string label, out int polarity)
        {
            label = null;
            polarity = 0;
            if (string.IsNullOrEmpty(counterType) || amount == 0) return false;

            label = StatLabel(counterType, amount);
            if (label != null)
            {
                // 属性族：PowerUp/LifeUp/PlusOne/HeroAttack 为正极性档，PowerDown/LifeDown/MinusOne 为负极性档——
                // 文案已带方向，颜色按「读数向好的方向=绿」：增量使 Power+ 或 Life+ = 绿。
                polarity = IsGainDirection(counterType, amount) ? 1 : -1;
                return true;
            }

            string tag = LabelOf(counterType);
            if (tag == null) return false; // 未知指示物——不出浮字（防脏数据糊屏）
            label = tag + " " + Sign(amount);
            bool counterNegative = CounterRules.IsNegative(counterType); // 负极性=妨害类（毒素/冻结/易损…）
            bool amountNegative = amount < 0;
            // §3.4：负极性指示物加层（妨害加深）=红；减层（妨害解除）=绿；正极性反之。
            polarity = (counterNegative ^ amountNegative) ? -1 : 1;
            return true;
        }

        /// <summary>属性变化方向（Power/Life 读数变好=绿）：Up 族正量=增益；Down 族正量（读数下降）=减益。
        /// 调用方传带符号增量（Down 族消耗时引擎发负量=读数回升=增益）。</summary>
        private static bool IsGainDirection(string counterType, int amount)
        {
            bool downFamily = counterType == CounterRules.PowerDownCounter
                              || counterType == CounterRules.LifeDownCounter
                              || counterType == CounterRules.MinusOneCounter;
            return downFamily ? amount < 0 : amount > 0;
        }

        /// <summary>有符号数（ASCII 负号，与现役 P +N 同字面）。</summary>
        public static string Sign(int value) => value >= 0 ? "+" + value : value.ToString();

        /// <summary>属性直改/设为的浮字标签（2026-11-13 P2/P3 单源——本地 Projector 与联机
        /// NetFxMapper 共用）：Power=P ±N、Life=L ±N、Both=±N/±N（delta 已带符号）。</summary>
        public static string StatDeltaLabel(StatType stat, int delta)
        {
            string n = Sign(delta);
            switch (stat)
            {
                case StatType.Power: return "P " + n;
                case StatType.Life: return "L " + n;
                default: return n + "/" + n; // Both：攻生同改（罕见——直改族通常单属性）
            }
        }
    }
}
