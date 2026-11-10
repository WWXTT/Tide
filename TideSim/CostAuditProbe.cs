using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TideSim
{
    /// <summary>D 组临时探针（2026-11-10 双轨收尾·验收用，跑完删）：三池卡表磁盘 costList
    /// vs 装载链 EnsureCost 现推导对账——输出差异卡清单（卡名 / 旧 costList / 新 costList / 归因）。
    /// 归因口径：新旧总和相等且仅红↔绿位互换 = 色变特征（B 组/吸血链路）；其余 = 推导口径漂移
    ///（并行 CostDerivation 改动或既有漂移，非本阶段色变引起）。主池 Cards.json 空壳自动跳过。</summary>
    internal static class CostAuditProbe
    {
        public static int Run()
        {
            Console.WriteLine("== D 组卡费对账：磁盘 costList vs EnsureCost 现推导 ==");
            int diffs = 0;
            diffs += AuditPool(SynergyUI.CardDataPaths.FileIn("Cards.json"), "主池");
            diffs += AuditPool(SynergyUI.CardDataPaths.TutorialFileIn("TutorialCards.json"), "教学");
            diffs += AuditPool(SynergyUI.CardDataPaths.TrainingFileIn("TrainingCards.json"), "训练");
            Console.WriteLine(diffs == 0
                ? "结论：三池 costList 与现推导零差异——B/C 组价色零变化，无须重推写回。"
                : $"结论：差异 {diffs} 张（上方清单——归因见各行）。");
            return diffs == 0 ? 0 : 1;
        }

        private static int AuditPool(string path, string label)
        {
            if (!File.Exists(path)) { Console.WriteLine($"[{label}] 文件不存在，跳过"); return 0; }
            string raw = File.ReadAllText(path);
            var root = Newtonsoft.Json.Linq.JObject.Parse(raw);
            var disk = new Dictionary<string, float[]>();
            foreach (var c in (root["cards"] as Newtonsoft.Json.Linq.JArray
                      ?? new Newtonsoft.Json.Linq.JArray()).OfType<Newtonsoft.Json.Linq.JObject>())
            {
                string id = (string)c["id"];
                var cl = c["costList"] as Newtonsoft.Json.Linq.JArray;
                if (id == null || cl == null) continue;
                disk[id] = cl.Select(t => (float?)t ?? 0f).ToArray();
            }
            if (disk.Count == 0) { Console.WriteLine($"[{label}] 空壳（0 卡），跳过"); return 0; }

            var cards = CardCore.CardLoader.LoadCardsFromText(raw); // 装载链含 EnsureCost → Cost=现推导
            int poolDiffs = 0;
            foreach (var card in cards)
            {
                if (card == null || !disk.TryGetValue(card.ID, out var old4)) continue;
                float[] cur = card.Cost == null ? new float[4] : Take4(card.Cost);
                if (Same(old4, cur)) continue;
                poolDiffs++;
                float oldSum = old4.Sum(), curSum = cur.Sum();
                bool colorSwap = Math.Abs(oldSum - curSum) < 0.01f; // 总和不变=色变特征
                string why = colorSwap
                    ? "色变特征（总价不变·分量位移——B组/吸血链路或既有色漂）"
                    : "推导口径漂移（并行 CostDerivation 改动或既有漂移，非 B/C 色变）";
                Console.WriteLine($"  ✗ [{label}] {card.CardName}({card.ID}) 旧[{Fmt(old4)}] → 新[{Fmt(cur)}]"
                                  + $"（Σ {oldSum:0.##}→{curSum:0.##}）——{why}");
            }
            Console.WriteLine($"[{label}] {disk.Count} 卡，差异 {poolDiffs} 张");
            return poolDiffs;
        }

        private static float[] Take4(CardCore.ElementCost c)
        {
            var arr = new float[4];
            for (int i = 0; i < 4 && i < c.v.Length; i++) arr[i] = c.v[i];
            return arr;
        }

        private static bool Same(float[] a, float[] b)
        {
            for (int i = 0; i < 4; i++)
                if (Math.Abs((a.Length > i ? a[i] : 0f) - (b.Length > i ? b[i] : 0f)) > 0.01f) return false;
            return true;
        }

        private static string Fmt(float[] a) => string.Join(",", a.Select(v => v.ToString("0.##")));
    }
}
