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
    /// 批量发 MillDeckCostEvent（MatchStats/归土进度照常）；磨对手只发逐张 CardMillEvent。</summary>
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

    // ScryCardsHandler 已删（2026-10-08 无墓碑清理）：占卜/刺探合并后唯一活跃路径=
    // LookAtTopCardsHandler（对手侧=实例域锁 {8}），本 handler 无表行无调用者——
    // 枚举槽位保序保留（见 AtomicEffects.cs）。

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

    // ---------------- 死亡原子（牺牲 / 吞噬 / 湮灭） ----------------

    /// <summary>牺牲（2026-09-13 定案，黑2，edict 形态）：作用对象=**双方角色**（表 filter "Player" 仅角色）——
    /// 目标玩家（持有者）**自行选择**一个己方生物效果死亡；死亡来源=持有者（其控制者，非施法者——
    /// 己方/敌方击杀触发分流正确）；不灭不拦牺牲（DeathRules 只拦{消灭,吞噬}）。
    /// 同步路径（触发式/headless）：自动选持有者战场首个生物（TargetSelectionService 代替选取同口径）。</summary>
    public class SacrificeHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.Sacrifice;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            foreach (var target in context.Targets)
                SacrificeOne(target, context);
        }

        public override async UniTask ExecuteAsync(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            foreach (var target in context.Targets)
            {
                if (!(target is Player holder)) continue; // 作用对象=角色
                var board = BoardCreatures(holder, context);
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
                        Title = $"{holder.Name}：选择一个生物牺牲",
                    });
                    if (picked != null && picked.Count > 0 && picked[0] is Card pc) chosen = pc;
                }

                DeathRules.TryKill(chosen, DeathCause.Sacrifice, holder, context.ZoneManager);
            }
        }

        /// <summary>同步结算（触发式/headless）：持有者战场首个生物效果死亡。</summary>
        private static void SacrificeOne(Entity target, EffectExecutionContext context)
        {
            if (!(target is Player holder)) return;
            var board = BoardCreatures(holder, context);
            if (board.Count == 0) return; // 空场空转
            DeathRules.TryKill(board[0], DeathCause.Sacrifice, holder, context.ZoneManager);
        }

        private static List<Card> BoardCreatures(Player holder, EffectExecutionContext context)
        {
            if (holder == null || context.ZoneManager == null) return new List<Card>();
            // 牺牲=生物（含衍生物——召唤定案下衍生物=真实生物卡的 CardWrapper 实例，同过此滤）
            return context.ZoneManager.GetCards(holder, Zone.Battlefield)
                .Where(c => c.IsAlive && c is IHasSupertype st && st.Supertype == Cardtype.Creature)
                .ToList();
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect) => "持有者选择一个己方生物牺牲（效果死亡，来源为持有者，无视不灭）";
    }

    /// <summary>摒弃（2026-09-13，黑2，edict 原子——牺牲的无生命等价）：作用对象=双方角色（filter "Player"）——
    /// 持有者自行选择一个己方场上**无生命单位**（结界等非生物持久物）直送墓地（DestroyReason.Abandoned）。
    /// 与牺牲同款：交互 Chooser=持有者（headless/同步自动选首个）、豁免帷幕（选择权在目标方）。</summary>
    public class AbandonHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.Abandon;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            foreach (var target in context.Targets)
                AbandonOne(target, context);
        }

        public override async UniTask ExecuteAsync(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            foreach (var target in context.Targets)
            {
                if (!(target is Player holder)) continue; // 作用对象=角色
                var board = BoardNonLiving(holder, context);
                if (board.Count == 0) continue; // 无无生命单位空转

                Card chosen = board[0];
                if (board.Count > 1)
                {
                    var picked = await TargetSelectionService.RequestAsync(new TargetSelectionRequest
                    {
                        Candidates = new List<Entity>(board),
                        MinCount = 1,
                        MaxCount = 1,
                        Chooser = holder, // 持有者自行选择
                        Title = $"{holder.Name}：选择一个场上无生命单位摒弃",
                    });
                    if (picked != null && picked.Count > 0 && picked[0] is Card pc) chosen = pc;
                }

                AbandonCard(chosen, holder, context);
            }
        }

        /// <summary>同步结算（触发式/headless）：持有者场上首个无生命单位摒弃。</summary>
        private void AbandonOne(Entity target, EffectExecutionContext context)
        {
            if (!(target is Player holder)) return;
            var board = BoardNonLiving(holder, context);
            if (board.Count == 0) return;
            AbandonCard(board[0], holder, context);
        }

        /// <summary>直送墓地 + 摒弃播报（无生命单位不走 DeathRules，同 Smash 直毁路径惯例；
        /// 地牌来源先出池再移动——与 Smash 同款）。</summary>
        private void AbandonCard(Card card, Player holder, EffectExecutionContext context)
        {
            var owner = card.GetOwner() ?? card.GetController();
            if (owner == null) return;

            // 地牌（元素池来源）：先出池（余量写回卡上，由拥有者的池持有），再入墓
            if (context.ElementPool != null && card.GetZone() == Zone.ElementPool)
                context.ElementPool.RemoveCardFromPool(card, owner);

            if (context.ZoneManager != null)
            {
                var from = card.GetZone();
                if (from != Zone.Graveyard)
                    context.ZoneManager.MoveCard(card, owner, from, Zone.Graveyard);
            }
            PublishEvent(new CardDestroyEvent
            {
                DestroyedCard = card,
                Reason = DestroyReason.Abandoned,
            });
        }

        /// <summary>摒弃候选（2026-09-13 定案：与摧毁 Smash 同覆盖）= 己方场上无生命单位（结界等）
        /// + 己方元素池地牌。对手对应区域无卡 → 跳过该目标（外层 continue，不空发整卡）。</summary>
        private static List<Card> BoardNonLiving(Player holder, EffectExecutionContext context)
        {
            var result = new List<Card>();
            if (holder == null || context.ZoneManager == null) return result;
            foreach (var c in context.ZoneManager.GetCards(holder, Zone.Battlefield))
                if (c.IsAlive && c is IHasSupertype st && st.Supertype != Cardtype.Creature)
                    result.Add(c);
            // 地牌：元素池区的卡（持有者自选一张摒弃——出池入墓）
            foreach (var c in context.ZoneManager.GetCards(holder, Zone.ElementPool))
                if (c != null) result.Add(c);
            return result;
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect) => "持有者选择一个己方场上无生命单位摒弃（直送墓地）";
    }

    /// <summary>吞噬（2026-09-13 简化定案）：只是**消灭** + 吞噬者按被消灭单位的**最大生命值**恢复生命——
    /// 删除关键词吸收与按当前生命回复。消灭裁决先行（不灭/复生替代拦下即无回复，DeathRules 定案不变）。</summary>
    public class DevourHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.Devour;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            var devourer = context.Source as Card;
            foreach (var target in context.Targets)
            {
                if (!(target is Card victim) || victim == devourer) continue;

                int maxLife = victim.GetMaxLife(); // 消灭前取（回复量=最大生命值，非当前）

                // 消灭裁决先行：被不灭拦下 / 复生替代 → 无回复（护盾矩阵拦 Devour，DeathRules 内定案）
                if (!DeathRules.TryKill(victim, DeathCause.Devour, context.Source, context.ZoneManager))
                    continue;

                if (devourer != null)
                {
                    devourer.Heal(maxLife);
                    PublishEvent(new Attribute.HealEvent { Target = devourer, Amount = maxLife, Source = context.Source });
                }
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect) => "吞噬：消灭目标并按其最大生命值恢复";
    }

    /// <summary>湮灭：彻底移除，直送除外区，不可复生（DeathRules 内定案）</summary>
    public class AnnihilateHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.Annihilate;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            foreach (var target in context.Targets)
            {
                if (target is Card card)
                    DeathRules.TryKill(card, DeathCause.Annihilate, context.Source, context.ZoneManager);
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect) => "湮灭目标生物";
    }

    /// <summary>
    /// 摧毁（新增原子，红3）：与"消灭"的区别——作用于**无生命值单位**（地牌/结界）。
    /// 地牌 = 双方元素池中的卡（Zone.ElementPool）：先出池（余量写回卡，不设耗尽标记——
    /// 摧毁≠资源枯竭，回收后可再作地牌），再直送拥有者墓地；结界 = 战场非生物持久物。
    /// 不经死亡决策表（无生命值者无"死亡"），发 CardDestroyEvent（Reason=Smashed）。
    /// </summary>
    public class SmashHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.Smash;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            foreach (var target in context.Targets)
            {
                if (!(target is Card card)) continue;

                // 出池：从双方元素池移除（余量写回卡上，由拥有者的池持有）
                if (context.ElementPool != null)
                {
                    var owner = card.GetOwner() ?? card.GetController();
                    if (owner == null) continue;
                    context.ElementPool.RemoveCardFromPool(card, owner);
                }

                // 直送墓地（无生命值单位不走 DeathRules）
                var cardOwner = card.GetOwner() ?? card.GetController();
                if (context.ZoneManager != null && cardOwner != null)
                {
                    var from = card.GetZone();
                    if (from != Zone.Graveyard)
                        context.ZoneManager.MoveCard(card, cardOwner, from, Zone.Graveyard);
                }

                PublishEvent(new CardDestroyEvent
                {
                    DestroyedCard = card,
                    Reason = DestroyReason.Smashed,
                    Source = context.Source
                });
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect) => "摧毁目标（无生命值单位：地牌/结界）";
    }

    // ---------------- 反制 / 沉默 ----------------

    /// <summary>
    /// 发动无效（2026-10-04 两层无效定案；原打落 KnockDown 承接）：无效化**发动**本身——净零成本。
    /// - 施放（发动区卡）：直接送墓 → cast 在消费点因「已不在发动区」中止：不付费、不结算（净效果=扣费返还）；
    /// - 场上发动（启动式/触发式）：标记该源卡待结算栈条目「发动层无效」——结算时跳过：
    ///   启动式不扣费（横置为声明期代价，不重置）、触发式退还发动次数（不计入）；
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
    /// - 场上发动（启动式/触发式）：标记栈条目「效果层无效」——扣费照走、横置不重置、触发式计入发动次数；
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
    /// 沉默指示物（定案，原"无效效果"改名改语义）：对目标附加沉默指示物——
    /// 持有者不可发动主动效果（激活式能力，挂 EffectExecutionEngine.CanActivate 第 0 步）；
    /// 未写持续时间 = 换区清除。
    /// </summary>
    public class SilenceHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.Silence;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            foreach (var target in context.Targets)
            {
                if (target == null || !target.IsAlive) continue;
                target.AddCounters(CounterRules.SilenceCounter, 1, context.Source);
                PublishEvent(new CounterChangedEvent
                {
                    Target = target,
                    CounterType = CounterRules.SilenceCounter,
                    Amount = 1,
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

    /// <summary>第三批 handler 工厂</summary>
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

                // 死亡原子
                new SacrificeHandler(),
                new AbandonHandler(),
                new DevourHandler(),
                new AnnihilateHandler(),

                // 摧毁（无生命值单位：地牌/结界）
                new SmashHandler(),

                // 反制 / 沉默（2026-10-04 两层无效定案：发动无效=净零成本 / 效果无效=扣费照付）
                new NegateActivationHandler(),
                new NegateEffectHandler(),
                new SilenceHandler(),
            };
        }
    }
}
