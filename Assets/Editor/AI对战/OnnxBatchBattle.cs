using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using CardCore;
using CardCore.AI;
using CardCore.AI.NeuralEnv;

namespace CardCore.Editor.Tests
{
    /// <summary>
    /// 模型 vs 脚本主题卡组批量对局（2026-09-21 主题口径）：训练侧 TideHeadlessDriver +
    /// 部署 ONNX 策略直接对打——与训练 eval 同环境同口径（模型 Cards.json 随机 30 vs 脚本
    /// 主题整组 SimpleAI+AutoMatch、随机座次、轮遍三主题）。
    /// 用途：分离「编辑器对战窗口路径的喂入 bug」与「模型/部署真实强度」——
    /// 此处胜率 ≈ 训练 eval（60-90%）而编辑器窗口稳输 → 差异在 AiBattleDriver/NeuralAI 链路；
    /// 此处也稳输 → 模型或部署 artifact 本身有问题（重查导出/manifest）。
    /// 首局前 40 个决策点输出动作追踪（类型/签名/估值），供行为对照。
    /// </summary>
    public static class OnnxBatchBattle
    {
        private const int Games = 20;
        private const int MaxSteps = 2000;
        private const int TraceDecisions = 40; // 首局决策追踪条数

        [MenuItem("Tools/AI/模型vs脚本主题批量对局")]
        public static void Run()
        {
            var pool = BattleDeckSources.AllCards();
            var themes = BattleDeckSources.ThemeDecks().Where(t => t.deck.Count > 0).ToList();
            if (pool.Count == 0 || themes.Count == 0)
            {
                Debug.LogError("[批量对局] 数据层缺失（Cards.json 卡池或主题卡组）——先运行 Tools/构建三色主题卡组");
                return;
            }

            // 身份链同口径：manifest（追加式）+ 全池登记（与 TideHeadlessServer.HandleReset 一致）
            string manifest = Path.Combine(Application.dataPath, "..", "tide_rl", "card_identity_manifest.json");
            int loaded = TideCardIndex.ConfigureManifest(manifest);
            Debug.Log($"[批量对局] manifest 载入 {loaded} 条（总 {TideCardIndex.Count}）");
            BattleDeckSources.RegisterIdentities();

            var rng = new System.Random(20260921);
            int wins = 0, losses = 0, unfinished = 0;
            int turnSum = 0;
            var themeStats = themes.ToDictionary(t => t.key, _ => (wins: 0, games: 0));
            using (var policy = new OnnxTidePolicy())
            {
                for (int g = 0; g < Games; g++)
                {
                    var theme = themes[g % themes.Count]; // 轮遍三主题（20 局 ≈ 7/7/6）
                    bool modelP1 = g % 2 == 0; // 换边：先后手各半
                    var driver = new TideHeadlessDriver();
                    try
                    {
                        var res = driver.Reset(
                            BattleDeckSources.SampleRandomDeck(pool, 30, rng), // 模型：Cards 随机 30
                            theme.deck,                                        // 脚本：主题整组
                            modelP1,
                            AiStrategy.AutoMatch(theme.deck));                 // 策略按主题自动匹配
                        policy.Reset();
                        int steps = 0;
                        while (!res.Done && steps++ < MaxSteps)
                        {
                            int idx = policy.Select(res.Obs, res.Legal);
                            if (idx < 0) break;
                            if (g == 0 && steps <= TraceDecisions)
                            {
                                var a = res.Legal.Actions[idx];
                                Debug.Log($"[追踪] 局1 t{res.Turn} 步{steps} 合法{res.Legal.Count} "
                                        + $"→ {a.Type} {a.Signature} V={policy.LastValue:F2}");
                            }
                            res = driver.Step(idx);
                        }

                        var tally = themeStats[theme.key];
                        tally.games++;
                        themeStats[theme.key] = tally;
                        if (!res.Done) { unfinished++; continue; }
                        turnSum += res.Turn;
                        var modelSeat = modelP1 ? GameCore.Instance.Player1 : GameCore.Instance.Player2;
                        if (ReferenceEquals(res.Winner, modelSeat))
                        {
                            wins++;
                            tally.wins++;
                            themeStats[theme.key] = tally;
                        }
                        else losses++;
                        if (g < 4 || res.Turn > 40)
                            Debug.Log($"[批量对局] 局{g + 1}（vs {theme.key}）：模型{(ReferenceEquals(res.Winner, modelSeat) ? "胜" : "负")}"
                                    + $"（{(modelP1 ? "先手" : "后手")}，{res.Reason}，{res.Turn} 回合）");
                    }
                    finally
                    {
                        driver.Dispose();
                    }
                }
            }

            var perTheme = string.Join("、", themeStats.Select(kv =>
                $"{kv.Key} {kv.Value.wins}/{kv.Value.games}"));
            Debug.Log($"[批量对局] ===== 模型 {wins} 胜 / {losses} 负 / {unfinished} 未完"
                    + $"（{Games} 局，均回合 {(float)turnSum / Math.Max(1, Games - unfinished):F0}；分主题 {perTheme}）——"
                    + (wins * 2 >= Games
                        ? "强度正常（≈训练 eval 口径）→ 编辑器对战窗口的喂入链路有 bug"
                        : "此处也弱 → 模型/部署 artifact 问题（导出或身份），与编辑器路径无关"));
        }
    }
}
