using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace HexMap
{
    /// <summary>
    /// 道路缎带网格构建（计划 5.4）：每条路一个渲染实体（HexRoadMesh + MeshReference + 注册，
    /// 模式同 HexWaterMeshBuilder / HexMeshWriteSystem）。
    ///
    /// 中心线 = 路径各格中心连折线（共线段合并）→ 按 sampleStep 细分采样；
    /// 每采样点：切向（段内常量，顶点处角平分 miter，转角 >150° 插双点防尖刺），
    /// 左右展开 roadHalfWidth；y = 采样器地表 y + 0.08（贴地跨坡），
    /// 河床/湖格修正 y = max(y, 水面 + 0.1)（简化桥）。
    /// 后生成道路整平改了先前路的格 → 先前缎带 y 采样时实算，自动贴新地形。
    /// </summary>
    public static class HexRoadMeshBuilder
    {
        private const float LiftY = 0.08f;
        private const float BridgeClearance = 0.1f;
        private const float CollinearEpsilon = 1e-3f;

        public static void Build(HexFeatureSnapshot snap, HexFeatureState state,
            ref HexMapConfigBlob blob, float roadHalfWidth, float sampleStep, float uvScale,
            Material roadMaterial, EntityManager em, EntitiesGraphicsSystem egs)
        {
            var metrics = HexMetrics.FromBlob(ref blob);
            float halfWidth = roadHalfWidth > 0f ? roadHalfWidth : metrics.InnerRadius * 0.5f;
            var grid = new HexCellDataSource { Em = em, Lookup = default };
            // 采样器需要已提交的 ECS cell 数据；网格构建发生在 CommitSnapshot 之后
            var streaming = em.World.GetExistingSystemManaged<HexChunkStreamingSystem>();
            if (streaming != null)
                grid.Lookup = streaming.CellLookup;

            foreach (var road in state.Roads)
                BuildRibbon(snap, road, state, ref blob, in metrics, grid, halfWidth,
                    math.max(0.25f, sampleStep), uvScale, roadMaterial, em, egs);
        }

        private static void BuildRibbon(HexFeatureSnapshot snap, RoadPath road, HexFeatureState state,
            ref HexMapConfigBlob blob, in HexMetrics metrics, HexCellDataSource grid, float halfWidth,
            float sampleStep, float uvScale, Material material, EntityManager em, EntitiesGraphicsSystem egs)
        {
            // ── 中心线（共线合并）──
            var polyline = new List<float2> { snap.Center[road.Cells[0].x, road.Cells[0].y] };
            for (int i = 1; i < road.Cells.Count; i++)
            {
                var c = snap.Center[road.Cells[i].x, road.Cells[i].y];
                var prev = polyline[^1];
                if (i < road.Cells.Count - 1 &&
                    math.distancesq(c, prev) < 1e-6f)
                    continue;
                // 与上一段共线 → 替换末点（吸收中间点）
                if (polyline.Count >= 2)
                {
                    var a = polyline[^2];
                    float2 d1 = prev - a;
                    float2 d2 = c - prev;
                    float l1 = math.length(d1), l2 = math.length(d2);
                    if (l1 > 1e-4f && l2 > 1e-4f)
                    {
                        float2 u1 = d1 / l1, u2 = d2 / l2;
                        if (math.abs(u1.x * u2.y - u1.y * u2.x) < CollinearEpsilon &&
                            math.dot(u1, u2) > 0f)
                        {
                            polyline[^1] = c;
                            continue;
                        }
                    }
                }
                polyline.Add(c);
            }
            if (polyline.Count < 2)
                return;

            // ── 细分采样点 + 逐点切向（角平分 miter / 尖角双点）──
            var points = new List<float2>();
            var tangents = new List<float2>();
            for (int i = 0; i < polyline.Count - 1; i++)
            {
                float2 a = polyline[i];
                float2 b = polyline[i + 1];
                float2 seg = b - a;
                float len = math.length(seg);
                if (len < 1e-4f)
                    continue;
                float2 t = seg / len;
                int steps = math.max(1, (int)math.ceil(len / sampleStep));

                if (i == 0)
                {
                    points.Add(a);
                    tangents.Add(t);
                }
                for (int s = 1; s <= steps; s++)
                {
                    points.Add(math.lerp(a, b, (float)s / steps));
                    tangents.Add(t);
                }

                // 段末点切向与下一段融合（角平分 miter；末段直接沿用）。
                // 已知限制：转角 >150° 的尖角未插双点（hex 网格路径转角最大 120°，实际不触发）
                if (i < polyline.Count - 2)
                {
                    float2 next = polyline[i + 2] - b;
                    float nl = math.length(next);
                    if (nl > 1e-4f)
                    {
                        float2 t2 = next / nl;
                        if (math.dot(t, t2) >= math.cos(math.radians(150f)))
                        {
                            float2 m = math.normalizesafe(t + t2, t);
                            tangents[^1] = m;
                        }
                    }
                }
            }

            // ── 生成左右顶点带 ──
            var positions = new List<Vector3>(points.Count * 2);
            var normals = new List<Vector3>(points.Count * 2);
            var uvs = new List<Vector2>(points.Count * 2);
            var leftIdx = new int[points.Count];
            var rightIdx = new int[points.Count];

            float arc = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                float2 p = points[i];
                float2 perp = new float2(-tangents[i].y, tangents[i].x);
                float3 left = new float3(p.x + perp.x * halfWidth, 0f, p.y + perp.y * halfWidth);
                float3 right = new float3(p.x - perp.x * halfWidth, 0f, p.y - perp.y * halfWidth);

                if (i > 0)
                    arc += math.distance(points[i - 1], p);

                float yl = SampleY(snap, grid, in metrics, left, ref blob, out var nl);
                leftIdx[i] = AddVert(positions, normals, uvs, new float3(left.x, yl, left.z), nl, arc * uvScale, 0f);

                float yr = SampleY(snap, grid, in metrics, right, ref blob, out var nr);
                rightIdx[i] = AddVert(positions, normals, uvs, new float3(right.x, yr, right.z), nr, arc * uvScale, 1f);
            }

            var indices = new List<int>((points.Count - 1) * 6);
            for (int i = 0; i < points.Count - 1; i++)
            {
                indices.Add(leftIdx[i]);
                indices.Add(leftIdx[i + 1]);
                indices.Add(rightIdx[i + 1]);
                indices.Add(leftIdx[i]);
                indices.Add(rightIdx[i + 1]);
                indices.Add(rightIdx[i]);
            }

            var mesh = new Mesh { name = $"HexRoad_{road.RoadId}" };
            mesh.SetVertices(positions);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds();

            var entity = em.CreateEntity(typeof(HexRoadMesh), typeof(MeshReference));
            em.SetComponentData(entity, new HexRoadMesh { RoadId = road.RoadId });
            em.SetComponentData(entity, new MeshReference { Mesh = mesh });

            var meshID = egs.RegisterMesh(mesh);
            var matID = egs.RegisterMaterial(material);
            var desc = new RenderMeshDescription(ShadowCastingMode.On, receiveShadows: true);
            RenderMeshUtility.AddComponents(entity, em, desc, new MaterialMeshInfo(matID, meshID));
            em.SetComponentData(entity, new LocalToWorld { Value = float4x4.identity });

            var mBounds = mesh.bounds;
            em.SetComponentData(entity, new RenderBounds
            {
                Value = new AABB
                {
                    Center = new float3(mBounds.center.x, mBounds.center.y, mBounds.center.z),
                    Extents = new float3(mBounds.extents.x, mBounds.extents.y, mBounds.extents.z),
                },
            });

            state.FeatureMeshEntities.Add(entity);
        }

        /// <summary>采样点 y 与法线（一次采样调用）：地表 + 抬升；河床/湖格 → 桥面抬到水面之上</summary>
        private static float SampleY(HexFeatureSnapshot snap, HexCellDataSource grid, in HexMetrics metrics,
            float3 p, ref HexMapConfigBlob blob, out float3 normal)
        {
            float y = HexTerrainHeightSampler.WorldHeight(p, in metrics, ref blob, grid, out normal) + LiftY;

            // 水格修正（名义归属即可，无需逆扰动精度）
            var off = HexCoordinates.FromPosition(new float3(p.x, 0f, p.z), in metrics).ToOffsetCoordinates();
            if (snap.InBounds(off))
            {
                float water = snap.RiverWaterY[off.x, off.y];
                if (float.IsNaN(water))
                    water = snap.LakeWaterY[off.x, off.y];
                if (!float.IsNaN(water))
                    y = math.max(y, water + BridgeClearance);
            }
            return y;
        }

        private static int AddVert(List<Vector3> positions, List<Vector3> normals, List<Vector2> uvs,
            float3 p, float3 normal, float u, float v)
        {
            positions.Add(new Vector3(p.x, p.y, p.z));
            normals.Add(new Vector3(normal.x, normal.y, normal.z));
            uvs.Add(new Vector2(u, v));
            return positions.Count - 1;
        }
    }
}
