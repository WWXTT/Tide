using System.Collections.Generic;
using System.Linq;
using CardCore.Attribute;

namespace CardCore.Attribute.Handlers
{
    // ================================================================
    // 红色效果 - 伤害与破坏
    // ================================================================

    /// <summary>造成伤害</summary>
    public class DealDamageHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.DealDamage;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            foreach (var target in context.Targets)
            {
                // 2026-09-13 数值随机定案：每目标独立掷（掷值在修饰链前——先掷名义值再吃增伤/减伤；
                // 幅度 0 时恒名义值，行为与旧版一致）
                int dmg = context.GetValueAfterModifiers(effect.GetRolledValue());
                int lifeBefore = target.GetLife();
                target.TakeDamage(dmg, context.Source); // 关键词管线：圣盾/护甲/坚韧/吸血
                int actual = System.Math.Max(0, lifeBefore - target.GetLife());
                context.LastOutcome.RecordDamage(target, lifeBefore, actual);
                PublishEvent(new AtomicDamageEvent
                {
                    Source = context.Source,
                    Target = target,
                    Damage = dmg,
                    IsCombatDamage = false
                });
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect)
        {
            return $"造成 {effect.Value} 点伤害";
        }
    }

    // 固有全域原子（SweepDamage/SweepHeal）2026-09-21 退役——全域改由组合期 TargetKinds+全取档表达；
    // Handler 与注册已删（枚举槽位保留作序列化墓碑，见 AtomicEffects.cs）。
    // 2026-10-09 复归改道：全体对象不再走「全取档」（选取歧义），改独立原子——见 DealDamageAllEnemiesHandler。

    /// <summary>
    /// 敌方全体伤害（2026-10-09 全体对象独立原子定案，红3基准）：对敌方全体生物各造成 {value} 点伤害。
    /// 无域原子（表行 TargetKinds 空）——handler 自结算：域={对方单位}+NoRole 过滤（排除角色与无生命单位，
    /// 恰=敌方全体生物），单次执行、LastOutcome 不逐目标 Reset——杀数聚合：
    /// 击杀门槛 DmgKillsTarget=至少击杀一个即过、分支奖励只结算一次。
    /// 每单位独立掷值、走 TakeDamage 关键词管线（与 DealDamageHandler 同款结算）。
    /// </summary>
    public class DealDamageAllEnemiesHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.DealDamageAllEnemies;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            var opp = context.Controller?.Opponent;
            if (opp == null) return;

            var targets = EffectHandlerRegistry.ResolveCandidates(
                new List<int> { (int)TargetKind.EnemyLivingUnit }, "NoRole", context);
            foreach (var target in targets)
            {
                // 每单位独立掷（掷值在修饰链前——与 DealDamageHandler 逐目标口径一致）
                int dmg = context.GetValueAfterModifiers(effect.GetRolledValue());
                int lifeBefore = target.GetLife();
                target.TakeDamage(dmg, context.Source); // 关键词管线：圣盾/护甲/坚韧/吸血
                int actual = System.Math.Max(0, lifeBefore - target.GetLife());
                context.LastOutcome.RecordDamage(target, lifeBefore, actual);
                PublishEvent(new AtomicDamageEvent
                {
                    Source = context.Source,
                    Target = target,
                    Damage = dmg,
                    IsCombatDamage = false
                });
            }
        }
    }

    /// <summary>
    /// 宣告胜利（2026-09-15 终局原子，黑）：效果控制者的对手获得游戏胜利——
    /// 亡语「对手获得胜利」等终局效果载体。经 GameCore.EndGame→PublishGameOverOnce
    ///（Ended 状态守卫幂等——同批已有终局时本宣告搁浅）。TargetKinds 置空（无目标原子）。
    /// 含本原子的效果经 converter 闸一律强制（2026-09-16：纯强制批合成双 Pass 直接结算，宣判即终局）。
    /// ⚠ 方向未接线（2026-09-16 标记）：表内「效果控制者获得胜利」行与本行同 EffectType，
    /// AtomicEffectTable.GetByType 按 Type 查行互相遮蔽，且本 handler 硬编码 winner=对手——
    /// 「自己胜」方向运行时不可达。等真有卡使用时再拆枚举或加方向参数。
    /// </summary>
    public class DeclareVictoryHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.DeclareVictory;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            var winner = context.Controller?.Opponent;
            if (winner == null) return;
            if (GameCore.Instance != null)
                GameCore.Instance.EndGame(winner, GameOverReason.EffectVictory);
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect)
            => "效果控制者的对手获得胜利";
    }

    // ================================================================
    // 蓝色效果 - 控制与知识
    // ================================================================

    /// <summary>抽牌</summary>
    public class DrawCardHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.DrawCard;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            int count = context.GetValueAfterModifiers(effect.Value);
            if (context.ZoneManager != null && context.Controller != null)
            {
                for (int i = 0; i < count; i++)
                {
                    var drawn = ZoneManagerExtensions.DrawCard(context.ZoneManager, context.Controller);
                    if (drawn != null)
                    {
                        PublishEvent(new CardDrawEvent
                        {
                            Player = context.Controller,
                            DrawnCard = drawn,
                            DrawCount = 1
                        });
                    }
                }
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect)
        {
            return $"抽 {effect.Value} 张牌";
        }
    }

    /// <summary>回响（2026-10-07 关键词→普通效果改版，蓝2）：将施放卡的临时复制加入手牌——
    /// 完全复制（null 覆盖：属性/费用/效果不变），临时标记+回合末清理由 TempCopyRules 承载，
    /// 复制带效果栏（连锁天然保留）。时点=结算期（旧宣言时点关键词形态退役——发动无效=无复制）。</summary>
    public class EchoCopyHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.EchoCopy;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            // 卡身份：先取施放中的宿主卡（CastCard——魔法卡效果 Source=角色 来源归因定案），
            // 退回 Source as Card（生物/结界本体效果的 Source 即卡）
            var data = (context.CastCard as CardWrapper)?.GetData()
                       ?? (context.Source as CardWrapper)?.GetData();
            TempCopyRules.CreateTemporaryCopy(data, null, null, null, context.Controller, context.ZoneManager);
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect)
            => "将施放卡的临时复制加入手牌";
    }

    /// <summary>弹回手牌</summary>
    public class ReturnToHandHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.ReturnToHand;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            foreach (var target in context.Targets)
            {
                if (target is Card card)
                {
                    // 归属路由（2026-09-13 定案：弹回回**持有者**手牌）——临时偷取的单位被弹回原主手牌；
                    // 改写持有者后归新主。与弹回牌库/洗回同口径。
                    var owner = card.GetOwner() ?? card.GetController();
                    if (context.ZoneManager != null && owner != null)
                        context.ZoneManager.MoveCard(card, owner, Zone.Battlefield, Zone.Hand);

                    PublishEvent(new CardReturnToHandEvent
                    {
                        Card = card,
                        Source = context.Source
                    });
                }
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect)
        {
            return "将目标移回手牌";
        }
    }

    /// <summary>冻结</summary>
    public class FreezeHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.Freeze;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            foreach (var target in context.Targets)
            {
                // 冻结（衰退类，2026-10-08 层即持续定案）：层=持续回合数——强制横置 + 持有期间无法重置
                //（BlocksUntap），持有者回合末 −1、归零解除（叠加=延长）。效果级持续档不参与
                //（旧 duration 死参数已随层即持续定案删除）。
                // 指示物数量随机：Value>0 时本次层数（=回合数）掷值（每目标独立，≤0 = 空过），缺省 1 层
                int layers = effect.Value > 0
                    ? context.GetValueAfterModifiers(effect.GetRolledValue())
                    : 1;
                if (layers <= 0) continue;
                target.Freeze(layers, context.Source);
                PublishEvent(new FreezeEvent
                {
                    Target = target,
                    Source = context.Source
                });
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect)
        {
            return "冻结目标";
        }
    }

    // ================================================================
    // 绿色效果 - 恢复（成长机制 2026-10-08 已删除，本区仅剩治疗）
    // ================================================================

    /// <summary>治疗</summary>
    public class HealHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.Heal;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            foreach (var target in context.Targets)
            {
                // 2026-09-13 数值随机定案：每目标独立掷（掷值在修饰链前；幅度 0 恒名义值）
                int amount = context.GetValueAfterModifiers(effect.GetRolledValue());
                int lifeBefore = target.GetLife();
                target.Heal(amount);
                int overfill = System.Math.Max(0, amount - (target.GetLife() - lifeBefore)); // 超上限截断部分
                context.LastOutcome.RecordHeal(target, lifeBefore, amount);
                PublishEvent(new HealEvent
                {
                    Target = target,
                    Amount = amount,
                    Overfill = overfill,
                    Source = context.Source
                });
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect)
        {
            return $"恢复 {effect.Value} 点生命";
        }
    }

    /// <summary>
    /// 修改攻击力（2026-10-08 来源分轨退役）：经 StatGrantRouter 直改 _power——永久、
    /// 跨区保留、净化不清（视同本体）。临时层（换区清）用 AddPowerUp/AddPowerDown。
    /// </summary>
    public class ModifyPowerHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.ModifyPower;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            int amount = context.GetValueAfterModifiers(effect.Value);
            foreach (var target in context.Targets)
            {
                if (!(target is Card card) || !card.IsAlive) continue;
                int oldPower = card.GetPower();
                StatGrantRouter.ModifyPower(card, amount, context.Source);
                PublishEvent(new StatModifyEvent
                {
                    Target = target,
                    StatType = StatType.Power,
                    OldValue = oldPower,
                    NewValue = target.GetPower(),
                    Delta = amount,
                    Source = context.Source
                });
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect)
        {
            string sign = effect.Value >= 0 ? "+" : "";
            return $"攻击力 {sign}{effect.Value}";
        }
    }

    /// <summary>
    /// 永久属性增加（2026-10-08 恢复开放，表行 9256b41a）：+{value}/+{value} 双属性**直写字段**
    ///（复用 CounterRules.ApplyStatDelta 的 PlusOnePlusOne 直写分支，不挂计数层）——本局游戏
    /// 永久、跨区保留、净化不清（视同本体），与来源无关。临时层（换区清）用 AddPlusOne。
    /// </summary>
    public class AddPermanentPlusOneHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.AddPermanentPlusOne;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            int amount = context.GetValueAfterModifiers(effect.Value);
            foreach (var target in context.Targets)
            {
                if (!(target is Card card) || !card.IsAlive) continue;
                int oldPower = card.GetPower();
                int oldLife = card.GetLife();
                CounterRules.ApplyStatDelta(card,
                    amount >= 0 ? StatCounterKind.PlusOnePlusOne : StatCounterKind.MinusOneMinusOne,
                    amount >= 0 ? amount : -amount, context.Source);
                PublishEvent(new StatModifyEvent
                {
                    Target = card,
                    StatType = StatType.Power,
                    OldValue = oldPower,
                    NewValue = card.GetPower(),
                    Delta = amount,
                    Source = context.Source
                });
                PublishEvent(new StatModifyEvent
                {
                    Target = card,
                    StatType = StatType.Life,
                    OldValue = oldLife,
                    NewValue = card.GetLife(),
                    Delta = amount,
                    Source = context.Source
                });
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect)
            => $"属性永久+{effect.Value}/+{effect.Value}";
    }

    /// <summary>
    /// 永久属性减少（表行 e5da6dd9）：−{value}/−{value} 双属性直写字段（上限削至 0 止、
    /// 有效生命归零标死交 SBA——死亡来源=施加方）；跨区保留、净化不清，与来源无关。
    /// </summary>
    public class AddPermanentMinusOneHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.AddPermanentMinusOne;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            int amount = context.GetValueAfterModifiers(effect.Value);
            foreach (var target in context.Targets)
            {
                if (!(target is Card card) || !card.IsAlive) continue;
                int oldPower = card.GetPower();
                int oldLife = card.GetLife();
                CounterRules.ApplyStatDelta(card,
                    amount >= 0 ? StatCounterKind.MinusOneMinusOne : StatCounterKind.PlusOnePlusOne,
                    amount >= 0 ? amount : -amount, context.Source);
                PublishEvent(new StatModifyEvent
                {
                    Target = card,
                    StatType = StatType.Power,
                    OldValue = oldPower,
                    NewValue = card.GetPower(),
                    Delta = -amount,
                    Source = context.Source
                });
                PublishEvent(new StatModifyEvent
                {
                    Target = card,
                    StatType = StatType.Life,
                    OldValue = oldLife,
                    NewValue = card.GetLife(),
                    Delta = -amount,
                    Source = context.Source
                });
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect)
            => $"属性永久−{effect.Value}/−{effect.Value}";
    }

    // ================================================================
    // 灰色效果 - 通用与变化
    // ================================================================

    /// <summary>变形：变成另一张卡（StringValue=目标卡 ID），不触发死亡；离开战场时解除变回原随从</summary>
    public class MorphHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.Morph;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            // 目标形态经组合根注入的解析器查 CardData（StringValue = 目标卡 ID）
            var targetData = MorphSystem.ResolveMorphTarget?.Invoke(effect.StringValue);
            if (targetData == null) return;

            foreach (var t in context.Targets)
            {
                if (!(t is Card card)) continue;
                if (card.GetZone() != Zone.Battlefield) continue; // 定案：变形仅包含场上目标
                if (card.MorphInto(targetData))
                {
                    PublishEvent(new KeywordAppliedEvent
                    {
                        Target = card,
                        Keyword = "Morph",
                        Detail = $"变形为 {targetData.CardName}",
                        Source = context.Source
                    });
                }
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect) => "变形为另一张卡";
    }

    // ============ 战斗底盘（2026-09-10 攻击/守卫效果化） ============

    /// <summary>
    /// 攻击原子（速度0主动效果，2026-09-16）：底盘能力的计价/组合锚。
    /// 战斗流程由 CombatSystem 消费（宣言→响应窗口→结算），不走常规原子执行——
    /// 本 handler 仅满足注册完整性（VerifyHandlerCoverage 契约），直调为防御性空转。
    /// 附带「竖直参战至结算完成」由默认攻击能力的明文组合承载（Untap 自身，持续到攻击结算）。
    /// </summary>
    public class AttackHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.Attack;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            TideLog.Warn("[Attack] 攻击原子不应经常规原子路径执行（战斗流程走 CombatSystem）");
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect) => "攻击（速度0·结算期横置）";
    }

    /// <summary>
    /// 守卫原子（速度1响应拦截，2026-09-16）：底盘能力的计价/组合锚。
    /// 拦截流程由 CombatSystem 目标确认段消费（友方被指→横置自身→转移目标），
    /// 本 handler 仅满足注册完整性，直调为防御性空转。
    /// </summary>
    public class GuardHandler : AtomicEffectHandlerBase
    {
        protected override AtomicEffectType DefaultEffectType => AtomicEffectType.Guard;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            TideLog.Warn("[Guard] 守卫原子不应经常规原子路径执行（拦截走 CombatSystem）");
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect) => "守卫（速度1·响应拦截）";
    }
}
