using UnityEditor;
namespace FIMSpace.FEditor
{
    public static class FTextureUtils_MenuItems
    {
        [MenuItem("Assets/Texture Tools/Convert any to PNG", priority = 102)]
        public static void ToPNGConversion()
        {
            FTextureQuickConverter.Init();
        }
    }
}
