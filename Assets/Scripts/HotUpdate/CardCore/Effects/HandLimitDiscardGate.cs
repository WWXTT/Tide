using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;

namespace CardCore
{
    /// <summary>手牌上限弃牌停靠门（P3 2026-11-19 §4.1 建·docs/AI动作空间补全_修改计划.md；
    /// A0 2026-11-24 回到一期训练目标改**逐张弃**）：GameCore.EnforceHandLimitAsync 对
    /// **模型驱动座**走有界循环——每轮经本门挂起 1 次请求（TaskCompletionSource），RL 驱动
    ///（TideHeadlessDriver）在循环顶停靠，用 LegalActionEnumerator.EnumerateDiscardChoices 产
    ///「每张手牌一行」的 DiscardChoice 动作行交模型选 1 张，随后 Complete 释放——余段
    ///（单卡入墓+事件）在 Complete 调用线程内联继续（TCS 不带 RunContinuationsAsynchronously，
    /// 防游戏态跨线程），仍超上限则再挂起（连续弃牌步，9 张/上限 7 → 恰 2 步）。
    /// 非模型座（人类/SimpleAI/无头）不 Park，回落 TargetSelectionService.RequestAsync
    ///（AI 自动选前 N，一次选完旧行为）；终局/重置/强收路径调用方须 Reset/CompleteDefault 兜底防泄漏。</summary>
    public static class HandLimitDiscardGate
    {
        private static Func<Player, bool> _isDriverSeat; // 驱动注入：true=该座位由 RL 驱动（停靠决策）
        private static Player _player;
        private static List<Card> _cards;
        private static int _over;
        private static TaskCompletionSource<List<Card>> _tcs;

        /// <summary>兜底放行轮次硬上限（A0 2026-11-24）：原弃牌组合行上限 24（MaxDiscardRows，
        /// 随「一次选完」旧语义退役）转用——正常局至多放行「手牌数−上限」轮，到顶即异常态。</summary>
        private const int MaxRounds = 24;

        /// <summary>驱动绑定（TideHeadlessDriver.Reset）：null=清绑（全程旧行为）。</summary>
        public static void Bind(Func<Player, bool> isDriverSeat) => _isDriverSeat = isDriverSeat;

        /// <summary>非消费探针：该座位当前是否模型驱动座——EnforceHandLimitAsync 用它在
        /// 「逐张弃有界循环」与「一次选完 RequestAsync」之间选路（A0 2026-11-24）。</summary>
        public static bool IsDriverSeat(Player player)
            => _isDriverSeat != null && player != null && _isDriverSeat(player);

        /// <summary>是否有挂起请求（驱动停靠判据）。</summary>
        public static bool HasPending => _tcs != null;

        /// <summary>尝试挂起：模型座 → 挂起并返回 true（调用方改 await WaitChosenAsync）；
        /// 非模型座/未绑定 → false（回落 RequestAsync 自动代选）。A0（2026-11-24 逐张弃）后
        /// over 仅作「剩余待弃张数」元数据（快照/日志/观测 g[99]）——单步选择恒 1 张，
        /// 由 EnforceHandLimitAsync 有界循环逐张推进。</summary>
        public static bool TryPark(Player player, List<Card> hand, int over)
        {
            if (_isDriverSeat == null || !_isDriverSeat(player) || hand == null || over <= 0) return false;
            _player = player;
            _cards = new List<Card>(hand);
            _over = over;
            _tcs = new TaskCompletionSource<List<Card>>(); // 内联续体：Complete 调用线程同步跑余段
            return true;
        }

        /// <summary>挂起快照（驱动停靠时取候选与张数建动作行；over=剩余待弃张数）。</summary>
        public static (Player player, List<Card> cards, int over) Snapshot()
            => _tcs == null ? (null, null, 0) : (_player, _cards, _over);

        /// <summary>等待模型选择（EnforceHandLimitAsync 侧 await；Complete 释放）。</summary>
        public static async UniTask<List<Card>> WaitChosenAsync()
            => _tcs == null ? new List<Card>() : await _tcs.Task;

        /// <summary>模型已选（驱动应用 DiscardChoice 行后调用）——释放并内联跑余段。
        /// A0（2026-11-24 逐张弃）后应只传 1 张；空/无效选集由引擎兜底（弃有效手牌第一张）。</summary>
        public static void Complete(List<Card> chosen)
        {
            var tcs = _tcs;
            Clear();
            tcs?.TrySetResult(chosen ?? new List<Card>());
        }

        /// <summary>兜底放行（A0 2026-11-24 逐张弃：每轮放行快照首卡）——强收路径/终局释放用
        ///（防挂起泄漏死等）。释放一轮后余段可能再次挂起（仍超上限），循环放行直到不再挂起；
        /// 轮次硬上限防异常态空转，触顶留日志并整体复位。</summary>
        public static void CompleteDefault()
        {
            for (int round = 0; round < MaxRounds && _tcs != null; round++)
            {
                var tcs = _tcs;
                var cards = _cards;
                Clear();
                var pick = new List<Card>();
                if (cards != null && cards.Count > 0) pick.Add(cards[0]);
                tcs?.TrySetResult(pick);
            }
            if (_tcs != null)
            {
                TideLog.Warn("[HandLimitDiscardGate] CompleteDefault 达轮次硬上限，整体复位（异常态防御）");
                Reset();
            }
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
