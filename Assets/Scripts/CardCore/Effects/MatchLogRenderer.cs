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
                    return $"{Name(pay.Player)} 支付 {DescribeCost(pay.PaidCost)}";
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
                case AtomicEffectPhaseEvent phase:
                    return RenderAtomicPhase(phase);
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

        /// <summary>
        /// 效果目标选择 + 结算产出：
        /// StartApplying 出目标行（仅 Targets 非空），ResolutionComplete 出结算行（仅 LastOutcome 可读）；
        /// Activation 与 StartApplying 紧邻发布，不出行。
        /// </summary>
        private static string RenderAtomicPhase(AtomicEffectPhaseEvent e)
        {
            switch (e.Phase)
            {
                case AtomicEffectPhase.StartApplying:
                    if (e.Targets != null && e.Targets.Count > 0)
                        return $"〔效果〕{Name(e.Source)} 的 {e.EffectType} → {DescribeEntities(e.Targets)}";
                    return null;

                case AtomicEffectPhase.ResolutionComplete:
                    var o = e.Context?.LastOutcome;
                    if (!HasReadableOutcome(o)) return null;
                    if (o.DamageDealt > 0)
                        return $"〔结算〕{e.EffectType}：{DescribeEntities(o.AffectedTargets)} 共受 {o.DamageDealt} 伤害"
                               + (o.KilledTargets.Count > 0 ? $"，{DescribeEntities(o.KilledTargets)} 死亡" : "");
                    if (o.HealApplied > 0)
                        return $"〔结算〕{e.EffectType}：{DescribeEntities(o.AffectedTargets)} 回复 {o.HealApplied} 生命";
                    if (o.Declaration != null)
                        return $"〔结算〕{e.EffectType}：宣言「{o.Declaration}」{(o.DeclareHit ? "命中" : "未命中")}";
                    return $"〔结算〕{e.EffectType}：作用于 {DescribeEntities(o.AffectedTargets)}";

                default:
                    return null;
            }
        }

        // ======================================== 公共辅助（原 AiBattleE2E 内部方法公共化） ========================================

        /// <summary>实体短名（玩家名 / 卡名 / 卡 ID）</summary>
        public static string Name(Entity entity) => entity == null ? "∅"
            : entity is Player p ? p.Name
            : entity is IHasName n && !string.IsNullOrEmpty(n.CardName) ? n.CardName
            : entity is Card c ? c.ID
            : entity.ToString();

        public static bool HasReadableOutcome(EffectOutcome o)
            => o != null && (o.DamageDealt > 0 || o.HealApplied > 0
                          || o.KilledTargets.Count > 0 || o.AffectedTargets.Count > 0
                          || o.Declaration != null);

        /// <summary>同名聚合的紧凑卡牌清单（"火球、嘲讽守卫×2、…"）</summary>
        public static string DescribeCards(List<Card> cards)
            => cards == null || cards.Count == 0 ? "∅"
               : string.Join("、", cards.GroupBy(Name)
                                        .Select(g => g.Count() > 1 ? $"{g.Key}×{g.Count()}" : g.Key));

        public static string DescribeEntities(List<Entity> list)
            => list == null || list.Count == 0 ? "∅" : string.Join("、", list.Select(Name));

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
