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
    /// 狩猎探针（2026-11-14 定案：删「未横置才触发」闸——回合末强行出击，本次战斗击杀防守方
    /// 任一单位则重置自身，发 UntapEvent；2026-11-15 定案：目标=对手当前生命最少的单位）：
    /// · 场景A 横置猎手（紊乱限打单位域）咬死 1/1 → 自身重置 + UntapEvent 恰 1 次；
    /// · 场景B 横置猎手空场打角色（击杀角色不算单位击杀）→ 保持横置、无 UntapEvent；
    /// · 场景C 未横置猎手咬死 1/1 → 维持未横置（重置幂等，不补发事件）；
    /// · 场景D 对手 3/3 在前、1/1 在后 → 自动选**生命最少**的 1/1 击杀（不选先到的 3/3），
    ///   3/3 满血存活、角色不掉血，猎手重置；
    /// · 场景E 对手只剩结界（无生命耐久体，生命读数 0）→ 不参与择敌，直接攻击角色；
    /// · 场景F 角色 6 血 < 对手 9/9 → **角色参与比大小**并被选中（9/9 满血不动）；
    /// · 场景G 角色 9 血 = 对手 9/9 → 同血**角色优先**（9/9 不动）。
    /// 夹具=教学卡表：C_030E8991 教学·狩猎 4/2、C_DA499FAE 1/1 白板、C_925E315C 3/3 白板、
    /// C_EA311EB8 8/9 白板、C_12705A5A 教学·绝缘光环（结界·耐久 9，生命读数 0）。
    ///（反击 1 不足以反杀猎手）。
    /// 用法：dotnet run --project TideSim -- --hunt-reset
    /// </summary>
    internal static class HuntResetProbe
    {
        private const int RngSeed = 20261114;
        private const string HunterId = "C_030E8991"; // 教学·狩猎 4/2 Hunt
        private const string VictimId = "C_DA499FAE"; // 1/1 白板（低血猎物）
        private const string BigId = "C_925E315C";    // 3/3 白板（高血诱饵，播种在前）
        private const string AuraId = "C_12705A5A";   // 结界（无生命耐久体，生命读数 0）
        private const string NinerId = "C_EA311EB8";  // 8/9 白板（高血诱饵，角色比大小用）

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

            Console.WriteLine("【狩猎重做】场景D：对手 3/3 在前、1/1 在后 → 自动选生命最少的 1/1 击杀");
            RunCase(lockToUnits: true, hunterTapped: true, expectKill: true,
                enemyUnits: new List<string> { BigId, VictimId },
                extra: core =>
                {
                    var big = OnField(core, core.Player2, BigId);
                    Assert(big != null && big.IsAlive && big.GetLife() == 3,
                        $"高血诱饵未被误伤（3/3 满血存活，生命 {big?.GetLife() ?? -1}）");
                    Assert(core.Player2.Life == 30, $"角色 30 血非最低，未被选中（实际 {core.Player2.Life}）");
                });

            Console.WriteLine("【狩猎重做】场景E：对手只剩结界（无生物）→ 跳过耐久体、直接打角色");
            RunCase(lockToUnits: false, hunterTapped: true, expectKill: false,
                enemyUnits: new List<string> { AuraId },
                extra: core =>
                {
                    var aura = OnField(core, core.Player2, AuraId);
                    Assert(aura != null && aura.IsAlive && aura.GetLife() == 0,
                        $"结界在场且生命读数 0（实际 {aura?.GetLife() ?? -1}）");
                });

            Console.WriteLine("【狩猎重做】场景F：角色 6 血 < 对手 9/9 → 角色参与比大小并被选中");
            RunCase(lockToUnits: false, hunterTapped: true, expectKill: false, aiLife: 6,
                enemyUnits: new List<string> { NinerId },
                extra: core =>
                {
                    var big = OnField(core, core.Player2, NinerId);
                    Assert(big != null && big.IsAlive && big.GetLife() == 9,
                        $"9/9 未被攻击（角色 6 血更低，实际 9/9 生命 {big?.GetLife() ?? -1}）");
                });

            Console.WriteLine("【狩猎重做】场景G：角色 9 血 = 对手 9/9 → 同血角色优先");
            RunCase(lockToUnits: false, hunterTapped: true, expectKill: false, aiLife: 9,
                enemyUnits: new List<string> { NinerId },
                extra: core =>
                {
                    var big = OnField(core, core.Player2, NinerId);
                    Assert(big != null && big.IsAlive && big.GetLife() == 9,
                        $"同血时 9/9 不动、角色被选中（9/9 生命 {big?.GetLife() ?? -1}）");
                });

            Console.WriteLine();
            Console.WriteLine(_fail == 0 ? "狩猎重做探针：通过" : $"狩猎重做探针：失败 {_fail} 项");
            return _fail == 0 ? 0 : 1;
        }

        /// <summary>单场景：播种猎手（横置态/紊乱可选）与猎物（enemyUnits 缺省=单只 1/1 或空场），
        /// aiLife=对手角色开局生命（缺省 30），EndTurn 触发回合末狩猎后即断言
        /// （不打 Pump——AI 回合不驱动，状态读取零干扰；击杀口径=IsAlive 落账，SBA 送葬不影响读数）。</summary>
        private static void RunCase(bool lockToUnits, bool hunterTapped, bool expectKill,
            List<string> enemyUnits = null, int aiLife = 30, Action<GameCore> extra = null)
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
                    life = aiLife,
                    units = (enemyUnits ?? (expectKill
                            ? new List<string> { VictimId }
                            : new List<string>()))
                        .Select(id => new TutorialUnitSpec { card = id }).ToList(),
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
                Assert(p2.Life == aiLife - 4, $"无击杀=攻击角色 {aiLife}→{aiLife - 4}（实际 {p2.Life}）");
                Assert(hunter != null && hunter.IsTapped(), "无单位击杀：横置猎手不重置");
                Assert(untapEvents == 0, $"UntapEvent 计数（期望 0，实际 {untapEvents}）");
            }

            extra?.Invoke(core); // 场景专属断言（清理前，盘面仍可读）

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
