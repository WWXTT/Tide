using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using SynergyUI;
using UnityEditor;
using UnityEngine;

namespace CardCore.Editor.Tests
{
    /// <summary>
    /// 教学固定局验证器（2026-10-02 教学模式）：
    /// S1 配置装载——TutorialConfig.json 可读、basics 条目卡组/剧本齐备且卡 id 全在卡池；
    /// S2 锁序确定性——同配置两次 InitGame(lockDeckOrder+钉种子)状态一致，且双方「手牌+牌库」连接序
    ///    与配置全序逐张一致（InitGame 内 StartNewTurn(P1) 已自动抽掉玩家第 1 回合的牌，连接序不受影响
    ///    ——这是"每回合摸到的卡固定"的机制断言）；
    /// S3a 剧本全量——P1 纯让过（只 EndTurn），机器人 5 个剧本回合全部动作必须落地
    ///    （含 attack 硬断言——无人对抗时攻击者必然存活）；同时断言整局摸牌序列与配置一致；
    /// S3b 整局胜负——P1=SimpleAI（通用策略扮演教学玩家，编辑器无真人），断言自然走到 GameOver 且玩家获胜
    ///    （机器人 R5 后无剧本被动、响应窗口全让过；attack 容忍失败——P1 清场优先，机器人单位可能先阵亡）。
    /// 运行：Tools/教学/教学固定局验证。风格对齐 Editor/验证/AI对战。
    /// </summary>
    public static class TutorialScriptVerifier
    {
        private const string TutorialId = "basics";
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

        private static void Report(List<string> errors)
        {
            if (errors.Count == 0)
                Debug.Log("[教学验证] PASS —— 配置装载 / 锁序确定性 / 剧本全量 / 整局胜负 全部通过");
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
