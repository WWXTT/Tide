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
    /// 关键词行为（定案）：
    /// - 召唤失调：随从入场当回合不可攻击（冲锋豁免；突袭豁免但只能攻随从）
    /// - 风怒：每回合可攻击 2 次；守卫：友方单位被选为攻击目标时横置并强制转移目标
    /// - 警戒：横置可被取消（攻击/代价横置，一回合一次，经 KeywordRules.ShouldTap）
    /// - 嘲讽：防守方有存活嘲讽随从时不能指定玩家为攻击目标（碾压无视）
    /// - 潜行：不可被指定为攻击目标；攻击后移除（发动效果后的移除在效果执行器）
    /// - 先攻/连击：先攻步先行结算（死者不反击）；连击两步均结算
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

        /// <summary>每回合攻击次数上限（1；风怒 = 2）</summary>
        public static int MaxAttacksPerTurn(Card card)
        {
            return card != null && card.HasKeyword(KeywordRules.Windfury) ? 2 : 1;
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

                // 召唤失调：入场当回合不可攻击（冲锋/突袭豁免——突袭的目标限制见 CanAttackTarget）
                if (card.SummonedThisTurn
                    && !card.HasKeyword(KeywordRules.Charge)
                    && !card.HasKeyword(KeywordRules.Rush))
                    return false;

                // 攻击次数上限（风怒 = 2）
                if (card.AttacksThisTurn >= MaxAttacksPerTurn(card))
                    return false;
            }

            // 检查是否已横置
            if (attacker is ITappable tappable && tappable.IsTapped)
                return false;

            // 检查是否有攻击力（按层引擎计算的当前力量）
            if (attacker is IHasPower && GetPower(attacker) <= 0)
                return false;

            // 检查是否已经攻击（本战斗会话内）
            if (_attackers.Any(a => a.Entity == attacker))
                return false;

            return true;
        }

        /// <summary>检查攻击者能否指定该目标（目标侧资格：突袭限制/嘲讽/潜行）</summary>
        public bool CanAttackTarget(Entity attacker, Entity target)
        {
            if (target == null || !target.IsAlive || target == attacker) return false;

            // 只能攻击防守方（对方随从或对方玩家）
            var targetController = target is Card tc ? tc.GetController() : target as Player;
            if (targetController != _defendingPlayer) return false;

            // 突袭：失调回合只能攻随从，不能攻玩家
            if (attacker is Card ac && ac.SummonedThisTurn
                && ac.HasKeyword(KeywordRules.Rush) && !ac.HasKeyword(KeywordRules.Charge)
                && target is Player)
                return false;

            // 潜行：不可被指定为攻击目标
            if (target is Card sc && sc.HasKeyword(KeywordRules.Stealth))
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

        /// <summary>宣告攻击</summary>
        public void DeclareAttack(Entity attacker, Entity target)
        {
            if (!CanDeclareAttack(attacker, _attackingPlayer))
                return;
            if (!CanAttackTarget(attacker, target))
                return;

            // 守卫：友方单位被选为攻击目标时，横置并强制转移攻击目标为自己
            if (target is Card targetCard)
            {
                var guard = _zoneManager.GetCards(_defendingPlayer, Zone.Battlefield)
                    .FirstOrDefault(c => c != targetCard && c.IsAlive && !c.IsTapped()
                                         && c.HasKeyword(KeywordRules.Guard));
                if (guard != null)
                {
                    if (KeywordRules.ShouldTap(guard))
                        guard.Tap();
                    target = guard;
                    EventManager.Instance.Publish(new KeywordAppliedEvent
                    {
                        Target = guard,
                        Keyword = KeywordRules.Guard,
                        Detail = "守卫转移：友方受到的攻击改由守卫承受",
                        Source = attacker
                    });
                }
            }

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

            // 攻击次数 +1（每回合上限见 CanDeclareAttack）
            if (attacker is Card counted)
                counted.AttacksThisTurn++;

            // 横置攻击者（警戒：一回合一次抵消横置）
            if (KeywordRules.ShouldTap(attacker))
            {
                if (attacker is ITappable tappable)
                    tappable.IsTapped = true;
            }

            _currentPhase = CombatPhase.DeclareAttack;

            // 经 GameCore 统一路由发布（On_AttackDeclare 触发时点可见）
            var declaration = new AttackDeclarationEvent
            {
                Attacker = attacker,
                Target = target,
                AttackingPlayer = _attackingPlayer
            };
            if (GameCore.Instance != null) GameCore.Instance.PublishEvent(declaration);
            else EventManager.Instance.Publish(declaration);
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

            if (blocker is ITappable tappable && tappable.IsTapped)
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
            foreach (var attacker in _attackers)
            {
                // 防守方玩家已判负（前序攻击致生命归零）→ 后续攻击不再结算，交由 EndCombat 判定胜负
                if (_defendingPlayer != null && _defendingPlayer.Life <= 0) break;

                var target = attacker.BlockedBy ?? attacker.DeclaredTarget ?? (Entity)_defendingPlayer;
                if (target == null || !target.IsAlive) continue; // 目标已倒（前序攻击击杀）→ 落空

                ResolvePair(attacker.Entity, target);
            }

            EndCombat();
        }

        /// <summary>结算一次攻击配对（含先攻/连击/碾压；剧毒吸血圣盾护甲坚韧在 KeywordRules 内）</summary>
        private void ResolvePair(Entity attacker, Entity target)
        {
            int attackerPower = GetPower(attacker);
            int targetPower = target is Card ? GetPower(target) : 0;

            bool attackerFirst = attacker.HasKeyword(KeywordRules.FirstStrike)
                                 || attacker.HasKeyword(KeywordRules.DoubleStrike);
            bool targetFirst = target.HasKeyword(KeywordRules.FirstStrike)
                               || target.HasKeyword(KeywordRules.DoubleStrike);
            bool attackerDouble = attacker.HasKeyword(KeywordRules.DoubleStrike);
            bool targetDouble = target.HasKeyword(KeywordRules.DoubleStrike);

            // ---- 先攻步 ----
            if (attackerFirst)
                DealCombatDamage(attacker, target, attackerPower);
            if (targetFirst)
                DealCombatDamage(target, attacker, targetPower);

            // ---- 普通步（存活者；连击者在两步各结算一次） ----
            if (attacker.IsAlive && (!attackerFirst || attackerDouble))
                DealCombatDamage(attacker, target, attackerPower);
            if (target != attacker && target.IsAlive && (!targetFirst || targetDouble))
                DealCombatDamage(target, attacker, targetPower);

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
        /// （圣盾/护甲指示物/坚韧/剧毒/吸血/系命），实际造成 > 0 才发事件。
        /// </summary>
        private void DealCombatDamage(Entity source, Entity target, int amount)
        {
            if (amount <= 0 || target == null || !target.IsAlive) return;

            int actual = KeywordRules.ApplyDamage(source, target, amount, true);
            if (actual <= 0) return; // 被圣盾/护甲/坚韧完全吸收

            EventManager.Instance.Publish(new CombatDamageEvent
            {
                Attacker = source,
                Defender = target,
                Damage = actual
            });
            // DamageEvent 由 KeywordRules.ApplyDamage 统一发布（结算前已过替代引擎，此处不补发）

            if (target is Player player)
            {
                EventManager.Instance.Publish(new LifeChangeEvent
                {
                    Player = player,
                    OldLife = player.Life + actual,
                    NewLife = player.Life,
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
