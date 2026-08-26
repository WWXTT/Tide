// TextureArrayGeneratorEditor.cs
// 使用 UnityTexture2DArrayImportPipeline 创建 Texture2DArray
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using Oddworm.EditorFramework;

public class TextureArrayGeneratorEditor : EditorWindow
{
    // 贴图类型：决定导入设置（sRGB / 法线 / 数据图）
    public enum TextureKind
    {
        Albedo,             // 颜色图，sRGB 开
        Normal,             // 法线图，textureType = NormalMap
        Height,             // 高度图，数据，sRGB 关
        MetallicSmoothness, // 金属/光滑度，数据，sRGB 关
        Occlusion           // 环境光遮蔽，数据，sRGB 关
    }

    private TextureKind selectedKind = TextureKind.Albedo;
    private List<Texture2D> textures = new List<Texture2D>();

    private int targetSize = 1024;
    private string outputFolder = "Assets/Performance/HexMap/_Materials/GeneratedTextures";

    // 每种类型记住各自的数组文件名
    private readonly Dictionary<TextureKind, string> arrayNames = new Dictionary<TextureKind, string>
    {
        { TextureKind.Albedo, "AlbedoMaps" },
        { TextureKind.Normal, "NormalMaps" },
        { TextureKind.Height, "HeightMaps" },
        { TextureKind.MetallicSmoothness, "MetallicSmoothnessMaps" },
        { TextureKind.Occlusion, "OcclusionMaps" },
    };

    private Vector2 scrollPos;

    // 每种类型的导入设置
    private struct KindSettings
    {
        public TextureImporterType textureType;
        public bool sRGB;
        public Color resizeBackground; // 缩放时 letterbox 填充色
    }

    private KindSettings GetKindSettings(TextureKind kind)
    {
        switch (kind)
        {
            case TextureKind.Normal:
                // 法线图：使用 NormalMap 类型，不做 sRGB
                return new KindSettings
                {
                    textureType = TextureImporterType.NormalMap,
                    sRGB = false,
                    resizeBackground = new Color(0.5f, 0.5f, 1f, 1f)
                };

            case TextureKind.Albedo:
                // 颜色图：唯一需要 sRGB 的类型
                return new KindSettings
                {
                    textureType = TextureImporterType.Default,
                    sRGB = true,
                    resizeBackground = Color.black
                };

            // Height / MetallicSmoothness / Occlusion 都是数据图：
            // 必须关闭 sRGB，否则 Gamma 校正会破坏 PBR 计算。
            default:
                return new KindSettings
                {
                    textureType = TextureImporterType.Default,
                    sRGB = false,
                    resizeBackground = Color.black
                };
        }
    }

    [MenuItem("Tools/创建贴图数组")]
    public static void ShowWindow()
    {
        GetWindow<TextureArrayGeneratorEditor>("Texture Array Generator");
    }

    private void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        GUILayout.Label("Texture2DArray Generator", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "1. 选择贴图类型（不同类型使用不同的导入设置）\n" +
            "2. 拖入贴图\n" +
            "3. 设置目标尺寸（所有贴图会统一缩放到此尺寸）\n" +
            "4. 点击 Generate，自动创建 .texture2darray 资产",
            MessageType.Info);

        EditorGUILayout.Space();

        // 设置
        GUILayout.Label("Settings", EditorStyles.boldLabel);
        targetSize = EditorGUILayout.IntSlider("Target Size", targetSize, 64, 4096);
        EditorGUILayout.BeginHorizontal();
        outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
        if (GUILayout.Button("Browse", GUILayout.Width(60)))
        {
            string folder = EditorUtility.OpenFolderPanel("Select Output Folder", "Assets", "");
            if (!string.IsNullOrEmpty(folder))
            {
                outputFolder = "Assets" + folder.Substring(Application.dataPath.Length);
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // 贴图类型选择
        GUILayout.Label("Texture Type", EditorStyles.boldLabel);
        selectedKind = (TextureKind)EditorGUILayout.EnumPopup("Type", selectedKind);

        EditorGUILayout.HelpBox(GetKindDescription(selectedKind), MessageType.None);

        // 当前类型的数组文件名
        string currentName = arrayNames[selectedKind];
        string newName = EditorGUILayout.TextField("Array Name", currentName);
        if (newName != currentName)
            arrayNames[selectedKind] = newName;

        EditorGUILayout.Space();

        // 单一导入区域
        GUILayout.Label($"{selectedKind} Textures:", EditorStyles.boldLabel);
        Rect dropArea = GUILayoutUtility.GetRect(0, 40, GUILayout.ExpandWidth(true));
        GUI.Box(dropArea, $"Drag & Drop {selectedKind} Textures Here");
        HandleDragDrop(dropArea, textures);

        EditorGUILayout.Space(4);

        for (int i = 0; i < textures.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"[{i}]", GUILayout.Width(25));
            textures[i] = (Texture2D)EditorGUILayout.ObjectField(textures[i], typeof(Texture2D), false, GUILayout.Height(32));
            if (GUILayout.Button("X", GUILayout.Width(24), GUILayout.Height(32)))
            {
                textures.RemoveAt(i);
                i--;
            }
            EditorGUILayout.EndHorizontal();
        }

        if (textures.Count > 0)
        {
            EditorGUILayout.Space(4);
            if (GUILayout.Button("Clear List"))
                textures.Clear();
        }

        EditorGUILayout.Space(10);

        // 生成按钮
        GUI.enabled = textures.Count > 0;
        if (GUILayout.Button($"Generate {selectedKind} Texture2DArray", GUILayout.Height(30)))
        {
            Generate();
        }
        GUI.enabled = true;

        EditorGUILayout.EndScrollView();
    }

    private string GetKindDescription(TextureKind kind)
    {
        switch (kind)
        {
            case TextureKind.Albedo:
                return "颜色图 (Diffuse/Color)。开启 sRGB。";
            case TextureKind.Normal:
                return "法线图。textureType = NormalMap，关闭 sRGB。";
            case TextureKind.Height:
                return "高度图。数据图，关闭 sRGB（存储的是数据而非颜色）。";
            case TextureKind.MetallicSmoothness:
                return "金属度/光滑度。数据图，关闭 sRGB，否则 Gamma 校正会破坏 PBR 计算。";
            case TextureKind.Occlusion:
                return "环境光遮蔽。数据图，关闭 sRGB，否则 Gamma 校正会破坏 PBR 计算。";
            default:
                return "";
        }
    }

    private void HandleDragDrop(Rect dropArea, List<Texture2D> targetList)
    {
        Event evt = Event.current;
        if (evt.type == EventType.DragUpdated && dropArea.Contains(evt.mousePosition))
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            evt.Use();
        }
        else if (evt.type == EventType.DragPerform && dropArea.Contains(evt.mousePosition))
        {
            DragAndDrop.AcceptDrag();
            foreach (Object obj in DragAndDrop.objectReferences)
            {
                if (obj is Texture2D tex && !targetList.Contains(tex))
                {
                    targetList.Add(tex);
                }
            }
            evt.Use();
        }
    }

    private void Generate()
    {
        // 确保输出目录存在
        if (!Directory.Exists(outputFolder))
        {
            Directory.CreateDirectory(outputFolder);
        }

        string resizedFolder = Path.Combine(outputFolder, "Resized");
        if (!Directory.Exists(resizedFolder))
        {
            Directory.CreateDirectory(resizedFolder);
        }

        AssetDatabase.Refresh();

        if (textures.Count == 0)
        {
            EditorUtility.DisplayDialog("Error", "No textures added.", "OK");
            return;
        }

        // 注意：不要使用 StartAssetEditing/StopAssetEditing 包裹整个流程。
        // 那会把所有 ImportAsset/SaveAndReimport 延迟到批处理结束才执行，
        // 导致缩放后的贴图在 .texture2darray 导入时仍是 Unity 默认压缩格式，
        // 不同贴图压缩成不同格式 (BC1/BC7) 触发 FormatMismatch，最终整张数组变粉色。
        int layers = 0;
        try
        {
            string fileName = SanitizeFileName(arrayNames[selectedKind], selectedKind + "Maps");
            string arrayPath = Path.Combine(outputFolder, fileName + ".texture2darray").Replace("\\", "/");
            layers = GenerateTextureArray(textures, arrayPath, resizedFolder, selectedKind);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.Refresh();
        }

        EditorUtility.DisplayDialog("Success",
            $"{selectedKind} Texture2DArray generated in:\n{outputFolder}\n\nLayers: {layers}", "OK");
    }

    private string SanitizeFileName(string name, string fallback)
    {
        if (string.IsNullOrWhiteSpace(name))
            return fallback;

        name = name.Trim();
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');

        // 去掉用户可能手动输入的扩展名
        if (name.EndsWith(".texture2darray", System.StringComparison.OrdinalIgnoreCase))
            name = name.Substring(0, name.Length - ".texture2darray".Length);

        return string.IsNullOrWhiteSpace(name) ? fallback : name;
    }

    private int GenerateTextureArray(List<Texture2D> sources, string arrayAssetPath, string resizedFolder, TextureKind kind)
    {
        string prefix = kind.ToString();

        // 第一步：将所有贴图统一缩放到 targetSize x targetSize，保存为磁盘资产
        List<Texture2D> resizedAssets = new List<Texture2D>();

        for (int i = 0; i < sources.Count; i++)
        {
            Texture2D source = sources[i];
            if (source == null)
            {
                Debug.LogWarning($"[TextureArrayGenerator] {prefix}[{i}] is null, skipping");
                continue;
            }

            string sourcePath = AssetDatabase.GetAssetPath(source);
            string sourceName = Path.GetFileNameWithoutExtension(sourcePath);
            string resizedPath = Path.Combine(resizedFolder, $"{prefix}_{i}_{sourceName}.png").Replace("\\", "/");

            // 确保源贴图可读
            MakeReadable(sourcePath);

            // 统一走缩放/重采样流程，保证所有切片尺寸与像素格式一致
            resizedAssets.Add(ResizeAndSave(source, resizedPath, kind));

            EditorUtility.DisplayProgressBar("Generating", $"{prefix} [{i}/{sources.Count}]", (float)i / sources.Count);
        }

        EditorUtility.ClearProgressBar();

        if (resizedAssets.Count == 0)
        {
            Debug.LogError($"[TextureArrayGenerator] No valid {prefix} textures to process");
            return 0;
        }

        // 第二步：确保所有缩放后的贴图导入设置一致
        foreach (var tex in resizedAssets)
        {
            string path = AssetDatabase.GetAssetPath(tex);
            ConfigureImportSettings(path, kind);
        }

        AssetDatabase.Refresh();

        // 重新加载，确保拿到最新的资产引用
        for (int i = 0; i < resizedAssets.Count; i++)
        {
            string path = AssetDatabase.GetAssetPath(resizedAssets[i]);
            resizedAssets[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // 第三步：创建 .texture2darray 文件并配置 importer
        CreateTexture2DArrayAsset(resizedAssets, arrayAssetPath);

        Debug.Log($"[TextureArrayGenerator] {prefix} array created: {arrayAssetPath}, layers: {resizedAssets.Count}");
        return resizedAssets.Count;
    }

    private void MakeReadable(string assetPath)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null && !importer.isReadable)
        {
            importer.isReadable = true;
            importer.SaveAndReimport();
        }
    }

    private void ConfigureImportSettings(string assetPath, TextureKind kind)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) return;

        var settings = GetKindSettings(kind);

        importer.textureType = settings.textureType;
        // 数据图 (Height / MetallicSmoothness / Occlusion) 必须关闭 sRGB，
        // 否则 Gamma 校正会破坏 PBR 计算；只有 Albedo 颜色图开启 sRGB。
        importer.sRGBTexture = settings.sRGB;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.isReadable = true;
        importer.mipmapEnabled = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;

        // 强制所有切片使用完全一致的格式 (RGBA32)。
        // 否则 Unity 会根据每张图是否含 alpha 自动选择 RGB24 / RGBA32，
        // 切片格式不一致会触发 Texture2DArrayImporter 的 FormatMismatch -> 粉色。
        var platformSettings = importer.GetDefaultPlatformTextureSettings();
        platformSettings.overridden = true;
        platformSettings.format = TextureImporterFormat.RGBA32;
        platformSettings.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SetPlatformTextureSettings(platformSettings);

        importer.SaveAndReimport();
    }

    private Texture2D ResizeAndSave(Texture2D source, string outputPath, TextureKind kind)
    {
        var settings = GetKindSettings(kind);

        // 数据图按线性空间重采样，避免 Gamma 校正改变像素数值；颜色图按 sRGB。
        var readWrite = settings.sRGB ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear;

        // 使用 RenderTexture 缩放
        RenderTexture rt = RenderTexture.GetTemporary(targetSize, targetSize, 0, RenderTextureFormat.ARGB32, readWrite);
        rt.filterMode = FilterMode.Bilinear;

        Graphics.SetRenderTarget(rt);
        GL.Clear(true, true, settings.resizeBackground);

        GL.PushMatrix();
        GL.LoadPixelMatrix(0, targetSize, 0, targetSize);

        // 保持宽高比居中
        float srcAspect = (float)source.width / source.height;
        float scale;
        Vector2 offset = Vector2.zero;
        if (srcAspect > 1f)
        {
            scale = (float)targetSize / source.width;
            offset.y = (targetSize - source.height * scale) * 0.5f;
        }
        else
        {
            scale = (float)targetSize / source.height;
            offset.x = (targetSize - source.width * scale) * 0.5f;
        }

        Graphics.DrawTexture(new Rect(offset.x, offset.y, source.width * scale, source.height * scale), source);
        GL.PopMatrix();

        RenderTexture.active = rt;
        Texture2D result = new Texture2D(targetSize, targetSize, TextureFormat.RGBA32, true);
        result.ReadPixels(new Rect(0, 0, targetSize, targetSize), 0, 0);
        result.Apply();

        // 保存为 PNG
        byte[] pngData = result.EncodeToPNG();
        File.WriteAllBytes(outputPath, pngData);

        RenderTexture.ReleaseTemporary(rt);
        RenderTexture.active = null;
        DestroyImmediate(result);

        AssetDatabase.ImportAsset(outputPath);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(outputPath);
    }

    private void CreateTexture2DArrayAsset(List<Texture2D> slices, string assetPath)
    {
        // 创建 .texture2darray 文件
        if (!File.Exists(assetPath))
        {
            string content = "Texture2DArray asset";
            File.WriteAllText(assetPath, content);
            AssetDatabase.ImportAsset(assetPath);
        }

        // 获取 importer 并配置
        var importer = AssetImporter.GetAtPath(assetPath) as Texture2DArrayImporter;
        if (importer == null)
        {
            Debug.LogError($"[TextureArrayGenerator] Failed to get Texture2DArrayImporter at {assetPath}");
            return;
        }

        importer.textures = slices.ToArray();
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.anisoLevel = 1;
        importer.isReadable = false;

        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
    }
}
