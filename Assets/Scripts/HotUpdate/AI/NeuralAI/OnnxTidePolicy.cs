using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IE = Unity.InferenceEngine;

namespace CardCore.AI.NeuralEnv
{
    /// <summary>
    /// ONNX 单步策略推理器（Sentis，包名 com.unity.ai.inference，命名空间 Unity.InferenceEngine）。
    ///
    /// 图契约（tide_rl/export_onnx.py 导出，batch 已脱皮）：
    ///   输入 float32：rstate(512) cards(86×115) global(50) actions(512×8)
    ///   输出 float32：rstate_next(512) logits(512) value(1)
    /// v6 布局（2026-10-29 P11 盘面重塑：卡槽 43/人（86 总，战场 12→9））、卡特征 115 维（v5 删旧立约位；
    /// v4 原子槽 6→12，2026-10-16）、globals 50 维（+放逐计数/弹药，
    /// 归一化）、动作类型 0..13、枚举器侧 MaxActions=512 守卫——旧 onnx/fixture 全部作废，
    /// 需重跑 export_onnx.py 重导出后使用本推理器。
    /// 非法动作在图内已掩 -1e9（actions[:,0]==0），Select 直接 argmax；
    /// rstate 局内逐步传递，新对局 Reset() 归零（与训练 rollout 同口径）。
    ///
    /// 模型加载：Sentis 运行时没有 ONNX 文件解析器（ModelLoader.Load(path) 只认
    /// .sentis 自有序列化格式）——ONNX 由编辑器 ScriptedImporter 转成 ModelAsset。
    /// 三级回落加载（LoadModelAsset）：YooAsset 热更包（真机/Launch 流程，模型随包热更）
    /// → 编辑器 AssetDatabase（直启 Main 的开发流程）→ Resources（过渡期旧位置兜底）。
    /// 模型默认放 Assets/Art/AIModels/（export_onnx.py 复制到该目录；YooAsset 收集组 AIModel）。
    ///
    /// 前置条件：TideCardIndex 必须绑定与训练同一份 manifest
    /// （ConfigureManifest(tide_rl/card_identity_manifest.json) 后 Register 卡池），
    /// 否则卡身份 embedding 行串台——模型导出时的 manifest 指纹写在 onnx metadata 里。
    /// </summary>
    public sealed class OnnxTidePolicy : IDisposable
    {
        // 与 tide_rl/tide_features.py 对齐（模型图内写死，改维度必须重导出）
        public const int RnnChannels = 512;
        public const int MaxActions = 512; // v2 契约第 4 节 128→512（逐目标展开后动作行变多；EndTurn/Pass 恒排末位保截断兜底）
        private const int MaxCards = TideObservation.MaxCardsTotal; // 86（v6：43/人×2，随 TideObservation 常量派生）

        private readonly IE.Worker _worker;
        private readonly float[] _rstate = new float[RnnChannels];
        private readonly float[] _actionBuf = new float[MaxActions * TideObservation.NAction];

        /// <summary>最近一步的 Critic 估值与原始 logits（调试/可视化用）。</summary>
        public float LastValue { get; private set; }
        public float[] LastLogits { get; private set; } = new float[MaxActions];

        /// <summary>默认模型资源名（Assets/Art/AIModels/tide_policy.onnx，YooAsset 寻址省扩展名）。</summary>
        public const string DefaultResourcePath = "tide_policy";

        /// <summary>模型目录（YooAsset Art 收集组覆盖，编辑器 AssetDatabase 兜底共用）。</summary>
        public const string ModelAssetFolder = "Assets/Art/AIModels";

        /// <summary>加载默认模型（tide_policy）并建 CPU worker。</summary>
        public OnnxTidePolicy() : this(DefaultResourcePath) { }

        /// <summary>加载 ONNX 导入的 ModelAsset 并建 CPU worker
        /// （小模型 CPU 快于 GPU，且输入输出都在 CPU）。加载时先做布局校验
        /// （ValidateModelLayout）——模型图与当前观测编码不同代立即明确报错，
        /// 不再等到首步推理抛晦涩的形状异常。</summary>
        public OnnxTidePolicy(string resourcePath)
        {
            var asset = LoadModelAsset(resourcePath);
            if (asset == null)
                throw new FileNotFoundException(
                    $"找不到策略模型 {resourcePath}（ModelAsset）。\n" +
                    $"期望位置：YooAsset 包（{Tide.HotUpdate.HotUpdateAssets.DefaultPackageName}）或 " +
                    $"{ModelAssetFolder}/{resourcePath}.onnx（tide_rl/export_onnx.py 复制到该目录；" +
                    "热更侧需先跑 Tools/热更/2 配置收集器）");
            var model = IE.ModelLoader.Load(asset);
            ValidateModelLayout(model, resourcePath);
            _worker = new IE.Worker(model, IE.BackendType.CPU);
        }

        /// <summary>
        /// 加载侧布局校验（2026-11-02 部署加固）：模型图输入维度必须与当前
        /// TideObservation 常量逐值一致。盘面重塑/特征增删后旧模型只会在首步推理
        /// 抛张量形状异常（或更糟——维度恰好兼容但语义全错），此处提前给出明确报错；
        /// 导出侧（export_onnx.py）同代把 layoutVersion/dims 写进 fixture 头与
        /// onnx metadata，Python 对拍/部署两侧可对账。不一致抛 InvalidOperationException，
        /// 由调用方（NeuralAiDriver）响亮回落 SimpleAI。
        /// </summary>
        private static void ValidateModelLayout(IE.Model model, string resourcePath)
        {
            var expect = new (string name, int[] dims)[]
            {
                ("rstate", new[] { RnnChannels }),
                ("cards", new[] { MaxCards, TideObservation.NCard }),
                ("global", new[] { TideObservation.NGlobal }),
                ("actions", new[] { MaxActions, TideObservation.NAction }),
            };
            var problems = new List<string>();
            foreach (var (name, dims) in expect)
            {
                int idx = model.inputs.FindIndex(i => i.name == name);
                if (idx < 0)
                {
                    problems.Add($"缺输入 {name}（图实际输入: {string.Join(", ", model.inputs.Select(i => i.name))}）");
                    continue;
                }
                var shape = model.inputs[idx].shape;
                if (shape.rank != dims.Length)
                {
                    problems.Add($"{name}: rank {shape.rank} ≠ {dims.Length}");
                    continue;
                }
                for (int k = 0; k < dims.Length; k++)
                {
                    int actual = shape.Get(k); // 动态维取出的值 ≠ 任何静态契约值，同样按不匹配报
                    if (actual != dims[k])
                        problems.Add($"{name}[{k}]: {DescribeDim(actual)} ≠ {dims[k]}");
                }
            }
            if (problems.Count > 0)
                throw new InvalidOperationException(
                    $"[OnnxTidePolicy] 模型布局与当前观测编码不一致（{resourcePath}）：\n  "
                    + string.Join("\n  ", problems)
                    + $"\n当前契约 layoutVersion={TideObservation.LayoutVersion}"
                      + $"（cards {MaxCards}x{TideObservation.NCard} / global {TideObservation.NGlobal}"
                      + $" / actions {MaxActions}x{TideObservation.NAction} / rstate {RnnChannels}）"
                    + "——代码与模型不同代，重跑 tide_rl/export_onnx.py 重导出后再用");
        }

        private static string DescribeDim(int dim) => dim < 0 ? "(动态维)" : dim.ToString();

        /// <summary>
        /// 三级回落：YooAsset 热更包 → 编辑器 AssetDatabase（直启 Main，未走 Launch）→
        /// Resources（过渡期旧副本兜底）。真机上模型只随资源包下发——注意必须与卡池
        /// manifest/Configs 同版本节奏更新（模型权重绑定导出时的卡身份 manifest 指纹）。
        /// </summary>
        private static IE.ModelAsset LoadModelAsset(string resourcePath)
        {
#if TIDE_HYBRID_YOO
            // YooAsset：包未初始化/收集器未配置时抛异常或返回失败句柄，捕获后回落，
            // 不阻断编辑器直启流程。成功路径的句柄不释放——worker 生命周期内保活。
            try
            {
                if (YooAsset.YooAssets.TryGetPackage(Tide.HotUpdate.HotUpdateAssets.DefaultPackageName, out var package))
                {
                    var handle = package.LoadAssetAsync<IE.ModelAsset>(resourcePath);
                    handle.WaitForAsyncComplete();
                    if (handle.Status == YooAsset.EOperationStatus.Succeeded
                        && handle.AssetObject is IE.ModelAsset viaYoo)
                        return viaYoo;
                    handle.Dispose();
                }
            }
            catch (Exception)
            {
                // 未配置 YooAsset（如编辑器直启 Main），走兜底
            }
#endif

#if UNITY_EDITOR
            var editorAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<IE.ModelAsset>(
                $"{ModelAssetFolder}/{resourcePath}.onnx");
            if (editorAsset != null)
                return editorAsset;
#endif

            return UnityEngine.Resources.Load<IE.ModelAsset>(resourcePath);
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
            if (n > MaxActions)
            {
                // v3 起枚举器已带 MaxActions 守卫（恒保末位兜底行），此处理论到不了——
                // 留作独立护栏（防旧调用方直灌超长表越界 LastLogits 定长）。
                UnityEngine.Debug.LogWarning(
                    $"[OnnxTidePolicy] 合法动作 {n} > {MaxActions}，截断到前 {MaxActions} 个");
                n = MaxActions;
            }

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
            public float[] Logits;     // (MaxActions,)
            public float Value;        // 标量
        }

        public void Dispose() => _worker?.Dispose();
    }
}
