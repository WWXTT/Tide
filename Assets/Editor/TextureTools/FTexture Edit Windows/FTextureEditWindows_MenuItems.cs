using UnityEditor;

namespace FIMSpace.FTextureTools
{
    public static class FTextureEditWindows_MenuItems
    {
        [MenuItem("Assets/Texture Tools/创建无缝贴图", false, -102)]
        public static void OpenSeamlessLooperWindow()
        {
            FSeamlessWindow.Init();
        }

        [MenuItem("Assets/Texture Tools/Texture Equalize Window",false,  -101)]
        public static void OpenEqualizeTextureWindow()
        {
            FTexEqualizeWindow.Init();
        }

        [MenuItem("Assets/Texture Tools/Color Replacer Window", false, -100)]
        public static void OpenColorReplacerWindow()
        {
            FColorReplacerWindow.Init();
        }

        [MenuItem( "Assets/Texture Tools/Normal Tool Window", false, -98 )]
        public static void OpenNormalToolWindow()
        {
            FNormalToolWindow.Init();
        }

        [MenuItem( "Assets/Texture Tools/Blending Tool Window", false, -99 )]
        public static void BlendingToolWindow()
        {
            FBlendToolWindow.Init();
        }


        [MenuItem( "Assets/Texture Tools/Mesh Paint Window", false, -100 )]
        public static void OpenMeshPaintWindow()
        {
            FMeshPaintWindow.Init();
        }

        [MenuItem( "Assets/Texture Tools/Color Curves Tool", false, 1 )]
        public static void ColorCurvesToolWindow()
        {
            FColorCurvesWindow.Init();
        }

        [MenuItem( "Assets/Texture Tools/Hue-Saturation Tool", false, 2 )]
        public static void HueSaturationToolWindow()
        {
            FHueSaturationWindow.Init();
        }

        // Tools/美术 三个独立工具入口：窗口本体在 TextureTools/Other/ 与
        // Assets/Editor/DanbaidongGUI/（渐变编辑器已从 Dbdrp 包挪出）
        [MenuItem("Assets/Texture Tools/创建贴图数组", false, 2)]
        public static void OpenTextureArrayGenerator()
        {
            TextureArrayGeneratorEditor.ShowWindow();
        }

        [MenuItem("Assets/Texture Tools/噪声图生成器", false, 2)]
        public static void OpenNoiseMapGenerator()
        {
            NoiseMapGenerator.NoiseMapGeneratorWindow.Open();
        }

        [MenuItem("Assets/Texture Tools/创建渐变贴图", false, 2)]
        public static void OpenGradientsRampEditor()
        {
            UnityEditor.DanbaidongGUI.GradientsRampEditorWindow.ShowWindow();
        }
    }
}