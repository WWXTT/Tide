using System.Collections.Generic;
using CardCore.Attribute;

namespace CardCore.Attribute.Handlers
{
    /// <summary>
    /// 通用授予关键词处理器
    /// 处理所有 GrantXxx 类型效果，将关键词字符串写入目标的 _keywords
    /// </summary>
    public class GrantKeywordHandler : AtomicEffectHandlerBase
    {
        private readonly AtomicEffectType _effectType;
        private readonly string _keywordId;
        private readonly string _description;

        public GrantKeywordHandler(AtomicEffectType effectType, string keywordId, string description)
        {
            _effectType = effectType;
            _keywordId = keywordId;
            _description = description;
            OverrideEffectType = effectType;
        }

        protected override AtomicEffectType DefaultEffectType => _effectType;

        public override void Execute(AtomicEffectInstance effect, EffectExecutionContext context)
        {
            foreach (var target in context.Targets)
            {
                // 关键词三轨（2026-11-07 系统特判）：**效果层关键词=赋予通道（Temp：驱散清/换区清）**——
                // 不论目标是来源自己还是他人、来源是生物还是角色；卡牌组合界面直加的本体关键词走
                // CardData.Keywords→Printed（装载期，免疫驱散），不经本 handler。
                var lane = KeywordLane.Temp;

                // 参数化（2026-10-07 值化定案；2026-10-08 不叠加；2026-10-09 全轨取代修订）：
                // 实例值=原子 value、生效次数=承载效果次数档（0→1；-1=无限）随授予入台账——
                // 重复授予=全轨取代（值/次数刷新不叠加；顶掉文本份/附加份均算取代）。
                // 运行时消费者已清零：坚韧 2026-10-08 指示物化（ToughnessCounter）、
                // 守护配对制无限次改写——台账 Value/Limit 现仅作不叠加账目，无行为读数。
                target.AddKeyword(_keywordId, lane, context.Source,
                    effect.GetRolledValue(), context.TriggerLimitPerTurn == 0 ? 1 : context.TriggerLimitPerTurn);
                // 守护无授予即选（2026-11-05 简化定案）：固定保护己方角色——关键词在场即生效，
                // 无弹选/配对表（GuardianRules 实时查询），授予与印刷同口径无双选。
                // 战斗法师（原舍身；2026-11-01 整链退役注释——见 Specs 战斗法师条）：无状态关键词——无授予即选/无入场钩子
                //（散射在 KeywordRules.ApplyDamage 咽喉内联，MartyrdomRules 配对表整套退役）。
                // EffectTint/IsDebuff（2026-11-01 赋予粒子定案）：原子行基准主色+落点极性——
                // 表现层据此在目标锚点播「极性×颜色」8 种赋予爆发粒子（Heal/Hit 两族）。
                PublishEvent(new KeywordEvent
                {
                    Target = target,
                    Keyword = _keywordId,
                    IsAdd = true,
                    Source = context.Source,
                    EffectTint = effect.AnchorTint(),
                    IsDebuff = GrantKeywordHandlerFactory.IsDebuffKeyword(_keywordId)
                });
            }
        }

        protected override string DescribeTemplate(AtomicEffectInstance effect)
        {
            return _description;
        }
    }

    /// <summary>
    /// Grant 关键词处理器工厂
    /// Specs 是 Grant 原子 → 运行时关键词 id / 中文描述的唯一真相源：
    /// 处理器注册（EffectExecutionEngine）与关键词目录（CardLoader.LoadKeywords）都从这里取值，
    /// 保证目录 id 与写入 IHasKeywords 的字符串一致。
    /// </summary>
    public static class GrantKeywordHandlerFactory
    {
        // (原子效果类型, 运行时关键词 id, 中文描述)
        // 2026-09-03 原子表整体修正：删 Windfury/Guard/MultiAttack/Immunity/Unaffected/Reach/Flying/
        // RemoveDebuffs 八条。
        // 2026-09-08 冲锋/突袭去关键词化：GrantHaste/GrantRush 删除——关键词都是持续性特征，
        // 无「一次性生效后消失」的说法；冲锋/突袭改由登场效果表达（OnPlay+激励自己，突袭另自上紊乱指示物）。
        // 2026-09-11：嘲讽还原（GrantTaunt）；辟邪更名扰魔（运行时 id 仍 Untargetable）；
        // 2026-09-13：嘲讽更名帷幕（只吸引效果目标、不拦攻击；运行时 id 仍 Taunt）；
        // 微缩/放大已删（2026-10-10 照抄炉石机制退役）——临时复制卡族仅余回响（普通效果 EchoCopy，
        // 复制/移除逻辑见 TempCopyRules）。
        private static readonly (AtomicEffectType type, string keywordId, string description)[] Specs =
        {
            // 红色 - 攻击性
            (AtomicEffectType.GrantDoubleStrike, "DoubleStrike", "赋予连击"),
            (AtomicEffectType.GrantFirstStrike, "FirstStrike", "赋予先攻"),
            (AtomicEffectType.GrantOverwhelm, "Overwhelm", "赋予碾压"),
            (AtomicEffectType.GrantSplash, "Splash", "赋予溅射"),
            (AtomicEffectType.GrantDisarm, "Disarm", "赋予缴械"),
            // 报复（2026-11-09 新增，红5）：受到实际伤害时对伤害来源造成等量伤害——
            // 触发口=KeywordRules.ApplyDamage 尾段（荆棘段后、病原体前）；反噬期间重入门
            // 拦报复关键词（等量互弹死循环封口）。与荆棘指示物（原「报复」，2026-11-09 改名）
            // 独立并存：荆棘=层数伤害+生效减半，本条=等量反弹不减。
            (AtomicEffectType.GrantRetaliate, "Retaliate", "赋予报复（受到伤害时，对伤害来源造成等量伤害）"),
            // 暴击（2026-11-09 新增，红5）：造成的战斗伤害翻倍（改写口=ApplyDamage 延时保护后、
            // 防护层前——圣盾/护甲吸收翻倍后的量；效果伤害不翻倍）
            (AtomicEffectType.GrantCrit, "Crit", "赋予暴击（造成的战斗伤害翻倍）"),

            // 蓝色 - 规避/控制
            (AtomicEffectType.GrantVigilance, "Vigilance", "赋予警戒"),
            // 潜行已移出关键词族（2026-10-08 指示物化）：GrantStealth 原子改由 GrantStealthHandler
            // 执行（挂 StealthCounter 层，EffectExecutionEngine 指示物原子区注册）——refId 已重推
            // a16f1e55→8f84641e（2026-10-08 全量 ID 重推·卡数据随后重建）
            // 隐密（2026-10-04，蓝5）：潜行的持续版——不因发动效果/受到伤害失效（三个失效口只消耗潜行层）
            (AtomicEffectType.GrantConcealed, "Concealed", "赋予隐密"),
            // 法术护盾已移出关键词族（2026-10-09 指示物化）：GrantSpellShield 原子改由 GrantSpellShieldHandler
            // 执行（挂 SpellShieldCounter 层，EffectExecutionEngine 指示物原子区注册）——refId 已重推
            // a3ad6a04→7270df35（2026-10-09 指示物化随文案换号）
            // 扰魔（GrantCannotBeTargeted/Untargetable）映射与枚举位已删（2026-10-13 整体退役；枚举收拢 2026-10-30）

            // 绿色 - 续航
            (AtomicEffectType.GrantLifesteal, "Lifesteal", "赋予吸血"),
            (AtomicEffectType.GrantLifelink, "Lifelink", "赋予系命"),
            (AtomicEffectType.GrantRegeneration, "Regeneration", "赋予再生"),
            // 坚韧已移出关键词族（2026-10-08 指示物化）：GrantToughness 原子（原 GrantArmor 同日更名）
            // 改由 GrantToughnessHandler 执行（挂 ToughnessCounter 层，EffectExecutionEngine 指示物原子区注册）
            //——refId 迁移 05485f65→a312b8b0→758a74e0（同日哈希对齐）→2b1e3700（2026-10-09 生效自减改版随文案换号）→a8f7f476（现行号）
            // 守护（2026-11-05 简化定案，退出连接光环族——表行位 7 拉黑）：固定保护己方角色——
            // 角色受伤改写为守护者承受（GuardianRules 实时查询；无限次直到守护者离场；单位不改写）
            (AtomicEffectType.GrantGuardian, "Guardian", "赋予守护"),
            // 战斗法师（原舍身，2026-10-30 改版）2026-11-01 退役注释：表行 c3112c0b（关键词表）已删，
            // 枚举位 GrantBattleMage/关键词常量/ApplyDamage 散射改写/战报播报同批注释——无存量卡引用。
            // (AtomicEffectType.GrantBattleMage, "BattleMage", "赋予战斗法师"),
            // 圣盾已移出关键词族（2026-10-08 指示物化）：GrantDivineShield 原子改由 GrantDivineShieldHandler
            // 执行（挂 DivineShieldCounter 层）——refId 已重推 feab5d3b→c8624e6a（2026-10-08 全量 ID 重推）
            (AtomicEffectType.GrantTaunt, "Taunt", "赋予帷幕"),
            // 毒刺（PoisonSting）/冰晶（IceCrystal）映射已删（2026-10-15 整体退役）：表行 2026-10-02
            // 已先退（改写走分支组合），遗留的印刷关键词路径（改写链臂/两张试作卡）同批清除——
            // 毒素/冻结指示物由各自施加原子独立保留，不经关键词。
            // GrantNightmare（梦魇）映射与枚举位已删（2026-10-13 引擎行为退役；枚举收拢 2026-10-30）
            // 剧毒（2026-10-08 指示物转关键词·落定追加式）：受到其战斗伤害的生物被消灭；
            // 表行 20c06d52（原 Poison 原子）改挂 GrantVenom——病原体（改写挂剧毒指示物）随之退役
            (AtomicEffectType.GrantVenom, "Venom", "赋予剧毒（受到其战斗伤害的生物被消灭）"),
            // 无实体已移出关键词族（2026-11-02 指示物化）：GrantSpellban 原子改由 GrantSpellbanHandler
            // 执行（挂 SpellbanCounter 衰退层，EffectExecutionEngine 指示物原子区注册）——表行 fb88c3f4
            // →6c38edaa 随文案重推，训练卡 C_933347A6 引用同步迁移
            // 绝缘体（2026-10-31，绿3）：持有单位不受指示物的效果影响——特殊裁定改版
            //（同日「不拦指示物添加」）：层照常落账+效果走 GetEffectiveCounterCount 读数压制；
            // 沉睡/缄默（压制型）与 StatKind 属性类（回写式）在 AddCounters 咽喉清零
            //（时序互压/账目安全），系统指示物豁免。表行 MountKinds 带
            //「不可作为连接光环」（免疫洞防线，同守护）。
            (AtomicEffectType.GrantInsulator, "Insulator", "赋予绝缘体（不受指示物的效果影响）"),
            // 病原体（2026-10-31 复活改版，绿5）：受到伤害时对伤害来源附加 1 层毒素——
            // 触发口=KeywordRules.ApplyDamage 管线尾段（实际落定才触发，同荆棘/报复口径；
            // 旧病原体=战斗伤害改写挂剧毒，2026-10-08 退役，本位全新语义复活·id 复用）
            (AtomicEffectType.GrantPathogen, "Pathogen", "赋予病原体（受到伤害时，对伤害来源附加一层毒素）"),
            // 复生已移出关键词族（2026-10-08 指示物化）：GrantReborn 原子改由 GrantRebornHandler
            // 执行（挂 RebornCounter 层）——refId 已重推 502be10d→94f6a32d（2026-10-08 全量 ID 重推）
            (AtomicEffectType.GrantIndestructible, "Indestructible", "赋予不灭"),
            // 狩猎（2026-11-01，绿3）：回合结束时若可行动（未横置），随机对防守方发起一次攻击——
            // 行为口=CombatSystem.ResolveHuntAttacks（GameCore.OnTurnEnded 首位显式调）；
            // 攻击不吃横置代价，战斗计算/事件链与普通攻击同口径
            (AtomicEffectType.GrantHunt, "Hunt", "赋予狩猎（回合结束时，如果处于可行动状态，随机对对手发起一次攻击）"),
        };

        /// <summary>落点为减益的关键词（2026-11-01 赋予粒子极性单源）：与指示物 CounterPolarity
        /// 同视角（对落点是增益还是减益，非表级 Polarity 的施放方受益口径）。当前仅缴械——
        /// 授予敌方单位的削弱类关键词入表；表现层据此分流 Heal/Hit 两族赋予粒子。</summary>
        internal static bool IsDebuffKeyword(string keywordId)
            => keywordId == "Disarm";

        /// <summary>
        /// 创建所有关键词授予处理器
        /// </summary>
        public static IAtomicEffectHandler[] CreateAll()
        {
            var handlers = new IAtomicEffectHandler[Specs.Length];
            for (int i = 0; i < Specs.Length; i++)
                handlers[i] = new GrantKeywordHandler(Specs[i].type, Specs[i].keywordId, Specs[i].description);
            return handlers;
        }

        /// <summary>Grant 原子 → 运行时关键词 id（写入 IHasKeywords 的字符串）。未登记返回 false。</summary>
        public static bool TryGetKeywordId(AtomicEffectType type, out string keywordId)
        {
            foreach (var spec in Specs)
            {
                if (spec.type == type)
                {
                    keywordId = spec.keywordId;
                    return true;
                }
            }
            keywordId = null;
            return false;
        }

        /// <summary>运行时关键词 id → 原子表行 HashId（2026-09-24 关键词引用化：
        /// 卡表 effectIds 直接存原子 refId=本体关键词——写出端经此反查 refId，装载端正向解析）。
        /// 未登记（无 Spec 或表行缺失）返回 false。</summary>
        public static bool TryGetAtomRefId(string keywordId, out string refId)
        {
            refId = null;
            if (string.IsNullOrEmpty(keywordId)) return false;
            foreach (var spec in Specs)
            {
                if (spec.keywordId != keywordId) continue;
                var row = AtomicEffectTable.GetByEnumName(spec.type.ToString());
                if (row == null || string.IsNullOrEmpty(row.HashId)) return false;
                refId = row.HashId;
                return true;
            }
            return false;
        }
    }
}
