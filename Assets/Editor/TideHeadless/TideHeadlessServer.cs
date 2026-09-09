using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using CardCore;
using CardCore.AI.NeuralEnv;
using CardCore.Editor.Tests;
using UnityEditor;
using UnityEngine;

namespace CardCore.Editor.TideHeadless
{
    /// <summary>
    /// 无头桥接服务（Phase 2）：Unity batchmode 内跑，stdio 换行 JSON 协议，供 Python 训练侧驱动。
    ///
    /// 入口：
    ///   - 编辑器菜单「Tools/AI/无头驱动自测」：随机策略跑完整一局，验证 reset/step 驱动环 + 终局收口。
    ///   - batchmode（-executeMethod TideHeadlessServer.Main）：起 TCP 桥接（同编辑器菜单路径，端口经 -tidePort 传入）。
    ///
    /// 协议（每行一条 JSON）：
    ///   请求  {"op":"reset"}                          → 自对弈（双方都由模型驱动）
    ///   请求  {"op":"reset","opponent":"simpleai"}    → 模型 vs SimpleAI（随机模型座次，对手回合 Unity 侧自动打）
    ///   请求  {"op":"step","action":N}                → 响应同上
    ///   响应 {op, obs{cards,globals,actions,nActions}, reward, done, info{toPlay,winner,reason,turn,modelSeat}}
    ///   obs 恒为「当前回合玩家」视角（自对弈）/「模型」视角（vs SimpleAI），reward 归属同视角；
    ///   info.modelSeat：模型座次（0=P1 / 1=P2），自对弈为 -1——vs 模式按它判胜负。
    ///
    /// batchmode 调用约定（关键）：
    ///   Unity.exe -batchmode -nographics -projectPath &lt;proj&gt; -executeMethod TideHeadlessServer.Main
    ///             -logFile &lt;proj&gt;/Logs/tide_headless.log -tidePort &lt;n&gt;
    ///   - stdio 方案已废弃：实测 6000.5.8f1 batchmode 会把 stdout 并入 -logFile，管道侧收不到；
    ///   - Main 起 TcpListener（loopback:-tidePort），只服务一个客户端，断开即退出；
    ///   - 协议与编辑器 TCP 菜单路径完全一致（逐行 JSON）。
    /// </summary>
    public static class TideHeadlessServer
    {
        private const int SelfTestMaxSteps = 2000;
        private const int TcpPort = 9999;

        private static TcpListener _tcpListener;
        private static Thread _tcpThread;
        private static bool _isRunning;

        // ===================================================== TCP 服务器（推荐开发/调试） =====================================================

        [MenuItem("Tools/AI/启动训练服务器 (TCP 9999)")]
        public static void StartTcpServer()
        {
            if (_isRunning)
            {
                Debug.LogWarning("[TideHeadless] TCP 服务器已在运行");
                return;
            }

            _isRunning = true;
            _deckPool = null; // 服务器重启时重新加载卡池（编辑器内可能改了卡表配置）
            _tcpThread = new Thread(TcpServerLoop) { IsBackground = true };
            _tcpThread.Start();
            Debug.Log($"[TideHeadless] TCP 服务器启动在 localhost:{TcpPort}，等待 Python 连接...");
        }

        [MenuItem("Tools/AI/停止训练服务器")]
        public static void StopTcpServer()
        {
            if (!_isRunning)
            {
                Debug.LogWarning("[TideHeadless] TCP 服务器未运行");
                return;
            }

            _isRunning = false;
            _tcpListener?.Stop();
            Debug.Log("[TideHeadless] TCP 服务器已停止");
        }

        private static void TcpServerLoop()
        {
            TcpListener listener = null;
            try
            {
                listener = new TcpListener(IPAddress.Loopback, TcpPort);
                _tcpListener = listener;
                listener.Start();

                while (_isRunning)
                {
                    // 等待连接（阻塞）
                    if (!listener.Pending())
                    {
                        Thread.Sleep(100);
                        continue;
                    }

                    TcpClient client = listener.AcceptTcpClient();
                    Debug.Log("[TideHeadless] Python 已连接");

                    // 处理客户端（单连接版本，一次只服务一个训练进程）
                    HandleTcpClient(client);

                    Debug.Log("[TideHeadless] Python 断开连接");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TideHeadless] TCP 服务器异常: {ex.Message}");
            }
            finally
            {
                listener?.Stop();
                _isRunning = false;
            }
        }

        private static void HandleTcpClient(TcpClient client)
        {
            var stream = client.GetStream();
            var reader = new StreamReader(stream, Encoding.UTF8);
            var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };

            var driver = new TideHeadlessDriver();

            try
            {
                // 热路径：训练期高频调用，除异常外不产生任何日志输出
                string line;
                while ((line = reader.ReadLine()) != null && _isRunning)
                {
                    line = line.Trim();
                    if (line.Length == 0) continue;

                    HeadlessRequest req;
                    try { req = JsonUtility.FromJson<HeadlessRequest>(line); }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogWarning($"[TideHeadless] 无法解析请求: {line}, 错误: {ex.Message}");
                        continue;
                    }

                    TideStepResult result = req.op == "reset"
                        ? HandleReset(driver, req.opponent)
                        : HandleStep(driver, req.action);

                    string resp = Serialize(req.op, result);
                    writer.WriteLine(resp);
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[TideHeadless] 处理客户端时出错: {ex.Message}");
            }
            finally
            {
                driver.Dispose();
                client.Close();
            }
        }

        // ===================================================== 自测入口（编辑器菜单） =====================================================

        [MenuItem("Tools/AI/无头驱动自测")]
        public static void RunSelfTest()
        {
            var deck = AiBattleE2E.LoadStandardDeck();
            if (deck == null || deck.Count == 0)
            {
                Debug.LogError("[无头驱动] 标准卡组加载失败（Configs/TestCreatureCards.json）");
                return;
            }
            TideCardIndex.Register(deck); // 自测路径同口径登记卡身份（与 DeckPool 同源同序）

            var driver = new TideHeadlessDriver();
            var rng = new System.Random(12345);
            try
            {
                var result = driver.Reset(deck, deck);
                int steps = 0;
                while (!result.Done && steps++ < SelfTestMaxSteps)
                {
                    result = driver.Step(PickAction(result, rng));
                }
                string outcome = result.Done
                    ? $"胜者 {NameOf(result.Winner)}（{result.Reason}）"
                    : $"步数上限 {SelfTestMaxSteps}（随机策略未分出胜负）";
                Debug.Log($"[无头驱动] 自测完成：{outcome}，共 {steps} 步，末步奖励 {result.Reward:F3}，回合 {result.Turn}");
            }
            finally
            {
                driver.Dispose();
            }
        }

        /// <summary>自测策略：30% 概率优先 EndTurn（保证对局前进），否则随机——覆盖全部动作类型。</summary>
        private static int PickAction(TideStepResult r, System.Random rng)
        {
            int n = r.Legal.Count;
            if (n == 0) return 0;
            for (int i = 0; i < n; i++)
            {
                if (r.Legal.Actions[i].Type == TideActionType.EndTurn && rng.NextDouble() < 0.3)
                    return i;
            }
            return rng.Next(n);
        }

        private static string NameOf(Player p)
            => p == null ? "?" : (ReferenceEquals(p, GameCore.Instance.Player1) ? "P1" : "P2");

        // ===================================================== batchmode 入口 =====================================================

        /// <summary>
        /// batchmode 入口：TCP 桥接（非 stdio）。
        /// 实测（Unity 6000.5.8f1 -batchmode -logFile）：stdout 写入会被并入日志文件，
        /// 管道侧收不到 → stdio 协议不可用，按预案回退 TCP。
        /// 端口经命令行 -tidePort &lt;n&gt; 传入（Python 侧随机选空闲口）；缺省用 TcpPort。
        /// 只服务一个客户端，客户端断开即返回 → batchmode 自然退出。
        /// </summary>
        public static void Main()
        {
            Debug.unityLogger.logEnabled = false;

            int port = TcpPort;
            var argv = Environment.GetCommandLineArgs();
            for (int i = 0; i < argv.Length - 1; i++)
                if (argv[i] == "-tidePort" && int.TryParse(argv[i + 1], out var p) && p > 0)
                {
                    port = p;
                    break;
                }

            var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start(1);
            // Python 侧靠端口轮询探活就绪，无需日志
            try
            {
                TcpClient client = listener.AcceptTcpClient(); // 阻塞等唯一客户端
                _isRunning = true; // HandleTcpClient 循环条件的开关（菜单路径由 StartTcpServer 置位）
                try { HandleTcpClient(client); }
                finally { _isRunning = false; }
            }
            finally
            {
                listener.Stop();
            }
        }

        private static TideStepResult HandleReset(TideHeadlessDriver driver, string opponent)
        {
            // v2 随机对局（TCP / batchmode 共用）：
            //   双方各自从标准池（TestCreatureCards 非仪式）随机抽 30 张组卡组；
            //   引擎 P1 恒先手 → 随机先后手（自对弈 = 换座 / vs SimpleAI = 随机模型座次）。
            //   opponent=="simpleai" 时另一座位整回合由 SimpleAI 自动打，obs/reward 恒为模型视角。
            var pool = DeckPool;
            if (pool == null || pool.Count == 0)
            {
                UnityEngine.Debug.LogError("[TideHeadless] 标准卡组加载失败（Configs/TestDecks/TestCreatureCards.json），退回整池空卡组不可用");
                return driver.Reset(new List<CardData>(), new List<CardData>());
            }

            TideCardIndex.Register(pool); // 卡身份下标（obs 第 15 维）：文件序确定性 → 跨局/跨进程稳定

            lock (DeckRngLock)
            {
                var deck1 = SampleRandomDeck(pool, RandomDeckSize);
                var deck2 = SampleRandomDeck(pool, RandomDeckSize);
                if (opponent == "simpleai")
                    return driver.Reset(deck1, deck2, DeckRng.Next(2) == 0); // 随机模型座次（先后手各半）
                bool swap = DeckRng.Next(2) == 1; // 自对弈：随机先后手换座
                if (swap) { var t = deck1; deck1 = deck2; deck2 = t; }
                return driver.Reset(deck1, deck2);
            }
        }

        private const int RandomDeckSize = 30;

        /// <summary>标准卡池缓存：JSON 只解析一次（每个 reset 复用；编辑器重启 TCP 服务器时置空重载）。</summary>
        private static List<CardData> _deckPool;
        private static List<CardData> DeckPool => _deckPool ??= AiBattleE2E.LoadStandardDeck();
        private static readonly System.Random DeckRng = new System.Random();
        private static readonly object DeckRngLock = new object();

        /// <summary>池洗牌（Fisher-Yates）后取前 size 张；池不足时整池上阵。</summary>
        private static List<CardData> SampleRandomDeck(List<CardData> pool, int size)
        {
            var shuffled = new List<CardData>(pool);
            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int j = DeckRng.Next(i + 1);
                (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
            }
            return shuffled.GetRange(0, Math.Min(size, shuffled.Count));
        }

        private static TideStepResult HandleStep(TideHeadlessDriver driver, int action)
            => driver.Step(action);

        private static string Serialize(string op, TideStepResult r)
        {
            var resp = new HeadlessResponse
            {
                op = op,
                obs = new HeadlessObs
                {
                    cards = ToList(r.Obs.Cards),
                    globals = ToList(r.Obs.Globals),
                    actions = ToList(r.Legal.Features),
                    nActions = r.Legal.Count,
                },
                reward = r.Reward,
                done = r.Done,
                info = new HeadlessInfo
                {
                    toPlay = r.Done ? -1
                        : (ReferenceEquals(GameCore.Instance.TurnEngine.TurnPlayer, GameCore.Instance.Player1) ? 0 : 1),
                    winner = r.Winner == null ? "" : (ReferenceEquals(r.Winner, GameCore.Instance.Player1) ? "0" : "1"),
                    reason = r.Reason ?? "",
                    turn = r.Turn,
                    modelSeat = r.ModelSeat,
                },
            };
            return JsonUtility.ToJson(resp);
        }

        private static List<float> ToList(float[] a)
        {
            var l = new List<float>(a.Length);
            l.AddRange(a);
            return l;
        }

        // ===================================================== JSON DTO（JsonUtility 字段名即协议键） =====================================================

        [Serializable] public class HeadlessRequest { public string op; public int action; public string opponent; }
        [Serializable] public class HeadlessObs { public List<float> cards; public List<float> globals; public List<float> actions; public int nActions; }
        [Serializable] public class HeadlessInfo { public int toPlay; public string winner; public string reason; public int turn; public int modelSeat; }
        [Serializable] public class HeadlessResponse { public string op; public HeadlessObs obs; public float reward; public bool done; public HeadlessInfo info; }
    }
}
