using System.Collections.Generic;
using GameBoard;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>
    /// 战斗演出寻路（2026-10-13 战斗编舞配套·纯表现层）：9×6 六邻接 BFS 最短路，
    /// 供攻击宣言箭头（悬停预览）与战斗走位（编舞器执行）共用同一份路径。
    ///
    /// 与引擎无关——引擎攻击不查距离（BoardState.InAttackRange 未接线），本寻路只为演出；
    /// 占用集由调用方从视图数据构建（双方单位格+地牌格+角色格，攻击者自身除外），
    /// 其余格均可借道（卡组/墓地等特殊格允许演出性穿行，单位不会停驻其上）。
    /// 54 格 BFS 成本可忽略；悬停侧按目标格缓存复用（由调用方负责）。
    /// </summary>
    public static class BattlePathfinder
    {
        /// <summary>攻击方 → 目标的接近路径：终点=目标某个空闲六邻格（总步数最短者）。
        /// 返回途经格序列（不含起点、含接近格）；已相邻=空表（原地攻击）；
        /// 目标周边完全围死=空表（原地攻击演出，日志点名——正常摆位不会发生）。</summary>
        public static List<(int x, int z)> FindApproach(
            int fromX, int fromZ, int targetX, int targetZ, HashSet<int> blocked)
        {
            var empty = new List<(int x, int z)>();
            if (BoardMath.HexDistance(fromX, fromZ, targetX, targetZ) <= 1)
                return empty; // 已在临格：无需走位

            int cells = BoardMath.Width * BoardMath.Height;
            var parent = new int[cells];
            var dist = new int[cells];
            for (int i = 0; i < cells; i++) parent[i] = -1;
            int start = BoardMath.Index(fromX, fromZ);
            parent[start] = start;

            var queue = new Queue<int>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int cur = queue.Dequeue();
                int cx = cur % BoardMath.Width, cz = cur / BoardMath.Width;
                for (int d = 0; d < 6; d++)
                {
                    var (nx, nz) = BoardMath.Neighbor(cx, cz, (BoardDirection)d);
                    if (!BoardMath.InBounds(nx, nz)) continue;
                    int ni = BoardMath.Index(nx, nz);
                    if (parent[ni] != -1 || IsBlocked(blocked, ni)) continue;
                    parent[ni] = cur;
                    dist[ni] = dist[cur] + 1;
                    queue.Enqueue(ni);
                }
            }

            // 目标六邻中挑「空闲且可达、总步数最短」的接近格
            int best = -1, bestDist = int.MaxValue;
            for (int d = 0; d < 6; d++)
            {
                var (nx, nz) = BoardMath.Neighbor(targetX, targetZ, (BoardDirection)d);
                if (!BoardMath.InBounds(nx, nz)) continue;
                int ni = BoardMath.Index(nx, nz);
                if (ni == start) return empty; // 保险：站位即邻（早退未覆盖的等价情形）
                if (IsBlocked(blocked, ni) || parent[ni] == -1) continue;
                if (dist[ni] < bestDist) { best = ni; bestDist = dist[ni]; }
            }

            if (best < 0)
            {
                // 退化：目标周边无「空闲且连通」的邻格。退而求其次选「空闲且离攻击方最近」的
                // 邻格单步直取（演出性穿行）；连空闲邻格都没有（理论围死）才原地攻击。
                int fallback = -1, fallbackDist = int.MaxValue;
                for (int d = 0; d < 6; d++)
                {
                    var (nx, nz) = BoardMath.Neighbor(targetX, targetZ, (BoardDirection)d);
                    if (!BoardMath.InBounds(nx, nz)) continue;
                    int ni = BoardMath.Index(nx, nz);
                    if (IsBlocked(blocked, ni)) continue;
                    int dd = BoardMath.HexDistance(fromX, fromZ, nx, nz);
                    if (dd < fallbackDist) { fallback = ni; fallbackDist = dd; }
                }
                if (fallback < 0)
                {
                    Debug.Log($"[CombatChoreo] 寻路围死：({targetX},{targetZ}) 六邻无空闲格——原地攻击演出");
                    return empty;
                }
                Debug.Log($"[CombatChoreo] 寻路退化：({targetX},{targetZ}) 无连通空闲邻格——单步直取穿行");
                return new List<(int x, int z)> { (fallback % BoardMath.Width, fallback / BoardMath.Width) };
            }

            var path = new List<(int x, int z)>();
            for (int cur = best; cur != start; cur = parent[cur])
                path.Add((cur % BoardMath.Width, cur / BoardMath.Width));
            path.Reverse();
            return path;
        }

        /// <summary>占用集（视图数据口径）：双方单位格（攻击者除外）+地牌格+双方角色格。
        /// 目标自身的格也在占用集内——BFS 只接近其邻格，不进入。</summary>
        public static HashSet<int> BlockedSetOf(BattleViewData d, uint exceptId)
        {
            var set = new HashSet<int>();
            if (d == null) return set;
            foreach (var c in d.SelfUnits) AddCell(set, c.X, c.Z, exceptId, c.RuntimeId);
            foreach (var c in d.OppUnits) AddCell(set, c.X, c.Z, exceptId, c.RuntimeId);
            foreach (var c in d.SelfLands) AddCell(set, c.X, c.Z, exceptId, 0);
            foreach (var c in d.OppLands) AddCell(set, c.X, c.Z, exceptId, 0);
            AddCell(set, BoardLayout.CharacterCell(0).x, BoardLayout.CharacterCell(0).z, exceptId, 0);
            AddCell(set, BoardLayout.CharacterCell(1).x, BoardLayout.CharacterCell(1).z, exceptId, 0);
            return set;
        }

        private static void AddCell(HashSet<int> set, int x, int z, uint exceptId, uint id)
        {
            if (id == exceptId && exceptId != 0) return; // 攻击者自身不阻塞（起点）
            if (!BoardMath.InBounds(x, z)) return;
            set.Add(BoardMath.Index(x, z));
        }

        private static bool IsBlocked(HashSet<int> blocked, int index)
            => blocked != null && blocked.Contains(index);
    }
}
