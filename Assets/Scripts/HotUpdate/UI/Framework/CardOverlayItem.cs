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

    /// <summary>手牌特殊效果行种类（2026-11-01 上卡改版，SpUse 钮退役）：充能=任意卡型
    ///（2026-11-06 opt-in：充能行只出现在卡面声明 chargeAbility 的卡上——不再是全卡统一行）。
    /// 行交互（选中/再点取消）与 Use 钮分流见 HandFanCard/BattleScreen。</summary>
    public enum SpEffectKind
    {
        Charge,
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
        public ElementCost PayloadCost; // 代价色构成（2026-11-03 CostSquares Cost2 组数据源：施放期代价
                                        // 按色生成面 PayloadGrantByColor，与净费抵扣/地牌门槛同源；null=无代价）
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
        public string AttributeText = "";                // attribute 区：攻击/生命/耐久（空=藏区；速度归 effect 行 lbl-skill-speed——EffectSpeed）
        public int? AttrPower, AttrLife;                 // 上半徽章数字（2026-10-30 美术定档：生物 Atc/Hea 烘焙徽章；
                                                          // null=非生物）
        public int? AttrDurability;                      // 上半徽章数字：结界耐久（Hea 徽章；TotalDurabilityOf 派生
                                                          // 单源，与 AttributeText 同口径；null=非结界）
        /// <summary>生物攻/守资格位（2026-11-06 opt-in 三开关）→ 卡面能力图标：AttackAbility=Ac
        ///（Atc 徽章旁）、GuardAbility=Dc（Hea 徽章旁）——开=显示、关=隐藏；默认 false。
        /// FromHand/FromCardDataFull 生物分支填充（与 AttrPower 同点）。</summary>
        public bool AttackAbility, GuardAbility;
        public string PayloadText = "";                  // cost 区：代价行（PayloadCost 产黑白元素等；空=藏区）
        public List<string> KeywordChips;                // keys 区：关键词中文简称芯片（null/空=藏区）
        public List<(string Name, int Layers)> CounterChips; // stats 区：指示物简称+层数（空=藏区）
        public string ActiveEffectText = "";             // effect 区：唯一主动效果全文（空=藏区）
        /// <summary>effect 行费用组（2026-11-06 方格改版）：带效果级代价的主动=净费
        ///（ActiveEffectNetCost=发动费−抵扣），无代价行=锚价/模式费账单；全零=藏费用行。</summary>
        public ElementCost ActiveEffectCost;
        /// <summary>effect 行代价组（2026-11-06）：效果级代价分色面（EffectPayloadCostOf）；
        /// null=该行无效果级代价——消费方回落卡级施放代价 PayloadCost。</summary>
        public ElementCost ActiveEffectPayloadCost;
        public List<string> PassiveLines;                // passives 区：被动行（已拼 (x/y) 后缀；无限/无对局不带）
        /// <summary>多主动法术逐效果行（2026-11-01 抉择上卡改版）：法术可施放主动 ≥2 时 FromHand 按
        /// GetCastableEffects 序填充（下标=引擎 modeIndex 对位，含空文案行防错位）——ACardView 克隆
        /// effect 行逐行渲染，手牌端点击行直选施放模式；此时这些效果不再占 ActiveEffectText/PassiveLines。</summary>
        public List<string> ActiveEffectLines;
        /// <summary>效果行标题（与 ActiveEffectLines 平行）：DisplayName 空=「作用 N」。</summary>
        public List<string> ActiveEffectLabels;
        /// <summary>效果行费用组（与 ActiveEffectLines 平行；净费口径——带代价行=发动费−抵扣，
        /// 无代价行=GetRuntimeCost 模式费）——手牌 CostSquares 随选中行联动显示。</summary>
        public List<ElementCost> ActiveEffectCosts;
        /// <summary>效果行代价组（与 ActiveEffectLines 平行；效果级代价分色面，null 项=该行无效果级代价
        /// ——消费方回落卡级 PayloadCost）。</summary>
        public List<ElementCost> ActiveEffectPayloadCosts;
        /// <summary>特殊效果行（2026-11-01 上卡）：充能（有 Charge 原子效果，任意卡型）一行，
        /// 渲染在效果模式行之后——手牌端点行选中（再点同行取消），
        /// Use 钮文案随选中变「充能」并按此分流施放（SpUse 钮退役）。
        /// 四表平行等长。</summary>
        public List<string> SpEffectLabels;
        public List<string> SpEffectLines;
        public List<SpEffectKind> SpEffectKinds;
        /// <summary>特殊行费用（充能=null——现付 1 点待选色，施放时才定）。</summary>
        public List<ElementCost> SpEffectCosts;
        /// <summary>effect 行速度标签值（2026-11-06 速度显示改版：效果文字退役速度句首——速度归卡
        /// 类别，ACard effect 行 lbl-skill-speed 独显）：法术=旧底盘分配（EffectSpeedOf 单源）；
        /// 生物=0；结界/其余=null=藏标签（结界效果恒自动，无速度概念）。FromHand/FromCardDataFull 填充。</summary>
        public int? EffectSpeed;
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
            if (d.Supertype == Cardtype.Creature)
            {
                it.AttrPower = d.Power ?? 0; it.AttrLife = d.Life ?? 0;
                it.AttackAbility = d.AttackAbility; it.GuardAbility = d.GuardAbility; // Ac/Dc 能力图标位
            }
            else if (d.Supertype == Cardtype.Enchantment) it.AttrDurability = CardCompositionCost.TotalDurabilityOf(d);
            it.PayloadText = PayloadTextOf(d);
            // 代价色构成（2026-11-06 预览补齐）：与手牌 FromHand 同口径——合成器/卡组构筑的
            // ACard 预览同样显 Cost2 代价方格（此前只有手牌填，预览恒缺）。
            it.PayloadCost = PayloadCostOf(d);
            it.EffectSpeed = EffectSpeedOf(d); // effect 行速度标签（2026-11-06：类别驱动单源）

            // keys 芯片：印刷关键词（合成期无运行态，无授予/净化合并）
            if (d.Keywords != null && d.Keywords.Count > 0)
                it.KeywordChips = d.Keywords.Where(k => !string.IsNullOrEmpty(k)).Select(KeywordZh).ToList();

            // effect / passives 拆分（2026-11-13 多主动预览对齐手牌口径）：主动=TriggerTiming∈
            // {Activate_Active, Activate_Instant}；**法术可施放主动 ≥2 时不再「首个上 effect 区、
            // 其余混进自动效果」**——按 GetCastableEffects 序逐效果成行（ActiveEffectLines，ACardView
            // 克隆 effect 行渲染，与 FromHand 同口径；行序=引擎 modeIndex 严格对位），这些效果不再
            // 并入 ActiveEffectText/PassiveLines。单主动卡与生物/结界的自动效果仍走原拆分。
            var castable = d.Supertype == Cardtype.Spell ? CostDerivationService.GetCastableEffects(d) : null;
            var rowSet = castable != null && castable.Count >= 2 ? new HashSet<CardEffectData>(castable) : null;
            if (rowSet != null)
            {
                it.ActiveEffectLines = new List<string>();
                it.ActiveEffectLabels = new List<string>();
                it.ActiveEffectCosts = new List<ElementCost>();
                it.ActiveEffectPayloadCosts = new List<ElementCost>();
                for (int i = 0; i < castable.Count; i++)
                {
                    var fx = castable[i];
                    var text = AtomText.RenderEffectSummary(new EffectGraphData(fx.DisplayName) { header = fx, steps = fx.Steps }, d);
                    it.ActiveEffectLabels.Add(SpellEffectLabel(fx, i));
                    it.ActiveEffectLines.Add(text ?? "");
                    // 费用组=净费（无代价=模式费）；2026-11-09 逐色进位：卡侧费=引擎实扣整数
                    it.ActiveEffectCosts.Add(ElementPaymentValidator.CeilBill(
                        CardComposerScreen.ActiveEffectNetCost(fx)
                        ?? CardCostService.GetRuntimeCost(d, i)));
                    it.ActiveEffectPayloadCosts.Add(CardComposerScreen.EffectPayloadCostOf(fx));
                }
            }
            if (d.Effects != null)
            {
                foreach (var fx in d.Effects)
                {
                    if (fx == null || rowSet != null && rowSet.Contains(fx)) continue;
                    var timing = (TriggerTiming)fx.TriggerTiming;
                    var isActive = timing == TriggerTiming.Activate_Active || timing == TriggerTiming.Activate_Instant;
                    var text = AtomText.RenderEffectSummary(new EffectGraphData(fx.DisplayName) { header = fx, steps = fx.Steps }, d);
                    if (string.IsNullOrEmpty(text)) continue;
                    if (isActive && string.IsNullOrEmpty(it.ActiveEffectText))
                    {
                        // 2026-11-06：净费/代价公式文本退役——费用组=净费、代价组=效果级代价，进行级方格
                        it.ActiveEffectText = text;
                        // 2026-11-09 逐色进位：卡侧主动效果费=引擎发动时实扣的整数（与支付终点同 ε）
                        it.ActiveEffectCost = ElementPaymentValidator.CeilBill(
                            CardComposerScreen.ActiveEffectNetCost(fx) ?? fx.AnchorCost);
                        it.ActiveEffectPayloadCost = CardComposerScreen.EffectPayloadCostOf(fx);
                        continue;
                    }
                    (it.PassiveLines ??= new List<string>()).Add(text);
                }
            }
            return it;
        }

        /// <summary>施放期代价色构成 → Cost2 方格数据（2026-11-06 提单源）：FromHand（手牌扇/战斗）
        /// 与 FromCardDataFull（合成器/卡组构筑预览）共用——施放期桶口径（2026-11-08 代价效果级化：
        /// 非主动效果行入桶；主动效果级代价走行级 ActiveEffectPayloadCost(s)）。
        /// null=无施放期代价（SetPayloadCosts(null)=藏 Cost2 组）。</summary>
        private static ElementCost PayloadCostOf(CardData data)
        {
            var grant = CardCostService.PayloadGrantByColor(data);
            if (grant == null || grant.Count == 0) return null;
            var payloadCost = new ElementCost();
            foreach (var kv in grant) payloadCost[kv.Key] = kv.Value;
            return payloadCost.IsZero ? null : payloadCost;
        }

        /// <summary>法术效果行标题（2026-11-01 抉择上卡·承袭原模式窗选项文案口径）：
        /// DisplayName 空=「作用 N」。</summary>
        private static string SpellEffectLabel(CardEffectData fx, int index)
            => string.IsNullOrEmpty(fx?.DisplayName) ? $"作用 {index + 1}" : fx.DisplayName;

        /// <summary>充能行文案（2026-11-05 充能系统动作化改版；2026-11-06 opt-in 修订）：充能=速度0
        /// 系统动作（不再是卡上 Charge 原子效果），卡面声明资格、一回合一次可宣——文案为系统规则
        /// 描述（全卡统一；开资格计价=卡费固定 +1 灰）。</summary>
        private static string ChargeRowText(CardData data)
            => "充能：速度0系统动作，本回合一次（仅己方主阶段）；结算期选择此卡具有的颜色，"
               + "为其放置1层同色费用降低指示物并展示这张卡（不支付元素；换区清除）";

        /// <summary>压制层数快照读数（沉默/失效/沉睡）——FromHand 与战场悬停面板
        /// 共用行级划线判定，与引擎 SuppressRules 计数原语同源。</summary>
        public static (int silence, int nullify, int sleep) SuppressionOf(
            CardCore.Serialization.SerializableRuntimeCardState runtime)
        {
            int silence = 0, nullify = 0, sleep = 0;
            if (runtime?.Counters != null)
            {
                foreach (var c in runtime.Counters)
                {
                    if (c.Key == CardCore.Attribute.CounterRules.SilenceCounter) silence = c.Value;
                    else if (c.Key == CardCore.Attribute.CounterRules.NullifyCounter) nullify = c.Value;
                    else if (c.Key == CardCore.Attribute.KeywordRules.SleepCounter) sleep = c.Value;
                }
            }
            return (silence, nullify, sleep);
        }

        /// <summary>绝缘体是否在场（2026-11-09 死层定案）：本地模式读引擎活卡；网络模式无
        /// CoreCard，退回快照关键词（手牌/牌库不会持有 Resident 类失效/沉睡层，误判无副作用）。</summary>
        public static bool IsInsulatorActive(BattleCardView v)
        {
            if (v == null) return false;
            if (v.CoreCard != null) return CardCore.Attribute.InsulatorRules.IsActive(v.CoreCard);
            var kws = v.Runtime?.Keywords;
            if (kws == null) return false;
            foreach (var k in kws)
                if (k == CardCore.Attribute.KeywordRules.Insulator) return true;
            return false;
        }

        /// <summary>压制行样式（2026-10-31 定案）：被压的效果/关键词行删除线+灰字——TMP 富文本
        ///（ACard 四行区 des 与战场悬停面板均已开富文本）。suppressed=false 原样返回。</summary>
        public static string SuppressedText(string text, bool suppressed)
            => suppressed && !string.IsNullOrEmpty(text)
                ? $"<s><color=#7f7f7f>{text}</color></s>"
                : text;

        /// <summary>手牌全信息卡面 → 显示模型（2026-10-13，ACardView 专用）。
        /// 三源合并：BattleCardView（名称/费用/横置）+ 卡表（类型/效果拆分/立绘）+ 运行态快照（关键词/指示物）。
        /// 被动效果不再带 (x/y) 次数后缀（2026-10-30 无次数上限定案：没有上限可显示）。幂等可重查。</summary>
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
                if (data.Supertype == Cardtype.Creature)
                {
                    it.AttrPower = data.Power ?? 0; it.AttrLife = data.Life ?? 0;
                    it.AttackAbility = data.AttackAbility; it.GuardAbility = data.GuardAbility; // Ac/Dc 能力图标位
                }
                else if (data.Supertype == Cardtype.Enchantment) it.AttrDurability = CardCompositionCost.TotalDurabilityOf(data);
                it.PayloadText = PayloadTextOf(data);
                // 代价色构成（2026-11-03 手牌费用方格双组）：施放期代价按色生成面——与净费
                // 抵扣/地牌门槛同源口径（主动效果行不入施放期桶，其代价发动时现付不在此列）。
                it.PayloadCost = PayloadCostOf(data);
                it.EffectSpeed = EffectSpeedOf(data); // effect 行速度标签（2026-11-06：类别驱动单源）
            }

            // 压制态（2026-10-31 删除线定案）：沉默/失效/沉睡层数 → 主动/被动行与本体关键词芯片划线灰显
            // 2026-11-09 用户定案：绝缘体单位上的失效/沉睡是死层（层可见、不生效）——不划线；
            // 与引擎 SuppressRules.IsAutoSuppressed(Entity) 同判（此处只有快照计数，故先判绝缘体）。
            var sup = SuppressionOf(v.Runtime);
            bool insulatorActive = IsInsulatorActive(v);
            bool activeSuppressed = !insulatorActive
                                    && CardCore.Attribute.SuppressRules.IsActiveSuppressed(sup.silence, sup.sleep);
            bool autoSuppressed = !insulatorActive
                                  && CardCore.Attribute.SuppressRules.IsAutoSuppressed(sup.nullify, sup.sleep);

            // keys 芯片：运行态关键词优先（含授予/净化），回落印刷；本体关键词被压（沉睡/失效）
            // → 芯片划线灰显（与引擎 HasKeyword 读假同口：SuppressRules.IsAutoSuppressed）
            var keywords = v.Runtime?.Keywords ?? data?.Keywords?.ToArray();
            if (keywords != null && keywords.Length > 0)
                it.KeywordChips = keywords.Where(k => !string.IsNullOrEmpty(k))
                    .Select(k => SuppressedText(KeywordZh(k), autoSuppressed)).ToList();

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
            // 其余全部归被动（含 Activate_Response——按被动展示）。
            // 多主动法术（2026-11-01 抉择上卡改版）：可施放主动按 GetCastableEffects 序逐效果成行
            //（ActiveEffectLines——ACardView 克隆 effect 行渲染，手牌端点行切换施放模式），
            // 这些效果不再并入 ActiveEffectText/PassiveLines；行序=引擎 modeIndex 严格对位。
            var castable = data != null && data.Supertype == Cardtype.Spell
                ? CostDerivationService.GetCastableEffects(data) : null;
            var rowSet = castable != null && castable.Count >= 2 ? new HashSet<CardEffectData>(castable) : null;
            if (rowSet != null)
            {
                it.ActiveEffectLines = new List<string>();
                it.ActiveEffectLabels = new List<string>();
                it.ActiveEffectCosts = new List<ElementCost>();
                it.ActiveEffectPayloadCosts = new List<ElementCost>();
                for (int i = 0; i < castable.Count; i++)
                {
                    var fx = castable[i];
                    var text = AtomText.RenderEffectSummary(new EffectGraphData(fx.DisplayName) { header = fx, steps = fx.Steps }, data);
                    it.ActiveEffectLabels.Add(SpellEffectLabel(fx, i));
                    it.ActiveEffectLines.Add(string.IsNullOrEmpty(text)
                        ? "" : SuppressedText(text, activeSuppressed)); // 2026-11-06：公式文本退役——纯摘要
                    // 费用组=净费（无代价=模式费）；2026-11-09 逐色进位——卡侧费=引擎实扣整数
                    it.ActiveEffectCosts.Add(ElementPaymentValidator.CeilBill(
                        CardComposerScreen.ActiveEffectNetCost(fx)
                        ?? CardCostService.GetRuntimeCost(data, i)));
                    it.ActiveEffectPayloadCosts.Add(CardComposerScreen.EffectPayloadCostOf(fx)); // 代价组=效果级代价
                }
            }
            // 特殊效果行（2026-11-05 充能系统动作化改版；2026-11-06 opt-in 修订）：充能=速度0系统
            // 动作（不再是卡上效果），**卡面声明 chargeAbility 才有资格**（组合界面 basicability
            // 「具备充能」）——有资格的手牌卡带一行充能入口（选中→Use 钮变「充能」分流到
            // DeclareCharge；颜色在结算期经引擎反问下拉选择）。渲染在模式行之后。
            //（旧「有 Charge 原子效果才出充能行」行替代逻辑已随原子链退役；「全卡统一出充能行」
            //  为 2026-11-05~11-06 间过渡口径，随 opt-in 收回。）
            if (data != null && data.ChargeAbility)
            {
                it.SpEffectLabels = new List<string>();
                it.SpEffectLines = new List<string>();
                it.SpEffectKinds = new List<SpEffectKind>();
                it.SpEffectCosts = new List<ElementCost>();
                it.SpEffectLabels.Add("充能");
                it.SpEffectLines.Add(ChargeRowText(data));
                it.SpEffectKinds.Add(SpEffectKind.Charge);
                it.SpEffectCosts.Add(null); // 免费落层（不支付元素）；可充色随卡费动态，无固定账单
            }
            if (data?.Effects != null)
            {
                foreach (var fx in data.Effects)
                {
                    if (fx == null || rowSet != null && rowSet.Contains(fx)) continue;
                    var timing = (TriggerTiming)fx.TriggerTiming;
                    var isActive = timing == TriggerTiming.Activate_Active || timing == TriggerTiming.Activate_Instant;
                    var text = AtomText.RenderEffectSummary(new EffectGraphData(fx.DisplayName) { header = fx, steps = fx.Steps }, data);
                    if (string.IsNullOrEmpty(text)) continue;
                    if (isActive && string.IsNullOrEmpty(it.ActiveEffectText))
                    {
                        // 被压（沉默/沉睡拦主动）整行划线灰显；2026-11-06：公式文本退役、费用组=净费
                        it.ActiveEffectText = SuppressedText(text, activeSuppressed);
                        // 2026-11-09 逐色进位：卡侧主动效果费=引擎发动时实扣的整数（与支付终点同 ε）
                        it.ActiveEffectCost = ElementPaymentValidator.CeilBill(
                            CardComposerScreen.ActiveEffectNetCost(fx) ?? fx.AnchorCost);
                        it.ActiveEffectPayloadCost = CardComposerScreen.EffectPayloadCostOf(fx);
                        continue;
                    }
                    (it.PassiveLines ??= new List<string>()).Add(SuppressedText(text, autoSuppressed));
                }
            }
            return it;
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
        /// 速度不在此显示（2026-11-06 起归 effect 行 lbl-skill-speed——见 EffectSpeed），空串=藏区。</summary>
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

        /// <summary>卡类别速度（2026-11-06 定案·单源）：主动效果本身不再带速度——速度只由卡类别决定。
        /// 法术=旧底盘分配 KindSpeedBonusOf（max(0,3−效果槽数)）+ 效果 BaseSpeed 上限（与引擎
        /// GetCardCastSpeed 同口——新组合 BaseSpeed 恒 0，即纯底盘值）；生物=0；
        /// 结界/其余=null（结界效果恒自动，无速度概念=不显示）。</summary>
        public static int? EffectSpeedOf(CardData d)
        {
            if (d == null) return null;
            if (d.Supertype == Cardtype.Spell)
            {
                int max = 0;
                if (d.Effects != null)
                    foreach (var fx in d.Effects)
                        if (fx != null && fx.BaseSpeed > max) max = fx.BaseSpeed;
                return max + CardCompositionCost.KindSpeedBonusOf(d);
            }
            if (d.Supertype == Cardtype.Creature) return 0;
            return null;
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
            void Collect(AtomicEffectEntry a)
            {
                if (a == null || string.IsNullOrEmpty(a.refId)) return;
                var text = AtomText.RenderAtomEntry(a);
                (AtomicEffectTable.GetByHashId(a.refId)?.Polarity < 0f ? selfParts : enemyParts).Add(text);
            }
            //（卡级 AllCostRowPayloads 已随 2026-11-08 代价效果级化整删——代价原子全在效果 Costs）
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
            return string.Join("｜", arms) + (formula == null ? "" : "｜" + formula);
        }
    }

    // ======================================== 净费单源（P4 §1 2026-10-29） ========================================

    /// <summary>卡面净费口径单源（P4 净费显示）：玩家实付=推导费 − 代价抵扣（战斗态另有建造预付/
    /// 费用指示物层有专属展示口——本类只管「推导 − 代价抵扣」两层）。
    /// 与本文件同收口：新文件进不了已生成的 csproj 是老坑（SearchQuery 先例）。
    /// 全部实时推导（CardCostService.GetRuntimeCost / GetPrePayloadCost——Derive 内
    /// PrePayloadCost→代价抵扣（色锁：只扣代价原子可产出色）→DerivedCost，三段自洽由引擎构造保证），
    /// 不读 costList 缓存。</summary>
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

        /// <summary>三段式公式行（P4 §1.1；2026-10-31 色锁口径）：「推导 2红3灰 − 代价抵扣 2（限红）= 净费 3灰」。
        /// 无代价抵扣（实抵 0）→ null（费用区净费即推导费，无差可显）。X/Y/Z 同模式取数自洽。</summary>
        public static string NetCostFormulaOf(CardData card)
        {
            if (card == null) return null;
            int mode = CostDerivationService.HasChoiceEffect(card) ? MaxNetModeOf(card) : 0;
            CardCostService.GetPrePayloadCost(card, mode, out var pre, out var applied);
            if (applied <= 0) return null;
            var net = CostDerivationService.HasChoiceEffect(card)
                ? CardCostService.GetModeCost(card, mode)
                : CardCostService.GetRuntimeCost(card, mode);
            var scope = CardCostService.PayloadScopeZh(card);
            return $"推导 {CostRunes(pre)} − 代价抵扣 {applied}（限{scope}）= 净费 {CostRunes(net)}";
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
