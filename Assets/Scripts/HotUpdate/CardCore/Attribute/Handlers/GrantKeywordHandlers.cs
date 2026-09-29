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
                // 判轨（2026-09-09 三轨制；2026-09-13 修订；2026-09-14 用户定案三分）：
                // ①角色来源（魔法/英雄——来源归因=Player）→ Setting（视同本体，按声明持续）；
                // ②**目标=来源卡自己** → Setting（**文本效果轨**——自赋予视同印制文本，永久：
                //「赋予」给别人才是 1 回合增益）；
                // ③其余（生物→别人的单位目标）→ Temp 固定 1 回合（回合末 GameCore 清 Temp 轨；
                // 光环 linkAuras 不经此口）。
                var lane = context.Source is Player || ReferenceEquals(target, context.Source)
                    ? KeywordLane.Setting
                    : KeywordLane.Temp;

                target.AddKeyword(_keywordId, lane, context.Source);
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
        // RemoveDebuffs 八条；GrantPoisonous→GrantPoisonSting（毒刺：对受战斗伤害目标附加毒素指示物）。
        // 2026-09-08 冲锋/突袭去关键词化：GrantHaste/GrantRush 删除——关键词都是持续性特征，
        // 无「一次性生效后消失」的说法；冲锋/突袭改由登场效果表达（OnPlay+激励自己，突袭另自上紊乱指示物）。
        // 2026-09-11：嘲讽还原（GrantTaunt）；辟邪更名扰魔（运行时 id 仍 Untargetable）；
        // 2026-09-13：嘲讽更名帷幕（只吸引效果目标、不拦攻击；运行时 id 仍 Taunt）；
        // 新增微缩/放大/回响（临时复制卡族，行为见 TempCopyRules，装载范围见原子表 MountKinds 列）。
        private static readonly (AtomicEffectType type, string keywordId, string description)[] Specs =
        {
            // 红色 - 攻击性
            (AtomicEffectType.GrantDoubleStrike, "DoubleStrike", "获得连击"),
            (AtomicEffectType.GrantFirstStrike, "FirstStrike", "获得先攻"),
            (AtomicEffectType.GrantOverwhelm, "Overwhelm", "获得碾压"),
            (AtomicEffectType.GrantDisarm, "Disarm", "获得缴械"),

            // 蓝色 - 规避/控制
            (AtomicEffectType.GrantVigilance, "Vigilance", "获得警戒"),
            (AtomicEffectType.GrantStealth, "Stealth", "获得潜行"),
            (AtomicEffectType.GrantSpellShield, "SpellShield", "获得法术护盾"),
            (AtomicEffectType.GrantCannotBeTargeted, "Untargetable", "获得扰魔"),

            // 绿色 - 续航/成长
            (AtomicEffectType.GrantLifesteal, "Lifesteal", "获得吸血"),
            (AtomicEffectType.GrantLifelink, "Lifelink", "获得系命"),
            (AtomicEffectType.GrantRegeneration, "Regeneration", "获得再生"),
            (AtomicEffectType.GrantGrowth, "Growth", "获得成长"),
            (AtomicEffectType.GrantArmor, "Armor", "获得坚韧"),
            (AtomicEffectType.GrantDivineShield, "DivineShield", "获得圣盾"),
            (AtomicEffectType.GrantTaunt, "Taunt", "获得帷幕"),
            (AtomicEffectType.GrantPoisonSting, "PoisonSting", "获得毒刺（战斗伤害改为毒素）"),
            (AtomicEffectType.GrantIceCrystal, "IceCrystal", "获得冰晶（战斗伤害改为冻结）"),
            (AtomicEffectType.GrantNightmare, "Nightmare", "获得梦魇（战斗伤害改为沉睡）"),
            (AtomicEffectType.GrantPathogen, "Pathogen", "获得病原体（战斗伤害改为剧毒）"),
            (AtomicEffectType.GrantSpellban, "Spellban", "获得禁魔石（非战斗伤害为0）"),
            (AtomicEffectType.GrantReborn, "Reborn", "获得复生"),
            (AtomicEffectType.GrantIndestructible, "Indestructible", "获得不灭"),

            // 临时复制卡族（2026-09-11；回响=瞬间法术自带/可赋予法术）
            (AtomicEffectType.GrantMiniature, "Miniature", "获得微缩"),
            (AtomicEffectType.GrantMagnify, "Magnify", "获得放大"),
            (AtomicEffectType.GrantEcho, "Echo", "获得回响"),
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
