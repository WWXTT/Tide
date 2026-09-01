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
    /// 方向换算：CardCore.HexDirection（链接箭头 Flags：Up/Down/UpperLeft/...）到本枚举的映射
    /// 在链接系统落地时再定（Up≈NE 方向簇），现在不做。
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
