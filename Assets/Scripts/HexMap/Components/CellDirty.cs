using Unity.Entities;

namespace HexMap
{
    /// <summary>
    /// Cell 网格待重建标记（enableable：置 enabled = 需要重建三角网格）
    /// </summary>
    public struct CellDirty : IComponentData, IEnableableComponent
    {
    }
}
