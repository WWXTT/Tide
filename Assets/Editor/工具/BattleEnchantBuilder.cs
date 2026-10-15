using System.Collections.Generic;
using SynergyUI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tide.工具
{
    /// <summary>
    /// 结界三件模型生成器（2026-10-14）：产出 BattleEnchant.prefab——
    /// cone=圆锥结界本体（URP Lit 半透明青白力场感，Cull Off 双面）；
    /// ring=光环指示环（torus 套锥腰，运行时 BattleEnchantFx 浮动+自转；有链接光环效果的结界才显示）；
    /// arrows=底座六向三角（arrow-NE/E/SE/SW/W/NW，平贴地面指向六个邻格——BoardDirection 邻位向量
    /// 定向：pointy-top 六边形 E=+X、NE=(+0.866,+1.5)…，按卡面箭头 HexDirection 位点亮）。
    ///
    /// 尺寸锚点：格内切半径 0.866/外接 1.0；锥底半径 0.42 高 1.05（中小存在感，巨型怪 1.5 档之下）；
    /// 环主径 0.52 管 0.05 挂锥腰 y=0.55；三角尖 0.62/底 0.30 抬 0.004 防叠面闪（同卡背/菲涅尔壳惯例）。
    /// 根原点=格顶面（消费方摆 UnitAnchor 即落座）。
    ///
    /// 用法：菜单 Tools/Battle3D/生成结界模型（幂等覆盖重建；mesh/材质/prefab 三类资产全量重生成）。
    /// 自检=数据层断言（层级/坐标/朝向角/顶点数/法线朝向/材质 guid YAML 比对），不做截图验收。
    /// </summary>
    public static class BattleEnchantBuilder
    {
        private const string MenuPath = "Tools/Battle3D/生成结界模型";
        private const string Dir = "Assets/Art/Battle3D";
        private const string MatDir = Dir + "/Materials";
        private const string PrefabPath = "Assets/Art/Prefab/BattleEnchant.prefab"; // prefab 区（2026-10-15 目录重构）
        private const string ConeMeshPath = Dir + "/BattleEnchant_Cone.asset";
        private const string RingMeshPath = Dir + "/BattleEnchant_Ring.asset";
        private const string ArrowMeshPath = Dir + "/BattleEnchant_Arrow.asset";

        // ---- 尺寸（详见类注释锚点） ----
        private const float ConeRadius = 0.42f;
        private const float ConeHeight = 1.05f;
        private const int ConeSegments = 24;
        private const float RingMajor = 0.52f;   // 主环半径（>锥腰半径 0.19，环体在锥外浮动）
        private const float RingTube = 0.05f;    // 管半径
        private const int RingSegU = 36, RingSegV = 12;
        private const float RingY = 0.55f;       // 锥腰挂点（浮动组件以此为基准 ±0.07）
        private const float ArrowTip = 0.62f;    // 尖到中心（< 内切 0.866）
        private const float ArrowBase = 0.30f;   // 底边所在半径
        private const float ArrowHalfW = 0.13f;  // 底边半宽
        private const float ArrowLiftY = 0.004f; // 抬离格面（防叠面闪，同 BattleZoneProps.LiftY）

        /// <summary>六个邻格方向（BoardDirection 名 → 邻位位移向量，奇偶行偏移无关的恒定差）。</summary>
        private static readonly KeyValuePair<string, Vector3>[] ArrowDirs =
        {
            new KeyValuePair<string, Vector3>("NE", new Vector3(0.8660254f, 0f, 1.5f)),
            new KeyValuePair<string, Vector3>("E",  new Vector3(1.7320508f, 0f, 0f)),
            new KeyValuePair<string, Vector3>("SE", new Vector3(0.8660254f, 0f, -1.5f)),
            new KeyValuePair<string, Vector3>("SW", new Vector3(-0.8660254f, 0f, -1.5f)),
            new KeyValuePair<string, Vector3>("W",  new Vector3(-1.7320508f, 0f, 0f)),
            new KeyValuePair<string, Vector3>("NW", new Vector3(-0.8660254f, 0f, 1.5f)),
        };

        [MenuItem(MenuPath)]
        public static void Build()
        {
            // ---- 目录先行（CreateAsset 不建父目录） ----
            string cur = "Assets";
            foreach (var seg in MatDir.Substring("Assets/".Length).Split('/'))
            {
                string next = cur + "/" + seg;
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, seg);
                cur = next;
            }

            // ---- 材质：锥=URP Lit 半透明（力场感双面）；环=Unlit additive（发光金）；
            //      三角=Unlit 透明金（Cull Off 双面）。属性须在 CreateAsset 前配完（后改不落盘） ----
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (unlit == null || lit == null)
            {
                Debug.LogError("[BattleEnchantBuilder] URP Lit/Unlit shader miss——URP 管线未就绪，中止");
                return;
            }
            var coneMat = SaveMat("BattleEnchant_Cone_Mat", lit, m =>
            {
                MakeTransparent(m);
                m.SetColor("_BaseColor", new Color(0.55f, 0.85f, 0.95f, 0.55f));
                m.SetInt("_Cull", 0); // 双面：半透明锥俯视可见内壁
            });
            var ringMat = SaveMat("BattleEnchant_Ring_Mat", unlit, m =>
            {
                MakeAdditive(m);
                m.SetColor("_BaseColor", new Color(1f, 0.78f, 0.35f, 0.9f));
            });
            var arrowMat = SaveMat("BattleEnchant_Arrow_Mat", unlit, m =>
            {
                MakeTransparent(m);
                m.SetColor("_BaseColor", new Color(1f, 0.82f, 0.40f, 0.85f));
                m.SetInt("_Cull", 0);
            });

            // ---- mesh 三枚（程序化；法线朝向自检兜底，绕序错=红项不产出错误视觉） ----
            var coneMesh = SaveMesh("BattleEnchant_Cone", ConeMeshPath, BuildCone());
            var ringMesh = SaveMesh("BattleEnchant_Ring", RingMeshPath, BuildTorus());
            var arrowMesh = SaveMesh("BattleEnchant_Arrow", ArrowMeshPath, BuildArrowTri());

            // ---- 组装（根原点=格顶面；_ring 引用经 SerializedObject 注入随 prefab 落盘） ----
            var root = new GameObject("BattleEnchant");
            var fx = root.AddComponent<BattleEnchantFx>();

            var cone = MakePart("cone", coneMesh, coneMat);
            cone.transform.SetParent(root.transform, false);
            cone.transform.localPosition = Vector3.zero;

            var ring = MakePart("ring", ringMesh, ringMat);
            ring.transform.SetParent(root.transform, false);
            ring.transform.localPosition = new Vector3(0f, RingY, 0f);

            var arrows = new GameObject("arrows");
            arrows.transform.SetParent(root.transform, false);
            arrows.transform.localPosition = Vector3.zero;
            foreach (var kv in ArrowDirs)
            {
                var a = MakePart("arrow-" + kv.Key, arrowMesh, arrowMat);
                a.transform.SetParent(arrows.transform, false);
                a.transform.localPosition = new Vector3(0f, ArrowLiftY, 0f);
                a.transform.localRotation = Quaternion.LookRotation(kv.Value.normalized, Vector3.up); // +Z 指向邻格
            }

            var so = new SerializedObject(fx);
            so.FindProperty("_ring").objectReferenceValue = ring.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            // 同会话反复删建资产后 prefab 指针解析会留过期导入缓存（BattleCardBackBuilder 同坑）——强制重导入
            AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);
            foreach (var p in new[] { ConeMeshPath, RingMeshPath, ArrowMeshPath })
                AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);

            SelfCheck(coneMesh, ringMesh, arrowMesh);
        }

        // ======================================== 自检（数据层断言） ========================================

        private static void SelfCheck(Mesh coneMesh, Mesh ringMesh, Mesh arrowMesh)
        {
            var fails = new List<string>();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

            if (prefab == null) { Debug.LogError("[FAIL][BattleEnchantBuilder] prefab 未生成：" + PrefabPath); return; }
            if (prefab.transform.childCount != 3) fails.Add("根子物体数≠3(cone/ring/arrows)");
            if (prefab.GetComponent<BattleEnchantFx>() == null) fails.Add("BattleEnchantFx 组件缺失");

            var ring = prefab.transform.Find("ring");
            if (ring == null || (ring.localPosition - new Vector3(0f, RingY, 0f)).sqrMagnitude > 1e-6f)
                fails.Add("ring 位置");

            // _ring 注入断言（磁盘重载后经 SerializedObject 读回）
            var fxSO = new SerializedObject(prefab.GetComponent<BattleEnchantFx>());
            if (fxSO.FindProperty("_ring").objectReferenceValue != ring) fails.Add("_ring 引用");

            // 六向三角：名齐全+朝向角（四元数等价比较，规避欧拉 360 规范化）+ 抬升
            var arrows = prefab.transform.Find("arrows");
            if (arrows == null || arrows.childCount != 6) fails.Add("arrows 子物体数≠6");
            else
            {
                foreach (var kv in ArrowDirs)
                {
                    var child = arrows.Find("arrow-" + kv.Key);
                    if (child == null) { fails.Add("缺 arrow-" + kv.Key); continue; }
                    var expect = Quaternion.LookRotation(kv.Value.normalized, Vector3.up);
                    if (Quaternion.Angle(child.localRotation, expect) > 0.5f) fails.Add(kv.Key + " 朝向角");
                    if (Mathf.Abs(child.localPosition.y - ArrowLiftY) > 1e-4f) fails.Add(kv.Key + " 抬升");
                }
            }

            // mesh：顶点数+法线朝向抽检（绕序错误在此暴露）
            var coneSaved = AssetDatabase.LoadAssetAtPath<Mesh>(ConeMeshPath);
            var ringSaved = AssetDatabase.LoadAssetAtPath<Mesh>(RingMeshPath);
            var arrowSaved = AssetDatabase.LoadAssetAtPath<Mesh>(ArrowMeshPath);
            if (coneSaved == null || coneSaved.vertexCount != 1 + ConeSegments * 2 + 1) fails.Add("cone 顶点数");
            else if (coneSaved.normals[ConeSegments].x < 0.3f) fails.Add("cone 侧面法线朝向(绕序)");
            if (ringSaved == null || ringSaved.vertexCount != (RingSegU + 1) * (RingSegV + 1)) fails.Add("ring 顶点数");
            else if (ringSaved.normals[0].x < 0.9f) fails.Add("ring 法线朝向(绕序)");
            if (arrowSaved == null || arrowSaved.vertexCount != 6) fails.Add("arrow 顶点数");
            else if (arrowSaved.normals[0].y < 0.9f || arrowSaved.normals[3].y > -0.9f) fails.Add("arrow 法线朝向(绕序)");

            // 材质挂载 guid YAML 比对（同 tick LoadAssetPath 指针瞬态假 null，文本比对可靠）
            var yaml = System.IO.File.ReadAllText(PrefabPath);
            foreach (var matName in new[] { "BattleEnchant_Cone_Mat", "BattleEnchant_Ring_Mat", "BattleEnchant_Arrow_Mat" })
            {
                var guid = AssetDatabase.AssetPathToGUID($"{MatDir}/{matName}.asset");
                if (string.IsNullOrEmpty(guid) || !yaml.Contains(guid)) fails.Add(matName + " 挂载guid");
            }

            bool pass = fails.Count == 0;
            Debug.Log($"[{(pass ? "PASS" : "FAIL")}][BattleEnchantBuilder] prefab={PrefabPath}" +
                      $" 锥R={ConeRadius}/H={ConeHeight} 环R={RingMajor}/管={RingTube}@y{RingY}" +
                      $" 三角尖{ArrowTip} 六向=NE/E/SE/SW/W/NW" +
                      $" 顶点 cone{coneSaved?.vertexCount ?? -1}/ring{ringSaved?.vertexCount ?? -1}/arrow{arrowSaved?.vertexCount ?? -1}" +
                      (pass ? "" : $" 红项=[{string.Join("、", fails)}]"));
        }

        // ======================================== mesh 构建 ========================================

        /// <summary>圆锥：apex + 侧面环(24) + 底面环(24) + 底心（侧面/底面独立顶点保硬边）。
        /// 绕序（Unity 左手正面=cross(b−a,c−a) 方向）：侧面 (apex, v[i+1], v[i]) 外向、底面 (center, b[i], b[i+1]) 朝 −Y。</summary>
        private static Mesh BuildCone()
        {
            const int apex = 0, sideStart = 1, botStart = 1 + ConeSegments, botCenter = 1 + ConeSegments * 2;
            var verts = new Vector3[botCenter + 1];
            var uvs = new Vector2[verts.Length];
            verts[apex] = new Vector3(0f, ConeHeight, 0f);
            verts[botCenter] = Vector3.zero;
            for (int i = 0; i < ConeSegments; i++)
            {
                float a = i / (float)ConeSegments * Mathf.PI * 2f;
                var p = new Vector3(Mathf.Cos(a) * ConeRadius, 0f, Mathf.Sin(a) * ConeRadius);
                verts[sideStart + i] = p;
                verts[botStart + i] = p;
                uvs[sideStart + i] = new Vector2(i / (float)ConeSegments, 0f);
                uvs[botStart + i] = new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f);
            }
            uvs[apex] = new Vector2(0f, 1f);

            var tris = new List<int>();
            for (int i = 0; i < ConeSegments; i++)
            {
                int n = sideStart + (i + 1) % ConeSegments;
                tris.AddRange(new[] { apex, n, sideStart + i });                 // 侧面外向
                tris.AddRange(new[] { botCenter, botStart + i, botStart + (i + 1) % ConeSegments }); // 底面朝下
            }

            var mesh = new Mesh { name = "BattleEnchant_Cone" };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();
            return mesh;
        }

        /// <summary>圆环（torus）：主环 XZ 平面绕 Y，u=主环角 v=管角。
        /// 绕序：四边形 (a,b,c,d)=p(u,v)/p(u+du,v)/p(u+du,dv)/p(u,dv) 拆 (a,c,b)+(b,d,c)（法线从管轴向外）。</summary>
        private static Mesh BuildTorus()
        {
            var verts = new Vector3[(RingSegU + 1) * (RingSegV + 1)];
            var uvs = new Vector2[verts.Length];
            for (int i = 0; i <= RingSegU; i++)
            {
                float u = i / (float)RingSegU * Mathf.PI * 2f;
                float cu = Mathf.Cos(u), su = Mathf.Sin(u);
                for (int j = 0; j <= RingSegV; j++)
                {
                    float v = j / (float)RingSegV * Mathf.PI * 2f;
                    verts[i * (RingSegV + 1) + j] = new Vector3(
                        (RingMajor + RingTube * Mathf.Cos(v)) * cu,
                        RingTube * Mathf.Sin(v),
                        (RingMajor + RingTube * Mathf.Cos(v)) * su);
                    uvs[i * (RingSegV + 1) + j] = new Vector2(i / (float)RingSegU, j / (float)RingSegV);
                }
            }

            var tris = new List<int>();
            for (int i = 0; i < RingSegU; i++)
                for (int j = 0; j < RingSegV; j++)
                {
                    int a = i * (RingSegV + 1) + j;
                    int b = (i + 1) * (RingSegV + 1) + j;
                    int c = (i + 1) * (RingSegV + 1) + j + 1;
                    int d = i * (RingSegV + 1) + j + 1;
                    tris.AddRange(new[] { a, c, b });
                    tris.AddRange(new[] { b, c, d });
                }

            var mesh = new Mesh { name = "BattleEnchant_Ring" };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();
            return mesh;
        }

        /// <summary>六向三角箭头（指向 +Z，消费方 LookRotation 定向）：上三角 (tip,br,bl) 法线 +Y、
        /// 下三角反向（独立顶点；材质 Cull Off 下双保险）。</summary>
        private static Mesh BuildArrowTri()
        {
            var verts = new[]
            {
                new Vector3(0f, 0f, ArrowTip),                    // 0 上·尖
                new Vector3(ArrowHalfW, 0f, ArrowBase),           // 1 上·右底
                new Vector3(-ArrowHalfW, 0f, ArrowBase),          // 2 上·左底
                new Vector3(0f, 0f, ArrowTip),                    // 3 下·尖
                new Vector3(ArrowHalfW, 0f, ArrowBase),           // 4 下·右底
                new Vector3(-ArrowHalfW, 0f, ArrowBase),          // 5 下·左底
            };
            var tris = new[] { 0, 1, 2, 3, 5, 4 };

            var mesh = new Mesh { name = "BattleEnchant_Arrow" };
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            return mesh;
        }

        // ======================================== 资产落盘 ========================================

        /// <summary>建结构件（MeshFilter+MeshRenderer，无碰撞体；战场惯例不投影）。</summary>
        private static GameObject MakePart(string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var rend = go.AddComponent<MeshRenderer>();
            rend.sharedMaterial = mat;
            rend.shadowCastingMode = ShadowCastingMode.Off;
            rend.receiveShadows = false;
            return go;
        }

        /// <summary>建材质资产（幂等删建；configure 在 CreateAsset 前执行）。</summary>
        private static Material SaveMat(string name, Shader shader, System.Action<Material> configure)
        {
            string path = $"{MatDir}/{name}.asset";
            var old = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (old != null) AssetDatabase.DeleteAsset(path);
            var m = new Material(shader) { name = name };
            configure?.Invoke(m);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        /// <summary>建 mesh 资产（幂等删建；生成器内存 mesh 与落盘资产同 guid 引用由 prefab 序列化保住）。</summary>
        private static Mesh SaveMesh(string name, string path, Mesh mesh)
        {
            var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (old != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static void MakeTransparent(Material m)
        {
            m.SetInt("_Surface", 1); // Transparent
            m.SetInt("_Blend", 0);   // Alpha
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
        }

        private static void MakeAdditive(Material m)
        {
            m.SetInt("_Surface", 1);
            m.SetInt("_Blend", 1);   // Additive
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)BlendMode.One);
            m.SetInt("_DstBlend", (int)BlendMode.One);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
        }
    }
}
