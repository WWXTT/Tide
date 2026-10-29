using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;

namespace SynergyUI
{
    /// <summary>卡组期衍生物槽绑定条目（P9b 2026-10-29）：(卡ID, 效果序号) → 模板卡 ID。
    /// JsonUtility 可序列化（DeckData.tokenBindings 落盘形态）；协议侧转 MsgDeckTokenBinding。
    /// 绑定不进效果哈希、不进 CardData——同一临摹效果在不同卡组指向不同衍生物，效果 ID 不变。</summary>
    [Serializable]
    public class DeckTokenBinding
    {
        public string cardId;
        public int effectIndex;
        public string templateId;

        public DeckTokenBinding() { }

        public DeckTokenBinding(string cardId, int effectIndex, string templateId)
        {
            this.cardId = cardId;
            this.effectIndex = effectIndex;
            this.templateId = templateId;
        }
    }

    /// <summary>
    /// 衍生物槽（P9b 2026-10-29）：临摹/变形引用卡族改「卡组期绑定」的共享单源——
    /// 效果只留空槽（原子 str 空=未绑定），卡组衍生物区选模板，运行时按卡实例解析。
    ///
    /// 职责三块（卡组数据 / 校验 / 装载盖章，UI 与联机握手共用）：
    /// · 槽枚举：卡效果里的 SummonToken / GrantMorph 原子（含分支奖励/抉择子序列下钻，
    ///   与引用闭环同走法）；效果序号 = CardData.Effects 下标（CardEffectConverter 保序保 Id，
    ///   运行时按 context.ResolvingEffect.Id 对位回同一序号）。
    /// · 卡组校验：区容量 ≤6 / 无重复 / 模板为生物；带槽效果（有空 str 槽）须绑定、
    ///   绑定值 ∈ 衍生物区、模板类型合规——不过不可保存（DeckBuilderScreen 保存口 +
    ///   DeckCompositionValidator 入口标记 + NetMatchHandshake 联机拦截三处同源）。
    /// · 装载盖章：把绑定写到卡实例（CardWrapper.TokenBindings，效果序号→模板 ID）——
    ///   本地（GameCore.InitGame→CardLoader.BuildDeck）与联机（NetRoom.StartMatch）同口。
    ///
    /// 解析顺序（SummonTokenHandler.ResolveBoundTemplateId 单源）：
    /// 实例绑定 → 回落原子 str（旧数据/教程固定绑定）→ 都无则 Error + 空转（不静默白板）。
    /// </summary>
    public static class DeckTokenSupport
    {
        /// <summary>衍生物区容量上限（可配常量——P9b §1）。</summary>
        public const int TokenZoneCapacity = 6;

        /// <summary>一个引用卡槽原子（效果粒度聚合前的最小单元）。</summary>
        public sealed class TokenSlot
        {
            public string CardId;
            public string CardName;
            public int EffectIndex;         // CardData.Effects 下标（绑定键第二维）
            public string EffectId;
            public string EffectDisplayName;
            public bool IsMorph;            // GrantMorph（变形赋予）；false=SummonToken（召唤衍生物）
            /// <summary>原子 str 直书的固定模板 ID（旧数据/教程）；空 = 卡组期绑定槽。</summary>
            public string FixedTemplateId;
            public bool NeedsBinding => string.IsNullOrEmpty(FixedTemplateId);
        }

        /// <summary>枚举卡的全部引用卡槽原子（效果序内；分支奖励 then / 抉择子序列一并下钻——
        /// 与 NetMatchHandshake 闭环、DeckBuilderScreen 外部引用枚举同走法）。
        /// 代价载荷原子（PayloadCost）不属效果槽，不收。</summary>
        public static List<TokenSlot> EnumerateSlots(CardData card)
        {
            var result = new List<TokenSlot>();
            if (card?.Effects == null) return result;
            for (int i = 0; i < card.Effects.Count; i++)
            {
                var fx = card.Effects[i];
                if (fx == null) continue;
                if (fx.Steps != null)
                    foreach (var step in fx.Steps)
                        WalkStep(card, i, fx, step, result);
                WalkAtoms(card, i, fx, fx.AtomicEffects, result);
            }
            return result;
        }

        private static void WalkStep(CardData card, int index, CardEffectData fx, EffectStepData step,
            List<TokenSlot> result)
        {
            if (step == null) return;
            TrySlot(card, index, fx, step.atomic, result);
            WalkAtoms(card, index, fx, step.thenSteps, result);
            WalkAtoms(card, index, fx, step.elseSteps, result);
            if (step.choices == null) return;
            foreach (var choice in step.choices)
                foreach (var sub in choice?.steps ?? new List<EffectStepData>())
                {
                    TrySlot(card, index, fx, sub?.atomic, result);
                    WalkAtoms(card, index, fx, sub?.thenSteps, result);
                    WalkAtoms(card, index, fx, sub?.elseSteps, result);
                }
        }

        private static void WalkAtoms(CardData card, int index, CardEffectData fx,
            List<AtomicEffectEntry> atoms, List<TokenSlot> result)
        {
            if (atoms == null) return;
            foreach (var atom in atoms) TrySlot(card, index, fx, atom, result);
        }

        /// <summary>单原子槽判定：查原子表行（refId→EnumName），SummonToken / GrantMorph
        /// 才是引用卡槽；branch.then 奖励原子里的同类槽一并下钻（倒计时召唤模板在此）。</summary>
        private static void TrySlot(CardData card, int index, CardEffectData fx, AtomicEffectEntry atom,
            List<TokenSlot> result)
        {
            if (atom == null) return;
            if (atom.branch?.then != null)
                WalkAtoms(card, index, fx, atom.branch.then, result);
            var cfg = CardCore.Attribute.AtomicEffectTable.GetByHashId(atom.refId);
            if (cfg == null || string.IsNullOrEmpty(cfg.EnumName)) return;
            if (!Enum.TryParse<AtomicEffectType>(cfg.EnumName, out var type)) return;
            if (type != AtomicEffectType.SummonToken
                && type != AtomicEffectType.GrantMorph) return;
            result.Add(new TokenSlot
            {
                CardId = card.ID,
                CardName = string.IsNullOrEmpty(card.CardName) ? card.ID : card.CardName,
                EffectIndex = index,
                EffectId = fx?.Id,
                EffectDisplayName = fx != null && !string.IsNullOrEmpty(fx.DisplayName) ? fx.DisplayName : fx?.Id,
                IsMorph = type == AtomicEffectType.GrantMorph,
                FixedTemplateId = atom.str,
            });
        }

        /// <summary>带槽效果聚合（绑定 UI 用）：效果序 → 该效果下需要卡组绑定的槽数（有空 str 槽才入列）。</summary>
        public static List<(int effectIndex, string effectName, int slots)> SlottedEffects(CardData card)
        {
            var result = new List<(int, string, int)>();
            foreach (var g in EnumerateSlots(card).Where(s => s.NeedsBinding)
                         .GroupBy(s => s.EffectIndex).OrderBy(g => g.Key))
                result.Add((g.Key, g.First().EffectDisplayName ?? g.Key.ToString(), g.Count()));
            return result;
        }

        // ======================================== 卡组校验（P9b §4 硬约束） ========================================

        /// <summary>整套卡组衍生物区/绑定校验：返回人读错误串（空=通过）。
        /// · 区容量 ≤ TokenZoneCapacity、无重复、每项可解析且为 Cardtype.Creature；
        /// · 每个带槽效果（有空 str 槽）都有绑定，绑定值 ∈ 衍生物区且类型合规
        ///   （召唤须生物；变形须生物/结界——结界模板现阶段进不了区=实际仅生物，见汇报）。</summary>
        public static List<string> ValidateDeck(List<string> cardIds, List<string> tokenCardIds,
            List<DeckTokenBinding> tokenBindings)
        {
            var errors = new List<string>();
            var zone = tokenCardIds ?? new List<string>();

            // ---- 衍生物区 ----
            var seenZone = new HashSet<string>();
            foreach (var id in zone)
            {
                if (string.IsNullOrEmpty(id)) continue;
                if (!seenZone.Add(id))
                {
                    errors.Add($"衍生物区重复模板 {NameOf(id)}——同一模板只须入区一次");
                    continue;
                }
                var tpl = CardCatalog.GetById(id);
                if (tpl == null)
                    errors.Add($"衍生物区模板 {id} 在卡表查不到（断链）——请移除后重新添加");
                else if (SupertypeOf(tpl) != Cardtype.Creature)
                    errors.Add($"衍生物区模板 {NameOf(id)} 不是生物卡——衍生物区只收生物模板");
            }
            if (zone.Count(id => !string.IsNullOrEmpty(id)) > TokenZoneCapacity)
                errors.Add($"衍生物区超容量（{zone.Count} 个 > 上限 {TokenZoneCapacity}）——请先移除多余模板");

            // ---- 绑定完整性 ----
            var bySlot = new Dictionary<(string, int), string>();
            foreach (var b in tokenBindings ?? new List<DeckTokenBinding>())
            {
                if (b == null || string.IsNullOrEmpty(b.cardId)) continue;
                var key = (b.cardId, b.effectIndex);
                if (bySlot.TryGetValue(key, out var prev) && prev != b.templateId)
                    errors.Add($"「{NameOf(b.cardId)}」效果{b.effectIndex + 1} 绑定冲突（{prev} vs {b.templateId}）");
                bySlot[key] = b.templateId;
            }

            foreach (var id in (cardIds ?? new List<string>()).Distinct())
            {
                var card = CardCatalog.GetById(id);
                if (card == null) continue; // 断链另报（DeckCompositionValidator.MissingIds）
                foreach (var slot in EnumerateSlots(card).Where(s => s.NeedsBinding))
                {
                    if (!bySlot.TryGetValue((id, slot.EffectIndex), out var templateId)
                        || string.IsNullOrEmpty(templateId))
                    {
                        errors.Add($"「{slot.CardName}」效果{slot.EffectIndex + 1}"
                                   + $"（{slot.EffectDisplayName ?? "?"}）的{(slot.IsMorph ? "变形" : "衍生物")}槽未绑定"
                                   + "——点卡组内该卡行打开绑定面板选择模板");
                        continue;
                    }
                    if (!zone.Contains(templateId))
                    {
                        errors.Add($"「{slot.CardName}」效果{slot.EffectIndex + 1} 绑定的 {NameOf(templateId)}"
                                   + " 不在衍生物区——先把模板加入衍生物区");
                        continue;
                    }
                    var tpl = CardCatalog.GetById(templateId);
                    var super = tpl == null ? (Cardtype?)null : SupertypeOf(tpl);
                    bool typeOk = slot.IsMorph
                        ? super == Cardtype.Creature || super == Cardtype.Enchantment
                        : super == Cardtype.Creature;
                    if (!typeOk)
                        errors.Add($"「{slot.CardName}」效果{slot.EffectIndex + 1} 绑定的 {NameOf(templateId)}"
                                   + $" 类型不符（{(slot.IsMorph ? "变形模板须为生物/结界" : "衍生物模板须为生物")}）");
                }
            }
            return errors;
        }

        private static Cardtype SupertypeOf(CardData card)
            => card is IHasSupertype ht ? ht.Supertype : card.Supertype;

        private static string NameOf(string id)
        {
            var card = CardCatalog.GetById(id);
            return card != null && !string.IsNullOrEmpty(card.CardName) ? card.CardName : id;
        }

        // ======================================== 装载（绑定 → 卡实例） ========================================

        /// <summary>把卡组绑定盖到卡实例（P9b §3：同名副本共享同一绑定，逐实例都盖）。
        /// 盖章键 = 效果序号；卡实例另记绑定所属卡 ID（变形/复制后数据换源即失效回落 str——
        /// 见 CardWrapper.HasTokenBindings）。装载口：GameCore.InitGame（本地）与 NetRoom（联机）。</summary>
        public static void Apply(List<Card> deck, List<DeckTokenBinding> bindings)
        {
            if (deck == null || bindings == null || bindings.Count == 0) return;
            var byCard = bindings
                .Where(b => b != null && !string.IsNullOrEmpty(b.cardId) && !string.IsNullOrEmpty(b.templateId))
                .GroupBy(b => b.cardId)
                .ToDictionary(g => g.Key, g => g.ToDictionary(b => b.effectIndex, b => b.templateId));
            if (byCard.Count == 0) return;
            foreach (var card in deck)
            {
                if (!(card is CardWrapper wrapper)) continue;
                var data = wrapper.GetData();
                if (data == null || !byCard.TryGetValue(data.ID, out var map)) continue;
                wrapper.TokenBindingsCardId = data.ID;
                foreach (var kv in map)
                    wrapper.TokenBindings[kv.Key] = kv.Value;
            }
        }

        /// <summary>卡组 → 对战卡列表（cardIds ∪ tokenCardIds，缺失 id 记警告跳过）——
        /// 衍生物区模板随组入局（GameCore 按 IsTokenTemplate 分流落 Zone.Extra，2026-10-22 口径）。
        /// 本地装载（BattleOpeningDirector/挑战/随机预组）与联机服务器组卡共用。</summary>
        public static List<CardData> BuildCardList(DeckData deck)
            => BuildCardList(deck?.cardIds, deck?.tokenCardIds);

        /// <summary>同上（列表口径——服务器侧无 DeckData 类型）。</summary>
        public static List<CardData> BuildCardList(List<string> cardIds, List<string> tokenCardIds)
        {
            var result = new List<CardData>();
            foreach (var id in cardIds ?? new List<string>()) AddById(id, result);
            foreach (var id in tokenCardIds ?? new List<string>()) AddById(id, result);
            return result;
        }

        private static void AddById(string id, List<CardData> result)
        {
            if (string.IsNullOrEmpty(id)) return;
            var data = CardCatalog.GetById(id);
            if (data == null)
            {
                TideLog.Warn($"[DeckTokenSupport] 卡 id 不在卡池：{id}（已跳过——卡组引用有误）");
                return;
            }
            result.Add(data);
        }
    }
}
