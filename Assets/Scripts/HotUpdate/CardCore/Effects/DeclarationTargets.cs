using System.Collections.Generic;

namespace CardCore
{
    /// <summary>
    /// 声明期目标收集结果（2026-11-14 动态数量配套）：法术/主动技能在发动前逐原子预选目标，
    /// 除平铺目标列表外，额外携带**每个动态数量原子本次选定的目标数**（按 def.Effects 原子下标对齐）。
    /// 消费链：声明期收集 → PlayCard/ActivateEffect（预检/扣费按 base×N）→ 随 cast/效果实例
    /// 透传到执行器（执行按 N 消费预选队列；N 已由收集器写入，执行器不再回头问数量）。
    /// 平铺 Targets 的归属仍沿用既有「预选队列有序消费」口径；DynamicCounts 只是把**
    /// 任意数量档实际选了几个**显式带过声明线，防计价与执行口径漂移。
    /// </summary>
    public sealed class DeclarationTargets
    {
        /// <summary>预选目标平铺列表（顺序=spec 计划问询序=原子执行序）。</summary>
        public List<Entity> Targets = new List<Entity>();

        /// <summary>动态数量：下标=def.Effects 原子序，值=本次所选目标数（0=该原子非动态/未选）。</summary>
        public List<int> DynamicCounts = new List<int>();

        /// <summary>声明期交互参数（2026-11-20 计划 §3.1）：人类声明期收集结果携带的非目标交互
        ///——现阶段=召唤落位计划（PickSummonPlacementAsync 写 SummonCells）；AI 走 LAE 直接写
        /// TideAction.Interaction。随声明线进 PlayCard/ActivateEffect 的 interaction 参数。</summary>
        public InteractionParams Interaction;

        /// <summary>空结果（无交互原子；仍按原子数建好计数表，保证下标对齐）。</summary>
        public static DeclarationTargets Empty(int atomCount)
        {
            var r = new DeclarationTargets();
            for (int i = 0; i < atomCount; i++) r.DynamicCounts.Add(0);
            return r;
        }

        /// <summary>拒发标记（2026-11-20）：无域可声明原子（牺牲/蛰伏）候选为空时，发动只会
        /// 扣费空转——收集口直接拦下并给出原因。**调用方读到 Blocked 必须中止发动**：
        /// 不扣费、不入栈、不消耗耐久（与「玩家取消」同一条中止路径，只是提示文案不同）。</summary>
        public bool Blocked;

        /// <summary>拒发原因（给玩家看的短句；Blocked=false 时为空串）。</summary>
        public string BlockReason = "";

        /// <summary>构造拒发结果（AtomCount 仍需对齐，消费方按 Empty 同口径读）。</summary>
        public static DeclarationTargets Refused(int atomCount, string reason)
        {
            var r = Empty(atomCount);
            r.Blocked = true;
            r.BlockReason = reason ?? "";
            return r;
        }

        /// <summary>写入某原子（或同域共享组的全部原子）的动态数量。</summary>
        public void SetDynamic(int atomIndex, int count, List<int> atomIndices = null)
        {
            void Put(int i)
            {
                if (i < 0) return;
                while (DynamicCounts.Count <= i) DynamicCounts.Add(0);
                DynamicCounts[i] = count;
            }
            if (atomIndices != null && atomIndices.Count > 0)
                foreach (var i in atomIndices) Put(i);
            else Put(atomIndex);
        }

        /// <summary>是否存在已选定数量的动态原子（结算/计价应用口）。</summary>
        public bool HasDynamic
        {
            get
            {
                if (DynamicCounts == null) return false;
                foreach (var c in DynamicCounts) if (c > 0) return true;
                return false;
            }
        }
    }
}
