using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using GameBoard;
using MemoryPack;

namespace CardCore.Network
{
    /// <summary>
    /// 服务器侧 intent → 引擎分派（M1 协议定版 2026-09-10）。
    /// 上行 intent 的唯一执行口：座位 → Player、RuntimeId → Entity（NetEntityDirectory），
    /// 逐消息类型分派到 GameActions（同步 bool 契约、失败无副作用）。
    /// 拒绝语义：返回 false + error 字符串，由传输层包成 Error 帧回发（M2）。
    /// SelectResponse 不走本口——由会话层路由到 NetworkTargetSelector.Complete（见协议文档）。
    /// 动作分派口径参照 LegalActionEnumerator.Apply（RL 侧已趟通同款映射）。
    /// </summary>
    public static class NetworkIntentApplier
    {
        /// <summary>应用一条上行 intent。返回 是否被引擎接受；false 时 error 说明拒绝原因。
        /// board（v19 落格坐标权威化，可空）：对战通道传入宿主 BoardState——IntentPlayCard 的
        /// CellX/CellZ 声明期校验（己方单位格+空格）+ 声明成功即 PinCard（钉子活到 cast 结算
        /// 入场，Resync 钉优先落所选格）。null=无棋盘环境（超时代打/旧测试口）：带格 intent
        /// 的选格被忽略，服务端按 first-free 落位。</summary>
        public static bool Apply(GameCore core, int seat, NetworkMessage msg, out string error,
            BoardState board = null)
        {
            error = null;
            if (core == null) { error = "core 未初始化"; return false; }
            if (msg == null) { error = "空消息"; return false; }

            var player = NetEntityDirectory.SeatToPlayer(core, seat);
            if (player == null) { error = $"未知座位 {seat}"; return false; }

            switch (msg.Type)
            {
                case NetworkMessageType.IntentPlayCard:
                {
                    var intent = MemoryPackSerializer.Deserialize<MsgIntentPlayCard>(msg.Payload);
                    if (intent == null) { error = "IntentPlayCard 载荷损坏"; return false; }

                    var card = NetEntityDirectory.ResolveCard(core, intent.CardRuntimeId);
                    if (card == null) { error = $"卡不存在 RuntimeId={intent.CardRuntimeId}"; return false; }

                    if (!NetEntityDirectory.TryResolveAll(core, intent.Targets, out var targets))
                    { error = "目标解析失败"; return false; }

                    return ApplyPlayCardCore(core, seat, player, intent, targets, null, board, false, out error);
                }

                // IntentTapForElement 已删（2026-10-11 现取现用：地牌横置=付费时系统自动动作，
                // 无手动产色意图；效果授予走临时钱包由结算管线自动完成）

                case NetworkMessageType.IntentAddToElementPool:
                {
                    var intent = MemoryPackSerializer.Deserialize<MsgIntentAddToElementPool>(msg.Payload);
                    if (intent == null) { error = "IntentAddToElementPool 载荷损坏"; return false; }

                    var card = NetEntityDirectory.ResolveCard(core, intent.CardRuntimeId);
                    if (card == null) { error = $"卡不存在 RuntimeId={intent.CardRuntimeId}"; return false; }

                    // ModeIndex 字段复用为锁色选择（2026-10-11：-1=缺省主色，否则=ManaType 枚举值）
                    var colorChoice = intent.ModeIndex >= 0 ? (ManaType?)intent.ModeIndex : null;
                    return Ok(GameActions.AddToElementPool(core, player, card, colorChoice), out error);
                }

                case NetworkMessageType.IntentBuildCard:
                {
                    // 充能（2026-11-05 系统动作化改版，消息名/号不动只换语义）：速度0系统动作宣入
                    //（仿攻击宣言——宣言期零选色，颜色在结算期经 TargetSelectionService 反问
                    // 下发 SelectRequest 由客户端下拉选择；AI/无头自动取最大余量色）。
                    // 拒绝文案直读 CanCharge 门禁（时点/每回合规额/余量等逐项可读——Ok(bool,reason) 出口）
                    var intent = MemoryPackSerializer.Deserialize<MsgIntentBuildCard>(msg.Payload);
                    if (intent == null) { error = "IntentBuildCard 载荷损坏"; return false; }

                    var card = NetEntityDirectory.ResolveCard(core, intent.CardRuntimeId);
                    if (card == null) { error = $"卡不存在 RuntimeId={intent.CardRuntimeId}"; return false; }

                    return Ok(GameActions.DeclareCharge(core, player, card, out var chargeReason),
                        chargeReason, out error);
                }

                case NetworkMessageType.IntentSkipStandby:
                    return Ok(GameActions.SkipElementPool(core, player), out error);

                case NetworkMessageType.IntentDeclareAttack:
                {
                    var intent = MemoryPackSerializer.Deserialize<MsgIntentDeclareAttack>(msg.Payload);
                    if (intent == null) { error = "IntentDeclareAttack 载荷损坏"; return false; }

                    var attacker = NetEntityDirectory.Resolve(core, intent.Attacker);
                    var target = NetEntityDirectory.Resolve(core, intent.Target);
                    if (attacker == null || target == null) { error = "攻击者/目标解析失败"; return false; }

                    return Ok(GameActions.DeclareAttack(core, player, attacker, target, out var attackReason),
                        attackReason, out error); // 2026-11-03 失败提示细化：拒绝文案直读引擎闸门
                }

                case NetworkMessageType.IntentDeclareBlock:
                {
                    var intent = MemoryPackSerializer.Deserialize<MsgIntentDeclareBlock>(msg.Payload);
                    if (intent == null) { error = "IntentDeclareBlock 载荷损坏"; return false; }

                    var blocker = NetEntityDirectory.Resolve(core, intent.Blocker);
                    var attacker = NetEntityDirectory.Resolve(core, intent.Attacker);
                    if (blocker == null || attacker == null) { error = "格挡者/攻击者解析失败"; return false; }

                    // 2026-09-16 战斗接入栈机器：守卫=速度1响应——按攻击者寻址栈上的攻击宣言对象，
                    // PushGuardDeclaration 入栈（结算期横置+攻击目标变更）。旧阻挡阶段 API 退役。
                    var attackInstance = core.StackEngine.GetStackContents()
                        .FirstOrDefault(o => o != null && o.IsAttackDeclaration && o.Source == attacker);
                    if (attackInstance == null)
                    { error = "栈上无该攻击者的攻击宣言（响应窗口已关闭？）"; return false; }
                    if (!core.StackEngine.PushGuardDeclaration(blocker, attackInstance, player, out var guardReason))
                    { error = guardReason ?? "守卫声明不合法（速度门/资格拒绝）"; return false; } // 2026-11-03 失败提示细化：引擎真因
                    return true;
                }

                case NetworkMessageType.IntentActivateEffect:
                {
                    var intent = MemoryPackSerializer.Deserialize<MsgIntentActivateEffect>(msg.Payload);
                    if (intent == null) { error = "IntentActivateEffect 载荷损坏"; return false; }

                    var source = NetEntityDirectory.ResolveCard(core, intent.SourceCardRuntimeId);
                    if (source == null) { error = $"源卡不存在 RuntimeId={intent.SourceCardRuntimeId}"; return false; }

                    // EffectDefinition.Id 寻址（string.IsNullOrEmpty 的空 Id 回退 "EFF_{cardId}"，见 CardEffectConverter）
                    var effect = GameActions.GetCardEffectDefinitions(source)
                        .FirstOrDefault(def => def.Id == intent.EffectId);
                    if (effect == null) { error = $"源卡无该效果 EffectId={intent.EffectId}"; return false; }

                    // 动态数量（2026-11-14）：单位主动效果的联机声明期预选尚未接（UI 不发送本 intent）；
                    // 直接放行会按 base×1 少收——显式拒绝，待接线后开放。
                    if (CostDerivationService.HasDynamicAtom(effect))
                    { error = "动态数量主动效果暂不支持联机发动（无声明期预选通道）"; return false; }

                    if (!NetEntityDirectory.TryResolveAll(core, intent.Targets, out var targets))
                    { error = "目标解析失败"; return false; }

                    // 注意：ActivateEffect 的 targets 经由 PendingEffect 携带（LegalActionEnumerator.Apply 同款口径）
                    return Ok(ActivateWithTargets(core, player, effect, source, targets, intent.PaidBoost, out var effectReason),
                        effectReason, out error); // 2026-11-03 失败提示细化：拒绝文案直读引擎闸门
                }

                case NetworkMessageType.IntentActivateHeroSkill:
                {
                    var intent = MemoryPackSerializer.Deserialize<MsgIntentActivateHeroSkill>(msg.Payload);
                    if (intent == null) { error = "IntentActivateHeroSkill 载荷损坏"; return false; }

                    // 技能发动全链同步（TryActivateSkill 同步核——2026-11-03 失败提示细化）：
                    // 泵线程直调同步核，拒绝文案直读引擎闸门（时点/横置/沉默/费用/目标预检逐条可读）
                    return Ok(GameActions.ActivateHeroSkill(core, player, intent.EffectIndex, out var skillReason),
                        skillReason, out error);
                }

                case NetworkMessageType.IntentPassPriority:
                    return Ok(GameActions.PassPriority(core, player), out error);

                case NetworkMessageType.IntentEndTurn:
                    return Ok(GameActions.EndTurn(core, player, out var endTurnReason), endTurnReason, out error);

                case NetworkMessageType.IntentConcede:
                    core.EndGame(player.Opponent, GameOverReason.Concede);
                    return true;

                default:
                    error = $"消息类型 {msg.Type} 不是上行 intent（SelectResponse 须路由到选择器，非本口）";
                    return false;
            }
        }

        /// <summary>v19 落格校验：请求者己方单位格 + 当前空格。越界/非单位格/对手格/已占格
        /// 均拒（文案随 Error 帧回发，客户端可读）。占用真值来自 AutoResync 后的盘面；
        /// 同格并发声明由 Resync 的钉子"同批先到先得"收敛（后到者 first-free）。</summary>
        private static bool TryValidatePickCell(BoardState board, int seat, int x, int z, out string error)
        {
            error = null;
            if (!BoardMath.InBounds(x, z)
                || BoardLayout.RoleOf(x, z) != CellRole.Unit
                || BoardLayout.OwnerOf(x, z) != seat)
            {
                error = $"非法落格：({x},{z}) 非座位 {seat} 的己方单位格";
                return false;
            }
            if (board.CardAt(x, z) != null)
            {
                error = $"非法落格：({x},{z}) 已被占用";
                return false;
            }
            return true;
        }

        /// <summary>bool → (bool, error) 统一出口：拒绝时给可读原因。</summary>
        private static bool Ok(bool accepted, out string error)
        {
            error = accepted ? null : "引擎拒绝（GameActions 校验未通过：时点/优先权/费用/目标等门禁）";
            return accepted;
        }

        /// <summary>带精确原因的出口（2026-09-26 联机排查）：拒绝文案直读引擎门禁
        /// （费用不足/浓度上限/无合法目标/非主阶段等），不再一律糊成通用「门禁」。</summary>
        private static bool Ok(bool accepted, string reason, out string error)
        {
            error = accepted ? null : reason ?? "引擎拒绝（GameActions 校验未通过：时点/优先权/费用/目标等门禁）";
            return accepted;
        }

        /// <summary>
        /// 带目标的主动激活。ActivateEffect 公共入口无 targets 参数（目标由效果配置自动解析）；
        /// 网络侧与 LegalActionEnumerator.Apply 同款：经 PendingEffect.Create 携带预选目标。
        /// 动态数量（2026-11-14）在联机单位主动效果上暂未接预选，入口处已显式拒绝——
        /// 本口只承担「预选目标随待发入栈」的旧职责，不改动既有门禁顺序。
        /// </summary>
        private static bool ActivateWithTargets(GameCore core, Player player, EffectDefinition effect,
            Card source, List<Entity> targets, int paidBoost, out string reason)
        {
            reason = null;
            if (targets == null || targets.Count == 0)
                return GameActions.ActivateEffect(core, player, effect, source, paidBoost, out reason);

            // 有预选目标：走 PendingEffect 路径（与 RL 侧 Apply 的带目标激活口径一致）
            var pending = PendingEffect.Create(
                effect, source, player,
                core.TurnEngine.TurnPlayer,
                core.TurnEngine.CurrentPhase?.Phase ?? PhaseType.Standby,
                paidBoost: paidBoost);
            pending.SelectedTargets = targets;
            var activated = core.StackEngine.PlayerActivateVoluntary(pending);
            if (!activated) reason = "发动入栈被拒（速度门/栈结算中）";
            if (activated && effect.IsActivatedEffect && source != null && Attribute.KeywordRules.ShouldTap(source))
                source.Tap();
            return activated;
        }

        // ======================================== 声明期动态数量预选（2026-11-14） ========================================

        /// <summary>出牌前动态数量预选探测（服务器权威）：仅「法术 + 目标未预选 + 所选模式含动态数量原子」
        /// 返回 true——NetBattleChannel 先走 CollectDeclarationTargetsAsync（经 SelectRequest 问客户端），
        /// 拿到「选了几个」再声明；普通出牌不改变既有「结算期反问」流程。</summary>
        public static bool TryBuildPlayCardPreselect(GameCore core, int seat, NetworkMessage msg,
            out MsgIntentPlayCard intent, out EffectDefinition effect, out EffectExecutionContext context)
        {
            intent = null; effect = null; context = null;
            if (core == null || msg?.Type != NetworkMessageType.IntentPlayCard) return false;
            intent = MemoryPackSerializer.Deserialize<MsgIntentPlayCard>(msg.Payload);
            if (intent == null || (intent.Targets != null && intent.Targets.Length > 0)) return false;
            var player = NetEntityDirectory.SeatToPlayer(core, seat);
            if (player == null) return false;
            var card = NetEntityDirectory.ResolveCard(core, intent.CardRuntimeId);
            if (card == null || !card.IsSpellCard()) return false;
            var defs = GameActions.GetCastableEffectDefinitions(card);
            if (defs == null || defs.Count == 0) return false;
            var def = defs[Math.Clamp(intent.ModeIndex, 0, defs.Count - 1)];
            if (def == null || !CostDerivationService.HasDynamicAtom(def)) return false;
            effect = def;
            context = new EffectExecutionContext
            {
                Source = card, // 声明期卡在手（与 CollectSpellDeclarationTargetsAsync 同口径）
                Controller = player,
                ZoneManager = core.ZoneManager,
                ElementPool = core.ElementPool,
                CastCard = card,
                ModeIndex = intent.ModeIndex,
            };
            return true;
        }

        /// <summary>英雄技能发动前动态数量预选探测（同出牌口径；技能效果 Source=技能卡）。</summary>
        public static bool TryBuildHeroSkillPreselect(GameCore core, int seat, NetworkMessage msg,
            out MsgIntentActivateHeroSkill intent, out EffectDefinition effect, out EffectExecutionContext context)
        {
            intent = null; effect = null; context = null;
            if (core == null || msg?.Type != NetworkMessageType.IntentActivateHeroSkill) return false;
            intent = MemoryPackSerializer.Deserialize<MsgIntentActivateHeroSkill>(msg.Payload);
            if (intent == null) return false;
            var player = NetEntityDirectory.SeatToPlayer(core, seat);
            if (player == null) return false;
            var skillCard = HeroSkillSystem.ResolveSkillCard(core, player);
            if (skillCard == null) return false;
            var effects = HeroSkillSystem.SkillEffectsOf(skillCard);
            if (effects == null || effects.Count == 0) return false;
            var def = effects[Math.Clamp(intent.EffectIndex, 0, effects.Count - 1)];
            if (def == null || !CostDerivationService.HasDynamicAtom(def)) return false;
            effect = def;
            context = new EffectExecutionContext
            {
                Source = skillCard,
                Controller = player,
                ZoneManager = core.ZoneManager,
                ElementPool = core.ElementPool,
                ModeIndex = 0,
            };
            return true;
        }

        /// <summary>预选结果落账：出牌（声明期选完目标/数量后再走完整 PlayCard 门禁）。</summary>
        public static bool ApplyPlayCardPreselected(GameCore core, int seat, MsgIntentPlayCard intent,
            DeclarationTargets decl, BoardState board, out string error)
        {
            var player = NetEntityDirectory.SeatToPlayer(core, seat);
            if (player == null) { error = $"未知座位 {seat}"; return false; }
            return ApplyPlayCardCore(core, seat, player, intent, decl?.Targets, decl?.DynamicCounts,
                board, true, out error);
        }

        /// <summary>预选结果落账：英雄技能（计数先落原子→预检/结算 base×N）。</summary>
        public static bool ApplyHeroSkillPreselected(GameCore core, int seat, MsgIntentActivateHeroSkill intent,
            DeclarationTargets decl, out string error)
        {
            var player = NetEntityDirectory.SeatToPlayer(core, seat);
            if (player == null) { error = $"未知座位 {seat}"; return false; }
            return Ok(GameActions.ActivateHeroSkill(core, player, intent.EffectIndex,
                    decl?.Targets, decl?.DynamicCounts, out var reason),
                reason, out error);
        }

        /// <summary>出牌核心（原 IntentPlayCard 分支体）：targets/dynamicCounts 显式给——普通 intent
        /// （结算期反问，null 计数）与声明期预选（动态数量）两路共用同一门禁/钉格/响应分派。</summary>
        private static bool ApplyPlayCardCore(GameCore core, int seat, Player player, MsgIntentPlayCard intent,
            List<Entity> targets, IReadOnlyList<int> dynamicCounts, BoardState board, bool preselected,
            out string error)
        {
            error = null;
            var card = NetEntityDirectory.ResolveCard(core, intent.CardRuntimeId);
            if (card == null) { error = $"卡不存在 RuntimeId={intent.CardRuntimeId}"; return false; }

            // 动态数量（2026-11-14）：必须由服务器声明期预选收集数量——客户端直送 targets 无计数表，
            // 会按 base×1 少收；显式拒绝而非静默错价（precollected=服务端预选路径，恒可信）。
            if (!preselected)
            {
                var chosenDefs = GameActions.GetCastableEffectDefinitions(card);
                var chosenDef = chosenDefs != null && chosenDefs.Count > 0
                    ? chosenDefs[Math.Clamp(intent.ModeIndex, 0, chosenDefs.Count - 1)] : null;
                if (chosenDef != null && CostDerivationService.HasDynamicAtom(chosenDef))
                { error = "动态数量效果须在声明期由服务器收集目标（不可由客户端直送目标）"; return false; }
            }

            // 参照选位（步5 协议 v10 §3.3）：分角色上送——解析失败按无参照降级
            //（参照域空转语义由引擎承担），不整卡拒绝。
            NetEntityDirectory.TryResolveAll(core, intent.References, out var references);

            var fromZone = (Zone)intent.FromZone;

            // v19 落格校验（声明期、出牌口之前——失败零副作用）：CellX/CellZ 为**绝对坐标**，
            // 须为请求者己方单位格且当前空格（占用真值=AutoResync 后的盘面）。
            // -1=未指定（AI/法术/场地卡），服务端按 first-free 落位（兼容路径）。
            if (board != null && intent.CellX >= 0
                && !TryValidatePickCell(board, seat, intent.CellX, intent.CellZ, out error))
                return false;

            // 响应出牌分派：栈上有待响应对象、非结算中、优先权在请求者手、来源限手牌
            // （与 PlayCardInResponse 自身门禁一致——此处预分派只是让"同一个消息类型"
            //   覆盖两种出牌时点；引擎入口仍有全部校验，分派错误只会得到 false）。
            bool ok;
            string reason;
            if (fromZone == Zone.Hand
                && !core.StackEngine.IsEmpty && !core.StackEngine.IsResolving
                && core.StackEngine.CurrentPriorityHolder == player)
            {
                ok = GameActions.PlayCardInResponse(core, player, card, targets, intent.ModeIndex,
                    references, dynamicCounts, out reason);
            }
            else
            {
                ok = GameActions.PlayCard(core, player, card, targets, fromZone, intent.ModeIndex,
                    references, dynamicCounts, out reason);
            }

            // v19 声明成功即钉（本地局 PlayCardFinal 同款时点）：永久物钉玩家所选格——
            // S3b 后声明期卡留手牌，钉子活到 cast 结算入场（BoardState.Resync 钉优先）。
            // 法术不落格、场地卡由 BoardState 固定落 FieldCell（客户端不送格）；响应出牌
            // 通常为法术（CellX=-1 不进此口），但永久物响应路径同样收口，不依赖该假设。
            if (ok && board != null && intent.CellX >= 0
                && !card.IsSpellCard() && !card.IsFieldCard())
            {
                board.PinCard(card, intent.CellX, intent.CellZ);
            }
            return Ok(ok, reason, out error);
        }
    }
}
