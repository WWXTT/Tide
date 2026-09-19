using System.Collections.Generic;
using System.IO;
using System.Linq;
using HexMap;
using Oddworm.EditorFramework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// HexMap 美术资产批处理初始化（计划 0.2 / 8.3 / 8.4）：
/// 1. 贴图数组扩层：备份现有 2 层数组后，按 TerrainIndex 层序表追加 10 个新层
///    （Albedo/Normal 同序对齐，缺法线的层用 Dirt_nrm 代）
/// 2. 树木/石头/草材质（URP Lit = DanbaidongRP 同 GUID 顶替）+ HexWater/HexRoad 材质
/// 3. HexMapFeatureSettings 资产（Assets/Art/HexMap/HexMapFeatureSettings.asset，
///    全工程唯一 HexMap 配置）：补缺材质引用/散布规则 + 沙层河床(riverBedTerrainIndex=7)
/// 幂等：重复运行安全（备份只在首次做；资产上已有的引用/规则/参数一律不覆盖，只补空缺）。
/// 批处理入口：Unity -batchmode -executeMethod HexMapBatchSetup.RunBatchSetup
/// （-executeMethod 不支持命名空间 → 本类放全局命名空间）
/// </summary>
public static class HexMapBatchSetup
{
    private const string GenFolder = "Assets/Art/HexMap";
    private const string ResizedFolder = GenFolder + "/Resized";
    private const string ArtRoot = "Assets/Art/HexMap";
    private const string MatFolder = ArtRoot + "/Materials";
    private const int TargetSize = 1024;

    /// <summary>
    /// 新增层（层序 = 现有层数 2 起连续追加），与 HexMapFeatureSettings.cs 头注释的
    /// TerrainIndex 层序表一致：2=GrassGreen 3=GrassYellow 4=Dirt 5=CliffDark 6=Gravel
    /// 7=Sand 8=SandCracks 9=Snow 10=CliffBright 11=CliffRed（CliffPink 留备 12）
    /// </summary>
    private static readonly (string Albedo, string Normal)[] NewLayers =
    {
        ("GrassGreen.tif", "GrassGreen_nrm.tif"),
        ("GrassYellow.tif", null),            // 无专用法线 → Dirt_nrm 代
        ("Dirt.tif", "Dirt_nrm.tif"),
        ("CliffDark.tif", "CliffDark_nrm.tif"),
        ("Gravel.tif", "Gravel_nrm.tif"),
        ("Sand.tif", "Sand_nrm.tif"),
        ("SandCracks.tif", "SandCracks_nrm.tif"),
        ("Snow.tif", "Snow_nrm.tif"),
        ("CliffBright.tif", "CliffBright_nrm.tif"),
        ("CliffRed.tif", null),               // Dirt_nrm 代
    };

    [MenuItem("Tools/HexMap/批处理初始化美术资产")]
    public static void RunFromMenu() => RunAll(exitEditor: false);

    public static void RunBatchSetup() => RunAll(exitEditor: true);

    private static void RunAll(bool exitEditor)
    {
        int failures = 0;
        try
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            failures += BuildTextureArrays();
            failures += CreateWaterAndRoadMaterials();
            failures += CreateVegetationMaterials();
            failures += CreateSettingsAsset();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[HexMapBatchSetup] 全部完成，失败步骤 {failures}");
            if (exitEditor)
                EditorApplication.Exit(failures == 0 ? 0 : 1);
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            if (exitEditor)
                EditorApplication.Exit(2);
        }
    }

    // ── 1. 贴图数组扩层 ─────────────────────────────────────────

    private static int BuildTextureArrays()
    {
        string albedoPath = GenFolder + "/AlbedoMaps.texture2darray";
        string normalPath = GenFolder + "/NormalMaps.texture2darray";

        BackupOnce(albedoPath);
        BackupOnce(normalPath);

        var albedoImporter = AssetImporter.GetAtPath(albedoPath) as Texture2DArrayImporter;
        var normalImporter = AssetImporter.GetAtPath(normalPath) as Texture2DArrayImporter;
        if (albedoImporter == null || normalImporter == null)
        {
            Debug.LogError($"[HexMapBatchSetup] 找不到数组 importer：albedo={albedoImporter != null} normal={normalImporter != null}");
            return 1;
        }

        var albedoSlices = albedoImporter.textures.ToList();
        var normalSlices = normalImporter.textures.ToList();
        if (albedoSlices.Count != normalSlices.Count)
        {
            Debug.LogError($"[HexMapBatchSetup] 现有数组层数不齐 albedo={albedoSlices.Count} normal={normalSlices.Count}");
            return 1;
        }
        int oldCount = albedoSlices.Count;

        foreach (var (albedo, normal) in NewLayers)
        {
            albedoSlices.Add(ResizeToPng($"{ArtRoot}/LandTextures/{albedo}", isNormal: false));
            normalSlices.Add(ResizeToPng($"{ArtRoot}/LandTextures/{normal ?? "Dirt_nrm.tif"}", isNormal: true));
        }

        albedoImporter.textures = albedoSlices.ToArray();
        EditorUtility.SetDirty(albedoImporter);
        albedoImporter.SaveAndReimport();

        normalImporter.textures = normalSlices.ToArray();
        EditorUtility.SetDirty(normalImporter);
        normalImporter.SaveAndReimport();

        Debug.Log($"[HexMapBatchSetup] 数组扩层完成：旧 {oldCount} 层保留 + 新 {NewLayers.Length} 层" +
                  $"（共 {oldCount + NewLayers.Length} 层，TerrainIndex 层序表见 HexMapFeatureSettings.cs 头注释）");
        return 0;
    }

    /// <summary>备份（连 .meta，GUID 不变）；只在备份不存在时做（幂等）</summary>
    private static void BackupOnce(string assetPath)
    {
        string backup = assetPath.Replace(".texture2darray", "_Backup2Layer.texture2darray");
        if (File.Exists(backup))
            return;
        File.Copy(assetPath, backup);
        if (File.Exists(assetPath + ".meta"))
            File.Copy(assetPath + ".meta", backup + ".meta");
        Debug.Log($"[HexMapBatchSetup] 已备份 {Path.GetFileName(assetPath)} → {Path.GetFileName(backup)}");
    }

    /// <summary>
    /// 缩放到 1024² PNG（letterbox 保宽高比，与 TextureArrayGeneratorEditor 同管线），
    /// 统一 RGBA32/Uncompressed/isReadable/mipmap；法线 NormalMap 类型 + 绿反 + 线性。
    /// </summary>
    private static Texture2D ResizeToPng(string sourcePath, bool isNormal)
    {
        var srcImporter = AssetImporter.GetAtPath(sourcePath) as TextureImporter;
        if (srcImporter == null)
        {
            Debug.LogError($"[HexMapBatchSetup] 源贴图缺失 {sourcePath}");
            return null;
        }
        if (!srcImporter.isReadable)
        {
            srcImporter.isReadable = true;
            srcImporter.SaveAndReimport();
        }

        var source = AssetDatabase.LoadAssetAtPath<Texture2D>(sourcePath);
        if (source == null)
        {
            Debug.LogError($"[HexMapBatchSetup] 贴图加载失败 {sourcePath}");
            return null;
        }

        // GPU 缩放（RenderTexture + GL，batchmode 无 -nographics 时可用）
        var readWrite = isNormal ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB;
        var background = isNormal ? new Color(0.5f, 0.5f, 1f, 1f) : Color.black;

        RenderTexture rt = RenderTexture.GetTemporary(TargetSize, TargetSize, 0,
            RenderTextureFormat.ARGB32, readWrite);
        rt.filterMode = FilterMode.Bilinear;
        Graphics.SetRenderTarget(rt);
        GL.Clear(true, true, background);
        GL.PushMatrix();
        GL.LoadPixelMatrix(0, TargetSize, 0, TargetSize);

        float srcAspect = (float)source.width / source.height;
        float scale;
        Vector2 offset = Vector2.zero;
        if (srcAspect > 1f)
        {
            scale = (float)TargetSize / source.width;
            offset.y = (TargetSize - source.height * scale) * 0.5f;
        }
        else
        {
            scale = (float)TargetSize / source.height;
            offset.x = (TargetSize - source.width * scale) * 0.5f;
        }
        Graphics.DrawTexture(new Rect(offset.x, offset.y, source.width * scale, source.height * scale), source);
        GL.PopMatrix();

        RenderTexture.active = rt;
        var result = new Texture2D(TargetSize, TargetSize, TextureFormat.RGBA32, true);
        result.ReadPixels(new Rect(0, 0, TargetSize, TargetSize), 0, 0);
        result.Apply();
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);

        string name = Path.GetFileNameWithoutExtension(sourcePath);
        string pngPath = $"{ResizedFolder}/HexMap_{(isNormal ? "Normal" : "Albedo")}_{name}.png";
        File.WriteAllBytes(pngPath, result.EncodeToPNG());
        Object.DestroyImmediate(result);
        AssetDatabase.ImportAsset(pngPath);

        // 导入设置与 TextureArrayGeneratorEditor.ConfigureImportSettings 逐项一致
        //（切片格式不一致会触发 FormatMismatch → 整数组粉屏）
        var pngImporter = AssetImporter.GetAtPath(pngPath) as TextureImporter;
        pngImporter.textureType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        pngImporter.sRGBTexture = !isNormal;
        if (isNormal)
            pngImporter.flipGreenChannel = true;
        pngImporter.npotScale = TextureImporterNPOTScale.None;
        pngImporter.isReadable = true;
        pngImporter.mipmapEnabled = true;
        pngImporter.textureCompression = TextureImporterCompression.Uncompressed;
        var plat = pngImporter.GetDefaultPlatformTextureSettings();
        plat.overridden = true;
        plat.format = TextureImporterFormat.RGBA32;
        plat.textureCompression = TextureImporterCompression.Uncompressed;
        pngImporter.SetPlatformTextureSettings(plat);
        pngImporter.SaveAndReimport();

        var loaded = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
        Debug.Log($"[HexMapBatchSetup] 切片 {Path.GetFileName(pngPath)}（源 {source.width}×{source.height}）");
        return loaded;
    }

    // ── 2. 材质 ─────────────────────────────────────────────────

    private static int CreateWaterAndRoadMaterials()
    {
        int failures = 0;

        var waterShader = Shader.Find("HexMap/Water");
        if (waterShader != null)
        {
            var water = LoadOrCreateMaterial($"{MatFolder}/HexWater.mat", waterShader);
            if (water.shader != waterShader)
                water.shader = waterShader;
            EditorUtility.SetDirty(water);
        }
        else
        {
            Debug.LogError("[HexMapBatchSetup] HexMap/Water shader 未找到（导入失败？）——HexWater.mat 跳过");
            failures++;
        }

        var lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null)
        {
            Debug.LogError("[HexMapBatchSetup] URP Lit(DanbaidongRP) 未找到——HexRoad.mat 跳过");
            return failures + 1;
        }
        var road = LoadOrCreateMaterial($"{MatFolder}/HexRoad.mat", lit);
        road.SetColor("_BaseColor", new Color32(96, 92, 86, 255));
        road.SetFloat("_Smoothness", 0.08f);
        road.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(road);
        return failures;
    }

    private static int CreateVegetationMaterials()
    {
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null)
        {
            Debug.LogError("[HexMapBatchSetup] URP Lit 未找到——植被材质跳过");
            return 1;
        }

        int failures = 0;
        failures += CreateLit("Trees/Birch/Birch.tif", "Trees/Birch/Birch_n.tif", "Materials/Birch_Lit.mat", lit, alphaClip: false);
        failures += CreateLit("Trees/Pine/Pine.tif", "Trees/Pine/Pine_n.tif", "Materials/Pine_Lit.mat", lit, alphaClip: false);
        failures += CreateLit("Stones/Stones.tif", "Stones/Stones_n.tif", "Materials/Stone_Lit.mat", lit, alphaClip: false);
        failures += CreateLit("Grass/Sedge.tif", null, "Materials/Grass_Card.mat", lit, alphaClip: true);
        return failures;
    }

    private static int CreateLit(string albedoRel, string normalRel, string matRel, Shader lit, bool alphaClip)
    {
        var albedo = AssetDatabase.LoadAssetAtPath<Texture>($"{ArtRoot}/{albedoRel}");
        if (albedo == null)
        {
            Debug.LogError($"[HexMapBatchSetup] 反照率缺失 {albedoRel}");
            return 1;
        }

        var mat = LoadOrCreateMaterial($"{ArtRoot}/{matRel}", lit);
        mat.SetTexture("_BaseMap", albedo);
        if (normalRel != null)
        {
            var nrm = AssetDatabase.LoadAssetAtPath<Texture>($"{ArtRoot}/{normalRel}");
            if (nrm != null)
                mat.SetTexture("_BumpMap", nrm);
        }
        if (alphaClip)
        {
            mat.SetFloat("_AlphaClip", 1f);
            mat.SetFloat("_Cutoff", 0.5f);
            mat.SetFloat("_Cull", 0f);             // 草片双面
            mat.renderQueue = (int)RenderQueue.AlphaTest;
        }
        EditorUtility.SetDirty(mat);
        Debug.Log($"[HexMapBatchSetup] 材质 {matRel}（alphaClip={alphaClip}）");
        return 0;
    }

    /// <summary>Material 无公共无参构造，不能走 new() 约束的泛型</summary>
    private static Material LoadOrCreateMaterial(string path, Shader shader)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
            return existing;
        var created = new Material(shader);
        AssetDatabase.CreateAsset(created, path);
        return created;
    }

    // ── 3. Settings 资产 ────────────────────────────────────────

    private static int CreateSettingsAsset()
    {
        string path = $"{ArtRoot}/HexMapFeatureSettings.asset";
        var s = AssetDatabase.LoadAssetAtPath<HexMapFeatureSettings>(path);
        bool created = s == null;
        if (created)
            s = ScriptableObject.CreateInstance<HexMapFeatureSettings>();

        // 只补空缺，不覆盖已有值——本资产是全工程唯一 HexMap 配置（地形参数/POI/散布规则/噪声接线）
        if (s.terrainMaterial == null)
            s.terrainMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{ArtRoot}/HexCell.mat");
        if (s.waterMaterial == null)
            s.waterMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{MatFolder}/HexWater.mat");
        if (s.roadMaterial == null)
            s.roadMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{MatFolder}/HexRoad.mat");

        if (s.scatterRules == null || s.scatterRules.Count == 0)
        {
            s.scatterRules = new List<HexScatterRule>
            {
                Rule("桦树", "Trees/Birch/TallSingleA.FBX", "Materials/Birch_Lit.mat",
                    new[] { 0f, 0f, 1.1f, 0.7f, 0, 0, 0, 0, 0, 0, 0, 0 }, 0, 6),
                Rule("松树", "Trees/Pine/PineBig1.FBX", "Materials/Pine_Lit.mat",
                    new[] { 0f, 0f, 0.5f, 0.9f, 0, 0, 0, 0, 0, 0.2f, 0, 0 }, 2, 9),
                Rule("石头", "Stones/Stone02.fbx", "Materials/Stone_Lit.mat",
                    new[] { 0f, 0f, 0.12f, 0f, 0.2f, 0.35f, 0, 0, 0, 0.15f, 0.3f, 0 }, 0, 10),
                Rule("草", "Grass/Sedge.FBX", "Materials/Grass_Card.mat",
                    new[] { 0f, 0f, 2.5f, 2.0f, 0, 0, 0, 0, 0, 0, 0, 0 }, 0, 7),
            };
        }

        var rivers = s.rivers;
        if (rivers.riverBedTerrainIndex < 0)
            rivers.riverBedTerrainIndex = 7;   // 沙层河床（层序表 7=Sand）
        s.rivers = rivers;

        if (created)
            AssetDatabase.CreateAsset(s, path);
        EditorUtility.SetDirty(s);

        // 噪声图四路提醒（NoiseMapGenerator 生成后手动接线到资产）
        if (s.heightNoise == null || s.mountainNoise == null || s.detailNoise == null || s.curlNoise == null)
            Debug.LogWarning("[HexMapBatchSetup] 噪声图未接齐（height=Perlin / mountain=Ridged / detail=Worley / curl=Curl），" +
                             "缺失路采样退化为中性值 0.5");

        // 树尺寸核对（计划 6.3：树高 8-20 单位 vs cell OR=10，不匹配则调 scaleRange）
        foreach (var r in s.scatterRules)
        {
            if (r.mesh != null)
                Debug.Log($"[HexMapBatchSetup] 规则「{r.name}」mesh 高度 {r.mesh.bounds.size.y:F1}" +
                          $"（scaleRange {r.scaleRange.x:F1}~{r.scaleRange.y:F1}）");
        }
        Debug.Log($"[HexMapBatchSetup] Settings 资产 {(created ? "创建" : "补缺")}：{path}，" +
                  $"terrainMat={(s.terrainMaterial != null)} waterMat={(s.waterMaterial != null)} roadMat={(s.roadMaterial != null)}");
        return 0;
    }

    private static HexScatterRule Rule(string name, string fbxRel, string matRel,
        float[] densityPerTerrain, int elevMin, int elevMax)
    {
        return new HexScatterRule
        {
            name = name,
            mesh = MainMeshOf($"{ArtRoot}/{fbxRel}"),
            material = AssetDatabase.LoadAssetAtPath<Material>($"{ArtRoot}/{matRel}"),
            densityPerTerrain = new List<float>(densityPerTerrain),
            scaleRange = new Vector2(0.8f, 1.3f),
            yOffset = 0f,
            randomYRotation = true,
            elevationMin = elevMin,
            elevationMax = elevMax,
            maxNeighborElevationDiff = 2,
            riverMarginCells = 1,
            roadMarginCells = 1,
        };
    }

    /// <summary>FBX 主网格 = 包围盒最大的子 Mesh（FBX 直接作 Mesh 引用，不用 prefab）</summary>
    private static Mesh MainMeshOf(string fbxPath)
    {
        var meshes = AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<Mesh>().ToList();
        if (meshes.Count == 0)
        {
            Debug.LogError($"[HexMapBatchSetup] FBX 无网格 {fbxPath}");
            return null;
        }
        meshes.Sort((a, b) => b.bounds.size.sqrMagnitude.CompareTo(a.bounds.size.sqrMagnitude));
        return meshes[0];
    }
}
