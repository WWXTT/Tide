using System.Collections.Generic;
using System.Text;

namespace CardCore
{
    /// <summary>引擎概率采样探针（2026-11-10 用户定案：8 自由分支统一概率定价）。
    /// 只在 TideSim `--engine-prob` 下开启；记录每个引擎评估点的指标值与是否达标，
    /// 供离线生成 p_e(x) 表。运行时不消费，Enabled=false 时零开销（一次 bool 判断）。
    /// 指标口径：
    /// · LuckRoll=min(d1,d2)（p(x)=P(min≥x)）
    /// · Clash=双方攻击差（p(x)=P(diff≥x)）
    /// · DeathToll=当前计数（p(x)=P(count≥x)，按每次递增采样）
    /// · ManaSurplus=双方剩余元素差（p(x)=P(diff==x) 恰等）
    /// · Unity=邻接单位数（p(x)=P(n≥x)）
    /// Countdown/Curse/Blessing 暂不采样（无脚本/一次性载荷），由调用侧用经验值补。</summary>
    public static class EngineProbabilityProbe
    {
        public static bool Enabled;

        private sealed class Stat
        {
            public int Eval;
            public int Hit;
            public readonly Dictionary<int, int> Metric = new Dictionary<int, int>();
        }

        private static readonly Dictionary<BranchEngineKind, Stat> _stats =
            new Dictionary<BranchEngineKind, Stat>();

        public static void Reset()
        {
            _stats.Clear();
        }

        public static void Record(BranchEngineKind kind, int metric, bool hit)
        {
            if (!Enabled) return;
            if (!_stats.TryGetValue(kind, out var s))
            {
                s = new Stat();
                _stats[kind] = s;
            }
            s.Eval++;
            if (hit) s.Hit++;
            s.Metric[metric] = s.Metric.TryGetValue(metric, out var n) ? n + 1 : 1;
        }

        /// <summary>采样覆盖（8 引擎中实际有评估的）——TideSim/验证器报告用。</summary>
        public static IReadOnlyDictionary<BranchEngineKind, (int eval, int hit)> Summary()
        {
            var d = new Dictionary<BranchEngineKind, (int, int)>();
            foreach (var kv in _stats) d[kv.Key] = (kv.Value.Eval, kv.Value.Hit);
            return d;
        }

        /// <summary>输出 CSV：# totals 段（engine,eval,hit）；# metrics 段（engine,metric,count）。
        /// 调用方写文件/控制台；核心不依赖 IO。</summary>
        public static string ToCsv()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# totals");
            sb.AppendLine("engine,eval,hit");
            foreach (var kv in _stats)
                sb.AppendLine($"{kv.Key},{kv.Value.Eval},{kv.Value.Hit}");
            sb.AppendLine("# metrics");
            sb.AppendLine("engine,metric,count");
            foreach (var kv in _stats)
            {
                var keys = new List<int>(kv.Value.Metric.Keys);
                keys.Sort();
                foreach (var m in keys)
                    sb.AppendLine($"{kv.Key},{m},{kv.Value.Metric[m]}");
            }
            return sb.ToString();
        }
    }
}
