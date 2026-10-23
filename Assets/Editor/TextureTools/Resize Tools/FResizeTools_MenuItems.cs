using FIMSpace.FTex;
using UnityEditor;
using UnityEngine;

namespace FIMSpace.FEditor
{
    public static class FResizeTools_MenuItems
    {
        // ── 尺寸调整（80-89）────────────────────────────────────
        // 修改贴图分辨率 / 快速缩放两个窗口入口已删（点不动，失效）。
        [MenuItem("Assets/Texture Tools/缩放到最近二次幂", priority = 82)]
        public static void ResizeToPowerOf2()
        {
            try
            {
                for (int i = 0; i < Selection.objects.Length; i++)
                {
                    Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GetAssetPath(Selection.objects[i]));

                    EditorUtility.DisplayProgressBar("Scaling textures...", "Scaling texture " + texture.name, (float)i / (float)Selection.objects.Length);

                    if (texture != null)
                        FTextureEditorToolsMethods.ScaleTextureFile(texture, texture, new Vector2(FTex_Methods.FindNearestPowOf2(texture.width), FTex_Methods.FindNearestPowOf2(texture.height)));
                }

                EditorUtility.ClearProgressBar();
            }
            catch (System.Exception exc)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError("[Fimpo Image Tools Something went wrong when scaling textures! " + exc);
            }
        }

        [MenuItem("Assets/Texture Tools/缩放到下限二次幂", priority = 83)]
        public static void ResizeToPowerOf2Lower()
        {
            try
            {
                for (int i = 0; i < Selection.objects.Length; i++)
                {
                    Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GetAssetPath(Selection.objects[i]));

                    EditorUtility.DisplayProgressBar("Scaling textures...", "Scaling texture " + texture.name, (float)i / (float)Selection.objects.Length);

                    if (texture != null)
                        FTextureEditorToolsMethods.ScaleTextureFile(texture, texture, new Vector2(FTex_Methods.FindLowerPowOf2(texture.width), FTex_Methods.FindLowerPowOf2(texture.height)));
                }

                EditorUtility.ClearProgressBar();
            }
            catch (System.Exception exc)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError("[Fimpo Image Tools] Something went wrong when scaling textures! " + exc);
            }
        }

        [MenuItem("Assets/Texture Tools/缩放到上限二次幂", priority = 84)]
        public static void ResizeToPowerOf2Higher()
        {
            try
            {
                for (int i = 0; i < Selection.objects.Length; i++)
                {
                    Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GetAssetPath(Selection.objects[i]));

                    EditorUtility.DisplayProgressBar("Scaling textures...", "Scaling texture " + texture.name, (float)i / (float)Selection.objects.Length);

                    if (texture != null)
                        FTextureEditorToolsMethods.ScaleTextureFile(texture, texture, new Vector2(FTex_Methods.FindHigherPowOf2(texture.width), FTex_Methods.FindHigherPowOf2(texture.height)));
                }

                EditorUtility.ClearProgressBar();
            }
            catch (System.Exception exc)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError("[Fimpo Image Tools] Something went wrong when scaling textures! " + exc);
            }

        }

    }
}