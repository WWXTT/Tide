using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace CardCore
{
    /// <summary>
    /// 卡表紧凑发射（2026-10-13 对齐 EffectSlim.EmitCompact 按值省略定案）：
    /// {"cards":[…],"deckConfig":{…}} 只写偏离默认的增量列——读侧 TideJson/JsonUtility
    /// 缺字段落字段初始值（ParseCost(null)=全0费 / ParseCardtype 默认生物 / ParseFlags 默认
    /// None / linkAuras 与 payload 均有 null 守卫），旧全量文件共存互读。
    /// 发射器居 CardCore=TideServer 共享源编译（TideServer.csproj Include CardCore\**），无头往返可测。
    ///
    /// 发射口径：身份三件（id/cardName/supertype）恒写；illustration 非空必写——立绘名即
    /// 3D 模型检索键（MonsterModelResolver 同源：illustration=模型目录名精确命中档），
    /// 压缩不得丢失。payload/linkAuras 与 EffectSlim.EmitAtom/EmitLinkAura 同源共用
    /// （可见性 public 化即为此），口径升级单点同改。
    /// </summary>
    public static class CardConfigCompact
    {
        /// <summary>瘦格式紧凑落盘文本（缩进 JSON）。deckConfig 仅在偏离默认（copiesPerCard≠3）时写。</summary>
        public static string EmitCompact(List<CardConfigEntry> cards, DeckConfig deckConfig = null)
        {
            var arr = new JArray();
            if (cards != null)
                foreach (var c in cards)
                {
                    var o = EmitEntry(c);
                    if (o != null) arr.Add(o);
                }
            var root = new JObject { ["cards"] = arr }; // cards 恒写（空表也写）——LoadCardsFromText 直接 foreach，null 会 NRE
            if (deckConfig != null && deckConfig.copiesPerCard != 3)
                root["deckConfig"] = new JObject { ["copiesPerCard"] = deckConfig.copiesPerCard };
            return root.ToString(Newtonsoft.Json.Formatting.Indented);
        }

        private static JObject EmitEntry(CardConfigEntry c)
        {
            if (c == null || string.IsNullOrEmpty(c.id)) return null;
            var o = new JObject
            {
                ["id"] = c.id,
                ["cardName"] = c.cardName ?? "",
                ["supertype"] = string.IsNullOrEmpty(c.supertype) ? nameof(Cardtype.Creature) : c.supertype,
            };
            // 内容指纹（2026-11-05 ID 冻结配套）：id 与内容解耦后，去重/漂移检测靠这列。
            // 旧档缺列 = 不写（读侧 null），下次保存由 ToEntry 回填。
            if (!string.IsNullOrEmpty(c.hash)) o["hash"] = c.hash;
            if (!string.IsNullOrEmpty(c.illustration)) o["illustration"] = c.illustration;
            if (c.power != 0) o["power"] = c.power;
            if (c.life != 0) o["life"] = c.life;
            if (HasCost(c.costList))
            {
                var cost = new JArray();
                foreach (var v in c.costList) cost.Add(v);
                o["costList"] = cost; // 全零费用省略——ParseCost(null)=空 ElementCost 同语义
            }
            if (c.keywords != null && c.keywords.Count > 0) o["keywords"] = new JArray(c.keywords);
            if (c.tags != null && c.tags.Count > 0) o["tags"] = new JArray(c.tags);
            if (c.effects != null && c.effects.Count > 0)
                o["effects"] = JArray.Parse(TideJson.ToJson(c.effects)); // 内嵌 PascalCase 全量（TestDecks 夹具 legacy 通道）
            if (!string.IsNullOrEmpty(c.arrows) && c.arrows != nameof(HexDirection.None)) o["arrows"] = c.arrows;
            if (c.effectIds != null && c.effectIds.Count > 0) o["effectIds"] = new JArray(c.effectIds);
            if (c.linkAuras != null && c.linkAuras.Count > 0)
            {
                var auras = new JArray();
                foreach (var a in c.linkAuras)
                {
                    var la = EffectSlim.EmitLinkAura(a);
                    if (la != null) auras.Add(la);
                }
                if (auras.Count > 0) o["linkAuras"] = auras;
            }
            // 系统动作资格位（2026-11-06 opt-in 改版）：正向列，只在声明时写（false 省略=不带）
            if (c.attackAbility) o["attackAbility"] = true;
            if (c.guardAbility) o["guardAbility"] = true;
            if (c.chargeAbility) o["chargeAbility"] = true;
            if (c.speedBonus != 0) o["speedBonus"] = c.speedBonus;
            if (c.durability != 0) o["durability"] = c.durability;
            if (c.isField) o["isField"] = true; // 场地标记（2026-10-29 P8）：false 省略——读侧缺字段回落默认
            //（卡级代价列 payloads/payload 发射已随 2026-11-08 代价效果级化整删——
            //  代价唯一形态=效果级 Costs，随 effectIds→Effects.json 通道走）
            return o;
        }

        private static bool HasCost(List<float> costList)
        {
            if (costList == null) return false;
            foreach (var v in costList)
                if (v != 0f) return true;
            return false;
        }
    }
}
