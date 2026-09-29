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
    /// 横置在**结算时**支付（确定进入战斗才横置——到点重查，宣言期零支付）。
    ///
    /// 战斗为双向结算（定案）：随从互殴双方同时互致伤害；角色（玩家）被攻击同样有反伤——
    /// 反伤力量与耐久消耗走武器系统扩展点（见 PlayerCounterattackPower，系统未实现前角色反伤为 0）。
    ///
    /// 关键词行为（定案；2026-09-16 攻/守效果化修订）：
    /// - 可用性统一走横置：随从一律横置入场；冲锋/突袭一次性生效 = 解除横置 + 消耗关键词
    ///   （突袭残留紊乱指示物一回合，期间不能以玩家为目标）；攻击/拦截/启动式均以未横置为资格，
    ///   横置即上限——激励解除横置即可再动
    /// - 攻击 = 速度0主动效果（默认自带，NoAttack 卡不能攻；标准速度门 0≥计数器——连锁中不可宣）；
    ///   守卫 = 速度1响应拦截（默认自带，NoGuard 卡不能拦）：响应窗口内入栈，
    ///   结算期横置 → 结算按横置单向受伤（被动代价不对称：攻击方强制竖直参战）
    /// - 警戒：横置也能造成战斗伤害（横置目标持警戒仍反击，见 ResolvePair）
    /// - 帷幕（原嘲讽，2026-09-13 更名）：不拦攻击（只吸引效果目标）；攻击侧目标强制由守卫拦截承担
    /// - 潜行：不可被指定为攻击目标；攻击后移除（发动效果后的移除在效果执行器）
    /// - 先攻/连击：先攻步先行结算（死者不反击）；连击两步均结算
    /// - 缴械：攻击结算时被攻击的目标无法反击（对角色目标同样生效——压制武器反伤）
    /// - 碾压：对目标相邻 1 格随从各结算一次攻击
    /// - 剧毒/吸血/系命/圣盾/护甲/坚韧：伤害经 KeywordRules.ApplyDamage 统一结算
    /// </summary>
    public class CombatSystem
    {
        /// <summary>
        /// 邻接解析扩展点（碾压）：目标随从 → 其相邻 1 格内的随从。
        /// 默认 null = 碾压只打主目标；棋盘层接线时注入 BoardState 邻接查询
        /// （保持核心 ↔ 棋盘单向依赖：核心定义扩展点，表现层注入实现）。
        /// </summary>
        public static Func<Card, IEnumerable<Card>> AdjacentResolver;

        // ======================= 武器系统扩展点（定案预留，系统未实现） =======================
        // 【武器系统 TODO】战斗为双向结算：角色（玩家）被攻击时同样有反伤——
        //   反伤力量 = 当前装备武器的攻击力；每次反伤结算消耗 1 点武器耐久，耐久归零武器销毁。
        // 接线方式（同 AdjacentResolver 惯例：核心定义扩展点，武器系统注入实现）：
        //   PlayerCounterattackPower —— 查询玩家反伤力量（无武器/扩展点未接线 = 0，不反伤）；
        //   OnPlayerCounterattackResolved —— 反伤结算完成回调（武器系统在此扣 1 耐久）。
        // 在武器系统落地前，角色反伤为 0（当前对局播报中打脸为单方面伤害即此原因）。

        /// <summary>角色反伤力量查询（武器系统注入：返回装备武器攻击力；null = 无武器不反伤）。</summary>
        public static Func<Player, int> PlayerCounterattackPower;

        /// <summary>角色反伤结算完成回调（武器系统注入：消耗 1 点武器耐久）。</summary>
        public static Action<Player> OnPlayerCounterattackResolved;

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

            // 检查是否已横置（宣言期已横置不可再宣；结算期横置在 ResolveAttackDeclaration 支付）
            if (attacker.IsTapped())
                return false;

            // 检查是否有攻击力（按层引擎计算的当前力量）
            if (attacker is IHasPower && GetPower(attacker) <= 0)
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

            // 潜行：不可被指定为攻击目标
            if (target is Card sc && sc.HasKeyword(KeywordRules.Stealth))
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
        /// 重检（存活/未横置——支付不出即落空，宣言期零支付天然不回滚）→ 支付横置 →
        /// **攻击目标变更**（战斗三段第①段）：改写被拦截攻击宣言对象的目标为守卫者。
        /// 原阻挡阶段（SelectBlocker/DeclareBlock）退役。
        /// </summary>
        public void ResolveGuardDeclaration(EffectInstance guard)
        {
            var guarder = guard?.Source;
            var attack = guard?.InterceptedAttack;
            if (guarder == null || attack == null || !attack.IsAttackDeclaration) return;

            // 重检+支付：窗口内被冻结抢先横置/死亡 → 拦截落空（不支付不回滚）
            if (!guarder.IsAlive || guarder.IsTapped())
            {
                UnityEngine.Debug.Log($"[CMBTDBG] 守卫拦截落空：守卫者不可用（{(guarder is Card gc ? gc.ID : "玩家")}）");
                return;
            }
            guarder.Tap(); // 守卫拦截横置自身（被动代价不对称：攻击/守卫均在结算期支付）

            // 攻击目标变更：改写攻击宣言对象的目标（攻击结算时按改写后目标走 CanAttackTarget 重检）
            attack.Targets[0] = guarder;
            UnityEngine.Debug.Log($"[CMBTDBG] 守卫拦截：{(guarder is Card g ? g.ID : "玩家")} 改写攻击目标");

            EventManager.Instance.Publish(new BlockDeclarationEvent
            {
                Blocker = guarder,
                Attacker = attack.Source,
                BlockingPlayer = guard.Controller
            });
        }

        /// <summary>
        /// 攻击宣言结算（战斗三段第①②段；第③段死亡处理由栈机器 SBA 轮统一接管——
        /// 战斗死亡与效果致死同口径，救场窗口自然覆盖，旧 EndCombat 直执泵退役）：
        /// ① 重检+支付：窗口内状态可能已变（冻结抢先横置/死亡/目标丢失）→ 落空
        ///    （CombatCancelledEvent；宣言期零支付，天然不回滚）；重检通过 → **支付横置**
        ///    （确定进入战斗才横置——与旧"宣言即横置"的差异点）。
        /// ② 战斗结算：ResolvePair——**显式特判：攻击方支付横置后强制按竖直参战**
        ///    （横置不削输出、不失去参战资格）；防守方/守卫横置 → 不反击（双方横置=攻击单向伤害）。
        /// </summary>
        public void ResolveAttackDeclaration(EffectInstance attack)
        {
            var attacker = attack?.Source;
            var target = attack?.Targets != null && attack.Targets.Count > 0 ? attack.Targets[0] : null;

            // ① 重检（此时已含守卫改写后的目标）
            if (attacker == null || !attacker.IsAlive || attacker.IsTapped()
                || target == null || !target.IsAlive
                || !CanAttackTarget(attacker, target, attack.Controller))
            {
                UnityEngine.Debug.Log($"[CMBTDBG] 攻击落空：重检失败 攻击者={(attacker is Card ac ? ac.ID : "玩家")} 目标={(target is Card tc ? tc.ID : "玩家")}");
                EventManager.Instance.Publish(new CombatCancelledEvent
                {
                    Attacker = attacker,
                    OriginalTarget = target,
                    AttackingPlayer = attack.Controller
                });
                return;
            }

            // 支付横置（确定进入战斗才横置；警戒不抵扣）
            if (KeywordRules.ShouldTap(attacker))
                attacker.Tap();

            // 潜行：攻击后移除；台账 +1（取消/落空的攻击不计数）
            if (attacker is Card attackerCard && attackerCard.HasKeyword(KeywordRules.Stealth))
            {
                attackerCard.RemoveKeyword(KeywordRules.Stealth);
                EventManager.Instance.Publish(new KeywordAppliedEvent
                {
                    Target = attackerCard,
                    Keyword = KeywordRules.Stealth,
                    Detail = "攻击后潜行失效",
                    Source = attacker
                });
            }
            if (attacker is Card counted)
                counted.AttacksThisTurn++;

            // ② 战斗结算（攻击方强制竖直参战特判在 ResolvePair：横置不查攻击方、不削输出）
            ResolvePair(attacker, target);
        }

        /// <summary>结算一次攻击配对（含先攻/连击/碾压；剧毒吸血圣盾护甲坚韧在 KeywordRules 内）</summary>
        private void ResolvePair(Entity attacker, Entity target)
        {
            int attackerPower = GetPower(attacker);
            UnityEngine.Debug.Log($"[CMBTDBG] 结算配对：攻击者={(attacker is Card ac ? ac.ID : "玩家")} 攻击力={attackerPower} 目标={(target is Card tc ? tc.ID : "玩家")} 攻击者先手={attacker.HasKeyword(KeywordRules.FirstStrike) || attacker.HasKeyword(KeywordRules.DoubleStrike)}");
            // 双向结算：随从目标按层引擎力量反击；角色（玩家）目标反伤走武器扩展点
            //（无武器/未接线 = 0，不反伤——见类头武器系统 TODO 注释）
            int targetPower = target is Card ? GetPower(target)
                            : target is Player defender ? PlayerCounterattackPower?.Invoke(defender) ?? 0
                            : 0;

            bool attackerFirst = attacker.HasKeyword(KeywordRules.FirstStrike)
                                 || attacker.HasKeyword(KeywordRules.DoubleStrike);
            bool targetFirst = target.HasKeyword(KeywordRules.FirstStrike)
                               || target.HasKeyword(KeywordRules.DoubleStrike);
            bool attackerDouble = attacker.HasKeyword(KeywordRules.DoubleStrike);
            bool targetDouble = target.HasKeyword(KeywordRules.DoubleStrike);

            // 缴械（定案）：攻击结算时，被攻击的目标无法反击（先攻步/普通步的目标伤害均不结算；
            // 对角色目标同样生效——武器反伤也被缴械压制）。目标无反击力量时不播报。
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

            // 反击资格（定案）：只有未横置的随从才能反击——反击不消耗横置（横置是攻击/发动的代价），
            // 已横置的随从只能挨打（守卫拦截即横置 → 单向受伤，警戒例外：横置也能造成战斗伤害）。
            // **攻击方强制竖直参战（2026-09-16 显式特判，原隐式注释"结算按竖直参战"显式化）**：
            // 攻击方在结算期支付横置后强制按竖直参战——横置不削输出、不失去参战资格；
            // 双方横置 = 攻击单向伤害（目标侧 targetCanCounter=false）。
            // 角色目标无横置概念（恒视为未横置）——武器反伤照常。
            bool targetCanCounter = !target.IsTapped()
                                    || (target is Card vc && vc.HasKeyword(KeywordRules.Vigilance));

            // ---- 先攻步 ----
            if (attackerFirst)
                DealCombatDamage(attacker, target, attackerPower);
            if (targetFirst && !disarmed && targetCanCounter)
                DealCombatDamage(target, attacker, targetPower);

            // ---- 普通步（存活者；连击者在两步各结算一次） ----
            if (attacker.IsAlive && (!attackerFirst || attackerDouble))
                DealCombatDamage(attacker, target, attackerPower);
            if (!disarmed && targetCanCounter && target != attacker && target.IsAlive && (!targetFirst || targetDouble))
            {
                DealCombatDamage(target, attacker, targetPower);
                // 角色（玩家）反伤结算完成 → 武器耐久回调（武器系统落地后在此扣 1 耐久；
                // 力量为 0 时无反伤不消耗）。攻击已锁力量，被圣盾/护甲抵挡不退还耐久。
                if (targetPower > 0 && target is Player counterattacking)
                    OnPlayerCounterattackResolved?.Invoke(counterattacking);
            }

            // ---- 碾压（2026-09-13 重定义，表行锚=红3）：攻击结算时对目标以及目标相邻 1 格随从
            //      造成战斗伤害——目标经上方正常步结算，相邻同侧随从在此各受一次（额外受击不反击） ----
            if (attacker.IsAlive && attacker.HasKeyword(KeywordRules.Overwhelm)
                && target is Card pivot && AdjacentResolver != null)
            {
                foreach (var adjacent in AdjacentResolver(pivot))
                {
                    if (adjacent == null || adjacent == attacker || adjacent == target || !adjacent.IsAlive)
                        continue;
                    if (adjacent.GetController() != pivot.GetController())
                        continue; // 只打目标同侧（防守方）的相邻随从
                    DealCombatDamage(attacker, adjacent, attackerPower);
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
        /// 毒刺（定案）：攻击者持有毒刺且目标受击后存活 → 目标附着一个毒素指示物
        /// （持续3回合，回合结束每层1伤——CounterRules 统一计时）。
        /// </summary>
        private void DealCombatDamage(Entity source, Entity target, int amount)
        {
            UnityEngine.Debug.Log($"[CMBTDBG] 造成战斗伤害：来源={(source is Card sc ? sc.ID : "玩家")} 目标={(target is Card tdc ? tdc.ID : "玩家")} 数值={amount} 目标存活={target?.IsAlive}");
            if (amount <= 0 || target == null || !target.IsAlive) return;

            KeywordRules.ApplyDamage(source, target, amount, true);

            // 毒刺改写（2026-09-13 定案）已上移 KeywordRules.ApplyDamage 战斗伤害改写口——
            // 造成战斗伤害时"改为"添加指示物（伤害不发生），此处不再附加处理。
        }

        // EndCombat/CancelCombat/阻挡阶段（SelectBlocker/DeclareBlock/EndBlockDeclaration/
        // EndAttackDeclaration/ExecuteDamage 批结算）已随 2026-09-16 战斗接入栈机器退役：
        // 攻击=速度0栈对象逐攻击开窗，死亡处理由栈机器 SBA 轮统一接管（见 ResolveAttackDeclaration）。
    }
}
