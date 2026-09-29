using UnityEditor;
namespace FIMSpace.FEditor
{
    public static class FTextureUtils_MenuItems
    {
        // ── 格式转换（100-109）──────────────────────────────────
        [MenuItem("Assets/Texture Tools/转换为 PNG", priority = 100)]
        public static void ToPNGConversion()
        {
            FTextureQuickConverter.Init();
        }
    }
}
