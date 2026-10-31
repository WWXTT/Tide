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
        /// <summary>该回合的地牌槽上限（= min(全局回合数, 7)，也是可出卡费用上限）</summary>
        public int LandCap { get; set; }
        /// <summary>实际产出元素数（主阶段手动自选色 + 结束阶段自动灰色）</summary>
        public int TapsTaken { get; set; }
        /// <summary>支付元素总量</summary>
        public int ElementsPaid { get; set; }
        /// <summary>回合开始时 bank 存量</summary>
        public int BankStart { get; set; }
        /// <summary>回合开始时池内指示物存量</summary>
        public int PoolTokensRemaining { get; set; }
        /// <summary>回合开始时场上地牌数</summary>
        public int LandsInPool { get; set; }

        /// <summary>地牌槽利用率 = 实际产出 / 上限（场地维护与用牌节奏的实证度量）</summary>
        public float LandUtilization => LandCap <= 0 ? 0f : (float)TapsTaken / LandCap;

        public override string ToString()
            => $"T{PersonalTurn} cap={LandCap} 产={TapsTaken} 付={ElementsPaid} bank={BankStart} 池余={PoolTokensRemaining} 地={LandsInPool}";
    }

    /// <summary>
    /// 资源台账：资源行为的实证度量（P0c）。
    /// 订阅元素池事件 + 回合结束事件，逐回合记录双方资源行为。
    /// 地牌槽利用率（TapsTaken/LandCap）是场地维护与用牌节奏的实测数据，
    /// 供 P3 平衡模拟、卡组编辑器软提示与后续 UI 使用。
    ///
    /// 2026-10-26 回合精准切分：开行自 TurnStartEvent 订阅改为显式口 OpenTurnRow——由
    /// GameCore.RunStandbyAutomation 在地牌槽上限推进后直调（原「订阅序保证上限已是新值」契约
    /// 改为调用点保证；TurnEnd 封行订阅不变）。
    /// </summary>
    public class ResourceLedger
    {
        private readonly ElementPoolSystem _elementPool;
        private readonly Dictionary<Player, List<ResourceTurnRecord>> _records =
            new Dictionary<Player, List<ResourceTurnRecord>>();
        private readonly Dictionary<Player, ResourceTurnRecord> _openRecords =
            new Dictionary<Player, ResourceTurnRecord>();

        public ResourceLedger(ElementPoolSystem elementPool)
        {
            _elementPool = elementPool ?? throw new ArgumentNullException(nameof(elementPool));

            var em = EventManager.Instance;
            em.Subscribe<TurnEndEvent>(OnTurnEnd);
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
        }

        // ======================================== 事件处理 ========================================

        /// <summary>开本回合资源行（GameCore.RunStandbyAutomation 在地牌槽上限推进后调用）。</summary>
        public void OpenTurnRow(Player player)
        {
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
                // 2026-10-11 现取现用：BankStart=临时钱包期初（回合开始清台账后/上回合末清空，通常 0）；
                // PoolTokensRemaining 已随指示物退役——改记未横置地牌数（可支付潜力）
                BankStart = _elementPool.GetTotalAvailableMana(player),
                PoolTokensRemaining = pool.PooledCards.Count(pc => !pc.IsTapped),
                LandsInPool = pool.PooledCards.Count,
            };

            if (!_records.TryGetValue(player, out var list))
            {
                list = new List<ResourceTurnRecord>();
                _records[player] = list;
            }
            list.Add(record);
            _openRecords[player] = record;
        }

        private void OnTurnEnd(TurnEndEvent e)
        {
            if (e.TurnPlayer == null) return;
            CloseRecord(e.TurnPlayer);
        }

        private void OnPoolPay(ElementPoolPayEvent e)
        {
            if (e.Player == null || !_openRecords.TryGetValue(e.Player, out var rec))
                return;
            if (e.PaidCost != null)
            {
                // 2026-10-11 现取现用：TapsTaken 改从支付事件计——PaidCost 含系统横置贡献
                //（横置不发 GainEvent），以 ElementsPaid 同源口径近似（每次支付≈一次系统横置批）
                rec.ElementsPaid += (int)e.PaidCost.Values.Sum();
                rec.TapsTaken++;
            }
        }

        // ======================================== 内部 ========================================

        private void CloseRecord(Player player)
        {
            // 2026-09-14：抵消使用量（OffsetsUsed）随 CostOffset 退役删除——本回合行封存只落资源产支
            _openRecords.Remove(player);
        }
    }
}
