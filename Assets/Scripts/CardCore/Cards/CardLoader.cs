using System;
using System.Collections.Generic;
using UnityEngine;
using CardCore.Attribute;
using CardCore.Attribute.Handlers;
using System.Linq;

namespace CardCore
{
    // ======================================== JSON 数据结构 ========================================

    /// <summary>
    /// 关键词定义（运行时由原子效果表的 Grant* 条目合成，见 CardLoader.LoadKeywords）
    /// </summary>
    [Serializable]
    public class KeywordDefinition
    {
        public string id;
        public string nameZh;
        public string nameEn;
        public string color;
        public string description;
        public bool isPassive;
        public string atomicEffect;     // 被动授予的关键词原子效果（GrantXxx）
        public string triggerTiming;    // 触发式：TriggerTiming 枚举名，空=被动
        public string triggeredEffect;  // 触发时执行的原子效果（区别于 atomicEffect 的授予）
        public int value;               // 触发效果数值
        public int value2;              // 触发效果第二数值
        public string duration;         // DurationType 枚举名
    }

    /// <summary>
    /// 卡牌配置条目（JSON 中单张卡的数据）
    /// </summary>
    [Serializable]
    public class CardConfigEntry
    {
        public string id;
        public string cardName;
        public string supertype;
        public int power;
        public int life;
        public List<CostJsonEntry> costList;
        public List<string> keywords;
        public List<string> tags;
        public List<CardEffectData> effects;

        // 子类型 / 扩展字段（缺省 → 旧行为，向后兼容）
        public string subtype;     // 逗号分隔的 CardSubtype Flags 名，如 "Dragon"
        public int level = -1;     // 等级，-1 = 无
        public string arrows;      // 逗号分隔的 HexDirection Flags 名，如 "Up,LowerRight"

        // 效果引用（2026-09-14 效果引用化）：效果定义统一在 Tide/Effects.json（EffectsLibrary），
        // 卡表只存 id（ContentHasher.HashEffect 8 位 hex）。与内嵌 effects 双格式并存——
        // effectIds 非空优先（新格式）；TestDecks 旧夹具继续走内嵌。
        public List<string> effectIds;

        // 连接光环声明（三轨制 2026-09-09）：箭头指向格占据者享受的持续效果（stat/keyword 二选一）
        public List<LinkAuraData> linkAuras;

        // 战斗底盘（2026-09-10 攻/守效果化）：opt-out 缺省 false=自带攻守；瞬间富余转速度缺省 false=退费
        public bool noAttack;
        public bool noGuard;
        public bool surplusToSpeed;
    }

    /// <summary>
    /// 费用 JSON 条目
    /// </summary>
    [Serializable]
    public class CostJsonEntry
    {
        public int manaType;
        public float amount;
    }

    /// <summary>
    /// 卡组配置
    /// </summary>
    [Serializable]
    public class DeckConfig
    {
        public int copiesPerCard = 3;
    }

    /// <summary>
    /// 测试卡牌配置包装
    /// </summary>
    [Serializable]
    public class TestCardsConfigWrapper
    {
        public List<CardConfigEntry> cards;
        public DeckConfig deckConfig;
    }

    // ======================================== 卡牌加载器 ========================================

    /// <summary>
    /// 卡牌加载器 - 从 JSON 配置构建 CardData 和测试卡组
    /// </summary>
    public static class CardLoader
    {
        private static Dictionary<string, KeywordDefinition> _keywordCache;

        /// <summary>
        /// 加载关键词定义 —— 关键词本身即原子效果（GrantXxx，作用默认指向自身），不单独开表：
        /// 直接从原子效果表（AtomicEffectTable ← AttributeValueConfig.json）的 Grant* 条目合成。
        /// 中文名/描述/颜色取自表内字段；id 取 GrantKeywordHandlerFactory 登记的运行时关键词 id
        /// （与写入 IHasKeywords 的字符串同源，如 GrantReborn → Reborn），未登记退化为去 Grant 前缀。
        /// 触发式关键词（triggerTiming）表内暂无来源，统一按被动处理。
        /// （2026-09-08 冲锋/突袭已去关键词化：GrantHaste/GrantRush 删除，改由登场效果表达。）
        /// </summary>
        public static Dictionary<string, KeywordDefinition> LoadKeywords()
        {
            if (_keywordCache != null) return _keywordCache;

            _keywordCache = new Dictionary<string, KeywordDefinition>();
            foreach (var atom in AtomicEffectTable.GetAll())
            {
                // EnumName = 英文枚举名（GrantXxx）；Grant 前缀 = 可作为关键词授予的原子
                if (atom == null || string.IsNullOrEmpty(atom.EnumName) || !atom.EnumName.StartsWith("Grant"))
                    continue;
                if (!Enum.TryParse<AtomicEffectType>(atom.EnumName, out var type))
                    continue;

                if (!GrantKeywordHandlerFactory.TryGetKeywordId(type, out var keywordId))
                    keywordId = atom.EnumName.Substring("Grant".Length);

                _keywordCache[keywordId] = new KeywordDefinition
                {
                    id = keywordId,
                    nameZh = atom.DisplayName,      // 中文短名（冲锋/突袭…）
                    nameEn = atom.EnumName,
                    color = ElementAffinities.GetAffinityForEffect(type).PrimaryColor.ToString(), // 源自表 EffectColor
                    description = atom.Description, // 展示模板（{target}获得冲锋）
                    isPassive = true,
                    atomicEffect = atom.EnumName,   // 被动授予的原子效果（GrantXxx）
                };
            }
            return _keywordCache;
        }

        /// <summary>
        /// 获取已加载的关键词定义
        /// </summary>
        public static KeywordDefinition GetKeywordDefinition(string keywordId)
        {
            if (_keywordCache != null && _keywordCache.TryGetValue(keywordId, out var def))
                return def;
            return null;
        }

        /// <summary>
        /// 从 JSON 加载测试卡牌列表
        /// </summary>
        public static List<CardData> LoadCards(string jsonPath)
        {
            var json = Resources.Load<TextAsset>(jsonPath);
            if (json == null)
            {
                Debug.LogWarning($"[CardLoader] 卡牌配置未找到: {jsonPath}");
                return new List<CardData>();
            }

            var wrapper = JsonUtility.FromJson<TestCardsConfigWrapper>(json.text);
            var result = new List<CardData>();

            foreach (var entry in wrapper.cards)
            {
                var cardData = CreateCardData(entry);
                result.Add(cardData);
            }

            WarnCostNonConformance(result);
            ValidateComboDomains(result);
            ValidateMountHosts(result);
            ValidateRandomParams(result);

            return result;
        }

        /// <summary>
        /// 构筑期装载宿主校验（2026-09-11 MountKinds 定案配套的硬约束，可见不炸——坏卡点名）：
        /// - 微缩/放大（MountKinds=0,5，登场效果）：宿主须具备属性（Power/Life）——无属性生物不可承载；
        /// - 回响（MountKinds=1,5,6）：2026-09-13 定案生物和法术通用——宿主类型不限制；
        /// - 召唤衍生物：字符串参数（条目 ID）必须指向一张真实生物卡（非空、非自指、可解析且为生物）。
        /// 后续 MountKinds 全面收紧时，通用装载位校验在此扩展。
        /// </summary>
        private static void ValidateMountHosts(List<CardData> cards)
        {
            foreach (var card in cards)
            {
                if (card?.Effects == null) continue;
                foreach (var eff in card.Effects)
                {
                    foreach (var atom in EnumerateAtomEntries(eff))
                    {
                        var atomType = TypeOf(atom);
                        if (atom == null || atomType == null) continue;
                        if (atomType == AtomicEffectType.GrantMiniature || atomType == AtomicEffectType.GrantMagnify
                            || atomType == AtomicEffectType.GrantGuardian)
                        {
                            if (!card.HasCombatStats)
                                Debug.LogError($"[CardLoader] 卡 {card.ID}({card.CardName})："
                                             + $"微缩/放大/守护为登场效果，宿主不具备属性（Power/Life）——构筑期拦截");
                        }
                        // 回响（2026-09-13 用户定案：生物和法术通用）——不再限制宿主类型，
                        // 旧"须为瞬间法术"拦截已废除
                        else if (atomType == AtomicEffectType.SummonToken)
                        {
                            if (string.IsNullOrEmpty(atom.str))
                            {
                                Debug.LogError($"[CardLoader] 卡 {card.ID}({card.CardName})："
                                             + $"召唤衍生物的字符串参数为空——必须指向一张真实生物卡，构筑期拦截");
                            }
                            else if (atom.str == card.ID)
                            {
                                Debug.LogError($"[CardLoader] 卡 {card.ID}({card.CardName})："
                                             + $"召唤衍生物自指（模板=宿主本身）——构筑期拦截");
                            }
                            else
                            {
                                var resolver = Attribute.Handlers.SummonTokenHandler.ResolveTemplate
                                               ?? Attribute.MorphSystem.ResolveMorphTarget;
                                var template = resolver?.Invoke(atom.str);
                                // 2026-09-13 修复：resolver 返回裸 CardData（不实现 IHasSupertype），
                                // is 判定恒 false 会把正常模板误报为非生物——改属性直判（同 SummonTokenHandler）。
                                var tplSuper = template is IHasSupertype ht ? ht.Supertype : template?.Supertype;
                                if (template != null && tplSuper != Cardtype.Creature)
                                    Debug.LogError($"[CardLoader] 卡 {card.ID}({card.CardName})："
                                                 + $"衍生物模板 {atom.str} 不是生物卡——构筑期拦截");
                            }
                        }
                    }
                }
                // 自带关键词回响（printed）：2026-09-13 生物和法术通用——不再限制宿主类型

                // 连接光环须有箭头（2026-09-13 坚韧光环定案：生物/结界搭载光环必须声明箭头——
                // 无箭头光环永远无受益者，属数据错误；箭头数=光环受益面与计价乘数）
                if (card.LinkAuras != null && card.LinkAuras.Count > 0
                    && card.ArrowDirections == HexDirection.None)
                    Debug.LogError($"[CardLoader] 卡 {card.ID}({card.CardName})：声明了连接光环但未配箭头（arrows 为空）——构筑期拦截（生物/结界同规）");
            }
        }

        /// <summary>是否瞬间法术：法术超类 + 任一效果 Activate_Instant（瞬间法术底盘盈余同判据）。</summary>
        private static bool IsInstantSpell(CardData card)
            => card is IHasSupertype ht && ht.Supertype == Cardtype.Spell
               && card.Effects != null
               && card.Effects.Any(f => f.TriggerTiming == (int)TriggerTiming.Activate_Instant);

        /// <summary>原子引用 → 枚举（2026-09-14 彻底引用化：经表行解析；行缺失/枚举错名→null）。</summary>
        private static AtomicEffectType? TypeOf(AtomicEffectEntry a)
        {
            var row = Attribute.AtomicEffectTable.GetByHashId(a?.refId);
            return row != null && System.Enum.TryParse<AtomicEffectType>(row.EnumName, out var t)
                ? t : (AtomicEffectType?)null;
        }

        /// <summary>枚举效果内全部原子条目（Steps 含抉择/分支嵌套 + 扁平 AtomicEffects 兜底）。</summary>
        private static IEnumerable<AtomicEffectEntry> EnumerateAtomEntries(CardEffectData eff)
        {
            if (eff.Steps != null)
            {
                foreach (var s in eff.Steps)
                    foreach (var a in EnumerateStepAtomEntries(s))
                        yield return a;
            }
            else if (eff.AtomicEffects != null)
            {
                foreach (var a in eff.AtomicEffects)
                    if (!string.IsNullOrEmpty(a?.refId)) yield return a;
            }
        }

        private static IEnumerable<AtomicEffectEntry> EnumerateStepAtomEntries(EffectStepData step)
        {
            if (step == null) yield break;
            if (!string.IsNullOrEmpty(step.atomic?.refId)) yield return step.atomic;
            if (step.choices != null)
                foreach (var c in step.choices)
                    if (c?.steps != null)
                        foreach (var s in c.steps)
                            foreach (var a in EnumerateStepAtomEntries(s))
                                yield return a;
            if (step.thenSteps != null)
                foreach (var a in step.thenSteps)
                    if (!string.IsNullOrEmpty(a?.refId)) yield return a;
            if (step.elseSteps != null)
                foreach (var a in step.elseSteps)
                    if (!string.IsNullOrEmpty(a?.refId)) yield return a;
        }

        /// <summary>
        /// 构筑期目标域校验（2026-09-10 目标域模型）：每效果的主序列原子域交集为空 → LogError
        /// （仿 BranchConfigTable.ValidateAgainstCode 先例：可见不炸——坏卡点名，装载不中断）。
        /// 同场校验组合上限：主序列原子 ≤ 2（2026-09-10 攻/守效果化定案——
        /// 三种合法组合形式：抉择 / 条件奖励(门) / 并列；超限告警不拦截）。
        /// </summary>
        private static void ValidateComboDomains(List<CardData> cards)
        {
            foreach (var card in cards)
            {
                if (card?.Effects == null) continue;
                foreach (var eff in card.Effects)
                {
                    if (eff == null) continue;
                    var def = CardEffectConverter.ConvertOne(eff, card.ID);
                    if (def == null) continue;

                    // 组合上限：主序列（Steps 优先，扁平兜底）原子计数 ≤ 2
                    var mainAtoms = def.Steps != null && def.Steps.Count > 0
                        ? CardEffectConverter.EnumerateMainSequenceAtoms(def.Steps, 0).ToList()
                        : def.Effects?.ToList() ?? new List<AtomicEffectInstance>();
                    int atomCount = mainAtoms.Count(a => a != null);
                    if (atomCount > 2)
                        Debug.LogWarning($"[CardLoader] 卡 {card.ID}({card.CardName}) 效果 {def.Id}："
                                       + $"主序列原子 {atomCount} 个超组合上限 2（抉择/条件奖励/并列三形式）");

                    if (def.TargetDomain == null || def.TargetDomain.Count > 0) continue;
                    // 域空且并非"全无目标原子"（存在带域原子但交集空）才是断链
                    var hasKindAtom = false;
                    foreach (var atom in CardEffectConverter.EnumerateMainSequenceAtoms(def.Steps, 0))
                        if (atom?.TargetKinds != null && atom.TargetKinds.Count > 0) { hasKindAtom = true; break; }
                    if (def.Steps == null || def.Steps.Count == 0)
                        foreach (var atom in def.Effects)
                            if (atom?.TargetKinds != null && atom.TargetKinds.Count > 0) { hasKindAtom = true; break; }
                    if (hasKindAtom)
                        Debug.LogError($"[CardLoader] 卡 {card.ID}({card.CardName}) 效果 {def.Id}："
                                     + $"主序列原子目标域交集为空——组合不可作用任何对象，构筑期拦截（检查各原子 TargetKinds）");
                }
            }
        }

        /// <summary>
        /// 两个随机（2026-09-13 定案）构筑校验：
        /// ① 数值随机幅度 ∉ [0,1] 告警（converter 会夹取，此处纯诊断）；
        /// ② SelectionMode=Random 且 DynamicTargetCount=true 告警（随机需固定个数，动态数量配随机退化为全取）。
        /// </summary>
        private static void ValidateRandomParams(List<CardData> cards)
        {
            foreach (var card in cards)
            {
                if (card?.Effects == null) continue;
                foreach (var eff in card.Effects)
                {
                    if (eff == null) continue;

                    if (eff.SelectionMode == (int)SelectionMode.Random && eff.DynamicTargetCount)
                        Debug.LogWarning($"[CardLoader] 卡 {card.ID}({card.CardName}) 效果 {eff.Id}："
                                       + "SelectionMode=Random 与 DynamicTargetCount 互斥——随机抽取需固定个数，动态数量将退化为全取");

                    // 固有全域原子（2026-09-13）：显式声明 Manual/Random 属数据错误——converter 会强制 Full，此处告警
                    bool HasSweep(AtomicEffectEntry a)
                        => a != null && TypeOf(a) is { } sweepType && CardCore.CostDerivationService.IsIntrinsicSweep(sweepType);
                    bool hasSweep = (eff.AtomicEffects ?? new List<AtomicEffectEntry>()).Any(HasSweep)
                        || (eff.Steps ?? new List<EffectStepData>()).Any(s => s != null
                            && (HasSweep(s.atomic) || (s.thenSteps ?? new List<AtomicEffectEntry>()).Any(HasSweep)));
                    if (hasSweep && (eff.SelectionMode == (int)SelectionMode.Manual || eff.SelectionMode == (int)SelectionMode.Random))
                        Debug.LogWarning($"[CardLoader] 卡 {card.ID}({card.CardName}) 效果 {eff.Id}："
                                       + "固有全域原子（类型伤害/全体治疗）只能全域结算——已强制 SelectionMode=Full（声明的 Manual/Random 被覆写）");

                    // 触发上限不可修改原子（2026-09-13，MountKinds 含 8——少数，如坚韧 bd623d85）：
                    // 声明 TriggerLimitPerTurn 属数据错误——converter 已覆写为无限（-1）
                    bool HasCap8(AtomicEffectEntry a)
                        => a != null && (Attribute.AtomicEffectTable.GetByHashId(a.refId)?.MountKinds ?? "")
                               .Split(',').Select(s => s.Trim()).Any(s => s == "8");
                    bool hasCap8 = (eff.AtomicEffects ?? new List<AtomicEffectEntry>()).Any(HasCap8)
                        || (eff.Steps ?? new List<EffectStepData>()).Any(s => s != null && HasCap8(s.atomic));
                    if (hasCap8 && eff.TriggerLimitPerTurn != 0)
                        Debug.LogWarning($"[CardLoader] 卡 {card.ID}({card.CardName}) 效果 {eff.Id}："
                                       + "含触发上限不可修改原子（MountKinds=8，如坚韧）——声明的 TriggerLimitPerTurn 被覆写为无限");

                    // 动态分支引擎（2026-09-13 分支体系正规化）：奖励原子须可挂载为分支奖励（MountKinds 含 4）；
                    // 运势/拼点 x∈[1,5]（运行时夹取）；倒计时 EngineParam>0 为显式回合数（缺省按奖励推导费换算）
                    if (eff.EngineKind != (int)CardCore.BranchEngineKind.None)
                    {
                        foreach (var a in (eff.AtomicEffects ?? new List<AtomicEffectEntry>()))
                        {
                            if (a == null) continue;
                            var mk4 = (Attribute.AtomicEffectTable.GetByHashId(a.refId)?.MountKinds ?? "").Split(',').Select(s => s.Trim());
                            if (!mk4.Contains("4"))
                                Debug.LogWarning($"[CardLoader] 卡 {card.ID}({card.CardName}) 效果 {eff.Id}："
                                               + $"引擎奖励原子 {a.refId} 未开放分支奖励挂载（MountKinds 不含 4）");
                        }
                        if (eff.EngineKind != (int)CardCore.BranchEngineKind.Countdown && (eff.EngineParam < 1 || eff.EngineParam > 5))
                            Debug.LogWarning($"[CardLoader] 卡 {card.ID}({card.CardName}) 效果 {eff.Id}："
                                           + $"运势/拼点参数 x={eff.EngineParam} 越界 [1,5]（运行时夹取）");

                    // 属性价梯（2026-09-13 定案）：属性效果固定回合只许 1、2 两档
                    //（UntilEndOfTurn/UntilNextTurn；ForTurns(3+) 构筑拦截——计价封顶按 2 回合兜底）
                    bool hasStatAtom = (eff.AtomicEffects ?? new List<AtomicEffectEntry>()).Any(a =>
                            TypeOf(a) is { } st2
                               && (st2 == AtomicEffectType.ModifyPower || st2 == AtomicEffectType.ModifyLife));
                    if (hasStatAtom && eff.Duration == (int)DurationType.ForTurns && eff.DurationValue > 2)
                        Debug.LogWarning($"[CardLoader] 卡 {card.ID}({card.CardName}) 效果 {eff.Id}："
                                       + $"属性效果固定回合只许 1、2 两档（ForTurns({eff.DurationValue}) 越界——计价按 2 回合封顶）");

                    // 生物赋予关键词固定 1 回合（2026-09-13 定案）：生物宿主的 Grant 原子声明
                    // Permanent/长持续 → 运行时被覆写为 Temp+回合末到期——计价按声明收（构筑侧提示）
                    if (card.Supertype == Cardtype.Creature
                        && (eff.AtomicEffects ?? new List<AtomicEffectEntry>()).Any(a =>
                            a != null && !string.IsNullOrEmpty(a.refId)
                                && Attribute.AtomicEffectTable.GetByHashId(a.refId)?.EnumName.StartsWith("Grant") == true)
                        && eff.Duration != (int)DurationType.UntilEndOfTurn && eff.Duration != (int)DurationType.Once)
                        Debug.LogWarning($"[CardLoader] 卡 {card.ID}({card.CardName}) 效果 {eff.Id}："
                                       + "生物赋予的关键词固定持续 1 回合（声明持续被运行时覆写为 Temp/UET——魔法与光环照旧）");
                    }

                    void CheckAmplitude(AtomicEffectEntry atom, string where)
                    {
                        if (atom == null) return;
                        if (atom.amp < 0f || atom.amp > 1f)
                            Debug.LogWarning($"[CardLoader] 卡 {card.ID}({card.CardName}) 效果 {eff.Id} 原子 {atom.refId}({where})："
                                           + $"amp={atom.amp:0.###} 越界 [0,1]（converter 已夹取）");
                        // 挂载位校验（2026-09-13 MountKinds 7=可挂载随机）：未开放的原子配幅度 → 告警
                        if (atom.amp > 0f)
                        {
                            var mountCsv = CardCore.Attribute.AtomicEffectTable.GetByHashId(atom.refId)?.MountKinds ?? "";
                            bool allowsRandom = mountCsv.Split(',')
                                .Select(s => s.Trim()).Any(s => s == "7");
                            if (!allowsRandom)
                                Debug.LogWarning($"[CardLoader] 卡 {card.ID}({card.CardName}) 效果 {eff.Id} 原子 {atom.refId}({where})："
                                               + "配了随机幅度但表未开放可挂载随机（MountKinds 不含 7）");
                        }
                    }

                    if (eff.AtomicEffects != null)
                        foreach (var atom in eff.AtomicEffects) CheckAmplitude(atom, "主序列");
                    if (eff.Steps != null)
                        foreach (var step in eff.Steps)
                        {
                            if (step == null) continue;
                            CheckAmplitude(step.atomic, "步骤");
                            if (step.thenSteps != null) foreach (var a in step.thenSteps) CheckAmplitude(a, "then");
                            if (step.elseSteps != null) foreach (var a in step.elseSteps) CheckAmplitude(a, "else");
                        }
                }
            }
        }

        /// <summary>
        /// 直接从 JSON 文本加载卡牌（用于测试，不依赖 Resources）
        /// </summary>
        public static List<CardData> LoadCardsFromText(string jsonText)
        {
            var wrapper = JsonUtility.FromJson<TestCardsConfigWrapper>(jsonText);
            var result = new List<CardData>();

            foreach (var entry in wrapper.cards)
            {
                var cardData = CreateCardData(entry);
                result.Add(cardData);
            }

            WarnCostNonConformance(result);
            ValidateComboDomains(result);
            ValidateMountHosts(result);
            ValidateRandomParams(result);

            return result;
        }

        /// <summary>
        /// 构筑期目标域校验（2026-09-10 目标域模型）：每效果的主序列原子域交集为空 → LogError
        /// （仿 BranchConfigTable.ValidateAgainstCode 先例：可见不炸——坏卡点名，装载不中断）。
        /// </summary>

        /// <summary>
        /// 将 CardData 列表转换为 CardWrapper 列表
        /// </summary>
        public static List<Card> ToCardInstances(List<CardData> cardsData)
        {
            var result = new List<Card>();
            foreach (var data in cardsData)
            {
                result.Add(new CardWrapper(data));
            }
            return result;
        }

        /// <summary>
        /// 构建测试卡组（每张卡 copiesPerCard 份）。
        /// 构筑规则（定案）：玩家卡组内卡牌不重复——每张卡是效果 ID 的唯一索引，
        /// 仅效果产生的衍生物可重复。copiesPerCard 默认 1；>1 仅测试脚手架用。
        /// </summary>
        public static List<Card> BuildTestDeck(string jsonPath, int copiesPerCard = 1)
        {
            var cardsData = LoadCards(jsonPath);
            return BuildDeck(cardsData, copiesPerCard);
        }

        /// <summary>
        /// 从文本构建测试卡组（用于测试；copiesPerCard 默认 1，见构筑规则定案）
        /// </summary>
        public static List<Card> BuildTestDeckFromText(string jsonText, int copiesPerCard = 1)
        {
            var cardsData = LoadCardsFromText(jsonText);
            return BuildDeck(cardsData, copiesPerCard);
        }

        /// <summary>
        /// 从 CardData 列表构建卡组（卡组不重复：copiesPerCard=1 为规则默认）
        /// </summary>
        public static List<Card> BuildDeck(List<CardData> cardsData, int copiesPerCard)
        {
            var deck = new List<Card>();
            foreach (var data in cardsData)
            {
                for (int i = 0; i < copiesPerCard; i++)
                {
                    deck.Add(new CardWrapper(data));
                }
            }
            return deck;
        }

        // ======================================== 内部方法 ========================================

        /// <summary>
        /// 生物黑白关键词转启动式自赋予（2026-09-14 定案）：黑白不进生物费用列表——
        /// 生物可作地牌，黑白若入费用构成即从地牌产出，破坏「黑白=错边效果补偿资源」定位
        /// （运行时选择：抵扣此卡灰色费用 / 生成黑白元素）。印制黑白关键词改为启动式效果：
        /// 构筑期不计锚价（激活式不进 E 桶，仅占技能挂载口），使用时现付锚价
        /// （发动=横置+付费+赋予自身；黑白元素经错边补偿/黑经济获得）。
        /// 法术不作地牌，印制黑白关键词照旧计价。幂等（转换后关键词已移除，二次调用空转）。
        /// 返回被转换的关键词 id 列表（空=无转换）。
        /// </summary>
        public static List<string> ConvertCreatureSpecialKeywordsToActivated(CardConfigEntry entry)
        {
            var converted = new List<string>();
            if (entry == null || entry.keywords == null || entry.keywords.Count == 0) return converted;
            if (ParseCardtype(entry.supertype) != Cardtype.Creature) return converted;

            LoadKeywords();
            for (int i = entry.keywords.Count - 1; i >= 0; i--)
            {
                var kwId = entry.keywords[i];
                var def = GetKeywordDefinition(kwId);
                if (def == null || string.IsNullOrEmpty(def.atomicEffect)
                    || !Enum.TryParse<AtomicEffectType>(def.atomicEffect, out var grant)) continue;

                var color = ElementAffinities.GetAffinityForEffect(grant).PrimaryColor;
                if (color != ManaType.White && color != ManaType.Black) continue;

                entry.keywords.RemoveAt(i);
                converted.Add(kwId);
                if (entry.effects == null) entry.effects = new List<CardEffectData>();
                entry.effects.Add(new CardEffectData
                {
                    Id = "ACT_" + kwId,
                    DisplayName = "启动·" + def.nameZh,
                    Description = "使用时支付锚价，自身获得「" + def.nameZh + "」",
                    TriggerTiming = (int)TriggerTiming.Activate_Active,
                    ActivationType = 2, // 主动（启动式只能主动发动）
                    SelectionMode = 0, // Self：源卡为唯一目标
                    TargetCount = 1,
                    AtomicEffects = new List<AtomicEffectEntry>
                    {
                        // 引用型：枚举名→行 ID 反查（GetByEnumName 键=枚举名；无表行→refId 空→装载告警）
                        new AtomicEffectEntry { refId = Attribute.AtomicEffectTable.GetByEnumName(def.atomicEffect)?.HashId ?? def.atomicEffect },
                    },
                });
            }
            return converted;
        }

        /// <summary>
        /// 从配置条目创建 CardData
        /// </summary>
        private static CardData CreateCardData(CardConfigEntry entry)
        {
            // 黑白关键词装载期转启动式（须先于 CardData 组装：keywords/effects 读转换后的条目）
            ConvertCreatureSpecialKeywordsToActivated(entry);

            var cardData = new CardData
            {
                ID = entry.id,
                CardName = entry.cardName,
                Supertype = ParseCardtype(entry.supertype),
                Power = entry.power,
                Life = entry.life,
                Cost = ParseCost(entry.costList),
                Keywords = entry.keywords ?? new List<string>(),
                Tags = entry.tags ?? new List<string>(),
                Effects = entry.effects ?? new List<CardEffectData>(),
                Subtype = ParseFlags<CardSubtype>(entry.subtype),
                ArrowDirections = ParseFlags<HexDirection>(entry.arrows),
            };

            // 效果引用解析（2026-09-14）：effectIds 非空 → 从 Effects.json 还原（内嵌 effects 被忽略）
            if (entry.effectIds != null && entry.effectIds.Count > 0)
            {
                var resolved = new List<CardEffectData>();
                foreach (var eid in entry.effectIds)
                {
                    var effect = EffectsLibrary.Resolve(eid);
                    if (effect != null) resolved.Add(effect);
                }
                cardData.Effects = resolved;
            }

            if (entry.level >= 0) cardData.Level = entry.level;

            // 连接光环声明（三轨制）：无效条目（stat/keyword 双空）装载期即丢弃
            if (entry.linkAuras != null)
                cardData.LinkAuras = entry.linkAuras
                    .Where(a => a != null && (!string.IsNullOrEmpty(a.stat) || !string.IsNullOrEmpty(a.keyword)))
                    .ToList();

            // 战斗底盘（2026-09-10 攻/守效果化）：opt-out 与瞬间盈余分配，缺省全 false（自带攻守、退费）
            cardData.NoAttack = entry.noAttack;
            cardData.NoGuard = entry.noGuard;
            cardData.SurplusToSpeed = entry.surplusToSpeed;

            // 统一计价兜底：costList 缺省 → 写入建议档位分布（幂等，非空不动）
            CardCostService.EnsureCost(cardData);

            return cardData;
        }

        /// <summary>
        /// 装载期构筑校验（提示级）：声明档位的卡若 D &gt; C（规则一 2026-09-11 简化口径）打警告，不阻止加载。
        /// </summary>
        private static void WarnCostNonConformance(List<CardData> cards)
        {
            foreach (var card in cards)
            {
                if (card?.Cost == null || card.Cost.Count == 0) continue;
                var r = CardCostService.Derive(card);
                if (!r.Conformant)
                    Debug.LogWarning($"[CardCost] {card.ID}({card.CardName}) 不符规则一：D={r.DerivedTotal} > C={r.DeclaredTier}（超模 {r.OffsetRequirement}）");
            }
        }

        /// <summary>
        /// 解析逗号分隔的 Flags 枚举串（缺省 → 默认值 0）
        /// </summary>
        private static T ParseFlags<T>(string raw) where T : struct, Enum
        {
            if (string.IsNullOrWhiteSpace(raw))
                return default;
            if (Enum.TryParse<T>(raw, out var result))
                return result;
            return default;
        }

        /// <summary>
        /// 解析卡牌类型
        /// </summary>
        private static Cardtype ParseCardtype(string typeStr)
        {
            if (Enum.TryParse<Cardtype>(typeStr, out var result))
                return result;
            return Cardtype.Creature;
        }

        /// <summary>
        /// 解析费用列表
        /// </summary>
        private static Dictionary<int, float> ParseCost(List<CostJsonEntry> costList)
        {
            var cost = new Dictionary<int, float>();
            if (costList == null) return cost;
            foreach (var entry in costList)
            {
                cost[entry.manaType] = entry.amount;
            }
            return cost;
        }
    }
}
