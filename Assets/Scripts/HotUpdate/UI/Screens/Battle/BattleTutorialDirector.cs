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
        private bool GameEnded => _gameEnded();

        private GameObject _introPanel; // TipPanel 实例（2026-10-17：改用预制体，见 ShowIntroPanel）
        private GameObject _tipPanelPrefab; // 惰性加载（Assets/Art/Prefab/TipPanel.prefab：msg=文案、btn-ok=确定）
        private AudioSource _voiceSource;
        private int _voiceSerial; // 连续换段防错播（加载完成时本段可能已被点走）
        private Func<GameCore, Player, List<ResponseOption>, ResponseOption> _tutorialResponder; // 教学守卫应答器（UninstallResponder 卸载）
        private bool _guideHooked;

        public BattleTutorialDirector(Func<RectTransform> root, Func<GameCore> core,
            Func<BattleController> ctrl, Func<BattleFieldView> field, BattleToastHub toast,
            Func<string, string, string, UniTask<bool?>> showTip, Action back,
            Action refreshField, Action refreshFans, Action endTurnFan,
            Func<bool> gameEnded, Action<bool> setGameEnded)
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
        }

        /// <summary>静态引导订阅退订（OnExit 首段——僵尸订阅教训：OnExit 中途异常截断会让
        /// OnStepAdvanced 摄着旧实例跨局复活）。</summary>
        public void UnhookBanner()
        {
            if (_guideHooked) { TutorialGuide.OnStepAdvanced -= OnGuideStepAdvanced; _guideHooked = false; }
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
            else if (BattleEntry.TutorialId == TutorialProgressManager.LunzhuanId)
                PromptLunzhuanCompleteAsync().Forget(); // 教学第二阶段（末日降临挑战）通关：结算弹窗
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
            TutorialGuide.OnStepAdvanced -= OnGuideStepAdvanced; // 先减后加（2026-10-17）：同实例残留订阅幂等去重
            TutorialGuide.OnStepAdvanced += OnGuideStepAdvanced;
            _toast.AttachIntroClick(OnToastIntroClicked); // intro 讲解步=点击横幅推进（2026-10-16 教学一两段式；hub 内幂等单次）
            _guideHooked = true;
        }

        private void OnGuideStepAdvanced(int stepIndex)
        {
            if (Root == null) return; // 僵尸订阅自防（2026-10-17）：本工程 EnterPlayModeOptions=3 禁域重载——
            // 停止 Play 不走框架 OnExit 退订，静态事件摄着旧实例跨 Play 复活；UI 已销毁（Root 假空）即不再响应
            RefreshBanner();
            ConsumeReveal(stepIndex);
            // 介绍段卡片（2026-10-17 配音改版）：intro 步一律走模态卡片（带 audio 播语音，无 audio=文字卡片）；
            // 其余步骤（操作教学/引导完成）一律收卡片回落横幅。
            var step = TutorialGuide.Current;
            if (step != null && step.type == "intro")
            {
                // 守卫演示后的解说卡（2026-10-17 考验改版）：aiGuard 攻击步在结算探针推进——先于编舞器
                // BeginVisual，须等战斗表现收尾再弹（用户剧本：【完整战斗表现后】解说）
                if (stepIndex > 0 && IsAiGuardAttackStep(TutorialGuide.Steps[stepIndex - 1]))
                    ShowIntroPanelAfterCombatAsync(step, stepIndex).Forget();
                else
                    ShowIntroPanel(step);
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

        private static bool IsAiGuardAttackStep(TutorialGuideStepSpec s)
            => s != null && s.type == "attack" && s.aiGuard;

        /// <summary>战斗表现收尾后再弹解说卡（守卫演示步的下一步）：结算探针推进时编舞器尚未
        /// BeginVisual——先让一帧让演出接管，再等视觉在途门归零（与人机节奏 WaitVisualsIdleAsync 同口）；
        /// 期间步骤又被推进（不应发生）则放弃本次弹卡。</summary>
        private async Cysharp.Threading.Tasks.UniTaskVoid ShowIntroPanelAfterCombatAsync(
            TutorialGuideStepSpec step, int stepIndex)
        {
            await Cysharp.Threading.Tasks.UniTask.Yield();
            if (TutorialGuide.StepIndex != stepIndex) return;
            if (Field != null) await Field.WaitVisualsIdleAsync();
            if (TutorialGuide.StepIndex != stepIndex) return;
            ShowIntroPanel(step);
        }

        /// <summary>新步骤成为当前步时的演出指令消费（2026-10-17 语音联动地块显隐定案）：
        /// midTransition=第二段「援军登场」注入（随后演出全恢复）；board/zone:*/hand=
        /// 独占显示语音描述的区块（BattleRevealDirector.Solo——讲哪个亮哪个）。解闸类 token
        /// （zone:role 建柱/hand 亮手牌）随后触发对应对账刷新。</summary>
        private void ConsumeReveal(int stepIndex)
        {
            if (stepIndex < 0 || stepIndex >= TutorialGuide.Steps.Count) return;
            var step = TutorialGuide.Steps[stepIndex];
            if (step == null || string.IsNullOrEmpty(step.reveal)) return;
            if (step.reveal == "midTransition")
            {
                ApplyMidTransition();
                return;
            }
            BattleRevealDirector.Solo(step.reveal);
            // 解闸类 token 须随后对账：zone:role 解柱闸建柱（对手柱建后隐藏）；all=操作预告段
            // 解除对手隐藏，RefreshField 让对手柱当场显现（2026-10-18）；hand 解手牌闸填手牌。
            if (step.reveal == "zone:role" || step.reveal == "all") _refreshField();
            else if (step.reveal == "hand") _refreshFans();
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
        /// 与机器人逐回合剧本（ScriptedAi 按条演出），rngSeed 钉效果随机；机器人响应窗口全让过
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
                    rngSeed: tutorial.rngSeed, scenario: tutorial.skipScenario);
                ctrl.EnablePassiveAiResponder();
                TutorialGuide.Begin(tutorial, tutorial.guideSkip);
                return true;
            }

            if (tutorial.scenario != null)
            {
                var playerCards = TutorialLibrary.BuildDeck(TutorialScenario.DeckIdsOf(tutorial.scenario.player));
                var aiCards = TutorialLibrary.BuildDeck(TutorialScenario.DeckIdsOf(tutorial.scenario.ai));
                ctrl.StartNewGame(playerCards, aiCards, new ScriptedAi(tutorial),
                    rngSeed: tutorial.rngSeed, scenario: tutorial.scenario);
                ctrl.EnablePassiveAiResponder();
                TutorialGuide.Begin(tutorial); // 暂停引导（2026-10-06）：guide 为空时内部不激活
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
                lockDeckOrder: true, rngSeed: tutorial.rngSeed,
                mySkillCardId: entryMySkill, aiSkillCardId: entryAiSkill,
                playerFirst: entryPlayerFirst, secondArmorBonus: entryPlayerFirst.HasValue ? 5 : 0);
            EnableTutorialAiResponder(ctrl, tutorial); // AI 响应：默认让过+aiGuard 守卫演示窗（2026-10-17 剧本）
            TutorialGuide.Begin(tutorial);
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
        private ScriptedAi MakePacedScriptedAi(TutorialConfig tutorial, int turnOffset = 0)
        {
            return new ScriptedAi(tutorial, turnOffset)
            {
                PacingWait = async () =>
                {
                    if (Field != null) await Field.WaitVisualsIdleAsync();
                },
            };
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
            ApplyMidTransition(); // 全清重铺：援军登场+生命重置+UI 对账（toast 同源）
            var p2 = Ctrl != null ? Ctrl.P2 : null;
            if (p2 != null && Ctrl.TurnPlayer == p2)
                await Ctrl.RunAiTurnAsync(); // 对手回合败（打脸致死常态）：驱动早已退场，直接续跑剧本
            else
                _endTurnFan(); // 玩家回合败（疲劳等）：结束回合→翻到对手→驱动（midTransition 同款序列）
        }

        /// <summary>教学第一阶段（basics）通关结算（2026-10-17）：TipPanel 单键弹窗「你已完成第一阶段
        /// 的教学」，确定=回主菜单（Manager.Back，终局口径同认输键直回）。拆屏/缺预制体=null：留在
        /// 终局画面（返回主菜单键仍可用，不自动退）。</summary>
        private async UniTaskVoid PromptTutorialCompleteAsync()
        {
            if (await _showTip("你已完成第一阶段的教学", "确定", null) == true)
                _back();
        }

        /// <summary>教学二（末日降临挑战）败局重挑（2026-10-18 真实对局流程版）：确认=Back 回对局
        /// 准备界面（栈上一屏=PreMatchScreen——重掷硬币+重选卡组；组合界面此时已解锁，可另组卡组
        /// 再战——教学二会话静态标记仍在，模式保持）。取消=留在终局画面（「返回主菜单」键同链路
        /// 直回对局准备）。</summary>
        private async UniTaskVoid PromptLunzhuanRetryAsync()
        {
            var picked = await _showTip("末日降临获胜了——重新挑战？（可另组卡组再来）", "重新挑战", "返回");
            if (picked != true || !GameEnded) return; // 取消/拆屏回落
            _back(); // 上一屏=对局准备界面（教学二模式保持）
        }

        /// <summary>教学二（末日降临挑战）通关结算（2026-10-18）：文案镜像教学一口径；确定=连退两屏
        /// 回主菜单（栈=[主菜单,对局准备,战场]——最后一课入口此时已现，IsCourseCompleted 达成）。</summary>
        private async UniTaskVoid PromptLunzhuanCompleteAsync()
        {
            if (await _showTip("你已完成第二阶段的教学", "确定", null) == true)
            {
                _back(); // 战场→对局准备
                _back(); // 对局准备→主菜单
            }
        }
    }
}
