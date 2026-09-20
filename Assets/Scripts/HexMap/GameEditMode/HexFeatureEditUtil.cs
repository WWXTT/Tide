using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HexMap
{
    /// <summary>
    /// 水/路手编操作（游戏内笔刷，HexMapEditingSystem 调用）：
    /// 直接编辑 HexFeatureState.Rivers/Lakes/Roads 列表 + cell 标签，
    /// 改完调 RefreshFeatureMeshes 当帧重建特征网格（实时刷新）。
    ///
    /// 语义（与生成管线一致）：
    /// - 水面网格按 state 列表构建（不查标签），标签只是 Commit/Overlay 的派生物 →
    ///   手编必须同时改列表与标签（Add/SetComponentData）。
    /// - 手编不改高程 → 不写 ElevationOverrides/OverlayApplied（河床挖掘用高度笔刷自己做）。
    /// - 河 WaterY 与 Cells 等长且单调不升：新格 = min(前格水面, 本格板面) − 0.1（生成器同式）；
    ///   湖水位 = (格集最高高程 + 0.5) × ElevationStep（计划 4.3 推导，不淹高台）。
    /// - R 重生成后手动水/路被清（ResetFeatures 清列表，既有语义）；删除河格不清理
    ///   生成河的 BankCells（加宽岸格残留到下次重生成，边缘情形）。
    /// </summary>
    public static class HexFeatureEditUtil
    {
        // ── 笔画起点（鼠标按下时由编辑系统调用）──────────────────

        /// <summary>开一条新手绘河（空路径，随笔画追加格）</summary>
        public static RiverPath BeginRiver(HexFeatureState state)
        {
            var river = new RiverPath
            {
                RiverId = NextRiverId(state),
                Cells = new List<int2>(),
                WaterY = new List<float>(),
                End = RiverEndKind.DryUp,
                BankCells = null,
                BankWaterY = null,
            };
            state.Rivers.Add(river);
            return river;
        }

        /// <summary>开一个新手绘湖</summary>
        public static LakeData BeginLake(HexFeatureState state)
        {
            var lake = new LakeData
            {
                LakeId = NextLakeId(state),
                Cells = new List<int2>(),
                SpillCell = new int2(-1, -1),
                WaterY = 0f,
                Level = 0,
            };
            state.Lakes.Add(lake);
            return lake;
        }

        /// <summary>开一条新手绘路（FromPoi/ToPoi=-1：非 POI 连接路）</summary>
        public static RoadPath BeginRoad(HexFeatureState state)
        {
            var road = new RoadPath
            {
                RoadId = NextRoadId(state),
                FromPoi = -1,
                ToPoi = -1,
                Cells = new List<int2>(),
                Elevations = new List<int>(),
            };
            state.Roads.Add(road);
            return road;
        }

        // ── 逐格追加 / 删除 ─────────────────────────────────────

        /// <summary>河床格追加（去重）。同格已属湖/别的河 → 先剥离再并入本河。</summary>
        public static void AppendRiverCell(RiverPath river, HexFeatureState state,
            EntityManager em, Entity cellEntity, int2 offset, float plateY)
        {
            if (river.Cells.Contains(offset))
                return;

            RemoveLakeMembership(state, offset);
            RemoveRiverMembership(state, offset, except: river);
            if (em.HasComponent<HexLakeCell>(cellEntity))
                em.RemoveComponent<HexLakeCell>(cellEntity);

            float y = river.WaterY.Count == 0
                ? plateY - 0.1f
                : math.min(river.WaterY[river.WaterY.Count - 1], plateY) - 0.1f;
            river.Cells.Add(offset);
            river.WaterY.Add(y);
            SetOrAdd(em, cellEntity, new HexRiverCell { RiverId = river.RiverId, WaterY = y, IsBank = 0 });
        }

        /// <summary>湖格追加（去重）。水位随格集最高高程重算；同格已属河先剥离。</summary>
        public static void AppendLakeCell(LakeData lake, HexFeatureState state,
            EntityManager em, Entity cellEntity, int2 offset, int elevation, float elevationStep)
        {
            if (lake.Cells.Contains(offset))
                return;

            RemoveLakeMembership(state, offset, except: lake);
            RemoveRiverMembership(state, offset, except: null);
            if (em.HasComponent<HexRiverCell>(cellEntity))
                em.RemoveComponent<HexRiverCell>(cellEntity);

            lake.Cells.Add(offset);
            lake.Level = math.max(lake.Level, elevation);
            lake.WaterY = (lake.Level + 0.5f) * elevationStep;
            SetOrAdd(em, cellEntity, new HexLakeCell { LakeId = lake.LakeId, WaterY = lake.WaterY });
        }

        /// <summary>道路格追加（去重；路可与河共存 = 桥，不剥离水成员）</summary>
        public static void AppendRoadCell(RoadPath road, EntityManager em,
            Entity cellEntity, int2 offset, int elevation)
        {
            if (road.Cells.Contains(offset))
                return;

            road.Cells.Add(offset);
            road.Elevations.Add(elevation);
            SetOrAdd(em, cellEntity, new HexRoadCell { RoadId = road.RoadId });
        }

        /// <summary>清除该格的水：从所有河/湖剔除；空水体整条删；剩余湖水位按格集重算。</summary>
        public static void RemoveWaterAt(HexFeatureState state, EntityManager em,
            NativeHashMap<int2, Entity> lookup, Entity cellEntity, int2 offset, float elevationStep)
        {
            for (int r = state.Rivers.Count - 1; r >= 0; r--)
            {
                var river = state.Rivers[r];
                int idx = river.Cells.IndexOf(offset);
                if (idx >= 0)
                {
                    river.Cells.RemoveAt(idx);
                    river.WaterY.RemoveAt(idx);
                }
                // BankCells 不清理：生成河的加宽岸格保留到下次重生成（边缘情形，见类注释）
                if (river.Cells.Count == 0)
                    state.Rivers.RemoveAt(r);
            }

            for (int l = state.Lakes.Count - 1; l >= 0; l--)
            {
                var lake = state.Lakes[l];
                if (!lake.Cells.Remove(offset))
                    continue;
                if (lake.Cells.Count == 0)
                {
                    state.Lakes.RemoveAt(l);
                    continue;
                }
                // 水位随剩余格重算（读活 cell 高程）
                int maxElev = 0;
                foreach (var c in lake.Cells)
                    if (lookup.TryGetValue(c, out var ce))
                        maxElev = math.max(maxElev, em.GetComponentData<HexCellData>(ce).Elevation);
                lake.Level = maxElev;
                lake.WaterY = (lake.Level + 0.5f) * elevationStep;
            }

            if (em.HasComponent<HexRiverCell>(cellEntity))
                em.RemoveComponent<HexRiverCell>(cellEntity);
            if (em.HasComponent<HexLakeCell>(cellEntity))
                em.RemoveComponent<HexLakeCell>(cellEntity);
        }

        /// <summary>清除该格的路；路径断成多段 → 拆成连续段各自成路（长度 1 的孤段删）</summary>
        public static void RemoveRoadAt(HexFeatureState state, EntityManager em, Entity cellEntity, int2 offset)
        {
            for (int r = state.Roads.Count - 1; r >= 0; r--)
            {
                var road = state.Roads[r];
                int idx = road.Cells.IndexOf(offset);
                if (idx < 0)
                    continue;

                road.Cells.RemoveAt(idx);
                road.Elevations.RemoveAt(idx);

                // 连续性拆分：相邻格 HexDistance==1 视为连续
                var segments = SplitContiguous(road.Cells, road.Elevations);
                state.Roads.RemoveAt(r);
                foreach (var seg in segments)
                {
                    if (seg.cells.Count < 2)
                        continue;   // 孤格路无缎带意义
                    state.Roads.Add(new RoadPath
                    {
                        RoadId = NextRoadId(state),
                        FromPoi = road.FromPoi,
                        ToPoi = road.ToPoi,
                        Cells = seg.cells,
                        Elevations = seg.elevations,
                    });
                }
            }

            if (em.HasComponent<HexRoadCell>(cellEntity))
                em.RemoveComponent<HexRoadCell>(cellEntity);
        }

        // ── 特征网格实时刷新（编辑系统帧末调用，脏标志节流）──────

        /// <summary>
        /// 全清重建水+路网格（照抄 HexWorldSaveService.Apply ⑤）：
        /// 销毁 FeatureMeshEntities（Destroy mesh+entity，不动植被）→ 新快照 →
        /// 距离图（植被 margin 用）→ BuildFeatureMeshes。地图仅数百格，全量成本可忽略。
        /// </summary>
        public static void RefreshFeatureMeshes(World world, EntityManager em, HexFeatureState state,
            HexFeatureConfig featureConfig, NativeHashMap<int2, Entity> lookup, ref HexMapConfigBlob blob)
        {
            // 先取托管 MeshReference 里的 Mesh 再销毁实体（防泄漏，同 ResetFeatures）
            foreach (var e in state.FeatureMeshEntities)
            {
                if (!em.Exists(e))
                    continue;
                if (em.HasComponent<MeshReference>(e))
                {
                    var mesh = em.GetComponentData<MeshReference>(e).Mesh;
                    if (mesh != null)
                        Object.Destroy(mesh);
                }
                em.DestroyEntity(e);
            }
            state.FeatureMeshEntities.Clear();

            var snap = HexFeatureSnapshot.Build(em, lookup, ref blob);
            HexRiverGenerator.BuildRiverDistanceMap(snap, state);
            HexRoadGenerator.BuildRoadDistanceMap(snap, state);
            HexFeatureCommitUtil.BuildFeatureMeshes(world, em, snap, state, featureConfig, ref blob);
        }

        // ── 内部 ────────────────────────────────────────────────

        private static void SetOrAdd<T>(EntityManager em, Entity e, T data) where T : unmanaged, IComponentData
        {
            if (em.HasComponent<T>(e))
                em.SetComponentData(e, data);
            else
                em.AddComponentData(e, data);
        }

        private static int NextRiverId(HexFeatureState state)
        {
            int id = 0;
            foreach (var r in state.Rivers)
                id = math.max(id, r.RiverId + 1);
            return id;
        }

        private static int NextLakeId(HexFeatureState state)
        {
            int id = 0;
            foreach (var l in state.Lakes)
                id = math.max(id, l.LakeId + 1);
            return id;
        }

        private static int NextRoadId(HexFeatureState state)
        {
            int id = 0;
            foreach (var r in state.Roads)
                id = math.max(id, r.RoadId + 1);
            return id;
        }

        /// <summary>把格从河列表成员剥离（空河整条删；except 保留——并入本河时排除自己）</summary>
        private static void RemoveRiverMembership(HexFeatureState state, int2 offset, RiverPath except)
        {
            for (int r = state.Rivers.Count - 1; r >= 0; r--)
            {
                var river = state.Rivers[r];
                if (river == except)
                    continue;
                int idx = river.Cells.IndexOf(offset);
                if (idx >= 0)
                {
                    river.Cells.RemoveAt(idx);
                    river.WaterY.RemoveAt(idx);
                }
                if (river.Cells.Count == 0)
                    state.Rivers.RemoveAt(r);
            }
        }

        /// <summary>把格从湖列表成员剥离（空湖整条删）</summary>
        private static void RemoveLakeMembership(HexFeatureState state, int2 offset, LakeData except = null)
        {
            for (int l = state.Lakes.Count - 1; l >= 0; l--)
            {
                var lake = state.Lakes[l];
                if (lake == except)
                    continue;
                if (lake.Cells.Remove(offset) && lake.Cells.Count == 0)
                    state.Lakes.RemoveAt(l);
            }
        }

        /// <summary>按相邻连续性把 cell 序列拆段（同下标 Elevations 跟随）</summary>
        private static List<(List<int2> cells, List<int> elevations)> SplitContiguous(
            List<int2> cells, List<int> elevations)
        {
            var result = new List<(List<int2>, List<int>)>();
            if (cells.Count == 0)
                return result;

            var curCells = new List<int2> { cells[0] };
            var curElev = new List<int> { elevations[0] };
            for (int i = 1; i < cells.Count; i++)
            {
                if (HexMapTerrainMath.HexDistance(cells[i - 1], cells[i]) == 1)
                {
                    curCells.Add(cells[i]);
                    curElev.Add(elevations[i]);
                }
                else
                {
                    result.Add((curCells, curElev));
                    curCells = new List<int2> { cells[i] };
                    curElev = new List<int> { elevations[i] };
                }
            }
            result.Add((curCells, curElev));
            return result;
        }
    }
}
