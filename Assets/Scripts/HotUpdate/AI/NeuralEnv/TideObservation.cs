using System;
using System.Collections.Generic;
using System.IO;
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
        // 卡特征布局（内容身份三通路，[15..] 由 CardIdentityService 推导）：
        //   [0..14]  状态特征（valid/controller/超类/力量/生命/费用/横置/关键词/指示物…实时值）
        //   [15..20] 原子精确内容哈希下标（跨效果按执行序展平，超出 6 个截断；参数敏感）
        //   [21]     组合结构哈希下标（时点/条件/代价/Steps 骨架 =「怎么组合」）
        //   [22]     关键词/tag/光环组合哈希下标
        //   [23..28] 原子 EffectType 下标（原子表内排名，参数无关）——「造成4伤」与「造成5伤」同行
        //   [29..64] 原子参数浮点块（6 槽 × CardIdentityService.AtomParamDim 维）——数值插值通路
        // 精确哈希 = 冻结的查表身份；类型 + 参数 = 泛化通路（新参数值 = 已训类型行 + 已训线性投影，
        // 不再是纯新 token）。0 = 无该成分 / token / 未登记。属性不在此（[3][4][5] 实时特征已覆盖）。
        public const int NCard = 65; // 2026-09-10 目标域模型：29 + 6槽 × AtomParamDim(6)；python 侧 N_CARD_FEATURES 同步（另一台机器）
        public const int AtomIdSlots = 6;                                        // [15..20] 精确哈希槽
        public const int TypeIdStart = 23;                                       // [23..28] 类型下标槽
        public const int ParamStart = 29;                                        // [29..] 参数块
        // 2026-09-14 32→36：追加 4 维黑白每回合获得余量（双方×两色，见 g[32..35]）——黑白万用化后
        // 这是 policy 估值错边收益的必要状态。2026-10-04 封顶改口径：余量=地牌槽上限-已获得
        //（原封顶 1/色 → 地牌槽上限/色），取值域 0/1 → 0..9，已训模型该维分布漂移（随下批重训吸收）。
        // python 侧 N_GLOBAL_FEATURES 同步；ONNX 图输入随之重导出（随本批重训一起做）。
        // 2026-09-30 36→49（动作空间 v2 契约第 3 节）：g[36..43] 双方英雄技能块（P1/P2 绝对序）、
        // g[44..46] 栈深度/栈顶来源/栈顶 effectIdentity、g[47] 决策上下文（0=己方Main/1=响应窗口）、
        // g[48] 决策座次是否回合玩家。python 侧 N_GLOBAL_FEATURES 同步；旧 checkpoint/fixture 作废。
        public const int NGlobal = 49;
        public const int NAction = 8; // 动作特征槽：valid/type/sourceIndex/targetIndex/modeIndex/actionCost/effectIdentity/targetKind

        public float[] Cards = new float[MaxCardsTotal * NCard];
        public float[] Globals = new float[NGlobal];

        private Player _me;

        /// <summary>从核心构建快照（me = 当前回合玩家；Main 决策点视角——v2 视角参数化的兼容入口）。</summary>
        public static TideObservation Build(GameCore core)
            => Build(core, core?.TurnEngine?.TurnPlayer, false);

        /// <summary>从核心构建快照（v2 视角参数化，2026-09-30 契约）：seat = 当前决策座次——
        /// Main 决策点 = 回合玩家；响应窗口停靠 = 优先权持有方（可能是非回合玩家）。
        /// 隐藏信息（对方手牌）按该 seat 的对手计算；g[0..35] 全部相对 seat。
        /// responseContext：停靠类型（g[47]，0=己方 Main / 1=响应窗口）。</summary>
        public static TideObservation Build(GameCore core, Player seat, bool responseContext = false)
        {
            var obs = new TideObservation();
            if (core == null || core.TurnEngine == null || seat == null)
                return obs;
            var opp = seat.Opponent;
            if (opp == null) return obs;
            obs._me = seat;

            obs.WritePlayer(core, seat, 0, concealHand: false);
            obs.WritePlayer(core, opp, MaxCardsPerPlayer, concealHand: true);
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

        /// <summary>卡实例 → cards_ 槽位下标（v2）：动作行目标/栈顶来源的反查——扫描双方四个已编码区
        /// （未编码区如 FieldZone/Deck/Activation 返回 -1）。仅枚举/观测辅助用，非热路径权威。</summary>
        public static int IndexOfCard(GameCore core, Player me, Card c)
        {
            if (core?.ZoneManager == null || me == null || c == null) return -1;
            int slot = TryZoneSlot(core.ZoneManager, me, c, 0);
            if (slot >= 0) return slot;
            var opp = me.Opponent;
            return opp != null ? TryZoneSlot(core.ZoneManager, opp, c, MaxCardsPerPlayer) : -1;
        }

        private static readonly Zone[] EncodedZones = { Zone.Battlefield, Zone.ElementPool, Zone.Hand, Zone.Graveyard };

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
            float cost = GameActions.GetCardCost(c).Total;

            Cards[b + 0] = 1f; // valid
            Cards[b + 1] = ReferenceEquals(c.GetController(), _me) ? 0f : 1f; // controller
            Cards[b + 2] = c is IHasSupertype s ? (int)s.Supertype : 0f;       // supertype
            Cards[b + 3] = Clamp(power, -99f, 999f);                          // 结算力量
            Cards[b + 4] = Clamp(life, -99f, 999f);                           // 结算防御/生命
            Cards[b + 5] = Clamp(cost, 0f, 99f);                              // 声明费用和
            Cards[b + 6] = c.IsTapped() ? 1f : 0f;
            Cards[b + 7] = (!c.IsTapped() && power > 0) ? 1f : 0f;           // 可攻击粗判
            Cards[b + 8] = KeywordRules.HasRushSickness(c) ? 1f : 0f;        // 召唤失调
            Cards[b + 9] = c.GetCounterCount(CounterRules.ToxinCounter);   // 毒素层数（剧毒已转关键词 Venom，指示物取消）
            Cards[b + 10] = c.GetCounterCount(CounterRules.SilenceCounter) > 0 ? 1f : 0f;
            Cards[b + 11] = c.GetCounterCount(CounterRules.CostUpCounter)
                          - c.GetCounterCount(CounterRules.CostDownCounter); // 费用层净量
            Cards[b + 12] = c.HasKeyword(KeywordRules.Taunt) ? 1f : 0f;
            Cards[b + 13] = (c.GetCounterCount(CounterRules.StealthCounter) > 0 || c.HasKeyword(KeywordRules.Concealed)) ? 1f : 0f; // 潜行已指示物化
            Cards[b + 14] = c.HasKeyword(DeathRules.DivineProtection) ? 1f : 0f;
            // 内容身份（原子级拆散，CardIdentityService 与费用同管线推导），三通路：
            // 1) 精确哈希 [15..20] 原子 + [21] 结构 + [22] 组合——内容寻址查表，同单元跨卡共享行；
            // 2) 类型下标 [23..28]（参数无关，同 EffectType 共享行）；
            // 3) 参数块 [29..]（每原子 AtomParamDim 维浮点，线性投影读幅值）。
            // 新参数值的原子：通路 1 查不到记 0（无信号），通路 2+3 从已训类型行 + 已训投影插值——
            // 不再是纯新 token。0 = 无该成分 / token / 未登记。属性不在此（第 3/4/5 维实时特征已覆盖）。
            var data = c is CardWrapper wrapper ? wrapper.GetData() : null;
            var atomHashes = data?.AtomContentHashes;
            for (int e = 0; e < AtomIdSlots && e < (atomHashes?.Length ?? 0); e++)
                Cards[b + 15 + e] = TideCardIndex.IndexOf(atomHashes[e]);
            Cards[b + 21] = TideCardIndex.IndexOf(data != null ? data.StructureContentHash : 0UL);
            Cards[b + 22] = TideCardIndex.IndexOf(data != null ? data.CompositionContentHash : 0UL);
            var typeIdx = data?.AtomTypeIndexes;
            for (int e = 0; e < AtomIdSlots && e < (typeIdx?.Length ?? 0); e++)
                Cards[b + TypeIdStart + e] = typeIdx[e];
            var atomParams = data?.AtomParams;
            int dim = CardIdentityService.AtomParamDim;
            int nParams = Math.Min(AtomIdSlots, (atomParams?.Length ?? 0) / dim);
            for (int e = 0; e < nParams; e++)
                for (int p = 0; p < dim; p++)
                    Cards[b + ParamStart + e * dim + p] = atomParams[e * dim + p];
        }

        private void WriteGlobals(GameCore core, Player me, Player opp, bool responseContext)
        {
            var g = Globals;
            var zm = core.ZoneManager;
            var ep = core.ElementPool;

            g[0] = me.GetLife();
            g[1] = opp.GetLife();
            g[2] = core.TurnEngine.TurnNumber;
            g[3] = (int)(core.TurnEngine.CurrentPhase?.Phase ?? PhaseType.Standby); // 0/1/2
            g[4] = ReferenceEquals(me, core.TurnEngine.TurnPlayer) ? 1f : 0f; // is_my_turn（v2 视角参数化：响应窗口停靠时可为 0）
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
            g[27] = core.StackEngine.IsEmpty ? 0f : 1f; // 栈占用（二值）
            g[28] = FieldValueReward.TotalValue(core, me);  // 己方全资源价值（战场 + 生命 + 法力 + 地牌 + 手牌）
            g[29] = FieldValueReward.TotalValue(core, opp); // 对方全资源价值
            g[30] = ep.GetLandCap(me);
            g[31] = ep.GetLandCap(opp);
            // 黑白本回合已获得台账（2026-10-04 使用侧定案：产出钳退役——获得不再封顶，
            // 约束在使用侧=支付单次贡献≤地牌上限；此槽从「获得余量」改为「已获得量」观测）
            g[32] = ep.GetPool(me).BlackGainedThisTurn;
            g[33] = ep.GetPool(me).WhiteGainedThisTurn;
            g[34] = ep.GetPool(opp).BlackGainedThisTurn;
            g[35] = ep.GetPool(opp).WhiteGainedThisTurn;

            // ---- v2 追加（2026-09-30 契约第 3 节）----
            WriteHeroSkillBlock(core, core.Player1, 36);
            WriteHeroSkillBlock(core, core.Player2, 40);

            var stack = core.StackEngine.GetStackContents(); // Reverse 序（栈顶在末位）
            g[44] = Math.Min(stack.Count, 16) / 16f;          // 栈深度（归一化 /16）
            var top = stack.Count > 0 ? stack[stack.Count - 1] : null;
            g[45] = top?.Source is Card topCard ? Math.Max(0, IndexOfCard(core, me, topCard)) / 80f : 0f;
            g[46] = top?.Definition != null
                ? TideCardIndex.IndexOfEffectDefinition(top.Definition) / 1024f
                : 0f; // 同动作特征 [6] 口径（cast/攻击/SBA 栈顶无效果定义 = 0）
            g[47] = responseContext ? 1f : 0f;                // 决策上下文（0=己方 Main / 1=响应窗口）
            g[48] = ReferenceEquals(me, core.TurnEngine.TurnPlayer) ? 1f : 0f; // 决策座次是否回合玩家
        }

        /// <summary>英雄技能 4 维块（契约 g[36..39]=P1 / g[40..43]=P2，绝对座次序）：
        /// [hasSkill, 本回合已用(tapped), 累计使用/10, 预留(恒0)]。卡牌化（2026-10-07）：
        /// hasSkill=技能卡在场；升级维随旧升级机制退役恒 0（张量形状不变，Python 契约不动）。</summary>
        private void WriteHeroSkillBlock(GameCore core, Player p, int b)
        {
            var g = Globals;
            var card = p != null ? HeroSkillSystem.ResolveSkillCard(core, p) : null;
            bool has = card != null;
            g[b] = has ? 1f : 0f;
            g[b + 1] = has && card.IsTapped() ? 1f : 0f; // 本回合已用（回合开始重置）
            g[b + 2] = has ? HeroSkillSystem.GetTotalUses(core, p) / 10f : 0f;
            g[b + 3] = 0f;
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

        /// <summary>效果定义 → 身份下标（v2 动作特征 [6] / globals g[46] 的 effectIdentity）：
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
