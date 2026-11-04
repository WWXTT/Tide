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
    /// · 卡组=「玩家卡组」下拉（DeckCatalog 合并口：CardData/Card 玩家存档 + TutorialData/Tutorial 只读预组——
    ///   三套主题预组在 TutorialData 手工维护，旧的生成/填充工具已删，禁止脚本重建）｜随机三色（三主题并池抽 30）｜拖入 JSON（旧路径保留）；
    /// · 策略=每玩家独立下拉（自动匹配卡组主色/通用/红快攻/绿慢速/蓝控制——SimpleAI 决策偏好分化）；
    /// · 大脑=脚本 SimpleAI（策略生效）/AI 神经网络（策略忽略）；
    /// · 局数=1 单局战报；N 批量换边循环并写 Logs/ThemeBattle_Report.md（原 ArchetypeBattle 职责并入）。
    /// 复用双卡组+双大脑+双策略的 AiBattleDriver.RunFullGame。
    /// 2026-11-04 修（与木桩/联机开局对齐）：此前只取 cardIds、不传 skillCardId 与 tokenBindings——
    /// 双方都没有英雄技能卡（红·快攻 40:0 横扫蓝·控制，与木桩 5%:95% 完全相反）。现改为
    /// DeckTokenSupport.BuildCardList（含衍生物区模板）+ DeckData.skillCardId + tokenBindings，
    /// 策略默认「自动匹配卡组」（木桩同口径）；拖入的卡组 JSON 同样解出 skillCardId/tokenBindings。
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
        private int _p1Strategy; // AiStrategy.Options 下标（0=自动匹配卡组=木桩同口径；1=通用）
        private int _p2Strategy;
        private int _maxTurns = 200;
        private int _games = 1;
        private int _seed = 20261104;   // 单局=该 seed；批量=基数（第 i 局用 seed+i，第 1 局与单局同 seed）

        private static readonly string[] StrategyLabels =
            AiStrategy.Options.Select(o => o.label).ToArray();

        [MenuItem("Tools/AI 自动对战（选卡组）")]
        public static void Open() => GetWindow<AiBattleDeckWindow>("AI 自动对战（选卡组）");

        private void OnEnable() => ReloadDecks();

        private void ReloadDecks()
        {
            // 卡组=玩家存档 + 只读预组（2026-10-22 DeckCatalog 合并口——三套主题卡组已迁教学目录）
            _deckNames = SynergyUI.DeckCatalog.LoadAll()
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
                "卡组：玩家存档 + 只读预组（TutorialData 手工维护，无生成入口）· 拖入 JSON · 随机三色。\n" +
                "策略：仅脚本大脑生效——红=铺场打脸+激励最高攻、绿=回血攒费+大生物碾压、蓝=指示物控场+中等换小；" +
                "默认「自动匹配卡组」= 木桩测试同一口径。\n" +
                "英雄技能：随卡组标记（DeckData.skillCardId）自动传入，与木桩/联机开局同规则；" +
                "拖入的 JSON 若是卡组文件（cardIds/skillCardId）同口径传入，纯卡定义池=无技能。",
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
            _seed = EditorGUILayout.IntField("seed（单局=本局；批量=基数，第 i 局 seed+i）", _seed);

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
            string skill1, skill2;
            List<SynergyUI.DeckTokenBinding> tokens1, tokens2;
            try
            {
                p1 = ResolveDeck(_p1Deck, ref _p1Path, out skill1, out tokens1);
                p2 = ResolveDeck(_p2Deck, ref _p2Path, out skill2, out tokens2);
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

            // 2026-11-03 诊断：先点名双方卡组张数与内容（战报张数异常时一眼定位；不改变行为）
            // 2026-11-04：技能随卡组标记一并播报（此前漏传=双方无技能，与木桩对局完全跑偏）
            UnityEngine.Debug.Log($"[AI对战] P1 卡组 {_DeckLabel(_p1Deck)} = {p1.Count} 张，技能 {_SkillLabel(skill1)}：{_DeckDigest(p1)}");
            UnityEngine.Debug.Log($"[AI对战] P2 卡组 {_DeckLabel(_p2Deck)} = {p2.Count} 张，技能 {_SkillLabel(skill2)}：{_DeckDigest(p2)}");

            var strategy1 = ResolveStrategy(_p1Strategy, p1, DeckNameOf(_p1Deck));
            var strategy2 = ResolveStrategy(_p2Strategy, p2, DeckNameOf(_p2Deck));
            UnityEngine.Debug.Log($"[AI对战] seed={_seed} 策略 P1={strategy1.Key}（{strategy1.DisplayName}）" +
                                  $" P2={strategy2.Key}（{strategy2.DisplayName}）");
            var brain1 = (AiBrain)_p1Brain;
            var brain2 = (AiBrain)_p2Brain;
            OnnxTidePolicy policy = null;
            if (brain1 == AiBrain.Neural || brain2 == AiBrain.Neural)
            {
                var guardError = NeuralDeployGuard.Verify(out var guardDetail);
                if (guardError != null)
                {
                    EditorUtility.DisplayDialog("AI 大脑", $"{guardError}\n（{guardDetail}）", "好");
                    return;
                }
                if (!ConfigureNeuralIdentity(p1, p2)) return;
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
                    new AiBattleDriver().RunFullGame(p1, p2, _maxTurns, brain1, brain2, policy,
                        strategy1, strategy2, skill1, skill2, tokens1, tokens2, _seed, detail: true);
                }
                else
                {
                    var report = RunBatch(p1, p2, strategy1, strategy2, brain1, brain2, policy, _games, _maxTurns,
                        skill1, skill2, tokens1, tokens2, _seed);
                    EditorUtility.DisplayDialog("批量对战完成", report.summary, "好");
                }
            }
            finally
            {
                policy?.Dispose();
            }
        }

        /// <summary>下拉项可读名（诊断用）。</summary>
        private string _DeckLabel(int index)
        {
            if (index == DragDropIndex) return "拖入JSON";
            if (index == RandomIndex) return "随机三色";
            int nameIndex = index - 1;
            return nameIndex >= 0 && nameIndex < _deckNames.Count ? _deckNames[nameIndex] : $"(越界{index})";
        }

        /// <summary>卡组摘要：卡名×张数（诊断用）。</summary>
        private static string _DeckDigest(List<CardData> deck)
        {
            if (deck == null) return "∅";
            return string.Join("、", deck.Where(c => c != null)
                .GroupBy(c => c.CardName)
                .OrderByDescending(g => g.Count())
                .Select(g => $"{g.Key}×{g.Count()}"));
        }

        /// <summary>技能标记可读名（诊断/报告用；null=无技能）。</summary>
        private static string _SkillLabel(string skillCardId)
        {
            if (string.IsNullOrEmpty(skillCardId)) return "无";
            var card = SynergyUI.CardCatalog.GetById(skillCardId);
            return card != null ? $"{card.CardName}({skillCardId})" : skillCardId;
        }

        /// <summary>解析下拉所选卡组：拖入 JSON / 玩家卡组（ID 引用→CardCatalog 还原）/ 随机三色。
        /// 同时回传英雄技能与衍生物槽绑定（2026-11-04 对齐木桩/联机开局口径）：
        /// · 玩家卡组/预组 = DeckData.skillCardId + tokenBindings，卡表 = cardIds ∪ tokenCardIds
        ///   （DeckTokenSupport.BuildCardList，衍生物模板落额外区）；
        /// · 随机三色 = 等价「随机卡组」，BattleController 对随机卡组按 AutoPickSkillCard 挑技能；
        /// · 拖入 JSON = 若是卡组文件（cardIds/skillCardId/tokenCardIds）按上面同口径解析并传技能；
        ///   旧口径的「卡定义池」JSON（Cards.json 那种 cards 数组）无卡组标记 → 无技能、无绑定。</summary>
        private List<CardData> ResolveDeck(int deckIndex, ref string path,
            out string skillCardId, out List<SynergyUI.DeckTokenBinding> tokenBindings)
        {
            skillCardId = null;
            tokenBindings = null;
            if (deckIndex == DragDropIndex)
            {
                if (!HasDeck(path)) throw new Exception("卡组 JSON 文件不存在，请重新拖入或浏览选择。");
                var text = File.ReadAllText(path);
                var fileDeck = TryReadDeckFile(text);
                if (fileDeck == null) return CardLoader.LoadCardsFromText(text); // 旧口径：卡定义池（无技能）
                var lost = new List<string>();
                foreach (var id in fileDeck.cardIds.Concat(fileDeck.tokenCardIds ?? new List<string>()))
                    if (SynergyUI.CardCatalog.GetById(id) == null) lost.Add(id);
                if (lost.Count > 0)
                    throw new Exception($"卡组文件有 {lost.Count} 张卡在卡表找不到：" +
                                        $"{string.Join(",", lost.Take(3))}…（{path}）");
                var built = SynergyUI.DeckTokenSupport.BuildCardList(fileDeck); // cardIds ∪ tokenCardIds
                skillCardId = fileDeck.skillCardId;
                tokenBindings = fileDeck.tokenBindings != null && fileDeck.tokenBindings.Count > 0
                    ? fileDeck.tokenBindings : null;
                return built;
            }
            if (deckIndex == RandomIndex)
            {
                var pool = ThemeColorPools().SelectMany(p => p).ToList();
                if (pool.Count == 0)
                    throw new Exception("主题卡池为空——检查 TutorialData 的 红·快攻/绿·慢速/蓝·控制.json 与卡表（预组手工维护，无生成入口）。");
                var rng = new System.Random(); // 单实例双采样（两个新实例可能同刻同种子）
                var sampled = SampleDeck(rng, pool);
                skillCardId = CardCore.HeroSkillSystem.AutoPickSkillCard(sampled); // 随机卡组口径
                return sampled;
            }
            int nameIndex = deckIndex - 1;
            if (nameIndex < 0 || nameIndex >= _deckNames.Count) throw new Exception("卡组列表已变化，请刷新。");
            var deck = SynergyUI.DeckCatalog.Load(_deckNames[nameIndex]);
            if (deck == null) throw new Exception($"卡组不存在：{_deckNames[nameIndex]}");
            var missing = new List<string>();
            foreach (var id in deck.cardIds.Concat(deck.tokenCardIds ?? new List<string>()))
                if (SynergyUI.CardCatalog.GetById(id) == null) missing.Add(id);
            if (missing.Count > 0)
                throw new Exception($"卡组 {deck.name} 有 {missing.Count} 张卡在卡表找不到" +
                                    $"（检查 TutorialData 预组文件与 Cards.json 卡表 ID）：{string.Join(",", missing.Take(3))}…");
            var cards = SynergyUI.DeckTokenSupport.BuildCardList(deck); // cardIds ∪ tokenCardIds
            skillCardId = deck.skillCardId;
            tokenBindings = deck.tokenBindings != null && deck.tokenBindings.Count > 0
                ? deck.tokenBindings : null;
            return cards;
        }

        /// <summary>把拖入的 JSON 当卡组文件解析（cardIds 非空才算；卡定义池/坏 JSON → null 回落旧口径）。
        /// 与 DeckSerializer 同形（JsonUtility），兼容 tokenCardIds/tokenBindings 新档；
        /// 卡组的英雄技能标记 skillCardId 由此回传——拖入卡组与下拉卡组同口径。</summary>
        private static SynergyUI.DeckData TryReadDeckFile(string jsonText)
        {
            if (string.IsNullOrEmpty(jsonText)) return null;
            try
            {
                var deck = JsonUtility.FromJson<SynergyUI.DeckData>(jsonText);
                return deck != null && deck.cardIds != null && deck.cardIds.Count > 0 ? deck : null;
            }
            catch { return null; }
        }

        /// <summary>策略解析：下标 0=自动匹配；预组按名单直接绑定专属策略（与无头 TideSim 同口径，
        /// 不依赖标签多数派猜测）；其余/拖入仍按 AutoMatch 回落。手选非 auto 时一律尊重手选。</summary>
        private static AiStrategy ResolveStrategy(int index, List<CardData> deck, string deckName = null)
        {
            var key = AiStrategy.Options[Mathf.Clamp(index, 0, AiStrategy.Options.Length - 1)].key;
            if (key != "auto") return AiStrategy.Create(key);
            if (deckName == "红·快攻") return AiStrategy.RedAggro;
            if (deckName == "绿·慢速") return AiStrategy.GreenRamp;
            if (deckName == "蓝·控制") return AiStrategy.BlueControl;
            return AiStrategy.AutoMatch(deck);
        }

        /// <summary>下拉下标 → 预组名（仅预组返回名字；拖入/随机返回 null）。</summary>
        private string DeckNameOf(int deckIndex)
            => deckIndex >= 1 && deckIndex <= _deckNames.Count ? _deckNames[deckIndex - 1] : null;

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
            AiStrategy sa, AiStrategy sb, AiBrain brain1, AiBrain brain2, OnnxTidePolicy policy, int games, int maxTurns,
            string skillA = null, string skillB = null,
            List<SynergyUI.DeckTokenBinding> tokensA = null, List<SynergyUI.DeckTokenBinding> tokensB = null,
            int baseSeed = 20261104)
        {
            string nameA = $"卡组A({(a.FirstOrDefault()?.Tags?.FirstOrDefault() ?? "拖入")})·{sa.DisplayName}";
            string nameB = $"卡组B({(b.FirstOrDefault()?.Tags?.FirstOrDefault() ?? "拖入")})·{sb.DisplayName}";
            var driver = new AiBattleDriver();
            var outcomes = new List<BatchOutcome>();
            for (int i = 0; i < games; i++)
            {
                bool aIsP1 = i % 2 == 0;
                int seed = baseSeed + i; // 第 1 局（i=0）与单局同 seed
                var r = aIsP1
                    ? driver.RunFullGame(a, b, maxTurns, brain1, brain2, policy, sa, sb, skillA, skillB, tokensA, tokensB, seed)
                    : driver.RunFullGame(b, a, maxTurns, brain1, brain2, policy, sb, sa, skillB, skillA, tokensB, tokensA, seed);

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
                $"英雄技能：A={_SkillLabel(skillA)}　B={_SkillLabel(skillB)}（随卡组标记，木桩/联机同口径）",
                $"策略：A={sa.DisplayName}　B={sb.DisplayName}",
                $"seed：基数 {baseSeed}（第 i 局 seed+i，第 1 局与单局同 seed）",
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

        /// <summary>ONNX 卡身份登记（镜像 TideHeadlessServer.HandleReset / 训练服务器口径）。
        /// 2026-09-29 起表指纹不符 = 失败拒跑（登记会全体换行、embedding 串台），不再只是警告。</summary>
        private static bool ConfigureNeuralIdentity(List<CardData> p1, List<CardData> p2)
        {
            int loaded = TideCardIndex.ConfigureManifest(ManifestPath);
            if (loaded < 0)
            {
                EditorUtility.DisplayDialog("AI 大脑",
                    "card_identity_manifest.json 表指纹与当前原子表不符——游戏内容已变更，"
                    + "卡身份会全体换行、模型 embedding 串台。\n需重训 + 重导出（模型/清单/游戏内容三者同源）。", "好");
                return false;
            }
            Debug.Log($"[AI 对战] 卡身份清单载入 {loaded} 条（{ManifestPath}）");
            BattleDeckSources.RegisterIdentities(); // Cards.json 全池（含三主题卡）——2026-09-21 起替代已删的 Configs/TestDecks
            TideCardIndex.Register(p1);
            TideCardIndex.Register(p2);
            return true;
        }

        private static string ManifestPath
            => Path.Combine(Application.dataPath, "..", "tide_rl", "card_identity_manifest.json");

        /// <summary>三色主题卡池（Cards.json 按三套新构建器标签过滤；下标红/绿/蓝——2026-10-22 起一代 Theme* 卡已清池）。</summary>
        private static List<CardData>[] ThemeColorPools()
        {
            var tags = new[] { "ThemeRedClash", "ThemeGreenCountdown", "ThemeBlueSurplus" };
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
