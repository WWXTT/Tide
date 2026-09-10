using System;
using CardCore.Attribute;

namespace CardCore.Attribute.Handlers
{
    /// <summary>
    /// 召唤衍生物（SummonToken）——时点接线配套原子（原 PutToBattlefield 枚举位收编）。
    ///
    /// 设计定案：
    /// - token 有全套卡参数（模板 = CardData），与卡的核心区别是**不唯一**；
    /// - 实例 ID = 模板ID#序号（TimestampSystem.NextSequence，对局中临时赋值作索引，
    ///   先例 CopyEffectsEngine.GenerateCopyID；实例 ID 唯一化避免 TextChangeLayer._cardIdMap 同 ID 互覆）；
    /// - 落区由组合层 context.SummonDropZone 指定（战场/手牌/牌组三档，费用按落区系数计价——
    ///   注意 Zone.Hand==0：效果合成界面必须显式填落区，计费不做默认猜测）；
    /// - 模板解析经静态委托 ResolveTemplate（组合根注入 CardCatalog.GetById，
    ///   仿 MorphSystem.ResolveMorphTarget；未注入时复用变形解析器——同为模板ID→CardData）。
    ///
    /// 落战场的进场事件（Source=TokenSpawned）与触发式注册由 TryAddToBattlefield 统一出口承担；
    /// 入手走区域容器（禁止 Player.AddToHand 旧列表），非抽牌入手（IsDraw=false）喂 NonDrawDrawAccum。
    /// </summary>
    public class SummonTokenHandler : AtomicEffectHandlerBase
    {
        /// <summary>token 模板解析器：StringValue（模板卡 ID）→ CardData。由组合根注入；null 时静默跳过。</summary>
        public static Func<string, CardData> ResolveTemplate;

        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.SummonToken;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            if (context.ZoneManager == null || context.Controller == null) return;

            var resolver = ResolveTemplate ?? MorphSystem.ResolveMorphTarget;
            if (resolver == null) return;

            string templateId = effect.StringValue;
            if (string.IsNullOrEmpty(templateId)) return;
            var template = resolver(templateId);
            if (template == null) return;

            int count = effect.Value <= 0 ? 1 : effect.Value;
            var dropZone = context.SummonDropZone;

            for (int i = 0; i < count; i++)
            {
                // 全参数工厂 + 对局临时实例 ID
                var token = new CardWrapper(template)
                {
                    ID = $"{templateId}#{CardCore.TimestampSystem.NextSequence}"
                };
                token.SetController(context.Controller);

                switch (dropZone)
                {
                    case Zone.Hand:
                        // 容器 Add 不经 OnCardMoved——补发入手事件（非抽牌，FromZone=None=token 新生）
                        context.ZoneManager.GetZoneContainer(context.Controller)?.Add(token, Zone.Hand);
                        PublishEvent(new CardEnterHandEvent
                        {
                            Player = context.Controller,
                            Card = token,
                            FromZone = Zone.None,
                            IsDraw = false
                        });
                        break;

                    case Zone.Deck:
                        context.ZoneManager.GetZoneContainer(context.Controller)?.Add(token, Zone.Deck);
                        context.ZoneManager.ShuffleDeck(context.Controller);
                        break;

                    default:
                        // 战场：满场入墓+失败事件、进场事件(TokenSpawned)、触发式注册 均由统一出口承担
                        context.ZoneManager.TryAddToBattlefield(token, context.Controller, EnterSource.TokenSpawned);
                        break;
                }

                PublishEvent(new TokenCreatedEvent
                {
                    TokenTemplateId = templateId,
                    Controller = context.Controller,
                    Source = context.Source,
                    Tapped = token._isTapped,
                    Card = token,
                    DropZone = dropZone == Zone.Hand || dropZone == Zone.Deck ? dropZone : Zone.Battlefield
                });

                context.LastOutcome.AffectedTargets.Add(token);
            }
        }

        public override string GetDescription(AtomicEffectInstance effect) =>
            $"生成 {effect.Value} 个衍生物（{effect.StringValue}）";
    }
}
