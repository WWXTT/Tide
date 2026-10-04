using System.Text;
using UnityEditor;
using UnityEngine;

namespace Tide.EditorVerify
{
    /// <summary>EffectUI 预制体诊断/修复工具（2026-10-04）：诊断=全树组件清单落盘 Temp/；
    /// 修复=剥离 Prefab 阶段内 missing script（运行时层级拷入 prefab 时 UiKit.DescTag 引用丢失，
    /// 控件本体完好，剥离无损失——DescTag 由运行时 Described() 重建）→ 保存 → 落盘复核。
    /// 手动拷贝运行时层级的工作流会复发此问题，故常驻。</summary>
    public static class EffectUiMissingDiag
    {
        private const string TargetPath = "Assets/Art/UI/EffectUI.prefab";

        [MenuItem("Tools/验证/EffectUI缺失脚本诊断")]
        public static void Diag()
        {
            var sb = new StringBuilder();
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.prefabContentsRoot != null)
            {
                sb.AppendLine($"[Diag] 打开的Prefab阶段: {stage.assetPath}");
                Walk(stage.prefabContentsRoot.transform, sb);
            }
            else
            {
                sb.AppendLine("[Diag] 当前没有打开的Prefab阶段");
            }
            Write("Temp/EffectUiMissingDiag.txt", sb);
        }

        [MenuItem("Tools/验证/EffectUI修复缺失脚本")]
        public static void Fix()
        {
            var sb = new StringBuilder();
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null || stage.prefabContentsRoot == null || stage.assetPath != TargetPath)
            {
                sb.AppendLine("[Fix] 前置不符：EffectUI.prefab 的 Prefab 阶段未打开，未做任何修改。");
                Write("Temp/EffectUiFix.txt", sb);
                return;
            }

            // 1) 剥离阶段内容里的 missing script 组件（改前逐个记录）
            int removed = 0, brokenObjects = 0;
            var t = stage.prefabContentsRoot.transform;
            for (int i = 0; i < t.childCount; i++) Strip(t.GetChild(i), sb, ref removed, ref brokenObjects);
            sb.AppendLine($"[Fix] 剥离完成：{brokenObjects} 个物体共移除 {removed} 个失效组件");

            // 2) 保存到资产路径
            bool saved = false;
            try
            {
                PrefabUtility.SaveAsPrefabAsset(stage.prefabContentsRoot, TargetPath);
                saved = true;
            }
            catch (System.Exception ex)
            {
                sb.AppendLine($"[Fix] 保存异常: {ex.Message}");
            }
            sb.AppendLine($"[Fix] SaveAsPrefabAsset: {(saved ? "成功" : "失败")}");

            // 3) 落盘复核：重载资产，确认 0 missing、手动槽在位
            var root = PrefabUtility.LoadPrefabContents(TargetPath);
            int diskMiss = 0, diskTotal = 0;
            var content = root.transform.Find("main/left/slot-area/viewport/content");
            bool manualSlotAlive = content != null && content.Find("slot") != null;
            Count(root.transform, ref diskTotal, ref diskMiss);
            PrefabUtility.UnloadPrefabContents(root);
            sb.AppendLine($"[Fix] 磁盘复核: 物体={diskTotal} missing={diskMiss} 手动槽slot在位={manualSlotAlive}");
            Write("Temp/EffectUiFix.txt", sb);
        }

        private static void Strip(Transform t, StringBuilder sb, ref int removed, ref int brokenObjects)
        {
            int miss = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
            if (miss > 0)
            {
                brokenObjects++;
                removed += miss;
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                sb.AppendLine($"[Fix] 已剥离 x{miss}: {t.name}");
            }
            for (int i = 0; i < t.childCount; i++) Strip(t.GetChild(i), sb, ref removed, ref brokenObjects);
        }

        private static void Count(Transform t, ref int total, ref int miss)
        {
            total++;
            miss += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
            for (int i = 0; i < t.childCount; i++) Count(t.GetChild(i), ref total, ref miss);
        }

        private static void Walk(Transform t, StringBuilder sb)
        {
            int miss = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
            var types = new StringBuilder();
            foreach (var c in t.GetComponents<Component>())
                types.Append(c == null ? "<MISSING>" : c.GetType().Name).Append(',');
            string mark = miss > 0 ? $" *** 缺失x{miss}" : "";
            sb.AppendLine($"{RelPath(t)}{mark}  [{types}]");
            for (int i = 0; i < t.childCount; i++) Walk(t.GetChild(i), sb);
        }

        private static string RelPath(Transform t)
        {
            var s = t.name;
            var p = t.parent;
            int depth = 0;
            while (p != null && depth < 3) { s = p.name + "/" + s; p = p.parent; depth++; }
            return p != null ? "…/" + s : s;
        }

        private static void Write(string path, StringBuilder sb)
        {
            Debug.Log(sb.ToString());
            System.IO.File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }
    }
}
