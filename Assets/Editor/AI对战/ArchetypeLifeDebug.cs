using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CardCore;
using UnityEngine;
using UnityEditor;

namespace CardCore.Editor.Tests
{
    /// <summary>
    /// 地牌消失复现调试器（2026-09-20）：逐步复刻 TestLandEconomy 的 T1→T3 流程，
    /// 每步把 p1/p2 元素池（张数/指示物/横置态）与墓地写入 Logs/LandDebug_trace.txt
    /// （双写 Debug.Log——文件不受控制台过滤器影响）。
    /// 入口：菜单 Tools/AI 自动对战（地牌调试），或 batchmode
    /// -executeMethod CardCore.Editor.Tests.ArchetypeLifeDebug.RunBatch -logFile ...
    /// </summary>
    public static class ArchetypeLifeDebug
    {
        [MenuItem("Tools/AI 自动对战（地牌调试）")]
        public static void RunFromMenu() => RunBatch();

        public static void RunBatch()
        {
            Directory.SetCurrentDirectory(Directory.GetParent(Application.dataPath).FullName);
            var cards = CardLoader.LoadCardsFromText(@"{
  ""cards"": [
    { ""cardName"": ""火球术"", ""supertype"": ""Spell"",
      ""costList"": [ { ""manaType"": 1, ""amount"": 3.0 }, { ""manaType"": 2, ""amount"": 2.0 } ],
      ""keywords"": [], ""effects"": [
        { ""Id"": ""FIREBALL_MAIN"", ""TriggerTiming"": 0, ""SelectionMode"": 0, ""TargetCount"": 1,
          ""AtomicEffects"": [ { ""refId"": ""a4b823fc"", ""value"": 4, ""kinds"": [2] } ] },
        { ""Id"": ""FIREBALL_DRAW"", ""TriggerTiming"": 0, ""SelectionMode"": -1,
          ""AtomicEffects"": [ { ""refId"": ""120ad4d1"", ""value"": 1 } ] } ] },
    { ""cardName"": ""古树守卫"", ""supertype"": ""Creature"", ""power"": 1, ""life"": 1,
      ""costList"": [ { ""manaType"": 0, ""amount"": 1.0 } ], ""keywords"": [], ""effects"": [] },
    { ""cardName"": ""灰色哨兵"", ""supertype"": ""Creature"", ""power"": 1, ""life"": 1,
      ""costList"": [ { ""manaType"": 0, ""amount"": 1.0 } ], ""keywords"": [], ""effects"": [] },
    { ""cardName"": ""光环测试"", ""supertype"": ""Creature"", ""power"": 1, ""life"": 1,
      ""costList"": [ { ""manaType"": 0, ""amount"": 3.0 }, { ""manaType"": 4, ""amount"": 1.0 } ],
      ""arrows"": ""Up,LowerRight"",
      ""linkAuras"": [ { ""stat"": ""Power"", ""value"": 2 }, { ""keyword"": ""Taunt"" } ],
      ""keywords"": [], ""effects"": [] }
  ]
}");
            var core = GameCore.Instance;
            core.InitGame(CardLoader.BuildDeck(cards, 1), CardLoader.BuildDeck(cards, 1));
            core.Player1.IsAI = true;
            core.Player2.IsAI = true;

            var p1 = core.Player1;
            var p2 = core.Player2;
            var sb = new StringBuilder();
            void Log(string step)
            {
                var line = $"[LandDebug] {step} | p1={Snap(core, p1)} p2={Snap(core, p2)}";
                Debug.Log(line);
                sb.AppendLine(line);
            }
            void Trace(string line)
            {
                Debug.Log(line);
                sb.AppendLine(line);
            }

            Log("开局");
            GameActions.SkipElementPool(core, p1); // Standby → Main（T1 主阶段才能放地/横置）
            var hand1 = new List<Card>(core.ZoneManager.GetCards(p1, Zone.Hand));
            Trace($"[LandDebug] p1 手牌：{string.Join(",", hand1.Select(c => $"{c.ID}({Total(c)})"))}");
            var creatures = hand1.Where(CardCore.ElementPoolSystem.CanServeAsLand).ToList();
            var land1 = creatures.FirstOrDefault(c => Total(c) >= 2) ?? creatures.FirstOrDefault();
            Trace($"[LandDebug] 选地：{land1?.ID}（{Total(land1)}）");
            Log("放地前");
            if (!GameActions.AddToElementPool(core, p1, land1)) Trace("[LandDebug] !! 放地失败");
            Log("放地后");
            var pc1 = core.ElementPool.GetPooledCards(p1).FirstOrDefault();
            var colors = pc1?.GetAvailableColors() ?? new List<ManaType>();
            if (pc1 != null && !GameActions.GainElementFromToken(core, p1, pc1, colors[0]))
                Trace("[LandDebug] !! 手动产出失败");
            Log("手动产出后");

            // 复刻验证器 T1 的费用门槛段：bank 灌满后尝试打出“总费>1”的卡（候选可能正是已入池的地）
            var pool1 = core.ElementPool.GetPool(p1);
            foreach (ManaType t in Enum.GetValues(typeof(ManaType)))
                pool1.AvailableMana[t] = 99;
            var candidate = hand1.Skip(1).FirstOrDefault(c => Total(c) > 1);
            if (candidate != null)
            {
                bool played = GameActions.PlayCard(core, p1, candidate);
                Trace($"[LandDebug] 费用门槛尝试打出 {candidate.ID}（{Total(candidate)}）→ {played}");
                GameActions.DrainStack(core);
            }
            else Trace("[LandDebug] 无 >1 费候选");
            Log("费用门槛段后");

            if (!GameActions.EndTurn(core, p1)) Trace("[LandDebug] !! EndTurn(p1) 失败");
            Log("EndTurn(p1) 后");
            core.TurnEngine.CheckPhaseTransition();
            Log("折返（进入 T2）后");

            // 完整复刻验证器 T2：p2 放 1 费地→产出耗尽→进墓→从手牌补充
            GameActions.SkipElementPool(core, p2);
            var hand2 = new List<Card>(core.ZoneManager.GetCards(p2, Zone.Hand));
            var oneCost = hand2.FirstOrDefault(c => Total(c) == 1 && CardCore.ElementPoolSystem.CanServeAsLand(c));
            if (oneCost == null)
            {
                var grayData = cards.FirstOrDefault(c => c.CardName == "灰色哨兵");
                if (grayData != null)
                {
                    oneCost = new CardWrapper(grayData);
                    oneCost.SetController(p2);
                    core.ZoneManager.GetZoneContainer(p2).Add(oneCost, Zone.Hand);
                    Trace("[LandDebug] 注入灰色哨兵到 p2 手牌");
                }
            }
            if (oneCost != null)
            {
                Trace($"[LandDebug] p2 放地 {oneCost.ID} → {GameActions.AddToElementPool(core, p2, oneCost)}");
                var pc2 = core.ElementPool.GetPooledCards(p2).FirstOrDefault();
                if (pc2 != null)
                {
                    var color2 = pc2.GetAvailableColors()[0];
                    Trace($"[LandDebug] p2 产出 → {GameActions.GainElementFromToken(core, p2, pc2, color2)}");
                }
                Trace($"[LandDebug] p2 耗尽后 {Snap(core, p2)}");
                var hand2b = new List<Card>(core.ZoneManager.GetCards(p2, Zone.Hand));
                foreach (var cand in hand2b)
                    if (GameActions.AddToElementPool(core, p2, cand)) { Trace($"[LandDebug] p2 补充地牌 {cand.ID}"); break; }
            }
            Log("T2 p2 放地后");
            if (!GameActions.EndTurn(core, p2)) Trace("[LandDebug] !! EndTurn(p2) 失败");
            Log("EndTurn(p2) 后");
            core.TurnEngine.CheckPhaseTransition();
            Log("折返（进入 T3）后");
            GameActions.SkipElementPool(core, p1);
            Log("T3 SkipElementPool(p1) 后");
            Trace($"[LandDebug] 终态：cap(p1)={core.ElementPool.GetLandCap(p1)} | {Snap(core, p1)}");

            Directory.CreateDirectory("Logs");
            File.WriteAllText(Path.Combine("Logs", "LandDebug_trace.txt"), sb.ToString(), new UTF8Encoding(false));
            Debug.Log($"[LandDebug] trace 写入 Logs/LandDebug_trace.txt（{sb.Length} 字符）");
            Quit();
        }

        private static int Total(Card c)
            => c is IHasCost hasCost && hasCost.Cost != null ? (int)hasCost.Cost.Values.Sum() : 1;

        private static string Snap(GameCore core, Player p)
        {
            var pooled = core.ElementPool.GetPooledCards(p);
            var parts = pooled.Select(l =>
            {
                var tokens = string.Join(",", l.Tokens.Select(kv => $"{kv.Key}:{kv.Value}"));
                return $"{l.SourceCard.ID}[{tokens}]{(l.IsTapped ? "·横置" : "")}";
            });
            var gy = core.ZoneManager.GetCards(p, Zone.Graveyard).Select(c => c.ID);
            return $"池({pooled.Count}):{string.Join("|", parts)} 墓地:{string.Join(",", gy)}";
        }

        private static void Quit()
        {
            if (Environment.GetCommandLineArgs().Contains("-batchmode"))
                EditorApplication.Exit(0);
        }
    }
}
