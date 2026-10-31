using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;

namespace CardCore.Network
{
    /// <summary>
    /// 引擎反问的网络实现（M1 协议）：TargetSelectionService.Current = 本实例时，
    /// 引擎的全部结算期交互（目标选择/抉择/弃牌…）转为 MsgSelectRequest 下发
    /// 到 Chooser 座位的客户端，await 客户端回传的 MsgSelectResponse。
    ///
    /// - 生命周期：requestId 自增关联 Dictionary&lt;int, UniTaskCompletionSource&gt;；
    ///   会话层收到 SelectResponse 后调 TryComplete 路由回引擎 await 点。
    /// - 发送通道：SendAsync 委托由宿主注入（M2 真服务器 = TCP 出队；M1 回环 = 同步内联）。
    /// - 超时：引擎侧 GraceSeconds 安全网原样生效（UI 任务 vs 引擎兜底竞速）——
    ///   网络环境下的超时代打策略即"引擎 AutoSelect 兜底"，M4 再做服务器侧主动代打。
    /// - 线程：M1/M2 定案服务器单逻辑线程驱动，无锁；跨线程化前须加同步（届时随 M2 收口）。
    /// - 越界/超量索引：不在本类校验——RequestAsync 的 MapIndices 与兜底已按引擎口径处理。
    /// - 文本反问（2026-10-31 阶段C）：同构第二条通道 MsgTextRequest/MsgTextResponse
    ///   （_pendingText；卡名宣言等开放集输入），超时/取消=null=效果不结算。
    /// </summary>
    public sealed class NetworkTargetSelector : ITargetSelectorEx
    {
        private int _nextRequestId;
        private readonly Dictionary<int, UniTaskCompletionSource<List<int>>> _pending =
            new Dictionary<int, UniTaskCompletionSource<List<int>>>();

        /// <summary>文本反问在途表（2026-10-31 阶段C）：RequestId → (TCS, MaxLen, ChooserSeat)——
        /// MaxLen/ChooserSeat 随表携带，供 TryCompleteText 做服务端校验（长度超限/错座应答拒绝，
        /// 按取消 null 收口——效果不结算）。</summary>
        private readonly Dictionary<int, (UniTaskCompletionSource<string> tcs, int maxLen, int chooserSeat)> _pendingText =
            new Dictionary<int, (UniTaskCompletionSource<string>, int, int)>();

        /// <summary>反问请求发送通道（宿主注入）。抛异常视为本轮反问失败——引擎兜底 AutoSelect。</summary>
        public Func<MsgSelectRequest, UniTask> SendAsync { get; set; }

        /// <summary>文本反问请求发送通道（宿主注入）。null=宿主未接线——回落 defaultValue（AI 口径）。</summary>
        public Func<MsgTextRequest, UniTask> SendTextAsync { get; set; }

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

        /// <summary>会话层收到 MsgTextResponse 后调用（2026-10-31 阶段C）：
        /// 校验应答座位与长度（fromSeat&lt;0=观战/未知座位直拒）——不合法一律按取消 null 收口。
        /// 返回是否命中在途条目（false=迟到/未知 RequestId，会话层给可诊断回执）。</summary>
        public bool TryCompleteText(MsgTextResponse response, int fromSeat)
        {
            if (response == null) return false;
            if (!_pendingText.TryGetValue(response.RequestId, out var entry)) return false;
            _pendingText.Remove(response.RequestId);
            string text = response.Text?.Trim();
            if (fromSeat >= 0 && entry.chooserSeat >= 0 && fromSeat != entry.chooserSeat)
                text = null; // 错座应答：按取消（防对手代答宣言）
            if (text != null && entry.maxLen > 0 && text.Length > entry.maxLen)
                text = null; // 超长拒绝：按取消（长度合法性服务端校验）
            entry.tcs.TrySetResult(string.IsNullOrEmpty(text) ? null : text);
            return true;
        }

        /// <summary>是否有未决反问（M2 服务器快照静默检查：非静默点不取样——半完成结算不快照）。
        /// 与 _pending 全部访问同在逻辑线程（单逻辑线程定案），无锁。文本反问计入同一口径。</summary>
        public bool HasPending => _pending.Count > 0 || _pendingText.Count > 0;

        /// <summary>作废全部未决反问（M2 断线收口）：空索引集完成 → 引擎按空应答回落 AutoSelect，
        /// 防止断线连接的反问永远悬住泵。文本在途一并按取消 null 收口。返回作废条数。逻辑线程调用。</summary>
        public int AbortPending()
        {
            int count = _pending.Count + _pendingText.Count;
            var aborted = _pending.Values.ToList();
            _pending.Clear();
            foreach (var tcs in aborted)
                tcs.TrySetResult(new List<int>());
            var abortedText = _pendingText.Values.ToList();
            _pendingText.Clear();
            foreach (var entry in abortedText)
                entry.tcs.TrySetResult(null);
            return count;
        }

        /// <summary>实体反问：完整请求（含 Candidates 引用）下发，await 客户端索引集。
        /// timeoutSeconds = 有效决策窗口（2026-09-24 竞速归属修订）——**超时竞速在本类内部自持并自清理**：
        /// 外层 RequestAsync 曾用 WhenAny 抛弃本方法的 await，grace 胜出后 _pending 条目残留 →
        /// HasPending 卡真 → 服务器快照取样/End 折返整局冻结（客户端表现为"选了没反应、手牌不刷新"）。
        /// 超时返回 null（外层 AutoSelect 兜底）；迟到应答命中已移除条目会得"未知 RequestId"回执（可诊断、无副作用）。</summary>
        public async UniTask<List<int>> SelectAsync(TargetSelectionRequest request, IReadOnlyList<string> labels,
            float timeoutSeconds)
        {
            if (request == null || labels == null) return null;

            var msg = new MsgSelectRequest
            {
                RequestId = ++_nextRequestId,
                ChooserSeat = NetEntityMapper.SeatOf(request.Chooser),
                Title = request.Title,
                Hint = request.Hint,
                AllowCancel = request.AllowCancel,
                TimeoutSeconds = timeoutSeconds, // 有效窗口直传：客户端倒计时=服务器兜底前 1.5s（GraceSeconds）
                Min = request.MinCount,
                Max = request.MaxCount,
                // 2026-09-22 线上去文本：实体反问不传 Labels（卡名客户端按 CardId 查表，应答索引指向
                // Candidates 下标不变）；纯选项反问（Candidates 空，如抉择模式）保留 Labels 作语义载荷
                Labels = request.Candidates.Count > 0 ? Array.Empty<string>() : labels.ToArray(),
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

            // 自超时竞速（+1.5s 宽限对齐外层本地路径）：无论哪边胜出都清 _pending 条目
            var timeoutTask = TideDelay.After(TimeSpan.FromSeconds(timeoutSeconds + GraceSeconds))
                .ContinueWith(() => (List<int>)null);
            var (winIndex, uiResult, _) = await UniTask.WhenAny(tcs.Task, timeoutTask);
            _pending.Remove(msg.RequestId);
            return winIndex == 0 ? uiResult : null;
        }

        /// <summary>纯选项反问（抉择/mode）：Candidates 空、min=max=1；负索引=取消/超时（引擎兜底 0）。</summary>
        public async UniTask<int> SelectOneAsync(Player chooser, IReadOnlyList<string> options, string title,
            float timeoutSeconds)
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

            var indices = await SelectAsync(request, options, timeoutSeconds);
            if (indices != null && indices.Count > 0)
            {
                int idx = indices[0];
                return idx >= 0 && idx < options.Count ? idx : -1;
            }
            return -1;
        }

        /// <summary>文本反问（2026-10-31 阶段C）：MsgTextRequest 定向下发 ChooserSeat，await 文本应答。
        /// 超时自持自清理（同 SelectAsync 竞速归属）；无发送通道/发送失败 → defaultValue（AI 口径兜底，
        /// 效果照结算——比取消温和，防宿主未接线卡死泵）。null=取消（效果不结算、费用不退）。</summary>
        public async UniTask<string> SelectTextAsync(Player chooser, string title, string defaultValue,
            int maxLen, float timeoutSeconds)
        {
            var msg = new MsgTextRequest
            {
                RequestId = ++_nextRequestId,
                ChooserSeat = NetEntityMapper.SeatOf(chooser),
                Title = title,
                Default = defaultValue,
                MaxLen = maxLen > 0 ? maxLen : 32,
                TimeoutSeconds = timeoutSeconds,
            };

            var tcs = new UniTaskCompletionSource<string>();
            _pendingText[msg.RequestId] = (tcs, msg.MaxLen, msg.ChooserSeat);

            if (SendTextAsync == null)
            {
                _pendingText.Remove(msg.RequestId);
                return defaultValue; // 未接线：AI 口径兜底
            }

            try
            {
                await SendTextAsync(msg);
            }
            catch
            {
                _pendingText.Remove(msg.RequestId);
                return defaultValue; // 发送失败：兜底（非取消——网络层故障不罚玩家）
            }

            var timeoutTask = TideDelay.After(TimeSpan.FromSeconds(timeoutSeconds + GraceSeconds))
                .ContinueWith(() => (string)null);
            var (winIndex, uiResult, _) = await UniTask.WhenAny(tcs.Task, timeoutTask);
            _pendingText.Remove(msg.RequestId);
            return winIndex == 0 ? uiResult : null;
        }

        /// <summary>超时宽限（对齐 TargetSelectionService.GraceSeconds——客户端倒计时先于此到期）。</summary>
        private const float GraceSeconds = 1.5f;

        /// <summary>基接口成员：Ex 消费方不使用（RequestAsync 已分流到 SelectAsync）。</summary>
        public UniTask<List<int>> SelectIndicesAsync(IReadOnlyList<string> labels,
            int min, int max, string title, string hint, bool allowCancel, float timeoutSeconds)
            => throw new NotSupportedException(
                "NetworkTargetSelector 只经 ITargetSelectorEx 消费（RequestAsync 分流）；基接口路径不应到达");
    }
}
