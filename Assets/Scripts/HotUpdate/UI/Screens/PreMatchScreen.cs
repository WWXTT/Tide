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
    /// 对局准备界面（2026-10-17 对局入口改版定案；2026-10-19 三课制双选卡组版）：先掷硬币定先后
    /// → 为双方各选一套卡组 → 开局。纯代码屏（PrefabName=null，UiKit 构建，ChallengeScreen 同款范式）。
    /// 硬币：50/50 随机（FirstPlayerIsPlayer 注入 BattleScreen→StartNewGame，后手角色 +5 护甲补偿）。
    /// 卡组（2026-10-19 双选定案）：我方与对方（AI）各一列，均列正式卡组文件夹全部卡组
    /// （DeckSerializer.LoadAll）——玩家替 AI 选定对手卡组；全缺=随机卡组兜底行。
    /// 主题行不做构成校验拦截（镜像/教学链同口径——BuildDeck 容忍断链，张数≥起手即用）。
    ///
    /// 教学三变体（2026-10-18 末日降临挑战定案，2026-10-19 随课序挪位更名 Teaching3）：
    /// Teaching3=true 时只列我方卡组（对方=复制我方——镜像开局）；硬币落定起 10 秒倒计时
    /// （归零=未选随机选定并自动开战）；开战写 BattleEntry.Mode=Tutorial
    /// + TutorialId=lunzhuan（AI 第一回合幻卡清算镜像卡组、召唤末日降临——见 ScriptedAi phantom）。
    /// 会话标记是静态持留（非一次性）：教学局败局 Back 回本屏重挑仍保持教学三模式，
    /// 由普通匹配入口（btn-battle/btn-match）/主菜单分叉点显式复位。
    /// </summary>
    public sealed class PreMatchScreen : UIScreen
    {
        protected override string RootName => "prematch-screen";

        private const string RandomDeckLabel = "随机卡组";

        /// <summary>教学三会话标记（2026-10-19 课序挪位：原 Teaching2）：true=教学三变体
        /// （第三课末日降临挑战——我方单列+10 秒倒计时+AI 镜像+Tutorial 局）。置位方=MainMenuScreen
        /// 教学分叉/败局重挑回流；复位方=本地对战入口（btn-battle）——静态持留跨屏存活，不随 OnExit 清。</summary>
        public static bool Teaching3;

        /// <summary>教学三选卡组倒计时秒数（2026-10-18 用户定案：10 秒等待双方选定）。</summary>
        private const float Teaching3PickSeconds = 10f;

        private TMP_Text _coinLabel;            // 硬币面（先/后）
        private TMP_Text _coinResult;           // 硬币结果横幅
        private UButton _coinButton;
        private TMP_Text _aiPick;               // 双方所选卡组汇总提示
        private TMP_Text _countdownLabel;       // 教学三选卡组倒计时（非教学三恒隐藏）
        private UButton _startButton;
        private UiKit.Scroll _deckList;         // 我方卡组列
        private RectTransform _aiDeckSection;   // 对方卡组列（教学三隐藏）
        private UiKit.Scroll _aiDeckList;        // 对方卡组列（普通双选）
        private RectTransform _deckSection;
        private readonly Dictionary<string, UButton> _deckRows = new Dictionary<string, UButton>();
        private readonly Dictionary<string, UButton> _aiDeckRows = new Dictionary<string, UButton>();

        private bool _coinDone;                 // 硬币掷完才解锁选卡组/开战
        private bool _coinFlipping;
        private bool? _playerFirst;
        private string _selectedDeckName;       // null=随机卡组
        private string _aiDeckName;             // 对方（AI）卡组（教学三=镜像我方；普通=玩家替它选；null=随机）
        private bool _teaching3;                // 本屏实例模式快照（OnEnter 时取 Teaching3）
        private bool _countdownRunning;         // 教学三倒计时在途（OnExit/开战即停）
        private bool _startFired;               // 开战去重（倒计时归零与手点竞态）
        private readonly List<string> _availableDecks = new List<string>(); // 可选真实卡组名（不含兜底行，倒计时随机用）

        protected override void Build()
        {
            Root = UiKit.Screen(RootName, Parent);

            // ---- 顶栏 ----
            var top = UiKit.Row("topbar", Root, spacing: 8f);
            UiKit.Button("btn-back", top, "返回", () => Manager.Back(), width: 72f);
            var title = UiKit.Label("title", top, "对局准备", UiStyle.HeaderSize, UiStyle.TextPrimary);
            UiKit.Size(title, w: 180f);
            UiKit.Label("subtitle", top, "掷硬币定先后手（后手角色开局 +5 护甲），再为双方选定卡组",
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

            // ---- 阶段二：双列卡组（我方 / 对方） ----
            _deckSection = UiKit.Column("deck-section", Root, spacing: 6f);
            UiKit.Size(_deckSection, fw: 1f);
            _countdownLabel = UiKit.Label("lbl-countdown", _deckSection, "", UiStyle.BodySize, UiStyle.TextPrimary);
            UiKit.Size(_countdownLabel, fw: 1f);
            var decksRow = UiKit.Row("decks-row", _deckSection, spacing: 8f);
            UiKit.Size(decksRow, fw: 1f);

            var myCol = UiKit.Column("my-deck-col", decksRow, spacing: 4f);
            UiKit.Size(myCol, fw: 0.5f);
            UiKit.Label("deck-header", myCol, "选择你的卡组", UiStyle.BodySize, UiStyle.TextBody);
            _deckList = UiKit.ScrollColumn("list-decks", myCol, spacing: 4f, pad: 4f);
            UiKit.Size(_deckList.Rect, fw: 1f, h: 170f);

            _aiDeckSection = UiKit.Column("ai-deck-col", decksRow, spacing: 4f);
            UiKit.Size(_aiDeckSection, fw: 0.5f);
            UiKit.Label("ai-deck-header", _aiDeckSection, "替对方（AI）选定卡组", UiStyle.BodySize, UiStyle.TextBody);
            _aiDeckList = UiKit.ScrollColumn("list-ai-decks", _aiDeckSection, spacing: 4f, pad: 4f);
            UiKit.Size(_aiDeckList.Rect, fw: 1f, h: 170f);

            _aiPick = UiKit.Label("lbl-ai-pick", _deckSection, "", UiStyle.SmallSize, UiStyle.TextFaint);
            UiKit.Size(_aiPick, fw: 1f);

            // ---- 开战 ----
            _startButton = UiKit.Button("btn-start", Root, "开始对局", OnStart,
                bg: UiStyle.BtnPrimary, width: 200f);
        }

        public override void OnEnter()
        {
            _teaching3 = Teaching3; // 模式快照（重挑回流时静态标记仍为 true）
            _coinDone = false;
            _coinFlipping = false;
            _playerFirst = null;
            _selectedDeckName = null;
            _aiDeckName = null;
            _startFired = false;
            _countdownRunning = false;
            _coinLabel.text = "？";
            _coinLabel.transform.localScale = Vector3.one;
            _coinResult.text = _teaching3
                ? "教学三·末日降临挑战——先掷硬币定先后手"
                : "先掷硬币，赢的一方先行动";
            _countdownLabel.text = "";
            _aiPick.text = "";
            if (_aiDeckSection != null)
                _aiDeckSection.gameObject.SetActive(!_teaching3); // 教学三=对方复制我方，不替它选
            _coinButton.interactable = true;
            _startButton.interactable = false;
            RefreshDeckList();
            RefreshLocks();
        }

        public override void OnExit()
        {
            _coinFlipping = false; // 在途翻转动画作废（屏销毁后 UiTask 自弃）
            _countdownRunning = false; // 在途倒计时作废（开战切屏/返回均停）
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
            if (_teaching3)
            {
                // 教学三：对方复制我方卡组（镜像开局——内容随后被幻卡清算，仅起手画面可见）；
                // 选卡组 10 秒倒计时同步开跑（归零=未选随机选定并自动开战）
                _aiDeckName = _selectedDeckName;
                _aiPick.text = "对方复制了你的卡组（10 秒内选定，超时自动随机）";
                StartCountdownAsync().Forget();
            }
            RefreshAiPickText();
            RefreshLocks();
        }

        /// <summary>教学三选卡组倒计时（2026-10-18 用户定案：10 秒等待双方选定）：每帧刷剩余秒
        /// （向上取整显示），归零=未选则随机选定并自动开战。开战/离屏即停（_countdownRunning）。</summary>
        private async UniTaskVoid StartCountdownAsync()
        {
            _countdownRunning = true;
            float t0 = Time.realtimeSinceStartup;
            while (_countdownRunning && Time.realtimeSinceStartup - t0 < Teaching3PickSeconds)
            {
                int remain = Mathf.CeilToInt(Teaching3PickSeconds - (Time.realtimeSinceStartup - t0));
                _countdownLabel.text = $"选卡组倒计时 {remain} 秒";
                await UniTask.Yield();
            }
            if (!_countdownRunning) return; // 已开战/离屏：作废
            _countdownRunning = false;
            _countdownLabel.text = "倒计时归零——自动开战";
            if (_selectedDeckName == null && _availableDecks.Count > 0)
                SelectDeck(_availableDecks[UnityEngine.Random.Range(0, _availableDecks.Count)]); // 随机一张真实卡组
            OnStart();
        }

        // ======================================== 卡组列表 ========================================

        /// <summary>正式卡组文件夹全部卡组名（双列同源；DeckSerializer.LoadAll 按名扫描 CardDir，
        /// Cards/Effects.json 无顶层 name 天然被滤；主题三套在内的已存卡组全列）。</summary>
        private static List<string> AllSavedDecks()
            => DeckSerializer.LoadAll()
                .Where(d => d != null && !string.IsNullOrEmpty(d.name))
                .Select(d => d.name)
                .ToList();

        private void RefreshDeckList()
        {
            ClearChildren(_deckList.Content);
            ClearChildren(_aiDeckList.Content);
            _deckRows.Clear();
            _aiDeckRows.Clear();

            var available = AllSavedDecks();
            _availableDecks.Clear();
            _availableDecks.AddRange(available);
            // 双列同源（我方 + 替对方选）+ 随机兜底行（全缺时的唯一选项）
            foreach (var name in available)
            {
                MakeDeckRow(_deckList.Content, _deckRows, name, DeckSerializer.Load(name), () => SelectDeck(name));
                MakeDeckRow(_aiDeckList.Content, _aiDeckRows, name, DeckSerializer.Load(name), () => SelectAiDeck(name));
            }
            if (available.Count == 0)
            {
                MakeDeckRow(_deckList.Content, _deckRows, RandomDeckLabel, null, () => SelectDeck(null));
                MakeDeckRow(_aiDeckList.Content, _aiDeckRows, RandomDeckLabel, null, () => SelectAiDeck(null));
            }
            SelectDeck(null); // 缺省随机
            SelectAiDeck(null);
        }

        /// <summary>卡组行（含张数；点击=选中 Outline 高亮——ChallengeScreen 同口径）。</summary>
        private void MakeDeckRow(RectTransform container, Dictionary<string, UButton> rows,
            string displayName, DeckData deck, Action onClick)
        {
            var row = UiKit.Row($"deck-row-{Sanitize(displayName)}", container, spacing: 8f, pad: 6f);
            UiKit.BgRow(row);
            UiKit.Size(row, h: 34f);
            var name = UiKit.Label("name", row, displayName, UiStyle.BodySize, UiStyle.TextBody);
            UiKit.Size(name, fw: 1f);
            UiKit.Label("meta", row, deck?.cardIds != null ? $"{deck.cardIds.Count} 张" : "30 张随机",
                UiStyle.SmallSize, UiStyle.TextFaint);

            var rowBtn = row.gameObject.AddComponent<UButton>();
            rowBtn.transition = Selectable.Transition.None;
            rowBtn.targetGraphic = row.GetComponent<Image>();
            rowBtn.onClick.AddListener(() => onClick());
            rows[displayName] = rowBtn;
        }

        private void SelectDeck(string deckName)
        {
            if (deckName != null && !_deckRows.ContainsKey(deckName)) deckName = null;
            _selectedDeckName = deckName;
            foreach (var kv in _deckRows)
                Highlight(kv.Value, kv.Key == deckName);
            if (_teaching3)
                _aiDeckName = deckName; // 镜像同步（幻卡清算前对手用的就是你选的）
            RefreshAiPickText();
            RefreshLocks();
        }

        /// <summary>替对方选卡组（2026-10-19 双选定案：普通模式专用；教学三列隐藏不会被点到）。</summary>
        private void SelectAiDeck(string deckName)
        {
            if (_teaching3) return; // 教学三：对方恒镜像我方
            if (deckName != null && !_aiDeckRows.ContainsKey(deckName)) deckName = null;
            _aiDeckName = deckName;
            foreach (var kv in _aiDeckRows)
                Highlight(kv.Value, kv.Key == deckName);
            RefreshAiPickText();
            RefreshLocks();
        }

        private void RefreshAiPickText()
        {
            if (!_coinDone) { if (!_teaching3) _aiPick.text = ""; return; }
            if (_teaching3)
                _aiPick.text = $"对方复制了你的卡组　我方：{_selectedDeckName ?? RandomDeckLabel}";
            else
                _aiPick.text = $"对方（AI）：{_aiDeckName ?? RandomDeckLabel}　我方：{_selectedDeckName ?? RandomDeckLabel}";
        }

        // ======================================== 开战 ========================================

        private void OnStart()
        {
            if (!_coinDone || _startFired) return;
            _startFired = true;     // 倒计时归零与手点开战竞态去重
            _countdownRunning = false; // 在途倒计时作废（label 停在当前文案无害）

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

            if (_teaching3)
            {
                // 教学三（2026-10-18 末日降临挑战，2026-10-19 挪入第三课）：对方卡组=直接复制我方
                //（镜像）——内容随后被幻卡清算烧掉，正主威胁=末日降临（ScriptedAi phantom 剧本）。
                // Mode=Tutorial 走 BattleScreen 教学装配（引导+课规+剧本 AI）。
                aiDeck = myDeck;
                aiSkill = mySkill;
                BattleEntry.Mode = BattleMode.Tutorial;
                BattleEntry.TutorialId = TutorialProgressManager.LunzhuanId;
                BattleEntry.MatchDeck = myDeck; // null=StartLocalFan 兜底随机（教学链再回落普通局）
                BattleEntry.MatchSkillCardId = mySkill;
                BattleEntry.AiDeck = aiDeck;
                BattleEntry.AiSkillCardId = aiSkill;
                BattleEntry.FirstPlayerIsPlayer = _playerFirst;
                BattleStageDirector.SwallowToBattleAsync(() => Manager.Show<BattleScreen>()).Forget();
                return;
            }

            // 普通本地对战（2026-10-19 双选定案）：对方卡组=玩家替它选的那套（缺省随机兜底）
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
            foreach (var kv in _aiDeckRows)
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
