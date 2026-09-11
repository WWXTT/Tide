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
    // 每次抵消 = 减 1 费（少付 1 个所需元素），消耗对应机制的资源。
    // 可抵至 0（定案：不设"每色保留 1 点"保底；护栏为纯色可支付量 ≤ 地牌槽上限
    // 与单局抵消预算上限）。4 机制 / 单局上限合计 14+6+6+5 = 31 = 玩家初始资源。
    // ================================================================

    public enum OffsetMechanism
    {
        Drain,          // 流失（无源失命）
        Discard,        // 弃手牌
        Mill,           // 送墓（本组）
        SendExtra,      // 送额外组
        // OpponentHeal / OpponentDraw 已删除（2026-09-10：被 Polarity 错边折价顶替）
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

    /// <summary>代价兑换事件（黑经济 2026-09-11：每次成功付资源兑换 1 黑元素发布一次；ReducedColor=产出色黑）。</summary>
    public class CostOffsetEvent : GameEventBase
    {
        public Player Player { get; set; }
        public OffsetMechanism Mechanism { get; set; }
        public ManaType ReducedColor { get; set; }
        public Entity Source { get; set; }
    }

    /// <summary>
    /// 代价兑换服务（黑经济 2026-09-11，原「代价抵消」改道）：把固有资源换成黑元素的异步流程。
    /// 兑换**不再减账单**——付资源得 1 黑入 bank（与三色同权：付黑/灰费用、浓度受限；纯色缺口补不了）。
    /// 交互式（有 UI 且非 AI）：逐次弹出 1-of-N 让玩家选机制或停止（可跨回合囤黑）；
    /// AI / 无头 / 超时：不主动兑换，仅在灰/黑缺口可推进时贪心兑换补齐。
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
                var parsed = JsonUtility.FromJson<CostOffsetConfig>(WrapBareArray(File.ReadAllText(path)));
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

        /// <summary>
        /// 导出器（Config/export_to_json.py）每个 sheet 产出顶层裸数组，
        /// 而 CostOffsetConfig DTO 需要 {"mechanisms":[...]} 包装——裸数组在此包一层
        /// （同 AtomicEffectTable.ParseEntries 的惯例）。已是对象则原样返回。
        /// </summary>
        private static string WrapBareArray(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw;
            return raw.TrimStart().StartsWith("[")
                ? "{\"mechanisms\":" + raw + "}"
                : raw;
        }

        // ======================================== 对外入口 ========================================

        /// <summary>
        /// 支付一组元素消耗代价：先（可选）兑换黑元素、再从可用元素扣除。
        /// 黑经济定案（2026-09-11）：兑换不再减账单——付固有资源得 1 黑入 bank（与三色同权，
        /// 可付黑/灰费用）；纯色（红/蓝/绿）缺口必须地牌产出实付。
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

            // 2. 交互式兑换（仅有 UI 且非 AI 时）：玩家自由把固有资源换成黑元素（可跨回合囤）
            bool interactive = TargetSelectionService.Current != null && !ctx.Payer.IsAI;
            if (interactive)
                await InteractiveOffsetAsync(ctx);

            // 3. 贪心兜底：仍不可付且兑换能推进可付性（灰/黑缺口）时继续补（AI/超时/交互后仍欠）
            GreedyOffset(need, avail, ctx);

            // 4. 最终元素支付（原子：先确认可付，再扣）
            if (!CanPayNeed(need, avail, ctx))
                return false;
            PayNeed(need, avail, ctx);
            return true;
        }

        /// <summary>
        /// 非破坏性预检：在「最大可能兑换」后，元素是否仍可支付。
        /// 黑经济口径：最大兑换次数全部折成黑入副本 bank（黑只补黑本色缺口与灰混付缺口，受浓度上限约束）。
        /// 供 CanActivate 发动前判定（让兑换能救活原本直接付不起的发动）。
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

            // 乐观模拟（同旧口径「最大可能抵消」）：全部可兑换次数折黑入副本
            int conversions = MaxAffordableOffsets(ctx);
            if (conversions > 0)
            {
                avail.TryGetValue(ManaType.Black, out var blackPrev);
                avail[ManaType.Black] = blackPrev + conversions;
            }

            return CanPayNeed(need, avail, ctx);
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
                    // LifePayment 要求 MaxHealth ≥ Value（2026-09-11 扣上限口径），保守取 (MaxHealth-1)/per
                    return Math.Max(0, (ctx.Payer.MaxHealth - 1) / per);
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

        // ======================================== 交互式兑换（黑经济） ========================================

        private static async UniTask InteractiveOffsetAsync(CostContext ctx)
        {
            while (true)
            {
                var usable = UsableMechanisms(ctx);
                if (usable.Count == 0) break;

                var labels = new List<string> { "不再兑换（直接支付元素）" };
                labels.AddRange(usable.Select(m =>
                {
                    var cfg = Config[m];
                    return $"{cfg.DisplayName}（+1黑元素 / 消耗{cfg.ResourcePerOffset}）";
                }));

                int idx = await TargetSelectionService.RequestOneIndexAsync(ctx.Payer, labels, "代价兑换黑元素");
                if (idx <= 0 || idx > usable.Count) break; // 0 = 不再兑换

                if (!ApplyOneOffset(usable[idx - 1], ctx))
                    break;
            }
        }

        // ======================================== 贪心兑换（黑经济） ========================================

        private static void GreedyOffset(Dictionary<ManaType, int> need, Dictionary<ManaType, int> avail, CostContext ctx)
        {
            // 按 config 顺序优先逐次兑换，直到可支付或兑换不再推进可付性。
            // 黑只补黑本色缺口与灰混付缺口（黑白非万能色定案）——纯色缺口兑换无用即停，不浪费资源。
            while (!CanPayNeed(need, avail, ctx) && ConversionHelps(need, avail, ctx))
            {
                var usable = UsableMechanisms(ctx);
                if (usable.Count == 0) break;
                if (!ApplyOneOffset(usable[0], ctx))
                    break;
            }
        }

        /// <summary>兑换是否能推进可付性：黑本色缺口（need[Black] > bank 黑）或灰混付缺口（通用容量不足）。</summary>
        private static bool ConversionHelps(Dictionary<ManaType, int> need, Dictionary<ManaType, int> avail, CostContext ctx)
        {
            if (need.TryGetValue(ManaType.Black, out var nb) && nb > 0
                && (!avail.TryGetValue(ManaType.Black, out var ab) || ab < nb))
                return true;

            if (need.TryGetValue(ManaType.Gray, out var ng) && ng > 0
                && !ElementPaymentValidator.CanPay(ElementAffinity.Generic, avail, ng, GetPureColorCap(ctx)))
                return true;

            return false;
        }

        // ======================================== 单次兑换 ========================================

        /// <summary>应用一次兑换：扣资源、记计数、得 1 黑元素入 bank（不再减账单）。成功返回 true。</summary>
        private static bool ApplyOneOffset(OffsetMechanism mech, CostContext ctx)
        {
            if (RemainingCap(mech, ctx.Payer) <= 0) return false;

            var cfg = Config[mech];
            var resourceCost = new CostInstance { Type = ToCostType(mech), Value = cfg.ResourcePerOffset };
            if (!CostHandlerRegistry.CanPay(resourceCost, ctx)) return false;

            CostHandlerRegistry.Pay(resourceCost, ctx);
            IncrementCap(mech, ctx.Payer);

            // 黑经济定案（2026-09-11）：付固有资源 = 得 1 黑（与三色同权，可付黑/灰费用，跨回合保留）
            ctx.ElementPool.AddMana(ctx.Payer, ManaType.Black, ctx.Source as Card, 1);

            EventManager.Instance.Publish(new CostOffsetEvent
            {
                Player = ctx.Payer,
                Mechanism = mech,
                ReducedColor = ManaType.Black, // 语义随黑经济平移：兑换产出色（历史消费者按机制过滤，颜色仅记录）
                Source = ctx.Source
            });
            return true;
        }

        // ======================================== 约束/工具 ========================================

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

        private static bool CanPayNeed(Dictionary<ManaType, int> need, Dictionary<ManaType, int> avail, CostContext ctx)
        {
            // 纯色浓度上限：每种纯色单次支付量 ≤ 当场地牌槽上限（灰不受限）
            int? cap = GetPureColorCap(ctx);
            foreach (var kv in need)
            {
                if (kv.Value <= 0) continue;
                var affinity = kv.Key == ManaType.Gray
                    ? ElementAffinity.Generic
                    : ElementAffinity.Single(kv.Key);
                if (!ElementPaymentValidator.CanPay(affinity, avail, kv.Value, cap))
                    return false;
            }
            return true;
        }

        /// <summary>当前纯色浓度上限（= 支付者地牌槽上限）；上下文不全时退化为不限制。</summary>
        private static int? GetPureColorCap(CostContext ctx)
            => ctx?.ElementPool != null && ctx.Payer != null
                ? ctx.ElementPool.GetLandCap(ctx.Payer)
                : (int?)null;

        private static void PayNeed(Dictionary<ManaType, int> need, Dictionary<ManaType, int> avail, CostContext ctx)
        {
            var paid = new Dictionary<int, float>();
            int? cap = GetPureColorCap(ctx);
            foreach (var kv in need)
            {
                if (kv.Value <= 0) continue;
                var affinity = kv.Key == ManaType.Gray
                    ? ElementAffinity.Generic
                    : ElementAffinity.Single(kv.Key);
                var plan = ElementPaymentValidator.GetPaymentPlan(affinity, avail, kv.Value, cap);
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
