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
                target.AddKeyword(_keywordId);
                PublishEvent(new KeywordEvent
                {
                    Target = target,
                    Keyword = _keywordId,
                    IsAdd = true,
                    Duration = DurationType.Permanent,
                    Source = context.Source
                });
            }
        }

        public override string GetDescription(AtomicEffectInstance effect)
        {
            return _description;
        }
    }

    /// <summary>
    /// Grant 关键词处理器工厂
    /// 统一创建所有关键词授予处理器
    /// </summary>
    public static class GrantKeywordHandlerFactory
    {
        /// <summary>
        /// 创建所有关键词授予处理器
        /// </summary>
        public static IAtomicEffectHandler[] CreateAll()
        {
            return new IAtomicEffectHandler[]
            {
                // 红色 - 攻击性
                new GrantKeywordHandler(AtomicEffectType.GrantHaste, "Charge", "获得冲锋"),
                new GrantKeywordHandler(AtomicEffectType.GrantRush, "Rush", "获得突袭"),
                new GrantKeywordHandler(AtomicEffectType.GrantDoubleStrike, "DoubleStrike", "获得连击"),
                new GrantKeywordHandler(AtomicEffectType.GrantFirstStrike, "FirstStrike", "获得先攻"),
                new GrantKeywordHandler(AtomicEffectType.GrantTrample, "Trample", "获得穿透"),
                new GrantKeywordHandler(AtomicEffectType.GrantWindfury, "Windfury", "获得风怒"),
                new GrantKeywordHandler(AtomicEffectType.GrantOverwhelm, "Overwhelm", "获得碾压"),
                new GrantKeywordHandler(AtomicEffectType.GrantMultiAttack, "MultiAttack", "获得多次攻击"),

                // 蓝色 - 规避/控制
                new GrantKeywordHandler(AtomicEffectType.GrantFlying, "Flying", "获得飞行"),
                new GrantKeywordHandler(AtomicEffectType.GrantVigilance, "Vigilance", "获得警戒"),
                new GrantKeywordHandler(AtomicEffectType.GrantStealth, "Stealth", "获得潜行"),
                new GrantKeywordHandler(AtomicEffectType.GrantSpellShield, "SpellShield", "获得法术护盾"),
                new GrantKeywordHandler(AtomicEffectType.GrantGuard, "Guard", "获得守卫"),
                new GrantKeywordHandler(AtomicEffectType.GrantReach, "Reach", "获得阻断飞行"),
                new GrantKeywordHandler(AtomicEffectType.GrantWard, "Ward", "获得守卫"),
                new GrantKeywordHandler(AtomicEffectType.GrantCannotBeTargeted, "Untargetable", "获得不可被指定"),
                new GrantKeywordHandler(AtomicEffectType.GrantImmunity, "Immunity", "获得免疫"),
                new GrantKeywordHandler(AtomicEffectType.GrantUnaffected, "Unaffected", "获得不受影响"),

                // 绿色 - 续航/成长
                new GrantKeywordHandler(AtomicEffectType.GrantLifesteal, "Lifesteal", "获得吸血"),
                new GrantKeywordHandler(AtomicEffectType.GrantRegeneration, "Regeneration", "获得再生"),
                new GrantKeywordHandler(AtomicEffectType.GrantGrowth, "Growth", "获得成长"),
                new GrantKeywordHandler(AtomicEffectType.GrantArmor, "Armor", "获得坚韧"),
                new GrantKeywordHandler(AtomicEffectType.GrantDivineShield, "DivineShield", "获得圣盾"),
                new GrantKeywordHandler(AtomicEffectType.GrantTaunt, "Taunt", "获得嘲讽"),
                new GrantKeywordHandler(AtomicEffectType.GrantPoisonous, "Poisonous", "获得剧毒"),

                // 通用
                new GrantKeywordHandler(AtomicEffectType.RemoveDebuffs, "RemoveDebuffs", "移除减益"),
            };
        }
    }
}
