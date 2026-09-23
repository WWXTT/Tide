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
        /// <summary>自由分支主干（2026-09-14 合成器重做）：引擎型条件条目（拼点/运势/倒计时）——
        /// 只可落入合成器「主干槽」（写 header.EngineKind/EngineParam），不可作普通原子/奖励/payload 挂载。
        /// 值只可尾部追加（同 TargetKind 惯例）。</summary>
        FreeBranchTrunk = 9,
        /// <summary>可作连接光环条目（2026-09-23 定案）：Grant 行声明该位才可作为光环关键词（LinkAuraData.keyword）。
        /// **消耗型关键词（圣盾/复生/潜行/法术护盾——移除即用掉）不声明此位**：与光环 live-query 持续语义冲突
        /// （不物化 → RemoveKeyword 空操作 → 等效永久持有）。合成期校验+装载期拦截均读本位，不硬编码名单。
        /// 坚韧(Armor)/守护(Guardian) 无表行（Grant 行已退役、光环本体）——代码特判放行。</summary>
        LinkAura = 10,
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
