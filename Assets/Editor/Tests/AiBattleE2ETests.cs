using System.Linq;
using CardCore;
using NUnit.Framework;
using UnityEngine;

namespace CardCore.Editor.Tests
{
    /// <summary>
    /// AI 自动对战端到端（Unity Test Framework）：
    /// 双 SimpleAI 整局对打 + 控制台播报（与 BattleScreen 相同事件面），直到出错或游戏结束。
    /// 引擎内 EventManager 的 handler 异常会 Debug.LogError → 测试框架自动判失败（"出错即停"）。
    /// </summary>
    public class AiBattleE2ETests
    {
        [Test]
        public void AiVsAi_标准卡组_整局无错()
        {
            var result = new AiBattleDriver().RunFullGame(AiBattleE2E.LoadStandardDeck(), maxTurns: 100);

            Assert.IsEmpty(result.Errors, "对局中出现异常:\n" + string.Join("\n", result.Errors));
            Assert.IsTrue(result.Completed || result.TurnLimitReached,
                $"既未结束也未到回合上限（当前回合 {result.TotalTurns}）");
            Assert.Greater(result.AnnouncedLines, 0, "播报器应产出对战文本（表现层事件面生效）");
            Assert.IsFalse(string.IsNullOrEmpty(result.LogPath), "对局日志应落盘导出（P2b）");
            Assert.IsTrue(System.IO.File.Exists(result.LogPath), $"对局日志文件应存在：{result.LogPath}");

            Debug.Log($"[Test] 标准卡组：{(result.Completed ? $"完成，胜者 {AiBattleE2E.Name(result.Winner)}（{result.Reason}，{result.TotalTurns} 回合）" : $"到达回合上限（{result.TotalTurns} 回合）")}，播报 {result.AnnouncedLines} 行");
        }

        [Test]
        public void AiVsAi_全仪式卡组_整局无错()
        {
            // 压力口径：开局 6 张仪式占满手牌 → 连环顶替 → 小卡组快速耗尽 → 疲劳收尾
            var result = new AiBattleDriver().RunFullGame(AiBattleE2E.LoadRitualHeavyDeck(), maxTurns: 100);

            Assert.IsEmpty(result.Errors, "对局中出现异常:\n" + string.Join("\n", result.Errors));
            Assert.IsTrue(result.Completed || result.TurnLimitReached,
                $"既未结束也未到回合上限（当前回合 {result.TotalTurns}）");

            Debug.Log($"[Test] 全仪式卡组：{(result.Completed ? $"完成，胜者 {AiBattleE2E.Name(result.Winner)}（{result.Reason}，{result.TotalTurns} 回合）" : $"到达回合上限（{result.TotalTurns} 回合）")}，播报 {result.AnnouncedLines} 行");
        }
    }
}
