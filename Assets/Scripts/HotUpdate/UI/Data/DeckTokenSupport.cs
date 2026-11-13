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
    /// 衍生物槽（P9b 2026-10-29 起，2026-11-13 自动引用改版）：临摹/变形引用卡族的共享单源——
    /// 衍生物定义收口到卡组合界面（原子 str 直书），卡组构筑界面只按卡组内容**自动引用**
    /// （CollectReferencedTemplateIds），不再手动管理衍生物区。
    ///
    /// 职责三块（卡组数据 / 校验 / 装载盖章，UI 与联机握手共用）：
    /// · 槽枚举：卡效果里的 SummonToken / GrantMorph 原子（含分支奖励/抉择子序列下钻，
    ///   与引用闭环同走法）；效果序号 = CardData.Effects 下标（CardEffectConverter 保序保 Id，
    ///   运行时按 context.ResolvingEffect.Id 对位回同一序号）。
    /// · 自动引用：CollectReferencedTemplateIds 枚举卡组全部 str 引用模板（含代价载荷原子）
    ///   ——卡组构筑侧的衍生物区即本枚举 ∪ str 空槽的旧档绑定目标，随加删卡实时重算。
    /// · 卡组校验：区无重复、每项可解析；带槽效果（有空 str 槽，仅旧档）须有兜底绑定、
    ///   绑定值 ∈ 衍生物区、模板类型/两限制合规——不过不可保存（DeckBuilderScreen 保存口 +
    ///   DeckCompositionValidator 入口标记 + NetMatchHandshake 联机拦截三处同源）。
    /// · 装载盖章：把绑定写到卡实例（CardWrapper.TokenBindings，效果序号→模板 ID）——
    ///   本地（GameCore.InitGame→CardLoader.BuildDeck）与联机（NetRoom.StartMatch）同口。
    ///
    /// 解析顺序（SummonTokenHandler.ResolveBoundTemplateId 单源）：
    /// 实例绑定 → 回落原子 str（卡组合界面直书权威）→ 都无则 Error + 空转（不静默白板）。
    /// </summary>
    public static class DeckTokenSupport
    {

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
            /// <summary>槽所在的原子实例（合成期直选通道：卡面衍生物槽直写 str 用）。</summary>
            public AtomicEffectEntry Atom;
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
                Atom = atom,
            });
        }

        // ======================================== 自动引用（2026-11-13 改版单源） ========================================

        /// <summary>卡组全部引用卡模板 ID 枚举：遍历卡组每张卡的 SummonToken/GrantMorph
        /// 原子 str（EnumerateSlots 全量下钻——效果步骤/分支奖励/抉择子序列/扁平原子）+
        /// 代价载荷原子（PayloadCost 里的召唤模板同样要在入局装载闭包内），去重保序。
        /// 卡组构筑侧的衍生物区 = 本枚举 ∪ str 空槽的旧档绑定目标（随加删卡实时重算，
        /// 不再手动管理）；DeckBuilderScreen 外部引用行显示亦走本单源（剔除已在主卡组的）。</summary>
        public static List<string> CollectReferencedTemplateIds(IEnumerable<string> cardIds)
        {
            var seen = new HashSet<string>();
            var result = new List<string>();
            foreach (var id in cardIds ?? System.Linq.Enumerable.Empty<string>())
            {
                var card = CardCatalog.GetById(id);
                if (card == null) continue;
                foreach (var slot in EnumerateSlots(card))
                {
                    if (string.IsNullOrEmpty(slot.FixedTemplateId)) continue;
                    if (seen.Add(slot.FixedTemplateId)) result.Add(slot.FixedTemplateId);
                }
                if (card.Effects == null) continue;
                foreach (var fx in card.Effects)
                {
                    if (fx?.Costs == null) continue;
                    foreach (var ce in fx.Costs)
                        CollectRefEntries(ce != null ? ce.AllPayloads() : null, seen, result);
                }
            }
            return result;
        }

        /// <summary>代价载荷原子列的引用下钻（载荷原子不在 EnumerateSlots 槽口径内——
        /// 槽只认效果步，载荷里的召唤/变形模板同要进区装载）。</summary>
        private static void CollectRefEntries(List<AtomicEffectEntry> atoms, HashSet<string> seen,
            List<string> result)
        {
            if (atoms == null) return;
            foreach (var atom in atoms) CollectRefEntry(atom, seen, result);
        }

        /// <summary>单载荷原子引用判定：查原子表行（refId→EnumName），SummonToken/GrantMorph
        /// 才把 str 当卡 ID；分支奖励原子列一并下钻（同旧 DeckBuilderScreen.CollectRefAtom 口径）。</summary>
        private static void CollectRefEntry(AtomicEffectEntry atom, HashSet<string> seen, List<string> result)
        {
            if (atom == null) return;
            if (atom.branch?.then != null) CollectRefEntries(atom.branch.then, seen, result);
            if (string.IsNullOrEmpty(atom.str) || seen.Contains(atom.str)) return;
            var cfg = CardCore.Attribute.AtomicEffectTable.GetByHashId(atom.refId);
            if (cfg == null || string.IsNullOrEmpty(cfg.EnumName)) return;
            if (!Enum.TryParse<AtomicEffectType>(cfg.EnumName, out var type)) return;
            if (type != AtomicEffectType.SummonToken && type != AtomicEffectType.GrantMorph) return;
            seen.Add(atom.str);
            result.Add(atom.str);
        }

        // ======================================== 卡组校验（P9b §4 硬约束） ========================================

        /// <summary>衍生物模板两限制（2026-10-31 代价系统放行 0 费生物后收紧）：费用（支付价单源
        /// GetRuntimeCost 推导）须 ≥1，且不得携带代价（PayloadCost/legacy Costs——衍生物不经手牌
        /// 支付链，带代价与 0 费都会绕过代价折价平衡）。ValidateDeck 与区管理候选过滤共用。</summary>
        public static bool TemplateRestrictionsOk(CardData tpl)
            => tpl != null
               && CardCostService.GetRuntimeCost(tpl).Total >= 1f - 1e-4f
               && !CardCostService.HasPayloadCost(tpl);

        /// <summary>合成期衍生物模板候选过滤（2026-10-31 用户定案三条）：①费用 ≥1 且不带代价
        /// （TemplateRestrictionsOk 同源——与卡组期校验同口径）；②不嵌套——候选自身不得再含
        /// 引用卡槽原子（SummonToken/GrantMorph，衍生物套衍生物禁）；③卡型——召唤槽仅生物
        ///（creatureOnly）、变形槽生物/结界。另剔衍生物模板、教学卡与训练卡（同 CardRef 选卡口径）。
        /// 卡面衍生物槽选卡器与效果组合界面 CardRef 选卡器（召唤/变形原子）共用。</summary>
        public static bool TokenTemplateCandidateOk(CardData tpl, bool creatureOnly)
            => tpl != null
               && !string.IsNullOrEmpty(tpl.ID)
               && !tpl.IsTokenTemplate
               && !CardCatalog.IsTeachingCard(tpl)
               && !CardCatalog.IsTrainingCard(tpl) // 训练池（2026-11-03 分家）：不可见
               && (creatureOnly
                   ? tpl.Supertype == Cardtype.Creature
                   : tpl.Supertype == Cardtype.Creature || tpl.Supertype == Cardtype.Enchantment)
               && TemplateRestrictionsOk(tpl)
               && EnumerateSlots(tpl).Count == 0;

        /// <summary>整套卡组衍生物区/绑定校验（2026-11-13 自动引用改版）：返回人读错误串（空=通过）。
        /// · 区（=自动引用派生）无重复、每项可解析——类型/两限制改到引用粒度报（能点名引用卡）；
        /// · 模板两限制（2026-10-31）：费用 ≥1、不携带代价——绑定目标与固定 str 引用同口径；
        /// · str 空槽（仅旧档）须有兜底绑定、绑定值 ∈ 衍生物区、类型合规（召唤须生物；变形须生物/结界）；
        /// · 固定 str 引用补类型检查（召唤须生物；变形须生物/结界——结界变形模板随自动区合法化）。</summary>
        public static List<string> ValidateDeck(List<string> cardIds, List<string> tokenCardIds,
            List<DeckTokenBinding> tokenBindings)
        {
            var errors = new List<string>();
            var zone = tokenCardIds ?? new List<string>();

            // ---- 衍生物区（自动派生——只查重复与断链；类型/两限制在引用粒度报） ----
            var seenZone = new HashSet<string>();
            foreach (var id in zone)
            {
                if (string.IsNullOrEmpty(id)) continue;
                if (!seenZone.Add(id))
                {
                    errors.Add($"衍生物区重复模板 {NameOf(id)}——同一模板只须入区一次");
                    continue;
                }
                if (CardCatalog.GetById(id) == null)
                    errors.Add($"衍生物区模板 {id} 在卡表查不到（断链）——请移除后重新添加");
            }

            // ---- 绑定完整性（str 空槽的旧档兜底通道——新卡一律卡组合界面直写 str） ----
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
                                   + $"（{slot.EffectDisplayName ?? "?"}）的{(slot.IsMorph ? "变形" : "衍生物")}槽未设置"
                                   + "——请到卡组合界面编辑该卡，点「设置衍生物」选定模板");
                        continue;
                    }
                    if (!zone.Contains(templateId))
                    {
                        errors.Add($"「{slot.CardName}」效果{slot.EffectIndex + 1} 绑定的 {NameOf(templateId)}"
                                   + " 不在衍生物区——重新保存卡组即可自动补全引用");
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
                    else if (CardCostService.GetRuntimeCost(tpl).Total < 1f - 1e-4f)
                        errors.Add($"「{slot.CardName}」效果{slot.EffectIndex + 1} 绑定的 {NameOf(templateId)}"
                                   + " 费用为 0——模板费用须 ≥1（0 费生物不可作模板）");
                    else if (CardCostService.HasPayloadCost(tpl))
                        errors.Add($"「{slot.CardName}」效果{slot.EffectIndex + 1} 绑定的 {NameOf(templateId)}"
                                   + " 携带代价——模板不能携带代价（请改卡移除代价栏）");
                }

                // 固定引用模板（原子 str 直书——卡组合界面设置/旧数据/教学倒计时召唤）：
                // 断链不在此报（装载/验证器口径）；类型 + 两限制（2026-10-31）逐槽点名。
                foreach (var slot in EnumerateSlots(card).Where(s => !s.NeedsBinding))
                {
                    var fixedTpl = CardCatalog.GetById(slot.FixedTemplateId);
                    if (fixedTpl == null) continue;
                    var super = SupertypeOf(fixedTpl);
                    bool typeOk = slot.IsMorph
                        ? super == Cardtype.Creature || super == Cardtype.Enchantment
                        : super == Cardtype.Creature;
                    if (!typeOk)
                        errors.Add($"「{slot.CardName}」效果{slot.EffectIndex + 1} 引用的 {NameOf(slot.FixedTemplateId)}"
                                   + $" 类型不符（{(slot.IsMorph ? "变形模板须为生物/结界" : "衍生物模板须为生物")}）");
                    else if (CardCostService.GetRuntimeCost(fixedTpl).Total < 1f - 1e-4f)
                        errors.Add($"「{slot.CardName}」效果{slot.EffectIndex + 1} 引用的 {NameOf(slot.FixedTemplateId)}"
                                   + $" 费用为 0——{(slot.IsMorph ? "变形" : "衍生物")}模板费用须 ≥1");
                    else if (CardCostService.HasPayloadCost(fixedTpl))
                        errors.Add($"「{slot.CardName}」效果{slot.EffectIndex + 1} 引用的 {NameOf(slot.FixedTemplateId)}"
                                   + $" 携带代价——{(slot.IsMorph ? "变形" : "衍生物")}模板不能携带代价");
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
