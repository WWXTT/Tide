using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.AI.NeuralEnv;
using SynergyUI;
using UnityEngine;

namespace CardCore.Editor.Tests
{
    /// <summary>
    /// AI 对战/训练桥共用取数助手（2026-09-21 主题卡组迁移）：卡池与卡组一律走正式数据层
    /// （README 数据分层定案）——Cards.json（CardCatalog）与 StreamingAssets/Card/ 卡组
    /// （DeckSerializer），不再读已删除的 Configs/TestDecks。
    /// 消费方：TideHeadlessServer（训练桥 reset）、OnnxBatchBattle（训练 eval 的编辑器镜像）、
    /// AiBattleDeckWindow（Neural 身份登记）、AiBattleE2E（标准池）。
    /// </summary>
    public static class BattleDeckSources
    {
        /// <summary>主题 key → 卡组名（与 ThemeDeckBuilder 三常量同序：red/green/blue）。</summary>
        public static readonly (string key, string deckName)[] ThemeKeys =
        {
            ("red", "红·快攻"),
            ("green", "绿·慢速"),
            ("blue", "蓝·控制"),
        };

        /// <summary>Cards.json 全池（CardCatalog 静态缓存；编辑器内改表后 Invalidate 再取）。</summary>
        public static List<CardData> AllCards() => CardCatalog.LoadAll();

        /// <summary>
        /// 三套主题卡组（整组 30 张，三层引用链正式路径：DeckSerializer.Load → CardCatalog.GetById
        /// 还原）。缺失卡组的槽位为空列表并 LogError（调用方决定兜底）；调用方缓存结果即可——
        /// batchmode 进程短命，编辑器 TCP 由 StartTcpServer 重启时置空重载。
        /// </summary>
        public static List<(string key, List<CardData> deck)> ThemeDecks()
        {
            var result = new List<(string, List<CardData>)>(ThemeKeys.Length);
            foreach (var (key, deckName) in ThemeKeys)
            {
                var deck = LoadDeckByName(deckName);
                if (deck == null || deck.Count == 0)
                    Debug.LogError($"[BattleDeckSources] 主题卡组缺失或为空：{deckName}（先运行 Tools/构建三色主题卡组）");
                result.Add((key, deck ?? new List<CardData>()));
            }
            return result;
        }

        /// <summary>按名加载卡组（卡 ID 引用 → CardCatalog 还原；缺卡记错误后跳过该卡）。</summary>
        public static List<CardData> LoadDeckByName(string deckName)
        {
            var deck = DeckSerializer.Load(deckName);
            if (deck == null || deck.cardIds == null || deck.cardIds.Count == 0) return null;
            var cards = new List<CardData>(deck.cardIds.Count);
            var missing = new List<string>();
            foreach (var id in deck.cardIds)
            {
                var card = CardCatalog.GetById(id);
                if (card == null) missing.Add(id);
                else cards.Add(card);
            }
            if (missing.Count > 0)
                Debug.LogError($"[BattleDeckSources] 卡组 {deckName} 有 {missing.Count} 张卡在 Cards.json 找不到" +
                               $"（先运行 Tools/构建三色主题卡组）：{string.Join(",", missing.Take(3))}…");
            return cards;
        }

        /// <summary>池洗牌（Fisher-Yates）后取前 size 张；池不足时整池上阵。</summary>
        public static List<CardData> SampleRandomDeck(List<CardData> pool, int size, System.Random rng)
        {
            var shuffled = new List<CardData>(pool);
            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
            }
            return shuffled.GetRange(0, Math.Min(size, shuffled.Count));
        }

        /// <summary>卡身份登记（manifest 追加式幂等，新哈希自动续排）：Cards.json 全池含三主题卡。</summary>
        public static int RegisterIdentities()
        {
            var pool = AllCards();
            return pool.Count > 0 ? TideCardIndex.Register(pool) : 0;
        }
    }
}
