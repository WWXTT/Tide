using System.Collections.Generic;
using System.IO;
using System.Text;
using MonsterRig;
using UnityEditor;
using UnityEngine;

namespace YGOImport
{
    /// <summary>
    /// YGOMonstersRig 按模块数定大小（2026-10-14 用户定案，取代 2026-10-13 全部归一 1：
    /// "太粗暴"——多肢体大型生物与简单小生物被压成一样大）：
    /// 目标=肢体构成模块数分档 —— <5 块→0.7、5~10 块→1、>10 块→1.5（2026-10-14 用户给定标准）。
    /// 模块数 = catalog 部位分解：腿/臂/翅/软体肢体实例数 + 颈/尾/躯干链的链段骨数
    /// （蛇长躯干、多腿蜘蛛、四足双翅龙自然落入高档；史莱姆/蛋类落入低档）。
    /// 指标不变=根空间包围盒三轴最大值（蛇自然取体长、有翅自然取翼展），均匀缩放 k=目标/现值
    /// 烘进全部 localPosition + 全部 mesh 顶点（localScale 恒1、根恒等、脚底y=0、XZ居中不变）。
    /// 缩到精确目标值 ⇒ 幂等可重跑（catalog 改部位后重跑即按新模块数改档）。
    /// 体型差异化已烘进资产：运行时不再放大（BattleFieldView 属性缩放 2026-10-14 同日退役）。
    ///
    /// catalog 缺条目（当前覆盖率 100%）按目标 1 处理并在报告点名。moveType 仅作报告分组。
    /// 菜单6干跑 → 核对 Logs/MonsterRigSizeScaleReport.txt → 菜单7执行（报告同文件）。
    /// 防泄漏守卫：模型引用的 mesh 必须都在自身目录内，出现外部/共享 mesh 则整只跳过不缩。
    /// </summary>
    public static class MonsterRigSizeScaler
    {
        const string RigRoot = "Assets/Art/YGOMonstersRig";
        const string ReportPath = "Logs/MonsterRigSizeScaleReport.txt";
        const float TargetSmall = 0.7f;  // 模块 <5
        const float TargetNormal = 1f;   // 模块 5~10
        const float TargetLarge = 1.5f;  // 模块 >10
        const float DeadZone = 0.001f; // |k-1| 死区防 fp 无限微触发

        [MenuItem("Tools/YGO模型/6 按模块数定大小（干跑）")]
        public static void DryRun() { Run(false); }

        [MenuItem("Tools/YGO模型/7 按模块数定大小（执行）")]
        public static void ExecuteRun() { Run(true); }

        /// <summary>目标分档（2026-10-14 用户标准）：&lt;5→0.7、5~10→1、&gt;10→1.5。</summary>
        static float TargetOf(int modules)
            => modules < 5 ? TargetSmall : (modules > 10 ? TargetLarge : TargetNormal);

        /// <summary>肢体构成模块数 = 肢体实例（腿/臂/翅/软体）+ 链段骨（颈/尾/躯干）。
        /// catalog 列表解析异常时按空表计（不炸整轮）。</summary>
        static int ModuleCountOf(MonsterRigEntry e)
        {
            int Limbs(List<LimbRef> l) => l?.Count ?? 0;
            int Wings(List<WingLimb> l) => l?.Count ?? 0;
            int Chain(List<string> l) => l?.Count ?? 0;
            return Limbs(e.legs) + Limbs(e.arms) + Wings(e.wings) + Limbs(e.soft)
                + Chain(e.neck) + Chain(e.tail) + Chain(e.bodyChain);
        }

        /// <summary>干跑/执行同一入口；返回摘要行（调用方=菜单或 execute_code 对账）。</summary>
        public static string Run(bool apply)
        {
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { RigRoot });
            var sb = new StringBuilder();
            sb.AppendLine($"YGOMonstersRig 按模块数定大小（<5→{TargetSmall}｜5~10→{TargetNormal}｜>10→{TargetLarge}） 干跑={(apply ? 0 : 1)}");
            sb.AppendLine($"prefab 总数 {guids.Length}；目录条目 {MonsterRigCatalog.All.Count}");
            sb.AppendLine();

            int scaled = 0, already = 0, failed = 0;
            var bandStats = new int[3]; // 各档命中数（小/中/大）
            var classStats = new Dictionary<string, float[]>(); // moveType/未分类 → [min,max] 缩后
            var extraNotes = new List<string>();

            for (int gi = 0; gi < guids.Length; gi++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[gi]);
                string name = Path.GetFileNameWithoutExtension(path);
                if (EditorUtility.DisplayCancelableProgressBar(
                        apply ? "按模块数定大小（执行）" : "按模块数定大小（干跑）",
                        $"{name}（{gi + 1}/{guids.Length}）", (float)gi / guids.Length))
                { sb.AppendLine($"[中断] 用户取消于 {gi}/{guids.Length}"); break; }

                // catalog 仅作模块数来源与报告分组；缺条目按目标 1 处理并点名
                var entry = MonsterRigCatalog.Get(name);
                string group = entry != null ? entry.moveType : "未分类";
                int modules = entry != null ? ModuleCountOf(entry) : -1;
                float target = entry != null ? TargetOf(modules) : TargetNormal;
                if (entry == null) extraNotes.Add($"{name}: 无目录条目，按目标 {TargetNormal:F2} 处理");
                bandStats[modules < 0 ? 1 : BandIndexOf(modules)]++;

                GameObject root = null;
                try
                {
                    root = PrefabUtility.LoadPrefabContents(path);
                    var renderers = root.GetComponentsInChildren<Renderer>(true);
                    if (renderers.Length == 0) { failed++; sb.AppendLine($"{name}\t[失败] 无 Renderer"); continue; }

                    var bounds = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                    float maxDim = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                    if (maxDim <= 0.0001f) { failed++; sb.AppendLine($"{name}\t[失败] 包围盒为空"); continue; }

                    string moduleLabel = modules >= 0 ? $"模块{modules}" : "模块?";
                    float k = target / maxDim;
                    if (Mathf.Abs(k - 1f) <= DeadZone)
                    {
                        already++;
                        sb.AppendLine($"{name}\t{GroupLabel(group)}\t{moduleLabel}\t已达目标 {maxDim:F3}≈{target:F2}（跳过）");
                        TrackClass(classStats, group, maxDim);
                        continue;
                    }

                    if (!apply)
                    {
                        scaled++;
                        sb.AppendLine($"{name}\t{GroupLabel(group)}\t{moduleLabel}\t{maxDim:F3}\tk={k:F3}\t→{target:F2}");
                        TrackClass(classStats, group, target);
                        continue;
                    }

                    // 守卫：所有 mesh 必须在本模型目录内（防跨模型共享 mesh 泄漏缩放）
                    var meshes = new HashSet<Mesh>();
                    foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                        if (mf.sharedMesh != null) meshes.Add(mf.sharedMesh);
                    foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                        if (smr.sharedMesh != null) meshes.Add(smr.sharedMesh);
                    string folderPrefix = Path.GetDirectoryName(path).Replace('\\', '/') + "/";
                    foreach (var mesh in meshes)
                    {
                        string mp = AssetDatabase.GetAssetPath(mesh);
                        if (string.IsNullOrEmpty(mp) || !mp.StartsWith(folderPrefix, System.StringComparison.OrdinalIgnoreCase))
                            throw new System.Exception($"外部/共享 mesh：{mp}");
                    }

                    // 缩放：全部局部位置 + 全部顶点（不含根；localScale 恒 1 不动）
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                        if (t != root.transform) t.localPosition = t.localPosition * k;
                    foreach (var mesh in meshes)
                    {
                        var verts = mesh.vertices;
                        for (int i = 0; i < verts.Length; i++) verts[i] *= k;
                        mesh.vertices = verts;
                        mesh.RecalculateBounds();
                        EditorUtility.SetDirty(mesh);
                    }

                    // 复测断言：缩后最大轴应落在目标上、脚底 y≈0（均匀缩放天然保持 0 与 XZ 居中）
                    bounds = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                    float afterMax = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                    if (Mathf.Abs(afterMax - target) > 0.005f)
                        extraNotes.Add($"{name}: 缩后 {afterMax:F3} ≠ 目标 {target:F2}");
                    if (Mathf.Abs(bounds.min.y) > 0.01f)
                        extraNotes.Add($"{name}: 脚底 y={bounds.min.y:F3} ≠ 0");

                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    scaled++;
                    sb.AppendLine($"{name}\t{GroupLabel(group)}\t{moduleLabel}\t{maxDim:F3}\tk={k:F3}\t→{afterMax:F3}");
                    TrackClass(classStats, group, afterMax);
                }
                catch (System.Exception ex)
                {
                    failed++;
                    sb.AppendLine($"{name}\t[失败] {ex.Message}");
                }
                finally
                {
                    if (root != null) PrefabUtility.UnloadPrefabContents(root);
                }
            }

            EditorUtility.ClearProgressBar();
            if (apply) AssetDatabase.SaveAssets();

            sb.AppendLine();
            sb.AppendLine($"== 汇总：{(apply ? "已缩放" : "待缩放")} {scaled}｜已达跳过 {already}｜失败 {failed}");
            sb.AppendLine($"  分档命中：小(<5→{TargetSmall}) {bandStats[0]}｜中(5~10→{TargetNormal}) {bandStats[1]}｜大(>10→{TargetLarge}) {bandStats[2]}");
            foreach (var kv in classStats)
                sb.AppendLine($"  {GroupLabel(kv.Key)}: 缩后范围 {kv.Value[0]:F3} ~ {kv.Value[1]:F3}");
            foreach (var n in extraNotes) sb.AppendLine("  [注意] " + n);

            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, sb.ToString(), new UTF8Encoding(false));
            string summary = $"[MonsterRigSizeScaler] 干跑={(apply ? 0 : 1)} 待缩/已缩 {scaled}｜已达 {already}｜失败 {failed}｜分档 小{bandStats[0]}/中{bandStats[1]}/大{bandStats[2]}｜报告 {ReportPath}";
            Debug.Log(summary);
            return summary;
        }

        /// <summary>模块数→分档下标（0=小 1=中 2=大）；与 TargetOf 同一阈值。</summary>
        static int BandIndexOf(int modules)
            => modules < 5 ? 0 : (modules > 10 ? 2 : 1);

        /// <summary>报告分组名：catalog 缺条目归"未分类"（MoveLabel 无此键）。</summary>
        static string GroupLabel(string group)
            => group == "未分类" ? group : MonsterRigCatalog.MoveLabel(group);

        static void TrackClass(Dictionary<string, float[]> stats, string moveType, float after)
        {
            float[] range;
            if (!stats.TryGetValue(moveType, out range)) stats[moveType] = new[] { after, after };
            else { if (after < range[0]) range[0] = after; if (after > range[1]) range[1] = after; }
        }
    }
}
