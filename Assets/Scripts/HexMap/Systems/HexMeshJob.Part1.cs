using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HexMap
{
    /// <summary>
    /// 单个 cell 的网格生成 Job（网格重做版：板/坡/桥/角闭合）。
    ///
    /// 架构原则（2026-09-18 定案）：
    /// - 切割分界线只落在无高度变化的平面上；高度定义在 Cell 上；
    /// - 顶点允许分裂：每个表面（板/坡/桥/角）持有自己的边界顶点，靠「位置的纯函数」
    ///   保证相邻 cell / 分裂顶点两侧算出同一位置；
    /// - 边为归属单位：桥/坡由方向 ∈ {NE,E,SE} 一侧生成；边界/流式边由图内 cell 生成；
    /// - 高度差显式表达为坡：高 cell 板从共享边内缩 d，坡占高 cell 面积，
    ///   从内缩边（高程）直落低 cell 板缘（低程）；低 cell 板铺满到共享边；
    /// - 地图外 ≡ 高度 bottomY、板=名义六边形的伪 cell：内部边/边界边/角落闭合同一套规则；
    ///   流式边同伪 cell 处理（临时竖墙落 bottomY），但本格板铺满不内缩——
    ///   邻居加载后本格板缘可能移动，由流式重建消除。
    ///
    /// 角落闭合（三格交界，角 k = 边 k-1 与 k 之间的顶点）：
    /// 三方各自的板角点 P_A/P_1/P_2 连成单个三角形，三条边恰为三条连接带的端截面
    /// ——任何高度组合下构造性水密（含边界角；双边界角退化由两条边界坡端截面对合）。
    /// 归属 = Elevation 最高者，平手图内优先 + offset 字典序，三方对称可算。
    ///
    /// 输出 4 个 NativeList（法线/变异常量通道在后续步骤扩展）：
    /// - positions (float3)：顶点位置（已扰动）
    /// - colors (float4)：RGB = splat 权重
    /// - cellIndices (float3)：UV1，splat 地形索引三元组（同一三角形 3 顶点相同）
    /// - triangles (int)：索引
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
        public NativeList<float3> Normals;
        public NativeList<float4> Variations;
        /// <summary>纯表面法线（未融合）：写进 mesh 的 NORMAL 通道，供光照/SH/SSAO/阴影使用；
        /// 融合法线（Normals 列表）写进 TANGENT 通道，仅供 shader 三平面投影权重。
        /// 双法线分离：rim 融合只作用于纹理投影连续性，不再污染光照（背光面阴影偏矮一类问题）。</summary>
        public NativeList<float3> PureNormals;

        /// <summary>本格变异常量 (s·cosθ, s·sinθ, ox, oy)，格内全部顶点共享（Execute 时算一次）</summary>
        private float4 _cellVariation;
        /// <summary>rim 法线融合系数（blob.RimNormalBlend，Execute 时缓存）</summary>
        private float _rimBlend;
        /// <summary>当前发射表面的纯法线（每个表面块开始时设置；Add* 自动写入 PureNormals）</summary>
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
            _rimBlend = math.saturate(blob.RimNormalBlend);

            TriangulateCell(CellData, ref metrics, ref blob);
        }

        /// <summary>
        /// 板 + 6 方向连接几何 + 6 角落闭合（底面为整图轮廓 Cap，不逐 cell 生成）
        /// </summary>
        private void TriangulateCell(HexCellData cell, ref HexMetrics metrics, ref HexMapConfigBlob blob)
        {
            BuildPlate(cell, ref metrics, ref blob);

            for (int d = 0; d < 6; d++)
                BuildDirection((HexDirection)d, cell, ref metrics, ref blob);

            for (int k = 0; k < 6; k++)
                BuildCornerClosure(k, cell, ref metrics, ref blob);
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
        /// - Boundary：邻居在地图外（永久缺失）→ 伪 cell（高度 bottomY、名义六边形）；
        /// - Streaming：邻居在图内但尚未加载 → 同伪 cell 处理，但本格板铺满（见 GetEdgeInset）。
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
                return EdgeKind.Bridge;
            return neighbor.Elevation > x.Elevation ? EdgeKind.SlopeLower : EdgeKind.SlopeHigher;
        }

        /// <summary>x 的板在方向 d 的内缩距离（分类一次一查）</summary>
        private float PlateInset(HexCellData x, Entity xEntity, HexDirection d, ref HexMetrics metrics,
            ref HexMapConfigBlob blob)
        {
            var kind = ClassifyEdge(x, xEntity, d, ref blob, out _, out _);
            return metrics.GetEdgeInset(kind);
        }

        /// <summary>
        /// x 的板角点 k（介于边 k-1 与 k 之间，对应 Corners[k] 方位），世界坐标、未扰动、y=板高。
        /// 纯函数：仅依赖双方共享的邻居高度数据，相邻 cell 对共享角点算出同一结果。
        /// </summary>
        private float3 PlateCorner(HexCellData x, Entity xEntity, int k, ref HexMetrics metrics,
            ref HexMapConfigBlob blob)
        {
            var dPrev = (HexDirection)((k + 5) % 6);
            float l1 = PlateInset(x, xEntity, dPrev, ref metrics, ref blob);
            float l2 = PlateInset(x, xEntity, (HexDirection)k, ref metrics, ref blob);
            return x.Position + metrics.GetPlateCorner(dPrev, l1, l2);
        }

        // ---------- 板（双环扇形）----------

        /// <summary>
        /// 连接面（边 d 上的桥/坡/墙）的解析法线，纯函数、两侧对称：
        /// 桥 = +Y；坡 = SlopeNormal（e 取「指向低侧」的边法线，Δh 取实际世界高差）；
        /// 边界坡同公式（低侧 = bottomY）；流式墙 = 水平朝缺 cell。
        /// </summary>
        private float3 StripNormal(HexDirection d, HexCellData cell, ref HexMetrics metrics,
            ref HexMapConfigBlob blob)
        {
            var kind = ClassifyEdge(cell, CellEntity, d, ref blob, out var neighbor, out _);
            float3 n = metrics.GetEdgeNormal(d);
            switch (kind)
            {
                case EdgeKind.Bridge:
                    return new float3(0f, 1f, 0f);
                case EdgeKind.SlopeHigher:
                    return HexMetrics.SlopeNormal(n, cell.Position.y - neighbor.Position.y, metrics.SlopeInset);
                case EdgeKind.SlopeLower:
                    // 邻居更高：坡法线朝本格（低侧），e = -n；Δh = 邻高-本高
                    return HexMetrics.SlopeNormal(-n, neighbor.Position.y - cell.Position.y, metrics.SlopeInset);
                case EdgeKind.Boundary:
                    return HexMetrics.SlopeNormal(n, cell.Position.y - GetBottomY(ref metrics), metrics.SlopeInset);
                default: // Streaming
                    return n;
            }
        }

        /// <summary>边 d 中段 rim 的交界法线（板与该边连接面相遇）：桥时退化为 +Y</summary>
        private float3 EdgeJunction(HexDirection d, float3 snD)
        {
            return math.normalize(new float3(0f, 1f, 0f) + snD);
        }

        /// <summary>角 k（边 k-1 与 k 之间）rim 的交界法线：板 + 两条边连接面之和</summary>
        private float3 CornerJunction(int k, float3 snPrev, float3 snK)
        {
            return math.normalize(new float3(0f, 1f, 0f) + snPrev + snK);
        }

        /// <summary>rim 融合：本面法线向交界法线插值（对称公式，共享线两侧取同值）</summary>
        private float3 BlendRim(float3 nSurface, float3 nJunction)
        {
            return math.normalize(math.lerp(nSurface, nJunction, _rimBlend));
        }

        /// <summary>
        /// 顶面板：中心 + 内环（板多边形再内缩 VariationFadeWidth）+ 边环（板边线，v1..v5）。
        /// 边环与连接带共享同一批标称角点与 EdgeVertices 插值 → 扰动后逐点重合。
        /// 变异权重：中心/内环 1，边环 0（渐变跨内环→边环的环带，即 fade 宽度）。
        /// 法线：板面 +Y；边环顶点按 rim 融合向交界法线过渡（桥侧自动无感）。
        /// </summary>
        private void BuildPlate(HexCellData cell, ref HexMetrics metrics, ref HexMapConfigBlob blob)
        {
            float3 center = cell.Position;
            float3 up = new float3(0f, 1f, 0f);
            _surfaceNormal = up; // 板的纯法线恒 +Y
            float ir = metrics.InnerRadius;
            float fade = math.clamp(blob.VariationFadeWidth, 0.01f, ir);

            // 逐边连接面法线（一次性算 6 条）
            var sn = new FixedList128Bytes<float3>();
            for (int d = 0; d < 6; d++)
                sn.Add(StripNormal((HexDirection)d, cell, ref metrics, ref blob));

            var rim = new FixedList128Bytes<float3>();   // 板角点（边线交点）
            var inner = new FixedList128Bytes<float3>(); // 内环角点（再内缩 fade，≥5% IR 兜底）
            for (int k = 0; k < 6; k++)
            {
                float l1 = PlateInset(cell, CellEntity, (HexDirection)((k + 5) % 6), ref metrics, ref blob);
                float l2 = PlateInset(cell, CellEntity, (HexDirection)k, ref metrics, ref blob);
                var dPrev = (HexDirection)((k + 5) % 6);
                rim.Add(center + metrics.GetPlateCorner(dPrev, l1, l2));
                inner.Add(center + metrics.GetPlateCorner(dPrev,
                    math.max(l1 - fade, ir * 0.05f),
                    math.max(l2 - fade, ir * 0.05f)));
            }

            float3 indices = new float3(cell.TerrainIndex, cell.TerrainIndex, cell.TerrainIndex);
            float4 wIn = new float4(W100.xyz, 1f);   // 内环/中心：变异权重 1
            float4 wRim = new float4(W100.xyz, 0f);  // 边环：变异权重 0

            for (int d = 0; d < 6; d++)
            {
                var ie = new EdgeVertices(inner[d], inner[(d + 1) % 6]);
                var re = new EdgeVertices(rim[d], rim[(d + 1) % 6]);

                // 边环法线：v1/v5 角点用角交界，v2..v4 用边交界
                float3 jMid = EdgeJunction((HexDirection)d, sn[d]);
                float3 j1 = CornerJunction(d, sn[(d + 5) % 6], sn[d]);
                float3 j5 = CornerJunction((d + 1) % 6, sn[d], sn[(d + 1) % 6]);
                float3 n1 = BlendRim(up, j1);
                float3 nMid = BlendRim(up, jMid);
                float3 n5 = BlendRim(up, j5);

                // 中心扇形 → 内环（4 三角，全 up / 权重 1）
                AddTriangle(center, ie.v1, ie.v2, up, up, up, indices, wIn, wIn, wIn);
                AddTriangle(center, ie.v2, ie.v3, up, up, up, indices, wIn, wIn, wIn);
                AddTriangle(center, ie.v3, ie.v4, up, up, up, indices, wIn, wIn, wIn);
                AddTriangle(center, ie.v4, ie.v5, up, up, up, indices, wIn, wIn, wIn);

                // 内环 → 边环（4 四边形，边环权重 0、法线融合）
                AddQuad(ie.v1, ie.v2, re.v1, re.v2, up, up, n1, nMid, indices, wIn, wIn, wRim, wRim);
                AddQuad(ie.v2, ie.v3, re.v2, re.v3, up, up, nMid, nMid, indices, wIn, wIn, wRim, wRim);
                AddQuad(ie.v3, ie.v4, re.v3, re.v4, up, up, nMid, nMid, indices, wIn, wIn, wRim, wRim);
                AddQuad(ie.v4, ie.v5, re.v4, re.v5, up, up, nMid, n5, indices, wIn, wIn, wRim, wRim);
            }
        }

        // ---------- 顶点追加（位置/法线/splat 索引/权重/变异常量五通道同 append）----------

        /// <summary>
        /// 添加三角形顶点（带扰动）
        /// </summary>
        private void AddTriangle(float3 v1, float3 v2, float3 v3,
            float3 n1, float3 n2, float3 n3, float3 indices, float4 w1, float4 w2, float4 w3)
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
            Normals.Add(n1);
            Normals.Add(n2);
            Normals.Add(n3);
            PureNormals.Add(_surfaceNormal);
            PureNormals.Add(_surfaceNormal);
            PureNormals.Add(_surfaceNormal);
            Variations.Add(_cellVariation);
            Variations.Add(_cellVariation);
            Variations.Add(_cellVariation);
        }

        /// <summary>
        /// 添加三角形顶点（显式位置，不再扰动）。供角落闭合使用：
        /// 伪 cell 角点（名义角 @bottomY）必须保持未扰动与边界坡底/Cap 同点，
        /// 真实板角点则由调用方先 Perturb。
        /// </summary>
        private void AddTriangleRaw(float3 v1, float3 v2, float3 v3,
            float3 n1, float3 n2, float3 n3, float3 indices, float4 w1, float4 w2, float4 w3)
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
            Normals.Add(n1);
            Normals.Add(n2);
            Normals.Add(n3);
            PureNormals.Add(_surfaceNormal);
            PureNormals.Add(_surfaceNormal);
            PureNormals.Add(_surfaceNormal);
            Variations.Add(_cellVariation);
            Variations.Add(_cellVariation);
            Variations.Add(_cellVariation);
        }

        /// <summary>
        /// 添加四边形（2 个三角形，不扰动）。顶点已在上游手动扰动过
        /// （与其它表面共用同一批点），这里再扰动会让同一点出现两个位置。
        /// </summary>
        private void AddQuadUnperturbed(float3 v1, float3 v2, float3 v3, float3 v4,
            float3 n1, float3 n2, float3 n3, float3 n4, float3 indices,
            float4 w1, float4 w2, float4 w3, float4 w4)
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
            Normals.Add(n1);
            Normals.Add(n2);
            Normals.Add(n3);
            Normals.Add(n4);
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
        /// 添加四边形（2 个三角形）
        /// </summary>
        private void AddQuad(float3 v1, float3 v2, float3 v3, float3 v4,
            float3 n1, float3 n2, float3 n3, float3 n4, float3 indices,
            float4 w1, float4 w2, float4 w3, float4 w4)
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
            Normals.Add(n1);
            Normals.Add(n2);
            Normals.Add(n3);
            Normals.Add(n4);
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

            float4 sample = HexMetrics.SampleNoise(ref blob, position);
            // 噪声 [0,1] 映射到 [-1,1]，再乘以以半径为单位的最大位移
            position.x += (sample.x * 2f - 1f) * amplitude;
            position.z += (sample.z * 2f - 1f) * amplitude;
            return position;
        }
    }
}
