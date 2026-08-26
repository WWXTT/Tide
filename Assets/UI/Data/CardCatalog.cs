using System.Collections.Generic;
using System.IO;
using CardCore;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>
    /// 卡牌总表 —— 从 Assets/Configs 下的卡牌 JSON 读取全部 CardData。
    /// 复用现有 CardLoader.LoadCardsFromText 解析器（无需改 CardLoader），
    /// 用 System.IO 直读文件，与 DeckSerializer 落盘位置一致。
    /// </summary>
    public static class CardCatalog
    {
        // 相对 Application.dataPath 的卡表文件。Phase 1 先用测试卡表。
        private const string CardsConfigRelative = "Configs/TestCreatureCards.json";

        private static List<CardData> _cache;
        private static Dictionary<string, CardData> _byId;

        /// <summary>加载（并缓存）全部卡牌。文件缺失返回空列表。</summary>
        public static List<CardData> LoadAll()
        {
            if (_cache != null)
            {
                return _cache;
            }

            string path = Path.Combine(Application.dataPath, CardsConfigRelative);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[CardCatalog] 卡表未找到: {path}");
                _cache = new List<CardData>();
                _byId = new Dictionary<string, CardData>();
                return _cache;
            }

            _cache = CardLoader.LoadCardsFromText(File.ReadAllText(path));
            _byId = new Dictionary<string, CardData>();
            foreach (var card in _cache)
            {
                if (!string.IsNullOrEmpty(card.ID))
                {
                    _byId[card.ID] = card;
                }
            }
            return _cache;
        }

        /// <summary>按卡牌 ID 取 CardData，找不到返回 null。</summary>
        public static CardData GetById(string id)
        {
            if (_byId == null)
            {
                LoadAll();
            }
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }
            return _byId.TryGetValue(id, out var card) ? card : null;
        }

        /// <summary>清空缓存，下次 LoadAll 重新读盘（编辑卡表后调用）。</summary>
        public static void Invalidate()
        {
            _cache = null;
            _byId = null;
        }
    }
}
