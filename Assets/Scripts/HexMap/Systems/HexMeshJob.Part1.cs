using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HexMap
{
    /// <summary>
    /// 单个 cell 的网格生成 Job（垂直侧壁版：板 + 陡壁）。
    ///
    /// 架构原则（2026-09-20 垂直重构定案）：
    /// - 每格 = 全宽名义六边形板（不内缩），高度定义在 Cell 上；相邻等高板直接共边密铺；
    /// - 侧面完全垂直：相邻更低（或图外/未加载）时，本格在共享边发陡壁条带，
    ///   从本格板缘垂直下落到低侧板缘（或 bottomY）——「上小下大」与坡带/桥/角落吸收
    ///   全套退役，角落多级高差由三条边壁共享角落竖棱（烟囱状交汇）构造性水密；
    /// - 顶点允许分裂：每个表面（板/壁）持有自己的边界顶点，靠「位置的纯函数」
    ///   （同批标称角点 + 同 EdgeVertices 插值 + 同 Perturb）保证相邻表面逐点重合；
    /// - 陡壁墙柱在两侧角落第三方格的板高处分段（防 T-junction 发丝缝）；
    /// - 法线单一来源：板 +Y、壁 = 逐边水平外法线，直写 NORMAL 通道
    ///   （rim 融合与 TANGENT 通道已随坡带设计退役，三平面投影权重只用纯法线）。
    ///
    /// 输出 5 个 NativeList：
    /// - positions (float3)：顶点位置（已扰动）
    /// - colors (float4)：RGB = splat 权重，A = 变异权重
    /// - cellIndices (float3)：UV1，splat 地形索引三元组（同一三角形 3 顶点相同）
    /// - pureNormals (float3)：表面常量法线（板 +Y / 壁水平），写 mesh NORMAL 通道
    /// - variations (float4)：UV2，本格变异常量
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
        public NativeList<float3> PureNormals;
        public NativeList<float4> Variations;

        /// <summary>本格变异常量 (s·cosθ, s·sinθ, ox, oy)，格内全部顶点共享（Execute 时算一次）</summary>
        private float4 _cellVariation;
        /// <summary>当前发射表面的常量法线（每个表面块开始时设置；Add* 自动写入 PureNormals）</summary>
        private float3 _surfaceNormal;

        // splat 权重基向量（alpha 为变异权重槽，调用处按需覆写）
        private static readonly float4 W100 = new float4(1f, 0f, 0f, 1f);
        private static readonly float4 W010 = new float4(0f, 1f, 0f, 1f);
        private static readonly float4 W001 = new float4(0f, 0f, 1f, 1f);

        public void Execute()
        {
            ref var blob = ref Blob.Value;
            var metrics = HexMetrics.FromBlob(ref blob);

            _cellVariation = blob.VariationEnabled != 0
                ? HexMetrics.CellVariation(CellData.Coordinates.ToOffsetCoordinates(),
                    blob.VariationSeed, blob.VariationScaleRange)
                : new float4(1f, 0f, 0f, 0f); // 恒等变换

            TriangulateCell(CellData, ref metrics, ref blob);
        }

        /// <summary>
        /// 全宽板 + 6 方向陡壁（底面为整图轮廓 Cap，不逐 cell 生成）
        /// </summary>
        private void TriangulateCell(HexCellData cell, ref HexMetrics metrics, ref HexMapConfigBlob blob)
        {
            BuildPlate(cell, ref metrics, ref blob);

            for (int d = 0; d < 6; d++)
                BuildDirection((HexDirection)d, cell, ref metrics, ref blob);
        }

        /// <summary>
        /// 封闭体底面高度。外圈 cell 恒为 elevation 0 且不做高度扰动，
        /// 所以只需低一个台阶即可保证底面始终在所有顶面之下。
        /// </summary>
        public static float GetBottomY(ref HexMetrics metrics)
        {
            return -metrics.ElevationStep;
        }

        // ---------- 邻居访问与边分类 ----------

        private static bool IsValidData(in HexCellData c)
        {
            return c.Elevation != int.MinValue &&
                   !math.any(math.isnan(c.Position)) &&
                   !math.any(math.isinf(c.Position));
        }

        private Entity NeighborEntity(Entity e, HexDirection d)
        {
            if (!AllNeighbors.HasBuffer(e))
                return Entity.Null;
            return AllNeighbors[e][(int)d].Value;
        }

        private HexCellData DataOf(Entity e)
        {
            if (e == Entity.Null || !AllCellData.HasComponent(e))
                return new HexCellData { Elevation = int.MinValue };
            return AllCellData[e];
        }

        /// <summary>
        /// 分类任意 cell x 沿方向 d 的边。返回伪邻居时 neighbor 为哨兵值：
        /// - Boundary：邻居在地图外（永久缺失）→ 陡壁直落 bottomY；
        /// - Streaming：邻居在图内但尚未加载 → 同 Boundary（临时壁，邻居加载后标脏重建）。
        /// </summary>
        private EdgeKind ClassifyEdge(HexCellData x, Entity xEntity, HexDirection d, ref HexMapConfigBlob blob,
            out HexCellData neighbor, out Entity neighborEntity)
        {
            neighborEntity = NeighborEntity(xEntity, d);
            neighbor = DataOf(neighborEntity);
            if (!IsValidData(neighbor))
            {
                var xOff = x.Coordinates.ToOffsetCoordinates();
                var nOff = HexBoundary.NeighborOffset(xOff, d);
                return HexBoundary.IsOutsideMap(nOff, blob.CellCount)
                    ? EdgeKind.Boundary
                    : EdgeKind.Streaming;
            }
            if (neighbor.Elevation == x.Elevation)
                return EdgeKind.Equal;
            return neighbor.Elevation > x.Elevation ? EdgeKind.Higher : EdgeKind.Lower;
        }

        // ---------- 板（全宽双环扇形）----------

        /// <summary>
        /// 顶面板 = 全宽名义六边形（不内缩）：中心 + 内环（再内缩 VariationFadeWidth）+ 边环（名义角）。
        /// 边环与陡壁顶行共享同一批标称角点与 EdgeVertices 插值 → 扰动后逐点重合。
        /// 变异权重：中心/内环 1，边环 0（渐变跨内环→边环的环带，即 fade 宽度；壁全 0 → 共享线两侧一致）。
        /// 法线恒 +Y。相邻等高格的板直接共边密铺——地形边界为硬边（块状观感定案）。
        /// </summary>
        private void BuildPlate(HexCellData cell, ref HexMetrics metrics, ref HexMapConfigBlob blob)
        {
            float3 center = cell.Position;
            float3 up = new float3(0f, 1f, 0f);
            _surfaceNormal = up; // 板的表面法线恒 +Y
            float ir = metrics.InnerRadius;
            float fade = math.clamp(blob.VariationFadeWidth, 0.01f, ir);

            var rim = new FixedList128Bytes<float3>();   // 板角点 = 名义角（全宽）
            var inner = new FixedList128Bytes<float3>(); // 内环角点（再内缩 fade，≥5% IR 兜底）
            for (int k = 0; k < 6; k++)
            {
                var dPrev = (HexDirection)((k + 5) % 6);
                rim.Add(center + metrics.Corners[k]);
                inner.Add(center + metrics.GetPlateCorner(dPrev,
                    math.max(ir - fade, ir * 0.05f),
                    math.max(ir - fade, ir * 0.05f)));
            }

            float3 indices = new float3(cell.TerrainIndex, cell.TerrainIndex, cell.TerrainIndex);
            float4 wIn = new float4(W100.xyz, 1f);   // 内环/中心：变异权重 1
            float4 wRim = new float4(W100.xyz, 0f);  // 边环：变异权重 0

            for (int d = 0; d < 6; d++)
            {
                var ie = new EdgeVertices(inner[d], inner[(d + 1) % 6]);
                var re = new EdgeVertices(rim[d], rim[(d + 1) % 6]);

                // 中心扇形 → 内环（4 三角，法线 +Y / 权重 1）
                AddTriangle(center, ie.v1, ie.v2, indices, wIn, wIn, wIn);
                AddTriangle(center, ie.v2, ie.v3, indices, wIn, wIn, wIn);
                AddTriangle(center, ie.v3, ie.v4, indices, wIn, wIn, wIn);
                AddTriangle(center, ie.v4, ie.v5, indices, wIn, wIn, wIn);

                // 内环 → 边环（4 四边形，边环变异权重 0）
                AddQuad(ie.v1, ie.v2, re.v1, re.v2, indices, wIn, wIn, wRim, wRim);
                AddQuad(ie.v2, ie.v3, re.v2, re.v3, indices, wIn, wIn, wRim, wRim);
                AddQuad(ie.v3, ie.v4, re.v3, re.v4, indices, wIn, wIn, wRim, wRim);
                AddQuad(ie.v4, ie.v5, re.v4, re.v5, indices, wIn, wIn, wRim, wRim);
            }
        }

        // ---------- 顶点追加（位置/法线/splat 索引/权重/变异常量五通道同 append）----------

        /// <summary>
        /// 添加三角形顶点（带扰动）。法线取当前 _surfaceNormal（表面常量）。
        /// </summary>
        private void AddTriangle(float3 v1, float3 v2, float3 v3,
            float3 indices, float4 w1, float4 w2, float4 w3)
        {
            int vertexIndex = Positions.Length;
            Positions.Add(Perturb(v1));
            Positions.Add(Perturb(v2));
            Positions.Add(Perturb(v3));
            Triangles.Add(vertexIndex);
            Triangles.Add(vertexIndex + 1);
            Triangles.Add(vertexIndex + 2);
            CellIndices.Add(indices);
            CellIndices.Add(indices);
            CellIndices.Add(indices);
            Colors.Add(w1);
            Colors.Add(w2);
            Colors.Add(w3);
            PureNormals.Add(_surfaceNormal);
            PureNormals.Add(_surfaceNormal);
            PureNormals.Add(_surfaceNormal);
            Variations.Add(_cellVariation);
            Variations.Add(_cellVariation);
            Variations.Add(_cellVariation);
        }

        /// <summary>
        /// 添加四边形（2 个三角形，扰动）。
        /// </summary>
        private void AddQuad(float3 v1, float3 v2, float3 v3, float3 v4,
            float3 indices, float4 w1, float4 w2, float4 w3, float4 w4)
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
            CellIndices.Add(indices);
            CellIndices.Add(indices);
            CellIndices.Add(indices);
            CellIndices.Add(indices);
            Colors.Add(w1);
            Colors.Add(w2);
            Colors.Add(w3);
            Colors.Add(w4);
            PureNormals.Add(_surfaceNormal);
            PureNormals.Add(_surfaceNormal);
            PureNormals.Add(_surfaceNormal);
            PureNormals.Add(_surfaceNormal);
            Variations.Add(_cellVariation);
            Variations.Add(_cellVariation);
            Variations.Add(_cellVariation);
            Variations.Add(_cellVariation);
        }

        /// <summary>
        /// 添加四边形（2 个三角形，不扰动）。顶点已在上游手动扰动/构造过
        /// （与其它表面共用同一批点），这里再扰动会让同一点出现两个位置。
        /// </summary>
        private void AddQuadUnperturbed(float3 v1, float3 v2, float3 v3, float3 v4,
            float3 indices, float4 w1, float4 w2, float4 w3, float4 w4)
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
            CellIndices.Add(indices);
            CellIndices.Add(indices);
            CellIndices.Add(indices);
            CellIndices.Add(indices);
            Colors.Add(w1);
            Colors.Add(w2);
            Colors.Add(w3);
            Colors.Add(w4);
            PureNormals.Add(_surfaceNormal);
            PureNormals.Add(_surfaceNormal);
            PureNormals.Add(_surfaceNormal);
            PureNormals.Add(_surfaceNormal);
            Variations.Add(_cellVariation);
            Variations.Add(_cellVariation);
            Variations.Add(_cellVariation);
            Variations.Add(_cellVariation);
        }

        /// <summary>把一条边的 5 个顶点整体扰动一次（表面间共用，避免二次扰动）</summary>
        private EdgeVertices PerturbEdge(EdgeVertices e)
        {
            var t = new EdgeVertices(Perturb(e.v1), Perturb(e.v5));
            t.v2 = Perturb(e.v2);
            t.v3 = Perturb(e.v3);
            t.v4 = Perturb(e.v4);
            return t;
        }

        /// <summary>
        /// 顶点形状扰动（仅 x/z，y 保持平坦）。
        /// 必须是「世界坐标的纯函数」：相邻表面在共享顶点处算出同一结果，网格才不会裂开。
        /// 振幅随位置到地图边缘的距离衰减到 0（边缘完全不扰动）。
        /// </summary>
        private float3 Perturb(float3 position)
        {
            ref var blob = ref Blob.Value;
            float amplitude = HexMetrics.CellPerturbAmplitude(ref blob, position);
            if (amplitude <= 0f)
                return position;

            float4 sample = HexMetrics.SampleNoise(ref blob, position, HexNoiseKind.Detail);
            // 噪声 [0,1] 映射到 [-1,1]，再乘以以半径为单位的最大位移
            // （Worley 图建议 SplitFirst3Octaves：R/G 独立去相关；灰度时 .x==.z → 斜向偏置）
            position.x += (sample.x * 2f - 1f) * amplitude;
            position.z += (sample.z * 2f - 1f) * amplitude;
            return position;
        }
    }
}
