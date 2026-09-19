using System.Collections.Generic;
using Unity.Mathematics;

namespace HexMap
{
    /// <summary>
    /// 道路生成（纯 C#，快照上运行，计划 5.1-5.3）：
    /// 连接选择（贪心 + 角度约束）→ Dijkstra 寻路（代价 = 长度 + 陡坡重罚 + 穿河 + 边界圈，
    /// POI 半径内向内吸引）→ 沿路径 DP 高度整平（|Δe|≤1、端点锁定、河床格冻结）。
    /// 多路交叉按接受顺序串行生成；后路整平改了前路格 → 前路缎带 y 采样时实算自动贴新地形。
    /// 确定性：平手按 (总代价, offset 字典序) 破平。
    /// </summary>
    public static class HexRoadGenerator
    {
        private const float SmoothLambda = 0.1f;   // DP 目标的相邻项权重

        public static void Generate(HexFeatureSnapshot snap, in HexRoadSettings cfg,
            List<HexPoiData> pois, HexFeatureState state)
        {
            var nodes = new List<(int2 Off, float Radius)>();
            if (pois != null)
            {
                foreach (var poi in pois)
                {
                    if (poi.Type == PoiType.RoadNode && snap.InBounds(poi.CellOffset))
                        nodes.Add((poi.CellOffset, poi.Radius));
                }
            }
            if (nodes.Count < 2)
                return;   // 无路网点 → 跳过道路

            foreach (var (a, b) in SelectConnections(snap, cfg, nodes))
                BuildRoad(snap, cfg, a, b, nodes, state);

            BuildRoadDistanceMap(snap, state);
        }

        // ── 5.1 连接选择 ────────────────────────────────────────────

        private static IEnumerable<(int A, int B)> SelectConnections(HexFeatureSnapshot snap,
            HexRoadSettings cfg, List<(int2 Off, float Radius)> nodes)
        {
            int n = nodes.Count;
            var degree = new int[n];
            var directions = new List<int2>[n];   // 已有连接的方向（offset 位移）用于夹角判定
            for (int i = 0; i < n; i++)
                directions[i] = new List<int2>();

            // 全部两两组合按 HexDistance 升序（平手 offset 字典序）
            var pairs = new List<(int Dist, int A, int B)>();
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                    pairs.Add((HexMapTerrainMath.HexDistance(nodes[i].Off, nodes[j].Off), i, j));
            }
            pairs.Sort((p, q) => p.Dist != q.Dist ? p.Dist.CompareTo(q.Dist)
                : p.A != q.A ? p.A.CompareTo(q.A) : p.B.CompareTo(q.B));

            bool AllSatisfied()
            {
                for (int i = 0; i < n; i++)
                    if (degree[i] < cfg.connectionsPerNode)
                        return false;
                return true;
            }

            foreach (var (_, a, b) in pairs)
            {
                if (AllSatisfied())
                    yield break;

                if (degree[a] >= cfg.maxConnectionsTotal || degree[b] >= cfg.maxConnectionsTotal)
                    continue;

                var dir = nodes[b].Off - nodes[a].Off;
                if (!AngleOk(directions[a], dir, cfg.minConnectionAngle) ||
                    !AngleOk(directions[b], -dir, cfg.minConnectionAngle))
                    continue;

                degree[a]++;
                degree[b]++;
                directions[a].Add(dir);
                directions[b].Add(-dir);
                yield return (a, b);
            }
        }

        /// <summary>新方向与节点所有既有连接方向的夹角均 ≥ minAngle（offset 位移近似为平面向量）</summary>
        private static bool AngleOk(List<int2> existing, int2 dir, float minAngleDeg)
        {
            float minCos = math.cos(math.radians(minAngleDeg));
            float len = math.length(new float2(dir.x, dir.y));
            if (len < 1e-4f)
                return false;
            var d = math.normalizesafe(new float2(dir.x, dir.y), new float2(1, 0));

            foreach (var e in existing)
            {
                float el = math.length(new float2(e.x, e.y));
                if (el < 1e-4f)
                    continue;
                if (math.dot(d, new float2(e.x, e.y) / el) > minCos)
                    return false;   // 夹角过小
            }
            return true;
        }

        // ── 5.2 Dijkstra 寻路 ───────────────────────────────────────

        private static void BuildRoad(HexFeatureSnapshot snap, in HexRoadSettings cfg,
            int a, int b, List<(int2 Off, float Radius)> nodes, HexFeatureState state)
        {
            var path = FindPath(snap, cfg, nodes[a].Off, nodes[b].Off, nodes);
            if (path == null || path.Count < 2)
                return;   // 寻路失败（图恒连通，理论不可达）→ 放弃该对

            var leveled = LevelPath(snap, path);
            int roadId = state.Roads.Count;

            foreach (var c in path)
            {
                snap.RoadCell[c.x, c.y] = true;
                snap.RoadIdOf[c.x, c.y] = roadId;
            }

            state.Roads.Add(new RoadPath
            {
                FromPoi = a,
                ToPoi = b,
                Cells = path,
                Elevations = leveled,
                RoadId = roadId,
            });
        }

        private static List<int2> FindPath(HexFeatureSnapshot snap, in HexRoadSettings cfg,
            int2 start, int2 goal, List<(int2 Off, float Radius)> nodes)
        {
            int w = snap.CellCount.x, h = snap.CellCount.y;
            var dist = new float[w, h];
            var done = new bool[w, h];
            var prev = new int2[w, h];
            for (int z = 0; z < h; z++)
            {
                for (int x = 0; x < w; x++)
                {
                    dist[x, z] = float.MaxValue;
                    prev[x, z] = new int2(-1, -1);
                }
            }

            // POI 半径吸引：端点半径内的格进入时代价 −2（下限钳 0.05 保 Dijkstra 非负）。
            // 半径（世界单位）→ hex 距离换算：格距 ≈ 2×InnerRadius ≈ 17.32
            bool inPoiRadius(int2 c)
            {
                foreach (var node in nodes)
                {
                    int r = node.Radius <= 0 ? 1 : math.max(1, (int)math.round(node.Radius / 17.32f));
                    if (HexMapTerrainMath.HexDistance(c, node.Off) <= r)
                        return true;
                }
                return false;
            }

            dist[start.x, start.y] = 0f;
            while (true)
            {
                // 取未确定节点中 (dist, offset 字典序) 最小者（≤200 格，线性扫描确定性破平）
                int2 cur = new int2(-1, -1);
                float best = float.MaxValue;
                for (int z = 0; z < h; z++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        if (done[x, z] || dist[x, z] >= best)
                            continue;
                        best = dist[x, z];
                        cur = new int2(x, z);
                    }
                }
                if (cur.x < 0)
                    return null;   // 不可达
                if (cur.Equals(goal))
                    break;
                done[cur.x, cur.y] = true;

                for (int d = 0; d < 6; d++)
                {
                    var n = HexBoundary.NeighborOffset(cur, (HexDirection)d);
                    if (!snap.InBounds(n) || done[n.x, n.y])
                        continue;

                    int de = math.abs(snap.GetElev(n) - snap.GetElev(cur));
                    float cost = 1f
                        + cfg.maxHeightCost * math.max(0, de - 1) * math.max(0, de - 1)  // 陡步重罚（软约束）
                        + cfg.maxHeightCost * 0.5f * de                                   // 轻度高度变化偏好
                        + (snap.RiverBed[n.x, n.y] || snap.RiverBank[n.x, n.y] || snap.LakeCell[n.x, n.y]
                            ? cfg.riverCrossCost : 0)
                        + (HexBoundary.IsBoundary(n, snap.CellCount) ? cfg.boundaryCost : 0);
                    if (inPoiRadius(n))
                        cost -= 2f;

                    float nd = dist[cur.x, cur.y] + math.max(0.05f, cost);
                    if (nd < dist[n.x, n.y])
                    {
                        dist[n.x, n.y] = nd;
                        prev[n.x, n.y] = cur;
                    }
                }
            }

            // 回溯
            var result = new List<int2>();
            var c2 = goal;
            while (c2.x >= 0)
            {
                result.Add(c2);
                if (c2.Equals(start))
                    break;
                c2 = prev[c2.x, c2.y];
            }
            result.Reverse();
            return result[0].Equals(start) ? result : null;
        }

        // ── 5.3 高度整平（沿路径 DP） ──────────────────────────────

        /// <summary>
        /// 目标剖面 e'：|e'ᵢ₊₁−e'ᵢ| ≤ 1、clamp、端点锁定（POI 站台不动）、
        /// 河床格强制 e'ᵢ = snapElevᵢ（河流已定稿，不许道路毁河），
        /// 最小化 Σ(|e'ᵢ−eᵢ| + λ·|e'ᵢ₊₁−e'ᵢ|)。复杂度 O(n × MaxElevation × 3)。
        /// </summary>
        private static List<int> LevelPath(HexFeatureSnapshot snap, List<int2> path)
        {
            int n = path.Count;
            int hMax = snap.MaxElevation;
            int[] e = new int[n];
            for (int i = 0; i < n; i++)
                e[i] = snap.GetElev(path[i]);

            const float inf = float.MaxValue / 4;
            var dp = new float[n, hMax + 1];
            var from = new int[n, hMax + 1];
            for (int hh = 0; hh <= hMax; hh++)
                dp[0, hh] = inf;
            dp[0, math.clamp(e[0], 0, hMax)] = 0f;   // 端点锁定

            for (int i = 1; i < n; i++)
            {
                bool bed = snap.RiverBed[path[i].x, path[i].y];   // 河床冻结
                bool endpoint = i == n - 1;                        // 终点锁定（优先于河床）

                for (int hh = 0; hh <= hMax; hh++)
                {
                    dp[i, hh] = inf;
                    if (bed && !endpoint && hh != e[i])
                        continue;

                    float best = inf;
                    int bestFrom = -1;
                    for (int hf = math.max(0, hh - 1); hf <= math.min(hMax, hh + 1); hf++)
                    {
                        if (dp[i - 1, hf] >= inf)
                            continue;
                        float v = dp[i - 1, hf] + math.abs(hh - e[i]) + SmoothLambda * math.abs(hh - hf);
                        if (v < best)
                        {
                            best = v;
                            bestFrom = hf;
                        }
                    }
                    dp[i, hh] = best;
                    from[i, hh] = bestFrom;
                }
            }

            // 回溯（终点锁定）
            int curH = math.clamp(e[n - 1], 0, hMax);
            var result = new int[n];
            for (int i = n - 1; i >= 0; i--)
            {
                result[i] = curH;
                snap.Snap[path[i].x, path[i].y] = curH;
                if (i > 0)
                    curH = from[i, curH];
            }
            return new List<int>(result);
        }

        // ── 距离图 ─────────────────────────────────────────────────

        /// <summary>多源 BFS 距离图（道路格作源）——植被散布的路距过滤用</summary>
        private static void BuildRoadDistanceMap(HexFeatureSnapshot snap, HexFeatureState state)
        {
            state.RoadDist.Clear();
            var queue = new Queue<int2>();
            for (int z = 0; z < snap.CellCount.y; z++)
            {
                for (int x = 0; x < snap.CellCount.x; x++)
                {
                    if (snap.RoadCell[x, z])
                    {
                        state.RoadDist[new int2(x, z)] = 0;
                        queue.Enqueue(new int2(x, z));
                    }
                }
            }

            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                int dist = state.RoadDist[c];
                for (int d = 0; d < 6; d++)
                {
                    var n = HexBoundary.NeighborOffset(c, (HexDirection)d);
                    if (snap.InBounds(n) && !state.RoadDist.ContainsKey(n))
                    {
                        state.RoadDist[n] = dist + 1;
                        queue.Enqueue(n);
                    }
                }
            }
        }
    }
}
