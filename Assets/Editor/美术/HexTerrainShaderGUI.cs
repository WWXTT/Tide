using UnityEditor;
using UnityEngine;

/// <summary>
/// HexTerrain 材质 GUI：按可选纹理数组是否绑定自动开关对应 keyword。
///
/// shader 声明 CustomEditor "HexTerrainShaderGUI"（本类，须与该字符串同名同命名空间）。
/// 未绑定数组时若 keyword 仍开启，片元会采样空纹理槽——结果是未定义的显存垃圾
/// （彩色噪点/随视角闪烁），所以每次绘制 Inspector 都同步一次绑定状态。
/// 在 Project 窗口选中材质（或改绑纹理后）即自动生效。
/// </summary>
public class HexTerrainShaderGUI : ShaderGUI
{
    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        base.OnGUI(materialEditor, properties);

        foreach (var obj in materialEditor.targets)
        {
            if (obj is Material mat)
                SyncKeywords(mat);
        }
    }

    private static void SyncKeywords(Material mat)
    {
        SetKeyword(mat, "_TERRAIN_NORMAL_MAP", "_TerrainNormalArray");
        SetKeyword(mat, "_TERRAIN_HEIGHT_MAP", "_TerrainHeightArray");
        SetKeyword(mat, "_TERRAIN_MS_MAP", "_TerrainMetallicSmoothnessArray");
        SetKeyword(mat, "_TERRAIN_OCCLUSION_MAP", "_TerrainOcclusionArray");
    }

    private static void SetKeyword(Material mat, string keyword, string textureProperty)
    {
        if (!mat.HasProperty(textureProperty))
            return;

        bool bound = mat.GetTexture(textureProperty) != null;
        if (mat.IsKeywordEnabled(keyword) == bound)
            return;

        if (bound)
            mat.EnableKeyword(keyword);
        else
            mat.DisableKeyword(keyword);
        EditorUtility.SetDirty(mat);
    }
}
