using System;
using System.Collections.Generic;
using System.Linq;
using CardCore.Attribute;

namespace CardCore
{
    /// <summary>
    /// 战斗阶段状态
    /// </summary>
    public enum CombatPhase
    {
        /// <summary>非战斗阶段</summary>
        None,
        /// <summary>选择攻击者</summary>
        SelectAttacker,
        /// <summary>攻击宣言</summary>
        DeclareAttack,
        /// <summary>选择阻挡者</summary>
        SelectBlocker,
        /// <summary>阻挡宣言</summary>
        DeclareBlock,
        /// <summary>伤害计算</summary>
        DamageCalculation,
        /// <summary>伤害结算</summary>
        DamageDealing,
        /// <summary>战斗结束</summary>
        EndCombat
    }

    /// <summary>
    /// 战斗参与者
    /// </summary>
    public class CombatParticipant
    {
        public Entity Entity { get; set; }
        public Player Controller { get; set; }
        public bool IsAttacking { get; set; }
        public bool IsBlocking { get; set; }
        public Entity BlockedTarget { get; set; }
        public Entity BlockedBy { get; set; }
        public int AssignedDamage { get; set; }

        /// <summary>直接指定模型下宣言的攻击目标（随从或玩家）。阻挡流程的 BlockedBy 优先于此。</summary>
        public Entity DeclaredTarget { get; set; }
    }

    /// <summary>
    /// 战斗系统（2026-09-16 战斗接入栈机器定案重写）：
    /// 攻击=速度0栈对象（逐攻击开窗）、守卫=速度1响应——战斗三段在栈结算分支执行：
    /// ① 攻击目标变更（守卫结算改写攻击目标）② 战斗结算（ResolvePair）③ 死亡处理
    /// （由栈机器 SBA 轮统一接管——战斗死亡与效果致死同口径，救场窗口自然覆盖）。
    /// 旧批次模型（会话/阻挡阶段/ExecuteDamage 批结算/EndCombat 直执泵）全部退役。
        /// 横置在**战斗结算完成后**支付（2026-10-15 定案：双方以可用状态完成战斗——战斗期间
        /// 攻击者照常输出、拦截守卫照常反击，战斗结束后攻击者与守卫才横置；确定进入战斗才支付，
        /// 到点重查，宣言期零支付）。
    ///
    /// 战斗为双向结算（定案）：随从互殴双方同时互致伤害；角色（玩家）被攻击同样有反伤——
    /// 反伤力=角色攻击指示物层数（2026-10-07 弹药定案，武器系统退役；见类内"角色参战"块注释）。
    ///
    /// 关键词行为（定案；2026-09-16 攻/守效果化修订）：
    /// - 可用性统一走横置：入场即可用（2026-10-17 取消横置入场）；冲锋的登场解横置成为空操作、
    ///   突袭残留紊乱指示物一回合（期间不能以玩家为目标）；攻击/拦截/主动均以未横置为资格，
    ///   横置即上限——激励解除横置即可再动
        /// - 攻击 = 速度0主动效果（默认自带，NoAttack 卡不能攻；标准速度门 0≥计数器——连锁中不可宣）；
        ///   守卫 = 速度1响应拦截（默认自带，NoGuard 卡不能拦）：响应窗口内入栈，结算期改写攻击
        ///   目标并登记 GuardBlocker——守卫以可用状态参战照常反击，横置随攻击结算在战斗完成后支付
        /// - 警戒：已横置（自身先前动作所致）的防守者也能造成战斗伤害（见 ResolvePair）
    /// - 帷幕（原嘲讽，2026-09-13 更名）：不拦攻击（只吸引效果目标）；攻击侧目标强制由守卫拦截承担
    /// - 潜行（2026-10-08 指示物化）：不可被指定为攻击/效果目标（三指定口读潜行层数>0）；
    ///   攻击/发动效果/实际受伤各消耗 1 层（三个失效口，逐份撤）
    /// - 先攻/连击：先攻步先行结算（死者不反击）；连击两步均结算
    /// - 缴械：攻击结算时被攻击的目标无法反击（对角色目标同样生效——压制角色反伤）
        /// - 碾压（2026-10-16 改版）：战斗伤害结算时对**宣言原目标**及其左右同排相邻生物各造成一次
    ///   相同的战斗伤害——守卫拦截只拦住原目标的主战斗伤害（目标改写为守卫者），战斗仍发生在
    ///   原被攻击者那一格，溅射域恒取原目标邻格（受拦截不受影响）；攻击角色时原目标=角色（Player），
    ///   溅射域=角色格左右邻格的生物（RoleAdjacentResolver 注入，8×7 布局下角色居侧列端头各仅一格在盘）
    /// - 剧毒/吸血/系命/圣盾/护甲/坚韧：伤害经 KeywordRules.ApplyDamage 统一结算
    /// </summary>
    public class CombatSystem
    {
        /// <summary>
        /// 邻接解析扩展点（碾压）：目标随从 → 其左右同排邻格的生物。
        /// 默认 null = 碾压只打主目标；棋盘层接线时注入 BoardState 邻接查询
        /// （保持核心 ↔ 棋盘单向依赖：核心定义扩展点，表现层注入实现）。
        /// 2026-10-04 碾压语义修订：溅射域从「6 邻格随从」收窄为「左右同排生物」——
        /// 后排斜邻与地牌行的地牌不入溅射域（接线方注入 BoardState.FlankNeighbors）。
        /// 2026-10-16 改版：本口只管**随从**枢轴；攻角色（Player 目标）的溅射域走 RoleAdjacentResolver。
        /// </summary>
        public static Func<Card, IEnumerable<Card>> AdjacentResolver;

        /// <summary>
        /// 角色邻接解析扩展点（碾压攻角色，2026-10-16 改版）：防守方角色 → 角色格左右（E/W）
        /// 邻格的生物。默认 null = 攻角色无溅射；棋盘层接线注入 BoardState.FlankNeighborsOfRole
        ///（与 AdjacentResolver 同惯例成对设置/清空——BattleController / NetRoom / TideHeadlessDriver）。
        /// </summary>
        public static Func<Player, IEnumerable<Card>> RoleAdjacentResolver;

        // ======================= 角色参战（2026-10-07 弹药定案，武器系统退役） =======================
        // 战斗为双向结算：角色（玩家）被攻击同样有反伤——反伤力=GetPower(player)
        //（=HeroAttackCounter 层数，EntityEffectExtensions.GetPower Player 分支）。
        // 攻击与反击结算后烧除全部弹药（CounterRules.RemoveHeroAttackAmmo）：攻击与反击共用
        // 同一份弹药，烧完即止——弹药即闸门（主动攻击一回合一发、同回合后续受击反伤归 0），
        // 无专用次数计数器。旧武器扩展点（PlayerCounterattackPower/OnPlayerCounterattackResolved）
        // 已随武器系统退役删除。

        private ZoneManager _zoneManager;
        private LayerEngine _layerEngine;

        public CombatSystem(ZoneManager zoneManager)
        {
            _zoneManager = zoneManager;
        }

        /// <summary>
        /// 注入层引擎：战斗伤害与攻击资格按 LayerEngine 计算的当前力量结算，
        /// 使连续/静态 P/T 效果（光环、增益）在战斗中生效。
        /// </summary>
        public void AttachLayerEngine(LayerEngine layerEngine)
        {
            _layerEngine = layerEngine;
        }

        /// <summary>攻击次数上限已撤（2026-09-10 定案：横置即上限——攻击=横置代价，激励解除横置
        /// 即可再攻；AttacksThisTurn 仅作统计）。保留此口径注释供引用方追溯。</summary>

        /// <summary>检查是否可以攻击（攻击者侧资格；2026-09-16 战斗接入栈机器：会话/阶段/
        /// 记速器依赖全退役）。速度约束由 StackEngine.PushAttackDeclaration 的标准速度门承担
        /// （攻击=速度0：回合方 ≥ 计数器，即计数器为 0 才可宣——旧手写"计数器>0 拒绝"守卫
        /// 与该规则重复，已删，规则本身零变化）。主阶段+回合方校验在 GameActions 层。</summary>
        public bool CanDeclareAttack(Entity attacker, Player controller)
        {
            if (attacker == null || controller == null) return false;

            // 检查是否在战场
            if (attacker is Card card)
            {
                if (_zoneManager != null && !_zoneManager.IsCardInZone(card, controller, Zone.Battlefield))
                    return false;

                // 攻击能力（攻击效果化）：攻击=速度0主动效果（默认自带），NoAttack 卡不能宣言攻击。
                // 次数上限已撤（横置即上限）——AttacksThisTurn 只作台账统计
                if (!card.HasAttackAbility())
                    return false;
            }

            // 禁攻令（2026-10-29）：受光环影响方不能宣言攻击——单位按控制者、角色按宣言方判命中
            if (RuleAuraSystem.ScopeHits(RuleAuraComponents.NoAttackAura,
                    attacker is Card atkCard ? atkCard.GetController() : controller))
                return false;

            // 检查是否已横置（宣言期已横置不可再宣；结算期横置在 ResolveAttackDeclaration 支付）
            if (attacker.IsTapped())
                return false;

            // 检查是否有攻击力（按层引擎计算的当前力量）。角色同门（2026-10-07 弹药定案）：
            // 角色攻击力=HeroAttackCounter 层数——读数 0 即无攻击资格；攻击/反击结算后烧除全部弹药
            //（弹药即闸门——烧完读数归 0，本回合不可再宣，无需专用次数计数器）。
            if (GetPower(attacker) <= 0)
                return false;

            return true;
        }

        /// <summary>检查攻击者能否指定该目标（目标侧资格：突袭紊乱限制/潜行；帷幕不拦攻击）。
        /// 突袭定案：生效即消耗（解除横置），负面=紊乱指示物（一回合内不能以玩家为目标，攻击与效果同口径）。
        /// 2026-09-16：防守方=攻击方对手（会话字段退役，参数化）。</summary>
        public bool CanAttackTarget(Entity attacker, Entity target, Player attackingPlayer)
        {
            if (target == null || !target.IsAlive || target == attacker) return false;

            // 只能攻击防守方（对方随从或对方玩家）
            var targetController = target is Card tc ? tc.GetController() : target as Player;
            if (attackingPlayer == null || targetController != attackingPlayer.Opponent) return false;

            // 2026-10-02 三类卡型定案：卡牌攻击目标=防守方**战场上**的卡（活体生物全额落血；
            // 无生命结界经耐久管线按伤害全额扣耐久，反击力=0 天然单向）或防守方角色。
            // 非战场卡（FieldZone 英雄技能卡等）不是合法攻击目标。
            if (target is Card zc && targetController != null
                && _zoneManager != null
                && !_zoneManager.IsCardInZone(zc, targetController, Zone.Battlefield))
                return false;

            // 潜行/隐密：不可被指定为攻击目标（潜行 2026-10-08 指示物化——层数>0 即持有；
            // 2026-10-19 Entity 级——角色持潜行层同样不可被指定）
            if (target.GetCounterCount(Attribute.CounterRules.StealthCounter) > 0
                || target.HasKeyword(KeywordRules.Concealed))
                return false;

            // 突袭紊乱（负面指示物，持续一回合）：期间不准以玩家为目标——只能攻随从
            if (target is Player && KeywordRules.HasRushSickness(attacker))
                return false;

            // 帷幕（原嘲讽，2026-09-13 更名定案）：**不拦攻击**——只吸引效果目标
            // （TargetResolver.ApplyTauntRestriction）；攻击侧的目标强制由守卫拦截承担。

            return true;
        }

        // ======================= 战斗三段结算（ResolveStack 分支消费，2026-09-16 定案） =======================

        /// <summary>
        /// 守卫拦截结算（速度1响应，LIFO 先于被拦截的攻击结算）：
        /// 重检（存活/未横置——横置支付不出即落空，宣言期零支付天然不回滚）→
        /// **攻击目标变更**（战斗三段第①段）：改写被拦截攻击宣言对象的目标为守卫者、
        /// 登记 GuardBlocker。守卫**不在此横置**（2026-10-15 定案）：战斗期间以可用状态参战
        /// 照常反击，横置由攻击结算在战斗完成后统一支付（攻击落空=守卫未参战不支付）。
        /// 原阻挡阶段（SelectBlocker/DeclareBlock）退役。
        /// </summary>
        public void ResolveGuardDeclaration(EffectInstance guard)
        {
            var guarder = guard?.Source;
            var attack = guard?.InterceptedAttack;
            if (guarder == null || attack == null || !attack.IsAttackDeclaration) return;

            // 重检：窗口内被冻结抢先横置/死亡 → 拦截落空（不支付不回滚）
            if (!guarder.IsAlive || guarder.IsTapped())
            {
                return;
            }

            // 重检（2026-10-29 禁守令）：卡牌守卫者失守卫能力 / 角色守卫者被禁守光环覆盖 → 拦截落空
            if (guarder is Card gCard && !gCard.HasGuardAbility())
                return;
            if (guarder is Player gp && RuleAuraSystem.ScopeHits(RuleAuraComponents.NoGuardAura, gp))
                return;

            // 攻击目标变更：改写攻击宣言对象的目标（攻击结算时按改写后目标走 CanAttackTarget 重检）
            attack.Targets[0] = guarder;
            attack.GuardBlocker = guarder; // 多次拦截只记最后一位（最终目标）；战斗后据此补横置

            EventManager.Instance.Publish(new BlockDeclarationEvent
            {
                Blocker = guarder,
                Attacker = attack.Source,
                BlockingPlayer = guard.Controller
            });

            RuleHooks.OnTutorialGuardResolved?.Invoke(guarder as Card, attack); // 教学探针：守卫步骤完成信号
        }

        /// <summary>
        /// 攻击宣言结算（战斗三段第①②段；第③段死亡处理由栈机器 SBA 轮统一接管——
        /// 战斗死亡与效果致死同口径，救场窗口自然覆盖，旧 EndCombat 直执泵退役）：
        /// ① 重检：窗口内状态可能已变（冻结抢先横置/死亡/目标丢失）→ 落空
        ///    （CombatCancelledEvent；宣言期零支付，天然不回滚）。
        /// ② 战斗结算：ResolvePair——双方以可用状态完成战斗（2026-10-15 定案：攻击者与
        ///    拦截守卫照常输出/反击，横置不再先于战斗支付）；结算完成后支付双方横置
        ///    （攻击者+GuardBlocker 守卫——完成战斗才横置，此后才无法反击）。
        /// </summary>
        public void ResolveAttackDeclaration(EffectInstance attack)
        {
            var attacker = attack?.Source;
            var target = attack?.Targets != null && attack.Targets.Count > 0 ? attack.Targets[0] : null;
            GameActions.Crumb($"CF-attack-resolve atk={(attacker as Card)?.ID ?? (attacker as Player)?.Name} power={GetPower(attacker)} tgt={(target as Card)?.ID ?? (target as Player)?.Name} alive={attacker?.IsAlive} tapped={attacker?.IsTapped()} stackLeft={GameCore.Instance?.StackEngine?.StackSize}");

            // ① 重检（此时已含守卫改写后的目标）
            if (attacker == null || !attacker.IsAlive || attacker.IsTapped()
                || target == null || !target.IsAlive
                || !CanAttackTarget(attacker, target, attack.Controller))
            {
                GameActions.Crumb($"CF-attack-cancel atk={(attacker as Card)?.ID ?? (attacker as Player)?.Name} reason=重检失败(存活/横置/目标)");
                EventManager.Instance.Publish(new CombatCancelledEvent
                {
                    Attacker = attacker,
                    OriginalTarget = target,
                    AttackingPlayer = attack.Controller
                });
                RuleHooks.OnTutorialAttackResolved?.Invoke(attacker, target); // 教学探针：守卫步骤漏守兜底（取消路径）
                return;
            }

            // 潜行：攻击后消耗 1 层（2026-10-08 指示物化；取消/落空的攻击不计数；
            // 2026-10-19 Entity 级——角色宣言攻击后同样消耗）
            if (attacker.GetCounterCount(Attribute.CounterRules.StealthCounter) > 0)
            {
                attacker.AddCounters(Attribute.CounterRules.StealthCounter, -1);
                EventManager.Instance.Publish(new KeywordAppliedEvent
                {
                    Target = attacker,
                    Keyword = Attribute.CounterRules.StealthCounter,
                    Detail = "攻击后潜行失效（消耗 1 层）",
                    Source = attacker
                });
            }
            if (attacker is Card counted)
                counted.AttacksThisTurn++;

            // ② 战斗结算（2026-10-15 定案：双方以可用状态完成战斗——此时攻击者未横置、拦截守卫
            // 未横置，反击资格按 ResolvePair 口径自然成立——守卫照常反击）。
            // 溅射枢轴=宣言原目标（2026-10-16 碾压改版：守卫改写后的 target 只是主战斗对象）
            ResolvePair(attacker, target, attack.OriginalAttackTarget ?? target);

            // 战斗完成 → 支付横置（确定进入战斗且已完成才横置；警戒不抵扣）。拦截守卫同批
            // 补横置（GuardBlocker=最终拦截者，前序被改写者未参战不付；死者横置无观察面，照付）
            if (KeywordRules.ShouldTap(attacker))
                attacker.Tap();
            if (attack.GuardBlocker != null)
                attack.GuardBlocker.Tap();

            // 角色主动攻击结算完成 → 烧除全部攻击弹药（2026-10-07 弹药定案：与反击共用同一份；
            // 重检落空/取消路径不走此处——宣言被无效化弹药保留）
            if (attacker is Player heroAttacker)
                CounterRules.RemoveHeroAttackAmmo(heroAttacker);

            // 攻击结算完成（2026-10-07 霜蚀光环配套：取消路径不发——取消不算"攻击后"）
            EventManager.Instance.Publish(new AttackResolvedEvent
            {
                Attacker = attacker,
                Target = target,
                AttackingPlayer = attack.Controller
            });

            RuleHooks.OnTutorialAttackResolved?.Invoke(attacker, target); // 教学探针：守卫步骤漏守兜底（结算路径，目标=改写后）
        }

        /// <summary>结算一次攻击配对（含先攻/连击/碾压；剧毒吸血圣盾护甲坚韧在 KeywordRules 内）。
        /// declaredTarget=宣言原目标（碾压溅射枢轴——守卫拦截改写 target 后战斗仍发生在原被攻击者那格）。</summary>
        private void ResolvePair(Entity attacker, Entity target, Entity declaredTarget)
        {
            int attackerPower = GetPower(attacker);
            // 双向结算：随从目标按层引擎力量反击；角色（玩家）目标反伤直读 GetPower
            //（2026-10-07 弹药定案：反伤力=HeroAttackCounter 层数，武器扩展点已退役）
            int targetPower = target is Card ? GetPower(target)
                            : target is Player defender ? GetPower(defender)
                            : 0;

            bool attackerFirst = attacker.HasKeyword(KeywordRules.FirstStrike)
                                 || attacker.HasKeyword(KeywordRules.DoubleStrike);
            bool targetFirst = target.HasKeyword(KeywordRules.FirstStrike)
                               || target.HasKeyword(KeywordRules.DoubleStrike);
            bool attackerDouble = attacker.HasKeyword(KeywordRules.DoubleStrike);
            bool targetDouble = target.HasKeyword(KeywordRules.DoubleStrike);

            // 缴械（定案）：攻击结算时，被攻击的目标无法反击（先攻步/普通步的目标伤害均不结算；
            // 对角色目标同样生效——角色反伤也被缴械压制）。目标无反击力量时不播报。
            bool disarmed = attacker.HasKeyword(KeywordRules.Disarm);
            if (disarmed && targetPower > 0)
            {
                EventManager.Instance.Publish(new KeywordAppliedEvent
                {
                    Target = target,
                    Keyword = KeywordRules.Disarm,
                    Detail = "缴械：目标无法反击",
                    Source = attacker
                });
            }

            // 反击资格（2026-10-15 修订）：只有未横置的随从才能反击——反击不消耗横置（横置是
            // 攻击/发动的代价），已横置（自身先前动作所致）的随从只能挨打，警戒例外：横置仍反击。
            // 横置支付已移到战斗完成后（ResolveAttackDeclaration）：战斗期间攻击者与拦截守卫
            // 均可用——守卫照常反击（旧"守卫拦截即横置→单向受伤"口径退役）。
            // 角色目标无横置概念（恒视为未横置）——角色反伤照常。
            bool targetCanCounter = !target.IsTapped()
                                    || (target is Card vc && vc.HasKeyword(KeywordRules.Vigilance));

            // 取证（2026-10-17 守卫无反击排查）：配对结算入口全变量快照——区分 0 力/已横置/资格否三条路径
            GameActions.Crumb($"CF-pair atk={(attacker as Card)?.ID ?? (attacker as Player)?.Name} atkP={attackerPower}"
                              + $" tgt={(target as Card)?.ID ?? (target as Player)?.Name} tgtP={targetPower}"
                              + $" tgtTapped={target.IsTapped()} tgtAlive={target.IsAlive} counter={targetCanCounter} disarm={disarmed}");

            // ---- 先攻步 ----
            if (attackerFirst)
                DealCombatDamage(attacker, target, attackerPower);
            if (targetFirst && !disarmed && targetCanCounter)
            {
                DealCombatDamage(target, attacker, targetPower);
                // 角色先攻反伤同烧弹药（2026-10-07：攻击与反击共用同一份，任一反伤步结算后烧光）
                if (targetPower > 0 && target is Player counterFirst)
                    CounterRules.RemoveHeroAttackAmmo(counterFirst);
            }

            // ---- 普通步（连击者在两步各结算一次；2026-10-17 双向同时定案：反击资格按攻击伤害
            //      落下前的存活快照判定——1/1 拦 8/8 被打死也照样输出 1 点反击，先落伤害不再吞反击） ----
            bool counterEligible = !disarmed && targetCanCounter && target != attacker
                                   && target.IsAlive && (!targetFirst || targetDouble);
            if (attacker.IsAlive && (!attackerFirst || attackerDouble))
                DealCombatDamage(attacker, target, attackerPower);
            if (counterEligible)
            {
                DealCombatDamage(target, attacker, targetPower);
                // 角色（玩家）反伤结算完成 → 烧除全部攻击弹药（2026-10-07 弹药定案：烧完即止，
                // 同回合后续受击反伤力归 0；力量为 0 时无反伤不烧）。攻击已锁力量，
                // 被圣盾/护甲抵挡不退还。
                if (targetPower > 0 && target is Player counterattacking)
                    CounterRules.RemoveHeroAttackAmmo(counterattacking);
            }

            // ---- 碾压（2026-10-04 语义修订，表行锚=红3；2026-10-16 枢轴改版）：战斗伤害结算时
            //      对**宣言原目标**以及其左右同排相邻生物造成与主目标相同的战斗伤害（attackerPower，
            //      已锁力量）——守卫拦截只拦住原目标的主战斗伤害（战斗发生在原被攻击者那一格），
            //      溅射域恒取原目标邻格、受拦截不受影响；攻角色时原目标=角色（Player），溅射域=
            //      角色格左右邻格生物（RoleAdjacentResolver）；结界等非生物不入溅射域（IsLivingUnit） ----
            if (attacker.IsAlive && attacker.HasKeyword(KeywordRules.Overwhelm))
            {
                var pivot = declaredTarget ?? target; // 溅射枢轴=宣言原目标（无拦截时与 target 同一）
                IEnumerable<Card> flanks = null;
                Entity flankSide = null; // 同侧判定锚（防守方）
                if (pivot is Player role)
                {
                    if (RoleAdjacentResolver != null)
                    {
                        flanks = RoleAdjacentResolver(role);
                        flankSide = role; // 防守方=角色自己（Player 即控制者，CanAttackTarget 同口径）
                    }
                }
                else if (pivot is Card pivotCard && AdjacentResolver != null)
                {
                    flanks = AdjacentResolver(pivotCard);
                    flankSide = pivotCard.GetController();
                }
                if (flanks != null)
                {
                    foreach (var adjacent in flanks)
                    {
                        if (adjacent == null || adjacent == attacker || adjacent == pivot || !adjacent.IsAlive)
                            continue; // 枢轴自身经正常步结算，不重复吃溅射；攻击者不误伤
                        if (!adjacent.IsLivingUnit())
                            continue; // 只打生物：结界（耐久体）与地牌不吃碾压溅射
                        if (adjacent.GetController() != flankSide)
                            continue; // 只打原目标同侧（防守方）的生物
                        DealCombatDamage(attacker, adjacent, attackerPower);
                    }
                }
            }

            // 风怒关键词已删除（2026-09-03 原子表整体修正）——攻击后不再重置自己
        }

        /// <summary>获取实体的攻击力</summary>
        private int GetPower(Entity entity)
        {
            if (_layerEngine != null)
                return _layerEngine.CalculatePower(entity);
            if (entity is IHasPower hasPower)
                return hasPower.Power;
            return 0;
        }

        /// <summary>
        /// 造成战斗伤害（对实体/玩家统一）：经 KeywordRules 关键词管线结算
        /// （圣盾/护甲指示物/坚韧/吸血/系命）。事件链（DamageEvent/CombatDamageEvent/
        /// LifeChangeEvent/吸血系命）由 ApplyDamage 按统一时序发布，此处不补发。
        /// </summary>
        private void DealCombatDamage(Entity source, Entity target, int amount)
        {
            if (amount <= 0 || target == null || !target.IsAlive) return;

            // 取证（2026-10-17 反击丢失排查）：伤害进关键词管线前的入口快照
            GameActions.Crumb($"CF-dmg src={(source as Card)?.ID ?? (source as Player)?.Name} tgt={(target as Card)?.ID ?? (target as Player)?.Name} amt={amount}");

            KeywordRules.ApplyDamage(source, target, amount, true);

            // 毒刺/冰晶改写与毒素语义（层=伤害、回合末减半）已上移（梦魇已删 2026-10-13）
            // KeywordRules.ApplyDamage 战斗伤害改写口与 CounterRules.OnTurnEnd——此处不再附加处理。
        }

        // EndCombat/CancelCombat/阻挡阶段（SelectBlocker/DeclareBlock/EndBlockDeclaration/
        // EndAttackDeclaration/ExecuteDamage 批结算）已随 2026-09-16 战斗接入栈机器退役：
        // 攻击=速度0栈对象逐攻击开窗，死亡处理由栈机器 SBA 轮统一接管（见 ResolveAttackDeclaration）。
    }
}
