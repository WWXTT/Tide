using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using CardCore.Attribute;

namespace CardCore.Attribute.Handlers
{
    // ================================================================
    // 充能（2026-10-31 P12：建造→充能改版，表行 afa24bf3）
    // 旧 CardWrapper.BuildPaid 逐色预付账本整体退役——充能改挂**四色费用降低指示物**
    //（CostDownGray/Red/Blue/Green，Resident 换区清），与旧 CostDown 指示物同一清除口径，
    // 天然实例级（同名副本/跨玩家互不共享，P7b 的返工诉求由机制形态自然满足）。
    // ================================================================

    /// <summary>
    /// 充能原子处理器（主动效果、速度 0、只能从手牌发动——发动口 GameActions.ActivateCharge，
    /// CanCharge 单源闸门；AI 枚举 LegalActionEnumerator.EnumerateCharge）。
    /// 结算步：扣 1 点所选色元素（现取现用）→ 手牌卡 +1 层同色费用降低指示物 →
    /// 首次充能展示这张卡（Exposed + RevealCardsEvent，二值不叠层）→ 发布 CardChargedEvent。
    /// 卡留在手牌：不横置、不消耗行动次数/耐久；可重复至上限（CanCharge 闸）；
    /// 换区清除（Resident 指示物口径；发动区暂存豁免照旧由 ZoneContainer 保证，施放付费时折扣还在）。
    /// 颜色来自 context.ChargeColor（ActivateCharge 随 PendingEffect 透传）；空/Any/非手牌源=空转。
    /// </summary>
    public class ChargeHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.Charge;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
            => ExecuteAsync(effect, context).GetAwaiter().GetResult();

        public override UniTask ExecuteAsync(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            var color = context.ChargeColor;
            if (color == null || color == ManaType.Any)
            {
                TideLog.Warn("[Charge] 充能色缺失（非充能发动路径直调？）——空转");
                return UniTask.CompletedTask;
            }

            var card = context.Source as CardCore.Card;
            if (card == null || card.GetZone() != CardCore.Zone.Hand)
            {
                TideLog.Warn($"[Charge] 充能源不在手牌（{card?.ID ?? "null"} @ {card?.GetZone()}）——空转");
                return UniTask.CompletedTask;
            }

            var counterId = CounterRules.CostDownCounterFor(color.Value);
            if (counterId == null)
            {
                TideLog.Warn($"[Charge] 非法充能色 {color}——空转");
                return UniTask.CompletedTask;
            }

            // 扣 1 点该色（现取现用：钱包优先，不足自动横置地牌）；付不出=空转不放置
            if (context.ElementPool == null
                || !context.ElementPool.TryPayCostWithAutoTap(
                    CardCore.ElementCost.FromValue(color.Value, 1), context.Controller,
                    $"充能:{(card as CardCore.IHasName)?.CardName}"))
            {
                TideLog.Warn($"[Charge] 元素不足（1 {color}）——空转（不放置指示物）");
                return UniTask.CompletedTask;
            }

            card.AddCounters(counterId, 1, context.Source);

            // 展示这张卡（信息轴「展示」口径：二值不叠层——已展示不重复挂/不重复发事件）
            if (card.GetCounterCount(CounterRules.ExposedCounter) == 0)
            {
                card.AddCounters(CounterRules.ExposedCounter, 1, context.Source);
                PublishEvent(new RevealCardsEvent
                {
                    Player = card.GetController() ?? context.Controller,
                    Cards = new List<CardCore.Card> { card },
                    Source = context.Source,
                });
            }

            PublishEvent(new CardChargedEvent
            {
                Player = context.Controller,
                Card = card,
                Color = color.Value,
                PaidOnColor = card.GetCounterCount(counterId),
            });
            return UniTask.CompletedTask;
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect)
            => "充能：支付1点对应颜色元素，为此手牌放置1层同色费用降低指示物（展示这张卡；换区清除）";
    }
}
