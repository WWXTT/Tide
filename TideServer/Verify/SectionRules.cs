using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.Attribute;

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
    /// Unity 侧对应段落见 Assets/Editor/验证/CardPipelineVerifier.cs（编辑器内跑，口径同源）。
    /// </summary>
    internal static class SectionRules
    {
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
                d.Cost = new Dictionary<int, float> { { (int)ManaType.Gray, tier } };
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
            VerifySuite.Assert(fb.DerivedCost.GetValueOrDefault(ManaType.Red) == 1
                              && fb.DerivedCost.GetValueOrDefault(ManaType.Blue) == 2,
                $"法术 4伤+1抽 = 红1+蓝2（实际 红{fb.DerivedCost.GetValueOrDefault(ManaType.Red)}蓝{fb.DerivedCost.GetValueOrDefault(ManaType.Blue)}）");
            // 减免落色自标（2026-10-02 定案）：RefundColor=Blue → 底盘退2 尽落蓝 → 红3蓝0
            fbCard.RefundColor = (int)ManaType.Blue;
            var fbBlue = CardCostService.Derive(fbCard);
            VerifySuite.Assert(fbBlue.DerivedCost.GetValueOrDefault(ManaType.Red) == 3
                              && fbBlue.DerivedCost.GetValueOrDefault(ManaType.Blue) == 0,
                $"减免落色自标：退2落蓝 → 红3蓝0（实际 红{fbBlue.DerivedCost.GetValueOrDefault(ManaType.Red)}蓝{fbBlue.DerivedCost.GetValueOrDefault(ManaType.Blue)}）");

            // ============================ V9.b 域数据（表级） ============================

            VerifySuite.Section("V9.b 域数据（伤害族开放 3,4；冻结收紧 1,2）");
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
            AssertKinds(AtomicEffectType.Freeze, new[] { 1, 2 }, "冻结（收紧为有生命域——对结界无效）");

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
            VerifySuite.Assert(GameActions.PlayCard(core, p1, ench), "结界从手牌发动（无效果免费卡）");
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
            //（GrantIceCrystal 行已于 2026-10-02 苏醒迁移同日退役——改用存续的 GrantPoisonSting/GrantStealth）
            VerifySuite.Assert(ComposerCatalog.IsKeywordStyleGrant(AtomicEffectType.GrantPoisonSting)
                              && ComposerCatalog.IsKeywordStyleGrant(AtomicEffectType.GrantStealth),
                "关键词型判定：自指域 Grant 行（毒刺/潜行授予）= 关键词型");
            VerifySuite.Assert(!ComposerCatalog.IsKeywordStyleGrant(AtomicEffectType.GrantMagnify)
                              && !ComposerCatalog.IsKeywordStyleGrant(AtomicEffectType.DealDamage),
                "关键词型判定：赋予型（放大，域含他人）与非 Grant 行不受限");

            var kwAct = new CardEffectData
            {
                Id = "V9_kw_act",
                TriggerTiming = (int)TriggerTiming.Activate_Instant,
                AtomicEffects = new List<AtomicEffectEntry>
                {
                    AtomRefs.New(AtomicEffectType.GrantPoisonSting),              // 关键词型 → 应剔除
                    AtomRefs.New(AtomicEffectType.DealDamage, value: 1),
                },
                Steps = new List<EffectStepData>
                {
                    new EffectStepData { kind = 0, atomic = AtomRefs.New(AtomicEffectType.GrantStealth) }, // 步骤内 → 整步剔除
                    new EffectStepData
                    {
                        kind = 1, conditionId = "DmgKillsTarget",
                        thenSteps = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.GrantLifesteal) }, // 分支奖励 → 剔除
                    },
                },
            };
            var kwDef = CardEffectConverter.ConvertOne(kwAct, "V9_src");
            var kwFlat = kwDef.Effects.Select(a => a.Type).ToList();
            VerifySuite.Assert(!kwFlat.Contains(AtomicEffectType.GrantPoisonSting),
                "启动式扁平列表：关键词型 Grant（毒刺）被剔除");
            VerifySuite.Assert(kwFlat.Contains(AtomicEffectType.DealDamage), "启动式扁平列表：非关键词原子保留");
            var stepTypes = (kwDef.Steps ?? new List<RuntimeEffectStep>())
                .Where(s => s.Kind == RuntimeStepKind.Atomic && s.Atomic != null).Select(s => s.Atomic.Type).ToList();
            VerifySuite.Assert(!stepTypes.Contains(AtomicEffectType.GrantStealth),
                "启动式步骤：关键词型原子步骤被整步剔除");
            var branchThen = (kwDef.Steps ?? new List<RuntimeEffectStep>())
                .Where(s => s.Kind == RuntimeStepKind.Branch).SelectMany(s => s.Then).Select(a => a.Type).ToList();
            VerifySuite.Assert(!branchThen.Contains(AtomicEffectType.GrantLifesteal),
                "启动式分支奖励：关键词型（吸血授予）被剔除");

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

            // ============================ V9.f 自我沉睡契约豁免（2026-10-02 苏醒退役配套） ============================

            VerifySuite.Section("V9.f 自我沉睡：契约豁免 + 灰费转时长 + 定长照付");
            // 苏醒行退役后 Sleep 单行极性 -1——自我沉睡（收窄域 {Self}）若不豁免错边契约会整原子被剔
            var ssData = new CardData
            {
                ID = "V9F_SLEEP", CardName = "V9F沉睡者", Supertype = Cardtype.Creature, Power = 4, Life = 4,
                Cost = new Dictionary<int, float> { { (int)ManaType.Gray, 3 }, { (int)ManaType.Green, 1 } },
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
                "契约豁免：自我沉睡原子（Sleep p=-1 锁 {Self}）不再被错边契约剔除");

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

                var sleepCard = ToHandF(f1, ssData);
                int grayBefore = bankF.AvailableMana[ManaType.Gray];
                VerifySuite.Assert(GameActions.PlayCard(fs, f1, sleepCard), "自我沉睡卡打出（灰豁免后仅需绿1）");
                GameActions.DrainStack(fs);
                VerifySuite.Assert(bankF.AvailableMana[ManaType.Gray] == grayBefore, "灰费豁免：3 灰未扣（转沉睡时长）");
                VerifySuite.Assert(sleepCard.GetCounterCount(KeywordRules.SleepCounter) == 3,
                    $"入场沉睡 3 层（=豁免灰量；实际 {sleepCard.GetCounterCount(KeywordRules.SleepCounter)}）");
                VerifySuite.Assert(sleepCard.IsTapped(), "横置进沉睡");

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
                    Cost = new Dictionary<int, float> { { (int)ManaType.Gray, 2 }, { (int)ManaType.Green, 1 } },
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

            VerifySuite.Section("V9 引擎规则回归完成");
        }
    }
}
