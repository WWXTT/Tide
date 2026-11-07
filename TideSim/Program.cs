using System;
using System.Collections.Generic;
using System.Globalization;

namespace TideSim
{
    internal sealed class SimOptions
    {
        public int Games = 20;
        public int Seed = 20261104;
        public int MaxTurns = 200;
        public string Matchup = "all";
        public bool Detail;
        public bool Quiet;
        public bool MatchLog;   // 导出与 Unity 窗口同源的 MatchLog_*.md，用于逐行对账

        public static SimOptions Parse(string[] args)
        {
            var o = new SimOptions();
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (a == "--games" && i + 1 < args.Length) o.Games = int.Parse(args[++i]);
                else if (a == "--seed" && i + 1 < args.Length) o.Seed = int.Parse(args[++i]);
                else if (a == "--max-turns" && i + 1 < args.Length) o.MaxTurns = int.Parse(args[++i]);
                else if (a == "--matchup" && i + 1 < args.Length) o.Matchup = args[++i];
                else if (a == "--detail") o.Detail = true;
                else if (a == "--quiet") o.Quiet = true;
                else if (a == "--match-log") o.MatchLog = true;
            }
            return o;
        }
    }

    internal static class Program
    {
        private static int Main(string[] args)
        {
            try { Console.OutputEncoding = System.Text.Encoding.UTF8; }
            catch { /* console 不支持 UTF8 时按默认编码继续 */ }

            if (args.Length > 0 && (args[0] == "-h" || args[0] == "--help" || args[0] == "help"))
            {
                Usage();
                return 0;
            }

            // 场景回归（2026-11-04）：AI 策略闸门断言，不跑对局
            if (args.Length > 0 && args[0] == "--scenario")
                return ScenarioCheck.Run();

            // 临时探针：充能系统动作化端到端（验收用，跑完删）
            if (args.Length > 0 && args[0] == "--charge-probe")
                return ChargeProbe.Run();

            // 教学式固定盘面场景回归（2026-11-04）：红快攻（血偿/连击准入+拆波）+ 蓝·元素充盈
            //（潮痕碑/深潭碑 触发·不触发·每回合封顶）+ 自由分支引擎主干 7 种（2026-11-06：
            // 倒计时/运势/拼枪/死亡计数/团结/附加诅咒/附加祝福）三套串跑，一次命令覆盖全部场景
            if (args.Length > 0 && args[0] == "--tutorial-scenario")
            {
                int red = TutorialScenarioCheck.Run();
                int blue = BlueFullnessScenarioCheck.Run();
                int free = FreeBranchScenarioCheck.Run();
                return red == 0 && blue == 0 && free == 0 ? 0 : 1;
            }

            var options = SimOptions.Parse(args);
            if (options.Games <= 0) { Console.WriteLine("--games 必须 > 0"); return 2; }
            if (options.MaxTurns <= 0) { Console.WriteLine("--max-turns 必须 > 0"); return 2; }

            return DeckSim.Run(options);
        }

        private static void Usage()
        {
            Console.WriteLine("TideSim —— 三套预组无头对战模拟（当前 CardCore 规则）");
            Console.WriteLine();
            Console.WriteLine("用法：");
            Console.WriteLine("  TideSim [--games N=20] [--seed S=20261104] [--max-turns N=200]");
            Console.WriteLine("          [--matchup all|cross|mirror|red-blue|red-green|blue-green]");
            Console.WriteLine("          [--detail] [--quiet] [--match-log]");
            Console.WriteLine("  TideSim --scenario            场景回归（红快攻策略闸门直调断言）");
            Console.WriteLine("  TideSim --tutorial-scenario   教学式固定盘面回归（红快攻9场景 + 蓝充盈6场景 + 自由分支10场景）");
            Console.WriteLine("  TideSim --charge-probe        规则探针（充能系统动作化 + 参照族声明域）");
            Console.WriteLine();
            Console.WriteLine("口径：");
            Console.WriteLine("  · 策略显式绑定：红·快攻→RedAggro / 绿·慢速→GreenRamp / 蓝·控制→BlueControl");
            Console.WriteLine("  · 技能/衍生物：从预组 JSON 的 skillCardId/tokenBindings 传入 InitGame");
            Console.WriteLine("  · 每局独立 seed；换边轮替先手；P2 后手护甲=GameCore.SecondPlayerArmorBonus");
            Console.WriteLine("  · --detail 打印每回合手牌/出牌/技能/生命，用于和设计逐手对账");
            Console.WriteLine();
            Console.WriteLine("示例（先跑一局 dry-run）：");
            Console.WriteLine("  TideSim --games 1 --matchup red-blue --detail --seed 20261104");
        }
    }
}
