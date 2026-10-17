using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CardCore;
using CardCore.Attribute;
using CardCore.Network;

namespace SynergyUI
{
    /// <summary>
    /// 战报文本渲染（2026-09-24 阶段四）：两种模式同构——
    /// 本地 GameEvent（强类型模板）与网络 NetEvent（schema-less Params 通用渲染）
    /// 输出同一格式的中文战报行（文本 + 样式类）。快照全量渲染 + 战报追加，动画后置。
    /// </summary>
    public static class BattleEventText
    {
        /// <summary>渲染结果：文本 + Common.uss 战报样式类（空=默认行）。</summary>
        public struct Line
        {
            public string Text;
            public string Class;
            public Line(string text, string cls) { Text = text; Class = cls; }
        }

        // ============================================================
        // 本地入口（GameEvent 强类型）
        // ============================================================

        /// <summary>isSelf：玩家归属判定（本地=P1；网络侧不用此入口）。</summary>
        public static Line ForLocal(IGameEvent e, Func<Player, bool> isSelf)
        {
            switch (e)
            {
                case TurnStartEvent t:
                    return new Line($"—— 回合 {t.TurnNumber}：{Who(t.TurnPlayer, isSelf)} ——" +
                        (t.TurnPlayer != null && t.TurnPlayer.IsAI ? "（AI）" : ""), "log-line--turn");

                // 阶段分段（2026-10-17）：回合行下按 准备/主要/结束 三段切开——
                // 中文口径与中线状态行同源（BattleView.PhaseZh）。
                case PhaseStartEvent ph:
                    return new Line($"〔{BattleView.PhaseZh(ph.Phase)}阶段〕", "log-line--phase");

                case CardDrawEvent d:
                    return new Line($"{Who(d.Player, isSelf)}抽 {Math.Max(1, d.DrawCount)} 张", "");

                case CardPlayEvent p:
                    return new Line($"{Who(p.Player, isSelf)}使用 {CardName(p.PlayedCard)}" +
                        (p.FromZone == Zone.Graveyard ? "（墓地）" : ""), "log-line--card");

                case CombatDamageEvent c:
                    return new Line($"{EntityName(c.Attacker, isSelf)} → {EntityName(c.Defender, isSelf)}：{c.Damage} 伤害", "log-line--combat");

                case LifeChangeEvent l:
                    return new Line($"{Who(l.Player, isSelf)}生命 {l.OldLife} → {l.NewLife}",
                        l.NewLife < l.OldLife ? "log-line--combat" : "log-line--effect");

                case LifeLossEvent ll:
                    return new Line($"{Who(ll.Player, isSelf)}生命流失 {ll.Amount}", "log-line--combat");

                case CardDestroyEvent k:
                    return new Line($"{CardName(k.DestroyedCard)} 死亡（{k.Reason}）", "log-line--combat");

                case FatigueEvent f:
                    return new Line($"{Who(WhoOf(f), isSelf)}疲劳（空牌库抽牌）", "log-line--combat");

                // 效果发动（2026-10-16 定案：主动/自动效果发动进日志）——StackAddEvent 精滤：
                // 只记效果实例（生物/结界主动、触发式自动），施放（CardPlay）/攻击宣言/守卫/
                // SBA 伪对象不在此行（各有专属行或属系统内部）。
                case StackAddEvent s when s.AddedObject is EffectInstance ei
                    && !ei.IsCardCast && !ei.IsAttackDeclaration && !ei.IsGuardDeclaration && !ei.IsSBA
                    && ei.Definition != null:
                {
                    string src = ei.Source is Card sc ? CardName(sc) : EntityName(ei.Source, isSelf);
                    return ei.Definition.ActivationType == EffectActivationType.Voluntary
                        ? new Line($"{Who(ei.Controller, isSelf)}发动：{src}「{ei.Definition.DisplayName}」", "log-line--effect")
                        : new Line($"{src} 触发「{ei.Definition.DisplayName}」", "log-line--effect");
                }
                case StackAddEvent _: // 施放/攻击/守卫/SBA：不重复记（空行=订阅方跳过）
                    return new Line(null, null);

                // 指示物落定（2026-10-16）：效果结算的「谁获得/失去什么」——此前增益类整段
                // 无日志（触发行只报效果名，潜行/圣盾/护甲等落定对象全程不可见）。
                // "*"=换区/净化全清标记（每次移卡都发，无逐类信息）显式空行跳过，
                // 防止回落 GenericLocal 反射兜底打出原生行。
                case CounterChangedEvent ctr:
                {
                    if (ctr.CounterType == "*" || ctr.Amount == 0) return new Line(null, null);
                    string name = CounterRules.Find(ctr.CounterType).DisplayName;
                    string who = EntityName(ctr.Target, isSelf);
                    return ctr.Amount > 0
                        ? new Line(CounterRules.IsNegative(ctr.CounterType)
                            ? $"{who} 被附加 {ctr.Amount} 层{name}"
                            : $"{who} 获得 {ctr.Amount} 层{name}", "log-line--effect")
                        : new Line($"{who} 失去 {-ctr.Amount} 层{name}", "log-line--effect");
                }

                // 关键词改写步（2026-10-17）：守护/舍身改写发生在 ApplyDamage 内层递归里，
                // 战报此前只见最终的「谁 → 谁造成 N 伤害」，缺中间改写描述行。KeywordAppliedEvent
                // 按关键词过滤只放行守护/舍身（结成 + 改写行）；其余关键词（圣盾/护甲/易损/吸血/
                // 潜行/禁魔石…）暂不放行防刷屏，显式空行防回落 GenericLocal 打出反射原生行。
                // 角色名观侧化同 EffectExecutionSummaryEvent 口径（本地局玩家名恒 Player 1/2）。
                case KeywordAppliedEvent kw when kw.Keyword == KeywordRules.Guardian
                                             || kw.Keyword == KeywordRules.Martyrdom:
                {
                    if (string.IsNullOrEmpty(kw.Detail)) return new Line(null, null);
                    return new Line(kw.Detail
                        .Replace("Player 1", "【己方角色】").Replace("Player 2", "【对方角色】"), "log-line--effect");
                }
                case KeywordAppliedEvent _:
                    return new Line(null, null);

                case GameOverEvent g:
                    return new Line($"★ {(g.Winner != null && isSelf(g.Winner) ? "我方" : "对手")}获胜（{g.Reason}）· 共 {g.TotalTurns} 回合", "log-line--system");

                // 效果结算完整文本（2026-10-17 战报指名）：Description 含逐原子「→ 具体目标名」——
                // 发动/触发行只报效果名（表内静态文本泛称「己方单位」），具体落点由此行补明。
                // 前缀「　→ 」缩进=视觉挂在上一条发动/触发行下；观侧化=句首控制器名→我方/对手、
                // 正文角色名→【己方/对方角色】（本地局玩家名恒 Player 1/2；替换未命中=自定义名原文保留）。
                case EffectExecutionSummaryEvent sum:
                {
                    if (string.IsNullOrEmpty(sum.Description)) return new Line(null, null);
                    string text = sum.Description;
                    if (sum.Controller != null && isSelf != null)
                    {
                        string prefix = EffectText.Name(sum.Controller) + " 的 ";
                        if (text.StartsWith(prefix, StringComparison.Ordinal))
                            text = (isSelf(sum.Controller) ? "我方" : "对手") + " 的 " + text.Substring(prefix.Length);
                    }
                    text = text.Replace("Player 1", "【己方角色】").Replace("Player 2", "【对方角色】");
                    return new Line("　→ " + text, "log-line--effect");
                }

                default:
                    return GenericLocal(e, isSelf);
            }
        }

        /// <summary>未知事件：反射拼接公共字段（信息保真兜底；Name/Player/Card/Amount 类）。</summary>
        private static Line GenericLocal(IGameEvent e, Func<Player, bool> isSelf)
        {
            var sb = new StringBuilder(e.GetType().Name);
            try
            {
                foreach (var prop in e.GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly))
                {
                    object value;
                    try { value = prop.GetValue(e); } catch { continue; }
                    string text = null;
                    if (value is Player p) text = Who(p, isSelf);
                    else if (value is Card c) text = CardName(c);
                    else if (value is Entity en) text = EntityName(en, isSelf);
                    else if (value is Enum || value is int || value is float || value is bool || value is string) text = value.ToString();
                    if (text == null) continue; // 复杂对象跳过
                    if (sb.Length + text.Length > 90) break;
                    sb.Append($" {prop.Name}={text}");
                }
            }
            catch { /* 反射失败只剩类型名 */ }
            return new Line(sb.ToString(), "");
        }

        private static Player WhoOf(FatigueEvent f)
        {
            try { return (Player)f.GetType().GetProperty("Player")?.GetValue(f); }
            catch { return null; }
        }

        // ============================================================
        // 网络入口（NetEvent schema-less Params）
        // ============================================================

        public static Line ForNet(NetEvent e, int viewerSeat)
        {
            var map = new Dictionary<string, NetParam>(e.Params?.Length ?? 0, StringComparer.Ordinal);
            if (e.Params != null)
                foreach (var p in e.Params) map[p.FieldName] = p;

            switch (e.EventType)
            {
                case "TurnStartEvent":
                {
                    string who = Entity(map, "TurnPlayer", viewerSeat);
                    return new Line($"—— 回合 {Int(map, "TurnNumber")}：{who} ——", "log-line--turn");
                }

                case "PhaseStartEvent":
                {
                    var phase = (PhaseType)Int(map, "Phase");
                    return new Line($"〔{BattleView.PhaseZh(phase)}阶段〕", "log-line--phase");
                }

                case "CardDrawEvent":
                {
                    string who = Entity(map, "Player", viewerSeat);
                    return new Line($"{who}抽 {Math.Max(1, Int(map, "DrawCount"))} 张", "");
                }

                case "CardPlayEvent":
                {
                    string who = Entity(map, "Player", viewerSeat);
                    string card = Entity(map, "PlayedCard", viewerSeat);
                    string tag = Int(map, "FromZone") == (int)Zone.Graveyard ? "（墓地）" : "";
                    return new Line($"{who}使用 {card}{tag}", "log-line--card");
                }

                case "CombatDamageEvent":
                {
                    string atk = Entity(map, "Attacker", viewerSeat);
                    string def = Entity(map, "Defender", viewerSeat);
                    return new Line($"{atk} → {def}：{Int(map, "Damage")} 伤害", "log-line--combat");
                }

                case "LifeChangeEvent":
                {
                    string who = Entity(map, "Player", viewerSeat);
                    int oldL = Int(map, "OldLife"), newL = Int(map, "NewLife");
                    return new Line($"{who}生命 {oldL} → {newL}",
                        newL < oldL ? "log-line--combat" : "log-line--effect");
                }

                case "LifeLossEvent":
                {
                    string who = Entity(map, "Player", viewerSeat);
                    return new Line($"{who}生命流失 {Int(map, "Amount")}", "log-line--combat");
                }

                case "CardDestroyEvent":
                {
                    string card = Entity(map, "DestroyedCard", viewerSeat);
                    return new Line($"{card} 死亡（{Str(map, "Reason")}）", "log-line--combat");
                }

                case "GameOverEvent":
                {
                    string winner = Entity(map, "Winner", viewerSeat);
                    return new Line($"★ {winner}获胜（{Str(map, "Reason")}）· 共 {Int(map, "TotalTurns")} 回合", "log-line--system");
                }

                default:
                    return GenericNet(e, map, viewerSeat);
            }
        }

        private static Line GenericNet(NetEvent e, Dictionary<string, NetParam> map, int viewerSeat)
        {
            var sb = new StringBuilder(e.EventType);
            foreach (var kv in map)
            {
                string text = ParamText(kv.Value, viewerSeat);
                if (text == null) continue;
                if (sb.Length + text.Length > 90) break;
                sb.Append($" {kv.Key}={text}");
            }
            return new Line(sb.ToString(), "");
        }

        // ---- NetParam 取值帮助 ----
        private static string Entity(Dictionary<string, NetParam> map, string field, int viewerSeat)
        {
            NetParam p;
            return map.TryGetValue(field, out p) && p.EntityRefs != null && p.EntityRefs.Length > 0
                ? BattleView.EntityTextOf(p.EntityRefs[0], viewerSeat)
                : "?";
        }

        private static int Int(Dictionary<string, NetParam> map, string field)
        {
            NetParam p;
            return map.TryGetValue(field, out p) ? (int)p.IntValue : 0;
        }

        private static bool Bool(Dictionary<string, NetParam> map, string field)
        {
            NetParam p;
            return map.TryGetValue(field, out p) && p.IntValue != 0;
        }

        private static string Str(Dictionary<string, NetParam> map, string field)
        {
            NetParam p;
            return map.TryGetValue(field, out p) ? (p.StringValue ?? p.IntValue.ToString()) : "?";
        }

        private static string ParamText(NetParam p, int viewerSeat)
        {
            switch (p.Kind)
            {
                case NetParamKind.Null: return null;
                case NetParamKind.Int:
                case NetParamKind.Float: return p.IntValue.ToString();
                case NetParamKind.String: return p.StringValue;
                case NetParamKind.Enum: return p.StringValue ?? p.IntValue.ToString();
                case NetParamKind.Entity:
                    return p.EntityRefs != null && p.EntityRefs.Length > 0
                        ? BattleView.EntityTextOf(p.EntityRefs[0], viewerSeat) : null;
                case NetParamKind.EntityList:
                    return p.EntityRefs != null ? $"{p.EntityRefs.Length}个对象" : null;
                case NetParamKind.Opaque:
                    return string.IsNullOrEmpty(p.StringValue) ? null : (p.StringValue.Length > 20 ? p.StringValue.Substring(0, 20) + "…" : p.StringValue);
                default: return null;
            }
        }

        // ============================================================
        // 共用名片段
        // ============================================================
        private static string Who(Player p, Func<Player, bool> isSelf)
        {
            if (p == null) return "?";
            return isSelf != null && isSelf(p) ? "我方" : "对手";
        }

        private static string CardName(Card c)
        {
            if (c == null) return "?";
            if (c is CardWrapper w)
            {
                var d = w.GetData();
                return string.IsNullOrEmpty(d?.CardName) ? (d?.ID ?? "?") : d.CardName;
            }
            return c.ToString();
        }

        private static string EntityName(Entity en, Func<Player, bool> isSelf)
        {
            // 2026-10-17 战报指名：角色目标不再泛称「我方/对手」——与悬停状态段/选择候选同口径
            //【己方/对方角色】（卡目标本就显卡名；CombatDamage/CounterChanged/反射兜底共用此口）。
            if (en is Player p) return isSelf != null && isSelf(p) ? "【己方角色】" : "【对方角色】";
            if (en is Card c) return CardName(c);
            return en != null ? en.ToString() : "?";
        }
    }
}
