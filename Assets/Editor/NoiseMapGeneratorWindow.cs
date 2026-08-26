/* Ported from Unreal Engine NoiseMapGenerator plugin
   Original: Copyright UNmisterIZE(Sebastien Durocher) 2025
   Unity port: 2026 */

using UnityEngine;
using UnityEditor;

namespace NoiseMapGenerator
{
    public class NoiseMapGeneratorWindow : EditorWindow
    {
        // Settings (persisted via EditorPrefs for simplicity)
        int width = 1024, height = 1024, tileRepeats = 1, seed = 1337, octaves = 5;
        float frequency = 0.25f, lacunarity = 2f, gain = 0.5f;
        bool normalize = true;
        NoiseMode mode = NoiseMode.FBM;
        NoiseAlgorithm algorithm = NoiseAlgorithm.Perlin;
        NoiseOutputMode outputMode = NoiseOutputMode.Grayscale;
        string outputPath = "Assets/Noise", baseName = "T_Noise";
        bool overwrite = false;

        Texture2D previewTex;
        Vector2 scrollPos;

        [MenuItem("Window/Noise Map Generator")]
        static void Open() => GetWindow<NoiseMapGeneratorWindow>("Noise Map Generator");

        void OnEnable()
        {
            LoadPrefs();
            Regenerate();
        }

        void OnDisable()
        {
            SavePrefs();
            if (previewTex) DestroyImmediate(previewTex);
        }

        void LoadPrefs()
        {
            width = EditorPrefs.GetInt("NMG_Width", 1024);
            height = EditorPrefs.GetInt("NMG_Height", 1024);
            tileRepeats = EditorPrefs.GetInt("NMG_TileRepeats", 1);
            seed = EditorPrefs.GetInt("NMG_Seed", 1337);
            octaves = EditorPrefs.GetInt("NMG_Octaves", 5);
            frequency = EditorPrefs.GetFloat("NMG_Frequency", 0.25f);
            lacunarity = EditorPrefs.GetFloat("NMG_Lacunarity", 2f);
            gain = EditorPrefs.GetFloat("NMG_Gain", 0.5f);
            normalize = EditorPrefs.GetBool("NMG_Normalize", true);
            mode = (NoiseMode)EditorPrefs.GetInt("NMG_Mode", 1);
            algorithm = (NoiseAlgorithm)EditorPrefs.GetInt("NMG_Algorithm", 0);
            outputMode = (NoiseOutputMode)EditorPrefs.GetInt("NMG_OutputMode", 0);
            outputPath = EditorPrefs.GetString("NMG_OutputPath", "Assets/Noise");
            baseName = EditorPrefs.GetString("NMG_BaseName", "T_Noise");
            overwrite = EditorPrefs.GetBool("NMG_Overwrite", false);
        }

        void SavePrefs()
        {
            EditorPrefs.SetInt("NMG_Width", width);
            EditorPrefs.SetInt("NMG_Height", height);
            EditorPrefs.SetInt("NMG_TileRepeats", tileRepeats);
            EditorPrefs.SetInt("NMG_Seed", seed);
            EditorPrefs.SetInt("NMG_Octaves", octaves);
            EditorPrefs.SetFloat("NMG_Frequency", frequency);
            EditorPrefs.SetFloat("NMG_Lacunarity", lacunarity);
            EditorPrefs.SetFloat("NMG_Gain", gain);
            EditorPrefs.SetBool("NMG_Normalize", normalize);
            EditorPrefs.SetInt("NMG_Mode", (int)mode);
            EditorPrefs.SetInt("NMG_Algorithm", (int)algorithm);
            EditorPrefs.SetInt("NMG_OutputMode", (int)outputMode);
            EditorPrefs.SetString("NMG_OutputPath", outputPath);
            EditorPrefs.SetString("NMG_BaseName", baseName);
            EditorPrefs.SetBool("NMG_Overwrite", overwrite);
        }

        NoiseBakeSettings GatherSettings() => new NoiseBakeSettings
        {
            Width = width, Height = height, TileRepeats = tileRepeats,
            Seed = seed, Octaves = octaves, Frequency = frequency,
            Lacunarity = lacunarity, Gain = gain, Normalize = normalize,
            Mode = mode, Algorithm = algorithm, OutputMode = outputMode,
            OutputPath = outputPath, BaseName = baseName, Overwrite = overwrite
        };

        void Regenerate()
        {
            if (previewTex) DestroyImmediate(previewTex);
            previewTex = NoiseMapCore.CreatePreview(GatherSettings(), tileRepeats);
            Repaint();
        }

        void OnBake()
        {
            var s = GatherSettings();
            // Bake always uses RepeatsUV=1 for a single seamless period
            NoiseMapCore.BakeToAsset(s);
        }

        void OnGUI()
        {
            float leftWidth = position.width * 0.38f;
            float previewWidth = position.width - leftWidth - 8;

            EditorGUILayout.BeginHorizontal(GUILayout.Height(position.height));

            // Left panel — controls
            EditorGUILayout.BeginVertical(GUILayout.Width(leftWidth));
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            bool changed = false;
            int oldSeed = seed, oldTile = tileRepeats;
            NoiseAlgorithm oldAlgo = algorithm;
            NoiseOutputMode oldOut = outputMode;
            NoiseMode oldMode = mode;

            GUILayout.Label("Noise Settings", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();

            width = EditorGUILayout.IntSlider("Width", width, 8, 8192);
            height = EditorGUILayout.IntSlider("Height", height, 8, 8192);
            tileRepeats = EditorGUILayout.IntSlider("Preview Tiling", tileRepeats, 1, 64);
            seed = EditorGUILayout.IntSlider("Seed", seed, 0, int.MaxValue);
            octaves = EditorGUILayout.IntSlider("Octaves", octaves, 1, 12);
            frequency = EditorGUILayout.Slider("Frequency", frequency, 0.001f, 2f);
            lacunarity = EditorGUILayout.Slider("Lacunarity", lacunarity, 1f, 8f);
            gain = EditorGUILayout.Slider("Gain", gain, 0f, 1f);
            normalize = EditorGUILayout.Toggle("Normalize 0..1", normalize);

            GUILayout.Space(4);
            mode = (NoiseMode)EditorGUILayout.EnumPopup("Mode", mode);
            algorithm = (NoiseAlgorithm)EditorGUILayout.EnumPopup("Algorithm", algorithm);
            outputMode = (NoiseOutputMode)EditorGUILayout.EnumPopup("Output", outputMode);

            GUILayout.Space(8);
            GUILayout.Label("Save Options", EditorStyles.boldLabel);
            outputPath = EditorGUILayout.TextField("Save Path", outputPath);
            baseName = EditorGUILayout.TextField("Base Name", baseName);
            overwrite = EditorGUILayout.Toggle("Overwrite Existing", overwrite);

            changed = EditorGUI.EndChangeCheck();

            GUILayout.Space(12);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Generate Preview", GUILayout.Height(28))) Regenerate();
            if (GUILayout.Button("Bake && Save", GUILayout.Height(28))) OnBake();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            // Separator
            EditorGUILayout.BeginVertical(GUILayout.Width(4));
            GUILayout.Box("", GUILayout.Width(2), GUILayout.ExpandHeight(true));
            EditorGUILayout.EndVertical();

            // Right panel — preview
            EditorGUILayout.BeginVertical();
            if (previewTex)
            {
                float aspect = (float)previewTex.width / previewTex.height;
                float displayH = position.height - 16;
                float displayW = displayH * aspect;

                // Scroll if preview wider than panel
                Vector2 pvScroll = Vector2.zero;
                Rect previewRect = GUILayoutUtility.GetRect(displayW, displayH, GUILayout.ExpandWidth(false));
                GUI.DrawTexture(previewRect, previewTex, ScaleMode.ScaleToFit);
            }
            else
            {
                GUILayout.Label("No preview", EditorStyles.centeredGreyMiniLabel);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            // Auto-regenerate when relevant params change
            if (changed && (seed != oldSeed || tileRepeats != oldTile || algorithm != oldAlgo ||
                            outputMode != oldOut || mode != oldMode))
            {
                Regenerate();
            }
        }
    }
}
