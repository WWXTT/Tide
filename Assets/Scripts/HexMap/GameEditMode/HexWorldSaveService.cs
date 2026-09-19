using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HexMap
{
    /// <summary>
    /// 存档采集 / 恢复核心（主线程静态；面板协程在「全图加载 + TerrainPending 清零」后调用）。
    ///
    /// Apply 为同步方法、单帧内完成（协程一个 yield 步内原子执行，系统组观察不到半状态）：
    /// ResetFeatures(重置到噪声基线) → 逐格恢复高程/地块 → POI/特征从 DTO 转回 →
    /// 重建快照与距离图 → 水面/道路网格复现 → 植被重散布标记。
    /// 特征标签（HexRiverCell 等）不手动写：HexFeatureOverlaySystem 每帧幂等补挂，1-2 帧内自动齐全。
    /// </summary>
    public static class HexWorldSaveService
    {
        public const int SaveVersion = 1;

        // ── 采集 ────────────────────────────────────────────────────

        /// <summary>
        /// 采集当前世界。前置：全图已加载（CellLookup.Count == X*Z）。
        /// 返回 null = 世界未就绪 / 特征系统未安装。
        /// </summary>
        public static HexWorldSave Capture(World world)
        {
            var em = world.EntityManager;
            var streaming = world.GetExistingSystemManaged<HexChunkStreamingSystem>();
            if (streaming == null)
            {
                Debug.LogWarning("[HexWorld] 找不到 HexChunkStreamingSystem，无法采集");
                return null;
            }

            var configEntity = em.CreateEntityQuery(typeof(HexMapConfig)).GetSingletonEntity();
            var config = em.GetComponentData<HexMapConfig>(configEntity);
            ref var blob = ref config.Blob.Value;

            var featureQuery = em.CreateEntityQuery(typeof(HexFeatureConfig), typeof(HexFeatureState));
            if (featureQuery.IsEmpty)
            {
                Debug.LogWarning("[HexWorld] 特征系统未安装，无法采集");
                return null;
            }
            var featureEntity = featureQuery.GetSingletonEntity();
            var featureConfig = em.GetComponentData<HexFeatureConfig>(featureEntity);
            var state = em.GetComponentData<HexFeatureState>(featureEntity);

            int cx = blob.CellCount.x, cz = blob.CellCount.y;
            if (streaming.CellLookup.Count != cx * cz)
            {
                Debug.LogWarning($"[HexWorld] 未全图加载（{streaming.CellLookup.Count}/{cx * cz}），" +
                                 "无法采集——调用方应先 EnsureAllLoaded");
                return null;
            }

            var save = new HexWorldSave
            {
                version = SaveVersion,
                cellCountX = cx,
                cellCountZ = cz,
                settingsFingerprint = HexWorldFingerprint.Compute(featureConfig.Settings),
                name = "",                       // 由调用方（面板）填
                savedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                elevations = new int[cx * cz],
                terrainIndices = new int[cx * cz],
            };

            foreach (var kv in streaming.CellLookup)
            {
                var cell = em.GetComponentData<HexCellData>(kv.Value);
                int i = kv.Key.y * cx + kv.Key.x;
                save.elevations[i] = cell.Elevation;
                save.terrainIndices[i] = cell.TerrainIndex;
            }

            // POI（运行时表为权威源）
            var pois = new HexPoiDto[HexPoiRuntime.Pois.Count];
            for (int i = 0; i < pois.Length; i++)
                pois[i] = HexPoiDto.Of(HexPoiRuntime.Pois[i]);
            save.pois = pois;

            // 特征
            var rivers = new HexRiverDto[state.Rivers.Count];
            for (int i = 0; i < rivers.Length; i++)
                rivers[i] = RiverToDto(state.Rivers[i]);
            save.rivers = rivers;

            var lakes = new HexLakeDto[state.Lakes.Count];
            for (int i = 0; i < lakes.Length; i++)
                lakes[i] = LakeToDto(state.Lakes[i]);
            save.lakes = lakes;

            var roads = new HexRoadDto[state.Roads.Count];
            for (int i = 0; i < roads.Length; i++)
                roads[i] = RoadToDto(state.Roads[i]);
            save.roads = roads;

            return save;
        }

        // ── 恢复 ────────────────────────────────────────────────────

        /// <summary>
        /// 把存档恢复到世界（同步、单帧）。前置：全图已加载 + TerrainPending 清零 +
        /// 指纹已由调用方校验（不符仅警告，照常加载——高程全量以存档为准）。
        /// </summary>
        public static void Apply(World world, HexWorldSave save)
        {
            if (save == null || save.version != SaveVersion)
            {
                Debug.LogError($"[HexWorld] 存档为空或版本不符（期望 {SaveVersion}，读到 {save?.version ?? -1}），拒载");
                return;
            }

            var em = world.EntityManager;
            var streaming = world.GetExistingSystemManaged<HexChunkStreamingSystem>();
            var configEntity = em.CreateEntityQuery(typeof(HexMapConfig)).GetSingletonEntity();
            var config = em.GetComponentData<HexMapConfig>(configEntity);
            ref var blob = ref config.Blob.Value;

            if (save.cellCountX != blob.CellCount.x || save.cellCountZ != blob.CellCount.y)
            {
                Debug.LogError($"[HexWorld] 地图尺寸不符（存档 {save.cellCountX}×{save.cellCountZ}，" +
                               $"当前 {blob.CellCount.x}×{blob.CellCount.y}），拒载——地图尺寸在 settings 上改");
                return;
            }

            int cx = blob.CellCount.x, cz = blob.CellCount.y;
            if (streaming.CellLookup.Count != cx * cz)
            {
                Debug.LogError("[HexWorld] 未全图加载，拒载（调用方应先 EnsureAllLoaded + 等 pending 清零）");
                return;
            }

            var featureEntity = em.CreateEntityQuery(typeof(HexFeatureConfig), typeof(HexFeatureState))
                .GetSingletonEntity();
            var featureConfig = em.GetComponentData<HexFeatureConfig>(featureEntity);
            var state = em.GetComponentData<HexFeatureState>(featureEntity);

            // ① 清特征 + 高程/地块重置回噪声与分带（= 基线，同时销毁旧水/路网格与植被）
            HexFeatureCommitUtil.ResetFeatures(em, streaming.CellLookup, state, true, ref blob);

            // ② 记录噪声基线（ElevationOverrides 只收「存档 ≠ 噪声」的格，含手编格——
            //    比保存前的运行态更完备：现状手编格不进覆写表，卸载重建会回滚）
            var lookup = streaming.CellLookup;
            var baseline = new Dictionary<int2, int>(cx * cz);
            foreach (var kv in lookup)
                baseline[kv.Key] = em.GetComponentData<HexCellData>(kv.Value).Elevation;

            // ③ 逐格恢复高程 + 地块
            for (int z = 0; z < cz; z++)
            {
                for (int x = 0; x < cx; x++)
                {
                    var o = new int2(x, z);
                    int i = z * cx + x;
                    if (!lookup.TryGetValue(o, out var e))
                        continue;

                    int saved = save.elevations[i];
                    if (saved != baseline[o])
                    {
                        HexMapCellEditUtil.ApplyElevation(em, e, saved, ref blob);
                        state.ElevationOverrides[o] = saved;
                        state.OverlayApplied.Add(o);   // 防 overlay 下帧重复套用（幂等，保持口径一致）
                    }

                    HexMapCellEditUtil.ApplyTerrain(em, e, save.terrainIndices[i]);
                }
            }

            // ④ POI / 特征从 DTO 转回
            HexPoiRuntime.Pois.Clear();
            if (save.pois != null)
            {
                foreach (var p in save.pois)
                    HexPoiRuntime.Pois.Add(p.ToData());
            }

            state.Rivers.Clear();
            if (save.rivers != null)
            {
                foreach (var r in save.rivers)
                    state.Rivers.Add(RiverFromDto(r));
            }

            state.Lakes.Clear();
            if (save.lakes != null)
            {
                foreach (var l in save.lakes)
                    state.Lakes.Add(LakeFromDto(l));
            }

            state.Roads.Clear();
            if (save.roads != null)
            {
                foreach (var r in save.roads)
                    state.Roads.Add(RoadFromDto(r));
            }

            // ⑤ 新快照（Snap/Elev = 已恢复的高程）→ 距离图 → 水/路网格复现
            var snap = HexFeatureSnapshot.Build(em, lookup, ref blob);
            HexRiverGenerator.BuildRiverDistanceMap(snap, state);
            HexRoadGenerator.BuildRoadDistanceMap(snap, state);
            HexFeatureCommitUtil.BuildFeatureMeshes(world, em, snap, state, featureConfig, ref blob);

            // ⑥ 植被重散布 + 完成信号
            state.VegetationDirty = true;
            state.GenerationSerial++;

            Debug.Log($"[HexWorld] 存档已恢复：{save.name}（{cx}×{cz}，" +
                      $"河 {state.Rivers.Count}、湖 {state.Lakes.Count}、路 {state.Roads.Count}，" +
                      $"POI {HexPoiRuntime.Pois.Count}，覆写 {state.ElevationOverrides.Count} 格）");
        }

        // ── DTO ↔ 运行态转换 ───────────────────────────────────────

        private static HexCellCoordDto[] CellsToDto(List<int2> cells)
        {
            if (cells == null)
                return null;
            var a = new HexCellCoordDto[cells.Count];
            for (int i = 0; i < a.Length; i++)
                a[i] = HexCellCoordDto.Of(cells[i]);
            return a;
        }

        private static List<int2> CellsFromDto(HexCellCoordDto[] dto)
        {
            if (dto == null)
                return new List<int2>();
            var l = new List<int2>(dto.Length);
            foreach (var c in dto)
                l.Add(c.ToInt2());
            return l;
        }

        private static HexRiverDto RiverToDto(RiverPath r) => new HexRiverDto
        {
            riverId = r.RiverId,
            endKind = (int)r.End,
            cells = CellsToDto(r.Cells),
            waterY = r.WaterY?.ToArray(),
            bankCells = CellsToDto(r.BankCells),
            bankWaterY = r.BankWaterY?.ToArray(),
        };

        private static RiverPath RiverFromDto(HexRiverDto d) => new RiverPath
        {
            RiverId = d.riverId,
            End = (RiverEndKind)d.endKind,
            Cells = CellsFromDto(d.cells),
            WaterY = d.waterY == null ? new List<float>() : new List<float>(d.waterY),
            BankCells = d.bankCells == null ? null : CellsFromDto(d.bankCells),
            BankWaterY = d.bankWaterY == null ? new List<float>() : new List<float>(d.bankWaterY),
        };

        private static HexLakeDto LakeToDto(LakeData l) => new HexLakeDto
        {
            lakeId = l.LakeId,
            level = l.Level,
            waterY = l.WaterY,
            spillX = l.SpillCell.x,
            spillZ = l.SpillCell.y,
            cells = CellsToDto(l.Cells),
        };

        private static LakeData LakeFromDto(HexLakeDto d) => new LakeData
        {
            LakeId = d.lakeId,
            Level = d.level,
            WaterY = d.waterY,
            SpillCell = new int2(d.spillX, d.spillZ),
            Cells = CellsFromDto(d.cells),
        };

        private static HexRoadDto RoadToDto(RoadPath r) => new HexRoadDto
        {
            roadId = r.RoadId,
            fromPoi = r.FromPoi,
            toPoi = r.ToPoi,
            cells = CellsToDto(r.Cells),
            elevations = r.Elevations?.ToArray(),
        };

        private static RoadPath RoadFromDto(HexRoadDto d) => new RoadPath
        {
            RoadId = d.roadId,
            FromPoi = d.fromPoi,
            ToPoi = d.toPoi,
            Cells = CellsFromDto(d.cells),
            Elevations = d.elevations == null ? new List<int>() : new List<int>(d.elevations),
        };
    }
}
