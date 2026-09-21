using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using CardCore;
using CardCore.AI;
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
    ///   请求  {"op":"reset","opponent":"simpleai"}    → 模型 vs 脚本主题卡组（随机模型座次，对手回合
    ///                                                  Unity 侧由 SimpleAI+AutoMatch 策略自动打）
    ///   请求  {"op":"step","action":N}                → 响应同上
    ///   响应 {op, obs{cards,globals,actions,nActions}, reward, done, info{toPlay,winner,reason,turn,modelSeat,theme}}
    ///   obs 恒为「当前回合玩家」视角（自对弈）/「模型」视角（vs 脚本），reward 归属同视角；
    ///   info.modelSeat：模型座次（0=P1 / 1=P2），自对弈为 -1——vs 模式按它判胜负；
    ///   info.theme：对手主题 key（red/green/blue，vs 模式才有；自对弈空串）——Python 分主题统计胜率。
    ///
    /// 卡组口径（2026-09-21 主题卡组迁移，BattleDeckSources 统一取数）：
    ///   自对弈 = 主题整组（三套随机其一）vs Cards.json 随机 30 张，随机换座；
    ///   vs 脚本 = 模型 Cards.json 随机 30 张 vs 随机主题整组（SimpleAI 按主题 AutoMatch 策略）。
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

        /// <summary>卡身份清单（追加式分配的持久化）：训练/部署同一份 → 新哈希续排，已训 embedding 行永不串台。</summary>
        private static string CardManifestPath
            => Path.Combine(Application.dataPath, "..", "tide_rl", "card_identity_manifest.json");

        private static TcpListener _tcpListener;
        private static Thread _tcpThread;
        private static bool _isRunning;

        // ===================================================== TCP 服务器（编辑器手动启停；batchmode 走 Main 独立入口） =====================================================

        public static void StartTcpServer()
        {
            if (_isRunning)
            {
                Debug.LogWarning("[TideHeadless] TCP 服务器已在运行");
                return;
            }

            _isRunning = true;
            // 服务器重启时重载数据层（编辑器内可能改了 Cards.json/主题卡组）：
            // CardCatalog 缓存失效 + 主题卡组缓存置空（BattleDeckSources 每次重新读盘）
            _themeDecks = null;
            int loaded = TideCardIndex.ConfigureManifest(CardManifestPath); // 追加式下标，重启不改写
            Debug.Log(loaded >= 0
                ? $"[TideHeadless] 卡身份清单载入 {loaded} 条（{CardManifestPath}）"
                : $"[TideHeadless] 卡身份清单表指纹不符，旧清单作废从零续排（{CardManifestPath}）");
            _tcpThread = new Thread(TcpServerLoop) { IsBackground = true };
            _tcpThread.Start();
            Debug.Log($"[TideHeadless] TCP 服务器启动在 localhost:{TcpPort}，等待 Python 连接...");
        }

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

        // ===================================================== 自测（菜单入口已删，保留方法供测试/反射调用） =====================================================

        public static void RunSelfTest()
        {
            var pool = BattleDeckSources.AllCards();
            if (pool == null || pool.Count == 0)
            {
                Debug.LogError("[无头驱动] Cards.json 卡池加载失败（StreamingAssets/Card/Cards.json，先运行 Tools/构建三色主题卡组）");
                return;
            }
            TideCardIndex.ConfigureManifest(CardManifestPath); // 与训练路径同一份清单（追加式续排）
            int added = TideCardIndex.Register(pool); // 自测路径同口径登记卡身份（与 BattleDeckSources 同源同序）
            Debug.Log($"[无头驱动] 卡身份登记 +{added}（总 {TideCardIndex.Count}）");

            var driver = new TideHeadlessDriver();
            var rng = new System.Random(12345);
            try
            {
                // 随机策略自测只验证驱动环，口径对齐 selfplay：Cards 池随机 30 vs 随机 30（池含主题卡）
                var result = driver.Reset(
                    BattleDeckSources.SampleRandomDeck(pool, RandomDeckSize, rng),
                    BattleDeckSources.SampleRandomDeck(pool, RandomDeckSize, rng));
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
            TideCardIndex.ConfigureManifest(CardManifestPath); // batchmode 进程短命：跨进程稳定全靠清单

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
            // 2026-09-21 主题卡组口径（数据层迁移到 Cards.json + StreamingAssets/Card/，TestDecks 已删）：
            //   自对弈  = 主题整组（三套随机其一）vs Cards.json 随机 30 张，随机换座，双方模型驱动；
            //   vs 脚本 = 模型 Cards.json 随机 30 张 vs 随机主题整组，SimpleAI 按主题 AutoMatch 策略
            //              整回合自动打，obs/reward 恒为模型视角（info.modelSeat 判胜负、info.theme 分主题统计）。
            var pool = BattleDeckSources.AllCards();
            var themes = ThemeDeckCache.Where(t => t.deck.Count > 0).ToList();
            if (pool.Count == 0 || themes.Count == 0)
            {
                _opponentTheme = "";
                UnityEngine.Debug.LogError("[TideHeadless] 数据层缺失（Cards.json 卡池 "
                    + $"{pool.Count} 张 / 有效主题卡组 {themes.Count} 套）——先运行 Tools/构建三色主题卡组");
                return driver.Reset(new List<CardData>(), new List<CardData>());
            }

            int addedIds = BattleDeckSources.RegisterIdentities(); // 追加式幂等：新卡身份自动续排进 manifest
            if (addedIds > 0)
                UnityEngine.Debug.Log($"[TideHeadless] 卡身份登记 +{addedIds}（总 {TideCardIndex.Count}）");

            lock (DeckRngLock)
            {
                var theme = themes[DeckRng.Next(themes.Count)];
                if (opponent == "simpleai")
                {
                    // 模型 = Cards 随机 30；脚本 = 主题整组（策略按主题自动匹配：红快攻/绿慢速/蓝控制）
                    _opponentTheme = theme.key;
                    return driver.Reset(
                        BattleDeckSources.SampleRandomDeck(pool, RandomDeckSize, DeckRng),
                        theme.deck,
                        DeckRng.Next(2) == 0, // 随机模型座次（先后手各半）
                        AiStrategy.AutoMatch(theme.deck));
                }

                // 自对弈：主题整组 vs Cards 随机 30，主题侧随机先后手
                _opponentTheme = "";
                var randomDeck = BattleDeckSources.SampleRandomDeck(pool, RandomDeckSize, DeckRng);
                return DeckRng.Next(2) == 0
                    ? driver.Reset(theme.deck, randomDeck)
                    : driver.Reset(randomDeck, theme.deck);
            }
        }

        private const int RandomDeckSize = 30;

        /// <summary>本局对手主题 key（vs 模式 red/green/blue；自对弈空串）——Serialize info.theme 用。
        /// 单连接单局串行，读写无竞态。</summary>
        private static string _opponentTheme = "";

        /// <summary>主题卡组缓存（编辑器 TCP 重启时置空重载；batchmode 进程内加载一次）。</summary>
        private static List<(string key, List<CardData> deck)> _themeDecks;
        private static List<(string key, List<CardData> deck)> ThemeDeckCache
            => _themeDecks ??= BattleDeckSources.ThemeDecks();

        private static readonly System.Random DeckRng = new System.Random();
        private static readonly object DeckRngLock = new object();

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
                    theme = _opponentTheme ?? "",
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
        [Serializable] public class HeadlessInfo { public int toPlay; public string winner; public string reason; public int turn; public int modelSeat; public string theme; }
        [Serializable] public class HeadlessResponse { public string op; public HeadlessObs obs; public float reward; public bool done; public HeadlessInfo info; }
    }
}
