using System.Collections.Generic;
using System.Linq;
using CardCore;

namespace SynergyUI
{
    /// <summary>效果合成目录里的一个关键词条目。</summary>
    public sealed class KeywordCatalogEntry
    {
        public string Id;            // 关键词 id（也是 StringValue）
        public string DisplayName;   // 中文名
        public string AtomicEffect;  // 被动授予的原子效果枚举名（GrantXxx）
        public UIColor Color;        // 颜色分类
    }

    /// <summary>
    /// 关键词目录 —— 把原子效果表的 Grant* 条目（经 CardLoader.LoadKeywords 合成）
    /// 暴露成"可作为原子追加到效果序列"的条目。关键词不单独开表。
    ///
    /// 关键词本身既是一个原子（atomicEffect=GrantXxx），也是一个完整效果，
    /// 作用对象默认是生物自己（Self）。效果合成界面把它和原子效果并列展示。
    /// </summary>
    public static class KeywordCatalog
    {
        public static List<KeywordCatalogEntry> LoadAll()
        {
            var keywords = CardLoader.LoadKeywords();
            var result = new List<KeywordCatalogEntry>();
            foreach (var kv in keywords)
            {
                var def = kv.Value;
                if (def == null || string.IsNullOrEmpty(def.id))
                {
                    continue;
                }
                // 面板资格双门（2026-10-07 仅可定案）：
                // ①关键词位——坚韧 2026-10-08 指示物化后连关键词目录都不入（GrantToughness 行
                //   未登记 Specs，LoadKeywords 直接跳过——指示物族面板另走原子库）；
                // ②表行 TargetFilter 含 "NoRole"（仅生物可赋予）
                // ——目标侧语义归目标过滤；以后增删可赋关键词只改原子表
                var row = CardCore.Attribute.AtomicEffectTable.GetByEnumName(def.atomicEffect);
                if (!ComposerCatalog.HasMountBit(row, MountKind.Keyword)) continue;
                if (!ComposerCatalog.IsCreatureGrantRow(row)) continue;
                result.Add(new KeywordCatalogEntry
                {
                    Id = def.id,
                    DisplayName = string.IsNullOrEmpty(def.nameZh) ? def.id : def.nameZh,
                    AtomicEffect = def.atomicEffect,
                    Color = ColorFilter.OfKeyword(def.id),
                });
            }
            return result.OrderBy(e => e.Color).ThenBy(e => e.DisplayName).ToList();
        }
    }
}
