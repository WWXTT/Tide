using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.AI;
using CardCore.Attribute;
using CardCore.Network;
using CardCore.Serialization;
using GameBoard;
using Tide.HotUpdate;
    using Cysharp.Threading.Tasks;
    using UnityEngine;
    using UnityEngine.EventSystems;
    using UnityEngine.UI;
    using TMPro;

namespace SynergyUI
{
    public sealed partial class BattleScreen : UIScreen
    {
        // ======================================== 本地模式 ========================================
        //（2026-10-15 彻底清除）旧一代本地启动 StartLocal 已删——零调用者的迁移残留，其订阅表与
        // 现役 StartLocalFan 漂移两次咬人（立牌滞留/入场演出失效）。本地局唯一入口=StartLocalFan；
        // 随迁已删：AppendLocal/OnGameOver（仅旧表引用）。RefreshLocal 仍被网络等路径共用故保留。

        // ======================================== 本地对局（扇形手牌过渡面，2026-10-03 接入） ========================================

        /// <summary>本局是否五段式开局（2026-10-19 掷币演出挪战场）：BattleEntry.StagedOpening 限定
        /// 普通对战（LocalAI）与教学三变体（Tutorial·lunzhuan，均主菜单注入）；2026-10-28 联机
        /// 开局流程改版并入（Network·DeckSubmit 相位进战场即五段式：掷币+网络选卡组面板）——
        /// 教学一二/挑战/测试镜像入口恒 false，立即启动路径零变化。</summary>
        private bool StagedOpeningActive()
            => BattleEntry.StagedOpening
               && (BattleEntry.Mode == BattleMode.LocalAI
                   || BattleEntry.Mode == BattleMode.Network
                   || (BattleEntry.Mode == BattleMode.Tutorial
                       && BattleEntry.TutorialId == TutorialProgressManager.LunzhuanId));

        /// <summary>本地对局启动（原型屏 HandFanScreen 移植接入）：开局即建双扇手牌，引擎手牌事件
        /// 幂等对账刷新。战场 3D 重做前的最小对战面——出牌交互暂缺，回合由「结束回合」推进。
        /// 2026-10-19 拆分：staged 局此处分流至 BattleOpeningDirector（地块→角色→硬币→选卡组
        /// 五段演出），段五回调 EngageStagedBattle；非 staged 局原位直调 EngageLocalBattle（行为零变）。</summary>
        private void StartLocalFan()
        {
            _ctrl = new BattleController();
            var mirrorDeck = BattleEntry.MirrorDeck; // 一次性消费（防跨局残留，同 Client 口径）
            BattleEntry.MirrorDeck = null;
            var mirrorSkill = BattleEntry.MirrorSkillCardId;
            BattleEntry.MirrorSkillCardId = null;
            var challengeDeck = BattleEntry.ChallengeDeck; // 一次性消费（挑战模式玩家卡组）
            BattleEntry.ChallengeDeck = null;
            var challengeSkill = BattleEntry.ChallengeSkillCardId;
            BattleEntry.ChallengeSkillCardId = null;
            var testScenario = BattleEntry.Scenario; // 一次性消费（测试开局预设，2026-10-16）
            BattleEntry.Scenario = null;
            // 对局准备流程五静态口（Match/Ai 卡组+技能+硬币先手）已随 2026-10-19 挪战场整删——
            // 掷币+选卡组在战场五段式开局内发生，结果由 EngageStagedBattle 直喂 EngageLocalBattle。

            if (StagedOpeningActive())
            {
                // 五段式开局（2026-10-19 掷币挪战场）：先战场演出（地块就位→角色就位→掷硬币→
                // 战场选卡组），引擎启动整体推迟到段五——BattleOpeningDirector 段末回调
                // EngageStagedBattle 交班。段一~四无 Core/无事件订阅可撞；段二预置柱由段五首次
                // 对账按名收养；AI 先手 kickoff 随 Engage 尾段（防硬币对方先手局冻结的同款洞）。
                WireLocalUi();
                if (BattleEntry.DummyTest)
                {
                    // 木桩测试（2026-10-22）：进入先不开始——选项面板确认后才走五段式开场
                    StartDummyOpeningAsync().Forget();
                    return;
                }
                if (BattleEntry.Mode == BattleMode.Tutorial
                    && BattleEntry.TutorialId == TutorialProgressManager.LunzhuanId)
                {
                    // 教学四毕业连战（2026-11-02 改版·2026-11-11 重编号）：首场战前规则说明弹窗后走五段式开场
                    StartGauntletOpeningAsync().Forget();
                    return;
                }
                _opening = new BattleOpeningDirector(() => _running, _field, false, EngageStagedBattle);
                _opening.RunAsync().Forget();
                return;
            }

            EngageLocalBattle(mirrorDeck, mirrorSkill, challengeDeck, challengeSkill, testScenario);
        }

        /// <summary>教学四毕业连战开局（2026-11-02 改版：末日降临 Boss 战退役）：首场（未选过
        /// 卡组）先弹规则说明（TipPanel 双键：开始连战/返回主菜单）；确认或后续场次（Picked=true
        /// ——场间弹窗「继续挑战」与败局「再战」均经 Manager.Replace 重入，不再打扰）直接走五段式
        /// 开场（段四首场单轮选我方卡组、后续沿用已选；段五对手=LunzhuanGauntlet.OpponentName
        /// 预组序列）。取消/拆屏=退回主菜单（连战态由下次主菜单入口 Begin 作废）。</summary>
        private async UniTaskVoid StartGauntletOpeningAsync()
        {
            if (!LunzhuanGauntlet.Picked)
            {
                var ok = await ShowTipPromptAsync(
                    "教学四·毕业连战：用你的卡组（或直接选一套预组）依次击败三套预组——红·快攻、蓝·控制、绿·慢速。\n"
                    + "三场全胜即完成教学；任一场失败可保留胜场，重整旗鼓再战当前对手。",
                    "开始连战", "返回主菜单");
                if (!_running) return; // 拆屏（面板随 Root 销毁自清）
                if (ok != true)
                {
                    Manager.Back();
                    return;
                }
            }
            _opening = new BattleOpeningDirector(() => _running, _field, true, EngageStagedBattle);
            _opening.RunAsync().Forget();
        }

        /// <summary>staged 局段五交班（BattleOpeningDirector 回调）：掷币/选卡组结果直喂
        /// EngageLocalBattle；开场导演先退役（指针闸让位）+首刷门随段五起（Core 就位即刷）。
        /// forceMatch=true=对局分支强制（null 卡组随机兜底+硬币先后手+后手 5 护甲；旧对局准备流程
        /// 全随机选择回落无头兜底分支丢硬币结果的潜在洞顺带堵上——兜底分支保留给测试路径）。
        /// myTokenBindings/aiTokenBindings（P9b）：所选卡组的衍生物槽绑定。</summary>
        private void EngageStagedBattle(List<CardData> myDeck, string mySkill, List<CardData> aiDeck,
            string aiSkill, bool? playerFirst,
            List<DeckTokenBinding> myTokenBindings = null, List<DeckTokenBinding> aiTokenBindings = null)
        {
            _opening?.Dispose();
            _opening = null;
            RefreshFieldWhenBoardReady().Forget();
            EngageLocalBattle(null, null, null, null, null, myDeck, mySkill, aiDeck, aiSkill,
                playerFirst, forceMatch: true, myTokenBindings, aiTokenBindings);
        }

        /// <summary>木桩测试开局（2026-10-22）：进入战场先不开始——模态弹选项面板（先后手/洗牌/
        /// 元素池），确认后按选项走五段式开场（固定先后手=开场跳过掷币段；掷硬币=照常）；
        /// 返回主菜单=直接退屏（无引擎已建，OnExit 常规收口即可）。三选项经 BattleEntry
        /// 静态口带给 EngageLocalBattle 木桩分支（段五交班时消费）。</summary>
        private async UniTaskVoid StartDummyOpeningAsync()
        {
            Debug.Log("[木桩] 选项面板已显示（等待确认）");
            // 2026-10-22 根因修复：dead 语义=「已死该退出」，须传 !_running。旧代码照抄
            // BattleOpeningDirector 的 () => _running 惯例（那边内部取反），导致面板刚建好
            // 循环即判死退出→恒走取消分支→当帧 Back 回主菜单（用户所见"点了没反应"）。
            var picked = await DummyOptionsPanel.ShowAsync(() => !_running, Root);
            if (!_running)
            {
                Debug.Log("[木桩] 面板拆屏退出（_running=false）");
                return; // 拆屏（面板随 Root 销毁自清）
            }
            if (picked == null)
            {
                Debug.Log("[木桩] 面板取消——返回主菜单");
                Manager.Back();
                return;
            }
            BattleEntry.DummyPlayerFirst = picked.PlayerFirst;
            BattleEntry.DummyShuffle = picked.Shuffle;
            BattleEntry.DummyFullPool = picked.FullPool;
            BattleEntry.DummySeed = picked.Seed; // 2026-11-11 随机种子钉子（0=每局新抽）
            Debug.Log("[BattleScreen] 木桩选项：先后手=" + (picked.PlayerFirst == null ? "掷硬币"
                    : picked.PlayerFirst == true ? "我方先手" : "对方先手")
                + " 洗牌=" + picked.Shuffle + " 元素池充满=" + picked.FullPool);
            _opening = new BattleOpeningDirector(() => _running, _field, false, EngageStagedBattle,
                picked.PlayerFirst, dummy: true, overlayParent: Root, onCancelled: () => Manager.Back());
            _opening.RunAsync().Forget();
        }

        /// <summary>本地对局启动后半段（原 StartLocalFan 尾段整体抽出·2026-10-19 五段式开局复用——
        /// 顺序与时序红线零变化）：_log.Wire→WireEarly→模式分支（StartNewGame/教学装配）→Wire→
        /// 扇/Region 接线→补偿刷→AI 先手 kickoff。
        /// myTokenBindings/aiTokenBindings（P9b 2026-10-29）：卡组期衍生物槽绑定——随所用卡组
        /// 的分支注入（match/dummy/挑战/教学注入），镜像/随机无绑定（固定绑定走原子 str）。</summary>
        private void EngageLocalBattle(List<CardData> mirrorDeck, string mirrorSkill,
            List<CardData> challengeDeck, string challengeSkill, TutorialScenario testScenario,
            List<CardData> matchDeck = null, string matchSkill = null, List<CardData> aiDeck = null,
            string aiSkill = null, bool? playerFirst = null, bool forceMatch = false,
            List<DeckTokenBinding> myTokenBindings = null, List<DeckTokenBinding> aiTokenBindings = null)
        {
            // 本地战报桥须先于 StartNewGame 接线（时序红线——详见 BattleLogController.Wire 注释）
            _log.Wire();

            // 抽卡飞入（时序红线——先于 StartNewGame，详见 BattleProjector.WireEarly 注释）
            _projector.WireEarly();

            // 木桩种子钉子（2026-11-11 日志复现链）：一次性消费——0=每局新抽（StartNewGame 统一抽定）
            int dummySeed = BattleEntry.DummyTest ? BattleEntry.DummySeed : 0;
            BattleEntry.DummySeed = 0;
            int? dummyRngSeed = dummySeed != 0 ? dummySeed : (int?)null;

            if (BattleEntry.Mode == BattleMode.Tutorial
                && BattleEntry.TutorialId == TutorialProgressManager.LunzhuanId)
                // 教学四毕业连战（2026-11-02 改版·2026-11-11 重编号）：不走教学装配（引导/课规/
                // 剧本 AI/被动响应器全不装）——AI 正式对手（MakePacedAi+AutoMatch 按预组 Theme
                // 标签映主题策略，木桩难度 2 同款；响应行为归驱动自身内置启发），常规洗牌常规
                // 开局（不钉种子——同对手重战不复现同局）。胜负不直接落档：TutorialFlow 连战
                // 拦截 + 终局弹窗链（LunzhuanGauntlet.ReportWin，三场全胜才 MarkTutorialCompleted）。
            {
                _ctrl.StartNewGame(matchDeck, aiDeck,
                    MakePacedAi(aiDeck != null ? AiStrategy.AutoMatch(aiDeck) : null),
                    mySkillCardId: matchSkill, aiSkillCardId: aiSkill,
                    playerFirst: playerFirst, secondArmorBonus: GameCore.SecondPlayerArmorBonus,
                    myTokenBindings: myTokenBindings, aiTokenBindings: aiTokenBindings);
            }
            else if (BattleEntry.Mode == BattleMode.Tutorial)
            {
                // 教学局（2026-10-18 教学二含对局准备流程注入的 matchDeck/aiDeck/硬币先手/技能——
                // 必须先于普通 matchDeck 分支接管，否则教学装配（引导+课规+剧本 AI）被匹配分支抢吞）；
                // 装配失败回落普通局（2026-10-18 晚修正）：必须同拍降级 Mode——旧口径回落局仍挂
                // Tutorial 标记，ReportBattleResult 会把这场无引导普通局的胜利误记成过课
                // （真机实录：热更配置档解析失败→Get(basics)=null→随机卡组白板局→打赢即
                // MarkTutorialCompleted，存档污染后教学按钮路由直跳教学二「教学一已通过」）。
                if (!_tutorial.StartGame(_ctrl))
                {
                    BattleEntry.Mode = BattleMode.LocalAI; // 回落局不计教学进度（胜负都不落档）
                    _ctrl.StartNewGame(matchDeck, aiDeck,
                        MakePacedAi(aiDeck != null ? AiStrategy.AutoMatch(aiDeck) : null),
                        mySkillCardId: matchSkill, aiSkillCardId: aiSkill,
                        playerFirst: playerFirst, secondArmorBonus: GameCore.SecondPlayerArmorBonus,
                        myTokenBindings: myTokenBindings, aiTokenBindings: aiTokenBindings);
                }
            }
            else if (BattleEntry.DummyTest)
                // 木桩测试局（2026-10-22；2026-10-23 选卡组弹窗+难度三档）：双方卡组由掷币后弹窗选定
                //（我方=玩家档+三套预组；木桩侧限三套主题预组；null=随机在此解析——场景管线的
                // hand/deck 引用须与卡组同源）。对手难度（BattleEntry.DummyDifficulty）：三档对手
                // 统一=预组主题策略 AI（2026-11-03 用户定案：AutoMatch——红·快攻→红快攻、蓝·控制→
                // 蓝控制、绿·慢速→绿慢速，预组卡全带各自 Theme 标签精确命中；0 档旧「全让过木桩」
                // 退役）；洗牌关=锁牌库序（可复现）；
                // 元素池充满=按所选卡组组「地牌充满」预设（素材缺失回落常规开局）。
                // 后手护甲补偿（2026-10-30 修正）：本入口 2026-10-25 起承载正式本地对战（打AI 归
                // 木桩难度档），旧「纯测试床与正式局差异从简」口径作废——掷币/固定先后手的后手
                // 角色 +5 护甲（GameRules.SecondPlayerArmorBonus 迁表单源），与联机/教学同规则。
                // 例外：地牌充满预设走 scenario 分支（起跳场面自钉），维持测试预设从简不注入。
            {
                // 随机兜底=三套预组随机其一（2026-10-22；玩家池已空）——技能随预组回传
                string myRandomSkill = null, aiRandomSkill = null;
                var myDummy = matchDeck ?? BattleController.RandomDeck(out myRandomSkill);
                var aiDummy = aiDeck ?? BattleController.RandomDeck(out aiRandomSkill);
                string myDummySkill = matchSkill ?? myRandomSkill;
                string aiDummySkill = aiSkill ?? aiRandomSkill;
                // 2026-11-03 用户定案：木桩测试三档难度对手统一=预组主题策略 AI（AutoMatch——
                // 红·快攻→红快攻、蓝·控制→蓝控制、绿·慢速→绿慢速；0 档旧「全让过木桩」退役）。
                // 响应行为归驱动自身内置启发，不加外部约束（被动响应器只剩教学固定局在用）。
                IAiTurnDriver dummyDriver = MakePacedAi(AiStrategy.AutoMatch(aiDummy));
                if (BattleEntry.DummyFullPool
                    && DummyTestFlow.TryBuildFullPool(myDummy, aiDummy, BattleEntry.DummyShuffle,
                        playerFirst != false, out var scen, out var mySeeded, out var aiSeeded))
                    _ctrl.StartNewGame(mySeeded, aiSeeded, dummyDriver,
                        rngSeed: dummyRngSeed,
                        scenario: scen, mySkillCardId: myDummySkill, aiSkillCardId: aiDummySkill);
                else
                    _ctrl.StartNewGame(myDummy, aiDummy, dummyDriver,
                        rngSeed: dummyRngSeed,
                        lockDeckOrder: !BattleEntry.DummyShuffle,
                        mySkillCardId: myDummySkill, aiSkillCardId: aiDummySkill, playerFirst: playerFirst,
                        secondArmorBonus: GameCore.SecondPlayerArmorBonus,
                        myTokenBindings: myTokenBindings, aiTokenBindings: aiTokenBindings);
            }
            else if (matchDeck != null || forceMatch)
                // 对局准备流程（2026-10-19 挪战场五段式开局：掷币+选卡组结果段五注入，旧
                // PreMatchScreen 当日整删）：AI 策略按其卡组自适配（卡组缺=缺省策略）；硬币后手
                // 角色 +5 护甲（StartNewGame 内注入）。forceMatch=staged 局恒走本分支（null 卡组
                // 随机兜底——掷币结果不因全随机选择漏进无头兜底分支）
                _ctrl.StartNewGame(matchDeck, aiDeck,
                    MakePacedAi(aiDeck != null ? AiStrategy.AutoMatch(aiDeck) : null),
                    mySkillCardId: matchSkill, aiSkillCardId: aiSkill,
                    playerFirst: playerFirst, secondArmorBonus: GameCore.SecondPlayerArmorBonus,
                    myTokenBindings: myTokenBindings, aiTokenBindings: aiTokenBindings);
            else if (mirrorDeck != null)
                // 测试镜像局（TestStartFlow btn-teststart 专用载体，2026-10-19 结业镜像局退役后唯一
                // 注入方）：AI 打同卡组（技能标记双方同用——镜像口径）。
                // testScenario 非空=测试开局：镜像局改走教学注入管线
                //（跳起手+Seeder 静默铺场+起跳回合），null=普通镜像开局
                _ctrl.StartNewGame(mirrorDeck, mirrorDeck,
                    aiDriver: MakePacedAi(AiStrategy.RedAggro),
                    scenario: testScenario,
                    mySkillCardId: mirrorSkill, aiSkillCardId: mirrorSkill);
            else if (BattleEntry.Mode == BattleMode.Challenge)
            {
                // 挑战模式（2026-10-06 单值联动）：难度 N = 电脑地牌槽 9+N、起手 6+N；
                // AI 卡组缺省随机 30 张，策略按卡组主色自适配（AutoMatch：标签优先回落费用主色）
                // 技能（2026-10-07 卡牌化）：玩家=卡组标记；AI 随机卡组自动挑一张合格结界
                var difficulty = Mathf.Clamp(BattleEntry.ChallengeDifficulty, 1, ChallengeProgressManager.MaxDifficulty);
                BattleEntry.ChallengeDifficulty = difficulty;
                var challTokens = BattleEntry.ChallengeTokenBindings; // 一次性消费（同 ChallengeDeck 口径）
                BattleEntry.ChallengeTokenBindings = null;
                string challAiRandomSkill;
                var challAiDeck = BattleController.RandomDeck(out challAiRandomSkill); // 三套预组随机其一（玩家池已空）
                _ctrl.StartNewGame(challengeDeck, challAiDeck, MakePacedAi(AiStrategy.AutoMatch(challAiDeck)),
                    aiLandCapBonus: difficulty, aiExtraOpeningDraws: difficulty,
                    mySkillCardId: challengeSkill,
                    aiSkillCardId: !string.IsNullOrEmpty(challAiRandomSkill)
                        ? challAiRandomSkill
                        : HeroSkillSystem.AutoPickSkillCard(challAiDeck),
                    myTokenBindings: challTokens); // P9b 挑战卡组绑定
            }
            else
                // 普通本地局兜底（2026-10-13 补）：LocalAI 无镜像时原链无分支命中=空战场不 InitGame
                // 2026-10-14 人机节奏定案：P2 走等待模式——每个动作等表现层演完再执行下一个
                _ctrl.StartNewGame(aiDriver: MakePacedAi());

            // 对账订阅表（2026-10-18 B2 投影单点化——唯一投影器 Battle/BattleProjector：
            // 事件→置脏/演出载荷钩子/终局流转；本屏不再直订引擎对账事件）
            _projector.Wire();

            // 对局种子展示（2026-11-11 日志复现链）：本地局 StartNewGame 已钉（木桩钉值/新抽）——
            // GameRng 检查点单源；联机局走 NetOnRoomState 的 RoomState.Seed
            var seedCp = CardCore.GameRng.Capture();
            if (seedCp.Seeded) _log.ShowSeed(seedCp.Seed);

            WireLocalUi(); // 扇/Region/响应窗接线（2026-10-19 抽出——原位调用，行为零变）

            RefreshFans();
            RefreshField(); // 补偿刷（2026-10-13）：订阅注册前已发生的静默变化（教学场景注入零事件）兜底
            _tutorial.RefreshBanner(); // 教学首步文案（Begin 在 StartGame 内已触发过事件，此处补首帧）

            // 硬币 AI 先手（2026-10-18 修）：开局即对手回合——AI 驱动此前只挂在玩家结束回合链上
            //（OnEndTurnFan），AI 先手时首回合无人驱动=冻结在 P2 主阶段（教学二幻卡清算/普通硬币
            // 对局同洞）。放在订阅齐备之后 kickoff，AI 动作的视图对账已接线；教学介绍段（intro
            // 面板）不拦引擎回合——幻卡清算会在首张介绍卡背后完成，确认后即可见末日降临。
            if (_ctrl != null && _ctrl.TurnPlayer == P2 && !_gameEnded)
                RunAiFirstTurnAsync().Forget();

            Debug.Log("[BattleScreen] 本地对局已启动（扇形手牌过渡面；战场 3D 重做中）");
        }

        /// <summary>引擎无关接线（原 StartLocalFan 接线段抽出·2026-10-19 五段式开局复用，幂等）：
        /// 手牌扇交互/Region 目标选择器/响应窗口注册。staged 局在开场演出前先接（扇区/选择器就绪，
        /// 演出期无引擎请求在途）；非 staged 局保持原位（StartNewGame 后）调用。</summary>
        private void WireLocalUi()
        {
            // 手牌扇交互接线（2026-10-14；SpUse 钩子随 2026-11-01 上卡退役）：
            // 悬停显「放置/使用」按钮+可发动流光（对手扇不注入=无按钮）
            _selfFan?.SetInteractions(new FanInteractions
            {
                CanUse = FanCanUse,
                CanPlaceLand = FanCanPlaceLand,
                OnUse = OnFanUse,
                OnPlaceLand = OnFanPlaceLand,
            });

            // 通用目标选择器（效果引擎结算期反问）：Region 面板选卡（2026-10-15 用户定案）。
            // 修复（2026-10-15）：此前注册只写在无人调用的 StartLocal——真实启动路径 StartLocalFan
            // 漏注册，结算期反问恒 ui=null→auto 静默代选（磨刀兽「没让选自动挑目标」根因）。
            // 2026-10-16 定案：UiTargetSelector 旧列表弹窗删除——不再作 Region 缺失兜底
            //（缺 Region=无交互反问，Service 自动代选）；法术声明期目标预选同批退役，
            // 指向性法术目标统一走结算期反问（与生物/角色技能同 UI 同源）。
            _regionSelector = _regionPanelCtl != null
                // 2026-10-16：boardCellOf=场上候选红瞄准面片；isLocalPlayer=角色候选【己方/对方角色】判侧（本地局人类恒 P1）；
                // roleCellOf=角色候选的红瞄准面片落格（选中才上盘·2026-10-16 晚定案）
                // buildPrompt/onCancelAvailable（2026-10-17 label-num 并入定案）：开窗=提示全文（完整
                // 效果文）直写 Region 面板 label-num；主动效果亮「取消」——详见 BuildSelectPrompt/OnSelectCancel
                ? new RegionTargetSelector(_regionPanelCtl.PanelNode, BoardCellOf, p => p == P1, RoleCellOf, BuildSelectPrompt, _decision.OnSelectCancel, CloseInformation)
                : null;
            TargetSelectionService.Current = _regionSelector;

            // 移动原子选格反问桥（2026-10-31 场面轴3c）：结算期 MoveUnit handler 经此弹格——
            // 与召唤选格同交互；卸载随本界面清理段（AI/无头无桥=安全空转）
            CellSelectionService.Current = PickMoveDestinationAsync;

            // 响应窗口（2026-10-14 接回）：守卫候选走棋盘箭头交互（效果类候选回落弹窗）
            ResponseWindowService.HumanResponder = _decision.ShowResponseAsync;
        }

        /// <summary>开局即 AI 回合的驱动（硬币对方先手，2026-10-18）：跑完对手首回合后全量对账
        /// ——尾段与 OnEndTurnFan 同口径（RefreshLocal）。</summary>
        private async UniTaskVoid RunAiFirstTurnAsync()
        {
            if (_ctrl == null) return;
            await _ctrl.RunAiTurnAsync();
            RefreshLocal();
        }

        /// <summary>目标候选的场上占格（2026-10-16 红瞄准面片）：BattleFieldView 单源查询——
        /// 单位（生物/结界/衍生物）与施放占格类型图标在场上即返回数据格；不在场（手牌/墓地/牌库等）
        /// 返回 null（无面片）。纯读对账记账，不触引擎。</summary>
        private (int x, int z)? BoardCellOf(Card card)
        {
            if (_field != null && _field.TryGetOnBoardCell(card.RuntimeId, out int x, out int z))
                return (x, z);
            return null;
        }

        /// <summary>角色候选的红瞄准面片落格（2026-10-16 晚定案·选中才上盘）：引擎单源
        /// BoardState.CharacterCell（角色格布局常量，角色立柱同格）；控制器未挂板=无面片。</summary>
        private (int x, int z)? RoleCellOf(Player p)
        {
            var board = _ctrl?.Board;
            if (board == null || p == null) return null;
            var cell = board.CharacterCell(p);
            return (cell.x, cell.z);
        }

        /// <summary>结束己方回合并让 AI 跑完对手回合（手牌变化经事件链刷新扇区）。</summary>
        private async void OnEndTurnFan()
        {
            if (NetActive) { NetEndTurn(); return; } // 网络局：intent 上行（对手回合由真人对手驱动）
            if (_gameEnded || Core == null || _ctrl.TurnPlayer != P1) return;
            if ((_field != null && _field.VisualsBusy) // 表演中不许结束回合（2026-10-17）：演出与回合状态撕裂防护
                || (_director != null && _director.Busy)) // 步进回放在途（2026-10-19）：表现层未追平引擎真值
            {
                _toast.Show("战斗演出进行中——稍后再结束回合");
                return;
            }
            if (!GameActions.EndTurn(Core, P1, out var endTurnReason))
            {
                // 2026-10-17：此前静默吞（连锁窗在途时栈空守卫拒绝→玩家只见「点了没反应」）——
                // 2026-11-03 失败提示细化：引擎真因直显；待处理选择窗的文案更准，仍优先
                _toast.Show(_regionSelector != null && _regionSelector.IsOpen
                    ? "有响应/选择窗口待处理——先选卡或让过"
                    : endTurnReason ?? "连锁尚未结算——暂时无法结束回合");
                return;
            }
            // 回合切换延迟一拍生效（EndTurn 排队推进、引擎泵下一帧才翻 TurnPlayer——同帧读
            // 仍 P1，曾因此从不驱动 AI 回合＝「结束回合后电脑不动」，2026-10-14）：
            // 先等翻到 P2 再交 AI 驱动。等待预算 10 秒防卡——但回合末反问（手牌上限弃牌等）开窗
            // 期间折返门（GameLoopController 等 PendingRequests==0）刻意挂起，玩家选卡远超 1 秒：
            // 反问在途不烧预算（2026-10-15 修复——旧版 60 帧后放弃等待，弃牌选完后 AI 回合
            // 无人驱动，整局冻结在对手主阶段）；反问自身有超时代选安全网，无无限等待风险。
            // 阶段优先权窗（2026-11-01）：EndTurn 先开「结束阶段开始」窗（0 速伪栈对象，玩家可在
            // 窗内打 1 速手牌响应）——栈非空期间同样不烧预算；且退出条件加栈空：折返落 P2 主阶段
            // 时「主阶段开始」窗若开着（我方人类候选弹窗在途），不得提前驱动 AI 回合——窗口期间
            // 非空栈会让 AI 的 0 速出牌被速度门拒绝（2026-11-01 同日修订：窗②时点迁主阶段开始）。
            // 2026-11-03 冻结修复：结束窗的步进回放会按住引擎泵（AcquirePresentationHold），
            // 期间回合切换不可能推进——这种「演出在途」与反问同权不烧预算；预算也从 60 帧
            // （1 秒）放宽到 600 帧（10 秒）。预算耗尽即 AI 回合孤儿（再无人驱动=整局冻结），
            // 必须大声告警留证，不再静默吞。
            int idle = 0;
            while (!_gameEnded && Core != null
                   && (_ctrl.TurnPlayer != P2
                       || (Core.StackEngine != null && !Core.StackEngine.IsEmpty)))
            {
                if (CardCore.TargetSelectionService.PendingRequests > 0
                    || (Core.StackEngine != null && !Core.StackEngine.IsEmpty)
                    || (_director != null && _director.Busy)      // 步进回放在途=泵被 hold，回合切换排队长尾
                    || (_field != null && _field.VisualsBusy))    // 演出在途同理
                    idle = 0;
                else if (++idle > 600)
                {
                    Debug.LogWarning($"[BattleScreen] 结束回合后等回合切换超时（10s）——TurnPlayer={_ctrl.TurnPlayer?.Name} " +
                                     "栈非空=" + (Core.StackEngine != null && !Core.StackEngine.IsEmpty) +
                                     "，放弃驱动 AI 回合（对局可能冻结，需人工排查）");
                    break;
                }
                await Cysharp.Threading.Tasks.UniTask.Yield();
            }
            if (!_gameEnded && _ctrl.TurnPlayer == P2)
                await _ctrl.RunAiTurnAsync();
        }

        /// <summary>人机节奏驱动（2026-10-14 用户定案）：人类在场的分支（普通/镜像/挑战）给 P2 的
        /// 脚本 AI 注入 PacingWait——电脑每个动作后等表现层视觉在途门归零（等一件事演完再做下一件）
        /// + 最小间隔拍（2026-10-16 用户定案：1 秒）。AI-vs-AI（编辑器批量/无头）
        /// 不注入=全速，完全不管渲染层。2026-10-16 出牌舞台：同批注入开演/收幕回调——
        /// 对手出牌先亮（左上角卡面+指向箭头）1 秒再结算。</summary>
        private SimpleAI MakePacedAi(AiStrategy strategy = null)
        {
            return new SimpleAI(strategy)
            {
                PacingWait = async () =>
                {
                    if (_field != null) await _field.WaitVisualsIdleAsync();
                    if (_director != null) await _director.WaitIdleAsync(); // 步进回放追平才许下一个动作（2026-10-19）
                },
                OnCastStaged = _castStage.Show,
                OnCastStageEnd = _castStage.Hide,
            };
        }


        /// <summary>终局弹窗（扇形过渡面口径；旧链 OnGameOver 的 RefreshLocal 依赖已删预制体字段，不走）。
        /// 2026-10-16 弹窗退役：终局呈现=toast+战报行，退出=认输键改「返回主菜单」直回（MarkGameOverUi）。
        /// 2026-10-17 教学败局追加：考验「打输重来」——弹「是否重新进行」，确认=终局撤销+midTransition
        /// 全清重铺（援军登场+生命 20/20 重置（2026-10-17 晚改值））原地续打（用户剧本：打赢就过，打输重来）。</summary>
        private void OnGameOverFan(GameOverEvent e)
        {
            _gameEnded = true;
            // 脚本化续接（2026-10-19 教学二多段化）：上段考验的预设击杀（引导未完、当前步非 victory）
            // ——终局弹窗/过课落档全跳过，冻结交互等换场步异步续接（终局撤销+重铺下一段）。
            if (BattleEntry.Mode == BattleMode.Tutorial && _tutorial != null && _tutorial.IsScriptedContinuation)
                return;
            bool win = e.Winner == P1;
            TutorialFlow.ReportBattleResult(BattleEntry.Mode, win); // 教学逻辑层：考核局/教学局结果落档
            MarkGameOverUi($"{(win ? "胜利" : "失败")}——{(win ? "我方" : "对手")}获胜（{e.Reason}）");
            _tutorial.PromptBattleOver(win); // 教学终局分发（Retry/Complete——BattleTutorialDirector·2026-10-18 拆分）
        }

        /// <summary>终局呈现（2026-10-16 旧弹窗退役）：提示走 toast+战报；认输键文案改「返回主菜单」
        ///（OnConcede 终局分支直回——旧弹窗取消键职责迁移，btn-back 已退役本屏无其他退出口）。</summary>
        private void MarkGameOverUi(string msg)
        {
            _toast.Show(msg);
            _log.Append(msg, "log-line--system");
            if (_btnConcede != null)
            {
                var lbl = _btnConcede.GetComponentInChildren<TMP_Text>(true);
                if (lbl != null) lbl.text = "返回主菜单";
            }
        }

        /// <summary>抽卡飞入武装（2026-10-17 抽卡演出）：抽牌对账前把卡堆顶世界坐标注入对应
        /// 手牌扇（新卡从卡堆弧线飞入扇位）+ 卡堆 XZ 脉冲。起手抽牌（_fanFlyArmed=false）与
        /// 教学介绍段（SuppressHandFan——防起点滞留到解除后的对账误飞）不注入；卡堆/相机/
        /// 坐标转换任一失败静默=新建卡维持原吸附行为。</summary>
        private void ArmDrawFlyIn(Player player)
        {
            if (!_fanFlyArmed || BattleRevealDirector.SuppressHandFan) return;
            int owner = player == P2 ? 1 : 0;
            var fan = owner == 0 ? _selfFan : _oppFan;
            if (fan == null) return;
            var cam = BattleStageDirector.WorldCam;
            if (cam == null || !BattleZoneProps.DeckTopWorldPosition(owner, out var top)) return;
            if (fan.SetDrawOriginFromWorld(top, cam))
                BattleZoneProps.PulseDeck(owner);
        }

        /// <summary>幂等全量对账：己方手牌全正面；对手手牌默认背面，带 Exposed 指示物的翻正面。
        /// 教学介绍段闸（2026-10-17 语音联动显隐）：介绍到费用段前手牌区保持空。
        /// S3b 手牌口径（2026-11-05 收口·五处同源）：施放中的卡留手牌区但**不上扇**——
        /// 与网络 HandCount/观测 g[5]/g[6]/手牌上限/BattleViewData.SelfHand 一致；施放呈现走链 UI。</summary>
        private void RefreshFans()
        {
            if (BattleRevealDirector.SuppressHandFan) return;
            if (Core?.ZoneManager == null || P1 == null || P2 == null) return;
            var stackEngine = Core.StackEngine;

            var self = new List<HandFanCardData>();
            foreach (var card in Core.ZoneManager.GetCards(P1, Zone.Hand))
            {
                if (stackEngine != null && stackEngine.IsPendingCast(card)) continue; // S3b：剔除施放中
                self.Add(new HandFanCardData
                {
                    RuntimeId = card.RuntimeId,
                    // FromHand（六区口径+RuntimeCard 事件锚）；FromBattle 只填旧版 Stats/Keywords
                    // 纯文本，ACardView 不消费——2026-10-15 修复手牌属性/关键词芯片恒空白
                    Item = CardOverlayItem.FromHand(BattleView.FromRuntime(
                        SerializableRuntimeCardState.FromCard(card), card, ownerIsOpponent: false)),
                    FaceUp = true,
                });
            }

            var opp = new List<HandFanCardData>();
            foreach (var card in Core.ZoneManager.GetCards(P2, Zone.Hand))
            {
                if (stackEngine != null && stackEngine.IsPendingCast(card)) continue; // S3b：剔除施放中（对手扇不显示其施放卡）
                bool exposed = RevealRules.IsExposed(card); // 信息轴「展示」：对双方公开
                opp.Add(new HandFanCardData
                {
                    RuntimeId = card.RuntimeId,
                    Item = exposed
                        ? CardOverlayItem.FromHand(BattleView.FromRuntime(
                            SerializableRuntimeCardState.FromCard(card), card, ownerIsOpponent: true))
                        : BlankBack(card.RuntimeId),
                    FaceUp = exposed,
                });
            }

            // 牌库顶当手牌（2026-10-29 阶段3b）：光环命中己方且牌库非空 → 手牌扇末尾显示牌库顶
            // 虚拟手牌（真实卡实例——出牌走 fromZone=Deck 授权路径；打出后顶牌自动换新。
            // 不计 7 张上限/不可弃/不可放地——放地按钮经 FanCanPlaceLand 手牌区限定；
            // 对手扇不显示=天然对手不可见）。
            var deckTop = TopDeckVirtualCard();
            if (deckTop != null)
                self.Add(new HandFanCardData
                {
                    RuntimeId = deckTop.RuntimeId,
                    Item = CardOverlayItem.FromHand(BattleView.FromRuntime(
                        SerializableRuntimeCardState.FromCard(deckTop), deckTop, ownerIsOpponent: false)),
                    FaceUp = true,
                });

            _selfFan?.SetCards(self);
            _oppFan?.SetCards(opp);
            EvaluateFanPlayability(); // 可发动流光随对账刷新（费用/上限/阶段变化即时反映）
        }

        /// <summary>牌库顶虚拟手牌（2026-10-29 阶段3b，本地局）：TopDeckAsHandAura 命中己方且
        /// 牌库非空 → 牌库顶卡（列表 index0——牌库序=牌库顶）；null=不显示（光环未开/牌库空/
        /// 网络局——快照不承载牌库序，联机侧暂不显示，随协议补字段接 BuildNet）。</summary>
        private Card TopDeckVirtualCard()
        {
            if (NetActive || Core?.ZoneManager == null || P1 == null) return null;
            if (!CardCore.RuleAuraSystem.ScopeHits(CardCore.RuleAuraComponents.TopDeckAsHandAura, P1))
                return null;
            var deck = Core.ZoneManager.GetCards(P1, Zone.Deck);
            return deck != null && deck.Count > 0 ? deck[0] : null;
        }

        /// <summary>背面卡的空内容（Key 仅为口径完整；文字被 back 覆盖不可见）。</summary>
        private static CardOverlayItem BlankBack(uint runtimeId)
            => new CardOverlayItem { Key = "R" + runtimeId };

    }
}
