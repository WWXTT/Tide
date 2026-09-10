using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CardCore;
using CardCore.AI.NeuralEnv;
using UnityEditor;
using UnityEngine;

namespace CardCore.Editor.Tests
{
    /// <summary>
    /// AI 对战选卡组窗口：两个玩家各拖一套卡组 JSON 运行对战，或勾选「随机卡组」
    /// （同训练测试口径：双方各随一色 Deck_Red/Blue/Green 洗牌抽 30 张）。
    /// 每个玩家独立选大脑：脚本 SimpleAI 或 AI 神经网络（Resources/tide_policy，ModelAsset）。
    /// 复用双卡组化的 AiBattleDriver.RunFullGame（玩家 1/玩家 2 各自加载）。
    /// 拖放收 .json（文件路径或项目里的 TextAsset），「浏览…」按钮兜底。
    /// </summary>
    public class AiBattleDeckWindow : EditorWindow
    {
        private static readonly string[] BrainLabels = { "脚本 SimpleAI", "AI 神经网络" };
        private static readonly string[] ColorNames = { "红", "蓝", "绿" }; // 下标对齐 LoadColorPools

        private const int DeckSize = 30; // 训练口径卡组张数

        private string _p1Path = "";
        private string _p2Path = "";
        private int _maxTurns = 100;
        private bool _randomDeck;   // 开：卡组不走拖入路径，按训练测试口径随机
        private int _p1Brain;       // 0=脚本 1=AI（对齐 AiBrain 枚举值）
        private int _p2Brain;

        [MenuItem("Tools/AI 自动对战（选卡组）")]
        public static void Open() => GetWindow<AiBattleDeckWindow>("AI 自动对战（选卡组）");

        private void OnGUI()
        {
            EditorGUILayout.LabelField("双方卡组", EditorStyles.boldLabel);
            _randomDeck = EditorGUILayout.ToggleLeft(
                new GUIContent("随机卡组（同训练测试：双方各随一色、洗牌抽 30 张）"), _randomDeck);

            using (new EditorGUI.DisabledScope(_randomDeck))
            {
                DrawDeckZone("玩家 1", ref _p1Path);
                DrawDeckZone("玩家 2", ref _p2Path);
            }

            EditorGUILayout.Space();
            _p1Brain = EditorGUILayout.Popup("玩家 1 大脑", _p1Brain, BrainLabels);
            _p2Brain = EditorGUILayout.Popup("玩家 2 大脑", _p2Brain, BrainLabels);

            EditorGUILayout.Space();
            _maxTurns = EditorGUILayout.IntField("最大回合数", _maxTurns);
            _maxTurns = Mathf.Max(1, _maxTurns);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(!_randomDeck && (!HasDeck(_p1Path) || !HasDeck(_p2Path))))
            {
                if (GUILayout.Button("运行对战", GUILayout.Height(32)))
                    RunBattle();
            }
        }

        private static void DrawDeckZone(string label, ref string path)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
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

        private void RunBattle()
        {
            List<CardData> p1;
            List<CardData> p2;
            if (_randomDeck)
            {
                var colorPools = LoadColorPools();
                int[] nonEmpty = Enumerable.Range(0, colorPools.Length)
                    .Where(i => colorPools[i].Count > 0).ToArray();
                if (nonEmpty.Length == 0)
                {
                    EditorUtility.DisplayDialog("随机卡组", "Configs/TestDecks 下没有可用的 Deck_Red/Blue/Green.json 卡池。", "好");
                    return;
                }
                int c1 = nonEmpty[UnityEngine.Random.Range(0, nonEmpty.Length)];
                int c2 = nonEmpty[UnityEngine.Random.Range(0, nonEmpty.Length)];
                var rng = new System.Random(); // 单实例双采样（两个新实例可能同刻同种子 → 同洗牌序列）
                p1 = SampleDeck(rng, colorPools[c1]);
                p2 = SampleDeck(rng, colorPools[c2]);
                Debug.Log($"[AI 对战] 随机卡组：玩家1={ColorNames[c1]}色 {p1.Count} 张，玩家2={ColorNames[c2]}色 {p2.Count} 张");
            }
            else
            {
                if (!HasDeck(_p1Path) || !HasDeck(_p2Path))
                {
                    EditorUtility.DisplayDialog("运行对战", "卡组 JSON 文件不存在，请重新选择。", "好");
                    return;
                }
                try
                {
                    p1 = CardLoader.LoadCardsFromText(File.ReadAllText(_p1Path));
                    p2 = CardLoader.LoadCardsFromText(File.ReadAllText(_p2Path));
                }
                catch (Exception ex)
                {
                    EditorUtility.DisplayDialog("运行对战", $"卡组解析失败：{ex.Message}", "好");
                    return;
                }
            }

            if (p1 == null || p1.Count == 0 || p2 == null || p2.Count == 0)
            {
                EditorUtility.DisplayDialog("运行对战", "卡组解析后为空，检查 JSON 结构（cards 数组）。", "好");
                return;
            }

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
                new AiBattleDriver().RunFullGame(p1, p2, _maxTurns, brain1, brain2, policy);
            }
            finally
            {
                policy?.Dispose();
            }
        }

        /// <summary>ONNX 卡身份登记（镜像 TideHeadlessServer.HandleReset / 训练服务器口径）：
        /// manifest 追加式幂等；表指纹不符只告警不阻断（embedding 行可能串台，建议重导出）。</summary>
        private static void ConfigureNeuralIdentity(List<CardData> p1, List<CardData> p2)
        {
            int loaded = TideCardIndex.ConfigureManifest(ManifestPath);
            Debug.Log(loaded >= 0
                ? $"[AI 对战] 卡身份清单载入 {loaded} 条（{ManifestPath}）"
                : "[AI 对战] ⚠ manifest 表指纹不符——模型 embedding 行可能串台，建议重导出");
            var pool = AiBattleE2E.LoadStandardDeck();
            if (pool.Count > 0) TideCardIndex.Register(pool);
            foreach (var cp in LoadColorPools()) TideCardIndex.Register(cp);
            TideCardIndex.Register(p1); // 实战卡组也登记：拖入卡组可能带池外新卡（追加分配不改旧下标）
            TideCardIndex.Register(p2);
        }

        // ======================================== 训练口径辅助（原 OnnxPolicyMenu，随工具精简并入） ========================================

        /// <summary>训练侧卡身份清单（追加式幂等）。</summary>
        private static string ManifestPath
            => Path.Combine(Application.dataPath, "..", "tide_rl", "card_identity_manifest.json");

        /// <summary>三色主题卡池（镜像 TideHeadlessServer.ColorPools：Deck_*.json；下标对齐红/蓝/绿）。</summary>
        private static List<CardData>[] LoadColorPools()
        {
            var files = new[] { "Deck_Red.json", "Deck_Blue.json", "Deck_Green.json" };
            var pools = new List<CardData>[files.Length];
            var dir = Path.Combine(Application.dataPath, "Configs", "TestDecks");
            for (int i = 0; i < files.Length; i++)
            {
                var path = Path.Combine(dir, files[i]);
                pools[i] = File.Exists(path)
                    ? CardLoader.LoadCardsFromText(File.ReadAllText(path))
                    : new List<CardData>();
            }
            return pools;
        }

        /// <summary>洗牌抽前 30（无放回，镜像 TideHeadlessServer.SampleRandomDeck / 训练评估口径）。</summary>
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
