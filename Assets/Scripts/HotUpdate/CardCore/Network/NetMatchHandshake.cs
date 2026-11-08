using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CardCore.Attribute;
using SynergyUI;

namespace CardCore.Network
{
    /// <summary>
    /// 开局握手校验（2026-09-22 定案，同日收缩为"只核对卡组引用"口径）：
    /// 不比对整个 Cards.json/Effects.json——只核对**卡组引用闭包**。
    ///
    /// - 三层内容核对（2026-11-05 ID 冻结收口：原子行/效果/卡三层 ID 已冻结，**ID 命中 ≠ 内容一致**）：
    ///   卡/效果层经 ContentSig（CardConfigEntry.hash / EffectSlimDto.hash 指纹列）核对——
    ///   补上冻结后「两端同 ID 不同效果也能过握手打出内容分裂局」的缺口；原子表层行 ID 稳定
    ///（平衡层，改内容不改 ID），闭包引用到的行仍须核对内容——AtomicRowsHash。
    /// - 对手侧 / 未来扩展：**用到再核对**（对局中按实际出现的 ID/行核对），开局不做全量对比。
    ///
    /// 服务器侧 ValidateDeckSubmit（拒绝=Error 帧语义）；客户端侧 VerifyMatchManifest。
    /// M2 会话层驱动消息流；M1 提供纯逻辑，回环验证器覆盖（见 网络协议.md §11）。
    /// </summary>
    public static class NetMatchHandshake
    {
        // ======================================== 摘要计算 ========================================

        /// <summary>按卡组计算引用闭包的内容摘要（双方各用本地数据算同一卡组，比对即可）。
        /// 三段：AtomicRowsHash（闭包原子行内容，口径不变）/ ContentSig（卡+效果层指纹，
        /// 2026-11-05 ID 冻结收口）/ TokenSig（衍生物绑定，P9b）。
        /// 闭包 = cardIds+tokenCardIds → 卡表条目 → effectIds → 效果步内/代价 payload/奖励的全部原子 refId。</summary>
        public static NetDeckDigest ComputeDeckDigest(IReadOnlyList<string> cardIds)
            => ComputeDeckDigest(cardIds, null, null);

        /// <summary>同上并计入衍生物区/槽绑定（P9b v5）：TokenSig = 区列表 + 绑定三元组（排序后哈希）
        /// ——效果 ID 不含绑定，同卡表不同绑定的两套牌在此摘要上分叉（互不同步）。
        /// null 与空等价（归一化后参与哈希——两侧口径恒一致）。
        /// ContentSig（v8）：闭包卡（C:卡ID|ConfigHash）+ 引用效果（E:效果ID|hash）——排序后哈希。
        /// 指纹列旧档可为 null（空串参与，两侧同版本口径一致；跨版本由协议号闸拦截）。</summary>
        public static NetDeckDigest ComputeDeckDigest(IReadOnlyList<string> cardIds,
            IReadOnlyList<string> tokenCardIds, IReadOnlyList<DeckTokenBinding> tokenBindings)
        {
            var refIds = new HashSet<string>(StringComparer.Ordinal);
            var cardLines = new SortedSet<string>(StringComparer.Ordinal); // 卡层：ID 冻结后按指纹核对内容
            var effectIds = new HashSet<string>(StringComparer.Ordinal);
            void CollectCard(string id)
            {
                if (string.IsNullOrEmpty(id)) return;
                var card = CardCatalog.GetById(id);
                if (card == null) return; // 缺卡由 ValidateDeckSubmit 显性报告，此处只算摘要
                cardLines.Add($"C:{card.ID}|{card.ConfigHash ?? ""}");
                if (card.Effects != null)
                    foreach (var fx in card.Effects)
                    {
                        if (fx == null || string.IsNullOrEmpty(fx.Id)) continue;
                        if (fx.Id.StartsWith("EFF_", StringComparison.Ordinal)) continue; // 内嵌遗留口径，无库行/指纹
                        effectIds.Add(fx.Id);
                    }
                CollectCardRefIds(card, refIds);
            }
            if (cardIds != null)
                foreach (var id in cardIds)
                    CollectCard(id);
            if (tokenCardIds != null)
                foreach (var id in tokenCardIds)
                    CollectCard(id); // 衍生物模板卡同属闭包

            var sigs = refIds
                .Select(AtomicEffectTable.GetByHashId)
                .Where(cfg => cfg != null)
                .Select(RowSig)
                .OrderBy(s => s, StringComparer.Ordinal)
                .ToList();

            var contentLines = new List<string>(cardLines);
            contentLines.AddRange(effectIds
                .Select(eid => $"E:{eid}|{EffectsLibrary.Get(eid)?.hash ?? ""}")
                .OrderBy(s => s, StringComparer.Ordinal));
            contentLines.Sort(StringComparer.Ordinal);

            // 衍生物区 + 绑定签名（P9b）：区原序即卡组语义（展示序）——排序后哈希防同内容异序误判
            var tokenLines = new List<string>();
            foreach (var id in (tokenCardIds ?? new List<string>()).Where(t => !string.IsNullOrEmpty(t)))
                tokenLines.Add("T:" + id);
            foreach (var b in (tokenBindings ?? new List<DeckTokenBinding>())
                         .Where(b => b != null && !string.IsNullOrEmpty(b.cardId))
                         .Select(b => $"B:{b.cardId}|{b.effectIndex}|{b.templateId ?? ""}")
                         .OrderBy(s => s, StringComparer.Ordinal))
                tokenLines.Add(b);

            return new NetDeckDigest
            {
                AtomicRowsHash = MurmurHash3.Hash32(string.Join("\n", sigs)).ToString("X8"),
                AtomicRowCount = sigs.Count,
                TokenSig = MurmurHash3.Hash32(string.Join("\n", tokenLines)).ToString("X8"),
                ContentSig = MurmurHash3.Hash32(string.Join("\n", contentLines)).ToString("X8"),
            };
        }

        /// <summary>绑定条目本地形态（DeckTokenBinding）→ 线形态（MsgDeckTokenBinding）；null/空=空数组。</summary>
        public static MsgDeckTokenBinding[] ToWireBindings(IReadOnlyList<DeckTokenBinding> bindings)
            => (bindings ?? new List<DeckTokenBinding>())
                .Where(b => b != null && !string.IsNullOrEmpty(b.cardId))
                .Select(b => new MsgDeckTokenBinding { CardId = b.cardId, EffectIndex = b.effectIndex, TemplateId = b.templateId })
                .ToArray();

        /// <summary>线形态 → 本地形态；null=空表。</summary>
        public static List<DeckTokenBinding> FromWireBindings(MsgDeckTokenBinding[] bindings)
            => (bindings ?? Array.Empty<MsgDeckTokenBinding>())
                .Where(b => b != null && !string.IsNullOrEmpty(b.CardId))
                .Select(b => new DeckTokenBinding(b.CardId, b.EffectIndex, b.TemplateId))
                .ToList();

        /// <summary>原子行内容指纹（行 ID 稳定，内容是平衡层可变——必须哈希内容本身）。
        /// 覆盖费用构成/极性/目标域/可装载域/模板文案（客户端本地重渲染依赖文案，文案漂移也要拦）。
        /// 2026-10-04 费用位置数组化：ManaSig 按序数序（下标=枚举序号）无损拼串。
        /// v10（2026-11-08 目标选择重构）：参照域四列（RefDomain/RefPick/RefRequired/RefFilter）入指纹——
        /// 参照域/参照条件漂移即拒（分段原子的选择行为随这几列变；RefFilter=护盾猛击「具有护甲的」）。</summary>
        private static string RowSig(AtomicEffectConfig r)
        {
            return $"{r.HashId ?? ""}|{r.EnumName ?? ""}|{r.DisplayName ?? ""}|{r.Description ?? ""}"
                + $"|ML:{ManaSig(r.ManaList)}|TG:{r.Tags ?? ""}"
                + $"|DM:{r.Domain ?? ""}|TF:{r.TargetFilter ?? ""}"
                + $"|PO:{Float(r.Polarity)}|MK:{r.MountKinds ?? ""}|CM:{Float(r.CostMultiplier)}"
                + $"|RF:{r.RefDomain ?? ""}|RP:{r.RefPick ?? ""}|RR:{r.RefRequired}|RF2:{r.RefFilter ?? ""}";
        }

        private static string ManaSig(ElementCost cost)
        {
            return cost == null ? "-" : cost.ToString();
        }

        private static string Float(float v)
            => v.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>卡级 refId 收集（与闭环校验同一走法——步内 kind 0/1/2、代价 payload、引擎奖励）。
        /// 2026-11-08 代价效果级化：卡级行整删——代价原子全部随效果库条目 dto.costs 收集
        ///（payloads 列表 + legacy 单条 payload 双收）。</summary>
        private static void CollectCardRefIds(CardData card, HashSet<string> refIds)
        {
            if (card.Effects == null) return;
            foreach (var fx in card.Effects)
            {
                if (fx == null || string.IsNullOrEmpty(fx.Id)) continue;
                if (fx.Id.StartsWith("EFF_", StringComparison.Ordinal)) continue; // 内嵌遗留口径，不经效果库

                var dto = EffectsLibrary.Get(fx.Id);
                if (dto == null) continue;

                foreach (var cost in dto.costs ?? new List<CostRef>())
                {
                    if (cost == null) continue;
                    if (cost.payload != null && !string.IsNullOrEmpty(cost.payload.refId))
                        refIds.Add(cost.payload.refId);
                    foreach (var pa in cost.payloads ?? new List<CardCore.AtomicEffectEntry>())
                        if (!string.IsNullOrEmpty(pa?.refId)) refIds.Add(pa.refId);
                }

                CollectStepRefIds(dto.steps, refIds);
                // 2026-10-05 两槽定案：引擎奖励 rewards 头字段已退役——奖励原子随
                // StepRef.atom.branch.then 往返（收集在 CollectStepRefIds kind=0 分支内完成）
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
                    // 槽级分支载荷的 Then 奖励（两槽定案）——引用闭环同收集
                    if (sr.atom?.branch?.then != null)
                        foreach (var a in sr.atom.branch.then)
                            if (a != null && !string.IsNullOrEmpty(a.refId)) refIds.Add(a.refId);
                }
                else if (sr.kind == 1)
                {
                    foreach (var a in sr.then ?? new List<AtomicEffectEntry>())
                        if (a != null && !string.IsNullOrEmpty(a.refId)) refIds.Add(a.refId);
                    foreach (var a in sr.els ?? new List<AtomicEffectEntry>())
                        if (a != null && !string.IsNullOrEmpty(a.refId)) refIds.Add(a.refId);
                }
                else if (sr.kind == 2)
                {
                    foreach (var choice in sr.choices ?? new List<ChoiceRef>())
                        CollectStepRefIds(choice?.steps, refIds);
                }
            }
        }

        // ======================================== 服务器侧 ========================================

        /// <summary>校验客户端卡组提交：卡表全命中 + 唯一键卡每卡组限 1 张（同名卡可重复——
        /// 2026-10-11 构筑规则③）+ 效果/原子引用闭环 +
        /// 提交方摘要与服务器按同一卡组计算的摘要一致。失败返回 false，reason 为拒绝文案
        /// （Error 帧载荷）；成功 reason=null。</summary>
        public static bool ValidateDeckSubmit(MsgDeckSubmit submit, out string reason)
        {
            if (submit == null || submit.CardIds == null || submit.CardIds.Length == 0)
            {
                reason = "卡组提交为空（CardIds 缺失）";
                return false;
            }

            var problems = new List<string>();

            // 构筑限量键（规则②，2026-10-11；2026-11-02 容量门起不限「1 张」）：唯一门/仪典
            // 每卡组 1 张；容量门（命运一抽）每 10 张牌库卡 1 张——与 DeckBuilderScreen
            // 添加口/保存口同口径（ComposerCatalog.DeckUniqueSlots/SlotCapacity 单源）。
            // 旧「卡组不重复」铁律已随 2026-10-11 构筑规则③（30-60 张、同名可重复）废止——
            // 2026-10-26 修复：预组副本类卡组（同名多册合法、构筑界全绿可保存）曾在此被拒，
            // 表现为「保存通过但联机提交不可用」的口径断裂。
            var slotCounts = new Dictionary<string, string>(); // key → 显示名（超额才留）
            var slotTotals = new Dictionary<string, int>();
            foreach (var id in submit.CardIds)
            {
                var card = CardCatalog.GetById(id);
                if (card == null) continue; // 缺卡另报
                foreach (var slot in ComposerCatalog.DeckUniqueSlots(card))
                {
                    slotTotals[slot.Key] = slotTotals.TryGetValue(slot.Key, out var n) ? n + 1 : 1;
                    slotCounts[slot.Key] = slot.Display;
                }
            }
            var slotExamples = new List<string>();
            foreach (var kv in slotTotals)
            {
                var cap = ComposerCatalog.SlotCapacity(kv.Key, submit.CardIds.Length);
                if (kv.Value > cap && !slotExamples.Contains(slotCounts[kv.Key]))
                    slotExamples.Add(slotCounts[kv.Key]);
            }
            if (slotExamples.Count > 0)
                problems.Add($"卡组含超额限量卡 ×{slotExamples.Count}（如 {slotExamples[0]}——唯一门/仪典每卡组限 1 张，命运一抽每 10 张限 1 张）");

            // 卡表命中（服务器本地 CardCatalog 解析）
            var missingCards = submit.CardIds.Where(id => CardCatalog.GetById(id) == null).Distinct().ToList();
            if (missingCards.Count > 0)
                problems.Add($"卡表缺失 {missingCards.Count} 张（如 {missingCards[0]}——本端 Cards.json 无此 ID）");

            // 效果/原子引用闭环（引用化三层的缺行在装载期是静默告警，此处显性拦截）
            var missingRefs = new List<string>();
            foreach (var id in submit.CardIds.Distinct())
            {
                var card = CardCatalog.GetById(id);
                if (card == null) continue;
                CheckCardClosure(card, missingRefs);
            }
            if (missingRefs.Count > 0)
            {
                var sample = string.Join(", ", missingRefs.Distinct().Take(3));
                problems.Add($"效果/原子/引用卡缺失 ×{missingRefs.Count}（如 {sample}——Effects.json、原子表或引用卡（Cards.json）缺行）");
            }

            // 衍生物区/槽绑定（P9b v5）：与卡组保存口（DeckTokenSupport.ValidateDeck）同源——
            // 区容量/重复/模板类型 + 带槽效果须绑定、绑定值 ∈ 区且类型合规。空槽不再是引用闭环错误。
            var tokenCardIds = submit.TokenCardIds ?? Array.Empty<string>();
            var tokenBindings = FromWireBindings(submit.TokenBindings);
            problems.AddRange(SynergyUI.DeckTokenSupport.ValidateDeck(
                submit.CardIds.ToList(), tokenCardIds.ToList(), tokenBindings));

            // 内容摘要比对（仅卡组引用闭包内的行——不做全池对比）+ 绑定摘要（TokenSig，P9b v5）
            // + 卡/效果层指纹（ContentSig，v8——ID 冻结后同 ID 须再核对内容）
            if (submit.Digest == null)
            {
                problems.Add("客户端未随提交携带原子行摘要");
            }
            else
            {
                var serverDigest = ComputeDeckDigest(submit.CardIds, tokenCardIds, tokenBindings);
                if (submit.Digest.AtomicRowsHash != serverDigest.AtomicRowsHash)
                    problems.Add($"原子行摘要不一致（服务器 {serverDigest.AtomicRowCount} 行 / 提交方 {submit.Digest.AtomicRowCount} 行）——卡组引用的原子表内容漂移");
                if ((submit.Digest.TokenSig ?? "") != serverDigest.TokenSig)
                    problems.Add("衍生物绑定摘要不一致（TokenSig）——卡组衍生物区/槽绑定与提交摘要不匹配");
                if (string.IsNullOrEmpty(submit.Digest.ContentSig))
                    problems.Add("客户端未随提交携带内容指纹摘要（ContentSig）——旧版本客户端，请升级到相同版本");
                else if (submit.Digest.ContentSig != serverDigest.ContentSig)
                    problems.Add("卡/效果内容指纹不一致（ContentSig）——同 ID 不同内容（卡表/效果表内容漂移或两端版本不一致）");
            }

            if (problems.Count > 0)
            {
                reason = "卡组提交被拒：" + string.Join("；", problems);
                return false;
            }
            reason = null;
            return true;
        }

        /// <summary>卡级闭环：effectIds → Effects.json 命中；效果内全部原子 refId → 原子表命中。
        /// 旧内嵌路径效果（EFF_ 前缀回退 ID）不经效果库，跳过库命中检查。
        /// 2026-11-08 代价效果级化：卡级行整删——代价原子全部随 dto.costs 闭环（payloads+legacy 双收）。</summary>
        private static void CheckCardClosure(CardData card, List<string> missing)
        {
            if (card.Effects == null) return;
            foreach (var fx in card.Effects)
            {
                if (fx == null || string.IsNullOrEmpty(fx.Id)) continue;
                if (fx.Id.StartsWith("EFF_", StringComparison.Ordinal)) continue;

                var dto = EffectsLibrary.Get(fx.Id);
                if (dto == null)
                {
                    missing.Add($"效果 {fx.Id}");
                    continue;
                }

                foreach (var cost in dto.costs ?? new List<CostRef>())
                {
                    if (cost == null) continue;
                    if (cost.payload != null) CheckAtom(cost.payload, missing);
                    foreach (var pa in cost.payloads ?? new List<CardCore.AtomicEffectEntry>())
                        if (pa != null) CheckAtom(pa, missing);
                }

                WalkSteps(dto.steps, missing);
                // 2026-10-05 两槽定案：rewards 头字段已退役——Then 奖励随 atom.branch（WalkSteps kind=0 分支内检查）
            }
        }

        private static void WalkSteps(List<StepRef> steps, List<string> missing)
        {
            if (steps == null) return;
            foreach (var sr in steps)
            {
                if (sr == null) continue;
                if (sr.kind == 0)
                {
                    CheckAtom(sr.atom, missing);
                    // 槽级分支载荷的 Then 奖励（两槽定案）——闭环同检查
                    if (sr.atom?.branch?.then != null)
                        foreach (var a in sr.atom.branch.then) CheckAtom(a, missing);
                }
                else if (sr.kind == 1)
                {
                    foreach (var a in sr.then ?? new List<AtomicEffectEntry>()) CheckAtom(a, missing);
                    foreach (var a in sr.els ?? new List<AtomicEffectEntry>()) CheckAtom(a, missing);
                }
                else if (sr.kind == 2)
                {
                    foreach (var choice in sr.choices ?? new List<ChoiceRef>())
                        WalkSteps(choice?.steps, missing);
                }
            }
        }

        private static void CheckAtom(AtomicEffectEntry atom, List<string> missing)
        {
            if (atom == null || string.IsNullOrEmpty(atom.refId)) return;
            var row = AtomicEffectTable.GetByHashId(atom.refId);
            if (row == null)
            {
                missing.Add($"原子行 {atom.refId}");
                return;
            }

            // 引用卡ID 闭环（2026-10-13 定案）：召唤衍生物（须生物）/变形赋予（须生物或结界）的
            // atom.str 须能解析到服务器端真实卡且类型合规——与 CardLoader 构筑校验同口径。
            // P9b（2026-10-29）：空 str = 卡组期绑定槽——不再是闭环错误，绑定完整性走
            // DeckTokenSupport.ValidateDeck（ValidateDeckSubmit 上方同批校验）。
            if (!System.Enum.TryParse<AtomicEffectType>(row.EnumName, out var type)) return;
            if (type != AtomicEffectType.SummonToken && type != AtomicEffectType.GrantMorph) return;
            bool isMorphRef = type == AtomicEffectType.GrantMorph;
            if (string.IsNullOrEmpty(atom.str)) return;
            var referenced = CardCatalog.GetById(atom.str);
            if (referenced == null)
            {
                missing.Add($"引用卡 {atom.str}");
                return;
            }
            var super = referenced is IHasSupertype ht ? ht.Supertype : referenced.Supertype;
            bool typeOk = isMorphRef
                ? super == Cardtype.Creature || super == Cardtype.Enchantment
                : super == Cardtype.Creature;
            if (!typeOk)
                missing.Add($"引用卡 {atom.str} 类型不符（须{(isMorphRef ? "生物/结界" : "生物")}）");
        }

        // ======================================== 下发/客户端侧 ========================================

        /// <summary>构造对局清单下发（双座位提交均通过后，按各座位视角各发一份；
        /// 摘要由服务器按该座位卡组计算——含 P9b 衍生物区/绑定 TokenSig）。</summary>
        public static MsgMatchManifest BuildMatchManifest(int ownSeat, IReadOnlyList<string> ownCardIds,
            int opponentCardCount,
            IReadOnlyList<string> ownTokenCardIds = null, IReadOnlyList<DeckTokenBinding> ownTokenBindings = null)
        {
            return new MsgMatchManifest
            {
                OwnSeat = ownSeat,
                OwnCardIds = ownCardIds?.ToArray() ?? Array.Empty<string>(),
                OpponentCardCount = opponentCardCount,
                OwnDeckDigest = ComputeDeckDigest(ownCardIds, ownTokenCardIds, ownTokenBindings),
            };
        }

        /// <summary>客户端校验对局清单：己方卡组回显对账 + 服务器摘要与本地按同一卡组计算的摘要一致
        /// （双向握手；摘要含 P9b TokenSig——绑定不同即不一致）。失败即本地中止进局（reason 供 UI 展示）。</summary>
        public static bool VerifyMatchManifest(MsgMatchManifest manifest,
            IReadOnlyList<string> submittedCardIds, out string reason)
            => VerifyMatchManifest(manifest, submittedCardIds, null, null, out reason);

        /// <summary>同上并核对衍生物区/槽绑定（P9b v5）。</summary>
        public static bool VerifyMatchManifest(MsgMatchManifest manifest,
            IReadOnlyList<string> submittedCardIds,
            IReadOnlyList<string> submittedTokenCardIds, IReadOnlyList<DeckTokenBinding> submittedTokenBindings,
            out string reason)
        {
            if (manifest == null)
            {
                reason = "对局清单缺失";
                return false;
            }

            if (manifest.OwnSeat != 0 && manifest.OwnSeat != 1)
            {
                reason = $"座位号非法：{manifest.OwnSeat}";
                return false;
            }

            if (manifest.OwnCardIds == null
                || submittedCardIds == null
                || !manifest.OwnCardIds.SequenceEqual(submittedCardIds))
            {
                reason = "己方卡组回显与提交不符（服务器装载的卡组与提交不一致）";
                return false;
            }

            if (manifest.OpponentCardCount <= 0)
            {
                reason = $"对手卡组数量非法：{manifest.OpponentCardCount}";
                return false;
            }

            if (manifest.OwnDeckDigest == null)
            {
                reason = "服务器未携带原子行摘要";
                return false;
            }
            var localDigest = ComputeDeckDigest(submittedCardIds, submittedTokenCardIds, submittedTokenBindings);
            if (manifest.OwnDeckDigest.AtomicRowsHash != localDigest.AtomicRowsHash)
            {
                reason = $"服务器与本端对卡组引用的原子行摘要不一致（{manifest.OwnDeckDigest.AtomicRowCount} 行 vs {localDigest.AtomicRowCount} 行）";
                return false;
            }
            if ((manifest.OwnDeckDigest.TokenSig ?? "") != localDigest.TokenSig)
            {
                reason = "服务器与本端的衍生物绑定摘要不一致（TokenSig）——服务器装载的绑定与提交不符";
                return false;
            }
            if (string.IsNullOrEmpty(manifest.OwnDeckDigest.ContentSig))
            {
                reason = "服务器未携带内容指纹摘要（ContentSig）——版本不一致，请升级到相同版本";
                return false;
            }
            if (manifest.OwnDeckDigest.ContentSig != localDigest.ContentSig)
            {
                reason = "服务器与本端的卡/效果内容指纹不一致（ContentSig）——同 ID 不同内容（表内容漂移或版本不一致）";
                return false;
            }

            reason = null;
            return true;
        }
    }
}
