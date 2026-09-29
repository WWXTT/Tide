using System;
using System.Collections.Generic;

namespace CardCore
{
    /// <summary>
    /// 子系统注册表
    /// 管理所有游戏子系统的注册和查询
    /// </summary>
    public class SubSystemRegistry
    {
        private readonly Dictionary<Type, object> _systems = new Dictionary<Type, object>();

        /// <summary>
        /// 注册子系统
        /// </summary>
        public void Register<T>(T system) where T : class
        {
            _systems[typeof(T)] = system;
        }

        /// <summary>
        /// 获取子系统
        /// </summary>
        public T Get<T>() where T : class
        {
            if (_systems.TryGetValue(typeof(T), out var system))
                return (T)system;
            return null;
        }

        /// <summary>
        /// 尝试获取子系统，不存在时返回默认值
        /// </summary>
        public T GetOrDefault<T>(T defaultValue = default) where T : class
        {
            return Get<T>() ?? defaultValue;
        }

        /// <summary>
        /// 清空所有已注册的子系统
        /// </summary>
        public void ClearAll()
        {
            _systems.Clear();
        }
    }
}
