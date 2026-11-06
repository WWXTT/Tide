namespace CardCore.Attribute
{
    /// <summary>
    /// 守护（2026-11-05 简化定案）：**固定保护己方角色**——角色受到伤害时改写为自己受到等量伤害。
    /// 无状态：关键词在场即生效——无弹选、无配对表、无事件订阅。2026-10-08 配对制（登场弹选
    /// 任意己方目标、单位亦可被守护）整体退役；更早三形态（箭头光环改写/关键词扫场护角色/
    /// 关键词挂角色空转）此前已退役。
    ///
    /// 生效判据（查询时实时校验，懒清扫）：
    /// - 守护者=该角色战场上的 Guardian 持有卡，取首个（存活 && 在场 && 未被自动效果压制——
    ///   沉睡/失效，统一口 SuppressRules）；
    /// - 无限次改写直到守护者死亡/离场——离场即被扫描自然跳过（无状态无需断链），
    ///   墓地复活即恢复保护；复生原地留场同理保持；
    /// - 改写在 KeywordRules.ApplyDamage 咽喉单跳（guardRerouted 防链式）；
    ///   单位受伤不在改写面（域收窄=仅角色）。
    /// </summary>
    public static class GuardianRules
    {
        /// <summary>反查：角色当前的有效守护者（无则 null）。改写咽喉（KeywordRules.ApplyDamage）调用。</summary>
        public static Card FindGuardian(Entity ward)
        {
            if (!(ward is Player hero)) return null; // 固定被守护对象=角色（2026-11-05 简化：单位受伤不改写）
            var zm = CardCore.GameCore.Instance?.ZoneManager;
            var cards = zm?.GetCards(hero, Zone.Battlefield);
            if (cards == null) return null;
            foreach (var c in cards)
            {
                if (c == null || !c.IsAlive) continue;
                if (!c.HasKeyword(KeywordRules.Guardian)) continue;
                if (SuppressRules.IsAutoSuppressed(c)) continue; // 沉睡/失效=自动效果压制口（2026-10-31 统一读数）
                return c;
            }
            return null;
        }

        /// <summary>正查：守护者保护的固定对象（无则 null）。状态段书写用（2026-10-16）——
        /// 固定=自身控制器角色；不在场/无关键词回 null（状态段不残留）。</summary>
        public static Entity FindWard(Card guardian)
        {
            if (guardian == null || !guardian.IsAlive) return null;
            if (guardian.GetZone() != Zone.Battlefield) return null;
            return guardian.HasKeyword(KeywordRules.Guardian) ? guardian.GetController() : null;
        }
    }
}
