using System;
using System.Collections.Generic;
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

            // 载荷 = Effects.json 现成条目（纯原子 steps、恰一个抽卡原子、无分支——发作时施诅方
            // 抽 N 张可观测；条件分支类载荷的评估路径不走此夹具——抽牌时点无上游产出，门恒否）
            var drawRow = AtomicEffectTable.GetByType(AtomicEffectType.DrawCard);
            VerifySuite.Assert(drawRow != null, "抽卡表行存在");
            var payloadEntry = EffectsLibrary.GetAll().FirstOrDefault(d => d != null && d.engine == 0
                && d.steps != null && d.steps.Count > 0
                && d.steps.All(s => s != null && s.kind == 0 && s.atom != null)
                && d.steps.Count(s => s.atom.refId == drawRow.HashId) == 1);
            VerifySuite.Assert(payloadEntry != null, "Effects.json 存在「纯原子 steps+恰一抽卡」条目（诅咒载荷候选）");
            var payloadDrawAtom = payloadEntry.steps.First(s => s.atom.refId == drawRow.HashId).atom;
            var payloadId = payloadEntry.id;
            int expectedDraws = Math.Max(1, payloadDrawAtom.value);

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

            // ---- ① 挂层 + 载荷登记 ----
            var curseCard1 = MakeCurseSpell("V9H_CURSE_1", payloadId);
            VerifySuite.Assert(GameActions.PlayCard(hcore, h1, curseCard1, null, Zone.Hand, 0, out var rejectH1),
                $"诅咒术打出（拒绝原因：{rejectH1 ?? "无"}）");
            GameActions.DrainStack(hcore);
            var cursed = hcore.ZoneManager.GetCards(h2, Zone.Deck)
                .FirstOrDefault(c => c.GetCounterCount(CounterRules.CurseCounter) > 0);
            VerifySuite.Assert(cursed != null && cursed.GetCounterCount(CounterRules.CurseCounter) == 1,
                "随机对方牌库一张挂 Curse=1（None 自结算）");
            var registered = CurseSystem.GetCurses(cursed);
            VerifySuite.Assert(registered.Count == 1
                              && registered[0].effectId == payloadId && registered[0].caster == h1,
                $"载荷登记完整（效果 id+施诅方；实际 {registered.Count} 条）");

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

            // ---- ① 激活 + 蚕褪伤害帽（2026-10-04 承接原丰盈并扩生物；双方生效） ----
            var capCarrier = MakeRuleCarrier("蚕褪仪典", RuleAuraComponents.DamageCap, 9);
            VerifySuite.Assert(GameActions.PlayCard(icore, i1, capCarrier, null, Zone.Hand, 0, out var rejectI1),
                $"蚕褪载体打出（拒绝原因：{rejectI1 ?? "无"}）");
            GameActions.DrainStack(icore);
            VerifySuite.Assert(capCarrier.GetZone() == Zone.Battlefield && RuleAuraSystem.IsActive(RuleAuraComponents.DamageCap),
                "载体入场 + 规则激活（IsActive 实时查询）");
            int i1LifeCap = i1.Life, i2LifeCap = i2.Life;
            KeywordRules.ApplyDamage(i2, i1, 8, isCombat: false);
            KeywordRules.ApplyDamage(i1, i2, 8, isCombat: false);
            VerifySuite.Assert(i1.Life == i1LifeCap - 5 && i2.Life == i2LifeCap - 5,
                $"伤害帽双方生效（角色）：两侧 8 伤均钳 5（p1 {i1LifeCap}→{i1.Life}，p2 {i2LifeCap}→{i2.Life}）");
            var capCreature = new CardWrapper(new CardData
            {
                ID = "V9I_CAP_CR", CardName = "蚕褪生物探针", Supertype = Cardtype.Creature, Power = 1, Life = 10,
            });
            capCreature.SetController(i2);
            KeywordRules.ApplyDamage(i1, capCreature, 8, isCombat: false);
            VerifySuite.Assert(capCreature.GetLife() == 5,
                $"伤害帽扩生物（2026-10-04 蚕褪）：单次 8 伤钳 5（实际余 {capCreature.GetLife()}）");

            // ---- ①b 丰盈（2026-10-04 改造）：回复溢出→生命上限+1（收编旧写死基线） ----
            var bloomCarrier = MakeRuleCarrier("丰盈仪典", RuleAuraComponents.HealOverflow, 9);
            VerifySuite.Assert(GameActions.PlayCard(icore, i1, bloomCarrier, null, Zone.Hand, 0, out var rejectI1b),
                $"丰盈载体打出（唯一槽换掉蚕褪；拒绝原因：{rejectI1b ?? "无"}）");
            GameActions.DrainStack(icore);
            VerifySuite.Assert(capCarrier.GetZone() == Zone.Graveyard && RuleAuraSystem.IsActive(RuleAuraComponents.HealOverflow),
                "蚕褪送墓、丰盈（溢出转化）激活");
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

            // ---- ② 全局唯一：新光环登场把旧载体送墓 ----
            var riverCarrier = MakeRuleCarrier("纳川仪典", RuleAuraComponents.HandLimitNoFatigue, 2);
            VerifySuite.Assert(GameActions.PlayCard(icore, i1, riverCarrier, null, Zone.Hand, 0, out var rejectI2),
                $"纳川载体打出（拒绝原因：{rejectI2 ?? "无"}）");
            GameActions.DrainStack(icore);
            VerifySuite.Assert(bloomCarrier.GetZone() == Zone.Graveyard, "全局唯一：新光环登场把旧载体送墓");
            VerifySuite.Assert(!RuleAuraSystem.IsActive(RuleAuraComponents.HealOverflow)
                              && RuleAuraSystem.IsActive(RuleAuraComponents.HandLimitNoFatigue),
                "旧规则失效、新规则激活（单槽切换）");
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
            VerifySuite.Assert(RuleAuraSystem.Active == null, "新局光环槽位空（InitGame 含 Reset——跨局不残留）");
            var capCarrier2 = MakeRuleCarrier("蚕褪仪典", RuleAuraComponents.DamageCap, 2);
            VerifySuite.Assert(GameActions.PlayCard(icore, i1, capCarrier2, null, Zone.Hand, 0, out var rejectI3),
                $"重激活蚕褪（拒绝原因：{rejectI3 ?? "无"}）");
            GameActions.DrainStack(icore);
            VerifySuite.Assert(RuleAuraSystem.IsActive(RuleAuraComponents.DamageCap), "重激活成功（前置）");
            icore.Reset();
            VerifySuite.Assert(RuleAuraSystem.Active == null
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

            // ---- ⑦ 窥渊：展示联动 + 回合开始锁定 + 出牌限制（信息轴×规则轴） ----
            var lockCarrier = MakeRuleCarrier("窥渊仪典", RuleAuraComponents.LockRevealed, 2);
            VerifySuite.Assert(GameActions.PlayCard(icore, i1, lockCarrier, null, Zone.Hand, 0, out var rejectI5),
                $"窥渊载体打出（唯一槽自动换掉归土；拒绝原因：{rejectI5 ?? "无"}）");
            GameActions.DrainStack(icore);
            VerifySuite.Assert(graveCarrier.GetZone() == Zone.Graveyard
                              && RuleAuraSystem.IsActive(RuleAuraComponents.LockRevealed),
                "唯一槽切换：归土载体送墓、窥渊激活");

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

            // 展示源保险：i1 手牌补一张填充（保证 i2 回合开始的光环随机展示必有未展示候选）
            IToHand(i1, new CardData { ID = "V9I_REVEAL_FILL", CardName = "展示填充", Supertype = Cardtype.Spell });
            GameActions.EndTurn(icore, i1);
            icore.TurnEngine.CheckPhaseTransition(); // → i2 回合开始：光环随机展示 i1 一张手牌并锁定
            VerifySuite.Assert(RevealRules.GetExposedCards(icore.ZoneManager, i1, Zone.Hand).Count == 1,
                "光环随机展示：回合开始随机展示对手一张手牌（2026-10-04 追加——RevealCard 同款口径）");
            GameActions.SkipElementPool(icore, i2);
            GameActions.EndTurn(icore, i2);
            icore.TurnEngine.CheckPhaseTransition(); // → i1 回合开始：随机展示 i2 一张 + 赋予被展示卡锁定×1
            GameActions.SkipElementPool(icore, i1);
            var i2Locked = i2Exposed[0];
            VerifySuite.Assert(i2Locked.GetCounterCount(CounterRules.LockCounter) == 1,
                "回合开始赋予对手被展示卡锁定指示物×1（2026-10-04 原子化——全部被展示卡，无选择窗口）");
            VerifySuite.Assert(RevealRules.GetExposedCards(icore.ZoneManager, i2, Zone.Hand).Count >= 2,
                "光环随机展示并锁定：i2 被展示卡含展示术一张+光环随机一张（同回合生效）");
            VerifySuite.Assert(!RuleHooks.CanPlay(icore, i2, i2Locked, Zone.Hand),
                "被锁定卡不可打出（IPlayRestriction——PlayCard/响应出牌同门）");
            // 环境确定性：i2 手牌裁到只剩被锁定卡——后续多回合抽牌不触手牌上限弃牌
            //（无头自动弃牌可能弃掉被展示卡→换区清展示→污染续锁断言）
            foreach (var c in icore.ZoneManager.GetCards(i2, Zone.Hand).ToList())
                if (c != i2Locked) icore.ZoneManager.MoveCard(c, i2, Zone.Hand, Zone.Graveyard);
            GameActions.EndTurn(icore, i1);
            icore.TurnEngine.CheckPhaseTransition();
            VerifySuite.Assert(RuleAuraComponents.IsLockedThisTurn(i2Locked),
                "锁定持续到持有者回合结束（施放方回合末不清——指示物层数倒数制）");
            GameActions.SkipElementPool(icore, i2);
            GameActions.EndTurn(icore, i2); // 持有者 i2 回合结束：手牌指示物倒数 1→0
            VerifySuite.Assert(i2Locked.GetCounterCount(CounterRules.LockCounter) == 0
                              && RuleHooks.CanPlay(icore, i2, i2Locked, Zone.Hand),
                "持有者回合结束锁定倒数归零（手牌区与场上同样结算）——解锁可打出");
            icore.TurnEngine.CheckPhaseTransition(); // → i1 回合开始：卡仍被展示 → 光环续锁
            VerifySuite.Assert(i2Locked.GetCounterCount(CounterRules.LockCounter) == 1,
                "持续暴露续锁：每回合开始对仍被展示的卡再赋一回合锁定");

            // ---- ⑦b 锁定原子直发（LockCard value=2）：层=剩余回合，直调两个持有者回合末倒数
            //     （不经回合引擎推进——光环随机展示/续锁不再干扰层数口径；回合集成路径 ⑦ 已覆盖）----
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
            GameActions.SkipElementPool(icore, i1); // 回合交还 i1（⑧-⑩ 同一主阶段连续驱动）

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

            var bloodCarrier = MakeRuleCarrier("血偿仪典", RuleAuraComponents.BloodPact, 2);
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
            var samsaraCarrier = MakeRuleCarrier("轮回仪典", RuleAuraComponents.DoubleTurn, 2);
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

            // ---- ① 双侧+全取 → ×0.5（对照=同款己方全取全价；底盘退费 2 同折于两侧，比较面只差减半） ----
            var healOwn = CardCostService.Derive(KCard("V9K_HEAL_OWN", new List<int> { 1 }, 2, AtomicEffectType.Heal, value: 4));
            var healBoth = CardCostService.Derive(KCard("V9K_HEAL_BOTH", new List<int> { 1, 2 }, 5, AtomicEffectType.Heal, value: 4));
            const int kChassisRefund = 2; // 法术底盘退费（攻守默认退 2——V9.a 同口径）
            int expectedHalf = Math.Max(0, (int)Math.Round((healOwn.DerivedTotal + kChassisRefund) * 0.5f, MidpointRounding.AwayFromZero) - kChassisRefund);
            VerifySuite.Assert(healBoth.DerivedTotal == expectedHalf,
                $"双方全体恢复减半：己方全取 {healOwn.DerivedTotal} → 双方全取 {healBoth.DerivedTotal}（期望 {expectedHalf} = round((总量+退2)×0.5)−退2）");

            // ---- ② 双侧域单体任选一侧 → 不减（宽域选一≠同时作用双方） ----
            var dmgPickOne = CardCostService.Derive(KCard("V9K_DMG_PICK1", new List<int> { 1, 2 }, 3, AtomicEffectType.DealDamage, targetCount: 1));
            var dmgOwn = CardCostService.Derive(KCard("V9K_DMG_OWN", new List<int> { 1 }, 3, AtomicEffectType.DealDamage, targetCount: 1));
            VerifySuite.Assert(dmgPickOne.DerivedTotal == dmgOwn.DerivedTotal,
                $"双侧域单体任选不減（{dmgPickOne.DerivedTotal} == 单侧同款 {dmgOwn.DerivedTotal}）");

            // ---- ③ 规则光环：空域不乘期望 N + 恒视为双方 ×0.5（2026-10-04 光环改造后锚价全动态读表） ----
            var capRow = AtomicEffectTable.GetAll().FirstOrDefault(r => r != null && r.DisplayName == "蚕褪仪典");
            VerifySuite.Assert(capRow != null, "蚕褪仪典表行存在（计价锚前置）");
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
            // 整卡链：锚×0.5（恒双方）× f(未声明档=d(9)=0.75) 取整 − 底盘退2（下限0）——锚价动态读表
            int expectedAuraTotal = Math.Max(0, (int)Math.Round((capRow?.TotalUnitCost ?? 0f) * 0.5f * 0.75f,
                MidpointRounding.AwayFromZero) - kChassisRefund);
            VerifySuite.Assert(auraCost.DerivedTotal == expectedAuraTotal,
                $"规则光环整卡计价=round(锚{capRow?.TotalUnitCost ?? 0f}×0.5×0.75)−底盘退2（实际 {auraCost.DerivedTotal}，期望 {expectedAuraTotal}）");
            // 无底盘干扰的纯原子口径（RewardDerivedCost：Once/单目标 shim）：
            // 行身份（RowHashId→蚕褪行锚）+ 空域不乘期望 N + ModifyGameRule 恒双方 → round(锚×0.5)
            var auraAtomCost = CostDerivationService.RewardDerivedCost(new List<AtomicEffectInstance>
            {
                new AtomicEffectInstance { Type = AtomicEffectType.ModifyGameRule, Value = 1, StringValue = "DamageCap",
                    TargetKinds = new List<int>(), Polarity = 0f, RowHashId = capRow?.HashId },
            });
            VerifySuite.Assert(Math.Abs(auraAtomCost - (int)Math.Round((capRow?.TotalUnitCost ?? 0f) * 0.5f, MidpointRounding.AwayFromZero)) < 0.01f,
                $"规则光环纯原子价=round(锚{capRow?.TotalUnitCost ?? 0f}×0.5)（实际 {auraAtomCost}——按行取锚/空域不乘期望 4/恒双方；锚价动态读表）");
            // 行身份回归锚：同枚举不同行各自计价（疾风=CastSpeedUp 蓝锚 vs 蚕褪锚——末行回落口径不再串行）
            var galeRow = AtomicEffectTable.GetAll().FirstOrDefault(r => r != null && r.DisplayName == "疾风仪典");
            var galeAtomCost = CostDerivationService.RewardDerivedCost(new List<AtomicEffectInstance>
            {
                new AtomicEffectInstance { Type = AtomicEffectType.ModifyGameRule, Value = 1, StringValue = "CastSpeedUp",
                    TargetKinds = new List<int>(), Polarity = 0f, RowHashId = galeRow?.HashId },
            });
            VerifySuite.Assert(Math.Abs(galeAtomCost - (int)Math.Round((galeRow?.TotalUnitCost ?? 0f) * 0.5f, MidpointRounding.AwayFromZero)) < 0.01f
                               && Math.Abs(galeAtomCost - auraAtomCost) > 0.01f,
                $"变体行各自计价：疾风锚{galeRow?.TotalUnitCost ?? 0f}×0.5（实际 {galeAtomCost}≠蚕褪 {auraAtomCost}——RowHashId 行身份生效；锚价动态读表）");

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

            // ---- ② 临时卡真路径（回响宣言复制 + 微缩复制）→ 不能作地牌（放大同微缩管线不重复驱动） ----
            var echoData = new CardData
            {
                ID = "V9L_ECHO", CardName = "V9L回响术", Supertype = Cardtype.Spell,
                Keywords = new List<string> { KeywordRules.Echo }, // 印刷关键词（构造回灌 Printed 轨）
            };
            var echoCard = LToHand(l1, echoData);
            VerifySuite.Assert(GameActions.PlayCard(lcore, l1, echoCard), "回响术打出");
            GameActions.DrainStack(lcore);
            var echoCopy = lcore.ZoneManager.GetCards(l1, Zone.Hand)
                .FirstOrDefault(c => c.IsTemporary && c.ID != null && c.ID.StartsWith("V9L_ECHO#t"));
            VerifySuite.Assert(echoCopy != null, "回响宣言时点获得临时复制（TempCopyRules 真路径）");
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

            VerifySuite.Section("V9.m 归土回响乒乓：唯一槽新换旧/墓地使用/临时归土/配额不随换任刷新");
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
                    Keywords = new List<string> { KeywordRules.Echo }, // 回响随复制连锁
                };
                d.Effects.Add(new CardEffectData
                {
                    Id = id + "_EFF",
                    TriggerTiming = (int)TriggerTiming.OnPlay,
                    SelectionMode = -1,
                    AtomicEffects = new List<AtomicEffectEntry>
                    {
                        new AtomicEffectEntry { refId = graveRow?.HashId, value = 1, str = RuleAuraComponents.GraveyardPlay },
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
                              && ReferenceEquals(RuleAuraSystem.Active.Carrier, auraOriginal),
                "原版归土入场并激活规则光环（载体=原版）");
            VerifySuite.Assert(MTemps().Count == 1, $"宣言时点回响复制 ×1（实际 {MTemps().Count}）");

            // ---- ② 打出临时归土①：唯一槽把原版送墓（载体换临时）----
            var tempGui1 = MTemps()[0];
            VerifySuite.Assert(GameActions.PlayCard(mcore, m1, tempGui1), "临时归土①打出（回响连锁再得临时②）");
            GameActions.DrainStack(mcore);
            VerifySuite.Assert(auraOriginal.GetZone() == Zone.Graveyard
                              && ReferenceEquals(RuleAuraSystem.Active.Carrier, tempGui1),
                "唯一槽：第二张归土把第一张（原版）送墓，载体换为临时①");
            VerifySuite.Assert(MTemps().Count == 1, $"连锁中（手上临时归土②；实际 {MTemps().Count}）");

            // ---- ③ 归土规则：从墓地使用刚送墓的原版 → 原版再入场，临时载体被顶替送墓（配额第 1 次消耗）----
            VerifySuite.Assert(GameActions.PlayCard(mcore, m1, auraOriginal, null, Zone.Graveyard, 0, out var rejectM1),
                $"从墓地使用原版归土（拒绝原因：{rejectM1 ?? "无"}——配额第 1 次）");
            GameActions.DrainStack(mcore);
            VerifySuite.Assert(auraOriginal.GetZone() == Zone.Battlefield
                              && ReferenceEquals(RuleAuraSystem.Active.Carrier, auraOriginal),
                "原版归土再生效（墓地使用→再入场→唯一槽把临时①送墓=临时归土消失）");
            VerifySuite.Assert(tempGui1.GetZone() == Zone.Graveyard, "临时归土①被顶替进墓（消失于场）");
            VerifySuite.Assert(MTemps().Count == 2, $"原版墓地施放也回响（手上临时②③；实际 {MTemps().Count}）");

            // ---- ④ 打出手里第二张临时归土：唯一槽再把原版送墓 ----
            var tempGui2 = MTemps()[0];
            VerifySuite.Assert(GameActions.PlayCard(mcore, m1, tempGui2), "临时归土②打出");
            GameActions.DrainStack(mcore);
            VerifySuite.Assert(auraOriginal.GetZone() == Zone.Graveyard
                              && ReferenceEquals(RuleAuraSystem.Active.Carrier, tempGui2),
                "唯一槽再换任：原版再入墓、载体=临时②");

            // ---- ⑤ 配额判定：本回合已用过墓地使用——再试拒绝（光环换任不刷新配额）----
            VerifySuite.Assert(!GameActions.PlayCard(mcore, m1, auraOriginal, null, Zone.Graveyard, 0, out var rejectM2)
                              && rejectM2 != null,
                $"每回合一次配额：第二次墓地使用被拒（{rejectM2}——光环载体换来换去不刷新）");

            VerifySuite.Section("V9 引擎规则回归完成");
        }
    }
}
