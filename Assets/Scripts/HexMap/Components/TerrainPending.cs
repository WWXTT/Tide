using Unity.Entities;

namespace HexMap
{
    /// <summary>
    /// 地形生成待处理标记：cell 创建时携带（Elevation 为哨兵值 -1），
    /// HexTerrainGenerationSystem 采样噪声填充 Elevation/TerrainIndex 后移除。
    /// 用于精确查询待生成的 cell，避免每帧全表扫描，
    /// 也避免把"合法的平地（elev=0, terrain=0）"误判为未生成而反复重建。
    /// </summary>
    public struct TerrainPending : IComponentData
    {
    }
}
