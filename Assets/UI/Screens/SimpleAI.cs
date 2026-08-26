using System.Collections.Generic;
using System.Linq;
using CardCore;

namespace SynergyUI
{
    /// <summary>
    /// 极简脚本 AI（P2）：仅用于驱动回合循环、产生可观测的状态变化，不追求强度或策略。
    /// 所有动作都走 GameActions / BattleController 既有入口；可行性由引擎校验（失败即跳过）。
    /// </summary>
    public sealed class SimpleAI
    {
        public void TakeTurn(BattleController ctrl)
        {
            var core = ctrl.Core;
            var me = ctrl.TurnPlayer;
            if (core == null || me == null || me != ctrl.P2) return;

            DoStandby(core, me);
            DoMain(core, me);
            DoCombat(ctrl, core, me);
            GameActions.EndTurn(core, me);
        }

        /// <summary>准备阶段：横置恢复由引擎自动处理，直接跳过进入主阶段。</summary>
        private void DoStandby(GameCore core, Player me)
        {
            GameActions.SkipElementPool(core, me);
        }

        /// <summary>
        /// 主阶段：补地牌到当前地牌槽上限 → 逐张横置产出（剩余最多颜色）→ 快照逐张尝试出牌。
        /// 付不起 / 不合法的由引擎拒绝。
        /// </summary>
        private void DoMain(GameCore core, Player me)
        {
            // 补充地牌（可用状态入场，本回合即可横置）
            var hand = new List<Card>(core.ZoneManager.GetCards(me, Zone.Hand) ?? new List<Card>());
            int cap = core.ElementPool.GetLandCap(me);
            foreach (var card in hand)
            {
                if (core.ElementPool.GetPooledCards(me).Count >= cap) break;
                GameActions.AddToElementPool(core, me, card);
            }

            // 横置全部地牌：各自产出剩余最多颜色的元素入 bank
            foreach (var land in core.ElementPool.GetPooledCards(me))
            {
                var color = PickColor(land);
                if (color.HasValue)
                    GameActions.GainElementFromToken(core, me, land, color.Value);
            }

            // 快照：PlayCard 会改动手牌区，避免迭代时修改集合。
            var handSnapshot = new List<Card>(core.ZoneManager.GetCards(me, Zone.Hand));
            foreach (var card in handSnapshot)
            {
                // 需目标的法术：targets 传 null，引擎按各原子效果配置自动取候选前 N。
                GameActions.PlayCard(core, me, card, null);
            }
        }

        /// <summary>选产出颜色：剩余指示物最多的颜色（并列取先见者）。</summary>
        private static ManaType? PickColor(PooledCard land)
        {
            ManaType? best = null;
            int bestCount = 0;
            foreach (var kv in land.Tokens)
            {
                if (kv.Value > bestCount)
                {
                    bestCount = kv.Value;
                    best = kv.Key;
                }
            }
            return best;
        }

        /// <summary>战斗：开战斗 → 每个可攻击的战场单位打脸 → 结算。</summary>
        private void DoCombat(BattleController ctrl, GameCore core, Player me)
        {
            ctrl.BeginCombat();

            var battlefield = new List<Card>(core.ZoneManager.GetCards(me, Zone.Battlefield));
            foreach (var unit in battlefield)
            {
                ctrl.DeclareAttack(me, unit, me.Opponent);
            }

            ctrl.ResolveCombat();
        }
    }
}
