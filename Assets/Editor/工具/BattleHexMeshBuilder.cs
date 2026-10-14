using System.Collections.Generic;
using SynergyUI;
using UnityEditor;
using UnityEngine;

namespace Tide.工具
{
    /// <summary>
    /// 六边形棋格 mesh 生成器（2026-10-13 战场重做二段收敛：只产一枚六边形资产，
    /// 摆位/配色归运行时装配，生成期不再烘焙整盘——整盘方案两处硬伤已弃：
    /// ①多 subMesh 经 CreateAsset 落盘只剩首区；②三角序反了=法线朝下、俯视被剔）。
    ///
    /// 2026-10-13 厚度定案：六棱柱=边长1（OuterRadius；正六边形边长=外接半径）
    /// ×厚0.2（顶面 y=0 生物立面上、柱体向下延伸——俯视见面顶，厚度显于格缝侧壁与盘缘）。
    /// 几何与 BattleBoardGrid 同源：pointy-top、顶点角 30°+60°k、XZ 平面；
    /// 绕向口径：三角 (a,b,c) 的叉积 (b−a)×(c−a) 指向面外 = Unity 可见面
    /// （顶面沿用已验的 (中心, ring[k+1], ring[k]) 序）。
    ///
    /// 用法：菜单 Tools/Battle3D/生成六边形棋格（幂等覆盖重建）。
    /// </summary>
    public static class BattleHexMeshBuilder
    {
        private const string MenuPath = "Tools/Battle3D/生成六边形棋格";
        private const string Dir = "Assets/Art/Battle3D";
        private const string MeshPath = Dir + "/BattleHex.asset";

        /// <summary>格缝：格半径缩放（1=密铺无缝；&lt;1 留缝显六边形轮廓与侧壁）。</summary>
        private const float CellScale = 0.94f;

        /// <summary>棱柱厚度：顶面 y=0、底面 y=−Thickness（边长=OuterRadius=1）。</summary>
        public const float Thickness = 0.2f;

        [MenuItem(MenuPath)]
        public static void Build()
        {
            // ---- 目录先行（CreateAsset 不建父目录，缺目录即断） ----
            string cur = "Assets";
            foreach (var seg in Dir.Substring("Assets/".Length).Split('/'))
            {
                string next = cur + "/" + seg;
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, seg);
                cur = next;
            }

            // ---- 六边形环顶点（pointy-top，顶点角 30°+60°k；半径=边长×格缝缩放） ----
            float r = BattleBoardGrid.OuterRadius * CellScale;
            var ring = new Vector3[6];
            var ringUv = new Vector2[6];
            for (int k = 0; k < 6; k++)
            {
                float a = Mathf.Deg2Rad * (30f + 60f * k);
                ring[k] = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                ringUv[k] = new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f);
            }

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            int Add(Vector3 v, Vector3 n, Vector2 uv)
            {
                vertices.Add(v);
                normals.Add(n);
                uvs.Add(uv);
                return vertices.Count - 1;
            }
            void Tri(int a, int b, int c)
            {
                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(c);
            }

            // ---- 顶面（y=0 法线朝上；扇形 (中心, ring[k+1], ring[k]) 俯视可见——旧口径已验） ----
            int topC = Add(Vector3.zero, Vector3.up, new Vector2(0.5f, 0.5f));
            var top = new int[6];
            for (int k = 0; k < 6; k++) top[k] = Add(ring[k], Vector3.up, ringUv[k]);
            for (int k = 0; k < 6; k++) Tri(topC, top[(k + 1) % 6], top[k]);

            // ---- 底面（y=−Thickness 法线朝下；绕向反转=仰视可见） ----
            int botC = Add(Vector3.down * Thickness, Vector3.down, new Vector2(0.5f, 0.5f));
            var bot = new int[6];
            for (int k = 0; k < 6; k++)
                bot[k] = Add(ring[k] + Vector3.down * Thickness, Vector3.down, ringUv[k]);
            for (int k = 0; k < 6; k++) Tri(botC, bot[k], bot[(k + 1) % 6]);

            // ---- 侧壁×6（平法线=棱边中点水平向外；四边两三角 (A,B,C)(A,C,D) 叉积朝外已验） ----
            for (int k = 0; k < 6; k++)
            {
                int k1 = (k + 1) % 6;
                var n = Vector3.Normalize(ring[k] + ring[k1]);
                int a = Add(ring[k], n, new Vector2(0f, 1f));
                int b = Add(ring[k1], n, new Vector2(1f, 1f));
                int c = Add(ring[k1] + Vector3.down * Thickness, n, new Vector2(1f, 0f));
                int d = Add(ring[k] + Vector3.down * Thickness, n, new Vector2(0f, 0f));
                Tri(a, b, c);
                Tri(a, c, d);
            }

            var mesh = new Mesh { name = "BattleHex" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();

            var old = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (old != null) AssetDatabase.DeleteAsset(MeshPath);
            AssetDatabase.CreateAsset(mesh, MeshPath);
            AssetDatabase.SaveAssets();

            // ---- 数据层自检：逐三角几何法线与顶点法线同向（绕向证据）+ 规格与厚度包围 ----
            int flipped = 0;
            var vs = mesh.vertices;
            for (int t = 0; t < triangles.Count; t += 3)
            {
                var gn = Vector3.Cross(vs[triangles[t + 1]] - vs[triangles[t]],
                    vs[triangles[t + 2]] - vs[triangles[t]]);
                var vn = (normals[triangles[t]] + normals[triangles[t + 1]] + normals[triangles[t + 2]]).normalized;
                if (Vector3.Dot(gn, vn) <= 0f) flipped++;
            }
            bool pass = flipped == 0 && vertices.Count == 38 && triangles.Count == 72
                        && Mathf.Approximately(mesh.bounds.min.y, -Thickness)
                        && Mathf.Approximately(mesh.bounds.max.y, 0f);
            Debug.Log($"[{(pass ? "PASS" : "FAIL")}][BattleHexMeshBuilder] 六棱柱 顶点={vertices.Count}(期38)" +
                      $" 三角={triangles.Count / 3}(期24) 边长={BattleBoardGrid.OuterRadius} 厚={Thickness}" +
                      $" bounds.y=[{mesh.bounds.min.y:0.###},{mesh.bounds.max.y:0.###}] 反向三角={flipped}");
        }
    }
}
