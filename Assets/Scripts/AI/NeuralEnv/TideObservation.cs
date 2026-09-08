using System;
using CardCore;
using CardCore.Attribute;

namespace CardCore.AI.NeuralEnv
{
    /// <summary>
    /// 状态快照：把 GameCore 当前局面编码成「当前回合玩家」视角的固定维度特征向量，
    /// 契约对齐 ygo-agent 的 cards_/global_ 表格（(实体数 × 特征维)，不是棋盘网格）。
    /// 纯读状态，不改任何核心。
    ///
    /// 槽位分配（每玩家 40 槽，共 80 槽）：
    ///   [0..17]  战场 Battlefield (18)
    ///   [18..26] 元素池 ElementPool (9)
    ///   [27..33] 手牌 Hand (7)
    ///   [34..39] 墓地 Graveyard (6)
    /// 牌库 / 放逐 / 发动区 v1 仅以计数入 global_（不逐卡编码）——放逐区可后续扩展槽位。
    /// </summary>
    public sealed class TideObservation
    {
        // ---- 维度常量（Python 侧只改这些，模型不动）----
        public const int MaxCardsPerPlayer = 40;
        public const int MaxCardsTotal = MaxCardsPerPlayer * 2;
        public const int NCard = 20;
        public const int NGlobal = 32;
        public const int NAction = 6; // 动作特征槽：valid/type/sourceIndex/targetIndex/modeIndex/actionCost

        public float[] Cards = new float[MaxCardsTotal * NCard];
        public float[] Globals = new float[NGlobal];

        private Player _me;

        /// <summary>从核心构建快照（me = 当前回合玩家）。</summary>
        public static TideObservation Build(GameCore core)
        {
            var obs = new TideObservation();
            if (core == null || core.TurnEngine == null || core.TurnEngine.TurnPlayer == null)
                return obs;
            obs._me = core.TurnEngine.TurnPlayer;
            var opp = obs._me.Opponent;
            if (opp == null) return obs;

            obs.WritePlayer(core, obs._me, 0);
            obs.WritePlayer(core, opp, MaxCardsPerPlayer);
            obs.WriteGlobals(core, obs._me, opp);
            return obs;
        }

        /// <summary>某卡在 cards_ 中的槽位下标（controller 相对 me；未编码区返回 -1）。</summary>
        public static int CardIndex(Player me, Player controller, Zone zone, int indexInZone)
        {
            int off = ZoneOffset(zone);
            if (off < 0 || indexInZone < 0) return -1;
            int baseSlot = ReferenceEquals(controller, me) ? 0 : MaxCardsPerPlayer;
            return baseSlot + off + indexInZone;
        }

        /// <summary>区域 → 槽位基址（未编码区返回 -1）。</summary>
        public static int ZoneOffset(Zone zone) => zone switch
        {
            Zone.Battlefield => 0,
            Zone.ElementPool => 18,
            Zone.Hand => 27,
            Zone.Graveyard => 34,
            _ => -1,
        };

        // =====================================================

        private void WritePlayer(GameCore core, Player p, int baseSlot)
        {
            var zm = core.ZoneManager;
            var layer = core.LayerEngine;
            WriteZone(zm, layer, p, baseSlot + 0, Zone.Battlefield, 18);
            WriteZone(zm, layer, p, baseSlot + 18, Zone.ElementPool, 9);
            WriteZone(zm, layer, p, baseSlot + 27, Zone.Hand, 7);
            WriteZone(zm, layer, p, baseSlot + 34, Zone.Graveyard, 6);
        }

        private void WriteZone(ZoneManager zm, LayerEngine layer, Player p, int baseSlot, Zone zone, int cap)
        {
            var cards = zm.GetCards(p, zone);
            int n = Math.Min(cards.Count, cap);
            for (int i = 0; i < cap; i++)
            {
                if (i < n)
                    WriteCard(layer, cards[i], baseSlot + i);
                // 空槽保持全 0（valid=0）
            }
        }

        private void WriteCard(LayerEngine layer, Card c, int slot)
        {
            int b = slot * NCard;
            int power = layer.CalculatePower(c);
            int life = layer.CalculateToughness(c);
            float cost = 0f;
            foreach (var v in GameActions.GetCardCost(c).Values) cost += v;

            Cards[b + 0] = 1f; // valid
            Cards[b + 1] = ReferenceEquals(c.GetController(), _me) ? 0f : 1f; // controller
            Cards[b + 2] = c is IHasSupertype s ? (int)s.Supertype : 0f;       // supertype
            Cards[b + 3] = Clamp(power, -99f, 999f);                          // 结算力量
            Cards[b + 4] = Clamp(life, -99f, 999f);                           // 结算防御/生命
            Cards[b + 5] = Clamp(cost, 0f, 99f);                              // 声明费用和
            Cards[b + 6] = c.IsTapped() ? 1f : 0f;
            Cards[b + 7] = (!c.IsTapped() && power > 0) ? 1f : 0f;           // 可攻击粗判
            Cards[b + 8] = KeywordRules.HasRushSickness(c) ? 1f : 0f;        // 召唤失调
            Cards[b + 9] = c.GetCounterCount(CounterRules.PoisonCounter);
            Cards[b + 10] = c.GetCounterCount(CounterRules.SilenceCounter) > 0 ? 1f : 0f;
            Cards[b + 11] = c.GetCounterCount(CounterRules.CostUpCounter)
                          - c.GetCounterCount(CounterRules.CostDownCounter); // 费用层净量
            Cards[b + 12] = c.HasKeyword(KeywordRules.Taunt) ? 1f : 0f;
            Cards[b + 13] = c.HasKeyword(KeywordRules.Stealth) ? 1f : 0f;
            Cards[b + 14] = c.HasKeyword(DeathRules.DivineProtection) ? 1f : 0f;
            // [15..19] 预留
        }

        private void WriteGlobals(GameCore core, Player me, Player opp)
        {
            var g = Globals;
            var zm = core.ZoneManager;
            var ep = core.ElementPool;

            g[0] = me.GetLife();
            g[1] = opp.GetLife();
            g[2] = core.TurnEngine.TurnNumber;
            g[3] = (int)(core.TurnEngine.CurrentPhase?.Phase ?? PhaseType.Standby); // 0/1/2
            g[4] = 1f; // is_my_turn（快照恒为当前回合玩家视角）
            g[5] = zm.GetCards(me, Zone.Hand).Count;
            g[6] = zm.GetCards(opp, Zone.Hand).Count;
            g[7] = zm.GetCards(me, Zone.Battlefield).Count;
            g[8] = zm.GetCards(opp, Zone.Battlefield).Count;
            g[9] = ep.GetPooledCards(me).Count;
            g[10] = ep.GetPooledCards(opp).Count;
            g[11] = zm.GetCards(me, Zone.Graveyard).Count;
            g[12] = zm.GetCards(opp, Zone.Graveyard).Count;
            g[13] = zm.GetCards(me, Zone.Deck).Count;
            g[14] = zm.GetCards(opp, Zone.Deck).Count;
            for (int i = 0; i < 6; i++) g[15 + i] = ep.GetPool(me).AvailableMana[(ManaType)i];
            for (int i = 0; i < 6; i++) g[21 + i] = ep.GetPool(opp).AvailableMana[(ManaType)i];
            g[27] = core.StackEngine.IsEmpty ? 0f : 1f; // 栈占用（二值，v1 不逐层）
            g[28] = FieldValueReward.TotalValue(core, me);  // 己方全资源价值（战场 + 生命 + 法力 + 地牌 + 手牌）
            g[29] = FieldValueReward.TotalValue(core, opp); // 对方全资源价值
            g[30] = ep.GetLandCap(me);
            g[31] = ep.GetLandCap(opp);
        }

        private static float Clamp(float v, float min, float max)
            => v < min ? min : (v > max ? max : v);
    }
}
