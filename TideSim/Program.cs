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
        /// <summary>固定排序（2026-11-15）：按卡组 JSON 的排列发牌，不做洗牌——
        /// 用来复现「固定起手」下的指定操作序列。</summary>
        public bool LockDeckOrder;

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
                else if (a == "--lock-order") o.LockDeckOrder = true;
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

            // 牺牲原子改版探针（2026-11-14 basics_10 修复验收）：域=候选声明 + handler 送墓入账
            if (args.Length > 0 && args[0] == "--sacrifice-probe")
                return SacrificeProbe.Run();

            // T7 暗影豹线探针（2026-11-15）：8/10 地盘面走完整一回合 → 牺牲 4/2 → 补地 → 豹落地
            if (args.Length > 0 && args[0] == "--panther-probe")
                return PantherLineProbe.Run();

            // 红套改版取证（2026-11-13）：己方仅 1 生物时两张直伤法术的连锁 + 代价只付一次
            if (args.Length > 0 && args[0] == "--chain-cast")
                return ChainCastCheck.Run();

            // 沃土「代价：一个敌方单位 +6 护甲」探针（2026-11-15 临时，验完删）
            if (args.Length > 0 && args[0] == "--bloom-cost-probe")
                return BloomCostProbe.Run();

            // 枯竭「全取」探针（2026-11-15 临时，验完删）：对手元素池一次抹光
            if (args.Length > 0 && args[0] == "--erase-all-probe")
                return EraseAllProbe.Run();

            // 手牌上限基值 7 / 自己回合无上限（2026-11-18 规则改版验收）
            if (args.Length > 0 && args[0] == "--hand-overflow-probe")
                return HandOverflowProbe.Run();

            // 蓝套可作地/产色探针（2026-11-18 基础运转验收）
            if (args.Length > 0 && args[0] == "--land-probe")
                return LandEligibilityProbe.Run();

            // 残局求解器·阶段1（2026-11-19）：本回合斩杀判定——合法动作枚举 + 试打 + 重建/重放回溯。
            // 口径：只解「打不打得死」，不解「要不要收手」；对手不响应=我方上界（见 EndgameSolver 注释）。
            if (args.Length > 0 && args[0] == "--lethal-probe")
                return EndgameSolver.Run();

            // 绿预组数据探针（2026-11-15 临时，跑完删）：推导费 / 选择模式 / 效果推导价 / 指纹
            if (args.Length > 0 && args[0] == "--green-probe")
            {
                var ids = new string[args.Length - 1];
                System.Array.Copy(args, 1, ids, 0, ids.Length);
                return GreenDeckProbe.Run(ids);
            }

            // 数据指纹复算（2026-11-13 临时）：手工改效果数据后回填 JSON hash 列
            if (args.Length > 1 && args[0] == "--data-hash")
            {
                var ids = new string[args.Length - 1];
                System.Array.Copy(args, 1, ids, 0, ids.Length);
                return DataHashProbe.Run(ids);
            }

            // 卡组校验·效果组合合法性（2026-11-13）：代价×门奖励互斥（活体炸弹改版回归）
            if (args.Length > 0 && args[0] == "--deck-rule")
                return DeckRuleCheck.Run();

            // 临时探针（2026-11-10 双轨收尾·D 组，跑完删）：三池磁盘 costList vs 现推导对账
            if (args.Length > 0 && args[0] == "--cost-audit")
                return CostAuditProbe.Run();

            // 临时探针（2026-11-10 R4 收尾·端到端计价锚 8 红取证，跑完删）：复现验证器失败夹具现值
            if (args.Length > 0 && args[0] == "--price-anchor")
                return PriceAnchorProbe.Run();

            // 教学式固定盘面场景回归（2026-11-04；2026-11-13 红套削弱版：血偿/活体炸弹准入+斩杀排序+拆波）
            // 蓝·元素充盈（潮痕碑/深潭碑 触发·不触发·每回合封顶）+ 自由分支引擎主干 7 种（2026-11-06：
            // 倒计时/运势/拼抢/死亡计数/团结/附加诅咒/附加祝福）三套串跑，一次命令覆盖全部场景
            if (args.Length > 0 && args[0] == "--tutorial-scenario")
            {
                int red = TutorialScenarioCheck.Run();
                int blue = BlueFullnessScenarioCheck.Run();
                int free = FreeBranchScenarioCheck.Run();
                return red == 0 && blue == 0 && free == 0 ? 0 : 1;
            }

            // 教学二关键词课回归（2026-11-09 建·2026-11-11 升格教学二）：basics_plus 配置结构 + 11 段引擎断言。
            // 独立参数（不并入 --tutorial-scenario 聚合）——段2 碾压「结界主目标直毁即时移墓→
            // 邻格溅射落空」为已知引擎×文本不符（见 docs/教学一进阶·引擎不符清单.md），两条红
            // 待引擎补齐后转绿再并入聚合口。
            if (args.Length > 0 && args[0] == "--keyword-scenario")
                return KeywordTutorialCheck.Run();

            // 狩猎探针（2026-11-14 删未横置闸+击杀重置；2026-11-15 目标改「生物∪角色比生命」）：
            // 横置强击/角色兜底/重置幂等/最低生命择敌/耐久体排除/角色参与比大小 六场景。
            if (args.Length > 0 && args[0] == "--hunt-reset")
                return HuntResetProbe.Run();

            // 冻地探针（2026-11-16 冻结扩展到地牌验收）：产轴收窄/候选解析/冻地执行/
            // 冻地不产色（付费规划跳过）/回合钩子（层>0 不恢复·回合末−1·归零恢复）
            if (args.Length > 0 && args[0] == "--freeze-land-probe")
                return FreezeLandProbe.Run();

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
            Console.WriteLine("  TideSim --keyword-scenario    教学二关键词课回归（basics_plus 结构 + 11 段引擎断言）");
            Console.WriteLine("  TideSim --hunt-reset          狩猎探针（生物∪角色比生命择敌 + 横置强击 + 击杀重置 + 幂等）");
            Console.WriteLine("  TideSim --chain-cast          连锁直伤探针（代价失败=效果无效 / 参照发动时快照 / 两张各付一次代价）");
            Console.WriteLine("  TideSim --deck-rule           卡组校验探针（代价×门奖励互斥；活体炸弹改版回归）");
            Console.WriteLine("  TideSim --data-hash <ids…>    数据内容指纹复算（手工改效果数据后回填 JSON hash 列）");
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
