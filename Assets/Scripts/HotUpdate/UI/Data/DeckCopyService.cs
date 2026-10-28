using System;
using System.Collections.Generic;
using System.Linq;

namespace SynergyUI
{
    /// <summary>
    /// 预组卡组复制服务（2026-10-22 预组只读配套）：把只读预组（TutorialData）复制成玩家自有副本。
    ///
    /// 引用复制口径（2026-10-27 卡零写入定案，承接 10-26 效果零写入）：
    /// - 复制只新建 DeckData 并**同 id 直引**预组的全部 cardIds 与 skillCardId——不克隆卡、
    ///   不写玩家主表 Cards.json、不写 Effects.json。卡与效果本体始终是教学表原件（主∪教学
    ///   合并视图按 id 解析；预组本身即此形态，联机提交同构预组直提）。
    /// - 旧深拷贝方案退役（10-22 逐卡克隆+「·副本」名分叉新 id+剥「教学」tag+逐张落主表）：
    ///   每次复制向主表倾倒数十字副本卡、删组不清卡致孤儿累积，效果也曾落分叉行。
    /// - 已知后果：预组卡日后改内容→内容哈希 id 变→副本组断链（标「不可用」），重新复制即可；
    ///   副本组内卡带「教学」tag 被 LoadPlayPool 过滤——构筑卡池已并入「玩家卡组已引用的卡」
    ///   补齐加回/重标技能（DeckBuilderScreen.PlayPoolWithDeckReferenced）。
    /// </summary>
    public static class DeckCopyService
    {
        /// <summary>副本卡组名后缀（仅卡组命名去重用；2026-10-27 起卡本体不再克隆改名）。</summary>
        public const string CopySuffix = "·副本";

        /// <summary>
        /// 复制预组为玩家副本（纯引用：新卡组档直引预组全部卡 id，卡/效果零写入）。
        /// 成功返回新卡组名（已落盘），失败返回 null 并给出 error。
        /// 卡组名=预组名+·副本（重名追加 2/3…序号）。
        /// </summary>
        public static string CopyPreset(string presetName, out string error)
        {
            error = null;
            if (!DeckCatalog.IsPreset(presetName))
            {
                error = $"「{presetName}」不是预组卡组";
                return null;
            }
            var deck = DeckCatalog.Load(presetName);
            if (deck == null || deck.cardIds == null || deck.cardIds.Count == 0)
            {
                error = $"预组「{presetName}」缺失或为空";
                return null;
            }

            // ---- 引用完整性：卡组引用的全部唯一卡（含技能卡）须在合并视图（主∪教学）可解析 ----
            var sourceIds = new List<string>(deck.cardIds);
            if (!string.IsNullOrEmpty(deck.skillCardId)) sourceIds.Add(deck.skillCardId);
            var missing = sourceIds.Distinct().Where(id => CardCatalog.GetById(id) == null).ToList();
            if (missing.Count > 0)
            {
                error = $"预组有 {missing.Count} 张卡在卡表查不到（如 {missing[0]}）——先重建主题卡组";
                return null;
            }

            // ---- 纯引用落盘：同 id 直引预组全部卡（保序），卡/效果零写入 → 无读侧缓存失效 ----
            string newName = UniqueDeckName(presetName + CopySuffix);
            var copy = new DeckData(newName)
            {
                skillCardId = deck.skillCardId,
            };
            copy.cardIds.AddRange(deck.cardIds);
            DeckSerializer.Save(copy);
            return newName;
        }

        /// <summary>卡组名去重：重名追加 2/3…序号（预组名/玩家档名都不撞）。</summary>
        private static string UniqueDeckName(string baseName)
        {
            if (!NameTaken(baseName)) return baseName;
            for (int i = 2; i < 100; i++)
            {
                var candidate = baseName + i;
                if (!NameTaken(candidate)) return candidate;
            }
            return baseName + System.DateTime.Now.Ticks % 1000; // 理论不可达兜底
        }

        private static bool NameTaken(string name)
            => DeckCatalog.IsPreset(name) || DeckSerializer.Load(name) != null;
    }
}
