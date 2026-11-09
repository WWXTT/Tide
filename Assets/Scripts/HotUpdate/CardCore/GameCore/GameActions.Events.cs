using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;

namespace CardCore
{
    /// <summary>
    /// 玩家可执行的操作 — 统一入口

    /// <summary>
    /// 充能事件（2026-11-05 充能系统动作化改版）：每次充能结算发布——UI/AI 可观测的进度变化信号
    ///（充能=速度0系统动作，免费落层不支付元素；进度本身=四色费用降低指示物层数，换区清除随
    /// CounterChangedEvent/快照可见。旧 P12「现付 1 点该色元素」已随改版退役）。
    /// </summary>
    public class CardChargedEvent : GameEventBase
    {
        public Player Player { get; set; }
        public Card Card { get; set; }
        /// <summary>本次充能的颜色（四色之一——任意不可充）。</summary>
        public ManaType Color { get; set; }
        /// <summary>本次充能后该卡该色累计已充层数（=同色费用降低指示物层数）。</summary>
        public int PaidOnColor { get; set; }
    }

    /// <summary>
    /// CardData 包装标记接口
    /// </summary>
    public interface CardDataWrapper { }
}
