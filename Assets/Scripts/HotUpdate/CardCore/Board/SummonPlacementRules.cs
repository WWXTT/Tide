using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;

namespace GameBoard
{
    /// <summary>召唤衍生物落位单源（2026-11-20 计划 §3.1，与 MoveUnitRules 同层）：
    /// 默认计划（SummonTokenHandler 旧算法原样搬入——行为逐字节不变）、己方空格、
    /// 锚点计划候选（AI/UI 共用）、团结达标预测（动作特征 [19] 与 UI 提示共用）、格校验、
    /// 格级团结口径邻接计数（观测 g[79..87]/卡特征 [153] 同源——只数己方活生物，与
    /// BoardState.AdjacentOccupantCount 同过滤）。
    /// 消费方：SummonTokenHandler（结算期逐只按序落位/默认计划）、LegalActionEnumerator
    /// （InteractionVariants 召唤分支）、TideObservation（团结观测）、SimpleAI.PickSummonPlacement。</summary>
    public static class SummonPlacementRules
    {
        // ======================================== 默认计划（旧算法原样搬入） ========================================

        /// <summary>默认计划钉格（=SummonTokenHandler 2026-11-16 起的既有算法，逐字节不变）：
        /// 5 只战斗衍生物优先与「战斗母巢」拼 2×3 六格块（取块首空格）；否则贪心取
        /// 「6 邻己方生物数最高」的空格。现状口径=**所有只同格钉**（历史行为）。返回 (-1,-1)=无棋盘/
        /// 无空格（调用方按未钉处理）。battlefield=控制者战场卡列表（母巢检索用）。</summary>
        public static (int x, int z) DefaultCell(BoardState board, Player controller, Card source,
            IEnumerable<Card> battlefield, int count)
        {
            if (board == null || controller == null) return (-1, -1);
            // 母巢检索（主动效果的 Source 有时是玩家/效果而非母巢本体；落格计划必须拿母巢卡）
            Card mother = source as Card;
            if (mother == null || !(mother is CardCore.CardWrapper mw) || mw.GetData()?.CardName != "战斗母巢")
            {
                foreach (var c in battlefield ?? Enumerable.Empty<Card>())
                {
                    if (c is CardCore.CardWrapper cw && cw.GetData()?.CardName == "战斗母巢") { mother = c; break; }
                }
            }
            var plan = BuildClusterPlan(board, controller, mother, count);
            if (plan != null && plan.Count > 0) return (plan[0].x, plan[0].z);
            var cells = BoardLayout.UnitCells(board.PlayerIndex(controller));
            int bestScore = -1, pinX = -1, pinZ = -1;
            foreach (var cell in cells)
            {
                if (board.CardAt(cell.x, cell.z) != null) continue;
                int score = AdjacentOwnCreatures(board, cell.x, cell.z, controller);
                if (score > bestScore)
                {
                    bestScore = score;
                    pinX = cell.x;
                    pinZ = cell.z;
                }
            }
            return (pinX, pinZ);
        }

        /// <summary>5 只召唤衍生物 + 母巢落成 2×3 六格块（原 SummonTokenHandler 私有实现原样搬入）：
        /// 块内正好两只各有 4 个相邻己方生物 → 团结4 触发两次。返回块内尚空的 5 格（按块序）；
        /// 母巢不在任何 2×3 块/格被占=null 回退。</summary>
        private static List<(int x, int z)> BuildClusterPlan(BoardState board, Player controller,
            Entity source, int count)
        {
            if (board == null || count != 5) return null;
            if (!(source is Card sourceCard)) return null;
            if (!board.TryGetCell(sourceCard, out int mx, out int mz)) return null;
            var cells = BoardLayout.UnitCells(board.PlayerIndex(controller));
            int mi = -1;
            for (int i = 0; i < cells.Count; i++)
                if (cells[i].x == mx && cells[i].z == mz) { mi = i; break; }
            if (mi < 0) return null;

            // 单位格序：远端 4 格 idx0-3，近端 5 格 idx4-8。四个 2×3 块（块内两只达标）。
            int[][] patterns =
            {
                new[] { 0, 1, 2, 4, 5, 6 },
                new[] { 0, 1, 2, 5, 6, 7 },
                new[] { 1, 2, 3, 5, 6, 7 },
                new[] { 1, 2, 3, 6, 7, 8 },
            };
            foreach (var pattern in patterns)
            {
                bool contains = false;
                foreach (int idx in pattern) if (idx == mi) { contains = true; break; }
                if (!contains) continue;
                var result = new List<(int x, int z)>();
                foreach (int idx in pattern)
                {
                    if (idx == mi) continue;
                    var c = cells[idx];
                    if (board.CardAt(c.x, c.z) == null) result.Add(c);
                }
                if (result.Count > 0) return result;
            }
            return null;
        }

        // ======================================== 空格 / 锚点计划 ========================================

        /// <summary>己方单位区空格下标（UnitCells 序 0..8；无棋盘=空表——候选为空=不发动/默认计划）。</summary>
        public static List<int> FreeCells(BoardState board, Player controller)
        {
            var free = new List<int>();
            if (board == null || controller == null) return free;
            var cells = BoardLayout.UnitCells(board.PlayerIndex(controller));
            for (int i = 0; i < cells.Count; i++)
                if (board.CardAt(cells[i].x, cells[i].z) == null) free.Add(i);
            return free;
        }

        /// <summary>锚点计划（计划 §3.3 确定性填充）：首格=anchor；此后每只取「与已选集合任一格
        /// 6 邻」的空格（BoardMath.Neighbor 判邻），同层按「6 邻己方生物数降序 → UnitCells 序」；
        /// 邻接填不满时按 UnitCells 序补空格；仍不足=计划只含可落格数（剩余只结算期照原满场逻辑）。
        /// 返回 UnitCells 下标序列。</summary>
        public static List<int> BuildAnchorPlan(BoardState board, Player controller, int anchor, int count)
        {
            var result = new List<int>();
            if (board == null || controller == null || count <= 0) return result;
            var cells = BoardLayout.UnitCells(board.PlayerIndex(controller));
            if (anchor < 0 || anchor >= cells.Count || board.CardAt(cells[anchor].x, cells[anchor].z) != null)
                return result;
            result.Add(anchor);
            var chosen = new HashSet<int> { anchor };
            while (result.Count < count)
            {
                int next = -1, nextScore = -1;
                // 候选一：与已选集合任一格 6 邻的空格（按 邻接生物数降序 → 格序）
                for (int i = 0; i < cells.Count; i++)
                {
                    if (chosen.Contains(i) || board.CardAt(cells[i].x, cells[i].z) != null) continue;
                    if (!IsAdjacentToAny(board, cells[i], chosen, cells)) continue;
                    int score = AdjacentOwnCreatures(board, cells[i].x, cells[i].z, controller);
                    if (score > nextScore) { nextScore = score; next = i; }
                }
                if (next < 0)
                {
                    // 候选二：任意剩余空格（UnitCells 序）
                    for (int i = 0; i < cells.Count; i++)
                    {
                        if (chosen.Contains(i) || board.CardAt(cells[i].x, cells[i].z) != null) continue;
                        next = i;
                        break;
                    }
                }
                if (next < 0) break; // 无格可补——计划只含已选（剩余只走结算期兜底）
                result.Add(next);
                chosen.Add(next);
            }
            return result;
        }

        private static bool IsAdjacentToAny(BoardState board, (int x, int z) cell,
            HashSet<int> chosen, IReadOnlyList<(int x, int z)> cells)
        {
            foreach (BoardDirection dir in Enum.GetValues(typeof(BoardDirection)))
            {
                var (nx, nz) = BoardMath.Neighbor(cell.x, cell.z, dir);
                foreach (int idx in chosen)
                    if (cells[idx].x == nx && cells[idx].z == nz) return true;
            }
            return false;
        }

        /// <summary>格校验（人类声明期/装载兜底共用）：全部下标合法、属己方单位区、当前为空格。</summary>
        public static bool ValidateCells(BoardState board, Player controller, IReadOnlyList<int> cells)
        {
            if (board == null || controller == null || cells == null || cells.Count == 0) return false;
            var layout = BoardLayout.UnitCells(board.PlayerIndex(controller));
            var seen = new HashSet<int>();
            foreach (var idx in cells)
            {
                if (idx < 0 || idx >= layout.Count || !seen.Add(idx)) return false;
                if (board.CardAt(layout[idx].x, layout[idx].z) != null) return false;
            }
            return true;
        }

        // ======================================== 团结口径计数 / 预测 ========================================

        /// <summary>格级团结口径邻接：6 邻中「controller 方活生物」占格数（与 BoardState.AdjacentOccupantCount
        /// 同过滤——IsAlive ∧ IsLivingUnit ∧ 同 controller；结界/角色格/地牌格/敌方一律不计；封顶 6）。
        /// 观测（g[79..87]/卡特征 [153]）与锚点计划/团结预测共用本单源。</summary>
        public static int AdjacentOwnCreatures(BoardState board, int x, int z, Player controller)
        {
            if (board == null || controller == null) return 0;
            int count = 0;
            foreach (BoardDirection dir in Enum.GetValues(typeof(BoardDirection)))
            {
                var (nx, nz) = BoardMath.Neighbor(x, z, dir);
                if (!BoardMath.InBounds(nx, nz)) continue;
                var occupant = board.CardAt(nx, nz);
                if (occupant != null && occupant.IsAlive && occupant.IsLivingUnit()
                    && ReferenceEquals(occupant.GetController(), controller)) count++;
            }
            return count;
        }

        /// <summary>团结达标预测（计划 §3.3，动作特征 [19]/UI 提示共用）：假定 plan 各格各落一只
        /// 己方生物后，「己方团结卡」立即达标（6 邻己方生物数 ≥ engineParam）的数量——评估集=
        /// 现有战场己方团结卡 ∪ 计划格（若衍生物模板自带团结——plannedUnityThreshold>0，逐格计）。
        /// 无棋盘/无团结=0。封顶 6。</summary>
        public static int PredictUnityHits(BoardState board, Player controller,
            IEnumerable<Card> battlefield, IReadOnlyList<int> plan, int plannedUnityThreshold = 0)
        {
            if (board == null || controller == null) return 0;
            var cells = BoardLayout.UnitCells(board.PlayerIndex(controller));
            var planned = new List<(int x, int z)>();
            if (plan != null)
                foreach (var idx in plan)
                {
                    if (idx >= 0 && idx < cells.Count) planned.Add(cells[idx]);
                }
            // Adj(x,z) = 既有己方生物邻接 + 计划格视作新生物后的真六邻（BoardMath 判邻，去自身格）
            int Adj(int x, int z)
            {
                int n = AdjacentOwnCreatures(board, x, z, controller);
                foreach (var p in planned)
                {
                    if (p.x == x && p.z == z) continue;
                    if (IsSixNeighbor(x, z, p.x, p.z)) n++;
                }
                return Math.Min(n, 6);
            }
            int hits = 0;
            foreach (var c in battlefield ?? Enumerable.Empty<Card>())
            {
                if (c == null || !ReferenceEquals(c.GetController(), controller)) continue;
                if (!board.TryGetCell(c, out int x, out int z)) continue;
                int threshold = UnityThresholdOf(c);
                if (threshold <= 0) continue;
                if (Adj(x, z) >= threshold) hits++;
            }
            if (plannedUnityThreshold > 0)
                foreach (var p in planned)
                    if (Adj(p.x, p.z) >= plannedUnityThreshold) hits++;
            return Math.Min(hits, 6);
        }

        /// <summary>(x,z) 与 (px,pz) 是否六邻（BoardMath.Neighbor 全向比对——与棋盘邻接权威同源）。</summary>
        private static bool IsSixNeighbor(int x, int z, int px, int pz)
        {
            foreach (BoardDirection dir in Enum.GetValues(typeof(BoardDirection)))
            {
                var (nx, nz) = BoardMath.Neighbor(x, z, dir);
                if (nx == px && nz == pz) return true;
            }
            return false;
        }

        /// <summary>卡的团结引擎门槛 x（settle=Engine ∧ engine=Unity ∧ engineParam>0；无=0）。
        /// CardWrapper 按 CardData.Effects 检索首个引擎分支（每回合多次评估的引擎卡按 1 张计）。
        /// 步骤形态与扁平 AtomicEffects 双扫（2026-11-20 验收通用性修复——与 CollectAtoms 同覆盖）。</summary>
        public static int UnityThresholdOf(Card card)
        {
            var data = (card as CardCore.CardWrapper)?.GetData();
            if (data?.Effects == null) return 0;
            foreach (var fx in data.Effects)
            {
                if (fx == null) continue;
                if (fx.Steps != null)
                    foreach (var s in fx.Steps)
                    {
                        var t = UnityThresholdOfEntry(s?.atomic?.branch);
                        if (t > 0) return t;
                    }
                if (fx.AtomicEffects != null)
                    foreach (var a in fx.AtomicEffects)
                    {
                        var t = UnityThresholdOfEntry(a?.branch);
                        if (t > 0) return t;
                    }
            }
            return 0;
        }

        private static int UnityThresholdOfEntry(CardCore.BranchEntryData b)
            => b != null
               && b.settle == (int)CardCore.BranchSettleKind.Engine
               && (CardCore.BranchEngineKind)b.engine == CardCore.BranchEngineKind.Unity
               && b.engineParam > 0
                ? b.engineParam : 0;
    }
}
