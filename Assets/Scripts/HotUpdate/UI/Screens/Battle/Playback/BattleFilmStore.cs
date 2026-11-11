using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using CardCore;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>剧本存取（2026-11-11 演出统一 S2·方案 §2.5）：persistentDataPath/Films/&lt;matchId&gt;.film.gz
    ///（TideJson 序列化 + GZip，不进云存档）。本地局终局自动录最近一局（调试/复盘用）；上限单局 2000 帧、
    /// 目录保留 20 局（Prune 收口）。回放断线/缺本=入口置灰（v1 已知限制）。</summary>
    public static class BattleFilmStore
    {
        private const int KeepCount = 20;       // 目录保留局数（超出按最旧删除）
        private const int MaxFrames = 2000;     // 单局帧上限（防异常局撑爆磁盘）

        public static string Dir => Path.Combine(Application.persistentDataPath, "Films");

        private static string PathOf(string matchId) => Path.Combine(Dir, matchId + ".film.gz");

        /// <summary>落盘（GZip+TideJson；帧超限截尾保留头部与终局帧——异常局兜底不炸存）。</summary>
        public static void Save(BattleFilm film)
        {
            if (film == null || string.IsNullOrEmpty(film.Header?.MatchId)) return;
            if (film.Frames.Count > MaxFrames)
            {
                Debug.LogWarning($"[Film] 帧数超上限（{film.Frames.Count}>{MaxFrames}）——截尾存盘");
                var keep = new List<BattleFrame>(film.Frames.Take(MaxFrames - 1));
                var end = film.Frames[film.Frames.Count - 1];
                if (end.Kind == BattleFrameKind.End) keep.Add(end); // 终局帧必留（回放收卷口）
                film.Frames.Clear();
                film.Frames.AddRange(keep);
            }
            Directory.CreateDirectory(Dir);
            File.WriteAllBytes(PathOf(film.Header.MatchId), Compress(TideJson.ToJson(film)));
            Prune();
        }

        /// <summary>读盘（缺本/损坏返回 null——复盘入口按无剧本处理）。</summary>
        public static BattleFilm Load(string matchId)
        {
            if (string.IsNullOrEmpty(matchId)) return null;
            try
            {
                var path = PathOf(matchId);
                if (!File.Exists(path)) return null;
                return TideJson.FromJson<BattleFilm>(Decompress(File.ReadAllBytes(path)));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Film] 剧本读取失败（{matchId}）：{ex.Message}");
                return null;
            }
        }

        public static bool Has(string matchId) => !string.IsNullOrEmpty(matchId) && File.Exists(PathOf(matchId));

        public static void Delete(string matchId)
        {
            var path = PathOf(matchId);
            if (File.Exists(path)) File.Delete(path);
        }

        /// <summary>目录内剧本 id（按修改时间新→旧——「最近一局」入口/调试回放取首个）。
        /// 双层后缀 .film.gz 须全剥（GetFileNameWithoutExtension 只剥 .gz——带尾巴的 id 会让
        /// Has/Load 拼 .film.film.gz 恒 miss，复盘入口静默退回）。</summary>
        public static List<string> ListIds()
        {
            if (!Directory.Exists(Dir)) return new List<string>();
            const string suffix = ".film.gz";
            return new DirectoryInfo(Dir)
                .GetFiles("*.film.gz")
                .OrderByDescending(f => f.LastWriteTime)
                .Select(f => f.Name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
                    ? f.Name.Substring(0, f.Name.Length - suffix.Length)
                    : f.Name)
                .ToList();
        }

        /// <summary>目录修剪到保留局数（最旧先删；Save 尾部自动调）。</summary>
        public static void Prune()
        {
            var ids = ListIds();
            for (int i = KeepCount; i < ids.Count; i++) Delete(ids[i]);
        }

        private static byte[] Compress(string json)
        {
            using var output = new MemoryStream();
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);
            using (var gzip = new GZipStream(output, CompressionMode.Compress))
                gzip.Write(bytes, 0, bytes.Length); // 字节数≠字符数（中文 UTF8 多字节——曾致 JSON 截尾读盘失败）
            return output.ToArray();
        }

        private static string Decompress(byte[] data)
        {
            using var input = new MemoryStream(data);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var reader = new StreamReader(gzip, System.Text.Encoding.UTF8);
            return reader.ReadToEnd();
        }
    }
}
