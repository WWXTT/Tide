using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCore
{
    /// <summary>
    /// 单回合资源记录（每玩家每「己方回合开始 → 下次回合结束」一行）。
    /// </summary>
    public class ResourceTurnRecord
    {
        public Player Player { get; set; }
        /// <summary>个人回合数（台账行标识）</summary>
        public int PersonalTurn { get; set; }
        /// <summary>该回合的地牌槽上限（= min(全局回合数, 9)，也是可出卡费用上限）</summary>
        public int LandCap { get; set; }
        /// <summary>实际产出元素数（主阶段手动自选色 + 结束阶段自动灰色）</summary>
        public int TapsTaken { get; set; }
        /// <summary>支付元素总量</summary>
        public int ElementsPaid { get; set; }
        /// <summary>抵消使用量（流失/弃牌/送墓/送额外组四类之和，本回合增量）</summary>
        public int OffsetsUsed { get; set; }
        /// <summary>回合开始时 bank 存量</summary>
        public int BankStart { get; set; }
        /// <summary>回合开始时池内指示物存量</summary>
        public int PoolTokensRemaining { get; set; }
        /// <summary>回合开始时场上地牌数</summary>
        public int LandsInPool { get; set; }

        /// <summary>地牌槽利用率 = 实际产出 / 上限（场地维护与用牌节奏的实证度量）</summary>
        public float LandUtilization => LandCap <= 0 ? 0f : (float)TapsTaken / LandCap;

        public override string ToString()
            => $"T{PersonalTurn} cap={LandCap} 产={TapsTaken} 付={ElementsPaid} 抵消={OffsetsUsed} bank={BankStart} 池余={PoolTokensRemaining} 地={LandsInPool}";
    }

    /// <summary>
    /// 资源台账：资源行为的实证度量（P0c）。
    /// 订阅元素池事件 + 回合事件，逐回合记录双方资源行为。
    /// 地牌槽利用率（TapsTaken/LandCap）是场地维护与用牌节奏的实测数据，
    /// 供 P3 平衡模拟、卡组编辑器软提示与后续 UI 使用。
    ///
    /// 注意：创建时机必须在 GameCore 订阅 TurnStart/TurnEnd 之后，
    /// 以保证开行时读到的回合数/地牌槽上限已是本回合新值，
    /// 且封行前已收到本核心结束阶段的自动产灰事件。
    /// </summary>
    public class ResourceLedger
    {
        private readonly ElementPoolSystem _elementPool;
        private readonly Dictionary<Player, List<ResourceTurnRecord>> _records =
            new Dictionary<Player, List<ResourceTurnRecord>>();
        private readonly Dictionary<Player, ResourceTurnRecord> _openRecords =
            new Dictionary<Player, ResourceTurnRecord>();
        private readonly Dictionary<Player, int> _offsetBaselines =
            new Dictionary<Player, int>();

        public ResourceLedger(ElementPoolSystem elementPool)
        {
            _elementPool = elementPool ?? throw new ArgumentNullException(nameof(elementPool));

            var em = EventManager.Instance;
            em.Subscribe<TurnStartEvent>(OnTurnStart);
            em.Subscribe<TurnEndEvent>(OnTurnEnd);
            em.Subscribe<ElementPoolGainEvent>(OnPoolGain);
            em.Subscribe<ElementPoolPayEvent>(OnPoolPay);
        }

        /// <summary>查询某玩家的全部回合记录</summary>
        public List<ResourceTurnRecord> GetRecords(Player player)
        {
            return _records.TryGetValue(player, out var list) ? list : new List<ResourceTurnRecord>();
        }

        /// <summary>查询双方全部记录（调试/转储用）</summary>
        public Dictionary<Player, List<ResourceTurnRecord>> GetAllRecords() =>
            new Dictionary<Player, List<ResourceTurnRecord>>(_records);

        /// <summary>清空（GameCore.Reset 调用）</summary>
        public void ClearAll()
        {
            _records.Clear();
            _openRecords.Clear();
            _offsetBaselines.Clear();
        }

        // ======================================== 事件处理 ========================================

        private void OnTurnStart(TurnStartEvent e)
        {
            var player = e.TurnPlayer;
            if (player == null) return;

            // 上一行已在上次 TurnEnd 封存；防御性再关一次（跳过的回合等边缘）
            CloseRecord(player);

            var pool = _elementPool.GetPool(player);
            var record = new ResourceTurnRecord
            {
                Player = player,
                PersonalTurn = pool.PersonalTurnIndex,
                LandCap = _elementPool.GetLandCap(player),
                TapsTaken = 0,
                ElementsPaid = 0,
                OffsetsUsed = 0,
                BankStart = _elementPool.GetTotalAvailableMana(player),
                PoolTokensRemaining = _elementPool.GetTotalTokensInPool(player),
                LandsInPool = pool.PooledCards.Count,
            };

            if (!_records.TryGetValue(player, out var list))
            {
                list = new List<ResourceTurnRecord>();
                _records[player] = list;
            }
            list.Add(record);
            _openRecords[player] = record;
            _offsetBaselines[player] = ReadOffsetTotal(player);
        }

        private void OnTurnEnd(TurnEndEvent e)
        {
            if (e.TurnPlayer == null) return;
            CloseRecord(e.TurnPlayer);
        }

        private void OnPoolGain(ElementPoolGainEvent e)
        {
            if (e.Player != null && _openRecords.TryGetValue(e.Player, out var rec))
                rec.TapsTaken++;
        }

        private void OnPoolPay(ElementPoolPayEvent e)
        {
            if (e.Player == null || !_openRecords.TryGetValue(e.Player, out var rec))
                return;
            if (e.PaidCost != null)
                rec.ElementsPaid += (int)e.PaidCost.Values.Sum();
        }

        // ======================================== 内部 ========================================

        private void CloseRecord(Player player)
        {
            if (!_openRecords.TryGetValue(player, out var rec))
                return;
            rec.OffsetsUsed = ReadOffsetTotal(player) - _offsetBaselines.GetValueOrDefault(player, 0);
            _openRecords.Remove(player);
        }

        /// <summary>读玩家四类抵消已用计数之和</summary>
        private static int ReadOffsetTotal(Player player)
        {
            return player.OffsetDrainUsed + player.OffsetDiscardUsed
                 + player.OffsetMillUsed + player.OffsetSendExtraUsed;
        }
    }
}
