using Unity.Entities;
using UnityEngine;

namespace HexMap
{
    /// <summary>
    /// 地图配置单例：Blob 引用 + 地形材质 + 流式参数。
    /// 由 HexMapAuthoring 在运行时构建（或 Baker 在 SubScene 烘焙时构建）。
    /// </summary>
    public struct HexMapConfig : IComponentData
    {
        public BlobAssetReference<HexMapConfigBlob> Blob;

        /// <summary>HexTerrain splat 材质</summary>
        public UnityObjectRef<Material> TerrainMaterial;

        /// <summary>流式加载半径（cell 单位，摄像机周边 Chebyshev 距离内的 cell 会被创建）</summary>
        public float LoadRadius;

        /// <summary>
        /// 流式卸载半径（cell 单位）。必须 > LoadRadius：两者之差构成滞回区，
        /// 摄像机在加载边界附近来回移动时不会反复创建/销毁同一批 cell。
        /// </summary>
        public float UnloadRadius;

        /// <summary>每帧最多创建的 cell 数</summary>
        public int MaxCellCreationsPerFrame;

        /// <summary>每帧最多重建网格的 cell 数</summary>
        public int MaxMeshBuildsPerFrame;
    }
}
