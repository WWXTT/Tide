using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HexMap
{
    /// <summary>
    /// HexMeshJob Part 2：连接几何（桥/坡/流式墙）+ 角落闭合。
    ///
    /// 桥（Δh=0）：本格与邻格板角间的平带（splat A→B 渐变），方向 ∈ {NE,E,SE} 一侧生成。
    /// 坡（Δh≠0）：占高 cell 面积——顶边 = 高格内缩板角（高程），底边 = 低格铺满板角（低程），
    /// 高格生成。边带两端与两侧板环逐点重合（同批标称角点 + 同 EdgeVertices 插值 + 同 Perturb）。
    /// 边界坡 = 同函数的伪邻居分支：底边落名义角 @bottomY（不扰动，与轮廓 Cap 同点）。
    /// 流式墙（邻居未加载）：本格板缘（该方向铺满不内缩）直落 bottomY，邻居加载后按实际边型重建。
    ///
    /// 法线：坡 = 逐边解析常量 SlopeNormal；桥 = +Y；墙 = 水平朝缺 cell；
    /// 首/末行与角点按 rim 融合向交界法线过渡（共享线两侧用同一交界值）。
    /// 变异权重：桥/角/墙全 0（过渡区）；坡按列 0→peak→0（首末列 0）。
    ///
    /// 角落闭合（角 k = 边 k-1 与 k 之间的顶点，三方 A/N(k)/N(k-1) 相遇）：
    /// - 三方皆真实：单三角形（三方板角点），三边恰为三条连接带端截面 → 构造性水密；
    ///   归属 = Elevation 最高者，平手 offset 字典序最小（三方对称可算）。
    /// - 一方为边界伪 cell（图外）：三角形，伪角 = 名义角 @bottomY（不扰动）。
    /// - 一方为流式伪 cell（图内未加载）：竖直楔形（两个真实板角点直落 bottomY），
    ///   与两条流式墙端截面重合。
    /// - 双方皆缺：两条墙/坡的端截面在同一点重合，天然闭合，跳过。
    /// </summary>
    public partial struct HexMeshJob
    {
        /// <summary>
        /// 为一个方向生成连接几何（分类 → 分发；板已在 BuildPlate 覆盖全部内缩情形）
        /// </summary>
        private void BuildDirection(HexDirection d, HexCellData cell, ref HexMetrics metrics,
            ref HexMapConfigBlob blob)
        {
            var kind = ClassifyEdge(cell, CellEntity, d, ref blob, out var neighbor, out var neighborEntity);

            switch (kind)
            {
                case EdgeKind.Bridge:
                    // 平桥：方向 ∈ {NE,E,SE} 一侧生成（对称规则的另一半由邻居反方向承担）
                    if ((int)d <= (int)HexDirection.SE)
                        BuildBridge(d, cell, neighbor, neighborEntity, ref metrics, ref blob);
                    break;

                case EdgeKind.SlopeLower:
                    break; // 坡占高 cell（邻居）面积，由邻居生成

                case EdgeKind.SlopeHigher:
                    BuildSlope(d, cell, neighbor, neighborEntity, ref metrics, ref blob, boundary: false);
                    break;

                case EdgeKind.Boundary:
                    // 伪邻居：高度 bottomY、板 = 名义六边形 → 走坡函数的边界分支
                    BuildSlope(d, cell, neighbor, neighborEntity, ref metrics, ref blob, boundary: true);
                    break;

                case EdgeKind.Streaming:
                    BuildStreamingWall(d, cell, ref metrics, ref blob);
                    break;
            }
        }

        /// <summary>
        /// 等高平桥：本格板角 d..d+1 ↔ 邻格板角（本格角 d ↔ 邻格角 dOpp+1 对齐 v1↔v1，
        /// 两侧板内缩级别不同时带是梯形而非平行四边形，参数化连接仍逐点重合）。
        /// 法线全 +Y、变异权重全 0（板缘 0 = 桥 0，共享线两侧一致）。
        /// </summary>
        private void BuildBridge(HexDirection d, HexCellData cell, HexCellData neighbor, Entity neighborEntity,
            ref HexMetrics metrics, ref HexMapConfigBlob blob)
        {
            int dOpp = (int)d.Opposite();
            float3 c1 = PlateCorner(cell, CellEntity, (int)d, ref metrics, ref blob);
            float3 c2 = PlateCorner(cell, CellEntity, ((int)d + 1) % 6, ref metrics, ref blob);
            float3 b1 = PlateCorner(neighbor, neighborEntity, (dOpp + 1) % 6, ref metrics, ref blob);
            float3 b2 = PlateCorner(neighbor, neighborEntity, dOpp, ref metrics, ref blob);

            var e1 = new EdgeVertices(c1, c2);
            var e2 = new EdgeVertices(b1, b2);

            float3 up = new float3(0f, 1f, 0f);
            _surfaceNormal = up;
            float3 indices = new float3(cell.TerrainIndex, neighbor.TerrainIndex, neighbor.TerrainIndex);
            float4 wTop = new float4(W100.xyz, 0f);
            float4 wBot = new float4(W010.xyz, 0f);

            AddQuad(e1.v1, e1.v2, e2.v1, e2.v2, up, up, up, up, indices, wTop, wTop, wBot, wBot);
            AddQuad(e1.v2, e1.v3, e2.v2, e2.v3, up, up, up, up, indices, wTop, wTop, wBot, wBot);
            AddQuad(e1.v3, e1.v4, e2.v3, e2.v4, up, up, up, up, indices, wTop, wTop, wBot, wBot);
            AddQuad(e1.v4, e1.v5, e2.v4, e2.v5, up, up, up, up, indices, wTop, wTop, wBot, wBot);

            // 角落吸收（与坡同一套规则）：端列 2 顶点（扰动）与三角形边共线
            EmitAbsorbedCorner(((int)d + 1) % 6, cell, neighbor, requireWinnerIsCell: true,
                Perturb(e1.v5), Perturb(e2.v5), up, indices, W010.xyz, ref metrics, ref blob);
            EmitAbsorbedCorner((int)d, cell, neighbor, requireWinnerIsCell: false,
                Perturb(e1.v1), Perturb(e2.v1), up, indices, W010.xyz, ref metrics, ref blob);
        }

        /// <summary>
        /// 高度差坡带（含边界分支），5 行 × 5 列网格。
        ///
        /// 法线场（关键）：坡带只有边界没有内部分区时，棱线融合会经插值污染整个坡面
        /// （朝上倾斜 → 三平面权重被顶面投影接管 → 高差大时 5:1 竖向拉伸拉丝）。
        /// 改为带状分布：顶/底棱线行（带宽 BandFraction）融合向交界法线（保留
        /// 「连成一片」的边缘过渡），内部三行用纯坡法线（侧面投影、纹素无拉伸）；
        /// 首/末列同理向角交界融合（与角落闭合三角形的边对齐）。
        ///
        /// splat：顶行 W100（高格地形）→ 底行 W010（低格地形）随行平滑渐变。
        /// 变异权重全 0（坡是边界过渡面）。边界分支底行不扰动（名义角，与 Cap 同点），
        /// 其交界法线换成「坡⊕朝下」（与 Cap −Y 交界）。
        /// </summary>
        private void BuildSlope(HexDirection d, HexCellData cell, HexCellData lower, Entity lowerEntity,
            ref HexMetrics metrics, ref HexMapConfigBlob blob, bool boundary)
        {
            float3 down = new float3(0f, -1f, 0f);
            const int Rows = 5;      // t = 0(顶) .. 1(底)
            const int Cols = 5;      // 横向 v1..v5
            const float BandFraction = 0.25f; // 棱线融合带占坡高比例

            float3 c1 = PlateCorner(cell, CellEntity, (int)d, ref metrics, ref blob);
            float3 c2 = PlateCorner(cell, CellEntity, ((int)d + 1) % 6, ref metrics, ref blob);

            float3 b1, b2;
            float dh;
            if (boundary)
            {
                // 伪 cell 板 = 名义六边形：底边 = 本格名义角 @bottomY（地图边缘，不扰动）
                float bottomY = GetBottomY(ref metrics);
                b1 = cell.Position + metrics.GetFirstCorner(d);
                b2 = cell.Position + metrics.GetSecondCorner(d);
                b1.y = bottomY;
                b2.y = bottomY;
                dh = cell.Position.y - bottomY;
            }
            else
            {
                int dOpp = (int)d.Opposite();
                b1 = PlateCorner(lower, lowerEntity, (dOpp + 1) % 6, ref metrics, ref blob);
                b2 = PlateCorner(lower, lowerEntity, dOpp, ref metrics, ref blob);
                dh = cell.Position.y - lower.Position.y;
            }

            var eTop = new EdgeVertices(c1, c2);
            var eBot = new EdgeVertices(b1, b2);

            // 坡法线（逐边常量）与交界法线（与板环/角落共用同一套公式 → 共享线两侧一致）
            float3 nSlope = HexMetrics.SlopeNormal(metrics.GetEdgeNormal(d), dh, metrics.SlopeInset);
            float3 snPrev = StripNormal(d.Previous(), cell, ref metrics, ref blob);
            float3 snNext = StripNormal(d.Next(), cell, ref metrics, ref blob);
            float3 j0 = CornerJunction((int)d, snPrev, nSlope);       // 角 d 侧
            float3 jMid = EdgeJunction(d, nSlope);                    // 中段棱线
            float3 j4 = CornerJunction(((int)d + 1) % 6, nSlope, snNext); // 角 d+1 侧
            float3 jDown = math.normalize(nSlope + down);             // 边界底行：与 Cap(−Y) 交界
            _surfaceNormal = nSlope; // 坡的纯法线（光照/阴影用），融合版进 TANGENT

            // 行/列网格：位置（名义 lerp）+ 法线（带状融合），本地函数按需计算
            // （结构体局部函数不能访问 this，实例量先拷贝为局部）
            float rimBlend = _rimBlend;
            float3 idxBot = boundary
                ? new float3(cell.TerrainIndex, cell.TerrainIndex, cell.TerrainIndex)
                : new float3(cell.TerrainIndex, lower.TerrainIndex, lower.TerrainIndex);
            float3 rgbTop = W100.xyz;
            float3 rgbBot = boundary ? W100.xyz : W010.xyz;

            // 列端点先扰动、行间线性插值（共线规则）：
            // 端列的中间行必须落在两端点扰动的直线段上，被吸收的角落三角形
            // （单段边）才能与端列逐点重合——否则 Perturb(lerp) 会在每个坡角
            // 留下 T-junction 发丝缝。
            var colTop = new FixedList128Bytes<float3>();
            var colBot = new FixedList128Bytes<float3>();
            for (int c = 0; c < Cols; c++)
            {
                colTop.Add(Perturb(EdgeAt(eTop, c)));
                // 边界底行 = 名义角不扰动（与轮廓 Cap 同点）
                colBot.Add(boundary ? EdgeAt(eBot, c) : Perturb(EdgeAt(eBot, c)));
            }

            float3 GridPos(int row, int col)
            {
                float t = row / (float)(Rows - 1);
                return math.lerp(colTop[col], colBot[col], t);
            }

            float3 GridNormal(int row, int col)
            {
                float t = row / (float)(Rows - 1);
                float s = col / (float)(Cols - 1);

                // 行带（顶/底棱线）与列带（首/末列）取更强的融合量
                float rowAmt = 1f - math.saturate(math.min(t, 1f - t) / BandFraction);
                float colAmt = 1f - math.saturate(math.min(s, 1f - s) / BandFraction);
                float amount = math.max(rowAmt, colAmt) * rimBlend;

                // 交界目标：首/末列用角交界；底行（仅边界）用 jDown；其余用中段棱线
                float3 junction = jMid;
                if (col == 0) junction = j0;
                else if (col == Cols - 1) junction = j4;
                else if (boundary && row == Rows - 1) junction = jDown;

                return math.normalize(math.lerp(nSlope, junction, amount));
            }

            float4 GridWeight(int row)
            {
                float t = row / (float)(Rows - 1);
                // splat 渐变只在坡底段（贴近低格那侧）：坡占高格面积，主体应显示高格地形，
                // 只在与低格相接的底部带内过渡（HeightBlend3 会再按高度图锯齿化）。
                const float bottomBand = 0.4f; // 底部 40% 渐变，上部恒高格地形
                float tb = math.saturate((t - (1f - bottomBand)) / bottomBand);
                return new float4(math.lerp(rgbTop, rgbBot, tb), 0f); // 变异权重恒 0
            }

            for (int row = 0; row + 1 < Rows; row++)
            for (int col = 0; col + 1 < Cols; col++)
            {
                float3 p00 = GridPos(row, col);
                float3 p01 = GridPos(row, col + 1);
                float3 p10 = GridPos(row + 1, col);
                float3 p11 = GridPos(row + 1, col + 1);

                float3 n00 = GridNormal(row, col);
                float3 n01 = GridNormal(row, col + 1);
                float3 n10 = GridNormal(row + 1, col);
                float3 n11 = GridNormal(row + 1, col + 1);

                float4 w0 = GridWeight(row);
                float4 w1 = GridWeight(row + 1);

                AddQuadUnperturbed(p00, p01, p10, p11, n00, n01, n10, n11,
                    idxBot, w0, w0, w1, w1);
            }

            // ---- 角落吸收：本带若为吸收方，把角落三角形并入端部（2 地形编码）----
            // 高端角 d+1：cell 赢角则吸收；低端角 d：far（对侧格）赢角则吸收。
            // 顶/底 = 端列两端点（已扰动/名义），第三 = 剩余参与者的板角。
            EmitAbsorbedCorner(((int)d + 1) % 6, cell, lower, requireWinnerIsCell: true,
                colTop[Cols - 1], colBot[Cols - 1], BlendRim(nSlope, j4), idxBot, rgbBot,
                ref metrics, ref blob);
            EmitAbsorbedCorner((int)d, cell, lower, requireWinnerIsCell: false,
                colTop[0], colBot[0], BlendRim(nSlope, j0), idxBot, rgbBot,
                ref metrics, ref blob);
        }

        /// <summary>
        /// 角落三角形吸收：连接带把它并入端部（替代独立的角闭合三角形）。
        /// 角 k 参与者 = cell、N(k)、N(k-1)；绕序 (pA, pC, pB)（与旧闭合一致，朝外）。
        /// 高端（k=d+1）：本带两端 = (cell 角, far 角)，第三方 = N(k)，cell 赢角才吸收，
        ///   绕序 (顶, 底, 第三)。
        /// 低端（k=d）：本带两端 = (cell 角, far 角)，第三方 = N(k-1)，far 赢角才吸收，
        ///   绕序 (顶, 第三, 底)。
        /// 2 地形编码：第三顶点取本格（高侧）地形——第三格地形在其板缘形成硬边界（定案）。
        /// 任一参与者流式缺失 → 跳过（楔形路径处理）。边界伪 cell → 名义角 @bottomY 不扰动。
        /// </summary>
        private void EmitAbsorbedCorner(int cornerK, HexCellData cell, HexCellData far,
            bool requireWinnerIsCell, float3 topPos, float3 botPos, float3 nEnd,
            float3 indices, float3 rgbBot, ref HexMetrics metrics, ref HexMapConfigBlob blob)
        {
            var dirK = (HexDirection)cornerK;
            var dirPrev = (HexDirection)((cornerK + 5) % 6);

            Entity n1e = NeighborEntity(CellEntity, dirK);
            Entity n2e = NeighborEntity(CellEntity, dirPrev);
            HexCellData n1 = DataOf(n1e);
            HexCellData n2 = DataOf(n2e);
            bool n1Real = IsValidData(n1);
            bool n2Real = IsValidData(n2);

            // 流式参与者 → 楔形路径，不吸收
            var off = cell.Coordinates.ToOffsetCoordinates();
            if (!n1Real && !HexBoundary.IsOutsideMap(HexBoundary.NeighborOffset(off, dirK), blob.CellCount))
                return;
            if (!n2Real && !HexBoundary.IsOutsideMap(HexBoundary.NeighborOffset(off, dirPrev), blob.CellCount))
                return;

            if (requireWinnerIsCell)
            {
                // cell 赢角（伪 cell 的哨兵 elevation 恒输）
                if (!WinsCornerPair(in cell, in n1) || !WinsCornerPair(in cell, in n2))
                    return;
            }
            else
            {
                // far = N(k) 赢角（仅当第三方路径下 far 就是 N(k)）
                if (!WinsCornerPair(in far, in cell) || !WinsCornerPair(in far, in n2))
                    return;
            }

            // 第三方板角：N(k) → 角 (k+4)%6；N(k-1) → 角 (k+2)%6；伪 → 本格名义角 @bottomY
            float3 third;
            if (requireWinnerIsCell)
            {
                // 高端：第三方 = N(k)
                third = n1Real
                    ? Perturb(PlateCorner(n1, n1e, (cornerK + 4) % 6, ref metrics, ref blob))
                    : PseudoCorner(cell, cornerK, ref metrics);
            }
            else
            {
                // 低端：第三方 = N(k-1)
                third = n2Real
                    ? Perturb(PlateCorner(n2, n2e, (cornerK + 2) % 6, ref metrics, ref blob))
                    : PseudoCorner(cell, cornerK, ref metrics);
            }

            // 退化（双边界角：两端点与伪角共点/共线）跳过
            float3 e1 = botPos - topPos;
            float3 e2 = third - topPos;
            if (math.lengthsq(math.cross(e1, e2)) < 1e-10f)
                return;

            // 三地形交汇点保留混合（用户定案）：第三顶点带第三方地形索引/权重，
            // 交汇点处三种地形平滑过渡，带主体仍是双地形。
            int thirdIdx = requireWinnerIsCell
                ? (n1Real ? n1.TerrainIndex : cell.TerrainIndex)
                : (n2Real ? n2.TerrainIndex : cell.TerrainIndex);
            float3 triIdx = new float3(indices.x, indices.y, thirdIdx);
            float4 wTop = new float4(W100.xyz, 0f);
            float4 wBot = new float4(rgbBot, 0f);
            float4 wThird = new float4(W001.xyz, 0f);
            if (requireWinnerIsCell)
            {
                // 绕序 (顶, 底, 第三)
                AddTriangleRaw(topPos, botPos, third, nEnd, nEnd, nEnd, triIdx, wTop, wBot, wThird);
            }
            else
            {
                // 绕序 (顶, 第三, 底)
                AddTriangleRaw(topPos, third, botPos, nEnd, nEnd, nEnd, triIdx, wTop, wThird, wBot);
            }
        }

        /// <summary>边界伪 cell 在角 k 的「板角」= 本格名义角 @bottomY（不扰动，与边界坡底/Cap 同点）</summary>
        private static float3 PseudoCorner(HexCellData cell, int cornerK, ref HexMetrics metrics)
        {
            float3 p = cell.Position + metrics.Corners[cornerK];
            p.y = -metrics.ElevationStep;
            return p;
        }

        /// <summary>EdgeVertices 按索引取列顶点（0..4 → v1..v5）</summary>
        private static float3 EdgeAt(EdgeVertices e, int col)
        {
            switch (col)
            {
                case 0: return e.v1;
                case 1: return e.v2;
                case 2: return e.v3;
                case 3: return e.v4;
                default: return e.v5;
            }
        }

        /// <summary>
        /// 流式临时竖墙：本格板缘（该方向铺满不内缩，邻居加载后板缘可能移动并重建）
        /// 直落 bottomY，下边缘复制上边缘 x/z。顶行 rim 融合，底行保持水平朝向。
        /// </summary>
        private void BuildStreamingWall(HexDirection d, HexCellData cell, ref HexMetrics metrics,
            ref HexMapConfigBlob blob)
        {
            float bottomY = GetBottomY(ref metrics);
            float3 n = metrics.GetEdgeNormal(d);
            _surfaceNormal = n;
            float3 nTop = BlendRim(n, EdgeJunction(d, n));
            float3 indices = new float3(cell.TerrainIndex, cell.TerrainIndex, cell.TerrainIndex);
            float4 w = new float4(W100.xyz, 0f);

            float3 c1 = PlateCorner(cell, CellEntity, (int)d, ref metrics, ref blob);
            float3 c2 = PlateCorner(cell, CellEntity, ((int)d + 1) % 6, ref metrics, ref blob);
            var t = PerturbEdge(new EdgeVertices(c1, c2));

            AddQuadUnperturbed(t.v1, t.v2, Down(t.v1, bottomY), Down(t.v2, bottomY),
                nTop, nTop, n, n, indices, w, w, w, w);
            AddQuadUnperturbed(t.v2, t.v3, Down(t.v2, bottomY), Down(t.v3, bottomY),
                nTop, nTop, n, n, indices, w, w, w, w);
            AddQuadUnperturbed(t.v3, t.v4, Down(t.v3, bottomY), Down(t.v4, bottomY),
                nTop, nTop, n, n, indices, w, w, w, w);
            AddQuadUnperturbed(t.v4, t.v5, Down(t.v4, bottomY), Down(t.v5, bottomY),
                nTop, nTop, n, n, indices, w, w, w, w);
        }

        // ---------- 角落闭合（流式专用）----------

        /// <summary>
        /// 常规角落（含边界角）已由连接带吸收（见 EmitAbsorbedCorner），
        /// 本方法只处理流式角（任一参与者图内未加载）：竖直楔形临时封闭，
        /// 邻居加载后由吸收规则重建。归属 = 两真实参与者 Elevation 高者。
        /// </summary>
        private void BuildCornerClosure(int k, HexCellData cell, ref HexMetrics metrics,
            ref HexMapConfigBlob blob)
        {
            var dirK = (HexDirection)k;
            var dirPrev = (HexDirection)((k + 5) % 6);

            Entity n1e = NeighborEntity(CellEntity, dirK);
            Entity n2e = NeighborEntity(CellEntity, dirPrev);
            HexCellData n1 = DataOf(n1e);
            HexCellData n2 = DataOf(n2e);
            bool n1Real = IsValidData(n1);
            bool n2Real = IsValidData(n2);

            if (!n1Real && !n2Real)
                return; // 双缺：两条墙/坡的端截面在同一点重合，天然闭合

            var off = cell.Coordinates.ToOffsetCoordinates();
            bool n1Stream = !n1Real &&
                !HexBoundary.IsOutsideMap(HexBoundary.NeighborOffset(off, dirK), blob.CellCount);
            bool n2Stream = !n2Real &&
                !HexBoundary.IsOutsideMap(HexBoundary.NeighborOffset(off, dirPrev), blob.CellCount);
            if (!n1Stream && !n2Stream)
                return; // 常规角已被连接带吸收

            bool otherIsN1 = n1Real;
            HexCellData other = otherIsN1 ? n1 : n2;
            Entity otherE = otherIsN1 ? n1e : n2e;
            int otherCornerIdx = otherIsN1 ? (k + 4) % 6 : (k + 2) % 6;

            // 归属：两个真实参与者决出
            if (!WinsCornerPair(in cell, in other))
                return;

            float3 pA = Perturb(PlateCorner(cell, CellEntity, k, ref metrics, ref blob));
            float3 pO = Perturb(PlateCorner(other, otherE, otherCornerIdx, ref metrics, ref blob));
            float bottomY = GetBottomY(ref metrics);

            // 竖直楔形：两个真实板角点直落 bottomY（与两条流式墙端截面重合）。
            // 绕序沿缺 cell 轮廓链方向（与两条墙的 v1→v5 走向头尾相接，面朝缺 cell）。
            float3 snK = StripNormal(dirK, cell, ref metrics, ref blob);
            float3 snPrev = StripNormal(dirPrev, cell, ref metrics, ref blob);
            float3 nW = math.normalize(snK + snPrev);
            _surfaceNormal = nW;
            float3 nTop = BlendRim(nW, CornerJunction(k, snPrev, snK));

            float3 indices = new float3(cell.TerrainIndex, other.TerrainIndex, other.TerrainIndex);
            float4 wA = new float4(W100.xyz, 0f);
            float4 wO = new float4(W010.xyz, 0f);
            if (otherIsN1)
            {
                // n2 缺：p2 槽侧是缺 cell
                AddQuadUnperturbed(pA, pO, Down(pA, bottomY), Down(pO, bottomY),
                    nTop, nTop, nW, nW, indices, wA, wO, wA, wO);
            }
            else
            {
                // n1 缺：p1 槽侧是缺 cell
                AddQuadUnperturbed(pO, pA, Down(pO, bottomY), Down(pA, bottomY),
                    nTop, nTop, nW, nW, indices, wO, wA, wO, wA);
            }
        }

        /// <summary>
        /// 角落归属比较：Elevation 高者胜；平手 offset 字典序 (x,z) 小者胜。
        /// 全序无平局（offset 唯一），三方各自调用得出同一归属。
        /// </summary>
        private static bool WinsCornerPair(in HexCellData a, in HexCellData b)
        {
            if (a.Elevation != b.Elevation)
                return a.Elevation > b.Elevation;
            var oa = a.Coordinates.ToOffsetCoordinates();
            var ob = b.Coordinates.ToOffsetCoordinates();
            return oa.x != ob.x ? oa.x < ob.x : oa.y < ob.y;
        }

        /// <summary>把（已扰动的）顶点直落到指定高度（x/z 不变）</summary>
        private static float3 Down(float3 p, float y) => new float3(p.x, y, p.z);
    }
}
