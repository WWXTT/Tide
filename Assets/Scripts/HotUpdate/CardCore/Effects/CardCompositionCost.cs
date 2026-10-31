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
 /// 当前规则（2026-10-15 卡型默认分配定案 + 2026-10-15 深夜三类卡改版；速度口径 2026-10-31 P12 回归）：
 /// 效果组合界面不再能设置发动速度；三个免费额度（底盘 = 显性三万能色：入组 1 + 抽牌 2）按卡型默认分配：
 /// · 法术 = 速度 3−槽数（2026-10-31 P12 回归，P6c 5−N 退役）：1 槽=速2、2 槽=速1、3 槽=速0、
 ///   4 槽=速0（槽含代价行与充能效果——充能照常计 1 槽；
 ///   速度不收钱、卡费一分不变，只变速度门），动态计算不落字段（KindSpeedBonusOf 单源；
 ///   SpeedBonus 字段退役）。
 ///   法术只挂 1~3 个主动效果（发动时弹窗抉择其一）：出牌费不含效果锚价，
 ///   发动=现付所选效果锚价（cast 结算步并入账单），无额外费用。
 ///   效果发动速度=卡的发动速度（法术专享通道）；从手牌打出即按此速度过门（GetCardCastSpeed）。
 /// · 结界禁主动效果（只挂自动/光环）：速度通道退役（EnchantSpeedBonusOf 删除），
 ///   免费点全投耐久（DurabilityBonusOf=剩余）；耐久=受击/被动触发次数池（主动−1 通道退役）。
 /// · 生物 = 攻击 1 + 守卫 1 + 每个效果槽 1——**结构项>3 的差额入灰**（2026-10-22 最终定案）：
 ///   纯白板（攻+守=2 项）盈余 1 点 → +1/+1 属性；攻/守 opt-out 各释放 1 点（NoGuard 红套 = 攻 1 项，
 ///   盈余 2 点 → +2/+2 属性）；3 效果+攻+守=5 项 → 2 灰槽位费。
 ///   效果槽与攻/守同占 3 点底盘，不再“效果槽不占额度、每槽单独加价”。
 ///   生物禁挂连接光环（2026-10-15 改版：只留主动/自动/规则光环/本体关键词）。
 /// · 生物和结界从手牌使用的默认速度是 0（仅己方主要阶段，目前无效果能改）。
 /// ①底盘结构项（占额度）：生物 = 攻/守在场各占 1 + 效果槽 ×CountEffectSlots；
 ///   非生物 = 效果槽 ×CountEffectSlots（抉择分支计槽 2026-09-21 维持；主动照常计槽；代价栏计 1 槽）。
 /// ②免费点 N = 3 − 结构项（≥0），按卡型固定去向：法术 → 提速（N=槽数余量，槽位从速度扣）；
 ///   结界 → 全入耐久（EquipRules 落位，免卡价）；
 ///   生物 → 全额减灰桶 S（仅灰，不溢出）；地牌/其余 → 全额抵价（ReduceBuckets 先灰后最高色）。
 /// ③超槽加价：**统一 = 结构项>3 的差额 ×1 入灰**（生物/非生物同式；3 效果+攻+守 = 2 灰）；
 ///   取整后入灰（不吃 d(C)），与抵价不再互斥（生物加价与灰桶抵扣同帧落位）。
 /// ④抵价落位：d(C) 取整后总价逐点扣，先灰后最高费用色（ReduceBuckets）；生物抵扣恒灰桶专属。
 /// ④抵价落位：d(C) 取整后总价逐点扣，先灰后最高费用色（ReduceBuckets）；生物抵扣恒灰桶专属。
    /// ⑤衍生物无底盘（CardCostService.ContentPriceOf/ByColor 口径）：未走「入组/抽牌」通道，
    ///   结构费全额计（攻/守/槽全算）、无免费点可支配——见 CostDerivation SummonToken 分支。
    ///
    /// 应用点：CardCostService.Derive/DeriveModeCosts 统一附加；
    /// 运行时消费口：GameActions（法术提速）/ EffectExecutionEngine（结界发动费抵扣）/
    /// EquipRules·HeroSkillSystem（结界耐久 +N）。
    /// 参数见 ValueSystemRuntimeConfig.CardCompositionConfig（表 Category=CardComposition）。
    /// </summary>
    public static class CardCompositionCost
    {
        /// <summary>结构项数（未乘费率；2026-10-22 最终定案）：**统一 = 攻/守/效果槽各占 1 点**——
        /// 生物 = 攻在 + 守在 + 效果槽（CountEffectSlots，抉择分支计槽；NoAttack/NoGuard 各释放 1 点）；
        /// 非生物 = 效果槽。三点免费底盘被占用后的盈余按卡型分配，结构项>3 的差额入灰加价。</summary>
        public static int ChassisItems(CardData card)
        {
            int slots = CostDerivationService.CountEffectSlots(card);
            if (card != null && card.Supertype == Cardtype.Creature)
                return (card.NoAttack ? 0 : 1) + (card.NoGuard ? 0 : 1) + slots;
            return slots;
        }

        /// <summary>
        /// 超槽加价（恒 ≥0、永不退费，取整后入灰不吃 d(C)）；2026-10-22 最终定案统一为
        /// **结构项 × 费率 − 3 点额度**（生物/非生物同式）：
        /// 生物 3 效果 + 攻 + 守 = 5 项 → 2 灰（用户点名口径）；
        /// 1 效果生物 = 3 项 → 0 灰（与 SurplusOf=0 对冲，净 0）；
        /// 2 效果生物 = 4 项 → 1 灰；纯白板 = 2 项（攻+守）→ 0 灰且盈余 1 点入属性。
        /// 攻/守 = 生物默认自带（NoAttack/NoGuard 玩家自选 opt-out——Power=0 不自动剥夺）。
        /// </summary>
        public static int StructuralSurcharge(CardData card)
        {
            var cfg = ValueSystemConfigManager.Instance.GetOrCreateConfig().CardCompositionConfig;
            int budget = (int)Math.Max(0f, cfg.ChassisBudget);
            int rate = Math.Max(1, (int)Math.Max(0f, cfg.ChassisItemRate));
            return Math.Max(0, ChassisItems(card) * rate - budget);
        }

        /// <summary>
        /// 免费点 N（恒 ≥0）：3 点额度被结构项占用后的盈余，按卡型固定去向（类头②）——
        /// 法术=提速+差额抵价 / 结界=速1+其余入耐久 / 生物=全额减灰桶 S / 地牌=全额抵价。
        /// </summary>
        public static int SurplusOf(CardData card)
        {
            var cfg = ValueSystemConfigManager.Instance.GetOrCreateConfig().CardCompositionConfig;
            int budget = (int)Math.Max(0f, cfg.ChassisBudget);
            int rate = Math.Max(1, (int)Math.Max(0f, cfg.ChassisItemRate));
            return Math.Max(0, budget - ChassisItems(card) * rate);
        }

        /// <summary>计价侧抵价点数（ApplyChassis 消费·ReduceBuckets 先灰后最高色）：结界/生物/法术 = 0
        ///（结界走耐久、生物走灰桶专属抵扣 StatOffsetOf、法术免费点全投提速——槽位从速度扣无抵价余量）；
        /// 地牌/其余 = N。</summary>
        public static int PriceOffsetOf(CardData card)
        {
            if (card == null) return 0;
            int surplus = SurplusOf(card);
            if (surplus <= 0) return 0;
            if (card.Supertype == Cardtype.Enchantment) return 0;
            if (card.Supertype == Cardtype.Creature) return 0;
            if (card.Supertype == Cardtype.Spell) return 0;
            return surplus;
        }

        /// <summary>生物身材抵扣点数（ApplyChassis 灰桶专属消费）：= 免费点 N（属性1 +
        /// 攻/守 opt-out 释放的点）——只减灰桶、不溢出彩色（2026-10-15「不干扰三色费用梯度」
        /// 定案：玩家不要攻/守的费用，全用来减身材费用，不能用作其他）。</summary>
        public static int StatOffsetOf(CardData card)
        {
            if (card == null || card.Supertype != Cardtype.Creature) return 0;
            return SurplusOf(card);
        }

        /// <summary>卡型速度加成单源（2026-10-15 定案：速度归卡型免费额度默认分配；
        /// 2026-10-29 P6c 曾改 5−槽数，**2026-10-31 P12 回归 3−槽数**）：
        /// 法术 = max(0, 3 − 效果槽数)（CountEffectSlots 含代价行与充能效果——充能照常计 1 槽；
        /// 1 槽=速2、2 槽=速1、3 槽=速0、4 槽=速0；速度不收钱，卡费不变只变速度门，动态计算不落字段，
        /// SpeedBonus 字段已退役不再消费）；结界/生物/地牌 = 0（结界主动只收充能（P12）且充能恒 0 速；
        /// 生物/结界从手牌使用恒 0 速）。
        /// 注意：**不动 SurplusOf**——免费点仍管生物属性抵扣 / 结界耐久 / 地牌抵价（改它会连带改价）。
        /// 消费口：GameActions.GetCardEffectDefinitions（效果 BaseSpeed 加和）、
        /// SpeedCalculator.GetCardCastSpeed（法术整卡施放速度门）、AtomText.ActivationPrefix（句首显示）。</summary>
        public static int KindSpeedBonusOf(CardData card)
        {
            if (card == null) return 0;
            if (card.Supertype == Cardtype.Spell)
                return Math.Max(0, 3 - CostDerivationService.CountEffectSlots(card));
            return 0;
        }

        /// <summary>结界入场耐久加成点数（EquipRules/MorphSystem 初始化消费；
        /// 2026-10-15 深夜改版：结界禁主动效果——速度通道退役，免费点全投耐久）。
        /// 2026-10-17 用户定案：结界一律走耐久经济（归零送墓）——不再要求印刷 Durability&gt;0
        /// 才可加（旧「持续型结界无次数池」口径作废——那是更早已废弃的带任务仪式设计）。</summary>
        public static int DurabilityBonusOf(CardData card)
        {
            if (card == null || card.Supertype != Cardtype.Enchantment) return 0;
            return SurplusOf(card);
        }

        /// <summary>结界入场耐久总层数（单源，2026-10-17 定案：结界一律走耐久经济）：
        /// 印刷耐久 + 免费点全投，下限 1（结界必有耐久池）；非结界返回印刷值（装备=纯数据驱动）。
        /// 消费：EquipRules/MorphSystem 入场初始化 + 各显示口（卡面耐久/头顶配色基准）。</summary>
        public static int TotalDurabilityOf(CardData card)
        {
            if (card == null) return 0;
            if (card.Supertype != Cardtype.Enchantment) return card.Durability;
            return Math.Max(1, card.Durability + DurabilityBonusOf(card));
        }

        /// <summary>结构费全额（费率 × 结构项，**无预算抵扣**）——衍生物口径专用
        ///（ContentPriceOf 组成部分：衍生物没走「入组 1 + 抽牌 2」，吃不到 3 点底盘）。
        /// 口径 = 攻/守/槽全算（分配改版不影响衍生物——无底盘即无免费点，三项照旧全额）。</summary>
        public static int StructureFeeFull(CardData card)
        {
            var cfg = ValueSystemConfigManager.Instance.GetOrCreateConfig().CardCompositionConfig;
            int rate = Math.Max(1, (int)Math.Max(0f, cfg.ChassisItemRate));
            bool isCreature = card != null && card.Supertype == Cardtype.Creature;
            int items = (isCreature && !card.NoAttack ? 1 : 0) + (isCreature && !card.NoGuard ? 1 : 0)
                + CostDerivationService.CountEffectSlots(card);
            return items * rate;
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

        /// <summary>代价抵扣逐点扣桶·色锁版（2026-10-31 代价槽改版定案）：只在代价原子**可产出色**
        ///（allowed=引用行 ManaList 非零色）内扣——扣序同 <see cref="ReduceBuckets"/>（先灰后最高费用色，
        /// 并列取枚举序靠前者），每色下限 0、溢出丢弃、不穿底到他色。allowed 空=无抵扣面（分文不扣）。
        /// 返回实际扣减点数：生成面超出可抵色容量即失效——「单个代价至多将这张卡对应色费用完全清零」
        /// 由下限 0+不溢出他色两条共同保证。消费口：CardCostService.ApplyPayloadDeduction 单源。</summary>
        public static int ReduceBucketsLocked(Dictionary<ManaType, int> mounted, int points, HashSet<ManaType> allowed)
        {
            if (allowed == null || allowed.Count == 0) return 0;
            int applied = 0;
            for (int i = 0; i < points; i++)
            {
                if (allowed.Contains(ManaType.Gray)
                    && mounted.TryGetValue(ManaType.Gray, out var g) && g >= 1)
                {
                    mounted[ManaType.Gray] = g - 1;
                    applied++;
                    continue;
                }
                ManaType best = default;
                int bestV = 0;
                foreach (var kv in mounted)
                    if (allowed.Contains(kv.Key) && kv.Key != ManaType.Gray && kv.Value > bestV)
                    {
                        bestV = kv.Value;
                        best = kv.Key;
                    }
                if (bestV <= 0) break;
                mounted[best] = bestV - 1;
                applied++;
            }
            return applied;
        }

        // 抉择价差溢价已废（2026-09-21）——抉择分支计槽 + 地牌按所选模式产元素承担防套利。
        // 底盘退费（ApplyChassisRefund）/瞬间法术豁免（IsInstantSpell）/SurplusToSpeed 计价豁免
        // 已删（2026-10-10 可支配底盘定案）；2026-10-15 卡型默认分配定案再退役：
        // 结界二选一（ActivationFeeOffsetOf 发动费抵扣 / SurplusToDurability 字段——EffectExecutionEngine
        // 抵扣块与合成器开关同批拆除），改固定拆分「速1+其余入耐久」。
    }
}
