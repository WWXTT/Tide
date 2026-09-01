using System;

namespace GameBoard
{
    /// <summary>
    /// 玩家半场地形数据（玩家自编辑、可存档、开战时拼合成整场）。
    ///
    /// 半场 = 11×3 = 33 格（玩家自身视角，非旋转）：
    /// ly0 = 远端（近中线）/ ly2 = 贴己方边缘；lx0 = 自己左手边。
    /// 每格两个 int：Elevation（落差，战棋距离预留）+ TerrainIndex（地形类型，增益预留）。
    ///
    /// P2P 平面规则下地形纯观赏（编辑的战略价值在故事模式）。
    /// 存档随地形编辑器（Phase B）落地——本类形状先行（[Serializable] 公共字段，
    /// 兼容 JsonUtility；仿 CardCore.ElementUnlockManager 的 persistentDataPath 模式）。
    /// </summary>
    [Serializable]
    public class HalfFieldData
    {
        public const int Width = 11;
        public const int Height = 3;

        /// <summary>噪声种子（视觉高程扰动的复现凭据；拼场时随半场带入，见 HexMap 约定）</summary>
        public int Seed;

        public int[] Elevations = new int[Width * Height];
        public int[] Terrains = new int[Width * Height];

        public static HalfFieldData Flat() => new HalfFieldData();

        public int ElevationAt(int lx, int ly) => Elevations[ly * Width + lx];

        public int TerrainAt(int lx, int ly) => Terrains[ly * Width + lx];

        public void Set(int lx, int ly, int elevation, int terrain)
        {
            int i = ly * Width + lx;
            Elevations[i] = elevation;
            Terrains[i] = terrain;
        }

        /// <summary>
        /// 半场本地坐标 → 整场坐标。
        /// player 0（P1，下半场）：原样平移 (1+lx, 4+ly)；
        /// player 1（P2，上半场）：自身视角旋转 180° 拼入 (11−lx, 3−ly)。
        /// </summary>
        public static (int x, int z) ToBoard(int player, int lx, int ly)
        {
            return player == 0 ? (1 + lx, 4 + ly) : (BoardMath.Width - 2 - lx, 3 - ly);
        }
    }

    /// <summary>
    /// 双方半场拼合后的整场地形（104 格，row-major）。
    /// 边缘环（38 格）恒为 0——与 HexMap「边界 cell 强制 elevation 0」的裙边约束天然一致。
    /// </summary>
    public class ComposedField
    {
        public readonly int[] Elevations = new int[BoardMath.Width * BoardMath.Height];
        public readonly int[] Terrains = new int[BoardMath.Width * BoardMath.Height];

        public int ElevationAt(int x, int z) => Elevations[BoardMath.Index(x, z)];

        public int TerrainAt(int x, int z) => Terrains[BoardMath.Index(x, z)];

        /// <summary>拼合双方半场（各自带地形；P2 半场旋转 180° 拼入上半场）</summary>
        public static ComposedField Compose(HalfFieldData half1, HalfFieldData half2)
        {
            var field = new ComposedField();
            CopyHalf(field, half1 ?? HalfFieldData.Flat(), 0);
            CopyHalf(field, half2 ?? HalfFieldData.Flat(), 1);
            return field;
        }

        private static void CopyHalf(ComposedField field, HalfFieldData half, int player)
        {
            for (int ly = 0; ly < HalfFieldData.Height; ly++)
            {
                for (int lx = 0; lx < HalfFieldData.Width; lx++)
                {
                    var (x, z) = HalfFieldData.ToBoard(player, lx, ly);
                    int boardIndex = BoardMath.Index(x, z);
                    field.Elevations[boardIndex] = half.ElevationAt(lx, ly);
                    field.Terrains[boardIndex] = half.TerrainAt(lx, ly);
                }
            }
        }
    }
}
