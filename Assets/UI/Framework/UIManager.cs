using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SynergyUI
{
    /// <summary>
    /// 运行时界面栈管理器。
    /// 负责：加载界面 UXML、装配到根容器、调用生命周期、维护导航历史。
    ///
    /// 用法：
    ///   manager.Show&lt;MainMenuScreen&gt;();   // 压栈进入新界面
    ///   manager.Back();                       // 返回上一界面
    ///   manager.Replace&lt;BattleScreen&gt;();  // 替换当前界面（不入历史）
    /// </summary>
    public sealed class UIManager
    {
        // 根容器：UIDocument.rootVisualElement，所有界面挂在它下面。
        private readonly VisualElement _root;

        // 导航历史栈，栈顶为当前界面。
        private readonly Stack<UIScreen> _stack = new Stack<UIScreen>();

        // 已实例化界面缓存（每种界面只 new 一次，复用实例）。
        private readonly Dictionary<Type, UIScreen> _cache = new Dictionary<Type, UIScreen>();

        public UIManager(VisualElement root)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
        }

        /// <summary>当前栈顶界面，无则返回 null。</summary>
        public UIScreen Current => _stack.Count > 0 ? _stack.Peek() : null;

        /// <summary>压栈进入新界面，隐藏（OnExit）当前界面。</summary>
        public T Show<T>() where T : UIScreen, new()
        {
            DeactivateCurrent();
            var screen = GetOrCreate<T>();
            _stack.Push(screen);
            Activate(screen);
            return screen;
        }

        /// <summary>替换当前界面（弹出当前并销毁其显示，压入新界面，历史深度不变）。</summary>
        public T Replace<T>() where T : UIScreen, new()
        {
            if (_stack.Count > 0)
            {
                var top = _stack.Pop();
                Deactivate(top);
            }
            var screen = GetOrCreate<T>();
            _stack.Push(screen);
            Activate(screen);
            return screen;
        }

        /// <summary>返回上一界面。若已在栈底则无操作。</summary>
        public void Back()
        {
            if (_stack.Count <= 1)
            {
                return;
            }
            var top = _stack.Pop();
            Deactivate(top);
            Activate(_stack.Peek());
        }

        private void DeactivateCurrent()
        {
            if (_stack.Count > 0)
            {
                Deactivate(_stack.Peek());
            }
        }

        private void Activate(UIScreen screen)
        {
            // 重新装配根容器：清空再 clone UXML，保证界面状态干净。
            _root.Clear();

            var tree = Resources.Load<VisualTreeAsset>(screen.UxmlResourcePath);
            if (tree == null)
            {
                Debug.LogError($"[UIManager] 找不到 UXML: Resources/{screen.UxmlResourcePath}");
                return;
            }

            var container = tree.Instantiate();
            // 让界面填满整个根容器。
            container.style.flexGrow = 1;
            _root.Add(container);

            screen.Bind(this, container);
            screen.OnEnter();
        }

        private void Deactivate(UIScreen screen)
        {
            screen.FlushSubscriptions();
            screen.OnExit();
        }

        private T GetOrCreate<T>() where T : UIScreen, new()
        {
            var type = typeof(T);
            if (!_cache.TryGetValue(type, out var screen))
            {
                screen = new T();
                _cache[type] = screen;
            }
            return (T)screen;
        }
    }
}
