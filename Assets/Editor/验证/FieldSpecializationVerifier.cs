using System;
using System.Collections.Generic;
using System.Linq;
using CardCore.Attribute.Handlers;
using UnityEditor;
using UnityEngine;

namespace CardCore.Editor.Tests
{
    /// <summary>
    /// 场地特殊化校验（2026-11-18·docs/场地特殊化_修改计划.md P8）：
    /// ■ ① 入场耐久初始化不受指示物闸影响（CounterGrantKind.Init 档）；
    /// ■ ② 固定 1 耐久：效果/战斗伤害逐次 −1（防护层照常算、落耐久口钳 1）；归零直毁入墓（Smashed）；
    /// ■ ③ 普通结界对照：同场景仍按（穿甲后）全伤扣耐久；
    /// ■ ④ 指示物效果对场地无效（护甲/圣盾/耐久增加 0 落账；普通生物不受影响）；
    /// ■ ⑤ 关键词效果对场地无效（AddKeyword 咽喉 + GrantKeywordHandler 无事件；箭头光环投递读取为假）；
    /// ■ ⑥ 类别域：:结界/:卡 不命中场地、场地区域词元可达、攻击场地合法（CanAttackTarget）；
    /// ■ ⑦ 摧毁原子对场地走 Smash 入墓（handler 无需改——行为锚）。
    /// </summary>
    public static class FieldSpecializationVerifier
    {
        private static readonly List<string> Fails = new List<string>();
        private static int _pass;

        private static void Check(bool cond, string msg)
        {
            if (cond) { _pass++; return; }
            Fails.Add(msg);
        }

        [MenuItem("Tools/场地特殊化校验")]
        public static void Run()
        {
            Fails.Clear();
            _pass = 0;
            Debug.Log("[场地特殊化] ===== 开始 =====");

            GameCore core;
            Player p1, p2;
            try { BuildFixture(out core, out p1, out p2); }
            catch (Exception ex)
            {
                Debug.Log($"[场地特殊化] ===== FAIL：夹具构造失败：{ex.Message}\n{ex.StackTrace} =====");
                return;
            }
            Debug.Log($"[场地特殊化] 夹具就绪：p1={p1.Name} p2={p2.Name}");

            try { SectionDurability(core, p1, p2); } catch (Exception ex) { Fails.Add("①②③段异常中断：" + ex + "\n" + ex.StackTrace); }
            try { SectionCounterGate(core, p1, p2); } catch (Exception ex) { Fails.Add("④段异常中断：" + ex + "\n" + ex.StackTrace); }
            try { SectionKeywordGate(core, p1, p2); } catch (Exception ex) { Fails.Add("⑤段异常中断：" + ex + "\n" + ex.StackTrace); }
            try { SectionCategory(core, p1, p2); } catch (Exception ex) { Fails.Add("⑥段异常中断：" + ex + "\n" + ex.StackTrace); }
            try { SectionSmash(core, p1, p2); } catch (Exception ex) { Fails.Add("⑦段异常中断：" + ex + "\n" + ex.StackTrace); }
            try { SectionOffBattlefield(core, p1, p2); } catch (Exception ex) { Fails.Add("⑧段异常中断：" + ex + "\n" + ex.StackTrace); }

            var sb = new System.Text.StringBuilder($"\n  [PASS] 断言 {_pass} 条");
            foreach (var f in Fails) sb.Append("\n  [FAIL] ").Append(f);
            Debug.Log($"[场地特殊化] ===== 汇总：PASS {_pass} / FAIL {Fails.Count} ====={sb}");
        }

        // ======================================== ⑧ 区外场地卡（手/墓） ========================================

        /// <summary>场地特殊化只作用于**战场上的场地**：手牌/墓地里的场地卡仍是普通结界卡——
        /// 可被 :卡/:结界 取到、可接费用指示物（2026-11-18 验收修复回归锚）。</summary>
        private static void SectionOffBattlefield(GameCore core, Player p1, Player p2)
        {
            var zm = core.ZoneManager;
            var data = NewData("验证手牌场地", Cardtype.Enchantment);
            data.IsField = true;
            data.Durability = 2;
            var card = new CardWrapper(data);
            card.SetController(p1);
            // 手牌上限 7（2026-11-18）：先腾一格，确保 Add 不会把手牌溢出入除外区
            var handNow = zm.GetCards(p1, Zone.Hand);
            if (handNow.Count >= 7 && handNow.Count > 0)
                zm.MoveCard(handNow[0], p1, Zone.Hand, Zone.Graveyard);
            zm.GetZoneContainer(p1).Add(card, Zone.Hand);
            Check(card.GetZone() == Zone.Hand, $"区外夹具：场地卡应在手牌（实 {card.GetZone()}）");

            List<Entity> Resolve(string domain)
                => CardCore.TargetAxisRules.TryParseDomain(domain, out var ax)
                    ? CardCore.TargetAxisResolver.GetCandidates(ax, new EffectExecutionContext
                    {
                        Source = p1, Controller = p1,
                        ZoneManager = zm, ElementPool = core.ElementPool,
                    })
                    : new List<Entity>();

            Check(Resolve("己方手牌:卡").Contains(card), "⑧ 手牌里的场地卡应被 :卡 取到");
            Check(Resolve("己方手牌:结界").Contains(card), "⑧ 手牌里的场地卡应被 :结界 取到（区外仍是结界卡）");
            Check(card.AddCounters(CardCore.Attribute.CounterRules.CostDownGrayCounter, 1, p1) == 1,
                "⑧ 手牌场地卡的费用指示物应照常落账（场地指示物闸只作用于战场）");

            zm.MoveCard(card, p1, Zone.Hand, Zone.Graveyard);
            Check(Resolve("己方墓地:卡").Contains(card), "⑧ 墓地里的场地卡应被 :卡 取到");
            Check(Resolve("己方墓地:结界").Contains(card), "⑧ 墓地里的场地卡应被 :结界 取到");
        }

        // ======================================== 夹具 ========================================

        private static int _seq;

        private static CardData NewData(string name, Cardtype type)
            => new CardData { ID = "VERIFY_FIELD_" + (_seq++), CardName = name, Supertype = type };

        private static Card MakeCreature(GameCore core, Player owner, int power, int life)
        {
            var data = NewData("验证生物" + _seq, Cardtype.Creature);
            data.Power = power;
            data.Life = life;
            var card = new CardWrapper(data);
            card.SetController(owner);
            if (!core.ZoneManager.TryAddToBattlefield(card, owner))
                throw new Exception("生物夹具入场失败（战场容量）");
            return card;
        }

        /// <summary>场地夹具卡：结界 + IsField + 印刷耐久（入场经 CardPutToBattlefieldEvent →
        /// EquipRules.OnEnterBattlefield（Init 档）初始化耐久）。</summary>
        private static Card MakeField(GameCore core, Player owner, int durability)
        {
            var data = NewData("验证场地" + _seq, Cardtype.Enchantment);
            data.IsField = true;
            data.Durability = durability;
            var card = new CardWrapper(data);
            card.SetController(owner);
            if (!core.ZoneManager.TryAddToBattlefield(card, owner))
                throw new Exception("场地夹具入场失败");
            return card;
        }

        private static void BuildFixture(out GameCore core, out Player p1, out Player p2)
        {
            var catalog = SynergyUI.CardCatalog.LoadAll().ToList();
            var filler = catalog.First(c => c.Supertype == Cardtype.Creature);
            var deck1 = new List<CardData>();
            var deck2 = new List<CardData>();
            for (int i = 0; i < 8; i++) { deck1.Add(filler); deck2.Add(filler); }
            core = GameCore.Instance;
            core.InitGame(CardLoader.BuildDeck(deck1, 1), CardLoader.BuildDeck(deck2, 1));
            p1 = core.Player1;
            p2 = core.Player2;
            p1.IsAI = false;
            p2.IsAI = false;
            // 清空起手随机抽到的战场残留（InitGame 只发牌不起场，防御性清手牌外区域）
        }

        // ======================================== ①②③ 固定 1 耐久 ========================================

        private static void SectionDurability(GameCore core, Player p1, Player p2)
        {
            var dur = CardCore.Attribute.CounterRules.DurabilityCounter;

            // ① 入场耐久初始化（Init 档不受场地指示物闸）：印刷 3 → 3 层
            var fieldA = MakeField(core, p2, 3);
            Check(fieldA.GetCounterCount(dur) == 3,
                $"① 入场耐久应为印刷值 3（实 {fieldA.GetCounterCount(dur)}——Init 档被闸误伤？）");

            // ② 效果伤害逐次 −1（与伤害量无关）：5 伤 → 3→2 → 2→1
            int dealt1 = CardCore.Attribute.KeywordRules.ApplyDamage(p1, fieldA, 5, false);
            Check(dealt1 == 1 && fieldA.GetCounterCount(dur) == 2,
                $"② 场地受伤应固定 −1（dealt={dealt1}，耐久 3→实 {fieldA.GetCounterCount(dur)}）");
            int dealt2 = CardCore.Attribute.KeywordRules.ApplyDamage(p1, fieldA, 5, false);
            Check(dealt2 == 1 && fieldA.GetCounterCount(dur) == 1,
                $"② 场地第二次受伤仍 −1（dealt={dealt2}，耐久→实 {fieldA.GetCounterCount(dur)}）");

            // ②b 战斗伤害同口径 + 耐久 1 归零直毁入墓（Smashed 直毁路径）+ 攻击场地合法
            var attacker = MakeCreature(core, p1, 4, 4);
            var fieldB = MakeField(core, p2, 1);
            Check(core.CombatSystem.CanAttackTarget(attacker, fieldB, p1),
                "⑥ CanAttackTarget：场地可被攻击（拍板保留战斗入口）");
            int dealtC = CardCore.Attribute.KeywordRules.ApplyDamage(attacker, fieldB, 4, true);
            Check(dealtC == 1 && fieldB.GetCounterCount(dur) == 0
                       && core.ZoneManager.GetCards(p2, Zone.Graveyard).Contains(fieldB),
                $"②b 战斗伤害 −1 且耐久 1 归零直毁入墓（dealt={dealtC}，区={fieldB.GetZone()}；直毁口径不设 IsAlive——耐久归零即送墓）");

            // ③ 普通结界对照：护甲 3 挡 5 伤 → 余 2 全额扣耐久（3→1）；伤害口径不受场地钳 1 影响
            var ench = NewData("验证结界", Cardtype.Enchantment);
            ench.Durability = 3;
            var enchCard = new CardWrapper(ench);
            enchCard.SetController(p2);
            core.ZoneManager.TryAddToBattlefield(enchCard, p2);
            enchCard.AddCounters(CardCore.Attribute.KeywordRules.ArmorCounter, 3, p2);
            int dealtE = CardCore.Attribute.KeywordRules.ApplyDamage(p1, enchCard, 5, false);
            Check(dealtE == 2 && enchCard.GetCounterCount(dur) == 1,
                $"③ 普通结界穿甲后按实际伤害扣耐久（5−甲3=2 应扣 2；dealt={dealtE}，耐久 3→实 {enchCard.GetCounterCount(dur)}）");
        }

        // ======================================== ④ 指示物闸 ========================================

        private static void SectionCounterGate(GameCore core, Player p1, Player p2)
        {
            var field = MakeField(core, p1, 2);

            // 效果类正量一律 0 落账：护甲/圣盾/耐久增加（IndicatorHandler 同走 AddCounters 咽喉）
            Check(field.AddCounters(CardCore.Attribute.KeywordRules.ArmorCounter, 2, p1) == 0
                       && field.GetCounterCount(CardCore.Attribute.KeywordRules.ArmorCounter) == 0,
                "④ 场地+护甲 2 层应 0 落账（返回 0 且层数 0）");
            Check(field.AddCounters(CardCore.Attribute.CounterRules.DivineShieldCounter, 1, p1) == 0
                       && field.GetCounterCount(CardCore.Attribute.CounterRules.DivineShieldCounter) == 0,
                "④ 场地+圣盾 1 层应 0 落账");
            Check(field.AddCounters(CardCore.Attribute.CounterRules.DurabilityCounter, 2, p1) == 0
                       && field.GetCounterCount(CardCore.Attribute.CounterRules.DurabilityCounter) == 2,
                "④ 场地+耐久增加 2 层应 0 落账（印刷 2 不变——耐久增加原子对场地空转）");

            // 负量（伤害扣耐久/移除）不拦：LoseDurability 直扣照常
            CardCore.Attribute.CounterRules.LoseDurability(core, field, 1, "验证移除");
            Check(field.GetCounterCount(CardCore.Attribute.CounterRules.DurabilityCounter) == 1,
                "④ 负量移除不拦（LoseDurability 2→1）");

            // 对照：普通生物不受场地闸影响
            var unit = MakeCreature(core, p1, 2, 3);
            Check(unit.AddCounters(CardCore.Attribute.KeywordRules.ArmorCounter, 2, p1) == 2
                       && unit.GetCounterCount(CardCore.Attribute.KeywordRules.ArmorCounter) == 2,
                "④ 对照：普通生物护甲 2 层照常落账");
        }

        // ======================================== ⑤ 关键词闸 ========================================

        private static void SectionKeywordGate(GameCore core, Player p1, Player p2)
        {
            var field = MakeField(core, p1, 2);

            // 咽喉：Temp 轨授予空转（不写 _keywords）
            field.AddKeyword("FirstStrike", CardCore.KeywordLane.Temp, null, 1, 1);
            Check(!field.HasKeyword("FirstStrike"),
                "⑤ AddKeyword(Temp) 对场地应空转（HasKeyword=false）");

            // 印刷轨不拦（卡面本体不是「效果」——MorphSystem 印刷复制路径不受闸）
            field.AddKeyword("Vigilance", CardCore.KeywordLane.Printed, null, 1, 1);
            Check(field.HasKeyword("Vigilance"),
                "⑤ AddKeyword(Printed) 印刷轨不拦（卡面本体非效果）");
            field.RemoveKeyword("Vigilance"); // 还原夹具（公开口——_keywords 为 internal）

            // handler 路径：GrantKeywordHandler 不写层、不发 KeywordEvent
            int keywordEvents = 0;
            void OnKeyword(Attribute.KeywordEvent e) { keywordEvents++; }
            EventManager.Instance.Subscribe<Attribute.KeywordEvent>(OnKeyword);
            try
            {
                var handler = new GrantKeywordHandler(CardCore.AtomicEffectType.GrantFirstStrike, "FirstStrike", "赋予先攻");
                var ctx = new EffectExecutionContext
                {
                    Source = p1,
                    Controller = p1,
                    ZoneManager = core.ZoneManager,
                    ElementPool = core.ElementPool,
                    Targets = new List<Entity> { field },
                };
                var inst = new AtomicEffectInstance { Type = CardCore.AtomicEffectType.GrantFirstStrike, Value = 1 };
                handler.Execute(inst, ctx);
                Check(keywordEvents == 0 && !field.HasKeyword("FirstStrike"),
                    $"⑤ GrantKeywordHandler 对场地应空转（KeywordEvent={keywordEvents}，HasKeyword={field.HasKeyword("FirstStrike")}）");

                // 对照：普通生物授予照常（事件 1 + 持有）
                var unit = MakeCreature(core, p1, 2, 2);
                ctx.Targets = new List<Entity> { unit };
                handler.Execute(inst, ctx);
                Check(keywordEvents == 1 && unit.HasKeyword("FirstStrike"),
                    "⑤ 对照：普通生物授予照常（KeywordEvent=1 且持有）");
            }
            finally
            {
                EventManager.Instance.Unsubscribe<Attribute.KeywordEvent>(OnKeyword);
            }

            // 箭头光环投递：箭头指向场地格 → 关键词读取为假（live-query 侧闸）
            SectionAuraKeyword(core, p1, field);
        }

        /// <summary>箭头光环投递关键词对场地读取为假（对照组：普通生物格同光环命中）。</summary>
        private static void SectionAuraKeyword(GameCore core, Player p1, Card fieldCard)
        {
            GameBoard.LinkAuraSystem.Detach();
            var board = new GameBoard.BoardState(core, core.Player1, core.Player2,
                GameBoard.HalfFieldData.Flat(), GameBoard.HalfFieldData.Flat());
            board.EnableAutoResync();
            GameBoard.LinkAuraSystem.Attach(board);
            board.Resync(); // 场地卡落 FieldCell（AssignCells 的 FieldCell 分支）

            var (fx, fz) = GameBoard.BoardLayout.FieldCell(0);
            Check(board.TryGetCell(fieldCard, out int fcx, out int fcz) && fcx == fx && fcz == fz,
                $"⑤ 场地卡应落场地格 ({fx},{fz})（实 ({fcx},{fcz})——光环用例前提）");

            // 光环源：贴场地格的单位格 (2,2)（z=2 行 x2..x5 单位区，x1=场地格右邻）
            var auraSrc = MakeCreature(core, p1, 1, 5);
            int sx = 2, sz = 2;
            GameBoard.BoardDirection dir = default;
            bool found = false;
            foreach (GameBoard.BoardDirection d in Enum.GetValues(typeof(GameBoard.BoardDirection)))
            {
                var (nx, nz) = GameBoard.BoardMath.Neighbor(sx, sz, d);
                if (nx == fx && nz == fz) { dir = d; found = true; break; }
            }
            Check(found, "⑤ 光环源相邻场地格前提（(2,2)→(1,2) 六邻接之一）");
            if (!found) return;
            board.PinCard(auraSrc, sx, sz);
            board.Resync();

            var srcData = (auraSrc as CardWrapper).GetData();
            srcData.LinkAuras.Add(new LinkAuraData { keyword = "Taunt" });
            srcData.ArrowDirections = GameBoard.BoardMath.ArrowOf(dir);
            GameBoard.LinkAuraSystem.InvalidateCache();

            Check(!GameBoard.LinkAuraSystem.HasAuraKeyword(fieldCard, "Taunt") && !fieldCard.HasKeyword("Taunt"),
                "⑤ 箭头光环指向场地格：关键词对场地读取为假（场地特殊化闸）");

            // 对照：同一光环源再指向一个普通生物格 → 命中（证明箭头/光环本身有效，只有场地被拦）
            var ben = MakeCreature(core, p1, 1, 5);
            // 找 auraSrc 的另一相邻空单位格
            int bx = -1, bz = -1;
            GameBoard.BoardDirection bDir = default;
            foreach (GameBoard.BoardDirection d in Enum.GetValues(typeof(GameBoard.BoardDirection)))
            {
                var (nx, nz) = GameBoard.BoardMath.Neighbor(sx, sz, d);
                if (nx == fx && nz == fz) continue;
                if (GameBoard.BoardMath.InBounds(nx, nz) && GameBoard.BoardLayout.NoTileAt(nx, nz)) continue;
                // 单位区格（z=1/2 行、非特殊位）
                if ((nz == 2 && nx >= 2 && nx <= 5) || (nz == 1 && nx >= 1 && nx <= 5))
                { bx = nx; bz = nz; bDir = d; break; }
            }
            Check(bx >= 0, "⑤ 对照生物落格前提");
            if (bx >= 0)
            {
                board.PinCard(ben, bx, bz);
                board.Resync();
                srcData.ArrowDirections = GameBoard.BoardMath.ArrowOf(dir) | GameBoard.BoardMath.ArrowOf(bDir);
                GameBoard.LinkAuraSystem.InvalidateCache();
                Check(GameBoard.LinkAuraSystem.HasAuraKeyword(ben, "Taunt") && ben.HasKeyword("Taunt"),
                    "⑤ 对照：箭头指向普通生物格同光环命中（闸只拦场地）");
            }
        }

        // ======================================== ⑥ 类别域 ========================================

        private static void SectionCategory(GameCore core, Player p1, Player p2)
        {
            var field = MakeField(core, p2, 2);

            // 类别判定单源：场地在任何类别集（含 Card 全选位）下恒 false
            Check(!CardCore.EntityCategoryRules.Matches(field, CardCore.TargetCategory.Card)
                       && !CardCore.EntityCategoryRules.Matches(field, CardCore.TargetCategory.Enchantment)
                       && !CardCore.EntityCategoryRules.Matches(field, CardCore.TargetCategory.Creature)
                       && !CardCore.EntityCategoryRules.Matches(field, CardCore.TargetCategory.All),
                "⑥ 场地不命中任何类别位（含 :卡 全选位）");

            // 候选口径：场地词元可达 / :结界/:卡 不可达
            var ctx = new EffectExecutionContext
            {
                Source = p1,
                Controller = p1,
                ZoneManager = core.ZoneManager,
                ElementPool = core.ElementPool,
            };
            List<Entity> Resolve(string domain)
                => CardCore.TargetAxisRules.TryParseDomain(domain, out var ax)
                    ? CardCore.TargetAxisResolver.GetCandidates(ax, ctx)
                    : new List<Entity>();
            var byField = Resolve("对方场地");
            Check(byField.Count == 1 && ReferenceEquals(byField[0], field),
                $"⑥ 对方场地应恰取该场地卡（实 {byField.Count}）");
            var byEnch = Resolve("对方战场:结界");
            Check(!byEnch.Contains(field),
                "⑥ 对方战场:结界 不可达场地（类别域退出）");
            var byCard = Resolve("对方战场:卡");
            Check(!byCard.Contains(field),
                "⑥ 对方战场:卡 不可达场地（:卡 亦不命中——拍板②）");
        }

        // ======================================== ⑦ 摧毁原子 ========================================

        private static void SectionSmash(GameCore core, Player p1, Player p2)
        {
            var field = MakeField(core, p2, 2);
            var handler = new SmashHandler();
            var ctx = new EffectExecutionContext
            {
                Source = p1,
                Controller = p1,
                ZoneManager = core.ZoneManager,
                ElementPool = core.ElementPool,
                Targets = new List<Entity> { field },
            };
            var inst = new AtomicEffectInstance { Type = CardCore.AtomicEffectType.Smash, Value = 1 };
            handler.Execute(inst, ctx);
            Check(field.GetZone() == Zone.Graveyard
                       && core.ZoneManager.GetCards(p2, Zone.Graveyard).Contains(field),
                $"⑦ 摧毁原子对场地走 Smash 直毁入墓（handler 无需改；区={field.GetZone()}）");
        }
    }
}
