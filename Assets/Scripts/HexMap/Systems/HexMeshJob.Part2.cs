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

            if (edgeType == HexEdgeType.Slope)
            {
                TriangulateEdgeTerraces(e1, cell, e2, neighbor, ref metrics);
            }
            else
            {
                TriangulateEdgeStrip(e1, 0f, e2, 1f, cell.TerrainIndex, neighbor.TerrainIndex);
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
            HexCellData endCell, ref HexMetrics metrics)
        {
            float beginIdx = beginCell.TerrainIndex;
            float endIdx = endCell.TerrainIndex;

            // 第一段
            var e2 = EdgeVertices.TerraceLerp(in begin, in end, 1, in metrics);
            float b2 = 1f * metrics.HorizontalTerraceStepSize;
            TriangulateEdgeStrip(begin, 0f, e2, b2, beginIdx, endIdx);

            // 中间段
            for (int i = 2; i < metrics.TerraceSteps; i++)
            {
                var e1 = e2;
                float b1 = b2;
                e2 = EdgeVertices.TerraceLerp(in begin, in end, i, in metrics);
                b2 = i * metrics.HorizontalTerraceStepSize;
                TriangulateEdgeStrip(e1, b1, e2, b2, beginIdx, endIdx);
            }

            // 最后一段
            TriangulateEdgeStrip(e2, b2, end, 1f, beginIdx, endIdx);
        }

        /// <summary>
        /// 细分矩形（4 个四边形，splat 编码）
        /// </summary>
        private void TriangulateEdgeStrip(EdgeVertices e1, float b1, EdgeVertices e2, float b2, float idxA, float idxB)
        {
            float3 indices = new float3(idxA, idxB, idxB);
            float4 w1 = WeightAB(b1);
            float4 w2 = WeightAB(b2);

            AddQuad(e1.v1, e1.v2, e2.v1, e2.v2);
            AddQuadCellData(indices, w1, w2);

            AddQuad(e1.v2, e1.v3, e2.v2, e2.v3);
            AddQuadCellData(indices, w1, w2);

            AddQuad(e1.v3, e1.v4, e2.v3, e2.v4);
            AddQuadCellData(indices, w1, w2);

            AddQuad(e1.v4, e1.v5, e2.v4, e2.v5);
            AddQuadCellData(indices, w1, w2);
        }

        /// <summary>
        /// 边界侧面：竖直四边形条带，上边缘是裁剪后的顶面边，下边缘对齐统一底面 (bottomY = -ElevationStep)。
        /// 外圈 cell 恒为 elevation 0 且不做高度扰动，所以顶边 y = 0，底边 y = -ElevationStep，侧面竖直。
        /// </summary>
        private void TriangulateBoundarySide(EdgeVertices e, HexCellData cell, ref HexMetrics metrics)
        {
            float bottomY = GetBottomY(ref metrics);
            float3 indices = new float3(cell.TerrainIndex, cell.TerrainIndex, cell.TerrainIndex);

            // 底边逐顶点复制顶边的 x/z（而非重新插值），保证上下边缘严格同一竖直平面
            AddQuadUnperturbed(e.v1, e.v2, Down(e.v1, bottomY), Down(e.v2, bottomY));
            AddQuadCellData(indices, W100, W100);

            AddQuadUnperturbed(e.v2, e.v3, Down(e.v2, bottomY), Down(e.v3, bottomY));
            AddQuadCellData(indices, W100, W100);

            AddQuadUnperturbed(e.v3, e.v4, Down(e.v3, bottomY), Down(e.v4, bottomY));
            AddQuadCellData(indices, W100, W100);

            AddQuadUnperturbed(e.v4, e.v5, Down(e.v4, bottomY), Down(e.v5, bottomY));
            AddQuadCellData(indices, W100, W100);
        }

        private static float3 Down(float3 p, float y) => new float3(p.x, y, p.z);

        /// <summary>
        /// 三角形角落分类处理（SSF/Slope-Cliff/纯三角形）
        /// </summary>
        private void TriangulateCorner(float3 bottom, HexCellData bottomCell, float3 left, HexCellData leftCell,
            float3 right, HexCellData rightCell, ref HexMetrics metrics)
        {
            var leftEdgeType = HexMetrics.GetEdgeType(bottomCell.Elevation, leftCell.Elevation);
            var rightEdgeType = HexMetrics.GetEdgeType(bottomCell.Elevation, rightCell.Elevation);

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
            }
        }

        /// <summary>
        /// SSF 类型角落（两个 Slope，一个 Flat）
        /// </summary>
        private void TriangulateCornerTerraces(float3 begin, HexCellData beginCell, float3 left, HexCellData leftCell,
            float3 right, HexCellData rightCell, ref HexMetrics metrics)
        {
            float3 indices = new float3(beginCell.TerrainIndex, leftCell.TerrainIndex, rightCell.TerrainIndex);

            float3 v3 = metrics.TerraceLerp(begin, left, 1);
            float3 v4 = metrics.TerraceLerp(begin, right, 1);
            float h = 1f * metrics.HorizontalTerraceStepSize;
            float4 w3 = math.lerp(W100, W010, h);
            float4 w4 = math.lerp(W100, W001, h);

            AddTriangle(begin, v3, v4);
            AddTriangleCellData(indices, W100, w3, w4);

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
            }

            AddQuad(v3, v4, left, right);
            AddQuadCellData(indices, w3, w4, W010, W001);
        }

        /// <summary>
        /// Slope-Cliff 类型角落
        /// </summary>
        private void TriangulateCornerTerracesCliff(float3 begin, HexCellData beginCell, float3 left,
            HexCellData leftCell, float3 right, HexCellData rightCell, ref HexMetrics metrics)
        {
            float3 indices = new float3(beginCell.TerrainIndex, leftCell.TerrainIndex, rightCell.TerrainIndex);

            float b = 1f / (rightCell.Elevation - beginCell.Elevation);
            if (b < 0) b = -b;

            float3 boundary = math.lerp(Perturb(begin), Perturb(right), b);
            float4 boundaryWeights = math.lerp(W100, W001, b);

            TriangulateBoundaryTriangle(begin, W100, left, W010, boundary, boundaryWeights, indices, ref metrics);

            if (HexMetrics.GetEdgeType(leftCell.Elevation, rightCell.Elevation) == HexEdgeType.Slope)
            {
                TriangulateBoundaryTriangle(left, W010, right, W001, boundary, boundaryWeights, indices, ref metrics);
            }
            else
            {
                AddTriangleUnperturbed(Perturb(left), Perturb(right), boundary);
                AddTriangleCellData(indices, W010, W001, boundaryWeights);
            }
        }

        /// <summary>
        /// Cliff-Slope 类型角落（镜像）
        /// </summary>
        private void TriangulateCornerCliffTerraces(float3 begin, HexCellData beginCell, float3 left,
            HexCellData leftCell, float3 right, HexCellData rightCell, ref HexMetrics metrics)
        {
            float3 indices = new float3(beginCell.TerrainIndex, leftCell.TerrainIndex, rightCell.TerrainIndex);

            float b = 1f / (leftCell.Elevation - beginCell.Elevation);
            if (b < 0) b = -b;

            float3 boundary = math.lerp(Perturb(begin), Perturb(left), b);
            float4 boundaryWeights = math.lerp(W100, W010, b);

            TriangulateBoundaryTriangle(right, W001, begin, W100, boundary, boundaryWeights, indices, ref metrics);

            if (HexMetrics.GetEdgeType(leftCell.Elevation, rightCell.Elevation) == HexEdgeType.Slope)
            {
                TriangulateBoundaryTriangle(left, W010, right, W001, boundary, boundaryWeights, indices, ref metrics);
            }
            else
            {
                AddTriangleUnperturbed(Perturb(left), Perturb(right), boundary);
                AddTriangleCellData(indices, W010, W001, boundaryWeights);
            }
        }

        /// <summary>
        /// Slope-Cliff 边界阶梯三角化
        /// </summary>
        private void TriangulateBoundaryTriangle(float3 begin, float4 beginWeights, float3 left, float4 leftWeights,
            float3 boundary, float4 boundaryWeights, float3 indices, ref HexMetrics metrics)
        {
            float3 v2 = Perturb(metrics.TerraceLerp(begin, left, 1));
            float h = 1f * metrics.HorizontalTerraceStepSize;
            float4 w2 = math.lerp(beginWeights, leftWeights, h);

            AddTriangleUnperturbed(Perturb(begin), v2, boundary);
            AddTriangleCellData(indices, beginWeights, w2, boundaryWeights);

            for (int i = 2; i < metrics.TerraceSteps; i++)
            {
                float3 v1 = v2;
                float4 w1 = w2;
                v2 = Perturb(metrics.TerraceLerp(begin, left, i));
                h = i * metrics.HorizontalTerraceStepSize;
                w2 = math.lerp(beginWeights, leftWeights, h);
                AddTriangleUnperturbed(v1, v2, boundary);
                AddTriangleCellData(indices, w1, w2, boundaryWeights);
            }

            AddTriangleUnperturbed(v2, Perturb(left), boundary);
            AddTriangleCellData(indices, w2, leftWeights, boundaryWeights);
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
