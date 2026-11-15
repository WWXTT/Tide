using System;
using System.Collections.Generic;
using System.Linq;
using CardCore.Serialization;

namespace CardCore
{
    /// <summary>
    /// 回合起点全量快照（2026-10-26 回合精准切分定案）：TurnEngine.StartNewTurn 在发布 TurnStartEvent 前
    /// 采集——回合开始=纯系统区 + 池子边界，为「跳过阶段装配 / 回合结束获得新回合 / 重新开始本回合（未做）」
    /// 提供边界快照。采集=纯只读（零事件、零行为影响）。
    ///
    /// 采集范围（全量可逆口径）：
    /// - 双 RNG 流检查点：GameRng（效果随机）+ ZoneContainer（洗牌流）——种子+已消耗调用数；
    ///   未钉种子的环境流记录调用数但不可重播（与 M3 对拍同前提：可重播要求钉种子开局）。
    /// - TurnEngine：额外回合计数、三组跳过标记（Whole/Standby/Main，按座位）。
    /// - 双方 Player：生命/疲劳/指示物(含施加来源座位)/关键词真身+授予台账/英雄技能卡(含横置态)/IsAI。
    /// - 双方全区域有序卡表（含牌库序=index0 牌库顶）+ 每卡 SerializableRuntimeCardState
    ///   （攻血/费用/横置/无效化/关键词/指示物——卡级可逆件，恢复口 ApplyToCard 已预留）。
    /// - 双方 ElementPool：地牌池（源卡 RuntimeId/锁色/横置）/临时钱包/曲线指数与台账/LandCapBonus/曲线类型。
    /// - 边界不变量：栈空 + 待发空（StartNewTurn 只从 End 空栈折返进入；违反记观测值不阻断）。
    ///
    /// 恢复（重开本回合，未做——策略文档化，随该功能立项实现）：
    /// - RNG 双流：GameRng.Restore / ZoneContainer.RestoreShuffle（同种子重播+按调用数快进）。
    /// - 区域与卡态：卡实例不重建（保注册表引用图）——ApplyToCard 回灌 + 区域容器按有序清单静默重排
    ///  （不发区域事件——Zones.cs 区域真源注释已为快照恢复预留静默重建例外）。
    /// - 注册表/台账类（触发注册·层效果·持续时长·延迟效果·守护·预言待验证·规则光环·引擎载荷）：
    ///   派生态不逐表快照——恢复=清空 + 按战场卡确定性重注册（注册口幂等），回合作用域台账随边界重建。
    /// </summary>
    public sealed class TurnStartSnapshot
    {
        // ---- Meta ----
        public int TurnNumber;
        public int TurnPlayerSeat = -1;              // 0=P1 / 1=P2
        public RngCheckpoint EffectRng;              // GameRng 效果流
        public RngCheckpoint ShuffleRng;             // ZoneContainer 洗牌流
        public int StackSizeAtCapture;               // 边界不变量观测（期望 0）
        public bool HasPendingEffectsAtCapture;      // 同上（期望 false）
        public DateTime CapturedAtUtc;

        // ---- TurnEngine 装配态 ----
        public int ExtraTurnsPending;
        public int[] SkipWholeSeats = Array.Empty<int>();
        public int[] SkipStandbySeats = Array.Empty<int>();
        public int[] SkipMainSeats = Array.Empty<int>();
        public int[] SkipEndSeats = Array.Empty<int>();   // 跳过结束阶段档（2026-10-29 阶段3a）

        // ---- 双方 ----
        public PlayerSnapshot P1;
        public PlayerSnapshot P2;

        /// <summary>玩家侧快照（角色 + 全区域 + 元素池）。</summary>
        public sealed class PlayerSnapshot
        {
            public int Seat = -1;
            public string Name;

            // 角色
            public int Life;
            public int FatigueCount;
            public bool IsAI;
            public Dictionary<string, int> Counters = new Dictionary<string, int>();
            public Dictionary<string, int> CounterSourceSeats = new Dictionary<string, int>(); // 来源座位（-1=无/非玩家）
            public List<string> Keywords = new List<string>();
            public List<KeywordGrant> KeywordGrants = new List<KeywordGrant>(); // 台账条目实例复用（capture-only）
            public uint? HeroSkillCardRuntimeId;
            public bool HeroSkillTapped;

            // 全区域（有序；牌库序即牌库列表序）
            public Dictionary<Zone, List<uint>> ZoneRuntimeIds = new Dictionary<Zone, List<uint>>();
            public List<SerializableRuntimeCardState> Cards = new List<SerializableRuntimeCardState>();

            // 元素池
            public PoolSnapshot Pool = new PoolSnapshot();
        }

        /// <summary>元素池快照（地牌池权威态：锁色/横置在 PooledCard 包装，不在卡 DTO）。</summary>
        public sealed class PoolSnapshot
        {
            public List<PooledCardEntry> Pooled = new List<PooledCardEntry>();
            public Dictionary<int, int> TempElements = new Dictionary<int, int>(); // (int)ManaType → 数量
            public int GlobalTurnIndex;
            public int PersonalTurnIndex;
            public int LandCapBonus;
            public int TapsThisTurn;
            public string CurveTypeName; // 观测口径（恢复经 SetCurve 重建，非反序列化）

            public sealed class PooledCardEntry
            {
                public uint SourceRuntimeId;
                public int LockedColor;
                public bool IsTapped;
                /// <summary>冻地层数（2026-10-29 阶段3a）：剩余回合数——池内包装态，与锁色/横置同源采集。</summary>
                public int FreezeLayers;
            }
        }

        // ======================================== 采集 ========================================

        /// <summary>纯只读采集（TurnEngine.StartNewTurn 边界调用；零事件）。</summary>
        public static TurnStartSnapshot Capture(GameCore core)
        {
            if (core == null) return null;
            var snap = new TurnStartSnapshot
            {
                CapturedAtUtc = DateTime.UtcNow,
                EffectRng = GameRng.Capture(),
                ShuffleRng = ZoneContainer.CaptureShuffle(),
                StackSizeAtCapture = core.StackEngine?.StackSize ?? 0,
                HasPendingEffectsAtCapture = core.StackEngine?.HasPendingEffects ?? false,
            };

            var te = core.TurnEngine;
            if (te != null)
            {
                snap.TurnNumber = te.TurnNumber;
                snap.TurnPlayerSeat = SeatOf(core, te.TurnPlayer);
                var markers = te.CaptureTurnState(core);
                snap.ExtraTurnsPending = markers.extraTurns;
                snap.SkipWholeSeats = markers.whole;
                snap.SkipStandbySeats = markers.standby;
                snap.SkipMainSeats = markers.main;
                snap.SkipEndSeats = markers.end;
            }

            snap.P1 = CapturePlayer(core, core.Player1, 0);
            snap.P2 = CapturePlayer(core, core.Player2, 1);
            return snap;
        }

        private static PlayerSnapshot CapturePlayer(GameCore core, Player player, int seat)
        {
            var ps = new PlayerSnapshot { Seat = seat, Name = player?.Name };
            if (player == null) return ps;

            ps.Life = player.Life;
            ps.FatigueCount = player.FatigueCount;
            ps.IsAI = player.IsAI;
            foreach (var kvp in player._counters)
            {
                ps.Counters[kvp.Key] = kvp.Value;
                ps.CounterSourceSeats[kvp.Key] = SeatOf(core, player._counterSources.GetValueOrDefault(kvp.Key));
            }
            ps.Keywords = new List<string>(player._keywords);
            ps.KeywordGrants = new List<KeywordGrant>(player._keywordGrants);
            if (player.HeroSkillCard != null)
            {
                ps.HeroSkillCardRuntimeId = player.HeroSkillCard.RuntimeId;
                ps.HeroSkillTapped = player.HeroSkillCard.IsTapped();
            }

            var container = core.ZoneManager.GetZoneContainer(player);
            if (container != null)
            {
                foreach (Zone zone in Enum.GetValues(typeof(Zone)))
                {
                    var cards = container.GetCards(zone); // 副本（快照稳定）
                    if (cards.Count == 0) continue;
                    ps.ZoneRuntimeIds[zone] = cards.Select(c => c.RuntimeId).ToList();
                    foreach (var card in cards)
                        ps.Cards.Add(SerializableRuntimeCardState.FromCard(card));
                }
            }

            var pool = core.ElementPool.GetPool(player);
            ps.Pool.GlobalTurnIndex = pool.GlobalTurnIndex;
            ps.Pool.PersonalTurnIndex = pool.PersonalTurnIndex;
            ps.Pool.LandCapBonus = pool.LandCapBonus;
            ps.Pool.TapsThisTurn = pool.TapsThisTurn;
            ps.Pool.CurveTypeName = pool.Curve?.GetType().Name;
            foreach (var pc in pool.PooledCards)
                ps.Pool.Pooled.Add(new PoolSnapshot.PooledCardEntry
                {
                    SourceRuntimeId = pc.SourceCard.RuntimeId,
                    LockedColor = (int)pc.LockedColor,
                    IsTapped = pc.IsTapped,
                    FreezeLayers = pc.FreezeLayers,
                });
            foreach (var kvp in pool.TempElements)
                if (kvp.Value != 0) ps.Pool.TempElements[(int)kvp.Key] = kvp.Value;

            return ps;
        }

        private static int SeatOf(GameCore core, Entity entity)
        {
            if (entity == null || core == null) return -1;
            if (ReferenceEquals(entity, core.Player1)) return 0;
            if (ReferenceEquals(entity, core.Player2)) return 1;
            return -1;
        }
    }
}
