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
        // ======================================== 响应窗口（2026-09-16 战斗接入栈机器定案） ========================================

        /// <summary>
        /// 收集响应窗口候选（发动弹窗条目）：速度门 + **双过滤**（元素费用 CanAfford 含 pending 承诺
        /// + 特殊代价 executor.CanActivate 内含 CostHandlerRegistry）——付不起的不进弹窗。
        /// 候选=0 → 调用方直接 Pass（跳过弹窗）。候选=守卫能力（速度1响应）+ 待发自愿效果 +
        /// 主动能力（回合方主阶段）+ 手牌响应卡（2026-10-16 二期窗口 UI 补接，见 ③）。
        /// </summary>
        public static List<ResponseOption> CollectAvailableResponses(GameCore core, Player p)
        {
            var options = new List<ResponseOption>();
            if (core?.StackEngine == null || p == null || core.IsGameOver) return options;
            var engine = core.StackEngine;

            // ① 守卫能力（速度1响应）：栈上有对方的攻击宣言 → 己方未横置守卫生物
            foreach (var obj in engine.GetStackContents())
            {
                if (obj == null || !obj.IsAttackDeclaration || obj.Controller == p) continue;
                if (!engine.SpeedCounter.CanActivate(StackEngine.GuardStackSpeed, EffectActivationType.Voluntary,
                        engine.IsEmpty)) continue;
                foreach (var g in core.ZoneManager.GetCards(p, Zone.Battlefield) ?? new List<Card>())
                {
                    if (g == null || !g.IsAlive || g.IsTapped() || !g.HasGuardAbility()) continue;
                    options.Add(new ResponseOption
                    {
                        Kind = ResponseOption.ResponseKind.GuardAbility,
                        SourceCard = g,
                        AttackInstance = obj,
                        Label = $"{EffectText.Name(g)} 守卫拦截（速度1·结算期横置）",
                        CostSummary = "横置",
                    });
                }
                // 角色（玩家）守卫候选（2026-10-16 战场UI三键改版定案：角色有攻击弹药时可攻可守）：
                // 守卫=把攻击目标改写为角色，双向结算吃战斗并反伤（反伤力=弹药层数）；
                // 反伤步结算后弹药全烧（ResolvePair 既有 Player 目标路径，与被打同口径）
                // 禁守令（2026-10-29）：受禁守光环影响的一方角色不进守卫候选
                if (p.GetPower() > 0 && !RuleAuraSystem.ScopeHits(RuleAuraComponents.NoGuardAura, p))
                    options.Add(new ResponseOption
                    {
                        Kind = ResponseOption.ResponseKind.GuardAbility,
                        SourcePlayer = p,
                        AttackInstance = obj,
                        Label = $"{p.Name} 角色守卫（反伤 {p.GetPower()}）",
                        CostSummary = "承受战斗",
                    });
                break; // 响应最先宣言的攻击（LIFO 顶层由后续窗口轮次覆盖）
            }

            // 教学引导闸（2026-10-06）：守卫候选过滤（此刻 options 只含守卫项，自愿效果在下方追加）
            if (options.Count > 0 && RuleHooks.TutorialGuardFilter != null)
                options = RuleHooks.TutorialGuardFilter(p, options) ?? options;

            // ② 主动效果 + 待发自愿效果（2026-10-17 连锁门禁收敛定案：连锁资格唯一条件=速度门）。
            //    主动效果此前只经待发队列瞬态（从不入队=从不进窗），现按场上来源枚举：
            //    战场+技能栏卡的 IsActivatedEffect 定义（含 Activate_Response 响应档——构筑
            //    体系注释本就写明「归响应窗口」，本次接上）。逐效果：PendingEffect.Create
            //    单源算速（BaseSpeed）→ 速度门 →
            //    executor.CanActivate 可发动性全检（结界禁主动/横置/沉默/沉睡/条件/费用/目标域
            //    ——这些是「可发动性」过滤，不是连锁条件）。阶段走 TurnEngine 真值
            //    （StackEngine.CurrentPhase 是死字段勿读）。
            var executor = engine.GetExecutor();
            var phase = core.TurnEngine.CurrentPhase?.Phase ?? PhaseType.Standby;
            foreach (var src in EnumerateActivationSources(core, p))
            {
                foreach (var def in GetCardEffectDefinitions(src))
                {
                    if (def == null || !def.IsActivatedEffect) continue;
                    var pending = PendingEffect.Create(def, src, p, engine.ActivePlayer, phase);
                    // 2026-11-01 统一口径（与③手牌整卡/PushCardCast/TryActivateEffect 同款）：
                    // 空栈首动免门槛（0 速主动效果主阶段照常）；非空栈=速≥1 且 速≥记速器
                    //（同速可接）——旧 CanActivate 全严格大于把 1 速主动效果接 1 速记速器判死，
                    // 与手牌整卡口径不一致。条件发动（非 Voluntary）不走速度（原口径保持）。
                    if (pending.ActivationType == EffectActivationType.Voluntary)
                    {
                        if (engine.IsEmpty)
                        {
                            if (engine.SpeedCounter.IsResolving) continue; // 空栈首动仍须非结算中（原 CanActivate 口径）
                        }
                        else if (pending.ActivationSpeed < 1
                                 || pending.ActivationSpeed < engine.SpeedCounter.CurrentSpeed)
                        {
                            continue; // 0 速锁自家主阶段；同速可接、更慢不行
                        }
                    }
                    if (!executor.CanActivate(def, src, p, engine.ActivePlayer, phase,
                            core.TurnEngine.TurnNumber)) continue;
                    options.Add(new ResponseOption
                    {
                        Kind = ResponseOption.ResponseKind.ActivatedAbility,
                        SourceCard = src,
                        Definition = def,
                        Label = $"{EffectText.Name(src)} 的 {def.DisplayName}（速度{pending.ActivationSpeed}）",
                    });
                }
            }
            // 待发自愿队列（触发式「可以发动」，速度门已在 GetVoluntaryEffects 内过）：
            // IsActivatedEffect 项跳过——主动效果统一走上方位枚举，防重复候选；
            // 控制器过滤（2026-10-20）：待发桶双方共享——只列决策方自己的待发，对方的
            // BRANCH_REWARD 不是本窗口可发动项
            foreach (var pe in engine.GetActivatableVoluntaryEffects(p))
            {
                var def = pe.Effect;
                if (def == null || def.IsActivatedEffect) continue;
                if (pe.Controller != p) continue;
                options.Add(new ResponseOption
                {
                    Kind = ResponseOption.ResponseKind.VoluntaryEffect,
                    Pending = pe,
                    SourceCard = pe.Source as Card,
                    Definition = def,
                    Label = $"{(pe.Source != null ? EffectText.Name(pe.Source) + " 的 " : "")}{def.DisplayName}",
                });
            }

            // ③ 手牌响应卡（2026-10-16 二期窗口 UI 补接；2026-10-18 速度门全严格大于定案）：
            //    连锁资格唯一条件=速度门（SpeedCounter.CanActivate：严格 > 记速器，同速连锁
            //    一律禁止——与 PlayCardInResponse/PushCardCast 同一裁决；本窗口恒非空栈，
            //    空栈首动豁免在此天然不触发），不再要求栈上有对手对象/攻宣锚点——旧口径
            //    攻击方在自家攻宣窗口 0 候选被自动双 Pass，攻击同帧结算、战斗诡雷永远迟到。
            //    代价（定案接受）：自己每次出牌/宣攻后，手里有**严格更快**且可付的卡就会
            //    弹「连锁吗」——让过=点「不连锁」（自己的 0 速/同速手牌不再进弹窗，含
            //    登场效果锚点场景——10-17 归属台账盲区由全严格大于口径根除）。
            //    其余过滤是「可发动性」而非连锁条件，全保留：CanPlay 钩子、同名仪典闸、
            //    代价门槛、永久物战场容量、任一模式可付+目标域有候选（付不起/指不了的不进弹窗，
            //    同"候选=0 跳过弹窗"口径）。目标不在此预选：cast 结算期经 TargetSelectionService
            //    反问（本地=Region 面板）。
            if (!engine.IsEmpty && !engine.IsResolving)
            {
                foreach (var c in core.ZoneManager.GetCards(p, Zone.Hand) ?? new List<Card>())
                {
                    if (c == null || !RuleHooks.CanPlay(core, p, c, Zone.Hand)) continue;
                    if (RuleAuraSystem.BlocksDuplicatePlay(c)) continue;
                    if (GetPayloadGateReject(core, p, c) != null) continue;
                    // 本轮窗口撤销记忆（2026-10-20 定案）：连锁内取消过声明期目标选择的卡
                    // 不再进候选——本次连锁不再询问该卡是否发动（防取消→重弹循环）
                    if (ResponseWindowService.IsDeclined(c)) continue;
                    int castSpeed = SpeedCalculator.GetCardCastSpeed(c);
                    // 2026-11-01 用户定案（蓝套 instant 前置）：**法术整卡同速可连锁**——
                    // 计费模型把法术速度定为 3−槽数（≤2 槽即 1 速以上），“不填满三个效果的法术
                    // 都应该能在对手回合发动”。旧口径走 CanActivate 的全严格大于：对手打一张
                    // 1 槽法术（速2）时，我的 1 槽法术（速2）连候选都进不了（2&gt;2 为假），
                    // 与设计意图冲突。此处只放开手牌整卡：要求 ①速≥1（0 速牌仍锁在自家主阶段）
                    // ②速≥记速器（同速可接、更慢不行）。效果发动仍走全严格大于（不变）。
                    if (castSpeed < 1 || castSpeed < engine.SpeedCounter.CurrentSpeed) continue;
                    // SBA 窗非回合方严格大于（2026-09-15 SbaStackSpeed 定案——与 PushCardCast 同口）：
                    // SBA 栈顶时非回合方候选按角色门槛剔列，防泵自动代选非法响应
                    if (engine.PeekStack()?.IsSBA == true && p != core.TurnEngine?.TurnPlayer
                        && castSpeed <= engine.SpeedCounter.CurrentSpeed) continue;
                    if (!c.IsSpellCard() && !core.ZoneManager.HasBattlefieldSpace(p)) continue;
                    var data = (c as CardWrapper)?.GetData();
                    int modes = data?.Supertype == Cardtype.Spell
                        ? Math.Max(1, data.Effects?.Count ?? 1) : 1; // 抉择模式数=法术效果数（ModeCount 同口径）
                    for (int m = 0; m < modes; m++)
                    {
                        if (!CanAffordCast(core, p, c, m)) continue;
                        if (!TargetDomainService.HasPlayableTargets(core, p, c, m)) continue;
                        options.Add(new ResponseOption
                        {
                            Kind = ResponseOption.ResponseKind.HandCard,
                            SourceCard = c,
                            ModeIndex = m,
                            Label = $"打出 {EffectText.Name(c)}（速度{castSpeed}）",
                        });
                        break; // 每卡一票（首个可行模式）；多模式细选留后续
                    }
                }
            }

            //（④ 手牌充能响应档已随 2026-11-05 充能系统动作化改版删除：充能=速度0系统动作，
            //  只能己方主阶段空栈首动宣入——0 速本就无法在对手回合响应窗口接链，旧速≥1 法术
            //  充能响应（2026-11-02 档）随「充能不再是卡上效果」一并退役。）

            return options;
        }

        /// <summary>窗口②主动效果来源枚举（2026-10-17 连锁门禁收敛）：战场+技能栏（英雄技能载体）。
        /// 手牌施放走整卡 cast 通道（③），墓地视手牌使用同走出牌口——均不是效果发动来源。</summary>
        private static IEnumerable<Card> EnumerateActivationSources(GameCore core, Player p)
        {
            foreach (var c in core.ZoneManager.GetCards(p, Zone.Battlefield) ?? new List<Card>())
                if (c != null) yield return c;
            foreach (var c in core.ZoneManager.GetCards(p, Zone.FieldZone) ?? new List<Card>())
                if (c != null) yield return c;
        }

        /// <summary>
        /// 响应窗口泵（交互式 DrainStack，2026-09-16）：循环——当前优先权方收集候选：
        /// 0 候选 → 自动 Pass（跳过弹窗）；有候选 → 人类弹窗（HumanResponder）/AI 决策/无头自动放弃；
        /// 选择 → 应用（守卫入栈/效果发动）→ 优先权翻转继续；双 Pass → 结算（await 完成整条链，
        /// 含 FinishResolution 新开窗口）→ 循环直到栈空且无待发（稳定）。
        /// </summary>
        public static async Cysharp.Threading.Tasks.UniTask SettleResponseWindowAsync(GameCore core, int maxRounds = 96)
        {
            // 并发泵闸（2026-11-03 帧泵自愈配套）：在途=别的链正在驱动本窗口（人类弹窗/AI 决策/
            // 结算中），本调用直接让路——双泵并发会双弹窗/双结算。顺序调用方（AI 回合驱动等）
            // 在让路时窗口由在途泵负责收口，语义不变；帧泵自愈口在途时本就不补发。
            if (core == null || core.ResponsePumpInFlight)
            {
                Crumb("CF-pump-skip " + (core == null ? "null-core" : "in-flight"));
                return;
            }
            core.ResponsePumpInFlight = true;
            Crumb($"CF-pump-enter stack={core?.StackEngine?.StackSize} res={core?.StackEngine?.IsResolving}");
            ResponseWindowService.ResetDeclinedWindow(); // 撤销记忆会话边界=本泵一次（一条连锁会话）
            try
            {
                for (int i = 0; i < maxRounds; i++)
                {
                    if (core == null || core.IsGameOver) return;
                    var engine = core.StackEngine;
                    if (engine.IsEmpty)
                    {
                        if (!engine.HasPendingEffects) return; // 稳定
                        engine.ProcessPendingEffects();
                        if (engine.IsEmpty) return;
                    }

                    var holder = engine.CurrentPriorityHolder;
                    if (holder == null) return;

                    ResponseOption choice = null;
                    var options = CollectAvailableResponses(core, holder);
                    Crumb($"CF-round i={i} holder={holder.Name} stack={engine.StackSize} opts={options.Count} waiting={engine.WaitingForPlayer} ctr={engine.SpeedCounter.CurrentSpeed}");
                    bool inquired = false;
                    if (options.Count > 0)
                    {
                        inquired = true;
                        // 连锁询问入日志（2026-11-03 连锁表现改日志定案）：有真实候选进入决策口才记
                        // ——0 候选自动 Pass 不询问不出行。应答三态：响应出牌=CardPlayEvent.IsResponseCast
                        // 「连锁了」、充能=ChainRespondedEvent（无既有行）、让过=ChainDeclinedEvent。
                        core.PublishEvent(new ChainInquiryEvent { Holder = holder });
                        if (holder.IsAI)
                            choice = ResponseWindowService.AiResponder?.Invoke(core, holder, options)
                                     ?? DefaultAiRespond(core, holder, options);
                        else if (ResponseWindowService.HumanResponder != null)
                        {
                            choice = await ResponseWindowService.HumanResponder(holder, options);
                            // 人类响应决策探针（连锁/让过都算——respond 引导步骤完成信号，2026-10-19）
                            RuleHooks.OnTutorialHumanResponded?.Invoke(holder);

                            // 声明期目标收集（2026-10-20 法术发动流程改案）：手牌响应卡选定后
                            // 先逐原子预选目标（可取消=撤销发动）。取消 → 该卡记入本轮窗口
                            // declined 集（本次连锁不再询问）+ 本轮视同让过；确认 → 目标随选项下发。
                            if (choice != null && choice.Kind == ResponseOption.ResponseKind.HandCard
                                && choice.SourceCard != null && choice.SourceCard.IsSpellCard())
                            {
                                var pre = await CollectSpellDeclarationTargetsAsync(choice.SourceCard, holder, choice.ModeIndex);
                                if (pre == null)
                                {
                                    ResponseWindowService.DeclineForThisWindow(choice.SourceCard);
                                    Crumb($"CF-declined card={(choice.SourceCard as IHasName)?.CardName}（声明期目标选择取消——本轮窗口不再询问）");
                                    choice = null; // 本轮让过（其余候选下轮照常收集）
                                }
                                else
                                {
                                    choice.Targets = pre.Targets;
                                    choice.DynamicCounts = pre.DynamicCounts; // 动态数量同表随响应下发
                                }
                            }
                        }
                        // 挂死兜底不在引擎侧（2026-10-17 定案）：UniTask.Delay 型超时在链孤悬时
                        // 同样不触发（两度实测），且 TideServer shim 无 Delay/WhenAny——真正的
                        // 保命层=BattleScreen.WatchdogStalePick（MonoBehaviour 帧驱动墙钟）。
                        // 均未注册（无头/训练）→ choice=null 自动 Pass（守卫不响应=训练旧行为）
                    }

                    if (choice != null)
                    {
                        bool applied = ApplyResponse(core, holder, choice);
                        Crumb($"CF-choice holder={holder.Name} kind={choice.Kind} label={choice.Label} applied={applied}");
                        if (applied)
                            continue; // 发动成功 → 优先权已翻转，继续下一轮
                    }

                    if (inquired) // 询问后的让过（让过按钮/倒计时/AI 不作主/声明期取消）——未询问的自动 Pass 不记
                        core.PublishEvent(new ChainDeclinedEvent { Decliner = holder });
                    await engine.PassPriority(holder); // 0 候选/放弃/失败 → Pass（双 Pass 触发结算并 await 完成）
                }
            }
            finally
            {
                core.ResponsePumpInFlight = false;
                Crumb("CF-pump-exit");
            }
        }

        /// <summary>应用所选响应：守卫=PushGuardDeclaration；主动=ActivateEffect；自愿效果=PlayerActivateVoluntary；
        /// 手牌卡=PlayCardInResponse（目标=声明期预选（2026-10-20 收集器下发）或 null——结算期反问单源）。
        ///（充能档已随 2026-11-05 充能系统动作化改版删除——充能=速度0系统动作，不再经响应窗。）</summary>
        private static bool ApplyResponse(GameCore core, Player p, ResponseOption opt)
        {
            if (opt == null) return false;
            switch (opt.Kind)
            {
                case ResponseOption.ResponseKind.GuardAbility:
                    return core.StackEngine.PushGuardDeclaration(opt.SourceCard, opt.AttackInstance, p);
                case ResponseOption.ResponseKind.HandCard:
                    return opt.SourceCard != null
                        && PlayCardInResponse(core, p, opt.SourceCard, opt.Targets, opt.ModeIndex,
                            null, opt.DynamicCounts, out _);
                case ResponseOption.ResponseKind.ActivatedAbility:
                    return opt.Definition != null && opt.SourceCard != null
                        && ActivateEffectInResponse(core, p, opt.Definition, opt.SourceCard);
                case ResponseOption.ResponseKind.VoluntaryEffect:
                    return core.StackEngine.PlayerActivateVoluntary(opt.Pending);
                default:
                    return false;
            }
        }

        /// <summary>内置 AI 响应启发（守卫决策，2026-11-01 二次改）：**最高攻击必拦**（盯防优先，打死了也拦），
        /// 其余攻击看「守卫够不够分」——还剩几次更大的宣言就留几张守卫，富余的才用来拦这一击；
        /// 且只在守卫扛得住（生命 &gt; 这一击）时才白拦，避免拿关键守卫换小伤害。
        /// 例：2 张 0/4 墙面对 1/3/5 三连击 → 1 放行、3 白拦、5 牺牲一张，全程只掉 1 血。
        /// 仍只保角色或带倒计时的单位（其余目标不拦）。</summary>
        private static ResponseOption DefaultAiRespond(GameCore core, Player defender, List<ResponseOption> options)
        {
            if (options == null || options.Count == 0) return null;
            var guards = options.Where(o => o.Kind == ResponseOption.ResponseKind.GuardAbility && o.SourceCard != null).ToList();
            if (guards.Count == 0) return null;

            var first = guards[0];
            var targets = first.AttackInstance?.Targets as List<Entity>;
            if (targets == null || targets.Count == 0) return null;
            var target = targets[0];
            bool face = target is Player;
            var countdownTarget = target as Card;
            if (!face && (countdownTarget == null || !HasCountdown(countdownTarget)))
                return null; // 只保角色，或保带倒计时的单位

            var source = first.AttackInstance?.Source;
            int incoming = core.LayerEngine != null && source != null
                ? core.LayerEngine.CalculatePower(source) : 0;
            if (incoming <= 0) return null;

            // 本击之外还剩几次更高攻击的宣言——更大的威胁要留守卫
            var attacker = core.TurnEngine?.TurnPlayer;
            var threats = RemainingAttackThreats(core, attacker, target);
            int bigger = threats.Count(t => t.Power > incoming);

            if (bigger <= 0)
                return PickGuard(guards, incoming, preferSurvivor: true); // 最高威胁：盯防，打死了也拦

            var survivors = guards.Where(g => g.SourceCard.GetLife() > incoming).ToList();
            // 富余守卫（> 更大威胁的数量）才白拦这一击；否则留给后面的高威胁
            return survivors.Count > bigger ? PickGuard(survivors, incoming, preferSurvivor: true) : null;
        }

        /// <summary>挑拦截者：优先「扛得住里生命最低的」（省大墙），没有能活的才牺牲生命最低的。</summary>
        private static ResponseOption PickGuard(List<ResponseOption> guards, int incoming, bool preferSurvivor)
        {
            ResponseOption best = null;
            int bestLife = int.MaxValue;
            foreach (var g in guards)
            {
                int life = g.SourceCard.GetLife();
                if (life <= incoming) continue;
                if (life < bestLife) { best = g; bestLife = life; }
            }
            if (best != null || !preferSurvivor) return best;

            int lowest = int.MaxValue;
            foreach (var g in guards)
            {
                int life = g.SourceCard.GetLife();
                if (life <= 0) continue;
                if (life < lowest) { best = g; lowest = life; }
            }
            return best;
        }

        /// <summary>威胁比=攻击力/生命（7/3 远高于 1/1；同为 1 时用攻击力决胜）。</summary>
        private static float ThreatRatio(GameCore core, Entity e)
        {
            if (e == null) return 0f;
            int p = core?.LayerEngine != null ? core.LayerEngine.CalculatePower(e) : 0;
            int l = e is Card c ? c.GetLife() : 1;
            return p / (float)Math.Max(1, l);
        }

        /// <summary>攻击者还能对同一目标宣言的攻击（攻击力, 威胁比）列表（含正在结算的当前一击；
        /// 当前攻击源可能已不在 CanDeclareAttack 口径内，调用方另行补）。</summary>
        private static List<(int Power, float Ratio)> RemainingAttackThreats(GameCore core, Player attacker, Entity target)
        {
            var result = new List<(int Power, float Ratio)>();
            if (core?.ZoneManager == null || attacker == null || target == null) return result;
            foreach (var u in core.ZoneManager.GetCards(attacker, Zone.Battlefield) ?? new List<Card>())
            {
                if (u == null || !u.IsAlive) continue;
                if (!core.CombatSystem.CanDeclareAttack(u, attacker)) continue;
                if (!core.CombatSystem.CanAttackTarget(u, target, attacker)) continue;
                int p = core.LayerEngine.CalculatePower(u);
                result.Add((p, p / (float)Math.Max(1, u.GetLife())));
            }
            return result;
        }

        /// <summary>卡面是否带倒计时引擎（扫描 Steps/AtomicEffects 的 branch；与 converter 同源）。</summary>
        private static bool HasCountdown(Card card)
        {
            var data = (card as CardWrapper)?.GetData();
            if (data?.Effects == null) return false;
            foreach (var fx in data.Effects)
            {
                if (fx == null) continue;
                if (fx.Steps != null)
                    foreach (var step in fx.Steps)
                    {
                        var b = step?.atomic?.branch;
                        if (b != null && b.settle == (int)BranchSettleKind.Engine
                            && b.engine == (int)BranchEngineKind.Countdown) return true;
                    }
                if (fx.AtomicEffects != null)
                    foreach (var a in fx.AtomicEffects)
                    {
                        var b = a?.branch;
                        if (b != null && b.settle == (int)BranchSettleKind.Engine
                            && b.engine == (int)BranchEngineKind.Countdown) return true;
                    }
            }
            return false;
        }

    }
}
