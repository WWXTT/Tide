using System;
using System.Collections.Generic;
using CardCore;
using Cysharp.Threading.Tasks;
using UnityEngine;
using TMPro;

namespace SynergyUI
{
    // ======================================== 复盘回放（2026-11-11 演出统一 S2·方案 §2.6） ========================================
    // Replay 模式：不建 BattleController/NetGameClient——剧本（BattleFilmStore）整包投 BattlePlayhead
    //（Replay 分支：PreOps→View→Ops→时间轴→Combat→350ms 节拍）；交互全关（无 Wire/无扇交互/无引擎闸）；
    // 战报面板=时间轴（全量行预铺、当前行高亮、点击行 SeekTo 续播）；认输键改「退出复盘」。

    public sealed partial class BattleScreen : UIScreen
    {
        private ReplayFilmSource _replaySource; // 回放剧本源（一次性消费——OnExit 随屏清）
        private BattleFxOpExecutor _fxExecutor; // 回放演出执行器（op→PlaybackFx 分发）

        /// <summary>复盘局启动（OnEnter 的 Replay 分支）：装台等棋盘就绪后整包投帧；面板展开铺时间轴。</summary>
        private void StartReplayFan()
        {
            var filmId = BattleEntry.ReplayFilmId; // 一次性消费（防跨局残留）
            BattleEntry.ReplayFilmId = null;
            _replaySource = new ReplayFilmSource(filmId);
            if (_replaySource.Film == null)
            {
                _toast.Show($"复盘剧本不存在：{filmId ?? "（空）"}");
                Manager.Back();
                return;
            }
            if (_lblMode != null) _lblMode.text = _replaySource.Summary();

            // 认输键改「退出复盘」（OnConcede 的 Replay 分支直回上一屏）
            if (_btnConcede != null)
            {
                var lbl = _btnConcede.GetComponentInChildren<TMP_Text>(true);
                if (lbl != null) lbl.text = "退出复盘";
            }

            _fxExecutor = new BattleFxOpExecutor(() => _field);
            var sink = new ReplayStageSink(this);
            _log.ShowTimeline(); // 面板展开（SetLogTarget 单源——摘要行随 PanelToggled 隐藏）
            _log.BeginTimeline(_replaySource.TimelineLines(), frame => _director.SeekTo(frame));
            StartReplayWhenBoardReadyAsync(sink).Forget();
        }

        /// <summary>棋盘就绪门（等待口径同 RefreshFieldWhenBoardReady，但不等 Core——回放无引擎）：
        /// 就绪即投帧（帧 #0 View 先落开局态再播第一步）。</summary>
        private async UniTaskVoid StartReplayWhenBoardReadyAsync(ReplayStageSink sink)
        {
            float deadline = Time.realtimeSinceStartup + 45f;
            while (_running && !BattleBoardGrid.Ready && Time.realtimeSinceStartup < deadline)
                await UniTask.Yield();
            if (!_running) return; // 拆屏（OnExit 已 Shutdown 播放器）
            _replaySource.Play(_director, sink);
        }

        /// <summary>回放视图应用：ApplyProjection 同链（3D 对账+连锁链+2D 信息栏）+ 回放扇区（View 驱动
        /// ——RefreshFans 是引擎驱动路径，回放无 Core 不适用）。</summary>
        private void ApplyReplayView(BattleViewData d)
        {
            ApplyProjection(d);
            RefreshReplayFans(d);
        }

        /// <summary>回放手牌扇（View 驱动）：己方=SelfHand 全正面（剧本快照正面口径）；对手=按 HandCount
        /// 铺卡背（回放无对手手牌信息——遮蔽口径与对局时一致）。</summary>
        private void RefreshReplayFans(BattleViewData d)
        {
            if (d == null) return;
            var self = new List<HandFanCardData>();
            foreach (var v in d.SelfHand)
                self.Add(new HandFanCardData { RuntimeId = v.RuntimeId, Item = CardOverlayItem.FromHand(v), FaceUp = true });
            var opp = new List<HandFanCardData>();
            const uint oppBackBase = 0x90000000u; // 伪 RuntimeId（卡背无实体——扇区对账键不与真卡撞段）
            for (int i = 0; i < d.Opp.HandCount; i++)
                opp.Add(new HandFanCardData { RuntimeId = oppBackBase + (uint)i, Item = BlankBack(oppBackBase + (uint)i), FaceUp = false });
            _selfFan?.SetCards(self);
            _oppFan?.SetCards(opp);
        }

        /// <summary>回放舞台（IBattleStageSink 的战斗屏实现）：视图=ApplyReplayView 渲染扇出；
        /// 演出=执行器（PlaybackFx ref 口）；时间轴=日志游标；战斗=CombatShot 回放器；视觉门=战场在途门。</summary>
        private sealed class ReplayStageSink : IBattleStageSink
        {
            private readonly BattleScreen _s;
            public ReplayStageSink(BattleScreen s) { _s = s; }

            public void ApplyView(BattleViewData view) => _s.ApplyReplayView(view);
            public UniTask ExecuteFxAsync(BattleFxOp op) => _s._fxExecutor != null ? _s._fxExecutor.ExecuteAsync(op) : UniTask.CompletedTask;
            public void AdvanceTimeline(int frameIndex) => _s._log.SetTimelineCursor(frameIndex);
            public UniTask PlayCombatAsync(CombatShot shot) => _s._field != null ? _s._field.PlayCombatShotAsync(shot) : UniTask.CompletedTask;
            public UniTask WaitVisualsIdleAsync() => _s._field != null ? _s._field.WaitVisualsIdleAsync() : UniTask.CompletedTask;
        }
    }
}
