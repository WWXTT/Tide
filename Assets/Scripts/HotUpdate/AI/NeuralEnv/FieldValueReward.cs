using System;
using System.Linq;
using CardCore;
using CardCore.Attribute;

namespace CardCore.AI.NeuralEnv
{
    /// <summary>
    /// 全资源场面价值势能 Φ(s) 与奖励塑形（2026-09-08 完整版）。
    ///
    ///   Φ(s) = V_battlefield(me) + V_resources(me) − [V_battlefield(opp) + V_resources(opp)]
    ///
    ///   V_battlefield = Σ 战场卡动态价值（基础价 + 指示物增益 − 减益 + 横置/失调折扣）
    ///   V_resources   = 生命×生命权重 + bank法力总值×法力权重 + 地牌剩余产出×地牌权重 + 手牌价值总和×手牌权重
    ///
    ///   每步塑形奖励 = λ·(Φ(s') − Φ(s))
    ///
    /// 势能塑形（potential-based reward shaping）理论上不改变最优策略——前提是塑形累计量 ≪ 终局 ±1。
    ///
    /// 【2026-09-22 训练定案】：
    ///  - λ 0.05 → 0.005：λ=0.05 时双方各自整局塑形累计 ≈ +3.1，是终局 ±1 的 3 倍，
    ///    且 Φ 含手牌（HandWeight 0.6）让「每回合摸牌」成为白拿的正奖励流——自对弈双方
    ///    联合收敛到拖局刷塑形（局长 90→267，对脚本胜率 0.35→0.1）。
    ///  - 奖励势能剔除手牌：ComputePotential(includeHand:false)，只用于驱动器塑形；
    ///    TideObservation 的 g[28]/g[29] 仍走 includeHand=true（默认），观测口径不变。
    ///
    /// 【2026-09-08 重写理由】：
    /// v1 只算战场卡静态 DerivedTotal，不含动态指示物/状态，也不含资源（生命/法力/地牌/手牌）；
    /// 会把「5 法力→5 场面」误判成净 +5，且忽略指示物增益。v2 纳入全部资源与动态状态。
    /// </summary>
    public static class FieldValueReward
    {
        // ---- 权重参数（2026-11-23 用户定案：塑形口径 v3 = 定价体系逐项映射）----
        // 定案口径（用户原话逐条映射，单位统一=费）：
        //  ① 手牌 = 卡面标价（3 费卡在手 = 3）→ 抽牌/回收自然是加分项；
        //  ② 地牌 = 1/块（比标价低）= 把牌变永久产出的"投资折价"，之后每横置 +1（产出元素 1 费）；
        //  ③ 场面生物按属性计价：1/1 = 1 费（=(攻+血)/2）；给自己加甲 1 层 = 1 费；
        //  ④ 攻击 = 攻击力转伤害，每 1 点伤害 = 1 费（体现为对手生命 −1 → Φ +1），属性转伤害完全净增；
        //  ⑤ 生命 = 1/点（自己 +1、对手 −1 对称计）。
        /// <summary>生命点权重（2026-11-23：0.15 → 1.0 —— 1 生命 = 1 费，与伤害=费用同轴）</summary>
        public static float LifeWeight = 1.0f;
        /// <summary>临时法力权重（产出元素 = 1 费）</summary>
        public static float ManaWeight = 1.0f;
        /// <summary>万用元素（Any）权重（= 1 费，不溢价）</summary>
        public static float WildManaWeight = 1.0f;
        /// <summary>地牌权重（2026-11-23 用户定案：1.0 → **3.0** = 复利资本化：一块地 ≈ 3 费，
        /// 相当于"未来 3 回合的产出"折现。横置不打折 → 横置仍净 +1（地不变 + 元素 1.0）。
        /// 由此：沃土（实际费 9，铺 3 地）= −9 + 3×3 = 0 打平，第 4 块起净赚；
        /// 归根（8 费，回 3 张手牌）= −8 + 3×3 = +1 小赚，4 张 +4）</summary>
        public static float LandTokenWeight = 3.0f;
        /// <summary>手牌权重（2026-11-23：改为「卡面标价 × 1.0」语义——3 费卡在手 = 3）</summary>
        public static float HandWeight = 1.0f;
        /// <summary>护甲权重（2026-11-23 新增：1 层护甲 = 1 费，此前护甲完全不在 Φ 里）</summary>
        public static float ArmorWeight = 1.0f;
        /// <summary>场面生物属性权重：价值 = (攻 + 血) × BioStatWeight；1/1 → 1（2026-11-23 新增，
        /// 与"场面属性 1-1 算 1 费"对齐；非生物仍按标价 CardBaseValue 计）</summary>
        public static float BioStatWeight = 0.5f;
        /// <summary>横置状态折扣（建议 0.7~0.9：横置卡本回合不可攻击/发动，价值打折）</summary>
        public static float TappedDiscount = 0.8f;
        /// <summary>召唤失调折扣（建议 0.6~0.8：刚入场、本回合不可动）</summary>
        public static float RushSicknessDiscount = 0.7f;
        /// <summary>属性指示物权重（每 +1/+1 或 −1/-1 ≈ 0.5 费）</summary>
        public static float StatCounterWeight = 0.5f;

        // ======================================== 公开 API ========================================

        /// <summary>己方净势能（me − opponent）。
        /// includeHand=false 为 2026-09-22 奖励口径：塑形势能剔除手牌（摸牌不再是白拿的奖励流）；
        /// 默认 true 保持 TideObservation 观测口径（g[28]/g[29]）不变。</summary>
        public static float ComputePotential(GameCore core, Player me, bool includeHand = true)
        {
            if (core == null || me == null) return 0f;
            float myTotal = TotalValue(core, me, includeHand);
            float oppTotal = TotalValue(core, me.Opponent, includeHand);
            return myTotal - oppTotal;
        }

        /// <summary>单步塑形奖励 λ·(Φ' − Φ)。</summary>
        public static float ShapingDelta(float potentialBefore, float potentialAfter, float lambda)
            => lambda * (potentialAfter - potentialBefore);

        /// <summary>某玩家的全部价值（战场 + 资源），供 TideObservation 使用（includeHand 语义见 ComputePotential）。</summary>
        public static float TotalValue(GameCore core, Player player, bool includeHand = true)
        {
            if (core == null || player == null) return 0f;
            return BattlefieldValue(core, player) + ResourceValue(core, player, includeHand);
        }

        // ======================================== 战场价值（动态） ========================================

        /// <summary>战场上所有卡的动态价值总和。</summary>
        public static float BattlefieldValue(GameCore core, Player player)
        {
            if (core == null || player == null) return 0f;
            var field = core.ZoneManager.GetCards(player, Zone.Battlefield);
            if (field == null) return 0f;
            float sum = 0f;
            foreach (var c in field) sum += CardDynamicValue(core, c);
            return sum;
        }

        /// <summary>单卡动态价值（基础价 + 指示物 + 状态折扣）。</summary>
        public static float CardDynamicValue(GameCore core, Card card)
        {
            if (card == null) return 0f;

            // 1) 基础价（2026-11-23 v3：生物按 **属性** 计价——(攻+血)×BioStatWeight，
            //    1/1 = 1 费，与"场面属性 1-1 算 1 费"对齐；非生物仍按标价 DerivedTotal）
            float baseValue = CardBaseValue(card);
            int pw = card.GetPower();
            int lf = card.GetLife();
            bool isCreature = (card is CardWrapper bw && bw.GetData()?.Supertype == Cardtype.Creature)
                              || (!(card is CardWrapper) && pw + lf > 0); // 裸 Card（token/复制）按属性
            if (isCreature && pw + lf > 0) baseValue = (pw + lf) * BioStatWeight;

            // 2) 属性指示物增益（+1/+1, PowerUp, LifeUp）与减益（-1/-1, PowerDown, LifeDown）
            float counterDelta = 0f;
            counterDelta += card.GetCounterCount(CounterRules.PowerUpCounter) * StatCounterWeight;
            counterDelta += card.GetCounterCount(CounterRules.LifeUpCounter) * StatCounterWeight;
            counterDelta -= card.GetCounterCount(CounterRules.PowerDownCounter) * StatCounterWeight;
            counterDelta -= card.GetCounterCount(CounterRules.LifeDownCounter) * StatCounterWeight;
            // +1/+1 / -1/-1（历史 id，已对消后的净值）
            counterDelta += card.GetCounterCount(CounterRules.PlusOneCounter) * StatCounterWeight * 2; // 攻+血双增
            counterDelta -= card.GetCounterCount(CounterRules.MinusOneCounter) * StatCounterWeight * 2;

            // 3) 负面指示物（毒素、沉默；剧毒已转关键词 Venom——关键词面由 Keyword 特征位覆盖）
            counterDelta -= card.GetCounterCount(CounterRules.ToxinCounter) * 0.3f; // 毒素：每回合末受=层数的伤害后减半（留存层持续伤害，权重高于旧一次性口径）
            if (card.GetCounterCount(CounterRules.SilenceCounter) > 0) counterDelta -= baseValue * 0.3f; // 沉默=不可发动效果，打折 30%
            if (card.GetCounterCount(CounterRules.VulnerableCounter) > 0) counterDelta -= 0.5f; // 易损=受伤 +1

            // 4) 状态折扣（横置、召唤失调）
            float stateDiscount = 1f;
            if (card.IsTapped()) stateDiscount *= TappedDiscount;
            if (KeywordRules.HasRushSickness(card)) stateDiscount *= RushSicknessDiscount;

            float finalValue = (baseValue + counterDelta) * stateDiscount;
            return Math.Max(0f, finalValue); // 保底 0（负面极端情况不拖累总势能）
        }

        /// <summary>单卡基础价 = **实际费用** = 卡面费 `DerivedTier` + 代价价 `PayloadApplied`
        /// （2026-11-23 用户定案："把代价视为费用，只是支付方式不是横置地牌"）。
        /// 例：沃土 6 卡面 + 3 代价 = **9**。
        /// 注意：代价价只在这里计一次；代价的**实物支付**（生物进墓等）不再额外扣，
        /// 因为那一份价值已经由本价代表（旧版先按 DerivedTotal 计价、又在盘面扣一次 = 收两遍，
        /// 血偿因此被算成 −2 永不出手，2026-11-23 已修）。</summary>
        private static float CardBaseValue(Card card)
        {
            if (card == null) return 0f;
            if (card is CardWrapper wrapper)
            {
                var data = wrapper.GetData();
                if (data != null)
                {
                    var cost = CardCostService.Derive(data);
                    return cost.DerivedTier + cost.PayloadApplied; // 卡面费 + 代价价 = 实际费用
                }
            }
            // 裸 Card（token/复制）：回退声明费用和
            return GameActions.GetCardCost(card).Total;
        }

        // ======================================== 资源价值 ========================================

        /// <summary>某玩家的资源价值（生命 + 法力 bank + 地牌剩余指示物 + 手牌）。</summary>
        public static float ResourceValue(GameCore core, Player player, bool includeHand = true)
        {
            if (core == null || player == null) return 0f;

            float value = 0f;

            // 1) 生命（2026-11-23 v3：1 生命 = 1 费，"攻击把攻击力转成伤害"就是净增）
            value += player.GetLife() * LifeWeight;

            // 1b) 护甲（2026-11-23 v3 新增：1 层 = 1 费。此前护甲完全不在 Φ 里，
            //     "给自己加甲"在塑形上是 0 收益——这是绿/蓝都不愿加甲的根因之一）
            value += player.GetCounterCount(CardCore.Attribute.KeywordRules.ArmorCounter) * ArmorWeight;

            // 2) 临时钱包（2026-10-11 现取现用：bank 退役；万能货币 Any 按 WildManaWeight 估值
            //    ——白/黑席位已随 2026-10-28 P2 收缩删除）
            var pool = core.ElementPool.GetPool(player);
            if (pool != null)
            {
                foreach (var kv in pool.TempElements)
                    value += kv.Value * (kv.Key == ManaType.Any
                        ? WildManaWeight : ManaWeight);
            }

            // 3) 地牌（2026-11-23 v2：横置不打折——地价恒定，横置的收益体现在「产出元素」那一项上。
            //    于是"横置一块地"净 Φ = 0（地）+ 1.0（元素）= +1，成长/复用链条才有正梯度）
            var pooledCards = core.ElementPool.GetPooledCards(player);
            if (pooledCards != null)
            {
                foreach (var pc in pooledCards)
                {
                    if (pc == null) continue;
                    value += LandTokenWeight;
                }
            }

            // 4) 手牌（2026-11-23 v2：按定价体系「每张 2.0」计价，不再按卡面价折算。
            //    奖励势能口径仍由 includeHand 控制——驱动器端已改为 true（见 ComputePotential 调用点），
            //    使"抽牌/回收"的价值与费用体系一致）
            if (includeHand)
            {
                var hand = core.ZoneManager.GetCards(player, Zone.Hand);
                if (hand != null)
                {
                    foreach (var c in hand)
                        if (c != null) value += HandWeight; // 每张固定 2.0
                }
            }

            return value;
        }
    }
}
