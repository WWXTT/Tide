using System;

namespace CardCore.Attribute
{
    /// <summary>
    /// 赋予族三轨（2026-09-09 定案）：同文本（属性增加/赋予关键词等）按【来源分轨】——
    /// 生物（场上卡）=指示物（永久赋予=Permanent 层换区不清 / 非永久=换区清层）；
    /// 魔法卡=设置类（来源=角色 Player，永久直改，跨区保留、净化不清，视同本体）；
    /// 连接箭头=光环（不经此路由——LinkAuraSystem live-query，断链即失效）。
    /// </summary>
    public enum StatGrantTrack
    {
        /// <summary>生物轨：指示物承载（Duration=Permanent 路由到 Permanent 层 id，其余换区清层 id）</summary>
        TempCounter,
        /// <summary>设置轨：永久直改字段（_power/_maxLife/_baseCost），不挂计数层</summary>
        SettingDirect,
    }

    /// <summary>
    /// 属性赋予三轨路由咽喉——ModifyPower/ModifyLife/ModifyCost 三族 handler 的唯一落点。
    /// 事件发布留在各 handler（保持既有事件形状与 Old/New 快照口径）。
    /// 光环轨（连接箭头）不在此：卡面 linkAuras 经 LinkAuraSystem 实时查询，无一次性赋予动作。
    /// </summary>
    public static class StatGrantRouter
    {
        /// <summary>
        /// 轨道判定：来源是 Player（=魔法卡发动，来源归因定案规则②）→ 设置轨；
        /// 其余（场上卡：生物/结界/地牌的登场·触发·启动式效果）→ 指示物轨。
        /// </summary>
        public static StatGrantTrack ResolveTrack(Entity source)
            => source is Player ? StatGrantTrack.SettingDirect : StatGrantTrack.TempCounter;

        /// <summary>
        /// 攻击力赋予：设置轨直改 _power（跨区保留）；生物轨按 duration 分流
        /// Permanent 层（PowerUpPermanent，换区不清）/ 换区清层（PowerUp）。
        /// </summary>
        public static void ModifyPower(Card target, int amount, Entity source, DurationType duration)
        {
            if (target == null || amount == 0) return;

            if (ResolveTrack(source) == StatGrantTrack.SettingDirect)
            {
                target._power += amount; // 设置类：永久直改（净化不清——设置后即「原本属性效果」）
                return;
            }

            string id = duration == DurationType.Permanent
                ? (amount > 0 ? CounterRules.PowerUpPermanentCounter : CounterRules.PowerDownPermanentCounter)
                : (amount > 0 ? CounterRules.PowerUpCounter : CounterRules.PowerDownCounter);
            CounterRules.AddStatCounter(target, id, Math.Abs(amount), source);
        }

        /// <summary>
        /// 生命值赋予：设置轨——Card 直改上限与当前（复用 CounterRules.ApplyStatDelta：
        /// 字段直写+削减归零标死 _pendingDeathSource=source，不挂计数层）；Player（角色=生物单位）
        /// 正值 IncreaseMaxHealth+回血、负值扣血发 LifeChangeEvent。生物轨按 duration 分流
        /// Permanent 层 / 换区清层。
        /// </summary>
        public static void ModifyLife(Entity target, int amount, Entity source, DurationType duration)
        {
            if (target == null || amount == 0) return;

            if (ResolveTrack(source) == StatGrantTrack.SettingDirect)
            {
                switch (target)
                {
                    case Player player:
                        if (amount > 0)
                        {
                            player.IncreaseMaxHealth(amount);
                            player.Life += amount;
                        }
                        else
                        {
                            int oldLife = player.Life;
                            player.Life = Math.Max(0, player.Life + amount);
                            EventManager.Instance.Publish(new LifeChangeEvent
                            {
                                Player = player,
                                OldLife = oldLife,
                                NewLife = player.Life,
                                Source = source,
                            });
                        }
                        break;
                    case Card card:
                        CounterRules.ApplyStatDelta(card,
                            amount > 0 ? StatCounterKind.LifeUp : StatCounterKind.LifeDown,
                            Math.Abs(amount), source);
                        break;
                }
                return;
            }

            if (target is Card c)
            {
                string id = duration == DurationType.Permanent
                    ? (amount > 0 ? CounterRules.LifeUpPermanentCounter : CounterRules.LifeDownPermanentCounter)
                    : (amount > 0 ? CounterRules.LifeUpCounter : CounterRules.LifeDownCounter);
                CounterRules.AddStatCounter(c, id, Math.Abs(amount), source);
            }
        }

        /// <summary>
        /// 费用赋予：设置轨直改 _baseCost（跨区保留——与「设置费用」同口径，区别于费用指示物的仅手牌）；
        /// 生物轨恒走换区清层（CostUp/CostDown——费用指示物语义=仅手牌生效、离手消失，
        /// Permanent 档对费用无意义：永久改费只存在于设置轨）。手牌门禁由 handler 的表过滤承担。
        /// </summary>
        public static void ModifyCost(Card target, int amount, Entity source, DurationType duration)
        {
            if (target == null || amount == 0) return;

            if (ResolveTrack(source) == StatGrantTrack.SettingDirect)
            {
                target._baseCost += amount;
                if (target._baseCost < 0) target._baseCost = 0;
                return;
            }

            CounterRules.AddStatCounter(target,
                amount > 0 ? CounterRules.CostUpCounter : CounterRules.CostDownCounter,
                Math.Abs(amount), source);
        }
    }
}
