using System;
using System.Collections.Generic;
using System.Linq;

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
    }

    /// <summary>
    /// 战斗系统
    /// 处理攻击宣言、阻挡宣言、伤害计算和结算
    /// </summary>
    public class CombatSystem
    {
        private CombatPhase _currentPhase = CombatPhase.None;
        private List<CombatParticipant> _attackers = new List<CombatParticipant>();
        private List<CombatParticipant> _blockers = new List<CombatParticipant>();
        private Player _attackingPlayer;
        private Player _defendingPlayer;
        private ZoneManager _zoneManager;
        private LayerEngine _layerEngine;

        /// <summary>
        /// 当前战斗阶段
        /// </summary>
        public CombatPhase CurrentPhase => _currentPhase;

        /// <summary>
        /// 攻击者列表
        /// </summary>
        public List<CombatParticipant> Attackers => _attackers;

        /// <summary>
        /// 阻挡者列表
        /// </summary>
        public List<CombatParticipant> Blockers => _blockers;

        /// <summary>
        /// 是否在战斗中
        /// </summary>
        public bool InCombat => _currentPhase != CombatPhase.None;

        /// <summary>
        /// 攻击玩家
        /// </summary>
        public Player AttackingPlayer => _attackingPlayer;

        /// <summary>
        /// 防守玩家
        /// </summary>
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

        /// <summary>
        /// 开始战斗阶段
        /// </summary>
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

        /// <summary>
        /// 检查是否可以攻击
        /// </summary>
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
            }

            // 检查是否已横置
            if (attacker is ITappable tappable && tappable.IsTapped)
                return false;

            // 检查是否有攻击力（按层引擎计算的当前力量）
            if (attacker is IHasPower && GetPower(attacker) <= 0)
                return false;

            // 检查是否已经攻击
            if (_attackers.Any(a => a.Entity == attacker))
                return false;

            return true;
        }

        /// <summary>
        /// 宣告攻击
        /// </summary>
        public void DeclareAttack(Entity attacker, Entity target)
        {
            if (!CanDeclareAttack(attacker, _attackingPlayer))
                return;

            var participant = new CombatParticipant
            {
                Entity = attacker,
                Controller = _attackingPlayer,
                IsAttacking = true
            };
            _attackers.Add(participant);

            // 横置攻击者
            if (attacker is ITappable tappable)
                tappable.IsTapped = true;

            _currentPhase = CombatPhase.DeclareAttack;

            EventManager.Instance.Publish(new AttackDeclarationEvent
            {
                Attacker = attacker,
                Target = target,
                AttackingPlayer = _attackingPlayer
            });
        }

        /// <summary>
        /// 结束攻击宣言阶段，进入阻挡阶段
        /// </summary>
        public void EndAttackDeclaration()
        {
            if (_attackers.Count == 0)
            {
                EndCombat();
                return;
            }
            _currentPhase = CombatPhase.SelectBlocker;
        }

        /// <summary>
        /// 检查是否可以阻挡
        /// </summary>
        public bool CanBlock(Entity blocker, Entity attacker, Player controller)
        {
            if (_currentPhase != CombatPhase.SelectBlocker &&
                _currentPhase != CombatPhase.DeclareBlock)
                return false;

            if (controller != _defendingPlayer)
                return false;

            // 检查是否在战场
            if (blocker is Card card)
            {
                if (!_zoneManager.IsCardInZone(card, controller, Zone.Battlefield))
                    return false;
            }

            // 检查是否已横置
            if (blocker is ITappable tappable && tappable.IsTapped)
                return false;

            // 检查攻击者是否存在
            if (!_attackers.Any(a => a.Entity == attacker))
                return false;

            // 检查是否已经阻挡
            if (_blockers.Any(b => b.Entity == blocker))
                return false;

            return true;
        }

        /// <summary>
        /// 宣告阻挡
        /// </summary>
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

        /// <summary>
        /// 结束阻挡宣言，进入伤害计算
        /// </summary>
        public void EndBlockDeclaration()
        {
            _currentPhase = CombatPhase.DamageCalculation;
            CalculateDamage();
        }

        /// <summary>
        /// 计算战斗伤害
        /// </summary>
        public void CalculateDamage()
        {
            foreach (var attacker in _attackers)
            {
                int attackerPower = GetPower(attacker.Entity);

                if (attacker.BlockedBy != null)
                {
                    // 被阻挡：与阻挡者互相造成伤害
                    int blockerPower = GetPower(attacker.BlockedBy);

                    attacker.AssignedDamage = blockerPower;

                    var blockerParticipant = _blockers.First(b => b.Entity == attacker.BlockedBy);
                    blockerParticipant.AssignedDamage = attackerPower;
                }
                else
                {
                    // 未被阻挡：对玩家造成伤害
                    attacker.AssignedDamage = 0;
                }
            }

            _currentPhase = CombatPhase.DamageDealing;
            ExecuteDamage();
        }

        /// <summary>
        /// 执行战斗伤害
        /// </summary>
        public void ExecuteDamage()
        {
            foreach (var attacker in _attackers)
            {
                if (attacker.BlockedBy != null)
                {
                    // 对阻挡者造成伤害
                    DealCombatDamage(attacker.Entity, attacker.BlockedBy, GetPower(attacker.Entity));
                }
                else
                {
                    // 对玩家造成伤害
                    DealDamageToPlayer(attacker.Entity, _defendingPlayer, GetPower(attacker.Entity));
                }

                // 攻击者受到伤害
                if (attacker.AssignedDamage > 0)
                {
                    DealCombatDamage(attacker.BlockedBy, attacker.Entity, attacker.AssignedDamage);
                }
            }

            foreach (var blocker in _blockers)
            {
                if (blocker.AssignedDamage > 0 && !_attackers.Any(a => a.BlockedBy == blocker.Entity))
                {
                    // 阻挡者受到伤害（如果还没处理过）
                    DealCombatDamage(blocker.BlockedTarget, blocker.Entity, blocker.AssignedDamage);
                }
            }

            EndCombat();
        }

        /// <summary>
        /// 获取实体的攻击力
        /// </summary>
        private int GetPower(Entity entity)
        {
            if (_layerEngine != null)
                return _layerEngine.CalculatePower(entity);
            if (entity is IHasPower hasPower)
                return hasPower.Power;
            return 0;
        }

        /// <summary>
        /// 造成战斗伤害（对实体）
        /// </summary>
        private void DealCombatDamage(Entity source, Entity target, int amount)
        {
            if (amount <= 0) return;

            if (target is IHasLife hasLife)
            {
                hasLife.Life -= amount;
            }

            EventManager.Instance.Publish(new CombatDamageEvent
            {
                Attacker = source,
                Defender = target,
                Damage = amount
            });

            EventManager.Instance.Publish(new DamageEvent
            {
                Source = source,
                Target = target,
                Amount = amount
            });
        }

        /// <summary>
        /// 对玩家造成伤害
        /// </summary>
        private void DealDamageToPlayer(Entity source, Player target, int amount)
        {
            if (amount <= 0) return;

            int oldLife = target.Life;
            target.Life -= amount;

            EventManager.Instance.Publish(new DamageEvent
            {
                Source = source,
                Target = target,
                Amount = amount
            });

            EventManager.Instance.Publish(new LifeChangeEvent
            {
                Player = target,
                OldLife = oldLife,
                NewLife = target.Life,
                Source = source
            });
        }

        /// <summary>
        /// 结束战斗
        /// </summary>
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
        }

        /// <summary>
        /// 取消战斗
        /// </summary>
        public void CancelCombat()
        {
            _currentPhase = CombatPhase.None;
            _attackers.Clear();
            _blockers.Clear();
        }
    }
}
