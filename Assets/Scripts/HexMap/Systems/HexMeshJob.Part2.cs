using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HexMap
{
    /// <summary>
    /// HexMeshJob Part 2：边带（阶梯 / 平带 / 陡壁）与阶梯缺口侧面板。
    ///
    /// 边带分级（高格生成，几何限制在本格自己的扇形内）：
    /// - 阶梯（Lower Δe≤2）：高格板沿该边内缩 L；台阶只占边中段一半（列 v2..v4），
    ///   每级 2 梯（n = 2Δe 段立面 + n+1 段踏面，带宽 L 等分）——
    ///   顶 = 本格板弦（与板边环同批点），底 = 低格名义板边（低格该边全宽）；
    ///   两翼（列 v1..v2 / v4..v5）= 板平面 y_A 延伸 + 名义边垂直壁（悬崖在翼侧保持完整）；
    ///   缺口两侧（列 v2/v4）发「楼梯侧面板」：从踏面剖面向上到 y_A 的竖直挡板，
    ///   面朝缺口内侧（彼此相向）；
    /// - 陡壁（Lower Δe>2 / Boundary / Streaming / 等高噪声缝壁）：板不内缩，
    ///   本格名义板缘垂直下落到低侧板高（或 bottomY）。顶行与板边环共享同批点；
    ///   底行复制顶行 x/z，与低侧板缘逐点重合。墙柱按角落第三方板高分段
    ///   （防 T-junction 发丝缝）；多级高差角落 = 三条边壁共享角落竖棱（烟囱构造）；
    /// - 平带（非阶梯边）：板弦→名义边的 y_A 平面延伸，通常零宽退化，
    ///   仅在相邻阶梯边切角的角落非退化（斜弦三角形，退化列收三角）；
    /// - 台阶不达角落 → 角列剖面恒平，角落封闭全部由垂直壁的角柱分段 + 烟囱承担，
    ///   零跨格三角形、无需角落面板。
    ///
    /// 共享线顶点集纪律（水密关键）：沿边 s 方向（板弦 s=0 → 名义边 s=1）统一用
    /// BandS 9 点网格 {0,⅕,⅓,⅖,½,⅗,⅔,⅘,1}（⊇ 通用 5 点 ⊇ Δe∈{1,2} 的 n=2/4
    /// 全部踏面断点 {⅓,⅔}/{⅕,⅖,⅗,⅘}）——板环/踏面/翼平带/侧面板的共享列
    /// 各自独立计算、顶点逐位重合；竖直方向行集 = {顶,底} ∪ 角落第三方板高分段
    /// ∪ 全部踏面高，翼壁与侧面板共用同一集合（踏面/立面端边与壁缺口列无缝）。
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
                    // 等高密铺零带几何；高度噪声竖缝由高侧发薄缝壁（块状拼块的自然错落）
                    BuildFlatBand(d, cell, ref metrics, ref blob, ref edges);
                    if (cell.Position.y > e.NeighborY + 1e-3f)
                    {
                        var rows = new FixedList64Bytes<float>();
                        BuildWallRows(d, cell.Position.y, e.NeighborY, ref rows);
                        BuildWall(d, cell, e, ref metrics, 0, 4, in rows);
                    }
                    break;

                case EdgeKind.Higher:
                    // 更低侧的壁由邻居生成；本格只需平带兜住相邻阶梯边的切角
                    BuildFlatBand(d, cell, ref metrics, ref blob, ref edges);
                    break;

                case EdgeKind.Lower:
                    if (e.DeltaE <= StairMaxDelta)
                    {
                        // 阶梯边整体：中段台阶 + 两翼平带/壁 + 缺口侧面板
                        BuildStairs(d, cell, e, ref metrics, ref blob, ref edges);
                    }
                    else
                    {
                        BuildFlatBand(d, cell, ref metrics, ref blob, ref edges);
                        var rows = new FixedList64Bytes<float>();
                        BuildWallRows(d, cell.Position.y, e.NeighborY, ref rows);
                        BuildWall(d, cell, e, ref metrics, 0, 4, in rows);
                    }
                    break;

                case EdgeKind.Boundary:
                case EdgeKind.Streaming:
                    // 图外永久壁 / 图内未加载临时壁（邻居加载后标脏重建），都直落 bottomY
                    BuildFlatBand(d, cell, ref metrics, ref blob, ref edges);
                    {
                        var rows = new FixedList64Bytes<float>();
                        BuildWallRows(d, cell.Position.y, e.NeighborY, ref rows);
                        BuildWall(d, cell, e, ref metrics, 0, 4, in rows);
                    }
                    break;
            }
        }

        // ---------- 阶梯带（中段台阶 + 两翼 + 缺口侧面板）----------

        /// <summary>
        /// 阶梯边（Lower Δe≤2）：台阶只占边中段一半（列 v2..v4），每级 2 梯
        /// （n = 2Δe 段立面 + n+1 段踏面，带宽 L 等分）；两翼 = 板平面 y_A 延伸 +
        /// 名义边垂直壁（悬崖保持完整，台阶像在崖顶挖出的缺口）；缺口两侧楼梯侧面板。
        /// 共享线顶点集纪律见类注释（BandS 网格 + 全局行集）。
        /// </summary>
        private void BuildStairs(HexDirection d, HexCellData cell, in EdgeInfo e, ref HexMetrics metrics,
            ref HexMapConfigBlob blob, ref FixedList128Bytes<EdgeInfo> edges)
        {
            int di = (int)d;
            float yTop = cell.Position.y;
            float yBot = e.NeighborY;
            int n = e.DeltaE * StairStepsPerLevel; // Δe=1→2 级、Δe=2→4 级
            float stepH = (yTop - yBot) / n;

            // 5 列端点（未扰动）：顶 = 板弦（内缩），底 = 名义边（低格板缘同批点）
            var top = new EdgeVertices(
                PlateRimCorner(di, cell, ref metrics, ref blob, ref edges),
                PlateRimCorner((di + 1) % 6, cell, ref metrics, ref blob, ref edges));
            var bot = new EdgeVertices(
                SnapToLattice(cell.Position + metrics.Corners[di], ref metrics),
                SnapToLattice(cell.Position + metrics.Corners[di + 1], ref metrics));

            float3 indices = new float3(cell.TerrainIndex, e.NeighborTerrain, e.NeighborTerrain);
            float3 rgbBot = e.RealNeighbor ? W010.xyz : W100.xyz;
            float bandH = BlendBandHeight(metrics.InnerRadius);

            // 竖直全局行集：{顶,底} ∪ 角落第三方分段 ∪ 全部踏面高
            // （翼壁缺口列 / 侧面板 s=1 列 / 踏面立面端边共用，防 T 缝）
            var rows = new FixedList64Bytes<float>();
            BuildWallRows(d, yTop, yBot, ref rows);
            for (int i = 1; i < n; i++)
                AddUniqueHeight(ref rows, StairLevelY(i, n, yTop, yBot, stepH));
            SortDesc(ref rows);

            // ---- (a) 中段踏面：列 v2..v4，BandS 网格细分（端列与翼平带/侧面板同集）----
            // 权重 = 固定带曲线（地面往上 50px 内从 50/50 渐到纯高格，与壁同一条）
            _surfaceNormal = new float3(0f, 1f, 0f);
            for (int i = 0; i <= n; i++)
            {
                float treadA = (float)i / (n + 1);
                float treadB = (float)(i + 1) / (n + 1);
                float h = StairLevelY(i, n, yTop, yBot, stepH);
                float4 w = BlendBandWeight(h, yBot, bandH, rgbBot);

                for (int u = 0; u + 1 < BandSCount; u++)
                {
                    float s0 = math.max(BandS(u), treadA);
                    float s1 = math.min(BandS(u + 1), treadB);
                    if (s1 - s0 < 1e-5f)
                        continue; // 该 BandS 子段不在本踏面内

                    for (int t = StairColFirst; t < StairColLast; t++)
                    {
                        float3 a = ColumnPoint(top, bot, t, s0, h);
                        float3 b = ColumnPoint(top, bot, t + 1, s0, h);
                        float3 c = ColumnPoint(top, bot, t, s1, h);
                        float3 e2 = ColumnPoint(top, bot, t + 1, s1, h);
                        AddQuadUnperturbed(a, b, c, e2, indices, w, w, w, w);
                    }
                }
            }

            // ---- (b) 中段立面：s = i/(n+1)（BandS 断点），列 v2..v4。
            // 逐行权重与壁/侧面板同一条固定带曲线（转角处带宽高度一致，
            // 整块单权重会在踏步转角与侧面板渐变错位）
            _surfaceNormal = metrics.GetEdgeNormal(d);
            for (int i = 1; i <= n; i++)
            {
                float s = (float)i / (n + 1);
                float hTop = StairLevelY(i - 1, n, yTop, yBot, stepH);
                float hBot = StairLevelY(i, n, yTop, yBot, stepH);
                float4 wTop = BlendBandWeight(hTop, yBot, bandH, rgbBot);
                float4 wBot = BlendBandWeight(hBot, yBot, bandH, rgbBot);

                for (int t = StairColFirst; t < StairColLast; t++)
                {
                    float3 a = ColumnPoint(top, bot, t, s, hTop);
                    float3 b = ColumnPoint(top, bot, t + 1, s, hTop);
                    float3 c = ColumnPoint(top, bot, t, s, hBot);
                    float3 e2 = ColumnPoint(top, bot, t + 1, s, hBot);
                    AddQuadUnperturbed(a, b, c, e2, indices, wTop, wTop, wBot, wBot);
                }
            }

            // ---- (c) 两翼平带：板弦→名义边 y_A 延伸（列 0..1 / 3..4，s=BandS 网格）----
            // s=0 与板环弦共点、s=1 与翼壁顶行共点；v2/v4 列与侧面板顶边同集
            _surfaceNormal = new float3(0f, 1f, 0f);
            float4 wFlat = new float4(W100.xyz, 0f);
            float3 flatIdx = new float3(cell.TerrainIndex, cell.TerrainIndex, cell.TerrainIndex);
            for (int u = 0; u + 1 < BandSCount; u++)
            {
                float s0 = BandS(u), s1 = BandS(u + 1);
                for (int side = 0; side < 2; side++)
                {
                    int tA = side == 0 ? 0 : StairColLast;
                    int tB = tA + 1;
                    float3 a = ColumnPoint(top, bot, tA, s0, yTop);
                    float3 b = ColumnPoint(top, bot, tB, s0, yTop);
                    float3 c = ColumnPoint(top, bot, tA, s1, yTop);
                    float3 d2 = ColumnPoint(top, bot, tB, s1, yTop);
                    AddQuadUnperturbed(a, b, c, d2, flatIdx, wFlat, wFlat, wFlat, wFlat);
                }
            }

            // ---- (d) 两翼垂直壁：名义边 yTop→yBot（角列第三方分段 + 踏面高行集）----
            BuildWall(d, cell, e, ref metrics, 0, StairColFirst, in rows);
            BuildWall(d, cell, e, ref metrics, StairColLast, 4, in rows);

            // ---- (e) 缺口侧面板 ×2：v2 面朝 +切向、v4 面朝 −切向（彼此相向对缺口内）----
            float3 tangent = math.normalize(new float3(
                EdgeV(bot, 4).x - EdgeV(bot, 0).x, 0f, EdgeV(bot, 4).z - EdgeV(bot, 0).z));
            BuildStairSidePanel(top, bot, StairColFirst, tangent, n, yTop, yBot, stepH, bandH, rgbBot, in rows, indices);
            BuildStairSidePanel(top, bot, StairColLast, -tangent, n, yTop, yBot, stepH, bandH, rgbBot, in rows, indices);
        }

        /// <summary>
        /// 楼梯侧面板：缺口一侧（列 colT）从踏面剖面向上到 y_A 的竖直挡板。
        /// 区间 = BandS 网格（区间踏面级取右极限），行 = 全局行集截到 [h, yTop]
        /// （含全部踏面高 → 与踏面/立面端边、翼壁缺口列逐点同集）。
        /// 绕向按「几何法向 ∝ cross(sDir, up) 与期望朝向点积」判定翻转。
        /// </summary>
        private void BuildStairSidePanel(in EdgeVertices top, in EdgeVertices bot, int colT,
            float3 facing, int n, float yTop, float yBot, float stepH, float bandH, float3 rgbBot,
            in FixedList64Bytes<float> rows, float3 indices)
        {
            float3 sDir = math.normalize(new float3(
                EdgeV(bot, colT).x - EdgeV(top, colT).x, 0f, EdgeV(bot, colT).z - EdgeV(top, colT).z));
            float3 winding = math.normalize(math.cross(sDir, new float3(0f, 1f, 0f)));
            bool flip = math.dot(facing, winding) < 0f;
            _surfaceNormal = facing;

            for (int u = 0; u + 1 < BandSCount; u++)
            {
                float s0 = BandS(u), s1 = BandS(u + 1);
                int level = math.clamp((int)math.floor(s0 * (n + 1)), 0, n); // 右极限
                float h = StairLevelY(level, n, yTop, yBot, stepH);
                if (yTop - h < 1e-4f)
                    continue; // 顶级与板齐平，无暴露挡板

                for (int r = 0; r + 1 < rows.Length; r++)
                {
                    float h0 = math.clamp(rows[r], h, yTop);
                    float h1 = math.clamp(rows[r + 1], h, yTop);
                    if (h0 - h1 < 1e-4f)
                        continue;

                    // 权重与壁同一条固定带曲线（地面往上 50px 内 50/50 → 纯高格）
                    float4 w = BlendBandWeight((h0 + h1) * 0.5f, yBot, bandH, rgbBot);

                    float3 a = ColumnPoint(top, bot, colT, s0, h0);
                    float3 b = ColumnPoint(top, bot, colT, s1, h0);
                    float3 c = ColumnPoint(top, bot, colT, s0, h1);
                    float3 d2 = ColumnPoint(top, bot, colT, s1, h1);
                    if (!flip)
                        AddQuadUnperturbed(a, b, c, d2, indices, w, w, w, w);
                    else
                        AddQuadUnperturbed(b, a, d2, c, indices, w, w, w, w);
                }
            }
        }

        /// <summary>阶梯第 i 级踏面高度（端级直取原值——中间值差 1 ulp 就会跨量化桶成发丝缝）</summary>
        private static float StairLevelY(int i, int n, float yTop, float yBot, float stepH)
        {
            if (i <= 0) return yTop;
            if (i >= n) return yBot;
            return yTop - i * stepH;
        }

        /// <summary>BandS 网格点数（{0,⅕,⅓,⅖,½,⅗,⅔,⅘,1}）</summary>
        private const int BandSCount = 9;

        /// <summary>
        /// 沿边 s 网格（板弦 s=0 → 名义边 s=1）：通用 5 点 {0,⅓,½,⅔,1}（平带/板环共享）
        /// ∪ n=2 踏面断点 {⅓,⅔} ∪ n=4 踏面断点 {⅕,⅖,⅗,⅘}——全部表面统一用它，
        /// 共享列上的顶点集逐点一致（扰动后不共线，细分缝无法靠共线合并折叠，只能构造性同集）。
        /// </summary>
        private static float BandS(int i)
        {
            switch (i)
            {
                case 0: return 0f;
                case 1: return 0.2f;
                case 2: return 1f / 3f;
                case 3: return 0.4f;
                case 4: return 0.5f;
                case 5: return 0.6f;
                case 6: return 2f / 3f;
                case 7: return 0.8f;
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

            // 按 BandS 网格细分（端列与阶梯翼平带/侧面板列同集，防扰动后不共线的细分缝）
            for (int u = 0; u + 1 < BandSCount; u++)
            {
                float s0 = BandS(u), s1 = BandS(u + 1);
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
        /// 垂直陡壁：顶 = 本格名义板缘（扰动，与板环/平带 s=1 边同批点），底 = 同批 x/z
        /// 落到低侧板高（真实低侧）；图外/流式底 = 未扰动名义边（与轮廓 Cap 直边共线）。
        /// 行 = 预算好的降序行集（角柱分段 + 阶梯踏面高），列范围 [colFrom, colTo)——
        /// 整边壁传 (0,4)，阶梯两翼传 (0,1)/(3,4)。splat 恒纯高格地形（2026-09-21 定案）。
        /// </summary>
        private void BuildWall(HexDirection d, HexCellData cell, in EdgeInfo e, ref HexMetrics metrics,
            int colFrom, int colTo, in FixedList64Bytes<float> rowY)
        {
            float topY = cell.Position.y;
            float toY = e.NeighborY;
            if (topY - toY < 1e-3f)
                return; // 防御：零高差不发壁

            // 顶边标称角点（吸附格点，与对侧板缘逐位重合）→ 扰动后的 5 列（与板边环同一批点）。
            // 底边复制顶行 x/z（壁恒垂直）：真实低侧与低格板缘同一批点；图外与轮廓 Cap 的
            // 扰动轮廓同一批点（Cap 走同一 PerturbPosition 链，2026-09-21 黑区修复定案——
            // 壁底若改成未扰动名义边，壁身倾斜会与相邻垂直几何在角柱处张开楔形缝）
            float3 c1 = SnapToLattice(cell.Position + metrics.Corners[(int)d], ref metrics);
            float3 c2 = SnapToLattice(cell.Position + metrics.Corners[(int)d + 1], ref metrics);
            var top = PerturbEdge(new EdgeVertices(c1, c2));
            var bot = top;

            _surfaceNormal = metrics.GetEdgeNormal(d); // 壁法线 = 逐边水平外法线（直写 NORMAL 通道）
            // 2026-09-21 二次定案：恢复高度混合——壁底 50/50（与低格板缘线同色），
            // 在固定带宽内（地面往上 50 纹理像素）渐到纯高格地形，带上方恒纯高格。
            // 固定带宽不随壁高缩放（按比例的过渡带在矮壁上太窄、高壁上糊半面）。
            float bandH = BlendBandHeight(metrics.InnerRadius);
            float3 indices = new float3(cell.TerrainIndex, e.NeighborTerrain, e.NeighborTerrain);
            float3 rgbBot = e.RealNeighbor ? W010.xyz : W100.xyz;

            for (int r = 0; r + 1 < rowY.Length; r++)
            {
                if (rowY[r] - rowY[r + 1] < 1e-3f)
                    continue; // 同高度去重后的残余段
                float4 wTop = BlendBandWeight(rowY[r], toY, bandH, rgbBot);
                float4 wBot = BlendBandWeight(rowY[r + 1], toY, bandH, rgbBot);

                for (int c = colFrom; c < colTo; c++)
                {
                    float3 pTL = WithY(EdgeV(top, c), rowY[r]);
                    float3 pTR = WithY(EdgeV(top, c + 1), rowY[r]);
                    float3 pBL = WithY(EdgeV(bot, c), rowY[r + 1]);
                    float3 pBR = WithY(EdgeV(bot, c + 1), rowY[r + 1]);
                    AddQuadUnperturbed(pTL, pTR, pBL, pBR, indices, wTop, wTop, wBot, wBot);
                }
            }
        }

        /// <summary>垂直面混合带宽度（世界单位）：地面往上 50 纹理像素。
        /// 侧面 V = worldY / (2·IR)，贴图数组 1024px 平铺一格脚印 → 50px = 50·2·IR/1024 ≈ 0.85。</summary>
        private static float BlendBandHeight(float ir)
            => BlendBandPixels / BlendBandTexSize * (2f * ir);

        /// <summary>混合带像素宽（用户定案 50px；改这里调带宽）</summary>
        private const float BlendBandPixels = 50f;
        /// <summary>地形贴图数组边长（像素）</summary>
        private const float BlendBandTexSize = 1024f;

        /// <summary>
        /// 垂直面的固定带混合权重：y=yBot（地面）处 50/50（与低格板缘线同色无缝），
        /// 带宽内线性渐到纯高格地形，带上方恒纯高格。壁/踏面/立面/侧面板共用同一条曲线。
        /// </summary>
        private static float4 BlendBandWeight(float y, float yBot, float bandH, float3 rgbBot)
        {
            float t = math.saturate((y - yBot) / bandH);
            return new float4(math.lerp(0.5f * (W100.xyz + rgbBot), W100.xyz, t), 0f);
        }

        /// <summary>
        /// 壁行集（降序）：{顶,底} ∪ 两端角落第三方板高分段（严格开区间、去重）。
        /// 阶梯边在此基础上再并入踏面高（BuildStairs），翼壁缺口列与侧面板共用。
        /// </summary>
        private void BuildWallRows(HexDirection d, float topY, float toY, ref FixedList64Bytes<float> rows)
        {
            rows.Add(topY);
            TrySplitAtWallCorner(d.Previous(), topY, toY, ref rows);
            TrySplitAtWallCorner(d.Next(), topY, toY, ref rows);
            rows.Add(toY);
            SortDesc(ref rows);
        }

        /// <summary>
        /// 角落第三方格（方向 thirdDir 的邻居）的板高严格落在 (toY, topY) 开区间时，
        /// 加入壁的行分段高度。流式/图外第三方无板面 → 不分段。
        /// </summary>
        private void TrySplitAtWallCorner(HexDirection thirdDir, float topY, float toY,
            ref FixedList64Bytes<float> rowY)
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

        // ---------- 共享小工具 ----------

        /// <summary>把（已扰动的）顶点改落到指定高度（x/z 不变）</summary>
        private static float3 WithY(float3 p, float y) => new float3(p.x, y, p.z);

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

        /// <summary>降序排序（FixedList 小列表，插入排序足够；同高度由 AddUniqueHeight 去重）</summary>
        private static void SortDesc(ref FixedList64Bytes<float> list)
        {
            int n = list.Length;
            for (int i = 1; i < n; i++)
            {
                float v = list[i];
                int j = i - 1;
                while (j >= 0 && list[j] < v) { list[j + 1] = list[j]; j--; }
                list[j + 1] = v;
            }
        }
    }
}
