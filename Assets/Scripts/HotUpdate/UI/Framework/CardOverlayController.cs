using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
// Button 与工厂方法撞名——控制器内部类型引用走别名
using UguiButton = UnityEngine.UI.Button;

namespace SynergyUI
{
    /// <summary>一条 UGUI 卡绑定：槽位（屏内 RectTransform）+ 显示模型 + 布局形态 + 点击（卡面/移除角标）。</summary>
    public sealed class CardOverlayBinding
    {
        public RectTransform Slot;
        public CardOverlayItem Item;
        public CardOverlayLayout Layout;
        public Action OnClick;          // 点卡面（可空=不可点，如对方单位）
        public Action OnRemoveClick;    // 点右上角×（构筑区移除；可空=隐藏角标）
    }

    /// <summary>
    /// UGUI 卡牌层（2026-10-01 预制体化定案）：卡实例直接挂进屏内槽位 RectTransform
    /// （stretch 填满），同 Canvas 兄弟序天然正确，RectMask2D 自动裁剪滚动区。
    /// prefab=Assets/Art/UI/ACard.prefab（子物体命名与 CardOverlayCard.Bind 一一对应），
    /// 池化复用 + Apply 填卡 + 点击/移除接线。生命周期：界面 OnExit 调 ClearActive 回收。
    /// </summary>
    public sealed class CardOverlayController : MonoBehaviour
    {
        private const string PrefabAddress = "ACard";
        private const string PrefabAssetPath = "Assets/Art/UI/ACard.prefab";

        private sealed class LiveCard
        {
            public GameObject Go;
            public CardOverlayCard View;
            public UguiButton ClickButton;
            public UguiButton RemoveButton;
            public CardOverlayBinding Binding;
        }

        private static CardOverlayController _instance;
        private GameObject _prefab;
        private readonly List<LiveCard> _live = new List<LiveCard>();
        private readonly Stack<LiveCard> _pool = new Stack<LiveCard>();

        public static CardOverlayController Instance => EnsureCreated();

        private static CardOverlayController EnsureCreated()
        {
            if (_instance == null)
            {
                var go = new GameObject("CardLayerPool");
                DontDestroyOnLoad(go);
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

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        /// <summary>全量重绑（按 Item.Key 增量复用池）；卡实例挂进各 binding.Slot 并 stretch 填满。</summary>
        public void Bind(IReadOnlyList<CardOverlayBinding> bindings)
        {
            // 先全部回池（槽位每次全量重建，简单归还再按需取用）
            for (int i = 0; i < _live.Count; i++)
            {
                _live[i].Go.SetActive(false);
                _live[i].Go.transform.SetParent(transform, false);
                _pool.Push(_live[i]);
            }
            _live.Clear();

            if (bindings == null) return;
            var used = new HashSet<string>();
            foreach (var b in bindings)
            {
                if (b?.Item?.Key == null || b.Slot == null) continue;
                if (!used.Add(b.Item.Key)) continue; // 同键只挂一处

                var live = _pool.Count > 0 ? _pool.Pop() : Create();
                var rt = (RectTransform)live.Go.transform;
                rt.SetParent(b.Slot, false);
                UiKit.Stretch(rt);
                live.Go.SetActive(true);
                live.Binding = b;
                live.View.Apply(b.Item, b.Layout);
                live.View.SetRemoveVisible(b.OnRemoveClick != null);
                _live.Add(live);
            }
        }

        public void Clear()
        {
            foreach (var live in _live)
            {
                live.Go.SetActive(false);
                live.Go.transform.SetParent(transform, false);
                _pool.Push(live);
            }
            _live.Clear();
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
                ? Instantiate(_prefab, transform, false)
                : new GameObject("CardFallback", typeof(RectTransform));

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
