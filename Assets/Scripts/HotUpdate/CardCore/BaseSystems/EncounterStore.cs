using System;
using System.Collections.Generic;
using System.IO;

namespace CardCore
{
    /// <summary>
    /// 联机对局记录（2026-11-09 云存档口径改版定案）：一场联机对局的终局快照——对手信息/胜负/
    /// 对手完整卡组/对手自建内容 payload/战报 markdown。落 persistentDataPath/Encounters/&lt;matchId&gt;.json，
    /// 随云存档同步（SaveBundleCodec 并集合并，不删本地）。对手内容默认不进玩家库（卡组/卡牌列表
    /// 不显示）——玩家在对局记录屏点「导入对手卡组」时才由 OpponentDeckImporter 拷入。
    /// matchId="m"+13位unix毫秒+4位随机hex（等长定长→字典序=时间序：云打包裁旧战报按名排序）。
    /// </summary>
    [Serializable]
    public class EncounterRecord
    {
        public string matchId;
        public long finishedAtUnix;      // 终局时刻（unix 秒）
        public string opponentName;      // 对手昵称（大厅昵称/TapTap 名；缺省「对手」）
        public string opponentOpenId;    // 对手 TapTap openId（局域网对手为空）
        public bool win;                 // 本机视角胜负
        public string ownDeckName;       // 本机卡组名
        public string opponentDeckName;  // 对手卡组名
        public string[] opponentCardIds; // 对手卡组主列表
        public string opponentSkillCardId;
        public string[] opponentTokenCardIds;
        public Network.MsgDeckTokenBinding[] opponentTokenBindings;
        public Network.MsgCardPayload[] cards;     // 对手自建卡 payload（导入时逐张拷入 Cards.json）
        public Network.MsgEffectPayload[] effects; // 对手自建效果 payload（导入时 upsert Effects.json）
        public Network.MsgAtomRowPayload[] atomRows; // 对手内容包原子行（导入时 add-if-missing）
        public string log;              // 战报 markdown（MatchLogService 导出；云打包超限时最旧先裁本字段）
    }

    /// <summary>
    /// 对局记录存取（文件型，无内存态——每次直读写盘；mtime 盯盘/云脏判定天然兼容）。
    /// 封顶 <see cref="KeepRecords"/> 条：Append 时按名（=时间序）裁最旧。
    /// </summary>
    public static class EncounterStore
    {
        /// <summary>保留最近对局条数（云打包 10MB 上限的总量护栏；超限另由打包侧裁最旧战报正文）。</summary>
        public const int KeepRecords = 100;

        public static string Dir => Path.Combine(TidePaths.PersistentDataPath, "Encounters");

        private static string PathOf(string matchId) => Path.Combine(Dir, matchId + ".json");

        /// <summary>新对局 ID：m + unix毫秒 + 4位随机hex（等长→字典序=时间序）。</summary>
        public static string NewMatchId()
        {
            long ms = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            string rnd = BitConverter.ToString(BitConverter.GetBytes(Guid.NewGuid().GetHashCode()))
                .Replace("-", "").Substring(0, 4);
            return $"m{ms:D13}{rnd}";
        }

        /// <summary>入账一条对局记录（写盘 + 超封顶裁最旧）。matchId 缺省自动生成。</summary>
        public static EncounterRecord Append(EncounterRecord record)
        {
            if (record == null) return null;
            if (string.IsNullOrEmpty(record.matchId)) record.matchId = NewMatchId();
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(PathOf(record.matchId), TideJson.ToJson(record, prettyPrint: true));
                TrimToCap();
            }
            catch (Exception ex)
            {
                TideLog.Warn($"[Encounter] 对局记录写盘失败（{record.matchId}）：{ex.Message}");
            }
            return record;
        }

        /// <summary>全部记录（matchId 降序=最新在前；坏文件跳过并告警）。</summary>
        public static List<EncounterRecord> LoadAll()
        {
            var result = new List<EncounterRecord>();
            foreach (string file in FilesAscending())
            {
                try { result.Add(TideJson.FromJson<EncounterRecord>(File.ReadAllText(file))); }
                catch (Exception ex) { TideLog.Warn($"[Encounter] 记录损坏跳过（{Path.GetFileName(file)}）：{ex.Message}"); }
            }
            result.Reverse(); // 最新在前
            return result;
        }

        /// <summary>单条读取（无则 null）。</summary>
        public static EncounterRecord Load(string matchId)
        {
            string path = PathOf(matchId);
            if (!File.Exists(path)) return null;
            try { return TideJson.FromJson<EncounterRecord>(File.ReadAllText(path)); }
            catch { return null; }
        }

        /// <summary>封顶裁剪：按文件名（=时间序）保留最近 KeepRecords 条。</summary>
        private static void TrimToCap()
        {
            var files = FilesAscending();
            for (int i = 0; i < files.Count - KeepRecords; i++)
            {
                try { File.Delete(files[i]); }
                catch (Exception ex) { TideLog.Warn($"[Encounter] 封顶裁旧失败（{files[i]}）：{ex.Message}"); }
            }
        }

        private static List<string> FilesAscending()
        {
            var files = new List<string>();
            if (!Directory.Exists(Dir)) return files;
            files.AddRange(Directory.GetFiles(Dir, "*.json"));
            files.Sort(StringComparer.Ordinal); // matchId 等长定长→字典序=时间序
            return files;
        }
    }
}
