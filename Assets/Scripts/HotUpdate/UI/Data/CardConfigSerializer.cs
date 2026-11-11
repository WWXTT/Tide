using System.Collections.Generic;
using System.IO;
using CardCore;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>卡表目标（2026-10-22 三套主题卡组迁教学）：主表=玩家卡池 Card/Cards.json；
    /// 教学表=Tutorial/TutorialCards.json（只读随包内容，玩家侧卡池/合成器经 tags=教学 隔离）。</summary>
    public enum CardTable
    {
        Main,
        Tutorial,
    }

    /// <summary>
    /// 卡牌 JSON 写出/读回 —— 把合成界面产出的 CardData 写成 CardLoader 能解析的格式。
    /// 项目原本只有 CardLoader 读、无任何写出路径，本类补齐写出端。
    ///
    /// 输出严格匹配 CardLoader 的 schema：
    ///   外层 TestCardsConfigWrapper { cards:[CardConfigEntry], deckConfig:{copiesPerCard} }
    ///   每卡 CardConfigEntry（camelCase: id/cardName/supertype/power/life/costList/keywords/tags/effects）
    ///   costList = float 位置数组（2026-10-04：下标=ManaType 枚举序号，长度=枚举成员数）；
    ///   effects 项为 CardEffectData（PascalCase）。
    ///   写侧=CardConfigCompact.EmitCompact 紧凑发射（2026-10-13 按值省略定案）——只写偏离默认的
    ///   增量列，旧全量文件共存互读。
    ///
    /// 落盘到 StreamingAssets/Card/Cards.json（2026-09-14 统一定案+效果引用化；2026-09-21 定案：
    /// 卡属用户数据，与效果/卡组同住 Card/ 目录。编辑器读取（CardCatalog 卡池）与 UI 保存同一文件——
    /// 合成卡即入池；效果不内嵌——写 effectIds 引用，定义经 EffectLibrarySerializer upsert 进
    /// Card/Effects.json；TestDecks 原件留作训练桥/验证夹具）。
    /// </summary>
    public static class CardConfigSerializer
    {
        public const string DefaultFileRelative = "Card/Cards.json";

        /// <summary>卡表路径（2026-10-22 双表）：主表=Card/Cards.json，教学表=Tutorial/TutorialCards.json。</summary>
        private static string PathOf(CardTable table)
        {
            return table == CardTable.Tutorial
                ? CardDataPaths.TutorialFileIn("TutorialCards.json")
                : CardDataPaths.FileIn("Cards.json");
        }

        /// <summary>
        /// 把一张卡写入目标卡表（按 id 去重更新，不存在则追加）。返回写入的完整路径。
        /// **2026-11-05 ID 冻结后**：内容是可变数据、id 是稳定身份——编辑旧卡保存时若 card.ID 已带上，
        /// 一律原地覆盖同一行（不再「内容变则换号」）；replaceId 分支只剩兼容旧调用方，
        /// 传给它的旧 id 与新 id 相同时是无操作。
        /// </summary>
        public static string Save(CardData card, string fileRelative = null, string replaceId = null)
            => SaveTo(card, CardTable.Main, replaceId);

        /// <summary>写入教学卡表（2026-10-22 三套主题卡组迁教学；效果行同步写教学效果表）。</summary>
        public static string SaveTutorial(CardData card, string replaceId = null)
            => SaveTo(card, CardTable.Tutorial, replaceId);

        private static string SaveTo(CardData card, CardTable table, string replaceId)
        {
            if (card == null)
            {
                Debug.LogWarning("[CardConfigSerializer] 卡牌为空，已跳过保存。");
                return null;
            }

            string path = PathOf(table);
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            var wrapper = ReadWrapper(path) ?? new TestCardsConfigWrapper();
            if (wrapper.cards == null)
            {
                wrapper.cards = new List<CardConfigEntry>();
            }
            if (wrapper.deckConfig == null)
            {
                wrapper.deckConfig = new DeckConfig();
            }

            // 效果引用化：ToEntry 的 EffectIdsOf 逐效果 upsert（HashEffect 为 id）并取引用——
            // 效果 id → 卡 id 依赖链在 HashCard 内部同构推导（ContentHasher.HashEffectOf 单源投影）

            var entry = ToEntry(card, table);

            // 内容重复告警（2026-11-05 ID 冻结配套）：旧口径下「同内容 = 同 id」会自动合并成一张；
            // 冻结后 id 与内容解耦，同样的内容可能出现两条不同 id——这里只告警不合并：合并是设计决策，
            // 静默吞掉作者/玩家的第二次创建更糟。
            var clash = wrapper.cards.Find(c => c != null && c.id != entry.id
                                                && !string.IsNullOrEmpty(c.hash) && c.hash == entry.hash);
            if (clash != null)
                Debug.LogWarning($"[CardConfigSerializer] 内容重复：{entry.cardName}({entry.id}) 与 " +
                                 $"{clash.cardName}({clash.id}) 内容指纹相同（{entry.hash}）——冻结口径下不会自动合并");

            // 按 id 去重更新：同 id 覆盖，不重复追加。
            int existing = wrapper.cards.FindIndex(c => c != null && c.id == entry.id);
            if (existing >= 0)
            {
                wrapper.cards[existing] = entry;
            }
            else
            {
                wrapper.cards.Add(entry);
            }

            // 覆盖替换（2026-09-24 定案）：删旧存新——replaceId 与新 id 相同（内容未变）时为无操作
            if (!string.IsNullOrEmpty(replaceId) && replaceId != entry.id)
            {
                wrapper.cards.RemoveAll(c => c != null && c.id == replaceId);
            }

            // 紧凑发射（2026-10-13 对齐 EffectSlim.EmitCompact 按值省略定案）：只写偏离默认的增量列
            //（illustration 立绘/模型检索名非空必写）；读侧 JsonUtility/TideJson 缺字段回落默认，旧全量共存互读。
            File.WriteAllText(path, CardConfigCompact.EmitCompact(wrapper.cards, wrapper.deckConfig));
            return path;
        }

        /// <summary>按 id 从卡表删除条目（2026-10-16 强制删除定案）。返回是否删到。
        /// 卡组引用检查由调用方负责：强删被卡组引用的卡会使卡组断链——卡组界面按
        /// DeckCompositionValidator.Check().MissingIds 标记「不可用」。</summary>
        public static bool Remove(string cardId) => RemoveFrom(CardTable.Main, cardId);

        /// <summary>按 id 从教学卡表删除条目（2026-10-22 三套主题卡组迁教学——构建器重建前清旧档）。</summary>
        public static bool RemoveTutorial(string cardId) => RemoveFrom(CardTable.Tutorial, cardId);

        private static bool RemoveFrom(CardTable table, string cardId)
        {
            if (string.IsNullOrEmpty(cardId)) return false;
            string path = PathOf(table);
            var wrapper = ReadWrapper(path);
            if (wrapper?.cards == null) return false;
            int removed = wrapper.cards.RemoveAll(c => c != null && c.id == cardId);
            if (removed == 0) return false;
            if (wrapper.deckConfig == null) wrapper.deckConfig = new DeckConfig();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, CardConfigCompact.EmitCompact(wrapper.cards, wrapper.deckConfig));
            return true;
        }

        /// <summary>读回主卡表为 CardData 列表（复用 CardLoader 解析器验证 round-trip）。
        /// 注意：这里是**主表文件**，不含教学副表——运行时全池请走 CardCatalog.LoadAll。</summary>
        public static List<CardData> LoadAll(string fileRelative = null)
            => LoadAllFrom(CardTable.Main);

        /// <summary>读回教学卡表（不含主表）。</summary>
        public static List<CardData> LoadAllTutorial() => LoadAllFrom(CardTable.Tutorial);

        private static List<CardData> LoadAllFrom(CardTable table)
        {
            string path = PathOf(table);
            if (!File.Exists(path))
            {
                return new List<CardData>();
            }
            return CardLoader.LoadCardsFromText(File.ReadAllText(path));
        }

        /// <summary>CardData → 卡表条目（Cards.json 行形状；2026-11-09 协议 v11 自建内容跨端：
        /// DeckSubmit 载荷/对局记录存储用——与写盘同一映射源，序列化形状与主表逐字段一致）。
        /// 主表口径（效果 upsert 落玩家 Effects.json——对本地已存在的效果是幂等 no-op）。
        /// persistEffects=false（2026-11-11 联机握手修复）：**只读序列化**——不 upsert 效果表。
        /// 载荷构建属读路径，落盘副作用会让「提交前算的摘要」与「服务器校验时算的摘要」分叉
        ///（写表→mtime 变→TransientNetContent.Install 的强制 Reload 读到新 hash→ContentSig 假拒）。</summary>
        public static CardConfigEntry EntryOf(CardData card, bool persistEffects = true)
            => ToEntry(card, CardTable.Main, persistEffects);

        // 把 CardData 映射回 JSON 条目。
        private static CardConfigEntry ToEntry(CardData card, CardTable table = CardTable.Main,
            bool persistEffects = true)
        {
            // 关键词引用化（2026-09-24 定案）：本体关键词=effectIds 直接引用原子 refId（Grant 族反查），
            // keywords 列退役——仅承接反查失败的遗留 id（引擎专用词），装载端合并去重。
            // HashCard 读运行时 Keywords 列表（物化结果）——引用化前后卡 ID 稳定，卡组引用不断链。
            var effectIds = EffectLibrarySerializer.EffectIdsOf(
                card.Effects, table == CardTable.Tutorial, persistEffects);
            var unmappedKeywords = new List<string>();
            foreach (var kw in card.Keywords ?? new List<string>())
            {
                if (CardCore.Attribute.Handlers.GrantKeywordHandlerFactory.TryGetAtomRefId(kw, out var refId))
                {
                    if (!effectIds.Contains(refId)) effectIds.Add(refId);
                }
                else
                {
                    unmappedKeywords.Add(kw);
                    Debug.LogWarning($"[CardConfigSerializer] 关键词 {kw} 反查不到原子表行——留 legacy keywords 列");
                }
            }

            var entry = new CardConfigEntry
            {
                // ID 冻结（2026-11-05）：已有 ID 一律沿用（它是卡组存档/回放/训练标签的引用键）；
                // 只有新建卡（ID 为空）才按内容指纹分配。内容指纹另存 hash 列，供去重与漂移检测——
                // 改卡名/身材不再换号，玩家卡组不会被平衡补丁打断。
                id = string.IsNullOrEmpty(card.ID) ? "C_" + ContentHasher.HashCard(card) : card.ID,
                hash = ContentHasher.HashCard(card),
                cardName = card.CardName ?? "",
                supertype = card.Supertype.ToString(),
                illustration = card.Illustration ?? "",
                power = card.Power ?? 0,
                life = card.Life ?? 0,
                costList = new List<float>(), // 位置数组（2026-10-04：下标=ManaType 枚举序号），下方逐位回填
                keywords = unmappedKeywords,
                tags = card.Tags != null ? new List<string>(card.Tags) : new List<string>(),
                effects = null, // 效果引用化（2026-09-14）：不内嵌——经 effectIds 引用 Effects.json
                effectIds = effectIds,
                arrows = card.ArrowDirections.ToString(),
                linkAuras = card.LinkAuras != null && card.LinkAuras.Count > 0
                    ? new List<LinkAuraData>(card.LinkAuras)
                    : null, // 空表不写列（向后兼容旧 JSON）
                durability = card.Durability,
                isField = card.IsField, // 场地标记（2026-10-29 P8）：结界专属——发射端只写偏离默认（true 才落列）
                speedBonus = card.SpeedBonus, // 卡型速度：法术提速份额（2026-10-15 缺省 2=剩余全投提速；结界/生物恒 0）
                // 系统动作资格位（2026-11-06 opt-in 改版）：攻/守/充能正向拷贝——漏拷则 Save 后
                // 永远 false，资格/计价（充能 +1 灰）全错。
                attackAbility = card.AttackAbility,
                guardAbility = card.GuardAbility,
                chargeAbility = card.ChargeAbility,
                //（卡级代价列 payloads/payload/costRows 已随 2026-11-08 代价效果级化整删——
                //  代价唯一形态=效果级 Costs，随效果序列化；卡表不再落代价列。）
            };

            if (card.Cost != null)
            {
                // 费用位置数组（2026-10-04）：整组回填（含 0 位——定长数组是装载/校验的统一形态）
                for (int i = 0; i < card.Cost.v.Length; i++)
                    entry.costList.Add(card.Cost.v[i]);
            }
            return entry;
        }

        //（BuildCostRows 已随 2026-11-08 代价效果级化整删——卡级指向行不存在。）

        private static TestCardsConfigWrapper ReadWrapper(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }
            return JsonUtility.FromJson<TestCardsConfigWrapper>(File.ReadAllText(path));
        }
    }
}
