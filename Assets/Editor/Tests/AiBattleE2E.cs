using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CardCore;
using SynergyUI;
using UnityEditor;
using UnityEngine;

namespace CardCore.Editor.Tests
{
    /// <summary>
    /// AI 自动对战端到端验证（Unity 测试框架 + 菜单双入口）：
    /// 双 SimpleAI 打完整局，控制台播报全程，跑到出错或游戏结束。
    /// 同时验证表现层契约——播报器订阅与 BattleScreen（真实表现层）完全相同的事件集合，
    /// 若事件面不足以还原一局对局，这里就会暴露缺口。
    /// </summary>
    public static class AiBattleE2E
    {
        private const string Tag = "[对局]";

        [MenuItem("Tools/卡牌核心/AI 自动对战验证")]
        public static void RunFromMenu()
        {
            if (!TryLoadRandomTestDecks(out var deck1, out var deck2))
            {
                Debug.LogError($"{Tag} 无法从 TestDecks 目录加载随机卡组（Configs/TestDecks/*.json）");
                return;
            }
            var result = new AiBattleDriver().RunFullGame(deck1, deck2, maxTurns: 100);
            Debug.Log($"{Tag} 菜单入口结果：{(result.Completed ? $"完成（胜者 {Name(result.Winner)}，{result.Reason}，共 {result.TotalTurns} 回合）" : result.TurnLimitReached ? "到达回合上限" : "异常中止")}\n错误 {result.Errors.Count} 条，播报 {result.AnnouncedLines} 行");
        }

        /// <summary>标准卡组：纯非仪式卡（仪式验证走全仪式压力口径；AI 对战当前测不到仪式，移出）。</summary>
        public static List<CardData> LoadStandardDeck()
            => LoadTestCards().Where(c => !RitualSystem.IsRitual(new CardWrapper(c))).ToList();

        /// <summary>全仪式卡组（压力口径）：开局仪式占满手牌、连环顶替、小卡组疲劳收尾。</summary>
        public static List<CardData> LoadRitualHeavyDeck()
            => LoadTestCards().Where(c => RitualSystem.IsRitual(new CardWrapper(c))).ToList();

        private static List<CardData> LoadTestCards()
        {
            string path = Path.Combine(Application.dataPath, "Configs/TestCreatureCards.json");
            return File.Exists(path) ? CardLoader.LoadCardsFromText(File.ReadAllText(path)) : new List<CardData>();
        }

        /// <summary>列出 TestDecks 目录下所有卡组 JSON 绝对路径（排除 .meta）。</summary>
        private static string[] ListTestDecks()
        {
            string dir = Path.Combine(Application.dataPath, "Configs", "TestDecks");
            if (!Directory.Exists(dir)) return new string[0];
            return Directory.GetFiles(dir, "*.json")
                .Where(f => !f.EndsWith(".meta"))
                .ToArray();
        }

        /// <summary>随机加载两套 TestDecks 卡组（可能同色/异色）。无卡组或解析失败返回 false。</summary>
        private static bool TryLoadRandomTestDecks(out List<CardData> deck1, out List<CardData> deck2)
        {
            deck1 = deck2 = null;
            var decks = ListTestDecks();
            if (decks.Length == 0) return false;
            deck1 = CardLoader.LoadCardsFromText(File.ReadAllText(decks[UnityEngine.Random.Range(0, decks.Length)]));
            deck2 = CardLoader.LoadCardsFromText(File.ReadAllText(decks[UnityEngine.Random.Range(0, decks.Length)]));
            return deck1 != null && deck1.Count > 0 && deck2 != null && deck2.Count > 0;
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

    /// <summary>
    /// 双 AI 自动对战驱动器：双方 SimpleAI 轮流打完整局，跑到出错或游戏结束。
    /// 所有范围/目标选择自动应答（双方 IsAI=true 走 TargetSelectionService 的自动路径）。
    /// 编辑器上下文无帧泵：SimpleAI 末尾的 EndTurn 只推进到结束阶段，此处补一次
    /// CheckPhaseTransition 完成 End→Standby 折返（同 CardPipelineVerifier.EndTurnPumped 惯例）。
    /// </summary>
    public sealed class AiBattleDriver
    {
        public BattleRunResult RunFullGame(List<CardData> deckSpec, int maxTurns = 100)
            => RunFullGame(deckSpec, deckSpec, maxTurns);

        /// <summary>双卡组对战：玩家 1 用 deck1、玩家 2 用 deck2（各 BuildDeck copiesPerCard=1）。</summary>
        public BattleRunResult RunFullGame(List<CardData> deck1, List<CardData> deck2, int maxTurns = 100)
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
                // 变形目标形态解析器：组合根注入（编辑器无头路径独立注入）
                CardCore.Attribute.MorphSystem.ResolveMorphTarget = CardCatalog.GetById;
                core.InitGame(CardLoader.BuildDeck(deck1, 1), CardLoader.BuildDeck(deck2, 1));
                core.Player1.IsAI = true; // 选择全自动
                core.Player2.IsAI = true;

                // 测试口径：双方开局元素池预置 5 点灰色元素——加速中高费随从（关键词卡多为 5-8 费）
                // 出场互殴，让关键词行为在对局内真正得到触发（费用门槛仍受地牌槽上限约束）
                foreach (var p in new[] { core.Player1, core.Player2 })
                {
                    var bank = core.ElementPool.GetPool(p).AvailableMana;
                    bank[ManaType.Gray] = (bank.TryGetValue(ManaType.Gray, out var g) ? g : 0) + 5;
                }

                // 棋盘占用层（派生，单向读核心）：为碾压关键词注入邻接解析（核心不绑棋盘，宿主接线）
                board = new GameBoard.BoardState(core, core.Player1, core.Player2,
                    GameBoard.HalfFieldData.Flat(), GameBoard.HalfFieldData.Flat());
                board.EnableAutoResync();
                CombatSystem.AdjacentResolver = board.Neighbors;

                for (int turn = 0; turn < maxTurns && !gameOver; turn++)
                {
                    try
                    {
                        ai.TakeTurn(ctrl);                          // 内部已 EndTurn（不折返）
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
