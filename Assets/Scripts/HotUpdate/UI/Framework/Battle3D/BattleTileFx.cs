using System.Collections.Generic;
using Tide.HotUpdate;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SynergyUI
{
/// <summary>
/// 地块高光特效层：可行动性/地牌状态改由地块自身表达——
/// 生物未横置未死亡=脚下地块暖白扩散波，横置或死亡即熄；
/// 地牌未横置=顶面锁色描边环（2026-10-17 定案：整块菲涅尔壳颜色压住格面文字/元素图标退役）
/// + 格面元素图标（Element.prefab，叠层错位两张），横置（已产元素）即熄。
///
/// 形态：生物=每格一枚菲涅尔壳（BattleHex mesh 同形，挂 tile-{x}-{z} 下随浮起动画同走；
/// 抬 4mm + 侧壁微外扩防与本体叠面闪烁），材质=落盘资产 BattleTile_Fresnel.mat（波模式）；
/// 地牌=顶面六边形描边环（环网格运行时由 BattleHex 顶面轮廓推导，外缘 0.98 内缘 0.85 让开
/// 格面图标与平贴计数文字），材质=BattleTileRing.shader 运行时实例；颜色均按格走
/// MaterialPropertyBlock 覆写。
///
/// 消费模型：全量对账——BattleFieldView.Apply 开头 ClearAll 全熄，随 Reconcile 按数据逐格
/// 点亮（幂等；对账频率=引擎事件，非逐帧）。依赖缺失（shader/材质/图标 prefab）逐级回落
/// 为"该格无特效"并告警一次，不阻塞对局。
/// </summary>
    public static class BattleTileFx
    {
        private const string FresnelMatPath = "Assets/Art/Battle3D/Materials/BattleTile_Fresnel.mat";
        private const string HexMeshPath = "Assets/Art/Battle3D/BattleHex.asset";
        private const string ElementPrefabPath = "Assets/Art/Prefab/Element.prefab";
        private const string RingShaderPath = "Assets/Art/Shaders/BattleTileRing.shader";

        private static readonly int FresnelColorId = Shader.PropertyToID("_FresnelColor");
        private static readonly int WaveWeightId = Shader.PropertyToID("_WaveWeight");
        private static readonly int RingColorId = Shader.PropertyToID("_RingColor");

        // 描边环几何（BattleHex mesh 本地空间，六边形角半径=1）：外缘近贴顶面边，内缘让开
        // 格面图标（+z 0.35 缩 0.5，最远点距格心 ~0.65）与平贴计数文字（-z 0.42，宽串 "10/10"
        // 端点距格心 ~0.69）——内缘平边 0.85×0.866≈0.736，两侧均留余量；带宽≈0.11~0.13
        private const float RingOuterScale = 0.98f;
        private const float RingInnerScale = 0.85f;
        private const float RingLift = 0.006f; // 顶面之上 6mm（菲涅尔壳 0.004 与图标 0.008 之间）

        // 地牌锁色配色（描边环，兼旧菲涅尔壳口径）：下标=ManaType 序 [灰,红,蓝,绿,白,黑,任意]；
        // 2026-10-24 七组配色定案：底色单源=UiStyle.ManaBlock*（与 CostSquares 费用块同源——
        // 深调压饱和，常驻环光不刺眼）；黑=该底色在 additive 环不发光，仍以紫罗兰替代（介质约束）；
        // 任意=万能货币鎏金压深档（与战场 v6 强调色同族——六色皆可替）
        private static readonly Color[] GlowColors =
        {
            UiStyle.ManaBlockGray,          // 灰
            UiStyle.ManaBlockRed,           // 红
            UiStyle.ManaBlockBlue,          // 蓝
            UiStyle.ManaBlockGreen,         // 绿
            UiStyle.ManaBlockWhite,         // 白
            new Color(0.62f, 0.38f, 0.95f), // 黑（additive 黑不发光→紫罗兰替代）
            UiStyle.ManaBlockAny,           // 任意（鎏金压深）
        };

        // 生物可行动高光=扩散波（2026-10-13 晚用户定案：菲涅尔侧壁太弱，改格心向外扩散光波；
        // 波参数全局调在 BattleTile_Fresnel.mat，此处只给色+波模式；
        // 2026-10-24 用户定案改回蓝红：己方=淡蓝、对手=红（v6 §11.2 鎏金/压暗锈红退役——
        // 与阵营低饱和青蓝/锈红分属两套：阵营色管单位本体，本高光管可行动性提示；
        // 公开=BattleHudRow 阶段光圈同色复用——蓝红提示色单源）
        public static readonly Color UnitGlowColor = new Color(0.45f, 0.72f, 1.00f);
        public static readonly Color UnitGlowColorOpp = new Color(1.00f, 0.35f, 0.28f);

        private static readonly string[] ElemMatNames = { "Gray", "Red", "Blue", "Green", "White", "Black", "Any" };
        private static readonly Material[] _elemMats = new Material[7]; // 元素图标面片材质（下标=ManaType 序；含 Any 万能货币）

        private static Material _fresnelMat;
        private static Mesh _hexMesh;
        private static GameObject _elementPrefab;
        private static MaterialPropertyBlock _mpb;
        private static Material _ringMat; // BattleTileRing.shader 运行时实例（跨域重载 fake-null 判死重建）
        private static Mesh _ringMesh;    // 顶面轮廓推导的描边环网格（全盘共享一枚）

        private static readonly Dictionary<long, Transform> _tiles = new Dictionary<long, Transform>();
        private static readonly Dictionary<long, Renderer> _glows = new Dictionary<long, Renderer>();
        private static readonly Dictionary<long, Renderer> _rings = new Dictionary<long, Renderer>();
        // 元素图标留存池（2026-10-18 性能）：键=格+色（技能格黑白双图标错位共存）——图标几何/色恒定，
        // 对账只做显隐不再销毁重建（原每事件 Destroy+Instantiate 全部地牌图标）
        private static readonly Dictionary<long, GameObject> _icons = new Dictionary<long, GameObject>();
        private static bool _depsLogged;

        /// <summary>当前点亮的高光件数=菲涅尔壳+描边环（对账日志证据）。</summary>
        public static int GlowOn
        {
            get
            {
                int n = 0;
                foreach (var r in _glows.Values) if (r != null && r.enabled) n++;
                foreach (var r in _rings.Values) if (r != null && r.enabled) n++;
                return n;
            }
        }

        /// <summary>当前格面元素图标数（对账日志证据；=留存池中激活数）。</summary>
        public static int IconCount
        {
            get
            {
                int n = 0;
                foreach (var i in _icons.Values) if (i != null && i.activeSelf) n++;
                return n;
            }
        }

        /// <summary>域重载关闭（Enter Play Mode Options）防跨 Play 残留：与 BattleStageDirector.ResetStatics 同口径。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _tiles.Clear();
            _glows.Clear();
            _rings.Clear();
            _icons.Clear();
            _fresnelMat = null;
            _hexMesh = null;
            _elementPrefab = null;
            _ringMat = null;
            _ringMesh = null;
            System.Array.Clear(_elemMats, 0, _elemMats.Length); // readonly 数组：清元素不换引用
            _depsLogged = false;
        }

        /// <summary>全熄（对账开头调用；菲涅尔壳/描边环/元素图标 renderer 全部留存复用只熄不毁）。</summary>
        public static void ClearAll()
        {
            foreach (var r in _glows.Values)
                if (r != null) r.enabled = false; // fake-null（棋盘重建）跳过，下次 Ensure 判死重建
            foreach (var r in _rings.Values)
                if (r != null) r.enabled = false;
            foreach (var icon in _icons.Values)
                if (icon != null) icon.SetActive(false);
        }

        /// <summary>拆台清索引（对象本体随棋盘根销毁；BattleStageDirector.DismountBoard 调用）。</summary>
        public static void Unmount()
        {
            _tiles.Clear();
            _glows.Clear();
            _rings.Clear();
            _icons.Clear();
        }

        /// <summary>生物可行动：脚下地块扩散波（己方淡蓝 / 对手红；缺省蓝=选格候选等同己方口径）。</summary>
        public static void SetUnitGlow(int x, int z, bool opponent = false)
            => Light(x, z, opponent ? UnitGlowColorOpp : UnitGlowColor);

        /// <summary>单格熄扩散波（2026-10-22 B5 自动选定脉冲收口）：只熄本格，不波及对账态
        ///（ClearAll 会连清选格候选/可行动高光，脉冲收口禁用）。</summary>
        public static void ClearGlow(int x, int z)
        {
            if (_glows.TryGetValue(Key(x, z), out var r) && r != null) r.enabled = false;
        }

        /// <summary>地牌色组仍有未横置张：顶面锁色描边环（lockedColor&lt;0 按灰；2026-10-14 按色叠格后每格恒单色）。</summary>
        public static void SetLandGlow(int x, int z, int lockedColor)
        {
            var r = EnsureRing(x, z);
            if (r == null) return;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            _mpb.SetColor(RingColorId, GlowColors[Mathf.Clamp(lockedColor, 0, GlowColors.Length - 1)]);
            r.SetPropertyBlock(_mpb);
            r.enabled = true;
        }

        // 环境光 SetAmbientGlow 随半场泛光退役（2026-10-15 用户定案：与地格玩法高光共用壳互相覆盖——
        // 优先权指示迁 BattlePriorityGlow 战场底部双光条）

        /// <summary>地牌格面元素图标（2026-10-14 按色叠格改版：每色一格单图标居中；
        /// offset=格面局部偏移，技能格黑白双图标错位复用本口；未锁色/依赖缺失不显示）。</summary>
        public static void AddElement(int x, int z, int lockedColor, Vector2 offset)
        {
            if (lockedColor < 0) return;
            var iconKey = (Key(x, z) << 8) | (uint)(lockedColor & 0xff);
            if (_icons.TryGetValue(iconKey, out var existing) && existing != null)
            {
                // 留存复用（2026-10-18 性能）：同格同色图标几何恒定——只重激活+校位（offset 调用侧恒定，防御性回写）
                existing.transform.localPosition = new Vector3(offset.x, 0.008f, offset.y);
                existing.SetActive(true);
                return;
            }
            var tile = TileOf(x, z);
            if (tile == null) return;
            if (_elementPrefab == null)
            {
                _elementPrefab = HotUpdateAssets.Load<GameObject>("Element", ElementPrefabPath);
                if (_elementPrefab == null) { WarnDeps("Element.prefab"); return; }
            }
            var mat = ElemMaterial(lockedColor);
            if (mat == null) return;

            var icon = Object.Instantiate(_elementPrefab, tile, false);
            icon.name = "fx-element-" + ElemMatNames[Mathf.Clamp(lockedColor, 0, ElemMatNames.Length - 1)];
            icon.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // 面朝上（与 prefab 同值，显式防御）
            icon.transform.localPosition = new Vector3(offset.x, 0.008f, offset.y);
            icon.transform.localScale = Vector3.one * 0.5f;
            var rend = icon.GetComponent<MeshRenderer>();
            rend.sharedMaterial = mat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _icons[iconKey] = icon;
        }

        // ======================================== 内部 ========================================

        private static long Key(int x, int z) => ((long)x << 32) | (uint)z;

        private static void Light(int x, int z, Color c)
        {
            var r = EnsureGlow(x, z);
            if (r == null) return;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            _mpb.SetColor(FresnelColorId, c);
            _mpb.SetFloat(WaveWeightId, 1f); // 壳现仅承载生物扩散波（地牌档 2026-10-17 迁描边环）
            r.SetPropertyBlock(_mpb);
            r.enabled = true;
        }

        /// <summary>格 → tile Transform（root.Find 按名；浮起动画不动父子关系，缓存跨帧安全）。
        /// 公开：BattleFieldView 箭头/计数条同源取格（2026-10-18——替代各自 transform.Find 字符串查找）。</summary>
        public static Transform TileOf(int x, int z)
        {
            var key = Key(x, z);
            if (_tiles.TryGetValue(key, out var t) && t != null) return t; // fake-null 判死重查
            t = BattleBoardGrid.Root != null ? BattleBoardGrid.Root.transform.Find($"tile-{x}-{z}") : null;
            if (t != null) _tiles[key] = t; // 未装台/格名异常不入缓存
            return t;
        }

        /// <summary>菲涅尔壳惰性建（每格最多一枚；依赖缺失回落 null=该格无特效）。</summary>
        private static Renderer EnsureGlow(int x, int z)
        {
            var key = Key(x, z);
            if (_glows.TryGetValue(key, out var r) && r != null) return r; // fake-null 判死重建
            var tile = TileOf(x, z);
            if (tile == null) return null;

            if (_fresnelMat == null)
            {
                _fresnelMat = HotUpdateAssets.Load<Material>("BattleTile_Fresnel", FresnelMatPath);
                if (_fresnelMat == null) { WarnDeps("BattleTile_Fresnel.mat"); return null; }
            }
            if (_hexMesh == null)
            {
                _hexMesh = HotUpdateAssets.Load<Mesh>("BattleHex", HexMeshPath);
                if (_hexMesh == null) { WarnDeps("BattleHex.asset(mesh)"); return null; }
            }

            var go = new GameObject("fx-fresnel");
            go.transform.SetParent(tile, false);
            go.transform.localPosition = new Vector3(0f, 0.004f, 0f);   // 抬 4mm：顶面 y=0 错开防叠面闪
            go.transform.localScale = new Vector3(1.015f, 1f, 1.015f); // 侧壁微外扩（顶面 y=0 不受 Y 缩放影响）
            go.AddComponent<MeshFilter>().sharedMesh = _hexMesh;
            var rend = go.AddComponent<MeshRenderer>();
            rend.sharedMaterial = _fresnelMat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
            rend.enabled = false; // 建=熄，Light 时点亮
            _glows[key] = rend;
            return rend;
        }

        /// <summary>描边环惰性建（每格最多一枚；依赖缺失回落 null=该格无环）。</summary>
        private static Renderer EnsureRing(int x, int z)
        {
            var key = Key(x, z);
            if (_rings.TryGetValue(key, out var r) && r != null) return r; // fake-null 判死重建
            var tile = TileOf(x, z);
            if (tile == null) return null;
            if (!EnsureRingDeps()) return null;

            var go = new GameObject("fx-ring");
            go.transform.SetParent(tile, false);
            go.transform.localPosition = new Vector3(0f, _hexMesh.bounds.max.y + RingLift, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = _ringMesh;
            var rend = go.AddComponent<MeshRenderer>();
            rend.sharedMaterial = _ringMat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
            rend.enabled = false; // 建=熄，SetLandGlow 时点亮
            _rings[key] = rend;
            return rend;
        }

        /// <summary>环材质+网格一次性就绪（BattleHex mesh 与菲涅尔壳链共享缓存；缺件告警一次回落 false）。</summary>
        private static bool EnsureRingDeps()
        {
            if (_ringMat != null && _ringMesh != null) return true;
            if (_hexMesh == null)
            {
                _hexMesh = HotUpdateAssets.Load<Mesh>("BattleHex", HexMeshPath);
                if (_hexMesh == null) { WarnDeps("BattleHex.asset(mesh)"); return false; }
            }
            if (_ringMat == null)
            {
                var shader = HotUpdateAssets.Load<Shader>("BattleTileRing", RingShaderPath);
                if (shader == null) { WarnDeps("BattleTileRing.shader"); return false; }
                _ringMat = new Material(shader) { name = "BattleTile_Ring_Runtime" };
            }
            if (_ringMesh == null) _ringMesh = BuildRingMesh(_hexMesh);
            return _ringMesh != null;
        }

        /// <summary>描边环网格/材质共享单源出口（2026-10-24 阶段光圈复用——BattleHudRow 阶段格
        /// 光圈扩散同网格同 shader，MPB 按件覆色互不干扰；缺件=null 调用方降级不显示）。</summary>
        public static Mesh SharedRingMesh => EnsureRingDeps() ? _ringMesh : null;
        /// <summary>同上（材质件）。</summary>
        public static Material SharedRingMat => EnsureRingDeps() ? _ringMat : null;

        /// <summary>顶面六边形描边环网格：轮廓直接取 BattleHex 顶面外缘顶点（绕序/朝向零假设），
        /// 内外双环成带平铺 y=0（GO 负责抬高）。顶面若有扇形中心点（半径近 0）按最远点半径滤除；
        /// 轮廓点不足 3=mesh 拓扑异常，告警一次回落 null=环禁用。</summary>
        private static Mesh BuildRingMesh(Mesh hex)
        {
            float topY = hex.bounds.max.y;
            var outline = new List<Vector2>();
            var seen = new HashSet<string>();
            foreach (var v in hex.vertices)
            {
                if (v.y < topY - 0.001f) continue;
                if (!seen.Add(v.x.ToString("F3") + "," + v.z.ToString("F3"))) continue;
                outline.Add(new Vector2(v.x, v.z));
            }

            var center = Vector2.zero;
            foreach (var p in outline) center += p;
            if (outline.Count > 0) center /= outline.Count;
            float maxR = 0f;
            foreach (var p in outline) maxR = Mathf.Max(maxR, (p - center).magnitude);
            outline.RemoveAll(p => (p - center).magnitude < maxR * 0.5f);
            if (outline.Count < 3) { WarnDeps("BattleHex 顶面轮廓"); return null; }
            outline.Sort((a, b) => Mathf.Atan2(a.y - center.y, a.x - center.x)
                .CompareTo(Mathf.Atan2(b.y - center.y, b.x - center.x)));

            int n = outline.Count;
            var verts = new Vector3[n * 2];
            var tris = new int[n * 6];
            for (int i = 0; i < n; i++)
            {
                verts[i] = new Vector3(outline[i].x * RingOuterScale, 0f, outline[i].y * RingOuterScale);
                verts[n + i] = new Vector3(outline[i].x * RingInnerScale, 0f, outline[i].y * RingInnerScale);
                int j = (i + 1) % n;
                tris[i * 6 + 0] = i;         tris[i * 6 + 1] = j;         tris[i * 6 + 2] = n + j;
                tris[i * 6 + 3] = i;         tris[i * 6 + 4] = n + j;     tris[i * 6 + 5] = n + i;
            }
            var mesh = new Mesh { name = "BattleTile_Ring_Runtime" };
            mesh.vertices = verts;
            mesh.triangles = tris; // shader 无 NORMAL 输入，法线可缺；Cull Off 免绕序
            return mesh;
        }

        /// <summary>元素图标面片材质（下标=ManaType 序；沿卡背时代资产 BattleCardBack_Elem_*_Mat）。</summary>
        private static Material ElemMaterial(int lockedColor)
        {
            if (lockedColor < 0 || lockedColor >= _elemMats.Length) return null;
            if (_elemMats[lockedColor] != null) return _elemMats[lockedColor];
            _elemMats[lockedColor] = HotUpdateAssets.Load<Material>(
                "Elem_" + ElemMatNames[lockedColor],
                "Assets/Art/Battle3D/Materials/BattleCardBack_Elem_" + ElemMatNames[lockedColor] + "_Mat.asset");
            return _elemMats[lockedColor];
        }

        private static void WarnDeps(string what)
        {
            if (_depsLogged) return;
            _depsLogged = true;
            Debug.LogWarning("[BattleTileFx] 依赖缺失：" + what + "——地块菲涅尔/元素图标降级为不显示");
        }
    }
}
