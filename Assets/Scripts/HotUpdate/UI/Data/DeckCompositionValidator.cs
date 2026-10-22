using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;

namespace SynergyUI
{
    /// <summary>
    /// 卡组构成检测（2026-10-15）：单源校验「主题卡组=主题色+灰」的双色纯度。编辑器侧
    /// （三套主题构建器落盘自检；原「Tools/卡组构成检测」菜单已移除）与卡组构筑界面
    /// （DeckBuilderScreen 添加口/保存口/统计区）共用本验证器——同一口径，改一处两边生效。
    ///
    /// 主题识别=卡组名首字（红→红+灰、绿→绿+灰、蓝→蓝+灰）；非红/绿/蓝开头=无颜色约束，
    /// 仅统计构成与断链（玩家自构筑多色卡组不受限）。
    ///
    /// 费用两层口径（与 2026-10-15 教学卡组人工复查同源）：
    /// ① 出牌费 card.Cost——EnsureCost 已刷新为推导费（恒推导定案：身材灰桶/关键词 K/效果锚价 E/
    ///    代价槽费烘焙后的推导分布，全量在此）；
    /// ② 主动效果的运行时锚价（CostDerivationService.DeriveElementCosts）——构筑期不占
    ///    卡费、发动时现付（CardCostService.E 桶跳过项），须单独检测。
    /// 断链（cardIds 在 Cards.json 无此 ID）单列——主题卡组重建期 0/30 断链的历史坑。
    /// </summary>
    public static class DeckCompositionValidator
    {
        public sealed class Violation
        {
            public string CardName;
            public string CardId;
            public string Surface;   // 「出牌费」/「发动费:效果名」
            public ManaType Color;   // 主题外颜色
            public float Amount;
            public string Detail;    // 人读明细（含该面费用全串）
            public int Copies;       // 卡组内册数（同卡多册聚合为一项）
        }

        public sealed class Report
        {
            public string DeckName;
            public ManaType? ThemeColor; // null=无主题约束（仅统计+断链）
            public List<Violation> Violations = new List<Violation>();
            public List<string> MissingIds = new List<string>();
            /// <summary>类型×形状违规（2026-10-15 深夜改版：生物禁连接光环/结界禁主动/法术形状）——
            /// 与主题违色分列（不属颜色约束，任何卡组都报）。</summary>
            public List<string> ShapeErrors = new List<string>();
            /// <summary>含该色的册数（出牌费口径，与统计区色点行同语义）。</summary>
            public Dictionary<ManaType, int> CardsByColor = new Dictionary<ManaType, int>();
            /// <summary>该色费点合计（出牌费口径）。</summary>
            public Dictionary<ManaType, float> PipsByColor = new Dictionary<ManaType, float>();
            public int CardCount;
            public bool Ok => Violations.Count == 0 && MissingIds.Count == 0 && ShapeErrors.Count == 0;
        }

        // 展示序=CostSquaresView.DisplayOrder 同款（红蓝绿黑白灰）
        private static readonly ManaType[] DisplayOrder =
        {
            ManaType.Red, ManaType.Blue, ManaType.Green, ManaType.Black, ManaType.White, ManaType.Gray,
        };

        /// <summary>卡组名首字 → 主题色；其余（含空名）null=无约束。</summary>
        public static ManaType? ThemeColorOf(string deckName)
        {
            if (string.IsNullOrEmpty(deckName)) return null;
            switch (deckName.Trim()[0])
            {
                case '红': return ManaType.Red;
                case '绿': return ManaType.Green;
                case '蓝': return ManaType.Blue;
                default: return null;
            }
        }

        /// <summary>整套卡组检测（DeckData 口径）。</summary>
        public static Report Check(DeckData deck)
            => deck == null ? new Report() : Check(deck.name, deck.cardIds);

        /// <summary>整套卡组检测：去重逐卡解析，出牌费入统计、两层费用对照主题约束、断链单列。</summary>
        public static Report Check(string deckName, IEnumerable<string> cardIds)
        {
            var report = new Report
            {
                DeckName = deckName ?? "",
                ThemeColor = ThemeColorOf(deckName),
            };
            if (cardIds == null) return report;

            // 同卡多册聚合（统计按册计；违例按「卡×面×色」去重记册数）
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

                if (report.ThemeColor == null) continue;
                foreach (var v in CheckCard(card, report.ThemeColor.Value))
                {
                    v.Copies = kv.Value;
                    report.Violations.Add(v);
                }
            }
            return report;
        }

        /// <summary>单卡两面检测（添加口拦截用；theme=null 由调用方回避）：
        /// 出牌费 + 各主动效果运行时锚价。</summary>
        public static List<Violation> CheckCard(CardData card, ManaType themeColor)
        {
            var list = new List<Violation>();
            if (card == null) return list;
            AddForeign(list, card, "出牌费", card.Cost, themeColor);

            var defs = CardEffectConverter.ConvertAll(card.Effects, card.ID);
            foreach (var def in defs)
            {
                if (def == null || !def.IsActivatedEffect) continue;
                var anchor = CostDerivationService.DeriveElementCosts(def);
                string fxName = string.IsNullOrEmpty(def.DisplayName) ? def.Id : def.DisplayName;
                AddForeign(list, card, "发动费:" + fxName, anchor, themeColor);
            }
            return list;
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

        private static void AddForeign(List<Violation> list, CardData card, string surface,
            ElementCost cost, ManaType themeColor)
        {
            if (cost == null || cost.IsZero) return;
            string cardName = string.IsNullOrEmpty(card.CardName) ? card.ID : card.CardName;
            foreach (var color in cost.NonzeroColors())
            {
                float amount = cost[color];
                if (amount <= 0f) continue;
                if (color == themeColor || color == ManaType.Gray) continue;
                list.Add(new Violation
                {
                    CardName = cardName,
                    CardId = card.ID,
                    Surface = surface,
                    Color = color,
                    Amount = amount,
                    Detail = $"{cardName}｜{surface}｜{ColorZh(color)}{amount:0.#}（全串 {FormatCost(cost)}）",
                });
            }
        }

        public static string ColorZh(ManaType m)
        {
            switch (m)
            {
                case ManaType.Red: return "红";
                case ManaType.Blue: return "蓝";
                case ManaType.Green: return "绿";
                case ManaType.Black: return "黑";
                case ManaType.White: return "白";
                default: return "灰";
            }
        }

        /// <summary>费用串「红2+蓝0.5+灰1」（展示序红蓝绿黑白灰）；空/全零=「0」。</summary>
        public static string FormatCost(ElementCost cost)
        {
            if (cost == null || cost.IsZero) return "0";
            return string.Join("+", DisplayOrder
                .Where(c => cost[c] > 0f)
                .Select(c => $"{ColorZh(c)}{cost[c]:0.#}"));
        }

        /// <summary>报告单行摘要（编辑器日志/toast 共用）：
        /// 名（张数）：构成统计；主题约束——通过/违色断链计数。</summary>
        public static string Describe(Report report)
        {
            if (report == null) return "(空报告)";
            string stats = report.CardsByColor.Count == 0
                ? "无费用"
                : string.Join(" · ", DisplayOrder
                    .Where(report.CardsByColor.ContainsKey)
                    .Select(c => $"{ColorZh(c)}{report.CardsByColor[c]}卡/{report.PipsByColor[c]:0.#}点"));
            string theme = report.ThemeColor == null
                ? "无主题约束"
                : $"主题约束 {ColorZh(report.ThemeColor.Value)}+灰";
            string verdict = report.Ok
                ? "✔ 通过"
                : $"✘ 违色{report.Violations.Count}项/断链{report.MissingIds.Count}张/形状{report.ShapeErrors.Count}项";
            return $"{report.DeckName}（{report.CardCount}张）：{stats}；{theme}——{verdict}";
        }

        /// <summary>卡组可用性单源（2026-10-16 全局对齐定案）：断链/形状违规/主题违色任一即
        /// 不可用——卡组界面（列表标记/保存拦截）、挑战入口（标记+开战回落）、对战入口
        /// （下拉标记+提交拦截）共用本判定。</summary>
        public static bool Usable(DeckData deck) => Check(deck).Ok;

        /// <summary>不可用标签（三入口列表/下拉共用文案）："断链2/形状1/违色3·不可用"；可用=""。</summary>
        public static string UnusableTag(DeckData deck)
        {
            if (deck == null) return "";
            var r = Check(deck);
            if (r.Ok) return "";
            var parts = new List<string>();
            if (r.MissingIds.Count > 0) parts.Add("断链" + r.MissingIds.Count);
            if (r.ShapeErrors.Count > 0) parts.Add("形状" + r.ShapeErrors.Count);
            if (r.Violations.Count > 0) parts.Add("违色" + r.Violations.Count);
            return string.Join("/", parts) + "·不可用";
        }
    }
}
