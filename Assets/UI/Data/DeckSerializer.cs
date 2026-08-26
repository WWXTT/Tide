using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>
    /// 卡组 JSON 读写 —— 落盘到 Assets/Configs/Decks/。
    /// 与现有 AtomicEffectTable 一致，使用 Application.dataPath + System.IO
    /// （非 Resources），保证存盘后能立即读回。
    ///
    /// 注意：写 Assets/ 仅在编辑器内有效；打包后该目录只读，届时应改用
    /// Application.persistentDataPath。Phase 1 为编辑器内验证。
    /// </summary>
    public static class DeckSerializer
    {
        // 相对 Application.dataPath 的卡组目录。
        private const string DeckDirRelative = "Configs/Decks";

        private static string DeckDir => Path.Combine(Application.dataPath, DeckDirRelative);

        /// <summary>保存卡组为 &lt;name&gt;.json。返回写入的完整路径。</summary>
        public static string Save(DeckData deck)
        {
            if (deck == null || string.IsNullOrEmpty(deck.name))
            {
                Debug.LogWarning("[DeckSerializer] 卡组为空或无名称，已跳过保存。");
                return null;
            }

            Directory.CreateDirectory(DeckDir);
            string path = Path.Combine(DeckDir, SanitizeFileName(deck.name) + ".json");
            File.WriteAllText(path, JsonUtility.ToJson(deck, true));
            return path;
        }

        /// <summary>读取所有已存卡组。目录不存在则返回空列表。</summary>
        public static List<DeckData> LoadAll()
        {
            var result = new List<DeckData>();
            if (!Directory.Exists(DeckDir))
            {
                return result;
            }

            foreach (var file in Directory.GetFiles(DeckDir, "*.json"))
            {
                var deck = JsonUtility.FromJson<DeckData>(File.ReadAllText(file));
                if (deck != null)
                {
                    result.Add(deck);
                }
            }
            return result;
        }

        /// <summary>按卡组名读取单个卡组，找不到返回 null。</summary>
        public static DeckData Load(string deckName)
        {
            if (string.IsNullOrEmpty(deckName))
            {
                return null;
            }
            string path = Path.Combine(DeckDir, SanitizeFileName(deckName) + ".json");
            if (!File.Exists(path))
            {
                return null;
            }
            return JsonUtility.FromJson<DeckData>(File.ReadAllText(path));
        }

        // 去除文件名非法字符，避免卡组名含 / : 等导致写盘失败。
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
