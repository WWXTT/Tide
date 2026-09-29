using System;
using System.Collections.Generic;
using System.Linq;
using CardCore.Attribute;

namespace CardCore
{
    /// <summary>
    /// 装备系统运行时（2026-09-13 第二十一批定案：万智神器 × 炉石武器——箭头佩带 + 驱动）。
    ///
    /// - **佩带 = 箭头指向格的占据者**（认格不认主：对手走进指向格效果照发）——
    ///   普通装备的佩带者效果经现有 LinkAuraSystem（LinkAuraData 声明 stat/keyword）live-query 生效，
    ///   悬空（空格）= 无受益者 = 效果暂停，天然成立。EquipRules 不重复实现此路径。
    /// - **武器**（IsWeapon）：佩带者 = 控制者**角色**（非箭头格）——角色获得 Power 字段攻击力、
    ///   可主动攻击（一回合一次）、被攻击时反伤；主动攻击与反伤**各 -1 耐久**。
    /// - **驱动（读法 A）**：N 支箭头的武器需 **N 个生物分别占据各箭头指向格**（己方存活生物）
    ///   才算驱动完成——角色才获得攻击力；少一个 = 未驱动（武器无效）。无箭头武器恒驱动。
    /// - **耐久归零 = 销毁入墓**（Smash 同款直毁路径）。
    /// - **转移**：移到己方空位 + 修改箭头方向，耐久 -1（见 GameActions.TransferEquipment）。
    ///
    /// 接线：CombatSystem 预留口（PlayerCounterattackPower / OnPlayerCounterattackResolved）
    /// 由本类的 Attach 注入（组合根 GameCore.Reset 调用，幂等）。
    /// </summary>
    public static class EquipRules
    {
        private static bool _attached;

        /// <summary>组合根接线（GameCore.Reset 调用，幂等）：注入反伤查询与耐久回调。</summary>
        public static void EnsureAttached(GameCore core)
        {
            if (_attached) return;
            _attached = true;
            CombatSystem.PlayerCounterattackPower = player => GetWeaponPower(core, player);
            CombatSystem.OnPlayerCounterattackResolved = player => ConsumeDurabilityOnCounterattack(core, player);
        }

        // ======================================== 驱动（读法 A：位置要求） ========================================

        /// <summary>
        /// 武器是否驱动完成（2026-09-13 用户裁决：**驱动暂时屏蔽**——武器暂时只有一支箭头，
        /// 恒驱动）。读法 A 的多箭头逐格检查保留为注释块，恢复时取消包裹。
        /// </summary>
        public static bool IsDriven(GameCore core, Card weapon)
        {
            // ===== 驱动屏蔽（2026-09-13 定案：驱动先屏蔽，武器暂时只有一个箭头）=====
            return true; // 恒驱动——单箭头武器不需逐格检查

            /*
            // ===== 读法 A（多箭头驱动——恢复时解包）=====
            if (weapon == null || !IsEquipment(weapon)) return false;
            var data = (weapon as CardWrapper)?.GetData();
            var arrows = data?.ArrowDirections ?? HexDirection.None;
            if (arrows == HexDirection.None) return true; // 无箭头武器恒驱动

            if (!GameBoard.LinkAuraSystem.Enabled) return false; // 无棋盘不驱动
            var sCell = GameBoard.LinkAuraSystem.TryGetCellOf?.Invoke(weapon);
            if (!sCell.HasValue) return false;
            int owner = GameBoard.LinkAuraSystem.OwnerIndexOf?.Invoke(weapon) ?? -1;
            if (owner < 0) return false;
            var controller = weapon.GetController();

            foreach (var bit in new[]
            {
                HexDirection.Up, HexDirection.UpperRight, HexDirection.LowerRight,
                HexDirection.Down, HexDirection.LowerLeft, HexDirection.UpperLeft,
            })
            {
                if ((arrows & bit) == 0) continue;
                var abs = GameBoard.BoardMath.MapArrow(bit);
                if (owner == 1) abs = GameBoard.BoardMath.Opposite(abs);
                var (nx, nz) = GameBoard.BoardMath.Neighbor(sCell.Value.x, sCell.Value.z, abs);
                if (!GameBoard.BoardMath.InBounds(nx, nz)) return false;
                var occupant = GameBoard.LinkAuraSystem.CardAt?.Invoke(nx, nz);
                if (!(occupant is Card c) || !c.IsAlive
                    || !(c is IHasSupertype st && st.Supertype == Cardtype.Creature)
                    || c.GetController() != controller) return false;
            }
            return true;
            */
        }

        // ======================================== 武器攻击力（反伤口） ========================================

        /// <summary>角色的武器总攻击力：所有已驱动武器 Power 之和（未驱动/无武器=0）。</summary>
        public static int GetWeaponPower(GameCore core, Player player)
        {
            if (core?.ZoneManager == null || player == null) return 0;
            int total = 0;
            foreach (var c in core.ZoneManager.GetCards(player, Zone.Battlefield))
            {
                if (!(c is CardWrapper w)) continue;
                var data = w.GetData();
                if (data?.IsWeapon != true || !c.IsAlive) continue;
                if (IsDriven(core, c)) total += Math.Max(0, c.GetPower());
            }
            return total;
        }

        /// <summary>反伤结算回调：消耗该角色所有已驱动武器各 1 耐久（归零销毁）。</summary>
        private static void ConsumeDurabilityOnCounterattack(GameCore core, Player player)
        {
            if (core?.ZoneManager == null || player == null) return;
            foreach (var c in core.ZoneManager.GetCards(player, Zone.Battlefield).ToList())
            {
                if (!(c is CardWrapper w)) continue;
                if (w.GetData()?.IsWeapon != true || !c.IsAlive) continue;
                if (IsDriven(core, c)) LoseDurability(core, c, 1, "反伤");
            }
        }

        // ======================================== 耐久 ========================================

        /// <summary>装备是否为装备（无生命持久物 + Artifact 超类或 Equip 标签）。</summary>
        public static bool IsEquipment(Card card)
            => card is IHasSupertype st && st.Supertype == Cardtype.Artifact;

        /// <summary>消耗 N 点耐久；归零 → 销毁入墓（Smash 同款直毁路径 + 播报）。</summary>
        public static void LoseDurability(GameCore core, Card equipment, int amount, string reason)
        {
            if (core?.ZoneManager == null || equipment == null || amount <= 0) return;
            int cur = equipment.GetCounterCount(CounterRules.DurabilityCounter);
            if (cur <= 0) return; // 无耐久档（持续型装备）不消耗

            equipment.AddCounters(CounterRules.DurabilityCounter, -Math.Min(amount, cur));
            EventManager.Instance.Publish(new KeywordAppliedEvent
            {
                Target = equipment,
                Keyword = CounterRules.DurabilityCounter,
                Detail = $"耐久 -{amount}（{reason}；剩余 {Math.Max(0, cur - amount)}）",
            });

            if (equipment.GetCounterCount(CounterRules.DurabilityCounter) <= 0)
            {
                var owner = equipment.GetOwner() ?? equipment.GetController();
                if (owner != null)
                {
                    var from = equipment.GetZone();
                    if (from != Zone.Graveyard)
                        core.ZoneManager.MoveCard(equipment, owner, from, Zone.Graveyard);
                }
                EventManager.Instance.Publish(new CardDestroyEvent
                {
                    DestroyedCard = equipment,
                    Reason = DestroyReason.Smashed, // 耐久耗尽=摧毁口径（无生命直毁）
                });
                EventManager.Instance.Publish(new KeywordAppliedEvent
                {
                    Target = equipment,
                    Keyword = CounterRules.DurabilityCounter,
                    Detail = "耐久归零——装备销毁",
                });
            }
        }

        /// <summary>入场初始化耐久（CardData.Durability > 0 时挂 N 层；由 CardPutToBattlefieldEvent 驱动）。</summary>
        public static void OnEnterBattlefield(Card card)
        {
            if (!(card is CardWrapper w)) return;
            var data = w.GetData();
            if (data == null || !IsEquipment(card)) return;
            if (data.Durability > 0 && card.GetCounterCount(CounterRules.DurabilityCounter) <= 0)
                card.AddCounters(CounterRules.DurabilityCounter, data.Durability);
        }
    }
}
