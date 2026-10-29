using System;
using System.Collections.Generic;
using CardCore;
using CardCore.Serialization;
using Tide.HotUpdate;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SynergyUI
{
    /// <summary>面板当前展示的区域。</summary>
    internal enum RegionKind { None, Graveyard, Exile, Activation, Deck } // Extra 已随 P11 额外区格整删（2026-10-29）

    /// <summary>
    /// Region 区域面板（2026-10-18 BattleScreen 功能拆分第四块外迁）：骨架构建（prefab 用户节点：
    /// 点棋盘墓地/除外/发动区/卡组格打开）+ 开关/重列 + 列卡池化。纯 C# 类——与
    /// RegionTargetSelector 共用同一 prefab 节点（选择面三件 label-num/btn-select/viewport
    /// 不参与浏览态排版），互斥判定留在屏侧（OpenRegion/CloseRegion/RefreshRegion 守卫转发——
    /// 选择面占用期间关面板=窗口蒸发，2026-10-15 冻结教训）。
    /// </summary>
    internal sealed class BattleRegionPanel
    {
        private readonly Func<GameCore> _core;
        private readonly Func<Player> _p1;

        private RegionKind _kind;
        private Player _owner;
        private RectTransform _panel;
        private TMP_Text _title;
        private UiKit.Scroll _grid;      // 列卡网格（墓地/除外/发动区）
        private RectTransform _lines;    // 文本行（卡组数量/临时元素钱包）
        private GameObject _cardPrefab;
        private readonly List<ACardView> _cardPool = new List<ACardView>(); // Region 列卡实例池（留存复用，2026-10-18）

        private GameCore Core => _core();
        private Player P1 => _p1();

        /// <summary>Region prefab 节点（RegionTargetSelector 的挂载点——屏侧构造选择器时取用）。</summary>
        public RectTransform PanelNode => _panel;

        public BattleRegionPanel(Func<GameCore> core, Func<Player> p1)
        {
            _core = core;
            _p1 = p1;
        }

        /// <summary>面板骨架（prefab 用户节点：标题+关闭+卡片网格/文本行两态；默认隐藏）。</summary>
        public void Build(RectTransform panelNode)
        {
            _panel = panelNode;
            if (_panel == null) return;

            var img = _panel.GetComponent<Image>();
            if (img != null) img.color = new Color(0.07f, 0.08f, 0.12f, 0.92f); // 白底改深衬（文字可读）

            var vg = _panel.GetComponent<VerticalLayoutGroup>()
                     ?? _panel.gameObject.AddComponent<VerticalLayoutGroup>();
            vg.spacing = 8f;
            vg.padding = new RectOffset(12, 12, 10, 10);
            vg.childControlWidth = true;
            vg.childControlHeight = true;
            vg.childForceExpandWidth = false;
            vg.childForceExpandHeight = false;
            vg.childAlignment = TextAnchor.UpperLeft;

            var titleRow = UiKit.Row("title", _panel, spacing: 8f);
            _title = UiKit.Label("lbl", titleRow, "", UiStyle.BodySize, UiStyle.TextPrimary, TextAnchor.MiddleLeft);
            UiKit.Size(_title, fw: 1f);
            UiKit.Button("btn-close", titleRow, "关闭", Close, height: 30f);

            _lines = UiKit.Column("lines", _panel, spacing: 6f);
            UiKit.Size(_lines, fw: 1f, fh: 1f);
            _lines.gameObject.SetActive(false);

            _grid = UiKit.ScrollGrid("cards", _panel, cellW: 232f, cellH: 296f, spacing: 10f);
            UiKit.Size(_grid.Rect.transform, fw: 1f, fh: 1f);

            // 选择三件（label-num/btn-select/viewport）不参与浏览态 VLG 排版（保持 prefab 摆位；
            // viewport 拉伸锚点被 VLG 接管则横滑面崩坏）；选择面由 RegionTargetSelector 自管显隐
            foreach (var kid in new[] { "label-num", "btn-select", "viewport" })
            {
                var t = _panel.Find(kid);
                if (t == null) continue;
                var le = t.GetComponent<LayoutElement>();
                if (le == null) le = t.gameObject.AddComponent<LayoutElement>();
                le.ignoreLayout = true;
            }

            _panel.gameObject.SetActive(false);
        }

        /// <summary>Region 选择面三件（label-num/btn-select/viewport）显隐——浏览态全藏；
        /// 选择面打开时由 RegionTargetSelector 自显（节点名与 prefab 单源）。</summary>
        private void SetSelectNodes(bool visible)
        {
            if (_panel == null) return;
            foreach (var kid in new[] { "label-num", "btn-select", "viewport" })
            {
                var t = _panel.Find(kid);
                if (t != null && t.gameObject.activeSelf != visible) t.gameObject.SetActive(visible);
            }
        }

        public void Open(RegionKind kind, Player owner)
        {
            if (_panel == null) { Debug.Log($"[Region] 面板节点缺失，无法展示 {kind}"); return; }
            _kind = kind;
            _owner = owner;
            _panel.gameObject.SetActive(true);
            Refresh();
        }

        public void Close()
        {
            if (_panel == null || !_panel.gameObject.activeSelf) return;
            _panel.gameObject.SetActive(false); // 选择进行中不开关（屏侧守卫防窗口蒸发——2026-10-15 冻结）
            _kind = RegionKind.None;
            _owner = null;
            Debug.Log("[Region] 关闭");
        }

        /// <summary>面板内容重列（打开时/区域变化刷新链调用；未开着零开销）。</summary>
        public void Refresh()
        {
            if (_panel == null || !_panel.gameObject.activeSelf || _owner == null) return;
            SetSelectNodes(false); // 浏览态藏选择三件（label-num/btn-select/viewport）
            string who = _owner == P1 ? "我方" : "对手";
            bool ownerIsOpp = _owner != P1;

            Zone? zone = _kind switch
            {
                RegionKind.Graveyard => Zone.Graveyard,
                RegionKind.Exile => Zone.Exile,
                RegionKind.Activation => Zone.Activation,
                _ => null,
            };
            if (zone.HasValue)
            {
                var cards = Core != null ? Core.ZoneManager.GetCards(_owner, zone.Value) : null;
                string zoneName = _kind == RegionKind.Graveyard ? "墓地"
                    : _kind == RegionKind.Exile ? "除外区"
                    : "发动区";
                _title.text = $"{who}{zoneName} · {cards?.Count ?? 0} 张";
                FillCards(cards, ownerIsOpp);
                Debug.Log($"[Region] {who}{zoneName} 列卡 {cards?.Count ?? 0} 张");
                return;
            }
            if (_kind == RegionKind.Deck)
            {
                // 2026-10-17 定案：卡组列表=被展示的卡（点击门槛同款——无被展示卡根本打不开本面板）
                var exposed = BattleHoverInfoProvider.ExposedDeckCardsOf(Core, _owner);
                _title.text = $"{who}卡组被展示 · {exposed.Count} 张";
                FillCards(exposed, ownerIsOpp);
                Debug.Log($"[Region] {who}卡组被展示列卡 {exposed.Count} 张");
            }
            // 临时元素 Region 面板（2026-10-14 技能格定案退役）：黑白钱包=技能格数字条、
            // 彩色钱包余额=地牌格「剩余」并入显示，不再有独立列表面板
        }

        /// <summary>列卡（2026-10-18 池化）：实例留存 Content 内复用——原每次刷新 ClearChildren+全量
        /// Instantiate（看墓地期间赶上 AI 回合=每个引擎事件 30-60 张卡销毁重建尖峰）。池件 Apply 先行
        ///（重指向新运行态卡）再激活：OnEnable 订阅落在正确卡上；多余件 SetActive(false)（OnDisable 自退订）。</summary>
        private void FillCards(List<Card> cards, bool ownerIsOpp)
        {
            _lines.gameObject.SetActive(false);
            _grid.Rect.gameObject.SetActive(true);
            int n = cards?.Count ?? 0;
            if (n > 0 && _cardPrefab == null)
                _cardPrefab = HotUpdateAssets.Load<GameObject>("ACard", UiKit.PrefabPath("ACard"));
            if (n > 0 && _cardPrefab == null)
            {
                Debug.LogError("[Region] ACard.prefab 缺失——无法列卡");
                return;
            }
            while (_cardPool.Count < n)
            {
                var go = UnityEngine.Object.Instantiate(_cardPrefab, _grid.Content, false);
                var cv = go.AddComponent<ACardView>();
                cv.Bind();
                go.SetActive(false);
                _cardPool.Add(cv);
            }
            int i = 0;
            if (cards != null)
            {
                foreach (var c in cards)
                {
                    var cv = _cardPool[i];
                    var view = BattleView.FromRuntime(SerializableRuntimeCardState.FromCard(c), c, ownerIsOpp);
                    cv.Apply(CardOverlayItem.FromHand(view), CardOverlayLayout.Full);
                    cv.gameObject.name = "region-card-" + i;
                    cv.transform.SetSiblingIndex(i); // 活跃件按列表序排前（多余池件沉底，布局忽略 inactive）
                    cv.gameObject.SetActive(true);
                    i++;
                }
            }
            for (; i < _cardPool.Count; i++)
                _cardPool[i].gameObject.SetActive(false);
        }
    }
}
