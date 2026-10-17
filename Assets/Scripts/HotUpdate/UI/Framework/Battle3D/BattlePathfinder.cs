using System.Collections.Generic;
using GameBoard;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>
    /// 战斗演出寻路（2026-10-13 战斗编舞配套·纯表现层；2026-10-17 改版：不再绕空地）：
    /// 途经=攻击方格→接近格的六邻贪心直线（每步取「离终点最近、世界方向最贴合」的邻格），
    /// 途中不看占用——演出沿格间缝行走（相邻两格共享棱中点，见 CombatChoreographer.SeamWaypoints），
    /// 贴缝穿模可容忍；占用集仅用于挑接近格（优先空闲，围死贴最近）。
    ///
    /// 与引擎无关——引擎攻击不查距离（BoardState.InAttackRange 未接线），本寻路只为演出。
    /// </summary>
    public static class BattlePathfinder
    {
        /// <summary>攻击方 → 目标的接近路径：接近格=目标六邻中离攻击方最近者（同距优先空闲），
        /// 途经=起点格到接近格的贪心直线（恒相邻步、不避占用——缝走+穿模定案）。
        /// 返回途经格序列（不含起点、含接近格）；已相邻=空表（原地攻击）；
        /// 目标六邻全不可用（越界+无tile）=空表（原地攻击演出，日志点名）。</summary>
        public static List<(int x, int z)> FindApproach(
            int fromX, int fromZ, int targetX, int targetZ, HashSet<int> blocked)
        {
            var empty = new List<(int x, int z)>();
            if (BoardMath.HexDistance(fromX, fromZ, targetX, targetZ) <= 1)
                return empty; // 已在临格：无需走位

            // 接近格（战斗站位）：六邻挑「离攻击方最近」者；同距优先空闲（站位不与驻场单位
            // 重叠——穿模只容忍途经）；再按方向序定死。无可用邻格才原地攻击。
            int bestX = -1, bestZ = -1, bestDist = int.MaxValue, bestFree = 0;
            for (int d = 0; d < 6; d++)
            {
                var (nx, nz) = BoardMath.Neighbor(targetX, targetZ, (BoardDirection)d);
                if (!BoardMath.InBounds(nx, nz) || BoardLayout.NoTileAt(nx, nz)) continue;
                int dist = BoardMath.HexDistance(fromX, fromZ, nx, nz);
                int free = IsBlocked(blocked, BoardMath.Index(nx, nz)) ? 0 : 1;
                if (bestX < 0 || dist < bestDist || (dist == bestDist && free > bestFree))
                { bestX = nx; bestZ = nz; bestDist = dist; bestFree = free; }
            }
            if (bestX < 0)
            {
                Debug.Log($"[CombatChoreo] 寻路围死：({targetX},{targetZ}) 六邻无可用格——原地攻击演出");
                return empty;
            }

            // 直线借道：贪心逐步逼近（只取推进步=恒相邻），不避占用（缝走定案）
            var path = new List<(int x, int z)>();
            int cx = fromX, cz = fromZ;
            int guard = BoardMath.HexDistance(fromX, fromZ, bestX, bestZ) * 2 + 4;
            while ((cx != bestX || cz != bestZ) && guard-- > 0)
            {
                int curDist = BoardMath.HexDistance(cx, cz, bestX, bestZ);
                var toDir = BattleBoardGrid.CellCenterLocal(bestX, bestZ)
                          - BattleBoardGrid.CellCenterLocal(cx, cz);
                (int x, int z) step = (cx, cz);
                int stepDist = int.MaxValue;
                float stepDot = float.MinValue;
                for (int d = 0; d < 6; d++)
                {
                    var (nx, nz) = BoardMath.Neighbor(cx, cz, (BoardDirection)d);
                    if (!BoardMath.InBounds(nx, nz)) continue;
                    int dist = BoardMath.HexDistance(nx, nz, bestX, bestZ);
                    if (dist >= curDist) continue; // 只取推进步（未到终点时六邻必有）
                    var dir = BattleBoardGrid.CellCenterLocal(nx, nz)
                            - BattleBoardGrid.CellCenterLocal(cx, cz);
                    float dot = Vector3.Dot(dir.normalized, toDir.normalized);
                    if (dist < stepDist || (dist == stepDist && dot > stepDot))
                    { step = (nx, nz); stepDist = dist; stepDot = dot; }
                }
                if (step.x == cx && step.z == cz) break; // 无推进步（理论不可达）：尽力截断
                cx = step.x;
                cz = step.z;
                path.Add((cx, cz));
            }
            return path;
        }

        /// <summary>占用集（视图数据口径）：双方单位格（攻击者除外）+地牌格+双方角色格+无格缺口。
        /// 目标自身的格也在占用集内——接近格只在目标邻格中挑，不入目标格。</summary>
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
            // 无格缺口（2026-10-14 8×7 中线 x7 槽）：无实体 tile，演出不可借道驻留
            for (int z = 0; z < BoardMath.Height; z++)
                for (int x = 0; x < BoardMath.Width; x++)
                    if (BoardLayout.NoTileAt(x, z)) set.Add(BoardMath.Index(x, z));
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
