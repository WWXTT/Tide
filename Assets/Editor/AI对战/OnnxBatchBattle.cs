using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using CardCore;
using CardCore.AI.NeuralEnv;

namespace CardCore.Editor.Tests
{
    /// <summary>
    /// 模型 vs SimpleAI 批量对局（2026-09-11）：训练侧 TideHeadlessDriver + 部署 ONNX 策略
    /// 直接对打——与训练 eval 同环境同口径（模型绿池 30 vs 脚本红池 30、随机座次）。
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

        [MenuItem("Tools/AI/模型vs脚本批量对局")]
        public static void Run()
        {
            var pool = AiBattleE2E.LoadStandardDeck();
            var green = LoadColorPool("Deck_Green.json", pool);
            var red = LoadColorPool("Deck_Red.json", pool);
            if (green.Count == 0 || red.Count == 0)
            {
                Debug.LogError("[批量对局] 三色卡组加载失败（Configs/TestDecks/Deck_Green|Red.json）");
                return;
            }

            // 身份链同口径：manifest（追加式）+ 全池登记
            string manifest = Path.Combine(Application.dataPath, "..", "tide_rl", "card_identity_manifest.json");
            int loaded = TideCardIndex.ConfigureManifest(manifest);
            Debug.Log($"[批量对局] manifest 载入 {loaded} 条（总 {TideCardIndex.Count}）");
            TideCardIndex.Register(pool);
            TideCardIndex.Register(green);
            TideCardIndex.Register(red);

            var rng = new System.Random(20260911);
            int wins = 0, losses = 0, unfinished = 0;
            int turnSum = 0;
            using (var policy = new OnnxTidePolicy())
            {
                for (int g = 0; g < Games; g++)
                {
                    bool modelP1 = g % 2 == 0; // 换边：先后手各半
                    var driver = new TideHeadlessDriver();
                    try
                    {
                        var res = driver.Reset(Sample(green, rng), Sample(red, rng), modelP1);
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

                        if (!res.Done) { unfinished++; continue; }
                        turnSum += res.Turn;
                        var modelSeat = modelP1 ? GameCore.Instance.Player1 : GameCore.Instance.Player2;
                        if (ReferenceEquals(res.Winner, modelSeat)) wins++;
                        else losses++;
                        if (g < 4 || res.Turn > 40)
                            Debug.Log($"[批量对局] 局{g + 1}：模型{(ReferenceEquals(res.Winner, modelSeat) ? "胜" : "负")}"
                                    + $"（{(modelP1 ? "先手" : "后手")}，{res.Reason}，{res.Turn} 回合）");
                    }
                    finally
                    {
                        driver.Dispose();
                    }
                }
            }

            Debug.Log($"[批量对局] ===== 模型 {wins} 胜 / {losses} 负 / {unfinished} 未完"
                    + $"（{Games} 局，均回合 {(float)turnSum / (Games - unfinished):F0}）——"
                    + (wins * 2 >= Games
                        ? "强度正常（≈训练 eval 口径）→ 编辑器对战窗口的喂入链路有 bug"
                        : "此处也弱 → 模型/部署 artifact 问题（导出或身份），与编辑器路径无关"));
        }

        private static List<CardData> LoadColorPool(string file, List<CardData> fallback)
        {
            var path = Path.Combine(Application.dataPath, "Configs", "TestDecks", file);
            return File.Exists(path)
                ? CardLoader.LoadCardsFromText(File.ReadAllText(path))
                : new List<CardData>(fallback);
        }

        /// <summary>洗牌抽前 30（镜像 TideHeadlessServer.SampleRandomDeck 口径）。</summary>
        private static List<CardData> Sample(List<CardData> source, System.Random rng)
        {
            var shuffled = new List<CardData>(source);
            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
            }
            return shuffled.GetRange(0, Math.Min(30, shuffled.Count));
        }
    }
}
