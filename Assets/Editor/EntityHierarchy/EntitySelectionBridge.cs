#if UNITY_EDITOR
using System;
using System.Reflection;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

namespace EntityHierarchy
{
    /// <summary>
    /// 对 Unity.Entities.Editor.EntitySelectionProxy 的反射封装。
    ///
    /// 该类型是 internal，本 assembly（Assembly-CSharp-Editor）无法直接引用，
    /// 因此统一走反射。类型/成员解析一次并缓存；任一成员缺失（Entities 包升级
    /// 改了内部 API）时降级为「无选中」，不抛异常。
    /// </summary>
    internal static class EntitySelectionBridge
    {
        private static bool _resolved;
        private static Type _proxyType;
        private static PropertyInfo _entityProp;
        private static PropertyInfo _worldProp;
        private static PropertyInfo _existsProp;
        private static MethodInfo _selectEntityMethod;

        private static void Resolve()
        {
            if (_resolved) return;
            _resolved = true;

            try
            {
                var asm = Assembly.Load("Unity.Entities.Editor");
                _proxyType = asm.GetType("Unity.Entities.Editor.EntitySelectionProxy");
                if (_proxyType == null)
                {
                    Debug.LogWarning("[EntityHierarchy] EntitySelectionProxy type not found");
                    return;
                }

                _entityProp = _proxyType.GetProperty("Entity");
                _worldProp = _proxyType.GetProperty("World");
                _existsProp = _proxyType.GetProperty("Exists");
                _selectEntityMethod = _proxyType.GetMethod(
                    "SelectEntity",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(World), typeof(Entity) },
                    null);

                // 首次解析时记录一次，后续不再打印
                if (_entityProp == null || _worldProp == null || _existsProp == null || _selectEntityMethod == null)
                {
                    Debug.LogWarning($"[EntityHierarchy] Resolved EntitySelectionProxy but missing members: Entity={_entityProp != null}, World={_worldProp != null}, Exists={_existsProp != null}, SelectEntity={_selectEntityMethod != null}");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[EntityHierarchy] 无法解析 EntitySelectionProxy: {e.Message}");
                _proxyType = null;
            }
        }

        /// <summary>在 Inspector 中选中指定 entity。返回是否成功。</summary>
        public static bool SelectEntity(World world, Entity entity)
        {
            Resolve();
            if (_selectEntityMethod == null || world == null || !world.IsCreated) return false;

            _selectEntityMethod.Invoke(null, new object[] { world, entity });
            return true;
        }

        /// <summary>
        /// 若当前 Selection 是一个存活的 entity 代理，取出其 world/entity。
        /// </summary>
        public static bool TryGetSelectedEntity(out World world, out Entity entity)
        {
            world = null;
            entity = Entity.Null;

            return TryGetSelectedEntity(out world, out entity, out _);
        }

        /// <summary>
        /// 同上，但额外输出失败原因，供诊断使用。
        /// </summary>
        public static bool TryGetSelectedEntity(out World world, out Entity entity, out string reason)
        {
            world = null;
            entity = Entity.Null;
            reason = null;

            Resolve();
            if (_proxyType == null || _entityProp == null || _worldProp == null || _existsProp == null)
            {
                reason = "reflection members missing";
                return false;
            }

            var obj = Selection.activeObject;
            if (obj == null)
            {
                reason = "activeObject is null";
                return false;
            }

            if (!_proxyType.IsInstanceOfType(obj))
            {
                reason = $"activeObject is {obj.GetType().FullName}, not proxy";
                return false;
            }

            if (!(bool)_existsProp.GetValue(obj))
            {
                reason = "proxy.Exists is false";
                return false;
            }

            entity = (Entity)_entityProp.GetValue(obj);
            world = (World)_worldProp.GetValue(obj);

            if (world == null || !world.IsCreated)
            {
                reason = "proxy.World is null or not created";
                return false;
            }

            return true;
        }
    }
}
#endif
