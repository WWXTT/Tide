using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using TMPro;

namespace Tide.验证
{
    /// <summary>
    /// UI 字体覆盖检查（2026-10-02 双字体定案配套，幂等可重跑）：
    /// 以 Fonts/txt.txt 字符集语料为基准，逐字核对两个 SDF 的覆盖——
    ///   SC-Heavy SDF（静态图集，无源字体）：查字符表；
    ///   庞门正道标题体.ttf（动态 SDF 的源）：Font.HasCharacter 查字形有无。
    /// 输出四组清单：SC 缺 / 庞门缺 / 两者都缺（字体本身缺失候选，换字定夺）/ SC 缺但庞门有
    /// （可 fallback 或换字）。菜单 Tools/验证/UI字体覆盖检查，结果读 Console。
    /// </summary>
    public static class UiFontCoverageCheck
    {
        private const string CorpusPath = "Assets/Packages/TextMesh Pro/Fonts/txt.txt";
        private const string ScSdfPath = "Assets/Packages/TextMesh Pro/Resources/Fonts & Materials/SC-Heavy SDF.asset";
        private const string PangmenTtfPath = "Assets/Packages/TextMesh Pro/Fonts/庞门正道标题体.ttf";

        [MenuItem("Tools/验证/UI字体覆盖检查")]
        public static void Run()
        {
            var corpus = new HashSet<char>();
            foreach (var c in File.ReadAllText(CorpusPath, Encoding.UTF8))
                if (!char.IsWhiteSpace(c) && c != '\uFEFF') corpus.Add(c);

            var sc = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ScSdfPath);
            if (sc == null) { Debug.LogError($"[字体覆盖] SC-Heavy SDF 缺失：{ScSdfPath}"); return; }
            var scChars = new HashSet<uint>();
            var so = new SerializedObject(sc);
            var table = so.FindProperty("m_CharacterTable");
            if (table != null && table.isArray)
                for (int i = 0; i < table.arraySize; i++)
                    scChars.Add((uint)table.GetArrayElementAtIndex(i).FindPropertyRelative("m_Unicode").intValue);

            var pangmen = AssetDatabase.LoadAssetAtPath<Font>(PangmenTtfPath);
            if (pangmen == null) { Debug.LogError($"[字体覆盖] 庞门源字体缺失：{PangmenTtfPath}"); return; }

            var missSc = new List<char>();
            var missPang = new List<char>();
            var missBoth = new List<char>();
            var missScOnly = new List<char>();
            foreach (var c in corpus)
            {
                bool inSc = scChars.Contains(c);
                bool inPang = pangmen.HasCharacter(c);
                if (inSc && inPang) continue;
                if (!inSc) missSc.Add(c);
                if (!inPang) missPang.Add(c);
                if (!inSc && !inPang) missBoth.Add(c);
                else if (!inSc) missScOnly.Add(c);
            }

            var sb = new StringBuilder();
            sb.AppendLine($"[字体覆盖] 语料 {corpus.Count} 字（{CorpusPath}）；SC-Heavy 字符表 {scChars.Count} 字形。");
            sb.AppendLine($"SC-Heavy 缺 {missSc.Count} 字。");
            sb.AppendLine($"庞门 ttf 缺 {missPang.Count} 字。");
            sb.AppendLine($"★ 两者都缺（字体本身缺失候选，需换字定夺）{missBoth.Count} 字：");
            AppendChars(sb, missBoth);
            sb.AppendLine($"SC 缺但庞门有（可挂 fallback 兜底或换字）{missScOnly.Count} 字：");
            AppendChars(sb, missScOnly);
            Debug.Log(sb.ToString());
            // Console 可能被用户侧过滤器遮蔽——同步落盘一份
            System.IO.Directory.CreateDirectory("Temp");
            File.WriteAllText("Temp/font_coverage.txt", sb.ToString(), Encoding.UTF8);
        }

        /// <summary>长字符清单分块输出（Console 单行过长会被截断）。</summary>
        private static void AppendChars(StringBuilder sb, List<char> chars)
        {
            const int chunk = 60;
            for (int i = 0; i < chars.Count; i += chunk)
                sb.AppendLine("  " + new string(chars.GetRange(i, Mathf.Min(chunk, chars.Count - i)).ToArray()));
            if (chars.Count == 0) sb.AppendLine("  （无）");
        }
    }
}
