using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HexMap
{
    /// <summary>
    /// cell 编辑共享工具（自 HexMapEditingSystem 提取，行为零变化）：
    /// 手编笔刷、特征生成提交、存档读取、POI 放置五处共用同一写路径，
    /// 保证高程公式与脏传播永远一致。
    /// </summary>
    public static class HexMapCellEditUtil
    {
        /// <summary>
        /// 设置 cell 高程（目标值制）：clamp 0..MaxElevation，重采样噪声算 y
        /// （SampleNoise 只用 x/z，用当前位置重采样结果稳定），写回 + 标脏（cell+6 邻居）。
        /// </summary>
        public static bool ApplyElevation(EntityManager em, Entity cellEntity,
            int newElevation, ref HexMapConfigBlob blob)
        {
            var cell = em.GetComponentData<HexCellData>(cellEntity);

            newElevation = math.clamp(newElevation, 0, blob.MaxElevation);
            if (newElevation == cell.Elevation)
                return false;

            var noise = HexMetrics.SampleNoise(ref blob, cell.Position);
            cell.Elevation = newElevation;
            cell.Position = new float3(
                cell.Position.x,
                HexMetrics.ElevationToY(ref blob, newElevation, noise.y),
                cell.Position.z);

            em.SetComponentData(cellEntity, cell);
            MarkCellAndNeighborsDirty(em, cellEntity);
            return true;
        }

        /// <summary>cell 与 6 路邻居全部标脏：边缘条带/扇面嵌入了双方的索引与高度</summary>
        public static void MarkCellAndNeighborsDirty(EntityManager em, Entity cellEntity)
        {
            if (em.HasComponent<CellDirty>(cellEntity))
                em.SetComponentEnabled<CellDirty>(cellEntity, true);

            if (!em.HasBuffer<Neighbors>(cellEntity))
                return;

            var neighbors = em.GetBuffer<Neighbors>(cellEntity);
            for (int d = 0; d < neighbors.Length; d++)
            {
                var n = neighbors[d].Value;
                if (n != Entity.Null && em.HasComponent<CellDirty>(n))
                    em.SetComponentEnabled<CellDirty>(n, true);
            }
        }

        /// <summary>
        /// 射线步进拾取（提取自编辑系统）：沿视线步进，命中条件是「位于某 cell 列内
        /// 且已降到该 cell 顶面以下」。步长 0.5（cell 宽 ~17，误差可忽略）。
        /// 编辑笔刷与 POI 放置共用。
        /// </summary>
        public static bool PickCell(Ray ray, EntityManager em, ref HexMapConfigBlob blob,
            in HexMetrics metrics, NativeHashMap<int2, Entity> lookup,
            out Entity cellEntity, out int2 offset)
        {
            cellEntity = Entity.Null;
            offset = default;
            const float step = 0.5f;
            // 视线朝下：走到穿过 y=0 平面再走 50 单位即可；否则走满诊断距离
            float descent = ray.direction.y < -0.05f
                ? ((float3)ray.origin).y / -ray.direction.y + 50f
                : 3000f;
            float maxDist = math.clamp(descent, 50f, 3000f);

            for (float t = step; t < maxDist; t += step)
            {
                float3 p = (float3)ray.origin + (float3)ray.direction * t;

                var cellOffset = HexCoordinates.FromPosition(p, in metrics).ToOffsetCoordinates();
                bool inBounds = cellOffset.x >= 0 && cellOffset.x < blob.CellCount.x &&
                                cellOffset.y >= 0 && cellOffset.y < blob.CellCount.y;

                if (inBounds && lookup.TryGetValue(cellOffset, out var e) && em.HasComponent<HexCellData>(e))
                {
                    var cell = em.GetComponentData<HexCellData>(e);
                    // Elevation >= 0：地形已生成（-1 是 TerrainPending 哨兵）
                    if (cell.Elevation >= 0 && p.y <= cell.Position.y + 0.75f)
                    {
                        cellEntity = e;
                        offset = cellOffset;
                        return true;
                    }
                }

                if (p.y < -10f && ray.direction.y < 0f)
                    return false;
            }
            return false;
        }
    }
}
