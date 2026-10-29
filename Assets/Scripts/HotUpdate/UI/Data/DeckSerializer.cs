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

        // 预组卡组目录（2026-10-22 三套主题卡组迁教学）：TutorialData/ 下随包只读，玩家不可改删。
        private static string PresetDeckDir => CardDataPaths.TutorialDir;

        /// <summary>保存卡组为 &lt;name&gt;.json。返回写入的完整路径。</summary>
        public static string Save(DeckData deck)
        {
            if (deck == null || string.IsNullOrEmpty(deck.name))
            {
                Debug.LogWarning("[DeckSerializer] 卡组为空或无名称，已跳过保存。");
                return null;
            }
            if (CardDataPaths.IsPresetDeckName(deck.name))
            {
                // 2026-10-22 预组只读：同名保存会遮蔽只读预组——保存侧直接拒绝（UI 另有提示）。
                Debug.LogWarning($"[DeckSerializer] 「{deck.name}」是只读预组卡组名，已拒绝保存。");
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
                var deck = NormalizeTokens(JsonUtility.FromJson<DeckData>(File.ReadAllText(file)));
                // 同目录混住 Cards.json/Effects.json（2026-09-21 数据分层定案）——
                // 它们解析出的 DeckData name 为空，跳过；只认真实卡组文件
                // 预组同名档不列（2026-10-22）：预组只读，旧安装残留的玩家侧同名档一律隐藏。
                if (deck != null && !string.IsNullOrEmpty(deck.name) && !CardDataPaths.IsPresetDeckName(deck.name))
                {
                    result.Add(deck);
                }
            }
            return result;
        }

        /// <summary>按卡组名读取单个卡组，找不到返回 null。预组名不走玩家目录（2026-10-22：
        /// 预组只读，读侧统一经 DeckCatalog 回落到教学目录）。</summary>
        public static DeckData Load(string deckName)
        {
            if (string.IsNullOrEmpty(deckName) || CardDataPaths.IsPresetDeckName(deckName))
            {
                return null;
            }
            string path = Path.Combine(DeckDir, SanitizeFileName(deckName) + ".json");
            if (!File.Exists(path))
            {
                return null;
            }
            return NormalizeTokens(JsonUtility.FromJson<DeckData>(File.ReadAllText(path)));
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

        // ======================================== 预组卡组（2026-10-22 迁教学，只读） ========================================

        /// <summary>写入预组卡组文件（TutorialData/&lt;name&gt;.json）。仅供编辑器构建器/版本内容使用——
        /// 运行时玩家路径绝不调用（卡组构筑保存走 Save→CardDir）。返回写入路径。</summary>
        public static string SaveTutorial(DeckData deck)
        {
            if (deck == null || string.IsNullOrEmpty(deck.name))
            {
                Debug.LogWarning("[DeckSerializer] 预组卡组为空或无名称，已跳过保存。");
                return null;
            }
            Directory.CreateDirectory(PresetDeckDir);
            string path = Path.Combine(PresetDeckDir, SanitizeFileName(deck.name) + ".json");
            File.WriteAllText(path, JsonUtility.ToJson(deck, true));
            return path;
        }

        /// <summary>读取预组卡组（TutorialData/&lt;name&gt;.json），缺文件返回 null。
        /// 不限于 PresetDeckNames——教学课卡组引用（basics/jinjie 等）也可读。</summary>
        public static DeckData LoadTutorial(string deckName)
        {
            if (string.IsNullOrEmpty(deckName)) return null;
            string path = Path.Combine(PresetDeckDir, SanitizeFileName(deckName) + ".json");
            if (!File.Exists(path)) return null;
            try { return NormalizeTokens(JsonUtility.FromJson<DeckData>(File.ReadAllText(path))); }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[DeckSerializer] 预组卡组读取失败：{deckName}：{ex.Message}");
                return null;
            }
        }

        /// <summary>旧档衍生物区规范化（P9b 2026-10-29）：P9b 前模板卡直接混在 cardIds 内
        /// （IsTokenTemplate 标记、不计张数）——读取时统一迁入 tokenCardIds（去重保序），
        /// 保存后即新档形态。内存态规范化不写回文件；卡表查不到的 id 不动（断链另报）。
        /// 超容量只告警不截断（只读预组兜底；玩家侧由保存口校验拦截）。</summary>
        private static DeckData NormalizeTokens(DeckData deck)
        {
            if (deck?.cardIds == null || deck.cardIds.Count == 0) return deck;
            List<string> moved = null;
            for (int i = deck.cardIds.Count - 1; i >= 0; i--)
            {
                var id = deck.cardIds[i];
                if (CardCatalog.GetById(id)?.IsTokenTemplate != true) continue;
                deck.cardIds.RemoveAt(i);
                (moved ??= new List<string>()).Insert(0, id); // 倒序前插=保持原相对序
            }
            if (moved == null) return deck;
            deck.tokenCardIds ??= new List<string>();
            foreach (var id in moved)
                if (!deck.tokenCardIds.Contains(id))
                    deck.tokenCardIds.Add(id);
            if (deck.tokenCardIds.Count > DeckTokenSupport.TokenZoneCapacity)
                Debug.LogWarning($"[DeckSerializer] 卡组「{deck.name}」衍生物区 {deck.tokenCardIds.Count} 个"
                                 + $"超上限 {DeckTokenSupport.TokenZoneCapacity}（旧档照读；玩家侧保存口会拦截）");
            return deck;
        }

        /// <summary>预组卡组文件是否存在。</summary>
        public static bool TutorialDeckExists(string deckName)
            => !string.IsNullOrEmpty(deckName)
               && File.Exists(Path.Combine(PresetDeckDir, SanitizeFileName(deckName) + ".json"));

        /// <summary>删除预组卡组文件（构建器重建前清旧档；运行时玩家路径不调用）。</summary>
        public static bool DeleteTutorial(string deckName)
        {
            if (string.IsNullOrEmpty(deckName)) return false;
            string path = Path.Combine(PresetDeckDir, SanitizeFileName(deckName) + ".json");
            if (!File.Exists(path)) return false;
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
