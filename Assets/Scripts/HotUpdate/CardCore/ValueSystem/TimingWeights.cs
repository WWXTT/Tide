using System;
using System.Collections.Generic;

namespace CardCore
{
    /// <summary>
    /// 时机档位（2026-11-14 用户定案，取代 2026-11-05「单次/多次（付费后一生几次）」两档）。
    /// 自动效果改按**一回合内的触发次数**分档——档位同时决定宿主资格与时机系数：
    /// · <see cref="PerTurn"/> 一回合一次：攻击/守卫（阻拦宣言）/回合开始/准备阶段/回合结束/登场/
    ///   进场/游戏开始/被攻击/横置/重置——默认每回合至多触发一次，**时机系数恒 1**（等效一张法术）；
    /// · <see cref="Rare"/> 稀有：被破坏/被除外/死亡/离场/墓地回场/角色死亡/占卜——比一回合一次
    ///   更难触发，**沿用之前按概率得到的系数**（表 Factor，如 被破坏 0.55、被除外 0.12）；
    /// · <see cref="Multi"/> 一回合多次：抽牌/伤害/使用卡牌/结算观察/原子三阶段/阶段起止/其他生物
    ///   进出场/施放法术——**只能挂结界**，每次触发扣 1 点耐久（耐久即次数池），系数 1；
    /// · <see cref="Active"/> 主动：Activate_* 三值，现付不进时序，恒 1。
    /// </summary>
    public enum TimingTier
    {
        Active = 0,
        PerTurn = 1,
        Rare = 2,
        Multi = 3,
    }

    /// <summary>
    /// 时机系数权重表行（TimingWeights.json，2026-11-14 档位制）。
    /// 计算只读 <see cref="Factor"/>（稀有档=概率系数，其余档恒 1）；<see cref="Bucket"/> 是
    /// <see cref="TimingTier"/> 名（宿主资格/计价档位的单源），<see cref="Lambda"/>/<see cref="Expected"/>
    /// 只作每回合频率的留档与验证读数——同一时机在任何卡上都是同一个档位/系数（只依赖时机）。
    /// </summary>
    [Serializable]
    public class TimingWeightRow
    {
        public string Timing;   // TriggerTiming 枚举名（唯一键）
        public string Bucket;   // TimingTier 名：PerTurn / Rare / Multi / Active
        public float Lambda;    // λ：每回合该时点事件数（留档/验证读数，不参与计算）
        public float Expected;  // 每回合期望触发次数（PerTurn=1；Rare=p；Multi=λ）——留档/展示
        public float Factor;    // 时机系数 f：PerTurn/Multi/Active 恒 1；Rare=触发概率 p（计价唯一读数）
        public string Note;
    }

    /// <summary>表文件根（TideJson 整体反序列化；public 字段名 = TimingWeights.json 键，未知键忽略）。</summary>
    [Serializable]
    public class TimingWeightFile
    {
        public float Base;                       // 留档（档位制后不参与计算——典型耐久）
        public float Cutoff;                     // 留档（档位制后不参与计算——旧 f 封顶）
        public float DurabilitySharePerTrigger;  // 留档（档位制后不参与计算——旧每次触发耐久比例）
        public List<TimingWeightRow> Weights;
    }

    /// <summary>
    /// 时机档位与系数表单源（docs/时机系数规格_v0.1.md §0「2026-11-14 档位制」；文件
    /// Assets/Configs/TimingWeights.json，刻意独立于 ValueSystemConfig.json 的字段级灌入——
    /// 35 行 × 6 列数据表不适合每列一个 C# 字段，见规格 §8）。
    ///
    /// 消费方：<see cref="TierOf"/>（宿主资格：生物只能挂 PerTurn/Rare，Multi=结界专属；
    /// 合成器下拉/装载校验/验证器共用）、CostDerivationService.TimingCostFactorOf /
    /// HostMountFactor（生物挂载价 ×f，结界 ×1）与 CounterRules.ConsumeTriggerDurability
    /// （结界每次触发 −1，不读 f）。缺行/表不可读回落内置档位镜像（安全侧兜底）。
    /// 文件缺失/解析失败：告警一次并回落；Invalidate 后重读。
    /// </summary>
    public static class TimingWeights
    {
        private const string ConfigRelativePath = "Configs/TimingWeights.json";

        /// <summary>结界存活回合 L（旧口径留档：只供验证器对旧 E[N]=λ×L 行核对，不参与计价）。</summary>
        public const float EnchantLifetimeTurns = 4f;
        /// <summary>生物存活回合 Lc（旧口径留档，同上）。</summary>
        public const float CreatureLifetimeTurns = 3f;

        private static TimingWeightFile _table;
        private static Dictionary<TriggerTiming, TimingWeightRow> _map;
        private static bool _loadAttempted;
        private static int _version = 1;

        /// <summary>一回合一次族（表 Bucket=PerTurn 的镜像——缺行/表不可读时的兜底分类）。</summary>
        private static readonly HashSet<TriggerTiming> PerTurnFallback = new HashSet<TriggerTiming>
        {
            TriggerTiming.OnPlay, TriggerTiming.OnGameStart, TriggerTiming.OnTurnStart,
            TriggerTiming.OnStandbyPhase, TriggerTiming.OnTurnEnd, TriggerTiming.OnSummon,
            TriggerTiming.OnAttack, TriggerTiming.OnAttacked, TriggerTiming.OnBlockDeclare,
            TriggerTiming.OnTap, TriggerTiming.OnUntap,
        };

        /// <summary>稀有族（概率触发——表 Bucket=Rare 的镜像兜底）。</summary>
        private static readonly HashSet<TriggerTiming> RareFallback = new HashSet<TriggerTiming>
        {
            TriggerTiming.OnDeath, TriggerTiming.OnDestroy, TriggerTiming.OnExile,
            TriggerTiming.OnLeaveBattlefield, TriggerTiming.OnReturnFromGraveyard,
            TriggerTiming.OnRoleDeath, TriggerTiming.OnScry,
        };

        /// <summary>装载版本号（Invalidate 递增——编辑器改表后重跑验证器取新读数）。</summary>
        public static int Version => _version;

        /// <summary>f 封顶（2026-11-14 档位制后退役：只留档，无运行期消费方）。</summary>
        public static float Cutoff => Table?.Cutoff ?? 0f;

        /// <summary>典型耐久（同上退役——只留档）。</summary>
        public static float Base => Table?.Base ?? 0f;

        /// <summary>旧「每次触发消耗的耐久比例」（2026-11-14 档位制后退役——耐久改按每次触发 −1，
        /// 本列与 TriggersPerDeduction 仅留档/展示，不再有运行时消费方）。</summary>
        public static float DurabilitySharePerTrigger => Table?.DurabilitySharePerTrigger ?? 0f;

        /// <summary>旧「每满 N 次触发扣整份耐久 D」（仅留档）。share 缺失/非法时返回 1。</summary>
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

        /// <summary>按时机取行（未知时机名/缺行返回 false → 调用方回落档位镜像/桶默认）。</summary>
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

        /// <summary>时机档位单源（2026-11-14 档位制）：表 Bucket 命中即读表；缺行/未知 Bucket/
        /// 表不可读回落内置镜像 <see cref="FallbackTierOf"/>。宿主资格（生物=PerTurn/Rare，
        /// Multi=结界专属）与计价（Rare 读行 Factor，其余恒 1）都经本口。</summary>
        public static TimingTier TierOf(TriggerTiming timing)
            => TryGet(timing, out var row) && TryParseTier(row.Bucket, out var tier)
                ? tier
                : FallbackTierOf(timing);

        /// <summary>内置档位镜像（表不可读/缺行兜底）：Activate_*=Active；一回合一次族/稀有族
        /// 两张内置集合；其余一律 Multi（自动两次以上即结界专属）。</summary>
        public static TimingTier FallbackTierOf(TriggerTiming timing)
        {
            if (timing == TriggerTiming.Activate_Active || timing == TriggerTiming.Activate_Instant
                || timing == TriggerTiming.Activate_Response) return TimingTier.Active;
            if (PerTurnFallback.Contains(timing)) return TimingTier.PerTurn;
            if (RareFallback.Contains(timing)) return TimingTier.Rare;
            return TimingTier.Multi;
        }

        /// <summary>Bucket 文本 → 档位（新名 PerTurn/Rare/Multi/Active；兼容旧表名
        /// Once→Rare、Repeat→Multi、TapAnchored/Universal/Disabled→PerTurn——旧表也能跑，
        /// 但 P9-9 会点名漂移，验收以新表为准）。</summary>
        public static bool TryParseTier(string bucket, out TimingTier tier)
        {
            tier = default;
            if (string.IsNullOrEmpty(bucket)) return false;
            switch (bucket.Trim())
            {
                case "Active": tier = TimingTier.Active; return true;
                case "PerTurn": case "PerTurnOnce": case "OncePerTurn": tier = TimingTier.PerTurn; return true;
                case "Rare": case "Once": tier = TimingTier.Rare; return true;
                case "Multi": case "Multiple": case "Repeat": tier = TimingTier.Multi; return true;
                case "TapAnchored": case "Universal": case "Disabled": tier = TimingTier.PerTurn; return true;
                default: return false;
            }
        }

        public static bool IsPerTurnTiming(TriggerTiming t) => TierOf(t) == TimingTier.PerTurn;
        public static bool IsRareTiming(TriggerTiming t) => TierOf(t) == TimingTier.Rare;
        public static bool IsMultiPerTurnTiming(TriggerTiming t) => TierOf(t) == TimingTier.Multi;

        /// <summary>该时机的每回合期望触发次数（2026-11-14 档位制后**只作留档/展示/验证读数**——
        /// 宿主资格改由 <see cref="TierOf"/> 判，不再看 E[N]）。未知时机/缺行回落 1.0。</summary>
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
