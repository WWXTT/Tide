using System;
using Unity.Mathematics;

namespace HexMap
{
    /// <summary>
    /// 旧版 HexCoordinates 的 Burst 安全移植。
    /// 轴向坐标（axial）：X + Y + Z = 0，Y = -X - Z；存储为 int2 (X, Z)。
    /// </summary>
    [Serializable]
    public struct HexCoordinates : IEquatable<HexCoordinates>
    {
        /// <summary>x = X（轴向），y = Z（行号）</summary>
        public int2 Value;

        public int X => Value.x;
        public int Z => Value.y;
        public int Y => -Value.x - Value.y;

        public HexCoordinates(int x, int z)
        {
            Value = new int2(x, z);
        }

        /// <summary>
        /// offset 坐标 → 轴向坐标（x 方向锯齿排列改为斜向排列）
        /// </summary>
        public static HexCoordinates FromOffsetCoordinates(int x, int z)
        {
            return new HexCoordinates(x - z / 2, z);
        }

        /// <summary>
        /// 轴向坐标 → offset 坐标（用于 cell 一维索引 / 查找表键）
        /// </summary>
        public int2 ToOffsetCoordinates()
        {
            return new int2(Value.x + Value.y / 2, Value.y);
        }

        /// <summary>
        /// 世界（地图）坐标 → 六边形坐标，含 cube 取整修正（与旧版 FromPosition 一致）
        /// </summary>
        public static HexCoordinates FromPosition(float3 position, in HexMetrics metrics)
        {
            float x = position.x / (metrics.InnerRadius * 2f);
            float y = -x;
            float offset = position.z / (metrics.OuterRadius * 3f);
            x -= offset;
            y -= offset;

            // Mathf.RoundToInt 与 math.round 均为四舍六入五取偶
            int iX = (int)math.round(x);
            int iY = (int)math.round(y);
            int iZ = (int)math.round(-x - y);

            if (iX + iY + iZ != 0)
            {
                float dX = math.abs(x - iX);
                float dY = math.abs(y - iY);
                float dZ = math.abs(-x - y - iZ);

                if (dX > dY && dX > dZ)
                {
                    iX = -iY - iZ;
                }
                else if (dZ > dY)
                {
                    iZ = -iX - iY;
                }
            }

            return new HexCoordinates(iX, iZ);
        }

        public bool Equals(HexCoordinates other) => Value.x == other.Value.x && Value.y == other.Value.y;

        public override bool Equals(object obj) => obj is HexCoordinates other && Equals(other);

        public override int GetHashCode() => unchecked(Value.x * 397 ^ Value.y);

        public override string ToString() => $"({X}, {Y}, {Z})";
    }
}
