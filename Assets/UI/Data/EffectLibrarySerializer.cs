using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>
    /// 效果图 JSON 读写 —— 落盘到 Assets/Configs/Effects/。
    /// 与 DeckSerializer 同模式（Application.dataPath + System.IO + JsonUtility），
    /// 保证存盘后能立即读回还原（含分支结构）。
    ///
    /// 注意：写 Assets/ 仅在编辑器内有效；打包后该目录只读，届时应改用
    /// Application.persistentDataPath。Phase 2 为编辑器内验证。
    /// </summary>
    public static class EffectLibrarySerializer
    {
        private const string DirRelative = "Configs/Effects";

        private static string Dir => Path.Combine(Application.dataPath, DirRelative);

        /// <summary>保存效果图为 &lt;name&gt;.json。返回写入的完整路径，失败返回 null。
        /// 按功能内容哈希去重：保存前删除库中任何功能内容相同（但文件名不同）的旧效果图，
        /// 使"改个名、功能不变"不会产生重复条目。</summary>
        public static string Save(EffectGraphData graph)
        {
            if (graph == null || string.IsNullOrEmpty(graph.name))
            {
                Debug.LogWarning("[EffectLibrarySerializer] 效果图为空或无名称，已跳过保存。");
                return null;
            }

            Directory.CreateDirectory(Dir);
            string path = Path.Combine(Dir, SanitizeFileName(graph.name) + ".json");
            string hash = ContentHasher.HashEffect(graph);

            // 删除功能内容相同但文件名不同的旧效果图（去重）。
            foreach (var file in Directory.GetFiles(Dir, "*.json"))
            {
                if (Path.GetFullPath(file) == Path.GetFullPath(path))
                {
                    continue;
                }
                var existing = JsonUtility.FromJson<EffectGraphData>(File.ReadAllText(file));
                if (existing != null && ContentHasher.HashEffect(existing) == hash)
                {
                    File.Delete(file);
                }
            }

            File.WriteAllText(path, JsonUtility.ToJson(graph, true));
            return path;
        }

        /// <summary>读取所有已存效果图。目录不存在则返回空列表。</summary>
        public static List<EffectGraphData> LoadAll()
        {
            var result = new List<EffectGraphData>();
            if (!Directory.Exists(Dir))
            {
                return result;
            }

            foreach (var file in Directory.GetFiles(Dir, "*.json"))
            {
                var graph = JsonUtility.FromJson<EffectGraphData>(File.ReadAllText(file));
                if (graph != null)
                {
                    result.Add(graph);
                }
            }
            return result;
        }

        /// <summary>按名读取单个效果图，找不到返回 null。</summary>
        public static EffectGraphData Load(string graphName)
        {
            if (string.IsNullOrEmpty(graphName))
            {
                return null;
            }
            string path = Path.Combine(Dir, SanitizeFileName(graphName) + ".json");
            if (!File.Exists(path))
            {
                return null;
            }
            return JsonUtility.FromJson<EffectGraphData>(File.ReadAllText(path));
        }

        private static string SanitizeFileName(string raw)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                raw = raw.Replace(c, '_');
            }
            return raw;
        }
    }
}
