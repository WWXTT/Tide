using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HexMap
{
    /// <summary>
    /// HexMeshJob Part 2：连接区域三角化（TriangulateConnection + 阶梯/角落处理）
    /// </summary>
    public partial struct HexMeshJob
    {
        /// <summary>
        /// 为当前 cell 的一个方向生成与相邻 cell 的连接区域（矩形/阶梯）+ 三角形角落
        /// </summary>
        private void TriangulateConnection(HexDirection direction, HexCellData cell, EdgeVertices e1,
            ref HexMetrics metrics, ref HexMapConfigBlob blob)
        {
            var neighbor = GetNeighbor(cell, direction);
            if (neighbor.Elevation == int.MinValue) // Entity.Null 或不存在
                return;

            // 验证邻居位置是否有效
            if (math.any(math.isnan(neighbor.Position)) || math.any(math.isinf(neighbor.Position)))
                return;

            float3 bridge = metrics.GetBridge(direction);
            bridge.y = neighbor.Position.y - cell.Position.y;

            var e2 = new EdgeVertices(e1.v1 + bridge, e1.v5 + bridge);

            var edgeType = HexMetrics.GetEdgeType(cell.Elevation, neighbor.Elevation);

            // UV 坡面补偿：连接带的顶视投影沿落差拉伸，把「低于坡顶的高度 × k·下坡方向」
            // 烘成逐顶点向量（StripCorr）。片元只插值——相邻三角形共享顶点取同一值
            float run = 2f * metrics.InnerRadius * metrics.BlendFactor;
            float2 corr = StripCorr(cell.Position, neighbor.Position, run);
            float topY = math.max(cell.Position.y, neighbor.Position.y);

            if (edgeType == HexEdgeType.Slope)
            {
                TriangulateEdgeTerraces(e1, cell, e2, neighbor, corr, topY, ref metrics);
            }
            else
            {
                TriangulateEdgeStrip(e1, 0f, e2, 1f, cell.TerrainIndex, neighbor.TerrainIndex, corr, topY);
            }

            // 三角形角落（仅 NE/E，避免重复）
            var nextNeighbor = GetNeighbor(cell, direction.Next());
            if ((int)direction <= (int)HexDirection.E && nextNeighbor.Elevation != int.MinValue)
            {
                // 验证下一个邻居位置是否有效
                if (!math.any(math.isnan(nextNeighbor.Position)) && !math.any(math.isinf(nextNeighbor.Position)))
                {
                    float3 v5 = e1.v5 + metrics.GetBridge(direction.Next());
                    v5.y = nextNeighbor.Position.y;

                    TriangulateCorner(e1.v5, cell, e2.v5, neighbor, v5, nextNeighbor, ref metrics);
                }
            }
        }

        /// <summary>
        /// 阶梯化矩形连接区域
        /// </summary>
        private void TriangulateEdgeTerraces(EdgeVertices begin, HexCellData beginCell, EdgeVertices end,
            HexCellData endCell, float2 corr, float topY, ref HexMetrics metrics)
        {
            float beginIdx = beginCell.TerrainIndex;
            float endIdx = endCell.TerrainIndex;

            // 第一段
            var e2 = EdgeVertices.TerraceLerp(in begin, in end, 1, in metrics);
            float b2 = 1f * metrics.HorizontalTerraceStepSize;
            TriangulateEdgeStrip(begin, 0f, e2, b2, beginIdx, endIdx, corr, topY);

            // 中间段
            for (int i = 2; i < metrics.TerraceSteps; i++)
            {
                var e1 = e2;
                float b1 = b2;
                e2 = EdgeVertices.TerraceLerp(in begin, in end, i, in metrics);
                b2 = i * metrics.HorizontalTerraceStepSize;
                TriangulateEdgeStrip(e1, b1, e2, b2, beginIdx, endIdx, corr, topY);
            }

            // 最后一段
            TriangulateEdgeStrip(e2, b2, end, 1f, beginIdx, endIdx, corr, topY);
        }

        /// <summary>
        /// 细分矩形（4 个四边形，splat 编码）。
        /// corr/topY：本连接带的 UV 补偿向量与坡顶高度，逐顶点按 (topY − y)·corr 烘焙——
        /// 每侧边缘内 y 恒定故为常量；阶梯细分时每条步进边自动取各自高度，共享边逐点一致
        /// </summary>
        private void TriangulateEdgeStrip(EdgeVertices e1, float b1, EdgeVertices e2, float b2, float idxA, float idxB,
            float2 corr = default, float topY = 0f)
        {
            float3 indices = new float3(idxA, idxB, idxB);
            float4 w1 = WeightAB(b1);
            float4 w2 = WeightAB(b2);

            float2 c1 = corr * math.max(0f, topY - e1.v1.y);
            float2 c2 = corr * math.max(0f, topY - e2.v1.y);

            AddQuad(e1.v1, e1.v2, e2.v1, e2.v2);
            AddQuadCellData(indices, w1, w2);
            PatchLast4(c1, c1, c2, c2);

            AddQuad(e1.v2, e1.v3, e2.v2, e2.v3);
            AddQuadCellData(indices, w1, w2);
            PatchLast4(c1, c1, c2, c2);

            AddQuad(e1.v3, e1.v4, e2.v3, e2.v4);
            AddQuadCellData(indices, w1, w2);
            PatchLast4(c1, c1, c2, c2);

            AddQuad(e1.v4, e1.v5, e2.v4, e2.v5);
            AddQuadCellData(indices, w1, w2);
            PatchLast4(c1, c1, c2, c2);
        }

        /// <summary>
        /// 连接带两侧 → UV 补偿向量 = tan(θ/2)·下坡单位向量（世界单位/单位落差）。
        /// 沿坡面移动 L：水平推进 L·cosθ + 补偿推进 L·sinθ·tan(θ/2) = L，密度恰好 1:1。
        /// 纯函数：同一对 cell 在连接带/角部的任何发射点算出同一结果
        /// </summary>
        private static float2 StripCorr(float3 posA, float3 posB, float run)
        {
            float dy = math.abs(posA.y - posB.y);
            if (dy < 1e-5f)
                return float2.zero;

            float2 dir = math.normalizesafe(posB.xz - posA.xz, float2.zero);
            if (posB.y > posA.y)
                dir = -dir; // 下坡方向：从高处指向低处

            float hyp = math.sqrt(dy * dy + run * run);
            float sin = dy / hyp;
            float cos = run / hyp;
            return dir * (sin / (1f + cos)); // tan(θ/2)
        }

        /// <summary>
        /// 角部 UV 补偿上下文：begin 顶点同时邻两条连接带（各有各的补偿），
        /// 取模长较大的一侧——另一侧仅在「begin 最低且两侧落差不同」时留一条细缝
        /// </summary>
        private struct CornerUv
        {
            public float2 CorrBL, CorrBR; // bottom-left / bottom-right 连接带的补偿向量
            public float TopBL, TopBR;    // 两条连接带的坡顶高度
            public float2 cB, cL, cR;     // begin/left/right 三个外顶点的补偿值

            /// <summary>[begin→left] 段（bottom-left 连接带端列）上某高度处的补偿</summary>
            public float2 LeftAt(float y) => CorrBL * math.max(0f, TopBL - y);

            /// <summary>[begin→right] 段（bottom-right 连接带端列）上某高度处的补偿</summary>
            public float2 RightAt(float y) => CorrBR * math.max(0f, TopBR - y);
        }

        /// <summary>角部三 cell → CornerUv</summary>
        private static CornerUv MakeCornerUv(HexCellData bottomCell, HexCellData leftCell, HexCellData rightCell,
            float run)
        {
            var uv = new CornerUv();
            uv.CorrBL = StripCorr(bottomCell.Position, leftCell.Position, run);
            uv.CorrBR = StripCorr(bottomCell.Position, rightCell.Position, run);
            uv.TopBL = math.max(bottomCell.Position.y, leftCell.Position.y);
            uv.TopBR = math.max(bottomCell.Position.y, rightCell.Position.y);

            float2 vBL = uv.LeftAt(bottomCell.Position.y);
            float2 vBR = uv.RightAt(bottomCell.Position.y);
            uv.cB = math.lengthsq(vBL) >= math.lengthsq(vBR) ? vBL : vBR;
            uv.cL = uv.LeftAt(leftCell.Position.y);
            uv.cR = uv.RightAt(rightCell.Position.y);
            return uv;
        }

        /// <summary>
        /// 地图边界连接区：从边界 cell 顶面的完整边缘（已扰动，与扇形共用同一批顶点）
        /// 倾斜下探，落点直接落在统一底面的边缘上。
        ///
        /// 顶边与落点都是「位置的纯函数」：相邻边界 cell 在共享角点上算出同一结果，
        /// 两条连接区的侧边逐点重合，裙边不开缝；底面与落点共用同一矩形，底边严格闭合。
        /// 外圈 cell 恒为 elevation 0（生成固定 + 禁止编辑），共享角点两侧等高。
        /// 角点本身已落在矩形边界上时（如地图正东西边缘）连接区退化为竖直面。
        /// </summary>
        private void TriangulateBoundaryConnection(EdgeVertices e, HexCellData cell, ref HexMetrics metrics,
            ref HexMapConfigBlob blob)
        {
            float bottomY = GetBottomY(ref metrics);
            float4 rect = HexBoundary.GetMapRect(metrics.OuterRadius, metrics.InnerRadius, blob.CellCount);
            float3 indices = new float3(cell.TerrainIndex, cell.TerrainIndex, cell.TerrainIndex);

            var t = PerturbEdge(e);

            // 5 个顶点逐个投射（而非在两端落点间插值），保证所有落点都在矩形边界上
            var l = new EdgeVertices(ProjectLanding(t.v1, rect, bottomY), ProjectLanding(t.v5, rect, bottomY));
            l.v2 = ProjectLanding(t.v2, rect, bottomY);
            l.v3 = ProjectLanding(t.v3, rect, bottomY);
            l.v4 = ProjectLanding(t.v4, rect, bottomY);

            AddQuadUnperturbed(t.v1, t.v2, l.v1, l.v2);
            AddQuadCellData(indices, W100, W100);

            AddQuadUnperturbed(t.v2, t.v3, l.v2, l.v3);
            AddQuadCellData(indices, W100, W100);

            AddQuadUnperturbed(t.v3, t.v4, l.v3, l.v4);
            AddQuadCellData(indices, W100, W100);

            AddQuadUnperturbed(t.v4, t.v5, l.v4, l.v5);
            AddQuadCellData(indices, W100, W100);
        }

        /// <summary>
        /// 流式加载边缘的临时竖直侧面：邻居尚未加载（之后会加载）时封闭体积。
        /// 上边缘与扇形共用同一批已扰动顶点，下边缘复制上边缘 x/z 对齐统一底面。
        /// 邻居加载后该方向重建为正常连接区域。
        /// </summary>
        private void TriangulateBoundarySide(EdgeVertices e, HexCellData cell, ref HexMetrics metrics)
        {
            float bottomY = GetBottomY(ref metrics);
            float3 indices = new float3(cell.TerrainIndex, cell.TerrainIndex, cell.TerrainIndex);

            var t = PerturbEdge(e);

            AddQuadUnperturbed(t.v1, t.v2, Down(t.v1, bottomY), Down(t.v2, bottomY));
            AddQuadCellData(indices, W100, W100);

            AddQuadUnperturbed(t.v2, t.v3, Down(t.v2, bottomY), Down(t.v3, bottomY));
            AddQuadCellData(indices, W100, W100);

            AddQuadUnperturbed(t.v3, t.v4, Down(t.v3, bottomY), Down(t.v4, bottomY));
            AddQuadCellData(indices, W100, W100);

            AddQuadUnperturbed(t.v4, t.v5, Down(t.v4, bottomY), Down(t.v5, bottomY));
            AddQuadCellData(indices, W100, W100);
        }

        /// <summary>把一条边的 5 个顶点整体扰动一次（扇形/边界几何共用，避免二次扰动）</summary>
        private EdgeVertices PerturbEdge(EdgeVertices e)
        {
            var t = new EdgeVertices(Perturb(e.v1), Perturb(e.v5));
            t.v2 = Perturb(e.v2);
            t.v3 = Perturb(e.v3);
            t.v4 = Perturb(e.v4);
            return t;
        }

        /// <summary>
        /// 混合角补面：本方向朝外（扇形铺到完整角 cF），相邻方向朝内（桥接从 solid 角 cS 起），
        /// cF 与两侧 solid 角之间的三角形区域没有任何三角形覆盖——这就是「同一个 cell
        /// 同时连接内部 cell 和底面」时顶面开裂的根源。
        ///
        /// 补面与四周几何逐边严格重合（三条边都是现成的共享边）：
        /// [cS→cF] 在本 cell 扇形的半径边上；[cS→cN] 是相邻方向桥接的角侧边；
        /// [cN→cF] 在邻居扇形的半径边上。朝外一侧用本 cell 自身充当虚拟第三方
        /// （完整角与本 cell 同高度、同地形），直接复用 TriangulateCorner：
        /// 邻居更高时自动生成与桥接逐点一致的阶梯。
        /// </summary>
        private void TriangulateMixedCorner(HexDirection direction, HexCellData cell, ref HexMetrics metrics)
        {
            float3 center = cell.Position;
            float3 fanIndices = new float3(cell.TerrainIndex, cell.TerrainIndex, cell.TerrainIndex);

            // 角 corners[(int)direction + 1]：与 direction.Next() 共享
            var next = GetNeighbor(cell, direction.Next());
            if (IsValidCell(next))
            {
                float3 cS = center + metrics.GetFirstSolidCorner(direction.Next());
                float3 cF = center + metrics.GetFirstCorner(direction.Next());

                // 发丝缝：本方向扇形铺到 cF、相邻方向扇形只到 cS，扰动后两条半径边
                // 不再共线，补一个（未扰动时退化的）扇形三角形填平
                AddTriangle(center, cS, cF);
                AddTriangleCellData(fanIndices, W100);

                float3 bridge = metrics.GetBridge(direction.Next());
                bridge.y = next.Position.y - cell.Position.y;

                TriangulateCorner(cS, cell, cF, cell, cS + bridge, next, ref metrics);
            }

            // 角 corners[(int)direction]：与 direction.Previous() 共享
            var prev = GetNeighbor(cell, direction.Previous());
            if (IsValidCell(prev))
            {
                float3 cS = center + metrics.GetSecondSolidCorner(direction.Previous());
                float3 cF = center + metrics.GetSecondCorner(direction.Previous());

                AddTriangle(center, cF, cS);
                AddTriangleCellData(fanIndices, W100);

                float3 bridge = metrics.GetBridge(direction.Previous());
                bridge.y = prev.Position.y - cell.Position.y;

                TriangulateCorner(cS, cell, cS + bridge, prev, cF, cell, ref metrics);
            }
        }

        /// <summary>邻居数据是否可用于三角化（存在且位置有效）</summary>
        private static bool IsValidCell(in HexCellData cell)
        {
            return cell.Elevation != int.MinValue &&
                   !math.any(math.isnan(cell.Position)) &&
                   !math.any(math.isinf(cell.Position));
        }

        /// <summary>把（已扰动的）顶点投射到底面矩形边界上，y 压到底面高度</summary>
        private static float3 ProjectLanding(float3 p, float4 rect, float bottomY)
        {
            float3 landing = HexBoundary.ProjectToRectEdge(p, rect);
            landing.y = bottomY;
            return landing;
        }

        private static float3 Down(float3 p, float y) => new float3(p.x, y, p.z);

        /// <summary>
        /// 三角形角落分类处理（SSF/Slope-Cliff/纯三角形）。
        /// 角部顶点的 UV 补偿与相邻连接带端列用同一公式（CornerUv），共享边逐点一致。
        /// </summary>
        private void TriangulateCorner(float3 bottom, HexCellData bottomCell, float3 left, HexCellData leftCell,
            float3 right, HexCellData rightCell, ref HexMetrics metrics)
        {
            var leftEdgeType = HexMetrics.GetEdgeType(bottomCell.Elevation, leftCell.Elevation);
            var rightEdgeType = HexMetrics.GetEdgeType(bottomCell.Elevation, rightCell.Elevation);

            var uv = MakeCornerUv(bottomCell, leftCell, rightCell, 2f * metrics.InnerRadius * metrics.BlendFactor);

            if (leftEdgeType == HexEdgeType.Slope)
            {
                if (rightEdgeType == HexEdgeType.Slope)
                {
                    TriangulateCornerTerraces(bottom, bottomCell, left, leftCell, right, rightCell, ref metrics);
                }
                else if (rightEdgeType == HexEdgeType.Flat)
                {
                    TriangulateCornerTerraces(left, leftCell, right, rightCell, bottom, bottomCell, ref metrics);
                }
                else
                {
                    TriangulateCornerTerracesCliff(bottom, bottomCell, left, leftCell, right, rightCell, ref metrics);
                }
            }
            else if (rightEdgeType == HexEdgeType.Slope)
            {
                if (leftEdgeType == HexEdgeType.Flat)
                {
                    TriangulateCornerTerraces(right, rightCell, bottom, bottomCell, left, leftCell, ref metrics);
                }
                else
                {
                    TriangulateCornerCliffTerraces(bottom, bottomCell, left, leftCell, right, rightCell, ref metrics);
                }
            }
            else if (HexMetrics.GetEdgeType(leftCell.Elevation, rightCell.Elevation) == HexEdgeType.Slope)
            {
                if (leftCell.Elevation < rightCell.Elevation)
                {
                    TriangulateCornerCliffTerraces(right, rightCell, bottom, bottomCell, left, leftCell, ref metrics);
                }
                else
                {
                    TriangulateCornerTerracesCliff(left, leftCell, right, rightCell, bottom, bottomCell, ref metrics);
                }
            }
            else
            {
                // 纯三角形（无阶梯）
                AddTriangle(bottom, left, right);
                float3 indices = new float3(bottomCell.TerrainIndex, leftCell.TerrainIndex, rightCell.TerrainIndex);
                AddTriangleCellData(indices, W100, W010, W001);
                PatchLast3(uv.cB, uv.cL, uv.cR);
            }
        }

        /// <summary>
        /// SSF 类型角落（两个 Slope，一个 Flat）
        /// </summary>
        private void TriangulateCornerTerraces(float3 begin, HexCellData beginCell, float3 left, HexCellData leftCell,
            float3 right, HexCellData rightCell, ref HexMetrics metrics)
        {
            float3 indices = new float3(beginCell.TerrainIndex, leftCell.TerrainIndex, rightCell.TerrainIndex);
            var uv = MakeCornerUv(beginCell, leftCell, rightCell, 2f * metrics.InnerRadius * metrics.BlendFactor);

            float3 v3 = metrics.TerraceLerp(begin, left, 1);
            float3 v4 = metrics.TerraceLerp(begin, right, 1);
            float h = 1f * metrics.HorizontalTerraceStepSize;
            float4 w3 = math.lerp(W100, W010, h);
            float4 w4 = math.lerp(W100, W001, h);

            AddTriangle(begin, v3, v4);
            AddTriangleCellData(indices, W100, w3, w4);
            PatchLast3(uv.cB, uv.LeftAt(v3.y), uv.RightAt(v4.y));

            for (int i = 2; i < metrics.TerraceSteps; i++)
            {
                float3 v1 = v3;
                float3 v2 = v4;
                float4 w1 = w3;
                float4 w2 = w4;
                v3 = metrics.TerraceLerp(begin, left, i);
                v4 = metrics.TerraceLerp(begin, right, i);
                h = i * metrics.HorizontalTerraceStepSize;
                w3 = math.lerp(W100, W010, h);
                w4 = math.lerp(W100, W001, h);
                AddQuad(v1, v2, v3, v4);
                AddQuadCellData(indices, w1, w2, w3, w4);
                PatchLast4(uv.LeftAt(v1.y), uv.RightAt(v2.y), uv.LeftAt(v3.y), uv.RightAt(v4.y));
            }

            AddQuad(v3, v4, left, right);
            AddQuadCellData(indices, w3, w4, W010, W001);
            PatchLast4(uv.LeftAt(v3.y), uv.RightAt(v4.y), uv.cL, uv.cR);
        }

        /// <summary>
        /// Slope-Cliff 类型角落
        /// </summary>
        private void TriangulateCornerTerracesCliff(float3 begin, HexCellData beginCell, float3 left,
            HexCellData leftCell, float3 right, HexCellData rightCell, ref HexMetrics metrics)
        {
            float3 indices = new float3(beginCell.TerrainIndex, leftCell.TerrainIndex, rightCell.TerrainIndex);
            var uv = MakeCornerUv(beginCell, leftCell, rightCell, 2f * metrics.InnerRadius * metrics.BlendFactor);

            float b = 1f / (rightCell.Elevation - beginCell.Elevation);
            if (b < 0) b = -b;

            float3 boundary = math.lerp(Perturb(begin), Perturb(right), b);
            float4 boundaryWeights = math.lerp(W100, W001, b);
            float2 cBnd = uv.RightAt(boundary.y); // boundary 位于 [begin→right] 段

            TriangulateBoundaryTriangle(begin, W100, left, W010, boundary, boundaryWeights, indices, ref metrics,
                uv.cB, uv.cL, cBnd, uv.CorrBL, uv.TopBL);

            if (HexMetrics.GetEdgeType(leftCell.Elevation, rightCell.Elevation) == HexEdgeType.Slope)
            {
                TriangulateBoundaryTriangle(left, W010, right, W001, boundary, boundaryWeights, indices, ref metrics,
                    uv.cL, uv.cR, cBnd, float2.zero, 0f);
            }
            else
            {
                AddTriangleUnperturbed(Perturb(left), Perturb(right), boundary);
                AddTriangleCellData(indices, W010, W001, boundaryWeights);
                PatchLast3(uv.cL, uv.cR, cBnd);
            }
        }

        /// <summary>
        /// Cliff-Slope 类型角落（镜像）
        /// </summary>
        private void TriangulateCornerCliffTerraces(float3 begin, HexCellData beginCell, float3 left,
            HexCellData leftCell, float3 right, HexCellData rightCell, ref HexMetrics metrics)
        {
            float3 indices = new float3(beginCell.TerrainIndex, leftCell.TerrainIndex, rightCell.TerrainIndex);
            var uv = MakeCornerUv(beginCell, leftCell, rightCell, 2f * metrics.InnerRadius * metrics.BlendFactor);

            float b = 1f / (leftCell.Elevation - beginCell.Elevation);
            if (b < 0) b = -b;

            float3 boundary = math.lerp(Perturb(begin), Perturb(left), b);
            float4 boundaryWeights = math.lerp(W100, W010, b);
            float2 cBnd = uv.LeftAt(boundary.y); // boundary 位于 [begin→left] 段

            TriangulateBoundaryTriangle(right, W001, begin, W100, boundary, boundaryWeights, indices, ref metrics,
                uv.cR, uv.cB, cBnd, uv.CorrBR, uv.TopBR);

            if (HexMetrics.GetEdgeType(leftCell.Elevation, rightCell.Elevation) == HexEdgeType.Slope)
            {
                TriangulateBoundaryTriangle(left, W010, right, W001, boundary, boundaryWeights, indices, ref metrics,
                    uv.cL, uv.cR, cBnd, float2.zero, 0f);
            }
            else
            {
                AddTriangleUnperturbed(Perturb(left), Perturb(right), boundary);
                AddTriangleCellData(indices, W010, W001, boundaryWeights);
                PatchLast3(uv.cL, uv.cR, cBnd);
            }
        }

        /// <summary>
        /// Slope-Cliff 边界阶梯三角化。
        /// UV 补偿：begin/left 端点用调用方传入的精确值，阶梯中间点按 (topSide − y)·corrSide
        /// 与相邻连接带的步进端列逐点一致（同公式）
        /// </summary>
        private void TriangulateBoundaryTriangle(float3 begin, float4 beginWeights, float3 left, float4 leftWeights,
            float3 boundary, float4 boundaryWeights, float3 indices, ref HexMetrics metrics,
            float2 cBegin, float2 cLeft, float2 cBoundary, float2 corrSide, float topSide)
        {
            float3 v2 = Perturb(metrics.TerraceLerp(begin, left, 1));
            float h = 1f * metrics.HorizontalTerraceStepSize;
            float4 w2 = math.lerp(beginWeights, leftWeights, h);

            AddTriangleUnperturbed(Perturb(begin), v2, boundary);
            AddTriangleCellData(indices, beginWeights, w2, boundaryWeights);
            PatchLast3(cBegin, corrSide * math.max(0f, topSide - v2.y), cBoundary);

            for (int i = 2; i < metrics.TerraceSteps; i++)
            {
                float3 v1 = v2;
                float4 w1 = w2;
                v2 = Perturb(metrics.TerraceLerp(begin, left, i));
                h = i * metrics.HorizontalTerraceStepSize;
                w2 = math.lerp(beginWeights, leftWeights, h);
                AddTriangleUnperturbed(v1, v2, boundary);
                AddTriangleCellData(indices, w1, w2, boundaryWeights);
                PatchLast3(corrSide * math.max(0f, topSide - v1.y),
                    corrSide * math.max(0f, topSide - v2.y), cBoundary);
            }

            AddTriangleUnperturbed(v2, Perturb(left), boundary);
            AddTriangleCellData(indices, w2, leftWeights, boundaryWeights);
            PatchLast3(corrSide * math.max(0f, topSide - v2.y), cLeft, cBoundary);
        }

        /// <summary>
        /// 获取邻居 cell 数据。不存在返回 Elevation = int.MinValue 的哨兵值
        /// </summary>
        private HexCellData GetNeighbor(HexCellData cell, HexDirection direction)
        {
            // 通过当前 cellEntity 的 Neighbors buffer 查找邻居
            if (!AllNeighbors.HasBuffer(CellEntity))
                return new HexCellData { Elevation = int.MinValue };

            var neighbors = AllNeighbors[CellEntity];
            var neighborEntity = neighbors[(int)direction].Value;

            if (neighborEntity == default || !AllCellData.HasComponent(neighborEntity))
                return new HexCellData { Elevation = int.MinValue };

            return AllCellData[neighborEntity];
        }
    }
}
