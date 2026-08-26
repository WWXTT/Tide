using Unity.Entities;
using Unity.Mathematics;

namespace HexMap
{
    /// <summary>
    /// 摄像机世界位置单例：由 HexMapCamera（MonoBehaviour）每帧写入，
    /// 流式系统读取以决定加载/卸载哪些 chunk。
    /// </summary>
    public struct HexMapCameraData : IComponentData
    {
        public float3 Position;
    }
}
