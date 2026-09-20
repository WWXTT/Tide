using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace HexMap.EditorTools
{
    /// <summary>
    /// 收集全部 cell mesh + 轮廓 Cap，按量化位置建有向边表做流形检查——
    /// 封闭水密 ⇨ 每条有向边恰好出现一次、且其反向边恰好出现一次。
    /// 无反向边的有向边 = 洞/开边缘；重复有向边 = 重叠面。
    /// 附带 NaN 顶点统计。（重合顶点法线分歧检查已随 rim 融合退役——
    /// 垂直侧壁/阶梯的硬棱下，重合位置两侧法线 90° 是设计使然，必报噪声。）
    /// 要求：Play 模式下网格已生成。
    ///
    /// 位置键说明：量化 int 三元组直接做键（int32 ±21 亿量化值 = ±21 万世界单位），
    /// 不做位打包掩码——地图跨度数百单位，早期 18bit 掩码版本会回绕混叠出
    /// 幻影边界/重复边。
    /// 流式前沿（已加载格对未加载格的临时壁底边，无 Cap 封闭）单独归类：
    /// 最低一层的开口 = 前沿（预期），其余高度的开口 = 真洞。
    /// </summary>
    public static class HexMeshVerifier
    {
        private const float Quant = 10000f;   // 1e-4 量化

        [MenuItem("Tools/HexMap/验证HexMap网格")]
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

            var edgeCount = new Dictionary<(int, int, int, int, int, int), int>(); // 有向边 → 出现次数
            var keyPos = new Dictionary<(int, int, int), Vector3>(); // 量化键 → 代表位置
            int totalVerts = 0, totalTris = 0, nanVerts = 0, degenerate = 0;
            float minY = float.MaxValue;

            foreach (var r in refs)
            {
                var mesh = r.Mesh;
                if (mesh == null) continue;
                var verts = mesh.vertices;
                var tris = mesh.triangles;
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
                    if (v.y < minY) minY = v.y;
                    keyPos.TryAdd(PosKey(v), v);
                }

                for (int t = 0; t < tris.Length; t += 3)
                {
                    var a = PosKey(verts[tris[t]]);
                    var b = PosKey(verts[tris[t + 1]]);
                    var c = PosKey(verts[tris[t + 2]]);
                    if (a.Equals(b) || b.Equals(c) || a.Equals(c))
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
            // 合并后计数取 min：链的各子段是同一条逻辑边的细分，真重叠时各子段
            // 计数同增，min 保留真实重数（旧版求和会把细分误报成重复边）。
            MergeCollinearChains(edgeCount, keyPos);

            int boundary = 0, frontier = 0, duplicate = 0;
            var boundarySamples = new List<((int, int, int), (int, int, int))>();
            foreach (var kv in edgeCount)
            {
                var rev = (kv.Key.Item4, kv.Key.Item5, kv.Key.Item6, kv.Key.Item1, kv.Key.Item2, kv.Key.Item3);
                edgeCount.TryGetValue(rev, out int revCount);
                if (kv.Value > 1) duplicate++;
                if (revCount == 0)
                {
                    // 最低层的开口 = 流式前沿临时壁的底边（无 Cap，预期开口）
                    if (keyPos[(kv.Key.Item1, kv.Key.Item2, kv.Key.Item3)].y <= minY + 0.01f &&
                        keyPos[(kv.Key.Item4, kv.Key.Item5, kv.Key.Item6)].y <= minY + 0.01f)
                    {
                        frontier++;
                        continue;
                    }
                    boundary++;
                    if (boundarySamples.Count < 8)
                        boundarySamples.Add(((kv.Key.Item1, kv.Key.Item2, kv.Key.Item3),
                            (kv.Key.Item4, kv.Key.Item5, kv.Key.Item6)));
                }
            }

            Debug.Log($"[验证] mesh={refs.Length} 顶点={totalVerts} 三角={totalTris} | " +
                      $"边界边(洞)={boundary} 前沿开口(预期)={frontier} 重复边={duplicate} " +
                      $"退化三角={degenerate} NaN顶点={nanVerts}");

            if (boundary == 0 && duplicate == 0 && nanVerts == 0)
                Debug.Log("[验证] ✓ 水密（封闭流形；流式前沿开口不计）");
            else
            {
                Debug.LogError($"[验证] ✗ 存在缺陷：洞={boundary} 重叠={duplicate} NaN={nanVerts}");
                foreach (var e in boundarySamples)
                {
                    string s1 = keyPos.TryGetValue(e.Item1, out var p1) ? p1.ToString("F6") : e.Item1.ToString();
                    string s2 = keyPos.TryGetValue(e.Item2, out var p2) ? p2.ToString("F6") : e.Item2.ToString();
                    Debug.Log($"  边界边: {s1} → {s2}");
                }
            }
        }

        /// <summary>
        /// 反复合并共线同向相邻边 (a→b)+(b→c ⇒ a→c)。量化键相邻即端点重合，
        /// 共线判定用原始坐标（keyPos）做叉积/点积。折叠细分接缝的拓扑假阳性。
        /// </summary>
        private static void MergeCollinearChains(
            Dictionary<(int, int, int, int, int, int), int> edges,
            Dictionary<(int, int, int), Vector3> keyPos)
        {
            for (int pass = 0; pass < 16; pass++)
            {
                bool changed = false;
                var startsAt = new Dictionary<(int, int, int), List<(int, int, int, int, int, int)>>();
                foreach (var kv in edges)
                {
                    var start = (kv.Key.Item1, kv.Key.Item2, kv.Key.Item3);
                    if (!startsAt.TryGetValue(start, out var list))
                    {
                        list = new List<(int, int, int, int, int, int)>(2);
                        startsAt[start] = list;
                    }
                    list.Add(kv.Key);
                }

                var snapshot = edges.Keys.ToList();
                foreach (var key in snapshot)
                {
                    edges.TryGetValue(key, out int c1);
                    if (c1 <= 0) continue; // 已被合并的死壳
                    var b = (key.Item4, key.Item5, key.Item6);
                    if (!startsAt.TryGetValue(b, out var nexts))
                        continue;
                    foreach (var nb in nexts)
                    {
                        edges.TryGetValue(nb, out int c2);
                        if (c2 <= 0) continue;
                        var c = (nb.Item4, nb.Item5, nb.Item6);
                        if (c.Equals((key.Item1, key.Item2, key.Item3))) continue;
                        if (!keyPos.TryGetValue((key.Item1, key.Item2, key.Item3), out var pa) ||
                            !keyPos.TryGetValue(b, out var pb) ||
                            !keyPos.TryGetValue(c, out var pc))
                            continue;
                        Vector3 d1 = pb - pa, d2 = pc - pb;
                        if (Vector3.Cross(d1, d2).sqrMagnitude > 1e-8f || Vector3.Dot(d1, d2) <= 0f)
                            continue; // 不共线或反向

                        var merged = (key.Item1, key.Item2, key.Item3, nb.Item4, nb.Item5, nb.Item6);
                        edges.TryGetValue(merged, out int cm);
                        edges[key] = 0;
                        edges[nb] = 0;
                        edges[merged] = math.max(cm, math.min(c1, c2));
                        changed = true;
                        break;
                    }
                }

                foreach (var k in edges.Where(e => e.Value == 0).Select(e => e.Key).ToList())
                    edges.Remove(k);
                if (!changed) break;
            }
        }

        private static void AddEdge(Dictionary<(int, int, int, int, int, int), int> map,
            (int, int, int) a, (int, int, int) b)
        {
            var k = (a.Item1, a.Item2, a.Item3, b.Item1, b.Item2, b.Item3);
            map.TryGetValue(k, out int c);
            map[k] = c + 1;
        }

        private static (int, int, int) PosKey(Vector3 v)
        {
            return ((int)math.round(v.x * Quant), (int)math.round(v.y * Quant), (int)math.round(v.z * Quant));
        }
    }
}
