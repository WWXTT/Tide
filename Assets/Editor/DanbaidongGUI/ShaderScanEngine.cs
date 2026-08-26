using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DanbaidongShaderConverter
{
    public enum ShaderCategory
    {
        URPHandCoded,
        BuiltIn,
        ShadergraphWithURPIncludes,
        ShadergraphWithTarget,
        HLSLWithURP,
        AlreadyConverted,
        DanbaidongNative,
        Unknown
    }

    public class ShaderFileInfo
    {
        public string FilePath;
        public string FileName;
        public ShaderCategory Category;
        public bool NeedsConversion;
        public bool Selected = true;
        public long FileSize;
        public string Info;
    }

    public static class ShaderScanEngine
    {
        private static readonly string[] ShaderExtensions = { ".shader", ".shadergraph", ".hlsl", ".cginc", ".h" };
        private static readonly string AssetsRoot = "Assets";

        public static List<ShaderFileInfo> ScanAllShaders(Action<string, float> onProgress = null)
        {
            var results = new List<ShaderFileInfo>();
            var allFiles = new List<string>();

            foreach (var ext in ShaderExtensions)
            {
                allFiles.AddRange(Directory.GetFiles(AssetsRoot, "*" + ext, SearchOption.AllDirectories));
            }

            float total = allFiles.Count;
            for (int i = 0; i < allFiles.Count; i++)
            {
                var file = allFiles[i].Replace('\\', '/');
                onProgress?.Invoke($"Scanning: {Path.GetFileName(file)}", i / total);

                var info = ClassifyFile(file);
                if (info != null)
                    results.Add(info);
            }

            onProgress?.Invoke("Scan complete", 1f);
            return results;
        }

        private static ShaderFileInfo ClassifyFile(string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLower();
            var fileName = Path.GetFileName(filePath);

            // Skip DanbaidongRP's own files
            if (filePath.Contains("Packages/Dbdrp/") || filePath.Contains("Packages\\Dbdrp\\"))
                return null;

            // Skip files in Library/Temp
            if (filePath.StartsWith("Library/") || filePath.StartsWith("Temp/"))
                return null;

            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists)
                return null;

            var info = new ShaderFileInfo
            {
                FilePath = filePath,
                FileName = fileName,
                FileSize = fileInfo.Length,
            };

            // Read file content for analysis
            string content;
            try
            {
                content = File.ReadAllText(filePath);
            }
            catch
            {
                return null;
            }

            // Skip binary files
            if (content.IndexOf('\0') >= 0 && content.IndexOf('\0') < 256)
                return null;

            bool hasUniversalPath = content.Contains("com.unity.render-pipelines.universal");
            bool hasDanbaidongPath = content.Contains("com.unity.render-pipelines.danbaidong");
            bool hasCGProgram = content.Contains("CGPROGRAM");
            bool hasUnityCG = content.Contains("UnityCG.cginc");
            bool hasUniversalTarget = content.Contains("UnityEditor.Rendering.Universal.ShaderGraph.UniversalTarget");

            switch (ext)
            {
                case ".shader":
                    if (hasDanbaidongPath)
                    {
                        info.Category = ShaderCategory.AlreadyConverted;
                        info.NeedsConversion = false;
                        info.Info = "Already using DanbaidongRP paths";
                    }
                    else if (hasUniversalPath)
                    {
                        info.Category = ShaderCategory.URPHandCoded;
                        info.NeedsConversion = true;
                        info.Info = "Uses standard URP package paths";
                    }
                    else if (hasCGProgram || hasUnityCG)
                    {
                        info.Category = ShaderCategory.BuiltIn;
                        info.NeedsConversion = false;
                        info.Info = "Built-in pipeline (CGPROGRAM) - cannot auto-convert, needs rewrite";
                    }
                    else
                    {
                        info.Category = ShaderCategory.Unknown;
                        info.NeedsConversion = false;
                        info.Info = "Could not determine pipeline";
                    }
                    break;

                case ".shadergraph":
                    if (hasUniversalPath)
                    {
                        info.Category = ShaderCategory.ShadergraphWithURPIncludes;
                        info.NeedsConversion = true;
                        info.Info = "Contains URP package path references (likely in Custom Function nodes)";
                    }
                    else if (hasUniversalTarget)
                    {
                        info.Category = ShaderCategory.ShadergraphWithTarget;
                        info.NeedsConversion = false;
                        info.Info = "Uses UniversalTarget (resolves via .NET assembly, usually works as-is)";
                    }
                    else
                    {
                        info.Category = ShaderCategory.ShadergraphWithTarget;
                        info.NeedsConversion = false;
                        info.Info = "Generic shadergraph";
                    }
                    break;

                case ".hlsl":
                case ".cginc":
                case ".h":
                    if (hasDanbaidongPath)
                    {
                        info.Category = ShaderCategory.AlreadyConverted;
                        info.NeedsConversion = false;
                        info.Info = "Already using DanbaidongRP paths";
                    }
                    else if (hasUniversalPath)
                    {
                        info.Category = ShaderCategory.HLSLWithURP;
                        info.NeedsConversion = true;
                        info.Info = "Uses standard URP package paths";
                    }
                    else
                    {
                        // Likely a standalone include file without pipeline references
                        return null;
                    }
                    break;

                default:
                    return null;
            }

            return info;
        }

        public static string GetCategoryDisplayName(ShaderCategory category)
        {
            return category switch
            {
                ShaderCategory.URPHandCoded => "URP Hand-coded Shaders",
                ShaderCategory.BuiltIn => "Built-in Pipeline (Cannot Auto-Convert)",
                ShaderCategory.ShadergraphWithURPIncludes => "ShaderGraph with URP Includes",
                ShaderCategory.ShadergraphWithTarget => "ShaderGraph with UniversalTarget",
                ShaderCategory.HLSLWithURP => "HLSL Includes with URP Paths",
                ShaderCategory.AlreadyConverted => "Already Converted",
                ShaderCategory.DanbaidongNative => "DanbaidongRP Native",
                ShaderCategory.Unknown => "Unknown",
                _ => category.ToString()
            };
        }

        public static MessageType GetCategoryMessageType(ShaderCategory category)
        {
            return category switch
            {
                ShaderCategory.URPHandCoded => MessageType.Warning,
                ShaderCategory.BuiltIn => MessageType.Error,
                ShaderCategory.ShadergraphWithURPIncludes => MessageType.Warning,
                ShaderCategory.ShadergraphWithTarget => MessageType.Info,
                ShaderCategory.HLSLWithURP => MessageType.Warning,
                ShaderCategory.AlreadyConverted => MessageType.Info,
                _ => MessageType.None
            };
        }
    }
}
