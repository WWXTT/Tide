using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CardCore;
using CardCore.Attribute;
using SynergyUI;
using UnityEditor;
using UnityEngine;

namespace CardCore.Editor.Tests
{
    /// <summary>
    /// 教学固定局验证器（2026-10-02 教学模式；2026-10-06 扩 S4/S5）：
    /// S1 配置装载——TutorialConfig.json 可读、basics 条目卡组/剧本齐备且卡 id 全在卡池；
    /// S2 锁序确定性——同配置两次 InitGame(lockDeckOrder+钉种子)状态一致，且双方「手牌+牌库」连接序
    ///    与配置全序逐张一致（InitGame 内 StartNewTurn(P1) 已自动抽掉玩家第 1 回合的牌，连接序不受影响
    ///    ——这是"每回合摸到的卡固定"的机制断言）；
    /// S3a 剧本全量——P1 纯让过（只 EndTurn），机器人 5 个剧本回合全部动作必须落地
    ///    （含 attack 硬断言——无人对抗时攻击者必然存活）；同时断言整局摸牌序列与配置一致；
    /// S3b 整局胜负——P1=SimpleAI（通用策略扮演教学玩家，编辑器无真人），断言自然走到 GameOver 且玩家获胜
    ///    （机器人 R5 后无剧本被动、响应窗口全让过；attack 容忍失败——P1 清场优先，机器人单位可能先阵亡）；
    /// S4 场面直入——scenario-demo 条目按预设场面开局：起跳回合/主阶段/归属、零抽牌、双方
    ///    生命/bank/手牌/牌库/墓地/单位（覆写·横置·落位）/地牌（横置·余量）逐项==预设，
    ///    地牌槽曲线对齐起跳回合，随后机器人剧本仍可正常推进（教学直入 2026-10-06）；
    /// S5 进度逻辑——TutorialProgressManager/TutorialFlow 状态机（临时存档路径，不碰真实档）：
    ///    建档待询问→选跳过→考核胜=免教学（按钮换匹配）；考核负=留挑战路径（不转课程，
    ///    开调卡组窗口重试）；多课序列 tianji→lunzhuan→chuangzuo 逐课推进；三课全完成→
    ///    最后一课镜像局胜=门禁解除；存档重读一致；
    /// S8 第三课创作管线——走查三步差集检测（新建效果→含新效果的卡→含新卡的卡组；卡内新建
    ///    效果单轮补记两步）；最后一课镜像局入口/胜利落档；跳过路径默认卡组缺失回落随机镜像；
    ///    真实镜像局开局冒烟（同一卡组双侧构成一致）。
    /// 运行：Tools/教学/教学固定局验证。风格对齐 Editor/验证/AI对战。
    /// </summary>
    public static class TutorialScriptVerifier
    {
        private const string TutorialId = "basics";
        private const string ScenarioTutorialId = "scenario-demo";
        private const int MaxTurns = 40;

        [MenuItem("Tools/教学/教学固定局验证")]
        public static void Run()
        {
            var errors = new List<string>();
            try
            {
                // ---- S1 配置装载 ----
                TutorialLibrary.Reload();
                var tut = TutorialLibrary.Get(TutorialId);
                if (tut == null)
                {
                    errors.Add($"S1: TutorialConfig.json 无 {TutorialId} 条目（TutorialLibrary 加载失败或文件缺失）");
                    Report(errors);
                    return;
                }
                if (tut.playerDeck == null || tut.playerDeck.Count < GameCore.OpeningHandSize)
                    errors.Add($"S1: playerDeck 不足 {GameCore.OpeningHandSize} 张（{tut.playerDeck?.Count ?? 0}）");
                if (tut.aiDeck == null || tut.aiDeck.Count < GameCore.OpeningHandSize)
                    errors.Add($"S1: aiDeck 不足 {GameCore.OpeningHandSize} 张（{tut.aiDeck?.Count ?? 0}）");
                if (tut.aiScript == null || tut.aiScript.Count == 0)
                    errors.Add("S1: aiScript 为空");
                if (errors.Count > 0) { Report(errors); return; }

                var deckSpec1 = TutorialLibrary.BuildDeck(tut.playerDeck);
                var deckSpec2 = TutorialLibrary.BuildDeck(tut.aiDeck);
                if (deckSpec1.Count != tut.playerDeck.Count)
                    errors.Add($"S1: 玩家卡组 {tut.playerDeck.Count - deckSpec1.Count} 个 id 不在卡池");
                if (deckSpec2.Count != tut.aiDeck.Count)
                    errors.Add($"S1: 机器人卡组 {tut.aiDeck.Count - deckSpec2.Count} 个 id 不在卡池");
                if (errors.Count > 0) { Report(errors); return; }

                // ---- S2 锁序确定性 ----
                var core = GameCore.Instance;
                core.InitGame(CardLoader.BuildDeck(deckSpec1, 1), CardLoader.BuildDeck(deckSpec2, 1), tut.rngSeed, true);
                var snapshotA = SnapshotZones(core);
                core.InitGame(CardLoader.BuildDeck(deckSpec1, 1), CardLoader.BuildDeck(deckSpec2, 1), tut.rngSeed, true);
                if (snapshotA != SnapshotZones(core))
                    errors.Add("S2: 两次锁序初始化状态不一致（起手或牌库序漂移）");
                CheckZoneAgainstConfig(errors, core.Player1, tut.playerDeck, "玩家");
                CheckZoneAgainstConfig(errors, core.Player2, tut.aiDeck, "机器人");

                // ---- S3a 剧本全量（P1 纯让过）----
                var passRun = RunGame(tut, deckSpec1, deckSpec2, playerMode: "pass", errors);
                if (passRun != null)
                {
                    if (passRun.AiTurns < tut.aiScript.Count)
                        errors.Add($"S3a: 机器人仅执行 {passRun.AiTurns} 回合（剧本 {tut.aiScript.Count} 回合未跑完——P1 纯让过不应提前终局）");
                    errors.AddRange(passRun.Failures);
                    Debug.Log($"[教学验证] S3a: 剧本全量遍——机器人 {passRun.AiTurns} 回合、动作失败 {passRun.Failures.Count} 项，" +
                              $"P1 摸牌 {passRun.Draws1.Count} 张 / P2 摸牌 {passRun.Draws2.Count} 张");
                }

                // ---- S3b 整局胜负（P1=SimpleAI）----
                var battleRun = RunGame(tut, deckSpec1, deckSpec2, playerMode: "simpleai", errors);
                if (battleRun != null)
                {
                    errors.AddRange(battleRun.Failures); // element/play 硬断言（attack 已在 S3a 硬断言，此处容忍）
                    Debug.Log($"[教学验证] S3b: 整局胜负遍——P1 摸牌 {battleRun.Draws1.Count} 张 / P2 摸牌 {battleRun.Draws2.Count} 张");
                }

                // ---- S4 场面直入（scenario-demo 预设场面）----
                RunScenarioChecks(errors);

                // ---- S5 教学进度逻辑（临时存档）----
                RunProgressChecks(errors);

                // ---- S6 第一课·田忌赛马（三线难度证明）----
                RunLessonChecks(errors);

                // ---- S7 第二课·资源流转（归土破壁）----
                RunLesson2Checks(errors);

                // ---- S8 第三课·创作管线走查 + 最后一课镜像局 ----
                RunCreationLessonChecks(errors);
            }
            catch (Exception ex)
            {
                errors.Add($"异常：{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            }
            Report(errors);
        }

        /// <summary>一局教学对局的结果（摸牌序列=CardDrawEvent 含起手 6 张，按序记录）。</summary>
        private sealed class RunResult
        {
            public readonly List<string> Draws1 = new List<string>();
            public readonly List<string> Draws2 = new List<string>();
            public int AiTurns;
            public readonly List<string> Failures = new List<string>();
        }

        /// <summary>
        /// 跑一局教学配置对局。playerMode：pass=P1 每回合只 EndTurn（剧本全量遍）；
        /// simpleai=P1 由通用 SimpleAI 扮演（整局胜负遍）。返回 null=局内异常已记入 errors。
        /// </summary>
        private static RunResult RunGame(TutorialConfig tut,
            List<CardData> deckSpec1, List<CardData> deckSpec2, string playerMode, List<string> errors)
        {
            var result = new RunResult();
            var core = GameCore.Instance;
            BattleController ctrl = null;
            Func<GameCore, Player, List<ResponseOption>, ResponseOption> prevResponder = null;
            Action<CardDrawEvent> onDraw = null;
            Action<GameOverEvent> onOver = null;
            bool gameOver = false;
            GameOverEvent gameOverEvent = null;
            Player p1 = null;

            try
            {
                // 订阅先于 InitGame：起手 6 张同样走 DrawCard→CardDrawEvent，期望序列=卡组全序。
                // 归属在事件时点取 core.Player1（Player 实例跨 InitGame 复用，Reset 只复位），
                // 不能靠订阅前缓存的引用——InitGame 期间的起手事件也要正确分桶。
                onDraw = e =>
                {
                    if (e?.DrawnCard == null) return;
                    if (e.Player == core.Player1) result.Draws1.Add(e.DrawnCard.ID);
                    else result.Draws2.Add(e.DrawnCard.ID);
                };
                onOver = e => { gameOver = true; gameOverEvent = e; };
                EventManager.Instance.Subscribe<CardDrawEvent>(onDraw);
                EventManager.Instance.Subscribe<GameOverEvent>(onOver);

                core.InitGame(CardLoader.BuildDeck(deckSpec1, 1), CardLoader.BuildDeck(deckSpec2, 1), tut.rngSeed, true);
                p1 = core.Player1;
                core.Player1.IsAI = true; // 双 AI 全自动（目标选择走自动路径，镜像 AiBattleE2E）
                core.Player2.IsAI = true;

                // 教学响应态：AI 响应窗口全让过——镜像 BattleController.EnablePassiveAiResponder
                prevResponder = ResponseWindowService.AiResponder;
                ResponseWindowService.AiResponder = (c, holder, options) => ResponseWindowService.PassOption;

                ctrl = new BattleController(); // 驱动桥（卡组已手工 InitGame，不经 StartNewGame 随机组卡）
                var ai1 = playerMode == "simpleai" ? new SimpleAI() : null;
                var ai2 = new ScriptedAi(tut);
                bool hardAttack = playerMode == "pass"; // 剧本全量遍连 attack 也硬断言

                for (int turn = 0; turn < MaxTurns && !gameOver; turn++)
                {
                    if (core.TurnEngine.TurnPlayer == core.Player1)
                    {
                        if (ai1 != null) ai1.TakeTurn(ctrl);
                        else GameActions.EndTurn(core, core.Player1);
                    }
                    else
                    {
                        result.AiTurns++;
                        ai2.TakeTurn(ctrl);
                        foreach (var (action, ok) in ai2.LastTurnReport)
                        {
                            // S3b 中 attack 容忍失败（P1 清场优先，机器人攻击者可能先阵亡属正常战况）
                            if (!ok && (hardAttack || action.type != "attack"))
                                result.Failures.Add($"机器人回合 {result.AiTurns}：{action.type} {action.card ?? action.attacker} 未落地");
                        }
                    }
                    if (!gameOver)
                        core.TurnEngine.CheckPhaseTransition(); // 补 End→Standby 折返（编辑器无帧泵）
                }

                // 摸牌序列断言：实际摸牌（含起手）与配置顺序逐张一致
                CompareDrawSequence(errors, result.Draws1, tut.playerDeck, "玩家");
                CompareDrawSequence(errors, result.Draws2, tut.aiDeck, "机器人");

                if (playerMode == "pass")
                {
                    if (gameOver)
                        result.Failures.Add($"S3a: P1 纯让过即分胜负（{gameOverEvent.Reason}，{gameOverEvent.TotalTurns} 回合）——教学局对让过方不应致死");
                }
                else
                {
                    if (!gameOver)
                        errors.Add($"S3b: {MaxTurns} 回合内未分胜负（教学局节奏异常：机器人应被动败退）");
                    else if (gameOverEvent.Winner != p1)
                        errors.Add($"S3b: 教学局胜者非玩家（{gameOverEvent.Reason}，{gameOverEvent.TotalTurns} 回合）");
                    else
                        Debug.Log($"[教学验证] S3b: 对局 {gameOverEvent.TotalTurns} 回合玩家获胜（{gameOverEvent.Reason}）");
                }
                return result;
            }
            catch (Exception ex)
            {
                errors.Add($"playerMode={playerMode} 异常：{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
            finally
            {
                if (onDraw != null) EventManager.Instance.Unsubscribe<CardDrawEvent>(onDraw);
                if (onOver != null) EventManager.Instance.Unsubscribe<GameOverEvent>(onOver);
                ResponseWindowService.AiResponder = prevResponder; // 教学响应态还原（防泄漏到其它验证）
                ctrl?.Shutdown(); // 棋盘静态接线归零（镜像 AiBattleE2E 收尾惯例）
            }
        }

        // ======================================== S4 场面直入（2026-10-06 教学直入） ========================================

        /// <summary>
        /// scenario-demo 预设场面开局断言：起跳回合/主阶段/归属、零抽牌（起手与回合抽都不发生）、
        /// 双方生命/bank/疲劳/手牌/牌库/墓地/单位/地牌逐项==预设、地牌槽曲线对齐起跳回合、
        /// 指定格落位（BoardState first-free）；随后 P1 让过、机器人剧本回合照常推进（教学直入后引擎可继续跑）。
        /// </summary>
        private static void RunScenarioChecks(List<string> errors)
        {
            var tut = TutorialLibrary.Get(ScenarioTutorialId);
            if (tut?.scenario == null)
            {
                errors.Add($"S4: TutorialConfig.json 无 {ScenarioTutorialId} 条目或缺 scenario（教学直入管线未配置）");
                return;
            }
            var sc = tut.scenario;

            int draws = 0;
            Action<CardDrawEvent> onDraw = null;
            BattleController ctrl = null;
            Func<GameCore, Player, List<ResponseOption>, ResponseOption> prevResponder = null;
            try
            {
                onDraw = _ => draws++;
                EventManager.Instance.Subscribe<CardDrawEvent>(onDraw);
                prevResponder = ResponseWindowService.AiResponder;
                ResponseWindowService.AiResponder = (c, holder, options) => ResponseWindowService.PassOption;

                var core = GameCore.Instance;
                var deck1 = TutorialLibrary.BuildDeck(TutorialScenario.DeckIdsOf(sc.player));
                var deck2 = TutorialLibrary.BuildDeck(TutorialScenario.DeckIdsOf(sc.ai));
                if (deck1.Count != TutorialScenario.DeckIdsOf(sc.player).Count
                    || deck2.Count != TutorialScenario.DeckIdsOf(sc.ai).Count)
                {
                    errors.Add("S4: scenario 卡 id 有不在卡池者（BuildDeck 跳过了缺失 id）");
                    return;
                }

                var ai2 = new ScriptedAi(tut);
                ctrl = new BattleController();
                ctrl.StartNewGame(deck1, deck2, ai2, rngSeed: tut.rngSeed, scenario: sc);
                core.Player1.IsAI = true; // 双 AI 全自动（镜像 RunGame；无人类响应弹窗）
                core.Player2.IsAI = true;

                // ---- 引擎起跳态 ----
                Require(errors, core.TurnEngine.TurnNumber == sc.startTurn,
                    $"S4: 起跳回合 {core.TurnEngine.TurnNumber} != 预设 {sc.startTurn}");
                Require(errors, core.TurnEngine.CurrentPhase?.Phase == PhaseType.Main,
                    $"S4: 起跳阶段 {core.TurnEngine.CurrentPhase?.Phase} != Main");
                var firstOk = sc.FirstIsPlayer1 ? core.TurnEngine.TurnPlayer == core.Player1
                    : core.TurnEngine.TurnPlayer == core.Player2;
                Require(errors, firstOk, "S4: 起跳回合归属与 firstPlayer 不符");
                Require(errors, draws == 0, $"S4: 场面直入不应发生任何抽牌（实际 {draws} 张）");
                Require(errors, core.ElementPool.GetLandCap(core.Player1) == sc.startTurn,
                    $"S4: 玩家地牌槽 {core.ElementPool.GetLandCap(core.Player1)} 未对齐起跳回合 {sc.startTurn}");

                // ---- 双侧场面逐项 ----
                CheckScenarioSide(errors, core, core.Player1, sc.player, "玩家");
                CheckScenarioSide(errors, core, core.Player2, sc.ai, "机器人");

                // ---- 指定格落位（demo 条目玩家侧两单位显式落格）----
                var p1Units = core.ZoneManager.GetCards(core.Player1, Zone.Battlefield) ?? new List<Card>();
                RequireCell(errors, ctrl.Board, p1Units, "C_9CBDD76F", 4, 4);
                RequireCell(errors, ctrl.Board, p1Units, "C_3C06E8E8", 6, 4);

                // ---- 剧本可推进：P1 让过 → 机器人回合 1 全动作落地 ----
                GameActions.EndTurn(core, core.Player1);
                core.TurnEngine.CheckPhaseTransition(); // End→Standby 折返（编辑器无帧泵）→ 机器人回合开始
                ai2.TakeTurn(ctrl);
                int okCount = 0;
                foreach (var (action, ok) in ai2.LastTurnReport)
                {
                    if (ok) okCount++;
                    else errors.Add($"S4: 机器人剧本动作 {action.type} {action.card ?? action.attacker} 未落地");
                }
                Require(errors, okCount >= 3, $"S4: 机器人剧本动作落地 {okCount} 项 < 3");
                if (!core.IsGameOver)
                    core.TurnEngine.CheckPhaseTransition(); // 折回 P1 回合，回合机继续可转
                Require(errors, !core.IsGameOver, "S4: 场面直入后一个让合回即终局（场面配置异常）");
                Debug.Log($"[教学验证] S4: 场面直入——回合 {sc.startTurn} 主阶段、零抽牌、双侧场面/落位断言通过，剧本推进 {okCount} 动作落地");
            }
            catch (Exception ex)
            {
                errors.Add($"S4 异常：{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                if (onDraw != null) EventManager.Instance.Unsubscribe<CardDrawEvent>(onDraw);
                ResponseWindowService.AiResponder = prevResponder;
                ctrl?.Shutdown();
            }
        }

        /// <summary>单侧预设逐项断言：生命（含上限）/疲劳/bank/手牌/牌库/墓地序/单位（覆写·横置）/地牌（横置·余量）。</summary>
        private static void CheckScenarioSide(List<string> errors, GameCore core, Player p,
            TutorialSideState side, string label)
        {
            var prefix = $"S4: {label}侧";
            if (side == null)
            {
                errors.Add($"{prefix}配置缺失");
                return;
            }

            Require(errors, p.Life == side.life, $"{prefix}生命 {p.Life} != 预设 {side.life}");
            Require(errors, p.MaxHealth == side.life, $"{prefix}生命上限 {p.MaxHealth} != 预设 {side.life}");
            Require(errors, p.FatigueCount == side.fatigue, $"{prefix}疲劳 {p.FatigueCount} != 预设 {side.fatigue}");

            var pool = core.ElementPool.GetPool(p);
            if (side.bank != null)
            {
                for (int i = 0; i < 6; i++)
                    Require(errors, pool.AvailableMana[(ManaType)i] == side.bank[i],
                        $"{prefix}bank[{i}] {pool.AvailableMana[(ManaType)i]} != 预设 {side.bank[i]}");
            }

            SeqEqual(errors, ZoneIds(core, p, Zone.Hand), side.hand, $"{prefix}手牌");
            SeqEqual(errors, ZoneIds(core, p, Zone.Deck), side.deck, $"{prefix}牌库");
            SeqEqual(errors, ZoneIds(core, p, Zone.Graveyard), side.grave, $"{prefix}墓地");

            var units = core.ZoneManager.GetCards(p, Zone.Battlefield) ?? new List<Card>();
            Require(errors, units.Count == (side.units?.Count ?? 0),
                $"{prefix}单位数 {units.Count} != 预设 {(side.units?.Count ?? 0)}");
            foreach (var spec in side.units ?? new List<TutorialUnitSpec>())
            {
                var card = units.FirstOrDefault(c => c.ID == spec.card);
                if (card == null)
                {
                    errors.Add($"{prefix}场上缺单位 {spec.card}");
                    continue;
                }
                if (spec.power >= 0)
                    Require(errors, card.GetPower() == spec.power,
                        $"{prefix}{spec.card} 攻击力 {card.GetPower()} != 覆写 {spec.power}");
                if (spec.life >= 0)
                    Require(errors, card.GetLife() == spec.life,
                        $"{prefix}{spec.card} 生命 {card.GetLife()} != 覆写 {spec.life}");
                Require(errors, card.IsTapped() == spec.tapped,
                    $"{prefix}{spec.card} 横置态 {card.IsTapped()} != 预设 {spec.tapped}");
            }

            var pooled = pool.PooledCards;
            Require(errors, pooled.Count == (side.lands?.Count ?? 0),
                $"{prefix}地牌数 {pooled.Count} != 预设 {(side.lands?.Count ?? 0)}");
            Require(errors, (core.ZoneManager.GetCards(p, Zone.ElementPool) ?? new List<Card>()).Count == pooled.Count,
                $"{prefix}元素池区域张数与池内张数脱节");
            foreach (var spec in side.lands ?? new List<TutorialLandSpec>())
            {
                var pc = pooled.FirstOrDefault(x => x.SourceCard != null && x.SourceCard.ID == spec.card);
                if (pc == null)
                {
                    errors.Add($"{prefix}元素池缺地牌 {spec.card}");
                    continue;
                }
                Require(errors, pc.IsTapped == spec.tapped,
                    $"{prefix}地牌 {spec.card} 横置态 {pc.IsTapped} != 预设 {spec.tapped}");
                if (spec.tokens > 0)
                    Require(errors, pc.TotalTokenCount == spec.tokens,
                        $"{prefix}地牌 {spec.card} 余量 {pc.TotalTokenCount} != 预设 {spec.tokens}");
                else
                    Require(errors, pc.TotalTokenCount > 0, $"{prefix}地牌 {spec.card} 未按费用生成指示物");
            }
        }

        private static void RequireCell(List<string> errors, GameBoard.BoardState board,
            List<Card> units, string id, int x, int z)
        {
            var card = units.FirstOrDefault(c => c.ID == id);
            if (card == null || board == null)
            {
                errors.Add($"S4: 落位断言缺卡 {id}");
                return;
            }
            board.TryGetCell(card, out var cx, out var cz);
            Require(errors, cx == x && cz == z, $"S4: {id} 落位 ({cx},{cz}) != 预设 ({x},{z})");
        }

        // ======================================== S5 教学进度逻辑（2026-10-06） ========================================

        /// <summary>进度状态机断言（临时存档路径，不碰真实档）：建档→询问→考核胜负分支→教学完成→落盘重读。</summary>
        private static void RunProgressChecks(List<string> errors)
        {
            var paths = new List<string>();
            try
            {
                // ① 首次建档：待询问、门禁主菜单
                var mgr = TutorialProgressManager.CreateForVerification(TempProgressPath(paths));
                Require(errors, mgr.SkipAskPending, "S5: 建档后应待询问跳过");
                Require(errors, !mgr.IsTutorialDone && mgr.ShouldGateMainMenu, "S5: 建档后教学未完成应门禁主菜单");
                Require(errors, TutorialFlow.ResolveTutorialButtonClick(mgr) == TutorialButtonClickAction.AskSkip,
                    "S5: 首次点击教学按钮应=AskSkip");

                // ② 选跳过 + 考核胜利 = 免教学（教学按钮换匹配、门禁解除）；存档重读一致
                mgr.ChooseSkip(true);
                Require(errors, !mgr.SkipAskPending, "S5: ChooseSkip 后询问标记应消费");
                Require(errors, TutorialFlow.ResolveTutorialButtonClick(mgr) == TutorialButtonClickAction.StartAssessment,
                    "S5: 选跳过后点击应=StartAssessment（考核局未打）");
                mgr.RecordAssessmentResult(true);
                Require(errors, mgr.Skipped && mgr.IsTutorialDone && !mgr.ShouldGateMainMenu,
                    "S5: 考核胜利应免教学（门禁解除、按钮换匹配）");
                var path1 = paths[0];
                var reload = TutorialProgressManager.CreateForVerification(path1);
                Require(errors, reload.Skipped && reload.IsTutorialDone, "S5: 存档重读应保持免教学");

                // ③ 考核失败 = 留在挑战路径（不转课程——可调整默认卡组后重试）；重试获胜=免教学
                var mgr2 = TutorialProgressManager.CreateForVerification(TempProgressPath(paths));
                mgr2.ChooseSkip(true);
                mgr2.RecordAssessmentResult(false);
                Require(errors, !mgr2.IsTutorialDone && mgr2.ShouldGateMainMenu, "S5: 考核失败不应免教学");
                Require(errors, string.IsNullOrEmpty(mgr2.CurrentTutorial),
                    "S5: 考核失败不应转课程（留挑战路径，currentTutorial 应为空）");
                Require(errors, mgr2.ShouldAllowAssessmentDeckAdjust,
                    "S5: 考核失败应开「调卡组再挑战」窗口（ShouldAllowAssessmentDeckAdjust）");
                Require(errors, TutorialFlow.ResolveTutorialButtonClick(mgr2) == TutorialButtonClickAction.StartAssessment,
                    "S5: 考核失败后点击应=StartAssessment（重试镜像局）");
                mgr2.RecordAssessmentResult(true); // 调整卡组后重试获胜
                Require(errors, mgr2.Skipped && mgr2.IsTutorialDone && !mgr2.ShouldAllowAssessmentDeckAdjust,
                    "S5: 重试获胜应免教学并关调卡组窗口");

                // ③b 逐课完成推进序列：三课全完成 → 最后一课镜像局获胜 = 门禁解除（教学整体完成）
                var mgr4 = TutorialProgressManager.CreateForVerification(TempProgressPath(paths));
                mgr4.ChooseSkip(false); // 消费询问标记（真实玩家走完三课时必然早已消费）
                Require(errors, mgr4.NextTutorialId == LessonId && !mgr4.ShouldShowFinalChallenge,
                    "S5: 序列首门应为第一课且未到最后一课");
                mgr4.MarkTutorialCompleted(LessonId);
                Require(errors, !mgr4.IsTutorialDone && mgr4.NextTutorialId == Lesson2Id,
                    "S5: 第一课完成后应推进第二课");
                mgr4.MarkTutorialCompleted(Lesson2Id);
                Require(errors, !mgr4.IsTutorialDone && mgr4.NextTutorialId == TutorialProgressManager.CreationLessonId,
                    "S5: 第二课完成后应推进第三课（chuangzuo）");
                Require(errors, !mgr4.ShouldShowFinalChallenge, "S5: 第三课未完成不应出现最后一课按钮");
                mgr4.MarkTutorialCompleted(TutorialProgressManager.CreationLessonId);
                Require(errors, mgr4.IsCourseCompleted && mgr4.NextTutorialId == null && !mgr4.IsTutorialDone,
                    "S5: 三课全完成后无课可开、教学未整体完成（等最后一课）");
                Require(errors, mgr4.ShouldShowFinalChallenge, "S5: 三课全完成后应出现最后一课按钮");
                Require(errors, TutorialFlow.ResolveTutorialButtonClick(mgr4) == TutorialButtonClickAction.StartFinalChallenge,
                    "S5: 三课全完成后教学按钮等效入口应=StartFinalChallenge");
                mgr4.MarkFinalChallengeDone();
                Require(errors, mgr4.IsTutorialDone && !mgr4.ShouldGateMainMenu && !mgr4.ShouldShowFinalChallenge,
                    "S5: 最后一课镜像局获胜应解除门禁（教学整体完成）");

                // ④ 选择不跳过 = 直接进教学（第一课）
                var mgr3 = TutorialProgressManager.CreateForVerification(TempProgressPath(paths));
                mgr3.ChooseSkip(false);
                Require(errors, mgr3.CurrentTutorial == LessonId && !mgr3.SkipAskPending,
                    $"S5: 选择不跳过应直接进入第一课（{LessonId}）教学流程");
                Require(errors, TutorialFlow.ResolveTutorialButtonClick(mgr3) == TutorialButtonClickAction.StartTutorial,
                    "S5: 不跳过后点击应=StartTutorial");

                Debug.Log("[教学验证] S5: 进度逻辑——建档/询问/考核胜负/多课序列/最后一课/落盘重读 全分支通过");
            }
            catch (Exception ex)
            {
                errors.Add($"S5 异常：{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                foreach (var path in paths)
                    if (File.Exists(path)) File.Delete(path);
            }
        }

        private static string TempProgressPath(List<string> registry)
        {
            var path = Path.Combine(Path.GetTempPath(), $"TideTutorialProgress_{Guid.NewGuid():N}.json");
            registry.Add(path);
            return path;
        }

        // ======================================== S6 第一课·田忌赛马（三线难度证明，2026-10-06） ========================================

        private const string LessonId = "tianji";
        private const string LGeneral = "C_7E5A1001";  // 教学生物1 主将 6/6
        private const string LWall = "C_7E5A1002";     // 教学生物2 壁垒 0/7（专职守卫）
        private const string LPawn = "C_7E5A1003";     // 教学生物3 卒 2/2（最小=引导守卫者）
        private const string LReserve = "C_7E5A1004";  // 教学生物4 援军 2/3
        private const string LBehemoth = "C_7E5A1005"; // 教学生物5 巨兽 6/3（致命威胁）
        private const string LWolfA = "C_7E5A1006";    // 教学生物6 狼 2/2
        private const string LWolfB = "C_7E5A1007";    // 教学生物7 狼 2/2
        private const string LTank = "C_7E5A1008";     // 教学生物8 岩甲兽 3/9（换血演示沙包）
        private const string LWolfC = "C_7E5A1009";    // 教学生物9 狼3 2/2
        private const string LReserve2 = "C_7E5A100A"; // 教学生物10 援军2 2/3（与 4 分 id 供剧本双点名）

        /// <summary>
        /// 第一课（2026-10-06 第三轮剧本：两回合强制演示+自由段考验）四线证明：
        /// 闸门线——引导①期间错误目标/锁定生物/打脸/提前结束全拒；引导动作放行并完成换血演示
        ///   （岩甲兽 9→3、主将 6→3 双活）；引导②期间攻击全拒、结束回合放行；驱动机器人 R1 后
        ///   断言演示结果（狼打已横置主将=单向 2 点、巨兽被卒拦截=单向 6 点卒亡、三步全完成引导退场）；
        /// 解线——自由段：清横置双威胁 → R2 壁垒扛岩甲/援军2拦狼2/放狼3 进脸（选择性承受 2 点）
        ///   → 清场 → 磨脸获胜。断言：全局 11 回合胜、终局生命 3、引导零漏做；
        /// 贪线——演示照走、自由段全打脸从不守卫：R2 五只全活 15 点 ≥ 剩 14 生命即死。断言：全局 6 机器人胜；
        /// 龟线——演示照走、只守最优从不攻击：守卫供给耗尽。断言：机器人胜。
        /// </summary>
        private static void RunLessonChecks(List<string> errors)
        {
            var tut = TutorialLibrary.Get(LessonId);
            if (tut?.scenario == null)
            {
                errors.Add($"S6: TutorialConfig.json 无 {LessonId} 条目或缺 scenario");
                return;
            }
            if (tut.guide == null || tut.guide.Count != 3)
            {
                errors.Add($"S6: {LessonId} 缺 guide 三步配置（attack+end+guard）");
                return;
            }

            // ---- 闸门线（含 R1 演示结果断言）----
            RunLessonGateChecks(tut, errors);

            // ---- 解线：引导回合只做引导攻击（ScriptedAi 收尾 EndTurn 自动完成 end 步骤）；
            //      自由段 T2 清横置巨兽+狼 → T3 清光三只 → T4/T5 磨脸；守卫顺位：巨兽=卒、岩甲=壁垒、狼2=援军2、狼3 放行 ----
            var solution = RunLessonLine(tut, new TutorialConfig
            {
                id = "tianji-solution",
                aiScript = new List<TutorialTurnScript>
                {
                    Turn(1, A("attack", attacker: LGeneral, target: LTank)),
                    Turn(2, A("attack", attacker: LGeneral, target: LBehemoth), A("attack", attacker: LReserve, target: LWolfA)),
                    Turn(3, A("attack", attacker: LGeneral, target: LTank),
                        A("attack", attacker: LReserve, target: LWolfB), A("attack", attacker: LReserve2, target: LWolfC)),
                    Turn(4, A("attack", attacker: LGeneral, target: "hero"),
                        A("attack", attacker: LReserve, target: "hero"), A("attack", attacker: LReserve2, target: "hero")),
                    Turn(5, A("attack", attacker: LGeneral, target: "hero"),
                        A("attack", attacker: LReserve, target: "hero"), A("attack", attacker: LReserve2, target: "hero")),
                },
            }, new Dictionary<string, string[]>
            {
                [LBehemoth] = new[] { LPawn },
                [LTank] = new[] { LWall },
                [LWolfB] = new[] { LReserve2 },
                // 狼3 无条目=放行（自由段唯一必须承受的 2 点——选择性承伤）
            }, errors, "解线");
            if (solution != null)
            {
                Require(errors, solution.WinnerIsPlayer,
                    $"S6 解线: 玩家未获胜（{solution.TotalTurns} 回合，终局生命 {solution.PlayerLife}）");
                Require(errors, solution.MissedStepIndices.Count == 0,
                    $"S6 解线: 引导步骤有漏做（{string.Join(",", solution.MissedStepIndices)}）——按引导走应全数完成");
                Require(errors, solution.TotalTurns == 11,
                    $"S6 解线: {solution.TotalTurns} 回合 != 11（预期玩家第 5 个回合收尾）");
                Require(errors, solution.PlayerLife == 3,
                    $"S6 解线: 终局玩家生命 {solution.PlayerLife} != 3（战斗伤害仅狼3 的 2 点+疲劳 1+2+3+4）");
                errors.AddRange(solution.Failures);
            }

            // ---- 贪线：演示照走，自由段全打脸、从不守卫 → R2 全存活 15 点压死 ----
            var greedy = RunLessonLine(tut, new TutorialConfig
            {
                id = "tianji-greedy",
                aiScript = new List<TutorialTurnScript>
                {
                    Turn(1, A("attack", attacker: LGeneral, target: LTank)),
                    Turn(2, A("attack", attacker: LGeneral, target: "hero"),
                        A("attack", attacker: LReserve, target: "hero"), A("attack", attacker: LReserve2, target: "hero")),
                },
            }, new Dictionary<string, string[]> { [LBehemoth] = new[] { LPawn } }, errors, "贪线");
            if (greedy != null)
            {
                Require(errors, !greedy.WinnerIsPlayer && greedy.TotalTurns == 6,
                    $"S6 贪线: 全打脸不守卫应在机器人 R2（全局 6）被 15 点压死（实际 {(greedy.WinnerIsPlayer ? "玩家胜" : "机器人胜")}，{greedy.TotalTurns} 回合，生命 {greedy.PlayerLife}）");
                errors.AddRange(greedy.Failures);
            }

            // ---- 龟线：演示照走，自由段只守最优从不攻击 → 守卫耗尽（plan=null 走守卫启发）----
            var turtle = RunLessonLine(tut, new TutorialConfig
            {
                id = "tianji-turtle",
                aiScript = new List<TutorialTurnScript>
                {
                    Turn(1, A("attack", attacker: LGeneral, target: LTank)),
                },
            }, null, errors, "龟线");
            if (turtle != null)
            {
                Require(errors, !turtle.WinnerIsPlayer && turtle.TotalTurns >= 6 && turtle.TotalTurns <= 10,
                    $"S6 龟线: 只守不攻应耗尽守卫落败（实际 {(turtle.WinnerIsPlayer ? "玩家胜" : "机器人胜")}，{turtle.TotalTurns} 回合）");
                errors.AddRange(turtle.Failures);
            }
        }

        /// <summary>闸门线：直接调 GameActions 断言三步引导闸 + 换血演示结算 + 驱动机器人 R1 断言演示结果。</summary>
        private static void RunLessonGateChecks(TutorialConfig tut, List<string> errors)
        {
            var core = GameCore.Instance;
            BattleController ctrl = null;
            Func<GameCore, Player, List<ResponseOption>, ResponseOption> prevResponder = null;
            try
            {
                var deck1 = TutorialLibrary.BuildDeck(TutorialScenario.DeckIdsOf(tut.scenario.player));
                var deck2 = TutorialLibrary.BuildDeck(TutorialScenario.DeckIdsOf(tut.scenario.ai));
                ctrl = new BattleController();
                var ai2 = new ScriptedAi(tut);
                ctrl.StartNewGame(deck1, deck2, ai2, rngSeed: tut.rngSeed, scenario: tut.scenario);
                core.Player1.IsAI = true; // 双 AI 全自动（守卫走下方脚本应答器，非 AI 会走 HumanResponder=null 自动让过）
                core.Player2.IsAI = true;
                TutorialGuide.Begin(tut);

                var general = FindOnBoard(core, core.Player1, LGeneral);
                var pawn = FindOnBoard(core, core.Player1, LPawn);
                var reserve = FindOnBoard(core, core.Player1, LReserve);
                var tank = FindOnBoard(core, core.Player2, LTank);
                var behemoth = FindOnBoard(core, core.Player2, LBehemoth);
                if (general == null || pawn == null || reserve == null || tank == null || behemoth == null)
                {
                    errors.Add("S6 闸门线: 场面缺教学生物（主将/卒/援军/岩甲兽/巨兽）");
                    return;
                }

                // ---- 引导①（attack 主将→岩甲兽）期间：一切偏差拒绝 ----
                Require(errors, !GameActions.DeclareAttack(core, core.Player1, general, behemoth),
                    "S6 闸门线: 错误目标（巨兽——互毁）的攻击未被拒绝");
                Require(errors, !GameActions.DeclareAttack(core, core.Player1, general, core.Player2),
                    "S6 闸门线: 非引导目标（打脸——无交换演示）的攻击未被拒绝");
                Require(errors, !GameActions.DeclareAttack(core, core.Player1, pawn, core.Player2),
                    "S6 闸门线: 锁定生物（卒，保留待守卫）的攻击未被拒绝");
                Require(errors, !GameActions.DeclareAttack(core, core.Player1, reserve, tank),
                    "S6 闸门线: 锁定生物（援军）的攻击未被拒绝");
                Require(errors, !GameActions.EndTurn(core, core.Player1),
                    "S6 闸门线: 引导①未完成时结束回合未被钉住");

                // 引导动作放行 → 换血演示（岩甲兽 9-6=3、主将 6-3=3，双方存活=交换伤害）
                Require(errors, GameActions.DeclareAttack(core, core.Player1, general, tank),
                    "S6 闸门线: 引导动作（主将→岩甲兽）未被放行");
                GameActions.DrainStack(core);
                Require(errors, tank.IsAlive && tank.GetLife() == 3 && general.IsAlive && general.GetLife() == 3,
                    $"S6 闸门线: 换血演示不符预期（岩甲兽 {(tank.IsAlive ? tank.GetLife().ToString() : "亡")}、主将 {(general.IsAlive ? general.GetLife().ToString() : "亡")}——应双方存活各 3）");
                Require(errors, TutorialGuide.StepIndex == 1 && TutorialGuide.Current?.type == "end",
                    $"S6 闸门线: 引导①完成后应推进到 end 步骤（实际 StepIndex={TutorialGuide.StepIndex}）");

                // ---- 引导②（end）期间：攻击全拒、结束回合放行 ----
                Require(errors, !GameActions.DeclareAttack(core, core.Player1, reserve, core.Player2),
                    "S6 闸门线: end 步骤期间的攻击未被拒绝（应强制结束回合）");
                Require(errors, GameActions.EndTurn(core, core.Player1),
                    "S6 闸门线: end 步骤的结束回合未被放行");
                Require(errors, TutorialGuide.StepIndex == 2 && TutorialGuide.Current?.type == "guard",
                    $"S6 闸门线: end 步骤完成后应推进到守卫步骤（实际 StepIndex={TutorialGuide.StepIndex}）");

                // ---- 驱动机器人 R1：狼打已横置主将（守卫窗口置空自动放行）、巨兽打脸被卒拦截 ----
                prevResponder = ResponseWindowService.AiResponder;
                ResponseWindowService.AiResponder = (c, holder, options) =>
                    holder == c.Player2 ? ResponseWindowService.PassOption : PickGuard(c, options,
                        new Dictionary<string, string[]> { [LBehemoth] = new[] { LPawn } });
                core.TurnEngine.CheckPhaseTransition(); // End→Standby 折返（编辑器无帧泵）→ 机器人回合
                ai2.TakeTurn(ctrl);
                Require(errors, general.IsAlive && general.GetLife() == 1 && general.IsTapped(),
                    $"S6 闸门线: 横置主将应被狼单向打 2 点至 1（实际 {(general.IsAlive ? general.GetLife().ToString() : "亡")}）");
                Require(errors, !pawn.IsAlive,
                    "S6 闸门线: 引导守卫者卒应吸收巨兽 6 点阵亡（守卫单向受伤演示）");
                Require(errors, !TutorialGuide.Active,
                    "S6 闸门线: 三步引导应全部完成退场（自由段开始）");
                Debug.Log("[教学验证] S6 闸门线: 三步闸全生效，换血/单向受伤×2 演示与引导退场断言通过 ✔");
            }
            catch (Exception ex)
            {
                errors.Add($"S6 闸门线异常：{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                ResponseWindowService.AiResponder = prevResponder;
                TutorialGuide.End();
                ctrl?.Shutdown();
            }
        }

        private static Card FindOnBoard(GameCore core, Player owner, string cardId)
            => (core.ZoneManager.GetCards(owner, Zone.Battlefield) ?? new List<Card>())
                .FirstOrDefault(c => c.ID == cardId);

        // ======================================== S7 第二课·资源流转（归土破壁，2026-10-06） ========================================

        private const string Lesson2Id = "lunzhuan";
        private const string L2Boss = "C_7E5A2001";      // 教学生物11·巨壁 30/30
        private const string L2White = "C_7E5A2002";     // 白源（LandTrait 产白）
        private const string L2Black = "C_7E5A2003";     // 黑源
        private const string L2RedSeed = "C_7E5A2004";   // 红苗
        private const string L2BlueSeed = "C_7E5A2005";  // 蓝苗
        private const string L2GreenSeed = "C_7E5A2006"; // 绿苗
        private const string L2Champion = "C_7E5A2007";  // 三色斗士 6/6
        private const string L2GraySeed = "C_7E5A2008";  // 灰苗
        private const string L2Scorch = "C_7E5A2009";    // 灼烧（红2 直伤4）
        private const string L2Purify = "C_7E5A200A";    // 净化（灰1）

        /// <summary>
        /// 第二课四线：闸门线（资源步骤偏差拒绝+heroLock 课规）；解线（引导后轮转攒红→单回合穿再生斩巨壁→
        /// 解锁收脸）；贪脸线（直伤全打脸——红经济撑不满 30 且攻击被锁，有限回合内无法获胜）；
        /// 净化陷阱线（中段对巨壁净化→长档紊乱被清→下回合巨壁 30+ 打脸即死）。
        /// 首轮以宽松断言+逐回合追踪跑通，再按证据收紧数值。
        /// </summary>
        private static void RunLesson2Checks(List<string> errors)
        {
            var tut = TutorialLibrary.Get(Lesson2Id);
            if (tut?.scenario == null || tut.guide == null || tut.guide.Count == 0)
            {
                errors.Add($"S7: TutorialConfig.json 无 {Lesson2Id} 条目或缺 scenario/guide");
                return;
            }

            // ---- boss 费用推导核验（不走特例：空费用装载期推导回填，此处断言非零并打印供调参）----
            var bossData = CardCatalog.GetById(L2Boss);
            if (bossData == null)
            {
                errors.Add("S7: 巨壁卡不在卡池（Cards.json/Effects.json 装载失败？看 Console 警告）");
                return;
            }
            var derived = CardCostService.DeriveSuggestedCost(bossData);
            Debug.Log($"[教学验证] S7 巨壁费用：推导=[{string.Join(",", Enumerable.Range(0, 6).Select(i => derived[(ManaType)i]))}] " +
                      $"声明=[{string.Join(",", Enumerable.Range(0, 6).Select(i => bossData.Cost[(ManaType)i]))}]（玩家同样可拼出——费用一致不走特例）");
            if (bossData.Cost == null || bossData.Cost.IsZero)
                errors.Add("S7: 巨壁声明费用为空——EnsureCost 未回填（装载链断？）");

            // ---- 闸门线：资源步骤偏差拒绝 + heroLock + 紊乱拦截 ----
            RunLesson2GateChecks(tut, errors);

            // ---- 引导完成线：按 20 步引导随抽牌节奏走完（生物横置入场——产色排在其入场后的下一回合）----
            var guided = RunLessonLine(tut, new TutorialConfig
            {
                id = "lunzhuan-guided",
                aiScript = new List<TutorialTurnScript>
                {
                    Turn(1, A("element", card: L2RedSeed), A("tapland", card: L2RedSeed, target: "Red")),
                    Turn(2, A("element", card: L2GraySeed), A("tapland", card: L2GraySeed, target: "Gray")),
                    Turn(3, A("play", card: L2White)),
                    Turn(4, A("tapcreature", card: L2White), A("play", card: L2Black)),
                    Turn(5, A("tapcreature", card: L2Black), A("play", card: L2Champion)),
                    Turn(6, A("element", card: L2BlueSeed), A("tapland", card: L2BlueSeed, target: "Blue")),
                    Turn(7, A("element", card: L2GreenSeed), A("tapland", card: L2GreenSeed, target: "Green")),
                },
            }, new Dictionary<string, string[]>(), errors, "引导完成线", traceId: L2Boss, requireFinish: false);
            if (guided != null)
            {
                Require(errors, guided.MissedStepIndices.Count == 0,
                    $"S7 引导完成线: 引导步骤有漏做（{string.Join(",", guided.MissedStepIndices)}）——抽牌节奏与步骤不匹配");
                errors.AddRange(guided.Failures);
                Require(errors, guided.Failures.Count == 0,
                    $"S7 引导完成线: {guided.Failures.Count} 项引导动作未落地（费用/目标/时序配置有误）");
                Require(errors, guided.GuideCompleted,
                    "S7 引导完成线: 引导应全部完成并退场（课规仍生效）");
                Require(errors, guided.PlayerLife == 30,
                    $"S7 引导完成线: 引导期间玩家生命 {guided.PlayerLife} != 30（纳川免疲劳未生效，或引导期受到了伤害）");
                Debug.Log("[教学验证] S7 引导完成线: 20 步全数按引导完成（P1 生命 30=无疲劳无伤）");
            }
        }

        private static Card FindAnywhere(string cardId)
        {
            var core = GameCore.Instance;
            foreach (var p in new[] { core?.Player1, core?.Player2 })
            {
                if (p == null) continue;
                var hit = FindOnBoard(core, p, cardId);
                if (hit != null) return hit;
            }
            return null;
        }

        /// <summary>第二课闸门线：element/tap/end 步骤的偏差拒绝与推进；
        /// 末日降临 15 层长档紊乱拦截其打脸（引导期不痛不痒的引擎面）。</summary>
        private static void RunLesson2GateChecks(TutorialConfig tut, List<string> errors)
        {
            var core = GameCore.Instance;
            BattleController ctrl = null;
            try
            {
                var deck1 = TutorialLibrary.BuildDeck(TutorialScenario.DeckIdsOf(tut.scenario.player));
                var deck2 = TutorialLibrary.BuildDeck(TutorialScenario.DeckIdsOf(tut.scenario.ai));
                ctrl = new BattleController();
                ctrl.StartNewGame(deck1, deck2, new ScriptedAi(tut), rngSeed: tut.rngSeed, scenario: tut.scenario);
                core.Player1.IsAI = true;
                core.Player2.IsAI = true;
                TutorialGuide.Begin(tut);

                var hand = core.ZoneManager.GetCards(core.Player1, Zone.Hand) ?? new List<Card>();
                var redSeed = hand.FirstOrDefault(c => c.ID == L2RedSeed);
                var guitool = hand.FirstOrDefault(c => c.ID == "C_7E5A200B");
                var boss = FindOnBoard(core, core.Player2, L2Boss);
                if (redSeed == null || guitool == null || boss == null)
                {
                    errors.Add("S7 闸门线: 场面缺件（红苗/归土手牌/末日降临）");
                    return;
                }

                // 幻卡效果核验：末日降临携带 15 层长档紊乱（引擎拦打脸=引导期不痛不痒）
                Require(errors, boss.GetCounterCount(KeywordRules.SustainedRushSicknessCounter) == 15,
                    $"S7 闸门线: 末日降临长档紊乱层数 {boss.GetCounterCount(KeywordRules.SustainedRushSicknessCounter)} != 15");
                Require(errors, !GameActions.DeclareAttack(core, core.Player2, boss, core.Player1),
                    "S7 闸门线: 长档紊乱持有期间末日降临打脸未被引擎拒绝");

                // element 步骤：放错卡（归土）/出牌/结束回合全拒；引导放地（红苗）放行并推进
                Require(errors, !GameActions.AddToElementPool(core, core.Player1, guitool),
                    "S7 闸门线: element 步骤期间放错卡（归土）未被拒绝");
                Require(errors, !GameActions.PlayCard(core, core.Player1, guitool),
                    "S7 闸门线: element 步骤期间出牌未被拒绝");
                Require(errors, !GameActions.EndTurn(core, core.Player1),
                    "S7 闸门线: element 步骤未完成时结束回合未被钉住");
                Require(errors, GameActions.AddToElementPool(core, core.Player1, redSeed),
                    "S7 闸门线: 引导放地（红苗）未被放行");
                Require(errors, TutorialGuide.StepIndex == 1,
                    $"S7 闸门线: element 步骤完成后未推进（StepIndex={TutorialGuide.StepIndex}）");

                // tap 步骤：结束回合仍被钉；引导产色（红苗地→红）放行并推进到 end 步骤
                Require(errors, !GameActions.EndTurn(core, core.Player1),
                    "S7 闸门线: tap 步骤未完成时结束回合未被钉住");
                var redLand = core.ElementPool.GetPooledCards(core.Player1)
                    .FirstOrDefault(pc => pc.SourceCard != null && pc.SourceCard.ID == L2RedSeed);
                Require(errors, redLand != null && GameActions.GainElementFromToken(core, core.Player1, redLand, ManaType.Red),
                    "S7 闸门线: 引导产色（红苗→红）未被放行");
                Require(errors, TutorialGuide.StepIndex == 2 && TutorialGuide.Current?.type == "end",
                    $"S7 闸门线: tap 步骤完成后应推进到 end 步骤（StepIndex={TutorialGuide.StepIndex}）");
                Require(errors, GameActions.EndTurn(core, core.Player1),
                    "S7 闸门线: end 步骤的结束回合未被放行");
                Debug.Log("[教学验证] S7 闸门线: 资源步骤闸/紊乱 15 层拦截 断言通过 ✔");
            }
            catch (Exception ex)
            {
                errors.Add($"S7 闸门线异常：{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                TutorialGuide.End();
                ctrl?.Shutdown();
            }
        }

        /// <summary>跑一条教学课线：P1 由 playerScript 驱动（ScriptedAi 驱动当前回合玩家，玩家侧同理），
        /// 守卫应答=脚本化（P2 恒让过=教学机器人；P1 按 attackerId→守卫者顺位表取首个可用者，
        /// 全不可用/无条目即放行；plan=null=最优守卫启发）。traceId 非空=逐玩家回合打印该实体状态（调参伙眼）。
        /// 返回 null=局内异常已记入 errors。</summary>
        private static LessonResult RunLessonLine(TutorialConfig lesson, TutorialConfig playerScript,
            Dictionary<string, string[]> guardPlan, List<string> errors, string label, string traceId = null,
            bool requireFinish = true)
        {
            var result = new LessonResult();
            var core = GameCore.Instance;
            BattleController ctrl = null;
            Func<GameCore, Player, List<ResponseOption>, ResponseOption> prevResponder = null;
            Action<GameOverEvent> onOver = null;
            bool gameOver = false;
            GameOverEvent over = null;
            Player p1 = null;
            try
            {
                onOver = e => { gameOver = true; over = e; };
                EventManager.Instance.Subscribe<GameOverEvent>(onOver);
                prevResponder = ResponseWindowService.AiResponder;
                ResponseWindowService.AiResponder = (c, holder, options) =>
                    holder == c.Player2 ? ResponseWindowService.PassOption : PickGuard(c, options, guardPlan);

                var deck1 = TutorialLibrary.BuildDeck(TutorialScenario.DeckIdsOf(lesson.scenario.player));
                var deck2 = TutorialLibrary.BuildDeck(TutorialScenario.DeckIdsOf(lesson.scenario.ai));
                ctrl = new BattleController();
                var ai2 = new ScriptedAi(lesson);
                var ai1 = new ScriptedAi(playerScript);
                ctrl.StartNewGame(deck1, deck2, ai2, rngSeed: lesson.rngSeed, scenario: lesson.scenario);
                if (lesson.guide != null && lesson.guide.Count > 0)
                    TutorialGuide.Begin(lesson); // 引导闸（真机由 BattleScreen 装；验证器无屏幕自行装）
                p1 = core.Player1;
                core.Player1.IsAI = true; // 双 AI 全自动（无人类弹窗；守卫走上方脚本应答器）
                core.Player2.IsAI = true;

                for (int turn = 0; turn < 24 && !gameOver; turn++)
                {
                    if (core.TurnEngine.TurnPlayer == core.Player1)
                    {
                        ai1.TakeTurn(ctrl);
                        foreach (var (action, ok) in ai1.LastTurnReport)
                            if (!ok)
                                result.Failures.Add($"S6 {label}: 玩家回合动作 {action.type} {action.card ?? action.attacker} 未落地");
                    }
                    else
                    {
                        ai2.TakeTurn(ctrl); // 机器人剧本（攻击者阵亡后的动作失败属正常战况，不计）
                    }
                    if (!gameOver)
                        core.TurnEngine.CheckPhaseTransition();
                    if (traceId != null && core.TurnEngine.TurnPlayer == core.Player1 && !gameOver)
                    {
                        var traceCard = FindOnBoard(core, core.Player2, traceId);
                        var bank = core.ElementPool.GetPool(core.Player1);
                        var bankText = string.Join("/", Enumerable.Range(0, 6).Select(i => bank.AvailableMana[(ManaType)i]));
                        Debug.Log($"[教学验证] S7 追踪 回合{core.TurnEngine.TurnNumber}: boss=" +
                                  (traceCard == null ? "已亡" : $"{traceCard.GetPower()}/{traceCard.GetLife()}{(traceCard.IsTapped() ? "T" : "")}") +
                                  $"、P2生命{core.Player2.Life}、P1生命{core.Player1.Life}、bank[{bankText}]、" +
                                  $"墓地{(core.ZoneManager.GetCards(core.Player1, Zone.Graveyard) ?? new List<Card>()).Count}张");
                    }
                }

                result.WinnerIsPlayer = over?.Winner == p1;
                result.PlayerLife = p1?.Life ?? -1;
                result.TotalTurns = over?.TotalTurns ?? -1;
                result.MissedStepIndices = Enumerable.Range(0, TutorialGuide.Steps.Count)
                    .Where(i => TutorialGuide.StepMissed(i)).ToList();
                // 引导终态须在 finally 的 End() 拆除前捕获（End 会把 StepIndex 归 -1）
                result.GuideCompleted = !TutorialGuide.Active
                                        && TutorialGuide.StepIndex >= TutorialGuide.Steps.Count;
                if (over == null && requireFinish)
                    errors.Add($"S6 {label}: 24 回合内未终局（教学课线应短促分胜负）");
                Debug.Log($"[教学验证] S6 {label}: 终局={(over == null ? "未终局" : result.WinnerIsPlayer ? "玩家胜" : "机器人胜")} " +
                          $"回合 {result.TotalTurns}、玩家终局生命 {result.PlayerLife}、引导漏做 [{string.Join(",", result.MissedStepIndices)}]、" +
                          $"玩家动作失败 {result.Failures.Count} 项");
                return result;
            }
            catch (Exception ex)
            {
                errors.Add($"S6 {label} 异常：{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
            finally
            {
                if (onOver != null) EventManager.Instance.Unsubscribe<GameOverEvent>(onOver);
                ResponseWindowService.AiResponder = prevResponder;
                TutorialGuide.End();
                ctrl?.Shutdown();
            }
        }

        /// <summary>脚本化守卫决策：候选全为同一攻击（首个栈对象）的守卫项；按 attackerId 查守卫者顺位表，
        /// 取顺位中首个仍可守卫（在候选内）者——全不可用/无条目=放行（承受该次攻击）；
        /// plan=null=最优守卫启发（能扛住的挑血最厚，否则任选垫刀）。</summary>
        private static ResponseOption PickGuard(GameCore core, List<ResponseOption> options, Dictionary<string, string[]> plan)
        {
            var guards = options.Where(o => o.Kind == ResponseOption.ResponseKind.GuardAbility && o.SourceCard != null).ToList();
            if (guards.Count == 0) return ResponseWindowService.PassOption;
            var attackerId = (guards[0].AttackInstance?.Source as Card)?.ID;

            if (plan != null)
            {
                if (attackerId != null && plan.TryGetValue(attackerId, out var order))
                {
                    foreach (var guarderId in order ?? Array.Empty<string>())
                    {
                        var chosen = guards.FirstOrDefault(g => g.SourceCard.ID == guarderId);
                        if (chosen != null) return chosen;
                    }
                }
                return ResponseWindowService.PassOption;
            }

            var incoming = core.LayerEngine != null && guards[0].AttackInstance?.Source != null
                ? core.LayerEngine.CalculatePower(guards[0].AttackInstance.Source) : 0;
            ResponseOption best = null, any = null;
            foreach (var g in guards)
            {
                if (any == null) any = g;
                if (g.SourceCard.GetLife() > incoming && (best == null || g.SourceCard.GetLife() > best.SourceCard.GetLife()))
                    best = g;
            }
            return best ?? any;
        }

        private static TutorialAction A(string type, string card = null, string attacker = null, string target = null,
            string targets = null)
            => new TutorialAction { type = type, card = card, attacker = attacker, target = target, targets = targets };

        private static TutorialTurnScript Turn(int turn, params TutorialAction[] actions)
            => new TutorialTurnScript { turn = turn, actions = actions.ToList() };

        private sealed class LessonResult
        {
            public bool WinnerIsPlayer;
            public int PlayerLife;
            public int TotalTurns;
            public bool GuideCompleted;
            public List<int> MissedStepIndices = new List<int>();
            public readonly List<string> Failures = new List<string>();
        }

        // ======================================== S8 第三课·创作管线走查 + 最后一课镜像局（2026-10-06） ========================================

        /// <summary>第三课逻辑验证（内存假源注入，不碰真实 StreamingAssets）：
        /// 走查三步差集检测（新建效果→含新效果的卡→含新卡的卡组；卡内新建效果单轮补记两步）；
        /// 最后一课镜像局入口载荷+胜利落档；跳过路径默认卡组缺失回落随机镜像；真实镜像局开局冒烟。</summary>
        private static void RunCreationLessonChecks(List<string> errors)
        {
            var paths = new List<string>();
            var prevMirrorDeck = BattleEntry.MirrorDeck;
            Action completedHandler = null;
            try
            {
                // ---- 假源基线：真实卡池前 20 张为基线卡、第 21 张当「新卡」（id 真实——BuildDeck 可解析，
                //      挑战卡组才能组出 ≥起手张数）；效果假 id 走同样差集口径 ----
                var catalog = CardCatalog.LoadAll();
                var realIds = catalog.Select(c => c.ID).ToList();
                if (realIds.Count < 50)
                {
                    errors.Add($"S8: 卡池不足 50 张（{realIds.Count}）——假源构造与镜像冒烟无法进行");
                    return;
                }
                var baselineCards = realIds.Take(20).ToList();
                var newCardId = realIds[20];
                var deckIds = realIds.Skip(20).Take(30).ToList(); // 挑战卡组 30 张（含新卡）

                var fakeEffects = new List<string> { "E_BASE1", "E_BASE2" };
                var fakeCards = baselineCards.Select(id => (id, new List<string>())).ToList();
                var fakeDecks = new List<DeckData>();
                TutorialCreationFlow.UseVerificationSources(
                    () => fakeEffects.ToList(),
                    () => fakeCards.Select(t => (t.Item1, t.Item2)).ToList(),
                    () => fakeDecks.ToList());

                // ---- ① 开课：课程前置（第一/二课已完成——ShouldShowFinalChallenge 判据=三课全完成）
                //      + 基线快照 + 进行中占位 ----
                var mgr = TutorialProgressManager.CreateForVerification(TempProgressPath(paths));
                mgr.MarkTutorialCompleted(LessonId);
                mgr.MarkTutorialCompleted(Lesson2Id);
                TutorialCreationFlow.BeginLesson(mgr);
                Require(errors, mgr.Creation != null && TutorialCreationFlow.CurrentStep(mgr) == 0,
                    "S8: 开课后应有走查状态且步=0");
                Require(errors, mgr.Creation.baselineEffects.Count == 2 && mgr.Creation.baselineCards.Count == 20,
                    "S8: 开课基线应=快照当时的假源");
                Require(errors, mgr.CurrentTutorial == TutorialProgressManager.CreationLessonId,
                    "S8: 开课应占进行中标记（chuangzuo）");

                // ---- ② 步①效果：无新效果不推进；新效果推进并记档 ----
                TutorialCreationFlow.NotifyEffectsChanged(mgr);
                Require(errors, TutorialCreationFlow.CurrentStep(mgr) == 0, "S8: 无新效果时不应推进步①");
                fakeEffects.Add("E_NEW1");
                TutorialCreationFlow.NotifyEffectsChanged(mgr);
                Require(errors, TutorialCreationFlow.CurrentStep(mgr) == 1 && mgr.Creation.effectId == "E_NEW1",
                    "S8: 新效果应推进步①并记档效果 id");

                // ---- ③ 步②卡：不含新效果的新卡不推进；含新效果的新卡推进 ----
                fakeCards.Add((realIds[21], new List<string>())); // 基线外新卡但无新效果
                TutorialCreationFlow.NotifyCardsChanged(mgr);
                Require(errors, TutorialCreationFlow.CurrentStep(mgr) == 1, "S8: 不含新效果的卡不应推进步②");
                fakeCards.Add((newCardId, new List<string> { "E_NEW1" }));
                TutorialCreationFlow.NotifyCardsChanged(mgr);
                Require(errors, TutorialCreationFlow.CurrentStep(mgr) == 2 && mgr.Creation.cardId == newCardId,
                    "S8: 含新效果的新卡应推进步②并记档卡 id");

                // ---- ④ 步③卡组：不含新卡的卡组不推进；含新卡推进=本课完成 ----
                int completedEvent = 0;
                completedHandler = () => completedEvent++;
                TutorialCreationFlow.OnLessonCompleted += completedHandler;
                fakeDecks.Add(new DeckData("别的卡组") { cardIds = realIds.Take(10).ToList() });
                TutorialCreationFlow.NotifyDecksChanged(mgr);
                Require(errors, TutorialCreationFlow.CurrentStep(mgr) == 2, "S8: 不含新卡的卡组不应推进步③");
                fakeDecks.Add(new DeckData("我的卡组") { cardIds = deckIds });
                TutorialCreationFlow.NotifyDecksChanged(mgr);
                Require(errors, TutorialCreationFlow.CurrentStep(mgr) == 3 && mgr.Creation.deckName == "我的卡组",
                    "S8: 含新卡的卡组应推进步③并记档卡组名");
                Require(errors, mgr.CompletedTutorials.Contains(TutorialProgressManager.CreationLessonId) && completedEvent == 1,
                    "S8: 三步走完应落档本课完成且完成事件恰发一次");
                TutorialCreationFlow.OnLessonCompleted -= completedHandler;

                // ---- ⑤ 最后一课镜像局：入口载荷 + 胜利落档 ----
                Require(errors, TutorialCreationFlow.IsChallengeReady(mgr) && mgr.ShouldShowFinalChallenge,
                    "S8: 走查完成后镜像挑战应就绪且最后一课按钮可见");
                Require(errors, TutorialCreationFlow.StartFinalChallenge(mgr), "S8: StartFinalChallenge 应成功");
                Require(errors, BattleEntry.Mode == BattleMode.LocalAI
                                && BattleEntry.MirrorDeck != null && BattleEntry.MirrorDeck.Count == 30
                                && BattleEntry.MirrorDeck.Any(c => c.ID == newCardId)
                                && TutorialCreationFlow.FinalChallengeActive,
                    "S8: 最后一课应写镜像局载荷（LocalAI+30 卡镜像含新卡+会话标记）");
                BattleEntry.MirrorDeck = null; // 消费口径模拟（StartLocalFan 开局即置空）
                TutorialCreationFlow.ReportFinalChallengeResult(true, mgr);
                Require(errors, mgr.FinalChallengeDone && mgr.IsTutorialDone && !mgr.ShouldShowFinalChallenge
                                && !TutorialCreationFlow.FinalChallengeActive,
                    "S8: 镜像局获胜应=教学整体完成（门禁解除、会话标记消费）");

                // ---- ⑥ 卡内新建效果单轮补记两步（从卡界面「+新建效果」再存卡的创作路径）----
                //      清空卡组假源——隔离 ④ 存下的卡组（含后续新卡，会把步③一并达成）
                fakeDecks.Clear();
                var mgr2 = TutorialProgressManager.CreateForVerification(TempProgressPath(paths));
                TutorialCreationFlow.BeginLesson(mgr2);
                fakeEffects.Add("E_NEW2");
                fakeCards.Add((realIds[22], new List<string> { "E_NEW2" }));
                TutorialCreationFlow.NotifyCardsChanged(mgr2);
                Require(errors, TutorialCreationFlow.CurrentStep(mgr2) == 2 && mgr2.Creation.effectId == "E_NEW2",
                    "S8: 卡内新建效果+存卡应单轮补记步①②");

                // ---- ⑦ 跳过路径默认卡组：缺失回落随机镜像（30 张非教学卡）；在库直接采用 ----
                var fallback = TutorialCreationFlow.ResolveDefaultMirrorDeck();
                Require(errors, fallback != null && fallback.Count == BattleController.RandomDeckSize
                                && fallback.All(c => !CardCatalog.IsTeachingCard(c)),
                    "S8: 默认卡组缺失应回落随机镜像（30 张非教学卡）");
                fakeDecks.Add(new DeckData(TutorialCreationFlow.DefaultDeckName) { cardIds = deckIds });
                var byDefault = TutorialCreationFlow.ResolveDefaultMirrorDeck();
                Require(errors, byDefault != null && byDefault.Select(c => c.ID).SequenceEqual(deckIds),
                    "S8: 默认卡组在库时应直接采用（不回落随机）");

                // ---- ⑧ 真实镜像局冒烟：同一卡组双侧开局——构成一致断言取「手牌+牌库连接序」
                //      （InitGame 自动开 P1 第一回合：P1 已抽第 7 张、P2 仍 6 张起手，绝对序必差一位）----
                var tut = TutorialLibrary.Get(TutorialId);
                var deck = TutorialLibrary.BuildDeck(tut.playerDeck);
                var ctrl = new BattleController();
                ctrl.StartNewGame(deck, deck, rngSeed: 20261006, lockDeckOrder: true);
                var core = ctrl.Core;
                var seq1 = ZoneIds(core, core.Player1, Zone.Hand).Concat(ZoneIds(core, core.Player1, Zone.Deck)).ToList();
                var seq2 = ZoneIds(core, core.Player2, Zone.Hand).Concat(ZoneIds(core, core.Player2, Zone.Deck)).ToList();
                SeqEqual(errors, seq1, seq2, "S8: 镜像局双侧构成（手牌+牌库连接序）");
                Require(errors, seq1.Count == tut.playerDeck.Count,
                    $"S8: 镜像局连接序长度 {seq1.Count} != 卡组 {tut.playerDeck.Count}");
                Require(errors, core.Player2.IsAI, "S8: 镜像局 P2 应为 AI");

                if (errors.All(e => !e.StartsWith("S8")))
                    Debug.Log("[教学验证] S8: 第三课创作管线——走查三步/卡内新建单轮补记/最后一课镜像局/默认卡组回落/镜像冒烟 全部通过");
            }
            catch (Exception ex)
            {
                errors.Add($"S8 异常：{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                if (completedHandler != null) TutorialCreationFlow.OnLessonCompleted -= completedHandler;
                TutorialCreationFlow.UseVerificationSources(null, null, null); // 复位真实源
                TutorialCreationFlow.ClearSession();
                BattleEntry.MirrorDeck = prevMirrorDeck;
                foreach (var path in paths)
                    if (File.Exists(path)) File.Delete(path);
            }
        }

        // ======================================== 断言小件 ========================================

        private static void Require(List<string> errors, bool cond, string message)
        {
            if (!cond) errors.Add(message);
        }

        private static void SeqEqual(List<string> errors, List<string> actual, List<string> expected, string label)
        {
            var a = actual ?? new List<string>();
            var e = expected ?? new List<string>();
            if (a.SequenceEqual(e)) return;
            errors.Add($"{label}序不一致（实际 [{string.Join(",", a)}] 期望 [{string.Join(",", e)}]）");
        }

        private static List<string> ZoneIds(GameCore core, Player p, Zone zone)
            => (core.ZoneManager.GetCards(p, zone) ?? new List<Card>()).Select(c => c.ID).ToList();

        private static void Report(List<string> errors)
        {
            if (errors.Count == 0)
                Debug.Log("[教学验证] PASS —— 配置装载 / 锁序确定性 / 剧本全量 / 整局胜负 / 场面直入 / 进度逻辑 / 第一课三线难度 / 第二课资源流转引导 / 第三课创作管线与最后一课镜像局 全部通过");
            else
            {
                Debug.LogError($"[教学验证] FAIL（{errors.Count} 项）：");
                foreach (var e in errors) Debug.LogError("  · " + e);
            }
        }

        /// <summary>起手+牌库全序快照（S2 两次初始化对比用）。</summary>
        private static string SnapshotZones(GameCore core)
            => JoinZones(core, core.Player1) + JoinZones(core, core.Player2);

        private static string JoinZones(GameCore core, Player p)
        {
            var hand = string.Join(",", (core.ZoneManager.GetCards(p, Zone.Hand) ?? new List<Card>()).Select(c => c.ID));
            var deck = string.Join(",", (core.ZoneManager.GetCards(p, Zone.Deck) ?? new List<Card>()).Select(c => c.ID));
            return $"[{hand}|{deck}]";
        }

        /// <summary>锁序断言（连接序不变式）：手牌+牌库连接=配置全序——已摸走的牌落在手牌序头，
        /// 不受 InitGame 内 P1 首回合自动抽牌影响。</summary>
        private static void CheckZoneAgainstConfig(List<string> errors, Player p, List<string> configDeck, string label)
        {
            var core = GameCore.Instance;
            var hand = (core.ZoneManager.GetCards(p, Zone.Hand) ?? new List<Card>()).Select(c => c.ID).ToList();
            var deck = (core.ZoneManager.GetCards(p, Zone.Deck) ?? new List<Card>()).Select(c => c.ID).ToList();
            var actual = hand.Concat(deck).ToList();
            if (!actual.SequenceEqual(configDeck))
                errors.Add($"S2: {label}手牌+牌库连接序与配置不一致（实际 [{string.Join(",", actual)}] 期望 [{string.Join(",", configDeck)}]）");
        }

        /// <summary>整局摸牌断言：实际摸到的每张（含起手）按序等于配置序列的前缀。</summary>
        private static void CompareDrawSequence(List<string> errors, List<string> actual, List<string> configDeck, string label)
        {
            if (actual.Count > configDeck.Count)
            {
                errors.Add($"{label}摸牌 {actual.Count} 张超过卡组 {configDeck.Count} 张（疲劳路径不在教学断言内）");
                return;
            }
            for (int i = 0; i < actual.Count; i++)
            {
                if (actual[i] != configDeck[i])
                {
                    errors.Add($"摸牌序 {label}第 {i + 1} 张与配置不符（实际 {actual[i]} 期望 {configDeck[i]}）");
                    return;
                }
            }
        }
    }
}
