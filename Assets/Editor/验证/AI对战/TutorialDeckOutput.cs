using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CardCore;
using SynergyUI;
using UnityEditor;
using UnityEngine;

namespace CardCore.Editor.Tests
{
    /// <summary>
    /// 三套主题卡组 → 教学表输出（2026-10-22 用户定案：三套卡组挪进教学、玩家不可动）：
    /// · 卡/效果写 Assets/TutorialData/TutorialCards.json / TutorialEffects.json —— 卡统一带
    ///   tags=教学（CardCatalog.LoadPlayPool 按 tag 隔离，玩家侧卡池/合成器/卡组构筑看不到也改不到；
    ///   内容哈希不含 tags → 卡 ID 不漂移，卡组引用保持）；
    /// · 卡组写 Assets/TutorialData/{卡组名}.json（DeckSerializer.SaveTutorial，只读随包内容）——
    ///   运行时经 DeckCatalog 合并口当「·预组」可选，DeckBuilderScreen 不列出、Save 拒同名；
    /// · 构建前双侧清旧主题卡（主表 + 教学表，幂等；主表迁移残留一并清）；
    /// · PruneOrphanMainEffects：卡迁走后主表 Effects.json 删除无卡引用的行（玩家自建卡引用则保留）。
    ///
    /// 只给编辑器构建器用；运行时玩家路径不调用。
    /// </summary>
    public static class TutorialDeckOutput
    {
        /// <summary>教学卡标签（CardCatalog.IsTeachingCard 单源判定）。</summary>
        public const string TeachingTag = "教学";

        // ======================================== 卡 ========================================

        /// <summary>给卡补「教学」标签（tags 不参与 ContentHasher.HashCard——卡 ID 不变）。</summary>
        public static void MarkTeaching(CardData card)
        {
            if (card == null) return;
            card.Tags ??= new List<string>();
            if (!card.Tags.Contains(TeachingTag)) card.Tags.Add(TeachingTag);
        }

        /// <summary>写一张卡进教学卡表（自动补教学标签；效果行同步落教学效果表）。</summary>
        public static void SaveCard(CardData card)
        {
            MarkTeaching(card);
            CardConfigSerializer.SaveTutorial(card);
        }

        /// <summary>按主题标签清旧卡（主表 + 教学表双侧）。</summary>
        public static void RemoveThemeCards(string themeTag)
        {
            foreach (var c in CardConfigSerializer.LoadAll()
                         .Where(c => c.Tags != null && c.Tags.Contains(themeTag)).ToList())
                CardConfigSerializer.Remove(c.ID);
            foreach (var c in CardConfigSerializer.LoadAllTutorial()
                         .Where(c => c.Tags != null && c.Tags.Contains(themeTag)).ToList())
                CardConfigSerializer.RemoveTutorial(c.ID);
        }

        /// <summary>按卡名清旧卡（主表 + 教学表双侧；蓝套「灵视」换装用）。</summary>
        public static void RemoveCardByName(string name)
        {
            foreach (var c in CardConfigSerializer.LoadAll()
                         .Where(c => c.CardName == name).ToList())
                CardConfigSerializer.Remove(c.ID);
            foreach (var c in CardConfigSerializer.LoadAllTutorial()
                         .Where(c => c.CardName == name).ToList())
                CardConfigSerializer.RemoveTutorial(c.ID);
        }

        // ======================================== 卡组 ========================================

        /// <summary>写预组卡组到 TutorialData（只读）。</summary>
        public static void SaveDeck(DeckData deck) => DeckSerializer.SaveTutorial(deck);

        /// <summary>删预组卡组文件（重建前清旧档）。</summary>
        public static void RemoveDeck(string name) => DeckSerializer.DeleteTutorial(name);

        /// <summary>测试开局夹具（TutorialData/测试卡组.json）跟随新卡池：只换 cardIds，不写技能位。
        /// 该文件是开发夹具（TestStartFlow「地牌充满」入口），不是三课教学卡组。</summary>
        public static void SyncTestDeck(DeckData deck)
        {
            if (deck == null) return;
            var test = new DeckData("测试卡组");
            test.cardIds.AddRange(deck.cardIds);
            File.WriteAllText(CardDataPaths.TutorialFileIn("测试卡组.json"),
                JsonUtility.ToJson(test, true));
        }

        // ======================================== 一键重建（三套） ========================================

        /// <summary>清理主表 + 教学表两侧的孤儿效果行（单套重建/换卡后收尾用）。</summary>
        [MenuItem("Tools/验证/清理孤儿效果（主表+教学表）", priority = 2)]
        public static void PruneOrphansMenu()
        {
            int main = PruneOrphanMainEffects();
            int tutorial = PruneOrphanTutorialEffects();
            CardCatalog.Invalidate();
            EffectsLibrary.Reload();
            AssetDatabase.Refresh();
            Debug.Log($"[教学迁移] 孤儿效果清理：主表 {main} 条 / 教学表 {tutorial} 条");
        }

        /// <summary>一键重建三套预组卡组并写入教学表（顺序：红→蓝→绿，最后清主表孤儿效果）。
        /// 2026-10-22 迁移入口；单独菜单保留各套独立重跑。</summary>
        [MenuItem("Tools/验证/重建三套主题卡组（写入教学表）", priority = 1)]
        public static void BuildAll()
        {
            RedClashDeckBuilder.Rebuild();
            BlueSurplusDeckBuilder.Build();
            GreenCountdownDeckBuilder.Build();
            int removedMain = PruneOrphanMainEffects();
            int removedTutorial = PruneOrphanTutorialEffects();
            CardCatalog.Invalidate();
            EffectsLibrary.Reload();
            AssetDatabase.Refresh();
            Debug.Log($"[教学迁移] 三套预组重建完成：主表孤儿效果删除 {removedMain} 条 / " +
                      $"教学表孤儿效果删除 {removedTutorial} 条；" +
                      $"教学卡表 {CardDataPaths.TutorialFileIn("TutorialCards.json")}");
        }

        // ======================================== 收尾 ========================================

        /// <summary>卡表原文里的 effectIds 集合（含关键词原子引用）。**必须从原文读**——
        /// 2026-10-22 实测：按 CardConfigSerializer.LoadAll 解析出的 card.Effects 统计引用面会漏，
        /// 导致孤儿清理误删教学效果行；原文 effectIds 才是卡组实际引用闭包。</summary>
        private static HashSet<string> RawEffectIdsOf(string cardsPath)
        {
            var used = new HashSet<string>();
            if (!File.Exists(cardsPath)) return used;
            string text = File.ReadAllText(cardsPath);
            foreach (Match m in Regex.Matches(text, "\"effectIds\"\\s*:\\s*\\[([^\\]]*)\\]"))
            {
                foreach (Match id in Regex.Matches(m.Groups[1].Value, "\"([^\"]+)\""))
                    used.Add(id.Groups[1].Value);
            }
            return used;
        }

        /// <summary>主表 Effects.json 删除无主表卡引用的行（返回删除条数）。教学效果在
        /// TutorialEffects.json，不受影响；玩家自建卡仍引用主表条目时保留。</summary>
        public static int PruneOrphanMainEffects()
        {
            var used = RawEffectIdsOf(CardDataPaths.FileIn("Cards.json"));

            int removed = 0;
            foreach (var graph in EffectLibrarySerializer.LoadAll())
            {
                if (graph == null || string.IsNullOrEmpty(graph.id)) continue;
                if (used.Contains(graph.id)) continue;
                EffectLibrarySerializer.DeleteById(graph.id);
                removed++;
            }
            return removed;
        }

        /// <summary>教学表 TutorialEffects.json 删除无教学卡引用的行（返回删除条数）——
        /// 单套重建后旧卡退役留下的效果行（如换卡前的效果）从这里清掉。</summary>
        public static int PruneOrphanTutorialEffects()
        {
            var used = RawEffectIdsOf(CardDataPaths.TutorialFileIn("TutorialCards.json"));

            int removed = 0;
            foreach (var graph in EffectLibrarySerializer.LoadAll(true))
            {
                if (graph == null || string.IsNullOrEmpty(graph.id)) continue;
                if (used.Contains(graph.id)) continue;
                EffectLibrarySerializer.DeleteById(graph.id, true);
                removed++;
            }
            return removed;
        }
    }
}
