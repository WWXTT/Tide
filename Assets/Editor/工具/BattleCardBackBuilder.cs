using System.Collections.Generic;
using SynergyUI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tide.工具
{
    /// <summary>
    /// 战场卡背长方体生成器（2026-10-13）：产出 BattleCardBack.prefab——比一格六边形小一些的
    /// 长方体卡背，顶面（俯视可见面）贴 Cardbg-b，中央叠一枚正方形面渲染元素标识（边长=卡宽一半）。
    ///
    /// 尺寸锚点：宽高比取 Cardbg-b.png 像素比（=ACard 手牌卡面同源图，344×512≈2:3）；
    /// 长（Z 向）1.7 ≈ 六棱柱点距（边长1×格缝0.94×2=1.88）的九成，宽随之≈1.14（flat-to-flat 1.63 的七成）。
    /// 根原点=六棱柱顶面（BattleHexMeshBuilder 顶面 y=0）：消费方把根摆到 BattleBoardGrid.CellCenterLocal
    /// 即落座格面。相机自 −Z 俯拍（BattleStageDirector.FrameBoardCamera），贴图上方朝 +Z=正向阅读。
    ///
    /// 结构：body=Unity Cube 纯色侧身（顶面被 back 遮住）；back/element=Unity Quad 旋 X+90 朝上
    /// （Quad 法线本指 −Z，Euler(90,0,0) 后指 +Y），透明材质承接 PNG 自带 alpha 圆角/留白。
    /// 元素标识六枚材质齐全（灰红蓝绿白黑，ElementKind 枚举序），prefab 默认灰；运行时换
    /// sharedMaterial 即换元素。图标非方形拉伸铺满正方形面（SD 出图自带透明留白，目验后可改收边）。
    ///
    /// 用法：菜单 Tools/Battle3D/生成卡背长方体（幂等覆盖重建）。
    /// </summary>
    public static class BattleCardBackBuilder
    {
        private const string MenuPath = "Tools/Battle3D/生成卡背长方体";
        private const string Dir = "Assets/Art/Battle3D";
        private const string MatDir = Dir + "/Materials";
        private const string PrefabPath = "Assets/Art/Prefab/BattleCardBack.prefab"; // prefab 区（2026-10-15 目录重构）

        private const string SpriteDir = "Assets/Art/Battle3D/Icons";
        private const string BackTexturePath = SpriteDir + "/Cardbg-b.png";

        /// <summary>卡长（Z 向，世界单位）。hex 点距 1.88 的九成=「比一格小一些」。</summary>
        private const float Length = 1.7f;

        /// <summary>卡厚。六棱柱厚 0.2，卡背取其三成读作「薄卡」。</summary>
        private const float Thickness = 0.06f;

        /// <summary>面片抬升：back 贴 body 顶面、element 叠 back 之上（防 z-fight）。</summary>
        private const float FaceLift = 0.004f;
        private const float IconLift = 0.008f;

        /// <summary>元素标识六色（ElementKind 枚举序 [灰,红,蓝,绿,白,黑] 对应 Sprites 同名图）。</summary>
        private static readonly string[] ElementNames = { "Gray", "Red", "Blue", "Green", "White", "Black" };

        [MenuItem(MenuPath)]
        public static void Build()
        {
            // ---- 目录先行（CreateAsset 不建父目录，缺目录即断） ----
            string cur = "Assets";
            foreach (var seg in MatDir.Substring("Assets/".Length).Split('/'))
            {
                string next = cur + "/" + seg;
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, seg);
                cur = next;
            }

            // ---- 宽高比单一事实源：Cardbg-b 像素比（ACard 手牌卡面同源） ----
            var backTex = AssetDatabase.LoadAssetAtPath<Texture2D>(BackTexturePath);
            if (backTex == null)
            {
                Debug.LogError($"[BattleCardBackBuilder] Cardbg-b 缺失：{BackTexturePath}");
                return;
            }
            float aspect = backTex.width / (float)backTex.height;
            float width = Length * aspect;
            float iconSide = width * 0.5f;

            // ---- 材质：body=URP Lit 纯色；back/element=URP Unlit 透明（PNG 自带 alpha）。
            // 属性必须在 CreateAsset 之前配完——CreateAsset 后再改只留内存态，磁盘 YAML 丢引用 ----
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            var bodyMat = SaveMat("BattleCardBack_Body_Mat",
                Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/SimpleLit")
                ?? Shader.Find("Standard"),
                m => { if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color32(0x1A, 0x1A, 0x24, 0xFF)); });
            var backMat = SaveMat("BattleCardBack_Back_Mat", unlit, m => { MakeTransparent(m); SetMap(m, backTex); });

            var elemMats = new Dictionary<string, Material>();
            Texture2D grayTex = null;
            foreach (var e in ElementNames)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{SpriteDir}/{e.ToLower()}.png");
                if (tex == null) Debug.LogWarning($"[BattleCardBackBuilder] 元素图缺失：{e.ToLower()}.png（材质空贴图）");
                if (e == "Gray") grayTex = tex;
                elemMats[e] = SaveMat($"BattleCardBack_Elem_{e}_Mat", unlit, m => { MakeTransparent(m); if (tex != null) SetMap(m, tex); });
            }

            // ---- 组装（根原点=格顶面；body 抬半厚使底面贴盘） ----
            var root = new GameObject("BattleCardBack");
            var body = MakeFace("body", PrimitiveType.Cube);
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, Thickness * 0.5f, 0f);
            body.transform.localScale = new Vector3(width, Thickness, Length);
            body.GetComponent<MeshRenderer>().sharedMaterial = bodyMat;

            var back = MakeFace("back", PrimitiveType.Quad);
            back.transform.SetParent(root.transform, false);
            back.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            back.transform.localPosition = new Vector3(0f, Thickness + FaceLift, 0f);
            back.transform.localScale = new Vector3(width, Length, 1f);
            back.GetComponent<MeshRenderer>().sharedMaterial = backMat;

            var element = MakeFace("element", PrimitiveType.Quad);
            element.transform.SetParent(root.transform, false);
            element.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            element.transform.localPosition = new Vector3(0f, Thickness + IconLift, 0f);
            element.transform.localScale = new Vector3(iconSide, iconSide, 1f);
            element.GetComponent<MeshRenderer>().sharedMaterial = elemMats["Gray"];

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            // 同会话反复删建材质后 prefab 指针解析会留过期导入缓存（探针证实 NULL→ForceUpdate 后恢复），
            // 生成尾强制重导入一次，保证消费方即时拿到有效引用
            AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);

            // 自检对象=磁盘重载的 prefab（SaveAsPrefabAsset 返回对象上的材质引用是瞬态假 null，
            // 探针证实磁盘 YAML 正确、事后重载正常——断言走重载通路）
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

            // ---- 数据层自检（层级/坐标/贴图断言，不做截图验收）。材质比较按名/贴图（等值安全），
            //      同 tick 引用相等不可靠（探针证实瞬态假 null） ----
            var fails = new List<string>();
            if (prefab == null || prefab.transform.childCount != 3) fails.Add("prefab/子物体数");
            if (!AssertNear(prefab.transform.Find("body"), new Vector3(0f, Thickness * 0.5f, 0f),
                new Vector3(width, Thickness, Length), new Vector3(0f, 0f, 0f))) fails.Add("body位/缩放");
            if (!AssertNear(prefab.transform.Find("back"), new Vector3(0f, Thickness + FaceLift, 0f),
                new Vector3(width, Length, 1f), new Vector3(90f, 0f, 0f))) fails.Add("back位/缩放/朝向");
            if (!AssertNear(prefab.transform.Find("element"), new Vector3(0f, Thickness + IconLift, 0f),
                new Vector3(iconSide, iconSide, 1f), new Vector3(90f, 0f, 0f))) fails.Add("element位/缩放/朝向");
            var backMatSaved = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/BattleCardBack_Back_Mat.asset");
            var grayMatSaved = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/BattleCardBack_Elem_Gray_Mat.asset");
            if (backMatSaved == null || backMatSaved.GetTexture("_BaseMap") != backTex
                || backMatSaved.renderQueue != (int)RenderQueue.Transparent) fails.Add("back材质贴图/透明队列");
            if (grayMatSaved == null || grayMatSaved.GetTexture("_BaseMap") != grayTex) fails.Add("Gray材质贴图");
            // 挂载断言走 YAML 文本比对 guid：同 tick LoadAssetPath 的 renderer 材质指针是瞬态假 null
            // （磁盘 YAML 正确、跨 tick 重载正常——探针证实），指针断言在同 tick 内不可靠
            var prefabYaml = System.IO.File.ReadAllText(PrefabPath);
            if (!prefabYaml.Contains(AssetDatabase.AssetPathToGUID($"{MatDir}/BattleCardBack_Body_Mat.asset"))
                || !prefabYaml.Contains(AssetDatabase.AssetPathToGUID($"{MatDir}/BattleCardBack_Back_Mat.asset"))
                || !prefabYaml.Contains(AssetDatabase.AssetPathToGUID($"{MatDir}/BattleCardBack_Elem_Gray_Mat.asset")))
                fails.Add("材质挂载guid");
            if (prefab.transform.Find("body").GetComponent<MeshFilter>().sharedMesh == null
                || prefab.transform.Find("back").GetComponent<MeshFilter>().sharedMesh == null) fails.Add("mesh引用");

            bool pass = fails.Count == 0;
            Debug.Log($"[{(pass ? "PASS" : "FAIL")}][BattleCardBackBuilder] prefab={PrefabPath}" +
                $" 宽={width:0.###}(比例 {backTex.width}x{backTex.height}) 长={Length:0.###} 厚={Thickness}" +
                $" 元素面={iconSide:0.###}(=卡宽一半) 元素材质×{ElementNames.Length}(默认Gray)" +
                $" shader={(unlit != null ? unlit.name : "URP/Unlit缺失!")}" +
                (pass ? "" : $" 红项=[{string.Join("、", fails)}]"));
        }

        /// <summary>建面片并拆碰撞体（盘上 tile 均无碰撞体，卡背对齐）。</summary>
        private static GameObject MakeFace(string name, PrimitiveType type)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            return go;
        }

        /// <summary>建材质资产：configure 在 CreateAsset 前执行（后改不落盘）。</summary>
        private static Material SaveMat(string name, Shader shader, System.Action<Material> configure)
        {
            if (shader == null) { Debug.LogError("[BattleCardBackBuilder] shader 全 miss（URP Lit/Unlit/Standard）"); return null; }
            string path = $"{MatDir}/{name}.asset";
            var old = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (old != null) AssetDatabase.DeleteAsset(path);
            var m = new Material(shader) { name = name };
            configure?.Invoke(m);
            AssetDatabase.CreateAsset(m, path);
            return m;
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

        private static void SetMap(Material m, Texture tex)
        {
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            else if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
        }

        /// <summary>位置/缩放/欧拉角三元断言（误差 0.001；scale 参数即期望 localScale）。</summary>
        private static bool AssertNear(Transform t, Vector3 pos, Vector3 scale, Vector3 euler)
        {
            if (t == null) return false;
            return (t.localPosition - pos).sqrMagnitude < 1e-6f
                && (t.localScale - scale).sqrMagnitude < 1e-6f
                && (t.localEulerAngles - euler).sqrMagnitude < 1e-3f;
        }
    }
}
