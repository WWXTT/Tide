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
        /// <summary>隐蔽信息可见开关（2026-11-04 用户定案）：开局块双方牌库/起手全量清单只允许
        /// AI 对战/无头验证展示（驱动入口 AiDuelRunner.Run / AiBattleDriver 置 true，用毕恢复）；
        /// 真实对战（本地/联机/教学）默认 false——日志与导出不得含双方隐蔽区信息，开局块退
        /// 「谁先手」两行。渲染层单点闸（MatchLogService 冻结文本在事件时点定格，同受此闸），
        /// 事件流零变化。</summary>
        public static bool AllowHiddenInfo = false;

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
                // 准备阶段行=重置账目行（2026-10-26 行序收口）：引擎发布序=回合自动化（重置+抽牌）先于
                // PhaseStartEvent（2026-09-24 回合推进口径，ResourceLedger/触发承接钉在现序），相位行语义
                // 在本映射层收口——〔准备阶段〕由本事件承载，重置明细与抽牌行随之呈现为准备阶段内容。
                case StandbyResetEvent r:
                {
                    var parts = new List<string>(4);
                    if (r.UntappedLands > 0) parts.Add($"地牌 {r.UntappedLands} 张解横置");
                    if (r.UntappedUnits > 0) parts.Add($"单位 {r.UntappedUnits} 个解横置");
                    if (r.SkillReset) parts.Add("技能就绪");
                    if (r.LandCapAdvanced) parts.Add($"地牌槽上限→{r.LandCap}");
                    string body = parts.Count == 0 ? "无变化" : string.Join("、", parts);
                    return new Line($"〔{PhaseZh(PhaseType.Standby)}阶段〕{Who(r.Player, isSelf)} 重置：{body}",
                        "log-line--phase");
                }
                case StandbySkippedEvent s: // 跳过自动化（教学直入/相位级跳过）：不发 StandbyResetEvent，本事件兜行
                    return new Line($"〔{PhaseZh(PhaseType.Standby)}阶段〕{Who(s.Player, isSelf)} 跳过（无重置/抽牌）",
                        "log-line--phase");
                case TurnEndCounterSummaryEvent tc when tc.Parts != null && tc.Parts.Count > 0:
                {
                    // 回合末指示物变化汇总（2026-11-04 用户定案「像回合开始那样一次性报出」）：
                    // 毒素发作减半/临时到期移除/衰退族逐层倒数聚合一行（此前逐项 KeywordAppliedEvent
                    // 全在白名单外=零播报）。parts 由引擎预拼中立名，此处只做角色实名观侧替换。
                    string body = string.Join("、", tc.Parts);
                    if (isSelf != null)
                        body = body.Replace("Player 1", "【己方角色】").Replace("Player 2", "【对方角色】");
                    return new Line($"〔{PhaseZh(PhaseType.End)}阶段〕{Who(tc.TurnPlayer, isSelf)} 指示物变化：{body}",
                        "log-line--phase");
                }
                case PhaseStartEvent p when p.Phase == PhaseType.Standby:
                    return Empty; // 准备阶段行由 StandbyResetEvent 承载（见上注——自动化先于相位事件发布）
                case PhaseStartEvent p:
                    return new Line($"〔{PhaseZh(p.Phase)}阶段〕", "log-line--phase");

                // ==== 抽牌/获得（可见性原则：抽牌只报数量） ====
                case CardDrawEvent d:
                    return new Line($"{Who(d.Player, isSelf)} 抽 {Math.Max(1, d.DrawCount)} 张", "");
                case CardEnterHandEvent hand:
                    if (hand.IsDraw) return Empty; // 逐张抽牌不出行（数量行已覆盖+私有信息；建局静默由 UI _logArmed 承担）
                    return new Line($"{Who(hand.Player, isSelf)} 获得 {Name(hand.Card)}（来自 {ZoneZh(hand.FromZone)}）",
                        "log-line--effect");

                // ==== 打出/入场/离场 ====
                case CardPlayEvent c:
                    return new Line(
                        c.IsResponseCast // 响应窗出牌（2026-11-03 连锁日志定案）：主阶段「使用」/响应「连锁了」分叉
                            ? $"{Who(c.Player, isSelf)} 连锁了 {Name(c.PlayedCard)}"
                            : $"{Who(c.Player, isSelf)} 使用 {Name(c.PlayedCard)}" +
                              (c.FromZone == Zone.Graveyard ? "（从墓地）" : ""),
                        "log-line--card");
                case CardPutToBattlefieldEvent b:
                {
                    string src = EnterSourceZh(b.Source);
                    string tail = src != null ? $"，来源 {src}" : "";
                    return new Line($"{Name(b.Card)} 入场（{(b.Tapped ? "横置" : "可用")}{tail}）", "log-line--card");
                }
                case CardLeaveBattlefieldEvent l:
                    return new Line($"{OwnerPrefix(l.Card, isSelf)}{Name(l.Card)} 离场", "log-line--card");
                case CardZoneChangeEvent:
                    return Empty; // 去重（专门行已呈现）

                // ==== 区域移动全景（B1 补缺：弃置/磨牌/除外/回手/洗回） ====
                case CardDiscardEvent disc:
                    return new Line($"{Who(disc.Player, isSelf)} 弃置 {Name(disc.Card)}", "log-line--card");
                case CardMillEvent mill:
                    return new Line($"{Who(mill.Player, isSelf)} 磨牌 {Name(mill.Card)}（牌库顶入墓）", "log-line--card");
                case CardExileEvent exile:
                    return new Line($"{Name(exile.Card)} 被除外（来自 {ZoneZh(exile.FromZone)}）", "log-line--card");
                case CardReturnToHandEvent ret:
                    return new Line($"{Name(ret.Card)} 回到手牌", "log-line--card");
                case CardShuffleIntoDeckEvent shuf:
                    return new Line($"{Name(shuf.Card)} 洗入牌库", "log-line--card");
                case TokenCreatedEvent token:
                    return new Line($"{Who(token.Controller, isSelf)} 生成衍生物 {Name(token.Card)}（{ZoneZh(token.DropZone)}）",
                        "log-line--card");

                // ==== 横置/重置经济学（B1 补缺：付费自动横置/回合开始重置可见） ====
                case TapEvent tap:
                    return new Line($"{EntityName(tap.TappedEntity, isSelf)} {(tap.IsUntapping ? "重置" : "横置")}", "");
                case UntapEvent untap when untap.IsTurnStartReset:
                    return Empty; // 回合开始批量重置：逐单位行由 StandbyResetEvent 汇总（2026-10-26 防与账目行双记）
                case UntapEvent untap:
                    return new Line($"{EntityName(untap.UntappedEntity, isSelf)} 重置", "");

                // ==== 钱包全生命周期（B1 补缺：支付/放地/授予/消耗四口齐） ====
                case ElementPoolAddEvent add:
                    return new Line($"{Who(add.Player, isSelf)} 放地牌 {Name(add.AddedCard)}（锁色 {ManaZh(add.LockedColor)}）",
                        "log-line--card");
                case ElementPoolPayEvent pay:
                {
                    // 零费支付（2026-11-04 用户定案）：实付组合为空（0 费卡打出/效果费 0）时旧文案
                    // 「支付 ∅」不可读——改「无消耗」。判空与 DescribeCost 计费过滤同口径（>0 才计，
                    // 全零组合同归零费）；行保留——「这张免费」对玩家是有效信息。
                    bool anyPaid = pay.PaidCost != null && pay.PaidCost.Any(kv => kv.Value > 0);
                    string note = string.IsNullOrEmpty(pay.SourceNote) ? "" : $"（{NoteZh(pay.SourceNote)}）";
                    return new Line(anyPaid
                        ? $"{Who(pay.Player, isSelf)} 支付 {DescribeCost(pay.PaidCost)}{note}"
                        : $"{Who(pay.Player, isSelf)} 无消耗{note}", "");
                }
                case ElementPoolGainEvent gain:
                    return new Line($"{Who(gain.Player, isSelf)} 获得 {gain.Amount} 个{ManaZh(gain.GainedType)}临时元素（来自 {Name(gain.FromCard)}）",
                        "log-line--effect");
                case ElementPoolConsumeEvent consume:
                    return new Line($"{Who(consume.Player, isSelf)} 消耗 {consume.Amount} 个{ManaZh(consume.ManaType)}临时元素",
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
                    return DestroyLine(k, isSelf);
                case CostPaidEvent paid: // 2026-11-04：代价与效果同口执行，单起一行把因果说清
                    return string.IsNullOrEmpty(paid.Description)
                        ? new Line($"〔代价〕{Who(paid.Player, isSelf)} 支付代价", "log-line--effect")
                        : new Line($"〔代价〕{Who(paid.Player, isSelf)} 支付：{paid.Description}", "log-line--effect");
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
                    return new Line($"{Who(life.Player, isSelf)} 生命 {life.OldLife} → {life.NewLife}",
                        life.NewLife < life.OldLife ? "log-line--combat" : "log-line--effect");
                case LifeLossEvent ll:
                    return new Line($"{Who(ll.Player, isSelf)} 生命流失 {ll.Amount}", "log-line--combat");
                case FatigueEvent fatigue:
                    return new Line($"{Who(fatigue.Player, isSelf)} 疲劳，受到 {fatigue.Damage} 伤害（空牌库抽牌）",
                        "log-line--combat");
                case HealEvent heal:
                {
                    // 2026-11-01 修复：原「Source!=null → Empty（由结先行呈现）」在倒计时奖励/分支奖励上
                    // 根本没有对应的「结」行（战报里 22→24 却一行回复都没有）——治疗一律出行，来源附括号。
                    string healSrc = heal.Source is Card hc && heal.Source != heal.Target ? $"（{Name(hc)}）" : "";
                    return new Line($"〔回复〕{EntityName(heal.Target, isSelf)} 回复 {heal.Amount} 生命{healSrc}", "log-line--effect");
                }

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
                        ? new Line($"{Who(ei.Controller, isSelf)} 发动：{src}「{ei.Definition.DisplayName}」", "log-line--effect")
                        : new Line($"{src} 触发「{ei.Definition.DisplayName}」", "log-line--effect");
                }
                case StackAddEvent _: // 施放/攻击/守卫/SBA：各有专属行
                    return Empty;

                // ==== 连锁窗口叙事（2026-11-03 连锁卡牌表现退役改日志定案：询问/应答/逆处理/收尾） ====
                case ChainInquiryEvent q: // 有真实候选进入决策口才发（0 候选自动 Pass 静默）
                    // 2026-11-04 口径：主体是「被询问的一方」——旧文案「Player 1询问连锁」会被读成他去问别人
                    return new Line($"{Who(q.Holder, isSelf)} 被询问是否连锁", "log-line--system");
                case ChainRespondedEvent r:
                    return new Line($"{Who(r.Responder, isSelf)} 连锁了{r.Description}", "log-line--system");
                case ChainDeclinedEvent dec: // 询问后的让过（未询问的自动 Pass 不发）
                    // 2026-11-04 口径：与全仓「让过」术语对齐（旧文案「拒绝了连锁」会被读成拒绝了别人的连锁）
                    return new Line($"{Who(dec.Decliner, isSelf)} 让过（不连锁）", "log-line--system");
                case StackResolutionStartEvent srs when srs.TopItem != null && !srs.TopItem.IsPhaseWindow:
                    // 真连锁分叉（2026-11-04 用户定案）：结算开始时栈上真实对象 ≥2（有人跟了连锁）
                    // 才叫「连锁开始逆处理」；单件窗口（询问→让过→处理原对象）是普通处理=「开始处理」，
                    // 收尾也不出「连锁结束」。RealCount 不计阶段窗伪对象（主阶段窗下出牌非连锁）。
                    return new Line((srs.RealCount >= 2 ? "连锁开始逆处理：" : "开始处理：")
                        + StackItemZh(srs.TopItem, isSelf), "log-line--system");
                case StackEmptyEvent se when se.HadRealItems && se.WasChain: // 真连锁收尾（单件/纯阶段窗排干均不出行）
                    return new Line("连锁结束", "log-line--system");
                case HeroSkillActivatedEvent skill:
                    return new Line($"〔技能〕{Who(skill.Player, isSelf)} 发动英雄技能「{Name(skill.SkillCard)}」", "log-line--effect");
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
                                                 // || kw.Keyword == KeywordRules.BattleMage // 战斗法师散射播报 2026-11-01 退役注释（原舍身；表行/常量同批）
                                                 || kw.Keyword == "运势" // 主动运势两段式（2026-10-20）：掷骰结果须玩家可见（成败都播报；自动路径同享）
                                                 || kw.Keyword == CounterRules.DurabilityCounter: // 耐久经济（2026-11-04）：扣层/归零此前在本白名单外=战报零播报；Detail 自带原因与剩余数
                {
                    if (string.IsNullOrEmpty(kw.Detail)) return Empty;
                    string text = kw.Detail;
                    // 耐久 Detail 不带主语（「耐久 -1（战斗伤害；剩余 2）」）——补挂载单位名，
                    // 否则多结界在场分不清是谁的耐久；守护/运势 Detail 自带完整句式不补。
                    if (kw.Keyword == CounterRules.DurabilityCounter && kw.Target != null)
                        text = $"{EntityName(kw.Target, isSelf)} {text}";
                    if (isSelf != null)
                        text = text.Replace("Player 1", "【己方角色】").Replace("Player 2", "【对方角色】");
                    return new Line(text, "log-line--effect");
                }
                case KeywordAppliedEvent _:
                    return Empty; // 其余关键词由结先行覆盖（防刷屏，类注释口径）

                // ==== 信息族·预言（2026-11-02 隐秘性定案：宣言时刻类别不可见，结算时刻公开） ====
                case ProphecyRegisteredEvent reg:
                    return new Line(
                        $"{Who(reg.Declarer, isSelf)}立下预言：押注内容隐藏，对手下回合首张出牌时揭晓",
                        "log-line--effect");
                case ProphecyResolvedEvent res:
                {
                    string bet = DeclarationZh(res.Declaration);
                    string verdict = res.Hit
                        ? res.VerifiedCard != null
                            ? $"押注「{bet}」——对手首张出牌「{Name(res.VerifiedCard)}」→ 命中"
                            : $"押注「{bet}」→ 命中"
                        : res.VerifiedCard != null
                            ? $"押注「{bet}」——对手首张出牌「{Name(res.VerifiedCard)}」→ 未命中"
                            : $"押注「{bet}」——对手整回合未出牌，作未命中";
                    return new Line($"{Who(res.Declarer, isSelf)}的预言揭晓：{verdict}", "log-line--effect");
                }

                // ==== 指示物落定 ====
                case CounterChangedEvent ctr:
                {
                    if (ctr.CounterType == "*" || ctr.Amount == 0) return Empty; // "*"=换区/净化全清标记，无逐类信息
                    string name = CounterRules.Find(ctr.CounterType).DisplayName;
                    string who = EntityName(ctr.Target, isSelf);
                    // 变化后读数（2026-11-04 用户定案）：属性/耐久变化行一并给出变化后的数值——
                    // AfterNote 由发布口定格（延迟渲染不能现查），null=无附注照旧行文。
                    string tail = string.IsNullOrEmpty(ctr.AfterNote) ? "" : $"（{ctr.AfterNote}）";
                    return ctr.Amount > 0
                        ? new Line(CounterRules.IsNegative(ctr.CounterType)
                            ? $"{who} 被附加 {ctr.Amount} 层{name}{tail}"
                            : $"{who} 获得 {ctr.Amount} 层{name}{tail}", "log-line--effect")
                        : new Line($"{who} 失去 {-ctr.Amount} 层{name}{tail}", "log-line--effect");
                }

                // ==== 骨架/终局 ====
                case StackEmptyEvent:
                    return Empty; // 高频低信息
                case GameOverEvent over:
                    return isSelf != null
                        ? new Line($"★ {(over.Winner != null && isSelf(over.Winner) ? "我方" : "对手")}获胜（{GameOverReasonZh(over.Reason)}）· 共 {over.TotalTurns} 回合",
                            "log-line--system")
                        : new Line($"★ 游戏结束：胜者 {Name(over.Winner)}（{GameOverReasonZh(over.Reason)}，共 {over.TotalTurns} 回合）",
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
            // 隐蔽信息闸（2026-11-04 用户定案）：真实对战的日志/导出不得出现双方牌库与起手清单
            //（对手牌库构成/起手、己方手牌都是隐蔽区信息）——只有 AI 对战/无头验证
            //（AllowHiddenInfo=true）保留全量中立导出口径；否则退「谁先手」骨架行。
            if (!AllowHiddenInfo)
                return "━━━━ 开局 ━━━━\n" + $"{Name(e.FirstPlayer)} 先手";

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

        // ======================================== 死亡/摧毁行（2026-11-04 口径修正） ========================================

        /// <summary>
        /// 销毁行（2026-11-04 口径修正——旧文案「X 死亡（Smashed）」把三种落点糊成一句，
        /// 玩家会把「元素池里的地牌被毁」读成「场上同名生物被打死」）：
        /// ① 元素池地牌 →「地牌「X」被摧毁（送入墓地）」——地牌可为任意卡面的卡（生物/法术/结界都能放地），
        ///   名字与场上生物同名是常态，必须标出「地牌」；
        /// ② 战场无生命单位 →「结界/场地「X」被摧毁（送入墓地）」；
        /// ③ 有生命单位 →「X 死亡（原因）」。
        /// 另：代价（CostType.Payload）触发的销毁标「作为代价」——费用与效果同一执行口，
        /// 不标则玩家分不清是自己付的代价还是对手的效果。
        /// </summary>
        private static Line DestroyLine(CardDestroyEvent k, Func<Player, bool> isSelf)
        {
            var card = k.DestroyedCard;
            if (card == null) return Empty;

            string name = Name(card);
            bool nonLiving = card is IHasSupertype ht && ht.Supertype != Cardtype.Creature;

            // 区域判定优先用事件时点定格（FromZone）：战报为延迟渲染，此刻卡已在墓地区，
            // 现读 GetZone() 恒墓地。旧事件（FromZone=null）按卡型兜底——Smashed=摧毁口径
            // 只作用于地牌/无生命单位，故非无生命者即地牌。
            bool land = k.FromZone == Zone.ElementPool
                        || (k.FromZone == null && k.Reason == DestroyReason.Smashed && !nonLiving);

            string whose = OwnerPrefix(card, isSelf);
            string asCost = k.IsCost ? "作为代价" : "";

            if (land)
                return new Line($"{whose}地牌「{name}」{asCost}被摧毁（送入墓地）", "log-line--card");

            if (nonLiving)
            {
                string kind = card.IsFieldCard() ? "场地" : "结界";
                return new Line($"{whose}{kind}「{name}」{asCost}被摧毁（送入墓地）", "log-line--card");
            }

            if (k.Reason == DestroyReason.Annihilated)
                return new Line($"{whose}{name}{asCost}被湮灭（移出本局）", "log-line--combat");

            // 死因取细分档（Cause）：DestroyReason 把「伤害致死/生命流失/防御归零」全归成 Combat，
            // 直接翻成「战斗」会把效果伤害打死写成战死——战报里玩家会问「我明明没被打」。
            return new Line($"{whose}{name} 死亡（{DeathCauseZh(k.Cause, k.Reason)}{(k.IsCost ? "，代价" : "")}）",
                "log-line--combat");
        }

        /// <summary>死因中文（2026-11-04）：细分档优先（Cause），未知/缺省回落粗分类（Reason）。</summary>
        public static string DeathCauseZh(CardCore.Attribute.DeathCause cause, DestroyReason reason) => cause switch
        {
            CardCore.Attribute.DeathCause.DamageLethal => "伤害致死",
            CardCore.Attribute.DeathCause.LifeLoss => "生命流失",
            CardCore.Attribute.DeathCause.LifePayment => "生命支付",
            CardCore.Attribute.DeathCause.ZeroToughness => "防御归零",
            CardCore.Attribute.DeathCause.DestroyEffect => "效果消灭",
            CardCore.Attribute.DeathCause.Annihilate => "湮灭",
            _ => DestroyReasonZh(reason),
        };

        /// <summary>归主前缀（2026-11-04 统一口径）：中立=「Player 1 的」、观侧=「我方的/对手的」，
        /// 无主（衍生物/临时卡）=空串。销毁行与离场行共用，两处措辞不得各说各的。</summary>
        private static string OwnerPrefix(Card card, Func<Player, bool> isSelf)
        {
            var owner = card?.GetOwner() ?? card?.GetController();
            if (owner == null) return "";
            return isSelf == null ? $"{Name(owner)} 的" : (isSelf(owner) ? "我方的" : "对手的");
        }

        /// <summary>销毁原因中文（2026-11-04：旧文案直出枚举名 Smashed/Combat，玩家读不懂）。</summary>
        public static string DestroyReasonZh(DestroyReason reason) => reason switch
        {
            DestroyReason.Combat => "战斗",
            DestroyReason.Destroyed => "效果消灭",
            DestroyReason.Annihilated => "湮灭",
            DestroyReason.Smashed => "摧毁",
            DestroyReason.LegendaryRule => "同名规则",
            _ => reason.ToString(),
        };

        /// <summary>入场来源中文（2026-11-04：旧文案直出枚举名「来源 CastPlayed」）。</summary>
        public static string EnterSourceZh(EnterSource src) => src switch
        {
            EnterSource.CastPlayed => "经发动区打出",
            EnterSource.SummonedByEffect => "效果召唤",
            EnterSource.Revived => "墓地复活",
            EnterSource.TokenSpawned => "衍生物生成",
            EnterSource.ControlChange => "控制权变更",
            _ => null, // None：不写来源（衍生物/未指定）
        };

        /// <summary>终局原因中文（2026-11-04：旧文案直出 LifeZero）。</summary>
        private static string GameOverReasonZh(GameOverReason reason) => reason switch
        {
            GameOverReason.LifeZero => "生命归零",
            GameOverReason.Concede => "认输",
            GameOverReason.TimeOut => "超时",
            GameOverReason.EffectVictory => "效果宣告胜利",
            _ => reason.ToString(),
        };

        /// <summary>支付备注排版（2026-11-04）：源串由引擎拼成「打出:卡名」「充能:卡名」，UI 按 ASCII
        /// 冒号判前缀（BattleScreen 高亮），不可改源——只在渲染层换全角冒号。</summary>
        private static string NoteZh(string note) => string.IsNullOrEmpty(note) ? note : note.Replace(':', '：');

        // ======================================== 观侧辅助 ========================================

        /// <summary>栈对象描述（连锁「逆处理」行载荷——2026-11-03）：卡施放=【名】、攻击=攻击者
        /// 的攻击宣言、守卫/SBA=专名、效果=来源「效果名」（与 BattleDecisionHub.DescribeResponseSource
        /// 同款口径，日志侧独立成单——UI 那份带交互指引文案，不可复用）。</summary>
        private static string StackItemZh(EffectInstance item, Func<Player, bool> isSelf)
        {
            if (item == null) return "?";
            if (item.IsCardCast)
            {
                // cast 宿主卡：CastCard 未设时在 Source（PushCardCast 构造口径——CastCard 字段
                // 属执行上下文语境，栈对象实际恒空，勿单判）
                var host = item.CastCard ?? item.Source as Card;
                if (host != null) return $"【{Name(host)}】";
            }
            if (item.IsAttackDeclaration)
            {
                string atk = item.Source is Card ? Name(item.Source) : EntityName(item.Source, isSelf);
                return $"{atk} 的攻击宣言";
            }
            if (item.IsGuardDeclaration) return "守卫宣言";
            if (item.IsSBA) return "系统动作";
            if (item.Definition != null)
            {
                string src = item.Source is Card ? Name(item.Source) : EntityName(item.Source, isSelf);
                return $"{src}「{item.Definition.DisplayName}」";
            }
            return "效果";
        }

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

        /// <summary>预言押注中文化（"维度:值"→「类别：生物」式；未知编码原文回显）——
        /// 预言揭晓行专用（2026-11-02 隐秘性定案：宣言时刻不出行，此口只在结算时刻被走到）。</summary>
        public static string DeclarationZh(string encoded)
        {
            if (!Attribute.Handlers.ProphecyDimension.TryParse(encoded, out var dim, out var val))
                return string.IsNullOrEmpty(encoded) ? "∅" : encoded;
            if (dim == Attribute.Handlers.ProphecyDimension.Name) return "卡名：" + val;
            if (dim == Attribute.Handlers.ProphecyDimension.Type)
                return "类别：" + (val.Equals(nameof(Cardtype.Creature), StringComparison.OrdinalIgnoreCase) ? "生物"
                    : val.Equals(nameof(Cardtype.Spell), StringComparison.OrdinalIgnoreCase) ? "法术"
                    : val.Equals(nameof(Cardtype.Enchantment), StringComparison.OrdinalIgnoreCase) ? "结界" : val);
            return encoded;
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
            ManaType.Red => "火",
            ManaType.Blue => "水",
            ManaType.Green => "植物",
            ManaType.Any => "任意",
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

        /// <summary>区域中文名（区域移动行用；2026-11-04 补齐元素池/技能栏/无——旧版漏项会直出英文枚举名）。</summary>
        public static string ZoneZh(Zone zone) => zone switch
        {
            Zone.Hand => "手牌",
            Zone.Deck => "牌库",
            Zone.Graveyard => "墓地",
            Zone.Exile => "除外区",
            Zone.Battlefield => "战场",
            Zone.ElementPool => "元素池",
            Zone.FieldZone => "技能栏",
            Zone.Activation => "发动区",
            Zone.None => "无",
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
