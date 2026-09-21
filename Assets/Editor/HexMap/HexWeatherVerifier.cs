using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

namespace HexMap.EditorTools
{
    /// <summary>
    /// 天气/植被系统验证器（Play 模式下运行）：
    ///   1. 全局链路：场景有 TTFE Global Shaders Controller，且
    ///      Shader.GetGlobalFloat(_SeasonChangeGlobal 等) 与组件值一致
    ///      （控制器 → 全局变量 → HexTerrain/HexGrass 的契约链通）。
    ///   2. 植被散布：HexScatterInstance 实体存在且世界位置未塌原点
    ///      （BRG + DOTS instancing 正常）。
    ///   3. 排除断言（初始化自动排布的质量口径，逐实例核对）：
    ///      a. 地形层 ∈ 规则 density&gt;0 的层（只落合适地形）；
    ///      b. 距河/路 BFS 距离 ≥ 规则 margin（道路/河流无植被）；
    ///      c. 与 6 邻最大高差 ≤ 规则上限（阶梯带/壁顶缘无植被；
    ///         落点级坡带规避由散布的 0.75×IR 内缩盘保证）。
    /// </summary>
    public static class HexWeatherVerifier
    {
        private const float Epsilon = 1e-3f;

        [MenuItem("Tools/HexMap/验证天气系统")]
        public static void Verify()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                Debug.LogError("[验证] ECS World 不存在（需 Play 模式下运行）");
                return;
            }
            var em = world.EntityManager;

            int errors = 0;
            var report = new System.Text.StringBuilder("[天气验证]\n");

            // ── 1. 全局链路 ──────────────────────────────────────
            var controllers = Object.FindObjectsOfType<TobyFredson.TobyGlobalShadersController>(true);
            if (controllers.Length == 0)
            {
                Debug.LogError("[验证] 场景缺少 TTFE Global Shaders Controller（季节/雪/湿/风不会驱动任何 shader）");
                errors++;
            }
            else
            {
                var so = new SerializedObject(controllers[0]);
                errors += CheckGlobal(report, "_SeasonChangeGlobal", so.FindProperty("season").floatValue);
                errors += CheckGlobal(report, "_SnowAmount", so.FindProperty("snow").floatValue);
                errors += CheckGlobal(report, "_Wetness", so.FindProperty("wetness").floatValue);
                errors += CheckGlobal(report, "_GlobalWindStrength", so.FindProperty("windStrength").floatValue);
            }

            // ── 2/3. 植被散布与排除断言 ─────────────────────────
            var q = em.CreateEntityQuery(typeof(HexScatterInstance));
            var entities = q.ToEntityArray(Unity.Collections.Allocator.Temp);
            if (entities.Length == 0)
            {
                Debug.LogError("[验证] 无 HexScatterInstance（散布未跑：需 Play 且 scatterRules 非空）");
                errors++;
            }
            else
            {
                errors += VerifyScatter(world, em, entities.ToArray(), report);
            }
            entities.Dispose();

            report.Append(errors == 0 ? "全部通过" : $"失败 {errors} 项");
            if (errors == 0) Debug.Log(report.ToString());
            else Debug.LogError(report.ToString());
        }

        private static int CheckGlobal(System.Text.StringBuilder report, string name, float expected)
        {
            float actual = Shader.GetGlobalFloat(name);
            if (Mathf.Abs(actual - expected) > Epsilon)
            {
                report.Append($"  × 全局 {name} = {actual} ≠ 控制器 {expected}\n");
                return 1;
            }
            report.Append($"  √ 全局 {name} = {actual}\n");
            return 0;
        }

        private static int VerifyScatter(World world, EntityManager em, Entity[] entities, System.Text.StringBuilder report)
        {
            int errors = 0;

            // 特征状态（河/路 BFS 距离图）与配置
            var fq = em.CreateEntityQuery(typeof(HexFeatureConfig), typeof(HexFeatureState));
            var states = fq.ToComponentArray<HexFeatureState>();
            var featureConfigs = fq.ToComponentArray<HexFeatureConfig>();
            if (states.Length == 0)
            {
                report.Append("  × 无 HexFeatureState（特征生成未跑，河/路距离图缺失）\n");
                return 1;
            }
            var state = states[0];
            var settings = featureConfigs[0].Settings;
            var streaming = world.GetExistingSystemManaged<HexChunkStreamingSystem>();
            if (streaming == null || !streaming.CellLookup.IsCreated)
            {
                report.Append("  × 流式 CellLookup 未就绪\n");
                return 1;
            }
            var lookup = streaming.CellLookup;
            var grid = new HexCellDataSource { Em = em, Lookup = lookup };

            int originCollapse = 0, terrainBad = 0, riverBad = 0, roadBad = 0, slopeBad = 0, checkedCells = 0;
            var slopeChecked = new HashSet<int2>();

            foreach (var e in entities)
            {
                var cell = em.GetComponentData<HexScatterCell>(e);
                var protoIndex = em.GetSharedComponentManaged<HexScatterPrototype>(e).PrototypeIndex;
                var lt = em.GetComponentData<LocalTransform>(e);

                if (math.abs(lt.Position.x) < 0.5f && math.abs(lt.Position.z) < 0.5f)
                    originCollapse++;

                var rule = settings.scatterRules[protoIndex];
                if (!lookup.TryGetValue(cell.Offset, out var cellEntity))
                    continue;
                var cd = em.GetComponentData<HexCellData>(cellEntity);

                // a. 地形层允许
                if (cd.TerrainIndex < 0 || cd.TerrainIndex >= rule.densityPerTerrain.Count
                    || rule.densityPerTerrain[cd.TerrainIndex] <= 0f)
                {
                    terrainBad++;
                    if (terrainBad <= 3)
                        report.Append($"  × 地形越权：cell {cell.Offset} terrain={cd.TerrainIndex} 规则「{rule.name}」不允许\n");
                }

                // b. 河/路 margin
                if (state.RiverDist.Count > 0 && state.RiverDist.TryGetValue(cell.Offset, out int rd) && rd < rule.riverMarginCells)
                {
                    riverBad++;
                    if (riverBad <= 3)
                        report.Append($"  × 河流 margin：cell {cell.Offset} 距河 {rd} < {rule.riverMarginCells}（{rule.name}）\n");
                }
                if (state.RoadDist.Count > 0 && state.RoadDist.TryGetValue(cell.Offset, out int od) && od < rule.roadMarginCells)
                {
                    roadBad++;
                    if (roadBad <= 3)
                        report.Append($"  × 道路 margin：cell {cell.Offset} 距路 {od} < {rule.roadMarginCells}（{rule.name}）\n");
                }

                // c. 壁顶缘/阶梯带（逐格只查一次）
                if (slopeChecked.Add(cell.Offset))
                {
                    checkedCells++;
                    int maxDiff = 0;
                    for (int d = 0; d < 6; d++)
                        if (grid.TryGetNeighbor(cell.Offset, (HexDirection)d, out var nb))
                            maxDiff = Mathf.Max(maxDiff, Mathf.Abs(nb.Elevation - cd.Elevation));
                    if (maxDiff > rule.maxNeighborElevationDiff)
                    {
                        slopeBad++;
                        if (slopeBad <= 3)
                            report.Append($"  × 坡度过滤：cell {cell.Offset} 邻差 {maxDiff} > {rule.maxNeighborElevationDiff}（{rule.name}）\n");
                    }
                }
            }

            report.Append($"  植被实例 {entities.Length}，覆盖格 {checkedCells}\n");
            report.Append($"  塌原点 {originCollapse}，地形越权 {terrainBad}，河 margin 违例 {riverBad}，路 margin 违例 {roadBad}，坡度违例格 {slopeBad}\n");

            if (originCollapse > entities.Length / 20)
            {
                report.Append("  × 塌原点实例过多（BRG/DOTS instancing 未生效）\n");
                errors++;
            }
            if (terrainBad > 0) errors++;
            if (riverBad > 0) errors++;
            if (roadBad > 0) errors++;
            if (slopeBad > 0) errors++;
            return errors;
        }
    }
}
