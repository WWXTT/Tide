using System.Collections.Generic;
using Tide.HotUpdate;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SynergyUI
{
/// <summary>
/// 地块高光特效层：可行动性/地牌状态改由地块自身表达——
/// 生物未横置未死亡=脚下地块从格心向外扩散的光波（2026-10-31 还原：10-30 描边环版退役、
/// 菲涅尔壳扩散波回归；己方淡蓝/对手红），横置或死亡即熄；
/// 地牌未横置=顶面锁色描边环（2026-10-17 定案沿用）+ 格面元素图标（Element.prefab），
/// 横置（已产元素）即熄。
///
/// 形态：生物=每格一枚菲涅尔壳（BattleHex mesh 同形，挂 tile-{x}-{z} 下随浮起动画同走；
/// 抬 4mm + 侧壁微外扩防与本体叠面闪烁），材质=落盘资产 BattleTile_Fresnel.mat（波模式，
/// MPB 覆写 _WaveWeight=1）；逐格提示环（放地候选色环/单位空格候选蓝环/B5 自动取目标
/// 脉冲环）=顶面六边形描边环（环网格运行时由 BattleHex 顶面轮廓推导，外缘 0.98 内缘
/// 0.85 让开格面图标与平贴计数文字），材质=BattleTileRing.shader 运行时实例；颜色均按格
/// 走 MaterialPropertyBlock 覆写。半场大包围面板归优先权底光（BattlePriorityGlow，2026-10-30
/// 用户目标系），mesh 单源=本类 BuildSideRegionMesh。
///
/// 消费模型：全量对账——BattleFieldView.Apply 开头 ClearAll 全熄，随 Reconcile 按数据逐格/逐侧
/// 点亮（幂等；对账频率=引擎事件，非逐帧）。依赖缺失（shader/材质/图标 prefab）逐级回落
/// 为"该格无特效"并告警一次，不阻塞对局。
/// </summary>
    public static class BattleTileFx
    {
        private const string FresnelMatPath = "Assets/Art/Materials/BattleTile_Fresnel.mat"; // 2026-10-26 迁移后目录（原 Battle3D/Materials 已撤）
        private const string HexMeshPath = "Assets/Art/Battle3D/BattleHex.asset";
        private const string ElementPrefabPath = "Assets/Art/Prefab/Element.prefab";
        private const string RingShaderPath = "Assets/Art/Shaders/BattleTileRing.shader";

        private static readonly int FresnelColorId = Shader.PropertyToID("_FresnelColor");
        private static readonly int WaveWeightId = Shader.PropertyToID("_WaveWeight");
        private static readonly int RingColorId = Shader.PropertyToID("_RingColor");

        // 描边环几何（BattleHex mesh 本地空间，六边形角半径=1）：外缘近贴顶面边，内缘让开
        // 格面图标（+z 0.42 缩 0.6，最远点距格心 ~0.77）与平贴计数文字（-z 0.504，宽串 "10/10"
        // 端点距格心 ~0.83）——内缘平边 0.85×0.866≈0.736，两侧均留余量；带宽≈0.11~0.13
        private const float RingOuterScale = 0.98f;
        private const float RingInnerScale = 0.85f;
        private const float RingLift = 0.006f; // 顶面之上 6mm（图标 0.008 与计数条 0.014 之间）

        // 地牌锁色配色（描边环，兼旧菲涅尔壳口径）：下标=ManaType 序 [灰,红,蓝,绿,任意]；
        // 2026-10-24 配色定案（底色单源=UiStyle.ManaBlock*，与 CostSquares 费用块同源——深调压饱和，
        // 常驻环光不刺眼）；2026-10-28 P2 席位收缩删白/黑两位（黑紫罗兰替代色随之退役）；
        // 任意=万能货币鎏金压深档（与战场 v6 强调色同族——四色皆可替）
        private static readonly Color[] GlowColors =
        {
            UiStyle.ManaBlockGray,          // 灰
            UiStyle.ManaBlockRed,           // 红
            UiStyle.ManaBlockBlue,          // 蓝
            UiStyle.ManaBlockGreen,         // 绿
            UiStyle.ManaBlockAny,           // 任意（鎏金压深）
        };

        // 可行动提示色（2026-10-24 用户定案蓝红：己方=淡蓝、对手=红；v6 §11.2 鎏金/压暗锈红
        // 退役——与阵营低饱和青蓝/锈红分属两套：阵营色管单位本体，本高光管可行动性提示；
        // 公开=BattleHudRow 阶段光圈/候选环同色复用——蓝红提示色单源）
        public static readonly Color UnitGlowColor = new Color(0.45f, 0.72f, 1.00f);
        public static readonly Color UnitGlowColorOpp = new Color(1.00f, 0.35f, 0.28f);

        private static readonly string[] ElemMatNames = { "Gray", "Red", "Blue", "Green", "Any" };
        private static readonly Material[] _elemMats = new Material[5]; // 元素图标面片材质（下标=ManaType 序 [灰,红,蓝,绿,任意]；白/黑席位已删 2026-10-28 P2）

        private static Mesh _hexMesh;                           // BattleHex mesh（描边环轮廓源/扩散波壳同 mesh）
        private static GameObject _elementPrefab;
        private static Material _fresnelMat;                    // BattleTile_Fresnel.mat（扩散波模式，2026-10-31 还原）
        private static MaterialPropertyBlock _mpb;
        private static Material _ringMat;                       // BattleTileRing.shader 运行时实例
        private static Mesh _ringMesh;                          // 顶面轮廓推导的描边环网格（全盘共享一枚）

        private static readonly Dictionary<long, Transform> _tiles = new Dictionary<long, Transform>();
        private static readonly Dictionary<long, Renderer> _glows = new Dictionary<long, Renderer>();   // 菲涅尔扩散波壳（生物可行动）
        private static readonly Dictionary<long, Renderer> _rings = new Dictionary<long, Renderer>();
        // 元素图标留存池（2026-10-18 性能）：键=格+色（同格双色错位共存）——图标几何/色恒定，
        // 对账只做显隐不再销毁重建（原每事件 Destroy+Instantiate 全部地牌图标）
        private static readonly Dictionary<long, GameObject> _icons = new Dictionary<long, GameObject>();
        private static bool _depsLogged;

        /// <summary>当前点亮的高光件数=扩散波壳+描边环（对账日志证据）。</summary>
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

        /// <summary>域重载关闭（Enter Play Mode Options）防跨 Play 残留：与 BattleStageDirector.ResetStatics 同口径。
        ///（面板/环 mesh 是引擎对象无域态，不在此清——fake-null 重建时复用缓存几何与配套中心。）</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _tiles.Clear();
            _glows.Clear();
            _rings.Clear();
            _icons.Clear();
            _hexMesh = null;
            _elementPrefab = null;
            _fresnelMat = null;
            _ringMat = null;
            _ringMesh = null;
            System.Array.Clear(_elemMats, 0, _elemMats.Length); // readonly 数组：清元素不换引用
            _depsLogged = false;
        }

        /// <summary>全熄（对账开头调用；扩散波壳/描边环/元素图标 renderer 全部留存复用只熄不毁）。</summary>
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

        /// <summary>生物可行动：脚下地块从格心向外扩散的光波（2026-10-31 还原扩散波——10-30
        /// 描边环版退役；己方淡蓝 / 对手红，候选等同己方蓝口径）。</summary>
        public static void SetUnitGlow(int x, int z, bool opponent = false)
            => Light(x, z, opponent ? UnitGlowColorOpp : UnitGlowColor);

        /// <summary>逐格提示环（2026-10-30 起逐格提示统一载体）：单位空格候选蓝环、
        /// B5 自动取目标脉冲（阵营色环）均走本口；放地候选锁色环仍走 SetLandGlow。</summary>
        public static void SetCellRing(int x, int z, Color c)
        {
            var r = EnsureRing(x, z);
            if (r == null) return;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            _mpb.SetColor(RingColorId, c);
            r.SetPropertyBlock(_mpb);
            r.enabled = true;
        }

        /// <summary>单格熄描边环（2026-10-22 B5 自动选定脉冲收口）：只熄本格，不波及对账态
        ///（ClearAll 会连清候选环/半场面板，脉冲收口禁用；可行动扩散波壳是独立 renderer
        /// 不受本口波及——脉冲环与可动波各管各的）。</summary>
        public static void ClearGlow(int x, int z)
        {
            if (_rings.TryGetValue(Key(x, z), out var r) && r != null) r.enabled = false;
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
            icon.transform.localScale = Vector3.one * 0.6f; // 2026-10-30 地块 mesh×1.2 联动（原 0.5，保与格面同比例）
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

        /// <summary>菲涅尔壳惰性建（每格最多一枚；依赖缺失回落 null=该格无特效；2026-10-31 还原）。</summary>
        private static Renderer EnsureGlow(int x, int z)
        {
            var key = Key(x, z);
            Renderer r;
            if (_glows.TryGetValue(key, out r) && r != null) return r; // fake-null 判死重建
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

        /// <summary>格 → tile Transform（root.Find 按名；浮起动画不动父子关系，缓存跨帧安全）。
        /// 公开：BattleFieldView 箭头/计数条同源取格（2026-10-18——替代各自 transform.Find 字符串查找）。</summary>
        public static Transform TileOf(int x, int z)
        {
            var key = Key(x, z);
            Transform t;
            if (_tiles.TryGetValue(key, out t) && t != null) return t; // fake-null 判死重查
            t = BattleBoardGrid.Root != null ? BattleBoardGrid.Root.transform.Find($"tile-{x}-{z}") : null;
            if (t != null) _tiles[key] = t; // 未装台/格名异常不入缓存
            return t;
        }

        /// <summary>半场大包围面板 mesh 生成器（2026-10-30 终案；正式链=**落盘 mesh 资产**，
        /// 本方法供编辑器烘焙与资产缺失回落）：己方=BoardLayout z0..3、对方=z3..6（各含中线行
        /// ——前线争夺带）全部地块的视觉六角剪影取凸包，四周斜接偏移外扩一条地缝宽
        ///（=同行格距−格宽）——大致包围成非正六边形。三角=**中心连接式扇面**（区域格心均值→
        /// 多边形各角，用户口径：不做"主体+外圈补边"双环）；顶点=Map 世界系绝对坐标
        ///（Map 恒 identity，消费方 GO 钉 (0, PanelY, 0) 即落位）。UV.y=纵向渐变（自家后缘 0→
        /// 中线侧 1，对方侧镜像烘反——BattlePriorityPanel 的 saturate(1-v) 直接可用；uv.x 恒 0）。
        /// 绕组俯视朝上（消费方 shader Cull Back 可见）。</summary>
        internal static Mesh BuildSideRegionMesh(bool selfSide)
        {
            var boardPos = BattleBoardGrid.Root != null ? BattleBoardGrid.Root.position
                                                        : new Vector3(0.1871f, 0f, -1.2f); // 未装台兜底=拼装偏移（2026-10-30 格距×1.1 绕盘心复位）
            int zFrom = selfSide ? 0 : 3, zTo = selfSide ? 3 : 6;
            float hx = 0.8660254f * 0.94f * BattleBoardGrid.HexMeshScale * BattleBoardGrid.TileScale; // 地块视觉半宽
            float hz = 0.94f * BattleBoardGrid.HexMeshScale * BattleBoardGrid.TileScale;               // 地块视觉半顶
            float margin = BattleBoardGrid.InnerRadius * 2f - hx * 2f; // 当前地缝宽（同行相邻格面间距）
            var cc = Vector2.zero; // 区域格心均值（外扩离心基准）
            var pts = new List<Vector2>(); // 各格六角剪影角点
            int cells = 0;
            for (int z = zFrom; z <= zTo; z++)
                for (int x = 0; x < GameBoard.BoardLayout.Width; x++)
                {
                    if (GameBoard.BoardLayout.NoTileAt(x, z)) continue;
                    var c = BattleBoardGrid.CellCenterLocal(x, z);
                    float wx = c.x + boardPos.x, wz = c.z + boardPos.z;
                    cc += new Vector2(wx, wz);
                    cells++;
                    pts.Add(new Vector2(wx, wz + hz));
                    pts.Add(new Vector2(wx + hx, wz + hz * 0.5f));
                    pts.Add(new Vector2(wx + hx, wz - hz * 0.5f));
                    pts.Add(new Vector2(wx, wz - hz));
                    pts.Add(new Vector2(wx - hx, wz - hz * 0.5f));
                    pts.Add(new Vector2(wx - hx, wz + hz * 0.5f));
                }
            cc /= cells;
            var hull = ConvexHull(pts); // 内环=区域剪影凸包
            int n = hull.Count;
            // 外环=经典斜接偏移：每顶点沿相邻两边外法线角平分线外推 margin/cos(半角)——
            // 四周每条边的边距恒=margin（浅角按 0.35 截断防飞点，该处边距下限≈0.35×margin）
            var outer = new List<Vector2>(n);
            float minZ = float.MaxValue, maxZ = float.MinValue;
            for (int i = 0; i < n; i++)
            {
                var prev = hull[(i - 1 + n) % n];
                var cur = hull[i];
                var next = hull[(i + 1) % n];
                var n0 = EdgeNormal(prev, cur, cc);
                var n1 = EdgeNormal(cur, next, cc);
                var bis = n0 + n1;
                if (bis.sqrMagnitude < 1e-6f) bis = n0; // 平角兜底
                bis.Normalize();
                var p = cur + bis * (margin / Mathf.Max(0.35f, Vector2.Dot(bis, n0)));
                outer.Add(p);
                if (p.y < minZ) minZ = p.y;
                if (p.y > maxZ) maxZ = p.y;
            }
            float footD = maxZ - minZ;
            // 纵向渐变：自家后缘 0 → 中线侧外缘 1（对方侧烘反）
            float GradZ(float wz) => selfSide ? (wz - minZ) / footD : (maxZ - wz) / footD;
            var verts = new Vector3[1 + n];
            var uvs = new Vector2[1 + n];
            var tris = new int[n * 3];
            verts[0] = new Vector3(cc.x, 0f, cc.y); // 区域格心均值=扇心（凸包内点保证扇面合法）
            uvs[0] = new Vector2(0f, GradZ(cc.y));
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                verts[1 + i] = new Vector3(outer[i].x, 0f, outer[i].y);
                uvs[1 + i] = new Vector2(0f, GradZ(outer[i].y));
                int t = i * 3;
                tris[t] = 0; tris[t + 1] = 1 + j; tris[t + 2] = 1 + i; // （中心,下一角,当前角）
            }
            var mesh = new Mesh { name = selfSide ? "BattlePriorityPanel_Self" : "BattlePriorityPanel_Opp" };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris; // shader 无 NORMAL 输入，法线可缺
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>边外法线（经验定向：与边中点相对 inward 的朝向一致即外法线，与环向无关）。</summary>
        private static Vector2 EdgeNormal(Vector2 a, Vector2 b, Vector2 inward)
        {
            var d = (b - a).normalized;
            var nn = new Vector2(d.y, -d.x);
            if (Vector2.Dot(nn, (a + b) * 0.5f - inward) < 0f) nn = -nn;
            return nn;
        }

        /// <summary>凸包（Andrew monotone chain；返回逆时针环）。</summary>
        private static List<Vector2> ConvexHull(List<Vector2> pts)
        {
            pts.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            var lower = new List<Vector2>();
            foreach (var p in pts)
            {
                while (lower.Count >= 2 && Cross(lower[lower.Count - 2], lower[lower.Count - 1], p) <= 0f)
                    lower.RemoveAt(lower.Count - 1);
                lower.Add(p);
            }
            var upper = new List<Vector2>();
            for (int i = pts.Count - 1; i >= 0; i--)
            {
                var p = pts[i];
                while (upper.Count >= 2 && Cross(upper[upper.Count - 2], upper[upper.Count - 1], p) <= 0f)
                    upper.RemoveAt(upper.Count - 1);
                upper.Add(p);
            }
            lower.RemoveAt(lower.Count - 1);
            upper.RemoveAt(upper.Count - 1);
            lower.AddRange(upper);
            return lower;
        }

        private static float Cross(Vector2 o, Vector2 a, Vector2 b)
            => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);

        /// <summary>描边环惰性建（每格最多一枚；依赖缺失回落 null=该格无特效）。</summary>
        private static Renderer EnsureRing(int x, int z)
        {
            var key = Key(x, z);
            Renderer r;
            if (_rings.TryGetValue(key, out r) && r != null) return r; // fake-null 判死重建
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
            rend.enabled = false; // 建=熄，SetLandGlow/SetCellRing 时点亮
            _rings[key] = rend;
            return rend;
        }

        /// <summary>环材质+网格一次性就绪（BattleHex mesh 与面板链共享缓存；缺件告警一次回落 false）。</summary>
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
                "Assets/Art/Materials/BattleCardBack_Elem_" + ElemMatNames[lockedColor] + "_Mat.asset");
            return _elemMats[lockedColor];
        }

        private static void WarnDeps(string what)
        {
            if (_depsLogged) return;
            _depsLogged = true;
            Debug.LogWarning("[BattleTileFx] 依赖缺失：" + what + "——地块高光/元素图标降级为不显示");
        }
    }
}
