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
    /// AI 出牌舞台 + 通用施放展示（2026-10-18 BattleScreen 功能拆分第五块外迁）。
    /// 纯 C# 类，Func 注入（CombatChoreographer 先例）：
    /// ①威胁箭头（2026-10-16 用户定案：对手出牌先亮 1 秒再结算）——SimpleAI 声明成功后
    ///   Show 开演、结算前 Hide 收幕（finally 保证终局/异常不漏收）。
    /// ②通用施放展示（2026-10-17 用户定案：发动位立绘立牌退役→平贴卡类型图标+左上角卡面）——
    ///   快照驱动：CastingCards 最新一张=左上角 ACard 全信息卡面，与棋盘类型图标同帧出现
    ///  （同一对账快照）；连锁等待期卡恒在 Activation 区→展示保持等待连锁。
    ///   时序定案：离区不即收——①成功转正在场=linger 至登场演出完成（≈1.15s）；②法术结算成功
    ///   =1s 等待期；③被阻拦失败（CardActivationFailedEvent→DropIf）即清；期间新卡上盘即顶替；
    ///   到期由 Tick 收。
    /// </summary>
    internal sealed class BattleAiCastStage
    {
        private readonly Func<RectTransform> _root;
        private readonly Func<BattleViewData> _view;
        private readonly Func<BattleFieldView> _field;
        private readonly Func<Player> _p1;
        private readonly Func<BattleCardView, Vector3> _unitAnchor;

        private readonly List<BattleAttackArrow> _aiStageArrows = new List<BattleAttackArrow>();
        private GameObject _overlayCardPrefab; // ACard 预制体（HotUpdateAssets 惰性加载，Region 列卡同源）
        private GameObject _castStageCard;     // 左上角卡面实例（换卡先拆旧再建新）
        private uint _castStageRid;            // 当前展示的施放卡 RuntimeId（0=空——linger 期保持非 0）
        private float _castStageLingerUntil;   // linger 截止（Time.time；0=不在 linger——离区后的演出/等待窗口）

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

        /// <summary>施放展示对账：CastingCards 最新一张上左上角卡面（本地 FromLiveCard 全信息/
        /// 网络 FromHand）。列表空≠即收——按离区去向定 linger 档后保持，Tick 到期收。</summary>
        public void Refresh(BattleViewData d)
        {
            var latest = d != null && d.CastingCards.Count > 0 ? d.CastingCards[d.CastingCards.Count - 1] : null;
            uint rid = latest != null ? latest.RuntimeId : 0;
            if (rid == 0)
            {
                if (_castStageCard == null) { _castStageRid = 0; _castStageLingerUntil = 0f; return; } // 无实例可 linger
                if (_castStageRid != 0 && _castStageLingerUntil <= 0f) // 刚离区：一次定档
                    _castStageLingerUntil = Time.time + (UnitOnBoard(_castStageRid) ? 1.15f : 1f);
                return;
            }
            if (rid == _castStageRid && _castStageCard != null) return; // 同卡且实例在——不重建
            _castStageRid = rid;
            _castStageLingerUntil = 0f; // 新卡顶替：旧 linger 作废
            if (_castStageCard != null) { UnityEngine.Object.Destroy(_castStageCard); _castStageCard = null; }
            var root = _root();
            if (root == null) return;

            if (_overlayCardPrefab == null)
                _overlayCardPrefab = HotUpdateAssets.Load<GameObject>("ACard", UiKit.PrefabPath("ACard"));
            if (_overlayCardPrefab == null) return;
            var go = UnityEngine.Object.Instantiate(_overlayCardPrefab, root, false);
            go.name = "cast-stage";
            var rt = (RectTransform)go.transform;
            rt.SetAsLastSibling();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); // 屏幕左上角
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(24f, -24f);
            var cv = go.AddComponent<ACardView>();
            cv.Bind();
            cv.Apply(latest.CoreCard != null
                ? CardOverlayItem.FromLiveCard(latest.CoreCard)
                : CardOverlayItem.FromHand(latest), CardOverlayLayout.Full);
            _castStageCard = go;
        }

        /// <summary>linger 到期收展示（逐帧探针——对账是事件驱动，纯计时到期没有事件可蹭）。</summary>
        public void Tick()
        {
            if (_castStageRid == 0 || _castStageLingerUntil <= 0f || Time.time < _castStageLingerUntil) return;
            Drop();
        }

        /// <summary>施放展示即收（linger 到期/被阻拦失败）：不等窗口。</summary>
        public void Drop()
        {
            _castStageRid = 0;
            _castStageLingerUntil = 0f;
            if (_castStageCard != null) { UnityEngine.Object.Destroy(_castStageCard); _castStageCard = null; }
        }

        /// <summary>发动失败即清口（CardActivationFailedEvent）：rid 匹配当前展示才收。</summary>
        public void DropIf(uint rid)
        {
            if (_castStageRid == rid) Drop();
        }

        /// <summary>rid 是否已转正在场（快照单位账——linger 定档用：在场=登场演出档）。</summary>
        private bool UnitOnBoard(uint rid)
        {
            var d = View;
            if (d == null) return false;
            foreach (var u in d.SelfUnits) if (u != null && u.RuntimeId == rid) return true;
            foreach (var u in d.OppUnits) if (u != null && u.RuntimeId == rid) return true;
            return false;
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
