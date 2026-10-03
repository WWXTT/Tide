using System.Collections.Generic;
using UnityEngine;

namespace SynergyUI
{
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
            public float TweenSpeed = 14f;             // exp 缓动速率（1/s）
        }

        private const string PrefabName = "ACard"; // 地址=文件名；路径由 UiKit.PrefabPath 派生

        private readonly RectTransform _layer;
        private readonly Config _cfg;
        private readonly List<HandFanCard> _cards = new List<HandFanCard>();
        private readonly Stack<HandFanCard> _pool = new Stack<HandFanCard>();
        private readonly Dictionary<uint, HandFanCard> _alive = new Dictionary<uint, HandFanCard>();
        private GameObject _prefab;
        private Vector2 _cardSize;   // 动态读取的卡牌尺寸（卡面改版只动 prefab，布局自适应）
        private HandFanCard _hovered;

        public HandFanView(RectTransform layer, Config cfg)
        {
            _layer = layer;
            _cfg = cfg;
        }

        /// <summary>扇区当前卡数。</summary>
        public int Count => _cards.Count;

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
            foreach (var c in _cards) _alive[c.RuntimeId] = c;
            _cards.Clear();

            if (data != null)
            {
                foreach (var d in data)
                {
                    if (!_alive.Remove(d.RuntimeId, out var card))
                    {
                        card = _pool.Count > 0 ? _pool.Pop() : Create();
                        card.RuntimeId = d.RuntimeId;
                        card.Fresh = true;
                    }
                    card.Face.Apply(d.Item, CardOverlayLayout.Full);
                    card.Face.SetFaceUp(d.FaceUp);
                    card.gameObject.SetActive(true);
                    _cards.Add(card);
                }
            }
            foreach (var leftover in _alive.Values)
            {
                leftover.gameObject.SetActive(false);
                _pool.Push(leftover);
            }
            _alive.Clear();

            if (_hovered != null && !_cards.Contains(_hovered)) _hovered = null;
            RebuildTargets();
        }

        /// <summary>重算全部卡的布局目标（间距压缩→谷形弧位姿→悬停态叠加→兄弟序）。</summary>
        private void RebuildTargets()
        {
            int n = _cards.Count;
            float cardW = _cardSize.x * _cfg.BaseScale;
            float cardH = _cardSize.y * _cfg.BaseScale;
            float spacing = n <= 1 ? 0f : Mathf.Min(cardW * _cfg.FullSpacingRatio, _cfg.MaxFanWidth / (n - 1));
            float push = _cfg.NeighborPushRatio * cardW;

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

                var pos = _cfg.Center + new Vector2(x, _cfg.TopSide ? -y : y);
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

        /// <summary>帧驱动：全部活跃卡向目标 exp 缓动（宿主挂 UiKit.Updater 调用）。</summary>
        public void Tick()
        {
            if (_cards.Count == 0) return;
            float t = 1f - Mathf.Exp(-Mathf.Min(Time.deltaTime, 0.05f) * _cfg.TweenSpeed);
            for (int i = 0; i < _cards.Count; i++)
            {
                var c = _cards[i];
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
                if (c != null) Object.Destroy(c.gameObject);
            _cards.Clear();
            while (_pool.Count > 0)
            {
                var c = _pool.Pop();
                if (c != null) Object.Destroy(c.gameObject);
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
                ? Object.Instantiate(_prefab, _layer, false)
                : new GameObject("CardFallback", typeof(RectTransform));
            var face = go.AddComponent<CardOverlayCard>();
            face.Bind();
            var card = go.AddComponent<HandFanCard>();
            card.Face = face;
            card.Fan = this;
            return card;
        }
    }
}
