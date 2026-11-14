using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.Attribute;
using SynergyUI;

namespace TideSim
{
    /// <summary>规则探针（2026-11-05 收口，常驻 QA；2026-11-14 夹具更新）：
    /// ① 充能系统动作化端到端——宣入栈 → 结算期反问代选色（AI 取最大余量）→ 免费 +1 层
    ///    同色费用降低 → 净费 -1 → 同回合不可再充；探针卡=卡池中**声明 chargeAbility** 的卡
    ///    （旧硬编码「活体炸弹」已无充能资格，2026-11-14 改为按资格选卡）；
    /// ② 活体炸弹声明域——逐原子目标制（PerAtomTargets）+ 参照域=己方战场:生物 +
    ///    主域=对方战场:生物/对方角色（2026-11-13 改版后的行域；旧「己方结界;己方生物」期望作废）。
    /// 用法：TideSim --charge-probe（退出码=失败项数）。</summary>
    internal static class ChargeProbe
    {
        private static int _fail;

        private static void Check(bool ok, string what)
        {
            Console.WriteLine($"    {(ok ? "✓" : "✗")} {what}");
            if (!ok) _fail++;
        }

        public static int Run()
        {
            var catalog = CardCatalog.LoadAll().ToList();
            // 2026-11-14 夹具修正：探针卡按「声明 chargeAbility」选（活体炸弹已无充能资格）；
            // 优先带费卡（充能净费 -1 需要可充颜色），无则退回灰源固定卡。
            var probe = catalog.FirstOrDefault(c => c.ChargeAbility && c.Cost != null && c.Cost.Total > 0f)
                        ?? catalog.FirstOrDefault(c => c.ChargeAbility)
                        ?? catalog.FirstOrDefault(c => c.CardName == "教学·灰源");
            if (probe == null) { Console.WriteLine("无可用充能探针卡"); return 1; }

            var deck1 = new List<CardData>();
            for (int i = 0; i < 6; i++) deck1.Add(probe);
            var deck2 = new List<CardData>();
            for (int i = 0; i < 6; i++) deck2.Add(probe);

            var core = GameCore.Instance;
            core.InitGame(deck1, deck2);
            var p1 = core.Player1;
            var p2 = core.Player2;
            p1.IsAI = true;
            p2.IsAI = true;

            AdvanceToMain(core, p1);
            var zm = core.ZoneManager;
            core.ElementPool.AddMana(p1, ManaType.Red, null, 10);

            var card = zm.GetCards(p1, Zone.Hand).FirstOrDefault();
            Check(card != null, "手牌非空");
            if (card == null) return _fail;

            var before = GameActions.GetCardCost(card, 0);
            bool ok = GameActions.DeclareCharge(core, p1, card, out var reason);
            Check(ok, $"DeclareCharge 成功（{reason ?? "ok"}）");

            int guard = 0;
            while (!core.StackEngine.IsEmpty && guard++ < 20) GameActions.DrainStack(core);

            var after = GameActions.GetCardCost(card, 0);
            int layers = 0;
            foreach (var color in new[] { ManaType.Gray, ManaType.Red, ManaType.Blue, ManaType.Green })
            {
                var cid = CounterRules.CostDownCounterFor(color);
                if (cid != null) layers += card.GetCounterCount(cid);
            }
            Check(layers == 1, $"充能落 1 层（实际 {layers}）");
            Check(Math.Abs(after.Total - (before.Total - 1f)) < 0.001f,
                  $"净费 -1（{before.Total} → {after.Total}）");

            bool again = GameActions.CanCharge(core, p1, card, out var reason2);
            Check(!again, $"同回合不可再充（{reason2}）");

            // ② 活体炸弹声明域（2026-11-13 改版后）：逐原子目标制 + 主域=对方生物/角色 +
            //    参照域=己方战场:生物；旧「己方结界;己方生物」主域期望已作废。
            var bomb = catalog.FirstOrDefault(c => c.CardName == "活体炸弹");
            Check(bomb != null, "卡池含活体炸弹");
            if (bomb != null)
            {
                var def = CardEffectConverter.ConvertAll(bomb.Effects, bomb.ID).FirstOrDefault();
                var atom = def?.Effects?.FirstOrDefault(a => a != null && a.Type == AtomicEffectType.BurstDamage);
                Console.WriteLine($"    诊断：PerAtomTargets={def?.PerAtomTargets} 原子={atom?.Type}"
                                  + $" 主域={(atom?.Axis?.Zh ?? "null")} 参照域={(atom?.ReferenceAxis?.Zh ?? "null")}"
                                  + $" 代价行={bomb.Effects?.FirstOrDefault()?.Costs?.Count ?? 0}");
                Check(def != null && def.PerAtomTargets, "活体炸弹=逐原子目标制（无 header 域）");
                Check(atom != null, "活体炸弹原子=BurstDamage");
                Check(atom?.Axis != null && atom.Axis.Zh.Contains("对方战场:生物"),
                      $"主域=对方战场:生物（实 {atom?.Axis?.Zh ?? "null"}）");
                Check(atom?.ReferenceAxis != null && atom.ReferenceAxis.Zh.Contains("己方战场:生物"),
                      $"参照域=己方战场:生物（实 {atom?.ReferenceAxis?.Zh ?? "null"}）");
                Check(bomb.Effects?.FirstOrDefault()?.Costs?.Count > 0,
                      "活体炸弹带代价行（2026-11-13 改版：参照生物为代价）");
            }

            Console.WriteLine(_fail == 0 ? "ChargeProbe PASS" : $"ChargeProbe FAIL {_fail}");
            return _fail;
        }

        private static void AdvanceToMain(GameCore core, Player p1)
        {
            int guard = 0;
            while (guard++ < 20
                   && (core.TurnEngine.CurrentPhase == null
                       || core.TurnEngine.CurrentPhase.Phase != PhaseType.Main))
            {
                var ph = core.TurnEngine.CurrentPhase == null
                    ? PhaseType.Standby
                    : core.TurnEngine.CurrentPhase.Phase;
                if (ph == PhaseType.Standby) GameActions.SkipElementPool(core, core.TurnEngine.TurnPlayer);
                else core.TurnEngine.CheckPhaseTransition();
            }
        }
    }
}
