using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;

namespace SynergyUI
{
    /// <summary>
    /// 引导步骤规格（TutorialConfig.guide 条目）：
    /// attack——玩家须用 attacker 宣言攻击 target（"hero"=对方角色 / 卡 id=场上单位）；
    /// guard——对手 attacker 宣言攻击时玩家须用 guarder 守卫拦截；
    /// end——强制结束回合（此步进行中一切攻击被拒）；
    /// element——玩家须把 card（手牌）放入元素池当地牌（第二课资源流转）；
    /// tap——玩家须横置 land（元素池内地牌源卡 id）产出 color 一点；
    /// tapcreature——玩家须横置 card（带 LandTrait 的生物）产色；
    /// play——玩家须从手牌打出 card。
    /// hint=引导提示文案（UI 高亮/弹窗后续接，逻辑层只透传）。
    /// </summary>
    [Serializable]
    public class TutorialGuideStepSpec
    {
        public string type;
        public string attacker;
        public string target;
        public string guarder;
        public string hint;
        public string card;   // element/tapcreature/play 的卡 id
        public string land;   // tap 的地牌源卡 id
        public string color;  // tap 的产出颜色（"Gray"/"Red"/"Blue"/"Green"/"White"/"Black"）
    }

    /// <summary>
    /// 教学引导状态机（纯逻辑层——暂停引导的高亮/弹窗 UI 后续接）：
    /// 步骤未完成时把对局"钉住"（暂停语义）——
    /// · 攻击闸：attack 步骤只放行「引导攻击者×引导目标」；guard 步骤期间生物保留待命；
    ///   end 步骤期间强制结束回合；**课规 heroLockCard（第二课）：该卡存活期间玩家不可攻击对方角色**
    ///   （步骤完成后仍然生效——boss 亡即解锁；法术直伤不在此列）；
    /// · 结束回合闸：attack 步骤未完成禁走；
    /// · 守卫候选过滤：guard 步骤只留引导守卫者，非引导攻击者的窗口置空自动放行；
    /// · 资源闸（第二课）：element/tap/tapcreature/play 步骤分别钉住放地/产色/生物产色/出牌。
    /// 步骤完成即推进；全部完成后步骤闸退场（Active=false），仅课规（heroLock）随钩子留存到 End()。
    /// 漏守兜底：guard 步骤对应的攻击结算/取消而未被拦截 → 步骤记 Missed 完成并推进（不因漏守死锁）。
    /// 安装：BattleScreen.StartTutorialGame（配置含 guide 时 Begin）/ OnExit 与 Shutdown 同口径 End。
    /// </summary>
    public static class TutorialGuide
    {
        private static readonly List<TutorialGuideStepSpec> _steps = new List<TutorialGuideStepSpec>();
        private static readonly List<bool> _missed = new List<bool>();
        private static int _index = -1;
        private static string _heroLockCard;

        /// <summary>引导步骤是否仍在进行（全部完成后=false，但课规钩子留存到 End）。</summary>
        public static bool Active { get; private set; }

        /// <summary>当前步骤序号（0 起；=Steps.Count 表示全部完成；-1=未开始）。</summary>
        public static int StepIndex => _index;

        /// <summary>当前待完成步骤（null=未开始或已全部完成）。</summary>
        public static TutorialGuideStepSpec Current
            => _index >= 0 && _index < _steps.Count ? _steps[_index] : null;

        /// <summary>引导步骤全集（UI 侧步骤条后续接）。</summary>
        public static IReadOnlyList<TutorialGuideStepSpec> Steps => _steps;

        /// <summary>步骤是否漏做（守卫窗口错过＝Missed 完成而非按引导完成）。</summary>
        public static bool StepMissed(int index) => index >= 0 && index < _missed.Count && _missed[index];

        /// <summary>步骤推进事件（参数=新步骤序号；=Steps.Count 表示全部完成）。UI 高亮/提示后续接此事件。</summary>
        public static event Action<int> OnStepAdvanced;

        // ======================================== 生命周期 ========================================

        /// <summary>按教学配置装闸启动引导（guide 为空/全空步骤=只装课规（heroLockCard）或直接返回；
        /// 重复 Begin 先 End 防钩子泄漏）。</summary>
        public static void Begin(TutorialConfig tutorial)
        {
            End();
            _steps.Clear();
            _missed.Clear();
            foreach (var s in tutorial?.guide ?? new List<TutorialGuideStepSpec>())
                if (s != null && !string.IsNullOrEmpty(s.type))
                    _steps.Add(s);
            _heroLockCard = tutorial?.heroLockCard;

            _index = 0;
            Active = _steps.Count > 0;
            _missed.AddRange(_steps.Select(_ => false));

            RuleHooks.TutorialAttackGate = GateAttack;
            RuleHooks.TutorialEndTurnGate = GateEndTurn;
            RuleHooks.TutorialGuardFilter = FilterGuards;
            RuleHooks.TutorialElementGate = GateElement;
            RuleHooks.TutorialTapGate = GateTap;
            RuleHooks.TutorialTapCreatureGate = GateTapCreature;
            RuleHooks.TutorialPlayGate = GatePlay;
            RuleHooks.OnTutorialAttackDeclared = NotifyAttackDeclared;
            RuleHooks.OnTutorialAttackResolved = NotifyAttackResolved;
            RuleHooks.OnTutorialGuardResolved = NotifyGuardResolved;
            RuleHooks.OnTutorialTurnEnded = NotifyTurnEnded;
            RuleHooks.OnTutorialElementPlaced = NotifyElementPlaced;
            RuleHooks.OnTutorialManaGained = NotifyManaGained;
            RuleHooks.OnTutorialCreatureTapped = NotifyCreatureTapped;
            RuleHooks.OnTutorialCardPlayed = NotifyCardPlayed;

            if (Active)
                TideLog.Info($"[TutorialGuide] 引导开始：{_steps.Count} 步——第 1 步 {Describe(_steps[0])}");
            else if (!string.IsNullOrEmpty(_heroLockCard))
                TideLog.Info($"[TutorialGuide] 无引导步骤，仅课规：{NameOf(_heroLockCard)} 存活期间不可攻击对方角色");
            else
                return; // 既无步骤也无课规：不占钩子
            OnStepAdvanced?.Invoke(0);
        }

        /// <summary>完全退场（卸全部钩子；局末/切屏/回落普通局时调用，成对 Begin/End 防跨局残留）。
        /// 注意：步骤全部完成只置 Active=false（课规随钩子留存），End 才是真正的拆除。</summary>
        public static void End()
        {
            if (!Active && _index < 0 && RuleHooks.TutorialAttackGate == null && RuleHooks.TutorialElementGate == null)
                return; // 从未安装
            Active = false;
            _index = -1;
            _heroLockCard = null;
            RuleHooks.TutorialAttackGate = null;
            RuleHooks.TutorialEndTurnGate = null;
            RuleHooks.TutorialGuardFilter = null;
            RuleHooks.TutorialElementGate = null;
            RuleHooks.TutorialTapGate = null;
            RuleHooks.TutorialTapCreatureGate = null;
            RuleHooks.TutorialPlayGate = null;
            RuleHooks.OnTutorialAttackDeclared = null;
            RuleHooks.OnTutorialAttackResolved = null;
            RuleHooks.OnTutorialGuardResolved = null;
            RuleHooks.OnTutorialTurnEnded = null;
            RuleHooks.OnTutorialElementPlaced = null;
            RuleHooks.OnTutorialManaGained = null;
            RuleHooks.OnTutorialCreatureTapped = null;
            RuleHooks.OnTutorialCardPlayed = null;
            TideLog.Info("[TutorialGuide] 引导退场");
        }

        // ======================================== 闸实现（RuleHooks 注入） ========================================

        private static string GateAttack(Player player, Entity attacker, Entity target)
        {
            if (player == null || player != GameCore.Instance?.Player1) return null; // 只闸玩家侧

            // 课规（heroLock）：锁卡存活期间玩家不可攻击对方角色——步骤完成后仍生效，锁卡亡即解锁
            if (!string.IsNullOrEmpty(_heroLockCard)
                && target is Player && target == player.Opponent
                && IsCardAliveOnBoard(GameCore.Instance?.Player2, _heroLockCard))
                return $"先解决拦路的 {NameOf(_heroLockCard)}——它存活期间不能攻击对方角色";

            if (!Active) return null; // 步骤全部完成=自由对局（课规已在上方处理）
            var step = Current;
            if (step == null) return null;
            if (step.type == "guard")
                return $"生物保留待守卫（{NameOf(step.guarder)} 将拦截对手的 {NameOf(step.attacker)}）";
            if (step.type == "end")
                return "引导要求结束回合——请结束回合（演示下一环节在对手回合发生）";
            if (step.type != "attack") return null; // 资源步骤不拦攻击（理论上不冲突，保守放行）
            // attack 步骤：精确匹配（错误目标/其余生物一律拒绝——含锁定生物）
            var attackerId = (attacker as Card)?.ID;
            if (attackerId != step.attacker)
                return $"引导要求用 {NameOf(step.attacker)} 攻击（{NameOf(attackerId)} 现在不可操作）";
            var targetOk = (target is Player && step.target == "hero")
                           || (target as Card)?.ID == step.target;
            if (!targetOk)
                return $"引导要求攻击目标：{(step.target == "hero" ? "对方角色" : NameOf(step.target))}";
            return null; // 精确匹配放行（宣言成功后探针完成步骤）
        }

        private static string GateEndTurn(Player player)
        {
            if (!Active || player == null || player != GameCore.Instance?.Player1) return null;
            var step = Current;
            if (step == null) return null;                      // 步骤全部完成=自由
            if (step.type == "end") return null;                // end 本身即引导动作
            if (step.type == "guard") return null;              // 守卫发生在对手回合，不挡
            return $"引导未完成——{step.hint ?? Describe(step)}"; // attack/element/tap/tapcreature/play：先做完引导动作
        }

        private static List<ResponseOption> FilterGuards(Player holder, List<ResponseOption> options)
        {
            if (!Active || holder == null || holder != GameCore.Instance?.Player1) return options;
            var step = Current;
            if (step == null || step.type != "guard") return options; // 非守卫步骤=自由守卫
            // 收紧：守卫步骤中，非引导攻击者的守卫窗口置空=自动放行——
            // 防止玩家把指定守卫者浪费在错误的攻击上（该攻击按剧本落身，如打已横置生物的演示）
            var attackerId = (options.FirstOrDefault(o => o.AttackInstance?.Source != null)
                ?.AttackInstance.Source as Card)?.ID;
            if (attackerId != step.attacker) return new List<ResponseOption>();
            return options.Where(o => o.SourceCard != null && o.SourceCard.ID == step.guarder).ToList();
        }

        private static string GateElement(Player player, Card card)
        {
            if (!Active || player != GameCore.Instance?.Player1) return null;
            var step = Current;
            if (step == null) return null;
            if (step.type == "element")
                return card?.ID == step.card
                    ? null
                    : $"引导要求把 {NameOf(step.card)} 放入元素池当土地（{NameOf(card?.ID)} 现在不可操作）";
            return "引导进行中——暂不可放地";
        }

        private static string GateTap(Player player, PooledCard land, ManaType type)
        {
            if (!Active || player != GameCore.Instance?.Player1) return null;
            var step = Current;
            if (step == null) return null;
            if (step.type == "tap")
            {
                if (land?.SourceCard?.ID != step.land)
                    return $"引导要求横置 {NameOf(step.land)} 产出元素";
                if (!string.IsNullOrEmpty(step.color) && type.ToString() != step.color)
                    return $"引导要求 {NameOf(step.land)} 产出 {ZhColor(type)} 元素";
                return null;
            }
            return "引导进行中——暂不可横置地产元素";
        }

        private static string GateTapCreature(Player player, Card creature)
        {
            if (!Active || player != GameCore.Instance?.Player1) return null;
            var step = Current;
            if (step == null) return null;
            if (step.type == "tapcreature")
                return creature?.ID == step.card
                    ? null
                    : $"引导要求横置 {NameOf(step.card)} 产出元素（{NameOf(creature?.ID)} 现在不可操作）";
            return "引导进行中——暂不可横置生物产元素";
        }

        private static string GatePlay(Player player, Card card)
        {
            if (!Active || player != GameCore.Instance?.Player1) return null;
            var step = Current;
            if (step == null) return null;
            if (step.type == "play")
                return card?.ID == step.card
                    ? null
                    : $"引导要求打出 {NameOf(step.card)}（{NameOf(card?.ID)} 现在不可打出）";
            return "引导进行中——暂不可出牌";
        }

        // ======================================== 探针（步骤完成信号） ========================================

        private static void NotifyAttackDeclared(Player player, Entity attacker, Entity target)
        {
            var step = Current;
            if (step == null || step.type != "attack"
                || player != GameCore.Instance?.Player1
                || (attacker as Card)?.ID != step.attacker)
                return;
            Advance(missed: false);
        }

        private static void NotifyGuardResolved(Card guarder, EffectInstance attack)
        {
            var step = Current;
            if (step == null || step.type != "guard"
                || guarder?.ID != step.guarder
                || (attack?.Source as Card)?.ID != step.attacker)
                return;
            Advance(missed: false);
        }

        /// <summary>漏守兜底：守卫步骤对应的攻击已结算/取消（未被拦截）→ 步骤记 Missed 完成推进。</summary>
        private static void NotifyAttackResolved(Entity attacker, Entity finalTarget)
        {
            var step = Current;
            if (step == null || step.type != "guard" || (attacker as Card)?.ID != step.attacker) return;
            Advance(missed: true);
        }

        /// <summary>end 步骤完成：玩家成功结束回合（GateEndTurn 已放行，此探针推进步骤）。</summary>
        private static void NotifyTurnEnded(Player player)
        {
            var step = Current;
            if (step == null || step.type != "end" || player != GameCore.Instance?.Player1) return;
            Advance(missed: false);
        }

        private static void NotifyElementPlaced(Player player, Card card)
        {
            var step = Current;
            if (step == null || step.type != "element"
                || player != GameCore.Instance?.Player1 || card?.ID != step.card) return;
            Advance(missed: false);
        }

        private static void NotifyManaGained(Player player, Card landCard, ManaType type)
        {
            var step = Current;
            if (step == null || step.type != "tap"
                || player != GameCore.Instance?.Player1 || landCard?.ID != step.land) return;
            if (!string.IsNullOrEmpty(step.color) && type.ToString() != step.color) return;
            Advance(missed: false);
        }

        private static void NotifyCreatureTapped(Player player, Card creature)
        {
            var step = Current;
            if (step == null || step.type != "tapcreature"
                || player != GameCore.Instance?.Player1 || creature?.ID != step.card) return;
            Advance(missed: false);
        }

        private static void NotifyCardPlayed(Player player, Card card)
        {
            var step = Current;
            if (step == null || step.type != "play"
                || player != GameCore.Instance?.Player1 || card?.ID != step.card) return;
            Advance(missed: false);
        }

        private static void Advance(bool missed)
        {
            if (missed && _index >= 0 && _index < _missed.Count) _missed[_index] = true;
            _index++;
            var done = _index >= _steps.Count;
            if (done) Active = false; // 步骤闸退场；课规（heroLock）随钩子留存到 End()
            TideLog.Info($"[TutorialGuide] 步骤 {_index}/{_steps.Count} 完成{(missed ? "（漏做/未按引导）" : "")}——"
                         + (done ? "引导步骤全部完成（课规仍生效）" : $"下一步 {Describe(_steps[_index])}"));
            OnStepAdvanced?.Invoke(_index);
        }

        // ======================================== 小件 ========================================

        private static bool IsCardAliveOnBoard(Player owner, string cardId)
        {
            if (owner == null || string.IsNullOrEmpty(cardId)) return false;
            return (GameCore.Instance?.ZoneManager?.GetCards(owner, Zone.Battlefield) ?? new List<Card>())
                .Any(c => c != null && c.IsAlive && c.ID == cardId);
        }

        private static string Describe(TutorialGuideStepSpec s)
        {
            switch (s.type)
            {
                case "guard": return $"guard：用 {NameOf(s.guarder)} 守卫对手的 {NameOf(s.attacker)}（{s.hint}）";
                case "end": return $"end：结束回合（{s.hint}）";
                case "element": return $"element：把 {NameOf(s.card)} 放入元素池当土地（{s.hint}）";
                case "tap": return $"tap：横置 {NameOf(s.land)} 产出 {s.color}（{s.hint}）";
                case "tapcreature": return $"tapcreature：横置 {NameOf(s.card)} 产色（{s.hint}）";
                case "play": return $"play：打出 {NameOf(s.card)}（{s.hint}）";
                default: return $"attack：用 {NameOf(s.attacker)} 攻击 {(s.target == "hero" ? "对方角色" : NameOf(s.target))}（{s.hint}）";
            }
        }

        private static string ZhColor(ManaType type)
        {
            switch (type)
            {
                case ManaType.Red: return "红";
                case ManaType.Blue: return "蓝";
                case ManaType.Green: return "绿";
                case ManaType.White: return "白";
                case ManaType.Black: return "黑";
                default: return "灰";
            }
        }

        private static string NameOf(string cardId)
            => string.IsNullOrEmpty(cardId) ? "?" : CardCatalog.GetById(cardId)?.CardName ?? cardId;
    }
}
