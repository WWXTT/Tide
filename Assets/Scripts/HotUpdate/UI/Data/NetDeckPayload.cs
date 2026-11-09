using System;
using System.Collections.Generic;
using CardCore;
using CardCore.Network;

namespace SynergyUI
{
    /// <summary>
    /// 卡组闭包内容载荷构建（2026-11-09 协议 v11 自建内容跨端）：DeckSubmit 提交方把卡组闭包
    ///（主列表+衍生物区+技能标记卡）的**全部卡**及其效果、效果引用的词条定义序列化随提交携带——
    /// 接收端 add-if-missing 装进瞬态覆盖层（TransientNetContent），对端缺卡不再恒拒局。
    ///
    /// 全量携带而非「只带非基础池」的取舍：设备端玩家主表 Cards.json/Effects.json 是
    /// 「随包种子+玩家自建」混居单文件，无可靠的逐卡基础池判定（种子只在包内 TextReader）；
    /// 全量闭包 ≤ 数十卡（≈几十 KB，一次性握手开销）换正确性零判定成本。接收端本地已有同 ID
    /// 一律跳过（add-if-missing），两端都有的卡零影响；内容分裂由握手摘要照旧响亮拒绝。
    /// 词条（@前缀 refId）仅包内容可产生——按效果步闭包收集，本地解析不到（无包/未装）跳过。
    /// </summary>
    public static class NetDeckPayload
    {
        /// <summary>构建闭包载荷（卡/效果/词条三组；闭包内本地解析不到的 ID 静默跳过——缺卡由
        /// 既有握手校验显性报告）。</summary>
        public static void BuildForDeck(
            IReadOnlyList<string> cardIds, IReadOnlyList<string> tokenCardIds, string skillCardId,
            out MsgCardPayload[] cards, out MsgEffectPayload[] effects, out MsgAtomRowPayload[] atomRows)
        {
            var cardList = new List<MsgCardPayload>();
            var effectList = new List<MsgEffectPayload>();
            var atomList = new List<MsgAtomRowPayload>();
            var seenCards = new HashSet<string>(StringComparer.Ordinal);
            var seenEffects = new HashSet<string>(StringComparer.Ordinal);
            var seenAtoms = new HashSet<string>(StringComparer.Ordinal);

            CollectCard(skillCardId, cardList, effectList, atomList, seenCards, seenEffects, seenAtoms);
            if (cardIds != null)
                foreach (var id in cardIds)
                    CollectCard(id, cardList, effectList, atomList, seenCards, seenEffects, seenAtoms);
            if (tokenCardIds != null)
                foreach (var id in tokenCardIds)
                    CollectCard(id, cardList, effectList, atomList, seenCards, seenEffects, seenAtoms);

            cards = cardList.ToArray();
            effects = effectList.ToArray();
            atomRows = atomList.ToArray();
        }

        private static void CollectCard(
            string cardId,
            List<MsgCardPayload> cards, List<MsgEffectPayload> effects, List<MsgAtomRowPayload> atomRows,
            HashSet<string> seenCards, HashSet<string> seenEffects, HashSet<string> seenAtoms)
        {
            if (string.IsNullOrEmpty(cardId) || !seenCards.Add(cardId)) return;
            var card = CardCatalog.GetById(cardId);
            if (card == null) return;

            // 行 JSON 与 Cards.json 同构（EntryOf=写盘同源映射；效果 upsert 对本地已有行幂等）
            var entry = CardConfigSerializer.EntryOf(card);
            cards.Add(new MsgCardPayload { CardId = card.ID, CardJson = TideJson.ToJson(entry) });

            // 效果闭包：卡效果 id（materialized Effects——含冻结 id）逐个带库行
            if (card.Effects != null)
            {
                foreach (var fx in card.Effects)
                {
                    if (fx == null || string.IsNullOrEmpty(fx.Id)) continue;
                    if (fx.Id.StartsWith("EFF_", StringComparison.Ordinal)) continue; // 内嵌遗留口径，无库行
                    CollectEffect(fx.Id, effects, atomRows, seenEffects, seenAtoms);
                }
            }
        }

        private static void CollectEffect(
            string effectId,
            List<MsgEffectPayload> effects, List<MsgAtomRowPayload> atomRows,
            HashSet<string> seenEffects, HashSet<string> seenAtoms)
        {
            if (!seenEffects.Add(effectId)) return;
            var dto = EffectsLibrary.Get(effectId);
            if (dto == null) return; // 内置原子 refId（关键词行）不在效果库——接收端内置表自解
            effects.Add(new MsgEffectPayload { EffectId = dto.id, EffectJson = TideJson.ToJson(dto) });

            // 词条引用（@前缀 refId，内容包产物）：步内/代价载荷/分支奖励全走收集（与
            // NetMatchHandshake.CollectCardRefIds 同一走法）；本地解析不到=未装包，跳过
            var refIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var cost in dto.costs ?? new List<CostRef>())
            {
                if (cost == null) continue;
                if (cost.payload != null && !string.IsNullOrEmpty(cost.payload.refId)) refIds.Add(cost.payload.refId);
                foreach (var pa in cost.payloads ?? new List<AtomicEffectEntry>())
                    if (!string.IsNullOrEmpty(pa?.refId)) refIds.Add(pa.refId);
            }
            CollectStepRefIds(dto.steps, refIds);
            foreach (var refId in refIds)
            {
                if (!refId.StartsWith("@", StringComparison.Ordinal) || !seenAtoms.Add(refId)) continue;
                var def = CompositeAtomTable.Get(refId);
                if (def == null) continue;
                atomRows.Add(new MsgAtomRowPayload { RefId = def.id, RowJson = TideJson.ToJson(def) });
            }
        }

        private static void CollectStepRefIds(List<StepRef> steps, HashSet<string> refIds)
        {
            if (steps == null) return;
            foreach (var sr in steps)
            {
                if (sr == null) continue;
                if (sr.kind == 0)
                {
                    if (sr.atom != null && !string.IsNullOrEmpty(sr.atom.refId)) refIds.Add(sr.atom.refId);
                    if (sr.atom?.branch?.then != null)
                        foreach (var a in sr.atom.branch.then)
                            if (a != null && !string.IsNullOrEmpty(a.refId)) refIds.Add(a.refId);
                }
                else if (sr.kind == 1)
                {
                    foreach (var a in sr.then ?? new List<AtomicEffectEntry>())
                        if (!string.IsNullOrEmpty(a?.refId)) refIds.Add(a.refId);
                    foreach (var a in sr.els ?? new List<AtomicEffectEntry>())
                        if (!string.IsNullOrEmpty(a?.refId)) refIds.Add(a.refId);
                }
                else if (sr.kind == 2)
                {
                    foreach (var choice in sr.choices ?? new List<ChoiceRef>())
                        CollectStepRefIds(choice?.steps, refIds);
                }
            }
        }
    }
}
