using FIMSpace.FTex;
using UnityEditor;
using UnityEngine;

namespace FIMSpace.FEditor
{
    public static class FResizeTools_MenuItems
    {
        // ── 尺寸调整（80-89）────────────────────────────────────
        [MenuItem("Assets/Texture Tools/修改贴图分辨率", priority = 80)]
        public static void ResizeTexture()
        {
            FResizeWindow.Init();
        }

        [MenuItem("Assets/Texture Tools/快速缩放", priority = 81)]
        public static void QuickResizeTexture()
        {
            FQuickResizeWindow.Init();
        }


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


        [MenuItem("Assets/Texture Tools/修改贴图分辨率", true)]
        [MenuItem("Assets/Texture Tools/快速缩放", true)]
        public static bool CheckResizeTextureAllSelected()
        {
            if (!Selection.activeObject) return false;

            for (int i = 0; i < Selection.objects.Length; i++) // We need just one file to be texture to return true
            {
                AssetImporter tex = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(Selection.objects[i]));
                if (tex as TextureImporter) return true;
            }

            return false;
        }
    }
}