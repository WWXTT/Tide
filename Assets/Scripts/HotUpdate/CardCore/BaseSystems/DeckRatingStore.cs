using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CardCore.Network;
using SynergyUI;

namespace CardCore
{
    /// <summary>一套卡组的排位战绩（S8 2026-11-10）。键=卡组内容指纹——改任何一张卡/绑定→指纹变→
    /// 旧记录不再命中（=胜率归零重打，防「换一张卡保胜率」）。</summary>
    [Serializable]
    public class DeckRatingEntry
    {
        public string fingerprint;
        public int wins;
        public int losses;
        public long updatedUnix;
    }

    /// <summary>排位战绩存取（S8；文件型单 JSON——persistentDataPath/DeckRatings/ratings.json）。
    /// 只对 ranked 终局记账（MsgMatchSummary.Ranked 到达时 +1 胜/负，见 BattleScreen.Net）；
    /// 读写全量落盘（百级条目无性能顾虑）。指纹=NetMatchHandshake.ComputeDeckDigest 三段
    /// （AtomicRowsHash:ContentSig:TokenSig——卡内容/效果指纹/衍生物绑定全覆盖）。</summary>
    public static class DeckRatingStore
    {
        private static string Dir => Path.Combine(TidePaths.PersistentDataPath, "DeckRatings");
        private static string FilePath => Path.Combine(Dir, "ratings.json");

        /// <summary>指纹（S8 匹配/记账键）：摘要三段以「:」连接（段内 hex 无「:」，不再转义）。
        /// 与 DeckSubmit 摘要同口径——内容/绑定任何变化都会换指纹（旧战绩自然失效）。</summary>
        public static string FingerprintOf(IReadOnlyList<string> cardIds,
            IReadOnlyList<string> tokenCardIds = null,
            IReadOnlyList<DeckTokenBinding> tokenBindings = null)
        {
            var digest = NetMatchHandshake.ComputeDeckDigest(cardIds, tokenCardIds, tokenBindings);
            return $"{digest.AtomicRowsHash}:{digest.ContentSig}:{digest.TokenSig}";
        }

        /// <summary>按指纹取战绩（无记录返回 null）。</summary>
        public static DeckRatingEntry Get(string fingerprint)
        {
            if (string.IsNullOrEmpty(fingerprint)) return null;
            return LoadAll().FirstOrDefault(e => e != null && e.fingerprint == fingerprint);
        }

        /// <summary>胜率千分比（0..1000；无战绩=-1——首战双方按无数据互配）。</summary>
        public static int PermilleOf(string fingerprint)
        {
            var e = Get(fingerprint);
            if (e == null || e.wins + e.losses <= 0) return -1;
            return (int)Math.Round(e.wins * 1000.0 / (e.wins + e.losses));
        }

        /// <summary>记账一次胜负（S8：仅 ranked 终局调）。</summary>
        public static void Record(string fingerprint, bool win)
        {
            if (string.IsNullOrEmpty(fingerprint)) return;
            var all = LoadAll();
            var entry = all.FirstOrDefault(e => e != null && e.fingerprint == fingerprint);
            if (entry == null)
            {
                entry = new DeckRatingEntry { fingerprint = fingerprint };
                all.Add(entry);
            }
            if (win) entry.wins++; else entry.losses++;
            entry.updatedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            SaveAll(all);
        }

        /// <summary>战绩描述（排位页 lbl-ranked-rate）：如「62.5% · 5胜3负」；无战绩=「暂无战绩」。</summary>
        public static string Describe(string fingerprint)
        {
            var e = Get(fingerprint);
            if (e == null || e.wins + e.losses <= 0) return "暂无战绩";
            return $"{e.wins * 100.0 / (e.wins + e.losses):F1}% · {e.wins}胜{e.losses}负";
        }

        private static List<DeckRatingEntry> LoadAll()
        {
            try
            {
                if (!File.Exists(FilePath)) return new List<DeckRatingEntry>();
                var data = TideJson.FromJson<RatingFile>(File.ReadAllText(FilePath));
                return data?.items ?? new List<DeckRatingEntry>();
            }
            catch (Exception ex)
            {
                TideLog.Warn($"[DeckRating] 战绩读盘失败（按空处理）：{ex.Message}");
                return new List<DeckRatingEntry>();
            }
        }

        private static void SaveAll(List<DeckRatingEntry> items)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(FilePath, TideJson.ToJson(new RatingFile { items = items }, prettyPrint: true));
            }
            catch (Exception ex)
            {
                TideLog.Warn($"[DeckRating] 战绩写盘失败：{ex.Message}");
            }
        }

        [Serializable]
        private class RatingFile
        {
            public List<DeckRatingEntry> items = new List<DeckRatingEntry>();
        }
    }
}
