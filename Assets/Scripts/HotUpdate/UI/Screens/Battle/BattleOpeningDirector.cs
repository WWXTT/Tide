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
        private readonly Action<List<CardData>, string, List<CardData>, string, bool?> _engage;
        private readonly bool? _fixedPlayerFirst; // 木桩测试固定先后手（null=照常掷币）

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
            bool? fixedPlayerFirst = null)
        {
            _running = running;
            _field = field;
            _lunzhuan = lunzhuan;
            _engage = engage;
            _fixedPlayerFirst = fixedPlayerFirst;
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

        /// <summary>段三：推近板心 → BattleCoinToss 掷币（结果即 startingPlayer）→ 还原机位。</summary>
        private async UniTask<bool> CoinStageAsync()
        {
            var cam = BattleStageDirector.WorldCam;
            var center = BoardCenterWorld();
            if (cam != null)
            {
                _camPose = new Pose(cam.transform.position, cam.transform.rotation);
                _camPushed = true;
                var viewPos = center + new Vector3(0f, 2.4f, -2.6f);
                var lookAt = center + Vector3.up * 1.1f;
                await TweenCamAsync(cam, viewPos, Quaternion.LookRotation(lookAt - viewPos), 0.6f);
            }
            var playerFirst = await BattleCoinToss.PlayAsync(center, () => _skip);
            if (cam != null && _camPose.HasValue)
                await TweenCamAsync(cam, _camPose.Value.position, _camPose.Value.rotation, 0.5f);
            _camPushed = false;
            return playerFirst;
        }

        /// <summary>段四：候选卡组建堆 → 双选两轮/教学三单轮+倒计时；返回卡组名（null=随机）。</summary>
        private async UniTask<(string myName, string aiName)> DeckPickStageAsync()
        {
            // 2026-10-22 三套主题卡组迁教学：候选=玩家存档 + 只读预组（DeckCatalog 合并口）。
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

        /// <summary>相机位姿补间（TweenLightX 同风格：realtime 计时+SmoothStep；skip=直接钉终态）。</summary>
        private async UniTask TweenCamAsync(Camera cam, Vector3 toPos, Quaternion toRot, float seconds)
        {
            var fromPos = cam.transform.position;
            var fromRot = cam.transform.rotation;
            _skip = false;
            float t0 = Time.realtimeSinceStartup;
            while (!Dead && Time.realtimeSinceStartup - t0 < seconds)
            {
                if (cam == null) return; // 拆台弃演
                if (_skip) break;
                float k = Mathf.Clamp01((Time.realtimeSinceStartup - t0) / seconds);
                float e = Mathf.SmoothStep(0f, 1f, k);
                cam.transform.SetPositionAndRotation(
                    Vector3.Lerp(fromPos, toPos, e), Quaternion.Slerp(fromRot, toRot, e));
                await UniTask.Yield();
            }
            if (cam != null) cam.transform.SetPositionAndRotation(toPos, toRot);
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
    /// 木桩测试选项面板（2026-10-22）：进入战场先不开始——模态弹窗给三行可选列表
    ///（先后手：掷硬币/我方先手/对方先手——固定先后=开场跳过掷币段；卡组洗牌：是/否；
    /// 元素池：常规/充满）+ 开始/返回主菜单。行内循环选钮（点击换档，LobbyScreen 选卡组钮先例）。
    /// 纯代码构建（LobbyScreen 纯代码屏先例，prefab 化留美术换皮）；全屏遮罩拦棋盘点击。
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

        private RectTransform _root;
        private Options? _result;
        private bool _cancelled;
        private readonly int[] _idx = new int[3]; // 0=先后手 1=洗牌 2=元素池（数组槽位可被闭包安全捕获）

        /// <summary>弹面板并等结果（dead=拆屏判定；返回 null=取消/拆屏——调用方退回主菜单）。</summary>
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
            _root = UiKit.Overlay("dummy-options", parent);
            var dim = _root.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.55f); // 模态遮罩：拦棋盘点击
            dim.raycastTarget = true;

            // 居中定宽盒（父级 Overlay 无布局组——锚点直写生效；高度由 CSF 随内容自适应）
            var box = UiKit.Panel("dummy-options-box", _root, pad: 20f, spacing: 12f);
            box.anchorMin = box.anchorMax = box.pivot = new Vector2(0.5f, 0.5f);
            box.anchoredPosition = Vector2.zero;
            box.sizeDelta = new Vector2(660f, 0f);
            var fitter = box.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            UiKit.Label("lbl-title", box, "木桩测试设置", 20, align: TextAnchor.MiddleCenter, style: FontStyle.Bold);
            UiKit.Label("lbl-hint", box, "对手为木桩：不出牌、不攻击、不响应、不守卫；双方卡组随后都由你选定。",
                13, align: TextAnchor.MiddleLeft);

            CycleRow(box, "row-order", "先后手", OrderLabels, 0);
            CycleRow(box, "row-shuffle", "卡组洗牌", ShuffleLabels, 1);
            CycleRow(box, "row-pool", "元素池", PoolLabels, 2);

            var btns = UiKit.Row("row-btns", box, spacing: 14f, align: TextAnchor.MiddleCenter);
            UiKit.Button("btn-start", btns, "开始", () => _result = new Options(
                _idx[0] == 0 ? (bool?)null : _idx[0] == 1, _idx[1] == 0, _idx[2] == 1),
                UiStyle.BtnPrimary);
            UiKit.Button("btn-back", btns, "返回主菜单", () => _cancelled = true);
        }

        /// <summary>一行「标题 + 循环选钮」：点击按 values 轮换（slot=选值数组槽——闭包经数组保值）。</summary>
        private void CycleRow(RectTransform parent, string rowName, string caption,
            string[] values, int slot)
        {
            var row = UiKit.Row(rowName, parent);
            UiKit.Label("lbl-" + rowName, row, caption, 15);
            UiKit.Size(UiKit.Button("btn-" + rowName, row, values[_idx[slot]], () =>
            {
                _idx[slot] = (_idx[slot] + 1) % values.Length;
                Refresh();
            }), w: 320f);
        }

        /// <summary>循环钮文案刷新（Build 期字段序：三行钮已建全，逐名深找回写）。</summary>
        private void Refresh()
        {
            SetCycleText("btn-row-order", OrderLabels[_idx[0]]);
            SetCycleText("btn-row-shuffle", ShuffleLabels[_idx[1]]);
            SetCycleText("btn-row-pool", PoolLabels[_idx[2]]);
        }

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
