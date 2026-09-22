using System;
using System.Collections.Generic;
using System.Linq;
using CardCore.Attribute;

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
            || t == AtomicEffectType.BranchEngineCountdown
            || t == AtomicEffectType.BranchEngineDeathToll
            || t == AtomicEffectType.BranchEngineManaSurplus
            || t == AtomicEffectType.BranchEngineNthHandCard;

        /// <summary>主干原子 → 引擎枚举（非主干返回 None）。落主干槽时 UI 写 header.EngineKind = 此值。</summary>
        public static BranchEngineKind TrunkToEngine(AtomicEffectType t)
        {
            if (t == AtomicEffectType.BranchEngineClash) return BranchEngineKind.Clash;
            if (t == AtomicEffectType.BranchEngineLuckRoll) return BranchEngineKind.LuckRoll;
            if (t == AtomicEffectType.BranchEngineCountdown) return BranchEngineKind.Countdown;
            if (t == AtomicEffectType.BranchEngineDeathToll) return BranchEngineKind.DeathToll;
            if (t == AtomicEffectType.BranchEngineManaSurplus) return BranchEngineKind.ManaSurplus;
            if (t == AtomicEffectType.BranchEngineNthHandCard) return BranchEngineKind.NthHandCard;
            return BranchEngineKind.None;
        }

        /// <summary>引擎参数 x 的钳制范围（UI 输入框与运行时判定共用；2026-09-22 新引擎并入）。</summary>
        public static void EngineParamRange(BranchEngineKind kind, out int min, out int max)
        {
            switch (kind)
            {
                case BranchEngineKind.LuckRoll: min = 1; max = 5; break;      // 概率门槛（双 6 才中=1/36）
                case BranchEngineKind.DeathToll: min = 1; max = 9; break;     // 双方合计死亡阈值
                case BranchEngineKind.ManaSurplus: min = 1; max = 9; break;   // bank 最多色阈值
                case BranchEngineKind.NthHandCard: min = 1; max = 9; break;   // 本回合手牌使用序位
                default: min = 0; max = 99; break;                            // Countdown：0=按奖励推导费自动换算
            }
        }

        /// <summary>引擎奖励预算（奖励原子锚价合计上限；2026-09-22 定案=奖励预算制）：死亡计数/元素充盈/手牌序位
        /// 预算=x（EngineParam）；既有三引擎维持无上限自平衡（拼点门槛=锚价、倒计时回合=锚价、运势概率制）→ -1。</summary>
        public static int EngineRewardBudget(BranchEngineKind kind, int engineParam)
        {
            switch (kind)
            {
                case BranchEngineKind.DeathToll:
                case BranchEngineKind.ManaSurplus:
                case BranchEngineKind.NthHandCard:
                    return Math.Max(1, engineParam);
                default:
                    return -1;
            }
        }

        // ======================================== 有限分支（OutcomeGate）目录 ========================================

        /// <summary>有限分支产出条件目录项。Premium（门附加费=奖励预算）从 GatePremium 读——勿双写。
        /// Producers=null 表示**通用门**（2026-09-22 局面状态族）：不读主干产出、任意可作主效果
        /// （表行 MountKinds 含 0）的原子都能挂；非 null 时按产出族过滤。</summary>
        public sealed class GateSpec
        {
            /// <summary>条件 id（评估走 BranchConditionEvaluator，如 "DmgKillsTarget"）。</summary>
            public string Id;
            /// <summary>中文名（UI 显示）。</summary>
            public string DisplayName;
            /// <summary>适用产出族（挂这个门的主干原子 EffectType 名单）；null=通用门。</summary>
            public string[] Producers;
        }

        /// <summary>伤害产出族（可挂「消灭目标时」与改写门）。</summary>
        public static readonly string[] DamageProducers = { "DealDamage", "PierceDamage", "DrainLife" };

        /// <summary>宣言产出族（可挂「宣言命中时」）。</summary>
        public static readonly string[] DeclareProducers = { "DeclareHand", "DeclareDeckTop", "DeclareArrow" };

        /// <summary>用户可拼的产出条件全集。</summary>
        public static readonly GateSpec[] OutcomeGates =
        {
            new GateSpec { Id = "DmgKillsTarget", DisplayName = "消灭目标时", Producers = DamageProducers },
            new GateSpec { Id = "DeclareHit",     DisplayName = "宣言命中时", Producers = DeclareProducers },

            // ---- 局面状态族（2026-09-22 定案：通用门，预算 1，任意主效果原子可挂）----
            new GateSpec { Id = "DrawnInStandbyThisTurn", DisplayName = "本回合准备阶段抽到的卡", Producers = null },
            new GateSpec { Id = "LifeBelowOpp",    DisplayName = "生命值低于对手", Producers = null },
            new GateSpec { Id = "LifeAboveOpp",    DisplayName = "生命值高于对手", Producers = null },
            new GateSpec { Id = "DeckBelowOpp",    DisplayName = "卡组剩余低于对手", Producers = null },
            new GateSpec { Id = "DeckAboveOpp",    DisplayName = "卡组剩余高于对手", Producers = null },
            new GateSpec { Id = "CreaturesBelowOpp", DisplayName = "场上生物低于对手", Producers = null },
            new GateSpec { Id = "CreaturesAboveOpp", DisplayName = "场上生物高于对手", Producers = null },
            new GateSpec { Id = "FirstCardThisTurn", DisplayName = "本回合使用的第一张卡", Producers = null },

            // ---- 局面状态族·二批（2026-09-22 追加，通用门，预算 2）----
            new GateSpec { Id = "LandsGe7",   DisplayName = "操控地数量≥7", Producers = null },
            new GateSpec { Id = "HandEmpty",  DisplayName = "手牌数量为0",  Producers = null },
            new GateSpec { Id = "LifeLe7",    DisplayName = "生命值≤7",     Producers = null },

            // ---- 改写族（2026-09-22 拦截式改写门：伤害不发生改为加指示物，无奖励槽，计价走差价制）----
            new GateSpec { Id = "DmgRewriteToxin",   DisplayName = "伤害改为毒素指示物", Producers = DamageProducers },
            new GateSpec { Id = "DmgRewriteFreeze",  DisplayName = "伤害改为冻结指示物", Producers = DamageProducers },
            new GateSpec { Id = "DmgRewriteSleep",   DisplayName = "伤害改为沉睡指示物", Producers = DamageProducers },
            new GateSpec { Id = "DmgRewriteVenom",   DisplayName = "伤害改为剧毒指示物", Producers = DamageProducers },
        };

        /// <summary>某主干原子可挂的门（产出族匹配门 ∪ 通用门；不在产出族且无通用门 → 空=不可做有限分支主干）。</summary>
        public static IEnumerable<GateSpec> GatesFor(AtomicEffectType trunk)
        {
            string name = trunk.ToString();
            return OutcomeGates.Where(g => g.Producers == null || Array.IndexOf(g.Producers, name) >= 0);
        }

        /// <summary>原子是否可做有限分支主干（产出族成员且非引擎主干）——2026-09-22 起：
        /// 产出族成员 **或** 可作主效果（表行 MountKinds 含 ActiveEffect）且通用门存在的原子皆可
        /// （通用门读局面状态，不依赖主干产出）。</summary>
        public static bool CanBeGateTrunk(AtomicEffectType t)
        {
            if (IsEngineTrunk(t)) return false;
            if (OutcomeGates.Any(g => g.Producers != null && Array.IndexOf(g.Producers, t.ToString()) >= 0))
                return true;
            if (!OutcomeGates.Any(g => g.Producers == null)) return false;
            var row = AtomicEffectTable.GetByEnumName(t.ToString());
            var mounts = MountKindExtensions.ParseCsv(row?.MountKinds ?? "");
            return mounts.Contains(MountKind.ActiveEffect);
        }

        /// <summary>门的显示文本（含【奖励x】——premium 取 GatePremium，与计价同源）；
        /// 改写门无奖励槽，显示改写说明不带预算后缀。</summary>
        public static string GateLabel(GateSpec gate)
        {
            if (BranchConditionEvaluator.IsRewriteCondition(gate.Id))
                return $"{gate.DisplayName}（伤害不发生）";
            int premium = CostDerivationService.GatePremium.TryGetValue(gate.Id, out var p) ? p : 0;
            return $"{gate.DisplayName}【奖励{premium}】";
        }

        /// <summary>门预算（奖励原子推导费上限）：GatePremium 值（灰）；改写门=0（无奖励槽）。</summary>
        public static int GateBudget(GateSpec gate)
            => BranchConditionEvaluator.IsRewriteCondition(gate.Id)
                ? 0
                : (CostDerivationService.GatePremium.TryGetValue(gate.Id, out var p) ? p : 0);
    }
}
