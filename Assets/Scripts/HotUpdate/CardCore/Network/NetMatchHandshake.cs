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
    /// - 卡/效果层：ID 即内容哈希（同 ID ⟹ 同内容），提交卡组在服务器端全命中 +
    ///   效果/原子引用闭环即保证一致，无需任何内容摘要。
    /// - 原子表层：行 ID 稳定（平衡层，改内容不改 ID），闭包引用到的行**必须核对内容**——
    ///   NetDeckDigest = 闭包 refId 集对应表行内容（排序后哈希），双方按同一卡组各算一次比对。
    /// - 对手侧 / 未来扩展：**用到再核对**（对局中按实际出现的 ID/行核对），开局不做全量对比。
    ///
    /// 服务器侧 ValidateDeckSubmit（拒绝=Error 帧语义）；客户端侧 VerifyMatchManifest。
    /// M2 会话层驱动消息流；M1 提供纯逻辑，回环验证器覆盖（见 网络协议.md §11）。
    /// </summary>
    public static class NetMatchHandshake
    {
        // ======================================== 摘要计算 ========================================

        /// <summary>按卡组计算引用闭包的原子行摘要（双方各用本地数据算同一卡组，比对即可）。
        /// 闭包 = cardIds → 卡表条目 → effectIds → 效果步内/代价 payload/奖励的全部原子 refId。</summary>
        public static NetDeckDigest ComputeDeckDigest(IReadOnlyList<string> cardIds)
        {
            var refIds = new HashSet<string>(StringComparer.Ordinal);
            if (cardIds != null)
            {
                foreach (var id in cardIds)
                {
                    var card = string.IsNullOrEmpty(id) ? null : CardCatalog.GetById(id);
                    if (card == null) continue; // 缺卡由 ValidateDeckSubmit 显性报告，此处只算摘要
                    CollectCardRefIds(card, refIds);
                }
            }

            var sigs = refIds
                .Select(AtomicEffectTable.GetByHashId)
                .Where(cfg => cfg != null)
                .Select(RowSig)
                .OrderBy(s => s, StringComparer.Ordinal)
                .ToList();

            return new NetDeckDigest
            {
                AtomicRowsHash = MurmurHash3.Hash32(string.Join("\n", sigs)).ToString("X8"),
                AtomicRowCount = sigs.Count,
            };
        }

        /// <summary>原子行内容指纹（行 ID 稳定，内容是平衡层可变——必须哈希内容本身）。
        /// 覆盖费用构成/极性/目标域/可装载域/模板文案（客户端本地重渲染依赖文案，文案漂移也要拦）。</summary>
        private static string RowSig(AtomicEffectConfig r)
        {
            return $"{r.HashId ?? ""}|{r.EnumName ?? ""}|{r.DisplayName ?? ""}|{r.Description ?? ""}"
                + $"|ML:{ManaSig(r.ManaList)}|TG:{r.Tags ?? ""}"
                + $"|TK:{r.TargetKinds ?? ""}|TF:{r.TargetFilter ?? ""}"
                + $"|PO:{Float(r.Polarity)}|MK:{r.MountKinds ?? ""}|CM:{Float(r.CostMultiplier)}";
        }

        private static string ManaSig(List<ManaAmountEntry> list)
        {
            if (list == null || list.Count == 0) return "-";
            return string.Join(",", list.Select(m => $"{m.manaType}:{Float(m.amount)}"));
        }

        private static string Float(float v)
            => v.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>卡级 refId 收集（与闭环校验同一走法——步内 kind 0/1/2、代价 payload、引擎奖励）。</summary>
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
                    if (cost?.payload != null && !string.IsNullOrEmpty(cost.payload.refId))
                        refIds.Add(cost.payload.refId);

                CollectStepRefIds(dto.steps, refIds);

                foreach (var reward in dto.rewards ?? new List<AtomicEffectEntry>())
                    if (reward != null && !string.IsNullOrEmpty(reward.refId))
                        refIds.Add(reward.refId);
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

        /// <summary>校验客户端卡组提交：卡表全命中 + 卡组不重复 + 效果/原子引用闭环 +
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

            // 卡组不重复（README 铁律：卡组构成不重复，每张卡唯一）
            var dup = submit.CardIds.GroupBy(i => i).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (dup.Count > 0)
                problems.Add($"卡组含重复卡 ×{dup.Count}（如 {dup[0]}）");

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
                problems.Add($"效果/原子引用缺失 ×{missingRefs.Count}（如 {sample}——Effects.json 或原子表缺行）");
            }

            // 原子行摘要比对（仅卡组引用闭包内的行——不做全池对比）
            if (submit.Digest == null)
            {
                problems.Add("客户端未随提交携带原子行摘要");
            }
            else
            {
                var serverDigest = ComputeDeckDigest(submit.CardIds);
                if (submit.Digest.AtomicRowsHash != serverDigest.AtomicRowsHash)
                    problems.Add($"原子行摘要不一致（服务器 {serverDigest.AtomicRowCount} 行 / 提交方 {submit.Digest.AtomicRowCount} 行）——卡组引用的原子表内容漂移");
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
        /// 旧内嵌路径效果（EFF_ 前缀回退 ID）不经效果库，跳过库命中检查。</summary>
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
                    if (cost?.payload != null) CheckAtom(cost.payload, missing);

                WalkSteps(dto.steps, missing);

                foreach (var reward in dto.rewards ?? new List<AtomicEffectEntry>())
                    CheckAtom(reward, missing);
            }
        }

        private static void WalkSteps(List<StepRef> steps, List<string> missing)
        {
            if (steps == null) return;
            foreach (var sr in steps)
            {
                if (sr == null) continue;
                if (sr.kind == 0) CheckAtom(sr.atom, missing);
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
            if (AtomicEffectTable.GetByHashId(atom.refId) == null)
                missing.Add($"原子行 {atom.refId}");
        }

        // ======================================== 下发/客户端侧 ========================================

        /// <summary>构造对局清单下发（双座位提交均通过后，按各座位视角各发一份；
        /// 摘要由服务器按该座位卡组计算）。</summary>
        public static MsgMatchManifest BuildMatchManifest(int ownSeat, IReadOnlyList<string> ownCardIds,
            int opponentCardCount)
        {
            return new MsgMatchManifest
            {
                OwnSeat = ownSeat,
                OwnCardIds = ownCardIds?.ToArray() ?? Array.Empty<string>(),
                OpponentCardCount = opponentCardCount,
                OwnDeckDigest = ComputeDeckDigest(ownCardIds),
            };
        }

        /// <summary>客户端校验对局清单：己方卡组回显对账 + 服务器摘要与本地按同一卡组计算的摘要一致
        /// （双向握手）。失败即本地中止进局（reason 供 UI 展示）。</summary>
        public static bool VerifyMatchManifest(MsgMatchManifest manifest,
            IReadOnlyList<string> submittedCardIds, out string reason)
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
            var localDigest = ComputeDeckDigest(submittedCardIds);
            if (manifest.OwnDeckDigest.AtomicRowsHash != localDigest.AtomicRowsHash)
            {
                reason = $"服务器与本端对卡组引用的原子行摘要不一致（{manifest.OwnDeckDigest.AtomicRowCount} 行 vs {localDigest.AtomicRowCount} 行）";
                return false;
            }

            reason = null;
            return true;
        }
    }
}
