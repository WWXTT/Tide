using System.Collections.Generic;
using System.Linq;
using CardCore.Attribute;

namespace CardCore
{
    /// <summary>
    /// 对局日志渲染器（P2b 定案）：事件 → 人话战报行，按 CLR 类型分派。
    /// 迁移自 Editor 层 ConsoleAnnouncer 的 20 个回调；表现层契约说明随迁——
    /// 本类覆盖的事件面 = BattleScreen.OnEnter 契约面 + 播报扩展（开局/效果/攻击/疲劳/回复/产出/关键词），
    /// 缺面在运行期即表现为"事件不出行"（附录统计仍可见原始类型）。
    ///
    /// 返回 null = 该事件不出行（StackEmpty 高频低信息、CardZoneChange 已由专门行去重、
    /// Activation 阶段与 StartApplying 紧邻等——与原播报器口径一致）。
    /// 起手逐张静默：_seenGameStart 与原播报器同款（起手由开局汇总行呈现）。
    /// </summary>
    public static class MatchLogRenderer
    {
        private static bool _seenGameStart;

        public static string Render(IGameEvent e)
        {
            switch (e)
            {
                case TurnStartEvent t:
                    return $"════ 回合 {t.TurnNumber} · {Name(t.TurnPlayer)} ════";
                case PhaseStartEvent p:
                    return $"〔阶段〕{Name(p.ActivePlayer)} 进入 {p.Phase}";
                case CardPlayEvent c:
                    return $"{Name(c.Player)} 打出 {Name(c.PlayedCard)}";
                case CardZoneChangeEvent:
                    return null; // 区域流转已由入场/离场/打牌等专门行呈现（去重）
                case CardPutToBattlefieldEvent b:
                    return $"{Name(b.Card)} 入场（战场，{(b.Tapped ? "横置" : "可用")}，来源 {b.Source}）";
                case CardLeaveBattlefieldEvent l:
                    return $"{Name(l.Card)} 离场";
                case LifeChangeEvent life:
                    return $"{Name(life.Player)} 生命 {life.OldLife} → {life.NewLife}";
                case CombatDamageEvent combat:
                    return $"⚔ {Name(combat.Attacker)} → {Name(combat.Defender)}，造成 {combat.Damage} 伤害";
                case ElementPoolAddEvent add:
                    return $"{Name(add.Player)} 放地牌 {Name(add.AddedCard)}（{DescribeTokens(add.Tokens)}）";
                case ElementPoolPayEvent pay:
                    return $"{Name(pay.Player)} 支付 {DescribeCost(pay.PaidCost)}" +
                           (string.IsNullOrEmpty(pay.SourceNote) ? "" : $"（{pay.SourceNote}）");
                case HeroSkillActivatedEvent skill:
                    // 技能=永续魔法实体（2026-09-21）：发动=横置技能卡——行内呈现横置语义
                    return $"〔技能〕{Name(skill.Player)} 发动 {HeroSkillSystem.SkillName(skill.Skill)}" +
                           (skill.Upgraded ? "（已升级）" : "") + "，横置技能卡";
                case StackEmptyEvent:
                    return null; // 高频低信息
                case GameOverEvent over:
                    return $"★ 游戏结束：胜者 {Name(over.Winner)}（{over.Reason}，共 {over.TotalTurns} 回合）";

                // ---- 播报专用扩展 ----
                case GameStartEvent gs:
                    return RenderGameStart(gs);
                case CardEnterHandEvent hand:
                    if (!_seenGameStart) return null; // 起手逐张静默（开局汇总行呈现）
                    return hand.IsDraw
                        ? $"{Name(hand.Player)} 抽到 {Name(hand.Card)}"
                        : $"{Name(hand.Player)} 获得 {Name(hand.Card)}（来自 {hand.FromZone}）";
                // 原子三阶段事件不再出行（2026-09-16 描述接口化：效果级文本统一走
                // EffectExecutionSummaryEvent——引擎时点语义不变，仅表现层收口）
                case EffectExecutionSummaryEvent summary:
                    return summary.Description;
                case AttackDeclarationEvent atk:
                    return $"〔攻击〕{Name(atk.Attacker)} → {Name(atk.Target)}";
                case FatigueEvent fatigue:
                    return $"〔疲劳〕{Name(fatigue.Player)} 受到 {fatigue.Damage} 伤害（空牌库抽牌）";
                case HealEvent heal:
                    if (heal.Source != null) return null; // 原子治疗由〔结算〕行呈现，不重复
                    return $"〔回复〕{Name(heal.Target)} 回复 {heal.Amount} 生命";
                case ElementPoolGainEvent gain:
                    return $"{Name(gain.Player)} 横置 {Name(gain.FromCard)} 产出 {gain.GainedType}";
                case KeywordAppliedEvent kw:
                    return $"〔关键词〕{Name(kw.Target)}：{kw.Detail}";
                case RitualCompletedEvent ritual:
                    return $"★ 仪式完成：{Name(ritual.Card)}（{ritual.RitualId}，完成者 {Name(ritual.Completer)}）";
                case TokenCreatedEvent token:
                    return $"{Name(token.Controller)} 生成衍生物 {Name(token.Card)}（{token.DropZone}）";

                default:
                    return null;
            }
        }

        /// <summary>开局块：双方牌库 + 起手（GameStartEvent 在起手抽完后发布，此处快照即终局起手）。多行以 \n 连接。</summary>
        private static string RenderGameStart(GameStartEvent e)
        {
            _seenGameStart = true;
            var lines = new List<string> { "━━━━ 开局 ━━━━" };
            var core = GameCore.Instance;
            foreach (var p in new[] { e.FirstPlayer, e.SecondPlayer })
            {
                if (p == null) continue;
                var deck = core?.ZoneManager?.GetCards(p, Zone.Deck);
                var hand = core?.ZoneManager?.GetCards(p, Zone.Hand);
                lines.Add($"{Name(p)} 牌库（{deck?.Count ?? 0}）：{DescribeCards(deck)}");
                lines.Add($"{Name(p)} 起手（{hand?.Count ?? 0}）：{DescribeCards(hand)}");
            }
            return string.Join("\n", lines);
        }

        // ======================================== 公共辅助（转发 EffectText 单一真相，2026-09-16 描述接口化） ========================================

        /// <summary>实体短名（转发 EffectText）</summary>
        public static string Name(Entity entity) => EffectText.Name(entity);

        /// <summary>同名聚合的紧凑卡牌清单（转发 EffectText）</summary>
        public static string DescribeCards(List<Card> cards) => EffectText.DescribeCards(cards);

        /// <summary>实体清单（转发 EffectText）</summary>
        public static string DescribeEntities(List<Entity> list) => EffectText.DescribeEntities(list);

        public static string DescribeTokens(Dictionary<ManaType, int> tokens)
        {
            if (tokens == null || tokens.Count == 0) return "无指示物";
            return string.Join("", tokens.Where(kv => kv.Value > 0).Select(kv => $"{kv.Key}×{kv.Value}"));
        }

        public static string DescribeCost(Dictionary<int, float> cost)
        {
            if (cost == null || cost.Count == 0) return "∅";
            return string.Join(" ", cost.Where(kv => kv.Value > 0).Select(kv => $"{(ManaType)kv.Key}×{kv.Value}"));
        }
    }
}
