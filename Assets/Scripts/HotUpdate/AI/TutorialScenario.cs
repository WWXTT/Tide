using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using GameBoard;

namespace SynergyUI
{
    /// <summary>
    /// 教学预设场面（2026-10-06 教学直入定案）：教学不再从空场第 1 回合开始，直接站在
    /// 「教学目的的操作环节」——中期回合、双方生命/地牌/单位/手牌/牌库已就位的场面。
    /// 挂在 TutorialConfig.scenario（null=旧口径从第 1 回合正常开局，完全兼容）。
    /// 数据口径：所有卡 id 在本条目内同侧不重复（教学卡组每卡 1 张）；deck 列表序=剩余牌库序
    /// （牌库顶在前）；bank 数组下标=ManaType 枚举序 [灰,红,蓝,绿,白,黑]。
    /// </summary>
    [Serializable]
    public class TutorialScenario
    {
        /// <summary>起跳全局回合号（≥1；开局首个 TurnStartEvent 携带该值，回合数直接钉到 N）。</summary>
        public int startTurn = 1;

        /// <summary>起跳回合归属："P1"（缺省）/"P2"——该玩家直接站在自己起跳回合的主阶段。</summary>
        public string firstPlayer;

        /// <summary>玩家（P1）侧初始状态。</summary>
        public TutorialSideState player;

        /// <summary>机器人（P2）侧初始状态。</summary>
        public TutorialSideState ai;

        /// <summary>起跳回合是否归属 P1（firstPlayer 缺省/非法均按 P1）。</summary>
        public bool FirstIsPlayer1
            => !string.Equals(firstPlayer, "P2", StringComparison.OrdinalIgnoreCase);

        /// <summary>一侧的全部卡 id 并集（InitGame 组卡用）：手牌+地牌+单位+墓地+剩余牌库。
        /// 顺序无语义——注入器按 id 从牌库取卡分配，剩余牌库保持 deck 列表自身顺序。</summary>
        public static List<string> DeckIdsOf(TutorialSideState side)
        {
            var ids = new List<string>();
            if (side == null) return ids;
            ids.AddRange(side.hand ?? new List<string>());
            ids.AddRange((side.lands ?? new List<TutorialLandSpec>()).Select(l => l?.card).Where(c => c != null));
            ids.AddRange((side.units ?? new List<TutorialUnitSpec>()).Select(u => u?.card).Where(c => c != null));
            ids.AddRange(side.grave ?? new List<string>());
            ids.AddRange(side.deck ?? new List<string>());
            return ids;
        }
    }

    /// <summary>教学预设场面·单侧初始状态（缺省值=引擎常规开局口径）。</summary>
    [Serializable]
    public class TutorialSideState
    {
        /// <summary>生命（同时写上限，ResetVitals 口径）。缺省 30=GameCore.InitialLife。</summary>
        public int life = 30;

        /// <summary>已疲劳次数（缺省 0）。</summary>
        public int fatigue;

        /// <summary>地牌槽上限加值（2026-10-16 测试开局功能）：成长口径 LandCapBonus 直加——
        /// 自然曲线封顶外的增加走此口（入池闸/支付浓度/黑白生成钳随 GetLandCap 同步）。
        /// 缺省 0=现有教学配置零影响。</summary>
        public int landCapBonus;

        /// <summary>元素 bank（AvailableMana）[灰,红,蓝,绿,白,黑]，缺省全 0。</summary>
        public int[] bank;

        /// <summary>场上地牌（元素池）。</summary>
        public List<TutorialLandSpec> lands;

        /// <summary>场上单位。</summary>
        public List<TutorialUnitSpec> units;

        /// <summary>手牌卡 id 序。</summary>
        public List<string> hand;

        /// <summary>剩余牌库卡 id 序（牌库顶在前；不含手牌/场面上的卡）。</summary>
        public List<string> deck;

        /// <summary>墓地卡 id。</summary>
        public List<string> grave;
    }

    /// <summary>教学预设场面·地牌：指示物缺省按卡费用构成生成（AddCardToPool 口径）。</summary>
    [Serializable]
    public class TutorialLandSpec
    {
        public string card;

        /// <summary>是否已横置（本回合已产过元素）。</summary>
        public bool tapped;

        /// <summary>已退役（2026-10-11 指示物删除）——保留字段兼容旧配置，读取忽略。</summary>
        public int tokens;

        /// <summary>锁定产色（"Red"/"Blue"/"Green"/"Gray"；空=缺省主色。2026-10-11：
        /// 原指示物颜色字段复用为锁色）。</summary>
        public string tokenType;

        /// <summary>已退役（2026-10-11：抉择卡按声明费用取色）——保留字段兼容旧配置，读取忽略。</summary>
        public int mode;
    }

    /// <summary>教学预设场面·单位：数值覆写 -1=用卡面原值；x/z -1=按区域序列自动落格。</summary>
    [Serializable]
    public class TutorialUnitSpec
    {
        public string card;

        /// <summary>攻击力覆写（-1=卡面原值）。</summary>
        public int power = -1;

        /// <summary>生命覆写（-1=卡面原值；覆写同时抬高上限，不出现生命>上限）。</summary>
        public int life = -1;

        /// <summary>是否已横置（本回合不可再攻击）。</summary>
        public bool tapped;

        /// <summary>落位格 x（己方单位区列 1..7；-1=自动）。</summary>
        public int x = -1;

        /// <summary>落位格 z（P1 行 3/4、P2 行 2/1；-1=自动）。</summary>
        public int z = -1;

        /// <summary>开局携带的指示物（key=CounterRules 登记的 id，如 RushSickness；value=层数）。</summary>
        public List<TutorialCounterSpec> counters;

        /// <summary>预置规则光环（如 "HandLimitNoFatigue"）：静默入场不走登场效果，seeder 直接
        /// RuleAuraSystem.Activate（载体=本卡）——预置仪典的正规等价路径。</summary>
        public string activateRuleAura;
    }

    /// <summary>教学预设场面·指示物（key/层数；长档紊乱等层=剩余回合的计数指示物）。</summary>
    [Serializable]
    public class TutorialCounterSpec
    {
        public string key;
        public int value = 1;
    }

    /// <summary>
    /// 教学预设场面注入器（静默重建口径，2026-10-06）：在 InitGame(deferStart=true) 之后、
    /// StartGame 之前把场面写入核心——全部走 ZoneContainer.Add/Remove（不发区域事件、不清指示物，
    /// Zones「区域真源约定」认可的快照重建口径，禁用 Move）；战场单位补 RegisterCardTriggeredEffects
    /// （静默入场不接触发式）；地牌走 ElementPool.AddCardToPool 官方路径；单位显式落位走
    /// BoardState.PinCard 落位钉子（钉格优先、其余填空）。
    /// 时序契约：先 InitGame(双方全卡并集入牌库, lockDeckOrder, skipOpeningDraw, deferStart) →
    /// AttachBoard（棋盘在场才能钉格）→ 本 Apply → core.StartGame()。被 PrepareScenarioStart
    /// 跳过的推进量（地牌槽曲线/解横置）由本注入器显式预置（GlobalTurnIndex=startTurn）。
    /// </summary>
    public static class TutorialScenarioSeeder
    {
        /// <summary>把预设场面写入核心（StartGame 前调用；board=落位钉子宿主，null 时忽略显式落位）。
        /// 卡缺失/落位非法记 Warn 跳过，不炸局。</summary>
        public static void Apply(GameCore core, TutorialScenario scenario, GameBoard.BoardState board = null)
        {
            if (core == null || scenario == null) return;
            if (scenario.startTurn < 1) scenario.startTurn = 1;

            SeedSide(core, core.Player1, scenario.player, scenario.startTurn, board);
            SeedSide(core, core.Player2, scenario.ai, scenario.startTurn, board);

            // 回合起跳预置在最后：StartGame→StartNewTurn 自增到 startTurn、跳准备阶段自动化落 Main
            var first = scenario.FirstIsPlayer1 ? core.Player1 : core.Player2;
            core.TurnEngine.PrepareScenarioStart(scenario.startTurn, first);
        }

        /// <summary>运行时归一注入（2026-10-16 教学一两段式·第二段「援军登场」）：对局进行中把局面
        /// 无视规则地重置成 scenario 口径——双方战场/地牌/手牌/牌库/墓地/除外全清（抹除原子同款
        /// 消失口径：容器直删+地牌出池+战场离场事件+CardMoveEvent To=None，不走死亡/换区结算）、
        /// 临时钱包清零、生命/疲劳按 scenario 重置；然后凭空造卡（CardCatalog 教学表→CardLoader）
        /// 入牌库，按 Apply 同款 SeedSide 静默铺场。回合不重置（对局继续，地牌曲线 GlobalTurnIndex
        /// 对齐当前全局回合）；技能卡（FieldZone）不动。调用方随后全量对账（Resync/RefreshField/RefreshFans）。</summary>
        public static void ApplyRuntime(GameCore core, TutorialScenario scenario, GameBoard.BoardState board = null)
        {
            if (core == null || scenario == null) return;

            ClearSide(core, core.Player1);
            ClearSide(core, core.Player2);

            // 凭空造卡：scenario 全部卡 id → CardData → Card 实例静默入牌库（SeedSide 从牌库取卡铺场）
            SpawnToDeck(core, core.Player1, scenario.player);
            SpawnToDeck(core, core.Player2, scenario.ai);

            SeedSide(core, core.Player1, scenario.player, core.TurnEngine.TurnNumber, board);
            SeedSide(core, core.Player2, scenario.ai, core.TurnEngine.TurnNumber, board);
        }

        /// <summary>一侧局面归零（抹除原子消失口径：不发死亡事件、不走换区清理——指示物随卡实例
        /// 一起废弃；战场单位补 CardLeaveBattlefieldEvent + CardMoveEvent To=None 供视图/守卫链收口）。</summary>
        private static void ClearSide(GameCore core, Player owner)
        {
            if (core == null || owner == null) return;
            var container = core.ZoneManager?.GetZoneContainer(owner);
            if (container == null) return;

            foreach (var zone in new[] { Zone.Battlefield, Zone.ElementPool, Zone.Hand, Zone.Deck, Zone.Graveyard, Zone.Exile })
            {
                var cards = core.ZoneManager.GetCards(owner, zone) ?? new List<Card>(); // GetCards=副本，遍历安全
                foreach (var card in cards)
                {
                    if (card == null) continue;
                    if (zone == Zone.ElementPool)
                        core.ElementPool.RemoveCardFromPool(card, owner); // 余量写回卡+清池内记录（官方出池口）
                    container.Remove(card, zone);
                    if (zone == Zone.Battlefield)
                    {
                        card.IsAlive = false;
                        core.PublishEvent(new CardLeaveBattlefieldEvent
                        {
                            Card = card,
                            Controller = owner,
                            Destination = Zone.None,
                        });
                    }
                    core.PublishEvent(new CardMoveEvent
                    {
                        MovedCard = card,
                        From = zone,
                        To = Zone.None,
                        Controller = owner,
                    });
                }
            }

            // 临时钱包清零（回合末清空同款——归一口径不留余额）
            var pool = core.ElementPool.GetPool(owner);
            if (pool != null)
                for (ManaType t = ManaType.Gray; t <= ManaType.Black; t++)
                    pool.TempElements[t] = 0;
        }

        /// <summary>凭空造卡入牌库：教学表按 id 取 CardData（缺失 Warn 跳过），CardLoader.BuildDeck
        /// 造 Card 实例（同 InitGame 口径），静默 Add 到牌库底（SeedSide 随后按 id 取出分配）。</summary>
        private static void SpawnToDeck(GameCore core, Player owner, TutorialSideState side)
        {
            var container = core.ZoneManager.GetZoneContainer(owner);
            foreach (var id in TutorialScenario.DeckIdsOf(side))
            {
                if (string.IsNullOrEmpty(id)) continue;
                var data = CardCatalog.GetById(id);
                if (data == null)
                {
                    TideLog.Warn($"[TutorialScenarioSeeder] 运行时造卡失败（卡表无此 id）：{id}（跳过）");
                    continue;
                }
                var cards = CardLoader.BuildDeck(new List<CardData> { data }, 1);
                foreach (var card in cards ?? new List<Card>())
                {
                    card.SetController(owner);
                    container.Add(card, Zone.Deck);
                }
            }
        }

        private static void SeedSide(GameCore core, Player owner, TutorialSideState side,
            int startTurn, GameBoard.BoardState board)
        {
            // 地牌槽曲线对齐：跳过的准备阶段本该推进 GlobalTurnIndex——先预置，AddCardToPool 的
            // 上限校验（2026-10-15 全局口径 CapAt(GlobalTurnIndex)）才能按起跳回合放行对应张数（双方同步值）
            var pool = core.ElementPool.GetPool(owner);
            pool.GlobalTurnIndex = startTurn;
            // 个人回合台账（纯观测，不参与上限）：起跳前已开始的回合数（P1 先手交替）P1=ceil((t-1)/2)、P2=floor((t-1)/2)
            pool.PersonalTurnIndex = owner == core.Player1
                ? (startTurn - 1 + 1) / 2
                : (startTurn - 1) / 2;

            if (side == null) return;

            // 地牌槽上限加值（2026-10-16 测试开局功能）：成长同口径直加——
            // 先于地牌入池应用，AddCardToPool 的上限校验按加值后的 GetLandCap 放行
            if (side.landCapBonus > 0)
                pool.LandCapBonus += side.landCapBonus;

            owner.ResetVitals(side.life > 0 ? side.life : GameCore.InitialLife);
            owner.FatigueCount = side.fatigue > 0 ? side.fatigue : 0;

            if (side.bank != null)
            {
                // 2026-10-11 现取现用：bank 字段预置临时钱包（效果授予元素——回合末清空）
                for (int i = 0; i < side.bank.Length && i < 6; i++)
                    pool.TempElements[(ManaType)i] = side.bank[i];
            }

            var container = core.ZoneManager.GetZoneContainer(owner);

            // 墓地 → 手牌（顺序=配置序）
            MoveByIds(core, container, owner, side.grave, Zone.Graveyard, "墓地");
            MoveByIds(core, container, owner, side.hand, Zone.Hand, "手牌");

            // 单位（配置序入场；显式落位经 BoardState 落位钉子——钉格优先、其余按列表序填空）
            foreach (var unit in side.units ?? new List<TutorialUnitSpec>())
            {
                if (unit == null || string.IsNullOrEmpty(unit.card)) continue;
                var card = TakeFromDeck(core, container, owner, unit.card);
                if (card == null)
                {
                    TideLog.Warn($"[TutorialScenarioSeeder] {owner?.Name} 单位卡不在牌库：{unit.card}（跳过）");
                    continue;
                }
                card.SetController(owner);
                container.Add(card, Zone.Battlefield);
                if (unit.power >= 0) card.SetPower(unit.power);
                if (unit.life >= 0)
                {
                    card.SetLife(unit.life);
                    if (card.GetMaxLife() < unit.life) card._maxLife = unit.life; // 覆写不产生生命>上限
                }
                if (unit.tapped) card.Tap();
                GameActions.RegisterCardTriggeredEffects(core, card, owner); // 静默入场不接触发式，显式补
                foreach (var counter in unit.counters ?? new List<TutorialCounterSpec>())
                {
                    if (counter == null || string.IsNullOrEmpty(counter.key) || counter.value == 0) continue;
                    card.AddCounters(counter.key, counter.value, owner);
                }
                if (!string.IsNullOrEmpty(unit.activateRuleAura))
                    RuleAuraSystem.Activate(unit.activateRuleAura, card, owner); // 预置仪典：静默入场不走登场效果，直接投放
                if (board != null && (unit.x >= 0 || unit.z >= 0))
                {
                    if (IsUnitCell(owner, unit.x, unit.z))
                        board.PinCard(card, unit.x, unit.z);
                    else
                        TideLog.Warn($"[TutorialScenarioSeeder] {owner?.Name} 单位 {unit.card} 落位 ({unit.x},{unit.z}) 不在己方单位区——按自动落位");
                }
            }

            // 地牌（官方入池路径：资格/各自上限；锁色随 tokenType 覆写、横置态随后覆写——
            // 2026-10-11 锁色定案：无指示物，tokenType=锁定产色、tokens 字段退役忽略）
            foreach (var land in side.lands ?? new List<TutorialLandSpec>())
            {
                if (land == null || string.IsNullOrEmpty(land.card)) continue;
                var card = TakeFromDeck(core, container, owner, land.card);
                if (card == null)
                {
                    TideLog.Warn($"[TutorialScenarioSeeder] {owner?.Name} 地牌卡不在牌库：{land.card}（跳过）");
                    continue;
                }
                card.SetController(owner);
                ManaType? lockColor = string.IsNullOrEmpty(land.tokenType)
                    ? (ManaType?)null
                    : ParseMana(land.tokenType);
                if (!core.ElementPool.AddCardToPool(card, owner, lockColor))
                {
                    TideLog.Warn($"[TutorialScenarioSeeder] {land.card} 入元素池被拒（上限/资格）——退回牌库底");
                    container.Add(card, Zone.Deck);
                    continue;
                }
                container.Add(card, Zone.ElementPool);
                core.ElementPool.PublishPoolAdd(card, owner);
                var pooled = pool.PooledCards.FirstOrDefault(pc => pc.SourceCard == card);
                if (pooled != null)
                    pooled.IsTapped = land.tapped;
            }
        }

        /// <summary>按 id 序列从牌库取卡静默放入目标区（缺失 id 记 Warn 跳过）。</summary>
        private static void MoveByIds(GameCore core, ZoneContainer container, Player owner,
            List<string> ids, Zone target, string label)
        {
            foreach (var id in ids ?? new List<string>())
            {
                var card = TakeFromDeck(core, container, owner, id);
                if (card == null)
                {
                    TideLog.Warn($"[TutorialScenarioSeeder] {owner?.Name} {label}卡不在牌库：{id}（跳过）");
                    continue;
                }
                container.Add(card, target);
            }
        }

        /// <summary>从牌库按 id 取出一张卡（同侧卡 id 不重复口径；找不到返回 null）。</summary>
        private static Card TakeFromDeck(GameCore core, ZoneContainer container, Player owner, string cardId)
        {
            var card = (core.ZoneManager.GetCards(owner, Zone.Deck) ?? new List<Card>())
                .FirstOrDefault(c => c.ID == cardId);
            if (card != null) container.Remove(card, Zone.Deck);
            return card;
        }

        /// <summary>显式落位格是否在该玩家的单位区格集内（P1 行 3/4、P2 行 2/1，列 1..7；2026-10-13 缩盘后口径）。</summary>
        private static bool IsUnitCell(Player owner, int x, int z)
        {
            var cells = BoardLayout.UnitCells(owner == GameCore.Instance.Player1 ? 0 : 1);
            foreach (var c in cells)
                if (c.x == x && c.z == z)
                    return true;
            return false;
        }

        private static ManaType ParseMana(string tokenType)
        {
            return Enum.TryParse<ManaType>(tokenType, true, out var type) ? type : ManaType.Gray;
        }
    }
}
