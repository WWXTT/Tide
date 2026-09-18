using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace HexMap.EditorTools
{
    /// <summary>
    /// 地形网格水密性验证（Tools/网格/验证HexMap网格）：
    /// 收集全部 cell mesh + 轮廓 Cap，按量化位置建有向边表做流形检查——
    /// 封闭水密 ⇨ 每条有向边恰好出现一次、且其反向边恰好出现一次。
    /// 无反向边的有向边 = 洞/开边缘；重复有向边 = 重叠面。
    /// 附带 NaN 顶点与重合顶点法线偏差统计（rim 融合的对称性检查）。
    /// 要求：Play 模式下网格已生成。
    /// </summary>
    public static class HexMeshVerifier
    {
        private const float Quant = 10000f;   // 1e-4 量化
        private const float NormalAngleTolerance = 2f; // 重合顶点两侧法线最大夹角（度）

        [MenuItem("Tools/验证HexMap网格")]
        public static void Verify()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                Debug.LogError("[验证] ECS World 不存在（需 Play 模式下运行）");
                return;
            }

            var em = world.EntityManager;
            using var query = em.CreateEntityQuery(typeof(MeshReference));
            var refs = query.ToComponentArray<MeshReference>();
            if (refs.Length == 0)
            {
                Debug.LogWarning("[验证] 没有任何已生成网格（先让地图加载）");
                return;
            }

            var edgeCount = new Dictionary<(long, long), int>(); // 有向边 → 出现次数
            var keyPos = new Dictionary<long, Vector3>();   // 量化键 → 代表位置（日志用）
            var keyNormal = new Dictionary<long, float3>(); // 量化键 → 首见法线
            var normalDiverged = new List<(Vector3 pos, float angle)>();
            int totalVerts = 0, totalTris = 0, nanVerts = 0, degenerate = 0;

            foreach (var r in refs)
            {
                var mesh = r.Mesh;
                if (mesh == null) continue;
                var verts = mesh.vertices;
                var tris = mesh.triangles;
                var normals = mesh.normals;
                totalVerts += verts.Length;
                totalTris += tris.Length / 3;

                for (int i = 0; i < verts.Length; i++)
                {
                    var v = verts[i];
                    if (float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z))
                    {
                        nanVerts++;
                        continue;
                    }
                    long key = PosKey(v);
                    keyPos.TryAdd(key, v);
                    var n = normals[i];
                    if (keyNormal.TryGetValue(key, out var n0))
                    {
                        float angle = math.degrees(math.acos(math.saturate(
                            math.dot(math.normalize(n0), math.normalize(n)))));
                        if (angle > NormalAngleTolerance && normalDiverged.Count < 8)
                            normalDiverged.Add((v, angle));
                    }
                    else
                    {
                        keyNormal[key] = n;
                    }
                }

                for (int t = 0; t < tris.Length; t += 3)
                {
                    long a = PosKey(verts[tris[t]]);
                    long b = PosKey(verts[tris[t + 1]]);
                    long c = PosKey(verts[tris[t + 2]]);
                    if (a == b || b == c || a == c)
                    {
                        degenerate++;
                        continue;
                    }
                    AddEdge(edgeCount, a, b);
                    AddEdge(edgeCount, b, c);
                    AddEdge(edgeCount, c, a);
                }
            }

            // 合并共线同向链：细分接缝（如 5 顶点端列 vs 单段三角形边）位置共线、
            // 视觉无洞，但子边找不到反向边 → 先把链折叠回单段再判流形。
            MergeCollinearChains(edgeCount, keyPos);

            int boundary = 0, duplicate = 0;
            var boundarySamples = new List<(long, long)>();
            foreach (var kv in edgeCount)
            {
                var rev = (kv.Key.Item2, kv.Key.Item1);
                edgeCount.TryGetValue(rev, out int revCount);
                if (kv.Value > 1) duplicate++;
                if (revCount == 0)
                {
                    boundary++;
                    if (boundarySamples.Count < 8) boundarySamples.Add(kv.Key);
                }
            }

            Debug.Log($"[验证] mesh={refs.Length} 顶点={totalVerts} 三角={totalTris} | " +
                      $"边界边(洞)={boundary} 重复边={duplicate} 退化三角={degenerate} NaN顶点={nanVerts} | " +
                      $"法线分歧>{NormalAngleTolerance}°={normalDiverged.Count}");

            if (boundary == 0 && duplicate == 0 && nanVerts == 0)
                Debug.Log("[验证] ✓ 水密（封闭流形）");
            else
            {
                Debug.LogError($"[验证] ✗ 存在缺陷：洞={boundary} 重叠={duplicate} NaN={nanVerts}");
                foreach (var e in boundarySamples)
                {
                    string s1 = keyPos.TryGetValue(e.Item1, out var p1) ? p1.ToString("F3") : e.Item1.ToString();
                    string s2 = keyPos.TryGetValue(e.Item2, out var p2) ? p2.ToString("F3") : e.Item2.ToString();
                    Debug.Log($"  边界边: {s1} → {s2}");
                }
                foreach (var nd in normalDiverged)
                    Debug.Log($"  法线分歧 @ {nd.pos:F3}: {nd.angle:F1}°");
            }
        }

        /// <summary>
        /// 反复合并共线同向相邻边 (a→b)+(b→c ⇒ a→c)。量化键相邻即端点重合，
        /// 共线判定用原始坐标（keyPos）做叉积/点积。折叠细分接缝的拓扑假阳性。
        /// </summary>
        private static void MergeCollinearChains(Dictionary<(long, long), int> edges,
            Dictionary<long, Vector3> keyPos)
        {
            for (int pass = 0; pass < 16; pass++)
            {
                bool changed = false;
                var startsAt = new Dictionary<long, List<(long, long)>>();
                foreach (var kv in edges)
                {
                    if (!startsAt.TryGetValue(kv.Key.Item1, out var list))
                    {
                        list = new List<(long, long)>(2);
                        startsAt[kv.Key.Item1] = list;
                    }
                    list.Add(kv.Key);
                }

                var snapshot = edges.Keys.ToList();
                foreach (var key in snapshot)
                {
                    edges.TryGetValue(key, out int c1);
                    if (c1 <= 0) continue; // 已被合并的死壳
                    long a = key.Item1, b = key.Item2;
                    if (!startsAt.TryGetValue(b, out var nexts))
                        continue;
                    foreach (var nb in nexts)
                    {
                        edges.TryGetValue(nb, out int c2);
                        if (c2 <= 0) continue;
                        long c = nb.Item2;
                        if (c == a) continue;
                        if (!keyPos.TryGetValue(a, out var pa) ||
                            !keyPos.TryGetValue(b, out var pb) ||
                            !keyPos.TryGetValue(c, out var pc))
                            continue;
                        Vector3 d1 = pb - pa, d2 = pc - pb;
                        if (Vector3.Cross(d1, d2).sqrMagnitude > 1e-8f || Vector3.Dot(d1, d2) <= 0f)
                            continue; // 不共线或反向

                        edges[key] = 0;
                        edges[nb] = 0;
                        var merged = (a, c);
                        edges.TryGetValue(merged, out int cm);
                        edges[merged] = cm + c1 + c2;
                        changed = true;
                        break;
                    }
                }

                foreach (var k in edges.Where(e => e.Value == 0).Select(e => e.Key).ToList())
                    edges.Remove(k);
                if (!changed) break;
            }
        }

        private static void AddEdge(Dictionary<(long, long), int> map, long a, long b)
        {
            var k = (a, b);
            map.TryGetValue(k, out int c);
            map[k] = c + 1;
        }

        private static long PosKey(Vector3 v)
        {
            long x = (long)math.round(v.x * Quant);
            long y = (long)math.round(v.y * Quant);
            long z = (long)math.round(v.z * Quant);
            // 位置范围有限（±2e5 量化值 ≈ 18bit），三段互不重叠
            return ((x & 0x3FFFF) << 36) | ((y & 0x3FFFF) << 18) | (z & 0x3FFFF);
        }
    }
}
