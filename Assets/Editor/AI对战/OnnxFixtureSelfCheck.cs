using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using CardCore.AI.NeuralEnv;

namespace CardCore.Editor.Tests
{
    /// <summary>
    /// 模型 fixture 自检（2026-09-11）：用编辑器实际加载的 ModelAsset 跑导出时的对拍样例
    /// （Resources/tide_policy_fixture.json，JAX 参考输出），区分两类故障：
    /// ① max|Δ| 远超容差 → 编辑器加载的是陈旧/错导入模型（onnx 更新后未重导入）→ 右键 Reimport；
    /// ② max|Δ| 在容差内但实战异常 → 模型正确，问题在喂入/状态链路（obs/rstate/manifest）。
    /// 同时比对 fixture 内嵌 manifest 指纹与当前 card_identity_manifest.json 的 sha256。
    /// </summary>
    public static class OnnxFixtureSelfCheck
    {
        [MenuItem("Tools/AI/模型fixture自检")]
        public static void Run()
        {
            var ta = UnityEngine.Resources.Load<TextAsset>("tide_policy_fixture");
            if (ta == null)
            {
                Debug.LogError("[Fixture自检] ✗ Resources 里找不到 tide_policy_fixture.json（先跑 export_onnx.py）");
                return;
            }
            var fx = JsonConvert.DeserializeObject<Fixture>(ta.text);

            // 身份侧：fixture 记录的 manifest sha vs 磁盘当前文件（编辑器加载的模型与清单是否同源）
            string manifestPath = Path.Combine(Application.dataPath, "..", "tide_rl", "card_identity_manifest.json");
            string diskSha = File.Exists(manifestPath)
                ? System.BitConverter.ToString(
                      System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(manifestPath)))
                    .Replace("-", "").ToLowerInvariant()
                : "(文件缺失)";
            bool manifestOk = diskSha == fx.manifest_sha256;
            Debug.Log($"[Fixture自检] manifest 指纹：fixture==磁盘 {manifestOk}（{diskSha[..16]}… vs {fx.manifest_sha256[..16]}…）");

            using (var policy = new OnnxTidePolicy())
            {
                float worst = 0f;
                int worstCase = -1, argmaxMiss = 0;
                for (int i = 0; i < fx.cases.Count; i++)
                {
                    var c = fx.cases[i];
                    var o = policy.Step(c.rstate.ToArray(), c.cards_flat.ToArray(),
                                        c.global_flat.ToArray(), c.actions_flat.ToArray());
                    float dLogits = MaxDelta(o.Logits, c.expect_logits);
                    float dRstate = MaxDelta(o.RstateNext, c.expect_rstate_flat);
                    float worstCaseDelta = Mathf.Max(dLogits, dRstate);
                    if (worstCaseDelta > worst) { worst = worstCaseDelta; worstCase = i; }

                    int am = ArgMax(o.Logits);
                    if (am != c.argmax) argmaxMiss++;
                    Debug.Log($"[Fixture自检] case{i}: max|Δlogits|={dLogits:E2} max|Δrstate|={dRstate:E2} "
                              + $"value={o.Value:F3}(ref {c.expect_value[0]:F3}) argmax={am}(ref {c.argmax})");
                }

                if (worst > fx.tol)
                    Debug.LogError($"[Fixture自检] ✗ 模型输出偏离参考（case{worstCase} max|Δ|={worst:E2} > 容差 {fx.tol}）——"
                                 + "编辑器加载的是陈旧/错导入模型：在 Project 窗口右键 Assets/Resources/tide_policy.onnx → Reimport 后重试");
                else
                    Debug.Log($"[Fixture自检] ✓ 模型输出与导出参考一致（max|Δ|={worst:E2} ≤ {fx.tol}，argmax 偏差 {argmaxMiss}/{fx.cases.Count}）——"
                            + "模型本体正确；实战异常请查喂入/状态链路（obs 视角、rstate 复位、manifest 登记顺序）");
            }
        }

        private static float MaxDelta(float[] a, List<float> b)
        {
            float m = 0f;
            int n = Mathf.Min(a.Length, b.Count);
            for (int i = 0; i < n; i++)
            {
                float d = Mathf.Abs(a[i] - b[i]);
                if (d > m) m = d;
            }
            return m;
        }

        private static int ArgMax(float[] v)
        {
            int best = 0;
            for (int i = 1; i < v.Length; i++)
                if (v[i] > v[best]) best = i;
            return best;
        }

        // ---- fixture JSON 形状（export_onnx.py 产出）----
        private sealed class Fixture
        {
            public string manifest_sha256 = "";
            public float tol = 0.005f;
            public int case_count;
            public List<Case> cases = new List<Case>();
        }

        private sealed class Case
        {
            public List<float> rstate = new List<float>();
            public List<float> cards_flat = new List<float>();
            public List<float> global_flat = new List<float>();
            public List<float> actions_flat = new List<float>();
            public List<float> expect_rstate_flat = new List<float>();
            public List<float> expect_logits = new List<float>();
            public List<float> expect_value = new List<float>();
            public int argmax;
        }
    }
}
