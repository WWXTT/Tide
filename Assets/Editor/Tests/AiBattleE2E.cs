using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CardCore;
using SynergyUI;
using UnityEditor;
using UnityEngine;

namespace CardCore.Editor.Tests
{
    /// <summary>
    /// AI 自动对战端到端验证（Unity 测试框架 + 菜单双入口）：
    /// 双 SimpleAI 打完整局，控制台播报全程，跑到出错或游戏结束。
    /// 同时验证表现层契约——播报器订阅与 BattleScreen（真实表现层）完全相同的事件集合，
    /// 若事件面不足以还原一局对局，这里就会暴露缺口。
    /// </summary>
    public static class AiBattleE2E
    {
        private const string Tag = "[对局]";

        [MenuItem("Tools/卡牌核心/AI 自动对战验证")]
        public static void RunFromMenu()
        {
            var result = new AiBattleDriver().RunFullGame(LoadStandardDeck(), maxTurns: 100);
            Debug.Log($"{Tag} 菜单入口结果：{(result.Completed ? $"完成（胜者 {Name(result.Winner)}，{result.Reason}，共 {result.TotalTurns} 回合）" : result.TurnLimitReached ? "到达回合上限" : "异常中止")}\n错误 {result.Errors.Count} 条，播报 {result.AnnouncedLines} 行");
        }

        /// <summary>标准卡组：纯非仪式卡（仪式验证走全仪式压力口径；AI 对战当前测不到仪式，移出）。</summary>
        public static List<CardData> LoadStandardDeck()
            => LoadTestCards().Where(c => !RitualSystem.IsRitual(new CardWrapper(c))).ToList();

        /// <summary>全仪式卡组（压力口径）：开局仪式占满手牌、连环顶替、小卡组疲劳收尾。</summary>
        public static List<CardData> LoadRitualHeavyDeck()
            => LoadTestCards().Where(c => RitualSystem.IsRitual(new CardWrapper(c))).ToList();

        private static List<CardData> LoadTestCards()
        {
            string path = Path.Combine(Application.dataPath, "Configs/TestCreatureCards.json");
            return File.Exists(path) ? CardLoader.LoadCardsFromText(File.ReadAllText(path)) : new List<CardData>();
        }

        internal static string Name(Entity e) => e == null ? "∅"
            : e is Player p ? p.Name
            : e is IHasName n && !string.IsNullOrEmpty(n.CardName) ? n.CardName
            : e is Card c ? c.ID
            : e.ToString();
    }

    /// <summary>一局自动对战的结果。</summary>
    public class BattleRunResult
    {
        public bool Completed;
        public bool TurnLimitReached;
        public Player Winner;
        public string Reason;
        public int TotalTurns;
        public readonly List<string> Errors = new List<string>();
        public int AnnouncedLines;
    }

    /// <summary>
    /// 控制台播报器：前 12 个订阅与 BattleScreen.OnEnter 完全相同的事件集合（表现层契约，
    /// 双向差集为空——若事件面不足以还原一局对局，这里就会暴露缺口）；
    /// 另有 6 个播报专用扩展订阅（开局/效果目标与结算/攻击宣言/入手/疲劳/产出），不参与契约对齐。
    /// StackEmpty 高频低信息，订阅但不播（保持契约一致性）。
    /// </summary>
    public sealed class ConsoleAnnouncer
    {
        private int _lines;
        private bool _seenGameStart; // 起手逐张静默开关：起手由开局汇总行呈现

        public int LineCount => _lines;

        public void Attach()
        {
            var bus = EventManager.Instance;
            // ---- 表现层契约面（12 个，与 BattleScreen.OnEnter 一致，双向差集为空）----
            bus.Subscribe<TurnStartEvent>(OnTurnStart);
            bus.Subscribe<PhaseStartEvent>(OnPhaseStart);
            bus.Subscribe<CardPlayEvent>(OnCardPlay);
            bus.Subscribe<CardZoneChangeEvent>(OnCardZoneChange); // SBA 泵驱动的流程才会发；契约保留
            bus.Subscribe<CardPutToBattlefieldEvent>(OnCardEnterBattlefield);
            bus.Subscribe<CardLeaveBattlefieldEvent>(OnCardLeaveBattlefield);
            bus.Subscribe<LifeChangeEvent>(OnLifeChange);
            bus.Subscribe<CombatDamageEvent>(OnCombatDamage);
            bus.Subscribe<ElementPoolAddEvent>(OnElementPoolAdd);
            bus.Subscribe<ElementPoolPayEvent>(OnElementPoolPay);
            bus.Subscribe<StackEmptyEvent>(OnStackEmpty);
            bus.Subscribe<GameOverEvent>(OnGameOver);

            // ---- 播报专用扩展（不参与表现层契约对齐）----
            bus.Subscribe<GameStartEvent>(OnGameStart);               // 开局牌库/起手（起手抽完后发布）
            bus.Subscribe<CardEnterHandEvent>(OnCardEnterHand);       // 逐张入手（含起手，_seenGameStart 抑制）
            bus.Subscribe<AtomicEffectPhaseEvent>(OnAtomicEffectPhase); // 效果目标选择 + 结算产出
            bus.Subscribe<AttackDeclarationEvent>(OnAttackDeclaration);  // 攻击宣言目标
            bus.Subscribe<CardCore.Attribute.FatigueEvent>(OnFatigue);
            bus.Subscribe<CardCore.Attribute.HealEvent>(OnHeal);       // 引擎直驱治疗（如再生），原子治疗由〔结算〕行呈现
            bus.Subscribe<ElementPoolGainEvent>(OnElementPoolGain);   // 横置产出（呈现"费用花完"）
            bus.Subscribe<KeywordAppliedEvent>(OnKeywordApplied);     // 关键词生效（圣盾/守卫/复生/成长等统一观察点）
        }

        public void Detach()
        {
            var bus = EventManager.Instance;
            // ---- 表现层契约面 ----
            bus.Unsubscribe<TurnStartEvent>(OnTurnStart);
            bus.Unsubscribe<PhaseStartEvent>(OnPhaseStart);
            bus.Unsubscribe<CardPlayEvent>(OnCardPlay);
            bus.Unsubscribe<CardZoneChangeEvent>(OnCardZoneChange);
            bus.Unsubscribe<CardPutToBattlefieldEvent>(OnCardEnterBattlefield);
            bus.Unsubscribe<CardLeaveBattlefieldEvent>(OnCardLeaveBattlefield);
            bus.Unsubscribe<LifeChangeEvent>(OnLifeChange);
            bus.Unsubscribe<CombatDamageEvent>(OnCombatDamage);
            bus.Unsubscribe<ElementPoolAddEvent>(OnElementPoolAdd);
            bus.Unsubscribe<ElementPoolPayEvent>(OnElementPoolPay);
            bus.Unsubscribe<StackEmptyEvent>(OnStackEmpty);
            bus.Unsubscribe<GameOverEvent>(OnGameOver);

            // ---- 播报专用扩展 ----
            bus.Unsubscribe<GameStartEvent>(OnGameStart);
            bus.Unsubscribe<CardEnterHandEvent>(OnCardEnterHand);
            bus.Unsubscribe<AtomicEffectPhaseEvent>(OnAtomicEffectPhase);
            bus.Unsubscribe<AttackDeclarationEvent>(OnAttackDeclaration);
            bus.Unsubscribe<CardCore.Attribute.FatigueEvent>(OnFatigue);
            bus.Unsubscribe<CardCore.Attribute.HealEvent>(OnHeal);
            bus.Unsubscribe<ElementPoolGainEvent>(OnElementPoolGain);
            bus.Unsubscribe<KeywordAppliedEvent>(OnKeywordApplied);
        }

        private void Say(string message)
        {
            _lines++;
            Debug.Log($"[对局] {message}");
        }

        private void OnTurnStart(TurnStartEvent e)
            => Say($"════ 回合 {e.TurnNumber} · {AiBattleE2E.Name(e.TurnPlayer)} ════");

        private void OnPhaseStart(PhaseStartEvent e)
            => Say($"〔阶段〕{AiBattleE2E.Name(e.ActivePlayer)} 进入 {e.Phase}");

        private void OnCardPlay(CardPlayEvent e)
            => Say($"{AiBattleE2E.Name(e.Player)} 打出 {AiBattleE2E.Name(e.PlayedCard)}");

        private void OnCardZoneChange(CardZoneChangeEvent e)
        {
            // 订阅以保持与 BattleScreen 相同的事件面；区域流转已由入场/离场/打牌等专门行呈现，不播（去重）
        }

        private void OnCardEnterBattlefield(CardPutToBattlefieldEvent e)
            => Say($"{AiBattleE2E.Name(e.Card)} 入场（战场，{(e.Tapped ? "横置" : "可用")}）");

        private void OnCardLeaveBattlefield(CardLeaveBattlefieldEvent e)
            => Say($"{AiBattleE2E.Name(e.Card)} 离场");

        private void OnLifeChange(LifeChangeEvent e)
            => Say($"{AiBattleE2E.Name(e.Player)} 生命 {e.OldLife} → {e.NewLife}");

        private void OnCombatDamage(CombatDamageEvent e)
            => Say($"⚔ {AiBattleE2E.Name(e.Attacker)} → {AiBattleE2E.Name(e.Defender)}，造成 {e.Damage} 伤害");

        private void OnElementPoolAdd(ElementPoolAddEvent e)
            => Say($"{AiBattleE2E.Name(e.Player)} 放地牌 {AiBattleE2E.Name(e.AddedCard)}（{DescribeTokens(e.Tokens)}）");

        private void OnElementPoolPay(ElementPoolPayEvent e)
            => Say($"{AiBattleE2E.Name(e.Player)} 支付 {DescribeCost(e.PaidCost)}");

        private void OnStackEmpty(StackEmptyEvent _)
        {
            // 订阅以保持与 BattleScreen 相同的事件面；栈空高频低信息，不播报
        }

        private void OnGameOver(GameOverEvent e)
            => Say($"★ 游戏结束：胜者 {AiBattleE2E.Name(e.Winner)}（{e.Reason}，共 {e.TotalTurns} 回合）");

        // ---- 播报专用扩展回调 ----

        /// <summary>开局块：双方牌库 + 起手（GameStartEvent 在起手抽完后发布，此处快照即终局起手）。</summary>
        private void OnGameStart(GameStartEvent e)
        {
            _seenGameStart = true;
            Say("━━━━ 开局 ━━━━");
            var core = GameCore.Instance;
            foreach (var p in new[] { e.FirstPlayer, e.SecondPlayer })
            {
                if (p == null) continue;
                var deck = core.ZoneManager.GetCards(p, Zone.Deck);
                var hand = core.ZoneManager.GetCards(p, Zone.Hand);
                Say($"{AiBattleE2E.Name(p)} 牌库（{deck?.Count ?? 0}）：{DescribeCards(deck)}");
                Say($"{AiBattleE2E.Name(p)} 起手（{hand?.Count ?? 0}）：{DescribeCards(hand)}");
            }
        }

        private void OnCardEnterHand(CardEnterHandEvent e)
        {
            if (!_seenGameStart) return; // 起手逐张静默（由开局汇总行呈现）
            if (e.IsDraw) Say($"{AiBattleE2E.Name(e.Player)} 抽到 {AiBattleE2E.Name(e.Card)}");
            else Say($"{AiBattleE2E.Name(e.Player)} 获得 {AiBattleE2E.Name(e.Card)}（来自 {e.FromZone}）");
        }

        /// <summary>
        /// 效果目标选择 + 结算产出：
        /// StartApplying 播目标行（仅 Targets 非空），ResolutionComplete 播结算行（仅 LastOutcome 可读）；
        /// Activation 与 StartApplying 紧邻发布，静默。
        /// </summary>
        private void OnAtomicEffectPhase(AtomicEffectPhaseEvent e)
        {
            switch (e.Phase)
            {
                case AtomicEffectPhase.StartApplying:
                    if (e.Targets != null && e.Targets.Count > 0)
                        Say($"〔效果〕{AiBattleE2E.Name(e.Source)} 的 {e.EffectType} → {DescribeEntities(e.Targets)}");
                    break;

                case AtomicEffectPhase.ResolutionComplete:
                    var o = e.Context?.LastOutcome;
                    if (!HasReadableOutcome(o)) break;
                    if (o.DamageDealt > 0)
                        Say($"〔结算〕{e.EffectType}：{DescribeEntities(o.AffectedTargets)} 共受 {o.DamageDealt} 伤害"
                            + (o.KilledTargets.Count > 0 ? $"，{DescribeEntities(o.KilledTargets)} 死亡" : ""));
                    else if (o.HealApplied > 0)
                        Say($"〔结算〕{e.EffectType}：{DescribeEntities(o.AffectedTargets)} 回复 {o.HealApplied} 生命");
                    else if (o.Declaration != null)
                        Say($"〔结算〕{e.EffectType}：宣言「{o.Declaration}」{(o.DeclareHit ? "命中" : "未命中")}");
                    else
                        Say($"〔结算〕{e.EffectType}：作用于 {DescribeEntities(o.AffectedTargets)}");
                    break;
            }
        }

        private void OnAttackDeclaration(AttackDeclarationEvent e)
            => Say($"〔攻击〕{AiBattleE2E.Name(e.Attacker)} → {AiBattleE2E.Name(e.Target)}");

        private void OnFatigue(CardCore.Attribute.FatigueEvent e)
            => Say($"〔疲劳〕{AiBattleE2E.Name(e.Player)} 受到 {e.Damage} 伤害（空牌库抽牌）");

        /// <summary>引擎直驱治疗（Source 为空，如再生关键词）；原子效果治疗已有〔结算〕行，不重复播。</summary>
        private void OnHeal(CardCore.Attribute.HealEvent e)
        {
            if (e.Source != null) return;
            Say($"〔回复〕{AiBattleE2E.Name(e.Target)} 回复 {e.Amount} 生命");
        }

        private void OnElementPoolGain(ElementPoolGainEvent e)
            => Say($"{AiBattleE2E.Name(e.Player)} 横置 {AiBattleE2E.Name(e.FromCard)} 产出 {e.GainedType}");

        private void OnKeywordApplied(KeywordAppliedEvent e)
            => Say($"〔关键词〕{AiBattleE2E.Name(e.Target)}：{e.Detail}");

        private static bool HasReadableOutcome(EffectOutcome o)
            => o != null && (o.DamageDealt > 0 || o.HealApplied > 0
                          || o.KilledTargets.Count > 0 || o.AffectedTargets.Count > 0
                          || o.Declaration != null);

        /// <summary>同名聚合的紧凑卡牌清单（"火球、嘲讽守卫×2、…"）。</summary>
        private static string DescribeCards(List<Card> cards)
            => cards == null || cards.Count == 0 ? "∅"
               : string.Join("、", cards.GroupBy(AiBattleE2E.Name)
                                        .Select(g => g.Count() > 1 ? $"{g.Key}×{g.Count()}" : g.Key));

        private static string DescribeEntities(List<Entity> list)
            => list == null || list.Count == 0 ? "∅" : string.Join("、", list.Select(AiBattleE2E.Name));

        private static string DescribeTokens(Dictionary<ManaType, int> tokens)
        {
            if (tokens == null || tokens.Count == 0) return "无指示物";
            return string.Join("", tokens.Where(kv => kv.Value > 0).Select(kv => $"{kv.Key}×{kv.Value}"));
        }

        private static string DescribeCost(Dictionary<int, float> cost)
        {
            if (cost == null || cost.Count == 0) return "∅";
            return string.Join(" ", cost.Where(kv => kv.Value > 0).Select(kv => $"{(ManaType)kv.Key}×{kv.Value}"));
        }
    }

    /// <summary>
    /// 双 AI 自动对战驱动器：双方 SimpleAI 轮流打完整局，跑到出错或游戏结束。
    /// 所有范围/目标选择自动应答（双方 IsAI=true 走 TargetSelectionService 的自动路径）。
    /// 编辑器上下文无帧泵：SimpleAI 末尾的 EndTurn 只推进到结束阶段，此处补一次
    /// CheckPhaseTransition 完成 End→Standby 折返（同 CardPipelineVerifier.EndTurnPumped 惯例）。
    /// </summary>
    public sealed class AiBattleDriver
    {
        public BattleRunResult RunFullGame(List<CardData> deckSpec, int maxTurns = 100)
        {
            var result = new BattleRunResult();
            var announcer = new ConsoleAnnouncer();
            var gameOver = false;
            GameBoard.BoardState board = null;

            void OnGameOver(GameOverEvent e)
            {
                gameOver = true;
                result.Completed = true;
                result.Winner = e.Winner;
                result.Reason = e.Reason.ToString();
                result.TotalTurns = e.TotalTurns;
            }

            EventManager.Instance.Subscribe<GameOverEvent>(OnGameOver);
            announcer.Attach();
            try
            {
                if (deckSpec == null || deckSpec.Count == 0)
                {
                    result.Errors.Add("卡组为空（缺 TestCreatureCards.json？）");
                    return result;
                }

                var core = GameCore.Instance;
                var ctrl = new BattleController();
                var ai = new SimpleAI();
                // 变形目标形态解析器：组合根注入（编辑器无头路径独立注入）
                CardCore.Attribute.MorphSystem.ResolveMorphTarget = CardCatalog.GetById;
                core.InitGame(CardLoader.BuildDeck(deckSpec, 1), CardLoader.BuildDeck(deckSpec, 1));
                core.Player1.IsAI = true; // 选择全自动
                core.Player2.IsAI = true;

                // 测试口径：双方开局元素池预置 5 点灰色元素——加速中高费随从（关键词卡多为 5-8 费）
                // 出场互殴，让关键词行为在对局内真正得到触发（费用门槛仍受地牌槽上限约束）
                foreach (var p in new[] { core.Player1, core.Player2 })
                {
                    var bank = core.ElementPool.GetPool(p).AvailableMana;
                    bank[ManaType.Gray] = (bank.TryGetValue(ManaType.Gray, out var g) ? g : 0) + 5;
                }

                // 棋盘占用层（派生，单向读核心）：为碾压关键词注入邻接解析（核心不绑棋盘，宿主接线）
                board = new GameBoard.BoardState(core, core.Player1, core.Player2,
                    GameBoard.HalfFieldData.Flat(), GameBoard.HalfFieldData.Flat());
                board.EnableAutoResync();
                CombatSystem.AdjacentResolver = board.Neighbors;

                for (int turn = 0; turn < maxTurns && !gameOver; turn++)
                {
                    try
                    {
                        ai.TakeTurn(ctrl);                          // 内部已 EndTurn（不折返）
                        if (!gameOver)
                            core.TurnEngine.CheckPhaseTransition(); // 补 End→Standby 折返；游戏已结束则不开新回合
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"回合 {core.TurnEngine.TurnNumber}（{AiBattleE2E.Name(core.TurnEngine.TurnPlayer)}）：{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                        break;
                    }
                }

                if (!gameOver && result.Errors.Count == 0)
                {
                    result.TurnLimitReached = true;
                    result.TotalTurns = core.TurnEngine.TurnNumber;
                }

                return result;
            }
            finally
            {
                announcer.Detach();
                EventManager.Instance.Unsubscribe<GameOverEvent>(OnGameOver);
                CombatSystem.AdjacentResolver = null; // 撤销本局的棋盘接线（静态扩展点归零）
                board?.Dispose();
                result.AnnouncedLines = announcer.LineCount;
                Debug.Log($"[对局] 战报：{(result.Completed ? $"游戏结束（胜者 {AiBattleE2E.Name(result.Winner)}，{result.Reason}，{result.TotalTurns} 回合）" : result.TurnLimitReached ? $"到达回合上限 {maxTurns}" : "异常中止")}；播报 {result.AnnouncedLines} 行；错误 {result.Errors.Count} 条");
            }
        }
    }
}
