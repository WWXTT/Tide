using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;

namespace CardCore
{
    /// <summary>手牌上限弃牌停靠门（P3 2026-11-19 §4.1，docs/AI动作空间补全_修改计划.md）：
    /// GameCore.EnforceHandLimitAsync 对**模型驱动座**改为「停靠决策」——本门挂起请求
    ///（TaskCompletionSource），RL 驱动（TideHeadlessDriver）在回合末停靠，用
    /// LegalActionEnumerator.EnumerateDiscardChoices 产 DiscardChoice 动作行交模型选，
    /// 随后 Complete 释放——EnforceHandLimitAsync 余段（入墓+事件）在 Complete 调用线程
    /// 内联继续（TCS 不带 RunContinuationsAsynchronously，防游戏态跨线程）。
    /// 非模型座（人类/SimpleAI/无头）不 Park，回落 TargetSelectionService.RequestAsync
    ///（AI 自动选前 N，旧行为）；终局/重置/强收路径调用方须 Reset/CompleteDefault 兜底防泄漏。</summary>
    public static class HandLimitDiscardGate
    {
        private static Func<Player, bool> _isDriverSeat; // 驱动注入：true=该座位由 RL 驱动（停靠决策）
        private static Player _player;
        private static List<Card> _cards;
        private static int _over;
        private static TaskCompletionSource<List<Card>> _tcs;

        /// <summary>驱动绑定（TideHeadlessDriver.Reset）：null=清绑（全程旧行为）。</summary>
        public static void Bind(Func<Player, bool> isDriverSeat) => _isDriverSeat = isDriverSeat;

        /// <summary>是否有挂起请求（驱动停靠判据）。</summary>
        public static bool HasPending => _tcs != null;

        /// <summary>尝试挂起：模型座 → 挂起并返回 true（调用方改 await WaitChosenAsync）；
        /// 非模型座/未绑定 → false（回落 RequestAsync 自动代选）。</summary>
        public static bool TryPark(Player player, List<Card> hand, int over)
        {
            if (_isDriverSeat == null || !_isDriverSeat(player) || hand == null || over <= 0) return false;
            _player = player;
            _cards = new List<Card>(hand);
            _over = over;
            _tcs = new TaskCompletionSource<List<Card>>(); // 内联续体：Complete 调用线程同步跑余段
            return true;
        }

        /// <summary>挂起快照（驱动停靠时取候选与张数建动作行）。</summary>
        public static (Player player, List<Card> cards, int over) Snapshot()
            => _tcs == null ? (null, null, 0) : (_player, _cards, _over);

        /// <summary>等待模型选择（EnforceHandLimitAsync 侧 await；Complete 释放）。</summary>
        public static async UniTask<List<Card>> WaitChosenAsync()
            => _tcs == null ? new List<Card>() : await _tcs.Task;

        /// <summary>模型已选（驱动应用 DiscardChoice 行后调用）——释放并内联跑余段。</summary>
        public static void Complete(List<Card> chosen)
        {
            var tcs = _tcs;
            Clear();
            tcs?.TrySetResult(chosen ?? new List<Card>());
        }

        /// <summary>兜底选默认组（前 over 张）——强收路径/终局释放用（防挂起泄漏死等）。</summary>
        public static void CompleteDefault()
        {
            var tcs = _tcs;
            var cards = _cards;
            var over = _over;
            Clear();
            if (tcs == null) return;
            var pick = new List<Card>();
            if (cards != null)
                for (int i = 0; i < cards.Count && pick.Count < over; i++) pick.Add(cards[i]);
            tcs.TrySetResult(pick);
        }

        /// <summary>整体复位（驱动 Reset/Dispose/终局调用；丢弃挂起请求）。</summary>
        public static void Reset()
        {
            var tcs = _tcs;
            Clear();
            tcs?.TrySetCanceled();
        }

        private static void Clear()
        {
            _tcs = null;
            _player = null;
            _cards = null;
            _over = 0;
        }
    }
}
