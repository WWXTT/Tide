using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using CardCore.Attribute;

namespace CardCore.Attribute.Handlers
{
    // ================================================================
    // 第一批基础效果 — 补齐的 handler（与 BuiltinEffectHandlers 配套）
    // 复用 EntityEffectExtensions / ZoneManagerExtensions 原语
    // ================================================================

    // ---------------- 伤害类 ----------------

    /// <summary>
    /// 穿透伤害（定案，原"不可防止伤害"改名）：越过关键词和指示物计算伤害——
    /// 跳过圣盾/护甲指示物/坚韧，但受光环限制（替代引擎/层效果照走），事件链/吸血照常。
    /// </summary>
    public class PierceDamageHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.PierceDamage;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            foreach (var target in context.Targets)
            {
                // 2026-09-13 数值随机：每目标独立掷（掷值在修饰链前）
                int dmg = context.GetValueAfterModifiers(effect.GetRolledValue());
                int lifeBefore = target.GetLife();
                int actual = KeywordRules.ApplyDamage(context.Source, target, dmg, false, pierce: true);
                context.LastOutcome.RecordDamage(target, lifeBefore, actual);
                PublishEvent(new AtomicDamageEvent
                {
                    Source = context.Source,
                    Target = target,
                    Damage = actual,
                    IsCombatDamage = false,
                    DamageType = DamageType.Normal
                });
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect) => $"造成 {effect.Value} 点穿透伤害（无视关键词与指示物）";
    }

    /// <summary>吸取生命（对目标造成伤害，控制者回复等量生命）</summary>
    public class DrainLifeHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.DrainLife;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            int drained = 0;
            foreach (var target in context.Targets)
            {
                // 2026-09-13 数值随机：每目标独立掷（吸取量按各目标实扣累计）
                int dmg = context.GetValueAfterModifiers(effect.GetRolledValue());
                int lifeBefore = target.GetLife();
                target.TakeDamage(dmg, context.Source); // 关键词管线
                int actual = System.Math.Max(0, lifeBefore - target.GetLife());
                drained += actual;
                context.LastOutcome.RecordDamage(target, lifeBefore, actual);
                PublishEvent(new AtomicDamageEvent
                {
                    Source = context.Source,
                    Target = target,
                    Damage = dmg,
                    IsCombatDamage = false,
                    DamageType = DamageType.LifeLoss
                });
            }

            if (context.Controller != null && drained > 0)
            {
                int lifeBefore = context.Controller.GetLife();
                context.Controller.Heal(drained);
                int overfill = System.Math.Max(0, drained - (context.Controller.GetLife() - lifeBefore));
                PublishEvent(new HealEvent { Target = context.Controller, Amount = drained, Overfill = overfill, Source = context.Source });
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect) => $"吸取 {effect.Value} 点生命";
    }

    // PoisonHandler（剧毒指示物原子）已删（2026-10-08 剧毒转关键词 GrantVenom·落定追加式消灭）——
    // 表行 20c06d52 改挂 GrantVenom，回合末死亡裁决随 CounterRules.PoisonCounter 一并退役。

    // ---------------- 卡牌移动 / 牌库操作 ----------------

    /// <summary>
    /// 弃牌：被弃方从手牌中自选弃掉 {value} 张牌（P1 启发式代选：AI=按价值升序弃最差）。
    /// 被弃方判定（2026-09-14 代价原子化）：目标中的 Player > 目标手牌卡的控制者 > 控制者的对手——
    /// 双域 {5,6} 效果栏用法=指向谁弃谁（原「恒对手」口径废除，与域模型对齐）；
    /// 代价栏 Payload 锁己方 {5}（解析出己方手牌卡）→ 弃自己 = 资源支付语义。
    /// </summary>
    public class DiscardCardHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.DiscardCard;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            int count = context.GetValueAfterModifiers(effect.Value);
            if (context.ZoneManager == null || context.Controller == null) return;

            var victim = context.Targets.OfType<Player>().FirstOrDefault()
                ?? context.Targets.OfType<Card>().Select(c => c.GetController()).FirstOrDefault(c => c != null)
                ?? context.Controller.Opponent;
            if (victim == null) return;

            var hand = context.ZoneManager.GetCards(victim, Zone.Hand).ToList();
            // 自选启发式（代弃方视角）：价值升序弃最差——费用低→攻击低 优先
            var picks = hand.OrderBy(c => c.GetCost()).ThenBy(c => c.GetPower()).Take(count);
            foreach (var card in picks)
            {
                context.ZoneManager.GetZoneContainer(victim).Move(card, Zone.Hand, Zone.Graveyard);
                PublishEvent(new CardDiscardEvent { Player = victim, Card = card, Source = context.Source });
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect) => $"对手从手牌中自选弃掉 {effect.Value} 张牌";
    }

    /// <summary>除外（将目标移入流放区）</summary>
    public class ExileHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.Exile;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            foreach (var target in context.Targets)
            {
                if (target is Card card)
                {
                    var fromZone = card.GetZone();
                    var controller = card.GetController();
                    if (context.ZoneManager != null && controller != null)
                        context.ZoneManager.GetZoneContainer(controller).Move(card, fromZone, Zone.Exile);
                    PublishEvent(new CardExileEvent { Card = card, Source = context.Source, FromZone = fromZone });
                }
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect) => "将目标除外";
    }

    /// <summary>洗入牌库（将目标随机洗回拥有者牌库）</summary>
    public class ShuffleIntoDeckHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.ShuffleIntoDeck;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            foreach (var target in context.Targets)
            {
                if (target is Card card)
                {
                    var owner = card.GetOwner() ?? card.GetController();
                    var fromZone = card.GetZone();
                    if (context.ZoneManager != null && owner != null)
                    {
                        context.ZoneManager.GetZoneContainer(owner).Move(card, fromZone, Zone.Deck);
                        context.ZoneManager.ShuffleDeck(owner);
                    }
                    PublishEvent(new CardShuffleIntoDeckEvent { Card = card, Source = context.Source });
                }
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect) => "将目标洗入牌库";
    }

    /// <summary>
    /// 检索牌库（定案改语义）：宣言一个卡名（StringValue 构筑期预置），从牌库检索对应的卡入手。
    /// 命中 = 卡名或卡 ID 匹配宣言文本；未命中 = 空手而归（宣言分支可挂 DeclareHit/Miss——
    /// 命中写 LastOutcome.DeclareHit 供紧邻分支判定）。
    /// </summary>
    public class SearchDeckHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.SearchDeck;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            if (context.ZoneManager == null || context.Controller == null) return;

            string declared = effect.StringValue;
            if (string.IsNullOrEmpty(declared)) return; // 未预置宣言名：不检索

            var deck = context.ZoneManager.GetCards(context.Controller, Zone.Deck).ToList();
            var found = deck.FirstOrDefault(c => MatchesDeclaration(c, declared));
            if (found == null)
            {
                context.LastOutcome.DeclareHit = false;
                context.LastOutcome.Declaration = declared;
                return;
            }

            context.ZoneManager.MoveCard(found, context.Controller, Zone.Deck, Zone.Hand);
            PublishEvent(new RevealCardsEvent
            {
                Player = context.Controller,
                Cards = new List<Card> { found },
                Source = context.Source
            });
            context.LastOutcome.DeclareHit = true;
            context.LastOutcome.Declaration = declared;
        }

        /// <summary>宣言匹配：卡名（CardData.CardName）或卡 ID 等值（构筑期预置的宣言文本）。</summary>
        private static bool MatchesDeclaration(Card card, string declared)
        {
            if (card.ID == declared) return true;
            if (card is CardWrapper wrapper && wrapper.GetData()?.CardName == declared) return true;
            return false;
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect) => $"宣言「{effect.StringValue}」并从牌库检索对应的卡";
    }

    /// <summary>弹回牌库顶</summary>
    public class BounceToTopHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.BounceToTop;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            foreach (var target in context.Targets)
            {
                if (target is Card card)
                {
                    var owner = card.GetOwner() ?? card.GetController();
                    var fromZone = card.GetZone();
                    if (context.ZoneManager != null && owner != null)
                        context.ZoneManager.GetZoneContainer(owner).Move(card, fromZone, Zone.Deck, DeckPosition.Top);
                    PublishEvent(new CardReturnToHandEvent { Card = card, Source = context.Source });
                }
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect) => "将目标放回牌库顶";
    }

    /// <summary>弹回牌库底</summary>
    public class BounceToBottomHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.BounceToBottom;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            foreach (var target in context.Targets)
            {
                if (target is Card card)
                {
                    var owner = card.GetOwner() ?? card.GetController();
                    var fromZone = card.GetZone();
                    if (context.ZoneManager != null && owner != null)
                        context.ZoneManager.GetZoneContainer(owner).Move(card, fromZone, Zone.Deck, DeckPosition.Bottom);
                    PublishEvent(new CardReturnToHandEvent { Card = card, Source = context.Source });
                }
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect) => "将目标放回牌库底";
    }

    /// <summary>苏生（原名"墓地返回"，2026-09-03 改名）：将目标从坟墓场放回战场</summary>
    public class ReturnFromGraveyardHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.ReturnFromGraveyard;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            if (context.ZoneManager == null || context.Controller == null) return;

            int count = effect.Value > 0 ? context.GetValueAfterModifiers(effect.Value) : 1;
            // 正式召唤闸已拆（2026-10-10：游戏王额外区适配遗物——额外区全面移除后死因不再区分）：
            // 墓地生物一律可回场（打出/弃置/送墓/衍生物同规）；类型闸=生物（表行「{target}生物从墓地
            // 返回到己方战场」——墓地混有法术/结界，不可裸取前 N 张）。
            var revivable = context.ZoneManager.GetCards(context.Controller, Zone.Graveyard)
                .Where(c => c is IHasSupertype ht && ht.Supertype == Cardtype.Creature)
                .ToList();
            for (int i = 0; i < count && i < revivable.Count; i++)
            {
                var card = revivable[i];
                // 经入场容量闸门：满则失败——卡留在墓地（来源即墓地，无移动），发失败事件
                if (!context.ZoneManager.TryMoveToBattlefield(card, context.Controller, Zone.Graveyard))
                    continue;
                card.SetController(context.Controller);
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect) => "从墓地返回到战场";
    }

    /// <summary>墓地回收到手牌（将控制者墓地前 N 张放回手牌；费用锚点：回收 1 张 = 1 费）。</summary>
    public class RecoverToHandHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.RecoverToHand;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            if (context.ZoneManager == null || context.Controller == null) return;

            int count = effect.Value > 0 ? context.GetValueAfterModifiers(effect.Value) : 1;
            // 优先回收已选目标卡；无目标时取控制者墓地前 N 张（快照避免迭代中移动）
            var targeted = context.Targets?.OfType<Card>().ToList();
            List<Card> toRecover = (targeted != null && targeted.Count > 0)
                ? targeted.Take(count).ToList()
                : context.ZoneManager.GetCards(context.Controller, Zone.Graveyard).Take(count).ToList();

            foreach (var card in toRecover)
            {
                context.ZoneManager.MoveCard(card, context.Controller, Zone.Graveyard, Zone.Hand);
                PublishEvent(new CardReturnToHandEvent { Card = card, Source = context.Source });
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect) => "从墓地回收到手牌";
    }

    /// <summary>
    /// 占卜（2026-09-11 排列实现；2026-10-08 并入刺探为单行）：查看牌库顶 {value} 张并任意排列——
    /// 域锁对方(8)看对手牌库，其余（7/双域默认）看自己（排列者恒为发动方）。
    /// 主路径 ExecuteAsync：排列交互逐张单选（先选的在最顶），整段写回新顶序；
    /// AI/无头/超时自动取剩余首张 = 维持原序（训练确定性）。
    /// 同步 Execute 仅查看播报（旧同步/触发路径兼容，不弹排列）。
    /// </summary>
    public class LookAtTopCardsHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.LookAtTopCards;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            var owner = DeckArrangeHelper.ResolveDeckOwner(effect, context);
            var top = DeckArrangeHelper.PeekTop(effect, context, owner);
            if (top.Count > 0)
                PublishEvent(new ScryEvent { Player = owner, Cards = top, Source = context.Source });
        }

        public override async UniTask ExecuteAsync(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            var owner = DeckArrangeHelper.ResolveDeckOwner(effect, context);
            var top = DeckArrangeHelper.PeekTop(effect, context, owner);
            if (top.Count == 0) return;

            var final = top;
            if (top.Count > 1)
            {
                var ordered = await DeckArrangeHelper.ArrangeAsync(top, context.Controller,
                    enemyDeck: owner != context.Controller);
                if (ordered != null && ordered.Count == top.Count)
                {
                    context.ZoneManager.GetZoneContainer(owner).ReorderTop(Zone.Deck, ordered);
                    final = ordered;
                }
            }
            PublishEvent(new ScryEvent { Player = owner, Cards = final, Source = context.Source });
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect) => $"占卜：查看牌库顶 {effect.Value} 张并任意排列（域锁对方则看对手）";
    }

    // 展示手牌原子已删除（2026-09-03 原子表整体修正）——
    // 手牌可见性收敛为宣言确认手牌的单张验证（ProphecyHandlers.DeclareHandHandler，按张发 RevealHandEvent）。

    // ---------------- 状态变更 ----------------

    /// <summary>
    /// 修改生命值（2026-10-08 来源分轨退役）：经 StatGrantRouter 设置轨直写——Card 走
    /// ApplyStatDelta 字段直写（减=削上限、有效生命归零标死交 SBA）、Player 走
    /// IncreaseMaxHealth/扣血+LifeChangeEvent。永久、跨区保留、净化不清；
    /// 临时层（换区清）用 AddLifeUp/AddLifeDown。
    /// </summary>
    public class ModifyLifeHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.ModifyLife;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            int amount = context.GetValueAfterModifiers(effect.Value);
            foreach (var target in context.Targets)
            {
                if (target == null || !target.IsAlive) continue;
                int oldLife = target.GetLife();
                StatGrantRouter.ModifyLife(target, amount, context.Source);
                PublishEvent(new StatModifyEvent
                {
                    Target = target,
                    StatType = StatType.Life,
                    OldValue = oldLife,
                    NewValue = target.GetLife(),
                    Delta = amount,
                    Source = context.Source
                });
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect)
        {
            string sign = effect.Value >= 0 ? "+" : "";
            return $"生命值 {sign}{effect.Value}";
        }
    }

    /// <summary>设置攻击力</summary>
    // ==================== 设置系（2026-09-09 三轨制重建复活；2026-10-08 来源分轨退役改口径） ====================
    // Set 族是**显式设置原子**：天然=设置类（不参与来源路由，任何来源都直改）；
    // 与 Modify 族（StatGrantRouter 设置轨直写）同语义——跨区保留、净化不清（视同本体）。
    // 临时层（换区清）一律由指示物原子族（AddPlusOne/AddLifeUp 等）显式表达，与来源无关。
    public class SetPowerHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.SetPower;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            int value = context.GetValueAfterModifiers(effect.Value);
            foreach (var target in context.Targets)
            {
                int oldPower = target.GetPower();
                target.SetPower(value);
                PublishEvent(new StatSetEvent
                {
                    Target = target,
                    StatType = StatType.Power,
                    OldValue = oldPower,
                    NewValue = value,
                    Source = context.Source
                });
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect) => $"将攻击力设为 {effect.Value}";
    }

    /// <summary>设置生命值</summary>
    public class SetLifeHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.SetLife;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            int value = context.GetValueAfterModifiers(effect.Value);
            foreach (var target in context.Targets)
            {
                int oldLife = target.GetLife();
                if (target is Card card)
                {
                    card.SetLife(value);
                    if (value > card._maxLife) card._maxLife = value;
                    // 归零标死交 SBA——死亡来源=设置施加方（对齐 LifeDown 减益致死归因口径）
                    if (card._life <= 0)
                    {
                        card._pendingDeathSource = context.Source;
                        card.IsAlive = false;
                    }
                }
                else if (target is Player player)
                {
                    player.Life = value; // 角色=生物单位世界观：设置生命直接改当前生命
                }
                PublishEvent(new StatSetEvent
                {
                    Target = target,
                    StatType = StatType.Life,
                    OldValue = oldLife,
                    NewValue = target.GetLife(),
                    Source = context.Source
                });
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect) => $"将生命值设为 {effect.Value}";
    }

    /// <summary>
    /// 设置费用（新增原子）：直接改卡的基本费用（_baseCost），持续到游戏结束——
    /// 与设置攻击力/生命值同族（直改基本属性、跨区保留），区别于费用指示物（仅手牌、离手消失）。
    /// 目标=手牌卡。
    /// </summary>
    public class SetCostHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.SetCost;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            int value = context.GetValueAfterModifiers(effect.Value);
            foreach (var target in context.Targets)
            {
                if (!(target is Card card)) continue;
                int oldCost = target.GetCost();
                target.SetCost(value);
                PublishEvent(new CostModifyEvent
                {
                    Target = target,
                    OldCost = oldCost,
                    NewCost = target.GetCost(),
                    Delta = value - oldCost,
                    Source = context.Source
                });
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect) => $"将费用设为 {effect.Value}";
    }

    /// <summary>
    /// 修改费用（2026-10-08 来源分轨退役）：经 StatGrantRouter 直改 _baseCost（跨区保留，
    /// 与「设置费用」同口径）；仅手牌生效的临时轨用 AddCostUp/AddCostDown 指示物族。
    /// 目标门禁=手牌（表过滤承担）。
    /// </summary>
    public class ModifyCostHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.ModifyCost;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            int amount = context.GetValueAfterModifiers(effect.Value);
            foreach (var target in context.Targets)
            {
                if (!(target is Card card) || card.GetZone() != Zone.Hand) continue; // 费用目标门禁=手牌
                int oldCost = target.GetCost();
                StatGrantRouter.ModifyCost(card, amount, context.Source);
                PublishEvent(new CostModifyEvent
                {
                    Target = target,
                    OldCost = oldCost,
                    NewCost = target.GetCost(),
                    Delta = amount,
                    Source = context.Source
                });
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect)
        {
            string sign = effect.Value >= 0 ? "+" : "";
            return $"费用 {sign}{effect.Value}";
        }
    }
}
