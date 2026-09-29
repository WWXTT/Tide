using System;

namespace GameBoard
{
    /// <summary>棋盘邻居方向（左上角起顺时针，成员与 HexMap.HexDirection 一一对应；独立定义避免与 CardCore 的链接箭头 Flags 枚举混淆）</summary>
    public enum BoardDirection
    {
        NE,
        E,
        SE,
        SW,
        W,
        NW,
    }

    /// <summary>
    /// 对战棋盘的六边形数学（13×8，纯 C#，无 Unity 依赖）。
    ///
    /// 【奇偶约定（odd-r，钉死不改）】尖顶六边形、水平行排列、奇数行向右偏移半格
    /// —— 与 HexMap 完全一致（cell 世界坐标 x = (offsetX + z*0.5 - z/2) × 内径×2，奇数行 +0.5；
    /// 邻居规则同 HexBoundary.NeighborOffset）。Phase B 渲染层若六邻接对不上，先查这里。
    ///
    /// 方向换算：CardCore.HexDirection（链接箭头 Flags）→ BoardDirection 已于 2026-09-09 定死
    /// （MapArrow + Opposite，见下方链接箭头方向映射区；对手视角镜像=绝对方向取 Opposite）。
    /// </summary>
    public static class BoardMath
    {
        public const int Width = 13;
        public const int Height = 8;

        /// <summary>row-major 一维索引（与 HexMap 噪声像素同惯例：index = z * Width + x）</summary>
        public static int Index(int x, int z) => z * Width + x;

        public static bool InBounds(int x, int z) => x >= 0 && x < Width && z >= 0 && z < Height;

        /// <summary>odd-r 邻居 offset 坐标（奇偶规则与 HexMap.HexBoundary.NeighborOffset 逐条一致）</summary>
        public static (int x, int z) Neighbor(int x, int z, BoardDirection dir)
        {
            int odd = z & 1;
            switch (dir)
            {
                case BoardDirection.E:  return (x + 1, z);
                case BoardDirection.W:  return (x - 1, z);
                case BoardDirection.NE: return (x + odd, z + 1);
                case BoardDirection.NW: return (x - 1 + odd, z + 1);
                case BoardDirection.SE: return (x + odd, z - 1);
                case BoardDirection.SW: return (x - 1 + odd, z - 1);
                default: return (x, z);
            }
        }

        // ==================== 链接箭头方向映射（三轨制定案 2026-09-09，本期定死） ====================
        // 接管上方「方向换算落地时再定」的预留：CardCore.HexDirection（卡面箭头 Flags）
        // → BoardDirection（棋盘绝对方向）六对六双射（俯视视角，顺时针一致）。
        // 箭头方向随玩家视角镜像（双方棋盘 180° 对称）：对手半场的箭头按其视角解读 = 绝对方向取 Opposite。

        /// <summary>卡面箭头方向 → 棋盘绝对方向（双射；组合值取首个命中位）</summary>
        public static BoardDirection MapArrow(CardCore.HexDirection arrow)
        {
            switch (arrow)
            {
                case CardCore.HexDirection.Up:        return BoardDirection.NE;
                case CardCore.HexDirection.UpperRight:return BoardDirection.E;
                case CardCore.HexDirection.LowerRight:return BoardDirection.SE;
                case CardCore.HexDirection.Down:      return BoardDirection.SW;
                case CardCore.HexDirection.LowerLeft: return BoardDirection.W;
                case CardCore.HexDirection.UpperLeft: return BoardDirection.NW;
                default: return BoardDirection.NE;
            }
        }

        /// <summary>MapArrow 的逆映射（棋盘绝对方向 → 卡面箭头 Flags 单值；测试/反推用）</summary>
        public static CardCore.HexDirection ArrowOf(BoardDirection dir)
        {
            switch (dir)
            {
                case BoardDirection.NE: return CardCore.HexDirection.Up;
                case BoardDirection.E:  return CardCore.HexDirection.UpperRight;
                case BoardDirection.SE: return CardCore.HexDirection.LowerRight;
                case BoardDirection.SW: return CardCore.HexDirection.Down;
                case BoardDirection.W:  return CardCore.HexDirection.LowerLeft;
                case BoardDirection.NW: return CardCore.HexDirection.UpperLeft;
                default: return CardCore.HexDirection.None;
            }
        }

        /// <summary>棋盘方向的 180° 对向（镜像：NE↔SW、E↔W、SE↔NW）</summary>
        public static BoardDirection Opposite(BoardDirection dir)
        {
            switch (dir)
            {
                case BoardDirection.NE: return BoardDirection.SW;
                case BoardDirection.E:  return BoardDirection.W;
                case BoardDirection.SE: return BoardDirection.NW;
                case BoardDirection.SW: return BoardDirection.NE;
                case BoardDirection.W:  return BoardDirection.E;
                case BoardDirection.NW: return BoardDirection.SE;
                default: return dir;
            }
        }

        /// <summary>odd-r offset → 轴向坐标（与 HexMap.HexCoordinates.FromOffsetCoordinates 一致：q = x − floor(z/2)）</summary>
        public static (int q, int r) ToAxial(int x, int z) => (x - (z - (z & 1)) / 2, z);

        /// <summary>两格六边形距离（cube 系取 max，与棋盘内容无关）</summary>
        public static int HexDistance(int x1, int z1, int x2, int z2)
        {
            var (q1, r1) = ToAxial(x1, z1);
            var (q2, r2) = ToAxial(x2, z2);
            int dq = q1 - q2;
            int dr = r1 - r2;
            int ds = -dq - dr;
            return Math.Max(Math.Abs(dq), Math.Max(Math.Abs(dr), Math.Abs(ds)));
        }
    }
}
