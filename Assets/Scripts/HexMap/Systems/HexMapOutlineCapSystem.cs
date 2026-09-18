using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace HexMap
{
    /// <summary>
    /// 地图轮廓封底系统（替换旧矩形底面）：
    /// 在 bottomY 生成一张跟随地图实际外轮廓链（外圈 cell 边界边的名义角点链，
    /// 六边形锯齿形状）的整面 Cap。边界坡底边落在这条链上（名义角、不扰动），
    /// 与 Cap 逐点重合构成封闭体；Cap 顶点法线在 rim 处向相邻边界坡法线融合。
    ///
    /// 轮廓全部用标称几何（不查实体、不扰动——地图边缘扰动振幅恒 0），
    /// 因此轮廓与高度/地形编辑无关，仅在 CellCount 变化时重建。
    /// 三角化用耳切（锯齿轮廓在方向转折处存在凹齿，不保证凸）。
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(HexMeshWriteSystem))]
    public partial class HexMapOutlineCapSystem : SystemBase
    {
        private Entity _capEntity;
        private int2 _builtForCellCount = new int2(-1, -1);

        protected override void OnCreate()
        {
            RequireForUpdate<HexMapConfig>();
        }

        protected override void OnUpdate()
        {
            var configEntity = SystemAPI.GetSingletonEntity<HexMapConfig>();
            var config = SystemAPI.GetComponentRO<HexMapConfig>(configEntity).ValueRO;
            ref var blob = ref config.Blob.Value;

            if (math.all(_builtForCellCount == blob.CellCount))
                return; // 已为当前地图尺寸建过，无需重建

            if (_capEntity != Entity.Null && EntityManager.Exists(_capEntity))
                EntityManager.DestroyEntity(_capEntity);

            CreateCapMesh(config, ref blob);
            _builtForCellCount = blob.CellCount;
        }

        private void CreateCapMesh(HexMapConfig config, ref HexMapConfigBlob blob)
        {
            var metrics = HexMetrics.FromBlob(ref blob);
            float bottomY = HexMeshJob.GetBottomY(ref metrics);
            // 边界坡法线估计（Cap rim 融合用）：Cap 只知名义几何不知 cell 高度，
            // 取一个台阶作近似；边界格被抬高后该估计略偏，仅影响 Cap 棱线法线的柔和度
            float slopeDh = metrics.ElevationStep;

            // ---- 1) 枚举边界段：名义角点段 + 相邻坡法线 ----
            var pointIndex = new Dictionary<long, int>();
            var points = new List<float3>();
            var slopeNormals = new List<List<float3>>(); // 每顶点相邻边界坡法线（rim 融合用）
            var segments = new List<int2>();             // 有向段（起点索引 → 终点索引）

            for (int oz = 0; oz < blob.CellCount.y; oz++)
            for (int ox = 0; ox < blob.CellCount.x; ox++)
            {
                float3 center = CellCenter(ox, oz, ref metrics);
                var off = new int2(ox, oz);

                for (int d = 0; d < 6; d++)
                {
                    var nOff = HexBoundary.NeighborOffset(off, (HexDirection)d);
                    if (!HexBoundary.IsOutsideMap(nOff, blob.CellCount))
                        continue;

                    float3 c1 = center + metrics.Corners[d];
                    float3 c2 = center + metrics.Corners[d + 1];
                    c1.y = bottomY;
                    c2.y = bottomY;
                    float3 nSlope = HexMetrics.SlopeNormal(
                        metrics.GetEdgeNormal((HexDirection)d), slopeDh, metrics.SlopeInset);

                    int i1 = InternPoint(c1, pointIndex, points, slopeNormals);
                    int i2 = InternPoint(c2, pointIndex, points, slopeNormals);
                    slopeNormals[i1].Add(nSlope);
                    slopeNormals[i2].Add(nSlope);
                    segments.Add(new int2(i1, i2));
                }
            }

            // ---- 2) 缝合成单一闭环 ----
            var loop = StitchLoop(segments, points.Count);
            if (loop == null || loop.Count < 3)
            {
                Debug.LogWarning("[HexMap] 轮廓缝合失败，封底跳过");
                return;
            }

            // ---- 3) 统一俯视 CCW（signed area > 0），保证耳切输出法线朝 -Y ----
            float area = 0f;
            for (int i = 0; i < loop.Count; i++)
            {
                float3 a = points[loop[i]];
                float3 b = points[loop[(i + 1) % loop.Count]];
                area += a.x * b.z - b.x * a.z;
            }
            if (area < 0f)
                loop.Reverse();

            // ---- 4) 耳切三角化（含共线顶点剔除与迭代上限兜底）----
            var triangles = EarClip(loop, points);

            // ---- 5) 组装顶点流并注册渲染 ----
            float3 down = new float3(0f, -1f, 0f);
            float rimBlend = math.saturate(blob.RimNormalBlend);
            var variation = blob.VariationEnabled != 0
                ? new float4(1f, 0f, 0f, 0f) // Cap 权重恒 0（恒等变换即可）
                : new float4(1f, 0f, 0f, 0f);

            var vertices = new NativeArray<TerrainVertex>(points.Count, Allocator.Temp);
            for (int i = 0; i < points.Count; i++)
            {
                // rim 融合：down 向「down ⊕ 相邻坡法线之和」过渡
                float3 junction = down;
                foreach (var ns in slopeNormals[i])
                    junction += ns;
                junction = math.normalize(junction);
                float3 n = math.normalize(math.lerp(down, junction, rimBlend));

                vertices[i] = new TerrainVertex
                {
                    Position = points[i],
                    Normal = down,            // 纯法线：恒朝下（光照/阴影）
                    Tangent = n,              // rim 融合法线（三平面投影权重）
                    Color = new float4(1f, 0f, 0f, 0f), // splat 权重 (1,0,0)，变异权重 0
                    UV1 = new float3(0f, 0f, 0f),
                    UV2 = variation,
                };
            }

            var indices = new NativeArray<int>(triangles.Count, Allocator.Temp);
            for (int i = 0; i < triangles.Count; i++)
                indices[i] = triangles[i];

            var mesh = new Mesh { name = "HexMapOutlineCap" };
            mesh.SetVertexBufferParams(vertices.Length,
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, 4),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 4));
            mesh.SetVertexBufferData(vertices, 0, 0, vertices.Length);
            mesh.SetIndexBufferParams(indices.Length, IndexFormat.UInt32);
            mesh.SetIndexBufferData(indices, 0, 0, indices.Length);
            mesh.SetSubMesh(0, new SubMeshDescriptor(0, indices.Length));
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);
            vertices.Dispose();
            indices.Dispose();

            // 创建实体并注册到 entities.graphics（与 cell 网格同材质同布局）
            var em = EntityManager;
            _capEntity = em.CreateEntity();
            // 挂 MeshReference 供编辑期验证工具统一遍历
            em.AddComponentData(_capEntity, new MeshReference { Mesh = mesh });

            var egs = World.GetExistingSystemManaged<EntitiesGraphicsSystem>();
            if (egs != null)
            {
                var meshID = egs.RegisterMesh(mesh);
                var material = config.TerrainMaterial.Value;
                if (material == null)
                {
                    Debug.LogWarning("[HexMap] 封底材质为空，跳过渲染注册");
                    return;
                }
                var matID = egs.RegisterMaterial(material);

                var desc = new RenderMeshDescription(ShadowCastingMode.On, receiveShadows: true);
                RenderMeshUtility.AddComponents(_capEntity, em, desc, new MaterialMeshInfo(matID, meshID));
                em.SetComponentData(_capEntity, new LocalToWorld { Value = float4x4.identity });

                var bounds = mesh.bounds;
                em.SetComponentData(_capEntity, new RenderBounds
                {
                    Value = new Unity.Mathematics.AABB
                    {
                        Center = new float3(bounds.center.x, bounds.center.y, bounds.center.z),
                        Extents = new float3(bounds.extents.x, bounds.extents.y, bounds.extents.z),
                    }
                });
            }
            else
            {
                Debug.LogWarning("[HexMap] EntitiesGraphicsSystem 不存在，无法注册封底网格");
            }
        }

        /// <summary>标称 cell 中心（与 HexChunkStreamingSystem 同公式，不查实体）</summary>
        private static float3 CellCenter(int ox, int oz, ref HexMetrics metrics)
        {
            float oddShift = (oz & 1) * 0.5f;
            return new float3(
                (ox + oddShift) * metrics.InnerRadius * 2f,
                0f,
                oz * metrics.OuterRadius * 1.5f);
        }

        /// <summary>量化去重：同一名义角点由多个 cell 独立算出，值相同 → 合并</summary>
        private static int InternPoint(float3 p, Dictionary<long, int> index, List<float3> points,
            List<List<float3>> slopeNormals)
        {
            long qx = (long)math.round(p.x * 10000f);
            long qz = (long)math.round(p.z * 10000f);
            long key = qx * 4000000L + qz;
            if (index.TryGetValue(key, out int i))
                return i;
            i = points.Count;
            points.Add(p);
            slopeNormals.Add(new List<float3>(2));
            index[key] = i;
            return i;
        }

        /// <summary>沿邻接关系把段缝成单一闭环；每个顶点恰有入/出各一条（简单连通区域）</summary>
        private static List<int> StitchLoop(List<int2> segments, int pointCount)
        {
            var outEdges = new Dictionary<int, List<int>>(pointCount);
            for (int s = 0; s < segments.Count; s++)
            {
                if (!outEdges.TryGetValue(segments[s].x, out var list))
                {
                    list = new List<int>(2);
                    outEdges[segments[s].x] = list;
                }
                list.Add(s);
            }

            var used = new bool[segments.Count];
            var loop = new List<int>(pointCount);

            int start = 0;
            for (int s = 0; s < segments.Count; s++)
            {
                if (used[s]) continue;
                start = s;
                break;
            }

            int cur = start;
            int guard = 0;
            while (guard++ < segments.Count * 4)
            {
                used[cur] = true;
                var seg = segments[cur];
                loop.Add(seg.x);
                int nextPoint = seg.y;
                int next = -1;
                if (outEdges.TryGetValue(nextPoint, out var cands))
                {
                    for (int c = 0; c < cands.Count; c++)
                        if (!used[cands[c]]) { next = cands[c]; break; }
                }
                if (next < 0)
                    break; // 回到起点闭环
                cur = next;
            }

            // 有效性：段全用上且闭环（段数 = 顶点数）
            if (loop.Count != segments.Count)
                return null;
            return loop;
        }

        /// <summary>
        /// 耳切三角化（输入俯视 CCW 闭环 → 输出绕序一致的三角形，法线 -Y）。
        /// 共线顶点直接剔除不产三角；凹点/含内点跳过；带迭代上限兜底（超限改扇形）。
        /// </summary>
        private static List<int> EarClip(List<int> loop, List<float3> points)
        {
            var idx = new List<int>(loop);
            var tris = new List<int>((idx.Count - 2) * 3);
            int guard = 0;

            while (idx.Count > 3 && guard++ < idx.Count * idx.Count * 4)
            {
                bool clipped = false;
                int n = idx.Count;
                for (int i = 0; i < n; i++)
                {
                    int ia = idx[(i + n - 1) % n];
                    int ib = idx[i];
                    int ic = idx[(i + 1) % n];
                    float2 a = points[ia].xz;
                    float2 b = points[ib].xz;
                    float2 c = points[ic].xz;

                    float cross = Cross2(b - a, c - b);

                    if (math.abs(cross) < 1e-9f)
                    {
                        // 共线顶点：剔除不产三角
                        idx.RemoveAt(i);
                        clipped = true;
                        break;
                    }
                    if (cross <= 0f)
                        continue; // 凹点

                    if (ContainsAnyPoint(a, b, c, idx, ia, ib, ic, points))
                        continue; // 有其它顶点在内，不是耳

                    tris.Add(ia);
                    tris.Add(ib);
                    tris.Add(ic);
                    idx.RemoveAt(i);
                    clipped = true;
                    break;
                }

                if (!clipped)
                    break; // 找不到耳（理论不发生），交给兜底
            }

            if (idx.Count >= 3)
            {
                // 兜底：剩余环扇形三角化（保证封闭，可能有少量翻转三角但仅在病态轮廓出现）
                for (int i = 1; i + 1 < idx.Count; i++)
                {
                    tris.Add(idx[0]);
                    tris.Add(idx[i]);
                    tris.Add(idx[i + 1]);
                }
            }

            return tris;
        }

        private static float Cross2(float2 a, float2 b) => a.x * b.y - a.y * b.x;

        private static bool ContainsAnyPoint(float2 a, float2 b, float2 c,
            List<int> idx, int ia, int ib, int ic, List<float3> points)
        {
            for (int j = 0; j < idx.Count; j++)
            {
                int iv = idx[j];
                if (iv == ia || iv == ib || iv == ic)
                    continue;
                float2 p = points[iv].xz;
                // CCW 三角形：三边同侧为内
                if (Cross2(b - a, p - a) >= -1e-9f &&
                    Cross2(c - b, p - b) >= -1e-9f &&
                    Cross2(a - c, p - c) >= -1e-9f)
                    return true;
            }
            return false;
        }
    }
}
