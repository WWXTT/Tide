using Unity.Entities;
using Unity.Mathematics;

namespace HexMap
{
    /// <summary>
    /// HexMeshJob Part 2：连接区域三角化（TriangulateConnection + 角落处理）。
    /// 梯田已退役：任何高度差都生成单条直壁连接带，角落统一为单三角形——
    /// 侧面有真实法线后阶梯细分只剩面数开销，贴图/光照连续性由 shader 的
    /// 三平面投影保证（贴图自循环直铺）。
    /// </summary>
    public partial struct HexMeshJob
    {
        /// <summary>
        /// 为当前 cell 的一个方向生成与相邻 cell 的连接区域（直壁）+ 三角形角落
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

            TriangulateEdgeStrip(e1, e2, cell.TerrainIndex, neighbor.TerrainIndex);

            // 三角形角落（仅 NE/E，避免重复）
            var nextNeighbor = GetNeighbor(cell, direction.Next());
            if ((int)direction <= (int)HexDirection.E && nextNeighbor.Elevation != int.MinValue)
            {
                // 验证下一个邻居位置是否有效
                if (!math.any(math.isnan(nextNeighbor.Position)) && !math.any(math.isinf(nextNeighbor.Position)))
                {
                    float3 v5 = e1.v5 + metrics.GetBridge(direction.Next());
                    v5.y = nextNeighbor.Position.y;

                    TriangulateCorner(e1.v5, cell, e2.v5, neighbor, v5, nextNeighbor);
                }
            }
        }

        /// <summary>
        /// 连接区域：细分矩形（4 个四边形，splat 编码）。两侧权重固定 W100/W010
        /// （本 cell 地形 → 邻居地形，跨连接带线性过渡交给顶点色插值）。
        /// </summary>
        private void TriangulateEdgeStrip(EdgeVertices e1, EdgeVertices e2, float idxA, float idxB)
        {
            float3 indices = new float3(idxA, idxB, idxB);

            AddQuad(e1.v1, e1.v2, e2.v1, e2.v2);
            AddQuadCellData(indices, W100, W010);

            AddQuad(e1.v2, e1.v3, e2.v2, e2.v3);
            AddQuadCellData(indices, W100, W010);

            AddQuad(e1.v3, e1.v4, e2.v3, e2.v4);
            AddQuadCellData(indices, W100, W010);

            AddQuad(e1.v4, e1.v5, e2.v4, e2.v5);
            AddQuadCellData(indices, W100, W010);
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
        /// （完整角与本 cell 同高度、同地形），直接复用 TriangulateCorner。
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

                TriangulateCorner(cS, cell, cF, cell, cS + bridge, next);
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

                TriangulateCorner(cS, cell, cS + bridge, prev, cF, cell);
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
        /// 三角形角落。梯田退役后所有高度组合（Flat/Slope/Cliff）统一为单三角形：
        /// 三条边分别与三条连接带的端边重合（同位置顶点，水密），表面是连接
        /// 三个 cell 顶面的直壁斜面，贴图连续性交给 shader 的三平面投影
        /// （世界坐标投影，无恒定 U 拉伸）。
        /// </summary>
        private void TriangulateCorner(float3 bottom, HexCellData bottomCell, float3 left, HexCellData leftCell,
            float3 right, HexCellData rightCell)
        {
            AddTriangle(bottom, left, right);
            float3 indices = new float3(bottomCell.TerrainIndex, leftCell.TerrainIndex, rightCell.TerrainIndex);
            AddTriangleCellData(indices, W100, W010, W001);
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
