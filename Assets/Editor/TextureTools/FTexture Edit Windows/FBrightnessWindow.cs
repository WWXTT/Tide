using UnityEditor;
using UnityEngine;

namespace FIMSpace.FTextureTools
{
    /// <summary>
    /// 亮度调整（乘法）：Hue-Saturation 的 Value 是 HSV 加性偏移、Color Curves 是
    /// sRGB 域曲线——都会非线性地改对比。本工具提供"线性域乘法"：sRGB→线性→乘
    /// 倍率→sRGB，对 albedo 贴图语义正确（漫反射 albedo 就是线性值，光照按它乘）。
    /// 预览面板实时显示平均线性亮度 before→after，可对着目标 albedo 标定
    /// （例：Sedge 草贴图绿通道等效 albedo≈0.92，压到 0.25~0.35 大约乘 0.3~0.4）。
    /// alpha 原样保留；Apply To File 走基类的 PNG/JPG/TGA/TIFF 编码器。
    /// </summary>
    public class FBrightnessWindow : FTextureProcessWindow
    {
        private float Master = 1f;

        private float ChannelR = 1f;
        private float ChannelG = 1f;
        private float ChannelB = 1f;

        private bool GammaCorrect = true;

        // 预览统计（线性亮度 0~1；-1 = 尚无数据）
        private float lumBefore = -1f;
        private float lumAfter = -1f;

        public static void Init()
        {
            FBrightnessWindow window = (FBrightnessWindow)GetWindow(typeof(FBrightnessWindow));
            window.titleContent = new GUIContent("Brightness", FTextureToolsGUIUtilities.FindIcon("SPR_rgbscale"), "Multiply brightness of target texture (linear-space option)");
            window.previewScale = FEPreview.m_1x1;
            window.drawPreviewScale = true;

            window.previewSize = 128;
            window.position = new Rect(140, 50, 454, 662);
            window.Show();

            called = true;
        }

        protected override void OnGUICustom()
        {
            GUILayout.Space(4);

            GUI.backgroundColor = new Color(0.6f, 1f, 0.7f);
            Master = EditorGUILayout.Slider(new GUIContent("Brightness 倍率"), Master, 0.05f, 4f);
            GUI.backgroundColor = Color.white;

            GammaCorrect = EditorGUILayout.Toggle(
                new GUIContent("线性域乘法 (Gamma Correct)",
                    "勾选: sRGB→线性→乘→sRGB，albedo 语义正确（推荐）\n" +
                    "不勾: 直接在 sRGB 数值上乘，暗部变化更剧烈（旧工具式调法）"),
                GammaCorrect);

            GUILayout.Space(8);
            EditorGUILayout.LabelField("Per-Channel 通道倍率:");
            ChannelR = EditorGUILayout.Slider("  R", ChannelR, 0f, 3f);
            ChannelG = EditorGUILayout.Slider("  G", ChannelG, 0f, 3f);
            ChannelB = EditorGUILayout.Slider("  B", ChannelB, 0f, 3f);
            GUILayout.Space(8);

            if (lumBefore >= 0f)
                EditorGUILayout.HelpBox(
                    "预览平均线性亮度: " + lumBefore.ToString("F3") + "  →  " + lumAfter.ToString("F3") +
                    "\n(漫反射 albedo 即线性值，草/地表类贴图建议落在 0.2~0.45)",
                    MessageType.None);
        }

        protected override void ProcessTexture(Texture2D source, Texture2D target, bool preview = true)
        {
            if (!preview) EditorUtility.DisplayProgressBar("Adjusting Brightness...", "Preparing... ", 2f / 5f);

            Color32[] sourcePixels = source.GetPixels32();
            Color32[] newPixels = source.GetPixels32();

            if (source.width != target.width || source.height != target.height)
            {
                Debug.LogError("[BRIGHTNESS] Source texture is different scale than target texture!");
                return;
            }

            if (!preview) EditorUtility.DisplayProgressBar("Adjusting Brightness...", "Processing pixels... ", 3f / 5f);

            double sumBefore = 0.0;
            double sumAfter = 0.0;
            int pixelCount = sourcePixels.Length;

            for (int p = 0; p < pixelCount; p++)
            {
                Color32 px = sourcePixels[p];

                float r = px.r / 255f;
                float g = px.g / 255f;
                float b = px.b / 255f;

                float lr = GammaCorrect ? Mathf.GammaToLinearSpace(r) : r;
                float lg = GammaCorrect ? Mathf.GammaToLinearSpace(g) : g;
                float lb = GammaCorrect ? Mathf.GammaToLinearSpace(b) : b;

                // 只统计有效不透明像素，避免透明区垃圾 RGB 拉偏平均读数
                if (px.a > 8)
                {
                    sumBefore += 0.2126 * lr + 0.7152 * lg + 0.0722 * lb;
                }

                lr *= Master * ChannelR;
                lg *= Master * ChannelG;
                lb *= Master * ChannelB;

                if (px.a > 8)
                {
                    sumAfter += 0.2126 * lr + 0.7152 * lg + 0.0722 * lb;
                }

                if (GammaCorrect)
                {
                    r = Mathf.LinearToGammaSpace(Mathf.Clamp01(lr));
                    g = Mathf.LinearToGammaSpace(Mathf.Clamp01(lg));
                    b = Mathf.LinearToGammaSpace(Mathf.Clamp01(lb));
                }
                else
                {
                    r = Mathf.Clamp01(lr);
                    g = Mathf.Clamp01(lg);
                    b = Mathf.Clamp01(lb);
                }

                newPixels[p] = new Color32(
                    (byte)(r * 255f + 0.5f),
                    (byte)(g * 255f + 0.5f),
                    (byte)(b * 255f + 0.5f),
                    px.a);
            }

            if (pixelCount > 0)
            {
                lumBefore = (float)(sumBefore / pixelCount);
                lumAfter = (float)(sumAfter / pixelCount);
            }

            if (!preview) EditorUtility.DisplayProgressBar("Adjusting Brightness...", "Applying to texture... ", 4f / 5f);

            target.SetPixels32(newPixels);
            target.Apply(false, false);

            if (!preview) EditorUtility.ClearProgressBar();
        }
    }
}
