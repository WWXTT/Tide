using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;

namespace CardCore
{
    /// <summary>
    /// 玩家可执行的操作 — 统一入口
    public static partial class GameActions
    {
        // ======================================== 内部方法 ========================================

        /// <summary>
        /// 从卡牌读取费用——出牌预检 / cast 付费 / pending 合计的唯一口径（public：UI/AI 声明期展示与预检同口径）。
        /// 返回 ElementCost 副本（2026-10-04 位置数组化：下标=ManaType 枚举序号）：
        /// 抉择卡（HasChoiceEffect）走 per-mode 推导缓存（CardCostService.GetModeCost——构筑期推导存储，
        /// 发动时只读不重推导）；推导全零=该模式免费（不落 {Gray:1} 默认——那是「无费用数据」的兜底）。
        /// 声明 costList 在装载期写为最大模式费，仅供 UI/排序消费；支付与地牌产元素均按所选模式
        /// （GetModeCost / ElementPool.AddCardToPool modeIndex——2026-09-21 地牌自选模式定案）。
        /// 费用指示物层在此接入（定案：层带颜色，P1 恒灰）：灰色分量 += 费用增加层 − 费用减少层（下限 0），
        /// 逐模式独立套用。层在进入发动区时不清（ZoneContainer.OnCardMoved 发动区豁免——付费发生在发动区内），
        /// 结算离开发动区（入墓/入场）与离手时按真实移动清除。
        /// </summary>
        /// <summary>
        /// 从卡牌获取费用（如有）。
        /// 法术（2026-10-15 深夜三类卡改版）：出牌费不含效果锚价——真实施放账单=卡面费+所选效果锚价，
        /// 本口单源并入（预检 CanAffordCast / cast 结算扣费 / pending 承诺 / AI 动作特征 / UI 费用预览全消费）；
        /// modeIndex=施放时抉择的效果序号（>1 效果弹窗三选一，单效果=0）。
        /// 2026-10-22 恒推导定案：基价恒走 CardCostService 推导（GetRuntimeCost）——存档 costList 仅为
        /// 显示缓存不参与支付；调原子表 BaseCost 后全表即时变价，无「上次推导快照」延迟。
        /// 出牌费用单源（2026-10-31 P12：建造 BuildPaid 重放整链退役——充能进度改挂四色费用降低
        /// 指示物，在下方指示物层统一套算）。层序恒定：恒推导基价 → 法术锚价 → 四色费用指示物层；
        /// 预检/扣费/pending/UI 预览全消费面共用本口，不另开口径。
        /// </summary>
        public static ElementCost GetCardCost(Card card, int modeIndex = 0)
        {
            ElementCost cost;
            var data = (card as CardCore.CardWrapper)?.GetData();
            if (data != null)
                cost = CardCostService.GetRuntimeCost(data, modeIndex); // 恒推导（抉择=所选模式）
            else
                cost = ElementCost.FromValue(ManaType.Gray, 1); // 防御默认：无卡数据按灰1（正常链路不达）

            // 法术锚价并入（改版定案：发动按效果费用支付，无额外费用——见 SpellCastAnchor）。
            // 2026-10-31 代价指向化：法术施放=使用所选主动效果，两层代价净费在此单源落账：
            // ① 所选模式效果的效果级代价行 → 对锚价净费（EffectActivationGate.NetBill——与
            //    生物/结界主动技能发动同算法；定案「主动效果不计入卡费，其代价同理」——不碰基价桶）；
            // ② 施放期代价行（卡级行+非主动效果行）→ Derive 桶对法术不预扣（见 Derive 5b），
            //    在此对「基价+锚价」整笔施放账单色锁补扣（法术施放账单=整笔，不拆基价/锚价容量）。
            if (data != null && data.Supertype == Cardtype.Spell)
            {
                var anchor = SpellCastAnchor(card, modeIndex);
                var modeDefs = GetCastableEffectDefinitions(card);
                var modeDef = modeDefs != null && modeDefs.Count > 0
                    ? modeDefs[Math.Clamp(modeIndex, 0, modeDefs.Count - 1)] : null;
                // 主动模式行才对锚价净费（法术施放=使用所选主动效果）；非主动行经
                // ApplyCastDeduction 施放期补扣——同桶互斥防双计（2026-11-08 效果级化配套）
                var modeCosts = modeDef != null && modeDef.IsActivatedEffect && modeDef.Costs != null
                    ? modeDef.Costs.Where(c => c != null && c.Type != CostType.ElementConsume).ToList()
                    : null;
                if (modeCosts != null && modeCosts.Count > 0)
                    anchor = EffectActivationGate.NetBill(anchor, modeCosts);
                foreach (var color in anchor.NonzeroColors())
                    cost[color] = cost[color] + anchor[color];

                // 施放期代价行（卡级行+非主动效果行）逐行色锁补扣（单个代价=抵扣单位）
                CardCostService.ApplyCastDeduction(cost, data);
            }

            // 费用指示物层（P1 恒灰 → 2026-10-31 P12 四色减扩容 → 同日通用增减四色化；
            // legacy 恒灰档 CostDown 已于 2026-11-02 删除——GetCardCost 不再读）：
            // 灰 += CostUp + CostUpGray − CostDownGray；红/蓝/绿各吃对应色 CostUp* − CostDown* 层
            //（增减同色对冲）。逐色单独钳 0，不串色。
            int costUp = card.GetCounterCount(Attribute.CounterRules.CostUpCounter)
                       + card.GetCounterCount(Attribute.CounterRules.CostUpGrayCounter);
            int grayDown = card.GetCounterCount(Attribute.CounterRules.CostDownGrayCounter);
            int redUp = card.GetCounterCount(Attribute.CounterRules.CostUpRedCounter);
            int blueUp = card.GetCounterCount(Attribute.CounterRules.CostUpBlueCounter);
            int greenUp = card.GetCounterCount(Attribute.CounterRules.CostUpGreenCounter);
            int redDown = card.GetCounterCount(Attribute.CounterRules.CostDownRedCounter);
            int blueDown = card.GetCounterCount(Attribute.CounterRules.CostDownBlueCounter);
            int greenDown = card.GetCounterCount(Attribute.CounterRules.CostDownGreenCounter);
            if (costUp != 0 || grayDown != 0 || redUp != 0 || blueUp != 0 || greenUp != 0
                || redDown != 0 || blueDown != 0 || greenDown != 0)
            {
                cost[ManaType.Gray] = Math.Max(0f, cost[ManaType.Gray] + costUp - grayDown);
                cost[ManaType.Red] = Math.Max(0f, cost[ManaType.Red] + redUp - redDown);
                cost[ManaType.Blue] = Math.Max(0f, cost[ManaType.Blue] + blueUp - blueDown);
                cost[ManaType.Green] = Math.Max(0f, cost[ManaType.Green] + greenUp - greenDown);
            }

            return cost;
        }

        /// <summary>元素池内指定色地牌数（LockedColor 计数，横置与否不计=持有口径，非可用口径；
        /// 同禁召灰地/纳川蓝地/归土绿地）。代价地牌门槛（EvaluatePayloadGate ③）消费。</summary>
        public static int CountLands(GameCore core, Player player, ManaType color)
            => core?.ElementPool?.GetPool(player)?.PooledCards?.Count(pc => pc != null && pc.LockedColor == color) ?? 0;

        /// <summary>
        /// 法术施放锚价（2026-10-15 深夜三类卡改版：法术只挂 1~3 个主动效果，出牌费不含锚价——
        /// 施放时抉择其一，锚价并入 cast 账单现付，无额外费用）。modeIndex=效果序号（GetCardCost 单源消费）。
        /// 与 CardCostService.Derive 同口径。非法术/无效果返回空。
        /// </summary>
        public static ElementCost SpellCastAnchor(Card card, int effectIndex)
        {
            var data = card is CardWrapper wrapper ? wrapper.GetData() : null;
            if (data == null || data.Supertype != Cardtype.Spell) return new ElementCost();
            var defs = GetCastableEffectDefinitions(card); // P12：模式序号索引非充能列表
            if (defs == null || defs.Count == 0) return new ElementCost();
            var def = defs[Math.Clamp(effectIndex, 0, defs.Count - 1)];
            if (def == null) return new ElementCost();
            return CostDerivationService.DeriveElementCosts(def, 0) ?? new ElementCost();
        }

        /// <summary>
        /// 检查是否能支付费用（2026-09-14 统一混付口径；public 供 LegalActionEnumerator 共用防掩码/引擎分叉）：
        /// 1. 门槛：卡总费用不得超过当前地牌槽上限（费用上限 10 由此隐含——自然上限 10（2026-10-15），成长可再抬；按卡判定不累计）
        /// 2. 余量：账单+pending 合并后走账单规划器（同色→灰→黑白；黑白=万用色）；
        ///    pending = 同玩家已声明未结算的整卡施放费用（响应窗口内的支付承诺，一并占用）
        /// </summary>
        public static bool CanAfford(GameCore core, Player player, ElementCost cost)
            => CanAfford(core?.ElementPool, cost, player, GetPendingCastCosts(core, player));

        /// <summary>
        /// 出一张卡的可付性（2026-10-28 P1 定案）：净费可付 ∧ 代价可支付。
        /// 净费=CanAfford（卡费〔已含代价抵扣〕+同玩家 pending 声明承诺统一混付规划）；
        /// 代价可支付=CostPayability.AllPayable（与 EvaluatePayloadGate 同口径，两处单源；
        /// 2026-11-09 补闸：法术所选模式〔主动行〕的 Payload 代价一并验付——结算期执行器现付，
        /// 声明期此前零闸）。
        /// 出牌声明预检（PlayCard/PlayCardInResponse）与 AI 枚举（LegalActionEnumerator）共用。
        /// </summary>
        public static bool CanAffordCast(GameCore core, Player player, Card card, int modeIndex)
        {
            if (core == null || player == null || card == null) return false;
            if (!CanAfford(core.ElementPool, GetCardCost(card, modeIndex), player,
                    GetPendingCastCosts(core, player)))
                return false;

            // 弃牌代价「另有一张」闸（2026-11-03）：本卡自身不算可弃手牌——AI/UI 预检同拒
            if (CheckDiscardCostSelfExclusion(core, player, card, modeIndex) != null) return false;

            // 代价可支付闸（声明期——整卡不可用，非结算期失败）
            var specialCosts = CollectCardSpecialCosts(card);
            var spellModeCosts = CollectSpellModeCosts(card, modeIndex); // 法术主动行代价镜像（2026-11-09）
            if ((specialCosts == null || specialCosts.Count == 0)
                && (spellModeCosts == null || spellModeCosts.Count == 0)) return true;
            var gateCosts = specialCosts ?? new List<CostInstance>();
            if (spellModeCosts != null)
            {
                gateCosts = new List<CostInstance>(gateCosts);
                gateCosts.AddRange(spellModeCosts);
            }
            return CostPayability.AllPayable(gateCosts, new CostContext
            {
                Payer = player,
                ZoneManager = core.ZoneManager,
                ElementPool = core.ElementPool,
                Source = player,
            });
        }

        private static bool CanAfford(ElementPoolSystem elementPool, ElementCost cost, Player player,
            ElementCost pending = null)
        {
            // 合并声明承诺后统一规划（2026-10-11 现取现用）：临时钱包+未横置地牌
            // 合成模型一次性判定（纯预检不动状态——PlanBill 内部先钱包后横置，口径与付费步一致）。
            // 旧「总价 > 地牌槽上限即拒」闸已随 2026-10-16 临时元素超上限改版退役——钱包贡献
            // 不受浓度钳，总价能否覆盖由规划自身判定（地牌贡献仍每货币 ≤ 上限）。
            var bill = ElementPaymentValidator.NormalizeBill(cost);
            foreach (var kv in ElementPaymentValidator.NormalizeBill(pending))
                bill[kv.Key] = bill.TryGetValue(kv.Key, out var v) ? v + kv.Value : kv.Value;
            return elementPool.CanPayCostWithAutoTap(bill, player);
        }

        /// <summary>
        /// 费用拒绝的可读原因（2026-09-26 联机排查）：总额超地牌槽上限（地牌侧浓度封顶——超出
        /// 部分须临时元素垫付）与元素不足分列，供网络 Error 帧直读。
        /// 2026-10-16 口径更新：临时元素可超上限支付——超帽不再是独立拒因，只在钱包也垫不起时提示。
        /// </summary>
        private static string CostRejectText(GameCore core, Player player, ElementCost cost)
        {
            // 支付进1口径（2026-10-30）：逐色向上取整求和（与实付同账）——总额 Ceiling 会把
            // 0.5红+0.5绿 报成 1 点（实付 2 点）
            int total = ElementPaymentValidator.NormalizeBill(cost).Values.Sum();
            int cap = core.ElementPool.GetLandCap(player);
            if (total > cap)
                return $"费用 {CostText(cost)} 共 {total} 点，超过当前地牌槽上限 {cap}——地牌单货币贡献封顶上限，超出部分须临时元素垫付且当前不足（临时元素可超上限：攒钱包即可出）";

            var wallet = core.ElementPool.GetPool(player).TempElements;
            string bankText = string.Join("、", wallet.Where(kv => kv.Value > 0)
                .Select(kv => $"{ManaZh(kv.Key)}{kv.Value}"));
            var untapped = core.ElementPool.GetPool(player).PooledCards.Count(pc => !pc.IsTapped);
            return $"元素不足——需 {CostText(cost)}，临时元素：{(string.IsNullOrEmpty(bankText) ? "空" : bankText)}，"
                + $"未横置地牌 {untapped} 张（现取现用：付费时系统自动横置）"
                + "（代价=构筑期抵扣已并入净费，不产出元素）";
        }

        private static string CostText(ElementCost cost)
        {
            if (cost == null || cost.IsZero) return "0";
            var sb = new System.Text.StringBuilder();
            foreach (var color in cost.NonzeroColors())
                sb.Append(ManaZh(color)).Append(cost[color].ToString("0.#"));
            return sb.ToString();
        }

        private static string ManaZh(ManaType type) => type switch
        {
            ManaType.Red => "红",
            ManaType.Blue => "蓝",
            ManaType.Green => "绿",
            ManaType.Any => "任意",
            _ => "灰",
        };

        /// <summary>
        /// 从卡牌获取效果定义（如有）。
        /// public：网络 intent 以 EffectDefinition.Id 寻址效果（M1 协议），服务器分派用
        /// （此前 LegalActionEnumerator 持有一份私有拷贝——统一走本口防口径漂移）。
        /// </summary>
        public static List<EffectDefinition> GetCardEffectDefinitions(Card card)
        {
            if (card is CardWrapper wrapper)
            {
                var cardData = wrapper.GetData();
                var defs = CardEffectConverter.ConvertAll(cardData.Effects, cardData.ID);
                // 卡型速度加成（2026-10-15 深夜改版：KindSpeedBonusOf 单源）：
                // 法术 = 3−槽数（2026-10-31 P12 回归：1槽=速2/2槽=速1/3槽=速0，动态不落字段）；
                // 结界/生物/地牌 = 0（结界禁主动后速度通道退役）。
                int kindSpeed = CardCompositionCost.KindSpeedBonusOf(cardData);
                if (kindSpeed != 0)
                    foreach (var def in defs)
                        if (def != null) def.BaseSpeed += kindSpeed;
                return defs;
            }
            return new List<EffectDefinition>();
        }

        /// <summary>法术可施放效果定义（序=Effects 声明序）——
        /// 模式序号 modeIndex 索引本列表（SpellCastAnchor/目标收集/结算/VFX 色调全消费面同源；
        /// 与 CostDerivationService.GetCastableEffects(CardData) 同序——转换保序）。
        ///（P12「过滤充能效果」已随 2026-11-05 充能系统动作化改版失效——充能不再是卡上效果。）</summary>
        public static List<EffectDefinition> GetCastableEffectDefinitions(Card card)
            => new List<EffectDefinition>(GetCardEffectDefinitions(card).Where(d => d != null));
    }
}
