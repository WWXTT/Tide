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
        // ======================================== 暂停闸（2026-11-21 通用暂停系统） ========================================

        /// <summary>暂停前置（2026-11-21 通用暂停系统·战场遮罩/高亮/暂停计划 §5.3-3）：
        /// 所有玩家意图入口（放地/产色/技能/出牌/攻击/结束回合/发动/响应）统一前置——
        /// Paused 态一律拒绝（引擎主循环与服务器泵已被 CanPerformGameActions 拦住，
        /// 这里堵的是 UI/AI 直调 GameActions 的路径）。只拦 Paused：Ended/NotStarted
        /// 的既有语义（终局/开局前的调用行为）不因本闸改变。内部效果结算链不走玩家
        /// 意图入口，不受影响。</summary>
        private static bool RejectIfPaused(GameCore core, out string reason)
        {
            if (core != null && core.StateManager.CurrentState == GameState.Paused)
            {
                reason = "对局已暂停——等待恢复";
                return true;
            }
            reason = null;
            return false;
        }

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
            if (RejectIfPaused(core, out reason)) return false; // 暂停闸（2026-11-21 通用暂停系统）
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

        /// <summary>
        /// 主阶段：发动英雄技能（2026-10-15 改版：技能=法术，每回合一次，>1 效果时弹窗择定）。
        /// 逻辑全在 HeroSkillSystem.ActivateAsync（守卫+计费+效果）；effectIndex 缺省 0（单效果卡免抉择）。
        /// </summary>
        public static async Cysharp.Threading.Tasks.UniTask<bool> ActivateHeroSkill(GameCore core, Player player, int effectIndex = 0)
            => await HeroSkillSystem.ActivateAsync(core, player, effectIndex);

        /// <summary>带原因版（2026-11-03 失败提示细化）：技能发动全链同步（见 ActivateAsync 尾注），
        /// UI/网络泵线程直调同步核读拒绝文案。暂停闸（2026-11-21 通用暂停系统）前置。</summary>
        public static bool ActivateHeroSkill(GameCore core, Player player, int effectIndex, out string reason)
            => RejectIfPaused(core, out reason) ? false
            : HeroSkillSystem.TryActivateSkill(core, player, effectIndex, out reason);

        /// <summary>动态数量/预选版（2026-11-14）：技能主动效果发动前已选目标与动态数量——
        /// 预检与结算扣费按 base×N（与法术施放同口径）。暂停闸前置（2026-11-21）。</summary>
        public static bool ActivateHeroSkill(GameCore core, Player player, int effectIndex,
            List<Entity> targets, IReadOnlyList<int> dynamicCounts, out string reason)
            => RejectIfPaused(core, out reason) ? false
            : HeroSkillSystem.TryActivateSkill(core, player, effectIndex, targets, dynamicCounts, out reason);

        /// <summary>交互参数版（2026-11-19 P1 v0.3）：interaction=声明期移动落格/宣言卡名——
        /// 随技能发动入栈（LAE 移动/宣言行/驱动透传）；null=旧行为。暂停闸前置（2026-11-21）。</summary>
        public static bool ActivateHeroSkill(GameCore core, Player player, int effectIndex,
            List<Entity> targets, IReadOnlyList<int> dynamicCounts, InteractionParams interaction, out string reason)
            => RejectIfPaused(core, out reason) ? false
            : HeroSkillSystem.TryActivateSkill(core, player, effectIndex, targets, dynamicCounts, interaction, out reason);

        /// <summary>
        /// 主阶段：横置带地牌特性的生物产 1 元素（2026-09-13 英雄技能·培育赋予的特性）。
        /// 颜色随该生物费用构成（多色时自动取首个有份额的颜色；无费用→灰）。
        /// </summary>
        public static bool TapCreatureForElement(GameCore core, Player player, Card creature)
        {
            if (RejectIfPaused(core, out _)) return false; // 暂停闸（2026-11-21 通用暂停系统）
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

            // 产色落**临时钱包**（任一回合结束清空）；2026-11-21 持有钳：入账走 CreditTempWallet
            // 单源（每类型余额封顶地牌上限）——顶格时实发 0 不发事件，横置本身仍照常完成。
            // 事件走 core.PublishEvent 路由路径（Trigger/Layer 引擎可见——AddMana 直发 EventManager
            // 会绕开，故不换路）。
            creature.Tap();
            int granted = core.ElementPool.CreditTempWallet(player, type, 1);
            if (granted > 0)
                core.PublishEvent(new ElementPoolGainEvent
                {
                    Player = player,
                    GainedType = type,
                    FromCard = creature,
                    Amount = granted,
                });
            RuleHooks.OnTutorialCreatureTapped?.Invoke(player, creature);
            return true;
        }

        // 主动横置地牌产元素（2026-11-15）已按用户要求回退删除：地牌只能在付费时由系统自动横置
        //（现取现用），不再有玩家自行横置地牌的口子；生物「地牌特性」产色（TapCreatureForElement）保留。

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
