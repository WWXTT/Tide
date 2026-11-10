using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using GameBoard;
using CardCore.Attribute;
using CardCore.Attribute.Handlers;
using CardCore.Damage;

namespace CardCore.Editor
{
    // =====================================================================================
    // 伤害管线与循环链验证（2026-11-09《伤害管线与循环链接口草案》§7.4 golden + §9 清单）。
    //
    // 三段：
    //   ① golden 对拍——同一夹具跑 Legacy（ApplyDamageLegacy + 段末 SBA）与
    //      Chain(OneBounce)（管线+后效+链，逐事件 SBA checkpoint），比 dealt/终态/事件多重集；
    //   ② 已裁决差异记录——暴击×减半取整（§2.4）、守护改写不重跑替代（RedirectPolicy）；
    //   ③ Chain 专属——UntilDeath+Life(1) 收敛、StateRepeat、预算断言、Visited、opt-in 路由。
    // 已知且豁免的事件差异：报复反噬 KeywordAppliedEvent.Source 改填伤害来源（§6.1.1 口径修正）
    //——对拍只比 类型|目标|文案，不比 Source 字段。
    // =====================================================================================
    public static partial class CardPipelineVerifier
    {
        [MenuItem("Tools/验证/伤害链（E2E）")]
        public static void RunDamageChainStandalone()
        {
            _pass = 0;
            _fail = 0;
            var cardsData = new List<CardData>();
            try { cardsData.AddRange(SynergyUI.CardCatalog.LoadAll()); }
            catch (System.Exception e) { Debug.LogWarning($"[伤害链] 正式卡池加载失败：{e.Message}"); }
            var core = GameCore.Instance;
            var deck1 = cardsData.Count > 0 ? CardLoader.BuildDeck(cardsData, 1) : new List<Card>();
            var deck2 = cardsData.Count > 0 ? CardLoader.BuildDeck(cardsData, 1) : new List<Card>();
            CardCore.ZoneContainer.Reseed(20260910);
            core.InitGame(deck1, deck2);
            TestDamageChain(core, core.Player1, core.Player2);
            Debug.Log($"[伤害链] 完成 — PASS={_pass} FAIL={_fail}");
        }

        private static void TestDamageChain(GameCore core, Player p1, Player p2)
        {
            Crumb("→伤害链 层序");
            TestDamageChainStageOrder();
            Crumb("→伤害链 golden");
            TestDamageChainGolden(core, p1, p2);
            Crumb("→伤害链 差异记录");
            TestDamageChainRecordedDiffs(core, p1, p2);
            Crumb("→伤害链 Chain 专属");
            TestDamageChainSemantics(core, p1, p2);
            Crumb("→伤害链 传播边");
            TestDamageChainEdges(core, p1, p2);
            Crumb("→每卡过载闸（§11）");
            TestOverloadGate(core, p1, p2);
            ResetDamageChainField(core, p1, p2); // 段界清理
        }

        // ------------------------------ 层序（§9-1） ------------------------------

        private static void TestDamageChainStageOrder()
        {
            Assert(DamageStageOrder.Target < DamageStageOrder.Replace
                   && DamageStageOrder.Replace < DamageStageOrder.Redirect
                   && DamageStageOrder.Redirect < DamageStageOrder.Amplify
                   && DamageStageOrder.Amplify < DamageStageOrder.Prevent
                   && DamageStageOrder.Prevent < DamageStageOrder.FinalModify
                   && DamageStageOrder.FinalModify < DamageStageOrder.Apply,
                "伤害链：层序常量单调（Target<Replace<Redirect<Amplify<Prevent<FinalModify<Apply）");

            var orders = DamagePipelineFactory.CreateDefault().Stages.Select(s => s.Order).ToList();
            Assert(orders.SequenceEqual(new List<int>
                   {
                       DamageStageOrder.Replace, DamageStageOrder.Redirect, DamageStageOrder.Amplify,
                       DamageStageOrder.Prevent, DamageStageOrder.FinalModify, DamageStageOrder.Apply,
                   }),
                $"伤害链：默认管线按层序装配（实际 [{string.Join(",", orders)}]，Target=校验器非 Stage）");
        }

        // ------------------------------ golden 对拍 ------------------------------

        private sealed class DamageChainBoard
        {
            public GameCore Core;
            public Player P1, P2;
            public List<Card> Made = new List<Card>();

            public Card Make(Player owner, string name, int power, int life, Cardtype type = Cardtype.Creature)
            {
                var data = new CardData { ID = "VERIFY_DC_" + name, CardName = name };
                data.Supertype = type;
                data.AttackAbility = true;
                data.GuardAbility = true;
                data.Power = power;
                data.Life = life;
                var card = new CardWrapper(data);
                card.SetController(owner);
                Assert(Core.ZoneManager.TryAddToBattlefield(card, owner),
                       $"伤害链夹具入场成功（{name}）");
                card.Untap();
                Made.Add(card);
                return card;
            }
        }

        private sealed class DamageEventRecorder
        {
            public readonly List<string> Events = new List<string>();

            public void Wire()
            {
                EventManager.Instance.Subscribe<DamageEvent>(OnDamage);
                EventManager.Instance.Subscribe<CombatDamageEvent>(OnCombat);
                EventManager.Instance.Subscribe<LifeChangeEvent>(OnLife);
                EventManager.Instance.Subscribe<KeywordAppliedEvent>(OnKw);
                EventManager.Instance.Subscribe<CounterChangedEvent>(OnCounter);
                EventManager.Instance.Subscribe<CardDestroyEvent>(OnDestroy);
            }

            public void Unwire()
            {
                EventManager.Instance.Unsubscribe<DamageEvent>(OnDamage);
                EventManager.Instance.Unsubscribe<CombatDamageEvent>(OnCombat);
                EventManager.Instance.Unsubscribe<LifeChangeEvent>(OnLife);
                EventManager.Instance.Unsubscribe<KeywordAppliedEvent>(OnKw);
                EventManager.Instance.Unsubscribe<CounterChangedEvent>(OnCounter);
                EventManager.Instance.Unsubscribe<CardDestroyEvent>(OnDestroy);
            }

            private static string N(Entity e) => e == null ? "∅" : EffectText.Name(e);

            private void OnDamage(DamageEvent e) => Events.Add($"Damage|{N(e.Source)}→{N(e.Target)}|{e.Amount}");
            private void OnCombat(CombatDamageEvent e) => Events.Add($"Combat|{N(e.Attacker)}→{N(e.Defender)}|{e.Damage}");
            private void OnLife(LifeChangeEvent e) => Events.Add($"Life|{N(e.Player)}|{e.OldLife}→{e.NewLife}");
            private void OnKw(KeywordAppliedEvent e) => Events.Add($"Kw|{N(e.Target)}|{e.Keyword}|{e.Detail}");
            private void OnCounter(CounterChangedEvent e) => Events.Add($"Ctr|{N(e.Target)}|{e.CounterType}|{e.Amount}");
            private void OnDestroy(CardDestroyEvent e) => Events.Add($"Destroy|{N(e.DestroyedCard)}|{e.Reason}");
        }

        private static void ResetDamageChainField(GameCore core, Player p1, Player p2)
        {
            foreach (var p in new[] { p1, p2 })
            {
                var container = core.ZoneManager.GetZoneContainer(p);
                foreach (var c in core.ZoneManager.GetCards(p, Zone.Battlefield).ToList())
                    container.Remove(c, Zone.Battlefield);
            }
            p1.Life = p1.GetMaxLife();
            p2.Life = p2.GetMaxLife();
        }

        private static List<string> SnapshotDamageChainState(Player p1, Player p2, IEnumerable<Card> made)
        {
            var list = new List<string>();
            foreach (var c in made)
                list.Add($"{EffectText.Name(c)}:alive={c.IsAlive},zone={c.GetZone()},life={c.GetLife()}" +
                         $",armor={c.GetEffectiveCounterCount(KeywordRules.ArmorCounter)}" +
                         $",thorns={c.GetEffectiveCounterCount(CounterRules.RetaliationCounter)}" +
                         $",vuln={c.GetEffectiveCounterCount(CounterRules.VulnerableCounter)}" +
                         $",tough={c.GetEffectiveCounterCount(CounterRules.ToughnessCounter)}" +
                         $",divine={c.GetEffectiveCounterCount(CounterRules.DivineShieldCounter)}" +
                         $",dur={c.GetEffectiveCounterCount(CounterRules.DurabilityCounter)}" +
                         $",toxin={c.GetEffectiveCounterCount(CounterRules.ToxinCounter)}");
            list.Add($"P1:life={p1.Life}");
            list.Add($"P2:life={p2.Life}");
            return list;
        }

        /// <summary>golden 对拍：同构双板，Legacy(+段末 SBA) vs Chain(OneBounce, 链内 checkpoint)，
        /// 比 dealt / 终态快照 / 事件多重集（顺序豁免：链 BFS vs Legacy DFS 交错，只比多重集）。</summary>
        private static void DamageChainGolden(GameCore core, Player p1, Player p2,
            string label, bool isCombat, int amount,
            Action<DamageChainBoard> setup, Func<DamageChainBoard, Entity[]> pick)
        {
            // Legacy 侧
            ResetDamageChainField(core, p1, p2);
            var b1 = new DamageChainBoard { Core = core, P1 = p1, P2 = p2 };
            setup(b1);
            var pair1 = pick(b1);
            var rec1 = new DamageEventRecorder();
            int dealt1;
            rec1.Wire();
            try
            {
                dealt1 = KeywordRules.ApplyDamageLegacy(pair1[0], pair1[1], amount, isCombat);
                core.SBAEngine.ExecuteAll();
            }
            finally { rec1.Unwire(); }
            var snap1 = SnapshotDamageChainState(p1, p2, b1.Made);

            // Chain(OneBounce) 侧
            ResetDamageChainField(core, p1, p2);
            var b2 = new DamageChainBoard { Core = core, P1 = p1, P2 = p2 };
            setup(b2);
            var pair2 = pick(b2);
            var rec2 = new DamageEventRecorder();
            int dealt2;
            rec2.Wire();
            try
            {
                var request = new DamageRequest
                {
                    Source = pair2[0],
                    Target = pair2[1],
                    Amount = amount,
                    Kind = isCombat ? DamageKind.Combat : DamageKind.Effect,
                    Tags = DamageTags.None,
                };
                dealt2 = ChainDamageResolver.Instance.Resolve(request, ChainDamageResolver.OneBouncePolicy()).Dealt;
            }
            finally { rec2.Unwire(); }
            var snap2 = SnapshotDamageChainState(p1, p2, b2.Made);

            Assert(dealt1 == dealt2, $"伤害链golden[{label}] dealt 对拍：L={dealt1} C={dealt2}");
            Assert(Enumerable.SequenceEqual(snap1.OrderBy(x => x), snap2.OrderBy(x => x)),
                   $"伤害链golden[{label}] 终态对拍\nL=[{string.Join(" | ", snap1)}]\nC=[{string.Join(" | ", snap2)}]");
            Assert(Enumerable.SequenceEqual(rec1.Events.OrderBy(x => x), rec2.Events.OrderBy(x => x)),
                   $"伤害链golden[{label}] 事件对拍（多重集）\nL=[{string.Join("\n  ", rec1.Events)}]\nC=[{string.Join("\n  ", rec2.Events)}]");
        }

        private static void TestDamageChainGolden(GameCore core, Player p1, Player p2)
        {
            ResetDamageChainField(core, p1, p2);

            // G1/G2 裸伤害（效果/战斗）
            DamageChainGolden(core, p1, p2, "裸效果伤害", false, 2,
                b => { b.Make(p1, "金攻", 3, 5); b.Make(p2, "金靶", 2, 5); },
                b => new Entity[] { b.Made[0], b.Made[1] });
            DamageChainGolden(core, p1, p2, "裸战斗伤害", true, 3,
                b => { b.Make(p1, "金攻二", 3, 5); b.Make(p2, "金靶二", 2, 5); },
                b => new Entity[] { b.Made[0], b.Made[1] });

            // G3 圣盾全挡（dealt=0、无 DamageEvent）
            DamageChainGolden(core, p1, p2, "圣盾全挡", false, 5,
                b =>
                {
                    b.Make(p1, "盾攻", 3, 5);
                    var t = b.Make(p2, "盾靶", 2, 5);
                    t.AddCounters(CounterRules.DivineShieldCounter, 1);
                },
                b => new Entity[] { b.Made[0], b.Made[1] });

            // G4 圣盾全挡 × 易损不放大不消耗（2026-11-09 双改版锚）
            DamageChainGolden(core, p1, p2, "圣盾全挡×易损保留", false, 5,
                b =>
                {
                    b.Make(p1, "盾攻二", 3, 5);
                    var t = b.Make(p2, "盾靶二", 2, 5);
                    t.AddCounters(CounterRules.DivineShieldCounter, 1);
                    t.AddCounters(CounterRules.VulnerableCounter, 2);
                },
                b => new Entity[] { b.Made[0], b.Made[1] });

            // G5 护甲部分吸收
            DamageChainGolden(core, p1, p2, "护甲部分吸收", false, 5,
                b =>
                {
                    b.Make(p1, "甲攻", 3, 5);
                    var t = b.Make(p2, "甲靶", 2, 5);
                    t.AddCounters(KeywordRules.ArmorCounter, 3);
                },
                b => new Entity[] { b.Made[0], b.Made[1] });

            // G6 坚韧（拦减后减半）
            DamageChainGolden(core, p1, p2, "坚韧拦减", false, 5,
                b =>
                {
                    b.Make(p1, "韧攻", 3, 5);
                    var t = b.Make(p2, "韧靶", 2, 6);
                    t.AddCounters(CounterRules.ToughnessCounter, 2);
                },
                b => new Entity[] { b.Made[0], b.Made[1] });

            // G7 易损放大+全部移除
            DamageChainGolden(core, p1, p2, "易损放大全移", false, 3,
                b =>
                {
                    b.Make(p1, "损攻", 3, 5);
                    var t = b.Make(p2, "损靶", 2, 8);
                    t.AddCounters(CounterRules.VulnerableCounter, 2);
                },
                b => new Entity[] { b.Made[0], b.Made[1] });

            // G8 暴击（战斗翻倍，效果不吃）
            DamageChainGolden(core, p1, p2, "暴击战斗翻倍", true, 3,
                b =>
                {
                    var s = b.Make(p1, "暴攻", 3, 5);
                    s.AddKeyword(KeywordRules.Crit, CardCore.KeywordLane.Status);
                    b.Make(p2, "暴靶", 2, 8);
                },
                b => new Entity[] { b.Made[0], b.Made[1] });

            // G9 无实体钳 1
            DamageChainGolden(core, p1, p2, "无实体钳1", false, 5,
                b =>
                {
                    b.Make(p1, "钳攻", 3, 5);
                    var t = b.Make(p2, "钳靶", 2, 5);
                    t.AddCounters(CounterRules.SpellbanCounter, 1);
                },
                b => new Entity[] { b.Made[0], b.Made[1] });

            // G10 减半护盾（无暴击=新旧同值）
            DamageChainGolden(core, p1, p2, "减半护盾", false, 3,
                b =>
                {
                    b.Make(p1, "半攻", 3, 5);
                    var t = b.Make(p2, "半靶", 2, 5);
                    t.AddCounters(CounterRules.HalveShieldCounter, 1);
                },
                b => new Entity[] { b.Made[0], b.Made[1] });

            // G11 免疫盾归 0（无事件）
            DamageChainGolden(core, p1, p2, "免疫盾归0", false, 3,
                b =>
                {
                    b.Make(p1, "免攻", 3, 5);
                    var t = b.Make(p2, "免靶", 2, 5);
                    t.AddCounters(CounterRules.NullShieldCounter, 1);
                },
                b => new Entity[] { b.Made[0], b.Made[1] });

            // G12 生命锁（落血不低于 1）
            DamageChainGolden(core, p1, p2, "生命锁", false, 5,
                b =>
                {
                    b.Make(p1, "锁攻", 3, 5);
                    var t = b.Make(p2, "锁靶", 2, 2);
                    t.AddCounters(CounterRules.LifeFloorCounter, 1);
                },
                b => new Entity[] { b.Made[0], b.Made[1] });

            // G13 结界耐久
            DamageChainGolden(core, p1, p2, "结界耐久", false, 3,
                b =>
                {
                    b.Make(p1, "界攻", 3, 5);
                    var t = b.Make(p2, "界靶", 0, 0, Cardtype.Enchantment);
                    t.AddCounters(CounterRules.DurabilityCounter, 5);
                },
                b => new Entity[] { b.Made[0], b.Made[1] });

            // G14 角色直伤（LifeChangeEvent）
            DamageChainGolden(core, p1, p2, "角色直伤", false, 4,
                b => { b.Make(p1, "角攻", 3, 5); },
                b => new Entity[] { b.Made[0], b.P2 });

            // G15 守护改写（角色受伤→守护者承受）
            DamageChainGolden(core, p1, p2, "守护改写", false, 4,
                b =>
                {
                    b.Make(p1, "护攻", 3, 5);
                    var g = b.Make(p2, "守护者", 3, 5);
                    g.AddKeyword(KeywordRules.Guardian, CardCore.KeywordLane.Status);
                },
                b => new Entity[] { b.Made[0], b.P2 });

            // G16 吸血（先打伤自己再结算，看回复）
            DamageChainGolden(core, p1, p2, "吸血回复", true, 3,
                b =>
                {
                    var s = b.Make(p1, "吸攻", 3, 5);
                    s.AddKeyword(KeywordRules.Lifesteal, CardCore.KeywordLane.Status);
                    b.Make(p2, "吸靶", 2, 5);
                    KeywordRules.ApplyDamageLegacy(b.P2, s, 2, false); // 预伤 2 → 生命 3（setup 内=不进对拍事件流）
                },
                b => new Entity[] { b.Made[0], b.Made[1] });

            // G17 系命（回复角色）
            DamageChainGolden(core, p1, p2, "系命回复", true, 3,
                b =>
                {
                    var s = b.Make(p1, "系攻", 3, 5);
                    s.AddKeyword(KeywordRules.Lifelink, CardCore.KeywordLane.Status);
                    b.Make(p2, "系靶", 2, 5);
                },
                b => new Entity[] { b.Made[0], b.Made[1] });

            // G18 荆棘反噬（单向：层数=4、生效减半）
            DamageChainGolden(core, p1, p2, "荆棘反噬", false, 3,
                b =>
                {
                    b.Make(p1, "棘攻", 3, 5);
                    var t = b.Make(p2, "棘靶", 2, 5);
                    t.AddCounters(CounterRules.RetaliationCounter, 4);
                },
                b => new Entity[] { b.Made[0], b.Made[1] });

            // G19 报复 OneBounce 双持（每次原始伤害至多反弹一次）
            DamageChainGolden(core, p1, p2, "报复单弹", false, 1,
                b =>
                {
                    var s = b.Make(p1, "报攻", 1, 3);
                    var t = b.Make(p2, "报靶", 1, 3);
                    s.AddKeyword(KeywordRules.Retaliate, CardCore.KeywordLane.Status);
                    t.AddKeyword(KeywordRules.Retaliate, CardCore.KeywordLane.Status);
                },
                b => new Entity[] { b.Made[0], b.Made[1] });

            // G20 剧毒消灭（战斗伤害非致死→结算后消灭）
            DamageChainGolden(core, p1, p2, "剧毒消灭", true, 2,
                b =>
                {
                    var s = b.Make(p1, "毒攻", 2, 5);
                    s.AddKeyword(KeywordRules.Venom, CardCore.KeywordLane.Status);
                    b.Make(p2, "毒靶", 2, 5);
                },
                b => new Entity[] { b.Made[0], b.Made[1] });

            // G21 病原体（来源 +1 毒素）
            DamageChainGolden(core, p1, p2, "病原体感染", false, 2,
                b =>
                {
                    b.Make(p1, "原攻", 3, 5);
                    var t = b.Make(p2, "原靶", 2, 5);
                    t.AddKeyword(KeywordRules.Pathogen, CardCore.KeywordLane.Status);
                },
                b => new Entity[] { b.Made[0], b.Made[1] });

            // G22 致死濒死反咬（报复持有者被打死仍反噬一口）
            DamageChainGolden(core, p1, p2, "致死反咬", false, 1,
                b =>
                {
                    b.Make(p1, "咬攻", 3, 5);
                    var t = b.Make(p2, "咬靶", 1, 1);
                    t.AddKeyword(KeywordRules.Retaliate, CardCore.KeywordLane.Status);
                },
                b => new Entity[] { b.Made[0], b.Made[1] });
        }

        // ------------------------------ 已裁决差异记录 ------------------------------

        /// <summary>测试替代件：只改写「目标为卡牌且伤害&gt;2」的 DamageEvent → 固定 2。
        /// 用于暴露守护改写后「是否重跑替代」的层序差异。</summary>
        private sealed class CardTargetCapReplacement : CardCore.ReplacementEffectBase
        {
            public CardTargetCapReplacement() : base(typeof(DamageEvent), "VERIFY_DC_CapCardDmg") { }

            public override bool CanReplace(CardCore.IGameEvent e)
                => e is DamageEvent de && de.Target is Card && de.Amount > 2;

            public override CardCore.IGameEvent CreateReplacement(CardCore.IGameEvent originalEvent, CardCore.Effect sourceEffect)
            {
                var de = (DamageEvent)originalEvent;
                return new DamageEvent { Source = de.Source, Target = de.Target, Amount = 2 };
            }
        }

        /// <summary>测试替代件：把任意伤害事件的目标改写为指定实体（改写到该实体即停，防替代环）。</summary>
        private sealed class RetargetToReplacement : CardCore.ReplacementEffectBase
        {
            private readonly Entity _to;
            public RetargetToReplacement(Entity to) : base(typeof(DamageEvent), "VERIFY_DC_Retarget") { _to = to; }

            public override bool CanReplace(CardCore.IGameEvent e)
                => e is DamageEvent de && !ReferenceEquals(de.Target, _to);

            public override CardCore.IGameEvent CreateReplacement(CardCore.IGameEvent originalEvent, CardCore.Effect sourceEffect)
            {
                var de = (DamageEvent)originalEvent;
                return new DamageEvent { Source = de.Source, Target = _to, Amount = de.Amount };
            }
        }

        private static void TestDamageChainRecordedDiffs(GameCore core, Player p1, Player p2)
        {
            // D1 暴击×减半取整（§2.4）：Legacy=减半(ceil)→暴击翻倍；Chain=暴击→减半(ceil)
            //   3 点：L ceil(3/2)=2→4；C 3→6→3。
            ResetDamageChainField(core, p1, p2);
            var dA1 = new DamageChainBoard { Core = core, P1 = p1, P2 = p2 };
            var critL = dA1.Make(p1, "取整攻", 3, 5);
            critL.AddKeyword(KeywordRules.Crit, CardCore.KeywordLane.Status);
            var halfL = dA1.Make(p2, "取整靶", 2, 8);
            halfL.AddCounters(CounterRules.HalveShieldCounter, 1);
            int dealtL = KeywordRules.ApplyDamageLegacy(critL, halfL, 3, true);
            core.SBAEngine.ExecuteAll();

            ResetDamageChainField(core, p1, p2);
            var dA2 = new DamageChainBoard { Core = core, P1 = p1, P2 = p2 };
            var critC = dA2.Make(p1, "取整攻", 3, 5);
            critC.AddKeyword(KeywordRules.Crit, CardCore.KeywordLane.Status);
            var halfC = dA2.Make(p2, "取整靶", 2, 8);
            halfC.AddCounters(CounterRules.HalveShieldCounter, 1);
            int dealtC = ChainDamageResolver.Instance.Resolve(new DamageRequest
            {
                Source = critC, Target = halfC, Amount = 3, Kind = DamageKind.Combat, Tags = DamageTags.None,
            }, ChainDamageResolver.OneBouncePolicy()).Dealt;

            Assert(dealtL == 4 && dealtC == 3,
                   $"伤害链差异记录①（§2.4 已裁决：Amplify 先于 Prevent）：暴击×减半 L={dealtL}（减半→翻倍） C={dealtC}（翻倍→减半）");

            // D2 守护改写后不重跑替代：替代件只匹配「目标为卡」——Legacy 守护递归整段重入
            //（改写后目标=守护者卡→替代再命中钳 2）；Chain Redirect 不重跑 Replace（吃满 5）。
            var cap = new CardTargetCapReplacement();
            core.ReplacementEngine.RegisterReplacementEffect(cap, typeof(DamageEvent));
            try
            {
                ResetDamageChainField(core, p1, p2);
                var dB1 = new DamageChainBoard { Core = core, P1 = p1, P2 = p2 };
                var u1 = dB1.Make(p1, "替攻", 3, 5);
                var g1 = dB1.Make(p2, "替守", 3, 5);
                g1.AddKeyword(KeywordRules.Guardian, CardCore.KeywordLane.Status);
                int capL = KeywordRules.ApplyDamageLegacy(u1, p2, 5, false);
                core.SBAEngine.ExecuteAll();

                ResetDamageChainField(core, p1, p2);
                var dB2 = new DamageChainBoard { Core = core, P1 = p1, P2 = p2 };
                var u2 = dB2.Make(p1, "替攻", 3, 5);
                var g2 = dB2.Make(p2, "替守", 3, 5);
                g2.AddKeyword(KeywordRules.Guardian, CardCore.KeywordLane.Status);
                int capC = ChainDamageResolver.Instance.Resolve(new DamageRequest
                {
                    Source = u2, Target = p2, Amount = 5, Kind = DamageKind.Effect, Tags = DamageTags.None,
                }, ChainDamageResolver.OneBouncePolicy()).Dealt;

                Assert(capL == 2 && capC == 5,
                       $"伤害链差异记录②（RedirectPolicy.ReapplyReplacements=false 已裁决）：守护改写后 Legacy 重跑替代钳 {capL} vs Chain 不重跑吃满 {capC}");
            }
            finally
            {
                core.ReplacementEngine.UnregisterReplacementEffect(cap, typeof(DamageEvent));
            }

            // D3 替代改目标后复检（§2.2.2 修复，§9-2 补测）：替代件把伤害改写到已死实体 →
            //    Chain 在下一层前复检目标存活 → Fizzled（dealt=0、零事件）；Legacy 无复检
            //    （替代改目标后照走尸体落血）——新层序规则，差异记录在案。
            ResetDamageChainField(core, p1, p2);
            var dC1 = new DamageChainBoard { Core = core, P1 = p1, P2 = p2 };
            var d3Atk = dC1.Make(p1, "替死攻", 3, 5);
            var d3Live = dC1.Make(p2, "替死活靶", 2, 5);
            var d3Corpse = dC1.Make(p2, "替死尸", 2, 5);
            KeywordRules.ApplyDamageLegacy(p1, d3Corpse, 99, false); // 预杀
            core.SBAEngine.ExecuteAll(); // 尸体先行入墓（否则链内 checkpoint 送墓的 CardDestroyEvent 会进录制窗口——非伤害路径事件）
            var retarget = new RetargetToReplacement(d3Corpse);
            core.ReplacementEngine.RegisterReplacementEffect(retarget, typeof(DamageEvent));
            try
            {
                var rec3 = new DamageEventRecorder();
                DamageResult r3;
                rec3.Wire();
                try
                {
                    r3 = ChainDamageResolver.Instance.Resolve(new DamageRequest
                    {
                        Source = d3Atk, Target = d3Live, Amount = 2, Kind = DamageKind.Effect, Tags = DamageTags.None,
                    }, ChainDamageResolver.OneBouncePolicy());
                }
                finally { rec3.Unwire(); }
                Assert(r3.Fizzled && r3.Dealt == 0 && rec3.Events.Count == 0 && d3Live.GetLife() == 5,
                       $"伤害链差异记录③（§2.2.2 目标复检）：替代改写到已死实体 → fizzle（Fizzled={r3.Fizzled}，dealt={r3.Dealt}，事件数={rec3.Events.Count}，活靶未受击={d3Live.GetLife()}）——Legacy 无此复检");
            }
            finally
            {
                core.ReplacementEngine.UnregisterReplacementEffect(retarget, typeof(DamageEvent));
            }
        }

        // ------------------------------ Chain 专属语义 ------------------------------

        private static Card MakeRetalUnit(DamageChainBoard b, Player owner, string name, int power, int life,
            bool lifesteal = false, int thorns = 0)
        {
            var c = b.Make(owner, name, power, life);
            c.AddKeyword(KeywordRules.Retaliate, CardCore.KeywordLane.Status);
            if (lifesteal) c.AddKeyword(KeywordRules.Lifesteal, CardCore.KeywordLane.Status);
            if (thorns > 0) c.AddCounters(CounterRules.RetaliationCounter, thorns);
            return c;
        }

        private static void TestDamageChainSemantics(GameCore core, Player p1, Player p2)
        {
            // C1 报复互弹 UntilDeath+Life(1)（无吸血，3/3 双持）：血债=势函数，双亡收束
            ResetDamageChainField(core, p1, p2);
            var c1 = new DamageChainBoard { Core = core, P1 = p1, P2 = p2 };
            var a1 = MakeRetalUnit(c1, p1, "势攻", 1, 3);
            var b1u = MakeRetalUnit(c1, p2, "势靶", 1, 3);
            var r1 = ChainDamageResolver.Instance.Resolve(new DamageRequest
            {
                Source = a1, Target = b1u, Amount = 1, Kind = DamageKind.Combat, Tags = DamageTags.None,
            }, ChainDamageResolver.DefaultPolicy());
            Assert(r1.Outcome == ChainOutcome.Lethal && !a1.IsAlive && !b1u.IsAlive,
                   $"伤害链C1：报复互弹 UntilDeath+Life(1) 双亡收束（outcome={r1.Outcome}，a.alive={a1.IsAlive}，b.alive={b1u.IsAlive}）");

            // C2 报复+吸血 UntilDeath+Life(1)：吸血补不回血债——有限步收束、无预算/循环命中
            ResetDamageChainField(core, p1, p2);
            var c2 = new DamageChainBoard { Core = core, P1 = p1, P2 = p2 };
            var a2 = MakeRetalUnit(c2, p1, "血攻", 1, 3, lifesteal: true);
            var b2u = MakeRetalUnit(c2, p2, "血靶", 1, 3, lifesteal: true);
            var r2 = ChainDamageResolver.Instance.Resolve(new DamageRequest
            {
                Source = a2, Target = b2u, Amount = 1, Kind = DamageKind.Combat, Tags = DamageTags.None,
            }, ChainDamageResolver.DefaultPolicy());
            bool terminatedOk = r2.Outcome == ChainOutcome.Resolved || r2.Outcome == ChainOutcome.Lethal;
            bool decayed = !a2.IsAlive || !b2u.IsAlive
                           || a2.GetLife() + b2u.GetLife() < 6;
            Assert(terminatedOk && decayed,
                   $"伤害链C2：报复+吸血+Life(1) 有限步收束且总生命净减（outcome={r2.Outcome}，a={a2.GetLife()}，b={b2u.GetLife()}）");

            // C3 Life(0)+StateRepeat：无血债时状态严格循环 → StateRepeat 命中（不静默跑满）
            ResetDamageChainField(core, p1, p2);
            var c3 = new DamageChainBoard { Core = core, P1 = p1, P2 = p2 };
            var a3 = MakeRetalUnit(c3, p1, "环攻", 1, 3, lifesteal: true);
            var b3u = MakeRetalUnit(c3, p2, "环靶", 1, 3, lifesteal: true);
            var noCost = ChainDamageResolver.DefaultPolicy();
            noCost.Cost = LoopCost.None;
            var r3 = ChainDamageResolver.Instance.Resolve(new DamageRequest
            {
                Source = a3, Target = b3u, Amount = 1, Kind = DamageKind.Combat, Tags = DamageTags.None,
            }, noCost);
            Assert(r3.Outcome == ChainOutcome.StateRepeat,
                   $"伤害链C3：报复+吸血+Life(0) 状态重复检测命中（outcome={r3.Outcome}，非死循环/非跑满预算）");

            // C4 预算断言：关循环检测 + 小 MaxEvents → BudgetExhausted（记日志不静默）
            ResetDamageChainField(core, p1, p2);
            var c4 = new DamageChainBoard { Core = core, P1 = p1, P2 = p2 };
            var a4 = MakeRetalUnit(c4, p1, "算攻", 1, 3, lifesteal: true);
            var b4u = MakeRetalUnit(c4, p2, "算靶", 1, 3, lifesteal: true);
            var budget = ChainDamageResolver.DefaultPolicy();
            budget.Cost = LoopCost.None;
            budget.StopOnStateRepeat = false;
            budget.MaxEvents = 6;
            var r4 = ChainDamageResolver.Instance.Resolve(new DamageRequest
            {
                Source = a4, Target = b4u, Amount = 1, Kind = DamageKind.Combat, Tags = DamageTags.None,
            }, budget);
            Assert(r4.Outcome == ChainOutcome.BudgetExhausted,
                   $"伤害链C4：MaxEvents 断言命中并显式收束（outcome={r4.Outcome}）");

            // C5 报复+荆棘：Halve 层数几何衰减收敛、无预算命中
            ResetDamageChainField(core, p1, p2);
            var c5 = new DamageChainBoard { Core = core, P1 = p1, P2 = p2 };
            var a5 = c5.Make(p1, "衰攻", 1, 3);
            var b5u = MakeRetalUnit(c5, p2, "衰靶", 1, 5, thorns: 4);
            var r5 = ChainDamageResolver.Instance.Resolve(new DamageRequest
            {
                Source = a5, Target = b5u, Amount = 1, Kind = DamageKind.Combat, Tags = DamageTags.None,
            }, ChainDamageResolver.DefaultPolicy());
            Assert(r5.Outcome != ChainOutcome.BudgetExhausted && r5.Outcome != ChainOutcome.StateRepeat
                   && b5u.GetEffectiveCounterCount(CounterRules.RetaliationCounter) == 2,
                   $"伤害链C5：报复+荆棘 Halve 收敛（outcome={r5.Outcome}，荆棘余层=2）");

            // C6 Visited 机制（传播类边；碾压/溅射接线属 Phase 5 内容侧，机制单测在此）
            var visit = new ChainContext { Policy = new ChainPolicy { AllowRevisit = false } };
            var dummy = c5.Make(p1, "访标", 1, 1);
            bool first = visit.TryVisit(dummy, "Splash");
            bool second = visit.TryVisit(dummy, "Splash");
            visit.Policy.AllowRevisit = true;
            bool third = visit.TryVisit(dummy, "Splash");
            Assert(first && !second && third,
                   "伤害链C6：Visited 每目标每关键词一次（AllowRevisit=false 拦重复；true 放行）");

            // C7 opt-in 路由：**默认恒 Legacy**（2026-11-10 用户两次裁定：默认切换未获批准——
            //     全绿只证新路径自洽）；谓词接线后链路由即时生效（机制就绪、接线待批准）
            ResetDamageChainField(core, p1, p2);
            var c7 = new DamageChainBoard { Core = core, P1 = p1, P2 = p2 };
            var plain1 = c7.Make(p1, "普攻", 1, 3);
            var plain2 = c7.Make(p2, "普靶", 1, 3);
            var retalUnit = MakeRetalUnit(c7, p1, "锐攻", 1, 3);
            Assert(DamageResolverRegistry.ChainOptIn == null
                   && DamageResolverRegistry.For(retalUnit, plain2) is LegacyDamageResolver
                   && DamageResolverRegistry.For(plain1, retalUnit) is LegacyDamageResolver
                   && DamageResolverRegistry.For(plain1, plain2) is LegacyDamageResolver,
                   "伤害链C7：默认恒 Legacy（含报复持有者对局——默认切换须显式批准，ChainOptIn 默认 null）");
            try
            {
                DamageResolverRegistry.ChainOptIn = DamageResolverRegistry.ChainNativeOptIn;
                Assert(DamageResolverRegistry.For(retalUnit, plain2) is ChainDamageResolver
                       && DamageResolverRegistry.For(plain1, retalUnit) is ChainDamageResolver
                       && DamageResolverRegistry.For(plain1, plain2) is LegacyDamageResolver,
                       "伤害链C7：接线 ChainNativeOptIn 后报复持有者切链（预置谓词可用，接线待批准）");
            }
            finally { DamageResolverRegistry.ChainOptIn = null; }

            // C8 facade 集成双验证：默认 Legacy 单弹（bounce guard 口径·零漂移锚）；
            //     注入接线后 UntilDeath+Life(1) 多弹（濒死反咬双尽，§6.4 样例口径）
            ResetDamageChainField(core, p1, p2);
            var c8 = new DamageChainBoard { Core = core, P1 = p1, P2 = p2 };
            var a8 = MakeRetalUnit(c8, p1, "锐攻", 1, 5);
            var b8u = MakeRetalUnit(c8, p2, "锐靶", 1, 5);
            KeywordRules.ApplyDamage(a8, b8u, 1, true); // facade → 默认 Legacy：单弹
            core.SBAEngine.ExecuteAll();
            Assert(a8.IsAlive && b8u.IsAlive && a8.GetLife() == 4 && b8u.GetLife() == 4,
                   $"伤害链C8a：默认 Legacy 单弹（攻血={a8.GetLife()} 靶血={b8u.GetLife()}（各 5−1，反跳一次即被 bounce guard 拦）——零漂移锚）");
            try
            {
                DamageResolverRegistry.ChainOptIn = DamageResolverRegistry.ChainNativeOptIn;
                ResetDamageChainField(core, p1, p2);
                var c8b = new DamageChainBoard { Core = core, P1 = p1, P2 = p2 };
                var a8b = MakeRetalUnit(c8b, p1, "锐攻", 1, 5);
                var b8b = MakeRetalUnit(c8b, p2, "锐靶", 1, 5);
                KeywordRules.ApplyDamage(a8b, b8b, 1, true); // facade → 注入接线 → Chain(UntilDeath+Life(1))
                core.SBAEngine.ExecuteAll();
                Assert(!b8b.IsAlive && !a8b.IsAlive,
                       $"伤害链C8b：接线后 facade 路由 UntilDeath——多弹+濒死反咬双尽（靶存活={b8b.IsAlive}，攻存活={a8b.IsAlive}）");
            }
            finally { DamageResolverRegistry.ChainOptIn = null; }
        }

        // ------------------------------ 传播边（§8，Phase 5） ------------------------------

        /// <summary>边运行器单测（直调，不依赖路由）。ResolvePair 集成：默认 Legacy 走原内联段
        ///（Keywords 段 11/11-2/11b 组即其回归锚）；链路由（ChainOptIn 接线）时走 RunXxxEdge。</summary>
        private static void TestDamageChainEdges(GameCore core, Player p1, Player p2)
        {
            // C7b opt-in 扩展：默认下碾压/溅射持有者同样恒 Legacy（默认切换须显式批准）；
            //     接线后传播类关键词一并切链
            ResetDamageChainField(core, p1, p2);
            var e0 = new DamageChainBoard { Core = core, P1 = p1, P2 = p2 };
            var owUnit = e0.Make(p1, "碾探", 3, 5);
            owUnit.AddKeyword(KeywordRules.Overwhelm, CardCore.KeywordLane.Status);
            var spUnit = e0.Make(p1, "溅探", 3, 5);
            spUnit.AddKeyword(KeywordRules.Splash, CardCore.KeywordLane.Status);
            var plainUnit = e0.Make(p2, "普探", 3, 5);
            Assert(DamageResolverRegistry.For(owUnit, plainUnit) is LegacyDamageResolver
                   && DamageResolverRegistry.For(spUnit, plainUnit) is LegacyDamageResolver,
                   "伤害链C7b：默认下碾压/溅射持有者恒 Legacy（存量红3 卡零漂移）");
            try
            {
                DamageResolverRegistry.ChainOptIn = DamageResolverRegistry.ChainNativeOptIn;
                Assert(DamageResolverRegistry.For(owUnit, plainUnit) is ChainDamageResolver
                       && DamageResolverRegistry.For(spUnit, plainUnit) is ChainDamageResolver,
                       "伤害链C7b：接线后传播类关键词（碾压/溅射）一并切链");
            }
            finally { DamageResolverRegistry.ChainOptIn = null; }

            // C9 碾压边 × 邻格报复：边内反噬收敛（边根不继承 Cost→报复自带 Life(1) 兜底；
            // 已死 Owner 不付血债）。邻格 1/3 吃 3 点死，垂死反噬 3 → 攻 5→2
            ResetDamageChainField(core, p1, p2);
            var c9 = new DamageChainBoard { Core = core, P1 = p1, P2 = p2 };
            var owAtk = c9.Make(p1, "碾攻", 3, 5);
            owAtk.AddKeyword(KeywordRules.Overwhelm, CardCore.KeywordLane.Status);
            var owPivot = c9.Make(p2, "碾轴", 2, 5);
            var owNeighbor = MakeRetalUnit(c9, p2, "碾邻", 1, 3);
            ChainDamageResolver.Instance.RunOverwhelmEdge(owAtk, owPivot,
                new List<Entity> { owNeighbor }, p2, lockedPower: 3);
            core.SBAEngine.ExecuteAll();
            Assert(!owNeighbor.IsAlive && owAtk.IsAlive && owAtk.GetLife() == 2 && owPivot.GetLife() == 5,
                   $"伤害链C9：碾压边内邻格报复收敛（邻死={owNeighbor.IsAlive==false}，攻血={owAtk.GetLife()}（5−反噬3），轴未吃溅射={owPivot.GetLife()}）");

            // C10 溅射边 × 受害者报复：溢出链内反噬收敛（攻 9−5=4，已死受害者不付血债）
            ResetDamageChainField(core, p1, p2);
            var c10 = new DamageChainBoard { Core = core, P1 = p1, P2 = p2 };
            var spAtk = c10.Make(p1, "溅攻", 6, 9);
            spAtk.AddKeyword(KeywordRules.Splash, CardCore.KeywordLane.Status);
            var spBait = c10.Make(p2, "溅饵", 0, 1);
            var spVictim = MakeRetalUnit(c10, p2, "溅害", 0, 2);
            ChainDamageResolver.Instance.RunSplashEdge(spAtk, spBait, lockedPower: 6, mainLifeSnapshot: 1);
            core.SBAEngine.ExecuteAll();
            Assert(!spVictim.IsAlive && spAtk.IsAlive && spAtk.GetLife() == 4,
                   $"伤害链C10：溅射边内受害者报复收敛（受害死={spVictim.IsAlive==false}，攻血={spAtk.GetLife()}（9−反噬5））");

            // C11 不连通（§8）：溅射伤害不再触发碾压——双关键词攻击者各边只打各自域、各目标恰一次。
            //     邻格=结界（碾压不限类别照打；溅射池只选生物→池唯一候选=双池，RNG 确定化）
            ResetDamageChainField(core, p1, p2);
            var c11 = new DamageChainBoard { Core = core, P1 = p1, P2 = p2 };
            var dualAtk = c11.Make(p1, "双攻", 3, 9);
            dualAtk.AddKeyword(KeywordRules.Overwhelm, CardCore.KeywordLane.Status);
            dualAtk.AddKeyword(KeywordRules.Splash, CardCore.KeywordLane.Status);
            var dualPivot = c11.Make(p2, "双轴", 2, 2);
            var dualNeighbor = c11.Make(p2, "双邻", 0, 0, Cardtype.Enchantment);
            dualNeighbor.AddCounters(CounterRules.DurabilityCounter, 5);
            int dualDurBase = dualNeighbor.GetCounterCount(CounterRules.DurabilityCounter); // 基线快照（铭刻光环在场时加层 +1，绝对值不钉死）
            var dualPool = c11.Make(p2, "双池", 0, 1);
            var rec11 = new DamageEventRecorder();
            rec11.Wire();
            try
            {
                ChainDamageResolver.Instance.RunOverwhelmEdge(dualAtk, dualPivot,
                    new List<Entity> { dualNeighbor }, p2, lockedPower: 3);
                ChainDamageResolver.Instance.RunSplashEdge(dualAtk, dualPivot, lockedPower: 3, mainLifeSnapshot: 2);
                core.SBAEngine.ExecuteAll();
            }
            finally { rec11.Unwire(); }
            int neighborHits = DamageHitsOf(rec11, dualNeighbor);
            int poolHits = DamageHitsOf(rec11, dualPool);
            Assert(neighborHits == 1 && poolHits == 1 && !dualPool.IsAlive
                   && dualNeighbor.GetCounterCount(CounterRules.DurabilityCounter) == dualDurBase - 3,
                   $"伤害链C11：不连通——溅射不再触发碾压（结界邻格命中={neighborHits}（=1，碾压域），池靶命中={poolHits}（=1，仅溅射），结界耐久 {dualDurBase}→{dualNeighbor.GetCounterCount(CounterRules.DurabilityCounter)}（−3））");

            // C12 碾压 Visited 去重：同一邻格重复出现在域快照中只打一次（每目标每链一次）
            ResetDamageChainField(core, p1, p2);
            var c12 = new DamageChainBoard { Core = core, P1 = p1, P2 = p2 };
            var dupAtk = c12.Make(p1, "复攻", 3, 9);
            dupAtk.AddKeyword(KeywordRules.Overwhelm, CardCore.KeywordLane.Status);
            var dupPivot = c12.Make(p2, "复轴", 2, 5);
            var dupNeighbor = c12.Make(p2, "复邻", 2, 5);
            var rec12 = new DamageEventRecorder();
            rec12.Wire();
            try
            {
                ChainDamageResolver.Instance.RunOverwhelmEdge(dupAtk, dupPivot,
                    new List<Entity> { dupNeighbor, dupNeighbor, dupPivot }, p2, lockedPower: 3);
            }
            finally { rec12.Unwire(); }
            int dupHits = DamageHitsOf(rec12, dupNeighbor);
            Assert(dupHits == 1 && dupNeighbor.GetLife() == 2 && dupPivot.GetLife() == 5,
                   $"伤害链C12：碾压 Visited 每目标每链一次（重复域条目命中={dupHits}（=1），轴排除未重复吃溅射）");
        }

        /// <summary>目标被实际伤害命中的次数（只数 DamageEvent——战斗命中另有 CombatDamageEvent
        /// 同报，混数会双计）。</summary>
        private static int DamageHitsOf(DamageEventRecorder recorder, Entity target)
            => recorder.Events.Count(x => x.StartsWith("Damage|") && x.Contains($"→{EffectText.Name(target)}|"));

        // ------------------------------ 每卡过载闸（§11，2026-11-10） ------------------------------

        /// <summary>把夹具卡灌满 30 次预算（真实 Commit 路径——过载指示物由预算挂载，
        /// 回合重置可正常移除）。</summary>
        private static void OverloadFixture(Card c)
        {
            for (int i = 0; i < CardEffectBudget.Limit; i++)
                CardEffectBudget.Instance.Commit(c, CardEffectCommitKind.Keyword, out _);
        }

        private static void TestOverloadGate(GameCore core, Player p1, Player p2)
        {
            var budget = CardEffectBudget.Instance;

            // E1 预算单元：29 次不挂载、第 30 次挂载+事件、31 次拒绝、退款回落、回合重置清账
            ResetDamageChainField(core, p1, p2);
            var e1 = new DamageChainBoard { Core = core, P1 = p1, P2 = p2 };
            var unit = e1.Make(p1, "预靶", 2, 5);
            int overloadEvents = 0;
            EventManager.Instance.Subscribe<CardOverloadedEvent>(OnOverload);
            try
            {
                for (int i = 0; i < CardEffectBudget.Limit - 1; i++)
                    budget.Commit(unit, CardEffectCommitKind.Keyword, out _);
                Assert(budget.Count(unit) == CardEffectBudget.Limit - 1
                       && !budget.IsOverloaded(unit)
                       && unit.GetCounterCount(CounterRules.OverloadCounter) == 0,
                       "过载E1a：29 次不挂载（指示物零层）");
                budget.Commit(unit, CardEffectCommitKind.Keyword, out _);
                string reject = null;
                bool commitOk = budget.CanCommit(unit, out reject);
                Assert(budget.IsOverloaded(unit) && unit.GetCounterCount(CounterRules.OverloadCounter) == 1
                       && !commitOk && reject != null,
                       $"过载E1b：第 30 次挂载过载指示物并拒绝后续（拒绝语={reject}）——第 30 次本身照常结算（结算后计）");
                budget.Refund(unit);
                Assert(!budget.IsOverloaded(unit) && budget.Count(unit) == CardEffectBudget.Limit - 1
                       && unit.GetCounterCount(CounterRules.OverloadCounter) == 0,
                       "过载E1c：发动无效退款——计数回落并移除过载（边界情况）");
            }
            finally { EventManager.Instance.Unsubscribe<CardOverloadedEvent>(OnOverload); }
            Assert(overloadEvents >= 1, $"过载E1d：挂载发 CardOverloadedEvent（实际 {overloadEvents} 次）");
            void OnOverload(CardOverloadedEvent e) { if (e.Card == unit) overloadEvents++; }

            // E2 宣言闸：攻击/守卫拒绝过载卡（按主动效果失效处理，§11.5）
            ResetDamageChainField(core, p1, p2);
            var e2 = new DamageChainBoard { Core = core, P1 = p1, P2 = p2 };
            var atk = e2.Make(p1, "宣攻", 3, 5);
            var def = e2.Make(p2, "宣靶", 3, 5);
            var guard = e2.Make(p2, "宣守卫", 3, 5);
            OverloadFixture(atk);
            OverloadFixture(guard);
            EnsureMainPhase(core, p1);
            string atkReason;
            Assert(!GameActions.DeclareAttack(core, p1, atk, def, out atkReason) && atkReason != null && atkReason.Contains("过载"),
                   $"过载E2a：过载卡攻击宣言被拒（reason={atkReason}）");
            var atkOk = e2.Make(p1, "常攻", 3, 5);
            Assert(GameActions.DeclareAttack(core, p1, atkOk, guard, out _),
                   "过载E2b：正常卡照常宣言（对照组——攻击入口未被误伤；靶=过载守卫可被攻击）");
            // 守卫闸：守卫者过载 → PushGuardDeclaration 拒绝（攻击已上栈，守卫速度1可响应）
            var attackEntry = core.StackEngine.PeekStack();
            Assert(attackEntry != null && attackEntry.IsAttackDeclaration, "过载E2c 前提：攻击宣言已上栈");
            Assert(!core.StackEngine.PushGuardDeclaration(guard, attackEntry, p2, out var guardReason)
                   && guardReason != null && guardReason.Contains("过载"),
                   $"过载E2c：过载卡守卫宣言被拒（reason={guardReason}）");
            GameActions.DrainStack(core);

            // E3 高危关键词压制（§11.5：过载期间自动触发型关键词不触发；指示物不压）
            ResetDamageChainField(core, p1, p2);
            var e3 = new DamageChainBoard { Core = core, P1 = p1, P2 = p2 };
            var critAtk = e3.Make(p1, "暴载", 3, 5);
            critAtk.AddKeyword(KeywordRules.Crit, CardCore.KeywordLane.Status);
            OverloadFixture(critAtk);
            var critTgt = e3.Make(p2, "暴靶", 2, 8);
            KeywordRules.ApplyDamageLegacy(critAtk, critTgt, 3, true);
            Assert(critTgt.GetLife() == 5,
                   $"过载E3a：过载暴击不翻倍（靶血={critTgt.GetLife()}（8−3，非 −6））");

            var stealAtk = e3.Make(p1, "吸载", 3, 5);
            stealAtk.AddKeyword(KeywordRules.Lifesteal, CardCore.KeywordLane.Status);
            OverloadFixture(stealAtk);
            KeywordRules.ApplyDamageLegacy(e3.P2, stealAtk, 2, false); // 预伤留回复空间
            int stealLife = stealAtk.GetLife();
            KeywordRules.ApplyDamageLegacy(stealAtk, e3.Make(p2, "吸靶", 2, 5), 2, false);
            Assert(stealAtk.GetLife() == stealLife,
                   $"过载E3b：过载吸血不回复（攻血 {stealLife}→{stealAtk.GetLife()} 不变）");

            var retalTgt = MakeRetalUnit(e3, p2, "报载", 2, 5);
            OverloadFixture(retalTgt);
            var retalAtk = e3.Make(p1, "报攻", 2, 5);
            KeywordRules.ApplyDamage(retalAtk, retalTgt, 1, false); // facade：报复持有者→链路由，RetaliateRule 过载门
            core.SBAEngine.ExecuteAll();
            Assert(retalTgt.GetLife() == 4 && retalAtk.GetLife() == 5,
                   $"过载E3c：过载报复不反噬（靶={retalTgt.GetLife()}（5−1），攻={retalAtk.GetLife()}（未吃反噬）——链路关键词闸）");

            // E4 指示物不被过载压 + 换区/净化不清（§11.4/11.5）
            ResetDamageChainField(core, p1, p2);
            var e4 = new DamageChainBoard { Core = core, P1 = p1, P2 = p2 };
            var shielded = e4.Make(p2, "盾载", 2, 5);
            shielded.AddCounters(CounterRules.DivineShieldCounter, 1);
            OverloadFixture(shielded);
            KeywordRules.ApplyDamageLegacy(e4.P1, shielded, 5, false);
            Assert(shielded.GetLife() == 5 && shielded.GetCounterCount(CounterRules.DivineShieldCounter) == 0,
                   "过载E4a：指示物不被过载压——圣盾照常抵挡并消耗（§11.5 指示物完全移出）");
            CounterRules.PurgeAll(shielded);
            CounterRules.ClearAll(shielded);
            Assert(shielded.GetCounterCount(CounterRules.OverloadCounter) == 1,
                   "过载E4b：过载不可净化、不可换区清除（双重清后仍 1 层）");

            // E6/E7 静态关键词不被过载压（2026-11-10 语义修复回归锚）：过载只压自动触发/主动/
            //     高危关键词/攻守宣言——HasKeyword 照真、守护改写照常（对照 E2c：守卫**宣言**仍被拒）
            var staticUnit = e4.Make(p1, "静载", 2, 5);
            staticUnit.AddKeyword(KeywordRules.Guardian, CardCore.KeywordLane.Status);
            staticUnit.AddKeyword(KeywordRules.Vigilance, CardCore.KeywordLane.Status);
            staticUnit.AddKeyword(KeywordRules.FirstStrike, CardCore.KeywordLane.Status);
            OverloadFixture(staticUnit);
            Assert(staticUnit.HasKeyword(KeywordRules.Guardian) && staticUnit.HasKeyword(KeywordRules.Vigilance)
                   && staticUnit.HasKeyword(KeywordRules.FirstStrike)
                   && !Attribute.SuppressRules.IsAutoSuppressed(staticUnit)
                   && Attribute.SuppressRules.IsOverloaded(staticUnit),
                   "过载E6：静态关键词读数不被过载压（守护/警戒/先攻照真；IsAutoSuppressed 回归纯沉睡/失效口径）——过载不走 HasKeyword 全局读");
            int staticLife = staticUnit.GetLife();
            KeywordRules.ApplyDamageLegacy(p2, p1, 3, false); // p2 打 p1 角色 → 守护改写到 p1 的过载守护者
            Assert(staticUnit.GetLife() == staticLife - 3,
                   $"过载E7：过载守护者照常改写承伤（守护=静态规则不压；静载 {staticLife}→{staticUnit.GetLife()}；对照 E2c 守卫宣言被拒=攻守口独立）");

            // E8 白名单计数（§11.2.1 OnTargeted——计入不压）：守护改写 +1、帷幕(Taunt)/隐密
            //    被指定为攻击目标 +1；隐密常态不可被指定（IsHiddenFromTargeting），该臂只在
            //    绕过隐藏的指定路径下才计——两关键词均为静态规则，过载不拦其生效
            budget.OnNewTurn(1000); // 清上一段计数
            ResetDamageChainField(core, p1, p2);
            var e8 = new DamageChainBoard { Core = core, P1 = p1, P2 = p2 };
            var guardCount = e8.Make(p1, "计守", 2, 5);
            guardCount.AddKeyword(KeywordRules.Guardian, CardCore.KeywordLane.Status);
            KeywordRules.ApplyDamageLegacy(p2, p1, 1, false); // 角色受伤 → 守护改写
            var veilTarget = e8.Make(p2, "计幕", 2, 5);
            veilTarget.AddKeyword(KeywordRules.Taunt, CardCore.KeywordLane.Status);
            var countAtk = e8.Make(p1, "计攻", 2, 5);
            Assert(GameActions.DeclareAttack(core, p1, countAtk, veilTarget, out _),
                   "过载E8 前提：指定帷幕持有者为攻击目标宣言成功");
            GameActions.DrainStack(core);
            Assert(budget.Count(guardCount) == 1 && budget.Count(veilTarget) == 1 && budget.Count(countAtk) == 1,
                   $"过载E8：白名单计数——守护改写 +1（实际 {budget.Count(guardCount)}）、帷幕被指定 +1（{budget.Count(veilTarget)}）、攻击宣言 +1（{budget.Count(countAtk)}）");

            // E9 真实入口：主动效果过载拒绝（单源预检 EffectExecutor.TryActivate=CanActivate 同路径）
            budget.OnNewTurn(1001);
            var activeDef = CardEffectConverter.ConvertOne(new CardEffectData
            {
                Id = "VERIFY_DC_ACTIVE",
                TriggerTiming = (int)TriggerTiming.Activate_Active,
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(CardCore.AtomicEffectType.DrawCard, value: 0) },
            }, "VERIFY_DC");
            var actSrc = e8.Make(p1, "主载", 2, 5);
            OverloadFixture(actSrc);
            string actReason;
            Assert(!GameActions.ActivateEffect(core, p1, activeDef, actSrc, 0, out actReason)
                   && actReason != null && actReason.Contains("过载"),
                   $"过载E9a：真实入口——过载源主动效果被拒（reason={actReason}）");
            var actOk = e8.Make(p1, "主常", 2, 5);
            string okReason;
            Assert(GameActions.ActivateEffect(core, p1, activeDef, actOk, 0, out okReason)
                   && budget.Count(actOk) == 1,
                   $"过载E9b：真实入口——正常源主动照常入栈并计数（计数={budget.Count(actOk)}，reason={okReason}）");
            GameActions.DrainStack(core);

            // E5 回合重置：OnNewTurn 移除指示物并清台账
            budget.OnNewTurn(999);
            Assert(!budget.IsOverloaded(shielded) && shielded.GetCounterCount(CounterRules.OverloadCounter) == 0
                   && budget.Count(shielded) == 0,
                   "过载E5：下回合开始移除过载指示物并清空预算台账");
        }
    }
}
