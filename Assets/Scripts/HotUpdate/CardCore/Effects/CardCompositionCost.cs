using System;
using System.Collections.Generic;

namespace CardCore
{
    /// <summary>
    /// 卡层组合费用（「效果→卡」组合层，2026-09-07 定案）——独立的第三层计价：
    /// · 原子层（CostDerivationService）：原子→效果锚价（并发取总 / 条件奖励免费 / 抉择 per-mode）；
    /// · 规则一（CardCostService.Derive）：声明档位推导 D(C) 与代价抵扣校验；
    /// · 本层：只对「卡的结构性弹性」收额外费——玩家为灵活性付费，与效果本身的强度无关。
    ///
    /// 当前规则（2026-10-10 可支配底盘定案，取代 09-10/09-21 底盘预算与同日早间「剩余作废」案）：
    /// ①底盘 = 显性三万能色（经济依据 = 入组 1 + 抽牌 2）：3 点额度（ChassisBudget），
    ///   结构项各占 1 点（ChassisItemRate）——攻/守（生物默认自带，NoAttack/NoGuard opt-out）、
    ///   效果槽 ×CountEffectSlots（抉择分支计槽 2026-09-21 维持；启动式照常计槽——AI 可见性锚）。
    /// ②剩余 N = 3 − 结构项，**可支配、不作废**（构筑期玩家按卡型自选）：
    ///   · 法术：抵价 N−k 或提速 k 档（SpeedBonus 卡级字段，k∈0..N——1 点提速 = 放弃 1 点抵价；
    ///     运行时与全部效果 BaseSpeed 加和，见 GameActions.GetCardEffectDefinitions）；
    ///   · 生物/地牌：全额抵价（无提速——生物速度只由效果组合阶段 BaseSpeed 决定）；
    ///   · 结界：二选一——该结界全部启动式效果**每次发动**现付元素费永久 −N（逐点扣、下限 0，
    ///     EffectExecutionEngine 结算期落点）；或入场耐久 +N（SurplusToDurability，
    ///     EquipRules/HeroSkillSystem 初始化处加，免费不加价）。
    /// ③结构项 >3 → 差额加价入灰（StructuralSurcharge，不吃 d(C)、取整后落灰）——与剩余互斥
    ///  （结构项 =3 时双零；>3 时无剩余可支配）。
    /// ④抵价落位：d(C) 取整后总价逐点扣，先灰后最高费用色（ReduceBuckets——原 09-10 退费默认
    ///   算法复活；RefundColor 自标随字段删除退役）。
    /// ⑤衍生物无底盘（CardCostService.ContentPriceOf/ByColor 口径）：未走「入组/抽牌」通道，
    ///   结构费全额计、无剩余可支配——见 CostDerivation SummonToken 分支。
    ///
    /// 应用点：CardCostService.Derive/DeriveModeCosts/BuildCostAtTier 统一附加；
    /// 运行时消费口：GameActions（法术提速）/ EffectExecutionEngine（结界发动费抵扣）/
    /// EquipRules·HeroSkillSystem（结界耐久 +N）。
    /// 参数见 ValueSystemRuntimeConfig.CardCompositionConfig（表 Category=CardComposition）。
    /// </summary>
    public static class CardCompositionCost
    {
        /// <summary>结构项数（未乘费率）：攻在 + 守在 + 效果槽（CountEffectSlots 口径，抉择分支计槽）。</summary>
        public static int ChassisItems(CardData card)
        {
            bool isCreature = card != null && card.Supertype == Cardtype.Creature;
            int atk = isCreature && !card.NoAttack ? 1 : 0;
            int grd = isCreature && !card.NoGuard ? 1 : 0;
            return atk + grd + CostDerivationService.CountEffectSlots(card);
        }

        /// <summary>
        /// 超槽加价（恒 ≥0、永不退费）：结构项 × 费率 超出 3 点额度的差额，取整后入灰（不吃 d(C)）。
        /// 与 SurplusOf 互斥。攻/守 = 生物默认自带（NoAttack/NoGuard 玩家自选 opt-out——
        /// Power=0 不自动剥夺，2026-10-10 撤销「Power=0 视同 NoAttack」）。
        /// </summary>
        public static int StructuralSurcharge(CardData card)
        {
            var cfg = ValueSystemConfigManager.Instance.GetOrCreateConfig().CardCompositionConfig;
            int budget = (int)Math.Max(0f, cfg.ChassisBudget);
            int rate = Math.Max(1, (int)Math.Max(0f, cfg.ChassisItemRate));
            return Math.Max(0, ChassisItems(card) * rate - budget);
        }

        /// <summary>
        /// 底盘剩余点 N（恒 ≥0）：3 点额度被结构项占用后的盈余，按卡型支配（类头②）——
        /// 即「法术减两费 / 白板退 1」的统一机制形态（每卡同额、非类型补贴）。
        /// </summary>
        public static int SurplusOf(CardData card)
        {
            var cfg = ValueSystemConfigManager.Instance.GetOrCreateConfig().CardCompositionConfig;
            int budget = (int)Math.Max(0f, cfg.ChassisBudget);
            int rate = Math.Max(1, (int)Math.Max(0f, cfg.ChassisItemRate));
            return Math.Max(0, budget - ChassisItems(card) * rate);
        }

        /// <summary>计价侧抵价点数（ApplyChassis 消费）：结界 = 0（剩余走运行时二向）；
        /// 法术 = N − clamp(SpeedBonus,0,N)（提速占用的点不再抵价）；生物/地牌/其余 = N。</summary>
        public static int PriceOffsetOf(CardData card)
        {
            if (card == null) return 0;
            int surplus = SurplusOf(card);
            if (surplus <= 0) return 0;
            if (card.Supertype == Cardtype.Enchantment) return 0;
            if (card.Supertype == Cardtype.Spell)
                return surplus - Math.Clamp(card.SpeedBonus, 0, surplus);
            return surplus;
        }

        /// <summary>结界启动式发动费抵扣点数（EffectExecutionEngine 结算期落点）：
        /// 结界且未选转耐久时 = 剩余 N——该结界全部启动式效果每次发动现付元素锚价逐点扣、下限 0、永久。</summary>
        public static int ActivationFeeOffsetOf(CardData card)
        {
            if (card == null || card.Supertype != Cardtype.Enchantment) return 0;
            if (card.SurplusToDurability) return 0;
            return SurplusOf(card);
        }

        /// <summary>结界入场耐久加成点数（EquipRules/HeroSkillSystem 初始化消费）：转耐久时 = 剩余 N。
        /// 仅耐久体（Durability>0）可转——持续型结界无次数池，转耐久无意义（开关视为无效）。</summary>
        public static int DurabilityBonusOf(CardData card)
        {
            if (card == null || card.Supertype != Cardtype.Enchantment) return 0;
            if (!card.SurplusToDurability || card.Durability <= 0) return 0;
            return SurplusOf(card);
        }

        /// <summary>结构费全额（ChassisItemRate × 结构项，**无预算抵扣**）——衍生物口径专用
        ///（ContentPriceOf 组成部分：衍生物没走「入组 1 + 抽牌 2」，吃不到 3 点底盘）。</summary>
        public static int StructureFeeFull(CardData card)
        {
            var cfg = ValueSystemConfigManager.Instance.GetOrCreateConfig().CardCompositionConfig;
            int rate = Math.Max(1, (int)Math.Max(0f, cfg.ChassisItemRate));
            return ChassisItems(card) * rate;
        }

        /// <summary>
        /// 抵价逐点扣桶（原 ApplyChassisRefund 默认算法复活；RefundColor 自标已随字段删除退役）：
        /// 先扣灰桶（≥1 才扣），灰不足（法术常无灰分量）逐点从最高费用色桶扣（并列取枚举序靠前者）。
        /// 全桶空则停——抵价后可为 0（2026-10-10 放宽：免费地板=抵价耗尽即可，非「内容为 0 才免费」）。
        /// </summary>
        public static void ReduceBuckets(Dictionary<ManaType, int> mounted, int points)
        {
            for (int i = 0; i < points; i++)
            {
                if (mounted.TryGetValue(ManaType.Gray, out var g) && g >= 1)
                {
                    mounted[ManaType.Gray] = g - 1;
                    continue;
                }
                ManaType best = default;
                int bestV = 0;
                foreach (var kv in mounted)
                    if (kv.Key != ManaType.Gray && kv.Value > bestV) { bestV = kv.Value; best = kv.Key; }
                if (bestV <= 0) break;
                mounted[best] = bestV - 1;
            }
        }

        // 抉择价差溢价已废（2026-09-21）——抉择分支计槽 + 地牌按所选模式产元素承担防套利。
        // 底盘退费（ApplyChassisRefund）/瞬间法术豁免（IsInstantSpell）/SurplusToSpeed 计价豁免
        // 已删（2026-10-10 可支配底盘定案：剩余不再「作废」或「整体转速度」，改按卡型分配，
        // 详见类头②与 GameActions.GetCardEffectDefinitions 的 SpeedBonus 加和）。
    }
}
