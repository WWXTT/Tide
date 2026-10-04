using System.Collections.Generic;
using System.Linq;
using CardCore;

namespace SynergyUI
{
    /// <summary>UGUI 卡牌层的布局形态：Full=手牌/卡组（全信息竖版）；Compact=战场单位；Land=地牌。</summary>
    public enum CardOverlayLayout
    {
        Full,
        Compact,
        Land,
    }

    /// <summary>
    /// UGUI 卡牌层显示模型（2026-09-30 方案2 定案：卡面改 UGUI SSO Canvas 整层压 UITK）。
    /// 对战 BattleCardView 与构筑 CardData 在此合并为同一口径——卡面 prefab 只认本模型，
    /// 不接触引擎对象与网络 DTO。
    /// </summary>
    public sealed class CardOverlayItem
    {
        public string Key;              // 池化/增量刷新键（对战=RuntimeId；构筑=CardId）
        public string Name = "";
        public string CostText = "";    // 如「红2 灰1」（文本兜底——方格缺数据时卡面仍可显示）
        public ElementCost Costs;       // 费用明细（2026-10-04 位置数组口径——卡面费用方格显示）
        public string StatsText = "";   // 如「3/2」「耐久2」
        public string TypeText = "";    // 生物/法术/结界…
        public string KeywordsText = "";
        public string EffectText = "";  // 效果摘要（代价行 + AtomText 渲染，可多行）
        public string LandTokensText = "";
        public int ArrowFlags;          // 与 BattleView 六向位一致
        public bool IsTapped, IsFrozen, IsDead;

        /// <summary>对战卡视图 → 显示模型。keepRuntimeStats=战场单位（光环加减后的实时 P/L 权威）。</summary>
        public static CardOverlayItem FromBattle(BattleCardView v, CardOverlayLayout layout)
        {
            var it = new CardOverlayItem
            {
                Key = "R" + v.RuntimeId,
                Name = v.Name ?? "",
                CostText = v.CostText ?? "",
                TypeText = v.SupertypeText ?? "",
                LandTokensText = v.LandTokensText ?? "",
                ArrowFlags = v.ArrowFlags,
                IsTapped = v.IsTapped,
                IsFrozen = v.IsFrozen,
                IsDead = v.IsDead,
            };
            var data = string.IsNullOrEmpty(v.CardId) ? null : CardCatalog.GetById(v.CardId);
            FillStatic(data, it, keepRuntimeStats: layout == CardOverlayLayout.Compact);
            if (layout == CardOverlayLayout.Compact && v.Runtime != null)
                it.StatsText = $"{v.Runtime.Power}/{v.Runtime.Life}";
            return it;
        }

        /// <summary>构筑卡表数据 → 显示模型（卡组区展示用）。</summary>
        public static CardOverlayItem FromCardData(CardData d, string key)
        {
            var it = new CardOverlayItem { Key = key };
            FillStatic(d, it, keepRuntimeStats: false);
            return it;
        }

        private static void FillStatic(CardData data, CardOverlayItem it, bool keepRuntimeStats)
        {
            if (data == null) return;
            if (string.IsNullOrEmpty(it.Name))
                it.Name = string.IsNullOrEmpty(data.CardName) ? data.ID : data.CardName;
            if (string.IsNullOrEmpty(it.CostText)) it.CostText = BattleView.CostTextOf(data.Cost);
            if (it.Costs == null && data.Cost != null && !data.Cost.IsZero)
                it.Costs = data.Cost;
            if (string.IsNullOrEmpty(it.TypeText)) it.TypeText = BattleView.SupertypeZh(data.Supertype);

            if (!keepRuntimeStats)
            {
                switch (data.Supertype)
                {
                    case Cardtype.Creature:
                        it.StatsText = $"{data.Power ?? 0}/{data.Life ?? 0}";
                        break;
                    case Cardtype.Enchantment:
                        if (data.Durability > 0) it.StatsText = $"耐久{data.Durability}";
                        break;
                }
            }

            if (data.Keywords != null && data.Keywords.Count > 0)
                it.KeywordsText = string.Join("·", data.Keywords.Select(KeywordZh));
            it.EffectText = EffectSummaryOf(data);
        }

        private static string KeywordZh(string keywordId)
        {
            var def = CardLoader.GetKeywordDefinition(keywordId);
            return string.IsNullOrEmpty(def?.nameZh) ? keywordId : def.nameZh;
        }

        /// <summary>效果摘要：与构筑预览（DeckBuilderScreen.BuildPreview）同口径——代价行 + 逐效果全文。</summary>
        private static string EffectSummaryOf(CardData data)
        {
            if (data.Effects == null || data.Effects.Count == 0) return "";
            var lines = new List<string>();
            var payload = data.PayloadCost?.payload;
            if (payload != null && !string.IsNullOrEmpty(payload.refId))
                lines.Add("代价：" + AtomText.RenderAtomEntry(payload));
            foreach (var fx in data.Effects)
            {
                if (fx == null) continue;
                var graph = new EffectGraphData(fx.DisplayName) { header = fx, steps = fx.Steps };
                var text = AtomText.RenderEffectSummary(graph);
                if (!string.IsNullOrEmpty(text)) lines.Add(text);
            }
            return string.Join("\n", lines);
        }
    }
}
