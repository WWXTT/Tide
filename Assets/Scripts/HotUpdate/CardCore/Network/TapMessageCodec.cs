using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace CardCore.Network
{
    /// <summary>
    /// TapTap 自定义消息适配（2026-10-23 TapTap 联机接入）：现有线格式是
    /// 4 字节长度前缀 + MemoryPack(NetworkMessage) 二进制帧（最大 16MB），而 TapTap
    /// SendCustomMessage 只收 UTF8 字符串且单条 ≤2048 字节——本层把**信封字节**
    ///（不带长度前缀，重组后总长即长度）Base64 后分片：
    ///
    ///   TGB1|&lt;msgId&gt;|&lt;idx&gt;|&lt;total&gt;|&lt;base64&gt;   （全 ASCII）
    ///
    /// - msgId：每发送方从 1 单调递增（TapMessageSequencer）；
    /// - idx：0 起分片序号；total：分片总数；
    /// - 顺序纪律（对齐「断线作废」定案）：接收方发现 msgId 缺口/分片损坏/超额 → 判连接死
    ///   （经既有 OnDisconnect 链路收口），不做 ACK 重发——中转长连接丢包≈断线，真机冒烟
    ///   若观察到误杀再补可靠层。
    /// 纯 C#（无 Unity/SDK 依赖）——TideServer 单测覆盖（tapcodec）。
    /// </summary>
    public static class TapMessageCodec
    {
        /// <summary>TapTap 单条消息字节上限（官方文档）。</summary>
        public const int MaxWireBytes = 2048;

        /// <summary>单片承载的信封二进制字节数（Base64 膨胀 4/3 + 头部余量后 ≤2048）。</summary>
        public const int ChunkBinaryBytes = 1500;

        private const string Prefix = "TGB1";

        /// <summary>单条完整消息（信封）上限——对齐 FrameCodec.MaxFrameBytes。</summary>
        public static int MaxMessageBytes => FrameCodec.MaxFrameBytes;

        /// <summary>单消息最大分片数（防御恶意的 total 声明）。</summary>
        public const int MaxChunks = FrameCodec.MaxFrameBytes / ChunkBinaryBytes + 1;

        /// <summary>把信封字节分片成一组待发字符串（msgId 由调用方的 Sequencer 分配）。</summary>
        public static string[] Chunk(int msgId, byte[] envelope)
        {
            if (envelope == null || envelope.Length == 0)
                throw new ArgumentException("信封为空", nameof(envelope));
            if (envelope.Length > MaxMessageBytes)
                throw new ArgumentException($"信封超上限：{envelope.Length} > {MaxMessageBytes}");
            int total = (envelope.Length + ChunkBinaryBytes - 1) / ChunkBinaryBytes;
            var chunks = new string[total];
            var sb = new StringBuilder(64 + ChunkBinaryBytes * 4 / 3);
            for (int i = 0; i < total; i++)
            {
                int off = i * ChunkBinaryBytes;
                int take = Math.Min(ChunkBinaryBytes, envelope.Length - off);
                sb.Append(Prefix).Append('|').Append(msgId).Append('|')
                  .Append(i).Append('|').Append(total).Append('|')
                  .Append(Convert.ToBase64String(envelope, off, take));
                chunks[i] = sb.ToString();
                if (chunks[i].Length > MaxWireBytes)
                    throw new InvalidOperationException($"分片超限 {chunks[i].Length}（预算参数错误）");
                sb.Clear();
            }
            return chunks;
        }

        /// <summary>解析一条收到的高层消息；格式不符返回 false（调用方判连接死）。</summary>
        public static bool TryParse(string wire, out int msgId, out int idx, out int total, out byte[] chunk)
        {
            msgId = idx = total = 0;
            chunk = null;
            if (string.IsNullOrEmpty(wire) || wire.Length < Prefix.Length + 8) return false;
            if (wire[0] != 'T' || wire[1] != 'G' || wire[2] != 'B' || wire[3] != '1' || wire[4] != '|') return false;

            int p1 = wire.IndexOf('|', 5);
            if (p1 < 0) return false;
            int p2 = wire.IndexOf('|', p1 + 1);
            if (p2 < 0) return false;
            int p3 = wire.IndexOf('|', p2 + 1);
            if (p3 < 0) return false;
            if (!int.TryParse(wire.Substring(5, p1 - 5), out msgId) ||
                !int.TryParse(wire.Substring(p1 + 1, p2 - p1 - 1), out idx) ||
                !int.TryParse(wire.Substring(p2 + 1, p3 - p2 - 1), out total))
                return false;
            if (msgId < 1 || total < 1 || total > MaxChunks || idx < 0 || idx >= total) return false;
            try { chunk = Convert.FromBase64String(wire.Substring(p3 + 1)); }
            catch (FormatException) { return false; }
            return chunk != null && chunk.Length > 0 && chunk.Length <= ChunkBinaryBytes;
        }
    }

    /// <summary>每发送方 msgId 序列（1 起单调递增；每条 channel/传输一份——接收方按对端唯一序列校验）。</summary>
    public sealed class TapMessageSequencer
    {
        private int _last;

        public int Next() => ++_last;
    }

    /// <summary>
    /// 每对端重组器：喂高层分片 → 吐完整信封。规则见 TapMessageCodec 头注；
    /// 状态只被喂入方串行访问（IO 侧锁内调用），产出经队列转逻辑线程。
    /// </summary>
    public sealed class TapMessageReassembler
    {
        private int _nextExpected = 1;
        private int _accTotal;
        private readonly byte[][] _accChunks;

        public TapMessageReassembler()
        {
            _accChunks = new byte[TapMessageCodec.MaxChunks][];
        }

        /// <summary>
        /// 喂一片。返回 false=连接已死（调用方置断线）；envelope 非 null=凑满一条
        ///（本轮产出），null=仍不完整或重复片（健康）。
        /// </summary>
        public bool Feed(string wire, out byte[] envelope)
        {
            envelope = null;
            if (!TapMessageCodec.TryParse(wire, out var msgId, out var idx, out var total, out var chunk))
                return false;

            if (msgId < _nextExpected)
                return true; // 迟到/重复的旧消息分片：丢弃（重发场景），不判死
            if (msgId > _nextExpected)
                return false; // 序列缺口：中转乱序或丢消息——按断线作废收口

            // msgId == _nextExpected：当前正在攒的消息
            if (_accTotal == 0)
                _accTotal = total;
            else if (_accTotal != total)
                return false; // 同一 msgId 声明不一致：损坏

            if (_accChunks[idx] != null)
                return true; // 重复分片：丢弃（分片乱序到达属健康，按 idx 归位）
            _accChunks[idx] = chunk;

            for (int i = 0; i < _accTotal; i++)
                if (_accChunks[i] == null)
                    return true; // 还差分片

            // 凑满：按序拼装
            int len = 0;
            for (int i = 0; i < _accTotal; i++) len += _accChunks[i].Length;
            if (len <= 0 || len > TapMessageCodec.MaxMessageBytes)
                return false;
            var assembled = new byte[len];
            int off = 0;
            for (int i = 0; i < _accTotal; i++)
            {
                var part = _accChunks[i];
                _accChunks[i] = null;
                Buffer.BlockCopy(part, 0, assembled, off, part.Length);
                off += part.Length;
            }
            _accTotal = 0;
            _nextExpected++;
            envelope = assembled;
            return true;
        }
    }

    /// <summary>
    /// 出站限频泵（服务端限频 15~20 次/秒，取 12 留裕量）：待发队列 + 令牌桶，
    /// 逻辑线程每帧 Pump 排干。发送器注入（(消息, 接收者playerId) → Task）——
    /// TapTap 侧包装 SendCustomMessage(type=1, 指定玩家)；SDK 官方口径「无需等待上一条
    /// 完成即可发下一条」，FIFO 队列保调用序。
    /// </summary>
    public sealed class TapSendPump
    {
        public const double DefaultRatePerSecond = 12;

        private readonly Func<string, string, Task> _send;
        private readonly double _tokensPerMs;
        private readonly double _burstTokens;
        private readonly ConcurrentQueue<(string Msg, string Receiver)> _pending = new();
        private double _tokens;
        private long _lastRefillTicks;
        private volatile bool _failed;

        /// <summary>发送链路故障（SDK Task 异常）——宿主据此判全部 TapTap 连接断线。</summary>
        public bool Failed => _failed;

        public int PendingCount => _pending.Count;

        public TapSendPump(Func<string, string, Task> send, double ratePerSecond = DefaultRatePerSecond)
        {
            _send = send ?? throw new ArgumentNullException(nameof(send));
            _tokensPerMs = ratePerSecond / 1000.0;
            _burstTokens = Math.Max(1.0, ratePerSecond);
            _tokens = _burstTokens;
            _lastRefillTicks = Stopwatch.GetTimestamp();
        }

        public void Enqueue(string msg, string receiverId)
        {
            if (_failed) return;
            _pending.Enqueue((msg, receiverId));
        }

        /// <summary>逻辑线程每帧调用：按令牌排干队列。</summary>
        public void Pump()
        {
            if (_failed) return;
            long now = Stopwatch.GetTimestamp();
            double elapsedMs = (now - _lastRefillTicks) * 1000.0 / Stopwatch.Frequency;
            if (elapsedMs > 0)
            {
                _tokens = Math.Min(_burstTokens, _tokens + elapsedMs * _tokensPerMs);
                _lastRefillTicks = now;
            }
            while (_tokens >= 1.0 && _pending.TryDequeue(out var item))
            {
                _tokens -= 1.0;
                try
                {
                    var task = _send(item.Msg, item.Receiver);
                    if (task != null)
                        task.ContinueWith(
                            t => { if (t.IsFaulted) _failed = true; },
                            TaskContinuationOptions.ExecuteSynchronously);
                }
                catch (Exception) { _failed = true; return; }
            }
        }

        /// <summary>清空待发（停机/断线收口用）。</summary>
        public void Clear()
        {
            while (_pending.TryDequeue(out _)) { }
        }
    }
}
