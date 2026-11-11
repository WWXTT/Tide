using System;
using System.Collections.Generic;
using CardCore;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>回放舞台接口（2026-11-11 演出统一 S2·方案 §2）：BattlePlayhead 只经此口触达表现层
    ///——战斗屏实现（视图扇出/FxOp 执行器/时间轴游标/战报行入列/战斗演出）与验证器桩实现
    ///（无头自检）同一协议。LiveData（二期 A）与 Replay 同口：本地实时帧体与回放帧体同构。</summary>
    public interface IBattleStageSink
    {
        /// <summary>应用帧局面（回放棋盘/角色/手牌落到该帧态）。</summary>
        void ApplyView(BattleViewData view);

        /// <summary>执行数据演出指令（伤害/回复/死亡/入场/浮字…——PreOps/Ops 逐条）。</summary>
        UniTask ExecuteFxAsync(BattleFxOp op);

        /// <summary>本帧战报行入列（LiveData 逐步放行；回放走时间轴预铺，本口空实现）。</summary>
        void AppendLines(IReadOnlyList<BattleLogLine> lines);

        /// <summary>时间轴游标推进（当前帧号——行高亮/滚动跟随；实时模式空实现）。</summary>
        void AdvanceTimeline(int frameIndex);

        /// <summary>战斗演出（攻击步——回放=CombatShot 播放器；本地实时=编舞器捕获开演）。</summary>
        UniTask PlayCombatAsync(CombatShot shot);

        /// <summary>等本帧全部演出收束（视觉在途门）。</summary>
        UniTask WaitVisualsIdleAsync();
    }

    /// <summary>
    /// 剧本播放器（2026-11-11 战斗演出统一重构 S1·Battle/Playback/BattlePlayhead.cs；二期 A 起双模式数据化）：
    /// 队列/首步导拍/步间节拍/看门狗/WaitIdleAsync/Shutdown/链消散延迟沿用，
    /// 帧体统一为数据指令（二期方案 §A）：**LiveData 与 Replay 走同一帧体/同一执行协议**
    ///（PreOps→View→Ops→Lines→Combat→等视觉→节拍），区别只在节拍时长（900/350ms）与收卷口径。
    /// LiveData（本地实时）：引擎侧结算一口气完成（数据真值瞬达、无头/验证零改动），表现层按帧回放；
    /// 期间引用计数 PresentationHold 冻结引擎泵、投影器抑制自动对账（防终态直出）；
    /// 轮外事件不产帧（行由帧生产者即时入列、演出 op 即时执行）；队列空且回合未完=WaitingForInput。
    /// Replay（S2 起·方案 §2.3）：Load 整包投帧永不等待输入；SeekTo 时间轴跳转
    /// （epoch 作废在途→清队列→最近 View→游标→续播）。
    /// 战斗演出严格按结算序（2026-10-19 用户定案）：回放到攻击步才驱动战斗演出，不再 StackEmpty 即演。
    /// 时序边界：多轮连锁（FinishResolution 最多 16 轮）事件同帧涌入——激活幂等、队列跨轮持续；
    /// 空轮（双 Pass 空栈）零开销；轮间开响应窗须先收尾（解除 hold 窗口才能开）。
    /// 看门狗 15s 无进展强制收口防死锁；Shutdown 供屏退场收闸。
    /// 等待原语可注入（delay/yield——编辑器验证器传同步完成版，回放整链同步推到底）。
    /// </summary>
    public sealed class BattlePlayhead
    {
        private const int LeadMs = 600;            // 首帧前置导拍：先看清链堆叠再开演（LiveData）
        private const int StepMs = 900;            // 帧间节拍（LiveData 本地现状值）
        private const int ReplayStepMs = 350;      // 帧间节拍（回放/联机——方案 §2.3）
        private const int DissolveLingerMs = 350;  // 轮末链消散延迟（期间新轮激活则取消消散）
        private const float WatchdogSeconds = 15f; // 无进展强制收口（战斗走位等长演出不误伤）

        private readonly Func<GameCore> _core;
        private readonly Func<BattleProjector> _projector;
        private readonly Func<BattleFieldView> _field;
        private readonly Func<BattleChainStage> _chain;
        private readonly Func<CombatChoreographer> _choreo;
        private readonly Action _refreshFans;
        private readonly Func<int, UniTask> _delay; // 可注入节拍（验证器=同步完成）
        private readonly Func<UniTask> _yield;      // 可注入帧让（验证器=同步完成）
        private int _liveStepMs = StepMs;                 // LiveData 节拍（联机 350ms——SetLivePacing 调）
        private int _liveBacklogSkip = int.MaxValue;      // LiveData 积压压缩阈值（队列超此值跳过节拍追帧）
        private int _liveLeadMs = LeadMs;                 // LiveData 首帧导拍（本地 600ms 看链堆叠；联机 0——低延迟）

        private readonly List<BattleFrame> _queue = new List<BattleFrame>();
        private bool _active;          // 回放会话在途（hold 已上）
        private bool _roundArmed;      // 结算轮已开始（StackResolutionStart）——Busy 口径含它（二期 A：接替旧日志 Hold 的「轮始即按住」节拍）
        private bool _sawRoundEmpty;   // 本会话见过 StackEmpty（队列干=可收尾；回放恒 true——永不等待输入）
        private bool _playbackRunning;
        private int _epoch;            // 收口代际：看门狗/退屏/SeekTo 作废在途回放
        private int _session;          // 激活会话号：轮末消散延迟期被新轮激活则取消消散
        private float _lastProgressAt; // 看门狗基准
        private BattleFilm _film;      // 回放剧本（null=LiveData）
        private IBattleStageSink _sink; // 舞台（LiveData/Replay 各自注入——SetLiveSink/Load）
        private readonly List<BattleLogLine> _tailLines = new List<BattleLogLine>(); // 轮末尾行（步帧之后到栈空前产出的行——随收尾放出，接替旧日志 ReleaseAll 时序）

        /// <summary>表现在途（含结算轮已开始未出帧——二期 A 起接替旧「日志缓冲探针」口径：
        /// 轮始（StackResolutionStart）即真，正是拦住步内 HP 读数中间态的那一拍）。</summary>
        public bool Busy => _active || _roundArmed;

        /// <summary>等待输入（LiveData：队列空且回合未收尾；回放恒 false）。</summary>
        public bool WaitingForInput => _film == null && _active && _queue.Count == 0 && !_sawRoundEmpty;

        /// <summary>已开播帧号（单调计数；回放=剧本帧序号——时间轴游标对齐）。</summary>
        public int CurrentFrame { get; private set; }

        /// <summary>回放模式（Load 过剧本）。</summary>
        public bool IsReplay => _film != null;

        public BattlePlayhead(Func<GameCore> core, Func<BattleProjector> projector,
            Func<BattleFieldView> field, Func<BattleChainStage> chain,
            Func<CombatChoreographer> choreo, Action refreshFans,
            Func<int, UniTask> delay = null, Func<UniTask> yield = null)
        {
            _core = core;
            _projector = projector;
            _field = field;
            _chain = chain;
            _choreo = choreo;
            _refreshFans = refreshFans;
            _delay = delay ?? new Func<int, UniTask>(ms => UniTask.Delay(ms));
            _yield = yield ?? new Func<UniTask>(() => UniTask.NextFrame()); // 帧让重询（Yield 为 YieldAwaitable 不可作 UniTask 返回——NextFrame 同为逐帧续跑）
        }

        // ==== 生产者入口（LocalFilmSource 投帧/回合边界；引擎事件同步时序） ====

        /// <summary>LiveData 舞台注入（本地局 OnEnter 调；轮内帧体的视图/演出/行全走它）。
        /// Replay 的舞台由 Load 携带（优先级更高）。</summary>
        public void SetLiveSink(IBattleStageSink sink)
        {
            if (_film != null) return; // 回放会话在途——不抢台
            _sink = sink;
        }

        /// <summary>LiveData 节拍调参（二期 C·联机）：stepMs=帧间停顿（联机 350ms）；backlogSkip=
        /// 积压压缩阈值（队列积压超此值跳过节拍追帧——防演出越落越远）；leadMs=首帧导拍
        ///（本地 600ms 看链堆叠；联机 0 低延迟）。</summary>
        public void SetLivePacing(int stepMs, int backlogSkip, int leadMs = LeadMs)
        {
            _liveStepMs = Math.Max(0, stepMs);
            _liveBacklogSkip = Math.Max(1, backlogSkip);
            _liveLeadMs = Math.Max(0, leadMs);
        }

        /// <summary>逆结算回合开始（BeginResolution）：只标记轮始（Busy 口径——HP 读数/终局闸消费）；
        /// 空轮（双 Pass 空栈）由轮末钩子即时还原，零停顿；会话延迟到首帧落地才建。</summary>
        public void BeginRound()
        {
            _roundArmed = true;
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

        /// <summary>逆结算轮末（栈排干）：tailLines=步帧之后到栈空前产出的战报行（随收尾放出——
        /// 接替旧日志 ReleaseAll 时序）。有会话=暂存到收尾（队列播干后 FinishCore 放出）；
        /// 空轮=无帧可放，行即时入列。</summary>
        public void NotifyRoundEmpty(IReadOnlyList<BattleLogLine> tailLines = null)
        {
            _roundArmed = false;
            if (tailLines != null && tailLines.Count > 0) _tailLines.AddRange(tailLines);
            if (!_active)
            {
                FlushTailLines(); // 空轮：不建会话零开销——行即刻入列
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
            var p = _projector();
            if (p != null) p.SuppressAutoFlush = true; // 自动对账抑制（终态直出防线）
            if (!_playbackRunning) RunPlaybackAsync(_epoch).Forget();
        }

        // ==== 回放整包投帧（S2·方案 §2.3/§2.4 ReplayFilmSource 消费） ====

        /// <summary>回放整包投帧（Replay）：作废在途→归位→应用目标帧之前最近一个 View（跳转/首载
        /// 同协议——先把棋盘/角色落到就近局面）→从 fromFrame 续播（跳过帧不播演出）。永不等待输入。</summary>
        public void Load(BattleFilm film, IBattleStageSink sink, int fromFrame = 0)
        {
            if (film == null || film.Frames.Count == 0 || sink == null)
                throw new ArgumentException("回放剧本/舞台不可为空（BattlePlayhead.Load）");
            _epoch++;      // 作废在途回放协程（Live 会话/上一次回放）
            FinishCore();  // 全闸归位（幂等——Live 各依赖在 Replay 态 null 安全）
            _film = film;
            _sink = sink;
            for (int i = fromFrame - 1; i >= 0; i--)
                if (film.Frames[i].View != null) { sink.ApplyView(film.Frames[i].View); break; }
            _queue.Clear();
            for (int i = fromFrame; i < film.Frames.Count; i++) _queue.Add(film.Frames[i]);
            CurrentFrame = fromFrame - 1;
            _active = true;
            _roundArmed = false;
            _sawRoundEmpty = true; // 剧本完整——队列干即收，不等输入
            _lastProgressAt = Time.time;
            _playbackRunning = true;
            RunPlaybackAsync(_epoch).Forget();
        }

        /// <summary>回放时间轴跳转（跳到某帧段首并继续播·方案 §2.3）：作废在途→中止在途演出
        ///（冻结读数放行/链消散）→应用目标帧之前最近 View→游标→续播。非回放/越界返回 false。</summary>
        public bool SeekTo(int frameIndex)
        {
            if (_film == null || _sink == null) return false;
            if (frameIndex < 0 || frameIndex >= _film.Frames.Count) return false;
            _field()?.ReleaseAllHeldStats(); // 攻击步冻结读数放行（在途命中拍作废）
            _chain()?.Dissolve();            // 在途链高亮消散
            Load(_film, _sink, frameIndex);  // epoch 作废在途 + 最近 View + 续播
            return true;
        }

        // ==== 播放主体（LiveData/Replay 同帧体） ====

        private async UniTaskVoid RunPlaybackAsync(int epoch)
        {
            _playbackRunning = true;
            try
            {
                if (_film == null && _liveLeadMs > 0) // 首帧前置导拍（LiveData 本地；联机 0 直开）
                {
                    await _delay(_liveLeadMs);
                    if (epoch != _epoch) return;
                }
                while (epoch == _epoch && _active)
                {
                    if (_queue.Count == 0)
                    {
                        if (_sawRoundEmpty) break; // 队列干+轮末已至——收尾（回放恒真=剧本播完即收）
                        await _yield();            // 同帧嵌套轮事件未到齐——续等（WaitingForInput）
                        continue;
                    }
                    var f = _queue[0];
                    _queue.RemoveAt(0);
                    CurrentFrame++;
                    _lastProgressAt = Time.time;

                    await RunFrameAsync(epoch, f);
                    if (epoch != _epoch) return;
                    if (f.Kind == BattleFrameKind.End) break; // 末帧收尾
                }
                if (epoch == _epoch && _active)
                    FinishAsync(epoch, _session).Forget();
            }
            finally
            {
                if (epoch == _epoch) _playbackRunning = false;
            }
        }

        /// <summary>统一帧体（二期方案 §A.1：抽自回放帧体，LiveData/Replay 共用）：
        /// PreOps（死亡预持）→View→Ops→Lines/时间轴→Combat→等视觉→节拍。</summary>
        private async UniTask RunFrameAsync(int epoch, BattleFrame f)
        {
            bool live = _film == null;
            _chain()?.MarkResolving(f.CardRid);
            if (f.IsAttack) _field()?.HoldAllHeroStats(); // 攻击帧读数冻结（快照终值让位命中拍）
            for (int i = 0; i < f.PreOps.Count; i++)
            {
                await _sink.ExecuteFxAsync(f.PreOps[i]); // 死亡预持先于 View（防对账抢先销毁尸体）
                if (epoch != _epoch) return;
            }
            if (f.View != null) _sink.ApplyView(f.View);
            _refreshFans?.Invoke(); // 手牌扇随帧同频（LiveData=引擎路径；Replay 屏=View 驱动路径）
            for (int i = 0; i < f.Ops.Count; i++)
            {
                await _sink.ExecuteFxAsync(f.Ops[i]);
                if (epoch != _epoch) return;
            }
            if (live && f.Lines.Count > 0) _sink.AppendLines(f.Lines); // 本帧战报行入列（接替旧日志水位放行）
            else _sink.AdvanceTimeline(CurrentFrame);                  // 时间轴游标（当前行高亮/滚动跟随）
            Debug.Log($"[Playhead] {(live ? "帧" : "回放帧")} {CurrentFrame} kind={f.Kind} label={f.Label} 余栈={f.Remaining}"
                      + (f.CardRid != 0 ? $" rid={f.CardRid}" : ""));
            if (f.Combat != null)
            {
                await _sink.PlayCombatAsync(f.Combat); // 战斗演出（LiveData=编舞器捕获开演；回放=CombatShot 播放器）
                if (epoch != _epoch) return;
            }
            else if (live && f.IsAttack) _choreo()?.PerformCapturedNow(); // 攻击步无 shot 兜底开演（严格结算序）
            await _sink.WaitVisualsIdleAsync();
            if (epoch != _epoch) return;
            if (f.IsAttack) _field()?.ReleaseAllHeldStats(); // 攻击帧兜底：演出中止/未开演的冻结放行（幂等）
            int beat = live ? _liveStepMs : ReplayStepMs;
            if (live && _queue.Count > _liveBacklogSkip) beat = 0; // 积压压缩：跳过节拍追帧
            if (beat > 0)
            {
                await _delay(beat);
                if (epoch != _epoch) return;
            }
            _chain()?.MarkResolved(f.CardRid);
        }

        /// <summary>轮末收尾：闸即刻归位（尾行入列+hold 解除+强终态对账——泵/窗口可续），
        /// 链消散延后一拍（期间新轮激活则取消——嵌套连锁不闪断）。</summary>
        private async UniTaskVoid FinishAsync(int epoch, int session)
        {
            FinishCore();
            await _delay(DissolveLingerMs);
            if (epoch != _epoch || session != _session || _active) return;
            _chain()?.Dissolve();
        }

        /// <summary>收尾核心（正常轮末/看门狗/退屏/回放收卷共用）：全部闸一次性归位+引擎真值兜底对账。</summary>
        private void FinishCore()
        {
            _active = false;
            _roundArmed = false;
            _queue.Clear();
            _core()?.ReleasePresentationHold();
            _field()?.ReleaseAllHeldStats(); // 数值节拍兜底：看门狗/退屏强收口防冻结读数卡死在旧值
            FlushTailLines(); // 轮末尾行落史（接替旧日志 ReleaseAll——行序=产出序）
            var p = _projector();
            if (p != null)
            {
                p.SuppressAutoFlush = false;
                p.RefreshField();             // 强制全量对账（终态=末帧快照，兜底口径一致）
            }
            _refreshFans?.Invoke();
            _choreo()?.PerformCapturedNow();      // 演出兜底：捕获未开演的战斗立即演（防静默丢演出）
        }

        /// <summary>轮末尾行入列并清账（空轮即时路径与收尾共用——播放器是行序的最后关口）。</summary>
        private void FlushTailLines()
        {
            if (_tailLines.Count == 0) return;
            var lines = _tailLines.ToArray();
            _tailLines.Clear();
            _sink?.AppendLines(lines);
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
            while (_active || _roundArmed) await _yield();
        }

        /// <summary>退屏/重局收闸（OnExit 调；幂等）：作废在途回放并全量还原（回放态一并清——跨局防串）。</summary>
        public void Shutdown()
        {
            _film = null;
            _sink = null;
            if (!_active) { _roundArmed = false; return; }
            _epoch++;
            FinishCore();
        }
    }
}
