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
        /// 不限张数/次数，张数由地牌槽上限约束（min(全局回合数, 9)）；
        /// 本回合即可手动横置产出。
        /// </summary>
        public static bool AddToElementPool(GameCore core, Player player, Card card)
        {
            if (core == null || player == null || card == null) return false;
            if (core.TurnEngine.TurnPlayer != player) return false;
            if (core.TurnEngine.CurrentPhase?.Phase != PhaseType.Main) return false;

            // 检查卡牌在手中
            var hand = core.ZoneManager.GetCards(player, Zone.Hand);
            if (!hand.Contains(card)) return false;

            // 放入元素池（张数 ≤ 地牌槽上限）
            var elementPool = core.ElementPool;
            if (!elementPool.AddCardToPool(card, player))
                return false;

            // 从手牌移到元素池区域
            core.ZoneManager.MoveCard(card, player, Zone.Hand, Zone.ElementPool);

            return true;
        }

        /// <summary>
        /// 主阶段：横置一张己方未横置地牌，自选颜色产 1 个元素入 bank。
        /// 自选颜色范围 = 该地牌剩余指示物的颜色集合（卡本身费用构成决定）。
        /// </summary>
        public static bool GainElementFromToken(GameCore core, Player player, PooledCard land, ManaType type)
        {
            if (core == null || player == null || land == null) return false;
            if (core.TurnEngine.TurnPlayer != player) return false;
            if (core.TurnEngine.CurrentPhase?.Phase != PhaseType.Main) return false;

            var elementPool = core.ElementPool;
            if (!elementPool.GainElementFromToken(land, type, player))
                return false;

            // 产出导致耗尽的地牌移入墓地
            elementPool.CheckDepletedCards(player, core.ZoneManager);

            return true;
        }

        /// <summary>
        /// 准备阶段：结束准备阶段进入主阶段
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
        /// （扣费在响应窗口之后；软打断=没付、硬反制=付了但被否定、费用不退、不回卷）。
        /// targets：可选的预选目标（如指向性法术）；为空时由各原子效果按配置自动解析。
        /// fromZone：出牌来源区（默认手牌；Graveyard = 归土仪典「墓地视手牌中使用」路径）。
        /// </summary>
        public static bool PlayCard(GameCore core, Player player, Card card, List<Entity> targets = null, Zone fromZone = Zone.Hand, int modeIndex = 0)
        {
            if (core == null || player == null || card == null) return false;
            if (core.TurnEngine.TurnPlayer != player) return false;
            if (core.TurnEngine.CurrentPhase?.Phase != PhaseType.Main) return false;

            // 检查卡牌在来源区
            var sourceZone = core.ZoneManager.GetCards(player, fromZone);
            if (!sourceZone.Contains(card)) return false;

            // 规则扩展点（OCP）：出牌限制（如信息轴锁定）经注册表询问
            if (!RuleHooks.CanPlay(core, player, card, fromZone)) return false;

            // 目标域预检（2026-09-10 定案：运行时无候选不可发动）
            if (!TargetDomainService.HasPlayableTargets(core, player, card, modeIndex)) return false;

            // 规则扩展点（OCP）：非手牌来源（如墓地视手牌使用）经注册表取得并占用配额
            if (fromZone != Zone.Hand)
            {
                var playSource = RuleHooks.GetPlaySource(fromZone);
                if (playSource == null || !playSource.TryBeginUse(player)) return false;
            }

            bool isSpell = card.IsSpellCard();

            // 永久物：战场容量预检（满场不允许发动，SimpleAI 依赖 false 跳过；
            // 「结算时入场满则失败入墓」由 TryMoveToBattlefield 承担）
            if (!isSpell && !core.ZoneManager.HasBattlefieldSpace(player))
                return false;

            // 费用预检（不支付——扣费在响应窗口之后的 cast 结算；抉择卡按所选模式取费——先选择再定费用）；
            // 代价可选（2026-09-11）：带可付代价的卡按「付+减费」封顶减免额预检
            //（选择窗口在付费步——声明期按最大可能减免放行，付不出时结算入墓不回卷）；
            // 同玩家已声明的施放费用一并计入，防同笔 bank 超发——结算付不出只入墓、不回卷）
            var cost = GetCardCost(card, modeIndex);
            if (!CanAfford(core.ElementPool, PrecheckBill(core, player, card, cost), player, GetPendingCastCosts(core, player)))
                return false;

            // ---- 声明（使用时点）：设控制者 → 进发动区 → cast 上栈 → 使用宣言（对手获得响应窗口） ----
            card.SetController(player);

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

            return true;
        }

        /// <summary>
        /// 响应时点出牌（自由时点）：非回合玩家在优先权响应窗口内打出一张牌
        /// （打落/发动无效 等反制——目标指向发动区中被使用的卡）。
        /// 与 PlayCard 同构（声明 → 发动区 → cast 上栈 → 付费延迟到 cast 结算），
        /// 门禁差异：不要求回合玩家/主阶段，改为 持有优先权 + 栈上有待响应对象；来源限手牌。
        /// </summary>
        public static bool PlayCardInResponse(GameCore core, Player player, Card card, List<Entity> targets = null, int modeIndex = 0)
        {
            if (core == null || player == null || card == null) return false;
            if (core.StackEngine.IsEmpty || core.StackEngine.IsResolving) return false; // 有可响应对象且非结算中
            if (core.StackEngine.CurrentPriorityHolder != player) return false;         // 优先权在手

            var hand = core.ZoneManager.GetCards(player, Zone.Hand);
            if (!hand.Contains(card)) return false;

            // 规则扩展点（OCP）：出牌限制经注册表询问（响应出牌同样受限，如信息轴锁定）
            if (!RuleHooks.CanPlay(core, player, card, Zone.Hand)) return false;

            bool isSpell = card.IsSpellCard();
            if (!isSpell && !core.ZoneManager.HasBattlefieldSpace(player))
                return false;

            // 费用预检（含本玩家已声明的施放承诺；不支付——cast 结算时才扣；抉择按所选模式；
            // 代价可选：带可付代价按封顶减费额预检，同 PlayCard 口径）
            var cost = GetCardCost(card, modeIndex);
            if (!CanAfford(core.ElementPool, PrecheckBill(core, player, card, cost), player, GetPendingCastCosts(core, player)))
                return false;

            card.SetController(player);

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
        /// 主阶段：从墓地使用一张牌，视为手牌中使用（归土仪典奖励）。
        /// 每回合主要阶段一次（RitualEffects 配额）；法术结算后照常入墓、永久物入场。
        /// </summary>
        public static bool PlayCardFromGraveyard(GameCore core, Player player, Card card, List<Entity> targets = null, int modeIndex = 0)
            => PlayCard(core, player, card, targets, Zone.Graveyard, modeIndex);

        // ======================================== 整卡施放结算（使用时点消费点） ========================================

        /// <summary>
        /// 整卡施放的栈结算——「此卡被使用」的消费点（Option Y 定案）：
        /// 1. 卡已不在发动区（被「打落」送墓等）→ 中止：不付费、不结算（软打断达成）；
        /// 2. 付费（扣费在响应窗口之后，从 bank 扣）；
        /// 3. 卡 _isNegated（被「发动无效」置位）→ 移入墓地、跳过效果、费用不退（硬反制达成）；
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

            // 1. 打落中止：卡已不在发动区 → 不付费、不结算
            if (!core.ZoneManager.IsCardInZone(card, player, Zone.Activation))
                return;

            // 2. 付费（响应窗口之后）——抉择卡按声明期选定的模式付费（cast.ModeIndex）。
            // 代价可选（2026-09-11 定案）：付费步的选择窗口（抉择窗口之外多一个）——
            // 不付（原价）/ 付+减费 / 付+得黑白（两通道共用单次上限=地牌上限，防重代价换超大生物）。
            // 被「发动无效」只跳效果、已付代价与补偿不回卷；被打落则全免（上方 1 已拦）。
            var specialCosts = CollectCardSpecialCosts(card);
            var cost = GetCardCost(card, cast.ModeIndex);
            var costCtx = new CostContext
            {
                Payer = player,
                ZoneManager = core.ZoneManager,
                ElementPool = core.ElementPool,
                Source = player // 来源=角色（来源归因定案 2026-09-09）
            };
            if (!await CostCompensationService.PayOptionalCardCostsAsync(specialCosts, costCtx, cost))
            {
                CastAbortToGraveyard(core, card, player, "代价流程异常（付费步失败，不回卷）");
                return;
            }
            if (!core.ElementPool.PayCost(cost, player))
            {
                CastAbortToGraveyard(core, card, player, "费用不足（响应窗口后支付失败，不回卷）");
                return;
            }

            // 3. 发动无效：付了但被否定 → 入墓、跳过效果、费用不退
            if (card._isNegated)
            {
                card._isNegated = false; // 消费即复位（同一否定标记只拦一次）
                CastAbortToGraveyard(core, card, player, "发动无效");
                return;
            }

            // 4. 结算：法术 → 效果全结算后离区入墓；永久物 → 登记触发式 + 入场
            if (card.IsSpellCard())
            {
                await ResolveSpellEffectsAsync(core, player, card, cast.Targets, cast.ModeIndex);
            }
            else
            {
                ResolvePermanentEntry(core, player, card);
            }
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
        /// 永久物（生物等）cast 结算：经发动区入场。
        /// （触发式注册已收口到 TryMoveToBattlefield/TryAddToBattlefield 统一出口——
        /// 时点接线定案：任何来源进场的卡都注册自身触发式，入场事件发布前完成，
        /// 保证入场卡自己的 OnPlay/OnSummon 能吃到自己的入场事件；仪式入场激活经 CardPutToBattlefieldEvent 事件驱动。）
        /// </summary>
        private static void ResolvePermanentEntry(GameCore core, Player player, Card card)
        {
            // 发动通过 → 入场（声明期预检已过；满则入墓的兜底在 helper 内）
            if (core.ZoneManager.TryMoveToBattlefield(card, player, Zone.Activation))
            {
                card.WasFormallySummoned = true; // 普通召唤正式入场
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
        public static Dictionary<int, float> GetPendingCastCosts(GameCore core, Player player)
        {
            var sum = new Dictionary<int, float>();
            if (core?.StackEngine == null || player == null) return sum;

            foreach (var obj in core.StackEngine.GetStackContents())
            {
                if (obj == null || !obj.IsCardCast || obj.Controller != player) continue;
                if (!(obj.Source is Card castCard)) continue;

                var cost = GetCardCost(castCard, obj.ModeIndex); // 抉择：按各自声明的模式计承诺
                foreach (var kv in cost)
                    sum[kv.Key] = sum.TryGetValue(kv.Key, out var v) ? v + kv.Value : kv.Value;
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

        /// <summary>
        /// 结算法术的施放效果（经本核心的 EffectExecutor），完成后离开发动区入墓。
        /// 法术一次性结算：OnPlay 等触发时点在此即是「施放即生效」，直接执行；
        /// 仅手动激活式能力（Activate_*）不随施放自动结算。
        /// 通过 EffectInstance 走与栈结算一致的执行路径，保证目标解析/事件一致。
        /// 元素费已在 cast 结算（ResolveCardCastAsync）按所选模式支付（skipElementCost 防双计）；特殊代价仍由执行器结算。
        /// modeIndex：抉择模式（声明期选定），执行引擎按此分派 Choices。
        /// </summary>
        private static async Cysharp.Threading.Tasks.UniTask ResolveSpellEffectsAsync(
            GameCore core, Player player, Card card, List<Entity> targets, int modeIndex = 0)
        {
            var defs = GetCardEffectDefinitions(card);
            if (defs != null)
            {
                var executor = core.StackEngine.GetExecutor();

                foreach (var def in defs)
                {
                    if (def.IsActivatedEffect)
                        continue;

                    await executor.ExecuteAsync(new EffectInstance
                    {
                        Definition = def,
                        // 来源归因定案（2026-09-09 规则②）：所有魔法卡的效果来源=角色（Player）——
                        // 伤害/死亡/指示物/三轨判轨的归因都指向施法者；cast 对象的 Source 仍是卡
                        // （发动区付费/无效裁决/离区依赖它，见 ResolveCardCastAsync）。
                        Source = player,
                        Controller = player,
                        Targets = targets != null ? new List<Entity>(targets) : new List<Entity>(),
                        ModeIndex = modeIndex,
                    }, skipElementCost: true);
                }
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
        /// 声明期费用预检口径（2026-09-11 代价可选）：卡带可付代价时按「付+减费」的封顶减免额预检
        ///（选择窗口在付费步；此处只放行最大可能减免——玩家实际可能选不付，付费步付不出按入墓不回卷）。
        /// </summary>
        private static Dictionary<int, float> PrecheckBill(GameCore core, Player player, Card card,
            Dictionary<int, float> cost)
        {
            var specials = CollectCardSpecialCosts(card);
            if (specials.Count == 0) return cost;

            var ctx = new CostContext
            {
                Payer = player,
                ZoneManager = core.ZoneManager,
                ElementPool = core.ElementPool,
                Source = player,
            };
            foreach (var c in specials)
            {
                if (c == null || c.Type == CostType.Payload) continue; // Payload 恒可付（强制效果）
                if (!CostHandlerRegistry.CanPay(c, ctx)) return cost;  // 代价付不起 → 无减免，按原价预检
            }

            var discounted = new Dictionary<int, float>(cost);
            CostCompensationService.ApplyDiscount(discounted,
                CostCompensationService.CappedTotalEquivalent(specials, ctx));
            return discounted;
        }

        /// <summary>
        /// 收集一张卡的卡级特殊代价（2026-09-11：代价栏单卡单条定案——整卡效果声明的
        /// 非元素代价在 cast 付费步统一执行并补偿；启动式能力的代价不在此列，发动时现付）。
        /// </summary>
        private static List<CostInstance> CollectCardSpecialCosts(Card card)
        {
            var result = new List<CostInstance>();
            var defs = GetCardEffectDefinitions(card);
            if (defs == null) return result;
            foreach (var def in defs)
            {
                if (def == null || def.IsActivatedEffect) continue; // 启动式：发动时现付（执行器路径）
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
        /// 主阶段：发起攻击（炉石式，随时可攻击）
        /// </summary>
        public static bool DeclareAttack(GameCore core, Player player, Entity attacker, Entity target)
        {
            if (core == null || player == null) return false;
            if (!core.TurnEngine.CanCombatAction()) return false;
            if (core.TurnEngine.TurnPlayer != player) return false;

            var combat = core.CombatSystem;

            // 战斗按需开启（主阶段随时可攻击，无独立战斗阶段）：
            // 若尚未处于本玩家的战斗会话，则进入攻击者选择状态，否则 CanDeclareAttack 因
            // _attackingPlayer 为空/不符而恒为 false。
            if (!combat.InCombat || combat.AttackingPlayer != player)
                combat.StartCombat(player, player.Opponent);

            if (!combat.CanDeclareAttack(attacker, player))
                return false;

            // 宣言可能被时点效果取消（重检失败：代价支付不出/目标丢失）——透传结果供上层重选目标
            return combat.DeclareAttack(attacker, target);
        }

        // ======================================== 回合控制 ========================================

        /// <summary>
        /// 结束回合
        /// </summary>
        public static bool EndTurn(GameCore core, Player player)
        {
            if (core == null || player == null) return false;
            if (core.TurnEngine.TurnPlayer != player) return false;

            core.TurnEngine.EndTurn();
            return true;
        }

        // ======================================== 栈操作 ========================================

        /// <summary>
        /// 速度发动：玩家主动发动一个效果。
        /// 启动式能力的发动代价（2026-09-08 定案）：只有启动式（Activate_*）= 横置源卡 + 现付元素锚价 +
        /// 选定目标（元素费经 ElementCostPrepaid=false 由执行器结算路径扣除；CanActivate 预检可付性）；
        /// 只能在自己的主要阶段以速度1使用（记速器须低于1，即栈空）。触发式效果不走本入口的横置与付费。
        /// 警戒自动抵扣一次横置（一回合一次，经 KeywordRules.ShouldTap）；预检在 EffectExecutionEngine.CanActivate。
        /// </summary>
        public static bool ActivateEffect(GameCore core, Player player, EffectDefinition effect, Card source, int paidBoost = 0)
        {
            if (core == null || player == null || effect == null) return false;
            if (core.TurnEngine.TurnPlayer != player) return false;
            if (core.TurnEngine.CurrentPhase?.Phase != PhaseType.Main) return false;

            // 横置代价权威校验（仅启动式）：战场上的源卡已横置 → 不可发动（手牌/墓地施放不适用）
            if (effect.IsActivatedEffect && source != null && source.IsTapped()
                && core.ZoneManager.IsCardInZone(source, source.GetController(), Zone.Battlefield))
                return false;

            var pending = PendingEffect.Create(
                effect,
                source,
                player,
                core.TurnEngine.TurnPlayer,
                core.TurnEngine.CurrentPhase?.Phase ?? PhaseType.Standby,
                paidBoost: paidBoost);

            var activated = core.StackEngine.PlayerActivateVoluntary(pending);

            // 发动成功 → 消耗横置（仅启动式；警戒：一回合一次自动抵扣，不发不扣）
            if (activated && effect.IsActivatedEffect && source != null && Attribute.KeywordRules.ShouldTap(source))
                source.Tap();

            return activated;
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

        // ======================================== 内部方法 ========================================

        /// <summary>
        /// 从卡牌读取费用——出牌预检 / cast 付费 / pending 合计的唯一口径（public：UI/AI 声明期展示与预检同口径）。
        /// 抉择卡（HasChoiceEffect）走 per-mode 推导缓存（CardCostService.GetModeCost——构筑期推导存储，
        /// 发动时只读不重推导）；推导为空=该模式免费（不落 {Gray:1} 默认——那是「无费用数据」的兜底）。
        /// 声明 costList 在装载期写为最大模式费，仅供地牌产元素/召唤素材/UI 消费，不用于支付。
        /// 费用指示物层在此接入（定案：层带颜色，P1 恒灰）：灰色分量 += 费用增加层 − 费用减少层（下限 0），
        /// 逐模式独立套用。层在进入发动区时不清（ZoneContainer.OnCardMoved 发动区豁免——付费发生在发动区内），
        /// 结算离开发动区（入墓/入场）与离手时按真实移动清除。
        /// </summary>
        public static Dictionary<int, float> GetCardCost(Card card, int modeIndex = 0)
        {
            Dictionary<int, float> cost;
            var data = card is CardCore.CardWrapper wrapper ? wrapper.GetData() : null;
            if (data != null && CostDerivationService.HasChoiceEffect(data))
                cost = CardCostService.GetModeCost(data, modeIndex); // 抉择：按所选模式（副本，指示物层叠加不污染缓存）
            else if (card is IHasCost hasCost && hasCost.Cost != null)
                cost = new Dictionary<int, float>(hasCost.Cost);
            else
                cost = new Dictionary<int, float> { { (int)ManaType.Gray, 1 } }; // 默认费用：灰色1点

            // 费用指示物层（P1 恒灰）：灰 += CostUp − CostDown，下限 0
            int costUp = card.GetCounterCount(Attribute.CounterRules.CostUpCounter);
            int costDown = card.GetCounterCount(Attribute.CounterRules.CostDownCounter);
            if (costUp != 0 || costDown != 0)
            {
                int gray = (cost.TryGetValue((int)ManaType.Gray, out var g) ? (int)g : 0) + costUp - costDown;
                cost[(int)ManaType.Gray] = Math.Max(0, gray);
            }

            // 自我沉睡的灰费豁免（2026-09-11 定案）：带「自我沉睡登场效果」的卡**使用时不扣灰色费用**，
            // 灰份额转沉睡时长（暂存 card.PendingSleepGray，入场 Sleep 原子消费为沉睡指示物数）。
            // 预检/付费/pending/UI 均经本口——全消费面同口径；卡面声明 Cost 不动（地牌/素材口径照旧）。
            if (data != null && HasSelfSleepEffect(data))
            {
                int waived = cost.TryGetValue((int)ManaType.Gray, out var wg) ? (int)wg : 0;
                if (waived > 0)
                {
                    cost.Remove((int)ManaType.Gray);
                    card._pendingSleepGray = waived;
                }
            }
            return cost;
        }

        /// <summary>
        /// 是否带「自我沉睡登场效果（灰时长模式）」（灰费豁免判定，2026-09-11 定案）：
        /// 任一非启动式效果含 **Value 缺省** 的 Sleep 原子且组合域={Self}——
        /// Value&gt;0 的显式层数沉睡不走灰费豁免（定长沉睡照常付费）。
        /// 结果缓存于 CardData（ResetCache 失效）。
        /// </summary>
        private static bool HasSelfSleepEffect(CardData data)
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
                    ? ContainsGraySleepAtom(def.Steps.SelectMany(s => s != null && s.Atomic != null
                        ? new[] { s.Atomic } : new CardCore.AtomicEffectInstance[0]))
                    : (def.Effects != null && ContainsGraySleepAtom(def.Effects));
                if (hasSleep) { result = true; break; }
            }
            data.SelfSleepEffectCache = result;
            return result;
        }

        /// <summary>灰时长模式的沉睡原子（Value≤0：层数=灰费豁免量）。</summary>
        private static bool ContainsGraySleepAtom(IEnumerable<CardCore.AtomicEffectInstance> atoms)
        {
            foreach (var atom in atoms)
            {
                if (atom == null) continue;
                if (atom.Type == AtomicEffectType.Sleep && atom.Value <= 0) return true;
                if (atom.SubEffects != null && ContainsGraySleepAtom(atom.SubEffects)) return true;
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
        /// 检查是否能支付费用：
        /// 1. 门槛：卡总费用不得超过当前地牌槽上限（费用上限 9 由此隐含——上限最大 9；按卡判定不累计）
        /// 2. 余量：bank 各颜色元素充足（bank 无上限，跨回合保留）；
        ///    pending = 同玩家已声明未结算的整卡施放费用（响应窗口内的支付承诺，一并占用）
        /// </summary>
        private static bool CanAfford(ElementPoolSystem elementPool, Dictionary<int, float> cost, Player player,
            Dictionary<int, float> pending = null)
        {
            if (cost.Values.Sum() > elementPool.GetLandCap(player))
                return false;

            foreach (var kvp in cost)
            {
                ManaType type = (ManaType)kvp.Key;
                int amount = (int)kvp.Value;
                if (pending != null && pending.TryGetValue(kvp.Key, out var committed))
                    amount += (int)committed;
                if (elementPool.GetAvailableManaCount(type, player) < amount)
                    return false;
            }
            return true;
        }

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
                // 瞬间富余转速度（2026-09-10 攻/守效果化）：无攻守法术的底盘盈余 2 灰构筑时
                // 自由分配——SurplusToSpeed=true → 全部效果 BaseSpeed+1（计价侧不退费，见 ChassisAdjust）
                if (cardData.SurplusToSpeed && cardData.Supertype == Cardtype.Spell)
                    foreach (var def in defs)
                        if (def != null) def.BaseSpeed += 1;
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
