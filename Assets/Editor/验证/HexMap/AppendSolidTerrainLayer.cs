using System.Collections.Generic;
using System.IO;
using System.Linq;
using HexMap;
using UnityEditor;
using UnityEngine;
using Oddworm.EditorFramework;

namespace Tide.验证
{
    /// <summary>
    /// 棋盘纯色地形层装配工具（2026-10-03，幂等可重跑）：
    /// ① 向 AlbedoMaps/NormalMaps 两个 Texture2DArray 各追加一层程序生成的纯色切片
    ///    （albedo=深灰蓝、normal=中性平坦；切片尺寸/导入设置与现有切片完全一致，防 FormatMismatch 粉屏）；
    /// ② 确保 BattleHexMapSettings.asset 存在（从世界地图资产复制：32×32、棋盘矩形 13×8 居中、换噪声种子），
    ///    并把 boardRegion.terrainIndex 钉到新层 index。
    /// 换色：改 AlbedoColor 后重跑（切片 PNG 原位重写，数组层序不变）。
    /// 注：HexCell.mat 的 Height/Metallic/Occlusion 数组本就未绑/悬空（历史状态），无需追加。
    /// 层序表见 HexMapFeatureSettings 类注释：追加后 index=10 为棋盘纯色层。
    /// </summary>
    public static class AppendSolidTerrainLayer
    {
        private const string AlbedoArrayPath = "Assets/Art/Materials/AlbedoMaps.texture2darray";
        private const string NormalArrayPath = "Assets/Art/Materials/NormalMaps.texture2darray";
        private const string LayerFolder = "Assets/Art/HexMap/BoardSolidLayer";
        private const string WorldSettingsPath = "Assets/Art/HexMap/HexMapFeatureSettings.asset";
        private const string BattleSettingsPath = "Assets/Art/HexMap/BattleHexMapSettings.asset";

        // 战斗地图参数（32×32 外围地形 + 13×8 棋盘居中：x 10..22, z 12..19）
        private const int BattleMapSize = 32;
        private const int BoardMinX = 10, BoardMaxX = 22; // 13 列（BoardMath 逻辑棋盘同宽）
        private const int BoardMinZ = 12, BoardMaxZ = 19; // 8 行

        /// <summary>棋盘纯色（默认 UI 底色系深灰蓝 #2A303B；改后重跑即可换色）。</summary>
        private static readonly Color AlbedoColor = new Color32(42, 48, 59, 255);

        [MenuItem("Tools/验证/HexMap/追加纯色地形层（棋盘）")]
        public static void Run()
        {
            int albedoIdx = AppendSolidSlice(AlbedoArrayPath, "Albedo", AlbedoColor, srgb: true, isNormal: false);
            int normalIdx = AppendSolidSlice(NormalArrayPath, "Normal", new Color(0.5f, 0.5f, 1f, 1f), srgb: false, isNormal: true);
            if (albedoIdx < 0 || normalIdx < 0)
            {
                Debug.LogError("[SolidLayer] 切片追加失败（数组资产或 importer 缺失），已中止——战斗资产未改动");
                return;
            }
            if (albedoIdx != normalIdx)
                Debug.LogWarning($"[SolidLayer] 两数组层数不一致（albedo={albedoIdx} normal={normalIdx}）——" +
                                 "以 albedo 层序为准，请检查 NormalMaps 数组是否与 AlbedoMaps 同源装配");

            EnsureBattleSettings(albedoIdx);
        }

        /// <summary>追加（或幂等复用）一个纯色切片，返回层 index；-1=失败。</summary>
        private static int AppendSolidSlice(string arrayPath, string kindName, Color color, bool srgb, bool isNormal)
        {
            var importer = AssetImporter.GetAtPath(arrayPath) as Texture2DArrayImporter;
            if (importer == null)
            {
                Debug.LogError($"[SolidLayer] 找不到 Texture2DArrayImporter：{arrayPath}");
                return -1;
            }

            var slices = importer.textures != null ? importer.textures.ToList() : new List<Texture2D>();
            int size = slices.Count > 0 && slices[0] != null ? slices[0].width : 1024; // 与现有切片同尺寸

            EnsureFolder(LayerFolder);
            string pngPath = $"{LayerFolder}/BoardSolid_{kindName}.png";
            WriteSolidPng(pngPath, size, color);
            ConfigureSliceImport(pngPath, srgb, isNormal);

            var slice = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
            if (slice == null)
            {
                Debug.LogError($"[SolidLayer] 切片生成失败：{pngPath}");
                return -1;
            }

            // 幂等：同路径切片已在数组中 → 原位复用（换色重写 PNG 即生效），只返回其 index
            int existing = -1;
            for (int i = 0; i < slices.Count; i++)
            {
                if (slices[i] != null && AssetDatabase.GetAssetPath(slices[i]) == pngPath)
                {
                    existing = i;
                    break;
                }
            }
            if (existing >= 0)
            {
                Debug.Log($"[SolidLayer] {kindName} 纯色切片已在层 {existing}（幂等复用）");
                return existing;
            }

            slices.Add(slice);
            importer.textures = slices.ToArray();
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();

            int index = slices.Count - 1;
            Debug.Log($"[SolidLayer] {kindName} 数组已追加纯色层 index={index}（共 {slices.Count} 层，{size}×{size}）");
            return index;
        }

        /// <summary>纯色 PNG 生成（RGBA32，无 mip——导入设置统一由 ConfigureSliceImport 收口）。</summary>
        private static void WriteSolidPng(string path, int size, Color color)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            var c32 = color;
            for (int i = 0; i < px.Length; i++) px[i] = c32;
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
        }

        /// <summary>切片导入设置——与 TextureArrayGeneratorEditor.GenerateTextureArray 完全同口径
        /// （RGBA32 未压缩 + npot 不缩放 + mip + 可读；albedo sRGB / 法线 NormalMap+翻绿通道），
        /// 任何一项不一致都会触发 importer FormatMismatch → 整数组粉屏。</summary>
        private static void ConfigureSliceImport(string path, bool srgb, bool isNormal)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            importer.textureType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = srgb;
            if (isNormal) importer.flipGreenChannel = true; // 中性法线 0.5 翻转不变，保持口径一致
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.isReadable = true;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            var platform = importer.GetDefaultPlatformTextureSettings();
            platform.overridden = true;
            platform.format = TextureImporterFormat.RGBA32;
            platform.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SetPlatformTextureSettings(platform);

            importer.SaveAndReimport();
        }

        /// <summary>确保战斗地图设置资产存在（从世界资产复制改造），并把棋盘层 index 钉进 boardRegion。</summary>
        private static void EnsureBattleSettings(int terrainIndex)
        {
            var settings = AssetDatabase.LoadAssetAtPath<HexMapFeatureSettings>(BattleSettingsPath);
            if (settings == null)
            {
                if (!AssetDatabase.CopyAsset(WorldSettingsPath, BattleSettingsPath))
                {
                    Debug.LogError($"[SolidLayer] 复制战斗资产失败：{WorldSettingsPath} → {BattleSettingsPath}");
                    return;
                }
                settings = AssetDatabase.LoadAssetAtPath<HexMapFeatureSettings>(BattleSettingsPath);

                // 战场口径：32×32 外围地形 + 换噪声种子 + 清 POI（棋盘局不摆世界编辑内容）
                settings.cellCountX = BattleMapSize;
                settings.cellCountZ = BattleMapSize;
                settings.noiseSeed = 20261003;
                settings.pois.Clear();
                Debug.Log($"[SolidLayer] 已创建战斗地图资产：{BattleSettingsPath}（{BattleMapSize}×{BattleMapSize}）");
            }

            settings.boardRegion = new HexBoardRegion
            {
                enabled = true,
                minX = BoardMinX, minZ = BoardMinZ,
                maxX = BoardMaxX, maxZ = BoardMaxZ,
                terrainIndex = terrainIndex,
            };
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            var arr = AssetDatabase.LoadAssetAtPath<Texture2DArray>(AlbedoArrayPath);
            Debug.Log($"[SolidLayer] 棋盘纯色层就绪 index={terrainIndex}（数组实层数 {arr?.depth ?? -1}）；" +
                      $"boardRegion=({BoardMinX},{BoardMinZ})..({BoardMaxX},{BoardMaxZ}) in {settings.cellCountX}×{settings.cellCountZ}");
        }

        private static void EnsureFolder(string folder)
        {
            if (Directory.Exists(folder)) return;
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
        }
    }
}
