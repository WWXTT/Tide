using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using SynergyUI; // CardDataPaths（TideServer 共享文件，见 csproj 显式条目）

namespace CardCore.Network
{
    /// <summary>
    /// 随机预组卡组（2026-10-28 开局流程改版「不选=随机卡组」定案，三方同源）：
    /// 联机服务器超时兜底（NetRoom.PumpDeckPickCountdown）/ 客户端随机档 / NetAiFiller AI 客人。
    /// 口径 = CardDataPaths.PresetDeckNames 三套主题预组（TutorialData 只读 json）随机抽一套整组
    /// （cardIds+skillCardId）——预组卡全在教学表合并索引内，恒过 ValidateDeckSubmit。
    /// 不引 DeckSerializer/DeckCatalog（UI 层程序集——TideServer 共享源编译不到），本地 DTO+JObject 解析。
    /// </summary>
    public static class NetPresetDecks
    {
        private static readonly Random Rng = new Random();
        private static readonly object RngLock = new object();

        /// <summary>随机抽一套预组（坏文件轮空重抽）。返回 null=全部不可读（预组缺失——上层自行兜底提示）。</summary>
        public static string[] RandomPreset(out string skillCardId)
        {
            skillCardId = null;
            var names = CardDataPaths.PresetDeckNames;
            if (names == null || names.Length == 0) return null;
            int start;
            lock (RngLock) { start = Rng.Next(names.Length); }
            for (int i = 0; i < names.Length; i++)
            {
                var ids = TryLoad(names[(start + i) % names.Length], out skillCardId);
                if (ids != null && ids.Length > 0) return ids;
            }
            return null;
        }

        /// <summary>按名读取一套预组（文件缺失/损坏返回 null——不抛）。</summary>
        public static string[] TryLoad(string deckName, out string skillCardId)
        {
            skillCardId = null;
            try
            {
                string path = CardDataPaths.TutorialFileIn(deckName + ".json");
                if (!File.Exists(path)) return null;
                var j = JObject.Parse(File.ReadAllText(path));
                var cardIds = j["cardIds"] as JArray;
                if (cardIds == null || cardIds.Count == 0) return null;
                var ids = new List<string>(cardIds.Count);
                foreach (var t in cardIds)
                {
                    var id = (string)t;
                    if (!string.IsNullOrEmpty(id)) ids.Add(id);
                }
                var skill = (string)j["skillCardId"];
                if (!string.IsNullOrEmpty(skill)) skillCardId = skill;
                return ids.ToArray();
            }
            catch (Exception)
            {
                return null; // 预组属随包只读资产——损坏按缺失处理（上层轮空/兜底）
            }
        }
    }
}
