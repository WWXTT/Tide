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
    /// Specs 是 Grant 原子 → 运行时关键词 id / 中文描述的唯一真相源：
    /// 处理器注册（EffectExecutionEngine）与关键词目录（CardLoader.LoadKeywords）都从这里取值，
    /// 保证目录 id 与写入 IHasKeywords 的字符串一致。
    /// </summary>
    public static class GrantKeywordHandlerFactory
    {
        // (原子效果类型, 运行时关键词 id, 中文描述)
        // 2026-09-03 原子表整体修正：删 Windfury/Guard/MultiAttack/Immunity/Unaffected/Reach/Flying/
        // RemoveDebuffs 八条；GrantPoisonous→GrantPoisonSting（毒刺：对受战斗伤害目标附加毒素指示物）。
        // 现役关键词 19 个 + 缴械。
        private static readonly (AtomicEffectType type, string keywordId, string description)[] Specs =
        {
            // 红色 - 攻击性
            (AtomicEffectType.GrantHaste, "Charge", "获得冲锋"),
            (AtomicEffectType.GrantRush, "Rush", "获得突袭"),
            (AtomicEffectType.GrantDoubleStrike, "DoubleStrike", "获得连击"),
            (AtomicEffectType.GrantFirstStrike, "FirstStrike", "获得先攻"),
            (AtomicEffectType.GrantOverwhelm, "Overwhelm", "获得碾压"),
            (AtomicEffectType.GrantDisarm, "Disarm", "获得缴械"),

            // 蓝色 - 规避/控制
            (AtomicEffectType.GrantVigilance, "Vigilance", "获得警戒"),
            (AtomicEffectType.GrantStealth, "Stealth", "获得潜行"),
            (AtomicEffectType.GrantSpellShield, "SpellShield", "获得法术护盾"),
            (AtomicEffectType.GrantCannotBeTargeted, "Untargetable", "获得不可被指定"),

            // 绿色 - 续航/成长
            (AtomicEffectType.GrantLifesteal, "Lifesteal", "获得吸血"),
            (AtomicEffectType.GrantLifelink, "Lifelink", "获得系命"),
            (AtomicEffectType.GrantRegeneration, "Regeneration", "获得再生"),
            (AtomicEffectType.GrantGrowth, "Growth", "获得成长"),
            (AtomicEffectType.GrantArmor, "Armor", "获得坚韧"),
            (AtomicEffectType.GrantDivineShield, "DivineShield", "获得圣盾"),
            (AtomicEffectType.GrantTaunt, "Taunt", "获得嘲讽"),
            (AtomicEffectType.GrantPoisonSting, "PoisonSting", "获得毒刺"),
            (AtomicEffectType.GrantReborn, "Reborn", "获得复生"),
            (AtomicEffectType.GrantIndestructible, "Indestructible", "获得不灭"),
        };

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
    }
}
