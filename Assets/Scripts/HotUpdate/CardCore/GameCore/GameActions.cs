using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;

namespace CardCore
{
    /// <summary>
    /// 玩家可执行的操作 — 统一入口
    /// 供 UI/AI 调用，封装规则校验
    /// </summary>
    public static partial class GameActions
    {
        // ======================================== 地牌操作 ========================================

        /// <summary>
        /// 主阶段：将手牌以可用（未横置）状态作为地牌放入元素池。
        /// 不限张数/次数；张数受**各自**地牌槽上限约束（2026-10-15 定案：全局同步曲线
        /// min(全局回合,10)+成长加值无封顶——每次回合开始双方同步 +1，先后手无差）。
        /// colorChoice（2026-10-11 锁色定案）：从卡费颜色（红/蓝/绿/灰）中自选锁定产色，
        /// null=缺省主色（份额最多色）；后续生成固定，付费时系统按账单自动横置。
        /// </summary>
        public static bool AddToElementPool(GameCore core, Player player, Card card, ManaType? colorChoice = null)
            => AddToElementPool(core, player, card, colorChoice, out _);

        /// <summary>带原因版（2026-11-03 失败提示细化）：池内四闸经 ElementPool.TryAddToPool 单源透传。</summary>
        public static bool AddToElementPool(GameCore core, Player player, Card card, ManaType? colorChoice, out string reason)
        {
            reason = null;
            if (core == null || player == null || card == null) { reason = "内部错误：空参数"; return false; }
            if (core.TurnEngine.TurnPlayer != player) { reason = "现在不是你的回合"; return false; }
            if (core.TurnEngine.CurrentPhase?.Phase != PhaseType.Main)
            { reason = $"放地只能在你的主阶段（当前：{core.TurnEngine.CurrentPhase?.Phase}）"; return false; }

            // 检查卡牌在手中
            var hand = core.ZoneManager.GetCards(player, Zone.Hand);
            if (!hand.Contains(card)) { reason = "卡不在手牌（快照过期？请重试）"; return false; }

            // 教学引导闸（2026-10-06 第二课）：放地步骤语义优先（非引导卡拒绝）
            var guideReject = RuleHooks.TutorialElementGate?.Invoke(player, card);
            if (guideReject != null)
            {
                reason = $"教学引导：{guideReject}";
                TideLog.Warn($"[GameActions] 放地被教学引导拒绝：{guideReject}");
                return false;
            }

            // 放入元素池（各自张数 ≤ 地牌上限；锁色自选/缺省主色）
            var elementPool = core.ElementPool;
            if (!elementPool.TryAddToPool(card, player, colorChoice, out reason))
                return false;

            // 从手牌移到元素池区域，然后发布入池事件（时序定案 2026-09-26：
            // 事件须在区域列表落定后发布——BoardState.AutoResync 等区域派生观察者
            // 立即读到正确占用，棋盘地牌行不滞后）
            core.ZoneManager.MoveCard(card, player, Zone.Hand, Zone.ElementPool);
            elementPool.PublishPoolAdd(card, player);
            RuleHooks.OnTutorialElementPlaced?.Invoke(player, card);

            return true;
        }

        // 手动横置产元素入口已删（2026-10-11 现取现用定案）：地牌横置=付费时系统自动的
        // 强制动作（无速度交互），不再有玩家手动产色操作；产色口收敛到
        // ElementPool.TryPayCostWithAutoTap / TryPayBillWithAutoTap。

        // ======================================== 充能（2026-11-05 系统动作化改版：速度0规则动作；2026-11-06 opt-in：卡面声明才有资格） ========================================

        /// <summary>
        /// 充能可用性单源（2026-11-05 充能系统动作化改版；2026-11-06 opt-in 修订）：UI 置灰 /
        /// AI 枚举 / 引擎终审三步同口径——DeclareCharge 内部先过本口。门禁序：**资格闸（卡面
        /// 声明 chargeAbility 才有——攻/守/充能三系统动作同日统一 opt-in）** → 时点（充能=速度0
        /// 系统动作，**仅己方主阶段空栈首动**——仿攻击宣言；对手回合响应档已随改版退役）→
        /// 卡在手牌（施放中除外）→ **一张卡一回合一次**（StackEngine 充能专账，回合开始清零）
        /// → 有可充颜色（「卡具有的颜色」=代价抵扣前逐色卡费 &gt;0 且未充满——法术补差并入模式 0
        /// 施放锚价；进度=四色费用降低指示物层数，GetPrePayloadCost 逐色）。
        /// **不支付元素**（旧「现付 1 点该色元素」随改版退役，充能=免费落层，节奏闸=一张卡一回合一次）。
        /// 开启充能资格的计价=推导费固定 +1 灰（CardCostService，不动底盘分配）。
        /// </summary>
        public static bool CanCharge(GameCore core, Player player, Card card, out string reason)
        {
            reason = null;
            if (core == null || player == null || card == null) { reason = "内部错误：空参数"; return false; }

            // 资格闸（2026-11-06 充能 opt-in 改版）：卡面声明 chargeAbility 才可宣——放最前，
            // 让无资格卡拿到准确拒因（时点/余量拒因对无资格卡无意义）。
            if (!card.HasChargeAbility())
            { reason = "该卡不具备充能能力"; return false; }

            // 充能=速度0系统动作（2026-11-05 改版，仿攻击宣言）：仅己方主阶段空栈首动——
            // 对手回合响应档（旧 P12 速≥1 法术充能）随改版退役，0 速本就锁死在自家主阶段。
            if (core.TurnEngine.TurnPlayer != player)
            { reason = "充能只能在自己的回合"; return false; }
            if (core.TurnEngine.CurrentPhase?.Phase != PhaseType.Main)
            { reason = $"充能只能在主阶段（当前：{core.TurnEngine.CurrentPhase?.Phase}）"; return false; }
            var engine = core.StackEngine;
            if (engine == null || engine.IsResolving) { reason = "结算进行中不可宣充能"; return false; }
            if (!engine.IsEmpty)
            { reason = "充能=速度0系统动作，须空栈首动（连锁/响应窗口中不可宣）"; return false; }

            var hand = core.ZoneManager.GetCards(player, Zone.Hand);
            if (!hand.Contains(card)) { reason = "充能对象必须在手牌"; return false; }
            if (engine.IsPendingCast(card)) { reason = "该卡施放中（结算未完成）"; return false; }

            // 满色前置（2026-11-05 拍板口径）：「没有可充颜色」先于每回合一档报告——充满后
            // 探针/UI 须见满色拒因（每回合一档在同一张卡刚充过时恒真，放前面会吞掉满色信息）。
            if (ChargeRemainingOf(card).Count == 0)
            { reason = "该卡没有可充颜色（各色已充满或卡费无对应色）"; return false; }

            // 一张卡一回合只能发动一次（2026-11-05 改版保持）：宣入即记账（StackEngine 充能专账），
            // 发动层无效不退账；跨回合随台账清零恢复（进度层本身跨回合保留）。
            if (ChargeUsedThisTurn(core, card))
            { reason = "充能一回合只能发动一次（本回合已充过）"; return false; }
            return true;
        }

        /// <summary>
        /// 充能发动（2026-11-05 系统动作化改版）：把充能作为**速度0系统动作**推入栈（仿攻击宣言）——
        /// 宣言期零支付零选色，对手获响应窗口（标准双 Pass 流）；结算期反问元素类别
        ///（ResolveChargeDeclarationAsync——下拉只列该卡具有且未充满的颜色）→ +1 层同色费用降低
        /// 指示物 → 展示。不横置、不换区——卡留在手牌；入栈失败（速度门）不改状态。
        /// 入栈成功即记一次本回合规额（MarkChargeUsed）。
        /// </summary>
        public static bool DeclareCharge(GameCore core, Player player, Card card, out string reason)
        {
            if (!CanCharge(core, player, card, out reason)) return false;
            if (!core.StackEngine.PushChargeDeclaration(card, player))
            { reason = "充能入栈被拒（速度门——栈上有待响应对象）"; return false; }
            core.StackEngine.MarkChargeUsed(card);
            return true;
        }

        /// <summary>
        /// 充能宣言结算（ResolveStack 的 IsChargeDeclaration 分支消费）：重检手牌 → 反问元素类别
        ///（TargetSelectionService.RequestOneIndexAsync——本地 Select 窗下拉/网络 SelectRequest/
        /// AI·无头自动取首项）→ 选项只列「卡具有且未充满」的颜色 → 确定 +1 层同色费用降低指示物
        /// → 首次充能展示这张卡 → 发布 CardChargedEvent。**不支付元素**（2026-11-05 改版）。
        /// 响应链中卡离手牌/各色充满=空转（不落层不发事件，规额已耗）。
        /// </summary>
        public static async Cysharp.Threading.Tasks.UniTask ResolveChargeDeclarationAsync(EffectInstance declaration)
        {
            var card = declaration?.Source as Card;
            var player = declaration?.Controller;
            var core = GameCore.Instance;
            if (core == null || card == null || player == null) return;

            if (card.GetZone() != Zone.Hand)
            {
                TideLog.Warn($"[Charge] 充能源不在手牌（{card.ID} @ {card.GetZone()}）——空转");
                return;
            }

            var remaining = ChargeRemainingOf(card);
            if (remaining.Count == 0)
            {
                TideLog.Warn($"[Charge] {card.ID} 无可充颜色（响应链中充满？）——空转");
                return;
            }

            // 选项序=余量降序、色序稳定（AI/无头/超时自动取首项=最大余量色，确定性可复播）
            var colors = remaining.OrderByDescending(kv => kv.Value).ThenBy(kv => (int)kv.Key)
                .Select(kv => kv.Key).ToList();
            var labels = new List<string>();
            foreach (var c in colors)
                labels.Add($"充能 {ManaZh(c)}（余{remaining[c]}点 · 充后净费 {CostTextOf(card, c)}）");
            int pick = await TargetSelectionService.RequestOneIndexAsync(
                player, labels, $"充能——为【{(card as IHasName)?.CardName ?? card.ID}】选择元素类别");
            if (pick < 0 || pick >= colors.Count) pick = 0; // 越界兜底（Service 已钳，双保险）
            var color = colors[pick];

            string counterId = Attribute.CounterRules.CostDownCounterFor(color);
            if (counterId == null)
            {
                TideLog.Warn($"[Charge] 非法充能色 {color}——空转");
                return;
            }

            int charged = card.AddCounters(counterId, 1, card);
            if (charged > 0)
                core.PublishEvent(new Attribute.CounterChangedEvent
                {
                    Target = card,
                    CounterType = counterId,
                    Amount = charged,
                    Source = card,
                    AfterNote = $"现有 {card.GetCounterCount(counterId)}",
                });

            // 展示这张卡（信息轴「展示」口径：二值不叠层；2026-11-03 收口 RevealRules.Reveal 单源）
            Attribute.RevealRules.Reveal(card, card);

            core.PublishEvent(new CardChargedEvent
            {
                Player = player,
                Card = card,
                Color = color,
                PaidOnColor = card.GetCounterCount(counterId),
            });
        }

        /// <summary>充能选项与余量单源：可充色 → 剩余额度（上限=代价抵扣前逐色卡费，法术补差并入
        /// 模式 0 施放锚价；进度=同色费用降低指示物层数，Resident 换区清——实例级）。
        /// 「卡具有的颜色」即由此口径派生（该色卡费 &gt; 0 且未充满）。</summary>
        private static Dictionary<ManaType, int> ChargeRemainingOf(Card card, int modeIndex = 0)
        {
            var remaining = new Dictionary<ManaType, int>();
            var data = (card as CardWrapper)?.GetData();
            if (data == null) return remaining;
            CardCostService.GetPrePayloadCost(data, modeIndex, out var preCost, out _);
            if (data.Supertype == Cardtype.Spell)
            {
                var anchor0 = SpellCastAnchor(card, 0);
                foreach (var color in anchor0.NonzeroColors())
                    if (color != ManaType.Any) preCost[color] = preCost[color] + anchor0[color];
            }
            foreach (var color in preCost.NonzeroColors())
            {
                if (color == ManaType.Any) continue;
                string cid = Attribute.CounterRules.CostDownCounterFor(color);
                if (cid == null) continue;
                int cap = (int)Math.Round(preCost[color]);
                if (cap <= 0) continue;
                int charged = card.GetCounterCount(cid);
                if (cap - charged > 0) remaining[color] = cap - charged;
            }
            return remaining;
        }

        /// <summary>充能后净费预览文案（单色）：当前净费该色 −1 下限 0（只读，不动卡状态）。</summary>
        private static string CostTextOf(Card card, ManaType color)
        {
            var net = GetCardCost(card, 0);
            float after = Math.Max(0f, net[color] - 1f);
            return $"{after:0.#}{ManaZh(color)}";
        }

        /// <summary>充能每回合一次读数（2026-11-05 系统动作化改版）：键=卡实例（StackEngine 充能专账，
        /// 回合开始清零）——一张卡一回合一次。记账点=DeclareCharge 入栈成功，发动层无效不退账。</summary>
        public static bool ChargeUsedThisTurn(GameCore core, Card card)
            => core != null && core.StackEngine != null && core.StackEngine.IsChargeUsed(card);

        /// <summary>充能时点窗口（纯时机——不含余量/每回合一次；CanCharge 前段与 UI 置灰同源）：
        /// 充能=速度0系统动作，仅己方主阶段（对手回合响应档已随 2026-11-05 改版退役）。</summary>
        public static bool ChargeTimingOpen(GameCore core, Player player, Card card)
        {
            if (core == null || player == null || card == null) return false;
            if (core.TurnEngine.TurnPlayer != player) return false;
            return core.TurnEngine.CurrentPhase?.Phase == PhaseType.Main;
        }

        /// <summary>手牌卡充能数据口（2026-11-05 系统动作化改版；2026-11-06 opt-in 修订：
        /// 充能资格=卡面 chargeAbility 声明，默认不带——WindowOpen 总闸含资格位）。
        /// 给数据不做画面——战场/手牌怎么画归用户。
        /// 消费面：本地对局=引擎卡直读；联机客户端=快照计数器（CounterEntryDTO 已覆盖四色层）。</summary>
        public sealed class HandCardChargeInfo
        {
            /// <summary>已充层数 [灰,红,蓝,绿,任意]（任意位恒 0；换区清零、跨回合保留）。</summary>
            public int[] ChargedByColor;
            /// <summary>时点窗口总闸：资格位（chargeAbility）+ 己方主阶段 + 手牌 + 本回合未充过
            ///（不含余量——UI 置灰第一层；2026-11-06 opt-in：无资格位恒 false）。</summary>
            public bool WindowOpen;
            /// <summary>当前净费（GetCardCost 同源——已含四色费用降低层与代价抵扣）。</summary>
            public ElementCost NetCostNow;
            /// <summary>可充色 → 还能充几点（代价抵扣前卡费上限口径；0/缺席=充满）。</summary>
            public Dictionary<ManaType, int> RemainingByColor = new Dictionary<ManaType, int>();
            /// <summary>可充色 → 再充 1 点该色后的净费预览（当前净费该色 −1 下限 0——不动卡状态；充满色=当前净费）。</summary>
            public Dictionary<ManaType, ElementCost> NetCostAfterCharge = new Dictionary<ManaType, ElementCost>();
        }

        /// <summary>
        /// 手牌卡充能数据：已充层数 / 可充颜色列表（每色还能充几点）/ 充能后净费预览。
        /// 异常入参 → 空信息（WindowOpen=false、空表）——UI 按总闸隐藏充能入口即可。
        /// modeIndex 仅抉择卡有意义（缺省 0 与上限口径一致）。
        /// </summary>
        public static HandCardChargeInfo GetHandCardChargeInfo(GameCore core, Player player, Card card, int modeIndex = 0)
        {
            var info = new HandCardChargeInfo();
            var data = (card as CardWrapper)?.GetData();
            info.ChargedByColor = new int[ElementCost.Length];
            if (card != null)
            {
                info.ChargedByColor[(int)ManaType.Gray] = card.GetCounterCount(Attribute.CounterRules.CostDownGrayCounter);
                info.ChargedByColor[(int)ManaType.Red] = card.GetCounterCount(Attribute.CounterRules.CostDownRedCounter);
                info.ChargedByColor[(int)ManaType.Blue] = card.GetCounterCount(Attribute.CounterRules.CostDownBlueCounter);
                info.ChargedByColor[(int)ManaType.Green] = card.GetCounterCount(Attribute.CounterRules.CostDownGreenCounter);
            }
            info.NetCostNow = card != null ? GetCardCost(card, modeIndex) : new ElementCost();
            // 时点窗口总闸（2026-11-05 改版；2026-11-06 opt-in 修订）：资格位 + 己方主阶段 +
            // 手牌 + 本回合未充过——充能=速度0系统动作（不含余量——UI 置灰第一层）。
            info.WindowOpen = core != null && player != null && card != null && data != null
                && data.ChargeAbility
                && core.ZoneManager.GetCards(player, Zone.Hand).Contains(card)
                && ChargeTimingOpen(core, player, card)
                && !ChargeUsedThisTurn(core, card);
            if (data == null || !info.WindowOpen) return info;

            var remaining = ChargeRemainingOf(card, modeIndex);
            foreach (var kv in remaining)
            {
                info.RemainingByColor[kv.Key] = kv.Value;
                // 充能后净费预览：当前净费读数已含进度层，手动 −1 该色（下限 0）即可，不动卡状态
                var after = info.NetCostNow.Clone();
                after[kv.Key] = Math.Max(0f, after[kv.Key] - 1f);
                info.NetCostAfterCharge[kv.Key] = after;
            }
            return info;
        }

        /// <summary>
        /// 主阶段：发动英雄技能（2026-10-15 改版：技能=法术，每回合一次，>1 效果时弹窗择定）。
        /// 逻辑全在 HeroSkillSystem.ActivateAsync（守卫+计费+效果）；effectIndex 缺省 0（单效果卡免抉择）。
        /// </summary>
        public static async Cysharp.Threading.Tasks.UniTask<bool> ActivateHeroSkill(GameCore core, Player player, int effectIndex = 0)
            => await HeroSkillSystem.ActivateAsync(core, player, effectIndex);

        /// <summary>带原因版（2026-11-03 失败提示细化）：技能发动全链同步（见 ActivateAsync 尾注），
        /// UI/网络泵线程直调同步核读拒绝文案。</summary>
        public static bool ActivateHeroSkill(GameCore core, Player player, int effectIndex, out string reason)
            => HeroSkillSystem.TryActivateSkill(core, player, effectIndex, out reason);

        /// <summary>
        /// 主阶段：横置带地牌特性的生物产 1 元素（2026-09-13 英雄技能·培育赋予的特性）。
        /// 颜色随该生物费用构成（多色时自动取首个有份额的颜色；无费用→灰）。
        /// </summary>
        public static bool TapCreatureForElement(GameCore core, Player player, Card creature)
        {
            if (core == null || player == null || creature == null) return false;
            if (core.TurnEngine.TurnPlayer != player) return false;
            if (core.TurnEngine.CurrentPhase?.Phase != PhaseType.Main) return false;
            if (!creature.IsAlive || creature.IsTapped()) return false;
            if (!creature.HasKeyword(Attribute.KeywordRules.LandTrait)) return false;
            if (creature.GetController() != player) return false;

            // 教学引导闸（2026-10-06 第二课）：tapcreature 步骤语义优先（非引导生物拒绝）
            var guideReject = RuleHooks.TutorialTapCreatureGate?.Invoke(player, creature);
            if (guideReject != null)
            {
                TideLog.Warn($"[GameActions] 生物产色被教学引导拒绝：{guideReject}");
                return false;
            }

            var data = (creature as CardWrapper)?.GetData();
            var cost = data?.Cost;
            ManaType type = ManaType.Gray;
            if (cost != null && !cost.IsZero)
            {
                foreach (var color in cost.NonzeroColors())
                {
                    type = color; // 序数序首个有份额的颜色（多色取首色）
                    break;
                }
            }

            // 产色落**临时钱包**（任一回合结束清空）；2026-10-28 P3b：全类型累计钳删除，直接入账。
            // 事件走 core.PublishEvent 路由路径（Trigger/Layer 引擎可见——AddMana 直发 EventManager
            // 会绕开，故不换路）。
            creature.Tap();
            core.ElementPool.GetPool(player).TempElements[type] += 1;
            core.PublishEvent(new ElementPoolGainEvent
            {
                Player = player,
                GainedType = type,
                FromCard = creature,
                Amount = 1,
            });
            RuleHooks.OnTutorialCreatureTapped?.Invoke(player, creature);
            return true;
        }

        // 武器主动攻击（AttackWithWeapon）已随武器系统退役删除（2026-10-07）——
        // 角色主动攻击改走标准攻击宣言栈（GameActions.DeclareAttack，Player 攻击者），
        // 攻击力=HeroAttackCounter 弹药读数（攻击/反击结算后烧除，弹药即闸门）。

        /// <summary>
        /// 主阶段：转移装备（2026-09-13 装备系统定案——移到己方空位+修改箭头方向，耐久-1）。
        /// newDirection 为新的箭头 Flags（可多支）；转移后 LinkAuraSystem 缓存失效重算。
        /// </summary>
        public static bool TransferEquipment(GameCore core, Player player, Card equipment,
            CardCore.HexDirection newDirection, int newCellX, int newCellZ)
        {
            if (core == null || player == null || equipment == null) return false;
            if (core.TurnEngine.TurnPlayer != player) return false;
            if (core.TurnEngine.CurrentPhase?.Phase != PhaseType.Main) return false;
            if (!core.ZoneManager.IsCardInZone(equipment, player, Zone.Battlefield)) return false;
            if (!(equipment is CardWrapper w) || !EquipRules.IsEquipment(equipment)) return false;

            // 目标格须为己方战场空位
            if (!GameBoard.LinkAuraSystem.Enabled) return false;
            var occupant = GameBoard.LinkAuraSystem.CardAt?.Invoke(newCellX, newCellZ);
            if (occupant != null) return false; // 须空位

            // 移动（棋盘层）：直接改 CardAt 映射——经 BoardState 的 Resync 或直接操作
            // 简化做法：装备仍在 Zone.Battlefield，棋盘层的格位由 BoardState 自动按容器顺序派生——
            // 真正的"指定格"需要 BoardState.PlaceCard(card, x, z) 类 API；当前用箭头方向修改 + InvalidateCache 近似。
            var data = w.GetData();
            data.ArrowDirections = newDirection;
            GameBoard.LinkAuraSystem.InvalidateCache();

            EquipRules.LoseDurability(core, equipment, 1, "转移");
            core.PublishEvent(new KeywordAppliedEvent
            {
                Target = equipment,
                Keyword = "装备转移",
                Detail = $"转移至 ({newCellX},{newCellZ}) 并修改箭头方向——耐久 -1",
                Source = player,
            });
            return true;
        }

        /// <summary>
        /// 准备阶段推进（**已退役的兼容口**，2026-09-24 回合口径定案）：准备→主阶段由
        /// `TurnEngine.StartNewTurn` 末尾直调 `AdvanceFromStandby` 自动完成，因此正常流程调用本方法时
        /// 相位已是 Main → 直接返回 false。保留签名只为 AI/验证器的防御性调用（万一真处于 Standby 仍可推进）。
        /// intent 103（IntentSkipStandby）仍路由到此，语义=幂等 no-op。
        /// </summary>
        public static bool SkipElementPool(GameCore core, Player player)
        {
            if (core == null || player == null) return false;
            if (core.TurnEngine.TurnPlayer != player) return false;
            if (core.TurnEngine.CurrentPhase?.Phase != PhaseType.Standby) return false;

            core.TurnEngine.AdvanceFromStandby();
            return true;
        }

        // ---- 冻死定位面包屑（验证器置位启用；Logs/EngineCrumb.txt 逐条落盘）----
        public static bool CrumbEnabled;

        public static void Crumb(string msg)
        {
            try { if (CrumbEnabled) System.IO.File.AppendAllText("Logs/EngineCrumb.txt", $"{System.Environment.TickCount} {msg}\n"); } catch { }
        }

    }
}
