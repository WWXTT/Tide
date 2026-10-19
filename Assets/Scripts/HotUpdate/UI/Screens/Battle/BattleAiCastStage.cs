using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using GameBoard;
using Tide.HotUpdate;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>
    /// AI 出牌舞台（2026-10-18 BattleScreen 功能拆分第五块外迁；2026-10-19 左上角卡面退役）。
    /// 纯 C# 类，Func 注入（CombatChoreographer 先例）：
    /// 威胁箭头（2026-10-16 用户定案：对手出牌先亮 1 秒再结算）——SimpleAI 声明成功后
    /// Show 开演、结算前 Hide 收幕（finally 保证终局/异常不漏收）。
    /// 卡面呈现职责已归连锁链舞台（BattleChainStage）：施放卡上栈即克隆上链，左上角
    /// cast-stage 卡面（2026-10-17 平贴图标时代的通用施放展示）全面退役删除。
    /// </summary>
    internal sealed class BattleAiCastStage
    {
        private readonly Func<RectTransform> _root;
        private readonly Func<BattleViewData> _view;
        private readonly Func<BattleFieldView> _field;
        private readonly Func<Player> _p1;
        private readonly Func<BattleCardView, Vector3> _unitAnchor;

        private readonly List<BattleAttackArrow> _aiStageArrows = new List<BattleAttackArrow>();

        private BattleViewData View => _view();
        private BattleFieldView Field => _field();
        private Player P1 => _p1();

        public BattleAiCastStage(Func<RectTransform> root, Func<BattleViewData> view,
            Func<BattleFieldView> field, Func<Player> p1, Func<BattleCardView, Vector3> unitAnchor)
        {
            _root = root;
            _view = view;
            _field = field;
            _p1 = p1;
            _unitAnchor = unitAnchor;
        }

        /// <summary>舞台开演：施放类型图标→目标的指向箭头（红=威胁指向）。
        /// 箭头只对场上单位/角色目标出（手牌/牌库等对象无棋盘锚）；起点=施放类型图标占格
        /// （CardPlayEvent 同步对账已上盘），缺件回落对手角色格。</summary>
        public void Show(Card card, List<Entity> targets)
        {
            Hide();
            var root = _root();
            if (card == null || root == null) return;

            Vector3 from = AiCastOrigin(card);
            foreach (var t in targets ?? new List<Entity>(0))
            {
                var anchor = StageTargetAnchor(t);
                if (anchor == null) continue;
                var arrow = new BattleAttackArrow();
                arrow.Show(from, anchor.Value, BattleAttackArrow.AttackSkin);
                _aiStageArrows.Add(arrow);
            }
        }

        /// <summary>舞台收幕：箭头冻结渐隐（0.3s）后回收。</summary>
        public void Hide()
        {
            foreach (var a in _aiStageArrows) FadeDisposeArrowAsync(a).Forget();
            _aiStageArrows.Clear();
        }

        private static async UniTaskVoid FadeDisposeArrowAsync(BattleAttackArrow arrow)
        {
            if (arrow == null) return;
            await arrow.FadeOutAsync(0.3f);
            arrow.Dispose();
        }

        /// <summary>箭头起点：施放类型图标占格心上抬（TryGetOnBoardCell 含施放占格账）；缺件回落对手角色格心。</summary>
        private Vector3 AiCastOrigin(Card card)
        {
            if (Field != null && card != null && Field.TryGetOnBoardCell(card.RuntimeId, out int x, out int z))
                return BattleBoardGrid.WorldPositionOf(x, z) + new Vector3(0f, 0.6f, 0f);
            var c = BoardLayout.CharacterCell(1);
            return BattleBoardGrid.WorldPositionOf(c.x, c.z) + new Vector3(0f, 0.6f, 0f);
        }

        /// <summary>目标锚点：场上单位=模型 wrapper/格心（UnitAnchorOf 同口径·经注入）；角色=角色格心上抬
        ///（守卫窗口角色锚同款）。非场上对象（手牌/牌库/未落格）=null 不出箭头。</summary>
        private Vector3? StageTargetAnchor(Entity t)
        {
            if (t is Card c)
            {
                var v = (View?.SelfUnits ?? new List<BattleCardView>())
                    .Concat(View?.OppUnits ?? new List<BattleCardView>())
                    .FirstOrDefault(u => u != null && !u.IsDead && ReferenceEquals(u.CoreCard, c));
                return v != null ? _unitAnchor(v) : (Vector3?)null;
            }
            if (t is Player p)
            {
                var cell = BoardLayout.CharacterCell(p == P1 ? 0 : 1);
                return BattleBoardGrid.WorldPositionOf(cell.x, cell.z) + new Vector3(0f, 0.6f, 0f);
            }
            return null;
        }
    }
}
