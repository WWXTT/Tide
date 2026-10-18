using System;
using System.Collections.Generic;
using CardCore;
using CardCore.Attribute;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>
    /// 战场投影器（2026-10-18 第二轮 B2 投影单点化）：**本屏唯一的引擎事件订阅点 + BattleViewData
    /// 所有权**。事件→置脏（经屏侧合帧队列）/演出载荷钩子/终局流转；视图构建（BuildLocal）与
    /// ViewVersion（悬停缓存失效源）单点持有。北极星链路：逻辑层→事件流→（日志中间层 B1 +
    /// 本投影器）→UI。
    /// 纯 C# 类：EventManager 直订自管退订（Dispose——镜像 BattleLogController 语义）；
    /// 演出/面板细节全部经 ctor 钩子注入（onApplied=渲染扇出、armFlyIn=抽卡飞入、终局/残单=屏侧流程），
    /// 投影器不认识任何视图组件——只产 View 与置脏信号。
    /// 时序红线（沿用原 BattleScreen 口径）：WireEarly（抽卡飞入对）须先于 StartNewGame——
    /// 起手抽与首个回合自然抽同在开局内发完；Wire（对账订阅表）在 StartNewGame 之后（原序），
    /// 开局内事件由尾段补偿刷兜底。
    /// </summary>
    internal sealed class BattleProjector : IDisposable
    {
        private readonly Func<GameCore> _core;
        private readonly Func<BattleController> _ctrl;
        private readonly Action<BattleViewData> _onApplied;   // 渲染扇出：field.Apply+castStage+bars+region+三键置脏（屏组装根接线）
        private readonly Action _onBarsOnly;                  // RefreshLocal 路径扇出（仅 2D 信息栏——出牌终态等的轻刷）
        private readonly Action _onGameStart;                 // 抽卡飞入武装点（GameStartEvent=起手抽后、首个 TurnStart 前）
        private readonly Action<Player> _armDrawFlyIn;        // 抽牌对账前注入卡堆顶坐标（扇区演出）
        private readonly Action<CardActivationFailedEvent> _onActivationFailed; // 施放展示即清+演出中断
        private readonly Action<FatigueEvent> _onFatigue;     // 疲劳受击演出（红闪脉冲+浮字）
        private readonly Action<ElementPoolPayEvent> _onPaid; // 最近实付账单捕获（入场粒子颜色口径）
        private readonly Action<CardPutToBattlefieldEvent> _onArrival; // 入场演出载荷组装
        private readonly Action<GameOverEvent> _onGameOver;   // 终局流转（弹窗/重试链——决策流，非投影）
        private readonly Action _clearPendingPins;            // TurnStart 清出牌选格残单

        private readonly List<Action> _unsubscribers = new List<Action>();
        private int _lastCoreNullLogFrame = -999; // 核心未就绪跳过的节流日志帧

        /// <summary>当前视图（渲染唯一输入——一写多读，写点只在 RefreshField/RefreshLocal 构建）。</summary>
        public BattleViewData View { get; private set; }

        /// <summary>战场视图版本（每次成功对账递增）——悬停源缓存失效源。</summary>
        public int ViewVersion { get; private set; }

        public BattleProjector(Func<GameCore> core, Func<BattleController> ctrl,
            Action<BattleViewData> onApplied, Action onBarsOnly,
            Action onGameStart, Action<Player> armDrawFlyIn,
            Action<CardActivationFailedEvent> onActivationFailed, Action<FatigueEvent> onFatigue,
            Action<ElementPoolPayEvent> onPaid, Action<CardPutToBattlefieldEvent> onArrival,
            Action<GameOverEvent> onGameOver, Action clearPendingPins)
        {
            _core = core;
            _ctrl = ctrl;
            _onApplied = onApplied;
            _onBarsOnly = onBarsOnly;
            _onGameStart = onGameStart;
            _armDrawFlyIn = armDrawFlyIn;
            _onActivationFailed = onActivationFailed;
            _onFatigue = onFatigue;
            _onPaid = onPaid;
            _onArrival = onArrival;
            _onGameOver = onGameOver;
            _clearPendingPins = clearPendingPins;
        }

        // ==== 订阅（唯一订阅点） ====

        /// <summary>抽卡飞入对（StartNewGame 之前——时序红线见类注释）。</summary>
        public void WireEarly()
        {
            On<GameStartEvent>(_ => _onGameStart());
            On<CardEnterHandEvent>(e =>
            {
                if (e != null && e.IsDraw) _armDrawFlyIn(e.Player); // 抽牌武装须同步（刷扇可合帧：armed 集合随后统一消费）
                QueueFans();
            });
        }

        /// <summary>对账订阅表（StartNewGame 之后，原序；引擎事件多但刷新幂等——统一对账重建）。</summary>
        public void Wire()
        {
            On<CardPlayEvent>(_ => { QueueFans(); QueueField(); });            // 出牌→单位/地牌落格
            On<CardZoneChangeEvent>(_ => { QueueFans(); QueueField(); });
            On<CardMoveEvent>(_ => { QueueFans(); QueueField(); });
            On<CardDestroyEvent>(_ => { QueueFans(); QueueField(); });
            // 施放类型图标收口（2026-10-15 发动区改版回归修复）：入场/离开发动区/发动失败即时刷新——
            // 白板卡结算零事件（无指示物/无效果），缺此订阅图标滞留、3D 模型要等回合结束才上
            On<CardPutToBattlefieldEvent>(e => { _onArrival(e); QueueFans(); QueueField(); }); // 入场演出载荷组装（2026-10-15 出场浮现）
            On<CardLeaveActivationEvent>(_ => { QueueFans(); QueueField(); }); // 结算离区：图标/卡面转 linger（2026-10-17 时序定案）
            On<CardActivationFailedEvent>(e => // 发动失败（阻拦无效/费用/代价异常+满场入墓口）：展示即清不等 linger
            {
                _onActivationFailed(e);
                QueueFans(); QueueField();
            });
            On<StackEmptyEvent>(_ => QueueField()); // 栈清空=一批结算完成的全量对账兜底
            On<CounterChangedEvent>(_ => { QueueFans(); QueueField(); }); // 指示物→对手卡翻面/攻生→头顶数值
            On<RevealCardsEvent>(_ => QueueFans());
            // 棋盘数字条：黑白钱包与地牌横置跨回合清零（TurnStart）、角色生命（LifeChange）、
            // 钱包入账/支付（ElementPool 两事件）——RefreshField 全量对账覆盖
            On<TurnStartEvent>(_ => { _clearPendingPins(); QueueField(); }); // 出牌选格残单随回合清
            On<PhaseStartEvent>(_ => QueueField()); // TurnStart 先于切阶段发布：缺此订阅 View.Phase 滞留上回合 End（回合≥2 点单位被误拒）
            On<LifeChangeEvent>(_ => QueueField());
            On<FatigueEvent>(_onFatigue); // 疲劳受击演出（红闪脉冲+头顶浮字——信息性表演占视觉门）
            On<ElementPoolGainEvent>(_ => QueueField());
            On<ElementPoolPayEvent>(e => { _onPaid(e); QueueField(); }); // 最近实付账单（入场演出颜色口径）
            // 放地入池（2026-10-14）：入池路径只发本事件（移区不发 CardMove/ZoneChange）——手牌收卡+地格计数即时对账
            On<ElementPoolAddEvent>(_ => { QueueFans(); QueueField(); });
            // 攻击宣言（2026-10-14 优先权半场高光）：宣言即优先权翻转（守卫窗口对手持有）
            On<AttackDeclarationEvent>(_ => QueueField());
            On<GameOverEvent>(_onGameOver);
        }

        private void On<T>(Action<T> handler) where T : class, IGameEvent
        {
            EventManager.Instance.Subscribe(handler);
            _unsubscribers.Add(() => EventManager.Instance.Unsubscribe(handler));
        }

        public void Dispose()
        {
            foreach (var unsub in _unsubscribers) unsub();
            _unsubscribers.Clear();
        }

        // ==== 置脏（合帧节流：事件订阅置脏，帧首 Flush 一次——2026-10-18 性能口径） ====

        private bool _fansQueued, _fieldQueued;

        /// <summary>排队全量刷扇（事件订阅口）：本帧多事件合并。直接调用语义不变：
        /// 交互/教学等一次性刷新仍走屏侧 RefreshFans() 同步路径。</summary>
        public void QueueFans() => _fansQueued = true;

        /// <summary>排队全量对账（语义同 QueueFans）。</summary>
        public void QueueField() => _fieldQueued = true;

        /// <summary>帧首冲刷（Updater 第一项）：刷扇回调经屏侧注入（扇区是屏组件），对账走本类。</summary>
        public void Flush(Action refreshFans)
        {
            if (_fansQueued) { _fansQueued = false; refreshFans(); }
            if (_fieldQueued) { _fieldQueued = false; RefreshField(); }
        }

        // ==== 视图构建（所有权单点） ====

        /// <summary>摆位刷新（渲染唯一输入=BattleViewData）：BuildLocal 直连构建→onApplied 渲染扇出→版本递增。</summary>
        public void RefreshField()
        {
            var core = _core();
            var ctrl = _ctrl();
            if (core == null || ctrl?.P1 == null || ctrl?.P2 == null)
            {
                if (Time.frameCount - _lastCoreNullLogFrame > 120)
                {
                    _lastCoreNullLogFrame = Time.frameCount;
                    Debug.Log($"[BattleField] 刷新跳过：对局核心未就绪（core={core != null}, p1={ctrl?.P1 != null}, p2={ctrl?.P2 != null}）");
                }
                return;
            }
            View = BattleView.BuildLocal(core, ctrl.Board, ctrl.P1, ctrl.P2);
            _onApplied(View);
            ViewVersion++; // 悬停源缓存作废：全部悬停文本的数据源随本刷新重建（2026-10-18 性能）
        }

        /// <summary>轻刷（出牌终态/攻击宣言回包等）：构建视图→仅 2D 信息栏扇出（不走 3D 对账——
        /// 原口径：事件订阅路径随后会补全量对账）。</summary>
        public void RefreshLocal()
        {
            var core = _core();
            var ctrl = _ctrl();
            if (core == null || ctrl?.P1 == null || ctrl?.P2 == null) return;
            View = BattleView.BuildLocal(core, ctrl.Board, ctrl.P1, ctrl.P2);
            _onBarsOnly();
        }
    }
}
