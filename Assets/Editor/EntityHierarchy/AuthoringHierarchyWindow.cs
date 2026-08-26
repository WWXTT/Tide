#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using Unity.Entities;

namespace EntityHierarchy
{
    public class AuthoringHierarchyWindow : EditorWindow
    {
        [MenuItem("Window/Entities/Authoring Hierarchy")]
        static void Open()
        {
            var window = GetWindow<AuthoringHierarchyWindow>("Entity Hierarchy");
            window.Show();
        }

        private TreeViewState<int> _treeState;
        private AuthoringTreeView _treeView;
        private int _lastEntityCount = 0;
        private bool _ignoreSelectionChange = false;
        private bool _pendingSelectionSync = false;

        // 公开属性让 TreeView 可以设置标志
        public bool IgnoreNextSelectionChange
        {
            get => _ignoreSelectionChange;
            set => _ignoreSelectionChange = value;
        }

        void OnEnable()
        {
            Debug.Log("[EntityHierarchy] Window OnEnable - registering Selection.selectionChanged");
            _treeState = new TreeViewState<int>();
            _treeView = new AuthoringTreeView(_treeState);

            Selection.selectionChanged += OnSelectionChanged;
        }

        void OnDisable()
        {
            Debug.Log("[EntityHierarchy] Window OnDisable - unregistering Selection.selectionChanged");
            Selection.selectionChanged -= OnSelectionChanged;
        }

        void OnGUI()
        {
            // 性能优化：只在 Entity 数量变化时重建
            var world = World.DefaultGameObjectInjectionWorld;
            if (world?.IsCreated == true)
            {
                var currentCount = world.EntityManager.Debug.EntityCount;
                if (currentCount != _lastEntityCount)
                {
                    _treeView.Rebuild();
                    _lastEntityCount = currentCount;
                }
            }
            else
            {
                // World 不存在时显示提示
                EditorGUILayout.HelpBox("No active ECS World found. Enter Play Mode or enable LiveLink.", MessageType.Info);
                return;
            }

            // 绘制 TreeView
            var rect = new Rect(0, 0, position.width, position.height);
            _treeView.OnGUI(rect);
        }

        void OnSelectionChanged()
        {
            if (_ignoreSelectionChange)
            {
                Debug.Log("[EntityHierarchy] OnSelectionChanged: ignored (flag was set)");
                _ignoreSelectionChange = false;
                return;
            }

            // Unity 在 selectionChanged 回调时尚未把 EntitySelectionProxy 写入 Selection，
            // 此刻 activeObject 仍是 null。推迟一个编辑器 tick 再读取。
            if (_pendingSelectionSync) return;
            _pendingSelectionSync = true;
            EditorApplication.delayCall += SyncSelectionFromInspector;
        }

        void SyncSelectionFromInspector()
        {
            _pendingSelectionSync = false;
            if (_treeView == null) return;

            if (EntitySelectionBridge.TryGetSelectedEntity(out var world, out var entity, out _))
            {
                _treeView.HighlightEntity(world, entity);
                Repaint();
            }
        }

        void OnInspectorUpdate()
        {
            // 实时刷新
            Repaint();
        }
    }
}
#endif
