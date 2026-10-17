using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SynergyUI
{
    /// <summary>手牌扇交互钩子（宿主屏注入，2026-10-14 手牌出牌接线）：条件评估与点击接管
    /// 全在宿主屏完成（UI Framework 不耦合引擎）；null 钩子或 null 委托=该项不可用。
    /// 未注入钩子的扇区（对手扇常态）不显示按钮层。</summary>
    public sealed class FanInteractions
    {
        public Func<HandFanCard, bool> CanUse;
        public Func<HandFanCard, bool> CanPlaceLand;
        public Action<HandFanCard> OnUse;
        public Action<HandFanCard> OnPlaceLand;
    }
    /// <summary>手牌扇区的一条数据：对账键=引擎卡 RuntimeId；FaceUp=false 只显示卡背（内容被 back 覆盖）。</summary>
    public sealed class HandFanCardData
    {
        public uint RuntimeId;
        public CardOverlayItem Item;
        public bool FaceUp = true;
    }

    /// <summary>
    /// 手牌扇形排布（炉石式原型，2026-10-02）：一条水平扇区——底部=己方（拱形弧：
    /// 圆心在屏外下方的持牌轴——中心卡最高、端卡下垂外倾），顶部=对手（垂直镜像+整体缩小，
    /// 默认卡背）。
    /// 纯 C# 类：宿主（屏幕）注入挂载层并调用 Tick 帧驱动；自持 ACard 实例池，
    /// 卡牌尺寸在加载 prefab 时动态读取（卡面未定型——间距/抬升/放大等全部按实际
    /// CardSize 比例推导，不写死像素）。悬停卡放大回正、抬向屏幕中央、渲染置顶，
    /// 邻卡向两侧让位（悬停卡占扇形比例增加）；SetCards 按 RuntimeId 幂等对账，
    /// 手牌数量变化即时重排。设计保持可挂载性：未来 3D 战场 UI 直接复用本类。
    /// </summary>
    public sealed class HandFanView
    {
        /// <summary>扇区配置（初值待截图调参）。</summary>
        public sealed class Config
        {
            public bool TopSide;                       // true=顶部扇区（对手）：弧线/倾角垂直镜像
            public Vector2 Center = Vector2.zero;      // 扇心（挂载层中心锚点系下的 anchoredPosition）
            public float BaseScale = 1f;               // 常态整体缩放（对手扇区 0.72）
            public float MaxFanWidth = 1150f;          // 最大展开宽（屏像素；超出压缩重叠）
            public float FullSpacingRatio = 0.98f;     // 满间距 = 卡宽×此值（牌少不重叠）
            public float RotateRadius = 2200f;         // 倾角半径：rot = x/R（越小端卡倾得越狠）
            public float RisePerX2 = 1f / 4800f;       // 端卡下垂 = RisePerX2·x²（拱形弧曲率）
            public float HoverScaleMul = 1.38f;        // 悬停放大倍率（叠乘 BaseScale）
            public float HoverLiftRatio = 0.45f;       // 悬停抬起 = 卡高×此值（朝屏幕中央）
            public float NeighborPushRatio = 0.30f;    // 悬停时其余卡让位 = 卡宽×此值
            public float IdleSinkRatio = 0.5f;         // 未悬停整扇收缩（2026-10-14 晚定案）：向屏幕外缘
                                                       // 沉入=卡高×此值（底部扇下沉/顶部扇上收），
                                                       // 屏幕视觉比例约减半；悬停任一卡即展开
            public float TweenSpeed = 14f;             // exp 缓动速率（1/s）
        }

        // 手牌扇专用卡预制体（2026-10-15）：ACard 卡面嵌为子实例 + btn 悬浮层 + Flowing
        // 可用流光层（HandFanCard 驱动）；选目标 Region 面板等仍直用 ACard。
        private const string PrefabName = "AHandCard"; // 地址=文件名；路径由 UiKit.PrefabPath 派生

        private readonly RectTransform _layer;
        private readonly Config _cfg;
        private readonly List<HandFanCard> _cards = new List<HandFanCard>();
        private readonly Stack<HandFanCard> _pool = new Stack<HandFanCard>();
        private readonly Dictionary<uint, HandFanCard> _alive = new Dictionary<uint, HandFanCard>();
        private GameObject _prefab;
        private Vector2 _cardSize;   // 动态读取的卡牌尺寸（卡面改版只动 prefab，布局自适应）
        private HandFanCard _hovered;
        private FanInteractions _hooks;
        private float _glowPhase;    // 可发动流光脉冲相位（Tick 推进）
        private float _sinkT = 1f;   // 未悬停收缩插值（1=收起 0=展开；Tick 缓动，RebuildTargets 消费）
        private Vector2? _drawOrigin; // 抽卡飞入起点（挂载层坐标系；单次消费，对账末恒清防滞留）

        /// <summary>交互钩子（宿主屏注入；null=无交互——按钮层永不显示）。</summary>
        public FanInteractions Hooks => _hooks;

        public HandFanView(RectTransform layer, Config cfg)
        {
            _layer = layer;
            _cfg = cfg;
        }

        /// <summary>注入交互钩子（对手扇不注入）；注入后悬停卡即时重评估按钮层。</summary>
        public void SetInteractions(FanInteractions hooks)
        {
            _hooks = hooks;
            _hovered?.RefreshActionButtons();
        }

        /// <summary>整扇显隐（2026-10-14 放地/出牌选格期隐藏手牌）：挂载层 SetActive，
        /// 对账/Tick 照常（卡实例状态不丢）。隐藏时清悬停（ deactivated 卡不派发 PointerExit，
        /// 防 _hovered 悬挂令扇区复现后滞留展开态）。</summary>
        public void SetVisible(bool visible)
        {
            if (_layer == null) return;
            if (!visible && _hovered != null)
            {
                _hovered = null;
                RebuildTargets();
            }
            if (_layer.gameObject.activeSelf != visible)
                _layer.gameObject.SetActive(visible);
        }

        /// <summary>逐卡可发动流光（宿主屏对账后刷新；未知 id 忽略）。</summary>
        public void SetPlayable(uint runtimeId, bool playable)
        {
            foreach (var c in _cards)
                if (c.RuntimeId == runtimeId)
                {
                    c.SetPlayable(playable);
                    return;
                }
        }

        /// <summary>扇区当前卡数。</summary>
        public int Count => _cards.Count;

        /// <summary>抽卡飞入起点注入（世界坐标口径，2026-10-17）：卡堆顶世界点 → 本扇挂载层
        /// 本地坐标（与 Config.Center 同系；Canvas 按 renderMode 自取相机，Overlay 传 null）。
        /// 下一次对账新建的卡从该点弧线飞入扇位（单次消费）。转换失败静默返回 false
        /// =新建卡维持吸附。</summary>
        public bool SetDrawOriginFromWorld(Vector3 worldPos, Camera worldCam)
        {
            if (_layer == null || worldCam == null) return false;
            var canvas = _layer.GetComponentInParent<Canvas>();
            if (canvas == null) return false;
            var screenPos = worldCam.WorldToScreenPoint(worldPos);
            var uiCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(_layer, screenPos, uiCam, out var wp))
                return false;
            _drawOrigin = (Vector2)_layer.InverseTransformPoint(wp);
            Debug.Log($"[HandFan] 抽卡飞入起点 world={worldPos} → layer-local={_drawOrigin}");
            return true;
        }

        /// <summary>悬停上报（HandFanCard 转发）：进入/离开都触发目标重排。</summary>
        internal void NotifyHover(HandFanCard card, bool enter)
        {
            if (enter)
            {
                if (_hovered == card) return;
                _hovered = card;
            }
            else
            {
                if (_hovered != card) return;
                _hovered = null;
            }
            RebuildTargets();
        }

        /// <summary>按 RuntimeId 幂等对账（增/删/复用卡实例），随后重排布局目标。</summary>
        public void SetCards(IReadOnlyList<HandFanCardData> data)
        {
            _alive.Clear();
            // 防销毁卡（跨屏重建/停 Play 残留订阅回灌，2026-10-14 放地后手牌不移除根因）：
            // 已销毁实例直接出列——防对账中途 MissingReferenceException 截断整个刷新链
            for (int i = _cards.Count - 1; i >= 0; i--)
                if (_cards[i] == null) _cards.RemoveAt(i);
            foreach (var c in _cards) _alive[c.RuntimeId] = c;
            _cards.Clear();

            if (data != null)
            {
                foreach (var d in data)
                {
                    if (!_alive.Remove(d.RuntimeId, out var card))
                    {
                        card = null;
                        while (_pool.Count > 0 && card == null) card = _pool.Pop(); // 池内销毁实例丢弃
                        if (card == null) card = Create();
                        card.RuntimeId = d.RuntimeId;
                        card.Fresh = true;
                        if (_drawOrigin is Vector2 origin)
                        {
                            // 抽卡飞入（2026-10-17）：单次消费；弧线大幅荡向屏幕中央
                            //（控制点 2 倍卡高——起终点同在屏幕边条，平移实测看不见；对手抽牌卡背同轨）
                            _drawOrigin = null;
                            card.BeginFlyIn(origin, (_cfg.TopSide ? -1f : 1f) * _cardSize.y * 2f);
                        }
                    }
                    card.Face.Apply(d.Item, CardOverlayLayout.Full);
                    card.Face.SetFaceUp(d.FaceUp);
                    card.HideActionButtons(); // 池复用重置（悬停卡随后重评估）
                    card.SetPlayable(false);  // 可用性由宿主屏对账后重设
                    card.gameObject.SetActive(true);
                    _cards.Add(card);
                }
            }
            foreach (var leftover in _alive.Values)
            {
                leftover.ResetFlyState(); // 池回收复位（防出池残留飞入态/半透明）
                leftover.gameObject.SetActive(false);
                _pool.Push(leftover);
            }
            _alive.Clear();

            if (_hovered != null && !_cards.Contains(_hovered)) _hovered = null;
            _hovered?.RefreshActionButtons(); // 对账后悬停卡按钮条件可能已变（费用/上限）
            _drawOrigin = null; // 未消费即弃（防武装后该拍无新卡、起点滞留到后续对账误飞）
            RebuildTargets();
        }

        /// <summary>飞入完成回调（HandFanCard 终点落位后调）：重建布局恢复正典兄弟序/位姿。</summary>
        internal void OnFlyInDone(HandFanCard card) => RebuildTargets();

        /// <summary>重算全部卡的布局目标（间距压缩→谷形弧位姿→悬停态叠加→兄弟序）。</summary>
        private void RebuildTargets()
        {
            int n = _cards.Count;
            float cardW = _cardSize.x * _cfg.BaseScale;
            float cardH = _cardSize.y * _cfg.BaseScale;
            float spacing = n <= 1 ? 0f : Mathf.Min(cardW * _cfg.FullSpacingRatio, _cfg.MaxFanWidth / (n - 1));
            float push = _cfg.NeighborPushRatio * cardW;
            // 未悬停收缩（2026-10-14 晚定案）：整扇向屏幕外缘沉入（底部扇向下/顶部扇向上），视觉比例约减半
            float sinkY = (_cfg.TopSide ? 1f : -1f) * _sinkT * _cfg.IdleSinkRatio * cardH;

            int hoverIdx = -1;
            if (_hovered != null)
            {
                for (int i = 0; i < n; i++)
                    if (_cards[i] == _hovered) { hoverIdx = i; break; }
            }

            for (int i = 0; i < n; i++)
            {
                var c = _cards[i];
                float x = (i - (n - 1) * 0.5f) * spacing;
                if (hoverIdx >= 0 && i != hoverIdx)
                    x += Mathf.Sign(i - hoverIdx) * push; // 邻卡让位：悬停卡占更大扇形比例

                bool hovered = i == hoverIdx;
                // 拱形弧（圆心在屏外下方·持牌轴）：中心卡最高、端卡下垂且外倾（左卡逆时针）；
                // 顶部扇区（对手）垂直镜像——谷形弧、端卡内倾
                float rot = -x / _cfg.RotateRadius * Mathf.Rad2Deg;
                if (_cfg.TopSide) rot = -rot;
                float y = -_cfg.RisePerX2 * x * x; // 己方两端下垂；对手侧取反
                float scale = _cfg.BaseScale * (hovered ? _cfg.HoverScaleMul : 1f);

                var pos = _cfg.Center + new Vector2(x, (_cfg.TopSide ? -y : y) + sinkY);
                if (hovered)
                {
                    pos.y += (_cfg.TopSide ? -1f : 1f) * _cfg.HoverLiftRatio * cardH; // 抬向屏幕中央
                    rot = 0f;                                                          // 回正
                }
                c.SetTarget(pos, rot, scale);
                c.transform.SetSiblingIndex(i);
            }
            if (hoverIdx >= 0)
                _hovered.transform.SetAsLastSibling(); // 悬停卡渲染置顶（盖住邻卡）
        }

        /// <summary>帧驱动：全部活跃卡向目标 exp 缓动（宿主挂 UiKit.Updater 调用）；
        /// 流光相位统一推进（可发动卡的边缘光带巡游）。
        /// 收缩插值同步缓动（无悬停卡→收起/有→展开），过渡期间逐帧重排目标。</summary>
        public void Tick()
        {
            _glowPhase += Time.deltaTime * 4.5f;
            if (_cards.Count == 0) return;
            float t = 1f - Mathf.Exp(-Mathf.Min(Time.deltaTime, 0.05f) * _cfg.TweenSpeed);

            float sinkTarget = _hovered == null ? 1f : 0f;
            if (Mathf.Abs(sinkTarget - _sinkT) > 0.001f)
            {
                _sinkT += (sinkTarget - _sinkT) * t;
                if (Mathf.Abs(sinkTarget - _sinkT) <= 0.001f) _sinkT = sinkTarget;
                RebuildTargets();
            }

            for (int i = 0; i < _cards.Count; i++)
            {
                var c = _cards[i];
                if (c == null) continue; // 防销毁卡（同 SetCards 清扫口径）：防 Updater 链被单卡异常打断
                c.TickGlow(_glowPhase);
                if (c.Flying)
                {
                    c.TickFlight(Time.deltaTime); // 抽卡飞入（2026-10-17）：优先于常规缓动，到达自动交还
                    continue;
                }
                if (c.Fresh)
                {
                    c.Fresh = false;
                    c.SnapToTarget();
                    continue;
                }
                c.Pos = Vector2.Lerp(c.Pos, c.TargetPos, t);
                c.Rot = Mathf.Lerp(c.Rot, c.TargetRot, t);
                c.Scale = Mathf.Lerp(c.Scale, c.TargetScale, t);
                c.ApplyTransform();
            }
        }

        /// <summary>清空并销毁全部实例（屏退出时调用；随挂载层销毁也安全）。</summary>
        public void Dispose()
        {
            foreach (var c in _cards)
                if (c != null) UnityEngine.Object.Destroy(c.gameObject);
            _cards.Clear();
            while (_pool.Count > 0)
            {
                var c = _pool.Pop();
                if (c != null) UnityEngine.Object.Destroy(c.gameObject);
            }
            _hovered = null;
        }

        /// <summary>实例化一张卡：加载 prefab（读根尺寸为布局基准）→ 挂卡面组件与悬停组件。</summary>
        private HandFanCard Create()
        {
            if (_prefab == null)
            {
                _prefab = Tide.HotUpdate.HotUpdateAssets.Load<GameObject>(PrefabName, UiKit.PrefabPath(PrefabName));
                if (_prefab != null)
                {
                    _cardSize = ((RectTransform)_prefab.transform).sizeDelta;
                    if (_cardSize.x < 1f || _cardSize.y < 1f)
                        _cardSize = new Vector2(100f, 140f); // 尺寸异常兜底，不阻断装配
                }
                else
                {
                    Debug.LogError($"[HandFan] 卡面 prefab 缺失：{UiKit.PrefabPath(PrefabName)}（扇形退化为占位尺寸）");
                    _cardSize = new Vector2(100f, 140f);
                }
            }

            var go = _prefab != null
                ? UnityEngine.Object.Instantiate(_prefab, _layer, false)
                : new GameObject("CardFallback", typeof(RectTransform));
            var face = go.AddComponent<ACardView>();
            face.Bind();
            var card = go.AddComponent<HandFanCard>();
            card.Face = face;
            card.Fan = this;
            card.WireFlowing(); // 可用流光层接线（AHandCard/Flowing；缺节点=无流光不报错）
            // 放置/发动按钮接线（prefab 用户摆位，m_OnClick 空）：点击交宿主屏钩子接管
            card.UseButton = go.transform.Find("btn/Use")?.GetComponent<Button>();
            card.PlaceButton = go.transform.Find("btn/Place")?.GetComponent<Button>();
            if (card.UseButton != null) card.UseButton.onClick.AddListener(card.OnUseClicked);
            if (card.PlaceButton != null) card.PlaceButton.onClick.AddListener(card.OnPlaceClicked);
            return card;
        }
    }
}
