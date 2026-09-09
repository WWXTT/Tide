using System;
using System.Collections.Generic;
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
    ///   [27..38] 手牌 Hand (12——回合中可超 7：上限回合结束才执行，CardIndex 容量护栏防串区)
    ///   [39]     墓地 Graveyard (1——墓地出牌已移出 RL 动作空间，逐卡编码降为占位；计数在 global_)
    /// 牌库 / 放逐 / 发动区 v1 仅以计数入 global_（不逐卡编码）——放逐区可后续扩展槽位。
    /// 对方手牌是隐藏信息：只写 valid+controller（不开图），张数在 global_ g[6]。
    /// </summary>
    public sealed class TideObservation
    {
        // ---- 维度常量（Python 侧只改这些，模型不动）----
        public const int MaxCardsPerPlayer = 40;
        public const int MaxCardsTotal = MaxCardsPerPlayer * 2;
        public const int NCard = 24;
        public const int AtomIdSlots = 6;  // 卡特征 [15..20] 原子内容哈希槽（跨效果按执行序展平，超出截断）
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

            obs.WritePlayer(core, obs._me, 0, concealHand: false);
            obs.WritePlayer(core, opp, MaxCardsPerPlayer, concealHand: true);
            obs.WriteGlobals(core, obs._me, opp);
            return obs;
        }

        /// <summary>某卡在 cards_ 中的槽位下标（controller 相对 me；未编码区/超出区域容量返回 -1）。</summary>
        public static int CardIndex(Player me, Player controller, Zone zone, int indexInZone)
        {
            if (!ZoneLayout(zone, out int off, out int cap) || indexInZone < 0 || indexInZone >= cap) return -1;
            int baseSlot = ReferenceEquals(controller, me) ? 0 : MaxCardsPerPlayer;
            return baseSlot + off + indexInZone;
        }

        /// <summary>区域 → (槽位基址, 容量)（未编码区返回 false）。容量是防串护栏：
        /// 手牌第 13+ 张（回合中超上限）返回 -1——动作仍可执行，只是来源槽不可编码，
        /// 不再串进相邻区（旧布局第 8 张手牌会别名到墓地槽 0）。</summary>
        public static bool ZoneLayout(Zone zone, out int offset, out int cap)
        {
            switch (zone)
            {
                case Zone.Battlefield: offset = 0; cap = 18; return true;
                case Zone.ElementPool: offset = 18; cap = 9; return true;
                case Zone.Hand: offset = 27; cap = 12; return true;
                case Zone.Graveyard: offset = 39; cap = 1; return true;
                default: offset = -1; cap = 0; return false;
            }
        }

        // =====================================================

        private void WritePlayer(GameCore core, Player p, int baseSlot, bool concealHand)
        {
            var zm = core.ZoneManager;
            var layer = core.LayerEngine;
            WriteZone(zm, layer, p, baseSlot + 0, Zone.Battlefield, 18);
            WriteZone(zm, layer, p, baseSlot + 18, Zone.ElementPool, 9);
            WriteZone(zm, layer, p, baseSlot + 27, Zone.Hand, 12, concealHand);
            WriteZone(zm, layer, p, baseSlot + 39, Zone.Graveyard, 1);
        }

        private void WriteZone(ZoneManager zm, LayerEngine layer, Player p, int baseSlot, Zone zone, int cap, bool conceal = false)
        {
            var cards = zm.GetCards(p, zone);
            int n = Math.Min(cards.Count, cap);
            for (int i = 0; i < cap; i++)
            {
                if (i < n)
                {
                    if (conceal) WriteConcealedCard(baseSlot + i);
                    else WriteCard(layer, cards[i], baseSlot + i);
                }
                // 空槽保持全 0（valid=0）
            }
        }

        /// <summary>隐藏信息区（对方手牌）：只写存在性与归属，其余特征（含身份槽）全 0——模型不开图（张数在 global_）。</summary>
        private void WriteConcealedCard(int slot)
        {
            int b = slot * NCard;
            Cards[b + 0] = 1f; // valid（这里有一张未知卡）
            Cards[b + 1] = 1f; // controller = 对方（该半区恒 opp）
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
            // 内容身份（原子级拆散，CardIdentityService 与费用同管线推导）：
            // [15..20] 按执行序展平的原子哈希下标（造成伤害a4b823fc+参数 这类单元，同原子跨卡
            // 共享 embedding 行），[21] 组合结构哈希下标（时点/条件/代价/Steps 骨架=「怎么组合」），
            // [22] 关键词/tag/光环组合哈希下标，[23] 预留。0 = 无该成分 / token / 未登记。
            // 属性不在此（第 3/4/5 维实时特征已覆盖，不重复）。
            var data = c is CardWrapper wrapper ? wrapper.GetData() : null;
            var atomHashes = data?.AtomContentHashes;
            for (int e = 0; e < AtomIdSlots && e < (atomHashes?.Length ?? 0); e++)
                Cards[b + 15 + e] = TideCardIndex.IndexOf(atomHashes[e]);
            Cards[b + 21] = TideCardIndex.IndexOf(data != null ? data.StructureContentHash : 0UL);
            Cards[b + 22] = TideCardIndex.IndexOf(data != null ? data.CompositionContentHash : 0UL);
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

    /// <summary>
    /// 内容身份登记簿：ulong 内容哈希（CardIdentityService：原子哈希 / 组合结构哈希 /
    /// 关键词+tag+光环组合哈希，均已混入原子表指纹）→ 池内小整数下标，供 TideObservation
    /// [15..22] 输出、Python 侧 nn.Embed 查表。拆散到原子级：同原子（如 造成伤害4）跨卡共享
    /// 同一行——相近效果在原子层重叠，语义直接迁移。
    /// 0 = 无该成分 / token / 未登记；容量 256 与 Python N_CARD_POOL 对齐。
    /// 键是内容哈希（内容寻址，别名 ID 不参与）。
    /// </summary>
    public static class TideCardIndex
    {
        private const int Capacity = 256;
        private static readonly Dictionary<ulong, int> _map = new Dictionary<ulong, int>();

        /// <summary>登记卡池（HandleReset / 自测菜单调用）：收集全部内容哈希 → 去重排序 → 顺序分配 1..N。
        /// 下标 = 哈希在池内去重集合中的排名，与卡在文件中的位置无关——「同效果不同属性」的新卡
        /// 不引入新哈希，任意位置插入/追加都不动已有下标（训练完直接可用，零重训）。
        /// 引入全新组合的哈希会使其排序后继 +1（新组合本就未经训练）；要彻底免重排需持久化
        /// 哈希清单（未实现）。容量截断丢弃的是排序最大的哈希。</summary>
        public static void Register(IEnumerable<CardData> pool)
        {
            if (pool == null) return;
            var hashes = new SortedSet<ulong>();
            foreach (var card in pool)
            {
                if (card == null) continue;
                if (card.AtomContentHashes != null)
                    foreach (var h in card.AtomContentHashes)
                        if (h != 0UL) hashes.Add(h);
                if (card.StructureContentHash != 0UL) hashes.Add(card.StructureContentHash);
                if (card.CompositionContentHash != 0UL) hashes.Add(card.CompositionContentHash);
            }
            foreach (var h in hashes)
            {
                if (_map.Count >= Capacity - 1) return;
                _map[h] = _map.Count + 1;
            }
        }

        /// <summary>内容哈希 → 下标（未登记 = 0）。</summary>
        public static int IndexOf(ulong hash)
            => hash != 0UL && _map.TryGetValue(hash, out int idx) ? idx : 0;
    }
}
