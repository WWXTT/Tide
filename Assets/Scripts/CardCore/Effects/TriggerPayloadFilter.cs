using System.Collections.Generic;
using CardCore.Attribute;

namespace CardCore
{
    /// <summary>
    /// 触发时点 payload 过滤器（时点接线定案）。
    ///
    /// FindMatchingEffects 只比事件 CLR 类型，无法区分同一事件下的 self/other、
    /// 施与受、进场来源——本类在类型匹配通过后做事件载荷级过滤：
    /// 规则表见各 case 注释；未列出的时点恒通过（保持"类型匹配即命中"的旧语义）。
    ///
    /// 登场/进场族对照（影之诗三分法的 Tide 落地）：
    /// - OnPlay（登场）= 入场曲：仅自己经发动区打出入场（Source==CastPlayed）；
    ///   横置非触发代价（不检查 Tapped）
    /// - OnSummon（进场）= 进入战场时：任意来源（超集：打出也触发，复活/token 只触发本时点）
    /// - OnOtherCreatureEnter = 自己的 XX 进场时：进场的不是自己
    /// </summary>
    public static class TriggerPayloadFilter
    {
        /// <summary>
        /// 类型匹配通过后的事件载荷过滤。
        /// registered：待判定的注册效果（Source=效果来源实体）。
        /// </summary>
        public static bool Matches(TriggerTiming timing, IGameEvent gameEvent, RegisteredEffect registered)
        {
            switch (timing)
            {
                // ---- 登场/进场族（CardPutToBattlefieldEvent）----
                case TriggerTiming.OnPlay:
                    return gameEvent is CardPutToBattlefieldEvent p &&
                           ReferenceEquals(p.Card, registered.Source) &&
                           p.Source == EnterSource.CastPlayed;

                case TriggerTiming.OnSummon:
                    // 任意来源进场（含打出）——OnPlay 的超集
                    return gameEvent is CardPutToBattlefieldEvent s &&
                           ReferenceEquals(s.Card, registered.Source);

                case TriggerTiming.OnOtherCreatureEnter:
                    return gameEvent is CardPutToBattlefieldEvent o &&
                           o.Card != null &&
                           !ReferenceEquals(o.Card, registered.Source);

                case TriggerTiming.OnReturnFromGraveyard:
                    return gameEvent is CardPutToBattlefieldEvent r &&
                           r.Source == EnterSource.Revived &&
                           ReferenceEquals(r.Card, registered.Source);

                // ---- 使用宣言观察族（CardPlayEvent，付费前）----
                case TriggerTiming.OnCardPlayed:
                    // 任意卡使用宣言（含法术、含对手——对手/自己限定交给 TriggerConditions）
                    return true;

                case TriggerTiming.OnSpellCast:
                    return gameEvent is CardPlayEvent c &&
                           c.PlayedCard is IHasSupertype st &&
                           st.Supertype == Cardtype.Spell;

                // ---- 伤害族（DamageEvent，施受区分）----
                case TriggerTiming.OnDealDamage:
                    return gameEvent is DamageEvent d &&
                           ReferenceEquals(d.Source, registered.Source);

                case TriggerTiming.OnTakeDamage:
                    return gameEvent is DamageEvent t &&
                           ReferenceEquals(t.Target, registered.Source);

                // ---- 战斗族 ----
                case TriggerTiming.OnAttacked:
                    // 被指方（攻方用 OnAttack）
                    return gameEvent is AttackDeclarationEvent a &&
                           ReferenceEquals(a.Target, registered.Source);

                // ---- 被指定为目标（原子三阶段，依赖统一路由修复）----
                case TriggerTiming.OnTargeted:
                    return gameEvent is AtomicEffectPhaseEvent ph &&
                           ph.Phase == AtomicEffectPhase.StartApplying &&
                           ph.Targets != null &&
                           ContainsEntity(ph.Targets, registered.Source);

                // ---- 除外族 ----
                case TriggerTiming.OnExile:
                    return gameEvent is CardExileEvent e &&
                           ReferenceEquals(e.Card, registered.Source);

                default:
                    return true;
            }
        }

        private static bool ContainsEntity(List<Entity> list, Entity entity)
        {
            if (entity == null) return false;
            for (int i = 0; i < list.Count; i++)
            {
                if (ReferenceEquals(list[i], entity)) return true;
            }
            return false;
        }
    }
}
