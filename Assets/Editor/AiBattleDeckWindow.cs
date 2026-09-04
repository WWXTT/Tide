using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CardCore;
using UnityEditor;
using UnityEngine;

namespace CardCore.Editor.Tests
{
    /// <summary>
    /// AI 对战选卡组窗口：两个玩家各拖一套卡组 JSON 运行对战。
    /// 复用双卡组化的 AiBattleDriver.RunFullGame（玩家 1/玩家 2 各自加载）。
    /// 拖放收 .json（文件路径或项目里的 TextAsset），「浏览…」按钮兜底；
    /// 「使用 TestDecks 随机卡组」回填 TestDecks 下随机一套卡组。
    /// </summary>
    public class AiBattleDeckWindow : EditorWindow
    {
        private string _p1Path = "";
        private string _p2Path = "";
        private int _maxTurns = 100;

        [MenuItem("Tools/卡牌核心/AI 自动对战（选卡组）")]
        public static void Open() => GetWindow<AiBattleDeckWindow>("AI 自动对战（选卡组）");

        private static string TestDecksDir
            => Path.Combine(Application.dataPath, "Configs", "TestDecks");

        /// <summary>随机挑一套 TestDecks 下的卡组 JSON；目录为空或不存在时返回 null。</summary>
        private static string PickRandomTestDeck()
        {
            if (!Directory.Exists(TestDecksDir)) return null;
            var decks = Directory.GetFiles(TestDecksDir, "*.json")
                .Where(f => !f.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            return decks.Length == 0 ? null : decks[UnityEngine.Random.Range(0, decks.Length)];
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("双方卡组（拖入 .json，或浏览选择）", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            DrawDeckZone("玩家 1", ref _p1Path);
            DrawDeckZone("玩家 2", ref _p2Path);

            EditorGUILayout.Space();
            _maxTurns = EditorGUILayout.IntField("最大回合数", _maxTurns);
            _maxTurns = Mathf.Max(1, _maxTurns);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(!HasDeck(_p1Path) || !HasDeck(_p2Path)))
            {
                if (GUILayout.Button("运行对战", GUILayout.Height(32)))
                    RunBattle();
            }

            if (GUILayout.Button("使用 TestDecks 随机卡组（双方各随机一套）"))
            {
                var d1 = PickRandomTestDeck();
                var d2 = PickRandomTestDeck();
                if (d1 == null || d2 == null)
                {
                    EditorUtility.DisplayDialog("随机卡组", "TestDecks 目录下没有卡组 JSON。", "好");
                    return;
                }
                _p1Path = d1;
                _p2Path = d2;
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
            if (!HasDeck(_p1Path) || !HasDeck(_p2Path))
            {
                EditorUtility.DisplayDialog("运行对战", "卡组 JSON 文件不存在，请重新选择。", "好");
                return;
            }

            List<CardData> p1;
            List<CardData> p2;
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

            if (p1 == null || p1.Count == 0 || p2 == null || p2.Count == 0)
            {
                EditorUtility.DisplayDialog("运行对战", "卡组解析后为空，检查 JSON 结构（cards 数组）。", "好");
                return;
            }

            new AiBattleDriver().RunFullGame(p1, p2, _maxTurns);
        }
    }
}
