#if UNITY_EDITOR
using System;
using System.Threading;
using CardCore.Network;
using UnityEditor;
using UnityEngine;

namespace CardCore.Editor.NetSession
{
    /// <summary>
    /// 大厅服务器宿主（L1，2026-09-24；随 Play 自启已于 2026-10-21 P2P 定案摘除）：
    /// P2P 拓扑下生产不走大厅层（房主=NetSessionServer 直连 JoinRoom），本宿主仅保留
    /// batchmode 手动入口供跨机调试与回环验证器对照——不再随 Play 模式自动起停。
    /// - batchmode：-executeMethod CardCore.Editor.NetSession.NetLobbyHost.Main -netPort N，
    ///   供另一台机器/关闭编辑器时做真人双端宿主（阻塞自旋 ~60Hz）。
    /// </summary>
    public static class NetLobbyHost
    {
        private const int DefaultPort = 8090;

        private static NetLobbyServer _server;

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
