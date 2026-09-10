using System;
using System.Collections.Generic;
using System.IO;
using CardCore;
using CardCore.AI.NeuralEnv;
using CardCore.Editor.Tests;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ONNX 策略验证菜单（训练完成后在编辑器内一键验证导出链路）：
///   1. 数值对拍——tide_policy_fixture.json 的输入喂 Sentis，与 JAX 参考输出比对
///      （同链路 Python 侧已用 onnxruntime 对拍过，此处验证的是 Sentis 后端的等价性）；
///   2. vs SimpleAI——镜像 TideHeadlessServer.HandleReset 的评估口径（模型三色随机 /
///      SimpleAI 恒红、随机座次、洗牌抽 30），胜率可与训练日志的 eval win_rate 直接对照。
/// 前置：tide_rl/export_onnx.py 已导出并复制 tide_policy.onnx + fixture 到 StreamingAssets。
/// </summary>
public static class OnnxPolicyMenu
{
    private const int MaxStepsPerGame = 2000; // 镜像 TideHeadlessServer.SelfTestMaxSteps
    private const int DeckSize = 30;

    private static string ManifestPath
        => Path.Combine(Application.dataPath, "..", "tide_rl", "card_identity_manifest.json");

    // ===================================================== 1. 数值对拍 =====================================================

    [MenuItem("Tools/AI/ONNX 策略/1. 数值对拍 (fixture)")]
    public static void RunFixtureParity()
    {
        string fixturePath = Path.Combine(Application.streamingAssetsPath, "tide_policy_fixture.json");
        if (!File.Exists(fixturePath))
        {
            Debug.LogError($"[OnnxPolicy] fixture 缺失: {fixturePath}\n先在 tide_rl 跑 export_onnx.py（默认会复制到 StreamingAssets）");
            return;
        }

        var fixture = JsonUtility.FromJson<PolicyFixture>(File.ReadAllText(fixturePath));
        if (fixture?.cases == null || fixture.cases.Count == 0)
        {
            Debug.LogError("[OnnxPolicy] fixture 解析失败或无用例");
            return;
        }

        // manifest 指纹对表：导出时的清单必须与当前 TideCardIndex 绑定的是同一份
        int loaded = TideCardIndex.ConfigureManifest(ManifestPath);
        Debug.Log(loaded >= 0
            ? $"[OnnxPolicy] 卡身份清单载入 {loaded} 条（{ManifestPath}）"
            : "[OnnxPolicy] ⚠ manifest 表指纹不符——模型 embedding 行可能串台，建议重导出");

        int failures = 0;
        using (var policy = new OnnxTidePolicy(OnnxTidePolicy.DefaultModelPath))
        {
            for (int ci = 0; ci < fixture.cases.Count; ci++)
            {
                var c = fixture.cases[ci];
                var outputs = policy.Step(c.rstate, c.cards_flat, c.global_flat, c.actions_flat);
                float dR = MaxAbsDiff(outputs.RstateNext, c.expect_rstate_flat);
                float dL = MaxAbsDiff(outputs.Logits, c.expect_logits);
                float dV = Math.Abs(outputs.Value - (c.expect_value.Length > 0 ? c.expect_value[0] : 0f));
                int argmax = Argmax(outputs.Logits);
                bool ok = dR <= fixture.tol && dL <= fixture.tol && dV <= fixture.tol && argmax == c.argmax;
                if (!ok) failures++;
                Debug.Log($"[对拍 {ci}] {(ok ? "✓" : "✗")} max|Δ| rstate={dR:E2} logits={dL:E2} value={dV:E2} " +
                          $"argmax={argmax}（期望 {c.argmax}）");
            }
        }

        if (failures == 0)
            Debug.Log($"[OnnxPolicy] ✅ 数值对拍全部通过（{fixture.cases.Count} 例，tol={fixture.tol}）——Sentis 后端与 JAX 等价");
        else
            Debug.LogError($"[OnnxPolicy] ❌ {failures}/{fixture.cases.Count} 例超差");
    }

    // ===================================================== 2. vs SimpleAI =====================================================

    [MenuItem("Tools/AI/ONNX 策略/2. vs SimpleAI 20 局")]
    public static void RunVersusSimpleAI()
    {
        RunVersusSimpleAI(20, 42);
    }

    /// <summary>N 局对打（口径镜像 TideHeadlessServer.HandleReset 的 simpleai 路径）。</summary>
    public static void RunVersusSimpleAI(int games, int seed)
    {
        // 卡池与身份登记（与训练服务器完全同源：标准池 + 三色池全部注册，manifest 追加式幂等）
        int loaded = TideCardIndex.ConfigureManifest(ManifestPath);
        Debug.Log($"[OnnxPolicy] 卡身份清单载入 {loaded} 条（{ManifestPath}）");
        var pool = AiBattleE2E.LoadStandardDeck();
        var colorPools = LoadColorPools();
        if (pool == null || pool.Count == 0)
        {
            Debug.LogError("[OnnxPolicy] 标准卡池加载失败（Configs/TestDecks/TestCreatureCards.json）");
            return;
        }
        TideCardIndex.Register(pool);
        foreach (var cp in colorPools) TideCardIndex.Register(cp);

        var rng = new System.Random(seed);
        var driver = new TideHeadlessDriver();
        int wins = 0, losses = 0, draws = 0, totalSteps = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            using (var policy = new OnnxTidePolicy(OnnxTidePolicy.DefaultModelPath))
            {
                for (int g = 0; g < games; g++)
                {
                    policy.Reset(); // 新对局 GRU 状态归零

                    // 模型 = 三色随机一色抽 30；SimpleAI 恒红色抽 30（缺池退回标准池）；随机座次
                    var modelPool = colorPools[rng.Next(colorPools.Length)];
                    if (modelPool.Count == 0) modelPool = pool;
                    var aiPool = colorPools[0].Count > 0 ? colorPools[0] : pool;
                    var result = driver.Reset(SampleDeck(rng, modelPool), SampleDeck(rng, aiPool), rng.Next(2) == 0);

                    int steps = 0;
                    while (!result.Done && steps < MaxStepsPerGame)
                    {
                        int idx = policy.Select(result.Obs, result.Legal);
                        result = driver.Step(idx);
                        steps++;
                    }
                    totalSteps += steps;

                    var core = GameCore.Instance;
                    if (result.Winner == null || steps >= MaxStepsPerGame)
                    {
                        draws++;
                        Debug.Log($"[对局 {g + 1}] 平局（步数 {steps}，seat={driver.ModelSeat}）");
                    }
                    else if (ReferenceEquals(result.Winner, driver.ModelSeat == 0 ? core.Player1 : core.Player2))
                    {
                        wins++;
                        Debug.Log($"[对局 {g + 1}] ✓ 胜（步数 {steps}，seat={driver.ModelSeat}）");
                    }
                    else
                    {
                        losses++;
                        Debug.Log($"[对局 {g + 1}] ✗ 负（步数 {steps}，seat={driver.ModelSeat}）");
                    }
                }
            }
        }
        finally
        {
            driver.Dispose();
        }

        sw.Stop();
        Debug.Log($"[OnnxPolicy] vs SimpleAI {games} 局：{wins}胜 {losses}负 {draws}平 " +
                  $"（胜率 {(float)wins / games:P1}，平均 {(float)totalSteps / games:F0} 步/局，" +
                  $"耗时 {sw.Elapsed.TotalSeconds:F1}s ≈ {sw.Elapsed.TotalMilliseconds / Math.Max(1, totalSteps):F2}ms/步）");
    }

    // ===================================================== 辅助 =====================================================

    [Serializable] private class PolicyFixture
    {
        public string manifest_sha256;
        public float tol;
        public int channels;
        public int rnn_channels;
        public int case_count;
        public List<PolicyFixtureCase> cases;
    }

    [Serializable] private class PolicyFixtureCase
    {
        public float[] rstate;
        public float[] cards_flat;
        public float[] global_flat;
        public float[] actions_flat;
        public float[] expect_rstate_flat;
        public float[] expect_logits;
        public float[] expect_value;
        public int argmax;
    }

    /// <summary>三色主题卡池（镜像 TideHeadlessServer.ColorPools：Deck_*.json）。</summary>
    private static List<CardData>[] LoadColorPools()
    {
        var files = new[] { "Deck_Red.json", "Deck_Blue.json", "Deck_Green.json" };
        var pools = new List<CardData>[files.Length];
        var dir = Path.Combine(Application.dataPath, "Configs", "TestDecks");
        for (int i = 0; i < files.Length; i++)
        {
            var path = Path.Combine(dir, files[i]);
            pools[i] = File.Exists(path)
                ? CardLoader.LoadCardsFromText(File.ReadAllText(path))
                : new List<CardData>();
        }
        return pools;
    }

    /// <summary>洗牌抽前 n（无放回，镜像 TideHeadlessServer.SampleRandomDeck）。</summary>
    private static List<CardData> SampleDeck(System.Random rng, List<CardData> source)
    {
        var shuffled = new List<CardData>(source);
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }
        return shuffled.GetRange(0, Math.Min(DeckSize, shuffled.Count));
    }

    private static float MaxAbsDiff(float[] a, float[] b)
    {
        if (a == null || b == null || a.Length != b.Length) return float.PositiveInfinity;
        float max = 0f;
        for (int i = 0; i < a.Length; i++)
        {
            float d = Math.Abs(a[i] - b[i]);
            if (d > max) max = d;
        }
        return max;
    }

    private static int Argmax(float[] v)
    {
        int best = 0;
        for (int i = 1; i < v.Length; i++)
            if (v[i] > v[best]) best = i;
        return best;
    }
}
