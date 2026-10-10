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

                // 参数化（2026-10-07 值化定案；2026-10-08 不叠加；2026-10-09 全轨取代修订）：
                // 实例值=原子 value、生效次数=承载效果次数档（0→1；-1=无限）随授予入台账——
                // 重复授予=全轨取代（值/次数刷新不叠加；顶掉文本份/附加份均算取代）。
                // 运行时消费者已清零：坚韧 2026-10-08 指示物化（ToughnessCounter）、
                // 守护配对制无限次改写——台账 Value/Limit 现仅作不叠加账目，无行为读数。
                target.AddKeyword(_keywordId, lane, context.Source,
                    effect.GetRolledValue(), context.TriggerLimitPerTurn == 0 ? 1 : context.TriggerLimitPerTurn);
                // 守护授予即选（2026-10-08 定案）：落到已在场的卡立即弹选保护目标
                //（印刷入场走 GuardianRules.OnEnterBattlefield 入场事件，不经此口——无双选）。
                if (_keywordId == KeywordRules.Guardian && target is Card grantedCard)
                    GuardianRules.OnGrantedInPlay(grantedCard);
                PublishEvent(new KeywordEvent
                {
                    Target = target,
                    Keyword = _keywordId,
                    IsAdd = true,
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
        // 微缩/放大已删（2026-10-10 照抄炉石机制退役）——临时复制卡族仅余回响（普通效果 EchoCopy，
        // 复制/移除逻辑见 TempCopyRules）。
        private static readonly (AtomicEffectType type, string keywordId, string description)[] Specs =
        {
            // 红色 - 攻击性
            (AtomicEffectType.GrantDoubleStrike, "DoubleStrike", "获得连击"),
            (AtomicEffectType.GrantFirstStrike, "FirstStrike", "获得先攻"),
            (AtomicEffectType.GrantOverwhelm, "Overwhelm", "获得碾压"),
            (AtomicEffectType.GrantDisarm, "Disarm", "获得缴械"),

            // 蓝色 - 规避/控制
            (AtomicEffectType.GrantVigilance, "Vigilance", "获得警戒"),
            // 潜行已移出关键词族（2026-10-08 指示物化）：GrantStealth 原子改由 GrantStealthHandler
            // 执行（挂 StealthCounter 层，EffectExecutionEngine 指示物原子区注册）——refId 已重推
            // a16f1e55→8f84641e（2026-10-08 全量 ID 重推·卡数据随后重建）
            // 隐密（2026-10-04，蓝5）：潜行的持续版——不因发动效果/受到伤害失效（三个失效口只消耗潜行层）
            (AtomicEffectType.GrantConcealed, "Concealed", "获得隐密"),
            // 法术护盾已移出关键词族（2026-10-09 指示物化）：GrantSpellShield 原子改由 GrantSpellShieldHandler
            // 执行（挂 SpellShieldCounter 层，EffectExecutionEngine 指示物原子区注册）——refId 已重推
            // a3ad6a04→7270df35（2026-10-09 指示物化随文案换号）
            (AtomicEffectType.GrantCannotBeTargeted, "Untargetable", "获得扰魔"),

            // 绿色 - 续航
            (AtomicEffectType.GrantLifesteal, "Lifesteal", "获得吸血"),
            (AtomicEffectType.GrantLifelink, "Lifelink", "获得系命"),
            (AtomicEffectType.GrantRegeneration, "Regeneration", "获得再生"),
            // 坚韧已移出关键词族（2026-10-08 指示物化）：GrantToughness 原子（原 GrantArmor 同日更名）
            // 改由 GrantToughnessHandler 执行（挂 ToughnessCounter 层，EffectExecutionEngine 指示物原子区注册）
            //——refId 迁移 05485f65→a312b8b0→758a74e0（同日哈希对齐）→2b1e3700（2026-10-09 生效自减改版随文案换号）
            // 守护（2026-10-08 配对制改版，退出连接光环族——表行位 8 拉黑）：登场/授予时弹选一个
            // 己方目标（单位或角色），其受伤改写为守护者承受（GuardianRules 配对表；无限次直到守护者离场）
            (AtomicEffectType.GrantGuardian, "Guardian", "获得守护"),
            // 圣盾已移出关键词族（2026-10-08 指示物化）：GrantDivineShield 原子改由 GrantDivineShieldHandler
            // 执行（挂 DivineShieldCounter 层）——refId 已重推 feab5d3b→c8624e6a（2026-10-08 全量 ID 重推）
            (AtomicEffectType.GrantTaunt, "Taunt", "获得帷幕"),
            (AtomicEffectType.GrantPoisonSting, "PoisonSting", "获得毒刺（战斗伤害改为毒素）"),
            // 冰晶/梦魇 2026-10-02 裁决表行退役（改写走分支组合）；Specs 保留——印刷关键词路径
            // （Cards.json keywords 字段）与 KeywordRules 改写链仍靠此映射解析。
            (AtomicEffectType.GrantIceCrystal, "IceCrystal", "获得冰晶（战斗伤害改为冻结）"),
            (AtomicEffectType.GrantNightmare, "Nightmare", "获得梦魇（战斗伤害改为沉睡）"),
            // 剧毒（2026-10-08 指示物转关键词·落定追加式）：受到其战斗伤害的生物被消灭；
            // 表行 20c06d52（原 Poison 原子）改挂 GrantVenom——病原体（改写挂剧毒指示物）随之退役
            (AtomicEffectType.GrantVenom, "Venom", "获得剧毒（受到其战斗伤害的生物被消灭）"),
            (AtomicEffectType.GrantSpellban, "Spellban", "获得禁魔石（非战斗伤害为0）"),
            // 复生已移出关键词族（2026-10-08 指示物化）：GrantReborn 原子改由 GrantRebornHandler
            // 执行（挂 RebornCounter 层）——refId 已重推 502be10d→94f6a32d（2026-10-08 全量 ID 重推）
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
