using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UButton = UnityEngine.UI.Button;

namespace SynergyUI
{
    /// <summary>
    /// 对局准备界面（2026-10-17 对局入口改版定案）：先掷硬币定先后手 → 各自选卡组 → 开局。
    /// 纯代码屏（PrefabName=null，UiKit 构建，ChallengeScreen 同款范式）。
    /// 硬币：50/50 随机（FirstPlayerIsPlayer 注入 BattleScreen→StartNewGame，后手角色 +5 护甲补偿）。
    /// 卡组：主题色卡组三套（红·快攻/绿·慢速/蓝·控制 ∩ 存档现存者，其余两套用户后续补齐）；
    /// 全缺=随机卡组兜底行。AI 从同一集合随机选（测试期=红·快攻镜像）。
    /// 主题行不做构成校验拦截（镜像/考核链同口径——BuildDeck 容忍断链，张数≥起手即用）。
    /// </summary>
    public sealed class PreMatchScreen : UIScreen
    {
        protected override string RootName => "prematch-screen";

        /// <summary>主题色卡组名单（与 ThemeDeckBuilder 产出的三套同名；用户后续补齐绿/蓝）。</summary>
        private static readonly string[] ThemeDeckNames = { "红·快攻", "绿·慢速", "蓝·控制" };

        private const string RandomDeckLabel = "随机卡组";

        private TMP_Text _coinLabel;            // 硬币面（先/后）
        private TMP_Text _coinResult;           // 硬币结果横幅
        private UButton _coinButton;
        private TMP_Text _aiPick;               // 对方所选卡组提示
        private UButton _startButton;
        private UiKit.Scroll _deckList;
        private RectTransform _deckSection;
        private readonly Dictionary<string, UButton> _deckRows = new Dictionary<string, UButton>();

        private bool _coinDone;                 // 硬币掷完才解锁选卡组/开战
        private bool _coinFlipping;
        private bool? _playerFirst;
        private string _selectedDeckName;       // null=随机卡组
        private string _aiDeckName;             // AI 选中的主题名（null=随机）

        protected override void Build()
        {
            Root = UiKit.Screen(RootName, Parent);

            // ---- 顶栏 ----
            var top = UiKit.Row("topbar", Root, spacing: 8f);
            UiKit.Button("btn-back", top, "返回", () => Manager.Back(), width: 72f);
            var title = UiKit.Label("title", top, "对局准备", UiStyle.HeaderSize, UiStyle.TextPrimary);
            UiKit.Size(title, w: 180f);
            UiKit.Label("subtitle", top, "掷硬币定先后手（后手角色开局 +5 护甲），再各自选卡组",
                UiStyle.SmallSize, UiStyle.TextFaint);

            // ---- 阶段一：硬币 ----
            var coinRow = UiKit.Row("coin-row", Root, spacing: 14f);
            UiKit.Size(coinRow, h: 64f);
            _coinButton = UiKit.Button("btn-coin", coinRow, "掷硬币", OnCoinClick, width: 120f, height: 48f);
            var coinFace = UiKit.Row("coin-face", coinRow, pad: 8f);
            UiKit.BgRow(coinFace);
            UiKit.Size(coinFace, w: 120f, h: 48f);
            _coinLabel = UiKit.Label("face", coinFace, "？", UiStyle.HeaderSize, UiStyle.TextPrimary);
            _coinResult = UiKit.Label("lbl-coin-result", coinRow, "先掷硬币，赢的一方先行动",
                UiStyle.BodySize, UiStyle.TextBody);
            UiKit.Size(_coinResult, fw: 1f);

            // ---- 阶段二：卡组 ----
            _deckSection = UiKit.Column("deck-section", Root, spacing: 6f);
            UiKit.Size(_deckSection, fw: 1f);
            UiKit.Label("deck-header", _deckSection, "选择你的卡组", UiStyle.BodySize, UiStyle.TextBody);
            _deckList = UiKit.ScrollColumn("list-decks", _deckSection, spacing: 4f, pad: 4f);
            UiKit.Size(_deckList.Rect, fw: 1f, h: 150f);
            _aiPick = UiKit.Label("lbl-ai-pick", _deckSection, "", UiStyle.SmallSize, UiStyle.TextFaint);
            UiKit.Size(_aiPick, fw: 1f);

            // ---- 开战 ----
            _startButton = UiKit.Button("btn-start", Root, "开始对局", OnStart,
                bg: UiStyle.BtnPrimary, width: 200f);
        }

        public override void OnEnter()
        {
            _coinDone = false;
            _coinFlipping = false;
            _playerFirst = null;
            _selectedDeckName = null;
            _aiDeckName = null;
            _coinLabel.text = "？";
            _coinLabel.transform.localScale = Vector3.one;
            _coinResult.text = "先掷硬币，赢的一方先行动";
            _aiPick.text = "";
            _coinButton.interactable = true;
            _startButton.interactable = false;
            RefreshDeckList();
            RefreshLocks();
        }

        public override void OnExit()
        {
            _coinFlipping = false; // 在途翻转动画作废（屏销毁后 UiTask 自弃）
        }

        // ======================================== 硬币 ========================================

        private void OnCoinClick()
        {
            if (_coinDone || _coinFlipping) return;
            _coinFlipping = true;
            _coinButton.interactable = false;
            FlipCoinAsync().Forget();
        }

        /// <summary>硬币翻转（~1.2s，scale.x 振荡模拟），落面 50/50 随机：先=我方先手。</summary>
        private async UniTaskVoid FlipCoinAsync()
        {
            bool playerFirst = UnityEngine.Random.value < 0.5f;
            float t0 = Time.realtimeSinceStartup;
            const float duration = 1.2f;
            while (Time.realtimeSinceStartup - t0 < duration)
            {
                if (!_coinFlipping) return; // 切屏作废
                // 振荡翻转：scale.x = |cos|（0=侧立 1=平展）
                float phase = (Time.realtimeSinceStartup - t0) * 14f;
                _coinLabel.transform.localScale = new Vector3(Mathf.Max(0.08f, Mathf.Abs(Mathf.Cos(phase))), 1f, 1f);
                await UniTask.Yield();
            }
            _coinFlipping = false;
            _coinDone = true;
            _playerFirst = playerFirst;
            _coinLabel.transform.localScale = Vector3.one;
            _coinLabel.text = playerFirst ? "先" : "后";
            _coinResult.text = playerFirst ? "我方先手！" : "对方先手（我方角色开局 +5 护甲）";
            _aiDeckName = PickAiDeck(); // 对方同场选卡组
            _aiPick.text = _aiDeckName != null
                ? $"对方选择了：{_aiDeckName}（选我方卡组后开始）"
                : $"对方选择了：{RandomDeckLabel}（选我方卡组后开始）";
            RefreshLocks();
        }

        /// <summary>AI 选卡组：可用主题集内随机（测试期只有红·快攻=恒选它；空集=随机）。</summary>
        private string PickAiDeck()
        {
            var names = AvailableThemeDecks();
            if (names.Count == 0) return null;
            return names[UnityEngine.Random.Range(0, names.Count)];
        }

        // ======================================== 卡组列表 ========================================

        private List<string> AvailableThemeDecks()
            => ThemeDeckNames
                .Where(n => DeckSerializer.LoadAll().Any(d => d != null && d.name == n))
                .ToList();

        private void RefreshDeckList()
        {
            ClearChildren(_deckList.Content);
            _deckRows.Clear();

            var available = AvailableThemeDecks();
            // 主题行（测试期现存者才出现）+ 随机兜底行（主题全缺时的唯一选项）
            foreach (var name in available)
                MakeDeckRow(name, DeckSerializer.Load(name));
            if (available.Count == 0)
                MakeDeckRow(RandomDeckLabel, null);
            SelectDeck(null); // 缺省随机
        }

        /// <summary>卡组行（含张数与主题色标记；点击=选中 Outline 高亮——ChallengeScreen 同口径）。</summary>
        private void MakeDeckRow(string displayName, DeckData deck)
        {
            var row = UiKit.Row($"deck-row-{Sanitize(displayName)}", _deckList.Content, spacing: 8f, pad: 6f);
            UiKit.BgRow(row);
            UiKit.Size(row, h: 34f);
            var name = UiKit.Label("name", row, displayName, UiStyle.BodySize, UiStyle.TextBody);
            UiKit.Size(name, fw: 1f);
            UiKit.Label("meta", row, deck?.cardIds != null ? $"{deck.cardIds.Count} 张" : "30 张随机",
                UiStyle.SmallSize, UiStyle.TextFaint);

            var rowBtn = row.gameObject.AddComponent<UButton>();
            rowBtn.transition = Selectable.Transition.None;
            rowBtn.targetGraphic = row.GetComponent<Image>();
            rowBtn.onClick.AddListener(() => SelectDeck(deck?.name));
            _deckRows[displayName] = rowBtn;
        }

        private void SelectDeck(string deckName)
        {
            if (deckName != null && !_deckRows.ContainsKey(deckName)) deckName = null;
            _selectedDeckName = deckName;
            foreach (var kv in _deckRows)
                Highlight(kv.Value, kv.Key == deckName);
            _aiPick.text = _coinDone && _aiDeckName != null
                ? $"对方选择了：{_aiDeckName}　我方：{deckName ?? RandomDeckLabel}"
                : "";
            RefreshLocks();
        }

        // ======================================== 开战 ========================================

        private void OnStart()
        {
            if (!_coinDone) return;

            List<CardData> myDeck = null, aiDeck = null;
            string mySkill = null, aiSkill = null;
            if (_selectedDeckName != null)
            {
                var data = DeckSerializer.Load(_selectedDeckName);
                if (data?.cardIds != null)
                {
                    var built = TutorialLibrary.BuildDeck(data.cardIds);
                    if (built != null && built.Count >= GameCore.OpeningHandSize)
                    {
                        myDeck = built;
                        mySkill = data.skillCardId;
                    }
                    else
                        TideLog.Warn($"[PreMatch] 我方卡组 {_selectedDeckName} 缺卡/张数不足（{built?.Count ?? 0}）——回落随机");
                }
            }
            if (_aiDeckName != null)
            {
                var data = DeckSerializer.Load(_aiDeckName);
                if (data?.cardIds != null)
                {
                    var built = TutorialLibrary.BuildDeck(data.cardIds);
                    if (built != null && built.Count >= GameCore.OpeningHandSize)
                    {
                        aiDeck = built;
                        aiSkill = data.skillCardId;
                    }
                    else
                        TideLog.Warn($"[PreMatch] 对方卡组 {_aiDeckName} 缺卡/张数不足（{built?.Count ?? 0}）——回落随机");
                }
            }

            BattleEntry.Mode = BattleMode.LocalAI;
            BattleEntry.MatchDeck = myDeck; // null=StartLocalFan 兜底随机
            BattleEntry.MatchSkillCardId = mySkill;
            BattleEntry.AiDeck = aiDeck;
            BattleEntry.AiSkillCardId = aiSkill;
            BattleEntry.FirstPlayerIsPlayer = _playerFirst;
            BattleStageDirector.SwallowToBattleAsync(() => Manager.Show<BattleScreen>()).Forget();
        }

        // ======================================== 小件 ========================================

        /// <summary>阶段锁：硬币掷完才可选卡组/开战（选卡组行点击在未掷时静默）。</summary>
        private void RefreshLocks()
        {
            _startButton.interactable = _coinDone;
            foreach (var kv in _deckRows)
                kv.Value.interactable = _coinDone;
        }

        /// <summary>选中高亮：Outline 开关（ChallengeScreen 同口径）。</summary>
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
            if (string.IsNullOrEmpty(name)) return "deck";
            foreach (var c in System.IO.Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }

        private static void ClearChildren(RectTransform container)
        {
            for (int i = container.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(container.GetChild(i).gameObject);
        }
    }
}
