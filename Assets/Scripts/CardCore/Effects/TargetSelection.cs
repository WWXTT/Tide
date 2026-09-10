using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;

namespace CardCore
{
    /// <summary>
    /// 通用目标选择请求。
    /// 调用方传入已筛选的候选实体与所需数量，由 TargetSelectionService 决定弹窗或自动选择。
    /// </summary>
    public class TargetSelectionRequest
    {
        /// <summary>候选实体（已由调用方筛选）。</summary>
        public List<Entity> Candidates = new List<Entity>();
        /// <summary>最少选择数量。任意数量场景设 0。</summary>
        public int MinCount = 1;
        /// <summary>最多选择数量。任意数量场景设 Candidates.Count。</summary>
        public int MaxCount = 1;
        /// <summary>做出选择的玩家（用于 AI 判定与超时时长来源）。</summary>
        public Player Chooser;
        /// <summary>弹窗标题。</summary>
        public string Title = "选择目标";
        /// <summary>弹窗提示语。</summary>
        public string Hint = "";
        /// <summary>是否允许取消（取消返回空列表）。</summary>
        public bool AllowCancel = false;
        /// <summary>超时秒数。0 或负数时由 Service 按回合数自动算出。</summary>
        public float TimeoutSeconds = 0f;
    }

    /// <summary>
    /// UI 层实现的纯弹窗 primitive（基于索引，实体/选项共用）。
    /// 返回选中项的索引列表；超时/取消由实现自行决定（参见 TargetSelectionService 的兜底）。
    /// </summary>
    public interface ITargetSelector
    {
        UniTask<List<int>> SelectIndicesAsync(IReadOnlyList<string> labels,
            int min, int max, string title, string hint, bool allowCancel, float timeoutSeconds);
    }

    /// <summary>
    /// 带实体引用的选择器扩展（M1 网络协议 2026-09-10）：
    /// 网络选择器（NetworkTargetSelector）需要 Candidates 实体引用才能让远端客户端
    /// 渲染候选并回传索引——基接口只传 labels 字符串不够。
    /// 引擎侧 RequestAsync/RequestOneIndexAsync 对 Current 做 is 分流，UI 实现（UiTargetSelector）
    /// 不实现本接口则走原路径，零感知零改动。requestId 由网络选择器自管（TargetSelectionRequest 本体不加字段）。
    /// </summary>
    public interface ITargetSelectorEx : ITargetSelector
    {
        /// <summary>实体反问：完整请求（含 Candidates 实体引用）+ 预生成标签；返回选中索引。</summary>
        UniTask<List<int>> SelectAsync(TargetSelectionRequest request, IReadOnlyList<string> labels);

        /// <summary>纯选项反问（RequestOneIndexAsync：抉择/mode 选择，无实体）；返回选中索引。</summary>
        UniTask<int> SelectOneAsync(Player chooser, IReadOnlyList<string> options, string title);
    }

    /// <summary>
    /// 目标选择服务（引擎侧入口）。
    /// 集中处理：AI/ヘッドレス 即时自动选择、回合数递增超时、超时/异常兜底自动选择。
    /// UI 层启动时注册 Current；测试/服务器环境保持 null（全部走自动选择）。
    /// </summary>
    public static class TargetSelectionService
    {
        /// <summary>UI 实现。null 时所有请求自动选择（headless/测试）。</summary>
        public static ITargetSelector Current { get; set; }

        /// <summary>超时下限（回合 1）。</summary>
        private const float BaseTimeout = 10f;
        /// <summary>每回合增量。</summary>
        private const float PerTurnIncrement = 5f;
        /// <summary>超时上限。</summary>
        private const float MaxTimeout = 60f;
        /// <summary>UI 自动确定的宽限时间（引擎安全网 race 用）。</summary>
        private const float GraceSeconds = 1.5f;

        /// <summary>
        /// 通用目标选择。返回玩家选中的实体列表（自动选择时为候选前 MinCount 个）。
        /// </summary>
        public static async UniTask<List<Entity>> RequestAsync(TargetSelectionRequest req)
        {
            if (req == null || req.Candidates == null || req.Candidates.Count == 0 || req.MaxCount <= 0)
                return new List<Entity>();

            GameActions.Crumb($"target-select cands={req.Candidates.Count} min={req.MinCount} chooser={req.Chooser?.Name} ui={(Current != null ? Current.GetType().Name : "null→auto")}");

            // AI / ヘッドレス → 即时自动选择（先頭 MinCount 个）
            if (Current == null || (req.Chooser != null && req.Chooser.IsAI))
                return AutoSelect(req.Candidates, req.MinCount);

            float timeout = req.TimeoutSeconds > 0f ? req.TimeoutSeconds : ComputeTimeout();
            var labels = req.Candidates.Select(DescribeEntity).ToList();

            // UI 任务与安全网超时竞速：UI 漏掉自动确定时引擎兜底。
            // M1 分流：扩展选择器（网络）拿完整请求（含 Candidates 实体引用），基接口走原路径
            var uiTask = Current is ITargetSelectorEx ex
                ? ex.SelectAsync(req, labels)
                : Current.SelectIndicesAsync(
                    labels, req.MinCount, req.MaxCount, req.Title, req.Hint, req.AllowCancel, timeout);
            var graceTask = UniTask.Delay(System.TimeSpan.FromSeconds(timeout + GraceSeconds))
                .ContinueWith(() => (List<int>)null);

            var (winIndex, uiResult, graceResult) = await UniTask.WhenAny(uiTask, graceTask);
            var indices = winIndex == 0 ? uiResult : graceResult;

            // 安全网超时（grace 胜出，winIndex==1）或 UI 返回空
            if (winIndex != 0 || indices == null || indices.Count == 0)
            {
                if (winIndex == 0 && req.AllowCancel)
                    return new List<Entity>();
                return AutoSelect(req.Candidates, req.MinCount);
            }

            return MapIndices(req.Candidates, indices);
        }

        /// <summary>
        /// 1 of N 选项选择（ChooseOne 等）。返回选中索引；自动/兜底为 0。
        /// </summary>
        public static async UniTask<int> RequestOneIndexAsync(Player chooser,
            IReadOnlyList<string> options, string title)
        {
            if (options == null || options.Count == 0) return 0;
            if (options.Count == 1) return 0;

            if (Current == null || (chooser != null && chooser.IsAI))
                return 0;

            float timeout = ComputeTimeout();

            // M1 分流：扩展选择器（网络）走 SelectOneAsync（带 chooser），基接口走原路径
            var uiTask = Current is ITargetSelectorEx ex
                ? WrapOne(ex.SelectOneAsync(chooser, options, title))
                : Current.SelectIndicesAsync(options, 1, 1, title, "", false, timeout);
            var graceTask = UniTask.Delay(System.TimeSpan.FromSeconds(timeout + GraceSeconds))
                .ContinueWith(() => (List<int>)null);

            var (winIndex, uiResult, graceResult) = await UniTask.WhenAny(uiTask, graceTask);
            var indices = winIndex == 0 ? uiResult : graceResult;

            if (winIndex == 0 && indices != null && indices.Count > 0)
            {
                int idx = indices[0];
                if (idx >= 0 && idx < options.Count) return idx;
            }
            return 0;
        }

        /// <summary>SelectOneAsync（int）适配到 WhenAny 竞速的 List&lt;int&gt; 形态；负索引视为取消（走兜底 0）。</summary>
        private static async UniTask<List<int>> WrapOne(UniTask<int> task)
        {
            int idx = await task;
            return idx >= 0 ? new List<int> { idx } : null;
        }

        /// <summary>回合数递增超时：min(10 + 5×(回合−1), 60)。</summary>
        private static float ComputeTimeout()
        {
            int turn = GameCore.Instance?.TurnEngine?.TurnNumber ?? 1;
            if (turn < 1) turn = 1;
            float t = BaseTimeout + PerTurnIncrement * (turn - 1);
            return t > MaxTimeout ? MaxTimeout : t;
        }

        private static List<Entity> AutoSelect(List<Entity> candidates, int count)
        {
            if (count <= 0) return new List<Entity>();
            return candidates.Take(System.Math.Min(count, candidates.Count)).ToList();
        }

        private static List<Entity> MapIndices(List<Entity> candidates, List<int> indices)
        {
            var result = new List<Entity>(indices.Count);
            foreach (var i in indices)
                if (i >= 0 && i < candidates.Count)
                    result.Add(candidates[i]);
            return result;
        }

        private static string DescribeEntity(Entity e)
        {
            if (e == null) return "(空)";
            if (e is Player p) return p.Name;
            if (e is IHasName named && !string.IsNullOrEmpty(named.CardName)) return named.CardName;
            if (e is Card c) return c.ID;
            return e.ToString();
        }
    }
}
