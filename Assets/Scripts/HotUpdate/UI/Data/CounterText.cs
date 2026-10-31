using System.Collections.Generic;
using CardCore;
using CardCore.Attribute;

namespace SynergyUI
{
    /// <summary>
    /// 指示物展示文案（2026-10-15 战场 hover-info 四段定案）：id → 完整解说（行为语义 + 生命周期）。
    /// 行为语义蒸馏自 CounterRules 注册行注释（引擎侧无解说字段——本表即展示层单一来源，
    /// 新指示物=此处补一行；id 引用 CounterRules/KeywordRules 常量防漂移）。
    /// 未登记 id 走 CounterSpec 四分类兜底（不静默丢状态）。
    /// </summary>
    public static class CounterText
    {
        private static readonly Dictionary<string, string> ExplainById = new Dictionary<string, string>
        {
            // ---- 生效自减（Exception：生效消耗层，跨区存活）----
            { KeywordRules.ArmorCounter, "每层吸收 1 点受到的伤害（逐点消耗层）；生效时消耗，不随换区清除，净化可清" },
            { CounterRules.DivineShieldCounter, "每层完全抵挡一次任意伤害（战斗与效果）并消耗 1 层" },
            { CounterRules.RebornCounter, "每层替代一次死亡——不离场不进墓，生命变为 1 并横置，消耗 1 层；湮灭不可拦" },
            { CounterRules.StealthCounter, "持有期间不可被攻击、不可被指定为效果目标；攻击宣言/发动效果/实际受到伤害各消耗 1 层" },
            { CounterRules.SpellShieldCounter, "每层抵消一次对手效果对自身的作用（效果执行时被移出目标列表），消耗 1 层" },
            { CounterRules.ToughnessCounter, "每次受到伤害时每层使伤害 −1；实际拦到即生效，随后层数减半（向下取整）" },
            { CounterRules.ToxinCounter, "持有者每回合结束受到等同于当前层数的伤害，随后层数减半（向下取整）" },
            { CounterRules.VulnerableCounter, "每次受到伤害时每层使受到的伤害 +1；生效后层数减半（向下取整）" },
            { CounterRules.HeroAttackCounter, "层数=角色攻击力弹药；角色攻击或反击结算后全部移除" },
            { CounterRules.CurseCounter, "抽到该卡时自动执行诅咒分支效果并消耗 1 层（跨区存活）" },
            { CounterRules.BlessingCounter, "抽到该卡时自动执行祝福分支效果并消耗 1 层（跨区存活）" },
            { CounterRules.DepravityCounter, "下一次受到的治疗改写为等量伤害并消耗 1 层；多层=逐次各拦一次治疗" },
            { CounterRules.ReplayCounter, "该单位效果发动结算后自动消耗 1 层并再发动一次（重放不触发重放）" },
            { CounterRules.TempCounter, "持有者回合结束时若该卡不在战场，从游戏中移除（不进墓）" },
            // ---- 常驻（Resident：换区清除）----
            { CounterRules.PlusOneCounter, "每层攻击力与生命 +1；换区清除" },
            { CounterRules.MinusOneCounter, "每层攻击力与生命 −1；换区清除" },
            { CounterRules.SilenceCounter, "持有者不可发动主动效果；换区清除" },
            { CounterRules.NullifyCounter, "持有者的自动效果失效（触发式与本体关键词；不影响光环——2026-10-31 失效改名定案：光环域整体退出压制）；换区清除" },
            { CounterRules.DurabilityCounter, "发动次数池——主动发动与场上被动触发各 −1；战场归零销毁入墓，技能栏归零休眠（回合开始 +1 回充）" },
            { CounterRules.PowerUpCounter, "每层攻击力 +1；仅场上，换区清除" },
            { CounterRules.PowerDownCounter, "每层攻击力 −1；仅场上，换区清除" },
            { CounterRules.LifeUpCounter, "每层生命上限与当前生命 +1；仅场上，换区清除" },
            { CounterRules.LifeDownCounter, "每层生命上限 −1；仅场上，换区清除" },
            { CounterRules.CostUpCounter, "每层费用 +1；仅手牌，离手消失" },
            { CounterRules.CostDownCounter, "每层费用 −1；仅手牌，离手消失" },
            { CounterRules.CostUpGrayCounter, "每层灰色费用 +1；仅手牌，离手消失" },
            { CounterRules.CostUpRedCounter, "每层红色费用 +1；仅手牌，离手消失" },
            { CounterRules.CostUpBlueCounter, "每层蓝色费用 +1；仅手牌，离手消失" },
            { CounterRules.CostUpGreenCounter, "每层绿色费用 +1；仅手牌，离手消失" },
            { CounterRules.CostDownGrayCounter, "每层灰色费用 −1；仅手牌，离手消失" },
            { CounterRules.CostDownRedCounter, "每层红色费用 −1；仅手牌，离手消失" },
            { CounterRules.CostDownBlueCounter, "每层蓝色费用 −1；仅手牌，离手消失" },
            { CounterRules.CostDownGreenCounter, "每层绿色费用 −1；仅手牌，离手消失" },
            { CounterRules.SkillUseCounter, "该技能累计发动次数（升级门读数）；随技能卡存续" },
            { CounterRules.ExposedCounter, "该卡持续暴露，双方可查看；离开当前区域即失效" },
            { CounterRules.MorphStateCounter, "模拟/变形状态标记（层恒 1）——解除即还原原形态" },
            // ---- 自然衰退（Decay：回合末 −1 归零，换区清除）----
            { KeywordRules.FreezeCounter, "持有期间无法重置（保持横置）；持有者回合结束层数 −1，归零解除" },
            { KeywordRules.RushSicknessCounter, "持有期间不可攻击角色（只能攻击随从）；持有者回合结束层数 −1，归零消失" },
            { KeywordRules.SleepCounter, "持有期间无法重置且效果无效；持有者回合结束层数 −1，归零解除" },
            { CounterRules.LockCounter, "持有期间无法打出、无法进入发动区与地牌区；持有者回合结束层数 −1，归零解锁" },
            // ---- 系统（System：引擎投放/消耗）----
            { CounterRules.CountdownCounter, "控制者回合开始层数 −1，归零触发奖励并重置" },
            { CounterRules.DeathTollCounter, "此卡在手牌或场上时计数双方生物死亡；计数≥x（x=奖励锚价推导）触发奖励；满 6 不再计数；使用后清零" },
        };

        /// <summary>单条状态行：「显示名×层数：完整解说」（与卡面 stats 芯片同 DisplayName 口径）。</summary>
        public static string Line(string counterId, int layers)
        {
            string name = DisplayNameOf(counterId);
            string body = Explain(counterId);
            return layers != 1 ? $"{name}×{layers}：{body}" : $"{name}：{body}";
        }

        /// <summary>显示名：登记 spec 的 DisplayName，未登记退回 id 原串。</summary>
        public static string DisplayNameOf(string counterId)
        {
            var spec = CounterRules.Find(counterId);
            return string.IsNullOrEmpty(spec?.DisplayName) ? counterId : spec.DisplayName;
        }

        /// <summary>完整解说：登记 id 查表；未登记按 CounterSpec 四分类+极性兜底（新指示物漏登记不静默）。</summary>
        public static string Explain(string counterId)
        {
            if (!string.IsNullOrEmpty(counterId) && ExplainById.TryGetValue(counterId, out var text))
                return text;
            var spec = CounterRules.Find(counterId);
            if (spec == null) return "未知指示物（未登记规则）";
            string life = spec.Class switch
            {
                CounterClass.Decay => "持有者回合结束层数 −1、归零消失，换区清除",
                CounterClass.Resident => "常驻状态，换区清除",
                CounterClass.Exception => "生效时消耗层，不随换区清除（跨区存活）",
                _ => "引擎系统计数",
            };
            string pol = spec.Polarity == CounterPolarity.Negative ? "负面" : spec.Polarity == CounterPolarity.Positive ? "正面" : "中性";
            return $"{pol}指示物；{life}";
        }
    }
}
