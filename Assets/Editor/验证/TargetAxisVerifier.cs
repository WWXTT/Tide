using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.Attribute;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace CardCore.Editor.Tests
{
    /// <summary>
    /// 目标域契约校验（目标域重做 P0，2026-11-05·docs/目标域契约.md 唯一真相源）：
    /// ■ ① 词表：22 区域 token（自己+3侧角色+3侧×6区；角色 Zone=None；无发动区）
    ///   + 4 类别 token（卡/生物/结界/法术；无角色/地牌类别）。
    /// ■ ② 契约解析：合法样例（多 selector/类别并集/自己/角色）全过；
    ///   非法全拒——裸 X方、卡区域无类别、角色/自己带类别、发动区、坏 token、空段。
    /// ■ ③ 区域收窄：entry.kinds 收窄区域**与角色 selector 侧别**（契约 §5：1/2 生命单位授角色
    ///   侧别、3/4 不授予；紊乱样例：双方战场:生物 + kind[2] → 对方战场:生物；Both 双侧保持；
    ///   无交删 selector；空 kinds 不收窄）。
    /// ■ ④ 候选取件（夹具）：selector 逐类正确性（生物/结界/法术/卡/角色/自己/属性 Filter）。
    /// ■ ⑤ 全表守卫：153 行（2026-11-05 硬切 B2：全体两行已删）——新语法合法/无目标二分。
    /// ■ ⑥ 生产链证据：converter 新 Domain 产轴+收窄 + executor 结算 LiveResolutions&gt;0。
    /// </summary>
    public static class TargetAxisVerifier
    {
        private sealed class Item
        {
            public readonly string Id;
            public bool Fail;
            public readonly List<string> Lines = new List<string>();
            public Item(string id) { Id = id; }
            public void FailWith(string s) { Fail = true; Lines.Add(s); }
        }

        private static readonly List<Item> Items = new List<Item>();

        [MenuItem("Tools/目标域契约校验（P0）")]
        public static void Run()
        {
            Items.Clear();
            foreach (var id in new[] { "1", "2", "3", "4", "5", "6" }) Of(id);
            AtomicEffectTable.Reload();

            Debug.Log("[域契约] ===== ① 词表 =====");
            try { SectionWords(); } catch (Exception ex) { Of("1").FailWith("段异常中断：" + ex); }
            Debug.Log("[域契约] ===== ② 契约解析（合法/非法） =====");
            try { SectionContract(); } catch (Exception ex) { Of("2").FailWith("段异常中断：" + ex); }
            Debug.Log("[域契约] ===== ③ 区域收窄 =====");
            try { SectionNarrow(); } catch (Exception ex) { Of("3").FailWith("段异常中断：" + ex); }
            Debug.Log("[域契约] ===== ④ 候选取件（夹具） =====");
            EffectExecutionContext ctx;
            string note;
            try { ctx = BuildFixture(out note); }
            catch (Exception ex)
            {
                Debug.Log($"[域契约] ===== FAIL：夹具构造失败：{ex.Message}\n{ex.StackTrace} =====");
                return;
            }
            Debug.Log($"[域契约] 夹具就绪：{note}");
            try { SectionCandidates(ctx); } catch (Exception ex) { Of("4").FailWith("段异常中断：" + ex + "\n" + ex.StackTrace); }
            Debug.Log("[域契约] ===== ⑤ 全表守卫 / ⑥ 生产链证据 =====");
            try { SectionTableGuard(); } catch (Exception ex) { Of("5").FailWith("段异常中断：" + ex); }
            try { SectionRuntime(ctx); } catch (Exception ex) { Of("6").FailWith("段异常中断：" + ex + "\n" + ex.StackTrace); }

            int pass = Items.Count(i => !i.Fail), fail = Items.Count(i => i.Fail);
            var sb = new System.Text.StringBuilder();
            foreach (var i in Items)
            {
                if (i.Fail) continue;
                sb.Append("\n  [PASS] ").Append(i.Id);
            }
            foreach (var i in Items.Where(i => i.Fail))
                foreach (var l in i.Lines)
                    sb.Append("\n  [FAIL] ").Append(i.Id).Append(' ').Append(l);
            Debug.Log($"[域契约] ===== 汇总：PASS {pass} / FAIL {fail} ====={sb}");
        }

        private static Item Of(string id)
        {
            var it = Items.FirstOrDefault(i => i.Id == id);
            if (it == null) { it = new Item(id); Items.Add(it); }
            return it;
        }

        private static void Check(string id, bool cond, string message)
        {
            if (cond) return;
            Of(id).FailWith(message);
        }

        // ======================================== ① 词表 ========================================

        private static void SectionWords()
        {
            // 22 区域 token = 自己 + 3侧角色 + 3侧×6区
            var regionTokens = new List<string> { "自己", "己方角色", "对方角色", "双方角色" };
            foreach (var side in new[] { "己方", "对方", "双方" })
                foreach (var zone in new[] { "战场", "手牌", "牌库", "墓地", "除外区", "元素池" })
                    regionTokens.Add(side + zone);
            Check("1", TargetRegionRules.Count == 22, $"区域词表行数 {TargetRegionRules.Count} ≠ 22");
            Check("1", regionTokens.Distinct().Count() == 22, "真相表存在重复 token");
            foreach (var token in regionTokens)
            {
                bool ok = TargetRegionRules.TryParse(token, out var region);
                Check("1", ok, $"TryParse('{token}') 失败（词表缺 token）");
                if (!ok) continue;
                Check("1", TargetRegionRules.ZhNameOf(region) == token, $"ZhNameOf({region}) 往返失败");
            }
            // 3侧×6区 覆盖恰 18；角色三 token Zone=None 且 IsCharacter；Self Zone=None 且非角色
            var regions = Enum.GetValues(typeof(TargetRegion)).Cast<TargetRegion>().ToList();
            Check("1", regions.Count == 22, $"枚举成员数 {regions.Count} ≠ 22");
            foreach (var side in new[] { RegionSide.Own, RegionSide.Opposing, RegionSide.Both })
                foreach (var zone in new[] { Zone.Battlefield, Zone.Hand, Zone.Deck, Zone.Graveyard, Zone.Exile, Zone.ElementPool })
                {
                    var hit = regions.Count(r => TargetRegionRules.ZoneOf(r) == zone && TargetRegionRules.SideOf(r) == side);
                    Check("1", hit == 1, $"侧别 {side} × 区域 {zone} 应恰 1 token（实 {hit}）");
                }
            foreach (var ch in new[] { TargetRegion.OwnCharacter, TargetRegion.EnemyCharacter, TargetRegion.BothCharacter })
                Check("1", TargetRegionRules.ZoneOf(ch) == Zone.None && TargetRegionRules.IsCharacter(ch),
                    $"角色区域 {ch} 应 Zone=None 且 IsCharacter");
            Check("1", TargetRegionRules.ZoneOf(TargetRegion.Self) == Zone.None && !TargetRegionRules.IsCharacter(TargetRegion.Self),
                "Self 应 Zone=None 且非角色");
            Check("1", regions.All(r => TargetRegionRules.ZoneOf(r) != Zone.Activation), "存在映射发动区的区域 token（应无）");
            foreach (var bad in new[] { "", null, "己方", "双方", "双方发动区", "战场", "角色", "己方角色:生物" })
                Check("1", !TargetRegionRules.TryParse(bad, out _), $"TryParse('{bad}') 应拒绝（裸X方/发动区/带类别形态）");
            Check("1", TargetRegionRules.TryParse(" 己方战场 ", out var trimmed) && trimmed == TargetRegion.OwnBattlefield,
                "TryParse 应容忍首尾空白");

            // 4 类别 token（卡/生物/结界/法术；无角色/地牌）
            foreach (var pair in new[] { ("卡", TargetCategory.Card), ("生物", TargetCategory.Creature),
                                         ("结界", TargetCategory.Enchantment), ("法术", TargetCategory.Spell) })
                Check("1", TargetCategoryRules.TryParse(pair.Item1, out var cat) && cat == pair.Item2,
                    $"类别 TryParse('{pair.Item1}') 失败或错位");
            foreach (var bad in new[] { "", null, "角色", "地牌", "单位", "卡们" })
                Check("1", !TargetCategoryRules.TryParse(bad, out _), $"类别 TryParse('{bad}') 应拒绝（无角色/地牌类别）");
            Check("1", TargetCategoryRules.ParseCsv("生物,结界") == (TargetCategory.Creature | TargetCategory.Enchantment),
                "类别 CSV 并集失败");
            var each = new[] { TargetCategory.Card, TargetCategory.Creature, TargetCategory.Enchantment, TargetCategory.Spell };
            for (int i = 0; i < each.Length; i++)
                for (int j = i + 1; j < each.Length; j++)
                    Check("1", (each[i] & each[j]) == 0, $"类别位重叠：{each[i]} & {each[j]}");
        }

        // ======================================== ② 契约解析 ========================================

        private static void SectionContract()
        {
            // 合法样例
            var legal = new (string domain, string expectZh)[]
            {
                ("自己", "自己"),
                ("双方角色", "双方角色"),
                ("双方战场:生物", "双方战场:生物"),
                ("双方战场:生物,结界", "双方战场:生物,结界"),
                ("双方战场:卡", "双方战场:卡"),
                ("双方战场:生物; 双方角色", "双方战场:生物；双方角色"),
                ("自己; 双方手牌:卡", "自己；双方手牌:卡"),
                (" 双方战场 : 生物 , 结界 ； 对方元素池 : 卡 ", "双方战场:生物,结界；对方元素池:卡"), // 空格容忍
                ("己方墓地:法术", "己方墓地:法术"),
            };
            foreach (var (domain, expectZh) in legal)
            {
                bool ok = TargetAxisRules.TryParseDomain(domain, out var axis);
                Check("2", ok, $"合法域 '{domain}' 被拒");
                if (ok) Check("2", axis.Zh == expectZh, $"域 '{domain}' 规范文本 '{axis.Zh}' ≠ '{expectZh}'");
            }

            // 非法全拒（契约规则 1-4 + 语法）
            foreach (var bad in new[]
                     {
                         "己方", "对方", "双方",                                   // 裸 X方
                         "双方战场", "己方手牌", "对方元素池",                       // 卡区域无类别
                         "双方角色:生物", "己方角色:卡", "自己:生物",                // 角色/自己带类别
                         "双方发动区:生物", "发动区",                                // 发动区
                         "双方战场:角色", "双方战场:地牌", "双方战场:单位",           // 坏类别（角色/地牌已删）
                         "双方战场:生物;;双方角色", "；双方战场:生物",                 // 空段
                         "双方战场:生物:", "双方战场::生物", "双方战场:生物:结界",      // 冒号形态
                         "双方战场:生物, ", "不存在的区域:生物",                       // 坏 token
                         "", "   ",
                     })
                Check("2", !TargetAxisRules.TryParseDomain(bad, out _), $"非法域 '{bad}' 应拒绝");

            // B2（2026-11-05 硬切）：旧 EffectDomain 词表已整文件删除——本段旧词表归宿断言随之退役。
            // 类别性 filter token（NoRole/NotRole/Player/Living/Field）解析已删：数据串残留静默忽略。

            // 行级 filter 冲突（契约规则 5）
            foreach (var bad in new[] { "NoRole", "NotRole", "Player", "Living", "Field", "NoRole,Damaged", "Damaged,Field" })
                Check("2", TargetAxisRules.FilterConflicts(bad), $"FilterConflicts('{bad}') 应=true（新 Domain 行禁类别性 token）");
            foreach (var ok in new[] { "", null, "Damaged", "Untapped,Tapped", "Power>2", "Stealth" })
                Check("2", !TargetAxisRules.FilterConflicts(ok), $"FilterConflicts('{ok}') 应=false（属性 token 合法）");
        }

        // ======================================== ③ 区域收窄 ========================================

        private static void SectionNarrow()
        {
            // 紊乱样例（契约 §4）：行 双方战场:生物 + 实例 kinds=[2]（对方战场单位）→ 对方战场:生物
            bool ok = TargetAxisRules.TryParseDomain("双方战场:生物", out var row);
            Check("3", ok, "紊乱行域解析失败");
            if (ok)
            {
                var narrowed = TargetAxisRules.NarrowByKinds(row, new List<int> { 2 });
                Check("3", narrowed.Selectors.Count == 1 && narrowed.Selectors[0].Region == TargetRegion.EnemyBattlefield
                           && narrowed.Selectors[0].Categories == TargetCategory.Creature,
                    $"紊乱收窄：双方战场:生物 + kind[2] 应 → 对方战场:生物（实 {narrowed.Zh}）");
            }

            // kinds 含双侧 → Both 保持
            ok = TargetAxisRules.TryParseDomain("双方战场:生物; 双方角色", out var both);
            if (ok)
            {
                var narrowed = TargetAxisRules.NarrowByKinds(both, new List<int> { 1, 2 });
                Check("3", narrowed.Selectors.Count == 2 && narrowed.Selectors.Any(s => s.Region == TargetRegion.BothBattlefield)
                           && narrowed.Selectors.Any(s => s.Region == TargetRegion.BothCharacter),
                    "双侧 kinds 收窄应保持 Both 区域/角色 selector");
            }

            // 角色侧别收窄（2026-11-05 收口·契约 §5）：kinds 收窄连角色 selector 一起按生命单位侧别收窄
            ok = TargetAxisRules.TryParseDomain("双方战场:生物; 双方角色", out var roleAxis);
            if (ok)
            {
                // kinds=[1]（己方生命单位）→ 双方战场→己方战场、双方角色→己方角色
                var ownOnly = TargetAxisRules.NarrowByKinds(roleAxis, new List<int> { 1 });
                Check("3", ownOnly.Selectors.Count == 2
                           && ownOnly.Selectors.Any(s => s.Region == TargetRegion.OwnBattlefield)
                           && ownOnly.Selectors.Any(s => s.Region == TargetRegion.OwnCharacter)
                           && ownOnly.Selectors.All(s => s.Region != TargetRegion.BothCharacter),
                    $"角色收窄：+ kind[1] 应 → 己方战场:生物; 己方角色（实 {ownOnly.Zh}）");
                // kinds=[3]（己方无生命单位）→ 3/4 不授予角色侧别 → 角色 selector 被删（战场仍收窄到己方）
                var noRole = TargetAxisRules.NarrowByKinds(roleAxis, new List<int> { 3 });
                Check("3", noRole.Selectors.Count == 1 && noRole.Selectors[0].Region == TargetRegion.OwnBattlefield
                           && !noRole.Selectors.Any(s => TargetRegionRules.IsCharacter(s.Region)),
                    $"角色收窄：+ kind[3] 应删角色 selector（无生命单位不授予侧别；实 {noRole.Zh}）");
            }

            // 无交 → 删 selector（行域手牌 + kinds 战场 → 空）
            ok = TargetAxisRules.TryParseDomain("双方手牌:卡; 自己", out var hand);
            if (ok)
            {
                var narrowed = TargetAxisRules.NarrowByKinds(hand, new List<int> { 1 }); // 己方战场
                Check("3", narrowed.Selectors.Count == 1 && narrowed.Selectors[0].Region == TargetRegion.Self,
                    "无交区域应删 selector（自己 selector 保留）");
            }

            // 空 kinds / 纯自己 kinds → 不收窄（原样返回）
            ok = TargetAxisRules.TryParseDomain("双方战场:生物", out var plain);
            if (ok)
            {
                Check("3", ReferenceEquals(TargetAxisRules.NarrowByKinds(plain, null), plain), "null kinds 应原样返回");
                Check("3", ReferenceEquals(TargetAxisRules.NarrowByKinds(plain, new List<int>()), plain), "空 kinds 应原样返回");
                Check("3", ReferenceEquals(TargetAxisRules.NarrowByKinds(plain, new List<int> { 0 }), plain),
                    "纯自己 kinds 应原样返回（Self selector 不分区）");
            }

            // 区域 kind 收窄（墓地）：行 双方墓地:法术 + kinds=[10]（TargetKind 10=EnemyGraveyard）→ 对方墓地:法术
            ok = TargetAxisRules.TryParseDomain("双方墓地:法术", out var grave);
            if (ok)
            {
                var narrowed = TargetAxisRules.NarrowByKinds(grave, new List<int> { 10 });
                Check("3", narrowed.Selectors.Count == 1 && narrowed.Selectors[0].Region == TargetRegion.EnemyGraveyard,
                    $"墓地收窄应 → 对方墓地:法术（kinds[10]=EnemyGraveyard；实 {narrowed.Zh}）");
            }
        }

        // ======================================== ④ 候选取件（夹具） ========================================

        private static void SectionCandidates(EffectExecutionContext ctx)
        {
            var core = GameCore.Instance;
            var p1 = core.Player1;
            var p2 = core.Player2;
            var zm = core.ZoneManager;
            int p1Creatures = zm.GetCards(p1, Zone.Battlefield).Count(c => c.IsAlive && IsType(c, Cardtype.Creature));
            int p2Creatures = zm.GetCards(p2, Zone.Battlefield).Count(c => c.IsAlive && IsType(c, Cardtype.Creature));
            int p1Enchants = zm.GetCards(p1, Zone.Battlefield).Count(c => c.IsAlive && IsType(c, Cardtype.Enchantment) && !c.IsFieldCard());
            int p1Hand = zm.GetCards(p1, Zone.Hand).Count;

            // 双方战场:生物 = 双方场上存活生物
            AssertCount(ctx, "双方战场:生物", p1Creatures + p2Creatures, "双方战场:生物");
            // 己方战场:结界 = 己方场上结界（场地=结界子类同命中）
            AssertCount(ctx, "己方战场:结界", p1Enchants + zm.GetCards(p1, Zone.Battlefield).Count(c => c.IsAlive && c.IsFieldCard()),
                "己方战场:结界（含场地子类）");
            // 双方角色 = 2 个 Player
            var chars = Resolve(ctx, "双方角色");
            Check("4", chars.Count == 2 && chars.All(e => e is Player), $"双方角色应恰 2 Player（实 {chars.Count}）");
            // 双方手牌:卡 = 双方手牌全卡
            AssertCount(ctx, "双方手牌:卡", p1Hand + zm.GetCards(p2, Zone.Hand).Count, "双方手牌:卡");
            // 双方元素池:卡
            AssertCount(ctx, "双方元素池:卡",
                zm.GetCards(p1, Zone.ElementPool).Count + zm.GetCards(p2, Zone.ElementPool).Count, "双方元素池:卡");
            // 自己 = 源卡（夹具 Source=己方受伤在场生物）
            var self = Resolve(ctx, "自己");
            Check("4", self.Count == 1 && ReferenceEquals(self[0], (Entity)(ctx.Source as Card)), "自己应恰源卡");
            // 属性 Filter：Damaged 只留受伤
            var axis = Parse("双方战场:生物");
            axis.Filter = "Damaged";
            var damaged = TargetAxisResolver.GetCandidates(axis, ctx);
            Check("4", damaged.All(e => !(e is Card c) || c.GetLife() < c.GetMaxLife()) && damaged.Count > 0,
                "属性 Filter=Damaged 应只留受伤实体（且非空）");
            // 类别滤：法术类别只选手牌法术探针
            var spellAxis = Parse("对方手牌:法术");
            var spells = TargetAxisResolver.GetCandidates(spellAxis, ctx);
            Check("4", spells.All(e => e is Card c && IsType(c, Cardtype.Spell)) && spells.Count >= 1,
                "对方手牌:法术 应只命法术（夹具探针在 p2 手牌）");
        }

        private static List<Entity> Resolve(EffectExecutionContext ctx, string domain)
        {
            var axis = Parse(domain);
            return axis != null ? TargetAxisResolver.GetCandidates(axis, ctx) : new List<Entity>();
        }

        private static TargetAxis Parse(string domain)
            => TargetAxisRules.TryParseDomain(domain, out var axis) ? axis : null;

        private static void AssertCount(EffectExecutionContext ctx, string domain, int expect, string label)
        {
            var got = Resolve(ctx, domain);
            Check("4", got.Count == expect, $"{label} 应 {expect} 实 {got.Count}");
        }

        // ======================================== ⑤ 全表守卫 ========================================

        private static void SectionTableGuard()
        {
            // 硬切 A（2026-11-05）：新语法是唯一合法形态——仍在用旧 Domain token / TargetKinds
            // 的行 = **迁移未完成（红）**，无试点前例外、无白名单（上次的后门已堵死）。
            // 豁免仅两类：①行 Domain 为合法新语法且 TargetFilter 无类别 token（绿=已迁）；
            // ②无目标行（kinds 空且无 Domain——反制/被动，无需迁移）。
            int rows = 0, migrated = 0, noTarget = 0, pendingLegacy = 0, pendingContract = 0;
            foreach (var row in AtomicEffectTable.GetAll())
            {
                if (row == null || string.IsNullOrEmpty(row.HashId)) continue;
                rows++;
                if (!string.IsNullOrEmpty(row.Domain) && TargetAxisRules.TryParseDomain(row.Domain, out _))
                {
                    if (TargetAxisRules.FilterConflicts(row.TargetFilter))
                    {
                        pendingContract++;
                        Of("5").FailWith($"行 {row.EnumName}（{row.HashId}）新语法 Domain='{row.Domain}' 但 TargetFilter='"
                                         + row.TargetFilter + "' 含类别性 token（契约规则 5：类别由 Domain 承载）");
                    }
                    else migrated++;
                    continue;
                }
                if (!string.IsNullOrEmpty(row.Domain))
                {
                    pendingLegacy++;
                    Of("5").FailWith($"行 {row.EnumName}（{row.HashId}）Domain='{row.Domain}' 为旧词表 token（迁移未完成——新语法唯一合法形态）");
                    continue;
                }
                if (row.GetTargetKindList().Count > 0)
                {
                    pendingLegacy++;
                    Of("5").FailWith($"行 {row.EnumName}（{row.HashId}）Domain 空但派生 kinds 非空（数据不一致——B2 后 Domain 为唯一域源）");
                    continue;
                }
                noTarget++;
            }
            Debug.Log($"[域契约][5] 全表硬切口径：{rows} 行 = 已迁（新语法）{migrated} + 无目标 {noTarget} + "
                      + $"迁移未完成 {pendingLegacy + pendingContract}（旧 Domain {pendingLegacy} / 新行契约违规 {pendingContract}）"
                      + "——大面积红为预期（映射提案见 docs/目标域迁移提案.md，不因红恢复 legacy）");
            Check("5", rows == 153, $"全表 {rows} 行（应 153——全体两行已删）");
        }

        // ======================================== ⑥ 生产链证据 ========================================

        private static void SectionRuntime(EffectExecutionContext ctx)
        {
            var core = GameCore.Instance;

            // B1 卡级样本（2026-11-05 硬切验收）：表行 Domain + entry.kinds 收窄 → converter 产轴落点
            var samples = new (string refId, List<int> kinds, string expectZh, string expectFilter)[]
            {
                ("d3ed120d", new List<int> { 2 }, "对方战场:生物", ""),                       // 紊乱卡：kinds[2]→对方战场，无角色
                ("d3ed120d", new List<int> { 1 }, "己方战场:生物", ""),                       // 紊乱可指己方生物（代价用法）
                ("a4b823fc", null, "双方战场:生物；双方角色", ""),                            // 伤害卡
                ("af7bd26e", null, "双方战场:生物；双方角色", ""),                            // 吸取卡
                ("a50b8c19", null, "双方战场:生物", ""),                                      // 消灭卡
                ("599fcc9a", null, "双方战场:生物；双方角色", "Damaged"),                     // 回复卡（+Damaged）
                ("973884b1", null, "双方战场:生物", ""),                                      // 冻结（无元素池）
            };
            foreach (var (refId, kinds, expectZh, expectFilter) in samples)
            {
                var def = CardEffectConverter.ConvertOne(new CardEffectData
                {
                    Id = "VERIFY_B1_" + refId,
                    DisplayName = "B1 样本",
                    AtomicEffects = new List<AtomicEffectEntry>
                        { new AtomicEffectEntry { refId = refId, value = 1, kinds = kinds } },
                }, "VERIFY_B1");
                var atom = def?.Effects?.FirstOrDefault();
                var gotZh = atom?.Axis?.Zh ?? "(无轴)";
                var gotFilter = atom?.Axis?.Filter ?? "";
                Check("6", gotZh == expectZh && gotFilter == expectFilter,
                    $"行 {refId} + kinds[{(kinds == null ? "" : string.Join(",", kinds))}] → '{gotZh}'+filter'{gotFilter}'"
                    + $" ≠ 期望 '{expectZh}'+'{expectFilter}'");
            }
            Debug.Log("[域契约][6] B1 卡级样本七项全对（紊乱×2/伤害/吸取/消灭/回复/冻结）");

            // executor 结算 → TargetAxisResolver 真实消费
            TargetAxisResolver.LiveResolutions = 0;
            var defRun = CardEffectConverter.ConvertOne(new CardEffectData
            {
                Id = "VERIFY_B1_RUN",
                DisplayName = "B1 消费探针",
                AtomicEffects = new List<AtomicEffectEntry>
                    { new AtomicEffectEntry { refId = "a4b823fc", value = 1 } },
            }, "VERIFY_B1");
            core.StackEngine.GetExecutor().ExecuteAsync(new EffectInstance
            {
                Definition = defRun,
                Source = ctx.Source,
                Controller = core.Player1,
                Targets = new List<Entity> { core.Player2 },
            }, skipElementCost: true).Forget();
            Check("6", TargetAxisResolver.LiveResolutions > 0,
                $"生产管线零消费（converter→executor→TargetAxisResolver，LiveResolutions={TargetAxisResolver.LiveResolutions}）");
            Debug.Log($"[域契约][6] 运行时消费证据：行 Domain 产轴 → executor 结算 → "
                      + "TargetAxisResolver.LiveResolutions=" + TargetAxisResolver.LiveResolutions);
        }

        // ======================================== 夹具（域迁移校验同款 + 法术探针） ========================================

        private static Card _spellCard; // 法术类别探针（克隆+Supertype=Spell，挂 p2 手牌）

        private static EffectExecutionContext BuildFixture(out string note)
        {
            var catalog = SynergyUI.CardCatalog.LoadAll().ToList();
            var creatureTpl = catalog.FirstOrDefault(c => c.Supertype == Cardtype.Creature && (c.Life ?? 0) >= 3)
                              ?? catalog.First(c => c.Supertype == Cardtype.Creature);
            var enchantTpl = catalog.FirstOrDefault(c => c.Supertype == Cardtype.Enchantment);
            var fillerTpl = catalog.FirstOrDefault(c => c != creatureTpl && c != enchantTpl)
                            ?? catalog.First(c => c != creatureTpl);

            var core = GameCore.Instance;
            // 场地卡模板（结界克隆+IsField=true——与 catalog 模板隔离零污染）
            CardData fieldTpl = null;
            if (enchantTpl != null)
            {
                fieldTpl = TideJson.FromJson<CardData>(TideJson.ToJson(enchantTpl));
                if (fieldTpl != null) fieldTpl.IsField = true;
            }
            var deck1 = new List<CardData>();
            var deck2 = new List<CardData>();
            for (int i = 0; i < 8; i++) { deck1.Add(creatureTpl); deck2.Add(creatureTpl); }
            for (int i = 0; i < 2 && enchantTpl != null; i++) { deck1.Add(enchantTpl); deck2.Add(enchantTpl); }
            if (fieldTpl != null) deck1.Add(fieldTpl);
            while (deck1.Count < 17) deck1.Add(fillerTpl);
            while (deck2.Count < 16) deck2.Add(fillerTpl);
            core.InitGame(CardLoader.BuildDeck(deck1, 1), CardLoader.BuildDeck(deck2, 1));
            var p1 = core.Player1;
            var p2 = core.Player2;
            p1.IsAI = false; p2.IsAI = false;

            var zm = core.ZoneManager;
            var selfCard = default(Card);
            var fieldCard = zm.GetCards(p1, Zone.Deck).FirstOrDefault(c => c.IsFieldCard())
                            ?? zm.GetCards(p1, Zone.Hand).FirstOrDefault(c => c.IsFieldCard());
            if (fieldCard != null)
                zm.MoveCard(fieldCard, p1,
                    zm.IsCardInZone(fieldCard, p1, Zone.Hand) ? Zone.Hand : Zone.Deck, Zone.Battlefield);
            foreach (var p in new[] { p1, p2 })
            {
                var deck = zm.GetCards(p, Zone.Deck).ToList();
                var creatures = deck.Where(c => c.IsAlive && IsType(c, Cardtype.Creature)).Take(2).ToList();
                foreach (var c in creatures) zm.MoveCard(c, p, Zone.Deck, Zone.Battlefield);
                var enchant = deck.FirstOrDefault(c => IsType(c, Cardtype.Enchantment))
                              ?? zm.GetCards(p, Zone.Hand).FirstOrDefault(c => IsType(c, Cardtype.Enchantment)); // 起手随机抽走兜底
                if (enchant != null)
                    zm.MoveCard(enchant, p, zm.IsCardInZone(enchant, p, Zone.Hand) ? Zone.Hand : Zone.Deck, Zone.Battlefield);
                var rest = zm.GetCards(p, Zone.Deck).ToList();
                int n = 0;
                foreach (var c in rest)
                {
                    if (n < 2) zm.MoveCard(c, p, Zone.Deck, Zone.Graveyard);
                    else if (n < 4) zm.MoveCard(c, p, Zone.Deck, Zone.Exile);
                    else if (n == 5 && zm.GetCards(p, Zone.Hand).Count < 2) zm.MoveCard(c, p, Zone.Deck, Zone.Hand);
                    else break;
                    n++;
                }
                var poolSrc = zm.GetCards(p, Zone.Deck).FirstOrDefault();
                if (poolSrc != null)
                {
                    core.ElementPool.GetPool(p).PooledCards.Add(new PooledCard(poolSrc, ManaType.Red));
                    zm.MoveCard(poolSrc, p, Zone.Deck, Zone.ElementPool);
                }
                if (creatures.Count > 0)
                {
                    // 受伤目标=生命最高那只（夹具随机序可能先抽到 1/1 填充生物——Max(1,x−2) 钳底
                    // 会造不出受伤实体，Damaged 断言随机假红；高血恒可 -2 落出差值）
                    var hurtSrc = creatures.OrderByDescending(c => ((Entity)c).GetMaxLife()).First();
                    var hurt = (Entity)hurtSrc;
                    hurt.SetLife(Math.Max(1, hurt.GetMaxLife() - 2));
                    if (p == p1) selfCard = hurtSrc;
                    if (creatures.Count > 1)
                        ((Entity)creatures.First(c => c != hurtSrc)).Tap();
                }
            }

            // 法术探针卡（对方手牌:法术 的类别证据）：克隆改 Supertype=Spell 挂 p2 手牌
            var spellData = TideJson.FromJson<CardData>(TideJson.ToJson(fillerTpl));
            if (spellData != null)
            {
                spellData.ID = "PROBE_AXIS_SPELL";
                spellData.Supertype = Cardtype.Spell;
                _spellCard = new CardWrapper(spellData);
                _spellCard.SetController(p2);
                zm.GetZoneContainer(p2)?.Add(_spellCard, Zone.Hand);
            }

            var selfEnt = (Entity)selfCard;
            note = $"卡名快照：生物={creatureTpl.CardName} 结界={(enchantTpl != null ? enchantTpl.CardName : "无")} 填充={fillerTpl.CardName}"
                 + $"｜场地={(fieldCard != null ? "已上p1战场" : "缺失!")}｜法术探针={(_spellCard != null ? "已挂p2手牌" : "缺失!")}"
                 + $"｜Self={(selfEnt != null ? $"己方受伤生物 {selfEnt.GetLife()}/{selfEnt.GetMaxLife()}" : "缺失!")}";

            return new EffectExecutionContext
            {
                Source = selfCard,
                Controller = p1,
                ZoneManager = core.ZoneManager,
                ElementPool = core.ElementPool,
            };
        }

        private static bool IsType(Card c, Cardtype t) => c is IHasSupertype st && st.Supertype == t;
    }
}
