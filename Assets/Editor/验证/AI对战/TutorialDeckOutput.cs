using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using CardCore;
using SynergyUI;
using UnityEditor;
using UnityEngine;

namespace CardCore.Editor.Tests
{
    /// <summary>
    /// 教学表孤儿效果清理（2026-10-24 定案：三套主题卡组重建工具整删——
    /// **预组/教学卡组不得再由脚本随意重建**，卡组内容归 TutorialData 手工维护；
    /// 重建入口（红·快攻/蓝·控制/绿·慢速 builder 与「重建三套主题卡组」菜单）已退役）。
    /// 本类只保留孤儿效果行清理（主表+教学表），不动卡组与卡。
    /// </summary>
    public static class TutorialDeckOutput
    {
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

        /// <summary>教学表 TutorialEffects.json 删除无教学卡引用的行（返回删除条数）。</summary>
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
