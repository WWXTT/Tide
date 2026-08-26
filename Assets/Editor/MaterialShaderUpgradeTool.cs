using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class MaterialShaderUpgradeTool : EditorWindow
{
    // 源 Shader 和目标 Shader（直接拖入）
    private Shader sourceShader;
    private Shader targetShader;

    // 纹理映射列表
    private List<TextureMapping> textureMappings = new List<TextureMapping>();

    // 缓存源/目标 Shader 的纹理属性名列表（用于下拉菜单）
    private string[] sourceTexProperties = new string[0];
    private string[] targetTexProperties = new string[0];

    [System.Serializable]
    private class TextureMapping
    {
        public bool enabled = true;
        public string sourceProperty;
        public string targetProperty;
    }

    [MenuItem("Tools/填写映射关系后自动更换材质")]
    static void Init()
    {
        MaterialShaderUpgradeTool window = (MaterialShaderUpgradeTool)GetWindow(typeof(MaterialShaderUpgradeTool));
        window.titleContent = new GUIContent("材质升级");
        window.Show();
    }

    void OnGUI()
    {
        GUILayout.Label("材质 Shader 批量升级工具", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // 选择源 Shader 和目标 Shader
        sourceShader = (Shader)EditorGUILayout.ObjectField("源 Shader（替换前）", sourceShader, typeof(Shader), false);
        targetShader = (Shader)EditorGUILayout.ObjectField("目标 Shader（替换后）", targetShader, typeof(Shader), false);

        // 当 Shader 变更时更新属性列表
        if (GUI.changed)
        {
            UpdatePropertyLists();
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "1. 拖入源和目标 Shader。\n2. 配置纹理映射（可自动匹配同名属性）。\n3. 在 Project 窗口中选中需要升级的材质，点击下方按钮。\n只会处理 Shader 为源 Shader 的材质。",
            MessageType.Info
        );

        EditorGUILayout.Space();

        // 映射编辑区域
        if (sourceShader != null && targetShader != null)
        {
            DrawMappingUI();
        }
        else
        {
            EditorGUILayout.HelpBox("请先设置源 Shader 和目标 Shader。", MessageType.Warning);
        }

        EditorGUILayout.Space();

        // 升级按钮
        GUI.enabled = (sourceShader != null && targetShader != null);
        if (GUILayout.Button("升级选中的材质"))
        {
            UpgradeSelectedMaterials();
        }
        GUI.enabled = true;
    }

    /// <summary>
    /// 更新源/目标 Shader 的纹理属性名列表
    /// </summary>
    void UpdatePropertyLists()
    {
        sourceTexProperties = GetTexPropertyNames(sourceShader);
        targetTexProperties = GetTexPropertyNames(targetShader);
    }

    /// <summary>
    /// 获取 Shader 中所有 TexEnv 类型的属性名
    /// </summary>
    string[] GetTexPropertyNames(Shader shader)
    {
        if (shader == null)
            return new string[0];

        List<string> names = new List<string>();
        int count = ShaderUtil.GetPropertyCount(shader);
        for (int i = 0; i < count; i++)
        {
            if (ShaderUtil.GetPropertyType(shader, i) == ShaderUtil.ShaderPropertyType.TexEnv)
            {
                names.Add(ShaderUtil.GetPropertyName(shader, i));
            }
        }
        return names.Distinct().ToArray();
    }

    /// <summary>
    /// 绘制映射配置界面
    /// </summary>
    void DrawMappingUI()
    {
        GUILayout.Label("纹理属性映射", EditorStyles.boldLabel);

        // 快捷操作按钮
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("自动匹配同名属性", GUILayout.Width(150)))
        {
            AutoMatch();
        }
        if (GUILayout.Button("清空列表", GUILayout.Width(100)))
        {
            textureMappings.Clear();
        }
        if (GUILayout.Button("添加新映射", GUILayout.Width(100)))
        {
            textureMappings.Add(new TextureMapping { enabled = true });
        }
        EditorGUILayout.EndHorizontal();

        // 映射列表
        if (textureMappings.Count == 0)
        {
            EditorGUILayout.HelpBox("暂无映射规则，请使用上方按钮添加或自动匹配。", MessageType.None);
        }

        for (int i = 0; i < textureMappings.Count; i++)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            // 启用复选框
            textureMappings[i].enabled = EditorGUILayout.Toggle(textureMappings[i].enabled, GUILayout.Width(20));

            // 源属性下拉
            int srcIndex = Mathf.Max(0, System.Array.IndexOf(sourceTexProperties, textureMappings[i].sourceProperty));
            srcIndex = EditorGUILayout.Popup(srcIndex, sourceTexProperties, GUILayout.Width(150));
            textureMappings[i].sourceProperty = (srcIndex < sourceTexProperties.Length) ? sourceTexProperties[srcIndex] : "";

            GUILayout.Label("→", GUILayout.Width(20));

            // 目标属性下拉
            int tgtIndex = Mathf.Max(0, System.Array.IndexOf(targetTexProperties, textureMappings[i].targetProperty));
            tgtIndex = EditorGUILayout.Popup(tgtIndex, targetTexProperties, GUILayout.Width(150));
            textureMappings[i].targetProperty = (tgtIndex < targetTexProperties.Length) ? targetTexProperties[tgtIndex] : "";

            // 删除按钮
            if (GUILayout.Button("✕", GUILayout.Width(30)))
            {
                textureMappings.RemoveAt(i);
                GUI.FocusControl(null);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                break; // 避免迭代中修改集合造成问题，直接跳出重绘
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }
    }

    /// <summary>
    /// 自动添加同名纹理映射（若目标 Shader 中存在同名属性）
    /// </summary>
    void AutoMatch()
    {
        if (sourceShader == null || targetShader == null)
            return;

        foreach (string srcProp in sourceTexProperties)
        {
            if (targetTexProperties.Contains(srcProp))
            {
                // 避免重复添加
                if (!textureMappings.Exists(m => m.sourceProperty == srcProp))
                {
                    textureMappings.Add(new TextureMapping
                    {
                        enabled = true,
                        sourceProperty = srcProp,
                        targetProperty = srcProp
                    });
                }
            }
        }
        Repaint();
    }

    /// <summary>
    /// 执行材质升级
    /// </summary>
    void UpgradeSelectedMaterials()
    {
        Object[] selectedObjects = Selection.objects;
        if (selectedObjects.Length == 0)
        {
            Debug.LogWarning("请先在 Project 窗口中选择至少一个材质球。");
            return;
        }

        if (sourceShader == null || targetShader == null)
        {
            Debug.LogError("源 Shader 或目标 Shader 未设置！");
            return;
        }

        // 只收集本次需要启用的映射
        var activeMappings = textureMappings.Where(m => m.enabled).ToList();
        if (activeMappings.Count == 0)
        {
            Debug.LogWarning("没有启用任何纹理映射，仅更换 Shader。");
        }

        int upgradedCount = 0;
        foreach (Object obj in selectedObjects)
        {
            Material material = obj as Material;
            if (material == null)
                continue;

            // 仅处理 Shader 为源 Shader 的材质
            if (material.shader != sourceShader)
                continue;

            Undo.RecordObject(material, "Upgrade Material Shader");

            // 1. 在替换 Shader 前，保存需要转移的纹理
            Dictionary<string, Texture> savedTextures = new Dictionary<string, Texture>();
            foreach (var mapping in activeMappings)
            {
                if (material.HasProperty(mapping.sourceProperty))
                {
                    Texture tex = material.GetTexture(mapping.sourceProperty);
                    if (tex != null)
                    {
                        savedTextures[mapping.sourceProperty] = tex;
                    }
                }
            }

            // 2. 替换 Shader
            material.shader = targetShader;

            // 3. 将纹理写回目标属性
            foreach (var mapping in activeMappings)
            {
                if (savedTextures.TryGetValue(mapping.sourceProperty, out Texture tex))
                {
                    if (material.HasProperty(mapping.targetProperty))
                    {
                        material.SetTexture(mapping.targetProperty, tex);
                    }
                }
            }

            EditorUtility.SetDirty(material);
            upgradedCount++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"升级完成！共处理了 {upgradedCount} 个材质球。");
    }
}