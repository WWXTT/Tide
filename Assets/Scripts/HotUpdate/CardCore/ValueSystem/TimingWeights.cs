using System;
using System.Collections.Generic;

namespace CardCore
{
    /// <summary>
    /// 时机系数权重表行（TimingWeights.json，2026-11-10 时机系数规格 v0.1 §3）。
    /// 计算只读 <see cref="Factor"/>；<see cref="Bucket"/> 只用于兜底归类与宿主校验，
    /// 不参与计算——同一时机在任何卡上都是同一个数（系数只依赖时机）。
    /// </summary>
    [Serializable]
    public class TimingWeightRow
    {
        public string Timing;   // TriggerTiming 枚举名（唯一键）
        public string Bucket;   // Once / Repeat / TapAnchored / Active / Disabled
        public float Lambda;    // λ：每回合该时点事件数（Repeat/TapAnchored 族的密度参数；Once 恒 0）
        public float Expected;  // E[N]：期望兑现次数（Once=p；Repeat=λ×L；TapAnchored=λ×Lc）
        public float Factor;    // f = min(E[N], Cutoff)——计价唯一读数（等效法术次数）
        public string Note;
    }

    /// <summary>表文件根（TideJson 整体反序列化；public 字段名 = TimingWeights.json 键，未知键忽略）。</summary>
    [Serializable]
    public class TimingWeightFile
    {
        public float Base;                       // 典型耐久（次数天花板；与卡表 durability 分布对齐）
        public float Cutoff;                     // f 封顶（与 Base 同值——Cutoff 是次数、Base 是耐久点，改一个必须同时看另一个）
        public float DurabilitySharePerTrigger;  // 每次触发消耗的耐久比例（D×share = 1 次触发；1/3 → D 恒兑现 3 次）
        public List<TimingWeightRow> Weights;
    }

    /// <summary>
    /// 时机系数权重表单源（docs/时机系数规格_v0.1.md；文件 Assets/Configs/TimingWeights.json，
    /// 刻意独立于 ValueSystemConfig.json 的字段级灌入（该表 2026-11-10 已结构化为 section 对象）——
    /// 35 行 × 6 列数据表不适合每列一个 C# 字段，见规格 §8）。
    ///
    /// 消费方：CostDerivationService.TimingCostFactorOf（计价）与 CounterRules.ConsumeTriggerDurabilityShare
    /// （耐久消耗）两口，**2026-11-10 段 2b 已切正：影子开关删除，不保留旧口径分支**——
    /// 计价直接读行 Factor、耐久走 D/3 累计口径；缺行/表不可读才回落 TimingFactor 桶（安全侧兜底）。
    /// 文件缺失/解析失败：告警一次并回落桶默认；Invalidate 后重读。
    /// </summary>
    public static class TimingWeights
    {
        private const string ConfigRelativePath = "Configs/TimingWeights.json";

        /// <summary>结界存活回合 L（规格 §2 校准基准）：只供验证器公式对账（E[N]=λ×L 重算），不参与计价。</summary>
        public const float EnchantLifetimeTurns = 4f;
        /// <summary>生物存活回合 Lc（同上，攻/守/横置族 E[N]=λ×Lc 的 Lc）。</summary>
        public const float CreatureLifetimeTurns = 3f;

        private static TimingWeightFile _table;
        private static Dictionary<TriggerTiming, TimingWeightRow> _map;
        private static bool _loadAttempted;
        private static int _version = 1;

        /// <summary>装载版本号（Invalidate 递增——编辑器改表后重跑验证器取新读数）。</summary>
        public static int Version => _version;

        /// <summary>f 封顶（表不可读返回 0——计价消费方须先 TryGet 命中行；缺行由调用方回落桶默认）。</summary>
        public static float Cutoff => Table?.Cutoff ?? 0f;

        /// <summary>典型耐久（次数天花板；与 Cutoff 同值联动——§9 风险 1：一个是次数一个是耐久点，改一个必须同时看另一个）。</summary>
        public static float Base => Table?.Base ?? 0f;

        /// <summary>每次触发消耗的耐久比例（表不可读返回 0=按旧口径处理）。</summary>
        public static float DurabilitySharePerTrigger => Table?.DurabilitySharePerTrigger ?? 0f;

        /// <summary>每满 N 次触发扣整份耐久 D（= 1/share 四舍五入；share=1/3 → 3）。
        /// share 缺失/非法时返回 1（退化为逐次扣——ConsumeTriggerDurabilityShare 影子关闭态不走本值）。</summary>
        public static int TriggersPerDeduction
        {
            get
            {
                float share = DurabilitySharePerTrigger;
                return share > 0f ? Math.Max(1, (int)Math.Round(1f / share)) : 1;
            }
        }

        /// <summary>全表行（表不可读返回 null——验证器 P9-9 断言非空 35 行）。</summary>
        public static IReadOnlyList<TimingWeightRow> Rows => Table?.Weights;

        /// <summary>按时机取行（计价消费须先判 Enabled；未知时机名/缺行返回 false → 调用方回落桶默认）。</summary>
        public static bool TryGet(TriggerTiming timing, out TimingWeightRow row)
        {
            row = null;
            if (_map == null)
            {
                var t = Table;
                if (t?.Weights == null || t.Weights.Count == 0) return false;
                var map = new Dictionary<TriggerTiming, TimingWeightRow>();
                foreach (var w in t.Weights)
                {
                    if (w == null || string.IsNullOrEmpty(w.Timing)) continue;
                    if (!Enum.TryParse(w.Timing, out TriggerTiming tt) || map.ContainsKey(tt)) continue;
                    map[tt] = w; // 首行优先；未知时机名丢弃（P9-9 对账点名，运行期不拦）
                }
                _map = map;
            }
            return _map.TryGetValue(timing, out row);
        }

        /// <summary>该时机的期望兑现次数 E[N]（2026-11-10 资格层新口径）：宿主资格闸的读数口——
        /// 生物挂多次档仅当 E[N]≤1（低频时机按期望收价）；未知时机/缺行/表不可读回落 1.0
        ///（与缺行计价回落桶默认同安全侧：恰为「可挂」边界，不误杀存量）。</summary>
        public static float ExpectedOf(TriggerTiming timing)
            => TryGet(timing, out var row) ? row.Expected : 1.0f;

        /// <summary>丢弃缓存（编辑器改表后重读；运行期单次装载不感知文件变化）。</summary>
        public static void Invalidate()
        {
            _table = null;
            _map = null;
            _loadAttempted = false;
            _version++;
        }

        private static TimingWeightFile Table
        {
            get
            {
                if (_loadAttempted) return _table;
                _loadAttempted = true;
                try
                {
                    string raw = TidePaths.ReadConfigText(ConfigRelativePath);
                    if (string.IsNullOrEmpty(raw))
                    {
                        TideLog.Warn($"[TimingWeights] 权重表不可读：{ConfigRelativePath}，时机计价/耐久消耗走旧口径");
                        return null;
                    }
                    _table = TideJson.FromJson<TimingWeightFile>(raw);
                    if (_table?.Weights == null || _table.Weights.Count == 0)
                    {
                        TideLog.Warn($"[TimingWeights] 权重表空/无行：{ConfigRelativePath}，走旧口径");
                        _table = null;
                    }
                }
                catch (Exception e)
                {
                    TideLog.Warn($"[TimingWeights] 解析 {ConfigRelativePath} 失败: {e.Message}，走旧口径");
                    _table = null;
                }
                return _table;
            }
        }
    }
}
