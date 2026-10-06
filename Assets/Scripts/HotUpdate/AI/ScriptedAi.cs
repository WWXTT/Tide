using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.AI;
using Cysharp.Threading.Tasks;

namespace SynergyUI
{
    /// <summary>
    /// 整回合 AI 驱动器抽象（2026-10-02 教学模式）：SimpleAI（动作耗尽启发）与 ScriptedAi（教学剧本）
    /// 同形可换，BattleController 持本接口、缺省 SimpleAI。NeuralAI 同形但由编辑器驱动器直调，不接。
    /// </summary>
    public interface IAiTurnDriver
    {
        UniTask TakeTurnAsync(BattleController ctrl);
    }

    /// <summary>
    /// 教学固定策略驱动器（2026-10-02 教学模式）：按 TutorialConfig.aiScript 逐回合精确执行机器人动作——
    /// 与 SimpleAI 的动作耗尽相反：剧本说什么做什么，多余动作一概不做（教学节奏完全可控）。
    /// 时序镜像 SimpleAI.TakeTurnAsync：跳产元素阶段 → 逐条执行剧本（出牌前横置全部元素、出牌后排干栈
    /// 结算付费；攻击后 await SettleResponseWindowAsync——人类玩家可弹窗响应）→ 排干栈 → 结束回合。
    /// 找牌口径：按 Card.ID 定位（教学卡组每卡 1 张不重复，id 唯一）；动作失败记日志继续
    /// （引擎契约：不付费、卡留手）；该回合无剧本=直接结束回合（教学局不卡死）。
    /// LastTurnReport 记录最近回合逐动作成败，验证器据此断言剧本全量落地。
    /// </summary>
    public sealed class ScriptedAi : IAiTurnDriver
    {
        private readonly TutorialConfig _script;
        private int _aiTurnCount; // 已驱动的机器人回合数（从 1 计）——剧本按机器人自己的回合号编排

        /// <summary>最近一个已执行回合的逐动作成败（验证器断言用；每回合开头清空）。</summary>
        public readonly List<(TutorialAction action, bool ok)> LastTurnReport =
            new List<(TutorialAction, bool)>();

        public ScriptedAi(TutorialConfig script) => _script = script;

        /// <summary>同步入口（编辑器/无头验证：无人类响应者，全链同步完成）。</summary>
        public void TakeTurn(BattleController ctrl)
            => TakeTurnAsync(ctrl).GetAwaiter().GetResult();

        public async UniTask TakeTurnAsync(BattleController ctrl)
        {
            var core = ctrl.Core;
            var me = ctrl.TurnPlayer;
            if (core == null || me == null || me != core.TurnEngine.TurnPlayer) return;

            _aiTurnCount++;
            LastTurnReport.Clear();

            // 0. 跳过产元素阶段进入主阶段（镜像 SimpleAI；剧本的 element 动作在主阶段执行）
            GameActions.SkipElementPool(core, me);

            var turn = _script?.GetAiTurn(_aiTurnCount);
            if (turn?.actions == null || turn.actions.Count == 0)
            {
                TideLog.Warn($"[ScriptedAi] 机器人回合 {_aiTurnCount} 无剧本——直接结束回合（教学剧本未覆盖）");
            }
            else
            {
                foreach (var action in turn.actions)
                {
                    if (core.IsGameOver) return;
                    if (action == null) continue;
                    if (action.type == "end") break; // 提前截断（收尾统一 EndTurn）
                    LastTurnReport.Add((action, await ExecuteAsync(ctrl, core, me, action)));
                }
            }

            // 收尾：排干栈（栈未空时 EndTurn 被栈空守卫静默拒绝）→ 结束回合；游戏已结束不再推进
            GameActions.DrainStack(core);
            if (!core.IsGameOver)
                GameActions.EndTurn(core, me);
        }

        // ======================================== 动作执行 ========================================

        private async UniTask<bool> ExecuteAsync(BattleController ctrl, GameCore core, Player me, TutorialAction action)
        {
            switch (action.type)
            {
                case "element":
                {
                    var card = FindInZone(core, me, Zone.Hand, action.card);
                    return LogResult(action, card != null && GameActions.AddToElementPool(core, me, card),
                        card == null ? $"手牌无 {action.card}" : "入元素池被拒（上限/资格）");
                }
                case "tap":
                {
                    LandTapPolicy.TapAllLands(core, me); // 共享横置策略（SimpleAI/Neural/训练同源，产色匹配手牌）
                    return true;
                }
                case "play":
                {
                    var card = FindInZone(core, me, Zone.Hand, action.card);
                    if (card == null) return LogResult(action, false, $"手牌无 {action.card}");
                    LandTapPolicy.TapAllLands(core, me); // 付费前置（镜像 SimpleAI：出牌前横置全部元素）
                    bool ok = GameActions.PlayCard(core, me, card, ResolveTargets(core, me, action.targets),
                        Zone.Hand, action.mode, out var reason);
                    if (!ok) return LogResult(action, false, $"出牌被拒：{reason}");
                    GameActions.DrainStack(core); // cast 已上栈：双 Pass 结算付费（AI 无响应）
                    return true;
                }
                case "attack":
                {
                    var attacker = FindOnBattlefield(core, me, action.attacker);
                    if (attacker == null) return LogResult(action, false, $"场上无 {action.attacker}");
                    var target = ResolveEntity(core, me, action.target);
                    if (target == null) return LogResult(action, false, $"目标不可解析：{action.target}");
                    if (!GameActions.DeclareAttack(core, me, attacker, target))
                        return LogResult(action, false, "攻击宣言被拒（资格/横置/守卫）");
                    await GameActions.SettleResponseWindowAsync(core); // 速度0开窗：人类玩家可响应
                    return true;
                }
                case "skill":
                {
                    bool ok = await GameActions.ActivateHeroSkill(core, me);
                    if (ok) GameActions.DrainStack(core);
                    return LogResult(action, ok, "英雄技能被拒（费用/横置/沉默/已用）");
                }
                case "tapland":
                {
                    // 横置指定地牌产指定色（教学资源流转演示的精确动作；TapAllLands 之外的可控产色口）
                    var pooled = core.ElementPool.GetPooledCards(me)
                        .FirstOrDefault(pc => pc.SourceCard != null && pc.SourceCard.ID == action.card);
                    if (pooled == null) return LogResult(action, false, $"元素池无 {action.card}");
                    ManaType color = Enum.TryParse<ManaType>(action.target, true, out var t) ? t : ManaType.Gray;
                    return LogResult(action, GameActions.GainElementFromToken(core, me, pooled, color),
                        "地牌产色被拒（横置/无该色指示物/引导闸）");
                }
                case "tapcreature":
                {
                    var creature = FindOnBattlefield(core, me, action.card);
                    if (creature == null) return LogResult(action, false, $"场上无 {action.card}");
                    return LogResult(action, GameActions.TapCreatureForElement(core, me, creature),
                        "生物产色被拒（横置/无地牌特性/引导闸）");
                }
                case "playgrave":
                {
                    // 墓地视手牌中使用（归土仪典轮转核心——PlayCard FromZone=Graveyard 走配额）
                    var card = FindInZone(core, me, Zone.Graveyard, action.card);
                    if (card == null) return LogResult(action, false, $"墓地无 {action.card}");
                    LandTapPolicy.TapAllLands(core, me); // 付费前置（镜像 play）
                    bool ok = GameActions.PlayCardFromGraveyard(core, me, card, ResolveTargets(core, me, action.targets));
                    if (!ok) return LogResult(action, false, "墓地使用被拒（配额/费用/目标）");
                    GameActions.DrainStack(core);
                    return true;
                }
                default:
                    TideLog.Warn($"[ScriptedAi] 未知动作类型：{action.type}（机器人回合 {_aiTurnCount}）");
                    return false;
            }
        }

        private bool LogResult(TutorialAction action, bool ok, string failReason)
        {
            if (!ok)
                TideLog.Warn($"[ScriptedAi] 机器人回合 {_aiTurnCount} 动作失败 {action.type} " +
                             $"{action.card ?? action.attacker}：{failReason}");
            return ok;
        }

        // ======================================== 找牌与目标解析 ========================================

        /// <summary>按卡 id 在指定区域找牌（教学卡组每卡 1 张，id 唯一；找不到 null）。</summary>
        private static Card FindInZone(GameCore core, Player p, Zone zone, string cardId)
            => string.IsNullOrEmpty(cardId) ? null
                : (core.ZoneManager.GetCards(p, zone) ?? new List<Card>())
                    .FirstOrDefault(c => c.ID == cardId);

        /// <summary>按卡 id 找场上单位（先己方后对方战场）。</summary>
        private static Card FindOnBattlefield(GameCore core, Player p, string cardId)
            => FindInZone(core, p, Zone.Battlefield, cardId)
               ?? FindInZone(core, p.Opponent, Zone.Battlefield, cardId);

        /// <summary>目标令牌 → 实体："hero"/空=对方英雄；卡 id=任一侧场上单位。</summary>
        private static Entity ResolveEntity(GameCore core, Player me, string token)
        {
            if (string.IsNullOrEmpty(token) || token == "hero") return me.Opponent;
            return FindOnBattlefield(core, me, token);
        }

        /// <summary>出牌目标串（逗号分隔令牌）→ 目标列表；null/"auto"=引擎自动解析。</summary>
        private static List<Entity> ResolveTargets(GameCore core, Player me, string spec)
        {
            if (string.IsNullOrEmpty(spec) || spec == "auto") return null;
            var list = new List<Entity>();
            foreach (var raw in spec.Split(','))
            {
                var entity = ResolveEntity(core, me, raw.Trim());
                if (entity == null) TideLog.Warn($"[ScriptedAi] 目标令牌不可解析：{raw}");
                else list.Add(entity);
            }
            return list.Count > 0 ? list : null;
        }
    }
}
