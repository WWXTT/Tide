using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CardCore
{
    /// <summary>对局日志条目：保留事件对象引用（渲染延迟到导出/订阅时，避免每事件字符串化）</summary>
    public class MatchLogEntry
    {
        public uint EventId;
        public int TurnNumber;
        public string EventType;
        public IGameEvent Event;
        public DateTime Time;
    }

    /// <summary>
    /// 对局日志服务（P2b 定案）：内存结构化缓冲 + 按需导出 markdown。
    ///
    /// - 挂载：订阅 EventManager.AnyPublished（唯一全局收口，可见 100% 事件各恰一次——
    ///   路由事件只经一次 PublishDynamic，直发事件经 Publish）；GameCore.Reset 清缓冲。
    /// - 回合旁注：发布时读 GameCore.TurnEngine.TurnNumber（无核心时 0）。
    /// - 渲染：MatchLogRenderer 按 CLR 类型分派"事件→人话"；条目存原始事件引用，
    ///   渲染行仅在导出或 Appended 订阅者（如 Editor 播报器）需要时产生。
    /// - 缓冲上限 5 万条，超限截头并置标志（防超长对局内存膨胀）。
    /// </summary>
    public static class MatchLogService
    {
        private const int MaxEntries = 50000;

        private static readonly List<MatchLogEntry> _entries = new List<MatchLogEntry>();
        private static bool _started;
        private static bool _truncated;

        /// <summary>每条事件入缓冲时触发（Editor 播报器/UI 即时行输出用；无订阅者零成本）</summary>
        public static event Action<MatchLogEntry> Appended;

        /// <summary>开始记录（幂等；GameCore.Reset / 编辑器入口调用）</summary>
        public static void EnsureStarted()
        {
            if (_started) return;
            _started = true;
            EventManager.Instance.AnyPublished += OnAnyPublished;
        }

        /// <summary>清空缓冲（GameCore.Reset 调用；跨局不残留）</summary>
        public static void ClearAll()
        {
            _entries.Clear();
            _truncated = false;
        }

        public static IReadOnlyList<MatchLogEntry> Entries => _entries;

        public static bool WasTruncated => _truncated;

        private static void OnAnyPublished(IEventData eventData)
        {
            if (!(eventData is IGameEvent e)) return;

            var entry = new MatchLogEntry
            {
                EventId = e.EventId,
                TurnNumber = GameCore.Instance?.TurnEngine?.TurnNumber ?? 0,
                EventType = e.GetType().Name,
                Event = e,
                Time = e.Timestamp
            };

            if (_entries.Count >= MaxEntries)
            {
                _entries.RemoveAt(0);
                _truncated = true;
            }
            _entries.Add(entry);

            Appended?.Invoke(entry);
        }

        // ======================================== 导出 ========================================

        /// <summary>
        /// 导出 markdown 战报：头部（时间/双方/回合数）+ 按回合分节的人话行 + 事件类型统计附录。
        /// 返回写入路径（目录不存在则创建）；无条目返回 null。
        /// </summary>
        public static string ExportMarkdown(string path)
        {
            if (_entries.Count == 0) return null;

            var sb = new StringBuilder();
            var core = GameCore.Instance;

            sb.AppendLine("# 对局战报");
            sb.AppendLine();
            sb.AppendLine($"- 导出时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            if (core != null)
            {
                var p1 = core.Player1;
                var p2 = core.Player2;
                if (p1 != null && p2 != null)
                    sb.AppendLine($"- 对局双方：{MatchLogRenderer.Name(p1)} vs {MatchLogRenderer.Name(p2)}");
                sb.AppendLine($"- 总回合数：{core.TurnEngine?.TurnNumber ?? 0}");
            }
            sb.AppendLine($"- 事件条数：{_entries.Count}{(_truncated ? "（已达缓冲上限，早期记录被截断）" : "")}");
            sb.AppendLine();

            int? currentTurn = null;
            foreach (var entry in _entries)
            {
                if (entry.TurnNumber != currentTurn)
                {
                    currentTurn = entry.TurnNumber;
                    sb.AppendLine($"## ════ 回合 {entry.TurnNumber} ════");
                }

                var line = MatchLogRenderer.Render(entry.Event);
                if (!string.IsNullOrEmpty(line))
                    sb.AppendLine(line);
            }

            // 附录：事件类型统计（含无人话行的事件，保完整可观察性）
            sb.AppendLine();
            sb.AppendLine("## 附录：事件类型统计");
            foreach (var g in _entries.GroupBy(e => e.EventType).OrderByDescending(g => g.Count()))
                sb.AppendLine($"- {g.Key} × {g.Count()}");

            var dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            return path;
        }
    }
}
