using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace SynergyUI
{
    /// <summary>
    /// 战场五段式开局编排器（2026-10-19 掷币演出挪战场定案）：
    /// 1 地块就位（等 BattleBoardAssemble 浮起落位）→ 2 角色就位（立柱自板下升起；
    /// 教学三让位 boss intro 揭示段=跳过）→ 3 硬币先后手（镜头推近板心+BattleCoinToss）→
    /// 4 战场选卡组（BattleDeckPickStage·双选两轮/教学三单轮+倒计时）→ 5 交班段五：
    /// 组装卡组喂 BattleScreen.EngageLocalBattle（StartNewGame/教学装配/AI 先手 kickoff 全在段五）。
    ///
    /// 演出期任意点击=当前段快进（skip 旗标，段四选卡组除外——须真实输入）；相机推拉自持
    /// （快照/还原均为战场定档位姿，不碰 StageDirector 的菜单位姿快照）。纯 C# 类——依赖经 ctor
    /// 注入（BattleTutorialDirector 先例）；全程发生在引擎开局前，无 Core/无事件订阅可撞。
    /// 时序红线：段五前不 StartNewGame（_log.Wire/WireEarly 紧贴 StartNewGame 的既有次序由
    /// EngageLocalBattle 保持）；RefreshFieldWhenBoardReady 随段五起步（45s deadline 不在选卡组期空转）。
    /// </summary>
    public sealed class BattleOpeningDirector
    {
        private readonly Func<bool> _running;
        private readonly BattleFieldView _field;
        private readonly bool _lunzhuan;
        private readonly bool _dummy;          // 木桩测试局（2026-10-23）：段四改掷币后弹 UI 选卡窗
        private readonly Action<List<CardData>, string, List<CardData>, string, bool?> _engage;
        private readonly bool? _fixedPlayerFirst; // 木桩测试固定先后手（null=照常掷币）
        private readonly RectTransform _overlayParent;  // 木桩段四弹窗挂载父（BattleScreen.Root）
        private readonly Action _onCancelled;      // 木桩段四弹窗取消（返回主菜单）

        private BattleDeckPickStage _pickStage;
        private bool _deckPicking;        // 段四输入期（点击归卡堆，不作跳过）
        private bool _skip;               // 演出快进旗标（点击置位，各段动画消费后自复位）
        private bool _disposed;
        private Pose? _camPose;           // 推镜前战场位姿快照（还原=FrameBoardCamera 定档口径）
        private bool _camPushed;

        /// <summary>开场在途（BattleScreen.PollFieldPointer 顶部闸——开场期点击不进游戏分支）。</summary>
        public bool Active { get; private set; }

        public BattleOpeningDirector(Func<bool> running, BattleFieldView field, bool lunzhuan,
            Action<List<CardData>, string, List<CardData>, string, bool?> engage,
            bool? fixedPlayerFirst = null, bool dummy = false,
            RectTransform overlayParent = null, Action onCancelled = null)
        {
            _running = running;
            _field = field;
            _lunzhuan = lunzhuan;
            _engage = engage;
            _fixedPlayerFirst = fixedPlayerFirst;
            _dummy = dummy;
            _overlayParent = overlayParent;
            _onCancelled = onCancelled;
        }

        private bool Dead => _disposed || _field == null || !_running();

        // ======================================== 主流程 ========================================

        public async UniTaskVoid RunAsync()
        {
            Active = true;
            try
            {
                // ---- 段一：地块就位（棋盘浮起≤2s 与日出并行，落位即续） ----
                await WaitBoardAssembledAsync();
                await BeatAsync(0.35f);
                Debug.Log("[BattleOpening] 段一 地块就位完成");

                // ---- 段二：角色就位（教学三让位 intro 揭示段——boss 现身归教学既有演出） ----
                if (!_lunzhuan)
                {
                    await PlaceHeroesAsync();
                    Debug.Log("[BattleOpening] 段二 角色就位完成");
                }

                // ---- 段三：硬币先后手（固定先后手=跳过掷币——木桩测试选项；镜头推近属掷币演出一并省） ----
                bool playerFirst;
                if (_fixedPlayerFirst.HasValue)
                {
                    playerFirst = _fixedPlayerFirst.Value;
                    Debug.Log("[BattleOpening] 段三 先后手已固定 playerFirst=" + playerFirst + "（跳过掷币）");
                }
                else
                {
                    playerFirst = await CoinStageAsync();
                    Debug.Log("[BattleOpening] 段三 硬币落定 playerFirst=" + playerFirst);
                }

                // ---- 段四：战场选卡组 ----
                var (myName, aiName) = await DeckPickStageAsync();
                if (Dead) return;

                // ---- 段五交班：组装卡组（旧 PreMatchScreen.OnStart 门槛口径平移）→ Engage ----
                var myDeck = BuildDeckOf(myName, "我方");
                var aiDeck = _lunzhuan ? myDeck : BuildDeckOf(aiName, "对方"); // 教学三镜像
                var mySkill = myDeck != null ? SkillOf(myName) : null;
                var aiSkill = _lunzhuan ? mySkill : (aiDeck != null ? SkillOf(aiName) : null);
                Debug.Log($"[BattleOpening] 段五 交班开战：我方={myName ?? "随机"}({myDeck?.Count ?? 0}张) "
                    + $"对方={aiName ?? "随机"}({aiDeck?.Count ?? 0}张) playerFirst={playerFirst}");
                _engage(myDeck, mySkill, aiDeck, aiSkill, playerFirst);
            }
            catch (Exception ex)
            {
                Debug.LogError("[BattleOpening] 开场流程异常：" + ex);
            }
            finally
            {
                Active = false;
            }
        }

        // ======================================== 段实现 ========================================

        /// <summary>段一：棋盘装台+浮起落位（装台缺件=静态盘直过——不阻塞开场）；附 4s 保险 deadline。</summary>
        private async UniTask WaitBoardAssembledAsync()
        {
            float deadline = Time.realtimeSinceStartup + 4f;
            while (!Dead && Time.realtimeSinceStartup < deadline)
            {
                if (BattleBoardGrid.Ready && !BattleBoardAssemble.Running) break;
                await UniTask.Yield();
            }
        }

        /// <summary>段二：立柱自板下 +1.6 升入定位（easeOutCubic，P1→P2 顺次小间隔）。
        /// 预置柱由 BattleFieldView.PlaceOpeningHero 建（规格同 ReconcileHero，段五首次对账按名收养）。</summary>
        private async UniTask PlaceHeroesAsync()
        {
            for (int owner = 0; owner <= 1; owner++)
            {
                var pillar = _field.PlaceOpeningHero(owner);
                if (pillar == null) continue;
                var target = pillar.position;
                var from = target - Vector3.up * 1.6f;
                pillar.position = from;
                _skip = false;
                float t0 = Time.realtimeSinceStartup;
                const float dur = 0.55f;
                while (!Dead && Time.realtimeSinceStartup - t0 < dur)
                {
                    if (_skip) break;
                    float k = (Time.realtimeSinceStartup - t0) / dur;
                    float e = 1f - (1f - k) * (1f - k) * (1f - k);
                    pillar.position = Vector3.Lerp(from, target, e);
                    await UniTask.Yield();
                }
                if (pillar != null) pillar.position = target; // 终态钉死（跳过/自然到位同口）
                await BeatAsync(0.18f);
            }
        }

        /// <summary>段三：BattleCoinToss 掷币（结果即 startingPlayer）。2026-10-23 13:00 v3：
        /// 推近板心与还原机位两段 Tween 整删——机位全程保持定档 (7,8,-2)/(60,0,0)。</summary>
        private async UniTask<bool> CoinStageAsync()
        {
            var playerFirst = await BattleCoinToss.PlayAsync(BoardCenterWorld(), () => _skip);
            return playerFirst;
        }

        /// <summary>段四：候选卡组建堆 → 双选两轮/教学三单轮+倒计时；返回卡组名（null=随机）。
        /// 木桩局（2026-10-23 用户改案）不走 3D 卡堆——掷币后弹 DummyDeckPanel（双方卡组循环选
        /// +对手难度木桩/脚本/AI），难度经 BattleEntry.DummyDifficulty 带给段五 Engage。</summary>
        private async UniTask<(string myName, string aiName)> DeckPickStageAsync()
        {
            if (_dummy)
            {
                _deckPicking = true; // 点击不作快进（同 3D 选卡期口径）
                var pick = await DummyDeckPanel.ShowAsync(() => Dead, _overlayParent);
                _deckPicking = false;
                if (pick.Cancelled)
                {
                    _onCancelled?.Invoke(); // 返回主菜单（Back→OnExit→Dispose→Dead，段五不再推进）
                    return (null, null);
                }
                BattleEntry.DummyDifficulty = pick.Difficulty;
                Debug.Log($"[BattleOpening] 段四（木桩弹窗）我方={pick.MyDeckName} 对方={pick.AiDeckName} 难度={DummyDeckPanel.DifficultyLabels[pick.Difficulty]}");
                return (pick.MyDeckName, pick.AiDeckName);
            }

            // 普通局：3D 卡堆双选（2026-10-22 三套主题卡组迁教学：候选=玩家存档+只读预组合并口。
            // 预组带「·预组」后缀仅作展示，deckName 仍是原卡组名（BuildDeckOf 经 DeckCatalog 回落教学目录）。
            // 预组恒入候选（同日修正）：预组在 LoadAll 里固定追加在玩家档之后，整列 Take(5) 会把它们
            // 挤出——改为玩家档限量（5−预组数）、预组全保。
            var all = DeckCatalog.LoadAll()
                .Where(d => d != null && !string.IsNullOrEmpty(d.name))
                .ToList();
            var presets = all.Where(d => DeckCatalog.IsPreset(d.name)).ToList();
            var players = all.Where(d => !DeckCatalog.IsPreset(d.name))
                .Take(Math.Max(0, 5 - presets.Count))
                .ToList();
            var decks = players.Concat(presets)
                .Select(d => new BattleDeckPickStage.Entry(DeckCatalog.DisplayNameOf(d), d.name))
                .ToList();
            decks.Add(new BattleDeckPickStage.Entry("随机卡组", null)); // 末位兜底堆（恒在）

            _pickStage = new BattleDeckPickStage(_lunzhuan, decks);
            _pickStage.Mount();
            _deckPicking = true;
            var picked = await _pickStage.PickAsync(() => Dead);
            _deckPicking = false;
            if (!Dead) _pickStage.Dispose(); // 拆堆收尾（飞堆已在 PickAsync 内演完）
            return picked;
        }

        // ======================================== 指针（PollFieldPointer 开场闸转发） ========================================

        /// <summary>开场期指针：deckpick- 命中转发卡堆选择；其余点击=当前演出段快进（段四输入期除外）。</summary>
        public void PollPointer()
        {
            if (!Active) return;
            var cam = BattleStageDirector.WorldCam;
            if (cam == null) return;
            var aim = BattlePointer.ConfirmPressed ? BattlePointer.ConfirmPoint : BattlePointer.AimPoint;
            string hitName = Physics.Raycast(cam.ScreenPointToRay(aim), out var hit, 300f)
                ? hit.collider.gameObject.name : "";
            if (_deckPicking)
            {
                _pickStage?.HandlePointer(hitName, BattlePointer.ConfirmPressed);
                return;
            }
            if (BattlePointer.ConfirmPressed) _skip = true;
        }

        // ======================================== 生命周期 ========================================

        /// <summary>退屏兜底（BattleScreen.OnExit）：停流程、拆卡堆、还原机位（推过才还原）。</summary>
        public void Dispose()
        {
            _disposed = true;
            Active = false;
            _pickStage?.Dispose();
            _pickStage = null;
            if (_camPushed && _camPose.HasValue)
            {
                var cam = BattleStageDirector.WorldCam;
                if (cam != null)
                    cam.transform.SetPositionAndRotation(_camPose.Value.position, _camPose.Value.rotation);
            }
            _camPushed = false;
        }

        // ======================================== 内部 ========================================

        private static Vector3 BoardCenterWorld()
            => BattleBoardGrid.Root != null
                ? BattleBoardGrid.Root.TransformPoint(BattleBoardGrid.LocalBounds().center)
                : Vector3.zero;

        /// <summary>卡组名→开局卡组（旧 PreMatchScreen.OnStart 口径平移：BuildDeck 断链容忍，
        /// 张数≥起手才用，不足回落随机=null；skill 随卡组标记，卡组弃用则同弃）。</summary>
        private static List<CardData> BuildDeckOf(string deckName, string side)
        {
            if (deckName == null) return null;
            var data = DeckCatalog.Load(deckName); // 玩家存档 / 只读预组（2026-10-22 统一口）
            if (data?.cardIds == null) return null;
            var built = TutorialLibrary.BuildDeck(data.cardIds);
            if (built != null && built.Count >= GameCore.OpeningHandSize) return built;
            TideLog.Warn($"[BattleOpening] {side}卡组 {deckName} 缺卡/张数不足（{built?.Count ?? 0}）——回落随机");
            return null;
        }

        private static string SkillOf(string deckName)
        {
            var data = deckName != null ? DeckCatalog.Load(deckName) : null;
            return data?.skillCardId;
        }

        /// <summary>节拍停顿（skip 点击即缩短至收）。</summary>
        private async UniTask BeatAsync(float seconds)
        {
            float t0 = Time.realtimeSinceStartup;
            _skip = false;
            while (!Dead && Time.realtimeSinceStartup - t0 < seconds)
            {
                if (_skip) return;
                await UniTask.Yield();
            }
        }
    }

    /// <summary>
    /// 木桩测试选项面板（2026-10-22；2026-10-23 预制体化）：预制体 Assets/Art/Prefab/dummy-options
    ///（用户从初版代码面板提取）——结构=全屏遮罩+居中盒（lbl-title/lbl-hint+先后手/卡组洗牌/元素池
    /// 三行循环选钮+btn-start/btn-back）。文本与按钮接线运行时写入（预制体只烤结构与样式，
    /// 文案以代码为唯一正本）。循环选钮=点击换档（LobbyScreen 选卡组钮先例）。
    /// ShowAsync 返回 Options?=null=返回主菜单（dead 拆屏同判）。
    /// </summary>
    public sealed class DummyOptionsPanel
    {
        /// <summary>面板结果：PlayerFirst=null=掷硬币；Shuffle=false=锁牌库序；FullPool=true=充满预设。</summary>
        public readonly struct Options
        {
            public readonly bool? PlayerFirst;
            public readonly bool Shuffle;
            public readonly bool FullPool;
            public Options(bool? playerFirst, bool shuffle, bool fullPool)
            {
                PlayerFirst = playerFirst;
                Shuffle = shuffle;
                FullPool = fullPool;
            }
        }

        private static readonly string[] OrderLabels = { "掷硬币决定", "我方先手（固定）", "对方先手（固定）" };
        private static readonly string[] ShuffleLabels = { "洗牌", "不洗牌（卡组文件序）" };
        private static readonly string[] PoolLabels = { "常规开局", "充满（40 地+5白5黑·起跳回合 10）" };
        private const string HintText = "掷硬币后弹窗选定双方卡组与对手难度（木桩/脚本/AI）；对手卡组限三套主题卡组。";

        private RectTransform _root;
        private Options? _result;
        private bool _cancelled;
        private readonly int[] _idx = new int[3]; // 0=先后手 1=洗牌 2=元素池（数组槽位可被闭包安全捕获）

        /// <summary>弹面板并等结果（dead=已死该退出判定；返回 null=取消/拆屏——调用方退回主菜单）。
        /// 2026-10-22 根因注记：dead 语义与 BattleOpeningDirector._running（内部取反）相反，
        /// 调用方必须传「!运行中」——传反=面板当帧判死退出（木桩进不去的根因，已修）。</summary>
        public static async UniTask<Options?> ShowAsync(Func<bool> dead, RectTransform parent)
        {
            var panel = new DummyOptionsPanel(parent);
            try
            {
                while (!dead() && panel._result == null && !panel._cancelled)
                    await UniTask.Yield();
                return panel._result;
            }
            finally
            {
                panel.Dispose();
            }
        }

        private DummyOptionsPanel(RectTransform parent)
        {
            var prefab = Tide.HotUpdate.HotUpdateAssets.Load<GameObject>(
                "dummy-options", UiKit.PrefabPath("dummy-options")); // 屏预制体同口（Yoo 地址+编辑器兜底）
            if (prefab == null)
            {
                Debug.LogError("[木桩] dummy-options 预制体缺失（Assets/Art/Prefab/dummy-options.prefab）——面板弹不出，退回主菜单");
                _cancelled = true; // ShowAsync 立即以取消收场（调用方 Back），不悬死
                return;
            }
            var inst = UnityEngine.Object.Instantiate(prefab, parent, false);
            inst.name = "dummy-options";
            _root = (RectTransform)inst.transform;
            _root.SetAsLastSibling();
            UiKit.Stretch(_root); // 模态层铺满屏（防预制体锚点漂移）

            // 文案正本在代码：标题/提示/三行题栏逐名回写（循环钮文案由 Refresh 统一）
            SetText("lbl-title", "木桩测试设置", 20, TMPro.FontStyles.Bold);
            SetText("lbl-hint", HintText, 13);
            SetText("lbl-row-order", "先后手", 15);
            SetText("lbl-row-shuffle", "卡组洗牌", 15);
            SetText("lbl-row-pool", "元素池", 15);
            SetText("btn-start", "开始", -1);
            SetText("btn-back", "返回主菜单", -1);

            WireCycle("btn-row-order", 0);
            WireCycle("btn-row-shuffle", 1);
            WireCycle("btn-row-pool", 2);
            var start = ButtonOf("btn-start");
            if (start != null)
            {
                start.onClick.RemoveAllListeners();
                start.onClick.AddListener(() =>
                {
                    Debug.Log("[UI点击] btn-start");
                    _result = new Options(
                        _idx[0] == 0 ? (bool?)null : _idx[0] == 1, _idx[1] == 0, _idx[2] == 1);
                });
            }
            var back = ButtonOf("btn-back");
            if (back != null)
            {
                back.onClick.RemoveAllListeners();
                back.onClick.AddListener(() => { Debug.Log("[UI点击] btn-back"); _cancelled = true; });
            }
            Refresh();
        }

        /// <summary>循环选钮接线（点击换档+刷新文案；重入先清旧监听）。</summary>
        private void WireCycle(string btnName, int slot)
        {
            var btn = ButtonOf(btnName);
            if (btn == null) return;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                Debug.Log("[UI点击] " + btnName);
                _idx[slot] = (_idx[slot] + 1) % CycleCount(slot);
                Refresh();
            });
        }

        private int CycleCount(int slot)
            => slot == 0 ? OrderLabels.Length : slot == 1 ? ShuffleLabels.Length : PoolLabels.Length;

        /// <summary>循环钮文案刷新（三行钮逐名深找回写）。</summary>
        private void Refresh()
        {
            SetText("btn-row-order", OrderLabels[_idx[0]], -1);
            SetText("btn-row-shuffle", ShuffleLabels[_idx[1]], -1);
            SetText("btn-row-pool", PoolLabels[_idx[2]], -1);
        }

        private UnityEngine.UI.Button ButtonOf(string name)
        {
            var node = _root != null ? UiKit.FindDeep(_root, name) : null;
            return node != null ? node.GetComponentInChildren<UnityEngine.UI.Button>(true) : null;
        }

        /// <summary>按名深找 TMP 回写文案（size&lt;0=不动字号；style 仅题栏用）。</summary>
        private void SetText(string name, string text, int size, TMPro.FontStyles style = default)
        {
            var node = _root != null ? UiKit.FindDeep(_root, name) : null;
            var t = node != null ? node.GetComponentInChildren<TMPro.TMP_Text>(true) : null;
            if (t == null)
            {
                Debug.LogWarning("[木桩] 预制体缺文本节点：" + name);
                return;
            }
            t.text = text;
            if (size > 0) t.fontSize = size;
            t.fontStyle = style;
        }

        public void Dispose()
        {
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root.gameObject);
                _root = null;
            }
        }
    }

    /// <summary>
    /// 木桩测试·选卡组弹窗（2026-10-23 用户改案，替 3D 卡堆）：掷币（或固定先后手跳过）后弹出——
    /// 我方卡组（玩家存档+三套预组）与对方卡组（仅三套主题预组，10-22 定案）两个循环选，
    /// 对手难度=木桩（全让过）/脚本（剧本挂载位·暂同木桩）/AI（正式对手）。
    /// 纯代码构建（DummyOptionsPanel 预制体化同款结构——dim 遮罩+居中盒+行循环钮），美术换皮
    /// 提取预制体时文案以代码回写口径平移。开战=确认；返回主菜单=Cancelled（导演回调退屏）。
    /// </summary>
    public sealed class DummyDeckPanel
    {
        /// <summary>弹窗结果：卡组名（DeckCatalog 原名，段五 BuildDeckOf 消费）+难度档（BattleEntry 口径）。</summary>
        public readonly struct Pick
        {
            public readonly string MyDeckName;
            public readonly string AiDeckName;
            public readonly int Difficulty;   // 0=木桩 1=脚本 2=AI
            public readonly bool Cancelled;
            public Pick(string my, string ai, int difficulty, bool cancelled)
            {
                MyDeckName = my;
                AiDeckName = ai;
                Difficulty = difficulty;
                Cancelled = cancelled;
            }
        }

        public static readonly string[] DifficultyLabels = { "木桩（全让过）", "脚本（剧本位·暂同木桩）", "AI（正式对手）" };

        private RectTransform _root;
        private Pick? _result;
        private bool _cancelled;
        private readonly List<string> _myNames = new List<string>();
        private readonly List<string> _aiNames = new List<string>();
        private readonly List<string> _myLabels = new List<string>();
        private readonly List<string> _aiLabels = new List<string>();
        private readonly int[] _idx = new int[3]; // 0=我方 1=对方 2=难度（数组槽位闭包保值）

        /// <summary>弹窗并等结果（dead=已死该退出；Cancelled=true=返回主菜单）。</summary>
        public static async UniTask<Pick> ShowAsync(Func<bool> dead, RectTransform parent)
        {
            var panel = new DummyDeckPanel(parent);
            try
            {
                while (!dead() && panel._result == null && !panel._cancelled)
                    await UniTask.Yield();
                return panel._result ?? new Pick(null, null, 0, true); // dead/取消=取消口径
            }
            finally
            {
                panel.Dispose();
            }
        }

        private DummyDeckPanel(RectTransform parent)
        {
            // 候选：我方=玩家存档+三套预组（限量同 3D 口径）；对方=仅三套主题预组。
            var all = DeckCatalog.LoadAll()
                .Where(d => d != null && !string.IsNullOrEmpty(d.name))
                .ToList();
            var presets = all.Where(d => DeckCatalog.IsPreset(d.name)).ToList();
            var players = all.Where(d => !DeckCatalog.IsPreset(d.name))
                .Take(Math.Max(0, 5 - presets.Count))
                .ToList();
            foreach (var d in players.Concat(presets))
            {
                _myNames.Add(d.name);
                _myLabels.Add(DeckCatalog.DisplayNameOf(d));
            }
            foreach (var d in presets)
            {
                _aiNames.Add(d.name);
                _aiLabels.Add(DeckCatalog.DisplayNameOf(d));
            }
            if (_aiNames.Count == 0)
            {
                Debug.LogError("[木桩] 三套主题预组缺失（TutorialData）——选卡窗弹不出，退回主菜单");
                _cancelled = true;
                return;
            }
            _idx[1] = 0;

            _root = UiKit.Overlay("dummy-deck", parent);
            var dim = _root.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.55f); // 模态遮罩：拦棋盘点击
            dim.raycastTarget = true;

            var box = UiKit.Panel("dummy-deck-box", _root, pad: 20f, spacing: 12f);
            box.anchorMin = box.anchorMax = box.pivot = new Vector2(0.5f, 0.5f);
            box.anchoredPosition = Vector2.zero;
            box.sizeDelta = new Vector2(660f, 0f);
            var fitter = box.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            UiKit.Label("lbl-title", box, "木桩测试 · 选卡组", 20, align: TextAnchor.MiddleCenter, style: FontStyle.Bold);
            UiKit.Label("lbl-hint", box, "对手难度：木桩=全让过；脚本=剧本驱动（挂载位，暂同木桩）；AI=正式对手。",
                13, align: TextAnchor.MiddleLeft);

            CycleRow(box, "row-my", "我方卡组", 0);
            CycleRow(box, "row-ai", "对方卡组", 1);
            CycleRow(box, "row-diff", "对手难度", 2);

            var btns = UiKit.Row("row-btns", box, spacing: 14f, align: TextAnchor.MiddleCenter);
            UiKit.Size(UiKit.Button("btn-fight", btns, "开战", () => _result = new Pick(
                _myNames.Count > 0 ? _myNames[_idx[0]] : null,
                _aiNames[_idx[1]], _idx[2], false), UiStyle.BtnPrimary), w: 200f);
            UiKit.Button("btn-back", btns, "返回主菜单", () => _cancelled = true);
        }

        /// <summary>一行「题栏+循环选钮」：点击轮换第 slot 槣位并刷新（列表空=只显题栏不建钮）。</summary>
        private void CycleRow(RectTransform parent, string rowName, string caption, int slot)
        {
            var labels = slot == 0 ? _myLabels : slot == 1 ? _aiLabels : null;
            var diff = slot == 2;
            var count = diff ? DifficultyLabels.Length : labels != null ? labels.Count : 0;
            var row = UiKit.Row(rowName, parent);
            UiKit.Label("lbl-" + rowName, row, caption, 15);
            if (count == 0) return;
            UiKit.Size(UiKit.Button("btn-" + rowName, row, CurrentLabel(slot), () =>
            {
                _idx[slot] = (_idx[slot] + 1) % count;
                SetCycleText("btn-" + rowName, CurrentLabel(slot));
            }), w: 380f);
        }

        private string CurrentLabel(int slot)
            => slot == 0 ? _myLabels[_idx[0]]
                : slot == 1 ? _aiLabels[_idx[1]]
                : DifficultyLabels[_idx[2]];

        private void SetCycleText(string btnName, string text)
        {
            var node = UiKit.FindDeep(_root, btnName);
            var t = node != null ? node.GetComponentInChildren<TMPro.TMP_Text>(true) : null;
            if (t != null) t.text = text;
        }

        public void Dispose()
        {
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root.gameObject);
                _root = null;
            }
        }
    }
}
