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

            // 绘制包围盒（如果有 WorldRenderBounds）
            if (em.HasComponent<WorldRenderBounds>(entity))
            {
                var bounds = em.GetComponentData<WorldRenderBounds>(entity);
                Handles.color = new Color(0, 1, 0, 0.8f);
                Handles.DrawWireCube(bounds.Value.Center, bounds.Value.Size);
            }

            // 绘制位置标记（如果有 LocalToWorld）
            if (em.HasComponent<Unity.Transforms.LocalToWorld>(entity))
            {
                var ltw = em.GetComponentData<Unity.Transforms.LocalToWorld>(entity);
                Handles.color = Color.cyan;
                Handles.SphereHandleCap(0, ltw.Position, Quaternion.identity, 0.5f, EventType.Repaint);

                // 绘制标签
                var style = new GUIStyle();
                style.normal.textColor = Color.white;
                style.fontSize = 12;
                Vector3 labelPos = ltw.Position;
                labelPos.y += 1f;
                Handles.Label(labelPos, $"Entity {entity.Index}", style);
            }
        }
    }
}
#endif
