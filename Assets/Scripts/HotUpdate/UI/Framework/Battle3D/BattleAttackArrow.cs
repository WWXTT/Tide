using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SynergyUI
{
    /// <summary>
    /// 攻击宣言箭头（2026-10-13 战斗编舞配套·表现层；2026-10-14 二次改版）：A→B 不再直线——
    /// 抛物线起落弧 + 虚线带（dash 世界长度恒定：弧越长虚线格越多）+ 实心三角箭头贴终点；
    /// 起点=发起方（单位/格锚），选择期终点随鼠标动态拉长，悬停合法目标换绿（选对了），
    /// 确认时吸附目标格冻结渐隐；动作色皮肤（2026-10-15 用户定案）：攻击=红、守卫=蓝、
    /// 放地/生物入场=黄（BattleAttackArrow.Skin 预设）。虚线=程序化网格（每 dash 沿切线、
    /// 面向相机展宽的窄带，LineRenderer 无法逐段换色故弃用）；材质=运行时 Sprites/Default
    /// （URP 兼容、无资产依赖、支持顶点色；shader 缺失时降级为无箭头——目标选择功能不受影响，
    /// 仅无视觉预览）。悬空贴棋盘 0.22m 起、弧峰随水平距离抬升（近低远高封顶）。
    /// 生命周期：创建方独占（CombatChoreographer/BattleCellPicker/BattleScreen 守卫窗）；
    /// Show 幂等换点，FadeOutAsync 冻结渐隐。
    /// </summary>
    public sealed class BattleAttackArrow
    {
        private const float HeightY = 0.22f;    // 起终点悬空高度（世界系 y 偏移）
        private const float Width = 0.13f;      // 虚线带宽
        private const float DashLen = 0.30f;    // 单格虚线长（世界米）
        private const float DashGap = 0.18f;    // 虚线间隔
        private const float SampleStep = 0.12f; // 弧线稠密采样步长（世界米）

        /// <summary>箭头配色皮肤（2026-10-15 用户定案）：颜色区分动作——攻击=红、守卫=蓝、
        /// 放地/生物入场=黄；「选对了」各动作统一绿（ValidSkin，悬停合法目标=可确认）。
        /// Head=箭头与弧末端色，Tail=起点淡色。</summary>
        public readonly struct Skin
        {
            public readonly Color Head, Tail;
            public Skin(Color head, Color tail) { Head = head; Tail = tail; }
        }

        public static readonly Skin AttackSkin = new Skin(        // 攻击（宣言/演出/选择期）
            new Color(1f, 0.30f, 0.24f, 0.95f), new Color(1f, 0.62f, 0.52f, 0.55f));
        public static readonly Skin GuardSkin = new Skin(         // 守卫拦截/守卫选择
            new Color(0.34f, 0.62f, 1f, 0.95f), new Color(0.62f, 0.82f, 1f, 0.55f));
        public static readonly Skin PlaceSkin = new Skin(         // 放地/生物入场选格
            new Color(1f, 0.88f, 0.20f, 0.95f), new Color(1f, 0.97f, 0.60f, 0.55f));
        public static readonly Skin ValidSkin = new Skin(         // 选对了（通用确认绿）
            new Color(0.35f, 0.95f, 0.55f, 0.95f), new Color(0.85f, 1f, 0.80f, 0.60f));

        private static Shader _shader;
        private static bool _shaderChecked;

        private Transform _root;
        private Mesh _ribbonMesh;
        private Material _ribbonMat;
        private Transform _head;
        private Mesh _headMesh;
        private Material _headMat;
        private Color _headColor = ValidSkin.Head; // 当前头色（皮肤可换；FadeOut 从当前色渐隐）
        private Color _tailColor = ValidSkin.Tail; // 当前尾色（虚线起点淡色，皮肤可换）

        public void Dispose()
        {
            if (_root != null) { Object.Destroy(_root.gameObject); _root = null; }
            if (_ribbonMesh != null) { Object.Destroy(_ribbonMesh); _ribbonMesh = null; }
            if (_headMesh != null) { Object.Destroy(_headMesh); _headMesh = null; }
            if (_ribbonMat != null) { Object.Destroy(_ribbonMat); _ribbonMat = null; }
            if (_headMat != null) { Object.Destroy(_headMat); _headMat = null; }
            _head = null;
        }

        /// <summary>显示/刷新（幂等）：from=起点（单位/格锚），to=箭头终点（选择期=鼠标地面投影，
        /// 确认时=目标格锚点）；皮肤=动作色（攻击红/守卫蓝/放地黄），悬停合法目标换 ValidSkin 绿。
        /// 弧峰随距离、虚线格随弧长。</summary>
        public void Show(Vector3 from, Vector3 to, Skin skin)
        {
            if (!EnsureBuilt()) return;
            _root.gameObject.SetActive(true);
            _headColor = skin.Head;
            _tailColor = skin.Tail;
            _ribbonMat.color = Color.white; // 复位（上一轮 FadeOut 可能压过透明度）
            BuildRibbon(Lift(from), Lift(to));
        }

        public void Hide()
        {
            if (_root != null) _root.gameObject.SetActive(false);
        }

        /// <summary>确认目标后的冻结渐隐（箭头已吸附目标格，与单位起步走位形成视觉连续）。</summary>
        public async UniTask FadeOutAsync(float seconds)
        {
            if (_root == null || !_root.gameObject.activeSelf) { Hide(); return; }
            float t0 = Time.realtimeSinceStartup;
            while (true)
            {
                float t = (Time.realtimeSinceStartup - t0) / seconds;
                if (t >= 1f || _root == null) break;
                float a = 1f - t;
                _ribbonMat.color = new Color(1f, 1f, 1f, a); // 网格顶点色×材质色=整体渐隐
                _headMat.color = new Color(_headColor.r, _headColor.g, _headColor.b, _headColor.a * a);
                await UniTask.Yield();
            }
            Hide();
        }

        private static Vector3 Lift(Vector3 p) => new Vector3(p.x, p.y + HeightY, p.z);

        /// <summary>抛物线弧采样 + 沿弧长布 dash（含边界精确插值）；尾 dash 吃到终点防箭头悬空。</summary>
        private void BuildRibbon(Vector3 a, Vector3 b)
        {
            Vector3 flat = b - a;
            flat.y = 0f;
            float dist = flat.magnitude;
            float arcH = dist < 0.5f ? 0.04f : Mathf.Clamp(dist * 0.2f, 0.10f, 0.65f);

            int n = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(dist, 0.5f) / SampleStep) + 1, 8, 160);
            var pts = new Vector3[n];
            var cum = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / (n - 1);
                pts[i] = Vector3.Lerp(a, b, t) + Vector3.up * (4f * arcH * t * (1f - t));
                if (i > 0) cum[i] = cum[i - 1] + Vector3.Distance(pts[i - 1], pts[i]);
            }
            float len = cum[n - 1];

            var verts = new List<Vector3>();
            var cols = new List<Color>();
            var tris = new List<int>();
            var cam = BattleStageDirector.WorldCam;

            float s = 0f;
            while (s < len - 0.02f)
            {
                float s1 = Mathf.Min(s + DashLen, len);
                if (s + DashLen + DashGap >= len) s1 = len; // 最后一段 dash 落到终点
                AddDash(verts, cols, tris, pts, cum, s, s1, len, cam, _headColor, _tailColor);
                s = s1 + DashGap;
            }

            _ribbonMesh.Clear();
            _ribbonMesh.SetVertices(verts);
            _ribbonMesh.SetColors(cols);
            _ribbonMesh.SetTriangles(tris, 0);
            _ribbonMesh.RecalculateBounds();

            // 箭头贴终点、朝水平去向（root 恒在原点，位置必须写世界系）
            _head.position = b;
            if (flat.sqrMagnitude > 1e-4f)
                _head.rotation = Quaternion.Euler(0f, Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg, 0f);
            _headMat.color = _headColor;
        }

        /// <summary>一段 dash：弧上 [s0,s1] 折线 → 各点按切线×视线求侧向 → 展宽成窄带三角面。
        /// 顶点色沿弧长 tail→head 插值（间隔不建面即虚线）。</summary>
        private static void AddDash(List<Vector3> verts, List<Color> cols, List<int> tris,
            Vector3[] pts, float[] cum, float s0, float s1, float len, Camera cam,
            Color headColor, Color tailColor)
        {
            var poly = new List<Vector3> { PointAt(pts, cum, s0) };
            var polyS = new List<float> { s0 };
            for (int i = 0; i < pts.Length; i++)
                if (cum[i] > s0 + 0.001f && cum[i] < s1 - 0.001f) { poly.Add(pts[i]); polyS.Add(cum[i]); }
            poly.Add(PointAt(pts, cum, s1));
            polyS.Add(s1);
            if (poly.Count < 2) return;

            // 各折线点侧向（中央差分切线；共顶点复用防裂缝；视线平行切线时上向兜底）
            var sides = new Vector3[poly.Count];
            for (int i = 0; i < poly.Count; i++)
            {
                Vector3 tangent = poly[Mathf.Min(i + 1, poly.Count - 1)] - poly[Mathf.Max(i - 1, 0)];
                Vector3 view = cam != null ? cam.transform.position - poly[i] : Vector3.up * 10f;
                Vector3 side = Vector3.Cross(tangent.normalized, view.normalized);
                if (side.sqrMagnitude < 1e-6f) side = Vector3.Cross(tangent.normalized, Vector3.up);
                sides[i] = side.normalized * (Width * 0.5f);
            }

            int baseIdx = verts.Count;
            for (int i = 0; i < poly.Count; i++)
            {
                Color c = Color.Lerp(tailColor, headColor, Mathf.Clamp01(polyS[i] / len));
                verts.Add(poly[i] + sides[i]);
                cols.Add(c);
                verts.Add(poly[i] - sides[i]);
                cols.Add(c);
            }
            for (int i = 0; i < poly.Count - 1; i++)
            {
                int l0 = baseIdx + 2 * i, r0 = l0 + 1, l1 = baseIdx + 2 * (i + 1), r1 = l1 + 1;
                tris.Add(l0); tris.Add(l1); tris.Add(r0);
                tris.Add(r0); tris.Add(l1); tris.Add(r1);
            }
        }

        /// <summary>弧长 s → 弧上点（线性扫段插值；弧点数 ≤160 无需二分）。</summary>
        private static Vector3 PointAt(Vector3[] pts, float[] cum, float s)
        {
            if (s <= 0f) return pts[0];
            for (int i = 1; i < cum.Length; i++)
                if (cum[i] >= s)
                {
                    float seg = cum[i] - cum[i - 1];
                    float t = seg > 1e-6f ? (s - cum[i - 1]) / seg : 0f;
                    return Vector3.Lerp(pts[i - 1], pts[i], t);
                }
            return pts[pts.Length - 1];
        }

        private bool EnsureBuilt()
        {
            if (_root != null && _ribbonMesh != null) return true;
            if (!EnsureShader()) return false;

            var rootGo = new GameObject("fx-attack-arrow");
            _root = rootGo.transform;
            // 收进 Map 总根（2026-10-14 晚定案）：运行时临时对象不堆积场景根目录；
            // 战场态恒在棋盘装台后使用（MapRoot 必在）；Map 恒 identity 世界坐标不变。
            if (BattleStageDirector.MapRoot != null)
                _root.SetParent(BattleStageDirector.MapRoot, true);

            var ribbonGo = new GameObject("ribbon");
            ribbonGo.transform.SetParent(_root, false);
            _ribbonMesh = new Mesh { name = "AttackArrowRibbon" };
            _ribbonMesh.MarkDynamic();
            ribbonGo.AddComponent<MeshFilter>().sharedMesh = _ribbonMesh;
            _ribbonMat = new Material(_shader);
            var rend = ribbonGo.AddComponent<MeshRenderer>();
            rend.sharedMaterial = _ribbonMat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;

            _headMesh = BuildHeadMesh();
            _headMat = new Material(_shader);
            var headGo = new GameObject("head");
            _head = headGo.transform;
            _head.SetParent(_root, false);
            _head.gameObject.AddComponent<MeshFilter>().sharedMesh = _headMesh;
            var headRend = _head.gameObject.AddComponent<MeshRenderer>();
            headRend.sharedMaterial = _headMat;
            headRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            headRend.receiveShadows = false;
            return true;
        }

        private static bool EnsureShader()
        {
            if (_shaderChecked) return _shader != null;
            _shaderChecked = true;
            _shader = Shader.Find("Sprites/Default");
            if (_shader == null)
                Debug.LogWarning("[CombatChoreo] Sprites/Default shader 不可用——攻击箭头降级为不显示（选择功能不受影响）");
            return _shader != null;
        }

        /// <summary>箭头三角 mesh（+Z 指向、XZ 平摊、双面渲染防俯视绕序问题）。</summary>
        private static Mesh BuildHeadMesh()
        {
            var mesh = new Mesh { name = "AttackArrowHead" };
            mesh.vertices = new[]
            {
                new Vector3(0f, 0f, 0.30f),
                new Vector3(-0.17f, 0f, -0.16f),
                new Vector3(0.17f, 0f, -0.16f),
            };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 1 }; // 双面
            return mesh;
        }
    }
}
