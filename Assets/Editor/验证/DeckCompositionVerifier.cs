using System.Linq;
using CardCore;
using UnityEditor;
using UnityEngine;

namespace CardCore.Editor.Tests
{
    /// <summary>
    /// 卡组构成检测（编辑器口，2026-10-15）：对 Card/ 目录下全部卡组跑
    /// SynergyUI.DeckCompositionValidator——主题双色纯度（红/绿/蓝+灰）+ 断链 +
    /// 三主题卡组存在性。违色/断链逐条 LogError（Console 可定位到具体卡与费用面），
    /// 结尾汇总通过率。与 ThemeDeckBuilder 构建自检、卡组构筑界面（DeckBuilderScreen）
    /// 共用同一验证器——本菜单=全量扫，那两处=源头拦。
    /// </summary>
    public static class DeckCompositionVerifier
    {
        [MenuItem("Tools/卡组构成检测")]
        public static void Run()
        {
            var decks = SynergyUI.DeckSerializer.LoadAll()
                .Where(d => !string.IsNullOrEmpty(d.name))
                .ToList();
            if (decks.Count == 0)
            {
                Debug.LogWarning("[卡组构成] Card/ 目录下无卡组文件——先 Tools/构建三色主题卡组 或在卡组构筑界面保存");
                return;
            }

            int bad = 0;
            foreach (var deck in decks)
            {
                var report = SynergyUI.DeckCompositionValidator.Check(deck);
                if (report.Ok)
                {
                    Debug.Log($"[卡组构成] ✔ {SynergyUI.DeckCompositionValidator.Describe(report)}");
                    continue;
                }
                bad++;
                Debug.LogError($"[卡组构成] ✘ {SynergyUI.DeckCompositionValidator.Describe(report)}");
                foreach (var v in report.Violations)
                    Debug.LogError($"[卡组构成] {report.DeckName}｜{v.CardName} ×{v.Copies}｜{v.Surface}｜" +
                        $"{SynergyUI.DeckCompositionValidator.ColorZh(v.Color)}{v.Amount:0.#}——主题卡组只允许主题色+灰");
                foreach (var id in report.MissingIds)
                    Debug.LogError($"[卡组构成] {report.DeckName}｜断链卡 {id}——Cards.json 无此 ID（卡内容已改/未重新生成卡组）");
            }

            // 三主题卡组存在性（AI 对战随机三色/教学的依赖底座）——缺失为警告非错误：
            // 当前盘面允许只保留单主题（如仅 红·快攻），三套齐备才不是开口
            foreach (var name in new[] { ThemeDeckBuilder.RedDeckName, ThemeDeckBuilder.GreenDeckName, ThemeDeckBuilder.BlueDeckName })
                if (decks.All(d => d.name != name))
                    Debug.LogWarning($"[卡组构成] 主题卡组缺失：{name}.json（AI 对战「随机三色」并池依赖；Tools/构建三色主题卡组 生成）");

            Debug.Log($"[卡组构成] 完成：{decks.Count} 套卡组，{decks.Count - bad} 通过 / {bad} 不通过");
        }
    }
}
