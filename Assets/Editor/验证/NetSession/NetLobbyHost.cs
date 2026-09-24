#if UNITY_EDITOR
using System;
using System.Threading;
using CardCore.Network;
using UnityEditor;
using UnityEngine;

namespace CardCore.Editor.NetSession
{
    /// <summary>
    /// 大厅服务器宿主（L1，2026-09-24，镜像 NetSessionHost 模式）：
    ///
    /// - 编辑器菜单「Tools/大厅服务器」：起/停本机大厅（房间列表/自动匹配/AI 填位），
    ///   EditorApplication.update 驱动泵。编辑器 Play 模式下照常运转——本机 UI 客户端
    ///   （MatchScreen）连 127.0.0.1 即可单人全流程测试（配 AI 填位当对手）。
    /// - batchmode：-executeMethod CardCore.Editor.NetSession.NetLobbyHost.Main -netPort N，
    ///   供另一台机器/关闭编辑器时做真人双端宿主（阻塞自旋 ~60Hz）。
    /// </summary>
    public static class NetLobbyHost
    {
        private const int DefaultPort = 7777;

        private static NetLobbyServer _server;

        [MenuItem("Tools/大厅服务器/启动（本机）")]
        public static void StartServer()
        {
            if (_server != null)
            {
                Debug.LogWarning("[NetLobby] 服务器已在运行");
                return;
            }
            _server = new NetLobbyServer("lobby", Debug.Log);
            _server.Start(DefaultPort);
            EditorApplication.update += PumpEditor;
        }

        [MenuItem("Tools/大厅服务器/停止")]
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
        /// </summary>
        public static void Main()
        {
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
