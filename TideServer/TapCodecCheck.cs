using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CardCore.Network;

namespace TideServer
{
    /// <summary>
    /// TapTap 消息适配层单测（2026-10-23 接入；纯 C# 域）：分片/重组 roundtrip、
    /// 分片乱序/重复、msgId 缺口判死、损坏判死、限频泵排干与故障置位。
    /// 用法：TideServer tapcodec
    /// </summary>
    internal static class TapCodecCheck
    {
        public static int Run(Dictionary<string, string> options)
        {
            int pass = 0, fail = 0;
            void Assert(bool ok, string what)
            {
                if (ok) { pass++; Console.WriteLine($"  ✓ {what}"); }
                else { fail++; Console.WriteLine($"  ✗ {what}"); }
            }

            // ---- 1. 分片尺寸契约：任意大小信封 → 每片 ≤2048 字节 ----
            var rng = new Random(20261023);
            foreach (int size in new[] { 16, 1499, 1500, 1501, 5000, 64 * 1024, 300 * 1024 })
            {
                var envelope = new byte[size];
                rng.NextBytes(envelope);
                var chunks = TapMessageCodec.Chunk(1, envelope);
                bool allWithinBudget = chunks.All(c => System.Text.Encoding.UTF8.GetByteCount(c) <= TapMessageCodec.MaxWireBytes);
                Assert(allWithinBudget, $"信封 {size}B → {chunks.Length} 片，全部 ≤{TapMessageCodec.MaxWireBytes}B");
            }

            // ---- 2. 顺序 roundtrip ----
            {
                var re = new TapMessageReassembler();
                var seq = new TapMessageSequencer();
                var ok = true;
                foreach (int size in new[] { 200, 1500, 3300, 7000 })
                {
                    var envelope = new byte[size];
                    rng.NextBytes(envelope);
                    var chunks = TapMessageCodec.Chunk(seq.Next(), envelope);
                    byte[] got = null;
                    foreach (var c in chunks)
                    {
                        if (!re.Feed(c, out var assembled)) { ok = false; break; }
                        if (assembled != null) got = assembled;
                    }
                    ok = ok && got != null && got.SequenceEqual(envelope);
                }
                Assert(ok, "顺序喂片：4 条消息（跨 1~5 片档）逐字节还原");
            }

            // ---- 3. 单消息内分片乱序 + 重复片：仍能还原（重发/乱序属健康） ----
            {
                var envelope = new byte[7000];
                rng.NextBytes(envelope);
                var chunks = TapMessageCodec.Chunk(1, envelope);
                var shuffled = new List<string> { chunks[3], chunks[3], chunks[0], chunks[4], chunks[2], chunks[1], chunks[2] };
                var re = new TapMessageReassembler();
                byte[] got = null;
                bool alive = true;
                foreach (var c in shuffled)
                {
                    if (!re.Feed(c, out var assembled)) { alive = false; break; }
                    if (assembled != null) got = assembled;
                }
                Assert(alive && got != null && got.SequenceEqual(envelope), "片内乱序+重复：按 idx 归位还原");
            }

            // ---- 4. msgId 缺口：立即判死；旧消息分片：stale 忽略（不判死/不复活） ----
            {
                var re = new TapMessageReassembler();
                var seq = new TapMessageSequencer();
                var m1 = TapMessageCodec.Chunk(seq.Next(), new byte[] { 1, 2, 3 });
                var m2 = TapMessageCodec.Chunk(seq.Next(), new byte[] { 4, 5, 6 });
                var m3 = TapMessageCodec.Chunk(seq.Next(), new byte[] { 7, 8, 9 });
                bool m1AllAlive = true;
                foreach (var c in m1)
                    if (!re.Feed(c, out _)) m1AllAlive = false;
                bool gapDead = !re.Feed(m3[0], out _); // 跳过 msgId=2 → 缺口立即判死
                bool staleAfterGap = re.Feed(m1[0], out _); // 判死后旧片：仍按 stale 忽略（宿主已弃用本重组器）
                Assert(m1AllAlive && gapDead && staleAfterGap, "msgId 缺口立即判死；旧消息分片按 stale 忽略");
            }
            {
                var re = new TapMessageReassembler();
                var seq = new TapMessageSequencer();
                var m1 = TapMessageCodec.Chunk(seq.Next(), new byte[] { 1 });
                var m2 = TapMessageCodec.Chunk(seq.Next(), new byte[] { 2 });
                foreach (var c in m1) re.Feed(c, out _);
                bool staleAlive = re.Feed(m1[0], out _); // 旧片：健康丢弃
                bool ok2 = re.Feed(m2[0], out var got2); // 下一条正常
                Assert(staleAlive && ok2 && got2 != null && got2[0] == 2, "旧消息重复片丢弃后，后续消息正常接收");
            }

            // ---- 5. 损坏输入判死 ----
            {
                var re = new TapMessageReassembler();
                Assert(!re.Feed("garbage", out _), "非协议串判死");
                var re2 = new TapMessageReassembler();
                Assert(!re2.Feed("TGB1|1|0|99999999|QUJD", out _), "total 超上限判死");
                var re3 = new TapMessageReassembler();
                Assert(!re3.Feed("TGB1|1|10|10|QUJD", out _), "idx 越界（idx=total）判死");
            }

            // ---- 6. NetworkMessage 信封端到端（MemoryPack roundtrip 经分片层） ----
            {
                var payload = NetworkSerializer.BuildEnvelope(NetworkMessageType.JoinRoom,
                    new MsgJoinRoom { RoomId = "tap-room", WantSeat = -1, Nickname = "编队测试-潮汐" });
                var envelope = MemoryPack.MemoryPackSerializer.Serialize(payload);
                var chunks = TapMessageCodec.Chunk(1, envelope);
                var re = new TapMessageReassembler();
                NetworkMessage restored = null;
                foreach (var c in chunks)
                {
                    re.Feed(c, out var assembled);
                    if (assembled != null)
                        restored = MemoryPack.MemoryPackSerializer.Deserialize<NetworkMessage>(assembled);
                }
                var back = restored != null
                    ? NetworkSerializer.DeserializePayload<MsgJoinRoom>(restored)
                    : null;
                Assert(restored != null && restored.Type == NetworkMessageType.JoinRoom
                    && back != null && back.Nickname == "编队测试-潮汐" && back.WantSeat == -1,
                    "信封端到端：JoinRoom 经分片层还原（类型/中文名/座位无损）");
            }

            // ---- 7. 限频泵：排干 + 故障置位 ----
            {
                int sent = 0;
                var pump = new TapSendPump((msg, recv) => { sent++; return Task.CompletedTask; },
                    ratePerSecond: 5000);
                for (int i = 0; i < 60; i++) pump.Enqueue($"m{i}", "peer");
                var deadline = DateTime.UtcNow.AddSeconds(5);
                while (pump.PendingCount > 0 && DateTime.UtcNow < deadline)
                {
                    pump.Pump();
                    Thread.Sleep(2);
                }
                Assert(sent == 60 && pump.PendingCount == 0 && !pump.Failed, $"限频泵排干 60 条（实发 {sent}，零失败）");
            }
            {
                var pump = new TapSendPump((msg, recv) => Task.FromException(new Exception("sdk-down")),
                    ratePerSecond: 5000);
                pump.Enqueue("boom", "peer");
                var deadline = DateTime.UtcNow.AddSeconds(5);
                while (!pump.Failed && DateTime.UtcNow < deadline)
                {
                    pump.Pump();
                    Thread.Sleep(2);
                }
                Assert(pump.Failed, "发送链路故障 → Failed 置位（宿主据此判断线）");
            }

            Console.WriteLine($"[TapCodecCheck] 完成：PASS {pass} / FAIL {fail}");
            return fail == 0 ? 0 : 1;
        }
    }
}
