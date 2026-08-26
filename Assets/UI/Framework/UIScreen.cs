using System;
using System.Collections.Generic;
using CardCore;
using UnityEngine.UIElements;

namespace SynergyUI
{
    /// <summary>
    /// 所有运行时界面的抽象基类。
    /// 每个界面对应一份 UXML（通过 UxmlResourcePath 指定 Resources 路径），
    /// 由 UIManager 负责加载、装配、维护界面栈。
    ///
    /// 事件绑定底座：用 Subscribe&lt;T&gt; 订阅 EventManager 广播事件，
    /// 离开界面（OnExit）时框架自动退订 —— 因为 EventManager 对非
    /// UnityEngine.Object 的订阅者不会自动清理（见 Events/EventManager.cs
    /// IsHandlerValid），手动界面若不退订会留下悬空回调。
    /// </summary>
    public abstract class UIScreen
    {
        /// <summary>本界面根节点（UXML clone 后的容器），由 UIManager 注入。</summary>
        public VisualElement Root { get; private set; }

        /// <summary>所属界面管理器，用于导航（Show/Back）。</summary>
        protected UIManager Manager { get; private set; }

        /// <summary>UXML 在 Resources 下的路径（不含扩展名）。</summary>
        public abstract string UxmlResourcePath { get; }

        // 记录本界面的所有事件退订动作，OnExit 时统一执行。
        private readonly List<Action> _unsubscribers = new List<Action>();

        /// <summary>由 UIManager 在装配时调用，注入运行时依赖。</summary>
        internal void Bind(UIManager manager, VisualElement root)
        {
            Manager = manager;
            Root = root;
        }

        /// <summary>
        /// 订阅 EventManager 广播事件，并登记退订动作。
        /// 界面切走时框架自动退订，子类无需手动管理。
        /// </summary>
        protected void Subscribe<T>(Action<T> handler) where T : IEventData
        {
            EventManager.Instance.Subscribe(handler);
            _unsubscribers.Add(() => EventManager.Instance.Unsubscribe(handler));
        }

        /// <summary>查询根节点下的具名元素（语法糖）。</summary>
        protected T Q<T>(string name) where T : VisualElement => Root.Q<T>(name);

        /// <summary>界面进入时调用：在此查询控件、接线回调、填充初始数据。</summary>
        public virtual void OnEnter() { }

        /// <summary>界面离开时调用：框架已自动退订事件，子类可在此释放额外资源。</summary>
        public virtual void OnExit() { }

        /// <summary>框架内部：执行所有登记的事件退订。OnExit 之前调用。</summary>
        internal void FlushSubscriptions()
        {
            foreach (var unsub in _unsubscribers)
            {
                unsub();
            }
            _unsubscribers.Clear();
        }
    }
}
