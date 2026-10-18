using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CardCore;
using CardCore.Attribute;

namespace TideServer.Verify
{
    /// <summary>
    /// V10 战报映射（B1 日志中间层 2026-10-18）：MatchLogRenderer = 唯一「事件→文本」映射。
    /// a) 纯函数锚：构造典型事件断言观侧/中立两口径文本与语义类（含六类补缺面）；
    /// b) 无头对局锚：脚本化对局经 MatchLogService.Appended 流收集渲染行 + ExportMarkdown
    ///    导出落盘断言——「只看文字能还原全过程」的无头门禁。
    /// </summary>
    internal static class SectionLog
    {
        public static void Run()
        {
            // ============================ V10.a 唯一映射纯函数锚 ============================
            VerifySuite.Section("V10.a 唯一映射：观侧/中立双口径");
            var core = GameCore.Instance;
            core.Reset();
            ZoneContainer.Reseed(20261002);
            var filler = Enumerable.Range(0, 4).Select(i => new CardData
            {
                ID = $"V10_fill_{i}", CardName = "锚点生物" + i, Supertype = Cardtype.Creature, Power = 2, Life = 3,
            }).ToList();
            core.InitGame(CardLoader.BuildDeck(filler, 2), CardLoader.BuildDeck(filler, 2));
            var p1 = core.Player1;
            var p2 = core.Player2;
            Func<Player, bool> self = p => ReferenceEquals(p, p1);

            var unit = new CardWrapper(filler[0]);
            unit.SetController(p1);
            core.ZoneManager.GetZoneContainer(p1).Add(unit, Zone.Battlefield);

            MatchLogRenderer.Line L(IGameEvent e) => MatchLogRenderer.Render(e, self);
            void A(IGameEvent e, string needle, string cls, string what)
            {
                var line = L(e);
                VerifySuite.Assert(line.Text != null && line.Text.Contains(needle), $"{what}：含「{needle}」（实际：{line.Text ?? "空"}）");
                if (cls != null) VerifySuite.Assert(line.Class == cls, $"{what}：类={line.Class}（期望 {cls}）");
            }
            void N(IGameEvent e, string what)
                => VerifySuite.Assert(string.IsNullOrEmpty(L(e).Text), $"{what}：显式空行（过滤口径）");

            // ---- 回合/阶段骨架：观侧 vs 中立 ----
            var turn = L(new TurnStartEvent { TurnNumber = 7, TurnPlayer = p2 });
            VerifySuite.Assert(turn.Text.Contains("回合 7") && turn.Text.Contains("对手") && turn.Class == "log-line--turn",
                $"回合行（观侧=对手）：{turn.Text}");
            var turnNeutral = MatchLogRenderer.Render(new TurnStartEvent { TurnNumber = 7, TurnPlayer = p2 });
            VerifySuite.Assert(turnNeutral.Contains(MatchLogRenderer.Name(p2)), $"回合行（中立=实名）：{turnNeutral}");
            A(new PhaseStartEvent { Phase = PhaseType.Standby, ActivePlayer = p1 }, "〔准备阶段〕", "log-line--phase", "阶段行");

            // ---- 抽牌/打出/入场 ----
            A(new CardDrawEvent { Player = p1, DrawCount = 2, FirstDrawOfTurn = true }, "抽 2 张", "", "抽牌行（只报数量）");
            A(new CardPlayEvent { Player = p1, PlayedCard = unit, FromZone = Zone.Hand }, "使用 锚点生物0", "log-line--card", "打出行");
            A(new CardPutToBattlefieldEvent { Card = unit, Tapped = false, Source = EnterSource.CastPlayed }, "入场", "log-line--card", "入场行");
            A(new CardEnterHandEvent { Player = p1, Card = unit, IsDraw = false, FromZone = Zone.Graveyard }, "获得", "log-line--effect", "效果获得行");
            N(new CardEnterHandEvent { Player = p1, Card = unit, IsDraw = true, FromZone = Zone.Deck }, "逐张抽牌（私有信息）");
            A(new CardPlayEvent { Player = p1, PlayedCard = unit, FromZone = Zone.Graveyard }, "（墓地）", "log-line--card", "墓地使用标注");

            // ---- 六类补缺面：横置/钱包/区域移动/攻击细节/状态改写 ----
            A(new TapEvent { TappedEntity = unit }, "锚点生物0 横置", "", "横置行");
            A(new TapEvent { TappedEntity = unit, IsUntapping = true }, "重置", "", "重置行（TapEvent 反向）");
            A(new UntapEvent { UntappedEntity = unit }, "重置", "", "重置行（UntapEvent）");
            A(new ElementPoolPayEvent { Player = p1, PaidCost = new Dictionary<int, float> { { (int)ManaType.Red, 2 } }, SourceNote = "出牌" },
                "支付 红×2", "", "支付行（中文计价）");
            A(new ElementPoolGainEvent { Player = p1, GainedType = ManaType.White, Amount = 1, FromCard = unit }, "白", "log-line--effect", "临时元素获得行");
            A(new ElementPoolConsumeEvent { Player = p1, ManaType = ManaType.Gray, Amount = 3 }, "消耗 3 个灰", "log-line--effect", "临时元素消耗行");
            A(new CardDiscardEvent { Player = p1, Card = unit }, "弃置", "log-line--card", "弃置行");
            A(new CardMillEvent { Player = p1, Card = unit }, "磨牌", "log-line--card", "磨牌行");
            A(new CardExileEvent { Card = unit, FromZone = Zone.Deck }, "被除外（来自 牌库）", "log-line--card", "除外行");
            A(new CardReturnToHandEvent { Card = unit }, "回到手牌", "log-line--card", "回手行");
            A(new CardShuffleIntoDeckEvent { Card = unit }, "洗入牌库", "log-line--card", "洗回行");
            A(new AttackDeclarationEvent { Attacker = unit, Target = p2 }, "〔攻击〕", "log-line--combat", "攻击宣言行");
            A(new BlockDeclarationEvent { Blocker = unit, Attacker = unit, BlockingPlayer = p1 }, "拦截", "log-line--combat", "守卫拦截行");
            A(new CombatCancelledEvent { Attacker = unit, OriginalTarget = p2, AttackingPlayer = p1 }, "攻击取消", "log-line--combat", "攻击取消行");
            A(new StatModifyEvent { Target = unit, StatType = StatType.Power, OldValue = 2, NewValue = 5, Delta = 3 },
                "攻击 2→5", "log-line--effect", "属性改写行");
            A(new FreezeEvent { Target = unit }, "被冻结", "log-line--effect", "冻结行");

            // ---- 生命/指示物/关键词/效果结先行 ----
            A(new LifeChangeEvent { Player = p1, OldLife = 20, NewLife = 15 }, "生命 20 → 15", "log-line--combat", "生命下降行");
            A(new LifeLossEvent { Player = p2, Amount = 3 }, "生命流失 3", "log-line--combat", "生命流失行");
            A(new FatigueEvent { Player = p1, Damage = 2 }, "疲劳", "log-line--combat", "疲劳行");
            A(new HealEvent { Target = p1, Amount = 3, Source = null }, "回复", "log-line--effect", "回复行");
            N(new HealEvent { Target = p1, Amount = 3, Source = unit }, "原子治疗（结先行去重）");
            A(new CounterChangedEvent { Target = unit, CounterType = CounterRules.DurabilityCounter, Amount = 2 },
                "2 层", "log-line--effect", "指示物行");
            A(new KeywordAppliedEvent { Keyword = KeywordRules.Guardian, Detail = "守护结成：锚点生物0 → 锚点生物1" },
                "守护结成", "log-line--effect", "守护改写行");
            N(new KeywordAppliedEvent { Keyword = KeywordRules.Concealed, Detail = "隐密" }, "其余关键词（防刷屏口径）");
            var sum = L(new EffectExecutionSummaryEvent { Controller = p1, Description = "Player 1 的 测试效果 → Player 2" });
            VerifySuite.Assert(sum.Text != null && sum.Text.StartsWith("　→ ") && sum.Text.Contains("我方 的") && sum.Text.Contains("【对方角色】"),
                $"结先行观侧化+缩进：{sum.Text}");
            A(new GameOverEvent { Winner = p1, Reason = GameOverReason.Concede, TotalTurns = 9 }, "我方获胜", "log-line--system", "终局行（观侧）");
            VerifySuite.Assert(MatchLogRenderer.Render(new GameOverEvent { Winner = p1, Reason = GameOverReason.Concede, TotalTurns = 9 })
                .Contains(MatchLogRenderer.Name(p1)), "终局行（中立=实名）");

            // ---- 显式空行口径 ----
            N(new StackEmptyEvent(), "StackEmpty（高频低信息）");
            N(new CardZoneChangeEvent(), "CardZoneChange（专门行去重）");
            N(new AttackResolvedEvent { Attacker = unit, Target = p2, AttackingPlayer = p1 }, "AttackResolved（伤害行承载）");

            // ============================ V10.b 无头对局：Appended 流 + 导出 ============================
            VerifySuite.Section("V10.b 无头对局：Appended 全事件流 + Markdown 导出");
            core.Reset(); // 清日志缓冲（ClearAll）
            ZoneContainer.Reseed(20261003);
            var deckA = Enumerable.Range(0, 6).Select(i => new CardData
            {
                ID = $"V10_ba_{i}", CardName = "对局填充" + i, Supertype = Cardtype.Creature, Power = 1, Life = 1,
            }).ToList();
            var deckB = Enumerable.Range(0, 6).Select(i => new CardData
            {
                ID = $"V10_bb_{i}", CardName = "对局填充B" + i, Supertype = Cardtype.Creature, Power = 1, Life = 1,
            }).ToList();
            var lines = new List<string>();
            void OnAppended(MatchLogEntry entry)
            {
                var l = MatchLogRenderer.Render(entry.Event); // 中立口径（导出同款）
                if (!string.IsNullOrEmpty(l)) lines.Add(l);
            }
            MatchLogService.Appended += OnAppended; // 订阅先于 InitGame——开局事件流（回合/阶段/起手）全收
            core.InitGame(CardLoader.BuildDeck(deckA, 2), CardLoader.BuildDeck(deckB, 2));
            var pa = core.Player1;
            try
            {
                GameActions.SkipElementPool(core, pa); // Standby → Main
                var pool = core.ElementPool.GetPool(pa);
                pool.TempElements[ManaType.Gray] = 2;
                pool.GlobalTurnIndex = 9;
                var ench = new CardData { ID = "V10_ench", CardName = "对局结界", Supertype = Cardtype.Enchantment, Durability = 3 };
                var card = new CardWrapper(ench);
                card.SetController(pa);
                core.ZoneManager.GetZoneContainer(pa).Add(card, Zone.Hand);
                VerifySuite.Assert(GameActions.PlayCard(core, pa, card), "锚点局：结界从手牌发动（耐久费 2 灰）");
                GameActions.DrainStack(core);

                VerifySuite.Assert(lines.Any(x => x.Contains("回合")), $"Appended 流含回合行（共 {lines.Count} 行）");
                VerifySuite.Assert(lines.Any(x => x.Contains("〔")), "Appended 流含阶段行");
                VerifySuite.Assert(lines.Any(x => x.Contains("抽")), "Appended 流含抽牌行");
                VerifySuite.Assert(lines.Any(x => x.Contains("使用 对局结界")), "Appended 流含打出行");
                VerifySuite.Assert(lines.Any(x => x.Contains("对局结界 入场")), "Appended 流含入场行");
                VerifySuite.Assert(lines.Any(x => x.Contains("支付") || x.Contains("消耗")),
                    "Appended 流含付费行（支付/消耗任一口径）");
            }
            finally { MatchLogService.Appended -= OnAppended; }

            var path = Path.Combine(Path.GetTempPath(), "v10_match_log.md");
            var written = MatchLogService.ExportMarkdown(path);
            VerifySuite.Assert(!string.IsNullOrEmpty(written) && File.Exists(path), "Markdown 导出落盘");
            var md = File.ReadAllText(path);
            VerifySuite.Assert(md.Contains("# 对局战报") && md.Contains("## 附录：事件类型统计"), "导出含头部与事件附录");
            VerifySuite.Assert(md.Contains("回合"), "导出含回合分节/回合行");
            VerifySuite.Assert(md.Contains("使用 对局结界"), "导出含对局过程行（纯文本还原口径）");
        }
    }
}
