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
        {
            if (core == null || player == null || card == null) return false;
            if (core.TurnEngine.TurnPlayer != player) return false;
            if (core.TurnEngine.CurrentPhase?.Phase != PhaseType.Main) return false;

            // 检查卡牌在手中
            var hand = core.ZoneManager.GetCards(player, Zone.Hand);
            if (!hand.Contains(card)) return false;

            // 教学引导闸（2026-10-06 第二课）：放地步骤语义优先（非引导卡拒绝）
            var guideReject = RuleHooks.TutorialElementGate?.Invoke(player, card);
            if (guideReject != null)
            {
                TideLog.Warn($"[GameActions] 放地被教学引导拒绝：{guideReject}");
                return false;
            }

            // 放入元素池（各自张数 ≤ 地牌上限；锁色自选/缺省主色）
            var elementPool = core.ElementPool;
            if (!elementPool.AddCardToPool(card, player, colorChoice))
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

        // ======================================== 充能（2026-10-31 P12：建造→充能；主动效果、速度 0、只能从手牌发动） ========================================

        /// <summary>
        /// 充能可用性单源（P12 §B2）：UI 置灰 / AI 枚举 / 引擎终审三步同口径——ActivateCharge 内部先过本口。
        /// 门禁序：回合方 → 主阶段 → 卡在手牌 → 卡有充能效果（ComposerCatalog.HasChargeEffect——
        /// 生物/法术/结界全类别）→ 色合法（灰/红/蓝/绿，Any 拒）→ 该色「代价抵扣前卡费」&gt;0 →
        /// 该色已充层数 &lt; 该色上限（进度=四色费用降低指示物层数，GetPrePayloadCost 逐色）→
        /// 元素池可付 1 点该色（CanPayCostWithAutoTap 现取现用）。
        /// 充能不消耗行动次数、不限次数/回合：还有可通过此方式减免的元素就能继续充。
        /// </summary>
        public static bool CanCharge(GameCore core, Player player, Card card, ManaType color, out string reason)
        {
            reason = null;
            if (core == null || player == null || card == null) { reason = "内部错误：空参数"; return false; }
            if (core.TurnEngine.TurnPlayer != player) { reason = "充能只能在自己的回合"; return false; }
            if (core.TurnEngine.CurrentPhase?.Phase != PhaseType.Main)
            { reason = $"充能只能在主阶段（当前：{core.TurnEngine.CurrentPhase?.Phase}）"; return false; }

            var hand = core.ZoneManager.GetCards(player, Zone.Hand);
            if (!hand.Contains(card)) { reason = "充能对象必须在手牌"; return false; }

            var data = (card as CardWrapper)?.GetData();
            if (data == null || !ComposerCatalog.HasChargeEffect(data))
            { reason = "该卡没有充能效果（充能入口只对带「充能」效果的卡开放）"; return false; }

            if (Attribute.CounterRules.CostDownCounterFor(color) == null)
            { reason = "任意元素是万用货币不是颜色，不可充能"; return false; }

            // 上限口径=代价抵扣前逐色卡费（旧建造同源，GetPrePayloadCost 单口）；
            // 进度=同色费用降低指示物层数（Resident 换区清——实例级，同名副本/跨玩家互不共享）。
            // 法术补差（P12 落地）：卡费基数不含施放锚价（主动效果运行时现付——法术出牌费≈0），
            // 并入模式 0 锚价（数据契约「模式0=最高消耗」；层在 GetCardCost 于锚价合并后套算、
            // 逐色钳 0——对低锚模式最多折到 0，无白赚）。
            CardCostService.GetPrePayloadCost(data, 0, out var preCost, out _);
            float costColor = preCost[color];
            if (data.Supertype == Cardtype.Spell)
                costColor += SpellCastAnchor(card, 0)[color];
            if (costColor <= 0f)
            { reason = $"该卡卡费中没有{ManaZh(color)}——只能充卡费里确实有的色"; return false; }
            int charged = card.GetCounterCount(Attribute.CounterRules.CostDownCounterFor(color));
            if (charged >= costColor)
            {
                reason = $"{ManaZh(color)}已充能满（已充 {charged}/{costColor:0.#}）——该色无剩余可充额度";
                return false;
            }

            if (!core.ElementPool.CanPayCostWithAutoTap(ElementCost.FromValue(color, 1), player))
            { reason = $"元素不足——充能需现付 1 {ManaZh(color)}（临时钱包优先，不足则系统自动横置地牌）"; return false; }
            return true;
        }

        /// <summary>
        /// 充能发动（P12 §B2）：把该卡的充能效果作为**速度 0 的主动效果**推入自愿桶
        ///（PlayerActivateVoluntary → 响应窗口 → ChargeHandler 结算：现付 1 点所选色 +
        /// 放 1 层同色费用降低指示物 + 展示 + CardChargedEvent）。不横置、不消耗耐久、不换区
        /// ——卡留在手牌；入栈失败（速度门）不改状态。ChargeColor 随 PendingEffect →
        /// EffectInstance → context 透传到执行期。
        /// </summary>
        public static bool ActivateCharge(GameCore core, Player player, Card card, ManaType color, out string reason)
        {
            if (!CanCharge(core, player, card, color, out reason)) return false;

            EffectDefinition chargeDef = null;
            foreach (var def in GetCardEffectDefinitions(card))
            {
                if (def != null && ComposerCatalog.IsChargeEffect(def)) { chargeDef = def; break; }
            }
            if (chargeDef == null)
            { reason = "该卡没有充能效果（效果转换后缺失——数据异常）"; return false; }

            var pending = PendingEffect.Create(
                chargeDef, card, player, core.TurnEngine.TurnPlayer,
                core.TurnEngine.CurrentPhase?.Phase ?? PhaseType.Main);
            pending.ActivationSpeed = 0; // 充能恒 0 速（主动效果——主阶段空栈首动豁免内）
            pending.ActivationType = EffectActivationType.Voluntary;
            pending.ChargeColor = color;

            if (!core.StackEngine.PlayerActivateVoluntary(pending))
            { reason = "充能入栈失败（速度门——栈上有待响应对象）"; return false; }
            return true;
        }

        /// <summary>手牌卡充能数据口（P12 §B3：给数据不做画面——战场/手牌怎么画归用户）。
        /// 消费面：本地对局=引擎卡直读；联机客户端=快照计数器（CounterEntryDTO 已覆盖四色层）。</summary>
        public sealed class HandCardChargeInfo
        {
            /// <summary>已充层数 [灰,红,蓝,绿,任意]（任意位恒 0；换区清零、跨回合保留）。</summary>
            public int[] ChargedByColor;
            /// <summary>时点窗口总闸：回合方+主阶段+手牌+有充能效果（不含颜色/余量——UI 置灰第一层）。</summary>
            public bool WindowOpen;
            /// <summary>当前净费（GetCardCost 同源——已含四色费用降低层与代价抵扣）。</summary>
            public ElementCost NetCostNow;
            /// <summary>可充色 → 还能充几点（代价抵扣前卡费上限口径；0=充满仍列出便于置灰）。</summary>
            public Dictionary<ManaType, int> RemainingByColor = new Dictionary<ManaType, int>();
            /// <summary>可充色 → 元素池现在能否再付 1 点（CanCharge 余量判定同口径）。</summary>
            public Dictionary<ManaType, bool> PayableNow = new Dictionary<ManaType, bool>();
            /// <summary>可充色 → 再充 1 点该色后的净费预览（当前净费该色 −1 下限 0——不动卡状态；充满色=当前净费）。</summary>
            public Dictionary<ManaType, ElementCost> NetCostAfterCharge = new Dictionary<ManaType, ElementCost>();
        }

        /// <summary>
        /// 手牌卡充能数据（P12 §B3）：已充层数 / 可充颜色列表（每色还能充几点）/ 充能后净费预览。
        /// 无充能效果/异常入参 → 空信息（WindowOpen=false、空表）——UI 按总闸隐藏充能入口即可。
        /// modeIndex 仅抉择卡有意义（缺省 0 与 CanCharge 上限口径一致）。
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
            info.WindowOpen = core != null && player != null && card != null && data != null
                && ComposerCatalog.HasChargeEffect(data)
                && core.TurnEngine.TurnPlayer == player
                && core.TurnEngine.CurrentPhase?.Phase == PhaseType.Main
                && core.ZoneManager.GetCards(player, Zone.Hand).Contains(card);
            if (data == null || !ComposerCatalog.HasChargeEffect(data)) return info;

            CardCostService.GetPrePayloadCost(data, modeIndex, out var preCost, out _);
            // 法术补差（P12，与 CanCharge 同口径）：上限并入模式 0 施放锚价（卡费基数不含锚）
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
                info.RemainingByColor[color] = Math.Max(0, cap - charged);
                info.PayableNow[color] = core != null && player != null
                    && core.ElementPool.CanPayCostWithAutoTap(ElementCost.FromValue(color, 1), player);
                // 充能后净费预览：当前净费读数已含进度层，手动 −1 该色（下限 0）即可，不动卡状态
                if (charged < cap)
                {
                    var after = info.NetCostNow.Clone();
                    after[color] = Math.Max(0f, after[color] - 1f);
                    info.NetCostAfterCharge[color] = after;
                }
                else
                    info.NetCostAfterCharge[color] = info.NetCostNow.Clone();
            }
            return info;
        }

        /// <summary>
        /// 主阶段：发动英雄技能（2026-10-15 改版：技能=法术，每回合一次，>1 效果时弹窗择定）。
        /// 逻辑全在 HeroSkillSystem.ActivateAsync（守卫+计费+效果）；effectIndex 缺省 0（单效果卡免抉择）。
        /// </summary>
        public static async Cysharp.Threading.Tasks.UniTask<bool> ActivateHeroSkill(GameCore core, Player player, int effectIndex = 0)
            => await HeroSkillSystem.ActivateAsync(core, player, effectIndex);

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
        /// awakenCast（2026-10-29 P6 苏醒改版；同日 P6c 修订：只免灰）：苏醒施放——生物免净费中的
        /// 灰份额（绿照付），等量转入场沉睡层数并横置登场（净费=GetCardCost(card, modeIndex, awakenCast) 单源）。
        /// </summary>
        public static bool PlayCard(GameCore core, Player player, Card card, List<Entity> targets = null, Zone fromZone = Zone.Hand, int modeIndex = 0, bool awakenCast = false)
            => PlayCard(core, player, card, targets, fromZone, modeIndex, awakenCast, out _);

        /// <summary>同 <see cref="PlayCard(GameCore,Player,Card,List{Entity},Zone,int,bool)"/>，附带精确拒绝原因（网络 Error 帧直读——2026-09-26 联机排查：通用「门禁」文案无法区分 费用/目标/时点）。</summary>
        public static bool PlayCard(GameCore core, Player player, Card card, List<Entity> targets, Zone fromZone, int modeIndex, bool awakenCast, out string rejectReason)
        {
            rejectReason = null;
            if (core == null || player == null || card == null) { rejectReason = "内部错误：空参数"; return false; }
            if (core.TurnEngine.TurnPlayer != player) { rejectReason = "现在不是你的回合"; return false; }
            if (core.TurnEngine.CurrentPhase?.Phase != PhaseType.Main)
            { rejectReason = $"出牌只能在你的主阶段（当前：{core.TurnEngine.CurrentPhase?.Phase}）"; return false; }

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

            // 苏醒施放闸（2026-10-29 P6 定案；P6c 修订只免灰；2026-10-31 P12 关键词化）：
            // 仅卡面带 Awaken 关键词的生物且净费灰份额 ≥1（=入场沉睡层数，GetAwakenSleepLayers 单源闸
            //——关键词闸在彼口内）；灰=0 无可免不该白给沉睡——不提供苏醒选项（UI/AI 枚举同判，此处终审）。
            if (awakenCast && GetAwakenSleepLayers(card, modeIndex) <= 0)
            {
                var dataAw = card is CardWrapper wAw ? wAw.GetData() : null;
                rejectReason = dataAw != null && dataAw.Supertype == Cardtype.Creature && HasAwakenKeyword(card)
                    ? "净费中灰为 0，无可苏醒"
                    : "该卡没有苏醒关键词（仅带「苏醒」的生物可苏醒施放）";
                return false;
            }

            // 规则扩展点（OCP）：出牌限制（如信息轴锁定）经注册表询问
            if (!RuleHooks.CanPlay(core, player, card, fromZone))
            { rejectReason = "出牌受限（规则锁定）"; return false; }

            // 目标域预检（2026-09-10 定案：运行时无候选不可发动）
            if (!TargetDomainService.HasPlayableTargets(core, player, card, modeIndex))
            { rejectReason = "无合法目标（当前场上没有该卡可选的对象）"; return false; }

            // 出牌两阶段（2026-10-04 定案）：有代价的卡**先算代价**——声明期预选代价目标并验证可执行；
            // 代价无目标 → 卡的发动无效（拒发）。无代价卡照常只过费用门禁。
            if (!TryPrepareCardCostTargets(core, player, card, out var costGateReject))
            { rejectReason = costGateReject; return false; }

            // 规则扩展点（OCP）：非手牌来源（如墓地视手牌使用）经注册表取得并占用配额
            if (fromZone != Zone.Hand)
            {
                var playSource = RuleHooks.GetPlaySource(fromZone);
                if (playSource == null || !playSource.TryBeginUse(player))
                { rejectReason = $"{fromZone}使用配额已用尽（每回合一次）"; return false; }
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
            var cost = GetCardCost(card, modeIndex, awakenCast);
            if (!CanAffordCast(core, player, card, modeIndex, awakenCast))
            { rejectReason = CostRejectText(core, player, cost); return false; }

            // ---- 声明（使用时点）：设控制者 → 进发动区 → cast 上栈 → 使用宣言（对手获得响应窗口） ----
            card.SetController(player);
            card._pendingCastFromZone = fromZone; // 回退锚（2026-10-04 本轮：代价无有效目标时原路退回）

            core.ZoneManager.MoveCard(card, player, fromZone, Zone.Activation);
            core.PublishEvent(new CardEnterActivationEvent
            {
                Card = card,
                Controller = player,
                FromZone = fromZone
            });

            if (!core.StackEngine.PushCardCast(card, player, targets, modeIndex))
            {
                // 理论不可达（声明预检已过）；保守回退：卡退回来源区，声明失败不付费
                CastReturnToOrigin(core, card, player, fromZone);
                rejectReason = "栈机器拒入（结算中或速度门）";
                return false;
            }

            // 苏醒施放声明标记（2026-10-29 P6）：cast 结算（ResolveCardCastAsync）一次性消费——
            // 付费按苏醒净费（P6c：只免灰、彩色含绿照付），入场等量转沉睡指示物。每次声明整体覆写（含 false）。
            card._awakenCastPending = awakenCast;

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

            // 禁扰令（2026-10-29 阶段2；2026-10-30 更名·改单侧）：受光环影响一方不能在对手回合
            // 施放卡牌——只拦响应出牌（PlayCard 本身已要求回合方，不加）；主动效果/守卫不受限。
            if (player != core.TurnEngine.TurnPlayer
                && RuleAuraSystem.ScopeHits(RuleAuraComponents.OwnTurnCastAura, player))
            { rejectReason = "禁扰令：不能在对手回合施放"; return false; }

            var hand = core.ZoneManager.GetCards(player, Zone.Hand);
            if (!hand.Contains(card)) { rejectReason = "卡不在手牌（快照过期？请重试）"; return false; }

            // 规则扩展点（OCP）：出牌限制经注册表询问（响应出牌同样受限，如信息轴锁定）
            if (!RuleHooks.CanPlay(core, player, card, Zone.Hand))
            { rejectReason = "出牌受限（规则锁定）"; return false; }

            // 同名仪典唯一闸（同 PlayCard——响应出牌同门，2026-10-07 唯一性定案）
            if (RuleAuraSystem.BlocksDuplicatePlay(card))
            { rejectReason = "场上已存在同名场地光环（同名仪典不可重复——载体离场前不能再打出）"; return false; }

            // 出牌两阶段（2026-10-04 定案）：有代价先算代价——响应出牌同门（代价无目标→拒发）
            if (!TryPrepareCardCostTargets(core, player, card, out var costGateReject))
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
            card._pendingCastFromZone = Zone.Hand; // 回退锚（2026-10-04 本轮：代价无有效目标时原路退回；响应路径来源恒手牌）
            card._awakenCastPending = false; // 响应出牌无苏醒变体（声明覆写，防上一手声明残留）

            core.ZoneManager.MoveCard(card, player, Zone.Hand, Zone.Activation);
            core.PublishEvent(new CardEnterActivationEvent
            {
                Card = card,
                Controller = player,
                FromZone = Zone.Hand
            });

            if (!core.StackEngine.PushCardCast(card, player, targets, modeIndex))
            {
                core.ZoneManager.MoveCard(card, player, Zone.Activation, Zone.Hand);
                rejectReason = "速度门拒绝（响应卡速度须高于记速器）";
                return false;
            }

            core.PublishEvent(new CardPlayEvent
            {
                Player = player,
                PlayedCard = card,
                FromZone = Zone.Hand,
                ModeIndex = modeIndex
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
        public static bool PlayCardFromDeckTop(GameCore core, Player player, Card card, List<Entity> targets = null, int modeIndex = 0, bool awakenCast = false)
            => PlayCard(core, player, card, targets, Zone.Deck, modeIndex, awakenCast);

        // ======================================== 整卡施放结算（使用时点消费点） ========================================

        /// <summary>
        /// 整卡施放的栈结算——「此卡被使用」的消费点（Option Y 定案）：
        /// 1. 卡已不在发动区（被「发动无效」送墓等）→ 中止：不付费、不结算（发动层无效达成）；
        /// 2. 付费（扣费在响应窗口之后，从 bank 扣）；
        /// 3. 卡 _isNegated（被「效果无效」置位）→ 移入墓地、跳过效果、费用不退（效果层无效达成）；
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

            // 苏醒施放标记消费（2026-10-29 P6；P6c 只免灰）：声明期 PlayCard 写入，此处一次性读取并清空——
            // 付费按苏醒净费（免灰、彩色含绿照付），入场由 ResolvePermanentEntry 等量转沉睡指示物；
            // 发动无效路径（下方 1 早退）同消费，标记不跨次使用泄漏。
            bool awakenCast = card._awakenCastPending;
            card._awakenCastPending = false;

            // 1. 发动无效中止：卡已不在发动区 → 不付费、不结算（净效果=扣费返还）
            if (!core.ZoneManager.IsCardInZone(card, player, Zone.Activation))
                return;

            // 2. 付费（响应窗口之后）——抉择卡按声明期选定的模式付费（cast.ModeIndex）。
            // 顺序（2026-10-28 P1 定案：代价=构筑期抵扣、不再产出元素）：
            // ① 代价目标存活校验——预选目标响应窗口内全灭/离场/不可指定 → 发动失败，
            //    卡回退声明来源区（手牌路径回手牌），不付费、不执行；
            // ② 先扣卡费（净费=推导费−代价抵扣，已并入 GetCardCost）——现取现用
            //    （钱包优先消耗+系统自动横置地牌补足）；付不出 → 入墓不回卷；
            // ③ 代价强制执行（只执行、不发放）。
            // 被「效果无效」只跳效果、已付代价不回卷；被「发动无效」则全免（上方 1 已拦）。
            var specialCosts = CollectCardSpecialCosts(card);
            var cost = GetCardCost(card, cast.ModeIndex, awakenCast);
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

            // 3. 效果无效：付了但被否定 → 入墓、跳过效果、费用不退。
            // 发动表演照播（2026-10-20 用户定案：效果无效≠发动无效——费已付，弹体照飞到预选目标；
            // 效果不结算=无伤害/死亡事件，表现层仅飞行+到达）。发动无效（①卡已离发动区）在上方
            // 早退：不付费不结算，什么都不播。
            if (card._isNegated)
            {
                card._isNegated = false; // 消费即复位（同一否定标记只拦一次）
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
                await ResolveSpellEffectsAsync(core, player, card, cast.Targets, cast.ModeIndex);
            }
            else
            {
                ResolvePermanentEntry(core, player, card,
                    awakenCast ? GetAwakenSleepLayers(card, cast.ModeIndex) : 0);
            }

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

        /// <summary>cast 结算中止（支付失败 / 发动无效）：离开发动区入墓 + 失败/离区事件。</summary>
        private static void CastAbortToGraveyard(GameCore core, Card card, Player player, string reason)
        {
            core.ZoneManager.MoveCard(card, player, Zone.Activation, Zone.Graveyard);
            core.PublishEvent(new CardActivationFailedEvent
            {
                Card = card,
                Controller = player,
                FromZone = Zone.Activation,
                Reason = reason
            });
            core.PublishEvent(new CardLeaveActivationEvent
            {
                Card = card,
                Controller = player,
                ToZone = Zone.Graveyard
            });
        }

        /// <summary>
        /// cast 结算中止·回退声明来源区（2026-10-04 本轮定案：代价无有效目标 → 发动失败回手牌——
        /// 手牌出牌路径即回手牌；墓地视手牌使用路径原路回墓地，不白赚挪区）。发生在扣费前：不付费、不补偿。
        /// </summary>
        /// <summary>声明失败回退原区（2026-10-29 阶段3b 牌库顶当手牌来源）：牌库来源回牌库**顶**
        ///（原位——MoveCard 默认追加末尾=牌库底，白送换牌）；其余来源常规回退。</summary>
        private static void CastReturnToOrigin(GameCore core, Card card, Player player, Zone fromZone)
        {
            if (fromZone == Zone.Deck)
                core.ZoneManager.GetZoneContainer(player).Move(card, Zone.Activation, Zone.Deck, DeckPosition.Top);
            else
                core.ZoneManager.MoveCard(card, player, Zone.Activation, fromZone);
        }

        private static void CastAbortToOrigin(GameCore core, Card card, Player player, Zone fromZone, string reason)
        {
            if (fromZone == Zone.None || fromZone == Zone.Activation) fromZone = Zone.Hand;
            CastReturnToOrigin(core, card, player, fromZone);
            core.PublishEvent(new CardActivationFailedEvent
            {
                Card = card,
                Controller = player,
                FromZone = Zone.Activation,
                Reason = reason
            });
            core.PublishEvent(new CardLeaveActivationEvent
            {
                Card = card,
                Controller = player,
                ToZone = fromZone
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
                var current = Attribute.EffectHandlerRegistry.ResolveCandidates(atom.TargetKinds, atom.Filter, resolveCtx);
                if (current != null) current.RemoveAll(e => ReferenceEquals(e, card)); // 本卡已在发动区：不作代价目标
                if (current == null || current.Count == 0) return true; // 该原子域已空：必然全灭
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
        /// sleepLayers（2026-10-29 P6 苏醒施放；P6c 只免灰）：入场成功后等量挂沉睡指示物并横置登场——
        /// 复用沉睡指示物既有语义（无法重置+效果无效、持有者回合末 −1 归零解除），不新造状态。
        /// </summary>
        private static void ResolvePermanentEntry(GameCore core, Player player, Card card, int sleepLayers = 0)
        {
            // 发动通过 → 入场（声明期预检已过；满则入墓的兜底在 helper 内）
            if (!core.ZoneManager.TryMoveToBattlefield(card, player, Zone.Activation))
                return; // 入场失败（满场兜底入墓）：不挂沉睡

            if (sleepLayers > 0)
            {
                card.Tap();
                card.AddCounters(Attribute.KeywordRules.SleepCounter, sleepLayers, player);
                core.PublishEvent(new TapEvent { TappedEntity = card, IsUntapping = false });
                core.PublishEvent(new Attribute.CounterChangedEvent
                {
                    Target = card,
                    CounterType = Attribute.KeywordRules.SleepCounter,
                    Amount = sleepLayers,
                    Source = player,
                });
            }
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

                // 抉择按各自声明的模式；苏醒施放按苏醒净费计承诺（标记随声明，2026-10-29 P6）
                var cost = GetCardCost(castCard, obj.ModeIndex, castCard._awakenCastPending);
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
            //    单源算速（BaseSpeed+疾风 OnBoardActiveSpeedBonus）→ 速度门 →
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
                    if (!engine.SpeedCounter.CanActivate(pending.ActivationSpeed, EffectActivationType.Voluntary,
                            engine.IsEmpty)) continue;
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
                    // 禁扰令（2026-10-29 阶段2）：非回合方不能施放——手牌响应候选跳过
                    //（与 PlayCardInResponse 硬闸同口径；守卫/主动效果候选不受限）
                    if (p != core.TurnEngine.TurnPlayer
                        && RuleAuraSystem.ScopeHits(RuleAuraComponents.OwnTurnCastAura, p)) continue;
                    if (c == null || !RuleHooks.CanPlay(core, p, c, Zone.Hand)) continue;
                    if (RuleAuraSystem.BlocksDuplicatePlay(c)) continue;
                    if (GetPayloadGateReject(core, p, c) != null) continue;
                    // 本轮窗口撤销记忆（2026-10-20 定案）：连锁内取消过声明期目标选择的卡
                    // 不再进候选——本次连锁不再询问该卡是否发动（防取消→重弹循环）
                    if (ResponseWindowService.IsDeclined(c)) continue;
                    int castSpeed = SpeedCalculator.GetCardCastSpeed(c);
                    if (!engine.SpeedCounter.CanActivate(castSpeed, EffectActivationType.Voluntary, false))
                        continue;
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
                    if (options.Count > 0)
                    {
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

                    await engine.PassPriority(holder); // 0 候选/放弃/失败 → Pass（双 Pass 触发结算并 await 完成）
                }
            }
            finally
            {
                Crumb("CF-pump-exit");
            }
        }

        /// <summary>应用所选响应：守卫=PushGuardDeclaration；主动=ActivateEffect；自愿效果=PlayerActivateVoluntary；
        /// 手牌卡=PlayCardInResponse（目标=声明期预选（2026-10-20 收集器下发）或 null——结算期反问单源）。</summary>
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

        /// <summary>内置 AI 响应启发（守卫决策，搬自原 DoGuardBlocks）：只拦打脸攻击——
        /// 能扛住的守卫里挑血最厚；玩家将被击杀时任何守卫都上。其他响应放弃（Pass）。</summary>
        private static ResponseOption DefaultAiRespond(GameCore core, Player defender, List<ResponseOption> options)
        {
            ResponseOption chosen = null, anyGuard = null;
            foreach (var o in options)
            {
                if (o.Kind != ResponseOption.ResponseKind.GuardAbility || o.SourceCard == null) continue;
                if (!(o.AttackInstance?.Targets is List<Entity> t && t.Count > 0 && t[0] is Player)) continue; // 只拦打脸
                if (anyGuard == null) anyGuard = o;
                int incoming = core.LayerEngine != null && o.AttackInstance.Source != null
                    ? core.LayerEngine.CalculatePower(o.AttackInstance.Source) : 0;
                if (o.SourceCard.GetLife() > incoming
                    && (chosen == null || o.SourceCard.GetLife() > chosen.SourceCard.GetLife()))
                    chosen = o;
            }
            if (chosen == null && anyGuard != null)
            {
                // 致命判定：任何守卫牺牲保命
                var t = anyGuard.AttackInstance?.Targets;
                int incoming = core.LayerEngine != null && anyGuard.AttackInstance?.Source != null
                    ? core.LayerEngine.CalculatePower(anyGuard.AttackInstance.Source) : 0;
                if (t != null && t.Count > 0 && t[0] is Player && defender.Life - incoming <= 0)
                    chosen = anyGuard;
            }
            return chosen;
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
            GameCore core, Player player, Card card, List<Entity> targets, int modeIndex = 0)
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
            if (chosen != null)
            {
                var executor = core.StackEngine.GetExecutor();
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
                }, skipElementCost: true);
            }

            // 结算完成 → 离开发动区入墓（终态与历史行为一致）
            core.ZoneManager.MoveCard(card, player, Zone.Activation, Zone.Graveyard);
            core.PublishEvent(new CardLeaveActivationEvent
            {
                Card = card,
                Controller = player,
                ToZone = Zone.Graveyard
            });
        }

        /// <summary>
        /// 收集一张卡的卡级特殊代价（2026-09-11：代价栏单卡单条定案——整卡效果声明的
        /// 非元素代价在 cast 付费步统一执行并补偿；主动能力的代价不在此列，发动时现付）。
        /// 2026-09-23 代价栏上移卡组合层：CardData.PayloadCost 为唯一正式口（合成器在卡层填装，
        /// 错侧改写已在放置口完成）；效果级 def.Costs 降为 legacy 兜底——卡层已填时跳过，防双收。
        /// </summary>
        private static List<CostInstance> CollectCardSpecialCosts(Card card)
        {
            var result = new List<CostInstance>();

            // 卡层代价栏（2026-09-23；2026-10-16 组装效果代价）：Payload 原子序列——逐条同口径转换后入列
            if (card is CardWrapper wrapper)
            {
                var pc = wrapper.GetData()?.PayloadCost;
                var pcAtoms = pc?.AllPayloads();
                if (pcAtoms != null && pcAtoms.Count > 0)
                {
                    var payloadAtoms = new List<AtomicEffectInstance>();
                    bool allOk = true;
                    foreach (var pe in pcAtoms)
                    {
                        var inst = CardEffectConverter.ConvertPayloadForDisplay(pe);
                        if (inst == null) { allOk = false; break; }
                        payloadAtoms.Add(inst);
                    }
                    if (allOk)
                    {
                        result.Add(new CostInstance
                        {
                            Type = CostType.Payload,
                            Value = pc.Value,
                            ManaType = (ManaType)pc.ManaType,
                            Payloads = payloadAtoms,
                        });
                        return result; // 卡层已填——legacy 效果级代价不再收集（单卡单条）
                    }
                    TideLog.Warn("[CardActions] 卡层代价栏 Payload 转换失败（表行缺失？）——按无代价处理");
                }
            }

            // legacy 兜底：效果级 Costs（旧数据——代价曾随效果合成）
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
        private static bool TryPrepareCardCostTargets(GameCore core, Player player, Card card, out string rejectReason)
        {
            rejectReason = EvaluatePayloadGate(core, player, card, out var payloadCost, out var candidatesByAtom);
            if (rejectReason != null)
            {
                card._pendingCostTargets = null;
                return false;
            }
            if (payloadCost == null)
            {
                card._pendingCostTargets = null; // 无代价：两阶段退化为一阶段（费用门禁）
                return true;
            }
            // 逐原子预选：candidatesByAtom 非空表=至少一个有域原子（无域原子位=null）；
            // 全无域原子=空外层表（无目标语义照常执行的口径不变）
            card._pendingCostTargets = candidatesByAtom;
            return true;
        }

        /// <summary>代价门槛纯检（AI LegalActionEnumerator 共用，不动卡状态）：null=通过，非 null=拒绝原因。</summary>
        public static string GetPayloadGateReject(GameCore core, Player player, Card card)
            => EvaluatePayloadGate(core, player, card, out _, out _);

        /// <summary>
        /// 代价门槛评估（2026-10-28 P1 定案，声明期与 AI 枚举同口；组装效果代价逐原子化）：
        /// ① 可支付门槛——CostPayability.AllPayable/RejectReason（弃牌→手牌数、流失生命→生命、
        ///    磨牌→牌库、有域原子→候选非空）；代价不可支付 → 整卡不可用（声明期闸）。
        ///    旧「全价过地牌槽上限」门槛已删（免费卡合法——净费 0 与本闸无关）。
        /// ② 目标门槛——有域原子逐个以支付者视角解析候选（剔除本卡），任一无候选 → 发动无效。
        /// 全无域原子则只过①（可发动，结算按无目标执行）。
        /// </summary>
        private static string EvaluatePayloadGate(GameCore core, Player player, Card card,
            out CostInstance payloadCost, out List<List<Entity>> candidates)
        {
            payloadCost = null;
            candidates = null;

            var specialCosts = CollectCardSpecialCosts(card);
            foreach (var c in specialCosts)
                if (c != null && c.Type == CostType.Payload && c.Payloads != null && c.Payloads.Count > 0) { payloadCost = c; break; }
            if (payloadCost == null) return null; // 无代价：两阶段退化为一阶段（费用门禁）

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

            // ② 目标门槛：按支付者视角逐原子解析候选（与结算期 ExecutePayloadAtomAsync 同口径）
            bool anyDomain = false;
            var byAtom = new List<List<Entity>>();
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
                if (list.Count == 0)
                    return "代价无目标（无法执行）——卡的发动无效";
                byAtom.Add(list);
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
        {
            if (core == null || player == null) return false;
            if (!core.TurnEngine.CanCombatAction()) return false;
            if (core.TurnEngine.TurnPlayer != player) return false;

            // 教学引导闸（2026-10-06）：先于常规资格——引导步骤语义优先（错误目标/锁定生物在此拒绝）
            var guideReject = RuleHooks.TutorialAttackGate?.Invoke(player, attacker, target);
            if (guideReject != null)
            {
                TideLog.Warn($"[GameActions] 攻击被教学引导拒绝：{guideReject}");
                return false;
            }

            var combat = core.CombatSystem;
            if (!combat.CanDeclareAttack(attacker, player)) return false;
            if (!combat.CanAttackTarget(attacker, target, player)) return false;

            if (!core.StackEngine.PushAttackDeclaration(attacker, target, player)) return false;
            RuleHooks.OnTutorialAttackDeclared?.Invoke(player, attacker, target);
            return true;
        }

        // ======================================== 回合控制 ========================================

        /// <summary>
        /// 结束回合
        /// </summary>
        public static bool EndTurn(GameCore core, Player player)
        {
            if (core == null || player == null) return false;
            if (core.TurnEngine.TurnPlayer != player) return false;

            // 教学引导闸（2026-10-06）：引导步骤未完成时钉住回合（必须先做引导动作）
            var guideReject = RuleHooks.TutorialEndTurnGate?.Invoke(player);
            if (guideReject != null)
            {
                TideLog.Warn($"[GameActions] 结束回合被教学引导拒绝：{guideReject}");
                return false;
            }

            // 真发判定（2026-10-20 教学双推进修复）：TurnEngine 静默拒绝（阶段非 Main/栈未空——
            // 同帧双击、回合翻转前重入的典型态）时**不发教学探针**——否则 OnTutorialTurnEnded
            // 连发两次，连续 end 步一击双进（段七实测：换场/考验武装提前、必杀直伤提前落地）。
            if (!core.TurnEngine.EndTurn())
            {
                TideLog.Warn("[GameActions] 结束回合被引擎拒绝（阶段已推进/连锁未结算）——不发教学探针");
                return false;
            }
            RuleHooks.OnTutorialTurnEnded?.Invoke(player);
            return true;
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
        {
            if (core == null || player == null || effect == null) return false;
            if (core.TurnEngine.TurnPlayer != player)
            {
                Crumb($"CF-activate-reject def={effect.Id} reason=非回合方 by={player?.Name}");
                return false;
            }
            if (core.TurnEngine.CurrentPhase?.Phase != PhaseType.Main)
            {
                Crumb($"CF-activate-reject def={effect.Id} reason=非主阶段({core.TurnEngine.CurrentPhase?.Phase})");
                return false;
            }

            // 禁发令硬闸（2026-10-29）：网络 intent 直调本口不经引擎 CanActivate——统一在此拦
            if (source != null && RuleAuraSystem.ScopeHits(RuleAuraComponents.NoActivateAura, source.GetController()))
            {
                Crumb($"CF-activate-reject def={effect.Id} reason=禁发令 by={player?.Name}");
                return false;
            }

            // 教学引导闸（2026-10-23 元素合成步）：资源步期间拦截玩家侧主动效果发动——
            // 防把后续步骤要用的效果提前烧掉（横置后步骤软锁）；AI 侧与响应路径不经此口
            var guideReject = RuleHooks.TutorialActivateGate?.Invoke(player, source, effect);
            if (guideReject != null)
            {
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
                    Crumb($"CF-activate-reject def={effect.Id} reason=结界禁主动 src={source.ID}");
                    return false; // 结界禁主动效果（2026-10-15 深夜改版）——耐久−1 通道整体退役
                }
                if (source.IsTapped())
                {
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
                Crumb($"CF-activate-reject def={effect.Id} reason=速度门或入栈失败 ctr={core.StackEngine.SpeedCounter.CurrentSpeed} stack={core.StackEngine.StackSize}");

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
        {
            if (core == null || player == null || effect == null || source == null) return false;
            if (core.StackEngine.IsEmpty || core.StackEngine.IsResolving)
            {
                Crumb($"CF-activate-reject def={effect.Id} reason=响应窗口已关闭（栈空或结算中）");
                return false;
            }

            // 禁发令硬闸（2026-10-29）：与 ActivateEffect 同口径——网络 intent 不经引擎 CanActivate
            if (source != null && RuleAuraSystem.ScopeHits(RuleAuraComponents.NoActivateAura, source.GetController()))
            {
                Crumb($"CF-activate-reject def={effect.Id} reason=禁发令 by={player?.Name}");
                return false;
            }

            if (effect.IsActivatedEffect)
            {
                if (Attribute.CounterRules.UsesDurabilityEconomy(source))
                {
                    Crumb($"CF-activate-reject def={effect.Id} reason=结界禁主动 src={source.ID}");
                    return false;
                }
                if (source.IsTapped())
                {
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
            core.StackEngine.PassPriority(player).Forget();
            return true;
        }

        // CommitAwaken（苏醒立约）已删（2026-10-29 P6 苏醒改版）：立约决策退役——
        // 所有生物都可随施放直接选「苏醒施放」（PlayCard awakenCast 参数：净费灰份额
        // 全免转入场沉睡层数，GetCardCost(card, modeIndex, awakenCast) 单源；P6c 修订只免灰），不再要求
        // 「带豁免时长自我沉睡效果」先在手牌预立约。

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
        /// awakenCast（2026-10-29 P6 苏醒改版，替代旧「立约豁免」；P6c 修订只免灰）：苏醒施放的
        /// 账单——净费中的灰份额全免（等量转入场沉睡层数，见 GetAwakenSleepLayers），彩色份额
        ///（含绿）照付、混付口径照旧走 ElementPool。非生物传 true 无效；灰=0 无免可免
///（退化回原净费，调用方不应提供苏醒选项）。预检/付费/pending/AI 特征/UI 预览均经本口
///——全消费面同口径。
        /// 出牌费用单源（2026-10-31 P12：建造 BuildPaid 重放整链退役——充能进度改挂四色费用降低
        /// 指示物，在下方指示物层统一套算）。层序恒定：恒推导基价 → 法术锚价 → 四色费用指示物层 →
        /// 苏醒豁免；预检/扣费/pending/UI 预览全消费面共用本口，不另开口径。
        /// </summary>
        public static ElementCost GetCardCost(Card card, int modeIndex = 0, bool awakenCast = false)
        {
            ElementCost cost;
            var data = (card as CardCore.CardWrapper)?.GetData();
            if (data != null)
                cost = CardCostService.GetRuntimeCost(data, modeIndex); // 恒推导（抉择=所选模式）
            else
                cost = ElementCost.FromValue(ManaType.Gray, 1); // 防御默认：无卡数据按灰1（正常链路不达）

            // 法术锚价并入（改版定案：发动按效果费用支付，无额外费用——见 SpellCastAnchor）
            if (data != null && data.Supertype == Cardtype.Spell)
            {
                var anchor = SpellCastAnchor(card, modeIndex);
                foreach (var color in anchor.NonzeroColors())
                    cost[color] = cost[color] + anchor[color];
            }

            // 费用指示物层（P1 恒灰 → 2026-10-31 P12 四色扩容）：灰 += CostUp − CostDown − CostDownGray
            //（旧 CostDown 恒灰 legacy）；红/蓝/绿各吃对应色 CostDown* 层。逐色单独钳 0，不串色。
            int costUp = card.GetCounterCount(Attribute.CounterRules.CostUpCounter);
            int grayDown = card.GetCounterCount(Attribute.CounterRules.CostDownCounter)
                         + card.GetCounterCount(Attribute.CounterRules.CostDownGrayCounter);
            int redDown = card.GetCounterCount(Attribute.CounterRules.CostDownRedCounter);
            int blueDown = card.GetCounterCount(Attribute.CounterRules.CostDownBlueCounter);
            int greenDown = card.GetCounterCount(Attribute.CounterRules.CostDownGreenCounter);
            if (costUp != 0 || grayDown != 0 || redDown != 0 || blueDown != 0 || greenDown != 0)
            {
                cost[ManaType.Gray] = Math.Max(0f, cost[ManaType.Gray] + costUp - grayDown);
                cost[ManaType.Red] = Math.Max(0f, cost[ManaType.Red] - redDown);
                cost[ManaType.Blue] = Math.Max(0f, cost[ManaType.Blue] - blueDown);
                cost[ManaType.Green] = Math.Max(0f, cost[ManaType.Green] - greenDown);
            }

            // 苏醒施放豁免（2026-10-29 P6；同日 P6c 修订：只免灰，绿照付；2026-10-31 P12 关键词化：
            // 仅卡面带 Awaken 关键词的生物可免——无双保险也不该给免）：
            // 免净费中的灰份额（先扣代价再算苏醒——上方 cost 已是「推导费−代价抵扣」的净费）。
            // 设计后果：灰=身材费——苏醒实际是「用沉睡抵身材费」（身体买得到、效果买不到）；
            // 绿重（效果费高）的生物几乎用不上苏醒，这是预期。豁免量不在此暂存
            //（旧 PendingSleepLayers 退役）——入场层数由 GetAwakenSleepLayers 同口径重取，与账单同刻推导。
            if (awakenCast && data != null && data.Supertype == Cardtype.Creature && HasAwakenKeyword(data))
            {
                cost[ManaType.Gray] = 0f;
            }
            return cost;
        }

        /// <summary>
        /// 苏醒施放可免的灰净费份额 = 入场沉睡层数（2026-10-29 P6 定案；同日 P6c 修订：只免灰，绿照付；
        /// 2026-10-31 P12 关键词化：**先判 Awaken 关键词**——没有该关键词的生物直接 0，不提供苏醒选项）：
        /// 苏醒施放免掉净费中的灰（先扣代价后算苏醒——GetCardCost 返回的已是净费），等量转沉睡指示物
        /// 并横置入场；层数=0（非生物 / 无关键词 / 净费灰=0）不提供苏醒选项（没有可免的，不该白给一个沉睡）。
        /// 绿是效果色，不在豁免面——绿重生物用不上苏醒属预期（灰=身材费，苏醒=用沉睡抵身材费）。
        /// 与 <see cref="GetCardCost(Card,int,bool)"/> 的豁免块同口径单源（本值=豁免块清零的量）。
        /// </summary>
        public static int GetAwakenSleepLayers(Card card, int modeIndex = 0)
        {
            var data = card is CardWrapper wrapper ? wrapper.GetData() : null;
            if (data == null || data.Supertype != Cardtype.Creature) return 0;
            if (!HasAwakenKeyword(card)) return 0; // P12 关键词闸：卡面（印刷/光环/授予轨）不带 Awaken 即无苏醒
            var cost = GetCardCost(card, modeIndex);
            int layers = (int)Math.Round(cost[ManaType.Gray]);
            return layers > 0 ? layers : 0;
        }

        /// <summary>苏醒层数预览（CardData 版——网络客户端无引擎卡实例时用）：按恒推导净费取灰
        ///（P6c：只灰，绿不计），不含费用指示物层（Card 实例态）。服务器终审以 Card 重载为准；
        /// 预览漂移只影响文案不影响账单。P12 关键词闸同 Card 重载：无 Awaken 关键词恒 0。</summary>
        public static int GetAwakenSleepLayers(CardData data, int modeIndex = 0)
        {
            if (data == null || data.Supertype != Cardtype.Creature) return 0;
            if (!HasAwakenKeyword(data)) return 0; // P12：网络预览同闸（印刷关键词口径——运行时授予轨不可见于 CardData）
            var cost = CardCostService.GetRuntimeCost(data, modeIndex);
            int layers = (int)Math.Round(cost[ManaType.Gray]);
            return layers > 0 ? layers : 0;
        }

        // ==================== 苏醒关键词闸（2026-10-31 P12 关键词化） ====================

        /// <summary>卡是否有「苏醒」关键词（印刷/Setting/Temp 轨 + 连接光环投递 + 缄默失效
        ///——HasKeyword 全口径）。P12 定案：只有带 Awaken 的生物才有「苏醒施放」入口。</summary>
        public static bool HasAwakenKeyword(Card card)
            => card != null && card.HasKeyword(Attribute.KeywordRules.Awaken);

        /// <summary>卡面定义是否含「苏醒」关键词（网络客户端无引擎卡实例时的同闸——印刷口径）。</summary>
        public static bool HasAwakenKeyword(CardData data)
            => data != null && data.Keywords != null && data.Keywords.Contains(Attribute.KeywordRules.Awaken);

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

        // HasSelfSleepEffect / ContainsWaivableSleepAtom 已删（2026-10-29 P6 苏醒改版）：
        // 苏醒不再要求「带豁免时长自我沉睡效果」——所有生物通用，层数=净费灰份额（P6c：只灰）
        //（GetAwakenSleepLayers）。CardData.SelfSleepEffectCache 字段随之失去消费方
        //（字段本身归 CardData/P8 管，留待彼包清理）。

        private static bool ContainsSleepAtom(IEnumerable<CardCore.AtomicEffectInstance> atoms)
        {
            foreach (var atom in atoms)
            {
                if (atom == null) continue;
                if (atom.Type == AtomicEffectType.Sleep) return true;
                if (atom.SubEffects != null && ContainsSleepAtom(atom.SubEffects)) return true;
            }
            return false;
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
        /// 代价可支付=CostPayability.AllPayable（与 EvaluatePayloadGate 同口径，两处单源）。
        /// 出牌声明预检（PlayCard/PlayCardInResponse）与 AI 枚举（LegalActionEnumerator）共用。
        /// awakenCast（2026-10-29 P6；P6c 只免灰）：苏醒施放同口径——账单换苏醒净费（免灰、
        /// 彩色含绿照付），CanPayCostWithAutoTap 只对该净费判定；代价侧不变（苏醒不减免代价）。
        /// </summary>
        public static bool CanAffordCast(GameCore core, Player player, Card card, int modeIndex, bool awakenCast = false)
        {
            if (core == null || player == null || card == null) return false;
            if (!CanAfford(core.ElementPool, GetCardCost(card, modeIndex, awakenCast), player,
                    GetPendingCastCosts(core, player)))
                return false;

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

        /// <summary>法术可施放效果定义（P12 C1：过滤充能效果、序=Effects 声明序）——
        /// 模式序号 modeIndex 索引本列表（SpellCastAnchor/目标收集/结算/VFX 色调全消费面同源；
        /// 与 CostDerivationService.GetCastableEffects(CardData) 同序——转换保序）。</summary>
        public static List<EffectDefinition> GetCastableEffectDefinitions(Card card)
        {
            var defs = GetCardEffectDefinitions(card);
            var castable = new List<EffectDefinition>();
            foreach (var def in defs)
                if (def != null && !ComposerCatalog.IsChargeEffect(def)) castable.Add(def);
            return castable;
        }
    }

    /// <summary>
    /// 充能事件（2026-10-31 P12：建造→充能，原 CardBuiltEvent 更名）：每次充能结算发布——
    /// UI/AI 可观测的进度变化信号（元素实付组合另见 ElementPoolPayEvent，SourceNote="充能:卡名"；
    /// 进度本身=四色费用降低指示物层数，换区清除随 CounterChangedEvent/快照可见）。
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
