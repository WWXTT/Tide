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
    /// 快攻 vs 慢速 主题卡组批量对战（2026-09-20）。两种入口：
    /// - 编辑器菜单 Tools/AI 自动对战（快攻vs慢速）：同步跑默认局数并写报告；
    /// - batchmode TCP（借鉴 TideHeadlessServer 训练桥接的调用方式）：
    ///   -executeMethod CardCore.Editor.Tests.ArchetypeBattleServer.Main -tidePort N -logFile ...
    ///   JSON 行协议（单客户端）：{"op":"ping"}→{"op":"pong"}；
    ///   {"op":"battle","games":N,"maxTurns":M}→每局完成即流式回一行 {"op":"game",...}，最后 {"op":"summary",...}；
    ///   {"op":"quit"} 或断开 → 服务器退出。战报/汇总报告仍落盘 Logs/。
    /// 复用 AiBattleDriver 的整局驱动与 MatchLog 战报落盘，不重写对战循环。
    /// </summary>
    public static class ArchetypeBattle
    {
        public const int DefaultGames = 12;
        public const int DefaultMaxTurns = 150;
        public const string AggroName = "赤红疾袭（快攻）";
        public const string ControlName = "苍蓝壁垒（慢速）";
        public const string AggroKey = "aggro";
        public const string ControlKey = "control";

        [MenuItem("Tools/AI 自动对战（快攻vs慢速）")]
        public static void RunFromMenu()
        {
            PinProjectRoot();
            var decks = LoadDecks();
            if (decks == null) return;
            var outcomes = RunBattles(decks.Value.aggro, decks.Value.control, DefaultGames, DefaultMaxTurns,
                o => Debug.Log($"[快攻vs慢速] 第 {o.Index}/{DefaultGames} 局：{WinnerText(o)}（{o.Reason}，{o.Turns} 回合，错误 {o.Errors.Count}）"));
            WriteReport(decks.Value.aggro, decks.Value.control, outcomes, DefaultGames, DefaultMaxTurns);
        }

        // ===================================================== 对局核心（双入口共用） =====================================================

        public sealed class GameOutcome
        {
            public int Index;
            public bool AggroIsP1;
            public bool Completed;
            public bool TurnLimitReached;
            public string WinnerKey; // AggroKey / ControlKey / "none"
            public string Reason;
            public int Turns;
            public List<string> Errors = new List<string>();
        }

        /// <summary>跑 N 局换边对战（奇数局快攻 P1），每局完成即回调 emit（TCP 流式/菜单日志共用）。</summary>
        public static List<GameOutcome> RunBattles(List<CardData> aggro, List<CardData> control,
            int games, int maxTurns, Action<GameOutcome> emit)
        {
            var driver = new AiBattleDriver();
            var outcomes = new List<GameOutcome>();
            for (int i = 0; i < games; i++)
            {
                bool aggroIsP1 = i % 2 == 0;
                var deck1 = aggroIsP1 ? aggro : control;
                var deck2 = aggroIsP1 ? control : aggro;
                var r = driver.RunFullGame(deck1, deck2, maxTurns);

                var o = new GameOutcome
                {
                    Index = i + 1,
                    AggroIsP1 = aggroIsP1,
                    Completed = r.Completed && r.Winner != null,
                    TurnLimitReached = r.TurnLimitReached,
                    Reason = r.Reason ?? "-",
                    Turns = r.TotalTurns,
                    Errors = new List<string>(r.Errors),
                };
                if (o.Completed)
                {
                    bool winnerIsP1 = ReferenceEquals(r.Winner, GameCore.Instance.Player1);
                    o.WinnerKey = (winnerIsP1 == aggroIsP1) ? AggroKey : ControlKey;
                }
                else o.WinnerKey = "none";
                outcomes.Add(o);
                emit?.Invoke(o);
            }
            return outcomes;
        }

        public static string WinnerText(GameOutcome o)
            => o.WinnerKey == AggroKey ? AggroName : o.WinnerKey == ControlKey ? ControlName
             : o.TurnLimitReached ? "（回合上限）" : "（异常中止）";

        // ===================================================== 报告 =====================================================

        public static string WriteReport(List<CardData> aggro, List<CardData> control,
            List<GameOutcome> outcomes, int games, int maxTurns)
        {
            var lines = new List<string>
            {
                $"# 快攻 vs 慢速 自动对战报告",
                $"",
                $"时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}　局数：{games}（换边）　回合上限：{maxTurns}",
                $"",
                $"## 卡组",
                $"{DeckSummary(AggroName, aggro)}",
                $"{DeckSummary(ControlName, control)}",
                $"",
                $"## 对局",
                $"| 局 | 快攻座位 | 胜者 | 回合 | 结束原因 | 错误 |",
                $"|---|---|---|---|---|---|",
            };
            foreach (var o in outcomes)
            {
                lines.Add($"| {o.Index} | {(o.AggroIsP1 ? "P1" : "P2")} | {WinnerText(o)} | {o.Turns} | {o.Reason} | {(o.Errors.Count == 0 ? "-" : o.Errors.Count + " 条")} |");
                foreach (var err in o.Errors)
                    lines.Add($"\n> 局 {o.Index} 错误：{err}\n");
            }

            int aggroWins = outcomes.Count(o => o.WinnerKey == AggroKey);
            int controlWins = outcomes.Count(o => o.WinnerKey == ControlKey);
            int unfinished = outcomes.Count(o => o.WinnerKey == "none");
            int totalErrors = outcomes.Sum(o => o.Errors.Count);
            var decided = outcomes.Where(o => o.Completed).ToList();

            lines.Add("");
            lines.Add("## 汇总");
            lines.Add($"- **{AggroName}：{aggroWins} 胜**（{Pct(aggroWins, games)}）");
            lines.Add($"- **{ControlName}：{controlWins} 胜**（{Pct(controlWins, games)}）");
            lines.Add($"- 未分胜负：{unfinished}　错误总数：{totalErrors}");
            if (decided.Count > 0)
                lines.Add($"- 平均回合数（有胜负局）：{decided.Sum(o => o.Turns) / (double)decided.Count:0.0}");
            var reasons = outcomes.Where(o => o.Completed).GroupBy(o => o.Reason).Select(g => $"{g.Key}×{g.Count()}").ToList();
            if (reasons.Count > 0) lines.Add($"- 结束原因：{string.Join("、", reasons)}");
            lines.Add($"- 每局战报见 Logs/MatchLog_*.md");

            string report = Path.Combine("Logs", "ArchetypeBattle_Report.md");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(report)));
            File.WriteAllText(report, string.Join("\n", lines), new UTF8Encoding(false));
            Debug.Log($"[快攻vs慢速] 完成：快攻 {aggroWins} : 慢速 {controlWins}（未分 {unfinished}，错误 {totalErrors}）；报告 {Path.GetFullPath(report)}");
            return Path.GetFullPath(report);
        }

        // ===================================================== 装载 =====================================================

        /// <summary>batchmode 的 CWD 不保证是工程根，而战报/报告均用相对 Logs/ 路径——先钉根。</summary>
        public static void PinProjectRoot()
            => Directory.SetCurrentDirectory(Directory.GetParent(Application.dataPath).FullName);

        /// <summary>装载两套卡组；任一为空返回 null（已 LogError 点名）。</summary>
        public static (List<CardData> aggro, List<CardData> control)? LoadDecks()
        {
            string deckDir = Path.Combine(Application.dataPath, "Configs", "BattleDecks");
            var aggro = LoadDeck(Path.Combine(deckDir, "Deck_Aggro.json"), AggroName);
            var control = LoadDeck(Path.Combine(deckDir, "Deck_Control.json"), ControlName);
            if (aggro.Count == 0 || control.Count == 0)
            {
                Debug.LogError("[快攻vs慢速] 卡组加载失败，中止");
                return null;
            }
            return (aggro, control);
        }

        private static List<CardData> LoadDeck(string path, string name)
        {
            if (!File.Exists(path))
            {
                Debug.LogError($"[快攻vs慢速] 卡组文件不存在：{path}");
                return new List<CardData>();
            }
            var deck = CardLoader.LoadCardsFromText(File.ReadAllText(path));
            Debug.Log($"[快攻vs慢速] {name} 装载 {deck.Count} 张（{path}）");
            return deck;
        }

        private static string DeckSummary(string name, List<CardData> deck)
        {
            var counts = deck.GroupBy(c => c.CardName)
                .Select(g => $"{g.Count()}×{g.Key}（{StatOf(g.First())}）");
            return $"- **{name}**（{deck.Count} 张）：{string.Join("、", counts)}";
        }

        private static string StatOf(CardData c)
        {
            string[] colorNames = { "灰", "红", "蓝", "绿", "白", "黑" };
            var cost = c.Cost != null && c.Cost.Count > 0
                ? string.Join("+", c.Cost.Select(kv => $"{colorNames[(int)kv.Key]}{kv.Value:0}"))
                : "0";
            return c.Supertype == Cardtype.Creature ? $"费{cost} {c.Power}/{c.Life}" : $"费{cost} 法术";
        }

        private static string Pct(int wins, int games)
            => games == 0 ? "0%" : $"{wins * 100.0 / games:0}%";
    }

    /// <summary>
    /// batchmode TCP 入口（借鉴 TideHeadlessServer 训练桥接）：单客户端 JSON 行协议。
    /// {"op":"ping"} 探活；{"op":"battle","games":N,"maxTurns":M} 每局流式回传 + summary；
    /// {"op":"quit"} 或断开即退出。与 TideHeadlessServer 不同：保留日志（-logFile 里可查装载告警）。
    /// </summary>
    public static class ArchetypeBattleServer
    {
        public const int DefaultPort = 17778;

        public static void Main()
        {
            int port = DefaultPort;
            var argv = Environment.GetCommandLineArgs();
            for (int i = 0; i < argv.Length - 1; i++)
                if (argv[i] == "-tidePort" && int.TryParse(argv[i + 1], out var p) && p > 0)
                {
                    port = p;
                    break;
                }

            ArchetypeBattle.PinProjectRoot();
            var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, port);
            listener.Start(1);
            try
            {
                Debug.Log($"[快攻vs慢速] TCP 桥接监听 127.0.0.1:{port}，等待客户端…");
                using (var client = listener.AcceptTcpClient()) // 阻塞等唯一客户端
                {
                    HandleClient(client);
                }
                Debug.Log("[快攻vs慢速] 客户端断开，服务器退出");
            }
            finally
            {
                listener.Stop();
                // batchmode 下 -executeMethod 返回后不保证自行退出——显式退出（客户端已确认收到结果）
                if (argv.Contains("-batchmode"))
                    EditorApplication.Exit(0);
            }
        }

        private static void HandleClient(System.Net.Sockets.TcpClient client)
        {
            var stream = client.GetStream();
            var reader = new StreamReader(stream, Encoding.UTF8);
            var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };

            (List<CardData> aggro, List<CardData> control)? decks = null;

            string line;
            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();
                if (line.Length == 0) continue;

                BattleRequest req;
                try { req = JsonUtility.FromJson<BattleRequest>(line); }
                catch (Exception ex)
                {
                    writer.WriteLine(Err($"无法解析请求: {line}（{ex.Message}）"));
                    continue;
                }

                try
                {
                    switch (req.op)
                    {
                        case "ping":
                            writer.WriteLine(JsonUtility.ToJson(new PongDto()));
                            break;
                        case "battle":
                        {
                            if (decks == null) decks = ArchetypeBattle.LoadDecks();
                            if (decks == null)
                            {
                                writer.WriteLine(Err("卡组加载失败（见 Unity 日志）"));
                                break;
                            }
                            int games = req.games > 0 ? req.games : ArchetypeBattle.DefaultGames;
                            int maxTurns = req.maxTurns > 0 ? req.maxTurns : ArchetypeBattle.DefaultMaxTurns;
                            var outcomes = ArchetypeBattle.RunBattles(decks.Value.aggro, decks.Value.control, games, maxTurns,
                                o => writer.WriteLine(JsonUtility.ToJson(DtoOf(o))));
                            string report = ArchetypeBattle.WriteReport(decks.Value.aggro, decks.Value.control, outcomes, games, maxTurns);
                            var summary = new SummaryDto
                            {
                                aggroWins = outcomes.Count(o => o.WinnerKey == ArchetypeBattle.AggroKey),
                                controlWins = outcomes.Count(o => o.WinnerKey == ArchetypeBattle.ControlKey),
                                unfinished = outcomes.Count(o => o.WinnerKey == "none"),
                                totalErrors = outcomes.Sum(o => o.Errors.Count),
                                reportPath = report,
                            };
                            writer.WriteLine(JsonUtility.ToJson(summary));
                            break;
                        }
                        case "quit":
                            Debug.Log("[快攻vs慢速] 收到 quit，退出");
                            return;
                        default:
                            writer.WriteLine(Err($"未知 op: {req.op}"));
                            break;
                    }
                }
                catch (Exception ex)
                {
                    writer.WriteLine(Err($"处理 {req.op} 出错: {ex.GetType().Name}: {ex.Message}"));
                }
            }
        }

        private static GameDto DtoOf(ArchetypeBattle.GameOutcome o) => new GameDto
        {
            index = o.Index,
            aggroSeat = o.AggroIsP1 ? "P1" : "P2",
            winner = o.WinnerKey,
            winnerName = ArchetypeBattle.WinnerText(o),
            reason = o.Reason,
            turns = o.Turns,
            completed = o.Completed,
            errors = o.Errors.Count,
        };

        private static string Err(string message)
        {
            Debug.LogError($"[快攻vs慢速] {message}");
            return JsonUtility.ToJson(new ErrorDto { message = message });
        }

        // ===================================================== JSON DTO（JsonUtility 字段名即协议键） =====================================================

        [Serializable] public class BattleRequest { public string op; public int games; public int maxTurns; }
        [Serializable] public class PongDto { public string op = "pong"; }
        [Serializable] public class ErrorDto { public string op = "error"; public string message; }
        [Serializable] public class GameDto { public string op = "game"; public int index; public string aggroSeat; public string winner; public string winnerName; public string reason; public int turns; public bool completed; public int errors; }
        [Serializable] public class SummaryDto { public string op = "summary"; public int aggroWins; public int controlWins; public int unfinished; public int totalErrors; public string reportPath; }
    }
}
