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
        /// <summary>主题 key → 卡组名（2026-10-24：三套主题构建器已删——预组禁脚本重建，
        /// 卡组名改字面量，与 TutorialData/*.json 文件名一致）。</summary>
        public static readonly (string key, string deckName)[] ThemeKeys =
        {
            ("red", "红·快攻"),
            ("green", "绿·慢速"),
            ("blue", "蓝·控制"),
        };

        /// <summary>Cards.json 全池（CardCatalog 静态缓存；编辑器内改表后 Invalidate 再取）。</summary>
        public static List<CardData> AllCards() => CardCatalog.LoadAll();

        /// <summary>训练池（2026-11-15 定案，训练池覆盖矩阵 §0 P0-C）：只抽 tag「训练池」的卡——
        /// 教学卡不参与训练（教学表并行重写中）。当前训练池 87 张 = TrainingCards 84 + 教学表双标签 3
        /// （2026-11-19 v18；P2-1..3 已入池）。
        /// P-1（2026-11-19）后无身份登记步骤（稳定下标纯派生，RegisterIdentities 已删）。</summary>
        public static List<CardData> TrainingPool()
            => AllCards().Where(c => c.Tags != null && c.Tags.Contains("训练池")).ToList();

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
                    Debug.LogError($"[BattleDeckSources] 主题卡组缺失或为空：{deckName}（先运行 Tools/验证 里的三套构建器）");
                result.Add((key, deck ?? new List<CardData>()));
            }
            return result;
        }

        /// <summary>按名加载卡组（卡 ID 引用 → CardCatalog 还原；缺卡记错误后跳过该卡）。
        /// 2026-10-22 三套主题卡组迁教学：DeckCatalog 合并口——预组名回落到 TutorialData 只读档。</summary>
        public static List<CardData> LoadDeckByName(string deckName)
        {
            var deck = DeckCatalog.Load(deckName);
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
                Debug.LogError($"[BattleDeckSources] 卡组 {deckName} 有 {missing.Count} 张卡在卡表找不到" +
                               $"（主表+教学表；先运行 Tools/验证 里的三套构建器）：{string.Join(",", missing.Take(3))}…");
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

        /// <summary>掺卡卡组（2026-11-17 阶段二口径，AI训练说明 §0.1）：主题整组按卡 Id 去重
        /// 每种 −1 张（红·快攻 30 → 25），缺额从训练池随机补足回原张数（+5 随机）。
        /// 保套路骨架、制造牌面差异；补卡与 SampleRandomDeck 同口径（池内各 id 至多 1 张）。</summary>
        public static List<CardData> BuildDopedDeck(List<CardData> themeDeck, List<CardData> pool, System.Random rng)
        {
            var kept = new List<CardData>();
            foreach (var group in themeDeck.GroupBy(c => c.ID))
            {
                var skip = true; // 每组第一张不保留 = 每种 −1
                foreach (var card in group)
                {
                    if (skip) { skip = false; continue; }
                    kept.Add(card);
                }
            }
            kept.AddRange(SampleRandomDeck(pool, themeDeck.Count - kept.Count, rng));
            return kept;
        }

        // RegisterIdentities 已删（P-1 2026-11-19 稳定下标）：身份下标纯派生，无 manifest 登记。
    }
}
