using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using CardCore;
using CardCore.Network;
using CardCore.Serialization;

namespace TideServer
{
    /// <summary>
    /// P2P 拓扑无头冒烟（2026-10-21 P2P 定案；网络协议.md §15）：
    /// 房主=进程内 NetSessionServer（本类手动泵=Unity 侧 UIBootstrap.Update 的无头等价），
    /// 客户端=回环 TCP（房主玩家/远程客人在传输层同形）。
    ///
    /// 段 A（intent 路由）：未开局时以观战连接发送 IntentActivateHeroSkill(113)——离散位在
    /// 会话层消息（110-112）之后，若 NetRoom.OnMessage 的 intent 段判定漏掉 113 会得到
    /// "不是会话层可处理的上行"；正确路由应得到"观战连接不能发 intent"（进入 HandleIntent）。
    /// 段 B（整局）：双 NetAiFiller.FromPlayPool（带技能卡提交）真实 TCP 回环入座打完整局
    /// 到 Finished——覆盖 DeckSubmit 握手/快照事件流/反问/intent 全链与 NetAiFiller 本体。
    /// </summary>
    internal static class P2pCheck
    {
        private const int TimeoutSeconds = 180;

        public static int Run(Dictionary<string, string> options)
        {
            int? seed = Program.GetNullableInt(options, "seed");
            var log = new List<string>();
            void Log(string msg)
            {
                log.Add(msg);
                Console.WriteLine($"[P2pCheck] {msg}");
            }

            int pass = 0, fail = 0;
            void Assert(bool ok, string what)
            {
                if (ok) { pass++; Console.WriteLine($"  ✓ {what}"); }
                else { fail++; Console.WriteLine($"  ✗ {what}"); }
            }

            // ---- 数据装载（DeckSubmit 校验/卡组摘要依赖卡表） ----
            LoadCheck.Run();

            // 帧大小探针（2026-10-23 TapTap 接入：量化各类消息实际大小——分片参数的实证依据）
            FrameSizeProbe.Reset();
            FrameSizeProbe.Enabled = true;

            // ---- 房主：进程内会话服务器（port 0=OS 分配） ----
            var server = new NetSessionServer("p2p-check", msg => Console.WriteLine($"[P2pCheck][服务器] {msg}"), seed);
            server.Start(0);
            try
            {
                Assert(server.Port > 0, $"会话服务器监听 127.0.0.1:{server.Port}");
                Assert(ReferenceEquals(NetSessionServer.Current, server), "NetSessionServer.Current 登记房主实例");
                Assert(NetSessionServer.IsRunning, "IsRunning 置位");

                // ============ 段 A：IntentActivateHeroSkill(113) 路由 ============
                string routingError = null;
                var probeDone = new ManualResetEventSlim(false);
                var probeThread = new Thread(() =>
                {
                    try
                    {
                        using var tcp = new TcpClient();
                        tcp.Connect("127.0.0.1", server.Port);
                        using var stream = tcp.GetStream();
                        // 进房（观战位——不占玩家座）→ 发 113（无载荷 intent）
                        Write(stream, NetworkMessageType.JoinRoom, new MsgJoinRoom { AsSpectator = true, Nickname = "probe" });
                        Thread.Sleep(300); // 等泵消化 JoinRoom（一连接一身份）
                        Write(stream, NetworkMessageType.IntentActivateHeroSkill, new MsgIntentActivateHeroSkill { EffectIndex = 0 });
                        // 收 Error 帧（观战发 intent 的拒绝文案）
                        var decoder = new FrameCodec.FrameDecoder();
                        var buf = new byte[16 * 1024];
                        var deadline = DateTime.UtcNow.AddSeconds(10);
                        while (DateTime.UtcNow < deadline)
                        {
                            if (!stream.DataAvailable) { Thread.Sleep(15); continue; }
                            int n = stream.Read(buf, 0, buf.Length);
                            if (n <= 0) break;
                            decoder.Append(buf, 0, n);
                            while (decoder.TryDecode(out var msg))
                            {
                                if (msg.Type != NetworkMessageType.Error) continue;
                                var err = NetworkSerializer.DeserializePayload<MsgError>(msg);
                                routingError = $"{err?.Context}: {err?.Reason}";
                                probeDone.Set();
                                return;
                            }
                        }
                    }
                    catch (Exception ex) { routingError = $"探测异常 {ex.GetType().Name}: {ex.Message}"; probeDone.Set(); }
                }) { IsBackground = true };
                probeThread.Start();
                var spin = DateTime.UtcNow.AddSeconds(TimeoutSeconds);
                while (!probeDone.IsSet && DateTime.UtcNow < spin) { server.Pump(); Thread.Sleep(15); }
                Assert(probeDone.IsSet, "路由探测收到回执");
                Assert(routingError != null
                        && (routingError.Contains("对局未在进行中") || routingError.Contains("观战连接不能发 intent")),
                    $"113 路由进 HandleIntent（回执：{routingError}）——落入会话层 intent 分支而非“不可处理上行”");

                // ============ 段 B：双回环 AI 客户端整局（带技能卡） ============
                var fillerA = NetAiFiller.FromPlayPool("P2P-A");
                var fillerB = NetAiFiller.FromPlayPool("P2P-B");
                Assert(fillerA != null && fillerB != null, "双回环 AI 客户端构造（卡池+自动技能卡）");
                if (fillerA == null || fillerB == null) return 1;
                fillerA.Start(server.Port);
                fillerB.Start(server.Port);

                spin = DateTime.UtcNow.AddSeconds(TimeoutSeconds);
                var lastPhase = (NetRoomPhase)(-1);
                var phaseSince = DateTime.UtcNow;
                while (DateTime.UtcNow < spin)
                {
                    server.Pump(); // 房主泵（UIBootstrap.Update 的无头等价：单逻辑线程收发/引擎推进/下行出队）
                    if (server.Room.Phase != lastPhase)
                    {
                        Log($"房间相位：{lastPhase} → {server.Room.Phase}（{(DateTime.UtcNow - phaseSince).TotalSeconds:F1}s）");
                        lastPhase = server.Room.Phase;
                        phaseSince = DateTime.UtcNow;
                    }
                    if (server.Room.Phase == NetRoomPhase.Finished) break;
                    Thread.Sleep(15);
                }
                Log($"退出等待：相位={server.Room.Phase} FirstSeat={server.Room.FirstSeat} 空位={server.Room.FreeChairCount}");

                Assert(server.Room.Phase == NetRoomPhase.Finished, "整局打完（Playing → Finished）");
                int skillEvents = NetEventProjector.Events.Count(e => e.EventType == nameof(HeroSkillActivatedEvent));
                Log($"技能发动遥测：整局 HeroSkillActivatedEvent × {skillEvents}（IntentActivateHeroSkill(113) 端到端；0=本局无可用目标，非断言项）");
                Assert(server.Room.FirstSeat == 0 || server.Room.FirstSeat == 1, "先手椅位已定（服务器权威掷先手）");
            }
            finally
            {
                server.Stop();
            }
            Assert(!NetSessionServer.IsRunning && NetSessionServer.Current == null, "停机后 Current/IsRunning 清空");

            // 帧大小报告（探针在开跑前已开）——TapTap 单条 2048B/片 1500B 的适配依据
            FrameSizeProbe.Enabled = false;
            Console.WriteLine(FrameSizeProbe.Report());

            Console.WriteLine($"[P2pCheck] 完成：PASS {pass} / FAIL {fail}");
            return fail == 0 ? 0 : 1;
        }

        private static void Write<T>(NetworkStream stream, NetworkMessageType type, T payload) where T : class
        {
            var frame = FrameCodec.Frame(NetworkSerializer.BuildEnvelope(type, payload));
            stream.Write(frame, 0, frame.Length);
            stream.Flush();
        }
    }
}
