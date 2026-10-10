using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCore.Attribute
{
    /// <summary>
    /// 关键词战斗/伤害行为的唯一实现点。
    ///
    /// 【设计原则（2026-10-08 不叠加定案；2026-10-09 修订彻底不叠加·全轨取代）】关键词不叠加：
    /// 卡面（合成器/印刷）=文本效果，对战中赋予=附加状态（台账挂载，类似指示物形式）；
    /// 同关键词**无论轨别**新实例取代旧实例（值/次数刷新）——跨轨不并存、互不补充
    ///（附加份不补文本份、文本份不补附加份；双轨只承载存储/清除语义，不是两份可各自消耗的存量）。
    /// 参数化效果走原子（AddArmor 护甲 N 点指示物）。
    /// 【坚韧指示物化（2026-10-08 定案；2026-10-09 生效自减改版）】坚韧退出关键词族——表行 2b1e3700
    ///（GrantToughness 原子，原 GrantArmor 2026-10-08 更名）执行改挂 ToughnessCounter 层
    ///（GrantToughnessHandler）：每次受到的伤害每层 −1、实际拦到即生效——生效后层数减半（floor）、可叠加；
    /// 角色可持有（旧关键词形态 is Card 死线消解）；换区不清（生效自减档）、净化可清。
    ///
    /// 伤害管线（效果 TakeDamage 与战斗 CombatSystem 两条路径都经 ApplyDamage）：
    /// 替代引擎（光环层——穿透伤害也受其限制）→ 圣盾指示物（每层挡一次任意伤害，消耗 1 层）→
    /// 护甲指示物（逐点吸收）→ 坚韧指示物（每层 −1，生效后层数减半）→ 落血；
    /// 吸血（恢复随从自身）/系命（回复角色）。
    /// 【穿透伤害（pierce=true）】越过关键词和指示物计算伤害——跳过圣盾/护甲/坚韧三层，
    /// 替代引擎与落血/事件链/吸血照走。
    /// 【剧毒 Venom】2026-10-08 指示物转关键词（落定追加式）：受到其战斗伤害的生物在结算后被消灭
    ///（见管线尾部 TryKill 消灭口径；对角色照常落血）；毒刺关键词=战斗伤害改为附加毒素指示物（见改写段）。
    /// 事件链统一在管线尾部按时序发布：DamageEvent（触发器）→ CombatDamageEvent（战斗）→
    /// LifeChangeEvent（角色）→ 吸血/系命（伤害的后果）。
    /// 生命流失（LifeLoss 类）不经此管线——圣盾/护甲不挡流失。
    /// </summary>
    public static class KeywordRules
    {
        /// <summary>护甲指示物名（AddArmor 原子写入，伤害先扣）</summary>
        public const string ArmorCounter = "Armor";

        /// <summary>
        /// 冻结指示物名（衰退类，2026-10-08 层即持续定案）：冻结 = 强制横置（一次性动作）
        /// + 负面指示物——层=剩余回合，持有者回合末 −1、归零解除；持有期间无法重置（BlocksUntap）。
        /// 不修改回合规则（回合开始横置重置照常）。
        /// </summary>
        public const string FreezeCounter = "Freeze";

        /// <summary>
        /// 沉睡指示物名（2026-09-11 定案；2026-10-08 层即持续定案）：沉睡期间无法重置
        ///（BlocksUntap）、效果无效（拦触发式+启动式，与无效/沉默合流为三口）；
        /// 层=剩余回合，持有者回合末 −1、归零解除（下回合开始自然恢复重置）。
        /// 效果/指示物分离原则：Sleep 原子（绿1 中性）只负责赋予指示物——持续规则由指示物自身承载。
        /// </summary>
        public const string SleepCounter = "Sleeping";

        // ---- 关键词 id（与 GrantKeywordHandlerFactory.Specs 的运行时字符串同源） ----
        // 圣盾关键词 id "DivineShield" 已删（2026-10-08 指示物化）：改走 CounterRules.DivineShieldCounter
        //（GrantDivineShieldHandler 挂层；挡下口=ApplyPreventionLayers 第 1 层，每层挡一次消耗一层）
        public const string PoisonSting = "PoisonSting";
        /// <summary>冰晶（2026-09-13 改写定案，蓝2）：造成战斗伤害时，改为对目标添加一个冻结指示物</summary>
        public const string IceCrystal = "IceCrystal";
        /// <summary>梦魇（2026-09-13 改写定案，黑2）：造成战斗伤害时，改为对目标添加一个沉睡指示物</summary>
        public const string Nightmare = "Nightmare";
        /// <summary>剧毒（2026-10-08 指示物转关键词定案，落定追加式）：受到其战斗伤害的生物被消灭
        ///（战斗伤害照常结算，实际落定 &gt;0 且目标为生物 → DeathRules.TryKill 消灭口径，不灭/神佑可拦；
        /// 对角色照常落血）。表行 20c06d52（原 Poison 原子行）改挂 GrantVenom。</summary>
        public const string Venom = "Venom";
        /// <summary>禁魔石（2026-09-13 改写定案，白3）：受到的非战斗伤害变为 0</summary>
        public const string Spellban = "Spellban";
        /// <summary>地牌特性（2026-09-13 英雄技能·培育）：持有者可横置产 1 元素（色随自身费用构成）。
        /// GameActions.TapCreatureForElement 是横置口。</summary>
        public const string LandTrait = "LandTrait";
        public const string Lifesteal = "Lifesteal";
        public const string Lifelink = "Lifelink";
        public const string Vigilance = "Vigilance";
        // 潜行关键词 id "Stealth" 已删（2026-10-08 指示物化）：改走 CounterRules.StealthCounter
        //（GrantStealthHandler 挂层；三指定口查层数>0，攻击/发动效果/受伤各消耗 1 层）
        /// <summary>隐密（2026-10-04，蓝5）：潜行的持续版——同不可被攻击/效果指定（三处指定口同查
        /// 潜行指示物+Concealed），但不因**发动效果/攻击/受到伤害**失效（三个失效口只消耗潜行层，天然豁免）。</summary>
        public const string Concealed = "Concealed";
        /// <summary>帷幕（原"嘲讽"，2026-09-13 更名定案）：只吸引**效果**目标（选择层收窄见
        /// TargetResolver.ApplyTauntRestriction）；不拦攻击——攻击侧目标强制由守卫拦截承担。
        /// 运行时 id 仍为 Taunt（更名只改中文文案，同辟邪→扰魔先例）。</summary>
        public const string Taunt = "Taunt";
        public const string FirstStrike = "FirstStrike";
        public const string DoubleStrike = "DoubleStrike";
        public const string Disarm = "Disarm";
        public const string Overwhelm = "Overwhelm";
        // 复生关键词 id "Reborn" 已删（2026-10-08 指示物化）：改走 CounterRules.RebornCounter
        //（GrantRebornHandler 挂层；TryReborn 每层一次死亡替代，消耗 1 层）
        public const string Indestructible = "Indestructible";
        public const string Regeneration = "Regeneration";
        // 法术护盾关键词 id "SpellShield" 已删（2026-10-09 指示物化）：改走 CounterRules.SpellShieldCounter
        //（GrantSpellShieldHandler 挂层；ConsumeSpellShields 每层抵消一次对手效果，消耗 1 层）
        public const string Untargetable = "Untargetable";
        // 微缩/放大关键词 id 已删（2026-10-10 照抄炉石机制退役）：临时复制卡族仅余回响（普通效果 EchoCopy）
        // 回响关键词常量已删（2026-10-07 改普通效果 EchoCopy——宣言时点分支随之退役）
        /// <summary>守护（2026-10-08 配对制改版）：登场选目标，其受伤改写为守护者承受（GuardianRules 配对表；无限次直到守护者离场）。</summary>
        public const string Guardian = "Guardian";

        // ---- 一次性关键词概念已彻底删除（2026-09-08 定案：错误设计）----
        // 关键词都是持续性特征，无「一次性生效后消失/重新入场刷新」的说法：
        // · 冲锋/突袭：改由卡的登场效果表达（OnPlay+激励自己解除横置，突袭另自上紊乱指示物
        //   作代价减费——见 CostDerivationService 的 Self 紊乱对冲）；
        // · 「生效后移除」型（圣盾/复生/潜行/法术护盾）：2026-10-08 起指示物化（法术护盾 10-09 收官）——
        //   每层一份、事件消耗 1 层（CounterRules 四常量），关键词族不再有消耗型成员。

        /// <summary>
        /// 突袭紊乱指示物名（衰退类，2026-10-08 层即持续定案）：持有期间不能以玩家为目标
        /// （攻击与效果发动同口径）；层=剩余回合，持有者回合末 −1、归零解除。
        /// 来源：突袭的登场效果自上（代价减费），或紊乱原子直接施加给敌方（长档=多层数，
        /// 长档紊乱 id 已并入本 id——教学 15 层即 15 回合）。
        /// </summary>
        public const string RushSicknessCounter = "RushSickness";
        // 长档紊乱 id "RushSicknessSustained" 已删（2026-10-08 层即持续统一：同一机制不同量，并入 RushSickness）

        /// <summary>是否处于突袭紊乱（负面指示物存在期间不能以玩家为目标；持有者回合末逐层倒数）。</summary>
        public static bool HasRushSickness(Entity entity)
            => entity is Card c && c.GetCounterCount(RushSicknessCounter) > 0;

        // ==================== 关键词轨别台账（三轨制定案 2026-09-09） ====================

        /// <summary>
        /// 净化豁免名单（2026-09-09 定案：神佑对净化有抗性）。净化剥神佑+剧毒杀角色的组合
        /// 价值过高、无法与作用在生物上的效果计价平衡——净化剥不掉神佑，移除留给未来专用效果。
        /// </summary>
        public static readonly HashSet<string> PurgeProtectedKeywords =
            new HashSet<string> { DeathRules.DivineProtection };

        /// <summary>
        /// 换区清关键词（与 CounterRules.ClearAll 同口）：移除 Temp 轨授予。
        /// 由 ZoneContainer.OnCardMoved 调用（同受发动区豁免约束），且须在 TryEndMorph 之后执行
        /// （否则变形快照恢复会把已清的临时层复活）。
        /// </summary>
        public static void ClearZoneKeywords(Entity entity)
            => RemoveGrants(entity, g => g.Lane == KeywordLane.Temp);

        /// <summary>
        /// 净化清关键词（净化语义重定义 2026-09-09：净化=变回生物原有状态）：
        /// 保留 Printed（卡面本体）与 Setting（设置类视同本体——设置后即「原本属性效果」）；
        /// 清除 Temp / Status 轨（PurgeProtectedKeywords 豁免——神佑等抗净化状态保留；
        /// GrantedPermanent 车道已删 2026-10-08，运行时无写入者）。
        /// 与 CounterRules.PurgeAll（指示物全清）配套，由 PurifyHandler 调用。
        /// </summary>
        public static void PurifyKeywords(Entity entity)
            => RemoveGrants(entity, g => g.Lane != KeywordLane.Printed
                                      && g.Lane != KeywordLane.Setting
                                      && !PurgeProtectedKeywords.Contains(g.Keyword));

        /// <summary>
        /// 入场刷新（2026-09-09 定案；2026-10-08 不叠加改单份）：**真实入场**时对 Printed 轨做
        /// 卡面差集补齐——卡面（CardData.Keywords）有而该实例 Printed 轨没有的关键词补回一份
        ///（卡面重复同名=单份，不叠加）；**指示物化印刷项**（圣盾/复生/潜行/法术护盾——2026-10-08/09）
        /// 不走台账，层数=0 时补 1 层对应指示物（生效自减档换区不清——未消耗层跨区保留，>0 不补防重复；
        /// 消耗归零后经真实入场恢复卡面份）。
        ///
        /// 挂载红线：只挂在 TryMoveToBattlefield / TryAddToBattlefield 统一出口（真换区才算入场）——
        /// · 复生（TryReborn）是死亡替代原地留场，不经出口 → 消耗掉的复生层不会自我补回（无无限复生）；
        /// · 控制权变更（ChangeControl）走容器直移 + 补发 CardPutToBattlefieldEvent——
        ///   ⚠ 因此**绝不能**把刷新挂到 CardPutToBattlefieldEvent 事件上（偷取不刷新消耗项）。
        ///
        /// 差集安全性：Printed 轨已无消耗型移除（法术护盾 2026-10-09 指示物化后清零）；
        /// 圣盾/复生/潜行/法术护盾走指示物差集（层数>0 判断）；净化保留本体、无任何效果可剥
        /// Printed——差集补回的必然只是被消耗项。
        /// </summary>
        public static void RefreshPrintedKeywordsOnEntry(Card card)
        {
            if (card == null) return;
            var face = (card as CardWrapper)?.GetData()?.Keywords;
            if (face == null || face.Count == 0) return;

            foreach (var kw in face.Distinct())
            {
                if (string.IsNullOrEmpty(kw)) continue;

                // 指示物化印刷项（2026-10-08 圣盾/复生/潜行；10-09 法术护盾）：补 1 层指示物
                //（Distinct 单份；消耗后层数=0 才需补）
                if (kw == CounterRules.DivineShieldCounter
                    || kw == CounterRules.RebornCounter
                    || kw == CounterRules.StealthCounter
                    || kw == CounterRules.SpellShieldCounter)
                {
                    if (card.GetCounterCount(kw) > 0) continue;
                    card.AddCounters(kw, 1);
                    EventManager.Instance.Publish(new CounterChangedEvent
                    {
                        Target = card,
                        CounterType = kw,
                        Amount = 1,
                        Source = null,
                    });
                    continue;
                }

                bool printedHeld = false;
                foreach (var g in card._keywordGrants)
                    if (g.Keyword == kw && g.Lane == KeywordLane.Printed) { printedHeld = true; break; }

                if (printedHeld) continue;
                card.AddKeyword(kw, KeywordLane.Printed); // 差集补齐单份（取代制口径内全轨唯一——若现挂 Setting/Temp 份即被此 Printed 份取代）
                EventManager.Instance.Publish(new KeywordAppliedEvent
                {
                    Target = card,
                    Keyword = kw,
                    Detail = "入场刷新：补回卡面本体关键词（消耗项随真实入场恢复）"
                });
            }
        }

        /// <summary>
        /// 台账移除核心：按谓词撤掉授予条目，随后同步 _keywords——
        /// 某关键词的授予条目被清空后（无任何轨再持有）才移除其本体占用。
        /// 注意：无台账条目的关键词（重连还原/直加）不在清除面内——保守视同本体。
        /// </summary>
        private static void RemoveGrants(Entity entity, Func<KeywordGrant, bool> predicate)
        {
            if (entity == null || entity._keywordGrants.Count == 0) return;
            var affected = entity._keywordGrants.Where(predicate).Select(g => g.Keyword).Distinct().ToList();
            if (affected.Count == 0) return;
            entity._keywordGrants.RemoveAll(g => predicate(g));
            foreach (var kw in affected)
            {
                // 不叠加定案（2026-10-09 全轨取代）：条目清空即移除本体占用
                //（全轨唯一下无「其余轨仍在」；此兜底检查仅服务旧档多份残留）
                if (entity._keywordGrants.All(g => g.Keyword != kw))
                    entity._keywords.RemoveAll(k => k == kw);
            }
        }

        // ==================== 生效次数闸已退役（2026-10-08 坚韧指示物化） ====================
        // 唯一消费者是坚韧关键词减伤；坚韧改挂 ToughnessCounter 层后（消耗语义 2026-10-09 起为
        // 生效自减——拦减后层数减半，非「天然无限次」），
        // 台账 Limit/值求和/每回合次数闸整链删除（Guardian 2026-10-08 配对制已先行作废其记账）。

        /// <summary>
        /// 统一伤害结算：修正并施加伤害，返回实际造成的伤害量。
        /// 只改状态不发事件——DamageEvent/CombatDamageEvent 等由调用方按路径发布。
        /// pierce=true 为穿透伤害：越过关键词和指示物（跳过圣盾/护甲/坚韧），
        /// 但受光环限制——替代引擎/层效果照走，事件链与吸血照常。
        /// </summary>
        public static int ApplyDamage(Entity source, Entity target, int amount, bool isCombat, bool pierce = false, bool guardRerouted = false)
        {
            if (amount <= 0 || target == null || !target.IsAlive) return 0;

            // 规则修改类效果（OCP）：伤害实例先经替代引擎取最终值（如伤害封顶），再走关键词管线。
            // 替代效果经 GameCore.ReplacementEngine 注册（状态无关、实时查询光环），本管线不点名任何具体系统。
            // 穿透伤害同样经此层（"受光环限制"）。
            // 2026-10-04：替代件可改写**承受者**（Target）——血偿仪典「己方回合角色受伤→对手承担」先例；
            // 改写后整条管线（防护层/落血/事件链）按新承受者走，DamageEvent 发布也用新目标。
            var routedEvent = new DamageEvent { Source = source, Target = target, Amount = amount };
            var engine = CardCore.GameCore.Instance?.ReplacementEngine;
            if (engine != null)
            {
                if (engine.CheckReplacements(routedEvent).GetFinalEvent() is DamageEvent final)
                {
                    amount = final.Amount;
                    if (final.Target != null && !ReferenceEquals(final.Target, target))
                        target = final.Target; // 替代件改写承受者（血偿转嫁）
                }
                if (amount <= 0) return 0;
            }

            // 禁魔石（2026-09-13 改写定案，白3）：受到的**非战斗伤害**变为 0（战斗伤害照常）
            if (!isCombat && target.HasKeyword(Spellban))
            {
                EventManager.Instance.Publish(new KeywordAppliedEvent
                { Target = target, Keyword = Spellban, Detail = "禁魔石：非战斗伤害变为 0", Source = source });
                return 0;
            }

            // 守护改写（2026-10-08 配对制改版，替代旧箭头光环/扫场护角色两形态）：登场时选定目标的
            // 受伤改写为配对守护者承受（GuardianRules.FindGuardian 活性实时校验——存活/在场/未被无效；
            // 无限次直到守护者死亡/离场，次数闸对守护作废）。单跳（guardRerouted 防链式改写）；
            // 替代路由之后、易损/防护层之前；守卫者承受时自身防护层照走（递归收口）。
            if (!guardRerouted)
            {
                var guardian = GuardianRules.FindGuardian(target);
                if (guardian != null && !ReferenceEquals(guardian, target))
                {
                    if (CardCore.GameCore.Instance != null)
                        CardCore.GameCore.Instance.PublishEvent(new KeywordAppliedEvent
                        {
                            Target = target,
                            Keyword = Guardian,
                            Detail = $"守护改写：伤害转由 {guardian} 承受",
                            Source = guardian,
                        });
                    return ApplyDamage(source, guardian, amount, isCombat, pierce, guardRerouted: true);
                }
            }

            // 0. 易损指示物（2026-10-09 生效自减定案，同毒素档）：受到伤害时每层使受到的伤害 +1，
            //    生效后层数减半（floor，1 层生效一次即清零）——替代结算后、防护层前生效
            //   （圣盾/护甲吸收的是放大后的量；穿透伤害同样被放大——凡放大即生效，毒素同口径）。
            int vulnerable = target.GetCounterCount(CounterRules.VulnerableCounter);
            if (vulnerable > 0)
            {
                amount += vulnerable;
                int vulnHalved = vulnerable / 2;
                target.AddCounters(CounterRules.VulnerableCounter, -(vulnerable - vulnHalved));
                EventManager.Instance.Publish(new KeywordAppliedEvent
                {
                    Target = target,
                    Keyword = CounterRules.VulnerableCounter,
                    Detail = $"易损放大 +{vulnerable} 点（生效减半，余 {vulnHalved} 层）",
                    Source = source
                });
            }

            if (!pierce)
            {
                ApplyPreventionLayers(source, target, ref amount);
                if (amount <= 0) return 0;
            }

            // 战斗伤害改写（2026-09-13 定案修订：**防护层之后、落血之前**——只有"将要成功造成的伤害"
            // 才改写：圣盾/护甲/坚韧完全挡住 → 不触发；部分吸收后仍有剩余 → 剩余改写，不再落血）。
            // 毒刺→毒素1层（绿1）/ 冰晶→冻结1层（蓝2，横置）/ 梦魇→沉睡1层（黑2，横置）。
            // 固定序取第一个命中（多关键词不叠加改写）；
            // 角色（打脸）也改写——毒素落角色有效，冻结/沉睡对角色空转；穿透伤害不经防护层，恒可改写。
            // 施加口径收口 ApplyRewriteCounter。改写只对活体/角色生效——无生命单位（结界）不是状态宿主，
            // 改写命中等价"伤害被无效化"，故跳过改写走耐久管线。
            // 2026-10-07 改写回归·单映射：原毒/冻/眠/疫四光环改写退役（负面光环化——挂层走 DamageEvent 订阅）；
            // 唯一光环改写=舍身仪典（CombatRedirect）——受光环影响的生物造成战斗伤害时，
            // 改为对**光环控制者的对手角色**等量伤害（非战斗路径递归：不再触发舍身；毒蚀只看卡受击）。
            // 病原体（→剧毒指示物）已随 2026-10-08 剧毒转关键词退役；剧毒=落定追加式消灭（见管线尾部）。
            if (isCombat && source != null && source.IsAlive
                && !(target is Card rwCard && rwCard.IsNonLivingUnit()))
            {
                string combatRewrite =
                    source.HasKeyword(PoisonSting) ? PoisonSting :
                    source.HasKeyword(IceCrystal) ? IceCrystal :
                    source.HasKeyword(Nightmare) ? Nightmare : null;
                if (combatRewrite != null)
                {
                    if (ApplyRewriteCounter(combatRewrite, target, source, CombatRewriteDetail(combatRewrite)))
                        return 0;
                }
                else if (source is Card redirectSrc
                         && RuleAuraSystem.HolderRewriteFor(redirectSrc) == RuleAuraComponents.CombatRedirect)
                {
                    // 转投目标=施伤生物控制者的对手角色（2026-10-07 用户定案：相对施伤方——
                    // 己方生物打对方主公、受光环的对方生物打我方主公、双方档=两侧对轰；
                    // 不随光环控制者取固定侧，否则"作用于对方"=对手自伤无敌）
                    var redirectVictim = redirectSrc.GetController()?.Opponent;
                    if (redirectVictim != null)
                    {
                        EventManager.Instance.Publish(new KeywordAppliedEvent
                        {
                            Target = redirectSrc,
                            Keyword = RuleAuraComponents.CombatRedirect,
                            Detail = $"舍身光环：{EffectText.Name(redirectSrc)} 的战斗伤害转投对手角色（原目标免受 {amount} 点）",
                            Source = RuleAuraSystem.CarrierOf(RuleAuraComponents.CombatRedirect),
                        });
                        ApplyDamage(redirectSrc, redirectVictim, amount, isCombat: false);
                        return 0;
                    }
                }
            }

            // 4. 落血（Card 到 0 标记死亡；Player 直接扣）。
            //    死亡决策走决策表（当前无护盾拦 DamageLethal；复生/落墓由 SBA 泵自发连锁处理）。
            //    死亡归因（2026-09-07）：伤害来源随尸体留档，SBA 送墓时经 TryKill 上 CardDestroyEvent。
            //    三轨制（2026-09-09）：归零判定按**有效生命**（_life + 连接光环加成）——
            //    生命光环垫着的单位要先吃穿光环加成才死；断链回落由 SBA 收尾。
            int oldLife = (target as Player)?.Life ?? 0; // 生命变化播报用
            if (target is Card card)
            {
                // 无生命单位（战场结界，2026-10-02 定案）：伤害不落血——受击恒 -1 耐久
                //（战斗/法术伤害同口径，与替代/防护层串行：完全挡住=不受击）；归零直送墓在
                // LoseDurability 内收口（Smashed 直毁，不经死亡决策表）。
                // 有效伤害钳 1：DamageEvent/吸血/系命/返回值同源（耐久口径=每次受击 1 点）。
                if (card.IsNonLivingUnit())
                {
                    amount = 1;
                    CounterRules.LoseDurability(CardCore.GameCore.Instance, card, 1,
                        isCombat ? "战斗伤害" : "效果伤害");
                }
                else
                {
                    card._life -= amount;
                    int auraLife = GameBoard.LinkAuraSystem.GetLifeBonus(card);
                    if (card._life + auraLife <= 0)
                    {
                        card._life = 0;
                        if (!DeathRules.IsShielded(card, DeathCause.DamageLethal))
                        {
                            card._pendingDeathCause = DeathCause.DamageLethal;
                            card._pendingDeathSource = source;
                            card.IsAlive = false;
                        }
                        else if (auraLife > 0)
                        {
                            card._life = 1 - auraLife; // 护盾拦下：以有效生命 1 存活（无光环保持原样=0 存活）
                        }
                    }
                }
            }
            else if (target is Player player)
            {
                player.Life -= amount;
            }

            // 潜行受伤失效（2026-10-04 弱化定案；2026-10-08 指示物化）：伤害**实际落定**（替代/改写/
            // 防护层之后仍有剩余到达落血/耐久）才算「受到伤害」——圣盾/护甲/坚韧完全挡住、毒刺族改写
            // 替代（return 0）均不触发；触发即消耗 1 层潜行指示物（逐份撤口径）。
            // 隐密（Concealed）不失效——不因受伤掉正是它的定价理由（蓝5 vs 蓝1）。
            if (target is Card hurtCard && hurtCard.GetCounterCount(CounterRules.StealthCounter) > 0)
            {
                hurtCard.AddCounters(CounterRules.StealthCounter, -1);
                EventManager.Instance.Publish(new KeywordAppliedEvent
                {
                    Target = hurtCard,
                    Keyword = CounterRules.StealthCounter,
                    Detail = "受到伤害后潜行失效（消耗 1 层）",
                    Source = source
                });
            }

            // 结算后发布伤害事件链（实际值；替代已在管线前消费）。
            // 时序定案：DamageEvent（触发器时点，OnDealDamage/OnTakeDamage 此刻可见）
            // → CombatDamageEvent（战斗路径表现）→ LifeChangeEvent（角色生命变化）
            // → 吸血/系命（伤害的后果最后结算——观察伤害事件的触发器不应看到回复已发生）。
            // 统一由本方法发布，调用方不再事后补发（战斗/效果两条路径同口径）。
            var damageEvent = new DamageEvent { Source = source, Target = target, Amount = amount };
            if (GameCore.Instance != null)
                GameCore.Instance.PublishEventRouted(damageEvent, replacementsApplied: true);
            else
                EventManager.Instance.Publish(damageEvent);

            if (isCombat)
            {
                EventManager.Instance.Publish(new CombatDamageEvent
                {
                    Attacker = source,
                    Defender = target,
                    Damage = amount
                });
            }
            if (target is Player hurt)
            {
                EventManager.Instance.Publish(new LifeChangeEvent
                {
                    Player = hurt,
                    OldLife = oldLife,
                    NewLife = hurt.Life,
                    Source = source
                });
            }

            // 6. 吸血（恢复随从自身）/ 系命（回复角色）——伤害事件的后果，按实际造成的伤害结算
            if (source != null && source.IsAlive)
            {
                if (source.HasKeyword(Lifesteal) && source is Card stealer)
                {
                    stealer.Heal(amount);
                    EventManager.Instance.Publish(new KeywordAppliedEvent
                    {
                        Target = stealer,
                        Keyword = Lifesteal,
                        Detail = $"吸血回复 {amount} 生命",
                        Source = target
                    });
                }
                if (source.HasKeyword(Lifelink) && source.GetController() is Player owner)
                {
                    owner.Life += amount;
                    EventManager.Instance.Publish(new KeywordAppliedEvent
                    {
                        Target = owner,
                        Keyword = Lifelink,
                        Detail = $"系命回复 {amount} 生命",
                        Source = source
                    });
                }
            }

            // 7. 剧毒（2026-10-08 指示物转关键词·落定追加式）：受到其战斗伤害的**生物**被消灭——
            //    伤害照常结算（圣盾/护甲/坚韧正常吸收，实际落定 amount>0 才触发；改写式 return 0
            //    早已出管线）；对角色照常落血不消灭（Deathtouch 口径）；无生命单位走耐久不是生物。
            //    消灭走 TryKill 消灭口径（不灭/神佑经 DeathRules 裁决拦截）；伤害本身已致死的不再重复。
            if (isCombat && amount > 0 && source != null && source.IsAlive
                && target is Card venomTarget && venomTarget.IsAlive && !venomTarget.IsNonLivingUnit()
                && source.HasKeyword(Venom))
            {
                DeathRules.TryKill(venomTarget, DeathCause.DestroyEffect, source,
                    CardCore.GameCore.Instance?.ZoneManager);
            }

            return amount;
        }

        // ======================================== 改写施加共用口（2026-09-22 拦截式改写门） ========================================

        /// <summary>改写施加共用口：按改写关键词 id 对目标施加对应指示物（固定 1 层）并发布审计事件——
        /// 战斗关键词路径（ApplyDamage 固定序命中）与效果侧拦截式改写门（EffectExecutionEngine 预执行拦截）
        /// 共用，防两套口径漂移。detail 由调用方给（战斗/改写门文案不同）。未知 id 返回 false。</summary>
        public static bool ApplyRewriteCounter(string keywordId, Entity target, Entity source, string detail)
        {
            switch (keywordId)
            {
                case PoisonSting:
                    target.AddCounters(CounterRules.ToxinCounter, 1, source);
                    break;
                case IceCrystal:
                    target.Freeze(1, source); // 层数模型：1 层=1 回合（对角色空转）
                    break;
                case Nightmare:
                    if (target is Card sleeper) sleeper.Tap();
                    target.AddCounters(SleepCounter, 1, source);
                    break;
                // Pathogen（→剧毒指示物）分支已删：剧毒 2026-10-08 转关键词（Venom 落定追加式消灭）
                default:
                    return false;
            }
            EventManager.Instance.Publish(new KeywordAppliedEvent
            { Target = target, Keyword = keywordId, Detail = detail, Source = source });
            return true;
        }

        /// <summary>战斗改写路径的审计文案（关键词路径）。
        /// 改写门路径已随拦截式改写门退役删除（2026-10-05 迁唯一光环）。</summary>
        private static string CombatRewriteDetail(string keywordId)
        {
            switch (keywordId)
            {
                case PoisonSting: return "毒刺：战斗伤害改为毒素指示物×1（层数留存逐回合减半）";
                case IceCrystal: return "冰晶：战斗伤害改为冻结指示物×1";
                case Nightmare: return "梦魇：战斗伤害改为沉睡指示物×1";
                default: return "战斗伤害改写";
            }
        }

        /// <summary>
        /// 防护层（非穿透伤害）：圣盾指示物（每层挡一次任意伤害，消耗 1 层）→ 护甲指示物（逐点吸收）→
        /// 坚韧指示物（每层 −1，实际拦到即生效——生效后层数减半 floor）。
        /// amount 按 ref 递减；归零即全部挡下。穿透伤害跳过本方法全部三层。
        /// </summary>
        private static void ApplyPreventionLayers(Entity source, Entity target, ref int amount)
        {
            // 1. 圣盾指示物（2026-10-08 指示物化）：每层挡下一次任意伤害（战斗+效果），消耗 1 层
            //    （Entity 级——角色可持有）；多层并存=逐层各挡一次。
            int shieldLayers = target.GetCounterCount(CounterRules.DivineShieldCounter);
            if (shieldLayers > 0)
            {
                target.AddCounters(CounterRules.DivineShieldCounter, -1);
                EventManager.Instance.Publish(new KeywordAppliedEvent
                {
                    Target = target,
                    Keyword = CounterRules.DivineShieldCounter,
                    Detail = $"圣盾抵挡 {amount} 点伤害（消耗 1 层，余 {shieldLayers - 1} 层）",
                    Source = source
                });
                amount = 0;
                return;
            }

            // 2. 护甲指示物：逐点吸收（AddArmor 原子写入的 counter 池）
            if (target is Card armored)
            {
                int armor = armored.GetCounterCount(ArmorCounter);
                if (armor > 0)
                {
                    int absorbed = Math.Min(armor, amount);
                    armored.AddCounters(ArmorCounter, -absorbed);
                    amount -= absorbed;
                    EventManager.Instance.Publish(new KeywordAppliedEvent
                    {
                        Target = target,
                        Keyword = ArmorCounter,
                        Detail = $"护甲指示物吸收 {absorbed} 点",
                        Source = source
                    });
                    if (amount <= 0)
                    {
                        amount = 0;
                        return;
                    }
                }
            }

            // 3. 坚韧指示物（2026-10-09 生效自减改版）：每层使本次受到的伤害 −1，实际拦到伤害即生效
            //    ——生效后层数减半（floor，易损的正面镜像）；可叠加；Entity 级——角色读数同走此口。
            //    圣盾/护甲全挡提前 return（坚韧未触及→不减半）；穿透跳过防护层同理不减半。
            //    施加口=GrantToughnessHandler（GrantToughness 原子，表行 2b1e3700）；换区不清、净化可清。
            int toughness = target.GetCounterCount(CounterRules.ToughnessCounter);
            if (toughness > 0)
            {
                amount = Math.Max(0, amount - toughness);
                int toughHalved = toughness / 2;
                target.AddCounters(CounterRules.ToughnessCounter, -(toughness - toughHalved));
                EventManager.Instance.Publish(new KeywordAppliedEvent
                {
                    Target = target,
                    Keyword = CounterRules.ToughnessCounter,
                    Detail = $"坚韧减免 {toughness} 点（生效减半，余 {toughHalved} 层）",
                    Source = source
                });
            }
            if (amount <= 0) amount = 0;
        }

        /// <summary>
        /// 是否应横置该实体：攻击/发动代价恒横置。
        /// 【2026-09-10 警戒重定义】警戒不再是「代替横置扣除」（攻击照常横置）——
        /// 新语义为「横置也能造成战斗伤害」（反击资格判定见 CombatSystem.ResolvePair：
        /// 横置目标持警戒仍可反击）。被动无额度、无消耗。
        /// </summary>
        public static bool ShouldTap(Entity entity)
        {
            if (entity == null) return false;
            return true; // 横置是固定代价（旧警戒抵扣已随重定义删除）
        }

        /// <summary>
        /// 复生结算（死亡替代定案；2026-10-08 指示物化）：死亡时的送墓效果被替代——不进墓地、不离场，
        /// 生命值变成 1、横置（本回合不可用），消耗 1 层复生指示物（多层=多次死亡替代）。
        /// 由死亡路径（SBA 零防御 / 摧毁效果）在送墓前调用；true = 已复生（留在战场）。
        /// </summary>
        public static bool TryReborn(Card card)
        {
            int layers = card?.GetCounterCount(CounterRules.RebornCounter) ?? 0;
            if (layers <= 0) return false;

            card.AddCounters(CounterRules.RebornCounter, -1);
            card._life = 1;
            if (card._maxLife < 1) card._maxLife = 1;
            card.IsAlive = true;
            card._isTapped = true;          // 横置（可用性统一指标：视为重新入场，本回合不可用）
            EventManager.Instance.Publish(new KeywordAppliedEvent
            {
                Target = card,
                Keyword = CounterRules.RebornCounter,
                Detail = $"复生：死亡替代——生命变 1 留场（横置，不进墓；余 {layers - 1} 层）"
            });
            return true;
        }

        /// <summary>
        /// 法术护盾结算（2026-10-09 指示物化，表行 7270df35）：目标列表中带护盾的对手随从——
        /// 该效果对其无效（移出目标）并消耗 1 层法术护盾指示物（多层=多次抵消）。
        /// 在效果执行前置过滤（候选/查询阶段不消耗——只在真正执行时挡）。
        /// </summary>
        public static void ConsumeSpellShields(List<Entity> targets, Entity source)
        {
            if (targets == null || targets.Count == 0) return;
            var sourceController = source?.GetController();

            for (int i = targets.Count - 1; i >= 0; i--)
            {
                if (!(targets[i] is Card shielded)) continue;
                var targetController = shielded.GetController();
                if (sourceController == null || targetController == null || sourceController == targetController)
                    continue;
                int layers = shielded.GetCounterCount(CounterRules.SpellShieldCounter);
                if (layers <= 0) continue;

                shielded.AddCounters(CounterRules.SpellShieldCounter, -1);
                targets.RemoveAt(i);
                EventManager.Instance.Publish(new KeywordAppliedEvent
                {
                    Target = shielded,
                    Keyword = CounterRules.SpellShieldCounter,
                    Detail = $"法术护盾使效果对其无效（消耗 1 层，余 {layers - 1} 层）",
                    Source = source
                });
            }
        }
    }
}
