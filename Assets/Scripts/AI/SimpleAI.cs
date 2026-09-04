using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.Attribute;

namespace SynergyUI
{
    /// <summary>
    /// 通用脚本 AI（卡组无关，一期：动作耗尽）。
    /// 每回合穷举可用动作并执行，直到无进展——费用花完、手牌打空、无可攻击单位、无可发动效果。
    /// 所有动作都走 GameActions / BattleController 既有入口；可行性由引擎校验（失败即静默跳过，卡留手不付费）。
    /// 清理对手场面作为通用目标偏好（非针对特定卡组）：攻击优先击杀对方随从（优换 > 换子），
    /// 无击杀机会才打脸（被嘲讽挡则拆嘲讽）；有害类原子的目标优先对方玩家、有益类优先己方；无偏好回落引擎自动解析（targets=null）。
    /// 当前回合玩家即可驱动（AI 对 AI 验证用；BattleScreen 仍只对 P2 调用）。
    /// </summary>
    public sealed class SimpleAI
    {
        private const int MaxActionRounds = 64;   // 动作耗尽循环硬上限（保险，防效果自我循环）
        private const int MaxSettleAttempts = 32; // 排干栈重试上限（保险）

        /// <summary>有害类原子：目标偏好对方玩家 &gt; 对方单位（削血优先的通用化，卡组无关）</summary>
        private static readonly HashSet<AtomicEffectType> HarmfulAtoms = new HashSet<AtomicEffectType>
        {
            // 伤害族（穿透伤害也偏好打脸——越过多层防护直击）
            AtomicEffectType.DealDamage, AtomicEffectType.DealCombatDamage, AtomicEffectType.LifeLoss,
            AtomicEffectType.DrainLife, AtomicEffectType.PierceDamage,
            // 指示物妨害族（剧毒/毒素：回合结束发作的延迟威胁；易损：受伤+1/层）
            AtomicEffectType.Poison, AtomicEffectType.AddToxin, AtomicEffectType.AddVulnerable,
            // 单向属性削弱（攻击力/生命值/±1/费用增加——目标偏好对方）
            AtomicEffectType.AddPowerDown, AtomicEffectType.AddLifeDown, AtomicEffectType.AddMinusOne,
            AtomicEffectType.AddCostUp, AtomicEffectType.Weaken, AtomicEffectType.RushSickness,
            // 破坏/移除族
            AtomicEffectType.Exile, AtomicEffectType.DiscardCard, AtomicEffectType.MillCard,
            AtomicEffectType.BounceToTop, AtomicEffectType.BounceToBottom,
            AtomicEffectType.Smash, // 摧毁：打对方地牌（无生命值单位）
            // 死亡原子族（牺牲目标己方友军，目标偏好对该原子无实际影响）
            AtomicEffectType.Sacrifice, AtomicEffectType.Devour, AtomicEffectType.Annihilate,
            // 妨碍/压制族
            AtomicEffectType.Tap, AtomicEffectType.FreezePermanent, AtomicEffectType.Silence,
            AtomicEffectType.Purify,
            AtomicEffectType.NegateActivation, AtomicEffectType.KnockDown,
            // 夺取控制族
            AtomicEffectType.GainControl,
        };

        /// <summary>有益类原子：目标偏好己方单位 &gt; 己方玩家</summary>
        private static readonly HashSet<AtomicEffectType> BeneficialAtoms = new HashSet<AtomicEffectType>
        {
            // 治疗恢复族
            AtomicEffectType.Heal,
            AtomicEffectType.ReturnToHand, AtomicEffectType.SummonToken,
            AtomicEffectType.ReturnFromGraveyard, AtomicEffectType.RecoverToHand,
            // 资源族
            AtomicEffectType.Untap, AtomicEffectType.AddArmor,
            // 强化族（GrantX 关键词与数值强化；单向属性增益——攻击力/生命值/±1/费用减少）
            AtomicEffectType.ModifyPower, AtomicEffectType.ModifyLife,
            AtomicEffectType.SetPower, AtomicEffectType.SetLife, AtomicEffectType.SetCost,
            AtomicEffectType.AddPowerUp, AtomicEffectType.AddLifeUp, AtomicEffectType.AddPlusOne,
            AtomicEffectType.AddCostDown, AtomicEffectType.Inspire,
            AtomicEffectType.GrantHaste, AtomicEffectType.GrantRush, AtomicEffectType.GrantDoubleStrike,
            AtomicEffectType.GrantCannotBeTargeted, AtomicEffectType.GrantSpellShield,
            AtomicEffectType.GrantLifesteal,
            AtomicEffectType.GrantStealth, AtomicEffectType.GrantTaunt,
            AtomicEffectType.GrantDivineShield, AtomicEffectType.GrantOverwhelm, AtomicEffectType.GrantArmor,
            AtomicEffectType.GrantFirstStrike,
            AtomicEffectType.GrantVigilance, AtomicEffectType.GrantRegeneration,
            AtomicEffectType.Recharging,
            AtomicEffectType.GrantGrowth, AtomicEffectType.GrantReborn, AtomicEffectType.GrantIndestructible,
            AtomicEffectType.GrantLifelink,
            // 展开族
            AtomicEffectType.TakeExtraTurn,
        };

        public void TakeTurn(BattleController ctrl)
        {
            var core = ctrl.Core;
            var me = ctrl.TurnPlayer;
            if (core == null || me == null || me != core.TurnEngine.TurnPlayer) return;

            // 0. 准备阶段：跳过产元素阶段进入主阶段
            GameActions.SkipElementPool(core, me);

            // 1. 资源准备：补地到槽上限（费用最高的生物优先——打不出的当地产元素）→ 横置全部（产色匹配手牌费用需求）
            PlayLands(core, me);
            TapAllLands(core, me);

            // 2. 动作耗尽主循环：各类动作每轮各试一遍（非短路 |），任一成功即继续轮询；
            //    出口 = 无进展：费用花完 / 手牌打空 / 无可发动效果
            for (int i = 0; i < MaxActionRounds && !core.IsGameOver; i++)
            {
                bool progress = DrainVoluntaryQueue(core)
                              | ActivateBattlefieldAbilities(core, me)
                              | PlayBestAffordableCard(core, me)
                              | TryGraveyardPlay(core, me);
                if (!progress) break;
            }

            // 3. 战前排干栈：让本回合发动/触发的效果先落地（增益类才影响攻击结算）
            SettleStack(core);

            // 4. 战斗：清场优先（能击杀的随从先清）→ 无击杀机会才打脸 → 嘲讽挡则拆嘲讽 → 兜底。
            //    连锁结算完成即判胜负（CombatSystem.EndCombat），判负后不再继续动作
            if (!core.IsGameOver)
                DoCombat(ctrl, core, me);

            // 5. 收尾：排干栈（栈未空时 EndTurn 会被栈空守卫静默拒绝）→ 结束回合。
            //    游戏已结束则不再 EndTurn（避免"★ 游戏结束"之后又出现阶段推进）
            SettleStack(core);
            if (!core.IsGameOver)
                GameActions.EndTurn(core, me);
        }

        // ======================================== 资源准备 ========================================

        /// <summary>
        /// 补地（定案策略）：只放"暂时打不出来"的生物（能付起的留手打出）——
        /// 付不起的里挑总费用最高的先放：作为地牌产元素不闲置，耗尽进墓后可经墓地回收再打出。
        /// 全部付得起时不放（保护手里的关键词/高费随从上场面）。
        /// 地牌资格（正式生物，非魔法/衍生物）由引擎权威校验，此处预检跳过无效尝试。
        /// </summary>
        private static void PlayLands(GameCore core, Player me)
        {
            var lands = (core.ZoneManager.GetCards(me, Zone.Hand) ?? new List<Card>())
                .Where(ElementPoolSystem.CanServeAsLand)
                .ToList();
            var unaffordable = lands.Where(c => !CanPlayCard(core, me, c, Zone.Hand))
                                    .OrderByDescending(TotalCost)
                                    .ToList();
            // 兜底：手牌有生物但全部付得起——不放（留手打出，最大化场面交互）
            if (unaffordable.Count == 0) return;
            int cap = core.ElementPool.GetLandCap(me);
            foreach (var card in unaffordable)
            {
                if (core.ElementPool.GetPooledCards(me).Count >= cap) break;
                GameActions.AddToElementPool(core, me, card);
            }
        }

        /// <summary>横置全部地牌：产色按手牌费用需求匹配，无需求色回落剩余指示物最多的颜色。</summary>
        private static void TapAllLands(GameCore core, Player me)
        {
            var demand = BuildColorDemand(core, me);
            foreach (var land in new List<PooledCard>(core.ElementPool.GetPooledCards(me)))
            {
                var color = SelectLandColor(land, demand);
                if (color.HasValue)
                    GameActions.GainElementFromToken(core, me, land, color.Value);
            }
        }

        /// <summary>手牌费用色需求直方图（决定横置产色的优先级）。</summary>
        private static Dictionary<ManaType, int> BuildColorDemand(GameCore core, Player me)
        {
            var demand = new Dictionary<ManaType, int>();
            foreach (var card in core.ZoneManager.GetCards(me, Zone.Hand) ?? new List<Card>())
            {
                if (!(card is IHasCost hc) || hc.Cost == null) continue;
                foreach (var kv in hc.Cost)
                {
                    var color = (ManaType)kv.Key;
                    int amount = (int)Math.Ceiling(kv.Value);
                    demand[color] = demand.TryGetValue(color, out var v) ? v + amount : amount;
                }
            }
            return demand;
        }

        /// <summary>单地产色决策：需求色优先（需求量大者优先），无需求色取剩余指示物最多者。</summary>
        private static ManaType? SelectLandColor(PooledCard land, Dictionary<ManaType, int> demand)
        {
            ManaType? best = null;
            int bestScore = 0;
            foreach (var color in land.GetAvailableColors())
            {
                if (demand.TryGetValue(color, out var d) && d > bestScore)
                {
                    best = color;
                    bestScore = d;
                }
            }
            if (best != null) return best;

            ManaType? most = null;
            int mostTokens = 0;
            foreach (var kv in land.Tokens)
            {
                if (kv.Value > mostTokens)
                {
                    mostTokens = kv.Value;
                    most = kv.Key;
                }
            }
            return most;
        }

        // ======================================== 动作耗尽循环的四个动作源 ========================================

        /// <summary>发动引擎已入队的可选待发效果（触发式可选项，"把能发动的都发动"）。</summary>
        private static bool DrainVoluntaryQueue(GameCore core)
        {
            bool any = false;
            // 快照遍历：发动（出队入栈）本身会改动待发队列
            var pending = new List<PendingEffect>(core.StackEngine.GetActivatableVoluntaryEffects());
            foreach (var effect in pending)
            {
                if (core.StackEngine.PlayerActivateVoluntary(effect))
                    any = true;
            }
            return any;
        }

        /// <summary>枚举场上卡的激活式能力并逐个发动（CanActivate 预检 + 引擎权威校验）。</summary>
        private static bool ActivateBattlefieldAbilities(GameCore core, Player me)
        {
            bool any = false;
            var battlefield = new List<Card>(core.ZoneManager.GetCards(me, Zone.Battlefield) ?? new List<Card>());
            var executor = core.StackEngine.GetExecutor();
            var phase = core.TurnEngine.CurrentPhase?.Phase ?? PhaseType.Main;
            foreach (var source in battlefield)
            {
                foreach (var def in GetEffectDefinitions(source))
                {
                    if (!def.IsActivatedEffect) continue; // 只管激活式；触发式由引擎事件驱动
                    if (!executor.CanActivate(def, source, me, me, phase, core.TurnEngine.TurnNumber)) continue;
                    if (GameActions.ActivateEffect(core, me, def, source))
                        any = true;
                }
            }
            return any;
        }

        /// <summary>出一张最优的牌：有害原子优先（削血优先的通用化）→ 总费用降序（大费先出，资源花光）。
        /// 出牌即上栈（使用时点）：出完立刻排干——cast 结算付费后 bank 已更新，下一轮预检不失真。</summary>
        private static bool PlayBestAffordableCard(GameCore core, Player me)
        {
            var hand = core.ZoneManager.GetCards(me, Zone.Hand) ?? new List<Card>();
            var ordered = hand
                .Where(c => CanPlayCard(core, me, c, Zone.Hand))
                .OrderByDescending(ContainsHarmfulAtom)
                .ThenByDescending(TotalCost)
                .ToList();
            foreach (var card in ordered)
            {
                // 失败静默重试下一张（引擎契约：不付费、卡留手）
                if (GameActions.PlayCard(core, me, card, ChooseTargets(core, me, card)))
                {
                    SettleStack(core); // cast 已上栈：双 Pass 排干（AI 无响应 → 立即结算付费）
                    return true;
                }
            }
            return false;
        }

        /// <summary>墓地出牌（归土仪典配额内；预检后再调，避免 TryBeginUse 先烧配额）。出完同样立刻排干。</summary>
        private static bool TryGraveyardPlay(GameCore core, Player me)
        {
            var graveyard = core.ZoneManager.GetCards(me, Zone.Graveyard) ?? new List<Card>();
            foreach (var card in graveyard)
            {
                if (!CanPlayCard(core, me, card, Zone.Graveyard)) continue;
                if (GameActions.PlayCardFromGraveyard(core, me, card, ChooseTargets(core, me, card)))
                {
                    SettleStack(core);
                    return true;
                }
            }
            return false;
        }

        // ======================================== 出牌辅助 ========================================

        /// <summary>出牌预检（引擎无公开 CanPlay，自行组合规则钩子 + 支付力）。</summary>
        private static bool CanPlayCard(GameCore core, Player me, Card card, Zone fromZone)
        {
            if (!RuleHooks.CanPlay(core, me, card, fromZone)) return false;
            if (card is IHasCost hc && hc.Cost != null && hc.Cost.Count > 0 &&
                !core.ElementPool.CanPayCost(hc.Cost, me)) return false;
            return true;
        }

        /// <summary>
        /// 通用目标偏好选择：引擎候选集保合法（同 BattleScreen.PromptTargetThenPlay 惯例），
        /// 再按有害/有益偏好重排取前 N；无偏好返回 null 交引擎自动解析。
        /// </summary>
        private static List<Entity> ChooseTargets(GameCore core, Player me, Card card)
        {
            var atomic = FirstTargetingAtomic(card, out var cfg);
            if (atomic == null || cfg == null) return null; // 无需选目标 → 引擎自动

            var ctx = new EffectExecutionContext
            {
                Source = card,
                Controller = me,
                ZoneManager = core.ZoneManager,
                ElementPool = core.ElementPool,
            };
            var resolver = new TargetResolver(core.ZoneManager);
            var candidates = resolver.GetCandidates(cfg, ctx);
            if (!string.IsNullOrEmpty(cfg.TargetFilter))
                candidates = resolver.ApplyFilters(candidates, resolver.ParseFilters(cfg.TargetFilter), ctx);
            if (candidates == null || candidates.Count == 0) return null;

            int need = cfg.TargetCount > 0 ? cfg.TargetCount : 1;
            var opp = me.Opponent;
            IEnumerable<Entity> ordered;
            if (HarmfulAtoms.Contains(atomic.Type))
            {
                // 有害：对方玩家 > 对方单位 > 其他
                ordered = candidates
                    .OrderByDescending(c => ReferenceEquals(c, opp))
                    .ThenBy(c => c is Card cd && cd.GetController() == me);
            }
            else if (BeneficialAtoms.Contains(atomic.Type))
            {
                // 有益：己方单位 > 己方玩家 > 其他
                ordered = candidates
                    .OrderByDescending(c => c is Card cd && cd.GetController() == me)
                    .ThenBy(c => ReferenceEquals(c, me));
            }
            else
            {
                ordered = candidates; // 无偏好：保持引擎序（等价自动解析）
            }
            return ordered.Take(need).ToList();
        }

        /// <summary>该卡第一个需玩家选目标的原子（TargetType==Target，仅非激活式；仿 BattleScreen.FindTargetingAtomic）。</summary>
        private static AtomicEffectInstance FirstTargetingAtomic(Card card, out AtomicEffectConfig cfg)
        {
            cfg = null;
            foreach (var def in GetEffectDefinitions(card))
            {
                if (def.IsActivatedEffect) continue; // 施放即结算的才会随出牌自动跑
                foreach (var atomic in def.Effects)
                {
                    var c = AtomicEffectTable.GetByType(atomic.Type);
                    if (c != null && c.TargetType == EffectTargetType.Target)
                    {
                        cfg = c;
                        return atomic;
                    }
                }
            }
            return null;
        }

        /// <summary>卡效果转译（CardData → EffectDefinition 列表）；非 CardWrapper 或无效果返回空。</summary>
        private static List<EffectDefinition> GetEffectDefinitions(Card card)
        {
            if (!(card is CardWrapper wrapper)) return new List<EffectDefinition>();
            var data = wrapper.GetData();
            if (data?.Effects == null || data.Effects.Count == 0) return new List<EffectDefinition>();
            return CardEffectConverter.ConvertAll(data.Effects, data.ID);
        }

        /// <summary>是否含非激活式的有害原子（出牌优先级依据）。</summary>
        private static bool ContainsHarmfulAtom(Card card)
        {
            foreach (var def in GetEffectDefinitions(card))
            {
                if (def.IsActivatedEffect) continue;
                foreach (var atomic in def.Effects)
                    if (HarmfulAtoms.Contains(atomic.Type)) return true;
            }
            return false;
        }

        private static float TotalCost(Card card)
            => card is IHasCost hc && hc.Cost != null ? hc.Cost.Values.Sum() : 0f;

        // ======================================== 战斗 ========================================

        /// <summary>开战斗 → 每个可攻击单位按 清场优先 选目标宣言 → 结算。</summary>
        private static void DoCombat(BattleController ctrl, GameCore core, Player me)
        {
            ctrl.BeginCombat();

            var opp = me.Opponent;
            var battlefield = new List<Card>(core.ZoneManager.GetCards(me, Zone.Battlefield) ?? new List<Card>());
            foreach (var unit in battlefield)
            {
                if (!core.CombatSystem.CanDeclareAttack(unit, me)) continue; // 失调/横置/零攻/超次数，引擎权威判定
                var target = PickAttackTarget(core, unit, opp);
                if (target != null)
                    ctrl.DeclareAttack(me, unit, target);
            }

            ctrl.ResolveCombat();
        }

        /// <summary>
        /// 攻击目标决策（清场优先）：能击杀的对方随从先清——"自己也存活"的优换严格优先于换子，
        /// 同档内挑攻击力最高的（拆最大威胁）；无击杀机会才打脸（不蹭随从白送血）；
        /// 打脸被嘲讽挡则拆嘲讽（无论能否击杀，清路优先）；兜底任意可指定目标。
        /// 力量按 LayerEngine 实时值（光环/增益在场时击杀判定不失真），权威结算仍在引擎。
        /// </summary>
        private static Entity PickAttackTarget(GameCore core, Card unit, Player opp)
        {
            var combat = core.CombatSystem;
            var oppField = core.ZoneManager.GetCards(opp, Zone.Battlefield) ?? new List<Card>();

            // ---- 清场档：打得死的对方随从（合法性经 CanAttackTarget：潜行/守卫等由引擎滤） ----
            int myPower = core.LayerEngine.CalculatePower(unit);
            int myLife = unit.GetLife();
            Card pick = null;
            bool pickSurvives = false;
            int pickThreat = int.MinValue;
            foreach (var enemy in oppField)
            {
                if (!enemy.IsAlive || !combat.CanAttackTarget(unit, enemy)) continue;
                if (myPower < enemy.GetLife()) continue; // 打不死——不白送，交还打脸

                int threat = core.LayerEngine.CalculatePower(enemy);
                // 反击资格（定案）：已横置的目标只能挨打不反击 → 恒优换；未横置按反击力量判断
                bool iSurvive = enemy.IsTapped() || threat < myLife;
                if (pick == null || (iSurvive && !pickSurvives)
                    || (iSurvive == pickSurvives && threat > pickThreat))
                {
                    pick = enemy;
                    pickSurvives = iSurvive;
                    pickThreat = threat;
                }
            }
            if (pick != null) return pick;

            // ---- 无击杀机会 → 打脸 ----
            if (combat.CanAttackTarget(unit, opp)) return opp;

            // ---- 打脸被嘲讽挡 → 拆嘲讽（CombatSystem.DefendersWithTaunt 的等价自查） ----
            foreach (var taunter in oppField)
            {
                if (taunter.IsAlive && taunter.HasKeyword(KeywordRules.Taunt) && combat.CanAttackTarget(unit, taunter))
                    return taunter;
            }

            foreach (var enemy in oppField) // 突袭（无冲锋）不能攻玩家等受限情形的兜底：只打随从
            {
                if (combat.CanAttackTarget(unit, enemy)) return enemy;
            }
            return null;
        }

        // ======================================== 收尾 ========================================

        /// <summary>排干栈：双 Pass 触发结算（AI 自动目标为同步路径，通常即时排干）。带重试上限保险。
        /// 引擎统一实现见 GameActions.DrainStack（验证器/UI 快进与 AI 共用）。</summary>
        private static void SettleStack(GameCore core)
        {
            GameActions.DrainStack(core, MaxSettleAttempts);
        }
    }
}
