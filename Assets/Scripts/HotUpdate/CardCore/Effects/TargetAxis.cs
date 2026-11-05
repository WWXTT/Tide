using System;
using System.Collections.Generic;
using System.Linq;
using CardCore.Attribute;

namespace CardCore
{
    /// <summary>
    /// 目标区域（目标域重做 P0，2026-11-05 定案·docs/目标域契约.md）：22 token =
    /// 自己 + 3侧"角色" + 3侧×6区。**无发动区**（E1-1/S3b：反制=响应绑定栈顶，不走目标域）。
    /// 角色是**区域**（Player 实体），不是类别；枚举不参与序列化；只许尾部追加、永不重排。
    /// </summary>
    public enum TargetRegion
    {
        /// <summary>自己=源卡（特殊：不看类别——selector 无类别段）</summary>
        Self = 0,
        OwnBattlefield = 1,   // 己方战场
        EnemyBattlefield = 2, // 对方战场
        BothBattlefield = 3,  // 双方战场
        OwnHand = 4,           // 己方手牌
        EnemyHand = 5,         // 对方手牌
        BothHand = 6,          // 双方手牌
        OwnDeck = 7,           // 己方牌库
        EnemyDeck = 8,         // 对方牌库
        BothDeck = 9,          // 双方牌库
        OwnGraveyard = 10,     // 己方墓地
        EnemyGraveyard = 11,   // 对方墓地
        BothGraveyard = 12,    // 双方墓地
        OwnExile = 13,         // 己方除外区
        EnemyExile = 14,       // 对方除外区
        BothExile = 15,        // 双方除外区
        OwnElementPool = 16,   // 己方元素池
        EnemyElementPool = 17, // 对方元素池
        BothElementPool = 18,  // 双方元素池
        /// <summary>己方角色（Player 实体——区域 selector，不得带类别）</summary>
        OwnCharacter = 19,
        /// <summary>对方角色</summary>
        EnemyCharacter = 20,
        /// <summary>双方角色</summary>
        BothCharacter = 21,
    }

    /// <summary>区域侧别（角色/区域的分解维；四人局扩展位=Opposing 的席位解析，非新增侧别）。</summary>
    public enum RegionSide
    {
        Own = 0,
        Opposing = 1,
        Both = 2,
    }

    /// <summary>
    /// TargetRegion 静态规则：中文 token 解析/格式化、Zone+Side 分解（候选取件与表列单源）。
    /// token=「侧别+区域基名」复合词；角色三 token Zone=None（取件特判 Player 实体）。
    /// </summary>
    public static class TargetRegionRules
    {
        private sealed class Row
        {
            public readonly TargetRegion Region;
            public readonly string Zh;
            public readonly Zone Zone;
            public readonly RegionSide Side;
            public Row(TargetRegion region, string zh, Zone zone, RegionSide side)
            { Region = region; Zh = zh; Zone = zone; Side = side; }
        }

        // ---- 唯一真相表（22 token = 自己 + 3侧角色 + 3侧×6区；无发动区）----
        private static readonly Row[] Rows =
        {
            new Row(TargetRegion.Self,             "自己",       Zone.None,         RegionSide.Own), // 特殊：源卡，Zone/Side 不消费
            new Row(TargetRegion.OwnBattlefield,   "己方战场",   Zone.Battlefield,  RegionSide.Own),
            new Row(TargetRegion.EnemyBattlefield, "对方战场",   Zone.Battlefield,  RegionSide.Opposing),
            new Row(TargetRegion.BothBattlefield,  "双方战场",   Zone.Battlefield,  RegionSide.Both),
            new Row(TargetRegion.OwnHand,          "己方手牌",   Zone.Hand,         RegionSide.Own),
            new Row(TargetRegion.EnemyHand,        "对方手牌",   Zone.Hand,         RegionSide.Opposing),
            new Row(TargetRegion.BothHand,         "双方手牌",   Zone.Hand,         RegionSide.Both),
            new Row(TargetRegion.OwnDeck,          "己方牌库",   Zone.Deck,         RegionSide.Own),
            new Row(TargetRegion.EnemyDeck,        "对方牌库",   Zone.Deck,         RegionSide.Opposing),
            new Row(TargetRegion.BothDeck,         "双方牌库",   Zone.Deck,         RegionSide.Both),
            new Row(TargetRegion.OwnGraveyard,     "己方墓地",   Zone.Graveyard,    RegionSide.Own),
            new Row(TargetRegion.EnemyGraveyard,   "对方墓地",   Zone.Graveyard,    RegionSide.Opposing),
            new Row(TargetRegion.BothGraveyard,    "双方墓地",   Zone.Graveyard,    RegionSide.Both),
            new Row(TargetRegion.OwnExile,         "己方除外区", Zone.Exile,        RegionSide.Own),
            new Row(TargetRegion.EnemyExile,       "对方除外区", Zone.Exile,        RegionSide.Opposing),
            new Row(TargetRegion.BothExile,        "双方除外区", Zone.Exile,        RegionSide.Both),
            new Row(TargetRegion.OwnElementPool,   "己方元素池", Zone.ElementPool,  RegionSide.Own),
            new Row(TargetRegion.EnemyElementPool, "对方元素池", Zone.ElementPool,  RegionSide.Opposing),
            new Row(TargetRegion.BothElementPool,  "双方元素池", Zone.ElementPool,  RegionSide.Both),
            new Row(TargetRegion.OwnCharacter,     "己方角色",   Zone.None,         RegionSide.Own),    // 角色=区域（Player 实体）
            new Row(TargetRegion.EnemyCharacter,   "对方角色",   Zone.None,         RegionSide.Opposing),
            new Row(TargetRegion.BothCharacter,    "双方角色",   Zone.None,         RegionSide.Both),
        };

        private static readonly Dictionary<string, Row> ByZh = new Dictionary<string, Row>(StringComparer.Ordinal);
        private static readonly Dictionary<TargetRegion, Row> ByRegion = new Dictionary<TargetRegion, Row>();

        static TargetRegionRules()
        {
            foreach (var row in Rows)
            {
                ByZh[row.Zh] = row;
                ByRegion[row.Region] = row;
            }
        }

        /// <summary>token 数（词表规模——验证器断言口）。</summary>
        public static int Count => Rows.Length;

        /// <summary>中文 token 解析（trim 后精确匹配；空/未知返回 false，调用方告警不猜）。</summary>
        public static bool TryParse(string token, out TargetRegion region)
        {
            region = default(TargetRegion);
            var t = token?.Trim();
            if (string.IsNullOrEmpty(t)) return false;
            if (!ByZh.TryGetValue(t, out var row)) return false;
            region = row.Region;
            return true;
        }

        /// <summary>token 中文文本（越界枚举值回退枚举名）。与 TryParse 互逆。</summary>
        public static string ZhNameOf(TargetRegion region)
            => ByRegion.TryGetValue(region, out var row) ? row.Zh : region.ToString();

        /// <summary>是否角色区域（selector 取件=Player 实体；不得带类别）。</summary>
        public static bool IsCharacter(TargetRegion region)
            => ByRegion.TryGetValue(region, out var row) && row.Zone == Zone.None && row.Region != TargetRegion.Self;

        /// <summary>区域 → 功能区（Self/角色 = None——不走区取件）。</summary>
        public static Zone ZoneOf(TargetRegion region)
            => ByRegion.TryGetValue(region, out var row) ? row.Zone : Zone.None;

        /// <summary>区域 → 侧别（Self 的返回值不消费）。</summary>
        public static RegionSide SideOf(TargetRegion region)
            => ByRegion.TryGetValue(region, out var row) ? row.Side : RegionSide.Own;

        /// <summary>CSV 解析（表行区域并集/验证器共用）：trim 逐 token TryParse，空段与坏 token
        /// 静默跳过、成功去重。</summary>
        public static List<TargetRegion> ParseCsv(string csv)
        {
            var result = new List<TargetRegion>();
            if (string.IsNullOrEmpty(csv)) return result;
            foreach (var token in csv.Split(','))
            {
                var t = token.Trim();
                if (t.Length == 0) continue;
                if (TryParse(t, out var r) && !result.Contains(r)) result.Add(r);
            }
            return result;
        }

        /// <summary>区域集规范文本（CSV 中文——报告用）。</summary>
        public static string ZhCsv(IEnumerable<TargetRegion> regions)
            => string.Join(",", (regions ?? new List<TargetRegion>()).Select(ZhNameOf));
    }

    /// <summary>
    /// 目标类别（目标域重做 P0）：4 token——卡=区域内全选（不按卡型滤）；场地=结界子类
    /// （IsFieldCard 标记，不入类别）；**无地牌类别**（地牌=元素池区域内的卡）；**无角色类别**
    /// （角色是区域 selector）。Flags：多类别并集；Card 位=该 selector 不按卡型过滤。
    /// </summary>
    [Flags]
    public enum TargetCategory : int
    {
        None = 0,
        Creature = 1 << 0,     // 生物（Supertype==Creature——含池内生物型地牌，按卡型判）
        Enchantment = 1 << 1,  // 结界（Supertype==Enchantment；场地卡=本类子集）
        Spell = 1 << 2,        // 法术（Supertype==Spell）
        Card = 1 << 3,         // 卡=区域内全选（该位出现即不按卡型滤）
        All = Creature | Enchantment | Spell | Card,
    }

    /// <summary>TargetCategory 静态规则：中文 token ↔ Flags（4 token；表 Domain selector 类别段 CSV）。</summary>
    public static class TargetCategoryRules
    {
        private static readonly (TargetCategory Cat, string Zh)[] Rows =
        {
            (TargetCategory.Card,       "卡"),
            (TargetCategory.Creature,   "生物"),
            (TargetCategory.Enchantment,"结界"),
            (TargetCategory.Spell,      "法术"),
        };

        /// <summary>token 解析（trim 精确匹配；未知 false）。</summary>
        public static bool TryParse(string token, out TargetCategory category)
        {
            category = TargetCategory.None;
            var t = token?.Trim();
            if (string.IsNullOrEmpty(t)) return false;
            foreach (var row in Rows)
                if (row.Zh == t) { category = row.Cat; return true; }
            return false;
        }

        /// <summary>CSV 解析 → Flags 并集（坏 token 静默跳过——调用方点名告警）。</summary>
        public static TargetCategory ParseCsv(string csv)
        {
            var cats = TargetCategory.None;
            if (string.IsNullOrEmpty(csv)) return cats;
            foreach (var token in csv.Split(','))
                if (TryParse(token, out var c)) cats |= c;
            return cats;
        }

        /// <summary>Flags 规范文本（按词表序展开 CSV；None="（缺类别）"——卡区域 selector 必带类别，此值即非法态）。</summary>
        public static string ZhOf(TargetCategory categories)
        {
            var parts = Rows.Where(r => categories.HasFlag(r.Cat)).Select(r => r.Zh).ToList();
            return parts.Count == 0 ? "（缺类别）" : string.Join(",", parts);
        }
    }

    /// <summary>
    /// 实体 → 卡型判定单源（目标域重做 P0）：只判卡型（生物/结界/法术）——
    /// 地牌双籍与角色位已随类别轴收敛删除（池内生物型地牌按卡型=生物可被 ":生物" 选中；
    /// 角色经区域 selector 取件，不经类别）。非三型卡=None（仅 ":卡" 可选）。
    /// </summary>
    public static class EntityCategoryRules
    {
        /// <summary>实体卡型（Player/非 Card=None——角色不经类别路径）。</summary>
        public static TargetCategory Of(Entity entity)
            => entity is Card card ? SupertypeCategoryOf(card) : TargetCategory.None;

        /// <summary>卡型（Supertype→Flags；正式三型之外=None，仅「卡」类别可选）。</summary>
        public static TargetCategory SupertypeCategoryOf(Card card)
        {
            if (!(card is IHasSupertype t)) return TargetCategory.None;
            switch (t.Supertype)
            {
                case Cardtype.Creature: return TargetCategory.Creature;
                case Cardtype.Enchantment: return TargetCategory.Enchantment;
                case Cardtype.Spell: return TargetCategory.Spell;
                default: return TargetCategory.None;
            }
        }

        /// <summary>类别命中（Card 位=全选；None 类别集恒 false）。</summary>
        public static bool Matches(Entity entity, TargetCategory categories)
        {
            if ((categories & TargetCategory.Card) != 0) return true;
            return (Of(entity) & categories) != 0;
        }

        /// <summary>场地=结界子类标记（CardExtensions.IsFieldCard 单源；光环上限已实现，不重做）。</summary>
        public static bool IsFieldSubclass(Card card) => card != null && card.IsFieldCard();
    }

    /// <summary>单个目标选择器：区域 + 类别并集（契约：selector := region (":" category ("," category)*)?）。
    /// 自己/角色 selector 恒 Card（无类别段——取件不看卡型）；卡区域 selector 必带类别。</summary>
    public sealed class TargetSelector
    {
        public TargetRegion Region;
        public TargetCategory Categories = TargetCategory.Card;
    }

    /// <summary>目标域声明值（目标域重做 P0）：selector 列表 + 残余属性过滤。</summary>
    public sealed class TargetAxis
    {
        /// <summary>selector 列表（";" 分隔；自己/区域 selector 可并存，各自独立取件）。</summary>
        public readonly List<TargetSelector> Selectors = new List<TargetSelector>();

        /// <summary>残余属性过滤（TargetFilter 中未被类别吸收的 token 原样保留——契约只留属性 token）。</summary>
        public string Filter = "";

        public string Zh => string.Join("；", Selectors.Select(s =>
            TargetRegionRules.ZhNameOf(s.Region)
            + (TargetRegionRules.IsCharacter(s.Region) || s.Region == TargetRegion.Self
                ? "" : ":" + TargetCategoryRules.ZhOf(s.Categories))));
    }

    /// <summary>
    /// 域语法规则（目标域重做 P0·契约 docs/目标域契约.md）：
    /// domain := selector (";" selector)*；selector := region (":" category ("," category)*)?。
    /// 硬规则全拒（不猜不回落）：角色 selector 不得带类别；卡区域必须带类别；禁裸 X方；禁发动区；
    /// 声明新 Domain 的行 TargetFilter 禁类别性 token（NoRole/NotRole/Player/Living/Field——FilterConflicts）。
    /// 旧 EffectDomain 词表 token（无 ":"/";" 形态）天然解析失败 → 调用方回落 TargetKinds legacy。
    /// </summary>
    public static class TargetAxisRules
    {
        /// <summary>类别性 filter token（新 Domain 行禁用——类别语义由 Domain 承载，双写即冲突）。</summary>
        private static readonly string[] CategoryFilterTokens = { "NoRole", "NotRole", "Player", "Living", "Field" };

        /// <summary>域串解析（新语法，**纯语法判定**——撞形不特判）。合法返回 selector 列表轴；
        /// 任何规则违反返回 false（axis=null）。旧 EffectDomain 词表的"X方角色"三 token 与新语法
        /// selector 撞形——按语法判即合法（候选语义与旧 Player 域等价，走新路径无行为差）；
        /// 其余旧 token（多 token CSV/裸区域）天然违反"卡区域必须带类别"被拒→调用方回落 legacy。
        /// 分号兼容全角"；"。</summary>
        public static bool TryParseDomain(string domain, out TargetAxis axis)
        {
            axis = null;
            if (string.IsNullOrEmpty(domain)) return false;
            var result = new TargetAxis();
            foreach (var rawSelector in domain.Split(';', '；'))
            {
                var text = rawSelector.Trim();
                if (text.Length == 0) return false; // 空段（";;" / 首尾分号）非法

                // ":" 分段（region : categories）——最多一个冒号
                var parts = text.Split(':');
                if (parts.Length > 2) return false;
                if (!TargetRegionRules.TryParse(parts[0], out var region)) return false; // 裸 X方/坏 token/发动区全在此拒
                if (TargetRegionRules.ZoneOf(region) == Zone.Activation) return false;  // 双保险（词表已无此区）

                var selector = new TargetSelector { Region = region };
                if (parts.Length == 2)
                {
                    // 带类别段：自己/角色 selector 不得带类别（契约规则 1）
                    if (region == TargetRegion.Self || TargetRegionRules.IsCharacter(region)) return false;
                    var cats = TargetCategory.None;
                    foreach (var rawCat in parts[1].Split(','))
                    {
                        if (!TargetCategoryRules.TryParse(rawCat, out var c)) return false; // 坏类别 token
                        cats |= c;
                    }
                    if (cats == TargetCategory.None) return false; // ":,," 形态
                    selector.Categories = cats;
                }
                else
                {
                    // 无类别段：卡区域 selector 必须带类别（契约规则 2）——自己/角色例外（恒 Card）
                    if (region != TargetRegion.Self && !TargetRegionRules.IsCharacter(region)) return false;
                    selector.Categories = TargetCategory.Card;
                }
                result.Selectors.Add(selector);
            }
            if (result.Selectors.Count == 0) return false;
            axis = result;
            return true;
        }

        /// <summary>行级冲突检查（契约规则 5）：声明了新语法 Domain 的行，TargetFilter 含类别性 token = 冲突。
        /// （filter 串本身不区分新旧——只在新 Domain 行上判禁。）</summary>
        public static bool FilterConflicts(string filter)
        {
            if (string.IsNullOrEmpty(filter)) return false;
            foreach (var raw in filter.Split(','))
            {
                var t = raw.Trim();
                foreach (var banned in CategoryFilterTokens)
                    if (t == banned) return true;
            }
            return false;
        }

        /// <summary>entry.kinds 区域收窄（契约 §5：kinds 收窄区域**与角色 selector 的侧别**，
        /// 类别取表行 Domain selector 原值）：逐 selector 按 kinds 的区域侧别集改写/删除——
        /// Both 区域落在单侧收窄集 → 改写为该侧区域；无交集 → 删该 selector；角色 selector 按
        /// 生命单位侧别同式收窄（1=OwnLivingUnit 授己方、2=EnemyLivingUnit 授对方；3/4 无生命
        /// 单位不授予角色侧别）；kinds 空/含 0=自己 → 不收窄。返回收窄后新轴（入参不变）。</summary>
        public static TargetAxis NarrowByKinds(TargetAxis axis, List<int> kinds)
        {
            if (axis == null || kinds == null || kinds.Count == 0) return axis;
            bool selfOnly = kinds.All(k => k == (int)TargetKind.Self);
            if (selfOnly) return axis; // 纯自己域：收窄无意义（自己 selector 不分区）

            // 角色 selector 侧别授权（契约 §5）：只有生命单位 kind（1/2）授予角色侧别
            bool roleOwn = kinds.Contains((int)TargetKind.OwnLivingUnit);
            bool roleEnemy = kinds.Contains((int)TargetKind.EnemyLivingUnit);

            // kinds → (zone, 侧别允许集)：1/2/3/4 都归战场侧别；区域 kind 归对应区侧别
            var allowed = new Dictionary<Zone, bool>(); // zone -> 允许对方侧？（true=双侧均许）
            var ownZones = new HashSet<Zone>();
            var enemyZones = new HashSet<Zone>();
            foreach (var k in kinds)
            {
                Zone zone; bool own;
                switch ((TargetKind)k)
                {
                    case TargetKind.OwnLivingUnit:
                    case TargetKind.OwnNonLivingUnit:
                        zone = Zone.Battlefield; own = true; break;
                    case TargetKind.EnemyLivingUnit:
                    case TargetKind.EnemyNonLivingUnit:
                        zone = Zone.Battlefield; own = false; break;
                    default:
                        var (kZone, kOwn) = TargetKindRules.ZoneOf(k);
                        if (kZone == Zone.None) continue; // 0=自己已上方分流；未知 kind 跳过
                        zone = kZone; own = kOwn; break;
                }
                if (own) ownZones.Add(zone);
                else enemyZones.Add(zone);
            }

            var result = new TargetAxis { Filter = axis.Filter };
            foreach (var s in axis.Selectors)
            {
                if (s.Region == TargetRegion.Self) { result.Selectors.Add(s); continue; } // 自己 selector 不受区域收窄
                if (TargetRegionRules.IsCharacter(s.Region))
                {
                    // 角色 selector 按生命单位侧别收窄（不再无条件保留）：
                    // 双方角色=双许留/仅己改己方/仅敌改对方/全无删；己方角色仅己方许留；对方角色仅对方许留
                    switch (TargetRegionRules.SideOf(s.Region))
                    {
                        case RegionSide.Both:
                            if (roleOwn && roleEnemy) result.Selectors.Add(s);
                            else if (roleOwn) result.Selectors.Add(new TargetSelector { Region = TargetRegion.OwnCharacter, Categories = s.Categories });
                            else if (roleEnemy) result.Selectors.Add(new TargetSelector { Region = TargetRegion.EnemyCharacter, Categories = s.Categories });
                            break; // 双侧皆无授权 → 删
                        case RegionSide.Own:
                            if (roleOwn) result.Selectors.Add(s);
                            break;
                        case RegionSide.Opposing:
                            if (roleEnemy) result.Selectors.Add(s);
                            break;
                    }
                    continue;
                }
                var zone = TargetRegionRules.ZoneOf(s.Region);
                var side = TargetRegionRules.SideOf(s.Region);
                bool ownOk = ownZones.Contains(zone);
                bool enemyOk = enemyZones.Contains(zone);
                switch (side)
                {
                    case RegionSide.Both:
                        if (ownOk && enemyOk) result.Selectors.Add(s);
                        else if (ownOk) result.Selectors.Add(new TargetSelector { Region = OwnOf(s.Region), Categories = s.Categories });
                        else if (enemyOk) result.Selectors.Add(new TargetSelector { Region = EnemyOf(s.Region), Categories = s.Categories });
                        break; // 双侧都无 → 删
                    case RegionSide.Own:
                        if (ownOk) result.Selectors.Add(s);
                        break;
                    case RegionSide.Opposing:
                        if (enemyOk) result.Selectors.Add(s);
                        break;
                }
            }
            return result;
        }

        /// <summary>双侧区域 → 己方单侧区域（同区；非双侧原样返回）。</summary>
        private static TargetRegion OwnOf(TargetRegion region)
        {
            foreach (var r in Enum.GetValues(typeof(TargetRegion)).Cast<TargetRegion>())
                if (TargetRegionRules.ZoneOf(r) == TargetRegionRules.ZoneOf(region)
                    && TargetRegionRules.SideOf(r) == RegionSide.Own)
                    return r;
            return region;
        }

        /// <summary>双侧区域 → 对方单侧区域（同区；非双侧原样返回）。</summary>
        private static TargetRegion EnemyOf(TargetRegion region)
        {
            foreach (var r in Enum.GetValues(typeof(TargetRegion)).Cast<TargetRegion>())
                if (TargetRegionRules.ZoneOf(r) == TargetRegionRules.ZoneOf(region)
                    && TargetRegionRules.SideOf(r) == RegionSide.Opposing)
                    return r;
            return region;
        }
    }

    /// <summary>
    /// 目标域候选解析（目标域重做 P0）：逐 selector 取件——自己=源卡（须在场存活）、
    /// 角色=Player 实体（按侧别）、卡区域=区卡（战场滤死亡——与 CreaturesOf/NonLivingOf 同口径）
    /// → 类别过滤（Card 位=全选）→ 突袭紊乱剔角色 → 属性过滤（TargetResolver.ParseFilters 同链）。
    /// 消费口：EffectHandlerRegistry.ResolveCandidates(axis)——axis=null 回落旧 kinds/Domain 口径。
    /// </summary>
    public static class TargetAxisResolver
    {
        /// <summary>真实消费计数（运行时证据口）：GetCandidates 每次被调用 ++。</summary>
        public static int LiveResolutions;

        /// <summary>域轴 → 候选实体集。</summary>
        public static List<Entity> GetCandidates(TargetAxis axis, EffectExecutionContext context)
        {
            LiveResolutions++;
            var result = new List<Entity>();
            if (axis == null || context == null || context.Controller == null || context.ZoneManager == null)
                return result;

            foreach (var selector in axis.Selectors)
            {
                if (selector == null) continue;
                if (selector.Region == TargetRegion.Self)
                {
                    if (context.Source != null && context.Source.IsAlive && context.Source is Card self)
                        result.Add(self);
                    continue;
                }
                if (TargetRegionRules.IsCharacter(selector.Region))
                {
                    result.AddRange(SidesOf(selector.Region, context.Controller).Where(p => p != null));
                    continue;
                }
                var zone = TargetRegionRules.ZoneOf(selector.Region);
                // 施放中的卡不得成为任何手牌域目标（含自己的代价）——单口同口径：
                // TargetFilterSystem.GetZoneCards（旧 kinds 路径）同款剔除（IsPendingCast 单口）。
                // 语义：施放中的卡在规则上已不在手牌（相当于已上链），不可被任何手牌域目标指到
                //——否则「弃牌代价可以弃掉自己」＝零代价发动。
                // 空引用防御：训练/无头无 GameCore 实例时放行（不剔除）。
                var pendingCastStack = zone == Zone.Hand ? GameCore.Instance?.StackEngine : null;
                foreach (var side in SidesOf(selector.Region, context.Controller))
                {
                    if (side == null) continue;
                    foreach (var card in context.ZoneManager.GetCards(side, zone))
                    {
                        if (zone == Zone.Battlefield && !card.IsAlive) continue; // 死亡窗口死卡仍列战场区
                        if (pendingCastStack != null && pendingCastStack.IsPendingCast(card)) continue;
                        result.Add(card);
                    }
                }
            }
            result = result.Distinct().ToList();

            // 类别过滤（契约 §1：类别只作用于卡区域 selector 的取件）——角色实体不经类别；
            // 源卡仅当 axis **含 Self selector** 时豁免（Self=源卡不看类别——无 Self selector 的域
            // 不得把源卡放行，否则「己方战场:结界」会漏进源生物）。
            if (axis.Selectors.Any(s => s.Region != TargetRegion.Self
                                        && !TargetRegionRules.IsCharacter(s.Region)
                                        && (s.Categories & TargetCategory.Card) == 0))
            {
                bool selfInAxis = axis.Selectors.Any(s => s.Region == TargetRegion.Self);
                result.RemoveAll(e =>
                {
                    if (e is Player) return false;                                  // 角色不经类别
                    if (selfInAxis && ReferenceEquals(e, context.Source)) return false; // 仅 Self 域豁免源卡
                    return !axis.Selectors.Any(s =>
                        s.Region != TargetRegion.Self && !TargetRegionRules.IsCharacter(s.Region)
                        && EntityCategoryRules.Matches(e, s.Categories));
                });
            }

            // 突袭紊乱：不可指角色（旧 GetCandidates 域层末端同口径）
            if (CardCore.Attribute.KeywordRules.HasRushSickness(context.Source))
                result.RemoveAll(e => e is Player);

            // 属性过滤（残余 Filter——旧路径同链同序）
            var resolver = new TargetResolver(context.ZoneManager);
            var filters = resolver.ParseFilters(axis.Filter);
            if (filters.Count > 0)
                result = resolver.ApplyFilters(result, filters, context);
            return result;
        }

        /// <summary>区域 → 座位集（侧别展开；Both=己方+全部对立座位；角色区域同式取 Player）。</summary>
        public static IEnumerable<Player> SidesOf(TargetRegion region, Player controller)
        {
            var side = TargetRegionRules.SideOf(region);
            if (side == RegionSide.Own || side == RegionSide.Both) yield return controller;
            if (side == RegionSide.Opposing || side == RegionSide.Both)
                foreach (var opp in OpponentsOf(controller)) yield return opp;
        }

        /// <summary>对立座位解析（**四人局扩展缝**：当前双人局=唯一对手）。</summary>
        private static IEnumerable<Player> OpponentsOf(Player controller)
        {
            var opp = controller?.Opponent;
            if (opp != null) yield return opp;
        }
    }
}
