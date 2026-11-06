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
    public static class GameActions
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

        // ======================================== 充能（2026-11-05 系统动作化改版：速度0规则动作，任意手牌卡一回合一次） ========================================

        /// <summary>
        /// 充能可用性单源（2026-11-05 充能系统动作化改版）：UI 置灰 / AI 枚举 / 引擎终审三步同口径
        /// ——DeclareCharge 内部先过本口。门禁序：时点（充能=速度0系统动作，**仅己方主阶段空栈首动**
        /// ——仿攻击宣言；对手回合响应档已随改版退役）→ 卡在手牌（施放中除外）→ **一张卡一回合一次**
        ///（StackEngine 充能专账，回合开始清零）→ 有可充颜色（「卡具有的颜色」=代价抵扣前逐色卡费 &gt;0
        /// 且未充满——法术补差并入模式 0 施放锚价；进度=四色费用降低指示物层数，GetPrePayloadCost 逐色）。
        /// **不支付元素**（旧「现付 1 点该色元素」随改版退役，充能=免费落层，节奏闸=一张卡一回合一次）。
        /// </summary>
        public static bool CanCharge(GameCore core, Player player, Card card, out string reason)
        {
            reason = null;
            if (core == null || player == null || card == null) { reason = "内部错误：空参数"; return false; }

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

        /// <summary>手牌卡充能数据口（2026-11-05 系统动作化改版：充能不再要求卡带充能效果——
        /// 任意手牌卡可充，给数据不做画面——战场/手牌怎么画归用户）。
        /// 消费面：本地对局=引擎卡直读；联机客户端=快照计数器（CounterEntryDTO 已覆盖四色层）。</summary>
        public sealed class HandCardChargeInfo
        {
            /// <summary>已充层数 [灰,红,蓝,绿,任意]（任意位恒 0；换区清零、跨回合保留）。</summary>
            public int[] ChargedByColor;
            /// <summary>时点窗口总闸：己方主阶段 + 手牌 + 本回合未充过（不含余量——UI 置灰第一层；
            /// 2026-11-05 改版：充能=速度0系统动作，任意手牌卡一回合一次，无充能效果门槛）。</summary>
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
            // 时点窗口总闸（2026-11-05 改版）：己方主阶段 + 手牌 + 本回合未充过——
            // 充能=速度0系统动作，无充能效果门槛（不含余量——UI 置灰第一层）。
            info.WindowOpen = core != null && player != null && card != null && data != null
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

        // ======================================== 主阶段操作 ========================================

        /// <summary>
        /// 主阶段：打出一张牌（使用时点 = 声明，Option Y 定案）。
        /// 流程：校验（不付费，含永久物满场预检 / CanAfford 预检）→ 来源区 → 发动区
        /// （公开、可被指向——反制指向发动区）→ CardPlayEvent（使用宣言）→
        /// 「此卡被使用」作为整卡施放对象上 StackEngine（对手获得优先权 = 响应窗口）。
        /// 付费与结算移交栈：双方 Pass 后 LIFO 结算，消费点 = ResolveCardCastAsync
        /// （扣费在响应窗口之后；发动无效=没付、效果无效=付了但被否定、费用不退、不回卷——两层无效定案 2026-10-04）。
        /// targets：可选的预选目标（如指向性法术）；为空时由各原子效果按配置自动解析。
        /// fromZone：出牌来源区（默认手牌；Graveyard = IPlaySource「墓地视手牌中使用」路径）。
        /// （2026-11-02 苏醒机制整链删除：awakenCast/awakenWaive 参数退役——灰身材概念已不存在，
        ///  「免灰抵沉睡」失去意义，且与倒计时玩法过近。）
        /// </summary>
        public static bool PlayCard(GameCore core, Player player, Card card, List<Entity> targets = null, Zone fromZone = Zone.Hand, int modeIndex = 0)
            => PlayCard(core, player, card, targets, fromZone, modeIndex, out _);

        /// <summary>同 <see cref="PlayCard(GameCore,Player,Card,List{Entity},Zone,int)"/>，附带精确拒绝原因（网络 Error 帧直读——2026-09-26 联机排查：通用「门禁」文案无法区分 费用/目标/时点）。</summary>
        public static bool PlayCard(GameCore core, Player player, Card card, List<Entity> targets, Zone fromZone, int modeIndex, out string rejectReason)
        {
            rejectReason = null;
            if (core == null || player == null || card == null) { rejectReason = "内部错误：空参数"; return false; }
            if (core.TurnEngine.TurnPlayer != player) { rejectReason = "现在不是你的回合"; return false; }
            if (core.TurnEngine.CurrentPhase?.Phase != PhaseType.Main)
            { rejectReason = $"出牌只能在你的主阶段（当前：{core.TurnEngine.CurrentPhase?.Phase}）"; return false; }

            // 反制卡响应闸（E1-1 2026-11-04 定案）：含反制原子的卡**只能走 PlayCardInResponse**
            //（反制=响应绑定栈顶——主阶段空栈发动无响应对象）。引擎拒绝，非仅 UI 藏按钮。
            if (IsNegateCard(card))
            { rejectReason = "反制必须响应栈顶（连锁中跟谁发动就反制谁）——主阶段不可直接打出"; return false; }

            // 教学引导闸（2026-10-06 第二课）：play 步骤语义优先（非引导卡拒绝）——
            // 只闸玩家手牌打出（墓地视手牌使用属自由段轮转手段，不闸）
            if (fromZone == Zone.Hand)
            {
                var guideReject = RuleHooks.TutorialPlayGate?.Invoke(player, card);
                if (guideReject != null)
                {
                    rejectReason = $"教学引导：{guideReject}";
                    TideLog.Warn($"[GameActions] 出牌被教学引导拒绝：{guideReject}");
                    return false;
                }
            }

            // 同名仪典唯一闸（2026-10-07 定案：异名共存、同名禁止——同名规则光环在场（不分敌我、
            // 不同范围也算同名）时不可再打出；直投/直调路径由 RuleAuraSystem.Activate 拒绝兜底）
            if (RuleAuraSystem.BlocksDuplicatePlay(card))
            { rejectReason = "场上已存在同名场地光环（同名仪典不可重复——载体离场前不能再打出）"; return false; }

            // 检查卡牌在来源区
            var sourceZone = core.ZoneManager.GetCards(player, fromZone);
            if (!sourceZone.Contains(card)) { rejectReason = $"卡不在{fromZone}（快照过期？请重试）"; return false; }

            // 规则扩展点（OCP）：出牌限制（如信息轴锁定）经注册表询问——
            // 命中的限制自带文案（2026-11-03 失败提示细化：禁召/禁魔等逐令可读）
            if (!RuleHooks.TryPlay(core, player, card, fromZone, out var playRestrict))
            { rejectReason = playRestrict ?? "出牌受限（规则锁定）"; return false; }

            // 目标域预检（2026-09-10 定案：运行时无候选不可发动）
            if (!TargetDomainService.HasPlayableTargets(core, player, card, modeIndex))
            { rejectReason = "无合法目标（当前场上没有该卡可选的对象）"; return false; }

            // 出牌两阶段（2026-10-04 定案）：有代价的卡**先算代价**——声明期预选代价目标并验证可执行；
            // 代价无目标 → 卡的发动无效（拒发）。无代价卡照常只过费用门禁。
            if (!TryPrepareCardCostTargets(core, player, card, modeIndex, out var costGateReject))
            { rejectReason = costGateReject; return false; }

            // 规则扩展点（OCP）：非手牌来源须经注册的 IPlaySource 授权（无授权即拒——
            // 2026-10-30 归土改版后墓地出牌退役，当前无注册来源，恒走拒绝臂）
            if (fromZone != Zone.Hand)
            {
                var playSource = RuleHooks.GetPlaySource(fromZone);
                if (playSource == null || !playSource.TryBeginUse(player))
                { rejectReason = "该区域不可作为出牌来源"; return false; }
            }

            bool isSpell = card.IsSpellCard();
            bool isField = card.IsFieldCard();

            // 永久物：战场容量预检（满场不允许发动，SimpleAI 依赖 false 跳过；
            // 「结算时入场满则失败入墓」由 TryMoveToBattlefield 承担）。
            // 场地卡（2026-10-29 P8）不占单位容量——落己方场地格恒可入（旧场地在位=顶替入墓）
            if (!isSpell && !isField && !core.ZoneManager.HasBattlefieldSpace(player))
            { rejectReason = "战场已满（永久物无处入场）"; return false; }

            // 费用预检（不支付——扣费在响应窗口之后的 cast 结算；抉择卡按所选模式取费——先选择再定费用）。
            // 2026-09-14 代价强制：撤销「按最大减免放行」——声明期即按**原价账单**预检，
            // 混付口径与结算一致（同色→灰→黑白，见 ElementPool.CanPayCost）；
            // 同玩家已声明的施放费用一并计入，防同笔 bank 超发——结算付不出只入墓、不回卷；
            // 2026-10-16 恢复旧序（回调 10-11「补偿先付垫本卡」）：代价补偿黑/白不预计算垫本卡
            //——CanAffordCast 不虚拟并入，补偿须本卡发动成功、代价强制执行完成后才入账
            var cost = GetCardCost(card, modeIndex);
            if (!CanAffordCast(core, player, card, modeIndex))
            { rejectReason = CostRejectText(core, player, cost); return false; }

            // ---- 声明（使用时点·S3b 2026-11-04 删发动区）：设控制者 → 重复施放闸 → cast 上栈 → 使用宣言 ----
            // 卡**留在来源区**（手牌/牌库顶）——「施放中」只由栈条目表示；不再 MoveCard 进 Activation、
            // 不再发 Enter 事件；PushCardCast 失败=零移动（卡没动过，无需回退）。
            card.SetController(player);
            card._pendingCastFromZone = fromZone; // 回退锚（2026-10-04 本轮：代价无有效目标时原路退回；S3b 后=Hand/Deck）

            // 重复施放闸（S3b）：卡已在该玩家的栈上施放中（旧防线=来源区 Contains——卡留手牌后天然恒真失效）
            if (core.StackEngine.GetPendingCastCards(player).Contains(card))
            { rejectReason = "该卡已在施放中（连锁上待结算——不可二次声明）"; return false; }

            if (!core.StackEngine.PushCardCast(card, player, targets, modeIndex))
            {
                // 理论不可达（声明预检已过）；S3b 后零移动——直接失败（卡从未动过）
                card._pendingCastFromZone = Zone.None;
                rejectReason = "栈机器拒入（结算中或速度门）";
                return false;
            }

            // 使用宣言（时点=声明，付费前）：OnCardPlayed/OnSpellCast 触发 / 预言验证 / 播报都看这一点
            core.PublishEvent(new CardPlayEvent
            {
                Player = player,
                PlayedCard = card,
                FromZone = fromZone,
                ModeIndex = modeIndex // 抉择：宣言即公开所选模式（对手响应窗口可见）
            });
            RuleHooks.OnTutorialCardPlayed?.Invoke(player, card);

            return true;
        }

        /// <summary>
        /// 响应时点出牌（自由时点）：非回合玩家在优先权响应窗口内打出一张牌
        /// （打落/发动无效 等反制——目标指向发动区中被使用的卡）。
        /// 与 PlayCard 同构（声明 → 发动区 → cast 上栈 → 付费延迟到 cast 结算），
        /// 门禁差异：不要求回合玩家/主阶段，改为 持有优先权 + 栈上有待响应对象；来源限手牌。
        /// </summary>
        public static bool PlayCardInResponse(GameCore core, Player player, Card card, List<Entity> targets = null, int modeIndex = 0)
            => PlayCardInResponse(core, player, card, targets, modeIndex, out _);

        /// <summary>同 <see cref="PlayCardInResponse(GameCore,Player,Card,List{Entity},int)"/>，附带精确拒绝原因。</summary>
        public static bool PlayCardInResponse(GameCore core, Player player, Card card, List<Entity> targets, int modeIndex, out string rejectReason)
        {
            rejectReason = null;
            if (core == null || player == null || card == null) { rejectReason = "内部错误：空参数"; return false; }
            if (core.StackEngine.IsEmpty || core.StackEngine.IsResolving)
            { rejectReason = "响应窗口已关闭（无可响应对象或结算中）"; return false; }
            if (core.StackEngine.CurrentPriorityHolder != player)
            { rejectReason = "响应出牌需要优先权在手"; return false; }                   // 优先权在手

            var hand = core.ZoneManager.GetCards(player, Zone.Hand);
            if (!hand.Contains(card)) { rejectReason = "卡不在手牌（快照过期？请重试）"; return false; }

            // 反制卡栈顶校验（E1-1）：反制卡只能在此口打出——且**当前栈顶必须可反制**
            //（非系统条目、未标 ActivationNegated；SBA/阶段窗/攻击·守卫宣言拒绝）。
            if (IsNegateCard(card))
            {
                var top = core.StackEngine.PeekStack();
                bool topNegatable = top != null && !top.IsSBA && !top.IsPhaseWindow
                                    && !top.IsAttackDeclaration && !top.IsGuardDeclaration
                                    && !top.IsActivationNegated;
                if (!topNegatable)
                { rejectReason = "反制必须响应栈顶（当前栈顶不可反制——系统条目或已被无效）"; return false; }
            }

            // 规则扩展点（OCP）：出牌限制经注册表询问（响应出牌同样受限，如信息轴锁定）
            if (!RuleHooks.CanPlay(core, player, card, Zone.Hand))
            { rejectReason = "出牌受限（规则锁定）"; return false; }

            // 同名仪典唯一闸（同 PlayCard——响应出牌同门，2026-10-07 唯一性定案）
            if (RuleAuraSystem.BlocksDuplicatePlay(card))
            { rejectReason = "场上已存在同名场地光环（同名仪典不可重复——载体离场前不能再打出）"; return false; }

            // 出牌两阶段（2026-10-04 定案）：有代价先算代价——响应出牌同门（代价无目标→拒发）
            if (!TryPrepareCardCostTargets(core, player, card, modeIndex, out var costGateReject))
            { rejectReason = costGateReject; return false; }

            bool isSpell = card.IsSpellCard();
            // 场地卡（2026-10-29 P8）：不占单位容量——落己方场地格恒可入（同 PlayCard 门禁口径）
            if (!isSpell && !card.IsFieldCard() && !core.ZoneManager.HasBattlefieldSpace(player))
            { rejectReason = "战场已满（永久物无处入场）"; return false; }

            // 费用预检（含本玩家已声明的施放承诺；不支付——cast 结算时才扣；抉择按所选模式；
            // 2026-09-14 代价强制：原价账单预检，混付口径同 PlayCard；
            // 2026-10-16 恢复旧序：同 PlayCard 走 CanAffordCast——代价补偿黑/白不预计算垫本卡）
            var cost = GetCardCost(card, modeIndex);
            if (!CanAffordCast(core, player, card, modeIndex))
            { rejectReason = CostRejectText(core, player, cost); return false; }

            card.SetController(player);
            card._pendingCastFromZone = Zone.Hand; // 回退锚（S3b 后卡留手牌——回退=零移动）

            // 重复施放闸（S3b）：卡已在栈上施放中（hand.Contains 恒真失效后的新防线）
            if (core.StackEngine.GetPendingCastCards(player).Contains(card))
            { rejectReason = "该卡已在施放中（连锁上待结算——不可二次声明）"; return false; }

            if (!core.StackEngine.PushCardCast(card, player, targets, modeIndex))
            {
                // S3b：零移动回退（卡留手牌，Push 失败不动卡）
                rejectReason = "速度门拒绝（响应卡速度须高于记速器）";
                return false;
            }

            core.PublishEvent(new CardPlayEvent
            {
                Player = player,
                PlayedCard = card,
                FromZone = Zone.Hand,
                ModeIndex = modeIndex,
                IsResponseCast = true // 响应窗出牌（2026-11-03 连锁日志）：渲染为「连锁了」
            });

            return true;
        }

        /// <summary>
        /// 主阶段：将牌库顶的卡当手牌使用（2026-10-29 阶段3b，TopDeckAsHandAura 光环授权——
        /// TopDeckPlaySource/IPlaySource 放行、不限额；声明失败回退回牌库**顶**原位）。
        /// 打出后牌库顶自动前移（换新虚拟手牌）；结算/入墓/入场照常。
        /// （旧 PlayCardFromGraveyard 墓地出牌口已随 2026-10-30 归土改版整删——
        /// 归土不再授权墓地施放，IPlaySource 墓地来源无注册即恒拒。）
        /// </summary>
        public static bool PlayCardFromDeckTop(GameCore core, Player player, Card card, List<Entity> targets = null, int modeIndex = 0)
            => PlayCard(core, player, card, targets, Zone.Deck, modeIndex);

        /// <summary>反制卡判定（E1-1 2026-11-04 定案）：卡的任意可施放效果含 NegateActivation/NegateEffect
        /// 原子——此类卡只能走 PlayCardInResponse（响应绑定栈顶，主阶段 PlayCard 拒绝）。
        /// 与 CardEffectConverter.ContainsAtom 同构的浅扫（Effects+Steps 首层；奖励原子不扫——
        /// 反制作为主效果出现，混入奖励槽的形态由装载校验另行拦截）。</summary>
        internal static bool IsNegateCard(Card card)
        {
            var data = (card as CardWrapper)?.GetData();
            if (data?.Effects == null) return false;
            foreach (var fx in data.Effects)
            {
                if (fx?.AtomicEffects == null) continue;
                foreach (var atom in fx.AtomicEffects)
                    if (atom != null && (atom.refId == "c3d65a3d" || atom.refId == "f1cf5ce6"))
                        return true;
                if (fx.Steps == null) continue;
                foreach (var st in fx.Steps)
                    if (st?.atomic != null && (st.atomic.refId == "c3d65a3d" || st.atomic.refId == "f1cf5ce6"))
                        return true;
            }
            return false;
        }

        // ======================================== 整卡施放结算（使用时点消费点） ========================================

        /// <summary>
        /// 整卡施放的栈结算——「此卡被使用」的消费点（Option Y 定案；E1-1 S3a 2026-11-04 判据翻转）：
        /// 1. cast.IsActivationNegated（被「发动无效」打标）→ 中止：不付费、不结算，**送墓收口到此**
        ///    （原「查卡在不在发动区」区位判据+handler 物理送墓改为 cast 标记——卡不再被 handler 挪走）；
        /// 2. 付费（扣费在响应窗口之后，从 bank 扣）；
        /// 3. cast.IsEffectNegated（被「效果无效」打标）→ 移入墓地、跳过效果、费用不退（效果层无效达成）；
        /// 4. 否则结算卡效果并离开发动区（法术入墓 / 永久物入场）。
        /// 支付失败（窗口内其他支付挤占 bank，不回卷）→ 发动失败入墓。
        /// 由 StackEngine 结算整卡施放对象时调用（PlayCard / PlayCardInResponse 声明的消费端）。
        /// </summary>
        internal static async Cysharp.Threading.Tasks.UniTask ResolveCardCastAsync(EffectInstance cast)
        {
            var core = GameCore.Instance;
            var card = cast?.Source as Card;
            var player = cast?.Controller;
            if (core == null || card == null || player == null) return;

            // 1. 发动无效中止（S3a 判据翻转：cast 标记替代区位查询）：不付费、不结算；
            //    送墓/失败事件收口到消费点（原 handler 物理送墓退役——15/16 候选同步剔除已标 cast）
            if (cast.IsActivationNegated)
            {
                CastAbortToGraveyard(core, card, player, "发动无效");
                return;
            }

            // 2. 付费（响应窗口之后）——抉择卡按声明期选定的模式付费（cast.ModeIndex）。
            // 顺序（2026-10-28 P1 定案：代价=构筑期抵扣、不再产出元素）：
            // ① 代价目标存活校验——预选目标响应窗口内全灭/离场/不可指定 → 发动失败，
            //    卡回退声明来源区（手牌路径回手牌），不付费、不执行；
            // ② 先扣卡费（净费=推导费−代价抵扣，已并入 GetCardCost）——现取现用
            //    （钱包优先消耗+系统自动横置地牌补足）；付不出 → 入墓不回卷；
            // ③ 代价强制执行（只执行、不发放）。
            // 被「效果无效」只跳效果、已付代价不回卷；被「发动无效」则全免（上方 1 已拦）。
            var specialCosts = CollectCardSpecialCosts(card);
            var cost = GetCardCost(card, cast.ModeIndex);
            var preselected = card._pendingCostTargets;
            card._pendingCostTargets = null; // 消费点即清空（防跨次使用泄漏；付费步一次性）

            // ① 代价目标存活校验：有域代价的预选目标全部失效 → 发动失败回退声明来源区
            if (PayloadTargetsAllLost(core, player, card, specialCosts, preselected))
            {
                var fromZone = card._pendingCastFromZone;
                card._pendingCastFromZone = Zone.None;
                CastAbortToOrigin(core, card, player, fromZone,
                    "代价无有效目标——发动失败回退（走不到代价黑白元素生成）");
                return;
            }

            // ② 先扣卡费（不预计将生成的黑白——本卡费用以现有钱包+地牌结算）
            if (!core.ElementPool.TryPayCostWithAutoTap(cost, player, $"打出:{(card as IHasName)?.CardName}"))
            {
                CastAbortToGraveyard(core, card, player, "费用不足（响应窗口后支付失败，不回卷——代价未执行）");
                return;
            }

            // ③ 代价强制执行（2026-10-28 P1 定案：只执行、不发放——黑白补偿经济退役，
            //    代价价值已在构筑期抵扣〔CardCostService P 行〕）
            var costCtx = new CostContext
            {
                Payer = player,
                ZoneManager = core.ZoneManager,
                ElementPool = core.ElementPool,
                Source = player, // 来源=角色（来源归因定案 2026-09-09）
                PreselectedCostTargets = preselected, // 出牌两阶段（2026-10-04）：声明期预选代价目标沿用
            };
            if (!await CostCompensationService.PayPayloadsAsync(specialCosts, costCtx))
            {
                CastAbortToGraveyard(core, card, player, "代价流程异常（付费步失败，不回卷）");
                return;
            }

            // 3. 效果无效（S3a：cast.IsEffectNegated 替代 card._isNegated——卡字段 cast 用法退役）：
            // 付了但被否定 → 入墓、跳过效果、费用不退。
            // 发动表演照播（2026-10-20 用户定案：效果无效≠发动无效——费已付，弹体照飞到预选目标；
            // 效果不结算=无伤害/死亡事件，表现层仅飞行+到达）。发动无效在上方早退：不付费不结算，
            // 什么都不播（双重标记场景由次序兜底：ActivationNegated 先查——IsEffectNegated 永不达）。
            if (cast.IsEffectNegated)
            {
                if (cast.Targets != null && cast.Targets.Count > 0)
                {
                    var fizzTint = FirstAtomTint(card, cast.ModeIndex);
                    foreach (var t in cast.Targets)
                        if (t != null)
                            core.PublishEvent(new EffectFlightEvent
                            {
                                Source = player, CastCard = card, Target = t, Tint = fizzTint,
                                Note = "效果无效（发动照常，效果未结算）"
                            });
                }
                CastAbortToGraveyard(core, card, player, "效果无效");
                return;
            }

            // 附加诅咒/附加祝福部署（BranchEngines.OnCardCastResolved）：施放结算时投放指示物+登记载荷；
            // 无效已在上游拦截（效果不结算）。手牌序位/效果序位引擎均已退役（后者 2026-10-20 换轴为团结——评估走 OnCardCastSettled，不走此口）。
            BranchEngines.OnCardCastResolved(card, player);

            // 4. 结算：法术 → 效果全结算后离区入墓；永久物 → 登记触发式 + 入场
            if (card.IsSpellCard())
            {
                await ResolveSpellEffectsAsync(core, player, card, cast.Targets, cast.ModeIndex, cast);
            }
            else
            {
                ResolvePermanentEntry(core, player, card);
            }

            // 成功路径复位回退锚（S3b 收口 2026-11-05）：cast 结算完成（入墓/入场已发生），
            // _pendingCastFromZone 使命结束——残留会让下一轮（同卡实例复用/验证器夹具）读到过期来源。
            card._pendingCastFromZone = Zone.None;

            // 元素充盈引擎判定（2026-09-22 定案；2026-10-20 判据五改）：施放结算完成后评估——
            // 仅对手回合（施放者==当前回合玩家）时评估施放者对手的引擎：双方**剩余元素总量**
            // （未横置地牌+钱包）之差（封顶 6）恰==x 即发奖（差每+1踩一档各一次）；奖励恒直发。
            // 发动无效/效果无效早退不评估；效果费支付不走此口。
            BranchEngines.OnCardCastSettled(player);
        }

        /// <summary>施放卡声明期收集（效果无效 fizz 场景的飞行色）：所选模式首个原子的行基准主色。</summary>
        private static ManaType? FirstAtomTint(Card card, int modeIndex)
        {
            var defs = GetCastableEffectDefinitions(card); // P12：模式序号索引非充能列表
            if (defs == null || defs.Count == 0) return null;
            var chosen = defs[Math.Clamp(modeIndex, 0, defs.Count - 1)];
            if (chosen?.Effects == null) return null;
            foreach (var a in chosen.Effects)
                if (a != null)
                    return a.AnchorTint();
            return null;
        }

        /// <summary>cast 结算中止（支付失败 / 发动无效）：**来源区**入墓（S3b 后卡在手牌/牌库顶）
        /// + 失败事件（FromZone=真实来源）。Leave 事件随区退役不再发。</summary>
        private static void CastAbortToGraveyard(GameCore core, Card card, Player player, string reason)
        {
            var fromZone = card.GetZone(); // S3b：卡留来源区——入墓从真实所在区起跳
            // 同区幂等（2026-11-05 收口）：中止前卡已被其它效果送墓 → 只发失败事件，不再移动
            if (fromZone != Zone.Graveyard)
                core.ZoneManager.MoveCard(card, player, fromZone, Zone.Graveyard);
            core.PublishEvent(new CardActivationFailedEvent
            {
                Card = card,
                Controller = player,
                FromZone = fromZone,
                Reason = reason
            });
        }

        /// <summary>
        /// cast 结算中止·回退声明来源区（2026-10-04 本轮定案；**S3b 2026-11-04 删发动区**：
        /// 卡从未离开来源区——回退=零移动 + 清回退锚。牌库顶来源同理（卡留在牌库顶原位）。
        /// </summary>
        private static void CastReturnToOrigin(GameCore core, Card card, Player player, Zone fromZone)
        {
            // S3b：声明不移动卡——回退无操作（保留签名以最小化调用面改动）
        }

        private static void CastAbortToOrigin(GameCore core, Card card, Player player, Zone fromZone, string reason)
        {
            // S3b：卡从未离开来源区——零移动；仅发失败事件（FromZone=真实来源）
            var realFrom = card.GetZone();
            core.PublishEvent(new CardActivationFailedEvent
            {
                Card = card,
                Controller = player,
                FromZone = realFrom,
                Reason = reason
            });
        }

        /// <summary>
        /// 结算期代价目标存活校验（2026-10-04 本轮定案；2026-10-16 组装效果代价逐原子化）：
        /// 有域代价原子的预选目标经响应窗口后全部失效（死亡/离场/不再满足过滤）→ true（发动失败回退）。
        /// 口径：按**支付者视角**逐原子重解析当前合法域（与声明期 EvaluatePayloadGate 同口径的纯读——
        /// 不重选，只验证预选目标是否仍有任一留在域内；手牌域 {5} 等非战场域同样正确）。
        /// 任一有域原子域空/预选全灭 → true。无代价/无域原子 → 不参与判定。
        /// </summary>
        private static bool PayloadTargetsAllLost(GameCore core, Player player, Card card,
            List<CostInstance> specialCosts, List<List<Entity>> preselected)
        {
            if (preselected == null) return false; // 无代价（无域代价为空表，由域判分支兜底）
            CostInstance payloadCost = null;
            foreach (var c in specialCosts)
                if (c != null && c.Type == CostType.Payload && c.Payloads != null && c.Payloads.Count > 0) { payloadCost = c; break; }
            if (payloadCost == null) return false;

            var resolveCtx = new EffectExecutionContext
            {
                Controller = player,
                Source = player,
                ZoneManager = core.ZoneManager,
                ElementPool = core.ElementPool,
            };
            for (int i = 0; i < payloadCost.Payloads.Count; i++)
            {
                var atom = payloadCost.Payloads[i];
                if (atom == null || atom.TargetKinds == null || atom.TargetKinds.Count == 0) continue; // 无域原子：无目标语义照常执行
                var current = Attribute.EffectHandlerRegistry.ResolveCandidates(atom.TargetKinds, atom.Filter, resolveCtx, atom.Axis);
                if (current != null) current.RemoveAll(e => ReferenceEquals(e, card)); // 本卡施放中仍留来源区（S3b）：不作自己的代价目标
                if (current == null || current.Count == 0) return true; // 该原子域已空：必然全灭
                // 单位域代价（2026-10-31 定案）：声明期不预选（该位恒 null）——无预选目标可验；
                // 域空（连一个可指定单位都没有，域非空检查已在上方）才判全灭（发动失败回退原区）。
                if (CostPayability.IsUnitDomainCost(atom))
                    continue;
                var picked = i < preselected.Count ? preselected[i] : null;
                if (picked == null) return true; // 预选缺该原子位——防御性判全灭
                bool anyAlive = false;
                foreach (var e in picked)
                    if (e != null && e.IsAlive && current.Contains(e)) { anyAlive = true; break; }
                if (!anyAlive) return true; // 该原子预选目标全灭
            }
            return false;
        }

        /// <summary>
        /// 永久物（生物等）cast 结算：经发动区入场。
        /// （触发式注册已收口到 TryMoveToBattlefield/TryAddToBattlefield 统一出口——
        /// 时点接线定案：任何来源进场的卡都注册自身触发式，入场事件发布前完成，
        /// 保证入场卡自己的 OnPlay/OnSummon 能吃到自己的入场事件。）
        /// （2026-11-02 苏醒机制整链删除：sleepLayers 入场代价沉睡档随之退役——
        ///  沉睡指示物仍由 Sleep 原子等常规路径施加。）
        /// </summary>
        private static void ResolvePermanentEntry(GameCore core, Player player, Card card)
        {
            // 发动通过 → 入场（声明期预检已过；满则入墓的兜底在 helper 内）。
            // S3b：卡在**来源区**（手牌/牌库顶）——入场起点=真实所在区；EnterSource 显式 CastPlayed
            //（旧口径 fromZone==Activation 派生已废——卡不在 Activation）。
            if (!core.ZoneManager.TryMoveToBattlefield(card, player, card.GetZone(), EnterSource.CastPlayed))
                return; // 入场失败（满场兜底入墓）
        }

        /// <summary>
        /// 注册一张卡的触发式效果（卡牌效果 + 关键词触发效果）到触发引擎——
        /// 时点接线定案的统一注册出口，由 TryMoveToBattlefield/TryAddToBattlefield
        /// 在入场成功后、入场事件发布前调用（RegisterEffect 自带幂等去重）。
        /// Zones 层 helper 不直接摸 TriggerEngine，只经此窄接口。
        /// </summary>
        public static void RegisterCardTriggeredEffects(GameCore core, Card card, Player player)
        {
            if (core == null || card == null || player == null) return;

            var cardEffects = new List<EffectDefinition>();
            var cardDataEffects = GetCardEffectDefinitions(card);
            if (cardDataEffects != null)
                cardEffects.AddRange(cardDataEffects);

            var keywordEffects = KeywordEffectMapper.CreateAllTriggeredEffects(card);
            if (keywordEffects != null)
                cardEffects.AddRange(keywordEffects);

            foreach (var effect in cardEffects)
                core.TriggerEngine.RegisterEffect(effect, card, player);
        }

        /// <summary>
        /// 玩家已声明、尚在栈上的整卡施放费用合计（cast 支付承诺）。
        /// 声明期费用预检用它防同笔 bank 超发——出多张牌时按「已声明未结算」累计。
        /// </summary>
        public static ElementCost GetPendingCastCosts(GameCore core, Player player)
        {
            var sum = new ElementCost();
            if (core?.StackEngine == null || player == null) return sum;

            foreach (var obj in core.StackEngine.GetStackContents())
            {
                if (obj == null || !obj.IsCardCast || obj.Controller != player) continue;
                if (!(obj.Source is Card castCard)) continue;

                // 抉择按各自声明的模式计承诺
                var cost = GetCardCost(castCard, obj.ModeIndex);
                sum.Add(cost);
            }
            return sum;
        }

        /// <summary>
        /// 排干栈：双 Pass 直到栈空（无头 / AI / UI 快进驱动用——对手无响应即自动结算）。
        /// **空栈上的待发触发一并排**（2026-09-09 修正）：真实对局由 GameLoopController 帧循环
        /// 泵 PutTriggersOnStack，无游戏循环场景（编辑器验证/headless）此前无人推——
        /// 栈空即返回导致「结算外入场的触发式」（如直接落场的 OnPlay）永远不被结算。
        /// 结算含异步原子效果时，本方法返回后结算链可能仍在后台推进
        /// （IsResolving 拦重入，先例：SimpleAI.SettleStack）。
        /// </summary>
        public static bool DrainStack(GameCore core, int maxAttempts = 32)
        {
            if (core == null) return false;

            bool any = false;
            for (int i = 0; i < maxAttempts && (!core.StackEngine.IsEmpty || core.StackEngine.HasPendingEffects); i++)
            {
                Crumb($"drain iter {i} stack={core.StackEngine.StackSize} pend={core.StackEngine.HasPendingEffects} holder={core.StackEngine.CurrentPriorityHolder?.Name}");
                if (core.StackEngine.IsEmpty && core.StackEngine.HasPendingEffects)
                    core.StackEngine.ProcessPendingEffects(); // 待发上栈（结算外排队的触发式）
                var holder = core.StackEngine.CurrentPriorityHolder;
                if (holder == null) break;
                if (!PassPriority(core, holder)) break;
                any = true;
            }
            Crumb($"drain exit stack={core.StackEngine.StackSize} pend={core.StackEngine.HasPendingEffects}");
            return any;
        }

        // ---- 冻死定位面包屑（验证器置位启用；Logs/EngineCrumb.txt 逐条落盘）----
        public static bool CrumbEnabled;

        public static void Crumb(string msg)
        {
            try { if (CrumbEnabled) System.IO.File.AppendAllText("Logs/EngineCrumb.txt", $"{System.Environment.TickCount} {msg}\n"); } catch { }
        }

        // ======================================== 响应窗口（2026-09-16 战斗接入栈机器定案） ========================================

        /// <summary>
        /// 收集响应窗口候选（发动弹窗条目）：速度门 + **双过滤**（元素费用 CanAfford 含 pending 承诺
        /// + 特殊代价 executor.CanActivate 内含 CostHandlerRegistry）——付不起的不进弹窗。
        /// 候选=0 → 调用方直接 Pass（跳过弹窗）。候选=守卫能力（速度1响应）+ 待发自愿效果 +
        /// 主动能力（回合方主阶段）+ 手牌响应卡（2026-10-16 二期窗口 UI 补接，见 ③）。
        /// </summary>
        public static List<ResponseOption> CollectAvailableResponses(GameCore core, Player p)
        {
            var options = new List<ResponseOption>();
            if (core?.StackEngine == null || p == null || core.IsGameOver) return options;
            var engine = core.StackEngine;

            // ① 守卫能力（速度1响应）：栈上有对方的攻击宣言 → 己方未横置守卫生物
            foreach (var obj in engine.GetStackContents())
            {
                if (obj == null || !obj.IsAttackDeclaration || obj.Controller == p) continue;
                if (!engine.SpeedCounter.CanActivate(StackEngine.GuardStackSpeed, EffectActivationType.Voluntary,
                        engine.IsEmpty)) continue;
                foreach (var g in core.ZoneManager.GetCards(p, Zone.Battlefield) ?? new List<Card>())
                {
                    if (g == null || !g.IsAlive || g.IsTapped() || !g.HasGuardAbility()) continue;
                    options.Add(new ResponseOption
                    {
                        Kind = ResponseOption.ResponseKind.GuardAbility,
                        SourceCard = g,
                        AttackInstance = obj,
                        Label = $"{EffectText.Name(g)} 守卫拦截（速度1·结算期横置）",
                        CostSummary = "横置",
                    });
                }
                // 角色（玩家）守卫候选（2026-10-16 战场UI三键改版定案：角色有攻击弹药时可攻可守）：
                // 守卫=把攻击目标改写为角色，双向结算吃战斗并反伤（反伤力=弹药层数）；
                // 反伤步结算后弹药全烧（ResolvePair 既有 Player 目标路径，与被打同口径）
                // 禁守令（2026-10-29）：受禁守光环影响的一方角色不进守卫候选
                if (p.GetPower() > 0 && !RuleAuraSystem.ScopeHits(RuleAuraComponents.NoGuardAura, p))
                    options.Add(new ResponseOption
                    {
                        Kind = ResponseOption.ResponseKind.GuardAbility,
                        SourcePlayer = p,
                        AttackInstance = obj,
                        Label = $"{p.Name} 角色守卫（反伤 {p.GetPower()}）",
                        CostSummary = "承受战斗",
                    });
                break; // 响应最先宣言的攻击（LIFO 顶层由后续窗口轮次覆盖）
            }

            // 教学引导闸（2026-10-06）：守卫候选过滤（此刻 options 只含守卫项，自愿效果在下方追加）
            if (options.Count > 0 && RuleHooks.TutorialGuardFilter != null)
                options = RuleHooks.TutorialGuardFilter(p, options) ?? options;

            // ② 主动效果 + 待发自愿效果（2026-10-17 连锁门禁收敛定案：连锁资格唯一条件=速度门）。
            //    主动效果此前只经待发队列瞬态（从不入队=从不进窗），现按场上来源枚举：
            //    战场+技能栏卡的 IsActivatedEffect 定义（含 Activate_Response 响应档——构筑
            //    体系注释本就写明「归响应窗口」，本次接上）。逐效果：PendingEffect.Create
            //    单源算速（BaseSpeed）→ 速度门 →
            //    executor.CanActivate 可发动性全检（结界禁主动/横置/沉默/沉睡/条件/费用/目标域
            //    ——这些是「可发动性」过滤，不是连锁条件）。阶段走 TurnEngine 真值
            //    （StackEngine.CurrentPhase 是死字段勿读）。
            var executor = engine.GetExecutor();
            var phase = core.TurnEngine.CurrentPhase?.Phase ?? PhaseType.Standby;
            foreach (var src in EnumerateActivationSources(core, p))
            {
                foreach (var def in GetCardEffectDefinitions(src))
                {
                    if (def == null || !def.IsActivatedEffect) continue;
                    var pending = PendingEffect.Create(def, src, p, engine.ActivePlayer, phase);
                    // 2026-11-01 统一口径（与③手牌整卡/PushCardCast/TryActivateEffect 同款）：
                    // 空栈首动免门槛（0 速主动效果主阶段照常）；非空栈=速≥1 且 速≥记速器
                    //（同速可接）——旧 CanActivate 全严格大于把 1 速主动效果接 1 速记速器判死，
                    // 与手牌整卡口径不一致。条件发动（非 Voluntary）不走速度（原口径保持）。
                    if (pending.ActivationType == EffectActivationType.Voluntary)
                    {
                        if (engine.IsEmpty)
                        {
                            if (engine.SpeedCounter.IsResolving) continue; // 空栈首动仍须非结算中（原 CanActivate 口径）
                        }
                        else if (pending.ActivationSpeed < 1
                                 || pending.ActivationSpeed < engine.SpeedCounter.CurrentSpeed)
                        {
                            continue; // 0 速锁自家主阶段；同速可接、更慢不行
                        }
                    }
                    if (!executor.CanActivate(def, src, p, engine.ActivePlayer, phase,
                            core.TurnEngine.TurnNumber)) continue;
                    options.Add(new ResponseOption
                    {
                        Kind = ResponseOption.ResponseKind.ActivatedAbility,
                        SourceCard = src,
                        Definition = def,
                        Label = $"{EffectText.Name(src)} 的 {def.DisplayName}（速度{pending.ActivationSpeed}）",
                    });
                }
            }
            // 待发自愿队列（触发式「可以发动」，速度门已在 GetVoluntaryEffects 内过）：
            // IsActivatedEffect 项跳过——主动效果统一走上方位枚举，防重复候选；
            // 控制器过滤（2026-10-20）：待发桶双方共享——只列决策方自己的待发，对方的
            // BRANCH_REWARD 不是本窗口可发动项
            foreach (var pe in engine.GetActivatableVoluntaryEffects(p))
            {
                var def = pe.Effect;
                if (def == null || def.IsActivatedEffect) continue;
                if (pe.Controller != p) continue;
                options.Add(new ResponseOption
                {
                    Kind = ResponseOption.ResponseKind.VoluntaryEffect,
                    Pending = pe,
                    SourceCard = pe.Source as Card,
                    Definition = def,
                    Label = $"{(pe.Source != null ? EffectText.Name(pe.Source) + " 的 " : "")}{def.DisplayName}",
                });
            }

            // ③ 手牌响应卡（2026-10-16 二期窗口 UI 补接；2026-10-18 速度门全严格大于定案）：
            //    连锁资格唯一条件=速度门（SpeedCounter.CanActivate：严格 > 记速器，同速连锁
            //    一律禁止——与 PlayCardInResponse/PushCardCast 同一裁决；本窗口恒非空栈，
            //    空栈首动豁免在此天然不触发），不再要求栈上有对手对象/攻宣锚点——旧口径
            //    攻击方在自家攻宣窗口 0 候选被自动双 Pass，攻击同帧结算、战斗诡雷永远迟到。
            //    代价（定案接受）：自己每次出牌/宣攻后，手里有**严格更快**且可付的卡就会
            //    弹「连锁吗」——让过=点「不连锁」（自己的 0 速/同速手牌不再进弹窗，含
            //    登场效果锚点场景——10-17 归属台账盲区由全严格大于口径根除）。
            //    其余过滤是「可发动性」而非连锁条件，全保留：CanPlay 钩子、同名仪典闸、
            //    代价门槛、永久物战场容量、任一模式可付+目标域有候选（付不起/指不了的不进弹窗，
            //    同"候选=0 跳过弹窗"口径）。目标不在此预选：cast 结算期经 TargetSelectionService
            //    反问（本地=Region 面板）。
            if (!engine.IsEmpty && !engine.IsResolving)
            {
                foreach (var c in core.ZoneManager.GetCards(p, Zone.Hand) ?? new List<Card>())
                {
                    if (c == null || !RuleHooks.CanPlay(core, p, c, Zone.Hand)) continue;
                    if (RuleAuraSystem.BlocksDuplicatePlay(c)) continue;
                    if (GetPayloadGateReject(core, p, c) != null) continue;
                    // 本轮窗口撤销记忆（2026-10-20 定案）：连锁内取消过声明期目标选择的卡
                    // 不再进候选——本次连锁不再询问该卡是否发动（防取消→重弹循环）
                    if (ResponseWindowService.IsDeclined(c)) continue;
                    int castSpeed = SpeedCalculator.GetCardCastSpeed(c);
                    // 2026-11-01 用户定案（蓝套 instant 前置）：**法术整卡同速可连锁**——
                    // 计费模型把法术速度定为 3−槽数（≤2 槽即 1 速以上），“不填满三个效果的法术
                    // 都应该能在对手回合发动”。旧口径走 CanActivate 的全严格大于：对手打一张
                    // 1 槽法术（速2）时，我的 1 槽法术（速2）连候选都进不了（2&gt;2 为假），
                    // 与设计意图冲突。此处只放开手牌整卡：要求 ①速≥1（0 速牌仍锁在自家主阶段）
                    // ②速≥记速器（同速可接、更慢不行）。效果发动仍走全严格大于（不变）。
                    if (castSpeed < 1 || castSpeed < engine.SpeedCounter.CurrentSpeed) continue;
                    // SBA 窗非回合方严格大于（2026-09-15 SbaStackSpeed 定案——与 PushCardCast 同口）：
                    // SBA 栈顶时非回合方候选按角色门槛剔列，防泵自动代选非法响应
                    if (engine.PeekStack()?.IsSBA == true && p != core.TurnEngine?.TurnPlayer
                        && castSpeed <= engine.SpeedCounter.CurrentSpeed) continue;
                    if (!c.IsSpellCard() && !core.ZoneManager.HasBattlefieldSpace(p)) continue;
                    var data = (c as CardWrapper)?.GetData();
                    int modes = data?.Supertype == Cardtype.Spell
                        ? Math.Max(1, data.Effects?.Count ?? 1) : 1; // 抉择模式数=法术效果数（ModeCount 同口径）
                    for (int m = 0; m < modes; m++)
                    {
                        if (!CanAffordCast(core, p, c, m)) continue;
                        if (!TargetDomainService.HasPlayableTargets(core, p, c, m)) continue;
                        options.Add(new ResponseOption
                        {
                            Kind = ResponseOption.ResponseKind.HandCard,
                            SourceCard = c,
                            ModeIndex = m,
                            Label = $"打出 {EffectText.Name(c)}（速度{castSpeed}）",
                        });
                        break; // 每卡一票（首个可行模式）；多模式细选留后续
                    }
                }
            }

            //（④ 手牌充能响应档已随 2026-11-05 充能系统动作化改版删除：充能=速度0系统动作，
            //  只能己方主阶段空栈首动宣入——0 速本就无法在对手回合响应窗口接链，旧速≥1 法术
            //  充能响应（2026-11-02 档）随「充能不再是卡上效果」一并退役。）

            return options;
        }

        /// <summary>窗口②主动效果来源枚举（2026-10-17 连锁门禁收敛）：战场+技能栏（英雄技能载体）。
        /// 手牌施放走整卡 cast 通道（③），墓地视手牌使用同走出牌口——均不是效果发动来源。</summary>
        private static IEnumerable<Card> EnumerateActivationSources(GameCore core, Player p)
        {
            foreach (var c in core.ZoneManager.GetCards(p, Zone.Battlefield) ?? new List<Card>())
                if (c != null) yield return c;
            foreach (var c in core.ZoneManager.GetCards(p, Zone.FieldZone) ?? new List<Card>())
                if (c != null) yield return c;
        }

        /// <summary>
        /// 响应窗口泵（交互式 DrainStack，2026-09-16）：循环——当前优先权方收集候选：
        /// 0 候选 → 自动 Pass（跳过弹窗）；有候选 → 人类弹窗（HumanResponder）/AI 决策/无头自动放弃；
        /// 选择 → 应用（守卫入栈/效果发动）→ 优先权翻转继续；双 Pass → 结算（await 完成整条链，
        /// 含 FinishResolution 新开窗口）→ 循环直到栈空且无待发（稳定）。
        /// </summary>
        public static async Cysharp.Threading.Tasks.UniTask SettleResponseWindowAsync(GameCore core, int maxRounds = 96)
        {
            // 并发泵闸（2026-11-03 帧泵自愈配套）：在途=别的链正在驱动本窗口（人类弹窗/AI 决策/
            // 结算中），本调用直接让路——双泵并发会双弹窗/双结算。顺序调用方（AI 回合驱动等）
            // 在让路时窗口由在途泵负责收口，语义不变；帧泵自愈口在途时本就不补发。
            if (core == null || core.ResponsePumpInFlight)
            {
                Crumb("CF-pump-skip " + (core == null ? "null-core" : "in-flight"));
                return;
            }
            core.ResponsePumpInFlight = true;
            Crumb($"CF-pump-enter stack={core?.StackEngine?.StackSize} res={core?.StackEngine?.IsResolving}");
            ResponseWindowService.ResetDeclinedWindow(); // 撤销记忆会话边界=本泵一次（一条连锁会话）
            try
            {
                for (int i = 0; i < maxRounds; i++)
                {
                    if (core == null || core.IsGameOver) return;
                    var engine = core.StackEngine;
                    if (engine.IsEmpty)
                    {
                        if (!engine.HasPendingEffects) return; // 稳定
                        engine.ProcessPendingEffects();
                        if (engine.IsEmpty) return;
                    }

                    var holder = engine.CurrentPriorityHolder;
                    if (holder == null) return;

                    ResponseOption choice = null;
                    var options = CollectAvailableResponses(core, holder);
                    Crumb($"CF-round i={i} holder={holder.Name} stack={engine.StackSize} opts={options.Count} waiting={engine.WaitingForPlayer} ctr={engine.SpeedCounter.CurrentSpeed}");
                    bool inquired = false;
                    if (options.Count > 0)
                    {
                        inquired = true;
                        // 连锁询问入日志（2026-11-03 连锁表现改日志定案）：有真实候选进入决策口才记
                        // ——0 候选自动 Pass 不询问不出行。应答三态：响应出牌=CardPlayEvent.IsResponseCast
                        // 「连锁了」、充能=ChainRespondedEvent（无既有行）、让过=ChainDeclinedEvent。
                        core.PublishEvent(new ChainInquiryEvent { Holder = holder });
                        if (holder.IsAI)
                            choice = ResponseWindowService.AiResponder?.Invoke(core, holder, options)
                                     ?? DefaultAiRespond(core, holder, options);
                        else if (ResponseWindowService.HumanResponder != null)
                        {
                            choice = await ResponseWindowService.HumanResponder(holder, options);
                            // 人类响应决策探针（连锁/让过都算——respond 引导步骤完成信号，2026-10-19）
                            RuleHooks.OnTutorialHumanResponded?.Invoke(holder);

                            // 声明期目标收集（2026-10-20 法术发动流程改案）：手牌响应卡选定后
                            // 先逐原子预选目标（可取消=撤销发动）。取消 → 该卡记入本轮窗口
                            // declined 集（本次连锁不再询问）+ 本轮视同让过；确认 → 目标随选项下发。
                            if (choice != null && choice.Kind == ResponseOption.ResponseKind.HandCard
                                && choice.SourceCard != null && choice.SourceCard.IsSpellCard())
                            {
                                var pre = await CollectSpellDeclarationTargetsAsync(choice.SourceCard, holder, choice.ModeIndex);
                                if (pre == null)
                                {
                                    ResponseWindowService.DeclineForThisWindow(choice.SourceCard);
                                    Crumb($"CF-declined card={(choice.SourceCard as IHasName)?.CardName}（声明期目标选择取消——本轮窗口不再询问）");
                                    choice = null; // 本轮让过（其余候选下轮照常收集）
                                }
                                else choice.Targets = pre;
                            }
                        }
                        // 挂死兜底不在引擎侧（2026-10-17 定案）：UniTask.Delay 型超时在链孤悬时
                        // 同样不触发（两度实测），且 TideServer shim 无 Delay/WhenAny——真正的
                        // 保命层=BattleScreen.WatchdogStalePick（MonoBehaviour 帧驱动墙钟）。
                        // 均未注册（无头/训练）→ choice=null 自动 Pass（守卫不响应=训练旧行为）
                    }

                    if (choice != null)
                    {
                        bool applied = ApplyResponse(core, holder, choice);
                        Crumb($"CF-choice holder={holder.Name} kind={choice.Kind} label={choice.Label} applied={applied}");
                        if (applied)
                            continue; // 发动成功 → 优先权已翻转，继续下一轮
                    }

                    if (inquired) // 询问后的让过（让过按钮/倒计时/AI 不作主/声明期取消）——未询问的自动 Pass 不记
                        core.PublishEvent(new ChainDeclinedEvent { Decliner = holder });
                    await engine.PassPriority(holder); // 0 候选/放弃/失败 → Pass（双 Pass 触发结算并 await 完成）
                }
            }
            finally
            {
                core.ResponsePumpInFlight = false;
                Crumb("CF-pump-exit");
            }
        }

        /// <summary>应用所选响应：守卫=PushGuardDeclaration；主动=ActivateEffect；自愿效果=PlayerActivateVoluntary；
        /// 手牌卡=PlayCardInResponse（目标=声明期预选（2026-10-20 收集器下发）或 null——结算期反问单源）。
        ///（充能档已随 2026-11-05 充能系统动作化改版删除——充能=速度0系统动作，不再经响应窗。）</summary>
        private static bool ApplyResponse(GameCore core, Player p, ResponseOption opt)
        {
            if (opt == null) return false;
            switch (opt.Kind)
            {
                case ResponseOption.ResponseKind.GuardAbility:
                    return core.StackEngine.PushGuardDeclaration(opt.SourceCard, opt.AttackInstance, p);
                case ResponseOption.ResponseKind.HandCard:
                    return opt.SourceCard != null
                        && PlayCardInResponse(core, p, opt.SourceCard, opt.Targets, opt.ModeIndex);
                case ResponseOption.ResponseKind.ActivatedAbility:
                    return opt.Definition != null && opt.SourceCard != null
                        && ActivateEffectInResponse(core, p, opt.Definition, opt.SourceCard);
                case ResponseOption.ResponseKind.VoluntaryEffect:
                    return core.StackEngine.PlayerActivateVoluntary(opt.Pending);
                default:
                    return false;
            }
        }

        /// <summary>内置 AI 响应启发（守卫决策，2026-11-01 二次改）：**最高攻击必拦**（盯防优先，打死了也拦），
        /// 其余攻击看「守卫够不够分」——还剩几次更大的宣言就留几张守卫，富余的才用来拦这一击；
        /// 且只在守卫扛得住（生命 &gt; 这一击）时才白拦，避免拿关键守卫换小伤害。
        /// 例：2 张 0/4 墙面对 1/3/5 三连击 → 1 放行、3 白拦、5 牺牲一张，全程只掉 1 血。
        /// 仍只保角色或带倒计时的单位（其余目标不拦）。</summary>
        private static ResponseOption DefaultAiRespond(GameCore core, Player defender, List<ResponseOption> options)
        {
            if (options == null || options.Count == 0) return null;
            var guards = options.Where(o => o.Kind == ResponseOption.ResponseKind.GuardAbility && o.SourceCard != null).ToList();
            if (guards.Count == 0) return null;

            var first = guards[0];
            var targets = first.AttackInstance?.Targets as List<Entity>;
            if (targets == null || targets.Count == 0) return null;
            var target = targets[0];
            bool face = target is Player;
            var countdownTarget = target as Card;
            if (!face && (countdownTarget == null || !HasCountdown(countdownTarget)))
                return null; // 只保角色，或保带倒计时的单位

            var source = first.AttackInstance?.Source;
            int incoming = core.LayerEngine != null && source != null
                ? core.LayerEngine.CalculatePower(source) : 0;
            if (incoming <= 0) return null;

            // 本击之外还剩几次更高攻击的宣言——更大的威胁要留守卫
            var attacker = core.TurnEngine?.TurnPlayer;
            var threats = RemainingAttackThreats(core, attacker, target);
            int bigger = threats.Count(t => t.Power > incoming);

            if (bigger <= 0)
                return PickGuard(guards, incoming, preferSurvivor: true); // 最高威胁：盯防，打死了也拦

            var survivors = guards.Where(g => g.SourceCard.GetLife() > incoming).ToList();
            // 富余守卫（> 更大威胁的数量）才白拦这一击；否则留给后面的高威胁
            return survivors.Count > bigger ? PickGuard(survivors, incoming, preferSurvivor: true) : null;
        }

        /// <summary>挑拦截者：优先「扛得住里生命最低的」（省大墙），没有能活的才牺牲生命最低的。</summary>
        private static ResponseOption PickGuard(List<ResponseOption> guards, int incoming, bool preferSurvivor)
        {
            ResponseOption best = null;
            int bestLife = int.MaxValue;
            foreach (var g in guards)
            {
                int life = g.SourceCard.GetLife();
                if (life <= incoming) continue;
                if (life < bestLife) { best = g; bestLife = life; }
            }
            if (best != null || !preferSurvivor) return best;

            int lowest = int.MaxValue;
            foreach (var g in guards)
            {
                int life = g.SourceCard.GetLife();
                if (life <= 0) continue;
                if (life < lowest) { best = g; lowest = life; }
            }
            return best;
        }

        /// <summary>威胁比=攻击力/生命（7/3 远高于 1/1；同为 1 时用攻击力决胜）。</summary>
        private static float ThreatRatio(GameCore core, Entity e)
        {
            if (e == null) return 0f;
            int p = core?.LayerEngine != null ? core.LayerEngine.CalculatePower(e) : 0;
            int l = e is Card c ? c.GetLife() : 1;
            return p / (float)Math.Max(1, l);
        }

        /// <summary>攻击者还能对同一目标宣言的攻击（攻击力, 威胁比）列表（含正在结算的当前一击；
        /// 当前攻击源可能已不在 CanDeclareAttack 口径内，调用方另行补）。</summary>
        private static List<(int Power, float Ratio)> RemainingAttackThreats(GameCore core, Player attacker, Entity target)
        {
            var result = new List<(int Power, float Ratio)>();
            if (core?.ZoneManager == null || attacker == null || target == null) return result;
            foreach (var u in core.ZoneManager.GetCards(attacker, Zone.Battlefield) ?? new List<Card>())
            {
                if (u == null || !u.IsAlive) continue;
                if (!core.CombatSystem.CanDeclareAttack(u, attacker)) continue;
                if (!core.CombatSystem.CanAttackTarget(u, target, attacker)) continue;
                int p = core.LayerEngine.CalculatePower(u);
                result.Add((p, p / (float)Math.Max(1, u.GetLife())));
            }
            return result;
        }

        /// <summary>卡面是否带倒计时引擎（扫描 Steps/AtomicEffects 的 branch；与 converter 同源）。</summary>
        private static bool HasCountdown(Card card)
        {
            var data = (card as CardWrapper)?.GetData();
            if (data?.Effects == null) return false;
            foreach (var fx in data.Effects)
            {
                if (fx == null) continue;
                if (fx.Steps != null)
                    foreach (var step in fx.Steps)
                    {
                        var b = step?.atomic?.branch;
                        if (b != null && b.settle == (int)BranchSettleKind.Engine
                            && b.engine == (int)BranchEngineKind.Countdown) return true;
                    }
                if (fx.AtomicEffects != null)
                    foreach (var a in fx.AtomicEffects)
                    {
                        var b = a?.branch;
                        if (b != null && b.settle == (int)BranchSettleKind.Engine
                            && b.engine == (int)BranchEngineKind.Countdown) return true;
                    }
            }
            return false;
        }

        /// <summary>
        /// 声明期目标收集（2026-10-20 法术发动流程改案）：法术发动（PlayCard/PlayCardInResponse）
        /// **前**逐原子预选目标——调用方（BattleScreen 主路径 / 响应窗泵手牌卡分支）拿到结果再声明。
        /// 返回：null=玩家取消（终止发动——主路径撤销、响应路径记 declined）；空表=无交互原子
        /// （直通声明，结算期照常自解析）；非空=预选目标（按原子序，结算期经预选队列有序消费）。
        /// 口径单源=EffectHandlerRegistry.CollectDeclarationTargetsAsync（与结算路径同谓词）；
        /// 教学闸/费用门不在此口——调用方先过、PlayCard 内终审（本口零引擎副作用）。
        /// caster 显式传参：响应路径施法者可能非回合玩家。
        /// </summary>
        public static async Cysharp.Threading.Tasks.UniTask<List<Entity>> CollectSpellDeclarationTargetsAsync(
            Card card, Player caster, int modeIndex)
        {
            var core = GameCore.Instance;
            if (card == null || caster == null || core?.ZoneManager == null) return new List<Entity>();

            var defs = GetCastableEffectDefinitions(card); // P12：模式序号索引非充能列表
            if (defs == null || defs.Count == 0) return new List<Entity>();
            var chosen = defs[Math.Clamp(modeIndex, 0, defs.Count - 1)];
            if (chosen == null) return new List<Entity>();

            var ctx = new EffectExecutionContext
            {
                Source = card, // 声明期卡在手（旧 PromptLocalTargetThenPlay 同口径——Self 域候选如此解析）
                Controller = caster,
                ZoneManager = core.ZoneManager,
                ElementPool = core.ElementPool,
                CastCard = card, // PromptSource 展示与结算口径一致
                ModeIndex = modeIndex,
            };
            return await Attribute.EffectHandlerRegistry.CollectDeclarationTargetsAsync(chosen, ctx);
        }

        /// <summary>
        /// 结算法术的施放效果（经本核心的 EffectExecutor），完成后离开发动区入墓。
        /// 2026-10-15 深夜三类卡改版：法术=1~3 个主动效果——施放时弹窗抉择其一（modeIndex=效果序号，
        /// 声明期随 CardPlayEvent 公开），此处只执行所选那条；锚价已并入 cast 账单
        /// （GetCardCost→SpellCastAnchor，付费步统一扣）——skipElementCost:true 防双计。
        /// 通过 EffectInstance 走与栈结算一致的执行路径，保证目标解析/事件一致；特殊代价仍由执行器结算。
        /// </summary>
        private static async Cysharp.Threading.Tasks.UniTask ResolveSpellEffectsAsync(
            GameCore core, Player player, Card card, List<Entity> targets, int modeIndex = 0,
            EffectInstance cast = null)
        {
            var defs = GetCastableEffectDefinitions(card); // P12：模式序号索引非充能列表
            EffectDefinition chosen = null;
            if (defs != null && defs.Count > 0)
                chosen = defs[Math.Clamp(modeIndex, 0, defs.Count - 1)];

            // 外给目标硬校验收口（2026-09-13 定案修复）：声明期 UI/AI 直接给定的目标不经候选解析——
            // 补两道同口径检查：源紊乱不可指角色；对手有嘲讽卡时对方侧仅嘲讽卡可指
            //（edict 原子=牺牲/摒弃豁免帷幕——选择权在目标方）。非法目标剔除（效果对其空转），不整卡拒绝。
            bool edictExempt = chosen != null && chosen.Effects != null
                && chosen.Effects.Any(a => a != null && TargetResolver.IsEdict(a.Type));
            targets = TargetResolver.FilterPreselectedTargets(targets, player, player, core.ZoneManager, edictExempt);
            // 取证（2026-11-05 SBA 救回排查）：法术结算前的目标过滤结果
            GameActions.Crumb($"[CAST] {card.ID} defs={defs?.Count ?? 0} atoms={chosen?.Effects?.Count ?? 0} " +
                              $"targetsAfterFilter={targets?.Count ?? 0}");
            if (chosen != null)
            {
                var executor = core.StackEngine.GetExecutor();
                // cast 直传优先（2026-11-05 断链修复）：结算时本卡 cast 条目**已弹栈**，
                // GetStackContents 反查恒 null → RespondedEntry 丢失 → 反制原子空转
                //（整卡施放的反制从未打中标）。栈上反查仅留作非 cast 调用口的兜底。
                var castEntry = cast ?? core.StackEngine.GetStackContents()
                    .LastOrDefault(e => e != null && e.IsCardCast && ReferenceEquals(e.Source, card));
                await executor.ExecuteAsync(new EffectInstance
                {
                    Definition = chosen,
                    // 来源归因定案（2026-09-09 规则②）：所有魔法卡的效果来源=角色（Player）——
                    // 伤害/死亡/指示物/三轨判轨的归因都指向施法者；cast 对象的 Source 仍是卡
                    //（发动区付费/无效裁决/离区依赖它，见 ResolveCardCastAsync）。
                    Source = player,
                    Controller = player,
                    Targets = targets != null ? new List<Entity>(targets) : new List<Entity>(),
                    ModeIndex = 0, // 法术效果禁嵌抉择（装载校验拦）——modeIndex 已被效果抉择占用，执行层恒 0
                    CastCard = card, // 施放宿主卡（状态门「本回合第一张抽到的卡」需要卡身份）
                    RespondedEntry = castEntry?.RespondedEntry, // E1-1：cast 的响应绑定透传给法术效果上下文（反制读它）
                }, skipElementCost: true);
            }

            // 结算完成 → 入墓（S3b：从来源区[手牌/牌库顶]入墓；Leave 事件随区退役）。
            // 同区幂等（2026-11-05 收口）：结算有效性只取决于效果目标，源卡所在区域不参与结算
            //（S3b 后卡留来源区，可能在任何时机被移走——效果结算期间被送墓/回手/弹牌库均合法），
            // 结算尾卡已在墓地则跳过移动，防 ZoneOperationException。
            if (card.GetZone() != Zone.Graveyard)
                core.ZoneManager.MoveCard(card, player, card.GetZone(), Zone.Graveyard);
        }

        /// <summary>
        /// 收集一张卡的**卡级**代价行（2026-11-02 重放改版自 CollectCardSpecialCosts 抽取）：
        /// PayloadCosts 逐行（行序=声明序；行内原子序列整行转换）。重放轮重执行
        ///（EffectExecutor.PayReplayRoundCostsAsync——「包括效果和代价」）与 cast 付费步共读此口。
        /// </summary>
        internal static List<CostInstance> CollectCardPayloadCostRows(Card card)
        {
            var result = new List<CostInstance>();
            if (!(card is CardWrapper wrapper)) return result;

            var rows = wrapper.GetData()?.PayloadCosts;
            if (rows == null) return result;
            foreach (var row in rows)
            {
                var atoms = row?.AllPayloads();
                if (atoms == null || atoms.Count == 0) continue;
                var payloadAtoms = new List<AtomicEffectInstance>();
                bool allOk = true;
                foreach (var pe in atoms)
                {
                    var inst = CardEffectConverter.ConvertPayloadForDisplay(pe);
                    if (inst == null) { allOk = false; break; }
                    payloadAtoms.Add(inst);
                }
                if (!allOk)
                {
                    TideLog.Warn("[CardActions] 卡级代价行 Payload 转换失败（表行缺失/契约违约）——该行按无代价处理");
                    continue;
                }
                result.Add(new CostInstance
                {
                    Type = CostType.Payload,
                    Value = row.Value,
                    ManaType = (ManaType)row.ManaType,
                    Payloads = payloadAtoms,
                });
            }
            return result;
        }

        /// <summary>
        /// 弃牌代价「另有一张」闸（2026-11-03 用户定案）：施放/发动本卡时**本卡自身不算可弃手牌**——
        /// 弃牌类代价要求「手牌数（剔除本卡）≥ 代价张数」，不够 → 整卡不可用（声明期闸，不是结算期失败）。
        /// 旧口径 CostPayability 只数手牌总数（本卡仍在手），会出现「手里只剩本卡 → 代价空转白嫖」。
        /// 覆盖面：卡级行 + 非主动效果行（CollectCardSpecialCosts）+ **法术所选模式（主动）效果行**
        /// （specialCosts 不收主动行，但法术施放就是使用该主动效果——其代价照付）。
        /// 返回 null=通过；非 null=拒绝原因。
        /// </summary>
        private static string CheckDiscardCostSelfExclusion(GameCore core, Player player, Card card, int modeIndex)
        {
            if (core == null || player == null || card == null) return null;
            int need = 0;
            foreach (var cost in CollectCardSpecialCosts(card))
                need += DiscardAtomsOf(cost);
            // 法术所选模式（主动）效果行——specialCosts 只收非主动效果
            var data = card is CardWrapper cw ? cw.GetData() : null;
            if (data != null && data.Supertype == Cardtype.Spell)
            {
                var defs = GetCastableEffectDefinitions(card);
                var modeDef = defs != null && defs.Count > 0
                    ? defs[Math.Clamp(modeIndex, 0, defs.Count - 1)] : null;
                if (modeDef?.Costs != null)
                    foreach (var c in modeDef.Costs) need += DiscardAtomsOf(c);
            }
            if (need <= 0) return null;

            var hand = core.ZoneManager.GetCards(player, Zone.Hand) ?? new List<Card>();
            int available = hand.Count - (hand.Contains(card) ? 1 : 0); // 本卡离手，不计入可弃
            if (available >= need) return null;
            return $"手牌不足：弃牌代价需另 {need} 张（本卡不计），当前可弃 {Math.Max(0, available)} 张——卡的发动无效";
        }

        /// <summary>单条代价里的弃牌张数（非弃牌子=0）。</summary>
        private static int DiscardAtomsOf(CostInstance cost)
        {
            if (cost == null || cost.Type != CostType.Payload || cost.Payloads == null) return 0;
            int n = 0;
            foreach (var atom in cost.Payloads)
                if (atom != null && atom.Type == AtomicEffectType.DiscardCard)
                    n += Math.Max(0, atom.Value);
            return n;
        }

        /// <summary>
        /// 收集一张卡的**施放期**特殊代价（2026-10-31 代价指向化定案）：
        /// = 卡级代价行（PayloadCosts 逐行，TargetAttr 任指——整卡 legacy / 攻血 / 耐久 / 速度）
        /// + **非主动**效果的效果级代价行（自动效果行——随卡施放在 cast 付费步统一执行）。
        /// 主动效果行不在此列：发动时经 EffectActivationGate 现付（净费+执行）；法术施放=使用
        /// 所选主动效果——其代价行在 GetCardCost 锚价净费落账、执行器结算时执行。
        /// </summary>
        private static List<CostInstance> CollectCardSpecialCosts(Card card)
        {
            // 卡级代价行（2026-11-02 重放改版抽取：CollectCardPayloadCostRows 单源——cast 付费步
            // 与 EffectExecutor 重放轮「包括代价」重执行共读）
            var result = CollectCardPayloadCostRows(card);

            // 效果级行：非主动（自动）效果——cast 付费步执行；主动跳过（发动时现付）
            var defs = GetCardEffectDefinitions(card);
            if (defs == null) return result;
            foreach (var def in defs)
            {
                if (def == null || def.IsActivatedEffect) continue; // 主动：发动时现付（执行器路径）
                if (def.Costs == null) continue;
                foreach (var c in def.Costs)
                {
                    if (c != null && c.Type != CostType.ElementConsume)
                        result.Add(c);
                }
            }
            return result;
        }

        /// <summary>
        /// 出牌两阶段·第一阶段（2026-10-04 定案；2026-10-28 P1 改版）：有代价的卡声明期先算代价——
        /// 预选代价目标。代价也是完整效果：按其自身域以**支付者视角**解析候选（与结算期
        /// ExecutePayloadAsync 同口径），候选存 <see cref="Card._pendingCostTargets"/> 供 cast 付费步沿用
        /// （响应窗口内不重选）。可支付闸（CostPayability：手牌/生命/牌库/候选）不可过 → 整卡不可用；
        /// 代价无目标（无法执行）→ 返回 false，卡的发动无效。无代价卡直接放行（只走费用门禁）。
        /// </summary>
        private static bool TryPrepareCardCostTargets(GameCore core, Player player, Card card, int modeIndex,
            out string rejectReason)
        {
            rejectReason = EvaluatePayloadGate(core, player, card, modeIndex, out var payloadCosts, out var candidatesByAtom);
            if (rejectReason != null)
            {
                card._pendingCostTargets = null;
                return false;
            }
            if (payloadCosts == null || payloadCosts.Count == 0)
            {
                card._pendingCostTargets = null; // 无代价：两阶段退化为一阶段（费用门禁）
                return true;
            }
            // 逐原子预选：candidatesByAtom 非空表=至少一个有域原子（无域原子位=null）；
            // 全无域原子=空外层表（无目标语义照常执行的口径不变）
            card._pendingCostTargets = candidatesByAtom;
            return true;
        }

        /// <summary>代价门槛纯检（AI LegalActionEnumerator 共用，不动卡状态）：null=通过，非 null=拒绝原因。
        /// modeIndex 缺省 0：多模式法术按模式 0 近似（现网效果级代价行均为单模式 legacy——分账差异面为零）。</summary>
        public static string GetPayloadGateReject(GameCore core, Player player, Card card, int modeIndex = 0)
            => EvaluatePayloadGate(core, player, card, modeIndex, out _, out _);

        /// <summary>
        /// 代价门槛评估（2026-10-28 P1 定案，声明期与 AI 枚举同口；组装效果代价逐原子化；
        /// 2026-10-31 代价指向化：多行代价+按使用口分账）：
        /// ① 可支付门槛——CostPayability.AllPayable/RejectReason（弃牌→手牌数、流失生命→生命、
        ///    磨牌→牌库张数、有域原子→候选非空）；代价不可支付 → 整卡不可用（声明期闸）。
        ///    旧「全价过地牌槽上限」门槛已删（免费卡合法——净费 0 与本闸无关）。
        /// ② 目标门槛——全部施放期代价行逐原子以支付者视角解析候选（剔除本卡），任一无候选 → 发动无效。
        /// ③ 地牌门槛（2026-10-31 代价地牌统一定案+指向化分账）——**使用卡只算主动效果以外的代价行**
        ///    （卡级行+非主动效果行，CardCostService.PayloadGrantByColor 单源）；法术施放=使用所选
        ///    主动效果，「使用主动效果时同理」——所选模式（modeIndex）效果的效果级代价行一并计入
        ///    （与 GetCardCost 锚价净费同口径）。每色代价合计 > 施放者该色地牌数 → 发动无效（等于放行）。
        /// 全无域原子则只过①③（可发动，结算按无目标执行）。
        /// </summary>
        private static string EvaluatePayloadGate(GameCore core, Player player, Card card, int modeIndex,
            out List<CostInstance> payloadCosts, out List<List<Entity>> candidates)
        {
            payloadCosts = null;
            candidates = null;

            var specialCosts = CollectCardSpecialCosts(card);
            payloadCosts = new List<CostInstance>();
            foreach (var c in specialCosts)
                if (c != null && c.Type == CostType.Payload && c.Payloads != null && c.Payloads.Count > 0)
                    payloadCosts.Add(c);
            // 2026-11-03 用户定案：弃牌代价「另有一张」闸——本卡自身不算可弃手牌（声明期闸，整卡不可用）。
            // 必须早于下方「无代价」早退：法术的代价行挂在**主动效果**上（不 Collected 进 specialCosts）。
            var discardReject = CheckDiscardCostSelfExclusion(core, player, card, modeIndex);
            if (discardReject != null) { payloadCosts = null; candidates = null; return discardReject; }

            if (payloadCosts.Count == 0) { payloadCosts = null; return null; } // 无代价：两阶段退化为一阶段（费用门禁）

            // ① 可支付门槛（声明期闸——与 CanAffordCast 同口径）：不可支付返回拒绝原因（UI/AI 直读）
            var costCtx = new CostContext
            {
                Payer = player,
                ZoneManager = core.ZoneManager,
                ElementPool = core.ElementPool,
                Source = player,
            };
            var payReject = CostPayability.RejectReason(specialCosts, costCtx);
            if (payReject != null) return payReject;

            // ③ 地牌门槛（2026-10-31 代价地牌统一定案+指向化分账）：施放口只算非主动行；
            //    法术施放=使用所选主动效果——模式效果行同尺并入（GetCardCost 锚价净费同口径）。
            var gateData = card is CardWrapper gw ? gw.GetData() : null;
            if (gateData != null)
            {
                var grantByColor = CardCostService.PayloadGrantByColor(gateData);
                if (gateData.Supertype == Cardtype.Spell)
                {
                    var defs = GetCastableEffectDefinitions(card);
                    var modeDef = defs != null && defs.Count > 0
                        ? defs[Math.Clamp(modeIndex, 0, defs.Count - 1)] : null;
                    foreach (var kv in CardCostService.PayloadGrantByColor(modeDef?.Costs))
                    {
                        grantByColor.TryGetValue(kv.Key, out var v);
                        grantByColor[kv.Key] = v + kv.Value;
                    }
                }
                foreach (var kv in grantByColor)
                {
                    int landsOfColor = CountLands(core, player, kv.Key);
                    if (kv.Value > landsOfColor)
                        return $"{ManaTypeNames.ZhNameOf(kv.Key)}色代价 {kv.Value} 大于当前{ManaTypeNames.ZhNameOf(kv.Key)}地 {landsOfColor}——卡的发动无效";
                }
            }

            // ② 目标门槛：全部代价行逐原子按支付者视角解析候选（与结算期 ExecutePayloadAtomAsync 同口径）
            bool anyDomain = false;
            var byAtom = new List<List<Entity>>();
            foreach (var payloadCost in payloadCosts)
            {
                foreach (var atom in payloadCost.Payloads)
                {
                    if (atom == null || atom.TargetKinds == null || atom.TargetKinds.Count == 0)
                    {
                        byAtom.Add(null); // 无域原子（无目标语义）：可发动
                        continue;
                    }
                    anyDomain = true;
                    var resolveCtx = new EffectExecutionContext
                    {
                        Controller = player,
                        Source = player,
                        ZoneManager = core.ZoneManager,
                        ElementPool = core.ElementPool,
                    };
                    var resolved = Attribute.EffectHandlerRegistry.ResolveCandidates(atom.TargetKinds, atom.Filter, resolveCtx);
                    // 出牌两阶段（2026-10-04 定案）原文：代价也是一个完整效果，先选定其目标；代价无法执行（无目标）→ 发动无效。
                    if (resolved != null) resolved.RemoveAll(e => ReferenceEquals(e, card)); // 声明期本卡尚在手：不作代价目标
                    var list = resolved ?? new List<Entity>();
                    // 单位域代价（2026-10-31 用户定案：禁全域）：不预选目标列表——付费步经
                    // TargetSelectionService 现选**单个**单位（本地弹选/联机反问/AI 代选）；
                    // 可付性=能指定一个目标即可（无数值门槛，量级限制归数据侧），
                    // 域空（连一个可指定单位都没有）→ 发动无效。
                    if (CostPayability.IsUnitDomainCost(atom))
                    {
                        if (list.Count == 0)
                            return "代价无目标（无法执行）——卡的发动无效";
                        byAtom.Add(null); // 单位域：预选位恒空（结算期现选单目标）
                        continue;
                    }
                    if (list.Count == 0)
                        return "代价无目标（无法执行）——卡的发动无效";
                    byAtom.Add(list);
                }
            }
            candidates = anyDomain ? byAtom : new List<List<Entity>>();
            return null;
        }

        /// <summary>
        /// 主阶段：宣言攻击（2026-09-16 战斗接入栈机器定案：攻击=速度0栈对象，逐攻击开窗）。
        /// 资格（CanDeclareAttack+CanAttackTarget）→ StackEngine.PushAttackDeclaration
        /// （宣言期零支付，防守方获响应窗口——发动弹窗候选：守卫速度1/响应卡）→
        /// 双 Pass 结算（重检+横置支付→战斗三段，死亡处理走栈机器 SBA 轮）。
        /// 主阶段+回合方在此把守；速度门（0≥计数器）在 PushAttackDeclaration。
        /// </summary>
        public static bool DeclareAttack(GameCore core, Player player, Entity attacker, Entity target)
            => DeclareAttack(core, player, attacker, target, out _);

        /// <summary>带原因版（2026-11-03 失败提示细化）：拒绝文案直读闸门（UI toast / 网络 Error 帧透传），
        /// 攻击者侧/目标侧资格经 CombatSystem.TryDeclareAttack/TryAttackTarget 单源透传。</summary>
        public static bool DeclareAttack(GameCore core, Player player, Entity attacker, Entity target, out string reason)
        {
            reason = null;
            if (core == null || player == null) { reason = "内部错误：空参数"; return false; }
            if (!core.TurnEngine.CanCombatAction())
            {
                reason = core.TurnEngine.CurrentPhase?.Phase != PhaseType.Main
                    ? $"攻击只能在你的主阶段（当前：{core.TurnEngine.CurrentPhase?.Phase}）"
                    : "当前不可宣言攻击（阶段非激活态/结算中）";
                return false;
            }
            if (core.TurnEngine.TurnPlayer != player) { reason = "现在不是你的回合"; return false; }

            // 教学引导闸（2026-10-06）：先于常规资格——引导步骤语义优先（错误目标/锁定生物在此拒绝）
            var guideReject = RuleHooks.TutorialAttackGate?.Invoke(player, attacker, target);
            if (guideReject != null)
            {
                reason = $"教学引导：{guideReject}";
                TideLog.Warn($"[GameActions] 攻击被教学引导拒绝：{guideReject}");
                return false;
            }

            var combat = core.CombatSystem;
            if (!combat.TryDeclareAttack(attacker, player, out reason)) return false;
            if (!combat.TryAttackTarget(attacker, target, player, out reason)) return false;

            if (!core.StackEngine.PushAttackDeclaration(attacker, target, player))
            { reason = "攻击入栈被拒（速度门/栈结算中）"; return false; }
            RuleHooks.OnTutorialAttackDeclared?.Invoke(player, attacker, target);
            return true;
        }

        // ======================================== 回合控制 ========================================

        /// <summary>
        /// 结束回合（2026-11-01 阶段优先权窗改版；同日修订：双窗=①「结束阶段开始」（本口）+
        /// ②「主阶段开始」（GameCore.OnMainPhaseStartedWindow 订阅 PhaseStartEvent(Main)，原
        /// 「回合结束前」时点废弃））：
        /// 主阶段可结束态（相位 Active+栈空+非结算中）时先压 0 速阶段窗伪对象开「结束阶段开始」窗，
        /// 窗口排干（响应链/级联触发/SBA 轮一并收口）后才真实推进 Main→End（结束阶段系统行为照旧），
        /// 之后折返照旧走既有口（帧泵 GameLoopController/驱动器 CheckPhaseTransition）——新回合
        /// 主阶段开始时窗②自然开启。非可结束态（同帧双击/连锁未清）走引擎静默拒绝路径（行为与
        /// 旧版一致）。无候选时响应窗泵自动双 Pass——AI/无头局全链同步收口不卡死；无响应者注册
        /// （编辑器验证）时窗口自动让路，EndTurn 返回时已推进到位（行为同旧版）。
        /// </summary>
        public static bool EndTurn(GameCore core, Player player)
            => EndTurn(core, player, out _);

        /// <summary>带原因版（2026-11-03 失败提示细化）：拒绝文案直读闸门——UI 直显，替代旧
        ///「Region 是否开窗」启发式二分猜测。</summary>
        public static bool EndTurn(GameCore core, Player player, out string reason)
        {
            reason = null;
            if (core == null || player == null) { reason = "内部错误：空参数"; return false; }
            if (core.TurnEngine.TurnPlayer != player) { reason = "现在不是你的回合"; return false; }

            // 教学引导闸（2026-10-06）：引导步骤未完成时钉住回合（必须先做引导动作）
            var guideReject = RuleHooks.TutorialEndTurnGate?.Invoke(player);
            if (guideReject != null)
            {
                reason = $"教学引导：{guideReject}";
                TideLog.Warn($"[GameActions] 结束回合被教学引导拒绝：{guideReject}");
                return false;
            }

            // 阶段窗①（2026-11-01）：可结束态先开「结束阶段开始」窗，续链内完成推进。
            // Push 失败（栈非空等异常态）回落旧直进路径——EndTurn 内守卫会照常裁决。
            if (core.TurnEngine.CurrentPhase?.Phase == PhaseType.Main
                && core.TurnEngine.CurrentPhase?.State == PhaseState.Active
                && core.StackEngine.IsEmpty
                && !core.StackEngine.IsResolving
                && core.StackEngine.PushPhaseWindow(core.TurnEngine.TurnPlayer, "结束阶段开始"))
            {
                EndTurnWithPhaseWindowAsync(core, player).Forget();
                return true;
            }

            // 真发判定（2026-10-20 教学双推进修复）：TurnEngine 静默拒绝（阶段非 Main/栈未空——
            // 同帧双击、回合翻转前重入的典型态）时**不发教学探针**——否则 OnTutorialTurnEnded
            // 连发两次，连续 end 步一击双进（段七实测：换场/考验武装提前、必杀直伤提前落地）。
            if (!core.TurnEngine.EndTurn())
            {
                reason = core.StackEngine.IsEmpty
                    ? $"当前阶段不可结束回合（{core.TurnEngine.CurrentPhase?.Phase}/{core.TurnEngine.CurrentPhase?.State}）"
                    : "连锁尚未结算——暂时无法结束回合";
                TideLog.Warn("[GameActions] 结束回合被引擎拒绝（阶段已推进/连锁未结算）——不发教学探针");
                return false;
            }
            RuleHooks.OnTutorialTurnEnded?.Invoke(player);
            return true;
        }

        /// <summary>
        /// 阶段窗①「结束阶段开始」续链（2026-11-01 定案；同日修订：窗②迁「主阶段开始」，
        /// 本续链只管窗①排干+真实推进）：
        /// ① 排干窗①（含窗口内响应连锁）→ 真实推进 Main→End（真发才发教学探针，口径同旧版）。
        /// 任何一步异常（终局/引擎拒绝）即止步，不重试不弹错——状态与旧版拒绝路径一致。
        /// </summary>
        private static async Cysharp.Threading.Tasks.UniTask EndTurnWithPhaseWindowAsync(GameCore core, Player player)
        {
            // 窗①排干：响应窗泵循环到栈空且无待发（含窗口内出牌的整条连锁与级联）
            await SettleResponseWindowAsync(core);
            if (core == null || core.IsGameOver || core.StackEngine == null) return;

            // 真实推进（守卫与旧版一致：Main+Active+栈空；窗口期间外部不应推进相位，若被拒=异常态止步）
            if (!core.TurnEngine.EndTurn())
            {
                Crumb("CF-phase-window-advance-reject");
                return;
            }
            RuleHooks.OnTutorialTurnEnded?.Invoke(player);
        }

        // ======================================== 栈操作 ========================================

        /// <summary>
        /// 速度发动：玩家主动发动一个效果。
        /// 主动能力的发动代价（2026-09-08 定案；2026-10-15 深夜三类卡改版）：只有主动（Activate_*）=
        /// 发动代价 + 现付元素锚价 + 选定目标（元素费经 ElementCostPrepaid=false 由执行器结算路径扣除；
        /// CanActivate 预检可付性）；只能在自己的主要阶段以速度1使用（记速器须低于1，即栈空）。
        /// 触发式效果不走本入口的代价与付费。发动代价按宿主分域（2026-10-15 深夜改版）：
        /// 结界（战场/技能栏）= **拒绝**——结界禁主动效果，只挂自动/光环（运行时临摹/变形把主动行
        /// 塞给结界也在此拦）；生物 = 横置（恒横置，警戒不抵扣）；技能区法术（英雄技能载体）= 横置
        ///（每回合一次闸门——回合开始 Untap 重置，接替旧耐久池）。
        /// </summary>
        public static bool ActivateEffect(GameCore core, Player player, EffectDefinition effect, Card source, int paidBoost = 0)
            => ActivateEffect(core, player, effect, source, paidBoost, out _);

        /// <summary>带原因版（2026-11-03 失败提示细化）：既有 Crumb 取证分支同步升级为用户可读
        /// reason（Crumb 保留作验证器取证），闸序零变化。</summary>
        public static bool ActivateEffect(GameCore core, Player player, EffectDefinition effect, Card source, int paidBoost, out string reason)
        {
            reason = null;
            if (core == null || player == null || effect == null) { reason = "内部错误：空参数"; return false; }
            if (core.TurnEngine.TurnPlayer != player)
            {
                reason = "只能在你的回合发动主动效果";
                Crumb($"CF-activate-reject def={effect.Id} reason=非回合方 by={player?.Name}");
                return false;
            }
            if (core.TurnEngine.CurrentPhase?.Phase != PhaseType.Main)
            {
                reason = $"效果发动只能在你的主阶段（当前：{core.TurnEngine.CurrentPhase?.Phase}）";
                Crumb($"CF-activate-reject def={effect.Id} reason=非主阶段({core.TurnEngine.CurrentPhase?.Phase})");
                return false;
            }

            // 禁发令硬闸（2026-10-29）：网络 intent 直调本口不经引擎 CanActivate——统一在此拦
            if (source != null && RuleAuraSystem.ScopeHits(RuleAuraComponents.NoActivateAura, source.GetController()))
            {
                reason = "禁发令生效中——不能发动主动效果";
                Crumb($"CF-activate-reject def={effect.Id} reason=禁发令 by={player?.Name}");
                return false;
            }

            // 教学引导闸（2026-10-23 元素合成步）：资源步期间拦截玩家侧主动效果发动——
            // 防把后续步骤要用的效果提前烧掉（横置后步骤软锁）；AI 侧与响应路径不经此口
            var guideReject = RuleHooks.TutorialActivateGate?.Invoke(player, source, effect);
            if (guideReject != null)
            {
                reason = $"教学引导：{guideReject}";
                Crumb($"CF-activate-reject def={effect.Id} reason=引导闸：{guideReject}");
                return false;
            }

            // 发动代价预检（仅主动；战场/技能栏源卡，手牌/墓地施放不适用）：
            // 结界=拒发动（禁主动定案）；其余（生物/技能区法术）=未横置（横置=一回合一次闸门）。
            if (effect.IsActivatedEffect && source != null
                && (core.ZoneManager.IsCardInZone(source, source.GetController(), Zone.Battlefield)
                    || core.ZoneManager.IsCardInZone(source, source.GetController(), Zone.FieldZone)))
            {
                if (Attribute.CounterRules.UsesDurabilityEconomy(source))
                {
                    reason = "结界不能主动发动效果";
                    Crumb($"CF-activate-reject def={effect.Id} reason=结界禁主动 src={source.ID}");
                    return false; // 结界禁主动效果（2026-10-15 深夜改版）——耐久−1 通道整体退役
                }
                if (source.IsTapped())
                {
                    reason = "该卡已横置（本回合已发动过）";
                    Crumb($"CF-activate-reject def={effect.Id} reason=源已横置 src={source.ID}");
                    return false;
                }
            }

            var pending = PendingEffect.Create(
                effect,
                source,
                player,
                core.TurnEngine.TurnPlayer,
                core.TurnEngine.CurrentPhase?.Phase ?? PhaseType.Standby,
                paidBoost: paidBoost);

            var activated = core.StackEngine.PlayerActivateVoluntary(pending);
            if (!activated)
            {
                reason = $"发动入栈被拒（速度门/栈结算中，记速器 {core.StackEngine.SpeedCounter.CurrentSpeed}）";
                Crumb($"CF-activate-reject def={effect.Id} reason=速度门或入栈失败 ctr={core.StackEngine.SpeedCounter.CurrentSpeed} stack={core.StackEngine.StackSize}");
            }

            // 发动成功 → 消耗发动代价（仅主动；固定代价，不发不扣）：横置（恒横置，警戒不抵扣）
            //——生物每回合一次；技能区法术=英雄技能每回合一次（回合开始 Untap 重置）。
            if (activated && effect.IsActivatedEffect && source != null)
            {
                if (Attribute.KeywordRules.ShouldTap(source))
                    source.Tap();
            }

            if (activated)
                RuleHooks.OnTutorialEffectActivated?.Invoke(player, source); // 教学探针（2026-10-23 activate 步完成信号）

            return activated;
        }

        /// <summary>
        /// 响应窗口内发动主动效果（2026-10-17 连锁门禁收敛定案）：与 ActivateEffect 同构，
        /// 差异=不设「回合方+主阶段」闸（那是非响应场景的时点语义，不是连锁条件——防守方凭
        /// 速度 &gt; 记速器在攻宣窗口连锁 2 速主动效果正是本口要放行的场景），改设栈上下文守卫
        /// （有对象可响应且不在结算中——泵上下文天然满足，防御性保留）。
        /// 速度门权威仍在 StackEngine.TryActivateEffect（回合方 ≥ / 非回合方严格大于）。
        /// 发动代价同口径：结界禁主动；源横置=每回合一次闸；发动成功横置（ShouldTap）。
        /// </summary>
        public static bool ActivateEffectInResponse(GameCore core, Player player, EffectDefinition effect, Card source)
            => ActivateEffectInResponse(core, player, effect, source, out _);

        /// <summary>带原因版（2026-11-03 失败提示细化）：Crumb 取证保留，同步升级 reason。</summary>
        public static bool ActivateEffectInResponse(GameCore core, Player player, EffectDefinition effect, Card source, out string reason)
        {
            reason = null;
            if (core == null || player == null || effect == null || source == null) { reason = "内部错误：空参数"; return false; }
            if (core.StackEngine.IsEmpty || core.StackEngine.IsResolving)
            {
                reason = "响应窗口已关闭（栈空或结算中）";
                Crumb($"CF-activate-reject def={effect.Id} reason=响应窗口已关闭（栈空或结算中）");
                return false;
            }

            // 禁发令硬闸（2026-10-29）：与 ActivateEffect 同口径——网络 intent 不经引擎 CanActivate
            if (source != null && RuleAuraSystem.ScopeHits(RuleAuraComponents.NoActivateAura, source.GetController()))
            {
                reason = "禁发令生效中——不能发动主动效果";
                Crumb($"CF-activate-reject def={effect.Id} reason=禁发令 by={player?.Name}");
                return false;
            }

            if (effect.IsActivatedEffect)
            {
                if (Attribute.CounterRules.UsesDurabilityEconomy(source))
                {
                    reason = "结界不能主动发动效果";
                    Crumb($"CF-activate-reject def={effect.Id} reason=结界禁主动 src={source.ID}");
                    return false;
                }
                if (source.IsTapped())
                {
                    reason = "该卡已横置（本回合已发动过）";
                    Crumb($"CF-activate-reject def={effect.Id} reason=源已横置 src={source.ID}");
                    return false;
                }
            }

            var pending = PendingEffect.Create(
                effect,
                source,
                player,
                core.TurnEngine.TurnPlayer,
                core.TurnEngine.CurrentPhase?.Phase ?? PhaseType.Standby);

            var activated = core.StackEngine.PlayerActivateVoluntary(pending);
            if (!activated)
            {
                reason = $"发动入栈被拒（速度门/栈结算中，记速器 {core.StackEngine.SpeedCounter.CurrentSpeed}）";
                Crumb($"CF-activate-reject def={effect.Id} reason=速度门或入栈失败 ctr={core.StackEngine.SpeedCounter.CurrentSpeed} stack={core.StackEngine.StackSize}");
                return false;
            }

            // 发动成功 → 消耗发动代价（与 ActivateEffect 同口径：横置=每回合一次闸）
            if (effect.IsActivatedEffect && Attribute.KeywordRules.ShouldTap(source))
                source.Tap();

            return true;
        }

        /// <summary>
        /// 玩家 Pass 优先权
        /// </summary>
        public static bool PassPriority(GameCore core, Player player)
        {
            if (core == null || player == null) return false;
            if (core.StackEngine.CurrentPriorityHolder != player) return false;

            // Pass 可能触发结算，结算含异步原子效果（await UI）。
            // 本入口返回校验结果，结算在后台推进（StackEngine.IsResolving 阻止主循环重入）。
            ForgetSafeAsync(core.StackEngine.PassPriority(player), "PassPriority 优先权让过", core).Forget();
            return true;
        }

        /// <summary>
        /// fire-and-forget 安全包装（2026-11-05 收口）：栈/响应窗结算泵后台推进的异常不再被
        /// UniTask.Forget 静默吞（否则以 UnobservedTaskException 形态漏出、对局日志无因可查）——
        /// 捕获后 TideLog.Error（带栈顶卡名/效果名定位来源）并继续。不改变结算顺序与控制流：
        /// 异步链在 catch 处截断，与裸 Forget 同语义，只是多一条错误日志。
        /// </summary>
        internal static async Cysharp.Threading.Tasks.UniTask ForgetSafeAsync(
            Cysharp.Threading.Tasks.UniTask task, string scope, GameCore core)
        {
            try { await task; }
            catch (Exception ex)
            {
                TideLog.Error($"[结算] {scope} 后台链异常（{DescribeStackTop(core)}）：{ex}");
            }
        }

        /// <summary>异常定位用：栈顶条目的卡名/效果名快照（栈空=标注空栈）。</summary>
        private static string DescribeStackTop(GameCore core)
        {
            var top = core?.StackEngine?.GetStackContents()?.LastOrDefault(e => e != null);
            if (top == null) return "栈空";
            var cardName = (top.Source as IHasName)?.CardName ?? (top.CastCard as IHasName)?.CardName;
            var effName = top.Definition?.DisplayName ?? top.Definition?.Id;
            return $"卡[{cardName ?? top.Source?.ToString() ?? "?"}] 效果[{effName ?? "?"}]";
        }

        // 苏醒机制已整链删除（2026-11-02）：灰身材概念已不存在，「免灰抵沉睡」失去意义；
        // 且与倒计时玩法过近。CommitAwaken（立约，2026-10-29 退役）→ awakenCast 施放变体
        //（P6/P6c）→ P12 关键词化（GrantAwaken 原子+HasAwakenKeyword 闸）全链不再存在，
        // 沉睡指示物仍由 Sleep 原子等常规路径施加。

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
        /// （2026-11-02 苏醒机制整链删除：末段「苏醒豁免」层退役。）
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
                var modeCosts = modeDef?.Costs != null
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

        // HasSelfSleepEffect / ContainsWaivableSleepAtom / GetAwakenSleepLayers /
        // AwakenExceedsGrayLands / HasAwakenKeyword 已随 2026-11-02 苏醒机制整链删除。
        // CardData.SelfSleepEffectCache 字段随之失去消费方（字段本身归 CardData/P8 管，留待彼包清理）。

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
        /// 代价可支付=CostPayability.AllPayable（与 EvaluatePayloadGate 同口径，两处单源）。
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
            if (specialCosts == null || specialCosts.Count == 0) return true;
            return CostPayability.AllPayable(specialCosts, new CostContext
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

    /// <summary>
    /// 充能事件（2026-11-05 充能系统动作化改版）：每次充能结算发布——UI/AI 可观测的进度变化信号
    ///（充能=速度0系统动作，免费落层不支付元素；进度本身=四色费用降低指示物层数，换区清除随
    /// CounterChangedEvent/快照可见。旧 P12「现付 1 点该色元素」已随改版退役）。
    /// </summary>
    public class CardChargedEvent : GameEventBase
    {
        public Player Player { get; set; }
        public Card Card { get; set; }
        /// <summary>本次充能的颜色（四色之一——任意不可充）。</summary>
        public ManaType Color { get; set; }
        /// <summary>本次充能后该卡该色累计已充层数（=同色费用降低指示物层数）。</summary>
        public int PaidOnColor { get; set; }
    }

    /// <summary>
    /// CardData 包装标记接口
    /// </summary>
    public interface CardDataWrapper { }
}
