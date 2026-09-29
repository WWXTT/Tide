using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq;

namespace CardCore
{
    /// <summary>
    /// 时间戳系统
    /// 负责分配和管理实体、效果的时间戳
    /// 参考MTG：先入场的实体（更老的）优先
    /// </summary>
    public static class TimestampSystem
    {
        private static uint _sequence = 0;
        private static DateTime _baseTime = DateTime.Now;
        private static readonly object _lock = new object();

        /// <summary>
        /// 获取下一个序列号（线程安全）
        /// </summary>
        public static uint NextSequence
        {
            get
            {
                lock (_lock)
                {
                    return ++_sequence;
                }
            }
        }

        /// <summary>
        /// 当前序列号（不递增）
        /// </summary>
        public static uint CurrentSequence => _sequence;

        /// <summary>
        /// 当前时间
        /// </summary>
        public static DateTime Now => DateTime.Now;

        /// <summary>
        /// 系统基准时间
        /// </summary>
        public static DateTime BaseTime => _baseTime;

        /// <summary>
        /// 创建一个新的时间戳
        /// </summary>
        public static TimestampInfo CreateTimestamp()
        {
            return new TimestampInfo
            {
                DateTime = Now,
                Sequence = NextSequence
            };
        }

        /// <summary>
        /// 创建指定序列号的时间戳（用于复制等情况）
        /// </summary>
        public static TimestampInfo CreateTimestampWithSequence(uint sequence)
        {
            return new TimestampInfo
            {
                DateTime = Now,
                Sequence = sequence
            };
        }

        /// <summary>
        /// 创建继承时间戳（复制卡时继承原卡时间戳）
        /// </summary>
        public static TimestampInfo CreateInheritedTimestamp(TimestampInfo original)
        {
            return new TimestampInfo
            {
                DateTime = Now,
                Sequence = NextSequence,
                IsInherited = true,
                OriginalSequence = original.EffectiveSequence,
                OriginalDateTime = original.EffectiveDateTime
            };
        }

        /// <summary>
        /// 比较两个时间戳
        /// 返回值：
        /// > 0 : a 比 b 老（先入场）
        /// = 0 : 相同
        /// < 0 : b 比 a 老
        /// </summary>
        public static int CompareTimestamps(TimestampInfo a, TimestampInfo b)
        {
            if (a == b) return 0;

            // 先比较序列号（精确）
            uint aSeq = a.EffectiveSequence;
            uint bSeq = b.EffectiveSequence;

            if (aSeq != bSeq)
                return bSeq.CompareTo(aSeq);

            // 序列号相同则比较时间（备用）
            DateTime aTime = a.EffectiveDateTime;
            DateTime bTime = b.EffectiveDateTime;

            return bTime.CompareTo(aTime);
        }

        /// <summary>
        /// 比较两个实体的时间戳
        /// </summary>
        public static int CompareEntities(ITimestamped a, ITimestamped b)
        {
            if (a == null || b == null) return 0;
            if (a == b) return 0;
            return CompareTimestamps(a.TimestampInfo, b.TimestampInfo);
        }

        /// <summary>
        /// 按时间戳排序实体（从老到新，即先入场的排前面）
        /// </summary>
        public static IEnumerable<T> SortByTimestamp<T>(IEnumerable<T> entities) where T : ITimestamped
        {
            return entities.OrderBy(e => e.TimestampInfo.Sequence).ThenBy(e => e.TimestampInfo.DateTime);
        }

        /// <summary>
        /// 按时间戳排序实体（从新到老，即后入场的排前面）
        /// </summary>
        public static IEnumerable<T> SortByTimestampDescending<T>(IEnumerable<T> entities) where T : ITimestamped
        {
            return entities.OrderByDescending(e => e.TimestampInfo.Sequence).ThenByDescending(e => e.TimestampInfo.DateTime);
        }

        /// <summary>
        /// 获取集合中最老的实体（先入场）
        /// </summary>
        public static T GetOldest<T>(IEnumerable<T> entities) where T : ITimestamped
        {
            return entities.OrderBy(e => e.TimestampInfo.Sequence).First();
        }

        /// <summary>
        /// 获取集合中最新的实体（后入场）
        /// </summary>
        public static T GetNewest<T>(IEnumerable<T> entities) where T : ITimestamped
        {
            return entities.OrderBy(e => e.TimestampInfo.Sequence).Last();
        }

        /// <summary>
        /// 检查两个时间戳是否在同一回合
        /// </summary>
        public static bool IsSameTurn(TimestampInfo a, TimestampInfo b, int currentTurnNumber, TimeSpan turnDuration)
        {
            // 简化实现：基于时间间隔判断
            var timeDiff = Math.Abs((a.DateTime - b.DateTime).TotalMilliseconds);
            return timeDiff <= turnDuration.TotalMilliseconds;
        }

        /// <summary>
        /// 计算时间戳与当前时间的间隔
        /// </summary>
        public static TimeSpan ElapsedSince(TimestampInfo timestamp)
        {
            return Now - timestamp.DateTime;
        }

        /// <summary>
        /// 重置时间戳系统（测试用）
        /// </summary>
        public static void Reset()
        {
            lock (_lock)
            {
                _sequence = 0;
                _baseTime = DateTime.Now;
            }
        }

        /// <summary>
        /// 获取系统统计信息
        /// </summary>
        public static TimestampStats GetStats()
        {
            return new TimestampStats
            {
                TotalSequenceIssued = _sequence,
                RunningTime = Now - _baseTime,
                CurrentTime = Now
            };
        }
    }

    /// <summary>
    /// 时间戳统计信息
    /// </summary>
    public struct TimestampStats
    {
        public uint TotalSequenceIssued { get; set; }
        public TimeSpan RunningTime { get; set; }
        public DateTime CurrentTime { get; set; }

        public override string ToString()
        {
            return $"TimestampStats: Sequences={TotalSequenceIssued}, Running={RunningTime:hh\\:mm\\:ss}";
        }
    }

    /// <summary>
    /// 时间戳信息
    /// </summary>
    public struct TimestampInfo : IEquatable<TimestampInfo>, IComparable<TimestampInfo>
    {
        public DateTime DateTime;
        public uint Sequence;
        public bool IsInherited { get; set; }

        /// <summary>
        /// 原始序列号（用于继承时间戳）
        /// </summary>
        public uint OriginalSequence { get; set; }

        /// <summary>
        /// 原始创建时间（用于继承时间戳）
        /// </summary>
        public DateTime OriginalDateTime { get; set; }

        /// <summary>
        /// 是否有原始时间戳
        /// </summary>
        public bool HasOriginalTimestamp => OriginalSequence > 0;

        public int CompareTo(TimestampInfo other)
        {
            return TimestampSystem.CompareTimestamps(this, other);
        }

        public bool Equals(TimestampInfo other)
        {
            return Sequence == other.Sequence && DateTime == other.DateTime;
        }

        public override bool Equals(object obj)
        {
            return obj is TimestampInfo other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Sequence, DateTime);
        }

        /// <summary>
        /// 获取有效序列号（继承的时间戳返回原始序列号）
        /// </summary>
        public uint EffectiveSequence => IsInherited && HasOriginalTimestamp
            ? OriginalSequence
            : Sequence;

        /// <summary>
        /// 获取有效创建时间（继承的时间戳返回原始创建时间）
        /// </summary>
        public DateTime EffectiveDateTime => IsInherited && HasOriginalTimestamp
            ? OriginalDateTime
            : DateTime;

        /// <summary>
        /// 时间戳描述
        /// </summary>
        public string Description => IsInherited
            ? $"Inherited(Seq:{EffectiveSequence}, {EffectiveDateTime:HH:mm:ss.fff})"
            : $"Seq:{Sequence}, {DateTime:HH:mm:ss.fff}";

        public static bool operator ==(TimestampInfo left, TimestampInfo right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TimestampInfo left, TimestampInfo right)
        {
            return !left.Equals(right);
        }

        public static bool operator <(TimestampInfo left, TimestampInfo right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator <=(TimestampInfo left, TimestampInfo right)
        {
            return left.CompareTo(right) <= 0;
        }

        public static bool operator >(TimestampInfo left, TimestampInfo right)
        {
            return left.CompareTo(right) > 0;
        }

        public static bool operator >=(TimestampInfo left, TimestampInfo right)
        {
            return left.CompareTo(right) >= 0;
        }
    }

    /// <summary>
    /// 带时间戳的实体接口
    /// </summary>
    public interface ITimestamped
    {
        TimestampInfo TimestampInfo { get; }
        DateTime CreationTime { get; }
        uint SequenceNumber { get; }
    }

    /// <summary>
    /// 时间戳实体扩展方法
    /// </summary>
    public static class TimestampedExtensions
    {
        /// <summary>
        /// 检查实体是否比另一个更老（先入场）
        /// </summary>
        public static bool IsOlderThan(this ITimestamped entity, ITimestamped other)
        {
            if (entity == null || other == null) return false;
            if (entity == other) return false;
            return TimestampSystem.CompareEntities(entity, other) > 0;
        }

        /// <summary>
        /// 检查实体是否比另一个更新（后入场）
        /// </summary>
        public static bool IsNewerThan(this ITimestamped entity, ITimestamped other)
        {
            if (entity == null || other == null) return false;
            if (entity == other) return false;
            return TimestampSystem.CompareEntities(entity, other) < 0;
        }

        /// <summary>
        /// 检查两个实体是否同时入场
        /// </summary>
        public static bool IsSameAge(this ITimestamped entity, ITimestamped other)
        {
            if (entity == null || other == null) return false;
            return TimestampSystem.CompareEntities(entity, other) == 0;
        }

        /// <summary>
        /// 获取实体的有效序列号
        /// </summary>
        public static uint GetEffectiveSequence(this ITimestamped entity)
        {
            if (entity == null) return 0;
            return entity.TimestampInfo.EffectiveSequence;
        }

        /// <summary>
        /// 获取实体的有效创建时间
        /// </summary>
        public static DateTime GetEffectiveCreationTime(this ITimestamped entity)
        {
            if (entity == null) return DateTime.MinValue;
            return entity.TimestampInfo.EffectiveDateTime;
        }

        /// <summary>
        /// 检查实体是否继承了时间戳（复制卡等情况）
        /// </summary>
        public static bool HasInheritedTimestamp(this ITimestamped entity)
        {
            if (entity == null) return false;
            return entity.TimestampInfo.IsInherited;
        }

        /// <summary>
        /// 计算实体存在的时间跨度
        /// </summary>
        public static TimeSpan Age(this ITimestamped entity)
        {
            if (entity == null) return TimeSpan.Zero;
            return TimestampSystem.ElapsedSince(entity.TimestampInfo);
        }

        /// <summary>
        /// 从集合中筛选出比指定实体更老的实体
        /// </summary>
        public static IEnumerable<T> OlderThan<T>(this IEnumerable<T> entities, ITimestamped reference) where T : ITimestamped
        {
            if (reference == null) return entities;
            return entities.Where(e => e.IsOlderThan(reference));
        }

        /// <summary>
        /// 从集合中筛选出比指定实体更新的实体
        /// </summary>
        public static IEnumerable<T> NewerThan<T>(this IEnumerable<T> entities, ITimestamped reference) where T : ITimestamped
        {
            if (reference == null) return entities;
            return entities.Where(e => e.IsNewerThan(reference));
        }

        /// <summary>
        /// 从集合中筛选出与指定实体同时入场的实体
        /// </summary>
        public static IEnumerable<T> SameAgeAs<T>(this IEnumerable<T> entities, ITimestamped reference) where T : ITimestamped
        {
            if (reference == null) return entities;
            return entities.Where(e => e.IsSameAge(reference));
        }

        /// <summary>
        /// 检查集合中是否包含比指定实体更老的实体
        /// </summary>
        public static bool HasOlder<T>(this IEnumerable<T> entities, ITimestamped reference) where T : ITimestamped
        {
            if (reference == null) return false;
            return entities.Any(e => e.IsOlderThan(reference));
        }

        /// <summary>
        /// 检查集合中是否包含比指定实体更新的实体
        /// </summary>
        public static bool HasNewer<T>(this IEnumerable<T> entities, ITimestamped reference) where T : ITimestamped
        {
            if (reference == null) return false;
            return entities.Any(e => e.IsNewerThan(reference));
        }

        /// <summary>
        /// 获取实体在集合中的排序位置（基于时间戳）
        /// </summary>
        public static int GetTimestampRank<T>(this IEnumerable<T> entities, ITimestamped target) where T : class, ITimestamped
        {
            if (target == null) return -1;
            var sorted = TimestampSystem.SortByTimestamp(entities).ToList();
            return sorted.IndexOf(target as T);
        }
    }
}
