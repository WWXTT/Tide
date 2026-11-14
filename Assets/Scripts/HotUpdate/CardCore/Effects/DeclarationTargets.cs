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

        /// <summary>空结果（无交互原子；仍按原子数建好计数表，保证下标对齐）。</summary>
        public static DeclarationTargets Empty(int atomCount)
        {
            var r = new DeclarationTargets();
            for (int i = 0; i < atomCount; i++) r.DynamicCounts.Add(0);
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
