using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DanbaidongShaderConverter
{
    public class DanbaidongShaderConverterWindow : EditorWindow
    {
        private List<ShaderFileInfo> scannedFiles = new List<ShaderFileInfo>();
        private Dictionary<ShaderCategory, bool> categoryFoldouts = new Dictionary<ShaderCategory, bool>();
        private Vector2 scrollPos;
        private bool createBackup = true;
        private string statusText = "Ready. Click 'Scan Project' to start.";
        private MessageType statusType = MessageType.Info;
        private bool isScanning = false;
        private List<ConversionResult> lastResults = new List<ConversionResult>();

        [MenuItem("Tools/标准urpshader自动替换引用到Danbaidong格式")]
        public static void ShowWindow()
        {
            var window = GetWindow<DanbaidongShaderConverterWindow>("Danbaidong Shader Converter");
            window.minSize = new Vector2(500, 400);
        }

        private Vector2 materialScrollPos;
        private string materialScanDir = "Assets/Synty";

        private void OnGUI()
        {
            DrawHeader();
            DrawScanButton();
            DrawResults();
            DrawActions();
            DrawStatusBar();
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Danbaidong Shader Converter", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Scans the project for shaders that reference the standard URP package and converts them to DanbaidongRP paths.\n\n" +
                "Conversion: com.unity.render-pipelines.universal → com.unity.render-pipelines.danbaidong",
                MessageType.Info);
            EditorGUILayout.Space(5);
        }

        private void DrawScanButton()
        {
            EditorGUI.BeginDisabledGroup(isScanning);
            if (GUILayout.Button("Scan Project", GUILayout.Height(35)))
            {
                ScanProject();
            }
            EditorGUI.EndDisabledGroup();
        }

        private void ScanProject()
        {
            isScanning = true;
            statusText = "Scanning...";
            statusType = MessageType.Info;

            try
            {
                scannedFiles = ShaderScanEngine.ScanAllShaders((msg, progress) =>
                {
                    if (EditorUtility.DisplayCancelableProgressBar("Scanning Shaders", msg, progress))
                        throw new OperationCanceledException();
                });

                // Initialize foldouts
                foreach (ShaderCategory cat in Enum.GetValues(typeof(ShaderCategory)))
                {
                    if (!categoryFoldouts.ContainsKey(cat))
                        categoryFoldouts[cat] = true; // Expanded by default
                }

                int needsConversion = scannedFiles.Count(f => f.NeedsConversion);
                int total = scannedFiles.Count;
                statusText = $"Scan complete. {total} shader files found, {needsConversion} need conversion.";
                statusType = needsConversion > 0 ? MessageType.Warning : MessageType.Info;
            }
            catch (OperationCanceledException)
            {
                statusText = "Scan cancelled.";
                statusType = MessageType.Warning;
            }
            catch (Exception ex)
            {
                statusText = $"Scan error: {ex.Message}";
                statusType = MessageType.Error;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                isScanning = false;
            }
        }

        private void DrawResults()
        {
            if (scannedFiles.Count == 0)
                return;

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField($"Scan Results ({scannedFiles.Count} files)", EditorStyles.boldLabel);

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.ExpandHeight(true));

            var grouped = scannedFiles
                .GroupBy(f => f.Category)
                .OrderBy(g => g.Key);

            foreach (var group in grouped)
            {
                var category = group.Key;
                var files = group.ToList();
                string catName = ShaderScanEngine.GetCategoryDisplayName(category);
                var msgType = ShaderScanEngine.GetCategoryMessageType(category);

                // Category header with foldout
                EditorGUILayout.Space(3);
                bool wasExpanded = categoryFoldouts.GetValueOrDefault(category, true);
                bool isExpanded = EditorGUILayout.Foldout(wasExpanded, $"{catName} ({files.Count})", true, EditorStyles.boldLabel);
                categoryFoldouts[category] = isExpanded;

                if (!isExpanded)
                    continue;

                // Category info box
                string catInfo = GetCategoryInfo(category);
                if (!string.IsNullOrEmpty(catInfo))
                    EditorGUILayout.HelpBox(catInfo, msgType);

                EditorGUI.indentLevel++;

                foreach (var file in files)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        bool wasSelected = file.Selected;
                        file.Selected = EditorGUILayout.ToggleLeft($"  {file.FileName}", file.Selected);

                        if (file.NeedsConversion && file.Selected != wasSelected)
                            Repaint();

                        // Show file path
                        var pathStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };
                        GUILayout.Label(TruncatePath(file.FilePath), pathStyle, GUILayout.MinWidth(100));
                    }

                    // Info text
                    if (!string.IsNullOrEmpty(file.Info))
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.LabelField(file.Info, EditorStyles.miniLabel);
                        EditorGUI.indentLevel--;
                    }
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawActions()
        {
            if (scannedFiles.Count == 0)
                return;

            EditorGUILayout.Space(5);

            // Select/Deselect all convertible
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Select All Convertible"))
                {
                    foreach (var f in scannedFiles)
                        if (f.NeedsConversion) f.Selected = true;
                }
                if (GUILayout.Button("Deselect All"))
                {
                    foreach (var f in scannedFiles)
                        f.Selected = false;
                }
            }

            EditorGUILayout.Space(3);

            createBackup = EditorGUILayout.ToggleLeft("Create backup before conversion (.backup files)", createBackup);

            EditorGUILayout.Space(5);

            int selectedCount = scannedFiles.Count(f => f.Selected && f.NeedsConversion);
            EditorGUI.BeginDisabledGroup(selectedCount == 0);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.backgroundColor = Color.yellow;
                if (GUILayout.Button($"Convert Selected ({selectedCount})", GUILayout.Height(30)))
                {
                    ConvertSelected();
                }
                GUI.backgroundColor = Color.white;

                if (GUILayout.Button("Generate Report", GUILayout.Height(30)))
                {
                    GenerateReport();
                }
            }

            EditorGUI.EndDisabledGroup();
        }

        private void DrawStatusBar()
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox(statusText, statusType);
        }

        private void ConvertSelected()
        {
            var toConvert = scannedFiles.Where(f => f.Selected && f.NeedsConversion).ToList();

            if (toConvert.Count == 0)
            {
                statusText = "No files selected for conversion.";
                statusType = MessageType.Warning;
                return;
            }

            if (!EditorUtility.DisplayDialog(
                "Confirm Conversion",
                $"Convert {toConvert.Count} shader file(s)?\n\n" +
                "This will replace URP package paths with DanbaidongRP paths.\n" +
                (createBackup ? "Backup files will be created." : "WARNING: No backup will be created!"),
                "Convert", "Cancel"))
            {
                return;
            }

            lastResults.Clear();
            int converted = 0;
            int errors = 0;

            try
            {
                for (int i = 0; i < toConvert.Count; i++)
                {
                    var file = toConvert[i];
                    float progress = (float)i / toConvert.Count;
                    if (EditorUtility.DisplayCancelableProgressBar(
                        "Converting Shaders",
                        $"Converting: {file.FileName}",
                        progress))
                        break;

                    var result = ShaderConversionEngine.ConvertFile(file, createBackup);
                    lastResults.Add(result);

                    if (result.HasError) errors++;
                    else if (result.WasConverted)
                    {
                        converted++;
                        file.NeedsConversion = false;
                        file.Category = ShaderCategory.AlreadyConverted;
                        file.Info = "Converted to DanbaidongRP paths";
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.Refresh();
            }

            statusText = $"Conversion complete: {converted} converted, {errors} errors.";
            statusType = errors > 0 ? MessageType.Warning : MessageType.Info;
        }

        private void GenerateReport()
        {
            try
            {
                string reportPath = ShaderConversionReport.GenerateReport(
                    lastResults.Count > 0 ? lastResults : new List<ConversionResult>(),
                    scannedFiles);

                statusText = $"Report generated: {reportPath}";
                statusType = MessageType.Info;

                // Ping the report file in Project window
                var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(reportPath);
                if (obj != null)
                {
                    EditorGUIUtility.PingObject(obj);
                }
            }
            catch (Exception ex)
            {
                statusText = $"Report error: {ex.Message}";
                statusType = MessageType.Error;
            }
        }

        private string GetCategoryInfo(ShaderCategory category)
        {
            return category switch
            {
                ShaderCategory.URPHandCoded => "Standard URP shaders with #include paths. Can be auto-converted by replacing package name.",
                ShaderCategory.BuiltIn => "Legacy built-in pipeline shaders (CGPROGRAM/UnityCG). Cannot be auto-converted — needs full rewrite or use URP replacement.",
                ShaderCategory.ShadergraphWithURPIncludes => "ShaderGraph files with URP package references (likely in Custom Function nodes). Can be auto-converted.",
                ShaderCategory.ShadergraphWithTarget => "ShaderGraph files using UniversalTarget. These resolve via .NET assembly and usually work as-is with DanbaidongRP.",
                ShaderCategory.HLSLWithURP => "HLSL include files referencing URP paths. Can be auto-converted.",
                ShaderCategory.AlreadyConverted => "Files already using DanbaidongRP paths. No action needed.",
                _ => null
            };
        }

        private string TruncatePath(string path)
        {
            const int maxLen = 60;
            if (path.Length <= maxLen) return path;
            return "..." + path.Substring(path.Length - maxLen + 3);
        }
    }
}
