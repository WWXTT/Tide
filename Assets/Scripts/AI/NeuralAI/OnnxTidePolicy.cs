using System;
using System.IO;
using IE = Unity.InferenceEngine;

namespace CardCore.AI.NeuralEnv
{
    /// <summary>
    /// ONNX 单步策略推理器（Sentis，包名 com.unity.ai.inference，命名空间 Unity.InferenceEngine）。
    ///
    /// 图契约（tide_rl/export_onnx.py 导出，batch 已脱皮）：
    ///   输入 float32：rstate(512) cards(80×71) global(32) actions(128×6)
    ///   输出 float32：rstate_next(512) logits(128) value(1)
    /// 非法动作在图内已掩 -1e9（actions[:,0]==0），Select 直接 argmax；
    /// rstate 局内逐步传递，新对局 Reset() 归零（与训练 rollout 同口径）。
    ///
    /// 模型加载：Sentis 运行时没有 ONNX 文件解析器（ModelLoader.Load(path) 只认
    /// .sentis 自有序列化格式）——ONNX 必须放 Assets/Resources/ 由编辑器
    /// ScriptedImporter 转成 ModelAsset，此处经 Resources.Load + ModelLoader.Load(asset)
    /// 加载（export_onnx.py 默认复制到该目录）。
    ///
    /// 前置条件：TideCardIndex 必须绑定与训练同一份 manifest
    /// （ConfigureManifest(tide_rl/card_identity_manifest.json) 后 Register 卡池），
    /// 否则卡身份 embedding 行串台——模型导出时的 manifest 指纹写在 onnx metadata 里。
    /// </summary>
    public sealed class OnnxTidePolicy : IDisposable
    {
        // 与 tide_rl/tide_features.py 对齐（模型图内写死，改维度必须重导出）
        public const int RnnChannels = 512;
        public const int MaxActions = 128;
        private const int MaxCards = TideObservation.MaxCardsTotal; // 80

        private readonly IE.Worker _worker;
        private readonly float[] _rstate = new float[RnnChannels];
        private readonly float[] _actionBuf = new float[MaxActions * TideObservation.NAction];

        /// <summary>最近一步的 Critic 估值与原始 logits（调试/可视化用）。</summary>
        public float LastValue { get; private set; }
        public float[] LastLogits { get; private set; } = new float[MaxActions];

        /// <summary>默认模型资源名（Assets/Resources/tide_policy.onnx，省扩展名）。</summary>
        public const string DefaultResourcePath = "tide_policy";

        /// <summary>加载默认模型（Resources/tide_policy）并建 CPU worker。</summary>
        public OnnxTidePolicy() : this(DefaultResourcePath) { }

        /// <summary>从 Resources 加载 ONNX 导入的 ModelAsset 并建 CPU worker
        /// （小模型 CPU 快于 GPU，且输入输出都在 CPU）。</summary>
        public OnnxTidePolicy(string resourcePath)
        {
            var asset = UnityEngine.Resources.Load<IE.ModelAsset>(resourcePath);
            if (asset == null)
                throw new FileNotFoundException(
                    $"Resources 里找不到策略模型 {resourcePath}（ModelAsset）。\n" +
                    "把 tide_policy.onnx 放到 Assets/Resources/ 并等 Unity 完成 ONNX 导入" +
                    "（tide_rl/export_onnx.py 默认复制到该目录）");
            _worker = new IE.Worker(IE.ModelLoader.Load(asset), IE.BackendType.CPU);
        }

        /// <summary>新对局：GRU 隐状态归零（镜像训练 env 的 episode 起点复位）。</summary>
        public void Reset() => Array.Clear(_rstate, 0, _rstate.Length);

        /// <summary>
        /// 部署主入口：观测 + 合法动作表 → 动作下标（argmax，确定性策略）。
        /// 合法动作特征补零到 128×6（valid=0 → 图内掩 -1e9），并接管 rstate 传递。
        /// </summary>
        public int Select(TideObservation obs, LegalActionEnumerator legal)
        {
            int n = legal?.Actions?.Count ?? 0;
            if (n <= 0) return -1;

            Array.Clear(_actionBuf, 0, _actionBuf.Length);
            int copy = Math.Min(legal.Features.Length, _actionBuf.Length);
            Array.Copy(legal.Features, _actionBuf, copy);

            var outputs = Step(_rstate, obs.Cards, obs.Globals, _actionBuf);
            Array.Copy(outputs.RstateNext, _rstate, RnnChannels);
            LastValue = outputs.Value;
            Array.Copy(outputs.Logits, LastLogits, Math.Min(outputs.Logits.Length, MaxActions));

            // argmax 限合法区间（图内已掩非法，此处只做区间护栏：真实动作数 n ≤ 128）
            int best = 0;
            float bestV = float.NegativeInfinity;
            for (int i = 0; i < n; i++)
                if (LastLogits[i] > bestV) { bestV = LastLogits[i]; best = i; }
            return best;
        }

        /// <summary>原始单步推理（无状态，fixture 对拍用）。输入维度不符将抛异常（张量构造处）。</summary>
        public StepOutputs Step(float[] rstate, float[] cards, float[] globals, float[] actions)
        {
            using (var tR = new IE.Tensor<float>(new IE.TensorShape(RnnChannels), rstate))
            using (var tC = new IE.Tensor<float>(new IE.TensorShape(MaxCards, TideObservation.NCard), cards))
            using (var tG = new IE.Tensor<float>(new IE.TensorShape(TideObservation.NGlobal), globals))
            using (var tA = new IE.Tensor<float>(new IE.TensorShape(MaxActions, TideObservation.NAction), actions))
            {
                _worker.SetInput("rstate", tR);
                _worker.SetInput("cards", tC);
                _worker.SetInput("global", tG);
                _worker.SetInput("actions", tA);
                _worker.Schedule();

                var oR = _worker.PeekOutput("rstate_next") as IE.Tensor<float>;
                var oL = _worker.PeekOutput("logits") as IE.Tensor<float>;
                var oV = _worker.PeekOutput("value") as IE.Tensor<float>;
                return new StepOutputs
                {
                    RstateNext = oR.DownloadToArray(),
                    Logits = oL.DownloadToArray(),
                    Value = oV.DownloadToArray()[0],
                };
            }
        }

        public struct StepOutputs
        {
            public float[] RstateNext; // (512,)
            public float[] Logits;     // (128,)
            public float Value;        // 标量
        }

        public void Dispose() => _worker?.Dispose();
    }
}
