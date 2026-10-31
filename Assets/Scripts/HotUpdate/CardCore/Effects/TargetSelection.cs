using System;
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
        /// <summary>选择目的说明源（2026-10-16 lbl-toast 定案）：发起本次选择的宿主实体
        ///（施放中的卡优先，其次效果来源卡；UI 据此渲染卡名与完整效果文，说明「选目标来干嘛」。
        /// null=未提供，UI 回落 Title 文案）。</summary>
        public Entity PromptSource;
        /// <summary>发起本次选择的效果 Id（对应卡表 CardData.Effects[].Id——UI 查卡表渲染完整效果文；
        /// 空=未知效果，UI 只报卡名+标题）。</summary>
        public string PromptEffectId;
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
    /// 引擎侧 RequestAsync/RequestOneIndexAsync 对 Current 做 is 分流，UI 实现（RegionTargetSelector）
    /// 不实现本接口则走原路径，零感知零改动。requestId 由网络选择器自管（TargetSelectionRequest 本体不加字段）。
    ///
    /// 2026-09-24 竞速归属修订：timeoutSeconds = 本请求的有效决策窗口——**超时竞速由 Ex 实现内部
    /// 自持并自清理**（外层不再 WhenAny 抛弃实现的 await——那会让网络选择器的 _pending 残留，
    /// HasPending 卡真导致服务器快照取样/End 折返整局冻结）；超时由实现返回 null，外层 AutoSelect 兜底。
    /// </summary>
    public interface ITargetSelectorEx : ITargetSelector
    {
        /// <summary>实体反问：完整请求（含 Candidates 实体引用）+ 预生成标签 + 决策窗口（秒）；超时返回 null。</summary>
        UniTask<List<int>> SelectAsync(TargetSelectionRequest request, IReadOnlyList<string> labels, float timeoutSeconds);

        /// <summary>纯选项反问（RequestOneIndexAsync：抉择/mode 选择，无实体）+ 决策窗口（秒）；负索引=取消/超时。</summary>
        UniTask<int> SelectOneAsync(Player chooser, IReadOnlyList<string> options, string title, float timeoutSeconds);
    }

    /// <summary>
    /// 目标选择服务（引擎侧入口）。
    /// 集中处理：AI/无头环境即时自动选择、回合数递增超时、超时/异常兜底自动选择。
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
        /// <summary>网络反问的人类决策超时（2026-09-24）：超时梯子（10s 起）为本地 UI 调校，
        /// 远端人类读牌远超此数——命中即被 AutoSelect 代选（弃错卡/选错目标，用户视角"选了没生效"）。
        /// AI Chooser 走即时自动选择不受影响。</summary>
        private const float NetworkHumanTimeoutSeconds = 120f;

        /// <summary>在途反问数（2026-09-24）：End→Standby 折返门用——手牌上限弃牌等回合末
        /// 异步链未收口前不得折返（否则弃牌与新回合抽牌交错、手牌口径漂移）。
        /// 静态计数对本地 UI 与网络选择器通用（网络侧另有 NetRoom.HasPending 同向门）。</summary>
        public static int PendingRequests { get; private set; }

        /// <summary>
        /// 通用目标选择。返回玩家选中的实体列表（自动选择时为候选前 MinCount 个）。
        /// </summary>
        public static async UniTask<List<Entity>> RequestAsync(TargetSelectionRequest req)
        {
            if (req == null || req.Candidates == null || req.Candidates.Count == 0 || req.MaxCount <= 0)
                return new List<Entity>();

            GameActions.Crumb($"target-select cands={req.Candidates.Count} min={req.MinCount} chooser={req.Chooser?.Name} ui={(Current != null ? Current.GetType().Name : "null→auto")}");

            // AI / 无头 → 即时自动选择（取前 MinCount 个）
            if (Current == null || (req.Chooser != null && req.Chooser.IsAI))
                return AutoSelect(req.Candidates, req.MinCount);

            float timeout = req.TimeoutSeconds > 0f ? req.TimeoutSeconds : ComputeTimeout();

            // 牌库域折叠（2026-10-16 定案：牌库=区域目标，同角色格——不是卡）：牌库卡永不进
            // 选择面（隐藏信息），每侧牌库折叠为单一区域格（实体=归属角色、标签=己方牌库/
            // 对方牌库，卡面信息不外泄）。仅人类交互路径生效——AI/无头已在上方代选早退，
            // 验证器/训练环境候选口径零改动。选数上限随折叠后候选数收口（防确认门永不满）。
            var zoneLabels = CollapseDeckZoneCells(req);
            var labels = req.Candidates.Select(
                e => e is Player p && zoneLabels != null && zoneLabels.TryGetValue(p, out var zl) ? zl
                    : DescribeEntity(e)).ToList();

            PendingRequests++;
            try
            {
            List<int> indices;
            if (Current is ITargetSelectorEx ex)
            {
                // 网络（Ex）+ 人类决策 → 拉长到网络人类档（梯子是本地 UI 口径）。
                // 竞速唯一归属=Ex 实现内部（自超时并自清理 _pending——2026-09-24 泄漏修复，
                // 此前外层 WhenAny 抛弃实现的 await，超时胜出后 _pending 残留 → HasPending 卡真
                // → 快照取样/End 折返整局冻结，客户端表现为"选了没反应、手牌不刷新"）。
                if (req.TimeoutSeconds <= 0f && (req.Chooser == null || !req.Chooser.IsAI))
                    timeout = NetworkHumanTimeoutSeconds;
                indices = await ex.SelectAsync(req, labels, timeout);
            }
            else
            {
                // 本地 UI：UI 任务与安全网超时竞速（UI 漏掉自动确定时引擎兜底）
                var uiTask = Current.SelectIndicesAsync(
                    labels, req.MinCount, req.MaxCount, req.Title, req.Hint, req.AllowCancel, timeout);
                var graceTask = TideDelay.After(System.TimeSpan.FromSeconds(timeout + GraceSeconds))
                    .ContinueWith(() => (List<int>)null);

                var (winIndex, uiResult, graceResult) = await UniTask.WhenAny(uiTask, graceTask);
                indices = winIndex == 0 ? uiResult : graceResult;
            }

                if (indices == null)
                    return AutoSelect(req.Candidates, req.MinCount); // 超时（安全网）：自动代选
                if (indices.Count == 0)
                    return req.AllowCancel ? new List<Entity>() : AutoSelect(req.Candidates, req.MinCount);

                return MapIndices(req.Candidates, indices);
            }
            finally
            {
                PendingRequests--;
            }
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

            PendingRequests++;
            try
            {
                // M1 分流 + 2026-09-24 竞速归属修订：Ex（网络）自持超时并自清理；人类决策拉长到网络档
                if (Current is ITargetSelectorEx ex)
                {
                    if (chooser == null || !chooser.IsAI)
                        timeout = NetworkHumanTimeoutSeconds;
                    var picked = await ex.SelectOneAsync(chooser, options, title, timeout);
                    return picked >= 0 && picked < options.Count ? picked : 0;
                }

                var uiTask = Current.SelectIndicesAsync(options, 1, 1, title, "", false, timeout);
                var graceTask = TideDelay.After(System.TimeSpan.FromSeconds(timeout + GraceSeconds))
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
            finally
            {
                PendingRequests--;
            }
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

        /// <summary>牌库域折叠（2026-10-16 定案）：候选中的牌库卡替换为其归属角色——每侧牌库一格
        ///（返回区域格标签表：角色→「己方牌库/对方牌库」，按 Chooser 判侧；null=本次无折叠）。
        /// 同一角色已在候选（混合域 角色+牌库）时去重不重复入格。Min/Max 随折叠后候选数收口
        ///（防选数门永不满卡确认）。非牌库卡照常逐卡入面。</summary>
        private static Dictionary<Player, string> CollapseDeckZoneCells(TargetSelectionRequest req)
        {
            var core = GameCore.Instance;
            var zm = core?.ZoneManager;
            if (zm == null || req.Candidates.Count == 0) return null;

            Dictionary<Player, string> labels = null;
            for (int i = 0; i < req.Candidates.Count; i++)
            {
                if (!(req.Candidates[i] is Card c)) continue;
                var owner = zm.IsCardInZone(c, core.Player1, Zone.Deck) ? core.Player1
                    : zm.IsCardInZone(c, core.Player2, Zone.Deck) ? core.Player2
                    : null;
                if (owner == null) continue;
                if (req.Candidates.Contains(owner))
                {
                    req.Candidates.RemoveAt(i); // 角色已在候选：去重（角色格即区域格）
                    i--;
                    continue;
                }
                req.Candidates[i] = owner; // 牌库卡 → 区域格（实体=归属角色）
                labels = labels ?? new Dictionary<Player, string>();
                labels[owner] = owner == req.Chooser ? "己方牌库" : "对方牌库";
            }
            if (labels == null) return null;

            if (req.MaxCount > req.Candidates.Count) req.MaxCount = req.Candidates.Count;
            if (req.MinCount > req.MaxCount) req.MinCount = req.MaxCount;
            return labels;
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

    /// <summary>
    /// 选格反问桥（2026-10-31 场面轴3c MoveUnit）：引擎侧原子 handler 结算期弹格用——
    /// 参数=待移动单位 + 合法落格（MoveUnitRules 单源筛得的候选）+ 提示语，
    /// 返回选中格在候选表中的**索引**（-1=取消/放弃；索引制沿用 RequestOneIndexAsync 口径，
    /// UniTask&lt;int&gt; 避开热更 struct AOT 坑）。
    /// UI（BattleScreen）注入 BattleCellPicker 实现（与召唤生物选格同交互）；
    /// null=无选格交互（AI/无头/服务器）——调用方安全空转 + TideLog.Warn。
    /// 联机 TODO：协议不承载选格，联机局不注入本桥（移动恒空转）。
    /// </summary>
    public static class CellSelectionService
    {
        public static Func<Card, IReadOnlyList<(int x, int z)>, string, UniTask<int>> Current { get; set; }
    }
}
