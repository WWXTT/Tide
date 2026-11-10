using System;
using System.Linq;
using CardCore;
using CardCore.Network;
using Cysharp.Threading.Tasks;
using Tide.HotUpdate;
using TMPro;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>排位匹配状态（S8/S9 2026-11-10）。Accepted 后移交战斗；Cancelled/Expired 统一收敛回 Idle。</summary>
    public enum RankedMatchState
    {
        Idle = 0,
        Searching = 1,   // 匹配中（计时走字；候选=TapTap ranked 房；无候选自开等待房）
        Matched = 2,     // 双座满——5s 接受窗（浮窗按钮变「接受」）
        Accepted = 3,    // 本端已接受——等对手/开局
    }

    /// <summary>
    /// 排位匹配服务（S8/S9；热更静态——不属任何屏，浮窗挂 UIBootstrap Canvas 根，切屏不关）。
    /// 状态机：Idle → Searching(计时) → Matched(5s 接受窗) → Accepted(移交战斗) | Cancelled/Expired→Idle。
    /// 定案（2026-11-10）：①双方都要点「接受」——服务器（NetRoom）收齐两 ack 才开局，任一拒绝/5s 超时
    /// 广播 Error("排位未接受","Ranked") 并解散；②排位必须 TapTap 登录；③超时回 Idle 不自动重排。
    /// 匹配策略：0-10s 胜率差≤5%（千分比 50）；10-20s ≤20%（200）；>20s 随机——候选=TapTap 公开
    /// ranked 房（滤自房），无候选时自开排位等待房（指纹/胜率随 customProperties 广播）。
    /// 终局记账：Fingerprint 静态保留到战斗终局（BattleScreen.Net 按 summary.Ranked 记 DeckRatingStore）。
    /// </summary>
    public static class RankedMatchmaking
    {
        /// <summary>匹配候选拉取间隔（秒）。</summary>
        private const float ScanIntervalSeconds = 2f;
        /// <summary>接受窗时长（秒；与 NetRoom.RankedAckSeconds 同值——客户端本地超时不点也收敛）。</summary>
        private const int AcceptWindowSeconds = 5;

        public static RankedMatchState State { get; private set; } = RankedMatchState.Idle;
        /// <summary>匹配占用中（含 Accepted——已接受等对手期间同样不能进常规房间）。</summary>
        public static bool IsBusy => State != RankedMatchState.Idle;

        /// <summary>本次排位的卡组内容指纹（Start 时定格；终局记账键——Accepted 后仍保留到记账）。</summary>
        public static string Fingerprint { get; private set; } = "";

        /// <summary>状态迁移通知（LobbyScreen 排位页状态行/常规门禁刷新）。</summary>
        public static event Action StateChanged;

        private static LobbyScreen _screen;
        private static int _winRatePermille = -1;
        private static DateTime _searchStartUtc;
        private static DateTime _matchedUtc;
        private static bool _hostingRoom;      // 自开排位等待房（候选出现时先拆再进）
        private static bool _loopRunning;
        private static DateTime _lastScanUtc = DateTime.MinValue;

        // ---- 浮窗（挂 UIBootstrap Canvas 根——切屏不关；关闭即销毁）----
        private static RectTransform _window;
        private static TMP_Text _mmLabel;
        private static UnityEngine.UI.Button _mmButton;
        private static TMP_Text _mmButtonLabel;
        private static UnityEngine.UI.Image _mmButtonImg;

        /// <summary>开始排位（排位页「开始排位」）：门槛=登录+选中卡组+Idle。成功进 Searching。</summary>
        public static void Start(LobbyScreen screen)
        {
            if (IsBusy) return; // 含 Accepted——上一场匹配未收口前不再起
            if (screen == null) return;
            var tap = screen.TapSection;
            if (tap == null || !tap.IsLoggedIn)
            {
                screen.SetError("排位需要 TapTap 登录（未登录可用常规局域网）");
                return;
            }
            var deck = screen.SelectedDeck;
            if (deck == null) { screen.SetError("请先在左侧选择一套卡组"); return; }

            _screen = screen;
            Fingerprint = DeckRatingStore.FingerprintOf(deck.cardIds, deck.tokenCardIds, deck.tokenBindings);
            _winRatePermille = DeckRatingStore.PermilleOf(Fingerprint);
            _hostingRoom = false;
            _lastScanUtc = DateTime.MinValue;
            _searchStartUtc = DateTime.UtcNow;
            State = RankedMatchState.Searching;
            BuildWindow();
            RaiseStateChanged();
            if (!_loopRunning) RunLoop().Forget();
        }

        /// <summary>取消匹配（浮窗按钮）：Matched/Accepted 都先发 RankedAck(false)——服务器即刻广播
        /// Error("排位未接受") 解散，对手立刻收敛回 Idle 重排（不必白等 5s 超时窗）。</summary>
        public static void Cancel()
        {
            if (State == RankedMatchState.Idle) return;
            if (State == RankedMatchState.Matched || State == RankedMatchState.Accepted)
                _screen?.SendRankedAck(false);
            var screen = _screen;
            Stop("已取消排位匹配", asError: false);
            screen?.TeardownSession(); // 拆等待房/断候选连接（SDK 退房）
        }

        /// <summary>RoomState 钩子（LobbyScreen.OnRoomState 转发）：双座满 → 进 5s 接受窗。</summary>
        internal static void OnRoomState(MsgRoomState room)
        {
            if (State != RankedMatchState.Searching || room?.Players == null) return;
            bool full = room.Players.Length >= 2 && room.Players.All(p => p != null && p.Connected);
            if (!full) return;
            _matchedUtc = DateTime.UtcNow;
            State = RankedMatchState.Matched;
            RaiseStateChanged();
            RefreshWindow();
        }

        /// <summary>本端点「接受」：发 RankedAck(true) → Accepted（等对手/服务器开局）。</summary>
        private static void Accept()
        {
            if (State != RankedMatchState.Matched) return;
            _screen?.SendRankedAck(true);
            State = RankedMatchState.Accepted;
            RaiseStateChanged();
            RefreshWindow();
        }

        /// <summary>战斗移交钩子（LobbyScreen.HandOverToBattle）：关窗回 Idle——Fingerprint 保留供终局记账。</summary>
        internal static void OnBattleHandedOver()
        {
            if (State == RankedMatchState.Idle) return;
            State = RankedMatchState.Idle;
            CloseWindow();
            RaiseStateChanged();
        }

        /// <summary>服务器 Error(Ranked) 钩子（排位未接受/超时——双方各自收敛回 Idle，不自动重排）。
        /// 文案已由 LobbyScreen.OnServerError SetError 展示。</summary>
        internal static void NotifyError()
        {
            if (State == RankedMatchState.Idle) return;
            State = RankedMatchState.Idle;
            CloseWindow();
            RaiseStateChanged();
        }

        /// <summary>浮窗主循环（250ms tick）：计时走字 + 候选扫描节流 + 接受窗本地 5s 超时。</summary>
        private static async UniTaskVoid RunLoop()
        {
            if (_loopRunning) return;
            _loopRunning = true;
            while (State != RankedMatchState.Idle)
            {
                try
                {
                    if (State == RankedMatchState.Searching)
                    {
                        if ((DateTime.UtcNow - _lastScanUtc).TotalSeconds >= ScanIntervalSeconds)
                            ScanOnce().Forget();
                    }
                    else if (State == RankedMatchState.Matched
                        && (DateTime.UtcNow - _matchedUtc).TotalSeconds >= AcceptWindowSeconds)
                    {
                        // 本端 5s 未点——本地收敛（服务器侧同窗超时也会广播；主动收口不等广播）
                        var screen = _screen;
                        Stop("排位匹配超时未接受——已结束（可重新开始排位）", asError: true);
                        screen?.TeardownSession();
                    }
                }
                catch (Exception ex)
                {
                    TideLog.Warn($"[RankedMM] 循环异常（继续）：{ex.GetType().Name} {ex.Message}");
                }
                RefreshWindow();
                await UniTask.Delay(250);
            }
            _loopRunning = false;
            CloseWindow();
        }

        /// <summary>拉一次候选并择优加入；无候选且未自开房 → 自开排位等待房。</summary>
        private static async UniTaskVoid ScanOnce()
        {
            _lastScanUtc = DateTime.UtcNow;
            var tap = _screen?.TapSection;
            if (tap == null) return;
            var rooms = await tap.ListRoomsAsync();
            if (State != RankedMatchState.Searching || rooms == null) return;

            double elapsed = (DateTime.UtcNow - _searchStartUtc).TotalSeconds;
            int ownRate = _winRatePermille < 0 ? 500 : _winRatePermille; // 无战绩按五成互配
            int maxDiff = elapsed <= 10 ? 50 : elapsed <= 20 ? 200 : int.MaxValue; // 5%/20%/随机
            var candidate = rooms
                .Where(r => r.Ranked && !string.IsNullOrEmpty(r.RoomId)
                            && r.RoomId != tap.LastCreatedRoomId) // 滤自开等待房
                .Where(r =>
                {
                    int rate = r.WinRate < 0 ? 500 : r.WinRate;
                    return Math.Abs(rate - ownRate) <= maxDiff;
                })
                .FirstOrDefault();
            if (candidate != null)
            {
                await JoinCandidate(tap, candidate);
                return;
            }
            if (!_hostingRoom)
                await HostWaitingRoom(tap);
        }

        /// <summary>加入候选排位房：先拆自开等待房（SDK 一连接一房——必须先退净再进），
        /// 再 SDK join + 进房协议（Ranked=true+指纹+胜率随 JoinRoom 上报）。RoomState 双座满切 Matched。</summary>
        private static async UniTask JoinCandidate(ITapLobbySection tap, TapRoomEntry room)
        {
            if (_hostingRoom)
            {
                _screen.TeardownSession(); // 停等待房服务器 + SDK 退房
                _hostingRoom = false;
                var reErr = await tap.EnsureLoginAsync(); // SDK 退房后重连对战服务
                if (reErr != null) return; // 下轮再扫
            }
            var client = await tap.JoinRoomAsync(room.RoomId, null);
            if (client == null || State != RankedMatchState.Searching) return; // 房已散——下轮再扫
            _screen.BeginGuestSession($"排位 vs {room.Name}", "排位房间");
            _screen.AdoptClientAndJoin(client, wantSeat: -1, _screen.ResolveOnlineNickname(),
                _screen.BuildJoinOptions(ranked: true));
            TideLog.Info($"[RankedMM] 已加入排位房「{room.Name}」——等双座满进接受窗");
        }

        /// <summary>自开排位等待房（无候选时）：ranked/fp/rate 随 customProperties 广播；
        /// 回环入座同样 Ranked=true——自己也是候选的一端。</summary>
        private static async UniTask HostWaitingRoom(ITapLobbySection tap)
        {
            var server = await tap.CreateRankedRoomAsync($"排位·{tap.LoginName}", Fingerprint, _winRatePermille);
            if (server == null || State != RankedMatchState.Searching) return; // 创建失败——下轮再试
            _hostingRoom = true;
            _screen.BeginHostSession(server, "排位等待对手——双座满后双方 5 秒内点「接受」", "排位房间");
            _screen.ConnectLoopback(server.Port, _screen.ResolveOnlineNickname(),
                _screen.BuildJoinOptions(ranked: true));
            TideLog.Info($"[RankedMM] 已开排位等待房（指纹 {Fingerprint}，胜率 ‰{_winRatePermille}）");
        }

        /// <summary>收敛到 Idle 并提示（asError=红字）。</summary>
        private static void Stop(string message, bool asError)
        {
            State = RankedMatchState.Idle;
            CloseWindow();
            RaiseStateChanged();
            if (string.IsNullOrEmpty(message)) return;
            if (asError) _screen?.SetError(message);
            else _screen?.SetStatus(message);
        }

        private static void RaiseStateChanged() => StateChanged?.Invoke();

        // ============================================================ 浮窗（全局——UIBootstrap Canvas 根） ============================================================

        /// <summary>建浮窗：置顶独立小面板（已匹配计时 + 取消/接受按钮）。重建前先销毁旧窗。</summary>
        private static void BuildWindow()
        {
            CloseWindow();
            var boot = UnityEngine.Object.FindFirstObjectByType<UIBootstrap>(); // S8b④：新 API（FindObjectOfType 已弃用警告）
            if (boot == null) { TideLog.Warn("[RankedMM] 无 UIBootstrap——浮窗缺席（匹配逻辑照跑）"); return; }
            var root = (RectTransform)boot.transform;
            _window = UiKit.Panel("ranked-mm-window", root, pad: 12f, spacing: 8f);
            _window.anchorMin = _window.anchorMax = new Vector2(0.5f, 1f); // 顶部居中悬浮
            _window.pivot = new Vector2(0.5f, 1f);
            _window.anchoredPosition = new Vector2(0f, -16f);
            UiKit.Size(_window, w: 640f, h: 96f);
            _window.SetAsLastSibling();
            _mmLabel = UiKit.Label("lbl-mm", _window, "已匹配 00:00", 18, UiStyle.TextPrimary);
            UiKit.Size(_mmLabel.rectTransform, fw: 1f);
            _mmButton = UiKit.Button("btn-mm", _window, "取消", OnWindowButton, UiStyle.BtnBg,
                height: 44f, fontSize: 16);
            _mmButtonLabel = _mmButton != null ? _mmButton.GetComponentInChildren<TMP_Text>(true) : null;
            _mmButtonImg = _mmButton != null ? _mmButton.GetComponent<UnityEngine.UI.Image>() : null;
            RefreshWindow();
        }

        /// <summary>浮窗按钮：Searching=取消 / Matched=接受 / Accepted=已接受（禁用）。</summary>
        private static void OnWindowButton()
        {
            if (State == RankedMatchState.Matched) Accept();
            else if (State == RankedMatchState.Searching) Cancel();
        }

        /// <summary>浮窗内容刷新（计时走字/阶段文案/按钮态）。
        /// 每次置顶（250ms tick）：UIScreen.Mount 进屏把屏根 SetAsLastSibling——不补这句切屏后浮窗被压底。</summary>
        private static void RefreshWindow()
        {
            if (_mmLabel == null) return;
            if (_window != null) _window.SetAsLastSibling();
            switch (State)
            {
                case RankedMatchState.Searching:
                {
                    var elapsed = DateTime.UtcNow - _searchStartUtc;
                    string phase = elapsed.TotalSeconds <= 10 ? "同率优先"
                        : elapsed.TotalSeconds <= 20 ? "放宽匹配" : "全量匹配";
                    _mmLabel.text = $"已匹配 {elapsed.Minutes:00}:{elapsed.Seconds:00}（{phase}）";
                    SetWindowButton("取消", UiStyle.BtnBg, UiStyle.TextBody, interactable: true);
                    break;
                }
                case RankedMatchState.Matched:
                {
                    int left = Math.Max(0, AcceptWindowSeconds - (int)(DateTime.UtcNow - _matchedUtc).TotalSeconds);
                    _mmLabel.text = $"已匹配对手——{left} 秒内点「接受」";
                    SetWindowButton("接受", UiStyle.BtnPrimary, UiStyle.White, interactable: true);
                    break;
                }
                case RankedMatchState.Accepted:
                    _mmLabel.text = "已接受——等待对手接受…";
                    SetWindowButton("已接受", UiStyle.BtnBg, UiStyle.TextDim, interactable: false);
                    break;
            }
        }

        private static void SetWindowButton(string text, Color bg, Color fg, bool interactable)
        {
            if (_mmButtonLabel != null) _mmButtonLabel.text = text;
            if (_mmButtonImg != null) _mmButtonImg.color = bg;
            if (_mmButtonLabel != null) _mmButtonLabel.color = fg;
            if (_mmButton != null) _mmButton.interactable = interactable;
        }

        private static void CloseWindow()
        {
            if (_window != null) UnityEngine.Object.Destroy(_window.gameObject);
            _window = null;
            _mmLabel = null;
            _mmButton = null;
            _mmButtonLabel = null;
            _mmButtonImg = null;
        }
    }
}
