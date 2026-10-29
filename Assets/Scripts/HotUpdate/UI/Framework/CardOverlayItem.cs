using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.Attribute;

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
        public ElementCost Costs;       // 费用明细（净费口径——CardNetCost.NetCostOf 实时推导，P4 2026-10-29）
        public string StatsText = "";   // 如「3/2」「耐久2」
        public string TypeText = "";    // 生物/法术/结界…
        public string KeywordsText = "";
        public string EffectText = "";  // 效果摘要（代价行 + AtomText 渲染，可多行）
        public string LandTokensText = "";
        public int ArrowFlags;          // 与 BattleView 六向位一致
        public bool IsTapped, IsFrozen, IsDead;

        // ==================== 手牌全信息卡面（2026-10-13 ACardView 口径） ====================
        // 六区数据源：type/name/effect/keys/stats/passives——View 只消费本模型，不碰引擎对象。

        public Cardtype Supertype;                       // type 区：底图+遮罩形状映射
        public string Illustration = "";                 // type 区立绘资源名（空=保持预制体占位）
        public string AttributeText = "";                // attribute 区：攻击/生命/耐久（空=藏区；发动速度归效果句首）
        public int? AttrPower, AttrLife;                 // attribute 区图标数字（2026-10-22 B6：生物攻/血改图标+数字；
                                                          // null=非生物。AttributeText 保留全词=图标素材缺失时的回落档）
        public string PayloadText = "";                  // cost 区：代价行（PayloadCost 产黑白元素等；空=藏区）
        public List<string> KeywordChips;                // keys 区：关键词中文简称芯片（null/空=藏区）
        public List<(string Name, int Layers)> CounterChips; // stats 区：指示物简称+层数（空=藏区）
        public string ActiveEffectText = "";             // effect 区：唯一主动效果全文（空=藏区）
        public ElementCost ActiveEffectCost;             // effect 区发动消耗（AnchorCost 快照；全零=藏费用行）
        public List<string> PassiveLines;                // passives 区：被动行（已拼 (x/y) 后缀；无限/无对局不带）
        public Card RuntimeCard;                         // 事件刷新锚（本地模式引擎对象；null=不订阅）

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
            var data = string.IsNullOrEmpty(v.CardId) ? null : CardCatalog.GetByInstanceId(v.CardId);
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

        /// <summary>合成界面静态六区快照 → 显示模型（2026-10-13 中栏 ACard 预览）。
        /// 与 FromHand 同链同口径、去运行态：RuntimeCard=null（不订阅事件）、无 stats 芯片
        /// （指示物=运行态，合成期天然为空）、被动行不带 (x/y) 次数后缀（无对局无 executor）。
        /// 消费端=CardComposerScreen 编辑中的 CardData（含即时编辑态卡名/挂载顺序）。</summary>
        public static CardOverlayItem FromCardDataFull(CardData d, string key)
        {
            var it = new CardOverlayItem { Key = key };
            if (d == null) return it;
            it.Name = string.IsNullOrEmpty(d.CardName) ? d.ID : d.CardName;
            var net = CardNetCost.NetCostOf(d); // 净费实时推导（P4 2026-10-29）——不读 costList 显示缓存
            if (!net.IsZero) it.Costs = net;
            it.Supertype = d.Supertype;
            it.Illustration = d.Illustration ?? "";
            it.AttributeText = AttributeTextOf(d);
            if (d.Supertype == Cardtype.Creature) { it.AttrPower = d.Power ?? 0; it.AttrLife = d.Life ?? 0; }
            it.PayloadText = PayloadTextOf(d);

            // keys 芯片：印刷关键词（合成期无运行态，无授予/净化合并）
            if (d.Keywords != null && d.Keywords.Count > 0)
                it.KeywordChips = d.Keywords.Where(k => !string.IsNullOrEmpty(k)).Select(KeywordZh).ToList();

            // effect / passives 拆分：主动=首个 TriggerTiming∈{Activate_Active, Activate_Instant}
            //（挂载顺序决定谁上 effect 区——合成端 ↑↓ 排序的控制点），其余归被动
            if (d.Effects != null)
            {
                foreach (var fx in d.Effects)
                {
                    if (fx == null) continue;
                    var timing = (TriggerTiming)fx.TriggerTiming;
                    var isActive = timing == TriggerTiming.Activate_Active || timing == TriggerTiming.Activate_Instant;
                    var text = AtomText.RenderEffectSummary(new EffectGraphData(fx.DisplayName) { header = fx, steps = fx.Steps }, d);
                    if (string.IsNullOrEmpty(text)) continue;
                    if (isActive && string.IsNullOrEmpty(it.ActiveEffectText))
                    {
                        it.ActiveEffectText = WithActiveNetFormula(text, fx);
                        it.ActiveEffectCost = fx.AnchorCost;
                        continue;
                    }
                    (it.PassiveLines ??= new List<string>()).Add(text);
                }
            }
            return it;
        }

        /// <summary>主动效果全文 + 净费公式内联（P4 §1.3·2026-10-29）：带效果级代价的主动效果
        /// 追加「（发动费 X → 抵扣 Y → 净费 Z）」——与两合成器（P5）同源单口 CardComposerScreen.
        /// ActiveEffectNetFeeFormula（EffectActivationGate.NetBill 只读不重算）；无代价行原样返回。</summary>
        private static string WithActiveNetFormula(string text, CardEffectData fx)
        {
            var formula = CardComposerScreen.ActiveEffectNetFeeFormula(fx);
            return formula == null ? text : $"{text}（{formula}）";
        }

        /// <summary>手牌全信息卡面 → 显示模型（2026-10-13，ACardView 专用）。
        /// 三源合并：BattleCardView（名称/费用/横置）+ 卡表（类型/效果拆分/立绘）+ 运行态快照（关键词/指示物）。
        /// 被动 (x/y)：y=TriggerLimitPerTurn 归一（0→1，-1=无限不带后缀）；x=本回合已作用次数
        /// （只读查询口 EffectExecutor.GetTurnUsageOf；网络模式无引擎对象→不带后缀）。幂等可重查。</summary>
        public static CardOverlayItem FromHand(BattleCardView v)
        {
            var it = new CardOverlayItem
            {
                Key = "R" + v.RuntimeId,
                Name = v.Name ?? "",
                CostText = v.CostText ?? "",
                IsTapped = v.IsTapped,
                IsFrozen = v.IsFrozen,
                RuntimeCard = v.CoreCard,
            };
            var data = string.IsNullOrEmpty(v.CardId) ? null : CardCatalog.GetByInstanceId(v.CardId);
            if (data != null)
            {
                if (string.IsNullOrEmpty(it.Name)) it.Name = string.IsNullOrEmpty(data.CardName) ? data.ID : data.CardName;
                if (it.Costs == null)
                {
                    var net = CardNetCost.NetCostOf(data); // 净费实时推导（P4 2026-10-29）——不读 costList 显示缓存
                    if (!net.IsZero) it.Costs = net;
                }
                it.Supertype = data.Supertype;
                it.Illustration = data.Illustration ?? "";
                it.AttributeText = AttributeTextOf(data);
                if (data.Supertype == Cardtype.Creature) { it.AttrPower = data.Power ?? 0; it.AttrLife = data.Life ?? 0; }
                it.PayloadText = PayloadTextOf(data);
            }

            // keys 芯片：运行态关键词优先（含授予/净化），回落印刷
            var keywords = v.Runtime?.Keywords ?? data?.Keywords?.ToArray();
            if (keywords != null && keywords.Length > 0)
                it.KeywordChips = keywords.Where(k => !string.IsNullOrEmpty(k)).Select(KeywordZh).ToList();

            // stats 芯片：运行态指示物（层数≠0 且非内部读数层）
            if (v.Runtime?.Counters != null)
            {
                foreach (var c in v.Runtime.Counters)
                {
                    if (c == null || c.Value == 0) continue;
                    var spec = CardCore.Attribute.CounterRules.Find(c.Key);
                    if (spec != null && spec.LayerRole == CardCore.Attribute.CounterLayerRole.Reading) continue;
                    (it.CounterChips ??= new List<(string, int)>())
                        .Add((string.IsNullOrEmpty(spec?.DisplayName) ? c.Key : spec.DisplayName, c.Value));
                }
            }

            // effect / passives 拆分：主动=TriggerTiming∈{Activate_Active, Activate_Instant}（与 CardData.HasActiveEffect 同源），
            // 其余全部归被动（含 Activate_Response——按被动展示）
            if (data?.Effects != null)
            {
                var executor = v.CoreCard != null && GameCore.Instance != null
                    ? GameCore.Instance.StackEngine.GetExecutor() : null;
                foreach (var fx in data.Effects)
                {
                    if (fx == null) continue;
                    var timing = (TriggerTiming)fx.TriggerTiming;
                    var isActive = timing == TriggerTiming.Activate_Active || timing == TriggerTiming.Activate_Instant;
                    var text = AtomText.RenderEffectSummary(new EffectGraphData(fx.DisplayName) { header = fx, steps = fx.Steps }, data);
                    if (string.IsNullOrEmpty(text)) continue;
                    if (isActive && string.IsNullOrEmpty(it.ActiveEffectText))
                    {
                        it.ActiveEffectText = WithActiveNetFormula(text, fx); // P4 §1.3：净费公式内联
                        it.ActiveEffectCost = fx.AnchorCost;
                        continue;
                    }
                    (it.PassiveLines ??= new List<string>()).Add(text + UsageSuffix(fx, executor, v.CoreCard));
                }
            }
            return it;
        }

        /// <summary>被动次数后缀：y=归一上限（-1=无限→不带后缀）；x 仅本地模式可查（网络无引擎对象→整段不带）。
        /// prefix=悬停文案用的量纲前缀（如「本回合 」；卡面调用不带——保持「（0/1）」短形态）。</summary>
        public static string UsageSuffix(CardEffectData fx, EffectExecutor executor, Card source, string prefix = "")
        {
            int y = fx.TriggerLimitPerTurn == 0 ? 1 : fx.TriggerLimitPerTurn; // 0=未声明→按 1（转换器同口径）
            if (y < 0) return "";                                             // -1=显式无限
            if (executor == null) return "";
            int x = executor.GetTurnUsageOf(fx.Id, source);
            return $"（{prefix}{x}/{y}）";
        }

        /// <summary>引擎活卡 → 手牌显示模型（View 刷新专用）：重快照运行态 + 卡表静态面合并，
        /// 与 FromHand 同链同口径。core=null 安全返回 null。</summary>
        public static CardOverlayItem FromLiveCard(Card core)
        {
            if (core == null) return null;
            var data = CardCatalog.GetByInstanceId(core.ID);
            return FromHand(new BattleCardView
            {
                RuntimeId = core.RuntimeId,
                CardId = core.ID,
                CoreCard = core,
                Runtime = CardCore.Serialization.SerializableRuntimeCardState.FromCard(core),
                Name = data != null ? data.CardName : "",
                CostText = data != null ? BattleView.CostTextOf(data.Cost) : "",
            });
        }

        private static void FillStatic(CardData data, CardOverlayItem it, bool keepRuntimeStats)
        {
            if (data == null) return;
            if (string.IsNullOrEmpty(it.Name))
                it.Name = string.IsNullOrEmpty(data.CardName) ? data.ID : data.CardName;
            if (string.IsNullOrEmpty(it.CostText)) it.CostText = BattleView.CostTextOf(CardNetCost.NetCostOf(data));
            if (it.Costs == null)
            {
                var net = CardNetCost.NetCostOf(data); // 净费实时推导（P4 2026-10-29）
                if (!net.IsZero) it.Costs = net;
            }
            if (string.IsNullOrEmpty(it.TypeText)) it.TypeText = BattleView.SupertypeZh(data.Supertype);
            // 立绘（2026-10-15 接线修复）：FromBattle/FromCardData 消费面（手牌初始构建/预览/列卡/构筑列表）
            // 此前漏填——ACardView 占位图"全同卡面"根因；与 FromHand/FromCardDataFull 同口径合并。
            if (string.IsNullOrEmpty(it.Illustration)) it.Illustration = data.Illustration ?? "";

            if (!keepRuntimeStats)
            {
                switch (data.Supertype)
                {
                    case Cardtype.Creature:
                        it.StatsText = $"{data.Power ?? 0}/{data.Life ?? 0}";
                        break;
                    case Cardtype.Enchantment:
                        it.StatsText = $"耐久{CardCompositionCost.TotalDurabilityOf(data)}"; // 2026-10-17：结界恒有耐久（派生单源）
                        break;
                }
            }

            if (data.Keywords != null && data.Keywords.Count > 0)
                it.KeywordsText = string.Join("·", data.Keywords.Select(KeywordZh));
            it.EffectText = EffectSummaryOf(data);
        }

        /// <summary>attribute 区紧凑格式（2026-10-15 晚定案）：生物=攻击/生命、结界=耐久（2026-10-17
        /// 起恒显——结界一律走耐久经济，TotalDurabilityOf 派生单源）；
        /// 发动速度不在此显示（与速度定案冲突——速度归效果描述句首，见 AtomText.ActivationPrefix），
        /// 空串=藏区。</summary>
        private static string AttributeTextOf(CardData d)
        {
            var parts = new List<string>();
            if (d.Supertype == Cardtype.Creature)
            {
                parts.Add($"攻击{d.Power ?? 0}");
                parts.Add($"生命{d.Life ?? 0}");
            }
            else if (d.Supertype == Cardtype.Enchantment)
                parts.Add((d.IsField ? "场地·" : "") + $"耐久{CardCompositionCost.TotalDurabilityOf(d)}"); // P4 §1.5：场地结界加「场地·」前缀（与合成器卡面摘要同措辞）
            return parts.Count == 0 ? "" : string.Join(" ", parts);
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
            var payloadLine = PayloadTextOf(data);
            if (payloadLine != null) lines.Add(payloadLine);
            foreach (var fx in data.Effects)
            {
                if (fx == null) continue;
                var graph = new EffectGraphData(fx.DisplayName) { header = fx, steps = fx.Steps };
                var text = AtomText.RenderEffectSummary(graph, data);
                if (!string.IsNullOrEmpty(text)) lines.Add(text);
            }
            return string.Join("\n", lines);
        }

        /// <summary>代价行（2026-10-16 组装效果代价；2026-10-21 补效果级 costs；2026-10-29 P4 §1.1/1.4
        /// 改版）：原子按极性分两臂标注——「代价（自伤）：…」（p&lt;0 锁己方）/「代价（资敌）：…」
        ///（p&gt;0 锁对方，CostPayability 同判定），臂内以「＋」连接；行尾接三段式净费公式
        /// 「推导 X − 代价抵扣 Y = 净费 Z」（CardNetCost.NetCostFormulaOf——有抵扣才显示）。
        /// 效果级 Costs 的 payload 原子并入（主动效果的代价由执行器现付同源）。null=无代价行。</summary>
        private static string PayloadTextOf(CardData d)
        {
            var selfParts = new List<string>();   // 自伤臂（p<0 锁己方）
            var enemyParts = new List<string>();  // 资敌臂（p≥0 表侧——p=0 为契约违例原子，按资敌位展示由校验另行标红）
            var atoms = new List<AtomicEffectEntry>();
            void Collect(AtomicEffectEntry a)
            {
                if (a == null || string.IsNullOrEmpty(a.refId)) return;
                var text = AtomText.RenderAtomEntry(a);
                (AtomicEffectTable.GetByHashId(a.refId)?.Polarity < 0f ? selfParts : enemyParts).Add(text);
                atoms.Add(a);
            }
            foreach (var a in d?.PayloadCost?.AllPayloads() ?? new List<AtomicEffectEntry>()) Collect(a);
            if (d?.Effects != null)
                foreach (var fx in d.Effects)
                {
                    if (fx?.Costs == null) continue;
                    foreach (var c in fx.Costs)
                        foreach (var a in c?.AllPayloads() ?? new List<AtomicEffectEntry>()) Collect(a);
                }
            if (selfParts.Count == 0 && enemyParts.Count == 0) return null;
            var arms = new List<string>();
            if (selfParts.Count > 0) arms.Add("代价（自伤）：" + string.Join("＋", selfParts));
            if (enemyParts.Count > 0) arms.Add("代价（资敌）：" + string.Join("＋", enemyParts));
            var formula = CardNetCost.NetCostFormulaOf(d); // 三段式（有代价抵扣才非空）
            return string.Join("｜", arms) + CardComposerScreen.PayloadGrantText(atoms)
                   + (formula == null ? "" : "｜" + formula);
        }
    }

    // ======================================== 净费单源（P4 §1 2026-10-29） ========================================

    /// <summary>卡面净费口径单源（P4 净费显示）：玩家实付=推导费 − 代价抵扣（战斗态另有建造预付/
    /// 苏醒免灰/费用指示物层，各有专属展示口——本类只管「推导 − 代价抵扣」两层）。
    /// 与本文件同收口：新文件进不了已生成的 csproj 是老坑（SearchQuery 先例）。
    /// 全部实时推导（CardCostService.GetRuntimeCost / GetPrePayloadCost——Derive 内
    /// PrePayloadCost→ReduceBuckets(代价价)→DerivedCost，三段自洽由引擎构造保证），不读 costList 缓存。</summary>
    public static class CardNetCost
    {
        /// <summary>公式行内费用分布序（P4 五类定案：红蓝绿灰万能）。</summary>
        private static readonly ManaType[] RuneOrder =
        {
            ManaType.Red, ManaType.Blue, ManaType.Green, ManaType.Gray, ManaType.Any,
        };

        /// <summary>净费（费用区口径·实时推导）：非抉择=GetRuntimeCost；抉择=净总额最大的模式费
        ///（与装载期 costList「最大模式费」显示口径一致——未选模式前展示保守上界）。全零=免费。</summary>
        public static ElementCost NetCostOf(CardData card)
        {
            if (card == null) return new ElementCost();
            if (!CostDerivationService.HasChoiceEffect(card))
                return CardCostService.GetRuntimeCost(card, 0);
            return CardCostService.GetModeCost(card, MaxNetModeOf(card));
        }

        /// <summary>三段式公式行（P4 §1.1）：「推导 2红3灰 − 代价抵扣 2 = 净费 2红1灰」。
        /// 无代价抵扣（Y=0）→ null（费用区净费即推导费，无差可显）。X/Y/Z 同模式取数自洽。</summary>
        public static string NetCostFormulaOf(CardData card)
        {
            if (card == null) return null;
            int mode = CostDerivationService.HasChoiceEffect(card) ? MaxNetModeOf(card) : 0;
            CardCostService.GetPrePayloadCost(card, mode, out var pre, out var points);
            if (points <= 0) return null;
            var net = CostDerivationService.HasChoiceEffect(card)
                ? CardCostService.GetModeCost(card, mode)
                : CardCostService.GetRuntimeCost(card, mode);
            return $"推导 {CostRunes(pre)} − 代价抵扣 {points} = 净费 {CostRunes(net)}";
        }

        /// <summary>费用分布文本：非零色按五类序拼「N色」（如「2红3灰」）；全零=「0」。</summary>
        public static string CostRunes(ElementCost cost)
        {
            if (cost == null || cost.IsZero) return "0";
            var parts = new List<string>();
            foreach (var c in RuneOrder)
                if (cost[c] > 0f) parts.Add(cost[c].ToString("0.##") + ManaTypeNames.ZhNameOf(c));
            return parts.Count == 0 ? "0" : string.Join("", parts);
        }

        /// <summary>净总额最大的模式序（抉择卡专用；非抉择/异常=0）。</summary>
        private static int MaxNetModeOf(CardData card)
        {
            int modes = CostDerivationService.GetModeCount(card);
            int best = 0;
            float bestTotal = -1f;
            for (int i = 0; i < modes; i++)
            {
                float total = CardCostService.GetModeCost(card, i).Total;
                if (total > bestTotal) { bestTotal = total; best = i; }
            }
            return best;
        }
    }
}
