using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SynergyUI
{
    /// <summary>
    /// 攻击宣言寻路箭头（2026-10-13 战斗编舞配套·表现层）：LineRenderer 折线 + 三角箭头，
    /// 悬空贴棋盘 0.22m（高于地块/漩涡、低于单位模型主体）；顶点色着色（暖白→琥珀），
    /// 材质=运行时 Sprites/Default（URP 兼容、无资产依赖；shader 缺失时降级为无箭头——
    /// 目标选择功能不受影响，仅无视觉预览）。
    /// 生命周期：CombatChoreographer 独占（创建/销毁）；Show 幂等换点，FadeOutAsync 冻结渐隐。
    /// </summary>
    public sealed class BattleAttackArrow
    {
        private const float HeightY = 0.22f;   // 悬空高度（世界系 y 偏移）
        private const float LineWidth = 0.13f;
        private static readonly Color HeadColor = new Color(1f, 0.72f, 0.20f, 0.95f);
        private static readonly Color TailColor = new Color(1f, 0.96f, 0.68f, 0.55f);

        private static Shader _shader;
        private static bool _shaderChecked;

        private Transform _root;
        private LineRenderer _line;
        private Transform _head;
        private Mesh _headMesh;
        private Material _headMat;

        public void Dispose()
        {
            if (_root != null) { Object.Destroy(_root.gameObject); _root = null; }
            if (_headMesh != null) { Object.Destroy(_headMesh); _headMesh = null; }
            if (_headMat != null) { Object.Destroy(_headMat); _headMat = null; }
            _line = null;
            _head = null;
        }

        /// <summary>显示/刷新路径（幂等）：from=攻击单位当前位置，waypoints=途经格世界锚点（含终点）。</summary>
        public void Show(Vector3 from, List<Vector3> waypoints)
        {
            if (!EnsureBuilt() || waypoints == null || waypoints.Count == 0) return;
            _root.gameObject.SetActive(true);

            var pts = new Vector3[waypoints.Count + 1];
            pts[0] = Lift(from);
            for (int i = 0; i < waypoints.Count; i++) pts[i + 1] = Lift(waypoints[i]);
            _line.positionCount = pts.Length;
            _line.SetPositions(pts);
            _line.startColor = TailColor;
            _line.endColor = HeadColor;

            // 箭头朝末段方向
            Vector3 dir = pts[pts.Length - 1] - pts[pts.Length - 2];
            dir.y = 0f;
            if (dir.sqrMagnitude > 1e-4f)
            {
                _head.localPosition = Vector3.zero;
                _head.localRotation = Quaternion.Euler(0f, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, 0f);
            }
            _headMat.color = HeadColor;
        }

        public void Hide()
        {
            if (_root != null) _root.gameObject.SetActive(false);
        }

        /// <summary>确认目标后的冻结渐隐（与单位起步走同路径形成视觉连续）。</summary>
        public async UniTask FadeOutAsync(float seconds)
        {
            if (_root == null || !_root.gameObject.activeSelf) { Hide(); return; }
            float t0 = Time.realtimeSinceStartup;
            while (true)
            {
                float t = (Time.realtimeSinceStartup - t0) / seconds;
                if (t >= 1f || _root == null) break;
                float a = 1f - t;
                _line.startColor = new Color(TailColor.r, TailColor.g, TailColor.b, TailColor.a * a);
                _line.endColor = new Color(HeadColor.r, HeadColor.g, HeadColor.b, HeadColor.a * a);
                _headMat.color = new Color(HeadColor.r, HeadColor.g, HeadColor.b, HeadColor.a * a);
                await UniTask.Yield();
            }
            Hide();
        }

        private static Vector3 Lift(Vector3 p) => new Vector3(p.x, p.y + HeightY, p.z);

        private bool EnsureBuilt()
        {
            if (_line != null && _root != null) return true;
            if (!EnsureShader()) return false;

            var rootGo = new GameObject("fx-attack-arrow");
            _root = rootGo.transform;
            _line = rootGo.AddComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.material = new Material(_shader);
            _line.widthMultiplier = LineWidth;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;

            _headMesh = BuildHeadMesh();
            _headMat = new Material(_shader);
            var headGo = new GameObject("head");
            _head = headGo.transform;
            _head.SetParent(_root, false);
            _head.gameObject.AddComponent<MeshFilter>().sharedMesh = _headMesh;
            var rend = _head.gameObject.AddComponent<MeshRenderer>();
            rend.sharedMaterial = _headMat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
            return true;
        }

        private static bool EnsureShader()
        {
            if (_shaderChecked) return _shader != null;
            _shaderChecked = true;
            _shader = Shader.Find("Sprites/Default");
            if (_shader == null)
                Debug.LogWarning("[CombatChoreo] Sprites/Default shader 不可用——寻路箭头降级为不显示（选择功能不受影响）");
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
