#if UNITY_EDITOR
using Unity.Entities;
using Unity.Rendering;
using UnityEditor;
using UnityEngine;

namespace EntityHierarchy
{
    /// <summary>
    /// 在 Scene 视图中为选中的 Entity 绘制视觉反馈
    /// </summary>
    [InitializeOnLoad]
    public static class EntitySceneGizmos
    {
        static EntitySceneGizmos()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        static void OnSceneGUI(SceneView sceneView)
        {
            if (!EntitySelectionBridge.TryGetSelectedEntity(out var world, out var entity))
                return;

            var em = world.EntityManager;
            if (!em.Exists(entity)) return;

            // 绘制包围盒（如果有 WorldRenderBounds）——标签锚点同源：框在哪名字在哪
            Unity.Mathematics.AABB? aabb = null;
            if (em.HasComponent<WorldRenderBounds>(entity))
            {
                aabb = em.GetComponentData<WorldRenderBounds>(entity).Value;
                Handles.color = new Color(0, 1, 0, 0.8f);
                Handles.DrawWireCube(aabb.Value.Center, aabb.Value.Size);
            }

            // 绘制位置标记（如果有 LocalToWorld）——语义是变换点，与包围盒中心可能不同（网格世界空间 + 单位变换时在原点）
            if (em.HasComponent<Unity.Transforms.LocalToWorld>(entity))
            {
                var ltw = em.GetComponentData<Unity.Transforms.LocalToWorld>(entity);
                Handles.color = Color.cyan;
                Handles.SphereHandleCap(0, ltw.Position, Quaternion.identity, 0.5f, EventType.Repaint);
            }

            // 绘制标签：文本与层级树同行显示名；位置贴包围盒上方，无框退回变换点 +1
            Vector3? labelPos = null;
            if (aabb.HasValue)
                labelPos = (Vector3)aabb.Value.Center + Vector3.up * (aabb.Value.Extents.y + 0.5f);
            else if (em.HasComponent<Unity.Transforms.LocalToWorld>(entity))
                labelPos = (Vector3)em.GetComponentData<Unity.Transforms.LocalToWorld>(entity).Position + Vector3.up;

            if (labelPos.HasValue)
            {
                var style = new GUIStyle();
                style.normal.textColor = Color.white;
                style.fontSize = 12;
                Handles.Label(labelPos.Value, AuthoringTreeView.GetEntityDisplayName(em, entity), style);
            }
        }
    }
}
#endif
