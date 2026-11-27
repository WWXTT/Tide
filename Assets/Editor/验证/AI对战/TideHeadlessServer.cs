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
    ///   - `RunSelfTest()`：随机策略跑完整一局，验证 reset/step 驱动环 + 终局收口（**当前无菜单入口**——
    ///     原「Tools/AI/无头驱动自测」菜单已移除，需经 -executeMethod 或编辑器直接调用）。
    ///   - batchmode（-executeMethod TideHeadlessServer.Main）：起 TCP 桥接（端口经 -tidePort 传入）。
    ///
    /// 协议（每行一条 JSON）：
        ///   请求  {"op":"reset"}                          → 自对弈（双方都由模型驱动）
        ///   请求  {"op":"reset","opponent":"simpleai"}    → 模型 vs 脚本主题卡组（随机模型座次，对手回合
        ///                                                  Unity 侧由 SimpleAI+AutoMatch 策略自动打）
        ///   请求  {"op":"reset","opponent":"simpleai","modelTheme":"red"}
        ///                                                → vs 脚本口径下模型侧卡组固定该主题整组
        ///                                                  （2026-10-30 红·快攻镜像训练口径；缺省模型侧
        ///                                                  仍 Cards 随机 30；找不到主题 LogWarning 回落随机）
        ///   请求  {"op":"reset","opponent":"script","modelTheme":"green","theme":"red"}
        ///                                                → 全脚本对局（2026-11-22「绿打红 0%」归因取证）：
        ///                                                  双方 SimpleAI+AutoMatch(各自卡组) 自动打到终局，
        ///                                                  reset 响应即终态（done=true；不发 step）；胜负按
        ///                                                  info.winner × info.modelSeat（modelTheme 侧座次）判，
        ///                                                  卡组/环境语义与 simpleai 口径逐位一致
        ///   请求  {"op":"reset","modelDoped":"red"}      → 掺卡口径（2026-11-17 阶段二，AI训练说明 §0.1）：
        ///                                                  命中主题的一侧 = 整组每种 −1 张 + 训练池随机补足
        ///                                                  （红 30→25+5）；vs 脚本=模型侧，自对弈=一侧
        ///                                                  （另一侧主题整组/随机 30 各半）；优先级高于 modelTheme；
        ///                                                  info.modelDoped 回显实际命中 key，未传零差
        ///   请求  {"op":"reset","shapingLambda":0,"shapingSet":1}
        ///                                                → 按局显式塑形 λ（2026-11-02：shapingSet=1
        ///                                                  时 shapingLambda 原样生效，0 = 关闭塑形只留
        ///                                                  终局 ±1——λ=0 基线；不带该组键 = 驱动默认 0.005）
        ///   请求  {"op":"reset","modelSeat":"0|1","seed":N}
        ///                                                → 评估协议 v2（2026-11-22 配对终版）：modelSeat
        ///                                                  钉模型座次（"0"=P1 先手 / "1"=P2；字符串类型——
        ///                                                  JsonUtility 缺省 int=0 会把老客户端误钉 P1；
        ///                                                  不带/空串 = 保留掷币）；seed&gt;0 钉对局种子
        ///                                                  （DeckRng 卡组抽样 + InitGame 双随机流重播，
        ///                                                  0 = 不钉缺省）。同 seed+同座次+同卡组 = 逐局
        ///                                                  精确重播——两检查点打同一批对局（配对评估）。
        ///                                                  info.seed 回显实际种子（0=未钉），Python 断言。
        ///   请求  {"op":"reset","picksMode":"macro|step|both"}
        ///                                                → v22 二期 S2a（2026-11-26）：目标拾取行模式——
        ///                                                  macro（默认）=多步计划只产选项提交行（[20]=1，
        ///                                                  OpenAndAutoComplete 按共享序一次选满提交）；
        ///                                                  step=A6 逐步行；both=并存（诊断）。不带=macro；
        ///                                                  非法回落 macro（LogWarning）。info.picksMode 回显。
        ///   请求  {"op":"step","action":N}                → 响应同上
        ///   响应 {op, obs{cards,globals,actions,nActions}, reward, done,
        ///         info{toPlay,winner,reason,turn,modelSeat,theme,modelTheme,modelDoped,layoutVersion,dims,decisionSeat,seed,picksMode}}
        ///   obs 恒为「当前决策座次」视角（v2：Main=回合玩家 / 响应窗口=优先权持有方；vs 脚本恒为模型视角），
        ///   reward 归属同视角；
        ///   info.toPlay：下一决策点行动方座次（0=P1 / 1=P2，终局 -1）——v2 起含响应窗口座次，Python 自对弈
        ///   GAE 座次修正用；info.decisionSeat 同值（显式别名）；
        ///   info.layoutVersion/dims：布局握手（v8 / {nCard:115,nGlobal:50,nAction:11,maxActions:512,maxCards:86}）——
        ///   Python reset 时断言逐值相等，维度漂移报清晰错误（防 2026-09-14 式 reshape 静默炸）；
        ///   info.modelSeat：模型座次（0=P1 / 1=P2），自对弈为 -1——vs 模式按它判胜负；
        ///   info.theme：对手主题 key（red/green/blue，vs 模式才有；自对弈空串）——Python 分主题统计胜率；
        ///   info.modelTheme：模型侧卡组实际命中的主题 key（vs 模式且 modelTheme 请求命中才有；
        ///   缺省/未命中/自对弈空串）——Python 镜像口径断言用。
        ///   info.seed：本局实际种子（评估协议 v2，2026-11-22；0 = 未钉沿用环境流）——Python
        ///   reset 断言与请求一致，防老 Unity 静默忽略新字段。
    ///
        /// 卡组口径（2026-09-21 主题卡组迁移，BattleDeckSources 统一取数；2026-10-30 modelTheme 扩展）：
        ///   自对弈 = 主题整组（三套随机其一）vs Cards.json 随机 30 张，随机换座；
        ///   vs 脚本 = 模型卡组（modelTheme 命中 → 该主题整组，红·快攻镜像口径；缺省 Cards.json
        ///             随机 30 张）vs 随机主题整组（SimpleAI 按主题 AutoMatch 策略）。
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

        // P-1（2026-11-19 稳定下标）：CardManifestPath/ConfigureManifest/RegisterIdentities 已删——
        // 身份下标纯派生（TideCardIndex/CardIdentityService），不读写 manifest（文件仅存档）。

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
            // P-1 布局回显（Python _assert_layout 同口径对账）：pool sizes + layout
            Debug.Log($"[TideHeadless] 稳定下标 P-1：cardPool {TideCardIndex.Capacity}"
                      + $" / typePool {CardIdentityService.MaxTypeIndex + 1}"
                      + $" / effectPool {TideCardIndex.EffectIdentityPool}"
                      + $" / layout v{TideObservation.LayoutVersion}（manifest 已废弃，纯派生同号）");
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
            string line = null; // 最后收到的请求（catch 异常日志引用，须声明在 try 外）

            try
            {
                // 热路径：训练期高频调用，除异常外不产生任何日志输出
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
                        ? HandleReset(driver, req.opponent, req.shapingLambda, req.theme, req.modelTheme,
                                      req.modelDoped, req.shapingSet != 0, req.modelSeat, req.seed,
                                      req.picksMode)
                        : HandleStep(driver, req.action);

                    string resp = Serialize(req.op, result);
                    writer.WriteLine(resp);
                }
            }
            catch (Exception ex)
            {
                // 热路径静音不挡异常通道（2026-09-30）：先恢复日志再报——否则对局中
                // 抛异常只会无声断连退出（Python 侧 10054，服务端零线索）
                Debug.unityLogger.logEnabled = true;
                UnityEngine.Debug.LogError(
                    $"[TideHeadless] 处理客户端时出错（最后请求: {line ?? "(无)"}）: {ex}");
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
            var pool = BattleDeckSources.TrainingPool(); // 2026-11-15 训练池定案（矩阵 §0 P0-C）：教学卡不参与训练
            if (pool == null || pool.Count == 0)
            {
                Debug.LogError("[无头驱动] 训练池加载失败（tag「训练池」0 张——TrainingCards.json 缺失或全表掉标签）");
                return;
            }
            // P-1（2026-11-19 稳定下标）：自测路径不再绑定/登记清单——下标纯派生，跨进程同号。
            Debug.Log($"[无头驱动] 稳定下标 P-1（cardPool {TideCardIndex.Capacity}"
                      + $" / typePool {CardIdentityService.MaxTypeIndex + 1}）——manifest 已废弃");

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
            // P-1 稳定下标：跨进程同号由纯派生保证（SplitMix64），batchmode 无需清单载入。

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

        private static TideStepResult HandleReset(TideHeadlessDriver driver, string opponent,
            float shapingLambda = 0f, string themeKey = null, string modelThemeKey = null,
            string modelDopedKey = null, bool shapingExplicit = false, string modelSeatKey = null,
            int seed = 0, string picksModeKey = null)
        {
            // v22 S2a（2026-11-26 二期）：拾取行模式——macro（默认）=多步计划只产选项提交行
            //（OpenAndAutoComplete 按共享序一次选满提交，时域/粒度修复）；step=A6 逐步行；
            // both=并存（诊断）。老客户端不带该键 = 缺省 macro（v22 默认口径）；非法值回落
            // macro 并 LogWarning（不静默）。写 LAE.PicksMode（枚举器单源）+ 回显字段。
            string picks = string.IsNullOrEmpty(picksModeKey) ? "macro" : picksModeKey;
            if (picks != "macro" && picks != "step" && picks != "both")
            {
                UnityEngine.Debug.LogWarning($"[TideHeadless] picksMode '{picksModeKey}' 非法（认 macro/step/both），本局回落 macro");
                picks = "macro";
            }
            LegalActionEnumerator.PicksMode = picks;
            _lastPicksMode = picks;

            // 按局塑形 λ（2026-11-02 显式协议）：shapingSet=1 = 客户端显式下发，shapingLambda
            // 原样生效（0 = 关闭塑形只留终局 ±1，驱动内联动关掉无进展扣分）；旧客户端不带该组
            // 键 = 不调用，保持驱动默认 0.005（旧行为，塑形常开）
            if (shapingExplicit)
                driver.SetShapingLambda(shapingLambda);

            // 评估协议 v2（2026-11-22 配对终版）——座次与种子解析：
            //   modelSeatKey："0"=钉 P1 / "1"=钉 P2 / 空·未带·非法=掷币（非法 LogWarning 不静默）；
            //     仅 vs 模式（simpleai/script）消费，自对弈忽略（modelSeat 本就 -1）。
            //   seed：>0 钉对局种子（DeckRng 抽样 + InitGame 双流重播）；0/负=不钉缺省兼容——
            //     老客户端不发该键 → JsonUtility 缺省 0 → 旧行为零差。
            int seatPin;
            if (string.IsNullOrEmpty(modelSeatKey)) seatPin = -1;
            else if (modelSeatKey == "0" || modelSeatKey == "1") seatPin = int.Parse(modelSeatKey);
            else
            {
                UnityEngine.Debug.LogWarning($"[TideHeadless] modelSeat '{modelSeatKey}' 非法（认 \"0\"/\"1\"），本局回落掷币");
                seatPin = -1;
            }
            _lastSeed = seed > 0 ? seed : 0;

            // 2026-09-21 主题卡组口径（数据层迁移到 Cards.json + StreamingAssets/Card/，TestDecks 已删）：
            //   自对弈  = 主题整组（三套随机其一）vs 随机 30 张，随机换座，双方模型驱动；
            //   vs 脚本 = 模型卡组（modelTheme 命中→该主题整组，2026-10-30 红·快攻镜像口径；
            //              缺省随机 30）vs 随机主题整组，SimpleAI 按主题 AutoMatch 策略
            //              整回合自动打，obs/reward 恒为模型视角（info.modelSeat 判胜负、info.theme 分主题统计）。
            // 2026-11-15 训练池定案（矩阵 §0 P0-C）：随机 30 只抽 tag「训练池」（教学卡不参与训练）；
            // 主题整组（对手/镜像）不变。P-1 稳定下标后无身份登记步骤（manifest 已废弃，纯派生）。
            var pool = BattleDeckSources.TrainingPool();
            var themes = ThemeDeckCache.Where(t => t.deck.Count > 0).ToList();
            if (pool.Count == 0 || themes.Count == 0)
            {
                _opponentTheme = "";
                _modelTheme = "";
                _modelDoped = "";
                UnityEngine.Debug.LogError("[TideHeadless] 数据层缺失（训练池 "
                    + $"{pool.Count} 张 / 有效主题卡组 {themes.Count} 套）——TrainingCards.json 缺失或先运行 Tools/构建三色主题卡组");
                return driver.Reset(new List<CardData>(), new List<CardData>());
            }
            if (!_poolLogged)
            {
                _poolLogged = true;
                UnityEngine.Debug.Log($"[TideHeadless] 训练池 {pool.Count} 张（tag「训练池」；随机 30 张抽样口径，教学卡不参与）");
            }

            lock (DeckRngLock)
            {
                // 评估协议 v2（2026-11-22）：seed>0 先重播 DeckRng——本局卡组抽样（随机 30/掺卡/
                // 主题回落随机）与未钉座次时的掷币全部由该种子决定；钉 modelSeat 时掷币被跳过
                //（不消耗流位，同种子±座次的两半可各自独立重播）。
                int? rngSeed = seed > 0 ? seed : (int?)null;
                if (rngSeed.HasValue)
                    DeckRng = new System.Random(rngSeed.Value);

                // 2026-09-22 临时：评估基准暂只认红（绿/蓝脚本待重设计——随机基线下绿 100%/蓝 55%
                // 白送分污染总胜率）。reset 可指定主题 key（red/green/blue），空/找不到回落随机；
                // 仅 vs 脚本口径使用，自对弈不传（保持三主题训练多样性）。
                var theme = string.IsNullOrEmpty(themeKey)
                    ? default
                    : themes.FirstOrDefault(t => t.key == themeKey);
                if (theme.deck == null)
                    theme = themes[DeckRng.Next(themes.Count)];
                _modelTheme = "";
                _modelDoped = "";

                // 模型侧（script 模式=「模型主题侧」）卡组构建：simpleai 与 script 两口径共用
                //（2026-11-22 opponent="script" 归因取证——卡组口径必须与 vs 模式逐位一致，
                // 否则归因实验自引入变量）。返回 (卡组, 正牌技能卡 ID)——阶段 D2a（2026-11-22）：
                // 主题整组命中带其 skillCardId；随机 30/掺卡无构筑配置 → null（driver 回落
                // AutoPickSkillCard 旧径）。
                (List<CardData> deck, string skillCardId) BuildModelDeck()
                {
                    // 模型侧卡组：modelTheme 命中 → 该主题整组（红镜像等固定口径）；缺省 Cards 随机 30。
                    // 找不到主题 LogWarning 回落随机（不静默），info.modelTheme 回显实际命中的 key。
                    var modelDeck = BattleDeckSources.SampleRandomDeck(pool, RandomDeckSize, DeckRng);
                    string modelSkill = null;
                    if (!string.IsNullOrEmpty(modelThemeKey))
                    {
                        var modelTheme = themes.FirstOrDefault(t => t.key == modelThemeKey);
                        if (modelTheme.deck != null && modelTheme.deck.Count > 0)
                        {
                            modelDeck = modelTheme.deck;
                            modelSkill = modelTheme.skillCardId;
                            _modelTheme = modelTheme.key;
                        }
                        else
                        {
                            UnityEngine.Debug.LogWarning(
                                $"[TideHeadless] modelTheme '{modelThemeKey}' 未命中主题卡组，模型侧回落 Cards 随机 30");
                        }
                    }

                    // 阶段二掺卡口径（2026-11-17，AI训练说明 §0.1）：modelDoped 命中 → 模型侧改
                    // 掺卡卡组（主题整组每种 −1 + 训练池随机补足），优先级高于 modelTheme 整组；
                    // 未传/未命中 = 旧行为零差，info.modelDoped 回显实际命中 key。
                    //（掺卡卡组无正牌技能配置 → modelSkill 置 null 走 AutoPick）
                    if (!string.IsNullOrEmpty(modelDopedKey))
                    {
                        var dopedTheme = themes.FirstOrDefault(t => t.key == modelDopedKey);
                        if (dopedTheme.deck != null && dopedTheme.deck.Count > 0)
                        {
                            modelDeck = BattleDeckSources.BuildDopedDeck(dopedTheme.deck, pool, DeckRng);
                            modelSkill = null;
                            _modelTheme = "";
                            _modelDoped = dopedTheme.key;
                        }
                        else
                        {
                            UnityEngine.Debug.LogWarning(
                                $"[TideHeadless] modelDoped '{modelDopedKey}' 未命中主题卡组，模型侧保持原口径");
                        }
                    }
                    return (modelDeck, modelSkill);
                }

                if (opponent == "simpleai" || opponent == "script")
                {
                    var (modelDeck, modelSkill) = BuildModelDeck();

                    // 卡组绑定模型座次（deck1 恒 P1 / deck2 恒 P2）：脚本侧 = 主题整组，
                    // 策略按主题自动匹配（红快攻/绿慢速/蓝控制）；模型座次随机 = 先后手各半，
                    // modelSeat 钉死时按请求（评估协议 v2：24/24 均分不再靠掷币凑）。
                    // 技能（阶段 D2a）：双方各传卡组正牌技能（模型侧主题命中才有；脚本侧恒主题）。
                    _opponentTheme = theme.key;
                    bool modelIsP1 = seatPin >= 0 ? seatPin == 0 : DeckRng.Next(2) == 0;
                    if (opponent == "script")
                    {
                        // 全脚本局（2026-11-22「绿打红 0%」归因取证）：双方 SimpleAI+AutoMatch(各自
                        // 卡组) 自动打到终局，reset 响应即终态（done=true，不发 step）；胜负按
                        // info.winner × info.modelSeat（modelTheme 侧）判——与 vs 模式同表可比。
                        return modelIsP1
                            ? driver.ResetScriptGame(modelDeck, theme.deck, true, rngSeed, modelSkill, theme.skillCardId)
                            : driver.ResetScriptGame(theme.deck, modelDeck, false, rngSeed, theme.skillCardId, modelSkill);
                    }
                    var strategy = AiStrategy.AutoMatch(theme.deck);
                    return modelIsP1
                        ? driver.Reset(modelDeck, theme.deck, true, strategy, rngSeed, modelSkill, theme.skillCardId)
                        : driver.Reset(theme.deck, modelDeck, false, strategy, rngSeed, theme.skillCardId, modelSkill);
                }

                // 自对弈：主题整组 vs Cards 随机 30，主题侧随机先后手。
                // 阶段二掺卡口径（2026-11-17，AI训练说明 §0.1）：modelDoped 命中 → 一侧 =
                // 掺卡卡组（主题整组每种 −1 + 训练池随机补足），另一侧 = 旧口径
                // （主题整组 / 随机 30 各半抽），座次随机；不传 = 旧行为零差。
                // 技能（阶段 D2a 2026-11-22）：主题整组一侧带正牌技能；随机 30/掺卡侧 null（AutoPick）。
                _opponentTheme = "";
                if (!string.IsNullOrEmpty(modelDopedKey))
                {
                    var dopedTheme = themes.FirstOrDefault(t => t.key == modelDopedKey);
                    if (dopedTheme.deck != null && dopedTheme.deck.Count > 0)
                    {
                        _modelDoped = dopedTheme.key;
                        var dopedDeck = BattleDeckSources.BuildDopedDeck(dopedTheme.deck, pool, DeckRng);
                        bool oppIsTheme = DeckRng.Next(2) == 0;
                        var oppDeck = oppIsTheme
                            ? theme.deck
                            : BattleDeckSources.SampleRandomDeck(pool, RandomDeckSize, DeckRng);
                        var oppSkill = oppIsTheme ? theme.skillCardId : null;
                        return DeckRng.Next(2) == 0
                            ? driver.Reset(dopedDeck, oppDeck, rngSeed, null, oppSkill)
                            : driver.Reset(oppDeck, dopedDeck, rngSeed, oppSkill, null);
                    }
                    UnityEngine.Debug.LogWarning(
                        $"[TideHeadless] modelDoped '{modelDopedKey}' 未命中主题卡组，自对弈保持旧口径");
                }
                var randomDeck = BattleDeckSources.SampleRandomDeck(pool, RandomDeckSize, DeckRng);
                return DeckRng.Next(2) == 0
                    ? driver.Reset(theme.deck, randomDeck, rngSeed, theme.skillCardId, null)
                    : driver.Reset(randomDeck, theme.deck, rngSeed, null, theme.skillCardId);
            }
        }

        private const int RandomDeckSize = 30;

        /// <summary>训练池规模日志一次性闸（reset 每局调用——只首次打印，防刷屏）。</summary>
        private static bool _poolLogged;

        /// <summary>本局对手主题 key（vs 模式 red/green/blue；自对弈空串）——Serialize info.theme 用。
        /// 单连接单局串行，读写无竞态。</summary>
        private static string _opponentTheme = "";

        /// <summary>本局模型侧卡组实际命中的主题 key（vs 模式且 modelTheme 请求命中才有；其余空串）——
        /// Serialize info.modelTheme 用（Python 镜像口径断言）。</summary>
        private static string _modelTheme = "";

        /// <summary>本局掺卡口径实际命中的主题 key（modelDoped 请求命中才有；其余空串）——
        /// Serialize info.modelDoped 用（Python 阶段二口径断言，2026-11-17）。</summary>
        private static string _modelDoped = "";

        /// <summary>主题卡组缓存（编辑器 TCP 重启时置空重载；batchmode 进程内加载一次）。
        /// 阶段 D2a（2026-11-22 红绿0胜归因计划）：带正牌技能卡 ID（ThemeDecksWithSkills）——
        /// reset 透传 driver.InitGame，与 TideSim DeckSim/挑战/联机同源；此前 ThemeDecks() 丢
        /// 技能 → headless AutoPickSkillCard（卡组内最低锚价法术）与基线口径分叉。</summary>
        private static List<(string key, List<CardData> deck, string skillCardId)> _themeDecks;
        private static List<(string key, List<CardData> deck, string skillCardId)> ThemeDeckCache
            => _themeDecks ??= BattleDeckSources.ThemeDecksWithSkills();

        // DeckRng 去 readonly（2026-11-22 评估协议 v2）：seed 钉局时按局重播（恒在 DeckRngLock 内
        // 赋值/消费，线程安全口径不变）；缺省构造 = 旧行为时间种子。
        private static System.Random DeckRng = new System.Random();
        private static readonly object DeckRngLock = new object();
        // 评估协议 v2：本局实际种子（0=未钉），info.seed 回显供 Python 断言
        private static int _lastSeed;
        // v22 S2a：本局拾取行模式（macro/step/both），info.picksMode 回显供 Python 断言
        private static string _lastPicksMode;

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
                    // toPlay = 本停靠点决策座次（v2：Main=回合玩家 / 响应窗口=优先权持有方——
                    // 不再从 TurnPlayer 推断；Python 自对弈 GAE 座次修正沿用本字段）
                    toPlay = r.Done ? -1 : r.DecisionSeat,
                    winner = r.Winner == null ? "" : (ReferenceEquals(r.Winner, GameCore.Instance.Player1) ? "0" : "1"),
                    reason = r.Reason ?? "",
                    turn = r.Turn,
                    modelSeat = r.ModelSeat,
                    seed = _lastSeed, // 评估协议 v2（2026-11-22）：回显实际种子，Python 断言防老版静默忽略
                    picksMode = _lastPicksMode ?? "macro", // v22 S2a：回显拾取行模式（Python 断言防 v21 对端静默忽略）
                    theme = _opponentTheme ?? "",
                    modelTheme = _modelTheme ?? "",
                    modelDoped = _modelDoped ?? "",
                    // v2 版本握手（2026-09-30 契约第 5 节，防 2026-09-14 式静默炸）：
                    // Python reset 时断言逐值相等，不等报清晰错误（替代 reshape 炸）
                    // v8（2026-11-02 LAE 多目标）：NAction 8→11——直接引用 TideObservation.LayoutVersion
                    // 单源（旧本地常量与注释「单源同步」名不符实，已删）
                    layoutVersion = TideObservation.LayoutVersion,
                    dims = new HeadlessDims
                    {
                        nCard = TideObservation.NCard,
                        nGlobal = TideObservation.NGlobal,
                        nAction = TideObservation.NAction,
                        maxActions = OnnxTidePolicy.MaxActions,
                        maxCards = TideObservation.MaxCardsTotal, // v3：卡槽总数入握手（契约 §7）
                        // P-1（2026-11-19）：池尺寸入握手——Python 侧 embedding 表与 C# 派生同代
                        cardPoolSize = TideCardIndex.Capacity,
                        typePoolSize = CardIdentityService.MaxTypeIndex + 1,
                    },
                    decisionSeat = r.Done ? -1 : r.DecisionSeat,
                    // L3-A 循环治理（2026-11-18 方案 §5.4）：收口步外显 + 本回合步数——
                    // rollout 据此把收口回合的全部步 learner 置 0（不进 PPO），stats 出四指标
                    loopPruned = r.LoopPruned,
                    loopReason = r.LoopReason ?? "",
                    turnActions = r.TurnActions,
                    // 2026-11-23 行为仪表（B4 用户口径）：模型侧「牺牲 + 回收」链 / 14 费大哥落地。
                    // 每局累计、ResetCore 清零——step 响应里即本局迄今的计数（终局那步=全量）。
                    mSac = r.ModelSacrifices,
                    mRec = r.ModelRecalls,
                    mChain = r.ModelChain,
                    mAgros = r.ModelAgros,
                    mAgrosTurn = r.ModelAgrosTurnMin,
                    // 2026-11-23 追加：红/绿招牌操作（成长 / 法术斩杀线）
                    mGrow = r.ModelGrow,
                    mLandT1 = r.ModelLandT1,
                    mLandT3 = r.ModelLandT3,
                    mBlood = r.ModelBlood,
                    mBomb = r.ModelBomb,
                    mCombo = r.ModelCombo,
                    // 2026-11-23 考察项：攻击目标分类（是否理解"打已横置单位不吃反击"）
                    mAtkSafe = r.ModelAtkSafe,
                    mAtkRisky = r.ModelAtkRisky,
                    mAtkFace = r.ModelAtkFace,
                    mAtkAny = r.ModelAtkAny,
                    // S3-3 遗留：链进度闩位（只读仪表；无奖励）——
                    // S3-3（2026-11-26 二期 §5）：链进度闩位（decider 座位；终局步=全量）——
                    // aux 第三标签「本局是否到达目标态」的回传源
                    chainBits = r.ChainBits,
                    chainDone = r.ChainDone,
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

        // modelSeat 用字符串（2026-11-22 评估协议 v2）：JsonUtility 缺省 int=0，若用 int 型
        // 老客户端不发该键会被误钉 P1；字符串缺省 null=掷币。"0"=P1/"1"=P2。
        // seed int，0=不钉（缺省兼容），>0=钉对局种子。
        [Serializable] public class HeadlessRequest { public string op; public int action; public string opponent; public float shapingLambda; public int shapingSet; public string theme; public string modelTheme; public string modelDoped; public string modelSeat; public int seed; public string picksMode; }
        [Serializable] public class HeadlessObs { public List<float> cards; public List<float> globals; public List<float> actions; public int nActions; }
        // v2 布局握手字段（2026-09-30 契约第 5 节）：layoutVersion / dims / decisionSeat；
        // P-1（2026-11-19）dims 增 cardPoolSize/typePoolSize（稳定下标池尺寸两侧对账）
        [Serializable] public class HeadlessDims { public int nCard; public int nGlobal; public int nAction; public int maxActions; public int maxCards; public int cardPoolSize; public int typePoolSize; }
        [Serializable] public class HeadlessInfo { public int toPlay; public string winner; public string reason; public int turn; public int modelSeat; public string theme; public string modelTheme; public string modelDoped; public int layoutVersion; public HeadlessDims dims; public int decisionSeat; public bool loopPruned; public string loopReason; public int turnActions; public int seed; public string picksMode;
            // S3-3 遗留：链进度闩位（只读仪表；2026-11-28 链奖励已删）
            public int chainBits; public bool chainDone;
            // 2026-11-23 行为仪表（B4 用户口径）：模型侧「牺牲 + 回收」链与 14 费大哥落地（每局累计）
            public int mSac; public int mRec; public int mChain; public int mAgros; public int mAgrosTurn;
            // 2026-11-23 追加：绿成长（沃土打出 / T1·T3 地数）+ 红法术线（血偿/炸弹/同回合斩杀尝试）
            public int mGrow; public int mLandT1; public int mLandT3; public int mBlood; public int mBomb; public int mCombo;
            // 2026-11-23 考察项：攻击目标分类——safe=打已横置且无警戒（不吃反击）/ risky=未横置或有警戒 / face=打角色
            public int mAtkSafe; public int mAtkRisky; public int mAtkFace; public int mAtkAny; }
        [Serializable] public class HeadlessResponse { public string op; public HeadlessObs obs; public float reward; public bool done; public HeadlessInfo info; }
    }
}
