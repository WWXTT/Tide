#if UNITY_EDITOR
using UnityEditor.IMGUI.Controls;
using Unity.Entities;
using Unity.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using HexMap;

namespace EntityHierarchy
{
    public class AuthoringTreeView : TreeView<int>
    {
        private const string RUNTIME_GROUP = "Runtime Created";

        public AuthoringTreeView(TreeViewState<int> state) : base(state)
        {
            showAlternatingRowBackgrounds = true;
            showBorder = true;
            Reload();
        }

        public void Rebuild()
        {
            Reload();
        }

        protected override TreeViewItem<int> BuildRoot()
        {
            var root = new TreeViewItem<int> { id = 0, depth = -1, displayName = "Root" };
            var allItems = new List<TreeViewItem<int>>();

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                SetupParentsAndChildrenFromDepths(root, allItems);
                return root;
            }

            var em = world.EntityManager;
            int nextId = 1;

            // 收集所有带 EntityDebugLabel 的 Entity
            var query = em.CreateEntityQuery(typeof(EntityDebugLabel));
            var entities = query.ToEntityArray(Allocator.Temp);

            // 按 Group 分组
            var groups = new Dictionary<string, List<Entity>>();
            foreach (var entity in entities)
            {
                if (!em.Exists(entity)) continue;

                var label = em.GetComponentData<EntityDebugLabel>(entity);
                var groupName = label.Group.ToString();

                if (string.IsNullOrEmpty(groupName))
                    groupName = RUNTIME_GROUP;

                if (!groups.ContainsKey(groupName))
                    groups[groupName] = new List<Entity>();
                groups[groupName].Add(entity);
            }

            entities.Dispose();

            // 构建树：每个 Group 是一个父节点
            foreach (var kvp in groups.OrderBy(g => g.Key))
            {
                var groupName = kvp.Key;
                var entityList = kvp.Value;

                // 父节点：Group
                var groupItem = new TreeViewItem<int>
                {
                    id = nextId++,
                    depth = 0,
                    displayName = $"{groupName} ({entityList.Count})"
                };
                allItems.Add(groupItem);

                // 子节点：Entity
                foreach (var entity in entityList)
                {
                    if (!em.Exists(entity)) continue;

                    var entityItem = new EntityTreeViewItem(
                        id: nextId++,
                        depth: 1,
                        displayName: GetEntityDisplayName(em, entity),
                        entity: entity,
                        world: world
                    );
                    allItems.Add(entityItem);
                }
            }

            // 可选：收集没有 EntityDebugLabel 的 Entity（如果 ENABLE_HEX_DEBUG_LABEL 未定义）
            // 这部分可以后续扩展

            SetupParentsAndChildrenFromDepths(root, allItems);
            return root;
        }

        protected override void SingleClickedItem(int id)
        {
            var item = FindItem(id, rootItem) as EntityTreeViewItem;
            if (item?.entity == null || item.world == null) return;

            // 选中到 Inspector
            SelectEntityInInspector(item.world, item.entity);
        }

        protected override void DoubleClickedItem(int id)
        {
            var item = FindItem(id, rootItem) as EntityTreeViewItem;
            if (item?.entity == null || item.world == null) return;

            // Frame 相机到 Entity 位置
            FrameEntity(item.world.EntityManager, item.entity);
        }

        public void HighlightEntity(World world, Entity entity)
        {
            // 递归搜索整棵树（GetRows 只含可见行，分组折叠时子节点不在其中）
            var match = FindEntityItem(rootItem, world, entity);
            if (match == null)
            {
                // 树可能是旧的（entity 尚未构建进树），重建后再找一次
                Rebuild();
                match = FindEntityItem(rootItem, world, entity);
                if (match == null)
                    return;
            }

            // 展开所有祖先分组，保证目标行可见后选中
            for (var parent = match.parent; parent != null && parent != rootItem; parent = parent.parent)
                SetExpanded(parent.id, true);

            SetSelection(new List<int> { match.id });
            FrameItem(match.id);
        }

        private static EntityTreeViewItem FindEntityItem(TreeViewItem<int> root, World world, Entity entity)
        {
            if (root?.children == null)
                return null;

            foreach (var child in root.children)
            {
                if (child is EntityTreeViewItem entityItem &&
                    entityItem.world == world &&
                    entityItem.entity.Equals(entity))
                    return entityItem;

                var found = FindEntityItem(child, world, entity);
                if (found != null)
                    return found;
            }
            return null;
        }

        /// <summary>实体显示名（层级树行文本；EntitySceneGizmos 场景标签同源复用）。</summary>
        internal static string GetEntityDisplayName(EntityManager em, Entity entity)
        {
            // HexCellData 内含轴向坐标，直接显示 hex 坐标最直观
            if (em.HasComponent<HexCellData>(entity))
            {
                var cell = em.GetComponentData<HexCellData>(entity);
                var coords = cell.Coordinates;
                return $"Hex({coords.X}, {coords.Z})  elev {cell.Elevation}";
            }

            // 通用显示：Entity Index + 首个非标签组件类型
            var types = em.GetComponentTypes(entity, Allocator.Temp);
            string mainType = "Empty";
            for (int i = 0; i < types.Length; i++)
            {
                var managed = types[i].GetManagedType();
                if (managed == null || managed == typeof(EntityDebugLabel))
                    continue;
                mainType = managed.Name;
                break;
            }
            types.Dispose();

            return $"Entity {entity.Index}:{entity.Version} [{mainType}]";
        }

        private void SelectEntityInInspector(World world, Entity entity)
        {
            // 获取 AuthoringHierarchyWindow 实例，设置忽略标志避免循环
            var window = EditorWindow.GetWindow<AuthoringHierarchyWindow>(false, null, false);
            if (window != null)
            {
                window.IgnoreNextSelectionChange = true;
            }

            // 使用 bridge 调用 EntitySelectionProxy.SelectEntity
            if (!EntitySelectionBridge.SelectEntity(world, entity))
            {
                Debug.LogWarning($"Failed to select entity {entity} in inspector.");
            }
        }

        private void FrameEntity(EntityManager em, Entity entity)
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null) return;

            Vector3 position = Vector3.zero;
            Quaternion rotation = Quaternion.identity;
            bool hasTransform = false;

            // 优先使用 LocalToWorld
            if (em.HasComponent<Unity.Transforms.LocalToWorld>(entity))
            {
                var ltw = em.GetComponentData<Unity.Transforms.LocalToWorld>(entity);
                position = ltw.Position;
                rotation = ltw.Rotation;
                hasTransform = true;
            }
            // 或使用 LocalTransform (Entities 1.x)
            else if (em.HasComponent<Unity.Transforms.LocalTransform>(entity))
            {
                var lt = em.GetComponentData<Unity.Transforms.LocalTransform>(entity);
                position = lt.Position;
                rotation = lt.Rotation;
                hasTransform = true;
            }

            if (!hasTransform)
            {
                Debug.LogWarning($"Entity {entity} has no transform component to frame.");
                return;
            }

            // 如果有 WorldRenderBounds，使用 Frame（更精确）
            if (em.HasComponent<Unity.Rendering.WorldRenderBounds>(entity))
            {
                var bounds = em.GetComponentData<Unity.Rendering.WorldRenderBounds>(entity);
                sceneView.Frame(new Bounds(bounds.Value.Center, bounds.Value.Size), false);
            }
            else
            {
                // 否则只移动相机到位置，给个默认观察距离
                sceneView.LookAt(position, rotation, 5f);
            }

            sceneView.Repaint();
        }
    }
}
#endif
