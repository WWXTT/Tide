using System;
using System.Collections.Generic;
using System.IO;
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

        // 费用（2026-10-04 位置数组化）：下标=ManaType 枚举序号 [灰,红,蓝,绿,白,黑]，长度=枚举成员数；
        // 旧 [{manaType,amount}] 对列表形态已随全表重推退役。
        public List<float> costList;
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

        // 结界耐久（2026-09-24 定案：结界=耐久体，被攻击每次仅损失 1 点，归零销毁；0=无）——生物不使用
        public int durability;

        // 底盘退费落色（2026-10-02 定案：玩家自标）：-1=未声明（默认先灰后最高费用色）；0..5=ManaType
        public int refundColor = -1;

        // 代价栏（2026-10-04 持久化链）：卡层 Payload 原子引用——CostType 恒 Payload、Value 恒 1，
        // 装载时按规范常量重建 CostEntry；逆转后的镜像域存于 payload.kinds（可超出表行域）。
        // refId 空/字段缺省 = 无代价（旧档兼容——JsonUtility 空对象回落同样由此守卫拦下）。
        public AtomicEffectEntry payload;
    }

    // CostJsonEntry（{manaType, amount} 对）已删除（2026-10-04 费用位置数组化）：
    // costList 现为 float 位置数组（下标=ManaType 枚举序号）。

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
        /// （与写入 IHasKeywords 的字符串同源，如 GrantReborn → Reborn）。
        /// 未登记 Specs 的 Grant 行**跳过**（2026-10-08 坚韧指示物化：GrantToughness 行已非关键词——
        /// 防止退化拼出幽灵目录条目）；旧「去 Grant 前缀」兜底随之退役。
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
                    continue; // 未登记=非关键词族 Grant 行（如指示物化的坚韧），不进关键词目录

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
        /// 从 JSON 加载测试卡牌列表（2026-09-27 去 Unity 化：原 Resources.Load 直读 Assets/Resources 下同名 json）
        /// </summary>
        public static List<CardData> LoadCards(string jsonPath)
        {
            string path = Path.Combine(TidePaths.DataPath, "Resources", jsonPath + ".json");
            string jsonText = null;
            try
            {
                if (File.Exists(path)) jsonText = File.ReadAllText(path);
            }
            catch (Exception) { }
            if (jsonText == null)
            {
                TideLog.Warn($"[CardLoader] 卡牌配置未找到: {path}");
                return new List<CardData>();
            }

            var wrapper = TideJson.FromJson<TestCardsConfigWrapper>(jsonText);
            var result = new List<CardData>();

            foreach (var entry in wrapper.cards)
            {
                var cardData = CreateCardData(entry);
                result.Add(cardData);
            }

            // D≤C 超模校验已移除（2026-09-24 定案：效果层纯表累加、卡层只算减费，直判无意义）
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
                        if (atomType == AtomicEffectType.GrantMiniature || atomType == AtomicEffectType.GrantMagnify)
                        {
                            if (!card.HasCombatStats)
                                TideLog.Error($"[CardLoader] 卡 {card.ID}({card.CardName})："
                                             + $"微缩/放大为登场效果，宿主不具备属性（Power/Life）——构筑期拦截");
                        }
                        // 守护（2026-10-07 仅可转换）：收为仅连接光环节点，不再作登场 Grant 原子
                        // ——宿主属性校验随之退役；结界守护走耐久、生物守护走生命（伤害改写天然分叉）。
                        else if (atomType == AtomicEffectType.SummonToken)
                        {
                            if (string.IsNullOrEmpty(atom.str))
                            {
                                TideLog.Error($"[CardLoader] 卡 {card.ID}({card.CardName})："
                                             + $"召唤衍生物的字符串参数为空——必须指向一张真实生物卡，构筑期拦截");
                            }
                            else if (atom.str == card.ID)
                            {
                                TideLog.Error($"[CardLoader] 卡 {card.ID}({card.CardName})："
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
                                    TideLog.Error($"[CardLoader] 卡 {card.ID}({card.CardName})："
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
                    TideLog.Error($"[CardLoader] 卡 {card.ID}({card.CardName})：声明了连接光环但未配箭头（arrows 为空）——构筑期拦截（生物/结界同规）");

                // 光环关键词条目须可挂（2026-10-07 晚终版：关键词默认可−位 8 黑名单拉黑）：
                // 名单在表（现值：守护/再生/禁魔石）——与光环 live-query 持续语义冲突
                //（守护=配对制弹选；法术护盾 2026-10-09 指示物化退出关键词族，目录不收）
                if (card.LinkAuras != null)
                {
                    foreach (var aura in card.LinkAuras)
                    {
                        if (aura == null || string.IsNullOrEmpty(aura.keyword)) continue;
                        if (!ComposerCatalog.IsAuraMountableKeyword(aura.keyword))
                            TideLog.Error($"[CardLoader] 卡 {card.ID}({card.CardName})：光环关键词「{aura.keyword}」"
                                         + "不可作光环（原子表 Grant 行标「不可作为连接光环」位 8 黑名单——"
                                         + "与光环 live-query 持续语义冲突）——构筑期拦截");
                    }
                }
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
            // 槽级分支 Then 奖励（2026-10-09 全局唯一规则配套）：作者形态的奖励挂在 atomic.branch.then
            //——装载校验（宿主/查重）与 thenSteps 展开形态一并枚举
            if (step.atomic?.branch?.then != null)
                foreach (var r in step.atomic.branch.then)
                    if (!string.IsNullOrEmpty(r?.refId)) yield return r;
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
        /// 单范围域宽校验（2026-09-16 六值定案）：模式 0/1/2 要求组合域单一 TargetKind——
        /// 一个 {target} 只能从一个范围选择，多范围域须用 3/4/5。
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

                    // 全局唯一规则（2026-10-09 定案）：同一原子（refId=表行身份）在一个组合效果中
                    // 只出现一次——主序列/抉择/Then 奖励均计入（并列与奖励同规；合成期已拦，此处构筑期兜底）
                    var seenRefIds = new HashSet<string>();
                    var dupRefIds = new HashSet<string>();
                    foreach (var a in EnumerateAtomEntries(eff))
                        if (!string.IsNullOrEmpty(a?.refId) && !seenRefIds.Add(a.refId)) dupRefIds.Add(a.refId);
                    if (dupRefIds.Count > 0)
                        TideLog.Error($"[CardLoader] 卡 {card.ID}({card.CardName}) 效果 {def.Id}：重复效果 "
                                       + $"[{string.Join(",", dupRefIds)}]——全局规则：一个组合效果中同一效果只能出现一次（并列与奖励均计入）");

                    // 组合上限（2026-10-05 两槽定案）：主序列（Steps 优先，扁平兜底）主干原子计数 ≤ 2——
                    // 并列缩为两槽（Then 奖励/光环条目不计入），超限=构筑期拦截
                    var mainAtoms = def.Steps != null && def.Steps.Count > 0
                        ? CardEffectConverter.EnumerateMainSequenceAtoms(def.Steps, 0).ToList()
                        : def.Effects?.ToList() ?? new List<AtomicEffectInstance>();
                    int atomCount = mainAtoms.Count(a => a != null);
                    if (atomCount > 2)
                        TideLog.Error($"[CardLoader] 卡 {card.ID}({card.CardName}) 效果 {def.Id}："
                                       + $"主序列主干原子 {atomCount} 个超两槽上限 2（2026-10-05 并列定案；Then 奖励不计入）");

                    // 效果级作用范围越界（2026-10-04 相同目标定案）：声明域须落在主序列原子域交集内
                    //（并列共享同一选中目标——越界目标对域外原子不合法）。交集空不判（下方断链告警覆盖）。
                    if (eff.TargetKinds != null && eff.TargetKinds.Count > 0)
                    {
                        List<int> rawInter = null;
                        foreach (var atom in mainAtoms)
                        {
                            if (atom?.TargetKinds == null || atom.TargetKinds.Count == 0) continue;
                            rawInter = rawInter == null
                                ? new List<int>(atom.TargetKinds)
                                : TargetKindRules.Intersect(rawInter, atom.TargetKinds);
                        }
                        if (rawInter != null && rawInter.Count > 0
                            && eff.TargetKinds.Any(k => !rawInter.Contains(k)))
                            TideLog.Warn($"[CardLoader] 卡 {card.ID}({card.CardName}) 效果 {def.Id}："
                                           + $"效果级作用范围 [{string.Join(",", eff.TargetKinds)}] 超出原子域交集 "
                                           + $"[{TargetKindRules.Format(rawInter)}]——相同目标定案：作用范围须落在交集内");
                    }

                    // 单范围域宽校验（2026-09-16 六值定案核心）：一个 {target} 只能从一个范围选择——
                    // 模式 0/1/2（Single/Multiple/Whole）要求组合域恰为单一 TargetKind，违反=数据错误
                    //（converter 已覆写的强制 WholeUnion 不受影响；域空=无目标效果不在此列）。
                    // 逐原子目标制（2026-10-09）：组合域=并集仅作展示/预检，宽度不限——不在此校验。
                    if (!def.PerAtomTargets && SelectionModeRules.IsSingleScope(def.SelectionMode))
                    {
                        var modeDomains = new List<List<int>> { def.TargetDomain };
                        if (def.ChoiceDomains != null)
                            foreach (var cd in def.ChoiceDomains)
                                if (cd != null && cd.Count > 0) modeDomains.Add(cd);
                        foreach (var dom in modeDomains)
                        {
                            if (dom != null && dom.Count > 1)
                            {
                                TideLog.Error($"[CardLoader] 卡 {card.ID}({card.CardName}) 效果 {def.Id}："
                                             + $"声明单范围模式 {def.SelectionMode}（{string.Join(",", dom)} 共 {dom.Count} 个范围）——"
                                             + "一个 {{target}} 只能从一个范围选择，应改用多范围模式 3/4/5");
                                break;
                            }
                        }
                    }

                    // 数量-模式矛盾（轻告警，按原始声明判——converter 兜底回填不触发）：
                    // 选一档显式声明非 1 的数量 / 全取档显式声明正数量，均属声明噪声。
                    if (!def.RandomTarget)
                    {
                        if (SelectionModeRules.IsPickOne(def.SelectionMode)
                            && eff.TargetCount != -2 && eff.TargetCount != 1)
                            TideLog.Warn($"[CardLoader] 卡 {card.ID}({card.CardName}) 效果 {def.Id}："
                                           + $"选一档（{def.SelectionMode}）显式声明 TargetCount={eff.TargetCount}——数量以模式为准（1），多余声明被忽略");
                        if (SelectionModeRules.IsTakeAll(def.SelectionMode) && eff.TargetCount > 0)
                            TideLog.Warn($"[CardLoader] 卡 {card.ID}({card.CardName}) 效果 {def.Id}："
                                           + $"全取档（{def.SelectionMode}）显式声明 TargetCount={eff.TargetCount}——全取不按数量，多余声明被忽略");
                    }

                    // 逐原子目标制（2026-10-09）：无 header 声明=各原子按自身域独立解析，交集断链退役——
                    // 改为单条效果作用域上限兜底：主序列原子极性过滤域**并集 ≤4 个不同 TargetKind**
                    //（合成器加入门的装载期兜底；无域原子不计数）。
                    if (def.PerAtomTargets)
                    {
                        var unionKinds = new HashSet<int>();
                        foreach (var atom in mainAtoms)
                        {
                            if (atom == null) continue;
                            foreach (var k in atom.TargetKinds ?? new List<int>())
                                unionKinds.Add(k);
                        }
                        if (unionKinds.Count > 4)
                            TideLog.Error($"[CardLoader] 卡 {card.ID}({card.CardName}) 效果 {def.Id}："
                                           + $"主序列原子作用域并集 {unionKinds.Count} 个超上限 4"
                                           + $"（{TargetKindRules.Format(unionKinds.ToList())}）——逐原子目标制单条效果限 4 个作用范围");
                        continue;
                    }

                    // 共享口径（header 声明）域非空恒过；域空=全无目标原子（无断链可断——
                    // 旧「带域原子交集空=断链」拦截已随逐原子目标制退役：域不交的组合现按各原子自解析，合法）。
                    if (def.TargetDomain == null || def.TargetDomain.Count > 0) continue;
                }
            }
        }

        /// <summary>
        /// 两个随机（2026-09-13 定案；2026-09-16 RandomTarget 移出枚举为正交标志）构筑校验：
        /// ① 数值随机幅度 ∉ [0,1] 告警（converter 会夹取，此处纯诊断）；
        /// ② RandomTarget 且动态数量(-1) 告警（随机需固定个数，动态数量配随机退化为全取）。
        /// </summary>
        private static void ValidateRandomParams(List<CardData> cards)
        {
            foreach (var card in cards)
            {
                if (card?.Effects == null) continue;
                foreach (var eff in card.Effects)
                {
                    if (eff == null) continue;

                    if (eff.RandomTarget != 0 && eff.TargetCount == -1)
                        TideLog.Warn($"[CardLoader] 卡 {card.ID}({card.CardName}) 效果 {eff.Id}："
                                       + "目标随机与任意数量(-1)互斥——随机抽取需固定个数，动态数量将退化为全取");

                    // 固有全域原子告警块已删（2026-09-21 退役——全域用 TargetKinds+全取档组合表达）

                    // 槽级分支载荷校验（2026-10-05 两槽定案；2026-10-07 奖励资格派生化）：
                    // Then 奖励须有派生奖励资格（CanBeRewardRow——非规则光环/非仅连接光环/非引擎行）；
                    /// 引擎参数按 EngineParamRange 钳制口径诊断
                    void CheckBranchPayload(AtomicEffectEntry atom, string where)
                    {
                        var b = atom?.branch;
                        if (b == null) return;
                        if (b.then != null)
                        {
                            // 局面门对赌（2026-10-05 定案，诅咒门豁免）：奖励须可逆转——未达成逆转惩罚才有落点
                            bool gateBet = (BranchSettleKind)b.settle == BranchSettleKind.Gate
                                           && b.gateId != ComposerCatalog.CurseGateId;
                            foreach (var r in b.then)
                            {
                                if (r == null) continue;
                                var row = Attribute.AtomicEffectTable.GetByHashId(r.refId);
                                if (!ComposerCatalog.CanBeRewardRow(row))
                                    TideLog.Warn($"[CardLoader] 卡 {card.ID}({card.CardName}) 效果 {eff.Id} 原子 {atom.refId}({where})："
                                                   + $"Then 奖励原子 {r.refId} 无派生奖励资格（规则光环/仅连接光环/引擎行不可作奖励）");
                                else if (gateBet && !CostDerivationService.PayloadCostDomain(row).eligible)
                                    TideLog.Warn($"[CardLoader] 卡 {card.ID}({card.CardName}) 效果 {eff.Id} 原子 {atom.refId}({where})："
                                                   + $"局面门奖励原子 {r.refId} 不可逆转（对赌惩罚无从执行，converter 将剔除）");
                            }
                        }
                        if ((BranchSettleKind)b.settle == BranchSettleKind.Engine)
                        {
                            var kind = (BranchEngineKind)b.engine;
                            ComposerCatalog.EngineParamRange(kind, out int min, out int max);
                            if (kind != BranchEngineKind.Countdown && (b.engineParam < min || b.engineParam > max))
                                TideLog.Warn($"[CardLoader] 卡 {card.ID}({card.CardName}) 效果 {eff.Id} 原子 {atom.refId}({where})："
                                               + $"引擎参数 x={b.engineParam} 越界 [{min},{max}]（运行时夹取）");
                        }
                    }

                    // 属性价梯（2026-09-13 定案；2026-09-14 收缩：DurationValue/ForTurns 效果级退役——
                    // 固定回合只有 UntilEndOfTurn/UntilNextTurn 两档专档，无越界可查，此校验随之删除）

                    // 关键词赋予权限三分（2026-09-14 用户定案；2026-10-05 两槽化后脱离引擎通道恒查）：
                    // 目标=自己（域={Self} 的 Grant）走 Setting 文本轨——声明任意持续按声明计价（Permanent=永久的诚实档）；
                    // **可指向他人的 Grant**（域含单位 1-4）由生物来源赋予 → 运行时覆写为 Temp 1 回合
                    //（回合末到期）——声明非 UET/Once 时计价按声明收（构筑侧提示）。
                    bool GrantsOthers(AtomicEffectEntry a)
                    {
                        if (a == null || string.IsNullOrEmpty(a.refId)) return false;
                        var row = Attribute.AtomicEffectTable.GetByHashId(a.refId);
                        if (row?.EnumName?.StartsWith("Grant") != true) return false;
                        var kinds = row.GetTargetKindList();
                        return kinds != null && kinds.Any(k => k >= 1 && k <= 4); // 可指向单位（别人）
                    }
                    bool grantsOthers = (eff.AtomicEffects ?? new List<AtomicEffectEntry>()).Any(GrantsOthers)
                        || (eff.Steps ?? new List<EffectStepData>()).Any(s => s != null && GrantsOthers(s.atomic));
                    if (card.Supertype == Cardtype.Creature
                        && eff.Duration != (int)DurationType.UntilEndOfTurn && eff.Duration != (int)DurationType.Once
                        && grantsOthers)
                        TideLog.Warn($"[CardLoader] 卡 {card.ID}({card.CardName}) 效果 {eff.Id}："
                                       + "生物赋予**他人**的关键词固定 1 回合（声明持续被覆写为 Temp/UET——自身/魔法/光环走文本轨照旧）");

                    void CheckAmplitude(AtomicEffectEntry atom, string where)
                    {
                        if (atom == null) return;
                        if (atom.amp < 0f || atom.amp > 1f)
                            TideLog.Warn($"[CardLoader] 卡 {card.ID}({card.CardName}) 效果 {eff.Id} 原子 {atom.refId}({where})："
                                           + $"amp={atom.amp:0.###} 越界 [0,1]（converter 已夹取）");
                        // 挂载位校验（2026-10-07 黑名单翻转）：标「不可随机」的行配幅度 → 告警
                        if (atom.amp > 0f)
                        {
                            var row = CardCore.Attribute.AtomicEffectTable.GetByHashId(atom.refId);
                            if (ComposerCatalog.HasMountBit(row, MountKind.NoRandom))
                                TideLog.Warn($"[CardLoader] 卡 {card.ID}({card.CardName}) 效果 {eff.Id} 原子 {atom.refId}({where})："
                                               + "配了随机幅度但表标不可随机（MountKinds 含 NoRandom）");
                        }
                        CheckBranchPayload(atom, where);
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
            var wrapper = TideJson.FromJson<TestCardsConfigWrapper>(jsonText);
            var result = new List<CardData>();

            foreach (var entry in wrapper.cards)
            {
                var cardData = CreateCardData(entry);
                result.Add(cardData);
            }

            // D≤C 超模校验已移除（2026-09-24 定案：效果层纯表累加、卡层只算减费，直判无意义）
            // ValidateMountKindsTable 已随 2026-10-05 两槽定案 MountKind 重排删除（主装载口同批已清）
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
        /// 从配置条目创建 CardData
        /// </summary>
        private static CardData CreateCardData(CardConfigEntry entry)
        {
            // 黑白关键词照旧印制（2026-09-14 撤销「转启动式」定案）：参杂黑白的生物可作地牌，
            // 地牌只是不从黑白份额产指示物（ElementPool.GetCardCostAsTokens 过滤）——
            // 黑白获取通道不变（=卡结算：错边/Payload 补偿），费用侧照常计价。
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

            // 效果引用解析（2026-09-14；2026-09-24 关键词引用化）：effectIds 双路——
            // Effects.json 命中=组合效果；**原子表命中=本体关键词**（直接引用原子效果即关键词，
            // 引用组合效果中的赋予族=赋予关键词）。非 Grant 原子直接引用无语义，点名跳过。
            // legacy keywords 列兼容读取（迁移前数据/反查失败的引擎专用词），与原子引用合并去重。
            if (entry.effectIds != null && entry.effectIds.Count > 0)
            {
                var resolved = new List<CardEffectData>();
                foreach (var eid in entry.effectIds)
                {
                    var effect = EffectsLibrary.Resolve(eid);
                    if (effect != null) { resolved.Add(effect); continue; }

                    var atom = AtomicEffectTable.GetByHashId(eid);
                    if (atom == null)
                    {
                        TideLog.Warn($"[CardLoader] {entry.id}：effectIds 引用 {eid} 既不在效果库也不在原子表——跳过");
                        continue;
                    }
                    if (Enum.TryParse<AtomicEffectType>(atom.EnumName, out var grantType)
                        && GrantKeywordHandlerFactory.TryGetKeywordId(grantType, out var kwId))
                    {
                        if (!cardData.Keywords.Contains(kwId)) cardData.Keywords.Add(kwId);
                    }
                    else
                    {
                        TideLog.Warn($"[CardLoader] {entry.id}：原子 {atom.EnumName} 非 Grant 族——不可作本体关键词，跳过");
                    }
                }
                cardData.Effects = resolved;
            }

            if (entry.level >= 0) cardData.Level = entry.level;

            // 连接光环声明（三轨制）：无效条目（stat/keyword 双空）装载期即丢弃
            if (entry.linkAuras != null)
                cardData.LinkAuras = entry.linkAuras
                    .Where(a => a != null && (!string.IsNullOrEmpty(a.stat) || !string.IsNullOrEmpty(a.keyword)))
                    .ToList();

            // 效果层光环聚合（2026-09-23 定案）：效果携带的箭头/光环条目并集入卡面
            //（与卡面直书值并集——兼容旧数据）；须在 EnsureCost 前完成（光环费/箭头累乘进计价）
            cardData.AggregateEffectAuras(false);

            // 战斗底盘（2026-09-10 攻/守效果化）：opt-out 与瞬间盈余分配，缺省全 false（自带攻守、退费）
            cardData.NoAttack = entry.noAttack;
            cardData.NoGuard = entry.noGuard;
            cardData.SurplusToSpeed = entry.surplusToSpeed;

            // 结界耐久（2026-09-24 定案）：战斗侧已实装（CounterRules.LoseDurability + 零坚韧 SBA，TideServer V9.c 全流程回归）——本行只做数据采集
            cardData.Durability = entry.durability;

            // 底盘退费落色（2026-10-02 定案：玩家自标）——计价推导口径，不影响运行时支付
            cardData.RefundColor = entry.refundColor;

            // 代价栏（2026-10-04 持久化链）：payload 原子引用 → 卡层 PayloadCost（正式口；
            // legacy 效果级 Costs 兜底不变——卡层已填时 CollectCardSpecialCosts 跳过 legacy 防双收）。
            // 须在 EnsureCost 前（单卡单条违约告警读 PayloadCost）。
            if (entry.payload != null && !string.IsNullOrEmpty(entry.payload.refId))
            {
                cardData.PayloadCost = new CostEntry
                {
                    CostType = (int)CostType.Payload,
                    Value = 1,
                    payload = entry.payload,
                };
            }

            // 统一计价兜底：costList 缺省 → 写入建议档位分布（幂等，非空不动）
            CardCostService.EnsureCost(cardData);

            return cardData;
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
        /// 解析费用位置数组（null → 空 ElementCost；短补零/长截断由 ElementCost 构造统一处理）
        /// </summary>
        private static ElementCost ParseCost(List<float> costList)
        {
            return costList == null ? new ElementCost() : new ElementCost(costList.ToArray());
        }
    }
}
