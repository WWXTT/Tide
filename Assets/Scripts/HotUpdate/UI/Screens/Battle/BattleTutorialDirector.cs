using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.AI;
using Tide.HotUpdate;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SynergyUI
{
    /// <summary>
    /// 教学导演（2026-10-18 BattleScreen 功能拆分第六块外迁·最后一块）：教学局开局装配
    /// （StartGame=卡组/剧本 AI/引导链/守卫应答器）、介绍段配音卡片（TipPanel+语音）、
    /// 引导步推进消费（reveal 显隐指令/过场交回合）、教学横幅（toast 底层·HookBanner 订阅
    /// TutorialGuide.OnStepAdvanced）、终局分发（Retry/Complete 弹窗+重开序列）。
    /// 纯 C# 类——全部依赖经 ctor Func/Action 注入（CombatChoreographer 先例）；静态事件
    /// 订阅自管生命周期（UnhookBanner=OnExit 首段退订——僵尸订阅教训 2026-10-17）。
    /// </summary>
    internal sealed class BattleTutorialDirector
    {
        private readonly Func<RectTransform> _root;
        private readonly Func<GameCore> _core;
        private readonly Func<BattleController> _ctrl;
        private readonly Func<BattleFieldView> _field;
        private readonly BattleToastHub _toast;
        private readonly Func<string, string, string, UniTask<bool?>> _showTip;
        private readonly Action _back;
        private readonly Action _refreshField;
        private readonly Action _refreshFans;
        private readonly Action _endTurnFan;
        private readonly Func<bool> _gameEnded;
        private readonly Action<bool> _setGameEnded;

        private RectTransform Root => _root();
        private GameCore Core => _core();
        private BattleController Ctrl => _ctrl();
        private BattleFieldView Field => _field();

        // ---- 段落账（2026-10-19 教学二多段化：midTransition 依次换场） ----
        // 段号 0=scenario 首段；1=skipScenario（首个换场步）；n≥2=phaseScenarios[n-2]。
        // _phaseStarts[n]=第 n 段的引导步号（常驻重置按钮按段回退的落点）。
        private int _phaseIndex;
        private readonly List<int> _phaseStarts = new List<int> { 0 };

        /// <summary>脚本化续接（2026-10-19）：终局发生但引导链上仍有换场步（当前步起向后任一
        /// reveal=midTransition）——上段考验的预设击杀（教学二段二最后一发），由换场步异步续接
        /// （终局撤销+重铺下一段）；BattleScreen.OnGameOverFan 据此跳过终局弹窗与过课落档。
        /// 尾段击杀（后续无换场步）=真正的课程终局，正常弹窗落档。</summary>
        public bool IsScriptedContinuation
        {
            get
            {
                if (!TutorialGuide.Active) return false;
                var steps = TutorialGuide.Steps;
                for (int i = TutorialGuide.StepIndex; i < steps.Count; i++)
                    if (steps[i] != null && steps[i].reveal == "midTransition")
                        return true;
                return false;
            }
        }
        private bool GameEnded => _gameEnded();

        private GameObject _introPanel; // TipPanel 实例（2026-10-17：改用预制体，见 ShowIntroPanel）
        private GameObject _tipPanelPrefab; // 惰性加载（Assets/Art/Prefab/TipPanel.prefab：msg=文案、btn-ok=确定）
        private AudioSource _voiceSource;
        private int _voiceSerial; // 连续换段防错播（加载完成时本段可能已被点走）
        private Func<GameCore, Player, List<ResponseOption>, ResponseOption> _tutorialResponder; // 教学守卫应答器（UninstallResponder 卸载）
        private bool _guideHooked;

        /// <summary>在役导演实例（2026-10-20 双换场修复）：HookBanner 夺位。旧实例（屏 Replace/
        /// OnExit 中断等僵尸订阅）各自持有**独立的段账**（_phaseIndex 落后）——收到同一
        /// OnStepAdvanced 会用旧账重放换场（实测段六b 双「新考验开始」+同场面重铺）。非在役实例
        /// 收到事件即自拆订阅；在役卸钩时清位。</summary>
        private static BattleTutorialDirector _activeDirector;

        public BattleTutorialDirector(Func<RectTransform> root, Func<GameCore> core,
            Func<BattleController> ctrl, Func<BattleFieldView> field, BattleToastHub toast,
            Func<string, string, string, UniTask<bool?>> showTip, Action back,
            Action refreshField, Action refreshFans, Action endTurnFan,
            Func<bool> gameEnded, Action<bool> setGameEnded, Action replayOpening = null,
            Func<bool> presentationBusy = null)
        {
            _root = root;
            _core = core;
            _ctrl = ctrl;
            _field = field;
            _toast = toast;
            _showTip = showTip;
            _back = back;
            _refreshField = refreshField;
            _refreshFans = refreshFans;
            _endTurnFan = endTurnFan;
            _gameEnded = gameEnded;
            _setGameEnded = setGameEnded;
            _replayOpening = replayOpening;
            _presentationBusy = presentationBusy;
        }

        /// <summary>表现在途探针（2026-10-20 运势段被截修复）：回放导演 Busy——步间停顿/
        /// 末步等待期视觉门瞬空，导演在途才是权威判据（其延迟演出队列非空必伴随 Busy）。
        /// null=未注入（防御：只等视觉门）。</summary>
        private readonly Func<bool> _presentationBusy;

        /// <summary>教学三重开战场（2026-10-19 掷币演出挪战场）：Manager.Replace&lt;BattleScreen&gt;——
        /// 弹栈重入战屏，五段式开场整体重放（重掷硬币+重选卡组=「换卡组新局」教学语义保持；
        /// 旧口径 Back 回对局准备屏随 PreMatchScreen 整删退役）。null=未注入（防御：回落 Back）。</summary>
        private readonly Action _replayOpening;

        /// <summary>静态引导订阅退订（OnExit 首段——僵尸订阅教训：OnExit 中途异常截断会让
        /// OnStepAdvanced 摄着旧实例跨局复活）。</summary>
        public void UnhookBanner()
        {
            if (_guideHooked)
            {
                TutorialGuide.OnStepAdvanced -= OnGuideStepAdvanced;
                CardCore.EventManager.Instance.Unsubscribe<CardCore.TurnEndEvent>(OnTrialTurnEndArm);
                CardCore.EventManager.Instance.Unsubscribe<CardCore.Attribute.HealEvent>(OnBlessingHealAutoAdvance);
                _guideHooked = false;
            }
            if (_activeDirector == this) _activeDirector = null;
        }

        /// <summary>教学守卫应答器卸载（null=回落内置启发）。</summary>
        public void UninstallResponder()
        {
            if (_tutorialResponder != null
                && ReferenceEquals(ResponseWindowService.AiResponder, _tutorialResponder))
            {
                ResponseWindowService.AiResponder = null;
                _tutorialResponder = null;
            }
        }

        /// <summary>终局教学分发（OnGameOverFan 转发——Retry/Complete 弹窗与重开序列）。</summary>
        public void PromptBattleOver(bool win)
        {
            if (BattleEntry.Mode != BattleMode.Tutorial) return;
            if (!win)
            {
                // 教学败局：教学二=重挑（回对局准备重选卡组，2026-10-18）；basics=考验原地重开
                if (BattleEntry.TutorialId == TutorialProgressManager.LunzhuanId)
                    PromptLunzhuanRetryAsync().Forget();
                else
                    PromptTutorialRetryAsync().Forget();
            }
            else if (BattleEntry.TutorialId == TutorialProgressManager.BasicsId)
                PromptTutorialCompleteAsync().Forget(); // 教学第一阶段（basics）通关：结算弹窗，确定回主菜单
            else if (BattleEntry.TutorialId == TutorialProgressManager.JinjieId)
                PromptTutorialCompleteAsync("你已完成第二课的教学").Forget(); // 第二课（主动效果）通关：同 basics 结算口径
            else if (BattleEntry.TutorialId == TutorialProgressManager.LunzhuanId)
                PromptLunzhuanCompleteAsync().Forget(); // 教学第三阶段（末日降临挑战）通关：结算弹窗
        }

        /// <summary>引导/守卫提示接线（教学步骤事件驱动，幂等——重入只补绑防重复订阅）。
        /// 2026-10-16 用户定案：横幅提示合并 lbl-toast——guide-banner 节点退役（找到即隐藏
        /// 不再写入），文案改写 toast 底层（UpdatePromptBanner→_toast.SetBase）。
        /// 2026-10-16 教学一两段式：intro 讲解步=点击横幅推进（面板代码兜底挂 Button，不改 prefab）；
        /// reveal 演出指令在此消费（midTransition=第二段注入，分区渐进显示后续接）。</summary>
        public void HookBanner()
        {
            if (_guideHooked) return;
            var legacy = UiKit.FindDeep(Root, "guide-banner") as RectTransform;
            if (legacy != null) legacy.gameObject.SetActive(false); // 旧横幅退役隐藏（防 prefab 默认 active 残影）
            _activeDirector = this; // 在役夺位（2026-10-20 双换场修复）：旧实例收到同一 OnStepAdvanced 会用各自落后的段账重放换场
            TutorialGuide.OnStepAdvanced -= OnGuideStepAdvanced; // 先减后加（2026-10-17）：同实例残留订阅幂等去重
            TutorialGuide.OnStepAdvanced += OnGuideStepAdvanced;
            CardCore.EventManager.Instance.Subscribe<CardCore.TurnEndEvent>(OnTrialTurnEndArm); // 考验必杀武装点（2026-10-20 定案）
            CardCore.EventManager.Instance.Subscribe<CardCore.Attribute.HealEvent>(OnBlessingHealAutoAdvance); // 段七祝福回血自动交回合（2026-10-21）
            _toast.AttachIntroClick(OnToastIntroClicked); // intro 讲解步=点击横幅推进（2026-10-16 教学一两段式；hub 内幂等单次）
            _guideHooked = true;
        }

        /// <summary>考验必杀武装（2026-10-20 用户设计："玩家操作完结束回合，下一个回合对手的直伤"）：
        /// P1 结束回合时已处于末段（后续无段）且当前引导步=击杀等待（luckuse，考验解说已读完、
        /// 玩家已拿到并打完考验回合）→ 武装 ScriptedAi，其下个回合开场直伤致死=考验失败重挑。
        /// 换场/重铺时刻不武装（RunMidTransition 一律先解除）——旧"末段换场即武装"在换场落
        /// end 点击（回合翻转前）时武装，AI 紧接的回合会在玩家拿到考验回合前就把人打死。</summary>
        private void OnTrialTurnEndArm(CardCore.TurnEndEvent e)
        {
            if (_activeDirector != null && _activeDirector != this) return;
            if (e?.TurnPlayer == null || Core == null || e.TurnPlayer != Core.Player1) return;
            if (_scriptedAi == null || TutorialGuide.Current == null
                || TutorialGuide.Current.type != "luckuse") return; // 考验等待步在位=已拿到考验回合
            var tutorial = TutorialLibrary.Get(BattleEntry.TutorialId);
            if (PhaseScenarioOf(tutorial, _phaseIndex + 1) != null) return; // 非末段
            _scriptedAi.TrialPressureArmed = true;
            Debug.Log("[BattleScreen] 考验回合结束未击杀——必杀直伤已武装（下个对手回合开场结算）");
        }

        /// <summary>段七祝福自动交回合闩锁（2026-10-21 当晚修幽灵二次交回合）：**静态按回合号去重**。
        /// 当晚日志实证：自动交回合异步在数回合后（玩家考验回合内）再次触发 _endTurnFan——把考验
        /// 回合提前结束，引发连环换场重铺/AI 多抽牌/牌库计数错乱。改为闩锁记「已自动交回合的
        /// 回合号」（静态=僵尸导演实例同号一并去重），执行前再核同回合+段七+end 步+轮到玩家。</summary>
        private static int _blessingAutoEndFiredTurn = -1;

        /// <summary>段七祝福回血自动交回合（2026-10-21）：祝福结算（准备抽牌→CurseSystem 发作→回复）
        /// 发生在玩家回血回合的开始，此后引导步 end#2 全闸只等玩家手点结束回合——改为回血表现
        /// 播完后自动交回合进入最终考验（换场/AI 考验回合/玩家考验回合首抽与手点完全同路径）。
        /// 触发口径：段七在账+当前步=end+治疗目标=P1+实效量>0；按回合号一次性（重置重入段七=
        /// 新祝福回合号，天然可再触发）。</summary>
        private void OnBlessingHealAutoAdvance(CardCore.Attribute.HealEvent e)
        {
            if (_activeDirector != null && _activeDirector != this) return;
            if (Core == null) return;
            if (_phaseIndex != 7) return; // 段七（诅5红+祝5绿）在账
            if (e?.Target != Core.Player1 || e.Amount - e.Overfill <= 0) return;
            if (!TutorialGuide.Active || TutorialGuide.Current == null
                || TutorialGuide.Current.type != "end") return;
            int turn = Core.TurnEngine.TurnNumber;
            if (_blessingAutoEndFiredTurn == turn) return; // 同回合已触发（含僵尸实例同帧竞争）
            _blessingAutoEndFiredTurn = turn;
            AutoEndTurnAfterHealFxAsync(turn).Forget();
        }

        /// <summary>等祝福回血表现播完再自动交回合：先等演出真正接管视觉门（回放抑制期
        /// DeferOrRun 延迟播出——门瞬空≠演完；短超时兜底防演出被吞死等），再等门归零+
        /// 回放导演收尾（ConsumeMidTransitionAsync 同款口径）。**幽灵拦截（当晚实测）**：等待期
        /// 跨过回合边界（首次交回合已完成/进入后续回合）、步骤已非段七 end 步、或已非玩家回合
        /// ——一律放弃，绝不二次交回合。</summary>
        private async Cysharp.Threading.Tasks.UniTaskVoid AutoEndTurnAfterHealFxAsync(int turnNo)
        {
            await Cysharp.Threading.Tasks.UniTask.Yield();
            int guard = 0;
            while (Field != null && !Field.VisualsBusy && guard++ < 120)
                await Cysharp.Threading.Tasks.UniTask.Yield();
            if (Field != null) await Field.WaitVisualsIdleAsync();
            if (_presentationBusy != null)
                while (_presentationBusy()) await Cysharp.Threading.Tasks.UniTask.Yield();
            if (Core == null || Ctrl == null || Ctrl.TurnPlayer != Core.Player1) return;
            if (Core.TurnEngine.TurnNumber != turnNo) return; // 迟发幽灵：已是后续回合——放弃
            if (_phaseIndex != 7 || !TutorialGuide.Active || TutorialGuide.Current == null
                || TutorialGuide.Current.type != "end") return;
            Debug.Log("[BattleScreen] 祝福回血表现播完——自动交回合进入最终考验");
            _endTurnFan();
        }

        private void OnGuideStepAdvanced(int stepIndex)
        {
            if (_activeDirector != null && _activeDirector != this)
            {
                UnhookBanner(); // 僵尸实例自拆（2026-10-20 双换场修复：实测段六b 双「新考验开始」=滞后段账的旧实例重铺）
                return;
            }
            if (Root == null) return; // 僵尸订阅自防（2026-10-17）：本工程 EnterPlayModeOptions=3 禁域重载——
            // 停止 Play 不走框架 OnExit 退订，静态事件摄着旧实例跨 Play 复活；UI 已销毁（Root 假空）即不再响应
            RefreshBanner();
            // 换场步（2026-10-19 多段化）：midTransition=换场续接。**栈空→同步执行**（intro 确认推进/
            // EndTurn 栈内推进时栈必空——重铺落在回合翻转前，AI 从新牌库抽牌=牌库对比门的前提）；
            // **栈忙→异步等结算**（play 步在宣言即推进、击杀/结算在后——必须等打完再重铺）。
            if (stepIndex >= 0 && stepIndex < TutorialGuide.Steps.Count
                && TutorialGuide.Steps[stepIndex] != null
                && TutorialGuide.Steps[stepIndex].reveal == "midTransition")
            {
                if (Core != null && Core.StackEngine != null
                    && (!Core.StackEngine.IsEmpty || Core.StackEngine.IsResolving))
                    ConsumeMidTransitionAsync(stepIndex).Forget();
                else
                    RunMidTransition(stepIndex);
                return;
            }
            ConsumeReveal(stepIndex);
            // 介绍段卡片（2026-10-17 配音改版）：intro 步一律走模态卡片（带 audio 播语音，无 audio=文字卡片）；
            // 其余步骤（操作教学/引导完成）一律收卡片回落横幅。
            var step = TutorialGuide.Current;
            if (step != null && step.type == "intro")
            {
                // intro 解说卡一律等前段表现收口再弹（2026-10-20 运势段被截修复，原 aiGuard/
                // waitTurnStart 两特判通用化）：步进点常落在结算/回放在途（luckuse 末击轮询、
                // aiGuard 结算探针、waitTurnStart 回合事件、deathtoll 计满…）——弹早了会把
                // 未播完的演出（掷骰浮字/伤害/回放步间节拍）盖进下一段解说。
                ShowIntroPanelAfterCombatAsync(step, stepIndex).Forget();
            }
            else
                CloseIntroPanel();

            // 考验开局（2026-10-17）：过场卡（basics_12·aiTurnOnConfirm）确定即把回合交给对手——
            // 援军全清重铺后=对手进攻回合（OnEndTurnFan 现成序列：EndTurn→等翻 P2→驱动剧本 AI）。
            if (stepIndex > 0 && stepIndex <= TutorialGuide.Steps.Count
                && TutorialGuide.Steps[stepIndex - 1] != null
                && TutorialGuide.Steps[stepIndex - 1].aiTurnOnConfirm)
                _endTurnFan();
        }

        /// <summary>换场续接（2026-10-19 教学二多段化）·异步壳：等一帧+栈收口（play 步在宣言即推进，
        /// 击杀/结算在其后——重铺必须等全部结算落地）+ 表现播毕（人机节奏同口），随后走同步换场核心。
        /// 2026-10-20 排演定版：**不再按 StepIndex 放弃**——旧守卫在玩家于等待期确认 intro 卡时静默
        /// 放弃换场，段账漏进账导致后续换场全部铺错段（段五中途重铺/段七滞后重铺把祝福蒸发）；
        /// 现由换场核心的「步序推导段号+陈旧跳过+重入守卫」自治（晚到的旧换场自然跳过或幂等）。</summary>
        private async Cysharp.Threading.Tasks.UniTaskVoid ConsumeMidTransitionAsync(int stepIndex)
        {
            await Cysharp.Threading.Tasks.UniTask.Yield();
            int guard = 0;
            while (Core != null && Core.StackEngine != null
                   && (!Core.StackEngine.IsEmpty || Core.StackEngine.IsResolving)
                   && guard++ < 600)
                await Cysharp.Threading.Tasks.UniTask.Yield();
            if (Field != null) await Field.WaitVisualsIdleAsync();
            if (_presentationBusy != null) // 回放导演在途（步间停顿门瞬空）——重铺会拆掉残余演出，等收尾
                while (_presentationBusy()) await Cysharp.Threading.Tasks.UniTask.Yield();
            RunMidTransition(stepIndex);
        }

        /// <summary>换场核心（同步）：终局撤销（脚本化续接——上段把对手打死的局，恢复运行才能重铺，
        /// 败局重开 ResumeFromEnded 同款）→ ApplyRuntime 重铺目标段场面+落段账（_phaseIndex/_phaseStarts）
        /// → 段首解说卡换场后弹出（含 basics 的 aiTurnOnConfirm 确认交回合链）。
        /// JumpTo 分段回退重入时：段账已就位（目标段=当前账段）只重弹卡不重铺不进账。
        /// 段号=步序推导（2026-10-20 排演定版）：第 N 个换场步=第 N 段，与配置一一对应——
        /// 不受异步竞态/重复触发/账本漂移影响，账不符时以步序为准回写自愈。</summary>
        private void RunMidTransition(int stepIndex)
        {
            var tutorial = TutorialLibrary.Get(BattleEntry.TutorialId);
            // 段号按步序推导（换场步#1=段1 skipScenario…#8=段8 考验）——自愈曾发生的漏进账漂移
            int seq = 0;
            for (int i = 0; i <= stepIndex && i < TutorialGuide.Steps.Count; i++)
                if (TutorialGuide.Steps[i] != null && TutorialGuide.Steps[i].reveal == "midTransition")
                    seq++;
            int targetPhase = Math.Max(1, seq);
            if (targetPhase < _phaseIndex) return; // 陈旧异步尾：更后的段已应用——跳过（防回退重铺）
            var target = PhaseScenarioOf(tutorial, targetPhase);
            if (target == null || Core == null || _ctrl == null)
            {
                Debug.LogWarning("[BattleScreen] midTransition 换场目标场面缺失（phaseScenarios 配置不足）——跳过");
                return;
            }

            // 必杀武装改挂玩家考验回合结束（2026-10-20 用户设计定案"操作完结束回合，下一回合对手直伤"）：
            // 换场/重铺一律先**解除**（防段切换/重试残留误杀）——真正的武装点在 OnTrialTurnEndArm
            //（P1 结束回合且已处于末段、当前步=击杀等待 luckuse 时）。旧口径"末段换场即武装"会在
            // 换场落点击时（回合翻转前）武装，AI 紧接回合开场直伤=玩家还没拿到考验回合就被打死。
            if (_scriptedAi != null)
                _scriptedAi.TrialPressureArmed = false;
            // 分段回退重入（TryResetTrial→JumpTo）：目标段已在账上=本步刚重铺过，只补段首卡
            if (_phaseStarts.Count > targetPhase && _phaseStarts[targetPhase] == stepIndex
                && _phaseIndex == targetPhase)
            {
                ShowIntroPanel(TutorialGuide.Steps[stepIndex]);
                return;
            }
            if (GameEnded && Core.StateManager != null)
            {
                if (!Core.StateManager.ResumeFromEnded())
                {
                    Debug.LogWarning("[BattleScreen] 换场续接失败：终局撤销被拒（非 Ended？）——保持终局画面");
                    return;
                }
                _setGameEnded(false);
            }
            TutorialScenarioSeeder.ApplyRuntime(Core, target, Ctrl.Board);
            _phaseIndex = targetPhase;
            while (_phaseStarts.Count <= targetPhase) _phaseStarts.Add(stepIndex);
            _phaseStarts[targetPhase] = stepIndex;
            InTrialSegment = true; // 换场后均为考验段（常驻重置按钮分流口径）
            BattleRevealDirector.End(); // 介绍段演出全恢复（考验段=完整呈现）
            Ctrl.Board?.Resync();
            _refreshField();
            _refreshFans();
            _toast.Show("新考验开始！");
            if (TutorialGuide.Steps[stepIndex] != null)
                ShowIntroPanel(TutorialGuide.Steps[stepIndex]); // 段首解说卡在换场后弹出
        }

        /// <summary>段号→场面（0=scenario 首段；1=skipScenario 段二；n≥2=phaseScenarios[n-2]）。</summary>
        private TutorialScenario PhaseScenarioOf(TutorialConfig tutorial, int phase)
        {
            if (tutorial == null) return null;
            if (phase == 0) return tutorial.scenario;
            if (phase == 1) return tutorial.skipScenario;
            var list = tutorial.phaseScenarios;
            int i = phase - 2;
            return list != null && i >= 0 && i < list.Count ? list[i] : null;
        }

        /// <summary>表现收口后再弹解说卡（intro 通用路径·2026-10-20 运势段被截修复）：先让一帧令
        /// 演出接管（结算探针推进时编舞器尚未 BeginVisual），再等视觉门归零+回放导演收尾
        ///（步间停顿/末步等待期门瞬空——导演 Busy 是权威在途判据）；期间步骤又被推进
        ///（不应发生）则放弃本次弹卡。</summary>
        private async Cysharp.Threading.Tasks.UniTaskVoid ShowIntroPanelAfterCombatAsync(
            TutorialGuideStepSpec step, int stepIndex)
        {
            await Cysharp.Threading.Tasks.UniTask.Yield();
            if (TutorialGuide.StepIndex != stepIndex) return;
            while ((Field != null && Field.VisualsBusy)
                   || (_presentationBusy != null && _presentationBusy()))
                await Cysharp.Threading.Tasks.UniTask.Yield();
            if (TutorialGuide.StepIndex != stepIndex) return;
            ShowIntroPanel(step);
        }

        /// <summary>新步骤成为当前步时的演出指令消费（2026-10-17 语音联动地块显隐定案）：
        /// midTransition=换场步（已上移 OnGuideStepAdvanced 异步续接）；board/zone:*/hand/units=
        /// 独占显示语音描述的区块（BattleRevealDirector.Solo——讲哪个亮哪个）。多令牌（2026-10-19
        /// 教学二段一）：逗号分隔逐个消费（units,hand=生物出场+手牌就位；all=棋盘全亮）。
        /// 解闸类 token（zone:role 建柱/units 出场/hand 亮手牌）随后触发对应对账刷新。</summary>
        private void ConsumeReveal(int stepIndex)
        {
            if (stepIndex < 0 || stepIndex >= TutorialGuide.Steps.Count) return;
            var step = TutorialGuide.Steps[stepIndex];
            if (step == null || string.IsNullOrEmpty(step.reveal)) return;
            foreach (var raw in step.reveal.Split(','))
            {
                var token = raw.Trim();
                if (token.Length == 0) continue;
                BattleRevealDirector.Solo(token);
                // 解闸类 token 须随后对账：zone:role 解柱闸建柱（对手柱建后隐藏）；all=操作预告段
                // 解除对手隐藏，RefreshField 让对手柱当场显现（2026-10-18）；hand 解手牌闸填手牌；
                // units 解单位闸（RefreshField 建单位=生物出场）
                if (token == "zone:role" || token == "all" || token == "units") _refreshField();
                else if (token == "hand") _refreshFans();
            }
        }

        /// <summary>第二段注入（midTransition）：TutorialFlow.ApplyMidTransitionScenario 逻辑层单源
        /// （清双方场面/手牌/牌库/地牌、生命 20/20 重置（2026-10-17 晚改值）、凭空铺教学生物）；
        /// 本侧只做 UI 对账（Resync/双刷）与提示。</summary>
        private void ApplyMidTransition()
        {
            var tutorial = TutorialLibrary.Get(BattleEntry.TutorialId);
            if (tutorial?.skipScenario == null || Core == null || _ctrl == null)
            {
                Debug.LogWarning("[BattleScreen] midTransition 注入条件不满足（skipScenario/核心未就绪）——跳过");
                return;
            }
            TutorialFlow.ApplyMidTransitionScenario(Core, tutorial, Ctrl.Board);
            InTrialSegment = true; // midTransition=考验段开始（2026-10-19 常驻重置按钮分流口径）
            BattleRevealDirector.End(); // 介绍段演出全恢复（第二段=田忌场面完整呈现）
            Ctrl.Board?.Resync();
            _refreshField();
            _refreshFans();
            _toast.Show("双方援军登场！");
        }

        /// <summary>intro 讲解步推进口：横幅被点击且当前步为 intro 才推进（其余步骤误点无害）。</summary>
        private void OnToastIntroClicked()
        {
            if (TutorialGuide.Active && TutorialGuide.Current?.type == "intro")
                TutorialGuide.NotifyIntroConfirmed();
        }

        /// <summary>介绍段卡片（2026-10-17 用户定案）：TipPanel 预制体（msg=大字文案；预制体已改
        /// 双按钮+label 运行时赋文——介绍段=btn-ok 赋「确定」、btn-no 隐藏的单键形态）+播放配音。
        /// 弃全屏模态遮罩——黑屏拦射线导致盘面悬停全不可用；TipPanel 只占自身矩形，
        /// 期间玩家可自由悬停棋盘查看点位信息（引导闸在逻辑层锁操作，不靠遮罩）。
        /// 语音文件缺失静默继续（文字照常显示，点确定推进）。</summary>
        private void ShowIntroPanel(TutorialGuideStepSpec step)
        {
            CloseIntroPanel();
            if (_tipPanelPrefab == null)
                _tipPanelPrefab = HotUpdateAssets.Load<GameObject>("TipPanel", UiKit.PrefabPath("TipPanel"));
            if (_tipPanelPrefab == null)
            {
                Debug.LogWarning("[BattleScreen] TipPanel.prefab 加载失败——文案回落横幅（点横幅仍可推进）");
                PlayTutorialVoiceAsync(step.audio).Forget();
                return;
            }
            var panel = UnityEngine.Object.Instantiate(_tipPanelPrefab, Root, false);
            panel.name = "tip-panel";
            var rt = panel.transform as RectTransform;
            var msgTmp = UiKit.FindDeep(rt, "msg")?.GetComponent<TMP_Text>();
            if (msgTmp != null) msgTmp.text = step.hint;
            var btn = UiKit.FindDeep(rt, "btn-ok");
            var button = btn != null ? btn.GetComponent<Button>() : null;
            if (button != null)
            {
                var lbl = button.GetComponentInChildren<TMP_Text>(true);
                if (lbl != null) lbl.text = "确定"; // 预制体改双按钮后 label 运行时赋文（2026-10-17）
                button.onClick.AddListener(() =>
                {
                    CloseIntroPanel();
                    TutorialGuide.NotifyIntroConfirmed();
                });
            }
            var noBtn = UiKit.FindDeep(rt, "btn-no");
            if (noBtn != null) noBtn.gameObject.SetActive(false); // 介绍段单键形态：只留确定
            _introPanel = panel;
            PlayTutorialVoiceAsync(step.audio).Forget();
        }

        public void CloseIntroPanel()
        {
            StopTutorialVoice();
            if (_introPanel != null)
            {
                UnityEngine.Object.Destroy(_introPanel);
                _introPanel = null;
            }
        }

        /// <summary>配音播放：热更包 AudioClip（TutorialData 组，地址=文件名+路径双试；
        /// 2026-10-18 数据迁出 StreamingAssets，UnityWebRequest 旧通道退役）。
        /// 文件缺失记日志静默——不阻塞文字卡片流程。</summary>
        private async Cysharp.Threading.Tasks.UniTaskVoid PlayTutorialVoiceAsync(string audio)
        {
            if (string.IsNullOrEmpty(audio)) return;
            int serial = ++_voiceSerial;
            await Cysharp.Threading.Tasks.UniTask.Yield();
            try
            {
                if (serial != _voiceSerial) return; // 段已切换：丢弃本次加载
                var clip = Tide.HotUpdate.HotUpdateAssets.Load<AudioClip>(
                    audio, "Assets/TutorialData/TutorialAudio/" + audio + ".mp3");
                if (clip == null)
                {
                    Debug.Log($"[BattleScreen] 教学语音未找到（文字照常）：{audio}");
                    return;
                }
                if (_voiceSource == null)
                {
                    var go = new GameObject("tutorial-voice");
                    go.transform.SetParent(Root, false);
                    _voiceSource = go.AddComponent<AudioSource>();
                    _voiceSource.playOnAwake = false;
                }
                _voiceSource.clip = clip;
                _voiceSource.Play();
            }
            catch (Exception e)
            {
                Debug.Log($"[BattleScreen] 教学语音播放失败（文字照常）：{e.Message}");
            }
        }

        private void StopTutorialVoice()
        {
            _voiceSerial++;
            if (_voiceSource != null && _voiceSource.isPlaying) _voiceSource.Stop();
        }

        /// <summary>toast 底层横幅文案：overrideText（守卫提示等）优先；否则教学「第x/n步 hint」；
        /// 无内容=清底层。顶层临时提示（选择/响应窗）在位时遮住底层，关窗清空自动回落。
        /// intro 讲解步统一追加「（点击横幅继续）」尾注（hint 自带点击提示时不重复）。</summary>
        public void RefreshBanner(string overrideText = null)
        {
            string text = overrideText;
            if (text == null && TutorialGuide.Active && TutorialGuide.Current != null)
            {
                int idx = TutorialGuide.StepIndex;
                text = $"【教学 第{idx + 1}/{TutorialGuide.Steps.Count}步】{TutorialGuide.Current.hint}";
                if (TutorialGuide.Current.type == "intro" && !text.Contains("点击"))
                    text += "（点击横幅继续）";
            }
            _toast.SetBase(text);
        }

        /// <summary>教学固定局开局（2026-10-02）：TutorialConfig 指定双方卡组顺序（=摸牌序列，锁牌库序）
        /// 与机器人逐回合剧本（ScriptedAi 按条演出），rngSeed=复现钉子（非 0 钉同局、0/缺省每局新抽）；机器人响应窗口全让过
        /// （EnablePassiveAiResponder——永不反制玩家）。返回 false=配置缺失回落普通局。
        /// 预设场面（2026-10-06 教学直入）：scenario 非 null=组卡取双方场面并集、
        /// 不走起手/摸牌序列断言，TutorialScenarioSeeder 直入起跳回合。
        /// 教学二（2026-10-18 真实对局流程版）：entryPlayerDeck/entryAiDeck=对局准备流程注入的
        /// 自选卡组+镜像（缺省回落 deckFile）；entryPlayerFirst=硬币先后手（后手+5 护甲同真实流程）；
        /// entryMySkill/entryAiSkill=卡组标记技能。</summary>
        public bool StartGame(BattleController ctrl,
            List<CardData> entryPlayerDeck = null, List<CardData> entryAiDeck = null,
            bool? entryPlayerFirst = null, string entryMySkill = null, string entryAiSkill = null)
        {
            var tutorial = TutorialLibrary.Get(BattleEntry.TutorialId);
            if (tutorial == null)
            {
                _toast.Show($"教学配置缺失：{BattleEntry.TutorialId}（回落本地 AI 局）");
                return false;
            }

            // 对局种子（2026-10-20 定案）：一局一枚、双方共用（GameRng+ZoneContainer 同籽）、换局重抽。
            // 配置 rngSeed 非 0=复现钉子（把日志种子号回填配置即重放同局）；0/缺省=每局新抽记日志留档。
            int matchSeed = tutorial.rngSeed != 0 ? tutorial.rngSeed : System.Guid.NewGuid().GetHashCode();
            Debug.Log($"[Tutorial] 对局种子 rngSeed={matchSeed}{(tutorial.rngSeed != 0 ? "（配置钉子·复现局）" : "（本局新抽）")}");

            // 跳过介绍（2026-10-16 教学一两段式）：skipScenario 直入第二段（田忌场面）+guideSkip 链；
            // 机器人剧本偏移 1（2026-10-17 第三版：田忌剧本在 aiScript turn2-5——直入时 AI 第 1 回合对 turn2）。
            // 条目不支持跳过（skipScenario/guideSkip 缺失）=回落完整链。
            bool skipIntro = BattleEntry.SkipIntro;
            BattleEntry.SkipIntro = false; // 一次性消费防跨局残留
            if (skipIntro && tutorial.skipScenario != null && tutorial.guideSkip != null
                && tutorial.guideSkip.Count > 0)
            {
                var playerCards = TutorialLibrary.BuildDeck(TutorialScenario.DeckIdsOf(tutorial.skipScenario.player));
                var aiCards = TutorialLibrary.BuildDeck(TutorialScenario.DeckIdsOf(tutorial.skipScenario.ai));
                ctrl.StartNewGame(playerCards, aiCards, MakePacedScriptedAi(tutorial, turnOffset: 1),
                    rngSeed: matchSeed, scenario: tutorial.skipScenario);
                ctrl.EnablePassiveAiResponder();
                TutorialGuide.Begin(tutorial, tutorial.guideSkip);
                InTrialSegment = true; // 跳介绍直入考验段（2026-10-19 常驻重置按钮分流口径）
                _phaseIndex = 1;
                _phaseStarts.Clear();
                _phaseStarts.AddRange(new[] { 0, 0 }); // guideSkip 步 0=考验段首（非换场步，重置走直铺路径）
                return true;
            }

            if (tutorial.scenario != null)
            {
                var playerCards = TutorialLibrary.BuildDeck(TutorialScenario.DeckIdsOf(tutorial.scenario.player));
                var aiCards = TutorialLibrary.BuildDeck(TutorialScenario.DeckIdsOf(tutorial.scenario.ai));
                // 2026-10-20 元素充盈判据五：主线剧本 AI 同样走节奏装配（动作间等演出+玩家弹选收口
                //——段六对手回合六连召唤依次弹选依赖此等待；其余段 AI 回合仅 end 零感知）。
                MakePacedScriptedAi(tutorial);
                ctrl.StartNewGame(playerCards, aiCards, _scriptedAi,
                    rngSeed: matchSeed, scenario: tutorial.scenario);
                ctrl.EnablePassiveAiResponder();
                TutorialGuide.Begin(tutorial); // 暂停引导（2026-10-06）：guide 为空时内部不激活
                InTrialSegment = false;
                _phaseIndex = 0;
                _phaseStarts.Clear();
                _phaseStarts.Add(0);
                // 语音联动显隐（2026-10-19 实测补调）：guide 以 intro 开头=介绍段模式——开局全隐藏，
                // 随 reveal 指令分段出场（units,hand→all）。scenario 直入分支此前提前 return 漏调
                // ——Solo 恒 _active=False 早退，分段演出全哑（重置路径有此调用故"重置后正常"）。
                if (TutorialGuide.Active && TutorialGuide.Current?.type == "intro")
                    BattleRevealDirector.BeginIntroReveal();
                return true;
            }
            // 教学二（2026-10-18）：外部注入卡组优先（对局准备流程自选+镜像），缺省回落 deckFile
            var playerDeck = entryPlayerDeck ?? TutorialLibrary.BuildDeck(tutorial.PlayerDeckIds);
            var aiDeck = entryAiDeck ?? TutorialLibrary.BuildDeck(tutorial.AiDeckIds);
            if (playerDeck.Count < GameCore.OpeningHandSize || aiDeck.Count < GameCore.OpeningHandSize)
            {
                _toast.Show("教学卡组配置不足（回落本地 AI 局）");
                return false;
            }
            ctrl.StartNewGame(playerDeck, aiDeck, MakePacedScriptedAi(tutorial),
                lockDeckOrder: true, rngSeed: matchSeed,
                mySkillCardId: entryMySkill, aiSkillCardId: entryAiSkill,
                playerFirst: entryPlayerFirst, secondArmorBonus: entryPlayerFirst.HasValue ? 5 : 0);
            EnableTutorialAiResponder(ctrl, tutorial); // AI 响应：默认让过+aiGuard 守卫演示窗（2026-10-17 剧本）
            TutorialGuide.Begin(tutorial);
            InTrialSegment = false;
            _phaseIndex = 0;
            _phaseStarts.Clear();
            _phaseStarts.Add(0);
            // 语音联动显隐（2026-10-17）：guide 以 intro 开头=介绍段模式——开局全隐藏，
            // 随配音段 reveal 指令独占显示对应区块（非 intro 链零影响）
            if (TutorialGuide.Active && TutorialGuide.Current?.type == "intro")
                BattleRevealDirector.BeginIntroReveal();
            return true;
        }

        /// <summary>教学局 AI 响应器（2026-10-17 剧本·守卫演示）：默认全让过（同 PassiveAiResponder
        /// 语义）；当前引导步带 aiGuard 标记且守卫候选非空时，AI 启发选守卫（生物赴挡位——
        /// 玩家的攻击目标被改写为对守卫者的攻击）。返回 false=未接管（沿用被动应答）。
        /// 委托自持引用（_tutorialResponder）：不走 BattleController 的还原机制，OnExit 自行卸载。</summary>
        private bool EnableTutorialAiResponder(BattleController ctrl, TutorialConfig tutorial)
        {
            bool hasGuardDemo = false;
            foreach (var s in tutorial.guide ?? new List<TutorialGuideStepSpec>())
                if (s != null && s.aiGuard) { hasGuardDemo = true; break; }
            if (!hasGuardDemo)
            {
                ctrl.EnablePassiveAiResponder(); // 无守卫演示的课：保持被动应答
                return false;
            }
            _tutorialResponder = (c, holder, options) =>
            {
                if (holder != c.Player2) return ResponseWindowService.PassOption; // 玩家侧响应：让过
                var step = TutorialGuide.Current;
                if (step == null || !step.aiGuard) return ResponseWindowService.PassOption;
                var guards = new List<ResponseOption>();
                foreach (var o in options ?? new List<ResponseOption>())
                    if (o != null && o.Kind == ResponseOption.ResponseKind.GuardAbility && o.SourceCard != null)
                        guards.Add(o);
                if (guards.Count == 0) return ResponseWindowService.PassOption;
                // 启发：能扛住的挑血最厚，否则任选（教学演示窗守卫者通常唯一=AI 的大生物）
                var source = guards[0].AttackInstance?.Source;
                int incoming = c.LayerEngine != null && source != null
                    ? c.LayerEngine.CalculatePower(source) : 0;
                ResponseOption best = null;
                foreach (var g in guards)
                    if (best == null || (g.SourceCard.GetLife() > incoming && g.SourceCard.GetLife() > best.SourceCard.GetLife()))
                        best = g;
                return best ?? guards[0];
            };
            ResponseWindowService.AiResponder = _tutorialResponder;
            return true;
        }

        /// <summary>教学脚本 AI 装配（2026-10-17 表现同步定案）：注入 PacingWait——剧本动作间等
        /// 视觉在途门归零（战斗演出走位/节拍/死亡/归位全播完才宣言下一动作）。缺省注入时
        /// ScriptedAi 全速（守卫确认结算后立刻下一击，把刚开始的动画冻在连锁窗口里）。</summary>
        /// <summary>当前教学局的剧本 AI 实例（考验压力武装持有者；普通局/回落局=null）。</summary>
        private ScriptedAi _scriptedAi;

        private ScriptedAi MakePacedScriptedAi(TutorialConfig tutorial, int turnOffset = 0)
        {
            _scriptedAi = new ScriptedAi(tutorial, turnOffset)
            {
                PacingWait = async () =>
                {
                    if (Field != null) await Field.WaitVisualsIdleAsync();
                    // 栈收口+稳定拍（2026-10-20 充盈六连连锁修复）：①上一动作的施放/奖励结算没走完
                    //（弹选清账后续排）不得宣言下一动作——否则幼兽施放叠上未空栈=速度0连锁（实测
                    // rid=86/87 以"步2/步3"多项目一次结算、幼兽在场累积）；②奖励 Forget 直发链到达
                    // 弹选登记要几帧——视觉门归零瞬间查 PendingRequests 会扑空。连续 2 帧全闲才放行。
                    int stable = 0;
                    while (stable < 2)
                    {
                        await UniTask.Yield();
                        var core = Core;
                        bool stackBusy = core?.StackEngine != null
                                         && (!core.StackEngine.IsEmpty || core.StackEngine.IsResolving);
                        // 回放导演在途也须等（2026-10-21 教学一考验守卫表现修复）：步间停顿期视觉门瞬空
                        // ——上一场「守卫宣言+攻击宣言」回放未收口就宣言下一击，新宣言 OnAttackDeclared
                        // 的 ClearCapture 会洗掉已捕获的守卫拦截，攻击步开演拿的是新宣言的未守卫捕获
                        //（原目标受击假象），晚到的 BlockDeclarationEvent 因捕获已交接被静默丢弃。
                        // 普通局 MakePacedAi 的 WaitIdleAsync 同口径（2026-10-19），教学剧本 AI 此前漏配。
                        if (stackBusy || TargetSelectionService.PendingRequests > 0
                            || (Field != null && Field.VisualsBusy)
                            || (_presentationBusy != null && _presentationBusy()))
                            stable = 0;
                        else stable++;
                    }
                },
            };
            return _scriptedAi;
        }

        /// <summary>介绍段配音卡片是否在屏（常驻重置按钮门控：卡片在屏=非自由操作期）。</summary>
        public bool IntroPanelOpen => _introPanel != null;

        /// <summary>考验段进行中（basics 两段式：midTransition/跳介绍后=true；重置目标据此分流）。
        /// 单段课程（jinjie 等 scenario 直入）恒 false——重置即回场面首步。</summary>
        public bool InTrialSegment { get; private set; }

        /// <summary>常驻考验重置（2026-10-19 用户定案：教学一败局重置升级为随时可点的重置按钮——
        /// 自由操作期+表现播毕由 BattleScreen 逐帧门控，此处只做二次防御与目标分流）：
        /// **分段回退**（2026-10-19 多段化）——回到当前段开始：段 0=scenario 重铺+引导回首步+
        /// 介绍段演出复位；段 n≥1=该段场面重铺+引导跳段首步（midTransition 步首=异步换场重入
        /// ——重铺/进账/段首卡/aiTurnOnConfirm 交回合链全部复用正片路径；非换场步首=直铺重置）。
        /// 重铺即重建全新牌库（重置前本回合已抽的牌不在新库里），无需抽牌补偿。</summary>
        public void TryResetTrial()
        {
            if (IntroPanelOpen || (Field != null && Field.VisualsBusy)) return; // 门控双保险
            var tutorial = TutorialLibrary.Get(BattleEntry.TutorialId);
            if (tutorial == null || Core == null || _ctrl == null) return;
            if (_phaseIndex == 0)
            {
                if (tutorial.scenario == null) return;
                TutorialScenarioSeeder.ApplyRuntime(Core, tutorial.scenario, Ctrl.Board);
                // 段零落点（2026-10-19 用户定案：不回介绍段开头）：重铺后直跳本段首个自由操作步
                //（fieldclear）——介绍三卡不重播、演出全恢复；无该步型（未配置）才回首步。
                int freeStep = IndexOfSegmentFieldClear();
                if (freeStep >= 0)
                {
                    TutorialGuide.JumpTo(freeStep);
                    BattleRevealDirector.End();
                }
                else
                {
                    TutorialGuide.Begin(tutorial); // 引导回首步（步0 intro 卡随 Begin 事件弹出）
                    if (TutorialGuide.Active && TutorialGuide.Current?.type == "intro")
                        BattleRevealDirector.BeginIntroReveal(); // 介绍段演出复位（StartGame 同款次序）
                }
                Ctrl.Board?.Resync();
                _refreshField();
                _refreshFans();
                _toast.Show("已重置到考验开始");
                return;
            }
            int target = _phaseIndex;
            if (_phaseStarts.Count <= target) return;
            int startStep = _phaseStarts[target];
            var startSpec = startStep >= 0 && startStep < TutorialGuide.Steps.Count
                ? TutorialGuide.Steps[startStep] : null;
            if (startSpec != null && startSpec.reveal == "midTransition")
            {
                // 段首=换场步：退段账后 JumpTo 重入——异步换场重铺/进账/段首卡全走正片路径
                // （basics：段首卡带 aiTurnOnConfirm，确定后自动交回合=考验开局对手先攻）
                JumpToSegmentStart(target, tutorial, compensateDraw: true);
                _toast.Show("已重置到考验开始");
                return;
            }
            var scenario = PhaseScenarioOf(tutorial, target);
            if (scenario == null) return;
            TutorialScenarioSeeder.ApplyRuntime(Core, scenario, Ctrl.Board);
            TutorialGuide.JumpTo(startStep); // 段首非换场步（skipIntro 直入型）：直铺+跳段首
            BattleRevealDirector.End();
            Ctrl.Board?.Resync();
            _refreshField();
            _refreshFans();
            if (BattleEntry.TutorialId == TutorialProgressManager.BasicsId)
                _endTurnFan(); // basics skipIntro 型考验段开局=对手先攻（原败局重开同款）
            _toast.Show("已重置到考验开始");
        }

        /// <summary>T 键测试跳段（2026-10-21 用户要求·临时调试口）：直接跳到最终考验段——目标=
        /// 引导链最后一个 reveal=midTransition 换场步（教学二=步 28→段 8 考验；basics=唯一换场步
        /// →段二考验）。走 TryResetTrial 换场步首分支同款路径：退段账后 JumpTo 重入，正片换场
        /// 全流程（终局撤销+重铺+段账+段首卡）由 RunMidTransition 步序推导自治；教学二末段补账
        ///（清本回合统计+首抽口径补抽牌库顶命运牌——跳段无回合翻转，准备抽牌须手动补）同款。
        /// 门控在 BattleScreen 侧（玩家主阶段+栈空+表现播毕，比重置按钮更严），此处只做双保险。</summary>
        public void JumpToFinalTrial()
        {
            if (IntroPanelOpen || (Field != null && Field.VisualsBusy)) return; // 门控双保险
            var tutorial = TutorialLibrary.Get(BattleEntry.TutorialId);
            if (tutorial == null || Core == null || _ctrl == null) return;
            int trialStep = -1, targetPhase = 0;
            for (int i = 0; i < TutorialGuide.Steps.Count; i++)
            {
                var s = TutorialGuide.Steps[i];
                if (s == null || s.reveal != "midTransition") continue;
                trialStep = i;
                targetPhase++; // 换场步序号=段号（与 RunMidTransition 步序推导同口径）
            }
            if (trialStep < 0 || PhaseScenarioOf(tutorial, targetPhase) == null) return; // 无换场步/无对应场面（lunzhuan 真实对局课）
            _phaseIndex = targetPhase - 1; // 退段账到目标段之下——JumpTo 重入走正片换场（不触发「重入只补卡」短路）
            if (_phaseStarts.Count > targetPhase)
                _phaseStarts.RemoveRange(targetPhase, _phaseStarts.Count - targetPhase);
            TutorialGuide.JumpTo(trialStep);
            // 教学二末段补账（同 TryResetTrial）：跳段落点仍在原回合（段首卡无 aiTurnOnConfirm 不交
            // 回合）——本回合已无准备抽牌，清本回合统计+按首抽口径补抽牌库顶（命运一抽，金圈随
            // CardDrawEvent 补刷）。其余课走 aiTurnOnConfirm 交回合链自带回合翻转+自然首抽，不补。
            if (BattleEntry.TutorialId == TutorialProgressManager.JinjieId
                && PhaseScenarioOf(tutorial, targetPhase + 1) == null)
            {
                MatchStatsService.Instance?.ClearTurnScope();
                Core.ZoneManager.DrawCard(Core.Player1, firstDrawOfTurn: true);
            }
            Debug.Log($"[TutorialJump] T 键跳段：步 {trialStep}→段 {targetPhase}（最终考验）");
        }

        /// <summary>分段回退重入核心（TryResetTrial 与败局弹窗共用，2026-10-21 用户定案「败局重开=
        /// 右上重置同款路径」）：退段账后 JumpTo 段首换场步——重铺/进账/段首卡全走正片路径。
        /// compensateDraw=教学二末段**回合中段**重置的补账（清本回合统计+按首抽口径补抽牌库顶命运牌
        /// ——本回合已无准备抽牌，CardsPlayed 残留让 FirstCardThisTurn 门恒假、金圈需 CardDrawEvent
        /// 补刷）；败局重开不补：回合将翻转（死亡在 AI 回合→驱动其收尾，玩家新考验回合自然准备抽牌
        /// =命运之牌）——再补即双抽。</summary>
        private void JumpToSegmentStart(int target, TutorialConfig tutorial, bool compensateDraw)
        {
            int startStep = _phaseStarts[target];
            _phaseIndex = target - 1;
            if (_phaseStarts.Count > target)
                _phaseStarts.RemoveRange(target, _phaseStarts.Count - target);
            TutorialGuide.JumpTo(startStep);
            if (compensateDraw && BattleEntry.TutorialId == TutorialProgressManager.JinjieId
                && PhaseScenarioOf(tutorial, target + 1) == null)
            {
                MatchStatsService.Instance?.ClearTurnScope();
                Core.ZoneManager.DrawCard(Core.Player1, firstDrawOfTurn: true);
            }
        }

        /// <summary>段零范围内首个 fieldclear 步序（段边界=首个 reveal=midTransition 换场步；
        /// 找不到返回 -1）。段零重置落点用（2026-10-19 用户定案：重置回自由操作开始）。</summary>
        private static int IndexOfSegmentFieldClear()
        {
            var steps = TutorialGuide.Steps;
            for (int i = 0; i < steps.Count; i++)
            {
                var s = steps[i];
                if (s == null) continue;
                if (i > 0 && s.reveal == "midTransition") break; // 首个换场步=段零边界
                if (s.type == "fieldclear") return i;
            }
            return -1;
        }

        /// <summary>教学败局「重新进行」询问（2026-10-17）：确认=终局撤销（Ended→Running，教学专用口）
        /// +ApplyMidTransition 单源重铺（双方全清、援军登场、生命 20/20 重置（2026-10-17 晚改值））+**对手先攻直接驱动 AI**
        /// （用户定案：对手不进横置状态就没机会逐个击杀，重开必须对手先——对手回合死=AI 驱动早已
        /// IsGameOver 退场，直接 RunAiTurnAsync 续跑；玩家回合死=EndTurn 翻到对手再驱动，同 midTransition 口径）。
        /// 弹窗=TipPanel（2026-10-17 提示弹窗定案：非对局中目标选择不用 Region）；取消/拆屏=保留
        /// 失败文案等返回主菜单（终局键直回）。</summary>
        private async Cysharp.Threading.Tasks.UniTaskVoid PromptTutorialRetryAsync()
        {
            var tutorial = TutorialLibrary.Get(BattleEntry.TutorialId);
            if (tutorial?.skipScenario == null || Core == null || _ctrl == null) return; // 无考验段的教学不适用
            var picked = await _showTip("考验失败——重新进行？", "重新进行", "返回主菜单");
            if (picked != true || !GameEnded) return; // 取消/拆屏回落；期间已被其他流程处理则不动
            if (!Core.StateManager.ResumeFromEnded())
            {
                Debug.LogWarning("[BattleScreen] 教学重开失败：终局状态撤销被拒（非 Ended？）");
                return;
            }
            _setGameEnded(false);
            // 教学二败局重开（2026-10-21 用户定案「绑右上重置同款」）：旧 ApplyMidTransition 会把
            // 场面铺回早期考验段。死亡时刻通常在 AI 考验回合（必杀直伤）——重铺后驱动 AI 收完回合，
            // 玩家新考验回合自然准备抽牌=命运之牌（不手动补抽，否则双抽）；己方回合死才交回合。
            if (BattleEntry.TutorialId == TutorialProgressManager.JinjieId
                && _phaseIndex > 0 && _phaseStarts.Count > _phaseIndex
                && TutorialGuide.Steps.Count > _phaseStarts[_phaseIndex]
                && TutorialGuide.Steps[_phaseStarts[_phaseIndex]] != null
                && TutorialGuide.Steps[_phaseStarts[_phaseIndex]].reveal == "midTransition")
            {
                JumpToSegmentStart(_phaseIndex, tutorial, compensateDraw: false);
                var aiSide = Ctrl != null ? Ctrl.P2 : null;
                if (aiSide != null && Ctrl.TurnPlayer == aiSide)
                    await Ctrl.RunAiTurnAsync();
                else
                    _endTurnFan();
                return;
            }
            ApplyMidTransition(); // 全清重铺：援军登场+生命重置+UI 对账（toast 同源）
            var p2 = Ctrl != null ? Ctrl.P2 : null;
            if (p2 != null && Ctrl.TurnPlayer == p2)
                await Ctrl.RunAiTurnAsync(); // 对手回合败（打脸致死常态）：驱动早已退场，直接续跑剧本
            else
                _endTurnFan(); // 玩家回合败（疲劳等）：结束回合→翻到对手→驱动（midTransition 同款序列）
        }

        /// <summary>教学第一阶段（basics）通关结算（2026-10-17）：TipPanel 单键弹窗「你已完成第一阶段
        /// 的教学」，确定=回主菜单（Manager.Back，终局口径同认输键直回）。拆屏/缺预制体=null：留在
        /// 终局画面（返回主菜单键仍可用，不自动退）。title 可覆写（2026-10-19 jinjie 第二课复用）。</summary>
        private async UniTaskVoid PromptTutorialCompleteAsync(string title = "你已完成第一阶段的教学")
        {
            if (await _showTip(title, "确定", null) == true)
                _back();
        }

        /// <summary>教学三（末日降临挑战）败局重挑（2026-10-19 挪战场版）：确认=重开战场（Replace 重入
        /// 战屏——五段式开场整体重放：重掷硬币+重选卡组，「换卡组=新局」教学语义保持；组合界面已解锁
        /// 可另组卡组再来）。取消=留在终局画面（「返回主菜单」键同链路直回）。</summary>
        private async UniTaskVoid PromptLunzhuanRetryAsync()
        {
            var picked = await _showTip("末日降临获胜了——重新挑战？（可另组卡组再来）", "重新挑战", "返回");
            if (picked != true || !GameEnded) return; // 取消/拆屏回落
            if (_replayOpening != null) _replayOpening(); // 重开战场=重放开场（重掷硬币+重选卡组）
            else _back(); // 防御兜底（注入缺位=退主菜单重进）
        }

        /// <summary>教学三（末日降临挑战）通关结算（2026-10-19 挪战场版）：确定=单退回主菜单
        ///（栈=[主菜单,战场]——对局准备屏已随 PreMatchScreen 整删；最后一课入口此时已现）。</summary>
        private async UniTaskVoid PromptLunzhuanCompleteAsync()
        {
            if (await _showTip("你已完成第二阶段的教学", "确定", null) == true)
                _back(); // 战场→主菜单
        }
    }
}
