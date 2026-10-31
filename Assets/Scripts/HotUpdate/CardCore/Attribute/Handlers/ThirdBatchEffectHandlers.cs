using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using CardCore.Attribute;

namespace CardCore.Attribute.Handlers
{
    // ================================================================
    // 第三批 handler（2026-09-03 原子表整体修正后存留）
    // 已删除：PutOnBottomOfDeck/MoveCard/MoveToAnyZone/DrawThenDiscard/SearchAndReveal/
    //         SearchAndPlay/RevealCards/AddMana/ConsumeMana/DamageBasedOnStat/DestroyRandom/
    //         CopyExact/ExchangePosition（表行随枚举一并移除）
    // NegateEffect → Silence（沉默指示物：持有者不可发动主动效果）
    // MillCard 改名"送墓"（表 EnumName/DisplayName 已改，EffectType 不变）
    // ================================================================

    // ---------------- 牌库 ----------------

    /// <summary>送墓（原名"磨牌"；牌库顶 N 张 → 坟墓场）。牌库归属按目标域解析（2026-09-14 代价原子化）：
    /// 域锁对方（{8}）磨对手牌库（干扰向），其余（含默认双域 {7,8}）磨自己牌库——
    /// 磨**自己**=「送墓」资源代价语义（代价栏 Payload 锁 {7}，付费步执行+错边补偿黑），
    /// 批量发 MillDeckCostEvent（MatchStats 照常——归土改墓地复活后墓量仍是对局量）；
    /// 磨对手只发逐张 CardMillEvent。</summary>
    public class MillCardHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.MillCard;

        public override bool CanExecute(AtomicEffectInstance effect, EffectExecutionContext context) => context != null;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            int count = context.GetValueAfterModifiers(effect.Value);
            if (context.ZoneManager == null || context.Controller == null) return;

            bool ownDeck = CostDerivationService.SideLock(effect.TargetKinds) != 1; // 对方锁→磨对手；其余→自己
            var owner = ownDeck ? context.Controller
                : (context.Controller.Opponent ?? context.Controller);

            var top = context.ZoneManager.GetTopCards(owner, count);
            var container = context.ZoneManager.GetZoneContainer(owner);
            foreach (var card in top)
            {
                container.Move(card, Zone.Deck, Zone.Graveyard);
                card.SetZone(Zone.Graveyard);
                PublishEvent(new CardMillEvent { Player = owner, Card = card, Source = context.Source });
            }

            if (ownDeck && top.Count > 0)
                PublishEvent(new MillDeckCostEvent { Player = owner, Cards = top.ToList(), Source = context.Source });
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect) => $"送墓：牌库顶 {effect.Value} 张入墓地";
    }

    // ScryCardsHandler 已删（2026-10-08）：占卜/刺探合并后唯一活跃路径=
    // LookAtTopCardsHandler（对手侧=实例域锁 {8}）——枚举位亦已随 2026-10-30 枚举收拢删除。

    /// <summary>改写持有者（2026-09-13 定案升级，黑2 锚 ×3.0）：永久换手 + owner 改写——
    /// 经 HandlerHelpers.ChangeControl(permanent:true) 迁场换控并改写 owner；
    /// 此后弹回/洗回回新主的卡组手牌、死亡去新主墓地（GainControl+Permanent 同语义，构筑显式可挂）。</summary>
    public class ChangeOwnerHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.ChangeOwner;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            if (context.Controller == null) return;
            foreach (var target in context.Targets)
                if (target is Card card)
                    HandlerHelpers.ChangeControl(context, card, context.Controller, permanent: true);
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect) => "改写目标的持有者（含控制权；弹回/洗回/死亡均归新主）";
    }

    // ---------------- 死亡原子（消灭 / 湮灭；牺牲 2026-10-09 转直送非死亡） ----------------

    /// <summary>牺牲（2026-10-09 舍弃并档定案，黑5，edict 形态）：作用对象=**角色**（表 filter "Player"）——
    /// 目标玩家（持有者）**自行选择**一个己方战场单位（生物+无生命单位，摒弃并池）**直送墓地**。
    /// 裁定：**非效果死亡**——不经 DeathRules、不发 CardDestroyEvent（OnDeath/亡语/消灭替代/击杀统计
    /// 全不触发），不灭/复生无从拦截（非死亡路径天然绕过）；送墓归属=卡牌持有者。
    /// 同步路径（触发式/headless）：自动选持有者战场首个单位（TargetSelectionService 代替选取同口径）。</summary>
    public class SacrificeHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.Sacrifice;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            foreach (var target in context.Targets)
            {
                if (!(target is Player holder)) continue; // 作用对象=角色
                var board = BoardUnits(holder, context);
                if (board.Count == 0) continue; // 空场空转
                SendToGrave(board[0], context);
            }
        }

        public override async UniTask ExecuteAsync(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            foreach (var target in context.Targets)
            {
                if (!(target is Player holder)) continue; // 作用对象=角色
                var board = BoardUnits(holder, context);
                if (board.Count == 0) continue; // 空场空转

                Card chosen = board[0];
                if (board.Count > 1)
                {
                    var picked = await TargetSelectionService.RequestAsync(new TargetSelectionRequest
                    {
                        Candidates = new List<Entity>(board),
                        MinCount = 1,
                        MaxCount = 1,
                        Chooser = holder, // 持有者自行选择（对手的牺牲对手挑）
                        Title = $"{holder.Name}：选择一个单位送入墓地",
                    });
                    if (picked != null && picked.Count > 0 && picked[0] is Card pc) chosen = pc;
                }

                SendToGrave(chosen, context);
            }
        }

        /// <summary>直送墓地（非死亡）：仅落死档 + 按持有者归属入墓——不发 CardDestroyEvent
        /// （死亡时点/消灭替代/统计不触发）；换区清理（指示物/守护断链/变形解除/临时关键词）
        /// 由 ZoneContainer.OnCardMoved 统一承担，与死亡落葬同款。</summary>
        private static void SendToGrave(Card card, EffectExecutionContext context)
        {
            var owner = card.GetOwner() ?? card.GetController();
            if (owner == null) return;
            card.IsAlive = false;
            if (context.ZoneManager != null)
            {
                var from = card.GetZone();
                if (from != Zone.Graveyard)
                    context.ZoneManager.MoveCard(card, owner, from, Zone.Graveyard);
            }
        }

        /// <summary>牺牲候选 = 持有者战场全部存活单位（生物+无生命单位——2026-10-09 摒弃并池；
        /// 元素池地牌非"单位"不入池）。含衍生物（召唤定案下衍生物=真实生物卡的 CardWrapper 实例）。
        /// 空场跳过该目标（外层 continue，不空发整卡）。</summary>
        private static List<Card> BoardUnits(Player holder, EffectExecutionContext context)
        {
            var result = new List<Card>();
            if (holder == null || context.ZoneManager == null) return result;
            foreach (var c in context.ZoneManager.GetCards(holder, Zone.Battlefield))
                if (c != null && c.IsAlive)
                    result.Add(c);
            return result;
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect) => "以角色为目标，使其自行选择一个单位效果送墓（非效果死亡，不触发死亡时点）";
    }

    /// <summary>消灭（2026-10-09 吞噬机制移除定案：原吞噬行改名，EffectType 沿用 Devour 枚举位）——
    /// 纯消灭：效果死亡（DestroyEffect 死因），无回复无吸收。不灭拦（消灭类）、复生可替代
    ///（表 filter NoRole 在目标层滤角色——2026-10-13 神佑删除后角色不被效果处决全靠此层）；
    /// 来源=效果来源。</summary>
    public class DevourHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.Devour;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            foreach (var target in context.Targets)
            {
                if (!(target is Card victim)) continue;
                // 发动表演照播（2026-10-20 用户定案）：不灭等拦截=效果对目标不适用——效果已发动
                //（费已付），补飞行事件驱动弹体+到达表现（无死亡事件，表现层仅飞行不结算）。
                if (!DeathRules.TryKill(victim, DeathCause.DestroyEffect, context.Source, context.ZoneManager,
                        effect.AnchorTint()))
                    PublishEvent(new EffectFlightEvent
                    {
                        Source = context.Source, CastCard = context.CastCard, Target = victim,
                        Tint = effect.AnchorTint(),
                        Note = "目标不灭——效果不适用"
                    });
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect) => "消灭目标生物（效果死亡；不灭可拦、复生可替代）";
    }

    /// <summary>全体消灭（2026-10-29 阶段3a，红12·无目标行）：消灭对方全体生物——逐个效果死亡
    ///（DestroyEffect 死因，与消灭同口径：不灭拦/复生可替代由 TryKill 决策表承担）。
    /// 无域原子（表行 TargetKinds 空）——handler 自结算：遍历对手战场存活的生物（Cardtype.Creature），
    /// 单次执行、无目标解析。</summary>
    public class DevourAllEnemiesHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.DevourAllEnemies;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            var opp = context.Controller?.Opponent;
            if (opp == null || context.ZoneManager == null) return;

            foreach (var card in context.ZoneManager.GetCards(opp, Zone.Battlefield))
            {
                if (card == null || !card.IsAlive) continue;
                if (!(card is IHasSupertype ht) || ht.Supertype != Cardtype.Creature) continue;
                DeathRules.TryKill(card, DeathCause.DestroyEffect, context.Source, context.ZoneManager);
            }
        }
    }

    // 湮灭处理器 2026-10-13 删除（表行已删、处理器不可达）；DeathCause.Annihilate 保留
    //（引擎层死因仍活——复活=重写表行+本处理器）；同名原子枚举位已随 2026-10-30 枚举收拢删除。

    /// <summary>
    /// 摧毁（黑5）：与"消灭"的区别——作用于**无生命值单位**（地牌/结界）。
    /// 地牌 = 双方元素池中的卡（Zone.ElementPool）：先出池（余量写回卡，不设耗尽标记——
    /// 摧毁≠资源枯竭，回收后可再作地牌），再直送拥有者墓地；结界 = 战场非生物持久物。
    /// 不经死亡决策表（无生命值者无"死亡"），发 CardDestroyEvent（Reason=Smashed）。
    /// 不灭免疫（2026-10-09 裁定：剧毒/消灭/摧毁三口全可被不灭拦）——显式走护盾矩阵消灭类
    /// 判定（IsShielded/DestroyEffect），拦下即整跳（不出池不入墓不发事件）。
    /// </summary>
    public class SmashHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.Smash;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            foreach (var target in context.Targets)
            {
                if (!(target is Card card)) continue;

                // 不灭免疫（2026-10-09 三口同拦裁定）：消灭类护盾判定，拦下即整跳
                if (DeathRules.IsShielded(card, DeathCause.DestroyEffect)) continue;

                // 出池：从双方元素池移除（余量写回卡上，由拥有者的池持有）
                if (context.ElementPool != null)
                {
                    var owner = card.GetOwner() ?? card.GetController();
                    if (owner == null) continue;
                    context.ElementPool.RemoveCardFromPool(card, owner);
                }

                // 事件序定案（2026-10-20 用户：先死亡后移墓）：摧毁事件先发（此刻卡仍在场），
                // 移区随后发离场——旧序（先移后发）日志离场先于死亡。
                PublishEvent(new CardDestroyEvent
                {
                    DestroyedCard = card,
                    Reason = DestroyReason.Smashed,
                    Source = context.Source,
                    EffectTint = effect.AnchorTint()
                });

                // 直送墓地（无生命值单位不走 DeathRules）
                var cardOwner = card.GetOwner() ?? card.GetController();
                if (context.ZoneManager != null && cardOwner != null)
                {
                    var from = card.GetZone();
                    if (from != Zone.Graveyard)
                        context.ZoneManager.MoveCard(card, cardOwner, from, Zone.Graveyard);
                }
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect) => "摧毁目标（无生命值单位：地牌/结界）";
    }

    /// <summary>
    /// 抹除（白5，2026-10-10）：将目标（场上生物/结界/地牌——表域六类 + NotRole 滤角色）
    /// 从本局游戏中除去——同遗忘指示物到期移除口径（CounterRules.OnTurnEnd ② 例程）：
    /// **不走换区、直接消失**（不进墓/不进除外区），不发 CardDestroyEvent（死亡时点/
    /// 亡语/击杀统计全不触发），不灭/复生无从拦截（非死亡路径天然绕过）。
    /// 地牌先出池（余量写回卡，同摧毁口）再消失；战场单位容器直删后补发离场事件
    ///（守护断链/棋盘位/UI 扇面同步），CardMoveEvent To=None 作「消失」信号。
    /// </summary>
    public class EraseHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.Erase;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            foreach (var target in context.Targets)
            {
                if (!(target is Card card)) continue;
                var owner = card.GetOwner() ?? card.GetController();
                if (owner == null) continue;
                var from = card.GetZone();

                // 地牌：先出池（余量写回卡、清池内记录——区域移动由下方直删完成）
                if (from == Zone.ElementPool)
                    context.ElementPool?.RemoveCardFromPool(card, owner);

                // 直接消失：容器删除（无目标区）；战场单位落死档（牺牲非死亡直送同款）
                context.ZoneManager?.GetZoneContainer(owner)?.Remove(card, from);
                if (from == Zone.Battlefield)
                {
                    card.IsAlive = false;
                    PublishEvent(new CardLeaveBattlefieldEvent
                    {
                        Card = card,
                        Controller = owner,
                        Destination = Zone.None,
                    });
                }
                PublishEvent(new CardMoveEvent
                {
                    MovedCard = card,
                    From = from,
                    To = Zone.None,
                    Controller = owner,
                });
                context.LastOutcome.AffectedTargets.Add(card);
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect) =>
            "将目标从本局游戏中除去（不走换区直接消失；不触发死亡时点，不灭无从拦截）";
    }

    // ---------------- 反制 / 沉默 ----------------

    /// <summary>
    /// 发动无效（2026-10-04 两层无效定案；原打落 KnockDown 承接）：无效化**发动**本身——净零成本。
    /// - 施放（发动区卡）：直接送墓 → cast 在消费点因「已不在发动区」中止：不付费、不结算（净效果=扣费返还）；
    /// - 场上发动（主动/触发式）：标记该源卡待结算栈条目「发动层无效」——结算时跳过：
    ///   主动不扣费（横置为声明期代价，不重置）、触发式退还发动次数（不计入）；
    /// - 强制桶（Mandatory，如光环类强制效果）不受发动无效管制：不标记、无效果。
    /// </summary>
    public class NegateActivationHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.NegateActivation;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            var stack = GameCore.Instance?.StackEngine;
            foreach (var target in context.Targets)
            {
                if (!(target is Card card)) continue;
                var owner = card.GetController() ?? context.Controller;

                // 施放路径：发动区卡送墓（cast 消费点中止——不付费不结算）
                if (context.ZoneManager.IsCardInZone(card, owner, Zone.Activation))
                {
                    context.ZoneManager.MoveCard(card, owner, Zone.Activation, Zone.Graveyard);
                    PublishEvent(new CardLeaveActivationEvent
                    {
                        Card = card,
                        Controller = owner,
                        ToZone = Zone.Graveyard
                    });
                    continue;
                }

                // 场上发动路径：标记该源卡**最晚入栈**的非强制条目（强制桶不受发动无效管制）
                var entry = stack?.GetStackContents()
                    .LastOrDefault(e => e != null && !e.IsCardCast && !e.IsSBA
                                        && !e.IsAttackDeclaration && !e.IsGuardDeclaration
                                        && ReferenceEquals(e.Source, card)
                                        && e.ActivationType != EffectActivationType.Mandatory);
                if (entry != null)
                {
                    entry.IsActivationNegated = true;
                    PublishEvent(new NegateEvent { Target = card, IsActivation = true, Source = context.Source });
                }
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect) => "无效化目标的发动（不扣费、不计发动次数；强制效果不受管制）";
    }

    /// <summary>
    /// 效果无效（2026-10-04 两层无效定案；承接旧 NegateActivation 标记实现）：发动照常、效果不结算。
    /// - 施放：照常扣费（不返还）、卡入墓、跳过效果（消费点 _isNegated 路径）；
    /// - 场上发动（主动/触发式）：标记栈条目「效果层无效」——扣费照走、横置不重置、触发式计入发动次数；
    /// - 强制桶（光环类强制效果）**同样受管制**：可标记、效果跳过。
    /// </summary>
    public class NegateEffectHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.NegateEffect;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            var stack = GameCore.Instance?.StackEngine;
            foreach (var target in context.Targets)
            {
                if (!(target is Card card)) continue;
                var owner = card.GetController() ?? context.Controller;

                // 施放路径：标记 _isNegated（ResolveCardCastAsync 步3：付费后入墓、费用不退）
                if (context.ZoneManager.IsCardInZone(card, owner, Zone.Activation))
                {
                    card.Negate();
                    PublishEvent(new NegateEvent { Target = card, IsActivation = true, Source = context.Source });
                    continue;
                }

                // 场上发动路径：标记该源卡最晚入栈的待结算条目（含强制桶——受效果无效管制）
                var entry = stack?.GetStackContents()
                    .LastOrDefault(e => e != null && !e.IsCardCast && !e.IsSBA
                                        && !e.IsAttackDeclaration && !e.IsGuardDeclaration
                                        && ReferenceEquals(e.Source, card));
                if (entry != null)
                {
                    entry.IsEffectNegated = true;
                    PublishEvent(new NegateEvent { Target = card, IsActivation = true, Source = context.Source });
                }
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect) => "无效化目标的效果（扣费照付，效果不结算；强制效果同受管制）";
    }

    /// <summary>
    /// 沉默指示物（定案，原"无效效果"改名改语义）：对目标附加 {value} 层沉默指示物——
    /// 持有者不可发动主动效果（主动能力，挂 EffectExecutionEngine.CanActivate 第 0 步）；
    /// 换区清除（表 Tags 2026-10-31 改衰退型）。
    /// </summary>
    public class SilenceHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.Silence;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            // 2026-10-31 晚定案：一次发动按 value 给层（≤0/未填=1）
            int amount = CounterRules.ResolveLayers(CounterRules.SilenceCounter, context.GetValueAfterModifiers(effect.Value));
            foreach (var target in context.Targets)
            {
                if (target == null || !target.IsAlive) continue;
                int actualDelta = target.AddCounters(CounterRules.SilenceCounter, amount, context.Source);
                PublishEvent(new CounterChangedEvent
                {
                    Target = target,
                    CounterType = CounterRules.SilenceCounter,
                    Amount = actualDelta,
                    Source = context.Source
                });
                PublishEvent(new KeywordAppliedEvent
                {
                    Target = target,
                    Keyword = "沉默",
                    Detail = "沉默：持有者不可发动主动效果",
                    Source = context.Source
                });
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect) => "附加沉默指示物（不可发动主动效果）";
    }

    /// <summary>
    /// 发现（2026-09-11，蓝2）：从牌库中随机展示 {value} 张牌（默认 3），从中选一张加入手牌。
    /// 炉石发现池=全收藏（卡池可枚举），本项目卡数量难以估计——收窄为牌库内随机三选一（受控检索的随机版）。
    /// 未选中的牌留在牌库原位（只展示不抽动顺序）；牌库不足时全展示；空牌库静默无效果。
    /// 结算期选择走 TargetSelectionService（AI/无头自动选首张，同排列交互先例）。
    /// </summary>
    public class DiscoverCardHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.DiscoverCard;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            // 选择需等待 UI——走 ExecuteAsync（EffectHandlerRegistry.ExecuteEffectAsync 统一入口）
        }

        public override async UniTask ExecuteAsync(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            // 禁寻令（2026-10-29 1b）：受光环影响的一方不能检索牌库（发现=检索族同门）
            if (RuleAuraSystem.ScopeHits(RuleAuraComponents.NoSearchAura, context.Controller)) return;

            var controller = context.Controller;
            var zm = context.ZoneManager;
            if (controller == null || zm == null) return;

            int showCount = context.GetValueAfterModifiers(effect.Value > 0 ? effect.Value : 3);

            var deck = zm.GetCards(controller, Zone.Deck);
            if (deck == null || deck.Count == 0) return;

            // 随机取 N 张（不放回抽样；不改牌库顺序——未选留在原位）
            // 2026-09-13 收编 GameRng（种子可复播——网络对拍/回放确定性的前提，弃 UnityEngine.Random）
            var pool = new List<Card>(deck);
            var shown = new List<Card>();
            while (shown.Count < showCount && pool.Count > 0)
            {
                int i = GameRng.Next(0, pool.Count);
                shown.Add(pool[i]);
                pool.RemoveAt(i);
            }

            // 2026-10-09 展示链路定案：展示即状态——被展示牌挂 Exposed（双方可查看，换区清除）：
            // 未选中留库=不换区保持展示；选中入手=deck→hand 换区即消（ZoneContainer 换区清统一承担）。
            foreach (var card in shown)
            {
                if (card.GetCounterCount(CounterRules.ExposedCounter) > 0) continue; // 二值不叠层
                int actualDelta = card.AddCounters(CounterRules.ExposedCounter, 1, context.Source);
                PublishEvent(new CounterChangedEvent
                {
                    Target = card,
                    CounterType = CounterRules.ExposedCounter,
                    Amount = actualDelta,
                    Source = context.Source,
                });
            }

            List<Entity> chosen;
            if (shown.Count <= 1)
            {
                chosen = new List<Entity>(shown);
            }
            else
            {
                chosen = await TargetSelectionService.RequestAsync(new TargetSelectionRequest
                {
                    Candidates = shown.Cast<Entity>().ToList(),
                    MinCount = 1,
                    MaxCount = 1,
                    Chooser = controller,
                    Title = "发现",
                    Hint = $"从展示的 {shown.Count} 张牌中选择一张加入手牌",
                    AllowCancel = false,
                });
            }
            if (chosen == null || chosen.Count == 0) return;

            if (chosen[0] is Card pick)
            {
                zm.MoveCard(pick, controller, Zone.Deck, Zone.Hand);
                PublishEvent(new CardEnterHandEvent
                {
                    Player = controller,
                    Card = pick,
                    FromZone = Zone.Deck,
                    IsDraw = false,
                });
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect) =>
            $"发现：从牌库随机展示 {Math.Max(1, effect.Value)} 张，选一张加入手牌";
    }

    // ---------------- 参照族（2026-10-31：以己方单位为参照读攻/血——参照目标是弹选的表域目标，
    // 产出作用面由 handler 自定：迸发打随机对手、活体护甲给己方角色） ----------------

    /// <summary>
    /// 迸发（2026-10-31，红3 表行 6a17079d）：以{target}（己方单位）为参照——对**随机**对手单位
    ///（对方存活生物+对方角色，GameRng 单次抽取——随机绕过潜行，战斗法师散射同口径）
    /// 造成等同参照单位当前攻击力的**效果伤害**（GetPower 活读含光环成分；非战斗伤害——
    /// TakeDamage 关键词管线照常：护甲/坚韧/圣盾吸收、易损放大）。受害者域空或攻击力≤0=空转。
    /// 伤害产出族：LastOutcome.RecordDamage 记账——「消灭目标时」产出条件可挂。
    /// </summary>
    public class BurstDamageHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.BurstDamage;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            var reference = context.PrimaryTarget;
            if (reference == null || !reference.IsAlive) return;

            int dmg = Math.Max(0, reference.GetPower());
            if (dmg <= 0) return;

            var foe = context.Controller?.Opponent;
            var pool = new List<Entity>();
            if (foe != null)
            {
                if (context.ZoneManager != null)
                    foreach (var c in context.ZoneManager.GetCards(foe, Zone.Battlefield))
                        if (c != null && c.IsAlive && !c.IsNonLivingUnit()) pool.Add(c); // 域=对方单位（有生命）
                if (foe.IsAlive) pool.Add(foe); // 角色=有生命单位（世界观定案）
            }
            if (pool.Count == 0) return;

            var victim = pool[GameRng.Next(0, pool.Count)];
            int lifeBefore = victim.GetLife();
            victim.TakeDamage(dmg, context.Source);
            int actual = Math.Max(0, lifeBefore - victim.GetLife());
            context.LastOutcome.RecordDamage(victim, lifeBefore, actual);
            PublishEvent(new AtomicDamageEvent
            {
                Source = context.Source,
                Target = victim,
                Damage = dmg,
                IsCombatDamage = false,
                EffectTint = effect.AnchorTint()
            });
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect)
            => "以己方单位为参照，对随机对手单位造成等同其攻击力的伤害";
    }

    /// <summary>
    /// 活体护甲（2026-10-31，绿3 表行 508a4975）：以{target}（己方单位）为参照——**己方角色**
    ///（=效果控制者）获得等同参照单位当前生命的护甲（ArmorCounter 层数，GetLife 活读含光环成分
    ///——每层吸收 1 点伤害、生效自减；Entity 级角色可持有，后手护甲补偿同款指示物）。
    /// 参照生命≤0=空转。参照目标选的是单位本体，护甲落点恒为角色（表域承担"以己方单位为目标"）。
    /// </summary>
    public class LivingArmorHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.LivingArmor;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            var reference = context.PrimaryTarget;
            var hero = context.Controller;
            if (reference == null || hero == null) return;

            int stacks = Math.Max(0, reference.GetLife());
            if (stacks <= 0) return;

            int actualDelta = hero.AddCounters(KeywordRules.ArmorCounter, stacks, context.Source);
            PublishEvent(new CounterChangedEvent
            {
                Target = hero,
                CounterType = KeywordRules.ArmorCounter,
                Amount = actualDelta,
                Source = context.Source,
            });
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect)
            => "以己方单位为参照，己方角色获得等同其生命的护甲";
    }

    /// <summary>第三批 handler 工厂</summary>
    /// <summary>
    /// 移动（2026-10-31 场面轴3c，灰2 表行 c69e2b96·位置族）：将{target}移动到空格。
    /// 表域=己方单位（结算期反问选目标——敌方单位不可选由表域承担）；落格合法性/执行=
    /// GameBoard.MoveUnitRules 单源（己方单位区空格·非原地·PinCard+Resync——邻接（碾压）/
    /// 箭头连接光环/攻击距离按新格即时派生）。选格经 CellSelectionService 反问桥由 UI 弹格
    /// （与召唤生物选格同交互）。AI/无头/联机（无桥或 Chooser.IsAI）：安全空转+Warn——
    /// 发动代价照旧由 ActivateEffect 结算（横置/耐久/费用），移动本身不收额外资源；
    /// LegalActionEnumerator 本阶段不枚举本原子（防 AI 白付代价）。
    /// </summary>
    public class MoveUnitHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.MoveUnit;

        /// <summary>同步路径（引擎非交互结算/兜底）：无选格交互——安全空转。</summary>
        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            TideLog.Warn("[MoveUnit] 无选格交互（AI/无头/联机）——移动空转");
        }

        public override async UniTask ExecuteAsync(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            var chooser = context.Controller;
            foreach (var target in context.Targets)
            {
                if (!(target is Card unit) || !unit.IsAlive) continue;
                // 非己方单位拒绝（表域外兜底——直构上下文/AI 代选错侧时在此拦）
                var owner = unit.GetController() ?? unit.GetOwner();
                if (owner == null || !ReferenceEquals(owner, chooser)) continue;

                var cells = GameBoard.MoveUnitRules.LegalDestinations(unit);
                if (cells.Count == 0)
                {
                    TideLog.Warn($"[MoveUnit] {unit.ID} 无合法空格（单位区满/全占）——空转");
                    continue;
                }

                var ask = CellSelectionService.Current;
                if (ask == null || chooser == null || chooser.IsAI)
                {
                    TideLog.Warn("[MoveUnit] 无选格交互（AI/无头/联机）——移动空转");
                    continue;
                }

                int pick = await ask(unit, cells, $"移动 {EffectText.Name(unit)}：选择一个空格");
                if (pick < 0 || pick >= cells.Count) continue; // 取消/放弃：不动盘面
                if (!GameBoard.MoveUnitRules.TryMove(unit, cells[pick].x, cells[pick].z)) continue;

                PublishEvent(new KeywordAppliedEvent
                {
                    Target = unit,
                    Keyword = "移动",
                    Detail = $"移动：{EffectText.Name(unit)} → 空格 ({cells[pick].x},{cells[pick].z})",
                    Source = context.CastCard ?? context.Source,
                });
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect) => "将目标移动到己方单位区的空格";
    }

    public static class ThirdBatchHandlerFactory
    {
        public static IAtomicEffectHandler[] CreateAll()
        {
            return new IAtomicEffectHandler[]
            {
                // 牌库
                new MillCardHandler(),
                new ChangeOwnerHandler(),
                new DiscoverCardHandler(),

                // 死亡原子（牺牲=直送非死亡；Devour=消灭（2026-10-09 吞噬机制移除））
                // AnnihilateHandler 已删（2026-10-13 湮灭残壳清理——表行已删不可达）
                new SacrificeHandler(),
                new DevourHandler(),

                // 摧毁（无生命值单位：地牌/结界）
                new SmashHandler(),

                // 抹除（从本局游戏中除去——非死亡直接消失，同遗忘到期移除口径）
                new EraseHandler(),

                // 反制 / 沉默（2026-10-04 两层无效定案：发动无效=净零成本 / 效果无效=扣费照付）
                new NegateActivationHandler(),
                new NegateEffectHandler(),
                new SilenceHandler(),

                // 移动（2026-10-31 场面轴3c）：将目标移动到空格（结算期选格反问）
                new MoveUnitHandler(),

                // 参照族（2026-10-31）：迸发=参照攻击力打随机对手；活体护甲=参照生命给己方角色
                new BurstDamageHandler(),
                new LivingArmorHandler(),
            };
        }
    }
}
