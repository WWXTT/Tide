using System;
using System.IO;
using UnityEngine;

namespace DanbaidongShaderConverter
{
    public static class ShaderConversionEngine
    {
        private const string URP_PACKAGE = "com.unity.render-pipelines.universal";
        private const string DBD_PACKAGE = "com.unity.render-pipelines.danbaidong";

        public static ConversionResult ConvertFile(ShaderFileInfo fileInfo, bool createBackup)
        {
            var result = new ConversionResult
            {
                FilePath = fileInfo.FilePath,
                Category = fileInfo.Category,
                Timestamp = DateTime.Now,
            };

            try
            {
                string content = File.ReadAllText(fileInfo.FilePath);

                // Skip if already converted
                if (content.Contains(DBD_PACKAGE) && !content.Contains(URP_PACKAGE))
                {
                    result.WasConverted = false;
                    result.AddDetail("File already uses DanbaidongRP paths, skipped.");
                    return result;
                }

                // Count changes before conversion
                int changeCount = CountOccurrences(content, URP_PACKAGE);

                if (changeCount == 0)
                {
                    result.WasConverted = false;
                    result.AddDetail("No URP package references found, skipped.");
                    return result;
                }

                // Create backup
                if (createBackup)
                {
                    result.BackupPath = CreateBackup(fileInfo.FilePath);
                    result.AddDetail($"Backup created: {result.BackupPath}");
                }

                // Perform conversion
                string converted = ConvertURPPaths(content);

                // Write converted file
                File.WriteAllText(fileInfo.FilePath, converted);

                result.WasConverted = true;
                result.ChangesCount = changeCount;
                result.AddDetail($"Replaced {changeCount} occurrence(s) of '{URP_PACKAGE}' with '{DBD_PACKAGE}'");
            }
            catch (Exception ex)
            {
                result.HasError = true;
                result.AddDetail($"Error: {ex.Message}");
            }

            return result;
        }

        public static string ConvertURPPaths(string content)
        {
            return content.Replace(URP_PACKAGE, DBD_PACKAGE);
        }

        public static string CreateBackup(string filePath)
        {
            string backupPath = filePath + ".backup";

            // If backup already exists, append timestamp
            if (File.Exists(backupPath))
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                backupPath = filePath + $".backup_{timestamp}";
            }

            File.Copy(filePath, backupPath);
            return backupPath;
        }

        public static bool RestoreBackup(string filePath)
        {
            string backupPath = filePath + ".backup";
            if (!File.Exists(backupPath))
            {
                // Try to find timestamped backup
                string dir = Path.GetDirectoryName(filePath);
                string name = Path.GetFileNameWithoutExtension(filePath);
                string ext = Path.GetExtension(filePath);
                string pattern = name + ext + ".backup*";
                var backups = Directory.GetFiles(dir, pattern);
                if (backups.Length > 0)
                {
                    Array.Sort(backups);
                    backupPath = backups[backups.Length - 1]; // Use latest backup
                }
                else
                {
                    return false;
                }
            }

            File.Copy(backupPath, filePath, true);
            return true;
        }

        private static int CountOccurrences(string text, string search)
        {
            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(search, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += search.Length;
            }
            return count;
        }
    }

    public class ConversionResult
    {
        public string FilePath;
        public ShaderCategory Category;
        public bool WasConverted;
        public bool HasError;
        public string BackupPath;
        public int ChangesCount;
        public System.DateTime Timestamp;
        public string Details;

        public void AddDetail(string detail)
        {
            if (string.IsNullOrEmpty(Details))
                Details = detail;
            else
                Details += "\n" + detail;
        }
    }
}
