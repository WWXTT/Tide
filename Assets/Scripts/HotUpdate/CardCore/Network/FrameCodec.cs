using System;
using System.Collections.Generic;
using MemoryPack;

namespace CardCore.Network
{
    /// <summary>
    /// TCP 帧编解码（M1 协议）：4 字节大端长度前缀 + MemoryPack(NetworkMessage)。
    /// 训练桥（换行 JSON）不动——两条协议栈各自独立（详见根目录 网络协议.md）。
    /// </summary>
    public static class FrameCodec
    {
        /// <summary>单帧上限（防长度字段损坏后的失控分配；快照/事件批次远小于此）。</summary>
        public const int MaxFrameBytes = 16 * 1024 * 1024;

        /// <summary>编码一帧。</summary>
        public static byte[] Frame(NetworkMessage message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            var payload = MemoryPackSerializer.Serialize(message);
            var frame = new byte[4 + payload.Length];
            frame[0] = (byte)(payload.Length >> 24);
            frame[1] = (byte)(payload.Length >> 16);
            frame[2] = (byte)(payload.Length >> 8);
            frame[3] = (byte)payload.Length;
            Buffer.BlockCopy(payload, 0, frame, 4, payload.Length);
            FrameSizeProbe.Record(message.Type, frame.Length);
            return frame;
        }

        /// <summary>
        /// 流式解码器：累积收到的字节块，凑满一帧吐一条消息（半包/粘包安全）。
        /// 单缓冲 + 头偏移，消费后惰性压缩。非线程安全——单连接单读侧使用
        /// （M2 定案：服务器单逻辑线程）。
        /// </summary>
        public sealed class FrameDecoder
        {
            private readonly List<byte> _buffer = new List<byte>(4096);
            private int _head;

            /// <summary>喂入一段收到的字节。</summary>
            public void Append(byte[] data)
            {
                if (data == null || data.Length == 0) return;
                _buffer.AddRange(data);
            }

            /// <summary>喂入缓冲的一段（socket 读返回 count &lt; 缓冲长时用，防把未读垃圾并入流）。</summary>
            public void Append(byte[] data, int offset, int count)
            {
                if (data == null || count <= 0) return;
                for (int i = 0; i < count; i++)
                    _buffer.Add(data[offset + i]);
            }

            /// <summary>尝试取出下一条完整消息；不足一帧返回 false。</summary>
            public bool TryDecode(out NetworkMessage message)
            {
                message = null;
                int available = _buffer.Count - _head;
                if (available < 4) { Compact(); return false; }

                int length = (_buffer[_head] << 24) | (_buffer[_head + 1] << 16)
                           | (_buffer[_head + 2] << 8) | _buffer[_head + 3];
                if (length < 0 || length > MaxFrameBytes)
                    throw new InvalidOperationException($"帧长度非法：{length}（连接数据损坏？）");
                if (available < 4 + length) { Compact(); return false; }

                var payload = new byte[length];
                _buffer.CopyTo(_head + 4, payload, 0, length);
                _head += 4 + length;
                Compact();

                message = MemoryPackSerializer.Deserialize<NetworkMessage>(payload);
                return true;
            }

            /// <summary>压缩已消费前缀：整空即清零；攒超 4KB 才搬移（摊销）。</summary>
            private void Compact()
            {
                if (_head == 0) return;
                if (_head >= _buffer.Count) { _buffer.Clear(); _head = 0; }
                else if (_head >= 4096) { _buffer.RemoveRange(0, _head); _head = 0; }
            }
        }
    }

    /// <summary>
    /// 帧大小探针（2026-10-23 TapTap 接入：量化各类消息序列化后大小分布——TapTap 单条
    /// ≤2048B/分片 1500B，快照/事件批量是否常态多片由实测定参）。默认关（Record 首行短路），
    /// p2pcheck 开启并在终局打印报告。
    /// </summary>
    public static class FrameSizeProbe
    {
        public static bool Enabled;

        private static readonly object _lock = new object();
        private static readonly Dictionary<NetworkMessageType, long> _max = new Dictionary<NetworkMessageType, long>();
        private static readonly Dictionary<NetworkMessageType, long> _sum = new Dictionary<NetworkMessageType, long>();
        private static readonly Dictionary<NetworkMessageType, int> _count = new Dictionary<NetworkMessageType, int>();

        public static void Record(NetworkMessageType type, int frameBytes)
        {
            if (!Enabled) return;
            lock (_lock)
            {
                _max[type] = _max.TryGetValue(type, out var m) && m > frameBytes ? m : frameBytes;
                _sum[type] = (_sum.TryGetValue(type, out var s) ? s : 0) + frameBytes;
                _count[type] = (_count.TryGetValue(type, out var c) ? c : 0) + 1;
            }
        }

        public static void Reset()
        {
            lock (_lock) { _max.Clear(); _sum.Clear(); _count.Clear(); }
        }

        /// <summary>按最大值降序的文本报告（含 TapTap 分片数换算）。</summary>
        public static string Report()
        {
            lock (_lock)
            {
                if (_count.Count == 0) return "[FrameSizeProbe] 无样本";
                var lines = new List<string> { "[FrameSizeProbe] 类型 | 条数 | 最大 | 平均 | TapTap片数(最大)" };
                var order = new List<NetworkMessageType>(_count.Keys);
                order.Sort((a, b) => _max[b].CompareTo(_max[a]));
                foreach (var t in order)
                {
                    long max = _max[t], sum = _sum[t];
                    int n = _count[t];
                    int chunks = (int)((max - 4 + TapMessageCodec.ChunkBinaryBytes - 1) / TapMessageCodec.ChunkBinaryBytes);
                    lines.Add($"{t} | {n} | {max}B | {sum / n}B | {Math.Max(1, chunks)}片");
                }
                return string.Join("\n", lines);
            }
        }
    }
}
