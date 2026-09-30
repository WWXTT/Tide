using System;
using System.Collections.Generic;
using System.Linq;

namespace TideServer.Verify
{
    /// <summary>
    /// 服务器端到端验证套件入口（2026-09-27，网络协议.md §14.2）——"每次修改完、实际运行前"的例行门禁。
    ///
    /// 分段与 Unity 回环验证器（NetProtocolLoopbackVerifier）编号对齐（纯 .NET 复刻 + 服务器专属段）：
    ///   V0 数据装载 | V1 事件投影+快照 | V2 intent 通道 | V3 反问回环 | V5 开局握手
    ///   V6 真实 socket 会话 | V7 同种子对拍 | V8 网关/子进程（服务器专属，跨进程真实拓扑）
    ///
    /// 模式：verify（全量）/ verify --quick（跳 V7 与 V8 长用例）/ verify --sections V1,V5（段过滤）。
    /// 断言模式平移 Unity 验证器：失败不中断、一跑收全部失败面；退出码 = 门禁结果。
    /// </summary>
    internal static class VerifySuite
    {
        private static int _pass;
        private static int _fail;
        private static readonly List<string> SectionSummaries = new List<string>();

        public static void Assert(bool condition, string message)
        {
            if (condition) { _pass++; }
            else
            {
                _fail++;
                Console.WriteLine($"[Verify] FAIL: {message}");
            }
        }

        public static void Section(string title)
            => Console.WriteLine($"[Verify] ══ {title} ══");

        public static void Log(string message)
            => Console.WriteLine($"[Verify] {message}");

        private sealed class SectionDef
        {
            public string Id;
            public string Title;
            public bool InQuick;                 // --quick 时保留
            public Action Runner;
        }

        public static int Run(Dictionary<string, string> options)
        {
            _pass = 0;
            _fail = 0;
            SectionSummaries.Clear();

            bool quick = options.ContainsKey("quick");
            var wanted = options.TryGetValue("sections", out var sectionsRaw)
                ? sectionsRaw.Split(',').Select(s => s.Trim().ToUpperInvariant()).ToHashSet()
                : null;

            // 预载卡池（各段共用；空池=数据问题，直接失败）
            var pool = SynergyUI.CardCatalog.LoadAll();
            Assert(pool.Count >= 2, $"卡池 ≥2（verify 依赖真实用户数据，实际 {pool.Count}）");
            // 复刻 AiBattleE2E.LoadStandardDeck：全池（仪式概念已随 0234192 合并退役，无需过滤）
            var deck = pool.ToList();
            Assert(deck.Count > 0, $"测试卡组非空（{deck.Count} 张）");
            Log($"卡池 {pool.Count} 张，测试卡组 {deck.Count} 张");

            var all = new List<SectionDef>
            {
                new SectionDef { Id = "V0", Title = "0. 数据装载", InQuick = true, Runner = RunLoadSection },
                new SectionDef { Id = "V1", Title = "1. 整局事件流与快照", InQuick = true, Runner = () => SectionInproc.RunProjection(deck) },
                new SectionDef { Id = "V2", Title = "2. intent 通道（枚举→映射→信封→分派）", InQuick = true, Runner = () => SectionInproc.RunIntent(deck) },
                new SectionDef { Id = "V3", Title = "3. 反问回环（选择协议）", InQuick = true, Runner = () => SectionInproc.RunSelector(deck) },
                new SectionDef { Id = "V5", Title = "5. 开局握手（卡组引用闭包校验）", InQuick = true, Runner = SectionInproc.RunHandshake },
                new SectionDef { Id = "V6", Title = "6. 真实 socket 会话（进程内 M2 冒烟）", InQuick = true, Runner = SectionInproc.RunSocket },
                new SectionDef { Id = "V7", Title = "7. 同种子对拍（M3 确定性）", InQuick = false, Runner = () => SectionInproc.RunDeterminism(deck) },
                new SectionDef { Id = "V8", Title = "8. 网关/子进程（跨进程真实拓扑）", InQuick = true, Runner = () => SectionGateway.Run(quick) },
            };

            Console.WriteLine($"[Verify] 开始：服务器端到端验证（{(quick ? "quick 档" : "全量档")}{(wanted != null ? $"，段过滤 {string.Join(",", wanted)}" : "")}）");
            var watch = System.Diagnostics.Stopwatch.StartNew();

            foreach (var def in all)
            {
                if (wanted != null && !wanted.Contains(def.Id)) continue;
                if (quick && !def.InQuick && wanted == null) continue; // quick 且未显式点名 → 跳过慢段

                Section(def.Title);
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var before = _fail;
                try { def.Runner(); }
                catch (Exception ex)
                {
                    _fail++;
                    Console.WriteLine($"[Verify] 段异常中止：{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                }
                SectionSummaries.Add($"{def.Id} {(before == _fail ? "✓" : "✗")} ({sw.Elapsed.TotalSeconds:0.0}s)");
            }

            watch.Stop();
            Console.WriteLine();
            foreach (var line in SectionSummaries) Console.WriteLine($"  {line}");
            Console.WriteLine($"[Verify] 完成：PASS {_pass} / FAIL {_fail}{(_fail == 0 ? " ✅" : " ❌")}（{watch.Elapsed.TotalSeconds:0.0}s）");
            return _fail == 0 ? 0 : 1;
        }

        private static void RunLoadSection()
        {
            var cards = SynergyUI.CardCatalog.LoadAll().Count;
            var effects = CardCore.EffectsLibrary.GetAll().Count;
            var atoms = CardCore.Attribute.AtomicEffectTable.GetAll().Count();
            Assert(cards > 0 && effects > 0 && atoms > 0,
                $"三表装载非空（卡 {cards}/效果 {effects}/原子 {atoms}——TideJson 读正式数据文件）");
            Log($"卡 {cards} / 效果 {effects} / 原子 {atoms}");
        }
    }
}
