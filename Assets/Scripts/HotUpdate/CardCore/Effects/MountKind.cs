using System;
using System.Linq;

namespace CardCore
{
    /// <summary>
    /// 可装载范围（2026-09-11 定案；2026-10-05 两槽改版全量重排）：
    /// 原子表 MountKinds 列（CSV 多选，同 TargetKinds 惯例）。
    /// 显性化「这个效果能以什么形式/落到谁身上」——取代用作用范围(TargetKinds)反推挂载形态的隐式口径。
    ///
    /// 2026-10-05 清理重排（效果库已清空、存储形状整体破弃，墓碑实删不留空洞）：
    /// - 删值 3（BranchTrunk）：新有限分支=局面状态门（与原子产出无关），任意原子可挂，主干资格位无意义；
    ///   产出挂钩条件（伤害击杀 Then xx）改归自由分支产出条件族（BranchPayload.Settle=Outcome）。
    /// - 删值 5/6（GrantOnCreature/GrantOnSpell）："可赋予生物/法术"是目标侧语义，组合阶段无法经挂载位区分
    ///   ——改由表行 TargetFilter token（Creature/Spell）表达。
    /// - 删值 9（FreeBranchTrunk）：引擎主干不再是原子（自由分支条件=槽级 BranchPayload 载荷），
    ///   表内 6 行引擎主干行随之删除。
    /// 其余值按序重排为 0..7；表 JSON 的 MountKinds CSV 已同步重写。
    /// </summary>
    public enum MountKind
    {
        /// <summary>效果栏挂载（登场/触发/启动式皆走此位——效果栏原子的通用准入，非"启动式资格"）。
        /// 启动式资格另受关键词校验：纯自指 Grant 行（关键词型）不得作启动式原子（2026-10-02 定案，
        /// 见 ComposerCatalog.IsKeywordStyleGrant——判定按目标域，不占本位语义）。</summary>
        ActiveEffect = 0,
        /// <summary>可作为关键词（卡面自带印刷，如剧毒/嘲讽/回响）</summary>
        Keyword = 1,
        /// <summary>可作为指示物（效果=赋予指示物，指示物承载持续规则）</summary>
        Counter = 2,
        /// <summary>可加入分支（作为条件达成的 Then 奖励原子，挂槽级 BranchPayload）</summary>
        BranchReward = 3,
        /// <summary>可挂载随机（2026-09-13 定案）：构筑期可配数值随机幅度（RandomAmplitude）——
        /// 已开放：伤害族（造成/穿透/吸取/流失）+ 治疗族 + 限制型指示物（紊乱/冻结/沉睡，层数随机）</summary>
        RandomMount = 4,
        /// <summary>触发上限不可修改（2026-09-13 定案，**少数**）：挂此位的原子以触发式结算时
        /// **无每回合触发上限、组合期不可加限**——按触发次数线性堆价值的效果（如坚韧：受伤-1/次）。
        /// 其余原子默认**可修改且默认上限=一回合一次**（组合层 TriggerLimitPerTurn 可调 N 或显式无限）。</summary>
        TriggerCapImmutable = 5,
        /// <summary>可作连接光环条目（2026-09-23 定案）：Grant 行声明该位才可作为光环关键词（LinkAuraData.keyword）。
        /// **消耗型关键词（圣盾/复生/潜行/法术护盾——移除即用掉）不声明此位**：与光环 live-query 持续语义冲突
        /// （不物化 → RemoveKeyword 空操作 → 等效永久持有）。合成期校验+装载期拦截均读本位，不硬编码名单。
        /// 2026-10-05 坚韧/守护入表（GrantArmor/GrantGuardian 行）——代码特判放行退役，全走本位。</summary>
        LinkAura = 6,
        /// <summary>系统内部用（2026-10-04 定案）：声明该位的行**移出玩家合成器全部选择面**
        /// （效果合成器原子库/分类、卡合成器代价候选）——引擎执行/计价代表原子/装载照常运转。
        /// 首批：修改攻击力/修改生命值/修改费用（数值修改原语，系统侧计价与运行时消费）。</summary>
        SystemInternal = 7,
        /// <summary>引擎主干行（2026-10-05 回表定案）：六引擎行（倒计时/运势/拼点/死亡计数/元素充盈/手牌序位）
        /// 的填充资格位——填入槽即自由分支（无 gate-row，直接引擎参数+奖励槽）；行为条件载体：
        /// 零锚价、零域、不入主序列执行、无处理器。与位 0（ActiveEffect）互斥——主干槽二选一路径。</summary>
        EngineTrunk = 8,
        /// <summary>规则光环族（2026-10-05 统一定案）：规则仪典行统一标记位——9 行双方生效仪典（三相/血偿/
        /// 丰盈/离散/窥渊/归土/疾风/轮回/纳川）+ 4 行战斗改写仪典（毒蚀/霜蚀/眠蚀/疫蚀——仅持有者生效）。
        /// 光环模式库与形态识别（IsRuleAuraRow/HasRuleAuraStep/InferMode）读此位驱动，取代代码侧
        /// DisplayName 字典识别；str 载荷短名映射（RuleAuraIds）仍由代码维护。与位 6（LinkAura 连接条目）
        /// 互斥——规则光环走 steps 原子步（ModifyGameRule），不占连接位、无箭头。</summary>
        RuleAura = 9,
    }

    /// <summary>MountKinds CSV 解析/判定辅助（AtomicEffectTable 与装载校验共用）。</summary>
    public static class MountKindExtensions
    {
        /// <summary>成员中文名（2026-10-06 表列中文化定案：原子表开放给玩家，MountKinds 列写中文 CSV；
        /// 下标=枚举序，与枚举成员一一对应，新增成员须同步补名）。</summary>
        private static readonly string[] ZhNames =
        {
            "主动",       // 0 ActiveEffect
            "关键词",     // 1 Keyword
            "指示物",     // 2 Counter
            "分支奖励",   // 3 BranchReward
            "随机幅度",   // 4 RandomMount
            "上限锁定",   // 5 TriggerCapImmutable
            "连接光环",   // 6 LinkAura
            "系统",       // 7 SystemInternal
            "引擎主干",   // 8 EngineTrunk
            "规则光环",   // 9 RuleAura
        };

        /// <summary>成员中文名（越界回退枚举名）。</summary>
        public static string ZhNameOf(MountKind kind)
        {
            int i = (int)kind;
            return i >= 0 && i < ZhNames.Length ? ZhNames[i] : kind.ToString();
        }

        /// <summary>枚举集 → 中文 CSV（升序去重，兜底字面量/展示用；持久哈希口径仍走数字）。</summary>
        public static string ZhCsv(System.Collections.Generic.IEnumerable<MountKind> kinds)
            => kinds == null ? "" : string.Join(",",
                kinds.Distinct().OrderBy(k => k).Select(ZhNameOf));

        /// <summary>解析 CSV（双轨：中文优先，序号数字回退——2026-10-06 表数据已迁中文，数字轨
        /// 供验证器夹具/过渡数据兼容）。"主动,分支奖励" 或 "0,3" → {ActiveEffect, BranchReward}；
        /// 空串=空集=未声明，消费方自行兜底。</summary>
        public static System.Collections.Generic.HashSet<MountKind> ParseCsv(string csv)
        {
            var set = new System.Collections.Generic.HashSet<MountKind>();
            if (string.IsNullOrEmpty(csv)) return set;
            foreach (var tok in csv.Split(','))
            {
                var t = tok.Trim();
                if (t.Length == 0) continue;
                int zh = System.Array.IndexOf(ZhNames, t);
                if (zh >= 0 && Enum.IsDefined(typeof(MountKind), zh))
                {
                    set.Add((MountKind)zh);
                    continue;
                }
                if (int.TryParse(t, out int v) && Enum.IsDefined(typeof(MountKind), v))
                    set.Add((MountKind)v);
            }
            return set;
        }
    }
}
