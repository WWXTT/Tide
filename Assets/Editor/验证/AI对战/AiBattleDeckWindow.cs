using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CardCore;
using CardCore.AI;
using CardCore.AI.NeuralEnv;
using UnityEditor;
using UnityEngine;

namespace CardCore.Editor.Tests
{
    /// <summary>
    /// AI 自动对战选卡组窗口（2026-09-21 策略模式整合）：
    /// · 卡组=「玩家卡组」下拉（StreamingAssets/Card/ 下的 DeckData——三色主题卡组经
    ///   Tools/构建三色主题卡组 生成）｜随机三色（三主题并池抽 30）｜拖入 JSON（旧路径保留）；
    /// · 策略=每玩家独立下拉（自动匹配卡组主色/通用/红快攻/绿慢速/蓝控制——SimpleAI 决策偏好分化）；
    /// · 大脑=脚本 SimpleAI（策略生效）/AI 神经网络（策略忽略）；
    /// · 局数=1 单局战报；N 批量换边循环并写 Logs/ThemeBattle_Report.md（原 ArchetypeBattle 职责并入）。
    /// 复用双卡组+双大脑+双策略的 AiBattleDriver.RunFullGame。
    /// </summary>
    public class AiBattleDeckWindow : EditorWindow
    {
        private static readonly string[] BrainLabels = { "脚本 SimpleAI（策略生效）", "AI 神经网络" };
        private const int DeckSize = 30; // 训练口径卡组张数

        // 选项下标：0=拖入 JSON；1..N=玩家卡组；末位=随机三色
        private const int DragDropIndex = 0;
        private int RandomIndex => 1 + _deckNames.Count;

        private List<string> _deckNames = new List<string>(); // Card/ 下的卡组名（下拉 1..N）
        private Vector2 _scroll;

        private int _p1Deck = 1;
        private int _p2Deck = 1;
        private string _p1Path = "";
        private string _p2Path = "";
        private int _p1Brain;
        private int _p2Brain;
        private int _p1Strategy = 1; // AiStrategy.Options 下标（1=通用；0=自动匹配放最后改）
        private int _p2Strategy = 1;
        private int _maxTurns = 100;
        private int _games = 1;

        private static readonly string[] StrategyLabels =
            AiStrategy.Options.Select(o => o.label).ToArray();

        [MenuItem("Tools/AI 自动对战（选卡组）")]
        public static void Open() => GetWindow<AiBattleDeckWindow>("AI 自动对战（选卡组）");

        private void OnEnable() => ReloadDecks();

        private void ReloadDecks()
        {
            // 玩家卡组=Card/ 下的 DeckData（DeckSerializer 与 Cards/Effects 同目录——过滤非卡组文件）
            _deckNames = SynergyUI.DeckSerializer.LoadAll()
                .Where(d => !string.IsNullOrEmpty(d.name) && d.cardIds != null && d.cardIds.Count > 0)
                .Select(d => d.name)
                .ToList();
            _p1Deck = Mathf.Clamp(_p1Deck, 0, _deckNames.Count + 1);
            _p2Deck = Mathf.Clamp(_p2Deck, 0, _deckNames.Count + 1);
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.HelpBox(
                "卡组：玩家卡组（StreamingAssets/Card/，Tools/构建三色主题卡组 生成红/绿/蓝）· 拖入 JSON · 随机三色。\n" +
                "策略：仅脚本大脑生效——红=铺场打脸+激励最高攻、绿=回血攒费+大生物碾压、蓝=指示物控场+中等换小。",
                MessageType.Info);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("双方卡组与策略", EditorStyles.boldLabel);
            DrawPlayerSection("玩家 1", ref _p1Deck, ref _p1Brain, ref _p1Strategy, ref _p1Path);
            DrawPlayerSection("玩家 2", ref _p2Deck, ref _p2Brain, ref _p2Strategy, ref _p2Path);

            EditorGUILayout.Space();
            _maxTurns = EditorGUILayout.IntField("最大回合数", _maxTurns);
            _maxTurns = Mathf.Max(1, _maxTurns);
            _games = EditorGUILayout.IntField("局数（>1 批量换边，写报告）", _games);
            _games = Mathf.Max(1, _games);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(!DecksReady()))
            {
                if (GUILayout.Button(_games > 1 ? $"运行 {_games} 局（换边循环）" : "运行对战", GUILayout.Height(32)))
                    RunBattle();
            }
            if (GUILayout.Button("刷新卡组列表"))
                ReloadDecks();

            EditorGUILayout.EndScrollView();
        }

        private void DrawPlayerSection(string label, ref int deckIndex, ref int brain, ref int strategy, ref string path)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            var options = new List<string> { "拖入 JSON 文件（下方）" };
            options.AddRange(_deckNames);
            options.Add("随机三色（主题并池抽 30）");
            deckIndex = EditorGUILayout.Popup("卡组", Mathf.Clamp(deckIndex, 0, options.Count - 1), options.ToArray());
            brain = EditorGUILayout.Popup("大脑", brain, BrainLabels);
            strategy = EditorGUILayout.Popup("策略", Mathf.Clamp(strategy, 0, StrategyLabels.Length - 1), StrategyLabels);

            if (deckIndex == DragDropIndex)
                DrawDeckZone(label, ref path);
        }

        private static void DrawDeckZone(string label, ref string path)
        {
            var rect = GUILayoutUtility.GetRect(0, 40, GUILayout.ExpandWidth(true));
            bool valid = HasDeck(path);
            string text = valid ? path
                : (string.IsNullOrEmpty(path) ? "拖 .json 到此处" : $"(未找到) {path}");
            GUI.Box(rect, text, valid ? EditorStyles.helpBox : EditorStyles.textArea);
            HandleDragDrop(rect, ref path);

            if (GUILayout.Button($"浏览 {label} …"))
            {
                var picked = EditorUtility.OpenFilePanel("选择卡组 JSON", Application.dataPath, "json");
                if (!string.IsNullOrEmpty(picked))
                    path = picked;
            }
        }

        private bool DecksReady()
            => (_p1Deck != DragDropIndex || HasDeck(_p1Path))
               && (_p2Deck != DragDropIndex || HasDeck(_p2Path));

        // ======================================== 运行 ========================================

        private void RunBattle()
        {
            List<CardData> p1;
            List<CardData> p2;
            try
            {
                p1 = ResolveDeck(_p1Deck, ref _p1Path);
                p2 = ResolveDeck(_p2Deck, ref _p2Path);
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("运行对战", ex.Message, "好");
                return;
            }
            if (p1 == null || p1.Count == 0 || p2 == null || p2.Count == 0)
            {
                EditorUtility.DisplayDialog("运行对战", "卡组解析后为空（检查卡组 JSON / 主题卡组是否已构建）。", "好");
                return;
            }

            var strategy1 = ResolveStrategy(_p1Strategy, p1);
            var strategy2 = ResolveStrategy(_p2Strategy, p2);
            var brain1 = (AiBrain)_p1Brain;
            var brain2 = (AiBrain)_p2Brain;
            OnnxTidePolicy policy = null;
            if (brain1 == AiBrain.Neural || brain2 == AiBrain.Neural)
            {
                ConfigureNeuralIdentity(p1, p2);
                try
                {
                    policy = new OnnxTidePolicy();
                }
                catch (Exception ex)
                {
                    EditorUtility.DisplayDialog("AI 大脑", $"{ex.Message}", "好");
                    return;
                }
            }

            try
            {
                if (_games <= 1)
                {
                    new AiBattleDriver().RunFullGame(p1, p2, _maxTurns, brain1, brain2, policy, strategy1, strategy2);
                }
                else
                {
                    var report = RunBatch(p1, p2, strategy1, strategy2, brain1, brain2, policy, _games, _maxTurns);
                    EditorUtility.DisplayDialog("批量对战完成", report.summary, "好");
                }
            }
            finally
            {
                policy?.Dispose();
            }
        }

        /// <summary>解析下拉所选卡组：拖入 JSON / 玩家卡组（ID 引用→CardCatalog 还原）/ 随机三色。</summary>
        private List<CardData> ResolveDeck(int deckIndex, ref string path)
        {
            if (deckIndex == DragDropIndex)
            {
                if (!HasDeck(path)) throw new Exception("卡组 JSON 文件不存在，请重新拖入或浏览选择。");
                return CardLoader.LoadCardsFromText(File.ReadAllText(path));
            }
            if (deckIndex == RandomIndex)
            {
                var pool = ThemeColorPools().SelectMany(p => p).ToList();
                if (pool.Count == 0)
                    throw new Exception("主题卡池为空——先运行 Tools/构建三色主题卡组。");
                var rng = new System.Random(); // 单实例双采样（两个新实例可能同刻同种子）
                return SampleDeck(rng, pool);
            }
            int nameIndex = deckIndex - 1;
            if (nameIndex < 0 || nameIndex >= _deckNames.Count) throw new Exception("卡组列表已变化，请刷新。");
            var deck = SynergyUI.DeckSerializer.Load(_deckNames[nameIndex]);
            if (deck == null) throw new Exception($"卡组不存在：{_deckNames[nameIndex]}");
            var cards = new List<CardData>();
            var missing = new List<string>();
            foreach (var id in deck.cardIds)
            {
                var card = SynergyUI.CardCatalog.GetById(id);
                if (card == null) missing.Add(id);
                else cards.Add(card);
            }
            if (missing.Count > 0)
                throw new Exception($"卡组 {deck.name} 有 {missing.Count} 张卡在 Cards.json 找不到" +
                                    $"（先运行 Tools/构建三色主题卡组）：{string.Join(",", missing.Take(3))}…");
            return cards;
        }

        /// <summary>策略解析：下标 0=自动匹配（主题标签优先→费用主色），其余=AiStrategy.Options。</summary>
        private static AiStrategy ResolveStrategy(int index, List<CardData> deck)
        {
            var key = AiStrategy.Options[Mathf.Clamp(index, 0, AiStrategy.Options.Length - 1)].key;
            if (key != "auto") return AiStrategy.Create(key);
            return AiStrategy.AutoMatch(deck);
        }

        // ======================================== 批量（原 ArchetypeBattle 职责并入） ========================================

        private struct BatchOutcome
        {
            public int Index;
            public bool DeckAIsP1;
            public bool Completed;
            public string WinnerName; // 卡组名 / （回合上限）/（异常中止）
            public string Reason;
            public int Turns;
            public List<string> Errors;
        }

        private (string summary, string reportPath) RunBatch(List<CardData> a, List<CardData> b,
            AiStrategy sa, AiStrategy sb, AiBrain brain1, AiBrain brain2, OnnxTidePolicy policy, int games, int maxTurns)
        {
            string nameA = $"卡组A({(a.FirstOrDefault()?.Tags?.FirstOrDefault() ?? "拖入")})·{sa.DisplayName}";
            string nameB = $"卡组B({(b.FirstOrDefault()?.Tags?.FirstOrDefault() ?? "拖入")})·{sb.DisplayName}";
            var driver = new AiBattleDriver();
            var outcomes = new List<BatchOutcome>();
            for (int i = 0; i < games; i++)
            {
                bool aIsP1 = i % 2 == 0;
                var r = aIsP1
                    ? driver.RunFullGame(a, b, maxTurns, brain1, brain2, policy, sa, sb)
                    : driver.RunFullGame(b, a, maxTurns, brain1, brain2, policy, sb, sa);

                var o = new BatchOutcome
                {
                    Index = i + 1,
                    DeckAIsP1 = aIsP1,
                    Completed = r.Completed && r.Winner != null,
                    Reason = r.Reason ?? "-",
                    Turns = r.TotalTurns,
                    Errors = new List<string>(r.Errors),
                };
                if (o.Completed)
                {
                    bool winnerIsP1 = ReferenceEquals(r.Winner, GameCore.Instance.Player1);
                    o.WinnerName = (winnerIsP1 == aIsP1) ? nameA : nameB;
                }
                else o.WinnerName = r.TurnLimitReached ? "（回合上限）" : "（异常中止）";
                outcomes.Add(o);
                Debug.Log($"[批量对战] 第 {o.Index}/{games} 局：{o.WinnerName}（{o.Reason}，{o.Turns} 回合，错误 {o.Errors.Count}）");
            }

            // 报告（继承 ArchetypeBattle 的对局表+汇总口径）
            int winsA = outcomes.Count(o => o.WinnerName == nameA);
            int winsB = outcomes.Count(o => o.WinnerName == nameB);
            int unfinished = outcomes.Count - winsA - winsB;
            var lines = new List<string>
            {
                "# 主题卡组批量对战报告",
                "",
                $"时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}　局数：{games}（换边）　回合上限：{maxTurns}",
                "",
                $"## 双方",
                $"- **{nameA}：{winsA} 胜**（{Pct(winsA, games)}）",
                $"- **{nameB}：{winsB} 胜**（{Pct(winsB, games)}）",
                $"- 未分胜负：{unfinished}　错误总数：{outcomes.Sum(o => o.Errors.Count)}",
                "",
                $"## 对局",
                $"| 局 | A 座位 | 胜者 | 回合 | 结束原因 | 错误 |",
                $"|---|---|---|---|---|---|",
            };
            foreach (var o in outcomes)
            {
                lines.Add($"| {o.Index} | {(o.DeckAIsP1 ? "P1" : "P2")} | {o.WinnerName} | {o.Turns} | {o.Reason} | {(o.Errors.Count == 0 ? "-" : o.Errors.Count + " 条")} |");
                foreach (var err in o.Errors) lines.Add($"\n> 局 {o.Index} 错误：{err}\n");
            }
            var decided = outcomes.Where(o => o.Completed).ToList();
            if (decided.Count > 0)
                lines.Add($"- 平均回合数（有胜负局）：{decided.Sum(o => o.Turns) / (double)decided.Count:0.0}");
            var reasons = outcomes.Where(o => o.Completed).GroupBy(o => o.Reason).Select(g => $"{g.Key}×{g.Count()}");
            lines.Add($"- 结束原因：{string.Join("、", reasons.ToList())}");
            lines.Add("- 每局战报见 Logs/MatchLog_*.md");

            string report = Path.Combine("Logs", "ThemeBattle_Report.md");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(report)));
            File.WriteAllText(report, string.Join("\n", lines), new UTF8Encoding(false));
            string summary = $"{nameA} {winsA} : {winsB} {nameB}（未分 {unfinished}，错误 {outcomes.Sum(o => o.Errors.Count)}）";
            Debug.Log($"[批量对战] 完成：{summary}；报告 {Path.GetFullPath(report)}");
            return (summary, Path.GetFullPath(report));
        }

        private static string Pct(int wins, int games) => games == 0 ? "0%" : $"{wins * 100.0 / games:0}%";

        // ======================================== 拖放/训练辅助（沿用） ========================================

        private static bool HasDeck(string path)
            => !string.IsNullOrEmpty(path) && File.Exists(path);

        private static void HandleDragDrop(Rect rect, ref string path)
        {
            var evt = Event.current;
            if (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform) return;
            if (!rect.Contains(evt.mousePosition)) return;

            var p = PickDroppedJson();
            if (p == null)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
            }
            else
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    path = p;
                }
            }
            evt.Use();
        }

        /// <summary>从拖放里取第一个 .json 路径：优先 DragAndDrop.paths（文件系统路径），次 TextAsset 项目路径。</summary>
        private static string PickDroppedJson()
        {
            foreach (var p in DragAndDrop.paths)
                if (p.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    return p;

            foreach (var obj in DragAndDrop.objectReferences)
            {
                if (!(obj is TextAsset)) continue;
                var assetPath = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(assetPath)) continue;
                if (!assetPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;
                return Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
            }
            return null;
        }

        /// <summary>ONNX 卡身份登记（镜像 TideHeadlessServer.HandleReset / 训练服务器口径）。</summary>
        private static void ConfigureNeuralIdentity(List<CardData> p1, List<CardData> p2)
        {
            int loaded = TideCardIndex.ConfigureManifest(ManifestPath);
            Debug.Log(loaded >= 0
                ? $"[AI 对战] 卡身份清单载入 {loaded} 条（{ManifestPath}）"
                : "[AI 对战] ⚠ manifest 表指纹不符——模型 embedding 行可能串台，建议重导出");
            BattleDeckSources.RegisterIdentities(); // Cards.json 全池（含三主题卡）——2026-09-21 起替代已删的 Configs/TestDecks
            TideCardIndex.Register(p1);
            TideCardIndex.Register(p2);
        }

        private static string ManifestPath
            => Path.Combine(Application.dataPath, "..", "tide_rl", "card_identity_manifest.json");

        /// <summary>三色主题卡池（Cards.json 按 Theme* 标签过滤；下标红/绿/蓝——2026-09-21 起替代已删的 Configs/TestDecks）。</summary>
        private static List<CardData>[] ThemeColorPools()
        {
            var tags = new[] { "ThemeRed", "ThemeGreen", "ThemeBlue" };
            var pools = new List<CardData>[tags.Length];
            var all = SynergyUI.CardCatalog.LoadAll();
            for (int i = 0; i < tags.Length; i++)
                pools[i] = all.Where(c => c.Tags != null && c.Tags.Contains(tags[i])).ToList();
            return pools;
        }

        /// <summary>洗牌抽前 30（无放回，镜像训练评估口径）。</summary>
        private static List<CardData> SampleDeck(System.Random rng, List<CardData> source)
        {
            var shuffled = new List<CardData>(source);
            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
            }
            return shuffled.GetRange(0, Math.Min(DeckSize, shuffled.Count));
        }
    }
}
