using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HexMap
{
    /// <summary>
    /// 单个 cell 的网格生成 Job（垂直侧壁 + 内缩阶梯版，2026-09-20 定案）。
    ///
    /// 架构原则：
    /// - 每格 = 名义六边形板，沿「阶梯边」（邻居更低且 Δe≤2）内缩 L、其余边全宽；
    ///   板角点走辐射线规则：格心→名义角点射线上取两邻边内缩较深者
    ///   （板角_k = center + Corners[k]·(min(l_{k-1}, l_k)/IR)），
    ///   相邻扇形共享同一板角点、几何互不越界，板边在内缩不等处为斜弦；
    /// - 边带分级：Δe≤2 高格在内缩带里造台阶（Δe+1 段踏面 + Δe 段立面，
    ///   带宽 L 等分）；Δe>2 垂直壁（板不内缩）；等高噪声竖缝由高侧发薄缝壁；
    ///   图外/未加载垂直壁落 bottomY——壁构造沿用「角柱分段 + 烟囱交汇」定案；
    /// - 非阶梯边发「平带」：板弦→名义边的 y_A 平面延伸，通常零宽退化，
    ///   仅在相邻阶梯边切角的角落非退化（斜弦三角形）；
    /// - 角落封闭：本格两相邻边带的端剖面（沿辐射线的折线）之间的竖直
    ///   「楼梯侧面板」，全部限制在本格扇形分界面（格心→角点辐射平面）内，
    ///   零跨格三角形；名义角点竖棱由对面那条边自己的几何覆盖；
    /// - 顶点允许分裂：每个表面持有自己的边界顶点，靠「位置的纯函数」
    ///   （同批标称角点 + 同 EdgeVertices 插值 + 同 Perturb）保证相邻表面逐点重合；
    /// - 法线单一来源：板/踏面 +Y、壁/立面逐边水平外法线、面板逐矩形水平常量，
    ///   直写 NORMAL 通道（rim 融合与 TANGENT 通道已随坡带设计退役）。
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
        /// <summary>阶梯边最大高差级数（Δe∈{1,2} 造阶梯，>2 垂直壁）</summary>
        private const int StairMaxDelta = 2;

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
        /// 一条边的分类结果 + 带生成所需数据（ClassifyAll 一次算好，板/带/面板共用）。
        /// Boundary/Streaming 的 NeighborY = bottomY、对侧地形 = 本格（恒本格 splat）。
        /// </summary>
        private struct EdgeInfo
        {
            public EdgeKind Kind;
            /// <summary>高差级数（Lower 时 = elevation 差，≥1；其余 0）</summary>
            public int DeltaE;
            /// <summary>对侧板高（真实邻居 = 其板 y；图外/未加载 = bottomY）</summary>
            public float NeighborY;
            /// <summary>对侧地形索引（无效邻居 = 本格）</summary>
            public int NeighborTerrain;
            /// <summary>真实邻居（splat 渐变到对侧地形；否则恒本格）</summary>
            public bool RealNeighbor;
        }

        private void TriangulateCell(HexCellData cell, ref HexMetrics metrics, ref HexMapConfigBlob blob)
        {
            var edges = new FixedList128Bytes<EdgeInfo>();
            ClassifyAll(cell, ref metrics, ref blob, ref edges);

            BuildPlate(cell, ref metrics, ref blob, ref edges);

            for (int d = 0; d < 6; d++)
                BuildDirection((HexDirection)d, cell, ref metrics, ref blob, ref edges);

            for (int k = 0; k < 6; k++)
                BuildCornerPanel(k, cell, ref metrics, ref blob, ref edges);
        }

        /// <summary>六边一次性分类（板内缩、边带、面板都要查边类型/对侧高度）</summary>
        private void ClassifyAll(HexCellData cell, ref HexMetrics metrics, ref HexMapConfigBlob blob,
            ref FixedList128Bytes<EdgeInfo> edges)
        {
            float bottomY = GetBottomY(ref metrics);
            for (int d = 0; d < 6; d++)
            {
                var kind = ClassifyEdge(cell, CellEntity, (HexDirection)d, ref blob, out var neighbor, out _);
                bool real = kind == EdgeKind.Equal || kind == EdgeKind.Lower || kind == EdgeKind.Higher;
                edges.Add(new EdgeInfo
                {
                    Kind = kind,
                    RealNeighbor = real,
                    NeighborY = real ? neighbor.Position.y : bottomY,
                    NeighborTerrain = real ? neighbor.TerrainIndex : cell.TerrainIndex,
                    DeltaE = kind == EdgeKind.Lower
                        ? math.max(1, cell.Elevation - neighbor.Elevation)
                        : 0,
                });
            }
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

        // ---------- 板（双环扇形 + 阶梯边内缩）----------

        /// <summary>阶梯带宽度 L = blob.SlopeInset，钳到 [5%, 90%]·IR（防 authoring 失配）</summary>
        private static float StairBandWidth(ref HexMapConfigBlob blob, float ir)
        {
            return math.clamp(blob.SlopeInset, ir * 0.05f, ir * 0.9f);
        }

        /// <summary>边有效内缩：阶梯边（Lower 且 Δe≤2）= IR−L，其余全宽 IR</summary>
        private static float EffectiveInset(in EdgeInfo e, float ir, float bandL)
        {
            return e.Kind == EdgeKind.Lower && e.DeltaE <= StairMaxDelta
                ? math.max(ir - bandL, ir * 0.1f)
                : ir;
        }

        /// <summary>
        /// 板角点 k（辐射线规则）：格心→名义角点 k 的射线上，取相邻两边
        /// (k−1)/k 有效内缩的较深者。两邻边等内缩时与平行内缩角点重合；
        /// 不等时角点收在辐射线上不戳到浅边——相邻扇形共享同一板角点、互不越界。
        /// 全宽角（两边都不内缩）= 名义角点，吸附格点保证跨格逐位重合。
        /// 纯函数：板、边带、面板各自调用得到同一结果（单一来源）。
        /// </summary>
        private float3 PlateRimCorner(int k, HexCellData cell, ref HexMetrics metrics,
            ref HexMapConfigBlob blob, ref FixedList128Bytes<EdgeInfo> edges)
        {
            float ir = metrics.InnerRadius;
            float bandL = StairBandWidth(ref blob, ir);
            float l = math.min(
                EffectiveInset(edges[(k + 5) % 6], ir, bandL),
                EffectiveInset(edges[k], ir, bandL));
            float3 c = cell.Position + metrics.Corners[k] * (l / ir);
            return l >= ir ? SnapToLattice(c, ref metrics) : c;
        }

        /// <summary>板角 k 是否未被切角（两边都不内缩 = 落在名义角点上）</summary>
        private static bool IsRimFull(ref FixedList128Bytes<EdgeInfo> edges, int k, float ir, float bandL)
        {
            return math.min(
                EffectiveInset(edges[(k + 5) % 6], ir, bandL),
                EffectiveInset(edges[k], ir, bandL)) >= ir;
        }

        /// <summary>
        /// 顶面板 = 名义六边形板（阶梯边内缩）：中心 + 内环（再内缩 fade）+ 边环（板多边形）。
        /// 边环与边带顶行共享同一批角点与 EdgeVertices 插值 → 扰动后逐点重合。
        /// 变异权重：中心/内环 1，边环 0（渐变跨内环→边环的环带；带/壁/面板全 0 → 共享线两侧一致）。
        /// 法线恒 +Y。相邻等高格的板直接共边密铺——地形边界为硬边（块状观感定案）。
        /// </summary>
        private void BuildPlate(HexCellData cell, ref HexMetrics metrics, ref HexMapConfigBlob blob,
            ref FixedList128Bytes<EdgeInfo> edges)
        {
            float3 center = cell.Position;
            _surfaceNormal = new float3(0f, 1f, 0f); // 板的表面法线恒 +Y
            float ir = metrics.InnerRadius;
            float bandL = StairBandWidth(ref blob, ir);
            float fade = math.clamp(blob.VariationFadeWidth, 0.01f, ir);

            var rim = new FixedList128Bytes<float3>();   // 板角点（辐射线规则，含阶梯边内缩）
            var inner = new FixedList128Bytes<float3>(); // 内环角点（同规则再内缩 fade，≥5% IR 兜底）
            for (int k = 0; k < 6; k++)
            {
                var ePrev = edges[(k + 5) % 6];
                var eNext = edges[k];
                float lIn = math.min(
                    math.max(EffectiveInset(ePrev, ir, bandL) - fade, ir * 0.05f),
                    math.max(EffectiveInset(eNext, ir, bandL) - fade, ir * 0.05f));
                rim.Add(PlateRimCorner(k, cell, ref metrics, ref blob, ref edges));
                inner.Add(center + metrics.Corners[k] * (lIn / ir));
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

        /// <summary>
        /// 名义角点/名义边位置吸附到六边形格点（x = i·IR，z = j·OR/2）。
        /// 跨格共享的位置必须逐位一致：相邻格各自用「格心 + 角点偏移」计算同一个
        /// 名义角点会有 ~1 ulp 浮点差（坐标 ~100 时 ~1e-5），量化配对后跨 1e-4 桶
        /// 即成发丝缝。格点下标用 round(坐标/间距) 从整数恢复，两侧必然同值
        /// （格心公式保证名义角点严格落在该格点上）；吸附只动 x/z（修正量 ~1e-5，
        /// 视觉不可见），y 不变。格心公式见 HexChunkStreamingSystem.CreateCell。
        /// </summary>
        private static float3 SnapToLattice(float3 p, ref HexMetrics metrics)
        {
            float dx = metrics.InnerRadius;
            float dz = metrics.OuterRadius * 0.5f;
            return new float3(math.round(p.x / dx) * dx, p.y, math.round(p.z / dz) * dz);
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

        /// <summary>添加四边形（2 个三角形，扰动）。</summary>
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

        /// <summary>添加三角形（不扰动，与 AddQuadUnperturbed 同理；退化列收三角时用）</summary>
        private void AddTriangleUnperturbed(float3 v1, float3 v2, float3 v3,
            float3 indices, float4 w1, float4 w2, float4 w3)
        {
            int vertexIndex = Positions.Length;
            Positions.Add(v1);
            Positions.Add(v2);
            Positions.Add(v3);
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
