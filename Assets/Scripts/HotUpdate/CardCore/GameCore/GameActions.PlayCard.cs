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
            => PlayCard(core, player, card, targets, fromZone, modeIndex, null, out _);

        /// <summary>同 <see cref="PlayCard(GameCore,Player,Card,List{Entity},Zone,int)"/>，附带精确拒绝原因（网络 Error 帧直读——2026-09-26 联机排查：通用「门禁」文案无法区分 费用/目标/时点）。</summary>
        public static bool PlayCard(GameCore core, Player player, Card card, List<Entity> targets, Zone fromZone, int modeIndex, out string rejectReason)
            => PlayCard(core, player, card, targets, fromZone, modeIndex, null, out rejectReason);

        /// <summary>步5（2026-11-08 目标选择重构 §3.3）：references=声明期 Reference spec 选位
        ///（网络意图/重放按 SpecKey 对齐显式携带）——随 cast 入栈（EffectInstance.Refs），执行器种进
        /// context.Refs；null=无参照（普通效果，行为与旧重载一致）。</summary>
        public static bool PlayCard(GameCore core, Player player, Card card, List<Entity> targets, Zone fromZone, int modeIndex, List<Entity> references, out string rejectReason)
            => PlayCard(core, player, card, targets, fromZone, modeIndex, references, null, out rejectReason);

        /// <summary>动态数量版（2026-11-14）：dynamicCounts=声明期收集的动态数量（下标=效果原子序，
        /// 0=非动态）——预检/扣费按 base×N，随 cast 透传到结算执行（ResolveCardCastAsync 同表扣费）。</summary>
        public static bool PlayCard(GameCore core, Player player, Card card, List<Entity> targets, Zone fromZone, int modeIndex, List<Entity> references, IReadOnlyList<int> dynamicCounts, out string rejectReason)
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
            var cost = GetCardCost(card, modeIndex, dynamicCounts);
            if (!CanAffordCast(core, player, card, modeIndex, dynamicCounts))
            { rejectReason = CostRejectText(core, player, cost); return false; }

            // ---- 声明（使用时点·S3b 2026-11-04 删发动区）：设控制者 → 重复施放闸 → cast 上栈 → 使用宣言 ----
            // 卡**留在来源区**（手牌/牌库顶）——「施放中」只由栈条目表示；不再 MoveCard 进 Activation、
            // 不再发 Enter 事件；PushCardCast 失败=零移动（卡没动过，无需回退）。
            card.SetController(player);
            card._pendingCastFromZone = fromZone; // 回退锚（2026-10-04 本轮：代价无有效目标时原路退回；S3b 后=Hand/Deck）

            // 重复施放闸（S3b）：卡已在该玩家的栈上施放中（旧防线=来源区 Contains——卡留手牌后天然恒真失效）
            if (core.StackEngine.GetPendingCastCards(player).Contains(card))
            { rejectReason = "该卡已在施放中（连锁上待结算——不可二次声明）"; return false; }

            if (!core.StackEngine.PushCardCast(card, player, targets, modeIndex, references, dynamicCounts))
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
            => PlayCardInResponse(core, player, card, targets, modeIndex, null, out rejectReason);

        /// <summary>步5（2026-11-08 目标选择重构 §3.3）：references=声明期 Reference spec 选位
        ///（网络意图/重放按 SpecKey 对齐显式携带）——随 cast 入栈（EffectInstance.Refs）。</summary>
        public static bool PlayCardInResponse(GameCore core, Player player, Card card, List<Entity> targets, int modeIndex, List<Entity> references, out string rejectReason)
            => PlayCardInResponse(core, player, card, targets, modeIndex, references, null, out rejectReason);

        /// <summary>动态数量版（2026-11-14）：同 PlayCard 计数透传（响应出牌同门）。</summary>
        public static bool PlayCardInResponse(GameCore core, Player player, Card card, List<Entity> targets, int modeIndex, List<Entity> references, IReadOnlyList<int> dynamicCounts, out string rejectReason)
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
            var cost = GetCardCost(card, modeIndex, dynamicCounts);
            if (!CanAffordCast(core, player, card, modeIndex, dynamicCounts))
            { rejectReason = CostRejectText(core, player, cost); return false; }

            card.SetController(player);
            card._pendingCastFromZone = Zone.Hand; // 回退锚（S3b 后卡留手牌——回退=零移动）

            // 重复施放闸（S3b）：卡已在栈上施放中（hand.Contains 恒真失效后的新防线）
            if (core.StackEngine.GetPendingCastCards(player).Contains(card))
            { rejectReason = "该卡已在施放中（连锁上待结算——不可二次声明）"; return false; }

            if (!core.StackEngine.PushCardCast(card, player, targets, modeIndex, references, dynamicCounts))
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
            var cost = GetCardCost(card, cast.ModeIndex, cast.DynamicCounts);
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
                await ResolveSpellEffectsAsync(core, player, card, cast.Targets, cast.ModeIndex, cast, cast.Refs,
                    cast.DynamicCounts);
            }
            else
            {
                ResolvePermanentEntry(core, player, card);
            }

            // 成功路径复位回退锚（S3b 收口 2026-11-05）：cast 结算完成（入墓/入场已发生），
            // _pendingCastFromZone 使命结束——残留会让下一轮（同卡实例复用/验证器夹具）读到过期来源。
            card._pendingCastFromZone = Zone.None;

            // 使用结算记账（2026-11-06 判据七·充盈改造；2026-11-08 阶段3）：充盈/团结均不在本口直调
            // ——成功结算先记账（NoteCastSettled），整条连锁结算完成的稳定点由 StackEngine.FinishResolution
            // 统一 flush（FlushCardCastSettled 补发 CardCastSettledEvent → 充盈双方评估·恰==x；
            // 团结随本事件经 OnRoutedEvent 按奖励效果时机评估——OnCardCastSettled 直调已删）。
            // 发动无效/效果无效/付费失败上方早退不达此；效果费支付不走此口。
            BranchEngines.NoteCastSettled(player, card, cast.ModeIndex);
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

                // 抉择按各自声明的模式计承诺；动态数量按各自声明期选定的 N 计（防两发动态法术
                // 同窗口声明时第二发按 base 少算承诺、账面超发）
                var cost = GetCardCost(castCard, obj.ModeIndex, obj.DynamicCounts);
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

        /// <summary>
        /// 声明期目标收集（2026-10-20 法术发动流程改案）：法术发动（PlayCard/PlayCardInResponse）
        /// **前**逐原子预选目标——调用方（BattleScreen 主路径 / 响应窗泵手牌卡分支）拿到结果再声明。
        /// 返回：null=玩家取消（终止发动——主路径撤销、响应路径记 declined）；空结果=无交互原子
        /// （直通声明，结算期照常自解析）；非空=预选目标+动态数量表（按原子序，结算期经预选队列有序消费）。
        /// 口径单源=EffectHandlerRegistry.CollectDeclarationTargetsAsync（与结算路径同谓词）；
        /// 教学闸/费用门不在此口——调用方先过、PlayCard 内终审（本口零引擎副作用）。
        /// caster 显式传参：响应路径施法者可能非回合玩家。
        /// </summary>
        public static async Cysharp.Threading.Tasks.UniTask<DeclarationTargets> CollectSpellDeclarationTargetsAsync(
            Card card, Player caster, int modeIndex)
        {
            var core = GameCore.Instance;
            if (card == null || caster == null || core?.ZoneManager == null) return DeclarationTargets.Empty(0);

            var defs = GetCastableEffectDefinitions(card); // P12：模式序号索引非充能列表
            if (defs == null || defs.Count == 0) return DeclarationTargets.Empty(0);
            var chosen = defs[Math.Clamp(modeIndex, 0, defs.Count - 1)];
            if (chosen == null) return DeclarationTargets.Empty(0);

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
        /// 主动效果/英雄技能发动前目标收集（2026-11-14 动态数量配套）：与法术声明收集同源
        /// （CollectDeclarationTargetsAsync 单链），供本地 UI/网络服务器在 CanActivate 预检**之前**
        /// 选定动态数量原子的目标数——扣费按 base×N、执行按 N 消费预选。0 费/无动态原子的效果
        /// 调用方可不走本口（行为不变）。
        /// </summary>
        public static async Cysharp.Threading.Tasks.UniTask<DeclarationTargets> CollectEffectDeclarationTargetsAsync(
            EffectDefinition effect, Card source, Player caster, int modeIndex = 0)
        {
            var core = GameCore.Instance;
            if (effect == null || source == null || caster == null || core?.ZoneManager == null)
                return DeclarationTargets.Empty(0);
            var ctx = new EffectExecutionContext
            {
                Source = source,
                Controller = caster,
                ZoneManager = core.ZoneManager,
                ElementPool = core.ElementPool,
                ModeIndex = modeIndex,
            };
            return await Attribute.EffectHandlerRegistry.CollectDeclarationTargetsAsync(effect, ctx);
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
            EffectInstance cast = null, List<Entity> references = null,
            IReadOnlyList<int> dynamicCounts = null)
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
                    Refs = references != null && references.Count > 0 ? new List<Entity>(references) : null,
                    DynamicCounts = dynamicCounts != null && dynamicCounts.Count > 0
                        ? new List<int>(dynamicCounts) : null, // 动态数量（2026-11-14）：执行价/执行数同表
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

        //（CollectCardPayloadCostRows 已随 2026-11-08 代价效果级化整删——卡级代价行不存在；
        //  施放期代价单源=CollectCardSpecialCosts 内非主动效果行 def.Costs，
        //  重放轮 PayReplayRoundCostsAsync 与 cast 付费步同经该口。）

        /// <summary>
        /// 弃牌代价「另有一张」闸（2026-11-03 用户定案）：施放/发动本卡时**本卡自身不算可弃手牌**——
        /// 弃牌类代价要求「手牌数（剔除本卡）≥ 代价张数」，不够 → 整卡不可用（声明期闸，不是结算期失败）。
        /// 旧口径 CostPayability 只数手牌总数（本卡仍在手），会出现「手里只剩本卡 → 代价空转白嫖」。
        /// 覆盖面：非主动效果行（CollectCardSpecialCosts）+ **法术所选模式（主动）效果行**
        /// （specialCosts 不收主动行，但法术施放就是使用该主动效果——其代价照付）。
        /// 返回 null=通过；非 null=拒绝原因。
        /// </summary>
        private static string CheckDiscardCostSelfExclusion(GameCore core, Player player, Card card, int modeIndex)
        {
            if (core == null || player == null || card == null) return null;
            int need = 0;
            foreach (var cost in CollectCardSpecialCosts(card))
                need += DiscardAtomsOf(cost);
            // 法术所选模式（主动）效果行——specialCosts 只收非主动效果；
            // 非主动模式行已在 specialCosts 内，不另计（防同桶双计——2026-11-08 效果级化配套）
            var data = card is CardWrapper cw ? cw.GetData() : null;
            if (data != null && data.Supertype == Cardtype.Spell)
            {
                var defs = GetCastableEffectDefinitions(card);
                var modeDef = defs != null && defs.Count > 0
                    ? defs[Math.Clamp(modeIndex, 0, defs.Count - 1)] : null;
                if (modeDef != null && modeDef.IsActivatedEffect && modeDef.Costs != null)
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
        /// 收集一张卡的**施放期**特殊代价（2026-11-08 代价效果级化——卡级行整删）：
        /// = **非主动**效果的效果级代价行（自动效果行——随卡施放在 cast 付费步统一执行）。
        /// 主动效果行不在此列：发动时经 EffectActivationGate 现付（净费+执行）；法术施放=使用
        /// 所选主动效果——其代价行在 GetCardCost 锚价净费落账、执行器结算时执行。
        /// </summary>
        private static List<CostInstance> CollectCardSpecialCosts(Card card)
        {
            // 效果级行：非主动（自动）效果——cast 付费步执行；主动跳过（发动时现付）
            var result = new List<CostInstance>();
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

        /// <summary>法术所选模式（主动效果行）的效果级 Payload 代价（2026-11-09 补闸）：
        /// 法术施放=使用该主动效果——其代价行不进 <see cref="CollectCardSpecialCosts"/>（非主动行专收），
        /// 结算期由执行器现付（EffectExecutionEngine「主动效果的效果级代价唯一真执行点」）。
        /// 声明期闸（EvaluatePayloadGate ①可支付/③地牌、CanAffordCast.AllPayable）在此并入
        /// ——此前零闸：0基价+代价净费归零（GetCardCost NetBill）的法术无同色地也照常入栈。
        /// ElementConsume 行不收（与 GetCardCost 净费/执行器 specialCosts 同剔除口径）。
        /// 非法术/无代价返回 null。</summary>
        private static List<CostInstance> CollectSpellModeCosts(Card card, int modeIndex)
        {
            var data = card is CardWrapper cw ? cw.GetData() : null;
            if (data == null || data.Supertype != Cardtype.Spell) return null;
            var defs = GetCastableEffectDefinitions(card);
            var modeDef = defs != null && defs.Count > 0
                ? defs[Math.Clamp(modeIndex, 0, defs.Count - 1)] : null;
            if (modeDef == null || !modeDef.IsActivatedEffect || modeDef.Costs == null) return null;
            var rows = new List<CostInstance>();
            foreach (var c in modeDef.Costs)
                if (c != null && c.Type == CostType.Payload && c.Payloads != null && c.Payloads.Count > 0)
                    rows.Add(c);
            return rows.Count > 0 ? rows : null;
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
        /// 2026-11-09 补闸：法术所选模式（主动行）的 Payload 代价并入①③——此前 payloadCosts 只收
        /// 非主动行，「仅主动行带代价」的法术在无代价早退处整段跳闸（③法术模式分支不可达）：
        /// 0基价+代价净费归零的法术无同色地也照常声明入栈弹连锁窗（如 代价2蓝 需 2 蓝地）。
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
            // 法术所选模式（主动行）代价：不进 payloadCosts（②预选/结算期 cast 付费步均为非主动行口径
            // ——主动行代价由执行器结算段现付），只并入①可支付/③地牌两道声明期闸
            var spellModeCosts = CollectSpellModeCosts(card, modeIndex);
            // 2026-11-03 用户定案：弃牌代价「另有一张」闸——本卡自身不算可弃手牌（声明期闸，整卡不可用）。
            // 必须早于下方「无代价」早退：法术的代价行挂在**主动效果**上（不 Collected 进 specialCosts）。
            var discardReject = CheckDiscardCostSelfExclusion(core, player, card, modeIndex);
            if (discardReject != null) { payloadCosts = null; candidates = null; return discardReject; }

            bool hasModeCosts = spellModeCosts != null && spellModeCosts.Count > 0;
            if (payloadCosts.Count == 0 && !hasModeCosts)
            { payloadCosts = null; return null; } // 无代价：两阶段退化为一阶段（费用门禁）

            // ① 可支付门槛（声明期闸——与 CanAffordCast 同口径）：不可支付返回拒绝原因（UI/AI 直读）
            var costCtx = new CostContext
            {
                Payer = player,
                ZoneManager = core.ZoneManager,
                ElementPool = core.ElementPool,
                Source = player,
            };
            List<CostInstance> gateCosts = specialCosts;
            if (hasModeCosts) // 法术模式行代价一并验付（与 CanAffordCast.AllPayable 镜像同口径）
            {
                gateCosts = new List<CostInstance>(specialCosts);
                gateCosts.AddRange(spellModeCosts);
            }
            var payReject = CostPayability.RejectReason(gateCosts, costCtx);
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
    }
}
