using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CardCore;
using CardCore.Attribute;
using CardCore.Network;

namespace TideServer.Verify
{
    /// <summary>
    /// V9 引擎规则回归（2026-10-02 三类卡型定案配套）：
    /// ①法术同折——延迟折 f=d(声明档位) 对法术不再豁免（旧「法术打出即生效 f≡1」已废），与生物同口径；
    /// ②域数据——伤害族（DealDamage/PierceDamage/DrainLife）开放无生命域 3,4（法术伤害可消耐久）；
    ///   冻结域收紧 1,2（冻结横置对结界无效——原 1,2,3,4 靠 NoRole 过滤副作用排除）；
    /// ③结界耐久战斗侧——入场初始化耐久（CardPutToBattlefieldEvent 驱动）、受击（战斗/法术同管线）
    ///   恒 -1 不落血、归零直送墓（Smashed 直毁，不经死亡决策表）、SBA 生命管线不扫无生命单位；
    /// ④攻击目标显式化——结界=合法攻击目标；非战场卡（FieldZone 英雄技能卡）不可被攻击。
    /// 2026-10-03 增：
    /// ⑤V9.g 展示（RevealCard，信息轴 2026-10-02 定案）端到端——挂层/二值/双视角快照投影/换区失效；
    /// ⑥V9.h 附加诅咒/附加祝福（2026-10-08 引擎主干化；原 AddCurse 信息轴 2026-10-02 定案）端到端——
    ///   施放部署（诅咒=对方牌库/祝福=己方牌库随机挂层+Then 登记）/抽到发作/一次性消耗/
    ///   Permanent 换区不清（与 Exposed 对比锚）/空 Then 空转。
    /// 2026-10-03 定案增补：
    /// ⑦V9.i 规则光环（ModifyGameRule 转正，路线 B）——全局唯一新换旧/双方生效/载体离场失效/
    ///   局重置回收 + 七条规则（旧仪式全量，疾风改双人连两回合）各自可观测接线。
    /// ⑧V9.j 地牌资格三型（生物/法术/结界；衍生物 IsToken/临时卡/耗竭除外）、V9.k 双方减半计价
    ///   （双侧+全取/多目标）+ 错边收缩（负面指己放行）+ RowHashId 变体行计价。
    /// ⑨V9.l/V9.m 端到端四则——衍生物弹回手×地牌拒绝、临时卡真路径（回响；微缩/放大已删 2026-10-10）×地牌拒绝、
    ///   灰不足苏醒立约、归土×回响×唯一槽×墓地配额压力剧本（配额每回合 1 次，光环换任不刷新）。
    /// 2026-10-04 代价栏规则改造增：
    /// ⑪V9.n 任意单向效果·镜像逆转·黑白封顶=地牌上限——PayloadCostDomain 三路（双侧收窄/正确侧镜像/已错侧免写）单元锚 + 镜像端到端（激励逆转到对方侧）+ 大额补偿钳制 + 持久化读回。
    /// 2026-10-04 两层无效定案增：
    /// ⑫V9.o 发动无效（净零成本：施放扣费返还/主动不扣费横置不重置/触发式不计发动次数/强制桶免疫）
    ///   × 效果无效（扣费照付：施放费用不退/主动扣费/触发式计入/强制桶受管制）——施放端到端+计数口径+混合批管制面。
    /// 2026-10-05 两槽定案重锚：
    /// ⑬构造形态从「header.EngineKind+AtomicEffects 奖励」改为「槽级原子 branch 载荷」（BranchEntryData/
    ///   BranchPayload）——V9.d 关键词型剔除改判平铺主干 def.Effects；V9.p 目录锚走 OutcomeConditions/
    ///   SituationGates（有限分支 2026-10-09 还原：达标奖励/不达标无事）；
    ///   Grant 引擎解体=无分支槽原子+效果级持续档计价；附两槽装载校验轻锚
    ///  （主序列主干 ≤2 / 域交集空 → 构筑期 Error）。
    /// 2026-10-08 附加诅咒/附加祝福引擎主干化：
    /// ⑭V9.h 重写为引擎路径（EngineCurseOnDraw/EngineBlessingOnDraw 施放结算部署→抽到发作→一次性消耗），
    ///   旧 AddCurse 指示物行退役；诅咒/倒计时/祝福指示物归系统（无玩家表行）。
    /// 2026-10-08 关键词不叠加定案增（2026-10-09 修订为彻底不叠加·全轨取代）：
    /// ⑮V9.t 台账取代制——同关键词**无论轨别**（文本轨=印刷/设置 vs 附加状态轨=临时），
    ///   新实例取代旧实例（值/次数刷新；跨轨不并存、互不补充：附加份不补文本份、文本份不补附加份）；
    ///   消耗型移除=整词撤除（无跨轨回补）；Limit -1=无限。
    /// Unity 侧对应段落见 Assets/Editor/验证/CardPipelineVerifier.cs（编辑器内跑，口径同源）。
    /// </summary>
    internal static class SectionRules
    {
        /// <summary>测试费用构造（2026-10-04 位置数组化）：PosCost((Gray,3),(Green,1)) == [3,0,0,1,0,0]。</summary>
        private static ElementCost PosCost(params (ManaType color, float amount)[] entries)
        {
            var c = new ElementCost();
            foreach (var (color, amount) in entries)
                c[color] = amount;
            return c;
        }

        /// <summary>原子表行 Tags 含指定标签（2026-10-08 层行为三型锚用）。</summary>
        private static bool HasTag(AtomicEffectType type, string tag)
            => AtomicEffectTable.GetByType(type)?.GetTagList().Contains(tag) == true;

        /// <summary>指示物原子按层数推导总价（2026-10-08 统一计价锚用）：行锚×max(1,|value|)，
        /// 单原子/单目标/无触发档 shim（RewardDerivedCost 同口径防档位膨胀）；负面原子锁对方域
        ///（锁己方=错边 0 费——kinds 由调用方按极性给）。2026-10-16 时机钉 OnTurnStart：
        /// 本助手只测原子轴（行锚×层数）——OnPlay 缺省会吃一发时机 ×0.8 污染原子轴读数。</summary>
        private static float DeriveAtomTotal(CardCore.Attribute.AtomicEffectConfig row, int value, int kind)
        {
            var def = new CardEffectData
            {
                Id = "V9_ATOM_TOTAL",
                TriggerTiming = (int)TriggerTiming.OnTurnStart,
                SelectionMode = -1,
                AtomicEffects = new List<AtomicEffectEntry>
                {
                    new AtomicEffectEntry { refId = row.HashId, value = value, kinds = new List<int> { kind } },
                },
            };
            var converted = CardEffectConverter.ConvertOne(def, "V9_ATOM_SRC");
            return CostDerivationService.DeriveElementCosts(converted).Total;
        }

        public static void Run()
        {
            AtomicEffectTable.Reload(); // 幂等装载（V0 已装载则重读同源）；折扣/原子引用都依赖表

            // ==== 临时探针已删（结论：池解析健康 102 卡/112 原子——Unity 塌陷系迁移中间态） ====

            // ============================ V9.a 法术同折（纯函数，不依赖对局） ============================

            VerifySuite.Section("V9.a 法术同折（f=d(C) 三类统一，旧豁免已废）");
            CardCostResult Probe(Cardtype type, int tier)
            {
                var d = new CardData { ID = $"V9_probe_{type}_{tier}", CardName = "V9折扣探针", Supertype = type };
                if (type == Cardtype.Creature) { d.Power = 2; d.Life = 2; }
                d.Cost = ElementCost.FromValue(ManaType.Gray, tier);
                d.Effects.Add(new CardEffectData
                {
                    Id = "V9_probe_onplay",
                    TriggerTiming = (int)TriggerTiming.OnPlay,
                    // value 6：6 damage probe anchor Red6 → D(1)=6 vs D(8)=5, the gap is stable and visible
                    // (2026-10-15 revision flipped: spells have no price offset channel, the difference is no longer erased by the offset)
                    AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DealDamage, value: 6) },
                });
                return CardCostService.Derive(d);
            }

            var spell1 = Probe(Cardtype.Spell, 1);
            var spell8 = Probe(Cardtype.Spell, 8);
            var creature8 = Probe(Cardtype.Creature, 8);
            VerifySuite.Assert(Math.Abs(spell1.Factor - 1f) < 1e-6f, $"法术低档 f=d(1)={spell1.Factor:0.###}=1（同内容同锚价）");
            VerifySuite.Assert(spell8.Factor < 0.99f && spell8.Factor > 0.7f,
                $"法术高档同折 f=d(8)={spell8.Factor:0.###}∈(0.7,1)——旧 f≡1 豁免已废");
            VerifySuite.Assert(Math.Abs(spell8.Factor - creature8.Factor) < 1e-6f,
                $"法术与生物同档同折（{spell8.Factor:0.###} vs {creature8.Factor:0.###}）");
            VerifySuite.Assert(spell8.DerivedTotal < spell1.DerivedTotal,
                $"高档推导价更低：D(8)={spell8.DerivedTotal} < D(1)={spell1.DerivedTotal}");

            // 法术 4伤+1抽（未声明档——与 Unity 计价锚同数字）：f=d(MaxTier=18)=0.75；
            // 锚价红4蓝2 ×0.75 取整总额5（最大余数法→红3蓝2）。2026-10-15 改版：法术免费点
            // 全投提速（槽位从速度扣）——无抵价通道 → 红3蓝2
            var fbCard = new CardData { ID = "V9_fb_card", CardName = "法术计价锚", Supertype = Cardtype.Spell };
            fbCard.Effects.Add(new CardEffectData
            {
                Id = "V9_fb_onplay",
                TriggerTiming = (int)TriggerTiming.OnPlay,
                AtomicEffects = new List<AtomicEffectEntry>
                {
                    AtomRefs.New(AtomicEffectType.DealDamage, value: 4),
                    AtomRefs.New(AtomicEffectType.DrawCard, value: 1),
                },
            });
            var fb = CardCostService.Derive(fbCard);
            VerifySuite.Assert(Math.Abs(fb.Factor - 0.75f) < 1e-4f,
                $"法术未声明档 f=d(18)=0.75（实际 {fb.Factor:0.###}）——旧 f≡1 已废");
            // 2026-10-16 一发时机入价 + 2026-10-18 效果合成阶段取整：E 逐色取整（红 round(4×0.8)=3、
            // 蓝 round(2×0.8)=2）→ ×0.75=红2.25蓝1.5 → L3 取整 4·最大余数法 → 红2蓝2
            VerifySuite.Assert((int)fb.DerivedCost[ManaType.Red] == 2
                              && (int)fb.DerivedCost[ManaType.Blue] == 2,
                $"法术 4伤+1抽 = 红2+蓝2（实际 红{(int)fb.DerivedCost[ManaType.Red]}蓝{(int)fb.DerivedCost[ManaType.Blue]}；"
                + "效果逐色取整后无抵价——2026-10-18 取整前移改写 10-16 的红3蓝1）");
            // SpeedBonus 字段退役（2026-10-15 改版：速度=3−槽数动态算）——脏数据写值不得影响
            fbCard.SpeedBonus = 2;
            var fbSpeed = CardCostService.Derive(fbCard);
            VerifySuite.Assert((int)fbSpeed.DerivedCost[ManaType.Red] == 2
                              && (int)fbSpeed.DerivedCost[ManaType.Blue] == 2,
                $"SpeedBonus 脏数据无效：仍红2蓝2（实际 红{(int)fbSpeed.DerivedCost[ManaType.Red]}蓝{(int)fbSpeed.DerivedCost[ManaType.Blue]}）");

            // ============================ V9.b 域数据（表级） ============================

            VerifySuite.Section("V9.b 域数据（伤害族开放 3,4；冻结=指示物行单位域）");
            void AssertKinds(AtomicEffectType type, int[] expect, string what)
            {
                var row = AtomicEffectTable.GetByType(type);
                VerifySuite.Assert(row != null, $"{type} 表行存在");
                if (row == null) return;
                var kinds = row.GetTargetKindList().OrderBy(x => x).ToArray();
                VerifySuite.Assert(kinds.SequenceEqual(expect.OrderBy(x => x)),
                    $"{what}：TargetKinds=[{string.Join(",", kinds)}] == [{string.Join(",", expect)}]");
            }
            AssertKinds(AtomicEffectType.DealDamage, new[] { 1, 2, 3, 4 }, "造成伤害（开放无生命域）");
            AssertKinds(AtomicEffectType.PierceDamage, new[] { 1, 2, 3, 4 }, "穿透伤害（开放无生命域）");
            AssertKinds(AtomicEffectType.DrainLife, new[] { 1, 2, 3, 4 }, "吸取（开放无生命域）");
            AssertKinds(AtomicEffectType.Freeze, new[] { 1, 2 },
                "冻结（2026-10-10 指示物目标域定案：指示物行不再默认自指——域=己/对方单位+NoRole 限生物；旧「存储态=自己」口径退役）");

            // ============================ V9.c 结界耐久全流程（对局内） ============================

            VerifySuite.Section("V9.c 结界耐久全流程");
            var core = GameCore.Instance;
            core.Reset();
            ZoneContainer.Reseed(20261002); // 固定种子：可复现
            var filler = Enumerable.Range(0, 8).Select(i => new CardData
            {
                ID = $"V9_fill_{i}", CardName = "填充" + i, Supertype = Cardtype.Creature, Power = 1, Life = 1,
            }).ToList();
            core.InitGame(CardLoader.BuildDeck(filler, 2), CardLoader.BuildDeck(filler, 2));
            var p1 = core.Player1;
            var p2 = core.Player2;
            GameActions.SkipElementPool(core, p1); // Standby → Main（p1 主阶段）

            Card ToHand(Player owner, CardData data)
            {
                var card = new CardWrapper(data);
                card.SetController(owner);
                core.ZoneManager.GetZoneContainer(owner).Add(card, Zone.Hand);
                return card;
            }

            // ---- ① 两步式入场 + 耐久初始化 + SBA 不误杀 ----
            var enchData = new CardData
            {
                ID = "V9_ench", CardName = "耐久结界", Supertype = Cardtype.Enchantment, Durability = 3,
            };
            var ench = ToHand(p1, enchData);
            // 耐久计价（2026-10-16 回调 0.5 灰/点）：3 耐久=1.5→取整 2 灰——备 2 灰并放开浓度上限后打出
            var v9cPool = core.ElementPool.GetPool(p1);
            v9cPool.TempElements[ManaType.Gray] = 2;
            v9cPool.GlobalTurnIndex = 9;
            VerifySuite.Assert(GameActions.PlayCard(core, p1, ench), "结界从手牌发动（耐久费 2 灰——2026-10-16 耐久计价 0.5 灰/点）");
            GameActions.DrainStack(core);
            VerifySuite.Assert((int)v9cPool.TempElements[ManaType.Gray] == 0,
                "耐久费恰好 2 灰付清（1.5 取整入档；余额归 0 钉价）");
            VerifySuite.Assert(ench.GetZone() == Zone.Battlefield, "两步式：发动区宣告→付费→进入战场");
            VerifySuite.Assert(ench.GetCounterCount(CounterRules.DurabilityCounter) == 6,
                "入场初始化耐久=6（印刷3+免费点3全投耐久——不装主动效果，2026-10-15 拆分定案；CardPutToBattlefieldEvent 驱动）");
            core.SBAEngine.ExecuteAll(); // 显式泵一轮 SBA
            VerifySuite.Assert(ench.IsAlive && ench.GetZone() == Zone.Battlefield,
                "SBA 生命管线不扫无生命单位（旧「结界被当 0 防御单位送墓」bug 回归锚）");
            VerifySuite.Assert(ench.IsNonLivingUnit(), "IsNonLivingUnit：战场结界=无生命单位");

            // ---- ② 战斗伤害：受击按伤害全额扣耐久（2026-10-16 撤钳 1）、不落血 ----
            int lifeBefore = ench._life;
            int dealt = KeywordRules.ApplyDamage(p2, ench, 5, isCombat: true);
            VerifySuite.Assert(dealt == 5, $"战斗伤害对结界全额返回（实际 {dealt}——撤钳后 DamageEvent/返回值=实际伤害额）");
            VerifySuite.Assert(ench.GetCounterCount(CounterRules.DurabilityCounter) == 1,
                "5 点战斗伤害全额扣 5 耐久（6→1）");
            VerifySuite.Assert(ench._life == lifeBefore, "伤害不落血（绕过生命管线）");
            VerifySuite.Assert(ench.IsAlive && ench.GetZone() == Zone.Battlefield, "结界仍在场");

            // ---- ③④ 法术/效果伤害同管线 + 耐久归零：直送墓 + Smashed（不经死亡决策表） ----
            DestroyReason? reason = null;
            EventManager.Instance.Subscribe<CardDestroyEvent>(
                e => { if (e.DestroyedCard == ench) reason = e.Reason; });
            KeywordRules.ApplyDamage(p1, ench, 3, isCombat: false); // 剩 1 耐久吃 3 伤：超扣按剩余钳 0 → 直毁
            VerifySuite.Assert(ench.GetCounterCount(CounterRules.DurabilityCounter) == 0,
                "法术伤害同管线全额扣耐久（1−3 超扣钳 0）");
            VerifySuite.Assert(ench.GetZone() == Zone.Graveyard, "耐久归零直送墓");
            VerifySuite.Assert(reason == DestroyReason.Smashed,
                $"死因=Smashed（无生命直毁；实际 {reason}）——不走 DeathRules/生命死亡路径");

            // ---- ⑤ 攻击目标显式化：结界合法；非战场卡不可被攻击 ----
            var attackerData = new CardData
            {
                ID = "V9_atk", CardName = "攻击者", Supertype = Cardtype.Creature, Power = 2, Life = 2,
            };
            var attacker = new CardWrapper(attackerData);
            attacker.SetController(p2);
            VerifySuite.Assert(core.ZoneManager.TryAddToBattlefield(attacker, p2), "攻击者入场");
            attacker.Untap();

            var ench2 = new CardWrapper(new CardData
            {
                ID = "V9_ench2", CardName = "耐久结界2", Supertype = Cardtype.Enchantment, Durability = 2,
            });
            ench2.SetController(p1);
            VerifySuite.Assert(core.ZoneManager.TryAddToBattlefield(ench2, p1), "第二张结界入场");
            VerifySuite.Assert(core.CombatSystem.CanAttackTarget(attacker, ench2, p2),
                "结界=合法攻击目标（防守方战场卡）");

            var fieldCards = core.ZoneManager.GetCards(p1, Zone.FieldZone);
            foreach (var fc in fieldCards)
            {
                fc.TryGetName(out var fcName);
                VerifySuite.Assert(!core.CombatSystem.CanAttackTarget(attacker, fc, p2),
                    $"非战场卡（{fcName}）不可被攻击");
            }

            // ---- ⑥ 生物生命管线回归锚：改动不影响活体单位死亡 ----
            var victimData = new CardData
            {
                ID = "V9_victim", CardName = "受害者", Supertype = Cardtype.Creature, Power = 1, Life = 1,
            };
            var victim = new CardWrapper(victimData);
            victim.SetController(p1);
            VerifySuite.Assert(core.ZoneManager.TryAddToBattlefield(victim, p1), "受害者入场");
            KeywordRules.ApplyDamage(attacker, victim, 1, isCombat: true);
            VerifySuite.Assert(!victim.IsAlive, "生物 1 伤致死：标死（SBA 送墓路径不变）");
            core.SBAEngine.ExecuteAll();
            VerifySuite.Assert(victim.GetZone() == Zone.Graveyard, "生物死亡经 SBA 送墓（回归锚）");

            // ============================ V9.d 主动效果计价口径（2026-10-02 定案） ============================

            VerifySuite.Section("V9.d 主动效果：费用移发动时、槽位维持；关键词不得作主动");
            // ① 主动锚价构筑期全免（E 桶回归锚）：卡带一个主动 DealDamage 效果 → EAnchor=0
            var activatedCard = new CardData { ID = "V9_act", CardName = "主动探针", Supertype = Cardtype.Creature, Power = 1, Life = 1 };
            activatedCard.Effects.Add(new CardEffectData
            {
                Id = "V9_act_onplay",
                TriggerTiming = (int)TriggerTiming.Activate_Active,
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DealDamage, value: 2) },
            });
            var actDerive = CardCostService.Derive(activatedCard);
            VerifySuite.Assert(actDerive.EAnchor == 0,
                $"主动不进 E 桶（EAnchor={actDerive.EAnchor}——锚价运行时现付，2026-09-08 定案回归锚）");

            // ② 主动仍占效果槽（2026-10-02 用户定案维持：AI 每卡最大原子数限制的可见性锚）
            var mixedCard = new CardData { ID = "V9_mix", CardName = "混合探针", Supertype = Cardtype.Creature, Power = 1, Life = 1 };
            mixedCard.Effects.Add(new CardEffectData
            {
                Id = "V9_mix_act",
                TriggerTiming = (int)TriggerTiming.Activate_Active,
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DealDamage, value: 2) },
            });
            mixedCard.Effects.Add(new CardEffectData
            {
                Id = "V9_mix_onplay",
                TriggerTiming = (int)TriggerTiming.OnPlay,
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DrawCard, value: 1) },
            });
            VerifySuite.Assert(CostDerivationService.CountEffectSlots(mixedCard) == 2,
                "主动照常计效果槽（1 主动 + 1 登场 = 2 槽——占用维持定案回归锚）");

            // ③ 关键词型 Grant（自指域 {0}）做主动 → converter 剔除（2026-10-02 定案）
            //（GrantIceCrystal 行已于 2026-10-02 退役；GrantPoisonSting/GrantPathogen 行已随改写光环化删除
            //  2026-10-05；GrantStealth/GrantReborn 等指示物行 2026-10-10 指示物目标域定案改单位域=
            //  转赋予型 Grant——正例改用存续自指的 GrantRegeneration/GrantVenom）
            VerifySuite.Assert(ComposerCatalog.IsKeywordStyleGrant(AtomicEffectType.GrantRegeneration)
                              && ComposerCatalog.IsKeywordStyleGrant(AtomicEffectType.GrantVenom),
                "关键词型判定：自指域 Grant 行（再生/剧毒）= 关键词型");
            VerifySuite.Assert(!ComposerCatalog.IsKeywordStyleGrant(AtomicEffectType.GrantDepravity)
                              && !ComposerCatalog.IsKeywordStyleGrant(AtomicEffectType.DealDamage),
                "关键词型判定：赋予型（反疗，域含他人）与非 Grant 行不受限");

            var kwAct = new CardEffectData
            {
                Id = "V9_kw_act",
                TriggerTiming = (int)TriggerTiming.Activate_Instant,
                AtomicEffects = new List<AtomicEffectEntry>
                {
                    AtomRefs.New(AtomicEffectType.GrantRegeneration),              // 关键词型 → 应剔除
                    AtomRefs.New(AtomicEffectType.DealDamage, value: 1),
                },
                Steps = new List<EffectStepData>
                {
                    // 门步前置存活主干（2026-10-05 两槽定案：无 Choice 的步骤平铺折叠，门折入前一个
                    // 原子的 Branch 载荷——留一个非关键词主干供下方观测「Then 空→门步整体丢弃」）
                    new EffectStepData { kind = 0, atomic = AtomRefs.New(AtomicEffectType.DealDamage, value: 1) },
                    new EffectStepData { kind = 0, atomic = AtomRefs.New(AtomicEffectType.GrantVenom) }, // 步骤内 → 整步剔除
                    new EffectStepData
                    {
                        kind = 1, conditionId = "DmgKillsTarget",
                        thenSteps = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.GrantLifesteal) }, // 分支奖励 → 剔除
                    },
                },
            };
            var kwDef = CardEffectConverter.ConvertOne(kwAct, "V9_src");
            // 剔除断言改判平铺主干 def.Effects（两槽定案：关键词型剔除作用于 AtomicEffects+Steps 全树，
            // 无 Choice 的步骤随后平铺折入主序列——kwDef.Steps 恒空）
            var kwFlat = kwDef.Effects.Select(a => a.Type).ToList();
            VerifySuite.Assert(!kwFlat.Contains(AtomicEffectType.GrantRegeneration),
                "主动平铺主干：关键词型 Grant（再生）被剔除");
            VerifySuite.Assert(kwFlat.Contains(AtomicEffectType.DealDamage), "主动平铺主干：非关键词原子保留");
            VerifySuite.Assert(!kwFlat.Contains(AtomicEffectType.GrantVenom),
                "主动平铺主干：关键词型原子步骤被整步剔除（GrantVenom 步折平后不出现）");
            var kwThenTypes = kwDef.Effects.Where(a => a.Branch != null)
                .SelectMany(a => a.Branch.Then).Select(a => a.Type).ToList();
            VerifySuite.Assert(!kwThenTypes.Contains(AtomicEffectType.GrantLifesteal)
                              && kwDef.Effects.All(a => a.Branch == null),
                "主动分支奖励：关键词型（吸血授予）被剔除——Then 空→门步整体丢弃（不产 Branch 载荷）");

            // ④ 赋予型 Grant 做主动 → 放行（赋予指示物可以；正例=反疗，微缩/放大已删 2026-10-10）
            var grantAct = new CardEffectData
            {
                Id = "V9_grant_act",
                TriggerTiming = (int)TriggerTiming.Activate_Active,
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.GrantDepravity, value: 1) },
            };
            var grantDef = CardEffectConverter.ConvertOne(grantAct, "V9_src");
            VerifySuite.Assert(grantDef.Effects.Any(a => a.Type == AtomicEffectType.GrantDepravity),
                "赋予型 Grant（反疗，域含他人）做主动放行");

            // ============================ V9.e 主动⇔Activate_* 钉死（2026-10-02 定案；2026-10-16 称谓统一） ============================

            VerifySuite.Section("V9.e 发动方式钉死：主动=玩家轮询发动（Activate_*） / 自动=可选触发 / 强制无歧义");
            var voluntaryBad = CardEffectConverter.ConvertOne(new CardEffectData
            {
                Id = "V9_vol_bad",
                ActivationType = (int)EffectActivationType.Voluntary,          // 主动
                TriggerTiming = (int)TriggerTiming.OnPlay,                     // 带触发时机 = 语义矛盾
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DealDamage, value: 1) },
            }, "V9_src");
            VerifySuite.Assert(voluntaryBad.TriggerTiming == TriggerTiming.Activate_Active,
                $"主动+触发时机 → 时机钉 Activate_Active（实际 {voluntaryBad.TriggerTiming}）");
            VerifySuite.Assert(voluntaryBad.ElementCostPrepaid == false,
                "钉死后按主动计：构筑期不计锚价（ElementCostPrepaid=false）");

            var voluntaryInstant = CardEffectConverter.ConvertOne(new CardEffectData
            {
                Id = "V9_vol_inst",
                ActivationType = (int)EffectActivationType.Voluntary,
                TriggerTiming = (int)TriggerTiming.Activate_Instant,           // 已是主动 → 保留具体档
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DealDamage, value: 1) },
            }, "V9_src");
            VerifySuite.Assert(voluntaryInstant.TriggerTiming == TriggerTiming.Activate_Instant
                              && voluntaryInstant.ActivationType == EffectActivationType.Voluntary,
                "主动+Activate_Instant：合法组合原样保留（主动具体档不动）");

            var actMandatory = CardEffectConverter.ConvertOne(new CardEffectData
            {
                Id = "V9_act_mand",
                ActivationType = (int)EffectActivationType.Mandatory,           // 主动声明强制 = 反向矛盾
                TriggerTiming = (int)TriggerTiming.Activate_Active,
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DealDamage, value: 1) },
            }, "V9_src");
            VerifySuite.Assert(actMandatory.ActivationType == EffectActivationType.Voluntary,
                "主动+强制 → 覆写回主动（既有对称校验回归锚）");

            // ============================ V9.f 自我沉睡（2026-10-03 苏醒改造：手牌立约制） ============================

            VerifySuite.Section("V9.f 自我沉睡：手牌苏醒立约 + 灰费转时长 + 定长照付");
            // Sleep 单行极性 -1——自我沉睡（收窄域 {Self}）经 2026-10-03 错边收缩在效果栏放行
            //（负面指向自己合法）；灰费豁免须先 CommitAwaken（手牌立约，2026-10-03 苏醒改造）
            var ssData = new CardData
            {
                ID = "V9F_SLEEP", CardName = "V9F沉睡者", Supertype = Cardtype.Creature, Power = 4, Life = 4,
                Cost = PosCost((ManaType.Gray, 3), (ManaType.Green, 1)),
            };
            ssData.Effects.Add(new CardEffectData
            {
                Id = "V9F_SLEEP_EFF",
                TriggerTiming = (int)TriggerTiming.OnPlay,
                AtomicEffects = new List<AtomicEffectEntry>
                {
                    AtomRefs.New(AtomicEffectType.Sleep, value: 0, kinds: new List<int> { 0 }), // 灰时长模式
                },
            });
            var ssDef = CardEffectConverter.ConvertOne(ssData.Effects[0], "V9F");
            VerifySuite.Assert(ssDef.Effects.Any(a => a.Type == AtomicEffectType.Sleep),
                "错边收缩：自我沉睡原子（Sleep p=-1 锁 {Self}）在效果栏放行（负面指向自己合法）");

            var fs = GameCore.Instance;
            fs.Reset();
            ZoneContainer.Reseed(20261003);
            var fillerF = Enumerable.Range(0, 8).Select(i => new CardData
            {
                ID = $"V9F_fill_{i}", CardName = "V9F填充" + i, Supertype = Cardtype.Creature, Power = 1, Life = 1,
            }).ToList();
            fs.InitGame(CardLoader.BuildDeck(fillerF, 2), CardLoader.BuildDeck(fillerF, 2));
            var f1 = fs.Player1;
            GameActions.SkipElementPool(fs, f1);
            var bankF = fs.ElementPool.GetPool(f1);
            var bankSnap = new Dictionary<ManaType, int>(bankF.TempElements);
            try
            {
                foreach (ManaType t in Enum.GetValues(typeof(ManaType))) bankF.TempElements[t] = 99;

                Card ToHandF(Player owner, CardData data)
                {
                    var card = new CardWrapper(data);
                    card.SetController(owner);
                    fs.ZoneManager.GetZoneContainer(owner).Add(card, Zone.Hand);
                    return card;
                }

                // ---- ① 未立约（对照）：灰照扣全价 ----
                var ssDataU = new CardData
                {
                    ID = "V9F_SLEEP_UNCOMMITTED", CardName = "V9F未立约沉睡者", Supertype = Cardtype.Creature, Power = 4, Life = 4,
                    Cost = PosCost((ManaType.Gray, 3), (ManaType.Green, 1)),
                };
                ssDataU.Effects.Add(new CardEffectData
                {
                    Id = "V9F_SLEEP_UNCOMMITTED_EFF",
                    TriggerTiming = (int)TriggerTiming.OnPlay,
                    AtomicEffects = new List<AtomicEffectEntry>
                    {
                        AtomRefs.New(AtomicEffectType.Sleep, value: 0, kinds: new List<int> { 0 }),
                    },
                });
                var uncommittedCard = ToHandF(f1, ssDataU);

                // ---- ② 手牌立约：灰全免 + 转沉睡时长 + 横置（回合1 即可出——立约修复的意义） ----
                var sleepCard = ToHandF(f1, ssData);
                VerifySuite.Assert(GameActions.CommitAwaken(fs, f1, sleepCard, out var rejectAwaken),
                    $"手牌苏醒立约（拒绝原因：{rejectAwaken ?? "无"}）");
                VerifySuite.Assert(!GameActions.CommitAwaken(fs, f1, sleepCard, out var rejectAwaken2)
                                  && rejectAwaken2 != null,
                    $"一卡一次：重复立约拒绝（{rejectAwaken2}）");
                var plainFiller = ToHandF(f1, fillerF[0]);
                VerifySuite.Assert(!GameActions.CommitAwaken(fs, f1, plainFiller, out var rejectAwaken3)
                                  && rejectAwaken3 != null,
                    $"非自我沉睡卡立约拒绝（{rejectAwaken3}）");
                int grayBefore = bankF.TempElements[ManaType.Gray];
                VerifySuite.Assert(GameActions.PlayCard(fs, f1, sleepCard), "立约沉睡卡打出（灰豁免后仅需绿1）");
                GameActions.DrainStack(fs);
                VerifySuite.Assert(bankF.TempElements[ManaType.Gray] == grayBefore, "灰费豁免：3 灰未扣（转沉睡时长）");
                VerifySuite.Assert(sleepCard.GetCounterCount(KeywordRules.SleepCounter) == 3,
                    $"入场沉睡 3 层（=豁免灰量；实际 {sleepCard.GetCounterCount(KeywordRules.SleepCounter)}）");
                VerifySuite.Assert(sleepCard.IsTapped(), "横置进沉睡");

                // ---- ① 未立约（对照）：总费 4 受浓度上限压制——推进三轮（全局同步上限升到 7）后照扣全价 ----
                // （立约卡②回合 1 即可出 vs 未立约①须等上限成长——苏醒死循环的修复对照；
                //   2026-10-11 现取现用：回合末清空临时钱包——循环后重播种）
                var f1u = fs.Player2;
                for (int turnCycle = 0; turnCycle < 3; turnCycle++)
                {
                    GameActions.EndTurn(fs, f1);
                    fs.TurnEngine.CheckPhaseTransition();
                    GameActions.SkipElementPool(fs, f1u);
                    GameActions.EndTurn(fs, f1u);
                    fs.TurnEngine.CheckPhaseTransition();
                    GameActions.SkipElementPool(fs, f1);
                }
                foreach (ManaType t in Enum.GetValues(typeof(ManaType))) bankF.TempElements[t] = 99; // 回合末清空后重播种
                int grayUncommittedBefore = bankF.TempElements[ManaType.Gray];
                VerifySuite.Assert(GameActions.PlayCard(fs, f1, uncommittedCard, null, Zone.Hand, 0, out var rejectF1),
                    $"未立约沉睡卡照常打出（付全价；拒绝原因：{rejectF1 ?? "无"}）");
                GameActions.DrainStack(fs);
                VerifySuite.Assert(bankF.TempElements[ManaType.Gray] == grayUncommittedBefore - 3,
                    $"未立约对照：灰照扣 3（{grayUncommittedBefore}→{bankF.TempElements[ManaType.Gray]}——豁免须先手牌立约）");

                // ---- ③ 定长沉睡照付（不变：显式层数不走灰豁免，立约与否同价） ----

                // 定长卡总费 3 > 首回合地牌槽上限 1——推进两回合（全局同步上限升到 9）再打
                var f2 = fs.Player2;
                GameActions.EndTurn(fs, f1);
                fs.TurnEngine.CheckPhaseTransition();
                GameActions.SkipElementPool(fs, f2);
                GameActions.EndTurn(fs, f2);
                fs.TurnEngine.CheckPhaseTransition();
                GameActions.SkipElementPool(fs, f1);
                foreach (ManaType t in Enum.GetValues(typeof(ManaType))) bankF.TempElements[t] = 99; // 回合末清空后重播种

                var fixedData = new CardData
                {
                    ID = "V9F_FIXED", CardName = "V9F定长沉睡", Supertype = Cardtype.Creature, Power = 3, Life = 3,
                    Cost = PosCost((ManaType.Gray, 2), (ManaType.Green, 1)),
                };
                fixedData.Effects.Add(new CardEffectData
                {
                    Id = "V9F_FIXED_EFF",
                    TriggerTiming = (int)TriggerTiming.OnPlay,
                    AtomicEffects = new List<AtomicEffectEntry>
                    {
                        AtomRefs.New(AtomicEffectType.Sleep, value: 2, kinds: new List<int> { 0 }), // 定长 2
                    },
                });
                var fixedCard = ToHandF(f1, fixedData);
                int grayBefore2 = bankF.TempElements[ManaType.Gray];
                VerifySuite.Assert(GameActions.PlayCard(fs, f1, fixedCard), "定长沉睡卡打出");
                GameActions.DrainStack(fs);
                VerifySuite.Assert(bankF.TempElements[ManaType.Gray] == grayBefore2 - 2,
                    "定长沉睡不走灰豁免：照扣 2 灰");
                VerifySuite.Assert(fixedCard.GetCounterCount(KeywordRules.SleepCounter) == 2,
                    $"定长沉睡 2 层（实际 {fixedCard.GetCounterCount(KeywordRules.SleepCounter)}）");
            }
            finally
            {
                foreach (var kv in bankSnap) bankF.TempElements[kv.Key] = kv.Value;
            }

            // ============================ V9.g 展示（RevealCard，信息轴 2026-10-02 定案） ============================

            VerifySuite.Section("V9.g 展示（RevealCard）：挂层/二值/双视角快照/换区失效");
            var gcore = GameCore.Instance;
            gcore.Reset();
            ZoneContainer.Reseed(20261004);
            var fillerG = Enumerable.Range(0, 8).Select(i => new CardData
            {
                ID = $"V9G_fill_{i}", CardName = "V9G填充" + i, Supertype = Cardtype.Creature, Power = 1, Life = 1,
            }).ToList();
            gcore.InitGame(CardLoader.BuildDeck(fillerG, 2), CardLoader.BuildDeck(fillerG, 2));
            var g1 = gcore.Player1;
            var g2 = gcore.Player2;
            GameActions.SkipElementPool(gcore, g1);
            // 夹具卡未声明 Cost——出牌链按效果推导费（蓝等色），灌满银行保证可付（夹具局即弃，不还原）
            foreach (ManaType t in Enum.GetValues(typeof(ManaType)))
                gcore.ElementPool.GetPool(g1).TempElements[t] = 99;
            // 2026-10-15 改版：法术施放账单=卡面费+所选效果锚价（GetCardCost 单源）——
            // 推进地牌曲线上限放行锚价（展示 蓝1.5 须上限 ≥2）
            gcore.ElementPool.GetPool(g1).GlobalTurnIndex = 9;

            Card GToHand(Player owner, CardData data)
            {
                var card = new CardWrapper(data);
                card.SetController(owner);
                gcore.ZoneManager.GetZoneContainer(owner).Add(card, Zone.Hand);
                return card;
            }

            // 夹具：无费用法术，OnPlay 展示原子收窄域 {8}=对方牌库 → None 自结算=牌库顶一张
            //(index 0=顶，容器约定——确定性，不耗随机)；隐藏区不给施放者选择权（SelectionMode=None）
            Card MakeRevealSpell(string id)
            {
                var data = new CardData { ID = id, CardName = "V9G展示术", Supertype = Cardtype.Spell };
                data.Effects.Add(new CardEffectData
                {
                    Id = id + "_EFF",
                    TriggerTiming = (int)TriggerTiming.OnPlay,
                    SelectionMode = -1,
                    AtomicEffects = new List<AtomicEffectEntry>
                    {
                        AtomRefs.New(AtomicEffectType.RevealCard, value: 1, kinds: new List<int> { 8 }),
                    },
                });
                return GToHand(g1, data);
            }

            // ---- ① 挂层：对方牌库顶 Exposed=1 ----
            var deckTop = gcore.ZoneManager.GetCards(g2, Zone.Deck)[0];
            var revealCard1 = MakeRevealSpell("V9G_REVEAL_1");
            VerifySuite.Assert(GameActions.PlayCard(gcore, g1, revealCard1, null, Zone.Hand, 0, out var rejectG1),
                $"展示术打出（None 自结算；拒绝原因：{rejectG1 ?? "无"}）");
            GameActions.DrainStack(gcore);
            VerifySuite.Assert(deckTop.GetCounterCount(CounterRules.ExposedCounter) == 1
                              && RevealRules.IsExposed(deckTop),
                $"对方牌库顶挂 Exposed=1 且 IsExposed 命中（实际层数 {deckTop.GetCounterCount(CounterRules.ExposedCounter)}）");

            // ---- ② 二值：重复展示不叠层 ----
            var revealCard2 = MakeRevealSpell("V9G_REVEAL_2");
            VerifySuite.Assert(GameActions.PlayCard(gcore, g1, revealCard2, null, Zone.Hand, 0, out var rejectG2),
                $"第二张展示术打出（拒绝原因：{rejectG2 ?? "无"}）");
            GameActions.DrainStack(gcore);
            VerifySuite.Assert(deckTop.GetCounterCount(CounterRules.ExposedCounter) == 1,
                $"重复展示不叠层（二值；实际 {deckTop.GetCounterCount(CounterRules.ExposedCounter)}）");

            // ---- ③ 双视角快照：RevealedZoneCards 恒全量下发；牌库仍不进 ZoneCards（隐藏口径不破） ----
            var seat2 = NetEntityMapper.SeatOf(g2);
            bool RevealedContains(MsgGameStateSync snap) => snap.RevealedZoneCards != null
                && snap.RevealedZoneCards
                    .Where(z => z.Seat == seat2 && z.Zone == (int)Zone.Deck)
                    .SelectMany(z => z.Cards)
                    .Any(c => c.RuntimeId == deckTop.RuntimeId);
            var snapSeat0 = NetSnapshotBuilder.Build(gcore, 0);
            var snapSeat1 = NetSnapshotBuilder.Build(gcore, 1);
            VerifySuite.Assert(RevealedContains(snapSeat0) && RevealedContains(snapSeat1),
                "双视角快照 RevealedZoneCards 均含被展示卡（恒全量——信息轴公开通道）");
            VerifySuite.Assert(snapSeat0.ZoneCards.All(z => (Zone)z.Zone != Zone.Deck),
                "牌库仍不进 ZoneCards（隐藏区只数量——展示走独立通道）");

            // ---- ④ Exposed 取件口（TargetFilter token \"Exposed\" 同源 CounterFilter） ----
            var exposedHit = new CounterFilter(CounterRules.ExposedCounter)
                .Filter(new List<Entity> { deckTop }, null);
            VerifySuite.Assert(exposedHit.Count == 1, "TargetFilter \"Exposed\" 取件口命中被展示卡");

            // ---- ⑤ 换区失效：抽走 → 清层 → 快照不再含 ----
            var drawnExposed = gcore.ZoneManager.DrawCard(g2);
            VerifySuite.Assert(drawnExposed == deckTop && deckTop.GetZone() == Zone.Hand, "被展示卡抽到手上");
            VerifySuite.Assert(deckTop.GetCounterCount(CounterRules.ExposedCounter) == 0
                              && !RevealRules.IsExposed(deckTop),
                "换区即失效（UntilLeaveBattlefield：Exposed 清零）");
            VerifySuite.Assert(!RevealedContains(NetSnapshotBuilder.Build(gcore, 0)),
                "失效后快照 RevealedZoneCards 不再含该卡");

            // ---- ⑥ 发现挂展示（2026-10-09 展示链路补全定案）：被展示牌即挂 Exposed（双方可查看）；
            // 未选中留库=不换区保持展示；选中入手=换区即消（ClearAll 换区口径统一承担）。
            // 直执 handler 夹具（PlayCard 受浓度上限拦高费——展示链路与出牌链无关） ----
            var discInst = CardEffectConverter.ConvertAtomForUI(AtomRefs.New(AtomicEffectType.DiscoverCard, value: 2));
            var discSrc = new CardWrapper(new CardData
            { ID = "V9G_DISCOVER_SRC", CardName = "V9G发现源", Supertype = Cardtype.Spell });
            discSrc.SetController(g1);
            int g1HandPre = gcore.ZoneManager.GetCards(g1, Zone.Hand).Count;
            int g1DeckExposedPre = gcore.ZoneManager.GetCards(g1, Zone.Deck)
                .Count(c => c.GetCounterCount(CounterRules.ExposedCounter) > 0);
            CardCore.Attribute.EffectHandlerRegistry.ExecuteEffectAsync(discInst, new EffectExecutionContext
            {
                Controller = g1,
                Source = discSrc,
                ZoneManager = gcore.ZoneManager,
                ElementPool = gcore.ElementPool,
            }).GetAwaiter().GetResult();
            var g1HandNow = gcore.ZoneManager.GetCards(g1, Zone.Hand);
            int g1DeckExposedNow = gcore.ZoneManager.GetCards(g1, Zone.Deck)
                .Count(c => c.GetCounterCount(CounterRules.ExposedCounter) > 0);
            VerifySuite.Assert(g1HandNow.Count == g1HandPre + 1,
                $"发现选入 1（直执无打出消耗：{g1HandPre}→{g1HandNow.Count}）");
            VerifySuite.Assert(g1DeckExposedNow == g1DeckExposedPre + 1,
                $"未选中留库保持展示（库内 Exposed {g1DeckExposedPre}→{g1DeckExposedNow}——展示即状态）");
            VerifySuite.Assert(g1HandNow.All(c => c.GetCounterCount(CounterRules.ExposedCounter) == 0),
                "选中入手换区即消（含选入卡——到手牌的展示态被换区清擦除）");

            // ============================ V9.h 附加诅咒/附加祝福（2026-10-08 引擎主干化） ============================

            VerifySuite.Section("V9.h 附加诅咒/附加祝福（引擎主干）：施放部署/抽到发作/一次性消耗/Permanent");
            var hcore = GameCore.Instance;
            hcore.Reset();
            ZoneContainer.Reseed(20261005);
            var fillerH = Enumerable.Range(0, 8).Select(i => new CardData
            {
                ID = $"V9H_fill_{i}", CardName = "V9H填充" + i, Supertype = Cardtype.Creature, Power = 1, Life = 1,
            }).ToList();
            hcore.InitGame(CardLoader.BuildDeck(fillerH, 2), CardLoader.BuildDeck(fillerH, 2));
            var h1 = hcore.Player1;
            var h2 = hcore.Player2;
            GameActions.SkipElementPool(hcore, h1);
            // 夹具卡未声明 Cost——出牌链按效果推导费，灌满银行保证可付（夹具局即弃，不还原）
            foreach (ManaType t in Enum.GetValues(typeof(ManaType)))
                hcore.ElementPool.GetPool(h1).TempElements[t] = 99;

            Card HToHand(Player owner, CardData data)
            {
                var card = new CardWrapper(data);
                card.SetController(owner);
                hcore.ZoneManager.GetZoneContainer(owner).Add(card, Zone.Hand);
                return card;
            }

            // 引擎主干卡（2026-10-08）：trunk=EngineCurseOnDraw/EngineBlessingOnDraw 表行，Then=抽卡原子——
            // 发作时施加方抽 1 可观测（DrawCard 走 Controller 抽牌，弹选目标不影响——无头确定性；
            // 自包含不依赖 Effects.json，清空态不再软跳过）
            Card MakeOnDrawEngine(string id, AtomicEffectType engineType, BranchEngineKind kind, List<AtomicEffectEntry> then)
            {
                var data = new CardData { ID = id, CardName = "V9H引擎卡", Supertype = Cardtype.Spell };
                data.Effects.Add(new CardEffectData
                {
                    Id = id + "_EFF",
                    TriggerTiming = (int)TriggerTiming.OnPlay,
                    SelectionMode = -1, // 引擎行零域——无声明期目标
                    AtomicEffects = new List<AtomicEffectEntry>
                    {
                        new AtomicEffectEntry
                        {
                            refId = AtomicEffectTable.GetByType(engineType)?.HashId,
                            value = 1,
                            branch = new BranchEntryData
                            {
                                settle = (int)BranchSettleKind.Engine,
                                engine = (int)kind,
                                engineParam = 1, // 附加张数=1
                                then = then,
                            },
                        },
                    },
                });
                return HToHand(h1, data);
            }

            // ---- ① 诅咒部署：施放结算时挂层+登记（对方牌库随机一张）----
            var curseEngine = MakeOnDrawEngine("V9H_CURSE_1", AtomicEffectType.EngineCurseOnDraw,
                BranchEngineKind.CurseOnDraw, new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DrawCard, value: 1) });
            VerifySuite.Assert(GameActions.PlayCard(hcore, h1, curseEngine, null, Zone.Hand, 0, out var rejectH1),
                $"诅咒引擎打出（拒绝原因：{rejectH1 ?? "无"}）");
            GameActions.DrainStack(hcore);
            Card cursed = hcore.ZoneManager.GetCards(h2, Zone.Deck)
                .FirstOrDefault(c => c.GetCounterCount(CounterRules.CurseCounter) > 0);
            VerifySuite.Assert(cursed != null && cursed.GetCounterCount(CounterRules.CurseCounter) == 1
                              && hcore.ZoneManager.GetCards(h1, Zone.Deck)
                                     .All(c => c.GetCounterCount(CounterRules.CurseCounter) == 0),
                "诅咒部署：施放结算时对方牌库随机一张挂 Curse=1（作用面=对手——己方牌库不受染）");
            var registered = CurseSystem.GetCurses(cursed);
            VerifySuite.Assert(registered.Count == 1 && registered[0].caster == h1,
                $"引擎载荷登记完整（施加方；实际 {registered.Count} 条）");

            // ---- ② Permanent：换区不清（对比锚——V9.g Exposed 换区即清） ----
            var hz = hcore.ZoneManager.GetZoneContainer(h2);
            hz.Move(cursed, Zone.Deck, Zone.Hand);
            VerifySuite.Assert(cursed.GetCounterCount(CounterRules.CurseCounter) == 1, "Deck→Hand 诅咒不清（Permanent）");
            hz.Move(cursed, Zone.Hand, Zone.Deck);
            VerifySuite.Assert(cursed.GetCounterCount(CounterRules.CurseCounter) == 1, "Hand→Deck 诅咒不清（活过换区）");

            // ---- ③ 抽到发作：消耗+播报+载荷落地（施诅方抽 1——SettleAsync 同步续行完成） ----
            string outbreak = null;
            EventManager.Instance.Subscribe<KeywordAppliedEvent>(
                e => { if (e.Keyword == "诅咒" && e.Target == cursed) outbreak = e.Detail; });
            int h1HandBefore = hcore.ZoneManager.GetCards(h1, Zone.Hand).Count;
            for (int i = 0; i < 64 && cursed.GetZone() == Zone.Deck; i++)
            {
                if (hcore.ZoneManager.DrawCard(h2) == null) break; // 防御：空库疲劳兜底
            }
            VerifySuite.Assert(cursed.GetZone() == Zone.Hand, "被诅咒卡抽到手上（触发时点=CardDrawEvent）");
            VerifySuite.Assert(cursed.GetCounterCount(CounterRules.CurseCounter) == 0
                              && CurseSystem.GetCurses(cursed).Count == 0,
                "一次性消耗：Curse 归零且载荷注册摘除");
            VerifySuite.Assert(outbreak != null && outbreak.Contains("诅咒发作"),
                $"发作播报已发布（实际：{outbreak ?? "无"}）");
            int h1HandAfter = hcore.ZoneManager.GetCards(h1, Zone.Hand).Count;
            VerifySuite.Assert(h1HandAfter - h1HandBefore == 1,
                $"引擎载荷落地：施诅方抽 1（{h1HandBefore}→{h1HandAfter}——Then 弹选口径、恰一次，CardDrawEvent 双发不重入）");

            // ---- ④ 祝福部署+发作：作用面=自己牌库、Blessing 指示物、抽到执行并消层 ----
            var blessEngine = MakeOnDrawEngine("V9H_BLESS_1", AtomicEffectType.EngineBlessingOnDraw,
                BranchEngineKind.BlessingOnDraw, new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DrawCard, value: 1) });
            VerifySuite.Assert(GameActions.PlayCard(hcore, h1, blessEngine, null, Zone.Hand, 0, out var rejectH3),
                $"祝福引擎打出（拒绝原因：{rejectH3 ?? "无"}）");
            GameActions.DrainStack(hcore);
            Card blessed = hcore.ZoneManager.GetCards(h1, Zone.Deck)
                .FirstOrDefault(c => c.GetCounterCount(CounterRules.BlessingCounter) > 0);
            VerifySuite.Assert(blessed != null && blessed.GetCounterCount(CounterRules.BlessingCounter) == 1
                              && hcore.ZoneManager.GetCards(h2, Zone.Deck)
                                     .All(c => c.GetCounterCount(CounterRules.BlessingCounter) == 0),
                "祝福部署：施放结算时自己牌库随机一张挂 Blessing=1（作用面=己方）");
            string blessOutbreak = null;
            EventManager.Instance.Subscribe<KeywordAppliedEvent>(
                e => { if (e.Keyword == "祝福" && e.Target == blessed) blessOutbreak = e.Detail; });
            int h1HandBefore2 = hcore.ZoneManager.GetCards(h1, Zone.Hand).Count;
            int blessDraws = 0;
            for (int i = 0; i < 64 && blessed.GetZone() == Zone.Deck; i++)
            {
                if (hcore.ZoneManager.DrawCard(h1) == null) break;
                blessDraws++;
            }
            int h1HandAfter2 = hcore.ZoneManager.GetCards(h1, Zone.Hand).Count;
            VerifySuite.Assert(blessed.GetZone() == Zone.Hand
                              && blessed.GetCounterCount(CounterRules.BlessingCounter) == 0
                              && CurseSystem.GetCurses(blessed).Count == 0,
                "祝福一次性消耗：Blessing 归零且载荷注册摘除");
            VerifySuite.Assert(blessOutbreak != null && blessOutbreak.Contains("祝福发作"),
                $"祝福发作播报已发布（实际：{blessOutbreak ?? "无"}）");
            VerifySuite.Assert(h1HandAfter2 - h1HandBefore2 == blessDraws + 1,
                $"祝福载荷落地：抽牌循环 {blessDraws} 张 + Then 奖励抽 1（{h1HandBefore2}→{h1HandAfter2}——抽到即触发，施加方=抽牌方）");

            // ---- ⑤ 空 Then 路径：converter「无 Then 条件空转整体折叠」——无载荷无部署（打出去也不挂层） ----
            var hz5 = hcore.ZoneManager.GetZoneContainer(h2);
            var h2Hand = hcore.ZoneManager.GetCards(h2, Zone.Hand).ToList();
            for (int i = 0; i < 3 && i < h2Hand.Count; i++)
                hz5.Move(h2Hand[i], Zone.Hand, Zone.Deck); // ③④ 可能抽空 h2 牌库——回填保证「有目标可挂」
            var emptyEngine = MakeOnDrawEngine("V9H_CURSE_2", AtomicEffectType.EngineCurseOnDraw,
                BranchEngineKind.CurseOnDraw, null);
            var emptyDef = CardEffectConverter.ConvertOne(((CardWrapper)emptyEngine).GetData().Effects[0], "V9H_src");
            VerifySuite.Assert(emptyDef != null && emptyDef.Effects.All(a => a.Branch == null),
                "空 Then 引擎载荷整体折叠（converter 无 Then 条件空转口径——不部署不挂层）");
            int h1HandBefore3 = hcore.ZoneManager.GetCards(h1, Zone.Hand).Count;
            VerifySuite.Assert(GameActions.PlayCard(hcore, h1, emptyEngine, null, Zone.Hand, 0, out var rejectH2),
                $"空 Then 诅咒引擎打出（拒绝原因：{rejectH2 ?? "无"}）");
            GameActions.DrainStack(hcore);
            VerifySuite.Assert(hcore.ZoneManager.GetCards(h2, Zone.Deck)
                                  .All(c => c.GetCounterCount(CounterRules.CurseCounter) == 0),
                "空 Then：无部署（对方牌库无 Curse 挂层——条件空转不投放）");
            VerifySuite.Assert(hcore.ZoneManager.GetCards(h1, Zone.Hand).Count == h1HandBefore3 - 1,
                "空 Then 空转：打出只消耗手牌自身（无任何原子执行）");

            // ============================ V9.i 规则光环（规则轴路线 B 转正，2026-10-03 定案） ============================

            VerifySuite.Section("V9.i 规则光环：唯一性/双方生效/载体离场失效/七规则接线");
            var icore = GameCore.Instance;
            icore.Reset();
            ZoneContainer.Reseed(20261006);
            var fillerI = Enumerable.Range(0, 8).Select(i => new CardData
            {
                ID = $"V9I_fill_{i}", CardName = "V9I填充" + i, Supertype = Cardtype.Creature, Power = 1, Life = 1,
            }).ToList();
            icore.InitGame(CardLoader.BuildDeck(fillerI, 2), CardLoader.BuildDeck(fillerI, 2));
            var i1 = icore.Player1;
            var i2 = icore.Player2;
            GameActions.SkipElementPool(icore, i1);
            foreach (ManaType t in Enum.GetValues(typeof(ManaType)))
                icore.ElementPool.GetPool(i1).TempElements[t] = 99; // 夹具卡免费用障碍（局即弃不还原）
            icore.ElementPool.GetPool(i1).GlobalTurnIndex = 9; // 浓度上限放开到 9（2026-10-04 用户调价适配：仪典锚价×2/×4 后载体建议费超回合 1 上限）

            Card IToHand(Player owner, CardData data)
            {
                var card = new CardWrapper(data);
                card.SetController(owner);
                icore.ZoneManager.GetZoneContainer(owner).Add(card, Zone.Hand);
                return card;
            }

            // 载体工厂：结界 + 耐久 + 登场效果 ModifyGameRule（refId=表行、str=规则短名）
            Card MakeRuleCarrier(string enumName, string ruleId, int durability)
            {
                // 装载后 config.EnumName=英文 EffectType、中文短名落在 DisplayName（字段换位历史遗留）
                var row = AtomicEffectTable.GetAll().FirstOrDefault(r => r != null && r.DisplayName == enumName);
                VerifySuite.Assert(row != null, $"原子表存在「{enumName}」行（EffectType=ModifyGameRule）");
                var data = new CardData
                {
                    ID = "V9I_" + ruleId, CardName = enumName, Supertype = Cardtype.Enchantment, Durability = durability,
                    // 显式声明费（2026-10-10 可支配底盘适配；2026-10-13 缩盘）：结界剩余点不再抵卡价（走运行时二向），
                    // 建议价 +2 后耐久9载体 ≈10 > 浓度上限 7（标准曲线封顶 7）——本段测规则光环行为非计价，
                    // 声明 7 灰锚定支付，与建议价解耦
                    Cost = ElementCost.FromValue(ManaType.Gray, 7),
                };
                data.Effects.Add(new CardEffectData
                {
                    Id = "V9I_" + ruleId + "_EFF",
                    TriggerTiming = (int)TriggerTiming.OnPlay,
                    SelectionMode = -1, // None：规则投放无目标选择
                    AtomicEffects = new List<AtomicEffectEntry>
                    {
                        new AtomicEffectEntry { refId = row?.HashId, value = 1, str = ruleId },
                    },
                });
                return IToHand(i1, data);
            }

            // ---- ① 激活 + 离散伤害二值化（2026-10-04 蚕褪改版：>5→5、<5→3；生物与角色、双方生效） ----
            var capCarrier = MakeRuleCarrier("离散仪典", RuleAuraComponents.DamageCap, 9);
            VerifySuite.Assert(GameActions.PlayCard(icore, i1, capCarrier, null, Zone.Hand, 0, out var rejectI1),
                $"离散载体打出（拒绝原因：{rejectI1 ?? "无"}）");
            GameActions.DrainStack(icore);
            VerifySuite.Assert(capCarrier.GetZone() == Zone.Battlefield && RuleAuraSystem.IsActive(RuleAuraComponents.DamageCap),
                "载体入场 + 规则激活（IsActive 实时查询）");
            int i1LifeCap = i1.Life, i2LifeCap = i2.Life;
            KeywordRules.ApplyDamage(i2, i1, 8, isCombat: false);
            KeywordRules.ApplyDamage(i1, i2, 4, isCombat: false);
            VerifySuite.Assert(i1.Life == i1LifeCap - 5 && i2.Life == i2LifeCap - 3,
                $"离散双方生效（角色）：8 伤改 5、4 伤改 3（p1 {i1LifeCap}→{i1.Life}，p2 {i2LifeCap}→{i2.Life}）");
            var capCreature = new CardWrapper(new CardData
            {
                ID = "V9I_CAP_CR", CardName = "离散生物探针", Supertype = Cardtype.Creature, Power = 1, Life = 20,
            });
            capCreature.SetController(i2);
            KeywordRules.ApplyDamage(i1, capCreature, 8, isCombat: false);
            VerifySuite.Assert(capCreature.GetLife() == 15,
                $"离散扩生物：单次 8 伤改 5（实际余 {capCreature.GetLife()}）");
            KeywordRules.ApplyDamage(i1, capCreature, 1, isCombat: false);
            KeywordRules.ApplyDamage(i1, capCreature, 3, isCombat: false);
            VerifySuite.Assert(capCreature.GetLife() == 9,
                $"离散低段抬底：1 伤改 3、3 伤恰值不动（20→15→12→9，实际 {capCreature.GetLife()}）");
            KeywordRules.ApplyDamage(i1, capCreature, 5, isCombat: false);
            VerifySuite.Assert(capCreature.GetLife() == 4,
                $"离散恰 5 不改写（实际余 {capCreature.GetLife()}）");

            // ---- ①b 丰盈（2026-10-04 改造）：回复溢出→生命上限+1（收编旧写死基线）----
            var bloomCarrier = MakeRuleCarrier("丰盈仪典", RuleAuraComponents.HealOverflow, 9);
            VerifySuite.Assert(GameActions.PlayCard(icore, i1, bloomCarrier, null, Zone.Hand, 0, out var rejectI1b),
                $"丰盈载体打出（异名共存；拒绝原因：{rejectI1b ?? "无"}）");
            GameActions.DrainStack(icore);
            VerifySuite.Assert(capCarrier.GetZone() == Zone.Battlefield
                              && RuleAuraSystem.IsActive(RuleAuraComponents.HealOverflow)
                              && RuleAuraSystem.IsActive(RuleAuraComponents.DamageCap),
                "异名共存（2026-10-07 唯一性改版）：离散在场不送墓，丰盈照常激活（多槽并存）");
            int i1MaxBloom = i1.MaxHealth;
            i1.Life = i1.MaxHealth; // 满血基线（此前伤害帽扣过血）
            i1.Heal(3);
            VerifySuite.Assert(i1.MaxHealth == i1MaxBloom + 1 && i1.Life == i1.MaxHealth,
                $"丰盈：满血回 3 → 上限+1、当前=新上限（实际 {i1MaxBloom}→{i1.MaxHealth}）");
            // 溢出剩余截断：再回 5 仍只 +1
            int i1MaxBloom2 = i1.MaxHealth;
            i1.Heal(5);
            VerifySuite.Assert(i1.MaxHealth == i1MaxBloom2 + 1,
                $"丰盈：溢出不折半——每次溢出固定+1层（回 5 仍 {i1MaxBloom2}→{i1.MaxHealth}）");

            // ---- ② 同名禁止 + 异名共存（2026-10-07 唯一性改版：同名在场不可再打出、异名多槽并存）----
            var bloomDup = MakeRuleCarrier("丰盈仪典", RuleAuraComponents.HealOverflow, 9);
            VerifySuite.Assert(!GameActions.PlayCard(icore, i1, bloomDup, null, Zone.Hand, 0, out var rejectI2)
                              && rejectI2 != null,
                $"同名禁止：丰盈在场→第二张丰盈被拒（{rejectI2}——不分范围/敌我）");
            var riverCarrier = MakeRuleCarrier("纳川仪典", RuleAuraComponents.HandLimitNoFatigue, 2);
            VerifySuite.Assert(GameActions.PlayCard(icore, i1, riverCarrier, null, Zone.Hand, 0, out var rejectI2b),
                $"纳川载体打出（异名共存；拒绝原因：{rejectI2b ?? "无"}）");
            GameActions.DrainStack(icore);
            VerifySuite.Assert(bloomCarrier.GetZone() == Zone.Battlefield
                              && RuleAuraSystem.IsActive(RuleAuraComponents.HealOverflow)
                              && RuleAuraSystem.IsActive(RuleAuraComponents.HandLimitNoFatigue),
                "异名共存：丰盈不送墓、纳川照常激活（多槽并存）");
            // 丰盈载体被摧毁（耐久 9+免费点2（1槽OnPlay不装主动→全投耐久，2026-10-15 拆分定案）
            // 逐点扣——结界无生命单位不走离散改写/伤害帽）→ 溢出回落基线
            for (int d = 0; d < 9 + 2; d++) KeywordRules.ApplyDamage(i2, bloomCarrier, 1, isCombat: true);
            VerifySuite.Assert(bloomCarrier.GetZone() == Zone.Graveyard
                              && !RuleAuraSystem.IsActive(RuleAuraComponents.HealOverflow),
                "丰盈载体被摧毁送墓 → 规则失效（载体离场自然失效）");
            // 丰盈离场即回基线：溢出纯浪费（生命钳上限，不加上限）
            int i1MaxAfter = i1.MaxHealth;
            i1.Life = i1.MaxHealth;
            i1.Heal(4);
            VerifySuite.Assert(i1.MaxHealth == i1MaxAfter && i1.Life == i1MaxAfter,
                "无丰盈（2026-10-04 基线收编后）：溢出纯浪费（上限不动、生命钳上限）");

            // ---- ③ 纳川：手牌上限 15 + 疲劳免疫（双方） ----
            VerifySuite.Assert(RuleHooks.GetHandLimit(i1) == 15 && RuleHooks.GetHandLimit(i2) == 15,
                $"手牌上限 15 双方生效（p1={RuleHooks.GetHandLimit(i1)}，p2={RuleHooks.GetHandLimit(i2)}）");
            var icz2 = icore.ZoneManager.GetZoneContainer(i2);
            foreach (var deckCard in icore.ZoneManager.GetCards(i2, Zone.Deck).ToList())
                icz2.Move(deckCard, Zone.Deck, Zone.Graveyard);
            int i2LifeFatigue = i2.Life;
            VerifySuite.Assert(icore.ZoneManager.DrawCard(i2) == null, "空库抽牌返回 null（疲劳路径入口）");
            VerifySuite.Assert(i2.Life == i2LifeFatigue,
                $"疲劳免疫：空库抽牌 0 伤（{i2LifeFatigue}→{i2.Life}——FatigueEvent 替代为 0）");

            // ---- ④ 载体离场失效（可被摧毁：耐久归零 Smashed 管线；纳川 2+免费点2=4 层） ----
            for (int d = 0; d < 2 + 2; d++) KeywordRules.ApplyDamage(i2, riverCarrier, 1, isCombat: true);
            VerifySuite.Assert(riverCarrier.GetZone() == Zone.Graveyard, "载体耐久归零直送墓（可被摧毁）");
            VerifySuite.Assert(!RuleAuraSystem.IsActive(RuleAuraComponents.HandLimitNoFatigue), "载体离场规则失效");
            VerifySuite.Assert(RuleHooks.GetHandLimit(i1) == RuleHooks.DefaultHandLimit,
                $"手牌上限回落基值（实际 {RuleHooks.GetHandLimit(i1)}）");

            // ---- ⑤ 局重置回收（新局跨局不残留 + 显式 Reset 清槽） ----
            icore.InitGame(CardLoader.BuildDeck(fillerI, 2), CardLoader.BuildDeck(fillerI, 2));
            i1 = icore.Player1;
            i2 = icore.Player2;
            GameActions.SkipElementPool(icore, i1);
            foreach (ManaType t in Enum.GetValues(typeof(ManaType)))
                icore.ElementPool.GetPool(i1).TempElements[t] = 99;
            icore.ElementPool.GetPool(i1).GlobalTurnIndex = 9; // InitGame 重置后重设（浓度上限放开，同段首）
            VerifySuite.Assert(RuleAuraSystem.ActiveRules.Count == 0, "新局光环槽位空（InitGame 含 Reset——跨局不残留）");
            var capCarrier2 = MakeRuleCarrier("离散仪典", RuleAuraComponents.DamageCap, 2);
            VerifySuite.Assert(GameActions.PlayCard(icore, i1, capCarrier2, null, Zone.Hand, 0, out var rejectI3),
                $"重激活离散（拒绝原因：{rejectI3 ?? "无"}）");
            GameActions.DrainStack(icore);
            VerifySuite.Assert(RuleAuraSystem.IsActive(RuleAuraComponents.DamageCap), "重激活成功（前置）");
            icore.Reset();
            VerifySuite.Assert(RuleAuraSystem.ActiveRules.Count == 0
                              && !RuleAuraSystem.IsActive(RuleAuraComponents.DamageCap)
                              && RuleHooks.GetHandLimit(i1) == RuleHooks.DefaultHandLimit,
                "局重置：光环槽位回收、全部规则失效（修改链/替代件实时查询自然回落）");

            // ---- ⑥ 归土：墓地视手牌使用（每回合一次配额；对双方——i2 侧后验） ----
            icore.InitGame(CardLoader.BuildDeck(fillerI, 2), CardLoader.BuildDeck(fillerI, 2));
            i1 = icore.Player1;
            i2 = icore.Player2;
            GameActions.SkipElementPool(icore, i1);
            foreach (ManaType t in Enum.GetValues(typeof(ManaType)))
                icore.ElementPool.GetPool(i1).TempElements[t] = 99;
            icore.ElementPool.GetPool(i1).GlobalTurnIndex = 9; // InitGame 重置后重设（浓度上限放开，同段首）
            var graveCarrier = MakeRuleCarrier("归土仪典", RuleAuraComponents.GraveyardPlay, 2);
            VerifySuite.Assert(GameActions.PlayCard(icore, i1, graveCarrier, null, Zone.Hand, 0, out var rejectI4),
                $"归土载体打出（拒绝原因：{rejectI4 ?? "无"}）");
            GameActions.DrainStack(icore);

            Card GraveResident(string id)
            {
                var c = new CardWrapper(new CardData
                {
                    ID = id, CardName = "墓地居民", Supertype = Cardtype.Creature, Power = 1, Life = 1,
                });
                c.SetController(i1);
                icore.ZoneManager.GetZoneContainer(i1).Add(c, Zone.Graveyard);
                return c;
            }
            var gyCard1 = GraveResident("V9I_GY_1");
            VerifySuite.Assert(GameActions.PlayCard(icore, i1, gyCard1, null, Zone.Graveyard, 0, out var rejectGy1),
                $"墓地牌视手牌打出（拒绝原因：{rejectGy1 ?? "无"}）");
            GameActions.DrainStack(icore);
            VerifySuite.Assert(gyCard1.GetZone() == Zone.Battlefield, "墓地牌成功入场");
            var gyCard2 = GraveResident("V9I_GY_2");
            VerifySuite.Assert(!GameActions.PlayCard(icore, i1, gyCard2, null, Zone.Graveyard, 0, out var rejectGy2)
                              && rejectGy2 != null,
                $"每回合一次配额：第二张被拒（{rejectGy2}）");

            // ---- ⑦ 窥渊（2026-10-04 时机改版：回合开始→回合结束）：回合末随机展示 + 锁定落在指示物倒数之后 ----
            var lockCarrier = MakeRuleCarrier("窥渊仪典", RuleAuraComponents.LockRevealed, 2);
            VerifySuite.Assert(GameActions.PlayCard(icore, i1, lockCarrier, null, Zone.Hand, 0, out var rejectI5),
                $"窥渊载体打出（异名共存；拒绝原因：{rejectI5 ?? "无"}）");
            GameActions.DrainStack(icore);
            VerifySuite.Assert(graveCarrier.GetZone() == Zone.Battlefield
                              && RuleAuraSystem.IsActive(RuleAuraComponents.LockRevealed),
                "异名共存：归土载体不送墓、窥渊照常激活");

            var iRevealData = new CardData { ID = "V9I_REVEAL", CardName = "V9I展示术", Supertype = Cardtype.Spell };
            iRevealData.Effects.Add(new CardEffectData
            {
                Id = "V9I_REVEAL_EFF",
                TriggerTiming = (int)TriggerTiming.OnPlay,
                SelectionMode = -1,
                AtomicEffects = new List<AtomicEffectEntry>
                {
                    AtomRefs.New(AtomicEffectType.RevealCard, value: 1, kinds: new List<int> { 6 }), // 对方手牌随机一张
                },
            });
            var iRevealCard = IToHand(i1, iRevealData);
            VerifySuite.Assert(GameActions.PlayCard(icore, i1, iRevealCard), "展示术打出");
            GameActions.DrainStack(icore);
            var i2Exposed = RevealRules.GetExposedCards(icore.ZoneManager, i2, Zone.Hand);
            VerifySuite.Assert(i2Exposed.Count == 1, "对方手牌一张被展示（信息轴联动锚）");

            // 展示源保险：i1 手牌补一张填充（保证 i2 回合末的光环随机展示必有未展示候选）
            IToHand(i1, new CardData { ID = "V9I_REVEAL_FILL", CardName = "展示填充", Supertype = Cardtype.Spell });
            GameActions.EndTurn(icore, i1); // i1 回合结束：光环随机展示 i2 一张 + 被展示的卡锁定×1（倒数后挂层）
            var i2Locked = i2Exposed[0];
            VerifySuite.Assert(RevealRules.GetExposedCards(icore.ZoneManager, i2, Zone.Hand).Count == 2,
                "光环随机展示：回合结束随机展示对手一张手牌（RevealCard 同款口径）");
            VerifySuite.Assert(i2Locked.GetCounterCount(CounterRules.LockCounter) == 1
                              && !RuleHooks.CanPlay(icore, i2, i2Locked, Zone.Hand),
                "回合结束赋予对手被展示卡锁定指示物×1（全部被展示卡，无选择窗口；期间无法使用）");
            icore.TurnEngine.CheckPhaseTransition(); // → i2 回合
            GameActions.SkipElementPool(icore, i2);
            // 环境确定性：i2 手牌裁到只剩被锁定卡——回合末手牌上限弃牌不弃掉被展示卡污染断言
            //（无头自动弃牌可能弃掉被展示卡→换区清展示→污染续锁断言）
            foreach (var c in icore.ZoneManager.GetCards(i2, Zone.Hand).ToList())
                if (c != i2Locked) icore.ZoneManager.MoveCard(c, i2, Zone.Hand, Zone.Graveyard);
            GameActions.EndTurn(icore, i2); // i2 回合结束：③ 块倒数锁 1→0 解锁；其后光环随机展示 i1 一张并挂锁
            VerifySuite.Assert(i2Locked.GetCounterCount(CounterRules.LockCounter) == 0
                              && RuleHooks.CanPlay(icore, i2, i2Locked, Zone.Hand),
                "持有者回合结束锁定倒数归零（手牌区与场上同样结算）——解锁可打出");
            var i1ExposedCards = RevealRules.GetExposedCards(icore.ZoneManager, i1, Zone.Hand);
            VerifySuite.Assert(i1ExposedCards.Count == 1
                              && i1ExposedCards[0].GetCounterCount(CounterRules.LockCounter) == 1,
                "同回合末随机展示 i1 一张并挂锁——挂层落在指示物倒数之后（新锁不被本回合末吞层）");
            icore.TurnEngine.CheckPhaseTransition(); // → i1 回合
            GameActions.SkipElementPool(icore, i1);

            // ---- ⑦b 锁定原子直发（LockCard value=2）：层=剩余回合，直调两个持有者回合末倒数
            //     （不经回合引擎推进——光环回合末展示/续锁不干扰层数口径；回合集成路径 ⑦ 已覆盖）----
            var lockProbe = IToHand(i2, new CardData
            {
                ID = "V9I_LOCK2", CardName = "锁定探针", Supertype = Cardtype.Creature, Power = 1, Life = 1,
            });
            CardCore.Attribute.EffectHandlerRegistry.ExecuteEffectAsync(
                new AtomicEffectInstance { Type = AtomicEffectType.LockCard, Value = 2 },
                new EffectExecutionContext { Controller = i1, Source = i1, Targets = new List<Entity> { lockProbe } })
                .GetAwaiter().GetResult();
            VerifySuite.Assert(lockProbe.GetCounterCount(CounterRules.LockCounter) == 2
                              && !RuleHooks.CanPlay(icore, i2, lockProbe, Zone.Hand),
                "锁定原子（value=2）：挂 2 层锁定，期间无法使用");
            VerifySuite.Assert(!ElementPoolSystem.CanServeAsLand(lockProbe),
                "锁定期间无法进入地牌区（放地不得绕道封锁——CanServeAsLand 单点，UI/AI 预检同口径）");
            CounterRules.OnTurnEnd(i2, icore.ZoneManager); // 持有者回合末 ①：2→1
            VerifySuite.Assert(lockProbe.GetCounterCount(CounterRules.LockCounter) == 1
                              && !RuleHooks.CanPlay(icore, i2, lockProbe, Zone.Hand)
                              && !ElementPoolSystem.CanServeAsLand(lockProbe),
                "锁定×2 首个持有者回合末：2→1 仍锁定");
            CounterRules.OnTurnEnd(i2, icore.ZoneManager); // 持有者回合末 ②：1→0
            VerifySuite.Assert(lockProbe.GetCounterCount(CounterRules.LockCounter) == 0
                              && RuleHooks.CanPlay(icore, i2, lockProbe, Zone.Hand)
                              && ElementPoolSystem.CanServeAsLand(lockProbe),
                "锁定×2 次个持有者回合末：1→0 解锁（手牌区倒数全程在场外结算），地牌资格恢复");

            // ---- ⑦c 临摹/变形赋予（2026-10-13 定案）：效果取代+跳规则光环+变形指示物+纯回滚+差额补蓝 ----
            Card LinmoCard(string id, string name, Cardtype type, int power, int life, int grayCost,
                Player owner, List<string> keywords, List<CardEffectData> effects)
            {
                var data = new CardData
                {
                    ID = id, CardName = name, Supertype = type, Power = power, Life = life,
                    Cost = ElementCost.FromValue(ManaType.Gray, grayCost),
                    Keywords = keywords, Effects = effects,
                };
                var card = new CardWrapper(data);
                card.SetController(owner);
                icore.ZoneManager.GetZoneContainer(owner).Add(card, Zone.Battlefield);
                GameActions.RegisterCardTriggeredEffects(icore, card, owner); // 教学 Seeder 同款显式注册
                return card;
            }

            var ritualRef = AtomicEffectTable.GetByType(AtomicEffectType.ModifyGameRule)?.HashId;
            // 临摹自身卡（i1 生物，灰2）：原触发式（OnTurnEnd 抽1）+ 印刷关键词 嘲讽
            var linmoSelf = LinmoCard("V9I_LINMO_SELF", "临摹者", Cardtype.Creature, 2, 3, 2, i1,
                new List<string> { KeywordRules.Taunt },
                new List<CardEffectData>
                {
                    new CardEffectData
                    {
                        Id = "V9I_LINMO_SELF_EFF", TriggerTiming = (int)TriggerTiming.OnTurnEnd, SelectionMode = -1,
                        AtomicEffects = new List<AtomicEffectEntry>
                            { AtomRefs.New(AtomicEffectType.DrawCard, value: 1) },
                    },
                });
            // 临摹模板源（i2 生物，灰5）：触发式（OnTurnEnd 抽1）+ **规则光环步**（窥渊——⑦ 段已活跃）
            var linmoSrc = LinmoCard("V9I_LINMO_SRC", "临摹源", Cardtype.Creature, 4, 6, 5, i2,
                new List<string> { KeywordRules.Venom },
                new List<CardEffectData>
                {
                    new CardEffectData
                    {
                        Id = "V9I_LINMO_SRC_EFF", TriggerTiming = (int)TriggerTiming.OnTurnEnd, SelectionMode = -1,
                        AtomicEffects = new List<AtomicEffectEntry>
                            { AtomRefs.New(AtomicEffectType.DrawCard, value: 1) },
                    },
                    new CardEffectData
                    {
                        Id = "V9I_LINMO_SRC_AURA", TriggerTiming = (int)TriggerTiming.OnPlay, SelectionMode = -1,
                        Steps = ritualRef == null ? null : new List<EffectStepData>
                        {
                            new EffectStepData
                            {
                                kind = 0,
                                atomic = new AtomicEffectEntry { refId = ritualRef, value = 1, str = RuleAuraComponents.LockRevealed },
                            },
                        },
                    },
                });
            bool HasTrigger(Card c, string effId)
                => icore.TriggerEngine.RegisteredEffects.Any(r => ReferenceEquals(r.Source, c) && r.Effect?.Id == effId);

            // 模板源补连接光环（箭头类——2026-10-13 定案：临摹复制非规则光环）
            var linmoSrcData = ((CardWrapper)linmoSrc).GetData();
            linmoSrcData.LinkAuras = new List<LinkAuraData> { new LinkAuraData { stat = "Power", value = 2 } };
            linmoSrcData.ArrowDirections = HexDirection.Up | HexDirection.Down;

            CardCore.Attribute.EffectHandlerRegistry.ExecuteEffectAsync(
                new AtomicEffectInstance { Type = AtomicEffectType.Morph, Value = 1 },
                new EffectExecutionContext { Controller = i1, Source = linmoSelf, Targets = new List<Entity> { linmoSrc } })
                .GetAwaiter().GetResult();
            VerifySuite.Assert(linmoSelf.ID == "V9I_LINMO_SRC"
                              && (linmoSelf as IHasName)?.CardName == "临摹源"
                              && linmoSelf.GetPower() == 4 && linmoSelf.GetLife() == 6
                              && linmoSelf.HasKeyword(KeywordRules.Venom) && !linmoSelf.HasKeyword(KeywordRules.Taunt)
                              && linmoSelf.GetCounterCount(CounterRules.MorphStateCounter) == 1,
                "临摹：身份/属性/关键词复制 + 变形指示物 ×1（中性·极性0）");
            VerifySuite.Assert(((CardWrapper)linmoSelf).GetData().Effects.Count == 1
                              && ReferenceEquals(RuleAuraSystem.CarrierOf(RuleAuraComponents.LockRevealed), lockCarrier),
                "临摹跳过唯一效果：规则光环步被滤除（空效果整条丢），既有同名光环载体不被顶替");
            VerifySuite.Assert(HasTrigger(linmoSelf, "V9I_LINMO_SRC_EFF") && !HasTrigger(linmoSelf, "V9I_LINMO_SELF_EFF"),
                "临摹触发式重挂：模板触发式按自身注册、原触发式整卡注销（UnregisterEntityEffects 首个消费者）");
            VerifySuite.Assert(MorphSystem.ActivationSurcharge(linmoSelf) == 3,
                "临摹发动补价（差额恒蓝·蓝色专属）：变形后 5 点 − 变形前 2 点 = 3 蓝（补价额=总点差·与卡价颜色无关）");
            var linmoSelfData = ((CardWrapper)linmoSelf).GetData();
            VerifySuite.Assert(linmoSelfData.LinkAuras != null && linmoSelfData.LinkAuras.Count == 1
                              && linmoSelfData.LinkAuras[0].stat == "Power" && linmoSelfData.LinkAuras[0].value == 2
                              && linmoSelfData.ArrowDirections == (HexDirection.Up | HexDirection.Down),
                "临摹复制连接光环（箭头类）：LinkAuras+箭头方向随形态复制（非规则光环不跳过）");

            // 净化解除（场上）：纯回滚不刷新——变形期间受到的伤害与横置随回滚丢弃，恢复变形前状态
            linmoSelf.Tap(); // 变形期间横置（生物形态可横置）
            KeywordRules.ApplyDamage(null, linmoSelf, 1, false); // 变形期间受 1 伤（6→5）
            CounterRules.PurgeAll(linmoSelf);
            VerifySuite.Assert(linmoSelf.ID == "V9I_LINMO_SELF"
                              && (linmoSelf as IHasName)?.CardName == "临摹者"
                              && linmoSelf.GetPower() == 2 && linmoSelf.GetLife() == 3
                              && !linmoSelf.IsTapped()
                              && linmoSelf.HasKeyword(KeywordRules.Taunt) && !linmoSelf.HasKeyword(KeywordRules.Venom)
                              && ((CardWrapper)linmoSelf).GetData().Effects.Count == 1
                              && linmoSelf.GetCounterCount(CounterRules.MorphStateCounter) == 0
                              && !MorphSystem.IsMorphed(linmoSelf)
                              && HasTrigger(linmoSelf, "V9I_LINMO_SELF_EFF") && !HasTrigger(linmoSelf, "V9I_LINMO_SRC_EFF")
                              && MorphSystem.ActivationSurcharge(linmoSelf) == 0,
                "净化解除=纯回滚：身份/属性/关键词/文本效果/横置态还原变形前值（变形期伤害与横置丢弃、无入场刷新），原触发式重挂、补价归零");
            VerifySuite.Assert((linmoSelfData.LinkAuras == null || linmoSelfData.LinkAuras.Count == 0)
                              && linmoSelfData.ArrowDirections == HexDirection.None,
                "回滚含连接光环：原形态无光环/无箭头——解除后清空变形期复制的光环与箭头");

            // 变形赋予（引用卡ID）：目标变成结界形态——自动非横置 + 耐久初始化；换区解除还原
            var morphForm = new CardData
            {
                ID = "V9I_MORPH_FORM", CardName = "变形形态", Supertype = Cardtype.Enchantment,
                Durability = 3, Cost = ElementCost.FromValue(ManaType.Gray, 4),
            };
            var grantTarget = LinmoCard("V9I_GRANT_TGT", "变形对象", Cardtype.Creature, 1, 2, 1, i1,
                new List<string>(), new List<CardEffectData>());
            grantTarget.Tap(); // 变形前横置——结界形态应自动非横置，解除还原横置
            var linmoPrevResolver = CardCore.Attribute.Handlers.SummonTokenHandler.ResolveTemplate;
            CardCore.Attribute.Handlers.SummonTokenHandler.ResolveTemplate = id => id == "V9I_MORPH_FORM" ? morphForm : null;
            try
            {
                CardCore.Attribute.EffectHandlerRegistry.ExecuteEffectAsync(
                    new AtomicEffectInstance { Type = AtomicEffectType.GrantMorph, Value = 1, StringValue = "V9I_MORPH_FORM" },
                    new EffectExecutionContext { Controller = i1, Source = i1, Targets = new List<Entity> { grantTarget } })
                    .GetAwaiter().GetResult();
            }
            finally { CardCore.Attribute.Handlers.SummonTokenHandler.ResolveTemplate = linmoPrevResolver; }
            VerifySuite.Assert(grantTarget.ID == "V9I_MORPH_FORM"
                              && grantTarget.IsNonLivingUnit() && !grantTarget.IsTapped()
                              && grantTarget.GetCounterCount(CounterRules.DurabilityCounter) >= 3
                              && grantTarget.GetCounterCount(CounterRules.MorphStateCounter) == 1,
                "变形赋予：目标变成引用卡（结界形态自动非横置+按入场同口径初始化耐久）+ 变形指示物");
            icore.ZoneManager.MoveCard(grantTarget, i1, Zone.Battlefield, Zone.Hand); // 换区解除（按现有逻辑）
            VerifySuite.Assert(grantTarget.ID == "V9I_GRANT_TGT"
                              && grantTarget.GetZone() == Zone.Hand
                              && grantTarget.IsTapped() // 解除还原变形前横置态
                              && grantTarget.GetCounterCount(CounterRules.MorphStateCounter) == 0
                              && grantTarget.GetCounterCount(CounterRules.DurabilityCounter) == 0
                              && !MorphSystem.IsMorphed(grantTarget),
                "换区解除变形（按现有逻辑）：指示物/耐久随 sweep 清、原形态与变形前横置态还原");

            // 减益解除（2026-10-13，白2=净化−1）：负面清空、正面与中性（变形指示物）保留
            var debuffProbe = LinmoCard("V9I_DEBUFF_TGT", "减益探针", Cardtype.Creature, 1, 1, 1, i1,
                new List<string>(), new List<CardEffectData>());
            debuffProbe.AddCounters(CounterRules.ToxinCounter, 2);
            debuffProbe.AddCounters(CounterRules.DivineShieldCounter, 1);
            debuffProbe.AddCounters(CounterRules.MorphStateCounter, 1);
            CardCore.Attribute.EffectHandlerRegistry.ExecuteEffectAsync(
                new AtomicEffectInstance { Type = AtomicEffectType.RemoveDebuffs, Value = 1 },
                new EffectExecutionContext { Controller = i1, Source = i1, Targets = new List<Entity> { debuffProbe } })
                .GetAwaiter().GetResult();
            VerifySuite.Assert(debuffProbe.GetCounterCount(CounterRules.ToxinCounter) == 0
                              && debuffProbe.GetCounterCount(CounterRules.DivineShieldCounter) == 1
                              && debuffProbe.GetCounterCount(CounterRules.MorphStateCounter) == 1,
                "减益解除：负面指示物清空，正面与中性（极性0）指示物保留");

            // 沉默/缄默与净化、减益解除的交互（2026-10-19 规格定案：压制类指示物归净化/解减益清除）
            debuffProbe.AddCounters(CounterRules.SilenceCounter, 1);
            debuffProbe.AddCounters(CounterRules.NullifyCounter, 1);
            debuffProbe.AddCounters(CounterRules.PlusOneCounter, 1);
            CardCore.Attribute.EffectHandlerRegistry.ExecuteEffectAsync(
                new AtomicEffectInstance { Type = AtomicEffectType.RemoveDebuffs, Value = 1 },
                new EffectExecutionContext { Controller = i1, Source = i1, Targets = new List<Entity> { debuffProbe } })
                .GetAwaiter().GetResult();
            VerifySuite.Assert(debuffProbe.GetCounterCount(CounterRules.SilenceCounter) == 0
                              && debuffProbe.GetCounterCount(CounterRules.NullifyCounter) == 0
                              && debuffProbe.GetCounterCount(CounterRules.PlusOneCounter) == 1,
                "减益解除清沉默/缄默层（负面极性），正面指示物保留");
            debuffProbe.AddCounters(CounterRules.SilenceCounter, 1);
            debuffProbe.AddCounters(CounterRules.NullifyCounter, 1);
            CardCore.Attribute.EffectHandlerRegistry.ExecuteEffectAsync(
                new AtomicEffectInstance { Type = AtomicEffectType.Purify, Value = 1 },
                new EffectExecutionContext { Controller = i1, Source = i1, Targets = new List<Entity> { debuffProbe } })
                .GetAwaiter().GetResult();
            VerifySuite.Assert(debuffProbe.GetCounterCount(CounterRules.SilenceCounter) == 0
                              && debuffProbe.GetCounterCount(CounterRules.NullifyCounter) == 0,
                "净化全清沉默/缄默层");

            // 探针离场清理（不扰动后续 ⑧-⑩ 段：注册注销+移除外区）
            icore.TriggerEngine.UnregisterEntityEffects(linmoSelf);
            icore.TriggerEngine.UnregisterEntityEffects(linmoSrc);
            icore.ZoneManager.MoveCard(linmoSelf, i1, Zone.Battlefield, Zone.Exile);
            icore.ZoneManager.MoveCard(linmoSrc, i2, Zone.Battlefield, Zone.Exile);
            icore.ZoneManager.MoveCard(grantTarget, i1, Zone.Hand, Zone.Exile);
            icore.ZoneManager.MoveCard(debuffProbe, i1, Zone.Battlefield, Zone.Exile);

            GameActions.EndTurn(icore, i1); // i1 回合结束：③ 块倒数 i1 被展示卡锁 1→0；其后光环对 i2 仍被展示的卡续锁
            VerifySuite.Assert(i2Locked.GetCounterCount(CounterRules.LockCounter) == 1,
                "持续暴露续锁：每回合结束对仍被展示的卡再赋一回合锁定（倒数先、挂层后）");
            icore.TurnEngine.CheckPhaseTransition(); // → i2 回合
            GameActions.SkipElementPool(icore, i2);
            GameActions.EndTurn(icore, i2); // 回合交还 i1（⑧-⑩ 同一主阶段连续驱动）
            icore.TurnEngine.CheckPhaseTransition();
            GameActions.SkipElementPool(icore, i1);
            // 2026-10-11 现取现用：⑦ 段多轮 EndTurn 已清空临时钱包——⑧-⑩ 前重播种（夹具免费用障碍）
            foreach (ManaType t in Enum.GetValues(typeof(ManaType)))
                icore.ElementPool.GetPool(i1).TempElements[t] = 99;

            // ---- ⑧ 三相：消耗 3 同色纯元素 → 红/蓝/绿各 1 ----
            var trinityCarrier = MakeRuleCarrier("三相仪典", RuleAuraComponents.ElementConversion, 2);
            VerifySuite.Assert(GameActions.PlayCard(icore, i1, trinityCarrier, null, Zone.Hand, 0, out var rejectI6),
                $"三相载体打出（拒绝原因：{rejectI6 ?? "无"}）");
            GameActions.DrainStack(icore);
            var iBank = icore.ElementPool.GetPool(i1).TempElements;
            int redB = iBank[ManaType.Red], blueB = iBank[ManaType.Blue], greenB = iBank[ManaType.Green];
            var redCostData = new CardData
            {
                ID = "V9I_RED3", CardName = "红费探针", Supertype = Cardtype.Creature, Power = 1, Life = 1,
                Cost = PosCost((ManaType.Red, 3)),
            };
            var redCostCard = IToHand(i1, redCostData);
            VerifySuite.Assert(GameActions.PlayCard(icore, i1, redCostCard), "红3 费探针打出（触发 ElementPoolPayEvent）");
            GameActions.DrainStack(icore);
            VerifySuite.Assert(iBank[ManaType.Red] == redB - 3 + 1
                              && iBank[ManaType.Blue] == blueB + 1
                              && iBank[ManaType.Green] == greenB + 1,
                $"三相兑换：付红3 → 红/蓝/绿各+1（红 {redB}→{iBank[ManaType.Red]}，蓝 {blueB}→{iBank[ManaType.Blue]}，绿 {greenB}→{iBank[ManaType.Green]}）");

            // ---- ⑨ 血偿（2026-10-04 改造）：己方回合中，回合方角色受到的伤害改由对手角色承担 ----
            // （旧语义"生命代价转嫁"退役——代价流失 LifeLoss 不走伤害管线，光环前后照常自付）
            // 探针场上生物清场，保证 LifeLoss kinds{1} 自结算唯一候选=己方角色（确定性）
            var i1Battlefield = icore.ZoneManager.GetCards(i1, Zone.Battlefield).ToList();
            var icz1 = icore.ZoneManager.GetZoneContainer(i1);
            foreach (var b in i1Battlefield) icz1.Move(b, Zone.Battlefield, Zone.Graveyard);
            Card MakeBloodPayCard(string id)
            {
                var data = new CardData { ID = id, CardName = "生命支付探针", Supertype = Cardtype.Spell };
                data.Effects.Add(new CardEffectData
                {
                    Id = id + "_EFF",
                    TriggerTiming = (int)TriggerTiming.OnPlay,
                    SelectionMode = -1,
                    Costs = new List<CostEntry>
                    {
                        new CostEntry
                        {
                            CostType = 7, // Payload：付费步执行 payload 原子（E3D20BEC 同款先例）
                            Value = 1,
                            payload = new AtomicEffectEntry
                            {
                                refId = AtomicEffectTable.GetByType(AtomicEffectType.LifeLoss)?.HashId,
                                value = 2, kinds = new List<int> { 1 }, // 己方有生命单位（清场后=己方角色）
                            },
                        },
                    },
                });
                return IToHand(i1, data);
            }
            int i1MaxBefore = i1.MaxHealth, i2MaxBefore = i2.MaxHealth;
            var bp1 = MakeBloodPayCard("V9I_BP1");
            VerifySuite.Assert(GameActions.PlayCard(icore, i1, bp1), "生命支付探针打出（无血偿——对照）");
            GameActions.DrainStack(icore);
            VerifySuite.Assert(i1.MaxHealth == i1MaxBefore - 2 && i2.MaxHealth == i2MaxBefore,
                $"对照：代价流失自己付（i1 {i1MaxBefore}→{i1.MaxHealth}，i2 不变）");

            var bloodCarrier = MakeRuleCarrier("苦痛仪典", RuleAuraComponents.BloodPact, 2);
            VerifySuite.Assert(GameActions.PlayCard(icore, i1, bloodCarrier, null, Zone.Hand, 0, out var rejectI7),
                $"血偿载体打出（拒绝原因：{rejectI7 ?? "无"}）");
            GameActions.DrainStack(icore);
            // 回归锚：代价流失（LifeLoss 非伤害）不转嫁——改造后光环只改写 DamageEvent
            int i1MaxBefore2 = i1.MaxHealth, i2MaxBefore2 = i2.MaxHealth;
            var bp2 = MakeBloodPayCard("V9I_BP2");
            VerifySuite.Assert(GameActions.PlayCard(icore, i1, bp2), "生命支付探针打出（血偿生效下——流失口径）");
            GameActions.DrainStack(icore);
            VerifySuite.Assert(i1.MaxHealth == i1MaxBefore2 - 2 && i2.MaxHealth == i2MaxBefore2,
                "血偿不转嫁代价流失：LifeLoss 非伤害管线，照常自付（旧转嫁退役锚）");
            // 新语义：i1 回合中，i1 角色受伤 → i2 角色承担（任意来源，效果伤害直测）
            i1.Life = i1.MaxHealth; i2.Life = i2.MaxHealth; // 满血基线（此前流失扣过上限）
            int i1LifeBP = i1.Life, i2LifeBP = i2.Life;
            KeywordRules.ApplyDamage(i2, i1, 8, isCombat: false);
            VerifySuite.Assert(i1.Life == i1LifeBP && i2.Life == i2LifeBP - 8,
                $"血偿转移：己方回合角色受伤→对手承担（i1 {i1LifeBP}→{i1.Life}，i2 {i2LifeBP}→{i2.Life}）");
            // 对照：伤害落在非回合方角色 → 不转移
            int i2LifeBP2 = i2.Life;
            KeywordRules.ApplyDamage(i1, i2, 5, isCombat: false);
            VerifySuite.Assert(i2.Life == i2LifeBP2 - 5,
                $"对照：非回合方角色受伤不转移（i2 {i2LifeBP2}→{i2.Life}）");

            // ---- ⑩b 疾风（2026-10-16 改版）：受光环影响的=场上卡——攻击宣言/主动效果发动速度+1；
            //      手牌施放与守卫（自动响应）不加（旧「手牌中的卡+1」口径退役） ----
            // 2026-10-15 改版：法术施放速度=效果 BaseSpeed+卡型速度（3−槽数动态：1槽=+2）——
            // 速度探针1（1 效果）=1+2=3；无效果卡=0 速档
            var speedData1 = new CardData { ID = "V9I_SPEED1", CardName = "速度探针1", Supertype = Cardtype.Spell };
            speedData1.Effects.Add(new CardEffectData { Id = "V9I_SPEED1_EFF", BaseSpeed = 1 });
            var speedProbe = IToHand(i1, speedData1);
            var speedProbe0 = IToHand(i1, new CardData
            {
                ID = "V9I_SPEED0", CardName = "速度探针0", Supertype = Cardtype.Spell,
            }); // 无效果=0 速档
            // 场上探针：1 攻生物（CanDeclareAttack 力量门 GetPower>0；两次攻击各对 i2 角色
            // 结算 1 点战斗伤害——⑩b 后段无生命断言，V9.j 另起核心不受扰）+ 主动主动效果探针（BaseSpeed 0）
            var galeUnit = new CardWrapper(new CardData
            {
                ID = "V9I_GALE_U", CardName = "疾风探针", Supertype = Cardtype.Creature, Power = 1, Life = 5,
            });
            galeUnit.SetController(i1);
            VerifySuite.Assert(icore.ZoneManager.TryAddToBattlefield(galeUnit, i1), "疾风探针入场");
            galeUnit.Untap(); // 横置入场 → 解除（攻击资格）
            var galeActDef = new EffectDefinition
            {
                Id = "V9I_GALE_EFF",
                DisplayName = "疾风探针主动",
                ActivationType = EffectActivationType.Voluntary,
                BaseSpeed = 0,
            };
            // 攻击宣言速度观测：栈本体无私读口——StackAddEvent 捕获攻宣栈对象
            EffectInstance galeAtkObj = null;
            void OnGaleStackAdd(StackAddEvent e)
            {
                if (e.AddedObject is EffectInstance ei && ei.IsAttackDeclaration) galeAtkObj = ei;
            }
            EventManager.Instance.Subscribe<StackAddEvent>(OnGaleStackAdd);
            try
            {
                VerifySuite.Assert(GameActions.DeclareAttack(icore, i1, galeUnit, i2), "对照攻击宣言入栈（无疾风）");
                GameActions.DrainStack(icore);
            }
            finally { EventManager.Instance.Unsubscribe<StackAddEvent>(OnGaleStackAdd); }
            VerifySuite.Assert(SpeedCalculator.GetCardCastSpeed(speedProbe) == 3
                              && SpeedCalculator.GetCardCastSpeed(speedProbe0) == 0
                              && galeAtkObj != null && galeAtkObj.ActivationSpeed == 0
                              && PendingEffect.Create(galeActDef, galeUnit, i1, i1, PhaseType.Main).ActivationSpeed == 0,
                "对照（无疾风）：施放速度=效果速+卡型速（3/0）；场上攻击宣言与主动效果=基速0");
            var galeSpeedCarrier = MakeRuleCarrier("疾风仪典", RuleAuraComponents.CastSpeedUp, 2);
            VerifySuite.Assert(GameActions.PlayCard(icore, i1, galeSpeedCarrier, null, Zone.Hand, 0, out var rejectI8b),
                $"疾风载体打出（拒绝原因：{rejectI8b ?? "无"}）");
            GameActions.DrainStack(icore);
            galeUnit.Untap(); // 对照攻击结算已横置 → 解除再探
            galeAtkObj = null;
            EventManager.Instance.Subscribe<StackAddEvent>(OnGaleStackAdd);
            try
            {
                VerifySuite.Assert(GameActions.DeclareAttack(icore, i1, galeUnit, i2), "疾风攻击宣言入栈");
                GameActions.DrainStack(icore);
            }
            finally { EventManager.Instance.Unsubscribe<StackAddEvent>(OnGaleStackAdd); }
            VerifySuite.Assert(SpeedCalculator.GetCardCastSpeed(speedProbe) == 3
                              && SpeedCalculator.GetCardCastSpeed(speedProbe0) == 0,
                $"疾风：手牌施放不再+1（3速仍3、0速仍0；旧 4/1 锚退役；实际 {SpeedCalculator.GetCardCastSpeed(speedProbe)}/{SpeedCalculator.GetCardCastSpeed(speedProbe0)}）");
            VerifySuite.Assert(galeAtkObj != null && galeAtkObj.ActivationSpeed == 1
                              && PendingEffect.Create(galeActDef, galeUnit, i1, i1, PhaseType.Main).ActivationSpeed == 1
                              && StackEngine.GuardStackSpeed == 1,
                $"疾风：场上卡攻击宣言/主动效果发动速度+1（0→1；守卫恒1不加；实际攻宣 {galeAtkObj?.ActivationSpeed}）");

            // 缄默压规则光环载体（2026-10-19 规格定案：缄默光环本体才让光环无效——载体被缄默即失效）
            galeSpeedCarrier.AddCounters(CounterRules.NullifyCounter, 1, i2);
            VerifySuite.Assert(!RuleAuraSystem.IsActive(RuleAuraComponents.CastSpeedUp),
                "缄默压规则光环：载体被缄默 → IsActive 实时失效（Matches 载体闸）");
            galeSpeedCarrier.AddCounters(CounterRules.NullifyCounter, -1, i2);
            VerifySuite.Assert(RuleAuraSystem.IsActive(RuleAuraComponents.CastSpeedUp),
                "缄默解除：规则光环恢复");

            // ---- ⑩ 轮回（2026-10-04 承接原疾风）：每个玩家连续进行两个回合（AABB） ----
            var samsaraCarrier = MakeRuleCarrier("时光仪典", RuleAuraComponents.DoubleTurn, 2);
            VerifySuite.Assert(GameActions.PlayCard(icore, i1, samsaraCarrier, null, Zone.Hand, 0, out var rejectI8),
                $"轮回载体打出（拒绝原因：{rejectI8 ?? "无"}）");
            GameActions.DrainStack(icore);
            GameActions.EndTurn(icore, i1);
            icore.TurnEngine.CheckPhaseTransition();
            VerifySuite.Assert(icore.TurnEngine.TurnPlayer == i1, "轮回：i1 连续第 2 回合（AABB 第一跳）");
            GameActions.SkipElementPool(icore, i1);
            GameActions.EndTurn(icore, i1);
            icore.TurnEngine.CheckPhaseTransition();
            VerifySuite.Assert(icore.TurnEngine.TurnPlayer == i2, "轮回：轮到 i2（i1 两回合用尽）");
            GameActions.SkipElementPool(icore, i2);
            GameActions.EndTurn(icore, i2);
            icore.TurnEngine.CheckPhaseTransition();
            VerifySuite.Assert(icore.TurnEngine.TurnPlayer == i2, "轮回：i2 连续第 2 回合");
            GameActions.SkipElementPool(icore, i2);
            GameActions.EndTurn(icore, i2);
            icore.TurnEngine.CheckPhaseTransition();
            VerifySuite.Assert(icore.TurnEngine.TurnPlayer == i1, "轮回：回到 i1（AABB 交替成立）");
            GameActions.SkipElementPool(icore, i1);

            // ============================ V9.j 地牌资格（2026-10-03 用户定案：三型+三排除） ============================

            VerifySuite.Section("V9.j 地牌资格：生物/法术/结界可；衍生物/临时卡/已耗竭不可");
            var jcore = GameCore.Instance;
            jcore.Reset();
            ZoneContainer.Reseed(20261008);
            var fillerJ = Enumerable.Range(0, 8).Select(i => new CardData
            {
                ID = $"V9J_fill_{i}", CardName = "V9J填充" + i, Supertype = Cardtype.Creature, Power = 1, Life = 1,
                Cost = PosCost((ManaType.Gray, 1)),
            }).ToList();
            jcore.InitGame(CardLoader.BuildDeck(fillerJ, 2), CardLoader.BuildDeck(fillerJ, 2));
            var j1 = jcore.Player1;
            var j2 = jcore.Player2;
            GameActions.SkipElementPool(jcore, j1);

            Card JToHand(Player owner, CardData data)
            {
                var card = new CardWrapper(data);
                card.SetController(owner);
                jcore.ZoneManager.GetZoneContainer(owner).Add(card, Zone.Hand);
                return card;
            }

            // ---- 排除类（不占地牌槽） ----
            var tokenCard = new CardWrapper(fillerJ[0]) { ID = "V9J_TOKEN", IsToken = true };
            tokenCard.SetController(j1);
            jcore.ZoneManager.GetZoneContainer(j1).Add(tokenCard, Zone.Hand);
            VerifySuite.Assert(!jcore.ElementPool.AddCardToPool(tokenCard, j1), "衍生物不可作地牌（IsToken——2026-10-03 守卫补齐）");
            var tempCard = new CardWrapper(fillerJ[1]) { ID = "V9J_TEMP" };
            tempCard.SetController(j1);
            tempCard.AddCounters(CardCore.Attribute.CounterRules.TempCounter, 1); // 临时卡=临时指示物标记（2026-10-10 指示物化）
            jcore.ZoneManager.GetZoneContainer(j1).Add(tempCard, Zone.Hand);
            VerifySuite.Assert(!jcore.ElementPool.AddCardToPool(tempCard, j1), "临时卡不可作地牌（临时指示物——回归锚）");
            // 2026-10-11 锁色定案：耗竭标记 WasDepletedAsLand 退役（地牌不耗竭）；
            // 新排除类=无可锁颜色：0 费卡与纯黑白费卡（锁色候选空 → 拒绝入池）
            var zeroCostData = new CardData
            {
                ID = "V9J_ZERO", CardName = "零费排除", Supertype = Cardtype.Creature, Power = 1, Life = 1,
                Cost = new ElementCost(),
            };
            var zeroCostCard = new CardWrapper(zeroCostData);
            // ⚠ 双写（V9.l 同款陷阱）：构造器会把建议费快照进 IHasCost.Cost——须显式清零，否则
            // 锁色候选非空、0 费卡被放行（占掉回合 1 的唯一地牌格）
            ((CardCore.IHasCost)zeroCostCard).Cost = new ElementCost();
            zeroCostCard.SetController(j1);
            jcore.ZoneManager.GetZoneContainer(j1).Add(zeroCostCard, Zone.Hand);
            VerifySuite.Assert(!jcore.ElementPool.AddCardToPool(zeroCostCard, j1), "0 费卡不可作地牌（无锁色候选——2026-10-11 定案）");
            var bwOnlyCard = new CardWrapper(new CardData
            {
                ID = "V9J_BW", CardName = "纯黑白排除", Supertype = Cardtype.Creature, Power = 1, Life = 1,
                Cost = PosCost((ManaType.Black, 1), (ManaType.White, 1)),
            });
            bwOnlyCard.SetController(j1);
            jcore.ZoneManager.GetZoneContainer(j1).Add(bwOnlyCard, Zone.Hand);
            VerifySuite.Assert(!jcore.ElementPool.AddCardToPool(bwOnlyCard, j1), "纯黑白费卡不可作地牌（黑白不由地牌产出——回归锚）");

            // ---- 三型放行（地牌槽上限：回合1=1，推进两回合到 3） ----
            var creatureLand = JToHand(j1, fillerJ[3]);
            VerifySuite.Assert(jcore.ElementPool.AddCardToPool(creatureLand, j1), "生物可作地牌（回归锚）");
            GameActions.EndTurn(jcore, j1);
            jcore.TurnEngine.CheckPhaseTransition();
            GameActions.SkipElementPool(jcore, j2);
            GameActions.EndTurn(jcore, j2);
            jcore.TurnEngine.CheckPhaseTransition();
            GameActions.SkipElementPool(jcore, j1);

            var spellLand = JToHand(j1, new CardData
            {
                ID = "V9J_SPELL_LAND", CardName = "法术地", Supertype = Cardtype.Spell,
                Cost = PosCost((ManaType.Blue, 2)),
            });
            VerifySuite.Assert(jcore.ElementPool.AddCardToPool(spellLand, j1), "法术可作地牌（2026-10-03 新资格）");
            // 地牌槽上限按全局回合同步涨（2026-10-15：此刻全局 3=上限 3，已占 2）——再推进一轮余位更足
            GameActions.EndTurn(jcore, j1);
            jcore.TurnEngine.CheckPhaseTransition();
            GameActions.SkipElementPool(jcore, j2);
            GameActions.EndTurn(jcore, j2);
            jcore.TurnEngine.CheckPhaseTransition();
            GameActions.SkipElementPool(jcore, j1);
            var enchLand = JToHand(j1, new CardData
            {
                ID = "V9J_ENCH_LAND", CardName = "结界地", Supertype = Cardtype.Enchantment,
                Cost = PosCost((ManaType.Green, 2)), // 黑白不产指示物（既有定案）——用可产色
            });
            VerifySuite.Assert(jcore.ElementPool.AddCardToPool(enchLand, j1), "结界可作地牌（2026-10-03 新资格）");

            // ============================ V9.k 计价双方减半 + 错边收缩（2026-10-03 定案） ============================

            VerifySuite.Section("V9.k 双方减半计价（双侧+全取/多目标）+ 错边收缩（负面指己放行）");

            CardData KCard(string id, List<int> kinds, int selectionMode, AtomicEffectType atomType, int value = 2, int targetCount = -2)
            {
                var d = new CardData { ID = id, CardName = "V9K" + id, Supertype = Cardtype.Spell };
                d.Effects.Add(new CardEffectData
                {
                    Id = id + "_EFF",
                    TriggerTiming = (int)TriggerTiming.OnPlay,
                    SelectionMode = selectionMode,
                    TargetCount = targetCount,
                    AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(atomType, value: value, kinds: kinds) },
                });
                return d;
            }

            // ---- ① 双侧+全取 → ×0.5（2026-10-05 档位化后卡层曲线非线性，闭式期望退役——
            //      改半量锚同管线对照：己方全取 v4（×全部档3=12）对称减半=6 ≡ 己方全取 v2（2×3=6）） ----
            var healOwn = CardCostService.Derive(KCard("V9K_HEAL_OWN", new List<int> { 1 }, 2, AtomicEffectType.Heal, value: 4));
            var healBoth = CardCostService.Derive(KCard("V9K_HEAL_BOTH", new List<int> { 1, 2 }, 5, AtomicEffectType.Heal, value: 4));
            var healHalfRef = CardCostService.Derive(KCard("V9K_HEAL_HALF", new List<int> { 1 }, 2, AtomicEffectType.Heal, value: 2));
            VerifySuite.Assert(healBoth.DerivedTotal == healHalfRef.DerivedTotal,
                $"双方全体恢复减半：己方全取 {healOwn.DerivedTotal} → 双方全取 {healBoth.DerivedTotal}（期望=半量锚同管线 {healHalfRef.DerivedTotal}）");

            // ---- ② 双侧域单体任选一侧 → 不减（宽域选一≠同时作用双方）----
            // 2026-10-15 改版适配：旧对照卡用己方域（DealDamage 锁己=错边原子出计价转黑白获得，
            // 恒 0）——旧相等靠法术抵价 2 的巧合（2−2=0 ≡ 0）；改版抵价通道退役后改用敌侧单选
            // 做同价对照（任选一侧全额、无双方减半）
            var dmgPickOne = CardCostService.Derive(KCard("V9K_DMG_PICK1", new List<int> { 1, 2 }, 3, AtomicEffectType.DealDamage, targetCount: 1));
            var dmgFoe = CardCostService.Derive(KCard("V9K_DMG_FOE", new List<int> { 2 }, 3, AtomicEffectType.DealDamage, targetCount: 1));
            VerifySuite.Assert(dmgPickOne.DerivedTotal == dmgFoe.DerivedTotal && dmgFoe.DerivedTotal > 0,
                $"双侧域单体任选不減（{dmgPickOne.DerivedTotal} == 敌侧同款 {dmgFoe.DerivedTotal}，均全额无减半）");

            // ---- ③ 规则光环：空域不乘期望 N + 恒视为双方 ×0.5（2026-10-04 光环改造后锚价全动态读表） ----
            var capRow = AtomicEffectTable.GetAll().FirstOrDefault(r => r != null && r.DisplayName == "离散仪典");
            VerifySuite.Assert(capRow != null, "离散仪典表行存在（计价锚前置）");
            var auraData = new CardData { ID = "V9K_AURA", CardName = "V9K规则光环", Supertype = Cardtype.Enchantment };
            auraData.Effects.Add(new CardEffectData
            {
                Id = "V9K_AURA_EFF",
                TriggerTiming = (int)TriggerTiming.OnPlay,
                SelectionMode = -1,
                AtomicEffects = new List<AtomicEffectEntry>
                {
                    new AtomicEffectEntry { refId = capRow?.HashId, value = 1, str = RuleAuraComponents.DamageCap },
                },
            });
            var auraCost = CardCostService.Derive(auraData);
            // 整卡链：锚×0.5（恒双方）× f(未声明档=d(MaxTier=18)=0.75) 取整——结界底盘剩余 2 点
            // **不抵卡价**（2026-10-10 可支配底盘：结界剩余走运行时二向=主动发动费抵扣/耐久+N），
            // 无退费项（旧 −2 已废）
            int expectedAuraTotal = (int)Math.Round((capRow?.TotalUnitCost ?? 0f) * 0.5f * 0.75f,
                MidpointRounding.AwayFromZero);
            VerifySuite.Assert(auraCost.DerivedTotal == expectedAuraTotal,
                $"规则光环整卡计价=round(锚×0.5×0.75)——结界无抵价项（实际 {auraCost.DerivedTotal}，期望 {expectedAuraTotal}）");
            // 无底盘干扰的纯原子口径（RewardDerivedCost：Once/单目标 shim）：
            // 行身份（RowHashId→离散行锚）+ 空域不乘期望 N + ModifyGameRule 恒双方 → round(锚×0.5)
            var auraAtomCost = CostDerivationService.RewardDerivedCost(new List<AtomicEffectInstance>
            {
                new AtomicEffectInstance { Type = AtomicEffectType.ModifyGameRule, Value = 1, StringValue = "DamageCap",
                    TargetKinds = new List<int>(), Polarity = 0f, RowHashId = capRow?.HashId },
            });
            VerifySuite.Assert(Math.Abs(auraAtomCost - (int)Math.Round((capRow?.TotalUnitCost ?? 0f) * 0.5f, MidpointRounding.AwayFromZero)) < 0.01f,
                $"规则光环纯原子价=round(锚{capRow?.TotalUnitCost ?? 0f}×0.5)（实际 {auraAtomCost}——按行取锚/空域不乘期望 4/恒双方；锚价动态读表）");
            // 行身份回归锚：同枚举不同行各自计价（苦痛=BloodPact 锚12 vs 离散锚8——2026-10-07 用户调价
            // 离散红6→8；对照行 12 锚；末行回落口径不再串行）
            var bloodRow = AtomicEffectTable.GetAll().FirstOrDefault(r => r != null && r.DisplayName == "苦痛仪典");
            var bloodAtomCost = CostDerivationService.RewardDerivedCost(new List<AtomicEffectInstance>
            {
                new AtomicEffectInstance { Type = AtomicEffectType.ModifyGameRule, Value = 1, StringValue = "BloodPact",
                    TargetKinds = new List<int>(), Polarity = 0f, RowHashId = bloodRow?.HashId },
            });
            VerifySuite.Assert(Math.Abs(bloodAtomCost - (int)Math.Round((bloodRow?.TotalUnitCost ?? 0f) * 0.5f, MidpointRounding.AwayFromZero)) < 0.01f
                               && Math.Abs(bloodAtomCost - auraAtomCost) > 0.01f,
                $"变体行各自计价：苦痛锚{bloodRow?.TotalUnitCost ?? 0f}×0.5（实际 {bloodAtomCost}≠离散 {auraAtomCost}——RowHashId 行身份生效；锚价动态读表）");

            // ---- ④ 错边收缩：有害锁己方（负面指向自己）效果栏放行；有益锁对方仍剔除 ----
            var selfHarmDef = CardEffectConverter.ConvertOne(new CardEffectData
            {
                Id = "V9K_SELFHARM_EFF",
                TriggerTiming = (int)TriggerTiming.OnPlay,
                SelectionMode = -1,
                AtomicEffects = new List<AtomicEffectEntry>
                {
                    // 剧毒原子已删（2026-10-08 转 GrantVenom 关键词）——有害锁己方样本改攻击力减少（p=-1 同口径）
                    AtomRefs.New(AtomicEffectType.AddPowerDown, value: 1, kinds: new List<int> { 1 }), // p=-1 己方锁
                },
            }, "V9K");
            VerifySuite.Assert(selfHarmDef.Effects.Any(a => a.Type == AtomicEffectType.AddPowerDown),
                "错边收缩：负面效果指向自己（有害锁己方域）在效果栏放行");
            var oppBenefitDef = CardEffectConverter.ConvertOne(new CardEffectData
            {
                Id = "V9K_OPPBENEFIT_EFF",
                TriggerTiming = (int)TriggerTiming.OnPlay,
                SelectionMode = -1,
                AtomicEffects = new List<AtomicEffectEntry>
                {
                    AtomRefs.New(AtomicEffectType.Heal, value: 1, kinds: new List<int> { 2 }), // 治疗 p=+1 对方锁
                },
            }, "V9K");
            VerifySuite.Assert(!oppBenefitDef.Effects.Any(a => a.Type == AtomicEffectType.Heal),
                "错边保留：对对手有益（有益锁对方域）仍只能进代价栏（效果栏剔除）");
            VerifySuite.Assert(CardEffectConverter.ConvertPayloadForDisplay(
                    AtomRefs.New(AtomicEffectType.Heal, value: 1, kinds: new List<int> { 2 })) != null,
                "代价栏 allowWrongSide 不变（错边限制完整保留在代价上）");

            // ============================ V9.l 端到端四则（2026-10-03 用户定案配套） ============================

            VerifySuite.Section("V9.l 衍生物/临时卡×地牌 + 灰不足苏醒 + 归土回响乒乓配额");
            var lcore = GameCore.Instance;
            lcore.Reset();
            ZoneContainer.Reseed(20261009);
            var fillerL = Enumerable.Range(0, 8).Select(i => new CardData
            {
                ID = $"V9L_fill_{i}", CardName = "V9L填充" + i, Supertype = Cardtype.Creature, Power = 1, Life = 1,
            }).ToList();
            lcore.InitGame(CardLoader.BuildDeck(fillerL, 2), CardLoader.BuildDeck(fillerL, 2));
            var l1 = lcore.Player1;
            var l2 = lcore.Player2;
            GameActions.SkipElementPool(lcore, l1);
            var lbank = lcore.ElementPool.GetPool(l1).TempElements;
            foreach (ManaType t in Enum.GetValues(typeof(ManaType))) lbank[t] = 99;
            // 注：本段基线不抬 GlobalTurnIndex（浓度上限 1）——③ 灰不足段依赖余额不足拒付
            //（2026-10-16 钱包豁免后「超上限即拒」闸退役，③ 段自清其余钱包色保证灰缺口无垫）；
            // 2026-10-15 改版：法术账单=卡面费+所选效果锚价——①② 段先抬上限放行锚价，
            // ③ 段前还原基线（index 0=上限 1）。① 召唤术的无底盘适配改走单卡显式声明费（见下）。
            lcore.ElementPool.GetPool(l1).GlobalTurnIndex = 9;

            Card LToHand(Player owner, CardData data)
            {
                var card = new CardWrapper(data);
                card.SetController(owner);
                lcore.ZoneManager.GetZoneContainer(owner).Add(card, Zone.Hand);
                return card;
            }
            Card LSpell(string id, AtomicEffectType type, List<int> kinds = null, string str = null, int value = 1)
            {
                var data = new CardData { ID = id, CardName = "V9L" + id, Supertype = Cardtype.Spell };
                data.Effects.Add(new CardEffectData
                {
                    Id = id + "_EFF",
                    TriggerTiming = (int)TriggerTiming.OnPlay,
                    SelectionMode = -1,
                    AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(type, value: value, kinds: kinds, str: str) },
                });
                return LToHand(l1, data);
            }

            // ---- ① 衍生物：真实召唤 → 弹回手牌 → 不能作地牌 ----
            var tokenTpl = new CardData
            {
                ID = "V9L_TOKEN_TPL", CardName = "V9L衍生物模板", Supertype = Cardtype.Creature, Power = 2, Life = 2,
            };
            var prevResolver = CardCore.Attribute.Handlers.SummonTokenHandler.ResolveTemplate;
            CardCore.Attribute.Handlers.SummonTokenHandler.ResolveTemplate = id => id == tokenTpl.ID ? tokenTpl : null;
            Card bouncedToken = null; // ④ 段 EndTurn 后断言其随遗忘出局（2026-10-12 挂遗忘改稿）
            try
            {
                var summonCard = LSpell("V9L_SUMMON", AtomicEffectType.SummonToken, str: tokenTpl.ID);
                // 落区显式声明（合成器三档惯例——缺省 0=Zone.Hand 是已知陷阱，见 CostDerivation 落区注释）
                (summonCard as CardWrapper).GetData().Effects[0].SummonDropZone = (int)Zone.Battlefield;
                // 无底盘适配（2026-10-10）：衍生物内容全价后建议费 2 > 浓度上限 1（本段保持上限 1——
                // ③ 灰不足段依赖原始基线）——显式声明 1 灰锚定支付。⚠ 须双写：CardWrapper 构造器
                // 已把建议费快照进 IHasCost.Cost（GetCardCost 读包装器），data.Cost 只改一份不生效
                var summonData = (summonCard as CardWrapper).GetData();
                summonData.Cost = ElementCost.FromValue(ManaType.Gray, 1);
                ((CardCore.IHasCost)summonCard).Cost = summonData.Cost;
                VerifySuite.Assert(GameActions.PlayCard(lcore, l1, summonCard, null, Zone.Hand, 0, out var rejectSummon),
                    $"召唤术打出（SummonToken 真路径；拒绝原因：{rejectSummon ?? "无"}）");
                GameActions.DrainStack(lcore);
                var summoned = lcore.ZoneManager.GetCards(l1, Zone.Battlefield)
                    .FirstOrDefault(c => c.IsToken && c.ID != null && c.ID.StartsWith(tokenTpl.ID + "#"));
                VerifySuite.Assert(summoned != null, "衍生物入场且带 IsToken 标记");
                VerifySuite.Assert(summoned.GetCounterCount(CardCore.Attribute.CounterRules.TempCounter) == 1,
                    "衍生物入场即挂 1 层遗忘（2026-10-12 表行文案兑现——推翻完全生物口径）");
                var bounceCard = LSpell("V9L_BOUNCE", AtomicEffectType.ReturnToHand);
                VerifySuite.Assert(GameActions.PlayCard(lcore, l1, bounceCard,
                    new List<Entity> { summoned }, Zone.Hand, 0), "弹回术打出（预选目标=衍生物）");
                GameActions.DrainStack(lcore);
                VerifySuite.Assert(summoned.GetZone() == Zone.Hand, "衍生物被弹回手牌");
                VerifySuite.Assert(!lcore.ElementPool.AddCardToPool(summoned, l1),
                    "弹回手牌的衍生物不能作为地牌（IsToken——2026-10-03 守卫）");
                bouncedToken = summoned;
            }
            finally
            {
                CardCore.Attribute.Handlers.SummonTokenHandler.ResolveTemplate = prevResolver;
            }

            // ---- ② 临时卡真路径（回响效果复制）→ 不能作地牌（微缩/放大已删 2026-10-10，段随删） ----
            // 2026-10-07 回响改版：印刷关键词→EchoCopy 普通效果（结算期复制，发动无效=无复制）
            var echoData = new CardData
            {
                ID = "V9L_ECHO", CardName = "V9L回响术", Supertype = Cardtype.Spell,
            };
            echoData.Effects.Add(new CardEffectData
            {
                Id = "V9L_ECHO_EFF",
                TriggerTiming = (int)TriggerTiming.OnPlay,
                SelectionMode = -1,
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.EchoCopy, value: 1) },
            });
            var echoCard = LToHand(l1, echoData);
            VerifySuite.Assert(GameActions.PlayCard(lcore, l1, echoCard, null, Zone.Hand, 0, out var rejectEcho),
                $"回响术打出（拒绝原因：{rejectEcho ?? "无"}）");
            GameActions.DrainStack(lcore);
            var echoCopy = lcore.ZoneManager.GetCards(l1, Zone.Hand)
                .FirstOrDefault(c => c.GetCounterCount(CardCore.Attribute.CounterRules.TempCounter) > 0
                                     && c.ID != null && c.ID.StartsWith("V9L_ECHO#t"));
            VerifySuite.Assert(echoCopy != null, "回响结算期获得临时复制（EchoCopy 普通效果真路径）");
            VerifySuite.Assert(!lcore.ElementPool.AddCardToPool(echoCopy, l1), "回响临时卡不能作为地牌");

            // ---- ③ 灰不足：未立约用不出（死循环根源锚）→ 立约后成功使用 ----
            // 2026-10-15 改版适配：①② 已抬上限放行法术锚价——此处还原基线（上限 1）。
            // 2026-10-16 钱包豁免适配：旧「4 费卡超上限1被拒」total 闸退役——改钉**真余额不足**：
            // 灰链可由黑白红蓝绿垫（清空其余钱包色，只留绿 3——绿先付绿费 1、余 2 垫不齐灰 3 → 拒付）
            lcore.ElementPool.GetPool(l1).GlobalTurnIndex = 0;
            foreach (ManaType t in Enum.GetValues(typeof(ManaType))) lbank[t] = 0;
            lbank[ManaType.Green] = 3;
            var awakenData = new CardData
            {
                ID = "V9L_AWAKEN", CardName = "V9L苏醒者", Supertype = Cardtype.Creature, Power = 3, Life = 3,
                Cost = PosCost((ManaType.Gray, 3), (ManaType.Green, 1)),
            };
            awakenData.Effects.Add(new CardEffectData
            {
                Id = "V9L_AWAKEN_EFF",
                TriggerTiming = (int)TriggerTiming.OnPlay,
                SelectionMode = -1,
                AtomicEffects = new List<AtomicEffectEntry>
                {
                    AtomRefs.New(AtomicEffectType.Sleep, value: 0, kinds: new List<int> { 0 }),
                },
            });
            var awakenCard = LToHand(l1, awakenData);
            VerifySuite.Assert(!GameActions.PlayCard(lcore, l1, awakenCard, null, Zone.Hand, 0, out var rejectL3)
                              && rejectL3 != null,
                $"灰不足未立约：无法使用（{rejectL3}——旧「使用时生效」死循环根源锚）");
            VerifySuite.Assert(GameActions.CommitAwaken(lcore, l1, awakenCard, out var rejectL4),
                $"手牌立约（拒绝原因：{rejectL4 ?? "无"}）");
            VerifySuite.Assert(GameActions.PlayCard(lcore, l1, awakenCard),
                "灰不足立约后：成功使用（灰份全免转时长，1 绿 ≤ 浓度上限）");
            GameActions.DrainStack(lcore);
            VerifySuite.Assert(lbank[ManaType.Gray] == 0 && awakenCard.GetCounterCount(KeywordRules.SleepCounter) == 3,
                $"立约结算：灰仍为 0、沉睡 3 层（实际层 {awakenCard.GetCounterCount(KeywordRules.SleepCounter)}）");

            // ---- ④ 临时指示物化（2026-10-10 回响改版；2026-10-12 生效自减改稿）：复制体带 1 层；
            //      回合末不在战场→从游戏中移除、在战场→不生效不倒数（层保留，转正退役） ----
            var tempRow = AtomicEffectTable.GetAll()
                .FirstOrDefault(r => r != null && r.EnumName == nameof(AtomicEffectType.GrantTemporary));
            VerifySuite.Assert(tempRow != null && tempRow.TotalUnitCost == 2f && tempRow.PrimaryColor == ManaType.White,
                "临时指示物表行存在（白2 锚，极性-1/生效自减型）");
            lbank[ManaType.Gray] = 99; // ③ 灰耗尽剧本后复位，④ 出牌不受限
            lcore.ElementPool.GetPool(l1).GlobalTurnIndex = 9; // 浓度上限放开（同 V9.i/m 手法）
            var gtHost = new CardWrapper(new CardData
            {
                ID = "V9L_GT_HOST", CardName = "临时宿主", Supertype = Cardtype.Creature, Power = 2, Life = 2,
            });
            gtHost.SetController(l1);
            VerifySuite.Assert(lcore.ZoneManager.TryAddToBattlefield(gtHost, l1), "临时宿主入场");
            var grantTempCard = LSpell("V9L_GRANT_TEMP", AtomicEffectType.GrantTemporary, kinds: new List<int> { 1 });
            VerifySuite.Assert(GameActions.PlayCard(lcore, l1, grantTempCard, new List<Entity> { gtHost }, Zone.Hand, 0),
                "临时赋予术打出（目标=场上生物——GrantTemporary 真路径）");
            GameActions.DrainStack(lcore);
            VerifySuite.Assert(gtHost.GetCounterCount(CardCore.Attribute.CounterRules.TempCounter) == 1,
                "场上生物挂 1 层临时指示物（GrantTemporaryHandler）");
            VerifySuite.Assert(echoCopy != null
                              && echoCopy.GetCounterCount(CardCore.Attribute.CounterRules.TempCounter) == 1,
                "回响复制体带 1 层临时指示物（IsTemporary 布尔退役的替代锚）");
            GameActions.EndTurn(lcore, l1);
            lcore.TurnEngine.CheckPhaseTransition();
            VerifySuite.Assert(echoCopy != null && echoCopy.GetZone() == Zone.None
                              && !lcore.ZoneManager.GetCards(l1, Zone.Hand).Contains(echoCopy)
                              && !lcore.ZoneManager.GetCards(l1, Zone.Graveyard).Contains(echoCopy)
                              && !lcore.ZoneManager.GetCards(l1, Zone.Deck).Contains(echoCopy),
                "回合末：手牌临时复制从游戏中移除（不进墓、不洗牌库——CounterRules.OnTurnEnd ②例程）");
            VerifySuite.Assert(bouncedToken != null && bouncedToken.GetZone() == Zone.None
                              && !lcore.ZoneManager.GetCards(l1, Zone.Hand).Contains(bouncedToken)
                              && !lcore.ZoneManager.GetCards(l1, Zone.Graveyard).Contains(bouncedToken),
                "回合末：弹回手牌的衍生物随遗忘出局（挂标离场即出局——同 ② 例程，不滞留墓地）");
            VerifySuite.Assert(gtHost.GetZone() == Zone.Battlefield
                              && gtHost.GetCounterCount(CardCore.Attribute.CounterRules.TempCounter) == 1,
                "回合末：在战场不生效不倒数（生效自减——层保留、生物留场，转正退役）");

            // ============================ V9.m 归土×回响×唯一槽×墓地配额（压力剧本） ============================

            VerifySuite.Section("V9.m 归土回响：同名禁止/墓地使用/回响连锁/配额不刷新（2026-10-07 唯一性改版）");
            var mcore = GameCore.Instance;
            mcore.Reset();
            ZoneContainer.Reseed(20261010);
            var fillerM = Enumerable.Range(0, 8).Select(i => new CardData
            {
                ID = $"V9M_fill_{i}", CardName = "V9M填充" + i, Supertype = Cardtype.Creature, Power = 1, Life = 1,
            }).ToList();
            mcore.InitGame(CardLoader.BuildDeck(fillerM, 2), CardLoader.BuildDeck(fillerM, 2));
            var m1 = mcore.Player1;
            GameActions.SkipElementPool(mcore, m1);
            foreach (ManaType t in Enum.GetValues(typeof(ManaType)))
                mcore.ElementPool.GetPool(m1).TempElements[t] = 99;
            mcore.ElementPool.GetPool(m1).GlobalTurnIndex = 9; // 浓度上限放开（2026-10-04 调价适配，同 V9.i）

            var graveRow = AtomicEffectTable.GetAll().FirstOrDefault(r => r != null && r.DisplayName == "归土仪典");
            VerifySuite.Assert(graveRow != null, "归土仪典表行存在（前置）");
            CardData MakeEchoAura(string id)
            {
                var d = new CardData
                {
                    ID = id, CardName = "V9M回响归土", Supertype = Cardtype.Enchantment, Durability = 9,
                    // 显式声明费（2026-10-10 可支配底盘适配，同 V9.i 载体；2026-10-13 缩盘）：结界无抵价后建议费 >7 超浓度上限
                    Cost = ElementCost.FromValue(ManaType.Gray, 7),
                };
                d.Effects.Add(new CardEffectData
                {
                    Id = id + "_EFF",
                    TriggerTiming = (int)TriggerTiming.OnPlay,
                    SelectionMode = -1,
                    AtomicEffects = new List<AtomicEffectEntry>
                    {
                        new AtomicEffectEntry { refId = graveRow?.HashId, value = 1, str = RuleAuraComponents.GraveyardPlay },
                        // 回响随复制连锁（2026-10-07 改版：EchoCopy 普通效果——复制带效果栏）
                        AtomRefs.New(AtomicEffectType.EchoCopy, value: 1),
                    },
                });
                return d;
            }
            Card MToHand(CardData data)
            {
                var card = new CardWrapper(data);
                card.SetController(m1);
                mcore.ZoneManager.GetZoneContainer(m1).Add(card, Zone.Hand);
                return card;
            }
            List<Card> MTemps()
                => mcore.ZoneManager.GetCards(m1, Zone.Hand)
                    .Where(c => c.GetCounterCount(CardCore.Attribute.CounterRules.TempCounter) > 0).ToList();

            // ---- ① 原版归土入场（宣言即回响复制 temp①）----
            var auraOriginal = MToHand(MakeEchoAura("V9M_GUI"));
            VerifySuite.Assert(GameActions.PlayCard(mcore, m1, auraOriginal), "原版归土打出");
            GameActions.DrainStack(mcore);
            VerifySuite.Assert(auraOriginal.GetZone() == Zone.Battlefield
                              && RuleAuraSystem.IsActive(RuleAuraComponents.GraveyardPlay)
                              && ReferenceEquals(RuleAuraSystem.CarrierOf(RuleAuraComponents.GraveyardPlay), auraOriginal),
                "原版归土入场并激活规则光环（载体=原版）");
            VerifySuite.Assert(MTemps().Count == 1, $"回响结算复制 ×1（实际 {MTemps().Count}——EchoCopy 效果真路径）");

            // ---- ② 同名禁止（2026-10-07 唯一性改版）：归土在场 → 临时归土①打出被拒 ----
            var tempGui1 = MTemps()[0];
            VerifySuite.Assert(!GameActions.PlayCard(mcore, m1, tempGui1, null, Zone.Hand, 0, out var rejectM0)
                              && rejectM0 != null,
                $"同名禁止：临时归土①被拒（{rejectM0}——同名在场不可再打出）");
            VerifySuite.Assert(ReferenceEquals(RuleAuraSystem.CarrierOf(RuleAuraComponents.GraveyardPlay), auraOriginal)
                              && MTemps().Count == 1,
                "拒绝无副作用：载体不变、临时①仍在手");

            // ---- ③ 归土规则活体期间：墓地视手牌使用（配额第 1 次；墓地施放的回响卡也回响）----
            //（新模型下"载体死→规则死→墓地使用入口死"——同名仪式无法经墓地重打，剧本改用普通回响卡）
            Card MGraveResident(string id)
            {
                var data = new CardData
                {
                    ID = id, CardName = "V9M墓地回响生物", Supertype = Cardtype.Creature, Power = 1, Life = 1,
                };
                data.Effects.Add(new CardEffectData
                {
                    Id = id + "_EFF",
                    TriggerTiming = (int)TriggerTiming.OnPlay,
                    SelectionMode = -1,
                    AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.EchoCopy, value: 1) },
                });
                var c = new CardWrapper(data);
                c.SetController(m1);
                mcore.ZoneManager.GetZoneContainer(m1).Add(c, Zone.Graveyard);
                return c;
            }
            var graveResident = MGraveResident("V9M_RES");
            VerifySuite.Assert(GameActions.PlayCard(mcore, m1, graveResident, null, Zone.Graveyard, 0, out var rejectM1),
                $"墓地生物视手牌打出（拒绝原因：{rejectM1 ?? "无"}——配额第 1 次）");
            GameActions.DrainStack(mcore);
            VerifySuite.Assert(graveResident.GetZone() == Zone.Battlefield,
                "墓地生物成功入场（归土规则活体期间——CanUse 实时查询）");
            VerifySuite.Assert(MTemps().Count == 2, $"墓地施放也回响（归土临时①+生物复制；实际 {MTemps().Count}）");

            // ---- ④ 同名禁止（再验）：归土原版在场 → 临时归土②打出被拒 ----
            var tempGui2 = MTemps().FirstOrDefault(c => (c as CardWrapper)?.GetData()?.CardName == "V9M回响归土");
            string rejectM2a = null;
            VerifySuite.Assert(tempGui2 != null
                              && !GameActions.PlayCard(mcore, m1, tempGui2, null, Zone.Hand, 0, out rejectM2a)
                              && rejectM2a != null,
                $"同名禁止（再验）：归土临时②被拒（{rejectM2a}——同名在场不可再打出）");

            // ---- ⑤ 配额判定：本回合已用过墓地使用——再试拒绝 ----
            var graveResident2 = MGraveResident("V9M_RES2");
            VerifySuite.Assert(!GameActions.PlayCard(mcore, m1, graveResident2, null, Zone.Graveyard, 0, out var rejectM2)
                              && rejectM2 != null,
                $"每回合一次配额：第二次墓地使用被拒（{rejectM2}）");

            // ============================ V9.n 代价栏规则改造（2026-10-04 定案配套） ============================

            VerifySuite.Section("V9.n 代价栏：任意单向·镜像逆转·全类型累计生成钳·全价地牌门槛·槽位计费·无目标回手·先扣卡费不垫本卡（2026-10-16 恢复 10-04 旧序）");
            var pcore = GameCore.Instance;
            pcore.Reset();
            ZoneContainer.Reseed(20261004);
            var fillerN = Enumerable.Range(0, 8).Select(i => new CardData
            {
                ID = $"V9N_fill_{i}", CardName = "V9N填充" + i, Supertype = Cardtype.Creature, Power = 1, Life = 1,
            }).ToList();
            pcore.InitGame(CardLoader.BuildDeck(fillerN, 2), CardLoader.BuildDeck(fillerN, 2));
            var n1 = pcore.Player1;
            var n2 = pcore.Player2;
            GameActions.SkipElementPool(pcore, n1);
            var npool = pcore.ElementPool.GetPool(n1);
            foreach (ManaType t in Enum.GetValues(typeof(ManaType))) npool.TempElements[t] = 99;

            // ---- ① PayloadCostDomain 三路单元锚（放置口/候选过滤共用口） ----
            var attackRow = AtomicEffectTable.GetByType(AtomicEffectType.Attack);       // TK={2} 纯敌方 有害
            var untapRow = AtomicEffectTable.GetByType(AtomicEffectType.Untap);         // TK={1} 纯己方 有益
            var drawRow = AtomicEffectTable.GetByType(AtomicEffectType.DrawCard);      // ⑤ 段法术夹具主动效果行
            var lifeRow = AtomicEffectTable.GetByType(AtomicEffectType.LifeLoss);       // TK={1,2} 双侧 有害
            var dmgRow = AtomicEffectTable.GetByType(AtomicEffectType.DealDamage);      // TK={1,2,3,4} 双侧 有害
            var firstStrikeRow = AtomicEffectTable.GetByType(AtomicEffectType.GrantFirstStrike); // TK={0} 纯自身 有益
            var summonRow = AtomicEffectTable.GetByType(AtomicEffectType.SummonToken);  // 空域 有益
            var ruleRow = AtomicEffectTable.GetByType(AtomicEffectType.ModifyGameRule); // 空域 中性

            var (attackKinds, attackOk) = CostDerivationService.PayloadCostDomain(attackRow);
            VerifySuite.Assert(attackOk && attackKinds != null && attackKinds.Count == 1
                              && attackKinds[0] == (int)TargetKind.OwnLivingUnit,
                $"镜像逆转：纯敌方有害（攻击 {attackRow.HashId} 域{{2}}）→ 己方有生命单位 {{1}}（实际 [{string.Join(",", attackKinds ?? new List<int>())}]）");
            var (untapKinds, untapOk) = CostDerivationService.PayloadCostDomain(untapRow);
            VerifySuite.Assert(untapOk && untapKinds != null && untapKinds.Count == 1
                              && untapKinds[0] == (int)TargetKind.EnemyLivingUnit,
                $"镜像逆转：纯己方有益（激励 域{{1}}）→ 对方有生命单位 {{2}}（实际 [{string.Join(",", untapKinds ?? new List<int>())}]）");
            var (lifeKinds, lifeOk) = CostDerivationService.PayloadCostDomain(lifeRow);
            VerifySuite.Assert(lifeOk && lifeKinds != null && lifeKinds.Count == 1
                              && lifeKinds[0] == (int)TargetKind.OwnLivingUnit,
                "双侧域收窄（回归）：生命流失 {1,2} 有害 → 收窄到己方 {1}");
            var (dmgKinds, dmgOk) = CostDerivationService.PayloadCostDomain(dmgRow);
            VerifySuite.Assert(dmgOk && dmgKinds != null && dmgKinds.Count == 2
                              && dmgKinds.Contains(1) && dmgKinds.Contains(3),
                $"双侧域收窄：造成伤害 {{1,2,3,4}} 有害 → 己方侧成员 {{1,3}}（实际 [{string.Join(",", dmgKinds ?? new List<int>())}]）");
            VerifySuite.Assert(!CostDerivationService.PayloadCostDomain(firstStrikeRow).eligible,
                "防退化：纯 {Self} 有益域（先攻）无对侧可逆转——不可作代价");
            VerifySuite.Assert(!CostDerivationService.PayloadCostDomain(summonRow).eligible,
                "空域有益（召唤衍生物表行）无域可逆转——不可作代价（实例显式 kinds 指路不受影响）");
            VerifySuite.Assert(!CostDerivationService.PayloadCostDomain(ruleRow).eligible,
                "中性空域（规则光环）既非代价也非收益——不可作代价");

            // ---- ② 端到端镜像：激励（有益·纯己方域）逆转到对方侧——对方横置单位被解除 + 我方得白 ----
            var enemyUnit = new CardWrapper(new CardData
            {
                ID = "V9N_ENEMY", CardName = "V9n敌军单位", Supertype = Cardtype.Creature, Power = 2, Life = 2,
            });
            enemyUnit.SetController(n2);
            pcore.ZoneManager.GetZoneContainer(n2).Add(enemyUnit, Zone.Battlefield);
            enemyUnit.Tap();
            VerifySuite.Assert(enemyUnit.IsTapped(), "前置：对方单位已横置");

            npool.GlobalTurnIndex = 3; // 地牌上限 3
            npool.TempElements[ManaType.White] = 0;
            npool.WhiteGainedThisTurn = 0;
            var mirrorPayload = AtomRefs.New(AtomicEffectType.Untap, value: 1, kinds: untapKinds); // 合成器同口径：镜像域随条目落盘
            int untapGrant = CostDerivationService.PayloadUnitGrant(
                CardEffectConverter.ConvertPayloadForDisplay(mirrorPayload));
            VerifySuite.Assert(untapGrant >= 1, $"激励全价非零（实际 {untapGrant}——白补偿可观测前提）");
            var mirrorCardData = new CardData { ID = "V9N_MIRROR", CardName = "V9n镜像代价卡", Supertype = Cardtype.Spell };
            mirrorCardData.PayloadCost = new CostEntry
            {
                CostType = (int)CostType.Payload, Value = 1, payload = mirrorPayload,
            }; // 卡层 PayloadCost（2026-09-23 正式口——CollectCardSpecialCosts 直读）
            var mirrorCard = new CardWrapper(mirrorCardData);
            mirrorCard.SetController(n1);
            pcore.ZoneManager.GetZoneContainer(n1).Add(mirrorCard, Zone.Hand);
            VerifySuite.Assert(GameActions.PlayCard(pcore, n1, mirrorCard), "镜像代价卡打出（卡层 PayloadCost 付费步）");
            GameActions.DrainStack(pcore);
            VerifySuite.Assert(!enemyUnit.IsTapped(),
                "镜像执行：有益·纯己方域原子逆转到对方侧——对方横置单位被解除（代价=资敌；补偿后置：生效后发放）");
            VerifySuite.Assert(npool.TempElements[ManaType.White] == untapGrant
                              && npool.WhiteGainedThisTurn == untapGrant,
                $"镜像补偿：得白 {npool.TempElements[ManaType.White]} = 全价 {untapGrant} 全量入账（2026-10-04 本轮：补偿后置+产出不封，旧 Min(全价,封顶) 退役公式清理）");

            // ---- ③ 全价过地牌门槛（2026-10-04 本轮定案）：全价 > 地牌槽上限 → 整卡不可使用 ----
            npool.GlobalTurnIndex = 2; // 地牌上限 2 —— 全价必超
            npool.TempElements[ManaType.Black] = 0;
            npool.BlackGainedThisTurn = 0;
            var n1Battlefield = pcore.ZoneManager.GetCards(n1, Zone.Battlefield).ToList();
            var ncz1 = pcore.ZoneManager.GetZoneContainer(n1);
            foreach (var b in n1Battlefield) ncz1.Move(b, Zone.Battlefield, Zone.Graveyard); // 清场：kinds{1} 唯一候选=己方角色
            var (lifeKinds2, _) = CostDerivationService.PayloadCostDomain(lifeRow);
            var lifePayload = AtomRefs.New(AtomicEffectType.LifeLoss, value: 8, kinds: lifeKinds2);
            int lifeGrant = CostDerivationService.PayloadUnitGrant(
                CardEffectConverter.ConvertPayloadForDisplay(lifePayload));
            VerifySuite.Assert(lifeGrant > 2, $"前置：生命流失全价 > 2（实际 {lifeGrant}——门槛拒绝可观测）");
            int n1MaxBefore = n1.MaxHealth;
            var drainCardData = new CardData { ID = "V9N_DRAIN", CardName = "V9n大额代价卡", Supertype = Cardtype.Spell };
            drainCardData.PayloadCost = new CostEntry { CostType = (int)CostType.Payload, Value = 1, payload = lifePayload };
            var drainCard = new CardWrapper(drainCardData);
            drainCard.SetController(n1);
            pcore.ZoneManager.GetZoneContainer(n1).Add(drainCard, Zone.Hand);
            VerifySuite.Assert(!GameActions.PlayCard(pcore, n1, drainCard, null, Zone.Hand, 0, out var gateReject)
                              && gateReject != null && gateReject.Contains("地牌槽上限")
                              && pcore.ZoneManager.GetCards(n1, Zone.Hand).Contains(drainCard),
                $"全价门槛：全价 {lifeGrant} > 上限 2 → 整卡拒发留手（实际原因「{gateReject}」）");
            VerifySuite.Assert(n1.MaxHealth == n1MaxBefore && npool.TempElements[ManaType.Black] == 0,
                "全价门槛拦截：走不到代价发动（上限未流失、无黑补偿）");

            // ---- ③b 门槛放行 + 代价恒执行 + 补偿后置全额 ----
            var smallPayload = AtomRefs.New(AtomicEffectType.LifeLoss, value: 1, kinds: lifeKinds2);
            int smallGrant = CostDerivationService.PayloadUnitGrant(
                CardEffectConverter.ConvertPayloadForDisplay(smallPayload));
            VerifySuite.Assert(smallGrant >= 1 && smallGrant <= 9,
                $"前置：小额代价全价 1..9（实际 {smallGrant}——门槛可在 cap≤9 内放行）");
            npool.GlobalTurnIndex = smallGrant; // cap = 全价（门槛恰好过；smallGrant≤9 未触自然封顶 10）
            npool.BlackGainedThisTurn = 0; npool.WhiteGainedThisTurn = 0; // 新回合模拟（真实局回合推进清台账——隔离②的本回合白获得）
            npool.TempGainedThisTurn = 0; // 全类型累计钳配额重置（②已产白 1 会挤占本段配额——同周期模拟清零）
            int n1MaxBefore2 = n1.MaxHealth;
            var smallCardData = new CardData { ID = "V9N_SMALL", CardName = "V9n小额代价卡", Supertype = Cardtype.Spell };
            smallCardData.PayloadCost = new CostEntry { CostType = (int)CostType.Payload, Value = 1, payload = smallPayload };
            var smallCard = new CardWrapper(smallCardData);
            smallCard.SetController(n1);
            pcore.ZoneManager.GetZoneContainer(n1).Add(smallCard, Zone.Hand);
            VerifySuite.Assert(GameActions.PlayCard(pcore, n1, smallCard), "门槛放行：全价 ≤ 地牌槽上限 → 可打出");
            GameActions.DrainStack(pcore);
            VerifySuite.Assert(n1.MaxHealth == n1MaxBefore2 - 1, $"代价恒执行：己方角色上限 -1（{n1MaxBefore2}→{n1.MaxHealth}）");
            VerifySuite.Assert(npool.TempElements[ManaType.Black] == smallGrant && npool.BlackGainedThisTurn == smallGrant,
                $"补偿全额：生效后立即得黑 {npool.TempElements[ManaType.Black]} = 全价 {smallGrant}（快照在执行前取）");

            // ---- ④ 全类型累计生成钳直测（2026-10-16 改案：六色同池，单周期累计 ≤ 地牌上限——
            //      接替当日上午「黑白单次钳」）+ 使用侧约束 ----
            var tpool = pcore.ElementPool.GetPool(n2);
            tpool.GlobalTurnIndex = 2;
            tpool.BlackGainedThisTurn = 0; tpool.WhiteGainedThisTurn = 0;
            tpool.TempGainedThisTurn = 0;
            tpool.TempElements[ManaType.Black] = 0; tpool.TempElements[ManaType.White] = 0;
            VerifySuite.Assert(pcore.ElementPool.AddMana(n2, ManaType.Black, null, 3) == 2
                              && tpool.TempElements[ManaType.Black] == 2 && tpool.BlackGainedThisTurn == 2
                              && tpool.TempGainedThisTurn == 2,
                "首笔钳制：cap2 时单次入黑 3 → 实发 2（配额 2/2——单次即受剩余配额钳）");
            VerifySuite.Assert(pcore.ElementPool.AddMana(n2, ManaType.Black, null, 1) == 0
                              && tpool.TempElements[ManaType.Black] == 2 && tpool.TempGainedThisTurn == 2,
                "累计钳：配额已满 → 再入黑 1 实发 0（全类型累计 ≤ 上限——同回合多次生成不再开放）");
            VerifySuite.Assert(pcore.ElementPool.AddMana(n2, ManaType.White, null, 5) == 0
                              && tpool.TempElements[ManaType.White] == 0,
                "六色同池：黑已占满配额 → 白 5 实发 0（旧「黑白各自单次钳/不跨色」退役）");
            pcore.ElementPool.OnTurnStart(n2, 3);
            VerifySuite.Assert(tpool.TempGainedThisTurn == 0
                              && tpool.BlackGainedThisTurn == 0 && tpool.WhiteGainedThisTurn == 0,
                "自己回合开始：全类型累计台账清零（周期锚点=自己回合开始，跨对方回合保留；黑白分色观测台账同清）");
            tpool.GlobalTurnIndex = 2; // OnTurnStart 已推到 3——钉回 2 保持本段 cap 语义
            VerifySuite.Assert(pcore.ElementPool.AddMana(n2, ManaType.White, null, 5) == 2
                              && tpool.TempElements[ManaType.White] == 2 && tpool.WhiteGainedThisTurn == 2
                              && tpool.TempGainedThisTurn == 2,
                "新周期白 5 · cap2 → 实发 2（配额重计；钱包仅回合末清）");
            tpool.GlobalTurnIndex = 10; // 自然上限到顶 10（2026-10-15：曲线封顶 10）
            VerifySuite.Assert(pcore.ElementPool.AddMana(n2, ManaType.Black, null, 15) == 8
                              && tpool.TempElements[ManaType.Black] == 2 + 8 && tpool.BlackGainedThisTurn == 8
                              && tpool.TempGainedThisTurn == 10,
                "配额内封顶：黑 15 · 剩余配额 8（cap10−白2，全类型同池共算）→ 实发 8");
            // 使用侧（支付）2026-10-16 临时元素超上限改版：钱包贡献不受浓度钳——
            // bank 黑 10 · cap2，黑3 需求照付（获取钳制不拦支付；旧「单次支付贡献 ≤ 地牌上限」退役）
            tpool.GlobalTurnIndex = 2;
            VerifySuite.Assert(ElementPaymentValidator.GetBillPaymentPlan(
                                  new Dictionary<ManaType, int> { { ManaType.Black, 3 } },
                                  tpool.TempElements) != null,
                "钱包豁免：bank 黑 10 · cap2 → 黑3 需求可付（生成钳不拦支付——临时元素超上限直付）");
            VerifySuite.Assert(ElementPaymentValidator.GetBillPaymentPlan(
                                  new Dictionary<ManaType, int> { { ManaType.Black, 99 } },
                                  tpool.TempElements) == null,
                "余额照拒：黑99 > bank 10 → 拒付（浓度钳退役后由余额定）");

            // ---- ⑤ 持久化读回：卡层 payload 字段 → CardData.PayloadCost（旧档无字段回落 null） ----
            // 2026-10-15 改版适配：法术须挂 1~3 个主动效果——夹具各挂 1 条主动 DrawCard
            //（锚价蓝2 只影响施放账单，不影响本段 payload/槽位断言）
            var persistJson = "{\"cards\":[{\"id\":\"V9N_PERSIST\",\"cardName\":\"V9n持久化代价卡\",\"supertype\":\"Spell\","
                + "\"power\":0,\"life\":0,\"costList\":[1,0,0,0,0,0],"
                + "\"effectIds\":[],\"effects\":[{\"id\":\"V9N_P_EFF\",\"timing\":19,\"activation\":2,"
                + "\"steps\":[{\"kind\":0,\"atom\":{\"refId\":\"" + drawRow?.HashId + "\",\"value\":1}}]}],"
                + "\"payload\":{\"refId\":\"" + untapRow.HashId + "\",\"value\":1,\"kinds\":[2]}},"
                + "{\"id\":\"V9N_LEGACY\",\"cardName\":\"V9n旧档卡\",\"supertype\":\"Spell\",\"power\":0,\"life\":0,"
                + "\"effects\":[{\"id\":\"V9N_L_EFF\",\"timing\":19,\"activation\":2,"
                + "\"steps\":[{\"kind\":0,\"atom\":{\"refId\":\"" + drawRow?.HashId + "\",\"value\":1}}]}]}],"
                + "\"deckConfig\":{\"copiesPerCard\":3}}";
            var loadedCards = CardLoader.LoadCardsFromText(persistJson);
            var persistCard = loadedCards.FirstOrDefault(c => c.ID == "V9N_PERSIST");
            var legacyCard = loadedCards.FirstOrDefault(c => c.ID == "V9N_LEGACY");
            VerifySuite.Assert(persistCard?.PayloadCost != null
                              && persistCard.PayloadCost.CostType == (int)CostType.Payload
                              && persistCard.PayloadCost.payload?.refId == untapRow.HashId
                              && persistCard.PayloadCost.payload?.kinds?.Count == 1
                              && persistCard.PayloadCost.payload.kinds[0] == (int)TargetKind.EnemyLivingUnit,
                "持久化读回：Cards.json payload 字段 → 卡层 PayloadCost（镜像域随条目还原）");
            VerifySuite.Assert(legacyCard != null && legacyCard.PayloadCost == null,
                "旧档兼容：无 payload 字段 → PayloadCost=null（legacy 效果级 Costs 兜底不受影响）");

            // ---- ⑥ 代价栏计效果槽（2026-10-04 本轮定案：代价也是效果栏，不是特殊判——占 1 槽走底盘预算） ----
            var slotCardData = new CardData { ID = "V9N_SLOT", CardName = "V9n槽位卡", Supertype = Cardtype.Creature, Power = 1, Life = 1 };
            slotCardData.Effects.Add(new CardEffectData
            {
                Id = "V9N_SLOT_E1", TriggerTiming = (int)TriggerTiming.OnPlay,
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DrawCard, value: 1) },
            });
            VerifySuite.Assert(CostDerivationService.CountEffectSlots(slotCardData) == 1, "无代价：槽位=效果数（1）");
            slotCardData.PayloadCost = new CostEntry { CostType = (int)CostType.Payload, Value = 1, payload = smallPayload };
            VerifySuite.Assert(CostDerivationService.CountEffectSlots(slotCardData) == 2,
                "代价栏计 1 效果槽（1 效果 + 1 代价 = 2——使用代价栏单独为效果栏付 1 费）");
            VerifySuite.Assert(CardCompositionCost.StructuralSurcharge(slotCardData) == 2,
                "底盘同口径：生物效果槽全加价（效果1+代价栏1=2槽 → +2 灰；三分额已归属性/攻/守，2026-10-15 分配定案）");
            VerifySuite.Assert(CardCompositionCost.SurplusOf(slotCardData) == 1,
                "免费点不随槽位减少：生物恒 属性1（与槽加价对冲后首槽净 0，总额同旧口径）");
            var legacySlotData = new CardData { ID = "V9N_SLOT2", CardName = "V9n旧式槽位卡", Supertype = Cardtype.Spell };
            legacySlotData.Effects.Add(new CardEffectData
            {
                Id = "V9N_SLOT2_E1", TriggerTiming = (int)TriggerTiming.OnPlay,
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DrawCard, value: 1) },
                Costs = new List<CostEntry> { new CostEntry { CostType = (int)CostType.Payload, Value = 1, payload = smallPayload } },
            });
            VerifySuite.Assert(CostDerivationService.CountEffectSlots(legacySlotData) == 2,
                "legacy 效果级 Costs 代价同计 1 槽（1 效果 + 1 代价 = 2）");

            // ---- ⑦ 无有效目标回手（2026-10-04 本轮定案：响应窗口内目标全灭 → 发动失败回退手牌） ----
            npool.GlobalTurnIndex = 9; // cap 放宽（门槛只看全价）
            npool.TempElements[ManaType.White] = 0;
            npool.WhiteGainedThisTurn = 0;
            npool.TempElements[ManaType.Gray] = 1; // 恰好够默认灰1——回退则分文未扣
            enemyUnit.Tap(); // 激励域=NoRole,Tapped——重横置使声明期有候选
            var revertCardData = new CardData { ID = "V9N_REVERT", CardName = "V9n回退代价卡", Supertype = Cardtype.Spell };
            revertCardData.Cost = ElementCost.FromValue(ManaType.Gray, 1); // 有费可扣——回退则分文未扣可观测
            revertCardData.PayloadCost = new CostEntry { CostType = (int)CostType.Payload, Value = 1, payload = mirrorPayload };
            var revertCard = new CardWrapper(revertCardData);
            revertCard.SetController(n1);
            pcore.ZoneManager.GetZoneContainer(n1).Add(revertCard, Zone.Hand);
            VerifySuite.Assert(GameActions.PlayCard(pcore, n1, revertCard), "前置：预选目标在场——声明成功");
            pcore.ZoneManager.GetZoneContainer(n2).Move(enemyUnit, Zone.Battlefield, Zone.Graveyard); // 响应窗口内目标离场
            GameActions.DrainStack(pcore);
            VerifySuite.Assert(pcore.ZoneManager.GetCards(n1, Zone.Hand).Contains(revertCard),
                "代价无有效目标：发动失败回退手牌（走不到代价黑白元素生成）");
            VerifySuite.Assert(npool.TempElements[ManaType.Gray] == 1 && npool.TempElements[ManaType.White] == 0,
                $"回退不付费不补偿（灰仍 {npool.TempElements[ManaType.Gray]}、白仍 {npool.TempElements[ManaType.White]}）");

            // ---- ⑦b 手牌域代价回手（域正确性锚：{5} 域目标在手牌，写死战场判区会误杀） ----
            var discPayload = AtomRefs.New(AtomicEffectType.DiscardCard, value: 1, kinds: new List<int> { 5 });
            var backCardData = new CardData { ID = "V9N_BACK", CardName = "V9n弃牌代价卡", Supertype = Cardtype.Spell };
            backCardData.Cost = ElementCost.FromValue(ManaType.Gray, 1);
            backCardData.PayloadCost = new CostEntry { CostType = (int)CostType.Payload, Value = 1, payload = discPayload };
            var backCard = new CardWrapper(backCardData);
            backCard.SetController(n1);
            pcore.ZoneManager.GetZoneContainer(n1).Add(backCard, Zone.Hand);
            var handSave7b = pcore.ZoneManager.GetCards(n1, Zone.Hand).Where(h => h != backCard).ToList();
            VerifySuite.Assert(handSave7b.Count >= 1 && GameActions.PlayCard(pcore, n1, backCard),
                "前置：手牌有弃牌目标——声明成功");
            foreach (var h in handSave7b) pcore.ZoneManager.GetZoneContainer(n1).Move(h, Zone.Hand, Zone.Deck); // 窗口内排空其余手牌
            GameActions.DrainStack(pcore);
            VerifySuite.Assert(pcore.ZoneManager.GetCards(n1, Zone.Hand).Contains(backCard)
                              && npool.TempElements[ManaType.Gray] == 1,
                "手牌域代价无有效目标：发动失败回退手牌（非战场域按域重解析，不误杀；灰未扣）");
            foreach (var h in handSave7b) pcore.ZoneManager.GetZoneContainer(n1).Move(h, Zone.Deck, Zone.Hand); // 还原

            // ---- ⑧ 先扣卡费·支付失败（2026-10-16 恢复 10-04 旧序——10-11「补偿先付垫本卡」回调退役）：
            //      窗口内池恶化 → 卡费付不出入墓不回卷，代价未执行、无补偿入账 ----
            var orderCardData = new CardData { ID = "V9N_ORDER", CardName = "V9n顺序代价卡", Supertype = Cardtype.Spell };
            orderCardData.Cost = ElementCost.FromValue(ManaType.Black, 3);
            orderCardData.PayloadCost = new CostEntry { CostType = (int)CostType.Payload, Value = 1, payload = smallPayload };
            var orderCard = new CardWrapper(orderCardData);
            orderCard.SetController(n1);
            pcore.ZoneManager.GetZoneContainer(n1).Add(orderCard, Zone.Hand);
            npool.BlackGainedThisTurn = 0; npool.WhiteGainedThisTurn = 0; // 新回合模拟：台账清零隔离观测
            npool.TempGainedThisTurn = 0; // 全类型累计钳配额重置（同周期模拟）
            npool.TempElements[ManaType.Black] = 3; // 声明期池够（现有黑3 足额——黑白本色费黑3 只黑付）
            int n1MaxBefore3 = n1.MaxHealth;
            VerifySuite.Assert(GameActions.PlayCard(pcore, n1, orderCard), "前置：黑3 池够——声明成功");
            npool.TempElements[ManaType.Black] = 0; // 响应窗口内元素被耗尽
            GameActions.DrainStack(pcore);
            VerifySuite.Assert(pcore.ZoneManager.GetCards(n1, Zone.Graveyard).Contains(orderCard),
                "先扣卡费：池恶化付不出 → 入墓不回卷（代价未执行——失败点在代价前）");
            VerifySuite.Assert(n1.MaxHealth == n1MaxBefore3
                              && npool.TempElements[ManaType.Black] == 0,
                $"代价后置：上限不变、无黑补偿入账（补偿须本卡发动成功、代价强制执行完成后才到账）");

            // ---- ⑨ 声明期不预计算代价垫色（2026-10-16 恢复旧序——垫色 seed 退役）：池差=补偿量 → 拒发留手 ----
            var affordCardData = new CardData { ID = "V9N_AFFORD", CardName = "V9n余量代价卡", Supertype = Cardtype.Spell };
            affordCardData.Cost = ElementCost.FromValue(ManaType.Black, 3);
            affordCardData.PayloadCost = new CostEntry { CostType = (int)CostType.Payload, Value = 1, payload = smallPayload };
            var affordCard = new CardWrapper(affordCardData);
            affordCard.SetController(n1);
            pcore.ZoneManager.GetZoneContainer(n1).Add(affordCard, Zone.Hand);
            npool.BlackGainedThisTurn = 0; npool.WhiteGainedThisTurn = 0; // 新回合模拟
            npool.TempGainedThisTurn = 0; // 全类型累计钳配额重置（同周期模拟）
            npool.TempElements[ManaType.Black] = 3 - smallGrant; // 池差恰好=将生成的黑——垫色后可付
            int n1MaxBefore4 = n1.MaxHealth;
            VerifySuite.Assert(!GameActions.PlayCard(pcore, n1, affordCard),
                $"声明期口径：将生成的黑（{smallGrant}）不预计算垫本卡——池余 {3 - smallGrant} < 卡费黑3 → 拒发");
            VerifySuite.Assert(pcore.ZoneManager.GetCards(n1, Zone.Hand).Contains(affordCard)
                              && n1.MaxHealth == n1MaxBefore4
                              && npool.TempElements[ManaType.Black] == 3 - smallGrant,
                "拒发留手：不付费、代价未执行、池余不动（补偿黑/白须发动成功后才到账）");

            // ---- ⑩ 生命恢复目标须受伤（2026-10-04 本轮定案：Damaged 动态判定 hp<上限，无存储标志位） ----
            var healCfg = AtomicEffectTable.GetByType(AtomicEffectType.Heal);
            VerifySuite.Assert(healCfg != null && healCfg.TargetFilter == "Damaged",
                "表行单源：回复生命 TargetFilter=Damaged（复用既有 DamagedFilter，动态判定）");
            var hfull = new CardWrapper(new CardData { ID = "V9N_HFULL", CardName = "V9n满血单位", Supertype = Cardtype.Creature, Power = 1, Life = 3 });
            var hhurt = new CardWrapper(new CardData { ID = "V9N_HHURT", CardName = "V9n受伤单位", Supertype = Cardtype.Creature, Power = 1, Life = 3 });
            hfull.SetController(n1);
            hhurt.SetController(n1);
            pcore.ZoneManager.GetZoneContainer(n1).Add(hfull, Zone.Battlefield);
            pcore.ZoneManager.GetZoneContainer(n1).Add(hhurt, Zone.Battlefield);
            KeywordRules.ApplyDamage(n2, hhurt, 2, isCombat: false);
            KeywordRules.ApplyDamage(n2, n1, 2, isCombat: false); // 角色掉血（动态判定对角色同口径）
            var healCtx = new EffectExecutionContext
            {
                Controller = n1, Source = n1, ZoneManager = pcore.ZoneManager, ElementPool = pcore.ElementPool,
            };
            var healCandidates = global::CardCore.Attribute.EffectHandlerRegistry.ResolveCandidates(
                healCfg.GetTargetKindList(), healCfg.TargetFilter, healCtx);
            VerifySuite.Assert(healCandidates != null && healCandidates.Contains(hhurt) && !healCandidates.Contains(hfull),
                $"Damaged 过滤：受伤单位在候选、满血单位不在（候选数 {healCandidates?.Count ?? -1}）");
            VerifySuite.Assert(healCandidates != null && healCandidates.Contains(n1),
                "Damaged 过滤：掉血角色在候选（hp<上限 动态判定，无受伤标志位）");
            var healCardData = new CardData { ID = "V9N_HEALCARD", CardName = "V9n治疗卡", Supertype = Cardtype.Spell };
            healCardData.Effects.Add(new CardEffectData
            {
                Id = "V9N_HEALCARD_E1", TriggerTiming = (int)TriggerTiming.OnPlay,
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.Heal, value: 2, kinds: new List<int> { 1, 2 }) },
            });
            var healCard = new CardWrapper(healCardData);
            healCard.SetController(n1);
            pcore.ZoneManager.GetZoneContainer(n1).Add(healCard, Zone.Hand);
            int hhurtLifeBefore = hhurt.GetLife();
            VerifySuite.Assert(GameActions.PlayCard(pcore, n1, healCard, new List<Entity> { hhurt }),
                "治疗卡可发动（存在受伤目标）");
            GameActions.DrainStack(pcore);
            VerifySuite.Assert(hhurt.GetLife() == Math.Min(3, hhurtLifeBefore + 2),
                $"治疗结算：受伤单位回复（{hhurtLifeBefore}→{hhurt.GetLife()}，封顶 3）");

            // ============================ V9.o 两层无效（2026-10-04 定案：发动无效=净零成本 / 效果无效=扣费照付） ============================

            VerifySuite.Section("V9.o 两层无效：发动无效净零成本 / 效果无效扣费照付（施放·触发计数·强制桶面）");
            var qcore = GameCore.Instance;
            qcore.Reset();
            ZoneContainer.Reseed(20261005);
            var fillerO = Enumerable.Range(0, 8).Select(i => new CardData
            {
                ID = $"V9O_fill_{i}", CardName = "V9O填充" + i, Supertype = Cardtype.Creature, Power = 1, Life = 1,
            }).ToList();
            qcore.InitGame(CardLoader.BuildDeck(fillerO, 2), CardLoader.BuildDeck(fillerO, 2));
            var o1 = qcore.Player1;
            var o2 = qcore.Player2;
            GameActions.SkipElementPool(qcore, o1);
            var opool = qcore.ElementPool.GetPool(o1);
            var opool2 = qcore.ElementPool.GetPool(o2);
            foreach (ManaType t in Enum.GetValues(typeof(ManaType)))
            {
                opool.TempElements[t] = 99;
                opool2.TempElements[t] = 99;
            }
            opool.GlobalTurnIndex = 9;
            opool2.GlobalTurnIndex = 9;

            var naRow = AtomicEffectTable.GetByEnumName("NegateActivation"); // 表行「发动无效」（净零成本档）
            var neRow = AtomicEffectTable.GetByEnumName("NegateEffect");     // 表行「效果无效」（扣费照付档）
            VerifySuite.Assert(naRow != null && neRow != null && naRow.HashId != neRow.HashId,
                "两层无效表行齐备（发动无效/效果无效——行 ID 各异）");

            Card OSpell(string id, string name, int baseSpeed, CardCore.Attribute.AtomicEffectConfig atomRow, Player owner,
                List<int> kinds = null)
            {
                var data = new CardData { ID = id, CardName = name, Supertype = Cardtype.Spell };
                data.Cost = PosCost((ManaType.Gray, 1));
                data.Effects.Add(new CardEffectData
                {
                    Id = id + "_EFF",
                    TriggerTiming = (int)TriggerTiming.OnPlay,
                    BaseSpeed = baseSpeed,
                    SelectionMode = -1,
                    AtomicEffects = new List<AtomicEffectEntry>
                    {
                        // 逐原子目标制（2026-10-09）：否定类指向发动区 {15,16}（响应窗口指向性——预选归属段保活）；
                        // 火球类改真实战斗域 {1,2}（旧 {15,16} 是「预选+None 早真从不解析候选」的旧模型残留，
                        // 严格逐原子预检下无候选不可发动）
                        new AtomicEffectEntry { refId = atomRow.HashId, value = 1, kinds = kinds ?? new List<int> { 15, 16 } },
                    },
                });
                var card = new CardWrapper(data);
                card.SetController(owner);
                qcore.ZoneManager.GetZoneContainer(owner).Add(card, Zone.Hand);
                return card;
            }

            // ---- ① 施放×发动无效：扣费返还（不付费）、送墓、无效果 ----
            var ddRowO = AtomicEffectTable.GetByType(AtomicEffectType.DealDamage);
            var fbA = OSpell("V9O_FB_A", "V9o火球A", 0, ddRowO, o1, new List<int> { 1, 2 });
            var naCard = OSpell("V9O_NA", "V9o发动无效", 2, naRow, o2);
            int o2LifeA = o2.Life, o1GrayA = opool.TempElements[ManaType.Gray];
            VerifySuite.Assert(GameActions.PlayCard(qcore, o1, fbA, new List<Entity> { o2 }), "① 火球A打出（响应窗口开）");
            VerifySuite.Assert(GameActions.PlayCardInResponse(qcore, o2, naCard, new List<Entity> { fbA }),
                "① 响应窗口内打出发动无效（指向发动区火球）");
            GameActions.DrainStack(qcore);
            if (!qcore.StackEngine.IsEmpty) qcore.StackEngine.Clear(); // 段内 SBA 窗口收口（Unity 侧同款残留惯例）
            VerifySuite.Assert(o2.Life == o2LifeA
                              && opool.TempElements[ManaType.Gray] == o1GrayA
                              && qcore.ZoneManager.GetCards(o1, Zone.Graveyard).Contains(fbA),
                $"① 发动无效=净零成本：无伤害、不付费（净效果=扣费返还）、卡入墓（life {o2LifeA}→{o2.Life}，灰 {o1GrayA}→{opool.TempElements[ManaType.Gray]}）");

            // ---- ② 施放×效果无效：扣费不返还、送墓、无效果 ----
            var fbB = OSpell("V9O_FB_B", "V9o火球B", 0, ddRowO, o1, new List<int> { 1, 2 });
            var neCard = OSpell("V9O_NE", "V9o效果无效", 2, neRow, o2);
            int o1GrayB = opool.TempElements[ManaType.Gray];
            VerifySuite.Assert(GameActions.PlayCard(qcore, o1, fbB, new List<Entity> { o2 }), "② 火球B打出");
            VerifySuite.Assert(GameActions.PlayCardInResponse(qcore, o2, neCard, new List<Entity> { fbB }),
                "② 响应窗口内打出效果无效");
            GameActions.DrainStack(qcore);
            if (!qcore.StackEngine.IsEmpty) qcore.StackEngine.Clear();
            VerifySuite.Assert(o2.Life == o2LifeA
                              && opool.TempElements[ManaType.Gray] == o1GrayB - 1
                              && qcore.ZoneManager.GetCards(o1, Zone.Graveyard).Contains(fbB),
                $"② 效果无效=扣费照付：无伤害、费用实扣不返还（灰 {o1GrayB}→{opool.TempElements[ManaType.Gray]}）、卡入墓");

            // ---- ③ 触发式计数口径：发动无效不计发动次数（记账退还）/ 效果无效计入（记账保留） ----
            var executor = qcore.StackEngine.GetExecutor();
            var trigSrc = new CardWrapper(new CardData
            {
                ID = "V9O_TRIG_SRC", CardName = "V9o触发源", Supertype = Cardtype.Creature, Power = 1, Life = 1,
            });
            trigSrc.SetController(o1);
            qcore.ZoneManager.GetZoneContainer(o1).Add(trigSrc, Zone.Battlefield);
            var trigDef = CardEffectConverter.ConvertOne(new CardEffectData
            {
                Id = "V9O_TRIG",
                ActivationType = (int)EffectActivationType.Automatic,
                TriggerTiming = (int)TriggerTiming.OnTurnStart,
                TriggerLimitPerTurn = 1, // 一回合一次——计数口径的观察窗
                SelectionMode = -1,
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DealDamage, value: 1, kinds: new List<int> { 2 }) },
            }, "V9O_TRIG_SRC");
            PendingEffect TrigPending()
            {
                var p = PendingEffect.Create(trigDef, trigSrc, o1, o1, PhaseType.Main,
                    triggeringEvent: new TurnStartEvent { TurnPlayer = o1, TurnNumber = 99 });
                p.SelectedTargets = new List<Entity> { o2 };
                return p;
            }
            int o2LifeC = o2.Life;
            executor.RecordQueuedActivation(trigDef, trigSrc); // 触发式入栈记账（引擎口径）
            VerifySuite.Assert(executor.TriggerCapReached(trigDef, trigSrc), "③ 前置：记账后达一回合一次上限");
            var naInst = EffectInstance.FromPendingEffect(TrigPending());
            naInst.IsActivationNegated = true;
            executor.ExecuteAsync(naInst).GetAwaiter().GetResult();
            VerifySuite.Assert(!executor.TriggerCapReached(trigDef, trigSrc) && o2.Life == o2LifeC,
                "③ 发动无效：效果跳过 + 发动次数退还（上限解锁——同回合可再触发）");
            executor.RecordQueuedActivation(trigDef, trigSrc); // 退还后再触发（重新入栈记账）
            var okInst = EffectInstance.FromPendingEffect(TrigPending());
            executor.ExecuteAsync(okInst).GetAwaiter().GetResult();
            VerifySuite.Assert(executor.TriggerCapReached(trigDef, trigSrc) && o2.Life == o2LifeC - 1,
                "③ 退还后再触发：正常结算（伤害 1 落地）且计数回到上限");
            var neInst = EffectInstance.FromPendingEffect(TrigPending());
            neInst.IsEffectNegated = true;
            executor.ExecuteAsync(neInst).GetAwaiter().GetResult();
            VerifySuite.Assert(executor.TriggerCapReached(trigDef, trigSrc) && o2.Life == o2LifeC - 1,
                "③ 效果无效：效果不结算但**计入发动次数**（记账不退——上限仍锁）");

            // ---- ④ 强制桶面：发动无效不管制强制效果、效果无效管制（混合批直调处理器 + 排干结算） ----
            var mandSrc = new CardWrapper(new CardData
            {
                ID = "V9O_MAND_SRC", CardName = "V9o强制源", Supertype = Cardtype.Creature, Power = 1, Life = 1,
            });
            mandSrc.SetController(o1);
            qcore.ZoneManager.GetZoneContainer(o1).Add(mandSrc, Zone.Battlefield);
            EffectDefinition OTrigDef(string id, EffectActivationType bucket, int dmg)
                => CardEffectConverter.ConvertOne(new CardEffectData
                {
                    Id = id,
                    ActivationType = (int)bucket,
                    TriggerTiming = (int)TriggerTiming.OnTurnStart,
                    TriggerLimitPerTurn = -1, // 显式无限（缺省 0=默认 1 次/回合——④ 两轮入队会被上限闸门吞）
                    SelectionMode = -1,
                    AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DealDamage, value: dmg, kinds: new List<int> { 2 }) },
                }, "V9O_MAND_SRC");
            var autoDef = OTrigDef("V9O_AUTO", EffectActivationType.Automatic, 1);
            // 强制效果直构（角色亡语同款先例）：CardEffectData.ActivationType=0（Mandatory 枚举值）
            // 会被转换器当"未声明"回落默认桶——真实强制效果（光环类/宣判）均直构 EffectDefinition。
            var mandDef = new EffectDefinition
            {
                Id = "V9O_MAND",
                DisplayName = "V9o强制效果",
                ActivationType = EffectActivationType.Mandatory,
                TriggerTiming = TriggerTiming.OnTurnStart,
                ElementCostPrepaid = true,
                Effects = new List<AtomicEffectInstance>
                {
                    new AtomicEffectInstance { Type = AtomicEffectType.DealDamage, Value = 2, TargetKinds = new List<int> { 2 } },
                },
            };
            int o2LifeD = o2.Life;
            var autoP = PendingEffect.Create(autoDef, trigSrc, o1, o1, PhaseType.Main); // 自动桶（trigSrc 作源）
            autoP.SelectedTargets = new List<Entity> { o2 };
            qcore.StackEngine.AddPendingEffect(autoP);
            var mandP = PendingEffect.Create(mandDef, mandSrc, o1, o1, PhaseType.Main); // 强制桶（mandSrc 作源）
            mandP.SelectedTargets = new List<Entity> { o2 };
            qcore.StackEngine.AddPendingEffect(mandP);
            qcore.StackEngine.ProcessTriggeredEffects(); // 两桶入栈（含自动桶=开窗批）
            CardCore.Attribute.EffectHandlerRegistry.ExecuteEffectAsync(
                new AtomicEffectInstance { Type = AtomicEffectType.NegateActivation, Value = 1 },
                new EffectExecutionContext
                {
                    Controller = o2, Source = o2,
                    ZoneManager = qcore.ZoneManager, ElementPool = qcore.ElementPool,
                    Targets = new List<Entity> { mandSrc, trigSrc }, // 强制源+自动源同指：管制面分野可观测
                }).GetAwaiter().GetResult();
            GameActions.DrainStack(qcore);
            if (!qcore.StackEngine.IsEmpty) qcore.StackEngine.Clear();
            VerifySuite.Assert(o2.Life == o2LifeD - 2,
                $"④ 发动无效不管制强制桶：强制源效果照常结算（伤害 2 落地）、自动源被净零跳过（未加 1——实际 {o2LifeD}→{o2.Life}）");
            var mandP2 = PendingEffect.Create(mandDef, mandSrc, o1, o1, PhaseType.Main);
            mandP2.SelectedTargets = new List<Entity> { o2 };
            qcore.StackEngine.AddPendingEffect(mandP2);
            qcore.StackEngine.ProcessTriggeredEffects();
            CardCore.Attribute.EffectHandlerRegistry.ExecuteEffectAsync(
                new AtomicEffectInstance { Type = AtomicEffectType.NegateEffect, Value = 1 },
                new EffectExecutionContext
                {
                    Controller = o2, Source = o2,
                    ZoneManager = qcore.ZoneManager, ElementPool = qcore.ElementPool,
                    Targets = new List<Entity> { mandSrc },
                }).GetAwaiter().GetResult();
            GameActions.DrainStack(qcore);
            if (!qcore.StackEngine.IsEmpty) qcore.StackEngine.Clear();
            VerifySuite.Assert(o2.Life == o2LifeD - 2,
                "④ 效果无效管制强制桶：强制源效果被跳过（伤害 0 追加）");

            // ============================ V9.p 改版回归（2026-10-05：改写光环/赋予主干/诅咒有限分支；成长翻倍 2026-10-08 机制删除） ============================

            VerifySuite.Section("V9.p 三负面光环（毒/霜/眠）+舍身关键词（2026-10-16 关键词化）+赋予主干+诅咒 CurseOnDraw");
            var wcore = GameCore.Instance;
            wcore.Reset();
            ZoneContainer.Reseed(20261012);
            var fillerP = Enumerable.Range(0, 8).Select(i => new CardData
            {
                ID = $"V9P_fill_{i}", CardName = "V9P填充" + i, Supertype = Cardtype.Creature, Power = 1, Life = 1,
            }).ToList();
            wcore.InitGame(CardLoader.BuildDeck(fillerP, 2), CardLoader.BuildDeck(fillerP, 2));
            var w1 = wcore.Player1;
            var w2 = wcore.Player2;
            GameActions.SkipElementPool(wcore, w1);
            foreach (ManaType t in Enum.GetValues(typeof(ManaType)))
                wcore.ElementPool.GetPool(w1).TempElements[t] = 99;
            wcore.ElementPool.GetPool(w1).GlobalTurnIndex = 9;

            // ---- ① 目录锚：改写门四条已删、诅咒通道=引擎主干行、附加诅咒/祝福引擎主干化、
            //      有限分支（局面门）2026-10-09 还原——达标奖励/不达标无事 ----
            VerifySuite.Assert(ComposerCatalog.OutcomeConditions
                              .Concat(ComposerCatalog.SituationGates)
                              .All(g => !g.Id.StartsWith("DmgRewrite")),
                "改写门四条已从条件目录退役（迁唯一光环）");
            VerifySuite.Assert(!BranchConditionEvaluator.IsKnownCondition("DmgRewriteToxin")
                              && !BranchConditionEvaluator.IsKnownCondition("CurseOnDraw"),
                "改写条件 id 已注销、CurseOnDraw 不在评估器（诅咒通道=引擎主干行）");
            // 2026-10-08 引擎主干化：旧 AddCurse 指示物行退役（原子库不可达）；
            // 接棒=引擎主干行 EngineCurseOnDraw/EngineBlessingOnDraw（行级锚在 V9.q bEnginePairs，e2e 在 V9.h）
            VerifySuite.Assert(AtomicEffectTable.GetByType(AtomicEffectType.AddCurse) == null
                              && AtomicEffectTable.GetByType(AtomicEffectType.EngineCurseOnDraw) != null
                              && AtomicEffectTable.GetByType(AtomicEffectType.EngineBlessingOnDraw) != null,
                "附加诅咒自由分支化：旧 AddCurse 指示物行退役、引擎主干行 EngineCurseOnDraw/EngineBlessingOnDraw 入表（诅咒/祝福指示物归系统，无玩家表行）");
            VerifySuite.Assert(!CostDerivationService.GatePremium.ContainsKey("CurseOnDraw")
                              && CostDerivationService.GatePremium.Count == 16
                              && CostDerivationService.GatePremium["LifeBelowOpp"] == 1
                              && CostDerivationService.GatePremium["LifeLe7"] == 2
                              && CostDerivationService.GatePremium["DrawnInStandbyThisTurn"] == 3,
                "条件预算表=产出五项+局面门 11 项（有限分支还原；诅咒门不还原）");

            // ---- ② 三负面光环 + 舍身仪典（2026-10-07 负面化定案：原"战斗伤害改写为指示物"退役，
            // 改持续型挂层——代码侧事件钩子；改写管线收敛为舍身单映射：战斗伤害转投对手角色。
            // 疫蚀 2026-10-09 随剧毒指示物删除退役——表行/常量/钩子分支全清）----
            var rewriteRows = AtomicEffectTable.GetAll()
                .Where(r => r != null && r.DisplayName is "毒蚀仪典" or "霜蚀仪典" or "眠蚀仪典")
                .ToList();
            VerifySuite.Assert(rewriteRows.Count == 3
                              && rewriteRows.All(r => MountKindExtensions.ParseCsv(r.MountKinds).Contains(MountKind.RuleAura))
                              && rewriteRows.All(r => r.Polarity < 0),
                "三负面光环挂规则光环位（2026-10-07 负面化定案——极性负、范围缺省对方；疫蚀已删）");

            Card NegCarrier(string id)
            {
                var c = new CardWrapper(new CardData
                {
                    ID = id, CardName = id, Supertype = Cardtype.Enchantment, Durability = 9,
                });
                c.SetController(w1);
                wcore.ZoneManager.GetZoneContainer(w1).Add(c, Zone.Battlefield);
                return c;
            }

            var toxinCarrier = NegCarrier("V9P_TOXIN");
            // 范围=对方（负面族缺省）：受光环影响的生物=光环控制者对手（w2）一侧
            RuleAuraSystem.Activate(RuleAuraComponents.CombatToxin, toxinCarrier, w1, (int)RuleAuraScope.Opponent);
            VerifySuite.Assert(RuleAuraSystem.IsActive(RuleAuraComponents.CombatToxin),
                "毒蚀激活（直调 Activate=ModifyGameRuleHandler 同口）；范围=对方");

            var mineCr = new CardWrapper(new CardData
            {
                ID = "V9P_MINE", CardName = "V9P己方生物", Supertype = Cardtype.Creature, Power = 2, Life = 5,
            });
            mineCr.SetController(w1);
            wcore.ZoneManager.GetZoneContainer(w1).Add(mineCr, Zone.Battlefield);
            var foeCr = new CardWrapper(new CardData
            {
                ID = "V9P_FOE", CardName = "V9P对方生物", Supertype = Cardtype.Creature, Power = 2, Life = 20,
            });
            foeCr.SetController(w2);
            wcore.ZoneManager.GetZoneContainer(w2).Add(foeCr, Zone.Battlefield);

            // 毒蚀：对方生物受到伤害 → 叠毒素（伤害照常发生）；己方生物受击不叠（范围=对方）
            int foeLife0 = foeCr.GetLife();
            int dealtA = KeywordRules.ApplyDamage(mineCr, foeCr, 3, isCombat: false);
            VerifySuite.Assert(dealtA == 3 && foeCr.GetLife() == foeLife0 - 3
                              && foeCr.GetCounterCount(CounterRules.ToxinCounter) == 1,
                $"毒蚀：对方生物受击叠毒素×1（伤害照常；毒素层 {foeCr.GetCounterCount(CounterRules.ToxinCounter)}）");
            int mineLife0 = mineCr.GetLife();
            int dealtB = KeywordRules.ApplyDamage(foeCr, mineCr, 2, isCombat: false);
            VerifySuite.Assert(dealtB == 2 && mineCr.GetLife() == mineLife0 - 2
                              && mineCr.GetCounterCount(CounterRules.ToxinCounter) == 0,
                $"毒蚀范围过滤：己方生物受击不叠（实际伤害 {dealtB}，毒素层 {mineCr.GetCounterCount(CounterRules.ToxinCounter)}）");

            // 同名禁止（直投兜底 + 打出闸数据面）：毒蚀在场 → 同 str 原子的卡被拦、Activate 拒绝载体不变
            var toxinDupData = new CardData
            {
                ID = "V9P_TOXIN2", CardName = "V9P毒蚀重复", Supertype = Cardtype.Enchantment, Durability = 9,
            };
            toxinDupData.Effects.Add(new CardEffectData
            {
                Id = "V9P_TOXIN2_EFF", TriggerTiming = (int)TriggerTiming.OnPlay, SelectionMode = -1,
                AtomicEffects = new List<AtomicEffectEntry>
                {
                    new AtomicEffectEntry { refId = rewriteRows[0]?.HashId, value = 2, str = RuleAuraComponents.CombatToxin },
                },
            });
            var toxinDup = new CardWrapper(toxinDupData);
            toxinDup.SetController(w1);
            VerifySuite.Assert(RuleAuraSystem.BlocksDuplicatePlay(toxinDup),
                "同名仪典打出闸：毒蚀在场→含同 str 原子的卡命中拦截（异名/普通卡不拦）");
            var toxinSecond = NegCarrier("V9P_TOXIN_B");
            RuleAuraSystem.Activate(RuleAuraComponents.CombatToxin, toxinSecond, w1, (int)RuleAuraScope.Opponent);
            VerifySuite.Assert(ReferenceEquals(RuleAuraSystem.CarrierOf(RuleAuraComponents.CombatToxin), toxinCarrier),
                "同名 Activate 直调拒绝：载体不变（直投路径兜底——不送墓不换任）");

            // 霜蚀：攻击结算事件 → 对方生物冻结（横置+层）；己方生物攻击后不冻结
            RuleAuraSystem.Activate(RuleAuraComponents.CombatFreeze, NegCarrier("V9P_FREEZE"), w1, (int)RuleAuraScope.Opponent);
            EventManager.Instance.Publish(new AttackResolvedEvent { Attacker = foeCr, Target = mineCr, AttackingPlayer = w2 });
            VerifySuite.Assert(foeCr.IsTapped() && foeCr.GetCounterCount(KeywordRules.FreezeCounter) >= 1,
                "霜蚀：对方生物攻击后冻结（横置+冻结层）");
            bool mineTapped0 = mineCr.IsTapped();
            EventManager.Instance.Publish(new AttackResolvedEvent { Attacker = mineCr, Target = foeCr, AttackingPlayer = w1 });
            VerifySuite.Assert(mineCr.IsTapped() == mineTapped0 && mineCr.GetCounterCount(KeywordRules.FreezeCounter) == 0,
                "霜蚀范围过滤：己方生物攻击后不冻结");

            // 眠蚀：主动发动钩（EffectExecutionEngine 结算点同口）→ 对方生物叠沉睡；己方不叠
            RuleAuraSystem.Activate(RuleAuraComponents.CombatSleep, NegCarrier("V9P_SLEEP"), w1, (int)RuleAuraScope.Opponent);
            RuleAuraComponents.OnActivatedForSleepAura(foeCr);
            RuleAuraComponents.OnActivatedForSleepAura(mineCr);
            VerifySuite.Assert(foeCr.GetCounterCount(KeywordRules.SleepCounter) == 1
                              && mineCr.GetCounterCount(KeywordRules.SleepCounter) == 0,
                "眠蚀：主动发动后叠沉睡×1（范围=对方过滤——沉睡层拦截后续主动+跳重置）");

            // 舍身（2026-10-16 关键词化——原舍身仪典规则光环退役；守护的进攻向对偶）：
            // 表行=GrantMartyrdom 关键词挂载+拉黑连接光环；授予/登场即弹选转投目标（无头自动选首候选）
            // → 持有者的战斗伤害改写为对目标的**效果伤害**（非战斗路径：禁魔石可清零、不触发战斗反击链）
            VerifySuite.Assert(AtomicEffectTable.GetByType(AtomicEffectType.GrantMartyrdom) is { } martyrRow
                              && martyrRow.Polarity > 0
                              && MountKindExtensions.ParseCsv(martyrRow.MountKinds).Contains(MountKind.Keyword)
                              && !ComposerCatalog.IsAuraMountableKeyword(KeywordRules.Martyrdom)
                              && AtomicEffectTable.GetByEnumName("舍身仪典") == null,
                "舍身表行：GrantMartyrdom 关键词挂载+拉黑连接光环（原仪典行退役——与守护同款黑名单）");

            var w1Martyr = new CardWrapper(new CardData
            {
                ID = "V9P_MARTYR", CardName = "V9P舍身者", Supertype = Cardtype.Creature, Power = 3, Life = 9,
            });
            w1Martyr.SetController(w1);
            wcore.ZoneManager.GetZoneContainer(w1).Add(w1Martyr, Zone.Battlefield);
            var foeTarget = new CardWrapper(new CardData
            {
                ID = "V9P_FOET", CardName = "V9P对方靶子", Supertype = Cardtype.Creature, Power = 1, Life = 20,
            });
            foeTarget.SetController(w2);
            wcore.ZoneManager.GetZoneContainer(w2).Add(foeTarget, Zone.Battlefield);

            // 授予即选：候选=对方战场单位+对方角色，无头自动选首候选（foeCr 先入场 → victim=foeCr）
            w1Martyr.AddKeyword(KeywordRules.Martyrdom, KeywordLane.Setting, w1Martyr, 1, -1);
            MartyrdomRules.OnGrantedInPlay(w1Martyr);
            int foeLifeM0 = foeCr.GetLife(); int foeTLife0 = foeTarget.GetLife();
            int dealtM = KeywordRules.ApplyDamage(w1Martyr, foeTarget, 3, isCombat: true);
            VerifySuite.Assert(dealtM == 0 && foeTarget.GetLife() == foeTLife0 && foeCr.GetLife() == foeLifeM0 - 3,
                $"舍身：持有者战斗伤害转投选定目标（返 {dealtM}，原目标余 {foeTarget.GetLife()}，victim {foeLifeM0}→{foeCr.GetLife()}）");

            // 效果伤害铁证：victim 挂禁魔石（非战斗伤害归零）→ 改写量被清零（若按战斗伤害直付则不会为 0）
            foeCr.AddKeyword(KeywordRules.Spellban, KeywordLane.Setting, foeCr, 1, -1);
            int foeLifeM1 = foeCr.GetLife();
            int dealtM2 = KeywordRules.ApplyDamage(w1Martyr, foeTarget, 2, isCombat: true);
            VerifySuite.Assert(dealtM2 == 0 && foeTarget.GetLife() == foeTLife0 && foeCr.GetLife() == foeLifeM1,
                $"舍身改写=效果伤害：victim 禁魔石清零（返 {dealtM2}，victim 余 {foeCr.GetLife()}，原目标余 {foeTarget.GetLife()}）");

            // victim 离场断链（活性实时校验）→ 战斗伤害照常结算
            wcore.ZoneManager.MoveCard(foeCr, w2, Zone.Battlefield, Zone.Graveyard);
            int foeTLife1 = foeTarget.GetLife();
            int dealtM3 = KeywordRules.ApplyDamage(w1Martyr, foeTarget, 2, isCombat: true);
            VerifySuite.Assert(dealtM3 == 2 && foeTarget.GetLife() == foeTLife1 - 2,
                $"舍身断链：victim 移墓后战斗伤害照常（返 {dealtM3}，原目标余 {foeTarget.GetLife()}）");

            // 舍身对向：对方持有者候选=我方战场单位+我方角色（控制者相对域——镜像守护己方域）。
            // 先清 w1 侧结界载体（负面光环测试已毕——否则自动首候选=结界走耐久管线，断言面不净）
            foreach (var nc in wcore.ZoneManager.GetCards(w1, Zone.Battlefield)
                         .Where(c => c is IHasSupertype ht && ht.Supertype == Cardtype.Enchantment).ToList())
                wcore.ZoneManager.MoveCard(nc, w1, Zone.Battlefield, Zone.Graveyard);
            var foeMartyr = new CardWrapper(new CardData
            {
                ID = "V9P_FMARTYR", CardName = "V9P对方舍身者", Supertype = Cardtype.Creature, Power = 2, Life = 9,
            });
            foeMartyr.SetController(w2);
            wcore.ZoneManager.GetZoneContainer(w2).Add(foeMartyr, Zone.Battlefield);
            foeMartyr.AddKeyword(KeywordRules.Martyrdom, KeywordLane.Setting, foeMartyr, 1, -1);
            MartyrdomRules.OnGrantedInPlay(foeMartyr);
            int mineLifeM0 = mineCr.GetLife(); int w1MartLife0 = w1Martyr.GetLife();
            int dealtM4 = KeywordRules.ApplyDamage(foeMartyr, w1Martyr, 2, isCombat: true);
            VerifySuite.Assert(dealtM4 == 0 && w1Martyr.GetLife() == w1MartLife0 && mineCr.GetLife() == mineLifeM0 - 2,
                $"舍身对向：对方持有者战斗伤害转投我方首单位（返 {dealtM4}，原目标余 {w1Martyr.GetLife()}，victim {mineLifeM0}→{mineCr.GetLife()}）");

            // ==================== 缄默/沉默压制矩阵（2026-10-19 规格定案） ====================
            // 缄默（Nullify）=拦自动效果：本体关键词（HasKeyword 收口）+ 舍身/守护配对 + 光环来源/载体；
            // 沉默=拦主动（CanActivate 引擎既有口）——负例钉死沉默不压关键词/守护。
            // 触发式拦截（FindMatchingEffects）为 2026-09-09 既有闸，本段不重复驱动。
            // 前序夹具已把 mineCr 打到 1 血——先回满，防本段反例伤害将其打死级联后续断言
            mineCr.Heal(4);

            // 缄默压舍身（FindVictim 持有者闸）：转投失效，战斗伤害照常落原目标
            foeMartyr.AddCounters(CounterRules.NullifyCounter, 1, w1);
            int w1MLifeN0 = w1Martyr.GetLife(); int mineLifeN0 = mineCr.GetLife();
            int dealtMute = KeywordRules.ApplyDamage(foeMartyr, w1Martyr, 2, isCombat: true);
            VerifySuite.Assert(dealtMute == 2 && w1Martyr.GetLife() == w1MLifeN0 - 2 && mineCr.GetLife() == mineLifeN0,
                $"缄默压舍身：转投失效战斗伤害照常（返 {dealtMute}，原目标余 {w1Martyr.GetLife()}，victim 不掉血）");
            foeMartyr.AddCounters(CounterRules.NullifyCounter, -1, w1);
            int w1MLifeN1 = w1Martyr.GetLife(); int mineLifeN1 = mineCr.GetLife();
            int dealtUnmute = KeywordRules.ApplyDamage(foeMartyr, w1Martyr, 1, isCombat: true);
            VerifySuite.Assert(dealtUnmute == 0 && w1Martyr.GetLife() == w1MLifeN1 && mineCr.GetLife() == mineLifeN1 - 1,
                $"缄默解除舍身恢复：转投照常（返 {dealtUnmute}，victim {mineLifeN1}→{mineCr.GetLife()}）");

            // 缄默压守护（FindGuardian 既有闸——回归）+ 沉默负例（沉默不压守护改写）
            var muteGuard = new CardWrapper(new CardData
            {
                ID = "V9P_MUTE_GUARD", CardName = "V9P缄默守护者", Supertype = Cardtype.Creature, Power = 2, Life = 9,
            });
            muteGuard.SetController(w1);
            wcore.ZoneManager.GetZoneContainer(w1).Add(muteGuard, Zone.Battlefield);
            muteGuard.AddKeyword(KeywordRules.Guardian, KeywordLane.Setting, muteGuard, 1, -1);
            GuardianRules.OnGrantedInPlay(muteGuard); // 无头自动配对首候选=mineCr
            var muteAtk = new CardWrapper(new CardData
            {
                ID = "V9P_MUTE_ATK", CardName = "V9P白板攻击手", Supertype = Cardtype.Creature, Power = 2, Life = 5,
            });
            muteAtk.SetController(w2);
            wcore.ZoneManager.GetZoneContainer(w2).Add(muteAtk, Zone.Battlefield);

            int mineLifeG0 = mineCr.GetLife(); int guardLife0 = muteGuard.GetLife();
            int dealtG0 = KeywordRules.ApplyDamage(muteAtk, mineCr, 2, isCombat: true);
            VerifySuite.Assert(dealtG0 == 2 && mineCr.GetLife() == mineLifeG0 && muteGuard.GetLife() == guardLife0 - 2,
                $"守护前置：受伤改写守护者承受（返 {dealtG0}，ward 不掉血，守护者 {guardLife0}→{muteGuard.GetLife()}）");
            muteGuard.AddCounters(CounterRules.NullifyCounter, 1, w2);
            int mineLifeG1 = mineCr.GetLife(); int guardLife1 = muteGuard.GetLife();
            int dealtG1 = KeywordRules.ApplyDamage(muteAtk, mineCr, 2, isCombat: true);
            VerifySuite.Assert(dealtG1 == 2 && mineCr.GetLife() == mineLifeG1 - 2 && muteGuard.GetLife() == guardLife1,
                $"缄默压守护：配对失效改写不发生（返 {dealtG1}，ward {mineLifeG1}→{mineCr.GetLife()}，守护者不掉血）");
            muteGuard.AddCounters(CounterRules.NullifyCounter, -1, w2);
            muteGuard.AddCounters(CounterRules.SilenceCounter, 1, w2);
            int mineLifeG2 = mineCr.GetLife(); int guardLife2 = muteGuard.GetLife();
            int dealtG2 = KeywordRules.ApplyDamage(muteAtk, mineCr, 2, isCombat: true);
            VerifySuite.Assert(dealtG2 == 2 && mineCr.GetLife() == mineLifeG2 && muteGuard.GetLife() == guardLife2 - 2,
                $"沉默不压守护：改写照常（返 {dealtG2}，守护者 {guardLife2}→{muteGuard.GetLife()}）——沉默只拦主动");
            muteGuard.AddCounters(CounterRules.SilenceCounter, -1, w2);

            // 缄默压剧毒（HasKeyword 收口行为证据）：关键词失效 → 战斗伤害落血不消灭
            var muteVenom = new CardWrapper(new CardData
            {
                ID = "V9P_MUTE_VENOM", CardName = "V9P缄默剧毒者", Supertype = Cardtype.Creature, Power = 2, Life = 5,
            });
            muteVenom.SetController(w2);
            wcore.ZoneManager.GetZoneContainer(w2).Add(muteVenom, Zone.Battlefield);
            muteVenom.AddKeyword(KeywordRules.Venom, KeywordLane.Setting, muteVenom, 1, -1);
            var mutePrey = new CardWrapper(new CardData
            {
                ID = "V9P_MUTE_PREY", CardName = "V9P剧毒靶", Supertype = Cardtype.Creature, Power = 1, Life = 5,
            });
            mutePrey.SetController(w1);
            wcore.ZoneManager.GetZoneContainer(w1).Add(mutePrey, Zone.Battlefield);

            muteVenom.AddCounters(CounterRules.NullifyCounter, 1, w1);
            int preyLife0 = mutePrey.GetLife();
            KeywordRules.ApplyDamage(muteVenom, mutePrey, 2, isCombat: true);
            VerifySuite.Assert(mutePrey.IsAlive && mutePrey.GetLife() == preyLife0 - 2,
                "缄默压剧毒：关键词失效，战斗伤害落血不消灭");
            muteVenom.AddCounters(CounterRules.NullifyCounter, -1, w1);
            KeywordRules.ApplyDamage(muteVenom, mutePrey, 2, isCombat: true);
            VerifySuite.Assert(!mutePrey.IsAlive,
                "缄默解除剧毒恢复：实际落定>0 → TryKill 消灭");

            // HasKeyword 收口单元断言：本体关键词缄默失效/沉默不失效（光环投递轨见 V9S 段）
            var muteProbe = new CardWrapper(new CardData
            {
                ID = "V9P_MUTE_PROBE", CardName = "V9P缄默探针", Supertype = Cardtype.Creature, Power = 1, Life = 5,
            });
            muteProbe.SetController(w1);
            wcore.ZoneManager.GetZoneContainer(w1).Add(muteProbe, Zone.Battlefield);
            muteProbe.AddKeyword(KeywordRules.Taunt, KeywordLane.Setting, muteProbe, 1, -1);
            VerifySuite.Assert(muteProbe.HasKeyword(KeywordRules.Taunt), "前置：探针持有嘲讽");
            muteProbe.AddCounters(CounterRules.NullifyCounter, 1, w2);
            VerifySuite.Assert(!muteProbe.HasKeyword(KeywordRules.Taunt), "缄默压本体关键词（HasKeyword 收口）");
            muteProbe.AddCounters(CounterRules.SilenceCounter, 1, w2);
            VerifySuite.Assert(!muteProbe.HasKeyword(KeywordRules.Taunt), "沉默不复活关键词（仍被缄默）");
            muteProbe.AddCounters(CounterRules.NullifyCounter, -1, w2);
            VerifySuite.Assert(muteProbe.HasKeyword(KeywordRules.Taunt), "沉默不压关键词（缄默解除即恢复）");
            muteProbe.AddCounters(CounterRules.SilenceCounter, -1, w2);

            // 探针清场（不扰动后续段；mutePrey 已被消灭入墓）
            wcore.ZoneManager.MoveCard(muteGuard, w1, Zone.Battlefield, Zone.Exile);
            wcore.ZoneManager.MoveCard(muteAtk, w2, Zone.Battlefield, Zone.Exile);
            wcore.ZoneManager.MoveCard(muteVenom, w2, Zone.Battlefield, Zone.Exile);
            wcore.ZoneManager.MoveCard(muteProbe, w1, Zone.Battlefield, Zone.Exile);

            // ---- ③ 成长翻倍：2026-10-08 机制删除，回归块随机制移除 ----

            // ---- ④ 赋予主干（Grant=7 引擎解体 2026-10-05）：无分支槽原子 + 效果级持续档计价 ----
            //（旧「header.EngineKind=Grant + 原子搬 RewardAtoms」形态退役——Grant 梯计价保留，
            //  原子留主序列照常计价，持续档从效果级 Duration 读）
            var grantData = new CardEffectData
            {
                Id = "V9P_GRANT",
                Duration = (int)DurationType.UntilEndOfTurn,
                SelectionMode = -1,
                AtomicEffects = new List<AtomicEffectEntry>
                {
                    // 载体用隐密（GrantConcealed——潜行已转指示物，Grant 梯对指示物族不生效）
                    AtomRefs.New(AtomicEffectType.GrantConcealed, value: 1, kinds: new List<int> { 1, 2 }, str: "Concealed"),
                    AtomRefs.New(AtomicEffectType.AddPowerUp, value: 2, kinds: new List<int> { 1, 2 }),
                },
            };
            var v9pGrantDef = CardEffectConverter.ConvertOne(grantData, "V9P_src");
            VerifySuite.Assert(v9pGrantDef != null
                              && v9pGrantDef.Effects.Count == 2
                              && v9pGrantDef.Effects.All(a => a.Branch == null),
                "赋予主干：原子留主序列照常计价（无分支槽原子——不再搬奖励载荷）");
            var grantCostUet = CostDerivationService.DeriveElementCosts(v9pGrantDef);
            VerifySuite.Assert(grantCostUet.Total > 0,
                $"赋予主干计价>0（实际 {grantCostUet.Total}——关键词 Grant 梯×1.2 + 属性指示物锚梯）");
            grantData.Duration = (int)DurationType.Permanent;
            var grantPermDef = CardEffectConverter.ConvertOne(grantData, "V9P_src");
            var grantCostPerm = CostDerivationService.DeriveElementCosts(grantPermDef);
            VerifySuite.Assert(grantCostPerm.Total > grantCostUet.Total,
                $"持续档驱动计价：永久档 > 回合结束档（{grantCostPerm.Total} > {grantCostUet.Total}——涨幅来自 Grant 关键词梯）");

            // ---- ④' 指示物统一计价（2026-10-08 层改造定案）：基础费用×赋予层数（线性，砍档位梯）——
            //      基础费用=表行 ManaList 锚（1 层价，量级回归表行）；层数=max(1,|value|)。
            //      判定=表行 Tags 行为三型；原属性指示物锚梯（StatCounterTierPrice 1.5/层）退役。
            //      2026-10-18 效果合成阶段取整：期望=round(行锚×层数)（毒素 0.5×3=1.5→2）——
            //      取整在效果出口逐色发生，支付/卡级不再二次约等于。
            var puRow = AtomicEffectTable.GetByType(AtomicEffectType.AddPowerUp);
            var toxinRow = AtomicEffectTable.GetByType(AtomicEffectType.AddToxin);
            float ExpectLayers(CardCore.Attribute.AtomicEffectConfig row, int n)
                => (int)Math.Round(row.TotalUnitCost * Math.Max(1, n), MidpointRounding.AwayFromZero);
            VerifySuite.Assert(puRow != null && toxinRow != null
                              && Math.Abs(DeriveAtomTotal(puRow, 2, 1) - ExpectLayers(puRow, 2)) < 0.01f
                              && Math.Abs(DeriveAtomTotal(toxinRow, 3, 2) - ExpectLayers(toxinRow, 3)) < 0.01f
                              && Math.Abs(DeriveAtomTotal(puRow, 0, 1) - ExpectLayers(puRow, 0)) < 0.01f,
                $"指示物统一计价：round(行锚×层数)（攻增 {puRow?.TotalUnitCost}×2={DeriveAtomTotal(puRow, 2, 1)}；" +
                $"毒素 {toxinRow?.TotalUnitCost}×3={DeriveAtomTotal(toxinRow, 3, 2)}；未声明层数=1 层；效果出口逐色取整）");

            // ---- ④'' 层行为标签（2026-10-08 显性化）：指示物行 Tags 携带行为型三值——
            //      衰退型（层=剩余回合，回合末倒数）/ 换区清除型（常驻）/ 生效自减型（生效后自减），
            //      与 CounterSpec.Class 同源（Decay / Resident / Exception+External 消耗）——防表码漂移锚。
            //      坚韧/易损 2026-10-09 迁生效自减（受伤拦减/放大后层数减半 floor，坚韧自换区清除型、
            //      易损自衰退型迁出）。
            VerifySuite.Assert(HasTag(AtomicEffectType.Freeze, "衰退型")
                              && HasTag(AtomicEffectType.RushSickness, "衰退型")
                              && HasTag(AtomicEffectType.Sleep, "衰退型")
                              && HasTag(AtomicEffectType.LockCard, "衰退型")
                              && HasTag(AtomicEffectType.Silence, "换区清除型")
                              && HasTag(AtomicEffectType.AddNullify, "换区清除型")
                              && HasTag(AtomicEffectType.RevealCard, "换区清除型")
                              && HasTag(AtomicEffectType.AddPowerUp, "换区清除型")
                              && HasTag(AtomicEffectType.AddCostUp, "换区清除型")
                              && HasTag(AtomicEffectType.AddPowerDown, "换区清除型")
                              && HasTag(AtomicEffectType.AddArmor, "生效自减型")
                              && HasTag(AtomicEffectType.AddToxin, "生效自减型")
                              && HasTag(AtomicEffectType.AddHeroAttack, "生效自减型")
                              && HasTag(AtomicEffectType.GrantToughness, "生效自减型")
                              && HasTag(AtomicEffectType.AddVulnerable, "生效自减型")
                              && HasTag(AtomicEffectType.GrantRetaliation, "生效自减型"),
                "层行为标签三型入表：衰退×4 / 换区清除×11 / 生效自减×15（坚韧/易损 2026-10-09 迁生效自减、报复 2026-10-16 入表；与 CounterSpec.Class 同源）");
            var counterOnly = new CardEffectData
            {
                Id = "V9P_COUNTER_ONLY",
                Duration = (int)DurationType.UntilEndOfTurn,
                SelectionMode = -1,
                AtomicEffects = new List<AtomicEffectEntry>
                {
                    AtomRefs.New(AtomicEffectType.AddPowerUp, value: 2, kinds: new List<int> { 1, 2 }),
                },
            };
            var counterUetCost = CostDerivationService.DeriveElementCosts(CardEffectConverter.ConvertOne(counterOnly, "V9P_src"));
            counterOnly.Duration = (int)DurationType.Permanent;
            var counterPermCost = CostDerivationService.DeriveElementCosts(CardEffectConverter.ConvertOne(counterOnly, "V9P_src"));
            VerifySuite.Assert(counterUetCost.Total > 0 && counterUetCost.Total == counterPermCost.Total,
                $"指示物计价不读效果持续档（UET {counterUetCost.Total} = Perm {counterPermCost.Total}——档=CounterSpec 换区清）");

            // ---- ⑤ 诅咒有限分支端到端已删（2026-10-08 引擎主干化：AddCurse 表行退役，CurseOnDraw 门转 legacy
            //      不可达；新机制 e2e 见 V9.h——EngineCurseOnDraw/EngineBlessingOnDraw 施放部署→抽到发作） ----

            // ---- ⑥ 两槽装载校验轻锚（2026-10-05 定案）：主序列主干原子 ≤2 / 域交集空 → 构筑期 Error ----
            //（可见不炸口径：经 TideLog.Sink 捕获 Error 断言——ValidateComboDomains 走 LoadCardsFromText 公共口）
            var sinkErrors = new List<string>();
            var prevSink = TideLog.Sink;
            TideLog.Sink = (level, text) => { if (level == TideLogLevel.Error) sinkErrors.Add(text); };
            try
            {
                string ddHash = AtomicEffectTable.GetByType(AtomicEffectType.DealDamage)?.HashId;
                // 断链对：DealDamage 锁己方 {1} + 锁对方 {2}（有害原子双侧各自合法——错边不剔除，域交集空）
                var twoSlotJson = "{\"cards\":["
                    + "{\"id\":\"V9P_TRUNK3\",\"cardName\":\"V9p三主干\",\"supertype\":\"Spell\",\"power\":0,\"life\":0,"
                    + "\"effects\":[{\"id\":\"V9P_TRUNK3_EFF\",\"triggerTiming\":" + (int)TriggerTiming.OnPlay + ",\"selectionMode\":-1,"
                    + "\"atomicEffects\":["
                    + "{\"refId\":\"" + ddHash + "\",\"value\":1},"
                    + "{\"refId\":\"" + ddHash + "\",\"value\":1},"
                    + "{\"refId\":\"" + ddHash + "\",\"value\":1}]}]},"
                    + "{\"id\":\"V9P_CHAIN\",\"cardName\":\"V9p断链\",\"supertype\":\"Spell\",\"power\":0,\"life\":0,"
                    + "\"effects\":[{\"id\":\"V9P_CHAIN_EFF\",\"triggerTiming\":" + (int)TriggerTiming.OnPlay + ",\"selectionMode\":-1,"
                    + "\"atomicEffects\":["
                    + "{\"refId\":\"" + ddHash + "\",\"value\":1,\"kinds\":[1]},"
                    + "{\"refId\":\"" + ddHash + "\",\"value\":1,\"kinds\":[2]}]}]}"
                    + "],\"deckConfig\":{\"copiesPerCard\":3}}";
                var twoSlotCards = CardLoader.LoadCardsFromText(twoSlotJson);
                VerifySuite.Assert(twoSlotCards.Count == 2, "两槽校验夹具装载成功（2 卡——Error 可见不炸）");
                VerifySuite.Assert(sinkErrors.Any(e => e.Contains("V9P_TRUNK3") && e.Contains("超两槽上限")),
                    $"主序列主干原子 >2 → Error（实际命中 {sinkErrors.Count(e => e.Contains("超两槽上限"))} 条）");
                // 逐原子目标制（2026-10-09）：域不交组合合法化——旧「交集空断链 Error」退役
                //（锁己方 {1} 有害原子=苏醒式自缚放行口径，与锁对方 {2} 各自按自身域独立解析；
                //  本夹具同 refId 双原子仍按「重复效果」全局规则点名——断言只锚断链文案的缺席）
                VerifySuite.Assert(!sinkErrors.Any(e => e.Contains("交集为空")),
                    "域不交双槽组合不再断链拦截（逐原子目标制——各原子自解析；「重复效果」Error 属另一规则照常）");
            }
            finally
            {
                TideLog.Sink = prevSink;
            }

            // ============================ V9.q 有限分支（达标奖励/不达标无事）+ 产出条件纯奖励 + 引擎主干回表 ============================

            VerifySuite.Section("V9.q 有限分支还原 + 产出条件纯奖励 + 引擎主干回表");
            var bcore = GameCore.Instance;
            bcore.Reset();
            ZoneContainer.Reseed(20261013);
            var bFill = Enumerable.Range(0, 8).Select(i => new CardData
            {
                ID = $"V9Q_fill_{i}", CardName = "V9Q填充" + i, Supertype = Cardtype.Creature, Power = 1, Life = 1,
            }).ToList();
            bcore.InitGame(CardLoader.BuildDeck(bFill, 2), CardLoader.BuildDeck(bFill, 2));
            var b1x = bcore.Player1;
            var b2x = bcore.Player2;
            GameActions.SkipElementPool(bcore, b1x);
            foreach (ManaType t in Enum.GetValues(typeof(ManaType)))
                bcore.ElementPool.GetPool(b1x).TempElements[t] = 99;

            // ---- ① 目录锚：产出条件五项（DeclareMiss/ProphecyHit/Miss 已归自由分支）+ 引擎主干行回表 ----
            VerifySuite.Assert(ComposerCatalog.OutcomeConditions.Select(g => g.Id).SequenceEqual(
                    new[] { "DmgKillsTarget", "DeclareHit", "DeclareMiss", "ProphecyHit", "ProphecyMiss" }),
                "产出条件目录五项（伤害击杀/宣言命中落空/预言命中落空——TargetSurvived 等 4 项已删）");
            var bEnginePairs = new Dictionary<AtomicEffectType, BranchEngineKind>
            {
                { AtomicEffectType.EngineCountdown, BranchEngineKind.Countdown },
                { AtomicEffectType.EngineLuckRoll, BranchEngineKind.LuckRoll },
                { AtomicEffectType.EngineClash, BranchEngineKind.Clash },
                { AtomicEffectType.EngineDeathToll, BranchEngineKind.DeathToll },
                { AtomicEffectType.EngineManaSurplus, BranchEngineKind.ManaSurplus },
                { AtomicEffectType.EngineEffectOrdinal, BranchEngineKind.EffectOrdinal },   // 2026-10-19 手牌序位改案
                { AtomicEffectType.EngineCurseOnDraw, BranchEngineKind.CurseOnDraw },       // 2026-10-08 引擎主干化
                { AtomicEffectType.EngineBlessingOnDraw, BranchEngineKind.BlessingOnDraw }, // 2026-10-08
            };
            foreach (var kv in bEnginePairs)
            {
                var row = AtomicEffectTable.GetByType(kv.Key);
                VerifySuite.Assert(row != null && ComposerCatalog.IsEngineTrunkRow(row)
                                   && ComposerCatalog.EngineKindOf(kv.Key) == kv.Value,
                    $"引擎主干行回表：{kv.Key}（位 5 引擎主干、映射 {kv.Value}——2026-10-07 位 0 删除后主干资格派生，引擎行互斥由 CanBeRewardRow 排除）");
            }

            // ---- ①b 效果序位（2026-10-19 手牌序位改案·定案）：本回合双方效果发动数达到 x−1 时触发——
            //      发动口径=主动/自动效果入栈声明+法术整卡施放各 1；SBA/攻击宣言等规则栈对象与引擎奖励
            //      不算；奖励自身不占发动位；观察面=手牌∪场上双方引擎卡。运行时行为夹具见本段段尾 ⑧ ----



            // ---- ② 引擎行装载：载荷挂主干原子、Then 零计价、主序列执行跳过（无处理器告警=漏跳证据） ----
            var bEngineCard = new CardData { ID = "V9Q_ENGINE", CardName = "V9Q引擎卡", Supertype = Cardtype.Spell };
            bEngineCard.Effects.Add(new CardEffectData
            {
                Id = "V9Q_ENGINE_EFF",
                TriggerTiming = (int)TriggerTiming.OnPlay,
                SelectionMode = -1,
                AtomicEffects = new List<AtomicEffectEntry>
                {
                    new AtomicEffectEntry
                    {
                        refId = AtomicEffectTable.GetByType(AtomicEffectType.EngineClash)?.HashId,
                        value = 1,
                        branch = new BranchEntryData
                        {
                            settle = (int)BranchSettleKind.Engine,
                            engine = (int)BranchEngineKind.Clash,
                            engineParam = 2,
                            then = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DrawCard, value: 1) },
                        },
                    },
                },
            });
            var bEngineDef = CardEffectConverter.ConvertOne(bEngineCard.Effects[0], "V9Q_src");
            VerifySuite.Assert(bEngineDef != null && bEngineDef.Effects.Count == 1
                               && bEngineDef.Effects[0].Branch != null
                               && bEngineDef.Effects[0].Branch.Settle == BranchSettleKind.Engine
                               && bEngineDef.Effects[0].Branch.EngineKind == BranchEngineKind.Clash
                               && bEngineDef.Effects[0].Branch.Then.Count == 1,
                "引擎行装载：branch 载荷（Settle=Engine·Clash·Then=抽1）挂主干原子");
            VerifySuite.Assert(CostDerivationService.DeriveElementCosts(bEngineDef).Total == 0f,
                "引擎行零锚价 + Then 零计价（回表后计价不变——机制自平衡）");

            // ---- ②b 引擎效果头预设兜底（2026-10-09 自由分支九项定案：数据声明被归一到引擎内生语义） ----
            VerifySuite.Assert(bEngineDef.ActivationType == EffectActivationType.Automatic
                               && bEngineDef.TriggerTiming == TriggerTiming.OnAttack
                               && bEngineDef.BaseSpeed == 0
                               && bEngineDef.TriggerLimitPerTurn == -1,
                $"拼点效果头预设：自动/攻击宣言/无限（数据声明 OnPlay 已兜底归一——实际 " +
                $"{bEngineDef.ActivationType}/{bEngineDef.TriggerTiming}/{bEngineDef.TriggerLimitPerTurn}）");
            var bCurseCard = new CardData { ID = "V9Q_CURSE", CardName = "V9Q诅咒引擎", Supertype = Cardtype.Spell };
            bCurseCard.Effects.Add(new CardEffectData
            {
                Id = "V9Q_CURSE_EFF",
                TriggerTiming = (int)TriggerTiming.OnTurnStart, // 故意错声明——预设应归一为主动/主动
                SelectionMode = -1,
                AtomicEffects = new List<AtomicEffectEntry>
                {
                    new AtomicEffectEntry
                    {
                        refId = AtomicEffectTable.GetByType(AtomicEffectType.EngineCurseOnDraw)?.HashId,
                        value = 1,
                        branch = new BranchEntryData
                        {
                            settle = (int)BranchSettleKind.Engine,
                            engine = (int)BranchEngineKind.CurseOnDraw,
                            engineParam = 1,
                            then = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DrawCard, value: 1) },
                        },
                    },
                },
            });
            var bCurseDef = CardEffectConverter.ConvertOne(bCurseCard.Effects[0], "V9Q_src");
            VerifySuite.Assert(bCurseDef.ActivationType == EffectActivationType.Voluntary
                               && bCurseDef.TriggerTiming == TriggerTiming.Activate_Active
                               && bCurseDef.BaseSpeed == 0
                               && bCurseDef.TriggerLimitPerTurn == 1,
                "附加诅咒/祝福效果头预设：主动/主动钉档/速度0/1次（部署点=施放结算不变）");

            // ---- ②c 附加诅咒/祝福分支附加费（Then 奖励不再免费：ceil 减半；其余引擎照旧零计价见②） ----
            var bThenCost = CostDerivationService.RewardDerivedCost(bCurseDef.Effects[0].Branch.Then);
            var bCursePrice = CostDerivationService.DeriveElementCosts(bCurseDef).Total;
            VerifySuite.Assert(bCursePrice > 0f
                               && Math.Abs(bCursePrice - Math.Ceiling(bThenCost / 2f)) < 1e-4f,
                $"附加诅咒分支费减半向上取整：卡价 {bCursePrice:0.##} = ceil(Then {bThenCost:0.##}/2)——拼点锚仍 {CostDerivationService.DeriveElementCosts(bEngineDef).Total:0.##}");

            // ---- ②d 拼点触发事件迁移回归锚（2026-10-09：回合开始→攻击宣言·仅攻击方·无限；
            //      2026-10-19 取样改版：牌库随机取样→手牌展示最高攻生物，挂「展示」对双方公开） ----
            var bEngineUnit = new CardWrapper(bEngineCard);
            bEngineUnit.SetController(b1x);
            bcore.ZoneManager.GetZoneContainer(b1x).Add(bEngineUnit, Zone.Battlefield);
            foreach (var side in new[] { b1x, b2x }) // 牌库+手牌清空（防抽牌引入未知卡），手牌各注一张已知生物=取样确定
            {
                var zc = bcore.ZoneManager.GetZoneContainer(side);
                foreach (var c in bcore.ZoneManager.GetCards(side, Zone.Deck).ToList())
                    zc.Move(c, Zone.Deck, Zone.Graveyard);
                foreach (var c in bcore.ZoneManager.GetCards(side, Zone.Hand).ToList())
                    zc.Move(c, Zone.Hand, Zone.Graveyard);
            }
            var bHandA = new CardWrapper(new CardData
            { ID = "V9Q_HAND_A", CardName = "V9Q己方手牌生物", Supertype = Cardtype.Creature, Power = 9, Life = 1 });
            bHandA.SetController(b1x);
            bcore.ZoneManager.GetZoneContainer(b1x).Add(bHandA, Zone.Hand);
            var bHandB = new CardWrapper(new CardData
            { ID = "V9Q_HAND_B", CardName = "V9Q对方手牌生物", Supertype = Cardtype.Creature, Power = 1, Life = 1 });
            bHandB.SetController(b2x);
            bcore.ZoneManager.GetZoneContainer(b2x).Add(bHandB, Zone.Hand);

            int clashEvents = 0;
            string clashDetail = null;
            int clashRevealEvents = 0;
            EventManager.Instance.Subscribe<KeywordAppliedEvent>(
                e => { if (e.Keyword == "拼点" && e.Target == bEngineUnit) { clashEvents++; clashDetail = e.Detail; } });
            EventManager.Instance.Subscribe<RevealCardsEvent>(
                e => { if (e.Source == bEngineUnit) clashRevealEvents += e.Cards?.Count ?? 0; });
            int bThreshold = CostDerivationService.RewardThreshold(bEngineDef.Effects[0].Branch.Then);
            EventManager.Instance.Publish(new TurnStartEvent { TurnPlayer = b1x, TurnNumber = 99 });
            VerifySuite.Assert(clashEvents == 0,
                "拼点不再回合开始触发（2026-10-09 触发改版回归锚——TurnStart 零拼点播报）");
            EventManager.Instance.Publish(new AttackDeclarationEvent { AttackingPlayer = b1x });
            VerifySuite.Assert(clashEvents == 1 && clashDetail != null && clashDetail.Contains("拼点 9 vs 1"),
                $"攻击宣言触发拼点（仅攻击方：9 vs 1 差额 8 ≥ 门槛 {bThreshold}——实际 {clashEvents} 次：{clashDetail ?? "无"}）");
            VerifySuite.Assert(clashRevealEvents == 2
                              && bHandA.GetCounterCount(CounterRules.ExposedCounter) == 1
                              && bHandB.GetCounterCount(CounterRules.ExposedCounter) == 1,
                $"拼点展示：双方手牌最高攻生物各挂「展示」并播报（RevealCards 计数 {clashRevealEvents}，指示物 " +
                $"{bHandA.GetCounterCount(CounterRules.ExposedCounter)}/{bHandB.GetCounterCount(CounterRules.ExposedCounter)}）");
            int clashAfterDefender = clashEvents;
            EventManager.Instance.Publish(new AttackDeclarationEvent { AttackingPlayer = b2x });
            VerifySuite.Assert(clashEvents == clashAfterDefender && clashRevealEvents == 2,
                "对方攻击宣言不触发我方拼点引擎（仅攻击方结算——守方引擎卡零播报零展示）");
            bcore.ZoneManager.GetZoneContainer(b2x).Move(bHandB, Zone.Hand, Zone.Graveyard); // 对方手牌无生物=0
            EventManager.Instance.Publish(new AttackDeclarationEvent { AttackingPlayer = b1x });
            VerifySuite.Assert(clashEvents == 2 && clashDetail.Contains("拼点 9 vs 0") && clashRevealEvents == 2,
                $"无法展示视为0（对方手牌无生物 9 vs 0 仍判定并再触发；已展示卡不重复挂层不重播——实际 {clashEvents} 次：{clashDetail ?? "无"}）");
            foreach (var c in bcore.ZoneManager.GetCards(b1x, Zone.Hand).ToList()) // 我方手牌也无生物
                bcore.ZoneManager.GetZoneContainer(b1x).Move(c, Zone.Hand, Zone.Graveyard);
            EventManager.Instance.Publish(new AttackDeclarationEvent { AttackingPlayer = b1x });
            VerifySuite.Assert(clashEvents == 2,
                $"双方均无法展示 0 vs 0 差额 0 < 门槛 {bThreshold}（下限1）不触发（实际 {clashEvents} 次）");

            // ---- ③ 条件体系现状锚：有限分支（Gate）2026-10-09 还原 + settle 越界丢弃 ----
            CardData BetCard(string id) => new CardData
            {
                ID = id, CardName = "V9Q条件术", Supertype = Cardtype.Spell,
            };
            VerifySuite.Assert(CardCore.BranchEntryRules.IsPhantom(new BranchEntryData { settle = 0 })
                              && !CardCore.BranchEntryRules.IsPhantom(new BranchEntryData { settle = 1 }),
                "settle=0 幽灵剥离；settle=1=有限分支（Gate，2026-10-09 还原）为有效值");
            VerifySuite.Assert(BranchConditionEvaluator.IsKnownCondition("LifeBelowOpp")
                              && BranchConditionEvaluator.IsKnownCondition("FirstCardThisTurn")
                              && BranchConditionEvaluator.IsKnownCondition("DmgKillsTarget")
                              && !BranchConditionEvaluator.IsKnownCondition("CurseOnDraw"),
                "评估器登记产出五项+局面门 11 项（CurseOnDraw 不在——诅咒通道=引擎主干行）");
            var gatePureDef = CardEffectConverter.ConvertOne(
                new CardEffectData
                {
                    Id = "V9Q_GATE_PRICE",
                    TriggerTiming = (int)TriggerTiming.OnPlay,
                    SelectionMode = -1,
                    AtomicEffects = new List<AtomicEffectEntry>
                    {
                        AtomRefs.New(AtomicEffectType.DealDamage, value: 1, kinds: new List<int> { 2 }),
                    },
                }, "V9Q_src");
            var gatePurePrice = CostDerivationService.DeriveElementCosts(gatePureDef).Total;
            var gateCard = BetCard("V9Q_GATE");
            gateCard.Effects.Add(new CardEffectData
            {
                Id = "V9Q_GATE_EFF",
                TriggerTiming = (int)TriggerTiming.OnPlay,
                SelectionMode = -1,
                AtomicEffects = new List<AtomicEffectEntry>
                {
                    new AtomicEffectEntry
                    {
                        refId = AtomicEffectTable.GetByType(AtomicEffectType.DealDamage)?.HashId,
                        value = 1,
                        kinds = new List<int> { 2 }, // 主干：对对方生物 1 伤
                        branch = new BranchEntryData
                        {
                            settle = (int)BranchSettleKind.Gate,
                            gateId = "LifeBelowOpp", // 有限分支：生命低于对手
                            then = new List<AtomicEffectEntry>
                            {
                                // 奖励=治疗 2（己方生物——无头自动选首=bMine）
                                AtomRefs.New(AtomicEffectType.Heal, value: 2, kinds: new List<int> { 1 }),
                            },
                        },
                    },
                },
            });
            var gateDef = CardEffectConverter.ConvertOne(gateCard.Effects[0], "V9Q_src");
            VerifySuite.Assert(gateDef.Effects.Count == 1 && gateDef.Effects[0].Branch != null
                               && gateDef.Effects[0].Branch.Settle == BranchSettleKind.Gate
                               && gateDef.Effects[0].Branch.GateId == "LifeBelowOpp"
                               && Math.Abs(CostDerivationService.DeriveElementCosts(gateDef).Total - gatePurePrice) < 1e-4f,
                $"有限分支零加价：settle=1+gateId → Gate 载荷（达标奖励/不达标无事），Then 零计价维持（带门 {CostDerivationService.DeriveElementCosts(gateDef).Total:0.##} = 纯主干 {gatePurePrice:0.##}）");
            var junkBet = BetCard("V9Q_JUNK");
            junkBet.Effects.Add(new CardEffectData
            {
                Id = "V9Q_JUNK_EFF",
                TriggerTiming = (int)TriggerTiming.OnPlay,
                SelectionMode = -1,
                AtomicEffects = new List<AtomicEffectEntry>
                {
                    new AtomicEffectEntry
                    {
                        refId = AtomicEffectTable.GetByType(AtomicEffectType.DealDamage)?.HashId,
                        value = 1,
                        kinds = new List<int> { 2 },
                        branch = new BranchEntryData
                        {
                            settle = 99, // 真越界值——converter 告警丢弃
                            gateId = "LifeBelowOpp",
                            then = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.Heal, value: 2) },
                        },
                    },
                },
            });
            var junkDef = CardEffectConverter.ConvertOne(junkBet.Effects[0], "V9Q_src");
            VerifySuite.Assert(junkDef.Effects.Count == 1 && junkDef.Effects[0].Branch == null,
                "settle 越界分支载荷装载即丢弃（主干原子保留、无分支——装载可见不炸）");

            // ---- ④ 产出条件纯奖励：未达成无动作 ----
            var bMine = new CardWrapper(new CardData
            {
                ID = "V9Q_MINE", CardName = "V9Q己方生物", Supertype = Cardtype.Creature, Power = 1, Life = 9,
            });
            bMine.SetController(b1x);
            bcore.ZoneManager.GetZoneContainer(b1x).Add(bMine, Zone.Battlefield);
            var bFoe = new CardWrapper(new CardData
            {
                ID = "V9Q_FOE", CardName = "V9Q对方生物", Supertype = Cardtype.Creature, Power = 1, Life = 9,
            });
            bFoe.SetController(b2x);
            bcore.ZoneManager.GetZoneContainer(b2x).Add(bFoe, Zone.Battlefield);
            bMine.SetLife(5);
            bFoe.SetLife(5);

            var ocCard = BetCard("V9Q_OUTCOME");
            ocCard.Effects.Add(new CardEffectData
            {
                Id = "V9Q_OUTCOME_EFF",
                TriggerTiming = (int)TriggerTiming.OnPlay,
                SelectionMode = -1,
                AtomicEffects = new List<AtomicEffectEntry>
                {
                    new AtomicEffectEntry
                    {
                        refId = AtomicEffectTable.GetByType(AtomicEffectType.DealDamage)?.HashId,
                        value = 1,
                        kinds = new List<int> { 2 },
                        branch = new BranchEntryData
                        {
                            settle = (int)BranchSettleKind.Outcome,
                            outcomeId = "DmgKillsTarget", // 1 伤杀不死 5 血生物 → 未达成
                            then = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.Heal, value: 2) },
                        },
                    },
                },
            });
            var ocHand = new CardWrapper(ocCard);
            ocHand.SetController(b1x);
            bcore.ZoneManager.GetZoneContainer(b1x).Add(ocHand, Zone.Hand);
            VerifySuite.Assert(GameActions.PlayCard(bcore, b1x, ocHand, null, Zone.Hand, 0, out var rejO),
                $"产出条件卡打出（拒绝原因：{rejO ?? "无"}）");
            GameActions.DrainStack(bcore);
            VerifySuite.Assert(bMine.GetLife() == 5 && bFoe.GetLife() == 4,
                $"产出条件未达成→无奖励也无惩罚（纯奖励语义；主干 5→4，双侧不动——实际 己{bMine.GetLife()}/敌{bFoe.GetLife()}）");

            // ---- ④b 有限分支（Gate）端到端：达标→Then 奖励；不达标→不奖励也不惩罚 ----
            var betHand1 = new CardWrapper(gateCard);
            betHand1.SetController(b1x);
            bcore.ZoneManager.GetZoneContainer(b1x).Add(betHand1, Zone.Hand);
            b1x.Life = 10; b2x.Life = 20; // 生命低于对手=达成面
            VerifySuite.Assert(GameActions.PlayCard(bcore, b1x, betHand1, null, Zone.Hand, 0, out var rejG1),
                $"有限分支卡1（达成面）打出（拒绝原因：{rejG1 ?? "无"}）");
            GameActions.DrainStack(bcore);
            VerifySuite.Assert(bMine.GetLife() == 7 && bFoe.GetLife() == 3,
                $"门达成→奖励照旧：主干打对方生物 4→3、Then 治疗己方生物（候选[己方生物,己方角色] 无头自动选首=己方生物）5→7（实际 己{bMine.GetLife()}/敌{bFoe.GetLife()}）");
            var betCard2 = BetCard("V9Q_GATE2");
            betCard2.Effects.Add(gateCard.Effects[0]);
            var betHand2 = new CardWrapper(betCard2);
            betHand2.SetController(b1x);
            bcore.ZoneManager.GetZoneContainer(b1x).Add(betHand2, Zone.Hand);
            b1x.Life = 30; // 生命高于对手=未达成面
            int mine2 = bMine.GetLife(), foe2 = bFoe.GetLife(), oppLife2 = b2x.Life;
            VerifySuite.Assert(GameActions.PlayCard(bcore, b1x, betHand2, null, Zone.Hand, 0, out var rejG2),
                $"有限分支卡2（未达成面）打出（拒绝原因：{rejG2 ?? "无"}）");
            GameActions.DrainStack(bcore);
            VerifySuite.Assert(bMine.GetLife() == mine2 && bFoe.GetLife() == foe2 - 1 && b2x.Life == oppLife2,
                $"门未达成→不奖励也不惩罚：只结算主干伤害（敌 {foe2}→{foe2 - 1}），双侧无任何额外动作——逆转惩罚面不还原（实际 己{bMine.GetLife()}/敌{bFoe.GetLife()}/对{b2x.Life}）");

            // ---- ⑤ 引擎主序列执行跳过（出牌放末位——不占本回合首卡计数）：
            //      无处理器告警=漏跳证据 ----
            var bSink = new List<string>();
            var bPrevSink = TideLog.Sink;
            TideLog.Sink = (level, text) => { bSink.Add(text); };
            try
            {
                var bEngineHand = new CardWrapper(bEngineCard);
                bEngineHand.SetController(b1x);
                bcore.ZoneManager.GetZoneContainer(b1x).Add(bEngineHand, Zone.Hand);
                VerifySuite.Assert(GameActions.PlayCard(bcore, b1x, bEngineHand, null, Zone.Hand, 0, out var rejE),
                    $"纯引擎卡打出（拒绝原因：{rejE ?? "无"}）");
                GameActions.DrainStack(bcore);
                VerifySuite.Assert(!bSink.Any(t => t.Contains("未注册的效果处理器")),
                    "引擎主干不进主序列执行（无处理器告警=漏跳证据）");
            }
            finally { TideLog.Sink = bPrevSink; }

            // ---- ⑥ 遗留门步骤折叠：产出条件→Outcome、局面门→Gate、未知条件剔除（装载可见不炸） ----
            var bSink2 = new List<string>();
            TideLog.Sink = (level, text) => { bSink2.Add(text); };
            try
            {
                var legacyGate = new CardData { ID = "V9Q_LEGACYG", CardName = "V9Q遗留门", Supertype = Cardtype.Spell };
                legacyGate.Effects.Add(new CardEffectData
                {
                    Id = "V9Q_LEGACYG_EFF",
                    TriggerTiming = (int)TriggerTiming.OnPlay,
                    SelectionMode = -1,
                    Steps = new List<CardCore.EffectStepData>
                    {
                        new CardCore.EffectStepData
                        {
                            kind = 0,
                            atomic = new AtomicEffectEntry
                            {
                                refId = AtomicEffectTable.GetByType(AtomicEffectType.DealDamage)?.HashId,
                                value = 1,
                            },
                        },
                        new CardCore.EffectStepData
                        {
                            kind = 1, conditionId = "LifeBelowOpp", // 非产出条件 id
                            thenSteps = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DrawCard, value: 1) },
                        },
                    },
                });
                var legacyDef = CardEffectConverter.ConvertOne(legacyGate.Effects[0], "V9Q_src");
                VerifySuite.Assert(legacyDef.Steps.Count == 0 && legacyDef.Effects.Count == 1
                                   && legacyDef.Effects[0].Branch != null
                                   && legacyDef.Effects[0].Branch.Settle == BranchSettleKind.Gate
                                   && legacyDef.Effects[0].Branch.GateId == "LifeBelowOpp"
                                   && !bSink2.Any(t => t.Contains("非产出/局面条件")),
                    "遗留门步骤折叠：局面门折入前原子 Branch（Gate 族——达标奖励/不达标无事）");
                var junkGate = new CardData { ID = "V9Q_JUNKG", CardName = "V9Q未知门", Supertype = Cardtype.Spell };
                junkGate.Effects.Add(new CardEffectData
                {
                    Id = "V9Q_JUNKG_EFF",
                    TriggerTiming = (int)TriggerTiming.OnPlay,
                    SelectionMode = -1,
                    Steps = new List<CardCore.EffectStepData>
                    {
                        new CardCore.EffectStepData
                        {
                            kind = 0,
                            atomic = new AtomicEffectEntry
                            {
                                refId = AtomicEffectTable.GetByType(AtomicEffectType.DealDamage)?.HashId,
                                value = 1,
                            },
                        },
                        new CardCore.EffectStepData
                        {
                            kind = 1, conditionId = "NotACondition", // 未知条件 id
                            thenSteps = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DrawCard, value: 1) },
                        },
                    },
                });
                var junkGateDef = CardEffectConverter.ConvertOne(junkGate.Effects[0], "V9Q_src");
                VerifySuite.Assert(junkGateDef.Steps.Count == 0 && junkGateDef.Effects.Count == 1
                                   && junkGateDef.Effects[0].Branch == null
                                   && bSink2.Any(t => t.Contains("非产出/局面条件")),
                    "遗留门步骤折叠：未知条件剔除（主干原子保留、无分支——Then 不落地）");
            }
            finally { TideLog.Sink = bPrevSink; }

            // ---- ⑧ 效果序位运行时（2026-10-19 手牌序位改案·定案）：本回合双方效果发动数达到 x−1 时触发。
            //      段尾执行——段内既有出牌已抬计数，经同程序集探针复位锚定基线（合成 TurnStart 有级联
            //      副作用不作复位用）；残留栈对象随下段 Reset 清理 ----
            {
                BranchEngines.ResetEffectOrdinalForVerify();
                VerifySuite.Assert(BranchEngines.EffectOrdinalCountForVerify == 0, "效果序位：计数基线复位（验证器探针）");

                // 奖励载体备料：段内既有断言已抽干 b1x 牌库——回填 2 张供 DrawCard 奖励观测
                for (int eoFill = 0; eoFill < 2; eoFill++)
                {
                    var eoDeckFiller = new CardWrapper(new CardData
                    {
                        ID = "V9Q_EO_FILL" + eoFill, CardName = "序位填充", Supertype = Cardtype.Creature, Power = 1, Life = 1,
                    });
                    eoDeckFiller.SetController(b1x);
                    bcore.ZoneManager.GetZoneContainer(b1x).Add(eoDeckFiller, Zone.Deck);
                }

                CardData EOCard(string id, int x)
                {
                    var d = new CardData { ID = id, CardName = id, Supertype = Cardtype.Creature, Power = 1, Life = 1 };
                    d.Effects.Add(new CardEffectData
                    {
                        Id = id + "_EFF", TriggerTiming = (int)TriggerTiming.OnCardPlayed, SelectionMode = -1,
                        AtomicEffects = new List<AtomicEffectEntry>
                        {
                            new AtomicEffectEntry
                            {
                                refId = AtomicEffectTable.GetByType(AtomicEffectType.EngineEffectOrdinal)?.HashId,
                                value = 1,
                                branch = new BranchEntryData
                                {
                                    settle = (int)BranchSettleKind.Engine,
                                    engine = (int)BranchEngineKind.EffectOrdinal,
                                    engineParam = x,
                                    then = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DrawCard, value: 1) },
                                },
                            },
                        },
                    });
                    return d;
                }
                Card EOToHand(Player owner, CardData data)
                {
                    var card = new CardWrapper(data);
                    card.SetController(owner);
                    bcore.ZoneManager.GetZoneContainer(owner).Add(card, Zone.Hand);
                    return card;
                }
                var eoAData = EOCard("V9Q_EO_A", 2); // x=2 → 门槛=1
                var eoBData = EOCard("V9Q_EO_B", 3); // x=3 → 门槛=2
                var eoA = EOToHand(b1x, eoAData);
                EOToHand(b1x, eoBData);

                // 转换器钉死组：错声明（使用卡牌时之外）归一 使用卡牌时+自动+无限（显示缺省；真实观察口=StackAddEvent）
                var eoDef = CardEffectConverter.ConvertOne(eoAData.Effects[0], "V9Q");
                VerifySuite.Assert(eoDef.TriggerTiming == TriggerTiming.OnCardPlayed
                                  && eoDef.ActivationType == EffectActivationType.Automatic
                                  && eoDef.TriggerLimitPerTurn == -1,
                    "效果序位钉死组：引擎头归一 使用卡牌时+自动+无限（2026-10-19 手牌序位改案口径）");

                int eoHand0 = bcore.ZoneManager.GetCards(b1x, Zone.Hand).Count; // 基线在合成回合开始（含其抽牌）之后

                // 规则栈对象探针（合成事件，不占栈）：SBA/攻击宣言无 Definition → 不占发动位
                EventManager.Instance.Publish(new StackAddEvent { AddedObject = new EffectInstance { IsSBA = true } });
                EventManager.Instance.Publish(new StackAddEvent
                {
                    AddedObject = new EffectInstance { IsAttackDeclaration = true, Source = b1x, Targets = new List<Entity> { b2x } },
                });
                VerifySuite.Assert(bcore.ZoneManager.GetCards(b1x, Zone.Hand).Count == eoHand0
                                  && BranchEngines.EffectOrdinalCountForVerify == 0,
                    "效果序位：SBA/攻击宣言不占发动位（计数恒 0，规则动作≠效果发动）");

                // 法术整卡施放=1 次发动（真实入栈）：计数 1 = A 门槛（x−1）→ A 触发抽 1
                var eoSpell = new CardWrapper(new CardData { ID = "V9Q_EO_SPELL", CardName = "序位法术", Supertype = Cardtype.Spell });
                eoSpell.SetController(b1x);
                VerifySuite.Assert(bcore.StackEngine.PushCardCast(eoSpell, b1x, null)
                                  && BranchEngines.EffectOrdinalCountForVerify == 1, "法术施放入栈探针（空栈首动豁免）+ 计数=1");
                VerifySuite.Assert(bcore.ZoneManager.GetCards(b1x, Zone.Hand).Count == eoHand0 + 1,
                    $"效果序位：法术施放计 1 次发动 → 计数 1 = x−1 → A(x=2) 触发抽 1（生物/地牌施放不算）" +
                    $"（实际手牌 {bcore.ZoneManager.GetCards(b1x, Zone.Hand).Count}/{eoHand0}、牌库余 {bcore.ZoneManager.GetCards(b1x, Zone.Deck).Count}）");

                // 主动效果声明=1 次发动（真实入栈，速 2>记速器 0 过门槛）：计数 2 = B 门槛 → B 触发抽 1
                var eoVolDef = new EffectDefinition
                {
                    Id = "V9Q_EO_VOL", DisplayName = "序位主动探针", BaseSpeed = 2,
                    TriggerTiming = TriggerTiming.Activate_Active, ActivationType = EffectActivationType.Voluntary,
                    ElementCostPrepaid = true,
                    Effects = new List<AtomicEffectInstance> { new AtomicEffectInstance { Type = AtomicEffectType.DrawCard, Value = 1 } },
                };
                VerifySuite.Assert(bcore.StackEngine.TryActivateEffect(PendingEffect.Create(eoVolDef, eoA, b1x, b1x,
                        bcore.TurnEngine.CurrentPhase?.Phase ?? PhaseType.Main))
                                  && BranchEngines.EffectOrdinalCountForVerify == 2,
                    "主动效果声明入栈探针（速 2>记速器 0）+ 计数=2");
                VerifySuite.Assert(bcore.ZoneManager.GetCards(b1x, Zone.Hand).Count == eoHand0 + 2,
                    "效果序位：主动效果声明计 1 次发动 → 计数 2 = x−1 → B(x=3) 触发抽 1（A 门槛已过不再重复——计数每值只经过一次）");
            }

            // ============================ V9.r 挑战模式（2026-10-06） ============================
            VerifySuite.Section("V9.r 挑战模式（曲线/起手）");

            // ---- ① 挑战曲线纯函数：CapAt(t)=min(t+N, 10+N)（2026-10-15：自然封顶 7→10，无终钳）；bonus=0 与标准曲线等价 ----
            var rCurve = ResourceCurve.ChallengeLandCurve(3);
            var rCurveOk = true;
            for (int t = 1; t <= 30; t++)
                if (rCurve.CapAt(t) != Math.Min(t + 3, 13)) rCurveOk = false;
            VerifySuite.Assert(rCurveOk, "挑战曲线(3)：CapAt(t)=min(t+3, 13)——开局 4、封顶 13");
            var rCurve0 = ResourceCurve.ChallengeLandCurve(0);
            VerifySuite.Assert(rCurve0.CapAt(1) == 1 && rCurve0.CapAt(10) == 10 && rCurve0.CapAt(99) == 10
                               && ResourceCurve.ChallengeLandCurve(9).CapAt(1) == 10
                               && ResourceCurve.ChallengeLandCurve(9).CapAt(50) == 19,
                "挑战曲线边界：bonus=0 等价标准曲线（自然封顶 10）；bonus=9 开局 10、曲线封顶 19（2026-10-15：MaxStackedLandCap 终钳退役）");

            // ---- ② 挑战局端到端：InitGame 加成（P2 曲线上移 + 起手加抽，P1 不动） ----
            var ccore = GameCore.Instance;
            ccore.Reset();
            var cFill = Enumerable.Range(0, 8).Select(i => new CardData
            {
                ID = $"V9R_fill_{i}", CardName = "V9R填充" + i, Supertype = Cardtype.Creature, Power = 1, Life = 1,
            }).ToList();
            ccore.InitGame(CardLoader.BuildDeck(cFill, 2), CardLoader.BuildDeck(cFill, 2), rngSeed: 20261014,
                p2LandCapBonus: 3, p2ExtraOpeningDraws: 4);
            VerifySuite.Assert(ccore.ElementPool.GetLandCap(ccore.Player1) == 1,
                "挑战局 P1 首回合地牌槽=1（标准曲线不动）");
            VerifySuite.Assert(ccore.ElementPool.GetLandCap(ccore.Player2) == 4,
                "挑战局 P2 首回合地牌槽=1+3（额外初始槽位）");
            var cHand1 = ccore.ZoneManager.GetCards(ccore.Player1, Zone.Hand).Count;
            var cHand2 = ccore.ZoneManager.GetCards(ccore.Player2, Zone.Hand).Count;
            VerifySuite.Assert(cHand1 == GameCore.OpeningHandSize + 1 && cHand2 == GameCore.OpeningHandSize + 4,
                $"起手张数：玩家 {cHand1}（6 起手+首回合抽 1） vs 电脑 {cHand2}（6+4 额外抽，无截断）");

            // ============================ V9.s 角色参战（2026-10-07 弹药定案） ============================
            // 弹药原子：角色攻击力=HeroAttackCounter 层数；攻击/反击结算后烧光（弹药即闸门）；
            // 关键词光环可经箭头指向角色格投递；属性/作用面光环钉死生物专用。

            VerifySuite.Section("V9.s 角色参战：弹药原子（读数/攻击烧弹药/反伤烧弹药/缴械）+关键词光环角色格通道");

            var score = GameCore.Instance;
            score.Reset();
            ZoneContainer.Reseed(20261007);
            var sFill = Enumerable.Range(0, 8).Select(i => new CardData
            {
                ID = $"V9S_fill_{i}", CardName = "V9S填充" + i, Supertype = Cardtype.Creature, Power = 1, Life = 1,
            }).ToList();
            score.InitGame(CardLoader.BuildDeck(sFill, 2), CardLoader.BuildDeck(sFill, 2));
            var sp1 = score.Player1;
            var sp2 = score.Player2;
            GameActions.SkipElementPool(score, sp1); // Standby → Main（sp1 主阶段）

            // ---- ① 读数与力量门：弹药=攻击力读数；0 层不可宣、3 层可宣 ----
            VerifySuite.Assert(sp1.GetPower() == 0, "角色攻击力读数=0（无弹药）");
            VerifySuite.Assert(!score.CombatSystem.CanDeclareAttack(sp1, sp1), "角色无弹药不可宣言攻击（力量门）");
            sp1.AddCounters(CounterRules.HeroAttackCounter, 3);
            VerifySuite.Assert(sp1.GetPower() == 3, "弹药 3 层 → 角色攻击力读数 3");

            // ---- ② 角色主动攻击（Player 攻击者走栈）：全额伤害+结算后烧光；二次宣言被力量门拒 ----
            var sVictim = new CardWrapper(new CardData
            {
                ID = "V9S_victim", CardName = "挨打者", Supertype = Cardtype.Creature, Power = 0, Life = 10,
            });
            sVictim.SetController(sp2);
            VerifySuite.Assert(score.ZoneManager.TryAddToBattlefield(sVictim, sp2), "挨打者入场（角色攻击目标）");
            int sVLife0 = sVictim.GetLife();
            VerifySuite.Assert(GameActions.DeclareAttack(score, sp1, sp1, sVictim), "角色攻击宣言入栈（Player 攻击者）");
            GameActions.DrainStack(score);
            score.SBAEngine.ExecuteAll();
            VerifySuite.Assert(sVictim.GetLife() == sVLife0 - 3,
                $"角色攻击全额 3 点战斗伤害（实际掉 {sVLife0 - sVictim.GetLife()}）");
            VerifySuite.Assert(sp1.GetCounterCount(CounterRules.HeroAttackCounter) == 0,
                "攻击结算后弹药烧光（攻击与反击共用同一份）");
            VerifySuite.Assert(sp1.GetPower() == 0 && !score.CombatSystem.CanDeclareAttack(sp1, sp1),
                "烧光后读数归零、同回合不可再宣（弹药即闸门——无专用次数计数器）");

            // ---- ③ 反伤闭环：防守方角色持弹药 → 反伤=层数并烧光；同回合再受击反伤 0 ----
            sp2.AddCounters(CounterRules.HeroAttackCounter, 4);
            var sAtk1 = new CardWrapper(new CardData
            {
                ID = "V9S_atk1", CardName = "攻门者一", Supertype = Cardtype.Creature, Power = 2, Life = 10,
            });
            sAtk1.SetController(sp1);
            VerifySuite.Assert(score.ZoneManager.TryAddToBattlefield(sAtk1, sp1), "攻门者一入场");
            sAtk1.Untap();
            int sA1Life0 = sAtk1.GetLife();
            VerifySuite.Assert(GameActions.DeclareAttack(score, sp1, sAtk1, sp2), "生物攻击角色宣言入栈");
            GameActions.DrainStack(score);
            score.SBAEngine.ExecuteAll();
            VerifySuite.Assert(sAtk1.GetLife() == sA1Life0 - 4,
                $"角色反伤=弹药 4 点（实际掉 {sA1Life0 - sAtk1.GetLife()}）");
            VerifySuite.Assert(sp2.GetCounterCount(CounterRules.HeroAttackCounter) == 0, "反伤结算后弹药烧光");

            var sAtk2 = new CardWrapper(new CardData
            {
                ID = "V9S_atk2", CardName = "攻门者二", Supertype = Cardtype.Creature, Power = 2, Life = 10,
            });
            sAtk2.SetController(sp1);
            VerifySuite.Assert(score.ZoneManager.TryAddToBattlefield(sAtk2, sp1), "攻门者二入场");
            sAtk2.Untap();
            int sA2Life0 = sAtk2.GetLife();
            VerifySuite.Assert(GameActions.DeclareAttack(score, sp1, sAtk2, sp2), "同回合第二次攻击角色宣言入栈");
            GameActions.DrainStack(score);
            score.SBAEngine.ExecuteAll();
            VerifySuite.Assert(sAtk2.GetLife() == sA2Life0,
                "弹药已烧光 → 同回合再受击反伤 0（反击限制=共用弹药，无次数计数器）");

            // ---- ④ 缴械：压制角色反伤且不烧弹药 ----
            sp2.AddCounters(CounterRules.HeroAttackCounter, 2);
            var sDisarmer = new CardWrapper(new CardData
            {
                ID = "V9S_disarm", CardName = "缴械者", Supertype = Cardtype.Creature, Power = 2, Life = 10,
            });
            sDisarmer.SetController(sp1);
            VerifySuite.Assert(score.ZoneManager.TryAddToBattlefield(sDisarmer, sp1), "缴械者入场");
            sDisarmer.Untap();
            sDisarmer.AddKeyword(KeywordRules.Disarm);
            int sDLife0 = sDisarmer.GetLife();
            VerifySuite.Assert(GameActions.DeclareAttack(score, sp1, sDisarmer, sp2), "缴械攻击角色宣言入栈");
            GameActions.DrainStack(score);
            score.SBAEngine.ExecuteAll();
            VerifySuite.Assert(sDisarmer.GetLife() == sDLife0, "缴械压制角色反伤（0 伤害）");
            VerifySuite.Assert(sp2.GetCounterCount(CounterRules.HeroAttackCounter) == 2,
                "缴械压制反伤不烧弹药（无反伤结算=无消耗）");

            // ---- ⑤ 关键词光环角色格通道 + 属性/作用面排除 ----
            GameBoard.LinkAuraSystem.Detach();
            var sBoard = new GameBoard.BoardState(score, sp1, sp2,
                GameBoard.HalfFieldData.Flat(), GameBoard.HalfFieldData.Flat());
            sBoard.EnableAutoResync();
            GameBoard.LinkAuraSystem.Attach(sBoard);

            var sAuraSrc = new CardWrapper(new CardData
            {
                ID = "V9S_auraSrc", CardName = "授勋者", Supertype = Cardtype.Creature, Power = 1, Life = 5,
            });
            sAuraSrc.SetController(sp1);
            VerifySuite.Assert(score.ZoneManager.TryAddToBattlefield(sAuraSrc, sp1), "光环源入场");

            // 找与角色格相邻的己方单位格 + 指向角色格的方向（箭头几何由 BoardMath 决定，不硬编码）
            var sCharCell = GameBoard.BoardLayout.CharacterCell(0);
            int? sPinX = null; int sPinZ = 0; var sCharDir = GameBoard.BoardDirection.NE;
            foreach (var (ux, uz) in GameBoard.BoardLayout.UnitCells(0))
            {
                if (sPinX.HasValue) break;
                foreach (GameBoard.BoardDirection d in Enum.GetValues(typeof(GameBoard.BoardDirection)))
                {
                    var (nx, nz) = GameBoard.BoardMath.Neighbor(ux, uz, d);
                    if (nx == sCharCell.Item1 && nz == sCharCell.Item2)
                    {
                        sPinX = ux; sPinZ = uz; sCharDir = d;
                        break;
                    }
                }
            }
            VerifySuite.Assert(sPinX.HasValue, "存在与角色格相邻的己方单位格（箭头几何可达角色）");
            sBoard.PinCard(sAuraSrc, sPinX.Value, sPinZ);
            sBoard.Resync();
            GameBoard.LinkAuraSystem.InvalidateCache();

            var sAuraData = sAuraSrc.GetData();
            sAuraData.ArrowDirections = GameBoard.BoardMath.ArrowOf(sCharDir);
            // 角色通道示例词改用合成 id（2026-10-08 坚韧指示物化：坚韧行 MountKinds=指示物退出
            // 关键词/光环族，不再是可投递示例）。运行时投递对未登记 id 放行（RoleChannelBlocked
            // 目录缺失=放行）——与旧坚韈权限口行为一致，通道机制断言语义不变。
            const string RoleProbe = "V9RoleProbe";
            sAuraData.LinkAuras.Add(new LinkAuraData { keyword = RoleProbe });
            sBoard.Resync();
            GameBoard.LinkAuraSystem.InvalidateCache();
            VerifySuite.Assert(sp1.HasKeyword(RoleProbe),
                "关键词光环箭头指向角色格 → 角色光环期间视为持有（HasKeyword 并入）");
            VerifySuite.Assert(!sp2.HasKeyword(RoleProbe), "对方角色不受本侧角色格箭头影响");

            sAuraData.LinkAuras.Clear();
            sAuraData.LinkAuras.Add(new LinkAuraData { stat = "Power", value = 5 });
            sBoard.Resync();
            GameBoard.LinkAuraSystem.InvalidateCache();
            VerifySuite.Assert(sp1.GetPower() == 0,
                "属性（攻）光环指向角色格不投递——角色攻击力=纯弹药读数（属性光环钉死生物专用）");

            sAuraData.LinkAuras.Clear();
            sAuraData.LinkAuras.Add(new LinkAuraData { keyword = RoleProbe, scope = 1 }); // 己方作用面（条目级，不经箭头几何）
            sAuraData.AuraScope = 0;
            sAuraData.ArrowDirections = CardCore.HexDirection.None;
            sBoard.Resync();
            GameBoard.LinkAuraSystem.InvalidateCache();
            // 2026-10-09 裁定：关键词光环一律可作用角色——不能用光环表达的行已由表
            // 「不可作为连接光环」位拉黑，通道不再设防（逐条目 role 与 NoRole 硬闸整体退役）
            VerifySuite.Assert(sp1.HasKeyword(RoleProbe),
                "作用面（己方）关键词条目 → 己方角色无条件获得（2026-10-09 角色通道开放）");
            VerifySuite.Assert(!sp2.HasKeyword(RoleProbe), "对方角色不在己方作用面（scope=1）内");

            sAuraData.LinkAuras[0].scope = 3; // 对方作用面 → 投递对手角色
            sBoard.Resync();
            GameBoard.LinkAuraSystem.InvalidateCache();
            VerifySuite.Assert(!sp1.HasKeyword(RoleProbe), "己方角色不在对方作用面（scope=3）内");
            VerifySuite.Assert(sp2.HasKeyword(RoleProbe),
                "作用面（对方）关键词条目 → 对手角色无条件获得");

            // 2026-10-09 裁定后的唯一限制口=表「不可作为连接光环」拉黑位（合成器库+装载校验层拦）：
            // 守护（配对制）/再生（角色回满太强）/禁魔石（角色免疫洞）三行不在光环面——运行时不再设防。
            VerifySuite.Assert(!CardCore.ComposerCatalog.IsAuraMountableKeyword(KeywordRules.Regeneration)
                   && !CardCore.ComposerCatalog.IsAuraMountableKeyword(KeywordRules.Spellban)
                   && !CardCore.ComposerCatalog.IsAuraMountableKeyword(CardCore.Attribute.KeywordRules.Guardian),
                "光环黑名单：再生/禁魔石/守护不可作光环（角色免疫洞与持续语义冲突由拉黑关闭——通道开放的边界）");

            // 对照：未登记关键词（RoleProbe 同权限口）箭头指角色格照投
            sAuraData.LinkAuras[0] = new LinkAuraData { keyword = RoleProbe };
            sAuraData.ArrowDirections = GameBoard.BoardMath.ArrowOf(sCharDir);
            sBoard.Resync();
            GameBoard.LinkAuraSystem.InvalidateCache();
            VerifySuite.Assert(sp1.HasKeyword(RoleProbe),
                "箭头档对照：未拉黑关键词箭头指角色格照投（通道无门——限制全在表拉黑位）");

            // ---- 缄默×连接光环（2026-10-19 规格定案：缄默光环本体才让光环无效） ----
            // 来源被缄默 → 条目失效；受益者被缄默 → 光环照常（只认来源侧，打受益者不除光环）
            sAuraSrc.AddCounters(CounterRules.NullifyCounter, 1, sp2);
            GameBoard.LinkAuraSystem.InvalidateCache();
            VerifySuite.Assert(!sp1.HasKeyword(RoleProbe),
                "来源被缄默：关键词光环失效（LinkAuraSystem 来源闸——条目随来源整体失效）");
            sAuraSrc.AddCounters(CounterRules.NullifyCounter, -1, sp2);
            GameBoard.LinkAuraSystem.InvalidateCache();
            VerifySuite.Assert(sp1.HasKeyword(RoleProbe), "来源缄默解除：光环恢复");
            sp1.AddCounters(CounterRules.NullifyCounter, 1, sp2);
            GameBoard.LinkAuraSystem.InvalidateCache();
            VerifySuite.Assert(sp1.HasKeyword(RoleProbe),
                "受益者被缄默：光环投递照常——缄默打在受益者身上不除光环（只认来源侧）");
            sp1.AddCounters(CounterRules.NullifyCounter, -1, sp2);
            GameBoard.LinkAuraSystem.InvalidateCache();

            // ---- 坚韧指示物（2026-10-09 生效自减改版，同毒素档）：受伤每层 −1 → 实际拦到即生效，
            //      生效后层数减半（floor，1 层生效一次即清零）；可叠加；角色同口径；换区不清 ----
            var toughUnit = new CardWrapper(new CardData
            {
                ID = "V9S_tough", CardName = "坚韧载体", Supertype = Cardtype.Creature, Power = 1, Life = 20,
            });
            toughUnit.SetController(sp1);
            VerifySuite.Assert(score.ZoneManager.TryAddToBattlefield(toughUnit, sp1), "坚韧载体入场");
            toughUnit.AddCounters(CardCore.Attribute.CounterRules.ToughnessCounter, 2, sp1);
            VerifySuite.Assert(KeywordRules.ApplyDamage(sAuraSrc, toughUnit, 5, isCombat: false) == 3
                && toughUnit.GetLife() == 17,
                "坚韧指示物：2 层 → 5 伤实扣 3（20→17）");
            VerifySuite.Assert(toughUnit.GetCounterCount(CardCore.Attribute.CounterRules.ToughnessCounter) == 1,
                "坚韧生效自减：拦减后层数减半（2→1，毒素同式 floor）");
            VerifySuite.Assert(KeywordRules.ApplyDamage(sAuraSrc, toughUnit, 2, isCombat: false, pierce: true) == 2
                && toughUnit.GetCounterCount(CardCore.Attribute.CounterRules.ToughnessCounter) == 1,
                "穿透伤害越坚韧层且不减半（未拦到=未生效；与圣盾/护甲同跳过）");
            VerifySuite.Assert(KeywordRules.ApplyDamage(sAuraSrc, toughUnit, 5, isCombat: false) == 4
                && toughUnit.GetLife() == 11,
                "坚韧第二击：余 1 层减 1（5 伤实扣 4）");
            VerifySuite.Assert(toughUnit.GetCounterCount(CardCore.Attribute.CounterRules.ToughnessCounter) == 0,
                "坚韧 1 层生效一次即清零（floor(1/2)=0）");
            // 换区存活（生效自减档）：拦减后余层跨区保留——移手牌不清（旧 Resident 换区清口径已废）
            toughUnit.AddCounters(CardCore.Attribute.CounterRules.ToughnessCounter, 3, sp1);
            KeywordRules.ApplyDamage(sAuraSrc, toughUnit, 1, isCombat: false); // 1 伤全拦（层 3→1，生命不动）
            score.ZoneManager.MoveCard(toughUnit, sp1, Zone.Battlefield, Zone.Hand);
            VerifySuite.Assert(toughUnit.GetCounterCount(CardCore.Attribute.CounterRules.ToughnessCounter) == 1,
                "坚韧换区不清（Exception 档跨区存活）");
            // 角色持有（旧关键词形态 is Card 死线消解）：2 层 → 4 伤实扣 2，生效减半余 1 层
            int sp1LifeBefore = sp1.Life;
            sp1.AddCounters(CardCore.Attribute.CounterRules.ToughnessCounter, 2, sp1);
            KeywordRules.ApplyDamage(sAuraSrc, sp1, 4, isCombat: false);
            VerifySuite.Assert(sp1.Life == sp1LifeBefore - 2
                && sp1.GetCounterCount(CardCore.Attribute.CounterRules.ToughnessCounter) == 1,
                $"坚韧指示物·角色：2 层 4 伤实扣 2（{sp1LifeBefore}→{sp1.Life}），生效减半余 1 层");
            sp1.AddCounters(CardCore.Attribute.CounterRules.ToughnessCounter, -1); // 还原

            // ---- 易损指示物（2026-10-09 生效自减改版，同毒素档）：受伤每层 +1 → 生效后层数减半（floor）；
            //      退出衰退族（无回合末倒数）；换区不清 ----
            var brittleUnit = new CardWrapper(new CardData
            {
                ID = "V9S_vuln", CardName = "易损载体", Supertype = Cardtype.Creature, Power = 1, Life = 30,
            });
            brittleUnit.SetController(sp2);
            VerifySuite.Assert(score.ZoneManager.TryAddToBattlefield(brittleUnit, sp2), "易损载体入场");
            brittleUnit.AddCounters(CounterRules.VulnerableCounter, 4, sp1);
            VerifySuite.Assert(KeywordRules.ApplyDamage(sAuraSrc, brittleUnit, 3, isCombat: false) == 7
                && brittleUnit.GetLife() == 23,
                "易损指示物：4 层 → 3 伤放大为 7（防护层前放大，30→23）");
            VerifySuite.Assert(brittleUnit.GetCounterCount(CounterRules.VulnerableCounter) == 2,
                "易损生效自减：放大后层数减半（4→2）");
            var vulnSpec = CounterRules.Find(CounterRules.VulnerableCounter);
            VerifySuite.Assert(vulnSpec.Class == CounterClass.Exception
                              && vulnSpec.TickPolicy != CounterTickPolicy.TurnEndTickDown,
                "易损已退出衰退族（CounterSpec=Exception 档、无回合末倒数）");
            VerifySuite.Assert(KeywordRules.ApplyDamage(sAuraSrc, brittleUnit, 3, isCombat: false) == 5
                && brittleUnit.GetLife() == 18,
                "易损第二击：余 2 层 → 3 伤放大为 5（层 2→1）");
            VerifySuite.Assert(KeywordRules.ApplyDamage(sAuraSrc, brittleUnit, 3, isCombat: false) == 4
                && brittleUnit.GetLife() == 14,
                "易损第三击：余 1 层 → 3 伤放大为 4，层清零");
            VerifySuite.Assert(KeywordRules.ApplyDamage(sAuraSrc, brittleUnit, 3, isCombat: false) == 3,
                "易损层耗尽后裸伤（无放大）");
            brittleUnit.AddCounters(CounterRules.VulnerableCounter, 2, sp1);
            VerifySuite.Assert(KeywordRules.ApplyDamage(sAuraSrc, brittleUnit, 3, isCombat: false, pierce: true) == 5
                && brittleUnit.GetCounterCount(CounterRules.VulnerableCounter) == 1,
                "易损对穿透伤害照常放大并减半（放大在防护层前，穿透不豁免）");

            // ---- 报复指示物（2026-10-16 新增，红1/层·极性+1·生效自减档）：实际受到伤害 →
            //      对来源反噬=层数的非战斗伤害（来源=持有者），生效后层数减半（floor）；
            //      **先减半再反击**——互带时层数几何衰减收敛（防无限互弹）；自伤/null 来源不触发；
            //      完全挡住不触发（同潜行受伤失效口径）；穿透伤害照常触发 ----
            var retalRow = AtomicEffectTable.GetByType(AtomicEffectType.GrantRetaliation);
            VerifySuite.Assert(retalRow != null
                              && Math.Abs(retalRow.ManaList[ManaType.Red] - 1f) < 0.01f
                              && retalRow.TotalUnitCost >= 1f
                              && retalRow.Polarity > 0,
                $"报复表行锚：在表且红1/层·极性+1（GrantRetaliation，计价=行锚×层数同指示物统一口径）——" +
                $"实际 row={(retalRow != null ? "有" : "无")}·红={(retalRow?.ManaList != null ? retalRow.ManaList[ManaType.Red].ToString() : "null")}·" +
                $"总价={retalRow?.TotalUnitCost}·极性={retalRow?.Polarity}");
            var retalSrc = new CardWrapper(new CardData
            {
                ID = "V9S_retalSrc", CardName = "报复靶", Supertype = Cardtype.Creature, Power = 1, Life = 30,
            });
            retalSrc.SetController(sp1);
            VerifySuite.Assert(score.ZoneManager.TryAddToBattlefield(retalSrc, sp1), "报复靶入场");
            var retalUnit = new CardWrapper(new CardData
            {
                ID = "V9S_retal", CardName = "报复载体", Supertype = Cardtype.Creature, Power = 1, Life = 20,
            });
            retalUnit.SetController(sp2);
            VerifySuite.Assert(score.ZoneManager.TryAddToBattlefield(retalUnit, sp2), "报复载体入场");
            retalUnit.AddCounters(CounterRules.RetaliationCounter, 4, sp2);
            VerifySuite.Assert(KeywordRules.ApplyDamage(retalSrc, retalUnit, 3, isCombat: false) == 3
                && retalSrc.GetLife() == 26,
                "报复指示物：4 层受 3 伤 → 对来源反噬 4（非战斗伤害，30→26）");
            VerifySuite.Assert(retalUnit.GetCounterCount(CounterRules.RetaliationCounter) == 2,
                "报复生效自减：反噬后层数减半（4→2，floor，毒素同式）");
            KeywordRules.ApplyDamage(null, retalUnit, 2, isCombat: false);
            VerifySuite.Assert(retalUnit.GetCounterCount(CounterRules.RetaliationCounter) == 2
                && retalSrc.GetLife() == 26,
                "报复对 null 来源不触发（毒素式无来源伤害无从反噬——层/靶不动）");
            KeywordRules.ApplyDamage(retalUnit, retalUnit, 2, isCombat: false);
            VerifySuite.Assert(retalUnit.GetCounterCount(CounterRules.RetaliationCounter) == 2,
                "报复对自伤不触发（报复对象须为外部伤害来源）");
            retalUnit.AddCounters(CounterRules.DivineShieldCounter, 1, sp2);
            KeywordRules.ApplyDamage(retalSrc, retalUnit, 3, isCombat: false);
            VerifySuite.Assert(retalUnit.GetCounterCount(CounterRules.RetaliationCounter) == 2
                && retalSrc.GetLife() == 26,
                "圣盾完全挡住=未受到伤害，报复不触发（伤害落定为唯一门）");
            VerifySuite.Assert(KeywordRules.ApplyDamage(retalSrc, retalUnit, 1, isCombat: false, pierce: true) == 1
                && retalSrc.GetLife() == 24
                && retalUnit.GetCounterCount(CounterRules.RetaliationCounter) == 1,
                "报复对穿透伤害照常反噬并减半（穿透不豁免；2→1，26→24）");
            retalUnit.AddCounters(CounterRules.RetaliationCounter, -1, sp2); // 还原（层清零）
            // 互带报复：先减半再反击=几何衰减收敛。2 伤引发 4/4/2/2/1/1 反噬链（甲 60→53、乙 60→51）
            var duelA = new CardWrapper(new CardData
            {
                ID = "V9S_retdA", CardName = "互带报复甲", Supertype = Cardtype.Creature, Power = 1, Life = 60,
            });
            var duelB = new CardWrapper(new CardData
            {
                ID = "V9S_retdB", CardName = "互带报复乙", Supertype = Cardtype.Creature, Power = 1, Life = 60,
            });
            duelA.SetController(sp1);
            duelB.SetController(sp2);
            VerifySuite.Assert(score.ZoneManager.TryAddToBattlefield(duelA, sp1), "互带报复甲入场");
            VerifySuite.Assert(score.ZoneManager.TryAddToBattlefield(duelB, sp2), "互带报复乙入场");
            duelA.AddCounters(CounterRules.RetaliationCounter, 4, sp1);
            duelB.AddCounters(CounterRules.RetaliationCounter, 4, sp2);
            KeywordRules.ApplyDamage(duelA, duelB, 2, isCombat: false);
            VerifySuite.Assert(duelA.GetCounterCount(CounterRules.RetaliationCounter) == 0
                && duelB.GetCounterCount(CounterRules.RetaliationCounter) == 0,
                "互带报复几何衰减收敛：先减半再反击，双方层归零（无无限互弹）");
            VerifySuite.Assert(duelA.GetLife() == 53 && duelB.GetLife() == 51,
                $"互带报复伤害账：2 伤引发 4/4/2/2/1/1 反噬链（甲 60→{duelA.GetLife()}、乙 60→{duelB.GetLife()}）");

            // ---- 圣盾指示物化（2026-10-08 定案，承「生效后移除」型关键词改造）：每层挡一次任意伤害、
            //      消耗 1 层；可叠加（多层=多挡。注：关键词台账 2026-10-09 起全轨取代不叠加，
            //      「多份」语义现仅存于指示物层）----
            var twinUnit = new CardWrapper(new CardData
            {
                ID = "V9S_kwstack", CardName = "圣盾双份载体", Supertype = Cardtype.Creature, Power = 1, Life = 9,
            });
            twinUnit.SetController(sp1);
            VerifySuite.Assert(score.ZoneManager.TryAddToBattlefield(twinUnit, sp1), "圣盾双份载体入场");
            twinUnit.AddCounters(CounterRules.DivineShieldCounter, 2, sp1); // 两层=两份（旧 Printed+Temp 双份等价锚）
            VerifySuite.Assert(KeywordRules.ApplyDamage(sAuraSrc, twinUnit, 4, isCombat: false) == 0
                && twinUnit.GetCounterCount(CounterRules.DivineShieldCounter) == 1,
                "圣盾指示物：消耗 1 层挡一次，余层仍生效");
            VerifySuite.Assert(KeywordRules.ApplyDamage(sAuraSrc, twinUnit, 4, isCombat: false) == 0
                && twinUnit.GetCounterCount(CounterRules.DivineShieldCounter) == 0,
                "两层先后耗尽");
            VerifySuite.Assert(KeywordRules.ApplyDamage(sAuraSrc, twinUnit, 4, isCombat: false) == 4
                && twinUnit.GetLife() == 5,
                "层耗尽后正常受伤（9→5）");

            // ---- 2026-10-08 终版：剧毒关键词（落定追加式消灭）----
            var venomSrc = new CardWrapper(new CardData
            {
                ID = "V9S_venom", CardName = "剧毒爪牙", Supertype = Cardtype.Creature, Power = 2, Life = 5,
            });
            venomSrc.SetController(sp1);
            VerifySuite.Assert(score.ZoneManager.TryAddToBattlefield(venomSrc, sp1), "剧毒爪牙入场");
            venomSrc.AddKeyword(KeywordRules.Venom, CardCore.KeywordLane.Printed);
            var venomPrey = new CardWrapper(new CardData
            {
                ID = "V9S_prey", CardName = "剧毒猎物", Supertype = Cardtype.Creature, Power = 1, Life = 9,
            });
            venomPrey.SetController(sp2);
            VerifySuite.Assert(score.ZoneManager.TryAddToBattlefield(venomPrey, sp2), "剧毒猎物入场");
            KeywordRules.ApplyDamage(venomSrc, venomPrey, 2, isCombat: true);
            VerifySuite.Assert(!venomPrey.IsAlive || venomPrey.GetZone() == Zone.Graveyard,
                "剧毒关键词：受其战斗伤害的生物被消灭（2 伤落定→TryKill，剩余生命无关）");
            // 对照①：圣盾完全挡住（落定=0）不触发消灭
            var venomShielded = new CardWrapper(new CardData
            {
                ID = "V9S_vshld", CardName = "圣盾猎物", Supertype = Cardtype.Creature, Power = 1, Life = 9,
            });
            venomShielded.SetController(sp2);
            VerifySuite.Assert(score.ZoneManager.TryAddToBattlefield(venomShielded, sp2), "圣盾猎物入场");
            venomShielded.AddCounters(CounterRules.DivineShieldCounter, 1, sp1);
            KeywordRules.ApplyDamage(venomSrc, venomShielded, 4, isCombat: true);
            VerifySuite.Assert(venomShielded.IsAlive && venomShielded.GetLife() == 9
                && venomShielded.GetCounterCount(CounterRules.DivineShieldCounter) == 0,
                "剧毒对照：圣盾挡下（落定 0）不触发消灭，圣盾照常消耗");
            // 对照②：对角色照常落血不消灭
            int sp2Life0 = sp2.Life;
            KeywordRules.ApplyDamage(venomSrc, sp2, 3, isCombat: true);
            VerifySuite.Assert(sp2.Life == sp2Life0 - 3 && sp2.IsAlive,
                "剧毒对照：对角色照常落血不消灭（Deathtouch 口径）");

            // ---- 2026-10-08 终版：毒素例外例程（回合末受伤=层数后减半 floor）+ 冻结多回合倒数 ----
            var toxinUnit = new CardWrapper(new CardData
            {
                ID = "V9S_toxin", CardName = "毒素载体", Supertype = Cardtype.Creature, Power = 1, Life = 30,
            });
            toxinUnit.SetController(sp1);
            VerifySuite.Assert(score.ZoneManager.TryAddToBattlefield(toxinUnit, sp1), "毒素载体入场");
            toxinUnit.AddCounters(CounterRules.ToxinCounter, 4, sp1);
            var freezeUnit = new CardWrapper(new CardData
            {
                ID = "V9S_frz3", CardName = "三重冻结载体", Supertype = Cardtype.Creature, Power = 1, Life = 9,
            });
            freezeUnit.SetController(sp1);
            VerifySuite.Assert(score.ZoneManager.TryAddToBattlefield(freezeUnit, sp1), "三重冻结载体入场");
            freezeUnit.Freeze(3);
            VerifySuite.Assert(freezeUnit.GetCounterCount(KeywordRules.FreezeCounter) == 3,
                "冻结施加 3 层（层=持续回合）");
            int toxinLife0 = toxinUnit.GetLife();
            CounterRules.OnTurnEnd(sp1, score.ZoneManager);
            VerifySuite.Assert(toxinUnit.GetLife() == toxinLife0 - 4
                              && toxinUnit.GetCounterCount(CounterRules.ToxinCounter) == 2
                              && freezeUnit.GetCounterCount(KeywordRules.FreezeCounter) == 2,
                "回合末①：毒素 4 层受 4 伤后余 2；③：冻结 3→2（同一次回合末并进）");
            toxinLife0 = toxinUnit.GetLife();
            CounterRules.OnTurnEnd(sp1, score.ZoneManager);
            VerifySuite.Assert(toxinUnit.GetLife() == toxinLife0 - 2
                              && toxinUnit.GetCounterCount(CounterRules.ToxinCounter) == 1
                              && freezeUnit.GetCounterCount(KeywordRules.FreezeCounter) == 1,
                "回合末②：毒素 2 层受 2 伤后余 1；冻结 2→1");
            toxinLife0 = toxinUnit.GetLife();
            CounterRules.OnTurnEnd(sp1, score.ZoneManager);
            VerifySuite.Assert(toxinUnit.GetLife() == toxinLife0 - 1
                              && toxinUnit.GetCounterCount(CounterRules.ToxinCounter) == 0
                              && freezeUnit.GetCounterCount(KeywordRules.FreezeCounter) == 0,
                "回合末③：毒素 1 层受 1 伤后余 0（1/2=0.5→0）；冻结 1→0 解除");

            // ---- ⑥ 角色守卫（2026-10-16 战场UI三键改版）：弹药>0 才进候选；拦截=攻击目标改写为角色，
            //      双向结算吃伤+弹药反伤、反伤后全烧（与被打同口径）----
            var guardVictim = new CardWrapper(new CardData
            {
                ID = "V9S_gVictim", CardName = "守卫戏肉", Supertype = Cardtype.Creature, Power = 1, Life = 10,
            });
            guardVictim.SetController(sp2);
            VerifySuite.Assert(score.ZoneManager.TryAddToBattlefield(guardVictim, sp2), "守卫戏肉入场");
            // 2026-10-17 取消横置入场：前段经 TryAddToBattlefield 入场的 sp2 残留生物现已可用——
            // 整队显式横置，保住「守卫候选=角色独占」夹具前提（旧规则下入场自带横置天然满足）
            foreach (var c in score.ZoneManager.GetCards(sp2, Zone.Battlefield)) c.Tap();
            var guardAtk = new CardWrapper(new CardData
            {
                ID = "V9S_gAtk", CardName = "守卫攻门者", Supertype = Cardtype.Creature, Power = 2, Life = 10,
            });
            guardAtk.SetController(sp1);
            VerifySuite.Assert(score.ZoneManager.TryAddToBattlefield(guardAtk, sp1), "守卫攻门者入场");
            guardAtk.Untap();
            VerifySuite.Assert(GameActions.DeclareAttack(score, sp1, guardAtk, guardVictim), "攻守卫戏肉宣言入栈");

            sp2.AddCounters(CounterRules.HeroAttackCounter, -sp2.GetCounterCount(CounterRules.HeroAttackCounter)); // 弹药清零
            VerifySuite.Assert(!GameActions.CollectAvailableResponses(score, sp2)
                    .Exists(o => o.Kind == ResponseOption.ResponseKind.GuardAbility && o.SourcePlayer == sp2),
                "角色无弹药 → 不进守卫候选（弹药即守卫资格门）");
            sp2.AddCounters(CounterRules.HeroAttackCounter, 3);
            var guardOpts = GameActions.CollectAvailableResponses(score, sp2)
                .Where(o => o.Kind == ResponseOption.ResponseKind.GuardAbility).ToList();
            VerifySuite.Assert(guardOpts.Exists(o => o.SourcePlayer == sp2 && o.SourceCard == null),
                "角色有弹药 3 → 进守卫候选（SourcePlayer 携带、SourceCard 恒 null）");
            VerifySuite.Assert(guardOpts.All(o => o.SourcePlayer != null),
                "守卫候选=角色独占（戏肉横置无守卫能力旁证——无生物候选混入）");

            var heroOpt = guardOpts.First(o => o.SourcePlayer == sp2);
            VerifySuite.Assert(score.StackEngine.PushGuardDeclaration(sp2, heroOpt.AttackInstance, sp2),
                "角色守卫入栈（PushGuardDeclaration 放行 Player 拦截者）");
            int gSp2Life0 = sp2.Life;
            GameActions.DrainStack(score);
            score.SBAEngine.ExecuteAll();
            VerifySuite.Assert(guardVictim.GetLife() == 10,
                "守卫拦截：宣言原目标（戏肉）不落伤——主战斗改写为角色");
            VerifySuite.Assert(sp2.Life == gSp2Life0 - 2 && sp2.IsAlive,
                $"角色守卫吃攻 2 点（{gSp2Life0}→{sp2.Life}）");
            VerifySuite.Assert(guardAtk.GetLife() == 7, "角色守卫反伤=弹药 3 点（攻门者 10→7）");
            VerifySuite.Assert(sp2.GetCounterCount(CounterRules.HeroAttackCounter) == 0,
                "守卫反伤结算后弹药烧光（ResolvePair 既有 Player 目标路径）");

            // ============================ V9.t 关键词不叠加·台账全轨取代制（2026-10-08 定案；2026-10-09 修订彻底不叠加） ============================
            VerifySuite.Section("V9.t 关键词不叠加：全轨取代（同轨/跨轨皆取代）/消耗整词撤/无限档");
            var noStack = new CardWrapper(new CardData
            {
                ID = "V9T_nostack", CardName = "不叠加载体", Supertype = Cardtype.Creature, Power = 1, Life = 9,
            });
            noStack.SetController(sp1);
            VerifySuite.Assert(score.ZoneManager.TryAddToBattlefield(noStack, sp1), "不叠加载体入场");
            // ① 同轨重复授予=取代（不叠加定案核心）：新实例刷新值/次数，本体占位单份
            noStack.AddKeyword(KeywordRules.Lifesteal, CardCore.KeywordLane.Temp, sp1, 2, 3);
            noStack.AddKeyword(KeywordRules.Lifesteal, CardCore.KeywordLane.Temp, sp2, 5, 1);
            VerifySuite.Assert(noStack.GetKeywordCount(KeywordRules.Lifesteal) == 1
                               && noStack.GetKeywordValue(KeywordRules.Lifesteal) == 5
                               && noStack.GetKeywordLimit(KeywordRules.Lifesteal) == 1,
                "同轨取代：重复授予刷新值/次数（2/3→5/1），_keywords 单份");
            // ② 跨轨授予也=取代（2026-10-09 修订：附加份不补文本份、文本份不补附加份）——
            //    文本轨（Printed）再授予直接顶掉附加状态（Temp）份：台账单实例、读数=最新实例（非 Max）
            noStack.AddKeyword(KeywordRules.Lifesteal, CardCore.KeywordLane.Printed, null, 3, 2);
            VerifySuite.Assert(noStack.GetKeywordCount(KeywordRules.Lifesteal) == 1
                               && noStack._keywordGrants.Count(g => g.Keyword == KeywordRules.Lifesteal) == 1
                               && noStack.GetKeywordValue(KeywordRules.Lifesteal) == 3
                               && noStack.GetKeywordLimit(KeywordRules.Lifesteal) == 2,
                "跨轨取代：文本份顶掉 Temp 份（台账单实例，读数=最新 3/2——旧 5/1 不残留、非跨轨 Max）");
            // ③ 消耗型移除=整词撤除（2026-10-09：无跨轨回补）：撤掉当前唯一台账实例后整词下岗
            noStack.RemoveKeyword(KeywordRules.Lifesteal);
            VerifySuite.Assert(!noStack.HasKeyword(KeywordRules.Lifesteal)
                               && noStack.GetKeywordValue(KeywordRules.Lifesteal) == 0,
                "消耗整词撤：撤除唯一实例后不再持有（无其余轨接续/回补）");
            // ③' 消耗归零后再授予=全新实例（取代制非存量接续）：值/次数随新授予入账
            noStack.AddKeyword(KeywordRules.Lifesteal, CardCore.KeywordLane.Temp, sp2, 4, 1);
            VerifySuite.Assert(noStack.HasKeyword(KeywordRules.Lifesteal)
                               && noStack.GetKeywordValue(KeywordRules.Lifesteal) == 4
                               && noStack.GetKeywordLimit(KeywordRules.Lifesteal) == 1,
                "再授予=全新实例：4/1 入账（非旧存量 3/2 或 5/1 接续）");
            // ④ Limit -1（无限档）：当前实例 -1 → 读数 -1
            noStack.AddKeyword(KeywordRules.Disarm, CardCore.KeywordLane.Setting, sp1, 1, -1);
            VerifySuite.Assert(noStack.GetKeywordLimit(KeywordRules.Disarm) == -1,
                "无限档：实例 Limit=-1 → GetKeywordLimit=-1");

            GameBoard.LinkAuraSystem.Detach();
            sBoard.Dispose();

            // ============================ V9.u 逐原子目标制（2026-10-09 定案） ============================
            // 无效果级作用范围声明 → 各原子按自身极性过滤域独立解析（弹窗落结算期·无头自动代选）；
            // 数量档/随机档各原子各自生效；单条效果作用域并集上限 4；header 声明=旧共享口径不回归。
            VerifySuite.Section("V9.u 逐原子目标制：域不交并列独立结算+无域共存+强制全取+并集>4兜底+共享不回归");

            {
                var ucore = GameCore.Instance;
                ucore.Reset();
                ZoneContainer.Reseed(20261009);
                var uFill = Enumerable.Range(0, 8).Select(i => new CardData
                {
                    ID = $"V9U_fill_{i}", CardName = "V9U填充" + i, Supertype = Cardtype.Creature, Power = 1, Life = 1,
                }).ToList();
                ucore.InitGame(CardLoader.BuildDeck(uFill, 2), CardLoader.BuildDeck(uFill, 2));
                var u1 = ucore.Player1;
                var u2 = ucore.Player2;
                GameActions.SkipElementPool(ucore, u1); // Standby → Main（u1 主阶段）

                // ---- ① 判别与域：无 header=PerAtomTargets+并集；header 声明=共享不回归 ----
                var uDmgEntry = AtomRefs.New(AtomicEffectType.DealDamage, value: 2);          // p=-1 {1,2}→{2}
                var uPlusEntry = AtomRefs.New(AtomicEffectType.AddPermanentPlusOne, value: 1); // p=+1 {1,2}→{1}
                var uPerAtomDef = CardEffectConverter.ConvertOne(new CardEffectData
                {
                    Id = "V9U_PA",
                    AtomicEffects = new List<AtomicEffectEntry> { uDmgEntry, uPlusEntry },
                }, "V9U_PA");
                VerifySuite.Assert(uPerAtomDef.PerAtomTargets
                                   && TargetKindRules.Format(uPerAtomDef.TargetDomain) == "1,2,3,4",
                    $"无 header=逐原子+并集域（flag={uPerAtomDef.PerAtomTargets} "
                    + $"域=[{TargetKindRules.Format(uPerAtomDef.TargetDomain)}]）");
                var uSharedDef = CardEffectConverter.ConvertOne(new CardEffectData
                {
                    Id = "V9U_SHARED",
                    TargetKinds = new List<int> { 2 }, // 旧数据：效果级作用范围声明（共享单选）
                    AtomicEffects = new List<AtomicEffectEntry> { uDmgEntry },
                }, "V9U_SHARED");
                VerifySuite.Assert(!uSharedDef.PerAtomTargets
                                   && TargetKindRules.Format(uSharedDef.TargetDomain) == "2",
                    "header 声明=共享口径不回归（PerAtomTargets=false、域=声明值）");

                // ---- ② Union 助手（逐原子组合域=并集来源；运行时不做极性过滤——双域原子可选两侧、
                //     跨侧命中走错边黑白发放经济，与旧共享口径一致；极性收窄是合成器 UI 的选择集口径） ----
                VerifySuite.Assert(
                    TargetKindRules.Format(TargetKindRules.Union(new List<int> { 2, 1 }, new List<int> { 3, 2 })) == "1,2,3"
                    && TargetKindRules.Format(TargetKindRules.Union(null, new List<int> { 5 })) == "5",
                    "Union：并集升序去重、null 成员不参与");

                // ---- ③ 行为端到端：域不交双原子并列（旧交集口径不可构筑）+ ④ 无域原子共存 ----
                var uField1 = new List<Card>(ucore.ZoneManager.GetCards(u1, Zone.Battlefield));
                var uField2 = new List<Card>(ucore.ZoneManager.GetCards(u2, Zone.Battlefield));
                foreach (var c in uField1) ucore.ZoneManager.MoveCard(c, u1, Zone.Battlefield, Zone.Graveyard);
                foreach (var c in uField2) ucore.ZoneManager.MoveCard(c, u2, Zone.Battlefield, Zone.Graveyard);
                var uPool = ucore.ElementPool.GetPool(u1);
                var uSnap = new Dictionary<ManaType, int>(uPool.TempElements);
                try
                {
                    foreach (ManaType t in Enum.GetValues(typeof(ManaType))) uPool.TempElements[t] = 99;
                    // 2026-10-15 改版：法术账单=卡面费+所选效果锚价——抬地牌曲线上限放行锚价
                    uPool.GlobalTurnIndex = 9;

                    Card USpawn(Player owner, int power, int life)
                    {
                        var card = new CardWrapper(new CardData
                        {
                            ID = "V9U_c_" + owner.Name, CardName = "V9U" + owner.Name, Supertype = Cardtype.Creature,
                            Power = power, Life = life,
                        });
                        card.SetController(owner);
                        VerifySuite.Assert(ucore.ZoneManager.TryAddToBattlefield(card, owner), "V9U 随从入场");
                        return card;
                    }

                    var ua = USpawn(u1, 2, 5);
                    var ub = USpawn(u2, 2, 5);
                    int uDeck = ucore.ZoneManager.GetCards(u1, Zone.Deck).Count;
                    VerifySuite.Assert(uDeck > 0, "V9U 前提：牌库非空（DrawCard 观察点）");

                    Card UToHand(CardData data)
                    {
                        var card = new CardWrapper(data);
                        card.SetController(u1);
                        ucore.ZoneManager.GetZoneContainer(u1).Add(card, Zone.Hand);
                        return card;
                    }

                    var uPaData = new CardData
                    {
                        ID = "V9U_pa", CardName = "逐原子双域", Supertype = Cardtype.Spell,
                        Cost = PosCost((ManaType.Gray, 1f)),
                    };
                    uPaData.Effects.Add(new CardEffectData
                    {
                        Id = "V9U_pa_fx", TriggerTiming = (int)TriggerTiming.OnPlay,
                        AtomicEffects = new List<AtomicEffectEntry>
                        {
                            // 显式域收窄（逐原子卡的标准形态：每原子声明自己的作用范围）——对方单位 {2}/己方单位 {1}
                            AtomRefs.New(AtomicEffectType.DealDamage, value: 2, kinds: new List<int> { 2 }),
                            AtomRefs.New(AtomicEffectType.AddPermanentPlusOne, value: 1, kinds: new List<int> { 1 }),
                        },
                    });
                    VerifySuite.Assert(GameActions.PlayCard(ucore, u1, UToHand(uPaData)), "打出域不交逐原子组合");
                    GameActions.DrainStack(ucore);
                    VerifySuite.Assert(ub.GetLife() == 3 && ua.GetPower() == 3,
                        $"逐原子独立解析：直伤落对方生物（{ub.GetLife()}=3）、永久+1 落己方生物（{ua.GetPower()}=3）");

                    var uDrawData = new CardData
                    {
                        ID = "V9U_draw", CardName = "逐原子抽卡", Supertype = Cardtype.Spell,
                        Cost = PosCost((ManaType.Gray, 1f)),
                    };
                    uDrawData.Effects.Add(new CardEffectData
                    {
                        Id = "V9U_draw_fx", TriggerTiming = (int)TriggerTiming.OnPlay,
                        AtomicEffects = new List<AtomicEffectEntry>
                        {
                            AtomRefs.New(AtomicEffectType.DrawCard, value: 1),             // 隐藏区（牌库）自结算
                            AtomRefs.New(AtomicEffectType.AddPermanentPlusOne, value: 1, kinds: new List<int> { 1 }),
                        },
                    });
                    VerifySuite.Assert(GameActions.PlayCard(ucore, u1, UToHand(uDrawData)), "打出无域+有域并列");
                    GameActions.DrainStack(ucore);
                    VerifySuite.Assert(ua.GetPower() == 4
                                       && ucore.ZoneManager.GetCards(u1, Zone.Deck).Count == uDeck - 1,
                        "无域原子自结算共存：DrawCard 抽 1（牌库 -1）、永久+1 照常落己方（a 攻 4）");

                    // ---- ⑤ 强制类无窗口：逐原子自动全取（不弹选，全域命中）——
                    //      直构 def（CardData.ActivationType=0 会被转换器当未声明回落默认桶，
                    //      V9.o ④ 强制桶同款先例）；PerAtomTargets 手动置位（绕过 converter） ----
                    var uMandDef = new EffectDefinition
                    {
                        Id = "V9U_mand",
                        DisplayName = "V9u强制逐原子",
                        ActivationType = EffectActivationType.Mandatory,
                        TriggerTiming = TriggerTiming.OnPlay,
                        ElementCostPrepaid = true,
                        PerAtomTargets = true,
                        Effects = new List<AtomicEffectInstance>
                        {
                            new AtomicEffectInstance
                            {
                                Type = AtomicEffectType.DealDamage, Value = 1,
                                TargetKinds = new List<int> { 2 }, Polarity = -1f,
                                RowHashId = AtomicEffectTable.GetByType(AtomicEffectType.DealDamage)?.HashId,
                            },
                        },
                    };
                    int u2Life0 = u2.Life, ubLife0 = ub.GetLife();
                    ucore.StackEngine.GetExecutor().ExecuteAsync(new EffectInstance
                    {
                        Definition = uMandDef,
                        Source = u1,
                        Controller = u1,
                        Targets = new List<Entity>(),
                    }).GetAwaiter().GetResult();
                    VerifySuite.Assert(ub.GetLife() == ubLife0 - 1 && u2.Life == u2Life0 - 1,
                        $"强制类自动全取：对方生物与角色各受 1（{ubLife0}→{ub.GetLife()}、角色 {u2Life0}→{u2.Life}）——不弹选全域命中");
                }
                finally
                {
                    foreach (var kv in uSnap) uPool.TempElements[kv.Key] = kv.Value;
                    var uKill = new List<Card>(ucore.ZoneManager.GetCards(u1, Zone.Battlefield).Concat(
                        ucore.ZoneManager.GetCards(u2, Zone.Battlefield)));
                    foreach (var c in uKill) ucore.ZoneManager.MoveCard(c, c.GetController(), Zone.Battlefield, Zone.Graveyard);
                    foreach (var c in uField1) ucore.ZoneManager.MoveCard(c, u1, Zone.Graveyard, Zone.Battlefield);
                    foreach (var c in uField2) ucore.ZoneManager.MoveCard(c, u2, Zone.Graveyard, Zone.Battlefield);
                }

                // ---- ⑥ 并集 >4 装载兜底（合成器加入门的 backstop） ----
                var uDdHash = AtomicEffectTable.GetByType(AtomicEffectType.DealDamage)?.HashId;
                var uPrevSink = TideLog.Sink;
                var uErrs = new List<string>();
                TideLog.Sink = (level, msg) =>
                {
                    if (level == TideLogLevel.Error) uErrs.Add(msg);
                    uPrevSink?.Invoke(level, msg);
                };
                try
                {
                    CardLoader.LoadCardsFromText("{\"cards\":[{\"id\":\"V9U_over\",\"cardName\":\"并集超限\","
                        + "\"supertype\":\"Spell\",\"effects\":[{\"id\":\"E1\",\"atomicEffects\":[{\"refId\":\""
                        + uDdHash + "\",\"value\":1,\"kinds\":[2,4,6,8,10]"
                        + "}]}]}]}");
                    VerifySuite.Assert(uErrs.Any(e => e.Contains("并集") && e.Contains("超上限")),
                        "并集 >4 装载兜底：Error 点名超上限（单条效果限 4 个作用范围）");
                    uErrs.Clear();
                    CardLoader.LoadCardsFromText("{\"cards\":[{\"id\":\"V9U_ok\",\"cardName\":\"并集合规\","
                        + "\"supertype\":\"Spell\",\"effects\":[{\"id\":\"E1\",\"atomicEffects\":[{\"refId\":\""
                        + uDdHash + "\",\"value\":1,\"kinds\":[2,4]"
                        + "}]}]}]}");
                    VerifySuite.Assert(!uErrs.Any(e => e.Contains("超上限")), "并集 ≤4 合规：无超上限报错");
                }
                finally
                {
                    TideLog.Sink = uPrevSink;
                }
            }

            // ============================ V9.v 敌方全体伤害原子（2026-10-09 全体对象独立原子定案） ============================
            // 全体对象不走「全取档」（选取歧义）——独立原子 DealDamageAllEnemies：无域自结算（单次执行），
            // 域=敌方全体生物（{对方单位}+NoRole——角色与无生命单位不在域），
            // 杀数聚合入 LastOutcome（击杀门槛 DmgKillsTarget=至少击杀一个即过、奖励只结算一次）；基准红3。
            VerifySuite.Section("V9.v 敌方全体伤害：无域自结算全域命中（仅生物）+击杀门槛一次结算+红3基准");

            {
                var vcore = GameCore.Instance;
                vcore.Reset();
                ZoneContainer.Reseed(20261009);
                var vFill = Enumerable.Range(0, 8).Select(i => new CardData
                {
                    ID = $"V9V_fill_{i}", CardName = "V9V填充" + i, Supertype = Cardtype.Creature, Power = 1, Life = 1,
                }).ToList();
                vcore.InitGame(CardLoader.BuildDeck(vFill, 2), CardLoader.BuildDeck(vFill, 2));
                var v1 = vcore.Player1;
                var v2 = vcore.Player2;
                GameActions.SkipElementPool(vcore, v1);
                foreach (ManaType t in Enum.GetValues(typeof(ManaType)))
                    vcore.ElementPool.GetPool(v1).TempElements[t] = 99;
                // 2026-10-15 改版：法术账单=卡面费+所选效果锚价（AOE 红3）——抬上限放行
                vcore.ElementPool.GetPool(v1).GlobalTurnIndex = 9;
                var vField1 = new List<Card>(vcore.ZoneManager.GetCards(v1, Zone.Battlefield));
                var vField2 = new List<Card>(vcore.ZoneManager.GetCards(v2, Zone.Battlefield));
                foreach (var c in vField1) vcore.ZoneManager.MoveCard(c, v1, Zone.Battlefield, Zone.Graveyard);
                foreach (var c in vField2) vcore.ZoneManager.MoveCard(c, v2, Zone.Battlefield, Zone.Graveyard);

                // ---- ① 表行锚：空域（自结算）+ 基准红3 + 伤害产出族（击杀门槛可挂） ----
                var vRow = AtomicEffectTable.GetByType(AtomicEffectType.DealDamageAllEnemies);
                float vRed = vRow != null && vRow.ManaList != null ? vRow.ManaList[ManaType.Red] : 0f;
                VerifySuite.Assert(vRow != null
                                   && vRow.GetTargetKindList().Count == 0
                                   && Math.Abs(vRed - 3f) < 1e-6f,
                    $"表行锚：DealDamageAllEnemies 存在、域空（自结算）、基准红3（实际 域[{TargetKindRules.Format(vRow?.GetTargetKindList())}]"
                    + $" 红{vRed:0.##}）");
                VerifySuite.Assert(vRow != null && vRow.GetTagList().Contains(ComposerCatalog.DamageProducerTag)
                                   && ComposerCatalog.OutcomeConditionsFor(AtomicEffectType.DealDamageAllEnemies)
                                       .Any(g => g.Id == "DmgKillsTarget"),
                    "伤害产出族标签：击杀门槛（消灭目标时）可挂本原子");

                // ---- ② 计价：X=1 → 红3；X=2 → 红6（max(1,Value) 线性；空域数量恒 ×1——不吃全取档系数3；
                //      2026-10-16 时机钉 OnTurnStart=×1——本段测原子轴，OnPlay 缺省会吃一发时机 ×0.8） ----
                var vCost1 = CardEffectConverter.ConvertOne(new CardEffectData
                {
                    Id = "V9V_COST1",
                    TriggerTiming = (int)TriggerTiming.OnTurnStart,
                    SelectionMode = -1,
                    AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DealDamageAllEnemies, value: 1) },
                }, "V9V_COST1");
                var vCost2 = CardEffectConverter.ConvertOne(new CardEffectData
                {
                    Id = "V9V_COST2",
                    TriggerTiming = (int)TriggerTiming.OnTurnStart,
                    SelectionMode = -1,
                    AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DealDamageAllEnemies, value: 2) },
                }, "V9V_COST2");
                var vc1 = CostDerivationService.DeriveElementCosts(vCost1);
                var vc2 = CostDerivationService.DeriveElementCosts(vCost2);
                VerifySuite.Assert(Math.Abs(vc1.Total - 3f) < 1e-4f && Math.Abs(vc1[ManaType.Red] - 3f) < 1e-4f
                                   && Math.Abs(vc2.Total - 6f) < 1e-4f && Math.Abs(vc2[ManaType.Red] - 6f) < 1e-4f,
                    $"计价=红3×max(1,X)：X=1→{vc1[ManaType.Red]:0.##}红、X=2→{vc2[ManaType.Red]:0.##}红"
                    + $"（空域数量恒×1，Total {vc1.Total:0.##}/{vc2.Total:0.##}）");

                // ---- ③ 行为端到端：敌 1 血×2（均亡）+ 敌 5 血×1（存活-1）+ 己方生物/双方角色不在域；
                //      击杀门槛=至少击杀一个即过、DrawCard 奖励恰结算一次（两杀非两结） ----
                Card VSpawn(Player owner, int power, int life, string tag)
                {
                    var card = new CardWrapper(new CardData
                    {
                        ID = "V9V_" + tag, CardName = "V9V" + tag, Supertype = Cardtype.Creature,
                        Power = power, Life = life,
                    });
                    card.SetController(owner);
                    VerifySuite.Assert(vcore.ZoneManager.TryAddToBattlefield(card, owner), "V9V 随从入场：" + tag);
                    return card;
                }

                var vMine = VSpawn(v1, 2, 5, "mine");
                var vFrag1 = VSpawn(v2, 2, 1, "f1");
                var vFrag2 = VSpawn(v2, 2, 1, "f2");
                var vTank = VSpawn(v2, 2, 5, "tank");
                // 敌方无生命单位（结界）：不在域——不受波及
                var vFacade = new CardWrapper(new CardData
                {
                    ID = "V9V_facade", CardName = "V9V结界", Supertype = Cardtype.Enchantment, Life = 3,
                });
                vFacade.SetController(v2);
                VerifySuite.Assert(vcore.ZoneManager.TryAddToBattlefield(vFacade, v2), "V9V 结界入场");
                int vDeck0 = vcore.ZoneManager.GetCards(v1, Zone.Deck).Count;
                int v1Life0 = v1.Life, v2Life0 = v2.Life, vMine0 = vMine.GetLife(), vFacade0 = vFacade.GetLife();
                VerifySuite.Assert(vDeck0 > 1, "V9V 前提：牌库 ≥2（DrawCard 奖励观察点）");

                var vAoeData = new CardData
                {
                    ID = "V9V_AOE", CardName = "敌方全体伤害", Supertype = Cardtype.Spell,
                    Cost = PosCost((ManaType.Gray, 1f)),
                };
                vAoeData.Effects.Add(new CardEffectData
                {
                    Id = "V9V_AOE_EFF", TriggerTiming = (int)TriggerTiming.OnPlay, SelectionMode = -1,
                    AtomicEffects = new List<AtomicEffectEntry>
                    {
                        new AtomicEffectEntry
                        {
                            refId = AtomicEffectTable.GetByType(AtomicEffectType.DealDamageAllEnemies)?.HashId,
                            value = 1,
                            branch = new BranchEntryData
                            {
                                settle = (int)BranchSettleKind.Outcome,
                                outcomeId = "DmgKillsTarget",
                                then = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DrawCard, value: 1) },
                            },
                        },
                    },
                });
                var vAoeHand = new CardWrapper(vAoeData);
                vAoeHand.SetController(v1);
                vcore.ZoneManager.GetZoneContainer(v1).Add(vAoeHand, Zone.Hand);
                VerifySuite.Assert(GameActions.PlayCard(vcore, v1, vAoeHand, null, Zone.Hand, 0, out var rejV),
                    $"敌方全体伤害卡打出（拒绝原因：{rejV ?? "无"}）");
                GameActions.DrainStack(vcore);

                VerifySuite.Assert(!vFrag1.IsAlive && !vFrag2.IsAlive && vTank.GetLife() == 4,
                    $"全域命中敌方生物：1 血双亡（存活 f1={vFrag1.IsAlive} f2={vFrag2.IsAlive}）、5 血受 1（{vTank.GetLife()}=4）");
                VerifySuite.Assert(vMine.GetLife() == vMine0 && v1.Life == v1Life0 && v2.Life == v2Life0
                                   && vFacade.GetLife() == vFacade0,
                    $"域=敌方生物：己方生物（{vMine0}={vMine.GetLife()}）、双方角色（{v1Life0}/{v2Life0}）、"
                    + $"敌方结界（{vFacade0}={vFacade.GetLife()}）均不在域");
                VerifySuite.Assert(vcore.ZoneManager.GetCards(v1, Zone.Deck).Count == vDeck0 - 1,
                    $"击杀门槛一次结算：两杀只过一次门、DrawCard 恰结算一次（牌库 {vDeck0}→"
                    + $"{vcore.ZoneManager.GetCards(v1, Zone.Deck).Count}，-1 非 -2）");

                foreach (var c in new List<Card>(vcore.ZoneManager.GetCards(v1, Zone.Battlefield).Concat(
                             vcore.ZoneManager.GetCards(v2, Zone.Battlefield))))
                    vcore.ZoneManager.MoveCard(c, c.GetController(), Zone.Battlefield, Zone.Graveyard);
                foreach (var c in vField1) vcore.ZoneManager.MoveCard(c, v1, Zone.Graveyard, Zone.Battlefield);
                foreach (var c in vField2) vcore.ZoneManager.MoveCard(c, v2, Zone.Graveyard, Zone.Battlefield);
            }

            // ============================ V9.w 衍生物计价无底盘（2026-10-10 定案） ============================
            // 衍生物没走「入组 1 + 抽牌 2」通道，吃不到 3 点底盘——召唤原子单价 = 模板内容全价
            //（S含耐久+K+E，**不乘 d(C)**）+ 结构费全额（攻/守/槽×1，**无预算抵扣**）= ContentPriceOf；
            // 2026-10-12 负面指示物补偿（表驱动）：召唤自带一层遗忘——单价再 − 遗忘行 GrantTemporary
            // 单层锚价/只（白2，下限 0）；原写死 −1 灰同日退役，后续遗忘改价自动跟随。
            //（同日早间「声明费+ChassisAdjust 回加」案被本口径取代——模板声明费与召唤价彻底脱钩。）
            VerifySuite.Section("V9.w 衍生物计价无底盘：单价=内容全价+结构全额−负面补偿（遗忘锚·表驱动），数量线性外乘");

            {
                // 白板模板（攻+守、零效果）：内容 S=(2+2)/2=2 + 结构2 → 4 灰；
                // 单效果模板：S2 + E(抽1=蓝2) + 结构3（攻+守+槽）→ 7（灰5蓝2）
                var wPlain = new CardData
                {
                    ID = "V9W_PLAIN", CardName = "V9W白板模板", Supertype = Cardtype.Creature, Power = 2, Life = 2,
                    Cost = PosCost((ManaType.Gray, 2f)),
                };
                var wSlotted = new CardData
                {
                    ID = "V9W_SLOT", CardName = "V9W单效果模板", Supertype = Cardtype.Creature, Power = 2, Life = 2,
                    Cost = PosCost((ManaType.Gray, 3f)),
                };
                wSlotted.Effects.Add(new CardEffectData
                {
                    Id = "V9W_SLOT_EFF", TriggerTiming = (int)TriggerTiming.OnPlay, SelectionMode = -1,
                    AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DrawCard, value: 1) },
                });
                VerifySuite.Assert(CardCompositionCost.StructureFeeFull(wPlain) == 2
                                   && CardCompositionCost.StructureFeeFull(wSlotted) == 3,
                    $"结构全额前提：白板 攻+守=2、单效果 攻+守+槽=3（费率1——无预算抵扣）");
                VerifySuite.Assert(CardCostService.ContentPriceOf(wPlain) == 4
                                   && CardCostService.ContentPriceOf(wSlotted) == 7,
                    $"内容全价：白板 S2+结构2=4、单效果 S2+E蓝2+结构3=7"
                    + $"（实际 {CardCostService.ContentPriceOf(wPlain)}/{CardCostService.ContentPriceOf(wSlotted)}）");

                var wPrevResolver = CardCore.Attribute.Handlers.SummonTokenHandler.ResolveTemplate;
                CardCore.Attribute.Handlers.SummonTokenHandler.ResolveTemplate = id =>
                    id == wPlain.ID ? wPlain : id == wSlotted.ID ? wSlotted : null;
                try
                {
                    var wSummon2 = CardEffectConverter.ConvertOne(new CardEffectData
                    {
                        Id = "V9W_SUMMON2",
                        SelectionMode = -1,
                        AtomicEffects = new List<AtomicEffectEntry>
                            { AtomRefs.New(AtomicEffectType.SummonToken, value: 2, str: wPlain.ID) },
                    }, "V9W_SUMMON2");
                    var wSummon1 = CardEffectConverter.ConvertOne(new CardEffectData
                    {
                        Id = "V9W_SUMMON1",
                        SelectionMode = -1,
                        AtomicEffects = new List<AtomicEffectEntry>
                            { AtomRefs.New(AtomicEffectType.SummonToken, value: 1, str: wSlotted.ID) },
                    }, "V9W_SUMMON1");
                    var wCost2 = CostDerivationService.DeriveElementCosts(wSummon2);
                    var wCost1 = CostDerivationService.DeriveElementCosts(wSummon1);
                    // 白板：单价 = 内容全价 4 −补偿2 = 2/只 ×2 层 = 4 → OnPlay 宿主一发时机 ×0.8 = 3.2
                    // → 效果出口逐色取整（2026-10-18）= 3 灰
                    VerifySuite.Assert(Math.Abs(wCost2.Total - 3f) < 1e-4f
                                       && Math.Abs(wCost2[ManaType.Gray] - 3f) < 1e-4f,
                        $"无底盘计价：白板×2层 → 3 灰（S2+结构2=4−负面补偿2=2/只×2=4，OnPlay 宿主×0.8=3.2 取整"
                        + $"——实际 Total {wCost2.Total:0.##} 灰{wCost2[ManaType.Gray]:0.##}）");
                    // 单效果：内容 灰5蓝2，白锚 2 无白可扣 → 转扣灰 → 补偿后桶 **灰3蓝2**（精确落位，
                    // 不再比例分摊）。2026-10-12 裁决：负面补偿=遗忘行 GrantTemporary 单层锚（白2·表驱动）
                    // ——白优先补白、无白自动转灰。2026-10-16：OnPlay 宿主一发时机 ×0.8=灰2.4蓝1.6；
                    // 2026-10-18 效果出口逐色取整 → 灰2蓝2（Total 4 不变）。
                    VerifySuite.Assert(Math.Abs(wCost1.Total - 4) < 1e-4f
                                       && Math.Abs(wCost1[ManaType.Gray] - 2f) < 1e-3f
                                       && Math.Abs(wCost1[ManaType.Blue] - 2f) < 1e-3f,
                        $"内容全价分色：单效果×1层 → 灰5蓝2−白锚转灰2=灰3+蓝2，OnPlay×0.8=灰2.4蓝1.6 取整灰2蓝2"
                        + $"（实际 Total {wCost1.Total:0.##} 灰{wCost1[ManaType.Gray]:0.##}蓝{wCost1[ManaType.Blue]:0.##}）");
                    // 声明费脱钩回归锚（取代旧「底盘中性」锚）：改模板声明费 → 召唤价不动
                    wSlotted.Cost = PosCost((ManaType.Gray, 99f));
                    var wCost1b = CostDerivationService.DeriveElementCosts(wSummon1);
                    VerifySuite.Assert(Math.Abs(wCost1b.Total - 4) < 1e-4f,
                        $"声明费脱钩：模板声明费 3→99 后召唤价仍 {wCost1b.Total:0.##}（旧口径随声明费漂移，已废）");
                    wSlotted.Cost = PosCost((ManaType.Gray, 3f));
                }
                finally
                {
                    CardCore.Attribute.Handlers.SummonTokenHandler.ResolveTemplate = wPrevResolver;
                }
            }

            // ============================ V9.y 碾压攻角色 + 守卫拦截（2026-10-16 改版） ============================
            // 改版口径：碾压溅射枢轴恒取**宣言原目标**——守卫拦截只拦住原目标的主战斗伤害
            //（战斗发生在原被攻击者那一格），溅射域不受拦截影响；攻角色时原目标=角色（Player），
            // 溅射域=角色格左右邻格生物（8×7 几何：角色居侧列端头，P2(7,5) 的 W=(6,5) 在盘、E 出界）。
            VerifySuite.Section("V9.y 碾压攻角色+守卫拦截：溅射枢轴=宣言原目标（2026-10-16 改版）");
            {
                var ycore = GameCore.Instance;
                ycore.Reset();
                ZoneContainer.Reseed(20261016);
                var yFill = Enumerable.Range(0, 8).Select(i => new CardData
                {
                    ID = $"V9Y_fill_{i}", CardName = "V9Y填充" + i, Supertype = Cardtype.Creature, Power = 1, Life = 1,
                }).ToList();
                ycore.InitGame(CardLoader.BuildDeck(yFill, 2), CardLoader.BuildDeck(yFill, 2));
                var y1 = ycore.Player1;
                var y2 = ycore.Player2;
                GameActions.SkipElementPool(ycore, y1); // Standby → Main（y1 主阶段）

                // 真实棋盘接线（同 BattleController.AttachBoard 口径：碾压两个扩展点成对注入）
                CombatSystem.AdjacentResolver = null;
                CombatSystem.RoleAdjacentResolver = null; // 防前段残留（静态扩展点惯例）
                var yBoard = new GameBoard.BoardState(ycore, y1, y2,
                    GameBoard.HalfFieldData.Flat(), GameBoard.HalfFieldData.Flat());
                yBoard.EnableAutoResync();
                CombatSystem.AdjacentResolver = yBoard.FlankNeighbors;
                CombatSystem.RoleAdjacentResolver = yBoard.FlankNeighborsOfRole;
                try
                {
                    // p2 角色格左右邻格（几何不硬编码：E/W 逐向取在盘单位格）
                    var yRoleCell = GameBoard.BoardLayout.CharacterCell(1);
                    int yFlankX = -1, yFlankZ = -1;
                    foreach (var yDir in new[] { GameBoard.BoardDirection.E, GameBoard.BoardDirection.W })
                    {
                        var (fnx, fnz) = GameBoard.BoardMath.Neighbor(yRoleCell.Item1, yRoleCell.Item2, yDir);
                        if (GameBoard.BoardLayout.RoleOf(fnx, fnz) == GameBoard.CellRole.Unit)
                        {
                            yFlankX = fnx; yFlankZ = fnz;
                            break;
                        }
                    }
                    VerifySuite.Assert(yFlankX >= 0, $"角色格({yRoleCell.Item1},{yRoleCell.Item2})存在在盘左右邻格单位格");

                    CardWrapper YUnit(string id, string name, int power, int life, Player owner)
                    {
                        var c = new CardWrapper(new CardData
                        {
                            ID = id, CardName = name, Supertype = Cardtype.Creature, Power = power, Life = life,
                        });
                        c.SetController(owner);
                        VerifySuite.Assert(ycore.ZoneManager.TryAddToBattlefield(c, owner), name + "入场");
                        c.Untap(); // 横置入场 → 解除（攻击/拦截资格）
                        return c;
                    }

                    // ---- ① 无拦截：碾压攻角色 → 主伤落角色 + 角色侧邻格生物吃同额溅射 ----
                    var yHammer1 = YUnit("V9Y_hammer1", "攻城槌一", 4, 9, y1);
                    yHammer1.AddKeyword(KeywordRules.Overwhelm);
                    var yFlanker1 = YUnit("V9Y_flanker1", "贴身护卫一", 0, 10, y2);
                    yBoard.PinCard(yFlanker1, yFlankX, yFlankZ); // 钉到角色格邻格
                    int yRoleLife0 = y2.Life;
                    int yFlanker1Life0 = yFlanker1.GetLife();
                    VerifySuite.Assert(GameActions.DeclareAttack(ycore, y1, yHammer1, y2), "碾压攻角色宣言入栈");
                    GameActions.DrainStack(ycore);
                    ycore.SBAEngine.ExecuteAll();
                    VerifySuite.Assert(y2.Life == yRoleLife0 - 4 && yFlanker1.GetLife() == yFlanker1Life0 - 4,
                        $"无拦截溅射：角色 -4（实际 {yRoleLife0 - y2.Life}）+ 角色邻格生物 -4（实际 {yFlanker1Life0 - yFlanker1.GetLife()}）");
                    VerifySuite.Assert(yHammer1.IsTapped(), "攻击结算后攻击者横置（战斗后支付口径）");

                    // ---- ② 守卫拦截：主伤被守卫拦下（角色不掉血），角色邻格生物**照吃**溅射；
                    //      守卫自己的邻格生物不吃溅射（枢轴≠守卫——旧口径退役锚） ----
                    var yHammer2 = YUnit("V9Y_hammer2", "攻城槌二", 3, 9, y1);
                    yHammer2.AddKeyword(KeywordRules.Overwhelm);
                    var yGuard = YUnit("V9Y_guard", "格挡门神", 0, 8, y2);   // 0 攻不反击（隔离观测）
                    var yGuardFlank = YUnit("V9Y_guardflank", "门神侧翼", 0, 9, y2); // 落位=守卫邻格（first-free 连座）
                    int yRoleLifeB = y2.Life;
                    int yFlankerLifeB = yFlanker1.GetLife(); // 贴身护卫仍在角色邻格（①后存活）
                    int yGuardLife0 = yGuard.GetLife();
                    int yGuardFlankLife0 = yGuardFlank.GetLife();
                    // 守卫落格核对（守卫/门神侧翼相邻两格——溅射域证伪的前提；& 非短路保插值定赋值）
                    bool yAdj = yBoard.TryGetCell(yGuard, out int yGx, out int yGz)
                              & yBoard.TryGetCell(yGuardFlank, out int yBx, out int yBz);
                    VerifySuite.Assert(yAdj && Math.Abs(yGx - yBx) + Math.Abs(yGz - yBz) == 1,
                        $"守卫与门神侧翼相邻（守卫({yGx},{yGz}) 侧翼({yBx},{yBz})）");
                    VerifySuite.Assert(yBoard.TryGetCell(yFlanker1, out int yFx, out int yFz)
                                      && yFx == yFlankX && yFz == yFlankZ,
                        "贴身护卫仍在角色邻格（钉位未被挤占）");
                    VerifySuite.Assert(GameActions.DeclareAttack(ycore, y1, yHammer2, y2), "碾压再攻角色宣言入栈");
                    VerifySuite.Assert(ycore.StackEngine.PushGuardDeclaration(yGuard, ycore.StackEngine.Peek(), y2),
                        "守卫速度1拦截入栈");
                    GameActions.DrainStack(ycore);
                    ycore.SBAEngine.ExecuteAll();
                    VerifySuite.Assert(y2.Life == yRoleLifeB, $"原目标伤害被守卫拦下：角色不掉血（实际 {yRoleLifeB - y2.Life}）");
                    VerifySuite.Assert(yGuard.GetLife() == yGuardLife0 - 3, $"守卫吃主战斗伤害 -3（实际 {yGuardLife0 - yGuard.GetLife()}）");
                    VerifySuite.Assert(yFlanker1.GetLife() == yFlankerLifeB - 3,
                        $"溅射不受拦截：角色邻格生物照吃 -3（实际 {yFlankerLifeB - yFlanker1.GetLife()}）");
                    VerifySuite.Assert(yGuardFlank.GetLife() == yGuardFlankLife0,
                        $"守卫邻格生物不吃溅射（枢轴=宣言原目标≠守卫；实际掉 {yGuardFlankLife0 - yGuardFlank.GetLife()}）");
                    VerifySuite.Assert(yHammer2.IsTapped() && yGuard.IsTapped(), "战斗后攻击者与拦截守卫双双横置");
                }
                finally
                {
                    CombatSystem.AdjacentResolver = null;
                    CombatSystem.RoleAdjacentResolver = null;
                    yBoard.Dispose();
                }
            }

            // ============================ V9.z 一发时机系数 + 引擎头解绑（2026-10-16 改版） ============================
            // 改版口径①（时机轴接入计价，取代 2026-10-05「时机不接计价」）：整个生命期望=1 的触发时机
            //（登场/死亡/游戏开始/角色死亡/破坏/除外/离场/墓地回场）×0.8（表 PricingTier.TimingOnceFactor）；
            // 期望>1 时机与非触发式不乘；引擎载荷豁免（def 时机=奖励发射窗，不代表主干发射次数）。
            // 改版口径②（引擎头解绑「回合开始」）：倒计时仅**计数**钉回合开始，归零→奖励待发（布尔，
            // 多轮不积累），按声明时机发射（主动=0费 def 上栈/自动=直接结算）；运势掷骰随声明时机判定。
            VerifySuite.Section("V9.z 一发时机系数 + 引擎头解绑（2026-10-16 改版）");
            {
                // ---- ① 分类表：一发时机八值；多频/事件/观察者族与 OnSummon 超集不入档 ----
                VerifySuite.Assert(CostDerivationService.FiresOncePerPaidLifetime(TriggerTiming.OnPlay)
                                  && CostDerivationService.FiresOncePerPaidLifetime(TriggerTiming.OnDeath)
                                  && CostDerivationService.FiresOncePerPaidLifetime(TriggerTiming.OnGameStart)
                                  && CostDerivationService.FiresOncePerPaidLifetime(TriggerTiming.OnRoleDeath)
                                  && CostDerivationService.FiresOncePerPaidLifetime(TriggerTiming.OnDestroy)
                                  && CostDerivationService.FiresOncePerPaidLifetime(TriggerTiming.OnExile)
                                  && CostDerivationService.FiresOncePerPaidLifetime(TriggerTiming.OnLeaveBattlefield)
                                  && CostDerivationService.FiresOncePerPaidLifetime(TriggerTiming.OnReturnFromGraveyard),
                    "一发时机集合八值（付费后整个生命期望=1——用户点名登场/死亡+每局一发+换区单发族）");
                VerifySuite.Assert(!CostDerivationService.FiresOncePerPaidLifetime(TriggerTiming.OnDraw)
                                  && !CostDerivationService.FiresOncePerPaidLifetime(TriggerTiming.OnDealDamage)
                                  && !CostDerivationService.FiresOncePerPaidLifetime(TriggerTiming.OnTakeDamage)
                                  && !CostDerivationService.FiresOncePerPaidLifetime(TriggerTiming.OnTurnStart)
                                  && !CostDerivationService.FiresOncePerPaidLifetime(TriggerTiming.OnTurnEnd)
                                  && !CostDerivationService.FiresOncePerPaidLifetime(TriggerTiming.OnSummon) // 超集：token/复活/换主控二次机会
                                  && !CostDerivationService.FiresOncePerPaidLifetime(TriggerTiming.OnAttack),
                    "抽牌/伤害/回合开始结束等期望>1 时机不入档（费用系数不变）；OnSummon 超集除外");

                // ---- ② 计价链：同原子 OnPlay 宿主=round(OnTurnStart 宿主×0.8)（2026-10-18 效果出口
                //      取整后小基数折扣会被取整吞掉——value 用 5：5×0.8=4 可观测）；主动/引擎载荷 ×1；
                //      公式链含时机档 ----
                CardEffectData ZDef(TriggerTiming t) => new CardEffectData
                {
                    Id = "V9Z_COST_" + t, TriggerTiming = (int)t, SelectionMode = -1,
                    AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DealDamage, value: 5, kinds: new List<int> { 2 }) },
                };
                var zOn = CostDerivationService.DeriveElementCosts(CardEffectConverter.ConvertOne(ZDef(TriggerTiming.OnTurnStart), "V9Z"));
                var zPlay = CostDerivationService.DeriveElementCosts(CardEffectConverter.ConvertOne(ZDef(TriggerTiming.OnPlay), "V9Z"));
                var zAct = CostDerivationService.DeriveElementCosts(CardEffectConverter.ConvertOne(new CardEffectData
                {
                    Id = "V9Z_COST_ACT", TriggerTiming = (int)TriggerTiming.Activate_Active, SelectionMode = -1,
                    AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DealDamage, value: 5, kinds: new List<int> { 2 }) },
                }, "V9Z"));
                VerifySuite.Assert(zOn.Total > 0f
                                  && Math.Abs(zPlay.Total - Math.Round(zOn.Total * 0.8f, MidpointRounding.AwayFromZero)) < 1e-4f
                                  && Math.Abs(zAct.Total - zOn.Total) < 1e-4f,
                    $"一发时机计价：OnPlay=round(OnTurnStart×0.8)（{zPlay.Total:0.##} vs {zOn.Total:0.##}）；主动×1（{zAct.Total:0.##}）");
                var zPlayDef = CardEffectConverter.ConvertOne(ZDef(TriggerTiming.OnPlay), "V9Z");
                var zPlayAtom = zPlayDef.Effects[0];
                var zFormula = CostDerivationService.AtomCostFormula(zPlayAtom, zPlayDef,
                    CostDerivationService.PricingVisitDomain(zPlayDef, zPlayAtom));
                VerifySuite.Assert(zFormula.Item2.Any(f => f.Contains("时机档0.8")),
                    $"公式链展开含时机档（实际 [{string.Join("×", zFormula.Item2)}]——与 ComputeAtomCost 同批判定原语）");

                // ---- ③ 引擎头解绑·转换器：FreeTiming 声明值优先/未声明回落缺省/钉死组照旧 ----
                List<AtomicEffectEntry> ZEngineAtoms(BranchEngineKind kind, int param) => new List<AtomicEffectEntry>
                {
                    new AtomicEffectEntry
                    {
                        refId = AtomicEffectTable.GetByType(kind switch
                        {
                            BranchEngineKind.Countdown => AtomicEffectType.EngineCountdown,
                            BranchEngineKind.LuckRoll => AtomicEffectType.EngineLuckRoll,
                            BranchEngineKind.DeathToll => AtomicEffectType.EngineDeathToll,
                            _ => AtomicEffectType.EngineCountdown,
                        })?.HashId,
                        value = 1,
                        branch = new BranchEntryData
                        {
                            settle = (int)BranchSettleKind.Engine,
                            engine = (int)kind,
                            engineParam = param,
                            then = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DrawCard, value: 1) },
                        },
                    },
                };
                var zCdDef = CardEffectConverter.ConvertOne(new CardEffectData
                {
                    Id = "V9Z_CD_DRAW", TriggerTiming = (int)TriggerTiming.OnDraw, ActivationType = 2, // 主动
                    SelectionMode = -1, AtomicEffects = ZEngineAtoms(BranchEngineKind.Countdown, 1),
                }, "V9Z");
                VerifySuite.Assert(zCdDef.TriggerTiming == TriggerTiming.OnDraw
                                  && zCdDef.ActivationType == EffectActivationType.Voluntary
                                  && zCdDef.ElementCostPrepaid && zCdDef.TriggerLimitPerTurn == -1
                                  && Math.Abs(CostDerivationService.TimingCostFactor(zCdDef) - 1f) < 1e-6f,
                    "倒计时解绑：声明 抽牌时+主动 保留、费用0（prepaid）、次数钉无限、时机系数豁免——不走诅咒主动折叠");
                var zCdLegacy = CardEffectConverter.ConvertOne(new CardEffectData
                {
                    Id = "V9Z_CD_LEGACY", TriggerTiming = -1, SelectionMode = -1,
                    AtomicEffects = ZEngineAtoms(BranchEngineKind.Countdown, 1),
                }, "V9Z");
                VerifySuite.Assert(zCdLegacy.TriggerTiming == TriggerTiming.OnTurnStart
                                  && zCdLegacy.ActivationType == EffectActivationType.Automatic,
                    "倒计时未声明（&lt;0 回落 Activate_Active）→ 缺省 回合开始+自动（存量行为不漂移）");
                var zLuckDef = CardEffectConverter.ConvertOne(new CardEffectData
                {
                    Id = "V9Z_LUCK_CP", TriggerTiming = (int)TriggerTiming.OnCardPlayed, ActivationType = 1,
                    SelectionMode = -1, AtomicEffects = ZEngineAtoms(BranchEngineKind.LuckRoll, 3),
                }, "V9Z");
                VerifySuite.Assert(zLuckDef.TriggerTiming == TriggerTiming.OnCardPlayed
                                  && zLuckDef.ActivationType == EffectActivationType.Automatic,
                    "运势解绑：声明 使用卡牌时 保留；掷骰恒自动（发动方式不开放）");
                var zPinned = CardEffectConverter.ConvertOne(new CardEffectData
                {
                    Id = "V9Z_DT_PIN", TriggerTiming = (int)TriggerTiming.OnDraw, SelectionMode = -1, // 故意错声明
                    AtomicEffects = ZEngineAtoms(BranchEngineKind.DeathToll, 1),
                }, "V9Z");
                VerifySuite.Assert(zPinned.TriggerTiming == TriggerTiming.OnOtherCreatureDeath
                                  && zPinned.ActivationType == EffectActivationType.Automatic
                                  && zPinned.TriggerLimitPerTurn == -1,
                    "钉死组照旧：死亡计数错声明归一 其他生物死亡+自动+无限（2026-10-19 手牌/场上改案口径；解绑只开倒计时/运势）");

                // 奖励估值不吃时机（REWARD_SHIM 钉非触发式）：DrawCard1=蓝2 面值（×0.8 会成 1.6）
                var zDrawDef = CardEffectConverter.ConvertOne(new CardEffectData
                {
                    Id = "V9Z_DRAW", TriggerTiming = (int)TriggerTiming.OnPlay, SelectionMode = -1,
                    AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DrawCard, value: 1) },
                }, "V9Z");
                VerifySuite.Assert(Math.Abs(CostDerivationService.RewardDerivedCost(zDrawDef.Effects) - 2f) < 1e-4f,
                    "分支奖励按单次面值估值（shim 钉非触发式——门槛/换算/附加费/召唤补偿四口不吃时机减免）");

                // ---- ④ 倒计时运行时：归零→待发（多轮不积累）→按声明时机发射→发射即消耗 ----
                //（时机用 OnExile 隔离：环境存在回合开始自动抽牌且其订阅序先于本类递减——OnDraw 时机
                // 会在下一回合开始被真实抽牌事件打掉，无法净观测「不积累」；除外事件零环境噪声）
                var zcore = GameCore.Instance;
                zcore.Reset();
                ZoneContainer.Reseed(20261017);
                var zFill = Enumerable.Range(0, 8).Select(i => new CardData
                {
                    ID = $"V9Z_fill_{i}", CardName = "V9Z填充" + i, Supertype = Cardtype.Creature, Power = 1, Life = 1,
                }).ToList();
                zcore.InitGame(CardLoader.BuildDeck(zFill, 2), CardLoader.BuildDeck(zFill, 2));
                var z1 = zcore.Player1;
                GameActions.SkipElementPool(zcore, z1);
                var zCdCardData = new CardData
                {
                    ID = "V9Z_CD_UNIT", CardName = "V9Z倒计时体", Supertype = Cardtype.Creature, Power = 1, Life = 5,
                };
                zCdCardData.Effects.Add(new CardEffectData
                {
                    Id = "V9Z_CD_UNIT_EFF", TriggerTiming = (int)TriggerTiming.OnExile, ActivationType = 1, // 自动（直接结算路径）
                    SelectionMode = -1, AtomicEffects = ZEngineAtoms(BranchEngineKind.Countdown, 1),
                });
                var zCdUnit = new CardWrapper(zCdCardData);
                zCdUnit.SetController(z1);
                VerifySuite.Assert(zcore.ZoneManager.TryAddToBattlefield(zCdUnit, z1), "倒计时引擎卡入场");
                VerifySuite.Assert(zCdUnit.GetCounterCount(CardCore.Attribute.CounterRules.CountdownCounter) == 1,
                    "入场挂倒计时计数=engineParam 初值 1");
                int zZero = 0, zFire = 0;
                EventManager.Instance.Subscribe<KeywordAppliedEvent>(e =>
                {
                    if (e.Target != zCdUnit) return;
                    if (e.Detail.Contains("归零")) zZero++;
                    if (e.Detail.Contains("按发动时机")) zFire++;
                });
                var zExiledStub = new CardWrapper(new CardData { ID = "V9Z_STUB", CardName = "V9Z除外桩", Supertype = Cardtype.Creature });
                zExiledStub.SetController(z1);
                zcore.PublishEvent(new TurnStartEvent { TurnPlayer = z1, TurnNumber = 101 });
                VerifySuite.Assert(zZero == 1 && zFire == 0,
                    $"回合开始①：计数归零→待发+重挂（声明时机=除外时，归零即发旧口径已废——实际 归零{zZero} 发{zFire}）");
                zcore.PublishEvent(new TurnStartEvent { TurnPlayer = z1, TurnNumber = 102 });
                VerifySuite.Assert(zZero == 2 && zFire == 0,
                    $"回合开始②：再归零不积累（待发恒 1 份——实际 归零{zZero} 发{zFire}）");
                zcore.PublishEvent(new CardExileEvent { Card = zExiledStub, Source = z1, FromZone = Zone.Graveyard });
                VerifySuite.Assert(zFire == 1,
                    $"除外事件：待发奖励按声明时机发射（实际 发{zFire}）");
                zcore.PublishEvent(new CardExileEvent { Card = zExiledStub, Source = z1, FromZone = Zone.Graveyard });
                VerifySuite.Assert(zFire == 1,
                    $"再除外不重发（发射即消耗——实际 发{zFire}）");

                // ---- ⑤ 运势运行时：随声明时机掷骰（回合开始不再触发）+ 种子镜像复现过/不过两分支 ----
                var zLuckCardData = new CardData
                {
                    ID = "V9Z_LUCK_UNIT", CardName = "V9Z运势体", Supertype = Cardtype.Creature, Power = 1, Life = 5,
                };
                zLuckCardData.Effects.Add(new CardEffectData
                {
                    Id = "V9Z_LUCK_UNIT_EFF", TriggerTiming = (int)TriggerTiming.OnCardPlayed, ActivationType = 1,
                    SelectionMode = -1, AtomicEffects = ZEngineAtoms(BranchEngineKind.LuckRoll, 3),
                });
                var zLuckUnit = new CardWrapper(zLuckCardData);
                zLuckUnit.SetController(z1);
                VerifySuite.Assert(zcore.ZoneManager.TryAddToBattlefield(zLuckUnit, z1), "运势引擎卡入场");
                int zLuck = 0;
                EventManager.Instance.Subscribe<KeywordAppliedEvent>(e =>
                {
                    if (e.Keyword == "运势" && e.Target == zLuckUnit) zLuck++;
                });
                zcore.PublishEvent(new TurnStartEvent { TurnPlayer = z1, TurnNumber = 103 });
                VerifySuite.Assert(zLuck == 0, "运势不再回合开始掷骰（解绑回归锚——TurnStart 零运势播报）");
                int ZSeed(bool wantPass)
                {
                    for (int s = 1; ; s++)
                    {
                        var r = new Random(s); // 镜像 GameRng（System.Random 同种子同序列）
                        bool pass = r.Next(1, 7) >= 3 && r.Next(1, 7) >= 3; // 双≥口径（2026-10-19 改版）
                        if (pass == wantPass) return s;
                    }
                }
                GameRng.Reseed(ZSeed(true));
                zcore.PublishEvent(new CardPlayEvent { Player = z1, PlayedCard = zExiledStub, FromZone = Zone.Hand });
                VerifySuite.Assert(zLuck == 1,
                    $"使用卡牌事件：掷骰双≥3 过门执行奖励（种子复现——实际 {zLuck}）");
                GameRng.Reseed(ZSeed(false));
                zcore.PublishEvent(new CardPlayEvent { Player = z1, PlayedCard = zExiledStub, FromZone = Zone.Hand });
                VerifySuite.Assert(zLuck == 1,
                    $"掷骰未过双≥3：不执行奖励（种子复现——实际 {zLuck}）");
            }

            // ============================ V9.x 已删（2026-10-11 资源重做） ============================
            // 旧段=地牌沉默/迟钝闸（横置换元素=主动效果口径）——现取现用后地牌横置=付费步系统
            // 强制动作，两闸拦截对象消失，沉默/迟钝表行已去元素池域（TargetKinds）。地牌的移除轴
            // =摧毁/抹除（原子既有、handler 出池支持既有），无需另立新锚。

            // ============================ V9.aa 速度门全严格大于 + 空栈首动豁免（2026-10-18 定案） ============================
            // 旧双轨（回合方 ≥ / 非回合方严格 >）与 10-17 归属台账退役：所有主动发动一律**严格大于**
            // 记速器，同速连锁（无论连锁对象归属）一律禁止；唯一豁免=空栈首动（新连锁第一动不查速度）。
            // 锚：① 自己 0 速施放锚点下自家 0 速手牌不进候选且引擎拒入；② 严格更快照常可连锁；
            // ③ 对手抬速后回合方同速跟=被禁（旧「对手抬速后可同速跟」口径退役）；
            // ④ 触发式锚点（0 速·ProcessTriggeredEffects 推栈形态，无台账可读）下自家 0 速手牌
            //    同样被禁（10-17 台账盲区→10-18 全严格大于根除的正主回归锚）；⑤ 空栈首动豁免。
            VerifySuite.Section("V9.aa 速度门：全严格大于+空栈首动豁免（2026-10-18 定案）");
            var aacore = GameCore.Instance;
            aacore.Reset();
            ZoneContainer.Reseed(20261018);
            var aaFill = Enumerable.Range(0, 8).Select(i => new CardData
            {
                ID = $"V9AA_fill_{i}", CardName = "V9AA填充" + i, Supertype = Cardtype.Creature, Power = 1, Life = 1,
            }).ToList();
            aacore.InitGame(CardLoader.BuildDeck(aaFill, 2), CardLoader.BuildDeck(aaFill, 2));
            var aa1 = aacore.Player1;
            var aa2 = aacore.Player2;
            GameActions.SkipElementPool(aacore, aa1);
            var aaPool1 = aacore.ElementPool.GetPool(aa1);
            var aaPool2 = aacore.ElementPool.GetPool(aa2);
            foreach (ManaType t in Enum.GetValues(typeof(ManaType)))
            {
                aaPool1.TempElements[t] = 99;
                aaPool2.TempElements[t] = 99;
            }
            aaPool1.GlobalTurnIndex = 9;
            aaPool2.GlobalTurnIndex = 9;

            Card AACard(string id, string name, Cardtype type, int baseSpeed, Player owner, List<int> kinds)
            {
                var data = new CardData { ID = id, CardName = name, Supertype = type };
                data.Cost = PosCost((ManaType.Gray, 1));
                if (type == Cardtype.Spell)
                    data.Effects.Add(new CardEffectData
                    {
                        Id = id + "_EFF",
                        TriggerTiming = (int)TriggerTiming.OnPlay,
                        BaseSpeed = baseSpeed,
                        SelectionMode = -1,
                        // 指域按场景选必然非空者：{1,2}=敌方生物（配合在场木桩）、{15,16}=对手发动区
                        //（V9.o 已证形态）——候选可达性与速度门解耦，别把「域空」误报成「门禁拦」
                        AtomicEffects = new List<AtomicEffectEntry>
                        {
                            AtomRefs.New(AtomicEffectType.DealDamage, value: 1, kinds: kinds),
                        },
                    });
                var card = new CardWrapper(data);
                card.SetController(owner);
                aacore.ZoneManager.GetZoneContainer(owner).Add(card, Zone.Hand);
                return card;
            }

            // 对手在场木桩：保住 {1,2} 敌方生物域候选非空（只进域、不被结算）
            var aaDummy2 = new CardWrapper(new CardData
            {
                ID = "V9AA_DUMMY", CardName = "V9aa木桩", Supertype = Cardtype.Creature, Power = 1, Life = 5,
            });
            aaDummy2.SetController(aa2);
            VerifySuite.Assert(aacore.ZoneManager.TryAddToBattlefield(aaDummy2, aa2), "④前置 对手木桩入场（保快卡指域非空）");

            var aaSlow = AACard("V9AA_SLOW", "V9aa慢卡", Cardtype.Creature, 0, aa1, null);   // 0 速生物（无效果）
            var aaFast = AACard("V9AA_FAST", "V9aa快卡", Cardtype.Spell, 0, aa1, new List<int> { 1, 2 }); // 法术 1 槽（=速度 2 档·敌生物域）
            var aaAnchor = AACard("V9AA_ANCH", "V9aa锚结界", Cardtype.Enchantment, 0, aa1, null); // 0 速结界锚
            int aaSlowSpeed = SpeedCalculator.GetCardCastSpeed(aaSlow);
            int aaFastSpeed = SpeedCalculator.GetCardCastSpeed(aaFast);
            VerifySuite.Assert(aaSlowSpeed == 0 && aaFastSpeed > 0,
                $"夹具前提：慢卡速度 0 / 快卡速度&gt;0（实际 {aaSlowSpeed}/{aaFastSpeed}）");

            // ---- ①⑤ 自己 0 速施放锚点（空栈首动豁免打出）→ 自家 0 速手牌被禁、严格更快可入 ----
            VerifySuite.Assert(GameActions.PlayCard(aacore, aa1, aaAnchor),
                "⑤ 空栈首动豁免：0 速结界照常打出（新连锁第一动不查速度）");
            var aaOpts1 = GameActions.CollectAvailableResponses(aacore, aa1);
            VerifySuite.Assert(aaOpts1.All(o => o.SourceCard != aaSlow)
                              && !aacore.StackEngine.PushCardCast(aaSlow, aa1, new List<Entity>()),
                "① 自己 0 速锚点下：0 速手牌不进响应候选且引擎拒入（同速自连锁禁——10-18 正主）");
            VerifySuite.Assert(aaOpts1.Any(o => o.SourceCard == aaFast),
                $"② 严格更快（{aaFastSpeed} 速）照常进候选（连锁未被封死）");
            GameActions.DrainStack(aacore);
            if (!aacore.StackEngine.IsEmpty) aacore.StackEngine.Clear();
            aacore.StackEngine.SpeedCounter.Reset();

            // ---- ③ 对手抬速后，回合方同速跟被禁（旧「等于」优惠退役锚）----
            var aaAnchor2 = AACard("V9AA_ANCH2", "V9aa锚结界2", Cardtype.Enchantment, 0, aa1, null);
            var aaFast2 = AACard("V9AA_FAST2", "V9aa快卡2", Cardtype.Spell, 0, aa2, new List<int> { 15, 16 });
            VerifySuite.Assert(GameActions.PlayCard(aacore, aa1, aaAnchor2), "③ 0 速锚2 打出（空栈首动豁免）");
            VerifySuite.Assert(GameActions.PlayCardInResponse(aacore, aa2, aaFast2, new List<Entity> { aaAnchor2 }),
                $"③ 对手以 {SpeedCalculator.GetCardCastSpeed(aaFast2)} 速响应（抬记速器）");
            var aaOpts3 = GameActions.CollectAvailableResponses(aacore, aa1);
            VerifySuite.Assert(aaOpts3.All(o => o.SourceCard != aaFast)
                              && !aacore.StackEngine.PushCardCast(aaFast, aa1, new List<Entity>()),
                $"③ 对手抬速后同速跟被禁：{aaFastSpeed} 速快卡不进候选且引擎拒入（全严格大于——旧「对手抬速后可同速跟」退役）");
            GameActions.DrainStack(aacore);
            if (!aacore.StackEngine.IsEmpty) aacore.StackEngine.Clear();
            aacore.StackEngine.SpeedCounter.Reset();

            // ---- ④ 触发式锚点（登场效果推栈形态：0 速、无台账可读）→ 自家 0 速手牌同样被禁 ----
            var aaTrigData = new CardData
            {
                ID = "V9AA_TRIG", CardName = "V9aa触发者", Supertype = Cardtype.Creature, Power = 1, Life = 5,
            };
            aaTrigData.Effects.Add(new CardEffectData
            {
                Id = "V9AA_TRIG_EFF", TriggerTiming = (int)TriggerTiming.OnPlay, SelectionMode = -1,
                AtomicEffects = new List<AtomicEffectEntry>
                {
                    AtomRefs.New(AtomicEffectType.Heal, value: 1, kinds: new List<int> { 1, 2 }),
                },
            });
            var aaTrig = new CardWrapper(aaTrigData);
            aaTrig.SetController(aa1);
            VerifySuite.Assert(aacore.ZoneManager.TryAddToBattlefield(aaTrig, aa1), "④ 触发者入场");
            var aaDef = GameActions.GetCardEffectDefinitions(aaTrig).FirstOrDefault();
            var aaPending = new PendingEffect
            {
                Effect = aaDef, Source = aaTrig, Controller = aa1,
                ActivationSpeed = 0, ActivationType = EffectActivationType.Mandatory, // 镜像 ProcessTriggeredEffects 推栈形态
                SelectedTargets = new List<Entity>(),
                TimestampInfo = TimestampSystem.CreateTimestamp(),
            };
            VerifySuite.Assert(aaDef != null && aacore.StackEngine.TryActivateEffect(aaPending),
                "④ 登场效果锚点入栈（0 速·触发式——台账已删，门槛不再读归属）");
            var aaOpts4 = GameActions.CollectAvailableResponses(aacore, aa1);
            VerifySuite.Assert(aaOpts4.All(o => o.SourceCard != aaSlow)
                              && aaOpts4.Any(o => o.SourceCard == aaFast),
                "④ 触发式锚点下：自家 0 速手牌被禁（10-17 弹窗 bug 根除锚）、严格更快照常可入");
            aacore.StackEngine.Clear();
            aacore.StackEngine.SpeedCounter.Reset();

            // ---- ⑥ 技能发动待遇（2026-10-18 深夜修订：技能每回合都能重复用，不与同名法术同速同免费）：
            //      发动速度恒 0（不吃法术槽位盈余）+ 免元素锚价 + 抉择技能每次 +1 灰 + 一回合一次 ----
            VerifySuite.Assert(aacore.TurnEngine.CurrentPhase?.Phase == PhaseType.Main
                              && aacore.TurnEngine.TurnPlayer == aa1,
                "⑥前置 主阶段+回合方（InitGame→准备自动推进）");
            var aaSkillData = new CardData { ID = "V9AA_SKILL", CardName = "V9aa技能", Supertype = Cardtype.Spell };
            aaSkillData.Cost = PosCost((ManaType.Gray, 1));
            // 原子=DrawCard（洞察术同款·无指向自结算——无头下不依赖目标反问），value 1/2 区分抉择路由
            aaSkillData.Effects.Add(new CardEffectData
            {
                Id = "V9AA_SK_E0", TriggerTiming = (int)TriggerTiming.Activate_Active, BaseSpeed = 1, SelectionMode = -1,
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DrawCard, value: 1) },
            });
            aaSkillData.Effects.Add(new CardEffectData
            {
                Id = "V9AA_SK_E1", TriggerTiming = (int)TriggerTiming.Activate_Active, BaseSpeed = 1, SelectionMode = -1,
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DrawCard, value: 2) },
            });
            var aaSkill = new CardWrapper(aaSkillData);
            aaSkill.SetController(aa1);
            aacore.ZoneManager.GetZoneContainer(aa1).Add(aaSkill, Zone.FieldZone); // 技能栏=FieldZone
            var aaSkillDefs = GameActions.GetCardEffectDefinitions(aaSkill);
            int aaCastSpeed = SpeedCalculator.GetCardCastSpeed(aaSkill);
            VerifySuite.Assert(aaSkillDefs.Count == 2 && aaCastSpeed == 2
                              && PendingEffect.Create(aaSkillDefs[0], aaSkill, aa1, aa1, PhaseType.Main).ActivationSpeed == 0
                              && PendingEffect.Create(aaSkillDefs[1], aaSkill, aa1, aa1, PhaseType.Main).ActivationSpeed == 0,
                $"⑥ 速度降级：技能发动速度恒 0（def.BaseSpeed 含烘焙盈余={aaSkillDefs[0].BaseSpeed} 但技能口不取；"
                + $"同名法术手牌施放仍 {aaCastSpeed} 速——法术语义不变）");
            int aaHand0 = aacore.ZoneManager.GetCards(aa1, Zone.Hand).Count;
            int aaGray0 = aaPool1.TempElements[ManaType.Gray]; // ①-⑤ 已打过锚结界（各付 1 灰卡费）——起点现取
            VerifySuite.Assert(GameActions.ActivateHeroSkill(aacore, aa1, 1).GetAwaiter().GetResult()
                              && GameActions.DrainStack(aacore)
                              && aacore.ZoneManager.GetCards(aa1, Zone.Hand).Count == aaHand0 + 2
                              && aaPool1.TempElements[ManaType.Gray] == aaGray0 - 1,
                $"⑥ 抉择路由+附加费：effectIndex=1 走第 2 条（抽 2 而非 1）且恰扣 1 灰"
                + $"（{aaGray0}→{aaPool1.TempElements[ManaType.Gray]}；{aaHand0}→{aacore.ZoneManager.GetCards(aa1, Zone.Hand).Count}）");
            VerifySuite.Assert(!GameActions.ActivateHeroSkill(aacore, aa1, 0).GetAwaiter().GetResult(),
                "⑥ 一回合一次：横置后同回合再发动被拒");
            foreach (ManaType t in Enum.GetValues(typeof(ManaType))) aaPool1.TempElements[t] = 0; // 钱包清零
            HeroSkillSystem.ResetSkillForTurn(aacore, aa1); // 回合开始解横置（等价推进一回合）
            VerifySuite.Assert(!GameActions.ActivateHeroSkill(aacore, aa1, 0).GetAwaiter().GetResult(),
                "⑥ 抉择附加费：钱包全 0 发动被拒（1 灰不可付——CanActivate 预检拦）");
            aaPool1.TempElements[ManaType.Gray] = 1;
            VerifySuite.Assert(GameActions.ActivateHeroSkill(aacore, aa1, 0).GetAwaiter().GetResult()
                              && GameActions.DrainStack(aacore)
                              && aacore.ZoneManager.GetCards(aa1, Zone.Hand).Count == aaHand0 + 3
                              && aaPool1.TempElements[ManaType.Gray] == 0,
                $"⑥ 抉择附加费：补 1 灰后发动成功且恰扣 1 灰（再抽 1；灰余 {aaPool1.TempElements[ManaType.Gray]}）");
            // 单效果技能=免费（无抉择附加费）：换 1 效果卡验证（FieldZone 只此一卡，清引用逼 lazy 回填）
            aacore.ZoneManager.GetZoneContainer(aa1).Remove(aaSkill, Zone.FieldZone);
            aa1.HeroSkillCard = null;
            var aaSkill1Data = new CardData { ID = "V9AA_SKILL1", CardName = "V9aa技能1", Supertype = Cardtype.Spell };
            aaSkill1Data.Cost = PosCost((ManaType.Gray, 1));
            aaSkill1Data.Effects.Add(new CardEffectData
            {
                Id = "V9AA_SK1_E0", TriggerTiming = (int)TriggerTiming.Activate_Active, BaseSpeed = 1, SelectionMode = -1,
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DrawCard, value: 1) },
            });
            var aaSkill1 = new CardWrapper(aaSkill1Data);
            aaSkill1.SetController(aa1);
            aacore.ZoneManager.GetZoneContainer(aa1).Add(aaSkill1, Zone.FieldZone);
            VerifySuite.Assert(GameActions.ActivateHeroSkill(aacore, aa1, 0).GetAwaiter().GetResult()
                              && GameActions.DrainStack(aacore)
                              && aacore.ZoneManager.GetCards(aa1, Zone.Hand).Count == aaHand0 + 4,
                $"⑥ 单效果免费：钱包全 0 照常发动（再抽 1；{aaHand0 + 3}→{aacore.ZoneManager.GetCards(aa1, Zone.Hand).Count}）");
            bool aaWalletClean = true;
            foreach (ManaType t in Enum.GetValues(typeof(ManaType)))
                if (aaPool1.TempElements[t] != 0) aaWalletClean = false;
            VerifySuite.Assert(aaWalletClean,
                "⑥ 单效果免费：全程分文未扣（横置即定价，无抉择附加费）");
            aacore.StackEngine.Clear();
            aacore.StackEngine.SpeedCounter.Reset();

            // ============================ V9.ab 运势双≥改版（2026-10-19 定案） ============================
            // 旧口径=双>x 已废：掷 2d6 后双骰都 ≥ 分支费用 x 才触发（表行 9f6e4f49 DisplayName 已先行改版，
            // 此处对齐运行时与参数域）。理论概率 P(x)=((7−x)/6)²：x=1→36/36 恒中（不测）、
            // 2→25/36≈69.44%、3→16/36≈44.44%、4→9/36=25%、5→4/36≈11.11%、6→1/36≈2.78%。
            VerifySuite.Section("V9.ab 运势双≥改版：参数域[1,6]+概率分布 x=2..6（2026-10-19）");
            {
                // ---- ① 参数域单源：x∈[1,6]（双≥下 x=6=双 6 才中，1/36；UI/装载/短文案全经此口） ----
                ComposerCatalog.EngineParamRange(BranchEngineKind.LuckRoll, out int abMin, out int abMax);
                VerifySuite.Assert(abMin == 1 && abMax == 6,
                    $"运势参数域 [1,6]（双≥口径 x=6 入列；实际 [{abMin},{abMax}]）");

                // ---- ② 概率分布：x=2..6 五张引擎卡同场，36000 次「使用卡牌」事件各独立掷骰 ----
                //（奖励=抽 0 张占位：纯采样掷骰播报，Hit 计数走 KeywordAppliedEvent。
                // 每档 σ：x=2≈0.24pp / x=6≈0.09pp，容差 1pp ≥ 3.8σ）
                var abCore = GameCore.Instance;
                abCore.Reset();
                ZoneContainer.Reseed(20261019);
                var abFill = Enumerable.Range(0, 8).Select(i => new CardData
                {
                    ID = $"V9AB_fill_{i}", CardName = "V9AB填充" + i, Supertype = Cardtype.Creature, Power = 1, Life = 1,
                }).ToList();
                abCore.InitGame(CardLoader.BuildDeck(abFill, 2), CardLoader.BuildDeck(abFill, 2));
                var abP1 = abCore.Player1;
                GameActions.SkipElementPool(abCore, abP1);
                int[] abXs = { 2, 3, 4, 5, 6 };
                var abUnits = new List<CardWrapper>();
                var abHits = new int[abXs.Length];
                for (int i = 0; i < abXs.Length; i++)
                {
                    var abData = new CardData
                    {
                        ID = $"V9AB_LUCK_{abXs[i]}", CardName = $"V9AB运势体{abXs[i]}",
                        Supertype = Cardtype.Creature, Power = 1, Life = 5,
                    };
                    abData.Effects.Add(new CardEffectData
                    {
                        Id = $"V9AB_LUCK_{abXs[i]}_EFF", TriggerTiming = (int)TriggerTiming.OnCardPlayed,
                        ActivationType = 1, SelectionMode = -1,
                        AtomicEffects = new List<AtomicEffectEntry>
                        {
                            new AtomicEffectEntry
                            {
                                refId = AtomicEffectTable.GetByType(AtomicEffectType.EngineLuckRoll)?.HashId,
                                value = 1,
                                branch = new BranchEntryData
                                {
                                    settle = (int)BranchSettleKind.Engine,
                                    engine = (int)BranchEngineKind.LuckRoll,
                                    engineParam = abXs[i],
                                    // 奖励=抽 0 张：Then 非空过 converter「条件空转折叠」守卫，handler 零循环零副作用
                                    //（空 Then 会被折为无分支；抽 1 张则 2.5 万次命中会把牌库抽干吃疲劳）
                                    then = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DrawCard, value: 0) },
                                },
                            },
                        },
                    });
                    var abUnit = new CardWrapper(abData);
                    abUnit.SetController(abP1);
                    VerifySuite.Assert(abCore.ZoneManager.TryAddToBattlefield(abUnit, abP1), $"运势引擎卡 x={abXs[i]} 入场");
                    abUnits.Add(abUnit);
                }
                EventManager.Instance.Subscribe<KeywordAppliedEvent>(e =>
                {
                    if (e.Keyword != "运势") return;
                    int j = abUnits.IndexOf(e.Target as CardWrapper);
                    if (j >= 0) abHits[j]++;
                });
                var abStub = new CardWrapper(new CardData
                {
                    ID = "V9AB_STUB", CardName = "V9AB使用桩", Supertype = Cardtype.Creature,
                });
                abStub.SetController(abP1);
                GameRng.Reseed(20261019);
                const int abRolls = 36000;
                for (int i = 0; i < abRolls; i++)
                    abCore.PublishEvent(new CardPlayEvent { Player = abP1, PlayedCard = abStub, FromZone = Zone.Graveyard });
                double[] abExpect = { 25 / 36.0, 16 / 36.0, 9 / 36.0, 4 / 36.0, 1 / 36.0 };
                var abLines = new List<string>();
                for (int i = 0; i < abXs.Length; i++)
                {
                    double freq = abHits[i] / (double)abRolls;
                    abLines.Add($"x={abXs[i]}:{abHits[i]}/{abRolls}={freq:P2}(理论 {abExpect[i]:P2})");
                    VerifySuite.Assert(Math.Abs(freq - abExpect[i]) < 0.01,
                        $"运势双≥概率 x={abXs[i]}：频率 {freq:P2} ≈ 理论 {abExpect[i]:P2}（±1pp 容差——{abHits[i]}/{abRolls}）");
                }
                VerifySuite.Assert(abHits[0] > abHits[1] && abHits[1] > abHits[2] && abHits[2] > abHits[3] && abHits[3] > abHits[4],
                    "触发频率随 x 单调递减（" + string.Join("，", abLines) + "）");
                VerifySuite.Log("运势双≥实测分布（" + abRolls + " 次/档）：" + string.Join("，", abLines));
            }

            VerifySuite.Section("V9 引擎规则回归完成");
        }
    }
}
