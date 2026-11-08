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

    /// <summary>牺牲（2026-11-03 改版，0费，edict 形态）：作用对象=**角色**（表 filter "Player"、域=己方单位）——
    /// 目标玩家（持有者）**自行选择** {value} 个己方场上单位（生物+结界+**地牌**——2026-11-03 地牌入池扩定，
    /// 旧「地牌非单位不入池」口径作废）**规则送墓**。裁定：**非效果死亡**——不经 DeathRules、不发
    /// CardDestroyEvent（OnDeath/亡语/消灭替代/击杀统计全不触发），不灭/复生无从拦截（非死亡路径天然
    /// 绕过）；地牌先出池（余量写回卡——摧毁/抹除同口）再入墓；送墓归属=卡牌持有者。
    /// 每送墓一个单位，该持有者临时钱包 +1 任意元素（AddMana 官方口，任一回合结束清空）。
    /// 候选不足 {value} 个时全量送墓；空场空转（不空发整卡、不入账）。
    /// 同步路径（触发式/headless）：自动选候选前 N 个（TargetSelectionService 代替选取同口径）。</summary>
    public class SacrificeHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.Sacrifice;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            int count = Wanted(effect, context);
            foreach (var target in context.Targets)
            {
                if (!(target is Player holder)) continue; // 作用对象=角色
                var pool = SacrificePool(holder, context);
                if (pool.Count == 0) continue; // 空场空转
                GrantAny(holder, SendAll(pool.Take(count), context), context);
            }
        }

        public override async UniTask ExecuteAsync(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            int count = Wanted(effect, context);
            foreach (var target in context.Targets)
            {
                if (!(target is Player holder)) continue; // 作用对象=角色
                var pool = SacrificePool(holder, context);
                if (pool.Count == 0) continue; // 空场空转

                List<Card> picks;
                if (pool.Count <= count)
                {
                    picks = pool; // 无选择余地：全量送墓（ShouldAutoTake「操作越省越好」同口径）
                }
                else if (holder.IsAI || TargetSelectionService.Current == null)
                {
                    picks = pool.Take(count).ToList(); // AI/无头：自动取前 N
                }
                else
                {
                    var chosen = await TargetSelectionService.RequestAsync(new TargetSelectionRequest
                    {
                        Candidates = new List<Entity>(pool),
                        MinCount = count,
                        MaxCount = count,
                        Chooser = holder, // 持有者自行选择（选择权在目标方——域收窄己方后恒自选）
                        Title = $"{holder.Name}：选择 {count} 个单位送入墓地（每个 +1 任意元素）",
                        AllowCancel = false,
                        PromptSource = (Entity)context.CastCard ?? context.Source,
                    });
                    picks = chosen?.OfType<Card>().ToList();
                    if (picks == null || picks.Count == 0)
                        picks = pool.Take(count).ToList(); // 超时兜底双保险（Service 已代选）
                }

                GrantAny(holder, SendAll(picks, context), context);
            }
        }

        /// <summary>送墓数量 {value}（缺省/≤0 钳 1）。</summary>
        private static int Wanted(AtomicEffectInstance effect, EffectExecutionContext context)
            => Math.Max(1, context.GetValueAfterModifiers(effect.Value > 0 ? effect.Value : 1));

        private static int SendAll(IEnumerable<Card> picks, EffectExecutionContext context)
        {
            int sent = 0;
            foreach (var card in picks)
            {
                SendToGrave(card, context);
                sent++;
            }
            return sent;
        }

        /// <summary>每送墓一个单位 +1 **临时任意元素**（万能货币落临时钱包，任一回合结束清空；
        /// 支付沿各色链尾垫付）。AddMana 官方口（ElementPoolGainEvent 可观测），入账方=交出单位的一方。</summary>
        private static void GrantAny(Player holder, int sent, EffectExecutionContext context)
        {
            if (sent <= 0 || context.ElementPool == null) return;
            var from = context.Source as Card ?? context.CastCard as Card;
            context.ElementPool.AddMana(holder, ManaType.Any, from, sent);
        }

        /// <summary>规则送墓（非效果死亡）：地牌先出池 + 落死档 + 按持有者归属入墓——不发
        /// CardDestroyEvent（死亡时点/消灭替代/统计不触发）；换区清理（指示物/变形解除/
        /// 临时关键词）由 ZoneContainer.OnCardMoved 统一承担，与死亡落葬同款。</summary>
        private static void SendToGrave(Card card, EffectExecutionContext context)
        {
            var owner = card.GetOwner() ?? card.GetController();
            if (owner == null) return;
            var from = card.GetZone();
            if (from == Zone.ElementPool)
                context.ElementPool?.RemoveCardFromPool(card, owner); // 地牌：余量写回卡再入墓（摧毁同口）
            card.IsAlive = false;
            if (context.ZoneManager != null && from != Zone.Graveyard)
                context.ZoneManager.MoveCard(card, owner, from, Zone.Graveyard);
        }

        /// <summary>牺牲候选 = 持有者场上单位全集（2026-11-03 扩定）：战场生物+结界（2026-10-09
        /// 摒弃并池承袭，含衍生物——召唤定案下衍生物=真实生物卡的 CardWrapper 实例）+ **元素池地牌**。
        /// 序=战场序→地牌序（AutoSelect 取前 N 的确定性来源）。空集跳过该目标（不空发整卡、不入账）。</summary>
        private static List<Card> SacrificePool(Player holder, EffectExecutionContext context)
        {
            var result = new List<Card>();
            if (holder == null || context.ZoneManager == null) return result;
            foreach (var c in context.ZoneManager.GetCards(holder, Zone.Battlefield))
                if (c != null && c.IsAlive)
                    result.Add(c);
            foreach (var c in context.ZoneManager.GetCards(holder, Zone.ElementPool))
                if (c != null && c.IsAlive)
                    result.Add(c);
            return result;
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect)
            => $"自行选择 {Math.Max(1, effect.Value > 0 ? effect.Value : 1)} 个场上单位（含地牌）规则送墓；每送墓一个单位，获得一点临时任意元素（非效果死亡，不触发死亡时点）";
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
                        effect.AnchorTint(), context.IsCostPayload))
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

    // 全体消灭 DevourAllEnemiesHandler 已随 2026-11-05 硬切 B2 删除（用户定案：删除全体原子——
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
                    EffectTint = effect.AnchorTint(),
                    FromZone = card.GetZone(), // 2026-11-04 日志口径：移区前定格（地牌=ElementPool/结界=Battlefield）
                    IsCost = context.IsCostPayload,
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
    ///（棋盘位/UI 扇面同步），CardMoveEvent To=None 作「消失」信号。
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
    /// 发动无效（2026-10-04 两层无效定案；E1-1 2026-11-04 定案·**响应绑定**）：无效化**发动**本身——净零成本。
    /// 无目标选择器——读 context.RespondedEntry（本条目入栈时绑定的当时栈顶）：**跟谁发动就反制谁**。
    /// RespondedEntry 为空 / 系统条目（SBA/阶段窗/攻击·守卫宣言）/ 已标 IsActivationNegated / 场上强制桶
    ///（Mandatory，cast 条目豁免——整卡施放永远是玩家行为）→ no-op。
    /// cast 条目由消费点 ResolveCardCastAsync 早退收口（不付费+入墓+Failed）；非 cast 条目由 EffectExecutor
    /// 跳过（主动不扣费、触发式退还发动次数）。NegateEvent.Target=条目源卡。
    /// </summary>
    public class NegateActivationHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.NegateActivation;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            var entry = context.RespondedEntry;
            if (entry == null || entry.IsSBA || entry.IsPhaseWindow
                || entry.IsAttackDeclaration || entry.IsGuardDeclaration) return; // 无响应对象/系统条目
            if (entry.IsActivationNegated) return;                                // 不可双重无效
            if (!entry.IsCardCast && entry.ActivationType == EffectActivationType.Mandatory) return; // 强制桶不受管制
            entry.IsActivationNegated = true;
            PublishEvent(new NegateEvent { Target = entry.Source as Card, IsActivation = true, Source = context.Source });
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect) => "响应连锁：无效化其发动的发动（不扣费、不计发动次数；强制效果不受管制）";
    }

    /// <summary>
    /// 效果无效（2026-10-04 两层无效定案；E1-1 2026-11-04 定案·**响应绑定**）：发动照常、效果不结算。
    /// 与发动无效同构读 context.RespondedEntry——差别仅：**强制桶同样受管制**（可标记）；
    /// cast 条目消费点付费后入墓、费用不退。RespondedEntry 为空/系统条目/已标 → no-op。
    /// </summary>
    public class NegateEffectHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.NegateEffect;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            var entry = context.RespondedEntry;
            if (entry == null || entry.IsSBA || entry.IsPhaseWindow
                || entry.IsAttackDeclaration || entry.IsGuardDeclaration) return;
            if (entry.IsActivationNegated) return; // 发动无效后不可再被效果无效指
            entry.IsEffectNegated = true;
            PublishEvent(new NegateEvent { Target = entry.Source as Card, IsActivation = true, Source = context.Source });
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect) => "响应连锁：无效化其发动的效果（扣费照付，效果不结算；强制效果同受管制）";
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
                    Source = context.Source,
                    EffectTint = effect.AnchorTint() // 赋予粒子（2026-11-01）
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
            // 2026-11-03 收口 RevealRules.Reveal 单源（补齐 RevealCardsEvent——动画/统计消费口同口径）。
            foreach (var card in shown)
                RevealRules.Reveal(card, context.Source);

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

    // 由单体效果选全域生成；枚举墓碑保留）。

    // ---------------- 参照族（2026-10-31；2026-11-04 双段目标改版：第一段弹选{target}=己方生物参照
    //（表域=己方单位+NoRole 滤角色——角色不作参照，防以角色生命 30 为基准刷甲），第二段=结算期
    // 反问落点（活体炸弹问受害者=对方单位含角色、活体护甲问护甲落点=己方单位含角色——角色可
    // 受甲）。2026-11-03「单目标+硬编码最高参照」与 str="SelfUnit" 档作废；护盾猛击无参照段照旧） ----------------

    /// <summary>
    /// 活体炸弹（参照族·2026-11-08 步3 换轨）：**选择全部在引擎 spec 计划**——参照=context.Refs["ref"]
    ///（旧直调路径回落 Targets 首位）、受害者=context.Targets 首位（行 Domain=受体域，引擎在进 handler 前
    /// 解析——候选>1 弹选/AI 代选/无头取首）。本 handler 只读结果：对受害者造成等同参照当前攻击力的
    /// **效果伤害**（GetPower 活读含光环成分；非战斗伤害——护甲/坚韧/圣盾吸收、易损放大）。
    /// 无己方生物/参照攻≤0/受害者域空=空转。伤害产出族：LastOutcome.RecordDamage 记账。
    /// </summary>
    public class BurstDamageHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.BurstDamage;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            // 同步路径：受体=Targets 首位（引擎侧已解析；无头直调兜底同口径）
            Resolve(effect, context, context.Targets.FirstOrDefault());
        }

        public override UniTask ExecuteAsync(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            Resolve(effect, context, context.Targets.FirstOrDefault());
            return UniTask.CompletedTask;
        }

        /// <summary>落账：参照亡/攻≤0/受害者无效=空转；效果伤害+AtomicDamageEvent（伤害产出族记账）。
        /// 数值=ResolveValue（步6：valueExpr=ref.power 活读参照攻；空=旧 GetRolledValue 名义值路径）。</summary>
        private void Resolve(AtomicEffectInstance effect, EffectExecutionContext context, Entity victim)
        {
            var reference = context.Ref ?? context.PrimaryTarget;
            if (reference == null || !reference.IsAlive || victim == null || !victim.IsAlive) return;

            int dmg = Math.Max(0, context.ResolveValue(effect));
            if (dmg <= 0) return;

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

        protected override string DescribeTemplate(AtomicEffectInstance effect) =>
            "以己方生物为参照，对一个对方单位造成等同其攻击力的伤害";
    }

    /// <summary>
    /// 活体护甲（参照族·2026-11-08 步3 换轨）：参照=context.Refs["ref"]（旧直调路径回落 Targets 首位）、
    /// 落点=context.Targets 首位（行 Domain=受体域，引擎侧解析——候选&gt;1 弹选/AI 代选/无头取首；
    /// 角色可受甲）。本 handler 只读结果：落点获得等同参照当前生命的护甲（ArmorCounter 层数——
    /// 每层吸收 1 点伤害、生效自减；Entity 级角色可持有）。无己方生物/参照生命≤0/落点域空=空转。
    /// </summary>
    public class LivingArmorHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.LivingArmor;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            Resolve(effect, context, context.Targets.FirstOrDefault());
        }

        public override UniTask ExecuteAsync(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            Resolve(effect, context, context.Targets.FirstOrDefault());
            return UniTask.CompletedTask;
        }

        /// <summary>落账：参照亡/生命≤0/落点无效=空转；护甲层数=ResolveValue（步6：valueExpr=ref.life
        /// 活读参照命；空=旧 GetRolledValue 名义值路径）；AddCounters 实际增量出账。</summary>
        private void Resolve(AtomicEffectInstance effect, EffectExecutionContext context, Entity receiver)
        {
            var reference = context.Ref ?? context.PrimaryTarget;
            if (reference == null || !reference.IsAlive || receiver == null || !receiver.IsAlive) return;

            int stacks = Math.Max(0, context.ResolveValue(effect));
            if (stacks <= 0) return;

            int actualDelta = receiver.AddCounters(KeywordRules.ArmorCounter, stacks, context.Source);
            PublishEvent(new CounterChangedEvent
            {
                Target = receiver,
                CounterType = KeywordRules.ArmorCounter,
                Amount = actualDelta,
                Source = context.Source,
                EffectTint = effect.AnchorTint(), // 赋予粒子（2026-11-01）
            });
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect) =>
            "以己方生物为参照，一个己方单位获得等同其生命的护甲";
    }

    /// <summary>
    /// 护盾猛击（2026-11-02 参照族；2026-11-03 单目标改版，红3 表行 247c2ae1）：移除**己方角色**
    ///（硬编码=效果控制者）全部护甲（ArmorCounter 账面层数；负量移除不受绝缘体拦截——
    /// 2026-10-31 特殊裁定口径），对弹选的{target}（表域=对方单位，含对方角色）造成等同移除
    /// 层数的**效果伤害**（非战斗伤害——TakeDamage 关键词管线照常：护甲/坚韧/圣盾吸收）。
    /// 角色无护甲或目标无效=空转（不移除不出伤）。
    /// 伤害产出族：LastOutcome.RecordDamage 记账——「消灭目标时」产出条件可挂。
    /// </summary>
    public class ShieldSlamHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.ShieldSlam;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            Resolve(effect, context, context.Targets.FirstOrDefault());
        }

        public override UniTask ExecuteAsync(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            Resolve(effect, context, context.Targets.FirstOrDefault());
            return UniTask.CompletedTask;
        }

        /// <summary>落账（2026-11-08 参照化）：参照=context.Refs["ref"]（行 RefDomain=己方单位+RefFilter
        /// Armored=具有护甲——引擎 spec 计划解析，旧直调路径回落 Targets 首位）；移除参照全部护甲
        ///（CounterChangedEvent 出账），对受害者（Targets 首位=行 Domain 受体）造成**移除护甲值等量**
        /// 效果伤害。参照亡/无甲/受害者无效=空转（RefRequired 同口径）。</summary>
        private void Resolve(AtomicEffectInstance effect, EffectExecutionContext context, Entity victim)
        {
            var reference = context.Ref ?? context.PrimaryTarget;
            if (reference == null || !reference.IsAlive || victim == null || !victim.IsAlive) return;

            int armor = reference.GetCounterCount(KeywordRules.ArmorCounter);
            if (armor <= 0) return; // 无护甲=空转（不出伤）

            int removed = reference.AddCounters(KeywordRules.ArmorCounter, -armor, context.Source);
            if (removed != 0)
                PublishEvent(new CounterChangedEvent
                {
                    Target = reference,
                    CounterType = KeywordRules.ArmorCounter,
                    Amount = removed,
                    Source = context.Source, // EffectTint 恒 null：移除非赋予（粒子判据=赋予原子才填）
                });

            int dmg = Math.Max(0, -removed);
            if (dmg <= 0) return;
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

        protected override string DescribeTemplate(AtomicEffectInstance effect) =>
            "移除{reference}全部护甲，对{target}造成移除护甲值等量的伤害";
    }

    /// <summary>
    /// 封印（2026-11-03 新增，蓝3 表行 c336be8f·一般效果表）：弹选的{target}（表域=对方单位+
    /// NoRole+Landable——敌方场上生物且带元素费用）离场，作为地牌加入**目标持有侧**（=对手）
    /// 的地牌池。锁色不指定=缺省主色（份额最多色=「最高费用」色，GetLandColorChoices 降序首位，
    /// 开对应色地牌格——与成长拉牌同口径）。AddCardToPool 权威闸照走：对手上限满/对手禁地令/
    /// 资格拒=空转不动盘面（表域 Landable 已滤 0费/衍生物，闸为兜底）。非死亡离场：不发摧毁/
    /// 死亡事件，指示物随换区清除；入池未横置（对手下回合周期即可产色）。
    /// </summary>
    public class SealUnitHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.SealUnit;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            var pool = context.ElementPool ?? GameCore.Instance?.ElementPool;
            var zm = context.ZoneManager;
            if (pool == null || zm == null) return;

            foreach (var target in context.Targets)
            {
                if (!(target is Card unit) || !unit.IsAlive) continue;
                if (unit.GetZone() != Zone.Battlefield) continue; // 表域兜底：只封场上单位

                // 入池方=目标持有侧（控制者优先——被偷单位封进当前控制者池=「对手的地牌池」）
                var owner = unit.GetController() ?? unit.GetOwner();
                if (owner == null) continue;

                if (!pool.AddCardToPool(unit, owner)) // 权威闸：缺省锁色=主色（最高费用色）
                {
                    TideLog.Warn($"[SealUnit] {unit.ID} 入池被拒（0费/资格/禁地令/上限满）——封印空转");
                    continue;
                }

                zm.MoveCard(unit, owner, Zone.Battlefield, Zone.ElementPool);
                pool.PublishPoolAdd(unit, owner);
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect)
            => "将{target}封印为对手的地牌，按其最高费用放入对应地牌格（不能对0费单位使用）";
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

                // 参照族（2026-10-31）：迸发=参照攻击力打随机对手；活体护甲=参照生命给己方角色；
                // 护盾猛击（2026-11-02）=移除参照全部护甲、反问受害者等量伤害
                new BurstDamageHandler(),
                new LivingArmorHandler(),
                new ShieldSlamHandler(),

                // 封印（2026-11-03）：对方场上单位封为对手的地牌（按最高费用锁色开格）
                new SealUnitHandler(),
            };
        }
    }
}

