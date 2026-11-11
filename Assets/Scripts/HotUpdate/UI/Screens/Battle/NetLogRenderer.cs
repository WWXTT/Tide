using System;
using System.Collections.Generic;
using CardCore;
using CardCore.Attribute;
using CardCore.Network;

namespace SynergyUI
{
    /// <summary>
    /// 联机战报行渲染器（2026-11-11 联机日志改版）：NetEvent（服务端 schema-less 投影）→
    /// 行文本 + 分类色。行文案与 class 对齐木桩口径 MatchLogRenderer（同一批
    /// log-line--turn/phase/combat/effect/card/system），观侧文案（我方/对手、【己方/对方角色】）
    /// 按 viewerSeat 判座位；卡名按 CardId 查 CardCatalog（对手自建卡经 TransientNetContent
    /// 覆盖层同表可见；实例 ID 带 #N 后缀由 GetByInstanceId 剥除）。
    ///
    /// 与木桩的差异=投影数据面限制（服务端协议不动，渲染层不可补救）：
    /// - 付费明细 PaidCost（Dictionary）/回合末指示物汇总 Parts（List&lt;string&gt;）投影 Opaque
    ///   （ToString 无意义）——支付行降级「支付了费用（来源注）」，回合末汇总行不出；
    /// - StackAddEvent.AddedObject / StackResolutionStartEvent.TopItem（EffectInstance）投影
    ///   Opaque——效果发动行不出（卡施放/攻击/守卫/充能各有专属行），连锁首行降级为
    ///   「连锁开始逆处理」（不带栈顶对象名，RealCount 分档与本地判据等价）；
    /// - EffectExecutionSummaryEvent.Description 线上去文本（NetProjectionIgnore 定案）——
    ///   按 EffectId 查效果短名出「　→ 效果名」，真实数值由生命/属性/指示物/治疗细粒度行承载；
    /// - HeroSkillActivatedEvent 的 Player/SkillCard 是公共字段非属性（投影器只反射属性，
    ///   Params 恒空）——技能发动行不出（如需补齐须字段改属性，属协议数据面变更另行拍板）；
    /// - 隐藏区引用（对方手牌/双方牌库——NetEventSeatFilter 置 Null）：涉及卡名降级「未知卡」。
    /// </summary>
    internal static class NetLogRenderer
    {
        /// <summary>渲染结果：文本 + 语义 class（同 MatchLogRenderer.Line 口径）+
        /// FirstDrawOfTurn（回合自然抽标记——UI 侧挂至〔准备阶段〕行后，同本地时序整理）。</summary>
        public struct Line
        {
            public string Text;
            public string Class;
            public bool FirstDraw;
            public Line(string text, string cls, bool firstDraw = false) { Text = text; Class = cls; FirstDraw = firstDraw; }
        }

        private static Line Empty => new Line(null, null);

        // class 常量（与 BattleLogController.Append 色映射、MatchLogRenderer 同一套）
        private const string ClsTurn = "log-line--turn";
        private const string ClsPhase = "log-line--phase";
        private const string ClsCombat = "log-line--combat";
        private const string ClsEffect = "log-line--effect";
        private const string ClsCard = "log-line--card";
        private const string ClsSystem = "log-line--system";

        /// <summary>唯一映射主入口（对齐 MatchLogRenderer.Render(e, isSelf) 的观侧口径）。</summary>
        public static Line Render(NetEvent e, int viewerSeat)
        {
            if (e == null) return Empty;
            switch (e.EventType)
            {
                // ==== 回合/阶段骨架（逐字同木桩） ====
                case nameof(TurnStartEvent):
                    return new Line(
                        $"—— 回合 {Int(e, nameof(TurnStartEvent.TurnNumber))}：{Who(Entity(e, nameof(TurnStartEvent.TurnPlayer)), viewerSeat)} ——",
                        ClsTurn);
                case nameof(StandbyResetEvent):
                {
                    var parts = new List<string>(4);
                    int lands = Int(e, nameof(StandbyResetEvent.UntappedLands));
                    int units = Int(e, nameof(StandbyResetEvent.UntappedUnits));
                    if (lands > 0) parts.Add($"地牌 {lands} 张解横置");
                    if (units > 0) parts.Add($"单位 {units} 个解横置");
                    if (Bool(e, nameof(StandbyResetEvent.SkillReset))) parts.Add("技能就绪");
                    if (Bool(e, nameof(StandbyResetEvent.LandCapAdvanced)))
                        parts.Add($"地牌槽上限→{Int(e, nameof(StandbyResetEvent.LandCap))}");
                    string body = parts.Count == 0 ? "无变化" : string.Join("、", parts);
                    return new Line($"〔准备阶段〕{Who(Entity(e, nameof(StandbyResetEvent.Player)), viewerSeat)} 重置：{body}", ClsPhase);
                }
                case nameof(StandbySkippedEvent):
                    return new Line($"〔准备阶段〕{Who(Entity(e, nameof(StandbySkippedEvent.Player)), viewerSeat)} 跳过（无重置/抽牌）", ClsPhase);
                case nameof(PhaseStartEvent):
                {
                    // 准备阶段行由 StandbyResetEvent 承载（引擎发布序=自动化先于相位事件，同木桩注释口径）
                    var phase = EnumOf<PhaseType>(e, nameof(PhaseStartEvent.Phase));
                    if (phase == PhaseType.Standby) return Empty;
                    return new Line($"〔{MatchLogRenderer.PhaseZh(phase)}阶段〕", ClsPhase);
                }

                // ==== 抽牌/获得（可见性原则：抽牌只报数量——DrawnCard 引用对观侧隐藏，行文本就不用） ====
                case nameof(CardDrawEvent):
                    return new Line(
                        $"{Who(Entity(e, nameof(CardDrawEvent.Player)), viewerSeat)} 抽 {Math.Max(1, Int(e, nameof(CardDrawEvent.DrawCount)))} 张",
                        "", Bool(e, nameof(CardDrawEvent.FirstDrawOfTurn)));
                case nameof(CardEnterHandEvent):
                    if (Bool(e, nameof(CardEnterHandEvent.IsDraw))) return Empty;
                    return new Line(
                        $"{Who(Entity(e, nameof(CardEnterHandEvent.Player)), viewerSeat)} 获得 {CardName(Entity(e, nameof(CardEnterHandEvent.Card)))}" +
                        $"（来自 {MatchLogRenderer.ZoneZh(EnumOf<Zone>(e, nameof(CardEnterHandEvent.FromZone)))}）",
                        ClsEffect);

                // ==== 打出/入场/离场 ====
                case nameof(CardPlayEvent):
                    return Bool(e, nameof(CardPlayEvent.IsResponseCast))
                        ? new Line($"{Who(Entity(e, nameof(CardPlayEvent.Player)), viewerSeat)} 连锁了 {CardName(Entity(e, nameof(CardPlayEvent.PlayedCard)))}", ClsCard)
                        : new Line(
                            $"{Who(Entity(e, nameof(CardPlayEvent.Player)), viewerSeat)} 使用 {CardName(Entity(e, nameof(CardPlayEvent.PlayedCard)))}" +
                            (EnumOf<Zone>(e, nameof(CardPlayEvent.FromZone)) == Zone.Graveyard ? "（从墓地）" : ""),
                            ClsCard);
                case nameof(CardActivationFailedEvent):
                    return Entity(e, nameof(CardActivationFailedEvent.Card)) == null ? Empty
                        : new Line($"〔发动失败〕{Who(Entity(e, nameof(CardActivationFailedEvent.Controller)), viewerSeat)} 的 " +
                                   $"{CardName(Entity(e, nameof(CardActivationFailedEvent.Card)))}：{Str(e, nameof(CardActivationFailedEvent.Reason))}",
                            ClsEffect);
                case nameof(CardPutToBattlefieldEvent):
                {
                    string src = MatchLogRenderer.EnterSourceZh(EnumOf<EnterSource>(e, nameof(CardPutToBattlefieldEvent.Source)));
                    string tail = src != null ? $"，来源 {src}" : "";
                    return new Line(
                        $"{CardName(Entity(e, nameof(CardPutToBattlefieldEvent.Card)))} 入场（" +
                        $"{(Bool(e, nameof(CardPutToBattlefieldEvent.Tapped)) ? "横置" : "可用")}{tail}）", ClsCard);
                }
                case nameof(CardLeaveBattlefieldEvent):
                    return new Line($"{OwnerPrefix(Entity(e, nameof(CardLeaveBattlefieldEvent.Card)), viewerSeat)}" +
                                    $"{CardName(Entity(e, nameof(CardLeaveBattlefieldEvent.Card)))} 离场", ClsCard);
                case nameof(CardZoneChangeEvent):
                    return Empty; // 去重（专门行已呈现——同木桩）

                // ==== 区域移动全景（弃置/磨牌/除外/回手/洗回；隐藏区引用降级「未知卡」） ====
                case nameof(CardDiscardEvent):
                    return new Line($"{Who(Entity(e, nameof(CardDiscardEvent.Player)), viewerSeat)} 弃置 {CardName(Entity(e, nameof(CardDiscardEvent.Card)))}", ClsCard);
                case nameof(CardMillEvent):
                    return new Line($"{Who(Entity(e, nameof(CardMillEvent.Player)), viewerSeat)} 磨牌 {CardName(Entity(e, nameof(CardMillEvent.Card)))}（牌库顶入墓）", ClsCard);
                case nameof(CardExileEvent):
                    return new Line($"{CardName(Entity(e, nameof(CardExileEvent.Card)))} 被除外（来自 " +
                                    $"{MatchLogRenderer.ZoneZh(EnumOf<Zone>(e, nameof(CardExileEvent.FromZone)))}）", ClsCard);
                case nameof(CardReturnToHandEvent):
                    return new Line($"{CardName(Entity(e, nameof(CardReturnToHandEvent.Card)))} 回到手牌", ClsCard);
                case nameof(CardShuffleIntoDeckEvent):
                    return new Line($"{CardName(Entity(e, nameof(CardShuffleIntoDeckEvent.Card)))} 洗入牌库", ClsCard);
                case nameof(TokenCreatedEvent):
                    return new Line($"{Who(Entity(e, nameof(TokenCreatedEvent.Controller)), viewerSeat)} 生成衍生物 " +
                                    $"{CardName(Entity(e, nameof(TokenCreatedEvent.Card)))}（{MatchLogRenderer.ZoneZh(EnumOf<Zone>(e, nameof(TokenCreatedEvent.DropZone)))}）", ClsCard);

                // ==== 横置/重置 ====
                case nameof(TapEvent):
                    return new Line($"{EntityName(Entity(e, nameof(TapEvent.TappedEntity)), viewerSeat)} " +
                                    $"{(Bool(e, nameof(TapEvent.IsUntapping)) ? "重置" : "横置")}", "");
                case nameof(UntapEvent):
                    if (Bool(e, nameof(UntapEvent.IsTurnStartReset))) return Empty; // 由 StandbyResetEvent 汇总承载
                    return new Line($"{EntityName(Entity(e, nameof(UntapEvent.UntappedEntity)), viewerSeat)} 重置", "");

                // ==== 钱包（支付明细 Opaque——降级「支付了费用（来源注）」，因果保留） ====
                case nameof(ElementPoolAddEvent):
                    return new Line($"{Who(Entity(e, nameof(ElementPoolAddEvent.Player)), viewerSeat)} 放地牌 " +
                                    $"{CardName(Entity(e, nameof(ElementPoolAddEvent.AddedCard)))}（锁色 " +
                                    $"{MatchLogRenderer.ManaZh(EnumOf<ManaType>(e, nameof(ElementPoolAddEvent.LockedColor)))}）", ClsCard);
                case nameof(ElementPoolPayEvent):
                {
                    string note = Str(e, nameof(ElementPoolPayEvent.SourceNote));
                    note = string.IsNullOrEmpty(note) ? "" : $"（{NoteZh(note)}）";
                    return new Line($"{Who(Entity(e, nameof(ElementPoolPayEvent.Player)), viewerSeat)} 支付了费用{note}", "");
                }
                case nameof(ElementPoolGainEvent):
                    return new Line($"{Who(Entity(e, nameof(ElementPoolGainEvent.Player)), viewerSeat)} 获得 " +
                                    $"{Int(e, nameof(ElementPoolGainEvent.Amount))} 个" +
                                    $"{MatchLogRenderer.ManaZh(EnumOf<ManaType>(e, nameof(ElementPoolGainEvent.GainedType)))}临时元素（来自 " +
                                    $"{CardName(Entity(e, nameof(ElementPoolGainEvent.FromCard)))}）", ClsEffect);
                case nameof(ElementPoolConsumeEvent):
                    return new Line($"{Who(Entity(e, nameof(ElementPoolConsumeEvent.Player)), viewerSeat)} 消耗 " +
                                    $"{Int(e, nameof(ElementPoolConsumeEvent.Amount))} 个" +
                                    $"{MatchLogRenderer.ManaZh(EnumOf<ManaType>(e, nameof(ElementPoolConsumeEvent.ManaType)))}临时元素", ClsEffect);

                // ==== 战斗 ====
                case nameof(AttackDeclarationEvent):
                    return new Line($"〔攻击〕{EntityName(Entity(e, nameof(AttackDeclarationEvent.Attacker)), viewerSeat)} → " +
                                    $"{EntityName(Entity(e, nameof(AttackDeclarationEvent.Target)), viewerSeat)}", ClsCombat);
                case nameof(BlockDeclarationEvent):
                    return new Line($"〔守卫〕{EntityName(Entity(e, nameof(BlockDeclarationEvent.Blocker)), viewerSeat)} 拦截 " +
                                    $"{EntityName(Entity(e, nameof(BlockDeclarationEvent.Attacker)), viewerSeat)}", ClsCombat);
                case nameof(CombatCancelledEvent):
                    return new Line($"〔攻击取消〕{EntityName(Entity(e, nameof(CombatCancelledEvent.Attacker)), viewerSeat)} 的攻击被取消", ClsCombat);
                case nameof(CombatDamageEvent):
                    return new Line($"{EntityName(Entity(e, nameof(CombatDamageEvent.Attacker)), viewerSeat)} → " +
                                    $"{EntityName(Entity(e, nameof(CombatDamageEvent.Defender)), viewerSeat)}：{Int(e, nameof(CombatDamageEvent.Damage))} 伤害", ClsCombat);
                case nameof(CardDestroyEvent):
                    return DestroyLine(e, viewerSeat);
                case nameof(CostPaidEvent):
                {
                    string desc = Str(e, nameof(CostPaidEvent.Description));
                    return string.IsNullOrEmpty(desc)
                        ? new Line($"〔代价〕{Who(Entity(e, nameof(CostPaidEvent.Player)), viewerSeat)} 支付代价", ClsEffect)
                        : new Line($"〔代价〕{Who(Entity(e, nameof(CostPaidEvent.Player)), viewerSeat)} 支付：{desc}", ClsEffect);
                }
                case nameof(EffectFlightEvent):
                {
                    string note = Str(e, nameof(EffectFlightEvent.Note));
                    return new Line(
                        $"{EntityName(Entity(e, nameof(EffectFlightEvent.Source)), viewerSeat)} 的 " +
                        $"{CardName(Entity(e, nameof(EffectFlightEvent.CastCard)))} → " +
                        $"{EntityName(Entity(e, nameof(EffectFlightEvent.Target)), viewerSeat)}：" +
                        $"{(string.IsNullOrEmpty(note) ? "效果不适用" : note)}", ClsEffect);
                }

                // ==== 生命 ====
                case nameof(LifeChangeEvent):
                {
                    int oldLife = Int(e, nameof(LifeChangeEvent.OldLife));
                    int newLife = Int(e, nameof(LifeChangeEvent.NewLife));
                    return new Line($"{Who(Entity(e, nameof(LifeChangeEvent.Player)), viewerSeat)} 生命 {oldLife} → {newLife}",
                        newLife < oldLife ? ClsCombat : ClsEffect);
                }
                case nameof(LifeLossEvent):
                    return new Line($"{Who(Entity(e, nameof(LifeLossEvent.Player)), viewerSeat)} 生命流失 {Int(e, nameof(LifeLossEvent.Amount))}", ClsCombat);
                case nameof(FatigueEvent):
                    return new Line($"{Who(Entity(e, nameof(FatigueEvent.Player)), viewerSeat)} 疲劳，受到 {Int(e, nameof(FatigueEvent.Damage))} 伤害（空牌库抽牌）", ClsCombat);
                case nameof(HealEvent):
                {
                    var target = Entity(e, nameof(HealEvent.Target));
                    var source = Entity(e, nameof(HealEvent.Source));
                    string healSrc = source != null && !source.IsPlayer && target != null && source.RuntimeId != target.RuntimeId
                        ? $"（{CardName(source)}）" : "";
                    return new Line($"〔回复〕{EntityName(target, viewerSeat)} 回复 {Int(e, nameof(HealEvent.Amount))} 生命{healSrc}", ClsEffect);
                }

                // ==== 状态改写 ====
                case nameof(StatModifyEvent):
                    return new Line($"{EntityName(Entity(e, nameof(StatModifyEvent.Target)), viewerSeat)} {StatZh(EnumOf<StatType>(e, nameof(StatModifyEvent.StatType)))} " +
                                    $"{Int(e, nameof(StatModifyEvent.OldValue))}→{Int(e, nameof(StatModifyEvent.NewValue))}（{(Int(e, nameof(StatModifyEvent.Delta)) >= 0 ? "+" : "")}{Int(e, nameof(StatModifyEvent.Delta))}）",
                        ClsEffect);
                case nameof(StatSetEvent):
                    return new Line($"{EntityName(Entity(e, nameof(StatSetEvent.Target)), viewerSeat)} {StatZh(EnumOf<StatType>(e, nameof(StatSetEvent.StatType)))} " +
                                    $"{Int(e, nameof(StatSetEvent.OldValue))}→{Int(e, nameof(StatSetEvent.NewValue))}（设为）", ClsEffect);
                case nameof(FreezeEvent):
                    return new Line($"{EntityName(Entity(e, nameof(FreezeEvent.Target)), viewerSeat)} 被冻结", ClsEffect);

                // ==== 连锁窗口叙事 ====
                case nameof(ChainInquiryEvent):
                    return new Line($"{Who(Entity(e, nameof(ChainInquiryEvent.Holder)), viewerSeat)} 被询问是否连锁", ClsSystem);
                case nameof(ChainRespondedEvent):
                    return new Line($"{Who(Entity(e, nameof(ChainRespondedEvent.Responder)), viewerSeat)} 连锁了{Str(e, nameof(ChainRespondedEvent.Description))}", ClsSystem);
                case nameof(ChainDeclinedEvent):
                    return new Line($"{Who(Entity(e, nameof(ChainDeclinedEvent.Decliner)), viewerSeat)} 让过（不连锁）", ClsSystem);
                case nameof(StackResolutionStartEvent):
                {
                    // RealCount 分档同本地判据（≥2=真连锁；0=纯阶段窗不出行）；TopItem 投影 Opaque
                    // 带不了栈顶对象名——降级为不带对象的首行（判据等价性见类注释）。
                    int real = Int(e, nameof(StackResolutionStartEvent.RealCount));
                    if (real == 0) return Empty;
                    return new Line(real >= 2 ? "连锁开始逆处理" : "开始处理", ClsSystem);
                }
                case nameof(StackEmptyEvent):
                    return Bool(e, nameof(StackEmptyEvent.HadRealItems)) && Bool(e, nameof(StackEmptyEvent.WasChain))
                        ? new Line("连锁结束", ClsSystem)
                        : Empty;

                // ==== 效果结算（Description 线上去文本——按 EffectId 查短名；数值由细粒度行承载） ====
                case nameof(EffectExecutionSummaryEvent):
                {
                    string effectId = Str(e, nameof(EffectExecutionSummaryEvent.EffectId));
                    if (string.IsNullOrEmpty(effectId)) return Empty;
                    return new Line("　→ " + BattleView.EffectShortName(effectId), ClsEffect);
                }

                // ==== 关键词白名单（同木桩：守护/运势/耐久；其余由效果行覆盖防刷屏） ====
                case nameof(KeywordAppliedEvent):
                {
                    string kw = Str(e, nameof(KeywordAppliedEvent.Keyword));
                    if (kw != KeywordRules.Guardian && kw != "运势" && kw != CounterRules.DurabilityCounter)
                        return Empty;
                    string text = Str(e, nameof(KeywordAppliedEvent.Detail));
                    if (string.IsNullOrEmpty(text)) return Empty;
                    // 耐久 Detail 不带主语——补挂载单位名（同木桩口径）
                    if (kw == CounterRules.DurabilityCounter)
                    {
                        var target = Entity(e, nameof(KeywordAppliedEvent.Target));
                        if (target != null) text = $"{EntityName(target, viewerSeat)} {text}";
                    }
                    text = text.Replace("Player 1", "【己方角色】").Replace("Player 2", "【对方角色】");
                    return new Line(text, ClsEffect);
                }

                // ==== 预言 ====
                case nameof(ProphecyRegisteredEvent):
                    return new Line($"{Who(Entity(e, nameof(ProphecyRegisteredEvent.Declarer)), viewerSeat)}立下预言：押注内容隐藏，对手下回合首张出牌时揭晓", ClsEffect);
                case nameof(ProphecyResolvedEvent):
                {
                    string bet = MatchLogRenderer.DeclarationZh(Str(e, nameof(ProphecyResolvedEvent.Declaration)));
                    bool hit = Bool(e, nameof(ProphecyResolvedEvent.Hit));
                    var verified = Entity(e, nameof(ProphecyResolvedEvent.VerifiedCard));
                    string verdict = hit
                        ? verified != null
                            ? $"押注「{bet}」——对手首张出牌「{CardName(verified)}」→ 命中"
                            : $"押注「{bet}」→ 命中"
                        : verified != null
                            ? $"押注「{bet}」——对手首张出牌「{CardName(verified)}」→ 未命中"
                            : $"押注「{bet}」——对手整回合未出牌，作未命中";
                    return new Line($"{Who(Entity(e, nameof(ProphecyResolvedEvent.Declarer)), viewerSeat)}的预言揭晓：{verdict}", ClsEffect);
                }

                // ==== 充能 ====
                case nameof(CardChargedEvent):
                {
                    var color = EnumOf<ManaType>(e, nameof(CardChargedEvent.Color));
                    string cid = CounterRules.CostDownCounterFor(color);
                    string cname = cid != null ? CounterRules.Find(cid).DisplayName : "费用降低";
                    return new Line($"{Who(Entity(e, nameof(CardChargedEvent.Player)), viewerSeat)}为【" +
                                    $"{CardName(Entity(e, nameof(CardChargedEvent.Card)))}】充能{MatchLogRenderer.ManaZh(color)}" +
                                    $"（{cname} {Int(e, nameof(CardChargedEvent.PaidOnColor))} 层）", ClsEffect);
                }

                // ==== 指示物落定（CounterRules 本地表可查——同木桩文案） ====
                case nameof(CounterChangedEvent):
                {
                    string type = Str(e, nameof(CounterChangedEvent.CounterType));
                    int amount = Int(e, nameof(CounterChangedEvent.Amount));
                    if (type == "*" || amount == 0) return Empty;
                    string name = CounterRules.Find(type).DisplayName;
                    string who = EntityName(Entity(e, nameof(CounterChangedEvent.Target)), viewerSeat);
                    string tail = NoteTail(Str(e, nameof(CounterChangedEvent.AfterNote)));
                    return amount > 0
                        ? new Line(CounterRules.IsNegative(type)
                            ? $"{who} 被附加 {amount} 层{name}{tail}"
                            : $"{who} 获得 {amount} 层{name}{tail}", ClsEffect)
                        : new Line($"{who} 失去 {-amount} 层{name}{tail}", ClsEffect);
                }

                // ==== 终局 ====
                case nameof(GameOverEvent):
                {
                    var winner = Entity(e, nameof(GameOverEvent.Winner));
                    return new Line($"★ {(winner != null && winner.Seat == viewerSeat ? "我方" : "对手")}获胜（" +
                                    $"{GameOverReasonZh(EnumOf<GameOverReason>(e, nameof(GameOverEvent.Reason)))}）· 共 " +
                                    $"{Int(e, nameof(GameOverEvent.TotalTurns))} 回合", ClsSystem);
                }

                default:
                    return Empty; // 未映射事件不出行（DamageEvent/AttackResolved/CardZoneChange 等
                                  // 木桩亦不出行；HeroSkillActivatedEvent 因字段非属性投影空——见类注释）
            }
        }

        // ======================================== 销毁行（口径同 MatchLogRenderer.DestroyLine，数据源换投影） =======

        private static Line DestroyLine(NetEvent e, int viewerSeat)
        {
            var card = Entity(e, nameof(CardDestroyEvent.DestroyedCard));
            if (card == null) return Empty; // 引用被隐藏过滤（牌库内被毁等）或缺失

            string name = CardName(card);
            var data = string.IsNullOrEmpty(card.CardId) ? null : CardCatalog.GetByInstanceId(card.CardId);
            bool nonLiving = data != null && data.Supertype != Cardtype.Creature;

            // FromZone 可空枚举：投影 Kind=Null（无值）或 Enum（有值）——EnumOf 缺省回 None 后按本地兜底
            bool hasFromZone = Param(e, nameof(CardDestroyEvent.FromZone))?.Kind == NetParamKind.Enum;
            var fromZone = EnumOf<Zone>(e, nameof(CardDestroyEvent.FromZone));
            var reason = EnumOf<DestroyReason>(e, nameof(CardDestroyEvent.Reason));
            bool land = fromZone == Zone.ElementPool
                        || (!hasFromZone && reason == DestroyReason.Smashed && !nonLiving);

            string whose = OwnerPrefix(card, viewerSeat);
            string asCost = Bool(e, nameof(CardDestroyEvent.IsCost)) ? "作为代价" : "";

            if (land)
                return new Line($"{whose}地牌「{name}」{asCost}被摧毁（送入墓地）", ClsCard);

            if (nonLiving)
            {
                string kind = data != null && data.IsField ? "场地" : "结界";
                return new Line($"{whose}{kind}「{name}」{asCost}被摧毁（送入墓地）", ClsCard);
            }

            if (reason == DestroyReason.Annihilated)
                return new Line($"{whose}{name}{asCost}被湮灭（移出本局）", ClsCombat);

            return new Line($"{whose}{name} 死亡（{MatchLogRenderer.DeathCauseZh(EnumOf<CardCore.Attribute.DeathCause>(e, nameof(CardDestroyEvent.Cause)), reason)}" +
                            $"{(Bool(e, nameof(CardDestroyEvent.IsCost)) ? "，代价" : "")}）", ClsCombat);
        }

        // ======================================== 观侧辅助（座位版——对齐 MatchLogRenderer 的 Player 谓词版） =======

        /// <summary>玩家称谓：我方/对手（Seat 对 viewerSeat；非玩家引用/座位未知="?"——防御）。</summary>
        private static string Who(NetEntityRef r, int viewerSeat)
            => r != null && r.IsPlayer ? (r.Seat == viewerSeat ? "我方" : "对手") : "?";

        /// <summary>实体称谓：角色=【己方/对方角色】；卡=卡名（不带 #RuntimeId 后缀——日志口径，
        /// 区别于选择列表的 EntityTextOf）。</summary>
        private static string EntityName(NetEntityRef r, int viewerSeat)
            => r != null && r.IsPlayer ? (r.Seat == viewerSeat ? "【己方角色】" : "【对方角色】") : CardName(r);

        /// <summary>归主前缀：引用 Seat=出队时点的控制者座位（FromCard 投影口径）——我方的/对手的；
        /// 无主（衍生物/座位未知）=空串。</summary>
        private static string OwnerPrefix(NetEntityRef r, int viewerSeat)
            => r != null && !r.IsPlayer && r.Seat >= 0 ? (r.Seat == viewerSeat ? "我方的" : "对手的") : "";

        /// <summary>卡名：CardId 查表（TransientNetContent 覆盖层同表）；查无回退实例 ID 原文
        /// （对齐 EffectText.Name 的 c.ID 兜底）；引用缺失（隐藏区过滤/字段空）=「未知卡」。</summary>
        private static string CardName(NetEntityRef r)
        {
            if (r == null || r.IsPlayer) return "未知卡";
            if (string.IsNullOrEmpty(r.CardId)) return "未知卡";
            var data = CardCatalog.GetByInstanceId(r.CardId);
            return data != null && !string.IsNullOrEmpty(data.CardName) ? data.CardName : r.CardId;
        }

        /// <summary>支付备注全角化（同 MatchLogRenderer.NoteZh——源串 ASCII 冒号是 UI 高亮判据不可改源）。</summary>
        private static string NoteZh(string note) => string.IsNullOrEmpty(note) ? note : note.Replace(':', '：');

        /// <summary>变化后读数附注括注（AfterNote 空串/缺省=无附注）。</summary>
        private static string NoteTail(string note) => string.IsNullOrEmpty(note) ? "" : $"（{note}）";

        /// <summary>属性中文名（MatchLogRenderer.StatZh 为 private——此处同款三行）。</summary>
        private static string StatZh(StatType stat) => stat switch
        {
            StatType.Power => "攻击",
            StatType.Life => "生命",
            StatType.Both => "属性",
            _ => stat.ToString(),
        };

        /// <summary>终局原因中文（MatchLogRenderer.GameOverReasonZh 为 private——此处同款）。</summary>
        private static string GameOverReasonZh(GameOverReason reason) => reason switch
        {
            GameOverReason.LifeZero => "生命归零",
            GameOverReason.Concede => "认输",
            GameOverReason.TimeOut => "超时",
            GameOverReason.EffectVictory => "效果宣告胜利",
            _ => reason.ToString(),
        };

        // ======================================== 投影参数读取助手（schema-less 字段直取） =======

        /// <summary>按字段名取参数（null=字段不在或值为显式 Null）。</summary>
        public static NetParam Param(NetEvent e, string field)
        {
            if (e?.Params == null) return null;
            foreach (var p in e.Params)
                if (p != null && p.FieldName == field) return p;
            return null;
        }

        /// <summary>实体引用（Kind=Entity 首个；被隐藏过滤的字段 Kind=Null → 返回 null）。</summary>
        public static NetEntityRef Entity(NetEvent e, string field)
        {
            var refs = Param(e, field)?.EntityRefs;
            return refs != null && refs.Length > 0 ? refs[0] : null;
        }

        /// <summary>int 数值字段（投影 IntValue 是 long——事件数值字段全是 int 值域，收窄安全）。</summary>
        public static int Int(NetEvent e, string field) => (int)(Param(e, field)?.IntValue ?? 0);

        public static bool Bool(NetEvent e, string field) => (Param(e, field)?.IntValue ?? 0) != 0;

        public static string Str(NetEvent e, string field) => Param(e, field)?.StringValue;

        /// <summary>枚举解析：StringValue（枚举名）优先、IntValue 兜底，缺省回 default。</summary>
        public static T EnumOf<T>(NetEvent e, string field) where T : struct, Enum
        {
            var p = Param(e, field);
            if (p == null) return default;
            if (!string.IsNullOrEmpty(p.StringValue) && Enum.TryParse<T>(p.StringValue, out var byName)) return byName;
            if (Enum.IsDefined(typeof(T), p.IntValue)) return (T)Enum.ToObject(typeof(T), p.IntValue);
            return default;
        }
    }
}
