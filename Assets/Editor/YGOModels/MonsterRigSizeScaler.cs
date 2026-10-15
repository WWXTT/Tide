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
    /// 目标=肢体构成模块数分档 —— <5 块→0.9、5~10 块→1、>10 块→1.2
    /// （2026-10-15 用户定案三档；此前 10-14 口径 <5→1/5~10→1/>10→1.5 作废）。
    /// 模块数 = catalog 部位分解：腿/臂/翅/软体肢体实例数 + 颈/尾/躯干链的链段骨数
    /// （蛇长躯干、多腿蜘蛛、四足双翅龙自然落入高档；史莱姆/蛋类落入低档）。
    /// 指标（2026-10-15 改版）=根空间包围盒三轴最大值，但包围盒只统计 catalog 部位链上的
    /// renderer（本体口径）——武器/道具（spear/key/umbrella/bike/throne 等不在任何部位 bones 内）
    /// 天然排除，持械模型不再被武器跨度压小本体（教训：Giltia 斜枪占满 X=1 本体仅 0.84、
    /// Kojikocy 竖剑顶满 Y=1 头顶仅 0.72、KeyMace 平钥匙占满 Z=1 本体仅 0.68）。
    /// 蛇取体长/有翅取翼展口径不变（躯干链/翅骨都在部位内）。部位 renderer 为 0 或本体最长轴
    /// < 全长 1/4（部位覆盖异常）时回落全长口径并在报告点名。
    /// 均匀缩放 k=目标/本体现值，烘进全部 localPosition + 全部 mesh 顶点（含武器——
    /// 整只等比变长、武器伸出格盘属预期）。
    /// 缩到精确目标值 ⇒ 幂等可重跑（catalog 改部位后重跑即按新模块数改档）。
    /// 2026-10-15 裁决：撤销「只放大不缩小」守卫，全部模型（含 10-14 手修保持名单/Gaia）
    /// 按新三档全量重生成到精确目标——过大的缩小（武器随整只等比）、过小的放大；
    /// 幂等跳过仅剩 |k-1| 死区。
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
        const float TargetSmall = 0.9f;  // 模块 <5（2026-10-15 全量重生成三档定案）
        const float TargetNormal = 1f;   // 模块 5~10
        const float TargetLarge = 1.2f;  // 模块 >10
        const float DeadZone = 0.001f; // |k-1| 死区防 fp 无限微触发

        [MenuItem("Tools/YGO模型/6 按模块数定大小（干跑）")]
        public static void DryRun() { Run(false); }

        [MenuItem("Tools/YGO模型/7 按模块数定大小（执行）")]
        public static void ExecuteRun() { Run(true); }

        /// <summary>目标分档（2026-10-15 用户标准）：&lt;5→0.9、5~10→1、&gt;10→1.2。</summary>
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
            sb.AppendLine($"YGOMonstersRig 按模块数定大小（本体口径·<5→{TargetSmall}｜5~10→{TargetNormal}｜>10→{TargetLarge}） 干跑={(apply ? 0 : 1)}");
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

                    // 2026-10-15 指标改版：本体口径 = 只统计 catalog 部位链 renderer 的包围盒
                    // （武器/道具不在部位 bones 内被天然排除）；部位覆盖异常时回落全长口径。
                    var partNames = PartNamesOf(entry);
                    var fullBounds = EncapsulateAll(renderers, null, out int _);
                    float fullMax = MaxAxis(fullBounds);
                    var bodyBounds = EncapsulateAll(renderers, partNames, out int bodyCount);
                    float bodyMax = bodyCount > 0 ? MaxAxis(bodyBounds) : 0f;
                    bool bodyMetric = bodyCount > 0 && bodyMax >= fullMax * 0.25f;
                    float maxDim = bodyMetric ? bodyMax : fullMax;
                    if (entry != null && !bodyMetric)
                        extraNotes.Add($"{name}: 本体口径回落全长（部位renderer {bodyCount}·本体 {bodyMax:F2}·全长 {fullMax:F2}）");
                    if (maxDim <= 0.0001f) { failed++; sb.AppendLine($"{name}\t[失败] 包围盒为空"); continue; }

                    string moduleLabel = modules >= 0 ? $"模块{modules}" : "模块?";
                    float k = target / maxDim;
                    if (Mathf.Abs(k - 1f) <= DeadZone)
                    {
                        already++;
                        sb.AppendLine($"{name}\t{GroupLabel(group)}\t{moduleLabel}\t已达目标 {(bodyMetric ? "本体" : "全长")}{maxDim:F3}≈{target:F2}（跳过）");
                        TrackClass(classStats, group, maxDim);
                        continue;
                    }

                    if (!apply)
                    {
                        scaled++;
                        sb.AppendLine($"{name}\t{GroupLabel(group)}\t{moduleLabel}\t{(bodyMetric ? "本体" : "全长")}{maxDim:F3}/全长{fullMax:F3}\t部位{bodyCount}/{renderers.Length}\tk={k:F3}\t→{target:F2}");
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

                    // 复测断言（2026-10-15 本体口径）：缩后本体最大轴应落在目标上、脚底 y≈0
                    // （均匀缩放天然保持 0 与 XZ 居中）；全长（含武器）可超目标属预期，不参与断言。
                    var afterBody = EncapsulateAll(renderers, bodyMetric ? partNames : null, out int _);
                    float afterMax = MaxAxis(afterBody);
                    if (Mathf.Abs(afterMax - target) > 0.005f)
                        extraNotes.Add($"{name}: 缩后本体 {afterMax:F3} ≠ 目标 {target:F2}");
                    var afterFull = EncapsulateAll(renderers, null, out int _);
                    if (Mathf.Abs(afterFull.min.y) > 0.01f)
                        extraNotes.Add($"{name}: 脚底 y={afterFull.min.y:F3} ≠ 0");

                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    scaled++;
                    sb.AppendLine($"{name}\t{GroupLabel(group)}\t{moduleLabel}\t{(bodyMetric ? "本体" : "全长")}{maxDim:F3}/全长{fullMax:F3}\tk={k:F3}\t→本体{afterMax:F3}/全长{MaxAxis(afterFull):F3}");
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

        /// <summary>catalog 部位链全部骨骼名（本体口径的 renderer 名单）。entry 为 null 返回空集。</summary>
        static HashSet<string> PartNamesOf(MonsterRigEntry e)
        {
            var s = new HashSet<string>();
            if (e == null) return s;
            if (!string.IsNullOrEmpty(e.head)) s.Add(e.head);
            if (!string.IsNullOrEmpty(e.jaw)) s.Add(e.jaw);
            if (!string.IsNullOrEmpty(e.abdomen)) s.Add(e.abdomen);
            if (!string.IsNullOrEmpty(e.bodyMain)) s.Add(e.bodyMain);
            AddAll(s, e.neck); AddAll(s, e.tail); AddAll(s, e.bodyChain);
            AddLimbs(s, e.legs); AddLimbs(s, e.arms); AddLimbs(s, e.soft); AddLimbs(s, e.wings);
            return s;
        }

        static void AddAll(HashSet<string> s, List<string> names)
        {
            if (names != null) foreach (var n in names) if (!string.IsNullOrEmpty(n)) s.Add(n);
        }

        static void AddLimbs(HashSet<string> s, List<LimbRef> limbs)
        {
            if (limbs != null) foreach (var l in limbs) AddAll(s, l.bones);
        }

        static void AddLimbs(HashSet<string> s, List<WingLimb> limbs)
        {
            if (limbs != null) foreach (var l in limbs) AddAll(s, l.bones);
        }

        /// <summary>names 为 null 时统计全部 renderer；返回累计包围盒与命中数（0=未命中，包围盒无效）。</summary>
        static Bounds EncapsulateAll(Renderer[] renderers, HashSet<string> names, out int count)
        {
            var b = default(Bounds); count = 0;
            foreach (var r in renderers)
            {
                if (names != null && !names.Contains(r.name)) continue;
                if (count == 0) b = r.bounds; else b.Encapsulate(r.bounds);
                count++;
            }
            return b;
        }

        static float MaxAxis(Bounds b) => Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));

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
