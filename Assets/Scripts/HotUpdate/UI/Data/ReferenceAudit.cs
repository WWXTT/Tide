using System.Collections.Generic;
using System.Linq;
using CardCore;

namespace SynergyUI
{
    /// <summary>
    /// 删除被引用资源前的反查（2026-10-16 强制删除机制定案）：
    /// 删卡 → 列出引用它的卡组（强删后卡组断链，卡组界面按 DeckCompositionValidator 标记不可用）；
    /// 删效果 → 列出引用它的卡（卡装载时 EffectsLibrary.Resolve miss 即跳过该效果——卡池行标记⚠断链）。
    /// 卡→效果引用无落盘反查表（effectIds 是写出端投影）：运行时按快照 Id / HashEffectOf 重推导，
    /// 与 TutorialCreationFlow.DefaultCardEntriesSource 同口径。
    /// </summary>
    public static class ReferenceAudit
    {
        /// <summary>引用了指定卡的所有卡组名（按 DeckSerializer 存档实读）。</summary>
        public static List<string> DecksReferencingCard(string cardId)
        {
            if (string.IsNullOrEmpty(cardId)) return new List<string>();
            return DeckSerializer.LoadAll()
                .Where(d => d?.cardIds != null && d.cardIds.Contains(cardId))
                .Select(d => d.name)
                .Where(n => !string.IsNullOrEmpty(n))
                .ToList();
        }

        /// <summary>引用了指定效果的所有卡显示名（快照 Id 命中或按内容重哈希命中——
        /// 手搓未入库的效果快照 Id 为空，靠 HashEffectOf 兜底）。</summary>
        public static List<string> CardsReferencingEffect(string effectId)
        {
            var names = new List<string>();
            if (string.IsNullOrEmpty(effectId)) return names;
            foreach (var card in CardCatalog.LoadAll())
            {
                if (card?.Effects == null) continue;
                foreach (var fx in card.Effects)
                {
                    if (fx == null) continue;
                    if (fx.Id == effectId || ContentHasher.HashEffectOf(fx) == effectId)
                    {
                        names.Add(string.IsNullOrEmpty(card.CardName) ? card.ID : card.CardName);
                        break;
                    }
                }
            }
            return names;
        }

        /// <summary>效果库现存 id 集（卡池行⚠断链标记与孤儿检测共用）。</summary>
        public static HashSet<string> LibraryEffectIds() => new HashSet<string>(
            EffectLibrarySerializer.LoadAll()
                .Where(g => !string.IsNullOrEmpty(g.id))
                .Select(g => g.id));
    }
}
