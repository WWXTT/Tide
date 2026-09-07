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
    /// 战斗系统
    /// 处理攻击宣言、阻挡宣言、伤害计算和结算。
    ///
    /// 战斗为双向结算（定案）：随从互殴双方同时互致伤害；角色（玩家）被攻击同样有反伤——
    /// 反伤力量与耐久消耗走武器系统扩展点（见 PlayerCounterattackPower，系统未实现前角色反伤为 0）。
    ///
    /// 关键词行为（定案）：
    /// - 可用性统一走横置：随从一律横置入场；冲锋/突袭一次性生效 = 解除横置 + 消耗关键词
    ///   （突袭残留紊乱指示物一回合，期间不能以玩家为目标）；攻击需未横置
    /// - 风怒：每回合可攻击 2 次；守卫：友方单位被选为攻击目标时横置并强制转移目标
    /// - 警戒：横置可被取消（攻击/代价横置，一回合一次，经 KeywordRules.ShouldTap）
    /// - 嘲讽：防守方有存活嘲讽随从时不能指定玩家为攻击目标（碾压无视）
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

        private CombatPhase _currentPhase = CombatPhase.None;
        private List<CombatParticipant> _attackers = new List<CombatParticipant>();
        private List<CombatParticipant> _blockers = new List<CombatParticipant>();
        private Player _attackingPlayer;
        private Player _defendingPlayer;
        private ZoneManager _zoneManager;
        private LayerEngine _layerEngine;

        public CombatPhase CurrentPhase => _currentPhase;
        public List<CombatParticipant> Attackers => _attackers;
        public List<CombatParticipant> Blockers => _blockers;
        public bool InCombat => _currentPhase != CombatPhase.None;
        public Player AttackingPlayer => _attackingPlayer;
        public Player DefendingPlayer => _defendingPlayer;

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

        /// <summary>开始战斗阶段</summary>
        public void StartCombat(Player attackingPlayer, Player defendingPlayer)
        {
            _attackingPlayer = attackingPlayer;
            _defendingPlayer = defendingPlayer;
            _attackers.Clear();
            _blockers.Clear();
            _currentPhase = CombatPhase.SelectAttacker;

            EventManager.Instance.Publish(new CombatPhaseStartEvent
            {
                AttackingPlayer = attackingPlayer,
                DefendingPlayer = defendingPlayer
            });
        }

        /// <summary>每回合攻击次数上限（1；风怒关键词已删除，多次攻击留待将来机制）</summary>
        public static int MaxAttacksPerTurn(Card card)
        {
            return 1;
        }

        /// <summary>检查是否可以攻击（攻击者侧资格）</summary>
        public bool CanDeclareAttack(Entity attacker, Player controller)
        {
            if (_currentPhase != CombatPhase.SelectAttacker &&
                _currentPhase != CombatPhase.DeclareAttack)
                return false;

            if (controller != _attackingPlayer)
                return false;

            // 检查是否在战场
            if (attacker is Card card)
            {
                if (!_zoneManager.IsCardInZone(card, controller, Zone.Battlefield))
                    return false;

                // 攻击次数上限（风怒 = 2）
                if (card.AttacksThisTurn >= MaxAttacksPerTurn(card))
                    return false;
            }

            // 检查是否已横置（横置状态经扩展方法读取——Card 不实现 ITappable，该接口仅地牌池卡实现）
            if (attacker.IsTapped())
                return false;

            // 检查是否有攻击力（按层引擎计算的当前力量）
            if (attacker is IHasPower && GetPower(attacker) <= 0)
                return false;

            // 检查是否已经攻击（本战斗会话内）
            if (_attackers.Any(a => a.Entity == attacker))
                return false;

            return true;
        }

        /// <summary>检查攻击者能否指定该目标（目标侧资格：突袭紊乱限制/嘲讽/潜行）。
        /// 突袭定案：生效即消耗（解除横置），负面=紊乱指示物（一回合内不能以玩家为目标，攻击与效果同口径）。</summary>
        public bool CanAttackTarget(Entity attacker, Entity target)
        {
            if (target == null || !target.IsAlive || target == attacker) return false;

            // 只能攻击防守方（对方随从或对方玩家）
            var targetController = target is Card tc ? tc.GetController() : target as Player;
            if (targetController != _defendingPlayer) return false;

            // 潜行：不可被指定为攻击目标
            if (target is Card sc && sc.HasKeyword(KeywordRules.Stealth))
                return false;

            // 突袭紊乱（负面指示物，持续一回合）：期间不准以玩家为目标——只能攻随从
            if (target is Player && KeywordRules.HasRushSickness(attacker))
                return false;

            // 嘲讽：防守方战场有存活嘲讽随从时，不能指定玩家（碾压无视嘲讽）
            if (target is Player && !attacker.HasKeyword(KeywordRules.Overwhelm)
                && DefendersWithTaunt().Any())
                return false;

            return true;
        }

        /// <summary>防守方战场上存活的嘲讽随从</summary>
        private IEnumerable<Card> DefendersWithTaunt()
        {
            return _zoneManager.GetCards(_defendingPlayer, Zone.Battlefield)
                .Where(c => c.IsAlive && c.HasKeyword(KeywordRules.Taunt));
        }

        /// <summary>
        /// 宣告攻击（时点定案）：
        /// 1. 资格预检 → 守卫转移（目标确认）→ 发布攻击宣言时点（触发器可响应，如连锁冻结攻击者）；
        /// 2. 宣言后重检：攻击者已横置（代价被连锁抢先支付不出）/死亡、目标丢失（死亡/不可指定）
        ///    → 攻击取消回滚（不支付、不计数、不入队），返回 false 交上层重新确认目标；
        /// 3. 重检通过 → 支付横置（警戒抵扣，一回合一次）→ 入队 + 潜行失效 + 次数 +1。
        /// 战斗效果结算与生命结算在 ExecuteDamage / EndCombat（第 2、3 段）。
        /// </summary>
        public bool DeclareAttack(Entity attacker, Entity target)
        {
            if (!CanDeclareAttack(attacker, _attackingPlayer))
                return false;
            if (!CanAttackTarget(attacker, target))
                return false;

            // 守卫关键词已删除（2026-09-03 原子表整体修正）——攻击目标确认不再有守卫转移

            _currentPhase = CombatPhase.DeclareAttack;

            // 攻击宣言时点（经 GameCore 统一路由发布，OnAttack/OnAttacked 触发可见）——
            // 先于横置支付：宣言触发的效果（如冻结攻击者）在此同步生效
            var declaration = new AttackDeclarationEvent
            {
                Attacker = attacker,
                Target = target,
                AttackingPlayer = _attackingPlayer
            };
            if (GameCore.Instance != null) GameCore.Instance.PublishEvent(declaration);
            else EventManager.Instance.Publish(declaration);

            // ---- 宣言后重检（连锁响应已生效）：支付不出 / 丢失目标 → 攻击取消回滚 ----
            // 横置代价不可被支付（宣言期间被冻结等抢先横置）——也不可被警戒抵消；
            // 目标丢失（死亡/不可指定）→ 回滚，交上层重新确认攻击目标
            if (!attacker.IsAlive || attacker.IsTapped() || !CanAttackTarget(attacker, target))
            {
                EventManager.Instance.Publish(new CombatCancelledEvent
                {
                    Attacker = attacker,
                    OriginalTarget = target,
                    AttackingPlayer = _attackingPlayer
                });
                return false;
            }

            // ---- 支付横置（固定代价；警戒一回合一次抵扣）→ 入队 ----
            if (KeywordRules.ShouldTap(attacker))
                attacker.Tap();

            var participant = new CombatParticipant
            {
                Entity = attacker,
                Controller = _attackingPlayer,
                IsAttacking = true,
                DeclaredTarget = target,
            };
            _attackers.Add(participant);

            // 潜行：攻击后移除（发动效果后的移除见 EffectExecutor）
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

            // 攻击次数 +1（每回合上限见 CanDeclareAttack；取消的宣言不计数）
            if (attacker is Card counted)
                counted.AttacksThisTurn++;

            return true;
        }

        /// <summary>结束攻击宣言阶段，进入阻挡阶段</summary>
        public void EndAttackDeclaration()
        {
            if (_attackers.Count == 0)
            {
                EndCombat();
                return;
            }
            _currentPhase = CombatPhase.SelectBlocker;
        }

        /// <summary>检查是否可以阻挡</summary>
        public bool CanBlock(Entity blocker, Entity attacker, Player controller)
        {
            if (_currentPhase != CombatPhase.SelectBlocker &&
                _currentPhase != CombatPhase.DeclareBlock)
                return false;

            if (controller != _defendingPlayer)
                return false;

            if (blocker is Card card)
            {
                if (!_zoneManager.IsCardInZone(card, controller, Zone.Battlefield))
                    return false;
            }

            if (blocker.IsTapped())
                return false;

            if (!_attackers.Any(a => a.Entity == attacker))
                return false;

            if (_blockers.Any(b => b.Entity == blocker))
                return false;

            return true;
        }

        /// <summary>宣告阻挡</summary>
        public void DeclareBlock(Entity blocker, Entity attacker)
        {
            if (!CanBlock(blocker, attacker, _defendingPlayer))
                return;

            var attackerParticipant = _attackers.First(a => a.Entity == attacker);

            var blockerParticipant = new CombatParticipant
            {
                Entity = blocker,
                Controller = _defendingPlayer,
                IsBlocking = true,
                BlockedTarget = attacker
            };
            _blockers.Add(blockerParticipant);

            attackerParticipant.BlockedBy = blocker;

            _currentPhase = CombatPhase.DeclareBlock;

            EventManager.Instance.Publish(new BlockDeclarationEvent
            {
                Blocker = blocker,
                Attacker = attacker,
                BlockingPlayer = _defendingPlayer
            });
        }

        /// <summary>结束阻挡宣言，进入伤害结算</summary>
        public void EndBlockDeclaration()
        {
            _currentPhase = CombatPhase.DamageCalculation;
            ExecuteDamage();
        }

        /// <summary>计算战斗伤害（保留阶段流转入口；实际结算在 ExecuteDamage 按关键词规则进行）</summary>
        public void CalculateDamage()
        {
            _currentPhase = CombatPhase.DamageDealing;
            ExecuteDamage();
        }

        /// <summary>
        /// 执行战斗伤害：逐攻击者按配对结算。
        /// 配对目标 = BlockedBy（阻挡流程）优先，否则 DeclaredTarget（直接指定），否则防守玩家。
        /// 先攻/连击分两步：先攻步（先攻/连击者结算，死者不进普通步）；普通步（存活者结算，连击者再结算一次）。
        /// </summary>
        public void ExecuteDamage()
        {
            UnityEngine.Debug.Log($"[CMBTDBG] ExecuteDamage entry attackers={_attackers.Count} defendingLife={_defendingPlayer?.Life} defendingAlive={_defendingPlayer?.IsAlive}");
            foreach (var attacker in _attackers)
            {
                // 防守方玩家已判负（前序攻击致生命归零）→ 后续攻击不再结算，交由 EndCombat 判定胜负
                if (_defendingPlayer != null && _defendingPlayer.Life <= 0) { UnityEngine.Debug.Log($"[CMBTDBG] ExecuteDamage BREAK defendingLife={_defendingPlayer.Life}"); break; }

                var target = attacker.BlockedBy ?? attacker.DeclaredTarget ?? (Entity)_defendingPlayer;
                if (target == null || !target.IsAlive) { UnityEngine.Debug.Log($"[CMBTDBG] ExecuteDamage CONTINUE target null/dead target={(target is Card tc ? tc.ID : "player")}"); continue; } // 目标已倒（前序攻击击杀）→ 落空
                if (!attacker.Entity.IsAlive) { UnityEngine.Debug.Log($"[CMBTDBG] ExecuteDamage CONTINUE attacker dead attacker={(attacker.Entity is Card ac ? ac.ID : "player")}"); continue; } // 攻击者已倒（宣言后效果致死）→ 落空（横置代价已付不退）

                ResolvePair(attacker.Entity, target);
            }

            EndCombat();
        }

        /// <summary>结算一次攻击配对（含先攻/连击/碾压；剧毒吸血圣盾护甲坚韧在 KeywordRules 内）</summary>
        private void ResolvePair(Entity attacker, Entity target)
        {
            int attackerPower = GetPower(attacker);
            UnityEngine.Debug.Log($"[CMBTDBG] ResolvePair attacker={(attacker is Card ac ? ac.ID : "player")} attackerPower={attackerPower} target={(target is Card tc ? tc.ID : "player")} attackerFirst={attacker.HasKeyword(KeywordRules.FirstStrike) || attacker.HasKeyword(KeywordRules.DoubleStrike)}");
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
            // 已横置的随从只能挨打。角色目标无横置概念（恒视为未横置）——武器反伤照常。
            bool targetCanCounter = !target.IsTapped();

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

            // ---- 碾压：对目标相邻 1 格随从各视为一次攻击（额外受击不反击） ----
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
            UnityEngine.Debug.Log($"[CMBTDBG] DealCombatDamage source={(source is Card sc ? sc.ID : "player")} target={(target is Card tdc ? tdc.ID : "player")} amount={amount} targetAlive={target?.IsAlive}");
            if (amount <= 0 || target == null || !target.IsAlive) return;

            KeywordRules.ApplyDamage(source, target, amount, true);

            // 毒刺：对受到战斗伤害的目标附加一个毒素指示物
            if (source != null && source.IsAlive && target.IsAlive
                && source.HasKeyword(KeywordRules.PoisonSting))
            {
                target.AddCounters(Attribute.CounterRules.ToxinCounter, 1,
                    Attribute.CounterRules.Find(Attribute.CounterRules.ToxinCounter).Turns, source);
                EventManager.Instance.Publish(new KeywordAppliedEvent
                {
                    Target = target,
                    Keyword = KeywordRules.PoisonSting,
                    Detail = "毒刺：附加一个毒素指示物（回合结束1伤，持续3回合）",
                    Source = source
                });
            }
        }

        /// <summary>结束战斗</summary>
        public void EndCombat()
        {
            _currentPhase = CombatPhase.EndCombat;

            EventManager.Instance.Publish(new CombatPhaseEndEvent
            {
                AttackingPlayer = _attackingPlayer,
                DefendingPlayer = _defendingPlayer
            });

            _currentPhase = CombatPhase.None;
            _attackers.Clear();
            _blockers.Clear();

            // 泵一次状态动作：战斗死亡的移墓/复生/触发不等下一次栈结算（编辑器直攻路径无人泵 SBA）
            GameCore.Instance?.SBAEngine.ExecuteAll();

            // 战斗连锁结算完成后：生命判负即时判定（每连锁一次；幂等只发一次）
            GameCore.Instance?.CheckLifeGameOver();
        }

        /// <summary>取消战斗</summary>
        public void CancelCombat()
        {
            _currentPhase = CombatPhase.None;
            _attackers.Clear();
            _blockers.Clear();
        }
    }
}
