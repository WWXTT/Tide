using System;
using System.Collections.Generic;
using System.Linq;
using CardCore.Attribute;

namespace CardCore
{
    /// <summary>
    /// 对局日志渲染器——**唯一的「事件→文本」映射**（B1 定案 2026-10-18 日志中间层收敛）：
    /// 此前引擎侧本类（22 case）/ UI 白名单 BattleEventText.ForLocal（14 订阅）/ ForNet（9 case）
    /// 三面口径不一，收敛为本类单一映射；消费方只有两个——MatchLogService 流水（Appended 即时行
    /// + ExportMarkdown 导出）与 UI BattleLogController（战报面板）。字段/时机行为保持：
    /// 同一事件、同一观侧，两处文字必然一致。
    ///
    /// 观侧（isSelf）：null=中立（导出/播报口径，玩家实名）；非 null=对局视角（"我方/对手"、
    /// 角色实体=【己方/对方角色】——本地局人类恒 P1）。
    ///
    /// 信息可见性原则：抽牌只报数量（抽到的卡是私有信息，战报不指名）；起手逐张由 GameStart
    /// 开局块（导出口径）呈现；效果获得的公开区来卡（回手/弃置等）指名。
    ///
    /// 出行过滤决策（null=不出行，附录统计仍可见原始类型）：
    /// - CardZoneChange：区域流转已由入场/离场/打出/弃置等专门行呈现（去重）；
    /// - StackEmpty：高频低信息；AttackResolved：结算细节由伤害/死亡/横置行承载（再加一行纯噪声）；
    /// - KeywordApplied：只放行守护/舍身（改写发生在 ApplyDamage 内层递归，战报只见终值会缺中间步）；
    ///   其余关键词由效果结先行（EffectExecutionSummary）覆盖，防刷屏；
    /// - Heal 原子（Source!=null）：由〔结算〕行呈现不重复；CardEnterHand 的 isDraw：见可见性原则。
    /// </summary>
    public static class MatchLogRenderer
    {
        /// <summary>渲染结果：文本 + 语义类（UI 侧映射样式，现役值=log-line--turn/phase/combat/effect/card/system，空=默认行）。</summary>
        public struct Line
        {
            public string Text;
            public string Class;
            public Line(string text, string cls) { Text = text; Class = cls; }
        }

        /// <summary>中立口径（导出/播报）：玩家实名，无观侧替换。</summary>
        public static string Render(IGameEvent e) => Render(e, null).Text;

        /// <summary>观侧口径（唯一映射主入口）：isSelf 非 null 时输出对局视角文案。</summary>
        public static Line Render(IGameEvent e, Func<Player, bool> isSelf)
        {
            switch (e)
            {
                // ==== 回合/阶段骨架 ====
                case TurnStartEvent t:
                    return new Line($"—— 回合 {t.TurnNumber}：{Who(t.TurnPlayer, isSelf)}{AiTag(t.TurnPlayer)} ——",
                        "log-line--turn");
                case PhaseStartEvent p:
                    return new Line($"〔{PhaseZh(p.Phase)}阶段〕", "log-line--phase");

                // ==== 抽牌/获得（可见性原则：抽牌只报数量） ====
                case CardDrawEvent d:
                    return new Line($"{Who(d.Player, isSelf)}抽 {Math.Max(1, d.DrawCount)} 张", "");
                case CardEnterHandEvent hand:
                    if (hand.IsDraw) return Empty; // 逐张抽牌不出行（数量行已覆盖+私有信息；建局静默由 UI _logArmed 承担）
                    return new Line($"{Who(hand.Player, isSelf)} 获得 {Name(hand.Card)}（来自 {ZoneZh(hand.FromZone)}）",
                        "log-line--effect");

                // ==== 打出/入场/离场 ====
                case CardPlayEvent c:
                    return new Line($"{Who(c.Player, isSelf)}使用 {Name(c.PlayedCard)}" +
                        (c.FromZone == Zone.Graveyard ? "（墓地）" : ""), "log-line--card");
                case CardPutToBattlefieldEvent b:
                    return new Line($"{Name(b.Card)} 入场（{(b.Tapped ? "横置" : "可用")}，来源 {b.Source}）", "log-line--card");
                case CardLeaveBattlefieldEvent l:
                    return new Line($"{Name(l.Card)} 离场", "log-line--card");
                case CardZoneChangeEvent:
                    return Empty; // 去重（专门行已呈现）

                // ==== 区域移动全景（B1 补缺：弃置/磨牌/除外/回手/洗回） ====
                case CardDiscardEvent disc:
                    return new Line($"{Who(disc.Player, isSelf)}弃置 {Name(disc.Card)}", "log-line--card");
                case CardMillEvent mill:
                    return new Line($"{Who(mill.Player, isSelf)}磨牌 {Name(mill.Card)}（牌库顶入墓）", "log-line--card");
                case CardExileEvent exile:
                    return new Line($"{Name(exile.Card)} 被除外（来自 {ZoneZh(exile.FromZone)}）", "log-line--card");
                case CardReturnToHandEvent ret:
                    return new Line($"{Name(ret.Card)} 回到手牌", "log-line--card");
                case CardShuffleIntoDeckEvent shuf:
                    return new Line($"{Name(shuf.Card)} 洗入牌库", "log-line--card");
                case TokenCreatedEvent token:
                    return new Line($"{Who(token.Controller, isSelf)}生成衍生物 {Name(token.Card)}（{ZoneZh(token.DropZone)}）",
                        "log-line--card");

                // ==== 横置/重置经济学（B1 补缺：付费自动横置/回合开始重置可见） ====
                case TapEvent tap:
                    return new Line($"{EntityName(tap.TappedEntity, isSelf)} {(tap.IsUntapping ? "重置" : "横置")}", "");
                case UntapEvent untap:
                    return new Line($"{EntityName(untap.UntappedEntity, isSelf)} 重置", "");

                // ==== 钱包全生命周期（B1 补缺：支付/放地/授予/消耗四口齐） ====
                case ElementPoolAddEvent add:
                    return new Line($"{Who(add.Player, isSelf)}放地牌 {Name(add.AddedCard)}（锁色 {ManaZh(add.LockedColor)}）",
                        "log-line--card");
                case ElementPoolPayEvent pay:
                    return new Line($"{Who(pay.Player, isSelf)}支付 {DescribeCost(pay.PaidCost)}" +
                        (string.IsNullOrEmpty(pay.SourceNote) ? "" : $"（{pay.SourceNote}）"), "");
                case ElementPoolGainEvent gain:
                    return new Line($"{Who(gain.Player, isSelf)}获得 {gain.Amount} 个{ManaZh(gain.GainedType)}临时元素（来自 {Name(gain.FromCard)}）",
                        "log-line--effect");
                case ElementPoolConsumeEvent consume:
                    return new Line($"{Who(consume.Player, isSelf)}消耗 {consume.Amount} 个{ManaZh(consume.ManaType)}临时元素",
                        "log-line--effect");

                // ==== 战斗（B1 补缺：宣言/拦截/取消三口，结算细节由伤害/死亡/横置行承载） ====
                case AttackDeclarationEvent atk:
                    return new Line($"〔攻击〕{EntityName(atk.Attacker, isSelf)} → {EntityName(atk.Target, isSelf)}",
                        "log-line--combat");
                case BlockDeclarationEvent blk:
                    return new Line($"〔守卫〕{EntityName(blk.Blocker, isSelf)} 拦截 {EntityName(blk.Attacker, isSelf)}",
                        "log-line--combat");
                case CombatCancelledEvent cc:
                    return new Line($"〔攻击取消〕{EntityName(cc.Attacker, isSelf)} 的攻击被取消", "log-line--combat");
                case AttackResolvedEvent:
                    return Empty; // 结算细节=伤害/死亡行，不重复
                case CombatDamageEvent combat:
                    return new Line($"{EntityName(combat.Attacker, isSelf)} → {EntityName(combat.Defender, isSelf)}：{combat.Damage} 伤害",
                        "log-line--combat");
                case CardDestroyEvent k:
                    return new Line($"{Name(k.DestroyedCard)} 死亡（{k.Reason}）", "log-line--combat");
                case EffectFlightEvent flight:
                {
                    // 效果不适用/无效（2026-10-20 定案：发动照常——费用照付弹体照飞，但结算无果。
                    // 此前对不灭目标放消灭=日志零提示）
                    string note = string.IsNullOrEmpty(flight.Note) ? "效果不适用" : flight.Note;
                    return new Line(
                        $"{EntityName(flight.Source, isSelf)} 的 {Name(flight.CastCard)} → " +
                        $"{EntityName(flight.Target, isSelf)}：{note}",
                        "log-line--effect");
                }

                // ==== 生命 ====
                case LifeChangeEvent life:
                    return new Line($"{Who(life.Player, isSelf)}生命 {life.OldLife} → {life.NewLife}",
                        life.NewLife < life.OldLife ? "log-line--combat" : "log-line--effect");
                case LifeLossEvent ll:
                    return new Line($"{Who(ll.Player, isSelf)}生命流失 {ll.Amount}", "log-line--combat");
                case FatigueEvent fatigue:
                    return new Line($"{Who(fatigue.Player, isSelf)}疲劳，受到 {fatigue.Damage} 伤害（空牌库抽牌）",
                        "log-line--combat");
                case HealEvent heal:
                    if (heal.Source != null) return Empty; // 原子治疗由结先行呈现，不重复
                    return new Line($"〔回复〕{EntityName(heal.Target, isSelf)} 回复 {heal.Amount} 生命", "log-line--effect");

                // ==== 状态改写（B1 补缺：属性增减/冻结） ====
                case StatModifyEvent stat:
                    return new Line($"{EntityName(stat.Target, isSelf)} {StatZh(stat.StatType)} {stat.OldValue}→{stat.NewValue}（{(stat.Delta >= 0 ? "+" : "")}{stat.Delta}）",
                        "log-line--effect");
                case FreezeEvent freeze:
                    return new Line($"{EntityName(freeze.Target, isSelf)} 被冻结", "log-line--effect");

                // ==== 效果发动与结算 ====
                case StackAddEvent s when s.AddedObject is EffectInstance ei
                    && !ei.IsCardCast && !ei.IsAttackDeclaration && !ei.IsGuardDeclaration && !ei.IsSBA
                    && ei.Definition != null:
                {
                    string src = ei.Source is Card sc ? Name(sc) : EntityName(ei.Source, isSelf);
                    return ei.Definition.ActivationType == EffectActivationType.Voluntary
                        ? new Line($"{Who(ei.Controller, isSelf)}发动：{src}「{ei.Definition.DisplayName}」", "log-line--effect")
                        : new Line($"{src} 触发「{ei.Definition.DisplayName}」", "log-line--effect");
                }
                case StackAddEvent _: // 施放/攻击/守卫/SBA：各有专属行
                    return Empty;
                case HeroSkillActivatedEvent skill:
                    return new Line($"〔技能〕{Who(skill.Player, isSelf)}发动英雄技能「{Name(skill.SkillCard)}」", "log-line--effect");
                case EffectExecutionSummaryEvent sum:
                {
                    if (string.IsNullOrEmpty(sum.Description)) return Empty;
                    string text = sum.Description;
                    if (isSelf != null && sum.Controller != null)
                    {
                        // 观侧化：句首控制器名→我方/对手（替换未命中=自定义名原文保留）
                        string prefix = Name(sum.Controller) + " 的 ";
                        if (text.StartsWith(prefix, StringComparison.Ordinal))
                            text = (isSelf(sum.Controller) ? "我方" : "对手") + " 的 " + text.Substring(prefix.Length);
                        text = text.Replace("Player 1", "【己方角色】").Replace("Player 2", "【对方角色】");
                    }
                    return new Line("　→ " + text, "log-line--effect");
                }
                case KeywordAppliedEvent kw when kw.Keyword == KeywordRules.Guardian
                                                 || kw.Keyword == KeywordRules.Martyrdom
                                                 || kw.Keyword == "运势": // 主动运势两段式（2026-10-20）：掷骰结果须玩家可见（成败都播报；自动路径同享）
                {
                    if (string.IsNullOrEmpty(kw.Detail)) return Empty;
                    string text = kw.Detail;
                    if (isSelf != null)
                        text = text.Replace("Player 1", "【己方角色】").Replace("Player 2", "【对方角色】");
                    return new Line(text, "log-line--effect");
                }
                case KeywordAppliedEvent _:
                    return Empty; // 其余关键词由结先行覆盖（防刷屏，类注释口径）

                // ==== 指示物落定 ====
                case CounterChangedEvent ctr:
                {
                    if (ctr.CounterType == "*" || ctr.Amount == 0) return Empty; // "*"=换区/净化全清标记，无逐类信息
                    string name = CounterRules.Find(ctr.CounterType).DisplayName;
                    string who = EntityName(ctr.Target, isSelf);
                    return ctr.Amount > 0
                        ? new Line(CounterRules.IsNegative(ctr.CounterType)
                            ? $"{who} 被附加 {ctr.Amount} 层{name}"
                            : $"{who} 获得 {ctr.Amount} 层{name}", "log-line--effect")
                        : new Line($"{who} 失去 {-ctr.Amount} 层{name}", "log-line--effect");
                }

                // ==== 骨架/终局 ====
                case StackEmptyEvent:
                    return Empty; // 高频低信息
                case GameOverEvent over:
                    return isSelf != null
                        ? new Line($"★ {(over.Winner != null && isSelf(over.Winner) ? "我方" : "对手")}获胜（{over.Reason}）· 共 {over.TotalTurns} 回合",
                            "log-line--system")
                        : new Line($"★ 游戏结束：胜者 {Name(over.Winner)}（{over.Reason}，共 {over.TotalTurns} 回合）",
                            "log-line--system");
                case GameStartEvent gs:
                    return new Line(RenderGameStart(gs), "log-line--system");

                default:
                    return Empty; // 未映射事件不出行（附录统计可见类型——新事件接入时补显式 case）
            }
        }

        private static Line Empty => new Line(null, null);

        /// <summary>开局块：双方牌库 + 起手（GameStartEvent 在起手抽完后发布，此处快照即终局起手）。
        /// 多行以 \n 连接。中立口径（导出/调试）——私有信息全量可见是导出口径的既定语义；
        /// UI 战报面板经 _logArmed 静默建局事件（首个回合行前不入史），此块只进导出。</summary>
        private static string RenderGameStart(GameStartEvent e)
        {
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

        // ======================================== 观侧辅助 ========================================

        private static string AiTag(Player p) => p != null && p.IsAI ? "（AI）" : "";

        /// <summary>玩家称谓：isSelf=null 用实名；否则 我方/对手。</summary>
        public static string Who(Player p, Func<Player, bool> isSelf)
        {
            if (p == null) return "?";
            return isSelf != null ? (isSelf(p) ? "我方" : "对手") : Name(p);
        }

        /// <summary>实体称谓：角色在观侧模式下=【己方/对方角色】（与悬停状态段/选择候选同口径）；卡=卡名。</summary>
        public static string EntityName(Entity en, Func<Player, bool> isSelf)
        {
            if (en is Player p) return isSelf == null ? Name(p) : (isSelf(p) ? "【己方角色】" : "【对方角色】");
            return Name(en);
        }

        // ======================================== 公共辅助（转发 EffectText 单一真相） ========================================

        /// <summary>实体短名（转发 EffectText）</summary>
        public static string Name(Entity entity) => EffectText.Name(entity);

        /// <summary>同名聚合的紧凑卡牌清单（转发 EffectText）</summary>
        public static string DescribeCards(List<Card> cards) => EffectText.DescribeCards(cards);

        /// <summary>实体清单（转发 EffectText）</summary>
        public static string DescribeEntities(List<Entity> list) => EffectText.DescribeEntities(list);

        /// <summary>元素中文名</summary>
        public static string ManaZh(ManaType type) => type switch
        {
            ManaType.Red => "红",
            ManaType.Blue => "蓝",
            ManaType.Green => "绿",
            ManaType.White => "白",
            ManaType.Black => "黑",
            _ => "灰",
        };

        /// <summary>阶段中文名（CardCore 单源——UI BattleView.PhaseZh 转发此口，2026-10-18 B1 收敛）</summary>
        public static string PhaseZh(PhaseType phase) => phase switch
        {
            PhaseType.Standby => "准备",
            PhaseType.Main => "主要",
            PhaseType.End => "结束",
            _ => phase.ToString(),
        };

        /// <summary>区域中文名（区域移动行用）</summary>
        public static string ZoneZh(Zone zone) => zone switch
        {
            Zone.Hand => "手牌",
            Zone.Deck => "牌库",
            Zone.Graveyard => "墓地",
            Zone.Exile => "除外区",
            Zone.Battlefield => "战场",
            Zone.Activation => "发动区",
            _ => zone.ToString(),
        };

        private static string StatZh(StatType stat) => stat switch
        {
            StatType.Power => "攻击",
            StatType.Life => "生命",
            StatType.Both => "属性",
            _ => stat.ToString(),
        };

        public static string DescribeCost(Dictionary<int, float> cost)
        {
            if (cost == null || cost.Count == 0) return "∅";
            return string.Join(" ", cost.Where(kv => kv.Value > 0).Select(kv => $"{ManaZh((ManaType)kv.Key)}×{kv.Value}"));
        }
    }
}
