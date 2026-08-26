// Entity Hierarchy 场景点选拾取专用 shader：
// 把 MaterialPropertyBlock 注入的 _Id（entity 编码 ID）原样输出到离屏 RT。
// 该材质只通过 Graphics.ExecuteCommandBuffer 立即执行，不参与 SRP 渲染流程，
// 因此用最朴素的 CG unlit 写法（与参考实现 EntitySelection 一致）。
//
// 注意两点（都是字节编码 ID 往返无损的前提）：
// 1. _Id 声明为 Vector 而非 Color：linear 色彩空间下引擎会对 Color 属性做
//    gamma 转换，破坏按字节编码的 ID；Vector 属性原样直达 shader
// 2. 输出必须是全精度 float4，RT 为 UNorm（非 sRGB）格式
Shader "Hidden/EntityHierarchy/EntityPick"
{
    Properties
    {
        _Id ("Entity Id", Vector) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            ZWrite On
            ZTest LEqual

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            float4 _Id;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                return _Id;
            }
            ENDCG
        }
    }
}
