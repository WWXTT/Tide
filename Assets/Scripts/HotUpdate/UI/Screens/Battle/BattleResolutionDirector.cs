using System;
using System.Collections.Generic;
using CardCore;
using CardCore.Serialization;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>
    /// 逆结算步进回放导演（2026-10-19 用户定案）：引擎侧结算一口气完成写全日志（数据真值瞬达、
    /// 无头/验证零改动），表现层按步回放——每步（栈上一个对象的结算）应用一份 BattleViewData
    /// 快照 + 放出对应日志行 + chain 链高亮推进，步间停顿；期间引用计数 PresentationHold 冻结
    /// 引擎泵、投影器抑制自动对账（防终态直出）、演出载荷（入场/疲劳/实付）延迟队列随步放出。
    /// 战斗演出严格按结算序（2026-10-19 用户定案）：回放到攻击步才驱动 CombatChoreographer
    /// 开演（ReplayDriven 模式），不再 StackEmpty 即演。
    /// 时序边界：多轮连锁（FinishResolution 最多 16 轮）事件同帧涌入——激活幂等、队列跨轮持续；
    /// 空轮（双 Pass 空栈）零开销（日志按住即放、不建会话不花导拍）；轮间开响应窗须先收尾
    /// （解除 hold 窗口才能开）；结算中交互原子（目标反问）在泵调用链内挂起不受 hold 影响。
    /// 看门狗 15s 无进展强制收口防死锁；Shutdown 供屏退场收闸。
    /// </summary>
    internal sealed class BattleResolutionDirector
    {
        private const int LeadMs = 600;            // 首步前置导拍：先看清链堆叠再开演
        private const int StepMs = 900;            // 步间节拍（表现层停顿）
        private const int DissolveLingerMs = 350;  // 轮末链消散延迟（期间新轮激活则取消消散）
        private const float WatchdogSeconds = 15f; // 无进展强制收口（战斗走位等长演出不误伤）

        private readonly Func<GameCore> _core;
        private readonly Func<BattleProjector> _projector;
        private readonly Func<BattleLogController> _log;
        private readonly Func<BattleFieldView> _field;
        private readonly Func<BattleChainStage> _chain;
        private readonly Func<CombatChoreographer> _choreo;
        private readonly Action _refreshFans;

        /// <summary>回放一步（栈对象结算的快照+日志水位+链映射载荷）。</summary>
        private sealed class Step
        {
            public BattleViewData Snapshot;
            public int LogBoundary;
            public uint CardRid;   // 卡牌施放步的卡 RuntimeId（0=非卡步：攻击/守卫/效果/SBA）
            public string Label;
            public bool IsAttack;
            public int Remaining;  // 步后余栈（日志证据链用）
        }

        private readonly List<Step> _queue = new List<Step>();
        private bool _active;          // 回放会话在途（hold 已上）
        private bool _sawRoundEmpty;   // 本会话见过 StackEmpty（队列干=可收尾）
        private bool _replayRunning;
        private int _epoch;            // 收口代际：看门狗/退屏作废在途回放
        private int _session;          // 激活会话号：轮末消散延迟期被新轮激活则取消消散
        private float _lastProgressAt; // 看门狗基准

        /// <summary>回放在途（AI 节奏等待/结束回合闸消费）。</summary>
        public bool Busy => _active;

        public BattleResolutionDirector(Func<GameCore> core, Func<BattleProjector> projector,
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

        // ==== 投影器钩子入口（引擎事件同步时序） ====

        /// <summary>逆结算回合开始（BeginResolution）：只按住日志（首步效果行不直出）——
        /// 空轮（双 Pass 空栈）由轮末钩子即时还原，零停顿；会话延迟到首步落地才建。</summary>
        public void OnResolutionStart(StackResolutionStartEvent e)
        {
            _log()?.Hold();
            _sawRoundEmpty = false;
        }

        /// <summary>逆结算单步完成：捕获快照+日志水位入队；首步落地建回放会话。
        /// 卡牌步快照补链头（2026-10-19 实测修复）：快照在对象出栈后捕获，ChainCards 已不含
        /// 本卡——无连锁的单发施放整段同步完成（无响应窗 await、置脏对账被抑制丢弃），链上
        /// 从未建过克隆=左侧恒无展示。补回后链舞台在回放期建槽（结算期只增不删口径不变）。</summary>
        public void OnStepResolved(StackResolutionEndEvent e)
        {
            if (!_active) Activate();
            var item = e?.ResolvedItem;
            var card = item != null && item.IsCardCast ? item.Source as Card : null;
            var snapshot = _projector()?.CaptureSnapshot();
            if (card != null && snapshot != null)
            {
                var core = _core();
                snapshot.ChainCards.Insert(0, BattleView.FromRuntime(
                    SerializableRuntimeCardState.FromCard(card), card,
                    core != null && item.Controller == core.Player2));
            }
            _queue.Add(new Step
            {
                Snapshot = snapshot,
                LogBoundary = MatchLogService.Entries.Count,
                CardRid = card != null ? card.RuntimeId : 0u,
                Label = card != null ? MatchLogRenderer.Name(card) // 卡名单源=EffectText（观侧口径与战报一致）
                    : item != null && item.IsAttackDeclaration ? "攻击宣言"
                    : item != null && item.IsGuardDeclaration ? "守卫宣言"
                    : item != null && item.IsSBA ? "状态动作" : "效果",
                IsAttack = item != null && item.IsAttackDeclaration,
                Remaining = e != null ? e.StackSize : 0,
            });
            _lastProgressAt = Time.time;
        }

        /// <summary>逆结算轮末（栈排干）：有会话=标记可收尾（队列干即收）；空轮=日志即时还原。</summary>
        public void OnRoundStackEmpty(StackEmptyEvent e)
        {
            if (!_active)
            {
                _log()?.ReleaseAll(); // 空轮：无步可放——不建会话零开销
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
            _log()?.Hold();                            // 日志行逐步放（幂等——Start 已按住则续用）
            var p = _projector();
            if (p != null) p.SuppressAutoFlush = true; // 自动对账抑制（终态直出防线）
            if (!_replayRunning) RunReplayAsync(_epoch).Forget();
        }

        // ==== 回放主体 ====

        private async UniTaskVoid RunReplayAsync(int epoch)
        {
            _replayRunning = true;
            try
            {
                if (LeadMs > 0)
                {
                    await UniTask.Delay(LeadMs);
                    if (epoch != _epoch) return;
                }
                int stepIndex = 0;
                while (epoch == _epoch && _active)
                {
                    if (_queue.Count == 0)
                    {
                        if (_sawRoundEmpty) break; // 队列干+轮末已至——收尾
                        await UniTask.Yield();     // 同帧嵌套轮事件未到齐——续等
                        continue;
                    }
                    var s = _queue[0];
                    _queue.RemoveAt(0);
                    stepIndex++;
                    _lastProgressAt = Time.time;

                    _chain()?.MarkResolving(s.CardRid);
                    var p = _projector();
                    if (p != null)
                    {
                        p.ApplySnapshot(s.Snapshot);      // 数据先到位（View 所有权单点仍在投影器）
                        p.DrainDeferredPresentationOps(); // 本步事件载荷随步放出（入场/疲劳/实付）
                    }
                    _refreshFans?.Invoke();               // 手牌扇区随快照同频（自动 Flush 被抑制）
                    _log()?.ReleaseUpTo(s.LogBoundary);   // 本步日志行落史
                    Debug.Log($"[Resolution] 步 {stepIndex}（余栈 {s.Remaining}）{s.Label}"
                              + (s.CardRid != 0 ? $" rid={s.CardRid}" : ""));

                    if (s.IsAttack) _choreo()?.PerformCapturedNow(); // 战斗演出=攻击步开演（严格结算序）
                    var f = _field();
                    if (f != null) await f.WaitVisualsIdleAsync();   // 等本步演出（战斗走位/入场粒子/疲劳）
                    if (epoch != _epoch) return;
                    await UniTask.Delay(StepMs);                    // 步间停顿
                    if (epoch != _epoch) return;
                    _chain()?.MarkResolved(s.CardRid);
                }
                if (epoch == _epoch && _active)
                    FinishAsync(epoch, _session).Forget();
            }
            finally
            {
                if (epoch == _epoch) _replayRunning = false;
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
            var p = _projector();
            if (p != null)
            {
                p.SuppressAutoFlush = false;
                p.DrainDeferredPresentationOps(); // 残余演出载荷放出
                p.RefreshField();                 // 强制全量对账（终态=末步快照，兜底口径一致）
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
            Debug.LogWarning("[Resolution] 看门狗：步进回放 15s 无进展——强制收口（疑演出挂死/事件时序异常）");
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
    }
}
