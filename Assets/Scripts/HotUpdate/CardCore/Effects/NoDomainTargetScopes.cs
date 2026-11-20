using System.Collections.Generic;

namespace CardCore
{
    /// <summary>无域原子的动作行模式（P0 2026-11-19 无域原子目标）：
    /// Subset=任意子集行（既有 -1/-3 口径，AddSubsetRows）；Prefix=前缀行（k=1..N 取候选前 k 张，
    /// AddPrefixRows——「祭几张」型决策，不枚举 2^N）。</summary>
    public enum TargetRowMode
    {
        Subset,
        Prefix,
    }

    /// <summary>
    /// 无域原子作用域白名单（P0 2026-11-19，docs/AI动作空间补全_修改计划.md §1）：
    /// 牺牲（Sacrifice）/蛰伏（Dormancy）这类原子的作用域写死在 handler（原子表行无 Domain、
    /// TargetKinds 恒空），CollectAtomTargetSpecs 原口径对无域原子直接跳过——导致 LAE 只产一条
    /// Targets=null 行（模型点牺牲=花 2 费空放；蓝蛰伏只能全取不能选张数）。
    /// 本表白名单只开这两个原子：无域且数量档=任意（-1）时按表里的域串产轴解析候选
    ///（TargetAxisRules.TryParseDomain——与 CardEffectConverter 行 Domain 同口），行模式=Prefix。
    /// 未知无域原子保持原跳过（handler 自结算），不静默改行为。
    /// 域串必须用新语法（不能用 TargetKind.OwnLivingUnit 塞 kinds——那个枚举含「己方角色」，
    /// 而牺牲的作用域是「己方战场:生物」（不含角色），把英雄选进牺牲名单是数据错误）。
        /// 候选顺序/排除规则单源在 CardCore.AI.HeroSkillTargetPolicy（LAE 与 GreenRampStrategy 共用）；
        /// 蛰伏的「未横置」过滤也走 policy（池内地牌横置态在 PooledCard 包装上，TargetFilter 的
        /// Untapped token 读 Card._isTapped 对池地无效——本表 filter 恒空）。
        /// 2026-11-20 起声明窗 spec 与拒发闸候选口同走该过滤（EffectHandlerRegistry.NoDomainScopeCandidates）
        /// ——全横置=候选空=拒发不扣费（「没有可横置的地牌无法发动」定案）。
    /// </summary>
    public static class NoDomainTargetScopes
    {
        // 牺牲作用域（SacrificeHandler 头注同口径）：己方战场生物/结界 + 己方元素池卡（地牌入池 2026-11-03 扩定）。
        private const string SacrificeDomain = "己方战场:生物;己方战场:结界;己方元素池:卡";
        // 蛰伏作用域（DormancyHandler 头注同口径）：己方元素池的卡；「未横置」由 OrderDormancy 过滤。
        private const string DormancyDomain = "己方元素池:卡";

        /// <summary>无域原子 → 白名单作用域。命中=true（domain/filter/mode 就绪）；未知原子=false。</summary>
        public static bool TryGet(AtomicEffectType type, out string domain, out string filter, out TargetRowMode mode)
        {
            switch (type)
            {
                case AtomicEffectType.Sacrifice:
                    domain = SacrificeDomain;
                    filter = "";
                    mode = TargetRowMode.Prefix;
                    return true;
                case AtomicEffectType.Dormancy:
                    domain = DormancyDomain;
                    filter = "";
                    mode = TargetRowMode.Prefix;
                    return true;
                default:
                    domain = null;
                    filter = null;
                    mode = TargetRowMode.Subset;
                    return false;
            }
        }

        /// <summary>是否「自行选择目标」的牺牲/蛰伏原子（LAE 展开口 + 声明期闸共用）。
        ///
        /// **语义归属（2026-11-20 用户定案）**：「牺牲/蛰伏没有 Domain、目标由玩家自行选择」是**写在效果里**的
        /// 语义——不是由「数量档 = 任意」这类通用档位规则推出来的。数量档（-1）只是这些效果在当前数据里的
        /// 表达形式，**不是判据来源**；固定数量的牺牲原子（如旧教学 61D64777「牺牲一个」count=1）
        /// **不提供支持**——不做 prefix 行、不做声明期窗口，维持原跳过。
        ///
        /// 必须带 def：原子自身 count 常为 -2（未声明），-1 落在效果级 def.TargetCount——
        /// 有效值按 DeclaredTargetCount(def) 取（-2 回落效果级）。</summary>
        public static bool IsPrefixAtom(AtomicEffectInstance atom, EffectDefinition def)
            => atom != null
               && (atom.Type == AtomicEffectType.Sacrifice || atom.Type == AtomicEffectType.Dormancy)
               && atom.DeclaredTargetCount(def) == -1;
    }
}
