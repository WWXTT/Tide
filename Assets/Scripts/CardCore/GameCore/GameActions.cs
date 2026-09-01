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
        /// 主阶段：打出一张牌。
        /// 流程（发动区流转，Phase A 暂态 = 本方法内完成进出）：
        /// 手牌 → 发动区（公开、可被指向；严格描述上还不算进入战场）→ 结算 →
        /// 永久物入战场（满场预检在付费前拒绝——MD 式，卡留手牌不付费）；
        /// 法术入墓（终态与改造前一致）。
        /// 场上卡发动效果不经发动区（原地发动），那是 StackEngine 路径的事（后续接）。
        /// targets：可选的预选目标（如指向性法术）；为空时由各原子效果按配置自动解析。
        /// </summary>
        public static bool PlayCard(GameCore core, Player player, Card card, List<Entity> targets = null)
        {
            if (core == null || player == null || card == null) return false;
            if (core.TurnEngine.TurnPlayer != player) return false;
            if (core.TurnEngine.CurrentPhase?.Phase != PhaseType.Main) return false;

            // 检查卡牌在手中
            var hand = core.ZoneManager.GetCards(player, Zone.Hand);
            if (!hand.Contains(card)) return false;

            bool isSpell = card is IHasSupertype hasType && hasType.Supertype == Cardtype.Spell;

            // 永久物：战场容量预检（付费前——满场不允许发动，SimpleAI 依赖 false 跳过；
            // 「结算时入场满则失败入墓」由 TryMoveToBattlefield 承担）
            if (!isSpell && !core.ZoneManager.HasBattlefieldSpace(player))
                return false;

            // 读取费用并检查
            var cost = GetCardCost(card);
            var elementPool = core.ElementPool;

            if (!CanAfford(elementPool, cost, player))
                return false;

            // 支付费用
            elementPool.PayCost(cost, player);

            // 设置控制者
            card.SetController(player);

            // 发动开始：手牌 → 发动区（反制指向发动区而非手牌）
            core.ZoneManager.MoveCard(card, player, Zone.Hand, Zone.Activation);
            core.PublishEvent(new CardEnterActivationEvent
            {
                Card = card,
                Controller = player,
                FromZone = Zone.Hand
            });

            if (isSpell)
            {
                // 法术：发动 → 结算 → 入墓。
                // 结算含异步原子（交互选目标等），完成前卡停留在发动区（严格描述：结算中的
                // 法术位于发动区，可被指向）；结算完成后才离区入墓。PlayCard 保持同步 bool 契约，
                // 结算链 fire-and-forget（先例：PassPriority）。
                core.PublishEvent(new CardPlayEvent
                {
                    Player = player,
                    PlayedCard = card
                });

                ResolveSpellEffectsAsync(core, player, card, targets).Forget();
            }
            else
            {
                // 永久物（生物等）：注册触发式效果到本核心的触发引擎，经发动区入场
                var cardEffects = new List<EffectDefinition>();
                var cardDataEffects = GetCardEffectDefinitions(card);
                if (cardDataEffects != null)
                    cardEffects.AddRange(cardDataEffects);

                var keywordEffects = KeywordEffectMapper.CreateAllTriggeredEffects(card);
                if (keywordEffects != null)
                    cardEffects.AddRange(keywordEffects);

                foreach (var effect in cardEffects)
                    core.TriggerEngine.RegisterEffect(effect, card, player);

                core.PublishEvent(new CardPlayEvent
                {
                    Player = player,
                    PlayedCard = card
                });

                // 发动通过 → 入场（预检已过，此处必成功；满则入墓的兜底在 helper 内）
                if (core.ZoneManager.TryMoveToBattlefield(card, player, Zone.Activation))
                {
                    card.WasFormallySummoned = true; // 普通召唤正式入场

                    // 仪式：入场即激活竞速任务（0 费说明书卡；全局唯一任务槽，后发顶先发）
                    RitualSystem.OnPlayed(card, player);
                }
            }

            return true;
        }

        /// <summary>
        /// 结算法术的施放效果（经本核心的 EffectExecutor），完成后离开发动区入墓。
        /// 法术一次性结算：OnPlay 等触发时点在此即是「施放即生效」，直接执行；
        /// 仅手动激活式能力（Activate_*）不随施放自动结算。
        /// 通过 EffectInstance 走与栈结算一致的执行路径，保证目标解析/事件一致。
        /// 元素费已在 PlayCard 支付（skipElementCost 防双计）；特殊代价仍由执行器结算。
        /// </summary>
        private static async Cysharp.Threading.Tasks.UniTask ResolveSpellEffectsAsync(
            GameCore core, Player player, Card card, List<Entity> targets)
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
                        Source = card,
                        Controller = player,
                        Targets = targets != null ? new List<Entity>(targets) : new List<Entity>(),
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

            combat.DeclareAttack(attacker, target);
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

            core.TurnEngine.EndTurn();
            return true;
        }

        // ======================================== 栈操作 ========================================

        /// <summary>
        /// 速度发动：玩家主动发动一个效果
        /// </summary>
        public static bool ActivateEffect(GameCore core, Player player, EffectDefinition effect, Card source, int paidBoost = 0)
        {
            if (core == null || player == null || effect == null) return false;
            if (core.TurnEngine.TurnPlayer != player) return false;
            if (core.TurnEngine.CurrentPhase?.Phase != PhaseType.Main) return false;

            var pending = PendingEffect.Create(
                effect,
                source,
                player,
                core.TurnEngine.TurnPlayer,
                core.TurnEngine.CurrentPhase?.Phase ?? PhaseType.Standby,
                paidBoost: paidBoost);

            return core.StackEngine.PlayerActivateVoluntary(pending);
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
        /// 从卡牌读取费用
        /// </summary>
        private static Dictionary<int, float> GetCardCost(Card card)
        {
            if (card is IHasCost hasCost && hasCost.Cost != null)
                return hasCost.Cost;

            // 默认费用：灰色1点
            return new Dictionary<int, float> { { (int)ManaType.Gray, 1 } };
        }

        /// <summary>
        /// 检查是否能支付费用：
        /// 1. 门槛：卡总费用不得超过当前地牌槽上限（费用上限 9 由此隐含——上限最大 9）
        /// 2. 余量：bank 中各颜色元素充足（bank 无上限，跨回合保留）
        /// </summary>
        private static bool CanAfford(ElementPoolSystem elementPool, Dictionary<int, float> cost, Player player)
        {
            if (cost.Values.Sum() > elementPool.GetLandCap(player))
                return false;

            foreach (var kvp in cost)
            {
                ManaType type = (ManaType)kvp.Key;
                int amount = (int)kvp.Value;
                if (elementPool.GetAvailableManaCount(type, player) < amount)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 从卡牌获取效果定义（如有）
        /// </summary>
        private static List<EffectDefinition> GetCardEffectDefinitions(Card card)
        {
            if (card is CardWrapper wrapper)
            {
                var cardData = wrapper.GetData();
                return CardEffectConverter.ConvertAll(cardData.Effects, cardData.ID);
            }
            return new List<EffectDefinition>();
        }
    }

    /// <summary>
    /// CardData 包装标记接口
    /// </summary>
    public interface CardDataWrapper { }
}
