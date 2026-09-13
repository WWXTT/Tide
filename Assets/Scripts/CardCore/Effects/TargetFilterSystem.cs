using CardCore.Attribute;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCore
{
    // ================================================================
    // 筛选器接口
    // ================================================================

    /// <summary>
    /// 目标筛选器接口
    /// 定义如何从候选列表中筛选合法目标
    /// </summary>
    public interface ITargetFilter
    {
        /// <summary>筛选器显示名称</summary>
        string DisplayName { get; }

        /// <summary>
        /// 从候选列表中筛选合法目标
        /// </summary>
        List<Entity> Filter(List<Entity> candidates, EffectExecutionContext context);
    }

    // ================================================================
    // 内置筛选器实现
    // ================================================================

    /// <summary>己方筛选器</summary>
    public class FriendlyFilter : ITargetFilter
    {
        public string DisplayName => "己方";
        public List<Entity> Filter(List<Entity> candidates, EffectExecutionContext context)
        {
            return candidates.Where(e =>
            {
                if (e is Card card) return card.GetController() == context.Controller;
                return false;
            }).ToList();
        }
    }

    /// <summary>敌方筛选器</summary>
    public class EnemyFilter : ITargetFilter
    {
        public string DisplayName => "敌方";
        public List<Entity> Filter(List<Entity> candidates, EffectExecutionContext context)
        {
            return candidates.Where(e =>
            {
                if (e is Card card) return card.GetController() != context.Controller;
                return false;
            }).ToList();
        }
    }

    /// <summary>卡牌类型筛选器</summary>
    /// <summary>凡躯（Mortal，2026-09-10 神佑定案）：滤除持有神佑（DivineProtection）的实体——
    /// 效果死亡族原子在表里带此 token，神佑角色自动不入候选——新击杀效果零引擎改动（表行带 token 即守约）。
    /// 死亡门口（DeathRules.IsShielded）保留为延迟路径（毒回合计时钟）的安全网。</summary>
    public class MortalFilter : ITargetFilter
    {
        public string DisplayName => "凡躯";

        public List<Entity> Filter(List<Entity> candidates, EffectExecutionContext context)
        {
            return candidates.Where(e => !e.HasKeyword(Attribute.DeathRules.DivineProtection)).ToList();
        }
    }

    public class CardTypeFilter : ITargetFilter
    {
        private readonly Cardtype _cardType;
        public string DisplayName => $"类型:{_cardType}";

        public CardTypeFilter(Cardtype cardType) { _cardType = cardType; }

        public List<Entity> Filter(List<Entity> candidates, EffectExecutionContext context)
        {
            return candidates.Where(e =>
            {
                if (e is IHasSupertype hasType) return hasType.Supertype == _cardType;
                return false;
            }).ToList();
        }
    }

    /// <summary>仅角色（TargetFilter token "Player"，2026-09-13 启用：原休眠 no-op）——
    /// 滤除全部非 Player 实体（生物等卡）。"以角色为作用对象"的效果用（如牺牲原子：
    /// 目标=双方角色，持有者自行选择一个生物效果死亡）。</summary>
    public class RoleOnlyFilter : ITargetFilter
    {
        public string DisplayName => "仅角色";

        public List<Entity> Filter(List<Entity> candidates, EffectExecutionContext context)
        {
            return candidates.Where(e => e is Player).ToList();
        }
    }

    /// <summary>属性比较筛选器（攻击力/生命值大于/小于/等于阈值）</summary>
    public class StatComparisonFilter : ITargetFilter
    {
        public enum ComparisonOp { GreaterThan, LessThan, Equal, GreaterOrEqual, LessOrEqual }

        private readonly string _statName; // "Power" or "Life"
        private readonly ComparisonOp _op;
        private readonly int _threshold;

        public string DisplayName => $"{_statName} {_OpText()} {_threshold}";

        public StatComparisonFilter(string statName, ComparisonOp op, int threshold)
        {
            _statName = statName;
            _op = op;
            _threshold = threshold;
        }

        public List<Entity> Filter(List<Entity> candidates, EffectExecutionContext context)
        {
            return candidates.Where(e => Compare(GetStatValue(e))).ToList();
        }

        private int GetStatValue(Entity e)
        {
            return _statName == "Power" ? e.GetPower() : e.GetLife();
        }

        private bool Compare(int value)
        {
            return _op switch
            {
                ComparisonOp.GreaterThan => value > _threshold,
                ComparisonOp.LessThan => value < _threshold,
                ComparisonOp.Equal => value == _threshold,
                ComparisonOp.GreaterOrEqual => value >= _threshold,
                ComparisonOp.LessOrEqual => value <= _threshold,
                _ => false
            };
        }

        private string _OpText()
        {
            return _op switch
            {
                ComparisonOp.GreaterThan => ">",
                ComparisonOp.LessThan => "<",
                ComparisonOp.Equal => "=",
                ComparisonOp.GreaterOrEqual => ">=",
                ComparisonOp.LessOrEqual => "<=",
                _ => "?"
            };
        }
    }

    /// <summary>关键词筛选器</summary>
    public class KeywordFilter : ITargetFilter
    {
        private readonly string _keyword;
        private readonly bool _mustHave;
        public string DisplayName => _mustHave ? $"拥有:{_keyword}" : $"没有:{_keyword}";

        /// <param name="keyword">关键词名称</param>
        /// <param name="mustHave">true=必须拥有，false=必须没有</param>
        public KeywordFilter(string keyword, bool mustHave = true)
        {
            _keyword = keyword;
            _mustHave = mustHave;
        }

        public List<Entity> Filter(List<Entity> candidates, EffectExecutionContext context)
        {
            return candidates.Where(e => e.HasKeyword(_keyword) == _mustHave).ToList();
        }
    }

    /// <summary>区域筛选器</summary>
    public class ZoneFilter : ITargetFilter
    {
        private readonly Zone _zone;
        public string DisplayName => $"区域:{_zone}";

        public ZoneFilter(Zone zone) { _zone = zone; }

        public List<Entity> Filter(List<Entity> candidates, EffectExecutionContext context)
        {
            return candidates.Where(e => e.GetZone() == _zone).ToList();
        }
    }

    /// <summary>未横置筛选器</summary>
    public class UntappedFilter : ITargetFilter
    {
        public string DisplayName => "未横置";
        public List<Entity> Filter(List<Entity> candidates, EffectExecutionContext context)
        {
            return candidates.Where(e => !e.IsTapped()).ToList();
        }
    }

    /// <summary>已横置筛选器</summary>
    public class TappedFilter : ITargetFilter
    {
        public string DisplayName => "已横置";
        public List<Entity> Filter(List<Entity> candidates, EffectExecutionContext context)
        {
            return candidates.Where(e => e.IsTapped()).ToList();
        }
    }

    /// <summary>生物类型筛选器</summary>
    public class SubtypeFilter : ITargetFilter
    {
        private readonly CardSubtype _subtype;
        public string DisplayName => $"种族:{_subtype}";

        public SubtypeFilter(CardSubtype subtype) { _subtype = subtype; }

        public List<Entity> Filter(List<Entity> candidates, EffectExecutionContext context)
        {
            return candidates.Where(e =>
            {
                if (e is IHasSubtypes hasSubtypes) return hasSubtypes.Subtypes.HasFlag(_subtype);
                return false;
            }).ToList();
        }
    }

    /// <summary>受伤筛选器</summary>
    public class DamagedFilter : ITargetFilter
    {
        public string DisplayName => "已受伤";
        public List<Entity> Filter(List<Entity> candidates, EffectExecutionContext context)
        {
            return candidates.Where(e => e.GetLife() < e.GetMaxLife()).ToList();
        }
    }

    // ================================================================
    // 组合筛选器
    // ================================================================

    /// <summary>
    /// 组合筛选器（AND逻辑）
    /// 所有子筛选器都必须通过
    /// </summary>
    public class CompositeFilter : ITargetFilter
    {
        private readonly List<ITargetFilter> _filters;
        public string DisplayName => string.Join(" + ", _filters.Select(f => f.DisplayName));

        public CompositeFilter(params ITargetFilter[] filters)
        {
            _filters = new List<ITargetFilter>(filters);
        }

        public CompositeFilter(IEnumerable<ITargetFilter> filters)
        {
            _filters = new List<ITargetFilter>(filters);
        }

        public List<Entity> Filter(List<Entity> candidates, EffectExecutionContext context)
        {
            var result = candidates;
            foreach (var filter in _filters)
            {
                result = filter.Filter(result, context);
                if (result.Count == 0) break;
            }
            return result;
        }

        public void AddFilter(ITargetFilter filter) => _filters.Add(filter);
    }

    // ================================================================
    // 目标解析器
    // ================================================================

    /// <summary>
    /// 目标解析器
    /// 负责从 AtomicEffectConfig 的配置生成合法候选目标列表
    /// </summary>
    public class TargetResolver
    {
        private readonly ZoneManager _zoneManager;

        public TargetResolver(ZoneManager zoneManager)
        {
            _zoneManager = zoneManager;
        }

        /// <summary>
        /// 获取候选目标列表（2026-09-10 目标域模型）：按 TargetKind 序号集合组装候选池。
        /// 自己（0）= 源卡自身（2026-09-11 Self 找回——关键词/关键词型效果的专属域）；
        /// 单位种类（1-4）= 战场生物/非生物 + 该侧角色（角色=有生命单位）；
        /// 卡种类（5-16）= 对应功能区域的卡。
        /// </summary>
        public List<Entity> GetCandidates(List<int> kinds, string targetFilter, EffectExecutionContext context)
        {
            var candidates = new List<Entity>();

            if (context.Controller == null || _zoneManager == null || kinds == null)
                return candidates;

            var opponent = context.Controller.Opponent;

            foreach (var kind in kinds)
            {
                if (!System.Enum.IsDefined(typeof(TargetKind), kind)) continue;
                switch ((TargetKind)kind)
                {
                    // 自己：源卡自身（须在战场存活——瞬间在发动区/已死者不入选）
                    case TargetKind.Self:
                        if (context.Source != null && context.Source.IsAlive && context.Source is Card)
                            candidates.Add(context.Source);
                        break;

                    // 单位种类：战场有生命（生物+该侧角色）/ 无生命（战场非生物持久物）
                    case TargetKind.OwnLivingUnit:
                        candidates.AddRange(CreaturesOf(context.Controller));
                        candidates.Add(context.Controller);
                        break;
                    case TargetKind.EnemyLivingUnit:
                        if (opponent != null)
                        {
                            candidates.AddRange(CreaturesOf(opponent));
                            candidates.Add(opponent);
                        }
                        break;
                    case TargetKind.OwnNonLivingUnit:
                        candidates.AddRange(NonLivingOf(context.Controller));
                        break;
                    case TargetKind.EnemyNonLivingUnit:
                        if (opponent != null)
                            candidates.AddRange(NonLivingOf(opponent));
                        break;

                    // 卡种类：对应功能区域
                    default:
                        var (zone, own) = TargetKindRules.ZoneOf(kind);
                        var owner = own ? context.Controller : opponent;
                        if (owner != null)
                            candidates.AddRange(GetZoneCards(owner, zone));
                        break;
                }
            }

            // 去重（同实体可能经多种类重复入池）
            candidates = candidates.Distinct().ToList();

            // 扰魔/潜行过滤搬家（2026-09-13 定案）：候选域保留对方侧扰魔/潜行——
            // "不可成为目标"= 存在此范围但**弹窗不显示**，只有手动选择层滤（TargetResolver.ExcludeUnselectable）；
            // 随机（SelectionMode.Random）与全域（Full）绕过选择、照常命中（范围波及）。
            // 指名路径的硬校验仍在 EffectTargeting.CanTarget。法术护盾在效果执行时消耗（EffectHandlerRegistry），此处不滤。

            // 突袭紊乱限制（定案，与攻击同口径）：紊乱指示物存在期间，其发动的效果不准以玩家为目标
            if (KeywordRules.HasRushSickness(context.Source))
                candidates.RemoveAll(c => c is Player);

            return candidates;
        }

        /// <summary>
        /// 手动选择显示域（2026-09-13 扰魔/潜行定案）：从"选择"候选中隐藏对方侧扰魔/潜行单位——
        /// 弹窗不显示、AI/无头代替选取（自动取前 N）同口径不可选；不改变范围本身
        /// （Full/Random 从完整候选域结算，照常命中）。指名硬校验走 EffectTargeting.CanTarget。
        /// </summary>
        public static List<Entity> ExcludeUnselectable(List<Entity> candidates, Player controller)
        {
            if (candidates == null || candidates.Count == 0) return candidates ?? new List<Entity>();
            return candidates.Where(c =>
            {
                if (!(c is Card card) || controller == null) return true;
                var tc = card.GetController();
                return tc == null || tc == controller
                    || !(card.HasKeyword(KeywordRules.Untargetable) || card.HasKeyword(KeywordRules.Stealth));
            }).ToList();
        }

        /// <summary>edict 原子（2026-09-13）：选择权在目标方——作用对象=角色、持有者自行选择自家单位结算
        /// （牺牲=生物/摒弃=无生命单位）。**豁免帷幕**：帷幕只约束对手的选择，不管目标方自己选（用户定案）。</summary>
        public static bool IsEdict(AtomicEffectType type)
            => type == AtomicEffectType.Sacrifice || type == AtomicEffectType.Abandon;

        /// <summary>
        /// 帷幕收窄（2026-09-13 更名定案：原"嘲讽"→帷幕，**只吸引效果目标**、不拦攻击）——
        /// 选择层口径：施放者对手的战场有存活帷幕卡时，对方侧候选（角色/随从/结界）收窄为帷幕卡；
        /// 己方侧不动；全域/范围波及**不受限**
        /// （"只能以…作为目标"约束的是指定与随机选择，不约束范围）。
        /// edictExempt：牺牲/摒弃类（选择权在目标方）豁免——帷幕=对手无法选择，不包括持有者自选。
        /// 攻击侧由守卫拦截承担（CombatSystem），帷幕不经此口。
        /// 碾压已重定义为攻击溅射（2026-09-13），不再无视帷幕。
        /// </summary>
        public static List<Entity> ApplyTauntRestriction(List<Entity> candidates, EffectExecutionContext context, bool edictExempt = false)
        {
            if (edictExempt) return candidates ?? new List<Entity>();
            if (candidates == null || candidates.Count == 0) return candidates ?? new List<Entity>();
            var controller = context.Controller;
            var opp = controller?.Opponent;
            if (opp == null || context.ZoneManager == null) return candidates;

            var taunts = context.ZoneManager.GetCards(opp, Zone.Battlefield)
                .Where(c => c.IsAlive && c.HasKeyword(KeywordRules.Taunt)).ToList();
            if (taunts.Count == 0) return candidates;

            return candidates.Where(c =>
            {
                var side = c is Card card ? card.GetController() : c as Player;
                if (side != opp) return true;                       // 己方侧不受限
                return c is Card tc && taunts.Contains(tc);          // 对方侧：仅帷幕卡
            }).ToList();
        }

        /// <summary>
        /// 外给目标硬校验收口（2026-09-13 定案修复）：声明期由 UI/AI 直接给定的目标不经候选解析——
        /// 此处补两道与解析路径同口径的检查：①源紊乱不可指角色（与 GetCandidates 域层一致）；
        /// ②对手有帷幕卡时对方侧仅帷幕卡可指（帷幕只吸引效果目标，不拦攻击；
        /// edictExempt=牺牲/摒弃类豁免——选择权在目标方，帷幕不管持有者自选）。
        /// 非法目标剔除（效果对其空转），不整卡拒绝。
        /// </summary>
        public static List<Entity> FilterPreselectedTargets(List<Entity> targets, Entity source, Player controller, ZoneManager zoneManager, bool edictExempt = false)
        {
            if (targets == null || targets.Count == 0) return targets ?? new List<Entity>();

            bool sick = KeywordRules.HasRushSickness(source);
            var opp = controller?.Opponent;
            var taunts = (!edictExempt && opp != null && zoneManager != null)
                ? zoneManager.GetCards(opp, Zone.Battlefield)
                    .Where(c => c.IsAlive && c.HasKeyword(KeywordRules.Taunt)).ToList()
                : null;
            bool hasTaunt = taunts != null && taunts.Count > 0;

            return targets.Where(t =>
            {
                if (t == null) return false;
                if (sick && t is Player) return false;               // 紊乱：不可指角色
                if (hasTaunt)
                {
                    var side = t is Card card ? card.GetController() : t as Player;
                    if (side == opp && !(t is Card tc && taunts.Contains(tc)))
                        return false;                                 // 嘲讽：对方侧仅嘲讽卡
                }
                return true;
            }).ToList();
        }

        /// <summary>某玩家的战场生物（有生命单位中的卡部分；角色由调用方补）。</summary>
        private List<Entity> CreaturesOf(Player player)
        {
            var list = new List<Entity>();
            foreach (var c in GetZoneCards(player, Zone.Battlefield))
                if (c is IHasSupertype st && st.Supertype == Cardtype.Creature)
                    list.Add(c);
            return list;
        }

        /// <summary>某玩家的战场非生物持久物（无生命单位）。</summary>
        private List<Entity> NonLivingOf(Player player)
        {
            var list = new List<Entity>();
            foreach (var c in GetZoneCards(player, Zone.Battlefield))
                if (c is IHasSupertype st && st.Supertype != Cardtype.Creature)
                    list.Add(c);
            return list;
        }

        /// <summary>从筛选串中解析候选分区 token（Battlefield/Hand/Graveyard/Deck/Exile/Activation/ElementPool），缺省战场。</summary>
        private static Zone ExtractZone(string filterString)
        {
            if (string.IsNullOrEmpty(filterString)) return Zone.Battlefield;
            foreach (var token in filterString.Split(','))
            {
                switch (token.Trim())
                {
                    case "Hand": return Zone.Hand;
                    case "Graveyard": return Zone.Graveyard;
                    case "Deck": return Zone.Deck;
                    case "Exile": return Zone.Exile;
                    case "Battlefield": return Zone.Battlefield;
                    case "Activation": return Zone.Activation; // 发动中的卡（反制指向发动区）
                    case "ElementPool": return Zone.ElementPool; // 地牌区（池内地牌）
                }
            }
            return Zone.Battlefield;
        }

        /// <summary>筛选串是否含指定 token（如摧毁域 "NoLife"）。</summary>
        private static bool HasFilterToken(string filterString, string token)
        {
            if (string.IsNullOrEmpty(filterString)) return false;
            foreach (var t in filterString.Split(','))
                if (t.Trim() == token) return true;
            return false;
        }

        /// <summary>
        /// 应用筛选器链
        /// </summary>
        public List<Entity> ApplyFilters(List<Entity> candidates, List<ITargetFilter> filters, EffectExecutionContext context)
        {
            if (filters == null || filters.Count == 0)
                return candidates;

            var composite = new CompositeFilter(filters);
            return composite.Filter(candidates, context);
        }

        /// <summary>
        /// 解析 AtomicEffectConfig 中的 TargetFilter 字符串为筛选器列表
        /// </summary>
        public List<ITargetFilter> ParseFilters(string filterString)
        {
            var filters = new List<ITargetFilter>();
            if (string.IsNullOrEmpty(filterString)) return filters;

            foreach (var token in filterString.Split(','))
            {
                var trimmed = token.Trim();
                switch (trimmed)
                {
                    case "NoRole": // 仅生物（排除角色）——2026-09-10 自 Creature 改名（语义自解释）
                        filters.Add(new CardTypeFilter(Cardtype.Creature));
                        break;
                    case "Mortal": // 凡躯：滤除神佑持有者（神佑的 TargetFilter 实现，2026-09-10）
                        filters.Add(new MortalFilter());
                        break;
                    case "Stealth": // 仅指潜行中（状态类过滤定案：仅指持有该状态者）
                        filters.Add(new KeywordFilter(KeywordRules.Stealth, true));
                        break;
                    case "Untargetable": // 仅指免疫（辟邪）持有者
                        filters.Add(new KeywordFilter(KeywordRules.Untargetable, true));
                        break;
                    case "Player":
                        // 仅角色（2026-09-13 启用，原休眠 no-op）：滤除生物等卡——牺牲原子（持有者选生物牺牲）等以角色为作用对象的效果
                        filters.Add(new RoleOnlyFilter());
                        break;
                    case "Hand":
                    case "Graveyard":
                    case "Deck":
                    case "Exile":
                    case "Battlefield":
                    case "Activation":
                        // 分区 token：候选池已按分区取（见 GetCandidates.ExtractZone），此处不再过滤
                        break;
                    case "Untapped":
                        filters.Add(new UntappedFilter());
                        break;
                    case "Tapped":
                        filters.Add(new TappedFilter());
                        break;
                    case "Damaged":
                        filters.Add(new DamagedFilter());
                        break;
                    case "Friendly":
                        filters.Add(new FriendlyFilter());
                        break;
                    case "Enemy":
                        filters.Add(new EnemyFilter());
                        break;
                    // 支持格式: Power>5, Life<3 等
                    default:
                        var parsed = TryParseStatComparison(trimmed);
                        if (parsed != null) filters.Add(parsed);
                        break;
                }
            }

            return filters;
        }

        private ITargetFilter TryParseStatComparison(string token)
        {
            // 格式: "Power>5" or "Life<=3"
            foreach (StatComparisonFilter.ComparisonOp op in Enum.GetValues(typeof(StatComparisonFilter.ComparisonOp)))
            {
                string opStr = op switch
                {
                    StatComparisonFilter.ComparisonOp.GreaterThan => ">",
                    StatComparisonFilter.ComparisonOp.LessThan => "<",
                    StatComparisonFilter.ComparisonOp.Equal => "=",
                    StatComparisonFilter.ComparisonOp.GreaterOrEqual => ">=",
                    StatComparisonFilter.ComparisonOp.LessOrEqual => "<=",
                    _ => ""
                };

                int idx = token.IndexOf(opStr);
                if (idx > 0)
                {
                    string statName = token.Substring(0, idx);
                    if ((statName == "Power" || statName == "Life") &&
                        int.TryParse(token.Substring(idx + opStr.Length), out int threshold))
                    {
                        return new StatComparisonFilter(statName, op, threshold);
                    }
                }
            }
            return null;
        }

        private List<Entity> GetZoneCards(Player player, Zone zone)
        {
            if (player == null || _zoneManager == null) return new List<Entity>();
            var container = _zoneManager.GetZoneContainer(player);
            if (container == null) return new List<Entity>();
            return container.GetCards(zone).Cast<Entity>().ToList();
        }
    }
}
