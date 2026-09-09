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
    ///   - batchmode（-executeMethod TideHeadlessServer.Main）：stdin 逐行读请求，stdout 逐行回响应。
    ///
    /// 协议（每行一条 JSON）：
    ///   请求  {"op":"reset"}             → 响应 {op, obs{cards,globals,actions,nActions}, reward, done, info{toPlay,winner,reason,turn}}
    ///   请求  {"op":"step","action":N}   → 响应同上
    ///   obs 恒为「当前回合玩家」视角（actor-centric 自对弈，reward 亦归属该视角玩家）。
    ///
    /// batchmode 调用约定（关键）：
    ///   Unity.exe -batchmode -nographics -projectPath &lt;proj&gt; -executeMethod TideHeadlessServer.Main
    ///             -logFile &lt;proj&gt;/Logs/headless.log -quit
    ///   - 必须 -logFile &lt;文件&gt;（不是 "-"），否则 Unity 日志会污染 stdout 协议流；
    ///   - Main 里再 Debug.unityLogger.logEnabled=false 双保险。
    ///   若 stdout 仍被污染（Unity 版本差异），回退 TCP：把 Main 换成 TcpListener 端口收发，协议不变。
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
            int requestCount = 0;

            try
            {
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

                    requestCount++;

                    // 每 10 个请求输出一次日志
                    if (requestCount % 10 == 1)
                    {
                        UnityEngine.Debug.Log($"[TideHeadless] 处理第 {requestCount} 个请求: {req.op}");
                    }

                    TideStepResult result = req.op == "reset"
                        ? HandleReset(driver)
                        : HandleStep(driver, req.action);

                    string resp = Serialize(req.op, result);
                    writer.WriteLine(resp);
                }

                UnityEngine.Debug.Log($"[TideHeadless] 客户端断开，共处理 {requestCount} 个请求");
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

        /// <summary>batchmode 主循环：stdin 逐行读请求，stdout 逐行回响应，读到 EOF 结束。</summary>
        public static void Main()
        {
            // 双保险防日志污染 stdout 协议流（配合 -logFile &lt;文件&gt; 启动参数）
            Debug.unityLogger.logEnabled = false;

            var driver = new TideHeadlessDriver();
            var stdin = new StreamReader(Console.OpenStandardInput(), Encoding.UTF8);
            var stdout = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true };
            try
            {
                string line;
                while ((line = stdin.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (line.Length == 0) continue;

                    HeadlessRequest req;
                    try { req = JsonUtility.FromJson<HeadlessRequest>(line); }
                    catch { continue; } // 无法解析的行丢弃

                    string resp = req.op == "reset"
                        ? Serialize("reset", HandleReset(driver))
                        : Serialize("step", HandleStep(driver, req.action));
                    stdout.WriteLine(resp);
                }
            }
            finally
            {
                driver.Dispose();
            }
        }

        private static TideStepResult HandleReset(TideHeadlessDriver driver)
        {
            // v2 随机对局（TCP / batchmode 共用）：
            //   双方各自从标准池（TestCreatureCards 非仪式）随机抽 30 张组卡组；
            //   引擎 P1 恒先手 → 50% 换座即随机先后手。obs 恒为当前回合玩家视角，换座对协议透明。
            var pool = AiBattleE2E.LoadStandardDeck();
            if (pool == null || pool.Count == 0)
            {
                UnityEngine.Debug.LogError("[TideHeadless] 标准卡组加载失败（Configs/TestDecks/TestCreatureCards.json），退回整池空卡组不可用");
                return driver.Reset(new List<CardData>(), new List<CardData>());
            }

            lock (DeckRngLock)
            {
                var deck1 = SampleRandomDeck(pool, RandomDeckSize);
                var deck2 = SampleRandomDeck(pool, RandomDeckSize);
                bool swap = DeckRng.Next(2) == 1; // 随机先后手：换座
                if (swap) { var t = deck1; deck1 = deck2; deck2 = t; }
                UnityEngine.Debug.Log($"[TideHeadless] 随机对局：双方各抽 {deck1.Count}/{deck2.Count} 张（池 {pool.Count}），先手 = {(swap ? "P2" : "P1")}");
                return driver.Reset(deck1, deck2);
            }
        }

        private const int RandomDeckSize = 30;
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

        [Serializable] public class HeadlessRequest { public string op; public int action; }
        [Serializable] public class HeadlessObs { public List<float> cards; public List<float> globals; public List<float> actions; public int nActions; }
        [Serializable] public class HeadlessInfo { public int toPlay; public string winner; public string reason; public int turn; }
        [Serializable] public class HeadlessResponse { public string op; public HeadlessObs obs; public float reward; public bool done; public HeadlessInfo info; }
    }
}
