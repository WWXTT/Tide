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

        /// <summary>
        /// 逐顶点 UV 坡面补偿向量（世界单位）：uv = 世界xz + 补偿。
        /// 顶视投影在陡壁上沿落差拉伸，连接带/角部把「低于坡顶的高度 × k·下坡方向」
        /// 烘进该通道；平地恒 0。片元只插值不现算——相邻三角形共享顶点取同一值，
        /// 不会像按片元法线计算那样逐面错位。
        /// </summary>
        public NativeList<float2> UvCorr;

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
            // 由外圈 cell 的边界连接区（下探到底面边缘）与之闭合。
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

            var e = new EdgeVertices(
                center + (hasNeighbor ? metrics.GetFirstSolidCorner(direction) : metrics.GetFirstCorner(direction)),
                center + (hasNeighbor ? metrics.GetSecondSolidCorner(direction) : metrics.GetSecondCorner(direction)));

            // 全扰动：Perturb 是世界坐标的纯函数，扇形/连接区/边界裙边在共享顶点处
            // 算出同一结果。扰动与不扰动混用反而会在两者的交界处错开裂缝。
            // 地图边缘的振幅由 Perturb 内部按位置衰减到 0（边缘完全不扰动）。
            TriangulateEdgeFan(center, e, cell.TerrainIndex);

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
                // 无邻居分两种情况：
                // 1. 邻居在地图外（永久缺失）→ 地图边界：连接区直接下探到底面边缘
                // 2. 邻居尚未流式加载（暂时缺失）→ 临时竖直侧面，邻居加载后自动替换
                var cellOffset = cell.Coordinates.ToOffsetCoordinates();
                var neighborOffset = HexBoundary.NeighborOffset(cellOffset, direction);
                if (HexBoundary.IsOutsideMap(neighborOffset, blob.CellCount))
                {
                    TriangulateBoundaryConnection(e, cell, ref metrics, ref blob);
                }
                else
                {
                    TriangulateBoundarySide(e, cell, ref metrics);
                }

                // 本方向扇形铺到完整角、相邻方向（若有邻居）桥接只从 solid 角起，
                // 两者之间的角部区域是空洞，必须补面（地图边界与流式边缘同样需要）
                TriangulateMixedCorner(direction, cell, ref metrics);
            }
        }

        /// <summary>
        /// 中心扇形三角化（4 个三角形，单一地形），顶点全部扰动。
        /// </summary>
        private void TriangulateEdgeFan(float3 center, EdgeVertices edge, int terrainIdx)
        {
            float3 indices = new float3(terrainIdx, terrainIdx, terrainIdx);

            AddTriangle(center, edge.v1, edge.v2);
            AddTriangleCellData(indices, W100);

            AddTriangle(center, edge.v2, edge.v3);
            AddTriangleCellData(indices, W100);

            AddTriangle(center, edge.v3, edge.v4);
            AddTriangleCellData(indices, W100);

            AddTriangle(center, edge.v4, edge.v5);
            AddTriangleCellData(indices, W100);
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
            UvCorr.Add(float2.zero);
            UvCorr.Add(float2.zero);
            UvCorr.Add(float2.zero);
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
            UvCorr.Add(float2.zero);
            UvCorr.Add(float2.zero);
            UvCorr.Add(float2.zero);
            Triangles.Add(vertexIndex);
            Triangles.Add(vertexIndex + 1);
            Triangles.Add(vertexIndex + 2);
        }

        /// <summary>回填刚发射的 3 个顶点的 UV 补偿（Add* 默认填 0，坡面几何按需覆盖）</summary>
        private void PatchLast3(float2 a, float2 b, float2 c)
        {
            int i = UvCorr.Length - 3;
            UvCorr[i] = a;
            UvCorr[i + 1] = b;
            UvCorr[i + 2] = c;
        }

        /// <summary>回填刚发射的 4 个顶点的 UV 补偿</summary>
        private void PatchLast4(float2 a, float2 b, float2 c, float2 d)
        {
            int i = UvCorr.Length - 4;
            UvCorr[i] = a;
            UvCorr[i + 1] = b;
            UvCorr[i + 2] = c;
            UvCorr[i + 3] = d;
        }

        /// <summary>
        /// 添加四边形（2 个三角形，不扰动）。用于边界连接区/流式侧面：顶点已在上游
        /// 手动扰动过（与扇形共用同一批点），这里再扰动会让同一点出现两个位置。
        /// </summary>
        private void AddQuadUnperturbed(float3 v1, float3 v2, float3 v3, float3 v4)
        {
            int vertexIndex = Positions.Length;
            Positions.Add(v1);
            Positions.Add(v2);
            Positions.Add(v3);
            Positions.Add(v4);
            UvCorr.Add(float2.zero);
            UvCorr.Add(float2.zero);
            UvCorr.Add(float2.zero);
            UvCorr.Add(float2.zero);
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
            UvCorr.Add(float2.zero);
            UvCorr.Add(float2.zero);
            UvCorr.Add(float2.zero);
            UvCorr.Add(float2.zero);
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
        /// 振幅随位置到地图边缘的距离衰减到 0（边缘完全不扰动），同样是纯函数，
        /// 边界与内部交界的共享顶点两侧依然逐点一致。
        /// </summary>
        private float3 Perturb(float3 position)
        {
            ref var blob = ref Blob.Value;
            float amplitude = HexMetrics.CellPerturbAmplitude(ref blob, position);
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
