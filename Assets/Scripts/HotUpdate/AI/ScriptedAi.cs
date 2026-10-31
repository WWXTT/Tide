using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.AI;
using Cysharp.Threading.Tasks;

namespace SynergyUI
{
    /// <summary>
    /// 整回合 AI 驱动器抽象（2026-10-02 教学模式）：SimpleAI（动作耗尽启发）与 ScriptedAi（教学剧本）
    /// 同形可换，BattleController 持本接口、缺省 SimpleAI。NeuralAI（tide_policy）经 NeuralAiDriver
    /// 适配接入（2026-10-14 镜像对决定案）。
    /// </summary>
    public interface IAiTurnDriver
    {
        UniTask TakeTurnAsync(BattleController ctrl);
    }

    /// <summary>
    /// 教学固定策略驱动器（2026-10-02 教学模式）：按 TutorialConfig.aiScript 逐回合精确执行机器人动作——
    /// 与 SimpleAI 的动作耗尽相反：剧本说什么做什么，多余动作一概不做（教学节奏完全可控）。
    /// 时序镜像 SimpleAI.TakeTurnAsync：跳产元素阶段 → 逐条执行剧本（出牌前横置全部元素、出牌后排干栈
    /// 结算付费；攻击后 await SettleResponseWindowAsync——人类玩家可弹窗响应）→ 排干栈 → 结束回合。
    /// 找牌口径：按 Card.ID 定位（实例 ID 唯一——教学同 id 多张经 Seeder 摆场唯一化
    /// 为衍生物式 `模板#N`，2026-10-17 用户定案）；动作失败记日志继续
    /// （引擎契约：不付费、卡留手）；该回合无剧本=直接结束回合（教学局不卡死）。
    /// LastTurnReport 记录最近回合逐动作成败，验证器据此断言剧本全量落地。
    /// </summary>
    public sealed class ScriptedAi : IAiTurnDriver
    {
        private readonly TutorialConfig _script;
        private int _aiTurnCount; // 已驱动的机器人回合数（从 1 计）——剧本按机器人自己的回合号编排

        /// <summary>最近一个已执行回合的逐动作成败（验证器断言用；每回合开头清空）。</summary>
        public readonly List<(TutorialAction action, bool ok)> LastTurnReport =
            new List<(TutorialAction, bool)>();

        /// <param name="script">教学配置（aiScript 按「机器人的第几个回合」编排）。</param>
        /// <param name="turnOffset">回合号偏移（2026-10-16 教学一跳过介绍：skip 模式直入第二段时
        /// 传 2——机器人第 1 个回合直接对到 aiScript turn3（田忌剧本段），跳过第一段剧本）。</param>
        public ScriptedAi(TutorialConfig script, int turnOffset = 0)
        {
            _script = script;
            _aiTurnCount = turnOffset;
        }

        /// <summary>动作间节奏等待（2026-10-17 表现同步定案）：每个剧本动作完成后注入——
        /// 由 BattleScreen 装配为「等视觉在途门归零」（战斗演出走位/节拍/死亡全播完才继续下一动作）。
        /// 缺省 null=全速（AI-vs-AI/无头验证不受影响）。镜像 SimpleAI.PacingWait 口径。</summary>
        public Func<UniTask> PacingWait { get; set; }

        /// <summary>无剧本回合是否告警（缺省 true=教学缺剧本属配置 bug 要吵；木桩脚本驱动
        /// 2026-10-23 无剧本=预期行为，置 false 静默让过）。</summary>
        public bool WarnWithoutScript = true;

        /// <summary>最终考验压力拍武装（导演器 RunMidTransition 单点维护：末段=武装、非末段/分段
        /// 回退重入=按目标段重算解除——防上段武装残留误杀）。武装后 AI 回合开场直伤对方英雄
        ///（玩家）致死→考验失败重挑（2026-10-20 用户定案：自己回合没解决对手，下个回合被打死）。</summary>
        public bool TrialPressureArmed;

        /// <summary>同步入口（编辑器/无头验证：无人类响应者，全链同步完成）。</summary>
        public void TakeTurn(BattleController ctrl)
            => TakeTurnAsync(ctrl).GetAwaiter().GetResult();

        public async UniTask TakeTurnAsync(BattleController ctrl)
        {
            var core = ctrl.Core;
            var me = ctrl.TurnPlayer;
            if (core == null || me == null || me != core.TurnEngine.TurnPlayer) return;

            _aiTurnCount++;
            LastTurnReport.Clear();

            // 0. 跳过产元素阶段进入主阶段（镜像 SimpleAI；剧本的 element 动作在主阶段执行）
            GameActions.SkipElementPool(core, me);

            // 最终考验压力拍（武装闸见 TrialPressureArmed）：AI 回合开场直伤对方英雄——玩家回合
            // 未解决对手即被打死，考验失败。走效果伤害正链（ApplyDamage+AtomicDamageEvent）：
            // 弹体/爆点/红浮字演出与英雄死亡/败局弹窗原生触发。致死即收（剧本动作/EndTurn 不再推进）。
            if (TrialPressureArmed && !core.IsGameOver)
            {
                ApplyTrialPressure(core, me);
                if (core.IsGameOver) return;
                if (PacingWait != null) await PacingWait();
            }

            var turn = _script?.GetAiTurn(_aiTurnCount);
            if (turn?.actions == null || turn.actions.Count == 0)
            {
                if (WarnWithoutScript)
                    TideLog.Warn($"[ScriptedAi] 机器人回合 {_aiTurnCount} 无剧本——直接结束回合（教学剧本未覆盖）");
            }
            else
            {
                foreach (var action in turn.actions)
                {
                    if (core.IsGameOver) return;
                    if (action == null) continue;
                    if (action.type == "end") break; // 提前截断（收尾统一 EndTurn）
                    LastTurnReport.Add((action, await ExecuteAsync(ctrl, core, me, action)));
                    // 节奏等待（2026-10-17 表现同步）：上一动作的演出（战斗走位/死亡等）播完才继续
                    // ——否则守卫确认结算后立刻宣言下一击，把刚要开始的动画冻在连锁窗口里
                    if (PacingWait != null && !core.IsGameOver) await PacingWait();
                }
            }

            // 收尾：排干栈（栈未空时 EndTurn 被栈空守卫静默拒绝）→ 结束回合；游戏已结束不再推进
            GameActions.DrainStack(core);
            if (!core.IsGameOver)
                GameActions.EndTurn(core, me);
        }

        /// <summary>考验直伤：对方英雄当前生命+2（护甲余量——考验段玩家无护甲，纯保险）=必杀。
        /// 演出走 AtomicDamageEvent 正链（BattleProjector→PlayEffectDamageAsync：弹体+爆点+红浮字）。</summary>
        private static void ApplyTrialPressure(GameCore core, Player me)
        {
            var target = me?.Opponent;
            if (target == null || target.GetLife() <= 0) return;
            int lifeBefore = target.GetLife();
            int actual = CardCore.Attribute.KeywordRules.ApplyDamage(me, target, lifeBefore + 2, false, false);
            core.PublishEvent(new CardCore.Attribute.AtomicDamageEvent
            {
                Source = me,
                Target = target,
                Damage = actual,
                IsCombatDamage = false,
                DamageType = CardCore.Attribute.DamageType.Normal,
            });
            TideLog.Info($"[ScriptedAi] 考验压力直伤：{me?.Name} → {target.Name} {actual} 伤（{lifeBefore}→{target.GetLife()}）");
        }

        // ======================================== 动作执行 ========================================

        private async UniTask<bool> ExecuteAsync(BattleController ctrl, GameCore core, Player me, TutorialAction action)
        {
            switch (action.type)
            {
                case "element":
                {
                    var card = FindInZone(core, me, Zone.Hand, action.card);
                    if (card == null) return LogResult(action, false, $"手牌无 {action.card}");
                    // 2026-10-11 锁色定案：action.target 可选填产色名（红/蓝/绿/灰，缺省=主色）
                    ManaType? color = null;
                    if (!string.IsNullOrEmpty(action.target)
                        && System.Enum.TryParse<ManaType>(action.target, true, out var lockColor))
                        color = lockColor;
                    return LogResult(action, GameActions.AddToElementPool(core, me, card, color),
                        "入元素池被拒（上限/资格/无锁色候选）");
                }
                case "play":
                {
                    var card = FindInZone(core, me, Zone.Hand, action.card);
                    if (card == null) return LogResult(action, false, $"手牌无 {action.card}");
                    // 连锁防线（2026-10-20 充盈六连修复）：出牌前先排干残留栈——上一动作的奖励结算
                    //（弹选清账后续排）没走完时直接施放会叠上未空栈=速度0非法连锁。教学剧本全部
                    // 串行动作，正常路径此处恒空栈零开销；DrainStack 幂等。
                    GameActions.DrainStack(core);
                    bool ok = GameActions.PlayCard(core, me, card, ResolveTargets(core, me, action.targets),
                        Zone.Hand, action.mode, false, out var reason); // awakenCast=false（剧本无苏醒）
                    if (!ok) return LogResult(action, false, $"出牌被拒：{reason}");
                    GameActions.DrainStack(core); // cast 已上栈：双 Pass 结算付费（AI 无响应；
                                                   // 付费步系统自动横置地牌——2026-10-11 现取现用）
                    return true;
                }
                case "attack":
                {
                    var attacker = FindOnBattlefield(core, me, action.attacker);
                    if (attacker == null) return LogResult(action, false, $"场上无 {action.attacker}");
                    var target = ResolveEntity(core, me, action.target);
                    if (target == null && !string.IsNullOrEmpty(action.fallback))
                        target = ResolveEntity(core, me, action.fallback); // 首选目标已死/离场回落（考验：6/6 死→转攻角色）
                    if (target == null) return LogResult(action, false, $"目标不可解析：{action.target}");
                    if (!GameActions.DeclareAttack(core, me, attacker, target))
                        return LogResult(action, false, "攻击宣言被拒（资格/横置/守卫）");
                    await GameActions.SettleResponseWindowAsync(core); // 速度0开窗：人类玩家可响应
                    return true;
                }
                case "skill":
                {
                    bool ok = await GameActions.ActivateHeroSkill(core, me);
                    if (ok) GameActions.DrainStack(core);
                    return LogResult(action, ok, "英雄技能被拒（费用/横置/沉默/已用）");
                }
                case "activate":
                {
                    // 场上卡主动效果宣动（2026-10-19 第二课连锁教学）：card=场上卡 id（action.card 复用），
                    // mode=第几个主动效果（0 起，缺省首个 IsActivatedEffect）。宣动成功后**不排干栈**——
                    // await SettleResponseWindowAsync 开响应窗给人类玩家连锁（教学核心演示位），
                    // 双 Pass 结算后才继续（镜像 attack 口径）。
                    var source = FindOnBattlefield(core, me, action.card);
                    if (source == null) return LogResult(action, false, $"场上无 {action.card}");
                    var defs = CardEffectConverter.ConvertAll(
                        (source as CardWrapper)?.GetData()?.Effects, source.ID);
                    EffectDefinition def = null;
                    int idx = 0;
                    foreach (var d in defs ?? new List<EffectDefinition>())
                    {
                        if (d == null || !d.IsActivatedEffect) continue;
                        if (idx == action.mode) { def = d; break; }
                        idx++;
                    }
                    if (def == null)
                        return LogResult(action, false, $"{action.card} 无主动效果（或序号越界）");
                    if (!GameActions.ActivateEffect(core, me, def, source))
                        return LogResult(action, false, "主动效果宣动被拒（主阶段/横置/速度门）");
                    await GameActions.SettleResponseWindowAsync(core); // 连锁窗口：人类玩家可响应
                    return true;
                }
                // "tap"/"tapland" 已删（2026-10-11 现取现用：地牌横置=付费步系统自动动作，
                // 教学脚本不再演示手动产色；"tapcreature" 保留——带地牌特性生物的产色仍是主动能力）
                case "tapcreature":
                {
                    var creature = FindOnBattlefield(core, me, action.card);
                    if (creature == null) return LogResult(action, false, $"场上无 {action.card}");
                    return LogResult(action, GameActions.TapCreatureForElement(core, me, creature),
                        "生物产色被拒（横置/无地牌特性/引导闸）");
                }
                // "playgrave" 已随 2026-10-30 归土改版整删（墓地视手牌使用退役——归土=回合末自动复活；
                // 无存量教学脚本引用该动作）
                case "phantom":
                {
                    // 幻卡清算（2026-10-18 教学二·末日降临挑战）：AI 第一回合签名动作——烧空己方
                    // 镜像卡组，凭空召唤末日降临（带紊乱束缚）+ 仪典载体直挂规则光环。
                    return LogResult(action, ExecutePhantom(core, me, action), "幻卡清算失败");
                }
                case "wallet":
                {
                    // 钱包灌入（2026-10-20 段六充盈钱包制）：钱包任一回合结束清空——场景播种的 bank
                    // 跨不过回合边界，须在实际消费的回合内重灌。side=player/ai（缺省 ai），
                    // target=颜色名（缺省 Gray，同 element 动作产色槽），mode=数量（缺省 1）。
                    // 走 AddMana 官方口：生成钳同口径（教学量≤地牌上限不受影响）+ElementPoolGainEvent 可观测。
                    var owner = action.side == "player" ? me.Opponent : me;
                    ManaType color = ManaType.Gray;
                    if (!string.IsNullOrEmpty(action.target)
                        && System.Enum.TryParse<ManaType>(action.target, true, out var wc))
                        color = wc;
                    int amount = action.mode > 0 ? action.mode : 1;
                    int granted = core.ElementPool.AddMana(owner, color, null, amount);
                    return LogResult(action, granted == amount, $"钱包灌入被钳（实发 {granted}/{amount} {color}）");
                }
                default:
                    TideLog.Warn($"[ScriptedAi] 未知动作类型：{action.type}（机器人回合 {_aiTurnCount}）");
                    return false;
            }
        }

        private bool LogResult(TutorialAction action, bool ok, string failReason)
        {
            if (!ok)
                TideLog.Warn($"[ScriptedAi] 机器人回合 {_aiTurnCount} 动作失败 {action.type} " +
                             $"{action.card ?? action.attacker}：{failReason}");
            return ok;
        }

        // ======================================== 幻卡清算（教学二） ========================================

        /// <summary>幻卡清算（2026-10-18 教学二·末日降临挑战·AI 第一回合签名动作）。字段口径——
        /// card=末日降临卡 id（正主 C_791FE42E）、target=仪典载体卡 id（涌泉仪典 C_57A3ADD6——
        /// 2026-10-30 纳川改版剥离疲劳免疫后教学载体随切；2026-10-31 涌泉表行退役后该卡保留为
        /// 教学幻卡载体、effectIds 已空——Boss 免疲劳全靠下方 RuleAuraSystem.Activate 直挂）、
        /// targets=仪典规则光环 id（FatigueImmunity=免疲劳——镜像卡组烧空后 AI 空牌库抽牌
        /// 不再疲劳自伤）、mode=紊乱层数（RushSickness：boss 暂不可攻击角色、每它回合末−1）。
        /// 实现：烧空=TutorialScenarioSeeder.ClearSide 同款抹除口径（容器直删+CardMoveEvent
        /// To=None，不走死亡/换区结算）；造卡=CardCatalog→CardLoader（Seeder SpawnToDeck 同源）；
        /// 入场=ZoneManager.TryMoveToBattlefield 官方口（容量闸+CardPutToBattlefieldEvent——
        /// 视图对账+出场演出）+RegisterCardTriggeredEffects 显式补（静默入场不接触发式）；
        /// 仪典光环=RuleAuraSystem.Activate 直挂（Seeder 预置仪典同口径，覆盖双方）。</summary>
        private static bool ExecutePhantom(GameCore core, Player me, TutorialAction action)
        {
            var container = core.ZoneManager?.GetZoneContainer(me);
            if (container == null || string.IsNullOrEmpty(action.card)) return false;

            // ① 烧空己方全部区域（镜像手牌+牌库为主；其余区域防御性同清——ClearSide 同款）
            int burned = 0;
            foreach (var zone in new[] { Zone.Battlefield, Zone.ElementPool, Zone.Hand, Zone.Deck, Zone.Graveyard, Zone.Exile })
            {
                foreach (var card in core.ZoneManager.GetCards(me, zone) ?? new List<Card>())
                {
                    if (card == null) continue;
                    if (zone == Zone.ElementPool) core.ElementPool.RemoveCardFromPool(card, me);
                    container.Remove(card, zone);
                    if (zone == Zone.Battlefield)
                    {
                        card.IsAlive = false;
                        core.PublishEvent(new CardLeaveBattlefieldEvent
                        {
                            Card = card,
                            Controller = me,
                            Destination = Zone.None,
                        });
                    }
                    core.PublishEvent(new CardMoveEvent
                    {
                        MovedCard = card,
                        From = zone,
                        To = Zone.None,
                        Controller = me,
                    });
                    burned++;
                }
            }
            var wallet = core.ElementPool.GetPool(me);
            if (wallet != null)
                foreach (ManaType t in System.Enum.GetValues(typeof(ManaType)))
                    wallet.TempElements[t] = 0;

            // ② 末日降临入场 + 紊乱束缚×mode
            var boss = SpawnToBattlefield(core, me, action.card);
            if (boss == null) return false;
            if (action.mode > 0)
                boss.AddCounters(CardCore.Attribute.KeywordRules.RushSicknessCounter, action.mode, me);

            // ③ 仪典载体入场 + 规则光环直挂
            string auraNote = "";
            if (!string.IsNullOrEmpty(action.target))
            {
                var carrier = SpawnToBattlefield(core, me, action.target);
                if (carrier != null)
                {
                    if (!string.IsNullOrEmpty(action.targets))
                        RuleAuraSystem.Activate(action.targets, carrier, me);
                    auraNote = $"，仪典入场（{action.targets}）";
                }
            }

            TideLog.Info($"[ScriptedAi] 幻卡清算：{me?.Name} 烧掉 {burned} 张镜像卡，召唤 " +
                         $"{TutorialGuide.NameOf(action.card)}（紊乱×{action.mode}）{auraNote}");
            return true;
        }

        /// <summary>凭空造卡直入战场：CardCatalog→CardLoader 造实例，先静默入牌库（官方入场口
        /// 的源区域），再 TryMoveToBattlefield（Deck→Battlefield=SummonedByEffect 入场源，发布
        /// 入场事件链）；失败（满场）=官方口已入墓，返回 null。</summary>
        private static Card SpawnToBattlefield(GameCore core, Player owner, string cardId)
        {
            var data = CardCatalog.GetById(cardId);
            if (data == null)
            {
                TideLog.Warn($"[ScriptedAi] 幻卡造卡失败（卡表无此 id）：{cardId}");
                return null;
            }
            var card = CardLoader.BuildDeck(new List<CardData> { data }, 1)?.FirstOrDefault(c => c != null);
            if (card == null) return null;
            card.SetController(owner);
            var container = core.ZoneManager.GetZoneContainer(owner);
            container.Add(card, Zone.Deck);
            if (!core.ZoneManager.TryMoveToBattlefield(card, owner, Zone.Deck))
            {
                TideLog.Warn($"[ScriptedAi] 幻卡召唤入场失败（满场？）：{cardId}");
                return null;
            }
            GameActions.RegisterCardTriggeredEffects(core, card, owner);
            return card;
        }

        // ======================================== 找牌与目标解析 ========================================

        /// <summary>按卡 id 在指定区域找牌（教学卡组每卡 1 张，id 唯一；找不到 null）。</summary>
        private static Card FindInZone(GameCore core, Player p, Zone zone, string cardId)
            => string.IsNullOrEmpty(cardId) ? null
                : (core.ZoneManager.GetCards(p, zone) ?? new List<Card>())
                    .FirstOrDefault(c => c.ID == cardId);

        /// <summary>按卡 id 找场上单位（先己方后对方战场）。id 唯一口径：教学同 id 多张经
        /// TutorialScenarioSeeder 摆场唯一化（2026-10-17 用户定案"相同 ID 直接用衍生物 ID"）——
        /// 剧本引用实例 ID（`模板`/`模板#N`）即精确到每一只，无需运行时挑选。</summary>
        private static Card FindOnBattlefield(GameCore core, Player p, string cardId)
            => FindInZone(core, p, Zone.Battlefield, cardId)
               ?? FindInZone(core, p.Opponent, Zone.Battlefield, cardId);

        /// <summary>目标令牌 → 实体："hero"/空=对方英雄；卡 id=任一侧场上单位。</summary>
        private static Entity ResolveEntity(GameCore core, Player me, string token)
        {
            if (string.IsNullOrEmpty(token) || token == "hero") return me.Opponent;
            return FindOnBattlefield(core, me, token);
        }

        /// <summary>出牌目标串（逗号分隔令牌）→ 目标列表；null/"auto"=引擎自动解析。</summary>
        private static List<Entity> ResolveTargets(GameCore core, Player me, string spec)
        {
            if (string.IsNullOrEmpty(spec) || spec == "auto") return null;
            var list = new List<Entity>();
            foreach (var raw in spec.Split(','))
            {
                var entity = ResolveEntity(core, me, raw.Trim());
                if (entity == null) TideLog.Warn($"[ScriptedAi] 目标令牌不可解析：{raw}");
                else list.Add(entity);
            }
            return list.Count > 0 ? list : null;
        }
    }
}
