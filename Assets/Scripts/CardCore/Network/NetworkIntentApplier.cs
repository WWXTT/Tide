using System;
using System.Linq;
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
                    if (fromZone == Zone.Hand
                        && !core.StackEngine.IsEmpty && !core.StackEngine.IsResolving
                        && core.StackEngine.CurrentPriorityHolder == player)
                    {
                        return Ok(GameActions.PlayCardInResponse(core, player, card, targets, intent.ModeIndex), out error);
                    }

                    return Ok(GameActions.PlayCard(core, player, card, targets, fromZone, intent.ModeIndex), out error);
                }

                case NetworkMessageType.IntentTapForElement:
                {
                    var intent = MemoryPackSerializer.Deserialize<MsgIntentTapForElement>(msg.Payload);
                    if (intent == null) { error = "IntentTapForElement 载荷损坏"; return false; }

                    // PooledCard 是元素池的包装器（非 Card 子类）：按源卡 RuntimeId 反查包装
                    var pooled = core.ElementPool.GetPooledCards(player)
                        .FirstOrDefault(pc => pc.SourceCard != null && pc.SourceCard.RuntimeId == intent.CardRuntimeId);
                    if (pooled == null) { error = $"地牌不在元素池 RuntimeId={intent.CardRuntimeId}"; return false; }

                    return Ok(GameActions.GainElementFromToken(core, player, pooled, (ManaType)intent.ManaType), out error);
                }

                case NetworkMessageType.IntentAddToElementPool:
                {
                    var intent = MemoryPackSerializer.Deserialize<MsgIntentAddToElementPool>(msg.Payload);
                    if (intent == null) { error = "IntentAddToElementPool 载荷损坏"; return false; }

                    var card = NetEntityDirectory.ResolveCard(core, intent.CardRuntimeId);
                    if (card == null) { error = $"卡不存在 RuntimeId={intent.CardRuntimeId}"; return false; }

                    return Ok(GameActions.AddToElementPool(core, player, card), out error);
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

                    // 预留路径：引擎 API 存在但全工程零调用。格挡窗口期声明，返回 void——
                    // 接受即入列（CanBlock 预检先行；EndBlockDeclaration 收口属 M2 会话编排）。
                    if (!core.CombatSystem.CanBlock(blocker, attacker, player))
                    { error = "格挡声明不合法（CanBlock 拒绝）"; return false; }
                    core.CombatSystem.DeclareBlock(blocker, attacker);
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

        /// <summary>
        /// 带目标的启动式激活。ActivateEffect 公共入口无 targets 参数（目标由效果配置自动解析）；
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
