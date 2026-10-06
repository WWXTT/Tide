using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UButton = UnityEngine.UI.Button;

namespace SynergyUI
{
    /// <summary>
    /// 挑战模式界面（2026-10-06）：选卡组 + 选难度（1-9 单值联动）后进战场。
    /// 纯代码屏（PrefabName=null，UiKit 构建）——节点按名稳定（deck-row-*、btn-diff-*、
    /// lbl-preview 等），验证走层级/字符串断言（截图验收禁令口径）。
    /// 难度语义：电脑地牌槽上限 9+N、起手 6+N（GameCore.InitGame 加成参数 +
    /// ResourceCurve.ChallengeLandCurve）；九档全开自由选，战绩落 ChallengeProgressManager。
    /// </summary>
    public sealed class ChallengeScreen : UIScreen
    {
        protected override string RootName => "challenge-screen";

        private UiKit.Scroll _deckList;
        private TMP_Text _preview;
        private readonly Dictionary<int, UButton> _diffButtons = new Dictionary<int, UButton>();
        private readonly Dictionary<string, UButton> _deckRows = new Dictionary<string, UButton>();
        private UButton _randomDeckRow;

        private string _selectedDeckName; // null=随机卡组
        private int _difficulty = 1;

        private const string RandomDeckLabel = "随机卡组";

        protected override void Build()
        {
            Root = UiKit.Screen(RootName, Parent);

            // ---- 顶栏 ----
            var top = UiKit.Row("topbar", Root, spacing: 8f);
            UiKit.Button("btn-back", top, "返回", () => Manager.Back(), width: 72f);
            var title = UiKit.Label("title", top, "挑战模式", UiStyle.HeaderSize, UiStyle.TextPrimary);
            UiKit.Size(title, w: 180f);
            UiKit.Label("subtitle", top, "自选卡组 vs 强化电脑——难度越高，电脑地牌槽与起手越多",
                UiStyle.SmallSize, UiStyle.TextFaint);

            // ---- 卡组选择 ----
            UiKit.Label("deck-header", Root, "选择你的卡组（在「卡组构筑」里保存）", UiStyle.BodySize, UiStyle.TextBody);
            _deckList = UiKit.ScrollColumn("list-decks", Root, spacing: 4f, pad: 4f);
            UiKit.Size(_deckList.Rect, fw: 1f, h: 150f);

            // ---- 难度选择 ----
            var diffHead = UiKit.Row("diff-header", Root, spacing: 8f);
            UiKit.Label("diff-title", diffHead, "难度", UiStyle.BodySize, UiStyle.TextBody);
            UiKit.Label("diff-note", diffHead, "电脑：地牌槽上限 9+N · 起手 6+N（N=难度）",
                UiStyle.SmallSize, UiStyle.TextFaint);
            var diffRow = UiKit.Row("diff-row", Root, spacing: 6f);
            for (int d = 1; d <= ChallengeProgressManager.MaxDifficulty; d++)
            {
                var captured = d;
                var btn = UiKit.MiniButton($"btn-diff-{captured}", diffRow, captured.ToString(),
                    () => SelectDifficulty(captured), width: 34f);
                _diffButtons[captured] = btn;
            }

            // ---- 预览与开战 ----
            _preview = UiKit.Label("lbl-preview", Root, "", UiStyle.BodySize, UiStyle.TextBody);
            UiKit.Size(_preview, fw: 1f);
            UiKit.Button("btn-start", Root, "开战", OnStart, bg: UiStyle.BtnPrimary, width: 200f);
        }

        public override void OnEnter()
        {
            RefreshDeckList();
            SelectDifficulty(1);
        }

        // ======================================== 卡组列表 ========================================

        private void RefreshDeckList()
        {
            ClearChildren(_deckList.Content);
            _deckRows.Clear();
            _randomDeckRow = null;

            // 随机卡组行（缺省选中——无存档卡组时的唯一选项）
            MakeDeckRow(RandomDeckLabel, null);
            foreach (var deck in DeckSerializer.LoadAll())
            {
                if (deck == null || string.IsNullOrEmpty(deck.name)) continue;
                MakeDeckRow(deck.name, deck);
            }
            SelectDeck(_selectedDeckName); // 保持上次选择（缺失回落随机）
        }

        /// <summary>卡组行（含张数；行点击=选中，Outline 高亮）。</summary>
        private void MakeDeckRow(string displayName, DeckData deck)
        {
            var row = UiKit.Row($"deck-row-{Sanitize(displayName)}", _deckList.Content, spacing: 8f, pad: 6f);
            UiKit.BgRow(row);
            UiKit.Size(row, h: 34f);
            var name = UiKit.Label("name", row, displayName, UiStyle.BodySize, UiStyle.TextBody);
            UiKit.Size(name, fw: 1f);
            UiKit.Label("meta", row, deck?.cardIds != null ? $"{deck.cardIds.Count} 张" : "30 张随机",
                UiStyle.SmallSize, UiStyle.TextFaint);

            var rowBtn = row.gameObject.AddComponent<Button>();
            rowBtn.transition = Selectable.Transition.None;
            rowBtn.targetGraphic = row.GetComponent<Image>();
            rowBtn.onClick.AddListener(() => SelectDeck(deck?.name)); // 随机行传 null

            if (deck == null)
                _randomDeckRow = rowBtn;
            else
                _deckRows[deck.name] = rowBtn;
        }

        private void SelectDeck(string deckName)
        {
            if (deckName != null && !_deckRows.ContainsKey(deckName)) deckName = null;
            _selectedDeckName = deckName;
            foreach (var kv in _deckRows)
                Highlight(kv.Value, kv.Key == deckName);
            if (_randomDeckRow != null)
                Highlight(_randomDeckRow, deckName == null);
            RefreshPreview();
        }

        // ======================================== 难度 ========================================

        private void SelectDifficulty(int d)
        {
            _difficulty = Mathf.Clamp(d, 1, ChallengeProgressManager.MaxDifficulty);
            foreach (var kv in _diffButtons)
                Highlight(kv.Value, kv.Key == _difficulty);
            RefreshPreview();
        }

        private void RefreshPreview()
        {
            var progress = ChallengeProgressManager.Instance;
            string deckText = _selectedDeckName ?? RandomDeckLabel;
            _preview.text = $"卡组：{deckText}　难度 {_difficulty}：电脑地牌槽上限 {9 + _difficulty}、起手 {6 + _difficulty} 张"
                + $"（战绩 {progress.RecordText(_difficulty)}）";
        }

        // ======================================== 开战 ========================================

        private void OnStart()
        {
            List<CardData> deck = null;
            if (_selectedDeckName != null)
            {
                var data = DeckSerializer.Load(_selectedDeckName);
                if (data?.cardIds != null)
                    deck = TutorialLibrary.BuildDeck(data.cardIds);
                if (deck == null || deck.Count < GameCore.OpeningHandSize)
                {
                    TideLog.Warn($"[ChallengeScreen] 卡组 {_selectedDeckName} 不可用（缺卡/张数不足）——回落随机卡组");
                    deck = null;
                }
            }

            BattleEntry.Mode = BattleMode.Challenge;
            BattleEntry.ChallengeDifficulty = _difficulty;
            BattleEntry.ChallengeDeck = deck; // null=StartLocalFan 兜底随机
            BattleStageDirector.SwallowToBattleAsync(() => Manager.Show<BattleScreen>()).Forget();
        }

        // ======================================== 小件 ========================================

        /// <summary>选中高亮：Outline 开关（DeckBuilderScreen 过滤 chip 同口径）。</summary>
        private static void Highlight(UButton btn, bool active)
        {
            if (btn == null) return;
            var ol = btn.GetComponent<Outline>();
            if (active && ol == null)
            {
                ol = btn.gameObject.AddComponent<Outline>();
                ol.effectColor = UiStyle.TextPrimary;
                ol.effectDistance = new Vector2(1.5f, -1.5f);
            }
            if (ol != null) ol.enabled = active;
        }

        /// <summary>节点名安全化（中文卡组名直接可用；只清路径非法字符）。</summary>
        private static string Sanitize(string name)
        {
            return string.IsNullOrEmpty(name) ? "空" : name.Replace('/', '_').Replace('\\', '_');
        }

        /// <summary>清空容器直系子节点（DeckBuilderScreen 同款幂等清理）。</summary>
        private static void ClearChildren(RectTransform container)
        {
            if (container == null) return;
            for (int i = container.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(container.GetChild(i).gameObject);
        }
    }
}
