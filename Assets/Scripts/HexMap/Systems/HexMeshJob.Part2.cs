using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HexMap
{
    /// <summary>
    /// HexMeshJob Part 2：边带（阶梯 / 平带 / 陡壁）与角落侧面板。
    ///
    /// 边带分级（高格生成，几何限制在本格自己的扇形内）：
    /// - 阶梯（Lower Δe≤2）：高格板沿该边内缩 L，内缩带里造台阶——
    ///   Δe+1 段踏面（带宽 L 等分）+ Δe 段立面（高差等分）；
    ///   顶 = 本格板弦（与板边环同批点），底 = 低格名义板边（低格该边全宽）；
    /// - 陡壁（Lower Δe>2 / Boundary / Streaming / 等高噪声缝壁）：板不内缩，
    ///   本格名义板缘垂直下落到低侧板高（或 bottomY）。顶行与板边环共享同批点；
    ///   底行复制顶行 x/z，与低侧板缘逐点重合。墙柱按角落第三方板高分段
    ///   （防 T-junction 发丝缝）；多级高差角落 = 三条边壁共享角落竖棱（烟囱构造）；
    /// - 平带（非阶梯边）：板弦→名义边的 y_A 平面延伸，通常零宽退化，
    ///   仅在相邻阶梯边切角的角落非退化（斜弦三角形，退化列收三角）；
    /// - 角落侧面板：本格两相邻边带端剖面（沿格心→角点辐射线的折线）之间的
    ///   竖直填充（楼梯侧视挡板），在辐射平面内、零跨格三角形；
    ///   名义角点竖棱由 N(k−1)↔N(k) 那条边自己的几何覆盖。
    ///
    /// splat：阶梯/壁顶行 = 高格地形 W100 → 底行 = 低格地形 W010 随级渐变
    /// （HeightBlend3 在 0→1 干净梯度上按高度图锯齿化，竖向过渡自然）；
    /// 边界/流式壁恒本格地形。变异权重全 0（带/壁是过渡面，与板缘 0 一致，
    /// 共享线两侧无缝）。
    /// </summary>
    public partial struct HexMeshJob
    {
        /// <summary>
        /// 为一个方向生成边带几何（分类 → 分发；板已在 BuildPlate 覆盖内缩多边形）
        /// </summary>
        private void BuildDirection(HexDirection d, HexCellData cell, ref HexMetrics metrics,
            ref HexMapConfigBlob blob, ref FixedList128Bytes<EdgeInfo> edges)
        {
            var e = edges[(int)d];

            switch (e.Kind)
            {
                case EdgeKind.Equal:
                    // 等高密铺零几何；高度噪声竖缝由高侧发薄缝壁（块状拼块的自然错落）
                    BuildFlatBand(d, cell, ref metrics, ref blob, ref edges);
                    if (cell.Position.y > e.NeighborY + 1e-3f)
                        BuildWall(d, cell, e, ref metrics);
                    break;

                case EdgeKind.Higher:
                    // 更低侧的壁由邻居生成；本格只需平带兜住相邻阶梯边的切角
                    BuildFlatBand(d, cell, ref metrics, ref blob, ref edges);
                    break;

                case EdgeKind.Lower:
                    if (e.DeltaE <= StairMaxDelta)
                        BuildStairs(d, cell, e, ref metrics, ref blob, ref edges);
                    else
                    {
                        BuildFlatBand(d, cell, ref metrics, ref blob, ref edges);
                        BuildWall(d, cell, e, ref metrics);
                    }
                    break;

                case EdgeKind.Boundary:
                case EdgeKind.Streaming:
                    // 图外永久壁 / 图内未加载临时壁（邻居加载后标脏重建），都直落 bottomY
                    BuildFlatBand(d, cell, ref metrics, ref blob, ref edges);
                    BuildWall(d, cell, e, ref metrics);
                    break;
            }
        }

        // ---------- 阶梯带 ----------

        /// <summary>
        /// 阶梯带：顶 = 板弦（内缩），底 = 低格名义板边。列 = EdgeVertices 5 段细分，
        /// 端列沿辐射线（与角落面板列同批点）；踏面 Δe+1 段（带宽 L 等分）、
        /// 立面 Δe 段（高差等分），交替展开。Δe=1 → 踏 L/2 → 立 → 踏 L/2。
        /// 踏面法线 +Y，立面法线 = 该边水平外法线。
        /// </summary>
        private void BuildStairs(HexDirection d, HexCellData cell, in EdgeInfo e, ref HexMetrics metrics,
            ref HexMapConfigBlob blob, ref FixedList128Bytes<EdgeInfo> edges)
        {
            int di = (int)d;
            float yTop = cell.Position.y;
            float yBot = e.NeighborY;
            int n = e.DeltaE;
            float stepH = (yTop - yBot) / n;

            // 5 列端点（未扰动）：顶 = 板弦，底 = 名义边
            var top = new EdgeVertices(
                PlateRimCorner(di, cell, ref metrics, ref blob, ref edges),
                PlateRimCorner((di + 1) % 6, cell, ref metrics, ref blob, ref edges));
            var bot = new EdgeVertices(
                SnapToLattice(cell.Position + metrics.Corners[di], ref metrics),
                SnapToLattice(cell.Position + metrics.Corners[di + 1], ref metrics));

            float3 indices = new float3(cell.TerrainIndex, e.NeighborTerrain, e.NeighborTerrain);

            // 踏面（含顶/底段）：踏面边界 {i/(n+1)} ⊆ 通用 s 网格；再按通用网格细分，
            // 使端列（t=0/4）与角落面板列的顶点集逐点一致（扰动后不共线，
            // 细分缝无法靠共线合并折叠，必须同集）
            _surfaceNormal = new float3(0f, 1f, 0f);
            for (int i = 0; i <= n; i++)
            {
                float treadA = (float)i / (n + 1);
                float treadB = (float)(i + 1) / (n + 1);
                float h = StairLevelY(i, n, yTop, yBot, stepH);
                float4 w = StairWeight(i, n);

                for (int u = 0; u + 1 < UniversalSCount; u++)
                {
                    float s0 = math.max(UniversalS(u), treadA);
                    float s1 = math.min(UniversalS(u + 1), treadB);
                    if (s1 - s0 < 1e-5f)
                        continue; // 该通用子段不在本踏面内

                    for (int t = 0; t + 1 < 5; t++)
                    {
                        float3 a = ColumnPoint(top, bot, t, s0, h);
                        float3 b = ColumnPoint(top, bot, t + 1, s0, h);
                        float3 c = ColumnPoint(top, bot, t, s1, h);
                        float3 e2 = ColumnPoint(top, bot, t + 1, s1, h);
                        AddQuadUnperturbed(a, b, c, e2, indices, w, w, w, w);
                    }
                }
            }

            // 立面：踏面断点处左右极限不同即 riser（权重取下侧踏面级）
            _surfaceNormal = metrics.GetEdgeNormal(d);
            for (int i = 1; i <= n; i++)
            {
                float s = (float)i / (n + 1);
                float hTop = StairLevelY(i - 1, n, yTop, yBot, stepH);
                float hBot = StairLevelY(i, n, yTop, yBot, stepH);
                float4 w = StairWeight(i, n);

                for (int t = 0; t + 1 < 5; t++)
                {
                    float3 a = ColumnPoint(top, bot, t, s, hTop);
                    float3 b = ColumnPoint(top, bot, t + 1, s, hTop);
                    float3 c = ColumnPoint(top, bot, t, s, hBot);
                    float3 e2 = ColumnPoint(top, bot, t + 1, s, hBot);
                    AddQuadUnperturbed(a, b, c, e2, indices, w, w, w, w);
                }
            }
        }

        /// <summary>踏面级 splat 权重：顶踏面 W100（高格地形）→ 底踏面 W010（低格地形）</summary>
        private static float4 StairWeight(int level, int n)
        {
            return new float4(math.lerp(W100.xyz, W010.xyz, (float)level / n), 0f);
        }

        /// <summary>阶梯第 i 级踏面高度（端级直取原值，与 ProfileY 同源防 1 ulp 跨桶）</summary>
        private static float StairLevelY(int i, int n, float yTop, float yBot, float stepH)
        {
            if (i <= 0) return yTop;
            if (i >= n) return yBot;
            return yTop - i * stepH;
        }

        /// <summary>通用 s 网格点数（{0, ⅓, ½, ⅔, 1}）</summary>
        private const int UniversalSCount = 5;

        /// <summary>
        /// 通用 s 网格 {0, ⅓, ½, ⅔, 1}：Δe≤2 阶梯的全部踏面断点（Δe=1 的 {½}、
        /// Δe=2 的 {⅓,⅔}）⊆ 此网格。边带端列与角落面板列统一按它分段——
        /// 径向线上的共享边必须顶点集一致；扰动后的细分点不共线，
        /// 靠共线合并折叠不了，只能构造性同集。
        /// </summary>
        private static float UniversalS(int i)
        {
            switch (i)
            {
                case 0: return 0f;
                case 1: return 1f / 3f;
                case 2: return 0.5f;
                case 3: return 2f / 3f;
                default: return 1f;
            }
        }

        // ---------- 平带（非阶梯边的板弦→名义边延伸）----------

        /// <summary>
        /// 平带：非阶梯边（Higher/Equal/Lower Δe&gt;2/Boundary/Streaming 都全宽）的
        /// 板弦到名义边之间的 y_A 平面延伸。两端都未被相邻阶梯边切角时弦 = 名义边，
        /// 零宽整体退化跳过；只有一端切角时为斜弦三角形，退化列收成三角。
        /// </summary>
        private void BuildFlatBand(HexDirection d, HexCellData cell, ref HexMetrics metrics,
            ref HexMapConfigBlob blob, ref FixedList128Bytes<EdgeInfo> edges)
        {
            int di = (int)d;
            float ir = metrics.InnerRadius;
            float bandL = StairBandWidth(ref blob, ir);

            // 端角是否未被切角（语义判定，不用距离——吸附修正量 ~1e-5 会撞几何阈值）
            bool end1Full = IsRimFull(ref edges, di, ir, bandL);
            bool end2Full = IsRimFull(ref edges, (di + 1) % 6, ir, bandL);
            if (end1Full && end2Full)
                return; // 全宽边：弦与名义边重合，零宽

            float3 r1 = PlateRimCorner(di, cell, ref metrics, ref blob, ref edges);
            float3 r2 = PlateRimCorner((di + 1) % 6, cell, ref metrics, ref blob, ref edges);
            float3 c1 = SnapToLattice(cell.Position + metrics.Corners[di], ref metrics);
            float3 c2 = SnapToLattice(cell.Position + metrics.Corners[di + 1], ref metrics);

            _surfaceNormal = new float3(0f, 1f, 0f);
            float y = cell.Position.y;
            float3 indices = new float3(cell.TerrainIndex, cell.TerrainIndex, cell.TerrainIndex);
            float4 w = new float4(W100.xyz, 0f);

            var top = new EdgeVertices(r1, r2);
            var bot = new EdgeVertices(c1, c2);

            // 按通用 s 网格细分（端列与角落面板列同集，防扰动后不共线的细分缝）
            for (int u = 0; u + 1 < UniversalSCount; u++)
            {
                float s0 = UniversalS(u), s1 = UniversalS(u + 1);
                for (int t = 0; t + 1 < 5; t++)
                {
                    // 列退化 = 端列落在未切角端（顶=底=吸附名义点，语义精确）
                    bool d0 = t == 0 && end1Full;
                    bool d1 = t + 1 == 4 && end2Full;
                    if (d0 && d1)
                        continue; // 两列都在全宽段，零宽四边形

                    float3 a = Perturb(ColumnLerp(EdgeV(top, t), EdgeV(bot, t), s0, y));
                    float3 b = Perturb(ColumnLerp(EdgeV(top, t + 1), EdgeV(bot, t + 1), s0, y));
                    float3 c = d0 ? a : Perturb(ColumnLerp(EdgeV(top, t), EdgeV(bot, t), s1, y));
                    float3 e2 = d1 ? b : Perturb(ColumnLerp(EdgeV(top, t + 1), EdgeV(bot, t + 1), s1, y));

                    if (d1)
                        AddTriangleUnperturbed(a, c, b, indices, w, w, w);
                    else if (d0)
                        AddTriangleUnperturbed(b, c, e2, indices, w, w, w);
                    else
                        AddQuadUnperturbed(a, b, c, e2, indices, w, w, w, w);
                }
            }
        }

        /// <summary>列插值（x/z 沿顶→底，y 直取；s=1 直取底端防 1 ulp 漂移）</summary>
        private static float3 ColumnLerp(float3 a, float3 b, float s, float y)
        {
            if (s >= 1f)
                return new float3(b.x, y, b.z);
            return new float3(a.x + (b.x - a.x) * s, y, a.z + (b.z - a.z) * s);
        }

        // ---------- 垂直陡壁 ----------

        /// <summary>
        /// 垂直陡壁：顶 = 本格板缘（扰动），底 = 同批 x/z 落到低侧板高（或 bottomY）；
        /// 行按角落第三方板高分段，列 = EdgeVertices 5 段。
        /// 真实低侧时底行 splat 渐变到低格地形，否则恒本格地形。
        /// </summary>
        private void BuildWall(HexDirection d, HexCellData cell, in EdgeInfo e, ref HexMetrics metrics)
        {
            float topY = cell.Position.y;
            float toY = e.NeighborY;
            if (topY - toY < 1e-3f)
                return; // 防御：零高差不发壁

            // 顶边标称角点（吸附格点，与对侧板缘逐位重合）→ 扰动后的 5 列（与板边环同一批点）
            float3 c1 = SnapToLattice(cell.Position + metrics.Corners[(int)d], ref metrics);
            float3 c2 = SnapToLattice(cell.Position + metrics.Corners[(int)d + 1], ref metrics);
            var top = PerturbEdge(new EdgeVertices(c1, c2));
            var colXZ = new FixedList128Bytes<float2>();
            colXZ.Add(top.v1.xz);
            colXZ.Add(top.v2.xz);
            colXZ.Add(top.v3.xz);
            colXZ.Add(top.v4.xz);
            colXZ.Add(top.v5.xz);

            // 行分段：两端角落第三方（N(d-1) 角 d / N(d+1) 角 d+1）的板高严格落在 (toY, topY)
            var rowY = new FixedList32Bytes<float>();
            rowY.Add(topY);
            TrySplitAtWallCorner(d.Previous(), topY, toY, ref rowY);
            TrySplitAtWallCorner(d.Next(), topY, toY, ref rowY);
            rowY.Add(toY);
            // 降序排列（顶 → 底）
            for (int i = 1; i < rowY.Length; i++)
            {
                float y = rowY[i];
                int j = i - 1;
                while (j >= 0 && rowY[j] < y) { rowY[j + 1] = rowY[j]; j--; }
                rowY[j + 1] = y;
            }

            bool realNeighbor = e.RealNeighbor;
            _surfaceNormal = metrics.GetEdgeNormal(d); // 壁法线 = 逐边水平外法线（直写 NORMAL 通道）
            int lowerTerrain = e.NeighborTerrain;
            float3 indices = new float3(cell.TerrainIndex, lowerTerrain, lowerTerrain);
            float3 rgbBot = realNeighbor ? W010.xyz : W100.xyz;

            float span = topY - toY;
            float4 RowWeight(float y)
                => new float4(math.lerp(W100.xyz, rgbBot, math.saturate((topY - y) / span)), 0f);

            for (int r = 0; r + 1 < rowY.Length; r++)
            {
                if (rowY[r] - rowY[r + 1] < 1e-3f)
                    continue; // 同高度去重后的残余段
                float4 wTop = RowWeight(rowY[r]);
                float4 wBot = RowWeight(rowY[r + 1]);

                for (int c = 0; c + 1 < colXZ.Length; c++)
                {
                    float2 xzL = colXZ[c];
                    float2 xzR = colXZ[c + 1];
                    float3 pTL = new float3(xzL.x, rowY[r], xzL.y);
                    float3 pTR = new float3(xzR.x, rowY[r], xzR.y);
                    float3 pBL = new float3(xzL.x, rowY[r + 1], xzL.y);
                    float3 pBR = new float3(xzR.x, rowY[r + 1], xzR.y);

                    AddQuadUnperturbed(pTL, pTR, pBL, pBR, indices, wTop, wTop, wBot, wBot);
                }
            }
        }

        /// <summary>
        /// 角落第三方格（方向 thirdDir 的邻居）的板高严格落在 (toY, topY) 开区间时，
        /// 加入壁的行分段高度。流式/图外第三方无板面 → 不分段。
        /// </summary>
        private void TrySplitAtWallCorner(HexDirection thirdDir, float topY, float toY,
            ref FixedList32Bytes<float> rowY)
        {
            var third = DataOf(NeighborEntity(CellEntity, thirdDir));
            if (!IsValidData(third))
                return;

            float y = third.Position.y;
            if (y <= toY + 1e-3f || y >= topY - 1e-3f)
                return;

            for (int i = 0; i < rowY.Length; i++)
                if (math.abs(rowY[i] - y) < 1e-3f)
                    return; // 已有同高度分段（两第三方同高）
            rowY.Add(y);
        }

        // ---------- 角落侧面板 ----------

        /// <summary>
        /// 边剖面在 s∈[0,1]（板弦→名义边）处的高度。阶梯边 = 踏/立交替折线；
        /// 其余边恒 yTop（壁/缝壁的竖落发生在名义边线上、由壁面覆盖，不属于剖面）。
        /// leftLimit = 取左极限（立面位置双值用）。
        /// 高度式与 BuildStairs 严格同式（stepH 先除后乘）；端级直取原值——
        /// 中间值差 1 ulp 就会跨过量化桶，与带端列/壁行分段错开成发丝缝。
        /// </summary>
        private static float ProfileY(in EdgeInfo e, float yTop, float s, bool leftLimit)
        {
            if (e.Kind != EdgeKind.Lower || e.DeltaE > StairMaxDelta)
                return yTop;

            int n = e.DeltaE;
            int level = (int)math.floor(s * (n + 1) - (leftLimit ? 1e-3f : 0f));
            level = math.clamp(level, 0, n);
            if (level <= 0)
                return yTop;             // 顶级 = 本格板高（原值）
            if (level >= n)
                return e.NeighborY;      // 底级 = 对侧板高（原值，与低格板缘/壁行分段同源）
            return yTop - level * ((yTop - e.NeighborY) / n);
        }

        /// <summary>
        /// 角落侧面板：本格边 k−1 / 边 k 两带端剖面在辐射平面（格心→角点 k）之间的
        /// 竖直填充（楼梯侧视挡板）。剖面按通用 s 网格分区间（与边带端列同集），
        /// 行按相邻区间端点的并集分段（列顶点集跨区间一致，面板内部无 T 缝）。
        /// s=1 端的闭合竖段只含两剖面终点（= 两邻格板高），与对面边
        /// （N(k−1)↔N(k)）自己的几何（壁行分段 / 对方面板）逐点重合。
        /// </summary>
        private void BuildCornerPanel(int k, HexCellData cell, ref HexMetrics metrics,
            ref HexMapConfigBlob blob, ref FixedList128Bytes<EdgeInfo> edges)
        {
            var eL = edges[(k + 5) % 6]; // 边 k−1（扇形左邻）
            var eR = edges[k];           // 边 k（扇形右邻）
            float yTop = cell.Position.y;

            // 剖面区间 = 通用 s 网格（与边带端列顶点集一致，见 UniversalS 注释）
            int m = UniversalSCount - 1;

            // 逐区间 lo/hi（断点处取右极限 = 区间值）
            var loHi = new FixedList64Bytes<float2>();
            bool any = false;
            for (int i = 0; i < m; i++)
            {
                float yl = ProfileY(eL, yTop, UniversalS(i), false);
                float yr = ProfileY(eR, yTop, UniversalS(i), false);
                loHi.Add(new float2(math.min(yl, yr), math.max(yl, yr)));
                if (math.abs(yl - yr) > 1e-4f)
                    any = true;
            }
            if (!any)
                return; // 两剖面逐段一致，无暴露侧壁

            float3 p = PlateRimCorner(k, cell, ref metrics, ref blob, ref edges); // 内缩板角（辐射线上）
            float3 c = SnapToLattice(cell.Position + metrics.Corners[k], ref metrics); // 名义角点（吸附格点）

            // 渐变终点 = 两剖面终点的较低者（平剖面不降，即面板底 = 阶梯边底）
            float endL = ProfileY(eL, yTop, 1f, false);
            float endR = ProfileY(eR, yTop, 1f, false);
            float yLow = math.min(endL, endR);
            int lowTerrain = endL <= endR ? eL.NeighborTerrain : eR.NeighborTerrain;
            float3 indices = new float3(cell.TerrainIndex, lowTerrain, lowTerrain);

            // 法线基准：s 升 + 行高升的绕序法向 ∝ (r.z, 0, −r.x)；面板朝较低剖面一侧
            float2 rDir = math.normalize(new float2(c.x - p.x, c.z - p.z));
            float3 perpCand = new float3(rDir.y, 0f, -rDir.x);
            bool candToR = math.dot(perpCand, metrics.GetEdgeNormal((HexDirection)k)) > 0f;

            for (int i = 0; i < m; i++)
            {
                float lo = loHi[i].x, hi = loHi[i].y;
                if (hi - lo < 1e-4f)
                    continue;

                // 行高分段：相邻区间（i−1..i+1）端点并集，截到 [lo, hi]
                var rows = new FixedList64Bytes<float>();
                for (int j = math.max(0, i - 1); j <= math.min(m - 1, i + 1); j++)
                {
                    AddUniqueHeight(ref rows, loHi[j].x);
                    AddUniqueHeight(ref rows, loHi[j].y);
                }
                SortDedup(ref rows);

                float yl = ProfileY(eL, yTop, UniversalS(i), false);
                float yr = ProfileY(eR, yTop, UniversalS(i), false);
                bool lowerIsR = yr < yl;
                float3 normal = lowerIsR == candToR ? perpCand : -perpCand;
                bool flip = math.dot(normal, perpCand) < 0f;
                _surfaceNormal = normal;

                float s0 = UniversalS(i), s1 = UniversalS(i + 1);
                for (int r = 0; r + 1 < rows.Length; r++)
                {
                    float h0 = math.clamp(rows[r], lo, hi);
                    float h1 = math.clamp(rows[r + 1], lo, hi);
                    if (h1 - h0 < 1e-4f)
                        continue;

                    float t = math.saturate((yTop - (h0 + h1) * 0.5f) / math.max(yTop - yLow, 1e-3f));
                    float4 w = new float4(math.lerp(W100.xyz, W010.xyz, t), 0f);

                    float3 a = RadialPoint(p, c, s0, h0);
                    float3 b = RadialPoint(p, c, s1, h0);
                    float3 c2 = RadialPoint(p, c, s0, h1);
                    float3 d2 = RadialPoint(p, c, s1, h1);
                    if (!flip)
                        AddQuadUnperturbed(a, b, c2, d2, indices, w, w, w, w);
                    else
                        AddQuadUnperturbed(b, a, d2, c2, indices, w, w, w, w);
                }
            }
        }

        /// <summary>
        /// 辐射线上的面板/端列点：p（板角）→ c（名义角）按 s 插值，y = h，扰动一次。
        /// s=1 直取 c：lerp(a,b,1) = a+(b−a) 可能差 1 ulp，名义角点必须逐位重合。
        /// </summary>
        private float3 RadialPoint(float3 p, float3 c, float s, float y)
        {
            float3 pos = s >= 1f ? c : p + (c - p) * s;
            pos.y = y;
            return Perturb(pos);
        }

        /// <summary>带列点：列 t 顶（板弦）→ 底（名义边）按 s 插值，y = h，扰动一次（s=1 直取底端）</summary>
        private float3 ColumnPoint(in EdgeVertices top, in EdgeVertices bot, int t, float s, float y)
        {
            float3 a = EdgeV(top, t);
            float3 b = EdgeV(bot, t);
            float3 pos = s >= 1f ? b : a + (b - a) * s;
            pos.y = y;
            return Perturb(pos);
        }

        private static float3 EdgeV(in EdgeVertices e, int i)
        {
            switch (i)
            {
                case 0: return e.v1;
                case 1: return e.v2;
                case 2: return e.v3;
                case 3: return e.v4;
                default: return e.v5;
            }
        }

        private static void AddUniqueHeight(ref FixedList64Bytes<float> list, float y)
        {
            for (int i = 0; i < list.Length; i++)
                if (math.abs(list[i] - y) < 1e-4f)
                    return;
            list.Add(y);
        }

        /// <summary>升序排序 + 相邻去重（FixedList 小列表，插入排序足够）</summary>
        private static void SortDedup(ref FixedList64Bytes<float> list)
        {
            int n = list.Length;
            for (int i = 1; i < n; i++)
            {
                float v = list[i];
                int j = i - 1;
                while (j >= 0 && list[j] > v) { list[j + 1] = list[j]; j--; }
                list[j + 1] = v;
            }
            int w = 0;
            for (int i = 0; i < n; i++)
            {
                if (w > 0 && math.abs(list[w - 1] - list[i]) < 1e-4f)
                    continue;
                list[w] = list[i];
                w++;
            }
            if (w < n)
                list.Length = w;
        }
    }
}
