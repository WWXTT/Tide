using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>回放帧源（2026-11-11 演出统一 S2·方案 §2.4 ReplayFilmSource）：BattleFilmStore.Load
    /// 一次性取整本剧本交给 BattlePlayhead.Load（回放=剧本完整、永不等待输入）；同时负责把剧本行
    /// 铺成时间轴数据（帧序展开——BeginTimeline 消费）与入口摘要（`复盘 · vs 对手`）。
    /// v1 无输入询问（Input 帧只随剧本透传展示——IBattleInputGate 只预留）。</summary>
    public sealed class ReplayFilmSource
    {
        /// <summary>剧本 id（入口传入——BattleEntry.ReplayFilmId 一次性消费）。</summary>
        public string MatchId { get; }

        /// <summary>载入的剧本（Load 失败=null——入口按无剧本退回）。</summary>
        public BattleFilm Film { get; }

        public ReplayFilmSource(string matchId)
        {
            MatchId = matchId;
            Film = BattleFilmStore.Load(matchId);
        }

        /// <summary>整包投给播放器（从 fromFrame 续播——时间轴跳转复用）。</summary>
        public void Play(BattlePlayhead playhead, IBattleStageSink sink, int fromFrame = 0)
        {
            if (Film == null) return;
            playhead.Load(Film, sink, fromFrame);
        }

        /// <summary>时间轴行（帧序展开）：回放文本=FullText ?? Text（本地局两者相同；联机 S4 起终局
        /// 全量行回填后 FullText=无遮蔽版）。帧前历史行（FrameIndex=-1）照铺（开局块/轮外行）。</summary>
        public List<BattleLogLine> TimelineLines()
        {
            if (Film == null) return new List<BattleLogLine>();
            var lines = new List<BattleLogLine>();
            foreach (var f in Film.Frames)
                for (int i = 0; i < f.Lines.Count; i++)
                    lines.Add(f.Lines[i]);
            return lines;
        }

        /// <summary>入口摘要文案（`对手名 · YYYY-MM-DD`——战斗屏 lbl-mode 消费）。</summary>
        public string Summary()
        {
            if (Film?.Header == null) return "复盘";
            var h = Film.Header;
            var date = h.CreatedAtUnix > 0
                ? DateTimeOffset.FromUnixTimeSeconds(h.CreatedAtUnix).LocalDateTime.ToString("MM-dd HH:mm")
                : "";
            return $"复盘 · vs {(string.IsNullOrEmpty(h.OppName) ? "对手" : h.OppName)} · {date}";
        }
    }
}
