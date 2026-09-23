#if UNITY_EDITOR
using System;
using System.Threading;
using CardCore.Network;
using UnityEditor;
using UnityEngine;

namespace CardCore.Editor.NetSession
{
    /// <summary>
    /// M2 会话服务器宿主（编辑器/批处理双入口，镜像 TideHeadlessServer 模式）：
    ///
    /// - 编辑器菜单「Tools/网络会话服务器」：起/停本机服务器，EditorApplication.update 驱动泵
    ///   （编辑模式主线程 = 逻辑线程，满足单逻辑线程定案；可配合本地 TCP 客户端联调）。
    /// - batchmode（-executeMethod CardCore.Editor.NetSession.NetSessionHost.Main -netPort N）：
    ///   阻塞自旋驱动泵——引擎链在逻辑线程同步收敛（人类反问经 TCS 内联完成），
    ///   已知限制：编辑器 PlayerLoop 不转 → 引擎侧 GraceSeconds 超时竞速不点火
    ///   （客户端正常应答不受影响；服务器主动代打是 M4 范畴，届时由服务器定时器接管超时）。
    /// </summary>
    public static class NetSessionHost
    {
        private const int DefaultPort = 7777;

        private static NetSessionServer _server;

        [MenuItem("Tools/网络会话服务器/启动（本机）")]
        public static void StartServer()
        {
            if (_server != null)
            {
                Debug.LogWarning("[NetSession] 服务器已在运行");
                return;
            }
            _server = new NetSessionServer("editor", Debug.Log);
            _server.Start(DefaultPort);
            EditorApplication.update += PumpEditor;
        }

        [MenuItem("Tools/网络会话服务器/停止")]
        public static void StopServer()
        {
            if (_server == null)
            {
                Debug.LogWarning("[NetSession] 服务器未在运行");
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
            catch (Exception ex) { Debug.LogError($"[NetSession] 泵异常: {ex}"); }
        }

        /// <summary>
        /// batchmode 入口：阻塞自旋泵，断开所有连接后 Ctrl+C/外部终止退出。
        /// 调用约定：Unity.exe -batchmode -nographics -projectPath &lt;proj&gt;
        ///   -executeMethod CardCore.Editor.NetSession.NetSessionHost.Main -netPort &lt;n&gt; -logFile &lt;path&gt;
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

            var server = new NetSessionServer("batch", null);
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
