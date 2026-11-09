using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;

namespace CardCore
{
    /// <summary>
    /// 玩家可执行的操作 — 统一入口
    public static partial class GameActions
    {
        /// 主阶段：宣言攻击（2026-09-16 战斗接入栈机器定案：攻击=速度0栈对象，逐攻击开窗）。
        /// 资格（CanDeclareAttack+CanAttackTarget）→ StackEngine.PushAttackDeclaration
        /// （宣言期零支付，防守方获响应窗口——发动弹窗候选：守卫速度1/响应卡）→
        /// 双 Pass 结算（重检+横置支付→战斗三段，死亡处理走栈机器 SBA 轮）。
        /// 主阶段+回合方在此把守；速度门（0≥计数器）在 PushAttackDeclaration。
        /// </summary>
        public static bool DeclareAttack(GameCore core, Player player, Entity attacker, Entity target)
            => DeclareAttack(core, player, attacker, target, out _);

        /// <summary>带原因版（2026-11-03 失败提示细化）：拒绝文案直读闸门（UI toast / 网络 Error 帧透传），
        /// 攻击者侧/目标侧资格经 CombatSystem.TryDeclareAttack/TryAttackTarget 单源透传。</summary>
        public static bool DeclareAttack(GameCore core, Player player, Entity attacker, Entity target, out string reason)
        {
            reason = null;
            if (core == null || player == null) { reason = "内部错误：空参数"; return false; }
            if (!core.TurnEngine.CanCombatAction())
            {
                reason = core.TurnEngine.CurrentPhase?.Phase != PhaseType.Main
                    ? $"攻击只能在你的主阶段（当前：{core.TurnEngine.CurrentPhase?.Phase}）"
                    : "当前不可宣言攻击（阶段非激活态/结算中）";
                return false;
            }
            if (core.TurnEngine.TurnPlayer != player) { reason = "现在不是你的回合"; return false; }

            // 教学引导闸（2026-10-06）：先于常规资格——引导步骤语义优先（错误目标/锁定生物在此拒绝）
            var guideReject = RuleHooks.TutorialAttackGate?.Invoke(player, attacker, target);
            if (guideReject != null)
            {
                reason = $"教学引导：{guideReject}";
                TideLog.Warn($"[GameActions] 攻击被教学引导拒绝：{guideReject}");
                return false;
            }

            var combat = core.CombatSystem;
            if (!combat.TryDeclareAttack(attacker, player, out reason)) return false;
            if (!combat.TryAttackTarget(attacker, target, player, out reason)) return false;

            if (!core.StackEngine.PushAttackDeclaration(attacker, target, player))
            { reason = "攻击入栈被拒（速度门/栈结算中）"; return false; }
            RuleHooks.OnTutorialAttackDeclared?.Invoke(player, attacker, target);
            return true;
        }

        // ======================================== 回合控制 ========================================

        /// <summary>
        /// 结束回合（2026-11-01 阶段优先权窗改版；同日修订：双窗=①「结束阶段开始」（本口）+
        /// ②「主阶段开始」（GameCore.OnMainPhaseStartedWindow 订阅 PhaseStartEvent(Main)，原
        /// 「回合结束前」时点废弃））：
        /// 主阶段可结束态（相位 Active+栈空+非结算中）时先压 0 速阶段窗伪对象开「结束阶段开始」窗，
        /// 窗口排干（响应链/级联触发/SBA 轮一并收口）后才真实推进 Main→End（结束阶段系统行为照旧），
        /// 之后折返照旧走既有口（帧泵 GameLoopController/驱动器 CheckPhaseTransition）——新回合
        /// 主阶段开始时窗②自然开启。非可结束态（同帧双击/连锁未清）走引擎静默拒绝路径（行为与
        /// 旧版一致）。无候选时响应窗泵自动双 Pass——AI/无头局全链同步收口不卡死；无响应者注册
        /// （编辑器验证）时窗口自动让路，EndTurn 返回时已推进到位（行为同旧版）。
        /// </summary>
        public static bool EndTurn(GameCore core, Player player)
            => EndTurn(core, player, out _);

        /// <summary>带原因版（2026-11-03 失败提示细化）：拒绝文案直读闸门——UI 直显，替代旧
        ///「Region 是否开窗」启发式二分猜测。</summary>
        public static bool EndTurn(GameCore core, Player player, out string reason)
        {
            reason = null;
            if (core == null || player == null) { reason = "内部错误：空参数"; return false; }
            if (core.TurnEngine.TurnPlayer != player) { reason = "现在不是你的回合"; return false; }

            // 教学引导闸（2026-10-06）：引导步骤未完成时钉住回合（必须先做引导动作）
            var guideReject = RuleHooks.TutorialEndTurnGate?.Invoke(player);
            if (guideReject != null)
            {
                reason = $"教学引导：{guideReject}";
                TideLog.Warn($"[GameActions] 结束回合被教学引导拒绝：{guideReject}");
                return false;
            }

            // 阶段窗①（2026-11-01）：可结束态先开「结束阶段开始」窗，续链内完成推进。
            // Push 失败（栈非空等异常态）回落旧直进路径——EndTurn 内守卫会照常裁决。
            if (core.TurnEngine.CurrentPhase?.Phase == PhaseType.Main
                && core.TurnEngine.CurrentPhase?.State == PhaseState.Active
                && core.StackEngine.IsEmpty
                && !core.StackEngine.IsResolving
                && core.StackEngine.PushPhaseWindow(core.TurnEngine.TurnPlayer, "结束阶段开始"))
            {
                EndTurnWithPhaseWindowAsync(core, player).Forget();
                return true;
            }

            // 真发判定（2026-10-20 教学双推进修复）：TurnEngine 静默拒绝（阶段非 Main/栈未空——
            // 同帧双击、回合翻转前重入的典型态）时**不发教学探针**——否则 OnTutorialTurnEnded
            // 连发两次，连续 end 步一击双进（段七实测：换场/考验武装提前、必杀直伤提前落地）。
            if (!core.TurnEngine.EndTurn())
            {
                reason = core.StackEngine.IsEmpty
                    ? $"当前阶段不可结束回合（{core.TurnEngine.CurrentPhase?.Phase}/{core.TurnEngine.CurrentPhase?.State}）"
                    : "连锁尚未结算——暂时无法结束回合";
                TideLog.Warn("[GameActions] 结束回合被引擎拒绝（阶段已推进/连锁未结算）——不发教学探针");
                return false;
            }
            RuleHooks.OnTutorialTurnEnded?.Invoke(player);
            return true;
        }

        /// <summary>
        /// 阶段窗①「结束阶段开始」续链（2026-11-01 定案；同日修订：窗②迁「主阶段开始」，
        /// 本续链只管窗①排干+真实推进）：
        /// ① 排干窗①（含窗口内响应连锁）→ 真实推进 Main→End（真发才发教学探针，口径同旧版）。
        /// 任何一步异常（终局/引擎拒绝）即止步，不重试不弹错——状态与旧版拒绝路径一致。
        /// </summary>
        private static async Cysharp.Threading.Tasks.UniTask EndTurnWithPhaseWindowAsync(GameCore core, Player player)
        {
            // 窗①排干：响应窗泵循环到栈空且无待发（含窗口内出牌的整条连锁与级联）
            await SettleResponseWindowAsync(core);
            if (core == null || core.IsGameOver || core.StackEngine == null) return;

            // 真实推进（守卫与旧版一致：Main+Active+栈空；窗口期间外部不应推进相位，若被拒=异常态止步）
            if (!core.TurnEngine.EndTurn())
            {
                Crumb("CF-phase-window-advance-reject");
                return;
            }
            RuleHooks.OnTutorialTurnEnded?.Invoke(player);
        }

        // ======================================== 栈操作 ========================================

        /// <summary>
        /// 速度发动：玩家主动发动一个效果。
        /// 主动能力的发动代价（2026-09-08 定案；2026-10-15 深夜三类卡改版）：只有主动（Activate_*）=
        /// 发动代价 + 现付元素锚价 + 选定目标（元素费经 ElementCostPrepaid=false 由执行器结算路径扣除；
        /// CanActivate 预检可付性）；只能在自己的主要阶段以速度1使用（记速器须低于1，即栈空）。
        /// 触发式效果不走本入口的代价与付费。发动代价按宿主分域（2026-10-15 深夜改版）：
        /// 结界（战场/技能栏）= **拒绝**——结界禁主动效果，只挂自动/光环（运行时临摹/变形把主动行
        /// 塞给结界也在此拦）；生物 = 横置（恒横置，警戒不抵扣）；技能区法术（英雄技能载体）= 横置
        ///（每回合一次闸门——回合开始 Untap 重置，接替旧耐久池）。
        /// </summary>
        public static bool ActivateEffect(GameCore core, Player player, EffectDefinition effect, Card source, int paidBoost = 0)
            => ActivateEffect(core, player, effect, source, paidBoost, out _);

        /// <summary>带原因版（2026-11-03 失败提示细化）：既有 Crumb 取证分支同步升级为用户可读
        /// reason（Crumb 保留作验证器取证），闸序零变化。</summary>
        public static bool ActivateEffect(GameCore core, Player player, EffectDefinition effect, Card source, int paidBoost, out string reason)
        {
            reason = null;
            if (core == null || player == null || effect == null) { reason = "内部错误：空参数"; return false; }
            if (core.TurnEngine.TurnPlayer != player)
            {
                reason = "只能在你的回合发动主动效果";
                Crumb($"CF-activate-reject def={effect.Id} reason=非回合方 by={player?.Name}");
                return false;
            }
            if (core.TurnEngine.CurrentPhase?.Phase != PhaseType.Main)
            {
                reason = $"效果发动只能在你的主阶段（当前：{core.TurnEngine.CurrentPhase?.Phase}）";
                Crumb($"CF-activate-reject def={effect.Id} reason=非主阶段({core.TurnEngine.CurrentPhase?.Phase})");
                return false;
            }

            // 禁发令硬闸 2026-11-06 改版退役（禁自动令不拦主动发动——自动效果闸在
            // TriggerEngine.FindMatchingEffects，触发式不经网络 intent，无对应硬闸）。

            // 教学引导闸（2026-10-23 元素合成步）：资源步期间拦截玩家侧主动效果发动——
            // 防把后续步骤要用的效果提前烧掉（横置后步骤软锁）；AI 侧与响应路径不经此口
            var guideReject = RuleHooks.TutorialActivateGate?.Invoke(player, source, effect);
            if (guideReject != null)
            {
                reason = $"教学引导：{guideReject}";
                Crumb($"CF-activate-reject def={effect.Id} reason=引导闸：{guideReject}");
                return false;
            }

            // 发动代价预检（仅主动；战场/技能栏源卡，手牌/墓地施放不适用）：
            // 结界=拒发动（禁主动定案）；其余（生物/技能区法术）=未横置（横置=一回合一次闸门）。
            if (effect.IsActivatedEffect && source != null
                && (core.ZoneManager.IsCardInZone(source, source.GetController(), Zone.Battlefield)
                    || core.ZoneManager.IsCardInZone(source, source.GetController(), Zone.FieldZone)))
            {
                if (Attribute.CounterRules.UsesDurabilityEconomy(source))
                {
                    reason = "结界不能主动发动效果";
                    Crumb($"CF-activate-reject def={effect.Id} reason=结界禁主动 src={source.ID}");
                    return false; // 结界禁主动效果（2026-10-15 深夜改版）——耐久−1 通道整体退役
                }
                if (source.IsTapped())
                {
                    reason = "该卡已横置（本回合已发动过）";
                    Crumb($"CF-activate-reject def={effect.Id} reason=源已横置 src={source.ID}");
                    return false;
                }
            }

            var pending = PendingEffect.Create(
                effect,
                source,
                player,
                core.TurnEngine.TurnPlayer,
                core.TurnEngine.CurrentPhase?.Phase ?? PhaseType.Standby,
                paidBoost: paidBoost);

            var activated = core.StackEngine.PlayerActivateVoluntary(pending);
            if (!activated)
            {
                reason = $"发动入栈被拒（速度门/栈结算中，记速器 {core.StackEngine.SpeedCounter.CurrentSpeed}）";
                Crumb($"CF-activate-reject def={effect.Id} reason=速度门或入栈失败 ctr={core.StackEngine.SpeedCounter.CurrentSpeed} stack={core.StackEngine.StackSize}");
            }

            // 发动成功 → 消耗发动代价（仅主动；固定代价，不发不扣）：横置（恒横置，警戒不抵扣）
            //——生物每回合一次；技能区法术=英雄技能每回合一次（回合开始 Untap 重置）。
            if (activated && effect.IsActivatedEffect && source != null)
            {
                if (Attribute.KeywordRules.ShouldTap(source))
                    source.Tap();
            }

            if (activated)
                RuleHooks.OnTutorialEffectActivated?.Invoke(player, source); // 教学探针（2026-10-23 activate 步完成信号）

            return activated;
        }

        /// <summary>
        /// 响应窗口内发动主动效果（2026-10-17 连锁门禁收敛定案）：与 ActivateEffect 同构，
        /// 差异=不设「回合方+主阶段」闸（那是非响应场景的时点语义，不是连锁条件——防守方凭
        /// 速度 &gt; 记速器在攻宣窗口连锁 2 速主动效果正是本口要放行的场景），改设栈上下文守卫
        /// （有对象可响应且不在结算中——泵上下文天然满足，防御性保留）。
        /// 速度门权威仍在 StackEngine.TryActivateEffect（回合方 ≥ / 非回合方严格大于）。
        /// 发动代价同口径：结界禁主动；源横置=每回合一次闸；发动成功横置（ShouldTap）。
        /// </summary>
        public static bool ActivateEffectInResponse(GameCore core, Player player, EffectDefinition effect, Card source)
            => ActivateEffectInResponse(core, player, effect, source, out _);

        /// <summary>带原因版（2026-11-03 失败提示细化）：Crumb 取证保留，同步升级 reason。</summary>
        public static bool ActivateEffectInResponse(GameCore core, Player player, EffectDefinition effect, Card source, out string reason)
        {
            reason = null;
            if (core == null || player == null || effect == null || source == null) { reason = "内部错误：空参数"; return false; }
            if (core.StackEngine.IsEmpty || core.StackEngine.IsResolving)
            {
                reason = "响应窗口已关闭（栈空或结算中）";
                Crumb($"CF-activate-reject def={effect.Id} reason=响应窗口已关闭（栈空或结算中）");
                return false;
            }

            // 禁发令硬闸 2026-11-06 改版退役（禁自动令不拦主动发动，与 ActivateEffect 同口径）。

            if (effect.IsActivatedEffect)
            {
                if (Attribute.CounterRules.UsesDurabilityEconomy(source))
                {
                    reason = "结界不能主动发动效果";
                    Crumb($"CF-activate-reject def={effect.Id} reason=结界禁主动 src={source.ID}");
                    return false;
                }
                if (source.IsTapped())
                {
                    reason = "该卡已横置（本回合已发动过）";
                    Crumb($"CF-activate-reject def={effect.Id} reason=源已横置 src={source.ID}");
                    return false;
                }
            }

            var pending = PendingEffect.Create(
                effect,
                source,
                player,
                core.TurnEngine.TurnPlayer,
                core.TurnEngine.CurrentPhase?.Phase ?? PhaseType.Standby);

            var activated = core.StackEngine.PlayerActivateVoluntary(pending);
            if (!activated)
            {
                reason = $"发动入栈被拒（速度门/栈结算中，记速器 {core.StackEngine.SpeedCounter.CurrentSpeed}）";
                Crumb($"CF-activate-reject def={effect.Id} reason=速度门或入栈失败 ctr={core.StackEngine.SpeedCounter.CurrentSpeed} stack={core.StackEngine.StackSize}");
                return false;
            }

            // 发动成功 → 消耗发动代价（与 ActivateEffect 同口径：横置=每回合一次闸）
            if (effect.IsActivatedEffect && Attribute.KeywordRules.ShouldTap(source))
                source.Tap();

            return true;
        }

        /// <summary>
        /// 玩家 Pass 优先权
        /// </summary>
        public static bool PassPriority(GameCore core, Player player)
        {
            if (core == null || player == null) return false;
            if (core.StackEngine.CurrentPriorityHolder != player) return false;

            // Pass 可能触发结算，结算含异步原子效果（await UI）。
            // 本入口返回校验结果，结算在后台推进（StackEngine.IsResolving 阻止主循环重入）。
            ForgetSafeAsync(core.StackEngine.PassPriority(player), "PassPriority 优先权让过", core).Forget();
            return true;
        }

        /// <summary>
        /// fire-and-forget 安全包装（2026-11-05 收口）：栈/响应窗结算泵后台推进的异常不再被
        /// UniTask.Forget 静默吞（否则以 UnobservedTaskException 形态漏出、对局日志无因可查）——
        /// 捕获后 TideLog.Error（带栈顶卡名/效果名定位来源）并继续。不改变结算顺序与控制流：
        /// 异步链在 catch 处截断，与裸 Forget 同语义，只是多一条错误日志。
        /// </summary>
        internal static async Cysharp.Threading.Tasks.UniTask ForgetSafeAsync(
            Cysharp.Threading.Tasks.UniTask task, string scope, GameCore core)
        {
            try { await task; }
            catch (Exception ex)
            {
                TideLog.Error($"[结算] {scope} 后台链异常（{DescribeStackTop(core)}）：{ex}");
            }
        }

        /// <summary>异常定位用：栈顶条目的卡名/效果名快照（栈空=标注空栈）。</summary>
        private static string DescribeStackTop(GameCore core)
        {
            var top = core?.StackEngine?.GetStackContents()?.LastOrDefault(e => e != null);
            if (top == null) return "栈空";
            var cardName = (top.Source as IHasName)?.CardName ?? (top.CastCard as IHasName)?.CardName;
            var effName = top.Definition?.DisplayName ?? top.Definition?.Id;
            return $"卡[{cardName ?? top.Source?.ToString() ?? "?"}] 效果[{effName ?? "?"}]";
        }

    }
}
