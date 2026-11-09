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
        // ======================================== 关键词行为（合成随从） ========================================

        /// <summary>
        /// 关键词行为验证（合成随从，不依赖卡表）：横置可用性/冲锋/突袭/嘲讽/潜行/警戒/
        /// 先攻/连击/碾压/吸血/系命/圣盾/坚韧/护甲/不灭/复生/再生/辟邪/法术护盾。
        /// 守卫/风怒已删除（2026-09-03 原子表整体修正）；成长已删除（2026-10-08 机制移除）；
        /// 毒刺/冰晶改写族已删（2026-10-15 整体退役——改写链仅余舍身（2026-10-30 改版战斗法师），毒素例程随退）；
        /// 穿透伤害/沉默/净化/换区清除（属性指示物反向回写）同段验证。
        /// 死亡交互（剧毒×不灭×复生）另见 DeathRules 决策表。
        /// </summary>
        private static void TestKeywords(GameCore core, Player p1, Player p2)
        {
            var combat = core.CombatSystem;
            var used = new List<Card>();

            // 合成随从：进场（带失调）+ 登记（测试后统一清理）
            // 2026-11-06 攻/守 opt-in 后补 AttackAbility/GuardAbility=恢复改版前默认资格——
            // 本段攻击/守卫/反击夹具全部依赖（无资格→CanDeclareAttack/PushGuardDeclaration 恒拒）
            Card Make(Player owner, int power, int life, params string[] keywords)
            {
                var data = new CardData { ID = "VERIFY_KW_" + used.Count, CardName = "KW" + used.Count };
                data.Supertype = Cardtype.Creature;
                data.AttackAbility = true;
                data.GuardAbility = true;
                data.Power = power;
                data.Life = life;
                foreach (var kw in keywords) data.Keywords.Add(kw);
                var card = new CardWrapper(data);
                card.SetController(owner);
                // 入场结果显式断言（2026-09-10）：战场容量 9（P11），静默入不了场会把后续断言级联成假回归
                Assert(core.ZoneManager.TryAddToBattlefield(card, owner),
                       $"合成随从入场成功（{data.CardName}）");
                card.Untap(); // 本段测试前提 = 可用随从（横置行为单独断言；入场即可用后为无害冗余）
                used.Add(card);
                return card;
            }

            void CleanKeywords()
            {
                foreach (var c in used)
                    core.ZoneManager.GetZoneContainer(c.GetController() ?? p1).Remove(c, Zone.Battlefield);
            }

            // 战场清场（2026-09-10）：本段合成量大（p1 侧 25+；容量 9=P11）且前置段落可能遗留单位——
            // 段界清场保证各段从净战场起步，避免「静默入不了场 → 断言级联」的假回归。
            void ResetField()
            {
                foreach (var p in new[] { p1, p2 })
                {
                    var container = core.ZoneManager.GetZoneContainer(p);
                    foreach (var c in core.ZoneManager.GetCards(p, Zone.Battlefield).ToList())
                        container.Remove(c, Zone.Battlefield);
                }
                used.Clear();
            }
            ResetField();
            // 相位复位（2026-09-20 补）：本段大量 DeclareAttack/PlayCard 走 CanCombatAction 门（Main+Active）——
            // 前序段落结束状态不保证 p1 主阶段（曾致战斗段整体级联假失败）；与 TestSleep 等同款前置。
            EnsureMainPhase(core, p1);
            // 血量复位（2026-11-05 补）：前序段落累计的合法直伤/打脸会把 p2 磨到斩杀线内——本段
            // 多个战斗夹具直攻角色（横置即上限/警戒/系命），残血进段会触发真实 LifeZero 终局并
            // 锁存 gameOver 毒化其后全部场景（回合推进瘫痪）。本段验证关键词行为，不验证斩杀预算。
            p1.Life = p1.GetMaxLife();
            p2.Life = p2.GetMaxLife();

            // ---- 1. 表与工厂 ----
            Assert(CardCore.Attribute.AtomicEffectTable.GetByEnumName("GrantReborn") != null
                   && CardCore.Attribute.AtomicEffectTable.GetByEnumName("GrantIndestructible") != null
                   && CardCore.Attribute.AtomicEffectTable.GetByEnumName("GrantLifelink") != null
                   && CardCore.Attribute.AtomicEffectTable.GetByEnumName("AddArmor") != null,
                   "新关键词（复生/不灭/系命）与护甲原子已入表");
            // 复生 2026-10-08 指示物化：GrantReborn 行存续（refId 已重推 502be10d→94f6a32d）但退出关键词工厂——
            // 指示物处理器族接管（GrantRebornHandler 挂 RebornCounter 层）
            string rebornId;
            Assert(!CardCore.Attribute.Handlers.GrantKeywordHandlerFactory.TryGetKeywordId(AtomicEffectType.GrantReborn, out rebornId)
                   && CardCore.Attribute.CounterRules.RebornCounter == "Reborn",
                   "复生已退关键词族：工厂不再登记（指示物处理器接管，counter id 承接运行时字符串）");
            // 法术护盾 2026-10-09 指示物化：同口径收官——行存续（refId 重推 a3ad6a04→7270df35）退关键词工厂
            string spellShieldId;
            Assert(!CardCore.Attribute.Handlers.GrantKeywordHandlerFactory.TryGetKeywordId(AtomicEffectType.GrantSpellShield, out spellShieldId)
                   && CardCore.Attribute.CounterRules.SpellShieldCounter == "SpellShield",
                   "法术护盾已退关键词族：工厂不再登记（指示物处理器接管，counter id 承接运行时字符串）");
            // 2026-11-05 ID 冻结算起：不再钉死历史 refId 字面量（文本改一次就换一次号），只断「行在、
            // 有稳定引用键」——语义意图（指示物化后行仍存续、可被 refId 引用）不变。
            var spellShieldRow = CardCore.Attribute.AtomicEffectTable.GetByEnumName("GrantSpellShield");
            Assert(spellShieldRow != null && !string.IsNullOrEmpty(spellShieldRow.HashId),
                   $"法术护盾行存续且有 refId（当前 {spellShieldRow?.HashId}；2026-11-05 起 ID 冻结，不再钉字面量）");

            // ---- 2. 横置可用性 / 冲锋 / 突袭（2026-10-17 定案：取消横置入场——入场一律可用；冲锋/突袭=登场效果，解横置成空操作） ----
            var tappedUnit = Make(p1, 3, 3);
            tappedUnit.Tap(); // 模拟横置在场（未过回合重置）
            Assert(!combat.CanDeclareAttack(tappedUnit, p1), "横置随从不可攻击（可用性统一走横置）");
            var readyUnit = Make(p1, 3, 3);
            Assert(combat.CanDeclareAttack(readyUnit, p1), "未横置随从可攻击");

            // 入场（直接观察 TryAddToBattlefield 行为，不经 Make 的竖置前提）
            CardWrapper MakeEntry(params CardEffectData[] effects)
            {
                var data = new CardData { ID = "VERIFY_KW_ENTRY_" + used.Count, CardName = "入场" + used.Count };
                data.Supertype = Cardtype.Creature;
                data.AttackAbility = true; // 2026-11-06 opt-in：冲锋夹具宣言攻击走 CanDeclareAttack 资格闸
                data.GuardAbility = true;
                data.Power = 3; data.Life = 3;
                if (effects != null && effects.Length > 0)
                {
                    data.Effects ??= new List<CardEffectData>();
                    data.Effects.AddRange(effects);
                }
                var card = new CardWrapper(data);
                card.SetController(p1);
                // 经发动区真入场（CastPlayed 派生）：OnPlay 载荷过滤只认 CastPlayed——
                // 直接 TryAddToBattlefield 是 TokenSpawned，冲锋/突袭的登场效果（OnPlay）不会触发
                // （锚在 f1eb82c 写下时即用错路径，存量修正）
                core.ZoneManager.GetZoneContainer(p1).Add(card, Zone.Activation);
                // 入场结果显式断言：满场入墓会静默断链登场效果（OnPlay 依赖 CastPlayed 入场事件）
                Assert(core.ZoneManager.TryMoveToBattlefield(card, p1, Zone.Activation),
                       $"经发动区入场成功（{data.CardName}）");
                used.Add(card);
                return card;
            }

            // 冲锋/突袭的登场效果（2026-09-08）：
            // OnPlay + 激励自己（域 {Self}）。突袭的自紊乱曾以 SelfSickness 代价承载——
            // 2026-09-14 代价原子化后退役（代价栏只剩 Payload 原子；自上紊乱改由 RushSickness
            // 原子表达——下方以直接执行原子模拟付费后果）。
            CardEffectData EntryReadyEffect(bool withSickness)
            {
                var eff = new CardEffectData
                {
                    // 2026-09-13 修复：Id 参数化——冲锋/突袭两张夹具共用同 Id 时，
                    // 触发上限闸门（按 effect.Id 记账，默认一回合一次）会把第二次触发静默拦截，
                    // 突袭段的入场效果根本不上栈（表现="解除横置失败"，曾误诊为代价语义分歧）
                    Id = withSickness ? "VERIFY_ENTRY_READY_RUSH" : "VERIFY_ENTRY_READY",
                    DisplayName = withSickness ? "突袭" : "冲锋",
                    Description = "登场：激励自身——解除横置",
                    TriggerTiming = (int)TriggerTiming.OnPlay,
                    SelectionMode = (int)CardCore.SelectionMode.Single,
                };
                eff.AtomicEffects = new List<AtomicEffectEntry>
                {
                    AtomRefs.New(AtomicEffectType.Untap, value: 1, kinds: new List<int> { 0 }),
                };
                return eff;
            }

            var plain = MakeEntry();
            Assert(!plain.IsTapped(), "普通随从：入场即可用（2026-10-17 取消横置入场）");
            // A组诊断埋点 v2（2026-09-10）：计数改为按效果 Id 过滤（v1 无过滤，×1 可能是杂散效果）；
            // 附转换 X 光（直接看 ConvertAll 产物的域/模式/激活类）+ 入场即入队探针。
            CardWrapper charger = null;
            int stackAdd = 0;
            EffectInstance chargerInstance = null;
            void OnStackAdd(StackAddEvent e)
            {
                if (e.AddedObject is EffectInstance ei && ei.Definition?.Id == "VERIFY_ENTRY_READY")
                {
                    stackAdd++;
                    chargerInstance = ei; // 捕获实例引用，事后查 IsResolved
                }
            }
            EventManager.Instance.Subscribe<StackAddEvent>(OnStackAdd);
            charger = MakeEntry(EntryReadyEffect(false));
            Assert(!charger.IsTapped(), "冲锋（登场效果）：入场即可用（解横置效果行成为空操作）");
            bool queuedAtEntry = core.StackEngine.HasPendingEffects; // 入场事件应已把 OnPlay 排进待发队列
            var conv = GameActions.GetCardEffectDefinitions(charger);
            UnityEngine.Debug.Log("[KWDBG] charger 转换=" + string.Join(";", conv.Select(d =>
                $"{d.Id}:触发式={d.IsTriggeredEffect},激活={d.ActivationType},域数={(d.TargetDomain == null ? -1 : d.TargetDomain.Count)}"
                + $",模式={d.SelectionMode},filter={d.TargetFilter}"))
                + $" | 入场即待发={queuedAtEntry}");
            GameActions.DrainStack(core); // 排干栈：登场效果结算
            EventManager.Instance.Unsubscribe<StackAddEvent>(OnStackAdd);
            UnityEngine.Debug.Log($"[KWDBG] charger 本体上栈×{stackAdd} 已结算标记={chargerInstance?.IsResolved}"
                                + $" 目标数={chargerInstance?.Targets?.Count} tapped={charger.IsTapped()} "
                                + $"栈深={core.StackEngine.StackSize} 待发={core.StackEngine.HasPendingEffects} "
                                + $"结算中={core.StackEngine.IsResolving}");
            Assert(!charger.IsTapped(),
                   $"冲锋（登场效果）：结算后解除横置（激励自己）（诊断：入场即待发={queuedAtEntry} 上栈×{stackAdd} "
                   + $"已结算={chargerInstance?.IsResolved} 目标数={chargerInstance?.Targets?.Count}——"
                   + "上栈=0查触发注册/匹配；目标数=0查Self解析[Console搜TargetDomain警告]）");
            Assert(combat.CanDeclareAttack(charger, p1) && combat.CanAttackTarget(charger, p2, p1),
                   "冲锋：无目标限制，可攻击玩家");
            var rusher = MakeEntry(EntryReadyEffect(true));
            var enemy = Make(p2, 1, 9);
            GameActions.DrainStack(core);
            Assert(!rusher.IsTapped()
                   && rusher.GetCounterCount(CardCore.Attribute.KeywordRules.RushSicknessCounter) == 0,
                   "突袭：结算后解除横置（自紊乱代价已退役——紊乱由下方原子执行模拟）");
            // 模拟「付代价」路径（2026-09-14 代价原子化）：RushSickness 原子直接执行 → 自上紊乱 1 层
            CardCore.Attribute.EffectHandlerRegistry.ExecuteEffectAsync(
                new AtomicEffectInstance { Type = AtomicEffectType.RushSickness, Value = 1 },
                new EffectExecutionContext
                {
                    Controller = p1, Source = rusher, ZoneManager = core.ZoneManager, ElementPool = core.ElementPool,
                    Targets = new List<Entity> { rusher },
                }).GetAwaiter().GetResult();
            Assert(rusher.GetCounterCount(CardCore.Attribute.KeywordRules.RushSicknessCounter) == 1,
                   "RushSickness 原子执行后：自上紊乱 1 层");
            Assert(combat.CanAttackTarget(rusher, enemy, p1) && !combat.CanAttackTarget(rusher, p2, p1),
                   "突袭：紊乱期间只能攻随从，不准攻击玩家（效果发动同口径）");
            CardCore.Attribute.CounterRules.OnTurnEnd(p1, core.ZoneManager); // 回合结束持续指示物清理
            Assert(rusher.GetCounterCount(CardCore.Attribute.KeywordRules.RushSicknessCounter) == 0
                   && combat.CanAttackTarget(rusher, p2, p1),
                   "突袭：紊乱消退（持续到回合结束）后目标限制解除");

            // ---- 3. 帷幕（原嘲讽,2026-09-13 更名=只吸引效果目标）不拦攻击；碾压已重定义为攻击溅射（见第 11 段） ----
            var attacker = Make(p1, 3, 3);
            var curtainHolder = Make(p2, 0, 9, "Taunt");
            Assert(combat.CanAttackTarget(attacker, p2, p1),
                   "帷幕：不拦攻击——有帷幕随从也可指定玩家（效果侧吸引见 TestTauntAndSickness；溅射见第 11 段）");
            curtainHolder.IsAlive = false; // 清场（不干扰后续段）

            // ---- 4. 守卫关键词已删除（2026-09-03 原子表整体修正）：无守卫转移，攻击目标保持宣言 ----
            var striker = Make(p1, 3, 3);
            var victim = Make(p2, 2, 5);
            var guardDoppel = Make(p2, 1, 8); // 同位置单位（无 Guard 关键词——已删除）
            bool decl4 = GameActions.DeclareAttack(core, p1, striker, victim);
            var attackInst4 = core.StackEngine.Peek();
            Assert(decl4 && attackInst4 != null && attackInst4.IsAttackDeclaration && attackInst4.Targets[0] == victim
                   && !guardDoppel.IsTapped(),
                   $"守卫删除：攻击目标保持宣言（无转移、无横置旁观者；宣言期零支付）"
                   + $"（诊断 decl={decl4} peek={(attackInst4 == null ? "null" : attackInst4.GetType().Name)}"
                   + $" phase={core.TurnEngine.CurrentPhase?.Phase}/{core.TurnEngine.CurrentPhase?.State}"
                   + $" turnIsP1={core.TurnEngine.TurnPlayer == p1}"
                   + $" canDecl={combat.CanDeclareAttack(striker, p1)} canTgt={combat.CanAttackTarget(striker, victim, p1)}"
                   + $" 栈深={core.StackEngine.StackSize} 待发={core.StackEngine.HasPendingEffects}"
                   + $" sp={core.StackEngine.SpeedCounter.CurrentSpeed} spr={core.StackEngine.SpeedCounter.IsResolving}"
                   + $" gate0={core.StackEngine.SpeedCounter.CanActivate(0, CardCore.EffectActivationType.Voluntary, false)}"
                   + $" prio={core.StackEngine.CurrentPriorityHolder?.Name ?? "null"}"
                   + $" act={core.StackEngine.ActivePlayer?.Name ?? "null"} turn={core.TurnEngine.TurnNumber}"
                   + $" tapped={striker.IsTapped()}）");
            GameActions.DrainStack(core);

            // ---- 5. 潜行：不可被指定 + 失效口改版（2026-11-02 终版：宣言攻击结算/守卫拦截/
            //      主动发动效果三口各消耗 1 层；实际受伤/触发式不消耗；Make 印刷路径入场挂层）----
            var lurker = Make(p2, 2, 2, CardCore.Attribute.CounterRules.StealthCounter);
            var hunter = Make(p1, 3, 3);
            Assert(!combat.CanAttackTarget(hunter, lurker, p1), "潜行：不可被指定为攻击目标（角色=友方暴露位，条件成立）");
            // 受伤不消耗（2026-11-02 改版：原受伤失效口已删——实际落定后层保持）
            Attribute.KeywordRules.ApplyDamage(hunter, lurker, 1, false);
            Assert(lurker.GetCounterCount(CardCore.Attribute.CounterRules.StealthCounter) == 1 && lurker.GetLife() == 1,
                "潜行：实际受到伤害不再消耗（2026-11-02 失效口改版）");
            // 宣言攻击结算后消耗（2026-11-02 终版：攻击/守卫/主动效果三失效口——受伤口已删；
            // EnsureMainPhase 复位防前段守卫窗优先权残留致宣言假失败、断言空洞）
            var spy = Make(p1, 2, 2, CardCore.Attribute.CounterRules.StealthCounter);
            EnsureMainPhase(core, p1);
            GameActions.DeclareAttack(core, p1, spy, p2);
            GameActions.DrainStack(core);
            Assert(spy.GetCounterCount(CardCore.Attribute.CounterRules.StealthCounter) == 0,
                "潜行：宣言攻击结算后消耗 1 层（2026-11-02 终版：攻击/守卫/主动效果三口，受伤不消耗）");
            // 触发式效果不消耗（非主动发动）——上面攻击消耗已归零，先补 1 层再验
            spy.AddCounters(CardCore.Attribute.CounterRules.StealthCounter, 1);
            var stealthDef = new EffectDefinition
            {
                Id = "VERIFY_STEALTH_PORT",
                TriggerTiming = TriggerTiming.Activate_Active,
                Effects = new List<AtomicEffectInstance> { new AtomicEffectInstance { Type = AtomicEffectType.Untap } },
            };
            core.StackEngine.GetExecutor().ExecuteAsync(new EffectInstance
            {
                Definition = stealthDef,
                Source = spy,
                Controller = p1,
                TriggeringEvent = new CardCore.DamageEvent(),
                Targets = new List<Entity> { spy },
            }, skipElementCost: true).Forget();
            Assert(spy.GetCounterCount(CardCore.Attribute.CounterRules.StealthCounter) == 1,
                "潜行：触发式效果不消耗（非主动发动）");
            // 主动发动效果消耗（三失效口之一）
            core.StackEngine.GetExecutor().ExecuteAsync(new EffectInstance
            {
                Definition = stealthDef,
                Source = spy,
                Controller = p1,
                Targets = new List<Entity> { spy },
            }, skipElementCost: true).Forget();
            Assert(spy.GetCounterCount(CardCore.Attribute.CounterRules.StealthCounter) == 0,
                "潜行：主动发动效果后消耗 1 层（三失效口之一）");

            // ---- 5b. 潜行生效条件（2026-11-02 改版）：己方全员隐藏（单位+角色）→ 潜行不保护 ----
            ResetField();
            var ghost = Make(p2, 2, 2, CardCore.Attribute.CounterRules.StealthCounter);
            var striker2 = Make(p1, 3, 3);
            p2.AddCounters(CardCore.Attribute.CounterRules.StealthCounter, 1); // 角色也被潜行=己方全员隐藏
            Assert(combat.CanAttackTarget(striker2, ghost, p1),
                "潜行条件：己方全员隐藏（唯一单位+角色均潜行）→ 潜行不保护，可被指定");
            Assert(combat.CanAttackTarget(striker2, p2, p1),
                "潜行条件：角色自身潜行且无友方暴露位 → 不保护");
            var shownAllHidden = TargetResolver.ExcludeUnselectable(new List<Entity> { ghost, p2 }, p1);
            Assert(shownAllHidden.Contains(ghost) && shownAllHidden.Contains(p2),
                "潜行条件：全员隐藏时选择域不再隐藏（潜行失效，弹窗可见）");
            p2.AddCounters(CardCore.Attribute.CounterRules.StealthCounter, -1); // 角色恢复暴露=友方暴露位
            Assert(!combat.CanAttackTarget(striker2, ghost, p1),
                "潜行条件：角色恢复未潜行=友方暴露位 → 潜行恢复保护");
            var shownCoverRestored = TargetResolver.ExcludeUnselectable(new List<Entity> { ghost, p2 }, p1);
            Assert(!shownCoverRestored.Contains(ghost) && shownCoverRestored.Contains(p2),
                "潜行条件：恢复后选择域照常隐藏潜行单位（角色本身未隐藏故可见）");

            // ---- 5c. 守卫拦截消耗潜行（2026-11-02 终版新增口；拦截落空不消耗——重检失败早退于消耗口）----
            ResetField();
            EnsureMainPhase(core, p1);
            var warden = Make(p2, 3, 6, CardCore.Attribute.CounterRules.StealthCounter);
            var raider2 = Make(p1, 4, 2);
            int p2Life5c = p2.Life;
            Assert(GameActions.DeclareAttack(core, p1, raider2, p2), "守卫潜行段：打脸宣言上栈");
            var atk5c = core.StackEngine.Peek();
            Assert(core.StackEngine.PushGuardDeclaration(warden, atk5c, p2), "守卫潜行段：守卫速度1响应入栈");
            GameActions.DrainStack(core);
            Assert(warden.IsTapped() && warden.GetLife() == 2 && p2.Life == p2Life5c,
                "守卫潜行段：拦截成立（守卫吃 4 伤 6→2、战斗后横置、玩家未被击中）");
            Assert(warden.GetCounterCount(CardCore.Attribute.CounterRules.StealthCounter) == 0,
                "潜行：守卫拦截后消耗 1 层（2026-11-02 终版失效口）");

            // ---- 6. 警戒（2026-09-10 重定义）：攻击照常横置；横置也能造成战斗伤害 ----
            ResetField(); // 段界清场（P11 容量 9：2-5 段 p1 侧已满 9——警戒/横置/激励/先攻段独立清场起步）
            var vigilant = Make(p1, 3, 3, "Vigilance");
            GameActions.DeclareAttack(core, p1, vigilant, p2);
            GameActions.DrainStack(core);
            Assert(vigilant.IsTapped(), "警戒重定义：攻击结算期照常横置（不再代替横置扣除）");
            Assert(CardCore.Attribute.KeywordRules.ShouldTap(vigilant), "警戒重定义：横置代价恒支付");
            // 横置持警戒反击：已横置防守者被攻击仍反击（见 TestAttackGuard 2b；2026-10-15 起
            // 拦截守卫战斗期间可用，反击不再依赖警戒）

            // ---- 7. 横置即上限（2026-09-10 定案：计数/会话门槛均已撤，激励可再动；逐攻击开窗）----
            var loneWolf = Make(p1, 1, 9);
            int p2Life7 = p2.Life;
            Assert(GameActions.DeclareAttack(core, p1, loneWolf, p2), "横置即上限：首次攻击宣言");
            GameActions.DrainStack(core);
            Assert(loneWolf.IsTapped() && !combat.CanDeclareAttack(loneWolf, p1),
                   "横置即上限：攻击结算后保持横置、不能再攻");
            loneWolf.Untap(); // 激励通路
            Assert(combat.CanDeclareAttack(loneWolf, p1) && GameActions.DeclareAttack(core, p1, loneWolf, p2),
                   "激励再动：解除横置即可再宣（无每回合计数/会话去重门槛）");
            GameActions.DrainStack(core);
            Assert(loneWolf.AttacksThisTurn == 2 && p2.Life == p2Life7 - 2,
                   "激励再动：两次宣言都结算（台账计数 2、玩家共受 2 伤）");

            // ---- 7b. 穿透档已删（2026-10-31 定案）：一切伤害照走防护层——圣盾挡下一次并消耗 ----
            var wardedTank = Make(p2, 1, 20, CardCore.Attribute.CounterRules.DivineShieldCounter);
            CardCore.Attribute.KeywordRules.ApplyDamage(p1, wardedTank, 5, false);
            Assert(wardedTank.IsAlive && wardedTank.GetLife() == 20
                   && wardedTank.GetCounterCount(CardCore.Attribute.CounterRules.DivineShieldCounter) == 0,
                   "穿透退役回归：伤害照走防护层——圣盾挡下一次并消耗（不再有绕防护的伤害档）");

            // ---- 8. 先攻：目标死亡不反击 ----
            var first = Make(p1, 5, 3, "FirstStrike");
            var bulky = Make(p2, 4, 3);
            bool declared8 = GameActions.DeclareAttack(core, p1, first, bulky);
            UnityEngine.Debug.Log($"[CMBTDBG] declared={declared8} stack={core.StackEngine.StackSize} first._power={first.GetPower()} layerPower={core.LayerEngine.CalculatePower(first)} firstTapped={first.IsTapped()} bulkyAlive={bulky.IsAlive} bulkyLife={bulky.GetLife()}");
            GameActions.DrainStack(core);
            UnityEngine.Debug.Log($"[CMBTDBG] after ExecuteDamage: bulkyAlive={bulky.IsAlive} bulkyLife={bulky.GetLife()} firstLife={first.GetLife()}");
            Assert(!bulky.IsAlive && first.GetLife() == 3, "先攻：目标死于先攻步，不反击");

            // ---- 8b. 缴械：被攻击的目标无法反击 ----
            ResetField(); // 段界清场（P11 容量 9：本段合成量大，逐段清防溢出）
            var disarmer = Make(p1, 3, 5, "Disarm");
            var bigGuard = Make(p2, 4, 9);
            GameActions.DeclareAttack(core, p1, disarmer, bigGuard);
            GameActions.DrainStack(core);
            Assert(disarmer.GetLife() == 5 && bigGuard.GetLife() == 6,
                   "缴械：目标（4 攻）无法反击，攻击者无伤（单向伤害）");

            // ---- 8c. 反击资格：已横置的随从只能挨打（不反击、无消耗） ----
            var aggressor = Make(p1, 3, 5);
            var tired = Make(p2, 4, 9);
            tired.Tap(); // 模拟已横置（刚攻击过/被冻结）
            GameActions.DeclareAttack(core, p1, aggressor, tired);
            GameActions.DrainStack(core);
            Assert(aggressor.GetLife() == 5 && tired.GetLife() == 6,
                   "反击资格：已横置目标（4 攻）不反击，攻击者无伤");

            // ---- 9. 连击：两步各结算一次 ----
            ResetField(); // 段界清场（容量 9）
            var doubleS = Make(p1, 2, 9, "DoubleStrike");
            var tank = Make(p2, 1, 5);
            GameActions.DeclareAttack(core, p1, doubleS, tank);
            GameActions.DrainStack(core);
            Assert(tank.GetLife() == 1 && doubleS.IsAlive, "连击：伤害结算两次（5命 −2×2 = 1）");

            // ---- 11. 碾压（2026-11-09 溅射域改版）：邻格占用者不限类别——生物/结界/角色照打 ----
            ResetField(); // 段界清场（容量 9）
            var hammer = Make(p1, 4, 9, "Overwhelm");
            var pivot = Make(p2, 1, 9);
            var neighbor = Make(p2, 1, 9);
            // 相邻结界（无生命单位）：溅射全额扣耐久（旧「只打生物 IsLivingUnit」口径退役）。
            // 耐久垫高到 9+（铭刻光环 +1 时基线 10）——避免归零直送墓干扰断言锚
            var wardStone = new CardWrapper(new CardData
            {
                ID = "VERIFY_KW_ENCH", CardName = "侧翼结界", Supertype = Cardtype.Enchantment,
            });
            wardStone.SetController(p2);
            Assert(core.ZoneManager.TryAddToBattlefield(wardStone, p2), "侧翼结界入场成功");
            wardStone.AddCounters(CardCore.Attribute.CounterRules.DurabilityCounter, 9);
            // 基线快照（铭刻光环在场时指示物添加 +1——9 或 10 均为合法基线）
            int wardBase = wardStone.GetCounterCount(CardCore.Attribute.CounterRules.DurabilityCounter);
            used.Add(wardStone);
            int p2LifeTrample = p2.Life;
            CardCore.CombatSystem.AdjacentResolver = c => c == pivot
                ? new Entity[] { neighbor, wardStone, p2 } : System.Array.Empty<Entity>();
            GameActions.DeclareAttack(core, p1, hammer, pivot);
            GameActions.DrainStack(core);
            Assert(pivot.GetLife() == 5 && neighbor.GetLife() == 5, "碾压：目标与相邻随从各受 4 点");
            Assert(wardStone.IsAlive
                   && wardStone.GetCounterCount(CardCore.Attribute.CounterRules.DurabilityCounter) == wardBase - 4,
                   $"碾压：相邻结界吃溅射——耐久全额扣（{wardBase}→{wardBase - 4}）——实际 alive={wardStone.IsAlive} " +
                   $"耐久={wardStone.GetCounterCount(CardCore.Attribute.CounterRules.DurabilityCounter)} " +
                   $"区={wardStone.GetZone()}");
            Assert(p2.Life == p2LifeTrample - 4, "碾压：邻格=防守方角色格——溅射直伤角色 4 点");

            // ---- 11-2. 碾压被拦截：守卫改写主目标，溅射仍以宣言原目标的邻格计算 ----
            ResetField(); // 段界清场（容量 9）
            var ram = Make(p1, 5, 9, "Overwhelm");
            var crushPrey = Make(p2, 1, 9);       // 宣言原目标（碾压溅射枢轴）
            var flanker = Make(p2, 1, 9);      // 原目标邻格生物
            var interceptor = Make(p2, 2, 6);  // 守卫（速度1响应改写主目标）
            CardCore.CombatSystem.AdjacentResolver = c => c == crushPrey
                ? new Entity[] { flanker } : System.Array.Empty<Entity>();
            Assert(GameActions.DeclareAttack(core, p1, ram, crushPrey), "碾压拦截段：攻击原目标宣言上栈");
            Assert(core.StackEngine.PushGuardDeclaration(interceptor, core.StackEngine.Peek(), p2),
                   "碾压拦截段：守卫速度1响应入栈");
            GameActions.DrainStack(core); // 守卫先结（目标改写）→ 攻击结算（战斗发生在守卫那格）
            Assert(interceptor.GetLife() == 1 && interceptor.IsTapped(),
                   "碾压被拦截：守卫承担主战斗伤害（6−5）并横置");
            Assert(crushPrey.GetLife() == 9 && !crushPrey.IsTapped(),
                   "碾压被拦截：原目标免主战斗伤害（战斗发生在守卫那格）");
            Assert(flanker.GetLife() == 4, "碾压被拦截：溅射仍按宣言原目标邻格结算（9−5）");
            CardCore.CombatSystem.AdjacentResolver = null;

            // ---- 11b. 溅射连锁（2026-11-09 改版，红3）：战斗结算对随机对手单位造成溢出伤害；
            //      溅射伤害再次溢出则继续连锁。夹具对称（受害者同攻同命）——链路结局与随机选谁无关 ----
            ResetField(); // 段界清场（容量 9）
            var splasher = Make(p1, 6, 9, "Splash");
            var splashBait = Make(p2, 0, 1); // 主目标 1 命：溢出 6−1=5
            var chainA = Make(p2, 0, 2);
            var chainB = Make(p2, 0, 2);
            int p2LifeSplash = p2.Life;
            GameActions.DeclareAttack(core, p1, splasher, splashBait);
            GameActions.DrainStack(core);
            // 链路：主目标吃 6 死 → 首溅 5（2 命死，余 3）→ 次溅 3（另一只 2 命死，余 1）→ 池空终止
            Assert(!splashBait.IsAlive && !chainA.IsAlive && !chainB.IsAlive,
                   "溅射连锁：主目标与两只溅射受害者全灭（6 攻：1命→溅5；2命→溅3；2命→余1 池空止）");
            Assert(splasher.IsAlive && p2.Life == p2LifeSplash,
                   "溅射连锁：0 攻受害者不反击；溅射只打单位，不波及角色");

            // 恰好耗尽：溅射量=受害者生命 → 连锁终止（对称双胞胎，另一只必然幸存满命）
            ResetField(); // 段界清场（容量 9）
            var pincher = Make(p1, 3, 9, "Splash");
            var pinchBait = Make(p2, 0, 1); // 溢出 3−1=2
            var pinchA = Make(p2, 0, 2);
            var pinchB = Make(p2, 0, 2);
            GameActions.DeclareAttack(core, p1, pincher, pinchBait);
            GameActions.DrainStack(core);
            Assert(!pinchBait.IsAlive
                   && ((!pinchA.IsAlive) != (!pinchB.IsAlive)) // 恰一只被溅死
                   && (pinchA.IsAlive ? pinchA.GetLife() : pinchB.GetLife()) == 2,
                   "溅射恰尽：溢出 2 恰好击杀 2 命受害者后连锁终止（幸存者满命 2）");

            // ---- 12. 毒刺/冰晶已删（2026-10-15 战斗伤害改写族整体退役——KeywordRules 改写链仅余舍身
            //      （2026-10-30 改版战斗法师，散射无配对）；毒素指示物随毒刺失去唯一施加口，回合末发作例程断言一并退役）----

            // ---- 12c. 剧毒关键词（2026-10-08 指示物转关键词·落定追加式）：受其战斗伤害的生物被消灭 ----
            ResetField(); // 段界清场（容量 9）
            var venomFang = Make(p1, 2, 9, "Venom");
            var bigPrey = Make(p2, 3, 10);
            CardCore.Attribute.KeywordRules.ApplyDamage(venomFang, bigPrey, 2, true);
            Assert(!bigPrey.IsAlive || bigPrey.GetZone() == Zone.Graveyard,
                   "剧毒关键词：受其战斗伤害的生物被消灭（2 伤落定→TryKill，剩余生命无关）");
            var shieldPrey = Make(p2, 1, 9, CardCore.Attribute.CounterRules.DivineShieldCounter);
            CardCore.Attribute.KeywordRules.ApplyDamage(venomFang, shieldPrey, 4, true);
            Assert(shieldPrey.IsAlive && shieldPrey.GetLife() == 9
                   && shieldPrey.GetCounterCount(CardCore.Attribute.CounterRules.DivineShieldCounter) == 0,
                   "剧毒对照：圣盾挡下（落定 0）不触发消灭，圣盾照常消耗");
            int p2LifeBeforeVenom = p2.Life;
            CardCore.Attribute.KeywordRules.ApplyDamage(venomFang, p2, 3, true);
            Assert(p2.Life == p2LifeBeforeVenom - 3 && p2.IsAlive,
                   "剧毒对照：对角色照常落血不消灭（Deathtouch 口径）");

            // ---- 13. 吸血（恢复自身）/ 系命（回复角色） ----
            ResetField(); // 段界清场（容量 9）：2-12 段已累计 17+ 单位——逐段清场在容量 9 下是硬前提
            var bat = Make(p1, 2, 3, "Lifesteal");
            CardCore.Attribute.KeywordRules.ApplyDamage(p2, bat, 2, false); // 受伤状态
            var prey = Make(p2, 0, 9);
            GameActions.DeclareAttack(core, p1, bat, prey);
            GameActions.DrainStack(core);
            Assert(bat.GetLife() == 3, "吸血：造成 2 伤害恢复随从自身");

            p1.Life = 20;
            var monk = Make(p1, 3, 3, "Lifelink");
            GameActions.DeclareAttack(core, p1, monk, p2);
            GameActions.DrainStack(core);
            Assert(p1.Life == 23, "系命：造成 3 伤害回复角色");

            // ---- 14. 圣盾 / 坚韧 / 护甲指示物 ----
            var shielded = Make(p2, 1, 5, CardCore.Attribute.CounterRules.DivineShieldCounter);
            var breaker = Make(p1, 4, 9);
            GameActions.DeclareAttack(core, p1, breaker, shielded);
            GameActions.DrainStack(core);
            Assert(shielded.IsAlive && shielded.GetLife() == 5
                   && shielded.GetCounterCount(CardCore.Attribute.CounterRules.DivineShieldCounter) == 0,
                   "圣盾：挡下一次伤害并消耗（层数归零）");
            breaker.Untap();
            breaker.AttacksThisTurn = 0; // 台账清零（2026-09-10 后非门槛，仅为下文计数断言口径干净）
            GameActions.DeclareAttack(core, p1, breaker, shielded);
            GameActions.DrainStack(core);
            Assert(shielded.GetLife() == 1, "圣盾消耗后正常受伤");

            // ---- 14b. 圣盾指示物叠加（2026-10-08 指示物化）：多层=多挡（逐份消耗口径）----
            var twin = Make(p2, 2, 9, CardCore.Attribute.CounterRules.DivineShieldCounter); // 印刷路径入场挂 1 层
            twin.AddCounters(CardCore.Attribute.CounterRules.DivineShieldCounter, 1);        // 追加 1 层（旧附加份等价）
            Assert(twin.GetCounterCount(CardCore.Attribute.CounterRules.DivineShieldCounter) == 2,
                   "圣盾指示物：两层并存（可叠加）");
            Attribute.KeywordRules.ApplyDamage(p1, twin, 4, false);
            Assert(twin.GetLife() == 9 && twin.GetCounterCount(CardCore.Attribute.CounterRules.DivineShieldCounter) == 1,
                   "两层并存：消耗 1 层，余层仍生效");
            Attribute.KeywordRules.ApplyDamage(p1, twin, 4, false);
            Assert(twin.GetLife() == 9 && twin.GetCounterCount(CardCore.Attribute.CounterRules.DivineShieldCounter) == 0,
                   "两层先后耗尽");
            Attribute.KeywordRules.ApplyDamage(p1, twin, 4, false);
            Assert(twin.GetLife() == 5, "层耗尽后正常受伤");

            var tough = Make(p2, 1, 9);
            tough.AddCounters(CardCore.Attribute.CounterRules.ToughnessCounter, 1); // 坚韧 1 层
            var hitter = Make(p1, 4, 9);
            GameActions.DeclareAttack(core, p1, hitter, tough);
            GameActions.DrainStack(core);
            Assert(tough.GetLife() == 6
                   && tough.GetCounterCount(CardCore.Attribute.CounterRules.ToughnessCounter) == 0,
                   "坚韧：最终伤害 −1（9 −3 = 6），1 层生效一次即清零（减半 floor）");

            // 重挂 2 层（可叠加口径；上一击已将 1 层清零）
            tough.AddCounters(CardCore.Attribute.CounterRules.ToughnessCounter, 2);
            Attribute.KeywordRules.ApplyDamage(p1, tough, 4, false);
            Assert(tough.GetLife() == 4
                   && tough.GetCounterCount(CardCore.Attribute.CounterRules.ToughnessCounter) == 1,
                   "双坚韧（叠加 2 层）：伤害 −2，生效后减半余 1 层");

            var plated = Make(p2, 1, 5);
            plated.AddCounters(CardCore.Attribute.KeywordRules.ArmorCounter, 3);
            CardCore.Attribute.KeywordRules.ApplyDamage(p1, plated, 2, false);
            Assert(plated.GetLife() == 5 && plated.GetCounterCount(CardCore.Attribute.KeywordRules.ArmorCounter) == 1,
                   "护甲指示物：吸收 2 点后剩余 1（生命未动）");

            // ---- 15. 不灭 / 复生（毁灭原子已删除——用吞噬路径验证不灭拦截） ----
            ResetField(); // 段界清场
            var eternal = Make(p1, 2, 5, "Indestructible");
            var devourDef = new EffectDefinition
            {
                Id = "VERIFY_DEVOUR",
                TriggerTiming = TriggerTiming.Activate_Active,
                Effects = new List<AtomicEffectInstance> { new AtomicEffectInstance { Type = AtomicEffectType.Devour } },
            };
            core.StackEngine.GetExecutor().ExecuteAsync(new EffectInstance
            {
                Definition = devourDef,
                Source = p1,
                Controller = p1,
                Targets = new List<Entity> { eternal },
            }, skipElementCost: true).Forget();
            Assert(eternal.IsAlive, "不灭：吞噬类消灭无效（毁灭原子已删除，不灭拦消灭族）");

            var phoenix = Make(p1, 2, 5, CardCore.Attribute.CounterRules.RebornCounter);
            phoenix.IsAlive = false;
            Assert(CardCore.Attribute.KeywordRules.TryReborn(phoenix)
                   && phoenix.IsAlive && phoenix.GetLife() == 1 && !phoenix.IsTapped()
                   && phoenix.GetCounterCount(CardCore.Attribute.CounterRules.RebornCounter) == 0,
                   "复生：1 血回场（2026-10-19 起不横置）、消耗 1 层");

            // ---- 15b. 死亡决策表（DeathRules）：死因×护盾 定案断言（DeathCause.Poison 列已随
            //      2026-10-08 剧毒转关键词删除——消灭统一走 DestroyEffect） ----
            var stoneGiant = Make(p2, 2, 9, "Indestructible");
            Assert(!CardCore.Attribute.DeathRules.TryKill(stoneGiant, CardCore.Attribute.DeathCause.DestroyEffect, p1, core.ZoneManager)
                   && stoneGiant.IsAlive,
                   "决策表：不灭拦截摧毁效果死因");
            var ironReborn = Make(p2, 2, 9, CardCore.Attribute.CounterRules.RebornCounter);
            Assert(!CardCore.Attribute.DeathRules.TryKill(ironReborn, CardCore.Attribute.DeathCause.DestroyEffect, p1, core.ZoneManager)
                   && ironReborn.IsAlive && ironReborn.GetLife() == 1,
                   "决策表：复生对摧毁效果死因生效（消耗回场，TryKill 返回未死）");

            // ---- 15b2. 角色复生/角色潜行（2026-10-19 Entity 化：表行去 NoRole 后对角色全通） ----
            int p2LifeBackup = p2.Life;
            p2.AddCounters(CardCore.Attribute.CounterRules.RebornCounter, 1);
            p2.Life = 0;
            core.SBAEngine.ExecuteAll();
            Assert(p2.Life == 1 && !core.IsGameOver
                   && p2.GetCounterCount(CardCore.Attribute.CounterRules.RebornCounter) == 0,
                   "角色复生：归零宣判前消耗 1 层回 1 血，不宣判不终局");
            p2.Life = p2LifeBackup;

            p2.AddCounters(CardCore.Attribute.CounterRules.StealthCounter, 1);
            Assert(!core.CombatSystem.CanAttackTarget(phoenix, p2, p1),
                   "角色潜行：持层期间不可被指定为攻击目标");
            var shown = TargetResolver.ExcludeUnselectable(new List<Entity> { phoenix, p2 }, p1);
            Assert(!shown.Contains(p2) && shown.Contains(phoenix),
                   "角色潜行：选择域隐藏对方角色、己方单位照常可选");
            p2.AddCounters(CardCore.Attribute.CounterRules.StealthCounter, -1);

            // ---- 15c. 神佑已删（2026-10-13 定案）----
            // 锚①：角色构造/Reset 均不再注入神佑；锚②：净化打在角色上照常清 Status 轨
            //（原「神佑抗净化」锚随机制删除，角色侧净化覆盖保留）。
            Assert(!p1.HasKeyword("DivineProtection") && !p2.HasKeyword("DivineProtection"),
                   "神佑已删：角色不再默认持有（字面量锚防意外复活）");
            p2.Life = 30;
            p2.AddKeyword(CardCore.Attribute.KeywordRules.Taunt, CardCore.KeywordLane.Status);
            new CardCore.Attribute.Handlers.PurifyHandler().Execute(
                new CardCore.AtomicEffectInstance { Type = CardCore.AtomicEffectType.Purify },
                new CardCore.EffectExecutionContext
                {
                    Source = p1, Controller = p1,
                    Targets = new List<Entity> { p2 },
                    ZoneManager = core.ZoneManager
                });
            Assert(!p2.HasKeyword(CardCore.Attribute.KeywordRules.Taunt),
                   "净化打在角色上：Status 轨照常清（神佑豁免已随机制删除）");

            // ---- 15d. 死亡归因全链路（2026-09-07 定案：死因/死亡来源/伤害来源/效果来源） ----
            // ① 生命流失：即时决策表死亡（非伤害、不可防止），Cause=LifeLoss、死亡来源=效果来源
            var drainSource = Make(p1, 4, 4);
            var drainVictim = Make(p2, 2, 3);
            CardDestroyEvent drainEvt = null;
            void OnDrainKill(CardDestroyEvent e) { if (e.DestroyedCard == drainVictim) drainEvt = e; }
            EventManager.Instance.Subscribe<CardDestroyEvent>(OnDrainKill);
            new CardCore.Attribute.Handlers.LifeLossHandler().Execute(
                new CardCore.AtomicEffectInstance { Type = CardCore.AtomicEffectType.LifeLoss, Value = 3 },
                new CardCore.EffectExecutionContext
                {
                    Source = drainSource, Controller = p1,
                    Targets = new List<Entity> { drainVictim },
                    ZoneManager = core.ZoneManager
                });
            Assert(drainEvt != null && drainEvt.Cause == CardCore.Attribute.DeathCause.LifeLoss
                   && drainEvt.Source == drainSource && !drainVictim.IsAlive
                   && core.ZoneManager.GetCards(p2, Zone.Graveyard).Contains(drainVictim),
                   "生命流失：即时决策表死亡（Cause=LifeLoss，死亡来源=效果来源，不走伤害管线）");

            // ② 伤害致死：SBA 收尸归因不丢（DamageLethal + 伤害来源）
            var killer = Make(p1, 5, 5);
            var slain = Make(p2, 2, 3);
            CardDestroyEvent combatEvt = null;
            void OnCombatKill(CardDestroyEvent e) { if (e.DestroyedCard == slain) combatEvt = e; }
            EventManager.Instance.Subscribe<CardDestroyEvent>(OnCombatKill);
            CardCore.Attribute.KeywordRules.ApplyDamage(killer, slain, 3, false);
            core.SBAEngine.CheckAndExecute();
            Assert(combatEvt != null && combatEvt.Cause == CardCore.Attribute.DeathCause.DamageLethal
                   && combatEvt.Source == killer && !slain.IsAlive
                   && core.ZoneManager.GetCards(p2, Zone.Graveyard).Contains(slain),
                   "伤害致死：归因随尸体留档，SBA 送墓上事件（Cause=DamageLethal，死亡来源=伤害来源）");

            // ③ 剧毒关键词消灭：死亡来源=剧毒持有者（伤害来源归因；Cause=DestroyEffect 消灭口径）
            var venomKiller = Make(p1, 3, 3, "Venom");
            var venomVictim = Make(p2, 2, 5);
            CardDestroyEvent venomEvt = null;
            void OnVenomKill(CardDestroyEvent e) { if (e.DestroyedCard == venomVictim) venomEvt = e; }
            EventManager.Instance.Subscribe<CardDestroyEvent>(OnVenomKill);
            CardCore.Attribute.KeywordRules.ApplyDamage(venomKiller, venomVictim, 2, true);
            Assert(venomEvt != null && venomEvt.Cause == CardCore.Attribute.DeathCause.DestroyEffect
                   && venomEvt.Source == venomKiller,
                   "剧毒消灭：Cause=DestroyEffect，死亡来源=剧毒持有者（伤害来源归因）");

            // ④ 减益致死：削减归零标死，指示物来源已登记（GetCounterSource 公共查询）
            var debuffer = Make(p1, 4, 4);
            var withered = Make(p2, 2, 3);
            CardCore.Attribute.CounterRules.AddStatCounter(withered, CardCore.Attribute.CounterRules.LifeDownCounter, 3, debuffer);
            Assert(!withered.IsAlive
                   && withered.GetCounterSource(CardCore.Attribute.CounterRules.LifeDownCounter) == debuffer,
                   "减益致死：削减归零标死，来源登记（SBA 收尸时归因到施加方）");

            // ⑤ 流失扣上限边界（2026-09-14 代价原子化：LifeLoss 原子=支付载体，无 CanPay 门槛）：
            // 可扣到恰好归零（归零=正常死亡交生命判定收尾）；新玩家夹具不污染共享 p2
            var plEdge = new Player("VERIFY_KW_LIFE_EDGE", 30);
            CardCore.Attribute.EffectHandlerRegistry.ExecuteEffectAsync(
                new AtomicEffectInstance { Type = AtomicEffectType.LifeLoss, Value = 30 },
                new EffectExecutionContext { Controller = plEdge, Source = plEdge, Targets = new List<Entity> { plEdge } })
                .GetAwaiter().GetResult();
            Assert(plEdge.MaxHealth == 0 && plEdge.Life == 0,
                   "流失原子：可扣到恰好归零（归零=正常死亡交生命判定收尾）");

            // ---- 16. 再生（2026-09-13 全额定案：己方准备阶段恢复全部生命；成长 2026-10-08 机制删除） ----
            for (int i = 0; i < 3; i++) core.ZoneManager.GetZoneContainer(p1).Add(new Card { ID = "VERIFY_KW_DECK" }, Zone.Deck);
            var regrow = Make(p1, 2, 4, "Regeneration");
            CardCore.Attribute.KeywordRules.ApplyDamage(p2, regrow, 2, false);
            EventManager.Instance.Publish(new TurnStartEvent { TurnPlayer = p1, TurnNumber = 300 });
            core.RunStandbyAutomation(p1, 300); // 2026-10-26 精准切分：自动化迁准备阶段——合成事件须显式补调
            Assert(regrow.GetLife() == 4 && regrow.GetPower() == 2,
                   "再生恢复全部生命（2→满4）——准备阶段维护");

            // ---- 17. 法术护盾（效果指定）（辟邪断言已随 2026-10-13 扰魔整体退役删除——
            //      原 EffectTargetValidator 为死基建从未接通，断言测的是死代码） ----
            var foe1 = Make(p1, 2, 2);

            var warded = Make(p2, 2, 5, CardCore.Attribute.CounterRules.SpellShieldCounter); // 印刷路径入场挂 1 层
            var targets = new List<Entity> { warded, p2 };
            CardCore.Attribute.KeywordRules.ConsumeSpellShields(targets, foe1);
            Assert(targets.Count == 1 && targets[0] == p2
                   && warded.GetCounterCount(CardCore.Attribute.CounterRules.SpellShieldCounter) == 0,
                   "法术护盾：成为对手效果目标时移出目标并消耗 1 层（印刷份=1 层归零）");

            warded.AddCounters(CardCore.Attribute.CounterRules.SpellShieldCounter, 2); // 追加 2 层（多层=多次抵消）
            // 每次消耗=一次独立效果指定目标——逐次重建目标列表（共享列表会在首次消耗时被移出，后两次空转）
            for (int volleyIdx = 0; volleyIdx < 3; volleyIdx++)
            {
                var volley = new List<Entity> { warded };
                CardCore.Attribute.KeywordRules.ConsumeSpellShields(volley, foe1);
                if (volley.Count == 1) break; // 第 3 次（层已尽）照常作用——目标保留
            }
            int layersAfterVolley = warded.GetCounterCount(CardCore.Attribute.CounterRules.SpellShieldCounter);
            Assert(layersAfterVolley == 0,
                   $"法术护盾多层：每层抵消一次——2 层恰挡 2 次，第 3 次照常作用（实际层余 {layersAfterVolley}）");

            warded.AddCounters(CardCore.Attribute.CounterRules.SpellShieldCounter, 1);
            var friendly = new List<Entity> { warded };
            CardCore.Attribute.KeywordRules.ConsumeSpellShields(friendly, warded);
            Assert(friendly.Count == 1
                   && warded.GetCounterCount(CardCore.Attribute.CounterRules.SpellShieldCounter) == 1,
                   $"法术护盾：友方效果不消耗（仅对手效果触发）（实际 volley={friendly.Count} 层余={warded.GetCounterCount(CardCore.Attribute.CounterRules.SpellShieldCounter)}）");

            // ---- 18. 单向属性指示物：加时回写、换区清除时反向回写 ----
            ResetField(); // 段界清场
            var buffed = Make(p2, 2, 5);
            CardCore.Attribute.CounterRules.AddStatCounter(buffed, CardCore.Attribute.CounterRules.PowerUpCounter, 3);
            CardCore.Attribute.CounterRules.AddStatCounter(buffed, CardCore.Attribute.CounterRules.LifeUpCounter, 2);
            Assert(buffed.GetPower() == 5 && buffed.GetMaxLife() == 7 && buffed.GetLife() == 7,
                   "单向属性指示物：攻击力+3层、生命值+2层（上限与当前同加）");
            core.ZoneManager.MoveCard(buffed, p2, Zone.Battlefield, Zone.Hand); // 弹回手牌 → 指示物清除
            Assert(buffed.GetCounterCount(CardCore.Attribute.CounterRules.PowerUpCounter) == 0
                   && buffed.GetCounterCount(CardCore.Attribute.CounterRules.LifeUpCounter) == 0
                   && buffed.GetPower() == 2 && buffed.GetMaxLife() == 5,
                   "换区清除：攻击力/生命值指示物离场消失（反向回写）");

            // ---- 18b. 削减类归零：生命值减少层到 0 标死（送墓交 SBA） ----
            var frail = Make(p2, 2, 3);
            CardCore.Attribute.CounterRules.AddStatCounter(frail, CardCore.Attribute.CounterRules.LifeDownCounter, 3);
            Assert(!frail.IsAlive, "生命值减少层：上限归零标死（SBA 收尸）");

            // ---- 18c. ±1/+1 与虚弱/鼓舞（同一路径：施加 ±1 层） ----
            var pmTwin = Make(p2, 2, 4); // twin 命名避让同方法既有变量
            CardCore.Attribute.CounterRules.AddStatCounter(pmTwin, CardCore.Attribute.CounterRules.MinusOneCounter, 2);
            CardCore.Attribute.CounterRules.AddStatCounter(pmTwin, CardCore.Attribute.CounterRules.PlusOneCounter, 1);
            Assert(pmTwin.GetPower() == 1 && pmTwin.GetMaxLife() == 3,
                   "±1 层对消共存：-1/-1×2 与 +1/+1×1（净 -1/-1）");

                // ---- 18d. 易损（2026-10-09 生效自减；2026-11-09 双改版：全部移除 + 移防护层后）：
                //      受到伤害每层 +1（防护层后放大剩余量——完全挡住不消耗）；退出衰退族（无回合末倒数） ----
                var brittle = Make(p2, 2, 15);
                brittle.AddCounters(CardCore.Attribute.CounterRules.VulnerableCounter, 2);
                Attribute.KeywordRules.ApplyDamage(p1, brittle, 3, false);
                Assert(brittle.GetLife() == 10
                       && brittle.GetCounterCount(CardCore.Attribute.CounterRules.VulnerableCounter) == 0,
                       "易损：3 伤 + 2 层 = 5 伤（防护层后放大剩余量），生效后全部移除（2→0）");
                CardCore.Attribute.CounterRules.OnTurnEnd(p2, core.ZoneManager);
                Assert(brittle.GetCounterCount(CardCore.Attribute.CounterRules.VulnerableCounter) == 0,
                       "易损：持有者回合末不倒数（生效自减档——已退出衰退族；施加方回合末同样不结算）");
                Attribute.KeywordRules.ApplyDamage(p1, brittle, 3, false);
                Assert(brittle.GetLife() == 7, "易损第二击：层已全部移除 → 裸伤 3（无放大）");

            // ---- 18d-1. 易损移位（2026-11-09 定案：放大口移防护层后）——完全挡住不放大、不消耗层 ----
            ResetField(); // 段界清场（P11 容量 9——18d 遗留 + 本段新卡）
            var shieldedBrittle = Make(p2, 2, 15, CardCore.Attribute.CounterRules.DivineShieldCounter);
            shieldedBrittle.AddCounters(CardCore.Attribute.CounterRules.VulnerableCounter, 2);
            Attribute.KeywordRules.ApplyDamage(p1, shieldedBrittle, 3, false);
            Assert(shieldedBrittle.GetLife() == 15
                   && shieldedBrittle.GetCounterCount(CardCore.Attribute.CounterRules.VulnerableCounter) == 2
                   && shieldedBrittle.GetCounterCount(CardCore.Attribute.CounterRules.DivineShieldCounter) == 0,
                   "易损移位：圣盾全挡 → 不放大、易损层保留（圣盾照常消耗）");
            Attribute.KeywordRules.ApplyDamage(p1, shieldedBrittle, 3, false);
            Assert(shieldedBrittle.GetLife() == 10
                   && shieldedBrittle.GetCounterCount(CardCore.Attribute.CounterRules.VulnerableCounter) == 0,
                   "易损移位：后续无盾 3 伤 + 2 层 = 5 伤，生效后全部移除");

            // ---- 18d-2. 破败仪典（2026-11-09 新增，改写族·表行 91f35ec1）：受光环影响一方获得的护甲
            //      转换为等量易损（易损生效全移已是全局默认——18d 口径，光环只剩转换一半）。仅对方档
            //      （scope=Opponent）验证单侧命中——控制者侧护甲照常落账。----
            ResetField(); // 段界清场（P11 容量 9）
            var decayVictim = Make(p2, 2, 15);
            var decayOwnUnit = Make(p1, 2, 15);
            decayVictim.AddCounters(CardCore.Attribute.KeywordRules.ArmorCounter, 2); // 光环激活前的存量护甲
            var decayCarrier = new CardWrapper(new CardData
            {
                ID = "VERIFY_DECAY_CARRIER", CardName = "破败验证载体", Supertype = Cardtype.Enchantment, Durability = 9,
            });
            decayCarrier.SetController(p1);
            Assert(core.ZoneManager.TryAddToBattlefield(decayCarrier, p1), "破败仪典段：载体入场成功");
            var decaySaveId = CardCore.RuleAuraSystem.IsActive(CardCore.RuleAuraComponents.ArmorDecay)
                ? CardCore.RuleAuraComponents.ArmorDecay : null;
            var decaySaveCarrier = CardCore.RuleAuraSystem.CarrierOf(CardCore.RuleAuraComponents.ArmorDecay);
            var decaySaveCtrl = CardCore.RuleAuraSystem.ControllerOf(CardCore.RuleAuraComponents.ArmorDecay);
            CardCore.RuleAuraSystem.Activate(CardCore.RuleAuraComponents.ArmorDecay, decayCarrier, p1,
                (int)CardCore.RuleAuraScope.Opponent); // 仅对方档：p2 命中、p1 不命中
            try
            {
                int decayRemoved = decayVictim.AddCounters(CardCore.Attribute.KeywordRules.ArmorCounter, -1);
                Assert(decayRemoved == -1
                       && decayVictim.GetCounterCount(CardCore.Attribute.KeywordRules.ArmorCounter) == 1,
                       "破败仪典：存量护甲负量移除照常（返回 -1、不拦不转换）");
                int decayDelta = decayVictim.AddCounters(CardCore.Attribute.KeywordRules.ArmorCounter, 3);
                Assert(decayDelta == 0
                       && decayVictim.GetCounterCount(CardCore.Attribute.KeywordRules.ArmorCounter) == 1
                       && decayVictim.GetCounterCount(CardCore.Attribute.CounterRules.VulnerableCounter) == 3,
                       "破败仪典：受影响单位获得护甲 3 层 → 转换等量易损（返回 0、护甲不涨）");
                decayOwnUnit.AddCounters(CardCore.Attribute.KeywordRules.ArmorCounter, 2);
                Assert(decayOwnUnit.GetCounterCount(CardCore.Attribute.KeywordRules.ArmorCounter) == 2
                       && decayOwnUnit.GetCounterCount(CardCore.Attribute.CounterRules.VulnerableCounter) == 0,
                       "破败仪典（仅对方档）：控制者侧护甲照常落账（ScopeHits 单侧命中）");
                // 易损放大在防护层后（2026-11-09 移位）：3 → 存量护甲 1 吸 1 → 剩 2 + 易损 3 = 5 → 落 5 血（15→10）；易损全部移除（非减半）
                Attribute.KeywordRules.ApplyDamage(p1, decayVictim, 3, false);
                Assert(decayVictim.GetLife() == 10
                       && decayVictim.GetCounterCount(CardCore.Attribute.KeywordRules.ArmorCounter) == 0
                       && decayVictim.GetCounterCount(CardCore.Attribute.CounterRules.VulnerableCounter) == 0,
                       "破败仪典：护甲先吸 1 → 易损放大剩余 2+3=5 → 落 5 血，生效后全部移除（防护层后口径）");
            }
            finally
            {
                CardCore.RuleAuraSystem.Reset(); // 还原（同丰盈段惯例：整槽清空+按存档回挂）
                if (decaySaveId != null) CardCore.RuleAuraSystem.Activate(decaySaveId, decaySaveCarrier, decaySaveCtrl);
                core.ZoneManager.MoveCard(decayCarrier, p1, Zone.Battlefield, Zone.Graveyard);
            }

            // ---- 18d-3. 暴击（2026-11-09 新增，红5 表行「暴击」879fe2c2）：战斗伤害翻倍——
            //      延时保护后、防护层前（圣盾/护甲吸收翻倍后的量）；效果伤害不吃暴击 ----
            ResetField(); // 段界清场（P11 容量 9——本段 5 张新卡）
            var critter = Make(p1, 3, 5, CardCore.Attribute.KeywordRules.Crit);
            var critPrey = Make(p2, 1, 15);
            Attribute.KeywordRules.ApplyDamage(critter, critPrey, 3, false);
            Assert(critPrey.GetLife() == 12, "暴击：效果伤害（isCombat=false）不翻倍");
            Attribute.KeywordRules.ApplyDamage(critter, critPrey, 3, true);
            Assert(critPrey.GetLife() == 6, "暴击：战斗伤害翻倍（12 − 3×2 = 6）");
            var critWarded = Make(p2, 1, 15, CardCore.Attribute.CounterRules.DivineShieldCounter);
            Attribute.KeywordRules.ApplyDamage(critter, critWarded, 3, true);
            Assert(critWarded.GetLife() == 15
                   && critWarded.GetCounterCount(CardCore.Attribute.CounterRules.DivineShieldCounter) == 0,
                   "暴击×圣盾：圣盾挡下翻倍后的 6 点（防护层前翻倍）、照常消耗");
            var critArmored = Make(p2, 1, 15);
            critArmored.AddCounters(CardCore.Attribute.KeywordRules.ArmorCounter, 2);
            Attribute.KeywordRules.ApplyDamage(critter, critArmored, 3, true);
            Assert(critArmored.GetLife() == 11
                   && critArmored.GetCounterCount(CardCore.Attribute.KeywordRules.ArmorCounter) == 0,
                   "暴击×护甲：护甲吸收翻倍后的量（3×2−2=4 落血，15→11）");
            var critBrittle = Make(p2, 1, 15);
            critBrittle.AddCounters(CardCore.Attribute.CounterRules.VulnerableCounter, 2);
            Attribute.KeywordRules.ApplyDamage(critter, critBrittle, 3, true);
            Assert(critBrittle.GetLife() == 7,
                   "暴击×易损：先翻倍 3×2=6，防护层后易损再 +2 → 8 伤（15→7）");
            ResetField(); // 段界清场（本段 5 张常驻卡——还后续段原始起步容量）

            // ---- 18e. 紊乱原子：附加后不能以玩家为目标（攻击与效果同口径） ----
            var dizzy = Make(p2, 2, 5);
            dizzy.AddCounters(CardCore.Attribute.KeywordRules.RushSicknessCounter, 1);
            Assert(!combat.CanAttackTarget(dizzy, p1, p1), "紊乱：持有者不能以玩家为目标（攻击侧）");
            // 持有者侧结算域：dizzy 属 p2，紊乱在 p2 回合末消退——须传持有者
            CardCore.Attribute.CounterRules.OnTurnEnd(p2, core.ZoneManager);
            Assert(dizzy.GetCounterCount(CardCore.Attribute.KeywordRules.RushSicknessCounter) == 0,
                   "紊乱：持续到回合结束消退");

            // ---- 18f. 属性赋予路由（2026-10-08 来源分轨退役）：Modify* 一律设置轨直写——
            //      生物来源也不例外；临时性（换区清层）由指示物原子族（AddPowerUp 等）显式表达 ----
            var trackA = Make(p2, 2, 5);
            var trackDefF = new EffectDefinition
            {
                Id = "VERIFY_TRACK_F",
                TriggerTiming = TriggerTiming.Activate_Active,
                Effects = new List<AtomicEffectInstance> { new AtomicEffectInstance { Type = AtomicEffectType.ModifyPower, Value = 2 } },
            };
            core.StackEngine.GetExecutor().ExecuteAsync(new EffectInstance
            {
                Definition = trackDefF,
                Source = trackA, // 生物来源（来源分轨退役——与角色来源同落设置轨）
                Controller = p2,
                Targets = new List<Entity> { trackA },
            }, skipElementCost: true).Forget();
            Assert(trackA.GetCounterCount(CardCore.Attribute.CounterRules.PowerUpCounter) == 0 && trackA.GetPower() == 4,
                   "属性路由：生物来源攻+2 同走设置轨直写（无指示物层——来源分轨 2026-10-08 退役）");

            // ---- 18g. 三轨路由·法术轨（设置类）：Source=角色 → 永久直改、跨区保留、净化不清 ----
            var trackB = Make(p2, 2, 5);
            var trackDefG = new EffectDefinition
            {
                Id = "VERIFY_TRACK_G",
                TriggerTiming = TriggerTiming.Activate_Active,
                Effects = new List<AtomicEffectInstance> { new AtomicEffectInstance { Type = AtomicEffectType.ModifyPower, Value = 3 } },
            };
            core.StackEngine.GetExecutor().ExecuteAsync(new EffectInstance
            {
                Definition = trackDefG,
                Source = p1, // 魔法卡来源（规则②：所有魔法卡的效果来源=角色）
                Controller = p1,
                Targets = new List<Entity> { trackB },
            }, skipElementCost: true).Forget();
            Assert(trackB.GetCounterCount(CardCore.Attribute.CounterRules.PowerUpCounter) == 0
                   && trackB.GetPower() == 5,
                   "三轨·法术轨：角色来源攻+3 永久直改（无任何指示物层）");
            core.ZoneManager.MoveCard(trackB, p2, Zone.Battlefield, Zone.Hand);
            Assert(trackB.GetPower() == 5,
                   "三轨·法术轨：设置类跨区保留（弹回手不清）");
            CardCore.Attribute.CounterRules.PurgeAll(trackB); // 净化口径全清指示物
            Assert(trackB.GetPower() == 5,
                   "三轨·法术轨：净化清不掉设置类属性（直改无层可回滚，视同本体）");
            var trackB2 = Make(p2, 2, 5);
            var trackDefG2 = new EffectDefinition
            {
                Id = "VERIFY_TRACK_G2",
                TriggerTiming = TriggerTiming.Activate_Active,
                Effects = new List<AtomicEffectInstance> { new AtomicEffectInstance { Type = AtomicEffectType.ModifyLife, Value = -5 } },
            };
            core.StackEngine.GetExecutor().ExecuteAsync(new EffectInstance
            {
                Definition = trackDefG2,
                Source = p1,
                Controller = p1,
                Targets = new List<Entity> { trackB2 },
            }, skipElementCost: true).Forget();
            Assert(!trackB2.IsAlive && trackB2.GetCounterCount(CardCore.Attribute.CounterRules.LifeDownCounter) == 0,
                   "三轨·法术轨：设置类生命直减归零标死（无层，交 SBA）");

            // ---- 18h. 直写跨区保留（Permanent 声明已无特殊落点——换区清层口径随来源分轨退役）----
            var trackC = Make(p2, 2, 5);
            var trackDefH = new EffectDefinition
            {
                Id = "VERIFY_TRACK_H",
                TriggerTiming = TriggerTiming.Activate_Active,
                Duration = DurationType.Permanent,
                Effects = new List<AtomicEffectInstance> { new AtomicEffectInstance { Type = AtomicEffectType.ModifyPower, Value = 2 } },
            };
            core.StackEngine.GetExecutor().ExecuteAsync(new EffectInstance
            {
                Definition = trackDefH,
                Source = trackC,
                Controller = p2,
                Targets = new List<Entity> { trackC },
            }, skipElementCost: true).Forget();
            Assert(trackC.GetCounterCount(CardCore.Attribute.CounterRules.PowerUpCounter) == 0
                   && trackC.GetPower() == 4,
                   "直写单落点：Permanent 声明同走设置轨（无指示物层——四永久层 id 已删）");
            core.ZoneManager.MoveCard(trackC, p2, Zone.Battlefield, Zone.Hand);
            Assert(trackC.GetCounterCount(CardCore.Attribute.CounterRules.PowerUpCounter) == 0
                   && trackC.GetPower() == 4 && trackC.GetMaxLife() == 5,
                   "直写单落点：跨区保留（弹回手不清——永久直改视同本体）");

            // ---- 19. 净化（2026-09-09 语义重定义：变回生物原有状态）----
            // 关键词轨矩阵：Printed（卡面本体）保留 / Setting（魔法卡赋予视同本体）保留 /
            // Temp（临时）清；指示物全清（含生效自减层）。
            var cursed = Make(p2, 3, 6, "Taunt"); // Taunt=Printed（CardWrapper 构造注入）
            cursed.AddCounters(CardCore.Attribute.CounterRules.StealthCounter, 1);       // 潜行指示物（2026-10-08 指示物化）
            cursed.AddKeyword("Indestructible", CardCore.KeywordLane.Temp);             // 临时轨（净化清）
            cursed.AddKeyword("Vigilance", CardCore.KeywordLane.Setting);               // 设置类（视同本体）
            cursed.AddCounters(CardCore.Attribute.CounterRules.ToxinCounter, 2); // 毒素统一档：无时钟（Turns 字段已随 2026-10-08 CounterSpec 改造删除）
            cursed.AddCounters(CardCore.Attribute.KeywordRules.ArmorCounter, 3);
            var purifyDef = new EffectDefinition
            {
                Id = "VERIFY_PURIFY",
                TriggerTiming = TriggerTiming.Activate_Active,
                Effects = new List<AtomicEffectInstance> { new AtomicEffectInstance { Type = AtomicEffectType.Purify } },
            };
            core.StackEngine.GetExecutor().ExecuteAsync(new EffectInstance
            {
                Definition = purifyDef,
                Source = p1,
                Controller = p1,
                Targets = new List<Entity> { cursed },
            }, skipElementCost: true).Forget();
            Assert(cursed.HasKeyword("Taunt") && cursed.HasKeyword("Vigilance")
                   && cursed.GetCounterCount(CardCore.Attribute.CounterRules.StealthCounter) == 0
                   && !cursed.HasKeyword("Indestructible"),
                   "净化轨矩阵：本体与设置类保留，临时赋予清除（潜行已指示物化随层清）");
            Assert(cursed.GetCounterCount(CardCore.Attribute.CounterRules.ToxinCounter) == 0
                   && cursed.GetCounterCount(CardCore.Attribute.KeywordRules.ArmorCounter) == 0,
                   "净化：全部指示物清空（含永久类，属性层反向回写）");

            // ---- 19b. 换区清除：Temp 关键词轨换区清、Printed 留；生效自减指示物（Exception）换区不清 ----
            var zoneKw = Make(p2, 2, 4, "Taunt");
            zoneKw.AddKeyword("Vigilance", CardCore.KeywordLane.Temp); // 临时轨占位
            zoneKw.AddCounters(CardCore.Attribute.CounterRules.StealthCounter, 1); // 潜行指示物层（生效自减档）
            zoneKw.AddKeyword("FirstStrike", CardCore.KeywordLane.Setting); // 设置轨（视同本体，换区不清）
            core.ZoneManager.MoveCard(zoneKw, p2, Zone.Battlefield, Zone.Hand);
            Assert(!zoneKw.HasKeyword("Vigilance")
                   && zoneKw.GetCounterCount(CardCore.Attribute.CounterRules.StealthCounter) == 1
                   && zoneKw.HasKeyword("Taunt") && zoneKw.HasKeyword("FirstStrike"),
                   "换区清除：Temp 轨清除；潜行指示物层换区不清（生效自减档·2026-10-08 晚补裁决）；"
                   + "Printed 与 Setting（换区不清档）保留");

            // ---- 19c.（已删 2026-10-08：生物轨永久档属性层随四永久 id 退役——换区保留语义并入 18g 设置轨锚） ----

            // ---- 19d. 入场刷新（2026-09-09 定案；2026-10-08 圣盾/复生/潜行指示物化）：真实入场补回
            //      被消耗的卡面消耗项（关键词走 Printed 差集、指示物走补 1 层）；
            //      复生（原地留场）与控制权变更（容器直移）不经统一出口，天然不触发 ----
            var refreshShield = Make(p2, 2, 5, CardCore.Attribute.CounterRules.DivineShieldCounter);
            CardCore.Attribute.KeywordRules.ApplyDamage(p1, refreshShield, 1, false);
            Assert(refreshShield.GetCounterCount(CardCore.Attribute.CounterRules.DivineShieldCounter) == 0
                   && refreshShield.GetLife() == 5,
                   "入场刷新前置：圣盾挡一次伤害后消耗（层数归零、伤害被完全挡下）");
            core.ZoneManager.MoveCard(refreshShield, p2, Zone.Battlefield, Zone.Hand); // 弹回手
            core.ZoneManager.TryAddToBattlefield(refreshShield, p2);                   // 真实入场（统一出口）
            Assert(refreshShield.GetCounterCount(CardCore.Attribute.CounterRules.DivineShieldCounter) == 1,
                   "入场刷新：弹回重打补回被消耗的卡面圣盾（印刷路径=补 1 层指示物）");

            var refreshReborn = Make(p2, 2, 5, CardCore.Attribute.CounterRules.RebornCounter);
            CardCore.Attribute.DeathRules.TryKill(refreshReborn, CardCore.Attribute.DeathCause.DamageLethal, p1, core.ZoneManager);
            Assert(refreshReborn.IsAlive && refreshReborn.GetLife() == 1
                   && refreshReborn.GetCounterCount(CardCore.Attribute.CounterRules.RebornCounter) == 0,
                   "复生不触发刷新：死亡替代原地留场（不经入场口），消耗后不自我补回（无无限复生）");

            var refreshStolen = Make(p1, 2, 5, CardCore.Attribute.CounterRules.DivineShieldCounter);
            CardCore.Attribute.KeywordRules.ApplyDamage(p2, refreshStolen, 1, false); // 消耗圣盾
            new CardCore.Attribute.Handlers.GainControlHandler().Execute(
                new CardCore.AtomicEffectInstance { Type = CardCore.AtomicEffectType.GainControl },
                new CardCore.EffectExecutionContext
                {
                    Source = p2, Controller = p2,
                    Targets = new List<Entity> { refreshStolen },
                    ZoneManager = core.ZoneManager
                });
            Assert(refreshStolen.GetCounterCount(CardCore.Attribute.CounterRules.DivineShieldCounter) == 0,
                   "控制权变更不触发刷新：场内迁移（容器直移+补发事件）不补回消耗项（偷取不白得圣盾）");

            // ---- 20. 沉默指示物：持有者不可发动主动效果（主动能力路径） ----
            ResetField(); // 段界清场（P11 容量 9：18-19 段 p2 侧已累计 10+——沉默/无效/摧毁段独立清场起步）
            var silenced = Make(p1, 2, 5);
            var activatable = new EffectDefinition
            {
                Id = "VERIFY_SILENCE_ABILITY",
                TriggerTiming = TriggerTiming.Activate_Active,
                ActivationType = EffectActivationType.Voluntary,
                Effects = new List<AtomicEffectInstance> { new AtomicEffectInstance { Type = AtomicEffectType.Tap } },
            };
            var executor = core.StackEngine.GetExecutor();
            bool beforeSilence = executor.CanActivate(activatable, silenced, p1, p1, PhaseType.Main, 1);
            silenced.AddCounters(CardCore.Attribute.CounterRules.SilenceCounter, 1);
            bool afterSilence = executor.CanActivate(activatable, silenced, p1, p1, PhaseType.Main, 1);
            Assert(beforeSilence && !afterSilence,
                   "沉默：持有者主动能力被封锁（出牌不受限——PlayCard 不经此门）");

            // ---- 20b. 无效指示物：拦全部触发式（事件匹配后、上栈前——含 OnTakeDamage 族） ----
            var nullified = Make(p2, 2, 9);
            var nullTrigDef = new EffectDefinition
            {
                Id = "VERIFY_NULLIFY_TRIG",
                TriggerTiming = TriggerTiming.OnTakeDamage,
                // 裸构造必补：ActivationType 默认 Voluntary 会进「玩家自愿」待发队列，
                // DrainStack 只自动泵 Automatic（converter 按时点补的正是它）；触发式费用已含卡价
                ActivationType = EffectActivationType.Automatic,
                ElementCostPrepaid = true,
                Effects = new List<AtomicEffectInstance> { new AtomicEffectInstance { Type = AtomicEffectType.DrawCard, Value = 1 } },
            };
            core.TriggerEngine.RegisterEffect(nullTrigDef, nullified, p2);
            nullified.AddCounters(CardCore.Attribute.CounterRules.NullifyCounter, 1, p1);
            int p2HandAtNullify = core.ZoneManager.GetCards(p2, Zone.Hand).Count;
            CardCore.Attribute.KeywordRules.ApplyDamage(p1, nullified, 1, false);
            GameActions.DrainStack(core);
            Assert(core.ZoneManager.GetCards(p2, Zone.Hand).Count == p2HandAtNullify,
                   "无效：非主动（触发式）被拦——受击触发不上栈");
            // 对照：无效消退后同一触发照常上栈结算
            nullified.RemoveCounters(CardCore.Attribute.CounterRules.NullifyCounter, 1);
            CardCore.Attribute.KeywordRules.ApplyDamage(p1, nullified, 1, false);
            GameActions.DrainStack(core);
            Assert(core.ZoneManager.GetCards(p2, Zone.Hand).Count == p2HandAtNullify + 1,
                   "无效消退后：同一触发照常上栈（对照锚，证明 20b 拦截来自无效指示物本身）");

            // ---- 20c. 无效不拦主动（与沉默对照：沉默拦主动、无效拦非主动，互不重叠） ----
            var nullActivated = Make(p1, 2, 5);
            var nullActDef = new EffectDefinition
            {
                Id = "VERIFY_NULLIFY_ACT",
                TriggerTiming = TriggerTiming.Activate_Active,
                ActivationType = EffectActivationType.Voluntary,
                Effects = new List<AtomicEffectInstance> { new AtomicEffectInstance { Type = AtomicEffectType.Tap } },
            };
            nullActivated.AddCounters(CardCore.Attribute.CounterRules.NullifyCounter, 1);
            bool canActUnderNullify = executor.CanActivate(nullActDef, nullActivated, p1, p1, PhaseType.Main, 1);
            Assert(canActUnderNullify,
                   "无效：主动照常可发动（拦截面与沉默互补不重叠）");

            // ---- 20d. 无效不拦伤害管线被动（坚韧/圣盾等非「能力发动」；回合维护再生同口径） ----
            var nullTough = Make(p2, 3, 8);
            nullTough.AddCounters(CardCore.Attribute.CounterRules.ToughnessCounter, 1); // 坚韧 1 层
            nullTough.AddCounters(CardCore.Attribute.CounterRules.NullifyCounter, 1);
            CardCore.Attribute.KeywordRules.ApplyDamage(p1, nullTough, 3, false);
            Assert(nullTough.GetLife() == 6,
                   "无效：伤害管线被动照常（坚韧减伤不受无效影响）");

            // ---- 21. 摧毁：无生命值单位（地牌）出池直送墓地，不走死亡决策表 ----
            Card landTarget = null;
            var p2Pooled = core.ElementPool.GetPooledCards(p2);
            if (p2Pooled.Count > 0)
            {
                landTarget = p2Pooled[0].SourceCard;
            }
            else
            {
                var p2Creature = core.ZoneManager.GetCards(p2, Zone.Hand)
                    .FirstOrDefault(c => CardCore.ElementPoolSystem.CanServeAsLand(c));
                if (p2Creature != null && GameActions.AddToElementPool(core, p2, p2Creature))
                    landTarget = p2Creature;
            }
            if (landTarget != null)
            {
                int graveBefore = core.ZoneManager.GetCards(p2, Zone.Graveyard).Count;
                var smashDef = new EffectDefinition
                {
                    Id = "VERIFY_SMASH",
                    TriggerTiming = TriggerTiming.Activate_Active,
                    Effects = new List<AtomicEffectInstance> { new AtomicEffectInstance { Type = AtomicEffectType.Smash } },
                };
                executor.ExecuteAsync(new EffectInstance
                {
                    Definition = smashDef,
                    Source = p1,
                    Controller = p1,
                    Targets = new List<Entity> { landTarget },
                }, skipElementCost: true).Forget();
                Assert(core.ElementPool.GetPooledCards(p2).All(pc => pc.SourceCard != landTarget)
                       && core.ZoneManager.GetCards(p2, Zone.Graveyard).Count == graveBefore + 1,
                       "摧毁：地牌出池直送墓地（2026-10-11：地牌本就不耗竭——回收后可再入池重选锁色）");
            }
            else
            {
                Debug.LogWarning("[Verify] 跳过摧毁段：p2 无可用地牌（池空且手牌无可入池生物）");
            }

            // ---- 21b. 摧毁×不灭（2026-10-09 三口裁定：剧毒/消灭/摧毁全可被不灭拦）----
            var enchantData = new CardData
            {
                ID = "VERIFY_SMASH_ENCH", CardName = "摧毁标的结界", Supertype = Cardtype.Enchantment, Power = 0, Life = 0,
            };
            var normalEnchant = new CardWrapper(enchantData);
            normalEnchant.SetController(p2);
            Assert(core.ZoneManager.TryAddToBattlefield(normalEnchant, p2), "摧毁段：普通结界入场");

            var rockEnchantData = new CardData
            {
                ID = "VERIFY_SMASH_ROCK", CardName = "不灭结界", Supertype = Cardtype.Enchantment, Power = 0, Life = 0,
            };
            rockEnchantData.Keywords.Add(CardCore.Attribute.KeywordRules.Indestructible);
            var rockEnchant = new CardWrapper(rockEnchantData);
            rockEnchant.SetController(p2);
            Assert(core.ZoneManager.TryAddToBattlefield(rockEnchant, p2), "摧毁段：不灭结界入场");

            var smashDef2 = new EffectDefinition
            {
                Id = "VERIFY_SMASH_INDESTRUCTIBLE",
                TriggerTiming = TriggerTiming.Activate_Active,
                Effects = new List<AtomicEffectInstance> { new AtomicEffectInstance { Type = AtomicEffectType.Smash } },
            };
            executor.ExecuteAsync(new EffectInstance
            {
                Definition = smashDef2,
                Source = p1,
                Controller = p1,
                Targets = new List<Entity> { normalEnchant, rockEnchant },
            }, skipElementCost: true).Forget();
            Assert(core.ZoneManager.IsCardInZone(normalEnchant, p2, Zone.Graveyard),
                   "摧毁：普通无生命单位直送墓");
            Assert(rockEnchant.IsAlive && core.ZoneManager.IsCardInZone(rockEnchant, p2, Zone.Battlefield),
                   "摧毁×不灭：不灭无生命单位免疫摧毁（三口同拦裁定）");
            RetireCards(core, p2, rockEnchant);

            // ---- 22. 费用指示物端到端：**已按 2026-11-05 用户定案删除** ----
            // 理由：卡费是运行时恒推导（GetCardCost→CardCostService.Derive），不再有「手填声明费」这回事；
            // 端到端验证防的是人写错内容，不是替代码验算推导公式。费用/加减层的正确性归
            // CardCostService/CostDerivation 的单测与「仅重推费用」工具的审计日志。

            // ---- 23. 再生·角色侧：**已按 2026-11-05 用户定案删除** ----
            // 理由：关键词=生物专属（HasKeyword 角色恒 false——角色光环读数与物化授予通道
            // 同批下线），角色不再有再生回满行为；卡侧关键词行为不受影响。

            CleanKeywords();
        }

        /// <summary>推进回合直到 p1 处于主阶段（验证器节奏：EndTurn 折返后 SkipElementPool 入主阶段）。</summary>
        private static void EnsureMainPhase(GameCore core, Player p1)
        {
            for (int i = 0; i < 6; i++)
            {
                if (core.TurnEngine.TurnPlayer == p1 && core.TurnEngine.CurrentPhase?.Phase == PhaseType.Main)
                    return;
                var tp = core.TurnEngine.TurnPlayer;
                if (core.TurnEngine.CurrentPhase?.Phase == PhaseType.Standby)
                    GameActions.SkipElementPool(core, tp);
                else
                    EndTurnPumped(core, tp);
            }
        }

        /// <summary>注入一张合成单色 1 费生物（竞速颜色断言的用卡载体）。</summary>
        private static Card InjectColoredCreature(GameCore core, Player owner, ManaType color)
        {
            var data = new CardData { ID = "VERIFY_RITUAL_" + color, CardName = "竞速色卡" + color };
            data.Supertype = Cardtype.Creature;
            data.Power = 1;
            data.Life = 1;
            data.Cost[color] = 1;
            return InjectCard(core, owner, data);
        }

        /// <summary>
        /// 结束回合并驱动折返：编辑器验证上下文没有帧泵（GameLoopController.Update 无人调用），
        /// EndTurn 只推进到结束阶段；手动补一次 CheckPhaseTransition 完成 End→Standby 折返回合切换。
        /// </summary>
        private static bool EndTurnPumped(GameCore core, Player player)
        {
            if (!GameActions.EndTurn(core, player)) return false;
            core.TurnEngine.CheckPhaseTransition();   // End(Active) → 折返 StartNewTurn(下一位)
            return true;
        }

        /// <summary>
        /// PlayCard + 排干栈（验证器同步快进）：出牌即上栈（使用时点声明，不付费），
        /// 双 Pass 让 cast 立即结算——付费、效果、离区在断言前全部完成。
        /// 响应窗口本身的交互（打落/发动无效）见 TestCounterWindow。
        /// </summary>
        private static bool PlayCardSync(GameCore core, Player player, Card card,
            List<Entity> targets = null, Zone fromZone = Zone.Hand, int modeIndex = 0)
        {
            if (!GameActions.PlayCard(core, player, card, targets, fromZone, modeIndex))
                return false;
            GameActions.DrainStack(core);
            return true;
        }

        /// <summary>把 CardData 包成实例注入指定玩家手牌。</summary>
        private static Card InjectCard(GameCore core, Player owner, CardData data)
        {
            var card = new CardWrapper(data);
            card.SetController(owner);
            core.ZoneManager.GetZoneContainer(owner).Add(card, Zone.Hand);
            return card;
        }

        /// <summary>
        /// 对目标执行一次"消灭"裁决（直连死亡决策表，DestroyEffect 死因）。
        /// 毁灭原子已删除（2026-09-03）——本 helper 直连同路径。
        /// </summary>
        private static void DestroyViaEffect(GameCore core, Player actor, Card target)
        {
            CardCore.Attribute.DeathRules.TryKill(target, CardCore.Attribute.DeathCause.DestroyEffect, actor, core.ZoneManager);
        }

    }
}
