using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.AI;
using CardCore.Attribute;
using SynergyUI;

namespace TideSim
{
    /// <summary>
    /// 狩猎重做探针（2026-11-14 定案：删「未横置才触发」闸——回合末强行出击；
    /// 本次战斗击杀防守方任一单位则重置自身，发 UntapEvent）：
    /// · 场景A 横置猎手（紊乱限打单位域）咬死 1/1 → 自身重置 + UntapEvent 恰 1 次；
    /// · 场景B 横置猎手空场打角色（击杀角色不算单位击杀）→ 保持横置、无 UntapEvent；
    /// · 场景C 未横置猎手咬死 1/1 → 维持未横置（重置幂等，不补发事件）。
    /// 夹具=教学卡表：C_030E8991 教学·狩猎 4/2、C_DA499FAE 1/1 白板（反击 1 不足以反杀猎手）。
    /// 用法：dotnet run --project TideSim -- --hunt-reset
    /// </summary>
    internal static class HuntResetProbe
    {
        private const int RngSeed = 20261114;
        private const string HunterId = "C_030E8991"; // 教学·狩猎 4/2 Hunt
        private const string VictimId = "C_DA499FAE"; // 1/1 白板

        private static int _fail;
        private static GameBoard.BoardState _board;

        private static void Assert(bool ok, string what)
        {
            Console.WriteLine($"    {(ok ? "✓" : "✗")} {what}");
            if (!ok) _fail++;
        }

        public static int Run()
        {
            CardCatalog.LoadAll();

            Console.WriteLine("【狩猎重做】场景A：横置猎手（紊乱限打单位）咬死 1/1 → 重置自身");
            RunCase(lockToUnits: true, hunterTapped: true, expectKill: true);

            Console.WriteLine("【狩猎重做】场景B：横置猎手空场打角色（无单位可杀）→ 保持横置");
            RunCase(lockToUnits: false, hunterTapped: true, expectKill: false);

            Console.WriteLine("【狩猎重做】场景C：未横置猎手咬死 1/1 → 维持未横置（重置幂等无事件）");
            RunCase(lockToUnits: true, hunterTapped: false, expectKill: true);

            Console.WriteLine();
            Console.WriteLine(_fail == 0 ? "狩猎重做探针：通过" : $"狩猎重做探针：失败 {_fail} 项");
            return _fail == 0 ? 0 : 1;
        }

        /// <summary>单场景：播种猎手（横置态/紊乱可选）与 1/1 猎物（或空场），EndTurn 触发回合末狩猎后即断言
        /// （不打 Pump——AI 回合不驱动，状态读取零干扰；击杀口径=IsAlive 落账，SBA 送葬不影响读数）。</summary>
        private static void RunCase(bool lockToUnits, bool hunterTapped, bool expectKill)
        {
            var core = GameCore.Instance;
            CardCore.Attribute.Handlers.SummonTokenHandler.ResolveTemplate = CardCatalog.GetById;

            var sc = new TutorialScenario
            {
                player = new TutorialSideState
                {
                    units = new List<TutorialUnitSpec>
                    {
                        new TutorialUnitSpec
                        {
                            card = HunterId,
                            tapped = hunterTapped,
                            counters = lockToUnits
                                ? new List<TutorialCounterSpec>
                                {
                                    new TutorialCounterSpec { key = KeywordRules.RushSicknessCounter, value = 1 }
                                }
                                : null,
                        }
                    },
                    deck = new List<string> { HunterId, HunterId, HunterId },
                },
                ai = new TutorialSideState
                {
                    units = expectKill
                        ? new List<TutorialUnitSpec> { new TutorialUnitSpec { card = VictimId } }
                        : new List<TutorialUnitSpec>(),
                    deck = new List<string> { VictimId, VictimId, VictimId },
                },
            };

            var deck1 = TutorialScenario.DeckIdsOf(sc.player).Select(CardCatalog.GetById).ToList();
            var deck2 = TutorialScenario.DeckIdsOf(sc.ai).Select(CardCatalog.GetById).ToList();
            core.InitGame(deck1, deck2, copiesPerCard: 1, rngSeed: RngSeed, lockDeckOrder: true,
                skipOpeningDraw: true, deferStart: true, ai1: true, ai2: true);
            core.Player1.IsAI = true;
            core.Player2.IsAI = true;
            AttachBoard(core);
            ResponseWindowService.AiResponder = AiStrategyRegistry.PickFor;
            TutorialScenarioSeeder.Apply(core, sc, _board);
            core.StartGame();

            var p1 = core.Player1; var p2 = core.Player2;
            var hunter = OnField(core, p1, HunterId);
            var victim = expectKill ? OnField(core, p2, VictimId) : null;
            Assert(hunter != null && hunter.IsTapped() == hunterTapped, $"猎手播种到位（横置={hunterTapped}）");
            if (expectKill) Assert(victim != null, "猎物 1/1 播种到位");

            int untapEvents = 0;
            Action<UntapEvent> onUntap = e =>
            {
                if (e.UntappedEntity == hunter && !e.IsTurnStartReset) untapEvents++;
            };
            EventManager.Instance.Subscribe(onUntap);
            try
            {
                GameActions.EndTurn(core, p1); // 回合末：狩猎在系统区首位同步结算
            }
            finally
            {
                EventManager.Instance.Unsubscribe(onUntap);
            }

            if (expectKill)
            {
                Assert(victim == null || !victim.IsAlive, "猎物被咬死（4 ≥ 1）");
                Assert(hunter != null && hunter.IsAlive && hunter.GetLife() == 1,
                    $"猎手吃 1 点反击存活（2→1，实际 {hunter?.GetLife() ?? -1}）");
                Assert(hunter != null && !hunter.IsTapped(),
                    hunterTapped
                        ? "击杀奖励：横置猎手被重置（未横置）"
                        : "击杀奖励幂等：未横置猎手维持未横置");
                int expectEvents = hunterTapped ? 1 : 0;
                Assert(untapEvents == expectEvents,
                    $"UntapEvent 计数（期望 {expectEvents}，实际 {untapEvents}）");
            }
            else
            {
                Assert(p2.Life == 26, $"空场=咬角色 30→26（实际 {p2.Life}）");
                Assert(hunter != null && hunter.IsTapped(), "无单位击杀：横置猎手不重置");
                Assert(untapEvents == 0, $"UntapEvent 计数（期望 0，实际 {untapEvents}）");
            }

            // 场间清理（同 KeywordTutorialCheck.EndSegment）
            AiStrategyRegistry.Clear();
            ResponseWindowService.AiResponder = null;
            TargetSelectionService.AiOrder = null;
            CardCore.Attribute.Handlers.SummonTokenHandler.ResolveTemplate = null;
            DetachBoard();
        }

        private static Card OnField(GameCore core, Player p, string id)
            => (core.ZoneManager.GetCards(p, Zone.Battlefield) ?? new List<Card>())
                .FirstOrDefault(c => c.ID == id);

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
