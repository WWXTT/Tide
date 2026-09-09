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
    /// 势能塑形（potential-based reward shaping）理论上不改变最优策略——终端 ±1 始终主导；
    /// λ 取小值（0.01~0.05）让终局信号为主导，塑形为引导。
    ///
    /// 【2026-09-08 重写理由】：
    /// v1 只算战场卡静态 DerivedTotal，不含动态指示物/状态，也不含资源（生命/法力/地牌/手牌）；
    /// 会把「5 法力→5 场面」误判成净 +5，且忽略指示物增益。v2 纳入全部资源与动态状态。
    /// </summary>
    public static class FieldValueReward
    {
        // ---- 权重参数（可调，用户可通过配置覆盖）----
        /// <summary>生命点权重（建议 0.1~0.2：1 生命 ≈ 0.1~0.2 费）</summary>
        public static float LifeWeight = 0.15f;
        /// <summary>bank 法力权重（建议 0.3~0.5：1 法力 ≈ 0.3~0.5 费；已支付待用资源）</summary>
        public static float ManaWeight = 0.4f;
        /// <summary>地牌剩余指示物权重（建议 0.5~0.8：1 指示物 ≈ 0.5~0.8 费；未来产出价值折现）</summary>
        public static float LandTokenWeight = 0.6f;
        /// <summary>手牌权重（建议 0.5~0.7：1 手牌 ≈ 0.5~0.7 费；未打出潜在价值折现）</summary>
        public static float HandWeight = 0.6f;
        /// <summary>横置状态折扣（建议 0.7~0.9：横置卡本回合不可攻击/发动，价值打折）</summary>
        public static float TappedDiscount = 0.8f;
        /// <summary>召唤失调折扣（建议 0.6~0.8：刚入场、本回合不可动）</summary>
        public static float RushSicknessDiscount = 0.7f;
        /// <summary>属性指示物权重（每 +1/+1 或 −1/-1 ≈ 0.5 费）</summary>
        public static float StatCounterWeight = 0.5f;

        // ======================================== 公开 API ========================================

        /// <summary>己方净势能（me − opponent）。</summary>
        public static float ComputePotential(GameCore core, Player me)
        {
            if (core == null || me == null) return 0f;
            float myTotal = TotalValue(core, me);
            float oppTotal = TotalValue(core, me.Opponent);
            return myTotal - oppTotal;
        }

        /// <summary>单步塑形奖励 λ·(Φ' − Φ)。</summary>
        public static float ShapingDelta(float potentialBefore, float potentialAfter, float lambda)
            => lambda * (potentialAfter - potentialBefore);

        /// <summary>某玩家的全部价值（战场 + 资源），供 TideObservation 使用。</summary>
        public static float TotalValue(GameCore core, Player player)
        {
            if (core == null || player == null) return 0f;
            return BattlefieldValue(core, player) + ResourceValue(core, player);
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

            // 1) 基础价（静态 DerivedTotal 或声明费用）
            float baseValue = CardBaseValue(card);

            // 2) 属性指示物增益（+1/+1, PowerUp, LifeUp）与减益（-1/-1, PowerDown, LifeDown）
            float counterDelta = 0f;
            counterDelta += card.GetCounterCount(CounterRules.PowerUpCounter) * StatCounterWeight;
            counterDelta += card.GetCounterCount(CounterRules.LifeUpCounter) * StatCounterWeight;
            counterDelta -= card.GetCounterCount(CounterRules.PowerDownCounter) * StatCounterWeight;
            counterDelta -= card.GetCounterCount(CounterRules.LifeDownCounter) * StatCounterWeight;
            // +1/+1 / -1/-1（历史 id，已对消后的净值）
            counterDelta += card.GetCounterCount(CounterRules.PlusOneCounter) * StatCounterWeight * 2; // 攻+血双增
            counterDelta -= card.GetCounterCount(CounterRules.MinusOneCounter) * StatCounterWeight * 2;

            // 3) 负面指示物（剧毒、毒素、沉默）
            if (card.GetCounterCount(CounterRules.PoisonCounter) > 0) counterDelta -= baseValue * 0.9f; // 剧毒=下回合末死亡，价值接近归零
            counterDelta -= card.GetCounterCount(CounterRules.ToxinCounter) * 0.3f; // 毒素每层受 1 伤，持续 3 回合
            if (card.GetCounterCount(CounterRules.SilenceCounter) > 0) counterDelta -= baseValue * 0.3f; // 沉默=不可发动效果，打折 30%
            if (card.GetCounterCount(CounterRules.VulnerableCounter) > 0) counterDelta -= 0.5f; // 易损=受伤 +1

            // 4) 状态折扣（横置、召唤失调）
            float stateDiscount = 1f;
            if (card.IsTapped()) stateDiscount *= TappedDiscount;
            if (KeywordRules.HasRushSickness(card)) stateDiscount *= RushSicknessDiscount;

            float finalValue = (baseValue + counterDelta) * stateDiscount;
            return Math.Max(0f, finalValue); // 保底 0（负面极端情况不拖累总势能）
        }

        /// <summary>单卡基础价（静态 DerivedTotal 或声明费用）。</summary>
        private static float CardBaseValue(Card card)
        {
            if (card == null) return 0f;
            if (card is CardWrapper wrapper)
            {
                var data = wrapper.GetData();
                if (data != null)
                    return CardCostService.Derive(data).DerivedTotal;
            }
            // 裸 Card（token/复制）：回退声明费用和
            float sum = 0f;
            foreach (var v in GameActions.GetCardCost(card).Values) sum += v;
            return sum;
        }

        // ======================================== 资源价值 ========================================

        /// <summary>某玩家的资源价值（生命 + 法力 bank + 地牌剩余指示物 + 手牌）。</summary>
        public static float ResourceValue(GameCore core, Player player)
        {
            if (core == null || player == null) return 0f;

            float value = 0f;

            // 1) 生命
            value += player.GetLife() * LifeWeight;

            // 2) bank 法力（已积攒待用）
            var pool = core.ElementPool.GetPool(player);
            if (pool != null)
            {
                foreach (var mana in pool.AvailableMana.Values)
                    value += mana * ManaWeight;
            }

            // 3) 地牌剩余指示物（未来产出价值）
            var pooledCards = core.ElementPool.GetPooledCards(player);
            if (pooledCards != null)
            {
                foreach (var pc in pooledCards)
                {
                    if (pc != null && pc.Tokens != null)
                    {
                        foreach (var tokenCount in pc.Tokens.Values)
                            value += tokenCount * LandTokenWeight;
                    }
                }
            }

            // 4) 手牌（潜在价值）
            var hand = core.ZoneManager.GetCards(player, Zone.Hand);
            if (hand != null)
            {
                foreach (var c in hand)
                    value += CardBaseValue(c) * HandWeight;
            }

            return value;
        }
    }
}
