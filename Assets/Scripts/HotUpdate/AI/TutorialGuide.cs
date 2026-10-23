using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;

namespace SynergyUI
{
    /// <summary>
    /// 引导步骤规格（TutorialConfig.guide 条目）：
    /// intro——纯讲解步（2026-10-16 教学一两段式）：闸住玩家一切操作，UI 点击引导横幅确认推进
    ///   （NotifyIntroConfirmed）；带 audio 字段（2026-10-17 配音改版）=介绍段配音卡片——
    ///   暂停画面+大字文案+播放 StreamingAssets/TutorialAudio/{audio}.mp3+【确定】钮（无 audio=文字卡片）；
    ///   reveal=演出指令串（语音联动地块显隐：BattleRevealDirector.Solo 独占显示语音描述的区块；
    ///   midTransition=第二段注入）；
    /// attack——玩家须用 attacker 宣言攻击 target（"hero"=对方角色 / 卡 id=场上单位）；
    ///   attacker 可空（2026-10-17 宽松步）：任意己方生物攻击 target 即推进；aiGuard=true 时
    ///   该攻击的响应窗由 AI 选守卫（守卫演示：目标被改写——AI 应答器消费，逻辑层只透传）；
    /// guard——对手 attacker 宣言攻击时玩家须用 guarder 守卫拦截；
    /// end——强制结束回合（此步进行中一切攻击被拒）；
    /// element——玩家须把 card（手牌）放入元素池当地牌（target 可选=锁定产色名，2026-10-11 锁色定案）；
    ///   card 可空（宽松）：任意手牌入池即推进；
    /// tap——已退役（2026-10-11 现取现用：地牌横置=付费步系统自动动作，无手动产色步骤）；
    /// tapcreature——玩家须横置 card（带 LandTrait 的生物）产色（临时元素）；
    /// play——玩家须从手牌打出 card；card 可空（宽松）：任意生物召唤成功即推进（引擎费用闸
    ///   自然逼玩家挑付得起的——「看费用」本身是教学点）；
    /// activate——玩家须发动 card（场上单位/角色）的主动效果（2026-10-23 元素合成步：
    ///   发动入栈成功即推进=RuleHooks.OnTutorialEffectActivated 探针，GameActions.ActivateEffect
    ///   主路径尾部发射）；card 可空（宽松）：任意主动效果发动即推进；其余引导步期间主动效果
    ///   发动被闸（TutorialActivateGate——防提前横置烧掉步骤资源）；
    /// respond——玩家须完成一次响应窗口决策（2026-10-19 第二课连锁教学）：AI 宣动后响应窗交给
    ///   玩家（连锁打出/发动或点「不连锁」让过）即推进——探针=OnTutorialHumanResponded；
    ///   窗口未开（玩家无可连锁项被自动让过）时由「对手回合结束」兜底记 Missed 推进（防死锁）；
    /// fieldclear——自由清场步（2026-10-19 教学二段一改版）：闸全放行（玩家自由操作，用卡顺序
    ///   不再钉死——打错了靠「重置考验」重来），对手场上存活生物清零即完成；探针=PollFieldClear
    ///   （UI 侧逐帧轮询，栈收口后调用）；
    /// luckuse——自由等待步（2026-10-20 教学二）：闸全放行。判据分档（PollLuckUse）：场上有运势
    ///   引擎单位=运势段口径（全部掷毕=横置且无待发主动奖励）；场上无运势单位=自由等待——己方卡
    ///   的引擎奖励**真实触发**（KeywordAppliedEvent「元素充盈/团结」发布点与 LaunchReward
    ///   同帧，段六/六b）或等待中击杀对手（终段「击杀对手」/段六备用击杀线）即闩锁完成；
    ///   都不满足=不推进；
    /// deathtoll——自由死亡计数步（2026-10-20 教学二段五改版）：闸全放行（谁攻击谁不钉死，
    ///   玩家自由分配攻击者与目标），死亡计数引擎在计数 ≥ 6 时触发过门槛奖励（触发即满值、
    ///   奖励伤害随栈收口结算完毕）即完成；探针=PollDeathToll 消费 KeywordAppliedEvent 触发
    ///   捕获（UI 侧逐帧轮询，栈收口后调用——判据是真实触发而非单纯计数=6：门槛 x 随锚价推导
    ///   无上限钳，调价漂移>6 时计数满 6 也不触发，此步不放行）；
    /// victory——自由挑战收尾步（2026-10-16）：闸全放行（玩家自由操作），对局胜利
    ///   （GameOverEvent 玩家胜）时步骤完成=引导全部完成。
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
        public string card;   // element/tapcreature/play 的卡 id（空=宽松任意）
        public string land;   // tap 的地牌源卡 id
        public string color;  // tap 的产出颜色（"Gray"/"Red"/"Blue"/"Green"/"White"/"Black"）
        public string reveal; // intro 的演出指令（语音联动显隐 zone:*/board/all/hand/units；逗号分隔多令牌
                              // 逐个消费（2026-10-19 教学二段一：units,hand/all）；midTransition=换场注入）
        public string audio;  // intro 的配音文件名（StreamingAssets/TutorialAudio/{audio}.mp3；空=文字卡片不播语音）
        public bool aiGuard;  // attack 步标记（2026-10-17 守卫演示）：此攻击的响应窗 AI 选守卫（目标改写演出）；
                              // 宣言不推进——结算/取消探针才算完成（UI 侧等战斗表现收尾再弹解说卡）
        public bool aiTurnOnConfirm; // intro 步标记（2026-10-17 考验改版）：确定后把回合交给对手——
                              // midTransition 全清重铺后=对手进攻回合（BattleScreen 复用 OnEndTurnFan 序列）
        public bool waitTurnStart; // end 步标记（2026-10-20 倒计时段）：点击结束回合**不立即推进**——
                              // 等玩家**下个回合开始**（TurnStartEvent）才完成。段切换（midTransition）重铺
                              // 会吞掉 pending 的回合翻转，倒计时归零直发的报酬必须先在回合开始落地再换场。
    }

    /// <summary>
    /// 教学引导状态机（纯逻辑层——暂停引导的高亮/弹窗 UI 后续接）：
    /// 步骤未完成时把对局"钉住"（暂停语义）——
    /// · 攻击闸：attack 步骤只放行「引导攻击者×引导目标」；guard 步骤期间生物保留待命；
    ///   end 步骤期间强制结束回合；**课规 heroLockCard（第二课）：该卡存活期间玩家不可攻击对方角色**
    ///   （步骤完成后仍然生效——boss 亡即解锁；法术直伤不在此列）；intro 步骤期间攻击全拒（讲解中）；
    ///   victory 步骤攻击全放行（自由挑战收尾）；
    /// · 结束回合闸：attack 步骤未完成禁走（intro/资源步骤同理；guard/victory 放行）；
    /// · 守卫候选过滤：guard 步骤只留引导守卫者，非引导攻击者的窗口置空自动放行；
    /// · 资源闸（第二课）：element/tap/tapcreature/play 步骤分别钉住放地/产色/生物产色/出牌
    ///   （victory 步骤全放行）；
    /// · intro（2026-10-16 教学一两段式）：讲解步闸住一切操作，UI 点击横幅调 NotifyIntroConfirmed 推进；
    /// · respond（2026-10-19 第二课连锁教学）：无闸纯等待步——AI 宣动后响应窗交到玩家（连锁/让过）
    ///   即完成（OnTutorialHumanResponded 探针）；窗口没开时由对手回合结束兜底（Missed 推进）；
    /// · victory（同上）：闸全放行，GameCore 终局发布探针（OnTutorialGameOver）玩家胜时推进=引导完成。
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
        /// <summary>玩家胜利已发生（多段尾段追赶：击杀先于 victory 步到达时由 Advance 补结算）。</summary>
        private static bool _victorySeen;
        /// <summary>end 步等待中（waitTurnStart，2026-10-20 倒计时段）：已点结束回合，
        /// 推进挪到玩家下个回合开始（NotifyTurnStarted）——防段切换重铺吞掉 pending 回合翻转。</summary>
        private static bool _endWaitNextTurn;

        /// <summary>deathtoll 步触发捕获（2026-10-20 教学二死亡计数段）：死亡计数引擎在计数 ≥ 6 时
        /// 触发过门槛奖励（KeywordAppliedEvent——发布点与 LaunchReward 同帧，故"已触发=伤害在途"）。
        /// Begin/JumpTo 清零（段重置防陈旧闩锁误过）。</summary>
        private static bool _deathTollSixFired;

        /// <summary>luckuse 步引擎触发捕获（2026-10-20 教学二段六/六b）：己方卡的引擎奖励真实触发
        ///（KeywordAppliedEvent「元素充盈/团结」——发布点与 LaunchReward 同帧，触发=奖励在途）。
        /// 消费即清（PollLuckUse 推进时）+ Begin/JumpTo 清零——防上一步期间的历史触发误过本步。</summary>
        private static bool _engineRewardFired;

        /// <summary>luckuse 步击杀捕获（2026-10-20 教学二终段「击杀对手」）：等待步在位时玩家胜利
        ///（NotifyGameOver 探针）即闩锁。终段之后无换场步、IsScriptedContinuation=false 走正常终局
        /// 弹窗（引导完成与否不拦终局）；本闩锁主要兜「等待步中把对手打死」的备用击杀线（如段六
        /// 不打成长改磨脸 3 血 AI——弹窗被跳过、全靠轮询推进到换场步续接，无此闩锁=死锁终局画面）。
        /// 消费即清 + Begin/JumpTo 清零。</summary>
        private static bool _luckKillFired;

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
        /// 重复 Begin 先 End 防钩子泄漏）。guideOverride（2026-10-16 教学一跳过介绍）：非空时替代
        /// tutorial.guide 装链（skip 模式传 tutorial.guideSkip 直入第二段）。</summary>
        public static void Begin(TutorialConfig tutorial, List<TutorialGuideStepSpec> guideOverride = null)
        {
            End();
            _steps.Clear();
            _missed.Clear();
            _victorySeen = false;
            _endWaitNextTurn = false;
            _deathTollSixFired = false;
            _engineRewardFired = false;
            _luckKillFired = false;
            foreach (var s in guideOverride ?? tutorial?.guide ?? new List<TutorialGuideStepSpec>())
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
            RuleHooks.TutorialActivateGate = GateActivate;
            RuleHooks.OnTutorialEffectActivated = NotifyEffectActivated;
            RuleHooks.OnTutorialAttackDeclared = NotifyAttackDeclared;
            RuleHooks.OnTutorialAttackResolved = NotifyAttackResolved;
            RuleHooks.OnTutorialGuardResolved = NotifyGuardResolved;
            RuleHooks.OnTutorialTurnEnded = NotifyTurnEnded;
            RuleHooks.OnTutorialElementPlaced = NotifyElementPlaced;
            RuleHooks.OnTutorialManaGained = NotifyManaGained;
            RuleHooks.OnTutorialCreatureTapped = NotifyCreatureTapped;
            RuleHooks.OnTutorialCardPlayed = NotifyCardPlayed;
            RuleHooks.OnTutorialGameOver = NotifyGameOver;
            RuleHooks.OnTutorialHumanResponded = NotifyHumanResponded;
            EventManager.Instance.Subscribe<TurnStartEvent>(NotifyTurnStarted); // end 步 waitTurnStart 推进口（2026-10-20）
            EventManager.Instance.Subscribe<KeywordAppliedEvent>(OnKeywordApplied); // deathtoll 步触发捕获（2026-10-20）

            if (Active)
                TideLog.Info($"[TutorialGuide] 引导开始：{_steps.Count} 步——第 1 步 {Describe(_steps[0])}");
            else if (!string.IsNullOrEmpty(_heroLockCard))
                TideLog.Info($"[TutorialGuide] 无引导步骤，仅课规：{NameOf(_heroLockCard)} 存活期间不可攻击对方角色");
            else
                return; // 既无步骤也无课规：不占钩子
            OnStepAdvanced?.Invoke(0);
        }

        /// <summary>跳段重入（2026-10-19 常驻重置按钮·分段回退）：跳到指定步重新成为当前步——
        /// 不重装闸（Begin 已装、跨段有效），重发步事件驱动 UI 消费（midTransition 步会重新换场：
        /// ApplyRuntime 幂等，导演侧按段落账去重）。越界钳到 [0, Count-1]。</summary>
        public static void JumpTo(int index)
        {
            if (_steps.Count == 0) return;
            _index = System.Math.Max(0, System.Math.Min(index, _steps.Count - 1));
            _deathTollSixFired = false; // 段重置重入：清陈旧闩锁（重铺后计数归零，触发须重新挣得）
            _engineRewardFired = false; // 同上：重铺后引擎状态归零，触发须重新挣得
            _luckKillFired = false; // 同上：段重置后击杀须重新达成
            while (_missed.Count < _steps.Count) _missed.Add(false);
            while (_missed.Count > _steps.Count) _missed.RemoveAt(_missed.Count - 1);
            Active = true;
            OnStepAdvanced?.Invoke(_index);
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
            _endWaitNextTurn = false;
            _deathTollSixFired = false;
            _engineRewardFired = false;
            _luckKillFired = false;
            EventManager.Instance.Unsubscribe<TurnStartEvent>(NotifyTurnStarted);
            EventManager.Instance.Unsubscribe<KeywordAppliedEvent>(OnKeywordApplied);
            RuleHooks.TutorialAttackGate = null;
            RuleHooks.TutorialEndTurnGate = null;
            RuleHooks.TutorialGuardFilter = null;
            RuleHooks.TutorialElementGate = null;
            RuleHooks.TutorialTapGate = null;
            RuleHooks.TutorialTapCreatureGate = null;
            RuleHooks.TutorialPlayGate = null;
            RuleHooks.TutorialActivateGate = null;
            RuleHooks.OnTutorialEffectActivated = null;
            RuleHooks.OnTutorialAttackDeclared = null;
            RuleHooks.OnTutorialAttackResolved = null;
            RuleHooks.OnTutorialGuardResolved = null;
            RuleHooks.OnTutorialTurnEnded = null;
            RuleHooks.OnTutorialElementPlaced = null;
            RuleHooks.OnTutorialManaGained = null;
            RuleHooks.OnTutorialCreatureTapped = null;
            RuleHooks.OnTutorialCardPlayed = null;
            RuleHooks.OnTutorialGameOver = null;
            RuleHooks.OnTutorialHumanResponded = null;
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
            if (step.type == "victory" || step.type == "fieldclear" || step.type == "luckuse" || step.type == "deathtoll") return null; // 自由步：攻击全放行（探针收尾）
            if (step.type == "intro")
                return "引导讲解中——先读完提示（点击横幅继续）";
            if (step.type == "guard")
                return $"生物保留待守卫（{NameOf(step.guarder)} 将拦截对手的 {NameOf(step.attacker)}）";
            if (step.type == "end")
                return "引导要求结束回合——请结束回合（演示下一环节在对手回合发生）";
            if (step.type == "respond") return null; // 响应步=对手回合（本侧攻击不可达，保守放行）
            if (step.type != "attack") // 资源步骤拦攻击（2026-10-23 收紧：预铺生物在场后步骤期乱攻击
                return "引导未完成——按提示完成当前步骤后再攻击"; // 会横置掉后续步骤要用的单位=软锁；自由步已在上方放行）
            // attack 步：attacker 空=宽松（任意己方生物）；target 精确匹配（错误目标/其余生物一律拒绝）
            var attackerId = (attacker as Card)?.ID;
            if (!string.IsNullOrEmpty(step.attacker) && attackerId != step.attacker)
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
            if (step.type == "respond") return null;            // 响应步发生在对手回合宣动后，放行（回合交接是它的前置）
            if (step.type == "victory" || step.type == "fieldclear" || step.type == "luckuse" || step.type == "deathtoll") return null; // 自由步：回合流转放行
            return $"引导未完成——{step.hint ?? Describe(step)}"; // intro/attack/element/tap/tapcreature/play：先做完引导动作
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
            if (step.type == "victory" || step.type == "fieldclear" || step.type == "luckuse" || step.type == "deathtoll") return null; // 自由步：放行
            if (step.type == "element")
                return string.IsNullOrEmpty(step.card) || card?.ID == step.card
                    ? null // 宽松（card 空=任意）或精确匹配
                    : $"引导要求把 {NameOf(step.card)} 放入元素池当土地（{NameOf(card?.ID)} 现在不可操作）";
            return "引导进行中——暂不可放地";
        }

        private static string GateTap(Player player, PooledCard land, ManaType type)
        {
            if (!Active || player != GameCore.Instance?.Player1) return null;
            var step = Current;
            if (step == null) return null;
            if (step.type == "victory" || step.type == "fieldclear" || step.type == "luckuse" || step.type == "deathtoll") return null; // 自由步：放行
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
            if (step.type == "victory" || step.type == "fieldclear" || step.type == "luckuse" || step.type == "deathtoll") return null; // 自由步：放行
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
            if (step.type == "victory" || step.type == "fieldclear" || step.type == "luckuse" || step.type == "deathtoll") return null; // 自由步：放行
            if (step.type == "play")
                return string.IsNullOrEmpty(step.card) || card?.ID == step.card
                    ? null // 宽松（card 空=任意——引擎费用闸自然逼玩家挑付得起的）或精确匹配
                    : $"引导要求打出 {NameOf(step.card)}（{NameOf(card?.ID)} 现在不可打出）";
            return "引导进行中——暂不可出牌";
        }

        /// <summary>主动效果发动闸（2026-10-23 元素合成步）：activate 步精确放行本卡、其余引导步拦截
        ///（防提前烧掉步骤资源：发动即横置，横置后 activate 步软锁）。GameActions.ActivateEffect 主路径消费。</summary>
        private static string GateActivate(Player player, Card card, EffectDefinition effect)
        {
            if (!Active || player != GameCore.Instance?.Player1) return null;
            var step = Current;
            if (step == null) return null;
            if (step.type == "victory" || step.type == "fieldclear" || step.type == "luckuse" || step.type == "deathtoll") return null; // 自由步：放行
            if (step.type == "activate")
                return string.IsNullOrEmpty(step.card) || card?.ID == step.card
                    ? null // 宽松（card 空=任意主动效果）或精确匹配
                    : $"引导要求发动 {NameOf(step.card)} 的主动效果（{NameOf(card?.ID)} 现在不可发动）";
            return "引导进行中——暂不可发动主动效果";
        }

        // ======================================== 探针（步骤完成信号） ========================================

        private static void NotifyAttackDeclared(Player player, Entity attacker, Entity target)
        {
            var step = Current;
            if (step == null || step.type != "attack"
                || player != GameCore.Instance?.Player1
                || (!string.IsNullOrEmpty(step.attacker) && (attacker as Card)?.ID != step.attacker))
                return; // attacker 空=宽松（任意己方生物宣言成功即推进）
            if (step.aiGuard) return; // 守卫演示步（2026-10-17 考验改版）：宣言帧不推进——等结算探针（战斗先演完）
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

        /// <summary>攻击结算/取消探针（CombatSystem 两条路径均到）：guard 步=漏守兜底（未被拦截）记
        /// Missed 完成推进；attack+aiGuard（守卫演示步，2026-10-17 考验改版）=战斗已结算即完成——
        /// 宣言帧不推进，解说卡由 UI 侧等战斗表现收尾后再弹。</summary>
        private static void NotifyAttackResolved(Entity attacker, Entity finalTarget)
        {
            var step = Current;
            if (step == null) return;
            if (step.type == "guard")
            {
                if ((attacker as Card)?.ID == step.attacker) Advance(missed: true);
                return;
            }
            if (step.type == "attack" && step.aiGuard
                && (string.IsNullOrEmpty(step.attacker) || (attacker as Card)?.ID == step.attacker))
                Advance(missed: false);
        }

        /// <summary>end 步骤完成：玩家成功结束回合（GateEndTurn 已放行，此探针推进步骤）；
        /// respond 步骤兜底（2026-10-19 连锁教学）：对手回合结束=响应窗已不可能再开（玩家无可
        /// 连锁项被自动让过等）——记 Missed 完成推进，防死锁。
        /// waitTurnStart（2026-10-20 倒计时段）：不在此推进——挂起等玩家下个回合开始
        ///（NotifyTurnStarted），保证段切换重铺前回合翻转完整走完、倒计时报酬落地。</summary>
        private static void NotifyTurnEnded(Player player)
        {
            var step = Current;
            if (step == null) return;
            if (step.type == "end" && player == GameCore.Instance?.Player1)
            {
                if (step.waitTurnStart)
                {
                    _endWaitNextTurn = true; // 幂等：双发 EndTurn 只置一次
                    return;
                }
                Advance(missed: false);
                return;
            }
            if (step.type == "respond" && player == GameCore.Instance?.Player2)
                Advance(missed: true);
        }

        /// <summary>end 步等待推进口（waitTurnStart，2026-10-20 倒计时段）：玩家下个回合开始
        ///（回合翻转含对手回合完整走完）才完成 end 步——此时段切换重铺不再有 pending 翻转可吞。</summary>
        private static void NotifyTurnStarted(TurnStartEvent e)
        {
            if (!_endWaitNextTurn) return;
            if (e == null || e.TurnPlayer != GameCore.Instance?.Player1) return;
            _endWaitNextTurn = false;
            var step = Current;
            if (step != null && step.type == "end") Advance(missed: false);
        }

        private static void NotifyElementPlaced(Player player, Card card)
        {
            var step = Current;
            if (step == null || step.type != "element"
                || player != GameCore.Instance?.Player1
                || (!string.IsNullOrEmpty(step.card) && card?.ID != step.card)) return; // card 空=宽松任意
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
                || player != GameCore.Instance?.Player1
                || (!string.IsNullOrEmpty(step.card) && card?.ID != step.card)) return; // card 空=宽松任意
            Advance(missed: false);
        }

        /// <summary>activate 步探针（2026-10-23 元素合成步）：玩家侧主动效果发动入栈成功
        ///（GameActions.ActivateEffect 主路径尾部 RuleHooks 发射）即推进——元素合成无目标无连锁等待，
        /// 宣言即教学事实完成；任意元素入账紧随其后。</summary>
        private static void NotifyEffectActivated(Player player, Card source)
        {
            var step = Current;
            if (step == null || step.type != "activate"
                || player == null || player != GameCore.Instance?.Player1) return;
            if (!string.IsNullOrEmpty(step.card) && source?.ID != step.card) return; // card 空=宽松任意
            Advance(missed: false);
        }

        /// <summary>intro 步推进口（2026-10-16 两段式）：UI 引导横幅被点击时调用——
        /// 当前步为 intro 即完成推进；其余步骤忽略（误点无害）。</summary>
        public static void NotifyIntroConfirmed()
        {
            var step = Current;
            if (step == null || step.type != "intro") return;
            Advance(missed: false);
        }

        /// <summary>victory 步探针（GameCore.PublishGameOverOnce 发射）：玩家胜利=步骤完成（引导全部完成）；
        /// 玩家失败=对局已终（步骤停留，回菜单重开教学局即重来——终局呈现由 BattleScreen 负责）。
        /// 多段尾段（2026-10-19）：击杀可能发生在 intro 步在位期（段尾自动引擎发奖不等点击）——
        /// 胜利先到、victory 步后到时由 Advance 追赶补结算（_victorySeen）。</summary>
        private static void NotifyGameOver(Player winner)
        {
            if (winner != null && winner == GameCore.Instance?.Player1)
            {
                _victorySeen = true;
                var cur = Current;
                if (cur != null && cur.type == "luckuse")
                    _luckKillFired = true; // 等待步在位击杀（终段「击杀对手」/段六备用击杀线）
            }
            var step = Current;
            if (step == null || step.type != "victory") return;
            if (winner == null || winner != GameCore.Instance?.Player1) return;
            Advance(missed: false);
        }

        /// <summary>respond 步探针（2026-10-19 第二课连锁教学）：人类玩家的响应窗口决策完成
        /// （连锁或让过都算——窗口被交到玩家手上即为本课的体验点）。</summary>
        private static void NotifyHumanResponded(Player player)
        {
            var step = Current;
            if (step == null || step.type != "respond"
                || player == null || player != GameCore.Instance?.Player1) return;
            Advance(missed: false);
        }

        /// <summary>fieldclear 步探针（2026-10-19 教学二段一自由操作）：对手场上存活生物清零即完成
        /// ——UI 侧逐帧轮询（栈收口、表现播毕后才调，防结算中途的瞬态空场误判）。结界等非生物
        /// 驻场不算数（判据=存活生物，非战场总数）。非 fieldclear 步在位时零开销直返。</summary>
        public static void PollFieldClear()
        {
            var step = Current;
            if (step == null || step.type != "fieldclear") return;
            var board = GameCore.Instance?.ZoneManager?
                .GetCards(GameCore.Instance?.Player2, Zone.Battlefield);
            if (board == null || board.Any(c => c != null && c.IsAlive
                    && CardCatalog.GetById(c.ID)?.Supertype == Cardtype.Creature))
                return;
            Advance(missed: false);
        }

        /// <summary>luckuse 步探针（2026-10-20 教学二运势段）：**双判据**自由等待步——
        /// ① 场上有运势引擎单位（段三b）：己方运势单位全部用毕即完成——全部已横置（两段式第一击
        /// 掷骰即横置，掷不中也算用毕）且无它们来源的待发主动奖励（掷达标的第二击「发动」完毕）；
        /// ② 场上无运势单位（段六元素充盈/段六b团结复用本步型）：己方卡引擎奖励**真实触发**
        ///（_engineRewardFired 闩锁——KeywordAppliedEvent 与 LaunchReward 同帧，触发=奖励在途）
        /// 即完成。UI 侧逐帧轮询（栈收口、表现播毕后才调，防结算中途瞬态误判）。两判据都不满足
        /// 不推进（等待本身即教学点：成长未打满/序位未到第 x 个=效果未触发）。非 luckuse 步零开销直返。</summary>
        public static void PollLuckUse()
        {
            var step = Current;
            if (step == null || step.type != "luckuse") return;
            var core = GameCore.Instance;
            var zm = core?.ZoneManager;
            var p1 = core?.Player1;
            if (zm == null || p1 == null || core?.StackEngine == null) return;
            var units = new List<Card>();
            foreach (var c in zm.GetCards(p1, Zone.Battlefield))
                if (c != null && c.IsAlive && HasLuckEngine(c)) units.Add(c);
            if (units.Count == 0)
            {
                // 判据②：引擎触发/自由击杀等待（无运势单位=非运势段复用步型）——
                // 成长三张打满充盈触发、序位第 6 个发动、或等待中直接击杀（备用线）任一达成即完成
                if (_engineRewardFired || _luckKillFired)
                {
                    _engineRewardFired = false; // 消费即清：防本步完成前的历史触发误过下个等待步
                    _luckKillFired = false;
                    Advance(missed: false);
                }
                return;
            }
            foreach (var c in units)
                if (!c.IsTapped()) return; // 有未掷骰的（第一击未做）
            foreach (var pe in core.StackEngine.GetActivatableVoluntaryEffects(p1))
                if (pe != null && units.Any(u => ReferenceEquals(pe.Source, u))) return; // 有待发奖励未发动（第二击未做）
            Advance(missed: false);
        }

        /// <summary>卡上是否携带运势引擎载荷（数据级直扫——扁平原子与节点化步骤双查，不做效果转换）。</summary>
        private static bool HasLuckEngine(Card card)
        {
            var data = (card as CardWrapper)?.GetData();
            if (data?.Effects == null) return false;
            foreach (var fx in data.Effects)
            {
                if (fx?.AtomicEffects != null)
                    foreach (var atom in fx.AtomicEffects)
                        if (IsLuckBranch(atom?.branch)) return true;
                if (fx.Steps != null)
                    foreach (var s in fx.Steps)
                        if (IsLuckBranch(s?.atomic?.branch)) return true;
            }
            return false;
        }

        private static bool IsLuckBranch(BranchEntryData b)
            => b != null
               && (BranchSettleKind)b.settle == BranchSettleKind.Engine
               && (BranchEngineKind)b.engine == BranchEngineKind.LuckRoll;

        /// <summary>deathtoll 步探针（2026-10-20 教学二死亡计数段）：死亡计数引擎在计数 ≥ 6 时
        /// 触发过门槛奖励（OnKeywordApplied 捕获的闩锁）即完成。判据=真实触发而非单纯计数=6：
        /// 门槛 x 随 Then 锚价推导且无上限钳（CostDerivationService.RewardThreshold 仅下限 1），
        /// 原子调价漂到 >6 时计数满 6 也永不触发——此步不放行（与「成功触发并造成伤害」判据一致）。
        /// 奖励伤害到 UI 轮询时点（栈收口、表现播毕后才调）已结算；计数官被打死换区（指示物清零、
        /// 引擎随墓失效）则再也触发不了——打错路线靠「重置考验」重来。非 deathtoll 步零开销直返。</summary>
        public static void PollDeathToll()
        {
            var step = Current;
            if (step == null || step.type != "deathtoll") return;
            if (!_deathTollSixFired) return;
            Advance(missed: false);
        }

        /// <summary>KeywordAppliedEvent 订阅（2026-10-20 教学二死亡计数段）：死亡计数引擎触发门槛
        /// 奖励时 BranchEngines 与 LaunchReward 同帧发布（Keyword="死亡计数"，全库唯一发布点）——
        /// 触发时读卡上计数 ≥ 6 才闩锁（门槛若被调价漂低，计数 5 的早发不算——判据钉「计数 6」）。
        /// luckuse 引擎等待（2026-10-20 教学二段六/六b）：己方卡的「元素充盈/团结」奖励触发
        /// 同帧发布——闩锁 _engineRewardFired（发布点紧邻 LaunchReward，已触发=奖励在途）。
        /// 步型守卫（2026-10-20 段六b 跳步修复）：闩锁只认**对应等待步在位**时的触发——
        /// 段六六连充盈在 end 步挂起期（waitTurnStart·对手回合）触发，无守卫时 _engineRewardFired
        /// 携 true 跨步存活，段六b luckuse 一上位即被 PollLuckUse 消费跳步（团结兽从未等玩家摆，
        /// 确定键连点直接换场）。deathtoll 同理：其他段位期间的死亡计数触发不得闩锁本步。</summary>
        private static void OnKeywordApplied(KeywordAppliedEvent e)
        {
            if (e == null || e.Target == null) return;
            var step = Current;
            if (step == null) return;
            if (step.type == "deathtoll"
                && e.Keyword == "死亡计数"
                && e.Target.GetCounterCount(CardCore.Attribute.CounterRules.DeathTollCounter) >= 6)
                _deathTollSixFired = true;
            if (step.type == "luckuse"
                && (e.Keyword == "元素充盈" || e.Keyword == "团结")
                && e.Target.GetController() == GameCore.Instance?.Player1)
                _engineRewardFired = true;
        }

        private static void Advance(bool missed)
        {
            if (missed && _index >= 0 && _index < _missed.Count) _missed[_index] = true;
            _index++;
            // 胜利先到步后到（多段尾段）：击杀发生时在位的是 intro 步——进入 victory 步时补结算
            if (Active && _victorySeen && _index >= 0 && _index < _steps.Count
                && _steps[_index] != null && _steps[_index].type == "victory")
            {
                _index++; // victory 步视为完成
                TideLog.Info("[TutorialGuide] 追赶补结算：终局先于 victory 步到达——视为完成");
            }
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
                case "intro": return $"intro：{s.hint}（reveal={s.reveal ?? "无"}）";
                case "guard": return $"guard：用 {NameOf(s.guarder)} 守卫对手的 {NameOf(s.attacker)}（{s.hint}）";
                case "end": return $"end：结束回合（{s.hint}）";
                case "element": return $"element：把 {NameOf(s.card)} 放入元素池当土地（{s.hint}）";
                case "tap": return $"tap：横置 {NameOf(s.land)} 产出 {s.color}（{s.hint}）";
                case "tapcreature": return $"tapcreature：横置 {NameOf(s.card)} 产色（{s.hint}）";
                case "play": return $"play：打出 {NameOf(s.card)}（{s.hint}）";
                case "activate": return $"activate：发动 {NameOf(s.card)} 的主动效果（{s.hint}）";
                case "respond": return $"respond：完成一次响应窗口决策（连锁或让过）（{s.hint}）";
                case "fieldclear": return $"fieldclear：自由操作清空对手全场生物（{s.hint}）";
                case "luckuse": return $"luckuse：自由等待（运势用毕/引擎奖励触发）（{s.hint}）";
                case "deathtoll": return $"deathtoll：自由攻击至死亡计数满 6 触发奖励（{s.hint}）";
                case "victory": return $"victory：消灭对手完成教学（{s.hint}）";
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

        /// <summary>卡 id → 卡名（UI 提示共用——2026-10-14 引导横幅/点击前置提示）。</summary>
        public static string NameOf(string cardId)
            => string.IsNullOrEmpty(cardId) ? "?" : CardCatalog.GetById(cardId)?.CardName ?? cardId;
    }
}
