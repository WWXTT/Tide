using System.Collections.Generic;

namespace CardCore.Attribute
{
    /// <summary>
    /// 死因分类（决策表的列索引）——术语定案（2026-09-02，用户拍板）。
    /// 「死亡 = 生命值归零」为基定义，其余术语按 来源/路径 细分：
    ///
    /// ── 正常死亡族（生命值归零；神佑不拦）──────────────────────────
    /// · 伤害致死：生命归零，来源 = 伤害来源
    /// · 生命流失：归零视为正常死亡，没有伤害来源（圣盾/护甲不挡）
    /// · 生命抵扣代价：归零视为正常死亡，伤害来源是自己
    /// · 状态动作：防御归零 / 战场尸体清理
    ///
    /// ── 效果死亡族（神佑全拦；不灭拦其中的"消灭"类）────────────────
    /// · 剧毒：指示物（持续1回合），回合结束时持有者死亡——效果死亡，无伤害来源
    /// · 消灭：效果中消灭一个生物的描述；没有额外说明的都是进墓
    /// · 牺牲：玩家主动将自己场上生物置入坟墓场，视为死亡，来源是控制者
    /// · 吞噬：一个生物消灭另一个并获得其能力/生命等，死亡，来源是吞噬者
    /// · 湮灭：彻底移除，死亡，不可被复活，直接送到除外区
    ///
    /// ── 非死亡移除族（不触发死亡，不经本决策表）────────────────────
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
        /// <summary>剧毒：效果死亡，来源=剧毒效果来源</summary>
        Poison,
        /// <summary>消灭：效果死亡，默认进墓（不灭拦）</summary>
        DestroyEffect,
        /// <summary>牺牲：玩家主动置入坟墓场，来源=控制者</summary>
        Sacrifice,
        /// <summary>吞噬：被吞噬消灭（吞噬者获得其能力/生命），来源=吞噬者</summary>
        Devour,
        /// <summary>湮灭：彻底移除，死亡，不可被复活，直送除外区</summary>
        Annihilate,
    }

    /// <summary>
    /// 死亡决策表——唯一的死亡决策点（用户定案模型）。
    ///
    /// 世界观定案：角色（玩家化身）就是普通生物单位，对生物有效的效果对他也有效；
    /// 使其免疫各种效果的是默认持有的【神佑】状态（可被效果移除的真实状态，非硬编码）——
    /// 只接受生命值归零的死亡。
    ///
    /// 护盾×死因 矩阵（稀疏，缺省=不拦截）：
    /// ┌─────────────┬──────────────┬───────┬──────────────┬──────────┬──────────┬──────────┬───────────┐
    /// │ 护盾＼死因    │ DamageLethal │ Poison │ DestroyEffect │ Sacrifice │ Devour  │ Annihilate│ 归零族其余 │
    /// ├─────────────┼──────────────┼───────┼──────────────┼──────────┼──────────┼──────────┼───────────┤
    /// │ 不灭          │      ✗       │   ✗   │      ✓       │     ✗    │    ✓     │    ✗     │     ✗     │
    /// │ 神佑(角色默认) │      ✗       │   ✓   │      ✓       │     ✓    │    ✓     │    ✓     │     ✗     │
    /// │ 复生（替代）   │      所有死因消耗回场；唯湮灭不可复活                        │          │
    /// └─────────────┴──────────────┴───────┴──────────────┴──────────┴──────────┴──────────┴───────────┘
    ///
    /// 新护盾/新死因 = 加一行/一列注册，不改既有代码（OCP）；
    /// 每格定案对应验证器一条断言（TestKeywords 惯例）。
    /// </summary>
    public static class DeathRules
    {
        /// <summary>神佑：角色默认持有的状态（Player 构造时写入），免疫一切效果死亡，只接受生命值归零。</summary>
        public const string DivineProtection = "DivineProtection";

        /// <summary>效果死亡族（神佑拦截的死因集合）——正常死亡族（归零）不在其中。</summary>
        private static readonly HashSet<DeathCause> EffectDeathCauses = new HashSet<DeathCause>
        {
            DeathCause.Poison,
            DeathCause.DestroyEffect,
            DeathCause.Sacrifice,
            DeathCause.Devour,
            DeathCause.Annihilate,
        };

        // 护盾关键词 → 拦截的死因集合
        private static readonly Dictionary<string, HashSet<DeathCause>> ShieldMatrix =
            new Dictionary<string, HashSet<DeathCause>>
            {
                // 不灭：不会被消灭（消灭/吞噬等"消灭类"路径；湮灭非消灭、照常生效）
                { KeywordRules.Indestructible, new HashSet<DeathCause> { DeathCause.DestroyEffect, DeathCause.Devour } },
                // 神佑：免疫一切效果死亡，只接受生命值归零（角色默认持有）
                { DivineProtection, EffectDeathCauses },
            };

        /// <summary>决策查询：该死因是否被实体上的护盾拦截（角色经神佑在此裁决）。</summary>
        public static bool IsShielded(Entity target, DeathCause cause)
        {
            if (target == null) return false;
            foreach (var row in ShieldMatrix)
                if (target.HasKeyword(row.Key) && row.Value.Contains(cause))
                    return true;
            return false;
        }

        /// <summary>
        /// 死亡全流程（效果驱动路径的统一入口）：护盾判定 → 仪式回手（摧毁特例）→
        /// 复生替代（湮灭不可复活）→ 按控制者落墓（湮灭直送除外区）→ CardDestroyEvent
        /// （Reason 按死因映射，经路由发布——On_Death 触发时点可见）。true = 已死亡落葬。
        /// </summary>
        public static bool TryKill(Card card, DeathCause cause, Entity source, ZoneManager zoneManager = null)
        {
            if (card == null) return false;
            if (IsShielded(card, cause)) return false;

            // 进行中仪式：被摧毁 → 回手牌（手牌满则入墓），任务进度作废（完成态已被不灭挡住）
            var manager = zoneManager ?? GameCore.Instance?.ZoneManager;
            if (cause == DeathCause.DestroyEffect && RitualSystem.IsActiveRitual(card))
            {
                if (manager != null)
                {
                    RitualSystem.OnDestroyed(card, manager);
                    return false;
                }
            }

            card.IsAlive = false;

            // 复生：死亡替代（1 血回场 + 横置 + 失调，消耗关键词）——湮灭不可被复活
            if (cause != DeathCause.Annihilate && KeywordRules.TryReborn(card))
                return false;

            var destination = cause == DeathCause.Annihilate ? Zone.Exile : Zone.Graveyard;
            var owner = card.GetController();
            if (manager != null && owner != null)
            {
                var from = card.GetZone();
                if (from != destination)
                    manager.GetZoneContainer(owner).Move(card, from, destination);
                card.SetZone(destination);
            }

            var e = new CardDestroyEvent
            {
                DestroyedCard = card,
                Reason = ToReason(cause),
                Source = source
            };
            if (GameCore.Instance != null) GameCore.Instance.PublishEvent(e);
            else EventManager.Instance.Publish(e);
            return true;
        }

        /// <summary>死因 → CardDestroyEvent.Reason：消灭/吞噬=Destroyed，牺牲=Sacrificed，湮灭=Annihilated，归零族=Combat。</summary>
        private static DestroyReason ToReason(DeathCause cause)
        {
            switch (cause)
            {
                case DeathCause.DestroyEffect:
                case DeathCause.Devour:
                    return DestroyReason.Destroyed;
                case DeathCause.Sacrifice:
                    return DestroyReason.Sacrificed;
                case DeathCause.Annihilate:
                    return DestroyReason.Annihilated;
                default:
                    return DestroyReason.Combat; // 归零族：伤害致死/剧毒/状态动作
            }
        }
    }
}
