using System;
using System.Collections.Generic;
using CardCore;
using CardCore.Attribute;
using CardCore.Effects;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>
    /// 战场投影器（2026-10-18 第二轮 B2 投影单点化）：**本屏唯一的引擎事件订阅点 + BattleViewData
    /// 所有权**。事件→置脏（经屏侧合帧队列）/演出指令构造（二期 A：只构造 BattleFxOp 交
    /// <see cref="LocalFilmSource.Present"/> 单源路由——闭包延迟队列已退役）/终局流转；
    /// 视图构建（BuildLocal）与 ViewVersion（悬停缓存失效源）单点持有。
    /// 北极星链路：逻辑层→事件流→（日志中间层 B1 + 本投影器）→UI。
    /// 纯 C# 类：EventManager 直订自管退订（Dispose——镜像 BattleLogController 语义）；
    /// 时序红线（沿用原 BattleScreen 口径）：WireEarly（抽卡飞入对）须先于 StartNewGame——
    /// 起手抽与首个回合自然抽同在开局内发完；Wire（对账订阅表）在 StartNewGame 之后（原序），
    /// 开局内事件由尾段补偿刷兜底。
    /// </summary>
    public sealed class BattleProjector : IDisposable
    {
        private readonly Func<GameCore> _core;
        private readonly Func<BattleController> _ctrl;
        private readonly Action<BattleViewData> _onApplied;   // 渲染扇出：field.Apply+castStage+bars+region+三键置脏（屏组装根接线）
        private readonly Action _onBarsOnly;                  // RefreshLocal 路径扇出（仅 2D 信息栏——出牌终态等的轻刷）
        private readonly Action _onGameStart;                 // 抽卡飞入武装点（GameStartEvent=起手抽后、首个 TurnStart 前）
        private readonly Action<CardActivationFailedEvent> _onActivationFailed; // 施放展示即清+演出中断
        private readonly Action<GameOverEvent> _onGameOver;   // 终局流转（弹窗/重试链——决策流，非投影）
        private readonly Action _clearPendingPins;            // TurnStart 清出牌选格残单
        private readonly Func<bool> _presentationBusy;        // 表现在途探针（二期 A 起接替旧「日志缓冲探针」：HP 读数
        //中间态按播放器 Busy 即时/随帧对账——见 On<LifeChangeEvent>）
        private readonly LocalFilmSource _film;               // 本地帧生产者+演出单源路由（生命周期三钩子透传；各演出钩子
        // 只构造 BattleFxOp 进 Present——轮内挂帧/轮外即时执行）

        private readonly List<Action> _unsubscribers = new List<Action>();
        private int _lastCoreNullLogFrame = -999; // 核心未就绪跳过的节流日志帧

        /// <summary>当前视图（渲染唯一输入——一写多读，写点只在 RefreshField/RefreshLocal 构建）。</summary>
        public BattleViewData View { get; private set; }

        /// <summary>战场视图版本（每次成功对账递增）——悬停源缓存失效源。</summary>
        public int ViewVersion { get; private set; }

        public BattleProjector(Func<GameCore> core, Func<BattleController> ctrl,
            Action<BattleViewData> onApplied, Action onBarsOnly,
            Action onGameStart,
            Action<CardActivationFailedEvent> onActivationFailed,
            Action<GameOverEvent> onGameOver, Action clearPendingPins,
            Func<bool> presentationBusy = null, LocalFilmSource film = null)
        {
            _core = core;
            _ctrl = ctrl;
            _onApplied = onApplied;
            _onBarsOnly = onBarsOnly;
            _onGameStart = onGameStart;
            _onActivationFailed = onActivationFailed;
            _onGameOver = onGameOver;
            _clearPendingPins = clearPendingPins;
            _presentationBusy = presentationBusy;
            _film = film;
        }

        // ==== 订阅（唯一订阅点） ====

        /// <summary>抽卡飞入对（StartNewGame 之前——时序红线见类注释）。</summary>
        public void WireEarly()
        {
            On<GameStartEvent>(e => { _onGameStart(); _film?.OnGameStart(e); }); // 后者=剧本开卷（帧 #0+头）
            On<CardEnterHandEvent>(e =>
            {
                // 抽卡飞入（2026-11-13 阶段1·L1-1）：op 单源——live 经 Present 即时执行（执行器回调
                // 武装扇区起点+卡堆脉冲，原 _armDrawFlyIn 直调收编）；剧本侧录进 PreOps（挂下一帧
                // 先于 View 应用——扇区 SetCards 建 新卡前起点已武装，回放才有弧线飞入）。
                // 轮内效果抽同口（Capture 轮内路径挂本步帧 PreOps，live/回放同拍）。
                if (e != null && e.IsDraw)
                    _film?.Present(new BattleFxOp { Kind = BattleFxKind.DrawFlyIn, Seat = SeatOf(e.Player) }, pre: true);
                QueueFans();
            });
        }

        /// <summary>对账订阅表（StartNewGame 之后，原序；引擎事件多但刷新幂等——统一对账重建）。</summary>
        public void Wire()
        {
            On<CardPlayEvent>(_ => { QueueFans(); QueueField(); });            // 出牌→单位/地牌落格
            // 金圈时序（2026-10-21 命运一抽绿→金修复）：入扇对账发生在 CardEnterHandEvent（移动内），
            // 早于 DrawnInStandbyThisTurn 的登记（CardDrawEvent——MatchStats 订阅在 GameCore 构造期，
            // 同一分派内先跑）——门卡首评恒绿；抽牌事件（此刻登记已完成）补一次刷扇重评即金。
            On<CardDrawEvent>(_ => QueueFans());
            On<CardZoneChangeEvent>(e => // 离场微拖尾（L2-2·阶段4）：弃牌/牌库弃置/除外——起点→对应
            {                                  // 漩涡一枚灰 Orb；战场→墓不走此口（死亡链演出已覆盖，防双播）
                PresentDepart(e);
                QueueFans(); QueueField();
            });
            On<CardMoveEvent>(_ => { QueueFans(); QueueField(); });
            On<CardDestroyEvent>(e => // 单位死亡→离场对账；效果死亡演出（二期 A 数据指令单源：
            {                          // DeathHold 引擎时点即时持票（防对账抢先销毁尸体），死亡演出轮内挂帧/轮外即时
                if (e != null && e.DestroyedCard != null)
                {
                    // 发动位抓拍须在事件期（回放期法术卡已入墓离区——事件期它还在发动区）：
                    // 死亡时序定案（2026-10-20）——消灭飞行体从法术位起飞、先等费用粒子到位
                    var castCell = ResolveCastFxCell(e.Source);
                    _film?.Present(new BattleFxOp { Kind = BattleFxKind.DeathHold, Target = FxRef.Of(e.DestroyedCard, SeatOf) }, pre: true);
                    _film?.Present(new BattleFxOp
                    {
                        Kind = BattleFxKind.Death,
                        Source = FxRef.Of(e.Source, SeatOf),
                        Target = FxRef.Of(e.DestroyedCard, SeatOf),
                        Reason = (int)e.Reason,
                        Tint = e.EffectTint.HasValue ? (int)e.EffectTint.Value : -1,
                        CastX = castCell?.x ?? -1,
                        CastZ = castCell?.z ?? -1,
                    });
                }
                QueueFans(); QueueField();
            });
            // 施放类型图标收口（2026-10-15 发动区改版回归修复）：入场/离开发动区/发动失败即时刷新——
            // 白板卡结算零事件（无指示物/无效果），缺此订阅图标滞留、3D 模型要等回合结束才上
            On<CardPutToBattlefieldEvent>(e => // 入场演出（二期 A：LocalFilmSource.OnArrival 构造 op 进 Present
            {                                  // ——颜色=最近实付；轮内挂帧/轮外即时）
                _film?.OnArrival(e);
                QueueFans(); QueueField();
            });
            // CardLeaveActivationEvent 已随 S3b 删发动区退役——结算离区刷新改由
            // StackResolutionEndEvent（步出栈）+ CardActivationFailedEvent（失败）双口驱动
            On<StackResolutionEndEvent>(_ => { QueueFans(); QueueField(); }); // 结算步完成：图标/卡面转 linger
            On<CardActivationFailedEvent>(e => // 发动失败（阻拦无效/费用/代价异常+满场入墓口）：展示即清不等 linger
            {
                _onActivationFailed(e);
                QueueFans(); QueueField();
            });
            On<StackResolutionStartEvent>(e => _film?.OnResolutionStart(e)); // 逆结算回合开始（2026-10-19 步进回放）
            On<StackResolutionEndEvent>(e => _film?.OnStepResolved(e));      // 逆结算单步完成
            On<StackEmptyEvent>(e => { _film?.OnRoundStackEmpty(e); QueueField(); }); // 栈清空=一批结算完成的全量对账兜底
            On<CounterChangedEvent>(e => // 指示物→对手卡翻面/攻生→头顶数值；属性族附浮字（白名单外零成本直返）
            {
                // 诅咒/祝福牌库标记（2026-10-26）：施加与消层（抽到发作 Amount=0）——爆发只在施加时播
                bool curseBless = e != null
                    && (e.CounterType == CounterRules.CurseCounter || e.CounterType == CounterRules.BlessingCounter);
                if (e != null)
                {
                    if (curseBless)
                    {
                        if (e.Amount > 0) // 爆发只在施加时（消层无粒子）
                            _film?.Present(new BattleFxOp
                            {
                                Kind = BattleFxKind.DeckMark,
                                Target = FxRef.Of(e.Target, SeatOf),
                                Amount = e.Amount,
                                Flag = e.CounterType == CounterRules.BlessingCounter,
                            });
                    }
                    else if (e.EffectTint != null && e.Amount > 0)
                    {
                        var polarity = CounterRules.Find(e.CounterType)?.Polarity ?? CounterPolarity.Neutral;
                        if (polarity != CounterPolarity.Neutral) // 中性（变形等状态标记）不播——与屏侧同闸
                            _film?.Present(new BattleFxOp
                            {
                                Kind = BattleFxKind.CounterBurst,
                                Source = FxRef.Of(e.Source, SeatOf),
                                Target = FxRef.Of(e.Target, SeatOf),
                                Tint = (int)e.EffectTint.Value,
                                Flag = polarity == CounterPolarity.Negative,
                            });
                    }
                    _film?.Present(new BattleFxOp // 值浮字（2026-11-13 P2 统一入口）：全指示物类型经
                    {                               // ValueFloatRules 拼标签+极性（捕获侧定型——回放/联机零推导；
                        Kind = BattleFxKind.ValueFloat,   // §3.5 豁免："*"换区标记/零增量/未知 id 由 TryBuild 拦截）
                        Target = FxRef.Of(e.Target, SeatOf),
                        Text = ValueFloatRules.TryBuild(e.CounterType, e.Amount, out var vfLabel, out var vfPolarity)
                            ? vfLabel : null,
                        Amount = e.Amount,
                        Polarity = vfPolarity,
                    });
                }
                QueueFans(); QueueField();
            });
            On<KeywordEvent>(e => // 赋予关键词爆发粒子（2026-11-01）：IsAdd 全仓唯一发布点=GrantKeywordHandler
            {                            //——印刷关键词入轨走 AddKeyword 纯状态口不发事件，钩子天然只捕「赋予落地」
                if (e != null && e.IsAdd)
                    _film?.Present(new BattleFxOp
                    {
                        Kind = BattleFxKind.KeywordBurst,
                        Source = FxRef.Of(e.Source, SeatOf),
                        Target = FxRef.Of(e.Target, SeatOf),
                        Tint = e.EffectTint.HasValue ? (int)e.EffectTint.Value : -1,
                        Flag = e.IsDebuff,
                    });
            });
            On<RevealCardsEvent>(_ => QueueFans());
            // 棋盘数字条：黑白钱包与地牌横置跨回合清零（TurnStart）、角色生命（LifeChange）、
            // 钱包入账/支付（ElementPool 两事件）——RefreshField 全量对账覆盖
            On<TurnStartEvent>(_ => { _clearPendingPins(); QueueField(); }); // 出牌选格残单随回合清
            On<PhaseStartEvent>(e => // TurnStart 先于切阶段发布：缺此订阅 View.Phase 滞留上回合 End（回合≥2 点单位被误拒）；
            {                     // 阶段播报（2026-10-26 事件序喂数）：相位事件载荷→PhaseAnnounce op
                                  //（轮外即时入 HUD 队列按 1s/拍播出——同帧瞬态 Standby 不再被合帧对账吞掉）
                QueueField();
                if (e != null)
                    _film?.Present(new BattleFxOp { Kind = BattleFxKind.PhaseAnnounce, Seat = SeatOf(e.ActivePlayer), Reason = (int)e.Phase });
            });
            On<LifeChangeEvent>(e => // 角色HP读数·表现层真相源（2026-10-20 用户定案）：引擎同帧瞬达、
            {                       // 步帧只带终值——中间值（宣言帧拼点奖励 30→24 等）只能靠"行
                                    // 可见"时刻的事件态同步对账上屏。未在播=行即刻入史可见→立即全量
                                    // 对账（此刻引擎即中间态）；在播（结算回放期）=行随帧放（终值归
                                    // 步进快照/攻击步另有命中节拍）→照旧只置脏（抑制期丢弃）。
                if (_presentationBusy == null || !_presentationBusy()) RefreshField();
                QueueField();
                // 值浮字按 Cause 分流（2026-11-13 P2·§3.5 去重 1）：伤害/回复/疲劳路径各有专属演出
                //与浮字（Damage/Heal/Fatigue op）——LifeChange 不再重复出值；None=旧发布点视同伴随。
                // Lifelink（系命回复角色）/Modify/Set/Lock（钳制差额）无其它浮字承载，此处出 L ±N。
                if (e != null && e.NewLife != e.OldLife
                    && (e.Cause == LifeChangeCause.Lifelink || e.Cause == LifeChangeCause.Modify
                        || e.Cause == LifeChangeCause.Set || e.Cause == LifeChangeCause.Lock))
                    _film?.Present(new BattleFxOp
                    {
                        Kind = BattleFxKind.ValueFloat,
                        Target = FxRef.Of(e.Player, SeatOf),
                        Text = ValueFloatRules.Sign(e.NewLife - e.OldLife),
                        Amount = e.NewLife - e.OldLife,
                        Polarity = e.NewLife > e.OldLife ? 1 : -1,
                    });
            });
            On<StatModifyEvent>(e => // 属性直改值浮字（2026-11-13 P2·§1 表 9/10）：「加攻击力浮字」对齐——
            {                       // 指示物涨有浮字、直改没有的同一件事两种观感自此统一；角色生命直改同时刷血条。
                if (e == null || e.NewValue == e.OldValue) return; // 差 0 不出（§3.5 D2）
                _film?.Present(new BattleFxOp
                {
                    Kind = BattleFxKind.ValueFloat,
                    Target = FxRef.Of(e.Target, SeatOf),
                    Text = StatFloatLabel(e.StatType, e.NewValue - e.OldValue),
                    Amount = e.NewValue - e.OldValue,
                    Polarity = e.NewValue > e.OldValue ? 1 : -1,
                });
                if (e.StatType != StatType.Power) QueueField(); // 生命/双属性直改→血条对账（角色 ModifyLife 写点 P2 承担）
            });
            On<StatSetEvent>(e => // 设置型属性改写值浮字（2026-11-13 P2）：旧→新算差（§3.3「设为」行）
            {
                if (e == null || e.NewValue == e.OldValue) return;
                _film?.Present(new BattleFxOp
                {
                    Kind = BattleFxKind.ValueFloat,
                    Target = FxRef.Of(e.Target, SeatOf),
                    Text = StatFloatLabel(e.StatType, e.NewValue - e.OldValue),
                    Amount = e.NewValue - e.OldValue,
                    Polarity = e.NewValue > e.OldValue ? 1 : -1,
                });
                if (e.StatType != StatType.Power) QueueField();
            });
            // QueueFans/QueueField 在别处（订阅表闭包捕获的置脏口）——StatFloatLabel 为本文件私有辅助。
            On<FatigueEvent>(e => // 疲劳受击演出（红闪脉冲+头顶浮字——信息性表演占视觉门）
            {
                if (e != null)
                    _film?.Present(new BattleFxOp { Kind = BattleFxKind.Fatigue, Seat = SeatOf(e.Player), Amount = e.Damage });
            });
            On<AtomicDamageEvent>(e => // 效果伤害演出（2026-10-19：法术/效果伤害此前零专属表现——教学二 1 伤卡根因）；
            {                            // IsCombatDamage 过滤防与战斗编舞双播（战斗伤害走 CombatChoreographer 受击节拍）
                // 发动位抓拍须在事件期（回放期法术卡已入墓离区——事件期它还在发动区）：
                // 费用粒子→弹体从法术位起飞→目标受击/死亡的时序链（2026-10-20 用户定案）
                if (!e.IsCombatDamage)
                {
                    var castCell = ResolveCastFxCell(e.Source);
                    _film?.Present(new BattleFxOp
                    {
                        Kind = BattleFxKind.Damage,
                        Source = FxRef.Of(e.Source, SeatOf),
                        Target = FxRef.Of(e.Target, SeatOf),
                        Amount = e.Damage,
                        Tint = e.EffectTint.HasValue ? (int)e.EffectTint.Value : -1,
                        CastX = castCell?.x ?? -1,
                        CastZ = castCell?.z ?? -1,
                        Flag = e.IsArea, // 范围伤害（L4-4）：AOE 走 Area 大件、命中链退为回落
                    });
                }
                QueueField();
            });
            On<HealEvent>(e => // 回复演出（吃效果色·L4-1；溢出截断实效——Heal 原子来源带 Tint，
            {                  // 吸血/系命/再生/溢出 null=回落绿）
                if (e != null)
                    _film?.Present(new BattleFxOp
                    {
                        Kind = BattleFxKind.Heal,
                        Target = FxRef.Of(e.Target, SeatOf),
                        Amount = e.Amount - e.Overfill,
                        Tint = e.EffectTint.HasValue ? (int)e.EffectTint.Value : -1,
                    });
                QueueField();
            });
            On<EffectFlightEvent>(e => // 效果飞行演出（2026-10-20 定案：效果无效/不适用≠发动无效——
            {                          // 费已付弹体照飞；发动无效在 ResolveCardCastAsync 早退=零表现零事件）
                var castCell = ResolveCastFxCell(e.Source); // 事件期抓拍（回放期法术卡已入墓）
                _film?.Present(new BattleFxOp
                {
                    Kind = BattleFxKind.Flight,
                    Source = FxRef.Of(e.Source, SeatOf),
                    Target = FxRef.Of(e.Target, SeatOf),
                    Tint = e.Tint.HasValue ? (int)e.Tint.Value : -1,
                    CastX = castCell?.x ?? -1,
                    CastZ = castCell?.z ?? -1,
                });
                QueueField();
            });
            On<ElementPoolGainEvent>(_ => QueueField());
            On<ElementPoolPayEvent>(e => // 最近实付账单（入场演出颜色口径——须与入场载荷同步，防后步付费覆写前步颜色）
            {
                _film?.OnPaid(e); // 记账本+打出法术扣费粒子 op（Present 路由）
                QueueField();
            });
            // 放地入池（2026-10-14）：入池路径只发本事件（移区不发 CardMove/ZoneChange）——手牌收卡+地格计数即时对账
            On<ElementPoolAddEvent>(e => // 放地短闪（L2-2·阶段4）：对应元素地格顶面一枚小 Hit 件
            {
                if (e != null && e.Player != null)
                    _film?.Present(new BattleFxOp { Kind = BattleFxKind.LandPulse, Seat = SeatOf(e.Player), Tint = (int)e.LockedColor });
                QueueFans(); QueueField();
            });
            // 攻击宣言（2026-10-14 优先权半场高光）：宣言即优先权翻转（守卫窗口对手持有）
            On<AttackDeclarationEvent>(_ => QueueField());
            On<ClashResolvedEvent>(e => // 拼点演出（2026-10-20：攻击宣言期引擎结算同步到——差值浮字随展示翻面同拍）
            {
                if (e != null)
                    _film?.Present(new BattleFxOp
                    {
                        Kind = BattleFxKind.Clash,
                        Target = FxRef.Of(e.EngineCard, SeatOf),
                        D1 = e.MyPower, D2 = e.TheirPower, Threshold = e.Threshold, Flag = e.Success,
                    });
            });
            On<LuckRolledEvent>(e => // 运势掷骰浮字（2026-10-20 一步点击定案：主动/自动两路掷骰同源结构化事件——成败都发）
            {
                if (e != null)
                    _film?.Present(new BattleFxOp
                    {
                        Kind = BattleFxKind.LuckRoll,
                        Target = FxRef.Of(e.EngineCard, SeatOf),
                        D1 = e.D1, D2 = e.D2, Flag = e.Success,
                    });
            });
            On<GameOverEvent>(e => { _onGameOver(e); _film?.OnGameOver(e); }); // 后者=剧本封卷（End 帧+行归帧+落盘）
        }

        /// <summary>座位归属（op 构造辅助：0=我方 1=对手；未知 -1）。</summary>
        private int SeatOf(Player p)
        {
            var core = _core();
            return p == null || core == null ? -1 : p == core.Player2 ? 1 : 0;
        }

        /// <summary>离场微拖尾 op 构造（L2-2·阶段4）：NewZone∈{墓地,除外} 且非「战场→墓」（死亡链
        /// 演出已覆盖）→ Depart op。战场来源除外：起点格**捕获时定格**（回放期实体已亡查不到格）——
        /// 从当前对账 View 按 Rid 查；手牌/牌库来源不带格（执行端回落牌堆顶锚近似）。</summary>
        private void PresentDepart(CardZoneChangeEvent e)
        {
            if (e?.Card == null || _film == null) return;
            bool grave = e.NewZone == Zone.Graveyard, exile = e.NewZone == Zone.Exile;
            if (!grave && !exile) return;
            if (grave && e.OldZone == Zone.Battlefield) return; // 死亡链（碎裂+沉降）已覆盖——防双播
            int x = -1, z = -1;
            if (e.OldZone == Zone.Battlefield && View != null)
            {
                // View 只读查找（事件期卡仍在旧区——上一次对账 View 里格位仍有效）
                BattleCardView uv = null;
                foreach (var c in View.SelfUnits) if (c != null && c.RuntimeId == e.Card.RuntimeId) { uv = c; break; }
                if (uv == null) foreach (var c in View.OppUnits) if (c != null && c.RuntimeId == e.Card.RuntimeId) { uv = c; break; }
                if (uv != null) { x = uv.X; z = uv.Z; }
            }
            _film?.Present(new BattleFxOp
            {
                Kind = BattleFxKind.Depart,
                Target = FxRef.Of(e.Card, SeatOf),
                Seat = SeatOf(e.Controller),
                Reason = exile ? 1 : 0,
                CastX = x,
                CastZ = z,
            });
        }

        private void On<T>(Action<T> handler) where T : class, IGameEvent
        {
            EventManager.Instance.Subscribe(handler);
            _unsubscribers.Add(() => EventManager.Instance.Unsubscribe(handler));
        }

        /// <summary>法术伤害表现的发动位抓拍（2026-10-20 时序定案；S3b A 切源读栈）：
        /// 伤害来源=角色（法术效果来源归因）且其栈上有施放中法术 → 弹体自施放者角色格起飞
        ///（S3b 收口 2026-11-05：施放贴片格已随「栈没有实体」退役——起点改角色格）；
        /// 否则 null——单位来源/技能/触发效果照旧从来源锚点起飞。**事件期求值**：回放期卡已入墓。</summary>
        private (int x, int z)? ResolveCastFxCell(Entity source)
        {
            if (!(source is Player p)) return null;
            var core = _core();
            var board = _ctrl()?.Board;
            if (core?.StackEngine == null || board == null) return null;
            Card spell = null;
            foreach (var c in core.StackEngine.GetPendingCastCards(p))
                if (c != null && c.IsSpellCard()) { spell = c; break; }
            if (spell == null) return null;
            var (x, z) = board.CharacterCell(p);
            return (x, z);
        }

        public void Dispose()
        {
            foreach (var unsub in _unsubscribers) unsub();
            _unsubscribers.Clear();
        }

        // ==== 置脏（合帧节流：事件订阅置脏，帧首 Flush 一次——2026-10-18 性能口径） ====

        private bool _fansQueued, _fieldQueued;

        /// <summary>播放期对账抑制（2026-10-19 逆结算步进；二期 A 起仅保留「播放期不自动对账」用途
        ///——置位期间 Flush 不做自动对账（引擎已瞬达终态，直刷会把最终结果一次怼到屏上——正是
        /// 帧回放要防的）；解除方负责随后强制全量对账。演出载荷已数据化（Present 路由），不再有延迟队列。</summary>
        public bool SuppressAutoFlush { get; set; }

        /// <summary>排队全量刷扇（事件订阅口）：本帧多事件合并。直接调用语义不变：
        /// 交互/教学等一次性刷新仍走屏侧 RefreshFans() 同步路径。</summary>
        public void QueueFans() => _fansQueued = true;

        /// <summary>排队全量对账（语义同 QueueFans）。</summary>
        public void QueueField() => _fieldQueued = true;

        /// <summary>属性直改/设为的浮字标签（2026-11-13 P2/P3）：单源=CardCore.ValueFloatRules.StatDeltaLabel
        ///（本地 Projector 与联机 NetFxMapper 同标签四端一致）。</summary>
        private static string StatFloatLabel(StatType stat, int delta) => ValueFloatRules.StatDeltaLabel(stat, delta);

        /// <summary>帧首冲刷（Updater 第一项）：刷扇回调经屏侧注入（扇区是屏组件），对账走本类。
        /// 播放抑制期跳过（置脏丢弃——抑制解除后由播放器 FinishCore 强制 RefreshField 兜底对账）。</summary>
        public void Flush(Action refreshFans)
        {
            if (SuppressAutoFlush) { _fansQueued = false; _fieldQueued = false; return; }
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
            // 棋盘先于视图（2026-10-21 段零召唤物无视觉终根因）：投影器与 BoardState.AutoResync
            // 同订 CardZoneChangeEvent 且订阅序在前——同一事件的视图快照建于棋盘落格之前（召唤物
            // X=-1 对账跳过建视觉）；Resync 本身不发事件、无补偿刷新，其后无刷新事件即永久滞留
            //（教学段零召唤后正无事件，考验/教学二靠换场与自由操作自愈故曾表现正常）。
            // 视图构建前显式 Resync（幂等全量重建，字典级开销）——对订阅序免疫。
            ctrl.Board?.Resync();
            View = BattleView.BuildLocal(core, ctrl.Board, ctrl.P1, ctrl.P2);
            _onApplied(View);
            ViewVersion++; // 悬停缓存作废：全部悬停文本的数据源随本刷新重建（2026-10-18 性能）
        }

        /// <summary>轻刷（出牌终态/攻击宣言回包等）：构建视图→仅 2D 信息栏扇出（不走 3D 对账——
        /// 原口径：事件订阅路径随后会补全量对账）。</summary>
        public void RefreshLocal()
        {
            var core = _core();
            var ctrl = _ctrl();
            if (core == null || ctrl?.P1 == null || ctrl?.P2 == null) return;
            ctrl.Board?.Resync(); // 同 RefreshField：视图快照前棋盘先落格（订阅序免疫）
            View = BattleView.BuildLocal(core, ctrl.Board, ctrl.P1, ctrl.P2);
            _onBarsOnly();
        }

        // ==== 步进回放（2026-10-19 逆结算定案） ====

        /// <summary>捕获当前引擎真值快照（回放步数据源）：BuildLocal 纯构建——不走 onApplied
        /// 渲染扇出、不动 ViewVersion；快照的应用时机归 ApplySnapshot（回放节拍驱动）。</summary>
        public BattleViewData CaptureSnapshot()
        {
            var core = _core();
            var ctrl = _ctrl();
            if (core == null || ctrl?.P1 == null || ctrl?.P2 == null) return null;
            return BattleView.BuildLocal(core, ctrl.Board, ctrl.P1, ctrl.P2);
        }

        /// <summary>应用回放快照：View 赋值+渲染扇出+版本递增（View 所有权单点不变——快照也经
        /// 投影器入 View，悬停缓存等下游消费天然跟随）。</summary>
        public void ApplySnapshot(BattleViewData d)
        {
            if (d == null) return;
            View = d;
            _onApplied(d);
            ViewVersion++;
        }
    }
}
