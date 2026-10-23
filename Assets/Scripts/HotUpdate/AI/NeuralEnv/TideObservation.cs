using System;
using System.Collections.Generic;
using System.IO;
using CardCore;
using CardCore.Attribute;
using GameBoard;

namespace CardCore.AI.NeuralEnv
{
    /// <summary>
    /// 状态快照：把 GameCore 当前局面编码成「当前决策座次」视角的固定维度特征向量，
    /// 契约对齐 ygo-agent 的 cards_/global_ 表格（(实体数 × 特征维)，不是棋盘网格）。
    /// 纯读状态，不改任何核心。
    ///
    /// v3 契约（2026-10-14，tide_rl/docs/action_space_v3_contract.md，对账依据=仓库根
    /// AI训练准备_v3对账报告_2026-10-14.md）：
    ///
    /// 槽位分配（每玩家 46 槽，共 92 槽；容量=引擎真值精确对齐）：
    ///   [0..11]  战场 Battlefield (12 = BattlefieldCapacityPerPlayer，单位区 2×6)
    ///   [12..25] 元素池 ElementPool (14 = 地牌槽编码容量护栏——2026-10-15 规则硬顶退役后仅编码上限；按色叠格)
    ///   [26..37] 手牌 Hand (12 = 回合中溢出护栏；回合末上限 7 由 globals 表达)
    ///   [38..41] 墓地 Graveyard (4，新死优先：slot38=最新入墓；更早只进计数)
    ///   [42..44] 发动区 Activation (3，公开区/反制指向点；第 4+ 张不可编码)
    ///   [45]     场地区 FieldZone (1，英雄技能卡)
    /// 牌库/放逐 v3 仅以计数入 globals（放逐计数为本版新增）。
    /// 对方手牌是隐藏信息：只写 valid+controller（不开图），张数在 globals g[6]。
    ///
    /// 位置特征 [66][67]：单位格=落位钉子+first-free（槽序≠格序，必须显式编码；
    /// 摧压溅射=左右同排）；地牌格=按色固定（LandCellOfElement——格坐标即锁色）。
    /// board=null 时单位位置降级 -1（不炸）；地牌格是纯几何不需 board。
    /// 地牌横置态在 PooledCard 包装上（Card.IsTapped 恒 0，v2 失效维——本版从包装读）。
    /// </summary>
    public sealed class TideObservation
    {
        // ---- 维度常量（Python 侧只改这些，模型不动）----
        public const int LayoutVersion = 4;
        public const int MaxCardsPerPlayer = 46;
        public const int MaxCardsTotal = MaxCardsPerPlayer * 2;
        // 卡特征布局（内容身份三通路，[15..] 由 CardIdentityService 推导；槽位下标全部由
        // AtomIdSlots 派生——扩容只改 AtomIdSlots 一处，python N_CARD_FEATURES 同步）：
        //   [0..13]  状态特征（valid/controller/超类/力量/生命/费用/横置/关键词/指示物…实时值）
        //   [14]     LandTrait（v3 复用神佑死维：TapCreature 资格与威胁预测）
        //   [15..26] 原子精确内容哈希下标（跨效果按执行序展平，超出 12 个截断；参数敏感。
        //            容量依据 2026-10-16：正式卡满配 9 = 代价1 + 主动/自动组合各按2 + 关键词3，留3槽余量）
        //   [27]     组合结构哈希下标（时点/条件/代价/Steps 骨架 =「怎么组合」）
        //   [28]     关键词/tag/光环组合哈希下标
        //   [29..40] 原子 EffectType 下标（原子表内排名，参数无关）
        //   [41..112] 原子参数浮点块（12 槽 × CardIdentityService.AtomParamDim 维）
        //   [113]    AwakenCommitted（苏醒已立约——CommitAwaken 决策相关）
        //   [114][115] 格坐标 x/Width、z/Height（未落格 = -1）
        // 精确哈希 = 冻结的查表身份；类型 + 参数 = 泛化通路。0 = 无该成分 / token / 未登记。
        public const int HashIdStart = 15;
        public const int AtomIdSlots = 12; // 2026-10-16 6→12（正式卡满配9+余量3）
        public const int StructureSlot = HashIdStart + AtomIdSlots;            // [27] 组合结构哈希
        public const int CompositionSlot = StructureSlot + 1;                  // [28] 关键词/tag/光环组合哈希
        public const int TypeIdStart = CompositionSlot + 1;                    // [29..40] 类型下标槽
        public const int ParamStart = TypeIdStart + AtomIdSlots;               // [41..] 参数块
        public const int AwakenSlot = ParamStart + AtomIdSlots * CardIdentityService.AtomParamDim; // [113]
        public const int PosXSlot = AwakenSlot + 1;
        public const int PosZSlot = AwakenSlot + 2;
        public const int NCard = PosZSlot + 1; // v4：68→116（原子槽 6→12）；python N_CARD_FEATURES 同步
        // globals v3（50 维，契约 §5——重排序+归一化+放逐计数+弹药维；v2 g[48] 冗余删除）：
        //   g[0..16] 双方生命/30、回合/40、相位、座次、各区计数（墓地/30、牌库/60、放逐新）；
        //   g[17..28] 临时钱包 12 维；g[29] 栈占用；g[30..31] 势能 Φ/30；g[32..33] 地牌上限/14；
        //   g[34..37] 黑白已获得/14；g[38..45] 双方英雄技能块（P1 绝对序）[has,tapped,uses/10,弹药/5]；
        //   g[46..48] 栈深度/16、栈顶来源/92、栈顶 effectIdentity/1024；g[49] 决策上下文。
        public const int NGlobal = 50;
        public const int NAction = 8; // 动作特征槽：valid/type/sourceIndex/targetIndex/modeIndex/actionCost/effectIdentity/targetKind

        public float[] Cards = new float[MaxCardsTotal * NCard];
        public float[] Globals = new float[NGlobal];

        private Player _me;

        /// <summary>从核心构建快照（me = 当前回合玩家；Main 决策点视角——无棋盘降级入口）。</summary>
        public static TideObservation Build(GameCore core)
            => Build(core, core?.TurnEngine?.TurnPlayer, false);

        /// <summary>从核心构建快照（v3 视角参数化）：seat = 当前决策座次——
        /// Main 决策点 = 回合玩家；响应窗口停靠 = 优先权持有方（可能是非回合玩家）。
        /// 隐藏信息（对方手牌）按该 seat 的对手计算；g[0..35] 语义全部相对 seat。
        /// responseContext：停靠类型（g[49]，0=己方 Main / 1=响应窗口）。
        /// board：棋盘占用层（v3 位置特征；null=单位格降级 -1，地牌格仍可编码——纯几何）。</summary>
        public static TideObservation Build(GameCore core, Player seat, bool responseContext = false,
            BoardState board = null)
        {
            var obs = new TideObservation();
            if (core == null || core.TurnEngine == null || seat == null)
                return obs;
            var opp = seat.Opponent;
            if (opp == null) return obs;
            obs._me = seat;

            obs.WritePlayer(core, seat, 0, concealHand: false, board);
            obs.WritePlayer(core, opp, MaxCardsPerPlayer, concealHand: true, board);
            obs.WriteGlobals(core, seat, opp, responseContext);
            return obs;
        }

        /// <summary>某卡在 cards_ 中的槽位下标（controller 相对 me；未编码区/超出区域容量返回 -1）。</summary>
        public static int CardIndex(Player me, Player controller, Zone zone, int indexInZone)
        {
            if (!ZoneLayout(zone, out int off, out int cap) || indexInZone < 0 || indexInZone >= cap) return -1;
            int baseSlot = ReferenceEquals(controller, me) ? 0 : MaxCardsPerPlayer;
            return baseSlot + off + indexInZone;
        }

        /// <summary>卡实例 → cards_ 槽位下标：动作行目标/栈顶来源的反查——扫描双方全部已编码区
        /// （未编码区如牌库/放逐/衍生物临时区返回 -1）。仅枚举/观测辅助用，非热路径权威。</summary>
        public static int IndexOfCard(GameCore core, Player me, Card c)
        {
            if (core?.ZoneManager == null || me == null || c == null) return -1;
            int slot = TryZoneSlot(core.ZoneManager, me, c, 0);
            if (slot >= 0) return slot;
            var opp = me.Opponent;
            return opp != null ? TryZoneSlot(core.ZoneManager, opp, c, MaxCardsPerPlayer) : -1;
        }

        private static readonly Zone[] EncodedZones =
            { Zone.Battlefield, Zone.ElementPool, Zone.Hand, Zone.Graveyard, Zone.Activation, Zone.FieldZone };

        private static int TryZoneSlot(ZoneManager zm, Player p, Card c, int baseSlot)
        {
            foreach (var zone in EncodedZones)
            {
                var cards = zm.GetCards(p, zone);
                if (cards == null) continue;
                int idx = cards.IndexOf(c);
                if (idx >= 0 && ZoneLayout(zone, out int off, out int cap) && idx < cap)
                    return baseSlot + off + idx;
            }
            return -1;
        }

        /// <summary>区域 → (槽位基址, 容量)（未编码区返回 false）。v3 精确对齐引擎容量（契约 §3）：
        /// 战场 12=BattlefieldCapacityPerPlayer、元素池 14=地牌槽编码容量护栏（2026-10-15 规则硬顶退役后
        /// 仅编码上限，超出部分照常参与规则只是不可编码）、手牌 12=回合中溢出护栏
        ///（回合末上限 7 由 globals 表达；第 13+ 张动作仍可执行，只是来源槽不可编码——防串区）、
        /// 墓地 4（新死优先）、发动区 3、场地区 1（技能卡）。容量变更须先改契约文档再改这里。</summary>
        public static bool ZoneLayout(Zone zone, out int offset, out int cap)
        {
            switch (zone)
            {
                case Zone.Battlefield: offset = 0; cap = 12; return true;
                case Zone.ElementPool: offset = 12; cap = 14; return true;
                case Zone.Hand: offset = 26; cap = 12; return true;
                case Zone.Graveyard: offset = 38; cap = 4; return true;
                case Zone.Activation: offset = 42; cap = 3; return true;
                case Zone.FieldZone: offset = 45; cap = 1; return true;
                default: offset = -1; cap = 0; return false;
            }
        }

        // =====================================================

        private void WritePlayer(GameCore core, Player p, int baseSlot, bool concealHand, BoardState board)
        {
            // 布局归属（LandCellOfElement 的 player 下标）：Player1=下半场 0
            int playerIdx = ReferenceEquals(p, core.Player1) ? 0 : 1;

            foreach (var zone in EncodedZones)
            {
                if (!ZoneLayout(zone, out int off, out int cap)) continue;
                bool conceal = concealHand && zone == Zone.Hand;
                WriteZone(core, p, baseSlot + off, zone, cap, conceal, board, playerIdx);
            }
        }

        private void WriteZone(GameCore core, Player p, int baseSlot, Zone zone, int cap,
            bool conceal, BoardState board, int playerIdx)
        {
            var zm = core.ZoneManager;
            var layer = core.LayerEngine;
            var cards = zm.GetCards(p, zone);
            if (cards == null) return;

            // 元素池地牌：横置态与锁色在 PooledCard 包装上（Card.IsTapped 恒 0——v2 失效维，v3 从包装读）
            Dictionary<Card, PooledCard> pooledOf = null;
            if (zone == Zone.ElementPool)
            {
                pooledOf = new Dictionary<Card, PooledCard>();
                var pooled = core.ElementPool.GetPooledCards(p);
                if (pooled != null)
                    foreach (var pc in pooled)
                        if (pc?.SourceCard != null) pooledOf[pc.SourceCard] = pc;
            }

            for (int i = 0; i < cap; i++)
            {
                // 墓地新死优先（slot0=最新入墓）——最近的死亡最影响决策（苏生源/威胁推理）
                int idx = zone == Zone.Graveyard ? cards.Count - 1 - i : i;
                if (idx < 0 || idx >= cards.Count) continue; // 空槽保持全 0（valid=0）

                var c = cards[idx];
                if (conceal) { WriteConcealedCard(baseSlot + i); continue; }

                float posX = -1f, posZ = -1f;
                bool tappedOverride = false;
                if (zone == Zone.Battlefield && board != null && board.TryGetCell(c, out int x, out int z))
                {
                    posX = (float)x / BoardLayout.Width;
                    posZ = (float)z / BoardLayout.Height;
                }
                else if (zone == Zone.ElementPool && pooledOf != null)
                {
                    pooledOf.TryGetValue(c, out var pc);
                    tappedOverride = pc?.IsTapped ?? false;
                    var color = pc?.LockedColor ?? ManaType.Gray;
                    var (lx, lz) = BoardLayout.LandCellOfElement(playerIdx, color);
                    posX = (float)lx / BoardLayout.Width;
                    posZ = (float)lz / BoardLayout.Height;
                }

                WriteCard(layer, c, baseSlot + i, tappedOverride, posX, posZ);
            }
        }

        /// <summary>隐藏信息区（对方手牌）：只写存在性与归属，其余特征（含身份槽/位置）全 0 或 -1
        /// ——模型不开图（张数在 global_）。</summary>
        private void WriteConcealedCard(int slot)
        {
            int b = slot * NCard;
            Cards[b + 0] = 1f; // valid（这里有一张未知卡）
            Cards[b + 1] = 1f; // controller = 对方（该半区恒 opp）
            Cards[b + PosXSlot] = -1f; // 未落格（手牌不在棋盘上）
            Cards[b + PosZSlot] = -1f;
        }

        private void WriteCard(LayerEngine layer, Card c, int slot, bool tappedOverride, float posX, float posZ)
        {
            int b = slot * NCard;
            int power = layer.CalculatePower(c);
            int life = layer.CalculateToughness(c);
            float cost = GameActions.GetCardCost(c).Total;

            Cards[b + 0] = 1f; // valid
            Cards[b + 1] = ReferenceEquals(c.GetController(), _me) ? 0f : 1f; // controller
            Cards[b + 2] = c is IHasSupertype s ? (int)s.Supertype : 0f;       // supertype
            Cards[b + 3] = Clamp(power, -99f, 999f);                          // 结算力量
            Cards[b + 4] = Clamp(life, -99f, 999f);                           // 结算防御/生命
            Cards[b + 5] = Clamp(cost, 0f, 99f);                              // 声明费用和
            Cards[b + 6] = (c.IsTapped() || tappedOverride) ? 1f : 0f;       // 横置（地牌读包装）
            Cards[b + 7] = (!c.IsTapped() && power > 0) ? 1f : 0f;           // 可攻击粗判
            Cards[b + 8] = KeywordRules.HasRushSickness(c) ? 1f : 0f;        // 召唤失调
            Cards[b + 9] = c.GetCounterCount(CounterRules.ToxinCounter);   // 毒素层数
            Cards[b + 10] = c.GetCounterCount(CounterRules.SilenceCounter) > 0 ? 1f : 0f;
            Cards[b + 11] = c.GetCounterCount(CounterRules.CostUpCounter)
                          - c.GetCounterCount(CounterRules.CostDownCounter); // 费用层净量
            Cards[b + 12] = c.HasKeyword(KeywordRules.Taunt) ? 1f : 0f;
            Cards[b + 13] = (c.GetCounterCount(CounterRules.StealthCounter) > 0 || c.HasKeyword(KeywordRules.Concealed)) ? 1f : 0f; // 潜行
            Cards[b + 14] = c.HasKeyword(KeywordRules.LandTrait) ? 1f : 0f; // 地牌特性（v3：TapCreature 资格）
            // 内容身份（原子级拆散，CardIdentityService 与费用同管线推导），三通路：
            // 1) 精确哈希 [15..26] 原子 + [27] 结构 + [28] 组合——内容寻址查表，同单元跨卡共享行；
            // 2) 类型下标 [29..40]（参数无关，同 EffectType 共享行）；
            // 3) 参数块 [41..]（每原子 AtomParamDim 维浮点，线性投影读幅值）。
            var data = c is CardWrapper wrapper ? wrapper.GetData() : null;
            var atomHashes = data?.AtomContentHashes;
            for (int e = 0; e < AtomIdSlots && e < (atomHashes?.Length ?? 0); e++)
                Cards[b + HashIdStart + e] = TideCardIndex.IndexOf(atomHashes[e]);
            Cards[b + StructureSlot] = TideCardIndex.IndexOf(data != null ? data.StructureContentHash : 0UL);
            Cards[b + CompositionSlot] = TideCardIndex.IndexOf(data != null ? data.CompositionContentHash : 0UL);
            var typeIdx = data?.AtomTypeIndexes;
            for (int e = 0; e < AtomIdSlots && e < (typeIdx?.Length ?? 0); e++)
                Cards[b + TypeIdStart + e] = typeIdx[e];
            var atomParams = data?.AtomParams;
            int dim = CardIdentityService.AtomParamDim;
            int nParams = Math.Min(AtomIdSlots, (atomParams?.Length ?? 0) / dim);
            for (int e = 0; e < nParams; e++)
                for (int p = 0; p < dim; p++)
                    Cards[b + ParamStart + e * dim + p] = atomParams[e * dim + p];
            Cards[b + AwakenSlot] = c.AwakenCommitted ? 1f : 0f;
            Cards[b + PosXSlot] = posX;
            Cards[b + PosZSlot] = posZ;
        }

        private void WriteGlobals(GameCore core, Player me, Player opp, bool responseContext)
        {
            var g = Globals;
            var zm = core.ZoneManager;
            var ep = core.ElementPool;

            g[0] = me.GetLife() / 30f;
            g[1] = opp.GetLife() / 30f;
            g[2] = core.TurnEngine.TurnNumber / 40f;
            g[3] = (int)(core.TurnEngine.CurrentPhase?.Phase ?? PhaseType.Standby); // 0/1/2
            g[4] = ReferenceEquals(me, core.TurnEngine.TurnPlayer) ? 1f : 0f; // is_my_turn（决策座次视角）
            g[5] = zm.GetCards(me, Zone.Hand).Count;
            g[6] = zm.GetCards(opp, Zone.Hand).Count;
            g[7] = zm.GetCards(me, Zone.Battlefield).Count;
            g[8] = zm.GetCards(opp, Zone.Battlefield).Count;
            g[9] = ep.GetPooledCards(me).Count;
            g[10] = ep.GetPooledCards(opp).Count;
            g[11] = zm.GetCards(me, Zone.Graveyard).Count / 30f;
            g[12] = zm.GetCards(opp, Zone.Graveyard).Count / 30f;
            g[13] = zm.GetCards(me, Zone.Deck).Count / 60f;
            g[14] = zm.GetCards(opp, Zone.Deck).Count / 60f;
            g[15] = zm.GetCards(me, Zone.Exile).Count;  // v3 新增：放逐计数（v2 完全缺失）
            g[16] = zm.GetCards(opp, Zone.Exile).Count;
            // g[17..28] 临时钱包（现取现用：效果授予元素，回合末清空）——固定六色向量（维度 v3 冻结）：
            // Any 任意元素（2026-10-23）不进观测，AI 感知其支付力走 CanAfford 规则口（PlanBill 含链尾 Any）
            for (int i = 0; i < 6; i++) g[17 + i] = ep.GetPool(me).TempElements[(ManaType)i];
            for (int i = 0; i < 6; i++) g[23 + i] = ep.GetPool(opp).TempElements[(ManaType)i];
            g[29] = core.StackEngine.IsEmpty ? 0f : 1f; // 栈占用（二值）
            g[30] = FieldValueReward.TotalValue(core, me) / 30f;  // 己方全资源价值（观测口径含手牌；归一化只为数值健康）
            g[31] = FieldValueReward.TotalValue(core, opp) / 30f;
            // 地牌上限（2026-10-15：自然封顶 10+成长无上限——归一化基准 14 保持 v3 训练分布不变，超出饱和到 1）
            g[32] = Math.Min(ep.GetLandCap(me) / 14f, 1f);
            g[33] = Math.Min(ep.GetLandCap(opp) / 14f, 1f);
            // 黑白本回合已获得台账（使用侧约束=单次贡献≤地牌上限；同理饱和）
            g[34] = Math.Min(ep.GetPool(me).BlackGainedThisTurn / 14f, 1f);
            g[35] = Math.Min(ep.GetPool(me).WhiteGainedThisTurn / 14f, 1f);
            g[36] = Math.Min(ep.GetPool(opp).BlackGainedThisTurn / 14f, 1f);
            g[37] = Math.Min(ep.GetPool(opp).WhiteGainedThisTurn / 14f, 1f);

            // 英雄技能块（绝对座次序：P1=38 / P2=42）
            WriteHeroSkillBlock(core, core.Player1, 38);
            WriteHeroSkillBlock(core, core.Player2, 42);

            var stack = core.StackEngine.GetStackContents(); // Reverse 序（栈顶在末位）
            g[46] = Math.Min(stack.Count, 16) / 16f;          // 栈深度（归一化 /16）
            var top = stack.Count > 0 ? stack[stack.Count - 1] : null;
            g[47] = top?.Source is Card topCard ? Math.Max(0, IndexOfCard(core, me, topCard)) / MaxCardsTotal : 0f;
            g[48] = top?.Definition != null
                ? TideCardIndex.IndexOfEffectDefinition(top.Definition) / 1024f
                : 0f; // 同动作特征 [6] 口径（cast/攻击/SBA 栈顶无效果定义 = 0）
            g[49] = responseContext ? 1f : 0f;                // 决策上下文（0=己方 Main / 1=响应窗口）
        }

        /// <summary>英雄技能 4 维块（契约 g[38..41]=P1 / g[42..45]=P2，绝对座次序）：
        /// [hasSkill, 本回合已用(tapped), 累计使用/10, 角色攻击弹药/5（v3 启用原预留维——
        /// HeroAttackCounter 层数即角色攻击资格与反伤力度）]。技能卡本体在场地区槽 [45] 逐卡编码。</summary>
        private void WriteHeroSkillBlock(GameCore core, Player p, int b)
        {
            var g = Globals;
            var card = p != null ? HeroSkillSystem.ResolveSkillCard(core, p) : null;
            bool has = card != null;
            g[b] = has ? 1f : 0f;
            g[b + 1] = has && card.IsTapped() ? 1f : 0f; // 本回合已用（回合开始重置）
            g[b + 2] = has ? HeroSkillSystem.GetTotalUses(core, p) / 10f : 0f;
            g[b + 3] = p != null ? p.GetCounterCount(Attribute.CounterRules.HeroAttackCounter) / 5f : 0f;
        }

        private static float Clamp(float v, float min, float max)
            => v < min ? min : (v > max ? max : v);
    }

    /// <summary>
    /// 内容身份登记簿：ulong 内容哈希（CardIdentityService：原子哈希 / 组合结构哈希 /
    /// 关键词+tag+光环组合哈希，均已混入原子表指纹）→ 池内小整数下标，供 TideObservation
    /// [15..22] 输出、Python 侧 nn.Embed 查表。拆散到原子级：同原子（如 造成伤害4）跨卡共享
    /// 同一行——相近效果在原子层重叠，语义直接迁移。
    /// 0 = 无该成分 / token / 未登记；容量 1024 与 Python N_CARD_POOL 对齐。
    /// 键是内容哈希（内容寻址，别名 ID 不参与）。容量口径（2026-09-21 扩容定案）：
    /// 占行的是「原子实例级身份」（原子类型+参数+目标域组合 / 结构骨架 / 关键词组合），
    /// 不是卡——白板卡不占行、同原子跨卡共享行；1024 ≈ 千张常用卡的互异效果实例余量。
    /// 触顶静默降级不崩（新哈希记 0，类型+参数通路照常）。
    ///
    /// 分配口径（追加式 + manifest 持久化，2026-09-09 修复）：
    ///   - 已分配的下标永不改写（旧实现重复 Register 时把全部哈希覆盖成 Count+1，
    ///     第二局起全体身份塌缩到同一行——已修）；
    ///   - 新哈希按批内排序追加到末尾（首批 = 全池排序 1..N，与历史口径一致），
    ///     任何新卡/新组合都不再挪动已有下标——已训 embedding 行永不串台；
    ///   - ConfigureManifest 绑定清单文件（训练/部署同一份）：启动载入、有新增即落盘。
    ///     表指纹不符则作废旧清单（表变更全体身份换血，旧下标本就无意义）。
    /// </summary>
    public static class TideCardIndex
    {
        private const int Capacity = 1024; // 2026-09-21 256→1024（趁新口径重训扩容；manifest 下标系统不变）
        private static readonly Dictionary<ulong, int> _map = new Dictionary<ulong, int>();
        private static string _manifestPath; // ConfigureManifest 绑定；null = 纯内存（无持久化）

        /// <summary>已登记身份数（诊断/日志用）。</summary>
        public static int Count => _map.Count;

        /// <summary>绑定清单文件并载入既有分配。返回载入条数；-1 = 表指纹不符（旧清单已作废，从零开始）。
        /// 幂等：重复调用以首次绑定的文件为准（训练服务器与自测共用同一路径）。</summary>
        public static int ConfigureManifest(string path)
        {
            if (string.IsNullOrEmpty(path)) return 0;
            if (_manifestPath != null) return 0; // 已绑定，不重复载入（进程内下标分配已生效）
            _manifestPath = path;

            try
            {
                if (!File.Exists(path)) return 0;
                bool tableMatched = false;
                int loaded = 0;
                foreach (var line in File.ReadAllLines(path))
                {
                    var t = line.Trim();
                    if (t.Length == 0 || t.StartsWith("#")) continue;
                    var parts = t.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2 && parts[0] == "table")
                    {
                        tableMatched = ulong.TryParse(parts[1], out ulong fp) && fp == CardIdentityService.TableFingerprint();
                        if (!tableMatched) break;
                        continue;
                    }
                    if (parts.Length == 2
                        && ulong.TryParse(parts[0], out ulong h) && h != 0UL
                        && int.TryParse(parts[1], out int idx)
                        && idx >= 1 && idx < Capacity && tableMatched)
                    {
                        _map[h] = idx; // 清单是权威分配，直接采纳
                        loaded++;
                    }
                }
                if (!tableMatched) return -1;
                // 载入后 Count 可能小于最大下标（清单曾被截断）；Count 只用于「追加位置」，
                // 从最大下标续排，避免与清单内保留条目撞号。
                foreach (var idx in _map.Values)
                    if (idx > _maxIndex) _maxIndex = idx;
                return loaded;
            }
            catch
            {
                return 0; // 读失败按无清单处理（不影响本局注册）
            }
        }

        private static int _maxIndex;

        /// <summary>登记卡池（HandleReset / 自测菜单调用；可重复调用）：收集全部内容哈希 →
        /// 去重排序 → 未登记者按批内排序追加分配（_maxIndex+1 起）。返回本批新增条数。
        /// 已登记哈希跳过——跨局重复 Register 是常态（每次 reset 都调），追加式保证幂等。
        /// 容量满后停止分配（返回值小于待登记数，调用方可据此告警）。</summary>
        public static int Register(IEnumerable<CardData> pool)
        {
            if (pool == null) return 0;
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

            int added = 0;
            foreach (var h in hashes)
            {
                if (_map.Count >= Capacity - 1) break;
                if (_map.ContainsKey(h)) continue; // 追加式：已分配下标永不改写
                _map[h] = ++_maxIndex;
                added++;
            }
            if (added > 0 && _manifestPath != null) SaveManifest();
            return added;
        }

        /// <summary>内容哈希 → 下标（未登记 = 0）。</summary>
        public static int IndexOf(ulong hash)
            => hash != 0UL && _map.TryGetValue(hash, out int idx) ? idx : 0;

        /// <summary>哈希 → 下标，未登记则追加分配（v2 effectIdentity 用）。追加式口径同 Register：
        /// 已分配下标永不改写、容量满记 0 静默降级、有清单绑定时落盘。</summary>
        public static int IndexOfOrRegister(ulong hash)
        {
            if (hash == 0UL) return 0;
            if (_map.TryGetValue(hash, out int idx)) return idx;
            if (_map.Count >= Capacity - 1) return 0;
            _map[hash] = ++_maxIndex;
            if (_manifestPath != null) SaveManifest();
            return _maxIndex;
        }

        /// <summary>效果定义 → 身份下标（动作特征 [6] / globals g[48] 的 effectIdentity）：
        /// 键 = EffectDefinition.Id（转换自卡表的内容哈希稳定键）的 FNV-1a 64 + 效果空间盐——
        /// 与 CardIdentityService 的卡身份哈希族隔离（64 位空间 + 盐，碰撞概率可忽略；即撞也只共用行不崩）。
        /// 追加式注册进同一 manifest（不动表指纹）；同一效果定义跨局同 Id → 同下标（embedding 不稀释）。
        /// 空 Id（动态授予的效果）= 0（未登记信号）。</summary>
        public static int IndexOfEffectDefinition(EffectDefinition def)
        {
            if (def == null || string.IsNullOrEmpty(def.Id)) return 0;
            ulong h = 0x9E3779B97F4A7C15UL; // 效果定义空间盐（区别于卡身份哈希族）
            foreach (var ch in def.Id)
            {
                h ^= ch;
                h *= 1099511628211UL;
            }
            return IndexOfOrRegister(h);
        }

        /// <summary>清单落盘（临时文件 + 替换，原子写）。格式：# 注释 / table 指纹 / 「哈希 下标」行。</summary>
        private static void SaveManifest()
        {
            try
            {
                var dir = Path.GetDirectoryName(_manifestPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                var sb = new System.Text.StringBuilder();
                sb.Append("# TideCardIndex identity manifest (append-only; hash=decimal index)\n");
                sb.Append("table ").Append(CardIdentityService.TableFingerprint()).Append('\n');
                foreach (var kvp in _map)
                    sb.Append(kvp.Key).Append(' ').Append(kvp.Value).Append('\n');
                string tmp = _manifestPath + ".tmp";
                File.WriteAllText(tmp, sb.ToString());
                if (File.Exists(_manifestPath)) File.Delete(_manifestPath);
                File.Move(tmp, _manifestPath);
            }
            catch
            {
                // 落盘失败不阻断训练（本进程内分配仍一致；下次进程从旧清单续排，极端情况 =
                // 本次新增哈希在下次进程换号，与未持久化时的行为相同）
            }
        }
    }
}
