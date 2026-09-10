using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;

namespace CardCore.Network
{
    /// <summary>
    /// 引擎反问的网络实现（M1 协议）：TargetSelectionService.Current = 本实例时，
    /// 引擎的全部结算期交互（目标选择/抉择/弃牌/召唤素材…）转为 MsgSelectRequest 下发
    /// 到 Chooser 座位的客户端，await 客户端回传的 MsgSelectResponse。
    ///
    /// - 生命周期：requestId 自增关联 Dictionary&lt;int, UniTaskCompletionSource&gt;；
    ///   会话层收到 SelectResponse 后调 TryComplete 路由回引擎 await 点。
    /// - 发送通道：SendAsync 委托由宿主注入（M2 真服务器 = TCP 出队；M1 回环 = 同步内联）。
    /// - 超时：引擎侧 GraceSeconds 安全网原样生效（UI 任务 vs 引擎兜底竞速）——
    ///   网络环境下的超时代打策略即"引擎 AutoSelect 兜底"，M4 再做服务器侧主动代打。
    /// - 线程：M1/M2 定案服务器单逻辑线程驱动，无锁；跨线程化前须加同步（届时随 M2 收口）。
    /// - 越界/超量索引：不在本类校验——RequestAsync 的 MapIndices 与兜底已按引擎口径处理。
    /// </summary>
    public sealed class NetworkTargetSelector : ITargetSelectorEx
    {
        private int _nextRequestId;
        private readonly Dictionary<int, UniTaskCompletionSource<List<int>>> _pending =
            new Dictionary<int, UniTaskCompletionSource<List<int>>>();

        /// <summary>反问请求发送通道（宿主注入）。抛异常视为本轮反问失败——引擎兜底 AutoSelect。</summary>
        public Func<MsgSelectRequest, UniTask> SendAsync { get; set; }

        /// <summary>挂载到引擎（TargetSelectionService.Current = 本实例）。装/卸由宿主管理。</summary>
        public void Attach() => TargetSelectionService.Current = this;

        /// <summary>卸载（恢复 headless 自动选择路径）。</summary>
        public void Detach()
        {
            if (ReferenceEquals(TargetSelectionService.Current, this))
                TargetSelectionService.Current = null;
        }

        /// <summary>会话层收到 MsgSelectResponse 后调用：唤醒对应引擎 await 点。</summary>
        public bool TryComplete(MsgSelectResponse response)
        {
            if (response == null) return false;
            if (!_pending.TryGetValue(response.RequestId, out var tcs)) return false;
            _pending.Remove(response.RequestId);
            var indices = response.Indices?.ToList() ?? new List<int>();
            tcs.TrySetResult(indices);
            return true;
        }

        /// <summary>实体反问：完整请求（含 Candidates 引用）下发，await 客户端索引集。</summary>
        public async UniTask<List<int>> SelectAsync(TargetSelectionRequest request, IReadOnlyList<string> labels)
        {
            if (request == null || labels == null) return null;

            var msg = new MsgSelectRequest
            {
                RequestId = ++_nextRequestId,
                ChooserSeat = NetEntityMapper.SeatOf(request.Chooser),
                Title = request.Title,
                Hint = request.Hint,
                AllowCancel = request.AllowCancel,
                TimeoutSeconds = request.TimeoutSeconds, // 0 = 客户端按引擎同款公式自算
                Min = request.MinCount,
                Max = request.MaxCount,
                Labels = labels.ToArray(),
                Candidates = request.Candidates.Select(NetEntityMapper.FromEntity).ToArray(),
            };

            var tcs = new UniTaskCompletionSource<List<int>>();
            _pending[msg.RequestId] = tcs;

            if (SendAsync == null)
            {
                _pending.Remove(msg.RequestId);
                return null; // 无发送通道：走引擎兜底（等同未注册）
            }

            try
            {
                await SendAsync(msg);
            }
            catch
            {
                _pending.Remove(msg.RequestId);
                return null; // 发送失败：引擎兜底
            }

            // 引擎 GraceSeconds 竞速在外层 RequestAsync——此处纯 await，超时由外层截胡
            return await tcs.Task;
        }

        /// <summary>纯选项反问（抉择/mode）：Candidates 空、min=max=1；负索引=取消（引擎兜底 0）。</summary>
        public async UniTask<int> SelectOneAsync(Player chooser, IReadOnlyList<string> options, string title)
        {
            var request = new TargetSelectionRequest
            {
                Candidates = new List<Entity>(),
                MinCount = 1,
                MaxCount = 1,
                Chooser = chooser,
                Title = title,
                AllowCancel = false,
            };

            var indices = await SelectAsync(request, options);
            if (indices != null && indices.Count > 0)
            {
                int idx = indices[0];
                return idx >= 0 && idx < options.Count ? idx : -1;
            }
            return -1;
        }

        /// <summary>基接口成员：Ex 消费方不使用（RequestAsync 已分流到 SelectAsync）。</summary>
        public UniTask<List<int>> SelectIndicesAsync(IReadOnlyList<string> labels,
            int min, int max, string title, string hint, bool allowCancel, float timeoutSeconds)
            => throw new NotSupportedException(
                "NetworkTargetSelector 只经 ITargetSelectorEx 消费（RequestAsync 分流）；基接口路径不应到达");
    }
}
