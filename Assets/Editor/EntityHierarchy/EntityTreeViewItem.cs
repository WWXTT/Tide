#if UNITY_EDITOR
using Unity.Entities;
using UnityEditor.IMGUI.Controls;

namespace EntityHierarchy
{
    /// <summary>
    /// TreeView 中代表一个 Entity 的节点
    /// </summary>
    public class EntityTreeViewItem : TreeViewItem<int>
    {
        public Entity entity;
        public World world;

        public EntityTreeViewItem(int id, int depth, string displayName, Entity entity, World world)
            : base(id, depth, displayName)
        {
            this.entity = entity;
            this.world = world;
        }
    }
}
#endif
