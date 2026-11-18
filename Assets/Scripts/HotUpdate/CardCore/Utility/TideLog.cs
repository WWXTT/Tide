using System;

namespace CardCore
{
    public enum TideLogLevel
    {
        Info,
        Warning,
        Error,
    }

    /// <summary>
    /// 跨宿主日志门面（2026-09-27 去 Unity 化）：共享源码双端（Unity 编辑器/玩家 + TideServer 纯 .NET）
    /// 的统一日志入口，取代散落各处的 UnityEngine.Debug 调用。
    /// Unity 侧由 TideUnityRuntime 装配 UnityEngine.Debug 接收器；未装配（服务器/裸进程）回落控制台。
    /// 接收器抛异常不向上传播（日志永不杀死调用方）。
    /// </summary>
    public static class TideLog
    {
        private static readonly object Gate = new object();
        private static Action<TideLogLevel, string> _sink;

        /// <summary>日志接收器（宿主装配；null=控制台直写）。签名：(级别, 文本)。</summary>
        public static Action<TideLogLevel, string> Sink
        {
            get => _sink;
            set => _sink = value;
        }

        /// <summary>验证器专用告警过滤器（2026-11-18）：返回 true=抑制并计数。
        /// 仅 CardPipelineVerifier 的测试夹具作用域装配；生产/运行时保持 null，不隐藏真实告警。
        /// 用途：夹具故意触发转换器/计费/引擎告警（VERIFY_*/TT_*/旧内联卡）时清空控制台噪声。</summary>
        public static Func<string, bool> WarningFilter;

        private static int _suppressedWarnings;

        /// <summary>当前作用域已抑制的告警数（验证器汇报用）。</summary>
        public static int SuppressedWarnings => _suppressedWarnings;

        public static void ResetSuppressedWarnings() => _suppressedWarnings = 0;

        public static void Info(object message) => Write(TideLogLevel.Info, message);
        public static void Warn(object message) => Write(TideLogLevel.Warning, message);
        public static void Error(object message) => Write(TideLogLevel.Error, message);

        private static void Write(TideLogLevel level, object message)
        {
            var text = message?.ToString() ?? "null";
            if (level == TideLogLevel.Warning && WarningFilter != null && WarningFilter(text))
            {
                _suppressedWarnings++;
                return;
            }
            var sink = _sink;
            if (sink != null)
            {
                try { sink(level, text); }
                catch { /* 日志接收器故障不影响业务 */ }
                return;
            }

            lock (Gate)
            {
                var writer = level == TideLogLevel.Error ? Console.Error : Console.Out;
                writer.WriteLine($"[{level}] {text}");
            }
        }
    }
}
