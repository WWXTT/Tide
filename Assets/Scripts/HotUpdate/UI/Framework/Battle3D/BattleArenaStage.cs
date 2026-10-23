using System.Collections.Generic;
using Tide.HotUpdate;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SynergyUI
{
    /// <summary>
    /// 战场祭坛环境·P0' 真·地编版（2026-10-23 12:45 用户方向纠正）：台面=包内地面预制体拼贴
    ///（Dirt_Flat 6×4 主铺+接缝件打散）、侧壁=崖壁件围圈、细节件只摆棋盘包围盒外——
    /// 废弃 12:38 的"一块大 Cube 当台面"方案。视觉参数全部收在本类（红线#6）。
    ///
    /// 红线：①环境根 EnvRoot 挂 Map 下——绝不进 BattleBoardRoot（浮起动画会把子物体当格子飞）；
    /// ②零碰撞体（拾取=单发 Physics.Raycast 认 tile- 名）——装配完剥全部 Collider；
    /// ③地编纪律（plan §3.5）：一切按实测 bounds 拼接落位（对齐 bounds.min.y，pivot 未必在底面），
    /// 不写死米数；每类件 ≥3 变体交替+yaw/Y 抖动，杜绝网格感。
    /// 层次（低→高）：兜底板(-5.2) < 地面件(-0.9±0.02，顶≈-0.78) < 优先权面片(-0.77，BattlePriorityGlow)
    /// < 金法阵(地面顶+0.02，起伏封顶 CircleTopLimit 不穿盘) < 棋盘(y=0)。
    /// 生命周期：BattleStageDirector.MountBoardAndDawnAsync 装 / DismountBoard 拆（材质实例随 Unmount 显式销毁）。
    /// </summary>
    public static class BattleArenaStage
    {
        // ---- 台面几何（v2：覆盖 24×16m）----
        public const float CenterX = 7.35f;          // 盘心（棋盘世界包围盒中心）
        public const float CenterZ = 3.75f;
        public const float SizeX = 24f;
        public const float SizeZ = 16f;
        public const float TopY = -0.9f;             // 台面基准（地块 y=0，缝下 0.9m 见台面）

        // ---- 法阵 ----
        public const float CircleDiameter = 15f;     // 缩到 14~15m（plan §3.1）
        public const float CircleTopLimit = -0.05f;  // 起伏封顶：不穿棋盘底面（不压 6% 薄片，保留浮雕）

        // ---- 岩壁 ----
        public const float CliffLipJitter = 0.15f;   // 顶嵌沿口 ±（-0.9±0.15）
        public const float CliffHeightMin = 3.2f;    // 露出段高度域（plan 2.5~4 的实装档）
        public const float CliffHeightMax = 5.2f;

        public const float EnvDim = 0.5f;            // 环境材质乘性压暗（亮度纪律：环境暗于棋盘）
        public const int GroundCols = 6, GroundRows = 4; // 主铺 6×4（plan §3.1）

        private const string PrefabDir = "Assets/Art/Battle3D/Environment/Prefab/";

        // 主铺（棋盘投影内只用 Dirt_Flat=最平整；投影外可混草皮）
        private static readonly string[] GroundFlat = { "SM_Env_Ground_Dirt_Flat_01", "SM_Env_Ground_Grass_Flat_01" };
        // 接缝打散件（置于网格缝/边缘，碎屑感）
        private static readonly string[] GroundSeam = { "SM_Env_Ground_Dirt_01", "SM_Env_Ground_Dirt_Small_01", "SM_Env_Ground_Dirt_Round_01" };
        // 岖壁（≥3 变体：崖壁×6+玄武岩柱×4）
        private static readonly string[] CliffSet =
        {
            "SM_Env_Rock_Cliff_01", "SM_Env_Rock_Cliff_02", "SM_Env_Rock_Cliff_03",
            "SM_Env_Rock_Cliff_04", "SM_Env_Rock_Cliff_05", "SM_Env_Rock_Cliff_06",
            "SM_Env_Cliff_Basalt_01", "SM_Env_Cliff_Basalt_02", "SM_Env_Cliff_Basalt_03", "SM_Env_Cliff_Basalt_04",
        };
        private const string ArchPiece = "SM_Env_Rock_Cliff_Arch_01"; // 远端拱形残岩点缀
        private static readonly string[] StairSet = { "SM_Env_Rock_Stairs_01", "SM_Env_Rock_Stairs_02" };
        // 细节件（只摆棋盘包围盒之外；每类 ≥3 变体）
        private static readonly string[] DetailPieces =
        {
            "SM_Env_Rocks_Small_01", "SM_Env_Rocks_Small_02",
            "SM_Env_Moss_01", "SM_Env_Moss_02", "SM_Env_Moss_03", "SM_Env_Moss_04", "SM_Env_Moss_05",
            "SM_Env_Plant_Grass_01", "SM_Env_Plant_Grass_02", "SM_Env_Plant_Grass_Patch_01",
            "SM_Env_Grunge_01", "SM_Env_Grunge_02", "SM_Env_Grunge_03", "SM_Env_Grunge_04", "SM_Env_Grunge_05",
            "SM_Env_Grunge_06", "SM_Env_Grunge_07", "SM_Env_Grunge_08", "SM_Env_Grunge_09", "SM_Env_Grunge_10",
            "SM_Env_Grunge_11", "SM_Env_Grunge_12",
            "SM_Env_Rock_01", "SM_Env_Rock_02", "SM_Env_Rock_03",
        };
        private const string CirclePiece = "SM_Prop_Ritual_Circle_01";

        private static readonly Color CircleGold = new Color(1f, 0.75f, 0.32f); // 暗夜鎏金：光只给暖金
        private static readonly Color CircleWhiteGold = new Color(1f, 0.93f, 0.78f); // v3：白金基色·尽量亮

        private static Transform _root;
        private static Transform _stairsRoot;                  // v3：台阶独立节点（prefab 层级）
        private static bool _owned;                            // true=Mount 运行时铺的独立 EnvRoot（Unmount 显式销毁）；false=Attach（随 stage 根走）
        private static readonly List<Material> _mats = new List<Material>(); // 运行时材质实例——Unmount 显式销毁（仅 _owned 态）
        private static readonly Dictionary<Material, Material> _dimCache = new Dictionary<Material, Material>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _root = null; _stairsRoot = null; _owned = false; _mats.Clear(); _dimCache.Clear(); }

        /// <summary>装环境（幂等；mapRoot=Map 根，恒 identity）。缺件逐类降级告警，不阻塞装台。</summary>
        public static void Mount(Transform mapRoot)
        {
            if (_root != null) return;
            if (mapRoot == null) { Debug.LogWarning("[BattleArenaStage] Map 根缺失——祭坛环境不装"); return; }
            _owned = true;

            var rootGo = new GameObject("EnvRoot");
            _root = rootGo.transform;
            _root.SetParent(mapRoot, false); // 绝不进 BattleBoardRoot

            var rng = new System.Random(20261023); // 固定种子：布局跨局稳定（截图可比对）
            var counts = new Dictionary<string, int>();

            int backing = BuildBacking();
            int ground = BuildGroundGrid(rng, counts);
            int seams = BuildSeamBreakers(rng, counts);
            int circle = BuildCircle(rng);
            int cliffs = BuildCliffRing(rng, counts);
            int stairs = BuildStairs(rng, counts);
            int details = BuildDetails(rng, counts);

            int stripped = 0;
            foreach (var col in _root.GetComponentsInChildren<Collider>(true))
            {
                Object.Destroy(col); // 红线：环境零碰撞体
                stripped++;
            }
            BattleStageCurve.Bend(_root); // v5 凸面台地（回落态同视觉）

            // 计数行（deliverable：每类件摆放计数；bounds 汇总见装载期 [P0'件bounds] 日志）
            var sb = new System.Text.StringBuilder("[BattleArenaStage][PASS] 地编装台：");
            sb.Append($"主铺={ground}（Dirt_Flat×{CountOf(counts, "SM_Env_Ground_Dirt_Flat_01")}/Grass_Flat×{CountOf(counts, "SM_Env_Ground_Grass_Flat_01")} 6×4 pitch按实测5m件）");
            sb.Append($" 接缝件={seams} 法阵={circle}（Ø{CircleDiameter:0} 起伏封顶{CircleTopLimit:0.00}）");
            sb.Append($" 岩壁={cliffs}（崖壁/玄武岩×{cliffs - CountOf(counts, ArchPiece)}+拱岩×{CountOf(counts, ArchPiece)} 台阶={stairs}）");
            sb.Append($" 细节={details}（{DetailSummary(counts)}）");
            sb.Append($" 兜底={backing} 剥碰撞体={stripped}");
            Debug.Log(sb.ToString());
        }

        /// <summary>拆环境（BattleStageDirector.DismountBoard 调）：Mount 态销毁独立 EnvRoot
        /// 与材质实例；Attach 态（prefab）物件随 stage 根销毁，只清引用。</summary>
        public static void Unmount()
        {
            BattleStageCurve.Unwind(); // v5：销毁弯曲克隆网格（先于根销毁，台账清空）
            if (_owned)
            {
                if (_root != null) Object.Destroy(_root.gameObject);
                foreach (var m in _mats) if (m != null) Object.Destroy(m);
                _mats.Clear();
                _dimCache.Clear();
            }
            _root = null;
            _stairsRoot = null;
            _owned = false;
        }

        /// <summary>prefab 态挂载（2026-10-23 v3 预制体化）：Env/Stairs 已烘进 BattleStage.prefab
        ///（材质/点光=子资产），运行时只复查剥碰撞体（红线②零碰撞）。BattleStageDirector 在
        /// Build() 返回 prefab 态时调用（与 Mount 互斥——两态同一 _root）。</summary>
        public static void Attach(Transform envRoot, Transform stairsRoot)
        {
            if (_root != null) Unmount(); // 防御：换态先清旧账
            _root = envRoot;
            _stairsRoot = stairsRoot;
            _owned = false;
            int stripped = 0;
            if (_root != null)
                foreach (var col in _root.GetComponentsInChildren<Collider>(true))
                { Object.Destroy(col); stripped++; }
            if (_stairsRoot != null)
                foreach (var col in _stairsRoot.GetComponentsInChildren<Collider>(true))
                { Object.Destroy(col); stripped++; }
            BattleStageCurve.Bend(_root);          // v5 凸面台地：Env 顶点弯曲（K=0 可一键关）
            if (_stairsRoot != null) BattleStageCurve.Bend(_stairsRoot);
            Debug.Log($"[BattleArenaStage] 预制体 Env 挂载：Env子件={ChildCount(_root)} Stairs子件={ChildCount(_stairsRoot)} 剥碰撞体={stripped}");
        }

        private static int ChildCount(Transform t) => t != null ? t.childCount : 0;

        // ======================================== 装配件 ========================================

        /// <summary>兜底底板（plan §3.1 允许）：URP Lit 近黑无高光（v3：不许 Unlit），
        /// 垫在全场最深处只作遮挡——地面件/岩壁完全盖住其边缘，画面里不作为"台面"可见；
        /// 把裙下漏视的天空间隙终结成暗部（亮度纪律）。</summary>
        private static int BuildBacking()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return 0;
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube); // Collider 结尾统一剥
            slab.name = "arena-backing";
            slab.transform.SetParent(_root, false);
            slab.transform.localPosition = new Vector3(CenterX, TopY - 4.3f, CenterZ);
            slab.transform.localScale = new Vector3(SizeX + 6f, 0.5f, SizeZ + 6f);
            var m = new Material(shader);
            m.color = new Color(0.015f, 0.015f, 0.02f); // 近黑（冷调）
            m.SetFloat("_Smoothness", 0f);              // 无高光
            m.SetFloat("_Metallic", 0f);
            _mats.Add(m);
            var rend = slab.GetComponent<MeshRenderer>();
            rend.sharedMaterial = m;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
            return 1;
        }

        /// <summary>主铺 6×4：5m 地面件按实测 bounds 排格（pitch 反推覆盖 24×16，件间重叠盖缝）；
        /// 棋盘投影内只用最平的 Dirt_Flat，投影外混 Grass_Flat；每块 yaw±12°/Y±0.02 抖动（plan §3.1）。</summary>
        private static int BuildGroundGrid(System.Random rng, Dictionary<string, int> counts)
        {
            var proto = LoadPiece(GroundFlat[0]);
            if (proto == null) { Debug.LogWarning("[BattleArenaStage] 主铺件缺失——台面不铺"); return 0; }
            var b = MeasureLocal(proto.transform);
            float tileW = Mathf.Max(b.size.x, b.size.z);
            float tileT = b.size.y;
            float pitchX = (SizeX - tileW) / (GroundCols - 1); // 反推 pitch：首末件外沿=覆盖域
            float pitchZ = (SizeZ - tileW) / (GroundRows - 1);
            GroundTopApprox = TopY + tileT + 0.02f; // 记给法阵/细节件用（地面件最高顶≈厚+最大抖动）

            int placed = 0;
            for (int cz = 0; cz < GroundRows; cz++)
                for (int cx = 0; cx < GroundCols; cx++)
                {
                    bool underBoard = cx >= 1 && cx <= GroundCols - 2 // 边列/边行多在投影外
                                      && BoardFootprintContains(
                                          CenterX + (cx - (GroundCols - 1) * 0.5f) * pitchX,
                                          CenterZ + (cz - (GroundRows - 1) * 0.5f) * pitchZ, 2.6f);
                    var piece = LoadPiece(underBoard ? GroundFlat[0] : GroundFlat[rng.Next(0, 3) / 2]); // 投影外 1/2 概率草皮
                    if (piece == null) continue;
                    var inst = Object.Instantiate(piece, _root, false);
                    inst.name = "ground-" + cx + "-" + cz;
                    PlaceByMinY(inst.transform,
                        new Vector3(
                            CenterX + (cx - (GroundCols - 1) * 0.5f) * pitchX,
                            TopY + Jitter(rng, 0.02f),
                            CenterZ + (cz - (GroundRows - 1) * 0.5f) * pitchZ),
                        Quaternion.Euler(0f, SnapYaw(rng, 90f, 12f), 0f));
                    DimRenderers(inst);
                    CountPiece(counts, piece.name);
                    placed++;
                }
            return placed;
        }

        /// <summary>接缝打散：非平整件散在网格缝与边带（避开棋盘投影核心，投影内保持平整件）。</summary>
        private static int BuildSeamBreakers(System.Random rng, Dictionary<string, int> counts)
        {
            int placed = 0;
            int target = 14;
            for (int i = 0; i < target; i++)
            {
                var piece = LoadPiece(GroundSeam[i % GroundSeam.Length]);
                if (piece == null) continue;
                // 采样点：全台面域随机，落在棋盘投影内（外扩 1m）则重掷——最多 8 次
                Vector3 pos;
                int guard = 0;
                do
                {
                    pos = new Vector3(
                        CenterX + Range(rng, -SizeX / 2f + 1.5f, SizeX / 2f - 1.5f), TopY,
                        CenterZ + Range(rng, -SizeZ / 2f + 1.5f, SizeZ / 2f - 1.5f));
                    guard++;
                } while (BoardFootprintContains(pos.x, pos.z, 1f) && guard < 8);
                if (BoardFootprintContains(pos.x, pos.z, 1f)) continue;
                var inst = Object.Instantiate(piece, _root, false);
                inst.name = "ground-seam-" + i;
                PlaceByMinY(inst.transform,
                    new Vector3(pos.x, TopY - 0.03f, pos.z), // 略沉：碎屑半埋
                    Quaternion.Euler(0f, rng.Next(0, 360), 0f));
                DimRenderers(inst);
                CountPiece(counts, piece.name);
                placed++;
            }
            return placed;
        }

        /// <summary>金法阵（2026-10-23 13:00 v3 用户定案：URP Lit·白金色基色+尽量亮，
        /// _EmissionColor 照设（本管线 Lit emission 实测不渲染也留着）——发光由台心一盏小型
        /// 暖点光伪造（实时灯预算：主光1+点光1=2≤4）。实测直径缩 Ø15、贴地面顶+0.02、
        /// 起伏封顶不穿盘（不压 6% 薄片）。</summary>
        private static int BuildCircle(System.Random rng)
        {
            var piece = LoadPiece(CirclePiece);
            if (piece == null) { Debug.LogWarning("[BattleArenaStage] 法阵件缺失"); return 0; }
            var inst = Object.Instantiate(piece, _root, false);
            inst.name = "arena-circle";
            var t = inst.transform;
            t.localPosition = new Vector3(CenterX, 0f, CenterZ);
            t.localRotation = Quaternion.Euler(0f, 90f, 0f);

            var b = MeasureLocal(t);
            float s = CircleDiameter / Mathf.Max(b.size.x, b.size.z);
            t.localScale *= s;
            float minY = GroundTopApprox + 0.02f;                 // 贴地面件顶
            float height = b.size.y * s;
            float yScale = Mathf.Min(1f, (CircleTopLimit - minY) / height); // 起伏封顶=不穿盘
            t.localScale = new Vector3(t.localScale.x, t.localScale.y * yScale, t.localScale.z);
            var b2 = MeasureLocal(t);
            t.localPosition = new Vector3(CenterX, minY - b2.min.y, CenterZ);

            foreach (var r in inst.GetComponentsInChildren<MeshRenderer>())
            {
                var src = r.sharedMaterial;
                if (src == null) continue;
                var m = Object.Instantiate(src); // URP Lit 克隆（v3：不许 Unlit 顶替）
                m.color = CircleWhiteGold;      // 白金基色·高亮度
                m.SetFloat("_Smoothness", 0.4f);
                m.SetFloat("_Metallic", 0f);
                m.EnableKeyword("_EMISSION");
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", CircleGold * 2.5f);
                r.sharedMaterial = m;
                _mats.Add(m);
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            // 伪造发光：台心暖点光（无阴影·预算内）
            var lightGo = new GameObject("arena-circle-light");
            lightGo.transform.SetParent(_root, false);
            lightGo.transform.localPosition = new Vector3(CenterX, TopY + 0.45f, CenterZ);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = CircleGold;
            l.intensity = 1.6f;
            l.range = 9f;
            l.shadows = LightShadows.None;

            Debug.Log($"[BattleArenaStage] 法阵实测：Ø={b2.size.x:0.00} 高={b2.size.y:0.00} min.y={minY:0.00}" +
                      $" max.y={minY + b2.size.y:0.00}（贴地面顶{minY:0.00} 起伏缩比{yScale:0.00}）+ 暖点光@{(Vector3)lightGo.transform.position}");
            return 1;
        }

        /// <summary>岩壁围圈：崖壁/玄武岩柱沿台面四周（顶嵌沿口 -0.9±0.15、露出段 3.2~5.2m 高
        /// ——按实测 bounds 反推缩放与落位，山体件余量沉入视线下方）；远端插一座拱岩。
        /// 替代 12:38 版立方体侧沿（竖直拉花来源）。</summary>
        private static int BuildCliffRing(System.Random rng, Dictionary<string, int> counts)
        {
            int placed = 0;
            float perimeter = 2f * (SizeX + SizeZ);
            float spacing = 3.6f;
            int count = Mathf.RoundToInt(perimeter / spacing);
            int archSlot = count / 2; // 远边中点（朝向相机背面）插拱岩
            for (int i = 0; i < count; i++)
            {
                var piece = LoadPiece(i == archSlot ? ArchPiece : CliffSet[i % CliffSet.Length]);
                if (piece == null) continue;
                var inst = Object.Instantiate(piece, _root, false);
                inst.name = "cliff-" + i;
                var t = inst.transform;
                var b = MeasureLocal(t);
                if (b.size.y < 0.1f) { Object.Destroy(inst); continue; }

                // 周长参数取边点+内外抖动；朝向=面向盘心±20°
                var p = RectPoint(SizeX / 2f, SizeZ / 2f, (i + 0.5f) / count);
                var pos = new Vector3(CenterX + p.x + Jitter(rng, 0.7f), 0f, CenterZ + p.y + Jitter(rng, 0.7f));
                float faceYaw = Mathf.Atan2(CenterX - pos.x, CenterZ - pos.z) * Mathf.Rad2Deg;
                t.localRotation = Quaternion.Euler(0f, faceYaw + Jitter(rng, 20f), 0f);

                // 缩放：露出段高度落在域内；顶嵌沿口（bounds.max.y = TopY ± jitter）
                float targetH = Range(rng, CliffHeightMin, CliffHeightMax);
                float scale = targetH / b.size.y;
                t.localScale *= scale;
                float lipTop = TopY + Jitter(rng, CliffLipJitter);
                var b2 = MeasureLocal(t);
                t.localPosition = new Vector3(pos.x, lipTop - b2.max.y, pos.z);
                DimRenderers(inst);
                CountPiece(counts, piece.name);
                placed++;
            }
            return placed;
        }

        /// <summary>近端台阶（z≈-3..-5）：两型阶梯交替、逐级抬升到沿口，面向镜头。</summary>
        private static int BuildStairs(System.Random rng, Dictionary<string, int> counts)
        {
            int placed = 0;
            for (int i = 0; i < 3; i++)
            {
                var piece = LoadPiece(StairSet[i % StairSet.Length]);
                if (piece == null) continue;
                var inst = Object.Instantiate(piece, _root, false);
                inst.name = "stairs-" + i;
                var t = inst.transform;
                t.localRotation = Quaternion.Euler(0f, 180f + Jitter(rng, 8f), 0f); // 面向 -z 镜头
                float stepX = CenterX + (i - 1) * 2.8f + Jitter(rng, 0.4f);
                float stepZ = -3.0f - i * 1.15f;                  // 近端向外逐级
                float topY = TopY + 0.1f - i * 0.75f;             // 逐级下降（内高外低）
                var b = MeasureLocal(t);
                t.localPosition = new Vector3(stepX, topY - b.max.y, stepZ);
                DimRenderers(inst);
                CountPiece(counts, piece.name);
                placed++;
            }
            return placed;
        }

        /// <summary>细节散件：碎石/苔藓/草/污渍/岩石只摆棋盘包围盒之外（外扩 0.4m 保护区），
        /// 坐在地面件顶（bounds.min.y ≈ 地面顶，随抖动半埋）。</summary>
        private static int BuildDetails(System.Random rng, Dictionary<string, int> counts)
        {
            int placed = 0;
            int target = 48;
            for (int i = 0; i < target; i++)
            {
                var piece = LoadPiece(DetailPieces[i % DetailPieces.Length]);
                if (piece == null) continue;
                Vector3 pos;
                int guard = 0;
                do
                {
                    pos = new Vector3(
                        CenterX + Range(rng, -SizeX / 2f + 1f, SizeX / 2f - 1f), 0f,
                        CenterZ + Range(rng, -SizeZ / 2f + 1f, SizeZ / 2f - 1f));
                    guard++;
                } while (BoardFootprintContains(pos.x, pos.z, 0.4f) && guard < 10);
                if (BoardFootprintContains(pos.x, pos.z, 0.4f)) continue;
                var inst = Object.Instantiate(piece, _root, false);
                inst.name = "detail-" + i;
                PlaceByMinY(inst.transform,
                    new Vector3(pos.x, GroundTopApprox - 0.06f + Range(rng, 0f, 0.05f), pos.z),
                    Quaternion.Euler(0f, rng.Next(0, 360), 0f));
                DimRenderers(inst);
                CountPiece(counts, piece.name);
                placed++;
            }
            return placed;
        }

        // ======================================== 工具 ========================================

        /// <summary>地面件顶面近似（BuildGroundGrid 实测回填；法阵/细节件落位基准。
        /// 默认值=厚 0.1+抖动 0.02 的先验，防主铺缺件时法阵沉底）。</summary>
        private static float GroundTopApprox { get; set; } = TopY + 0.12f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetGroundTop() => GroundTopApprox = TopY + 0.12f;

        /// <summary>棋盘世界包围盒（BattleBoardGrid 口径：8×7 盘 + 根偏移 (0.866,·,-0.75)）。</summary>
        private static readonly float BoardMinX = 0.866f, BoardMaxX = 0.866f + 7 * 1.732f + 1f;
        private static readonly float BoardMinZ = -0.75f, BoardMaxZ = -0.75f + 6 * 1.5f + 1f;

        private static bool BoardFootprintContains(float x, float z, float margin)
            => x > BoardMinX - margin && x < BoardMaxX + margin && z > BoardMinZ - margin && z < BoardMaxZ + margin;

        private static GameObject LoadPiece(string name)
            => HotUpdateAssets.Load<GameObject>(name, PrefabDir + name + ".prefab");

        /// <summary>按实测 bounds.min.y 对齐落位（pivot 未必在底面——plan §3.5 纪律）。</summary>
        private static void PlaceByMinY(Transform t, Vector3 worldMinTarget, Quaternion rot)
        {
            t.localRotation = rot;
            var b = MeasureLocal(t);
            t.localPosition = new Vector3(worldMinTarget.x, worldMinTarget.y - b.min.y, worldMinTarget.z);
        }

        /// <summary>局部系包围盒（EnvRoot 恒 identity：局部=世界；prefab 原生缩放参与）。</summary>
        private static Bounds MeasureLocal(Transform t)
        {
            var b = new Bounds();
            var has = false;
            foreach (var r in t.GetComponentsInChildren<MeshRenderer>())
            {
                var e = r.bounds; // 世界包围（实例未定位前=原点位姿系）
                if (!has) { b = e; has = true; }
                else b.Encapsulate(e);
            }
            return b;
        }

        /// <summary>环境件统一乘性压暗（包内共享材质不可改资产——按源材质缓存压暗实例换绑）。</summary>
        private static void DimRenderers(GameObject inst)
        {
            foreach (var r in inst.GetComponentsInChildren<MeshRenderer>())
            {
                var src = r.sharedMaterial;
                if (src == null) continue;
                Material dim;
                if (!_dimCache.TryGetValue(src, out dim))
                {
                    dim = Object.Instantiate(src);
                    var c = dim.color;
                    dim.color = new Color(c.r * EnvDim, c.g * EnvDim, c.b * EnvDim, c.a);
                    _dimCache[src] = dim;
                    _mats.Add(dim);
                }
                r.sharedMaterial = dim;
            }
        }

        /// <summary>矩形边取点（halfW/halfD 半宽深，t∈[0,1) 沿周长走一圈；近边起点）。</summary>
        private static Vector2 RectPoint(float halfW, float halfD, float t)
        {
            float x, y;
            float seg = 2f * halfW + 2f * halfD;
            float d = t * seg;
            if (d < 2f * halfW) { x = -halfW + d; y = -halfD; }
            else if (d < 2f * halfW + 2f * halfD) { d -= 2f * halfW; x = halfW; y = -halfD + d; }
            else if (d < 4f * halfW + 2f * halfD) { d -= 2f * halfW + 2f * halfD; x = halfW - d; y = halfD; }
            else { d -= 4f * halfW + 2f * halfD; x = -halfW; y = halfD - d; }
            return new Vector2(x, y);
        }

        private static float Jitter(System.Random rng, float amp) => (float)(rng.NextDouble() * 2 - 1) * amp;
        private static float Range(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);

        /// <summary>90° 步进 + ±12° 抖动（地面件接缝方向感打散但保持边缘对齐）。</summary>
        private static float SnapYaw(System.Random rng, float step, float jitter)
            => rng.Next(0, 4) * step + Jitter(rng, jitter);

        private static void CountPiece(Dictionary<string, int> counts, string name)
        {
            int v; counts.TryGetValue(name, out v);
            counts[name] = v + 1;
        }

        private static int CountOf(Dictionary<string, int> counts, string name)
        {
            int v; counts.TryGetValue(name, out v);
            return v;
        }

        private static string DetailSummary(Dictionary<string, int> counts)
        {
            int rocksSmall = 0, moss = 0, grass = 0, grunge = 0, rocks = 0;
            foreach (var kv in counts)
            {
                if (kv.Key.StartsWith("SM_Env_Rocks_Small")) rocksSmall += kv.Value;
                else if (kv.Key.StartsWith("SM_Env_Moss")) moss += kv.Value;
                else if (kv.Key.StartsWith("SM_Env_Plant")) grass += kv.Value;
                else if (kv.Key.StartsWith("SM_Env_Grunge")) grunge += kv.Value;
                else if (kv.Key.StartsWith("SM_Env_Rock_")) rocks += kv.Value;
            }
            return $"碎石×{rocksSmall} 苔×{moss} 草×{grass} 污渍×{grunge} 岩×{rocks}";
        }
    }
}
