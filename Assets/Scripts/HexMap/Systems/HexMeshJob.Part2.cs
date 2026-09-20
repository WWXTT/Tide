using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HexMap
{
    /// <summary>
    /// HexMeshJob Part 2：垂直陡壁。
    ///
    /// 壁（本格高于对侧）：本格全宽板缘（名义角，扰动后）垂直下落到低侧板缘（或 bottomY），
    /// 由高格生成；等高相邻零几何（两板直接共边密铺）；更低相邻由邻居发壁。
    /// 顶行与板边环共享同一批标称角点 + 同 EdgeVertices 插值 + 同 Perturb → 逐点重合；
    /// 底行复制顶行 x/z（同一批点），与低侧板缘逐点重合。
    ///
    /// 墙柱分段（防 T-junction 发丝缝）：边 d 两端角落的第三方格（N(d-1) 与 N(d+1)）的
    /// 板角顶点会落在本壁的角落竖棱上——其板高严格处于 (底, 顶) 开区间时，把壁按行
    /// 在该高度处分段，第三方板角/邻壁端点与本壁顶点逐点重合。同一高度同时覆盖
    /// 「第三方板角」与「第三方与低侧邻格间陡壁的端点」两类落点（高度同源）。
    /// 流式第三方无板面 → 不分段；其加载后本格随邻居标脏重建。
    ///
    /// 多级高差角落（烟囱构造）：角 k 三方 A/N(k)/N(k-1) 各自在自己的边上发壁，
    /// 三条壁共享同一条角落竖棱（同一批扰动 x/z + 同一批分段高度），围绕竖棱
    /// 逐段首尾相接 → 任何高度组合下构造性水密，无重叠无裂缝。
    ///
    /// splat：顶行 = 高格地形 W100 → 底行 = 低格地形 W010 随行线性渐变
    /// （HeightBlend3 在 0→1 干净梯度上按高度图锯齿化，竖向过渡自然）；
    /// 边界/流式壁恒高格地形。变异权重全 0（壁是过渡面，与板缘 0 一致）。
    /// </summary>
    public partial struct HexMeshJob
    {
        /// <summary>
        /// 为一个方向生成连接几何（分类 → 分发；板已在 BuildPlate 全宽覆盖）
        /// </summary>
        private void BuildDirection(HexDirection d, HexCellData cell, ref HexMetrics metrics,
            ref HexMapConfigBlob blob)
        {
            var kind = ClassifyEdge(cell, CellEntity, d, ref blob, out var neighbor, out _);

            switch (kind)
            {
                case EdgeKind.Equal:
                case EdgeKind.Higher:
                    break; // 等高密铺零几何；更低侧的壁由邻居生成

                case EdgeKind.Lower:
                    BuildWall(d, cell, neighbor, neighbor.Position.y, ref metrics);
                    break;

                case EdgeKind.Boundary:
                case EdgeKind.Streaming:
                    // 图外永久壁 / 图内未加载临时壁（邻居加载后标脏重建），都直落 bottomY。
                    // neighbor 为哨兵值，仅用于占位（realNeighbor = false 分支不读它）。
                    BuildWall(d, cell, neighbor, GetBottomY(ref metrics), ref metrics);
                    break;
            }
        }

        /// <summary>
        /// 垂直陡壁：顶 = 本格板缘（扰动），底 = 同批 x/z 落到低侧板高（或 bottomY）；
        /// 行按角落第三方板高分段，列 = EdgeVertices 5 段。
        /// lower 为有效邻居时（真实低侧）底行 splat 渐变到低格地形，否则恒本格地形。
        /// </summary>
        private void BuildWall(HexDirection d, HexCellData cell, HexCellData lower, float toY,
            ref HexMetrics metrics)
        {
            float topY = cell.Position.y;
            if (topY - toY < 1e-3f)
                return; // 防御：零高差不发壁

            // 顶边标称角点 → 扰动后的 5 列（与板边环同一批点）
            float3 c1 = cell.Position + metrics.Corners[(int)d];
            float3 c2 = cell.Position + metrics.Corners[(int)d + 1];
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

            bool realNeighbor = IsValidData(lower);
            _surfaceNormal = metrics.GetEdgeNormal(d); // 壁法线 = 逐边水平外法线（直写 NORMAL 通道）
            int lowerTerrain = realNeighbor ? lower.TerrainIndex : cell.TerrainIndex;
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
    }
}
