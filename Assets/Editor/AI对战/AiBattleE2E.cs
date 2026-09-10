using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CardCore;
using CardCore.AI.NeuralEnv;
using SynergyUI;
using UnityEngine;

namespace CardCore.Editor.Tests
{
    /// <summary>
    /// AI 自动对战端到端验证（Unity 测试框架入口，编辑器对战驱动与卡池加载共用设施）：
    /// 双 SimpleAI 打完整局，控制台播报全程，跑到出错或游戏结束。
    /// 同时验证表现层契约——播报器订阅与 BattleScreen（真实表现层）完全相同的事件集合，
    /// 若事件面不足以还原一局对局，这里就会暴露缺口。
    /// </summary>
    public static class AiBattleE2E
    {

        /// <summary>标准卡组：纯非仪式卡（仪式验证走全仪式压力口径；AI 对战当前测不到仪式，移出）。</summary>
        public static List<CardData> LoadStandardDeck()
            => LoadTestCards().Where(c => !RitualSystem.IsRitual(new CardWrapper(c))).ToList();

        /// <summary>全仪式卡组（压力口径）：开局仪式占满手牌、连环顶替、小卡组疲劳收尾。</summary>
        public static List<CardData> LoadRitualHeavyDeck()
            => LoadTestCards().Where(c => RitualSystem.IsRitual(new CardWrapper(c))).ToList();

        private static List<CardData> LoadTestCards()
        {
            string path = Path.Combine(Application.dataPath, "Configs/TestDecks/TestCreatureCards.json");
            return File.Exists(path) ? CardLoader.LoadCardsFromText(File.ReadAllText(path)) : new List<CardData>();
        }

        internal static string Name(Entity e) => MatchLogRenderer.Name(e);
    }

    /// <summary>一局自动对战的结果。</summary>
    public class BattleRunResult
    {
        public bool Completed;
        public bool TurnLimitReached;
        public Player Winner;
        public string Reason;
        public int TotalTurns;
        public readonly List<string> Errors = new List<string>();
        public int AnnouncedLines;
        /// <summary>对局日志导出路径（P2b：RunFullGame finally 落盘；null=无条目未导出）</summary>
        public string LogPath;
    }

    /// <summary>
    /// 控制台播报器（P2b 薄壳化）：不再逐事件订阅——订阅 MatchLogService.Appended，
    /// 每条事件经 MatchLogRenderer 渲染成行后 Debug.Log（原 20 个回调已迁入运行时渲染器，
    /// 表现层契约说明随迁至 MatchLogRenderer 类注释；LineCount 语义不变，多行块按 \n 计数）。
    /// </summary>
    public sealed class ConsoleAnnouncer
    {
        private int _lines;

        public int LineCount => _lines;

        public void Attach()
        {
            MatchLogService.EnsureStarted();
            MatchLogService.Appended += OnAppended;
        }

        public void Detach()
        {
            MatchLogService.Appended -= OnAppended;
        }

        private void OnAppended(MatchLogEntry entry)
        {
            var line = MatchLogRenderer.Render(entry.Event);
            if (string.IsNullOrEmpty(line)) return;
            _lines += line.Split('\n').Length;
            Debug.Log(line);
        }
    }

    /// <summary>回合大脑选择：脚本启发式 SimpleAI / ONNX 神经网络策略（编辑器对战工具口径）。</summary>
    public enum AiBrain { Script, Neural }

    /// <summary>
    /// 双 AI 自动对战驱动器：双方轮流打完整局，跑到出错或游戏结束（默认双 SimpleAI，
    /// 可按座位换 ONNX 神经网络大脑——见 RunFullGame 双大脑重载）。
    /// 所有范围/目标选择自动应答（双方 IsAI=true 走 TargetSelectionService 的自动路径）。
    /// 编辑器上下文无帧泵：SimpleAI 末尾的 EndTurn 只推进到结束阶段，此处补一次
    /// CheckPhaseTransition 完成 End→Standby 折返（同 CardPipelineVerifier.EndTurnPumped 惯例）。
    /// </summary>
    public sealed class AiBattleDriver
    {
        public BattleRunResult RunFullGame(List<CardData> deckSpec, int maxTurns = 100)
            => RunFullGame(deckSpec, deckSpec, maxTurns);

        /// <summary>双卡组对战（双 SimpleAI）：玩家 1 用 deck1、玩家 2 用 deck2（各 BuildDeck copiesPerCard=1）。</summary>
        public BattleRunResult RunFullGame(List<CardData> deck1, List<CardData> deck2, int maxTurns = 100)
            => RunFullGame(deck1, deck2, maxTurns, AiBrain.Script, AiBrain.Script, null);

        /// <summary>
        /// 双卡组 + 双大脑对战：brain 为 Neural 的座位整回合由 ONNX 策略驱动（NeuralAI，
        /// 时序镜像训练 driver，非 SimpleAI 时序）。policy 由调用方提供并管理生命周期；
        /// 双 Neural 共用同一 policy —— 单 rstate 链贯穿全局，与训练自对弈同口径。
        /// 指定了 Neural 但 policy 为 null 时该座位回落 SimpleAI 并记错误。
        /// </summary>
        public BattleRunResult RunFullGame(List<CardData> deck1, List<CardData> deck2, int maxTurns,
            AiBrain brain1, AiBrain brain2, OnnxTidePolicy policy)
        {
            var result = new BattleRunResult();
            var announcer = new ConsoleAnnouncer();
            var gameOver = false;
            GameBoard.BoardState board = null;

            void OnGameOver(GameOverEvent e)
            {
                gameOver = true;
                result.Completed = true;
                result.Winner = e.Winner;
                result.Reason = e.Reason.ToString();
                result.TotalTurns = e.TotalTurns;
            }

            EventManager.Instance.Subscribe<GameOverEvent>(OnGameOver);
            announcer.Attach();
            try
            {
                if (deck1 == null || deck1.Count == 0 || deck2 == null || deck2.Count == 0)
                {
                    result.Errors.Add("卡组为空（玩家 1 或玩家 2 的卡组无卡，检查 JSON）");
                    return result;
                }

                var core = GameCore.Instance;
                var ctrl = new BattleController();
                var ai = new SimpleAI();
                // Neural 座位共用一个 NeuralAI 实例（TakeTurn 无跨回合状态；rstate 在 policy
                // 内单链贯穿双方决策点，镜像训练自对弈口径）。policy 缺失回落 SimpleAI。
                bool wantNeural = brain1 == AiBrain.Neural || brain2 == AiBrain.Neural;
                var neural = wantNeural && policy != null ? new NeuralAI(policy) : null;
                if (wantNeural && neural == null)
                    result.Errors.Add("指定了 Neural 大脑但未提供 ONNX 策略，该座位回落 SimpleAI");
                // 变形目标形态解析器：组合根注入（编辑器无头路径独立注入）
                CardCore.Attribute.MorphSystem.ResolveMorphTarget = CardCatalog.GetById;
                core.InitGame(CardLoader.BuildDeck(deck1, 1), CardLoader.BuildDeck(deck2, 1));
                core.Player1.IsAI = true; // 选择全自动
                core.Player2.IsAI = true;

                // 棋盘占用层（派生，单向读核心）：为碾压关键词注入邻接解析（核心不绑棋盘，宿主接线）
                board = new GameBoard.BoardState(core, core.Player1, core.Player2,
                    GameBoard.HalfFieldData.Flat(), GameBoard.HalfFieldData.Flat());
                board.EnableAutoResync();
                CombatSystem.AdjacentResolver = board.Neighbors;
                GameBoard.LinkAuraSystem.Attach(board); // 连接光环（三轨制）——与碾压邻接同惯例接线

                neural?.ResetEpisode(); // 新对局 GRU rstate 归零（镜像训练 env 复位口径）

                for (int turn = 0; turn < maxTurns && !gameOver; turn++)
                {
                    try
                    {
                        // 座位大脑分派：Neural 座位策略整回合，其余 SimpleAI（两者内部均已 EndTurn 不折返）
                        var isP1Turn = ReferenceEquals(core.TurnEngine.TurnPlayer, core.Player1);
                        if (neural != null && (isP1Turn ? brain1 : brain2) == AiBrain.Neural)
                            neural.TakeTurn(ctrl);
                        else
                            ai.TakeTurn(ctrl);
                        if (!gameOver)
                            core.TurnEngine.CheckPhaseTransition(); // 补 End→Standby 折返；游戏已结束则不开新回合
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"回合 {core.TurnEngine.TurnNumber}（{AiBattleE2E.Name(core.TurnEngine.TurnPlayer)}）：{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                        break;
                    }
                }

                if (!gameOver && result.Errors.Count == 0)
                {
                    result.TurnLimitReached = true;
                    result.TotalTurns = core.TurnEngine.TurnNumber;
                }

                return result;
            }
            finally
            {
                announcer.Detach();
                EventManager.Instance.Unsubscribe<GameOverEvent>(OnGameOver);
                CombatSystem.AdjacentResolver = null; // 撤销本局的棋盘接线（静态扩展点归零）
                GameBoard.LinkAuraSystem.Detach();    // 连接光环接线同步归零
                board?.Dispose();
                result.AnnouncedLines = announcer.LineCount;
                // P2b：对局日志按需导出（内存缓冲 → markdown 战报落盘）
                result.LogPath = MatchLogService.ExportMarkdown(
                    $"Logs/MatchLog_{DateTime.Now:yyyyMMdd_HHmmss}.md");
                Debug.Log($"[对局] 战报：{(result.Completed ? $"游戏结束（胜者 {AiBattleE2E.Name(result.Winner)}，{result.Reason}，{result.TotalTurns} 回合）" : result.TurnLimitReached ? $"到达回合上限 {maxTurns}" : "异常中止")}；播报 {result.AnnouncedLines} 行；错误 {result.Errors.Count} 条；日志 {(result.LogPath != null ? result.LogPath : "无条目未导出")}");
            }
        }
    }
}
