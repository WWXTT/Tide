using System.Collections.Generic;

namespace CardCore.Attribute
{
    /// <summary>
    /// 死因分类（决策表的列索引）——术语定案（2026-09-02，用户拍板）。
    /// 「死亡 = 生命值归零」为基定义，其余术语按 来源/路径 细分：
    ///
    /// ── 正常死亡族（生命值归零）────────────────────────────────────
    /// · 伤害致死：生命归零，来源 = 伤害来源
    /// · 生命流失：归零视为正常死亡，没有伤害来源（圣盾/护甲不挡）
    /// · 生命抵扣代价：归零视为正常死亡，伤害来源是自己
    /// · 状态动作：防御归零 / 战场尸体清理
    ///
    /// ── 效果死亡族（不灭拦其中的"消灭"类）──────────────────────────────
    /// · 消灭：效果中消灭一个生物的描述；没有额外说明的都是进墓
    ///   （剧毒 Venom=消灭口径：受到其战斗伤害的生物在结算后被消灭，2026-10-08 转关键词；
    ///   原「吞噬」行 2026-10-09 改纯消灭——消灭原子（EffectType=Devour）同走本死因；
    ///   摧毁 Smash=无生命直毁不经本表，但处理器显式走 IsShielded 同拦——
    ///   2026-10-09 三口裁定：不灭免疫=剧毒/消灭/摧毁）
    /// · 湮灭：彻底移除，死亡，不可被复活，直接送到除外区
    ///
    /// ── 非死亡移除族（不触发死亡，不经本决策表）────────────────────
    /// · 牺牲（2026-10-09 舍弃并档定案）：以角色为目标，持有者自选一个单位效果
    ///   直送墓地（SacrificeHandler）——非效果死亡，不发 CardDestroyEvent
    /// · 弹回：移回手牌        · 变形：变成其他卡
    /// · 放逐：移动到除外区    · 相位：暂移除外区，回归时无格则触发死亡
    /// · 封印：翻面覆盖占格，效果无效，卡不可成为目标、封印格可作为目标
    ///   （封印需要「卡与格子两种目标」的目标系统扩展——未实装，定案先记录）
    /// </summary>
    public enum DeathCause
    {
        // ── 正常死亡族（生命值归零；神佑不拦）──
        /// <summary>伤害致死：生命归零，来源=伤害来源</summary>
        DamageLethal,
        /// <summary>生命流失：归零视为正常死亡，无伤害来源</summary>
        LifeLoss,
        /// <summary>生命抵扣代价：归零视为正常死亡，来源=自己</summary>
        LifePayment,
        /// <summary>状态动作：防御归零 / 战场尸体清理</summary>
        ZeroToughness,

        // ── 效果死亡族（神佑全拦；湮灭另改葬除外区）──
        // Poison（剧毒）/Sacrifice（牺牲）/Devour（吞噬）三死因已分别随 2026-10-08 剧毒转关键词、
        // 2026-10-09 牺牲/吞噬转直送非死亡退役（零调用方）——枚举位已随 2026-10-30 枚举收拢删除。
        /// <summary>消灭：效果死亡，默认进墓（不灭拦）</summary>
        DestroyEffect,
        /// <summary>湮灭：彻底移除，死亡，不可被复活，直送除外区</summary>
        Annihilate,
    }

    /// <summary>
    /// 死亡决策表——唯一的死亡决策点（用户定案模型）。
    ///
    /// 世界观定案：角色（玩家化身）就是普通生物单位，对生物有效的效果对他也有效。
    /// 【神佑已删 2026-10-13】角色曾以默认持有的【神佑】状态免疫效果死亡——现整体退役：
    /// 湮灭表行已删、消灭行以 TargetFilter=NoRole 在目标层滤角色，引擎层不再为角色保留免疫；
    /// 未来「效果处决角色」按高价终极手段直接写表，不再设默认护盾。
    ///
    /// 护盾×死因 矩阵（稀疏，缺省=不拦截；Poison/Sacrifice/Devour 三死因列已随各自机制退役删除——
    /// 枚举位亦已随 2026-10-30 枚举收拢清零；神佑行已随 2026-10-13 删除）：
    /// ┌─────────────┬──────────────┬──────────────┬───────────┬───────────┐
    /// │ 护盾＼死因    │ DamageLethal │ DestroyEffect │ Annihilate│ 归零族其余 │
    /// ├─────────────┼──────────────┼──────────────┼───────────┼───────────┤
    /// │ 不灭          │      ✗       │      ✓       │     ✗     │     ✗     │
    /// │ 复生（替代）   │      所有死因消耗回场；唯湮灭不可复活              │
    /// └─────────────┴──────────────┴──────────────┴───────────┴───────────┘
    ///
    /// 新护盾/新死因 = 加一行/一列注册，不改既有代码（OCP）；
    /// 每格定案对应验证器一条断言（TestKeywords 惯例）。
    /// </summary>
    public static class DeathRules
    {
        // 神佑（DivineProtection）已删（2026-10-13）：角色默认免疫状态整体退役——
        // 消灭行以 TargetFilter=NoRole 在目标层滤角色；未来「效果处决角色」按高价终极手段直写表。

        // 护盾关键词 → 拦截的死因集合
        private static readonly Dictionary<string, HashSet<DeathCause>> ShieldMatrix =
            new Dictionary<string, HashSet<DeathCause>>
            {
                // 不灭：不会被消灭（2026-10-09 三口裁定：剧毒/消灭/摧毁全拦——摧毁为 Smash 处理器
                // 显式走本判定；湮灭非消灭、照常生效）
                { KeywordRules.Indestructible, new HashSet<DeathCause> { DeathCause.DestroyEffect } },
            };

        /// <summary>决策查询：该死因是否被实体上的护盾拦截。</summary>
        public static bool IsShielded(Entity target, DeathCause cause)
        {
            if (target == null) return false;
            foreach (var row in ShieldMatrix)
                if (target.HasKeyword(row.Key) && row.Value.Contains(cause))
                    return true;
            return false;
        }

        /// <summary>
        /// 死亡全流程（效果驱动路径的统一入口）：护盾判定 →
        /// 复生替代（湮灭不可复活）→ CardDestroyEvent（Reason 按死因映射，经路由发布——
        /// OnDeath 触发时点可见）→ 按控制者落墓（湮灭直送除外区；移区再发离场事件）。
        /// true = 已死亡落葬。
        /// 事件序定案（2026-10-20 用户）：**先死亡后移墓**——逻辑因果=死亡在先、进入墓地在后、
        /// 离场随移区发布；旧序（先移墓后发死亡事件）致日志/表现层离场先于死亡，靠展示层配对
        /// 补救被否——引擎侧正本清源。OnDeath 触发此刻见卡仍在场（IsAlive 已 false）。
        /// </summary>
        public static bool TryKill(Card card, DeathCause cause, Entity source, ZoneManager zoneManager = null,
            ManaType? effectTint = null)
        {
            if (card == null) return false;
            if (IsShielded(card, cause)) return false;

            // 延迟死亡改写（2026-10-29 阶段3b）：光环活跃且受影响方、卡未带 _delayedDeath 标记
            // → 本次不死（登记「下一个回合结束时死亡」，OnTurnEndDelayedDeaths 消费）；标记在位=
            // 改写已用（防到点再改写无限延迟）。真实死亡推进时清标记——复活后是新的一命可再延迟。
            if (ShouldDelayDeath(card))
            {
                TryDelayDeath(card, cause, source, effectTint);
                return false;
            }
            card._delayedDeath = false;

            card.IsAlive = false;
            // 归因消费：进入死亡流程即清档（含复生替代——回场后死亡需重新归因，不沿尸旧来源）
            card._pendingDeathCause = null;
            card._pendingDeathSource = null;

            // 复生：死亡替代（1 血回场，消耗 1 层复生指示物——多层=多次替代；2026-10-19 起回场不横置）——湮灭不可被复活
            if (cause != DeathCause.Annihilate && KeywordRules.TryReborn(card))
                return false;

            var e = new CardDestroyEvent
            {
                DestroyedCard = card,
                Cause = cause,
                Reason = ToReason(cause),
                Source = source,
                EffectTint = effectTint // 效果基础色透传（2026-10-20：消灭族弹体按所用效果基础色）
            };
            if (GameCore.Instance != null) GameCore.Instance.PublishEvent(e);
            else EventManager.Instance.Publish(e);

            var destination = cause == DeathCause.Annihilate ? Zone.Exile : Zone.Graveyard;
            // 归属路由（2026-09-13 定案：死亡去**持有者**的墓地）——临时偷取的单位死在对手场
            // 上也回原主墓地；改写持有者（ChangeOwner/GainControl Permanent）后归新主。
            var manager = zoneManager ?? GameCore.Instance?.ZoneManager;
            var owner = card.GetOwner() ?? card.GetController();
            if (manager != null && owner != null)
            {
                var from = card.GetZone();
                if (from != destination)
                    manager.GetZoneContainer(owner).Move(card, from, destination);
                card.SetZone(destination);
            }
            return true;
        }

        /// <summary>死因 → CardDestroyEvent.Reason：消灭=Destroyed，湮灭=Annihilated，归零族=Combat
        ///（Sacrifice/Devour 臂已随 2026-10-09 牺牲直送化/吞噬移除退役）。</summary>
        private static DestroyReason ToReason(DeathCause cause)
        {
            switch (cause)
            {
                case DeathCause.DestroyEffect:
                    return DestroyReason.Destroyed;
                case DeathCause.Annihilate:
                    return DestroyReason.Annihilated;
                default:
                    return DestroyReason.Combat; // 归零族：伤害致死/状态动作（剧毒走消灭口径不经此臂）
            }
        }

        // ============ 延迟死亡（2026-10-29 阶段3b 改写光环 DelayedDeathAura，灰6 表行 977f31ca） ============
        // 受光环影响的单位死亡时改为在下个回合结束时死亡：TryKill 头部改写（置 _delayedDeath 标记
        // + 登记待死条目 + 存活恢复），GameCore.OnTurnEnd 逐回合末消费到点条目。
        // 「下个回合结束」= 登记回合计数之后的那个回合结束（turnNumber > ScheduledTurn 才到点）——
        // 额外回合/跳过回合下按全局回合号自然对齐。

        private sealed class PendingDelayedDeath
        {
            public Card Card;
            public DeathCause Cause;
            public Entity Source;
            public ManaType? EffectTint;
            public int ScheduledTurn; // 登记时的全局回合号
        }

        private static readonly List<PendingDelayedDeath> _delayedDeaths = new List<PendingDelayedDeath>();

        /// <summary>改写命中判定：光环活跃且受影响方命中 ScopeHits、卡未带标记（一命一次——
        /// 到点再 TryKill 时标记在位不再改写，防无限延迟）。</summary>
        private static bool ShouldDelayDeath(Card card)
            => !card._delayedDeath
               && RuleAuraSystem.ScopeHits(RuleAuraComponents.DelayedDeathAura, card.GetController());

        /// <summary>登记待死条目并存活恢复（镜像复生口径）：伤害/归零路径进本口时卡已标死
        ///（IsAlive=false/生命≤0）→ 回 1 血存活（否则 SBA 泵仍见尸体反复送葬）；消灭/湮灭路径
        ///（卡还活着）保持现血。归因档清空——到点死亡经条目重新归因（Source=原死亡来源）。</summary>
        private static void TryDelayDeath(Card card, DeathCause cause, Entity source, ManaType? effectTint)
        {
            card._delayedDeath = true;
            if (!card.IsAlive || card._life <= 0)
            {
                card._life = 1;
                if (card._maxLife < 1) card._maxLife = 1;
                card.IsAlive = true;
            }
            card._pendingDeathCause = null;
            card._pendingDeathSource = null;
            _delayedDeaths.Add(new PendingDelayedDeath
            {
                Card = card,
                Cause = cause,
                Source = source,
                EffectTint = effectTint,
                ScheduledTurn = GameCore.Instance?.TurnEngine?.TurnNumber ?? 0,
            });
            EventManager.Instance.Publish(new KeywordAppliedEvent
            {
                Target = card,
                Keyword = RuleAuraComponents.DelayedDeathAura,
                Detail = $"延迟死亡：{EffectText.Name(card)} 本次不死，将在下个回合结束时死亡",
                Source = RuleAuraSystem.CarrierOf(RuleAuraComponents.DelayedDeathAura),
            });
        }

        /// <summary>回合末消费（GameCore.OnTurnEnded 调）：登记于更早回合的条目到点执行死亡
        ///（原死因/原来源归因，走 TryKill 常规流程——标记在位不再改写）；卡已离场/死亡/不在战场
        /// （被弹回/被移除等——标记经换区钩已清）→ 撤销条目。</summary>
        public static void OnTurnEndDelayedDeaths(int turnNumber)
        {
            if (_delayedDeaths.Count == 0) return;
            List<PendingDelayedDeath> due = null;
            for (int i = _delayedDeaths.Count - 1; i >= 0; i--)
            {
                var p = _delayedDeaths[i];
                bool stale = p.Card == null || !p.Card.IsAlive || p.Card.GetZone() != Zone.Battlefield;
                if (stale) { _delayedDeaths.RemoveAt(i); continue; }
                if (turnNumber > p.ScheduledTurn)
                {
                    (due ??= new List<PendingDelayedDeath>()).Add(p);
                    _delayedDeaths.RemoveAt(i);
                }
            }
            if (due == null) return;
            var zm = GameCore.Instance?.ZoneManager;
            foreach (var p in due)
                TryKill(p.Card, p.Cause, p.Source, zm, p.EffectTint);
        }

        /// <summary>换区钩（ZoneContainer.OnCardMoved 直调，发动区豁免由调用方把关）：
        /// 离开战场（弹回/入墓/除外等）= 不再待死——清标记并撤销条目；再入场是新的一命可再延迟。</summary>
        public static void OnZoneChanged(Card card)
        {
            if (card == null || !card._delayedDeath) return;
            card._delayedDeath = false;
            for (int i = _delayedDeaths.Count - 1; i >= 0; i--)
                if (ReferenceEquals(_delayedDeaths[i].Card, card))
                    _delayedDeaths.RemoveAt(i);
        }

        /// <summary>局重置（GameCore.Reset 调）：待死条目跨局不残留（卡标记随换区清理/实例销毁）。</summary>
        public static void ResetDelayedDeaths() => _delayedDeaths.Clear();
    }
}
