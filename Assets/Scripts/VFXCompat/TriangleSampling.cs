// INabCommon 兼容垫片（2026-10-18 出场粒子复活）：INab 插件移除时其运行时程序集 INabCommon
// 随之消失且从未入库，而 Dissolve Template.vfx 的 Uniform Mesh Sample 算子按装配名引用其中类型
// （Console："Unable to find type: UnityEngine.VFX.TriangleSampling, INabCommon"）——算子解析失败
// → 图内采样输出恒零/NaN → 粒子出生即死（探针 alive 恒 0）。图零改动，以同名程序集重建该类型
// 即可恢复算子解析（10-15 粒子验收态的原始链路）。
// 布局对齐 TideVfxUniformMeshBaker.TideTriangleSampling（coord Vector2 + index uint，12 字节，
// 与 baker 裸字节写入的 GraphicsBuffer stride 一致）；[VFXType] 供 VSG 编辑器类型登记。
using UnityEngine;

namespace UnityEngine.VFX
{
    // Usage.GraphicsBuffer 必须带：本类型要作 StructuredBuffer<TriangleSampling> 的元素类型
    //（baker 上传的 UniformMeshBuffer）——默认用途仅端口/暴露参数，缺标志=生成的 compute
    // 内核无效（"Kernel at index (0) is invalid"，2026-10-18 实测踩坑）；Default 保留=算子输出
    // 槽（coord/index）仍按端口类型解析。
    [VFXType(VFXTypeAttribute.Usage.Default | VFXTypeAttribute.Usage.GraphicsBuffer)]
    public struct TriangleSampling
    {
        public Vector2 coord;
        public uint index;
    }
}
