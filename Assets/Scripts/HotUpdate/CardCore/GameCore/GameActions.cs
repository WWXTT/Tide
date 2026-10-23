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

            // 产色落**临时钱包**（任一回合结束清空）；受全类型累计钳（2026-10-16 改案：六色同池，
            // 配额=地牌上限−本周期累计——amount=1 常过钳，配额耗尽时实发 0）。事件走 core.PublishEvent
            // 路由路径（Trigger/Layer 引擎可见——AddMana 直发 EventManager 会绕开，故不换路）；
            // 台账经 CommitGainLedger 与 AddMana 单源。
            var pool = core.ElementPool.GetPool(player);

            creature.Tap();
            int allowed = core.ElementPool.PreviewGrantAmount(player, type, 1);
            if (allowed > 0)
            {
                ElementPoolSystem.CommitGainLedger(pool, type, allowed);
                pool.TempElements[type] += allowed;
                core.PublishEvent(new ElementPoolGainEvent
                {
                    Player = player,
                    GainedType = type,
                    FromCard = creature,
                    Amount = allowed,
                });
            }
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
            { rejectReason = "场上已存在同名规则光环（同名仪典不可重复——载体离场前不能再打出）"; return false; }

            // 检查卡牌在来源区
            var sourceZone = core.ZoneManager.GetCards(player, fromZone);
            if (!sourceZone.Contains(card)) { rejectReason = $"卡不在{fromZone}（快照过期？请重试）"; return false; }

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

            // 永久物：战场容量预检（满场不允许发动，SimpleAI 依赖 false 跳过；
            // 「结算时入场满则失败入墓」由 TryMoveToBattlefield 承担）
            if (!isSpell && !core.ZoneManager.HasBattlefieldSpace(player))
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
                core.ZoneManager.MoveCard(card, player, Zone.Activation, fromZone);
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

            // 规则扩展点（OCP）：出牌限制经注册表询问（响应出牌同样受限，如信息轴锁定）
            if (!RuleHooks.CanPlay(core, player, card, Zone.Hand))
            { rejectReason = "出牌受限（规则锁定）"; return false; }

            // 同名仪典唯一闸（同 PlayCard——响应出牌同门，2026-10-07 唯一性定案）
            if (RuleAuraSystem.BlocksDuplicatePlay(card))
            { rejectReason = "场上已存在同名规则光环（同名仪典不可重复——载体离场前不能再打出）"; return false; }

            // 出牌两阶段（2026-10-04 定案）：有代价先算代价——响应出牌同门（代价无目标→拒发）
            if (!TryPrepareCardCostTargets(core, player, card, out var costGateReject))
            { rejectReason = costGateReject; return false; }

            bool isSpell = card.IsSpellCard();
            if (!isSpell && !core.ZoneManager.HasBattlefieldSpace(player))
            { rejectReason = "战场已满（永久物无处入场）"; return false; }

            // 费用预检（含本玩家已声明的施放承诺；不支付——cast 结算时才扣；抉择按所选模式；
            // 2026-09-14 代价强制：原价账单预检，混付口径同 PlayCard；
            // 2026-10-16 恢复旧序：同 PlayCard 走 CanAffordCast——代价补偿黑/白不预计算垫本卡）
            var cost = GetCardCost(card, modeIndex);
            if (!CanAffordCast(core, player, card, modeIndex))
            { rejectReason = CostRejectText(core, player, cost); return false; }

            card.SetController(player);
            card._pendingCastFromZone = Zone.Hand; // 回退锚（2026-10-04 本轮：代价无有效目标时原路退回；响应路径来源恒手牌）

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
        /// 主阶段：从墓地使用一张牌，视为手牌中使用（引擎通用能力，经 IPlaySource 扩展点授权/计配额）。
        /// 法术结算后照常入墓、永久物入场。
        /// </summary>
        public static bool PlayCardFromGraveyard(GameCore core, Player player, Card card, List<Entity> targets = null, int modeIndex = 0)
            => PlayCard(core, player, card, targets, Zone.Graveyard, modeIndex);

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

            // 1. 发动无效中止：卡已不在发动区 → 不付费、不结算（净效果=扣费返还）
            if (!core.ZoneManager.IsCardInZone(card, player, Zone.Activation))
                return;

            // 2. 付费（响应窗口之后）——抉择卡按声明期选定的模式付费（cast.ModeIndex）。
            // 顺序（2026-10-16 恢复 2026-10-04 旧序——回调 10-11「补偿先付垫本卡」定案：
            // 代价补偿黑/白不预计算垫本卡，须本卡发动成功、代价强制执行完成后才入账）：
            // ① 代价目标存活校验——预选目标响应窗口内全灭/离场/不可指定 → 发动失败，
            //    卡回退声明来源区（手牌路径回手牌），不付费、不执行、走不到代价黑白元素生成；
            // ② 先扣卡费——现取现用（钱包优先消耗+系统自动横置地牌补足，2026-09-30 定案），
            //    将生成的代价黑白不提前计入（声明期 CanAffordCast 与此处实际支付同口径：
            //    新生成黑白不能垫本卡）；付不出 → 入墓不回卷（失败点在代价前——代价未执行、
            //    无补偿入账）；
            // ③ 代价强制执行+补偿入池（2026-09-14 撤销「代价可选」；快照在执行前取：
            //    Payload 执行会膨胀模板身价，09-13 护栏维持）——卡费已付清，生成的黑/白
            //    落临时钱包留待后续消费（本卡支付已闭合）。
            // 被「效果无效」只跳效果、已付代价与补偿不回卷；被「发动无效」则全免（上方 1 已拦）。
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

            // ③ 代价强制执行 + 补偿入临时钱包（PayWithCompensationAsync 内部：快照全价 → 执行 → 发放；
            //    AddMana 黑白单次钳：单次 ≤ 地牌上限）
            var costCtx = new CostContext
            {
                Payer = player,
                ZoneManager = core.ZoneManager,
                ElementPool = core.ElementPool,
                Source = player, // 来源=角色（来源归因定案 2026-09-09）
                PreselectedCostTargets = preselected, // 出牌两阶段（2026-10-04）：声明期预选代价目标沿用
            };
            if (!await CostCompensationService.PayWithCompensationAsync(specialCosts, costCtx))
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
                ResolvePermanentEntry(core, player, card);
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
            var defs = GetCardEffectDefinitions(card);
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
        private static void CastAbortToOrigin(GameCore core, Card card, Player player, Zone fromZone, string reason)
        {
            if (fromZone == Zone.None || fromZone == Zone.Activation) fromZone = Zone.Hand;
            core.ZoneManager.MoveCard(card, player, Zone.Activation, fromZone);
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
        /// </summary>
        private static void ResolvePermanentEntry(GameCore core, Player player, Card card)
        {
            // 发动通过 → 入场（声明期预检已过；满则入墓的兜底在 helper 内）
            core.ZoneManager.TryMoveToBattlefield(card, player, Zone.Activation);
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

                var cost = GetCardCost(castCard, obj.ModeIndex); // 抉择：按各自声明的模式计承诺
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
                if (p.GetPower() > 0)
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

            var defs = GetCardEffectDefinitions(card);
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
            var defs = GetCardEffectDefinitions(card);
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
        /// 出牌两阶段·第一阶段（2026-10-04 定案）：有代价的卡声明期先算代价——预选代价目标。
        /// 代价也是完整效果：按其自身域以**支付者视角**解析候选（与结算期 ExecutePayloadAsync 同口径），
        /// 候选存 <see cref="Card._pendingCostTargets"/> 供 cast 付费步沿用（响应窗口内不重选）。
        /// 代价全价过地牌门槛（2026-10-04 本轮定案）：全价 &gt; 地牌槽上限 → 整卡不可使用（走不到代价发动）；
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
        /// 代价门槛评估（2026-10-04 本轮定案，声明期与 AI 枚举同口；2026-10-16 组装效果代价逐原子化）：
        /// ① 全价门槛——代价全价（逐原子 PayloadUnitGrant 求和，与补偿同锚、不受减免）&gt; 地牌槽上限 → 整卡不可使用；
        /// ② 目标门槛——有域原子逐个以支付者视角解析候选（剔除本卡），任一无候选 → 发动无效。
        /// 全无域原子则两门槛中只过①（可发动，结算按无目标执行）。
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

            // ① 全价门槛：全价不并入卡费、装载不受减免——使用时单独过地牌门槛（例：上限3、全价4 → 整卡卡死）
            int fullPrice = 0;
            foreach (var atom in payloadCost.Payloads)
                if (atom != null) fullPrice += CostDerivationService.PayloadUnitGrant(atom);
            int landCap = core.ElementPool.GetLandCap(player);
            if (fullPrice > landCap)
                return $"代价全价 {fullPrice} 超过当前地牌槽上限 {landCap}——整卡无法使用（走不到代价发动）";

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

        /// <summary>
        /// 苏醒：在手牌中发动（2026-10-03 用户定案——手牌区动作，无先例）：
        /// 对带「自我沉睡登场效果」的卡预立约——此后施放费用中的**灰、绿份额全免**
        /// （转入场沉睡层数，1 元素=1 层，GetCardCost 豁免闸门读 AwakenCommitted；2026-10-22 扩灰+绿）。
        /// 未发动=照常全价。定长沉睡（Value 显式）无豁免语义，发动无效果（拒绝）。
        /// 门禁：自己回合主阶段 + 卡在自己手牌 + 含豁免时长模式自我沉睡 + 未发动过（幂等拒绝）。
        /// </summary>
        public static bool CommitAwaken(GameCore core, Player player, Card card)
            => CommitAwaken(core, player, card, out _);

        /// <summary>同 <see cref="CommitAwaken(GameCore,Player,Card)"/>，附带精确拒绝原因。</summary>
        public static bool CommitAwaken(GameCore core, Player player, Card card, out string rejectReason)
        {
            rejectReason = null;
            if (core == null || player == null || card == null) { rejectReason = "内部错误：空参数"; return false; }
            if (core.TurnEngine.TurnPlayer != player) { rejectReason = "现在不是你的回合"; return false; }
            if (core.TurnEngine.CurrentPhase?.Phase != PhaseType.Main)
            { rejectReason = $"苏醒只能在主阶段发动（当前：{core.TurnEngine.CurrentPhase?.Phase}）"; return false; }
            var hand = core.ZoneManager.GetCards(player, Zone.Hand);
            if (!hand.Contains(card)) { rejectReason = "卡不在你手上（快照过期？请重试）"; return false; }
            if (card.AwakenCommitted) { rejectReason = "已发动过苏醒（一卡一次）"; return false; }
            // 锁定指示物（2026-10-04 窥渊原子化）：无法使用=打出/响应出牌/苏醒立约同门
            if (card.GetCounterCount(Attribute.CounterRules.LockCounter) > 0)
            { rejectReason = "该牌被锁定（锁定指示物期间无法使用）"; return false; }

            var data = card is CardWrapper wrapper ? wrapper.GetData() : null;
            if (data == null || !HasSelfSleepEffect(data))
            { rejectReason = "该卡没有豁免时长模式的自我沉睡效果（定长沉睡无豁免语义）"; return false; }

            // 苏醒门限（2026-10-22 定案，同日扩灰+绿）：必须有可豁免的灰/绿费——两色皆 0 的卡
            // 发动苏醒不产生任何沉睡层数，纯空转；门限=灰+绿≥1（本套设计只给大生物，见绿·慢速设计稿）。
            var awakenCost = GetCardCost(card);
            if (awakenCost == null
                || awakenCost[ManaType.Gray] + awakenCost[ManaType.Green] <= 0f)
            { rejectReason = "该卡没有可豁免的灰/绿费（苏醒门限：灰+绿≥1）"; return false; }

            card.AwakenCommitted = true;
            core.PublishEvent(new KeywordAppliedEvent
            {
                Target = card,
                Keyword = "苏醒",
                Detail = "苏醒：在手牌中发动——施放费用的灰、绿份额全免，转为入场沉睡层数",
                Source = player,
            });
            return true;
        }

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
        /// </summary>
        public static ElementCost GetCardCost(Card card, int modeIndex = 0)
        {
            ElementCost cost;
            var data = card is CardCore.CardWrapper wrapper ? wrapper.GetData() : null;
            if (data != null)
                cost = CardCostService.GetRuntimeCost(data, modeIndex); // 恒推导（抉择=所选模式；副本供指示物层叠加）
            else
                cost = ElementCost.FromValue(ManaType.Gray, 1); // 防御默认：无卡数据按灰1（正常链路不达）

            // 法术锚价并入（改版定案：发动按效果费用支付，无额外费用——见 SpellCastAnchor）
            if (data != null && data.Supertype == Cardtype.Spell)
            {
                var anchor = SpellCastAnchor(card, modeIndex);
                foreach (var color in anchor.NonzeroColors())
                    cost[color] = cost[color] + anchor[color];
            }

            // 费用指示物层（P1 恒灰）：灰 += CostUp − CostDown，下限 0
            int costUp = card.GetCounterCount(Attribute.CounterRules.CostUpCounter);
            int costDown = card.GetCounterCount(Attribute.CounterRules.CostDownCounter);
            if (costUp != 0 || costDown != 0)
            {
                int gray = (int)cost[ManaType.Gray] + costUp - costDown;
                cost[ManaType.Gray] = Math.Max(0, gray);
            }

            // 自我沉睡的豁免费（2026-09-11 定案；2026-10-03 苏醒改造；2026-10-22 扩灰+绿）：
            // **须先在手牌中发动苏醒**（card.AwakenCommitted——预立约）才豁免——灰、绿份额转沉睡
            // 时长（暂存 card.PendingSleepLayers，入场 Sleep 原子消费为沉睡指示物数）。未发动=照常
            // 全价（修复旧「使用时生效」死循环：付不起→用不出→无减免——现在穷时先在手牌立约，
            // 再以减免价施放）。预检/付费/pending/UI 均经本口——全消费面同口径（恒推导定案）。
            if (data != null && card.AwakenCommitted && HasSelfSleepEffect(data))
            {
                int waived = (int)cost[ManaType.Gray] + (int)cost[ManaType.Green];
                if (waived > 0)
                {
                    cost[ManaType.Gray] = 0f;
                    cost[ManaType.Green] = 0f;
                    card._pendingSleepLayers = waived;
                }
            }
            return cost;
        }

        /// <summary>
        /// 法术施放锚价（2026-10-15 深夜三类卡改版：法术只挂 1~3 个主动效果，出牌费不含锚价——
        /// 施放时抉择其一，锚价并入 cast 账单现付，无额外费用）。modeIndex=效果序号（GetCardCost 单源消费）。
        /// 与 CardCostService.Derive 同口径：法术宿主次数分量坍缩 ×1。非法术/无效果返回空。
        /// </summary>
        public static ElementCost SpellCastAnchor(Card card, int effectIndex)
        {
            var data = card is CardWrapper wrapper ? wrapper.GetData() : null;
            if (data == null || data.Supertype != Cardtype.Spell) return new ElementCost();
            var defs = GetCardEffectDefinitions(card);
            if (defs == null || defs.Count == 0) return new ElementCost();
            var def = defs[Math.Clamp(effectIndex, 0, defs.Count - 1)];
            if (def == null) return new ElementCost();
            def.TriggerLimitPerTurn = 1; // 次数轴坍缩（法术=整卡一次施放）——同 Derive 主入口口径
            return CostDerivationService.DeriveElementCosts(def, 0) ?? new ElementCost();
        }

        /// <summary>
        /// 是否带「自我沉睡登场效果（豁免时长模式）」（豁免费判定，2026-09-11 定案；扩灰+绿 2026-10-22）：
        /// 任一非主动效果含 **Value 缺省** 的 Sleep 原子且组合域={Self}——
        /// Value&gt;0 的显式层数沉睡不走豁免（定长沉睡照常付费）。
        /// 结果缓存于 CardData（ResetCache 失效）。
        /// v3（2026-10-14）private→public：LegalActionEnumerator 的 CommitAwaken 资格预检
        /// 复用同一谓词（镜像复制会漂移）；零行为变化。
        /// </summary>
        public static bool HasSelfSleepEffect(CardData data)
        {
            if (data.SelfSleepEffectCache.HasValue) return data.SelfSleepEffectCache.Value;

            bool result = false;
            var defs = CardEffectConverter.ConvertAll(data.Effects, data.ID);
            foreach (var def in defs)
            {
                if (def == null || def.IsActivatedEffect) continue;
                if (def.TargetDomain == null || def.TargetDomain.Count != 1
                    || def.TargetDomain[0] != (int)TargetKind.Self) continue;
                bool hasSleep = def.Steps != null && def.Steps.Count > 0
                    ? ContainsWaivableSleepAtom(def.Steps.SelectMany(s => s != null && s.Atomic != null
                        ? new[] { s.Atomic } : new CardCore.AtomicEffectInstance[0]))
                    : (def.Effects != null && ContainsWaivableSleepAtom(def.Effects));
                if (hasSleep) { result = true; break; }
            }
            data.SelfSleepEffectCache = result;
            return result;
        }

        /// <summary>豁免时长模式的沉睡原子（Value≤0：层数=豁免费量灰+绿）。</summary>
        private static bool ContainsWaivableSleepAtom(IEnumerable<CardCore.AtomicEffectInstance> atoms)
        {
            foreach (var atom in atoms)
            {
                if (atom == null) continue;
                if (atom.Type == AtomicEffectType.Sleep && atom.Value <= 0) return true;
                if (atom.SubEffects != null && ContainsWaivableSleepAtom(atom.SubEffects)) return true;
            }
            return false;
        }

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
        /// 出一张卡的可付性：CanAfford（卡费+同玩家 pending 声明承诺统一混付规划）。
        /// 代价补偿黑/白**不预计算垫本卡**（2026-10-16 恢复 10-04 旧序——回调 10-11「补偿先付
        /// 垫本卡」定案）：补偿须本卡发动成功、代价强制执行完成后才入账，声明期不虚拟并入。
        /// 出牌声明预检（PlayCard/PlayCardInResponse）与 AI 枚举（LegalActionEnumerator）共用单源。
        /// </summary>
        public static bool CanAffordCast(GameCore core, Player player, Card card, int modeIndex)
        {
            if (core == null || player == null || card == null) return false;
            return CanAfford(core.ElementPool, GetCardCost(card, modeIndex), player,
                GetPendingCastCosts(core, player));
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
            int total = (int)Math.Ceiling(cost.Total);
            int cap = core.ElementPool.GetLandCap(player);
            if (total > cap)
                return $"费用 {CostText(cost)} 共 {total} 点，超过当前地牌槽上限 {cap}——地牌单货币贡献封顶上限，超出部分须临时元素垫付且当前不足（临时元素可超上限：攒钱包即可出）";

            var wallet = core.ElementPool.GetPool(player).TempElements;
            string bankText = string.Join("、", wallet.Where(kv => kv.Value > 0)
                .Select(kv => $"{ManaZh(kv.Key)}{kv.Value}"));
            var untapped = core.ElementPool.GetPool(player).PooledCards.Count(pc => !pc.IsTapped);
            return $"元素不足——需 {CostText(cost)}，临时元素：{(string.IsNullOrEmpty(bankText) ? "空" : bankText)}，"
                + $"未横置地牌 {untapped} 张（现取现用：付费时系统自动横置）"
                + "（代价补偿黑/白须发动成功后才到账，不预先计入）";
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
            ManaType.White => "白",
            ManaType.Black => "黑",
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
                // 法术 = 剩余免费点全额（槽位从速度扣：1槽=速2/2槽=速1/3槽=速0，动态不落字段）；
                // 结界/生物/地牌 = 0（结界禁主动后速度通道退役）。
                int kindSpeed = CardCompositionCost.KindSpeedBonusOf(cardData);
                if (kindSpeed != 0)
                    foreach (var def in defs)
                        if (def != null) def.BaseSpeed += kindSpeed;
                return defs;
            }
            return new List<EffectDefinition>();
        }
    }

    /// <summary>
    /// CardData 包装标记接口
    /// </summary>
    public interface CardDataWrapper { }
}
