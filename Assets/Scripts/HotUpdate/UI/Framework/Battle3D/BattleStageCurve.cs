using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SynergyUI
{
    /// <summary>
    /// 炉石式凸面台地（2026-10-23 v5 用户定案：相机保持俯视，环境故意向镜头方向弯——
    /// 中心到四周是凸面浅丘，舞台读成"一块有边界的台子"，不再是无限斜平面）。
    ///
    /// 做法=装台期对 Env/Stairs 全部网格做一次性**顶点弯曲**（运行时克隆网格，prefab 文件零改动）：
    /// 顶点世界半径 r=FlatRadius(7m) 内原样不动（棋盘投影与用户手调中央件绝对不碰），
    /// r 外按 y -= K*(r-7)² 径向下压；法线/切线按当地坡度 atan(2K(r-7)) 绕切向轴旋转
    /// （保持包内平直着色风格，不用 RecalculateNormals 破坏原有明暗）。相邻片共享同一公式
    /// →共边顶点位移一致，接缝天然密封、无悬空件，不需要岩壁补缝。
    ///
    /// 选型说明（方案 A shader 位移 / B 逐件重落位 / C 曲面地形壳 之外的稳妥解）：
    /// 手调版环境主力是 8 张 15~25m 大地面片（ground-seam，scale=2）横跨棋盘下方——
    /// B 刚性旋转必戳穿棋盘；C 新地形壳盖不住现有件；A 动 Dbdrp Lit shader 违反"管线定案"红线
    /// 且 shader_feature 变体在 Yoo 分包有剥离前科。本方案=A 的 CPU 落点：零管线改动、
    /// 零 prefab 改动、克隆网格拆台销毁无泄漏；用户仍可在 prefab 里随手调摆位——
    /// 弯曲每次装台按**当时**摆位现算。
    ///
    /// 参数全部收在本类（红线#6）；一键关：CurvatureK=0（Bend 直接返回=平地现状）。
    /// 生命周期：BattleArenaStage.Attach/Mount 尾部调 Bend，Unmount 调 Unwind。
    /// </summary>
    public static class BattleStageCurve
    {
        /// <summary>弯曲强度：v5 规格值 0.03125（r=15m 降 2m）。**v6（2026-10-23 §11）碗形台地定案后
        /// 归零**——外围上坡由 BattleV6Rebuild 烘进 BattleStage.prefab 的 bowl-terrain 承担，
        /// 本类保留作回滚开关（改回 0.03125=恢复凸面下压，与碗形互斥勿同开）。</summary>
        public const float CurvatureK = 0f;

        /// <summary>平顶半径：r&lt;7m（棋盘外沿）完全不动。</summary>
        public const float FlatRadius = 7f;

        /// <summary>盘心（与 BattleArenaStage.CenterX/CenterZ 同口径：棋盘世界包围盒中心）。</summary>
        public const float CenterX = 7.35f;
        public const float CenterZ = 3.75f;

        private static readonly List<Mesh> _bent = new List<Mesh>(); // 克隆网格台账——Unwind 统一销毁

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _bent.Clear(); // 域重载关闭防跨 Play 残留引用（同 BattleArenaStage 口径）

        /// <summary>顶点弯曲（一次性，装台期调用；根缺/K≤0 直接跳过）。整件落在平顶半径内的不换网格。</summary>
        public static void Bend(Transform envRoot)
        {
            if (envRoot == null || CurvatureK <= 0f) return;
            int bentMeshes = 0, bentVerts = 0, skippedWhole = 0;
            float maxDrop = 0f;
            foreach (var mf in envRoot.GetComponentsInChildren<MeshFilter>(false))
            {
                if (mf.GetComponent<MeshRenderer>() == null) continue; // 无渲染网格（纯过滤用）不弯
                var src = mf.sharedMesh;
                if (src == null) continue;
                var t = mf.transform;
                var clone = Object.Instantiate(src);
                clone.name = src.name + "-curve";
                var vs = clone.vertices;
                var ns = clone.normals;
                var ts = clone.tangents;
                bool hasNormals = ns != null && ns.Length == vs.Length;
                bool hasTangents = ts != null && ts.Length == vs.Length;
                var toWorld = t.rotation; // 方向换算（uniform scale 精确；arena-circle 非均匀缩放为近似，坡度平缓可忽略）
                var toLocal = Quaternion.Inverse(toWorld);
                int hit = 0;
                float pieceMax = 0f;
                for (int i = 0; i < vs.Length; i++)
                {
                    var w = t.TransformPoint(vs[i]);
                    float dx = w.x - CenterX, dz = w.z - CenterZ;
                    float r = Mathf.Sqrt(dx * dx + dz * dz);
                    if (r <= FlatRadius) continue;
                    float u = r - FlatRadius;
                    float drop = CurvatureK * u * u;
                    if (drop > pieceMax) pieceMax = drop;
                    w.y -= drop;
                    vs[i] = t.InverseTransformPoint(w);
                    if (drop > 0.001f)
                    {
                        // 坡度倾角 θ=atan(dy/dr)=atan(2K·u)，绕切向轴 (dz,0,-dx)/r：外缘下沉、坡面朝镜头
                        var axis = new Vector3(dz / r, 0f, -dx / r);
                        var q = Quaternion.AngleAxis(Mathf.Atan(2f * CurvatureK * u) * Mathf.Rad2Deg, axis);
                        if (hasNormals) ns[i] = toLocal * (q * (toWorld * ns[i]));
                        if (hasTangents)
                        {
                            var tv = toLocal * (q * (toWorld * new Vector3(ts[i].x, ts[i].y, ts[i].z)));
                            ts[i] = new Vector4(tv.x, tv.y, tv.z, ts[i].w);
                        }
                    }
                    hit++;
                }
                if (hit == 0) { Object.Destroy(clone); skippedWhole++; continue; }
                clone.vertices = vs;
                if (hasNormals) clone.normals = ns;
                if (hasTangents) clone.tangents = ts;
                clone.RecalculateBounds(); // 顶点外移后包围盒必须重算，防视锥剔除错杀
                mf.sharedMesh = clone;
                _bent.Add(clone);
                bentMeshes++;
                bentVerts += hit;
                if (pieceMax > maxDrop) maxDrop = pieceMax;
            }
            Debug.Log("[BattleStageCurve][PASS] " + envRoot.name + " 凸面弯曲：K=" + CurvatureK +
                      " 平顶半径=" + FlatRadius + " 弯网格=" + bentMeshes + " 弯顶点=" + bentVerts +
                      " 整件盘内跳过=" + skippedWhole + " 最大下压=" + maxDrop.ToString("0.00") + "m" +
                      "（prefab 未改动，K=0 可一键关）");
        }

        /// <summary>拆台销毁克隆网格（BattleArenaStage.Unmount 调；幂等）。</summary>
        public static void Unwind()
        {
            int n = 0;
            foreach (var m in _bent)
                if (m != null) { Object.Destroy(m); n++; }
            _bent.Clear();
            if (n > 0) Debug.Log("[BattleStageCurve] 拆台：销毁克隆网格 " + n);
        }
    }
}
