using System;
using System.Collections.Generic;
using UnityEngine;

namespace CardCore
{
    /// <summary>
    /// 事件数据基接口
    /// 广播事件会触发所有订阅者，定向事件需要指定接收者ID
    /// </summary>
    public interface IEventData { }

    /// <summary>
    /// 定向事件数据接口
    /// </summary>
    public interface ITargetedEventData : IEventData
    {
        int TargetId { get; }
    }

    /// <summary>
    /// 统一事件管理器
    /// 整合了原 GameEventBus（游戏逻辑事件）和 EventManager（UI事件）的功能
    /// 支持广播和定向两种事件模式
    /// </summary>
    public sealed class EventManager
    {
        private static EventManager _instance;
        public static EventManager Instance => _instance ??= new EventManager();

        // 广播事件处理程序
        private readonly Dictionary<Type, List<Func<IEventData, bool>>> _broadcastHandlers =
             new Dictionary<Type, List<Func<IEventData, bool>>>(64);

        // 定向事件处理程序（指定接收者ID）
        private readonly Dictionary<Type, Dictionary<int, List<Func<IEventData, bool>>>> _targetedHandlers =
            new Dictionary<Type, Dictionary<int, List<Func<IEventData, bool>>>>(64);

        // 对象池
        private readonly Stack<List<Func<IEventData, bool>>> _listPool = new Stack<List<Func<IEventData, bool>>>();

        #region 订阅

        /// <summary>订阅广播事件（Func 版，返回是否执行成功）</summary>
        public void Subscribe<T>(Func<T, bool> handler, int receiverId = -1) where T : IEventData
        {
            var eventType = typeof(T);

            if (receiverId == -1)
            {
                if (!_broadcastHandlers.TryGetValue(eventType, out var handlers))
                {
                    handlers = new List<Func<IEventData, bool>>(4);
                    _broadcastHandlers[eventType] = handlers;
                }
                handlers.Add(evt => handler((T)evt));
            }
            else
            {
                if (!_targetedHandlers.TryGetValue(eventType, out var idToHandlers))
                {
                    idToHandlers = new Dictionary<int, List<Func<IEventData, bool>>>();
                    _targetedHandlers[eventType] = idToHandlers;
                }
                if (!idToHandlers.TryGetValue(receiverId, out var handlers))
                {
                    handlers = new List<Func<IEventData, bool>>(4);
                    idToHandlers[receiverId] = handlers;
                }
                handlers.Add(evt => handler((T)evt));
            }
        }

        /// <summary>订阅广播事件（Action 版，无需返回值）</summary>
        public void Subscribe<T>(Action<T> handler) where T : IEventData
        {
            Subscribe<T>(e => { handler(e); return true; }, -1);
        }

        /// <summary>取消订阅</summary>
        public void Unsubscribe<T>(Func<T, bool> handler, int receiverId = -1) where T : IEventData
        {
            var eventType = typeof(T);

            if (receiverId == -1)
            {
                if (_broadcastHandlers.TryGetValue(eventType, out var handlers))
                {
                    for (int i = handlers.Count - 1; i >= 0; i--)
                    {
                        if (handlers[i].Target == handler.Target)
                        {
                            handlers.RemoveAt(i);
                            break;
                        }
                    }
                    if (handlers.Count == 0) _broadcastHandlers.Remove(eventType);
                }
            }
            else
            {
                if (_targetedHandlers.TryGetValue(eventType, out var idToHandlers))
                {
                    if (idToHandlers.TryGetValue(receiverId, out var handlers))
                    {
                        for (int i = handlers.Count - 1; i >= 0; i--)
                        {
                            if (handlers[i].Target == handler.Target)
                            {
                                handlers.RemoveAt(i);
                                break;
                            }
                        }
                        if (handlers.Count == 0)
                        {
                            idToHandlers.Remove(receiverId);
                            if (idToHandlers.Count == 0)
                                _targetedHandlers.Remove(eventType);
                        }
                    }
                }
            }
        }

        /// <summary>取消订阅（Action 版）</summary>
        public void Unsubscribe<T>(Action<T> handler) where T : IEventData
        {
            Unsubscribe<T>(e => { handler(e); return true; }, -1);
        }

        #endregion

        #region 发布

        /// <summary>广播发布事件（无 targetId）</summary>
        public bool Publish<T>(T eventData) where T : IEventData
        {
            return Publish(eventData, -1);
        }

        /// <summary>发布事件（支持定向）</summary>
        public bool Publish<T>(T eventData, int targetId) where T : IEventData
        {
            var eventType = typeof(T);
            List<Func<IEventData, bool>> handlersToInvoke = GetHandlerListFromPool();
            bool allSuccess = true;

            try
            {
                if (targetId == -1) // 广播事件
                {
                    if (_broadcastHandlers.TryGetValue(eventType, out var handlers))
                    {
                        handlersToInvoke.AddRange(handlers);
                    }

                    if (handlersToInvoke.Count == 0) return false;

                    foreach (var handler in handlersToInvoke)
                    {
                        try
                        {
                            if (!IsHandlerValid(handler)) continue;
                            if (!handler(eventData))
                            {
                                allSuccess = false;
                            }
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"Event handler error: {e}");
                            allSuccess = false;
                        }
                    }
                    return allSuccess;
                }
                else // 定向事件
                {
                    if (_targetedHandlers.TryGetValue(eventType, out var idToHandlers) &&
                        idToHandlers.TryGetValue(targetId, out var handlers))
                    {
                        handlersToInvoke.AddRange(handlers);
                    }

                    if (handlersToInvoke.Count == 0) return false;

                    foreach (var handler in handlersToInvoke)
                    {
                        try
                        {
                            if (!IsHandlerValid(handler)) continue;
                            if (!handler(eventData))
                            {
                                return false;
                            }
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"Event handler error: {e}");
                            return false;
                        }
                    }
                    return true;
                }
            }
            finally
            {
                handlersToInvoke.Clear();
                if (_listPool.Count < 16)
                {
                    _listPool.Push(handlersToInvoke);
                }
            }
        }

        /// <summary>
        /// 按运行时类型广播发布事件。
        /// 当事件被替代效果替换为另一具体类型（例如 CardDestroyEvent → CardBanishEvent）时，
        /// 泛型 Publish&lt;T&gt; 会以静态类型 T 作为键而漏掉真实订阅者；此方法以 eventData.GetType() 为键分发。
        /// </summary>
        public bool PublishDynamic(IEventData eventData)
        {
            if (eventData == null) return false;

            var eventType = eventData.GetType();
            List<Func<IEventData, bool>> handlersToInvoke = GetHandlerListFromPool();
            bool allSuccess = true;

            try
            {
                if (_broadcastHandlers.TryGetValue(eventType, out var handlers))
                {
                    handlersToInvoke.AddRange(handlers);
                }

                if (handlersToInvoke.Count == 0) return false;

                foreach (var handler in handlersToInvoke)
                {
                    try
                    {
                        if (!IsHandlerValid(handler)) continue;
                        if (!handler(eventData))
                        {
                            allSuccess = false;
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Event handler error: {e}");
                        allSuccess = false;
                    }
                }
                return allSuccess;
            }
            finally
            {
                handlersToInvoke.Clear();
                if (_listPool.Count < 16)
                {
                    _listPool.Push(handlersToInvoke);
                }
            }
        }

        #endregion

        #region 维护

        /// <summary>清空所有订阅者</summary>
        public void ClearAll()
        {
            _broadcastHandlers.Clear();
            _targetedHandlers.Clear();
            _listPool.Clear();
        }

        /// <summary>获取当前订阅者数量</summary>
        public int ObserverCount => _broadcastHandlers.Count + _targetedHandlers.Count;

        /// <summary>清理无效的处理器</summary>
        public void CleanupInvalidHandlers()
        {
            CleanupBroadcastHandlers();
            CleanupTargetedHandlers();
        }

        #endregion

        #region 内部方法

        private bool IsHandlerValid(Func<IEventData, bool> handler)
        {
            return !(handler.Target is UnityEngine.Object obj) || obj;
        }

        private List<Func<IEventData, bool>> GetHandlerListFromPool()
        {
            return _listPool.Count > 0 ? _listPool.Pop() : new List<Func<IEventData, bool>>(16);
        }

        private void CleanupBroadcastHandlers()
        {
            var typesToRemove = new List<Type>();

            foreach (var kvp in _broadcastHandlers)
            {
                var handlers = kvp.Value;
                handlers.RemoveAll(handler => !IsHandlerValid(handler));

                if (handlers.Count == 0)
                {
                    typesToRemove.Add(kvp.Key);
                }
            }

            foreach (var type in typesToRemove)
            {
                _broadcastHandlers.Remove(type);
            }
        }

        private void CleanupTargetedHandlers()
        {
            var typesToRemove = new List<Type>();

            foreach (var typeKvp in _targetedHandlers)
            {
                var idToHandlers = typeKvp.Value;
                var idsToRemove = new List<int>();

                foreach (var idKvp in idToHandlers)
                {
                    var handlers = idKvp.Value;
                    handlers.RemoveAll(handler => !IsHandlerValid(handler));

                    if (handlers.Count == 0)
                    {
                        idsToRemove.Add(idKvp.Key);
                    }
                }

                foreach (var id in idsToRemove)
                {
                    idToHandlers.Remove(id);
                }

                if (idToHandlers.Count == 0)
                {
                    typesToRemove.Add(typeKvp.Key);
                }
            }

            foreach (var type in typesToRemove)
            {
                _targetedHandlers.Remove(type);
            }
        }

        #endregion
    }

    #region 扩展方法

    public interface IEventHandler<T> where T : IEventData { }

    public static class EventHandlerExtensions
    {
        public static void RegisterHandler<T>(
            this IEventHandler<T> handler,
            Func<T, bool> handlerAction,
            int receiverId = -1) where T : IEventData
        {
            EventManager.Instance.Subscribe(handlerAction, receiverId);
        }

        public static void UnregisterHandler<T>(
            this IEventHandler<T> handler,
            Func<T, bool> handlerAction,
            int receiverId = -1) where T : IEventData
        {
            EventManager.Instance.Unsubscribe(handlerAction, receiverId);
        }
    }

    public interface IEventTrigger { }

    public static class EventTriggerExtensions
    {
        public static bool TriggerEvent<T>(this IEventTrigger eventTrigger, T eventData, int receiverId = -1)
            where T : IEventData
        {
            return EventManager.Instance.Publish(eventData, receiverId);
        }
    }

    #endregion
}
