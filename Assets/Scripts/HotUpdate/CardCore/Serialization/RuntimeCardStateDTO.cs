using System;
using System.Collections.Generic;
using System.Linq;
using MemoryPack;

namespace CardCore.Serialization
{
    [MemoryPackable]
    public partial class SerializableRuntimeCardState
    {
        [MemoryPackOrder(TagTable.RCS_ID)]
        public string ID;

        [MemoryPackOrder(TagTable.RCS_Power)]
        public int Power;

        [MemoryPackOrder(TagTable.RCS_Life)]
        public int Life;

        [MemoryPackOrder(TagTable.RCS_MaxLife)]
        public int MaxLife;

        [MemoryPackOrder(TagTable.RCS_BaseCost)]
        public int BaseCost;

        [MemoryPackOrder(TagTable.RCS_CostModifier)]
        public int CostModifier;

        [MemoryPackOrder(TagTable.RCS_IsTapped)]
        public bool IsTapped;

        [MemoryPackOrder(TagTable.RCS_IsNegated)]
        public bool IsNegated;

        [MemoryPackOrder(TagTable.RCS_IsNullified)]
        public bool IsNullified;

        [MemoryPackOrder(TagTable.RCS_Zone)]
        public int Zone;

        [MemoryPackOrder(TagTable.RCS_Keywords)]
        public string[] Keywords;

        [MemoryPackOrder(TagTable.RCS_Counters)]
        public CounterEntryDTO[] Counters;

        /// <summary>地牌锁定产色（2026-10-11 资源重做：指示物/耗竭字段退役）。
        /// -1=非地牌/未锁色；地牌权威态在 PooledCard 包装，入池即由快照侧覆盖（FromCard 恒写 -1）。</summary>
        [MemoryPackOrder(TagTable.RCS_LockedColor)]
        public int LockedColor = -1;

        /// <summary>建造已建额（2026-10-29 P7）——**恒空 legacy 字段**（2026-10-31 P12 建造→充能退役：
        /// 卡实例 BuildPaid 已删，FromCard 恒写全零、ApplyToCard 不再回灌；保留字段防协议断裂，
        /// 充能进度经 Counters（CostDownGray/Red/Blue/Green + Exposed）承载，CounterEntryDTO 已覆盖）。</summary>
        [MemoryPackOrder(TagTable.RCS_BuildPaid)]
        public float[] BuildPaid;

        /// <summary>延迟死亡标记（2026-10-29 阶段3b，DelayedDeathAura 改写光环）：本命已用一次延迟
        /// 改写——到点 TryKill 不再改写（防无限延迟）；换区/真实死亡由引擎清（OnZoneChanged/TryKill）。
        /// FromCard 从卡实例 _delayedDeath 导出；ApplyToCard 回灌（回放/对拍确定性——任务书 §4）。</summary>
        [MemoryPackOrder(TagTable.RCS_DelayedDeath)]
        public bool DelayedDeath;

        // ---- M1 网络快照扩展（2026-09-10）：单向导出字段，ApplyToCard 不回灌 ----
        // RuntimeId 只读（Entity 构造期分配）；ControllerSeat 经 SetController 走 Player 对象（DTO 无核心上下文）；
        // IsFrozen 是计算值（FreezeCounter>0），回灌须走指示物写入——三者均为快照展示/对账口径。

        [MemoryPackOrder(TagTable.RCS_RuntimeId)]
        public uint RuntimeId;

        [MemoryPackOrder(TagTable.RCS_ControllerSeat)]
        public int ControllerSeat;

        [MemoryPackOrder(TagTable.RCS_IsFrozen)]
        public bool IsFrozen;

        public static SerializableRuntimeCardState FromCard(Card card)
        {
            var dto = new SerializableRuntimeCardState
            {
                ID = card.ID,
                Power = card._power,
                Life = card._life,
                MaxLife = card._maxLife,
                BaseCost = card._baseCost,
                CostModifier = card._costModifier,
                IsTapped = card._isTapped,
                IsNegated = card._isNegated,
                IsNullified = card._isNullified,
                Zone = (int)card._zone,
                RuntimeId = card.RuntimeId,
                ControllerSeat = SeatOfController(card),
                IsFrozen = card.IsFrozen(),
                LockedColor = -1, // 地牌锁色权威态在 PooledCard 包装——快照侧 BuildZone 覆盖
                DelayedDeath = card._delayedDeath,
            };

            // 建造已建额 legacy（2026-10-31 P12）：恒写全零（卡实例 BuildPaid 已删；充能进度走 Counters）
            dto.BuildPaid = new float[ElementCost.Length];

            dto.Keywords = card._keywords?.ToArray() ?? Array.Empty<string>();
            dto.Counters = card._counters?.Select(kvp => new CounterEntryDTO { Key = kvp.Key, Value = kvp.Value }).ToArray()
                           ?? Array.Empty<CounterEntryDTO>();

            return dto;
        }

        /// <summary>控制器座位（P1=0/P2=1/其他=-1）。快照对账口径；回灌走 SetController 不经 DTO。</summary>
        private static int SeatOfController(Card card)
        {
            var controller = card.GetController();
            var core = GameCore.Instance;
            if (controller == null || core == null) return -1;
            if (ReferenceEquals(controller, core.Player1)) return 0;
            if (ReferenceEquals(controller, core.Player2)) return 1;
            return -1;
        }

        public void ApplyToCard(Card card)
        {
            card.ID = ID;
            card._power = Power;
            card._life = Life;
            card._maxLife = MaxLife;
            card._baseCost = BaseCost;
            card._costModifier = CostModifier;
            card._isTapped = IsTapped;
            card._isNegated = IsNegated;
            card._isNullified = IsNullified;
            card._zone = (Zone)Zone;
            card._delayedDeath = DelayedDeath;

            if (Keywords != null)
            {
                card._keywords.Clear();
                foreach (var kw in Keywords)
                    card._keywords.Add(kw);
            }

            if (Counters != null)
            {
                card._counters.Clear();
                foreach (var c in Counters)
                    card._counters[c.Key] = c.Value;
            }

            // 建造已建额回灌已删（2026-10-31 P12：卡实例 BuildPaid 已删——legacy 字段不再读写，
            // 旧端载荷中非零值静默丢弃；充能进度经上方 Counters 回灌）
        }
    }

    [MemoryPackable]
    public partial class CounterEntryDTO
    {
        [MemoryPackOrder(TagTable.CounterEntryDTO_Key)]
        public string Key;

        [MemoryPackOrder(TagTable.CounterEntryDTO_Value)]
        public int Value;
    }

    [MemoryPackable]
    public partial class ManaEntryDTO
    {
        [MemoryPackOrder(TagTable.ManaEntryDTO_ManaType)]
        public int ManaType;

        [MemoryPackOrder(TagTable.ManaEntryDTO_Value)]
        public int Value;
    }
}