using System;
using System.Collections.Generic;
using System.Linq;
using CardCore.Attribute;
using SynergyUI;

namespace CardCore.AI
{
    /// <summary>
    /// 单局对战配置（2026-11-04 单局/批量一致性改造）：
    /// 所有字段都由调用方显式给出——卡组、技能、衍生物绑定、双方策略、seed、回合上限。
    /// 单局与批量只允许在「调用几次」上不同，不允许在准备/推进/收尾上不同。
    /// </summary>
    public sealed class DuelConfig
    {
        public List<CardData> Deck1;
        public List<CardData> Deck2;
        public string SkillCardId1;
        public string SkillCardId2;
        public List<DeckTokenBinding> TokenBindings1;
        public List<DeckTokenBinding> TokenBindings2;
        public AiStrategy Strategy1;
        public AiStrategy Strategy2;
        public int Seed;
        public int MaxTurns = 200;
        public bool ApplySecondPlayerArmor = true;
        public bool Detail;
        public string Label1 = "P1";
        public string Label2 = "P2";
        /// <summary>逐行日志回调（单局窗口/TideSim 共用）；null=不额外输出（仍会进 LogLines）。</summary>
        public Action<string> Log;
    }

    /// <summary>
    /// 单局结果（与驱动无关的纯数据）。Winner 为 Player 实体；调用方可用 GameCore.Player1/2 比对。
    /// </summary>
    public sealed class DuelResult
    {
        public bool Completed;
        public bool TurnLimitReached;
        public Player Winner;
        public string Reason;
        public int TotalTurns;
        public int Life1, Life2;
        public int SkillUses1, SkillUses2;
        public int Plays1, Plays2;
        public List<string> Errors = new List<string>();
        public List<string> LogLines = new List<string>();
    }

    /// <summary>
    /// 脚本 AI 单局执行器（2026-11-04）：Unity 窗口单局/批量与无头 TideSim **共用这一份**。
    /// 只做五件事：InitGame（技能/衍生物/seed）→ 双方 IsAI → 绑定策略/响应窗 → 回合推进 + End→Standby 补泵
    /// → 收尾归零。不包含 UI、ONNX、报告格式；这些在调用方。
    /// </summary>
    public static class AiDuelRunner
    {
        private static GameBoard.BoardState _board;

        public static DuelResult Run(DuelConfig cfg)
        {
            var result = new DuelResult();
            if (cfg == null || cfg.Deck1 == null || cfg.Deck1.Count == 0
                || cfg.Deck2 == null || cfg.Deck2.Count == 0)
            {
                result.Errors.Add("卡组为空（Deck1/Deck2）");
                return result;
            }

            var core = GameCore.Instance;
            GameOverEvent over = null;
            int skill1 = 0, skill2 = 0, plays1 = 0, plays2 = 0;

            void Log(string line)
            {
                result.LogLines.Add(line);
                cfg.Log?.Invoke(line);
            }

            Action<GameOverEvent> onOver = e => over = e;
            Action<CardPlayEvent> onPlay = e =>
            {
                if (e?.Player == null) return;
                bool p1 = ReferenceEquals(e.Player, core.Player1);
                if (p1) plays1++; else plays2++;
                if (cfg.Detail)
                    Log($"[play] T{core.TurnEngine.TurnNumber} {(p1 ? cfg.Label1 : cfg.Label2)} " +
                        $"{NameOf(e.PlayedCard)}{(e.IsResponseCast ? "（连锁）" : "")}");
            };
            Action<HeroSkillActivatedEvent> onSkill = e =>
            {
                if (e?.Player == null) return;
                bool p1 = ReferenceEquals(e.Player, core.Player1);
                if (p1) skill1++; else skill2++;
                if (cfg.Detail)
                    Log($"[skill] T{core.TurnEngine.TurnNumber} {(p1 ? cfg.Label1 : cfg.Label2)} " +
                        $"发动技能 {NameOf(e.SkillCard)}（第 {e.TotalUses} 次）");
            };

            EventManager.Instance.Subscribe(onOver);
            EventManager.Instance.Subscribe(onPlay);
            EventManager.Instance.Subscribe(onSkill);

            // 隐蔽信息可见（2026-11-04 用户定案）：AI 对战/无头验证的开局块允许展示双方牌库与
            // 起手全量（真实对战默认关——日志/导出不得含隐蔽区信息）；finally 恢复原值防串局。
            bool prevHidden = MatchLogRenderer.AllowHiddenInfo;
            MatchLogRenderer.AllowHiddenInfo = true;

            try
            {
                // 召唤衍生物模板解析（与 BattleController/旧驱动同口径；预组无 token 也保持一致）
                CardCore.Attribute.Handlers.SummonTokenHandler.ResolveTemplate = CardCatalog.GetById;
                core.InitGame(cfg.Deck1, cfg.Deck2, copiesPerCard: 1, rngSeed: cfg.Seed,
                    skillCardId1: cfg.SkillCardId1, skillCardId2: cfg.SkillCardId2,
                    tokenBindings1: cfg.TokenBindings1, tokenBindings2: cfg.TokenBindings2,
                    ai1: true, ai2: true);
                core.Player1.IsAI = true;
                core.Player2.IsAI = true;
                if (cfg.ApplySecondPlayerArmor && GameCore.SecondPlayerArmorBonus > 0)
                    core.Player2.AddCounters(KeywordRules.ArmorCounter, GameCore.SecondPlayerArmorBonus, core.Player2);
                AttachBoard(core);

                AiStrategyRegistry.Bind(core.Player1, cfg.Strategy1 ?? AiStrategy.General);
                AiStrategyRegistry.Bind(core.Player2, cfg.Strategy2 ?? AiStrategy.General);
                ResponseWindowService.AiResponder = AiStrategyRegistry.PickFor;

                var ai1 = new SimpleAI(cfg.Strategy1 ?? AiStrategy.General);
                var ai2 = new SimpleAI(cfg.Strategy2 ?? AiStrategy.General);
                var ctrl = new BattleController();

                if (cfg.Detail)
                    Log($"[game] seed={cfg.Seed} P1={cfg.Label1}（{(cfg.Strategy1 ?? AiStrategy.General).Key}）" +
                        $" P2={cfg.Label2}（{(cfg.Strategy2 ?? AiStrategy.General).Key}）" +
                        $"技能={cfg.SkillCardId1 ?? "无"}/{cfg.SkillCardId2 ?? "无"}");

                for (int t = 0; t < cfg.MaxTurns && !core.IsGameOver; t++)
                {
                    var p = core.TurnEngine.TurnPlayer;
                    bool p1 = ReferenceEquals(p, core.Player1);
                    if (cfg.Detail) LogTurnStart(core, p, p1 ? cfg.Label1 : cfg.Label2, Log);
                    (p1 ? ai1 : ai2).TakeTurn(ctrl);
                    if (!core.IsGameOver)
                        core.TurnEngine.CheckPhaseTransition(); // 无帧泵环境的唯一推进口
                }

                result.Completed = core.IsGameOver;
                result.TurnLimitReached = !core.IsGameOver && result.Errors.Count == 0;
                result.Winner = over?.Winner;
                result.Reason = over != null ? over.Reason.ToString() : (result.Completed ? "Unknown" : "MaxTurns");
                result.TotalTurns = over != null ? over.TotalTurns : core.TurnEngine.TurnNumber;
                result.Life1 = core.Player1.Life;
                result.Life2 = core.Player2.Life;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"{ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                MatchLogRenderer.AllowHiddenInfo = prevHidden;
                EventManager.Instance.Unsubscribe(onOver);
                EventManager.Instance.Unsubscribe(onPlay);
                EventManager.Instance.Unsubscribe(onSkill);
                AiStrategyRegistry.Clear();
                ResponseWindowService.AiResponder = null;
                TargetSelectionService.AiOrder = null;
                CardCore.Attribute.Handlers.SummonTokenHandler.ResolveTemplate = null;
                DetachBoard();
            }

            result.SkillUses1 = skill1;
            result.SkillUses2 = skill2;
            result.Plays1 = plays1;
            result.Plays2 = plays2;
            if (cfg.Detail)
            {
                string outcome = result.Errors.Count > 0
                    ? $"异常：{string.Join("；", result.Errors)}"
                    : result.Winner != null
                        ? $"{(ReferenceEquals(result.Winner, core.Player1) ? cfg.Label1 : cfg.Label2)} 胜（{result.Reason}，{result.TotalTurns} 回合）"
                        : $"未完（{result.Reason}，{result.TotalTurns} 回合）";
                Log($"[result] {outcome}｜剩血 {result.Life1}/{result.Life2}｜" +
                    $"技能 {skill1}/{skill2}｜出牌 {plays1}/{plays2}｜seed={cfg.Seed}");
            }
            return result;
        }

        private static void LogTurnStart(GameCore core, Player p, string label, Action<string> log)
        {
            int lands = core.ElementPool.GetPooledCards(p)?.Count ?? -1;
            var hand = (core.ZoneManager.GetCards(p, Zone.Hand) ?? new List<Card>())
                .Select(NameOf).ToList();
            log($"[turn] T{core.TurnEngine.TurnNumber} {label} 地{lands} 血{p.Life}/{p.Opponent?.Life} " +
                $"手[{string.Join("，", hand)}]");
        }

        private static string NameOf(Entity e)
        {
            if (e is CardWrapper w) return w.GetData()?.CardName ?? "?";
            if (e is Player p) return p.Name;
            return e?.GetType().Name ?? "null";
        }

        // 棋盘接线（与旧 AiBattleDriver/DeckStats 同口径）
        private static void AttachBoard(GameCore core)
        {
            DetachBoard();
            _board = new GameBoard.BoardState(core, core.Player1, core.Player2,
                GameBoard.HalfFieldData.Flat(), GameBoard.HalfFieldData.Flat());
            _board.EnableAutoResync();
            CombatSystem.AdjacentResolver = _board.FlankNeighbors;
            CombatSystem.RoleAdjacentResolver = _board.FlankNeighborsOfRole;
            BranchEngines.UnityAdjacentCountOf = _board.AdjacentOccupantCount;
            GameBoard.LinkAuraSystem.Attach(_board);
        }

        private static void DetachBoard()
        {
            _board?.Dispose();
            _board = null;
            CombatSystem.AdjacentResolver = null;
            CombatSystem.RoleAdjacentResolver = null;
            BranchEngines.UnityAdjacentCountOf = null;
            GameBoard.LinkAuraSystem.Detach();
        }
    }
}
