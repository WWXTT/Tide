namespace HexMap
{
    //表示相邻cell方位的枚举
    //从左上角顺时针依次开始（与旧版全局 HexDirection 语义一致，放入 HexMap 命名空间避免冲突）
    public enum HexDirection
    {
        NE,
        E,
        SE,
        SW,
        W,
        NW
    }

    public static class HexDirectionExtensions
    {
        /// <summary>
        /// 根据 相邻cell 位于自身的位置，获得自身位于 相邻cell 的位置（相反方位）
        /// W(1) - E(4) 这样的对应关系，之间正好相差 3
        /// </summary>
        public static HexDirection Opposite(this HexDirection direction)
        {
            return (int)direction < 3 ? direction + 3 : direction - 3;
        }

        /// <summary>
        /// 获取当前相邻cell 之前的一个cell的方位
        /// </summary>
        public static HexDirection Previous(this HexDirection direction)
        {
            return direction == HexDirection.NE ? HexDirection.NW : direction - 1;
        }

        /// <summary>
        /// 获取当前相邻cell 之后的一个cell的方位
        /// </summary>
        public static HexDirection Next(this HexDirection direction)
        {
            return direction == HexDirection.NW ? HexDirection.NE : direction + 1;
        }
    }
}
