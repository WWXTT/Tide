using System;
using System.Collections.Generic;
using CardCore;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>
    /// 剧本播放器（2026-11-11 战斗演出统一重构 S1·Battle/Playback/BattlePlayhead.cs）：
    /// 原 BattleResolutionDirector（2026-10-19 逆结算步进回放导演）的队列/首步导拍/步间节拍/
    /// PresentationHold/日志水位/看门狗/WaitIdleAsync/Shutdown/链消散延迟逐行搬迁，步进改消费
    /// BattleFrame（方案 §2.3）——本地/联机/回放共用同一播放器，本阶段只接本地 Live
    /// （LocalFilmSource 产帧）；Replay/SeekTo 自 S2 起。
    /// 引擎侧结算一口气完成写全日志（数据真值瞬达、无头/验证零改动），表现层按帧回放——
    /// 每帧（栈上一个对象的结算）应用一份 BattleViewData 快照 + 放出对应日志行 + chain 链
    /// 高亮推进，帧间停顿；期间引用计数 PresentationHold 冻结引擎泵、投影器抑制自动对账
    /// （防终态直出）、演出载荷（入场/疲劳/实付）延迟队列随帧放出（S1 沿用，S5 数据化退役）。
    /// 战斗演出严格按结算序（2026-10-19 用户定案）：回放到攻击步才驱动 CombatChoreographer
    /// 开演（ReplayDriven 模式），不再 StackEmpty 即演。
    /// 时序边界：多轮连锁（FinishResolution 最多 16 轮）事件同帧涌入——激活幂等、队列跨轮持续；
    /// 空轮（双 Pass 空栈）零开销（日志按住即放、不建会话不花导拍）；轮间开响应窗须先收尾
    /// （解除 hold 窗口才能开）；结算中交互原子（目标反问）在泵调用链内挂起不受 hold 影响。
    /// 看门狗 15s 无进展强制收口防死锁；Shutdown 供屏退场收闸。
    /// </summary>
    internal sealed class BattlePlayhead
    {
        private const int LeadMs = 600;            // 首帧前置导拍：先看清链堆叠再开演
        private const int StepMs = 900;            // 帧间节拍（表现层停顿；本地 Live 现状值）
        private const int DissolveLingerMs = 350;  // 轮末链消散延迟（期间新轮激活则取消消散）
        private const float WatchdogSeconds = 15f; // 无进展强制收口（战斗走位等长演出不误伤）

        private readonly Func<GameCore> _core;
        private readonly Func<BattleProjector> _projector;
        private readonly Func<BattleLogController> _log;
        private readonly Func<BattleFieldView> _field;
        private readonly Func<BattleChainStage> _chain;
        private readonly Func<CombatChoreographer> _choreo;
        private readonly Action _refreshFans;

        private readonly List<BattleFrame> _queue = new List<BattleFrame>();
        private bool _active;          // 回放会话在途（hold 已上）
        private bool _sawRoundEmpty;   // 本会话见过 StackEmpty（队列干=可收尾）
        private bool _playbackRunning;
        private int _epoch;            // 收口代际：看门狗/退屏作废在途回放
        private int _session;          // 激活会话号：轮末消散延迟期被新轮激活则取消消散
        private float _lastProgressAt; // 看门狗基准

        /// <summary>回放在途（AI 节奏等待/结束回合闸消费——原导演 Busy 口径原样）。</summary>
        public bool Busy => _active;

        /// <summary>已开播帧号（单调计数；S2 回放时间轴游标按剧本帧序号对齐）。</summary>
        public int CurrentFrame { get; private set; }

        public BattlePlayhead(Func<GameCore> core, Func<BattleProjector> projector,
            Func<BattleLogController> log, Func<BattleFieldView> field,
            Func<BattleChainStage> chain, Func<CombatChoreographer> choreo, Action refreshFans)
        {
            _core = core;
            _projector = projector;
            _log = log;
            _field = field;
            _chain = chain;
            _choreo = choreo;
            _refreshFans = refreshFans;
        }

        // ==== 生产者入口（LocalFilmSource 投帧/回合边界；引擎事件同步时序） ====

        /// <summary>逆结算回合开始（BeginResolution）：只按住日志（首帧效果行不直出）——
        /// 空轮（双 Pass 空栈）由轮末钩子即时还原，零停顿；会话延迟到首帧落地才建。</summary>
        public void BeginRound()
        {
            _log()?.Hold();
            _sawRoundEmpty = false;
        }

        /// <summary>帧入队（单步完成/纯局面帧）：首帧落地建回放会话（原导演 OnStepResolved 激活口径）。</summary>
        public void Enqueue(BattleFrame frame)
        {
            if (frame == null) return;
            if (!_active) Activate();
            _queue.Add(frame);
            _lastProgressAt = Time.time;
        }

        /// <summary>逆结算轮末（栈排干）：有会话=标记可收尾（队列干即收）；空轮=日志即时还原。</summary>
        public void NotifyRoundEmpty()
        {
            if (!_active)
            {
                _log()?.ReleaseAll(); // 空轮：无帧可放——不建会话零开销
                return;
            }
            _sawRoundEmpty = true;
        }

        private void Activate()
        {
            _active = true;
            _session++;
            _sawRoundEmpty = false;
            _lastProgressAt = Time.time;
            _core()?.AcquirePresentationHold();       // 冻结泵：轮间自动双 Pass/新响应窗不得抢先
            _log()?.Hold();                            // 日志行逐步放（幂等——BeginRound 已按住则续用）
            var p = _projector();
            if (p != null) p.SuppressAutoFlush = true; // 自动对账抑制（终态直出防线）
            if (!_playbackRunning) RunPlaybackAsync(_epoch).Forget();
        }

        // ==== 回放主体 ====

        private async UniTaskVoid RunPlaybackAsync(int epoch)
        {
            _playbackRunning = true;
            try
            {
                if (LeadMs > 0)
                {
                    await UniTask.Delay(LeadMs);
                    if (epoch != _epoch) return;
                }
                while (epoch == _epoch && _active)
                {
                    if (_queue.Count == 0)
                    {
                        if (_sawRoundEmpty) break; // 队列干+轮末已至——收尾
                        await UniTask.Yield();     // 同帧嵌套轮事件未到齐——续等
                        continue;
                    }
                    var f = _queue[0];
                    _queue.RemoveAt(0);
                    CurrentFrame++;
                    _lastProgressAt = Time.time;

                    _chain()?.MarkResolving(f.CardRid);
                    var p = _projector();
                    if (p != null)
                    {
                        if (f.IsAttack) _field()?.HoldAllHeroStats(); // 攻击帧：角色HP读数冻结（单位 StatsHeld 同构）——快照终值先缓冲，命中拍才落地（2026-10-20「先24再23」）
                        if (f.View != null) p.ApplySnapshot(f.View);  // 数据先到位（View 所有权单点仍在投影器）
                        p.DrainDeferredPresentationOps();             // 本帧事件载荷随帧放出（入场/疲劳/实付——S1 沿用延迟队列，S5 数据化退役）
                    }
                    _refreshFans?.Invoke();               // 手牌扇区随快照同频（自动 Flush 被抑制）
                    if (f.Kind == BattleFrameKind.Step)
                        _log()?.ReleaseUpTo(f.LogBoundary); // 本帧日志行落史（S1 全帧皆 Step；S2 起 Lines 随帧放）
                    Debug.Log($"[Playhead] 帧 {CurrentFrame} kind={f.Kind} label={f.Label} 余栈={f.Remaining}"
                              + (f.CardRid != 0 ? $" rid={f.CardRid}" : ""));

                    if (f.IsAttack) _choreo()?.PerformCapturedNow(); // 战斗演出=攻击帧开演（严格结算序）
                    var field = _field();
                    if (field != null) await field.WaitVisualsIdleAsync(); // 等本帧演出（战斗走位/入场粒子/疲劳）
                    if (epoch != _epoch) return;
                    if (f.IsAttack) _field()?.ReleaseAllHeldStats(); // 攻击帧兜底：演出中止/未开演的冻结放行（命中拍已放则幂等——单位+角色柱）
                    await UniTask.Delay(StepMs);                    // 帧间停顿
                    if (epoch != _epoch) return;
                    _chain()?.MarkResolved(f.CardRid);
                }
                if (epoch == _epoch && _active)
                    FinishAsync(epoch, _session).Forget();
            }
            finally
            {
                if (epoch == _epoch) _playbackRunning = false;
            }
        }

        /// <summary>轮末收尾：闸即刻归位（hold 解除+日志全放+强终态对账——泵/窗口可续），
        /// 链消散延后一拍（期间新轮激活则取消——嵌套连锁不闪断）。</summary>
        private async UniTaskVoid FinishAsync(int epoch, int session)
        {
            FinishCore();
            await UniTask.Delay(DissolveLingerMs);
            if (epoch != _epoch || session != _session || _active) return;
            _chain()?.Dissolve();
        }

        /// <summary>收尾核心（正常轮末/看门狗/退屏共用）：全部闸一次性归位+引擎真值兜底对账。</summary>
        private void FinishCore()
        {
            _active = false;
            _queue.Clear();
            _core()?.ReleasePresentationHold();
            _field()?.ReleaseAllHeldStats(); // 数值节拍兜底：看门狗/退屏强收口防冻结读数卡死在旧值
            var p = _projector();
            if (p != null)
            {
                p.SuppressAutoFlush = false;
                p.DrainDeferredPresentationOps(); // 残余演出载荷放出
                p.RefreshField();                 // 强制全量对账（终态=末帧快照，兜底口径一致）
            }
            _log()?.ReleaseAll();
            _refreshFans?.Invoke();
            _choreo()?.PerformCapturedNow();      // 演出兜底：捕获未开演的战斗立即演（防静默丢演出）
        }

        /// <summary>看门狗（Updater 每帧）：回放会话超时无进展强制收口——防演出挂死冻死整局。</summary>
        public void Tick()
        {
            if (!_active) return;
            if (Time.time - _lastProgressAt <= WatchdogSeconds) return;
            Debug.LogWarning("[Playhead] 看门狗：帧回放 15s 无进展——强制收口（疑演出挂死/事件时序异常）");
            _epoch++; // 作废在途回放协程
            FinishCore();
        }

        /// <summary>回放空闲门（AI 节奏等待消费）：会话在途则等（消散延迟期不阻塞）。</summary>
        public async UniTask WaitIdleAsync()
        {
            while (_active) await UniTask.Yield();
        }

        /// <summary>退屏/重局收闸（OnExit 调；幂等）：作废在途回放并全量还原。</summary>
        public void Shutdown()
        {
            if (!_active) { _log()?.ReleaseAll(); return; }
            _epoch++;
            FinishCore();
        }

        // ==== S2 起接入（本阶段 Live-only 空实现·方案 §2.3） ====

        /// <summary>回放整包投帧（Replay 模式）：S2 接 ReplayFilmSource/BattleFilmStore 时实现。</summary>
        public void Load(BattleFilm film) =>
            throw new NotSupportedException("Replay 自 S2 起支持（当前 Live-only）");

        /// <summary>回放时间轴跳转（跳到某帧段首并继续播）：S2 实现 SeekTo 协议
        /// （epoch++ 作废在途→清队列→中止演出→应用最近 View→游标→续播）。</summary>
        public bool SeekTo(int frameIndex) => false;
    }
}
