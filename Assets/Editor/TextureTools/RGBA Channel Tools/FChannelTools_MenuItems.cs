using FIMSpace.FEditor;
using UnityEditor;

namespace FIMSpace.FTextureTools
{
    public static class FChannelTools_MenuItems
    {
        [MenuItem("Assets/Texture Tools/Channelled Generator Window", priority = -97)]
        public static void ChannelledGenerator()
        {
            FChannelledGenerator.Init();
        }


        [MenuItem("Assets/Texture Tools/Channel Insert", priority = 3)]
        public static void ChannelInserter()
        {
            FChannelInserter.Init();
        }


        [MenuItem("Assets/Texture Tools/Extract RGBA Channels", priority = 4)]
        public static void ChannelsExtract()
        {
            FChannelsExtractor.Init();
        }
    }
}