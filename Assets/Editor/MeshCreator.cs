using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 从mesh组件上直接复制修改参数，节省找美术拆分合并的时间
/// </summary>
public class MeshCreator : MonoBehaviour
{
    [MenuItem("Tools/从mesh组件上复制网格")]
    public static void CreateMesh()
    {
        // 获取选中的游戏对象
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            Debug.LogError("No game object selected!");
            return;
        }

        // 尝试获取MeshFilter组件
        MeshFilter meshFilter = selected.GetComponent<MeshFilter>();
        Mesh originalMesh = null;

        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            originalMesh = meshFilter.sharedMesh;
        }
        else
        {
            // 尝试获取SkinnedMeshRenderer组件
            SkinnedMeshRenderer skinnedRenderer = selected.GetComponent<SkinnedMeshRenderer>();
            if (skinnedRenderer != null && skinnedRenderer.sharedMesh != null)
            {
                originalMesh = skinnedRenderer.sharedMesh;
            }
        }

        if (originalMesh == null)
        {
            Debug.LogError("Selected object has no valid mesh!");
            return;
        }

        // 创建网格副本并重置原点
        Mesh newMesh = CreateCenteredMeshCopy(originalMesh);
        newMesh.name = $"{selected.name}_CenteredMesh";

        // 保存网格资产
        SaveMeshAsset(newMesh);
    }

    [MenuItem("Tools/多选游戏物体合并网格")]
    public static void MergeMeshes()
    {
        GameObject[] selectedObjects = Selection.gameObjects;
        if (selectedObjects.Length < 2)
        {
            Debug.LogError("Please select at least 2 objects to merge!");
            return;
        }

        List<Mesh> meshes = new List<Mesh>();
        List<Matrix4x4> transforms = new List<Matrix4x4>();
        List<Material[]> materialsList = new List<Material[]>(); // 重命名避免冲突
        List<Renderer> renderers = new List<Renderer>();

        foreach (GameObject obj in selectedObjects)
        {
            MeshFilter meshFilter = obj.GetComponent<MeshFilter>();
            SkinnedMeshRenderer skinnedRenderer = obj.GetComponent<SkinnedMeshRenderer>();
            MeshRenderer meshRenderer = obj.GetComponent<MeshRenderer>();

            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                meshes.Add(meshFilter.sharedMesh);
                transforms.Add(obj.transform.localToWorldMatrix);
                materialsList.Add(meshRenderer.sharedMaterials);
                renderers.Add(meshRenderer);
            }
            else if (skinnedRenderer != null && skinnedRenderer.sharedMesh != null)
            {
                meshes.Add(skinnedRenderer.sharedMesh);
                transforms.Add(obj.transform.localToWorldMatrix);
                materialsList.Add(skinnedRenderer.sharedMaterials);
                renderers.Add(skinnedRenderer);
            }
        }

        if (meshes.Count < 2)
        {
            Debug.LogError("Less than 2 valid meshes found!");
            return;
        }

        Mesh mergedMesh = MergeMeshes(meshes, transforms, materialsList);
        mergedMesh.name = "MergedMesh";

        GameObject mergedObject = new GameObject("MergedObject");
        MeshFilter mergedFilter = mergedObject.AddComponent<MeshFilter>();
        mergedFilter.sharedMesh = mergedMesh;

        MeshRenderer mergedRenderer = mergedObject.AddComponent<MeshRenderer>();

        mergedRenderer.sharedMaterials = materialsList.SelectMany(m => m).ToArray();

        SaveMeshAsset(mergedMesh);
        EditorUtility.FocusProjectWindow();
        Selection.activeGameObject = mergedObject;
    }

    [MenuItem("Tools/合并材质（去重并列）")]
    public static void MergeMaterial()
    {
        GameObject[] selectedObjects = Selection.gameObjects;
        if (selectedObjects.Length == 0) return;

        MeshFilter meshFilter = selectedObjects[0].GetComponent<MeshFilter>();
        if (meshFilter == null) return;

        // 使用 sharedMesh 避免创建临时实例
        Mesh originalMesh = meshFilter.sharedMesh;
        if (originalMesh == null) return;

        MeshRenderer meshRenderer = selectedObjects[0].GetComponent<MeshRenderer>();
        if (meshRenderer == null) return;

        if (originalMesh.subMeshCount <= 1)
            return;

        Material[] materials = meshRenderer.sharedMaterials;

        // 步骤1: 按材质分组子网格
        Dictionary<Material, List<CombineInstance>> materialGroups = new Dictionary<Material, List<CombineInstance>>();

        for (int subMeshIndex = 0; subMeshIndex < originalMesh.subMeshCount; subMeshIndex++)
        {
            if (subMeshIndex >= materials.Length) break;

            Material material = materials[subMeshIndex];
            if (material == null) continue;

            if (!materialGroups.ContainsKey(material))
                materialGroups[material] = new List<CombineInstance>();

            materialGroups[material].Add(new CombineInstance
            {
                mesh = originalMesh,
                subMeshIndex = subMeshIndex,
                transform = Matrix4x4.identity
            });
        }

        // 步骤2: 为每个材质组创建合并后的子网格
        List<CombineInstance> finalCombines = new List<CombineInstance>();
        List<Material> newMaterials = new List<Material>();
        List<Mesh> temporaryMeshes = new List<Mesh>(); // 跟踪临时网格

        foreach (var group in materialGroups)
        {
            List<CombineInstance> combines = group.Value;

            // 只有一个子网格时直接使用原始数据
            if (combines.Count == 1)
            {
                finalCombines.Add(combines[0]);
                newMaterials.Add(group.Key);
            }
            // 合并多个子网格
            else
            {
                Mesh combinedMesh = new Mesh();
                combinedMesh.CombineMeshes(combines.ToArray(), true); // 合并为一个子网格
                temporaryMeshes.Add(combinedMesh); // 标记为临时网格

                finalCombines.Add(new CombineInstance
                {
                    mesh = combinedMesh,
                    subMeshIndex = 0,
                    transform = Matrix4x4.identity
                });
                newMaterials.Add(group.Key);
            }
        }

        // 步骤3: 创建最终网格
        Mesh finalMesh = new Mesh();
        finalMesh.name = "MergedMaterialMesh";
        finalMesh.CombineMeshes(finalCombines.ToArray(), false); // 保留多个子网格

        meshFilter.sharedMesh = finalMesh; // 使用 sharedMesh 赋值
        meshRenderer.sharedMaterials = newMaterials.ToArray();

        // 步骤5:清理临时资源
        SaveMeshAsset(finalMesh);
        foreach (Mesh mesh in temporaryMeshes)
            Object.DestroyImmediate(mesh);
    }

    /// <summary>
    /// 合并网格 增加网格scale可能为负值的处理
    /// </summary>
    /// <param name="meshes"></param>
    /// <param name="transforms"></param>
    /// <param name="materialsList"></param>
    /// <returns></returns>
    private static Mesh MergeMeshes(List<Mesh> meshes, List<Matrix4x4> transforms, List<Material[]> materialsList)
    {
        Mesh mergedMesh = new Mesh();
        mergedMesh.name = "Merged_Mesh";

        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<Vector4> tangents = new List<Vector4>();
        List<Vector2> uv = new List<Vector2>();
        List<Vector2> uv2 = new List<Vector2>();
        List<Vector2> uv3 = new List<Vector2>();
        List<Vector2> uv4 = new List<Vector2>();
        List<Color> colors = new List<Color>();

        List<int[]> submeshes = new List<int[]>();
        int vertexOffset = 0;

        List<bool> reverseTrianglesFlags = new List<bool>();
        List<int> meshVertexCounts = new List<int>(); // 记录每个网格的顶点数

        for (int meshIndex = 0; meshIndex < meshes.Count; meshIndex++)
        {
            Mesh mesh = meshes[meshIndex];
            Matrix4x4 matrix = transforms[meshIndex];

            Vector3 scale = matrix.lossyScale;
            bool reverseTriangles = (scale.x * scale.y * scale.z) < 0;
            reverseTrianglesFlags.Add(reverseTriangles);

            Vector3[] meshVertices = mesh.vertices;
            Vector3[] meshNormals = mesh.normals;
            Vector4[] meshTangents = mesh.tangents;

            int vertexCount = meshVertices.Length;
            meshVertexCounts.Add(vertexCount);

            for (int i = 0; i < vertexCount; i++)
            {
                vertices.Add(matrix.MultiplyPoint(meshVertices[i]));

                if (meshNormals != null && i < meshNormals.Length)
                {
                    Vector3 normal = matrix.inverse.transpose.MultiplyVector(meshNormals[i]);
                    normals.Add(normal.normalized);
                }

                if (meshTangents != null && i < meshTangents.Length)
                {
                    Vector3 tangent = matrix.MultiplyVector((Vector3)meshTangents[i]);
                    tangents.Add(new Vector4(tangent.x, tangent.y, tangent.z, meshTangents[i].w));
                }
            }

            // === 修复：确保UV和颜色数据长度匹配 ===
            // UV通道0
            if (mesh.uv != null && mesh.uv.Length == vertexCount)
                uv.AddRange(mesh.uv);
            else
                uv.AddRange(Enumerable.Repeat(Vector2.zero, vertexCount));

            // UV通道1
            if (mesh.uv2 != null && mesh.uv2.Length == vertexCount)
                uv2.AddRange(mesh.uv2);
            else
                uv2.AddRange(Enumerable.Repeat(Vector2.zero, vertexCount));

            // UV通道2
            if (mesh.uv3 != null && mesh.uv3.Length == vertexCount)
                uv3.AddRange(mesh.uv3);
            else
                uv3.AddRange(Enumerable.Repeat(Vector2.zero, vertexCount));

            // UV通道3
            if (mesh.uv4 != null && mesh.uv4.Length == vertexCount)
                uv4.AddRange(mesh.uv4);
            else
                uv4.AddRange(Enumerable.Repeat(Vector2.zero, vertexCount));

            // 顶点颜色
            if (mesh.colors != null && mesh.colors.Length == vertexCount)
                colors.AddRange(mesh.colors);
            else
                colors.AddRange(Enumerable.Repeat(Color.white, vertexCount));
            // ===========================================

            for (int submeshIndex = 0; submeshIndex < mesh.subMeshCount; submeshIndex++)
            {
                int[] triangles = mesh.GetTriangles(submeshIndex);
                int[] newTriangles = new int[triangles.Length];

                if (reverseTriangles)
                {
                    for (int i = 0; i < triangles.Length; i += 3)
                    {
                        newTriangles[i] = triangles[i] + vertexOffset;
                        newTriangles[i + 1] = triangles[i + 2] + vertexOffset;
                        newTriangles[i + 2] = triangles[i + 1] + vertexOffset;
                    }
                }
                else
                {
                    for (int i = 0; i < triangles.Length; i++)
                    {
                        newTriangles[i] = triangles[i] + vertexOffset;
                    }
                }
                submeshes.Add(newTriangles);
            }

            vertexOffset += vertexCount;
        }

        mergedMesh.SetVertices(vertices);
        if (normals.Count > 0) mergedMesh.SetNormals(normals);
        if (tangents.Count > 0) mergedMesh.SetTangents(tangents);

        // 现在可以安全设置UVs
        if (uv.Count > 0) mergedMesh.SetUVs(0, uv);
        if (uv2.Count > 0) mergedMesh.SetUVs(1, uv2);
        if (uv3.Count > 0) mergedMesh.SetUVs(2, uv3);
        if (uv4.Count > 0) mergedMesh.SetUVs(3, uv4);
        if (colors.Count > 0) mergedMesh.SetColors(colors);

        mergedMesh.subMeshCount = submeshes.Count;
        for (int i = 0; i < submeshes.Count; i++)
        {
            mergedMesh.SetTriangles(submeshes[i], i);
        }

        mergedMesh.RecalculateBounds();
        mergedMesh.RecalculateNormals();
        mergedMesh.RecalculateTangents();

        mergedMesh.Optimize();
        mergedMesh.UploadMeshData(false);

        return mergedMesh;
    }


    private static Mesh CreateCenteredMeshCopy(Mesh originalMesh)
    {
        Mesh newMesh = new Mesh();
        newMesh.name = originalMesh.name + "_Copy";

        // 获取原始顶点数据
        Vector3[] vertices = originalMesh.vertices;
        Vector3[] normals = originalMesh.normals;
        Vector4[] tangents = originalMesh.tangents;
        Vector2[] uv = originalMesh.uv;
        Vector2[] uv2 = originalMesh.uv2;
        Vector2[] uv3 = originalMesh.uv3;
        Vector2[] uv4 = originalMesh.uv4;
        Color[] colors = originalMesh.colors;

        // 计算包围盒中心点
        Bounds bounds = originalMesh.bounds;
        Vector3 centerOffset = bounds.center;

        // 平移所有顶点使模型中心位于原点
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] -= centerOffset;
        }

        // 设置新网格数据
        newMesh.vertices = vertices;
        newMesh.normals = normals;
        newMesh.tangents = tangents;
        newMesh.uv = uv;
        newMesh.uv2 = uv2;
        newMesh.uv3 = uv3;
        newMesh.uv4 = uv4;
        newMesh.colors = colors;

        // 复制子网格和三角形数据
        newMesh.subMeshCount = originalMesh.subMeshCount;
        for (int i = 0; i < originalMesh.subMeshCount; i++)
        {
            newMesh.SetTriangles(originalMesh.GetTriangles(i), i);
        }

        // 重新计算包围盒和法线
        newMesh.RecalculateBounds();
        newMesh.RecalculateNormals();
        newMesh.RecalculateTangents();

        return newMesh;
    }

    private static void SaveMeshAsset(Mesh mesh)
    {
#if UNITY_EDITOR
        // 确保保存目录存在
        string folderPath = "Assets/Meshes";
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            AssetDatabase.CreateFolder("Assets", "Meshes");
        }

        // 生成唯一文件名
        string assetPath = $"{folderPath}/{mesh.name}.asset";

        // 避免覆盖已有文件
        assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);

        // 保存Mesh
        AssetDatabase.CreateAsset(mesh, assetPath);
        AssetDatabase.SaveAssets();

        Debug.Log($"Mesh saved: {assetPath}");
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = mesh;
#endif
    }
}
