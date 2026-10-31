using System;
using System.Linq;
using Cysharp.Threading.Tasks;
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
        /// <summary>应用一条上行 intent。返回 是否被引擎接受；false 时 error 说明拒绝原因。</summary>
        public static bool Apply(GameCore core, int seat, NetworkMessage msg, out string error)
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

                    var fromZone = (Zone)intent.FromZone;

                    // 响应出牌分派：栈上有待响应对象、非结算中、优先权在请求者手、来源限手牌
                    // （与 PlayCardInResponse 自身门禁一致——此处预分派只是让"同一个消息类型"
                    //   覆盖两种出牌时点；引擎入口仍有全部校验，分派错误只会得到 false）。
                    // AwakenCast（2026-10-29 P6）：苏醒施放仅主阶段 PlayCard 分支——响应路径无苏醒变体。
                    if (fromZone == Zone.Hand
                        && !core.StackEngine.IsEmpty && !core.StackEngine.IsResolving
                        && core.StackEngine.CurrentPriorityHolder == player)
                    {
                        return Ok(GameActions.PlayCardInResponse(core, player, card, targets, intent.ModeIndex, out var respReason),
                            respReason, out error);
                    }

                    return Ok(GameActions.PlayCard(core, player, card, targets, fromZone, intent.ModeIndex, intent.AwakenCast, out var reason),
                        reason, out error);
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
                    // 充能（2026-10-31 P12：建造→充能，消息名/号不动只换语义）：卡+色 → ActivateCharge
                    //（速度 0 主动效果入栈，结算 ChargeHandler 扣元素+放层+展示）。
                    // 拒绝文案直读 CanCharge 门禁（上限/色资格/元素余量等逐项可读——Ok(bool,reason) 出口）
                    var intent = MemoryPackSerializer.Deserialize<MsgIntentBuildCard>(msg.Payload);
                    if (intent == null) { error = "IntentBuildCard 载荷损坏"; return false; }

                    var card = NetEntityDirectory.ResolveCard(core, intent.CardRuntimeId);
                    if (card == null) { error = $"卡不存在 RuntimeId={intent.CardRuntimeId}"; return false; }

                    return Ok(GameActions.ActivateCharge(core, player, card, (ManaType)intent.Color, out var chargeReason),
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

                    return Ok(GameActions.DeclareAttack(core, player, attacker, target), out error);
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
                    if (!core.StackEngine.PushGuardDeclaration(blocker, attackInstance, player))
                    { error = "守卫声明不合法（速度门/资格拒绝）"; return false; }
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

                    if (!NetEntityDirectory.TryResolveAll(core, intent.Targets, out var targets))
                    { error = "目标解析失败"; return false; }

                    // 注意：ActivateEffect 的 targets 经由 PendingEffect 携带（LegalActionEnumerator.Apply 同款口径）
                    return Ok(ActivateWithTargets(core, player, effect, source, targets, intent.PaidBoost), out error);
                }

                case NetworkMessageType.IntentActivateHeroSkill:
                {
                    var intent = MemoryPackSerializer.Deserialize<MsgIntentActivateHeroSkill>(msg.Payload);
                    if (intent == null) { error = "IntentActivateHeroSkill 载荷损坏"; return false; }

                    // ActivateAsync 是伪异步（全同步守卫 + 同步 ActivateEffect，末尾 CompletedTask——
                    // 2026-10-21 补课时核实）：泵线程同步取结果即可。防御分支：未来真异步化（如内部
                    // 反问化）时按已受理放行，失败经事件流/快照可见，不阻塞泵线程。
                    var task = GameActions.ActivateHeroSkill(core, player, intent.EffectIndex);
                    if (task.Status == UniTaskStatus.Succeeded)
                        return Ok(task.GetAwaiter().GetResult(), "技能发动被拒（时点/每回合横置闸/沉默/费用/目标预检）", out error);
                    return true;
                }

                case NetworkMessageType.IntentPassPriority:
                    return Ok(GameActions.PassPriority(core, player), out error);

                case NetworkMessageType.IntentEndTurn:
                    return Ok(GameActions.EndTurn(core, player), out error);

                case NetworkMessageType.IntentConcede:
                    core.EndGame(player.Opponent, GameOverReason.Concede);
                    return true;

                default:
                    error = $"消息类型 {msg.Type} 不是上行 intent（SelectResponse 须路由到选择器，非本口）";
                    return false;
            }
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
        /// </summary>
        private static bool ActivateWithTargets(GameCore core, Player player, EffectDefinition effect,
            Card source, System.Collections.Generic.List<Entity> targets, int paidBoost)
        {
            if (targets == null || targets.Count == 0)
                return GameActions.ActivateEffect(core, player, effect, source, paidBoost);

            // 有预选目标：走 PendingEffect 路径（与 RL 侧 Apply 的带目标激活口径一致）
            var pending = PendingEffect.Create(
                effect, source, player,
                core.TurnEngine.TurnPlayer,
                core.TurnEngine.CurrentPhase?.Phase ?? PhaseType.Standby,
                paidBoost: paidBoost);
            pending.SelectedTargets = targets;
            var activated = core.StackEngine.PlayerActivateVoluntary(pending);
            if (activated && effect.IsActivatedEffect && source != null && Attribute.KeywordRules.ShouldTap(source))
                source.Tap();
            return activated;
        }
    }
}
