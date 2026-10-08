using System;

namespace CardCore.Attribute
{
    /// <summary>
    /// 属性赋予路由（2026-10-08 用户定案：来源分轨退役——「魔法=永久 / 生物=指示物」是早期
    /// 为突出魔法卡价值做的设定，已删除）。Modify* 族一律**设置轨直写**：永久直改字段
    ///（_power/_maxLife/_baseCost），跨区保留、净化不清（视同本体）；临时性（换区清层）
    /// 由指示物原子族（AddPowerUp/AddPlusOne 等）显式表达，与来源无关。
    /// 连接箭头=光环（不经此路由——LinkAuraSystem live-query，断链即失效）。
    /// </summary>
    public static class StatGrantRouter
    {
        /// <summary>攻击力赋予：直改 _power（跨区保留、净化不清）。</summary>
        public static void ModifyPower(Card target, int amount, Entity source)
        {
            if (target == null || amount == 0) return;

            target._power += amount; // 设置类：永久直改（净化不清——设置后即「原本属性效果」）
        }

        /// <summary>
        /// 生命值赋予：Card 复用 CounterRules.ApplyStatDelta（字段直写+削减归零标死
        /// _pendingDeathSource=source，不挂计数层）；Player（角色=生物单位）正值
        /// IncreaseMaxHealth+回血、负值扣血发 LifeChangeEvent。
        /// </summary>
        public static void ModifyLife(Entity target, int amount, Entity source)
        {
            if (target == null || amount == 0) return;

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
                        amount > 0 ? amount : -amount, source);
                    break;
            }
        }

        /// <summary>
        /// 费用赋予：直改 _baseCost（跨区保留——与「设置费用」同口径；仅手牌生效的临时轨
        /// 是 AddCostUp/AddCostDown 指示物族）。手牌门禁由 handler 的表过滤承担。
        /// </summary>
        public static void ModifyCost(Card target, int amount, Entity source)
        {
            if (target == null || amount == 0) return;

            target._baseCost += amount;
            if (target._baseCost < 0) target._baseCost = 0;
        }
    }
}
