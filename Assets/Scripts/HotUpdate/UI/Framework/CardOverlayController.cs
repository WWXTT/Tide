using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.UIElements;
// Button 与 UITK Button 撞名——UGUI 侧统一走别名
using UguiButton = UnityEngine.UI.Button;

namespace SynergyUI
{
    /// <summary>一条 UGUI 卡绑定：UITK 占位槽 + 显示模型 + 布局形态 + 点击（卡面 / 移除角标）。</summary>
    public sealed class CardOverlayBinding
    {
        public VisualElement Slot;
        public CardOverlayItem Item;
        public CardOverlayLayout Layout;
        public Action OnClick;          // 点卡面（可空=不可点，如对方单位）
        public Action OnRemoveClick;    // 点右上角×（构筑区移除；可空=隐藏角标）
    }

    /// <summary>
    /// UGUI 卡牌层（2026-09-30 方案2 定案：SSO Canvas 整层压 UITK——卡位在 UITK 留占位槽，
    /// 每帧把槽 worldBound 同步成卡 RectTransform；PanelSettings=ConstantPixelSize，两侧坐标
    /// 1:1，仅 y 翻转）。
    /// · 弹窗抑制：抑制元素（overlay/selector-overlay 等）可见时整层隐藏——SSO 恒压 UITK，
    ///   UITK 弹窗永远盖不过 UGUI 卡，只能反向让层。
    /// · 裁剪：占位槽超出裁剪容器（滚动视口）时隐藏卡——UGUI 不跟 UITK ScrollView 裁剪。
    /// · 输入：旧输入（activeInputHandler=0）→ StandaloneInputModule；卡面点击走
    ///   GraphicRaycaster，占位槽 pickingMode=Ignore 防 UITK 双响应。
    /// · 生命周期：界面 OnExit 调 ClearActive 回收；切屏后槽脱离面板（panel==null）自动隐藏。
    /// </summary>
    public sealed class CardOverlayController : MonoBehaviour
    {
        private const string PrefabAddress = "BattleCardUGUI";
        private const string PrefabAssetPath = "Assets/UI/Res/BattleCardUGUI.prefab";

        private sealed class LiveCard
        {
            public GameObject Go;
            public CardOverlayCard View;
            public UguiButton ClickButton;
            public UguiButton RemoveButton;
            public CardOverlayBinding Binding;
        }

        private static CardOverlayController _instance;
        private Canvas _canvas;
        private GameObject _prefab;
        private readonly List<LiveCard> _live = new List<LiveCard>();
        private readonly Stack<LiveCard> _pool = new Stack<LiveCard>();
        private readonly List<VisualElement> _suppressors = new List<VisualElement>();
        private VisualElement _clip;
        private bool _manualSuppressed;

        public static CardOverlayController Instance => EnsureCreated();

        private static CardOverlayController EnsureCreated()
        {
            if (_instance == null)
            {
                var go = new GameObject("CardOverlayCanvas");
                _instance = go.AddComponent<CardOverlayController>();
            }
            return _instance;
        }

        /// <summary>界面退出时清空本层（无实例时安全空操作）。</summary>
        public static void ClearActive()
        {
            if (_instance == null) return;
            _instance.Clear();
        }

        private void Awake()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 10; // SSO 恒在 UITK 面板之上；显式排序给未来多 Canvas 留空间
            gameObject.AddComponent<GraphicRaycaster>();

            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>(); // 旧输入（项目未装 Input System 包）
            }
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        /// <summary>全量重绑（按 Item.Key 增量复用池）。clip=滚动视口等裁剪容器；
        /// suppressors=任一可见即整层隐藏的 UITK 弹窗元素。</summary>
        public void Bind(IReadOnlyList<CardOverlayBinding> bindings, VisualElement clip = null,
            IReadOnlyList<VisualElement> suppressors = null)
        {
            _clip = clip;
            _suppressors.Clear();
            if (suppressors != null) _suppressors.AddRange(suppressors);

            var wanted = new HashSet<string>();
            foreach (var b in bindings)
            {
                if (b?.Item?.Key == null || b.Slot == null) continue;
                wanted.Add(b.Item.Key);
            }

            // 消失的卡回池
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                if (wanted.Contains(_live[i].Binding.Item.Key)) continue;
                _live[i].Go.SetActive(false);
                _pool.Push(_live[i]);
                _live.RemoveAt(i);
            }

            foreach (var b in bindings)
            {
                if (b?.Item?.Key == null || b.Slot == null) continue;

                LiveCard live = null;
                foreach (var existing in _live)
                {
                    if (existing.Binding.Item.Key == b.Item.Key) { live = existing; break; }
                }
                if (live == null)
                {
                    live = _pool.Count > 0 ? _pool.Pop() : Create();
                    live.Go.transform.SetParent(_canvas.transform, false);
                    _live.Add(live);
                }
                live.Binding = b;
                live.View.Apply(b.Item, b.Layout);
                live.View.SetRemoveVisible(b.OnRemoveClick != null);
            }
        }

        public void Clear()
        {
            foreach (var live in _live)
            {
                live.Go.SetActive(false);
                _pool.Push(live);
            }
            _live.Clear();
            _suppressors.Clear();
            _clip = null;
        }

        /// <summary>手动整层隐藏（与弹窗抑制元素取或）。</summary>
        public void SetSuppressed(bool value) => _manualSuppressed = value;

        private void LateUpdate()
        {
            bool suppressed = _manualSuppressed || AnySuppressorVisible();
            if (_canvas.enabled == suppressed) _canvas.enabled = !suppressed;
            if (suppressed) return;

            float h = _canvas.pixelRect.height;
            bool hasClip = _clip != null && _clip.panel != null;
            var clipBound = hasClip ? _clip.worldBound : Rect.zero;

            foreach (var live in _live)
            {
                var slot = live.Binding.Slot;
                bool alive = slot != null && slot.panel != null;
                var wb = alive ? slot.worldBound : Rect.zero;
                alive = alive && wb.width > 1f && wb.height > 1f;
                if (alive && hasClip && !wb.Overlaps(clipBound))
                    alive = false;
                if (live.Go.activeSelf != alive) live.Go.SetActive(alive);
                if (!alive) continue;

                // UITK worldBound 原点在左上、y 向下；Canvas anchor 左下、y 向上
                var rt = (RectTransform)live.Go.transform;
                rt.anchoredPosition = new Vector2(wb.center.x, h - wb.center.y);
                rt.sizeDelta = new Vector2(wb.width, wb.height);
            }
        }

        private bool AnySuppressorVisible()
        {
            foreach (var s in _suppressors)
            {
                if (s == null || s.panel == null) continue;
                if (s.resolvedStyle.display != DisplayStyle.None) return true;
            }
            return false;
        }

        private LiveCard Create()
        {
            if (_prefab == null)
            {
                _prefab = Tide.HotUpdate.HotUpdateAssets.Load<GameObject>(PrefabAddress, PrefabAssetPath);
                if (_prefab == null)
                    Debug.LogError($"[CardOverlay] 卡面 prefab 缺失：{PrefabAssetPath}（卡面将只有底板，请先构建 prefab）");
            }
            var go = _prefab != null
                ? Instantiate(_prefab, _canvas.transform, false)
                : new GameObject("CardFallback", typeof(RectTransform));
            if (_prefab == null) go.transform.SetParent(_canvas.transform, false);

            // 同步公式按左下角锚点算 anchoredPosition——prefab 根默认中心锚点会整体错位，强制归零
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);

            var view = go.AddComponent<CardOverlayCard>();
            view.Bind();

            var live = new LiveCard { Go = go, View = view };
            live.ClickButton = go.AddComponent<UguiButton>();
            live.ClickButton.transition = Selectable.Transition.None;
            live.ClickButton.onClick.AddListener(() => { if (live.Binding != null) live.Binding.OnClick?.Invoke(); });

            var removeGo = view.RemoveButton;
            if (removeGo != null)
            {
                live.RemoveButton = removeGo.AddComponent<UguiButton>();
                live.RemoveButton.transition = Selectable.Transition.None;
                live.RemoveButton.onClick.AddListener(() => { if (live.Binding != null) live.Binding.OnRemoveClick?.Invoke(); });
            }
            return live;
        }
    }
}
