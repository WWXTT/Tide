using Unity.Entities;

namespace HexMap
{
    /// <summary>
    /// cell 的 6 路邻居（按 HexDirection 枚举值索引，长度恒为 6）。
    /// 不存在的邻居为 Entity.Null（与旧版 null 语义一致：不生成连接混合区）。
    /// </summary>
    public struct Neighbors : IBufferElementData
    {
        public Entity Value;
    }
}
