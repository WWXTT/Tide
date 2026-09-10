using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CardCore.Serialization;
using MemoryPack;

namespace CardCore.Network
{
    /// <summary>事件字段投影形态（M1 协议定版 2026-09-10）。</summary>
    public enum NetParamKind : int
    {
        /// <summary>字段值为 null（显式表达，与空串/0 区分）。</summary>
        Null = 0,
        /// <summary>bool/sbyte..ulong/DateTime(Ticks)——数值放 IntValue。</summary>
        Int = 1,
        /// <summary>float/double——数值放 FloatValue。</summary>
        Float = 2,
        /// <summary>字符串——放 StringValue。</summary>
        String = 3,
        /// <summary>枚举——IntValue=数值、StringValue=枚举名（双表达便于客户端显示）。</summary>
        Enum = 4,
        /// <summary>单个实体（Card/Player）——EntityRefs[0]。</summary>
        Entity = 5,
        /// <summary>实体集合（List&lt;Card&gt;/List&lt;Entity&gt; 等）——EntityRefs[N]（含混合元素时整字段降级 Opaque）。</summary>
        EntityList = 6,
        /// <summary>嵌套事件引用（TriggerEvent.OriginalEvent 等）——IntValue=其 EventId。不递归投影（防环+避免重复：嵌套事件此前已单独投过）。</summary>
        EventRef = 7,
        /// <summary>不可结构化对象（Effect/EffectInstance/Context 等）——StringValue=ToString()，仅展示用途；客户端真身以快照为准。</summary>
        Opaque = 8,
    }

    /// <summary>事件字段投影（schema-less：FieldName 与事件 CLR 属性同名，新增字段对旧客户端透明）。</summary>
    [MemoryPackable]
    public partial class NetParam
    {
        [MemoryPackOrder(TagTable.NP_FieldName)]
        public string FieldName;

        [MemoryPackOrder(TagTable.NP_Kind)]
        public NetParamKind Kind;

        [MemoryPackOrder(TagTable.NP_IntValue)]
        public long IntValue;

        [MemoryPackOrder(TagTable.NP_FloatValue)]
        public double FloatValue;

        [MemoryPackOrder(TagTable.NP_StringValue)]
        public string StringValue;

        [MemoryPackOrder(TagTable.NP_EntityRefs)]
        public NetEntityRef[] EntityRefs;
    }

    /// <summary>通用事件信封：EventType=CLR 类型名（跨版本稳定）；EventId 取自 GameEventBase 自增。</summary>
    [MemoryPackable]
    public partial class NetEvent
    {
        [MemoryPackOrder(TagTable.NE_EventType)]
        public string EventType;

        [MemoryPackOrder(TagTable.NE_EventId)]
        public uint EventId;

        [MemoryPackOrder(TagTable.NE_TurnNumber)]
        public int TurnNumber;

        [MemoryPackOrder(TagTable.NE_Params)]
        public NetParam[] Params;
    }

    /// <summary>事件流批次（下行）。全信息内部流——按座位过滤发生在服务器出队时（M2），投影器本身不做隐藏信息裁剪。</summary>
    [MemoryPackable]
    public partial class MsgNetEventBatch
    {
        [MemoryPackOrder(TagTable.MNEB_Events)]
        public NetEvent[] Events;
    }

    /// <summary>
    /// 事件投影器：GameEvent → NetEvent（M1 协议下行事件流的产出端）。
    ///
    /// - 挂载：订阅 EventManager.AnyPublished（唯一全局收口，恰一次保序；先于 handler），
    ///   与 MatchLogService 同款（GameCore.Reset 内 EnsureStarted + ClearAll）。
    /// - 投影：反射公共属性（PropertyInfo 按类型缓存一次）拆成原语 + 实体引用
    ///   （Card→RuntimeId、Player→座位）。schema-less：新增事件/字段不是协议破坏性变更。
    /// - 性能：回合制事件量低（单局数千条、每条 ≤7 属性），反射开销可忽略；
    ///   如后续 profile 超标可升级 compiled expression（留优化注记，勿过早做）。
    /// - 线程：AnyPublished 在发布线程同步触发（编辑器 TCP 桥在后台线程跑对局）——
    ///   追加与读取均 lock；ErrorCount 用 Interlocked。
    /// - 容错：单事件投影异常自兜（ErrorCount++），不中断对局——验证器断言 ErrorCount==0。
    /// </summary>
    public static class NetEventProjector
    {
        private const int MaxEvents = 50000;

        private static readonly object _gate = new object();
        private static readonly List<NetEvent> _events = new List<NetEvent>();
        private static bool _started;
        private static bool _truncated;
        private static int _errorCount;

        private static readonly ConcurrentDictionary<Type, PropertyInfo[]> _propCache =
            new ConcurrentDictionary<Type, PropertyInfo[]>();

        /// <summary>开始投影（幂等；GameCore.Reset / 验证 harness 调用）。</summary>
        public static void EnsureStarted()
        {
            lock (_gate)
            {
                if (_started) return;
                _started = true;
                EventManager.Instance.AnyPublished += OnAnyPublished;
            }
        }

        /// <summary>清空缓冲（GameCore.Reset 调用；跨局不残留）。</summary>
        public static void ClearAll()
        {
            lock (_gate)
            {
                _events.Clear();
                _truncated = false;
            }
        }

        public static IReadOnlyList<NetEvent> Events
        {
            get { lock (_gate) return _events.ToArray(); }
        }

        public static bool WasTruncated
        {
            get { lock (_gate) return _truncated; }
        }

        public static int ErrorCount => _errorCount;

        /// <summary>投影一个事件（公开静态：验证器/回放工具可单测投影规则）。</summary>
        public static NetEvent Project(IGameEvent e)
        {
            if (e == null) return null;
            var props = _propCache.GetOrAdd(e.GetType(),
                t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));

            var parameters = new List<NetParam>(props.Length);
            foreach (var prop in props)
            {
                object value;
                try { value = prop.GetValue(e); }
                catch { value = null; }
                parameters.Add(ProjectValue(prop.Name, value));
            }

            return new NetEvent
            {
                EventType = e.GetType().Name,
                EventId = e.EventId,
                TurnNumber = GameCore.Instance?.TurnEngine?.TurnNumber ?? 0,
                Params = parameters.ToArray(),
            };
        }

        private static void OnAnyPublished(IEventData eventData)
        {
            if (!(eventData is IGameEvent e)) return;
            try
            {
                var projected = Project(e);
                if (projected == null) return;
                lock (_gate)
                {
                    if (_events.Count >= MaxEvents)
                    {
                        _events.RemoveAt(0);
                        _truncated = true;
                    }
                    _events.Add(projected);
                }
            }
            catch
            {
                // 单事件投影失败不中断对局；验证器断言 ErrorCount==0 以暴露回归
                System.Threading.Interlocked.Increment(ref _errorCount);
            }
        }

        /// <summary>单字段投影：按运行时类型分派（装箱 object 先拆箱再判定）。</summary>
        private static NetParam ProjectValue(string fieldName, object value)
        {
            var param = new NetParam { FieldName = fieldName, Kind = NetParamKind.Null };

            if (value == null) return param; // Null：显式表达，字段在

            // 引用类型先判（string 是 IEnumerable<char>，必须在集合判定之前）
            if (value is string s)
            {
                param.Kind = NetParamKind.String;
                param.StringValue = s;
                return param;
            }
            if (value is Player player)
            {
                param.Kind = NetParamKind.Entity;
                param.EntityRefs = new[] { NetEntityMapper.FromPlayer(player) };
                return param;
            }
            if (value is Card card)
            {
                param.Kind = NetParamKind.Entity;
                param.EntityRefs = new[] { NetEntityMapper.FromCard(card) };
                return param;
            }
            if (value is Entity entity) // 非 Card/Player 的实体兜底（当前无第三种，防演化踩空）
            {
                param.Kind = NetParamKind.Opaque;
                param.StringValue = entity.ToString();
                return param;
            }
            if (value is IGameEvent inner)
            {
                param.Kind = NetParamKind.EventRef;
                param.IntValue = inner.EventId;
                return param;
            }

            // 值类型与枚举
            var type = value.GetType();
            if (type.IsEnum)
            {
                param.Kind = NetParamKind.Enum;
                param.IntValue = Convert.ToInt64(value);
                param.StringValue = Enum.GetName(type, value);
                return param;
            }
            if (value is bool b) { param.Kind = NetParamKind.Int; param.IntValue = b ? 1 : 0; return param; }
            if (value is DateTime time) { param.Kind = NetParamKind.Int; param.IntValue = time.Ticks; return param; }
            if (value is int i) { param.Kind = NetParamKind.Int; param.IntValue = i; return param; }
            if (value is long l) { param.Kind = NetParamKind.Int; param.IntValue = l; return param; }
            if (value is short sh) { param.Kind = NetParamKind.Int; param.IntValue = sh; return param; }
            if (value is byte by) { param.Kind = NetParamKind.Int; param.IntValue = by; return param; }
            if (value is sbyte sb) { param.Kind = NetParamKind.Int; param.IntValue = sb; return param; }
            if (value is ushort us) { param.Kind = NetParamKind.Int; param.IntValue = us; return param; }
            if (value is uint ui) { param.Kind = NetParamKind.Int; param.IntValue = ui; return param; }
            if (value is ulong ul) { param.Kind = NetParamKind.Int; param.IntValue = unchecked((long)ul); return param; }
            if (value is float f) { param.Kind = NetParamKind.Float; param.FloatValue = f; return param; }
            if (value is double d) { param.Kind = NetParamKind.Float; param.FloatValue = d; return param; }
            if (value is decimal dec) { param.Kind = NetParamKind.Float; param.FloatValue = (double)dec; return param; }

            // 集合：全元素皆实体（含 null 元素跳过）→ EntityList；混合元素 → 整字段降级 Opaque
            if (value is System.Collections.IEnumerable enumerable)
            {
                var refs = new List<NetEntityRef>();
                var mixed = false;
                foreach (var item in enumerable)
                {
                    if (item == null) { refs.Add(NetEntityMapper.None); continue; }
                    if (item is Card itemCard) { refs.Add(NetEntityMapper.FromCard(itemCard)); continue; }
                    if (item is Player itemPlayer) { refs.Add(NetEntityMapper.FromPlayer(itemPlayer)); continue; }
                    mixed = true;
                    break;
                }
                if (!mixed)
                {
                    param.Kind = NetParamKind.EntityList;
                    param.EntityRefs = refs.ToArray();
                    return param;
                }
                param.Kind = NetParamKind.Opaque;
                param.StringValue = value.ToString();
                return param;
            }

            // 兜底：Effect/EffectInstance/EffectResolutionContext 等——仅展示用途（损失已在协议文档声明）
            param.Kind = NetParamKind.Opaque;
            param.StringValue = value.ToString();
            return param;
        }
    }
}
