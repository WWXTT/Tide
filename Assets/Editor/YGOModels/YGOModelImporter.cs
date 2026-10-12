// YGOModelImporter.cs — 将 Assets/Art/YGOMCModels 下的 .bbmodel（Blockbench 工程）烘焙为 Unity 资产。
// 菜单：Tools/YGO模型 →
//   1 干跑·仅解析统计（选中优先）
//   2 生成静态版（选中优先；未选中弹确认全量，覆盖 Assets/Art/YGOMonsters）
//   3 生成骨骼版（选中优先；未选中弹确认全量，输出 Assets/Art/YGOMonstersRig）
// 「选中优先」= Project 窗口选中 YGOMCModels 下的文件夹或 .bbmodel（可多选）时只处理对应模型；
// 命名与全量生成完全一致（消歧后缀按源文件夹实际 bbmodel 数量推导）。
//
// 骨骼版为正式产出：预制体保持默认待机姿态（姿态源=bbmodel 内嵌动画，组 UUID 定位），
// 程序动画组件由运行时按需挂载（后续开发：尾/触手链、蜘蛛步态、绳索、头部注视、双足/四足行走）。
//
// 透明镂空（2026-10-11 定案：整体替换）：全部材质统一用自定义 shader「Tide/YGOMonster」
//（Assets/Art/Shaders/YGOMonster.shader，受光+Alpha 硬裁切叶片式，各 Pass 同裁）——
// 作者贴图自带 alpha（黑 RGB+alpha≈0 即镂空/翅膀膜），shader 直接按 alpha 裁，
// 不做逐面路由、不建双材质，身体部位与翅膀同一材质同一行为。
//
// 坐标系转换（Blockbench 右手系 +X东/+Y上/+Z南 → Unity 左手系）：
//   映射=绕Y轴180°纯旋转 diag(−1,1,−1)：点/法线 p_u=(−x,y,−z)*UnitScale；四元数 q_u=(−x,y,−z,w)
//   （纯旋转共轭，MapPt(q·v)=MapQ(q)·MapPt(v) 恒等；取代旧 (x,y,−z) 镜像映射——其左右反已由
//   ZoneEater ±1 关节读数标定证实）。绕序不变量：亮面判据 Cross(三角形边)·指定法线 > 0（shader VFACE）。
//   源面片角点序（外侧视角 TL,TR,BR,BL）在 BB 系内 Cross=−法线；旧镜像映射 det=−1 自翻一次定向恰好配平，
//   纯旋转 det=+1 不翻 → 三角形必须整体反向 (0,3,2)+(0,2,1)。注意 (0,1,2)+(0,2,3) 与 (0,2,3)+(0,1,2)
//   是同两三角形换排放序（朝向不变=无效操作）——映射与绕序必须成对审计。
// 变换链：立方体自身旋转先应用，再沿 outliner 骨骼链内→外（全部在 BB 坐标系完成后才映射）；
// 骨骼网格按绑定姿势烘进组局部系，骨骼局部姿态 = 待机姿势（内嵌动画增量已合成）。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace YGOImport
{
    public static class YGOModelImporter
    {
        const string SourceRoot = "Assets/Art/YGOMCModels";
        const string StaticRoot = "Assets/Art/YGOMonsters";
        const string RigRoot = "Assets/Art/YGOMonstersRig";
        const float UnitScale = 1f / 16f;
        const string MonsterShader = "Tide/YGOMonster";
        const bool MonsterCastShadows = true;  // 怪兽是否往地面投影（不接收阴影在 shader 侧定案）；GPU 占用仍高可改 false 重生成

        enum GenMode { Dry, Static, Rig }

        [MenuItem("Tools/YGO模型/1 干跑·仅解析统计（选中优先）")]
        static void DryRun() { Run(GenMode.Dry); }

        [MenuItem("Tools/YGO模型/2 生成静态版（选中优先·YGOMonsters）")]
        static void GenStatic() { Run(GenMode.Static); }

        [MenuItem("Tools/YGO模型/3 生成骨骼版（选中优先·YGOMonstersRig）")]
        static void GenRig() { Run(GenMode.Rig); }

        static string ProjRoot => Path.GetDirectoryName(Application.dataPath);

        static int _windingViolations;
        static int _flatTotal, _flatFlipped;

        // ---------- 源文件收集（选中优先） ----------
        /// <summary>选中项 → 源 bbmodel 绝对/相对路径对；无有效选中返回 null（=全量）。</summary>
        static List<string[]> CollectSelected(string srcAbs)
        {
            var result = new List<string[]>();
            foreach (var obj in Selection.objects)
            {
                string rel = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(rel)) continue;
                rel = rel.Replace('\\', '/');
                if (!rel.StartsWith(SourceRoot + "/", StringComparison.OrdinalIgnoreCase)) continue;
                string abs = Path.Combine(ProjRoot, rel).Replace('\\', '/');
                if (Directory.Exists(abs))
                {
                    foreach (var f0 in Directory.EnumerateFiles(abs, "*.bbmodel", SearchOption.AllDirectories))
                    {
                        var f = f0.Replace('\\', '/');
                        result.Add(new[] { f, f.Substring(srcAbs.Length + 1) });
                    }
                }
                else if (abs.EndsWith(".bbmodel", StringComparison.OrdinalIgnoreCase) && File.Exists(abs))
                {
                    result.Add(new[] { abs, rel.Substring(SourceRoot.Length + 1) });
                }
            }
            // 同一文件可能经多选重复，去重
            return result.Count == 0 ? null : result
                .GroupBy(p => p[0], StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First()).ToList();
        }

        static void Run(GenMode mode)
        {
            string srcAbs = Path.Combine(ProjRoot, SourceRoot).Replace('\\', '/');
            if (!Directory.Exists(srcAbs))
            {
                Debug.LogError("[YGO] 源目录不存在：" + srcAbs);
                return;
            }

            var picked = CollectSelected(srcAbs);
            List<string[]> batch;
            if (picked != null)
            {
                batch = picked.OrderBy(p => p[1], StringComparer.OrdinalIgnoreCase).ToList();
                Debug.Log("[YGO] 选中模式：本次处理 " + batch.Count + " 个 bbmodel");
            }
            else
            {
                var absAll = new List<string>();
                var relAll = new List<string>();
                foreach (var f0 in Directory.EnumerateFiles(srcAbs, "*.bbmodel", SearchOption.AllDirectories))
                {
                    var f = f0.Replace('\\', '/');
                    var firstSeg = f.Substring(srcAbs.Length + 1).Split('/')[0];
                    // 全量排除：_OLD（作者旧模）与 _AprilFools*（愚人节批，2026-10-12 用户裁定不要）；
                    // 选中模式不受此限——显式选中即尊重
                    if (f.IndexOf("/_OLD/", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    if (firstSeg.StartsWith("_AprilFools", StringComparison.OrdinalIgnoreCase)) continue;
                    absAll.Add(f);
                    relAll.Add(f.Substring(srcAbs.Length + 1));
                }
                if (mode != GenMode.Dry)
                {
                    if (!EditorUtility.DisplayDialog("YGO 模型导入",
                        "未选中 YGOMCModels 子项，将全量处理 " + absAll.Count + " 个模型，继续？", "全量", "取消"))
                        return;
                }
                batch = new List<string[]>();
                for (int i = 0; i < absAll.Count; i++) batch.Add(new[] { absAll[i], relAll[i] });
            }

            // 消歧计数：按涉及源文件夹在磁盘上的实际 bbmodel 数（与全量命名一致）
            var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in batch)
            {
                var seg = p[1].Split('/');
                folders.Add(seg[0] == "_OLD" && seg.Length > 2 ? seg[1] : seg[0]);
            }
            var countByFolder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var f0 in folders)
            {
                string dir = Path.Combine(srcAbs, f0);
                countByFolder[f0] = Directory.Exists(dir)
                    ? Directory.EnumerateFiles(dir, "*.bbmodel", SearchOption.AllDirectories).Count()
                    : 1;
            }

            int ok = 0, fail = 0, cubeTotal = 0, boneTotal = 0;
            int idleApplied = 0, idleFromEmbedded = 0, anonModels = 0;
            var fails = new List<string>();
            _windingViolations = 0;
            _flatTotal = _flatFlipped = 0;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                for (int n = 0; n < batch.Count; n++)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("YGO 模型导入",
                        Path.GetFileName(batch[n][0]) + "（" + (n + 1) + "/" + batch.Count + "）",
                        (float)n / batch.Count))
                    {
                        Debug.LogWarning("[YGO] 用户取消，已完成部分保留");
                        break;
                    }

                    string name = MonsterName(batch[n][1], countByFolder);
                    try
                    {
                        var model = BBModelParser.ParseFile(batch[n][0]);
                        model.Idle = BBModelParser.LoadIdle(
                            Path.Combine(Path.GetDirectoryName(batch[n][0]) ?? "", "idle.json"));
                        BBRigMath.Prepare(model);
                        if (model.IdleApplied) idleApplied++;
                        if (model.IdleApplied && model.ActiveIdlePose.FromEmbedded) idleFromEmbedded++;
                        if (model.AnonymousGroups > 0) anonModels++;
                        boneTotal += model.Groups.Count;
                        if (model.Cubes.Count == 0) { fail++; fails.Add(name + "：无立方体"); continue; }
                        if (!model.Textures.Any(t => t.Png != null)) { fail++; fails.Add(name + "：无内嵌纹理"); continue; }

                        if (mode == GenMode.Static) GenerateStatic(model, name);
                        else if (mode == GenMode.Rig) GenerateRig(model, name);
                        ok++;
                        cubeTotal += model.Cubes.Count;
                    }
                    catch (Exception ex)
                    {
                        fail++;
                        fails.Add(name + "：" + ex.Message);
                    }
                }
            }
            finally { EditorUtility.ClearProgressBar(); }

            if (mode != GenMode.Dry) { AssetDatabase.SaveAssets(); AssetDatabase.Refresh(); }
            string modeName = mode == GenMode.Dry ? "干跑" : mode == GenMode.Static ? "静态版生成" : "骨骼版生成";
            string failLog = fails.Count > 0
                ? "\n失败清单：\n" + string.Join("\n", fails.Take(50))
                : "\n无失败。";
            string idleLog = "\n待机姿态：应用 " + idleApplied + "（内嵌动画 " + idleFromEmbedded + "），匿名组模型 " + anonModels;
            Debug.Log("[YGO] " + modeName + "完成：成功 " + ok + " / 失败 " + fail
                + "（立方体 " + cubeTotal + "，骨骼 " + boneTotal + "），绕序自检违规 " + _windingViolations
                + "，单面片选面翻转 " + _flatFlipped + "/" + _flatTotal
                + "，耗时 " + sw.Elapsed.TotalSeconds.ToString("F0") + "s" + idleLog + failLog);
        }

        // 相对路径 → 资产名：一级文件夹为准（_OLD 深层取二级）；文件夹多 bbmodel 时追加文件名消歧
        static string MonsterName(string rel, Dictionary<string, int> countByFolder)
        {
            var seg = rel.Split('/');
            string folder = seg[0] == "_OLD" && seg.Length > 2 ? seg[1] : seg[0];
            string name = countByFolder.GetOrDefault(folder, 1) > 1
                ? seg[0] + "_" + Path.GetFileNameWithoutExtension(rel)
                : folder;
            return Sanitize(name);
        }

        static string Sanitize(string s)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder();
            foreach (var ch in s) sb.Append(Array.IndexOf(invalid, ch) >= 0 ? '_' : ch);
            return sb.ToString();
        }

        // ---------- 逐模型生成 ----------
        class TexInfo
        {
            public BBTexture Bb;
            public Texture2D Asset;
            public Material Mat;
            public Color[] Pixels;   // 内容感知选面用（惰性解码自 Png，仅导入期统计，不落盘）
        }

        // 子网格槽 = 贴图（每贴图一个材质槽，全部用 Tide/YGOMonster 整体裁切）；全部位共用同一材质数组
        class MeshCtx
        {
            public List<TexInfo> Texes = new List<TexInfo>();
            public Dictionary<int, int> TexMap = new Dictionary<int, int>(); // bbmodel 纹理下标 → TexInfo 下标
            List<int> _slots = new List<int>();
            Dictionary<int, int> _slotOf = new Dictionary<int, int>();
            public Material[] Materials;

            public int Slot(int texIdx)
            {
                int slot;
                if (!_slotOf.TryGetValue(texIdx, out slot))
                {
                    slot = _slots.Count;
                    _slotOf[texIdx] = slot;
                    _slots.Add(texIdx);
                }
                return slot;
            }

            public int SlotCount { get { return _slots.Count; } }
            public int SlotTex(int slot) { return _slots[slot]; }
        }

        static void GenerateStatic(BBModel model, string name)
        {
            string dirRel = StaticRoot + "/" + name;
            Directory.CreateDirectory(Path.Combine(ProjRoot, dirRel));
            ClearGeneratedAssets(dirRel, name);

            var ctx = SetupTextures(model, name, dirRel);
            var mb = new MeshBuffers(ctx);
            foreach (var cube in model.Cubes) EmitCube(mb, ctx, model, cube, null, true);
            Mesh mesh = FinishMesh(mb, name);
            AssetDatabase.CreateAsset(mesh, dirRel + "/" + name + "_mesh.asset");
            CreateMaterials(ctx, name, dirRel);

            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = ctx.Materials;
            mr.shadowCastingMode = MonsterCastShadows
                ? UnityEngine.Rendering.ShadowCastingMode.On
                : UnityEngine.Rendering.ShadowCastingMode.Off;
            CenterOnRoot(go);
            PrefabUtility.SaveAsPrefabAsset(go, dirRel + "/" + name + ".prefab");
            UnityEngine.Object.DestroyImmediate(go);
        }

        static void GenerateRig(BBModel model, string name)
        {
            string dirRel = RigRoot + "/" + name;
            Directory.CreateDirectory(Path.Combine(ProjRoot, dirRel));
            ClearGeneratedAssets(dirRel, name);

            var ctx = SetupTextures(model, name, dirRel);
            var usedMeshNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var root = new GameObject(name);
            var rootCubes = model.Cubes.Where(c => c.OwnerGroup == null).ToList();
            if (rootCubes.Count > 0)
            {
                Mesh rootMesh = BuildGroupMesh(model, rootCubes, null, ctx, name, dirRel, usedMeshNames);
                root.AddComponent<MeshFilter>().sharedMesh = rootMesh;
            }
            foreach (var g in model.Groups.Where(g => g.Parent == null))
                CreateBone(root.transform, g, model, ctx, name, dirRel, usedMeshNames);

            // 材质依赖网格期登记的贴图槽位——骨骼全建完才能创建，最后统一装 Renderer（顺序不可换：
            // sharedMaterials 赋 null 会抛 ArgumentNullException("m")，lower_body 之后全部中断）
            CreateMaterials(ctx, name, dirRel);
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
            {
                var mr = mf.gameObject.AddComponent<MeshRenderer>();
                mr.sharedMaterials = ctx.Materials;
                mr.shadowCastingMode = MonsterCastShadows
                    ? UnityEngine.Rendering.ShadowCastingMode.On
                    : UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            CenterOnRoot(root);

            PrefabUtility.SaveAsPrefabAsset(root, dirRel + "/" + name + ".prefab");
            UnityEngine.Object.DestroyImmediate(root);
        }

        // 根节点原点统一（2026-10-12 用户定案）：idle 姿态下整体刚性平移，使包围盒 XZ 中心与最低点落到根原点
        //（XZ 居中、y=0=模型低点）——根节点即立足点，后续站立/摆放不穿入地面，也不改骨骼间相对关系。
        // 构建期根为 identity，子级 localToWorld 即相对根矩阵；顶级骨骼平移 localPosition、
        // 根级立方体网格（挂在根上）直接平移顶点；骨内网格本就是骨骼局部坐标无需动。
        static void CenterOnRoot(GameObject root)
        {
            bool has;
            var bounds = ComputeRootBounds(root, out has);
            if (!has) return;
            var offset = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
            if (offset.sqrMagnitude < 1e-12f) return;
            foreach (Transform child in root.transform)
                child.localPosition += offset;
            var rootMf = root.GetComponent<MeshFilter>();
            if (rootMf != null && rootMf.sharedMesh != null) ShiftMeshVertices(rootMf.sharedMesh, offset);
        }

        // 全部网格合并包围盒（相对根；AABB 角点变换封装，含旋转时保守外扩=只可能略浮起不会穿地）
        static Bounds ComputeRootBounds(GameObject root, out bool has)
        {
            has = false;
            var bounds = new Bounds();
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
            {
                var b = mf.sharedMesh.bounds;
                var toRoot = root.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                var mn = b.min; var mx = b.max;
                for (int cx = 0; cx < 2; cx++)
                    for (int cy = 0; cy < 2; cy++)
                        for (int cz = 0; cz < 2; cz++)
                        {
                            var p = toRoot.MultiplyPoint3x4(new Vector3(
                                cx == 0 ? mn.x : mx.x, cy == 0 ? mn.y : mx.y, cz == 0 ? mn.z : mx.z));
                            if (!has) { bounds = new Bounds(p, Vector3.zero); has = true; }
                            else bounds.Encapsulate(p);
                        }
            }
            return bounds;
        }

        static void ShiftMeshVertices(Mesh mesh, Vector3 offset)
        {
            var verts = mesh.vertices;
            for (int i = 0; i < verts.Length; i++) verts[i] += offset;
            mesh.SetVertices(verts);
            mesh.RecalculateBounds();
        }

        static void CreateBone(Transform parent, BBGroup g, BBModel model, MeshCtx ctx,
            string name, string dirRel, HashSet<string> usedMeshNames)
        {
            var go = new GameObject(string.IsNullOrEmpty(g.Name) ? "bone" + g.Depth : g.Name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = MapPt(g.LocalIdlePosV);
            go.transform.localRotation = MapQ(g.LocalIdleQuat);
            if (g.Cubes.Count > 0)
            {
                Mesh mesh = BuildGroupMesh(model, g.Cubes, g, ctx, name, dirRel, usedMeshNames);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                // MeshRenderer 由 GenerateRig 在材质就绪后统一装配
            }
            foreach (var child in g.Children)
                CreateBone(go.transform, child, model, ctx, name, dirRel, usedMeshNames);
        }

        static Mesh BuildGroupMesh(BBModel model, List<BBCube> cubes, BBGroup owner, MeshCtx ctx,
            string name, string dirRel, HashSet<string> usedMeshNames)
        {
            var mb = new MeshBuffers(ctx);
            foreach (var cube in cubes) EmitCube(mb, ctx, model, cube, owner, false);
            Mesh mesh = FinishMesh(mb, name + "_" + (owner != null ? owner.Name : "root"));
            string fileName = UniqueAssetName(usedMeshNames, mesh.name);
            AssetDatabase.CreateAsset(mesh, dirRel + "/" + fileName + ".asset");
            return mesh;
        }

        // 骨骼网格资产名去重（重名骨骼/同基名时追加序号）
        static string UniqueAssetName(HashSet<string> used, string baseName)
        {
            string candidate = baseName;
            int i = 2;
            while (!used.Add(candidate.ToLowerInvariant())) candidate = baseName + "_" + i++;
            return candidate;
        }

        // 贴图落盘 + 导入（Default 类型/Point/无 mipmap/Clamp）
        static MeshCtx SetupTextures(BBModel model, string name, string dirRel)
        {
            var ctx = new MeshCtx();
            for (int i = 0; i < model.Textures.Count; i++)
            {
                var t = model.Textures[i];
                if (t.Png == null) continue;
                ctx.TexMap[i] = ctx.Texes.Count;
                string pngRel = dirRel + "/" + name + "_tex" + ctx.Texes.Count + ".png";
                File.WriteAllBytes(Path.Combine(ProjRoot, pngRel), t.Png);
                AssetDatabase.ImportAsset(pngRel, ImportAssetOptions.ForceUpdate);
                var imp = AssetImporter.GetAtPath(pngRel) as TextureImporter;
                if (imp != null)
                {
                    imp.textureType = TextureImporterType.Default; // 防 Sprite 切片预设
                    imp.filterMode = FilterMode.Point;
                    imp.mipmapEnabled = true;   // 无 mipmap 时远距/斜视角逐像素混叠，镜头移动翼膜边缘爬动闪烁
                    imp.wrapMode = TextureWrapMode.Clamp;
                    imp.SaveAndReimport();
                }
                ctx.Texes.Add(new TexInfo
                {
                    Bb = t,
                    Asset = AssetDatabase.LoadAssetAtPath<Texture2D>(pngRel),
                });
            }
            return ctx;
        }

        // 生成前清掉本模型的旧生成物（prefab/网格/材质/贴图，连 meta），保证重生成=干净重建、
        // 材质 shader 不会残留旧引用；同文件夹的作者预览图 <名>.png 不在清理范围。
        static void ClearGeneratedAssets(string dirRel, string name)
        {
            string dirAbs = Path.Combine(ProjRoot, dirRel);
            if (!Directory.Exists(dirAbs)) return;
            foreach (var file0 in Directory.GetFiles(dirAbs))
            {
                string fn = Path.GetFileName(file0);
                bool generated =
                    fn.Equals(name + ".prefab", StringComparison.OrdinalIgnoreCase)
                    || (fn.StartsWith(name + "_tex", StringComparison.OrdinalIgnoreCase) && fn.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    || (fn.StartsWith(name + "_mat", StringComparison.OrdinalIgnoreCase) && fn.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
                    || (fn.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) && fn.StartsWith(name + "_", StringComparison.OrdinalIgnoreCase) && fn.IndexOf("_mesh", StringComparison.OrdinalIgnoreCase) >= 0)
                    || fn.Equals(name + "_mesh.asset", StringComparison.OrdinalIgnoreCase);
                if (!generated) continue;
                AssetDatabase.DeleteAsset(dirRel + "/" + fn);
            }
        }

        // 槽位需求确定后创建材质：每贴图一份，统一 Tide/YGOMonster（受光+硬裁切整体替换）
        static void CreateMaterials(MeshCtx ctx, string name, string dirRel)
        {
            var shader = Shader.Find(MonsterShader);
            if (shader == null) shader = Shader.Find("Unlit/Texture");
            ctx.Materials = new Material[ctx.SlotCount];
            for (int slot = 0; slot < ctx.SlotCount; slot++)
            {
                var t = ctx.Texes[ctx.SlotTex(slot)];
                if (t.Mat == null)
                {
                    int texIdx = ctx.SlotTex(slot);
                    var mat = new Material(shader)
                    {
                        name = name + "_mat" + (texIdx > 0 ? texIdx.ToString() : ""),
                    };
                    if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", t.Asset);
                    if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", t.Asset);
                    if (mat.HasProperty("_Cutoff")) mat.SetFloat("_Cutoff", 0.5f);
                    t.Mat = mat;
                    AssetDatabase.CreateAsset(mat, dirRel + "/" + mat.name + ".mat");
                }
                ctx.Materials[slot] = t.Mat;
            }
        }

        // ---------- 网格发射 ----------
        class MeshBuffers
        {
            public List<Vector3> Verts = new List<Vector3>();
            public List<Vector3> Norms = new List<Vector3>();
            public List<Vector2> Uvs = new List<Vector2>();
            public List<List<int>> Tris = new List<List<int>>();
            readonly MeshCtx _ctx;
            public MeshBuffers(MeshCtx ctx) { _ctx = ctx; }

            public List<int> TrisOf(int slot)
            {
                while (Tris.Count <= slot) Tris.Add(new List<int>());
                return Tris[slot];
            }
        }

        // owner=null 且 idlePose=true → 静态版（待机姿态世界坐标）；
        // owner!=null 且 idlePose=false → 骨骼版（绑定姿势烘进 owner 局部系）
        static void EmitCube(MeshBuffers mb, MeshCtx ctx, BBModel model, BBCube cube, BBGroup owner, bool idlePose)
        {
            var from = cube.From;
            var to = cube.To;
            // 零厚度面片：只发射单一大面，其余五面（对面+四条零面积侧边）全部跳过——无 Z-fighting、无侧边条；
            // 双视角受光由 shader Cull Off + VFACE 翻转法线承担。
            // 保留面=内容感知选面（2026-10-12 定案）：候选两面各自 UV 矩形内数不透明像素（alpha ≥ _Cutoff 0.5），
            // 多者胜出；平手/两面全空=默认面 west/down/south。旧固定押注面规则在源数据上是赌运气——
            // 全库 2001 面片中 712 UV 落空贴图区 + 208 仅剩红点（40.7% 模型受害），作者图案常画在另一面。
            string flatKeep = cube.FlatAxis >= 0 ? PickFlatFace(cube, model, ctx) : null;
            for (int fi = 0; fi < BBModelParser.FaceNames.Length; fi++)
            {
                string face = BBModelParser.FaceNames[fi];
                if (flatKeep != null && face != flatKeep) continue;
                FaceCornersBB(face, from, to, out var tl, out var tr, out var br, out var bl);
                var nBB = FaceNormalBB(face);

                BBV3 tlW, trW, brW, blW, nW;
                if (idlePose)
                {
                    tlW = BBRigMath.IdleWorldPoint(cube, tl);
                    trW = BBRigMath.IdleWorldPoint(cube, tr);
                    brW = BBRigMath.IdleWorldPoint(cube, br);
                    blW = BBRigMath.IdleWorldPoint(cube, bl);
                    nW = BBRigMath.IdleWorldNormal(cube, nBB);
                }
                else
                {
                    tlW = BBRigMath.LocalizePoint(owner, BBRigMath.CubeWorldPoint(cube, tl, false));
                    trW = BBRigMath.LocalizePoint(owner, BBRigMath.CubeWorldPoint(cube, tr, false));
                    brW = BBRigMath.LocalizePoint(owner, BBRigMath.CubeWorldPoint(cube, br, false));
                    blW = BBRigMath.LocalizePoint(owner, BBRigMath.CubeWorldPoint(cube, bl, false));
                    nW = BBRigMath.LocalizeNormal(owner, BBRigMath.CubeWorldNormal(cube, nBB, false));
                }

                BBFace bf;
                cube.Faces.TryGetValue(face, out bf);
                int texIdx = 0;
                int mapped;
                if (bf != null && bf.Texture >= 0 && ctx.TexMap.TryGetValue(bf.Texture, out mapped)) texIdx = mapped;
                var tex = ctx.Texes[texIdx];
                int tw = tex.Bb.Width > 0 ? tex.Bb.Width : model.ResolutionW;
                int th = tex.Bb.Height > 0 ? tex.Bb.Height : model.ResolutionH;
                if (tw <= 0) tw = 16;
                if (th <= 0) th = 16;

                var r = BBModelParser.ResolveFaceUv(cube, face, model);
                int slot = ctx.Slot(texIdx);

                int baseI = mb.Verts.Count;
                var normal = MapDir(nW).normalized;
                mb.Verts.Add(MapPt(tlW)); mb.Norms.Add(normal);
                mb.Verts.Add(MapPt(trW)); mb.Norms.Add(normal);
                mb.Verts.Add(MapPt(brW)); mb.Norms.Add(normal);
                mb.Verts.Add(MapPt(blW)); mb.Norms.Add(normal);
                // 纹理像素 → Unity UV（v 轴翻转；矩形自带镜像编码，不排序归一）
                mb.Uvs.Add(new Vector2((float)(r[0] / tw), 1f - (float)(r[1] / th)));
                mb.Uvs.Add(new Vector2((float)(r[2] / tw), 1f - (float)(r[1] / th)));
                mb.Uvs.Add(new Vector2((float)(r[2] / tw), 1f - (float)(r[3] / th)));
                mb.Uvs.Add(new Vector2((float)(r[0] / tw), 1f - (float)(r[3] / th)));
                // 纯旋转映射不翻定向 → 三角形整体反向 (0,3,2)+(0,2,1)，保证 Cross(边) 与指定法线同向
                //（亮面不变量；旧镜像映射 (x,y,−z) det=−1 靠自翻定向配旧序——映射与绕序必须成对换）
                mb.TrisOf(slot).AddRange(new[] { baseI, baseI + 3, baseI + 2, baseI, baseI + 2, baseI + 1 });

                // 自检取实际发射三角 (0,3,2)：Cross·normal ≤ 0 即绕序/映射失配（表现为 VFACE 背面全暗）
                if ((baseI / 4) % 7 == 0)
                {
                    var a = mb.Verts[baseI];
                    var b = mb.Verts[baseI + 3];
                    var c = mb.Verts[baseI + 2];
                    if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) <= 0f) _windingViolations++;
                }
            }
        }

        // 零厚度面片保留面：内容感知——候选两面（FlatAxis 对向）在各自 UV 矩形内不透明像素多者胜出
        static string PickFlatFace(BBCube cube, BBModel model, MeshCtx ctx)
        {
            _flatTotal++;
            string def = cube.FlatAxis == 0 ? "west" : cube.FlatAxis == 1 ? "down" : "south";
            string alt = cube.FlatAxis == 0 ? "east" : cube.FlatAxis == 1 ? "up" : "north";
            if (CountOpaquePixels(cube, alt, model, ctx) > CountOpaquePixels(cube, def, model, ctx))
            {
                _flatFlipped++;
                return alt;
            }
            return def;
        }

        // 面 UV 矩形内不透明像素数（alpha ≥ 0.5 与材质 _Cutoff 一致）；矩形允许镜像编码→min/max 归一并钳制贴图范围
        static int CountOpaquePixels(BBCube cube, string face, BBModel model, MeshCtx ctx)
        {
            BBFace bf;
            cube.Faces.TryGetValue(face, out bf);
            int texIdx = 0, mapped;
            if (bf != null && bf.Texture >= 0 && ctx.TexMap.TryGetValue(bf.Texture, out mapped)) texIdx = mapped;
            var tex = ctx.Texes[texIdx];
            int tw = tex.Bb.Width > 0 ? tex.Bb.Width : model.ResolutionW;
            int th = tex.Bb.Height > 0 ? tex.Bb.Height : model.ResolutionH;
            if (tw <= 0) tw = 16;
            if (th <= 0) th = 16;
            var px = PixelsOf(tex);
            if (px.Length < tw * th) return 0;
            var r = BBModelParser.ResolveFaceUv(cube, face, model);
            // 行映射：UV 的 v=0 是贴图顶部（BB/作者朝向），GetPixels 行 0 是底部——
            // authored 行区间 [vLo,vHi) 必须翻转为 GetPixels 行 [h−vHi, h−vLo)；列 u 不受影响。
            // （直接拿 v 当行索引用=垂直镜像读图，艺术区在半张图时计数全零→选面全走默认）
            int x1 = Math.Max(0, (int)Math.Ceiling(Math.Min(r[0], r[2])));
            int x2 = Math.Min(tw, (int)Math.Ceiling(Math.Max(r[0], r[2])));
            int vLo = Math.Max(0, (int)Math.Floor(Math.Min(r[1], r[3])));
            int vHi = Math.Min(th, (int)Math.Ceiling(Math.Max(r[1], r[3])));
            int y1 = Math.Max(0, th - vHi), y2 = Math.Min(th, th - vLo);
            int count = 0;
            for (int y = y1; y < y2; y++)
                for (int x = x1; x < x2; x++)
                    if (px[y * tw + x].a >= 0.5f) count++;
            return count;
        }

        // 贴图像素惰性解码（临时 Texture2D.LoadImage，取完 Color[] 即弃）。
        // 注意 GetPixels 行 0=图像底部，BB UV v=0=顶部——消费方必须做行翻转（见 CountOpaquePixels）
        static Color[] PixelsOf(TexInfo t)
        {
            if (t.Pixels != null) return t.Pixels;
            if (t.Bb.Png != null)
            {
                var tmp = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (tmp.LoadImage(t.Bb.Png))
                {
                    t.Pixels = tmp.GetPixels();
                    UnityEngine.Object.DestroyImmediate(tmp);
                    return t.Pixels;
                }
                UnityEngine.Object.DestroyImmediate(tmp);
            }
            t.Pixels = new Color[0];
            return t.Pixels;
        }

        static Mesh FinishMesh(MeshBuffers mb, string meshName)
        {
            var mesh = new Mesh { name = Sanitize(meshName) + "_mesh" };
            if (mb.Verts.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(mb.Verts);
            mesh.SetNormals(mb.Norms);
            mesh.SetUVs(0, mb.Uvs);
            mesh.subMeshCount = mb.Tris.Count;
            for (int i = 0; i < mb.Tris.Count; i++) mesh.SetTriangles(mb.Tris[i], i);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ---------- BB→Unity 映射（绕Y 180°纯旋转 diag(−1,1,−1)：点/法线 (−x,y,−z)，四元数纯旋转共轭） ----------
        static Vector3 MapPt(BBV3 v) => new Vector3((float)-v.X, (float)v.Y, -(float)v.Z) * UnitScale;
        static Vector3 MapDir(BBV3 v) => new Vector3((float)-v.X, (float)v.Y, -(float)v.Z);
        static Quaternion MapQ(BBQuat q) => new Quaternion((float)-q.X, (float)q.Y, (float)-q.Z, (float)q.W);

        static BBV3 FaceNormalBB(string face)
        {
            switch (face)
            {
                case "north": return new BBV3 { Z = -1 };
                case "south": return new BBV3 { Z = 1 };
                case "west": return new BBV3 { X = -1 };
                case "east": return new BBV3 { X = 1 };
                case "up": return new BBV3 { Y = 1 };
                default: return new BBV3 { Y = -1 };
            }
        }

        // 面四角（BB 坐标系，外侧视角 TL,TR,BR,BL）：north=-Z、south=+Z、west=-X、east=+X、up=+Y、down=-Y
        static void FaceCornersBB(string face, double[] f, double[] t,
            out BBV3 tl, out BBV3 tr, out BBV3 br, out BBV3 bl)
        {
            double x1 = f[0], y1 = f[1], z1 = f[2], x2 = t[0], y2 = t[1], z2 = t[2];
            switch (face)
            {
                case "north":
                    tl = V(x2, y2, z1); tr = V(x1, y2, z1); br = V(x1, y1, z1); bl = V(x2, y1, z1);
                    break;
                case "south":
                    tl = V(x1, y2, z2); tr = V(x2, y2, z2); br = V(x2, y1, z2); bl = V(x1, y1, z2);
                    break;
                case "west":
                    tl = V(x1, y2, z1); tr = V(x1, y2, z2); br = V(x1, y1, z2); bl = V(x1, y1, z1);
                    break;
                case "east":
                    tl = V(x2, y2, z2); tr = V(x2, y2, z1); br = V(x2, y1, z1); bl = V(x2, y1, z2);
                    break;
                case "up":
                    tl = V(x1, y2, z1); tr = V(x2, y2, z1); br = V(x2, y2, z2); bl = V(x1, y2, z2);
                    break;
                default: // down
                    tl = V(x1, y1, z2); tr = V(x2, y1, z2); br = V(x2, y1, z1); bl = V(x1, y1, z1);
                    break;
            }
        }

        static BBV3 V(double x, double y, double z) => new BBV3 { X = x, Y = y, Z = z };
    }

    static class DictExt
    {
        public static TValue GetOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> d, TKey key, TValue def)
        {
            TValue v;
            return d.TryGetValue(key, out v) ? v : def;
        }
    }
}
