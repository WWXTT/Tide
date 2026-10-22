using System;
using System.Collections.Generic;
using CardCore;

namespace SynergyUI
{
    /// <summary>
    /// 卡组总目录（2026-10-22 三套主题卡组迁教学）：玩家存档卡组（CardDataPipe/CardDir，可改可删）
    /// 与随包预组卡组（TutorialData，只读）合并成唯一检索口——战场选卡组、挑战、联机、创作回退
    /// 一律从这里取，不再直接 DeckSerializer.LoadAll。
    ///
    /// 只读口径：预组名 = CardDataPaths.PresetDeckNames（红·快攻/蓝·控制/绿·慢速）。DeckSerializer
    /// 已在读写两侧过滤同名玩家档（LoadAll 不列、Load 不读、Save 拒绝），所以这里不会出现同名双份。
    /// 卡组构筑界面（DeckBuilderScreen）仍只连 DeckSerializer——玩家看不到也改不到预组。
    /// </summary>
    public static class DeckCatalog
    {
        /// <summary>是否预组卡组名（只读）。</summary>
        public static bool IsPreset(string name) => CardDataPaths.IsPresetDeckName(name);

        /// <summary>是否只读卡组（按名判定；DeckData 本身不承载来源位，避免污染存档格式）。</summary>
        public static bool IsReadOnly(DeckData deck) => deck != null && IsPreset(deck.name);

        /// <summary>预组卡组列表（按 CardDataPaths.PresetDeckNames 顺序；缺文件的条目告警跳过，
        /// 不阻塞其余——包体/热更缺档时游戏仍可随机兜底）。</summary>
        public static List<DeckData> LoadPresets()
        {
            CardDataPaths.EnsureBootstrap(); // 真机首启：教学目录种子先落盘（编辑器空操作）
            var result = new List<DeckData>();
            foreach (var name in CardDataPaths.PresetDeckNames)
            {
                var deck = DeckSerializer.LoadTutorial(name);
                if (deck == null || deck.cardIds == null || deck.cardIds.Count == 0)
                {
                    TideLog.Warn($"[DeckCatalog] 预组卡组缺失或为空：{name}（检查 Assets/TutorialData/{name}.json）");
                    continue;
                }
                if (string.IsNullOrEmpty(deck.name)) deck.name = name; // 文件内 name 缺省时以文件名为准
                result.Add(deck);
            }
            return result;
        }

        /// <summary>全部可选卡组 = 玩家存档（文件序）+ 预组（固定顺序，追加在后）。
        /// 预组在后=默认列表末位，玩家自有卡组优先展示。</summary>
        public static List<DeckData> LoadAll()
        {
            var result = DeckSerializer.LoadAll();
            foreach (var preset in LoadPresets())
            {
                // 防御：DeckSerializer 已过滤同名玩家档，这里再挡一次（外部注入/旧缓存）
                if (result.Exists(d => d != null && d.name == preset.name)) continue;
                result.Add(preset);
            }
            return result;
        }

        /// <summary>按名取卡组：预组名→教学目录只读档；其余→玩家存档。找不到返回 null。</summary>
        public static DeckData Load(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (IsPreset(name))
            {
                var preset = DeckSerializer.LoadTutorial(name);
                if (preset != null && string.IsNullOrEmpty(preset.name)) preset.name = name;
                return preset;
            }
            return DeckSerializer.Load(name);
        }

        /// <summary>展示名（预组后缀「·预组」；仅 UI 用，不参与任何存档/比对）。</summary>
        public static string DisplayNameOf(DeckData deck)
        {
            if (deck == null) return "";
            return IsReadOnly(deck) ? deck.name + "·预组" : deck.name;
        }
    }
}
