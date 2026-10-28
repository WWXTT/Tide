using System;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>
    /// HUD 时间/速度格驱动（2026-10-18 BattleScreen 功能拆分第五块外迁）：4Hz 节流喂文
    /// BattleHudRow 中线功能格。时间恒 ∞（2026-10-18 网络簇整删后无真人局——真人计时随联机
    /// 复活时随决策管线重建）；记速器 4Hz 兜底重写（格装台晚于本屏/栈事件间隙
    /// 同周期覆盖，装台晚于本屏——周期重写直至就绪（幂等））。
    /// 2026-10-28 元素缩编：临时元素产量双格随临时上限概念退役拆除（RefreshTempGain 删）。
    /// </summary>
    internal sealed class BattleHudTicker
    {
        private readonly Func<BattleViewData> _view;
        private double _accum; // 刷新节流（4Hz）

        private BattleViewData View => _view();

        public BattleHudTicker(Func<BattleViewData> view) => _view = view;

        /// <summary>每帧驱动（Updater 转发）：4Hz 到点重写两组格子。</summary>
        public void Tick()
        {
            _accum += Time.deltaTime;
            if (_accum < 0.25f) return;
            _accum = 0;
            RefreshChainSpeed(); // 记速器 4Hz 兜底重写（格装台晚于本屏/栈事件间隙同周期覆盖）
            // 人机对局：时间无限（不建钟不掉秒）
            BattleHudRow.SetStatusValue(BattleHudRow.SelfTimeCol, "∞");
            BattleHudRow.SetStatusValue(BattleHudRow.OppTimeCol, "∞");
        }

        /// <summary>连锁速度格（中线 x3）喂文（2026-10-15 接线）：本地引擎记速器实值——
        /// 0=无连锁、N=本连锁已发动的最高速度、结算中附「·结算」标记。
        /// 未装台/缺格静默（SetStatusValue 幂等——RefreshBars/RefreshView/4Hz 三路同喂）。</summary>
        public void RefreshChainSpeed()
        {
            var d = View;
            if (d == null || d.ChainSpeed < 0) return;
            BattleHudRow.SetStatusValue(BattleHudRow.ChainSpeedCol,
                d.ChainResolving ? d.ChainSpeed + "·结算" : d.ChainSpeed.ToString());
        }
    }
}
