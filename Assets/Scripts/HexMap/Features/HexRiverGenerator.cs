using System.Collections.Generic;
using Unity.Mathematics;

namespace HexMap
{
    /// <summary>
    /// 河流生成（纯 C#，快照上运行，MapMagic 技术的六边形移植——只借鉴思路零引用）：
    /// 泉眼选择 → 贪心液滴行走（预算雕刻 + 回路防护 + 宽度增长）→
    /// 卡死泛洪成湖（水位抬升求溢流）→ 溢流续走。
    /// 提交（写回 ECS/标签/水面网格）由 HexFeatureGenerationSystem 负责。
    ///
    /// 确定性：无随机数——候选排序固定（高程/转角/方向序），湖泊泛洪按 offset 字典序破平；
    /// 泉眼阈值/间距过滤全部确定性。同 seed 同地形 → 同结果。
    /// </summary>
    public static class HexRiverGenerator
    {
        private const float WidthMax = 2.2f;
        private const int HistoryLength = 12;
        /// <summary>
        /// 河流可见水深（床面上方）。计划原文写 −0.1（贴床），但地形板不透明、
        /// 水面在床面之下会被完全遮住；且计划 4.5 的岸裙边（岸板低于水面 → 向下裙边）
        /// 明确预期水面高于岸板——故取 +Depth，岸壁竖裙边由水面网格补齐。
        /// </summary>
        public const float RiverDepth = 0.35f;

        public static void Generate(HexFeatureSnapshot snap, in HexRiverSettings cfg,
            List<HexPoiData> pois, HexFeatureState state)
        {
            var bedWater = new Dictionary<int2, float>();   // 已定稿河床格 → 水面（后继河流 JoinRiver 校验用）
            foreach (var spring in SelectSprings(snap, cfg, pois))
                WalkRiver(snap, cfg, spring, state, bedWater);

            BuildRiverDistanceMap(snap, state);
        }

        // ── 4.1 泉眼选择 ────────────────────────────────────────────

        /// <summary>
        /// 手动 POI(RiverSpring) ∪ 程序泉眼。
        /// 程序候选 = 高程 ≥ 阈值 且 6 邻域无更高邻居（含平顶）；
        /// 按 (高程 desc, offset 字典序) 排序贪心取 ≤ maxSprings 个，两两/与手动 POI 间距 ≥ springMinSpacing。
        /// </summary>
        private static List<int2> SelectSprings(HexFeatureSnapshot snap, in HexRiverSettings cfg,
            List<HexPoiData> pois)
        {
            var accepted = new List<int2>();
            if (pois != null)
            {
                foreach (var poi in pois)
                {
                    if (poi.Type == PoiType.RiverSpring && snap.InBounds(poi.CellOffset))
                        accepted.Add(poi.CellOffset);
                }
            }

            int threshold = cfg.springNoiseThresholdElevation > 0
                ? cfg.springNoiseThresholdElevation
                : (int)math.ceil(snap.MaxElevation * 0.7f);

            var candidates = new List<(int2 Off, int Elev)>();
            for (int z = 0; z < snap.CellCount.y; z++)
            {
                for (int x = 0; x < snap.CellCount.x; x++)
                {
                    var o = new int2(x, z);
                    int e = snap.GetElev(o);
                    if (e < threshold)
                        continue;

                    bool hasHigher = false;
                    for (int d = 0; d < 6 && !hasHigher; d++)
                    {
                        var n = HexBoundary.NeighborOffset(o, (HexDirection)d);
                        if (snap.InBounds(n) && snap.GetElev(n) > e)
                            hasHigher = true;
                    }
                    if (!hasHigher)
                        candidates.Add((o, e));
                }
            }

            candidates.Sort((a, b) =>
                a.Elev != b.Elev ? b.Elev.CompareTo(a.Elev)
                : a.Off.x != b.Off.x ? a.Off.x.CompareTo(b.Off.x)
                : a.Off.y.CompareTo(b.Off.y));

            int taken = 0;
            foreach (var c in candidates)
            {
                if (taken >= cfg.maxSprings)
                    break;
                bool spaced = true;
                foreach (var a in accepted)
                {
                    if (HexMapTerrainMath.HexDistance(c.Off, a) < cfg.springMinSpacing)
                    {
                        spaced = false;
                        break;
                    }
                }
                if (!spaced)
                    continue;
                accepted.Add(c.Off);
                taken++;
            }
            return accepted;
        }

        // ── 4.2 河流行走 ────────────────────────────────────────────

        private static void WalkRiver(HexFeatureSnapshot snap, in HexRiverSettings cfg, int2 spring,
            HexFeatureState state, Dictionary<int2, float> bedWater)
        {
            int riverId = state.Rivers.Count;
            var path = new List<int2> { spring };
            var visited = new HashSet<int2> { spring };
            var history = new Queue<int2>();
            history.Enqueue(spring);

            var bankCells = new List<int2>();
            var bankBedIdx = new List<int>();       // 每个河岸格侧翼的河床步下标（水面取该步值）

            snap.RiverBed[spring.x, spring.y] = true;
            snap.RiverIdOf[spring.x, spring.y] = riverId;

            int2 cur = spring;
            int curElev = snap.GetElev(spring);
            int? prevDir = null;
            float width = 1f;
            var end = RiverEndKind.DryUp;

            while (true)
            {
                if (path.Count > 200)
                {
                    end = RiverEndKind.DryUp;   // 保险丝
                    break;
                }

                var cands = OrderCandidates(snap, cur, prevDir, visited, history);
                bool advanced = false;

                foreach (var (dir, cand) in cands)
                {
                    // JoinRiver：接入既有河床（|水面差| ≤ 1 台阶 且 转角 ≤ 2）
                    if (snap.RiverBed[cand.x, cand.y] && snap.RiverIdOf[cand.x, cand.y] != riverId)
                    {
                        if (bedWater.TryGetValue(cand, out var otherWy))
                        {
                            float curWy = snap.PlateY(cur, curElev) + RiverDepth;
                            if (math.abs(otherWy - curWy) <= snap.ElevationStep &&
                                TurnDelta(prevDir, dir) <= 2)
                            {
                                end = RiverEndKind.Joined;
                                advanced = true;
                                break;   // 接入成功，立即结束行走（否则后续候选会被误接受）
                            }
                        }
                        continue;   // 校验失败 → 不可行，次优候选
                    }

                    // 既有湖 → 河入湖（Joined）
                    if (snap.LakeCell[cand.x, cand.y])
                    {
                        end = RiverEndKind.Joined;
                        advanced = true;
                        break;
                    }

                    // 雕刻预算：宽度越大允许挖越深
                    float widthNorm = math.saturate((width - 1f) / (WidthMax - 1f));
                    int allowance = (int)math.round(math.lerp(
                        cfg.initialCarveAllowance, cfg.maxCarveAllowance, widthNorm));
                    int diff = snap.GetElev(cand) - curElev;

                    if (diff > allowance)
                        continue;   // 挖不动 → 不可行，次优候选

                    if (diff > 0)
                    {
                        // 雕刻：挖到当前河床（峡谷河）；每挖 1 层宽度惩罚
                        snap.Snap[cand.x, cand.y] = curElev;
                        width -= 0.15f * diff;
                    }
                    else if (diff < -cfg.maxElevationDropPerStep)
                    {
                        // 自然陡降合理：把高的一侧（本格）削平到差 = 1（阶梯河，水面瀑布 = 落差裙边）
                        curElev = snap.GetElev(cand) + cfg.maxElevationDropPerStep;
                        snap.Snap[cur.x, cur.y] = curElev;
                    }

                    int candElev = snap.GetElev(cand);
                    int stepIdx = path.Count;
                    path.Add(cand);
                    visited.Add(cand);
                    history.Enqueue(cand);
                    if (history.Count > HistoryLength)
                        history.Dequeue();
                    snap.RiverBed[cand.x, cand.y] = true;
                    snap.RiverIdOf[cand.x, cand.y] = riverId;

                    width = math.clamp(width + cfg.widthGrowPerStep, 0f, WidthMax);
                    if (width >= cfg.widthTwoCellThreshold)
                        Widen(snap, cur, (HexDirection)dir, candElev, riverId, bankCells, bankBedIdx, stepIdx);

                    advanced = true;

                    if (candElev == 0 || HexBoundary.IsBoundary(cand, snap.CellCount))
                    {
                        end = RiverEndKind.Sea;
                        break;
                    }

                    cur = cand;
                    curElev = candElev;
                    prevDir = dir;
                    break;
                }

                if (end == RiverEndKind.Sea || end == RiverEndKind.Joined)
                    break;

                if (advanced)
                    continue;

                // 卡死 → 湖泊泛洪（4.3）
                var lake = FloodLake(snap, cfg, cur, state);
                if (lake == null || !lake.HasSpill)
                {
                    end = RiverEndKind.Lake;   // 封闭盆地 / 超限 / 合并既有湖 → 河终于湖（或并入）
                    break;
                }

                var spill = lake.SpillCell;
                int spillElev = snap.GetElev(spill);

                // 本河的湖格并入 visited：湖内高程更低，候选排序会优先选中 →
                // 不排除的话下一步立即「入湖 Joined」把出湖河截断
                foreach (var lc in lake.Cells)
                    visited.Add(lc);

                path.Add(spill);
                visited.Add(spill);
                snap.RiverBed[spill.x, spill.y] = true;
                snap.RiverIdOf[spill.x, spill.y] = riverId;

                if (spillElev == 0 || HexBoundary.IsBoundary(spill, snap.CellCount))
                {
                    end = RiverEndKind.Sea;    // 溢流口即海 → 结束
                    break;
                }

                cur = spill;
                curElev = spillElev;
                // 出湖方向 = 湖心 → 溢流口，供下步转角约束
                prevDir = PrevDirection(snap, lake, spill);
            }

            // 验收：长度不足整条丢弃（DryUp 且长度足够 → 保留，末端细流自然消失）
            if (path.Count < cfg.minRiverLength)
            {
                DiscardRiver(snap, riverId, path, bankCells);
                return;
            }

            // 剖面修复：行进中「削平当前格接陡降」只修了前向边（cur→cand），被削的 cur
            // 与其前驱的落差可能被拉大（A5→B5→C1 削 B 到 2，A→B 差 3）。
            // 两遍 min 平滑（前向+后向）把整条路径压回 |Δe| ≤ maxDrop——只降不升，
            // 与雕刻语义一致；后向遍历同时兜住「源头被削」的情形。
            int maxDrop = math.max(1, cfg.maxElevationDropPerStep);
            var bedE = new int[path.Count];
            for (int i = 0; i < path.Count; i++)
                bedE[i] = snap.GetElev(path[i]);
            for (int i = 1; i < path.Count; i++)
                bedE[i] = math.min(bedE[i], bedE[i - 1] + maxDrop);
            for (int i = path.Count - 2; i >= 0; i--)
                bedE[i] = math.min(bedE[i], bedE[i + 1] + maxDrop);
            for (int i = 0; i < path.Count; i++)
            {
                if (snap.GetElev(path[i]) != bedE[i])
                    snap.Snap[path[i].x, path[i].y] = bedE[i];
            }

            // 河岸格重新对齐其侧翼河床步的平滑后高程
            for (int i = 0; i < bankCells.Count; i++)
            {
                int step = math.min(bankBedIdx[i], bedE.Length - 1);
                snap.Snap[bankCells[i].x, bankCells[i].y] = bedE[step];
            }

            // 水面后算：单调不升 + 床面上方 RiverDepth（见常量注释），河岸格与侧翼河床步同值
            var waterY = new List<float>(path.Count);
            float prevWy = float.MaxValue;
            foreach (var c in path)
            {
                float wy = math.min(prevWy, snap.PlateY(c, snap.GetElev(c)) + RiverDepth);
                waterY.Add(wy);
                prevWy = wy;
                bedWater[c] = wy;
                snap.RiverWaterY[c.x, c.y] = wy;
            }
            var bankWaterY = new List<float>(bankCells.Count);
            foreach (var idx in bankBedIdx)
            {
                float wy = waterY[math.min(idx, waterY.Count - 1)];
                bankWaterY.Add(wy);
            }
            for (int i = 0; i < bankCells.Count; i++)
                snap.RiverWaterY[bankCells[i].x, bankCells[i].y] = bankWaterY[i];

            state.Rivers.Add(new RiverPath
            {
                Cells = path,
                WaterY = waterY,
                End = end,
                RiverId = riverId,
                BankCells = bankCells,
                BankWaterY = bankWaterY,
            });
        }

        /// <summary>候选排序：转角锥（≤2，即 120°）内按高程最低、转角最小、方向序；锥空才允许掉头</summary>
        private static List<(int Dir, int2 Cand)> OrderCandidates(HexFeatureSnapshot snap,
            int2 cur, int? prevDir, HashSet<int2> visited, Queue<int2> history)
        {
            var hist = history.ToArray();   // 回路防护参考最近 6 格
            int histCheck = math.min(6, hist.Length);

            var inCone = new List<(int Dir, int2 Cand, int Elev)>();
            var uTurn = new List<(int Dir, int2 Cand, int Elev)>();

            for (int d = 0; d < 6; d++)
            {
                var cand = HexBoundary.NeighborOffset(cur, (HexDirection)d);
                if (!snap.InBounds(cand) || visited.Contains(cand))
                    continue;

                // 回路防护：候选靠近任何近格反而比当前更近 → 视为回头
                bool loopsBack = false;
                for (int i = hist.Length - histCheck; i < hist.Length; i++)
                {
                    if (HexMapTerrainMath.HexDistance(cand, hist[i]) <=
                        HexMapTerrainMath.HexDistance(cur, hist[i]))
                    {
                        loopsBack = true;
                        break;
                    }
                }
                if (loopsBack)
                    continue;

                var entry = (d, cand, snap.GetElev(cand));
                if (TurnDelta(prevDir, d) <= 2)
                    inCone.Add(entry);
                else
                    uTurn.Add(entry);
            }

            var pool = inCone.Count > 0 ? inCone : uTurn;
            pool.Sort((a, b) =>
                a.Item3 != b.Item3 ? a.Item3.CompareTo(b.Item3)
                : TurnDelta(prevDir, a.Dir) != TurnDelta(prevDir, b.Dir)
                    ? TurnDelta(prevDir, a.Dir).CompareTo(TurnDelta(prevDir, b.Dir))
                : a.Dir.CompareTo(b.Dir));

            var result = new List<(int, int2)>(pool.Count);
            foreach (var e in pool)
                result.Add((e.Dir, e.Cand));
            return result;
        }

        /// <summary>加宽：A→B 共享边两侧相邻格标 riverBank，高程对齐河床，水面同值</summary>
        private static void Widen(HexFeatureSnapshot snap, int2 cur, HexDirection dir,
            int bedElev, int riverId, List<int2> bankCells, List<int> bankBedIdx, int stepIdx)
        {
            _ = riverId;   // 河岸格归属由整河验收决定（丢弃时回滚）
            foreach (var nd in new[] { dir.Previous(), dir.Next() })
            {
                var b = HexBoundary.NeighborOffset(cur, nd);
                if (!snap.InBounds(b))
                    continue;
                if (snap.RiverBed[b.x, b.y] || snap.LakeCell[b.x, b.y] || snap.RiverBank[b.x, b.y])
                    continue;

                snap.RiverBank[b.x, b.y] = true;
                snap.Snap[b.x, b.y] = bedElev;
                bankCells.Add(b);
                bankBedIdx.Add(stepIdx);
            }
        }

        private static int TurnDelta(int? prevDir, int dir)
        {
            if (prevDir == null)
                return 0;
            return TurnDelta(prevDir.Value, dir);
        }

        private static int TurnDelta(int a, int b)
        {
            int delta = (b - a + 6) % 6;
            return math.min(delta, 6 - delta);
        }

        // ── 4.3 湖泊泛洪（卡死时） ─────────────────────────────────

        private sealed class FloodResult
        {
            public bool HasSpill;
            public int2 SpillCell;
            public List<int2> Cells;
        }

        /// <summary>
        /// 从卡死格泛洪：BFS 可通过 = 图内 且 snapElev ≤ L 且非既有湖。
        /// border 空 → 封闭盆地定稿；最低 border ≤ L+1 → 溢流定稿（河从 spill 续走）；
        /// 面积超 maxLakeCells → 定稿当前 L；触到既有湖 → 返回 null（合并语义，河终于湖）。
        /// 湖不削地形（水 sitting on plates），水位 y=(L+0.5)×Step 覆盖 ≤L 板面且不淹 L+1。
        /// </summary>
        private static FloodResult FloodLake(HexFeatureSnapshot snap, in HexRiverSettings cfg,
            int2 start, HexFeatureState state)
        {
            int L = snap.GetElev(start);

            while (true)
            {
                var flooded = new List<int2>();
                var floodedSet = new HashSet<int2>();
                var queue = new Queue<int2>();
                queue.Enqueue(start);
                floodedSet.Add(start);

                while (queue.Count > 0)
                {
                    var c = queue.Dequeue();
                    flooded.Add(c);
                    for (int d = 0; d < 6; d++)
                    {
                        var n = HexBoundary.NeighborOffset(c, (HexDirection)d);
                        if (!snap.InBounds(n) || floodedSet.Contains(n) ||
                            snap.GetElev(n) > L || snap.LakeCell[n.x, n.y])
                            continue;
                        floodedSet.Add(n);
                        queue.Enqueue(n);
                    }
                }

                // 触到既有湖 → 合并语义（河终于湖，水并入既有水位体系）
                bool touchesLake = false;
                foreach (var c in flooded)
                {
                    for (int d = 0; d < 6 && !touchesLake; d++)
                    {
                        var n = HexBoundary.NeighborOffset(c, (HexDirection)d);
                        if (snap.InBounds(n) && snap.LakeCell[n.x, n.y])
                            touchesLake = true;
                    }
                    if (touchesLake)
                        break;
                }
                if (touchesLake)
                    return null;

                // border = flooded 邻居中高于 L 的格
                bool borderEmpty = true;
                int2 spill = default;
                int spillElev = int.MaxValue;
                foreach (var c in flooded)
                {
                    for (int d = 0; d < 6; d++)
                    {
                        var n = HexBoundary.NeighborOffset(c, (HexDirection)d);
                        if (!snap.InBounds(n) || snap.GetElev(n) <= L || floodedSet.Contains(n))
                            continue;
                        borderEmpty = false;
                        int ne = snap.GetElev(n);
                        if (ne < spillElev || (ne == spillElev && LessThan(n, spill)))
                        {
                            spillElev = ne;
                            spill = n;
                        }
                    }
                }

                if (borderEmpty || flooded.Count >= cfg.maxLakeCells)
                    return FinalizeLake(snap, state, flooded, L, false, new int2(-1, -1));

                if (spillElev <= L + 1)
                    return FinalizeLake(snap, state, flooded, L, true, spill);

                L += 1;   // 抬一级水位继续泛洪
            }
        }

        private static FloodResult FinalizeLake(HexFeatureSnapshot snap, HexFeatureState state,
            List<int2> cells, int level, bool hasSpill, int2 spill)
        {
            float waterY = (level + 0.5f) * snap.ElevationStep;
            int lakeId = state.Lakes.Count;

            foreach (var c in cells)
            {
                snap.LakeCell[c.x, c.y] = true;
                snap.LakeIdOf[c.x, c.y] = lakeId;
                snap.LakeWaterY[c.x, c.y] = waterY;
                // 湖格淹没水下的河床段保留 RiverBed 标记（水下河道），河岸标记清除避免双重语义
                snap.RiverBank[c.x, c.y] = false;
            }

            state.Lakes.Add(new LakeData
            {
                Cells = cells,
                SpillCell = spill,
                WaterY = waterY,
                Level = level,
                LakeId = lakeId,
            });

            return new FloodResult { HasSpill = hasSpill, SpillCell = spill, Cells = cells };
        }

        // ── 丢弃回滚 / 距离图 ──────────────────────────────────────

        /// <summary>整条丢弃：恢复本河触碰格的快照（高程/标记），湖保留（独立水系）</summary>
        private static void DiscardRiver(HexFeatureSnapshot snap, int riverId,
            List<int2> path, List<int2> bankCells)
        {
            foreach (var c in bankCells)
            {
                if (!snap.RiverBed[c.x, c.y])   // 后续成为河床的岸格不动
                {
                    snap.RiverBank[c.x, c.y] = false;
                    snap.Snap[c.x, c.y] = snap.Elev[c.x, c.y];
                }
            }
            foreach (var c in path)
            {
                if (snap.RiverIdOf[c.x, c.y] == riverId)
                {
                    snap.RiverBed[c.x, c.y] = false;
                    snap.RiverIdOf[c.x, c.y] = -1;
                }
                if (snap.Snap[c.x, c.y] != snap.Elev[c.x, c.y] && snap.LakeIdOf[c.x, c.y] < 0)
                    snap.Snap[c.x, c.y] = snap.Elev[c.x, c.y];
            }
        }

        /// <summary>多源 BFS 距离图（河/湖格一起作河源）——植被散布的河距过滤用；
        /// 存档加载路径重建状态时也调用</summary>
        public static void BuildRiverDistanceMap(HexFeatureSnapshot snap, HexFeatureState state)
        {
            state.RiverDist.Clear();
            var queue = new Queue<int2>();
            for (int z = 0; z < snap.CellCount.y; z++)
            {
                for (int x = 0; x < snap.CellCount.x; x++)
                {
                    if (snap.RiverBed[x, z] || snap.RiverBank[x, z] || snap.LakeCell[x, z])
                    {
                        state.RiverDist[new int2(x, z)] = 0;
                        queue.Enqueue(new int2(x, z));
                    }
                }
            }

            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                int dist = state.RiverDist[c];
                for (int d = 0; d < 6; d++)
                {
                    var n = HexBoundary.NeighborOffset(c, (HexDirection)d);
                    if (snap.InBounds(n) && !state.RiverDist.ContainsKey(n))
                    {
                        state.RiverDist[n] = dist + 1;
                        queue.Enqueue(n);
                    }
                }
            }
        }

        /// <summary>出湖方向的近似：取湖质心 → 溢流口方向（确定性破平：首个最小点积方向）</summary>
        private static int PrevDirection(HexFeatureSnapshot snap, FloodResult lake, int2 spill)
        {
            _ = snap;
            if (lake.Cells.Count < 2)
                return 0;   // 单格湖无方向可言

            float2 center = float2.zero;
            foreach (var c in lake.Cells)
                center += new float2(c.x, c.y);
            center /= lake.Cells.Count;

            int best = 0;
            float bestDot = float.MaxValue;
            for (int d = 0; d < 6; d++)
            {
                // 方向 d 的格间位移（offset 奇偶行规则逆用的近似常量：六方向单位向量）
                var n = HexBoundary.NeighborOffset(spill, (HexDirection)d);
                float2 v = math.normalize(new float2(n.x - spill.x, n.y - spill.y));
                float dot = math.dot(v, math.normalize(center - new float2(spill.x, spill.y)));
                if (dot < bestDot)
                {
                    bestDot = dot;
                    best = d;
                }
            }
            return best;
        }

        private static bool LessThan(int2 a, int2 b)
            => a.x != b.x ? a.x < b.x : a.y < b.y;
    }
}
