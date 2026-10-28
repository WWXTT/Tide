using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;

namespace SynergyUI
{
    /// <summary>
    /// 卡组构成检测：断链/形状/黑白代价覆盖（硬约束——不过不可保存）+ 地源（advisory 提示）。
    /// 卡组构筑界面（DeckBuilderScreen 统计区/保存口）与挑战/对战入口（Usable/UnusableTag）
    /// 共用本验证器——同一口径，改一处两边生效。
    ///
    /// 主题双色约束已删（2026-10-26 用户定案）：那是三套主题预组卡组构建期的临时检查，
    /// 不面向玩家——玩家自构筑多色卡组不受限（旧档载入/挑战/对战入口不再按卡组名首字判违色）。
    ///
    /// 费用口径：出牌费 card.Cost=恒推导（EnsureCost 刷新；法术施放账单=卡面费+锚价并入，
    /// 见 BuildCastSurfaces）；主动效果运行时锚价构筑期不占卡费。
    /// 断链（cardIds 在 Cards.json 无此 ID）单列——卡被强删后卡组引用悬空的历史坑。
    ///
    /// 黑白代价覆盖（2026-10-26 定案，硬约束）：CheckBWCostCoverage——需黑/白元素的卡
    /// 须至少带 1 张对应补偿色的代价卡（数量不限）。
    /// 地源检查（2026-10-26，advisory）：CheckLandSupport——施放费是否有足够可放地的卡支撑
    /// （红3 需 ≥3 张可产红的卡），只提示不拦保存（Ok/UnusableTag 不含）。
    /// </summary>
    public static class DeckCompositionValidator
    {
        public sealed class Report
        {
            public string DeckName;
            public List<string> MissingIds = new List<string>();
            /// <summary>类型×形状违规（2026-10-15 深夜改版：生物禁连接光环/结界禁主动/法术形状）。</summary>
            public List<string> ShapeErrors = new List<string>();
            /// <summary>含该色的册数（出牌费口径，与统计区色点行同语义）。</summary>
            public Dictionary<ManaType, int> CardsByColor = new Dictionary<ManaType, int>();
            /// <summary>该色费点合计（出牌费口径）。</summary>
            public Dictionary<ManaType, float> PipsByColor = new Dictionary<ManaType, float>();
            public int CardCount;
            public bool Ok => MissingIds.Count == 0 && ShapeErrors.Count == 0;
        }

        // 展示序=CostSquaresView.DisplayOrder 同款（红蓝绿灰；白/黑席位已删 2026-10-28 P2）
        private static readonly ManaType[] DisplayOrder =
        {
            ManaType.Red, ManaType.Blue, ManaType.Green, ManaType.Gray,
        };

        /// <summary>整套卡组检测（DeckData 口径）。</summary>
        public static Report Check(DeckData deck)
            => deck == null ? new Report() : Check(deck.name, deck.cardIds);

        /// <summary>整套卡组检测：去重逐卡解析，出牌费入统计；断链/形状单列（主题违色已随
        /// 主题约束删除——2026-10-26）。deckName 仅入报告（Describe 用），不再参与判定。</summary>
        public static Report Check(string deckName, IEnumerable<string> cardIds)
        {
            var report = new Report
            {
                DeckName = deckName ?? "",
            };
            if (cardIds == null) return report;

            // 同卡多册聚合（统计按册计）
            var copiesById = new Dictionary<string, int>();
            foreach (var id in cardIds)
            {
                if (string.IsNullOrEmpty(id)) continue;
                report.CardCount++;
                copiesById.TryGetValue(id, out var n);
                copiesById[id] = n + 1;
            }

            foreach (var kv in copiesById)
            {
                var card = CardCatalog.GetById(kv.Key);
                if (card == null)
                {
                    report.MissingIds.Add(kv.Key);
                    continue;
                }

                // 类型×形状校验（2026-10-15 深夜改版，与 CardLoader 装载校验同口径）——任何卡组都报
                foreach (var err in CheckShape(card))
                    report.ShapeErrors.Add(err);

                // 统计=出牌费构成（含该色册数/费点合计；灰也计入）
                if (card.Cost != null && !card.Cost.IsZero)
                {
                    foreach (var color in card.Cost.NonzeroColors())
                    {
                        float amount = card.Cost[color];
                        if (amount <= 0f) continue;
                        report.CardsByColor.TryGetValue(color, out int c);
                        report.CardsByColor[color] = c + kv.Value;
                        report.PipsByColor.TryGetValue(color, out float p);
                        report.PipsByColor[color] = p + amount * kv.Value;
                    }
                }
            }
            return report;
        }

        /// <summary>类型×形状校验（2026-10-15 深夜三类卡改版，与 CardLoader.ValidateSpellShape/
        /// ValidateEnchantShape/ValidateMountHosts 同口径——装载侧已拦，此处为卡组面兜底）：
        /// · 生物禁挂连接光环（光环专属结界）；
        /// · 结界禁一切主动（只挂自动/光环）；
        /// · 法术=1~3 个效果且全部为主动（Active/Instant）、禁内嵌抉择分支。
        /// 返回人读错误串（空=合规）。</summary>
        public static List<string> CheckShape(CardData card)
        {
            var errors = new List<string>();
            if (card == null) return errors;
            string name = string.IsNullOrEmpty(card.CardName) ? card.ID : card.CardName;

            if (card.Supertype == Cardtype.Creature
                && card.LinkAuras != null && card.LinkAuras.Count > 0)
                errors.Add($"{name}：生物不可挂连接光环（2026-10-15 改版——光环专属结界）");

            if (card.Supertype == Cardtype.Enchantment && card.Effects != null)
            {
                foreach (var e in card.Effects)
                {
                    int t = e?.TriggerTiming ?? -1;
                    if (t == (int)TriggerTiming.Activate_Active || t == (int)TriggerTiming.Activate_Instant
                        || t == (int)TriggerTiming.Activate_Response)
                        errors.Add($"{name}：结界效果「{e.DisplayName ?? e.Id}」为主动效果（结界只挂自动/光环）");
                }
            }

            if (card.Supertype == Cardtype.Spell)
            {
                int count = card.Effects?.Count ?? 0;
                if (count < 1 || count > 3)
                    errors.Add($"{name}：法术须挂 1~3 个主动效果（现 {count} 个）");
                if (card.Effects != null)
                {
                    foreach (var e in card.Effects)
                    {
                        int t = e?.TriggerTiming ?? -1;
                        if (t != (int)TriggerTiming.Activate_Active && t != (int)TriggerTiming.Activate_Instant)
                            errors.Add($"{name}：法术效果「{e.DisplayName ?? e.Id}」须为主动效果");
                        if (e?.Steps != null)
                            foreach (var s in e.Steps)
                                if (s?.choices != null && s.choices.Count > 1)
                                    errors.Add($"{name}：法术效果「{e.DisplayName ?? e.Id}」内嵌抉择分支（modeIndex 已被效果抉择占用）");
                    }
                }
            }
            return errors;
        }

        // ======================================== 黑白代价覆盖（2026-10-26 硬约束） ========================================

        /// <summary>黑白代价缺口（不过=不可保存）：需该色的卡组没有任何对应补偿色的代价卡。</summary>
        public sealed class BWGap
        {
            public ManaType Color;   // 黑/白
            public string CardName;  // 首例需求卡（人读示例）
        }

        /// <summary>黑白代价覆盖检查【已退役 2026-10-28 P1】：代价只执行、不产出元素
        ///（黑白获得经济删除）——「需黑/白元素的卡须带对应补偿色代价卡」的硬约束失去基础，
        /// 恒返回空表。调用面（DeckBuilderScreen 两处）零改动；文案/显示清理归 P4。</summary>
        public static List<BWGap> CheckBWCostCoverage(IEnumerable<string> cardIds)
        {
            return new List<BWGap>();
        }

        // PayloadAtomsOf（卡的代价原子枚举）已随 CheckBWCostCoverage 退役删除（2026-10-28 P1）。

        // ======================================== 地源检查（2026-10-26） ========================================

        /// <summary>地源不足项（advisory 提示：不进 Ok/UnusableTag、不拦保存——只提示「完全没有可能释放」的卡）。</summary>
        public sealed class LandShortage
        {
            public string CardName;
            public string CardId;
            public int Copies;
            public string Surface; // 施放面（「出牌费 {费用串}」）
            public string Detail;  // 人读明细（缺哪种源/差多少）
        }

        /// <summary>单个不满足的地源条件（null=可付）。</summary>
        private sealed class LandFail
        {
            public string Detail;
        }

        /// <summary>一张卡的一个施放账单面（抉择/多效果卡=多面，任一面可付即整卡可用）。</summary>
        private sealed class CastSurface
        {
            public ElementCost Bill;
        }

        /// <summary>地源检查（2026-10-26）：卡组内是否有足够「可放地」的卡支撑每张卡的**施放**账单
        /// （如加一张红3，至少还需 3 张可产红的卡放地才能释放）。advisory——不拦保存。
        /// · 账单口径=GetCardCost 静态镜像：法术=卡面费+所选效果锚价（逐主动效果一面，次数坍缩×1，
        ///   SpellCastAnchor 同参）；抉择永久物=逐模式卡面费；其余=单面卡面费。费用指示物/苏醒豁免
        ///   属运行态不计；永久物主动效果的发动费不属「释放」门槛，不查。
        /// · 支付口径=地牌静态最优（支付链同源）：红/蓝/绿仅同色地牌可付、灰任意地牌可付；黑白/任意
        ///   不由地牌产出（获取=代价补偿/元素合成等结算路径，卡组面无法判定）——对应需求不判。
        /// · 供给=除手中待释放的一册外全部册数可放地（放地不限次；同卡多册可留场作地）；
        ///   锁色候选=ElementPoolSystem.GetLandColorChoices 非空。地牌槽自然上限 10 可被「成长」
        ///   无限抬高，故不设上限闸（只查卡组源的色量）。
        /// · Hall 子集校验：多色账单与多候选源卡（一卡只能锁一色）须全子集满足，防「红池蓝池各自
        ///   够、合起来不够」的漏报。
        /// 整卡全部施放面均不可付才入列（抉择/多效果卡有任一面可付=可用）。</summary>
        public static List<LandShortage> CheckLandSupport(IEnumerable<string> cardIds)
        {
            var result = new List<LandShortage>();
            if (cardIds == null) return result;

            // 同卡多册聚合（统计按册计；判定=整卡全部施放面不可付）
            var copiesById = new Dictionary<string, int>();
            foreach (var id in cardIds)
            {
                if (string.IsNullOrEmpty(id)) continue;
                copiesById.TryGetValue(id, out var n);
                copiesById[id] = n + 1;
            }

            // 源池：锁色候选非空的册才可放地。subsetPool[mask]=候选∩mask≠∅ 的册数（mask 位 0/1/2=红/蓝/绿）
            var landColors = new[] { ManaType.Red, ManaType.Blue, ManaType.Green };
            var subsetPool = new int[8];
            int landCapableTotal = 0;
            var candCache = new Dictionary<string, List<ManaType>>();
            foreach (var kv in copiesById)
            {
                var card = CardCatalog.GetById(kv.Key);
                var cands = card != null ? ElementPoolSystem.GetLandColorChoices(card) : new List<ManaType>();
                candCache[kv.Key] = cands;
                if (cands.Count == 0) continue;
                landCapableTotal += kv.Value;
                int mask = 0;
                for (int i = 0; i < landColors.Length; i++)
                    if (cands.Contains(landColors[i])) mask |= 1 << i;
                for (int sub = 1; sub < 8; sub++)
                    if ((sub & mask) != 0) subsetPool[sub] += kv.Value;
            }

            foreach (var kv in copiesById)
            {
                var card = CardCatalog.GetById(kv.Key);
                if (card == null || card.IsTokenTemplate) continue; // 断链另报；衍生物模板落额外区不施放
                var surfaces = BuildCastSurfaces(card);
                if (surfaces.Count == 0) continue;

                LandShortage worst = null; // 全部面不可付才报——记录首面明细
                foreach (var surface in surfaces)
                {
                    var fail = FirstLandFail(surface.Bill, candCache[kv.Key], subsetPool, landCapableTotal);
                    if (fail == null) { worst = null; break; } // 任一面可付=整卡可用
                    if (worst == null)
                        worst = new LandShortage
                        {
                            CardName = string.IsNullOrEmpty(card.CardName) ? card.ID : card.CardName,
                            CardId = card.ID,
                            Surface = "出牌费 " + FormatCost(surface.Bill),
                            Detail = fail.Detail,
                        };
                }
                if (worst != null)
                {
                    worst.Copies = kv.Value;
                    result.Add(worst);
                }
            }
            return result;
        }

        /// <summary>施放账单面枚举（GetCardCost 镜像）：法术=卡面费+逐主动效果锚价（施放时择一效果，
        /// 每效果一面；modeIndex 传效果序同 GetCardCost 实参）；抉择永久物=逐模式费；其余=单面卡面费。</summary>
        private static List<CastSurface> BuildCastSurfaces(CardData card)
        {
            var list = new List<CastSurface>();
            if (card.Supertype == Cardtype.Spell)
            {
                var actives = new List<EffectDefinition>();
                foreach (var def in CardEffectConverter.ConvertAll(card.Effects, card.ID))
                    if (def != null && def.IsActivatedEffect) actives.Add(def);
                if (actives.Count == 0) // 无主动效果（形状违规另报）——退单面卡面费
                {
                    list.Add(new CastSurface { Bill = CardCostService.GetRuntimeCost(card, 0) });
                    return list;
                }
                for (int e = 0; e < actives.Count; e++)
                {
                    actives[e].TriggerLimitPerTurn = 1; // 次数坍缩——SpellCastAnchor 同口径
                    var bill = CardCostService.GetRuntimeCost(card, e);
                    var anchor = CostDerivationService.DeriveElementCosts(actives[e], 0);
                    if (anchor != null)
                        foreach (var color in anchor.NonzeroColors())
                            bill[color] = bill[color] + anchor[color];
                    list.Add(new CastSurface { Bill = bill });
                }
                return list;
            }
            if (CostDerivationService.HasChoiceEffect(card))
            {
                int modeCount = CostDerivationService.GetModeCount(card);
                for (int m = 0; m < modeCount; m++)
                    list.Add(new CastSurface { Bill = CardCostService.GetModeCost(card, m) });
                return list;
            }
            list.Add(new CastSurface { Bill = CardCostService.GetRuntimeCost(card, 0) });
            return list;
        }

        /// <summary>单账单的地源可付性（null=可付）：彩池按 Hall 子集（子集需求 ≤ 候选并集册数 −
        /// 待释放一册）；灰+彩合计 ≤ 可放地总册数（灰任意地可付）。黑白/任意需求地牌付不了也不判。</summary>
        private static LandFail FirstLandFail(ElementCost bill, List<ManaType> selfCands,
            int[] subsetPool, int landCapableTotal)
        {
            var b = ElementPaymentValidator.NormalizeBill(bill);
            int r = b.TryGetValue(ManaType.Red, out var rv) ? rv : 0;
            int bl = b.TryGetValue(ManaType.Blue, out var bv) ? bv : 0;
            int g = b.TryGetValue(ManaType.Green, out var gv) ? gv : 0;
            int y = b.TryGetValue(ManaType.Gray, out var yv) ? yv : 0;
            if (r == 0 && bl == 0 && g == 0 && y == 0) return null; // 无地牌可判需求（黑白/任意/零费）

            var landColors = new[] { ManaType.Red, ManaType.Blue, ManaType.Green };
            var demands = new[] { r, bl, g };
            int selfMask = 0; // 待释放一册占用的候选位（在场作地的供给要扣掉它）
            for (int i = 0; i < landColors.Length; i++)
                if (selfCands.Contains(landColors[i])) selfMask |= 1 << i;

            // 子集序：单色（红/蓝/绿）先行给最直白的「差几源」文案，再双色、后三色
            int[] subsetOrder = { 1, 2, 4, 3, 5, 6, 7 };
            foreach (int sub in subsetOrder)
            {
                int demand = 0;
                for (int i = 0; i < 3; i++)
                    if ((sub & (1 << i)) != 0) demand += demands[i];
                if (demand == 0) continue;
                int pool = subsetPool[sub] - ((selfMask & sub) != 0 ? 1 : 0);
                if (demand > pool)
                    return new LandFail { Detail = SubsetDetail(sub, landColors, demands, pool) };
            }

            int total = r + bl + g + y;
            int poolAll = landCapableTotal - (selfCands.Count > 0 ? 1 : 0);
            if (total > poolAll)
            {
                string grayPart = y > 0 ? $"（含灰{y}·任意地可付）" : "";
                return new LandFail { Detail = $"可放地的卡仅 {poolAll} 张，需 {total} 张同时供费{grayPart}" };
            }
            return null;
        }

        /// <summary>子集缺口文案：单色=「红源不足：可产红的卡仅 N 张，需 M 张」；
        /// 多色=「可产红/蓝的卡仅 N 张，需 M 张（红2+蓝2）」。</summary>
        private static string SubsetDetail(int sub, ManaType[] landColors, int[] demands, int pool)
        {
            var names = new List<string>();
            var parts = new List<string>();
            int demand = 0;
            for (int i = 0; i < 3; i++)
                if ((sub & (1 << i)) != 0)
                {
                    names.Add(ColorZh(landColors[i]));
                    parts.Add($"{ColorZh(landColors[i])}{demands[i]}");
                    demand += demands[i];
                }
            return names.Count == 1
                ? $"{names[0]}源不足：可产{names[0]}的卡仅 {pool} 张，需 {demand} 张"
                : $"可产{string.Join("/", names)}的卡仅 {pool} 张，需 {demand} 张（{string.Join("+", parts)}）";
        }

        public static string ColorZh(ManaType m)
        {
            switch (m)
            {
                case ManaType.Red: return "红";
                case ManaType.Blue: return "蓝";
                case ManaType.Green: return "绿";
                default: return "灰";
            }
        }

        /// <summary>费用串「红2+蓝0.5+灰1」（展示序红蓝绿灰）；空/全零=「0」。</summary>
        public static string FormatCost(ElementCost cost)
        {
            if (cost == null || cost.IsZero) return "0";
            return string.Join("+", DisplayOrder
                .Where(c => cost[c] > 0f)
                .Select(c => $"{ColorZh(c)}{cost[c]:0.#}"));
        }

        /// <summary>报告单行摘要（编辑器日志/toast 共用）：名（张数）：构成统计；通过/断链形状计数。</summary>
        public static string Describe(Report report)
        {
            if (report == null) return "(空报告)";
            string stats = report.CardsByColor.Count == 0
                ? "无费用"
                : string.Join(" · ", DisplayOrder
                    .Where(report.CardsByColor.ContainsKey)
                    .Select(c => $"{ColorZh(c)}{report.CardsByColor[c]}卡/{report.PipsByColor[c]:0.#}点"));
            string verdict = report.Ok
                ? "✔ 通过"
                : $"✘ 断链{report.MissingIds.Count}张/形状{report.ShapeErrors.Count}项";
            return $"{report.DeckName}（{report.CardCount}张）：{stats}——{verdict}";
        }

        /// <summary>卡组可用性单源（2026-10-16 全局对齐定案；2026-10-26 主题违色随主题约束删除）：
        /// 断链/形状违规任一即不可用——卡组界面（列表标记/保存拦截）、挑战入口（标记+开战回落）、
        /// 对战入口（下拉标记+提交拦截）共用本判定。</summary>
        public static bool Usable(DeckData deck) => Check(deck).Ok;

        /// <summary>不可用标签（三入口列表/下拉共用文案）："断链2/形状1·不可用"；可用=""。</summary>
        public static string UnusableTag(DeckData deck)
        {
            if (deck == null) return "";
            var r = Check(deck);
            if (r.Ok) return "";
            var parts = new List<string>();
            if (r.MissingIds.Count > 0) parts.Add("断链" + r.MissingIds.Count);
            if (r.ShapeErrors.Count > 0) parts.Add("形状" + r.ShapeErrors.Count);
            return string.Join("/", parts) + "·不可用";
        }
    }
}
