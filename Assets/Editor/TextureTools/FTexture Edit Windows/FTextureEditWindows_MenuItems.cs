using UnityEditor;

namespace FIMSpace.FTextureTools
{
    /// <summary>
    /// Assets/Texture Tools 菜单：全中文、按功能分区排序。
    /// 分区靠 priority 间隔 >10 触发 Unity 菜单分隔线，改分组时保持 20 的步进。
    /// 其他分区入口：尺寸调整在 FResizeTools_MenuItems.cs（80-89）、
    /// 通道工具在 FChannelTools_MenuItems.cs（40-43 与本文件法线同区）、
    /// 格式转换在 FTextureUtils_MenuItems.cs（100）。
    /// </summary>
    public static class FTextureEditWindows_MenuItems
    {
        // ── 调色（0-9）──────────────────────────────────────────
        [MenuItem("Assets/Texture Tools/亮度调整", false, 0)]
        public static void BrightnessToolWindow()
        {
            FBrightnessWindow.Init();
        }

        [MenuItem("Assets/Texture Tools/色相饱和度调整", false, 1)]
        public static void HueSaturationToolWindow()
        {
            FHueSaturationWindow.Init();
        }

        [MenuItem("Assets/Texture Tools/颜色曲线", false, 2)]
        public static void ColorCurvesToolWindow()
        {
            FColorCurvesWindow.Init();
        }

        [MenuItem("Assets/Texture Tools/颜色替换", false, 3)]
        public static void OpenColorReplacerWindow()
        {
            FColorReplacerWindow.Init();
        }

        [MenuItem("Assets/Texture Tools/贴图均衡", false, 4)]
        public static void OpenEqualizeTextureWindow()
        {
            FTexEqualizeWindow.Init();
        }

        // ── 生成（20-29）────────────────────────────────────────
        [MenuItem("Assets/Texture Tools/创建无缝贴图", false, 20)]
        public static void OpenSeamlessLooperWindow()
        {
            FSeamlessWindow.Init();
        }

        [MenuItem("Assets/Texture Tools/噪声图生成器", false, 21)]
        public static void OpenNoiseMapGenerator()
        {
            NoiseMapGenerator.NoiseMapGeneratorWindow.Open();
        }

        [MenuItem("Assets/Texture Tools/创建贴图数组", false, 22)]
        public static void OpenTextureArrayGenerator()
        {
            TextureArrayGeneratorEditor.ShowWindow();
        }

        // ── 法线与通道（40-49）──────────────────────────────────
        [MenuItem("Assets/Texture Tools/法线贴图工具", false, 40)]
        public static void OpenNormalToolWindow()
        {
            FNormalToolWindow.Init();
        }

        // ── 混合与绘制（60-69）──────────────────────────────────
        [MenuItem("Assets/Texture Tools/贴图混合工具", false, 60)]
        public static void BlendingToolWindow()
        {
            FBlendToolWindow.Init();
        }

        [MenuItem("Assets/Texture Tools/网格贴图绘制", false, 61)]
        public static void OpenMeshPaintWindow()
        {
            FMeshPaintWindow.Init();
        }
    }
}
