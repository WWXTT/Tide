using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tide.验证
{
    /// <summary>
    /// 战场 v6 重构（2026-10-23 §11 定案：双读数视角 + 舒适配色）——编辑态一次性烘焙进
    /// BattleStage.prefab，幂等可重跑（bowl-terrain/bowl-props 先删后建；外圈件抬升按公式确定）。
    ///
    /// 一、碗形台地：中央平顶=用户手调平台原样不动；平顶圆角矩形边界外按对数剖面起坡
    /// （坡脚切线 52°∈45–60°，正俯视下四周读作 30–45°），坡到画面边缘外再收口（上翘檐堵漏视）。
    /// 旧平铺外圈件（detail/added-plants/added-landmarks 里落在边界外的）按「保方位角、抬上坡面」
    /// 重落位：新距离=原距离夹到[0.5,6]m，姿态=坡面法线对齐+保留朝向。
    /// 二、配色（§11.2 色板落材质）：25 个 BattleTile_Zone*.mat 按对象上色+格边 _Smoothness 0.2；
    /// 台面/岩体调暗；植物三色变体替换 v4 高饱和件；法阵金收口 #C6A24B；篝火双色灯。
    /// 红线：环境零碰撞体、实时灯 ≤4（主光+法阵金+篝火×2）、参数全收本类。
    /// </summary>
    public static class BattleV6Rebuild
    {
        private const string MenuRoot = "Tools/战场/v6 碗形重构";
        private const string StagePrefabPath = "Assets/Art/Battle3D/Prefab/BattleStage.prefab";
        private const string MatDir = "Assets/Art/Battle3D/Materials";
        private const string MeshDir = "Assets/Art/Battle3D/Mesh";
        private const string EnvPrefabDir = "Assets/Art/Battle3D/Environment/Prefab";
        private const string PackRoot = "Assets/Packages/PolygonDarkFantasy/Prefabs";

        // ---- 碗形几何（§11.1；盘心与 BattleStageCurve 同口径）----
        private const float Cx = 7.35f, Cz = 3.75f;
        private const float HalfX = 9.6f, HalfZ = 7.0f, CornerR = 3.2f; // 平顶圆角矩形（棋盘角净距≥1.5m）
        private const float FootY = -1.08f;      // 坡脚高程（藏进平台平片下）
        private const float Tuck = -0.8f;        // 起坡内敛（塞到平台边下封缝）
        private const float ProfA = 5f, ProfB = 3.9f; // y=A·ln(1+d/B)：坡脚切线 A/B≈1.28=52°，外缘趋平
        private const float RunMax = 22f;        // 径向铺到画面边缘外
        private const float LipUp = 2.2f;        // 末端上翘檐：杜绝檐外漏视
        private const int Columns = 128;
        private const float UvTile = 3.2f;

        // ---- 摆件 ----
        private const int FootRocks = 24, Trees = 7, Walls = 6, SlopePlants = 30;

        // ---- §11.2 色板 ----
        private static Color HexOf(string s) { Color c; ColorUtility.TryParseHtmlString(s, out c); return c; }
        private static readonly Color CBackdrop = HexOf("#0B0A09");
        private static readonly Color CGap = HexOf("#14120F");
        private static readonly Color CSelf = HexOf("#4A6478");
        private static readonly Color COpp = HexOf("#6B4038");
        private static readonly Color CNeutral = HexOf("#3A3630");
        private static readonly Color CMiddle = HexOf("#1A1816");
        private static readonly Color CGold = HexOf("#C6A24B");
        private static readonly Color CPlantA = HexOf("#56694C");
        private static readonly Color CPlantB = HexOf("#5F6F7A");
        private static readonly Color CPlantC = HexOf("#6B5566");
        private static readonly Color CFireWarm = HexOf("#B5652E");
        private static readonly Color CFireCold = HexOf("#4A6FA5");

        [MenuItem(MenuRoot + "/一键（碗形+摆件+配色）")]
        public static void RunAll()
        {
            var log = new List<string>();
            var root = PrefabUtility.LoadPrefabContents(StagePrefabPath);
            try
            {
                var env = root.transform.Find("Env");
                if (env == null) { Debug.LogError("[V6Rebuild] FAIL：prefab 无 Env 节点"); return; }
                BuildBowl(env, log);
                BuildProps(env, log);
                RelocateOuter(env, log);
                RepaintEnv(env, log);
                int cols = 0;
                foreach (var c in env.GetComponentsInChildren<Collider>(true)) { Object.DestroyImmediate(c); cols++; }
                PrefabUtility.SaveAsPrefabAsset(root, StagePrefabPath);
                log.Add("剥碰撞体=" + cols);
                log.Add("Env 子件=" + env.childCount);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            ApplyZonePalette(log);
            AssetDatabase.SaveAssets();
            Debug.Log("[V6Rebuild][PASS] " + string.Join(" ; ", log));
        }

        // ======================================== 碗形网格 ========================================

        private static float YAt(float d) { return d <= 0f ? FootY : FootY + ProfA * Mathf.Log(1f + d / ProfB); }
        private static float SlopeTan(float d) { return d <= 0f ? 0f : ProfA / (ProfB + d); }

        /// <summary>平顶圆角矩形边界点（外法线 n 精确=n；圆角矩形=核矩形⊕圆盘的支撑映射）。</summary>
        private static Vector2 BoundaryPoint(Vector2 n)
        {
            float sx = n.x >= 0f ? 1f : -1f, sz = n.y >= 0f ? 1f : -1f;
            return new Vector2(Cx + (HalfX - CornerR) * sx + CornerR * n.x, Cz + (HalfZ - CornerR) * sz + CornerR * n.y);
        }

        private static void BuildBowl(Transform env, List<string> log)
        {
            var old = env.Find("bowl-terrain");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            // 环距采样：坡脚密（曲率高）、外缘疏
            var ds = new List<float>();
            for (float d = Tuck; d < 3f; d += 0.5f) ds.Add(d);
            for (float d = 3f; d < RunMax; d += 1f) ds.Add(d);
            ds.Add(RunMax);
            int rings = ds.Count;

            var verts = new Vector3[(rings + 1) * Columns]; // +1 末端上翘檐
            var uvs = new Vector2[verts.Length];
            var norms = new Vector3[verts.Length];
            for (int k = 0; k < rings; k++)
            {
                float d = ds[k], y = YAt(d), t = SlopeTan(d);
                for (int i = 0; i < Columns; i++)
                {
                    float th = i * 2f * Mathf.PI / Columns;
                    var n = new Vector2(Mathf.Cos(th), Mathf.Sin(th));
                    var p = BoundaryPoint(n) + n * d;
                    int idx = k * Columns + i;
                    verts[idx] = new Vector3(p.x, y, p.y);
                    uvs[idx] = new Vector2(p.x / UvTile, p.y / UvTile);
                    var nn = new Vector3(-n.x * t, 1f, -n.y * t).normalized;
                    // 全碗法线立直（21:35 修订终案：单方向光下背光坡=环境光-only 死黑（实测 24.6=albedo
                    // 0.10×ambient 0.19 模型吻合），坡脚带留 15% 真倾角做斜面读数暗示；坡感由剪影/
                    // 贴图压缩/三带 tint/坡上件姿态承载）
                    float soften = 0.85f + 0.15f * Mathf.Clamp01(d / 2f);
                    nn = Vector3.Slerp(nn, Vector3.up, soften).normalized;
                    norms[idx] = nn;
                }
            }
            // 檐：末环同径上抬（法线转水平向外）
            int last = (rings - 1) * Columns;
            for (int i = 0; i < Columns; i++)
            {
                int idx = rings * Columns + i;
                verts[idx] = verts[last + i] + new Vector3(0f, LipUp, 0f);
                uvs[idx] = uvs[last + i];
                float th = i * 2f * Mathf.PI / Columns;
                norms[idx] = new Vector3(Mathf.Cos(th), 0f, Mathf.Sin(th));
            }
            var tris = new int[rings * Columns * 6];
            int w = 0;
            for (int k = 0; k < rings; k++)
                for (int i = 0; i < Columns; i++)
                {
                    int a = k * Columns + i, b = k * Columns + (i + 1) % Columns;
                    int c = a + Columns, d2 = b + Columns;
                    tris[w++] = a; tris[w++] = b; tris[w++] = c;
                    tris[w++] = b; tris[w++] = d2; tris[w++] = c;
                }
            // v6 21:35 修订·碗面明暗层次（告别亮灰墙）：一条坡拆三带（坡脚暖亮/中带/外带最暗），
            // 各带独立材质（tint 阶梯 + 差异化 tiling），单 renderer 三 submesh。
            int bandAEnd = 10, bandBEnd = 19; // 环序号上界（d≈4 / d≈13 分带）
            var mesh = new Mesh { name = "BattleBowl", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.vertices = verts; mesh.uv = uvs; mesh.normals = norms;
            mesh.subMeshCount = 3;
            var subA = new int[bandAEnd * Columns * 6];
            var subB = new int[(bandBEnd - bandAEnd) * Columns * 6];
            var subC = new int[tris.Length - bandBEnd * Columns * 6];
            System.Array.Copy(tris, 0, subA, 0, subA.Length);
            System.Array.Copy(tris, bandAEnd * Columns * 6, subB, 0, subB.Length);
            System.Array.Copy(tris, bandBEnd * Columns * 6, subC, 0, subC.Length);
            mesh.SetTriangles(subA, 0); mesh.SetTriangles(subB, 1); mesh.SetTriangles(subC, 2);
            mesh.RecalculateBounds();
            Directory.CreateDirectory(MeshDir);
            var meshPath = MeshDir + "/BattleBowl.asset";
            if (File.Exists(meshPath)) AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);

            var go = new GameObject("bowl-terrain");
            go.transform.SetParent(env, false);
            var mf = go.AddComponent<MeshFilter>(); mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[] { BowlRockMat("_A", 0.28f, 0.25f, 0.23f, 1f),   // 坡脚带（暖·最亮）
                                         BowlRockMat("_B", 0.25f, 0.23f, 0.21f, 1.4f),  // 中带（tiling 错开）
                                         BowlRockMat("_C", 0.23f, 0.215f, 0.20f, 0.75f) }; // 外带（最暗·大格）
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // 碗体自投影关（21:35 修订：凹面挡光致单侧死黑；层次靠三带 tint）
            mr.receiveShadows = true;
            var legacy = MatDir + "/BattleBowlRock.mat";
            if (File.Exists(legacy)) AssetDatabase.DeleteAsset(legacy); // 单带版退役
            log.Add("碗形网格：环=" + rings + " 列=" + Columns + " 顶点=" + verts.Length + " 三角=" + tris.Length / 3 +
                    " 坡脚坡度=" + (Mathf.Atan(SlopeTan(0.01f)) * Mathf.Rad2Deg).ToString("0.#") + "° 三带分层=10/9/" + (rings - bandBEnd));
        }

        // ======================================== 坡上摆件 ========================================

        private static void BuildProps(Transform env, List<string> log)
        {
            var old = env.Find("bowl-props");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var propRoot = new GameObject("bowl-props").transform;
            propRoot.SetParent(env, false);
            var rng = new System.Random(20261023);

            string[] rockPrefabs = { "SM_Env_Rock_Cliff_01", "SM_Env_Rock_Cliff_03", "SM_Env_Rock_Cliff_05", "SM_Env_Cliff_Basalt_02", "SM_Env_Rock_Cliff_02", "SM_Env_Cliff_Basalt_01" };
            string[] treePrefabs = { "SM_Env_Tree_Dead_02", "SM_Env_Tree_Dead_01", "SM_Env_Tree_Dead_03", "SM_Env_Tree_Dead_04" };
            string[] wallPrefabs = { "SM_Bld_Wall_Ruin_03", "SM_Bld_Wall_Ruin_01", "SM_Bld_Wall_Ruin_05", "SM_Bld_Wall_Ruin_02" };
            string[] plantPrefabs = { "SM_Env_Moss_02", "SM_Env_Moss_04", "SM_Env_Plant_Grass_Patch_01", "SM_Env_Moss_01", "SM_Env_Plant_Grass_02" };
            var rockMat = DimRockMat();

            int placed = 0;
            // 坡脚岩环（封平台↔坡面接缝）——目标体量 1.2-2.2m
            for (int i = 0; i < FootRocks; i++)
            {
                float az = i * 2f * Mathf.PI / FootRocks + (float)(rng.NextDouble() - 0.5) * 0.12f;
                float d = -0.25f + (float)rng.NextDouble() * 1.15f;
                var p = PlaceOnSlope(propRoot, rockPrefabs[i % rockPrefabs.Length], az, d, rng,
                    1.2f + (float)rng.NextDouble() * 1.0f, sink: 0.32f, tiltToSlope: true);
                if (p == null) continue;
                RepaintPiece(p, rockMat);
                placed++;
            }
            // 枯树 / 断墙（坡中段剪影）——树 3.2-4.8m、墙 2.6-3.8m
            for (int i = 0; i < Trees; i++)
            {
                float az = 0.45f + i * (2f * Mathf.PI / Trees) + (float)(rng.NextDouble() - 0.5) * 0.2f;
                var p = PlaceOnSlope(propRoot, treePrefabs[i % treePrefabs.Length], az, 1.2f + (float)rng.NextDouble() * 2.0f, rng,
                    3.2f + (float)rng.NextDouble() * 1.6f, sink: 0.28f, tiltToSlope: true);
                if (p != null) placed++;
            }
            for (int i = 0; i < Walls; i++)
            {
                float az = 1.0f + i * (2f * Mathf.PI / Walls) + (float)(rng.NextDouble() - 0.5) * 0.25f;
                var p = PlaceOnSlope(propRoot, wallPrefabs[i % wallPrefabs.Length], az, 1.8f + (float)rng.NextDouble() * 2.6f, rng,
                    2.6f + (float)rng.NextDouble() * 1.2f, sink: 0.42f, tiltToSlope: true);
                if (p == null) continue;
                foreach (var r in p.GetComponentsInChildren<MeshRenderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                placed++;
            }
            // 坡面植物（§11.2 三色灰调变体）——0.6-1.0m
            var plantMats = new[] { PlantMat("A", CPlantA), PlantMat("B", CPlantB), PlantMat("C", CPlantC) };
            for (int i = 0; i < SlopePlants; i++)
            {
                float az = (float)rng.NextDouble() * 2f * Mathf.PI;
                float d = 0.35f + (float)rng.NextDouble() * 3.6f;
                var p = PlaceOnSlope(propRoot, plantPrefabs[i % plantPrefabs.Length], az, d, rng,
                    0.6f + (float)rng.NextDouble() * 0.4f, sink: 0.12f, tiltToSlope: true);
                if (p == null) continue;
                RepaintPiece(p, plantMats[i % 3]);
                placed++;
            }
            // 双色篝火（左冷蓝/右暖橙；火焰本体=VFX 色相保留、尺寸收小；灯+篝火各一）
            placed += BuildFire(propRoot, Mathf.PI, 1.5f, CFireCold, true);
            placed += BuildFire(propRoot, 0f, 1.5f, CFireWarm, false);
            // 硬约束（§11.1）：坡上件不得遮挡棋盘投影——冠幅/墙身越界的径向外推
            int pushed = 0;
            foreach (Transform p in propRoot)
                if (PushClearOfBoard(p)) pushed++;
            if (pushed > 0) log.Add("坡上件推出棋盘投影=" + pushed);
            log.Add("坡上摆件=" + placed + "（岩环" + FootRocks + "/树" + Trees + "/墙" + Walls + "/植物" + SlopePlants + "/篝火2）");
        }

        /// <summary>棋盘世界包围盒（XZ 足印，外扩 0.5）——遮挡禁区。</summary>
        private static readonly Rect BoardFootprint = Rect.MinMaxRect(0.2f - 0.5f, -1.5f - 0.5f, 14.5f + 0.5f, 9f + 0.5f);

        /// <summary>件包围盒（XZ）压到棋盘足印=遮挡风险（正俯视）：沿径向外推 0.6m/步直到清空（≤10 步）。
        /// 返回是否推过。低矮件（岩环/植物/篝火）通常不触界，树冠/墙身触界即外推。</summary>
        private static bool PushClearOfBoard(Transform t)
        {
            bool pushed = false;
            for (int step = 0; step < 10; step++)
            {
                var b = new Bounds(); bool f = true;
                foreach (var r in t.GetComponentsInChildren<MeshRenderer>(true)) { if (f) { b = r.bounds; f = false; } else b.Encapsulate(r.bounds); }
                if (f) return pushed;
                var rect = Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z);
                if (!BoardFootprint.Overlaps(rect)) return pushed;
                var n = new Vector2(t.position.x - Cx, t.position.z - Cz).normalized;
                if (n.sqrMagnitude < 0.001f) n = Vector2.right;
                t.position += new Vector3(n.x, 0f, n.y) * 0.6f;
                // 坡面高度跟随（保持贴坡：按新距离重落 y）
                float d = DistToBoundary(t.position);
                var up = new Vector3(-n.x * SlopeTan(d), 1f, -n.y * SlopeTan(d)).normalized;
                var yaw = t.eulerAngles.y;
                t.position = new Vector3(t.position.x, YAt(d) - 0.25f, t.position.z);
                t.rotation = Quaternion.AngleAxis(yaw, up) * Quaternion.FromToRotation(Vector3.up, up);
                pushed = true;
            }
            return pushed;
        }

        /// <summary>点到平顶边界的近似径向距离（>0=界外坡上）。</summary>
        private static float DistToBoundary(Vector3 p)
        {
            var off = new Vector2(p.x - Cx, p.z - Cz);
            if (off.magnitude < 0.01f) return 0f;
            var n = off.normalized;
            var b = BoundaryPoint(n);
            return Vector2.Dot(off - (b - new Vector2(Cx, Cz)), n);
        }

        private static GameObject PlaceOnSlope(Transform parent, string prefabName, float az, float d,
            System.Random rng, float targetSize, float sink, bool tiltToSlope)
        {
            var proto = LoadPiece(prefabName);
            if (proto == null) return null;
            // 实测定尺寸（§3.5 地编纪律）：包内岩壁/枯树原生 10-20m， BlindScale 会盖满全场——
            // 按 proto 最大维度反推 scale，落位即目标体量。
            var protoRend = proto.GetComponentInChildren<MeshRenderer>(true);
            if (protoRend == null) return null;
    var protoSize = protoRend.bounds.size;
            float maxDim = Mathf.Max(protoSize.x, protoSize.y, protoSize.z);
            if (maxDim < 0.01f) return null;
            float scale = targetSize / maxDim * (0.85f + (float)rng.NextDouble() * 0.35f);
            var go = (GameObject)Object.Instantiate(proto, parent);
            go.name = "slope-" + prefabName;
            var n = new Vector2(Mathf.Cos(az), Mathf.Sin(az));
            var p = BoundaryPoint(n) + n * d;
            float yaw = (float)rng.NextDouble() * 360f;
            var up = tiltToSlope ? new Vector3(-n.x * SlopeTan(d), 1f, -n.y * SlopeTan(d)).normalized : Vector3.up;
            go.transform.localPosition = new Vector3(p.x, YAt(d) - sink, p.y);
            go.transform.localRotation = Quaternion.AngleAxis(yaw, up) * Quaternion.FromToRotation(Vector3.up, up);
            go.transform.localScale = Vector3.one * scale;
            foreach (var col in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);
            foreach (var rr in go.GetComponentsInChildren<MeshRenderer>(true))
                rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // 红线§6.3：小物件关投影（墙在 BuildProps 单独开）
            return go;
        }

        private static int BuildFire(Transform parent, float az, float d, Color lightColor, bool coldFire)
        {
            var group = new GameObject(coldFire ? "fire-cold" : "fire-warm");
            group.transform.SetParent(parent, false);
            var n = new Vector2(Mathf.Cos(az), Mathf.Sin(az));
            var p = BoundaryPoint(n) + n * d;
            group.transform.localPosition = new Vector3(p.x, YAt(d) - 0.1f, p.y);
            group.transform.localScale = Vector3.one * 0.62f; // 火焰尺寸收小（§11.2）

            var pit = LoadPiece("SM_Prop_Fire_Pit_01");
            if (pit != null) { var g = (GameObject)Object.Instantiate(pit, group.transform, false); g.name = "pit"; }
            var fx = LoadPiece("FX_Fire_01");
            if (fx != null)
            {
                var f = (GameObject)Object.Instantiate(fx, group.transform, false);
                f.name = "flame";
                foreach (var ps in f.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = ps.main;
                    main.startSizeMultiplier *= 0.7f;
                    if (coldFire) main.startColor = new ParticleSystem.MinMaxGradient(lightColor * 1.25f);
                }
            }
            var lightGo = new GameObject("glow");
            lightGo.transform.SetParent(group.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point; l.color = lightColor; l.intensity = 1.15f; l.range = 6.5f;
            l.shadows = LightShadows.None;
            return 1;
        }

        private static GameObject LoadPiece(string name)
        {
            var local = AssetDatabase.LoadAssetAtPath<GameObject>(EnvPrefabDir + "/" + name + ".prefab");
            if (local != null) return local;
            foreach (var sub in new[] { "Environment", "Props", "Building", "FX" })
            {
                var p = PackRoot + "/" + sub + "/" + name + ".prefab";
                if (File.Exists(p))
                {
                    AssetDatabase.CopyAsset(p, EnvPrefabDir + "/" + name + ".prefab");
                    AssetDatabase.SaveAssets();
                    return AssetDatabase.LoadAssetAtPath<GameObject>(EnvPrefabDir + "/" + name + ".prefab");
                }
            }
            Debug.LogWarning("[V6Rebuild] 缺件：" + name);
            return null;
        }

        // ======================================== 旧外圈件抬升上坡 ========================================

        /// <summary>detail-*/added-* 组里落在平顶边界外的子件：保方位角，距离夹到[0.5,6]m 抬上坡面，
        /// 姿态对齐坡法线+保留朝向。边界内（含全部平台平片/法阵）原样不动。</summary>
        private static void RelocateOuter(Transform env, List<string> log)
        {
            int moved = 0;
            foreach (Transform group in env)
            {
                if (!group.name.StartsWith("detail-") && !group.name.StartsWith("added-")) continue;
                foreach (Transform piece in group)
                {
                    var pos = piece.position;
                    var off = new Vector2(pos.x - Cx, pos.z - Cz);
                    if (off.magnitude < 0.01f) continue;
                    var n = off.normalized;
                    var boundary = BoundaryPoint(n);
                    float dNow = Vector2.Dot(off - (boundary - new Vector2(Cx, Cz)), n);
                    if (dNow <= -0.5f) continue; // 平台内不动
                    float dNew = Mathf.Clamp(dNow, 0.5f, 6f);
                    var yaw = piece.eulerAngles.y;
                    var up = new Vector3(-n.x * SlopeTan(dNew), 1f, -n.y * SlopeTan(dNew)).normalized;
                    var p = boundary + n * dNew;
                    bool big = group.name.StartsWith("added-landmarks");
                    piece.position = new Vector3(p.x, YAt(dNew) - (big ? 0.55f : 0.15f), p.y);
                    piece.rotation = Quaternion.AngleAxis(yaw, up) * Quaternion.FromToRotation(Vector3.up, up);
                    PushClearOfBoard(piece); // 大件冠幅压棋盘足印时径向外推（含 landmarks）
                    moved++;
                }
            }
            log.Add("外圈件抬升上坡=" + moved);
        }

        // ======================================== Env 材质 ========================================

        private static void RepaintEnv(Transform env, List<string> log)
        {
            var rockMat = DimRockMat();
            var plantMats = new[] { PlantMat("A", CPlantA), PlantMat("B", CPlantB), PlantMat("C", CPlantC) };
            var dimCache = new Dictionary<string, Material>();
            int plants = 0, rocks = 0, dimmed = 0;
            foreach (var r in env.GetComponentsInChildren<MeshRenderer>(true))
            {
                var ms = r.sharedMaterials;
                bool dirty = false;
                for (int i = 0; i < ms.Length; i++)
                {
                    var m = ms[i];
                    if (m == null) continue;
                    if (m.name == "PolygonDarkFantasy_Mat_01_A" || m.name == "StageDim_PolygonDarkFantasy_Mat_01_A")
                    { ms[i] = rockMat; rocks++; dirty = true; }
                    else if (m.name == "Moss_01" || m.name == "StageDim_Moss_01" ||
                             m.name.StartsWith("BattlePlantVivid"))
                    { ms[i] = plantMats[(plants++) % 3]; dirty = true; }
                    else if (m.name == "StageCircle_Gold" || m.name.StartsWith("BattleDim") || m.name.StartsWith("BattlePlantMoss") || m.name.StartsWith("BattleBowlRock"))
                    { /* 本轮已收口的变体 */ }
                    else
                    {
                        // 通配：包内其余材质（Stucco/Roof/Glass 等）一律克隆暗变体——85% 暗纪律，不动包资产
                        var path = AssetDatabase.GetAssetPath(m);
                        if (path.StartsWith("Assets/Packages/PolygonDarkFantasy"))
                        {
                            Material dim;
                            if (!dimCache.TryGetValue(m.name, out dim))
                            {
                                var dimPath = MatDir + "/BattleDim_" + m.name + ".mat";
                                dim = AssetDatabase.LoadAssetAtPath<Material>(dimPath);
                                if (dim == null)
                                {
                                    dim = Object.Instantiate(m);
                                    dim.name = "BattleDim_" + m.name;
                                    AssetDatabase.CreateAsset(dim, dimPath);
                                }
                                dim.SetColor("_BaseColor", new Color(0.32f, 0.30f, 0.27f)); // 暖灰暗化（×贴图·二档压暗）
                                dim.SetFloat("_Smoothness", 0.3f);
                                dim.SetFloat("_Metallic", 0f);
                                EditorUtility.SetDirty(dim);
                                dimCache[m.name] = dim;
                            }
                            ms[i] = dim; dimmed++; dirty = true;
                        }
                    }
                }
                if (dirty) r.sharedMaterials = ms;
            }
            // v6 21:35 修订：紫色变体收量收个头——C 件每 3 件 1 件转 B，其余 ×0.72（名字后缀防重缩）
            var cRoots = new List<Transform>();
            foreach (Transform group in env)
            {
                if (!(group.name == "bowl-props" || group.name.StartsWith("added-") || group.name.StartsWith("detail-"))) continue;
                foreach (Transform piece in group)
                {
                    bool isC = false;
                    foreach (var rr in piece.GetComponentsInChildren<MeshRenderer>(true))
                        foreach (var mm in rr.sharedMaterials)
                            if (mm != null && mm.name == "BattlePlantMoss_C") isC = true;
                    if (isC) cRoots.Add(piece);
                }
            }
            cRoots.Sort((x, y) => string.CompareOrdinal(x.name, y.name));
            int cToB = 0, cShrunk = 0;
            for (int i = 0; i < cRoots.Count; i++)
            {
                var piece = cRoots[i];
                if (i % 3 == 0)
                {
                    foreach (var rr in piece.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        var ms2 = rr.sharedMaterials; bool dirty2 = false;
                        for (int j = 0; j < ms2.Length; j++)
                            if (ms2[j] != null && ms2[j].name == "BattlePlantMoss_C") { ms2[j] = plantMats[1]; dirty2 = true; }
                        if (dirty2) rr.sharedMaterials = ms2;
                    }
                    cToB++;
                    continue;
                }
                if (!piece.name.EndsWith("(c72)"))
                {
                    piece.localScale = piece.localScale * 0.72f;
                    piece.name = piece.name + "(c72)";
                    cShrunk++;
                }
            }
            log.Add("紫植物收口：C→B=" + cToB + " 缩小=" + cShrunk + "（余C=" + (cRoots.Count - cToB) + "）");

            // 法阵金收口（基色提亮+既有金点光）
            var circle = env.Find("arena-circle");
            if (circle != null)
            {
                var matPath = MatDir + "/StageCircle_Gold.mat";
                Material gold = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                foreach (var r in circle.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var m = r.sharedMaterial;
                    if (m == null || !m.HasProperty("_BaseColor")) continue;
                    if (gold == null)
                    {
                        gold = Object.Instantiate(m);
                        gold.name = "StageCircle_Gold";
                        AssetDatabase.CreateAsset(gold, matPath);
                    }
                    r.sharedMaterial = gold;
                }
                if (gold != null) { gold.SetColor("_BaseColor", new Color(0.55f, 0.45f, 0.2f)); EditorUtility.SetDirty(gold); }
            }
            var light = env.Find("arena-circle-light");
            if (light != null)
            {
                var l = light.GetComponent<Light>();
                if (l != null) { l.color = CGold; l.intensity = 1.15f; l.range = 8f; }
            }
            log.Add("Env 重上色：岩体/台面→Dim=" + rocks + " 植物→三色=" + plants + " 包内杂件暗化=" + dimmed + " 法阵金=1");
        }

        private static void RepaintPiece(GameObject go, Material mat)
        {
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true)) r.sharedMaterial = mat;
        }

        // ======================================== Zone 色板（§11.2） ========================================

        private static void ApplyZonePalette(List<string> log)
        {
            var selfMix = Color.Lerp(CNeutral, CSelf, 0.15f);   // 己方功能区微偏蓝（低饱和）
            var oppMix = Color.Lerp(CNeutral, COpp, 0.15f);     // 对方功能区微偏锈
            var map = new Dictionary<string, Color>
            {
                { "BattleTile_Zone00_Backdrop", CBackdrop },
                { "BattleTile_Zone01_Edge", CGap },
                { "BattleTile_Zone02_P1Unit", CSelf },
                { "BattleTile_Zone09_P2Unit", COpp },
                { "BattleTile_Zone20_Middle", CMiddle },
            };
            string[] p1Func = { "03_P1Land", "04_P1Deck", "05_P1Grave", "06_P1Exile", "07_P1Char", "08_P1Act", "16_P1Temp", "18_P1Skill", "21_P1LandCap", "23_P1Extra" };
            string[] p2Func = { "10_P2Land", "11_P2Deck", "12_P2Grave", "13_P2Exile", "14_P2Char", "15_P2Act", "17_P2Temp", "19_P2Skill", "22_P2LandCap", "24_P2Extra" };
            foreach (var z in p1Func) map["BattleTile_Zone" + z] = selfMix;
            foreach (var z in p2Func) map["BattleTile_Zone" + z] = oppMix;

            int applied = 0;
            foreach (var kv in map)
            {
                var path = MatDir + "/" + kv.Key + ".mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null) { Debug.LogWarning("[V6Rebuild] 缺材质：" + kv.Key); continue; }
                m.SetColor("_BaseColor", kv.Value);
                m.SetFloat("_Smoothness", 0.2f);   // 格边不要亮边高光（§11.2）
                m.SetFloat("_Metallic", 0f);
                if (kv.Key == "BattleTile_Zone20_Middle") SetTransparent85(m);
                EditorUtility.SetDirty(m);
                applied++;
            }
            log.Add("Zone 材质上色=" + applied + "（己方功能#" + ColorUtility.ToHtmlStringRGB(selfMix) +
                    "/对方功能#" + ColorUtility.ToHtmlStringRGB(oppMix) + "）");
        }

        /// <summary>URP Lit 透明 85%（中线信息条底）。</summary>
        private static void SetTransparent85(Material m)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            var c = m.GetColor("_BaseColor"); c.a = 0.85f; m.SetColor("_BaseColor", c);
        }

        // ======================================== 材质工厂 ========================================

        private static Material BowlRockMat(string suffix, float r, float g, float b, float tiling)
        {
            var path = MatDir + "/BattleBowlRock" + suffix + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "BattleBowlRock" + suffix };
                m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Battle3D/Environment/Textures/MossyStone_Albedo.png"));
                var bump = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Battle3D/Environment/Textures/MossyStone_Normal_GL.png");
                if (bump != null) { MakeNormalMap("Assets/Art/Battle3D/Environment/Textures/MossyStone_Normal_GL.png"); m.SetTexture("_BumpMap", bump); m.SetFloat("_BumpScale", 0.8f); }
                // 不带 Occlusion 贴图：背光坡=环境光-only，AO 暗斑会把环境光压成斑驳纯黑（21:35 修订实测）
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", new Color(r, g, b)); // 分带 tint 阶梯（脚暖亮→外最暗）
            m.SetFloat("_Smoothness", 0.28f);
            m.SetFloat("_Metallic", 0f);
            m.SetTexture("_OcclusionMap", null); // 幂等：清掉旧版烘进来的 AO（环境光-only 区压黑元凶）
            m.SetTextureScale("_BaseMap", new Vector2(tiling, tiling)); // 带间 tiling 错开（层次感）
            m.SetTextureOffset("_BaseMap", suffix == "_B" ? new Vector2(0.37f, 0.61f) : Vector2.zero);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material DimRockMat()
        {
            var path = MatDir + "/BattleDim_Rock.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                var src = AssetDatabase.LoadAssetAtPath<Material>("Assets/Packages/PolygonDarkFantasy/Materials/PolygonDarkFantasy_Mat_01_A.mat");
                m = Object.Instantiate(src != null ? src : new Material(Shader.Find("Universal Render Pipeline/Lit")));
                m.name = "BattleDim_Rock";
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", new Color(0.28f, 0.26f, 0.23f)); // 台面石地 #2B2723 口径（×贴图均值≈目标·二档压暗）
            m.SetFloat("_Smoothness", 0.3f);
            m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material PlantMat(string suffix, Color c)
        {
            var path = MatDir + "/BattlePlantMoss_" + suffix + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "BattlePlantMoss_" + suffix };
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", 0.25f);
            m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static void MakeNormalMap(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null || importer.textureType == TextureImporterType.NormalMap) return;
            importer.textureType = TextureImporterType.NormalMap;
            importer.SaveAndReimport();
        }
    }
}
