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
        private const string CardsConfigRelative = "Configs/TestDecks/TestCreatureCards.json";

        private static List<CardData> _cache;
        private static Dictionary<string, CardData> _byId;
        private static bool _loading;

        /// <summary>加载（并缓存）全部卡牌。文件缺失返回空列表。
        /// 重入防护（2026-09-13 崩溃修复）：装载过程中 CardLoader 的费用巡检会经
        /// SummonToken 模板解析回调 GetById——此时 _byId 仍空，旧逻辑再触发 LoadAll
        /// 成无限递归（StackOverflow 硬崩编辑器）。装载中重入返回空表：模板查询得 null，
        /// 计价按既定 fallback 回落数量口径，装载完成后恢复正解。</summary>
        public static List<CardData> LoadAll()
        {
            if (_cache != null)
            {
                return _cache;
            }
            if (_loading)
            {
                return new List<CardData>();
            }

            _loading = true;
            try
            {
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
            finally
            {
                _loading = false;
            }
        }

        /// <summary>按卡牌 ID 取 CardData，找不到返回 null。</summary>
        public static CardData GetById(string id)
        {
            if (_byId == null)
            {
                LoadAll();
            }
            if (_byId == null || string.IsNullOrEmpty(id))
            {
                return null; // 重入装载未建索引（冷启动费用巡检回调）——查无此卡
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
