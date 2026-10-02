#if UNITY_EDITOR
using System;
using System.Threading;
using CardCore.Network;
using UnityEditor;
using UnityEngine;

namespace CardCore.Editor.NetSession
{
    /// <summary>
    /// 大厅服务器宿主（L1，2026-09-24；2026-09-26 改为随 Play 模式自动起停）：
    ///
    /// - **自动托管**：进入 Play 模式即自动启动本机大厅（房间列表/自动匹配/AI 填位），
    ///   退出 Play 模式即停止——[InitializeOnLoad] 挂 playModeStateChanged，无需菜单操作。
    ///   EditorApplication.update 驱动泵；本机 UI 客户端（MatchScreen）连 127.0.0.1 即可
    ///   单人全流程测试（配 AI 填位当对手）。非对局期间大厅空转，不干扰本地 AI 对战
    ///   （UIBootstrap 的引擎驱动闸只在对局期间停驱——见 NetLobbyServer.HasLiveMatch）。
    /// - batchmode：-executeMethod CardCore.Editor.NetSession.NetLobbyHost.Main -netPort N，
    ///   供另一台机器/关闭编辑器时做真人双端宿主（阻塞自旋 ~60Hz）。
    /// </summary>
    [InitializeOnLoad]
    public static class NetLobbyHost
    {
        private const int DefaultPort = 8090;

        private static NetLobbyServer _server;

        static NetLobbyHost()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        /// <summary>Play 模式进出自动起停（2026-09-26 定案）：运行=开服，停止=关服。
        /// 端口被占（双编辑器实例联机：另一实例已是宿主）时静默退位——本实例作为纯客户端。
        /// 比赛禁联机（NetGate，2026-10-01）：闸关时不再自启大厅（也不监听端口）。</summary>
        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.EnteredPlayMode:
                    if (!NetGate.OnlineEnabled)
                    {
                        Debug.Log("[NetLobby] 联机已禁用");
                        break;
                    }
                    if (_server != null)
                    {
                        Debug.Log("[NetLobby] 沿用已在运行的大厅服务器（进入 Play 模式）");
                        break;
                    }
                    try { StartServer(); }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[NetLobby] 自动启动失败（{ex.Message}）——本实例作为纯客户端，请连接对方大厅");
                    }
                    break;

                case PlayModeStateChange.ExitingPlayMode:
                    if (_server != null) StopServer();
                    break;
            }
        }

        public static void StartServer()
        {
            if (_server != null)
            {
                Debug.LogWarning("[NetLobby] 服务器已在运行");
                return;
            }
            var server = new NetLobbyServer("lobby", Debug.Log);
            server.Start(DefaultPort); // 监听失败（端口占用等）抛出——_server 保持 null，宿主退位
            _server = server;
            EditorApplication.update += PumpEditor;
        }

        public static void StopServer()
        {
            if (_server == null)
            {
                Debug.LogWarning("[NetLobby] 服务器未在运行");
                return;
            }
            EditorApplication.update -= PumpEditor;
            _server.Stop();
            _server = null;
        }

        private static void PumpEditor()
        {
            if (_server == null) return;
            try { _server.Pump(); }
            catch (Exception ex) { Debug.LogError($"[NetLobby] 泵异常: {ex}"); }
        }

        /// <summary>
        /// batchmode 入口：阻塞自旋泵，外部终止退出。
        /// Unity.exe -batchmode -nographics -projectPath &lt;proj&gt;
        ///   -executeMethod CardCore.Editor.NetSession.NetLobbyHost.Main -netPort &lt;n&gt; -logFile &lt;path&gt;
        /// 比赛禁联机（NetGate）：闸关时拒绝启动（防绕过 UI 用命令行宿主开服）。
        /// </summary>
        public static void Main()
        {
            if (!NetGate.OnlineEnabled)
            {
                Debug.LogError("[NetLobby] 联机已禁用（NetGate，比赛禁联机）——batchmode 大厅宿主拒绝启动");
                return;
            }

            int port = DefaultPort;
            var argv = Environment.GetCommandLineArgs();
            for (int i = 0; i < argv.Length - 1; i++)
                if (argv[i] == "-netPort" && int.TryParse(argv[i + 1], out var p) && p > 0)
                {
                    port = p;
                    break;
                }

            var server = new NetLobbyServer("batch-lobby", null);
            server.Start(port);
            try
            {
                while (true)
                {
                    server.Pump();
                    Thread.Sleep(15); // ~60Hz 逻辑帧
                }
            }
            finally
            {
                server.Stop();
            }
        }
    }
}
#endif
