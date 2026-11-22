using System;
using CardCore;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>教学/断线暂停的恢复口（v0.3 计划 §2.2）：confirm=只认【确定】钮；
    /// window=只认点遮罩窗内；windowOrConfirm=两者皆可（推荐）。</summary>
    public enum BattlePauseResume
    {
        Confirm,
        Window,
        WindowOrConfirm
    }

    /// <summary>
    /// 战斗通用暂停总线（2026-11-21 战场遮罩/高亮/暂停计划 §5·v0.4）：逻辑+表现一起停。
    /// 逻辑侧=GameStateManager.Pause（GameCore.Update/服务器泵/GameActions 意图闸全被
    /// CanPerformGameActions/Paused 校验拦住）；本总线只管**表现层**与**恢复口**：
    /// · 进场（Paused）：Time.timeScale=0（缩放时间动画/粒子/模型程序动画（MonsterRig 用
    ///   Time.time 驱动，时间冻结=姿态冻结）/相机 tween 冻住）+ AudioListener.pause=true
    ///   （全音频停——教学解说配音例外：BattleTutorialDirector 的解说源挂 ignoreListenerPause）；
    /// · 退场（任何来源的 Resume/终局/重置）：还原 timeScale 与音频，清横幅。
    /// 事件源=GameStateManager.StateChangedRelay（跨实例静态口——GameCore.ResetInstance 换代不失联）。
    /// 调用方：**只有教学**（v0.4 §5.4：联机断线=断开就结束，不经本总线——暂停等重连已废止）。
    /// 无订阅者不报错（§5.1 渐进接入）；回放/观战（BattlePlayhead.SetPaused）自带暂停不混入本总线（§5.3-5）。
    /// 已知边界：realtimeSinceStartup 驱动的展示（数值滚动等）在暂停期继续走完（纯表现余量，可接受）。
    /// </summary>
    public static class BattlePauseBus
    {
        /// <summary>暂停在位（表现侧镜像 GameStateManager.Paused）。</summary>
        public static bool Paused { get; private set; }

        /// <summary>本次暂停原因（透传 GameStateManager.PauseReason）。</summary>
        public static GamePauseReason Reason { get; private set; }

        /// <summary>暂停横幅文案（OpponentLost=等待重连提示；Tutorial=步 hint 常驻）。</summary>
        public static string Hint { get; private set; }

        /// <summary>教学暂停的恢复口（断线暂停无窗无钮=只能等重连/退出，本字段无意义）。</summary>
        public static BattlePauseResume ResumeBy { get; private set; }

        /// <summary>暂停横幅是否常驻（pause.showHint）。</summary>
        public static bool ShowHint { get; private set; }

        /// <summary>暂停进出场（参数=是否暂停、原因）。UI 提示层/遮罩窗恢复口订阅。</summary>
        public static event Action<bool, GamePauseReason> PausedChanged;

        private static float _savedTimeScale = 1f;
        private static bool _installed;
        private static bool _presentationEngaged;

        /// <summary>域重载关闭防跨 Play 残留（BattleTileFx.ResetStatics 同口径）+ 订阅中继。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Install()
        {
            if (_installed && _presentationEngaged) DisengagePresentation(); // 上个 Play 残留的 timeScale/音频还原
            _installed = true;
            Paused = false;
            Reason = GamePauseReason.None;
            Hint = null;
            ResumeBy = BattlePauseResume.WindowOrConfirm;
            ShowHint = true;
            GameStateManager.StateChangedRelay -= OnStateChanged;
            GameStateManager.StateChangedRelay += OnStateChanged;
        }

        private static void OnStateChanged(GameState oldState, GameState newState)
        {
            if (newState == GameState.Paused)
            {
                Paused = true;
                Reason = GameStateManager.PauseReason;
                Hint = GameStateManager.PauseHint;
                EngagePresentation();
                PausedChanged?.Invoke(true, Reason);
                Debug.Log($"[BattlePause] 暂停进入：{Reason}{(string.IsNullOrEmpty(Hint) ? "" : $"——{Hint}")}");
            }
            else if (Paused)
            {
                // 任何离场（Resume/Ended/NotStarted/ResetInstance 后新局）都释放表现层（§5.3-4 强制恢复兜底）
                var reason = Reason;
                Paused = false;
                Reason = GamePauseReason.None;
                Hint = null;
                DisengagePresentation();
                PausedChanged?.Invoke(false, reason);
                Debug.Log("[BattlePause] 暂停退出");
            }
        }

        /// <summary>教学侧暂停入口（导演调用；v0.4：暂停调用方只有教学——断线走
        /// NetBattleChannel.EndByDisconnect 直接结算，不经本总线）：resumeBy/showHint 随步配置。
        /// 引擎非 Running（未开局/终局）时静默不动（教学重铺期终局撤销前调用无害跳过）。</summary>
        public static bool PauseForTutorial(string hint, BattlePauseResume resumeBy, bool showHint)
        {
            var core = GameCore.Instance;
            if (core == null) return false;
            ResumeBy = resumeBy;
            ShowHint = showHint;
            // 逻辑侧置 Paused（StateChangedRelay 同拍回灌本总线：reason/hint 经静态字段透传）
            return core.StateManager.Pause(GamePauseReason.Tutorial, hint);
        }

        /// <summary>点【确定】恢复（resumeBy 含 confirm 时有效）：教学解说卡确定钮转发。</summary>
        public static void ResumeByConfirm()
        {
            if (!Paused) return;
            if (ResumeBy == BattlePauseResume.Window) return; // 只认点窗内
            Resume();
        }

        /// <summary>点遮罩窗内恢复（resumeBy 含 window 时有效）：遮罩控制器点中窗内时转发。</summary>
        public static void ResumeByWindow()
        {
            if (!Paused) return;
            if (ResumeBy == BattlePauseResume.Confirm) return; // 只认确定钮
            Resume();
        }

        /// <summary>恢复（通用口）：任何来源（确定/窗内/重连）都走这里。</summary>
        public static void Resume()
        {
            GameCore.Instance?.StateManager.Resume();
            // 表现层释放由 StateChangedRelay → Running 回调统一处理
        }

        /// <summary>强制恢复（切屏/终局/异常兜底，§5.3-4）：OnExit/Shutdown 调用——
        /// 引擎侧 Resume 被拒（已 Ended/重置）也要把表现层（timeScale/音频）放回来。</summary>
        public static void ForceResume()
        {
            GameCore.Instance?.StateManager.Resume();
            if (_presentationEngaged || Paused)
            {
                Paused = false;
                Reason = GamePauseReason.None;
                Hint = null;
                DisengagePresentation();
                PausedChanged?.Invoke(false, GamePauseReason.None);
            }
        }

        // ======================================== 表现层进出场 ========================================

        private static void EngagePresentation()
        {
            if (_presentationEngaged) return;
            _presentationEngaged = true;
            _savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;      // 缩放时间动画/粒子/模型程序动画/相机 tween 冻结
            AudioListener.pause = true; // 全音频停（解说配音源挂 ignoreListenerPause 豁免）
        }

        private static void DisengagePresentation()
        {
            if (!_presentationEngaged) return;
            _presentationEngaged = false;
            Time.timeScale = _savedTimeScale;
            AudioListener.pause = false;
        }
    }
}
