using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using Cysharp.Threading.Tasks;
using UnityEngine;

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

        private BattleDeckPickStage _pickStage;
        private bool _deckPicking;        // 段四输入期（点击归卡堆，不作跳过）
        private bool _skip;               // 演出快进旗标（点击置位，各段动画消费后自复位）
        private bool _disposed;
        private Pose? _camPose;           // 推镜前战场位姿快照（还原=FrameBoardCamera 定档口径）
        private bool _camPushed;

        /// <summary>开场在途（BattleScreen.PollFieldPointer 顶部闸——开场期点击不进游戏分支）。</summary>
        public bool Active { get; private set; }

        public BattleOpeningDirector(Func<bool> running, BattleFieldView field, bool lunzhuan,
            Action<List<CardData>, string, List<CardData>, string, bool?> engage)
        {
            _running = running;
            _field = field;
            _lunzhuan = lunzhuan;
            _engage = engage;
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

                // ---- 段三：硬币先后手（镜头推近板心→掷币→还原） ----
                bool playerFirst = await CoinStageAsync();
                Debug.Log("[BattleOpening] 段三 硬币落定 playerFirst=" + playerFirst);

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
            var decks = DeckSerializer.LoadAll()
                .Where(d => d != null && !string.IsNullOrEmpty(d.name))
                .Take(5)
                .Select(d => new BattleDeckPickStage.Entry(d.name, d.name))
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
            var data = DeckSerializer.Load(deckName);
            if (data?.cardIds == null) return null;
            var built = TutorialLibrary.BuildDeck(data.cardIds);
            if (built != null && built.Count >= GameCore.OpeningHandSize) return built;
            TideLog.Warn($"[BattleOpening] {side}卡组 {deckName} 缺卡/张数不足（{built?.Count ?? 0}）——回落随机");
            return null;
        }

        private static string SkillOf(string deckName)
        {
            var data = deckName != null ? DeckSerializer.Load(deckName) : null;
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
}
