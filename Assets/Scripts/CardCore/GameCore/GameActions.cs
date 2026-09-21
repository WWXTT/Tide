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
        /// 抉择卡（2026-09-21 定案）：modeIndex 玩家自选（与出牌同口径），指示物按所选模式生成。
        /// </summary>
        public static bool AddToElementPool(GameCore core, Player player, Card card, int modeIndex = 0)
        {
            if (core == null || player == null || card == null) return false;
            if (core.TurnEngine.TurnPlayer != player) return false;
            if (core.TurnEngine.CurrentPhase?.Phase != PhaseType.Main) return false;

            // 检查卡牌在手中
            var hand = core.ZoneManager.GetCards(player, Zone.Hand);
            if (!hand.Contains(card)) return false;

            // 放入元素池（张数 ≤ 地牌槽上限；抉择卡按所选模式产指示物）
            var elementPool = core.ElementPool;
            if (!elementPool.AddCardToPool(card, player, modeIndex))
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
        /// 主阶段：发动英雄技能（2026-09-13 第二十批——一回合一次、付色费、7 次升级）。
        /// 逻辑全在 HeroSkillSystem.ActivateAsync（守卫+计费+效果）。
        /// </summary>
        public static async Cysharp.Threading.Tasks.UniTask<bool> ActivateHeroSkill(GameCore core, Player player)
            => await HeroSkillSystem.ActivateAsync(core, player);

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

            var data = (creature as CardWrapper)?.GetData();
            var cost = data?.Cost;
            ManaType type = ManaType.Gray;
            if (cost != null && cost.Count > 0)
            {
                foreach (var kv in cost)
                    if (kv.Value > 0) { type = (ManaType)kv.Key; break; }
            }

            creature.Tap();
            core.ElementPool.GetPool(player).AvailableMana[type]++;
            core.PublishEvent(new ElementPoolGainEvent
            {
                Player = player,
                GainedType = type,
                FromCard = creature,
            });
            return true;
        }

        /// <summary>
        /// 主阶段：武器主动攻击（2026-09-13 装备系统定案——一回合一次、须已驱动、耐久-1）。
        /// 以武器 Power 对目标造成**战斗伤害**（走 ApplyDamage 战斗管线：易损/圣盾/护甲/坚韧/改写族全适用）。
        /// 简化版：一次直接结算不上栈（与 TapCreatureForElement 同口径的主阶段动作）。
        /// </summary>
        public static bool AttackWithWeapon(GameCore core, Player player, Card weapon, Entity target)
        {
            if (core == null || player == null || weapon == null || target == null) return false;
            if (core.TurnEngine.TurnPlayer != player) return false;
            if (core.TurnEngine.CurrentPhase?.Phase != PhaseType.Main) return false;
            if (!core.ZoneManager.IsCardInZone(weapon, player, Zone.Battlefield)) return false;
            if (!(weapon is CardWrapper w) || w.GetData()?.IsWeapon != true) return false;
            if (!EquipRules.IsDriven(core, weapon)) return false; // 须驱动完成
            if (!target.IsAlive) return false;

            // 一回合一次闸门（本回合使用追踪）
            if (weapon.GetCounterCount("__WeaponUsedThisTurn") > 0) return false;
            weapon.AddCounters("__WeaponUsedThisTurn", 1);
            // 回合开始清（订阅一次性挂——简单做法：临时计数，回合事件在验证器直接驱动）

            int power = Math.Max(0, weapon.GetPower());
            if (power <= 0) return false;

            // 目标合法性：走战斗 CanAttackTarget（帷幕/守卫拦截照常——从"我"视角）
            var combat = core.CombatSystem;
            if (combat != null && !combat.CanAttackTarget(weapon, target, player)) return false;

            Attribute.KeywordRules.ApplyDamage(player, target, power, true);
            core.PublishEvent(new KeywordAppliedEvent
            {
                Target = target,
                Keyword = "武器攻击",
                Detail = $"{player.Name} 以 {weapon} 主动攻击：{power} 点战斗伤害",
                Source = player,
            });

            EquipRules.LoseDurability(core, weapon, 1, "主动攻击");
            return true;
        }

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

            // 费用预检（不支付——扣费在响应窗口之后的 cast 结算；抉择卡按所选模式取费——先选择再定费用）。
            // 2026-09-14 代价强制：撤销「按最大减免放行」——声明期即按**原价账单**预检，
            // 混付口径与结算一致（同色→灰→黑白，见 ElementPool.CanPayCost）；
            // 同玩家已声明的施放费用一并计入，防同笔 bank 超发——结算付不出只入墓、不回卷）
            var cost = GetCardCost(card, modeIndex);
            if (!CanAfford(core.ElementPool, cost, player, GetPendingCastCosts(core, player)))
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
            // 2026-09-14 代价强制：原价账单预检，混付口径同 PlayCard）
            var cost = GetCardCost(card, modeIndex);
            if (!CanAfford(core.ElementPool, cost, player, GetPendingCastCosts(core, player)))
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
            // 代价强制（2026-09-14 撤销「代价可选」）：付费步强制执行全部 Payload 代价并按全价
            // 获得黑/白（每回合封顶 1/色，AddMana 钳制）——无选择窗口、无减费通道；
            // 补偿先于代价执行（2026-09-13 排序保留：Payload 执行会膨胀模板身价，补偿按打出时点全价）。
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
            if (!await CostCompensationService.PayWithCompensationAsync(specialCosts, costCtx))
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

        // ======================================== 响应窗口（2026-09-16 战斗接入栈机器定案） ========================================

        /// <summary>
        /// 收集响应窗口候选（发动弹窗条目）：速度门 + **双过滤**（元素费用 CanAfford 含 pending 承诺
        /// + 特殊代价 executor.CanActivate 内含 CostHandlerRegistry）——付不起的不进弹窗。
        /// 候选=0 → 调用方直接 Pass（跳过弹窗）。本期候选=守卫能力（速度1响应）+ 待发自愿效果 +
        /// 启动式能力（回合方主阶段）；手牌响应卡待二期窗口 UI 一并接入。
        /// </summary>
        public static List<ResponseOption> CollectAvailableResponses(GameCore core, Player p)
        {
            var options = new List<ResponseOption>();
            if (core?.StackEngine == null || p == null || core.IsGameOver) return options;
            var engine = core.StackEngine;
            bool isTurnPlayer = p == engine.ActivePlayer;

            // ① 守卫能力（速度1响应）：栈上有对方的攻击宣言 → 己方未横置守卫生物
            foreach (var obj in engine.GetStackContents())
            {
                if (obj == null || !obj.IsAttackDeclaration || obj.Controller == p) continue;
                if (!engine.SpeedCounter.CanActivate(StackEngine.GuardStackSpeed, isTurnPlayer,
                        EffectActivationType.Voluntary)) continue;
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
                break; // 响应最先宣言的攻击（LIFO 顶层由后续窗口轮次覆盖）
            }

            // ② 待发自愿效果（速度门已过）+ 可付性过滤（启动式走 executor.CanActivate：费用+代价+条件）
            var executor = engine.GetExecutor();
            foreach (var pe in engine.GetActivatableVoluntaryEffects(p))
            {
                var def = pe.Effect;
                if (def == null) continue;
                if (def.IsActivatedEffect)
                {
                    // 启动式：回合方+主阶段+横置/沉默/沉睡/条件/费用全检（executor.CanActivate）
                    if (!isTurnPlayer || engine.CurrentPhase != PhaseType.Main) continue;
                    if (!executor.CanActivate(def, pe.Source, p, engine.ActivePlayer, engine.CurrentPhase,
                            core.TurnEngine.TurnNumber)) continue;
                }
                options.Add(new ResponseOption
                {
                    Kind = def.IsActivatedEffect
                        ? ResponseOption.ResponseKind.ActivatedAbility
                        : ResponseOption.ResponseKind.VoluntaryEffect,
                    Pending = pe,
                    SourceCard = pe.Source as Card,
                    Definition = def,
                    Label = $"{(pe.Source != null ? EffectText.Name(pe.Source) + " 的 " : "")}{def.DisplayName}",
                });
            }

            return options;
        }

        /// <summary>
        /// 响应窗口泵（交互式 DrainStack，2026-09-16）：循环——当前优先权方收集候选：
        /// 0 候选 → 自动 Pass（跳过弹窗）；有候选 → 人类弹窗（HumanResponder）/AI 决策/无头自动放弃；
        /// 选择 → 应用（守卫入栈/效果发动）→ 优先权翻转继续；双 Pass → 结算（await 完成整条链，
        /// 含 FinishResolution 新开窗口）→ 循环直到栈空且无待发（稳定）。
        /// </summary>
        public static async Cysharp.Threading.Tasks.UniTask SettleResponseWindowAsync(GameCore core, int maxRounds = 96)
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
                if (options.Count > 0)
                {
                    if (holder.IsAI)
                        choice = ResponseWindowService.AiResponder?.Invoke(core, holder, options)
                                 ?? DefaultAiRespond(core, holder, options);
                    else if (ResponseWindowService.HumanResponder != null)
                        choice = await ResponseWindowService.HumanResponder(holder, options);
                    // 均未注册（无头/训练）→ choice=null 自动 Pass（守卫不响应=训练旧行为）
                }

                if (choice != null && ApplyResponse(core, holder, choice))
                    continue; // 发动成功 → 优先权已翻转，继续下一轮

                await engine.PassPriority(holder); // 0 候选/放弃/失败 → Pass（双 Pass 触发结算并 await 完成）
            }
        }

        /// <summary>应用所选响应：守卫=PushGuardDeclaration；启动式=ActivateEffect；自愿效果=PlayerActivateVoluntary。</summary>
        private static bool ApplyResponse(GameCore core, Player p, ResponseOption opt)
        {
            if (opt == null) return false;
            switch (opt.Kind)
            {
                case ResponseOption.ResponseKind.GuardAbility:
                    return core.StackEngine.PushGuardDeclaration(opt.SourceCard, opt.AttackInstance, p);
                case ResponseOption.ResponseKind.ActivatedAbility:
                    return opt.Definition != null && opt.SourceCard != null
                        && ActivateEffect(core, p, opt.Definition, opt.SourceCard);
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

            // 外给目标硬校验收口（2026-09-13 定案修复）：声明期 UI/AI 直接给定的目标不经候选解析——
            // 补两道同口径检查：源紊乱不可指角色；对手有嘲讽卡时对方侧仅嘲讽卡可指
            //（edict 原子=牺牲/摒弃豁免帷幕——选择权在目标方）。非法目标剔除（效果对其空转），不整卡拒绝。
            bool edictExempt = defs != null && defs.Any(d =>
                d?.Effects != null && d.Effects.Any(a => a != null && TargetResolver.IsEdict(a.Type)));
            targets = TargetResolver.FilterPreselectedTargets(targets, player, player, core.ZoneManager, edictExempt);
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

            var combat = core.CombatSystem;
            if (!combat.CanDeclareAttack(attacker, player)) return false;
            if (!combat.CanAttackTarget(attacker, target, player)) return false;

            return core.StackEngine.PushAttackDeclaration(attacker, target, player);
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
        /// 横置为固定代价（警戒不抵扣——2026-09-10 重定义为「横置也能反击」）；预检在 EffectExecutionEngine.CanActivate。
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

            // 发动成功 → 消耗横置（仅启动式；固定代价，不发不扣）
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
        /// 声明 costList 在装载期写为最大模式费，仅供 UI/排序消费；支付与地牌产元素均按所选模式
        /// （GetModeCost / ElementPool.AddCardToPool modeIndex——2026-09-21 地牌自选模式定案）。
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
        /// 检查是否能支付费用（2026-09-14 统一混付口径；public 供 LegalActionEnumerator 共用防掩码/引擎分叉）：
        /// 1. 门槛：卡总费用不得超过当前地牌槽上限（费用上限 9 由此隐含——上限最大 9；按卡判定不累计）
        /// 2. 余量：账单+pending 合并后走账单规划器（同色→灰→黑白；黑白=万用色）；
        ///    pending = 同玩家已声明未结算的整卡施放费用（响应窗口内的支付承诺，一并占用）
        /// </summary>
        public static bool CanAfford(GameCore core, Player player, Dictionary<int, float> cost)
            => CanAfford(core?.ElementPool, cost, player, GetPendingCastCosts(core, player));

        private static bool CanAfford(ElementPoolSystem elementPool, Dictionary<int, float> cost, Player player,
            Dictionary<int, float> pending = null)
        {
            if (cost.Values.Sum() > elementPool.GetLandCap(player))
                return false;

            // 合并声明承诺后统一混付规划（2026-09-14：原逐色精确余量检查退役）
            var bill = ElementPaymentValidator.NormalizeBill(cost);
            foreach (var kv in ElementPaymentValidator.NormalizeBill(pending))
                bill[kv.Key] = bill.TryGetValue(kv.Key, out var v) ? v + kv.Value : kv.Value;
            return ElementPaymentValidator.CanPayBill(
                bill, elementPool.GetPool(player).AvailableMana, elementPool.GetLandCap(player));
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
