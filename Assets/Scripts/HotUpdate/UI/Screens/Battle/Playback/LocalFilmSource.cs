using System;
using CardCore;
using CardCore.Serialization;

namespace SynergyUI
{
    /// <summary>
    /// 本地实时帧生产者（2026-11-11 战斗演出统一重构 S1·Battle/Playback/LocalFilmSource.cs）：
    /// 投影器三钩子（StackResolutionStart/End/StackEmpty）→ 建 BattleFrame 投给 BattlePlayhead。
    /// 原导演 OnStepResolved 的捕获逻辑（卡牌步链头补插/步标签/快照/日志水位）原样搬来——
    /// 生产者只产帧不解释，播放器只解释不产帧（方案 §2.4）。
    /// S2 起：投影器演出钩子捕获时构造 BattleFxOp 挂帧（双轨录制）；战报行按
    /// 「上一帧边界→本帧边界」归帧写 Lines/FrameIndex；GameStartEvent 补帧 #0（View）。
    /// </summary>
    internal sealed class LocalFilmSource
    {
        private readonly BattlePlayhead _playhead;
        private readonly Func<GameCore> _core;
        private readonly Func<BattleProjector> _projector;

        public LocalFilmSource(BattlePlayhead playhead, Func<GameCore> core, Func<BattleProjector> projector)
        {
            _playhead = playhead;
            _core = core;
            _projector = projector;
        }

        /// <summary>逆结算回合开始：转播放器按住日志（会话延迟到首帧落地才建——空轮零开销）。</summary>
        public void OnResolutionStart(StackResolutionStartEvent e) => _playhead.BeginRound();

        /// <summary>逆结算单步完成：捕获快照+日志水位建 Step 帧入队。
        /// 卡牌步快照补链头（2026-10-19 实测修复）：快照在对象出栈后捕获，ChainCards 已不含
        /// 本卡——无连锁的单发施放整段同步完成（无响应窗 await、置脏对账被抑制丢弃），链上
        /// 从未建过克隆=左侧恒无展示。补回后链舞台在回放期建槽（结算期只增不删口径不变）。</summary>
        public void OnStepResolved(StackResolutionEndEvent e)
        {
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
            var entries = MatchLogService.Entries;
            _playhead.Enqueue(new BattleFrame
            {
                Kind = BattleFrameKind.Step,
                View = snapshot,
                LogBoundary = entries.Count,
                LastEventId = entries.Count > 0 ? entries[entries.Count - 1].EventId : 0u, // S2 行→帧映射锚
                CardRid = card != null ? card.RuntimeId : 0u,
                Label = card != null ? MatchLogRenderer.Name(card) // 卡名单源=EffectText（观侧口径与战报一致）
                    : item != null && item.IsAttackDeclaration ? "攻击宣言"
                    : item != null && item.IsGuardDeclaration ? "守卫宣言"
                    : item != null && item.IsSBA ? "状态动作"
                    : item != null && item.IsPhaseWindow
                        ? (string.IsNullOrEmpty(item.PhaseWindowLabel) ? "阶段窗口" : item.PhaseWindowLabel + "窗口")
                        : "效果",
                IsAttack = item != null && item.IsAttackDeclaration,
                Remaining = e != null ? e.StackSize : 0,
            });
        }

        /// <summary>逆结算轮末（栈排干）：转播放器（空轮即时还原日志/有会话标记可收尾）。</summary>
        public void OnRoundStackEmpty(StackEmptyEvent e) => _playhead.NotifyRoundEmpty();
    }
}
