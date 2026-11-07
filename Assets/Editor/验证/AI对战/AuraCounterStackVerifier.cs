using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CardCore;
using CardCore.Attribute;
using UnityEditor;
using UnityEngine;

namespace CardCore.Editor.Tests
{
    /// <summary>
    /// 指示物 × 光环叠算端到端（2026-11-07 用户定案场景）：
    /// 一个基础 1 攻的生物，吃：
    ///   ① 两层「攻击力减少」指示物（每层 −1，写 _power 基值）；
    ///   ② 一个「攻击力增加 2」的连接光环（箭头指向该生物，LinkAuraSystem live-query +2）；
    ///   ③ 一个「全场生物攻击力 −1」的场地光环（对方场地光环，条目 scope=3 对方生物，−1）。
    /// 期望结算：1 − 2 + 2 − 1 = **0**（攻击力 0，不能宣言攻击）。
    /// 读数链：EntityEffectExtensions.GetPower（= _power + 连接/场地光环加成）与
    /// LayerEngine.CalculatePower（战斗/SBA 同源）都为 0——两处同源，避免只测 UI 读数。
    /// 入口：菜单 验证/指示物×光环叠算（E2E）；Run() 返回报告文本。
    /// </summary>
    public static class AuraCounterStackVerifier
    {
        private const string CreatureName = "探针·1攻生物";
        private const string LinkerName = "探针·攻击力增加2光环源";
        private const string FieldName = "探针·全场攻击力-1场地光环";

        [MenuItem("Tools/验证/指示物×光环叠算（E2E）")]
        public static void RunFromMenu() => Debug.Log(Run());

        public static string Run()
        {
            var log = new StringBuilder();
            var checks = new List<(bool Pass, string Text)>();
            void Check(bool ok, string text)
            {
                checks.Add((ok, text));
                if (!ok) log.AppendLine("[FAIL] " + text);
            }

            GameBoard.BoardState board = null;
            try
            {
                var core = GameCore.Instance;
                if (core == null) throw new InvalidOperationException("GameCore.Instance 为空（编辑器域未初始化）");

                // ---- 夹具：内存合成三张卡（不落盘） ----
                var creatureData = new CardData
                {
                    ID = "VERIFY_STACK_CREATURE", CardName = CreatureName,
                    Supertype = Cardtype.Creature, Power = 1, Life = 10,
                };
                var linkerData = new CardData
                {
                    ID = "VERIFY_STACK_LINKER", CardName = LinkerName,
                    Supertype = Cardtype.Creature, Power = 1, Life = 10,
                };
                var fieldData = new CardData
                {
                    ID = "VERIFY_STACK_FIELD", CardName = FieldName,
                    Supertype = Cardtype.Enchantment, Life = 10,
                };

                var deck1 = new List<CardData>();
                var deck2 = new List<CardData>();
                for (int i = 0; i < 6; i++)
                {
                    deck1.Add(creatureData); deck1.Add(linkerData); deck1.Add(fieldData);
                    deck2.Add(creatureData); deck2.Add(fieldData);
                }

                core.InitGame(CardLoader.BuildDeck(deck1, 1), CardLoader.BuildDeck(deck2, 1), rngSeed: 20261107);
                var p1 = core.Player1;
                var p2 = core.Player2;
                p1.IsAI = false;
                p2.IsAI = false;

                board = new GameBoard.BoardState(core, p1, p2, GameBoard.HalfFieldData.Flat(), GameBoard.HalfFieldData.Flat());
                board.EnableAutoResync();
                GameBoard.LinkAuraSystem.Attach(board);
                board.Resync();

                var zm = core.ZoneManager;
                var creature = PlaceFromDeck(zm, p1, CreatureName);
                var linker = PlaceFromDeck(zm, p1, LinkerName);
                var field = PlaceFromDeck(zm, p2, FieldName);
                board.Resync();
                Check(creature != null && linker != null && field != null,
                    "夹具：1攻生物/光环源（己方战场）+ 场地光环（对方战场）三卡就位");
                if (creature == null || linker == null || field == null) return Finish(log, checks);

                // —— 基线：1 攻（此时无任何光环/指示物） ——
                Check(creature.GetPower() == 1, $"基线：生物 GetPower 期望 1，实际 {creature.GetPower()}");

                // ---- ② 连接光环几何前提：光环源与受益者相邻（箭头由下面 BindLinkAura 建立） ----
                var arrow = ArrowTo(board, linker, creature);
                Check(arrow != HexDirection.None, "连接光环：光环源与受益者相邻（可建箭头）");

                // ---- ① 两层攻击力减少：写 _power 基值（1 → −1；此时连接光环尚未挂上） ----
                int before = creature.GetPower();
                CounterRules.AddStatCounter(creature, CounterRules.PowerDownCounter, 2, p1);
                GameBoard.LinkAuraSystem.InvalidateCache();
                int afterCounters = creature.GetPower();
                Check(creature.GetCounterCount(CounterRules.PowerDownCounter) == 2,
                    $"指示物：期望 2 层攻击力减少，实际 {creature.GetCounterCount(CounterRules.PowerDownCounter)} 层");
                Check(afterCounters == -1 && before == 1,
                    $"指示物结算：1 − 2 = −1（基值允许为负，光环前读 −1），实际 {before} → {afterCounters}");

                // ---- ② 连接光环 +2 并入读数（−1 + 2 = 1） ----
                var linkerCard = DataOf(linker);
                linkerCard.LinkAuras.Add(new LinkAuraData { stat = "Power", value = 2 });
                linkerCard.ArrowDirections = arrow;
                GameBoard.LinkAuraSystem.InvalidateCache();
                int afterLink = creature.GetPower();
                Check(afterLink == 1,
                    $"连接光环结算：−1 + 2 = 1，实际 {afterLink}");

                // ---- ③ 场地光环 −1（对方场地光环 scope=3，全场对方生物；1 − 1 = 0） ----
                var fieldCard = DataOf(field);
                fieldCard.LinkAuras.Add(new LinkAuraData { stat = "Power", value = -1, scope = 3 });
                GameBoard.LinkAuraSystem.InvalidateCache();
                int finalPower = creature.GetPower();
                int battlePower = core.LayerEngine.CalculatePower(creature);
                Check(finalPower == 0,
                    $"最终结算：1 − 2 + 2 − 1 = 0，实际 {finalPower}");
                Check(battlePower == 0,
                    $"战斗读数同源：LayerEngine.CalculatePower 期望 0，实际 {battlePower}");

                // ---- 来源自身不吃自己的光环（连接光环单向） ----
                Check(linker.GetPower() == 1,
                    $"连接光环单向：光环源自身不吃自己的 +2（期望 1），实际 {linker.GetPower()}");

                log.AppendLine($"[叠算] 基线 1；指示物 2 层 → {afterCounters}；连接光环 +2 → {afterLink}；"
                              + $"场地光环 −1 → {finalPower}（战斗读数 {battlePower}）");
            }
            catch (Exception ex)
            {
                checks.Add((false, $"异常中断：{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"));
                log.AppendLine("[FAIL] 异常中断：" + ex);
            }
            finally
            {
                GameBoard.LinkAuraSystem.Detach();
                board?.Dispose();
            }

            return Finish(log, checks);
        }

        private static string Finish(StringBuilder log, List<(bool Pass, string Text)> checks)
        {
            int fails = checks.Count(c => !c.Pass);
            log.AppendLine(fails == 0
                ? $"==== AuraCounterStackVerifier 全过（{checks.Count} 项断言）===="
                : $"==== AuraCounterStackVerifier 失败 {fails}/{checks.Count} 项 ====");
            return log.ToString();
        }

        // ======================================== 夹具辅助 ========================================

        private static CardData DataOf(Card c) => (c as CardWrapper)?.GetData();

        /// <summary>从牌库/手牌找首张同名卡，移出原区后走进场统一出口（发入场事件/容量闸）。</summary>
        private static Card PlaceFromDeck(ZoneManager zm, Player owner, string name)
        {
            var card = zm.GetCards(owner, Zone.Hand).FirstOrDefault(c => IsName(c, name))
                       ?? zm.GetCards(owner, Zone.Deck).FirstOrDefault(c => IsName(c, name));
            if (card == null) return null;
            var from = zm.IsCardInZone(card, owner, Zone.Hand) ? Zone.Hand : Zone.Deck;
            zm.GetZoneContainer(owner).Remove(card, from);
            return zm.TryAddToBattlefield(card, owner) ? card : null;
        }

        /// <summary>光环源 → 受益者的箭头方向（BoardMath 映射同连接光环几何）。</summary>
        private static HexDirection ArrowTo(GameBoard.BoardState board, Card from, Card to)
        {
            if (!board.TryGetCell(from, out int fx, out int fz)) return HexDirection.None;
            if (!board.TryGetCell(to, out int tx, out int tz)) return HexDirection.None;
            foreach (GameBoard.BoardDirection d in Enum.GetValues(typeof(GameBoard.BoardDirection)))
            {
                var (nx, nz) = GameBoard.BoardMath.Neighbor(fx, fz, d);
                if (nx == tx && nz == tz) return GameBoard.BoardMath.ArrowOf(d);
            }
            return HexDirection.None;
        }

        private static bool IsName(Card c, string name)
            => c is IHasName n && n.CardName == name;
    }
}
