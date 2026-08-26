using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CardCore
{
    // ================================================================
    // 代价抵消（无色抵扣）配置 + 服务
    // ----------------------------------------------------------------
    // 元素消耗是最基本代价；更高层「代价抵消」用其它资源换取减免：
    // 每次抵消 = 减 1 费（少付 1 个所需元素），消耗对应机制的资源，
    // 且必须满足「每种本色至少保留 1 点元素」（不能把某色完全抵掉）。
    // 4 机制 / 单局上限合计 14+6+6+5 = 31 = 玩家初始资源。
    // ================================================================

    public enum OffsetMechanism
    {
        Drain,      // 流失（无源失命）
        Discard,    // 弃手牌
        Mill,       // 磨本组
        SendExtra,  // 送额外组
    }

    [Serializable]
    public class CostOffsetMechanismConfig
    {
        public string Mechanism;        // OffsetMechanism 枚举名
        public string DisplayName;
        public int ResourcePerOffset;   // 抵消 1 费消耗的资源量（命/张）
        public int MaxOffsetPerGame;    // 单局该机制最多抵消的费数
    }

    [Serializable]
    public class CostOffsetConfig
    {
        public List<CostOffsetMechanismConfig> mechanisms = new List<CostOffsetMechanismConfig>();
    }

    /// <summary>代价抵消事件（每次成功抵消 1 费发布一次）。</summary>
    public class CostOffsetEvent : GameEventBase
    {
        public Player Player { get; set; }
        public OffsetMechanism Mechanism { get; set; }
        public ManaType ReducedColor { get; set; }
        public Entity Source { get; set; }
    }

    /// <summary>
    /// 代价抵消服务：把「元素消耗代价」升级为「抵消 + 元素支付」的异步流程。
    /// 交互式（有 UI 且非 AI）：逐次弹出 1-of-N 让玩家选机制或停止；
    /// AI / 无头 / 超时：不主动抵消，仅在元素不足时贪心抵消补齐。
    /// </summary>
    public static class CostOffsetService
    {
        private const string ConfigRelativePath = "Configs/CostOffsetConfig.json";

        private static Dictionary<OffsetMechanism, CostOffsetMechanismConfig> _config;

        private static Dictionary<OffsetMechanism, CostOffsetMechanismConfig> Config
        {
            get
            {
                if (_config == null) Load();
                return _config;
            }
        }

        private static void Load()
        {
            _config = new Dictionary<OffsetMechanism, CostOffsetMechanismConfig>();
            try
            {
                string path = Path.Combine(Application.dataPath, ConfigRelativePath);
                if (!File.Exists(path))
                {
                    Debug.LogWarning($"[CostOffsetService] 配置文件不存在: {path}，抵消功能将不可用");
                    return;
                }
                var parsed = JsonUtility.FromJson<CostOffsetConfig>(File.ReadAllText(path));
                if (parsed?.mechanisms == null) return;
                foreach (var m in parsed.mechanisms)
                {
                    if (m == null || string.IsNullOrEmpty(m.Mechanism)) continue;
                    if (Enum.TryParse<OffsetMechanism>(m.Mechanism, out var mech))
                        _config[mech] = m;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CostOffsetService] 加载 {ConfigRelativePath} 失败: {e.Message}");
            }
        }

        // ======================================== 对外入口 ========================================

        /// <summary>
        /// 支付一组元素消耗代价：先（可选）抵消、再从可用元素扣除。
        /// 返回 false 表示无法支付（调用方应中止结算）。
        /// </summary>
        public static async UniTask<bool> PayElementWithOffsetAsync(List<CostInstance> elementCosts, CostContext ctx)
        {
            if (elementCosts == null || elementCosts.Count == 0)
                return true;
            if (ctx?.Payer == null || ctx.ElementPool == null)
                return false;

            // 1. 按颜色聚合需求
            var need = new Dictionary<ManaType, int>();
            foreach (var c in elementCosts)
            {
                if (c.Type != CostType.ElementConsume || c.Value <= 0) continue;
                need.TryGetValue(c.ManaType, out var prev);
                need[c.ManaType] = prev + c.Value;
            }
            if (need.Count == 0) return true;

            var avail = ctx.ElementPool.GetPool(ctx.Payer).AvailableMana;

            // 2. 交互式抵消（仅有 UI 且非 AI 时）
            bool interactive = TargetSelectionService.Current != null && !ctx.Payer.IsAI;
            if (interactive)
                await InteractiveOffsetAsync(need, ctx);

            // 3. 贪心兜底：元素仍不足则继续抵消补齐（AI/超时/交互后仍欠）
            GreedyOffset(need, avail, ctx);

            // 4. 最终元素支付（原子：先确认可付，再扣）
            if (!CanPayNeed(need, avail))
                return false;
            PayNeed(need, avail, ctx);
            return true;
        }

        /// <summary>
        /// 非破坏性预检：在「最大可能抵消」后，元素是否仍可支付。
        /// 供 CanActivate 发动前判定（让抵消能救活原本直接付不起的发动）。
        /// </summary>
        public static bool CanAfford(List<CostInstance> elementCosts, CostContext ctx)
        {
            if (elementCosts == null || elementCosts.Count == 0) return true;
            if (ctx?.Payer == null || ctx.ElementPool == null) return false;

            var need = new Dictionary<ManaType, int>();
            foreach (var c in elementCosts)
            {
                if (c.Type != CostType.ElementConsume || c.Value <= 0) continue;
                need.TryGetValue(c.ManaType, out var prev);
                need[c.ManaType] = prev + c.Value;
            }
            if (need.Count == 0) return true;

            // 可用元素副本（不改动真实池）
            var avail = new Dictionary<ManaType, int>(ctx.ElementPool.GetPool(ctx.Payer).AvailableMana);

            // 估算最大可抵消费数（受 Reducible、单局上限、资源量三者约束）
            int maxOffset = Math.Min(Reducible(need), MaxAffordableOffsets(ctx));

            // 贪心从最高需求颜色削减 maxOffset 次（副本上模拟）
            var needCopy = new Dictionary<ManaType, int>(need);
            for (int i = 0; i < maxOffset; i++)
            {
                var color = needCopy.Where(kv => kv.Value > 1)
                                    .OrderByDescending(kv => kv.Value)
                                    .Select(kv => (ManaType?)kv.Key)
                                    .FirstOrDefault();
                if (color == null) break;
                needCopy[color.Value]--;
            }

            return CanPayNeed(needCopy, avail);
        }

        /// <summary>各机制（受单局上限 + 当前资源）可抵消的费数之和。</summary>
        private static int MaxAffordableOffsets(CostContext ctx)
        {
            int total = 0;
            foreach (OffsetMechanism mech in Enum.GetValues(typeof(OffsetMechanism)))
            {
                if (!Config.TryGetValue(mech, out var cfg)) continue;
                int cap = RemainingCap(mech, ctx.Payer);
                if (cap <= 0) continue;
                int byResource = ResourceAffordableOffsets(mech, cfg, ctx);
                total += Math.Min(cap, byResource);
            }
            return total;
        }

        /// <summary>仅按当前资源量估算某机制可抵消的费数（非破坏）。</summary>
        private static int ResourceAffordableOffsets(OffsetMechanism mech, CostOffsetMechanismConfig cfg, CostContext ctx)
        {
            int per = Math.Max(1, cfg.ResourcePerOffset);
            switch (mech)
            {
                case OffsetMechanism.Drain:
                    // LifePayment 要求 Life > Value，保守取 (Life-1)/per
                    return Math.Max(0, (ctx.Payer.Life - 1) / per);
                case OffsetMechanism.Discard:
                    return (ctx.ZoneManager?.GetCards(ctx.Payer, Zone.Hand)?.Count ?? 0) / per;
                case OffsetMechanism.Mill:
                    return (ctx.ZoneManager?.GetCards(ctx.Payer, Zone.Deck)?.Count ?? 0) / per;
                case OffsetMechanism.SendExtra:
                    return (ctx.ZoneManager?.GetCards(ctx.Payer, Zone.ExtraDeck)?.Count ?? 0) / per;
                default:
                    return 0;
            }
        }

        // ======================================== 交互式抵消 ========================================

        private static async UniTask InteractiveOffsetAsync(Dictionary<ManaType, int> need, CostContext ctx)
        {
            while (Reducible(need) > 0)
            {
                var usable = UsableMechanisms(ctx);
                if (usable.Count == 0) break;

                var labels = new List<string> { "不再抵消（直接支付元素）" };
                labels.AddRange(usable.Select(m =>
                {
                    var cfg = Config[m];
                    return $"{cfg.DisplayName}（-1费 / 消耗{cfg.ResourcePerOffset}）";
                }));

                int idx = await TargetSelectionService.RequestOneIndexAsync(ctx.Payer, labels, "选择代价抵消");
                if (idx <= 0 || idx > usable.Count) break; // 0 = 不再抵消

                if (!ApplyOneOffset(usable[idx - 1], need, ctx))
                    break;
            }
        }

        // ======================================== 贪心抵消 ========================================

        private static void GreedyOffset(Dictionary<ManaType, int> need, Dictionary<ManaType, int> avail, CostContext ctx)
        {
            // 按 config 顺序优先，逐次抵消，直到可支付或无法再抵消
            while (!CanPayNeed(need, avail) && Reducible(need) > 0)
            {
                var usable = UsableMechanisms(ctx);
                if (usable.Count == 0) break;
                if (!ApplyOneOffset(usable[0], need, ctx))
                    break;
            }
        }

        // ======================================== 单次抵消 ========================================

        /// <summary>应用一次（1 费）抵消：扣资源、记计数、降一色需求。成功返回 true。</summary>
        private static bool ApplyOneOffset(OffsetMechanism mech, Dictionary<ManaType, int> need, CostContext ctx)
        {
            if (Reducible(need) <= 0) return false;
            if (RemainingCap(mech, ctx.Payer) <= 0) return false;

            var cfg = Config[mech];
            var resourceCost = new CostInstance { Type = ToCostType(mech), Value = cfg.ResourcePerOffset };
            if (!CostHandlerRegistry.CanPay(resourceCost, ctx)) return false;

            // 选一个需求 > 1 的颜色削减（保留 ≥1 本色）
            var color = need.Where(kv => kv.Value > 1)
                            .OrderByDescending(kv => kv.Value)
                            .Select(kv => (ManaType?)kv.Key)
                            .FirstOrDefault();
            if (color == null) return false;

            CostHandlerRegistry.Pay(resourceCost, ctx);
            need[color.Value]--;
            IncrementCap(mech, ctx.Payer);

            EventManager.Instance.Publish(new CostOffsetEvent
            {
                Player = ctx.Payer,
                Mechanism = mech,
                ReducedColor = color.Value,
                Source = ctx.Source
            });
            return true;
        }

        // ======================================== 约束/工具 ========================================

        /// <summary>可抵消总量 = Σ max(0, need-1)（每色至少保留 1 点本色）。</summary>
        private static int Reducible(Dictionary<ManaType, int> need)
            => need.Values.Sum(v => Math.Max(0, v - 1));

        /// <summary>当前可用（未达上限 + 资源足够抵 1 次）的机制，按 config 顺序。</summary>
        private static List<OffsetMechanism> UsableMechanisms(CostContext ctx)
        {
            var list = new List<OffsetMechanism>();
            foreach (OffsetMechanism mech in Enum.GetValues(typeof(OffsetMechanism)))
            {
                if (!Config.ContainsKey(mech)) continue;
                if (RemainingCap(mech, ctx.Payer) <= 0) continue;
                var probe = new CostInstance { Type = ToCostType(mech), Value = Config[mech].ResourcePerOffset };
                if (CostHandlerRegistry.CanPay(probe, ctx))
                    list.Add(mech);
            }
            return list;
        }

        private static bool CanPayNeed(Dictionary<ManaType, int> need, Dictionary<ManaType, int> avail)
        {
            foreach (var kv in need)
            {
                if (kv.Value <= 0) continue;
                var affinity = kv.Key == ManaType.Gray
                    ? ElementAffinity.Generic
                    : ElementAffinity.Single(kv.Key);
                if (!ElementPaymentValidator.CanPay(affinity, avail, kv.Value))
                    return false;
            }
            return true;
        }

        private static void PayNeed(Dictionary<ManaType, int> need, Dictionary<ManaType, int> avail, CostContext ctx)
        {
            var paid = new Dictionary<int, float>();
            foreach (var kv in need)
            {
                if (kv.Value <= 0) continue;
                var affinity = kv.Key == ManaType.Gray
                    ? ElementAffinity.Generic
                    : ElementAffinity.Single(kv.Key);
                var plan = ElementPaymentValidator.GetPaymentPlan(affinity, avail, kv.Value);
                if (plan == null) continue;
                foreach (var p in plan)
                {
                    avail[p.Key] -= p.Value;
                    paid.TryGetValue((int)p.Key, out var prev);
                    paid[(int)p.Key] = prev + p.Value;
                }
            }

            if (paid.Count > 0)
            {
                EventManager.Instance.Publish(new ElementPoolPayEvent
                {
                    Player = ctx.Payer,
                    PaidCost = paid
                });
            }
        }

        private static CostType ToCostType(OffsetMechanism mech)
        {
            switch (mech)
            {
                case OffsetMechanism.Drain: return CostType.LifePayment;
                case OffsetMechanism.Discard: return CostType.DiscardCard;
                case OffsetMechanism.Mill: return CostType.MillDeck;
                case OffsetMechanism.SendExtra: return CostType.SendExtraDeck;
                default: return CostType.LifePayment;
            }
        }

        private static int RemainingCap(OffsetMechanism mech, Player p)
        {
            int max = Config.TryGetValue(mech, out var c) ? c.MaxOffsetPerGame : 0;
            int used;
            switch (mech)
            {
                case OffsetMechanism.Drain: used = p.OffsetDrainUsed; break;
                case OffsetMechanism.Discard: used = p.OffsetDiscardUsed; break;
                case OffsetMechanism.Mill: used = p.OffsetMillUsed; break;
                case OffsetMechanism.SendExtra: used = p.OffsetSendExtraUsed; break;
                default: used = 0; break;
            }
            return max - used;
        }

        private static void IncrementCap(OffsetMechanism mech, Player p)
        {
            switch (mech)
            {
                case OffsetMechanism.Drain: p.OffsetDrainUsed++; break;
                case OffsetMechanism.Discard: p.OffsetDiscardUsed++; break;
                case OffsetMechanism.Mill: p.OffsetMillUsed++; break;
                case OffsetMechanism.SendExtra: p.OffsetSendExtraUsed++; break;
            }
        }
    }
}
