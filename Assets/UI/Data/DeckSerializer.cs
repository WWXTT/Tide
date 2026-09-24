using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>
    /// 卡组 JSON 读写 —— 落盘到 StreamingAssets/Card/（2026-09-14 统一定案；2026-09-21 定案：
    /// 卡组属用户数据，与卡/效果同目录。卡组只存卡 ID 引用数组（DeckData.cardIds）——
    /// 实体卡经 Cards.json（CardCatalog）按 ID 还原）。
    /// 与现有 AtomicEffectTable 一致，使用 Application.dataPath + System.IO
    /// （非 Resources），保证存盘后能立即读回。
    ///
    /// 注意：写 Assets/ 仅在编辑器内有效；打包后该目录只读，届时应改用
    /// Application.persistentDataPath。Phase 1 为编辑器内验证。
    /// </summary>
    public static class DeckSerializer
    {
        // 卡组目录（2026-09-24 路径收口）：经 CardDataPaths——编辑器 StreamingAssets / 玩家 persistentData
        private static string DeckDir => CardDataPaths.CardDir;

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
                // 同目录混住 Cards.json/Effects.json（2026-09-21 数据分层定案）——
                // 它们解析出的 DeckData name 为空，跳过；只认真实卡组文件
                if (deck != null && !string.IsNullOrEmpty(deck.name))
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

        /// <summary>删除卡组文件（按名）。返回是否存在且已删除。</summary>
        public static bool Delete(string deckName)
        {
            if (string.IsNullOrEmpty(deckName))
            {
                return false;
            }
            string path = Path.Combine(DeckDir, SanitizeFileName(deckName) + ".json");
            if (!File.Exists(path))
            {
                return false;
            }
            File.Delete(path);
            return true;
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
