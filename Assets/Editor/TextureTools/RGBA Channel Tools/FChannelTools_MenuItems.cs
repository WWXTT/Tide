using FIMSpace.FEditor;
using UnityEditor;

namespace FIMSpace.FTextureTools
{
    public static class FChannelTools_MenuItems
    {
        // ── 法线与通道（40-49，法线入口在 FTextureEditWindows_MenuItems）──
        [MenuItem("Assets/Texture Tools/通道图生成器", priority = 41)]
        public static void ChannelledGenerator()
        {
            FChannelledGenerator.Init();
        }


        [MenuItem("Assets/Texture Tools/通道插入", priority = 42)]
        public static void ChannelInserter()
        {
            FChannelInserter.Init();
        }


        [MenuItem("Assets/Texture Tools/通道提取", priority = 43)]
        public static void ChannelsExtract()
        {
            FChannelsExtractor.Init();
        }
    }
}