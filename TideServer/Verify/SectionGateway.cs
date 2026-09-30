using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using CardCore;
using CardCore.Network;

namespace TideServer.Verify
{
    /// <summary>
    /// V8 网关/子进程段（服务器专属，跨进程真实拓扑）：spawn 真网关进程——
    /// ①大厅反例（重复建房/加入不存在/直连 JoinRoom 走网关）②AI 填位整局+终局回收
    /// ③双房并发（两局两子进程同时打）④对局中断线作废 ⑤网关强杀 → Job Object 连带杀子进程。
    /// 段序固定：强杀必须最后（网关死亡）。①⑤为快用例；②③④为整局长用例（--quick 跳过③④）。
    /// </summary>
    internal static class SectionGateway
    {
        public static void Run(bool quick = false)
        {
            using var gw = new GatewayProc();
            if (!gw.Start())
            {
                VerifySuite.Assert(false, "网关子进程未在时限内就绪（READY 行协议）");
                return;
            }
            VerifySuite.Assert(true, $"网关就绪（端口 {gw.Port}，pid {gw.ProcessId}）");

            var deckIds = SynergyUI.CardCatalog.LoadAll().Select(c => c.ID).Distinct().Take(30).ToArray();
            var submit = new MsgDeckSubmit
            {
                DeckName = "verify-gateway",
                CardIds = deckIds,
                Digest = NetMatchHandshake.ComputeDeckDigest(deckIds),
            };

            RunLobbyRejections(gw, deckIds);
            RunAiFillMatch(gw, submit);
            if (!quick)
            {
                RunConcurrentRooms(gw, submit);
                RunDisconnectAbort(gw, submit);
            }
            RunGatewayKill(gw); // 必须最后（强杀网关）
        }

        // ---- ① 大厅反例（快） ----

        private static void RunLobbyRejections(GatewayProc gw, string[] deckIds)
        {
            // 重复建房：已入座连接再建 → Error
            var c1 = new VerifyClient(); c1.Connect("127.0.0.1", gw.Port);
            c1.Hello("反例1");
            c1.CreateRoom("反例房");
            WaitUntil(3000, () => c1.LastRoomState != null, c1);
            VerifySuite.Assert(c1.LastRoomState != null && c1.LastRoomState.Phase == (int)NetRoomPhase.Waiting,
                "建房入座成功（RoomState=Waiting）");
            c1.CreateRoom("再来一间");
            WaitUntil(3000, () => c1.Errors.Any(e => e.Context == "LobbyCreateRoom"), c1);
            VerifySuite.Assert(c1.Errors.Any(e => e.Context == "LobbyCreateRoom" && e.Reason.Contains("已在房间")),
                "已入座连接重复建房被拒（Error Context=LobbyCreateRoom）");
            c1.Close();

            // 加入不存在的房间 → Error
            var c2 = new VerifyClient(); c2.Connect("127.0.0.1", gw.Port);
            c2.Hello("反例2");
            WaitUntil(2000, () => c2.LastLobbyState != null, c2);
            c2.JoinRoomByLobby("nope-404");
            WaitUntil(3000, () => c2.Errors.Any(e => e.Context == "LobbyJoinRoom"), c2);
            VerifySuite.Assert(c2.Errors.Any(e => e.Context == "LobbyJoinRoom"),
                "加入不存在房间被拒（Error Context=LobbyJoinRoom）");
            c2.Close();

            // 直连 JoinRoom(112) 打网关 → Error（调试直连请连 match 子进程端口）
            var c3 = new VerifyClient(); c3.Connect("127.0.0.1", gw.Port);
            c3.Hello("反例3");
            WaitUntil(2000, () => c3.LastLobbyState != null, c3);
            c3.Send(NetworkMessageType.JoinRoom, new MsgJoinRoom { RoomId = "", WantSeat = -1, Nickname = "直连" });
            WaitUntil(3000, () => c3.Errors.Any(), c3);
            VerifySuite.Assert(c3.Errors.Any(e => e.Reason.Contains("网关")),
                "直连 JoinRoom 打网关被拒并提示走大厅（Error Reason 含\"网关\"）");
            c3.Close();
        }

        // ---- ② AI 填位整局（人类客户端 + LobbyAddAi → 终局回收） ----

        private static void RunAiFillMatch(GatewayProc gw, MsgDeckSubmit submit)
        {
            var a = new VerifyClient { Brain = new NetClientBrain() };
            a.Connect("127.0.0.1", gw.Port);
            a.Hello("人类A");
            a.CreateRoom("AI填位房");
            if (!WaitUntil(4000, () => a.LastRoomState != null, a))
            {
                VerifySuite.Assert(false, "AI 填位：建房未收到 RoomState");
                a.Close();
                return;
            }
            string roomId = a.LastRoomState.RoomId;
            a.AddAi(roomId, "填位AI");

            // A 提交卡组（AI 由网关回环客户端自行提交）→ 整局锁步
            if (!DriveToManifest(a, submit, out string err))
            {
                VerifySuite.Assert(false, $"AI 填位：A 握手未完成（{err}）");
                a.Close();
                return;
            }
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 180000 && !a.SawGameOver && !a.Disconnected)
            {
                a.Receive(20);
                a.Brain.Think(a, 60);
            }
            VerifySuite.Assert(a.SawGameOver, "AI 填位整局到达终局（GameOverEvent 到达人类客户端）");
            a.Close(); // 回收前提：全员离场（玩家在场=房间按设计保留）

            int recycled = gw.WaitForRecycleIncrement(1, 20000);
            VerifySuite.Assert(recycled >= 1, "AI 填位局终局回收（子进程被杀，房间移除）");
        }

        // ---- ③ 双房并发（两局两子进程同时打） ----

        private static void RunConcurrentRooms(GatewayProc gw, MsgDeckSubmit submit)
        {
            var c1 = new VerifyClient { Brain = new NetClientBrain() };
            var c2 = new VerifyClient { Brain = new NetClientBrain() };
            var c3 = new VerifyClient { Brain = new NetClientBrain() };
            var c4 = new VerifyClient { Brain = new NetClientBrain() };
            c1.Connect("127.0.0.1", gw.Port); c1.Hello("并发A1"); c1.CreateRoom("并发房1");
            c2.Connect("127.0.0.1", gw.Port); c2.Hello("并发B1"); c2.AutoMatch();
            c3.Connect("127.0.0.1", gw.Port); c3.Hello("并发A2"); c3.CreateRoom("并发房2");
            c4.Connect("127.0.0.1", gw.Port); c4.Hello("并发B2"); c4.AutoMatch();

            if (!DriveAllToManifest(30000, submit, out var err, c1, c2, c3, c4))
            {
                VerifySuite.Assert(false, $"双房并发握手未完成（{err}——自动匹配未入座？）");
                CloseAll(c1, c2, c3, c4);
                return;
            }
            VerifySuite.Assert(c1.LastRoomState?.RoomId != c3.LastRoomState?.RoomId,
                $"两局确实在两个房间（{c1.LastRoomState?.RoomId} vs {c3.LastRoomState?.RoomId}——各配一个子进程）");

            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 240000
                && !(c1.SawGameOver && c3.SawGameOver))
            {
                foreach (var c in new[] { c1, c2, c3, c4 })
                {
                    c.Receive(10);
                    c.Brain.Think(c, 60);
                }
            }
            VerifySuite.Assert(c1.SawGameOver && c3.SawGameOver,
                $"双房并发两局都到达终局（房1={c1.SawGameOver} 房2={c3.SawGameOver}——每局一进程并发成立）");
            CloseAll(c1, c2, c3, c4); // 回收前提：全员离场

            int recycled = gw.WaitForRecycleIncrement(2, 30000);
            VerifySuite.Assert(recycled >= 2, "两局终局回收（两个子进程都被杀）");
        }

        // ---- ④ 对局中断线作废 ----

        private static void RunDisconnectAbort(GatewayProc gw, MsgDeckSubmit submit)
        {
            var a = new VerifyClient { Brain = new NetClientBrain() };
            var b = new VerifyClient { Brain = new NetClientBrain() };
            a.Connect("127.0.0.1", gw.Port); a.Hello("断线A"); a.CreateRoom("断线房");
            b.Connect("127.0.0.1", gw.Port); b.Hello("断线B"); b.AutoMatch();

            if (!DriveAllToManifest(30000, submit, out var ea, a, b))
            {
                VerifySuite.Assert(false, $"断线作废：握手未完成（{ea}）");
                CloseAll(a, b);
                return;
            }

            // 打到 Playing 且至少推进 1 回合，然后 A 硬断（拔线）
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 120000)
            {
                a.Receive(15); b.Receive(15);
                a.Brain.Think(a, 60);
                b.Brain.Think(b, 60);
                var snap = b.LastSnapshot;
                if (b.LastRoomState?.Phase == (int)NetRoomPhase.Playing && snap != null && snap.CurrentTurn >= 1)
                    break;
            }
            VerifySuite.Assert(b.LastRoomState?.Phase == (int)NetRoomPhase.Playing,
                "对局进行中（Playing 且回合 ≥1）——硬断前奏");
            a.Abort(); // 不告而别

            sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 15000
                && !(b.RoomFinished && b.Errors.Any(e => e.Context == "Room")))
                b.Receive(50);
            VerifySuite.Assert(b.Errors.Any(e => e.Context == "Room" && e.Reason.Contains("对局作废")),
                $"留守方收到对局作废 Error（实际：{string.Join("；", b.Errors.Select(x => $"{x.Context}:{x.Reason}").Take(3))}）");
            VerifySuite.Assert(b.RoomFinished, "留守方 RoomState=Finished（断线作废收口）");
            b.Close(); // 回收前提：全员离场

            int recycled = gw.WaitForRecycleIncrement(1, 20000);
            VerifySuite.Assert(recycled >= 1, "作废局终局回收（子进程被杀）");
        }

        // ---- ⑤ 网关强杀 → Job Object 连带杀子进程（必须最后执行） ----

        private static void RunGatewayKill(GatewayProc gw)
        {
            gw.Kill();
            gw.WaitForExit(5000);
            VerifySuite.Assert(gw.HasExited, "网关进程已退出（外部强杀）");

            Thread.Sleep(2000); // Job Object kill-on-close 传播窗口
            int selfId = Environment.ProcessId;
            var survivors = Process.GetProcessesByName("TideServer")
                .Where(p => p.Id != selfId)
                .ToList();
            VerifySuite.Assert(survivors.Count == 0,
                $"网关死亡连带杀全部 match 子进程（残留 {survivors.Count} 个 TideServer 进程——Job Object 失效？）");
            foreach (var p in survivors) { try { p.Kill(); } catch { } }
        }

        // ============================================================ 辅助 ============================================================

        /// <summary>多客户端并行握手：同时收泵，各自到 DeckSubmit 即提交，全体等 Manifest。
        /// 串行等待会互相挤占窗口（先等者超时期间后者的卡组提交迟迟不发 → StartMatch 拖延 → 先等者必超时）。</summary>
        private static bool DriveAllToManifest(int timeoutMs, MsgDeckSubmit submit, out string error, params VerifyClient[] clients)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline && clients.Any(c => c.Manifest == null))
            {
                foreach (var c in clients)
                {
                    c.Receive(10);
                    if (c.Manifest == null && c.LastRoomState?.Phase == (int)NetRoomPhase.DeckSubmit)
                        c.Send(NetworkMessageType.DeckSubmit, submit);
                }
            }
            var stuck = clients.FirstOrDefault(c => c.Manifest == null);
            if (stuck != null)
            {
                error = $"客户端未到 Manifest（phase={stuck.LastRoomState?.Phase.ToString() ?? "null"}，errors={stuck.Errors.Count}）";
                return false;
            }
            foreach (var c in clients)
            {
                c.MyEngineSeat = c.Manifest.OwnSeat;
                if (c.Brain != null) c.Brain.MyEngineSeat = c.Manifest.OwnSeat;
            }
            error = null;
            return true;
        }

        /// <summary>驱动客户端走到 Manifest（等 RoomState(DeckSubmit) → 提交卡组 → 等 Manifest → 引擎座位回填）。</summary>
        private static bool DriveToManifest(VerifyClient c, MsgDeckSubmit submit, out string error)
        {
            error = null;
            if (!WaitUntil(10000, () => c.LastRoomState?.Phase == (int)NetRoomPhase.DeckSubmit, c))
            {
                error = $"未到 DeckSubmit（phase={c.LastRoomState?.Phase.ToString() ?? "null"}，errors={c.Errors.Count}）";
                return false;
            }
            c.Send(NetworkMessageType.DeckSubmit, submit);
            if (!WaitUntil(20000, () => c.Manifest != null, c))
            {
                error = $"Manifest 未到达（errors={string.Join("；", c.Errors.Select(x => x.Reason).Take(2))}）";
                return false;
            }
            c.MyEngineSeat = c.Manifest.OwnSeat;
            if (c.Brain != null) c.Brain.MyEngineSeat = c.Manifest.OwnSeat;
            return true;
        }

        /// <summary>等条件成立（期间收包泵客户端——条件字段只在 Receive 里更新，不收包=死等）。</summary>
        private static bool WaitUntil(int timeoutMs, Func<bool> condition, params VerifyClient[] pump)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                if (condition()) return true;
                foreach (var c in pump) c.Receive(15);
            }
            return condition();
        }

        private static void CloseAll(params VerifyClient[] clients)
        {
            foreach (var c in clients) c.Close();
        }

        /// <summary>网关子进程句柄（stdout 行协议 READY 解析 + stderr 实时透传 + 回收计数）。</summary>
        internal sealed class GatewayProc : IDisposable
        {
            private readonly System.Collections.Concurrent.ConcurrentQueue<string> _stdout = new();
            private Process _process;
            private int _recycleCount;

            public int Port { get; private set; }
            public int ProcessId => _process?.Id ?? -1;
            public bool HasExited => _process == null || _process.HasExited;

            public bool Start()
            {
                var psi = new ProcessStartInfo(Environment.ProcessPath, "gateway --port 0 --tick-ms 15")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };
                _process = Process.Start(psi);

                var outReader = new Thread(() =>
                {
                    string l;
                    while (!HasExited && (l = _process.StandardOutput.ReadLine()) != null) _stdout.Enqueue(l);
                }) { IsBackground = true };
                var errReader = new Thread(() =>
                {
                    string l;
                    while (!HasExited && (l = _process.StandardError.ReadLine()) != null)
                    {
                        Console.WriteLine($"    [gw] {l}");
                        if (l.Contains("终局回收")) Interlocked.Increment(ref _recycleCount);
                    }
                }) { IsBackground = true };
                outReader.Start();
                errReader.Start();

                var deadline = DateTime.UtcNow.AddSeconds(15);
                while (Port == 0 && DateTime.UtcNow < deadline && !HasExited)
                {
                    while (_stdout.TryDequeue(out var line))
                        if (line.StartsWith("READY port=", StringComparison.Ordinal)
                            && int.TryParse(line.Substring(11).Trim(), out var port))
                        {
                            Port = port;
                            break;
                        }
                    if (Port == 0) Thread.Sleep(30);
                }
                return Port != 0;
            }

            /// <summary>等回收计数达到 target（相对当前值的增量口径由调用方掌握）。</summary>
            public int WaitForRecycleIncrement(int target, int timeoutMs)
            {
                var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
                while (Volatile.Read(ref _recycleCount) < target && DateTime.UtcNow < deadline)
                    Thread.Sleep(200);
                return Volatile.Read(ref _recycleCount);
            }

            public void Kill()
            {
                try { if (!HasExited) _process.Kill(); } catch { }
            }

            public void WaitForExit(int ms)
            {
                try { _process?.WaitForExit(ms); } catch { }
            }

            public void Dispose()
            {
                try { if (!HasExited) { _process.Kill(); _process.WaitForExit(3000); } } catch { }
                _process?.Dispose();
            }
        }
    }
}
