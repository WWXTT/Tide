using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCore
{
    /// <summary>
    /// 合成器共享目录（2026-09-14 合成器重做）：替代已删除的 BranchConfig.json——
    /// 自由分支主干判定 + 有限分支（OutcomeGate）条件的**代码侧**目录。
    /// UI（EffectComposerScreen）与验证器（TestComposerModel）同源消费，防两套口径。
    ///
    /// - 条件评估真源仍是 BranchConditionEvaluator（运行时）；此处只暴露「用户可拼」的两个门
    ///   （CostDerivationService.GatePremium 有定价的：DmgKillsTarget/DeclareHit，均 2 灰）。
    /// - 预言族（延迟验证）/治疗门不进目录（无 premium 定价口径——目录恢复时再议）。
    /// - 产出族名单沿用 BranchConfigTable.OutcomeProducerTypes 的分组口径（伤害/宣言两族）。
    /// </summary>
    public static class ComposerCatalog
    {
        // ======================================== 自由分支主干 ========================================

        /// <summary>是否自由分支主干原子（表行 MountKinds=9——经 header.EngineKind 声明，不进普通步骤/奖励/payload）。</summary>
        public static bool IsEngineTrunk(AtomicEffectType t)
            => t == AtomicEffectType.BranchEngineClash
            || t == AtomicEffectType.BranchEngineLuckRoll
            || t == AtomicEffectType.BranchEngineCountdown;

        /// <summary>主干原子 → 引擎枚举（非主干返回 None）。落主干槽时 UI 写 header.EngineKind = 此值。</summary>
        public static BranchEngineKind TrunkToEngine(AtomicEffectType t)
        {
            if (t == AtomicEffectType.BranchEngineClash) return BranchEngineKind.Clash;
            if (t == AtomicEffectType.BranchEngineLuckRoll) return BranchEngineKind.LuckRoll;
            if (t == AtomicEffectType.BranchEngineCountdown) return BranchEngineKind.Countdown;
            return BranchEngineKind.None;
        }

        // ======================================== 有限分支（OutcomeGate）目录 ========================================

        /// <summary>有限分支产出条件目录项。Premium（门附加费=奖励预算）从 GatePremium 读——勿双写。</summary>
        public sealed class GateSpec
        {
            /// <summary>条件 id（评估走 BranchConditionEvaluator，如 "DmgKillsTarget"）。</summary>
            public string Id;
            /// <summary>中文名（UI 显示）。</summary>
            public string DisplayName;
            /// <summary>适用产出族（挂这个门的主干原子 EffectType 名单）。</summary>
            public string[] Producers;
        }

        /// <summary>伤害产出族（可挂「消灭目标时」）。</summary>
        public static readonly string[] DamageProducers = { "DealDamage", "PierceDamage", "DrainLife" };

        /// <summary>宣言产出族（可挂「宣言命中时」）。</summary>
        public static readonly string[] DeclareProducers = { "DeclareHand", "DeclareDeckTop", "DeclareArrow" };

        /// <summary>用户可拼的产出条件全集。</summary>
        public static readonly GateSpec[] OutcomeGates =
        {
            new GateSpec { Id = "DmgKillsTarget", DisplayName = "消灭目标时", Producers = DamageProducers },
            new GateSpec { Id = "DeclareHit",     DisplayName = "宣言命中时", Producers = DeclareProducers },
        };

        /// <summary>某主干原子可挂的门（按产出族过滤；不在产出族 → 空=不可做有限分支主干）。</summary>
        public static IEnumerable<GateSpec> GatesFor(AtomicEffectType trunk)
        {
            string name = trunk.ToString();
            return OutcomeGates.Where(g => Array.IndexOf(g.Producers, name) >= 0);
        }

        /// <summary>原子是否可做有限分支主干（产出族成员且非引擎主干）。</summary>
        public static bool CanBeGateTrunk(AtomicEffectType t)
            => !IsEngineTrunk(t) && OutcomeGates.Any(g => Array.IndexOf(g.Producers, t.ToString()) >= 0);

        /// <summary>门的显示文本（含【奖励x】——premium 取 GatePremium，与计价同源）。</summary>
        public static string GateLabel(GateSpec gate)
        {
            int premium = CostDerivationService.GatePremium.TryGetValue(gate.Id, out var p) ? p : 0;
            return $"{gate.DisplayName}【奖励{premium}】";
        }

        /// <summary>门预算（奖励原子推导费上限）：GatePremium 值（灰）。</summary>
        public static int GateBudget(GateSpec gate)
            => CostDerivationService.GatePremium.TryGetValue(gate.Id, out var p) ? p : 0;
    }
}
