using Unity.Entities;
using UnityEngine;

namespace HexMap
{
    /// <summary>
    /// Shader 全局常量同步系统：把 _ChunkWorldSize 等 HexTerrain 依赖的
    /// 全局 uniform 从 HexMapConfig 单例写入 Shader（幂等，写一次即停）。
    ///
    /// 修复旧缺口：此前只有 HexMapAuthoring.Install()（场景直摆路径）会设置，
    /// SubScene/Baker 路径漏设 → _ChunkWorldSize 为 0，侧面 UV 除零。
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class HexMapShaderGlobalsSystem : SystemBase
    {
        private bool _set;

        protected override void OnCreate()
        {
            RequireForUpdate<HexMapConfig>();
        }

        protected override void OnUpdate()
        {
            if (_set)
                return;

            var configEntity = SystemAPI.GetSingletonEntity<HexMapConfig>();
            var config = SystemAPI.GetComponentRO<HexMapConfig>(configEntity).ValueRO;
            ref var blob = ref config.Blob.Value;

            // 与 HexMapAuthoring.Install 同公式：贴图平铺区域 = 单 cell 脚印
            float tileWorldX = blob.InnerRadius * 2f;
            float tileWorldZ = blob.OuterRadius * 1.5f;
            Shader.SetGlobalVector("_ChunkWorldSize", new Vector4(tileWorldX, tileWorldZ, 0f, 0f));
            _set = true;
        }
    }
}
