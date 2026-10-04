using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameBoard;
using SynergyUI;
using UnityEditor;
using UnityEngine;
using CardCore;
using CardCore.Attribute;
using CardCore.Network;
using CardCore.Serialization;

namespace CardCore.Editor
{
    /// <summary>
    /// 战场辅助检查器（2026-09-26）：针对 2026-09-24"阶段四重做"仓促交付的战场三层补端到端盲区——
    /// 棋盘派生层（事件驱动占用一致性）、快照投影↔客户端格位重建（双实现漂移锁死）、
    /// 容量/尸体边界、静态扩展点生命周期卫生（本地局退出接线归零）。
    ///
    /// 与 CardPipelineVerifier.TestBoard 的分工：TestBoard 管布局常量/点测数学/手动 Resync 确定性/
    /// 容量闸门；本检查器管**全量性质遍历、事件驱动一致性、网络/本地格位对拍、生命周期卫生**。
    /// 可独立运行（Tools/战场专项验证 / -executeMethod），也可经 RunEmbedded 并入主验证统计。
    /// </summary>
    public static class BattlefieldVerifier
    {
        private static System.Action<bool, string> _sink;
        private static int _pass;
        private static int _fail;
        private static string _crumbPath;

        [MenuItem("Tools/战场专项验证")]
        public static void Run()
        {
            _pass = 0; _fail = 0;
            _sink = (ok, label) =>
            {
                if (ok) { _pass++; Debug.Log($"[BoardVerify] PASS — {label}"); }
                else
                {
                    _fail++;
                    Debug.LogError($"[BoardVerify] FAIL — {label}");
                    try { File.AppendAllText(Path.Combine("Logs", "VerifyFailures.txt"), $"[BoardVerify] {label}\n"); } catch { }
                }
            };
            RunAll();
            Debug.Log($"[BoardVerify] 完成 — PASS={_pass} FAIL={_fail}");
            try { File.AppendAllText(Path.Combine("Logs", "VerifyStats.txt"),
                $"{System.DateTime.Now:HH:mm:ss} [BoardVerify] PASS={_pass} FAIL={_fail}\n"); } catch { }
            if (_fail == 0) Debug.Log("✅ 战场辅助检查全绿：数学性质/事件驱动占用/格位对拍/容量边界/接线卫生");
            else Debug.LogError($"❌ 战场辅助检查存在 {_fail} 项失败");
        }

        /// <summary>并入主验证器统计（断言汇入 CardPipelineVerifier 的 PASS/FAIL 口径）。</summary>
        public static void RunEmbedded(System.Action<bool, string> sink)
        {
            _sink = sink;
            RunAll();
        }

        private static void RunAll()
        {
            _crumbPath = Path.Combine("Logs", "BattlefieldCrumb.txt");
            try { Directory.CreateDirectory("Logs"); File.WriteAllText(_crumbPath, $"== battlefield verify {System.DateTime.Now:HH:mm:ss} ==\n"); } catch { }
            Crumb("entry");

            Crumb("→S1 数学全量性质");
            S1_MathProperties();
            Crumb("→S2 事件驱动占用一致性");
            S2_AutoResyncConsistency();
            Crumb("→S3 格位三方对拍");
            S3_NetCellParity();
            Crumb("→S4 容量边界");
            S4_CapacityEdges();
            Crumb("→S6 标死尸体占格");
            S6_CorpseOccupancy();
            Crumb("→S5 接线生命周期卫生");
            S5_WiringHygiene(); // 最后跑：StartNewGame 会重置 GameCore 单例
            Crumb("done");
        }

        private static void Assert(bool cond, string label)
        {
            if (_sink == null) return;
            _sink(cond, label);
        }

        private static void Crumb(string s)
        {
            try { File.AppendAllText(_crumbPath, s + "\n"); } catch { }
        }

        // ======================================== 公共夹具 ========================================

        /// <summary>极简夹具生物（灰1 费无效果——可付性不受浓度上限卡喉；卡身份按名字区分）。</summary>
        private static CardData SimpleCreature(string name, int power = 2, int life = 3)
            => new CardData
            {
                ID = "BFVERIFY_" + name,
                CardName = name,
                Supertype = Cardtype.Creature,
                Power = power,
                Life = life,
                Cost = CardCore.ElementCost.FromValue(ManaType.Gray, 1.0f), // 灰1（同古树守卫）：T1 浓度上限内可付
            };

        /// <summary>
        /// 开一局全新对局（正式卡池过滤仪式；miss 时退化夹具卡组）并推进到主阶段。
        /// 固定种子保证洗牌可复现。
        /// </summary>
        private static void InitFreshGame(out GameCore core, out Player p1, out Player p2)
        {
            var catalog = new List<CardData>();
            try { catalog.AddRange(CardCatalog.LoadAll()); } catch { }
            var deckData = catalog.Count > 0
                ? catalog.ToList()
                : Enumerable.Range(0, 8).Select(i => SimpleCreature("退化卡组生物" + i)).ToList();
            var deck1 = CardLoader.BuildDeck(deckData, 1);
            var deck2 = CardLoader.BuildDeck(deckData, 1);
            ZoneContainer.Reseed(20260926);
            core = GameCore.Instance;
            core.InitGame(deck1, deck2);
            p1 = core.Player1;
            p2 = core.Player2;
            GameActions.SkipElementPool(core, p1);

            // bank 灌满（TestBoard 同款）：夹具操作不受费用/浓度上限卡喉
            foreach (var p in new[] { p1, p2 })
            {
                var bank = core.ElementPool.GetPool(p);
                foreach (ManaType t in System.Enum.GetValues(typeof(ManaType)))
                    bank.AvailableMana[t] = 99;
            }
        }

        /// <summary>注入夹具生物到手牌并打出（同步排干栈；失败带精确拒绝原因）。</summary>
        private static Card PlayCreature(GameCore core, Player owner, CardData data)
        {
            var card = new CardWrapper(data);
            card.SetController(owner);
            core.ZoneManager.GetZoneContainer(owner).Add(card, Zone.Hand);
            if (!GameActions.PlayCard(core, owner, card, null, Zone.Hand, 0, out string reject))
                Debug.LogError($"[BoardVerify] 夹具生物打出失败：{data.CardName}（原因：{reject}）");
            GameActions.DrainStack(core);
            return card;
        }

        /// <summary>占用快照串（RuntimeId@x,z 排序——确定性对比，双实现/双次重建通用）。</summary>
        private static string SnapshotOf(BoardState board)
        {
            var entries = new List<string>();
            for (int z = 0; z < BoardMath.Height; z++)
                for (int x = 0; x < BoardMath.Width; x++)
                {
                    var role = BoardLayout.RoleOf(x, z);
                    if (role != CellRole.Unit && role != CellRole.Land) continue;
                    var card = board.CardAt(x, z);
                    if (card != null) entries.Add($"{card.RuntimeId}@{x},{z}");
                }
            entries.Sort();
            return string.Join("|", entries);
        }

        /// <summary>双方战场卡/地牌都在己方归属格（占用↔区域列表↔布局三方一致）。</summary>
        private static bool OwnersCorrect(GameCore core, BoardState board, Player p1, Player p2, out string detail)
        {
            var bad = new List<string>();
            foreach (var (p, seat) in new[] { (p1, 0), (p2, 1) })
            {
                foreach (var c in core.ZoneManager.GetCards(p, Zone.Battlefield))
                {
                    bool ok = board.TryGetCell(c, out int x, out int z)
                              && BoardLayout.RoleOf(x, z) == CellRole.Unit
                              && BoardLayout.OwnerOf(x, z) == seat;
                    if (!ok) bad.Add($"单位{c.RuntimeId}");
                }
                foreach (var c in core.ZoneManager.GetCards(p, Zone.ElementPool))
                {
                    bool ok = board.TryGetCell(c, out int x, out int z)
                              && BoardLayout.RoleOf(x, z) == CellRole.Land
                              && BoardLayout.OwnerOf(x, z) == seat;
                    if (!ok) bad.Add($"地牌{c.RuntimeId}");
                }
            }
            detail = string.Join(",", bad);
            return bad.Count == 0;
        }

        // ======================================== S1 六边形数学全量性质 ========================================

        private static void S1_MathProperties()
        {
            // 全盘邻接双射：Neighbor(A,d)=B（界内）⟹ Neighbor(B,Opposite(d))=A；且邻格六距恒 1
            int bijectionBad = 0, neighborDistBad = 0, symmetricBad = 0, zeroBad = 0;
            var dirs = (BoardDirection[])System.Enum.GetValues(typeof(BoardDirection));
            for (int z = 0; z < BoardMath.Height; z++)
                for (int x = 0; x < BoardMath.Width; x++)
                    foreach (var d in dirs)
                    {
                        var (nx, nz) = BoardMath.Neighbor(x, z, d);
                        if (!BoardMath.InBounds(nx, nz)) continue;
                        var (bx, bz) = BoardMath.Neighbor(nx, nz, BoardMath.Opposite(d));
                        if (bx != x || bz != z) bijectionBad++;
                        if (BoardMath.HexDistance(x, z, nx, nz) != 1) neighborDistBad++;
                    }

            // 距离对称 + 自距 0（全盘两两枚举 104²）
            var all = new List<(int x, int z)>();
            for (int z = 0; z < BoardMath.Height; z++)
                for (int x = 0; x < BoardMath.Width; x++) all.Add((x, z));
            foreach (var a in all)
            {
                if (BoardMath.HexDistance(a.x, a.z, a.x, a.z) != 0) zeroBad++;
                foreach (var b in all)
                    if (BoardMath.HexDistance(a.x, a.z, b.x, b.z) != BoardMath.HexDistance(b.x, b.z, a.x, a.z))
                        symmetricBad++;
            }

            Assert(bijectionBad == 0, $"S1 全盘邻接双射成立（违例 {bijectionBad}）");
            Assert(neighborDistBad == 0, $"S1 全盘界内邻格六距恒 1（违例 {neighborDistBad}）");
            Assert(symmetricBad == 0 && zeroBad == 0, $"S1 六距对称且自距 0（对称违例 {symmetricBad}、自距违例 {zeroBad}）");

            // 箭头六向全双射 + Opposite 对合（TestBoard 只点测了 1 条回路）
            bool arrowOk = true, oppositeOk = true;
            foreach (CardCore.HexDirection a in System.Enum.GetValues(typeof(CardCore.HexDirection)))
            {
                if (a == CardCore.HexDirection.None || a == CardCore.HexDirection.All) continue;
                if (BoardMath.ArrowOf(BoardMath.MapArrow(a)) != a) arrowOk = false;
            }
            foreach (var d in dirs)
                if (BoardMath.Opposite(BoardMath.Opposite(d)) != d) oppositeOk = false;

            Assert(arrowOk, "S1 卡面箭头↔棋盘方向六向全双射（MapArrow/ArrowOf 互逆）");
            Assert(oppositeOk, "S1 Opposite 对合（镜像往返恒等）");
        }

        // ======================================== S2 事件驱动占用一致性（AutoResync） ========================================

        private static void S2_AutoResyncConsistency()
        {
            InitFreshGame(out var core, out var p1, out var p2);
            var board = new BoardState(core, p1, p2, HalfFieldData.Flat(), HalfFieldData.Flat());
            board.EnableAutoResync(); // 订阅 8 类事件自动全量重建

            void CheckState(string step)
            {
                Assert(board.IsConsistent(), $"S2[{step}] 自动占用双向索引成对一致");
                var fresh = new BoardState(core, p1, p2);
                fresh.Resync();
                string autoSnap = SnapshotOf(board), freshSnap = SnapshotOf(fresh);
                Assert(autoSnap == freshSnap,
                       $"S2[{step}] 事件驱动占用 == 手动重建（逐格一致）。自动[{autoSnap}] 手动[{freshSnap}]");
                fresh.Dispose();
                Assert(OwnersCorrect(core, board, p1, p2, out string bad), $"S2[{step}] 双方单位/地牌都在己方归属格（不符 {bad}）");
            }

            CheckState("开局");

            // 1. p1 出生物（CardPutToBattlefieldEvent 路径）
            var u1 = PlayCreature(core, p1, SimpleCreature("一致性生物A"));
            CheckState("p1出生物");

            // 2. p2 直接入场（TryAddToBattlefield 衍生物路径）
            var u2 = new CardWrapper(SimpleCreature("一致性生物B"));
            u2.SetController(p2);
            Assert(core.ZoneManager.TryAddToBattlefield(u2, p2), "S2 p2 衍生物入场成功");
            CheckState("p2衍生物入场");

            // 3. p1 放地牌（ElementPoolAddEvent 路径）
            var land = new CardWrapper(SimpleCreature("一致性地牌", 1, 1));
            land.SetController(p1);
            core.ZoneManager.GetZoneContainer(p1).Add(land, Zone.Hand);
            Assert(GameActions.AddToElementPool(core, p1, land), "S2 放地牌成功");
            CheckState("放地牌");

            // 4. 控制权迁移（仿真 ChangeControl 的真实事件序列：Remove+SetController+Add
            //     之后补发 CardPutToBattlefieldEvent(Source=ControlChange)——见 SecondBatchEffectHandlers）
            core.ZoneManager.GetZoneContainer(p1).Remove(u1, Zone.Battlefield);
            u1.SetController(p2);
            core.ZoneManager.GetZoneContainer(p2).Add(u1, Zone.Battlefield);
            // GameCore.PublishEvent 是 internal（Editor 程序集不可见）——直发总线：
            // BoardState.AutoResync 的订阅挂在 EventManager，对拍语义等价
            EventManager.Instance.Publish(new CardPutToBattlefieldEvent
            {
                Card = u1,
                Controller = p2,
                Tapped = false,
                FromZone = Zone.Battlefield,
                Source = EnterSource.ControlChange,
            });
            CheckState("控制权迁移");
            Assert(board.TryGetCell(u1, out int mx, out int mz) && BoardLayout.OwnerOf(mx, mz) == 1,
                   "S2 迁移后占用落在 P2 单位格（按新归属重排）");

            // 5. 效果伤害致死 → SBA 窗口排干 → 送墓（CardZoneChangeEvent/Leave 路径）
            Attribute.KeywordRules.ApplyDamage(p1, u2, u2.GetLife(), isCombat: false);
            GameActions.DrainStack(core);
            CheckState("伤害致死送墓");

            // 6. 直接消灭（DeathRules.TryKill 死透直送）
            var u3 = new CardWrapper(SimpleCreature("一致性生物C"));
            u3.SetController(p1);
            core.ZoneManager.TryAddToBattlefield(u3, p1);
            Attribute.DeathRules.TryKill(u3, Attribute.DeathCause.DestroyEffect, p2, core.ZoneManager);
            CheckState("直接消灭");

            // 7. 移入除外（弹回/放逐族换区）
            core.ZoneManager.MoveCard(u1, p2, Zone.Battlefield, Zone.Exile);
            CheckState("移入除外");

            board.Dispose();
        }

        // ======================================== S3 格位三方对拍（服务器/快照/客户端重建） ========================================

        private static void S3_NetCellParity()
        {
            InitFreshGame(out var core, out var p1, out var p2);

            // 造一个中等场面：双方各 2 单位 + p1 一张地牌
            PlayCreature(core, p1, SimpleCreature("对拍生物A"));
            PlayCreature(core, p1, SimpleCreature("对拍生物B"));
            foreach (var name in new[] { "对拍生物C", "对拍生物D" })
            {
                var c = new CardWrapper(SimpleCreature(name));
                c.SetController(p2);
                core.ZoneManager.TryAddToBattlefield(c, p2);
            }
            var land = new CardWrapper(SimpleCreature("对拍地牌", 1, 1));
            land.SetController(p1);
            core.ZoneManager.GetZoneContainer(p1).Add(land, Zone.Hand);
            GameActions.AddToElementPool(core, p1, land);

            // 服务器权威占用（NetRoom 同款：手动 Resync）
            var server = new BoardState(core, p1, p2, HalfFieldData.Flat(), HalfFieldData.Flat());
            server.Resync();

            // 权威格位表：RuntimeId → (x,z)（战场 + 元素池双源——与 BoardState.Resync 读的列表同源）
            var cellOf = new Dictionary<uint, (int x, int z)>();
            foreach (var p in new[] { p1, p2 })
            {
                foreach (var c in core.ZoneManager.GetCards(p, Zone.Battlefield))
                    if (server.TryGetCell(c, out int x, out int z)) cellOf[c.RuntimeId] = (x, z);
                foreach (var c in core.ZoneManager.GetCards(p, Zone.ElementPool))
                    if (server.TryGetCell(c, out int x, out int z)) cellOf[c.RuntimeId] = (x, z);
            }
            Assert(cellOf.Count == 5, $"S3 对拍场面就绪（5 张在场卡有格位，实际 {cellOf.Count}）");
            if (cellOf.Count == 0) return;

            // 双视角快照 → 客户端重建格位：逐卡与「观察者归一化」的权威全等。
            // BuildNet 2026-09-30 起把格位归一到观察者视角（己方恒 P1 半场 z4/5/6、对手恒 P2 半场
            // z1/2/3——列表序 first-free 与服务器 AssignCells 逐格对应，仅半场标签按视角互换）；
            // 权威侧同规则换算后再比对：校验快照区域列表与服务器区域列表成员+顺序一致、
            // 归一化分格与服务器绝对分格同构（2026-10-02 修——旧断言直接比绝对坐标，视角1 恒误报）。
            for (int seat = 0; seat <= 1; seat++)
            {
                var snap = NetSnapshotBuilder.Build(core, seat);
                var view = BattleView.BuildNet(snap);
                var expect = new Dictionary<uint, (int x, int z)>();
                void MapHalf(Player owner, Zone zone, bool selfHalf)
                {
                    var cells = zone == Zone.Battlefield
                        ? BoardLayout.UnitCells(selfHalf ? 0 : 1)
                        : BoardLayout.LandCells(selfHalf ? 0 : 1);
                    var list = core.ZoneManager.GetCards(owner, zone);
                    for (int i = 0; i < list.Count && i < cells.Count; i++)
                        expect[list[i].RuntimeId] = cells[i];
                }
                bool p1IsSelf = seat == 0;
                MapHalf(p1, Zone.Battlefield, p1IsSelf);
                MapHalf(p1, Zone.ElementPool, p1IsSelf);
                MapHalf(p2, Zone.Battlefield, !p1IsSelf);
                MapHalf(p2, Zone.ElementPool, !p1IsSelf);

                var bad = new List<string>();
                foreach (var v in view.SelfUnits.Concat(view.OppUnits).Concat(view.SelfLands).Concat(view.OppLands))
                    if (!expect.TryGetValue(v.RuntimeId, out var s) || v.X != s.x || v.Z != s.z)
                        bad.Add($"#{v.RuntimeId} 重建({v.X},{v.Z})≠权威({(expect.TryGetValue(v.RuntimeId, out var e2) ? $"{e2.x},{e2.z}" : "缺失")})");
                Assert(expect.Count == 5 && bad.Count == 0,
                       $"S3 客户端重建格位==权威（视角{seat}；覆盖 {expect.Count}/5；不符 {string.Join(";", bad)}）");
            }

            // 本地视图（BoardState 权威直读）与权威表自证
            var local = BattleView.BuildLocal(core, server, p1, p2);
            var badLocal = new List<string>();
            foreach (var v in local.SelfUnits.Concat(local.OppUnits).Concat(local.SelfLands).Concat(local.OppLands))
                if (cellOf.TryGetValue(v.RuntimeId, out var s) && (v.X != s.x || v.Z != s.z))
                    badLocal.Add($"#{v.RuntimeId}");
            Assert(badLocal.Count == 0, $"S3 本地视图格位==权威（不符 {string.Join(",", badLocal)}）");

            server.Dispose();
        }

        // ======================================== S4 容量边界 ========================================

        private static void S4_CapacityEdges()
        {
            InitFreshGame(out var core, out var p1, out var p2);
            var board = new BoardState(core, p1, p2);
            var container = core.ZoneManager.GetZoneContainer(p1);

            // 填满 18 单位：UnitCells 恰好全部占用一次、全部 filler 有格
            var fillers = new List<Card>();
            while (core.ZoneManager.GetCards(p1, Zone.Battlefield).Count < ZoneManager.BattlefieldCapacityPerPlayer)
            {
                var f = new Card { ID = "BFVERIFY_FILLER" };
                f.SetController(p1);
                container.Add(f, Zone.Battlefield);
                fillers.Add(f);
            }
            board.Resync();
            int occupied = BoardLayout.UnitCells(0).Count(c => board.CardAt(c.x, c.z) != null);
            int distinct = BoardLayout.UnitCells(0).Select(c => board.CardAt(c.x, c.z)).Where(c => c != null)
                .Distinct().Count();
            Assert(occupied == BoardLayout.UnitCellsPerPlayer && distinct == BoardLayout.UnitCellsPerPlayer,
                   $"S4 满 18 单位：单位格全部占用且无重复（占用 {occupied}、去重 {distinct}）");
            Assert(fillers.All(f => board.TryGetCell(f, out _, out _)), "S4 每个 filler 都获得单位格");

            // 地牌行溢出：容器层面强塞 10 张（绕过 LandCap 的结构性压力测试）——
            // Min-clamp 前 9 张落格、第 10 张不落格不抛错、索引仍一致
            for (int i = 0; i < BoardLayout.LandCellsPerPlayer + 1; i++)
            {
                var l = new Card { ID = "BFVERIFY_LAND_OVERFLOW" };
                l.SetController(p1);
                container.Add(l, Zone.ElementPool);
            }
            board.Resync();
            int landOccupied = BoardLayout.LandCells(0).Count(c => board.CardAt(c.x, c.z) != null);
            var extras = core.ZoneManager.GetCards(p1, Zone.ElementPool)
                .Where(c => !board.TryGetCell(c, out _, out _)).ToList();
            Assert(landOccupied == BoardLayout.LandCellsPerPlayer,
                   $"S4 地牌溢出 Min-clamp：恰好 {BoardLayout.LandCellsPerPlayer} 格占用（实际 {landOccupied}）");
            Assert(extras.Count == 1, $"S4 溢出地牌恰 1 张不落格（实际 {extras.Count}）");
            Assert(board.IsConsistent(), "S4 溢出后占用双向索引仍一致（不抛错）");

            board.Dispose();
        }

        // ======================================== S6 标死尸体占格 ========================================

        private static void S6_CorpseOccupancy()
        {
            InitFreshGame(out var core, out var p1, out var p2);

            var unit = PlayCreature(core, p1, SimpleCreature("尸体占格生物", 2, 3));

            // 效果伤害致死 = 标死（README 定案：窗口期尸体仍占战场容量/格位，不可被指定但范围照常）
            Attribute.KeywordRules.ApplyDamage(p2, unit, unit.GetLife(), isCombat: false);
            Assert(!unit.IsAlive, "S6 伤害致死标死（IsAlive=false）");
            Assert(core.ZoneManager.GetCards(p1, Zone.Battlefield).Contains(unit), "S6 标死瞬间仍在战场区（战场尸体）");
            var corpseView = new BoardState(core, p1, p2);
            corpseView.Resync();
            Assert(corpseView.TryGetCell(unit, out _, out _), "S6 尸体仍占格（重建视角：尸体计入占用）");
            corpseView.Dispose();

            // SBA 窗口排干 → 到点重查仍死 → 送墓 → 格位释放
            //（SBA 速度1窗口 DrainStack 双 Pass 排不掉是已知残留——收尾同主验证器归因段惯例：CheckAndExecute）
            GameActions.DrainStack(core);
            core.SBAEngine.CheckAndExecute();
            bool inGrave = core.ZoneManager.GetCards(p1, Zone.Graveyard).Contains(unit);
            Assert(inGrave, "S6 SBA 排干后尸体送墓");
            if (inGrave)
            {
                var after = new BoardState(core, p1, p2);
                after.Resync();
                Assert(!after.TryGetCell(unit, out _, out _), "S6 送墓后格位释放");
                after.Dispose();
            }
        }

        // ======================================== S5 接线生命周期卫生 ========================================

        private static void S5_WiringHygiene()
        {
            // 入口卫生：前序验证段/上一局退出后静态扩展点必须归零
            //（本地局泄漏 = AdjacentResolver/LinkAura 指向已过期棋盘——2026-09-26 修复对象）
            Assert(CombatSystem.AdjacentResolver == null, "S5 入口卫生：AdjacentResolver 无残留");
            Assert(!GameBoard.LinkAuraSystem.Enabled, "S5 入口卫生：LinkAuraSystem 未挂接");

            // 本地局生命周期（BattleScreen 本地模式同路径）：开局接线 → 关局归零 → 二连局可重复
            var tiny = Enumerable.Range(0, 8).Select(i => SimpleCreature("卫生生物" + i)).ToList();
            var ctrl = new BattleController();
            ctrl.StartNewGame(tiny, tiny);
            Assert(CombatSystem.AdjacentResolver != null, "S5 本地开局：AdjacentResolver 已接线");
            Assert(GameBoard.LinkAuraSystem.Enabled, "S5 本地开局：LinkAuraSystem 已挂接");

            ctrl.Shutdown(); // BattleScreen.OnExit 退出对局时调用（接线泄漏修复的落点）
            Assert(CombatSystem.AdjacentResolver == null, "S5 关局归零：AdjacentResolver 已清理（无跨局泄漏）");
            Assert(!GameBoard.LinkAuraSystem.Enabled, "S5 关局归零：LinkAuraSystem 已拆线");

            ctrl.StartNewGame(tiny, tiny); // 二连局：归零后可重复接线（上一局 Dispose 不影响新局）
            Assert(CombatSystem.AdjacentResolver != null && GameBoard.LinkAuraSystem.Enabled,
                   "S5 二连局重接线成功");
            ctrl.Shutdown();
            Assert(CombatSystem.AdjacentResolver == null && !GameBoard.LinkAuraSystem.Enabled,
                   "S5 二连局关局归零");
        }
    }
}
