using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using CardCore;
using CardCore.AI;
using CardCore.Attribute;
using Newtonsoft.Json.Linq;
using SynergyUI;

namespace TideSim
{
    internal sealed class DeckSpec
    {
        public string Name;
        public string SkillCardId;
        public string SkillName;
        public List<CardData> Cards = new List<CardData>();
        public AiStrategy Strategy;
    }

    internal sealed class GameResult
    {
        public string Matchup;
        public int Seed;
        public string Seat1, Seat2;
        public string Strategy1, Strategy2;
        public string Skill1, Skill2;
        public string Winner;
        public string Result;   // seat1 / seat2 / draw / unfinished / error
        public string Reason;
        public int Turns;
        public int Life1, Life2;
        public int SkillUses1, SkillUses2;
        public int Plays1, Plays2;
        public string Error;
    }

    /// <summary>
    /// 三套预组无头对战（2026-11-04 新建）。规则=当前 CardCore；策略按预组名显式绑定；
    /// 技能/衍生物从预组 JSON 传入；每局独立 seed；换边轮替先手；P2 后手护甲同游戏规则。
    /// </summary>
    internal static class DeckSim
    {
        private const string DeckRed = "红·快攻";
        private const string DeckGreen = "绿·慢速";
        private const string DeckBlue = "蓝·控制";

        public static int Run(SimOptions o)
        {
            var all = CardCatalog.LoadAll();
            Console.WriteLine($"[decksim] 卡表={all?.Count ?? 0} 张；规则=当前 CardCore；后手护甲={GameCore.SecondPlayerArmorBonus}");

            var decks = new List<DeckSpec>();
            foreach (var name in CardDataPaths.PresetDeckNames)
            {
                var d = LoadDeck(name);
                if (d == null) return 1;
                decks.Add(d);
            }

            // 预检：技能必须解析、策略必须显式落到红/绿/蓝（不是 general）
            bool bad = false;
            Console.WriteLine("[preflight]");
            foreach (var d in decks)
            {
                bool skillOk = !string.IsNullOrEmpty(d.SkillCardId) && !string.IsNullOrEmpty(d.SkillName);
                bool strategyOk = d.Strategy != null && d.Strategy.Key != "general";
                if (!skillOk || !strategyOk) bad = true;
                Console.WriteLine($"  {d.Name}：{d.Cards.Count} 张｜技能={d.SkillName ?? "无"}（{d.SkillCardId ?? "null"}）｜" +
                                  $"策略={d.Strategy?.Key}（{d.Strategy?.DisplayName}）" +
                                  (skillOk && strategyOk ? "" : "  ← 预检失败"));
                // 非主色费检查（当前规则：攻红/血绿 0.5/点；预组是单色套，出现非主色费=这套牌自己付不起）
                var main = MainColorOf(d.Name);
                var offColor = new List<string>();
                foreach (var card in d.Cards)
                {
                    var cost = GameActions.GetCardCost(new CardWrapper(card), 0);
                    if (cost == null) continue;
                    foreach (var color in cost.NonzeroColors())
                    {
                        if (color == ManaType.Gray || color == ManaType.Any || color == main) continue;
                        offColor.Add($"{card.CardName}({cost})");
                        break;
                    }
                }
                if (offColor.Count > 0)
                    Console.WriteLine($"    ⚠ {offColor.Count} 张非主色费（单色套付不起）：" +
                                      string.Join("，", offColor.Distinct().Take(6)) +
                                      (offColor.Count > 6 ? "…" : ""));
            }
            if (bad)
            {
                Console.WriteLine("[preflight] 失败：预组技能未解析或策略未显式绑定，停止（不跑没有可信口径的对局）。");
                return 1;
            }

            var pairs = BuildPairs(decks, o.Matchup);
            if (pairs == null) return 2;
            if (pairs.Count == 0)
            {
                Console.WriteLine($"✗ --matchup={o.Matchup} 没有匹配到对阵；可用：all/cross/mirror/red-blue/red-green/blue-green");
                return 2;
            }

            var allResults = new List<GameResult>();
            int pairIndex = 0;
            foreach (var (a, b, mirror) in pairs)
            {
                for (int g = 0; g < o.Games; g++)
                {
                    bool aIsP1 = g % 2 == 0; // 换边：A 先手/后手各半
                    int seed = o.Seed + g; // 与 Unity 窗口批量同口径：第 1 局即单局 seed（基数+0）
                    var r = Play(a, b, aIsP1, seed, o.MaxTurns, o.Detail, o.MatchLog);
                    r.Matchup = $"{a.Name} vs {b.Name}" + (mirror ? "（镜像）" : "");
                    r.Seed = seed;
                    allResults.Add(r);
                    if (!o.Quiet)
                        Console.WriteLine($"[{pairIndex + 1}/{pairs.Count} {g + 1}/{o.Games}] {r.Matchup} seed={seed} {Describe(r)}");
                }
                pairIndex++;
            }

            WriteReports(allResults, decks, o);
            return allResults.Any(r => r.Result == "error") ? 1 : 0;
        }

        // ======================================== 装载 ========================================

        private static DeckSpec LoadDeck(string name)
        {
            var path = CardDataPaths.TutorialFileIn(name + ".json");
            if (!File.Exists(path))
            {
                Console.WriteLine($"[decksim] 缺预组文件：{path}");
                return null;
            }
            var root = JObject.Parse(File.ReadAllText(path));
            var ids = (root["cardIds"] as JArray)?.Select(x => (string)x).ToList() ?? new List<string>();
            if (ids.Count == 0)
            {
                Console.WriteLine($"[decksim] 预组 {name} 卡列表为空：{path}");
                return null;
            }
            var cards = new List<CardData>();
            var missing = new List<string>();
            foreach (var id in ids)
            {
                var c = CardCatalog.GetById(id);
                if (c == null) missing.Add(id);
                else cards.Add(c);
            }
            if (missing.Count > 0)
            {
                Console.WriteLine($"[decksim] 预组 {name} 有 {missing.Count} 张卡在卡表解析不到：{string.Join(",", missing.Take(8))}");
                return null;
            }
            string skillId = (string)root["skillCardId"];
            var skill = string.IsNullOrEmpty(skillId) ? null : CardCatalog.GetById(skillId);
            return new DeckSpec
            {
                Name = name,
                SkillCardId = skillId,
                SkillName = skill?.CardName,
                Cards = cards,
                Strategy = StrategyFor(name, cards),
            };
        }

        /// <summary>显式策略绑定（不走 AutoMatch 猜测）；只有未知卡组才回落自动匹配。</summary>
        private static AiStrategy StrategyFor(string name, List<CardData> cards)
        {
            switch (name)
            {
                case DeckRed: return AiStrategy.RedAggro;
                case DeckGreen: return AiStrategy.GreenRamp;
                case DeckBlue: return AiStrategy.BlueControl;
                default:
                    Console.WriteLine($"[decksim] 警告：{name} 不是已知预组，回落 AutoMatch");
                    return AiStrategy.AutoMatch(cards);
            }
        }

        private static ManaType MainColorOf(string name)
        {
            switch (name)
            {
                case DeckRed: return ManaType.Red;
                case DeckGreen: return ManaType.Green;
                case DeckBlue: return ManaType.Blue;
                default: return ManaType.Gray;
            }
        }

        private static List<(DeckSpec a, DeckSpec b, bool mirror)> BuildPairs(List<DeckSpec> decks, string matchup)
        {
            var cross = new List<(DeckSpec, DeckSpec, bool)>();
            for (int i = 0; i < decks.Count; i++)
                for (int j = i + 1; j < decks.Count; j++)
                    cross.Add((decks[i], decks[j], false));
            var mirrors = decks.Select(d => (d, d, true)).ToList();

            switch ((matchup ?? "all").ToLowerInvariant())
            {
                case "all": return cross.Concat(mirrors).ToList();
                case "cross": return cross;
                case "mirror": return mirrors;
            }

            var alias = new Dictionary<string, string>
            {
                { "red", DeckRed }, { "green", DeckGreen }, { "blue", DeckBlue },
                { "红", DeckRed }, { "绿", DeckGreen }, { "蓝", DeckBlue },
            };
            var parts = matchup.ToLowerInvariant()
                .Split(new[] { "-vs-", "vs", "-", "_" }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && alias.TryGetValue(parts[0], out var n1) && alias.TryGetValue(parts[1], out var n2))
            {
                var a = decks.FirstOrDefault(d => d.Name == n1);
                var b = decks.FirstOrDefault(d => d.Name == n2);
                if (a != null && b != null) return new List<(DeckSpec, DeckSpec, bool)> { (a, b, false) };
            }
            Console.WriteLine($"✗ 无法解析 --matchup={matchup}");
            return null;
        }

        // ======================================== 单局 ========================================

        private static GameResult Play(DeckSpec a, DeckSpec b, bool aIsP1, int seed, int maxTurns, bool detail, bool matchLog = false)
        {
            var r = new GameResult();
            var d1 = aIsP1 ? a : b;
            var d2 = aIsP1 ? b : a;
            r.Seat1 = d1.Name;
            r.Seat2 = d2.Name;
            r.Strategy1 = d1.Strategy.Key;
            r.Strategy2 = d2.Strategy.Key;
            r.Skill1 = d1.SkillName;
            r.Skill2 = d2.SkillName;

            // 与 Unity 单局/批量共用同一执行器：技能/衍生物/策略/seed/补泵/收尾全同口径。
            var cfg = new DuelConfig
            {
                Deck1 = d1.Cards,
                Deck2 = d2.Cards,
                SkillCardId1 = d1.SkillCardId,
                SkillCardId2 = d2.SkillCardId,
                Strategy1 = d1.Strategy,
                Strategy2 = d2.Strategy,
                Seed = seed,
                MaxTurns = maxTurns,
                Detail = detail,
                Label1 = d1.Name,
                Label2 = d2.Name,
                Log = detail ? (Action<string>)Console.WriteLine : null,
            };
            var d = AiDuelRunner.Run(cfg);

            if (matchLog)
            {
                string logsDir = Path.GetFullPath(Path.Combine(TidePaths.DataPath, "..", "Logs"));
                Directory.CreateDirectory(logsDir);
                // 2026-11-04：文件名带对阵 + 毫秒——同一 seed 在三组对阵里各跑一局，
                // 旧命名（秒级+seed）会互相覆盖（红镜像日志因此丢过）。
                string pair = $"{d1.Name}-{d2.Name}".Replace("·", "").Replace(" ", "");
                string path = Path.Combine(logsDir,
                    $"TideSim_MatchLog_{DateTime.Now:yyyyMMdd_HHmmss_fff}_{pair}_{seed}.md");
                string exported = MatchLogService.ExportMarkdown(path);
                if (!string.IsNullOrEmpty(exported))
                    Console.WriteLine($"[matchlog] {exported}");
            }

            r.Turns = d.TotalTurns;
            r.Reason = d.Reason;
            r.Life1 = d.Life1;
            r.Life2 = d.Life2;
            r.SkillUses1 = d.SkillUses1;
            r.SkillUses2 = d.SkillUses2;
            r.Plays1 = d.Plays1;
            r.Plays2 = d.Plays2;

            if (d.Errors.Count > 0)
            {
                r.Result = "error";
                r.Error = string.Join("；", d.Errors);
            }
            else if (d.Completed && d.Winner != null)
            {
                bool p1Wins = ReferenceEquals(d.Winner, GameCore.Instance.Player1);
                r.Result = p1Wins ? "seat1" : "seat2";
                r.Winner = p1Wins ? d1.Name : d2.Name;
            }
            else if (d.Completed)
            {
                r.Result = "draw";
            }
            else
            {
                r.Result = "unfinished";
                if (string.IsNullOrEmpty(r.Reason)) r.Reason = "MaxTurns";
            }

            return r;
        }

        private static string Describe(GameResult r)
        {
            switch (r.Result)
            {
                case "seat1":
                case "seat2":
                    return $"{r.Winner} 胜（{r.Reason}，{r.Turns} 回合，剩血 {r.Life1}/{r.Life2}）";
                case "draw":
                    return $"平局（{r.Reason}，{r.Turns} 回合）";
                case "unfinished":
                    return $"未完（回合上限 {r.Turns}，剩血 {r.Life1}/{r.Life2}）";
                default:
                    return $"异常：{r.Error}";
            }
        }

        // ======================================== 报告 ========================================

        private static void WriteReports(List<GameResult> results, List<DeckSpec> decks, SimOptions o)
        {
            string logsDir = Path.GetFullPath(Path.Combine(TidePaths.DataPath, "..", "Logs"));
            Directory.CreateDirectory(logsDir);
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            var csv = new StringBuilder();
            csv.AppendLine("matchup,seed,seat1,strategy1,skill1,seat2,strategy2,skill2,winner,result,reason,turns,life1,life2,skill_uses1,skill_uses2,plays1,plays2,error");
            foreach (var r in results)
                csv.AppendLine($"{r.Matchup},{r.Seed},{r.Seat1},{r.Strategy1},{r.Skill1},{r.Seat2},{r.Strategy2},{r.Skill2}," +
                               $"{r.Winner ?? ""},{r.Result},{r.Reason},{r.Turns},{r.Life1},{r.Life2}," +
                               $"{r.SkillUses1},{r.SkillUses2},{r.Plays1},{r.Plays2},{r.Error ?? ""}");
            string csvPath = Path.Combine(logsDir, $"TideSim_{stamp}.csv");
            File.WriteAllText(csvPath, csv.ToString(), new UTF8Encoding(true));

            var md = new StringBuilder();
            md.AppendLine($"# 三套预组无头对战（TideSim，{DateTime.Now:yyyy-MM-dd HH:mm}）");
            md.AppendLine();
            md.AppendLine($"口径：规则=当前 CardCore；策略显式绑定（红→RedAggro/绿→GreenRamp/蓝→BlueControl）；" +
                          $"技能/衍生物随预组 JSON 传入；每局独立 seed（基数 {o.Seed}）；换边轮替先手；" +
                          $"后手护甲 {GameCore.SecondPlayerArmorBonus}；每方单回合上限 {o.MaxTurns}；" +
                          $"响应窗策略已接（AiResponder）。");
            md.AppendLine();
            md.AppendLine("## 预检");
            md.AppendLine("| 卡组 | 张数 | 技能 | 策略 |");
            md.AppendLine("|---|---|---|---|");
            foreach (var d in decks)
                md.AppendLine($"| {d.Name} | {d.Cards.Count} | {d.SkillName}（{d.SkillCardId}） | {d.Strategy.Key}（{d.Strategy.DisplayName}） |");
            md.AppendLine();
            md.AppendLine($"样本：{results.Count} 局 / {results.Select(r => r.Matchup).Distinct().Count()} 组对阵；seed 基数 {o.Seed}。");
            md.AppendLine();
            md.AppendLine("## 汇总");
            md.AppendLine("| 对阵 | 局数 | seat1 胜 | seat2 胜 | 未完 | 错误 | 平均回合 | 技能发动(1/2) |");
            md.AppendLine("|---|---|---|---|---|---|---|---|");
            foreach (var m in results.Select(r => r.Matchup).Distinct())
            {
                var rs = results.Where(r => r.Matchup == m).ToList();
                int w1 = rs.Count(r => r.Result == "seat1");
                int w2 = rs.Count(r => r.Result == "seat2");
                int unf = rs.Count(r => r.Result == "unfinished");
                int err = rs.Count(r => r.Result == "error");
                var decided = rs.Where(r => r.Result == "seat1" || r.Result == "seat2").ToList();
                double avg = decided.Count > 0 ? decided.Average(r => r.Turns) : 0;
                md.AppendLine($"| {m} | {rs.Count} | {w1} | {w2} | {unf} | {err} | {avg:0.0} | " +
                              $"{rs.Sum(r => r.SkillUses1)}/{rs.Sum(r => r.SkillUses2)} |");
            }
            md.AppendLine();
            md.AppendLine("## 逐局");
            md.AppendLine("| seed | seat1 | seat2 | 胜者 | 回合 | 结束 | 技能发动(1/2) |");
            md.AppendLine("|---|---|---|---|---|---|---|");
            foreach (var r in results)
                md.AppendLine($"| {r.Seed} | {r.Seat1} | {r.Seat2} | {r.Winner ?? "-"} | {r.Turns} | {r.Reason} | {r.SkillUses1}/{r.SkillUses2} |");

            string mdPath = Path.Combine(logsDir, $"TideSim_{stamp}.md");
            File.WriteAllText(mdPath, md.ToString(), new UTF8Encoding(false));

            Console.WriteLine();
            Console.WriteLine($"[decksim] 完成 {results.Count} 局；明细 CSV：{csvPath}");
            Console.WriteLine($"[decksim] 报告 MD：{mdPath}");
            foreach (var m in results.Select(r => r.Matchup).Distinct())
            {
                var rs = results.Where(r => r.Matchup == m).ToList();
                Console.WriteLine($"[decksim] {m}：seat1 {rs.Count(r => r.Result == "seat1")} 胜 / " +
                                  $"seat2 {rs.Count(r => r.Result == "seat2")} 胜 / 未完 {rs.Count(r => r.Result == "unfinished")} / " +
                                  $"错误 {rs.Count(r => r.Result == "error")}；技能发动 {rs.Sum(r => r.SkillUses1)}/{rs.Sum(r => r.SkillUses2)}");
            }
        }
    }
}
