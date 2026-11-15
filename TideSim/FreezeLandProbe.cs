using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using SynergyUI;

namespace TideSim
{
    /// <summary>
    /// 冻地探针（2026-11-16 冻结扩展到地牌验收）：表行 973884b1 Domain 扩「双方元素池:卡」后——
    /// ① 产轴收窄：kinds=[2] 现状删元素池 selector（改前基线）；kinds=[2,14] 保留（扩 kinds 后）
    /// ② 候选解析：扩 kinds 轴候选含对方地牌卡（TargetZoneGrouping 侧组也验）
    /// ③ 冻地执行：FreezeHandler 直调 → 池内包装强制横置+叠层
    /// ④ 冻地不产色：付费规划跳过冻地（CanPay/TryPay 双口）
    /// ⑤ 回合钩子：持有者回合末 −1；层&gt;0 回合开始不恢复；归零后恢复
    /// 用法：dotnet run --project TideSim -c Release -- --freeze-land-probe
    /// </summary>
    internal static class FreezeLandProbe
    {
        private const string LandId = "C_C3B25F32";   // 萌芽（绿3——入池锁绿）
        private const string UnitId = "C_F245F2F9";   // 小精灵（P2 场上生物——战场侧候选对照）
        private const string FreezeRow = "973884b1";  // 原子表·冻结行
        private const int RngSeed = 20261116;

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
            Console.WriteLine("【冻地探针】表行 Domain 扩元素池 · 产轴/候选/执行/付费/回合钩子五段");

            SectionA_AxisNarrowing();
            Console.WriteLine();

            TargetSelectionService.AiOrder = null;
            var core = Setup();
            try
            {
                SectionB_Candidates(core);
                Console.WriteLine();
                SectionC_Execute(core);
                Console.WriteLine();
                SectionD_Payment(core);
                Console.WriteLine();
                SectionE_TurnHooks(core);
                Console.WriteLine();
                SectionF_RealCards(core);
            }
            finally { Teardown(core); }

            Console.WriteLine();
            Console.WriteLine(_fail == 0 ? "冻地探针：通过" : $"冻地探针：失败 {_fail} 项");
            return _fail == 0 ? 0 : 1;
        }

        // ① 产轴收窄（纯函数·真实表数据）
        private static void SectionA_AxisNarrowing()
        {
            Console.WriteLine("① 产轴收窄（converter 同式：ParseDomain → NarrowByKinds）");
            var row = CardCore.Attribute.AtomicEffectTable.GetByHashId(FreezeRow);
            Assert(row != null, $"原子表行 {FreezeRow} 装载");
            Assert(row.Domain == "双方战场:生物; 双方元素池:卡",
                $"表行 Domain 已扩（实际：{row?.Domain}）");
            Assert(CardCore.TargetAxisRules.TryParseDomain(row.Domain, out var axis)
                   && axis.Selectors.Count == 2, "Domain 新语法解析=2 selector（战场:生物+元素池:卡）");

            // 改前基线：效果行 kinds=[2]（对方单位）→ 元素池 selector 被删
            var narrowedOld = CardCore.TargetAxisRules.NarrowByKinds(axis, new List<int> { 2 });
            Assert(narrowedOld.Selectors.Count == 1
                   && CardCore.TargetRegionRules.ZoneOf(narrowedOld.Selectors[0].Region) == Zone.Battlefield,
                "kinds=[2] 现状：元素池 selector 被收窄删除（只剩对方战场）——改前选不到地");

            // 扩 kinds：[2,14]（对方单位+对方元素池）→ 两 selector 保留并单侧化
            var narrowedNew = CardCore.TargetAxisRules.NarrowByKinds(axis, new List<int> { 2, 14 });
            var zones = narrowedNew.Selectors
                .Select(s => CardCore.TargetRegionRules.ZoneOf(s.Region)).ToList();
            Assert(narrowedNew.Selectors.Count == 2 && zones.Contains(Zone.Battlefield)
                   && zones.Contains(Zone.ElementPool),
                "kinds=[2,14]：战场+元素池两 selector 均保留");
            Assert(narrowedNew.Selectors.All(
                      s => CardCore.TargetRegionRules.SideOf(s.Region) == CardCore.RegionSide.Opposing),
                "kinds 全对方侧：两 selector 均收窄为对方侧（实例 kinds 优先，不再极性收窄）");
        }

        // ② 候选解析（真实引擎摆盘）
        private static void SectionB_Candidates(GameCore core)
        {
            Console.WriteLine("② 候选解析（TargetAxisResolver.GetCandidates·扩 kinds 轴）");
            var p1 = core.Player1;
            var p2 = core.Player2;
            var row = CardCore.Attribute.AtomicEffectTable.GetByHashId(FreezeRow);
            CardCore.TargetAxisRules.TryParseDomain(row.Domain, out var axis);
            var narrowed = CardCore.TargetAxisRules.NarrowByKinds(axis, new List<int> { 2, 14 });

            var ctx = new EffectExecutionContext
            {
                Source = p1, Controller = p1,
                ZoneManager = core.ZoneManager, ElementPool = core.ElementPool,
            };
            var candidates = CardCore.TargetAxisResolver.GetCandidates(narrowed, ctx);
            var landCards = candidates.OfType<Card>()
                .Where(c => c.GetZone() == Zone.ElementPool).ToList();
            var units = candidates.OfType<Card>()
                .Where(c => c.GetZone() == Zone.Battlefield).ToList();
            Assert(landCards.Count == 2, $"候选含对方地牌×2（实际 {landCards.Count}）");
            Assert(units.Count == 1, $"候选含对方战场生物×1（实际 {units.Count}）");
            Assert(candidates.All(e => e is Card c && (c.GetController() ?? c.GetOwner()) == p2),
                "候选全部归属对方侧（P1 无泄漏）");

            var groups = TargetZoneGrouping.Build(candidates, p1);
            var landGroup = groups.FirstOrDefault(g => g.ZoneRank == 2);
            Assert(landGroup != null && landGroup.Label == "对方元素池" && landGroup.Indices.Count == 2,
                $"区域分组「对方元素池」组成立（实际 {landGroup?.Label ?? "无"}）");
        }

        // ③ 冻地执行（FreezeHandler 直调·P2 地1）
        private static void SectionC_Execute(GameCore core)
        {
            Console.WriteLine("③ 冻地执行（FreezeHandler → 池内包装横置+叠层）");
            var p1 = core.Player1;
            var land1 = PoolCard(core, core.Player2, 0);
            var handler = new CardCore.Attribute.Handlers.FreezeHandler();
            var ctx = new EffectExecutionContext
            {
                Source = p1, Controller = p1,
                ZoneManager = core.ZoneManager, ElementPool = core.ElementPool,
                Targets = new List<Entity> { land1.SourceCard },
            };
            handler.Execute(new AtomicEffectInstance { Type = AtomicEffectType.Freeze, Value = 1 }, ctx);
            Assert(land1.IsTapped, "地1 冻结后强制横置（IsTapped=true）");
            Assert(land1.FreezeLayers == 1, $"地1 冻结层=1（实际 {land1.FreezeLayers}）");
        }

        // ④ 冻地不产色（付费规划跳过冻地）
        private static void SectionD_Payment(GameCore core)
        {
            Console.WriteLine("④ 冻地不产色（CanPay/TryPay 双口）");
            var p2 = core.Player2;
            var land1 = PoolCard(core, p2, 0);
            var land2 = PoolCard(core, p2, 1);
            var bill2 = new ElementCost { [ManaType.Green] = 2 };
            Assert(!core.ElementPool.CanPayCostWithAutoTap(bill2, p2),
                "两绿账单付不起（地1 冻结不可用——仅地2 可贡献 1 绿）");
            var bill1 = new ElementCost { [ManaType.Green] = 1 };
            Assert(core.ElementPool.TryPayCostWithAutoTap(bill1, p2, "冻地探针"),
                "一绿账单可付（未冻地2 横置支付）");
            Assert(land2.IsTapped && land1.IsTapped,
                "支付后：地2 横置（付了 1 绿）、地1 仍冻结横置（未参与支付）");
            Assert(land1.FreezeLayers == 1, "支付不动冻结层（层仍=1）");
            Assert(!core.ElementPool.CanPayCostWithAutoTap(bill1, p2),
                "再付一绿付不起（地2 已横置、地1 仍冻结）——冻地持续不可用");
        }

        // ⑤ 回合钩子（持有者回合末减层/回合开始恢复闸）
        private static void SectionE_TurnHooks(GameCore core)
        {
            Console.WriteLine("⑤ 回合钩子（层>0 不恢复 · 回合末 −1 · 归零恢复）");
            var p2 = core.Player2;
            var land1 = PoolCard(core, p2, 0);
            var land2 = PoolCard(core, p2, 1);

            // 回合开始（层1>0）：地1 不恢复；地2 层0 恢复
            core.ElementPool.OnTurnStart(p2, 41);
            Assert(land1.IsTapped && land1.FreezeLayers == 1,
                "地1 层1>0：回合开始不解除横置");
            Assert(!land2.IsTapped, "地2 无冻层：回合开始恢复横置");

            // 回合末：地1 层 1→0
            core.ElementPool.OnTurnEnd(p2);
            Assert(land1.FreezeLayers == 0, "持有者回合末层 1→0");

            // 下个回合开始：地1 恢复
            core.ElementPool.OnTurnStart(p2, 42);
            Assert(!land1.IsTapped, "层归零后回合开始恢复横置（产色能力回归）");

            // 多层叠加：地2 冻 2 层——回合末 −1、层1 期间回合开始不恢复、再末 −0、再始恢复
            var handler = new CardCore.Attribute.Handlers.FreezeHandler();
            var ctx = new EffectExecutionContext
            {
                Source = core.Player1, Controller = core.Player1,
                ZoneManager = core.ZoneManager, ElementPool = core.ElementPool,
                Targets = new List<Entity> { land2.SourceCard },
            };
            handler.Execute(new AtomicEffectInstance { Type = AtomicEffectType.Freeze, Value = 2 }, ctx);
            Assert(land2.FreezeLayers == 2 && land2.IsTapped, "地2 冻 2 层（叠加=延长）");
            core.ElementPool.OnTurnEnd(p2);
            core.ElementPool.OnTurnStart(p2, 43);
            Assert(land2.IsTapped && land2.FreezeLayers == 1, "第 1 回合：层 2→1，仍横置");
            core.ElementPool.OnTurnEnd(p2);
            core.ElementPool.OnTurnStart(p2, 44);
            Assert(!land2.IsTapped && land2.FreezeLayers == 0, "第 2 回合：层 1→0，恢复横置");
        }

        // ⑥ 真实训练卡全链（CardLoader 装载 → converter 产轴 → 原子 Axis 含元素池）
        private static void SectionF_RealCards(GameCore core)
        {
            Console.WriteLine("⑥ 真实训练卡全链（converter 产轴·kinds=[2,14] 生效）");
            // (id, name, selection) 三张冻结宿主卡（2026-11-16 训练表扩域后）
            var hosts = new[]
            {
                ("C_57614AD4", "训练·冻结单体卡", 3),
                ("C_7D1ED886", "训练·全场冻结卡", 5),
                ("C_B1D76E72", "训练·冰封响应卡", 3),
            };
            foreach (var (cardId, name, sel) in hosts)
            {
                var card = CardCatalog.GetById(cardId);
                Assert(card != null, $"{name} 装载（{cardId}）");
                if (card == null) continue;
                var defs = CardEffectConverter.ConvertAll(card.Effects, card.ID);
                var atom = defs.SelectMany(d => d?.Effects ?? new List<AtomicEffectInstance>())
                    .FirstOrDefault(a => a != null && a.RowHashId == FreezeRow);
                Assert(atom != null, $"{name} 转换含冻结原子");
                if (atom == null) continue;
                Assert(defs.Any(d => d != null && (int)d.SelectionMode == sel),
                    $"{name} selection={sel}（多范围档）");
                Assert(atom.TargetKinds != null && atom.TargetKinds.Contains(14),
                    $"{name} 原子 TargetKinds 含 14（对方元素池）");
                var zones = (atom.Axis?.Selectors ?? new List<CardCore.TargetSelector>())
                    .Select(s => CardCore.TargetRegionRules.ZoneOf(s.Region)).ToList();
                Assert(zones.Contains(Zone.ElementPool),
                    $"{name} 原子 Axis 含元素池 selector（实际域 {atom.Axis?.Zh ?? "空"}）");
            }
        }

        private static PooledCard PoolCard(GameCore core, Player owner, int index)
            => core.ElementPool.GetPool(owner).PooledCards[index];

        private static GameCore Setup()
        {
            var core = GameCore.Instance;
            var deck1 = new List<CardData> { CardCatalog.GetById(LandId) };
            var deck2 = new List<CardData>
            {
                CardCatalog.GetById(UnitId),
                CardCatalog.GetById(LandId), CardCatalog.GetById(LandId),
            };
            core.InitGame(deck1, deck2, copiesPerCard: 1, rngSeed: RngSeed, lockDeckOrder: true,
                skipOpeningDraw: true, deferStart: true, ai1: true, ai2: true);
            core.Player1.IsAI = true;
            core.Player2.IsAI = true;
            _board = new GameBoard.BoardState(core, core.Player1, core.Player2,
                GameBoard.HalfFieldData.Flat(), GameBoard.HalfFieldData.Flat());
            _board.EnableAutoResync();
            CombatSystem.AdjacentResolver = _board.FlankNeighbors;
            CombatSystem.RoleAdjacentResolver = _board.FlankNeighborsOfRole;

            var sc = new TutorialScenario
            {
                startTurn = 40,
                player = new TutorialSideState
                {
                    hand = new List<string>(),
                },
                ai = new TutorialSideState
                {
                    lands = new List<TutorialLandSpec>
                    {
                        new TutorialLandSpec { card = LandId, tapped = false, tokenType = "Green" },
                        new TutorialLandSpec { card = LandId, tapped = false, tokenType = "Green" },
                    },
                    units = new List<TutorialUnitSpec> { new TutorialUnitSpec { card = UnitId, x = -1, z = -1 } },
                },
            };
            TutorialScenarioSeeder.Apply(core, sc, _board);
            core.StartGame();
            return core;
        }

        private static void Teardown(GameCore core)
        {
            _board?.Dispose();
            _board = null;
            CombatSystem.AdjacentResolver = null;
            CombatSystem.RoleAdjacentResolver = null;
        }
    }
}
