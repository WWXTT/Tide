using UnityEngine;

namespace BlackHole
{
    /// <summary>
    /// 程序化星空的统一调参入口：星云双色、星云/星星亮度、星空流动速度。
    /// 以 shader 全局量（_SkyNebulaColorA 等）下发，黑洞逃逸光线与天空盒两份
    /// shader 读同一份数值，剪影接缝从机制上不可能裂开——不要改成材质属性。
    /// 挂在相机（黑洞球体的父物体）上即可，编辑态改动实时生效。
    /// 注意：场景里没有本组件时全局量为 0，星空会全黑。
    /// </summary>
    [ExecuteAlways]
    public class BlackHoleSkyTuner : MonoBehaviour
    {
        [Tooltip("星云主色（现在的暗粉来源）")] [ColorUsage(false)]
        public Color nebulaColorA = new Color(0.5f, 0.2f, 0.13f, 1f);

        [Tooltip("星云次色（与主色按噪声混合）")] [ColorUsage(false)]
        public Color nebulaColorB = new Color(0.1f, 0.14f, 0.34f, 1f);

        [Tooltip("星云整体亮度")] [Range(0f, 4f)]
        public float nebulaIntensity = 0.7f;

        [Tooltip("星星整体亮度")] [Range(0f, 6f)]
        public float starIntensity = 1.8f;

        [Tooltip("星点大小（相对格子的比例，越大被透镜拉出的弧越明显）")] [Range(0.02f, 0.25f)]
        public float starSize = 0.1f;

        [Tooltip("星点密度（0-1，格子出现星星的概率）")] [Range(0f, 1f)]
        public float starDensity = 0.28f;

        [Tooltip("星空绕轴流动的角速度")] [Range(0f, 0.1f)]
        public float flowSpeed = 0.018f;

        private void OnEnable() => Apply();

        private void OnValidate() => Apply();

        private void Apply()
        {
            Shader.SetGlobalColor("_SkyNebulaColorA", nebulaColorA);
            Shader.SetGlobalColor("_SkyNebulaColorB", nebulaColorB);
            Shader.SetGlobalFloat("_SkyNebulaIntensity", Mathf.Max(nebulaIntensity, 0f));
            Shader.SetGlobalFloat("_SkyStarIntensity", Mathf.Max(starIntensity, 0f));
            Shader.SetGlobalFloat("_SkyStarSize", Mathf.Max(starSize, 0.01f));
            Shader.SetGlobalFloat("_SkyStarDensity", Mathf.Clamp01(starDensity));
            Shader.SetGlobalFloat("_SkyFlowSpeed", Mathf.Max(flowSpeed, 0f));
        }
    }
}
