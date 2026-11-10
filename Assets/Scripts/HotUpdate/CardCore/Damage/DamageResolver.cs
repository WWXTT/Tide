using System;

namespace CardCore.Damage
{
    // =====================================================================================
    // 伤害解析层（2026-11-09《伤害管线与循环链接口草案》Phase 1：抽接口，不换实现）。
    //
    // 本文件只引入解析器契约与请求/结果模型：KeywordRules.ApplyDamage 改为兼容 facade——
    // 经 DamageResolverRegistry 选解析器，默认恒 LegacyDamageResolver（直调原实现
    // ApplyDamageLegacy，行为零漂移）。DamagePipeline / DamageAftermath / DamageChain
    // 与 ChainDamageResolver 由后续阶段（草案 §7.3 步 2-6）经 Registry opt-in 接入；
    // 届时 DamageResult 再扩展 FollowUps/Log 等字段，本阶段保持最小集。
    // =====================================================================================

    /// <summary>伤害类别（草案 §3）。</summary>
    public enum DamageKind
    {
        Combat,      // 战斗伤害
        Effect,      // 效果伤害
        Environment, // 环境/规则伤害
    }

    /// <summary>
    /// 伤害标签（草案 §3）：标记事件在循环链上的身份。Phase 1 仅承载于请求模型，
    /// 消费（OneBounce 拦截、吸血豁免等）随 Aftermath/Chain 阶段落地。
    /// </summary>
    [Flags]
    public enum DamageTags
    {
        None        = 0,
        Retaliate   = 1 << 0, // 报复反噬
        Splash      = 1 << 1, // 溅射
        Overwhelm   = 1 << 2, // 碾压
        Chain       = 1 << 3, // 链上的后续伤害
        NoLifesteal = 1 << 4, // 本事件不触发吸血（备用）
    }

    /// <summary>单次伤害请求（草案 §3）。Origin 供日志与重放：攻击宣言/效果实例/触发事件。</summary>
    public sealed class DamageRequest
    {
        public Entity Source;
        public Entity Target;
        public int Amount;
        public DamageKind Kind;
        public DamageTags Tags;
        public object Origin;
    }

    /// <summary>
    /// 单次伤害结果（草案 §3）。Legacy 包装只能填 Dealt
    ///（旧实现签名只返回实际扣除量，Prevented/Lethal/Fizzled 无法从旧口取得）；
    /// Kind/OldLife 由 Pipeline 填（Aftermath 事件发布用）；Outcome/FollowUps 由 Chain 填。
    /// </summary>
    public sealed class DamageResult
    {
        public Entity Source;
        public Entity FinalTarget;
        public int Requested;
        public int Dealt;
        public int Prevented;
        public bool Lethal;
        public bool Fizzled;
        public int ChainDepth;
        public int OldLife;                 // 角色落血前生命（LifeChangeEvent）
        public DamageKind Kind;             // 战斗/效果（事件链与暴击/剧毒门）
        public ChainOutcome Outcome;        // 整链收束方式（Chain 路径填；Legacy 恒 default）
    }

    /// <summary>
    /// 伤害解析器（草案 §7.1）：ApplyDamage facade 按 Source/Target 在此选实现。
    /// Phase 1 只有 Legacy；ChainDamageResolver 属于 Chain 阶段。
    /// </summary>
    public interface IDamageResolver
    {
        DamageResult Resolve(DamageRequest request);
    }

    /// <summary>
    /// Legacy 解析器：直调 KeywordRules.ApplyDamageLegacy（原 ApplyDamage 实现整段平移），
    /// 守护递归/_retaliateBounceGuard/现有段序全部保持——行为零漂移。
    /// </summary>
    public sealed class LegacyDamageResolver : IDamageResolver
    {
        public static readonly LegacyDamageResolver Instance = new LegacyDamageResolver();

        public DamageResult Resolve(DamageRequest request)
        {
            int dealt = CardCore.Attribute.KeywordRules.ApplyDamageLegacy(
                request.Source, request.Target, request.Amount, request.Kind == DamageKind.Combat);
            return new DamageResult
            {
                Source = request.Source,
                FinalTarget = request.Target,
                Requested = request.Amount,
                Dealt = dealt,
            };
        }
    }

    /// <summary>
    /// 解析器选择（草案 §7.1/§7.2）。**默认恒 Legacy、行为零漂移**（2026-11-10 用户两次裁定：
    /// 默认切换未被批准——测试全绿只证新路径自洽）。Chain 路径仅经 ChainOptIn 显式接线可达；
    /// ChainNativeOptIn（报复/碾压/溅射持有者切链）为预置谓词，接线与否由用户另行拍板，
    /// 测试可临时注入并在 finally 还原为 null。
    /// </summary>
    public static class DamageResolverRegistry
    {
        /// <summary>预置谓词：链原生关键词（报复/碾压/溅射）持有者切链（未接线）。</summary>
        public static readonly Func<Entity, Entity, bool> ChainNativeOptIn =
            (source, target) => HasChainKeyword(source) || HasChainKeyword(target);

        /// <summary>opt-in 判定（source/target 任一命中即走 Chain）。默认 null = 全 Legacy。</summary>
        public static Func<Entity, Entity, bool> ChainOptIn;

        /// <summary>是否链路由（facade 与 CombatSystem 传播边分流共用同一判定）。</summary>
        public static bool IsChainRouted(Entity source, Entity target)
            => ChainOptIn != null && ChainOptIn(source, target);

        public static IDamageResolver For(Entity source, Entity target)
        {
            return IsChainRouted(source, target)
                ? ChainDamageResolver.Instance
                : LegacyDamageResolver.Instance;
        }

        private static bool HasChainKeyword(Entity e)
            => e != null && e.IsAlive
               && (e.HasKeyword(CardCore.Attribute.KeywordRules.Retaliate)
                   || e.HasKeyword(CardCore.Attribute.KeywordRules.Overwhelm)
                   || e.HasKeyword(CardCore.Attribute.KeywordRules.Splash));
    }
}
