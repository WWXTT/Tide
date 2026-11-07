using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.Attribute;
using SynergyUI;

namespace TideSim
{
    /// <summary>规则探针（2026-11-05 收口，常驻 QA）：
    /// ① 充能系统动作化端到端——宣入栈 → 结算期反问代选色（AI 取最大余量）→ 免费 +1 层
    ///    同色费用降低 → 净费 -1 → 同回合不可再充；
    /// ② 参照族声明域——活体炸弹卡必须是逐原子目标制（PerAtomTargets），第一段轴=
    ///    己方战场:结界；己方战场:生物（伤害行 2026-11-05 扩含结界、实例 kinds=[1] 收窄落己方；
    ///    不含角色 selector），第二段由 handler 代码常量承担。
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
            var probe = catalog.FirstOrDefault(c => c.CardName == "活体炸弹")
                        ?? catalog.FirstOrDefault(c => c.Supertype == Cardtype.Spell);
            if (probe == null) { Console.WriteLine("无可用探针卡"); return 1; }

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

            // ② 参照族声明域（活体炸弹）：不得再带 header 域，第一段轴=己方战场:生物、不含角色
            var bomb = catalog.FirstOrDefault(c => c.CardName == "活体炸弹");
            Check(bomb != null, "卡池含活体炸弹");
            if (bomb != null)
            {
                var def = CardEffectConverter.ConvertAll(bomb.Effects, bomb.ID).FirstOrDefault();
                Check(def != null && def.PerAtomTargets, "活体炸弹=逐原子目标制（无 header 域）");
                var atom = def?.Effects?.FirstOrDefault(a => a != null && a.Type == AtomicEffectType.BurstDamage);
                Check(atom != null && atom.Axis != null, "活体炸弹原子挂轴（不为空）");
                if (atom?.Axis != null)
                {
                    bool hasChar = atom.Axis.Selectors.Any(s => TargetRegionRules.IsCharacter(s.Region));
                    // 2026-11-05 伤害行扩含结界：行域=双方战场:结界;己方战场:生物，实例 kinds=[1]
                    // 把「双方结界」段收窄落己方（NarrowByKinds·Both 落单侧）→ 轴=己方结界;己方生物
                    Check(atom.Axis.Zh == "己方战场:结界；己方战场:生物" && !hasChar,
                          $"第一段轴=己方战场:结界；己方战场:生物（伤害行已扩含结界·2026-11-05·kinds[1] 收窄）且不含角色（实 {atom.Axis.Zh}）");
                }
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
