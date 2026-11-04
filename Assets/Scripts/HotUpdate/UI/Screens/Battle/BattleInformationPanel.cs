using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.Attribute;
using CardCore.Serialization;
using GameBoard;
using Tide.HotUpdate;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SynergyUI
{
    /// <summary>
    /// Information 区域卡查阅窗（2026-11-03 战场UI改版）：BattleUI.prefab 的 Information 节点
    /// （btn-hide + viewport/content/ACard 模板——用户烘焙，与 Select 同位同尺寸）。点墓地/除外/
    /// 有地牌的元素格列出该区域卡（非交互全卡面，BattleFaceScale 战场尺寸）：
    /// - btn-hide=整窗关闭（区别于 Select 的 btn-hide 收 viewport 不关窗）；
    /// - 与 Select 的互斥由屏侧收口（BattleScreen）：Select 被选择/文本/下拉/预览占用或
    ///   btn-hide 切回显示时关本窗（onWindowNeedsSpace）；选择面开着且 viewport 未收起时
    ///   区域点击不开本窗；3D 接管态（守卫/选格/攻击箭头）本窗让位。
    /// 实例池化（同 BattleRegionPanel 性能口径）：查看期间赶上 AI 回合 Refresh 重拉不整池销毁重建。
    /// </summary>
    internal sealed class BattleInformationPanel
    {
        private readonly Func<GameCore> _core;                     // 区域卡重拉（墓地/除外）
        private readonly Func<int, List<BattleCardView>> _landsOf; // ownerIdx→该方场上地牌（地牌格查阅）
        private readonly List<ACardView> _pool = new List<ACardView>();

        private RectTransform _node;
        private Button _btnHide;
        private RectTransform _content;
        private RectTransform _template;
        private TMP_Text _labelTitle; // Information/label-title：区域名+张数（2026-11-03 用户补建）
        private GameObject _cardPrefab; // 模板缺失时的 ACard.prefab 直载回落（BattleRegionPanel 同款）

        // 当前展示来源（Refresh 对账重拉用；两来源互斥）
        private Zone? _zone;
        private Player _zoneOwner;
        private int _landOwnerIdx = -1, _landX, _landZ;
        private bool _ownerIsOpp;

        public bool IsOpen => _node != null && _node.gameObject.activeSelf;

        public BattleInformationPanel(Func<GameCore> core, Func<int, List<BattleCardView>> landsOf)
        {
            _core = core;
            _landsOf = landsOf;
        }

        /// <summary>装配（prefab 用户节点：btn-hide/viewport/content/ACard 模板）。默认隐藏。</summary>
        public void Build(RectTransform node)
        {
            _node = node;
            if (_node == null) return;
            _btnHide = _node.Find("btn-hide")?.GetComponent<Button>();
            _content = _node.Find("viewport/content") as RectTransform;
            _template = _node.Find("viewport/content/ACard") as RectTransform;
            if (_btnHide != null)
            {
                _btnHide.onClick.RemoveAllListeners();
                _btnHide.onClick.AddListener(Close);
                var lbl = _btnHide.GetComponentInChildren<TMP_Text>(true);
                if (lbl != null) lbl.text = "关闭"; // 文案回写防烘焙漂移（BindBattleButton 同惯例）
            }
            if (_template != null) _template.gameObject.SetActive(false); // 只做克隆源，不参与展示
            var titleNode = _node.Find("label-title");
            _labelTitle = titleNode != null ? titleNode.GetComponentInChildren<TMP_Text>(true) : null;
            if (_btnHide == null || _content == null)
                Debug.LogError("[Information] prefab 缺节点（btn-hide/viewport/content）——区域查阅窗不可用");
            if (_labelTitle == null)
                Debug.LogWarning("[Information] 缺 label-title——区域名/张数不显示（prefab 2026-11-03 补建节点）");
            _node.gameObject.SetActive(false);
        }

        /// <summary>开区域（墓地/除外）。重复开=按最新来源重列。</summary>
        public void OpenZone(Zone zone, Player owner, bool ownerIsOpp)
        {
            if (_node == null || _content == null) return;
            _zone = zone; _zoneOwner = owner; _ownerIsOpp = ownerIsOpp; _landOwnerIdx = -1;
            _node.gameObject.SetActive(true);
            ResetScroll();
            Refresh();
        }

        /// <summary>开地牌格（ownerIdx 方 x,z 格上的全部地牌）。</summary>
        public void OpenLands(int ownerIdx, int x, int z, bool ownerIsOpp)
        {
            if (_node == null || _content == null) return;
            _zone = null; _zoneOwner = null; _landOwnerIdx = ownerIdx; _landX = x; _landZ = z; _ownerIsOpp = ownerIsOpp;
            _node.gameObject.SetActive(true);
            ResetScroll();
            Refresh();
        }

        /// <summary>关窗（btn-hide 同口）。来源记账一并清。</summary>
        public void Close()
        {
            if (_node == null || !_node.gameObject.activeSelf) return;
            _node.gameObject.SetActive(false);
            _zone = null; _zoneOwner = null; _landOwnerIdx = -1;
        }

        /// <summary>按来源重拉重列（屏侧 RefreshRegion 联动——查看期间区域变动对账；未开零开销）。
        /// 正背面规则（2026-11-03 定案）：墓地/除外/己方手牌=正面；牌组（双方）与对方手牌=隐藏区，
        /// 卡背展示——被展示卡（RevealRules.Exposed）翻正面含状态（指示物芯片随 ACardView 全卡面）。</summary>
        public void Refresh()
        {
            if (!IsOpen || _content == null) return;
            var items = new List<(CardOverlayItem item, bool faceUp)>();
            string what;
            if (_zone.HasValue)
            {
                List<Card> cards = null;
                if (_core != null)
                {
                    try { cards = _core()?.ZoneManager?.GetCards(_zoneOwner, _zone.Value); }
                    catch (KeyNotFoundException) { cards = null; } // 区未初始化=空（ProphecyHandlerUtil 同口径）
                }
                bool hidden = _zone.Value == Zone.Deck || (_zone.Value == Zone.Hand && _ownerIsOpp);
                HashSet<Card> exposed = null;
                if (hidden)
                {
                    var core = _core != null ? _core() : null;
                    if (core != null)
                        exposed = new HashSet<Card>(RevealRules.GetExposedCards(core.ZoneManager, _zoneOwner, _zone.Value));
                }
                what = (_ownerIsOpp ? "对方" : "己方") + ZoneZh(_zone.Value);
                foreach (var c in cards ?? new List<Card>())
                {
                    if (c == null) continue;
                    var view = BattleView.FromRuntime(SerializableRuntimeCardState.FromCard(c), c, _ownerIsOpp);
                    items.Add((CardOverlayItem.FromHand(view), exposed == null || exposed.Contains(c)));
                }
            }
            else if (_landOwnerIdx >= 0 && _landsOf != null)
            {
                var lands = _landsOf(_landOwnerIdx) ?? new List<BattleCardView>();
                what = LandCellTitle();
                foreach (var l in lands.Where(c => c.X == _landX && c.Z == _landZ && c.Runtime != null))
                    items.Add((CardOverlayItem.FromHand(l), true));
            }
            else return;
            Fill(items);
            if (_labelTitle != null) _labelTitle.text = $"{what} · {items.Count} 张"; // 标题=区域名+当前张数（Refresh 随对账更新）
            Debug.Log($"[Information] {what} 列卡 {items.Count} 张");
        }

        private static string ZoneZh(Zone zone)
            => zone == Zone.Graveyard ? "墓地"
            : zone == Zone.Exile ? "除外区"
            : zone == Zone.Deck ? "牌组"
            : zone == Zone.Hand ? "手牌"
            : zone.ToString();

        /// <summary>地牌格标题（2026-11-03 定案口径）：「对/己方元素池 X色地牌格」——元素按
        /// BoardLayout 固定格序（LandCellElementOrder）从坐标反查；查不到回落旧坐标式。</summary>
        private string LandCellTitle()
        {
            string who = _ownerIsOpp ? "对方" : "己方";
            var cells = BoardLayout.LandCells(_landOwnerIdx);
            for (int i = 0; i < cells.Count; i++)
                if (cells[i].x == _landX && cells[i].z == _landZ)
                    return $"{who}元素池 {BattleView.ManaZh(BoardLayout.LandCellElementOrder[i])}色地牌格";
            return $"{who}地牌格({_landX},{_landZ})";
        }

        private void ResetScroll()
        {
            if (_node != null && _node.GetComponent<ScrollRect>() is { } scroll)
                scroll.horizontalNormalizedPosition = 0f; // 开窗从最左起（Refresh 不复位——查看中不拽回滚动）
        }

        /// <summary>列卡（池化）：模板/ACard.prefab 克隆 + ACardView 全卡面；背面卡 SetFaceUp(false)
        /// 卡背盖面（ACardBack 整卡实例）；多余件沉底隐藏。</summary>
        private void Fill(List<(CardOverlayItem item, bool faceUp)> items)
        {
            Transform src = _template;
            if (src == null)
            {
                if (_cardPrefab == null)
                    _cardPrefab = HotUpdateAssets.Load<GameObject>("ACard", UiKit.PrefabPath("ACard"));
                if (_cardPrefab == null)
                {
                    Debug.LogError("[Information] ACard 模板与 prefab 双缺——无法列卡");
                    return;
                }
                src = _cardPrefab.transform;
            }
            while (_pool.Count < items.Count)
            {
                var rt = UnityEngine.Object.Instantiate(src, _content, false) as RectTransform;
                rt.gameObject.SetActive(false);
                rt.name = "info-card-" + _pool.Count;
                var cost = rt.Find("cost");
                if (cost != null) cost.gameObject.SetActive(false);
                var btnLayer = rt.Find("btn");
                if (btnLayer != null) btnLayer.gameObject.SetActive(false);
                HandFanView.ApplyBattleFaceScale(rt);
                var cv = rt.gameObject.AddComponent<ACardView>();
                cv.Bind();
                _pool.Add(cv);
            }
            for (int i = 0; i < _pool.Count; i++)
            {
                bool on = i < items.Count;
                if (on)
                {
                    _pool[i].Apply(items[i].item, CardOverlayLayout.Full);
                    _pool[i].SetFaceUp(items[i].faceUp); // 隐藏区卡背盖面；被展示卡正面（含指示物状态）
                    _pool[i].transform.SetSiblingIndex(i); // 活跃件按列表序排前（多余池件沉底，布局忽略 inactive）
                }
                if (_pool[i].gameObject.activeSelf != on) _pool[i].gameObject.SetActive(on);
            }
        }
    }
}
