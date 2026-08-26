using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.Attribute;
using UnityEngine.UIElements;

namespace SynergyUI
{
    /// <summary>
    /// 纯 UI 对战界面（Phase 4，玩家 vs 极简 AI）：第一次把运行时执行接上
    /// —— 出牌 / 元素池 / 效果结算 / 指向性法术目标弹窗 / 攻击战斗。
    /// 对称布局：上对手区 / 中(阶段+操作) / 下我方区；弹窗用于目标与攻击选择、胜负提示。
    /// 编排与战斗结算缺口由 BattleController 在 UI 层补齐，不改 CardCore 引擎。
    /// </summary>
    public sealed class BattleScreen : UIScreen
    {
        public override string UxmlResourcePath => "UXML/Battle";

        private readonly BattleController _ctrl = new BattleController();
        private bool _gameEnded;

        // 中文映射（展示串中文，标识符英文）。
        private static readonly Dictionary<PhaseType, string> PhaseNames = new Dictionary<PhaseType, string>
        {
            { PhaseType.Standby, "准备" }, { PhaseType.Main, "主要" }, { PhaseType.End, "结束" },
        };
        private static readonly Dictionary<ManaType, string> ManaNames = new Dictionary<ManaType, string>
        {
            { ManaType.Gray, "灰" }, { ManaType.Red, "红" }, { ManaType.Blue, "蓝" },
            { ManaType.Green, "绿" }, { ManaType.White, "白" }, { ManaType.Black, "黑" },
        };

        private GameCore Core => GameCore.Instance;
        private Player P1 => _ctrl.P1;
        private Player P2 => _ctrl.P2;
        private PhaseType CurrentPhase => Core.TurnEngine.CurrentPhase?.Phase ?? PhaseType.Standby;
        private bool IsPlayerTurn => _ctrl.IsPlayerTurn;

        public override void OnEnter()
        {
            _gameEnded = false;
            _ctrl.StartNewGame();

            UIBinder.BindButton(Root, "btn-back", () => Manager.Back());
            UIBinder.BindButton(Root, "btn-skip-standby", OnSkipStandby);
            UIBinder.BindButton(Root, "btn-begin-combat", OnBeginCombat);
            UIBinder.BindButton(Root, "btn-resolve-combat", OnResolveCombat);
            UIBinder.BindButton(Root, "btn-end-turn", OnEndTurn);
            UIBinder.BindButton(Root, "overlay-cancel", OnOverlayCancel);

            // 事件多但刷新幂等：统一全量重建（先求正确，性能后续再优化）。
            Subscribe<TurnStartEvent>(_ => RefreshAll());
            Subscribe<PhaseStartEvent>(_ => RefreshAll());
            Subscribe<CardPlayEvent>(_ => RefreshAll());
            Subscribe<CardZoneChangeEvent>(_ => RefreshAll());
            Subscribe<CardPutToBattlefieldEvent>(_ => RefreshAll());
            Subscribe<CardLeaveBattlefieldEvent>(_ => RefreshAll());
            Subscribe<LifeChangeEvent>(_ => RefreshAll());
            Subscribe<CombatDamageEvent>(_ => RefreshAll());
            Subscribe<ElementPoolAddEvent>(_ => RefreshAll());
            Subscribe<ElementPoolPayEvent>(_ => RefreshAll());
            Subscribe<StackEmptyEvent>(_ => RefreshAll());
            Subscribe<GameOverEvent>(OnGameOver);

            // 注册通用目标选择器（效果引擎在结算时通过 TargetSelectionService 调用本弹窗）
            TargetSelectionService.Current = new UiTargetSelector(Root);

            CloseOverlay();
            RefreshAll();
        }

        public override void OnExit()
        {
            // 仅在仍是本界面注册时解除，避免覆盖其它界面的注册
            if (TargetSelectionService.Current is UiTargetSelector)
                TargetSelectionService.Current = null;
        }

        // ======================================== 全量刷新 ========================================

        private void RefreshAll()
        {
            if (Core == null || P1 == null || P2 == null) return;

            // 对手区（上）：手牌张数背面、战场生物、墓地计数、生命、元素池。
            Q<Label>("lbl-opp-life").text = $"生命 {P2.Life}";
            Q<Label>("lbl-opp-hand-count").text = $"手牌 {Count(P2, Zone.Hand)}";
            Q<Label>("lbl-opp-grave").text = $"墓地 {Count(P2, Zone.Graveyard)}";
            Q<Label>("lbl-opp-pool").text = $"元素池：{PoolSummary(P2)}";
            Q<Label>("lbl-opp-lands").text = $"地牌：{LandSummary(P2)}";
            BuildBattlefield(Q<VisualElement>("opp-battlefield"), P2, mine: false);

            // 我方区（下）：手牌正面（可点）、战场生物（可点攻击）、墓地、生命、元素池。
            Q<Label>("lbl-self-life").text = $"生命 {P1.Life}";
            Q<Label>("lbl-self-grave").text = $"墓地 {Count(P1, Zone.Graveyard)}";
            Q<Label>("lbl-self-pool").text = $"元素池：{PoolSummary(P1)}";
            Q<Label>("lbl-self-lands").text = $"地牌：{LandSummary(P1)}";
            BuildBattlefield(Q<VisualElement>("self-battlefield"), P1, mine: true);
            BuildHand(Q<VisualElement>("self-hand"), P1);

            // 中区：回合 / 阶段 / 栈 / 操作可用性。
            var tp = _ctrl.TurnPlayer;
            string who = tp == P1 ? "我方" : "对手";
            Q<Label>("lbl-phase").text = $"回合 {Core.TurnEngine.TurnNumber} · {who}的{PhaseName(CurrentPhase)}阶段";
            Q<Label>("lbl-stack").text = _ctrl.InCombat ? "战斗中" : "栈：空";
            UpdateActionButtons();
        }

        private void UpdateActionButtons()
        {
            bool myMain = IsPlayerTurn && CurrentPhase == PhaseType.Main && !_gameEnded;
            bool myStandby = IsPlayerTurn && CurrentPhase == PhaseType.Standby && !_gameEnded;
            SetEnabled("btn-skip-standby", myStandby);
            SetEnabled("btn-begin-combat", myMain && !_ctrl.InCombat);
            SetEnabled("btn-resolve-combat", myMain && _ctrl.InCombat);
            SetEnabled("btn-end-turn", IsPlayerTurn && !_gameEnded);
        }

        private void SetEnabled(string name, bool enabled) => Q<Button>(name).SetEnabled(enabled);

        private int Count(Player p, Zone z) => Core.ZoneManager.GetCards(p, z)?.Count ?? 0;

        private string PoolSummary(Player p)
        {
            var parts = new List<string>();
            foreach (ManaType type in Enum.GetValues(typeof(ManaType)))
            {
                int n = Core.ElementPool.GetAvailableManaCount(type, p);
                if (n > 0) parts.Add($"{ManaName(type)}{n}");
            }
            return parts.Count > 0 ? string.Join(" ", parts) : "—";
        }

        /// <summary>地牌行：每张地牌的剩余指示物与横置态。</summary>
        private string LandSummary(Player p)
        {
            var lands = Core.ElementPool.GetPooledCards(p);
            if (lands == null || lands.Count == 0) return "—";
            var parts = new List<string>();
            foreach (var land in lands)
            {
                var name = (land.SourceCard as IHasName)?.CardName ?? land.SourceCard.ID;
                var tokens = string.Join("", land.Tokens
                    .Where(kv => kv.Value > 0)
                    .Select(kv => $"{ManaName(kv.Key)}{kv.Value}"));
                var tap = land.IsTapped ? "·横置" : "";
                parts.Add($"[{name}{tap} {tokens}]");
            }
            return string.Join(" ", parts);
        }

        private static string PhaseName(PhaseType p) => PhaseNames.TryGetValue(p, out var s) ? s : p.ToString();
        private static string ManaName(ManaType m) => ManaNames.TryGetValue(m, out var s) ? s : m.ToString();

        // ======================================== 卡片视图 ========================================

        private void BuildHand(VisualElement container, Player p)
        {
            container.Clear();
            foreach (var card in new List<Card>(Core.ZoneManager.GetCards(p, Zone.Hand)))
            {
                var captured = card;
                container.Add(MakeCard(captured, onClick: () => OnClickHandCard(captured)));
            }
        }

        private void BuildBattlefield(VisualElement container, Player p, bool mine)
        {
            container.Clear();
            foreach (var card in new List<Card>(Core.ZoneManager.GetCards(p, Zone.Battlefield)))
            {
                var captured = card;
                Action click = mine ? (() => OnClickMyUnit(captured)) : (Action)null;
                container.Add(MakeCard(captured, click));
            }
        }

        private VisualElement MakeCard(Card card, Action onClick)
        {
            var el = new VisualElement();
            el.AddToClassList("battle-card");
            if (card.IsTapped()) el.AddToClassList("battle-card--tapped");

            var name = new Label(CardName(card));
            name.AddToClassList("battle-card__name");
            el.Add(name);

            var stats = new Label(StatLine(card));
            stats.AddToClassList("battle-card__stats");
            el.Add(stats);

            if (onClick != null)
                el.RegisterCallback<ClickEvent>(_ => onClick());
            return el;
        }

        private static string CardName(Card card)
        {
            if (card is CardWrapper w)
            {
                var d = w.GetData();
                return string.IsNullOrEmpty(d.CardName) ? d.ID : d.CardName;
            }
            return card.ToString();
        }

        private static string StatLine(Card card)
        {
            int power = card.GetPower();
            int life = card.GetLife();
            string s = $"{power}/{life}";
            if (card.IsTapped()) s += " 横";
            return s;
        }

        // ======================================== 操作派发 ========================================

        private void OnClickHandCard(Card card)
        {
            if (!IsPlayerTurn || _gameEnded) return;

            if (CurrentPhase == PhaseType.Standby)
            {
                GameActions.AddToElementPool(Core, P1, card);
                RefreshAll();
                return;
            }

            if (CurrentPhase == PhaseType.Main)
            {
                var targetAtomic = FindTargetingAtomic(card);
                if (targetAtomic != null)
                {
                    PromptTargetThenPlay(card, targetAtomic);
                    return;
                }
                GameActions.PlayCard(Core, P1, card, null);
                RefreshAll();
            }
        }

        private void OnSkipStandby()
        {
            if (!IsPlayerTurn || CurrentPhase != PhaseType.Standby) return;
            GameActions.SkipElementPool(Core, P1);
            RefreshAll();
        }

        private void OnBeginCombat()
        {
            if (!IsPlayerTurn || CurrentPhase != PhaseType.Main) return;
            _ctrl.BeginCombat();
            RefreshAll();
        }

        private void OnResolveCombat()
        {
            _ctrl.ResolveCombat();
            RefreshAll();
        }

        private void OnEndTurn()
        {
            if (!IsPlayerTurn || _gameEnded) return;
            GameActions.EndTurn(Core, P1);
            // 交给 AI 跑完 P2 的整个回合，回到 P1，再统一刷新。
            if (!_gameEnded && _ctrl.TurnPlayer == P2)
                _ctrl.RunAiTurn();
            RefreshAll();
        }

        // ======================================== 我方单位攻击 ========================================

        private void OnClickMyUnit(Card unit)
        {
            if (!IsPlayerTurn || _gameEnded) return;
            if (!_ctrl.InCombat)
            {
                ShowToast("先点「进入战斗」");
                return;
            }
            if (unit.IsTapped()) { ShowToast("该单位已横置"); return; }

            // 攻击目标：对手玩家 + 对手战场单位。
            var targets = new List<Entity> { P2 };
            targets.AddRange(Core.ZoneManager.GetCards(P2, Zone.Battlefield).Cast<Entity>());

            ShowOverlay("选择攻击目标", $"用 {CardName(unit)} 攻击：", targets, picked =>
            {
                if (!_ctrl.DeclareAttack(P1, unit, picked))
                    ShowToast("无法对该目标攻击");
                CloseOverlay();
                RefreshAll();
            });
        }

        // ======================================== 目标选择弹窗（指向性法术） ========================================

        /// <summary>返回该卡第一个需玩家选择目标（TargetType==Target）的原子配置；无则 null。</summary>
        private AtomicEffectConfig FindTargetingAtomic(Card card)
        {
            if (!(card is CardWrapper wrapper)) return null;
            var defs = CardEffectConverter.ConvertAll(wrapper.GetData().Effects, wrapper.GetData().ID);
            foreach (var def in defs)
            {
                if (def.IsActivatedEffect) continue; // 施放即结算的才会自动跑
                foreach (var atomic in def.Effects)
                {
                    var cfg = AtomicEffectTable.GetByType(atomic.Type);
                    if (cfg != null && cfg.TargetType == EffectTargetType.Target)
                        return cfg;
                }
            }
            return null;
        }

        private void PromptTargetThenPlay(Card card, AtomicEffectConfig cfg)
        {
            var ctx = new EffectExecutionContext
            {
                Source = card,
                Controller = P1,
                ZoneManager = Core.ZoneManager,
                ElementPool = Core.ElementPool,
            };
            var resolver = new TargetResolver(Core.ZoneManager);
            var candidates = resolver.GetCandidates(cfg, ctx);
            if (!string.IsNullOrEmpty(cfg.TargetFilter))
                candidates = resolver.ApplyFilters(candidates, resolver.ParseFilters(cfg.TargetFilter), ctx);

            if (candidates.Count == 0)
            {
                // 无候选：按无目标直接结算（引擎自动解析兜底）。
                GameActions.PlayCard(Core, P1, card, null);
                RefreshAll();
                return;
            }

            int need = cfg.TargetCount > 0 ? cfg.TargetCount : 1;
            ShowOverlay("选择目标", $"为 {CardName(card)} 选择 {need} 个目标：", candidates, picked =>
            {
                CloseOverlay();
                GameActions.PlayCard(Core, P1, card, new List<Entity> { picked });
                RefreshAll();
            });
        }

        // ======================================== 弹窗通用 ========================================

        private void ShowOverlay(string title, string hint, List<Entity> options, Action<Entity> onPick)
        {
            Q<Label>("overlay-title").text = title;
            Q<Label>("overlay-hint").text = hint;
            var list = Q<ScrollView>("overlay-list");
            list.Clear();
            foreach (var entity in options)
            {
                var captured = entity;
                var row = new VisualElement();
                row.AddToClassList("list-row");
                var label = new Label(EntityName(captured));
                label.AddToClassList("list-row__name");
                row.Add(label);
                row.RegisterCallback<ClickEvent>(_ => onPick(captured));
                list.Add(row);
            }
            Q<VisualElement>("overlay").style.display = DisplayStyle.Flex;
        }

        private void CloseOverlay()
        {
            Q<VisualElement>("overlay").style.display = DisplayStyle.None;
        }

        /// <summary>取消按钮：游戏结束时充当「返回主菜单」，否则仅关闭弹窗。</summary>
        private void OnOverlayCancel()
        {
            if (_gameEnded) Manager.Back();
            else CloseOverlay();
        }

        private string EntityName(Entity e)
        {
            if (e is Player p) return $"{(p == P1 ? "我方" : "对手")}（生命 {p.Life}）";
            if (e is Card c) return $"{CardName(c)} [{StatLine(c)}]";
            return e.ToString();
        }

        // ======================================== 胜负 / 提示 ========================================

        private void OnGameOver(GameOverEvent e)
        {
            _gameEnded = true;
            bool win = e.Winner == P1;
            ShowOverlay(win ? "胜利" : "失败",
                $"{(win ? "我方" : "对手")}获胜（{e.Reason}）。",
                new List<Entity>(), null);
            Q<Button>("overlay-cancel").text = "返回主菜单";
            UpdateActionButtons();
        }

        private void ShowToast(string message) => Q<Label>("lbl-toast").text = message;
    }
}
