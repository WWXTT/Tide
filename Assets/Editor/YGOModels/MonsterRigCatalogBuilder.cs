using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CardCore;
using MonsterRig;
using UnityEditor;
using UnityEngine;

namespace YGOImport
{
    /// <summary>
    /// 体型分类目录生成器（2026-10-12 美术待办§二）：扫描 Assets/Art/YGOMonstersRig 全部
    /// prefab → RigPartClassifier 分类 → 写 Assets/Configs/MonsterRigCatalog.json（运行时只读）。
    ///
    /// - 幂等可重跑：坐标转换修正重烘模型后重跑本菜单，铰链轴/头朝向随新姿态重算；
    /// - 手改保护：条目 manual=true 原样保留（误判修正写进 JSON 再标 manual）；
    /// - 报告落 Logs/MonsterRigCatalogReport.txt（各体型清单 + 未知名词表 + 兜底统计）。
    /// </summary>
    public static class MonsterRigCatalogBuilder
    {
        const string RigRoot = "Assets/Art/YGOMonstersRig";
        const string OutputPath = "Assets/Configs/MonsterRigCatalog.json";
        const string ReportPath = "Logs/MonsterRigCatalogReport.txt";

        [MenuItem("Tools/YGO模型/4 生成体型分类目录（→Configs）")]
        public static void Run()
        {
            string projRoot = Path.GetDirectoryName(Application.dataPath);
            string rigAbs = Path.Combine(projRoot, RigRoot);
            if (!Directory.Exists(rigAbs))
            {
                Debug.LogError("[RigCatalog] 源目录不存在：" + rigAbs);
                return;
            }
            var prefabPaths = Directory.GetFiles(rigAbs, "*.prefab", SearchOption.AllDirectories)
                .Where(p => !p.Replace('\\', '/').Contains("/Logs/")).ToList();
            if (prefabPaths.Count == 0)
            {
                Debug.LogError("[RigCatalog] 未找到 prefab");
                return;
            }

            // 手改条目保护
            var manualKeep = new Dictionary<string, MonsterRigEntry>(StringComparer.OrdinalIgnoreCase);
            try
            {
                string existing = File.ReadAllText(Path.Combine(projRoot, OutputPath));
                var oldRoot = TideJson.FromJson<MonsterRigCatalogRoot>(existing);
                if (oldRoot != null && oldRoot.models != null)
                    foreach (var e in oldRoot.models)
                        if (e != null && e.manual && !string.IsNullOrEmpty(e.name))
                            manualKeep[e.name] = e;
            }
            catch { /* 首次生成/文件损坏 = 全量重建 */ }

            var unknownNames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int headFwdJaw = 0, headFwdBody = 0, headFwdFallback = 0;
            int hingeFallback = 0;
            var entries = new List<MonsterRigEntry>();
            int manualUsed = 0;
            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                for (int i = 0; i < prefabPaths.Count; i++)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("体型分类目录",
                        Path.GetFileNameWithoutExtension(prefabPaths[i]) + $"（{i + 1}/{prefabPaths.Count}）",
                        (float)i / prefabPaths.Count))
                    {
                        Debug.LogWarning("[RigCatalog] 用户取消，未写盘");
                        return;
                    }
                    string name = Path.GetFileNameWithoutExtension(prefabPaths[i]);
                    if (manualKeep.ContainsKey(name)) { entries.Add(manualKeep[name]); manualUsed++; continue; }

                    // AssetDatabase 只认 Assets/ 起的相对路径（绝对路径静默返回 null）
                    string relPath = prefabPaths[i].Substring(projRoot.Length + 1).Replace('\\', '/');
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(relPath);
                    if (prefab == null) { Debug.LogWarning("[RigCatalog] 加载失败：" + name); continue; }
                    entries.Add(BuildEntry(prefab.transform, name, unknownNames,
                        ref headFwdJaw, ref headFwdBody, ref headFwdFallback, ref hingeFallback));
                }
            }
            finally { EditorUtility.ClearProgressBar(); }

            entries.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            var root = new MonsterRigCatalogRoot
            {
                models = entries,
                generatedNote = $"生成于 {DateTime.Now:yyyy-MM-dd HH:mm}；规则见 Assets/Editor/YGOModels/RigPartClassifier.cs；"
                    + "手改条目须 manual=true；重烘模型后重跑本菜单",
            };
            File.WriteAllText(Path.Combine(projRoot, OutputPath), TideJson.ToJson(root, true));
            AssetDatabase.ImportAsset(OutputPath, ImportAssetOptions.ForceUpdate);
            MonsterRigCatalog.Reload();

            WriteReport(entries, unknownNames, prefabPaths.Count, manualUsed,
                headFwdJaw, headFwdBody, headFwdFallback, hingeFallback, projRoot, sw);

            var counts = CountByType(entries);
            Debug.Log($"[RigCatalog] 完成：{entries.Count} 条（手改保留 {manualUsed}）→ {OutputPath}，耗时 {sw.Elapsed.TotalSeconds:F1}s\n"
                + TypeSummary(counts)
                + $"\n头朝向来源：颌 {headFwdJaw} / 躯干 {headFwdBody} / 兜底+Z {headFwdFallback}；翅轴兜底 {hingeFallback}\n"
                + $"未识别骨骼名 {unknownNames.Count} 种（明细见 {ReportPath}）");
        }

        // ================= 单条目构建 =================

        static MonsterRigEntry BuildEntry(Transform root, string name,
            Dictionary<string, int> unknownNames,
            ref int fwdJaw, ref int fwdBody, ref int fwdFallback, ref int hingeFallback)
        {
            var nodes = RigPartClassifier.BuildTree(root);
            var all = RigPartClassifier.AllNodes(nodes).ToList();
            foreach (var n in all)
                if (n.Info.Kind == RigPartClassifier.PartKind.None)
                {
                    int c; unknownNames[n.T.name] = unknownNames.TryGetValue(n.T.name, out c) ? c + 1 : 1;
                }

            var facts = RigPartClassifier.CollectFacts(nodes);
            string type = RigPartClassifier.DecideMoveType(facts);

            var e = new MonsterRigEntry { name = name, moveType = type };

            RigPartClassifier.Node Pick(params RigPartClassifier.PartKind[] kinds)
            {
                foreach (var k in kinds)
                {
                    RigPartClassifier.Node best = null;
                    foreach (var n in all)
                        if (n.Info.Kind == k && (best == null || n.Info.Index < best.Info.Index))
                            best = n;
                    if (best != null) return best;
                }
                return null;
            }

            var headNode = Pick(RigPartClassifier.PartKind.Head);
            var jawNode = Pick(RigPartClassifier.PartKind.Jaw);
            var abdomenNode = Pick(RigPartClassifier.PartKind.Abdomen);
            if (headNode != null) e.head = headNode.T.name;
            if (jawNode != null) e.jaw = jawNode.T.name;
            if (abdomenNode != null) e.abdomen = abdomenNode.T.name;

            // 躯干主骨：基名 body 优先，否则任一躯干骨
            var bodyNodes = all.Where(n => n.Info.Kind == RigPartClassifier.PartKind.Body).ToList();
            var bodyMain = bodyNodes.FirstOrDefault(n => n.Info.Base == "body") ?? bodyNodes.FirstOrDefault();
            if (bodyMain != null) e.bodyMain = bodyMain.T.name;

            // 链：尾/颈按尾号升序；多体怪物存在同名骨（两条 tail）——去重取首条，
            // 第二条同骨名运行时无法按名寻址（v1 限制，计数入报告）
            int dupChainBones = 0;
            List<string> DedupChain(IEnumerable<string> src)
            {
                var list = new List<string>();
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var n in src)
                    if (seen.Add(n)) list.Add(n);
                    else dupChainBones++;
                return list;
            }
            e.tail = DedupChain(all.Where(n => n.Info.Kind == RigPartClassifier.PartKind.Tail)
                .OrderBy(n => n.Info.Index).Select(n => n.T.name));
            e.neck = DedupChain(all.Where(n => n.Info.Kind == RigPartClassifier.PartKind.Neck)
                .OrderBy(n => n.Info.Index).Select(n => n.T.name));

            // 躯干链：近头在前（无头则高位在前），同样去重
            Vector3 headPos = headNode != null ? headNode.T.position : root.position;
            e.bodyChain = DedupChain(bodyNodes
                .OrderBy(n => headNode != null
                    ? (n.T.position - headPos).sqrMagnitude
                    : -n.T.position.y)
                .Select(n => n.T.name));

            // 参考点位（prefab 资产空间 = 根空间，根恒等变换）
            Vector3 bodyCenter = bodyNodes.Count > 0
                ? new Vector3(bodyNodes.Average(n => n.T.position.x), bodyNodes.Average(n => n.T.position.y), bodyNodes.Average(n => n.T.position.z))
                : root.position;

            // 肢体（腿/臂/软体）：链根 → 链；相位 = 序号均分 + 镜像反相
            List<LimbRef> Limbs(int superFamily)
            {
                var limbRoots = all.Where(n => n.IsLimbRoot && RigPartClassifier.SuperFamily(n.Info.Kind) == superFamily).ToList();
                var list = new List<LimbRef>(limbRoots.Count);
                for (int i = 0; i < limbRoots.Count; i++)
                {
                    var chain = RigPartClassifier.ChainOf(limbRoots[i]);
                    list.Add(new LimbRef
                    {
                        bones = chain.Select(c => c.T.name).ToList(),
                        phase = i * Mathf.PI * 2f / Mathf.Max(1, limbRoots.Count)
                            + (limbRoots[i].Info.Side < 0 ? Mathf.PI : 0f),
                        side = limbRoots[i].Info.Side == 0 ? 1 : limbRoots[i].Info.Side,
                    });
                }
                return list;
            }
            e.legs = Limbs(1);
            e.arms = Limbs(2);
            e.soft = Limbs(4);

            // 翅/鳍：链 + 根空间铰链轴（cross(up, 翅展方向)）
            var wingRoots = all.Where(n => n.IsLimbRoot && RigPartClassifier.SuperFamily(n.Info.Kind) == 3).ToList();
            foreach (var w in wingRoots)
            {
                Vector3 wingCenter = SubtreeRenderersCenter(w);
                Vector3 tip = wingCenter - bodyCenter;
                tip.y = 0f;
                if (tip.sqrMagnitude < 1e-6f)
                {
                    tip = Vector3.right * (w.Info.Side == 0 ? 1 : w.Info.Side);
                    hingeFallback++;
                }
                Vector3 hinge = Vector3.Cross(Vector3.up, tip.normalized).normalized;
                e.wings.Add(new WingLimb
                {
                    bones = RigPartClassifier.ChainOf(w).Select(c => c.T.name).ToList(),
                    phase = 0f,
                    side = w.Info.Side == 0 ? 1 : w.Info.Side,
                    hingeAxis = hinge,
                    amplitudeDeg = 10f,
                });
            }

            // 头朝向（根空间）：颌指向 > 头-躯干指向 > +Z 兜底
            Vector3 fwd = Vector3.forward;
            if (headNode != null)
            {
                Vector3 v = Vector3.zero;
                bool haveDir = false;
                if (jawNode != null)
                {
                    v = jawNode.T.position - headNode.T.position;
                    v.y = 0f;
                    haveDir = v.sqrMagnitude > 1e-6f;
                    if (haveDir) fwdJaw++;
                }
                if (!haveDir)
                {
                    v = headNode.T.position - bodyCenter;
                    v.y = 0f;
                    haveDir = v.sqrMagnitude > 1e-6f;
                    if (haveDir) fwdBody++;
                }
                if (haveDir) fwd = v.normalized;
                else { fwdFallback++; e.note = "headForward 兜底+Z"; }
            }
            else { fwdFallback++; e.note = "无头"; }
            e.headForward = fwd;
            e.yawOffsetDeg = Vector3.SignedAngle(Vector3.forward, fwd, Vector3.up);
            if (dupChainBones > 0)
                e.note = (string.IsNullOrEmpty(e.note) ? "" : e.note + "；") + $"链同名去重 {dupChainBones}（多体怪物，仅驱动首条）";

            return e;
        }

        /// <summary>骨骼子树的网格包围盒中心均值（MeshFilter.sharedMesh.bounds → 世界）。</summary>
        static Vector3 SubtreeRenderersCenter(RigPartClassifier.Node node)
        {
            double x = 0, y = 0, z = 0; int c = 0;
            foreach (var n in RigPartClassifier.AllNodes(new List<RigPartClassifier.Node> { node }))
            {
                var mf = n.T.GetComponent<MeshFilter>();
                var mesh = mf != null ? mf.sharedMesh : null;
                if (mesh == null) continue;
                Vector3 wc = n.T.localToWorldMatrix.MultiplyPoint3x4(mesh.bounds.center);
                x += wc.x; y += wc.y; z += wc.z; c++;
            }
            return c == 0 ? node.T.position : new Vector3((float)(x / c), (float)(y / c), (float)(z / c));
        }

        // ================= 报告 =================

        static Dictionary<string, int> CountByType(List<MonsterRigEntry> entries)
        {
            var d = new Dictionary<string, int>();
            foreach (var e in entries)
            {
                int c; d.TryGetValue(e.moveType, out c);
                d[e.moveType] = c + 1;
            }
            return d;
        }

        static string TypeSummary(Dictionary<string, int> counts)
        {
            var sb = new StringBuilder();
            foreach (var kv in counts.OrderByDescending(k => k.Value))
                sb.Append($"  {kv.Key}: {kv.Value}\n");
            return sb.ToString().TrimEnd('\n');
        }

        static void WriteReport(List<MonsterRigEntry> entries, Dictionary<string, int> unknownNames,
            int prefabCount, int manualUsed,
            int fwdJaw, int fwdBody, int fwdFallback, int hingeFallback,
            string projRoot, System.Diagnostics.Stopwatch sw)
        {
            try
            {
                Directory.CreateDirectory("Logs");
                var sb = new StringBuilder();
                sb.AppendLine($"== MonsterRigCatalog 生成报告 {DateTime.Now:yyyy-MM-dd HH:mm} ==");
                sb.AppendLine($"prefab {prefabCount} → 条目 {entries.Count}（手改保留 {manualUsed}），耗时 {sw.Elapsed.TotalSeconds:F1}s");
                sb.AppendLine();
                foreach (var g in entries.GroupBy(e => e.moveType).OrderByDescending(g => g.Count()))
                {
                    sb.AppendLine($"[{g.Key}] {g.Count()}");
                    foreach (var name in g.Select(e => e.name).OrderBy(s => s, StringComparer.Ordinal))
                        sb.AppendLine("  " + name);
                    sb.AppendLine();
                }
                sb.AppendLine($"[头朝向来源] 颌 {fwdJaw} / 躯干 {fwdBody} / 兜底+Z {fwdFallback}；[翅轴兜底] {hingeFallback}");
                sb.AppendLine();
                sb.AppendLine($"[未识别骨骼名] {unknownNames.Count} 种（可补进 RigPartClassifier.Table）");
                foreach (var kv in unknownNames.OrderByDescending(k => k.Value).Take(80))
                    sb.AppendLine($"  {kv.Value,4} × {kv.Key}");
                File.WriteAllText(Path.Combine(projRoot, ReportPath), sb.ToString());
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[RigCatalog] 报告写盘失败：" + ex.Message);
            }
        }
    }
}
