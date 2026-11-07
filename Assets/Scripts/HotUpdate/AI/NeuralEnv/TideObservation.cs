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
    /// 槽位分配（每玩家 77 槽，共 154 槽 + 尾块 35 = 189；容量=引擎真值精确对齐）：
    ///   [0..8]   战场 Battlefield (9 = BattlefieldCapacityPerPlayer，单位区 4+5；P11 盘面重塑)
    ///   [9..22]  元素池 ElementPool (14 = 地牌槽编码容量护栏——2026-10-15 规则硬顶退役后仅编码上限；按色叠格)
    ///   [23..34] 手牌 Hand (12 = 回合中溢出护栏；回合末上限 7 由 globals 表达)
    ///   [35..64] 墓地 Graveyard (30 = v10 4→30 全量，新死优先：slot35=最新入墓；超出 30 截断)
    ///   [65..67] 发动区 Activation (3，公开区/反制指向点；第 4+ 张不可编码)
    ///   [68]     场地区 FieldZone (1，英雄技能卡)
    ///   [69..76] 除外区 Exile (8 = v10 逐卡入册，区内时序 index0=最先放逐；超出 8 截断)
    /// v9（2026-11-02 牌库双通道）：尾接己方牌库块 [154..183] 共 30 槽（仅决策座次方，无序集合
    /// 按内容哈希排序编码——见 WriteOwnDeck；对方牌库不进 cards_，走 g[50..57] 开局标签通道）。
    /// v10（2026-11-02 观测欠账四件套）：+牌库顶槽 [184]（光环命中才编码，防泄露下一抽）
    /// +对方牌库被展示卡块 [185..188]（RevealRules 信息轴）；对方手牌被展示卡豁免为全特征开图。
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
        // v11（2026-11-05 充能系统动作化）：动作空间语义变更——Charge 行从「带充能效果的卡逐色
        // 一行（Color 承载选色）」改为「任意手牌卡每卡一行（免元素、选色移交引擎结算期反问，
        // AI 代取最大余量色）」；充能从主动效果入栈改为速度0系统动作。观测布局（槽位/特征数）不变，
        // 旧 v10 模型对充能行的语义不符作废（python LAYOUT_VERSION 同步升 11）。
        // v10（2026-11-02 观测欠账四件套）：卡槽 116→189——墓地区 4→30 全量（新死优先保留）、
        // 除外区逐卡 8 槽入册（连带修复 LAE 目标槽位断链：除外回收/深墓地卡 IndexOfCard 原恒 -1）、
        // 对方手牌被展示（Exposed）卡豁免为全特征开图、尾块 +牌库顶槽 1（TopDeckAsHandAura
        // 光环命中才编码——防泄露下一抽）+对方牌库被展示卡 4（RevealRules 信息轴接入）。
        // 每玩家槽 43→77、DeckBase 86→154。NCard/NGlobal 不变（zoneKind 值扩 7/8/9）。
        // 旧 v9 模型布局不符作废（python MAX_CARDS/LAYOUT_VERSION 同步升 10）。
        // v9（2026-11-02 牌库双通道）：卡槽 86→116（尾接己方牌库块 [86..115] 共 30 槽，仅决策
        // 座次方、按内容哈希排序编码防泄露洗牌顺序）+ 卡特征 115→116（尾加 [115] zoneKind——
        // 逐卡编码器无槽位位置嵌入，不加则牌库卡与手牌卡特征同形不可区分）+ globals 50→58
        //（尾加 g[50..57] 对方牌库开局标签：GameCore.OriginalDeckOf 快照，整局恒定）。
        // 旧 v8 模型布局不符作废（python MAX_CARDS/N_CARD_FEATURES/N_GLOBAL_FEATURES/LAYOUT_VERSION 同步升 9）。
        // v8（2026-11-02 LAE 多目标动作空间）：动作特征 8→11 维——追加 [8]target2Index/[9]target3Index/
        // [10]nTargets（多目标组合行次目标可见；逐原子 effCount 选 N 与 EmitCombinations 组合行可区分）。
        // 旧 v7 模型布局不符作废（python LAYOUT_VERSION 同步升 8、N_ACTION_FEATURES 8→11）。
        // v7（2026-10-29 对座镜像修订）：盘面坐标轴平移——双方六格行同列带 x1..x6、P2 改
        // 上下镜像像（槽位数/容量与 v6 一致，位置特征 posX/posZ 分布位移；python LAYOUT_VERSION 同步升 7）。
        // v6（2026-10-29 P11 盘面重塑）：战场槽 12→9（单位区 4+5）——每玩家 46→43 槽、
        // 后续区域槽位整体前移 3；位置特征归一化分母随 BoardLayout.Width 8→7。
        // 旧模型权重作废（python LAYOUT_VERSION 同步升 6）。
        // v5（2026-10-29）：删卡特征 [113]——NCard 116→115、N_GLOBAL 不变。
        // v12（2026-11-06 墓碑清理）：TideActionType 空洞移除连续重排——动作特征[1] type 数值漂移，
        // 旧模型作废（python LAYOUT_VERSION 同步升 12）。
        public const int LayoutVersion = 12;
        public const int MaxCardsPerPlayer = 77; // v10：43→77（墓地 4→30、除外 8 入册）
        // v9 己方牌库块（尾接全局块，不走 ZoneLayout 每玩家偏移语义——专用 DeckBase+排序名次路径）：
        public const int DeckSlots = 30;                                      // 截断护栏（初始整组 30 恰满；衍生物入库超出不编码）
        public const int DeckBase = MaxCardsPerPlayer * 2;                    // v10：86→154
        public const int DeckTopSlot = DeckBase + DeckSlots;                  // v10 [184]：牌库顶虚拟手牌（光环命中才编码）
        public const int OppDeckExposedSlots = 4;                             // v10：对方牌库被展示卡截断护栏
        public const int OppDeckExposedBase = DeckTopSlot + 1;                // v10 [185..188]
        public const int MaxCardsTotal = DeckBase + DeckSlots + 1 + OppDeckExposedSlots; // v10：116→189
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
        //   [113][114] 格坐标 x/Width、z/Height（未落格 = -1）
        // 精确哈希 = 冻结的查表身份；类型 + 参数 = 泛化通路。0 = 无该成分 / token / 未登记。
        public const int HashIdStart = 15;
        public const int AtomIdSlots = 12; // 2026-10-16 6→12（正式卡满配9+余量3）
        public const int StructureSlot = HashIdStart + AtomIdSlots;            // [27] 组合结构哈希
        public const int CompositionSlot = StructureSlot + 1;                  // [28] 关键词/tag/光环组合哈希
        public const int TypeIdStart = CompositionSlot + 1;                    // [29..40] 类型下标槽
        public const int ParamStart = TypeIdStart + AtomIdSlots;               // [41..] 参数块
        public const int PosXSlot = ParamStart + AtomIdSlots * CardIdentityService.AtomParamDim; // [113]
        public const int PosZSlot = PosXSlot + 1;                              // [114]
        // v9 区域种类 [115]：显式映射（不依 Zone 枚举序，防重排）——0=战场 1=元素池 2=手牌 3=墓地
        // 4=发动区 5=场地区 6=己方牌库块；v10 扩 7=牌库顶虚拟手牌 8=对方牌库被展示 9=除外区。
        // 对方隐藏手牌=2（WriteConcealedCard；被展示卡豁免为全特征，仍写 2=手牌）。
        public const int ZoneKindSlot = PosZSlot + 1;                          // [115]
        public const int NCard = ZoneKindSlot + 1; // v9：115→116（尾加 zoneKind）；python N_CARD_FEATURES 同步
        // globals v3（50 维，契约 §5——重排序+归一化+放逐计数+弹药维；v2 g[48] 冗余删除）：
        //   g[0..16] 双方生命/30、回合/40、相位、座次、各区计数（墓地/30、牌库/60、放逐新）；
        //   g[17..28] 临时钱包 12 维；g[29] 栈占用；g[30..31] 势能 Φ/30；g[32..33] 地牌上限/14；
        //   g[34..37] 黑白已获得/14；g[38..45] 双方英雄技能块（P1 绝对序）[has,tapped,uses/10,弹药/5]；
        //   g[46..48] 栈深度/16、栈顶来源/MaxCardsTotal、栈顶 effectIdentity/1024；g[49] 决策上下文。
        // v9 尾加 g[50..57]（对方牌库开局标签快照，DeckTagsOf——随卡组创建保留、整局恒定）。
        public const int NGlobal = 58; // v9：50→58（+8 对方牌库标签）
        public const int DeckTagStart = 50; // v9 g[50..57] = DeckTagsOf（对方牌库开局标签）
        // 动作特征槽（v8 8→11 维）：[0]valid [1]type [2]sourceIndex [3]targetIndex [4]modeIndex
        // [5]actionCost [6]effectIdentity [7]targetKind + v8 多目标追加：
        // [8]target2Index [9]target3Index（Targets[1]/[2] 槽位，-1=无/玩家目标/>3 截断不编码）[10]nTargets（0..8 钳）。
        public const int NAction = 11;

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
            obs.WriteOwnDeck(core, seat);        // v9：己方牌库块（仅决策座次方）
            obs.WriteDeckTop(core, seat);        // v10：牌库顶虚拟手牌槽（光环命中才编码——防泄露下一抽）
            obs.WriteOppDeckExposed(core, opp);  // v10：对方牌库被展示卡尾块（RevealRules 信息轴）
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
        ///（v9 起含己方牌库块、v10 起含除外区；未编码区如对方牌库/衍生物临时区返回 -1）。
        /// 仅枚举/观测辅助用，非热路径权威。</summary>
        public static int IndexOfCard(GameCore core, Player me, Card c)
        {
            if (core?.ZoneManager == null || me == null || c == null) return -1;
            int slot = TryZoneSlot(core.ZoneManager, me, c, 0);
            if (slot >= 0) return slot;
            slot = TryDeckSlot(core.ZoneManager, me, c); // v9：己方牌库块（排序名次对齐编码序）
            if (slot >= 0) return slot;
            var opp = me.Opponent;
            return opp != null ? TryZoneSlot(core.ZoneManager, opp, c, MaxCardsPerPlayer) : -1;
        }

        /// <summary>己方牌库块反查（v9）：牌库卡在 [DeckBase..] 的槽位 = 排序名次（与
        /// WriteOwnDeck 同享 SortDeckForEncoding 单源序）。对方牌库不编码，恒 -1。</summary>
        private static int TryDeckSlot(ZoneManager zm, Player me, Card c)
        {
            var cards = zm.GetCards(me, Zone.Deck);
            if (cards == null) return -1;
            int rank = SortDeckForEncoding(cards).IndexOf(c);
            return rank >= 0 && rank < DeckSlots ? DeckBase + rank : -1;
        }

        private static readonly Zone[] EncodedZones =
            { Zone.Battlefield, Zone.ElementPool, Zone.Hand, Zone.Graveyard, Zone.Activation, Zone.FieldZone,
              Zone.Exile }; // v10：除外区入册（IndexOfCard/CardIndex 自动覆盖——除外回收目标槽位修复）

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

        /// <summary>区域 → (槽位基址, 容量)（未编码区返回 false）。v6 对齐引擎容量（P11 盘面重塑）：
        /// 战场 9=BattlefieldCapacityPerPlayer（单位区 4+5）、元素池 14=地牌槽编码容量护栏（2026-10-15 规则硬顶退役后
        /// 仅编码上限，超出部分照常参与规则只是不可编码）、手牌 12=回合中溢出护栏
        ///（回合末上限 7 由 globals 表达；第 13+ 张动作仍可执行，只是来源槽不可编码——防串区）、
        /// 墓地 30（v10 4→30 全量，新死优先；超出截断）、发动区 3、场地区 1（技能卡）、
        /// 除外 8（v10 入册，区内时序 index0=最先放逐=UI Region 面板同序；超出截断）。
        /// 容量变更须先改契约文档再改这里。</summary>
        public static bool ZoneLayout(Zone zone, out int offset, out int cap)
        {
            switch (zone)
            {
                case Zone.Battlefield: offset = 0; cap = 9; return true;
                case Zone.ElementPool: offset = 9; cap = 14; return true;
                case Zone.Hand: offset = 23; cap = 12; return true;
                case Zone.Graveyard: offset = 35; cap = 30; return true; // v10：4→30
                case Zone.Activation: offset = 65; cap = 3; return true; // v11-B（S3b 删发动区）：槽保留恒零——布局不变免重训
                case Zone.FieldZone: offset = 68; cap = 1; return true;  // v10：42→68
                case Zone.Exile: offset = 69; cap = 8; return true;      // v10 新增
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
                // v10 展示豁免（RevealRules 信息轴）：对方手牌被展示（Exposed 指示物，换区清除）的卡
                // 对双方公开——全特征开图（玩家口径：UI 翻正面/网络快照同源下发）；未展示保持隐藏
                if (conceal && !RevealRules.IsExposed(c)) { WriteConcealedCard(baseSlot + i); continue; }

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

                WriteCard(layer, c, baseSlot + i, tappedOverride, posX, posZ, ZoneKindOf(zone));
            }
        }

        // =====================================================

        /// <summary>己方牌库块 [DeckBase..]（v9 牌库双通道·己侧）：完整卡表**无序集合**——
        /// 按内容哈希排序（SortDeckForEncoding 单源）写入前 DeckSlots 张全特征卡（身份/类型/
        /// 参数三通路全吃；posX/posZ=-1、zoneKind=6）。排序键位置无关——**不泄露洗牌顺序**
        ///（同哈希副本特征全同可互换；诅咒/祝福标记差异在卡特征内公开呈现，与槽序无关）。
        /// cap=30 截断护栏：衍生物入库等超出部分不编码，剩余计数仍走 g[13]（同手牌 12 槽先例）。
        /// 对方牌库不进 cards_——走 g[50..57] 开局标签通道（真实玩家口径=开局展示的卡组标签）。</summary>
        private void WriteOwnDeck(GameCore core, Player seat)
        {
            var cards = core.ZoneManager.GetCards(seat, Zone.Deck);
            if (cards == null || cards.Count == 0) return;
            var sorted = SortDeckForEncoding(cards);
            int n = Math.Min(DeckSlots, sorted.Count);
            for (int i = 0; i < n; i++)
                WriteCard(core.LayerEngine, sorted[i], DeckBase + i, false, -1f, -1f, 6);
        }

        /// <summary>牌库顶虚拟手牌槽 [DeckTopSlot]（v10）：TopDeckAsHandAura 光环命中座次方且
        /// 牌库非空时编码 deck[0] 全特征（zoneKind=7）。**未命中恒空槽——绝不无件编码牌库顶
        ///（防泄露下一抽，验收硬断言）**。口径同源 BattleScreen.TopDeckVirtualCard（光环命中
        /// 己方+牌库非空；不计手牌上限/不可弃/放地——LAE 只枚举出牌行）。</summary>
        private void WriteDeckTop(GameCore core, Player seat)
        {
            if (!RuleAuraSystem.ScopeHits(RuleAuraComponents.TopDeckAsHandAura, seat)) return;
            var deck = core.ZoneManager.GetCards(seat, Zone.Deck);
            if (deck == null || deck.Count == 0) return;
            WriteCard(core.LayerEngine, deck[0], DeckTopSlot, false, -1f, -1f, 7);
        }

        /// <summary>对方牌库被展示卡尾块 [OppDeckExposedBase..]（v10）：RevealRules.GetExposedCards
        /// 前 4 张全特征（zoneKind=8，区内序=玩家点卡组面板所见——面板即按此序列出，无额外位置泄露）。
        /// cap=4 截断护栏。己方牌库被展示卡不另设块：身份已在己方牌库多重集块内（无增量信息）。</summary>
        private void WriteOppDeckExposed(GameCore core, Player opp)
        {
            var exposed = RevealRules.GetExposedCards(core.ZoneManager, opp, Zone.Deck);
            int n = Math.Min(OppDeckExposedSlots, exposed.Count);
            for (int i = 0; i < n; i++)
                WriteCard(core.LayerEngine, exposed[i], OppDeckExposedBase + i, false, -1f, -1f, 8);
        }

        /// <summary>牌库编码序（单源）：StructureContentHash → CompositionContentHash → RuntimeId。
        /// 位置无关的确定性全序——编码槽序与区内序（洗牌序）解耦；WriteOwnDeck 写入与
        /// TryDeckSlot 反查同享此序。token/未装载卡哈希 0 排最前（确定即可，无语义）。</summary>
        private static List<Card> SortDeckForEncoding(List<Card> cards)
        {
            var sorted = new List<Card>(cards);
            sorted.Sort(CompareDeckCards);
            return sorted;
        }

        private static int CompareDeckCards(Card a, Card b)
        {
            var da = a is CardWrapper wa ? wa.GetData() : null;
            var db = b is CardWrapper wb ? wb.GetData() : null;
            int cmp = (da != null ? da.StructureContentHash : 0UL)
                .CompareTo(db != null ? db.StructureContentHash : 0UL);
            if (cmp != 0) return cmp;
            cmp = (da != null ? da.CompositionContentHash : 0UL)
                .CompareTo(db != null ? db.CompositionContentHash : 0UL);
            if (cmp != 0) return cmp;
            return a.RuntimeId.CompareTo(b.RuntimeId);
        }

        /// <summary>区域 → zoneKind 下标值（[115]；显式映射不依 Zone 枚举序，防重排）。
        /// 己方牌库块=6 / 牌库顶=7 / 对方牌库被展示=8 由专用写入方法直传；
        /// 对方隐藏手牌=2 由 WriteConcealedCard 直传。</summary>
        private static int ZoneKindOf(Zone zone)
        {
            switch (zone)
            {
                case Zone.Battlefield: return 0;
                case Zone.ElementPool: return 1;
                case Zone.Hand: return 2;
                case Zone.Graveyard: return 3;
                case Zone.Activation: return 4;
                case Zone.FieldZone: return 5;
                case Zone.Exile: return 9; // v10
                default: return 0;
            }
        }

        /// <summary>对方牌库开局标签 8 维（v9 牌库双通道·对侧；g[50..57]）：
        /// [0..3] 四色张数（灰/红/蓝/绿=ManaType 0..3 序；逐色多计 Cost[色]&gt;0——与
        /// CardSorter.HasCostColor 同口径，多色卡在构成色各计一次）、[4..6] 费用三档张数
        ///（档值=Clamp(Round(TotalCost),1,9)：0 费并入 1 档、&gt;9 并入 9+——与 DeckBuilder
        /// 费用曲线 BuildCostCurve 同口径；[4]=1-2 [5]=3-6 [6]=7-9）、[7] 卡组总数。
        /// 输入=GameCore.OriginalDeckOf（随卡组创建保留的入局卡表快照），故整局恒定——
        /// 不随抽牌/洗回变化（真实玩家口径=开局展示的标签，非剩余构成）。
        /// 原始计数不归一化（与 g[5..10] 手牌/战场计数同族）。null/空卡表返回全 0。</summary>
        public static float[] DeckTagsOf(IReadOnlyList<CardData> deck)
        {
            var tags = new float[8];
            if (deck == null) return tags;
            int total = 0;
            foreach (var d in deck)
            {
                if (d == null) continue;
                total++;
                var cost = d.Cost;
                if (cost != null)
                    for (int i = 0; i <= (int)ManaType.Green; i++)
                        if (cost[(ManaType)i] > 0f) tags[i]++;
                // 费用档=Clamp(Round(TotalCost),1,9)，Round 为 .5 取偶（Banker's）——
                // 与 DeckBuilder BuildCostCurve 的 Mathf.RoundToInt 逐值同款（正域手写，不引 UnityEngine）
                float v = d.TotalCost;
                int fl = (int)Math.Floor(v);
                float frac = v - fl;
                int rounded = frac > 0.5f ? fl + 1 : frac < 0.5f ? fl : (fl % 2 == 0 ? fl : fl + 1);
                int tier = Math.Max(1, Math.Min(9, rounded));
                if (tier <= 2) tags[4]++;
                else if (tier <= 6) tags[5]++;
                else tags[6]++;
            }
            tags[7] = total;
            return tags;
        }

        /// <summary>隐藏信息区（对方手牌）：只写存在性与归属，其余特征（含身份槽/位置）全 0 或 -1
        /// ——模型不开图（张数在 global_）。zoneKind=2（手牌，契约 §4 v9）。</summary>
        private void WriteConcealedCard(int slot)
        {
            int b = slot * NCard;
            Cards[b + 0] = 1f; // valid（这里有一张未知卡）
            Cards[b + 1] = 1f; // controller = 对方（该半区恒 opp）
            Cards[b + ZoneKindSlot] = 2f; // zoneKind=手牌
            Cards[b + PosXSlot] = -1f; // 未落格（手牌不在棋盘上）
            Cards[b + PosZSlot] = -1f;
        }

        private void WriteCard(LayerEngine layer, Card c, int slot, bool tappedOverride, float posX, float posZ,
            int zoneKind)
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
                          - (c.GetCounterCount(CounterRules.CostDownGrayCounter)   // 2026-10-31 P12 四色
                          + c.GetCounterCount(CounterRules.CostDownRedCounter)    // 充能投放，逐色只减对应费
                          + c.GetCounterCount(CounterRules.CostDownBlueCounter)
                          + c.GetCounterCount(CounterRules.CostDownGreenCounter)); // 费用层净量（合计）
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
            Cards[b + PosXSlot] = posX;
            Cards[b + PosZSlot] = posZ;
            Cards[b + ZoneKindSlot] = zoneKind; // v9 区域种类（契约 §4）
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
            // S3b（2026-11-04 删发动区）：手牌计数剔除施放中的卡（IsPendingCast 单口全域剔除）——
            // 卡留手牌但不可用（弃牌/上限/展示已剔），观测口径与可用手牌对齐。
            var se = core.StackEngine;
            g[5] = se != null ? zm.GetCards(me, Zone.Hand).FindAll(c => !se.IsPendingCast(c)).Count : zm.GetCards(me, Zone.Hand).Count;
            g[6] = se != null ? zm.GetCards(opp, Zone.Hand).FindAll(c => !se.IsPendingCast(c)).Count : zm.GetCards(opp, Zone.Hand).Count;
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
            // g[17..28] 临时钱包（现取现用：效果授予元素，回合末清空）——原六色块（维度 v3 冻结）：
            // 2026-10-28 P2 席位收缩删白/黑后实际四色（Gray/Red/Blue/Green=枚举 0..3），
            // g[21][22]/g[27][28] 死位恒 0（同 g[34..37] 口径）；
            // Any 任意元素（2026-10-23）不进观测，AI 感知其支付力走 CanAfford 规则口（PlanBill 含链尾 Any）
            for (int i = 0; i <= (int)ManaType.Green; i++) g[17 + i] = ep.GetPool(me).TempElements[(ManaType)i];
            for (int i = 0; i <= (int)ManaType.Green; i++) g[23 + i] = ep.GetPool(opp).TempElements[(ManaType)i];
            g[29] = core.StackEngine.IsEmpty ? 0f : 1f; // 栈占用（二值）
            g[30] = FieldValueReward.TotalValue(core, me) / 30f;  // 己方全资源价值（观测口径含手牌；归一化只为数值健康）
            g[31] = FieldValueReward.TotalValue(core, opp) / 30f;
            // 地牌上限（2026-10-15：自然封顶 10+成长无上限——归一化基准 14 保持 v3 训练分布不变，超出饱和到 1）
            g[32] = Math.Min(ep.GetLandCap(me) / 14f, 1f);
            g[33] = Math.Min(ep.GetLandCap(opp) / 14f, 1f);
            // g[34..37] 原=黑白本回合已获得台账——台账已随 2026-10-28 四色重构删除；
            // 维度 v3 冻结保留槽位（下游块 g[38+] 座次不动），恒置 0（P2 删枚举成员后彻底死位）
            g[34] = 0f;
            g[35] = 0f;
            g[36] = 0f;
            g[37] = 0f;

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
            // v9 对方牌库开局标签（g[50..57]，契约 §5）：GameCore.OriginalDeckOf 入局快照计算，
            // 整局恒定（真实玩家口径=开局展示的标签，不随抽牌变化）
            var deckTags = DeckTagsOf(core.OriginalDeckOf(opp));
            for (int i = 0; i < deckTags.Length; i++)
                g[DeckTagStart + i] = deckTags[i];
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
    /// 关键词+tag+光环组合哈希，2026-11-05 ID 冻结收口后直接锚定冻结 ID——不再混原子表指纹）
    /// → 池内小整数下标，供 TideObservation [15..22] 输出、Python 侧 nn.Embed 查表。
    /// 拆散到原子级：同原子（如 造成伤害4）跨卡共享同一行——相近效果在原子层重叠，语义直接迁移。
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
    ///     manifest v2（2026-11-05 ID 冻结收口）：清单不再锚定原子表指纹——表内容编辑/新增行
    ///     不再作废清单（旧「table 指纹」头一票作废已拆）；EffectType 下标冻结分配同存清单
    ///     （type 行）。旧格式清单（-1）按作废处理：冻结前哈希混盐，键本就不兼容，重登记即可。
    /// </summary>
    public static class TideCardIndex
    {
        private const int Capacity = 1024; // 2026-09-21 256→1024（趁新口径重训扩容；manifest 下标系统不变）
        private static readonly Dictionary<ulong, int> _map = new Dictionary<ulong, int>();
        private static string _manifestPath; // ConfigureManifest 绑定；null = 纯内存（无持久化）

        /// <summary>已登记身份数（诊断/日志用）。</summary>
        public static int Count => _map.Count;

        /// <summary>绑定清单文件并载入既有分配。返回载入条数；-1 = 旧格式清单已作废（从零开始）。
        /// 幂等：重复调用以首次绑定的文件为准（训练服务器与自测共用同一路径）。
        /// manifest v2（2026-11-05 ID 冻结收口）：scheme 2 头 + type 行（EffectType 冻结分配）+
        /// 哈希行；不再有表内容闸。旧「table 指纹」头 = 冻结前混盐口径，键不兼容——作废重登记。</summary>
        public static int ConfigureManifest(string path)
        {
            if (string.IsNullOrEmpty(path)) return 0;
            if (_manifestPath != null) return 0; // 已绑定，不重复载入（进程内下标分配已生效）
            _manifestPath = path;

            try
            {
                if (!File.Exists(path)) return 0;
                bool schemeOk = false;
                var typeLines = new Dictionary<string, int>();
                int loaded = 0;
                foreach (var line in File.ReadAllLines(path))
                {
                    var t = line.Trim();
                    if (t.Length == 0 || t.StartsWith("#")) continue;
                    var parts = t.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2 && parts[0] == "scheme")
                    {
                        schemeOk = parts[1] == "2";
                        if (!schemeOk) break; // 旧 table 头 / 未知版本：冻结前清单作废
                        continue;
                    }
                    if (parts.Length == 3 && parts[0] == "type"
                        && !string.IsNullOrEmpty(parts[1])
                        && int.TryParse(parts[2], out int tix) && tix >= 1 && tix <= CardIdentityService.MaxTypeIndex)
                    {
                        typeLines[parts[1]] = tix;
                        continue;
                    }
                    if (parts.Length == 2
                        && ulong.TryParse(parts[0], out ulong h) && h != 0UL
                        && int.TryParse(parts[1], out int idx)
                        && idx >= 1 && idx < Capacity && schemeOk)
                    {
                        _map[h] = idx; // 清单是权威分配，直接采纳
                        loaded++;
                    }
                }
                if (!schemeOk) return -1;
                if (typeLines.Count > 0) CardIdentityService.AdoptTypeIndexes(typeLines);
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
                // 2026-11-05 ID 冻结收口：装载期身份可能早于清单绑定（EffectType 下标按播种号暂存）——
                // 清单 type 行 Adoption 后按冻结分配重刷一遍（幂等便宜），观测取用口径一致。
                CardIdentityService.EnsureIdentity(card);
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

        /// <summary>清单落盘（临时文件 + 替换，原子写）。格式（v2）：# 注释 / scheme 2 头 /
        /// type &lt;枚举名&gt; &lt;下标&gt;（EffectType 冻结分配）/ 「哈希 下标」行。</summary>
        private static void SaveManifest()
        {
            try
            {
                var dir = Path.GetDirectoryName(_manifestPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                var sb = new System.Text.StringBuilder();
                sb.Append("# TideCardIndex identity manifest v2 (append-only; hash=decimal index; type=frozen EffectType index)\n");
                sb.Append("scheme 2\n");
                var typeRows = new List<KeyValuePair<string, int>>(CardIdentityService.TypeIndexSnapshot());
                typeRows.Sort((a, b) => a.Value.CompareTo(b.Value)); // 按下标序落盘（追加史可读）
                foreach (var kv in typeRows)
                    sb.Append("type ").Append(kv.Key).Append(' ').Append(kv.Value).Append('\n');
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
