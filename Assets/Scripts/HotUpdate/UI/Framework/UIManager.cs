using System;
using System.Collections.Generic;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>
    /// 运行时界面栈管理器（2026-10-01 预制体化定案）：根容器=bootstrap 自身 RectTransform
    /// （Canvas 根，无中间包装层）。每屏生命周期自治——进屏实例化自己的预制体
    /// （UIScreen.PrefabAssetPath，缺省屏纯代码构建），离屏销毁自己的实例
    /// （UIScreen.DestroyRoot）；不再 ClearRoot 清根（主菜单等内容不在运行时被连坐销毁）。
    ///
    /// 用法与语义不变：
    ///   manager.Show&lt;MainMenuScreen&gt;();   // 压栈进入新界面
    ///   manager.Back();                       // 返回上一界面
    ///   manager.Replace&lt;BattleScreen&gt;();  // 替换当前界面（不入历史）
    /// 界面实例按类型缓存复用（C# 实例与字段状态保留，层级每进屏重建）。
    /// </summary>
    public sealed class UIManager
    {
        // 根容器：bootstrap 的 ScreenRoot（Canvas 下全屏层）。
        private readonly RectTransform _root;

        // 导航历史栈，栈顶为当前界面。
        private readonly Stack<UIScreen> _stack = new Stack<UIScreen>();

        // 已实例化界面缓存（每种界面只 new 一次，复用实例；层级离屏销毁、进屏重建）。
        private readonly Dictionary<Type, UIScreen> _cache = new Dictionary<Type, UIScreen>();

        public UIManager(RectTransform root)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
        }

        /// <summary>当前栈顶界面，无则返回 null。</summary>
        public UIScreen Current => _stack.Count > 0 ? _stack.Peek() : null;

        /// <summary>压栈进入新界面，隐藏（OnExit+销毁层级）当前界面。</summary>
        public T Show<T>() where T : UIScreen, new()
        {
            DeactivateCurrent();
            var screen = GetOrCreate<T>();
            _stack.Push(screen);
            Activate(screen);
            return screen;
        }

        /// <summary>替换当前界面（弹出当前并压入新界面，历史深度不变）。</summary>
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
            screen.Mount(this, _root); // Bind → 建/实例化 Root → Build（绑定）→ OnEnter
        }

        private void Deactivate(UIScreen screen)
        {
            screen.FlushSubscriptions();
            screen.OnExit();
            screen.DestroyRoot(); // 每屏只销毁自己的实例，不动根下其他内容
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
