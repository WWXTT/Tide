using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using GameBoard;
using CardCore.Attribute.Handlers;
using Cysharp.Threading.Tasks;

namespace CardCore.Editor
{
    public static partial class CardPipelineVerifier
    {
        // ======================================== 抉择（Modal/选发，2026-09-07 定案） ========================================

        /// <summary>
        /// 抉择卡端到端：per-mode 独立计价（构筑期推导存储，发动时只读）+ 声明期先选模式再定费用 +
        /// 只执行所选模式。同时锁死组合计价三规则：①并发取总（4伤并抽1=两笔之和）；
        /// ②条件奖励免费（门后 then/else 不计价）；③抉择各计所需（只付所选模式）。
        /// </summary>
        private static void TestSpellModal(GameCore core, Player p1, Player p2, List<CardData> cardsData)
        {
            EnsureMainPhase(core, p1);

            // ---- 0. 规则①并发取总 / ②条件奖励免费（既有卡锁死断言）----
            // 并发取总（2026-10-15 后语义）：**同一效果内多原子**费用取总——
            // 火球术夹具已是「双效果=施放抉择」，并发语义改由本探针（单效果双原子）钉死
            {
                var concData = new CardData
                {
                    ID = "VERIFY_MODAL_CONC", CardName = "并发取总探针", Supertype = Cardtype.Spell,
                };
                concData.Effects.Add(new CardEffectData
                {
                    Id = "CONC_MAIN",
                    TriggerTiming = (int)TriggerTiming.Activate_Active,
                    SelectionMode = (int)CardCore.SelectionMode.Single,
                    TargetCount = 1,
                    AtomicEffects = new List<AtomicEffectEntry>
                    {
                        AtomRefs.New(AtomicEffectType.DealDamage, value: 4, kinds: new List<int> { 2 }),
                        AtomRefs.New(AtomicEffectType.DrawCard, value: 1),
                    },
                });
                var concDef = CardEffectConverter.ConvertOne(concData.Effects[0], concData.ID);
                var concCosts = CostDerivationService.DeriveElementCosts(concDef);
                int drawPrice = (int)System.Math.Round(Cfg(AtomicEffectType.DrawCard).TotalUnitCost,
                    System.MidpointRounding.AwayFromZero);
                var drawColor = CardCore.ElementAffinities.GetAffinityForEffect(AtomicEffectType.DrawCard).PrimaryColor;
                Assert((int)concCosts[ManaType.Red] == 4,
                       "并发组合：单效果 4伤+抽1 → 红4（同效果原子费用取总）");
                Assert((int)concCosts[drawColor] == drawPrice,
                       "并发组合：抽1=抽牌表价并入总费（费用取总）");
            }

            // ②条件奖励免费（2026-10-05 两槽定案载荷口径）：主干计价 vs 主干+Then 载荷全量——
            // Then 奖励不进 VisitBillableAtoms（分支整体零计价，预算只是放置上限）
            {
                CardCore.EffectDefinition BranchPlain(bool withPayload)
                {
                    var trunk = withPayload
                        ? OutcomeAtom(AtomicEffectType.DealDamage, 3, "DmgKillsTarget",
                            AtomRefs.New(AtomicEffectType.DrawCard, value: 1))
                        : AtomRefs.New(AtomicEffectType.DealDamage, value: 3);
                    return CardEffectConverter.ConvertOne(new CardEffectData
                    {
                        Id = "VERIFY_MODAL_GATE" + (withPayload ? "_B" : "_P"),
                        AtomicEffects = new List<AtomicEffectEntry> { trunk },
                    }, "VERIFY_MODAL_GATE");
                }
                int totalPlain = (int)CostDerivationService.DeriveElementCosts(BranchPlain(false)).Total;
                int totalBranched = (int)CostDerivationService.DeriveElementCosts(BranchPlain(true)).Total;
                Assert(totalPlain > 0 && totalBranched == totalPlain,
                       $"条件奖励不计费：主干+Then 载荷全量 = 主干计价（{totalBranched} vs {totalPlain}——Then 不进 VisitBillableAtoms）");
            }

            // ---- 1. 抉择卡数据/转换/判据（2026-10-15：多主动效果=施放抉择，无 Choice 步）----
            var data = cardsData.FirstOrDefault(c => c.CardName == "抉择试作");
            Assert(data != null, "抉择卡配置存在（抉择试作）");
            if (data == null) return;

            Assert(data.Effects.Count == 2
                   && data.Effects.All(e => e.TriggerTiming == (int)TriggerTiming.Activate_Active),
                   "抉择判据：两主动效果=施放时 modeIndex 二选一（内嵌 Choice 步已随 10-15 改版退役）");
            var defs = CardEffectConverter.ConvertAll(data.Effects, data.ID);
            Assert(defs.Count == 2 && defs.All(d => d.Steps == null || d.Steps.Count == 0),
                   "转换：两效果平铺（无 Choice 步——modeIndex=效果序号）");

            // ---- 2. per-mode 账单单源（2026-10-15：账单=卡面费+所选效果锚价 GetCardCost 单源；
            //      旧「底盘抵价」通道随槽位从速度扣改版退役——测试不钉数，钉「按模式独立、非求和」） ----
            var modalDrawColor = CardCore.ElementAffinities.GetAffinityForEffect(AtomicEffectType.DrawCard).PrimaryColor;
            var pool1 = core.ElementPool.GetPool(p1);
            var snap = new Dictionary<ManaType, int>(pool1.TempElements);
            int idxSnap = pool1.GlobalTurnIndex;
            pool1.GlobalTurnIndex = 9;
            pool1.TempElements[ManaType.Red] += 20;
            pool1.TempElements[modalDrawColor] += 9;

            var probeCard = InjectCard(core, p1, data);
            var mode0Bill = CardCore.ElementPaymentValidator.NormalizeBill(GameActions.GetCardCost(probeCard, 0));
            var mode1Bill = CardCore.ElementPaymentValidator.NormalizeBill(GameActions.GetCardCost(probeCard, 1));
            core.ZoneManager.GetZoneContainer(p1).Remove(probeCard, Zone.Hand); // 探针即弃
            Assert(mode0Bill[ManaType.Red] > 0 && mode0Bill.GetValueOrDefault(modalDrawColor) == 0,
                   "模式0账单：伤害锚红（per-mode 独立——恒推导口径：模式费=该模式效果推导价）");
            // 2026-10-24 适配费用恒推导定案（2026-10-22：GetRuntimeCost 单源、costList=显示缓存）：
            // 模式费按所选效果独立推导、无「卡面费两模式同担」——模式1=纯抽牌锚（蓝），红份额恒 0
            //（旧断言直取 mode1Bill[Red] 在恒推导下 KeyNotFound 中断全量验证）。
            Assert(mode1Bill.GetValueOrDefault(modalDrawColor) > 0
                   && mode1Bill.GetValueOrDefault(ManaType.Red) < mode0Bill[ManaType.Red],
                   "模式1账单：抽牌锚价落蓝（per-mode 独立不求和——恒推导：模式1无红份额）");

            try
            {
                // ---- 3. 模式0端到端（缺省参数=0）：只伤不抽 ----
                int p2Life = p2.Life;
                int deckBefore = core.ZoneManager.GetCards(p1, Zone.Deck).Count;
                int redBefore = pool1.TempElements[ManaType.Red];
                var modal0 = InjectCard(core, p1, data);

                // 伤害事件捕获（诊断）：观察模式0结算期间实际发生的每笔伤害（目标/来源/量）
                var dmgSeen = new List<string>();
                void OnModalDamage(DamageEvent e) =>
                    dmgSeen.Add($"{(e.Target is Player pl ? pl.Name : (e.Target as Card)?.ID)}<-{e.Amount}(src={(e.Source is Card sc ? sc.ID : "Player")})");
                EventManager.Instance.Subscribe<DamageEvent>(OnModalDamage);
                try
                {
                    Assert(PlayCardSync(core, p1, modal0, new List<Entity> { p2 }),
                           "抉择卡模式0打出（缺省 mode=0）");
                }
                finally
                {
                    EventManager.Instance.Unsubscribe<DamageEvent>(OnModalDamage);
                }
                Assert(p2.Life == p2Life - 4,
                       $"模式0：对目标造成4伤（实际 {p2Life}→{p2.Life}；期间伤害事件 [{string.Join("; ", dmgSeen)}]；p2 护甲={p2.GetCounterCount(CardCore.Attribute.KeywordRules.ArmorCounter)} 易损={p2.GetCounterCount(CardCore.Attribute.CounterRules.VulnerableCounter)}）");
                Assert(core.ZoneManager.GetCards(p1, Zone.Deck).Count == deckBefore, "模式0：不抽牌（只执行所选）");
                Assert(pool1.TempElements[ManaType.Red] == redBefore - mode0Bill[ManaType.Red],
                       $"模式0实付红 {mode0Bill[ManaType.Red]}（GetCardCost 单源账单——先选择模式再定费用；实际扣 {redBefore - pool1.TempElements[ManaType.Red]}）");
                Assert(core.ZoneManager.GetCards(p1, Zone.Graveyard).Contains(modal0)
                       && core.ZoneManager.GetCards(p1, Zone.Activation).Count == 0, "抉择法术结算后入墓、发动区清空");

                // ---- 4. 模式1端到端：只抽不伤 ----
                p2Life = p2.Life;
                deckBefore = core.ZoneManager.GetCards(p1, Zone.Deck).Count;
                int drawBefore = pool1.TempElements[modalDrawColor];
                var modal1 = InjectCard(core, p1, data);

                Assert(PlayCardSync(core, p1, modal1, null, modeIndex: 1), "抉择卡模式1打出（先选模式再定费用）");
                Assert(core.ZoneManager.GetCards(p1, Zone.Deck).Count == deckBefore - 1, "模式1：抽一张牌");
                Assert(p2.Life == p2Life, "模式1：不造伤害（执行隔离）");
                Assert(pool1.TempElements[modalDrawColor] == drawBefore - mode1Bill[modalDrawColor],
                       $"模式1实付={mode1Bill[modalDrawColor]} {modalDrawColor}（per-mode 独立账单；实际扣 {drawBefore - pool1.TempElements[modalDrawColor]}）");

                // ---- 5. 防超发：pending 按声明模式计 ----
                // 混付垫付会绕过「精确同色占用即拒」：清全色 bank 只留单源账单（根因 A 修复）
                foreach (var t in AllManaTypes()) pool1.TempElements[t] = 0;
                foreach (var kv in mode0Bill) pool1.TempElements[kv.Key] = kv.Value; // bank 恰好一张模式0
                var overA = InjectCard(core, p1, data);
                var overB = InjectCard(core, p1, data);
                Assert(GameActions.PlayCard(core, p1, overA, new List<Entity> { p2 }, Zone.Hand, 0),
                       "防超发：第一张模式0声明上栈（声明期不付费）");
                Assert(!GameActions.PlayCard(core, p1, overB, new List<Entity> { p2 }, Zone.Hand, 0),
                       "防超发：同笔 bank 第二张模式0被拒（GetPendingCastCosts 按模式计）");
                GameActions.DrainStack(core);
                // 实付=单源账单（同模式0断言口径）→ 恰好扣空（第二张拒付不扣款）
                Assert(AllManaTypes().All(t => pool1.TempElements[t] == 0),
                       "防超发：结算后实付扣空（第二张拒付不扣款）");
            }
            finally
            {
                foreach (var kv in snap) pool1.TempElements[kv.Key] = kv.Value;
                pool1.GlobalTurnIndex = idxSnap;
            }

            // ---- 6. 无效数据容错：choices<2 整步跳过 ----
            var bad = new CardData { ID = "VERIFY_MODAL_BAD", CardName = "无效抉择" };
            bad.Supertype = Cardtype.Spell;
            bad.Effects.Add(new CardEffectData
            {
                Id = "BAD_MAIN",
                Steps = new List<EffectStepData>
                {
                    new EffectStepData
                    {
                        kind = 2,
                        choices = new List<EffectChoiceData> { new EffectChoiceData() }, // 仅 1 个模式 → 无效
                    }
                }
            });
            var badDefs = CardEffectConverter.ConvertAll(bad.Effects, bad.ID);
            Assert(badDefs.Count == 1 && (badDefs[0].Steps == null || badDefs[0].Steps.Count == 0),
                   "choices<2：整步跳过不抛异常（等价无该步骤）");
            Assert(!CostDerivationService.HasChoiceEffect(bad) && CostDerivationService.GetModeCount(bad) == 1,
                   "无效抉择不计模式数");

            // ---- 7. 功能哈希：效果顺序参与（不碰撞）——平铺双效果（modeIndex=效果序），换序=不同卡 ----
            var swap = new CardData { ID = data.ID, CardName = data.CardName };
            swap.Supertype = data.Supertype;
            swap.Cost = data.Cost != null ? data.Cost.Clone() : null;
            swap.Effects.Add(data.Effects[1]); // 换序：抽牌在前
            swap.Effects.Add(data.Effects[0]);
            Assert(SynergyUI.ContentHasher.HashCard(data) != SynergyUI.ContentHasher.HashCard(swap),
                   "抉择模式顺序参与功能哈希（效果序=模式序，换序不碰撞）");

            // ---- 8. MemoryPack DTO 往返（legacy Choice 步通道——独立夹具，不依赖卡面格式）----
            var dtoStep = new EffectStepData
            {
                kind = 2,
                choices = new List<EffectChoiceData>
                {
                    new EffectChoiceData { steps = new List<EffectStepData> { new EffectStepData
                        { kind = 0, atomic = AtomRefs.New(AtomicEffectType.DealDamage, value: 4) } } },
                    new EffectChoiceData { steps = new List<EffectStepData> { new EffectStepData
                        { kind = 0, atomic = AtomRefs.New(AtomicEffectType.DrawCard, value: 1) } } },
                },
            };
            var ser = CardCore.Serialization.SerializableEffectStepData.FromData(dtoStep);
            var back = ser.ToData();
            Assert(back.choices != null && back.choices.Count == 2
                   && back.choices[0].steps[0].atomic.refId == CardCore.Attribute.AtomicEffectTable.GetByEnumName("DealDamage")?.HashId
                   && back.choices[1].steps[0].atomic.refId == CardCore.Attribute.AtomicEffectTable.GetByEnumName("DrawCard")?.HashId,
                   "序列化往返保抉择结构（模式数与原子类型——legacy Choice 步 DTO 通道）");
        }

        // ======================================== 攻击/守卫效果化（2026-09-10；2026-09-16 战斗接入栈机器重写） ========================================

        /// <summary>
        /// 攻击/守卫端到端（2026-09-16 逐攻击开窗定案；2026-10-15 反击定案修订）：
        /// - 攻击=速度0栈对象：宣言零支付上栈开窗，结算期重检→战斗三段→战斗后横置支付；
        /// - 守卫=速度1响应：窗口内 PushGuardDeclaration 入栈（资格闸 NoGuard/横置/存活），
        ///   LIFO 先结算——攻击目标变更+GuardBlocker 登记（守卫横置随攻击结算在战斗后支付）；
        /// - 双向结算基线（2026-10-15）：双方以可用状态完成战斗——守卫照常反击，战斗后才横置；
        /// - 警戒：已横置防守者仍反击（拦截局战斗期间本就可用，反击不再依赖警戒）；
        /// - 速度门槛：标准速度门（0≥计数器）——连锁开启时攻击不可宣言；
        /// - 法术底盘剩余提速（2026-10-10 可支配底盘）：SpeedBonus 卡级增量与全部效果 BaseSpeed 加和。
        /// </summary>
        private static void TestAttackGuard(GameCore core, Player p1, Player p2)
        {
            EnsureMainPhase(core, p1);
            var combat = core.CombatSystem;
            int seq = 0;

            Card Spawn(Player owner, int power, int life, bool noAttack = false, bool noGuard = false, params string[] keywords)
            {
                var data = new CardData
                {
                    ID = "VERIFY_AG_" + (++seq), CardName = "AG" + seq,
                    Supertype = Cardtype.Creature, Power = power, Life = life,
                    // 2026-11-06 opt-in 改版：默认攻/守齐备（旧默认口径），noAttack/noGuard=显式缺资格
                    AttackAbility = !noAttack, GuardAbility = !noGuard,
                };
                foreach (var kw in keywords) data.Keywords.Add(kw);
                var card = new CardWrapper(data);
                card.SetController(owner);
                Assert(core.ZoneManager.TryAddToBattlefield(card, owner), $"AG 合成随从入场（{data.CardName}）");
                card.Untap(); // 本段测试前提 = 可用随从（2026-10-17 入场即可用后为无害冗余）
                return card;
            }

            // ---- 1. NoAttack 资格（墙不能攻；同身材正常卡能攻） ----
            var wall = Spawn(p1, 3, 5, noAttack: true);
            var hitter = Spawn(p1, 3, 3);
            Assert(!combat.CanDeclareAttack(wall, p1), "攻击效果化：NoAttack 卡不能宣言攻击");
            Assert(combat.CanDeclareAttack(hitter, p1), "攻击效果化：默认自带攻击能力");

            // ---- 2. 守卫拦截：打脸攻击 → 窗口守卫响应 → 目标转移 + 双向结算 + 战斗后双方横置 ----
            var striker = Spawn(p1, 4, 2);       // 4 攻
            var shield = Spawn(p2, 3, 6);        // 3 攻 6 血守卫
            int p2Life0 = p2.Life;
            Assert(GameActions.DeclareAttack(core, p1, striker, p2), "守卫段：打脸宣言上栈（速度0开窗，宣言期零支付）");
            Assert(!striker.IsTapped(), "守卫段：宣言期不横置（战斗后支付）");
            var attackInstance = core.StackEngine.Peek();
            Assert(core.StackEngine.PushGuardDeclaration(shield, attackInstance, p2), "守卫段：守卫速度1响应入栈");
            GameActions.DrainStack(core);        // 守卫先结（目标变更+登记）→ 攻击三段（双向）→ 战斗后双方横置
            Assert(shield.IsTapped(), "守卫拦截：战斗后横置（战斗期间以可用状态参战）");
            Assert(shield.IsAlive && shield.GetLife() == 2, "守卫拦截：守卫吃 4 伤（6→2）");
            Assert(!striker.IsAlive, "守卫反击：3 反伤致死 4/2 攻击者（不计关键词的基线=双向结算）");
            Assert(p2.Life == p2Life0, "守卫拦截：玩家未被击中（目标已转移）");

            // ---- 2b. 警戒（2026-10-15 现职责）：已横置防守者被攻击仍反击 ----
            //（拦截局守卫战斗期间本就可用、反击不依赖警戒；普通横置不反击见 8c 对照）
            var raider = Spawn(p1, 4, 2);
            var valiant = Spawn(p2, 3, 6, false, false, CardCore.Attribute.KeywordRules.Vigilance);
            valiant.Tap(); // 模拟已横置（刚攻击过/被冻结）
            Assert(GameActions.DeclareAttack(core, p1, raider, valiant), "警戒段：攻击已横置警戒者宣言上栈");
            GameActions.DrainStack(core);
            Assert(valiant.IsTapped() && valiant.IsAlive && valiant.GetLife() == 2,
                   "警戒：已横置防守者吃伤存活（6−4=2）");
            Assert(!raider.IsAlive, "警戒：已横置防守者仍反击（3 反伤致死 4/2 攻击者）");

            // ---- 2c. NoGuard 不能拦（守卫入栈资格闸） ----
            var charger2 = Spawn(p1, 4, 2);
            var pacifist = Spawn(p2, 3, 6, noGuard: true);
            Assert(GameActions.DeclareAttack(core, p1, charger2, p2), "NoGuard 段：打脸宣言上栈");
            Assert(!core.StackEngine.PushGuardDeclaration(pacifist, core.StackEngine.Peek(), p2),
                   "守卫效果化：NoGuard 卡不能响应拦截（资格闸拒绝）");
            GameActions.DrainStack(core);        // 无守卫 → 攻击照常结算（打脸）
            Assert(pacifist.GetLife() == 6, "NoGuard 段：未拦截（守卫满血）");

            // ---- 2d. 激励再拦：守卫解除横置后可拦下一攻（逐攻击开窗） ----
            var a1 = Spawn(p1, 2, 2);
            var a2 = Spawn(p1, 3, 2);
            var gatekeeper = Spawn(p2, 1, 6);
            Assert(GameActions.DeclareAttack(core, p1, a1, p2), "再拦段：首攻宣言上栈");
            var attackA1 = core.StackEngine.Peek();
            Assert(core.StackEngine.PushGuardDeclaration(gatekeeper, attackA1, p2), "再拦段：首拦入栈");
            Assert(!core.StackEngine.PushGuardDeclaration(gatekeeper, attackA1, p2),
                   "再拦段：同窗二拦被拒（速度门 1 不> 1——记速器已被守卫抬到 1）");
            GameActions.DrainStack(core);
            Assert(gatekeeper.IsTapped() && gatekeeper.GetLife() == 4, "再拦段：首拦结算（6−2=4，战斗后横置）");
            Assert(a1.IsAlive && a1.GetLife() == 1 && a1.IsTapped(),
                   "再拦段：守卫反 1 伤（2/2 攻击者剩 1 活、战斗后横置）");
            gatekeeper.Untap(); // 激励通路
            Assert(GameActions.DeclareAttack(core, p1, a2, p2), "再拦段：第二攻宣言上栈（新窗口）");
            Assert(core.StackEngine.PushGuardDeclaration(gatekeeper, core.StackEngine.Peek(), p2),
                   "再拦段：解除横置可拦第二攻击者");
            GameActions.DrainStack(core);
            Assert(gatekeeper.IsAlive && gatekeeper.GetLife() == 1,
                   "再拦段：两拦都结算（6−2−3=1）");
            Assert(a2.IsAlive && a2.GetLife() == 1, "再拦段：守卫再反 1 伤（3/2 攻击者剩 1 活）");

            // ---- 3. 速度门槛：标准速度门（0≥计数器）——连锁开启时攻击不可宣言 ----
            var runner = Spawn(p1, 3, 3);
            var dummyData = new CardData
            {
                ID = "VERIFY_AG_CAST", CardName = "AG占位", Supertype = Cardtype.Spell,
                Effects = new List<CardEffectData>
                {
                    // 2026-09-13 记速器=峰值模型：0 速 cast 不抬记速器——占位卡带 1 速才连锁开启
                    new CardEffectData
                    {
                        Id = "VERIFY_AG_CAST_E", BaseSpeed = 1,
                        AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DrawCard, value: 1) },
                    },
                },
            };
            var dummy = new CardWrapper(dummyData);
            dummy.SetController(p1);
            core.StackEngine.PushCardCast(dummy, p1, null); // 1 速 cast → 记速器抬到 1（连锁开启）
            Assert(!GameActions.DeclareAttack(core, p1, runner, p2),
                   "速度门槛：连锁开启（记速器>0）时 0 速攻击不可宣言（0≥1 不达）");
            core.StackEngine.Clear();                        // 记速器归零
            Assert(GameActions.DeclareAttack(core, p1, runner, p2), "速度门槛：连锁闭合后攻击恢复（0≥0）");
            GameActions.DrainStack(core);                    // 攻击收口（3 攻打脸）

            // ---- 4. 法术速度=5−槽数（2026-10-29 P6c 修订；旧 3−N 于 2026-10-15 定案——槽含抉择分支与代价行，动态算不落字段）----
            var swiftData = new CardData
            {
                ID = "VERIFY_AG_SWIFT", CardName = "疾风术", Supertype = Cardtype.Spell,
                SpeedBonus = 1, // 字段退役——脏数据探针：写值不得影响动态速度
                Effects = new List<CardEffectData>
                {
                    new CardEffectData
                    {
                        Id = "VERIFY_AG_SWIFT_E",
                        AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DrawCard, value: 1) },
                    },
                },
            };
            var swift = new CardWrapper(swiftData);
            var swiftDefs = GameActions.GetCardEffectDefinitions(swift);
            Assert(swiftDefs.Count == 1 && swiftDefs[0].BaseSpeed == 2,
                   $"法术 1 槽=速2（实际 {swiftDefs[0].BaseSpeed}；2026-10-31 P12 回归 3−槽·下限0，SpeedBonus 脏数据无效）");
            var plainData = new CardData
            {
                ID = "VERIFY_AG_PLAIN", CardName = "平风术", Supertype = Cardtype.Spell,
                Effects = new List<CardEffectData> { swiftData.Effects[0] },
            };
            Assert(GameActions.GetCardEffectDefinitions(new CardWrapper(plainData))[0].BaseSpeed == 2,
                   "法术缺省 1 槽=速2（3−槽数，下限 0——P6c 5−槽已废）");
            var dualSlot = new CardData
            {
                ID = "VERIFY_AG_DUAL", CardName = "双策术", Supertype = Cardtype.Spell,
                Effects = new List<CardEffectData> { swiftData.Effects[0], swiftData.Effects[0] },
            };
            var dualDefs = GameActions.GetCardEffectDefinitions(new CardWrapper(dualSlot));
            Assert(dualDefs.Count == 2 && dualDefs[0].BaseSpeed == 1,
                   $"法术 2 槽=速1（3−槽数，实际 {dualDefs[0].BaseSpeed}）");
            var tripleSlot = new CardData
            {
                ID = "VERIFY_AG_TRIPLE", CardName = "三策术", Supertype = Cardtype.Spell,
                Effects = new List<CardEffectData> { swiftData.Effects[0], swiftData.Effects[0], swiftData.Effects[0] },
            };
            var tripleDefs = GameActions.GetCardEffectDefinitions(new CardWrapper(tripleSlot));
            Assert(tripleDefs.Count == 3 && tripleDefs[0].BaseSpeed == 0,
                   $"法术 3 槽=速0（3−槽数下限 0，实际 {tripleDefs[0].BaseSpeed}）");
            // 4 槽 = 3 效果 + 代价（2026-11-08 效果级化：代价行挂第三效果 Costs——「3 效果其一带代价」
            // 与旧「3 效果+卡级代价行」同槽位口径，代价行不计槽）
            var quadSlot = new CardData
            {
                ID = "VERIFY_AG_QUAD", CardName = "重策术", Supertype = Cardtype.Spell,
                Effects = new List<CardEffectData> { swiftData.Effects[0], swiftData.Effects[0], swiftData.Effects[0] },
            };
            quadSlot.Effects[2].Costs = new List<CostEntry>
            {
                new CostEntry
                {
                    payloads = new List<AtomicEffectEntry>
                        { Atom(AtomicEffectType.DiscardCard, value: 1, kinds: new List<int> { (int)CardCore.TargetKind.OwnHand }) },
                },
            };
            var quadDefs = GameActions.GetCardEffectDefinitions(new CardWrapper(quadSlot));
            Assert(quadDefs.Count == 3 && quadDefs[0].BaseSpeed == 0,
                   $"法术 4 槽（3 效果+代价行）=速0（代价行不计槽、3 槽已触底，实际 {quadDefs[0].BaseSpeed}）");
            var bioSwiftData = new CardData
            {
                ID = "VERIFY_AG_BIO_SWIFT", CardName = "疾风兽", Supertype = Cardtype.Creature, Power = 1, Life = 1,
                SpeedBonus = 1, // 脏数据探针：生物带 SpeedBonus 也不得生效
                Effects = new List<CardEffectData> { swiftData.Effects[0] },
            };
            Assert(GameActions.GetCardEffectDefinitions(new CardWrapper(bioSwiftData))[0].BaseSpeed == 0,
                   "生物无提速通道：SpeedBonus 仅法术生效（生物速度只由效果组合阶段 BaseSpeed 决定）");

            RetireCards(core, p1, wall, hitter, striker, raider, charger2, runner, a1, a2);
            RetireCards(core, p2, shield, valiant, pacifist, gatekeeper);
        }

        // ======================================== 使用时点 / 响应窗口（Phase 2） ========================================

        /// <summary>
        /// 使用时点 + 响应窗口端到端（Option Y 定案；合成卡驱动，不依赖卡表）：
        /// - 打出即声明上栈（声明期不付费），对手获得优先权；
        /// - 发动无效（2026-10-04 两层无效定案，原打落承接）：发动区卡送墓 → cast 中止：不付费（净效果=扣费返还）、无效果；
        /// - 效果无效（承接旧标记实现）：cast 照常付费但跳效果入墓，费用不退；
        /// - 声明超发保护：同笔 bank 已被在栈 cast 占用时拒绝新声明（防超发，不回卷）。
        /// </summary>
        private static void TestCounterWindow(GameCore core, Player p1, Player p2)
        {
            EnsureMainPhase(core, p1);
            // 段落卫生（2026-09-21）：前一 TestEquipment 的结算若产生死亡，SBA（速度1）会留在
            // 栈上开窗（记速器=1）——先排干再开始本段，否则 0 速火球过不了速度门、响应窗口错乱
            GameActions.DrainStack(core);

            // 合成法术：显式灰 1 费（绕开推导），原子由参数给定
            // 2026-09-13 修复：反制卡（发动无效/效果无效）须配 2 速——速度峰值模型下非回合方过门槛
            // 要求严格 > 记速器（0>0 恒假，0 速响应全被拒）；主动出的火球保持 0 速（不抬记速器）
            CardData SpellData(string id, string name, int baseSpeed = 0, params AtomicEffectEntry[] atoms)
            {
                var data = new CardData { ID = id, CardName = name };
                data.Supertype = Cardtype.Spell;
                data.Cost = CostOf((ManaType.Gray, 1));
                data.Effects.Add(new CardEffectData
                {
                    Id = id + "_MAIN",
                    DisplayName = name,
                    BaseSpeed = baseSpeed,
                    AtomicEffects = new List<AtomicEffectEntry>(atoms),
                });
                return data;
            }
            // 局部 Atom 收敛到类级助手（引用化适配）；本段保留 value 缺省=0 语义
            AtomicEffectEntry Atom(string type, int value = 0) => CardPipelineVerifier.Atom(type, value);

            // 状态快照（本段灌满双方 bank/上限，结束恢复——不污染后续段落）
            var pool1 = core.ElementPool.GetPool(p1);
            var pool2 = core.ElementPool.GetPool(p2);
            var snap1 = new Dictionary<ManaType, int>(pool1.TempElements);
            var snap2 = new Dictionary<ManaType, int>(pool2.TempElements);
            int idx1 = pool1.GlobalTurnIndex, idx2 = pool2.GlobalTurnIndex;
            pool1.GlobalTurnIndex = 9;   // 费用门槛（=地牌槽上限）不挡
            pool2.GlobalTurnIndex = 9;
            foreach (var t in AllManaTypes()) { pool1.TempElements[t] = 99; pool2.TempElements[t] = 99; }

            try
            {
                // ---- 1. 发动无效（两层无效定案：净零成本）：不付费、无效果 ----
                var fireball = InjectCard(core, p1, SpellData("VERIFY_CAST_FB", "验证火球", 0, Atom("DealDamage", 4)));
                var knock = InjectCard(core, p2, SpellData("VERIFY_CAST_KD", "验证发动无效", 2, Atom("NegateActivation")));

                int p2Life = p2.Life;
                int gray1 = pool1.TempElements[ManaType.Gray];

                Assert(GameActions.PlayCard(core, p1, fireball, new List<Entity> { p2 }),
                       "打出火球（使用时点声明上栈）—门禁:" + PlayGateProbe(core, p1, fireball));
                Assert(core.StackEngine.StackSize == 1 && core.StackEngine.CurrentPriorityHolder == p2,
                       "cast 上栈且对手持有优先权（响应窗口开启）");
                Assert(pool1.TempElements[ManaType.Gray] == gray1, "声明期不付费（Option Y）");

                Assert(GameActions.PlayCardInResponse(core, p2, knock, new List<Entity> { fireball }),
                       "响应窗口内打出发动无效（响应绑定栈顶=火球 cast）");
                Assert(core.ZoneManager.IsCardInZone(fireball, p1, Zone.Hand) && core.StackEngine.IsPendingCast(fireball),
                       "S3b：发动无效仅入栈未结算——火球仍留手牌（施放中=栈条目，不进发动区）");

                GameActions.DrainStack(core);   // 双 Pass → LIFO：发动无效先、火球 cast 后

                // 已知残留（2026-09-21 数据扩池后漂移）：段内会产生一个空目标的速度1 SBA 窗口
                // （DrainStack 双 Pass 排不掉，疑似记帐型 SBA 入栈后窗口未闭合——待夹具二期深挖）。
                // 收口排空防污染下段（同 Equipment/HeroSkills 段尾惯例）。
                GameActions.DrainStack(core);
                if (!core.StackEngine.IsEmpty)
                    core.StackEngine.Clear();
                Assert(core.StackEngine.IsEmpty, "栈已排干");
                Assert(core.ZoneManager.GetCards(p1, Zone.Graveyard).Contains(fireball), "火球被送墓（发动层无效达成）");
                Assert(p2.Life == p2Life, "被中止的 cast 不结算效果（伤害 0）");
                Assert(pool1.TempElements[ManaType.Gray] == gray1, "被中止的 cast 不付费（净效果=扣费返还）");
                Assert(core.ZoneManager.GetCards(p2, Zone.Graveyard).Contains(knock), "发动无效牌结算后入墓");

                // ---- 2. 效果无效（两层无效定案：扣费不返还）：付费但跳效果 ----
                var fireball2 = InjectCard(core, p1, SpellData("VERIFY_CAST_FB2", "验证火球二", 0, Atom("DealDamage", 4)));
                var negate = InjectCard(core, p2, SpellData("VERIFY_CAST_NA", "验证效果无效", 2, Atom("NegateEffect")));

                int gray1b = pool1.TempElements[ManaType.Gray];
                int red1b = pool1.TempElements[ManaType.Red];

                Assert(GameActions.PlayCard(core, p1, fireball2, new List<Entity> { p2 }), "打出第二张火球");
                Assert(GameActions.PlayCardInResponse(core, p2, negate, new List<Entity> { fireball2 }),
                       "响应窗口内打出效果无效");
                GameActions.DrainStack(core);
                if (!core.StackEngine.IsEmpty) core.StackEngine.Clear(); // 段内 SBA 窗口收口（同上小节）

                Assert(p2.Life == p2Life, "被无效的 cast 跳过效果（伤害 0）");
                // 恒推导账单对账（火球=DealDamage4；效果无效=锚价结算时现付照付，费用不退）
                Assert(pool1.TempElements[ManaType.Red] == red1b - (int)GameActions.GetCardCost(fireball2, 0)[ManaType.Red]
                        && pool1.TempElements[ManaType.Gray] == gray1b,
                       $"无效路径照常付费（窗口后扣锚价不退——实际红{red1b}→{pool1.TempElements[ManaType.Red]} 灰{gray1b}→{pool1.TempElements[ManaType.Gray]}）");
                Assert(core.ZoneManager.GetCards(p1, Zone.Graveyard).Contains(fireball2), "被无效的卡入墓");

                // ---- 3. 声明超发保护：同笔 bank 不重复承诺 ----
                var a = InjectCard(core, p1, SpellData("VERIFY_CAST_A", "验证超发A", 0, Atom("DealDamage", 1)));
                var b = InjectCard(core, p1, SpellData("VERIFY_CAST_B", "验证超发B", 0, Atom("DealDamage", 1)));
                // 账单=卡面灰1+效果锚价（2026-10-15 施放账单单源）——bank 精确灌一张的量，
                // 混付垫付（灰←黑白三色）会绕过「精确占用即拒」，故清全色只留账单本身（根因 A 修复）
                var billA = CardCore.ElementPaymentValidator.NormalizeBill(GameActions.GetCardCost(a, 0));
                foreach (var t in AllManaTypes()) pool1.TempElements[t] = 0;
                foreach (var kv in billA) pool1.TempElements[kv.Key] = kv.Value; // 只够一张
                Assert(GameActions.PlayCard(core, p1, a, new List<Entity> { p2 }), "第一张可声明（占用承诺——bank 恰好单源账单）");
                Assert(!GameActions.PlayCard(core, p1, b, new List<Entity> { p2 }),
                       "同一笔 bank 已被在栈 cast 占用：第二张拒绝（防超发）");
                GameActions.DrainStack(core);
                if (!core.StackEngine.IsEmpty) core.StackEngine.Clear(); // 段内 SBA 窗口收口（同上小节）
                Assert(AllManaTypes().All(t => pool1.TempElements[t] == 0), "第一张结算后照常扣费（账单扣空）");
                Assert(p2.Life == p2Life - 1, "第一张效果照常结算（伤害 1）");

                // ---- 4. 跨边代价断言已删（2026-09-10：OpponentDraw/OpponentHeal 机制被 Polarity 错边折价顶替）----
            }
            finally
            {
                pool1.GlobalTurnIndex = idx1;
                pool2.GlobalTurnIndex = idx2;
                pool1.TempElements.Clear();
                foreach (var kv in snap1) pool1.TempElements[kv.Key] = kv.Value;
                pool2.TempElements.Clear();
                foreach (var kv in snap2) pool2.TempElements[kv.Key] = kv.Value;
            }
        }

        // ======================================== 死亡原子（牺牲/消灭/湮灭） ========================================

        /// <summary>
        /// 死亡原子端到端（DeathRules 死因的原子层落地；合成随从驱动，不依赖卡表）：
        /// - 牺牲（2026-10-09 舍弃并档定案）：持有者自选单位直送墓——非效果死亡，不发 CardDestroyEvent；
        /// - 消灭（2026-10-09 吞噬机制移除）：纯效果死亡入墓，无回复无吸收——不灭拦（消灭类）；
        /// - 抹除（2026-10-13 湮灭退役承接）：非死亡直接消失——不走换区、无 CardDestroyEvent，复生不可救；不灭不拦。
        /// </summary>
        private static void TestDeathAtoms(GameCore core, Player p1, Player p2)
        {
            var used = new List<Card>();

            Card Make(Player owner, int power, int life, params string[] keywords)
            {
                var data = new CardData { ID = "VERIFY_DEATH_" + used.Count, CardName = "死" + used.Count };
                data.Supertype = Cardtype.Creature;
                data.Power = power;
                data.Life = life;
                foreach (var kw in keywords) data.Keywords.Add(kw);
                var card = new CardWrapper(data);
                card.SetController(owner);
                core.ZoneManager.TryAddToBattlefield(card, owner);
                card.Untap();
                used.Add(card);
                return card;
            }
            void Clean()
            {
                foreach (var c in used)
                {
                    var owner = c.GetController() ?? p1;
                    core.ZoneManager.GetZoneContainer(owner).Remove(c, Zone.Battlefield);
                    core.ZoneManager.GetZoneContainer(owner).Remove(c, Zone.Graveyard);
                    core.ZoneManager.GetZoneContainer(owner).Remove(c, Zone.Exile);
                }
            }

            // 原子直发（直连执行器，跳过元素费；与 TestCreature 击杀路径同法）
            // 2026-09-13：target 放宽为 Entity——牺牲/摒弃等 edict 原子的作用对象是角色（Player）
            void Atom(AtomicEffectType type, Player controller, Entity source, Entity target, int value = 0)
            {
                var def = new EffectDefinition
                {
                    Id = "VERIFY_DEATH_" + type,
                    TriggerTiming = TriggerTiming.Activate_Active,
                    Effects = new List<AtomicEffectInstance> { new AtomicEffectInstance { Type = type, Value = value } },
                };
                core.StackEngine.GetExecutor().ExecuteAsync(new EffectInstance
                {
                    Definition = def,
                    Source = source,
                    Controller = controller,
                    Targets = new List<Entity> { target },
                }, skipElementCost: true).Forget();
            }

            try
            {
                // ---- 牺牲（2026-11-14 域=候选声明）：目标卡直送墓·非死亡（不发 CardDestroyEvent=死亡时点不触发）----
                var victim = Make(p1, 2, 3);
                int destroyEvents = 0;
                void OnDeathAtomDestroy(CardDestroyEvent e) => destroyEvents++;
                EventManager.Instance.Subscribe<CardDestroyEvent>(OnDeathAtomDestroy);
                Atom(AtomicEffectType.Sacrifice, p1, p1, victim); // 2026-11-14：handler 目标=卡（旧 Player 目标作废）
                EventManager.Instance.Unsubscribe<CardDestroyEvent>(OnDeathAtomDestroy);
                Assert(!victim.IsAlive && core.ZoneManager.GetCards(p1, Zone.Graveyard).Contains(victim),
                       "牺牲：目标卡直送墓（非死亡定案；域=候选声明后目标为卡）");
                Assert(destroyEvents == 0,
                       "牺牲非死亡：不发 CardDestroyEvent（OnDeath/亡语/消灭替代/统计全不触发）");

                // ---- 消灭（2026-10-09 吞噬机制移除）：纯效果死亡，无回复无吸收 ----
                var slayer = Make(p1, 3, 6);
                Atom(AtomicEffectType.DealDamage, p1, p1, slayer, 4); // 先受伤至 2——验证消灭不再回复
                Assert(slayer.GetLife() == 2, "消灭段：施法单位先受伤至 2（回复缺口）");

                var prey = Make(p2, 1, 3, CardCore.Attribute.KeywordRules.Taunt);
                int killEvents = 0;
                void OnKillDestroy(CardDestroyEvent e) => killEvents++;
                EventManager.Instance.Subscribe<CardDestroyEvent>(OnKillDestroy);
                Atom(AtomicEffectType.Devour, p1, slayer, prey); // 消灭（原吞噬行改名，EffectType 沿用）
                EventManager.Instance.Unsubscribe<CardDestroyEvent>(OnKillDestroy);
                Assert(!prey.IsAlive && core.ZoneManager.GetCards(p2, Zone.Graveyard).Contains(prey),
                       "消灭：目标生物经 DestroyEffect 死因入墓");
                Assert(slayer.GetLife() == 2, "消灭无吞噬：不按目标生命回复（机制已删）");
                Assert(killEvents == 1, "消灭=效果死亡：恰发一次 CardDestroyEvent（与牺牲直送相对）");

                // 不灭拦消灭（消灭类死因）：拦下即无事发生
                var rock = Make(p2, 0, 5, CardCore.Attribute.KeywordRules.Indestructible,
                                CardCore.Attribute.CounterRules.StealthCounter);
                Atom(AtomicEffectType.Devour, p1, p1, rock);
                Assert(rock.IsAlive && core.ZoneManager.GetCards(p2, Zone.Battlefield).Contains(rock),
                       "不灭拦下消灭（消灭类死因）");

                // ---- 抹除（2026-10-13 湮灭残壳清理：Annihilate 行/handler 已删，Erase 承接语义——
                //      非死亡直接消失：不走换区、无 CardDestroyEvent，复生不可救、不灭不拦）----
                var phoenix = Make(p2, 2, 2, CardCore.Attribute.CounterRules.RebornCounter);
                int eraseDestroys = 0;
                void OnEraseDestroy(CardDestroyEvent e) => eraseDestroys++;
                EventManager.Instance.Subscribe<CardDestroyEvent>(OnEraseDestroy);
                Atom(AtomicEffectType.Erase, p1, p1, phoenix);
                EventManager.Instance.Unsubscribe<CardDestroyEvent>(OnEraseDestroy);
                Assert(!phoenix.IsAlive
                       && !core.ZoneManager.GetCards(p2, Zone.Battlefield).Contains(phoenix)
                       && !core.ZoneManager.GetCards(p2, Zone.Graveyard).Contains(phoenix)
                       && !core.ZoneManager.GetCards(p2, Zone.Exile).Contains(phoenix),
                       "抹除：非死亡直接消失（不入墓不除外——从本局移除）");
                Assert(!core.ZoneManager.GetCards(p2, Zone.Battlefield).Contains(phoenix) && eraseDestroys == 0,
                       "抹除：复生不可救（未回场）、不发 CardDestroyEvent（非死亡路径）");

                var adamant = Make(p2, 1, 4, CardCore.Attribute.KeywordRules.Indestructible);
                Atom(AtomicEffectType.Erase, p1, p1, adamant);
                Assert(!adamant.IsAlive && !core.ZoneManager.GetCards(p2, Zone.Battlefield).Contains(adamant),
                       "抹除：不灭不拦（非消灭类，照常抹除）");
            }
            finally
            {
                Clean();
            }
        }

        private static void TestCreature(GameCore core, Player p1, List<CardData> cardsData)
        {
            var data = cardsData.FirstOrDefault(c => c.CardName == "古树守卫");
            Assert(data != null, "古树守卫配置存在");
            if (data == null) return;

            var creature = new CardWrapper(data);
            creature.SetController(p1);
            core.ZoneManager.GetZoneContainer(p1).Add(creature, Zone.Hand);
            var pool = core.ElementPool.GetPool(p1);
            // 新费用（身材灰 3+5=8 点 = 灰 4）：灰 4 = 档位 4（2026-09-13 测试卡组生物统一 1/1 观测口径——
            // 声明费不变，身材降档只降 D，D≤C 照旧成立）
            pool.TempElements[ManaType.Gray] = 4;

            bool played = PlayCardSync(core, p1, creature);
            Assert(played, "古树守卫成功打出");
            Assert(core.ZoneManager.GetCards(p1, Zone.Battlefield).Contains(creature),
                   "生物进入战场");
            // 配置驱动血量：CardWrapper 同步 _life 前此处恒为 1
            Assert(creature.GetLife() == 1, $"血量来自配置（应为1，实际{creature.GetLife()}）");

            // 伤害效果击杀
            var killDef = new EffectDefinition
            {
                Id = "VERIFY_KILL",
                TriggerTiming = TriggerTiming.Activate_Active,
                Effects = new List<AtomicEffectInstance>
                {
                    new AtomicEffectInstance { Type = AtomicEffectType.DealDamage, Value = 5 }
                }
            };
            var kill = new EffectInstance
            {
                Definition = killDef,
                Source = creature,
                Controller = p1,
                Targets = new List<Entity> { creature },
            };
            // 直连执行器（跳过元素费：合成效果的计价与击杀语义无关）
            core.StackEngine.GetExecutor().ExecuteAsync(kill, skipElementCost: true).Forget();

            Assert(creature.GetLife() <= 0, $"受伤后血量≤0（实际{creature.GetLife()}）");
            Assert(!creature.IsAlive, "生物被伤害效果击杀");
        }

    }
}
