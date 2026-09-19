using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace HexMap
{
    /// <summary>
    /// 世界存档 JSON 读写（DeckSerializer 模板）：StreamingAssets/Tide/HexMaps/。
    /// 与卡组同款注意：写 Assets/ 仅编辑器内有效，打包后该目录只读——
    /// 出包时把 HexMapDir 切到 Application.persistentDataPath 即一行改动。
    /// </summary>
    public static class HexWorldSerializer
    {
        private const string DirRelative = "Tide/HexMaps";

        private static string HexMapDir => Path.Combine(Application.streamingAssetsPath, DirRelative);

        /// <summary>保存世界为 &lt;name&gt;.json。返回完整路径；null=名字空/写失败。</summary>
        public static string Save(HexWorldSave world)
        {
            if (world == null || string.IsNullOrEmpty(world.name))
            {
                Debug.LogWarning("[HexWorld] 存档为空或无名称，已跳过保存");
                return null;
            }

            try
            {
                Directory.CreateDirectory(HexMapDir);
                string path = Path.Combine(HexMapDir, SanitizeFileName(world.name) + ".json");
                File.WriteAllText(path, JsonUtility.ToJson(world, true));
                return path;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[HexWorld] 写盘失败：{e.Message}");
                return null;
            }
        }

        /// <summary>按名字读取存档，找不到/解析失败返回 null。</summary>
        public static HexWorldSave Load(string saveName)
        {
            if (string.IsNullOrEmpty(saveName))
                return null;
            string path = Path.Combine(HexMapDir, SanitizeFileName(saveName) + ".json");
            if (!File.Exists(path))
                return null;
            try
            {
                return JsonUtility.FromJson<HexWorldSave>(File.ReadAllText(path));
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[HexWorld] 读档失败（{saveName}）：{e.Message}");
                return null;
            }
        }

        /// <summary>列出全部存档名（去扩展名），目录不存在返回空表——面板下拉用</summary>
        public static List<string> ListSaveNames()
        {
            var names = new List<string>();
            if (!Directory.Exists(HexMapDir))
                return names;
            foreach (var file in Directory.GetFiles(HexMapDir, "*.json"))
                names.Add(Path.GetFileNameWithoutExtension(file));
            return names;
        }

        public static bool Delete(string saveName)
        {
            string path = Path.Combine(HexMapDir, SanitizeFileName(saveName) + ".json");
            if (!File.Exists(path))
                return false;
            File.Delete(path);
            return true;
        }

        // 去除文件名非法字符（DeckSerializer 同款）
        private static string SanitizeFileName(string raw)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                raw = raw.Replace(c, '_');
            return raw;
        }
    }
}
