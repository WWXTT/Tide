using Unity.Collections;
using Unity.Entities;

namespace HexMap
{
    /// <summary>
    /// 编辑器分组标签：记录 entity 属于哪个逻辑分组，供 Entity Hierarchy 面板做树状组织。
    ///
    /// 放在运行时 assembly（而非 Editor）是必要的：HexMap 的 cell 由
    /// HexChunkStreamingSystem 在运行时 CreateEntity 出来，不走 Baker，
    /// archetype 必须能引用这个类型。ENABLE_HEX_DEBUG_LABEL 未定义时，
    /// 写入方不再添加该组件，面板退化为按 archetype 分组。
    /// </summary>
    public struct EntityDebugLabel : IComponentData
    {
        /// <summary>分组名（面板上的父节点），如 "HexCell" / "HexMapConfig"</summary>
        public FixedString64Bytes Group;
    }
}
