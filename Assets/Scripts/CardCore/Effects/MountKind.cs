using System;

namespace CardCore
{
    /// <summary>
    /// 可装载范围（2026-09-11 定案）：原子表新增 MountKinds 列（CSV 多选，同 TargetKinds 惯例）。
    /// 显性化「这个效果能以什么形式/落到谁身上」——取代此前用作用范围(TargetKinds)反推
    /// 主动效果/关键词/指示物/分支位置的隐式口径。值只可尾部追加。
    /// </summary>
    public enum MountKind
    {
        /// <summary>可作为主动效果（效果栏原子：登场/触发/启动式）</summary>
        ActiveEffect = 0,
        /// <summary>可作为关键词（卡面自带印刷，如剧毒/嘲讽/回响）</summary>
        Keyword = 1,
        /// <summary>可作为指示物（效果=赋予指示物，指示物承载持续规则）</summary>
        Counter = 2,
        /// <summary>可作为条件分支主干（产出可被 OutcomeGate 条件观测：伤害/治疗/宣言/预言族）</summary>
        BranchTrunk = 3,
        /// <summary>可加入分支（作为条件达成的奖励原子）</summary>
        BranchReward = 4,
        /// <summary>可赋予生物（Grant 类授予的合法目标）</summary>
        GrantOnCreature = 5,
        /// <summary>可赋予法术（回响等法术侧关键词）</summary>
        GrantOnSpell = 6,
        /// <summary>可挂载随机（2026-09-13 定案）：构筑期可配数值随机幅度（RandomAmplitude）——
        /// 已开放：伤害族（造成/穿透/吸取/流失）+ 治疗族 + 限制型指示物（紊乱/冻结/沉睡，层数随机）</summary>
        RandomMount = 7,
        /// <summary>触发上限不可修改（2026-09-13 定案，**少数**）：挂此位的原子以触发式结算时
        /// **无每回合触发上限、组合期不可加限**——按触发次数线性堆价值的效果（如坚韧：受伤-1/次）。
        /// 其余原子默认**可修改且默认上限=一回合一次**（组合层 TriggerLimitPerTurn 可调 N 或显式无限）。</summary>
        TriggerCapImmutable = 8,
    }

    /// <summary>MountKinds CSV 解析/判定辅助（AtomicEffectTable 与装载校验共用）。</summary>
    public static class MountKindExtensions
    {
        /// <summary>解析 CSV（"0,4" → {ActiveEffect, BranchReward}；空串=空集=未声明，消费方自行兜底）</summary>
        public static System.Collections.Generic.HashSet<MountKind> ParseCsv(string csv)
        {
            var set = new System.Collections.Generic.HashSet<MountKind>();
            if (string.IsNullOrEmpty(csv)) return set;
            foreach (var tok in csv.Split(','))
            {
                if (int.TryParse(tok.Trim(), out int v) && Enum.IsDefined(typeof(MountKind), v))
                    set.Add((MountKind)v);
            }
            return set;
        }
    }
}
