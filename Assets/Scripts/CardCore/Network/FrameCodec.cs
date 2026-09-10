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
}
