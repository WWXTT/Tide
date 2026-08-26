using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HexMap
{
    /// <summary>
    /// 单个 cell 的网格生成 Job（移植自旧版 HexMesh.Triangulate）。
    ///
    /// 输出 4 个 NativeList：
    /// - positions (float3)：顶点位置（已扰动）
    /// - colors (float4)：顶点颜色（splat 权重，RGB = 3 地形混合权重）
    /// - cellIndices (float3)：UV1，splat 地形索引三元组（同一三角形 3 顶点相同）
    /// - triangles (int)：索引
    ///
    /// 与旧版顶点流布局完全一致。
    /// </summary>
    [BurstCompile]
    public partial struct HexMeshJob
    {
        [ReadOnly] public BlobAssetReference<HexMapConfigBlob> Blob;
        [ReadOnly] public Entity CellEntity; // 当前 cell entity
        [ReadOnly] public HexCellData CellData; // 当前 cell
        [ReadOnly] public ComponentLookup<HexCellData> AllCellData; // 读取邻居
        [ReadOnly] public BufferLookup<Neighbors> AllNeighbors;

        public NativeList<float3> Positions;
        public NativeList<int> Triangles;
        public NativeList<float4> Colors;
        public NativeList<float3> CellIndices;

        // splat 权重基向量
        private static readonly float4 W100 = new float4(1f, 0f, 0f, 1f);
        private static readonly float4 W010 = new float4(0f, 1f, 0f, 1f);
        private static readonly float4 W001 = new float4(0f, 0f, 1f, 1f);

        public void Execute()
        {
            ref var blob = ref Blob.Value;
            var metrics = HexMetrics.FromBlob(ref blob);

            TriangulateCell(CellData, ref metrics, ref blob);
        }

        /// <summary>
        /// 为单个 cell 的 6 个方向生成三角形（与旧版 Triangulate(HexCell cell) 一致）
        /// </summary>
        private void TriangulateCell(HexCellData cell, ref HexMetrics metrics, ref HexMapConfigBlob blob)
        {
            for (int d = 0; d < 6; d++)
            {
                TriangulateDirection((HexDirection)d, cell, ref metrics, ref blob);
            }
            // 底面不再逐 cell 生成：整张地图共用一个矩形底面（HexMapBaseSystem），
            // 由外圈 cell 的竖直侧面与之闭合。
        }

        /// <summary>
        /// 封闭体底面高度。外圈 cell 恒为 elevation 0 且不做高度扰动，
        /// 所以只需低一个台阶即可保证底面始终在所有顶面之下。
        /// </summary>
        public static float GetBottomY(ref HexMetrics metrics)
        {
            return -metrics.ElevationStep;
        }

        /// <summary>
        /// 为 cell 的一个方向生成三角形：中心扇形 + 连接区域（与旧版 Triangulate(HexDirection, HexCell) 一致）
        /// </summary>
        private void TriangulateDirection(HexDirection direction, HexCellData cell, ref HexMetrics metrics, ref HexMapConfigBlob blob)
        {
            float3 center = cell.Position;

            var neighbor = GetNeighbor(cell, direction);
            bool hasNeighbor = neighbor.Elevation != int.MinValue;

            // 边界 cell 判定（此处只需判断本 cell，不关心邻居是否也是边界）
            bool isBoundary = HexBoundary.IsBoundary(in cell, blob.CellCount);

            var e = new EdgeVertices(
                center + (hasNeighbor ? metrics.GetFirstSolidCorner(direction) : metrics.GetFirstCorner(direction)),
                center + (hasNeighbor ? metrics.GetSecondSolidCorner(direction) : metrics.GetSecondCorner(direction)));

            // 只裁剪朝外（无邻居）的边：朝内的边要与邻居用 e1+bridge 推出的
            // 同一条边逐点重合，裁剪它就会让连接区错位。
            if (isBoundary && !hasNeighbor)
            {
                float4 rect = HexBoundary.GetMapRect(metrics.OuterRadius, metrics.InnerRadius, blob.CellCount);
                e.v1 = HexBoundary.ClampToRect(e.v1, rect);
                e.v2 = HexBoundary.ClampToRect(e.v2, rect);
                e.v3 = HexBoundary.ClampToRect(e.v3, rect);
                e.v4 = HexBoundary.ClampToRect(e.v4, rect);
                e.v5 = HexBoundary.ClampToRect(e.v5, rect);
            }

            // 只有朝外（无邻居）的边不扰动，让它与侧面顶边严格重合。
            // 朝内的边照常扰动，才能和内侧邻居的连接区顶点对齐。
            TriangulateEdgeFan(center, e, cell.TerrainIndex, perturbEdge: hasNeighbor);

            if (hasNeighbor)
            {
                // 连接区域（仅 NE/E/SE 三个方向，避免重复）
                if ((int)direction <= (int)HexDirection.SE)
                {
                    TriangulateConnection(direction, cell, e, ref metrics, ref blob);
                }
            }
            else
            {
                // 地图边界：生成竖直侧面连接到统一底面（bottomY = -ElevationStep）
                TriangulateBoundarySide(e, cell, ref metrics);
            }
        }

        /// <summary>
        /// 中心扇形三角化（4 个三角形，单一地形）。
        /// perturbEdge = false 时边缘顶点不扰动（地图边界边），使顶面边界与
        /// 同样未扰动的侧面顶边、底面轮廓严格重合；中心点始终扰动，保持形状自然。
        /// </summary>
        private void TriangulateEdgeFan(float3 center, EdgeVertices edge, int terrainIdx, bool perturbEdge = true)
        {
            float3 indices = new float3(terrainIdx, terrainIdx, terrainIdx);

            if (perturbEdge)
            {
                AddTriangle(center, edge.v1, edge.v2);
                AddTriangleCellData(indices, W100);

                AddTriangle(center, edge.v2, edge.v3);
                AddTriangleCellData(indices, W100);

                AddTriangle(center, edge.v3, edge.v4);
                AddTriangleCellData(indices, W100);

                AddTriangle(center, edge.v4, edge.v5);
                AddTriangleCellData(indices, W100);
            }
            else
            {
                AddTriangleCenterPerturbed(center, edge.v1, edge.v2);
                AddTriangleCellData(indices, W100);

                AddTriangleCenterPerturbed(center, edge.v2, edge.v3);
                AddTriangleCellData(indices, W100);

                AddTriangleCenterPerturbed(center, edge.v3, edge.v4);
                AddTriangleCellData(indices, W100);

                AddTriangleCenterPerturbed(center, edge.v4, edge.v5);
                AddTriangleCellData(indices, W100);
            }
        }

        /// <summary>
        /// 只扰动第一个顶点（扇形中心），另外两个（边界边缘）保持原位
        /// </summary>
        private void AddTriangleCenterPerturbed(float3 center, float3 v2, float3 v3)
        {
            int vertexIndex = Positions.Length;
            Positions.Add(Perturb(center));
            Positions.Add(v2);
            Positions.Add(v3);
            Triangles.Add(vertexIndex);
            Triangles.Add(vertexIndex + 1);
            Triangles.Add(vertexIndex + 2);
        }

        /// <summary>
        /// 添加三角形顶点（带扰动）
        /// </summary>
        private void AddTriangle(float3 v1, float3 v2, float3 v3)
        {
            int vertexIndex = Positions.Length;
            Positions.Add(Perturb(v1));
            Positions.Add(Perturb(v2));
            Positions.Add(Perturb(v3));
            Triangles.Add(vertexIndex);
            Triangles.Add(vertexIndex + 1);
            Triangles.Add(vertexIndex + 2);
        }

        /// <summary>
        /// 添加三角形顶点（不扰动，用于阶梯边界）
        /// </summary>
        private void AddTriangleUnperturbed(float3 v1, float3 v2, float3 v3)
        {
            int vertexIndex = Positions.Length;
            Positions.Add(v1);
            Positions.Add(v2);
            Positions.Add(v3);
            Triangles.Add(vertexIndex);
            Triangles.Add(vertexIndex + 1);
            Triangles.Add(vertexIndex + 2);
        }

        /// <summary>
        /// 按需扰动：边界 cell 的边缘顶点已经是最终坐标（裁剪到地图矩形），
        /// 任何再次扰动都会让它与侧面/邻居的同一顶点错开，也就是肉眼可见的裂缝。
        /// </summary>
        private float3 MaybePerturb(float3 v, bool perturb) => perturb ? Perturb(v) : v;

        /// <summary>
        /// 添加四边形，两条边各自决定是否扰动。
        /// (v1,v2) 属于 e1 边，(v3,v4) 属于 e2 边。
        /// </summary>
        private void AddQuad(float3 v1, float3 v2, float3 v3, float3 v4, bool perturbE1, bool perturbE2)
        {
            int vertexIndex = Positions.Length;
            Positions.Add(MaybePerturb(v1, perturbE1));
            Positions.Add(MaybePerturb(v2, perturbE1));
            Positions.Add(MaybePerturb(v3, perturbE2));
            Positions.Add(MaybePerturb(v4, perturbE2));
            Triangles.Add(vertexIndex);
            Triangles.Add(vertexIndex + 2);
            Triangles.Add(vertexIndex + 1);
            Triangles.Add(vertexIndex + 1);
            Triangles.Add(vertexIndex + 2);
            Triangles.Add(vertexIndex + 3);
        }

        /// <summary>
        /// 添加三角形，逐顶点决定是否扰动
        /// </summary>
        private void AddTriangle(float3 v1, float3 v2, float3 v3, bool p1, bool p2, bool p3)
        {
            int vertexIndex = Positions.Length;
            Positions.Add(MaybePerturb(v1, p1));
            Positions.Add(MaybePerturb(v2, p2));
            Positions.Add(MaybePerturb(v3, p3));
            Triangles.Add(vertexIndex);
            Triangles.Add(vertexIndex + 1);
            Triangles.Add(vertexIndex + 2);
        }

        /// <summary>
        /// 添加四边形（2 个三角形，不扰动）。用于边界侧面：顶边已被裁剪到地图矩形、
        /// 底边直接复制顶边的 x/z，再扰动就会让两者错开并重新开缝。
        /// </summary>
        private void AddQuadUnperturbed(float3 v1, float3 v2, float3 v3, float3 v4)
        {
            int vertexIndex = Positions.Length;
            Positions.Add(v1);
            Positions.Add(v2);
            Positions.Add(v3);
            Positions.Add(v4);
            Triangles.Add(vertexIndex);
            Triangles.Add(vertexIndex + 2);
            Triangles.Add(vertexIndex + 1);
            Triangles.Add(vertexIndex + 1);
            Triangles.Add(vertexIndex + 2);
            Triangles.Add(vertexIndex + 3);
        }

        /// <summary>
        /// 添加四边形（2 个三角形）
        /// </summary>
        private void AddQuad(float3 v1, float3 v2, float3 v3, float3 v4)
        {
            int vertexIndex = Positions.Length;
            Positions.Add(Perturb(v1));
            Positions.Add(Perturb(v2));
            Positions.Add(Perturb(v3));
            Positions.Add(Perturb(v4));
            Triangles.Add(vertexIndex);
            Triangles.Add(vertexIndex + 2);
            Triangles.Add(vertexIndex + 1);
            Triangles.Add(vertexIndex + 1);
            Triangles.Add(vertexIndex + 2);
            Triangles.Add(vertexIndex + 3);
        }

        /// <summary>
        /// splat：三角形 3 个顶点共用索引三元组 + 单一权重
        /// </summary>
        private void AddTriangleCellData(float3 indices, float4 weight)
        {
            CellIndices.Add(indices);
            CellIndices.Add(indices);
            CellIndices.Add(indices);
            Colors.Add(weight);
            Colors.Add(weight);
            Colors.Add(weight);
        }

        /// <summary>
        /// splat：三角形 3 个顶点共用索引三元组 + 各自权重
        /// </summary>
        private void AddTriangleCellData(float3 indices, float4 w1, float4 w2, float4 w3)
        {
            CellIndices.Add(indices);
            CellIndices.Add(indices);
            CellIndices.Add(indices);
            Colors.Add(w1);
            Colors.Add(w2);
            Colors.Add(w3);
        }

        /// <summary>
        /// splat：四边形 4 个顶点共用索引三元组 + 各自权重
        /// </summary>
        private void AddQuadCellData(float3 indices, float4 w1, float4 w2, float4 w3, float4 w4)
        {
            CellIndices.Add(indices);
            CellIndices.Add(indices);
            CellIndices.Add(indices);
            CellIndices.Add(indices);
            Colors.Add(w1);
            Colors.Add(w2);
            Colors.Add(w3);
            Colors.Add(w4);
        }

        /// <summary>
        /// splat：四边形混合区域（2 个地形，2 个权重）
        /// </summary>
        private void AddQuadCellData(float3 indices, float4 w1, float4 w2)
        {
            AddQuadCellData(indices, w1, w1, w2, w2);
        }

        /// <summary>
        /// splat：A→B 混合权重（1-b, b, 0）
        /// </summary>
        private static float4 WeightAB(float b)
        {
            return new float4(1f - b, b, 0f, 1f);
        }

        /// <summary>
        /// 顶点形状扰动（仅 x/z，y 保持平坦）。
        /// 高度（Y）扰动已由 HexTerrainGenerationSystem 按 cell 整体处理，保持顶面平坦。
        ///
        /// 必须是「世界坐标的纯函数」：相邻 cell 在连接区共享同一顶点位置，
        /// 只有两边算出完全相同的结果，网格才不会裂开。因此这里不能按 cell 中心做相对缩放，
        /// 而是用 CellPerturbRange 换算出一个以六边形半径为 1 的有界位移量。
        /// </summary>
        private float3 Perturb(float3 position)
        {
            ref var blob = ref Blob.Value;
            float amplitude = HexMetrics.CellPerturbAmplitude(ref blob);
            if (amplitude <= 0f)
                return position;

            float4 sample = HexMetrics.SampleNoise(ref blob, position);
            // 噪声 [0,1] 映射到 [-1,1]，再乘以以半径为单位的最大位移
            position.x += (sample.x * 2f - 1f) * amplitude;
            position.z += (sample.z * 2f - 1f) * amplitude;
            return position;
        }
    }
}
