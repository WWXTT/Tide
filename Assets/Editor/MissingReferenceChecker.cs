using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public class MissingReferenceChecker : EditorWindow
{
    private List<MarkedUsageInfo> markedUsages = new List<MarkedUsageInfo>();
    private Vector2 scrollPos;
    private string searchFilter = "";

    private List<MarkedEntry> markedEntries = new List<MarkedEntry>();
    private string[] entryDisplayNames = new[] { "-- 选择标记类型 --" };
    private int selectedEntryIndex = 0;
    private bool showOnlyMissing = false;

    private readonly string[] ignoreAssemblies = { "Unity", "System", "Mono", "netstandard", "mscorlib" };

    // ========== 枚举 & 数据类 ==========

    private enum MarkedUsageType { MarkedClass, MarkedField }

    private class MarkedUsageInfo
    {
        public MarkedUsageType usageType;
        public GameObject gameObject;
        public string scriptName;
        public string fieldName;
        public bool isAssigned;
        public string valueDisplay;
    }

    private class MarkedEntry
    {
        public Type ClassType;
        public FieldInfo FieldInfo;
        public MonoScript Script;
        public string ScriptPath;
        public int LineNumber;
        public string DisplayName;
    }

    // ========== 入口 ==========

    [MenuItem("Tools/属性引用检查器")]
    public static void ShowWindow()
    {
        GetWindow<MissingReferenceChecker>("引用检查器");
    }

    // ========== 主界面 ==========

    private void OnGUI()
    {
        DrawMainUI();
    }

    private void DrawMainUI()
    {
        GUILayout.Label("引用检测 — 标记类/字段使用情况", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("选择特定的标记类或字段搜索引用，或扫描全部空引用", MessageType.Info);
        GUILayout.Space(10);

        // ---- 1. 标记下拉列表 & 跳转按钮 ----
        using (new EditorGUILayout.HorizontalScope())
        {
            int newIndex = EditorGUILayout.Popup("选择标记:", selectedEntryIndex, entryDisplayNames);
            if (newIndex != selectedEntryIndex)
            {
                selectedEntryIndex = newIndex;
                markedUsages.Clear();
            }

            if (GUILayout.Button("刷新列表", GUILayout.Width(70)))
            {
                RefreshMarkedEntries();
            }

            if (selectedEntryIndex > 0)
            {
                var entry = markedEntries[selectedEntryIndex - 1];
                GUIContent scriptBtnContent = entry.FieldInfo != null
                    ? new GUIContent($"打开 {entry.FieldInfo.Name}", "跳转到字段定义行")
                    : new GUIContent($"打开 {entry.ClassType.Name}", "跳转到类定义行");

                if (GUILayout.Button(scriptBtnContent, GUILayout.Width(120)))
                {
                    OpenScriptAtLine(entry);
                }
            }
        }

        GUILayout.Space(5);

        // ---- 2. 搜索按钮 ----
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("搜索选中项引用", GUILayout.Height(30)))
            {
                if (selectedEntryIndex > 0)
                {
                    ScanMarkedUsagesForEntry();
                }
                else
                {
                    Debug.LogWarning("请先从下拉列表中选择一个标记项。");
                }
            }

            if (GUILayout.Button("扫描全部空引用", GUILayout.Height(30)))
            {
                ScanAllMissingRefs();
            }
        }

        GUILayout.Space(5);

        // ---- 3. 切换按钮 ----
        string filterLabel = showOnlyMissing ? "当前: 仅空引用 (点击切换)" : "当前: 显示全部 (点击切换)";
        if (GUILayout.Button(filterLabel, GUILayout.Height(25)))
        {
            showOnlyMissing = !showOnlyMissing;
        }

        GUILayout.Space(5);

        // ---- 4. 结果列表 ----
        var filtered = GetFilteredUsages();

        if (filtered.Count > 0)
        {
            int assignedCount = filtered.Count(u => u.isAssigned);
            int missingCount = filtered.Count(u => !u.isAssigned);

            GUILayout.Label(
                $"引用总数: {filtered.Count}  |  已赋值: {assignedCount}  |  空引用: {missingCount}",
                EditorStyles.helpBox);
            GUILayout.Space(5);

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            string lastGo = "";

            foreach (var info in filtered)
            {
                if (info.gameObject.name != lastGo)
                {
                    lastGo = info.gameObject.name;
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField($"● {info.gameObject.name}", EditorStyles.boldLabel);
                }

                DrawMarkedUsageItem(info);
            }

            EditorGUILayout.EndScrollView();
        }
        else if (markedUsages.Count > 0)
        {
            EditorGUILayout.HelpBox("当前筛选条件下无匹配结果。", MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox("请从上方下拉列表中选择一个标记项搜索，或点击扫描全部空引用。", MessageType.Info);
        }
    }

    private List<MarkedUsageInfo> GetFilteredUsages()
    {
        var result = new List<MarkedUsageInfo>();
        foreach (var info in markedUsages)
        {
            if (showOnlyMissing && info.isAssigned) continue;

            if (!string.IsNullOrEmpty(searchFilter))
            {
                string lower = searchFilter.ToLower();
                if (!info.gameObject.name.ToLower().Contains(lower) &&
                    !info.scriptName.ToLower().Contains(lower) &&
                    !info.fieldName.ToLower().Contains(lower) &&
                    !info.valueDisplay.ToLower().Contains(lower))
                    continue;
            }

            result.Add(info);
        }
        return result;
    }

    private void DrawMarkedUsageItem(MarkedUsageInfo info)
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
        {
            if (GUILayout.Button("定位", GUILayout.Width(50)))
            {
                Selection.activeGameObject = info.gameObject;
                EditorGUIUtility.PingObject(info.gameObject);
                SceneView.FrameLastActiveSceneView();
            }

            GUILayout.Label($"  路径: {info.scriptName}.{info.fieldName}", GUILayout.Width(300));

            if (info.isAssigned)
            {
                var style = new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(0.2f, 0.7f, 0.2f) } };
                GUILayout.Label($"✓ {info.valueDisplay}", style);
            }
            else
            {
                var style = new GUIStyle(EditorStyles.label) { normal = { textColor = Color.red } };
                GUILayout.Label("✗ 缺失引用", style);
            }
        }
    }

    // ---- 核心逻辑：下拉数据刷新 ----

    private void RefreshMarkedEntries()
    {
        markedEntries.Clear();

        var allScripts = AssetDatabase.FindAssets("t:MonoScript")
             .Select(g => AssetDatabase.GUIDToAssetPath(g))
             .Where(p => p.EndsWith(".cs"))
             .Select(p => (Script: AssetDatabase.LoadAssetAtPath<MonoScript>(p), Path: p))
             .Where(s => s.Script != null)
             .ToList();

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (IsSystemAssembly(asm)) continue;

            try
            {
                foreach (var type in asm.GetTypes())
                {
                    if (!type.IsClass || type.IsAbstract) continue;

                    bool isClassRequired = type.GetCustomAttribute<RequiredAttribute>() != null;
                    bool isSerializable = type.IsSerializable || Attribute.IsDefined(type, typeof(SerializableAttribute));

                    if (isClassRequired && isSerializable)
                    {
                        var scriptInfo = FindScriptForType(allScripts, type);
                        int line = FindTypeLine(scriptInfo.Path, type.Name);

                        markedEntries.Add(new MarkedEntry
                        {
                            ClassType = type,
                            FieldInfo = null,
                            Script = scriptInfo.Script,
                            ScriptPath = scriptInfo.Path,
                            LineNumber = line,
                            DisplayName = $"[类] {type.FullName}"
                        });
                    }

                    if (isSerializable)
                    {
                        var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        foreach (var field in fields)
                        {
                            bool isPublic = field.IsPublic;
                            bool hasSerializeField = field.GetCustomAttribute<SerializeField>() != null;
                            if (!isPublic && !hasSerializeField) continue;

                            bool isFieldRequired = field.GetCustomAttribute<RequiredAttribute>() != null;
                            if (isFieldRequired)
                            {
                                var scriptInfo = FindScriptForType(allScripts, type);
                                int line = FindFieldLine(scriptInfo.Path, field.Name);

                                markedEntries.Add(new MarkedEntry
                                {
                                    ClassType = type,
                                    FieldInfo = field,
                                    Script = scriptInfo.Script,
                                    ScriptPath = scriptInfo.Path,
                                    LineNumber = line,
                                    DisplayName = $"[字段] {type.Name}.{field.Name}"
                                });
                            }
                        }
                    }
                }
            }
            catch { /* 忽略反射异常 */ }
        }

        entryDisplayNames = new[] { "-- 选择标记类型 --" }
            .Concat(markedEntries.Select(e => e.DisplayName))
            .ToArray();
    }

    // ---- 核心逻辑：针对特定选择项搜索场景引用 ----

    private void ScanMarkedUsagesForEntry()
    {
        markedUsages.Clear();
        if (selectedEntryIndex <= 0) return;

        var entry = markedEntries[selectedEntryIndex - 1];
        ScanEntryInternal(entry);

        SortAndLogResult();
    }

    // ---- 核心逻辑：扫描全部空引用 ----

    private void ScanAllMissingRefs()
    {
        markedUsages.Clear();
        showOnlyMissing = true; // 默认只显示空引用

        if (markedEntries.Count == 0)
        {
            RefreshMarkedEntries();
        }

        foreach (var entry in markedEntries)
        {
            ScanEntryInternal(entry);
        }

        SortAndLogResult();
    }

    private void ScanEntryInternal(MarkedEntry entry)
    {
        var allMonoBehaviours = FindObjectsOfType<MonoBehaviour>(true);

        foreach (var mb in allMonoBehaviours)
        {
            if (mb == null) continue;
            var type = mb.GetType();
            if (IsSystemType(type)) continue;

            if (entry.FieldInfo != null)
            {
                // 字段可能存在于嵌套的可序列化类中，需遍历所有MonoBehaviour的字段树
                SearchSpecificField(mb, mb.gameObject, type.Name, "", entry.FieldInfo);
            }
            else
            {
                SearchSpecificClass(mb, mb.gameObject, type.Name, "", entry.ClassType);
            }
        }
    }

    private void SortAndLogResult()
    {
        markedUsages.Sort((a, b) =>
        {
            int c = string.Compare(a.gameObject.name, b.gameObject.name, StringComparison.Ordinal);
            if (c != 0) return c;
            c = string.Compare(a.scriptName, b.scriptName, StringComparison.Ordinal);
            if (c != 0) return c;
            return string.Compare(a.fieldName, b.fieldName, StringComparison.Ordinal);
        });

        int missingCount = markedUsages.Count(u => !u.isAssigned);
        Debug.Log($"[引用检测] 搜索完成，共找到 {markedUsages.Count} 处引用，其中 {missingCount} 处空引用。");
    }

    /// <summary>
    /// 深度搜索特定字段的赋值情况
    /// </summary>
    private void SearchSpecificField(object obj, GameObject rootGo, string scriptName, string pathPrefix, FieldInfo targetField)
    {
        if (obj == null) return;

        var type = obj.GetType();
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        foreach (var field in fields)
        {
            bool isPublic = field.IsPublic;
            bool hasSerializeField = field.GetCustomAttribute<SerializeField>() != null;
            if (!isPublic && !hasSerializeField) continue;

            string fieldPath = string.IsNullOrEmpty(pathPrefix) ? field.Name : $"{pathPrefix}.{field.Name}";
            var fieldValue = field.GetValue(obj);
            var fieldType = field.FieldType;

            if (field == targetField)
            {
                bool isNull = fieldValue == null || (fieldValue is UnityEngine.Object uObj && uObj == null);
                string display = isNull ? "null" : (fieldValue is UnityEngine.Object uo ? uo.name : fieldValue.ToString());

                markedUsages.Add(new MarkedUsageInfo
                {
                    usageType = MarkedUsageType.MarkedField,
                    gameObject = rootGo,
                    scriptName = scriptName,
                    fieldName = fieldPath,
                    isAssigned = !isNull,
                    valueDisplay = display
                });
            }
            else
            {
                if (fieldValue is IList list)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (list[i] != null && list[i].GetType().IsClass)
                        {
                            if (list[i] is UnityEngine.Object) continue;

                            var itemType = list[i].GetType();
                            bool isSerializable = itemType.IsSerializable || Attribute.IsDefined(itemType, typeof(SerializableAttribute));
                            if (isSerializable)
                            {
                                SearchSpecificField(list[i], rootGo, scriptName, $"{fieldPath}[{i}]", targetField);
                            }
                        }
                    }
                }
                else if (fieldType.IsClass && !fieldType.IsArray && fieldType != typeof(string) && fieldValue != null)
                {
                    if (fieldValue is UnityEngine.Object) continue;

                    bool isSerializable = fieldType.IsSerializable || Attribute.IsDefined(fieldType, typeof(SerializableAttribute));
                    if (isSerializable)
                    {
                        SearchSpecificField(fieldValue, rootGo, scriptName, fieldPath, targetField);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 深度搜索特定类的引用情况
    /// </summary>
    private void SearchSpecificClass(object obj, GameObject rootGo, string scriptName, string pathPrefix, Type targetClassType)
    {
        if (obj == null) return;

        var type = obj.GetType();
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        foreach (var field in fields)
        {
            bool isPublic = field.IsPublic;
            bool hasSerializeField = field.GetCustomAttribute<SerializeField>() != null;
            if (!isPublic && !hasSerializeField) continue;

            string fieldPath = string.IsNullOrEmpty(pathPrefix) ? field.Name : $"{pathPrefix}.{field.Name}";
            var fieldValue = field.GetValue(obj);
            var fieldType = field.FieldType;

            if (targetClassType.IsAssignableFrom(fieldType))
            {
                bool isNull = fieldValue == null || (fieldValue is UnityEngine.Object uObj && uObj == null);
                string display = isNull ? "null" : (fieldValue is UnityEngine.Object uo ? uo.name : fieldValue.ToString());

                markedUsages.Add(new MarkedUsageInfo
                {
                    usageType = MarkedUsageType.MarkedClass,
                    gameObject = rootGo,
                    scriptName = scriptName,
                    fieldName = fieldPath,
                    isAssigned = !isNull,
                    valueDisplay = display
                });
            }

            if (fieldValue is IList list)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] != null && list[i].GetType().IsClass)
                    {
                        if (list[i] is UnityEngine.Object) continue;

                        var itemType = list[i].GetType();
                        bool isSerializable = itemType.IsSerializable || Attribute.IsDefined(itemType, typeof(SerializableAttribute));
                        if (isSerializable)
                        {
                            SearchSpecificClass(list[i], rootGo, scriptName, $"{fieldPath}[{i}]", targetClassType);
                        }
                    }
                }
            }
            else if (fieldType.IsClass && !fieldType.IsArray && fieldType != typeof(string) && fieldValue != null)
            {
                if (fieldValue is UnityEngine.Object) continue;

                bool isSerializable = fieldType.IsSerializable || Attribute.IsDefined(fieldType, typeof(SerializableAttribute));
                if (isSerializable)
                {
                    SearchSpecificClass(fieldValue, rootGo, scriptName, fieldPath, targetClassType);
                }
            }
        }
    }

    // ---- 脚本跳转辅助方法 ----

    private void OpenScriptAtLine(MarkedEntry entry)
    {
        if (entry.Script != null)
        {
            AssetDatabase.OpenAsset(entry.Script, Math.Max(1, entry.LineNumber));
        }
        else if (!string.IsNullOrEmpty(entry.ScriptPath))
        {
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(entry.ScriptPath);
            if (asset != null) AssetDatabase.OpenAsset(asset, Math.Max(1, entry.LineNumber));
        }
        else
        {
            Debug.LogWarning("无法定位对应脚本文件，请确保脚本在项目中且无编译错误。");
        }
    }

    private (MonoScript Script, string Path) FindScriptForType(List<(MonoScript Script, string Path)> allScripts, Type type)
    {
        var match = allScripts.FirstOrDefault(s => s.Script != null && s.Script.GetClass() == type);
        if (match.Script != null) return match;

        foreach (var s in allScripts)
        {
            if (File.Exists(s.Path) && File.ReadAllText(s.Path).Contains($"class {type.Name}"))
            {
                return (s.Script, s.Path);
            }
        }
        return (null, "");
    }

    private int FindTypeLine(string path, string className)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return 0;
        string[] lines = File.ReadAllLines(path);
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains($"class {className}"))
                return i + 1;
        }
        return 0;
    }

    private int FindFieldLine(string path, string fieldName)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return 0;
        string[] lines = File.ReadAllLines(path);
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains(fieldName) && !lines[i].TrimStart().StartsWith("//"))
                return i + 1;
        }
        return 0;
    }

    private bool IsSystemAssembly(Assembly asm)
    {
        string name = asm.FullName;
        foreach (var ignore in ignoreAssemblies)
        {
            if (name.StartsWith(ignore)) return true;
        }
        return false;
    }

    private bool IsSystemType(System.Type type)
    {
        string name = type.Assembly.FullName;
        foreach (var ignore in ignoreAssemblies)
        {
            if (name.StartsWith(ignore)) return true;
        }
        return false;
    }
}
