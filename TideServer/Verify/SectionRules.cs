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
    /// ⑥V9.h 附加诅咒（AddCurse，信息轴 2026-10-02 定案）端到端——载荷登记/抽到发作/一次性消耗/
    ///   Permanent 换区不清（与 Exposed 对比锚）/缺失载荷空转。
    /// 2026-10-03 定案增补：
    /// ⑦V9.i 规则光环（ModifyGameRule 转正，路线 B）——全局唯一新换旧/双方生效/载体离场失效/
    ///   局重置回收 + 七条规则（旧仪式全量，疾风改双人连两回合）各自可观测接线。
    /// ⑧V9.j 地牌资格三型（生物/法术/结界；衍生物 IsToken/临时卡/耗竭除外）、V9.k 双方减半计价
    ///   （双侧+全取/多目标）+ 错边收缩（负面指己放行）+ RowHashId 变体行计价。
    /// ⑨V9.l/V9.m 端到端四则——衍生物弹回手×地牌拒绝、临时卡真路径（回响/微缩）×地牌拒绝、
    ///   灰不足苏醒立约、归土×回响×唯一槽×墓地配额压力剧本（配额每回合 1 次，光环换任不刷新）。
    /// 2026-10-04 代价栏规则改造增：
    /// ⑪V9.n 任意单向效果·镜像逆转·黑白封顶=地牌上限——PayloadCostDomain 三路（双侧收窄/正确侧镜像/已错侧免写）单元锚 + 镜像端到端（激励逆转到对方侧）+ 大额补偿钳制 + 持久化读回。
    /// 2026-10-04 两层无效定案增：
    /// ⑫V9.o 发动无效（净零成本：施放扣费返还/启动式不扣费横置不重置/触发式不计发动次数/强制桶免疫）
    ///   × 效果无效（扣费照付：施放费用不退/启动式扣费/触发式计入/强制桶受管制）——施放端到端+计数口径+混合批管制面。
    /// 2026-10-05 两槽定案重锚：
    /// ⑬构造形态从「header.EngineKind+AtomicEffects 奖励」改为「槽级原子 branch 载荷」（BranchEntryData/
    ///   BranchPayload）——V9.d 关键词型剔除改判平铺主干 def.Effects；V9.p 目录锚走 OutcomeConditions/
    ///   SituationGates/CurseGateId；Grant 引擎解体=无分支槽原子+效果级持续档计价；诅咒门=branch 载荷
    ///  （Gate·CurseOnDraw）；附两槽装载校验轻锚（主序列主干 ≤2 / 域交集空 → 构筑期 Error）。
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
                    AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DealDamage, value: 3) },
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

            // 法术 4伤+1抽（未声明档——与 Unity 计价锚同数字）：f=d(MaxTier)=0.75；
            // 锚价红4蓝2 ×0.75 取整总额5（最大余数法→红3蓝2）− 底盘退2：默认落最高色红→红1蓝2
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
                $"法术未声明档 f=d(9)=0.75（实际 {fb.Factor:0.###}）——旧 f≡1 已废");
            VerifySuite.Assert((int)fb.DerivedCost[ManaType.Red] == 1
                              && (int)fb.DerivedCost[ManaType.Blue] == 2,
                $"法术 4伤+1抽 = 红1+蓝2（实际 红{(int)fb.DerivedCost[ManaType.Red]}蓝{(int)fb.DerivedCost[ManaType.Blue]}）");
            // 减免落色自标（2026-10-02 定案）：RefundColor=Blue → 底盘退2 尽落蓝 → 红3蓝0
            fbCard.RefundColor = (int)ManaType.Blue;
            var fbBlue = CardCostService.Derive(fbCard);
            VerifySuite.Assert((int)fbBlue.DerivedCost[ManaType.Red] == 3
                              && (int)fbBlue.DerivedCost[ManaType.Blue] == 0,
                $"减免落色自标：退2落蓝 → 红3蓝0（实际 红{(int)fbBlue.DerivedCost[ManaType.Red]}蓝{(int)fbBlue.DerivedCost[ManaType.Blue]}）");

            // ============================ V9.b 域数据（表级） ============================

            VerifySuite.Section("V9.b 域数据（伤害族开放 3,4；冻结=指示物行存储态自己）");
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
            AssertKinds(AtomicEffectType.Freeze, new[] { 0 },
                "冻结（2026-10-07 指示物同型化：存储态=自己——旧表级收紧 1,2 退役，授予域由合成器覆写 {1..8} 后按极性收窄）");

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
            // 耐久计价（2026-10-05 定案 0.5 灰/点）：3 耐久=1.5→取整 2 灰——备灰并放开浓度上限后打出
            var v9cPool = core.ElementPool.GetPool(p1);
            v9cPool.AvailableMana[ManaType.Gray] = 2;
            v9cPool.GlobalTurnIndex = 9;
            VerifySuite.Assert(GameActions.PlayCard(core, p1, ench), "结界从手牌发动（耐久费 2 灰——2026-10-05 耐久计价）");
            GameActions.DrainStack(core);
            VerifySuite.Assert(ench.GetZone() == Zone.Battlefield, "两步式：发动区宣告→付费→进入战场");
            VerifySuite.Assert(ench.GetCounterCount(CounterRules.DurabilityCounter) == 3,
                "入场初始化耐久=3（CardPutToBattlefieldEvent 驱动）");
            core.SBAEngine.ExecuteAll(); // 显式泵一轮 SBA
            VerifySuite.Assert(ench.IsAlive && ench.GetZone() == Zone.Battlefield,
                "SBA 生命管线不扫无生命单位（旧「结界被当 0 防御单位送墓」bug 回归锚）");
            VerifySuite.Assert(ench.IsNonLivingUnit(), "IsNonLivingUnit：战场结界=无生命单位");

            // ---- ② 战斗伤害：受击 -1 耐久、不落血、有效伤害钳 1 ----
            int lifeBefore = ench._life;
            int dealt = KeywordRules.ApplyDamage(p2, ench, 5, isCombat: true);
            VerifySuite.Assert(dealt == 1, $"战斗伤害对结界有效伤害钳 1（实际 {dealt}）");
            VerifySuite.Assert(ench.GetCounterCount(CounterRules.DurabilityCounter) == 2,
                "5 点战斗伤害只掉 1 耐久（剩 2）");
            VerifySuite.Assert(ench._life == lifeBefore, "伤害不落血（绕过生命管线）");
            VerifySuite.Assert(ench.IsAlive && ench.GetZone() == Zone.Battlefield, "结界仍在场");

            // ---- ③ 法术/效果伤害：同一耐久管线 ----
            KeywordRules.ApplyDamage(p1, ench, 3, isCombat: false);
            VerifySuite.Assert(ench.GetCounterCount(CounterRules.DurabilityCounter) == 1,
                "法术伤害同口径 -1 耐久（剩 1）");

            // ---- ④ 耐久归零：直送墓 + Smashed（不经死亡决策表） ----
            DestroyReason? reason = null;
            EventManager.Instance.Subscribe<CardDestroyEvent>(
                e => { if (e.DestroyedCard == ench) reason = e.Reason; });
            KeywordRules.ApplyDamage(p1, ench, 1, isCombat: false);
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

            VerifySuite.Section("V9.d 主动效果：费用移发动时、槽位维持；关键词不得作启动式");
            // ① 启动式锚价构筑期全免（E 桶回归锚）：卡带一个启动式 DealDamage 效果 → EAnchor=0
            var activatedCard = new CardData { ID = "V9_act", CardName = "启动式探针", Supertype = Cardtype.Creature, Power = 1, Life = 1 };
            activatedCard.Effects.Add(new CardEffectData
            {
                Id = "V9_act_onplay",
                TriggerTiming = (int)TriggerTiming.Activate_Active,
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DealDamage, value: 2) },
            });
            var actDerive = CardCostService.Derive(activatedCard);
            VerifySuite.Assert(actDerive.EAnchor == 0,
                $"启动式不进 E 桶（EAnchor={actDerive.EAnchor}——锚价运行时现付，2026-09-08 定案回归锚）");

            // ② 启动式仍占效果槽（2026-10-02 用户定案维持：AI 每卡最大原子数限制的可见性锚）
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
                "启动式照常计效果槽（1 启动式 + 1 登场 = 2 槽——占用维持定案回归锚）");

            // ③ 关键词型 Grant（自指域 {0}）做启动式 → converter 剔除（2026-10-02 定案）
            //（GrantIceCrystal 行已于 2026-10-02 退役；GrantPoisonSting/GrantPathogen 行已随改写光环化删除
            //  2026-10-05——正例改用存续的 GrantRegeneration/GrantStealth）
            VerifySuite.Assert(ComposerCatalog.IsKeywordStyleGrant(AtomicEffectType.GrantRegeneration)
                              && ComposerCatalog.IsKeywordStyleGrant(AtomicEffectType.GrantStealth),
                "关键词型判定：自指域 Grant 行（再生/潜行授予）= 关键词型");
            VerifySuite.Assert(!ComposerCatalog.IsKeywordStyleGrant(AtomicEffectType.GrantMagnify)
                              && !ComposerCatalog.IsKeywordStyleGrant(AtomicEffectType.DealDamage),
                "关键词型判定：赋予型（放大，域含他人）与非 Grant 行不受限");

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
                    new EffectStepData { kind = 0, atomic = AtomRefs.New(AtomicEffectType.GrantStealth) }, // 步骤内 → 整步剔除
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
                "启动式平铺主干：关键词型 Grant（再生）被剔除");
            VerifySuite.Assert(kwFlat.Contains(AtomicEffectType.DealDamage), "启动式平铺主干：非关键词原子保留");
            VerifySuite.Assert(!kwFlat.Contains(AtomicEffectType.GrantStealth),
                "启动式平铺主干：关键词型原子步骤被整步剔除（GrantStealth 步折平后不出现）");
            var kwThenTypes = kwDef.Effects.Where(a => a.Branch != null)
                .SelectMany(a => a.Branch.Then).Select(a => a.Type).ToList();
            VerifySuite.Assert(!kwThenTypes.Contains(AtomicEffectType.GrantLifesteal)
                              && kwDef.Effects.All(a => a.Branch == null),
                "启动式分支奖励：关键词型（吸血授予）被剔除——Then 空→门步整体丢弃（不产 Branch 载荷）");

            // ④ 赋予型 Grant 做启动式 → 放行（赋予关键词可以）
            var grantAct = new CardEffectData
            {
                Id = "V9_grant_act",
                TriggerTiming = (int)TriggerTiming.Activate_Active,
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.GrantMagnify, value: 1) },
            };
            var grantDef = CardEffectConverter.ConvertOne(grantAct, "V9_src");
            VerifySuite.Assert(grantDef.Effects.Any(a => a.Type == AtomicEffectType.GrantMagnify),
                "赋予型 Grant（放大，域含他人）做启动式放行");

            // ============================ V9.e 主动⇔启动式钉死（2026-10-02 定案） ============================

            VerifySuite.Section("V9.e 发动方式钉死：主动=启动式 / 自动=可选触发式 / 强制无歧义");
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
                "钉死后按启动式计：构筑期不计锚价（ElementCostPrepaid=false）");

            var voluntaryInstant = CardEffectConverter.ConvertOne(new CardEffectData
            {
                Id = "V9_vol_inst",
                ActivationType = (int)EffectActivationType.Voluntary,
                TriggerTiming = (int)TriggerTiming.Activate_Instant,           // 已是启动式 → 保留具体档
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DealDamage, value: 1) },
            }, "V9_src");
            VerifySuite.Assert(voluntaryInstant.TriggerTiming == TriggerTiming.Activate_Instant
                              && voluntaryInstant.ActivationType == EffectActivationType.Voluntary,
                "主动+Activate_Instant：合法组合原样保留（启动式具体档不动）");

            var actMandatory = CardEffectConverter.ConvertOne(new CardEffectData
            {
                Id = "V9_act_mand",
                ActivationType = (int)EffectActivationType.Mandatory,           // 启动式声明强制 = 反向矛盾
                TriggerTiming = (int)TriggerTiming.Activate_Active,
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DealDamage, value: 1) },
            }, "V9_src");
            VerifySuite.Assert(actMandatory.ActivationType == EffectActivationType.Voluntary,
                "启动式+强制 → 覆写回主动（既有对称校验回归锚）");

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
            var bankSnap = new Dictionary<ManaType, int>(bankF.AvailableMana);
            try
            {
                foreach (ManaType t in Enum.GetValues(typeof(ManaType))) bankF.AvailableMana[t] = 99;

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
                int grayBefore = bankF.AvailableMana[ManaType.Gray];
                VerifySuite.Assert(GameActions.PlayCard(fs, f1, sleepCard), "立约沉睡卡打出（灰豁免后仅需绿1）");
                GameActions.DrainStack(fs);
                VerifySuite.Assert(bankF.AvailableMana[ManaType.Gray] == grayBefore, "灰费豁免：3 灰未扣（转沉睡时长）");
                VerifySuite.Assert(sleepCard.GetCounterCount(KeywordRules.SleepCounter) == 3,
                    $"入场沉睡 3 层（=豁免灰量；实际 {sleepCard.GetCounterCount(KeywordRules.SleepCounter)}）");
                VerifySuite.Assert(sleepCard.IsTapped(), "横置进沉睡");

                // ---- ① 未立约（对照）：总费 4 受浓度上限压制——推进到回合 5（上限 5）后照扣全价 ----
                // （立约卡②回合 1 即可出 vs 未立约①须等上限成长——苏醒死循环的修复对照）
                var f1u = fs.Player2;
                for (int turnCycle = 0; turnCycle < 2; turnCycle++)
                {
                    GameActions.EndTurn(fs, f1);
                    fs.TurnEngine.CheckPhaseTransition();
                    GameActions.SkipElementPool(fs, f1u);
                    GameActions.EndTurn(fs, f1u);
                    fs.TurnEngine.CheckPhaseTransition();
                    GameActions.SkipElementPool(fs, f1);
                }
                int grayUncommittedBefore = bankF.AvailableMana[ManaType.Gray];
                VerifySuite.Assert(GameActions.PlayCard(fs, f1, uncommittedCard, null, Zone.Hand, 0, out var rejectF1),
                    $"未立约沉睡卡照常打出（付全价；拒绝原因：{rejectF1 ?? "无"}）");
                GameActions.DrainStack(fs);
                VerifySuite.Assert(bankF.AvailableMana[ManaType.Gray] == grayUncommittedBefore - 3,
                    $"未立约对照：灰照扣 3（{grayUncommittedBefore}→{bankF.AvailableMana[ManaType.Gray]}——豁免须先手牌立约）");

                // ---- ③ 定长沉睡照付（不变：显式层数不走灰豁免，立约与否同价） ----

                // 定长卡总费 3 > 首回合地牌槽上限 1——推进两回合（上限=min(回合,9)=3）再打
                var f2 = fs.Player2;
                GameActions.EndTurn(fs, f1);
                fs.TurnEngine.CheckPhaseTransition();
                GameActions.SkipElementPool(fs, f2);
                GameActions.EndTurn(fs, f2);
                fs.TurnEngine.CheckPhaseTransition();
                GameActions.SkipElementPool(fs, f1);

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
                int grayBefore2 = bankF.AvailableMana[ManaType.Gray];
                VerifySuite.Assert(GameActions.PlayCard(fs, f1, fixedCard), "定长沉睡卡打出");
                GameActions.DrainStack(fs);
                VerifySuite.Assert(bankF.AvailableMana[ManaType.Gray] == grayBefore2 - 2,
                    "定长沉睡不走灰豁免：照扣 2 灰");
                VerifySuite.Assert(fixedCard.GetCounterCount(KeywordRules.SleepCounter) == 2,
                    $"定长沉睡 2 层（实际 {fixedCard.GetCounterCount(KeywordRules.SleepCounter)}）");
            }
            finally
            {
                foreach (var kv in bankSnap) bankF.AvailableMana[kv.Key] = kv.Value;
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
                gcore.ElementPool.GetPool(g1).AvailableMana[t] = 99;

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

            // ============================ V9.h 附加诅咒（AddCurse，信息轴 2026-10-02 定案） ============================

            VerifySuite.Section("V9.h 附加诅咒（AddCurse）：载荷登记/抽到发作/一次性消耗/Permanent");
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
                hcore.ElementPool.GetPool(h1).AvailableMana[t] = 99;

            Card HToHand(Player owner, CardData data)
            {
                var card = new CardWrapper(data);
                card.SetController(owner);
                hcore.ZoneManager.GetZoneContainer(owner).Add(card, Zone.Hand);
                return card;
            }

            // 载荷 = Effects.json 现成条目（纯原子 steps、恰一个抽卡原子、无分支载荷——发作时施诅方
            // 抽 N 张可观测；两槽定案后瘦条目无引擎头字段（engine 已删），分支挂原子 branch；
            // 条件分支类载荷的评估路径不走此夹具——抽牌时点无上游产出，门恒否）
            var drawRow = AtomicEffectTable.GetByType(AtomicEffectType.DrawCard);
            VerifySuite.Assert(drawRow != null, "抽卡表行存在");
            var payloadEntry = EffectsLibrary.GetAll().FirstOrDefault(d => d != null
                && d.steps != null && d.steps.Count > 0
                && d.steps.All(s => s != null && s.kind == 0 && s.atom != null && s.atom.branch == null)
                && d.steps.Count(s => s.atom.refId == drawRow.HashId) == 1);
            // Effects.json 2026-10-04 起为用户清空态（真效果入库前装载 0 属预期——记忆 effects-json-reset-real-content）：
            // 载荷候选缺失时软跳过本夹具（Log 留痕不硬断言），后续 V9.i 光环段照常回归。
            if (payloadEntry == null)
                VerifySuite.Log("V9.h 跳过：Effects.json 无「纯原子 steps+恰一抽卡」载荷候选（真效果入库前预期态）");
            var payloadDrawAtom = payloadEntry?.steps
                .FirstOrDefault(s => s.atom != null && s.atom.refId == drawRow.HashId)?.atom;
            var payloadId = payloadEntry?.id;
            int expectedDraws = payloadDrawAtom != null ? Math.Max(1, payloadDrawAtom.value) : 0;

            Card MakeCurseSpell(string id, string payload)
            {
                var data = new CardData { ID = id, CardName = "V9H诅咒术", Supertype = Cardtype.Spell };
                data.Effects.Add(new CardEffectData
                {
                    Id = id + "_EFF",
                    TriggerTiming = (int)TriggerTiming.OnPlay,
                    SelectionMode = -1, // None 自结算=随机对方牌库一张/次（不给施放者看对方牌库）
                    AtomicEffects = new List<AtomicEffectEntry>
                    {
                        AtomRefs.New(AtomicEffectType.AddCurse, value: 1, kinds: new List<int> { 8 }, str: payload),
                    },
                });
                return HToHand(h1, data);
            }

            // ---- ① 挂层 + 载荷登记 ----（载荷候选缺失=清空态：整段软跳过，cursed 恒 null → ②③ 顺位空转）
            Card cursed = null;
            if (payloadId != null)
            {
                var curseCard1 = MakeCurseSpell("V9H_CURSE_1", payloadId);
                VerifySuite.Assert(GameActions.PlayCard(hcore, h1, curseCard1, null, Zone.Hand, 0, out var rejectH1),
                    $"诅咒术打出（拒绝原因：{rejectH1 ?? "无"}）");
                GameActions.DrainStack(hcore);
                cursed = hcore.ZoneManager.GetCards(h2, Zone.Deck)
                    .FirstOrDefault(c => c.GetCounterCount(CounterRules.CurseCounter) > 0);
                VerifySuite.Assert(cursed != null && cursed.GetCounterCount(CounterRules.CurseCounter) == 1,
                    "随机对方牌库一张挂 Curse=1（None 自结算）");
                var registered = CurseSystem.GetCurses(cursed);
                VerifySuite.Assert(registered.Count == 1
                                  && registered[0].effectId == payloadId && registered[0].caster == h1,
                    $"载荷登记完整（效果 id+施诅方；实际 {registered.Count} 条）");
            }

            // ---- ② Permanent：换区不清（对比锚——V9.g Exposed 换区即清） ----
            if (cursed != null)
            {
                var hz = hcore.ZoneManager.GetZoneContainer(h2);
                hz.Move(cursed, Zone.Deck, Zone.Hand);
                VerifySuite.Assert(cursed.GetCounterCount(CounterRules.CurseCounter) == 1, "Deck→Hand 诅咒不清（Permanent）");
                hz.Move(cursed, Zone.Hand, Zone.Deck);
                VerifySuite.Assert(cursed.GetCounterCount(CounterRules.CurseCounter) == 1, "Hand→Deck 诅咒不清（活过换区）");
            }

            // ---- ③ 抽到发作：消耗+播报+载荷落地（施诅方抽 N——SettleAsync 同步续行完成） ----
            if (cursed != null)
            {
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
                VerifySuite.Assert(h1HandAfter - h1HandBefore == expectedDraws,
                    $"载荷落地：施诅方抽 {expectedDraws}（{h1HandBefore}→{h1HandAfter}——恰一次，CardDrawEvent 双发不重入）");
            }

            // ---- ④ 载荷缺失路径：层照常消耗、不执行任何原子 ----
            // ③ 的循环可能已抽干 p2 牌库（None 自结算遇空库直接 return）——先从手牌回填几张保证有随机目标
            var hz4 = hcore.ZoneManager.GetZoneContainer(h2);
            var h2Hand = hcore.ZoneManager.GetCards(h2, Zone.Hand);
            for (int i = 0; i < 3 && i < h2Hand.Count; i++)
                hz4.Move(h2Hand[i], Zone.Hand, Zone.Deck);
            var curseCard2 = MakeCurseSpell("V9H_CURSE_2", "V9H_missing_payload");
            VerifySuite.Assert(GameActions.PlayCard(hcore, h1, curseCard2, null, Zone.Hand, 0, out var rejectH2),
                $"空载荷诅咒术打出（拒绝原因：{rejectH2 ?? "无"}）");
            GameActions.DrainStack(hcore);
            var cursed2 = hcore.ZoneManager.GetCards(h2, Zone.Deck)
                .FirstOrDefault(c => c.GetCounterCount(CounterRules.CurseCounter) > 0);
            VerifySuite.Assert(cursed2 != null, "空载荷目标同样挂层（Attach 只校验 id 非空）");
            if (cursed2 != null)
            {
                int h1HandBefore2 = hcore.ZoneManager.GetCards(h1, Zone.Hand).Count;
                for (int i = 0; i < 64 && cursed2.GetZone() == Zone.Deck; i++)
                {
                    if (hcore.ZoneManager.DrawCard(h2) == null) break;
                }
                VerifySuite.Assert(cursed2.GetZone() == Zone.Hand
                                  && cursed2.GetCounterCount(CounterRules.CurseCounter) == 0
                                  && CurseSystem.GetCurses(cursed2).Count == 0,
                    "缺失载荷：层照常消耗（一次性定案——不赖账留层）");
                VerifySuite.Assert(hcore.ZoneManager.GetCards(h1, Zone.Hand).Count == h1HandBefore2,
                    "缺失载荷空转：不执行任何原子（手牌数不变）");
            }

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
                icore.ElementPool.GetPool(i1).AvailableMana[t] = 99; // 夹具卡免费用障碍（局即弃不还原）
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
            // 丰盈载体被摧毁（耐久 9 逐点——结界无生命单位不走离散改写/伤害帽）→ 溢出回落基线
            for (int d = 0; d < 9; d++) KeywordRules.ApplyDamage(i2, bloomCarrier, 1, isCombat: true);
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

            // ---- ④ 载体离场失效（可被摧毁：耐久归零 Smashed 管线） ----
            KeywordRules.ApplyDamage(i2, riverCarrier, 1, isCombat: true);
            KeywordRules.ApplyDamage(i2, riverCarrier, 1, isCombat: true);
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
                icore.ElementPool.GetPool(i1).AvailableMana[t] = 99;
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
                icore.ElementPool.GetPool(i1).AvailableMana[t] = 99;
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
            CounterRules.OnTurnEnd(i2, icore.ZoneManager); // 持有者回合末 ①：2→1
            VerifySuite.Assert(lockProbe.GetCounterCount(CounterRules.LockCounter) == 1
                              && !RuleHooks.CanPlay(icore, i2, lockProbe, Zone.Hand),
                "锁定×2 首个持有者回合末：2→1 仍锁定");
            CounterRules.OnTurnEnd(i2, icore.ZoneManager); // 持有者回合末 ②：1→0
            VerifySuite.Assert(lockProbe.GetCounterCount(CounterRules.LockCounter) == 0
                              && RuleHooks.CanPlay(icore, i2, lockProbe, Zone.Hand),
                "锁定×2 次个持有者回合末：1→0 解锁（手牌区倒数全程在场外结算）");

            GameActions.EndTurn(icore, i1); // i1 回合结束：③ 块倒数 i1 被展示卡锁 1→0；其后光环对 i2 仍被展示的卡续锁
            VerifySuite.Assert(i2Locked.GetCounterCount(CounterRules.LockCounter) == 1,
                "持续暴露续锁：每回合结束对仍被展示的卡再赋一回合锁定（倒数先、挂层后）");
            icore.TurnEngine.CheckPhaseTransition(); // → i2 回合
            GameActions.SkipElementPool(icore, i2);
            GameActions.EndTurn(icore, i2); // 回合交还 i1（⑧-⑩ 同一主阶段连续驱动）
            icore.TurnEngine.CheckPhaseTransition();
            GameActions.SkipElementPool(icore, i1);

            // ---- ⑧ 三相：消耗 3 同色纯元素 → 红/蓝/绿各 1 ----
            var trinityCarrier = MakeRuleCarrier("三相仪典", RuleAuraComponents.ElementConversion, 2);
            VerifySuite.Assert(GameActions.PlayCard(icore, i1, trinityCarrier, null, Zone.Hand, 0, out var rejectI6),
                $"三相载体打出（拒绝原因：{rejectI6 ?? "无"}）");
            GameActions.DrainStack(icore);
            var iBank = icore.ElementPool.GetPool(i1).AvailableMana;
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

            // ---- ⑩b 疾风（2026-10-04 改造）：从手牌使用的卡发动速度+1 ----
            var speedData1 = new CardData { ID = "V9I_SPEED1", CardName = "速度探针1", Supertype = Cardtype.Spell };
            speedData1.Effects.Add(new CardEffectData { Id = "V9I_SPEED1_EFF", BaseSpeed = 1 });
            var speedProbe = IToHand(i1, speedData1);
            var speedProbe0 = IToHand(i1, new CardData
            {
                ID = "V9I_SPEED0", CardName = "速度探针0", Supertype = Cardtype.Spell,
            }); // 无效果=0 速档
            VerifySuite.Assert(SpeedCalculator.GetCardCastSpeed(speedProbe) == 1
                              && SpeedCalculator.GetCardCastSpeed(speedProbe0) == 0,
                "对照（无疾风）：施放速度=卡面声明（1速/0速）");
            var galeSpeedCarrier = MakeRuleCarrier("疾风仪典", RuleAuraComponents.CastSpeedUp, 2);
            VerifySuite.Assert(GameActions.PlayCard(icore, i1, galeSpeedCarrier, null, Zone.Hand, 0, out var rejectI8b),
                $"疾风载体打出（拒绝原因：{rejectI8b ?? "无"}）");
            GameActions.DrainStack(icore);
            VerifySuite.Assert(SpeedCalculator.GetCardCastSpeed(speedProbe) == 2
                              && SpeedCalculator.GetCardCastSpeed(speedProbe0) == 1,
                $"疾风：从手牌使用的卡发动速度+1（1速→2，0速→1；实际 {SpeedCalculator.GetCardCastSpeed(speedProbe)}/{SpeedCalculator.GetCardCastSpeed(speedProbe0)}）");

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
            var tempCard = new CardWrapper(fillerJ[1]) { ID = "V9J_TEMP", IsTemporary = true };
            tempCard.SetController(j1);
            jcore.ZoneManager.GetZoneContainer(j1).Add(tempCard, Zone.Hand);
            VerifySuite.Assert(!jcore.ElementPool.AddCardToPool(tempCard, j1), "临时卡不可作地牌（回归锚）");
            var depletedCard = new CardWrapper(fillerJ[2]) { ID = "V9J_DEPLETED", WasDepletedAsLand = true };
            depletedCard.SetController(j1);
            jcore.ZoneManager.GetZoneContainer(j1).Add(depletedCard, Zone.Hand);
            VerifySuite.Assert(!jcore.ElementPool.AddCardToPool(depletedCard, j1), "已耗竭卡不可作地牌（回归锚——资源枯竭不可逆）");

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
            // 地牌槽上限按「自己回合开始数」涨（j1 已开始 2 个回合=上限 2，已占 2）——再推进一轮到 3
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

            // ---- ② 双侧域单体任选一侧 → 不减（宽域选一≠同时作用双方） ----
            var dmgPickOne = CardCostService.Derive(KCard("V9K_DMG_PICK1", new List<int> { 1, 2 }, 3, AtomicEffectType.DealDamage, targetCount: 1));
            var dmgOwn = CardCostService.Derive(KCard("V9K_DMG_OWN", new List<int> { 1 }, 3, AtomicEffectType.DealDamage, targetCount: 1));
            VerifySuite.Assert(dmgPickOne.DerivedTotal == dmgOwn.DerivedTotal,
                $"双侧域单体任选不減（{dmgPickOne.DerivedTotal} == 单侧同款 {dmgOwn.DerivedTotal}）");

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
            const int kChassisRefund = 2; // 法术底盘退费（攻守默认退 2——V9.a 同口径）
            // 整卡链：锚×0.5（恒双方）× f(未声明档=d(9)=0.75) 取整 − 底盘退2（下限0）——锚价动态读表
            int expectedAuraTotal = Math.Max(0, (int)Math.Round((capRow?.TotalUnitCost ?? 0f) * 0.5f * 0.75f,
                MidpointRounding.AwayFromZero) - kChassisRefund);
            VerifySuite.Assert(auraCost.DerivedTotal == expectedAuraTotal,
                $"规则光环整卡计价=round(锚{capRow?.TotalUnitCost ?? 0f}×0.5×0.75)−底盘退2（实际 {auraCost.DerivedTotal}，期望 {expectedAuraTotal}）");
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
                    AtomRefs.New(AtomicEffectType.Poison, value: 1, kinds: new List<int> { 1 }), // 剧毒 p=-1 己方锁
                },
            }, "V9K");
            VerifySuite.Assert(selfHarmDef.Effects.Any(a => a.Type == AtomicEffectType.Poison),
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
            var lbank = lcore.ElementPool.GetPool(l1).AvailableMana;
            foreach (ManaType t in Enum.GetValues(typeof(ManaType))) lbank[t] = 99;

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
            try
            {
                var summonCard = LSpell("V9L_SUMMON", AtomicEffectType.SummonToken, str: tokenTpl.ID);
                // 落区显式声明（合成器三档惯例——缺省 0=Zone.Hand 是已知陷阱，见 CostDerivation 落区注释）
                (summonCard as CardWrapper).GetData().Effects[0].SummonDropZone = (int)Zone.Battlefield;
                VerifySuite.Assert(GameActions.PlayCard(lcore, l1, summonCard), "召唤术打出（SummonToken 真路径）");
                GameActions.DrainStack(lcore);
                var summoned = lcore.ZoneManager.GetCards(l1, Zone.Battlefield)
                    .FirstOrDefault(c => c.IsToken && c.ID != null && c.ID.StartsWith(tokenTpl.ID + "#"));
                VerifySuite.Assert(summoned != null, "衍生物入场且带 IsToken 标记");
                var bounceCard = LSpell("V9L_BOUNCE", AtomicEffectType.ReturnToHand);
                VerifySuite.Assert(GameActions.PlayCard(lcore, l1, bounceCard,
                    new List<Entity> { summoned }, Zone.Hand, 0), "弹回术打出（预选目标=衍生物）");
                GameActions.DrainStack(lcore);
                VerifySuite.Assert(summoned.GetZone() == Zone.Hand, "衍生物被弹回手牌");
                VerifySuite.Assert(!lcore.ElementPool.AddCardToPool(summoned, l1),
                    "弹回手牌的衍生物不能作为地牌（IsToken——2026-10-03 守卫）");
            }
            finally
            {
                CardCore.Attribute.Handlers.SummonTokenHandler.ResolveTemplate = prevResolver;
            }

            // ---- ② 临时卡真路径（回响效果复制 + 微缩复制）→ 不能作地牌（放大同微缩管线不重复驱动） ----
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
                .FirstOrDefault(c => c.IsTemporary && c.ID != null && c.ID.StartsWith("V9L_ECHO#t"));
            VerifySuite.Assert(echoCopy != null, "回响结算期获得临时复制（EchoCopy 普通效果真路径）");
            VerifySuite.Assert(!lcore.ElementPool.AddCardToPool(echoCopy, l1), "回响临时卡不能作为地牌");

            var miniHost = new CardWrapper(new CardData
            {
                ID = "V9L_MINI_HOST", CardName = "微缩宿主", Supertype = Cardtype.Creature, Power = 2, Life = 2,
            });
            miniHost.SetController(l1);
            VerifySuite.Assert(lcore.ZoneManager.TryAddToBattlefield(miniHost, l1), "微缩宿主入场");
            var grantMiniCard = LSpell("V9L_GRANT_MINI", AtomicEffectType.GrantMiniature, kinds: new List<int> { 1 });
            VerifySuite.Assert(GameActions.PlayCard(lcore, l1, grantMiniCard,
                new List<Entity> { miniHost }, Zone.Hand, 0), "微缩授予术打出（目标=宿主）");
            GameActions.DrainStack(lcore);
            VerifySuite.Assert(miniHost.HasKeyword(KeywordRules.Miniature), "宿主获得微缩关键词");
            var anyPlayData = new CardData { ID = "V9L_ANYPLAY", CardName = "V9L任意牌", Supertype = Cardtype.Spell };
            var anyPlayCard = LToHand(l1, anyPlayData);
            VerifySuite.Assert(GameActions.PlayCard(lcore, l1, anyPlayCard), "任意牌打出（触发微缩复制）");
            GameActions.DrainStack(lcore);
            var miniCopy = lcore.ZoneManager.GetCards(l1, Zone.Hand)
                .FirstOrDefault(c => c.IsTemporary && c.ID != null && c.ID.StartsWith("V9L_ANYPLAY#t"));
            VerifySuite.Assert(miniCopy != null, "微缩复制入手（1/1 费1灰——TempCopyRules 真路径；放大同管线）");
            VerifySuite.Assert(!lcore.ElementPool.AddCardToPool(miniCopy, l1), "微缩临时卡不能作为地牌");

            // ---- ③ 灰不足：未立约用不出（死循环根源锚）→ 立约后成功使用 ----
            lbank[ManaType.Gray] = 0;
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
                mcore.ElementPool.GetPool(m1).AvailableMana[t] = 99;
            mcore.ElementPool.GetPool(m1).GlobalTurnIndex = 9; // 浓度上限放开（2026-10-04 调价适配，同 V9.i）

            var graveRow = AtomicEffectTable.GetAll().FirstOrDefault(r => r != null && r.DisplayName == "归土仪典");
            VerifySuite.Assert(graveRow != null, "归土仪典表行存在（前置）");
            CardData MakeEchoAura(string id)
            {
                var d = new CardData
                {
                    ID = id, CardName = "V9M回响归土", Supertype = Cardtype.Enchantment, Durability = 9,
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
                => mcore.ZoneManager.GetCards(m1, Zone.Hand).Where(c => c.IsTemporary).ToList();

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

            VerifySuite.Section("V9.n 代价栏：任意单向·镜像逆转·产出不封（使用侧=支付浓度上限）·全价地牌门槛·槽位计费·无目标回手·补偿后置·先扣卡费（2026-10-04 本轮）");
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
            foreach (ManaType t in Enum.GetValues(typeof(ManaType))) npool.AvailableMana[t] = 99;

            // ---- ① PayloadCostDomain 三路单元锚（放置口/候选过滤共用口） ----
            var attackRow = AtomicEffectTable.GetByType(AtomicEffectType.Attack);       // TK={2} 纯敌方 有害
            var untapRow = AtomicEffectTable.GetByType(AtomicEffectType.Untap);         // TK={1} 纯己方 有益
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
            npool.AvailableMana[ManaType.White] = 0;
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
            VerifySuite.Assert(npool.AvailableMana[ManaType.White] == untapGrant
                              && npool.WhiteGainedThisTurn == untapGrant,
                $"镜像补偿：得白 {npool.AvailableMana[ManaType.White]} = 全价 {untapGrant} 全量入账（2026-10-04 本轮：补偿后置+产出不封，旧 Min(全价,封顶) 退役公式清理）");

            // ---- ③ 全价过地牌门槛（2026-10-04 本轮定案）：全价 > 地牌槽上限 → 整卡不可使用 ----
            npool.GlobalTurnIndex = 2; // 地牌上限 2 —— 全价必超
            npool.AvailableMana[ManaType.Black] = 0;
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
            VerifySuite.Assert(n1.MaxHealth == n1MaxBefore && npool.AvailableMana[ManaType.Black] == 0,
                "全价门槛拦截：走不到代价发动（上限未流失、无黑补偿）");

            // ---- ③b 门槛放行 + 代价恒执行 + 补偿后置全额 ----
            var smallPayload = AtomRefs.New(AtomicEffectType.LifeLoss, value: 1, kinds: lifeKinds2);
            int smallGrant = CostDerivationService.PayloadUnitGrant(
                CardEffectConverter.ConvertPayloadForDisplay(smallPayload));
            VerifySuite.Assert(smallGrant >= 1 && smallGrant <= 9,
                $"前置：小额代价全价 1..9（实际 {smallGrant}——门槛可在 cap≤9 内放行）");
            npool.GlobalTurnIndex = smallGrant; // cap = 全价（门槛恰好过）
            int n1MaxBefore2 = n1.MaxHealth;
            var smallCardData = new CardData { ID = "V9N_SMALL", CardName = "V9n小额代价卡", Supertype = Cardtype.Spell };
            smallCardData.PayloadCost = new CostEntry { CostType = (int)CostType.Payload, Value = 1, payload = smallPayload };
            var smallCard = new CardWrapper(smallCardData);
            smallCard.SetController(n1);
            pcore.ZoneManager.GetZoneContainer(n1).Add(smallCard, Zone.Hand);
            VerifySuite.Assert(GameActions.PlayCard(pcore, n1, smallCard), "门槛放行：全价 ≤ 地牌槽上限 → 可打出");
            GameActions.DrainStack(pcore);
            VerifySuite.Assert(n1.MaxHealth == n1MaxBefore2 - 1, $"代价恒执行：己方角色上限 -1（{n1MaxBefore2}→{n1.MaxHealth}）");
            VerifySuite.Assert(npool.AvailableMana[ManaType.Black] == smallGrant && npool.BlackGainedThisTurn == smallGrant,
                $"补偿后置全额：生效后立即得黑 {npool.AvailableMana[ManaType.Black]} = 全价 {smallGrant}（快照在执行前取）");

            // ---- ④ 黑白获得双限制直测（2026-10-05 定案，取代 2026-10-04「产出不封」）+ 使用侧约束 ----
            var tpool = pcore.ElementPool.GetPool(n2);
            tpool.GlobalTurnIndex = 2;
            tpool.BlackGainedThisTurn = 0; tpool.WhiteGainedThisTurn = 0;
            tpool.AvailableMana[ManaType.Black] = 0; tpool.AvailableMana[ManaType.White] = 0;
            VerifySuite.Assert(pcore.ElementPool.AddMana(n2, ManaType.Black, null, 3) == 2
                              && tpool.AvailableMana[ManaType.Black] == 2 && tpool.BlackGainedThisTurn == 2,
                "单次钳制：cap2 时单次入黑 3 → 实发 2（单次获得 ≤ 浓度上限）");
            VerifySuite.Assert(pcore.ElementPool.AddMana(n2, ManaType.Black, null, 1) == 0
                              && tpool.AvailableMana[ManaType.Black] == 2 && tpool.BlackGainedThisTurn == 2,
                "回合总量钳制：本回合黑已满 2 → 再入 1 实发 0（单回合获得总量 ≤ 浓度上限）");
            VerifySuite.Assert(pcore.ElementPool.AddMana(n2, ManaType.White, null, 5) == 2
                              && tpool.WhiteGainedThisTurn == 2,
                "白同口径：黑白各自独立计单次/回合总量（白 5 → 实发 2）");
            pcore.ElementPool.OnTurnStart(n2, 3);
            VerifySuite.Assert(tpool.BlackGainedThisTurn == 0 && tpool.WhiteGainedThisTurn == 0,
                "回合开始：黑白获得台账清零（总量钳制按新回合重计）");
            tpool.GlobalTurnIndex = 9;
            VerifySuite.Assert(pcore.ElementPool.AddMana(n2, ManaType.Black, null, 15) == 9
                              && tpool.AvailableMana[ManaType.Black] == 2 + 9 && tpool.BlackGainedThisTurn == 9,
                "大额 15 · cap9 → 实发 9（单次与回合总量同受浓度上限约束）");
            // 使用侧（支付）约束不变：bank 黑 11 · cap2——黑3 需求 > cap → 拒付（获得钳制不改变支付侧口径）
            tpool.GlobalTurnIndex = 2;
            VerifySuite.Assert(ElementPaymentValidator.GetBillPaymentPlan(
                                  new Dictionary<ManaType, int> { { ManaType.Black, 3 } },
                                  tpool.AvailableMana, 2) == null,
                "使用侧封顶：bank 黑 11 · cap2 → 黑3 需求拒付（单次支付贡献 ≤ 地牌上限）");
            VerifySuite.Assert(ElementPaymentValidator.GetBillPaymentPlan(
                                  new Dictionary<ManaType, int> { { ManaType.Black, 2 } },
                                  tpool.AvailableMana, 2) != null,
                "使用侧封顶：黑2 ≤ cap2 → 可付（上限=单次使用量约束）");

            // ---- ⑤ 持久化读回：卡层 payload 字段 → CardData.PayloadCost（旧档无字段回落 null） ----
            var persistJson = "{\"cards\":[{\"id\":\"V9N_PERSIST\",\"cardName\":\"V9n持久化代价卡\",\"supertype\":\"Spell\","
                + "\"power\":0,\"life\":0,\"costList\":[1,0,0,0,0,0],"
                + "\"payload\":{\"refId\":\"" + untapRow.HashId + "\",\"value\":1,\"kinds\":[2]}},"
                + "{\"id\":\"V9N_LEGACY\",\"cardName\":\"V9n旧档卡\",\"supertype\":\"Spell\",\"power\":0,\"life\":0}],"
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
            VerifySuite.Assert(CardCompositionCost.ChassisAdjust(slotCardData) == 3 - (1 + 1 + 2),
                "底盘同口径：攻1+守1+槽2 → 3−4 = −1（加价 1 灰——不是特殊判）");
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
            npool.AvailableMana[ManaType.White] = 0;
            npool.WhiteGainedThisTurn = 0;
            npool.AvailableMana[ManaType.Gray] = 1; // 恰好够默认灰1——回退则分文未扣
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
            VerifySuite.Assert(npool.AvailableMana[ManaType.Gray] == 1 && npool.AvailableMana[ManaType.White] == 0,
                $"回退不付费不补偿（灰仍 {npool.AvailableMana[ManaType.Gray]}、白仍 {npool.AvailableMana[ManaType.White]}）");

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
                              && npool.AvailableMana[ManaType.Gray] == 1,
                "手牌域代价无有效目标：发动失败回退手牌（非战场域按域重解析，不误杀；灰未扣）");
            foreach (var h in handSave7b) pcore.ZoneManager.GetZoneContainer(n1).Move(h, Zone.Deck, Zone.Hand); // 还原

            // ---- ⑧ 先扣卡费·不预计将生成的黑白（2026-10-04 本轮定案）：窗口内池恶化 → 支付失败入墓、代价不执行 ----
            var orderCardData = new CardData { ID = "V9N_ORDER", CardName = "V9n顺序代价卡", Supertype = Cardtype.Spell };
            orderCardData.Cost = ElementCost.FromValue(ManaType.Black, 3);
            orderCardData.PayloadCost = new CostEntry { CostType = (int)CostType.Payload, Value = 1, payload = smallPayload };
            var orderCard = new CardWrapper(orderCardData);
            orderCard.SetController(n1);
            pcore.ZoneManager.GetZoneContainer(n1).Add(orderCard, Zone.Hand);
            npool.AvailableMana[ManaType.Black] = 3; // 恰好够黑3（未来将产的黑不计入——声明期只看当前池）
            int n1MaxBefore3 = n1.MaxHealth;
            VerifySuite.Assert(GameActions.PlayCard(pcore, n1, orderCard), "前置：黑3 池够——声明成功");
            npool.AvailableMana[ManaType.Black] = 0; // 响应窗口内元素被耗尽
            GameActions.DrainStack(pcore);
            VerifySuite.Assert(pcore.ZoneManager.GetCards(n1, Zone.Graveyard).Contains(orderCard),
                "先扣卡费：池恶化付不出 → 入墓不回卷（旧序「补偿先付垫本卡」退役）");
            VerifySuite.Assert(n1.MaxHealth == n1MaxBefore3 && npool.AvailableMana[ManaType.Black] == 0,
                "支付失败先于代价执行：上限未流失、无黑补偿垫付");

            // ---- ⑨ 声明期不预计未来黑白：池差补偿量 → 拒发（元素不足口径） ----
            var affordCardData = new CardData { ID = "V9N_AFFORD", CardName = "V9n余量代价卡", Supertype = Cardtype.Spell };
            affordCardData.Cost = ElementCost.FromValue(ManaType.Black, 3);
            affordCardData.PayloadCost = new CostEntry { CostType = (int)CostType.Payload, Value = 1, payload = smallPayload };
            var affordCard = new CardWrapper(affordCardData);
            affordCard.SetController(n1);
            pcore.ZoneManager.GetZoneContainer(n1).Add(affordCard, Zone.Hand);
            npool.AvailableMana[ManaType.Black] = 3 - smallGrant; // 差值恰好=将生成的黑
            VerifySuite.Assert(!GameActions.PlayCard(pcore, n1, affordCard, null, Zone.Hand, 0, out var affordReject)
                              && affordReject != null && affordReject.Contains("元素不足")
                              && pcore.ZoneManager.GetCards(n1, Zone.Hand).Contains(affordCard),
                $"声明期口径：将生成的黑（{smallGrant}）不提前计入——池不足拒发留手（实际「{affordReject}」）");

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
                opool.AvailableMana[t] = 99;
                opool2.AvailableMana[t] = 99;
            }
            opool.GlobalTurnIndex = 9;
            opool2.GlobalTurnIndex = 9;

            var naRow = AtomicEffectTable.GetByEnumName("NegateActivation"); // 表行「发动无效」（净零成本档）
            var neRow = AtomicEffectTable.GetByEnumName("NegateEffect");     // 表行「效果无效」（扣费照付档）
            VerifySuite.Assert(naRow != null && neRow != null && naRow.HashId != neRow.HashId,
                "两层无效表行齐备（发动无效/效果无效——行 ID 各异）");

            Card OSpell(string id, string name, int baseSpeed, CardCore.Attribute.AtomicEffectConfig atomRow, Player owner)
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
                        new AtomicEffectEntry { refId = atomRow.HashId, value = 1, kinds = new List<int> { 15, 16 } },
                    },
                });
                var card = new CardWrapper(data);
                card.SetController(owner);
                qcore.ZoneManager.GetZoneContainer(owner).Add(card, Zone.Hand);
                return card;
            }

            // ---- ① 施放×发动无效：扣费返还（不付费）、送墓、无效果 ----
            var fbA = OSpell("V9O_FB_A", "V9o火球A", 0, AtomicEffectTable.GetByType(AtomicEffectType.DealDamage), o1);
            var naCard = OSpell("V9O_NA", "V9o发动无效", 2, naRow, o2);
            int o2LifeA = o2.Life, o1GrayA = opool.AvailableMana[ManaType.Gray];
            VerifySuite.Assert(GameActions.PlayCard(qcore, o1, fbA, new List<Entity> { o2 }), "① 火球A打出（响应窗口开）");
            VerifySuite.Assert(GameActions.PlayCardInResponse(qcore, o2, naCard, new List<Entity> { fbA }),
                "① 响应窗口内打出发动无效（指向发动区火球）");
            GameActions.DrainStack(qcore);
            if (!qcore.StackEngine.IsEmpty) qcore.StackEngine.Clear(); // 段内 SBA 窗口收口（Unity 侧同款残留惯例）
            VerifySuite.Assert(o2.Life == o2LifeA
                              && opool.AvailableMana[ManaType.Gray] == o1GrayA
                              && qcore.ZoneManager.GetCards(o1, Zone.Graveyard).Contains(fbA),
                $"① 发动无效=净零成本：无伤害、不付费（净效果=扣费返还）、卡入墓（life {o2LifeA}→{o2.Life}，灰 {o1GrayA}→{opool.AvailableMana[ManaType.Gray]}）");

            // ---- ② 施放×效果无效：扣费不返还、送墓、无效果 ----
            var fbB = OSpell("V9O_FB_B", "V9o火球B", 0, AtomicEffectTable.GetByType(AtomicEffectType.DealDamage), o1);
            var neCard = OSpell("V9O_NE", "V9o效果无效", 2, neRow, o2);
            int o1GrayB = opool.AvailableMana[ManaType.Gray];
            VerifySuite.Assert(GameActions.PlayCard(qcore, o1, fbB, new List<Entity> { o2 }), "② 火球B打出");
            VerifySuite.Assert(GameActions.PlayCardInResponse(qcore, o2, neCard, new List<Entity> { fbB }),
                "② 响应窗口内打出效果无效");
            GameActions.DrainStack(qcore);
            if (!qcore.StackEngine.IsEmpty) qcore.StackEngine.Clear();
            VerifySuite.Assert(o2.Life == o2LifeA
                              && opool.AvailableMana[ManaType.Gray] == o1GrayB - 1
                              && qcore.ZoneManager.GetCards(o1, Zone.Graveyard).Contains(fbB),
                $"② 效果无效=扣费照付：无伤害、费用实扣不返还（灰 {o1GrayB}→{opool.AvailableMana[ManaType.Gray]}）、卡入墓");

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

            // ============================ V9.p 改版回归（2026-10-05：改写光环/成长翻倍/赋予主干/诅咒有限分支） ============================

            VerifySuite.Section("V9.p 四负面光环（毒/霜/眠/疫）+舍身仪典+成长翻倍+赋予主干+诅咒 CurseOnDraw");
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
                wcore.ElementPool.GetPool(w1).AvailableMana[t] = 99;
            wcore.ElementPool.GetPool(w1).GlobalTurnIndex = 9;

            // ---- ① 目录退役锚：改写门四条已删、诅咒门唯一、预算=2 ----
            VerifySuite.Assert(ComposerCatalog.OutcomeConditions.Concat(ComposerCatalog.SituationGates)
                              .All(g => !g.Id.StartsWith("DmgRewrite")),
                "改写门四条已从条件目录退役（迁唯一光环）");
            VerifySuite.Assert(!BranchConditionEvaluator.IsKnownCondition("DmgRewriteToxin")
                              && BranchConditionEvaluator.IsKnownCondition(ComposerCatalog.CurseGateId),
                "改写条件 id 已注销、诅咒门已登记");
            VerifySuite.Assert(ComposerCatalog.CurseGateId == "CurseOnDraw"
                              && !ComposerCatalog.IsSituationCondition(ComposerCatalog.CurseGateId)
                              && !ComposerCatalog.IsOutcomeCondition(ComposerCatalog.CurseGateId)
                              && AtomicEffectTable.GetByType(AtomicEffectType.AddCurse)?.GetTagList()
                                     .Contains(ComposerCatalog.CurseProducerTag) == true,
                "诅咒门=有限分支 Gate 特例（不在通用局面/产出目录——仅 AddCurse 主干可挂，Tags=诅咒产出族 迁表中文化口径）");
            VerifySuite.Assert(CostDerivationService.GatePremium.TryGetValue(ComposerCatalog.CurseGateId, out var curseBudget)
                              && curseBudget == 2,
                $"诅咒门预算=2（奖励=抽到时的专属载荷；实际 {curseBudget}）");

            // ---- ② 四负面光环 + 舍身仪典（2026-10-07 负面化定案：原"战斗伤害改写为指示物"退役，
            // 改持续型挂层——代码侧事件钩子；改写管线收敛为舍身单映射：战斗伤害转投对手角色）----
            var rewriteRows = AtomicEffectTable.GetAll()
                .Where(r => r != null && r.DisplayName is "毒蚀仪典" or "霜蚀仪典" or "眠蚀仪典" or "疫蚀仪典")
                .ToList();
            VerifySuite.Assert(rewriteRows.Count == 4
                              && rewriteRows.All(r => MountKindExtensions.ParseCsv(r.MountKinds).Contains(MountKind.RuleAura))
                              && rewriteRows.All(r => r.Polarity < 0),
                "四负面光环挂规则光环位（2026-10-07 负面化定案——极性负、范围缺省对方）");

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

            // 疫蚀：对方生物对角色造成伤害 → 施伤者自身叠剧毒；己方生物对角色伤害不叠
            RuleAuraSystem.Activate(RuleAuraComponents.CombatVenom, NegCarrier("V9P_VENOM"), w1, (int)RuleAuraScope.Opponent);
            int w1Life0 = w1.Life;
            KeywordRules.ApplyDamage(foeCr, w1, 2, isCombat: false);
            VerifySuite.Assert(w1.Life == w1Life0 - 2
                              && foeCr.GetCounterCount(CounterRules.PoisonCounter) == 1,
                $"疫蚀：对方生物对角色伤害→施伤者叠剧毒×1（层 {foeCr.GetCounterCount(CounterRules.PoisonCounter)}）");
            KeywordRules.ApplyDamage(mineCr, w1, 1, isCombat: false);
            VerifySuite.Assert(mineCr.GetCounterCount(CounterRules.PoisonCounter) == 0,
                "疫蚀范围过滤：己方生物对角色伤害不叠");

            // 霜蚀：攻击结算事件 → 对方生物冻结（横置+层）；己方生物攻击后不冻结
            RuleAuraSystem.Activate(RuleAuraComponents.CombatFreeze, NegCarrier("V9P_FREEZE"), w1, (int)RuleAuraScope.Opponent);
            EventManager.Instance.Publish(new AttackResolvedEvent { Attacker = foeCr, Target = mineCr, AttackingPlayer = w2 });
            VerifySuite.Assert(foeCr.IsTapped() && foeCr.GetCounterCount(KeywordRules.FreezeCounter) >= 1,
                "霜蚀：对方生物攻击后冻结（横置+冻结层）");
            bool mineTapped0 = mineCr.IsTapped();
            EventManager.Instance.Publish(new AttackResolvedEvent { Attacker = mineCr, Target = foeCr, AttackingPlayer = w1 });
            VerifySuite.Assert(mineCr.IsTapped() == mineTapped0 && mineCr.GetCounterCount(KeywordRules.FreezeCounter) == 0,
                "霜蚀范围过滤：己方生物攻击后不冻结");

            // 眠蚀：启动式发动钩（EffectExecutionEngine 结算点同口）→ 对方生物叠沉睡；己方不叠
            RuleAuraSystem.Activate(RuleAuraComponents.CombatSleep, NegCarrier("V9P_SLEEP"), w1, (int)RuleAuraScope.Opponent);
            RuleAuraComponents.OnActivatedForSleepAura(foeCr);
            RuleAuraComponents.OnActivatedForSleepAura(mineCr);
            VerifySuite.Assert(foeCr.GetCounterCount(KeywordRules.SleepCounter) == 1
                              && mineCr.GetCounterCount(KeywordRules.SleepCounter) == 0,
                "眠蚀：启动式发动后叠沉睡×1（范围=对方过滤——沉睡层拦截后续启动式+跳重置）");

            // 舍身（范围=己方）：己方生物战斗伤害转投施伤方对手角色（原目标免伤）；对方生物不转投
            var redirectCarrierOwn = NegCarrier("V9P_REDIRECT");
            RuleAuraSystem.Activate(RuleAuraComponents.CombatRedirect, redirectCarrierOwn, w1, (int)RuleAuraScope.Own);
            int foeLifeR0 = foeCr.GetLife(); int w2LifeR0 = w2.Life;
            int dealtR = KeywordRules.ApplyDamage(mineCr, foeCr, 3, isCombat: true);
            VerifySuite.Assert(dealtR == 0 && foeCr.GetLife() == foeLifeR0 && w2.Life == w2LifeR0 - 3,
                $"舍身：己方生物战斗伤害转投对手角色（返 {dealtR}，目标余 {foeCr.GetLife()}，w2 {w2LifeR0}→{w2.Life}）");
            int mineLifeR0 = mineCr.GetLife();
            int dealtR2 = KeywordRules.ApplyDamage(foeCr, mineCr, 2, isCombat: true);
            VerifySuite.Assert(dealtR2 == 2 && mineCr.GetLife() == mineLifeR0 - 2,
                $"舍身范围过滤：对方生物战斗伤害照常不转投（实际 {dealtR2}，余 {mineCr.GetLife()}）");

            // 舍身对向（2026-10-07 用户定案：转投目标=施伤生物控制者的对手——作用于对方时对手生物打我方主公）：
            // 载体直接移墓（耐久归零的 SBA 送墓在直调路径不泵——活性=在场实时查，移墓即失效）→ 换范围=对方重激活
            wcore.ZoneManager.MoveCard(redirectCarrierOwn, w1, Zone.Battlefield, Zone.Graveyard);
            VerifySuite.Assert(redirectCarrierOwn.GetZone() == Zone.Graveyard
                              && !RuleAuraSystem.IsActive(RuleAuraComponents.CombatRedirect), "舍身载体移墓→规则失效（换范围重激活前置）");
            RuleAuraSystem.Activate(RuleAuraComponents.CombatRedirect, NegCarrier("V9P_REDIRECT2"), w1, (int)RuleAuraScope.Opponent);
            VerifySuite.Assert(RuleAuraSystem.IsActive(RuleAuraComponents.CombatRedirect)
                              && RuleAuraSystem.HolderRewriteFor(foeCr) == RuleAuraComponents.CombatRedirect,
                $"舍身重激活探针：Active={RuleAuraSystem.IsActive(RuleAuraComponents.CombatRedirect)}"
                + $" Rewrite={RuleAuraSystem.HolderRewriteFor(foeCr)}"
                + $" foeCtrlIsW2={ReferenceEquals(foeCr.GetController(), w2)}"
                + $" w1OppIsW2={ReferenceEquals(w1.Opponent, w2)}");
            int w1LifeR0 = w1.Life; int mineLifeR1 = mineCr.GetLife();
            int dealtR3 = KeywordRules.ApplyDamage(foeCr, mineCr, 2, isCombat: true);
            VerifySuite.Assert(dealtR3 == 0 && mineCr.GetLife() == mineLifeR1 && w1.Life == w1LifeR0 - 2,
                $"舍身对向：范围=对方时对手生物战斗伤害转投我方角色（返 {dealtR3}，目标余 {mineCr.GetLife()}，w1 {w1LifeR0}→{w1.Life}）");

            // ---- ③ 成长改版：持有者回合结束属性指示物翻倍；回合开始 +1/+1 退役 ----
            mineCr.AddKeyword(KeywordRules.Growth, KeywordLane.Printed, mineCr);
            CounterRules.AddStatCounter(mineCr, CounterRules.PowerUpCounter, 2, mineCr);
            CounterRules.AddStatCounter(mineCr, CounterRules.PlusOneCounter, 1, mineCr);
            int gw0 = mineCr.GetPower(), gl0 = mineCr.GetLife();
            GameActions.EndTurn(wcore, w1);          // w1 回合结束：限时层消退 → 成长翻倍
            wcore.TurnEngine.CheckPhaseTransition(); // → w2 回合
            VerifySuite.Assert(mineCr.GetCounterCount(CounterRules.PowerUpCounter) == 4
                              && mineCr.GetCounterCount(CounterRules.PlusOneCounter) == 2,
                $"成长翻倍：PowerUp 2→4、+1/+1 1→2（实际 {mineCr.GetCounterCount(CounterRules.PowerUpCounter)}/{mineCr.GetCounterCount(CounterRules.PlusOneCounter)}）");
            VerifySuite.Assert(mineCr.GetPower() == gw0 + 3 && mineCr.GetLife() == gl0 + 1,
                $"翻倍回写攻血（攻 {gw0}→{mineCr.GetPower()}：+2攻层翻倍+1/+1层翻倍；生 {gl0}→{mineCr.GetLife()}）");
            GameActions.EndTurn(wcore, w2);          // w2 回合结束：w1 生物不动
            wcore.TurnEngine.CheckPhaseTransition(); // → w1 回合（开始不 +1/+1——旧口径退役锚）
            VerifySuite.Assert(mineCr.GetCounterCount(CounterRules.PlusOneCounter) == 2
                              && mineCr.GetCounterCount(CounterRules.PowerUpCounter) == 4,
                "持有者回合结束才翻倍：他人回合末不动、己方回合开始不再 +1/+1");

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
                    AtomRefs.New(AtomicEffectType.GrantStealth, value: 1, kinds: new List<int> { 1, 2 }, str: "Stealth"),
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

            // ---- ④' 三轨统一定案（2026-10-05 指示物并梯）：属性指示物单价=锚×CounterSpec 持久档，
            //      不读效果持续档（运行时 handler 不传持续时间——声明 Once/UET 低价买换区清层的漏洞堵死）。
            VerifySuite.Assert(Math.Abs(CostDerivationService.StatCounterTierPrice(AtomicEffectType.AddPowerUp) - 1.5f) < 0.001f
                              && Math.Abs(CostDerivationService.StatCounterTierPrice(AtomicEffectType.AddPlusOne) - 2.0f) < 0.001f
                              && Math.Abs(CostDerivationService.StatCounterTierPrice(AtomicEffectType.AddCostUp) - 1.5f) < 0.001f,
                "属性指示物并梯：换区清层 1.5/点（攻/血/费同 Modify 换区档）、±1/±1 永久点包 2.0/层");
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

            // ---- ⑤ 诅咒有限分支端到端：CurseOnDraw 门 Then=槽级 branch 载荷（AddCurseHandler 消费→抽到发作） ----
            var curseGateData = new CardData
            {
                ID = "V9P_CURSE_CARD", CardName = "V9P诅咒术", Supertype = Cardtype.Spell,
            };
            curseGateData.Effects.Add(new CardEffectData
            {
                Id = "V9P_CURSE_EFF",
                TriggerTiming = (int)TriggerTiming.OnPlay,
                SelectionMode = -1, // None 自结算=随机对方牌库一张/次
                AtomicEffects = new List<AtomicEffectEntry>
                {
                    // 两槽定案构造形态：settle=1 有限分支（Gate 特例）直接挂主干原子 branch 载荷
                    new AtomicEffectEntry
                    {
                        refId = AtomicEffectTable.GetByType(AtomicEffectType.AddCurse)?.HashId,
                        value = 1, kinds = new List<int> { 8 },
                        branch = new BranchEntryData
                        {
                            settle = (int)BranchSettleKind.Gate,
                            gateId = ComposerCatalog.CurseGateId,
                            then = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DrawCard, value: 1) },
                        },
                    },
                },
            });
            var curseGateDef = CardEffectConverter.ConvertOne(curseGateData.Effects[0], "V9P_src");
            var trunkAtom = curseGateDef.Effects.FirstOrDefault(a => a.Type == AtomicEffectType.AddCurse);
            VerifySuite.Assert(trunkAtom != null
                              && trunkAtom.Branch != null
                              && trunkAtom.Branch.Settle == BranchSettleKind.Gate
                              && trunkAtom.Branch.GateId == ComposerCatalog.CurseGateId
                              && trunkAtom.Branch.Then.Count == 1
                              && trunkAtom.Branch.Then[0].Type == AtomicEffectType.DrawCard,
                "诅咒门配对：branch 载荷挂主干原子（Gate·CurseOnDraw·Then=抽1——AddCurseHandler 消费，≤2 费由合成器预算校验）");

            var curseHand = new CardWrapper(curseGateData);
            curseHand.SetController(w1);
            wcore.ZoneManager.GetZoneContainer(w1).Add(curseHand, Zone.Hand);
            VerifySuite.Assert(GameActions.PlayCard(wcore, w1, curseHand, null, Zone.Hand, 0, out var rejectP1),
                $"诅咒术打出（拒绝原因：{rejectP1 ?? "无"}）");
            GameActions.DrainStack(wcore);
            var cursedP = wcore.ZoneManager.GetCards(w2, Zone.Deck)
                .FirstOrDefault(c => c.GetCounterCount(CounterRules.CurseCounter) > 0);
            VerifySuite.Assert(cursedP != null && cursedP.GetCounterCount(CounterRules.CurseCounter) == 1,
                "诅咒门版打出：随机对方牌库一张挂 Curse=1");
            VerifySuite.Assert(CurseSystem.GetCurses(cursedP).Count == 1,
                "inline 载荷登记（无 Effects.json 外挂条目）");
            int p1HandBefore = wcore.ZoneManager.GetCards(w1, Zone.Hand).Count;
            for (int i = 0; i < 64 && cursedP.GetZone() == Zone.Deck; i++)
            {
                if (wcore.ZoneManager.DrawCard(w2) == null) break;
            }
            VerifySuite.Assert(cursedP.GetZone() == Zone.Hand
                              && cursedP.GetCounterCount(CounterRules.CurseCounter) == 0
                              && CurseSystem.GetCurses(cursedP).Count == 0,
                "抽到发作+一次性消耗（inline 载荷同口）");
            VerifySuite.Assert(wcore.ZoneManager.GetCards(w1, Zone.Hand).Count - p1HandBefore == 1,
                $"inline 载荷落地：施诅方抽 1（{p1HandBefore}→{wcore.ZoneManager.GetCards(w1, Zone.Hand).Count}——Controller=施诅方）");

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
                VerifySuite.Assert(sinkErrors.Any(e => e.Contains("V9P_CHAIN") && e.Contains("目标域交集为空")),
                    "主序列原子域交集空 → Error（断链构筑期拦截——Then 奖励/光环条目不计入主干）");
            }
            finally
            {
                TideLog.Sink = prevSink;
            }

            // ============================ V9.q 门槛对赌 + 引擎主干回表（2026-10-05 晚间定案） ============================

            VerifySuite.Section("V9.q 门槛对赌（局面门逆转惩罚）+引擎主干回表");
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
                bcore.ElementPool.GetPool(b1x).AvailableMana[t] = 99;

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
                { AtomicEffectType.EngineNthHandCard, BranchEngineKind.NthHandCard },
            };
            foreach (var kv in bEnginePairs)
            {
                var row = AtomicEffectTable.GetByType(kv.Key);
                VerifySuite.Assert(row != null && ComposerCatalog.IsEngineTrunkRow(row)
                                   && ComposerCatalog.EngineKindOf(kv.Key) == kv.Value,
                    $"引擎主干行回表：{kv.Key}（位 5 引擎主干、映射 {kv.Value}——2026-10-07 位 0 删除后主干资格派生，引擎行互斥由 CanBeRewardRow 排除）");
            }

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

            // ---- ③ 局面门对赌端到端：首卡达成得奖励 / 次卡未达成奖励逆转强制执行 ----
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

            CardData BetCard(string id) => new CardData
            {
                ID = id, CardName = "V9Q对赌术", Supertype = Cardtype.Spell,
            };
            void AddBetEffect(CardData c)
            {
                c.Effects.Add(new CardEffectData
                {
                    Id = c.ID + "_EFF",
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
                                gateId = "FirstCardThisTurn", // 本回合首卡=达成；次卡=未达成
                                then = new List<AtomicEffectEntry>
                                {
                                    // 奖励=治疗 2（实例收窄己方生物 {1}——奖励路径单候选确定；
                                    // 逆转按行域 [1,2] 收窄到对方侧 [2] → 强制治疗对方生物）
                                    AtomRefs.New(AtomicEffectType.Heal, value: 2, kinds: new List<int> { 1 }),
                                },
                            },
                        },
                    },
                });
            }
            var betDef = CardEffectConverter.ConvertOne(
                new CardEffectData
                {
                    Id = "V9Q_BET_PRICE",
                    TriggerTiming = (int)TriggerTiming.OnPlay,
                    SelectionMode = -1,
                    AtomicEffects = new List<AtomicEffectEntry>
                    {
                        AtomRefs.New(AtomicEffectType.DealDamage, value: 1, kinds: new List<int> { 2 }),
                    },
                }, "V9Q_src");
            var betCardPrice = CostDerivationService.DeriveElementCosts(betDef).Total;
            var bet1 = BetCard("V9Q_BET1");
            AddBetEffect(bet1);
            var betDef1 = CardEffectConverter.ConvertOne(bet1.Effects[0], "V9Q_src");
            VerifySuite.Assert(betDef1.Effects[0].Branch != null
                               && betDef1.Effects[0].Branch.Settle == BranchSettleKind.Gate
                               && Math.Abs(CostDerivationService.DeriveElementCosts(betDef1).Total - betCardPrice) < 1e-4f,
                $"对赌零加价：Then 零计价维持（带门 {CostDerivationService.DeriveElementCosts(betDef1).Total:0.##} = 纯主干 {betCardPrice:0.##}）");

            var betHand1 = new CardWrapper(bet1);
            betHand1.SetController(b1x);
            bcore.ZoneManager.GetZoneContainer(b1x).Add(betHand1, Zone.Hand);
            VerifySuite.Assert(GameActions.PlayCard(bcore, b1x, betHand1, null, Zone.Hand, 0, out var rejB1),
                $"对赌卡1（达成面）打出（拒绝原因：{rejB1 ?? "无"}）");
            GameActions.DrainStack(bcore);
            VerifySuite.Assert(bMine.GetLife() == 7 && bFoe.GetLife() == 4,
                $"门达成→奖励照旧：主干打对方 5→4、Then 治疗（候选[己方生物,己方角色] 无头自动选首=己方生物；2026-10-07 晚起多候选=弹选/AI 与无头选首）5→7（实际 己{bMine.GetLife()}/敌{bFoe.GetLife()}）");

            var bet2 = BetCard("V9Q_BET2");
            AddBetEffect(bet2);
            var betHand2 = new CardWrapper(bet2);
            betHand2.SetController(b1x);
            bcore.ZoneManager.GetZoneContainer(b1x).Add(betHand2, Zone.Hand);
            VerifySuite.Assert(GameActions.PlayCard(bcore, b1x, betHand2, null, Zone.Hand, 0, out var rejB2),
                $"对赌卡2（未达成面）打出（拒绝原因：{rejB2 ?? "无"}）");
            GameActions.DrainStack(bcore);
            VerifySuite.Assert(bMine.GetLife() == 7 && bFoe.GetLife() == 5,
                $"门未达成→逆转惩罚：主干打对方 4→3、奖励逆转（Heal 收窄对方侧 [2]）对手弹选落点（无头自动选首=对方生物）治疗 3→5，己方不动（实际 己{bMine.GetLife()}/敌{bFoe.GetLife()}）");

            // ---- ④ 产出条件纯奖励：未达成无动作（不对赌） ----
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
            VerifySuite.Assert(bMine.GetLife() == 7 && bFoe.GetLife() == 4,
                $"产出条件未达成→无奖励也无惩罚（纯奖励语义；主干 5→4，双侧不动——实际 己{bMine.GetLife()}/敌{bFoe.GetLife()}）");

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

            // ---- ⑥ 局面门奖励不可逆转 → converter 剔除（对赌惩罚落点校验，装载可见不炸） ----
            var bSink2 = new List<string>();
            TideLog.Sink = (level, text) => { bSink2.Add(text); };
            try
            {
                var badBet = BetCard("V9Q_BADBET");
                badBet.Effects.Add(new CardEffectData
                {
                    Id = "V9Q_BADBET_EFF",
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
                                settle = (int)BranchSettleKind.Gate,
                                gateId = "LifeBelowOpp",
                                then = new List<AtomicEffectEntry>
                                {
                                    // Guard：p=0 且域空——不可逆转（惩罚无从执行）
                                    new AtomicEffectEntry
                                    {
                                        refId = AtomicEffectTable.GetByType(AtomicEffectType.Guard)?.HashId,
                                        value = 1,
                                    },
                                },
                            },
                        },
                    },
                });
                var badDef = CardEffectConverter.ConvertOne(badBet.Effects[0], "V9Q_src");
                VerifySuite.Assert(badDef.Effects[0].Branch == null
                                   && bSink2.Any(t => t.Contains("不可逆转")),
                    "局面门奖励不可逆转 → Warn 剔除（Then 空→门载荷整体折叠为无分支）");
            }
            finally { TideLog.Sink = bPrevSink; }

            // ============================ V9.r 原子表工坊 + 挑战模式（2026-10-06） ============================
            VerifySuite.Section("V9.r 原子表工坊（改价边界/占比分摊/overlay 幂等/强制重推）+挑战模式（曲线/起手）");

            // ---- ① 改价边界：0 与 9 永远设不进；0.5 网格；未知行/不计价行拒收 ----
            var rDmg = AtomicEffectTable.GetByType(AtomicEffectType.DealDamage);
            VerifySuite.Assert(rDmg != null && !string.IsNullOrEmpty(rDmg.HashId) && rDmg.ManaList != null,
                "DealDamage 行在表（带 hashId、计价行）");
            var rHash = rDmg.HashId;
            var rBaseTotal = rDmg.ManaList.Total;
            var rBaseShares = new Dictionary<ManaType, float>();
            foreach (ManaType m in Enum.GetValues(typeof(ManaType)))
                if (rDmg.ManaList[m] > 0f) rBaseShares[m] = rDmg.ManaList[m];

            VerifySuite.Assert(!AtomicEffectTable.IsEditableTotal(0f) && !AtomicEffectTable.IsEditableTotal(9f)
                               && !AtomicEffectTable.IsEditableTotal(0.4f) && !AtomicEffectTable.IsEditableTotal(9.5f)
                               && !AtomicEffectTable.IsEditableTotal(8.6f),
                "可编辑性边界：0/9/越界/非 0.5 网格值全部拒收");
            VerifySuite.Assert(AtomicEffectTable.IsEditableTotal(0.5f) && AtomicEffectTable.IsEditableTotal(4f)
                               && AtomicEffectTable.IsEditableTotal(8.5f),
                "可编辑性边界：0.5/4/8.5 放行");
            VerifySuite.Assert(!AtomicEffectTable.TrySetRowTotal(rHash, 0f)
                               && !AtomicEffectTable.TrySetRowTotal(rHash, 9f)
                               && !AtomicEffectTable.TrySetRowTotal(rHash, 8.75f)
                               && !AtomicEffectTable.TrySetRowTotal("deadbeef", 1f),
                "行改写拒收：0/9/非网格/未知 hashId（表内容不动）");
            VerifySuite.Assert(AtomicEffectTable.OrderedRows.Count >= AtomicEffectTable.Count
                               && AtomicEffectTable.OrderedRows.Select(r => r.Id).Distinct().Count()
                                   == AtomicEffectTable.OrderedRows.Count,
                "OrderedRows 全行保序无重（行数 ≥ 去重类型数）");
            var rNullRow = AtomicEffectTable.OrderedRows.FirstOrDefault(r => r != null && !string.IsNullOrEmpty(r.HashId)
                && r.ManaList == null);
            if (rNullRow != null)
                VerifySuite.Assert(!AtomicEffectTable.TrySetRowTotal(rNullRow.HashId, 1f), "不计价行不可编辑");

            // ---- ② 等比分摊：占比不动、总额精确；Version 跳变 ----
            var rVersion0 = AtomicEffectTable.Version;
            VerifySuite.Assert(AtomicEffectTable.TrySetRowTotal(rHash, 2.5f), "改价 2.5 写入成功");
            VerifySuite.Assert(AtomicEffectTable.Version == rVersion0 + 1, "成功改价 Version+1（缓存失效判据）");
            var rNew = AtomicEffectTable.GetByHashId(rHash);
            VerifySuite.Assert(Math.Abs(rNew.ManaList.Total - 2.5f) < 1e-4f, "改价后总额=2.5（精确）");
            var rShareOk = true;
            foreach (var kv in rBaseShares)
                if (Math.Abs(rNew.ManaList[kv.Key] / 2.5f - kv.Value / rBaseTotal) > 1e-3f) rShareOk = false;
            foreach (ManaType m in Enum.GetValues(typeof(ManaType)))
                if (!rBaseShares.ContainsKey(m) && rNew.ManaList[m] > 1e-6f) rShareOk = false;
            VerifySuite.Assert(rShareOk, "六色占比不变（按原份额分摊，零色仍为零）");
            VerifySuite.Assert(Math.Abs(rNew.Polarity - rDmg.Polarity) < 1e-6f && rNew.HashId == rHash,
                "行身份字段不动（Polarity/HashId——转换器快照不过期）");

            // ---- ③ overlay 幂等：落表→同代际不重放→Reload 回基线→重放恢复 ----
            var rOverlayPath = Path.Combine(Path.GetTempPath(), $"V9R_overlay_{Guid.NewGuid():N}.json");
            try
            {
                var rOverlay = AtomicTableOverlay.CreateForVerification(rOverlayPath);
                VerifySuite.Assert(!rOverlay.Enabled && rOverlay.Rows.Count == 0, "缺档=空覆盖层（基线）");
                rOverlay.SetEnabled(true);
                rOverlay.SetRow(rHash, 2.5f);
                rOverlay.ApplyToTable();
                VerifySuite.Assert(Math.Abs(AtomicEffectTable.GetByHashId(rHash).ManaList.Total - 2.5f) < 1e-4f,
                    "overlay 落表（独立存档不碰真实档）");
                var rVersionApplied = AtomicEffectTable.Version;
                rOverlay.EnsureApplied();
                VerifySuite.Assert(AtomicEffectTable.Version == rVersionApplied, "同代际 EnsureApplied 幂等（不重放）");
                AtomicEffectTable.Reload();
                VerifySuite.Assert(Math.Abs(AtomicEffectTable.GetByHashId(rHash).ManaList.Total - rBaseTotal) < 1e-4f,
                    "Reload 回基线（合成器切表口径）");
                rOverlay.EnsureApplied();
                VerifySuite.Assert(Math.Abs(AtomicEffectTable.GetByHashId(rHash).ManaList.Total - 2.5f) < 1e-4f,
                    "Reload 后 EnsureApplied 重放恢复改价（版本跳变驱动）");
            }
            finally
            {
                try { File.Delete(rOverlayPath); } catch { /* 临时档清理失败不判 */ }
            }

            // ---- ④ 强制重推：改价→卡费覆写；幂等；往返一致（注意：进入本节时表=2.5——③ overlay 已重放） ----
            var rCard = new CardData { ID = "V9R_RC", CardName = "V9R重推卡", Supertype = Cardtype.Spell };
            rCard.Effects.Add(new CardEffectData
            {
                Id = "V9R_RC_EFF",
                TriggerTiming = (int)TriggerTiming.OnPlay,
                SelectionMode = -1,
                AtomicEffects = new List<AtomicEffectEntry>
                {
                    new AtomicEffectEntry { refId = rHash, value = 3, kinds = new List<int> { 2 } },
                },
            });
            CardCostService.EnsureCost(rCard); // 无声明费 → 建议费（当前表=2.5 价口径）
            var rCostAtHigh = rCard.Cost != null ? rCard.Cost.Total : 0f;
            VerifySuite.Assert(rCostAtHigh > 0f, $"高价表（2.5）建议费非零（{rCostAtHigh:0.##}）");
            AtomicEffectTable.Reload(); // 回基线（1.0 价）
            var rLines = CardCostService.ReforceSuggestedCosts(new[] { rCard });
            VerifySuite.Assert(rLines.Count == 1 && rLines[0].CardId == "V9R_RC" && rCard.Cost.Total < rCostAtHigh,
                $"回基线重推费↓且覆写声明优先（{rCostAtHigh:0.##} → {rCard.Cost.Total:0.##}）");
            var rCostAtBase = rCard.Cost.Total;
            var rLines2 = CardCostService.ReforceSuggestedCosts(new[] { rCard });
            VerifySuite.Assert(rLines2.Count == 0 && Math.Abs(rCard.Cost.Total - rCostAtBase) < 1e-4f,
                "重推幂等（同表二推零变化、费用不动）");
            VerifySuite.Assert(AtomicEffectTable.TrySetRowTotal(rHash, 2.5f), "复设 2.5（往返验证准备）");
            var rLines3 = CardCostService.ReforceSuggestedCosts(new[] { rCard });
            VerifySuite.Assert(rLines3.Count == 1 && Math.Abs(rCard.Cost.Total - rCostAtHigh) < 1e-4f,
                $"改价往返一致：2.5 重推回到高价口径（{rCard.Cost.Total:0.##} = {rCostAtHigh:0.##}）");
            AtomicEffectTable.Reload(); // 收尾回基线
            CardIdentityService.InvalidateTableCache();

            // ---- ⑤ 挑战曲线纯函数：CapAt(t)=min(t+N, 9+N)；bonus=0 与标准曲线等价 ----
            var rCurve = ResourceCurve.ChallengeLandCurve(3);
            var rCurveOk = true;
            for (int t = 1; t <= 30; t++)
                if (rCurve.CapAt(t) != Math.Min(t + 3, 12)) rCurveOk = false;
            VerifySuite.Assert(rCurveOk, "挑战曲线(3)：CapAt(t)=min(t+3, 12)——开局 4、封顶 12");
            var rCurve0 = ResourceCurve.ChallengeLandCurve(0);
            VerifySuite.Assert(rCurve0.CapAt(1) == 1 && rCurve0.CapAt(9) == 9 && rCurve0.CapAt(99) == 9
                               && ResourceCurve.ChallengeLandCurve(9).CapAt(1) == 10
                               && ResourceCurve.ChallengeLandCurve(9).CapAt(50) == 18,
                "挑战曲线边界：bonus=0 等价标准曲线；bonus=9 开局 10、封顶 18");

            // ---- ⑥ 挑战局端到端：InitGame 加成（P2 曲线上移 + 起手加抽，P1 不动） ----
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
            VerifySuite.Assert(AtomicEffectTable.GetByHashId(rHash).ManaList.Total - rBaseTotal < 1e-4f
                               && AtomicEffectTable.GetByHashId(rHash).ManaList.Total - rBaseTotal > -1e-4f,
                "对局装载不依赖改价（本节收尾表=基线）");

            VerifySuite.Section("V9 引擎规则回归完成");
        }
    }
}
