using System;
using System.Collections.Generic;
using System.Linq;
using CardCore.Attribute;

namespace CardCore
{
    // ================================================================
    // 代价接口与数据
    // ================================================================

    /// <summary>
    /// 代价类型
    /// </summary>
    public enum CostType
    {
        /// <summary>元素消耗</summary>
        ElementConsume,
        /// <summary>弃牌</summary>
        DiscardCard,
        /// <summary>扣除玩家生命</summary>
        LifePayment,
        /// <summary>沉睡（翻面+苏醒倒计时）</summary>
        Sleep,
        /// <summary>召唤素材（额外卡组条件）</summary>
        SummonMaterial,
        /// <summary>送墓（本组）：将牌库顶 N 张送入墓地（代价抵消用）</summary>
        MillDeck,
        /// <summary>送额外组：将额外卡组 N 张送墓（代价抵消用）</summary>
        SendExtraDeck,
        // OpponentDraw / OpponentHeal 已删除（2026-09-10：被 Polarity 错边折价顶替——
        // "有益原子锁对方域"自动减费取代显式跨边代价；当日卡数据 Costs 零使用，删值重排无影响）。
        /// <summary>自身减益（紊乱指示物，当量 1/条，2026-09-08 拓展）：Value=层数、TurnDuration=持续回合</summary>
        SelfSickness,
        /// <summary>对手增益（属性增加指示物 +1/+1，当量 1/层，2026-09-08 拓展）：Value=层数</summary>
        OpponentBuff,
        /// <summary>效果型代价（2026-09-11 定案）：代价栏装载的强制原子效果（如「给对手召唤 30/30 衍生物」），
        /// 在付费步执行并按全价补偿黑/白元素。Payload 字段承载原子。枚举追加在尾，存量序列化值不动。</summary>
        Payload,
    }

    /// <summary>
    /// 代价实例
    /// </summary>
    [Serializable]
    public class CostInstance
    {
        /// <summary>代价类型</summary>
        public CostType Type;

        /// <summary>数值（元素数量/弃牌数/生命值/沉睡回合数）</summary>
        public int Value;

        /// <summary>法力类型（元素消耗专用）</summary>
        public ManaType ManaType;

        /// <summary>沉睡持续回合数</summary>
        public int TurnDuration;

        /// <summary>召唤方式（召唤素材专用）</summary>
        public SummonMethod SummonMethod;

        /// <summary>素材筛选器（召唤素材专用）</summary>
        public ITargetFilter TargetFilter;

        /// <summary>效果型代价的原子（CostType.Payload 专用，2026-09-11）：付费步强制执行并按全价补偿黑/白。</summary>
        public AtomicEffectInstance Payload;
    }

    /// <summary>
    /// 代价执行上下文
    /// </summary>
    public class CostContext
    {
        /// <summary>支付者</summary>
        public Player Payer;

        /// <summary>区域管理器</summary>
        public ZoneManager ZoneManager;

        /// <summary>元素池系统</summary>
        public ElementPoolSystem ElementPool;

        /// <summary>效果来源</summary>
        public Entity Source;
    }

    // ================================================================
    // 代价处理器接口与注册表
    // ================================================================

    /// <summary>
    /// 代价处理器接口
    /// </summary>
    public interface ICostHandler
    {
        /// <summary>处理的代价类型</summary>
        CostType CostType { get; }

        /// <summary>检查是否可以支付</summary>
        bool CanPay(CostInstance cost, CostContext context);

        /// <summary>执行支付</summary>
        void Pay(CostInstance cost, CostContext context);

        /// <summary>获取代价描述</summary>
        string GetDescription(CostInstance cost);
    }

    /// <summary>
    /// 代价处理器注册表
    /// </summary>
    public static class CostHandlerRegistry
    {
        private static readonly Dictionary<CostType, ICostHandler> _handlers
            = new Dictionary<CostType, ICostHandler>();

        public static void Register(ICostHandler handler)
        {
            if (handler != null)
                _handlers[handler.CostType] = handler;
        }

        public static ICostHandler GetHandler(CostType type)
        {
            return _handlers.TryGetValue(type, out var handler) ? handler : null;
        }

        public static bool CanPay(CostInstance cost, CostContext context)
        {
            var handler = GetHandler(cost.Type);
            return handler?.CanPay(cost, context) ?? false;
        }

        public static bool Pay(CostInstance cost, CostContext context)
        {
            var handler = GetHandler(cost.Type);
            if (handler == null) return false;
            if (!handler.CanPay(cost, context)) return false;
            handler.Pay(cost, context);
            return true;
        }

        /// <summary>
        /// 检查所有代价是否都可以支付
        /// </summary>
        public static bool CanPayAll(List<CostInstance> costs, CostContext context)
        {
            return costs.All(c => CanPay(c, context));
        }

        /// <summary>
        /// 支付所有代价（原子：先确认可支付「全部」代价，再逐项支付）。
        /// 遵循 MTG 601.2 的「确定总代价 → 不可撤销地支付」模型：
        /// 任一代价不可支付则整体失败且不支付任何代价，从而避免「部分支付」造成的资源丢失。
        /// （不采用支付后回滚：代价事件一旦发布即可能触发观察者，真正的撤销并不安全。）
        /// </summary>
        public static bool PayAll(List<CostInstance> costs, CostContext context)
        {
            if (costs == null || costs.Count == 0)
                return true;

            // 预检：未能确认可支付全部代价时，不开始任何支付
            if (!CanPayAll(costs, context))
                return false;

            foreach (var cost in costs)
            {
                if (!Pay(cost, context))
                    return false;
            }
            return true;
        }
    }

    // ================================================================
    // 内置代价处理器
    // ================================================================

    /// <summary>
    /// 元素消耗代价处理器
    /// 复用 ElementPoolSystem 的 ConsumeElement / CanConsume
    /// </summary>
    public class ElementConsumeCostHandler : ICostHandler
    {
        public CostType CostType => CostType.ElementConsume;

        public bool CanPay(CostInstance cost, CostContext context)
        {
            if (context.ElementPool == null || context.Payer == null) return false;
            var costDict = new Dictionary<int, float> { { (int)cost.ManaType, cost.Value } };
            return context.ElementPool.CanPayCost(costDict, context.Payer);
        }

        public void Pay(CostInstance cost, CostContext context)
        {
            var costDict = new Dictionary<int, float> { { (int)cost.ManaType, cost.Value } };
            context.ElementPool.PayCost(costDict, context.Payer);
        }

        public string GetDescription(CostInstance cost)
        {
            return $"消耗 {cost.Value} 点{cost.ManaType} 元素";
        }
    }

    /// <summary>
    /// 弃牌代价处理器
    /// </summary>
    public class DiscardCardCostHandler : ICostHandler
    {
        public CostType CostType => CostType.DiscardCard;

        public bool CanPay(CostInstance cost, CostContext context)
        {
            if (context.ZoneManager == null || context.Payer == null) return false;
            var hand = context.ZoneManager.GetCards(context.Payer, Zone.Hand);
            return hand.Count >= cost.Value;
        }

        public void Pay(CostInstance cost, CostContext context)
        {
            var hand = context.ZoneManager.GetCards(context.Payer, Zone.Hand);
            for (int i = 0; i < cost.Value && i < hand.Count; i++)
            {
                var card = hand[hand.Count - 1 - i]; // 从最后一张开始弃
                context.ZoneManager.MoveCard(card, context.Payer, Zone.Hand, Zone.Graveyard);
                EventManager.Instance.Publish(new CardDiscardCostEvent
                {
                    Player = context.Payer,
                    Card = card,
                    Source = context.Source
                });
            }
        }

        public string GetDescription(CostInstance cost)
        {
            return $"弃 {cost.Value} 张牌";
        }
    }

    /// <summary>
    /// 扣除生命代价处理器（2026-09-11 定案：改扣生命上限）。
    /// 超出的当前血一起裁掉；本来就受伤只扣上限；归零=正常死亡。
    /// </summary>
    public class LifePaymentCostHandler : ICostHandler
    {
        public CostType CostType => CostType.LifePayment;

        public bool CanPay(CostInstance cost, CostContext context)
        {
            if (context.Payer == null) return false;
            // 定案（死亡术语表，随扣上限口径平移）：可付到恰好归零——归零视为正常死亡（死因=LifePayment，
            // 死亡来源=自己；效果归因见 LifePaymentCostEvent.Source）。付不出（上限低于代价）才不可付。
            return context.Payer.MaxHealth >= cost.Value;
        }

        public void Pay(CostInstance cost, CostContext context)
        {
            context.Payer.DecreaseMaxHealth(cost.Value);
            EventManager.Instance.Publish(new LifePaymentCostEvent
            {
                Player = context.Payer,
                Amount = cost.Value,
                Source = context.Source
            });
            // 抵扣可归零（定案：归零=正常死亡，来源=自己），但此处只扣不发终局——
            // 生命判负属连锁结算后的检查（EffectExecutionEngine.FinishResolution →
            // CheckLifeGameOver，幂等），连锁中途归零不立即终局（效果照常结算完）。
        }

        public string GetDescription(CostInstance cost)
        {
            return $"支付 {cost.Value} 点生命上限";
        }
    }


    /// <summary>
    /// 沉睡代价处理器（2026-09-11 统一指示物模型）：翻面（Tap）+ 沉睡指示物 ×持续回合数
    ///（持有期间无法重置——回合开始逐层倒数；效果无效——拦触发式+启动式）。
    /// 不再挂 Sleeping 关键词/Awakening 倒计时（效果/指示物分离定案：规则全在指示物上）。
    /// </summary>
    public class SleepCostHandler : ICostHandler
    {
        public CostType CostType => CostType.Sleep;

        public bool CanPay(CostInstance cost, CostContext context)
        {
            // 来源必须是场上生物且未横置
            return context.Source != null
                && context.Source.IsAlive
                && !context.Source.IsTapped()
                && context.Source is Card;
        }

        public void Pay(CostInstance cost, CostContext context)
        {
            var target = context.Source;
            int duration = cost.TurnDuration > 0 ? cost.TurnDuration : cost.Value;

            // 沉睡=横置进沉睡（不走 ShouldTap——警戒已重定义，无横置抵扣）
            target.Tap();
            target.AddCounters(Attribute.KeywordRules.SleepCounter, duration, context.Source);

            EventManager.Instance.Publish(new SleepCostEvent
            {
                Target = target,
                TurnDuration = duration,
                Source = context.Source
            });
        }

        public string GetDescription(CostInstance cost)
        {
            int duration = cost.TurnDuration > 0 ? cost.TurnDuration : cost.Value;
            return $"沉睡 {duration} 回合";
        }
    }

    /// <summary>
    /// 召唤素材代价处理器
    /// 验证素材条件，通过后将素材送入墓地
    /// </summary>
    public class SummonMaterialCostHandler : ICostHandler
    {
        public CostType CostType => CostType.SummonMaterial;

        public bool CanPay(CostInstance cost, CostContext context)
        {
            if (context.ZoneManager == null || context.Payer == null) return false;
            var battlefield = context.ZoneManager.GetCards(context.Payer, Zone.Battlefield);

            if (cost.TargetFilter != null)
            {
                var candidates = battlefield.Cast<Entity>().ToList();
                var effectCtx = new EffectExecutionContext { Controller = context.Payer, Source = context.Source };
                return cost.TargetFilter.Filter(candidates, effectCtx).Count > 0;
            }

            return battlefield.Count >= cost.Value;
        }

        public void Pay(CostInstance cost, CostContext context)
        {
            if (context.ZoneManager == null || context.Payer == null) return;

            var battlefield = context.ZoneManager.GetCards(context.Payer, Zone.Battlefield);
            List<Card> materials;

            if (cost.TargetFilter != null)
            {
                var candidates = battlefield.Cast<Entity>().ToList();
                var effectCtx = new EffectExecutionContext { Controller = context.Payer, Source = context.Source };
                var filtered = cost.TargetFilter.Filter(candidates, effectCtx);
                materials = filtered.OfType<Card>().ToList();
            }
            else
            {
                materials = battlefield.Take(cost.Value).ToList();
            }

            foreach (var mat in materials)
            {
                context.ZoneManager.MoveCard(mat, context.Payer, Zone.Battlefield, Zone.Graveyard);
            }

            EventManager.Instance.Publish(new SummonMaterialCostEvent
            {
                Player = context.Payer,
                Materials = materials,
                SummonMethod = cost.SummonMethod,
                Source = context.Source
            });
        }

        public string GetDescription(CostInstance cost)
        {
            if (cost.TargetFilter != null)
                return $"使用素材: {cost.TargetFilter.DisplayName}";
            return $"使用 {cost.Value} 个素材";
        }
    }

    /// <summary>
    /// 送墓（本组）代价处理器：将牌库顶 N 张送入墓地（代价抵消机制之一）。
    /// </summary>
    public class MillDeckCostHandler : ICostHandler
    {
        public CostType CostType => CostType.MillDeck;

        public bool CanPay(CostInstance cost, CostContext context)
        {
            if (context.ZoneManager == null || context.Payer == null) return false;
            var deck = context.ZoneManager.GetCards(context.Payer, Zone.Deck);
            return deck.Count >= cost.Value;
        }

        public void Pay(CostInstance cost, CostContext context)
        {
            var deck = context.ZoneManager.GetCards(context.Payer, Zone.Deck);
            var milled = new List<Card>();
            // 牌库顶 = index 0（与抽牌/送墓/GetTopCards 同约定，见 Zones.cs 牌库顶注释）。
            // 先快照再移动，规避 GetCards 返回活列表时边移边取的错位；曾误从列表末端（牌库底）取牌。
            for (int i = 0; i < cost.Value && i < deck.Count; i++)
            {
                milled.Add(deck[i]);
            }
            foreach (var card in milled)
            {
                context.ZoneManager.MoveCard(card, context.Payer, Zone.Deck, Zone.Graveyard);
            }

            EventManager.Instance.Publish(new MillDeckCostEvent
            {
                Player = context.Payer,
                Cards = milled,
                Source = context.Source
            });
        }

        public string GetDescription(CostInstance cost) => $"送墓（本组）{cost.Value} 张";
    }

    /// <summary>
    /// 送额外组代价处理器：将额外卡组 N 张送入墓地（代价抵消机制之一）。
    /// </summary>
    public class SendExtraDeckCostHandler : ICostHandler
    {
        public CostType CostType => CostType.SendExtraDeck;

        public bool CanPay(CostInstance cost, CostContext context)
        {
            if (context.ZoneManager == null || context.Payer == null) return false;
            var extra = context.ZoneManager.GetCards(context.Payer, Zone.ExtraDeck);
            return extra.Count >= cost.Value;
        }

        public void Pay(CostInstance cost, CostContext context)
        {
            var extra = context.ZoneManager.GetCards(context.Payer, Zone.ExtraDeck);
            var sent = new List<Card>();
            for (int i = 0; i < cost.Value && i < extra.Count; i++)
            {
                var card = extra[extra.Count - 1 - i];
                context.ZoneManager.MoveCard(card, context.Payer, Zone.ExtraDeck, Zone.Graveyard);
                sent.Add(card);
            }

            EventManager.Instance.Publish(new SendExtraDeckCostEvent
            {
                Player = context.Payer,
                Cards = sent,
                Source = context.Source
            });
        }

        public string GetDescription(CostInstance cost) => $"送 {cost.Value} 张额外组卡入墓";
    }

    // ================================================================
    // 代价相关事件
    // ================================================================

    /// <summary>送墓（本组）代价事件</summary>
    public class MillDeckCostEvent : GameEventBase
    {
        public Player Player { get; set; }
        public List<Card> Cards { get; set; }
        public Entity Source { get; set; }
    }

    /// <summary>送额外组代价事件</summary>
    public class SendExtraDeckCostEvent : GameEventBase
    {
        public Player Player { get; set; }
        public List<Card> Cards { get; set; }
        public Entity Source { get; set; }
    }

    /// <summary>弃牌代价事件</summary>
    public class CardDiscardCostEvent : GameEventBase
    {
        public Player Player { get; set; }
        public Card Card { get; set; }
        public Entity Source { get; set; }
    }

    /// <summary>生命支付代价事件</summary>
    public class LifePaymentCostEvent : GameEventBase
    {
        public Player Player { get; set; }
        public int Amount { get; set; }
        public Entity Source { get; set; }
    }

    /// <summary>沉睡代价事件</summary>
    public class SleepCostEvent : GameEventBase
    {
        public Entity Target { get; set; }
        public int TurnDuration { get; set; }
        public Entity Source { get; set; }
    }

    /// <summary>召唤素材代价事件</summary>
    public class SummonMaterialCostEvent : GameEventBase
    {
        public Player Player { get; set; }
        public List<Card> Materials { get; set; }
        public SummonMethod SummonMethod { get; set; }
        public Entity Source { get; set; }
    }

    /// <summary>
    /// 注册内置代价处理器
    /// </summary>
    /// <summary>自身紊乱代价事件（2026-09-08 代价拓展：自身减益作代价）。</summary>
    public class SelfSicknessCostEvent : GameEventBase
    {
        public Player Payer;
        public Card Target;
        public int Stacks;
        public int Turns;
        public Entity Source;
    }

    /// <summary>
    /// 自身紊乱代价处理器（2026-09-08 拓展：自身减益作代价）：支付时给源卡附加紊乱指示物
    /// Value 层（默认 1）、持续 TurnDuration 回合（0 = 按指示物默认持续到回合结束）。
    /// 构筑期当量 CardCostConfig.SelfSicknessValue（默认 1/条）。
    /// </summary>
    public class SelfSicknessCostHandler : ICostHandler
    {
        public CostType CostType => CostType.SelfSickness;

        public bool CanPay(CostInstance cost, CostContext context)
            => context != null && context.Source is Card c && c.IsAlive;

        public void Pay(CostInstance cost, CostContext context)
        {
            if (!(context.Source is Card self) || !self.IsAlive) return;
            int stacks = Math.Max(1, cost.Value);
            int turns = cost.TurnDuration > 0 ? cost.TurnDuration : -1; // -1 = 无回合时钟（按指示物注册的默认持续）
            self.AddCounters(Attribute.KeywordRules.RushSicknessCounter, stacks, turns, context.Source);
            EventManager.Instance.Publish(new SelfSicknessCostEvent
            {
                Payer = context.Payer,
                Target = self,
                Stacks = stacks,
                Turns = cost.TurnDuration,
                Source = context.Source
            });
        }

        public string GetDescription(CostInstance cost)
            => $"自身紊乱 {Math.Max(1, cost.Value)} 层（持续 {(cost.TurnDuration > 0 ? cost.TurnDuration : 1)} 回合，期间不能以玩家为目标）";
    }

    /// <summary>对手增益代价事件（2026-09-08 代价拓展：给对方增加增益作代价）。</summary>
    public class OpponentBuffCostEvent : GameEventBase
    {
        public Player Payer;
        public Card Beneficiary;
        public int Stacks;
        public Entity Source;
    }

    /// <summary>
    /// 对手增益代价处理器（2026-09-08 拓展）：支付时给对手战场首个存活生物加 Value 层
    /// 属性增加指示物（+1/+1/层，默认 1 层）。构筑期当量 CardCostConfig.OpponentBuffValue（默认 1/层）。
    /// CanPay 要求对手战场有存活生物（增益无处安放 = 代价不可支付）。
    /// </summary>
    public class OpponentBuffCostHandler : ICostHandler
    {
        public CostType CostType => CostType.OpponentBuff;

        public bool CanPay(CostInstance cost, CostContext context)
            => context != null && context.Payer != null && context.Payer.Opponent != null
               && FirstAliveEnemyCreature(context) != null;

        public void Pay(CostInstance cost, CostContext context)
        {
            var beneficiary = FirstAliveEnemyCreature(context);
            if (beneficiary == null) return;
            int stacks = Math.Max(1, cost.Value);
            Attribute.CounterRules.AddStatCounter(beneficiary, Attribute.CounterRules.PlusOneCounter, stacks, context.Source);
            EventManager.Instance.Publish(new OpponentBuffCostEvent
            {
                Payer = context.Payer,
                Beneficiary = beneficiary,
                Stacks = stacks,
                Source = context.Source
            });
        }

        private static Card FirstAliveEnemyCreature(CostContext context)
        {
            var cards = context.ZoneManager?.GetCards(context.Payer.Opponent, Zone.Battlefield);
            if (cards == null) return null;
            foreach (var c in cards)
                if (c.IsAlive) return c;
            return null;
        }

        public string GetDescription(CostInstance cost) => $"对手一个生物获得 +1/+1 ×{Math.Max(1, cost.Value)}";
    }

    /// <summary>
    /// 效果型代价处理器（2026-09-11 定案）：代价栏装载的强制原子效果（如「给对手召唤 30/30 衍生物」）。
    /// CanPay 恒真（强制效果无支付门槛）；实际执行在 CostCompensationService.PayWithCompensationAsync
    /// （付费步异步路径——ICostHandler 接口为同步，此处只做注册占位与描述，防双执行）。
    /// </summary>
    public class PayloadCostHandler : ICostHandler
    {
        public CostType CostType => CostType.Payload;

        public bool CanPay(CostInstance cost, CostContext context)
            => cost?.Payload != null;

        public void Pay(CostInstance cost, CostContext context)
        {
            // 异步执行与补偿在 CostCompensationService（付费步），此处不动
        }

        public string GetDescription(CostInstance cost)
            => cost?.Payload != null ? $"代价效果：{cost.Payload.GetDescription()}" : "代价效果";
    }

    // ================================================================
    // 代价补偿服务（2026-09-11 黑白元素经济定案）
    // ================================================================

    /// <summary>卡级代价的选择（2026-09-11 定案：代价可选——使用时多一个选择窗口）。</summary>
    public enum OptionalCostChoice
    {
        /// <summary>不付代价：原价支付元素费，代价不执行（无补偿）。</summary>
        Skip = 0,
        /// <summary>支付代价 + 减费：元素账单按当量削减（可减至 0）。</summary>
        Discount = 1,
        /// <summary>支付代价 + 得黑白：按当量获得黑/白元素（封顶地牌上限）。</summary>
        Elements = 2,
    }

    /// <summary>
    /// 代价补偿服务（2026-09-11 黑白元素经济定案）。
    /// **卡级代价可选**（使用时的选择窗口，除抉择窗口外）：
    /// 不付（原价）/ 付+减费（元素账单−当量）/ 付+得黑白（黑=己方侧、白=对方侧，封顶地牌上限）——
    /// 避免「产生了用不了，白白承受代价」。
    /// 当量=CardCost 当量表（弃1张=1费、2命=1费、沉睡1回合=1费、素材1个=1费、自紊乱1条=1费、
    /// 对手+1/+1一层=1费、送墓5张=1费、送额外3张=1费）；Payload 效果型代价按全价。
    /// 启动式/动态效果的特殊代价仍为强制支付+得黑白（发动条件，见 PayWithCompensationAsync）。
    /// </summary>
    public static class CostCompensationService
    {
        /// <summary>单条代价的当量（元素数，向下取整）；读 CardCostConfig 当量表（Payload 按全价）。0=无当量。</summary>
        public static int EquivalentValue(CostInstance cost)
        {
            if (cost == null) return 0;
            if (cost.Type == CostType.Payload)
                return CostDerivationService.PayloadUnitGrant(cost.Payload);

            var cc = ValueSystemConfigManager.Instance.GetOrCreateConfig().CardCostConfig;
            float total;
            switch (cost.Type)
            {
                case CostType.DiscardCard: total = cc.DiscardCardValue * Math.Max(1, cost.Value); break;
                case CostType.LifePayment: total = cc.LifeValuePerPoint * Math.Max(1, cost.Value); break;
                case CostType.Sleep:
                    total = cc.SleepValuePerTurn * Math.Max(1, cost.TurnDuration > 0 ? cost.TurnDuration : cost.Value);
                    break;
                case CostType.SummonMaterial: total = cc.SummonMaterialValue * Math.Max(1, cost.Value); break;
                case CostType.SelfSickness: total = cc.SelfSicknessValue * Math.Max(1, cost.Value); break;
                case CostType.OpponentBuff: total = cc.OpponentBuffValue * Math.Max(1, cost.Value); break;
                case CostType.MillDeck: total = cost.Value / 5f; break;       // 送墓 5 张=1 费当量（CostOffsetConfig 机制行同源）
                case CostType.SendExtraDeck: total = cost.Value / 3f; break;  // 送额外 3 张=1 费当量（同上）
                default: return 0; // 元素消耗本身不是「额外代价」，不当量
            }
            return (int)Math.Floor(total);
        }

        /// <summary>一组代价的当量合计。</summary>
        public static int TotalEquivalent(List<CostInstance> costs)
        {
            int total = 0;
            if (costs == null) return 0;
            foreach (var c in costs)
                if (c != null) total += EquivalentValue(c);
            return total;
        }

        /// <summary>补偿上限（生成黑白与代价抵扣**共用**，2026-09-11 定案）：当前地牌槽上限——
        /// 防第一回合重代价直接换出超大生物/巨量黑白。黑/白生成与减费两通道同源封顶。</summary>
        public static int CompensationCap(CostContext ctx)
            => ctx?.ElementPool != null && ctx.Payer != null ? Math.Max(0, ctx.ElementPool.GetLandCap(ctx.Payer)) : 0;

        /// <summary>封顶后的当量：min(合计当量, 地牌上限)——减费通道用（黑白生成按条在 IssueGrant 封顶）。</summary>
        public static int CappedTotalEquivalent(List<CostInstance> costs, CostContext ctx)
            => Math.Min(TotalEquivalent(costs), CompensationCap(ctx));

        /// <summary>普通代价的补偿颜色：对方侧代价→白，其余（己方侧）→黑。</summary>
        public static ManaType GrantColor(CostInstance cost)
            => cost.Type == CostType.OpponentBuff ? ManaType.White : ManaType.Black;

        /// <summary>
        /// 元素账单按当量减费（从最高需求色减起，可减至 0——沿用旧抵消的贪心口径；原地修改）。
        /// </summary>
        public static void ApplyDiscount(Dictionary<int, float> elementBill, int amount)
        {
            if (elementBill == null) return;
            while (amount > 0)
            {
                int bestKey = -1;
                float bestVal = 0;
                foreach (var kv in elementBill)
                {
                    if (kv.Value > bestVal) { bestVal = kv.Value; bestKey = kv.Key; }
                }
                if (bestKey < 0) break;
                int take = Math.Min(amount, (int)elementBill[bestKey]);
                elementBill[bestKey] -= take;
                if (elementBill[bestKey] <= 0) elementBill.Remove(bestKey);
                amount -= take;
            }
        }

        /// <summary>
        /// 卡级代价的可选支付流程（cast 付费步——抉择窗口之后的选择窗口）：
        /// 交互（有 UI 非 AI）弹 1-of-3；AI 防御性策略（原价付不起且减费后付得起 → 付+减费，
        /// 不主动承受牺牲）；无头/超时默认不付。forcedChoice 供验证器直测三路径。
        /// 返回 false=不可中止的异常态（Payer 缺失）。
        /// </summary>
        public static async Cysharp.Threading.Tasks.UniTask<bool> PayOptionalCardCostsAsync(
            List<CostInstance> costs, CostContext ctx, Dictionary<int, float> elementBill,
            OptionalCostChoice? forcedChoice = null)
        {
            if (costs == null || costs.Count == 0) return true;
            if (ctx?.Payer == null) return false;

            var choice = OptionalCostChoice.Skip;

            if (forcedChoice.HasValue)
            {
                choice = forcedChoice.Value;
            }
            else
            {
                // 可付性预检（不可付 → 窗口不出现，直接原价）
                bool payable = true;
                foreach (var cost in costs)
                {
                    if (cost == null || cost.Type == CostType.Payload) continue;
                    if (!CostHandlerRegistry.CanPay(cost, ctx)) { payable = false; break; }
                }

                if (payable)
                {
                    bool interactive = TargetSelectionService.Current != null && !ctx.Payer.IsAI;
                    if (interactive)
                    {
                        int eq = CappedTotalEquivalent(costs, ctx);
                        var labels = new List<string>
                        {
                            "不付代价（原价支付）",
                            eq > 0 ? $"支付代价，费用 −{eq}" : "支付代价，减费",
                            "支付代价，获得黑/白元素",
                        };
                        int idx = await TargetSelectionService.RequestOneIndexAsync(ctx.Payer, labels, "代价选择");
                        choice = idx == 1 ? OptionalCostChoice.Discount
                              : idx == 2 ? OptionalCostChoice.Elements
                              : OptionalCostChoice.Skip;
                    }
                    else if (ctx.Payer.IsAI && ctx.ElementPool != null && elementBill != null)
                    {
                        // AI 防御策略：原价付不起、减费后付得起 → 付+减费；否则不付
                        int eq = CappedTotalEquivalent(costs, ctx);
                        var afterDiscount = new Dictionary<int, float>(elementBill);
                        ApplyDiscount(afterDiscount, eq);
                        if (eq > 0
                            && !ctx.ElementPool.CanPayCost(elementBill, ctx.Payer)
                            && ctx.ElementPool.CanPayCost(afterDiscount, ctx.Payer))
                            choice = OptionalCostChoice.Discount;
                    }
                }
            }

            if (choice == OptionalCostChoice.Skip) return true;

            // 执行代价（Payload 异步执行；其余 handler 支付）
            foreach (var cost in costs)
            {
                if (cost == null) continue;
                if (cost.Type == CostType.Payload)
                    await ExecutePayloadAsync(cost, ctx);
                else
                    CostHandlerRegistry.Pay(cost, ctx);
            }

            if (choice == OptionalCostChoice.Discount)
                ApplyDiscount(elementBill, CappedTotalEquivalent(costs, ctx)); // 减费与黑白共用上限
            else
                foreach (var cost in costs)
                    if (cost != null) IssueGrant(cost, ctx);

            return true;
        }

        /// <summary>
        /// 强制支付一组代价并逐条发放补偿（启动式/动态效果路径——代价=发动条件，无选择窗口）。
        /// Payload 在此异步执行。返回 false=存在不可支付的代价（调用方应中止，未支付任何项）。
        /// </summary>
        public static async Cysharp.Threading.Tasks.UniTask<bool> PayWithCompensationAsync(
            List<CostInstance> costs, CostContext ctx)
        {
            if (costs == null || costs.Count == 0) return true;
            if (ctx?.Payer == null) return false;

            // 预检（原子：全部可付才开始支付；Payload 恒可付=强制效果）
            foreach (var cost in costs)
            {
                if (cost == null) continue;
                if (cost.Type == CostType.Payload) continue;
                if (!CostHandlerRegistry.CanPay(cost, ctx)) return false;
            }

            foreach (var cost in costs)
            {
                if (cost == null) continue;
                if (cost.Type == CostType.Payload)
                    await ExecutePayloadAsync(cost, ctx);
                else
                    CostHandlerRegistry.Pay(cost, ctx);

                IssueGrant(cost, ctx); // 补偿跟代价走：执行即发放
            }
            return true;
        }

        /// <summary>单条代价的补偿发放（每条=一次发放事件，封顶地牌上限）。</summary>
        private static void IssueGrant(CostInstance cost, CostContext ctx)
        {
            if (ctx?.Payer == null || ctx.ElementPool == null) return;

            ManaType color;
            int amount;
            if (cost.Type == CostType.Payload)
            {
                amount = CostDerivationService.PayloadUnitGrant(cost.Payload);
                color = PayloadGrantColor(cost.Payload);
            }
            else
            {
                amount = EquivalentValue(cost);
                color = GrantColor(cost);
            }
            if (amount <= 0) return;

            int cap = ctx.ElementPool.GetLandCap(ctx.Payer);
            amount = Math.Min(amount, Math.Max(0, cap));
            if (amount <= 0) return;

            ctx.ElementPool.AddMana(ctx.Payer, color, ctx.Source as Card, amount);
        }

        /// <summary>Payload 补偿颜色：极性非零按极性（有害→黑/有益→白）；零极性按域侧（对方锁→白，其余→黑）。</summary>
        public static ManaType PayloadGrantColor(AtomicEffectInstance atom)
        {
            if (atom == null) return ManaType.Black;
            if (atom.Polarity != 0f) return CostDerivationService.PolarityGrantColor(atom.Polarity);
            return CostDerivationService.SideLock(atom.TargetKinds) == 1 ? ManaType.White : ManaType.Black;
        }

        /// <summary>执行 Payload 原子：按其自身域解析目标（如「给对手召唤」→ 对方侧），无域以无目标执行。
        /// 受惠侧执行：对方域锁定的 payload 以**对手**为控制者执行——产出型原子（SummonToken 等）
        /// 落在控制者侧，给对手的代价自然落在对手战场；己方/双侧以支付者执行。</summary>
        private static async Cysharp.Threading.Tasks.UniTask ExecutePayloadAsync(CostInstance cost, CostContext ctx)
        {
            var atom = cost.Payload;
            if (atom == null) return;

            var executor = CostDerivationService.SideLock(atom.TargetKinds) == 1 && ctx.Payer.Opponent != null
                ? ctx.Payer.Opponent
                : ctx.Payer;

            var ectx = new EffectExecutionContext
            {
                Controller = executor,
                Source = ctx.Payer, // 来源=支付者角色（来源归因定案 2026-09-09）
                ZoneManager = ctx.ZoneManager,
                ElementPool = ctx.ElementPool,
                Targets = new List<Entity>(),
                Duration = DurationType.Once,          // 与 PayloadUnitGrant 计价同口径
                SummonDropZone = Zone.Battlefield,
            };

            if (atom.TargetKinds != null && atom.TargetKinds.Count > 0 && ctx.ZoneManager != null)
            {
                var resolved = EffectHandlerRegistry.ResolveCandidates(atom.TargetKinds, atom.Filter, ectx);
                if (resolved != null && resolved.Count > 0)
                    ectx.Targets = resolved;
            }

            await EffectHandlerRegistry.ExecuteEffectAsync(atom, ectx);
        }
    }

    public static class BuiltinCostHandlers
    {
        public static void RegisterAll()
        {
            CostHandlerRegistry.Register(new ElementConsumeCostHandler());
            CostHandlerRegistry.Register(new DiscardCardCostHandler());
            CostHandlerRegistry.Register(new LifePaymentCostHandler());
            CostHandlerRegistry.Register(new SleepCostHandler());
            CostHandlerRegistry.Register(new SummonMaterialCostHandler());
            CostHandlerRegistry.Register(new MillDeckCostHandler());
            CostHandlerRegistry.Register(new SendExtraDeckCostHandler());
            CostHandlerRegistry.Register(new SelfSicknessCostHandler());
            CostHandlerRegistry.Register(new OpponentBuffCostHandler());
            CostHandlerRegistry.Register(new PayloadCostHandler());
        }
    }
}
