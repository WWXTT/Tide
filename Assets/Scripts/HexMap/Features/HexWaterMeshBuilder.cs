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
    /// 河流/湖泊水面网格构建（计划 4.5）：
    /// 每条河/每个湖一个合并 Mesh + 一个渲染实体（HexWaterBody + MeshReference + 渲染注册，
    /// 注册模式照抄 HexMeshWriteSystem.RebuildCellMesh：RegisterMesh/RegisterMaterial →
    /// RenderMeshUtility.AddComponents → LocalToWorld 单位阵 → RenderBounds）。
    ///
    /// 几何：
    /// - 顶面 = 逐水格名义六边形（含中心扇形）抬到该格 WaterY；角点经 PerturbOffset
    ///   扰动后与地形边逐点重合（同 HexMeshJob.Perturb 纯函数）。
    /// - 落差裙边：同水体相邻格水面差 &gt; ε → 高侧向低侧发竖直四边形（瀑布阶梯面），
    ///   由高侧单侧发面（天然去重）。
    /// - 岸裙边：非水邻居板面低于水面 → 向下裙边到岸板 y；图外边界裙到本格板面。
    /// - 透明材质 ZWrite Off → 内部共边重复面无碍。
    /// 顶点流仅需 Position/Normal/UV0（UV0 = 世界 xz，shader 波纹用）。
    /// </summary>
    public static class HexWaterMeshBuilder
    {
        private const float SkirtEpsilon = 0.02f;

        public static void Build(HexFeatureSnapshot snap, HexFeatureState state,
            ref HexMapConfigBlob blob, Material waterMaterial,
            EntityManager em, EntitiesGraphicsSystem egs)
        {
            int bodyId = 0;

            foreach (var river in state.Rivers)
            {
                var cells = new Dictionary<int2, float>();
                for (int i = 0; i < river.Cells.Count; i++)
                    cells[river.Cells[i]] = river.WaterY[i];
                if (river.BankCells != null)
                {
                    for (int i = 0; i < river.BankCells.Count; i++)
                        if (!cells.ContainsKey(river.BankCells[i]))
                            cells[river.BankCells[i]] = river.BankWaterY[i];
                }
                BuildBody(snap, cells, bodyId++, state, ref blob, waterMaterial, em, egs);
            }

            foreach (var lake in state.Lakes)
            {
                var cells = new Dictionary<int2, float>();
                foreach (var c in lake.Cells)
                    cells[c] = lake.WaterY;
                BuildBody(snap, cells, bodyId++, state, ref blob, waterMaterial, em, egs);
            }
        }

        private static void BuildBody(HexFeatureSnapshot snap, Dictionary<int2, float> cells, int bodyId,
            HexFeatureState state, ref HexMapConfigBlob blob, Material material,
            EntityManager em, EntitiesGraphicsSystem egs)
        {
            var metrics = HexMetrics.FromBlob(ref blob);
            var positions = new List<Vector3>(cells.Count * 8);
            var normals = new List<Vector3>(cells.Count * 8);
            var uvs = new List<Vector2>(cells.Count * 8);
            var indices = new List<int>(cells.Count * 18);
            var up = new Vector3(0f, 1f, 0f);

            foreach (var kv in cells)
            {
                var cell = kv.Key;
                float wy = kv.Value;
                float2 c = snap.Center[cell.x, cell.y];
                float2 cp = c + HexTerrainHeightSampler.PerturbOffset(c, ref blob);
                int centerIdx = Vert(positions, normals, uvs, new float3(cp.x, wy, cp.y), up);

                // 6 角点（名义六边形 + 同款扰动 → 与地形边逐点重合）
                var cornerIdx = new int[6];
                var cornerPos = new float2[6];
                for (int k = 0; k < 6; k++)
                {
                    float2 cor = c + metrics.Corners[k].xz;
                    cornerPos[k] = cor + HexTerrainHeightSampler.PerturbOffset(cor, ref blob);
                    cornerIdx[k] = Vert(positions, normals, uvs,
                        new float3(cornerPos[k].x, wy, cornerPos[k].y), up);
                }

                // 顶面扇形
                for (int k = 0; k < 6; k++)
                {
                    indices.Add(centerIdx);
                    indices.Add(cornerIdx[k]);
                    indices.Add(cornerIdx[(k + 1) % 6]);
                }

                // 裙边（逐边）
                for (int d = 0; d < 6; d++)
                {
                    float2 e1 = cornerPos[d];
                    float2 e2 = cornerPos[(d + 1) % 6];
                    float3 n = metrics.GetEdgeNormal((HexDirection)d);

                    if (cells.TryGetValue(HexBoundary.NeighborOffset(cell, (HexDirection)d), out float nwy))
                    {
                        // 同水体邻居：高侧单侧发落差裙边（瀑布）
                        if (wy > nwy + SkirtEpsilon)
                            AddSkirt(positions, normals, uvs, indices, e1, e2, wy, nwy, n);
                    }
                    else
                    {
                        var nOff = HexBoundary.NeighborOffset(cell, (HexDirection)d);
                        float plateY = snap.InBounds(nOff)
                            ? snap.PlateY(nOff, snap.GetElev(nOff))   // 岸裙边：补到岸板面
                            : snap.PlateY(cell, snap.GetElev(cell));  // 图外边界：裙到本格板面
                        if (wy > plateY + SkirtEpsilon)
                            AddSkirt(positions, normals, uvs, indices, e1, e2, wy, plateY, n);
                    }
                }
            }

            if (indices.Count == 0)
                return;

            var mesh = new Mesh { name = $"HexWater_{bodyId}" };
            mesh.SetVertices(positions);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds();

            var entity = em.CreateEntity(typeof(HexWaterBody), typeof(MeshReference));
            em.SetComponentData(entity, new HexWaterBody { BodyId = bodyId });
            em.SetComponentData(entity, new MeshReference { Mesh = mesh });

            var meshID = egs.RegisterMesh(mesh);
            var matID = egs.RegisterMaterial(material);
            var desc = new RenderMeshDescription(ShadowCastingMode.Off, receiveShadows: false);
            RenderMeshUtility.AddComponents(entity, em, desc, new MaterialMeshInfo(matID, meshID));

            // AddComponents 后可能残留零矩阵（HexMeshWriteSystem 同坑），强制单位阵
            em.SetComponentData(entity, new LocalToWorld { Value = float4x4.identity });

            var b = mesh.bounds;
            em.SetComponentData(entity, new RenderBounds
            {
                Value = new AABB
                {
                    Center = new float3(b.center.x, b.center.y, b.center.z),
                    Extents = new float3(b.extents.x, b.extents.y, b.extents.z),
                },
            });

            state.FeatureMeshEntities.Add(entity);
        }

        private static int Vert(List<Vector3> positions, List<Vector3> normals, List<Vector2> uvs,
            float3 p, float3 n)
        {
            positions.Add(new Vector3(p.x, p.y, p.z));
            normals.Add(new Vector3(n.x, n.y, n.z));
            uvs.Add(new Vector2(p.x, p.z));
            return positions.Count - 1;
        }

        /// <summary>竖直裙边四边形：e1/e2 为扰动后边端点（xz），从 topY 落到 bottomY</summary>
        private static void AddSkirt(List<Vector3> positions, List<Vector3> normals, List<Vector2> uvs,
            List<int> indices, float2 e1, float2 e2, float topY, float bottomY, float3 edgeNormal)
        {
            int i1 = Vert(positions, normals, uvs, new float3(e1.x, topY, e1.y), edgeNormal);
            int i2 = Vert(positions, normals, uvs, new float3(e2.x, topY, e2.y), edgeNormal);
            int i3 = Vert(positions, normals, uvs, new float3(e2.x, bottomY, e2.y), edgeNormal);
            int i4 = Vert(positions, normals, uvs, new float3(e1.x, bottomY, e1.y), edgeNormal);
            indices.Add(i1);
            indices.Add(i2);
            indices.Add(i3);
            indices.Add(i1);
            indices.Add(i3);
            indices.Add(i4);
        }
    }
}
