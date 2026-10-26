using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using GameBoard;
using CardCore.Attribute.Handlers;
using Cysharp.Threading.Tasks;

namespace CardCore.Editor
{
    /// <summary>
    /// 端到端验证：读配置 → 组卡 → 真实对局中打出并结算。
    /// 覆盖法术（火球术 DealDamage+DrawCard）与生物（古树守卫，配置驱动血量、可被伤害效果击杀）。
    /// 棋盘层（TestBoard）：布局常量 → 六边形数学 → 确定性重建（重连语义）→
    /// 发动区流转 → 容量闸门 → _zone 维护回归。
    /// </summary>
    public static class CardPipelineVerifier
    {
        private static int _pass;
        private static int _fail;

        // ======================================== 引用化适配助手（2026-09-14：原子=表行引用+增量） ========================================

        /// <summary>枚举名/字符串 → 原子引用条目（refId=表行 HashId 反查；value/kinds/str/amp 四项增量）。</summary>
        static AtomicEffectEntry Atom(string type, int value = 1, List<int> kinds = null, string str = null, float amp = 0f)
            => new AtomicEffectEntry { refId = CardCore.Attribute.AtomicEffectTable.GetByEnumName(type)?.HashId, value = value, kinds = kinds, str = str, amp = amp };

        /// <summary>枚举重载（同参直通）。</summary>
        static AtomicEffectEntry Atom(AtomicEffectType type, int value = 1, List<int> kinds = null, string str = null, float amp = 0f)
            => Atom(type.ToString(), value, kinds, str, amp);

        /// <summary>枚举 → 表行（TotalUnitCost/极性/域等行级读取的短口）。</summary>
        static CardCore.Attribute.AtomicEffectConfig Cfg(AtomicEffectType type)
            => CardCore.Attribute.AtomicEffectTable.GetByType(type);

        // ======================================== 槽级分支载荷夹具（2026-10-05 两槽定案） ========================================

        /// <summary>主干原子 + 槽级 Branch 载荷（settle=3 事件引擎；then=条件达成的奖励原子）。</summary>
        static AtomicEffectEntry EngineAtom(AtomicEffectType trunk, int value,
            CardCore.BranchEngineKind engine, int engineParam, params AtomicEffectEntry[] then)
        {
            var e = AtomRefs.New(trunk, value);
            e.branch = new CardCore.BranchEntryData
            {
                settle = (int)CardCore.BranchSettleKind.Engine,
                engine = (int)engine,
                engineParam = engineParam,
                then = then.ToList(),
            };
            return e;
        }

        /// <summary>主干原子 + 槽级 Branch 载荷（settle=2 产出条件；如 DmgKillsTarget 读主干 per-target 产出）。</summary>
        static AtomicEffectEntry OutcomeAtom(AtomicEffectType trunk, int value,
            string outcomeId, params AtomicEffectEntry[] then)
        {
            var e = AtomRefs.New(trunk, value);
            e.branch = new CardCore.BranchEntryData
            {
                settle = (int)CardCore.BranchSettleKind.Outcome,
                outcomeId = outcomeId,
                then = then.ToList(),
            };
            return e;
        }

        /// <summary>主干原子 + 槽级 Branch 载荷（settle=1 局面状态门；效果结算时评估一次——
        /// 达标→奖励，不达标→不奖不惩，2026-10-09 还原定案）。</summary>
        static AtomicEffectEntry GateAtom(AtomicEffectType trunk, int value,
            string gateId, params AtomicEffectEntry[] then)
        {
            var e = AtomRefs.New(trunk, value);
            e.branch = new CardCore.BranchEntryData
            {
                settle = (int)CardCore.BranchSettleKind.Gate,
                gateId = gateId,
                then = then.ToList(),
            };
            return e;
        }


        [MenuItem("Tools/端到端验证")]
        public static void RunVerification()
        {
            _pass = 0;
            _fail = 0;

            // 面包屑（冻死定位用）：Console 在同步死循环下不渲染，文件追加即时落盘——
            // 卡死时最后一条面包屑 = 卡点（Logs/VerifyCrumb.txt）
            _crumbPath = Path.Combine(Application.dataPath, "..", "Logs", "VerifyCrumb.txt");
            try { System.IO.Directory.CreateDirectory(Path.GetDirectoryName(_crumbPath)); File.WriteAllText(_crumbPath, $"== verify start {System.DateTime.Now:HH:mm:ss} ==\n"); } catch { }
            CardCore.GameActions.CrumbEnabled = true; // 引擎侧面包屑同步启用（Logs/EngineCrumb.txt）
            try { System.IO.File.WriteAllText("Logs/EngineCrumb.txt", $"== engine crumb {System.DateTime.Now:HH:mm:ss} ==\n"); } catch { }
            // 2026-09-21：失败清单每次运行截断重记（Assert 只追加从不清空，跨运行累积会混淆本次失败）
            try { System.IO.File.WriteAllText(System.IO.Path.Combine("Logs", "VerifyFailures.txt"),
                $"== verify start {System.DateTime.Now:HH:mm:ss} ==\n"); } catch { }
            Crumb("entry");

            // 2026-09-14 双表换源（2026-10-18 热更迁移：正式卡池住 Assets/CardData，经 CardDataPaths）：
            // CardCatalog——effectIds 经 EffectsLibrary 解析 + 装载期 EnsureCost 推导费刷新
            // + 验证器本地合成夹具卡
            //（内联 JSON 新格式，命名卡自 TestDecks 历史恢复——测引擎不测数据，不污染正式表；
            // 卡 ID 是内容哈希随编辑漂移，一律按卡名定位=业务键）。
            var cardsData = new List<CardData>();
            try { cardsData.AddRange(SynergyUI.CardCatalog.LoadAll()); }
            catch (System.Exception e) { Debug.LogWarning($"[Verify] 正式卡池加载失败：{e.Message}"); }
            int catalogCount = cardsData.Count;
            cardsData.AddRange(FixtureCards());
            Crumb($"cards loaded={cardsData.Count} (catalog={catalogCount}, fixtures={cardsData.Count - catalogCount})");
            Debug.Log($"[Verify] 加载卡牌数据 {cardsData.Count} 张（正式池 {catalogCount} + 夹具 {cardsData.Count - catalogCount}）");

            var core = GameCore.Instance;
            // 构筑规则（2026-10-11 定案：30-60 张、卡可重复）：夹具组卡仍各取一册（便于断言口径）。
            var deckData = new List<CardData>();
            if (cardsData.Count > 0)
            {
                deckData.AddRange(cardsData);
            }
            var deck1 = deckData.Count > 0 ? CardLoader.BuildDeck(deckData, 1) : new List<Card>();
            var deck2 = deckData.Count > 0 ? CardLoader.BuildDeck(deckData, 1) : new List<Card>();
            CardCore.ZoneContainer.Reseed(20260910); // 固定种子：验证可复现（洗牌默认时间种子会逐跑漂移）
            Crumb("before InitGame");
            core.InitGame(deck1, deck2);
            Crumb("after InitGame");

            var p1 = core.Player1;
            var p2 = core.Player2;

            if (cardsData.Count > 0)
            {
                // 统一计价：全表规则一巡检（2026-09-11 简化口径 D≤C；2026-10-22 恒推导定案后 C=推导档 Ĉ）。
                Crumb("→规则一巡检 derive");
                var offenders = cardsData
                    .Where(c => !CardCostService.Derive(c).Conformant)
                    .ToList();
                // 不符明细（一次性诊断输出：定位 S/K/E/f/挂载口/D/G 哪一环口径漂了）
                foreach (var o in offenders)
                {
                    var r = CardCostService.Derive(o);
                    Debug.LogError($"[规则一明细] {o.CardName}({o.ID}) 推导档Ĉ={r.DerivedTier} S={r.S} K={r.K} "
                        + $"E={r.EAnchor} f={r.Factor:0.###} D={r.DerivedTotal} "
                        + $"Req={r.OffsetRequirement} G=[{r.Grants}]\n  "
                        + string.Join("\n  ", r.Breakdown.Select(l => $"[{l.Stage}] {l.Label} = {l.Value}")));
                }
                Assert(offenders.Count == 0,
                       $"规则一巡检：全表 {cardsData.Count} 张 D≤Ĉ（不符 {offenders.Count}：{string.Join(",", offenders.Select(o => o.ID))}）");

                // 双表装载（2026-09-14 引用化管线）：正式池非空 + effectIds 解析产物就绪
                Assert(catalogCount > 0,
                       $"双表装载：Cards.json 正式池 {catalogCount} 张（CardCatalog → EffectsLibrary 解析）");

                // 准备阶段 → 主阶段
                GameActions.SkipElementPool(core, p1);

                // 回合开始接线（2.1）：InitGame→StartGame→StartNewTurn 发布 TurnStartEvent，
                // GameCore 订阅后应已重置栈优先权持有者
                Assert(core.StackEngine.CurrentPriorityHolder != null,
                       $"回合开始接线生效（优先权持有者非空：{core.StackEngine.CurrentPriorityHolder?.Name}）");

                // 地牌经济（新资源模型）：上限曲线 / 补地牌 / 手动产出 / 费用门槛 / 结束产灰 / 台账
                Crumb("→TestLandEconomy");
                TestLandEconomy(core, p1, p2, cardsData);

                Crumb("→TestSpell");
                TestSpell(core, p1, p2, cardsData);
                Crumb("→TestCreature");
                TestCreature(core, p1, cardsData);
                Crumb("→TestSpellBranch");
                TestSpellBranch(core, p1, p2, cardsData);
                Crumb("→TestSpellModal");
                TestSpellModal(core, p1, p2, cardsData);
                Crumb("→TestTargetDomainModel");
                TestTargetDomainModel(core, p1, p2, cardsData);
                Crumb("→TestPerAtomTargeting");
                TestPerAtomTargeting(core, p1, p2);
                Crumb("→TestBlackWhiteEconomy");
                TestBlackWhiteEconomy(core, p1, p2);
                Crumb("→TestSleep");
                TestSleep(core, p1, p2);
                Crumb("→TestBoard");
                TestBoard(core, p1, p2, cardsData);
                Crumb("→TestLinkAura");
                TestLinkAura(core, p1, p2, cardsData);
            }

            // 分支条件目录 + 信息族（宣言/预言）引擎流：不依赖卡表（合成卡驱动）
            Crumb("→TestCostAnchors");
            TestCostAnchors();
            Crumb("→TestComposerModel");
            TestComposerModel();
            // 分支目录层已于 2026-10-22 彻底删除（2026-09-10 曾以 5397a3f 暂时下架，本次定案不再恢复：
            // 配置只保留原子表 + ValueSystemConfig 两张）。原 TestBranchCatalog 收敛为
            // TestInfoAtomsAndDimensions（保留信息族原子/维度有限域断言，目录断言随表删除）。
            // TestInfoAtomsAndDimensions();
            Crumb("→TestInformationFlow");
            TestInformationFlow(core, p1, p2);
            Crumb("→TestKeywords");
            TestKeywords(core, p1, p2);
            Crumb("→TestAttackGuard");
            TestAttackGuard(core, p1, p2);
            Crumb("→TestRandomness");
            TestRandomness(core, p1, p2);
            Crumb("→TestTauntAndSickness");
            TestTauntAndSickness(core, p1, p2);
            Crumb("→TestFullDomainCompose");
            TestFullDomainCompose(core, p1, p2);
            Crumb("→TestSacrificeAtom");
            TestSacrificeAtom(core, p1, p2);
            Crumb("→TestGuardianRelay");
            TestGuardianRelay(core, p1, p2);
            Crumb("→TestTriggerCap");
            TestTriggerCap(core, p1, p2);
            Crumb("→TestBranchEngines");
            TestBranchEngines(core, p1, p2);
            Crumb("→TestTierConsolidation");
            TestTierConsolidation(core, p1, p2);
            Crumb("→TestHeroSkills");
            TestHeroSkills(core, p1, p2);
            Crumb("→TestEquipment");
            TestEquipment(core, p1, p2);

            // 使用时点/响应窗口 + 死亡原子（合成卡驱动，不依赖卡表）
            Crumb("→TestCounterWindow");
            TestCounterWindow(core, p1, p2);
            Crumb("→TestDeathAtoms");
            TestDeathAtoms(core, p1, p2);

            // 三轨制（2026-09-09）：来源归因（法术=角色）
            Crumb("→TestAttribution");
            TestAttribution(core, p1, p2);

            // 时点接线（P0）+ 衍生物（P1）+ 计数与日志（P2）（合成卡驱动，不依赖卡表）
            Crumb("→TestEntrySources");
            TestEntrySources(core, p1, p2);
            Crumb("→TestTriggerPayloadFilters");
            TestTriggerPayloadFilters(core, p1, p2);
            Crumb("→TestDeadTimings");
            TestDeadTimings(core, p1, p2);
            Crumb("→TestAtomicPhaseRouting");
            TestAtomicPhaseRouting(core, p1, p2);
            Crumb("→TestSummonToken");
            TestSummonToken(core, p1, p2);
            Crumb("→TestMatchStats");
            TestMatchStats(core, p1, p2);

            // SBA=速度1栈对象（2026-09-15 定案）——必须挂最末：T3 终局后 core 进入 Ended
            //（PublishGameOverOnce 幂等），共享单例 core 的后续段落全部静默
            // 2026-09-21 隔离加固：前面 ~30 段在共享单例上累积场面/bank/终局污染（疲劳判负后
            // FinishResolution 对 IsGameOver 搁浅，SBA 窗口永不再开）——重开一局再测 SBA
            Crumb("→TestSbaWindow (re-init)");
            var sbaDeck1 = deckData.Count > 0 ? CardLoader.BuildDeck(deckData, 1) : new List<Card>();
            var sbaDeck2 = deckData.Count > 0 ? CardLoader.BuildDeck(deckData, 1) : new List<Card>();
            core.InitGame(sbaDeck1, sbaDeck2);
            TestSbaWindow(core, core.Player1, core.Player2);

            // 三类卡型定案回归（2026-10-02）：法术同折/结界耐久/域数据/攻击显式化——自开新局
            Crumb("→TestThreeCardTypes");
            TestThreeCardTypes(core, core.Player1, core.Player2);

            // 战场辅助检查段已删（2026-10-24 战场验收定案——BattlefieldVerifier 退役，
            // 战场不再由验证/重建脚本触碰）
            Crumb("all sections done");

            // P2b：对局日志按需导出（内存缓冲 → markdown 战报落盘）
            var verifyLog = MatchLogService.ExportMarkdown($"Logs/VerifyLog_{System.DateTime.Now:yyyyMMdd_HHmmss}.md");
            Debug.Log($"[Verify] 对局日志导出：{verifyLog ?? "无条目未导出"}");

            Debug.Log($"[Verify] 完成 — PASS={_pass} FAIL={_fail}");
            // 统计行落盘（2026-09-26）：控制台桥/批处理下可靠读取（Logs/VerifyStats.txt 追加）
            try { File.AppendAllText(Path.Combine("Logs", "VerifyStats.txt"),
                $"{System.DateTime.Now:HH:mm:ss} PASS={_pass} FAIL={_fail}\n"); } catch { }
            CardCore.GameActions.CrumbEnabled = false;
            if (_fail == 0)
                Debug.Log("[Verify] ✅ 整条链路贯通：读配置→组卡→对局结算");
            else
                Debug.LogError($"[Verify] ❌ 存在 {_fail} 项失败");
        }

        /// <summary>
        /// 地牌经济（新资源模型）验证：
        /// - 地牌槽上限 = min(全局回合数, 9)，双方同步推进（先手首回合 1，对手首回合即 2）
        /// - 主阶段补地牌：可用（未横置）状态入场，张数受上限约束
        /// - 手动横置自选色：颜色限于地牌自身构成；每地牌每回合周期一次
        /// - 费用门槛已退役（2026-10-16）：上限只限元素产出（入池张数），使用侧钱包无钳
        /// - 结束阶段：未横置地牌自动产 1 灰；耗尽进墓后槽位空出可补充
        /// - 己方回合开始地牌恢复；台账逐行 LandCap 与产出计数
        /// 结束时游戏停在 p1 的第 3 回合主阶段。
        /// </summary>
        /// <summary>
        /// 验证器本地合成夹具卡（2026-09-14：内联 JSON **新引用化格式**，自 TestDecks 历史恢复——
        /// 原子=表行 refId+增量。测引擎不测数据，不污染正式表；读心预言缺席见 TestSpellBranch 注记）。
        /// 2026-10-15 三类卡改版：法术效果一律主动（TriggerTiming=19 Activate_Active），
        /// 多效果=施放时 modeIndex 三选一（内嵌 choices 分支已废）；链接光环测试=双箭头双光环。
        /// 火球术=双主动效果（mode0 伤害 / mode1 抽牌——旧「双效果并发全发」语义随施放抉择改版退役）。
        /// </summary>
        private static List<CardData> FixtureCards()
        {
            // refId 动态解析（2026-10-16 教训：原子表行换号（DrawCard 120ad4d1→e5e2e116）会令夹具悬空
            // ——原子被剔、断言级联。占位符 @枚举名 装载前替换现行 HashId，免疫再换号。
            var json = @"{
  ""cards"": [
    {
      ""cardName"": ""火球术"", ""supertype"": ""Spell"",
      ""costList"": [ 0.0, 3.0, 2.0, 0.0, 0.0, 0.0 ],
      ""keywords"": [], ""effects"": [
        { ""Id"": ""FIREBALL_MAIN"", ""TriggerTiming"": 19, ""SelectionMode"": 0, ""TargetCount"": 1,
          ""AtomicEffects"": [ { ""refId"": ""@DealDamage"", ""value"": 4, ""kinds"": [2] } ] },
        { ""Id"": ""FIREBALL_DRAW"", ""TriggerTiming"": 19, ""SelectionMode"": -1,
          ""AtomicEffects"": [ { ""refId"": ""@DrawCard"", ""value"": 1 } ] } ]
    },
    {
      ""cardName"": ""古树守卫"", ""supertype"": ""Creature"", ""power"": 1, ""life"": 1,
      ""costList"": [ 1.0, 0.0, 0.0, 0.0, 0.0, 0.0 ], ""keywords"": [], ""effects"": []
    },
    {
      ""cardName"": ""灰色哨兵"", ""supertype"": ""Creature"", ""power"": 1, ""life"": 1,
      ""costList"": [ 1.0, 0.0, 0.0, 0.0, 0.0, 0.0 ], ""keywords"": [], ""effects"": []
    },
    {
      ""cardName"": ""抉择试作"", ""supertype"": ""Spell"",
      ""costList"": [ 0.0, 3.0, 0.0, 0.0, 0.0, 0.0 ],
      ""keywords"": [], ""effects"": [
        { ""Id"": ""MODAL_FIRE"", ""TriggerTiming"": 19, ""SelectionMode"": 0, ""TargetCount"": 1,
          ""AtomicEffects"": [ { ""refId"": ""@DealDamage"", ""value"": 4, ""kinds"": [2] } ] },
        { ""Id"": ""MODAL_DRAW"", ""TriggerTiming"": 19, ""SelectionMode"": -1,
          ""AtomicEffects"": [ { ""refId"": ""@DrawCard"", ""value"": 1 } ] } ]
    },
    {
      ""cardName"": ""链接光环测试"", ""supertype"": ""Creature"", ""power"": 1, ""life"": 1,
      ""costList"": [ 3.0, 0.0, 0.0, 0.0, 1.0, 0.0 ],
      ""arrows"": ""Up,LowerRight"",
      ""linkAuras"": [ { ""stat"": ""Power"", ""value"": 2 }, { ""keyword"": ""Taunt"" } ],
      ""keywords"": [], ""effects"": []
    }
  ]
}";
            foreach (var name in new[] { "DealDamage", "DrawCard" })
                json = json.Replace("@" + name,
                    CardCore.Attribute.AtomicEffectTable.GetByEnumName(name)?.HashId ?? "");
            return CardLoader.LoadCardsFromText(json);
        }

        private static void TestLandEconomy(GameCore core, Player p1, Player p2, List<CardData> cardsData)
        {
            var pool1 = core.ElementPool.GetPool(p1);

            // ---- T1（p1 主阶段）：上限 1 ----
            Assert(core.ElementPool.GetLandCap(p1) == 1, "T1 地牌槽上限 = 1");

            var hand1 = new List<Card>(core.ZoneManager.GetCards(p1, Zone.Hand));
            Assert(hand1.Count > 0, "p1 有手牌可放地牌");

            // 地牌资格（定案）：只有卡组正式生物可作地牌——魔法等非生物、衍生物/副本临时卡拒绝
            var creatures = hand1.Where(CardCore.ElementPoolSystem.CanServeAsLand).ToList();
            Assert(creatures.Count > 0, "p1 手牌有生物可放地牌");
            var nonCreature = hand1.FirstOrDefault(c => !CardCore.ElementPoolSystem.CanServeAsLand(c));
            var token = new Card { ID = "VERIFY_TOKEN" }; // 裸 Card = 效果生成的临时卡
            Assert(!core.ElementPool.AddCardToPool(token, p1), "地牌资格：衍生物/副本临时卡（裸 Card）拒绝");
            if (nonCreature != null)
                Assert(!GameActions.AddToElementPool(core, p1, nonCreature), "地牌资格：魔法等非生物超类拒绝");

            // T1 地牌选 ≥2 指示物的生物（总费用=指示物总量）：T3「回合开始恢复」断言要求
            // T1 地牌产一次后仍在池——1 指示物地产出即耗尽离池。2026-09-20 确定性修复：
            // 卡组混入正式池后起手是否含 ≥2 费生物随洗牌漂移（正式池曾被全标灰1，回退
            // 1 指示物地即耗尽→T3 池空假失败）——固定注入 4 费夹具生物保证择卡成立。
            var auraData = cardsData.FirstOrDefault(c => c.CardName == "光环测试");
            Card land1;
            if (auraData != null)
            {
                land1 = new CardWrapper(auraData);
                land1.SetController(p1);
                core.ZoneManager.GetZoneContainer(p1).Add(land1, Zone.Hand);
                hand1.Add(land1);
            }
            else land1 = creatures.FirstOrDefault(c => TotalCost(c) >= 2) ?? creatures[0];
            Assert(GameActions.AddToElementPool(core, p1, land1), "主阶段放地牌成功");

            var pooled1 = core.ElementPool.GetPooledCards(p1);
            Assert(pooled1.Count == 1 && !pooled1[0].IsTapped, "地牌以可用（未横置）状态入场");

            var secondLand = creatures.FirstOrDefault(c => !ReferenceEquals(c, land1));
            if (secondLand != null)
                Assert(!GameActions.AddToElementPool(core, p1, secondLand), "超过地牌槽上限拒绝（T1 上限 1）");

            // 锁色定案（2026-10-11）：入池锁定主色（费用份额最多色）；显式非法色拒绝
            var choices1 = CardCore.ElementPoolSystem.GetLandColorChoices(land1);
            Assert(choices1.Count > 0 && pooled1[0].LockedColor == choices1[0], "地牌入池锁定主色（份额最多色）");
            Assert(!core.ElementPool.AddCardToPool(land1, p1, ManaType.White),
                   "显式非法锁色拒绝（黑白不可锁）——直接调池校验口");

            // 现取现用：无手动横置——付费时系统按账单自动横置（每地牌每回合一次）
            // 2026-10-16 口径：地牌上限只限元素产出（入池张数），不限使用——钱包无钳、
            // 总费用 > 上限的卡照打（旧「费用门槛」断言随 CanAfford 总闸删除退役）。
            {
                var payable = hand1.Skip(1).FirstOrDefault(c => TotalCost(c) == 1);
                if (payable != null)
                {
                    Assert(PlayCardSync(core, p1, payable, null), "1 费卡打出（系统自动横置地牌支付）");
                    Assert(pooled1[0].IsTapped, "付费后地牌横置（现取现用·每回合周期一次）");
                }
                else
                {
                    Debug.LogWarning("[Verify] 跳过地牌横置用例：手牌无总费用 = 1 的卡");
                }
            }

            // ---- 结束 T1 → T2：临时钱包清空（现取现用定案）+ 全局曲线随回合数推进 ----
            pool1.TempElements[ManaType.Gray] = 5; // 残余临时元素（模拟未用完的授予）
            Assert(EndTurnPumped(core, p1), "结束 T1 回合");
            Assert(pool1.TempElements[ManaType.Gray] == 0, "回合结束：临时钱包清空（未用即逝）");
            // 2026-10-15 全局曲线：上限= min(全局回合数,10) 纯函数（GetLandCap 读 GlobalTurnIndex 单源）
            // ——回合结束折返即 T2 开始、全局数=2，双方恒等；旧「挂回合开始/回合结束」之分已无意义。
            Assert(core.ElementPool.GetLandCap(p1) == 2 && core.ElementPool.GetLandCap(p2) == 2,
                   "全局同步曲线：T2 开局双方上限 = 2（读全局回合数，双方恒等同步）");

            // ---- T2（p2）：放地不耗竭 + 结束阶段无自动产出 ----
            Crumb("land T2");
            GameActions.SkipElementPool(core, p2);
            var hand2 = new List<Card>(core.ZoneManager.GetCards(p2, Zone.Hand));

            // 合格地牌=正式三型（CanServeAsLand）：手牌首张可能是零费卡，直接放会被资格拒绝
            var oneCost = hand2.FirstOrDefault(c => TotalCost(c) >= 1 && CardCore.ElementPoolSystem.CanServeAsLand(c));
            if (oneCost == null)
            {
                // 卡表 3 份白板未必抽进手牌：注入 1 费白板保证用例确定性
                var grayData = cardsData.FirstOrDefault(c => c.CardName == "灰色哨兵");
                if (grayData != null)
                {
                    oneCost = new CardWrapper(grayData);
                    oneCost.SetController(p2);
                    core.ZoneManager.GetZoneContainer(p2).Add(oneCost, Zone.Hand);
                }
            }
            if (oneCost != null)
            {
                Assert(GameActions.AddToElementPool(core, p2, oneCost), "p2 放入地牌（锁色）");
                var pc2 = core.ElementPool.GetPooledCards(p2)[0];
                int before2 = core.ElementPool.GetPool(p2).TempElements[pc2.LockedColor];
                int pooled2Count = core.ElementPool.GetPooledCards(p2).Count;

                // 旧「耗尽→墓地→补充」循环已随指示物退役：地牌常驻不耗竭
                EndTurnPumped(core, p2);
                Assert(core.ElementPool.GetPooledCards(p2).Count == pooled2Count,
                       "结束回合：地牌不耗竭不离池（指示物/耗尽退役）");
                Assert(!pc2.IsTapped, "结束阶段无自动产出（未横置保持——产色只发生在付费步）");
                Assert(core.ElementPool.GetPool(p2).TempElements[pc2.LockedColor] == before2,
                       "结束阶段不产色入钱包");
                Assert(!core.ZoneManager.GetCards(p2, Zone.Graveyard).Contains(oneCost), "地牌未进墓地");
            }
            else
            {
                if (hand2.Count > 0)
                    GameActions.AddToElementPool(core, p2, hand2[0]);
                EndTurnPumped(core, p2);
            }

            // ---- T3（p1）：地牌恢复 + 全局同步上限 3 + 台账 ----
            Crumb("land T3");
            GameActions.SkipElementPool(core, p1);
            Assert(core.ElementPool.GetLandCap(p1) == 3, "T3（p1 第 2 个回合=全局第 3 回合）地牌槽上限 = 3（每次回合开始双方同步 +1）");
            var landAfter = core.ElementPool.GetPooledCards(p1).FirstOrDefault();
            Assert(landAfter != null && !landAfter.IsTapped,
                   $"己方回合开始地牌转为可用（解除横置）（p1 池 {core.ElementPool.GetPooledCards(p1).Count} 张：{string.Join(",", core.ElementPool.GetPooledCards(p1).Select(l => l.SourceCard.ID + (l.IsTapped ? "·横置" : "·可用")))}）");

            var records = core.ResourceLedger.GetRecords(p1);
            Assert(records.Count >= 2, "台账已有 p1 两行记录");
            Assert(records[0].LandCap == 1 && records[1].LandCap == 3, "台账 LandCap = 开行时全局同步上限（2026-10-15 全局曲线）");
        }

        /// <summary>卡总费用（费用合计；无费用卡按默认灰 1 计）</summary>
        private static int TotalCost(Card card)
        {
            if (card is IHasCost hasCost && hasCost.Cost != null)
                return (int)hasCost.Cost.Total;
            return 1;
        }

        private static List<ManaType> AllManaTypes()
        {
            return System.Enum.GetValues(typeof(ManaType)).Cast<ManaType>().ToList();
        }

        /// <summary>六色灌满（2026-10-24 Any 定案配套）：灰红蓝绿白黑灌 n、**Any 恒 0**——万能货币
        /// 不进六色时代的「满钱包」环境（否则各链尾 Any 垫付会让「缺色拒付/优先序」类断言失真）。
        /// 清零环境仍走 AllManaTypes() 全枚举（Any 必须被清）；测 Any 的断言显式设置。</summary>
        private static void FillSixColors(PlayerElementPool pool, int n)
        {
            foreach (var t in AllManaTypes()) pool.TempElements[t] = t == ManaType.Any ? 0 : n;
        }

        /// <summary>测试费用构造（2026-10-04 位置数组化）：替代旧 Dictionary 初始化器——
        /// CostOf((ManaType.Gray, 3), (ManaType.Green, 1)) == [3,0,0,1,0,0]。</summary>
        private static ElementCost CostOf(params (ManaType color, float amount)[] entries)
        {
            var c = new ElementCost();
            foreach (var (color, amount) in entries)
                c[color] = amount;
            return c;
        }

        /// <summary>混付定案（2026-09-14）后，红/蓝/绿账单可由 [本色,灰,黑,白] 垫付、灰由 [灰,黑,白] 垫付
        /// （ElementAffinity.ChainFor）。「精确同色占用即拒」类断言须清空全色 bank 只留被测色，
        /// 否则黑/白万用残留会替第二张账单垫付，导致应拒变放行。</summary>
        private static void HygieneBank(PlayerElementPool pool, ManaType keep, int amount)
        {
            foreach (var t in AllManaTypes()) pool.TempElements[t] = 0;
            pool.TempElements[keep] = amount;
        }

        /// <summary>PlayCard 拒因探针（诊断用）：按 GameActions.PlayCard 门禁顺序逐项检查，
        /// 返回首个失败门 + 关键状态。挂在断言消息里，失败时一次运行即可定位拒因。</summary>
        private static string PlayGateProbe(GameCore core, Player player, Card card, int modeIndex = 0)
        {
            if (core.TurnEngine.TurnPlayer != player)
                return $"turn={core.TurnEngine.TurnPlayer?.Name}≠{player.Name}";
            if (core.TurnEngine.CurrentPhase?.Phase != PhaseType.Main)
                return $"phase={core.TurnEngine.CurrentPhase?.Phase}";
            if (!core.ZoneManager.GetCards(player, Zone.Hand).Contains(card)) return "notInHand";
            if (!RuleHooks.CanPlay(core, player, card, Zone.Hand)) return "RuleHooks";
            if (!TargetDomainService.HasPlayableTargets(core, player, card, modeIndex)) return "noTargets";
            if (!GameActions.CanAfford(core, player, GameActions.GetCardCost(card, modeIndex))) return "CanAfford";
            if (core.StackEngine.IsResolving) return "isResolving";
            return $"PushCardCast(速={SpeedCalculator.GetCardCastSpeed(card)} 计={core.StackEngine.SpeedCounter.CurrentSpeed}"
                 + $" 持有={core.StackEngine.CurrentPriorityHolder?.Name} 栈深={core.StackEngine.StackSize})";
        }

        /// <summary>牌库保底填充（2026-09-13 起的失效模式：抽牌断言遇到牌库见底=净 0，测的是牌库而非触发；
        /// 2026-09-21 加固：蓝技能循环连续抽牌会抽干双方牌库触发疲劳判负，终局后 SBA 窗口永不再开）。
        /// 填充卡=无效果 1/1 生物，垫到 min 张。</summary>
        private static void PadDeck(GameCore core, Player p, int min, string idPrefix)
        {
            while (core.ZoneManager.GetCards(p, Zone.Deck).Count < min)
            {
                var filler = new CardWrapper(new CardData
                {
                    ID = idPrefix + core.ZoneManager.GetCards(p, Zone.Deck).Count,
                    CardName = "填充", Supertype = Cardtype.Creature, Power = 1, Life = 1,
                });
                filler.SetController(p);
                core.ZoneManager.GetZoneContainer(p).Add(filler, Zone.Deck);
            }
        }

        private static void TestSpell(GameCore core, Player p1, Player p2, List<CardData> cardsData)
        {
            var data = cardsData.FirstOrDefault(c => c.CardName == "火球术");
            Assert(data != null, "火球术配置存在");
            if (data == null) return;
            // 2026-10-15 施放抉择：法术多主动效果=modeIndex 三选一——伤害/抽牌拆两效果（同体两模式）
            Assert(data.Effects != null && data.Effects.Count == 2
                   && data.Effects[0].AtomicEffects != null && data.Effects[0].AtomicEffects.Count == 1
                   && data.Effects[0].AtomicEffects[0].refId == CardCore.Attribute.AtomicEffectTable.GetByEnumName(nameof(AtomicEffectType.DealDamage))?.HashId
                   && data.Effects[1].AtomicEffects != null && data.Effects[1].AtomicEffects.Count == 1
                   && data.Effects[1].AtomicEffects[0].refId == CardCore.Attribute.AtomicEffectTable.GetByEnumName(nameof(AtomicEffectType.DrawCard))?.HashId,
                   "火球术效果反序列化（伤害/抽牌两主动效果=施放抉择两模式——2026-10-15）");

            // 模式0（伤害）：账单=卡面费+所选效果锚价（GetCardCost 单源）——灌足钱包消除计价漂移
            var firePool = core.ElementPool.GetPool(p1);
            firePool.GlobalTurnIndex = 9;
            FillSixColors(firePool, 99);

            var fireball = new CardWrapper(data);
            fireball.SetController(p1);
            core.ZoneManager.GetZoneContainer(p1).Add(fireball, Zone.Hand);
            int lifeBefore = p2.Life;
            int deckBefore = core.ZoneManager.GetCards(p1, Zone.Deck).Count;

            Assert(PlayCardSync(core, p1, fireball, new List<Entity> { p2 }, modeIndex: 0),
                   "火球术模式0打出（施放抉择=伤害）");
            Assert(p2.Life == lifeBefore - 4, $"模式0：对手 Life −4（{lifeBefore}→{p2.Life}）");
            Assert(core.ZoneManager.GetCards(p1, Zone.Deck).Count == deckBefore, "模式0：不抽牌（只执行所选模式）");
            Assert(core.ZoneManager.GetCards(p1, Zone.Graveyard).Contains(fireball),
                   "法术结算后入墓地");

            // 模式1（抽牌）：同卡另一模式——执行隔离与费用按所选模式
            var fireball2 = new CardWrapper(data);
            fireball2.SetController(p1);
            core.ZoneManager.GetZoneContainer(p1).Add(fireball2, Zone.Hand);
            lifeBefore = p2.Life;
            deckBefore = core.ZoneManager.GetCards(p1, Zone.Deck).Count;
            Assert(PlayCardSync(core, p1, fireball2, null, modeIndex: 1), "火球术模式1打出（施放抉择=抽牌）");
            Assert(p2.Life == lifeBefore, "模式1：不造伤害（执行隔离）");
            Assert(core.ZoneManager.GetCards(p1, Zone.Deck).Count == deckBefore - 1,
                   "模式1：抽牌生效（牌库 −1）");
            Assert(core.ZoneManager.GetCards(p1, Zone.Graveyard).Contains(fireball2),
                   "模式1：结算后入墓地");
        }

        /// <summary>
        /// 分支卡经 PlayCard 的端到端（法术执行链回归锚；2026-10-05 两槽定案改原子 Branch 载荷形态）：
        /// 伤害主干 + DmgKillsTarget 产出条件载荷 → 法术打出 → 扁平路径 per-target 评估 →
        /// 击杀命中 Then 奖励抽牌（ExecuteThenRewardsAsync——奖励目标隐藏区自结算/无头自动选首）→ 入墓；
        /// 元素费只扣一次（执行器 skipElementCost 防双计）。
        /// （旧「读心预言」Steps+宣言门夹具随 DeclareHand 无表行缺位退役——产出条件路径改由本段覆盖。）
        /// </summary>
        private static void TestSpellBranch(GameCore core, Player p1, Player p2, List<CardData> cardsData)
        {
            // 合成分支法术：3 伤主干（锁对方单位域）+ 击杀产出条件载荷（Then=抽1）
            var data = new CardData
            {
                ID = "VERIFY_BRANCH_SPELL", CardName = "击杀奖励试作", Supertype = Cardtype.Spell,
                Cost = CostOf((ManaType.Red, 1f)),
            };
            data.Effects.Add(new CardEffectData
            {
                Id = "VERIFY_BRANCH_MAIN",
                TriggerTiming = (int)TriggerTiming.Activate_Active, // 法术效果须主动（2026-10-15）
                SelectionMode = (int)CardCore.SelectionMode.Single,
                TargetCount = 1,
                AtomicEffects = new List<AtomicEffectEntry>
                {
                    OutcomeAtom(AtomicEffectType.DealDamage, 3, "DmgKillsTarget",
                        AtomRefs.New(AtomicEffectType.DrawCard, value: 1)),
                },
            });
            // 载荷形态断言（converter）：settle=2 产出条件折入主干原子 Branch，Then 奖励转存
            var payloadDef = CardEffectConverter.ConvertOne(data.Effects[0], data.ID);
            Assert(payloadDef != null && payloadDef.Effects.Count == 1
                   && payloadDef.Effects[0].Branch != null
                   && payloadDef.Effects[0].Branch.Settle == CardCore.BranchSettleKind.Outcome
                   && payloadDef.Effects[0].Branch.OutcomeId == "DmgKillsTarget"
                   && payloadDef.Effects[0].Branch.Then.Count == 1,
                   "分支载荷转换：产出条件（DmgKillsTarget）+ Then 奖励折入主干原子 Branch 槽");

            // 确定性：p2 场上置 2 血生物（3 伤必杀 → 产出条件必命中）
            var victim = SpawnTier(core, p2, 2, 2);

            var pool1 = core.ElementPool.GetPool(p1);
            pool1.GlobalTurnIndex = 9; // 模拟后期回合（使用侧无钳，历史注释留档）
            pool1.TempElements[ManaType.Red] += 9;
            int deckBefore = core.ZoneManager.GetCards(p1, Zone.Deck).Count;
            int redBefore = pool1.TempElements[ManaType.Red];
            var branchSpell = InjectCard(core, p1, data);

            Assert(PlayCardSync(core, p1, branchSpell, new List<Entity> { victim }), "分支载荷法术成功打出");

            Assert(!victim.IsAlive, "主干伤害击杀目标（3 伤 vs 2 血）");
            Assert(core.ZoneManager.GetCards(p1, Zone.Deck).Count == deckBefore - 1,
                   "产出条件命中 → Then 奖励抽 1（扁平路径 per-target 评估，经 PlayCard 真实触发）");
            Assert(core.ZoneManager.GetCards(p1, Zone.Graveyard).Contains(branchSpell), "分支法术结算后入墓地");
            Assert(core.ZoneManager.GetCards(p1, Zone.Activation).Count == 0, "结算完成后发动区清空");
            // skipElementCost=true 保证执行器不再对锚价二次扣款——实扣=GetCardCost 单源账单
            //（2026-10-15：账单=卡面费+所选效果锚价，红线锚价随表/时机系数浮动——测试不钉数只钉「只扣一次」）
            var branchBill = CardCore.ElementPaymentValidator.NormalizeBill(GameActions.GetCardCost(branchSpell, 0));
            int redBill = branchBill.GetValueOrDefault(ManaType.Red);
            Assert(pool1.TempElements[ManaType.Red] == redBefore - redBill,
                   $"元素费只扣一次（skipElementCost 防执行器双计费；单源账单红 {redBill}，实际扣 {redBefore - pool1.TempElements[ManaType.Red]}）");

            // 对照组：不击杀（3 伤 vs 5 血）→ 产出条件不命中 → 无奖励
            var survivor = SpawnTier(core, p2, 3, 5);
            deckBefore = core.ZoneManager.GetCards(p1, Zone.Deck).Count;
            var branchSpell2 = InjectCard(core, p1, data);
            Assert(PlayCardSync(core, p1, branchSpell2, new List<Entity> { survivor }), "分支载荷法术第二次打出（不击杀对照）");
            Assert(survivor.IsAlive && survivor.GetLife() == 2,
                   "对照组：3 伤不致死（产出条件不命中）");
            Assert(core.ZoneManager.GetCards(p1, Zone.Deck).Count == deckBefore,
                   "未击杀 → Then 不结算（条件奖励只在产出达成时强制结算）");
        }

        // ======================================== 目标域模型（2026-09-10 M1 重构定案） ========================================
        // TargetKind 序号集合 + 组合交集 + SelectionMode + 候选空不可发动。
        private static void TestTargetDomainModel(GameCore core, Player p1, Player p2, List<CardData> cardsData)
        {
            // a) 全部原子解析出有效域或显式无目标；b) 全部效果交集非空（有带域原子时）
            int atomTotal = 0, kindAtoms = 0, effectsChecked = 0, emptyDomain = 0;
            foreach (var card in cardsData)
            {
                if (card?.Effects == null) continue;
                foreach (var eff in card.Effects)
                {
                    if (eff == null) continue;
                    var def = CardEffectConverter.ConvertOne(eff, card.ID);
                    if (def == null) continue;
                    effectsChecked++;

                    var atoms = def.Steps != null && def.Steps.Count > 0
                        ? CardEffectConverter.EnumerateMainSequenceAtoms(def.Steps, 0).ToList()
                        : def.Effects?.ToList() ?? new List<AtomicEffectInstance>();
                    foreach (var atom in atoms)
                    {
                        if (atom == null) continue;
                        atomTotal++;
                        if (atom.TargetKinds != null && atom.TargetKinds.Count > 0) kindAtoms++;
                        else if (atom.TargetKinds == null)
                        {
                            Assert(false, $"原子 {atom.Type} 域为 null（converter 未解析表默认？）");
                        }
                    }

                    bool anyKind = atoms.Any(a => a?.TargetKinds != null && a.TargetKinds.Count > 0);
                    if (anyKind && (def.TargetDomain == null || def.TargetDomain.Count == 0))
                    {
                        emptyDomain++;
                        Assert(false, $"卡 {card.ID}({card.CardName}) 效果 {def.Id}：带域原子存在但组合域为空"
                                     + "（逐原子口径域=并集，带域原子存在则并集必非空——converter 断链）");
                    }
                }
            }
            Assert(emptyDomain == 0, $"全部效果组合域非空（空域 {emptyDomain} 个——逐原子口径=并集）");
            // 2026-10-09 清理：">10 原子"规模阈值断言删除——钉在旧 5 卡夹具 ~13 原子规模（现行夹具仅产 3 原子，
            // 与卡池无关恒不可达）；逐原子域完整性由上方 null/空域断言承担
            Debug.Log($"[Verify] 目标域：{effectsChecked} 效果 / {atomTotal} 原子（带域 {kindAtoms}）");

            // c) 表默认抽查：DealDamage 域 = {0,1} 且 filter 为空（target_kinds_review 定案：
            //    直伤可指角色——打脸合法；角色排除口径已废，NoRole 是个别原子的域内细化）
            var dd = CardCore.Attribute.AtomicEffectTable.GetByType(AtomicEffectType.DealDamage);
            var ddKinds = dd?.GetTargetKindList();
            Assert(ddKinds != null && ddKinds.Contains(1) && ddKinds.Contains(2),
                $"DealDamage 表默认域 = 己方+对方有生命单位（实际 [{string.Join(",", ddKinds ?? new List<int>())}]）");
            Assert(dd != null && string.IsNullOrEmpty(dd.TargetFilter),
                "DealDamage 表默认 filter 为空（可指角色，直伤打脸合法——review 定案）");

            // d) 双路径一致（两槽定案新语义）：无抉择 Steps → 平铺折叠进 def.Effects（域与扁平一致）；
            //    含抉择（kind=2）→ 保留 Steps 结构（执行引擎步骤遍历）
            var entry = AtomRefs.New(AtomicEffectType.DealDamage, value: 2);
            var flat = new CardEffectData { Id = "VERIFY_TDM_FLAT", AtomicEffects = new List<AtomicEffectEntry> { entry } };
            var stepped = new CardEffectData
            {
                Id = "VERIFY_TDM_STEP",
                Steps = new List<EffectStepData> { new EffectStepData { kind = 0, atomic = entry } },
            };
            var flatDef = CardEffectConverter.ConvertOne(flat, "VERIFY_TDM");
            var stepDef = CardEffectConverter.ConvertOne(stepped, "VERIFY_TDM");
            Assert(stepDef.Steps.Count == 0 && stepDef.Effects.Count == 1
                   && stepDef.Effects[0].Type == AtomicEffectType.DealDamage,
                   "无抉择 Steps：平铺折叠进 def.Effects（原子步落主干序列，Steps 清空）");
            Assert(TargetKindRules.Format(flatDef.TargetDomain) == TargetKindRules.Format(stepDef.TargetDomain)
                && flatDef.TargetDomain.Count > 0,
                $"扁平与 Steps 路径组合域一致（{TargetKindRules.Format(flatDef.TargetDomain)}）");
            var choiced = CardEffectConverter.ConvertOne(new CardEffectData
            {
                Id = "VERIFY_TDM_CHOICE",
                Steps = new List<EffectStepData>
                {
                    new EffectStepData
                    {
                        kind = 2,
                        choices = new List<EffectChoiceData>
                        {
                            new EffectChoiceData { steps = new List<EffectStepData> { new EffectStepData { kind = 0, atomic = entry } } },
                            new EffectChoiceData { steps = new List<EffectStepData> { new EffectStepData { kind = 0, atomic = AtomRefs.New(AtomicEffectType.DrawCard, value: 1) } } },
                        },
                    },
                },
            }, "VERIFY_TDM");
            Assert(choiced.Steps.Count == 1 && choiced.Steps[0].Kind == CardCore.RuntimeStepKind.Choice
                   && choiced.Effects.Count == 0,
                   "含抉择 Steps：保留步骤结构不折叠（def.Effects 空——抉择 per-mode 执行/计价）");

            // e) 候选空不可发动：清场后 NoRole 原子（激励：{0}+NoRole,Tapped）不可打——
            //    角色被 NoRole 滤除、生物清空 → 候选空。（DealDamage 定案 filter 空=可打脸，
            //    空场仍可发动，不再作此反例探针）
            var saveP1Field = new List<Card>(core.ZoneManager.GetCards(p1, Zone.Battlefield));
            var saveP2Field = new List<Card>(core.ZoneManager.GetCards(p2, Zone.Battlefield));
            foreach (var c in saveP1Field) core.ZoneManager.MoveCard(c, p1, Zone.Battlefield, Zone.Graveyard);
            foreach (var c in saveP2Field) core.ZoneManager.MoveCard(c, p2, Zone.Battlefield, Zone.Graveyard);
            try
            {
                var probeData = new CardData
                {
                    ID = "VERIFY_TDM_PLAY",
                    Supertype = Cardtype.Spell,
                    Effects = new List<CardEffectData>
                    {
                        new CardEffectData
                        {
                            Id = "VERIFY_TDM_PROBE",
                            SelectionMode = (int)CardCore.SelectionMode.Single, // 显式选一：None=区域自结算类会早真（2026-09-13 对齐）
                            AtomicEffects = new List<AtomicEffectEntry>
                            {
                                AtomRefs.New(AtomicEffectType.Untap, value: 1),
                            },
                        },
                    },
                };
                var probeCard = CardLoader.BuildDeck(new List<CardData> { probeData }, 1)[0];
                bool playable = TargetDomainService.HasPlayableTargets(core, p1, probeCard, 0);
                Assert(!playable,
                    $"空战场 + NoRole 过滤 → HasPlayableTargets=false（候选空不可发动；实际 {playable}）");
            }
            finally
            {
                foreach (var c in saveP1Field) core.ZoneManager.MoveCard(c, p1, Zone.Graveyard, Zone.Battlefield);
                foreach (var c in saveP2Field) core.ZoneManager.MoveCard(c, p2, Zone.Graveyard, Zone.Battlefield);
            }

            // f) 内容契约（2026-09-11 定案）：效果栏只能挂对自己有益/中性的原子——错边（对自己有害/对对手有益）
            //    被转换器剔除只能进代价栏；正侧全价无获得；双侧域全价照旧（"同时作用双方"不支持，先不管）
            var healCfg = CardCore.Attribute.AtomicEffectTable.GetByType(AtomicEffectType.Heal);
            Assert(healCfg != null && healCfg.Polarity > 0f, "Heal 表极性为正（对己方释放有益）");
            CardEffectData PolEff(int sideKind) => new CardEffectData
            {
                Id = "VERIFY_POL",
                AtomicEffects = new List<AtomicEffectEntry>
                {
                    AtomRefs.New(AtomicEffectType.Heal, value: 2, kinds: new List<int> { sideKind })
                },
            };
            var wrongDef = CardEffectConverter.ConvertOne(PolEff(2), "VERIFY_POL"); // 锁对方 {2}=EnemyLivingUnit = 错边
            var rightDef = CardEffectConverter.ConvertOne(PolEff(1), "VERIFY_POL"); // 锁己方 {1}=OwnLivingUnit
            Assert(rightDef.Effects.Count == 1, "正侧原子正常转换");
            // 小数直通（2026-10-12）：Heal 锚 0.5 绿/点——value2=绿1.0，(int) 截断会假零，用 float 断言
            float rightCost = CostDerivationService.DeriveElementCosts(rightDef).Total;
            Assert(rightCost > 0f, $"Heal(p=+1) 锁己方域 → 全价（实际 {rightCost}）");
            Assert(wrongDef.Effects.Count == 0 && CostDerivationService.DeriveElementCosts(wrongDef).IsZero,
                   "内容契约：错边原子在效果栏被剔除（只能进代价栏——Payload 路径见 TestBlackWhiteEconomy）");
            Assert(CostDerivationService.DeriveElementGrants(rightDef).IsZero, "正侧原子无黑白获得");
            var bothDef = CardEffectConverter.ConvertOne(new CardEffectData
            {
                Id = "VERIFY_POL_B",
                AtomicEffects = new List<AtomicEffectEntry>
                {
                    AtomRefs.New(AtomicEffectType.Heal, value: 2, kinds: new List<int> { 1 }),
                },
            }, "VERIFY_POL_B");
            Assert(System.Math.Abs(CostDerivationService.DeriveElementCosts(bothDef).Total - rightCost) < 1e-4f,
                "双侧域构筑期全价（实际打错边照发——运行时口径，结算发放见 TestBlackWhiteEconomy）");

            // g) TargetKind 重排（2026-09-11 Self 找回）：Self=0 / 单位 1-4 / 卡域 5-16；关键词域={Self}
            Assert((int)CardCore.TargetKind.Self == 0
                   && (int)CardCore.TargetKind.OwnLivingUnit == 1
                   && (int)CardCore.TargetKind.EnemyActivation == 16,
                   "TargetKind 序号：Self=0、单位 1-4、卡域 5-16（整体后移）");
            var tauntCfg = CardCore.Attribute.AtomicEffectTable.GetByType(AtomicEffectType.GrantTaunt);
            var tauntKinds = tauntCfg?.GetTargetKindList();
            Assert(tauntKinds != null && tauntKinds.Count == 1 && tauntKinds[0] == (int)CardCore.TargetKind.Self,
                   "关键词（嘲讽）表域 = {Self}（关键词只能作用于自己）");

            // h) 生物黑白关键词照旧印制计价（2026-09-14 撤销「转主动」定案）：参杂黑白的生物
            //    可作地牌——地牌不从黑白份额产指示物（入池口径见 TestBlackWhiteEconomy 第 7 段），
            //    黑白获取通道不变（=卡结算：错边/Payload 补偿），费用侧照常计价。
            var bwJson = "{\"cards\":[{\"id\":\"VERIFY_BW_KW\",\"cardName\":\"黑白关键词验证\","
                       + "\"supertype\":\"Creature\",\"power\":2,\"life\":2,"
                       + "\"keywords\":[\"DivineShield\",\"Lifesteal\",\"Taunt\",\"Armor\"]}]}";
            var bwCards = CardLoader.LoadCardsFromText(bwJson);
            Assert(bwCards.Count == 1
                   && bwCards[0].Keywords.Count == 4
                   && (bwCards[0].Effects ?? new List<CardEffectData>())
                       .TrueForAll(e => e.TriggerTiming != (int)CardCore.TriggerTiming.Activate_Active),
                   "黑白关键词保留印制（不转主动；无合成 ACT_ 自赋予效果）");
            CardCostService.EnsureCost(bwCards[0]);
            Assert(bwCards[0].Cost[ManaType.White] > 0f || bwCards[0].Cost[ManaType.Black] > 0f,
                   "生物建议费用含黑白份额（黑白进费用列表；地牌侧由入池过滤兜住，不从黑白产元素）");
        }

        // ======================================== 逐原子目标制（2026-10-09 定案） ========================================

        /// <summary>
        /// 逐原子目标制端到端（2026-10-09 定案：并列组合无效果级作用范围声明 → 各原子按自身
        /// 极性过滤域独立解析，弹窗全部落结算期连续等待，数量档/随机档各原子各自生效；
        /// 单条效果作用域并集上限 4）：
        /// ①判别与域：无 header=PerAtomTargets+并集域；header 声明=共享口径不回归；
        /// ②极性过滤助手锚（TargetKindRules.PolarityFilter）；
        /// ③行为端到端：域不交的 DealDamage（对方侧）+永久属性增加（己方侧）同卡并列——
        ///   旧交集口径下不可构筑的组合，现各自独立解析结算（无头连弹自动代选不挂起）；
        /// ④无域原子（DrawCard）与有域原子并列自结算共存；
        /// ⑤强制类无窗口=逐原子自动全取；
        /// ⑥并集 >4 装载兜底拦截（合成器加入门的 backstop）。
        /// </summary>
        private static void TestPerAtomTargeting(GameCore core, Player p1, Player p2)
        {
            EnsureMainPhase(core, p1);

            // ---- 1. 判别与域 ----
            var dmgEntry = AtomRefs.New(CardCore.AtomicEffectType.DealDamage, value: 2);          // p=-1 域{1,2}→{2}
            var plusEntry = AtomRefs.New(CardCore.AtomicEffectType.AddPermanentPlusOne, value: 1); // p=+1 域{1,2}→{1}
            var perAtomDef = CardEffectConverter.ConvertOne(new CardEffectData
            {
                Id = "VERIFY_PA",
                AtomicEffects = new List<AtomicEffectEntry> { dmgEntry, plusEntry },
            }, "VERIFY_PA");
            Assert(perAtomDef.PerAtomTargets
                   && TargetKindRules.Format(perAtomDef.TargetDomain) == "1,2,3,4",
                $"无 header=逐原子模式+并集域（实际 flag={perAtomDef.PerAtomTargets} "
                + $"域=[{TargetKindRules.Format(perAtomDef.TargetDomain)}]）");
            var sharedDef = CardEffectConverter.ConvertOne(new CardEffectData
            {
                Id = "VERIFY_PA_SHARED",
                TargetKinds = new List<int> { 2 }, // 旧数据：效果级作用范围声明（共享单选）
                AtomicEffects = new List<AtomicEffectEntry> { dmgEntry },
            }, "VERIFY_PA_SHARED");
            Assert(!sharedDef.PerAtomTargets && TargetKindRules.Format(sharedDef.TargetDomain) == "2",
                "header 声明=共享口径不回归（PerAtomTargets=false、域=声明值）");

            // ---- 2. Union 助手（逐原子组合域=并集来源；运行时不做极性过滤——双域原子可选两侧、
            //     跨侧命中走错边黑白发放经济，与旧共享口径一致；极性收窄是合成器 UI 的选择集口径） ----
            Assert(TargetKindRules.Format(TargetKindRules.Union(new List<int> { 2, 1 }, new List<int> { 3, 2 })) == "1,2,3"
                   && TargetKindRules.Format(TargetKindRules.Union(null, new List<int> { 5 })) == "5",
                "Union：并集升序去重、null 成员不参与");

            // ---- 3/4. 行为端到端：域不交双原子并列 + 无域原子共存（无头连弹自动代选） ----
            int seq = 0;
            Card Spawn(Player owner, int power, int life)
            {
                var data = new CardData
                {
                    ID = "VERIFY_PA_" + (++seq), CardName = "PA" + seq,
                    Supertype = Cardtype.Creature, Power = power, Life = life,
                };
                var card = new CardWrapper(data);
                card.SetController(owner);
                Assert(core.ZoneManager.TryAddToBattlefield(card, owner), $"逐原子段合成随从入场（{data.CardName}）");
                card.Untap();
                return card;
            }

            var saveP1Field = new List<Card>(core.ZoneManager.GetCards(p1, Zone.Battlefield));
            var saveP2Field = new List<Card>(core.ZoneManager.GetCards(p2, Zone.Battlefield));
            foreach (var c in saveP1Field) core.ZoneManager.MoveCard(c, p1, Zone.Battlefield, Zone.Graveyard);
            foreach (var c in saveP2Field) core.ZoneManager.MoveCard(c, p2, Zone.Battlefield, Zone.Graveyard);
            var pool1 = core.ElementPool.GetPool(p1);
            var snap = new Dictionary<ManaType, int>(pool1.TempElements);
            try
            {
                FillSixColors(pool1, 99);
                var a = Spawn(p1, 2, 5);
                var b = Spawn(p2, 2, 5);
                int p1Deck = core.ZoneManager.GetCards(p1, Zone.Deck).Count;
                Assert(p1Deck > 0, "逐原子段前提：牌库非空（DrawCard 自结算观察点）");

                var paCardData = new CardData
                {
                    ID = "VERIFY_PA_CR", CardName = "逐原子验证", Supertype = Cardtype.Spell,
                    Cost = CostOf((ManaType.Gray, 1f)),
                };
                paCardData.Effects.Add(new CardEffectData
                {
                    Id = "VERIFY_PA_ONPLAY",
                    TriggerTiming = (int)TriggerTiming.OnPlay,
                    AtomicEffects = new List<AtomicEffectEntry>
                    {
                        // 显式域收窄（逐原子卡的标准形态：每原子声明自己的作用范围）——对方单位 {2}/己方单位 {1}
                        AtomRefs.New(CardCore.AtomicEffectType.DealDamage, value: 2, kinds: new List<int> { 2 }),
                        AtomRefs.New(CardCore.AtomicEffectType.AddPermanentPlusOne, value: 1, kinds: new List<int> { 1 }),
                    },
                });
                var paCard = InjectCard(core, p1, paCardData);
                Assert(GameActions.PlayCard(core, p1, paCard), "打出域不交逐原子组合（旧交集口径不可构筑）");
                GameActions.DrainStack(core);
                Assert(b.GetLife() == 3 && a.GetPower() == 3,
                    $"逐原子独立解析：DealDamage 落对方生物（b 生 {b.GetLife()}=3）、"
                    + $"永久+1 落己方生物（a 攻 {a.GetPower()}=3）——两原子域不交各自结算");

                var paDrawData = new CardData
                {
                    ID = "VERIFY_PA_DRAW", CardName = "逐原子抽卡", Supertype = Cardtype.Spell,
                    Cost = CostOf((ManaType.Gray, 1f)),
                };
                paDrawData.Effects.Add(new CardEffectData
                {
                    Id = "VERIFY_PA_DRAW_ONPLAY",
                    TriggerTiming = (int)TriggerTiming.OnPlay,
                    AtomicEffects = new List<AtomicEffectEntry>
                    {
                        AtomRefs.New(CardCore.AtomicEffectType.DrawCard, value: 1),             // 隐藏区（牌库）自结算
                        AtomRefs.New(CardCore.AtomicEffectType.AddPermanentPlusOne, value: 1, kinds: new List<int> { 1 }),
                    },
                });
                var paDrawCard = InjectCard(core, p1, paDrawData);
                Assert(GameActions.PlayCard(core, p1, paDrawCard), "打出无域+有域并列组合");
                GameActions.DrainStack(core);
                Assert(a.GetPower() == 4
                       && core.ZoneManager.GetCards(p1, Zone.Deck).Count == p1Deck - 1,
                    "无域原子自结算共存：DrawCard 抽 1（牌库 -1）、永久+1 照常落己方生物（a 攻 4）");

                // ---- 5. 强制类无窗口：逐原子自动全取（不弹选，全域命中）——
                //      直构 def（CardData.ActivationType=0 会被转换器当未声明回落默认桶，
                //      V9.o ④ 强制桶同款先例）；PerAtomTargets 手动置位（绕过 converter） ----
                var mandDef = new CardCore.EffectDefinition
                {
                    Id = "VERIFY_PA_MAND",
                    DisplayName = "强制逐原子",
                    ActivationType = CardCore.EffectActivationType.Mandatory,
                    TriggerTiming = TriggerTiming.OnPlay,
                    ElementCostPrepaid = true,
                    PerAtomTargets = true,
                    Effects = new List<CardCore.AtomicEffectInstance>
                    {
                        new CardCore.AtomicEffectInstance
                        {
                            Type = CardCore.AtomicEffectType.DealDamage, Value = 1,
                            TargetKinds = new List<int> { 2 }, Polarity = -1f,
                            RowHashId = CardCore.Attribute.AtomicEffectTable.GetByType(CardCore.AtomicEffectType.DealDamage)?.HashId,
                        },
                    },
                };
                int ubLife0 = b.GetLife(), p2Life0 = p2.Life;
                core.StackEngine.GetExecutor().ExecuteAsync(new EffectInstance
                {
                    Definition = mandDef,
                    Source = p1,
                    Controller = p1,
                    Targets = new List<Entity>(),
                }).GetAwaiter().GetResult();
                Assert(b.GetLife() == ubLife0 - 1 && p2.Life == p2Life0 - 1,
                    $"强制类自动全取：对方生物与角色各受 1（{ubLife0}→{b.GetLife()}、角色 {p2Life0}→{p2.Life}）——不弹选全域命中");
            }
            finally
            {
                foreach (var kv in snap) pool1.TempElements[kv.Key] = kv.Value;
                var kill = new List<Card>(core.ZoneManager.GetCards(p1, Zone.Battlefield).Concat(
                    core.ZoneManager.GetCards(p2, Zone.Battlefield)));
                foreach (var c in kill) core.ZoneManager.MoveCard(c, c.GetController(), Zone.Battlefield, Zone.Graveyard);
                foreach (var c in saveP1Field) core.ZoneManager.MoveCard(c, p1, Zone.Graveyard, Zone.Battlefield);
                foreach (var c in saveP2Field) core.ZoneManager.MoveCard(c, p2, Zone.Graveyard, Zone.Battlefield);
            }
            // ---- 6. 并集 >4 装载兜底（合成器加入门的 backstop） ----
            var ddHash = CardCore.Attribute.AtomicEffectTable.GetByType(CardCore.AtomicEffectType.DealDamage)?.HashId;
            var prevSink = TideLog.Sink;
            var errs = new List<string>();
            TideLog.Sink = (level, msg) =>
            {
                if (level == TideLogLevel.Error) errs.Add(msg);
                prevSink?.Invoke(level, msg);
            };
            try
            {
                var overJson = "{\"cards\":[{\"id\":\"VERIFY_PA_OVER\",\"cardName\":\"并集超限\","
                             + "\"supertype\":\"Spell\",\"effects\":[{\"id\":\"E1\",\"atomicEffects\":[{\"refId\":\""
                             + ddHash + "\",\"value\":1,\"kinds\":[2,4,6,8,10]"
                             + "}]}]}]}";
                CardLoader.LoadCardsFromText(overJson); // p=-1 全对方侧 5 域 → 并集 5 > 4
                Assert(errs.Any(e => e.Contains("并集") && e.Contains("超上限")),
                    "并集 >4 装载兜底：TideLog Error 点名超上限（逐原子单条效果限 4 个作用范围）");

                errs.Clear();
                var okJson = "{\"cards\":[{\"id\":\"VERIFY_PA_OK\",\"cardName\":\"并集合规\","
                           + "\"supertype\":\"Spell\",\"effects\":[{\"id\":\"E1\",\"atomicEffects\":[{\"refId\":\""
                           + ddHash + "\",\"value\":1,\"kinds\":[2,4]"
                           + "}]}]}]}";
                CardLoader.LoadCardsFromText(okJson);
                Assert(!errs.Any(e => e.Contains("超上限")),
                    "并集 ≤4 合规装载：无超上限报错（并集恰 4 为合法上限内）");
            }
            finally
            {
                TideLog.Sink = prevSink;
            }
        }

        // ======================================== 沉睡（2026-09-11 改造） ========================================

        /// <summary>
        /// 沉睡端到端：①表行锚（2026-09-13 用户改表：绿2 Pol=-1 域={1,2} NoRole——通用赋予域）
        /// ②自我沉睡灰费豁免（打出不扣灰、入场沉睡指示物=豁免量）
        /// ③沉睡期间无法重置（回合开始扣层代替重置、扣完即醒）+ 效果无效（OnTurnEnd 触发被拦）
        /// ④通用赋予（Value 层数直接给）。效果/指示物分离：持续规则全在指示物上（本原子只赋予）。
        /// </summary>
        private static void TestSleep(GameCore core, Player p1, Player p2)
        {
            EnsureMainPhase(core, p1);

            // ---- 0. 表行锚（表为单一来源：颜色/价格钉断言已删——沉睡表价绿→黑随表改面漂移，不钉） ----
            var sleepCfg = CardCore.Attribute.AtomicEffectTable.GetAll()
                .FirstOrDefault(c => c.EnumName == "Sleep" && c.DisplayName == "沉睡");
            var sleepKinds = sleepCfg?.GetTargetKindList();
            Assert(sleepKinds != null && sleepKinds.Count == 2
                   && sleepKinds.Contains(0) == false && sleepKinds.Contains(1) && sleepKinds.Contains(2),
                   "沉睡域 = {1,2}（表授予域=双方单位——「存储态=自己」口径已随表改面退役，自我沉睡经实例收窄 kinds={0} 表达）");
            Assert(sleepCfg?.TargetFilter == "NoRole", "沉睡域滤 NoRole（角色不可沉睡）");

            var pool1 = core.ElementPool.GetPool(p1);
            var snapBank = new Dictionary<ManaType, int>(pool1.TempElements);

            try
            {
                FillSixColors(pool1, 99);

                // ---- 1. 自我沉睡：灰费豁免 + 入场指示物 ----
                var sleepData = new CardData
                {
                    ID = "VERIFY_SLEEP_CR", CardName = "验证沉睡生物", Supertype = Cardtype.Creature, Power = 4, Life = 4,
                    Cost = CostOf((ManaType.Gray, 3), (ManaType.Green, 1)),
                };
                sleepData.Effects.Add(new CardEffectData
                {
                    Id = "VERIFY_SLEEP_ONPLAY",
                    TriggerTiming = (int)TriggerTiming.OnPlay,
                    AtomicEffects = new List<AtomicEffectEntry>
                    {
                        AtomRefs.New(AtomicEffectType.Sleep, value: 0, kinds: new List<int> { 0 }), // 收窄域={Self}，Value≤0=豁免时长模式（灰+绿豁免判定口径）
                    },
                });
                sleepData.Effects.Add(new CardEffectData
                {
                    Id = "VERIFY_SLEEP_TURNEND",
                    TriggerTiming = (int)TriggerTiming.OnTurnEnd,
                    AtomicEffects = new List<AtomicEffectEntry>
                    {
                        // 探针原子须**表域=Self**（GrantTaunt 关键词行自指域）：运行时目标解析按效果头域
                        // 兜底双方单位（旧 ModifyPower+kinds{0} 探针会弹 8 候选被自动代选到别的单位）
                        AtomRefs.New(AtomicEffectType.GrantTaunt)
                    },
                });
                var sleepCard = InjectCard(core, p1, sleepData);

                // 未立约全价口径（豁免前取价，恒推导不钉数值——断言对表价漂移免疫）
                var sleepFull = GameActions.GetCardCost(sleepCard);
                int grayFull = (int)sleepFull[ManaType.Gray], greenFull = (int)sleepFull[ManaType.Green];
                int totalLayers = grayFull + greenFull;
                Assert(totalLayers > 0, $"前置：本卡推导含可豁免灰/绿费（灰{grayFull}+绿{greenFull}）");

                // 苏醒立约（2026-10-03 定案）：豁免费须先在手牌中发动苏醒（AwakenCommitted 预立约），
                // 未立约=照常全价——先立约再打出，灰、绿份额才转沉睡时长（2026-10-22 扩灰+绿）
                Assert(GameActions.CommitAwaken(core, p1, sleepCard, out var awakenReject),
                       $"苏醒立约成功（实际拒绝：{awakenReject}）");
                int grayBefore = pool1.TempElements[ManaType.Gray];
                int greenBefore = pool1.TempElements[ManaType.Green];
                Assert(GameActions.PlayCard(core, p1, sleepCard), "打出自我沉睡生物（灰、绿全免 0 费）");
                GameActions.DrainStack(core);

                Assert(pool1.TempElements[ManaType.Gray] == grayBefore && pool1.TempElements[ManaType.Green] == greenBefore,
                       $"灰、绿全免：两色均未扣（灰{grayFull}+绿{greenFull}转沉睡时长）");
                Assert(core.ZoneManager.IsCardInZone(sleepCard, p1, Zone.Battlefield), "已入场");
                Assert(sleepCard.IsTapped() && sleepCard.GetCounterCount(Attribute.KeywordRules.SleepCounter) == totalLayers,
                       $"入场沉睡：横置+沉睡指示物{totalLayers}层（=豁免灰+绿量；实际 {sleepCard.GetCounterCount(Attribute.KeywordRules.SleepCounter)} 层）");

                // ---- 2. 周期推进：沉睡期间无法重置 + OnTurnEnd 被拦；totalLayers 次己方回合结束后倒数归零 ----
                // 周期 = p1回合结束(③块倒数+触发口被拦) → p2空过 → p1回合开始(BlocksUntap 判定) → 回主阶段
                // （2026-10-08 层即持续定案：倒数从回合开始挪回合末，苏醒特判退役——归零后下回合开始自然重置）
                for (int cycle = 1; cycle <= totalLayers; cycle++)
                {
                    int countersBefore = sleepCard.GetCounterCount(Attribute.KeywordRules.SleepCounter);
                    EndTurnPumped(core, p1);                 // p1 回合结束：③块倒数 −1 层；沉睡中 → OnTurnEnd 被拦
                    int tickedTo = sleepCard.GetCounterCount(Attribute.KeywordRules.SleepCounter);
                    Assert(tickedTo == countersBefore - 1,
                           $"周期{cycle}：回合末倒数一层（{countersBefore}→{tickedTo}）");
                    GameActions.SkipElementPool(core, p2);
                    EndTurnPumped(core, p2);                 // p2 空过 → p1 回合开始
                    GameActions.SkipElementPool(core, p1);

                    if (tickedTo == 0)
                        Assert(!sleepCard.IsTapped(), "苏醒：层归零后下回合开始自然重置");
                    else
                        Assert(sleepCard.IsTapped(), $"周期{cycle}：沉睡期间无法重置（仍横置）");
                    Assert(!sleepCard.HasKeyword("Taunt"),
                           $"周期{cycle}：沉睡期间 OnTurnEnd 效果无效（未获嘲讽）");
                }
                Assert(!sleepCard.IsTapped() && sleepCard.GetCounterCount(Attribute.KeywordRules.SleepCounter) == 0, "倒数归零后完全苏醒");

            // ---- 3. 苏醒后 OnTurnEnd 恢复 ----
            Crumb($"awake pre: sleep={sleepCard.GetCounterCount(Attribute.KeywordRules.SleepCounter)} tapped={sleepCard.IsTapped()}");
            EndTurnPumped(core, p1);
            FillSixColors(pool1, 99); // 回合结束清空钱包后再灌——
            // 触发式效果执行走执行器计费口，欠费会静默跳过（排干前灌足保证金可付）
            GameActions.DrainStack(core); // OnTurnEnd 触发式入栈后显式排干（同打出路径 PlayCardSync 先例）
            GameActions.SkipElementPool(core, p2);
            EndTurnPumped(core, p2);
            GameActions.SkipElementPool(core, p1);
            Assert(sleepCard.HasKeyword("Taunt"), "苏醒后 OnTurnEnd 效果恢复生效（获嘲讽——GrantTaunt 自指探针）");

                // ---- 4. 定长沉睡（Value 显式）：灰费**不**豁免，层数=Value ----
                var fixedData = new CardData
                {
                    ID = "VERIFY_SLEEP_FIXED", CardName = "验证定长沉睡", Supertype = Cardtype.Creature, Power = 3, Life = 3,
                    Cost = CostOf((ManaType.Gray, 2), (ManaType.Green, 1)),
                };
                fixedData.Effects.Add(new CardEffectData
                {
                    Id = "VERIFY_SLEEP_FIXED_EFF",
                    TriggerTiming = (int)TriggerTiming.OnPlay,
                    AtomicEffects = new List<AtomicEffectEntry>
                    {
                        AtomRefs.New(AtomicEffectType.Sleep, value: 2, kinds: new List<int> { 0 }), // 收窄域={Self} 定长 2
                    },
                });
                var fixedCard = InjectCard(core, p1, fixedData);
                FillSixColors(pool1, 99); // 上一周期的回合结束已清空钱包——重灌
                int grayBefore4 = pool1.TempElements[ManaType.Gray];
                Assert(GameActions.PlayCard(core, p1, fixedCard), "打出定长沉睡生物");
                GameActions.DrainStack(core);
                Assert(pool1.TempElements[ManaType.Gray] == grayBefore4 - 2,
                       $"定长沉睡不走灰豁免：照扣 2 灰（实际余 {pool1.TempElements[ManaType.Gray]}）");
                Assert(fixedCard.GetCounterCount(Attribute.KeywordRules.SleepCounter) == 2,
                       $"定长沉睡：层数=Value 2（实际 {fixedCard.GetCounterCount(Attribute.KeywordRules.SleepCounter)}）");
            }
            finally
            {
                foreach (var kv in snapBank) pool1.TempElements[kv.Key] = kv.Value;
                // 战场残留（沉睡生物/同伴）送墓清理，不污染后续段落
                foreach (var c in core.ZoneManager.GetCards(p1, Zone.Battlefield)
                             .Where(c => c.ID.StartsWith("VERIFY_SLEEP")).ToList())
                    core.ZoneManager.MoveCard(c, p1, Zone.Battlefield, Zone.Graveyard);
            }
        }

        // ======================================== 黑白元素经济（2026-09-11 定案） ========================================

        /// <summary>
        /// 黑白元素经济端到端：①错边原子出计价+结算发放（产出不封，2026-10-04）②黑白支付侧
        /// （本色费/浓度上限/**统一混付规划器**：同色→灰→黑→白，灰也入浓度上限，实发组合事件）
        /// ③代价强制（2026-09-14 撤销代价可选：Payload 恒执行+恒补偿，无减费通道）
        /// ④Payload 效果型代价（对手召唤+得白）⑤元素支付（万用填充/灰帽/真负例）
        /// ⑥流失改扣 MaxHealth ⑦含黑白费用卡作地牌不产黑白指示物
        /// ⑧2026-10-04 本轮新规：全价过地牌门槛/代价栏计效果槽/无有效目标回手/先扣卡费·补偿后置/生命恢复 Damaged 目标。
        /// </summary>
        private static void TestBlackWhiteEconomy(GameCore core, Player p1, Player p2)
        {
            EnsureMainPhase(core, p1);

            var pool1 = core.ElementPool.GetPool(p1);
            var snapBank = new Dictionary<ManaType, int>(pool1.TempElements);
            int snapTurnIdx = pool1.GlobalTurnIndex;
            int snapBlackGained = pool1.BlackGainedThisTurn, snapWhiteGained = pool1.WhiteGainedThisTurn;
            int snapGainedLedger = pool1.TempGainedThisTurn; // 全类型累计钳台账（2026-10-16）——随段快照
            int snapMax = p1.MaxHealth, snapLife = p1.Life;

            CardData SpellData(string id, string name, params AtomicEffectEntry[] atoms) => new CardData
            {
                ID = id, CardName = name, Supertype = Cardtype.Spell,
                Effects = new List<CardEffectData>
                {
                    new CardEffectData { Id = id + "_EFF", AtomicEffects = new List<AtomicEffectEntry>(atoms) },
                },
            };
            // 局部 Atom 已收敛到类级助手（引用化适配）；本段语义不变

            try
            {
            // ---- 1. 错边结算发放（双域实际打错边）：DealDamage(p=−1) 双域全价打出、实际命中自己 → 得黑
            //      2026-10-16 全类型累计钳终版：六色同池，单周期（自己回合开始清零）累计生成 ≤ 地牌上限
            //      ——单次入账受剩余配额钳制；「产出不封」的上午单次钳口径已废 ----
            pool1.GlobalTurnIndex = 3; // 地牌上限 3
            FillSixColors(pool1, 99);
            pool1.TempElements[ManaType.Black] = 0; pool1.TempElements[ManaType.White] = 0;
            pool1.BlackGainedThisTurn = 0;
            pool1.TempGainedThisTurn = 0; // 累计钳台账隔离（快照前的段间残留不计入本段配额）

                int UnitGrantOf(AtomicEffectType type, int value)
                {
                    var def = CardEffectConverter.ConvertOne(new CardEffectData
                    {
                        Id = "VERIFY_BW_UNITPROBE",
                        AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(type, value: value) },
                    }, "VERIFY_BW_UNITPROBE");
                    return CostDerivationService.ComputeAtomUnitGrant(def.Effects[0], def);
                }

                // 内容契约下效果栏不可锁错边——双域原子（全价）实际打错边是效果侧唯一错边入口
                var selfHarm = InjectCard(core, p1, SpellData("VERIFY_BW_SELFHARM", "验证自伤",
                    Atom(AtomicEffectType.DealDamage.ToString(), 4))); // 表默认双域 {0,1}
                var selfHarmDef = CardEffectConverter.ConvertOne(((CardWrapper)selfHarm).GetData().Effects[0], "VERIFY_BW_SELFHARM");
                Assert(CostDerivationService.DeriveElementCosts(selfHarmDef).Total > 0,
                       "双域原子构筑期全价（无 grant 记录——运行时实判）");
                Assert(CostDerivationService.DeriveElementGrants(selfHarmDef).IsZero,
                       "双域原子构筑期无黑白获得记录");

                int bwCap = core.ElementPool.GetLandCap(p1);
                int dmg4Unit = UnitGrantOf(AtomicEffectType.DealDamage, 4);
                int expectBlack1 = System.Math.Min(dmg4Unit, bwCap); // 首次：配额全额（cap3）
                int lifeBefore = p1.Life;
                Assert(GameActions.PlayCard(core, p1, selfHarm, new List<Entity> { p1 }), "打出自伤法术（双域指自己=实际错边）");
                GameActions.DrainStack(core);
                Assert(p1.Life == lifeBefore - 4, "自伤结算：扣 4 当前生命");
                Assert(pool1.TempElements[ManaType.Black] == expectBlack1 && pool1.BlackGainedThisTurn == expectBlack1,
                       $"结算发放：得黑 {expectBlack1}（单价{dmg4Unit}·cap{bwCap}；实际 {pool1.TempElements[ManaType.Black]}）");

                // 同回合二次施放：受**全类型累计钳**（2026-10-16 终版）——配额=cap−本周期已得
                var selfHarm2 = InjectCard(core, p1, SpellData("VERIFY_BW_SELFHARM2", "验证自伤2",
                    Atom(AtomicEffectType.DealDamage.ToString(), 1)));
                int quotaLeft = System.Math.Max(0, bwCap - expectBlack1);
                int gain2 = System.Math.Min(UnitGrantOf(AtomicEffectType.DealDamage, 1), quotaLeft);
                int expectBlack2 = expectBlack1 + gain2;
                Assert(GameActions.PlayCard(core, p1, selfHarm2, new List<Entity> { p1 }), "同回合再打一张自伤");
                GameActions.DrainStack(core);
                Assert(pool1.TempElements[ManaType.Black] == expectBlack2 && pool1.BlackGainedThisTurn == expectBlack2,
                       $"累计钳：二次实发 {gain2} = Min(单价, 剩余配额 {quotaLeft})（黑 {expectBlack1}→{expectBlack2}——同周期六色同池累计 ≤ cap{bwCap}）");

                // 翻回合：累计钳台账清零（周期=自己回合开始），新周期配额重计
                core.ElementPool.OnTurnStart(p1, pool1.GlobalTurnIndex + 1);
                Assert(pool1.BlackGainedThisTurn == 0 && pool1.WhiteGainedThisTurn == 0
                       && pool1.TempGainedThisTurn == 0, "回合开始：获得台账清零（新周期配额重计）");
                pool1.GlobalTurnIndex = 3; // 钉回 cap 3（全局口径下翻回合已推到 4——本段「黑4超帽拒付」探针需确定 cap）
                int dmg2Unit = UnitGrantOf(AtomicEffectType.DealDamage, 2);
                int capNew = core.ElementPool.GetLandCap(p1);
                int gain3 = System.Math.Min(dmg2Unit, capNew);
                int expectBlack3 = expectBlack2 + gain3;
                var selfHarm3 = InjectCard(core, p1, SpellData("VERIFY_BW_SELFHARM3", "验证自伤3",
                    Atom(AtomicEffectType.DealDamage.ToString(), 2)));
                Assert(GameActions.PlayCard(core, p1, selfHarm3, new List<Entity> { p1 }), "新周期再打自伤");
                GameActions.DrainStack(core);
                Assert(pool1.TempElements[ManaType.Black] == expectBlack3,
                       $"新周期配额重置：+{gain3} 黑（cap{capNew}·bank 累计 {expectBlack3}；实际 {pool1.TempElements[ManaType.Black]}）");

                // ---- 2. 黑白支付侧：本色费 / 浓度上限 / 灰混付 ----
                var drainDef = CardEffectConverter.ConvertOne(new CardEffectData
                {
                    Id = "VERIFY_BW_DRAIN",
                    AtomicEffects = new List<AtomicEffectEntry> { Atom(AtomicEffectType.DrainLife.ToString(), 2) },
                }, "VERIFY_BW_DRAIN");
                var drainCosts = CostDerivationService.DeriveElementCosts(drainDef);
                Assert(drainCosts[ManaType.Black] > 0,
                       "DrainLife（表色 Black）正确侧计价落黑（不再归一为灰）");

                pool1.TempElements[ManaType.Black] = 4; pool1.TempElements[ManaType.Gray] = 0;
                var blackDict = ElementCost.FromValue(ManaType.Black, 4f);
                Assert(core.ElementPool.CanPayCostWithAutoTap(blackDict, p1),
                       "钱包豁免（2026-10-16）：黑4 > 地牌上限 3 → 钱包足额仍可付（临时元素超上限）");
                Assert(core.ElementPool.TryPayCostWithAutoTap(blackDict, p1), "黑4 钱包直付并扣除（现取现用）");
                Assert(pool1.TempElements[ManaType.Black] == 0, "黑已扣（临时钱包记账）");
                Assert(!core.ElementPool.TryPayCostWithAutoTap(ElementCost.FromValue(ManaType.Black, 1f), p1),
                       $"余额照拒：黑已空 → 黑1 拒付（浓度钳退役后由余额定）——诊断 Any={pool1.TempElements[ManaType.Any]} 黑={pool1.TempElements[ManaType.Black]}");

                // 统一混付规划器（2026-09-14：出牌/效果费共用 GetBillPaymentPlan，非破坏；
                // 2026-10-16 钱包豁免改版：cap 参数随浓度钳退役，本组钉现行链序）
                Dictionary<ManaType, int> Bank(params (ManaType t, int n)[] cells)
                {
                    var d = new Dictionary<ManaType, int>();
                    foreach (var c in cells) d[c.t] = c.n;
                    return d;
                }

                // 万用序（2026-10-11 链序）：灰2 账单，灰1+红1+黑1+白1 → {灰1, 黑1}
                //（链序 灰→黑→白→红→蓝→绿：黑白垫灰先于三色——旧 10-04「黑白不垫灰」钉已废）
                var wildPlan = CardCore.ElementPaymentValidator.GetBillPaymentPlan(
                    new Dictionary<ManaType, int> { { ManaType.Gray, 2 } },
                    Bank((ManaType.Gray, 1), (ManaType.Red, 1), (ManaType.Black, 1), (ManaType.White, 1)));
                Assert(wildPlan != null
                       && wildPlan.GetValueOrDefault(ManaType.Gray) == 1
                       && wildPlan.GetValueOrDefault(ManaType.Black) == 1
                       && !wildPlan.ContainsKey(ManaType.Red)
                       && !wildPlan.ContainsKey(ManaType.White),
                       "万用序（现行链）：灰2 = 灰1+黑1（黑白垫灰先于三色）");

                // 同色优先：红2 账单，红2+黑9+白9 → 只扣红2（灰不在红链）
                var samePlan = CardCore.ElementPaymentValidator.GetBillPaymentPlan(
                    new Dictionary<ManaType, int> { { ManaType.Red, 2 } },
                    Bank((ManaType.Red, 2), (ManaType.Gray, 9), (ManaType.Black, 9)));
                Assert(samePlan != null && samePlan.Count == 1
                       && samePlan.GetValueOrDefault(ManaType.Red) == 2,
                       "同色优先：红2 有红付红（灰黑不动——灰不垫三色）");

                // 黑白预留序：灰1+黑1 账单，红1+黑1 → 黑先留给本色费，灰由红垫（贪心不串色）
                var reservePlan = CardCore.ElementPaymentValidator.GetBillPaymentPlan(
                    new Dictionary<ManaType, int> { { ManaType.Gray, 1 }, { ManaType.Black, 1 } },
                    Bank((ManaType.Black, 1), (ManaType.Red, 1)));
                Assert(reservePlan != null
                       && reservePlan.GetValueOrDefault(ManaType.Black) == 1
                       && reservePlan.GetValueOrDefault(ManaType.Red) == 1,
                       "黑白本色费先行预留：三色不贪吃黑（灰1 由红垫）");

                // 单向性：黑费四色/白补不了；白费黑补不了
                Assert(CardCore.ElementPaymentValidator.GetBillPaymentPlan(
                           new Dictionary<ManaType, int> { { ManaType.Black, 1 } },
                           Bank((ManaType.Red, 9), (ManaType.Gray, 9), (ManaType.White, 9))) == null,
                       "单向：黑费只能黑付（红蓝绿灰白都补不了）");
                Assert(CardCore.ElementPaymentValidator.GetBillPaymentPlan(
                           new Dictionary<ManaType, int> { { ManaType.White, 1 } },
                           Bank((ManaType.Black, 9))) == null,
                       "单向：白费黑补不了");

                // 钱包豁免（2026-10-16 临时元素超上限改版）：灰3 账单 bank 灰9 → {灰3} 直付
                //（旧「灰浓度帽 cap1=灰1+红1+蓝1 分摊」随浓度钳退役——钱包可用可超上限）
                pool1.GlobalTurnIndex = 1;
                var grayCapPlan = CardCore.ElementPaymentValidator.GetBillPaymentPlan(
                    new Dictionary<ManaType, int> { { ManaType.Gray, 3 } },
                    Bank((ManaType.Gray, 9), (ManaType.Red, 9), (ManaType.Blue, 9), (ManaType.Green, 9)));
                Assert(grayCapPlan != null
                       && grayCapPlan.Count == 1
                       && grayCapPlan.GetValueOrDefault(ManaType.Gray) == 3,
                       "钱包豁免：灰3（旧cap1）= 灰3 直付（超上限不分摊）");
                // 黑白垫灰（2026-10-11 链序复证）：灰3 账单 bank 只有余黑 → {黑3}（黑白只垫灰）
                var bwPadGrayPlan = CardCore.ElementPaymentValidator.GetBillPaymentPlan(
                    new Dictionary<ManaType, int> { { ManaType.Gray, 3 } },
                    Bank((ManaType.Black, 9)));
                Assert(bwPadGrayPlan != null
                       && bwPadGrayPlan.Count == 1
                       && bwPadGrayPlan.GetValueOrDefault(ManaType.Black) == 3,
                       "黑白垫灰：灰3 由黑3 垫付（黑白只垫灰、链序同源）");

                // 任意元素（2026-10-23 定案，42e883b2 元素合成）：Any=万能货币，各链末位垫付
                //（含黑白本色费）；本色优先消耗、Any 保留（链序末端）
                var anyPadRedPlan = CardCore.ElementPaymentValidator.GetBillPaymentPlan(
                    new Dictionary<ManaType, int> { { ManaType.Red, 1 } },
                    Bank((ManaType.Any, 1)));
                Assert(anyPadRedPlan != null
                       && anyPadRedPlan.Count == 1
                       && anyPadRedPlan.GetValueOrDefault(ManaType.Any) == 1,
                       "任意元素垫三色：红1 = Any1（红缺由任意垫付）");
                var anyPadBlackPlan = CardCore.ElementPaymentValidator.GetBillPaymentPlan(
                    new Dictionary<ManaType, int> { { ManaType.Black, 1 } },
                    Bank((ManaType.Any, 1)));
                Assert(anyPadBlackPlan != null
                       && anyPadBlackPlan.Count == 1
                       && anyPadBlackPlan.GetValueOrDefault(ManaType.Any) == 1,
                       "任意元素垫黑白本色费：黑1 = Any1（单向性不拦 Any）");
                var anyKeepPlan = CardCore.ElementPaymentValidator.GetBillPaymentPlan(
                    new Dictionary<ManaType, int> { { ManaType.Red, 1 } },
                    Bank((ManaType.Red, 1), (ManaType.Any, 1)));
                Assert(anyKeepPlan != null
                       && anyKeepPlan.GetValueOrDefault(ManaType.Red) == 1
                       && !anyKeepPlan.ContainsKey(ManaType.Any),
                       "本色优先：红1 有红付红（Any 保留在链尾不消耗）");
                var anyGrayTailPlan = CardCore.ElementPaymentValidator.GetBillPaymentPlan(
                    new Dictionary<ManaType, int> { { ManaType.Gray, 1 } },
                    Bank((ManaType.Any, 1)));
                Assert(anyGrayTailPlan != null
                       && anyGrayTailPlan.GetValueOrDefault(ManaType.Any) == 1,
                       "灰链尾任意垫付：灰1 = Any1（六色全零后 Any 兜底）");
                Assert(CardCore.ElementPaymentValidator.GetBillPaymentPlan(
                           new Dictionary<ManaType, int> { { ManaType.Gray, 1 }, { ManaType.Blue, 1 } },
                           Bank((ManaType.Any, 1))) == null,
                       "Any 单份不重复垫两色：灰1+蓝1 只有 Any1 → 拒付");

                pool1.GlobalTurnIndex = 3;

                // 任意元素端到端（PlanBill 综合规划器 + AddMana 入账，2026-10-23）：隔离遗留地牌后
                // 钱包只 Any1 → 蓝1 账单由 Any 直付并扣除；AddMana(Any) 入账受全类型累计钳同口径
                pool1.PooledCards.Clear(); // 隔离：防遗留锁色地横置垫付污染 Any 直付断言
                pool1.TempGainedThisTurn = 0; // 累计钳台账隔离（前段黑白补偿探针已耗配额——配额重置按全价入账）
                foreach (ManaType t in System.Enum.GetValues(typeof(ManaType)))
                    pool1.TempElements[t] = 0;
                pool1.TempElements[ManaType.Any] = 1;
                Assert(core.ElementPool.TryPayCostWithAutoTap(ElementCost.FromValue(ManaType.Blue, 1f), p1)
                       && pool1.TempElements[ManaType.Any] == 0,
                       "任意元素端到端：蓝1 由钱包 Any1 直付并扣除（地牌清场）");
                Assert(core.ElementPool.AddMana(p1, ManaType.Any, null, 2) == 2
                       && pool1.TempElements[ManaType.Any] == 2,
                       "任意元素入账：AddMana(Any) 落临时钱包（全类型累计钳同口径）");
                pool1.TempElements[ManaType.Any] = 0;

                // 支付链收窄（2026-10-11）：三色仅本色——黑不再垫红；红缺=拒付（先证）
                pool1.TempElements[ManaType.Red] = 0;
                pool1.TempElements[ManaType.Gray] = 0;
                pool1.TempElements[ManaType.Black] = 2;
                Assert(!core.ElementPool.TryPayCostWithAutoTap(ElementCost.FromValue(ManaType.Red, 1f), p1)
                       && pool1.TempElements[ManaType.Black] == 2,
                       "支付链收窄：黑不垫红（红缺拒付、黑不动）");
                // 本色直付 + 实发组合事件：PaidCost=实际货币组合（Trinity/台账读真值）
                ElementPoolPayEvent captured = null;
                void OnPayEvt(ElementPoolPayEvent e) { if (e.Player == p1) captured = e; }
                EventManager.Instance.Subscribe<ElementPoolPayEvent>(OnPayEvt);
                pool1.TempElements[ManaType.Red] = 1;
                bool wildPaid = core.ElementPool.TryPayCostWithAutoTap(ElementCost.FromValue(ManaType.Red, 1f), p1);
                EventManager.Instance.Unsubscribe<ElementPoolPayEvent>(OnPayEvt);
                Assert(wildPaid && pool1.TempElements[ManaType.Red] == 0
                       && pool1.TempElements[ManaType.Black] == 2
                       && captured != null
                       && System.Math.Abs(captured.PaidCost.GetValueOrDefault((int)ManaType.Red) - 1f) < 1e-4
                       && !captured.PaidCost.ContainsKey((int)ManaType.Black),
                       "本色直付：红1 由红支付，PaidCost={红1}");

                // ---- 3. 代价强制（2026-09-14 撤销代价可选）：恒执行+恒补偿，无选择窗口/减费通道 ----
                FillSixColors(pool1, 99);
                pool1.TempElements[ManaType.Black] = 0;
                pool1.BlackGainedThisTurn = 0; // 计数器直控（隔离前段错边发放的累计）
                pool1.WhiteGainedThisTurn = 0; // 纯观测台账隔离（2026-10-16 起不参与钳制）
                pool1.TempGainedThisTurn = 0;  // 全类型累计钳台账同隔离（配额重置——补偿按全价入账）
                var optCtx = new CostContext
                {
                    Payer = p1, ZoneManager = core.ZoneManager, ElementPool = core.ElementPool, Source = p1,
                };

                // 3a. e2e：无头也不再有"默认不付"——代价恒执行（弃1张）+恒补偿（按全价，受地牌上限钳）
                // （2026-09-14 代价原子化：弃牌代价=DiscardCard 原子锁己方手牌域 {5} 的 Payload）
                var discardCostCard = InjectCard(core, p1, SpellData("VERIFY_BW_DISC", "验证弃牌代价"));
                ((CardWrapper)discardCostCard).GetData().Effects[0].Costs =
                    new List<CostEntry>
                    {
                        new CostEntry
                        {
                            CostType = (int)CostType.Payload, Value = 1,
                            payload = AtomRefs.New(AtomicEffectType.DiscardCard, value: 1, kinds: new List<int> { 5 }),
                        },
                    };
                int discGrant = CostDerivationService.PayloadUnitGrant(CardEffectConverter.ConvertPayloadForDisplay(
                    AtomRefs.New(AtomicEffectType.DiscardCard, value: 1, kinds: new List<int> { 5 })));
                int discExpect = discGrant; // cap3 下单次钳不缩量：实发=全价（台账不参与钳制——2026-10-16 口径）
                int handBefore = core.ZoneManager.GetCards(p1, Zone.Hand).Count;
                Assert(GameActions.PlayCard(core, p1, discardCostCard, new List<Entity>()), "打出带弃牌代价的卡");
                GameActions.DrainStack(core);
                Assert(core.ZoneManager.GetCards(p1, Zone.Hand).Count == handBefore - 2,
                       "代价强制：少打出本体+弃1张（代价恒执行，无'不付'选项）");
                Assert(pool1.TempElements[ManaType.Black] == discExpect && pool1.BlackGainedThisTurn == discExpect,
                       $"代价强制补偿：+{discExpect} 黑（弃1张 Payload 全价{discGrant}·生效后立即发放；实际 {pool1.TempElements[ManaType.Black]}）");

                // 3a-2. 出牌两阶段（2026-10-04 定案）：有代价的卡声明期先算代价——
                //      代价目标预选（本卡自身不作代价目标）；代价无目标 → 卡的发动无效（拒发）
                var loneCostCardData = SpellData("VERIFY_BW_LONE", "孤注代价");
                loneCostCardData.Effects[0].Costs = new List<CostEntry>
                {
                    new CostEntry
                    {
                        CostType = (int)CostType.Payload, Value = 1,
                        payload = AtomRefs.New(AtomicEffectType.DiscardCard, value: 1, kinds: new List<int> { 5 }),
                    },
                };
                // 排空 p1 手牌（代价域 {5}=己方手牌，预选剔除本卡后无候选 → 拒发）
                var handSave3a2 = core.ZoneManager.GetCards(p1, Zone.Hand).ToList();
                var hzone3a2 = core.ZoneManager.GetZoneContainer(p1);
                foreach (var h in handSave3a2) hzone3a2.Move(h, Zone.Hand, Zone.Deck);
                var loneCostCard = InjectCard(core, p1, loneCostCardData); // 手牌=仅本卡
                bool lonePlayed = GameActions.PlayCard(core, p1, loneCostCard, new List<Entity>(), Zone.Hand, 0, out var loneReject);
                Assert(!lonePlayed && loneReject != null && loneReject.Contains("代价无目标")
                       && loneCostCard.GetZone() == Zone.Hand,
                       $"两阶段拒发：代价（弃1张）无目标 → 卡的发动无效（拒因：{loneReject}，卡留手牌）");
                foreach (var h in handSave3a2) hzone3a2.Move(h, Zone.Deck, Zone.Hand); // 还原手牌
                core.ZoneManager.GetZoneContainer(p1).Remove(loneCostCard, Zone.Hand); // 清理探针

                // 3b. 直测唯一付费路径 PayWithCompensationAsync（原三选一入口已删——无账单参数，减费通道不存在）
                pool1.BlackGainedThisTurn = 0;
                pool1.WhiteGainedThisTurn = 0; // 纯观测台账隔离（不参与钳制）
                pool1.TempGainedThisTurn = 0;  // 累计钳台账同隔离
                int hand3b = core.ZoneManager.GetCards(p1, Zone.Hand).Count;
                var discCosts = new List<CostInstance>
                {
                    new CostInstance
                    {
                        Type = CostType.Payload,
                        Payloads = new List<CardCore.AtomicEffectInstance>
                        {
                            CardEffectConverter.ConvertPayloadForDisplay(AtomRefs.New(AtomicEffectType.DiscardCard, value: 1, kinds: new List<int> { 5 })),
                        },
                    },
                };
                Assert(CostCompensationService.PayWithCompensationAsync(discCosts, optCtx).GetAwaiter().GetResult(),
                       "PayWithCompensationAsync 执行（cast 付费步/主动共用唯一路径）");
                Assert(core.ZoneManager.GetCards(p1, Zone.Hand).Count == hand3b - 1, "强制路径：代价执行（弃1张）");
                Assert(pool1.TempElements[ManaType.Black] == discExpect + discExpect,
                       $"强制路径补偿：再 +{discExpect} 黑（快照全价 {discGrant}·生效后立即发放；实际 {pool1.TempElements[ManaType.Black]}）");

                // ---- 4. Payload 效果型代价：恒执行（对手召唤）+恒补偿（得白） ----
                // 模板身价入 Cost（2026-10-07 表价清零定案：召唤全价随模板费——零费模板=零补偿）
                var tpl = new CardData
                {
                    ID = "VERIFY_BW_TPL", CardName = "验证白衍生物", Supertype = Cardtype.Creature, Power = 30, Life = 30,
                    Cost = CardCore.ElementCost.FromValue(ManaType.White, 5),
                };
                CardCore.Attribute.Handlers.SummonTokenHandler.ResolveTemplate = id => id == tpl.ID ? tpl : null;
                pool1.TempElements[ManaType.White] = 0;
                pool1.WhiteGainedThisTurn = 0;
                var payloadCard = InjectCard(core, p1, SpellData("VERIFY_BW_PAY", "验证代价效果"));
                ((CardWrapper)payloadCard).GetData().Effects[0].Costs = new List<CostEntry>
                {
                    new CostEntry
                    {
                        CostType = (int)CostType.Payload,
                        payload = AtomRefs.New(AtomicEffectType.SummonToken, value: 1, kinds: new List<int> { 2 }, str: tpl.ID), // 锁对方域（EnemyLivingUnit）→ 受惠侧=对手执行
                    },
                };
                var payDerive = CardCostService.Derive(((CardWrapper)payloadCard).GetData());
                Assert(payDerive.Grants[ManaType.White] >= 1 && payDerive.Grants[ManaType.Black] == 0,
                       "构筑显示：Payload 代价 →「获得白≥1」（CardCostResult.Grants，模式0口径）");

                // 2026-10-09 清理：4a/4b（打出/直测 Payload 恒执行+补偿）两段删除——高费代价卡在低地牌上限
                // 下被全价门槛拦截（2026-10-04 使用侧定案，4c 正例专测该门槛），打出路径搭场过时；
                // 代价恒执行/补偿语义在 TideServer V9.n（代价栏：产出不封·补偿后置）有现行覆盖
                core.ZoneManager.GetZoneContainer(p1).Remove(payloadCard, Zone.Hand); // 清理探针

                // ---- 4c.（2026-10-04 本轮新规镜像）全价门槛 / 代价栏计槽 / 无有效目标回手 / 先扣卡费 ----
                pool1.GlobalTurnIndex = 3; // 上限 3（钉回——门槛探针需要确定 cap）
                // ① 全价过地牌门槛：全价 > 地牌槽上限 → 整卡拒发留手（走不到代价发动）
                var gateCardData = SpellData("VERIFY_BW_GATE", "门槛代价卡");
                gateCardData.Effects[0].Costs = new List<CostEntry>
                {
                    new CostEntry
                    {
                        CostType = (int)CostType.Payload, Value = 1,
                        payload = AtomRefs.New(AtomicEffectType.LifeLoss, value: 8, kinds: new List<int> { 1 }),
                    },
                };
                var gateCard = InjectCard(core, p1, gateCardData);
                int gateGrant = CostDerivationService.PayloadUnitGrant(CardEffectConverter.ConvertPayloadForDisplay(
                    AtomRefs.New(AtomicEffectType.LifeLoss, value: 8, kinds: new List<int> { 1 })));
                Assert(gateGrant > 3, $"前置：门槛探针全价 > 上限3（实际 {gateGrant}）");
                Assert(!GameActions.PlayCard(core, p1, gateCard, new List<Entity>(), Zone.Hand, 0, out var gateReject)
                       && gateReject != null && gateReject.Contains("地牌槽上限")
                       && gateCard.GetZone() == Zone.Hand,
                       $"全价门槛：全价 {gateGrant} > 上限 3 → 整卡拒发留手（拒因：{gateReject}）");
                core.ZoneManager.GetZoneContainer(p1).Remove(gateCard, Zone.Hand); // 清理探针

                // ② 代价栏计 1 效果槽（代价也是效果栏，不是特殊判）
                var slotProbe = SpellData("VERIFY_BW_SLOT", "槽位探针");
                Assert(CostDerivationService.CountEffectSlots(slotProbe) == 1, "槽位：1 效果 = 1 槽");
                slotProbe.PayloadCost = new CostEntry
                {
                    CostType = (int)CostType.Payload, Value = 1,
                    payload = AtomRefs.New(AtomicEffectType.LifeLoss, value: 1, kinds: new List<int> { 1 }),
                };
                Assert(CostDerivationService.CountEffectSlots(slotProbe) == 2, "槽位：+代价栏 = 2 槽（底盘 3 灰同口径扣槽）");

                // ③ 无有效目标回手（手牌域 {5}——非战场域按域重解析，防战场判区误杀）
                pool1.TempElements[ManaType.Gray] = 1; // 恰好够灰1——回退则分文未扣
                var backCardData = SpellData("VERIFY_BW_BACK", "回手代价卡");
                backCardData.Cost = ElementCost.FromValue(ManaType.Gray, 1);
                backCardData.Effects[0].Costs = new List<CostEntry>
                {
                    new CostEntry
                    {
                        CostType = (int)CostType.Payload, Value = 1,
                        payload = AtomRefs.New(AtomicEffectType.DiscardCard, value: 1, kinds: new List<int> { 5 }),
                    },
                };
                var backCard = InjectCard(core, p1, backCardData);
                var backOthers = core.ZoneManager.GetCards(p1, Zone.Hand).Where(h => h != backCard).ToList();
                Assert(backOthers.Count >= 1 && GameActions.PlayCard(core, p1, backCard, new List<Entity>()),
                       "前置：手牌有弃牌目标——声明成功");
                foreach (var h in backOthers) core.ZoneManager.GetZoneContainer(p1).Move(h, Zone.Hand, Zone.Deck); // 窗口内排空
                GameActions.DrainStack(core);
                Assert(backCard.GetZone() == Zone.Hand && pool1.TempElements[ManaType.Gray] == 1,
                       "手牌域代价无有效目标：发动失败回退手牌（不付费不补偿）");
                foreach (var h in backOthers) core.ZoneManager.GetZoneContainer(p1).Move(h, Zone.Deck, Zone.Hand); // 还原

                // ④ 先扣卡费（2026-10-16 恢复 10-04 旧序——10-11「补偿先付垫本卡」回调退役）：窗口内池恶化 →
                //    卡费付不出 → 入墓不回卷、代价未执行（上限不变）、无补偿入账（黑白不互垫——黑3 只黑付）
                var orderCardData = SpellData("VERIFY_BW_ORDER", "顺序代价卡");
                orderCardData.Cost = ElementCost.FromValue(ManaType.Black, 3);
                orderCardData.PayloadCost = new CostEntry
                {
                    CostType = (int)CostType.Payload, Value = 1,
                    payload = AtomRefs.New(AtomicEffectType.LifeLoss, value: 1, kinds: new List<int> { 1 }),
                };
                var orderCard = InjectCard(core, p1, orderCardData);
                pool1.BlackGainedThisTurn = 0; pool1.WhiteGainedThisTurn = 0; // 新回合模拟：台账清零隔离观测
                pool1.TempElements[ManaType.Black] = 3; // 声明期池够（黑3 只黑付——现有钱包足额、不靠补偿垫）
                int orderMaxBefore = p1.MaxHealth;
                Assert(GameActions.PlayCard(core, p1, orderCard, new List<Entity>()), "前置：黑3 池够——声明成功");
                pool1.TempElements[ManaType.Black] = 0; // 响应窗口内元素被耗尽
                GameActions.DrainStack(core);
                Assert(orderCard.GetZone() == Zone.Graveyard && p1.MaxHealth == orderMaxBefore
                       && pool1.TempElements[ManaType.Black] == 0,
                       $"先扣卡费：池恶化付不出 → 入墓不回卷、代价未执行（上限不变）、无补偿入账（补偿须发动成功代价执行后才到账）");

                // ⑤ 生命恢复目标须受伤（Damaged 动态判定 hp<上限——复用既有 DamagedFilter，无标志位）
                var healRow4c = CardCore.Attribute.AtomicEffectTable.GetByType(AtomicEffectType.Heal);
                Assert(healRow4c != null && healRow4c.TargetFilter == "Damaged",
                       "表行单源：回复生命 TargetFilter=Damaged（动态判定）");
                var healCtx4c = new EffectExecutionContext
                {
                    Controller = p1, Source = p1, ZoneManager = core.ZoneManager, ElementPool = core.ElementPool,
                };
                int p1Life4c = p1.Life;
                var candidatesFull = CardCore.Attribute.EffectHandlerRegistry.ResolveCandidates(
                    healRow4c.GetTargetKindList(), healRow4c.TargetFilter, healCtx4c);
                bool p1Damaged4c = p1.Life < p1.MaxHealth;
                Assert((candidatesFull != null && candidatesFull.Contains(p1)) == p1Damaged4c,
                       $"Damaged 动态判定：角色现伤={p1Damaged4c} 与候选一致（hp{p1Life4c}/{p1.MaxHealth}）");
                if (!p1Damaged4c)
                {
                    CardCore.Attribute.KeywordRules.ApplyDamage(p2, p1, 2, isCombat: false);
                    var candidatesHurt = CardCore.Attribute.EffectHandlerRegistry.ResolveCandidates(
                        healRow4c.GetTargetKindList(), healRow4c.TargetFilter, healCtx4c);
                    Assert(candidatesHurt != null && candidatesHurt.Contains(p1), "受伤后入候选（hp<上限 动态生效）");
                }

                // ---- 5. 元素支付（统一混付）：链序垫付 / 钱包超上限直付 / 三色仅本色 ----
                foreach (var t in AllManaTypes()) pool1.TempElements[t] = 0;
                pool1.GlobalTurnIndex = 1; // 上限 1（地牌侧浓度钳；钱包 2026-10-16 起豁免）
                pool1.PooledCards.Clear(); // 隔离：前段遗留锁色地牌（灰锁地会替灰账单横置垫付，污染链序断言）
                var ctx5 = new CostContext
                {
                    Payer = p1, ZoneManager = core.ZoneManager, ElementPool = core.ElementPool, Source = p1,
                };

                // 灰费混付（现行链）：灰2 账单，灰1+红1 → 可付（黑白缺位时三色垫灰）
                foreach (var t in AllManaTypes()) pool1.TempElements[t] = 0;
                pool1.TempElements[ManaType.Gray] = 1;
                pool1.TempElements[ManaType.Red] = 1;
                var grayNeed = new List<CostInstance> { new CostInstance { Type = CostType.ElementConsume, Value = 2, ManaType = ManaType.Gray } };
                bool paidGray = ElementCostPayment.Pay(grayNeed, ctx5);
                Assert(paidGray && pool1.TempElements[ManaType.Gray] == 0 && pool1.TempElements[ManaType.Red] == 0,
                       "灰费混付（现行链）：灰1+红1 付灰2（三色垫灰）");

                // 钱包超上限直付（2026-10-16 临时元素超上限改版）：灰2 账单（旧cap1）灰 bank 2 → 直付
                //（旧「灰贡献 1 后拒付」灰浓度帽随钱包豁免退役）
                pool1.TempElements[ManaType.Gray] = 2;
                pool1.TempElements[ManaType.Red] = 0;
                var gray2 = new List<CostInstance> { new CostInstance { Type = CostType.ElementConsume, Value = 2, ManaType = ManaType.Gray } };
                Assert(ElementCostPayment.Pay(gray2, ctx5) && pool1.TempElements[ManaType.Gray] == 0,
                       "钱包豁免：灰2（旧cap1）灰 bank 2 → 直付（可用可超上限）");

                // 黑白垫灰（2026-10-11 链序）：灰2 账单，灰0+黑9 → 黑垫可付（黑白只垫灰；
                // 旧 10-04「黑白不垫灰」钉与现行链矛盾，已废）
                pool1.TempElements[ManaType.Gray] = 0;
                pool1.TempElements[ManaType.Black] = 9;
                Assert(ElementCostPayment.Pay(gray2, ctx5) && pool1.TempElements[ManaType.Black] == 7,
                       "黑白垫灰：灰2 由黑2 垫付（黑白只垫灰）");

                // 三色仅本色（2026-10-11 收窄）：红1 账单，灰9黑9白9、红 0 → 拒付
                //（红缺口只有红能付——灰黑白都不垫三色；旧 10-04「黑垫红」钉与现行链矛盾，已废）
                FillSixColors(pool1, 9);
                pool1.TempElements[ManaType.Red] = 0;
                var redNeed = new List<CostInstance> { new CostInstance { Type = CostType.ElementConsume, Value = 1, ManaType = ManaType.Red } };
                Assert(!ElementCostPayment.Pay(redNeed, ctx5) && pool1.TempElements[ManaType.Black] == 9,
                       "三色仅本色：红1 · 灰9黑9白9 → 拒付（黑不动——红链=[红]）");

                // 自动横置补足正例（2026-09-30 定案）：bank 全空但未横置地牌可产红 → 自动横置支付
                pool1.PooledCards.Clear();
                var autoTapLand = new PooledCard(core.ZoneManager.GetCards(p1, Zone.Deck)[0], ManaType.Red);
                pool1.PooledCards.Add(autoTapLand);
                foreach (var t in AllManaTypes()) pool1.TempElements[t] = 0;
                var redAuto = new List<CostInstance> { new CostInstance { Type = CostType.ElementConsume, Value = 1, ManaType = ManaType.Red } };
                Assert(ElementCostPayment.Pay(redAuto, ctx5) && autoTapLand.IsTapped
                       && pool1.TempElements[ManaType.Red] == 0,
                       "自动横置补足：红费 bank 空+未横置红地 → 横置产红支付");

                // 真负例（2026-09-30 自动横置口径更新）：红费全空 bank + **无未横置地牌可产** → 拒付
                //（旧口径只清 bank——前段遗留地牌在新口径下会被自动横置垫付，负例不再是负例；
                //  故清空地牌池保持「无地可产 → 拒付」的原意图）
                pool1.PooledCards.Clear();
                foreach (var t in AllManaTypes()) pool1.TempElements[t] = 0;
                Assert(!ElementCostPayment.Pay(redNeed, ctx5),
                       "真负例：红费且红灰黑白全空+无未横置地 → 拒付");

                // ---- 6. 流失改扣 MaxHealth：满血裁剪 / 受伤只扣上限 / 归零正常死亡 ----
                var pl = new Player("VERIFY_BW_MAXHP", 30);
                pl.DecreaseMaxHealth(2);
                Assert(pl.MaxHealth == 28 && pl.Life == 28, "满血扣上限：30/30 → 28/28（超出当前血一起裁）");
                pl.DecreaseMaxHealth(2);
                Assert(pl.MaxHealth == 26 && pl.Life == 26, "连续满血扣：28/28 → 26/26（每次裁到新上限）");
                var pl2 = new Player("VERIFY_BW_MAXHP2", 30);
                pl2.Life = 20;
                pl2.DecreaseMaxHealth(2);
                Assert(pl2.MaxHealth == 28 && pl2.Life == 20, "已受伤（20/30）扣 2 → 20/28（只扣上限）");
                // 流失原子（2026-09-14 代价原子化：LifeLoss=支付载体）——目标=自己 → 扣上限 + 发支付事件
                int evtSum = 0;
                void OnLifePay(LifePaymentCostEvent e) { if (e.Player == pl2) evtSum += e.Amount; }
                EventManager.Instance.Subscribe<LifePaymentCostEvent>(OnLifePay);
                var drainAtom = new AtomicEffectInstance { Type = AtomicEffectType.LifeLoss, Value = 8 };
                var drainCtx = new EffectExecutionContext
                {
                    Controller = pl2, Source = pl2,
                    Targets = new List<Entity> { pl2 },
                };
                CardCore.Attribute.EffectHandlerRegistry.ExecuteEffectAsync(drainAtom, drainCtx).GetAwaiter().GetResult();
                EventManager.Instance.Unsubscribe<LifePaymentCostEvent>(OnLifePay);
                Assert(pl2.MaxHealth == 20 && pl2.Life == 20 && evtSum == 8,
                       $"流失原子：20/28 扣 8 → 20/20（只降上限）+ LifePaymentCostEvent 8（实际 {pl2.MaxHealth}/{pl2.Life}，事件 {evtSum}）");
                var plZero = new Player("VERIFY_BW_MAXHP3", 30);
                var drainAllCtx = new EffectExecutionContext
                {
                    Controller = plZero, Source = plZero,
                    Targets = new List<Entity> { plZero },
                };
                CardCore.Attribute.EffectHandlerRegistry.ExecuteEffectAsync(
                    new AtomicEffectInstance { Type = AtomicEffectType.LifeLoss, Value = 30 }, drainAllCtx).GetAwaiter().GetResult();
                Assert(plZero.MaxHealth == 0 && plZero.Life == 0, "可扣到恰好归零（归零=正常死亡交生命判定收尾）");

                // ---- 7. 含黑白费用的卡作地牌（2026-10-24 存量退役）----
                // 旧断言「红+黑 Cost 卡可入池锁红 / 纯黑拒入池」基于 Cost 字段进锁色候选的前置口径；
                // 费用恒推导（2026-10-22）+锚价开格（2026-10-23「真无费仍拒」+黑白过滤）后，无效果纯
                // Cost 卡恒推导=0 → 拒入池——本意图已由服务器 VerifySuite V9.ac 段 10 断言覆盖
                ///（TideServer verify --quick 全过），不再本地重述（清池残留循环同删——本地夹具专用）。
            }
            finally
            {
                foreach (var kv in snapBank) pool1.TempElements[kv.Key] = kv.Value;
                pool1.GlobalTurnIndex = snapTurnIdx;
                pool1.BlackGainedThisTurn = snapBlackGained;
                pool1.WhiteGainedThisTurn = snapWhiteGained;
                pool1.TempGainedThisTurn = snapGainedLedger;
                // 生命/上限不逐点恢复（后续段落自建夹具；MaxHealth 只在本段被扣，恢复到快照）
                if (p1.MaxHealth != snapMax || p1.Life != snapLife)
                {
                    // 通过反射无法直写——用 IncreaseMaxHealth 补差 + 直调 Life 恢复
                    if (snapMax > p1.MaxHealth) p1.IncreaseMaxHealth(snapMax - p1.MaxHealth);
                    p1.Life = System.Math.Min(snapLife, p1.MaxHealth);
                }
            }
        }

        // ======================================== 抉择（Modal/选发，2026-09-07 定案） ========================================

        /// <summary>
        /// 抉择卡端到端：per-mode 独立计价（构筑期推导存储，发动时只读）+ 声明期先选模式再定费用 +
        /// 只执行所选模式。同时锁死组合计价三规则：①并发取总（4伤并抽1=两笔之和）；
        /// ②条件奖励免费（门后 then/else 不计价）；③抉择各计所需（只付所选模式）。
        /// </summary>
        private static void TestSpellModal(GameCore core, Player p1, Player p2, List<CardData> cardsData)
        {
            EnsureMainPhase(core, p1);

            // ---- 0. 规则①并发取总 / ②条件奖励免费（既有卡锁死断言）----
            // 并发取总（2026-10-15 后语义）：**同一效果内多原子**费用取总——
            // 火球术夹具已是「双效果=施放抉择」，并发语义改由本探针（单效果双原子）钉死
            {
                var concData = new CardData
                {
                    ID = "VERIFY_MODAL_CONC", CardName = "并发取总探针", Supertype = Cardtype.Spell,
                };
                concData.Effects.Add(new CardEffectData
                {
                    Id = "CONC_MAIN",
                    TriggerTiming = (int)TriggerTiming.Activate_Active,
                    SelectionMode = (int)CardCore.SelectionMode.Single,
                    TargetCount = 1,
                    AtomicEffects = new List<AtomicEffectEntry>
                    {
                        AtomRefs.New(AtomicEffectType.DealDamage, value: 4, kinds: new List<int> { 2 }),
                        AtomRefs.New(AtomicEffectType.DrawCard, value: 1),
                    },
                });
                var concDef = CardEffectConverter.ConvertOne(concData.Effects[0], concData.ID);
                var concCosts = CostDerivationService.DeriveElementCosts(concDef);
                int drawPrice = (int)System.Math.Round(Cfg(AtomicEffectType.DrawCard).TotalUnitCost,
                    System.MidpointRounding.AwayFromZero);
                var drawColor = CardCore.ElementAffinities.GetAffinityForEffect(AtomicEffectType.DrawCard).PrimaryColor;
                Assert((int)concCosts[ManaType.Red] == 4,
                       "并发组合：单效果 4伤+抽1 → 红4（同效果原子费用取总）");
                Assert((int)concCosts[drawColor] == drawPrice,
                       "并发组合：抽1=抽牌表价并入总费（费用取总）");
            }

            // ②条件奖励免费（2026-10-05 两槽定案载荷口径）：主干计价 vs 主干+Then 载荷全量——
            // Then 奖励不进 VisitBillableAtoms（分支整体零计价，预算只是放置上限）
            {
                CardCore.EffectDefinition BranchPlain(bool withPayload)
                {
                    var trunk = withPayload
                        ? OutcomeAtom(AtomicEffectType.DealDamage, 3, "DmgKillsTarget",
                            AtomRefs.New(AtomicEffectType.DrawCard, value: 1))
                        : AtomRefs.New(AtomicEffectType.DealDamage, value: 3);
                    return CardEffectConverter.ConvertOne(new CardEffectData
                    {
                        Id = "VERIFY_MODAL_GATE" + (withPayload ? "_B" : "_P"),
                        AtomicEffects = new List<AtomicEffectEntry> { trunk },
                    }, "VERIFY_MODAL_GATE");
                }
                int totalPlain = (int)CostDerivationService.DeriveElementCosts(BranchPlain(false)).Total;
                int totalBranched = (int)CostDerivationService.DeriveElementCosts(BranchPlain(true)).Total;
                Assert(totalPlain > 0 && totalBranched == totalPlain,
                       $"条件奖励不计费：主干+Then 载荷全量 = 主干计价（{totalBranched} vs {totalPlain}——Then 不进 VisitBillableAtoms）");
            }

            // ---- 1. 抉择卡数据/转换/判据（2026-10-15：多主动效果=施放抉择，无 Choice 步）----
            var data = cardsData.FirstOrDefault(c => c.CardName == "抉择试作");
            Assert(data != null, "抉择卡配置存在（抉择试作）");
            if (data == null) return;

            Assert(data.Effects.Count == 2
                   && data.Effects.All(e => e.TriggerTiming == (int)TriggerTiming.Activate_Active),
                   "抉择判据：两主动效果=施放时 modeIndex 二选一（内嵌 Choice 步已随 10-15 改版退役）");
            var defs = CardEffectConverter.ConvertAll(data.Effects, data.ID);
            Assert(defs.Count == 2 && defs.All(d => d.Steps == null || d.Steps.Count == 0),
                   "转换：两效果平铺（无 Choice 步——modeIndex=效果序号）");

            // ---- 2. per-mode 账单单源（2026-10-15：账单=卡面费+所选效果锚价 GetCardCost 单源；
            //      旧「底盘抵价」通道随槽位从速度扣改版退役——测试不钉数，钉「按模式独立、非求和」） ----
            var modalDrawColor = CardCore.ElementAffinities.GetAffinityForEffect(AtomicEffectType.DrawCard).PrimaryColor;
            var pool1 = core.ElementPool.GetPool(p1);
            var snap = new Dictionary<ManaType, int>(pool1.TempElements);
            int idxSnap = pool1.GlobalTurnIndex;
            pool1.GlobalTurnIndex = 9;
            pool1.TempElements[ManaType.Red] += 20;
            pool1.TempElements[modalDrawColor] += 9;

            var probeCard = InjectCard(core, p1, data);
            var mode0Bill = CardCore.ElementPaymentValidator.NormalizeBill(GameActions.GetCardCost(probeCard, 0));
            var mode1Bill = CardCore.ElementPaymentValidator.NormalizeBill(GameActions.GetCardCost(probeCard, 1));
            core.ZoneManager.GetZoneContainer(p1).Remove(probeCard, Zone.Hand); // 探针即弃
            Assert(mode0Bill[ManaType.Red] > 0 && mode0Bill.GetValueOrDefault(modalDrawColor) == 0,
                   "模式0账单：伤害锚红（per-mode 独立——恒推导口径：模式费=该模式效果推导价）");
            // 2026-10-24 适配费用恒推导定案（2026-10-22：GetRuntimeCost 单源、costList=显示缓存）：
            // 模式费按所选效果独立推导、无「卡面费两模式同担」——模式1=纯抽牌锚（蓝），红份额恒 0
            //（旧断言直取 mode1Bill[Red] 在恒推导下 KeyNotFound 中断全量验证）。
            Assert(mode1Bill.GetValueOrDefault(modalDrawColor) > 0
                   && mode1Bill.GetValueOrDefault(ManaType.Red) < mode0Bill[ManaType.Red],
                   "模式1账单：抽牌锚价落蓝（per-mode 独立不求和——恒推导：模式1无红份额）");

            try
            {
                // ---- 3. 模式0端到端（缺省参数=0）：只伤不抽 ----
                int p2Life = p2.Life;
                int deckBefore = core.ZoneManager.GetCards(p1, Zone.Deck).Count;
                int redBefore = pool1.TempElements[ManaType.Red];
                var modal0 = InjectCard(core, p1, data);

                // 伤害事件捕获（诊断）：观察模式0结算期间实际发生的每笔伤害（目标/来源/量）
                var dmgSeen = new List<string>();
                void OnModalDamage(DamageEvent e) =>
                    dmgSeen.Add($"{(e.Target is Player pl ? pl.Name : (e.Target as Card)?.ID)}<-{e.Amount}(src={(e.Source is Card sc ? sc.ID : "Player")})");
                EventManager.Instance.Subscribe<DamageEvent>(OnModalDamage);
                try
                {
                    Assert(PlayCardSync(core, p1, modal0, new List<Entity> { p2 }),
                           "抉择卡模式0打出（缺省 mode=0）");
                }
                finally
                {
                    EventManager.Instance.Unsubscribe<DamageEvent>(OnModalDamage);
                }
                Assert(p2.Life == p2Life - 4,
                       $"模式0：对目标造成4伤（实际 {p2Life}→{p2.Life}；期间伤害事件 [{string.Join("; ", dmgSeen)}]；p2 护甲={p2.GetCounterCount(CardCore.Attribute.KeywordRules.ArmorCounter)} 易损={p2.GetCounterCount(CardCore.Attribute.CounterRules.VulnerableCounter)}）");
                Assert(core.ZoneManager.GetCards(p1, Zone.Deck).Count == deckBefore, "模式0：不抽牌（只执行所选）");
                Assert(pool1.TempElements[ManaType.Red] == redBefore - mode0Bill[ManaType.Red],
                       $"模式0实付红 {mode0Bill[ManaType.Red]}（GetCardCost 单源账单——先选择模式再定费用；实际扣 {redBefore - pool1.TempElements[ManaType.Red]}）");
                Assert(core.ZoneManager.GetCards(p1, Zone.Graveyard).Contains(modal0)
                       && core.ZoneManager.GetCards(p1, Zone.Activation).Count == 0, "抉择法术结算后入墓、发动区清空");

                // ---- 4. 模式1端到端：只抽不伤 ----
                p2Life = p2.Life;
                deckBefore = core.ZoneManager.GetCards(p1, Zone.Deck).Count;
                int drawBefore = pool1.TempElements[modalDrawColor];
                var modal1 = InjectCard(core, p1, data);

                Assert(PlayCardSync(core, p1, modal1, null, modeIndex: 1), "抉择卡模式1打出（先选模式再定费用）");
                Assert(core.ZoneManager.GetCards(p1, Zone.Deck).Count == deckBefore - 1, "模式1：抽一张牌");
                Assert(p2.Life == p2Life, "模式1：不造伤害（执行隔离）");
                Assert(pool1.TempElements[modalDrawColor] == drawBefore - mode1Bill[modalDrawColor],
                       $"模式1实付={mode1Bill[modalDrawColor]} {modalDrawColor}（per-mode 独立账单；实际扣 {drawBefore - pool1.TempElements[modalDrawColor]}）");

                // ---- 5. 防超发：pending 按声明模式计 ----
                // 混付垫付会绕过「精确同色占用即拒」：清全色 bank 只留单源账单（根因 A 修复）
                foreach (var t in AllManaTypes()) pool1.TempElements[t] = 0;
                foreach (var kv in mode0Bill) pool1.TempElements[kv.Key] = kv.Value; // bank 恰好一张模式0
                var overA = InjectCard(core, p1, data);
                var overB = InjectCard(core, p1, data);
                Assert(GameActions.PlayCard(core, p1, overA, new List<Entity> { p2 }, Zone.Hand, 0),
                       "防超发：第一张模式0声明上栈（声明期不付费）");
                Assert(!GameActions.PlayCard(core, p1, overB, new List<Entity> { p2 }, Zone.Hand, 0),
                       "防超发：同笔 bank 第二张模式0被拒（GetPendingCastCosts 按模式计）");
                GameActions.DrainStack(core);
                // 实付=单源账单（同模式0断言口径）→ 恰好扣空（第二张拒付不扣款）
                Assert(AllManaTypes().All(t => pool1.TempElements[t] == 0),
                       "防超发：结算后实付扣空（第二张拒付不扣款）");
            }
            finally
            {
                foreach (var kv in snap) pool1.TempElements[kv.Key] = kv.Value;
                pool1.GlobalTurnIndex = idxSnap;
            }

            // ---- 6. 无效数据容错：choices<2 整步跳过 ----
            var bad = new CardData { ID = "VERIFY_MODAL_BAD", CardName = "无效抉择" };
            bad.Supertype = Cardtype.Spell;
            bad.Effects.Add(new CardEffectData
            {
                Id = "BAD_MAIN",
                Steps = new List<EffectStepData>
                {
                    new EffectStepData
                    {
                        kind = 2,
                        choices = new List<EffectChoiceData> { new EffectChoiceData() }, // 仅 1 个模式 → 无效
                    }
                }
            });
            var badDefs = CardEffectConverter.ConvertAll(bad.Effects, bad.ID);
            Assert(badDefs.Count == 1 && (badDefs[0].Steps == null || badDefs[0].Steps.Count == 0),
                   "choices<2：整步跳过不抛异常（等价无该步骤）");
            Assert(!CostDerivationService.HasChoiceEffect(bad) && CostDerivationService.GetModeCount(bad) == 1,
                   "无效抉择不计模式数");

            // ---- 7. 功能哈希：效果顺序参与（不碰撞）——平铺双效果（modeIndex=效果序），换序=不同卡 ----
            var swap = new CardData { ID = data.ID, CardName = data.CardName };
            swap.Supertype = data.Supertype;
            swap.Cost = data.Cost != null ? data.Cost.Clone() : null;
            swap.Effects.Add(data.Effects[1]); // 换序：抽牌在前
            swap.Effects.Add(data.Effects[0]);
            Assert(SynergyUI.ContentHasher.HashCard(data) != SynergyUI.ContentHasher.HashCard(swap),
                   "抉择模式顺序参与功能哈希（效果序=模式序，换序不碰撞）");

            // ---- 8. MemoryPack DTO 往返（legacy Choice 步通道——独立夹具，不依赖卡面格式）----
            var dtoStep = new EffectStepData
            {
                kind = 2,
                choices = new List<EffectChoiceData>
                {
                    new EffectChoiceData { steps = new List<EffectStepData> { new EffectStepData
                        { kind = 0, atomic = AtomRefs.New(AtomicEffectType.DealDamage, value: 4) } } },
                    new EffectChoiceData { steps = new List<EffectStepData> { new EffectStepData
                        { kind = 0, atomic = AtomRefs.New(AtomicEffectType.DrawCard, value: 1) } } },
                },
            };
            var ser = CardCore.Serialization.SerializableEffectStepData.FromData(dtoStep);
            var back = ser.ToData();
            Assert(back.choices != null && back.choices.Count == 2
                   && back.choices[0].steps[0].atomic.refId == CardCore.Attribute.AtomicEffectTable.GetByEnumName("DealDamage")?.HashId
                   && back.choices[1].steps[0].atomic.refId == CardCore.Attribute.AtomicEffectTable.GetByEnumName("DrawCard")?.HashId,
                   "序列化往返保抉择结构（模式数与原子类型——legacy Choice 步 DTO 通道）");
        }

        // ======================================== 攻击/守卫效果化（2026-09-10；2026-09-16 战斗接入栈机器重写） ========================================

        /// <summary>
        /// 攻击/守卫端到端（2026-09-16 逐攻击开窗定案；2026-10-15 反击定案修订）：
        /// - 攻击=速度0栈对象：宣言零支付上栈开窗，结算期重检→战斗三段→战斗后横置支付；
        /// - 守卫=速度1响应：窗口内 PushGuardDeclaration 入栈（资格闸 NoGuard/横置/存活），
        ///   LIFO 先结算——攻击目标变更+GuardBlocker 登记（守卫横置随攻击结算在战斗后支付）；
        /// - 双向结算基线（2026-10-15）：双方以可用状态完成战斗——守卫照常反击，战斗后才横置；
        /// - 警戒：已横置防守者仍反击（拦截局战斗期间本就可用，反击不再依赖警戒）；
        /// - 速度门槛：标准速度门（0≥计数器）——连锁开启时攻击不可宣言；
        /// - 法术底盘剩余提速（2026-10-10 可支配底盘）：SpeedBonus 卡级增量与全部效果 BaseSpeed 加和。
        /// </summary>
        private static void TestAttackGuard(GameCore core, Player p1, Player p2)
        {
            EnsureMainPhase(core, p1);
            var combat = core.CombatSystem;
            int seq = 0;

            Card Spawn(Player owner, int power, int life, bool noAttack = false, bool noGuard = false, params string[] keywords)
            {
                var data = new CardData
                {
                    ID = "VERIFY_AG_" + (++seq), CardName = "AG" + seq,
                    Supertype = Cardtype.Creature, Power = power, Life = life,
                    NoAttack = noAttack, NoGuard = noGuard,
                };
                foreach (var kw in keywords) data.Keywords.Add(kw);
                var card = new CardWrapper(data);
                card.SetController(owner);
                Assert(core.ZoneManager.TryAddToBattlefield(card, owner), $"AG 合成随从入场（{data.CardName}）");
                card.Untap(); // 本段测试前提 = 可用随从（2026-10-17 入场即可用后为无害冗余）
                return card;
            }

            // ---- 1. NoAttack 资格（墙不能攻；同身材正常卡能攻） ----
            var wall = Spawn(p1, 3, 5, noAttack: true);
            var hitter = Spawn(p1, 3, 3);
            Assert(!combat.CanDeclareAttack(wall, p1), "攻击效果化：NoAttack 卡不能宣言攻击");
            Assert(combat.CanDeclareAttack(hitter, p1), "攻击效果化：默认自带攻击能力");

            // ---- 2. 守卫拦截：打脸攻击 → 窗口守卫响应 → 目标转移 + 双向结算 + 战斗后双方横置 ----
            var striker = Spawn(p1, 4, 2);       // 4 攻
            var shield = Spawn(p2, 3, 6);        // 3 攻 6 血守卫
            int p2Life0 = p2.Life;
            Assert(GameActions.DeclareAttack(core, p1, striker, p2), "守卫段：打脸宣言上栈（速度0开窗，宣言期零支付）");
            Assert(!striker.IsTapped(), "守卫段：宣言期不横置（战斗后支付）");
            var attackInstance = core.StackEngine.Peek();
            Assert(core.StackEngine.PushGuardDeclaration(shield, attackInstance, p2), "守卫段：守卫速度1响应入栈");
            GameActions.DrainStack(core);        // 守卫先结（目标变更+登记）→ 攻击三段（双向）→ 战斗后双方横置
            Assert(shield.IsTapped(), "守卫拦截：战斗后横置（战斗期间以可用状态参战）");
            Assert(shield.IsAlive && shield.GetLife() == 2, "守卫拦截：守卫吃 4 伤（6→2）");
            Assert(!striker.IsAlive, "守卫反击：3 反伤致死 4/2 攻击者（不计关键词的基线=双向结算）");
            Assert(p2.Life == p2Life0, "守卫拦截：玩家未被击中（目标已转移）");

            // ---- 2b. 警戒（2026-10-15 现职责）：已横置防守者被攻击仍反击 ----
            //（拦截局守卫战斗期间本就可用、反击不依赖警戒；普通横置不反击见 8c 对照）
            var raider = Spawn(p1, 4, 2);
            var valiant = Spawn(p2, 3, 6, false, false, CardCore.Attribute.KeywordRules.Vigilance);
            valiant.Tap(); // 模拟已横置（刚攻击过/被冻结）
            Assert(GameActions.DeclareAttack(core, p1, raider, valiant), "警戒段：攻击已横置警戒者宣言上栈");
            GameActions.DrainStack(core);
            Assert(valiant.IsTapped() && valiant.IsAlive && valiant.GetLife() == 2,
                   "警戒：已横置防守者吃伤存活（6−4=2）");
            Assert(!raider.IsAlive, "警戒：已横置防守者仍反击（3 反伤致死 4/2 攻击者）");

            // ---- 2c. NoGuard 不能拦（守卫入栈资格闸） ----
            var charger2 = Spawn(p1, 4, 2);
            var pacifist = Spawn(p2, 3, 6, noGuard: true);
            Assert(GameActions.DeclareAttack(core, p1, charger2, p2), "NoGuard 段：打脸宣言上栈");
            Assert(!core.StackEngine.PushGuardDeclaration(pacifist, core.StackEngine.Peek(), p2),
                   "守卫效果化：NoGuard 卡不能响应拦截（资格闸拒绝）");
            GameActions.DrainStack(core);        // 无守卫 → 攻击照常结算（打脸）
            Assert(pacifist.GetLife() == 6, "NoGuard 段：未拦截（守卫满血）");

            // ---- 2d. 激励再拦：守卫解除横置后可拦下一攻（逐攻击开窗） ----
            var a1 = Spawn(p1, 2, 2);
            var a2 = Spawn(p1, 3, 2);
            var gatekeeper = Spawn(p2, 1, 6);
            Assert(GameActions.DeclareAttack(core, p1, a1, p2), "再拦段：首攻宣言上栈");
            var attackA1 = core.StackEngine.Peek();
            Assert(core.StackEngine.PushGuardDeclaration(gatekeeper, attackA1, p2), "再拦段：首拦入栈");
            Assert(!core.StackEngine.PushGuardDeclaration(gatekeeper, attackA1, p2),
                   "再拦段：同窗二拦被拒（速度门 1 不> 1——记速器已被守卫抬到 1）");
            GameActions.DrainStack(core);
            Assert(gatekeeper.IsTapped() && gatekeeper.GetLife() == 4, "再拦段：首拦结算（6−2=4，战斗后横置）");
            Assert(a1.IsAlive && a1.GetLife() == 1 && a1.IsTapped(),
                   "再拦段：守卫反 1 伤（2/2 攻击者剩 1 活、战斗后横置）");
            gatekeeper.Untap(); // 激励通路
            Assert(GameActions.DeclareAttack(core, p1, a2, p2), "再拦段：第二攻宣言上栈（新窗口）");
            Assert(core.StackEngine.PushGuardDeclaration(gatekeeper, core.StackEngine.Peek(), p2),
                   "再拦段：解除横置可拦第二攻击者");
            GameActions.DrainStack(core);
            Assert(gatekeeper.IsAlive && gatekeeper.GetLife() == 1,
                   "再拦段：两拦都结算（6−2−3=1）");
            Assert(a2.IsAlive && a2.GetLife() == 1, "再拦段：守卫再反 1 伤（3/2 攻击者剩 1 活）");

            // ---- 3. 速度门槛：标准速度门（0≥计数器）——连锁开启时攻击不可宣言 ----
            var runner = Spawn(p1, 3, 3);
            var dummyData = new CardData
            {
                ID = "VERIFY_AG_CAST", CardName = "AG占位", Supertype = Cardtype.Spell,
                Effects = new List<CardEffectData>
                {
                    // 2026-09-13 记速器=峰值模型：0 速 cast 不抬记速器——占位卡带 1 速才连锁开启
                    new CardEffectData
                    {
                        Id = "VERIFY_AG_CAST_E", BaseSpeed = 1,
                        AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DrawCard, value: 1) },
                    },
                },
            };
            var dummy = new CardWrapper(dummyData);
            dummy.SetController(p1);
            core.StackEngine.PushCardCast(dummy, p1, null); // 1 速 cast → 记速器抬到 1（连锁开启）
            Assert(!GameActions.DeclareAttack(core, p1, runner, p2),
                   "速度门槛：连锁开启（记速器>0）时 0 速攻击不可宣言（0≥1 不达）");
            core.StackEngine.Clear();                        // 记速器归零
            Assert(GameActions.DeclareAttack(core, p1, runner, p2), "速度门槛：连锁闭合后攻击恢复（0≥0）");
            GameActions.DrainStack(core);                    // 攻击收口（3 攻打脸）

            // ---- 4. 法术速度=3−槽数（2026-10-15 改版：槽位从速度扣，动态算不落字段）----
            var swiftData = new CardData
            {
                ID = "VERIFY_AG_SWIFT", CardName = "疾风术", Supertype = Cardtype.Spell,
                SpeedBonus = 1, // 字段退役——脏数据探针：写值不得影响动态速度
                Effects = new List<CardEffectData>
                {
                    new CardEffectData
                    {
                        Id = "VERIFY_AG_SWIFT_E",
                        AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DrawCard, value: 1) },
                    },
                },
            };
            var swift = new CardWrapper(swiftData);
            var swiftDefs = GameActions.GetCardEffectDefinitions(swift);
            Assert(swiftDefs.Count == 1 && swiftDefs[0].BaseSpeed == 2,
                   $"法术 1 槽=速2（实际 {swiftDefs[0].BaseSpeed}；SpeedBonus 脏数据无效）");
            var plainData = new CardData
            {
                ID = "VERIFY_AG_PLAIN", CardName = "平风术", Supertype = Cardtype.Spell,
                Effects = new List<CardEffectData> { swiftData.Effects[0] },
            };
            Assert(GameActions.GetCardEffectDefinitions(new CardWrapper(plainData))[0].BaseSpeed == 2,
                   "法术缺省 1 槽=速2（2026-10-15 改版——旧「缺省 0 速走抵价」翻转）");
            var dualSlot = new CardData
            {
                ID = "VERIFY_AG_DUAL", CardName = "双策术", Supertype = Cardtype.Spell,
                Effects = new List<CardEffectData> { swiftData.Effects[0], swiftData.Effects[0] },
            };
            var dualDefs = GameActions.GetCardEffectDefinitions(new CardWrapper(dualSlot));
            Assert(dualDefs.Count == 2 && dualDefs[0].BaseSpeed == 1,
                   "法术 2 槽=速1（槽位从速度扣）");
            var bioSwiftData = new CardData
            {
                ID = "VERIFY_AG_BIO_SWIFT", CardName = "疾风兽", Supertype = Cardtype.Creature, Power = 1, Life = 1,
                SpeedBonus = 1, // 脏数据探针：生物带 SpeedBonus 也不得生效
                Effects = new List<CardEffectData> { swiftData.Effects[0] },
            };
            Assert(GameActions.GetCardEffectDefinitions(new CardWrapper(bioSwiftData))[0].BaseSpeed == 0,
                   "生物无提速通道：SpeedBonus 仅法术生效（生物速度只由效果组合阶段 BaseSpeed 决定）");

            RetireCards(core, p1, wall, hitter, striker, raider, charger2, runner, a1, a2);
            RetireCards(core, p2, shield, valiant, pacifist, gatekeeper);
        }

        // ======================================== 使用时点 / 响应窗口（Phase 2） ========================================

        /// <summary>
        /// 使用时点 + 响应窗口端到端（Option Y 定案；合成卡驱动，不依赖卡表）：
        /// - 打出即声明上栈（声明期不付费），对手获得优先权；
        /// - 发动无效（2026-10-04 两层无效定案，原打落承接）：发动区卡送墓 → cast 中止：不付费（净效果=扣费返还）、无效果；
        /// - 效果无效（承接旧标记实现）：cast 照常付费但跳效果入墓，费用不退；
        /// - 声明超发保护：同笔 bank 已被在栈 cast 占用时拒绝新声明（防超发，不回卷）。
        /// </summary>
        private static void TestCounterWindow(GameCore core, Player p1, Player p2)
        {
            EnsureMainPhase(core, p1);
            // 段落卫生（2026-09-21）：前一 TestEquipment 的结算若产生死亡，SBA（速度1）会留在
            // 栈上开窗（记速器=1）——先排干再开始本段，否则 0 速火球过不了速度门、响应窗口错乱
            GameActions.DrainStack(core);

            // 合成法术：显式灰 1 费（绕开推导），原子由参数给定
            // 2026-09-13 修复：反制卡（发动无效/效果无效）须配 2 速——速度峰值模型下非回合方过门槛
            // 要求严格 > 记速器（0>0 恒假，0 速响应全被拒）；主动出的火球保持 0 速（不抬记速器）
            CardData SpellData(string id, string name, int baseSpeed = 0, params AtomicEffectEntry[] atoms)
            {
                var data = new CardData { ID = id, CardName = name };
                data.Supertype = Cardtype.Spell;
                data.Cost = CostOf((ManaType.Gray, 1));
                data.Effects.Add(new CardEffectData
                {
                    Id = id + "_MAIN",
                    DisplayName = name,
                    BaseSpeed = baseSpeed,
                    AtomicEffects = new List<AtomicEffectEntry>(atoms),
                });
                return data;
            }
            // 局部 Atom 收敛到类级助手（引用化适配）；本段保留 value 缺省=0 语义
            AtomicEffectEntry Atom(string type, int value = 0) => CardPipelineVerifier.Atom(type, value);

            // 状态快照（本段灌满双方 bank/上限，结束恢复——不污染后续段落）
            var pool1 = core.ElementPool.GetPool(p1);
            var pool2 = core.ElementPool.GetPool(p2);
            var snap1 = new Dictionary<ManaType, int>(pool1.TempElements);
            var snap2 = new Dictionary<ManaType, int>(pool2.TempElements);
            int idx1 = pool1.GlobalTurnIndex, idx2 = pool2.GlobalTurnIndex;
            pool1.GlobalTurnIndex = 9;   // 费用门槛（=地牌槽上限）不挡
            pool2.GlobalTurnIndex = 9;
            foreach (var t in AllManaTypes()) { pool1.TempElements[t] = 99; pool2.TempElements[t] = 99; }

            try
            {
                // ---- 1. 发动无效（两层无效定案：净零成本）：不付费、无效果 ----
                var fireball = InjectCard(core, p1, SpellData("VERIFY_CAST_FB", "验证火球", 0, Atom("DealDamage", 4)));
                var knock = InjectCard(core, p2, SpellData("VERIFY_CAST_KD", "验证发动无效", 2, Atom("NegateActivation")));

                int p2Life = p2.Life;
                int gray1 = pool1.TempElements[ManaType.Gray];

                Assert(GameActions.PlayCard(core, p1, fireball, new List<Entity> { p2 }),
                       "打出火球（使用时点声明上栈）—门禁:" + PlayGateProbe(core, p1, fireball));
                Assert(core.StackEngine.StackSize == 1 && core.StackEngine.CurrentPriorityHolder == p2,
                       "cast 上栈且对手持有优先权（响应窗口开启）");
                Assert(pool1.TempElements[ManaType.Gray] == gray1, "声明期不付费（Option Y）");

                Assert(GameActions.PlayCardInResponse(core, p2, knock, new List<Entity> { fireball }),
                       "响应窗口内打出发动无效（指向发动区的火球）");
                Assert(core.ZoneManager.IsCardInZone(fireball, p1, Zone.Activation),
                       "发动无效仅入栈未结算：火球仍留发动区");

                GameActions.DrainStack(core);   // 双 Pass → LIFO：发动无效先、火球 cast 后

                // 已知残留（2026-09-21 数据扩池后漂移）：段内会产生一个空目标的速度1 SBA 窗口
                // （DrainStack 双 Pass 排不掉，疑似记帐型 SBA 入栈后窗口未闭合——待夹具二期深挖）。
                // 收口排空防污染下段（同 Equipment/HeroSkills 段尾惯例）。
                GameActions.DrainStack(core);
                if (!core.StackEngine.IsEmpty)
                    core.StackEngine.Clear();
                Assert(core.StackEngine.IsEmpty, "栈已排干");
                Assert(core.ZoneManager.GetCards(p1, Zone.Graveyard).Contains(fireball), "火球被送墓（发动层无效达成）");
                Assert(p2.Life == p2Life, "被中止的 cast 不结算效果（伤害 0）");
                Assert(pool1.TempElements[ManaType.Gray] == gray1, "被中止的 cast 不付费（净效果=扣费返还）");
                Assert(core.ZoneManager.GetCards(p2, Zone.Graveyard).Contains(knock), "发动无效牌结算后入墓");

                // ---- 2. 效果无效（两层无效定案：扣费不返还）：付费但跳效果 ----
                var fireball2 = InjectCard(core, p1, SpellData("VERIFY_CAST_FB2", "验证火球二", 0, Atom("DealDamage", 4)));
                var negate = InjectCard(core, p2, SpellData("VERIFY_CAST_NA", "验证效果无效", 2, Atom("NegateEffect")));

                int gray1b = pool1.TempElements[ManaType.Gray];

                Assert(GameActions.PlayCard(core, p1, fireball2, new List<Entity> { p2 }), "打出第二张火球");
                Assert(GameActions.PlayCardInResponse(core, p2, negate, new List<Entity> { fireball2 }),
                       "响应窗口内打出效果无效");
                GameActions.DrainStack(core);
                if (!core.StackEngine.IsEmpty) core.StackEngine.Clear(); // 段内 SBA 窗口收口（同上小节）

                Assert(p2.Life == p2Life, "被无效的 cast 跳过效果（伤害 0）");
                Assert(pool1.TempElements[ManaType.Gray] == gray1b - 1,
                       "无效路径照常付费（窗口后扣费，费用不退）");
                Assert(core.ZoneManager.GetCards(p1, Zone.Graveyard).Contains(fireball2), "被无效的卡入墓");

                // ---- 3. 声明超发保护：同笔 bank 不重复承诺 ----
                var a = InjectCard(core, p1, SpellData("VERIFY_CAST_A", "验证超发A", 0, Atom("DealDamage", 1)));
                var b = InjectCard(core, p1, SpellData("VERIFY_CAST_B", "验证超发B", 0, Atom("DealDamage", 1)));
                // 账单=卡面灰1+效果锚价（2026-10-15 施放账单单源）——bank 精确灌一张的量，
                // 混付垫付（灰←黑白三色）会绕过「精确占用即拒」，故清全色只留账单本身（根因 A 修复）
                var billA = CardCore.ElementPaymentValidator.NormalizeBill(GameActions.GetCardCost(a, 0));
                foreach (var t in AllManaTypes()) pool1.TempElements[t] = 0;
                foreach (var kv in billA) pool1.TempElements[kv.Key] = kv.Value; // 只够一张
                Assert(GameActions.PlayCard(core, p1, a, new List<Entity> { p2 }), "第一张可声明（占用承诺——bank 恰好单源账单）");
                Assert(!GameActions.PlayCard(core, p1, b, new List<Entity> { p2 }),
                       "同一笔 bank 已被在栈 cast 占用：第二张拒绝（防超发）");
                GameActions.DrainStack(core);
                if (!core.StackEngine.IsEmpty) core.StackEngine.Clear(); // 段内 SBA 窗口收口（同上小节）
                Assert(AllManaTypes().All(t => pool1.TempElements[t] == 0), "第一张结算后照常扣费（账单扣空）");
                Assert(p2.Life == p2Life - 1, "第一张效果照常结算（伤害 1）");

                // ---- 4. 跨边代价断言已删（2026-09-10：OpponentDraw/OpponentHeal 机制被 Polarity 错边折价顶替）----
            }
            finally
            {
                pool1.GlobalTurnIndex = idx1;
                pool2.GlobalTurnIndex = idx2;
                pool1.TempElements.Clear();
                foreach (var kv in snap1) pool1.TempElements[kv.Key] = kv.Value;
                pool2.TempElements.Clear();
                foreach (var kv in snap2) pool2.TempElements[kv.Key] = kv.Value;
            }
        }

        // ======================================== 死亡原子（牺牲/消灭/湮灭） ========================================

        /// <summary>
        /// 死亡原子端到端（DeathRules 死因的原子层落地；合成随从驱动，不依赖卡表）：
        /// - 牺牲（2026-10-09 舍弃并档定案）：持有者自选单位直送墓——非效果死亡，不发 CardDestroyEvent；
        /// - 消灭（2026-10-09 吞噬机制移除）：纯效果死亡入墓，无回复无吸收——不灭拦（消灭类）；
        /// - 抹除（2026-10-13 湮灭退役承接）：非死亡直接消失——不走换区、无 CardDestroyEvent，复生不可救；不灭不拦。
        /// </summary>
        private static void TestDeathAtoms(GameCore core, Player p1, Player p2)
        {
            var used = new List<Card>();

            Card Make(Player owner, int power, int life, params string[] keywords)
            {
                var data = new CardData { ID = "VERIFY_DEATH_" + used.Count, CardName = "死" + used.Count };
                data.Supertype = Cardtype.Creature;
                data.Power = power;
                data.Life = life;
                foreach (var kw in keywords) data.Keywords.Add(kw);
                var card = new CardWrapper(data);
                card.SetController(owner);
                core.ZoneManager.TryAddToBattlefield(card, owner);
                card.Untap();
                used.Add(card);
                return card;
            }
            void Clean()
            {
                foreach (var c in used)
                {
                    var owner = c.GetController() ?? p1;
                    core.ZoneManager.GetZoneContainer(owner).Remove(c, Zone.Battlefield);
                    core.ZoneManager.GetZoneContainer(owner).Remove(c, Zone.Graveyard);
                    core.ZoneManager.GetZoneContainer(owner).Remove(c, Zone.Exile);
                }
            }

            // 原子直发（直连执行器，跳过元素费；与 TestCreature 击杀路径同法）
            // 2026-09-13：target 放宽为 Entity——牺牲/摒弃等 edict 原子的作用对象是角色（Player）
            void Atom(AtomicEffectType type, Player controller, Entity source, Entity target, int value = 0)
            {
                var def = new EffectDefinition
                {
                    Id = "VERIFY_DEATH_" + type,
                    TriggerTiming = TriggerTiming.Activate_Active,
                    Effects = new List<AtomicEffectInstance> { new AtomicEffectInstance { Type = type, Value = value } },
                };
                core.StackEngine.GetExecutor().ExecuteAsync(new EffectInstance
                {
                    Definition = def,
                    Source = source,
                    Controller = controller,
                    Targets = new List<Entity> { target },
                }, skipElementCost: true).Forget();
            }

            try
            {
                // ---- 牺牲（2026-10-09 舍弃并档定案）：直送墓·非死亡（不发 CardDestroyEvent=死亡时点不触发）----
                var victim = Make(p1, 2, 3);
                int destroyEvents = 0;
                void OnDeathAtomDestroy(CardDestroyEvent e) => destroyEvents++;
                EventManager.Instance.Subscribe<CardDestroyEvent>(OnDeathAtomDestroy);
                Atom(AtomicEffectType.Sacrifice, p1, p1, p1);
                EventManager.Instance.Unsubscribe<CardDestroyEvent>(OnDeathAtomDestroy);
                Assert(!victim.IsAlive && core.ZoneManager.GetCards(p1, Zone.Graveyard).Contains(victim),
                       "牺牲：持有者自选单位直送墓（非死亡定案）");
                Assert(destroyEvents == 0,
                       "牺牲非死亡：不发 CardDestroyEvent（OnDeath/亡语/消灭替代/统计全不触发）");

                // ---- 消灭（2026-10-09 吞噬机制移除）：纯效果死亡，无回复无吸收 ----
                var slayer = Make(p1, 3, 6);
                Atom(AtomicEffectType.DealDamage, p1, p1, slayer, 4); // 先受伤至 2——验证消灭不再回复
                Assert(slayer.GetLife() == 2, "消灭段：施法单位先受伤至 2（回复缺口）");

                var prey = Make(p2, 1, 3, CardCore.Attribute.KeywordRules.Taunt);
                int killEvents = 0;
                void OnKillDestroy(CardDestroyEvent e) => killEvents++;
                EventManager.Instance.Subscribe<CardDestroyEvent>(OnKillDestroy);
                Atom(AtomicEffectType.Devour, p1, slayer, prey); // 消灭（原吞噬行改名，EffectType 沿用）
                EventManager.Instance.Unsubscribe<CardDestroyEvent>(OnKillDestroy);
                Assert(!prey.IsAlive && core.ZoneManager.GetCards(p2, Zone.Graveyard).Contains(prey),
                       "消灭：目标生物经 DestroyEffect 死因入墓");
                Assert(slayer.GetLife() == 2, "消灭无吞噬：不按目标生命回复（机制已删）");
                Assert(killEvents == 1, "消灭=效果死亡：恰发一次 CardDestroyEvent（与牺牲直送相对）");

                // 不灭拦消灭（消灭类死因）：拦下即无事发生
                var rock = Make(p2, 0, 5, CardCore.Attribute.KeywordRules.Indestructible,
                                CardCore.Attribute.CounterRules.StealthCounter);
                Atom(AtomicEffectType.Devour, p1, p1, rock);
                Assert(rock.IsAlive && core.ZoneManager.GetCards(p2, Zone.Battlefield).Contains(rock),
                       "不灭拦下消灭（消灭类死因）");

                // ---- 抹除（2026-10-13 湮灭残壳清理：Annihilate 行/handler 已删，Erase 承接语义——
                //      非死亡直接消失：不走换区、无 CardDestroyEvent，复生不可救、不灭不拦）----
                var phoenix = Make(p2, 2, 2, CardCore.Attribute.CounterRules.RebornCounter);
                int eraseDestroys = 0;
                void OnEraseDestroy(CardDestroyEvent e) => eraseDestroys++;
                EventManager.Instance.Subscribe<CardDestroyEvent>(OnEraseDestroy);
                Atom(AtomicEffectType.Erase, p1, p1, phoenix);
                EventManager.Instance.Unsubscribe<CardDestroyEvent>(OnEraseDestroy);
                Assert(!phoenix.IsAlive
                       && !core.ZoneManager.GetCards(p2, Zone.Battlefield).Contains(phoenix)
                       && !core.ZoneManager.GetCards(p2, Zone.Graveyard).Contains(phoenix)
                       && !core.ZoneManager.GetCards(p2, Zone.Exile).Contains(phoenix),
                       "抹除：非死亡直接消失（不入墓不除外——从本局移除）");
                Assert(!core.ZoneManager.GetCards(p2, Zone.Battlefield).Contains(phoenix) && eraseDestroys == 0,
                       "抹除：复生不可救（未回场）、不发 CardDestroyEvent（非死亡路径）");

                var adamant = Make(p2, 1, 4, CardCore.Attribute.KeywordRules.Indestructible);
                Atom(AtomicEffectType.Erase, p1, p1, adamant);
                Assert(!adamant.IsAlive && !core.ZoneManager.GetCards(p2, Zone.Battlefield).Contains(adamant),
                       "抹除：不灭不拦（非消灭类，照常抹除）");
            }
            finally
            {
                Clean();
            }
        }

        private static void TestCreature(GameCore core, Player p1, List<CardData> cardsData)
        {
            var data = cardsData.FirstOrDefault(c => c.CardName == "古树守卫");
            Assert(data != null, "古树守卫配置存在");
            if (data == null) return;

            var creature = new CardWrapper(data);
            creature.SetController(p1);
            core.ZoneManager.GetZoneContainer(p1).Add(creature, Zone.Hand);
            var pool = core.ElementPool.GetPool(p1);
            // 新费用（身材灰 3+5=8 点 = 灰 4）：灰 4 = 档位 4（2026-09-13 测试卡组生物统一 1/1 观测口径——
            // 声明费不变，身材降档只降 D，D≤C 照旧成立）
            pool.TempElements[ManaType.Gray] = 4;

            bool played = PlayCardSync(core, p1, creature);
            Assert(played, "古树守卫成功打出");
            Assert(core.ZoneManager.GetCards(p1, Zone.Battlefield).Contains(creature),
                   "生物进入战场");
            // 配置驱动血量：CardWrapper 同步 _life 前此处恒为 1
            Assert(creature.GetLife() == 1, $"血量来自配置（应为1，实际{creature.GetLife()}）");

            // 伤害效果击杀
            var killDef = new EffectDefinition
            {
                Id = "VERIFY_KILL",
                TriggerTiming = TriggerTiming.Activate_Active,
                Effects = new List<AtomicEffectInstance>
                {
                    new AtomicEffectInstance { Type = AtomicEffectType.DealDamage, Value = 5 }
                }
            };
            var kill = new EffectInstance
            {
                Definition = killDef,
                Source = creature,
                Controller = p1,
                Targets = new List<Entity> { creature },
            };
            // 直连执行器（跳过元素费：合成效果的计价与击杀语义无关）
            core.StackEngine.GetExecutor().ExecuteAsync(kill, skipElementCost: true).Forget();

            Assert(creature.GetLife() <= 0, $"受伤后血量≤0（实际{creature.GetLife()}）");
            Assert(!creature.IsAlive, "生物被伤害效果击杀");
        }

        // ======================================== 棋盘层（Phase A） ========================================

        /// <summary>
        /// 棋盘选择/表现层验证。架构铁律：棋盘 = 核心状态的确定性纯函数
        /// （核心不与棋盘绑定，重连/传输只同步 CardCore，Resync 即重建占用）。
        /// 全程手动 Resync，不启用事件自动刷新。
        /// </summary>
        /// <summary>
        /// 来源归因（三轨制定案 2026-09-09 规则①②）：所有魔法卡的效果来源=角色（Player）——
        /// 法术伤害致死归因到施法玩家（不再是法术卡）；法术伤害不触发吸血（来源非生物）。
        /// 规则①（生物效果来源=该生物）由 TestKeywords 段 18f 指示物轨锚锁定。
        /// </summary>
        private static void TestAttribution(GameCore core, Player p1, Player p2)
        {
            EnsureMainPhase(core, p1);

            // 归因靶：p2 场上 1/2 生物（受 3 伤必死）
            var victimData = new CardData { ID = "VERIFY_ATTRIB_VICTIM", CardName = "归因靶" };
            victimData.Supertype = Cardtype.Creature;
            victimData.Power = 1;
            victimData.Life = 2;
            var victim = new CardWrapper(victimData);
            victim.SetController(p2);
            core.ZoneManager.TryAddToBattlefield(victim, p2);

            // 法术：灰 0 费 DealDamage 3
            var boltData = new CardData { ID = "VERIFY_ATTRIB_SPELL", CardName = "归因法术" };
            boltData.Supertype = Cardtype.Spell;
            boltData.Cost[(int)ManaType.Gray] = 0;
            var boltEffect = new CardEffectData { Id = "VERIFY_ATTRIB_EFF", TriggerTiming = (int)TriggerTiming.OnPlay };
            boltEffect.AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(CardCore.AtomicEffectType.DealDamage, value: 3) };
            boltData.Effects.Add(boltEffect);
            var bolt = InjectCard(core, p1, boltData);

            CardDestroyEvent killEvt = null;
            void OnAttribKill(CardDestroyEvent e) { if (e.DestroyedCard == victim) killEvt = e; }
            EventManager.Instance.Subscribe<CardDestroyEvent>(OnAttribKill);
            int p1LifeBefore = p1.Life;
            try
            {
                Assert(PlayCardSync(core, p1, bolt, new List<Entity> { victim }), "归因法术：成功施放并结算");
                core.SBAEngine.CheckAndExecute();
                Assert(killEvt != null && ReferenceEquals(killEvt.Source, p1) && !(killEvt.Source is Card),
                       "来源归因②：法术伤害致死归因到施法玩家（Source=角色，非法术卡）");
                Assert(!victim.IsAlive && core.ZoneManager.GetCards(p2, Zone.Graveyard).Contains(victim),
                       "来源归因②：目标死亡入墓（伤害链完整）");
                Assert(p1.Life == p1LifeBefore, "法术伤害不触发吸血/系命（来源非生物无从回复）");
            }
            finally
            {
                EventManager.Instance.Unsubscribe<CardDestroyEvent>(OnAttribKill);
            }
        }

        /// <summary>
        /// 连接光环运行时（三轨制·光环轨 2026-09-09）：方向映射双射 + 单向指向 + 受益者豁免干扰 +
        /// 无效压制来源（唯一能压光环的口）+ 断链失效（live-query 无物化）+ 对手视角镜像 +
        /// 生命光环（伤害先吃光环/断链回落 SBA 收尸）。自建 BoardState（合成卡）；结束归零。
        /// </summary>
        private static void TestLinkAura(GameCore core, Player p1, Player p2, List<CardData> cardsData)
        {
            // ---- 1. 方向映射定死：六对六双射（互逆）+ Opposite 180° 对向 ----
            Assert(GameBoard.BoardMath.MapArrow(CardCore.HexDirection.Up) == GameBoard.BoardDirection.NE
                   && GameBoard.BoardMath.MapArrow(CardCore.HexDirection.UpperRight) == GameBoard.BoardDirection.E
                   && GameBoard.BoardMath.MapArrow(CardCore.HexDirection.LowerRight) == GameBoard.BoardDirection.SE
                   && GameBoard.BoardMath.MapArrow(CardCore.HexDirection.Down) == GameBoard.BoardDirection.SW
                   && GameBoard.BoardMath.MapArrow(CardCore.HexDirection.LowerLeft) == GameBoard.BoardDirection.W
                   && GameBoard.BoardMath.MapArrow(CardCore.HexDirection.UpperLeft) == GameBoard.BoardDirection.NW
                   && GameBoard.BoardMath.ArrowOf(GameBoard.BoardDirection.NE) == CardCore.HexDirection.Up,
                   "链接箭头映射：六对六双射（MapArrow/ArrowOf 互逆）");
            Assert(GameBoard.BoardMath.Opposite(GameBoard.BoardDirection.NE) == GameBoard.BoardDirection.SW
                   && GameBoard.BoardMath.Opposite(GameBoard.BoardDirection.E) == GameBoard.BoardDirection.W
                   && GameBoard.BoardMath.Opposite(GameBoard.BoardDirection.SE) == GameBoard.BoardDirection.NW,
                   "Opposite：180° 对向（对手视角镜像基础）");

            GameBoard.LinkAuraSystem.Detach(); // 隔离上局残留（静态扩展点惯例）
            var board = new GameBoard.BoardState(core, p1, p2,
                GameBoard.HalfFieldData.Flat(), GameBoard.HalfFieldData.Flat());
            board.EnableAutoResync();
            GameBoard.LinkAuraSystem.Attach(board);

            // 清空双方战场残留（前面各段的测试卡已断言完毕；直删不发事件，随后手动 Resync）
            foreach (var pl in new[] { p1, p2 })
            {
                var leftover = core.ZoneManager.GetCards(pl, Zone.Battlefield).ToList();
                foreach (var c in leftover)
                    core.ZoneManager.GetZoneContainer(pl).Remove(c, Zone.Battlefield);
            }
            board.Resync();

            var used = new List<Card>();
            Card MakePlain(Player owner, int power, int life)
            {
                var data = new CardData { ID = "VERIFY_LA_" + used.Count, CardName = "链" + used.Count };
                data.Supertype = Cardtype.Creature;
                data.Power = power;
                data.Life = life;
                var card = new CardWrapper(data);
                card.SetController(owner);
                core.ZoneManager.TryAddToBattlefield(card, owner);
                used.Add(card);
                return card;
            }
            CardData DataOf(Card c) => (c as CardWrapper).GetData();
            BoardDirection DirBetween(Card from, Card to, out bool adjacent)
            {
                adjacent = false;
                board.TryGetCell(from, out int fx, out int fz);
                board.TryGetCell(to, out int tx, out int tz);
                foreach (GameBoard.BoardDirection d in System.Enum.GetValues(typeof(GameBoard.BoardDirection)))
                {
                    var (nx, nz) = GameBoard.BoardMath.Neighbor(fx, fz, d);
                    if (nx == tx && nz == tz) { adjacent = true; return d; }
                }
                return default;
            }

            try
            {
                // ---- 2. 单向指向：箭头指向格的占据者享受（攻/关键词）；来源自身不吃 ----
                var ben = MakePlain(p1, 3, 5);
                var src = MakePlain(p1, 2, 5);
                var dirTo = DirBetween(src, ben, out bool adjacent);
                Assert(adjacent, "光环：落位相邻前提（first-free 顺序相邻）");
                DataOf(src).LinkAuras.Add(new LinkAuraData { stat = "Power", value = 2 });
                DataOf(src).LinkAuras.Add(new LinkAuraData { keyword = "Taunt" });
                DataOf(src).ArrowDirections = GameBoard.BoardMath.ArrowOf(dirTo);
                GameBoard.LinkAuraSystem.InvalidateCache();
                Assert(ben.GetPower() == 5 && ben.HasKeyword("Taunt"),
                       "连接光环：箭头指向格的占据者攻+2 且视为有嘲讽（单向指向）");
                Assert(ben.GetKeywordCount("Taunt") == 0,
                       "光环关键词不物化：GetKeywordCount 仍 0（融合叠加计数只归真授予）");
                Assert(src.GetPower() == 2 && !src.HasKeyword("Taunt"),
                       "箭头不指向自己：来源自身无加成");
                Assert(core.LayerEngine.CalculatePower(ben) == 5,
                       "战斗读数可见：LayerEngine.CalculatePower 基值含光环（战斗/SBA 同源）");

                // ---- 3. 受益者豁免：净化/沉默/无效打在受益者身上，光环不变 ----
                CardCore.Attribute.KeywordRules.PurifyKeywords(ben);
                ben.AddCounters(CardCore.Attribute.CounterRules.SilenceCounter, 1);
                ben.AddCounters(CardCore.Attribute.CounterRules.NullifyCounter, 1);
                GameBoard.LinkAuraSystem.InvalidateCache();
                Assert(ben.GetPower() == 5 && ben.HasKeyword("Taunt"),
                       "受益者不可被干扰：净化/沉默/无效都不压光环（只有作用于来源才有效）");
                ben.RemoveCounters(CardCore.Attribute.CounterRules.SilenceCounter, 1);
                ben.RemoveCounters(CardCore.Attribute.CounterRules.NullifyCounter, 1);

                // ---- 4. 无效压制来源（唯一能压光环的口）；净化不压箭头（箭头=卡面数据）----
                src.AddCounters(CardCore.Attribute.CounterRules.NullifyCounter, 1, p1);
                GameBoard.LinkAuraSystem.InvalidateCache();
                Assert(ben.GetPower() == 3 && !ben.HasKeyword("Taunt"),
                       "无效压制来源：光环熄灭（无效=唯一能压光环的指示物）");
                CardCore.Attribute.CounterRules.PurgeAll(src); // 净化口径清掉无效（含全部指示物）
                GameBoard.LinkAuraSystem.InvalidateCache();
                Assert(ben.GetPower() == 5 && ben.HasKeyword("Taunt"),
                       "净化来源不压箭头：无效被清后光环恢复（箭头是卡面数据，净化只清状态）");

                // ---- 4c. 属性光环负值（Power）：LayerEngine 战斗读数同减。
                //      2026-10-09 清理：Life 负值/Both（六类属性光环方案）未实施——两断言删除，待方案落地再回锚 ----
                var statBen = MakePlain(p1, 3, 5);
                var statSrc = MakePlain(p1, 2, 5);
                var sDir = DirBetween(statSrc, statBen, out bool sAdj);
                Assert(sAdj, "属性光环段：落位相邻前提");
                DataOf(statSrc).ArrowDirections = GameBoard.BoardMath.ArrowOf(sDir);
                DataOf(statSrc).LinkAuras.Add(new LinkAuraData { stat = "Power", value = -2 });
                GameBoard.LinkAuraSystem.InvalidateCache();
                Assert(core.LayerEngine.CalculatePower(statBen) == 1,
                       "攻−光环：LayerEngine.CalculatePower 基值同减（战斗/SBA 同源）");

                // ---- 4d. 坚韧指示物（2026-10-09 生效自减改版，同毒素档）：受伤每层 −1，实际拦到即生效
                //      ——生效后层数减半（floor）；值/limit 台账与次数闸已退役 ----
                var toughBen = MakePlain(p1, 3, 9);
                toughBen.AddCounters(CardCore.Attribute.CounterRules.ToughnessCounter, 2, null); // 2 层
                int tLife = toughBen.GetLife();
                Attribute.KeywordRules.ApplyDamage(null, toughBen, 5, false);
                Assert(toughBen.GetLife() == tLife - 3
                       && toughBen.GetCounterCount(CardCore.Attribute.CounterRules.ToughnessCounter) == 1,
                       "坚韧指示物：2 层 → 5 伤实扣 3，生效减半（2→1）");
                tLife = toughBen.GetLife();
                Attribute.KeywordRules.ApplyDamage(null, toughBen, 5, false);
                Assert(toughBen.GetLife() == tLife - 4
                       && toughBen.GetCounterCount(CardCore.Attribute.CounterRules.ToughnessCounter) == 0,
                       "坚韧生效自减：第二击余 1 层减 1，层清零（易损镜像；旧 value/limit 台账与次数闸已随指示物化退役）");

                // ---- 4e. 守护配对制（2026-10-08 改版）：扫场护角色+次数闸形态退役——
                //      裸持有不建配对不挡刀；登场/授予弹选才建配对（行为全量锚见 TestGuardianRelay 六段）----
                var roleGuard = MakePlain(p1, 2, 6);
                roleGuard.AddKeyword("Guardian", CardCore.KeywordLane.Setting, null, 1, 1);
                int p1LifeBefore = p1.Life;
                Attribute.KeywordRules.ApplyDamage(p2, p1, 4, false);
                Assert(p1.Life == p1LifeBefore - 4,
                       "守护改版：裸持有不挡刀（配对制——登场/授予弹选才建配对；旧扫场护角色+次数闸退役）");

                // ---- 5. 断链失效：来源离场光环消失（live-query 无物化） ----
                core.ZoneManager.MoveCard(src, p1, Zone.Battlefield, Zone.Graveyard);
                Assert(ben.GetPower() == 3 && !ben.HasKeyword("Taunt"),
                       "断链失效：来源离场光环消失");

                // ---- 6. 对手视角镜像：p2（归属1）的箭头绝对方向取 Opposite ----
                var ben2 = MakePlain(p2, 2, 4);
                var src2 = MakePlain(p2, 2, 4);
                var dir2 = DirBetween(src2, ben2, out bool adjacent2);
                Assert(adjacent2, "光环镜像：落位相邻前提");
                DataOf(src2).LinkAuras.Add(new LinkAuraData { stat = "Life", value = 2 });
                DataOf(src2).ArrowDirections = GameBoard.BoardMath.ArrowOf(dir2); // 反例：按绝对方向写
                GameBoard.LinkAuraSystem.InvalidateCache();
                Assert(ben2.GetLife() == 4 && ben2.GetMaxLife() == 4,
                       "镜像负例：对手箭头未按其视角写（直接绝对方向）不生效");
                DataOf(src2).ArrowDirections = GameBoard.BoardMath.ArrowOf(GameBoard.BoardMath.Opposite(dir2)); // 正例
                GameBoard.LinkAuraSystem.InvalidateCache();
                Assert(ben2.GetLife() == 6 && ben2.GetMaxLife() == 6,
                       "镜像正例：对手箭头按其视角写（绝对方向取 Opposite）生效——生命光环上限与当前同加");

                // ---- 7. 生命光环：伤害先吃光环；断链回落经 SBA 收尸 ----
                CardCore.Attribute.KeywordRules.ApplyDamage(p1, ben2, 5, false); // 有效 6 受 5 伤剩 1
                Assert(ben2.IsAlive && ben2.GetLife() == 1,
                       "生命光环：伤害先吃穿光环加成（有效生命 6 受 5 伤剩 1，不死）");
                core.ZoneManager.MoveCard(src2, p2, Zone.Battlefield, Zone.Graveyard); // 断链
                core.SBAEngine.CheckAndExecute();
                Assert(!ben2.IsAlive,
                       "断链回落：光环垫的生命随链消失，有效归零经 SBA 收尸");

                // ---- 8. JSON 链路：夹具卡 linkAuras/arrows 装载 + 内容哈希分叉（LA: 条件段） ----
                var fixture = cardsData.FirstOrDefault(c => c.CardName == "链接光环测试");
                Assert(fixture != null
                       && fixture.ArrowDirections == (CardCore.HexDirection.Up | CardCore.HexDirection.LowerRight)
                       && fixture.LinkAuras.Count == 2
                       && fixture.LinkAuras[0].stat == "Power" && fixture.LinkAuras[0].value == 2
                       && fixture.LinkAuras[1].keyword == "Taunt",
                       "光环 JSON 链路：arrows/linkAuras 装载还原（三轨测试夹具卡）");
                var noAuraClone = MakePlain(p1, 2, 4);
                var noAuraData = (noAuraClone as CardWrapper).GetData();
                string hashNoAura = SynergyUI.ContentHasher.HashCard(noAuraData);
                noAuraData.LinkAuras.Add(new LinkAuraData { stat = "Power", value = 2 });
                string hashWithAura = SynergyUI.ContentHasher.HashCard(noAuraData);
                Assert(hashNoAura != hashWithAura,
                       "光环内容哈希：linkAuras 影响 ID（LA: 段）");
                noAuraData.LinkAuras.Clear();
                Assert(SynergyUI.ContentHasher.HashCard(noAuraData) == hashNoAura,
                       "光环内容哈希：空表不追加 LA: 段（存量卡 ID 不漂移）");
                used.Add(noAuraClone); // 交由 finally 统一清理

                // ---- 坚韧光环已退役（2026-10-08 指示物化）：表行 MountKinds=指示物 → CanMountAsAura
                //      位 1 硬拒；关键词目录不再收录（GrantToughness 未登记 Specs，LoadKeywords 跳过）——
                //      光环/关键词面板两路均不可达，坚韧行为面改走 ToughnessCounter（上 4d 段锚定）。----
                var armorRow = CardCore.Attribute.AtomicEffectTable.GetByType(CardCore.AtomicEffectType.GrantToughness);
                Assert(armorRow != null
                       && ComposerCatalog.HasMountBit(armorRow, CardCore.MountKind.Counter)
                       && !ComposerCatalog.CanMountAsAura(armorRow),
                       "坚韧表行（2b1e3700）：指示物族（位1）→ 不可作连接光环条目");
                Assert(!CardLoader.LoadKeywords().ContainsKey("Armor"),
                       "坚韧不入关键词目录（GrantToughness 未登记 Specs——LoadKeywords 跳过未登记 Grant 行）");

                // 2026-10-09 清理：属性价梯（Duration 档 0.5/1.5/2.0/3.0）与属性光环 0.6 单回合折算断言删除——
                // Duration 轴计价已随 2026-10-05 三轨统一 + 2026-10-08 指示物四分类退役（档=CounterSpec 真实持久）
            }
            finally
            {
                foreach (var c in used)
                {
                    var owner = c.GetController() ?? p1;
                    core.ZoneManager.GetZoneContainer(owner).Remove(c, Zone.Battlefield);
                    core.ZoneManager.GetZoneContainer(owner).Remove(c, Zone.Graveyard);
                }
                GameBoard.LinkAuraSystem.Detach();
                board.Dispose();
            }
        }

        private static void TestBoard(GameCore core, Player p1, Player p2, List<CardData> cardsData)
        {
            // ---- 1. 布局静态：计数 / 语义格坐标 / 180° 对称 ----
            int edge = 0, unit = 0, land = 0, special = 0;
            for (int z = 0; z < BoardMath.Height; z++)
                for (int x = 0; x < BoardMath.Width; x++)
                {
                    switch (BoardLayout.RoleOf(x, z))
                    {
                        case CellRole.Edge: edge++; break;
                        case CellRole.Unit: unit++; break;
                        case CellRole.Land: land++; break;
                        default: special++; break;
                    }
                }
            Assert(edge == 5 && unit == 24 && land == 12 && special == 15,
                   $"布局计数 56 = 边缘{edge}（中线缺口 + 原发动格位×2 与原技能格位×2 回落） + 单位{unit} + 地牌{land} + 特殊{special}（含中线行7；发动格拆除 2026-10-15、技能格撤销 2026-10-16）");

            // 特殊格（2026-10-14 晚镜像修正后口径：画面左=x0 列、底边=z0 贴玩家——
            // P1 角色落左列 (0,1)，除外/墓地/卡组落右列 x7；P2 = Rotate180 逐一对应。
            // 发动格 2026-10-15 拆除、技能格 2026-10-16 撤销：四格位回落边缘装饰格=Edge·归属-1）
            Assert(BoardLayout.RoleOf(7, 4) == CellRole.Edge && BoardLayout.OwnerOf(7, 4) == -1, "原发动格 (7,4) 拆除后回落边缘装饰格（2026-10-15）");
            Assert(BoardLayout.RoleOf(0, 1) == CellRole.Character && BoardLayout.OwnerOf(0, 1) == 0, "P1 角色格 = (0,1)（镜像修正：左列中间）");
            Assert(BoardLayout.RoleOf(7, 6) == CellRole.Edge && BoardLayout.OwnerOf(7, 6) == -1, "原技能格 (7,6) 撤销后回落边缘装饰格（2026-10-16）");
            Assert(BoardLayout.RoleOf(7, 2) == CellRole.Exile && BoardLayout.OwnerOf(7, 2) == 0, "P1 除外格 = (7,2)");
            Assert(BoardLayout.RoleOf(7, 1) == CellRole.Graveyard && BoardLayout.OwnerOf(7, 1) == 0, "P1 墓地格 = (7,1)");
            Assert(BoardLayout.RoleOf(7, 0) == CellRole.Deck && BoardLayout.OwnerOf(7, 0) == 0, "P1 卡组格 = (7,0)");
            Assert(BoardLayout.RoleOf(0, 2) == CellRole.Edge && BoardLayout.OwnerOf(0, 2) == -1, "原发动格 (0,2) 拆除后回落边缘装饰格（2026-10-15）");
            Assert(BoardLayout.RoleOf(7, 5) == CellRole.Character && BoardLayout.OwnerOf(7, 5) == 1, "P2 角色格 = (7,5)");
            Assert(BoardLayout.RoleOf(0, 0) == CellRole.Edge && BoardLayout.OwnerOf(0, 0) == -1, "原技能格 (0,0) 撤销后回落边缘装饰格（2026-10-16）");
            Assert(BoardLayout.RoleOf(0, 4) == CellRole.Exile && BoardLayout.OwnerOf(0, 4) == 1, "P2 除外格 = (0,4)");
            Assert(BoardLayout.RoleOf(0, 5) == CellRole.Graveyard && BoardLayout.OwnerOf(0, 5) == 1, "P2 墓地格 = (0,5)");
            Assert(BoardLayout.RoleOf(0, 6) == CellRole.Deck && BoardLayout.OwnerOf(0, 6) == 1, "P2 卡组格 = (0,6)");
            bool middleOk = true;
            for (int x = 0; x < BoardMath.Width; x++)
            {
                var expect = x < BoardLayout.MiddleCellCount ? CellRole.Middle : CellRole.Edge;
                if (BoardLayout.RoleOf(x, 3) != expect || BoardLayout.OwnerOf(x, 3) != -1) middleOk = false;
            }
            Assert(middleOk, "中线行 z3 = 7 格 Middle（x0..x6）+ x7 缺口 Edge·归属-1（2026-10-14 状态行）");

            bool symmetric = true;
            for (int z = 0; z < BoardMath.Height && symmetric; z++)
                for (int x = 0; x < BoardMath.Width && symmetric; x++)
                {
                    if (z == BoardLayout.MiddleZ) continue; // 中线状态行 7 格居中缺口=刻意不对称（表现层，玩法归属恒 -1）
                    var (rx, rz) = BoardLayout.Rotate180(x, z);
                    if (BoardLayout.RoleOf(rx, rz) != BoardLayout.RoleOf(x, z)) symmetric = false;
                    int o = BoardLayout.OwnerOf(x, z);
                    if (o != -1 && BoardLayout.OwnerOf(rx, rz) != 1 - o) symmetric = false;
                }
            Assert(symmetric, "全盘 180° 旋转对称（中线行豁免；角色一致、归属互补）");
            Assert(BoardLayout.UnitCellsPerPlayer == ZoneManager.BattlefieldCapacityPerPlayer,
                   "单位格数 12 = 核心战场容量 12（计数与格子天然一致）");
            Assert(BoardLayout.UnitCellsPerPlayer == (BoardLayout.ColMax - BoardLayout.ColMin + 1) * 2
                   && BoardLayout.LandCellsPerPlayer == BoardLayout.ColMax - BoardLayout.ColMin + 1,
                   "容量与几何一致（单位 2×6、地牌 6 格=可用列 6——一元素一格，配置漂移即红）");

            // ---- 2. 六边形数学（手算向量表，odd-r 奇偶约定） ----
            Assert(BoardMath.Neighbor(2, 4, BoardDirection.NE) == (2, 5), "偶行 NE 邻居 = (x, z+1)");
            Assert(BoardMath.Neighbor(2, 5, BoardDirection.NE) == (3, 6), "奇行 NE 邻居 = (x+1, z+1)");
            Assert(BoardMath.Neighbor(2, 5, BoardDirection.SW) == (2, 4), "SW 与 NE 互逆（odd-r 邻接回路）");
            Assert(BoardMath.HexDistance(2, 4, 3, 4) == 1, "同行相邻格距离 1");
            Assert(BoardMath.HexDistance(2, 4, 2, 5) == 1, "跨行邻接距离 1");
            Assert(BoardMath.HexDistance(2, 4, 4, 4) == 2, "同行隔一格距离 2（默认射程边界）");
            Assert(BoardMath.HexDistance(2, 4, 5, 4) == 3, "同行隔两格距离 3（默认射程外）");

            // ---- 3. 距离档位（战棋预留公式）与半场拼合 ----
            var half1 = new HalfFieldData();
            half1.Set(5, 0, 2, 0); // 本地(5,0) → 整场 (5,4)（P1 近中级行），落差 2；中线行 z3 恒 0
            var board = new BoardState(core, p1, p2, half1, HalfFieldData.Flat());
            Assert(board.Field.ElevationAt(5, 4) == 2 && board.Field.ElevationAt(5, 3) == 0,
                   "半场地形拼合（落差写入正确格，中线行恒 0）");
            board.Mode = BoardRulesMode.Flat;
            Assert(board.AttackDistance(5, 4, 5, 5) == 1, "Flat 档忽略落差（P2P 完全平面）");
            board.Mode = BoardRulesMode.Tactical;
            Assert(board.AttackDistance(5, 4, 5, 5) == 3, "Tactical 档落差参与距离（六距1 + |2−0| = 3）");
            board.Mode = BoardRulesMode.Flat;

            // ---- 4. 确定性重建（重连语义）+ 占用与区域一致 ----
            board.Resync();
            Assert(board.IsConsistent(), "占用双向索引成对一致");
            string snap1 = BoardSnapshot(board);
            board.Resync();
            Assert(snap1 == BoardSnapshot(board), "Resync 幂等（同状态两次重建逐格一致 = 重连零成本）");

            foreach (var c in core.ZoneManager.GetCards(p1, Zone.Battlefield))
            {
                bool onCell = board.TryGetCell(c, out int bx, out int bz)
                              && BoardLayout.RoleOf(bx, bz) == CellRole.Unit && BoardLayout.OwnerOf(bx, bz) == 0;
                Assert(onCell, $"战场卡落在 P1 单位区格（{c.ID}）");
            }
            foreach (var landCard in core.ElementPool.GetPooledCards(p1))
            {
                bool onCell = board.TryGetCell(landCard.SourceCard, out int lx, out int lz)
                              && BoardLayout.RoleOf(lx, lz) == CellRole.Land && BoardLayout.OwnerOf(lx, lz) == 0;
                Assert(onCell, $"地牌落在 P1 地牌行格（{landCard.SourceCard.ID}）");
            }

            // ---- 5. 发动区流转：事件序 / 暂态不跨调用 / 终态 ----
            var pool1 = core.ElementPool.GetPool(p1);
            FillSixColors(pool1, 99);
            var creatureData = cardsData.FirstOrDefault(c => c.CardName == "古树守卫");
            if (creatureData != null)
            {
                var creature = new CardWrapper(creatureData);
                creature.SetController(p1);
                core.ZoneManager.GetZoneContainer(p1).Add(creature, Zone.Hand);

                var order = new List<string>();
                System.Action<CardEnterActivationEvent> onEnter = _ => order.Add("Enter");
                System.Action<CardPlayEvent> onPlay = _ => order.Add("Play");
                System.Action<CardLeaveActivationEvent> onLeave = _ => order.Add("Leave");
                EventManager.Instance.Subscribe(onEnter);
                EventManager.Instance.Subscribe(onPlay);
                EventManager.Instance.Subscribe(onLeave);
                try
                {
                    Assert(PlayCardSync(core, p1, creature), "棋盘段生物成功打出");
                }
                finally
                {
                    EventManager.Instance.Unsubscribe(onEnter);
                    EventManager.Instance.Unsubscribe(onPlay);
                    EventManager.Instance.Unsubscribe(onLeave);
                }

                Assert(order.SequenceEqual(new[] { "Enter", "Play", "Leave" }),
                       $"发动区流转事件序 Enter→Play→Leave（实际 {string.Join("→", order)}）");
                Assert(core.ZoneManager.GetCards(p1, Zone.Activation).Count == 0, "调用结束后发动区为空（暂态不跨调用）");
                Assert(core.ZoneManager.GetCards(p1, Zone.Battlefield).Contains(creature), "永久物终态在战场");
                Assert(creature.GetZone() == Zone.Battlefield, "_zone 随容器维护（GetZone 正确）");

                board.Resync();
                Assert(board.TryGetCell(creature, out int cx, out int cz)
                       && BoardLayout.RoleOf(cx, cz) == CellRole.Unit,
                       "Resync 后新入场卡获得单位格");
            }

            var spellData = cardsData.FirstOrDefault(c => c.CardName == "火球术");
            if (spellData != null)
            {
                var spell = new CardWrapper(spellData);
                spell.SetController(p1);
                core.ZoneManager.GetZoneContainer(p1).Add(spell, Zone.Hand);
                Assert(PlayCardSync(core, p1, spell), "棋盘段法术成功打出");
                Assert(core.ZoneManager.GetCards(p1, Zone.Graveyard).Contains(spell), "法术终态入墓地");
                Assert(spell.GetZone() == Zone.Graveyard, "法术 GetZone == Graveyard");
                Assert(core.ZoneManager.GetCards(p1, Zone.Activation).Count == 0, "法术结算后发动区为空");
            }

            // ---- 6. _zone 维护回归（不再双挂） ----
            var handNow = core.ZoneManager.GetCards(p1, Zone.Hand);
            Assert(handNow.All(c => c.GetZone() == Zone.Hand), "_zone 修复：手牌卡 GetZone==Hand");
            Assert(core.ZoneManager.GetCards(p1, Zone.Graveyard).All(c => c.GetZone() == Zone.Graveyard),
                   "_zone 修复：墓地卡 GetZone==Graveyard");
            if (handNow.Count > 0)
            {
                var mover = handNow[0];
                core.ZoneManager.MoveCard(mover, p1, Zone.Hand, Zone.Exile);
                Assert(core.ZoneManager.GetCards(p1, Zone.Hand).Count == handNow.Count - 1,
                       "_zone 修复：移出后源区计数 −1（不再双挂旧列表）");
                Assert(mover.GetZone() == Zone.Exile, "_zone 修复：除外后 GetZone==Exile");
            }

            // ---- 7. 容量闸门：手动预检拒绝 / 结算时入场失败入墓 ----
            var container1 = core.ZoneManager.GetZoneContainer(p1);
            var fillers = new List<Card>();
            int fill = ZoneManager.BattlefieldCapacityPerPlayer - container1.GetCount(Zone.Battlefield);
            for (int i = 0; i < fill; i++)
            {
                var filler = new Card { ID = "VERIFY_FILLER" };
                filler.SetController(p1);
                container1.Add(filler, Zone.Battlefield);
                fillers.Add(filler);
            }
            Assert(!core.ZoneManager.HasBattlefieldSpace(p1), "战场填满 14 后无空位");

            if (creatureData != null)
            {
                var blocked = new CardWrapper(creatureData);
                blocked.SetController(p1);
                container1.Add(blocked, Zone.Hand);
                int bankBefore = pool1.TempElements.Values.Sum();
                Assert(!PlayCardSync(core, p1, blocked), "满场手动出牌被拒（MD 式预检）");
                Assert(pool1.TempElements.Values.Sum() == bankBefore, "被拒时费用未扣");
                Assert(core.ZoneManager.GetCards(p1, Zone.Hand).Contains(blocked), "被拒时卡留在手牌");
            }

            bool failedFired = false;
            System.Action<CardActivationFailedEvent> onFail = _ => failedFired = true;
            EventManager.Instance.Subscribe(onFail);
            var tokenCard = new Card { ID = "VERIFY_TOKEN" };
            try
            {
                Assert(!core.ZoneManager.TryAddToBattlefield(tokenCard, p1), "满场时结算入场（衍生物）失败");
            }
            finally
            {
                EventManager.Instance.Unsubscribe(onFail);
            }
            Assert(core.ZoneManager.GetCards(p1, Zone.Graveyard).Contains(tokenCard), "入场失败 → 进墓地");
            Assert(failedFired, "入场失败事件已发布");

            // ---- 8. 离场清理（派生层自动释放） ----
            var victim = core.ZoneManager.GetCards(p1, Zone.Battlefield).FirstOrDefault();
            if (victim != null)
            {
                core.ZoneManager.MoveCard(victim, p1, Zone.Battlefield, Zone.Graveyard);
                board.Resync();
                Assert(!board.TryGetCell(victim, out _, out _), "离场卡不再占用格子");
            }

            // 清理填充物（保证重复运行稳定）
            foreach (var f in fillers) container1.Remove(f, Zone.Battlefield);
            container1.Remove(tokenCard, Zone.Graveyard);
        }

        /// <summary>棋盘占用快照（排序串，用于确定性对比）</summary>
        private static string BoardSnapshot(BoardState board)
        {
            var entries = new List<string>();
            for (int z = 0; z < BoardMath.Height; z++)
                for (int x = 0; x < BoardMath.Width; x++)
                {
                    var role = BoardLayout.RoleOf(x, z);
                    if (role != CellRole.Unit && role != CellRole.Land) continue;
                    var card = board.CardAt(x, z);
                    if (card != null) entries.Add($"{card.ID}@{x},{z}");
                }
            entries.Sort();
            return string.Join("|", entries);
        }

        // ======================================== 信息族（Phase A） ========================================

        // 分支目录契约验证 TestBranchCatalog 已删（2026-10-22 用户定案：配置只保留原子表
        // AttributeValueConfig + 功能系数表 ValueSystemConfig 两张，BranchConfig.json 目录层
        // 连同 BranchConfigTable/BranchConfig 类型一并删除）。分支条件目录的现行真相源
        //（2026-10-23 有限分支入表）：身份/挂载/预算=原子表（SituationGates/BuildGatePremium 表派生）、
        // 判定谓词=BranchConditionEvaluator 代码注册；产出条件族仍为代码常量（OutcomeConditions）。

        /// <summary>
        /// 信息族与维度有限域验证（原 TestBranchCatalog 的非目录部分）。
        /// </summary>
        private static void TestInfoAtomsAndDimensions()
        {
            // 信息族原子已入原子表（JSON → 运行时表全链路；宣言手牌族只保留一档，探查手牌已删）
            Assert(CardCore.Attribute.AtomicEffectTable.GetByEnumName("DeclareHand") != null
                   && CardCore.Attribute.AtomicEffectTable.GetByEnumName("DeclareDeckTop") != null
                   && CardCore.Attribute.AtomicEffectTable.GetByEnumName("DeclareArrow") != null
                   && CardCore.Attribute.AtomicEffectTable.GetByEnumName("ProphecyNextCard") != null,
                   "信息族 4 原子已入原子表（ID 由描述哈希生成）");
            // 剧毒（2026-10-08 转关键词 GrantVenom）与毒素/沉默/净化/穿透同链入表
            Assert(CardCore.Attribute.AtomicEffectTable.GetByEnumName("GrantVenom") != null
                   && CardCore.Attribute.AtomicEffectTable.GetByEnumName("AddToxin") != null
                   && CardCore.Attribute.AtomicEffectTable.GetByEnumName("Silence") != null
                   && CardCore.Attribute.AtomicEffectTable.GetByEnumName("Purify") != null
                   && CardCore.Attribute.AtomicEffectTable.GetByEnumName("PierceDamage") != null
                   && CardCore.Attribute.AtomicEffectTable.GetByEnumName("SetCost") != null,
                   "新语义六原子已入表（剧毒关键词/毒素/沉默/净化/穿透/设置费用）");
            // 三轨制同期（2026-09-09）：无效指示物（蓝3）入表 + 设置系三原子 handler 已注册复活
            Assert(CardCore.Attribute.AtomicEffectTable.GetByEnumName("AddNullify") != null,
                   "无效指示物原子已入表（蓝3，非主动能力无法发动）");
            Assert(CardCore.Attribute.EffectHandlerRegistry.TryGetHandler(CardCore.AtomicEffectType.SetPower, out _)
                   && CardCore.Attribute.EffectHandlerRegistry.TryGetHandler(CardCore.AtomicEffectType.SetLife, out _)
                   && CardCore.Attribute.EffectHandlerRegistry.TryGetHandler(CardCore.AtomicEffectType.SetCost, out _)
                   && CardCore.Attribute.EffectHandlerRegistry.TryGetHandler(CardCore.AtomicEffectType.AddNullify, out _),
                   "设置系三原子复活注册 + 无效原子注册（三轨制重建）");

            // ---- 维度有限域（宣言对象必须是有限明确范围；卡名等开放集被排除） ----
            Assert(ProphecyDimension.IsValidValue("Type", "Creature") && !ProphecyDimension.IsValidValue("Type", "Bogus"), "维度 Type 域校验");
            Assert(ProphecyDimension.IsValidValue("Color", "Red") && !ProphecyDimension.IsValidValue("Color", "Pink"), "维度 Color 域校验");
            Assert(ProphecyDimension.IsValidValue("CostParity", "Odd") && !ProphecyDimension.IsValidValue("CostParity", "Maybe"), "维度 CostParity 域校验");
            Assert(ProphecyDimension.IsValidValue("CostExact", "9") && !ProphecyDimension.IsValidValue("CostExact", "10"), "维度 CostExact 域 0..9");
            Assert(ProphecyDimension.IsValidValue("LinkArrow", "Up") && !ProphecyDimension.IsValidValue("LinkArrow", "All"), "维度 LinkArrow 限单方向（CardCore.HexDirection 成员名）");
            Assert(ProphecyDimension.TryParse("Type:Creature", out var dim, out var val) && dim == "Type" && val == "Creature", "编码解析 维度:值");
        }

        /// <summary>
        /// 信息族引擎流验证（合成卡，不依赖卡表）：
        /// - 宣言：DeclareHand + DeclareHit 门 → 即时结算 then 奖励；
        /// - 预言：ProphecyNextCard + ProphecyHit 门 → 延迟注册（押注隐藏），
        ///   对手下回合首张出牌验证命中/未命中；整回合未出牌作未命中走 else。
        /// </summary>
        private static void TestInformationFlow(GameCore core, Player p1, Player p2)
        {
            var container1 = core.ZoneManager.GetZoneContainer(p1);
            var container2 = core.ZoneManager.GetZoneContainer(p2);
            var pool1 = core.ElementPool.GetPool(p1);
            foreach (var t in AllManaTypes()) pool1.TempElements[t] = 9;

            // 合成对手手牌：一张生物（红 2 费）+ 一张法术（蓝 1 费）
            var creatureData = new CardData { ID = "VERIFY_INFO_CREATURE", CardName = "验证生物" };
            creatureData.Supertype = Cardtype.Creature;
            creatureData.Cost[ManaType.Red] = 2;
            var spellData = new CardData { ID = "VERIFY_INFO_SPELL", CardName = "验证法术" };
            spellData.Supertype = Cardtype.Spell;
            spellData.Cost[ManaType.Blue] = 1;
            var foeCreature = new CardWrapper(creatureData);
            var foeSpell = new CardWrapper(spellData);
            foeCreature.SetController(p2);
            foeSpell.SetController(p2);
            container2.Add(foeCreature, Zone.Hand);
            container2.Add(foeSpell, Zone.Hand);

            // p1 牌库放 3 张白卡（DrawCard 奖励的可观测载体）
            for (int i = 0; i < 3; i++) container1.Add(new Card { ID = "VERIFY_INFO_DECK" }, Zone.Deck);

            var executor = core.StackEngine.GetExecutor();

            // 单张验证确定性：A 段选位器固定指定注入的生物卡；A2 递进到法术卡
            CardCore.Attribute.Handlers.ProphecyHandlerUtil.PositionPicker =
                (_, cands) => cands.Contains(foeCreature) ? foeCreature : cands[0];

            // ---- A. 宣言（即时验证）：宣言 类型:Creature → 命中 → then 抽 1 ----
            var declareDef = new EffectDefinition
            {
                Id = "VERIFY_DECLARE",
                TriggerTiming = TriggerTiming.Activate_Active,
                Steps = new List<RuntimeEffectStep>
                {
                    new RuntimeEffectStep
                    {
                        Kind = RuntimeStepKind.Atomic,
                        Atomic = new AtomicEffectInstance { Type = AtomicEffectType.DeclareHand, StringValue = "Type:Creature" },
                    },
                    new RuntimeEffectStep
                    {
                        Kind = RuntimeStepKind.Branch,
                        ConditionId = "DeclareHit",
                        Then = { new AtomicEffectInstance { Type = AtomicEffectType.DrawCard, Value = 1 } },
                    },
                },
            };

            int handBefore = core.ZoneManager.GetCards(p1, Zone.Hand).Count;
            executor.ExecuteAsync(new EffectInstance
            {
                Definition = declareDef,
                Source = p1,
                Controller = p1,
                Targets = new List<Entity>(),
            }).Forget();
            Assert(core.ZoneManager.GetCards(p1, Zone.Hand).Count == handBefore + 1,
                   "宣言命中（对手手牌含生物）→ then 抽 1 即时结算");

            // ---- A2. 已展示递进（单张验证，不展示全部）：已确认的卡不再可指定 → 下一宣言取下一张 ----
            Assert(foeCreature.IsRevealed && !foeSpell.IsRevealed,
                   "宣言确认：翻开的卡永久已展示，其余保持未展示（不展示全部）");
            CardCore.Attribute.Handlers.ProphecyHandlerUtil.PositionPicker =
                (_, cands) => cands.Contains(foeSpell) ? foeSpell : cands[0];
            handBefore = core.ZoneManager.GetCards(p1, Zone.Hand).Count;
            executor.ExecuteAsync(new EffectInstance
            {
                Definition = declareDef,
                Source = p1,
                Controller = p1,
                Targets = new List<Entity>(),
            }).Forget();
            Assert(core.ZoneManager.GetCards(p1, Zone.Hand).Count == handBefore && foeSpell.IsRevealed,
                   "宣言递进：已展示卡跳过 → 指定法术卡 → Type:Creature 未命中 → 无奖励");

            // ---- B. 预言（延迟验证）：押注 颜色:红，命中走 then 抽 1 ----
            var prophecyDef = new EffectDefinition
            {
                Id = "VERIFY_PROPHECY",
                TriggerTiming = TriggerTiming.Activate_Active,
                Steps = new List<RuntimeEffectStep>
                {
                    new RuntimeEffectStep
                    {
                        Kind = RuntimeStepKind.Atomic,
                        Atomic = new AtomicEffectInstance { Type = AtomicEffectType.ProphecyNextCard, StringValue = "Color:Red" },
                    },
                    new RuntimeEffectStep
                    {
                        Kind = RuntimeStepKind.Branch,
                        ConditionId = "ProphecyHit",
                        Then = { new AtomicEffectInstance { Type = AtomicEffectType.DrawCard, Value = 1 } },
                    },
                },
            };

            ProphecySystem.Reset();
            handBefore = core.ZoneManager.GetCards(p1, Zone.Hand).Count;
            executor.ExecuteAsync(new EffectInstance
            {
                Definition = prophecyDef,
                Source = p1,
                Controller = p1,
                Targets = new List<Entity>(),
            }).Forget();

            Assert(ProphecySystem.Pending.Count == 1, "预言已注册为待验证押注");
            Assert(core.ZoneManager.GetCards(p1, Zone.Hand).Count == handBefore,
                   "预言押注时刻不结算奖励（延迟验证）");

            // 模拟对手回合开始 → 首张打出红费生物 → 验证命中
            EventManager.Instance.Publish(new TurnStartEvent { TurnPlayer = p2, TurnNumber = 99 });
            EventManager.Instance.Publish(new CardPlayEvent { Player = p2, PlayedCard = foeCreature });
            Assert(core.ZoneManager.GetCards(p1, Zone.Hand).Count == handBefore + 1,
                   "对手首张出红卡 → 预言命中 → then 抽 1（验证时刻结算）");
            Assert(ProphecySystem.Pending.Count == 0, "验证后预言出列");

            // ---- C. 预言到期：整回合未出牌 → 未命中走 else ----
            var expiryDef = new EffectDefinition
            {
                Id = "VERIFY_PROPHECY_EXPIRY",
                TriggerTiming = TriggerTiming.Activate_Active,
                Steps = new List<RuntimeEffectStep>
                {
                    new RuntimeEffectStep
                    {
                        Kind = RuntimeStepKind.Atomic,
                        Atomic = new AtomicEffectInstance { Type = AtomicEffectType.ProphecyNextCard, StringValue = "Color:Green" },
                    },
                    new RuntimeEffectStep
                    {
                        Kind = RuntimeStepKind.Branch,
                        ConditionId = "ProphecyHit",
                        Else = { new AtomicEffectInstance { Type = AtomicEffectType.DrawCard, Value = 1 } },
                    },
                },
            };

            handBefore = core.ZoneManager.GetCards(p1, Zone.Hand).Count;
            executor.ExecuteAsync(new EffectInstance
            {
                Definition = expiryDef,
                Source = p1,
                Controller = p1,
                Targets = new List<Entity>(),
            }).Forget();
            EventManager.Instance.Publish(new TurnStartEvent { TurnPlayer = p2, TurnNumber = 100 });
            EventManager.Instance.Publish(new TurnEndEvent { TurnPlayer = p2, TurnNumber = 100 });
            Assert(core.ZoneManager.GetCards(p1, Zone.Hand).Count == handBefore + 1,
                   "对手整回合未出牌 → 预言作未命中 → else 抽 1（用户定案）");

            // 清理：合成卡不残留
            container2.Remove(foeCreature, Zone.Hand);
            container2.Remove(foeSpell, Zone.Hand);
            CardCore.Attribute.Handlers.ProphecyHandlerUtil.PositionPicker = null; // 还原缺省选位器
            ProphecySystem.Reset();
        }

        // ======================================== 关键词行为（合成随从） ========================================

        /// <summary>
        /// 关键词行为验证（合成随从，不依赖卡表）：横置可用性/冲锋/突袭/嘲讽/潜行/警戒/
        /// 先攻/连击/碾压/吸血/系命/圣盾/坚韧/护甲/不灭/复生/再生/辟邪/法术护盾。
        /// 守卫/风怒已删除（2026-09-03 原子表整体修正）；成长已删除（2026-10-08 机制移除）；
        /// 毒刺/冰晶改写族已删（2026-10-15 整体退役——改写链仅余舍身，毒素例程随退）；
        /// 穿透伤害/沉默/净化/换区清除（属性指示物反向回写）同段验证。
        /// 死亡交互（剧毒×不灭×复生）另见 DeathRules 决策表。
        /// </summary>
        private static void TestKeywords(GameCore core, Player p1, Player p2)
        {
            var combat = core.CombatSystem;
            var used = new List<Card>();

            // 合成随从：进场（带失调）+ 登记（测试后统一清理）
            Card Make(Player owner, int power, int life, params string[] keywords)
            {
                var data = new CardData { ID = "VERIFY_KW_" + used.Count, CardName = "KW" + used.Count };
                data.Supertype = Cardtype.Creature;
                data.Power = power;
                data.Life = life;
                foreach (var kw in keywords) data.Keywords.Add(kw);
                var card = new CardWrapper(data);
                card.SetController(owner);
                // 入场结果显式断言（2026-09-10）：战场容量 18，静默入不了场会把后续断言级联成假回归
                Assert(core.ZoneManager.TryAddToBattlefield(card, owner),
                       $"合成随从入场成功（{data.CardName}）");
                card.Untap(); // 本段测试前提 = 可用随从（横置行为单独断言；入场即可用后为无害冗余）
                used.Add(card);
                return card;
            }

            void CleanKeywords()
            {
                foreach (var c in used)
                    core.ZoneManager.GetZoneContainer(c.GetController() ?? p1).Remove(c, Zone.Battlefield);
            }

            // 战场清场（2026-09-10）：本段合成量大（p1 侧 25+，战场容量 18）且前置段落可能遗留单位——
            // 段界清场保证各段从净战场起步，避免「静默入不了场 → 断言级联」的假回归。
            void ResetField()
            {
                foreach (var p in new[] { p1, p2 })
                {
                    var container = core.ZoneManager.GetZoneContainer(p);
                    foreach (var c in core.ZoneManager.GetCards(p, Zone.Battlefield).ToList())
                        container.Remove(c, Zone.Battlefield);
                }
                used.Clear();
            }
            ResetField();
            // 相位复位（2026-09-20 补）：本段大量 DeclareAttack/PlayCard 走 CanCombatAction 门（Main+Active）——
            // 前序段落结束状态不保证 p1 主阶段（曾致战斗段整体级联假失败）；与 TestSleep 等同款前置。
            EnsureMainPhase(core, p1);

            // ---- 1. 表与工厂 ----
            Assert(CardCore.Attribute.AtomicEffectTable.GetByEnumName("GrantReborn") != null
                   && CardCore.Attribute.AtomicEffectTable.GetByEnumName("GrantIndestructible") != null
                   && CardCore.Attribute.AtomicEffectTable.GetByEnumName("GrantLifelink") != null
                   && CardCore.Attribute.AtomicEffectTable.GetByEnumName("AddArmor") != null,
                   "新关键词（复生/不灭/系命）与护甲原子已入表");
            // 复生 2026-10-08 指示物化：GrantReborn 行存续（refId 已重推 502be10d→94f6a32d）但退出关键词工厂——
            // 指示物处理器族接管（GrantRebornHandler 挂 RebornCounter 层）
            string rebornId;
            Assert(!CardCore.Attribute.Handlers.GrantKeywordHandlerFactory.TryGetKeywordId(AtomicEffectType.GrantReborn, out rebornId)
                   && CardCore.Attribute.CounterRules.RebornCounter == "Reborn",
                   "复生已退关键词族：工厂不再登记（指示物处理器接管，counter id 承接运行时字符串）");
            // 法术护盾 2026-10-09 指示物化：同口径收官——行存续（refId 重推 a3ad6a04→7270df35）退关键词工厂
            string spellShieldId;
            Assert(!CardCore.Attribute.Handlers.GrantKeywordHandlerFactory.TryGetKeywordId(AtomicEffectType.GrantSpellShield, out spellShieldId)
                   && CardCore.Attribute.CounterRules.SpellShieldCounter == "SpellShield",
                   "法术护盾已退关键词族：工厂不再登记（指示物处理器接管，counter id 承接运行时字符串）");
            var spellShieldRow = CardCore.Attribute.AtomicEffectTable.GetByEnumName("GrantSpellShield");
            Assert(spellShieldRow != null && spellShieldRow.HashId == "7270df35",
                   "法术护盾行 refId 重推：a3ad6a04→7270df35（指示物化随文案换号）");

            // ---- 2. 横置可用性 / 冲锋 / 突袭（2026-10-17 定案：取消横置入场——入场一律可用；冲锋/突袭=登场效果，解横置成空操作） ----
            var tappedUnit = Make(p1, 3, 3);
            tappedUnit.Tap(); // 模拟横置在场（未过回合重置）
            Assert(!combat.CanDeclareAttack(tappedUnit, p1), "横置随从不可攻击（可用性统一走横置）");
            var readyUnit = Make(p1, 3, 3);
            Assert(combat.CanDeclareAttack(readyUnit, p1), "未横置随从可攻击");

            // 入场（直接观察 TryAddToBattlefield 行为，不经 Make 的竖置前提）
            CardWrapper MakeEntry(params CardEffectData[] effects)
            {
                var data = new CardData { ID = "VERIFY_KW_ENTRY_" + used.Count, CardName = "入场" + used.Count };
                data.Supertype = Cardtype.Creature;
                data.Power = 3; data.Life = 3;
                if (effects != null && effects.Length > 0)
                {
                    data.Effects ??= new List<CardEffectData>();
                    data.Effects.AddRange(effects);
                }
                var card = new CardWrapper(data);
                card.SetController(p1);
                // 经发动区真入场（CastPlayed 派生）：OnPlay 载荷过滤只认 CastPlayed——
                // 直接 TryAddToBattlefield 是 TokenSpawned，冲锋/突袭的登场效果（OnPlay）不会触发
                // （锚在 f1eb82c 写下时即用错路径，存量修正）
                core.ZoneManager.GetZoneContainer(p1).Add(card, Zone.Activation);
                // 入场结果显式断言：满场入墓会静默断链登场效果（OnPlay 依赖 CastPlayed 入场事件）
                Assert(core.ZoneManager.TryMoveToBattlefield(card, p1, Zone.Activation),
                       $"经发动区入场成功（{data.CardName}）");
                used.Add(card);
                return card;
            }

            // 冲锋/突袭的登场效果（2026-09-08）：
            // OnPlay + 激励自己（域 {Self}）。突袭的自紊乱曾以 SelfSickness 代价承载——
            // 2026-09-14 代价原子化后退役（代价栏只剩 Payload 原子；自上紊乱改由 RushSickness
            // 原子表达——下方以直接执行原子模拟付费后果）。
            CardEffectData EntryReadyEffect(bool withSickness)
            {
                var eff = new CardEffectData
                {
                    // 2026-09-13 修复：Id 参数化——冲锋/突袭两张夹具共用同 Id 时，
                    // 触发上限闸门（按 effect.Id 记账，默认一回合一次）会把第二次触发静默拦截，
                    // 突袭段的入场效果根本不上栈（表现="解除横置失败"，曾误诊为代价语义分歧）
                    Id = withSickness ? "VERIFY_ENTRY_READY_RUSH" : "VERIFY_ENTRY_READY",
                    DisplayName = withSickness ? "突袭" : "冲锋",
                    Description = "登场：激励自身——解除横置",
                    TriggerTiming = (int)TriggerTiming.OnPlay,
                    SelectionMode = (int)CardCore.SelectionMode.Single,
                };
                eff.AtomicEffects = new List<AtomicEffectEntry>
                {
                    AtomRefs.New(AtomicEffectType.Untap, value: 1, kinds: new List<int> { 0 }),
                };
                return eff;
            }

            var plain = MakeEntry();
            Assert(!plain.IsTapped(), "普通随从：入场即可用（2026-10-17 取消横置入场）");
            // A组诊断埋点 v2（2026-09-10）：计数改为按效果 Id 过滤（v1 无过滤，×1 可能是杂散效果）；
            // 附转换 X 光（直接看 ConvertAll 产物的域/模式/激活类）+ 入场即入队探针。
            CardWrapper charger = null;
            int stackAdd = 0;
            EffectInstance chargerInstance = null;
            void OnStackAdd(StackAddEvent e)
            {
                if (e.AddedObject is EffectInstance ei && ei.Definition?.Id == "VERIFY_ENTRY_READY")
                {
                    stackAdd++;
                    chargerInstance = ei; // 捕获实例引用，事后查 IsResolved
                }
            }
            EventManager.Instance.Subscribe<StackAddEvent>(OnStackAdd);
            charger = MakeEntry(EntryReadyEffect(false));
            Assert(!charger.IsTapped(), "冲锋（登场效果）：入场即可用（解横置效果行成为空操作）");
            bool queuedAtEntry = core.StackEngine.HasPendingEffects; // 入场事件应已把 OnPlay 排进待发队列
            var conv = GameActions.GetCardEffectDefinitions(charger);
            UnityEngine.Debug.Log("[KWDBG] charger 转换=" + string.Join(";", conv.Select(d =>
                $"{d.Id}:触发式={d.IsTriggeredEffect},激活={d.ActivationType},域数={(d.TargetDomain == null ? -1 : d.TargetDomain.Count)}"
                + $",模式={d.SelectionMode},filter={d.TargetFilter}"))
                + $" | 入场即待发={queuedAtEntry}");
            GameActions.DrainStack(core); // 排干栈：登场效果结算
            EventManager.Instance.Unsubscribe<StackAddEvent>(OnStackAdd);
            UnityEngine.Debug.Log($"[KWDBG] charger 本体上栈×{stackAdd} 已结算标记={chargerInstance?.IsResolved}"
                                + $" 目标数={chargerInstance?.Targets?.Count} tapped={charger.IsTapped()} "
                                + $"栈深={core.StackEngine.StackSize} 待发={core.StackEngine.HasPendingEffects} "
                                + $"结算中={core.StackEngine.IsResolving}");
            Assert(!charger.IsTapped(),
                   $"冲锋（登场效果）：结算后解除横置（激励自己）（诊断：入场即待发={queuedAtEntry} 上栈×{stackAdd} "
                   + $"已结算={chargerInstance?.IsResolved} 目标数={chargerInstance?.Targets?.Count}——"
                   + "上栈=0查触发注册/匹配；目标数=0查Self解析[Console搜TargetDomain警告]）");
            Assert(combat.CanDeclareAttack(charger, p1) && combat.CanAttackTarget(charger, p2, p1),
                   "冲锋：无目标限制，可攻击玩家");
            var rusher = MakeEntry(EntryReadyEffect(true));
            var enemy = Make(p2, 1, 9);
            GameActions.DrainStack(core);
            Assert(!rusher.IsTapped()
                   && rusher.GetCounterCount(CardCore.Attribute.KeywordRules.RushSicknessCounter) == 0,
                   "突袭：结算后解除横置（自紊乱代价已退役——紊乱由下方原子执行模拟）");
            // 模拟「付代价」路径（2026-09-14 代价原子化）：RushSickness 原子直接执行 → 自上紊乱 1 层
            CardCore.Attribute.EffectHandlerRegistry.ExecuteEffectAsync(
                new AtomicEffectInstance { Type = AtomicEffectType.RushSickness, Value = 1 },
                new EffectExecutionContext
                {
                    Controller = p1, Source = rusher, ZoneManager = core.ZoneManager, ElementPool = core.ElementPool,
                    Targets = new List<Entity> { rusher },
                }).GetAwaiter().GetResult();
            Assert(rusher.GetCounterCount(CardCore.Attribute.KeywordRules.RushSicknessCounter) == 1,
                   "RushSickness 原子执行后：自上紊乱 1 层");
            Assert(combat.CanAttackTarget(rusher, enemy, p1) && !combat.CanAttackTarget(rusher, p2, p1),
                   "突袭：紊乱期间只能攻随从，不准攻击玩家（效果发动同口径）");
            CardCore.Attribute.CounterRules.OnTurnEnd(p1, core.ZoneManager); // 回合结束持续指示物清理
            Assert(rusher.GetCounterCount(CardCore.Attribute.KeywordRules.RushSicknessCounter) == 0
                   && combat.CanAttackTarget(rusher, p2, p1),
                   "突袭：紊乱消退（持续到回合结束）后目标限制解除");

            // ---- 3. 帷幕（原嘲讽,2026-09-13 更名=只吸引效果目标）不拦攻击；碾压已重定义为攻击溅射（见第 11 段） ----
            var attacker = Make(p1, 3, 3);
            var curtainHolder = Make(p2, 0, 9, "Taunt");
            Assert(combat.CanAttackTarget(attacker, p2, p1),
                   "帷幕：不拦攻击——有帷幕随从也可指定玩家（效果侧吸引见 TestTauntAndSickness；溅射见第 11 段）");
            curtainHolder.IsAlive = false; // 清场（不干扰后续段）

            // ---- 4. 守卫关键词已删除（2026-09-03 原子表整体修正）：无守卫转移，攻击目标保持宣言 ----
            var striker = Make(p1, 3, 3);
            var victim = Make(p2, 2, 5);
            var guardDoppel = Make(p2, 1, 8); // 同位置单位（无 Guard 关键词——已删除）
            bool decl4 = GameActions.DeclareAttack(core, p1, striker, victim);
            var attackInst4 = core.StackEngine.Peek();
            Assert(decl4 && attackInst4 != null && attackInst4.IsAttackDeclaration && attackInst4.Targets[0] == victim
                   && !guardDoppel.IsTapped(),
                   $"守卫删除：攻击目标保持宣言（无转移、无横置旁观者；宣言期零支付）"
                   + $"（诊断 decl={decl4} peek={(attackInst4 == null ? "null" : attackInst4.GetType().Name)}"
                   + $" phase={core.TurnEngine.CurrentPhase?.Phase}/{core.TurnEngine.CurrentPhase?.State}"
                   + $" turnIsP1={core.TurnEngine.TurnPlayer == p1}"
                   + $" canDecl={combat.CanDeclareAttack(striker, p1)} canTgt={combat.CanAttackTarget(striker, victim, p1)}"
                   + $" 栈深={core.StackEngine.StackSize} 待发={core.StackEngine.HasPendingEffects}"
                   + $" sp={core.StackEngine.SpeedCounter.CurrentSpeed} spr={core.StackEngine.SpeedCounter.IsResolving}"
                   + $" gate0={core.StackEngine.SpeedCounter.CanActivate(0, CardCore.EffectActivationType.Voluntary, false)}"
                   + $" prio={core.StackEngine.CurrentPriorityHolder?.Name ?? "null"}"
                   + $" act={core.StackEngine.ActivePlayer?.Name ?? "null"} turn={core.TurnEngine.TurnNumber}"
                   + $" tapped={striker.IsTapped()}）");
            GameActions.DrainStack(core);

            // ---- 5. 潜行：不可被指定 + 攻击结算后消耗 1 层（2026-10-08 指示物化；Make 印刷路径入场挂层）----
            var lurker = Make(p2, 2, 2, CardCore.Attribute.CounterRules.StealthCounter);
            var hunter = Make(p1, 3, 3);
            Assert(!combat.CanAttackTarget(hunter, lurker, p1), "潜行：不可被指定为攻击目标");
            var spy = Make(p1, 2, 2, CardCore.Attribute.CounterRules.StealthCounter);
            GameActions.DeclareAttack(core, p1, spy, p2);
            GameActions.DrainStack(core);
            Assert(spy.GetCounterCount(CardCore.Attribute.CounterRules.StealthCounter) == 0,
                "潜行：攻击结算后消耗（结算期支付段，层数归零）");

            // ---- 6. 警戒（2026-09-10 重定义）：攻击照常横置；横置也能造成战斗伤害 ----
            var vigilant = Make(p1, 3, 3, "Vigilance");
            GameActions.DeclareAttack(core, p1, vigilant, p2);
            GameActions.DrainStack(core);
            Assert(vigilant.IsTapped(), "警戒重定义：攻击结算期照常横置（不再代替横置扣除）");
            Assert(CardCore.Attribute.KeywordRules.ShouldTap(vigilant), "警戒重定义：横置代价恒支付");
            // 横置持警戒反击：已横置防守者被攻击仍反击（见 TestAttackGuard 2b；2026-10-15 起
            // 拦截守卫战斗期间可用，反击不再依赖警戒）

            // ---- 7. 横置即上限（2026-09-10 定案：计数/会话门槛均已撤，激励可再动；逐攻击开窗）----
            var loneWolf = Make(p1, 1, 9);
            int p2Life7 = p2.Life;
            Assert(GameActions.DeclareAttack(core, p1, loneWolf, p2), "横置即上限：首次攻击宣言");
            GameActions.DrainStack(core);
            Assert(loneWolf.IsTapped() && !combat.CanDeclareAttack(loneWolf, p1),
                   "横置即上限：攻击结算后保持横置、不能再攻");
            loneWolf.Untap(); // 激励通路
            Assert(combat.CanDeclareAttack(loneWolf, p1) && GameActions.DeclareAttack(core, p1, loneWolf, p2),
                   "激励再动：解除横置即可再宣（无每回合计数/会话去重门槛）");
            GameActions.DrainStack(core);
            Assert(loneWolf.AttacksThisTurn == 2 && p2.Life == p2Life7 - 2,
                   "激励再动：两次宣言都结算（台账计数 2、玩家共受 2 伤）");

            // ---- 7b. 穿透伤害（原"不可防止伤害"改名）：越过关键词与指示物，替代层照走 ----
            var wardedTank = Make(p2, 1, 20, CardCore.Attribute.CounterRules.DivineShieldCounter);
            wardedTank.AddCounters(CardCore.Attribute.KeywordRules.ArmorCounter, 3);
            wardedTank.AddCounters(CardCore.Attribute.CounterRules.ToughnessCounter, 1); // 坚韧 1 层
            CardCore.Attribute.KeywordRules.ApplyDamage(p1, wardedTank, 5, false, pierce: true);
            Assert(wardedTank.IsAlive && wardedTank.GetLife() == 15
                   && wardedTank.GetCounterCount(CardCore.Attribute.CounterRules.DivineShieldCounter) == 1
                   && wardedTank.GetCounterCount(CardCore.Attribute.KeywordRules.ArmorCounter) == 3,
                   "穿透：圣盾/护甲/坚韧全被越过（20−5=15，防护层不消耗）");
            CardCore.Attribute.KeywordRules.ApplyDamage(p1, wardedTank, 5, false); // 普通路径对照
            Assert(wardedTank.GetLife() == 15
                   && wardedTank.GetCounterCount(CardCore.Attribute.CounterRules.DivineShieldCounter) == 0,
                   "普通伤害对照：圣盾挡下一次并消耗（层数归零）");

            // ---- 8. 先攻：目标死亡不反击 ----
            var first = Make(p1, 5, 3, "FirstStrike");
            var bulky = Make(p2, 4, 3);
            bool declared8 = GameActions.DeclareAttack(core, p1, first, bulky);
            UnityEngine.Debug.Log($"[CMBTDBG] declared={declared8} stack={core.StackEngine.StackSize} first._power={first.GetPower()} layerPower={core.LayerEngine.CalculatePower(first)} firstTapped={first.IsTapped()} bulkyAlive={bulky.IsAlive} bulkyLife={bulky.GetLife()}");
            GameActions.DrainStack(core);
            UnityEngine.Debug.Log($"[CMBTDBG] after ExecuteDamage: bulkyAlive={bulky.IsAlive} bulkyLife={bulky.GetLife()} firstLife={first.GetLife()}");
            Assert(!bulky.IsAlive && first.GetLife() == 3, "先攻：目标死于先攻步，不反击");

            // ---- 8b. 缴械：被攻击的目标无法反击 ----
            ResetField(); // 段界清场（2026-10-13 缩盘后容量 12：本段合成量大，逐段清防溢出）
            var disarmer = Make(p1, 3, 5, "Disarm");
            var bigGuard = Make(p2, 4, 9);
            GameActions.DeclareAttack(core, p1, disarmer, bigGuard);
            GameActions.DrainStack(core);
            Assert(disarmer.GetLife() == 5 && bigGuard.GetLife() == 6,
                   "缴械：目标（4 攻）无法反击，攻击者无伤（单向伤害）");

            // ---- 8c. 反击资格：已横置的随从只能挨打（不反击、无消耗） ----
            var aggressor = Make(p1, 3, 5);
            var tired = Make(p2, 4, 9);
            tired.Tap(); // 模拟已横置（刚攻击过/被冻结）
            GameActions.DeclareAttack(core, p1, aggressor, tired);
            GameActions.DrainStack(core);
            Assert(aggressor.GetLife() == 5 && tired.GetLife() == 6,
                   "反击资格：已横置目标（4 攻）不反击，攻击者无伤");

            // ---- 9. 连击：两步各结算一次 ----
            ResetField(); // 段界清场（容量 12）
            var doubleS = Make(p1, 2, 9, "DoubleStrike");
            var tank = Make(p2, 1, 5);
            GameActions.DeclareAttack(core, p1, doubleS, tank);
            GameActions.DrainStack(core);
            Assert(tank.GetLife() == 1 && doubleS.IsAlive, "连击：伤害结算两次（5命 −2×2 = 1）");

            // ---- 11. 碾压：邻接受击（注入邻接扩展点；2026-10-04 语义修订=左右同排生物） ----
            ResetField(); // 段界清场（容量 12）
            var hammer = Make(p1, 4, 9, "Overwhelm");
            var pivot = Make(p2, 1, 9);
            var neighbor = Make(p2, 1, 9);
            // 相邻结界（无生命单位，耐久 3）：溅射域只含生物——结界不吃溅射（耐久不扣）
            var wardStone = new CardWrapper(new CardData
            {
                ID = "VERIFY_KW_ENCH", CardName = "侧翼结界", Supertype = Cardtype.Enchantment,
            });
            wardStone.SetController(p2);
            Assert(core.ZoneManager.TryAddToBattlefield(wardStone, p2), "侧翼结界入场成功");
            wardStone.AddCounters(CardCore.Attribute.CounterRules.DurabilityCounter, 3);
            used.Add(wardStone);
            CardCore.CombatSystem.AdjacentResolver = c => c == pivot
                ? new[] { neighbor, wardStone } : System.Array.Empty<Card>();
            GameActions.DeclareAttack(core, p1, hammer, pivot);
            GameActions.DrainStack(core);
            Assert(pivot.GetLife() == 5 && neighbor.GetLife() == 5, "碾压：目标与相邻随从各受 4 点（无反击）");
            Assert(wardStone.IsAlive
                   && wardStone.GetCounterCount(CardCore.Attribute.CounterRules.DurabilityCounter) == 3,
                   "碾压：溅射只打生物——相邻结界耐久不扣（3→3）");
            CardCore.CombatSystem.AdjacentResolver = null;

            // ---- 12. 毒刺/冰晶已删（2026-10-15 战斗伤害改写族整体退役——KeywordRules 改写链仅余舍身；
            //      毒素指示物随毒刺失去唯一施加口，回合末发作例程断言一并退役）----

            // ---- 12c. 剧毒关键词（2026-10-08 指示物转关键词·落定追加式）：受其战斗伤害的生物被消灭 ----
            ResetField(); // 段界清场（容量 12）
            var venomFang = Make(p1, 2, 9, "Venom");
            var bigPrey = Make(p2, 3, 10);
            CardCore.Attribute.KeywordRules.ApplyDamage(venomFang, bigPrey, 2, true);
            Assert(!bigPrey.IsAlive || bigPrey.GetZone() == Zone.Graveyard,
                   "剧毒关键词：受其战斗伤害的生物被消灭（2 伤落定→TryKill，剩余生命无关）");
            var shieldPrey = Make(p2, 1, 9, CardCore.Attribute.CounterRules.DivineShieldCounter);
            CardCore.Attribute.KeywordRules.ApplyDamage(venomFang, shieldPrey, 4, true);
            Assert(shieldPrey.IsAlive && shieldPrey.GetLife() == 9
                   && shieldPrey.GetCounterCount(CardCore.Attribute.CounterRules.DivineShieldCounter) == 0,
                   "剧毒对照：圣盾挡下（落定 0）不触发消灭，圣盾照常消耗");
            int p2LifeBeforeVenom = p2.Life;
            CardCore.Attribute.KeywordRules.ApplyDamage(venomFang, p2, 3, true);
            Assert(p2.Life == p2LifeBeforeVenom - 3 && p2.IsAlive,
                   "剧毒对照：对角色照常落血不消灭（Deathtouch 口径）");

            // ---- 13. 吸血（恢复自身）/ 系命（回复角色） ----
            ResetField(); // 段界清场：2-12 段已累计 17+ 单位，逼近容量 18
            var bat = Make(p1, 2, 3, "Lifesteal");
            CardCore.Attribute.KeywordRules.ApplyDamage(p2, bat, 2, false); // 受伤状态
            var prey = Make(p2, 0, 9);
            GameActions.DeclareAttack(core, p1, bat, prey);
            GameActions.DrainStack(core);
            Assert(bat.GetLife() == 3, "吸血：造成 2 伤害恢复随从自身");

            p1.Life = 20;
            var monk = Make(p1, 3, 3, "Lifelink");
            GameActions.DeclareAttack(core, p1, monk, p2);
            GameActions.DrainStack(core);
            Assert(p1.Life == 23, "系命：造成 3 伤害回复角色");

            // ---- 14. 圣盾 / 坚韧 / 护甲指示物 ----
            var shielded = Make(p2, 1, 5, CardCore.Attribute.CounterRules.DivineShieldCounter);
            var breaker = Make(p1, 4, 9);
            GameActions.DeclareAttack(core, p1, breaker, shielded);
            GameActions.DrainStack(core);
            Assert(shielded.IsAlive && shielded.GetLife() == 5
                   && shielded.GetCounterCount(CardCore.Attribute.CounterRules.DivineShieldCounter) == 0,
                   "圣盾：挡下一次伤害并消耗（层数归零）");
            breaker.Untap();
            breaker.AttacksThisTurn = 0; // 台账清零（2026-09-10 后非门槛，仅为下文计数断言口径干净）
            GameActions.DeclareAttack(core, p1, breaker, shielded);
            GameActions.DrainStack(core);
            Assert(shielded.GetLife() == 1, "圣盾消耗后正常受伤");

            // ---- 14b. 圣盾指示物叠加（2026-10-08 指示物化）：多层=多挡（逐份消耗口径）----
            var twin = Make(p2, 2, 9, CardCore.Attribute.CounterRules.DivineShieldCounter); // 印刷路径入场挂 1 层
            twin.AddCounters(CardCore.Attribute.CounterRules.DivineShieldCounter, 1);        // 追加 1 层（旧附加份等价）
            Assert(twin.GetCounterCount(CardCore.Attribute.CounterRules.DivineShieldCounter) == 2,
                   "圣盾指示物：两层并存（可叠加）");
            Attribute.KeywordRules.ApplyDamage(p1, twin, 4, false);
            Assert(twin.GetLife() == 9 && twin.GetCounterCount(CardCore.Attribute.CounterRules.DivineShieldCounter) == 1,
                   "两层并存：消耗 1 层，余层仍生效");
            Attribute.KeywordRules.ApplyDamage(p1, twin, 4, false);
            Assert(twin.GetLife() == 9 && twin.GetCounterCount(CardCore.Attribute.CounterRules.DivineShieldCounter) == 0,
                   "两层先后耗尽");
            Attribute.KeywordRules.ApplyDamage(p1, twin, 4, false);
            Assert(twin.GetLife() == 5, "层耗尽后正常受伤");

            var tough = Make(p2, 1, 9);
            tough.AddCounters(CardCore.Attribute.CounterRules.ToughnessCounter, 1); // 坚韧 1 层
            var hitter = Make(p1, 4, 9);
            GameActions.DeclareAttack(core, p1, hitter, tough);
            GameActions.DrainStack(core);
            Assert(tough.GetLife() == 6
                   && tough.GetCounterCount(CardCore.Attribute.CounterRules.ToughnessCounter) == 0,
                   "坚韧：最终伤害 −1（9 −3 = 6），1 层生效一次即清零（减半 floor）");

            // 重挂 2 层（可叠加口径；上一击已将 1 层清零）
            tough.AddCounters(CardCore.Attribute.CounterRules.ToughnessCounter, 2);
            Attribute.KeywordRules.ApplyDamage(p1, tough, 4, false);
            Assert(tough.GetLife() == 4
                   && tough.GetCounterCount(CardCore.Attribute.CounterRules.ToughnessCounter) == 1,
                   "双坚韧（叠加 2 层）：伤害 −2，生效后减半余 1 层");

            var plated = Make(p2, 1, 5);
            plated.AddCounters(CardCore.Attribute.KeywordRules.ArmorCounter, 3);
            CardCore.Attribute.KeywordRules.ApplyDamage(p1, plated, 2, false);
            Assert(plated.GetLife() == 5 && plated.GetCounterCount(CardCore.Attribute.KeywordRules.ArmorCounter) == 1,
                   "护甲指示物：吸收 2 点后剩余 1（生命未动）");

            // ---- 15. 不灭 / 复生（毁灭原子已删除——用吞噬路径验证不灭拦截） ----
            ResetField(); // 段界清场
            var eternal = Make(p1, 2, 5, "Indestructible");
            var devourDef = new EffectDefinition
            {
                Id = "VERIFY_DEVOUR",
                TriggerTiming = TriggerTiming.Activate_Active,
                Effects = new List<AtomicEffectInstance> { new AtomicEffectInstance { Type = AtomicEffectType.Devour } },
            };
            core.StackEngine.GetExecutor().ExecuteAsync(new EffectInstance
            {
                Definition = devourDef,
                Source = p1,
                Controller = p1,
                Targets = new List<Entity> { eternal },
            }, skipElementCost: true).Forget();
            Assert(eternal.IsAlive, "不灭：吞噬类消灭无效（毁灭原子已删除，不灭拦消灭族）");

            var phoenix = Make(p1, 2, 5, CardCore.Attribute.CounterRules.RebornCounter);
            phoenix.IsAlive = false;
            Assert(CardCore.Attribute.KeywordRules.TryReborn(phoenix)
                   && phoenix.IsAlive && phoenix.GetLife() == 1 && !phoenix.IsTapped()
                   && phoenix.GetCounterCount(CardCore.Attribute.CounterRules.RebornCounter) == 0,
                   "复生：1 血回场（2026-10-19 起不横置）、消耗 1 层");

            // ---- 15b. 死亡决策表（DeathRules）：死因×护盾 定案断言（DeathCause.Poison 列已随
            //      2026-10-08 剧毒转关键词删除——消灭统一走 DestroyEffect） ----
            var stoneGiant = Make(p2, 2, 9, "Indestructible");
            Assert(!CardCore.Attribute.DeathRules.TryKill(stoneGiant, CardCore.Attribute.DeathCause.DestroyEffect, p1, core.ZoneManager)
                   && stoneGiant.IsAlive,
                   "决策表：不灭拦截摧毁效果死因");
            var ironReborn = Make(p2, 2, 9, CardCore.Attribute.CounterRules.RebornCounter);
            Assert(!CardCore.Attribute.DeathRules.TryKill(ironReborn, CardCore.Attribute.DeathCause.DestroyEffect, p1, core.ZoneManager)
                   && ironReborn.IsAlive && ironReborn.GetLife() == 1,
                   "决策表：复生对摧毁效果死因生效（消耗回场，TryKill 返回未死）");

            // ---- 15b2. 角色复生/角色潜行（2026-10-19 Entity 化：表行去 NoRole 后对角色全通） ----
            int p2LifeBackup = p2.Life;
            p2.AddCounters(CardCore.Attribute.CounterRules.RebornCounter, 1);
            p2.Life = 0;
            core.SBAEngine.ExecuteAll();
            Assert(p2.Life == 1 && !core.IsGameOver
                   && p2.GetCounterCount(CardCore.Attribute.CounterRules.RebornCounter) == 0,
                   "角色复生：归零宣判前消耗 1 层回 1 血，不宣判不终局");
            p2.Life = p2LifeBackup;

            p2.AddCounters(CardCore.Attribute.CounterRules.StealthCounter, 1);
            Assert(!core.CombatSystem.CanAttackTarget(phoenix, p2, p1),
                   "角色潜行：持层期间不可被指定为攻击目标");
            var shown = TargetResolver.ExcludeUnselectable(new List<Entity> { phoenix, p2 }, p1);
            Assert(!shown.Contains(p2) && shown.Contains(phoenix),
                   "角色潜行：选择域隐藏对方角色、己方单位照常可选");
            p2.AddCounters(CardCore.Attribute.CounterRules.StealthCounter, -1);

            // ---- 15c. 神佑已删（2026-10-13 定案）----
            // 锚①：角色构造/Reset 均不再注入神佑；锚②：净化打在角色上照常清 Status 轨
            //（原「神佑抗净化」锚随机制删除，角色侧净化覆盖保留）。
            Assert(!p1.HasKeyword("DivineProtection") && !p2.HasKeyword("DivineProtection"),
                   "神佑已删：角色不再默认持有（字面量锚防意外复活）");
            p2.Life = 30;
            p2.AddKeyword(CardCore.Attribute.KeywordRules.Taunt, CardCore.KeywordLane.Status);
            new CardCore.Attribute.Handlers.PurifyHandler().Execute(
                new CardCore.AtomicEffectInstance { Type = CardCore.AtomicEffectType.Purify },
                new CardCore.EffectExecutionContext
                {
                    Source = p1, Controller = p1,
                    Targets = new List<Entity> { p2 },
                    ZoneManager = core.ZoneManager
                });
            Assert(!p2.HasKeyword(CardCore.Attribute.KeywordRules.Taunt),
                   "净化打在角色上：Status 轨照常清（神佑豁免已随机制删除）");

            // ---- 15d. 死亡归因全链路（2026-09-07 定案：死因/死亡来源/伤害来源/效果来源） ----
            // ① 生命流失：即时决策表死亡（非伤害、不可防止），Cause=LifeLoss、死亡来源=效果来源
            var drainSource = Make(p1, 4, 4);
            var drainVictim = Make(p2, 2, 3);
            CardDestroyEvent drainEvt = null;
            void OnDrainKill(CardDestroyEvent e) { if (e.DestroyedCard == drainVictim) drainEvt = e; }
            EventManager.Instance.Subscribe<CardDestroyEvent>(OnDrainKill);
            new CardCore.Attribute.Handlers.LifeLossHandler().Execute(
                new CardCore.AtomicEffectInstance { Type = CardCore.AtomicEffectType.LifeLoss, Value = 3 },
                new CardCore.EffectExecutionContext
                {
                    Source = drainSource, Controller = p1,
                    Targets = new List<Entity> { drainVictim },
                    ZoneManager = core.ZoneManager
                });
            Assert(drainEvt != null && drainEvt.Cause == CardCore.Attribute.DeathCause.LifeLoss
                   && drainEvt.Source == drainSource && !drainVictim.IsAlive
                   && core.ZoneManager.GetCards(p2, Zone.Graveyard).Contains(drainVictim),
                   "生命流失：即时决策表死亡（Cause=LifeLoss，死亡来源=效果来源，不走伤害管线）");

            // ② 伤害致死：SBA 收尸归因不丢（DamageLethal + 伤害来源）
            var killer = Make(p1, 5, 5);
            var slain = Make(p2, 2, 3);
            CardDestroyEvent combatEvt = null;
            void OnCombatKill(CardDestroyEvent e) { if (e.DestroyedCard == slain) combatEvt = e; }
            EventManager.Instance.Subscribe<CardDestroyEvent>(OnCombatKill);
            CardCore.Attribute.KeywordRules.ApplyDamage(killer, slain, 3, false);
            core.SBAEngine.CheckAndExecute();
            Assert(combatEvt != null && combatEvt.Cause == CardCore.Attribute.DeathCause.DamageLethal
                   && combatEvt.Source == killer && !slain.IsAlive
                   && core.ZoneManager.GetCards(p2, Zone.Graveyard).Contains(slain),
                   "伤害致死：归因随尸体留档，SBA 送墓上事件（Cause=DamageLethal，死亡来源=伤害来源）");

            // ③ 剧毒关键词消灭：死亡来源=剧毒持有者（伤害来源归因；Cause=DestroyEffect 消灭口径）
            var venomKiller = Make(p1, 3, 3, "Venom");
            var venomVictim = Make(p2, 2, 5);
            CardDestroyEvent venomEvt = null;
            void OnVenomKill(CardDestroyEvent e) { if (e.DestroyedCard == venomVictim) venomEvt = e; }
            EventManager.Instance.Subscribe<CardDestroyEvent>(OnVenomKill);
            CardCore.Attribute.KeywordRules.ApplyDamage(venomKiller, venomVictim, 2, true);
            Assert(venomEvt != null && venomEvt.Cause == CardCore.Attribute.DeathCause.DestroyEffect
                   && venomEvt.Source == venomKiller,
                   "剧毒消灭：Cause=DestroyEffect，死亡来源=剧毒持有者（伤害来源归因）");

            // ④ 减益致死：削减归零标死，指示物来源已登记（GetCounterSource 公共查询）
            var debuffer = Make(p1, 4, 4);
            var withered = Make(p2, 2, 3);
            CardCore.Attribute.CounterRules.AddStatCounter(withered, CardCore.Attribute.CounterRules.LifeDownCounter, 3, debuffer);
            Assert(!withered.IsAlive
                   && withered.GetCounterSource(CardCore.Attribute.CounterRules.LifeDownCounter) == debuffer,
                   "减益致死：削减归零标死，来源登记（SBA 收尸时归因到施加方）");

            // ⑤ 流失扣上限边界（2026-09-14 代价原子化：LifeLoss 原子=支付载体，无 CanPay 门槛）：
            // 可扣到恰好归零（归零=正常死亡交生命判定收尾）；新玩家夹具不污染共享 p2
            var plEdge = new Player("VERIFY_KW_LIFE_EDGE", 30);
            CardCore.Attribute.EffectHandlerRegistry.ExecuteEffectAsync(
                new AtomicEffectInstance { Type = AtomicEffectType.LifeLoss, Value = 30 },
                new EffectExecutionContext { Controller = plEdge, Source = plEdge, Targets = new List<Entity> { plEdge } })
                .GetAwaiter().GetResult();
            Assert(plEdge.MaxHealth == 0 && plEdge.Life == 0,
                   "流失原子：可扣到恰好归零（归零=正常死亡交生命判定收尾）");

            // ---- 16. 再生（2026-09-13 全额定案：己方准备阶段恢复全部生命；成长 2026-10-08 机制删除） ----
            for (int i = 0; i < 3; i++) core.ZoneManager.GetZoneContainer(p1).Add(new Card { ID = "VERIFY_KW_DECK" }, Zone.Deck);
            var regrow = Make(p1, 2, 4, "Regeneration");
            CardCore.Attribute.KeywordRules.ApplyDamage(p2, regrow, 2, false);
            EventManager.Instance.Publish(new TurnStartEvent { TurnPlayer = p1, TurnNumber = 300 });
            core.RunStandbyAutomation(p1, 300); // 2026-10-26 精准切分：自动化迁准备阶段——合成事件须显式补调
            Assert(regrow.GetLife() == 4 && regrow.GetPower() == 2,
                   "再生恢复全部生命（2→满4）——准备阶段维护");

            // ---- 17. 法术护盾（效果指定）（辟邪断言已随 2026-10-13 扰魔整体退役删除——
            //      原 EffectTargetValidator 为死基建从未接通，断言测的是死代码） ----
            var foe1 = Make(p1, 2, 2);

            var warded = Make(p2, 2, 5, CardCore.Attribute.CounterRules.SpellShieldCounter); // 印刷路径入场挂 1 层
            var targets = new List<Entity> { warded, p2 };
            CardCore.Attribute.KeywordRules.ConsumeSpellShields(targets, foe1);
            Assert(targets.Count == 1 && targets[0] == p2
                   && warded.GetCounterCount(CardCore.Attribute.CounterRules.SpellShieldCounter) == 0,
                   "法术护盾：成为对手效果目标时移出目标并消耗 1 层（印刷份=1 层归零）");

            warded.AddCounters(CardCore.Attribute.CounterRules.SpellShieldCounter, 2); // 追加 2 层（多层=多次抵消）
            // 每次消耗=一次独立效果指定目标——逐次重建目标列表（共享列表会在首次消耗时被移出，后两次空转）
            for (int volleyIdx = 0; volleyIdx < 3; volleyIdx++)
            {
                var volley = new List<Entity> { warded };
                CardCore.Attribute.KeywordRules.ConsumeSpellShields(volley, foe1);
                if (volley.Count == 1) break; // 第 3 次（层已尽）照常作用——目标保留
            }
            int layersAfterVolley = warded.GetCounterCount(CardCore.Attribute.CounterRules.SpellShieldCounter);
            Assert(layersAfterVolley == 0,
                   $"法术护盾多层：每层抵消一次——2 层恰挡 2 次，第 3 次照常作用（实际层余 {layersAfterVolley}）");

            warded.AddCounters(CardCore.Attribute.CounterRules.SpellShieldCounter, 1);
            var friendly = new List<Entity> { warded };
            CardCore.Attribute.KeywordRules.ConsumeSpellShields(friendly, warded);
            Assert(friendly.Count == 1
                   && warded.GetCounterCount(CardCore.Attribute.CounterRules.SpellShieldCounter) == 1,
                   $"法术护盾：友方效果不消耗（仅对手效果触发）（实际 volley={friendly.Count} 层余={warded.GetCounterCount(CardCore.Attribute.CounterRules.SpellShieldCounter)}）");

            // ---- 18. 单向属性指示物：加时回写、换区清除时反向回写 ----
            ResetField(); // 段界清场
            var buffed = Make(p2, 2, 5);
            CardCore.Attribute.CounterRules.AddStatCounter(buffed, CardCore.Attribute.CounterRules.PowerUpCounter, 3);
            CardCore.Attribute.CounterRules.AddStatCounter(buffed, CardCore.Attribute.CounterRules.LifeUpCounter, 2);
            Assert(buffed.GetPower() == 5 && buffed.GetMaxLife() == 7 && buffed.GetLife() == 7,
                   "单向属性指示物：攻击力+3层、生命值+2层（上限与当前同加）");
            core.ZoneManager.MoveCard(buffed, p2, Zone.Battlefield, Zone.Hand); // 弹回手牌 → 指示物清除
            Assert(buffed.GetCounterCount(CardCore.Attribute.CounterRules.PowerUpCounter) == 0
                   && buffed.GetCounterCount(CardCore.Attribute.CounterRules.LifeUpCounter) == 0
                   && buffed.GetPower() == 2 && buffed.GetMaxLife() == 5,
                   "换区清除：攻击力/生命值指示物离场消失（反向回写）");

            // ---- 18b. 削减类归零：生命值减少层到 0 标死（送墓交 SBA） ----
            var frail = Make(p2, 2, 3);
            CardCore.Attribute.CounterRules.AddStatCounter(frail, CardCore.Attribute.CounterRules.LifeDownCounter, 3);
            Assert(!frail.IsAlive, "生命值减少层：上限归零标死（SBA 收尸）");

            // ---- 18c. ±1/+1 与虚弱/鼓舞（同一路径：施加 ±1 层） ----
            var pmTwin = Make(p2, 2, 4); // twin 命名避让同方法既有变量
            CardCore.Attribute.CounterRules.AddStatCounter(pmTwin, CardCore.Attribute.CounterRules.MinusOneCounter, 2);
            CardCore.Attribute.CounterRules.AddStatCounter(pmTwin, CardCore.Attribute.CounterRules.PlusOneCounter, 1);
            Assert(pmTwin.GetPower() == 1 && pmTwin.GetMaxLife() == 3,
                   "±1 层对消共存：-1/-1×2 与 +1/+1×1（净 -1/-1）");

                // ---- 18d. 易损（2026-10-09 生效自减改版，同毒素档）：受到伤害每层 +1（防护层前放大），
                //      生效后层数减半（floor）；退出衰退族（无回合末倒数） ----
                var brittle = Make(p2, 2, 15);
                brittle.AddCounters(CardCore.Attribute.CounterRules.VulnerableCounter, 2);
                Attribute.KeywordRules.ApplyDamage(p1, brittle, 3, false);
                Assert(brittle.GetLife() == 10
                       && brittle.GetCounterCount(CardCore.Attribute.CounterRules.VulnerableCounter) == 1,
                       "易损：3 伤 + 2 层 = 5 伤（防护层前放大），生效后减半（2→1）");
                CardCore.Attribute.CounterRules.OnTurnEnd(p2, core.ZoneManager);
                Assert(brittle.GetCounterCount(CardCore.Attribute.CounterRules.VulnerableCounter) == 1,
                       "易损：持有者回合末不倒数（生效自减档——已退出衰退族；施加方回合末同样不结算）");
                Attribute.KeywordRules.ApplyDamage(p1, brittle, 3, false);
                Assert(brittle.GetLife() == 6
                       && brittle.GetCounterCount(CardCore.Attribute.CounterRules.VulnerableCounter) == 0,
                       "易损第二击：余 1 层 → 3 伤 +1 = 4 伤，层清零");
                Attribute.KeywordRules.ApplyDamage(p1, brittle, 3, false);
                Assert(brittle.GetLife() == 3, "易损层耗尽后裸伤（无放大）");

            // ---- 18e. 紊乱原子：附加后不能以玩家为目标（攻击与效果同口径） ----
            var dizzy = Make(p2, 2, 5);
            dizzy.AddCounters(CardCore.Attribute.KeywordRules.RushSicknessCounter, 1);
            Assert(!combat.CanAttackTarget(dizzy, p1, p1), "紊乱：持有者不能以玩家为目标（攻击侧）");
            // 持有者侧结算域：dizzy 属 p2，紊乱在 p2 回合末消退——须传持有者
            CardCore.Attribute.CounterRules.OnTurnEnd(p2, core.ZoneManager);
            Assert(dizzy.GetCounterCount(CardCore.Attribute.KeywordRules.RushSicknessCounter) == 0,
                   "紊乱：持续到回合结束消退");

            // ---- 18f. 属性赋予路由（2026-10-08 来源分轨退役）：Modify* 一律设置轨直写——
            //      生物来源也不例外；临时性（换区清层）由指示物原子族（AddPowerUp 等）显式表达 ----
            var trackA = Make(p2, 2, 5);
            var trackDefF = new EffectDefinition
            {
                Id = "VERIFY_TRACK_F",
                TriggerTiming = TriggerTiming.Activate_Active,
                Effects = new List<AtomicEffectInstance> { new AtomicEffectInstance { Type = AtomicEffectType.ModifyPower, Value = 2 } },
            };
            core.StackEngine.GetExecutor().ExecuteAsync(new EffectInstance
            {
                Definition = trackDefF,
                Source = trackA, // 生物来源（来源分轨退役——与角色来源同落设置轨）
                Controller = p2,
                Targets = new List<Entity> { trackA },
            }, skipElementCost: true).Forget();
            Assert(trackA.GetCounterCount(CardCore.Attribute.CounterRules.PowerUpCounter) == 0 && trackA.GetPower() == 4,
                   "属性路由：生物来源攻+2 同走设置轨直写（无指示物层——来源分轨 2026-10-08 退役）");

            // ---- 18g. 三轨路由·法术轨（设置类）：Source=角色 → 永久直改、跨区保留、净化不清 ----
            var trackB = Make(p2, 2, 5);
            var trackDefG = new EffectDefinition
            {
                Id = "VERIFY_TRACK_G",
                TriggerTiming = TriggerTiming.Activate_Active,
                Effects = new List<AtomicEffectInstance> { new AtomicEffectInstance { Type = AtomicEffectType.ModifyPower, Value = 3 } },
            };
            core.StackEngine.GetExecutor().ExecuteAsync(new EffectInstance
            {
                Definition = trackDefG,
                Source = p1, // 魔法卡来源（规则②：所有魔法卡的效果来源=角色）
                Controller = p1,
                Targets = new List<Entity> { trackB },
            }, skipElementCost: true).Forget();
            Assert(trackB.GetCounterCount(CardCore.Attribute.CounterRules.PowerUpCounter) == 0
                   && trackB.GetPower() == 5,
                   "三轨·法术轨：角色来源攻+3 永久直改（无任何指示物层）");
            core.ZoneManager.MoveCard(trackB, p2, Zone.Battlefield, Zone.Hand);
            Assert(trackB.GetPower() == 5,
                   "三轨·法术轨：设置类跨区保留（弹回手不清）");
            CardCore.Attribute.CounterRules.PurgeAll(trackB); // 净化口径全清指示物
            Assert(trackB.GetPower() == 5,
                   "三轨·法术轨：净化清不掉设置类属性（直改无层可回滚，视同本体）");
            var trackB2 = Make(p2, 2, 5);
            var trackDefG2 = new EffectDefinition
            {
                Id = "VERIFY_TRACK_G2",
                TriggerTiming = TriggerTiming.Activate_Active,
                Effects = new List<AtomicEffectInstance> { new AtomicEffectInstance { Type = AtomicEffectType.ModifyLife, Value = -5 } },
            };
            core.StackEngine.GetExecutor().ExecuteAsync(new EffectInstance
            {
                Definition = trackDefG2,
                Source = p1,
                Controller = p1,
                Targets = new List<Entity> { trackB2 },
            }, skipElementCost: true).Forget();
            Assert(!trackB2.IsAlive && trackB2.GetCounterCount(CardCore.Attribute.CounterRules.LifeDownCounter) == 0,
                   "三轨·法术轨：设置类生命直减归零标死（无层，交 SBA）");

            // ---- 18h. 直写跨区保留（Permanent 声明已无特殊落点——换区清层口径随来源分轨退役）----
            var trackC = Make(p2, 2, 5);
            var trackDefH = new EffectDefinition
            {
                Id = "VERIFY_TRACK_H",
                TriggerTiming = TriggerTiming.Activate_Active,
                Duration = DurationType.Permanent,
                Effects = new List<AtomicEffectInstance> { new AtomicEffectInstance { Type = AtomicEffectType.ModifyPower, Value = 2 } },
            };
            core.StackEngine.GetExecutor().ExecuteAsync(new EffectInstance
            {
                Definition = trackDefH,
                Source = trackC,
                Controller = p2,
                Targets = new List<Entity> { trackC },
            }, skipElementCost: true).Forget();
            Assert(trackC.GetCounterCount(CardCore.Attribute.CounterRules.PowerUpCounter) == 0
                   && trackC.GetPower() == 4,
                   "直写单落点：Permanent 声明同走设置轨（无指示物层——四永久层 id 已删）");
            core.ZoneManager.MoveCard(trackC, p2, Zone.Battlefield, Zone.Hand);
            Assert(trackC.GetCounterCount(CardCore.Attribute.CounterRules.PowerUpCounter) == 0
                   && trackC.GetPower() == 4 && trackC.GetMaxLife() == 5,
                   "直写单落点：跨区保留（弹回手不清——永久直改视同本体）");

            // ---- 19. 净化（2026-09-09 语义重定义：变回生物原有状态）----
            // 关键词轨矩阵：Printed（卡面本体）保留 / Setting（魔法卡赋予视同本体）保留 /
            // Temp（临时）清；指示物全清（含生效自减层）。
            var cursed = Make(p2, 3, 6, "Taunt"); // Taunt=Printed（CardWrapper 构造注入）
            cursed.AddCounters(CardCore.Attribute.CounterRules.StealthCounter, 1);       // 潜行指示物（2026-10-08 指示物化）
            cursed.AddKeyword("Indestructible", CardCore.KeywordLane.Temp);             // 临时轨（净化清）
            cursed.AddKeyword("Vigilance", CardCore.KeywordLane.Setting);               // 设置类（视同本体）
            cursed.AddCounters(CardCore.Attribute.CounterRules.ToxinCounter, 2); // 毒素统一档：无时钟（Turns 字段已随 2026-10-08 CounterSpec 改造删除）
            cursed.AddCounters(CardCore.Attribute.KeywordRules.ArmorCounter, 3);
            var purifyDef = new EffectDefinition
            {
                Id = "VERIFY_PURIFY",
                TriggerTiming = TriggerTiming.Activate_Active,
                Effects = new List<AtomicEffectInstance> { new AtomicEffectInstance { Type = AtomicEffectType.Purify } },
            };
            core.StackEngine.GetExecutor().ExecuteAsync(new EffectInstance
            {
                Definition = purifyDef,
                Source = p1,
                Controller = p1,
                Targets = new List<Entity> { cursed },
            }, skipElementCost: true).Forget();
            Assert(cursed.HasKeyword("Taunt") && cursed.HasKeyword("Vigilance")
                   && cursed.GetCounterCount(CardCore.Attribute.CounterRules.StealthCounter) == 0
                   && !cursed.HasKeyword("Indestructible"),
                   "净化轨矩阵：本体与设置类保留，临时赋予清除（潜行已指示物化随层清）");
            Assert(cursed.GetCounterCount(CardCore.Attribute.CounterRules.ToxinCounter) == 0
                   && cursed.GetCounterCount(CardCore.Attribute.KeywordRules.ArmorCounter) == 0,
                   "净化：全部指示物清空（含永久类，属性层反向回写）");

            // ---- 19b. 换区清除：Temp 关键词轨换区清、Printed 留；生效自减指示物（Exception）换区不清 ----
            var zoneKw = Make(p2, 2, 4, "Taunt");
            zoneKw.AddKeyword("Vigilance", CardCore.KeywordLane.Temp); // 临时轨占位
            zoneKw.AddCounters(CardCore.Attribute.CounterRules.StealthCounter, 1); // 潜行指示物层（生效自减档）
            zoneKw.AddKeyword("FirstStrike", CardCore.KeywordLane.Setting); // 设置轨（视同本体，换区不清）
            core.ZoneManager.MoveCard(zoneKw, p2, Zone.Battlefield, Zone.Hand);
            Assert(!zoneKw.HasKeyword("Vigilance")
                   && zoneKw.GetCounterCount(CardCore.Attribute.CounterRules.StealthCounter) == 1
                   && zoneKw.HasKeyword("Taunt") && zoneKw.HasKeyword("FirstStrike"),
                   "换区清除：Temp 轨清除；潜行指示物层换区不清（生效自减档·2026-10-08 晚补裁决）；"
                   + "Printed 与 Setting（换区不清档）保留");

            // ---- 19c.（已删 2026-10-08：生物轨永久档属性层随四永久 id 退役——换区保留语义并入 18g 设置轨锚） ----

            // ---- 19d. 入场刷新（2026-09-09 定案；2026-10-08 圣盾/复生/潜行指示物化）：真实入场补回
            //      被消耗的卡面消耗项（关键词走 Printed 差集、指示物走补 1 层）；
            //      复生（原地留场）与控制权变更（容器直移）不经统一出口，天然不触发 ----
            var refreshShield = Make(p2, 2, 5, CardCore.Attribute.CounterRules.DivineShieldCounter);
            CardCore.Attribute.KeywordRules.ApplyDamage(p1, refreshShield, 1, false);
            Assert(refreshShield.GetCounterCount(CardCore.Attribute.CounterRules.DivineShieldCounter) == 0
                   && refreshShield.GetLife() == 5,
                   "入场刷新前置：圣盾挡一次伤害后消耗（层数归零、伤害被完全挡下）");
            core.ZoneManager.MoveCard(refreshShield, p2, Zone.Battlefield, Zone.Hand); // 弹回手
            core.ZoneManager.TryAddToBattlefield(refreshShield, p2);                   // 真实入场（统一出口）
            Assert(refreshShield.GetCounterCount(CardCore.Attribute.CounterRules.DivineShieldCounter) == 1,
                   "入场刷新：弹回重打补回被消耗的卡面圣盾（印刷路径=补 1 层指示物）");

            var refreshReborn = Make(p2, 2, 5, CardCore.Attribute.CounterRules.RebornCounter);
            CardCore.Attribute.DeathRules.TryKill(refreshReborn, CardCore.Attribute.DeathCause.DamageLethal, p1, core.ZoneManager);
            Assert(refreshReborn.IsAlive && refreshReborn.GetLife() == 1
                   && refreshReborn.GetCounterCount(CardCore.Attribute.CounterRules.RebornCounter) == 0,
                   "复生不触发刷新：死亡替代原地留场（不经入场口），消耗后不自我补回（无无限复生）");

            var refreshStolen = Make(p1, 2, 5, CardCore.Attribute.CounterRules.DivineShieldCounter);
            CardCore.Attribute.KeywordRules.ApplyDamage(p2, refreshStolen, 1, false); // 消耗圣盾
            new CardCore.Attribute.Handlers.GainControlHandler().Execute(
                new CardCore.AtomicEffectInstance { Type = CardCore.AtomicEffectType.GainControl },
                new CardCore.EffectExecutionContext
                {
                    Source = p2, Controller = p2,
                    Targets = new List<Entity> { refreshStolen },
                    ZoneManager = core.ZoneManager
                });
            Assert(refreshStolen.GetCounterCount(CardCore.Attribute.CounterRules.DivineShieldCounter) == 0,
                   "控制权变更不触发刷新：场内迁移（容器直移+补发事件）不补回消耗项（偷取不白得圣盾）");

            // ---- 20. 沉默指示物：持有者不可发动主动效果（主动能力路径） ----
            var silenced = Make(p1, 2, 5);
            var activatable = new EffectDefinition
            {
                Id = "VERIFY_SILENCE_ABILITY",
                TriggerTiming = TriggerTiming.Activate_Active,
                ActivationType = EffectActivationType.Voluntary,
                Effects = new List<AtomicEffectInstance> { new AtomicEffectInstance { Type = AtomicEffectType.Tap } },
            };
            var executor = core.StackEngine.GetExecutor();
            bool beforeSilence = executor.CanActivate(activatable, silenced, p1, p1, PhaseType.Main, 1);
            silenced.AddCounters(CardCore.Attribute.CounterRules.SilenceCounter, 1);
            bool afterSilence = executor.CanActivate(activatable, silenced, p1, p1, PhaseType.Main, 1);
            Assert(beforeSilence && !afterSilence,
                   "沉默：持有者主动能力被封锁（出牌不受限——PlayCard 不经此门）");

            // ---- 20b. 无效指示物：拦全部触发式（事件匹配后、上栈前——含 OnTakeDamage 族） ----
            var nullified = Make(p2, 2, 9);
            var nullTrigDef = new EffectDefinition
            {
                Id = "VERIFY_NULLIFY_TRIG",
                TriggerTiming = TriggerTiming.OnTakeDamage,
                // 裸构造必补：ActivationType 默认 Voluntary 会进「玩家自愿」待发队列，
                // DrainStack 只自动泵 Automatic（converter 按时点补的正是它）；触发式费用已含卡价
                ActivationType = EffectActivationType.Automatic,
                ElementCostPrepaid = true,
                Effects = new List<AtomicEffectInstance> { new AtomicEffectInstance { Type = AtomicEffectType.DrawCard, Value = 1 } },
            };
            core.TriggerEngine.RegisterEffect(nullTrigDef, nullified, p2);
            nullified.AddCounters(CardCore.Attribute.CounterRules.NullifyCounter, 1, p1);
            int p2HandAtNullify = core.ZoneManager.GetCards(p2, Zone.Hand).Count;
            CardCore.Attribute.KeywordRules.ApplyDamage(p1, nullified, 1, false);
            GameActions.DrainStack(core);
            Assert(core.ZoneManager.GetCards(p2, Zone.Hand).Count == p2HandAtNullify,
                   "无效：非主动（触发式）被拦——受击触发不上栈");
            // 对照：无效消退后同一触发照常上栈结算
            nullified.RemoveCounters(CardCore.Attribute.CounterRules.NullifyCounter, 1);
            CardCore.Attribute.KeywordRules.ApplyDamage(p1, nullified, 1, false);
            GameActions.DrainStack(core);
            Assert(core.ZoneManager.GetCards(p2, Zone.Hand).Count == p2HandAtNullify + 1,
                   "无效消退后：同一触发照常上栈（对照锚，证明 20b 拦截来自无效指示物本身）");

            // ---- 20c. 无效不拦主动（与沉默对照：沉默拦主动、无效拦非主动，互不重叠） ----
            var nullActivated = Make(p1, 2, 5);
            var nullActDef = new EffectDefinition
            {
                Id = "VERIFY_NULLIFY_ACT",
                TriggerTiming = TriggerTiming.Activate_Active,
                ActivationType = EffectActivationType.Voluntary,
                Effects = new List<AtomicEffectInstance> { new AtomicEffectInstance { Type = AtomicEffectType.Tap } },
            };
            nullActivated.AddCounters(CardCore.Attribute.CounterRules.NullifyCounter, 1);
            bool canActUnderNullify = executor.CanActivate(nullActDef, nullActivated, p1, p1, PhaseType.Main, 1);
            Assert(canActUnderNullify,
                   "无效：主动照常可发动（拦截面与沉默互补不重叠）");

            // ---- 20d. 无效不拦伤害管线被动（坚韧/圣盾等非「能力发动」；回合维护再生同口径） ----
            var nullTough = Make(p2, 3, 8);
            nullTough.AddCounters(CardCore.Attribute.CounterRules.ToughnessCounter, 1); // 坚韧 1 层
            nullTough.AddCounters(CardCore.Attribute.CounterRules.NullifyCounter, 1);
            CardCore.Attribute.KeywordRules.ApplyDamage(p1, nullTough, 3, false);
            Assert(nullTough.GetLife() == 6,
                   "无效：伤害管线被动照常（坚韧减伤不受无效影响）");

            // ---- 21. 摧毁：无生命值单位（地牌）出池直送墓地，不走死亡决策表 ----
            Card landTarget = null;
            var p2Pooled = core.ElementPool.GetPooledCards(p2);
            if (p2Pooled.Count > 0)
            {
                landTarget = p2Pooled[0].SourceCard;
            }
            else
            {
                var p2Creature = core.ZoneManager.GetCards(p2, Zone.Hand)
                    .FirstOrDefault(c => CardCore.ElementPoolSystem.CanServeAsLand(c));
                if (p2Creature != null && GameActions.AddToElementPool(core, p2, p2Creature))
                    landTarget = p2Creature;
            }
            if (landTarget != null)
            {
                int graveBefore = core.ZoneManager.GetCards(p2, Zone.Graveyard).Count;
                var smashDef = new EffectDefinition
                {
                    Id = "VERIFY_SMASH",
                    TriggerTiming = TriggerTiming.Activate_Active,
                    Effects = new List<AtomicEffectInstance> { new AtomicEffectInstance { Type = AtomicEffectType.Smash } },
                };
                executor.ExecuteAsync(new EffectInstance
                {
                    Definition = smashDef,
                    Source = p1,
                    Controller = p1,
                    Targets = new List<Entity> { landTarget },
                }, skipElementCost: true).Forget();
                Assert(core.ElementPool.GetPooledCards(p2).All(pc => pc.SourceCard != landTarget)
                       && core.ZoneManager.GetCards(p2, Zone.Graveyard).Count == graveBefore + 1,
                       "摧毁：地牌出池直送墓地（2026-10-11：地牌本就不耗竭——回收后可再入池重选锁色）");
            }
            else
            {
                Debug.LogWarning("[Verify] 跳过摧毁段：p2 无可用地牌（池空且手牌无可入池生物）");
            }

            // ---- 21b. 摧毁×不灭（2026-10-09 三口裁定：剧毒/消灭/摧毁全可被不灭拦）----
            var enchantData = new CardData
            {
                ID = "VERIFY_SMASH_ENCH", CardName = "摧毁标的结界", Supertype = Cardtype.Enchantment, Power = 0, Life = 0,
            };
            var normalEnchant = new CardWrapper(enchantData);
            normalEnchant.SetController(p2);
            Assert(core.ZoneManager.TryAddToBattlefield(normalEnchant, p2), "摧毁段：普通结界入场");

            var rockEnchantData = new CardData
            {
                ID = "VERIFY_SMASH_ROCK", CardName = "不灭结界", Supertype = Cardtype.Enchantment, Power = 0, Life = 0,
            };
            rockEnchantData.Keywords.Add(CardCore.Attribute.KeywordRules.Indestructible);
            var rockEnchant = new CardWrapper(rockEnchantData);
            rockEnchant.SetController(p2);
            Assert(core.ZoneManager.TryAddToBattlefield(rockEnchant, p2), "摧毁段：不灭结界入场");

            var smashDef2 = new EffectDefinition
            {
                Id = "VERIFY_SMASH_INDESTRUCTIBLE",
                TriggerTiming = TriggerTiming.Activate_Active,
                Effects = new List<AtomicEffectInstance> { new AtomicEffectInstance { Type = AtomicEffectType.Smash } },
            };
            executor.ExecuteAsync(new EffectInstance
            {
                Definition = smashDef2,
                Source = p1,
                Controller = p1,
                Targets = new List<Entity> { normalEnchant, rockEnchant },
            }, skipElementCost: true).Forget();
            Assert(core.ZoneManager.IsCardInZone(normalEnchant, p2, Zone.Graveyard),
                   "摧毁：普通无生命单位直送墓");
            Assert(rockEnchant.IsAlive && core.ZoneManager.IsCardInZone(rockEnchant, p2, Zone.Battlefield),
                   "摧毁×不灭：不灭无生命单位免疫摧毁（三口同拦裁定）");
            RetireCards(core, p2, rockEnchant);

            // ---- 22. 费用指示物端到端：减费作用于预检与付费全程；层在发动区存活；结算后清除 ----
            EnsureMainPhase(core, p1);
            CardData GraySpellData(int gray, string id, string name)
            {
                var d = new CardData { ID = id, CardName = name };
                d.Supertype = Cardtype.Spell;
                d.Cost[(int)ManaType.Gray] = gray;
                return d;
            }
            var cheap = InjectCard(core, p1, GraySpellData(3, "VERIFY_COST_GRAY3", "灰费验证"));
            var pool1 = core.ElementPool.GetPool(p1);

            // 基线：无层原价 灰3，bank 灰1 → 声明预检拦截（灰账单可由 黑/白 垫付，须清全色——根因 A 修复）
            HygieneBank(pool1, ManaType.Gray, 1);
            Assert(!GameActions.PlayCard(core, p1, cheap), "费用基线：灰3 卡在 bank 灰1 下声明被拒（原价）");

            // 减 2 层 → 有效费 灰1：声明过（预检按层后费）+ cast 付费按层后费扣（层在发动区存活）
            CardCore.Attribute.CounterRules.AddStatCounter(cheap, CardCore.Attribute.CounterRules.CostDownCounter, 2);
            Assert(PlayCardSync(core, p1, cheap), "减费：灰3 − 2层 = 灰1，声明通过");
            Assert(pool1.TempElements[ManaType.Gray] == 0,
                   "减费：cast 付费按层后费扣 1（进发动区不清层——发动区豁免）");
            Assert(core.ZoneManager.GetCards(p1, Zone.Graveyard).Contains(cheap), "减费卡（空效果法术）结算后入墓");
            Assert(cheap.GetCounterCount(CardCore.Attribute.CounterRules.CostDownCounter) == 0,
                   "费用层：入墓（真实去向）清除");

            // 增 2 层：灰1 + 2层 = 灰3，bank 灰2 拒、灰3 过
            var pricey = InjectCard(core, p1, GraySpellData(1, "VERIFY_COST_GRAY1", "增费验证"));
            CardCore.Attribute.CounterRules.AddStatCounter(pricey, CardCore.Attribute.CounterRules.CostUpCounter, 2);
            HygieneBank(pool1, ManaType.Gray, 2);
            Assert(!GameActions.PlayCard(core, p1, pricey), "增费：灰1 + 2层 = 灰3，bank 灰2 拒");
            HygieneBank(pool1, ManaType.Gray, 3);
            Assert(PlayCardSync(core, p1, pricey), "增费：bank 灰3 过（层后费全额支付）");

            // ---- 23. 再生·角色侧（2026-10-08 引擎统一）：回合玩家角色准备阶段恢复全部生命 ----
            // 物化授予（Setting 轨）：光环通道被 NoRole 门拦（再生=仅生物行），本段锚物化通道行为。
            p2.AddKeyword(CardCore.Attribute.KeywordRules.Regeneration, CardCore.KeywordLane.Setting, p2);
            Assert(p2.HasKeyword(CardCore.Attribute.KeywordRules.Regeneration), "再生·角色侧：物化授予落到角色");
            int p2Full = p2.GetMaxLife();
            p2.Life = p2Full - 4;
            Assert(EndTurnPumped(core, core.TurnEngine.TurnPlayer), "推进回合（再生·角色侧结算窗）");
            if (core.TurnEngine.TurnPlayer != p2) // 推进后仍是 p1 回合 → 再推一次到 p2 回合开始
                Assert(EndTurnPumped(core, core.TurnEngine.TurnPlayer), "再推进到 p2 回合开始");
            Assert(p2.Life == p2Full,
                   $"再生·角色侧：p2 回合开始恢复全部生命（与卡侧同口径；NoRole 门只拦光环不拦物化）——实际 {p2.Life}/{p2Full}");
            p2.RemoveKeyword(CardCore.Attribute.KeywordRules.Regeneration); // 卫生：不泄漏到后续段

            CleanKeywords();
        }

        /// <summary>推进回合直到 p1 处于主阶段（验证器节奏：EndTurn 折返后 SkipElementPool 入主阶段）。</summary>
        private static void EnsureMainPhase(GameCore core, Player p1)
        {
            for (int i = 0; i < 6; i++)
            {
                if (core.TurnEngine.TurnPlayer == p1 && core.TurnEngine.CurrentPhase?.Phase == PhaseType.Main)
                    return;
                var tp = core.TurnEngine.TurnPlayer;
                if (core.TurnEngine.CurrentPhase?.Phase == PhaseType.Standby)
                    GameActions.SkipElementPool(core, tp);
                else
                    EndTurnPumped(core, tp);
            }
        }

        /// <summary>注入一张合成单色 1 费生物（竞速颜色断言的用卡载体）。</summary>
        private static Card InjectColoredCreature(GameCore core, Player owner, ManaType color)
        {
            var data = new CardData { ID = "VERIFY_RITUAL_" + color, CardName = "竞速色卡" + color };
            data.Supertype = Cardtype.Creature;
            data.Power = 1;
            data.Life = 1;
            data.Cost[color] = 1;
            return InjectCard(core, owner, data);
        }

        /// <summary>
        /// 结束回合并驱动折返：编辑器验证上下文没有帧泵（GameLoopController.Update 无人调用），
        /// EndTurn 只推进到结束阶段；手动补一次 CheckPhaseTransition 完成 End→Standby 折返回合切换。
        /// </summary>
        private static bool EndTurnPumped(GameCore core, Player player)
        {
            if (!GameActions.EndTurn(core, player)) return false;
            core.TurnEngine.CheckPhaseTransition();   // End(Active) → 折返 StartNewTurn(下一位)
            return true;
        }

        /// <summary>
        /// PlayCard + 排干栈（验证器同步快进）：出牌即上栈（使用时点声明，不付费），
        /// 双 Pass 让 cast 立即结算——付费、效果、离区在断言前全部完成。
        /// 响应窗口本身的交互（打落/发动无效）见 TestCounterWindow。
        /// </summary>
        private static bool PlayCardSync(GameCore core, Player player, Card card,
            List<Entity> targets = null, Zone fromZone = Zone.Hand, int modeIndex = 0)
        {
            if (!GameActions.PlayCard(core, player, card, targets, fromZone, modeIndex))
                return false;
            GameActions.DrainStack(core);
            return true;
        }

        /// <summary>把 CardData 包成实例注入指定玩家手牌。</summary>
        private static Card InjectCard(GameCore core, Player owner, CardData data)
        {
            var card = new CardWrapper(data);
            card.SetController(owner);
            core.ZoneManager.GetZoneContainer(owner).Add(card, Zone.Hand);
            return card;
        }

        /// <summary>
        /// 对目标执行一次"消灭"裁决（直连死亡决策表，DestroyEffect 死因）。
        /// 毁灭原子已删除（2026-09-03）——本 helper 直连同路径。
        /// </summary>
        private static void DestroyViaEffect(GameCore core, Player actor, Card target)
        {
            CardCore.Attribute.DeathRules.TryKill(target, CardCore.Attribute.DeathCause.DestroyEffect, actor, core.ZoneManager);
        }

        // ======================================== 统一计价锚点（规则一·平衡） ========================================

        /// <summary>
        /// 统一计价锚点验证（纯函数，不依赖卡表/对局）：
        /// 表值（GrantTaunt White / d(C) 分段曲线）、身材 1费=2属性、法术同折、回2命=0费、
        /// 卡层组合费用（可支配底盘 2026-10-10：超槽加价+剩余抵价/提速/结界二向——原「选发额外折」
        /// 与「剩余作废」均已废）、构筑代价抵消（无"超模"态）、关键词同享 d(C)、缺省档位 Ĉ。
        /// </summary>
        // ======================================== 合成器模型（2026-09-14 合成器重做） ========================================

        /// <summary>
        /// 合成器重做模型断言（纯模型层——UI 交互留 Unity 手测；2026-10-05 两槽定案重锚）：
        /// ①MountKind 8 值重排 + 引擎参数/预算锚（引擎=槽级 Branch 载荷，主干行已删）；
        /// ②挂载位数据驱动（位 6 光环/位 7 系统内部/生物·法术可赋予行=TargetFilter 语义）；
        /// ③槽级载荷端到端（settle=3 引擎：Then 转存/零计价/倒计时换算——合成器产出形态=TestBranchEngines 数据形态）；
        /// ④条件目录三族（产出条件/局面门/引擎）与预算口径；⑤并列保序（Steps 平铺折叠）；
        /// ⑥描述动态渲染（值随机区间文本）；⑦哈希口径（amp/br{} 载荷段参与去重）。
        /// </summary>
        private static void TestComposerModel()
        {
            // ---- 1. MountKind 值域（2026-10-07 深夜终版：位 7 仅可删除；2026-10-23 有限分支入表：位 10 新增）----
            Assert(System.Linq.Enumerable.Range(0, 7).All(v => System.Enum.IsDefined(typeof(CardCore.MountKind), v))
                   && System.Enum.IsDefined(typeof(CardCore.MountKind), 8)
                   && System.Enum.IsDefined(typeof(CardCore.MountKind), 10)
                   && !System.Enum.IsDefined(typeof(CardCore.MountKind), 7)
                   && !System.Enum.IsDefined(typeof(CardCore.MountKind), 9),
                   "MountKind 值域 0..6+8+10（2026-10-23：位 10=有限分支门槛行；位 7/9 空缺防旧数字数据误读）");
            Assert((int)CardCore.MountKind.Keyword == 0
                   && (int)CardCore.MountKind.Counter == 1
                   && (int)CardCore.MountKind.NoRandom == 2
                   && (int)CardCore.MountKind.LinkAura == 3
                   && (int)CardCore.MountKind.SystemInternal == 4
                   && (int)CardCore.MountKind.EngineTrunk == 5
                   && (int)CardCore.MountKind.RuleAura == 6
                   && (int)CardCore.MountKind.NoLinkAura == 8
                   && (int)CardCore.MountKind.SituationGate == 10,
                   "MountKind 位锚：Keyword=0/Counter=1/NoRandom=2/LinkAura=3/SystemInternal=4/EngineTrunk=5/RuleAura=6/NoLinkAura=8/SituationGate=10（7/9 空缺）");
            var dualAuraKinds = CardCore.MountKindExtensions.ParseCsv("关键词,可以作为连接光环");
            var noAuraKinds = CardCore.MountKindExtensions.ParseCsv("不可作为连接光环");
            Assert(dualAuraKinds.Count == 2
                   && dualAuraKinds.Contains(CardCore.MountKind.Keyword)
                   && dualAuraKinds.Contains(CardCore.MountKind.LinkAura)
                   && noAuraKinds.Count == 1
                   && noAuraKinds.Contains(CardCore.MountKind.NoLinkAura)
                   && CardCore.MountKindExtensions.ParseCsv("8").Count == 1
                   && CardCore.MountKindExtensions.ParseCsv("8").Contains(CardCore.MountKind.NoLinkAura)
                   && CardCore.MountKindExtensions.ParseCsv("7").Count == 0
                   && CardCore.MountKindExtensions.ParseCsv("9").Count == 0
                   && CardCore.MountKindExtensions.ParseCsv("有限分支").Count == 1
                   && CardCore.MountKindExtensions.ParseCsv("有限分支").Contains(CardCore.MountKind.SituationGate),
                   "MountKinds 词表：可（3）/不可（8）中文与数字双轨解析通过；位 7/9 空缺解析为空集（数字轨兼容）；有限分支（10）命中新位");
            // 引擎参数范围与奖励预算（2026-10-09 自平衡统一：全引擎预算 -1——门槛=Then 锚价推导，
            // 「死亡计数/元素充盈 预算=x」旧制退役；手填参数=运势点数线/倒计时/团结 x/附加诅咒·祝福张数）
            CardCore.ComposerCatalog.EngineParamRange(CardCore.BranchEngineKind.DeathToll, out int rMin, out int rMax);
            Assert(rMin == 0 && rMax == 99, "引擎参数范围：死亡计数区间退役落 default [0,99]（派生门槛引擎无手填参数）");
            CardCore.ComposerCatalog.EngineParamRange(CardCore.BranchEngineKind.LuckRoll, out int rMin2, out int rMax2);
            Assert(rMin2 == 1 && rMax2 == 6, "引擎参数范围：运势 x∈[1,6]（双骰≥x；x=6 双 6 才中=1/36）");
            CardCore.ComposerCatalog.EngineParamRange(CardCore.BranchEngineKind.Unity, out int rMin3, out int rMax3);
            Assert(rMin3 == 1 && rMax3 == 6, "引擎参数范围：团结 x∈[1,6] 手填=所需相邻单位数（2026-10-20 效果序位换轴）");
            Assert(CardCore.ComposerCatalog.EngineRewardBudget(CardCore.BranchEngineKind.DeathToll, 3) == -1
                   && CardCore.ComposerCatalog.EngineRewardBudget(CardCore.BranchEngineKind.ManaSurplus, 2) == -1
                   && CardCore.ComposerCatalog.EngineRewardBudget(CardCore.BranchEngineKind.Unity, 2) == -1
                   && CardCore.ComposerCatalog.EngineRewardBudget(CardCore.BranchEngineKind.Clash, 3) == -1
                   && CardCore.ComposerCatalog.EngineRewardBudget(CardCore.BranchEngineKind.Countdown, 0) == -1
                   && CardCore.ComposerCatalog.EngineRewardBudget(CardCore.BranchEngineKind.LuckRoll, 1) == -1,
                   "引擎奖励预算：全引擎 -1 自平衡（可实现上限由 UI 侧 RewardFilterCap 封顶 5）");
            Assert(CardCore.ComposerCatalog.IsDerivedThresholdEngine(CardCore.BranchEngineKind.Clash)
                   && CardCore.ComposerCatalog.IsDerivedThresholdEngine(CardCore.BranchEngineKind.DeathToll)
                   && CardCore.ComposerCatalog.IsDerivedThresholdEngine(CardCore.BranchEngineKind.ManaSurplus)
                   && !CardCore.ComposerCatalog.IsDerivedThresholdEngine(CardCore.BranchEngineKind.Unity)
                   && !CardCore.ComposerCatalog.IsDerivedThresholdEngine(CardCore.BranchEngineKind.LuckRoll)
                   && !CardCore.ComposerCatalog.IsDerivedThresholdEngine(CardCore.BranchEngineKind.Countdown)
                   && !CardCore.ComposerCatalog.IsDerivedThresholdEngine(CardCore.BranchEngineKind.CurseOnDraw)
                   && !CardCore.ComposerCatalog.IsDerivedThresholdEngine(CardCore.BranchEngineKind.BlessingOnDraw),
                   "派生门槛引擎单源：拼点/死亡计数/元素充盈（门槛=Then 锚价，无手填参数）；"
                   + "团结/运势/倒计时/附加诅咒/附加祝福仍手填（团结 x∈[1,6] 门槛=x）");
            Assert(CardCore.CostDerivationService.RewardThreshold(new List<CardCore.AtomicEffectInstance>
                       { CardCore.CardEffectConverter.ConvertAtomForUI(AtomRefs.New(CardCore.AtomicEffectType.DrawCard, value: 1)) }) == 2
                   && CardCore.CostDerivationService.RewardThreshold(null) == 1,
                   "派生门槛公式：DrawCard(1) 锚价 2 → 门槛 2；无奖励下限 1（随计费变）");

            // ---- 1b. MountKinds 数据驱动（2026-10-07 晚终版：关键词默认可−消耗型拉黑；位3=属性变更；位7=光环专属）----
            // 光环关键词资格：关键词**默认可**（隐密无移除口=持续型，默认可）；真消耗型已全部指示物化
            //（2026-10-08 圣盾/潜行/复生、2026-10-09 法术护盾收官——改位 1 指示物行，指示物行硬拒光环挂载，
            // 目录不收），关键词族位 8 拉黑现值仅守护/再生/禁魔石
            Assert(!CardCore.ComposerCatalog.IsAuraMountableKeyword("DivineShield")
                   && !CardCore.ComposerCatalog.IsAuraMountableKeyword("Stealth")
                   && !CardCore.ComposerCatalog.IsAuraMountableKeyword("Reborn")
                   && !CardCore.ComposerCatalog.IsAuraMountableKeyword("SpellShield")
                   && !CardCore.ComposerCatalog.IsAuraMountableKeyword("Guardian"),
                   "光环可挂判定：圣盾/潜行/复生/法术护盾（已指示物化，目录不收）+守护（位 8 拉黑）不可挂");
            Assert(CardCore.ComposerCatalog.IsAuraMountableKeyword("Lifesteal")
                   && CardCore.ComposerCatalog.IsAuraMountableKeyword("Taunt")
                   && CardCore.ComposerCatalog.IsAuraMountableKeyword("Concealed")
                   && CardCore.ComposerCatalog.IsAuraMountableKeyword("DoubleStrike"),
                   "光环可挂判定（2026-10-07 深夜）：吸血/帷幕/隐密/连击（关键词默认可）可挂"
                   + "（坚韧已指示物化、守护已配对制拉黑——2026-10-08 双双退出，引用随摘）");
            // 光环条目资格数据（2026-10-07 深夜终版）：关键词行默认可；
            // 2026-10-08 圣盾/潜行/复生指示物化——三行退出关键词族改位 1 指示物（硬拒光环），
            // 位 8 黑名单构成归并行表改面（名单在表漂移中，本锚只钉三行不在黑名单）
            var grantRows = CardCore.Attribute.AtomicEffectTable.GetAll()
                .Where(r => r != null && !string.IsNullOrEmpty(r.EnumName) && r.EnumName.StartsWith("Grant")).ToList();
            Assert(grantRows.Where(r => CardCore.ComposerCatalog.HasMountBit(r, CardCore.MountKind.Keyword)
                                         && !CardCore.ComposerCatalog.HasMountBit(r, CardCore.MountKind.NoLinkAura))
                            .All(r => CardCore.ComposerCatalog.CanMountAsAura(r))
                   && grantRows.Where(r => CardCore.ComposerCatalog.HasMountBit(r, CardCore.MountKind.NoLinkAura))
                               .All(r => !CardCore.ComposerCatalog.CanMountAsAura(r)),
                   "默认翻转：关键词行（未拉黑）全部可作光环；拉黑行全部不可（CanMountAsAura 统一判定）");
            var convertedAuraExits = new[] { "GrantStealth", "GrantDivineShield", "GrantReborn", "GrantSpellShield" }
                .Select(n => CardCore.Attribute.AtomicEffectTable.GetByEnumName(n)).ToList();
            Assert(convertedAuraExits.All(r => r != null)
                   && convertedAuraExits.All(r => !CardCore.ComposerCatalog.HasMountBit(r, CardCore.MountKind.NoLinkAura))
                   && convertedAuraExits.All(r => !CardCore.ComposerCatalog.HasMountBit(r, CardCore.MountKind.Keyword))
                   && convertedAuraExits.All(r => CardCore.ComposerCatalog.HasMountBit(r, CardCore.MountKind.Counter)),
                   "圣盾/复生/潜行（2026-10-08）/法术护盾（2026-10-09）指示物化：四行退出关键词族（无位 0/位 8）改位 1 指示物——光环挂载由指示物位硬拒");
            var guardianKwRow = CardCore.Attribute.AtomicEffectTable.GetByEnumName("GrantGuardian");
            Assert(guardianKwRow != null
                   && CardCore.ComposerCatalog.HasMountBit(guardianKwRow, CardCore.MountKind.Keyword)
                   && CardCore.ComposerCatalog.HasMountBit(guardianKwRow, CardCore.MountKind.NoLinkAura)
                   && !CardCore.ComposerCatalog.CanMountAsAura(guardianKwRow),
                   "守护配对制：位 0 关键词 + 位 8 拉黑（退出连接光环族——登场选目标与 live-query 语义冲突）");
            // 2026-10-09 清理：位 3 光环化源断言删除——AddPlusOne/MinusOne 行已随 2026-10-08 永久属性档
            // 退役改名（AddPermanentPlusOne/MinusOne），断言钉旧行名与 10-07 位分类，随表五类重排过时
            // 生物/法术可赋予行（原挂载位 5/6 语义迁 TargetFilter token——2026-10-05；2026-10-07 回响改普通效果、
            // Spell token 退役——IsSpellGrantRow 删除）
            // ---- 1b-2. 派生挂载资格（2026-10-07 位 0/3 删除后）+ 四类仅可锚 ----
            // 规则光环行单位互斥（仅此一位）且无派生主干资格——总数归表改面漂移，不锚（表位锚勿硬编码总数）
            var ruleRows = CardCore.Attribute.AtomicEffectTable.GetAll()
                .Where(r => r != null && CardCore.ComposerCatalog.HasMountBit(r, CardCore.MountKind.RuleAura)).ToList();
            Assert(ruleRows.Count >= 1
                   && ruleRows.All(r => CardCore.MountKindExtensions.ParseCsv(r.MountKinds).Count == 1)
                   && ruleRows.All(r => !CardCore.ComposerCatalog.CanBeTrunkRow(r)),
                   "规则光环行仅声明单位且无派生主干资格（仅可作为规则光环；总数随表漂移不锚）");
            // 守护（2026-10-08 配对制改版）：关键词行 + 派生主干资格（可授予）
            // + TargetFilter 开放（结界可宿主——非生物载体挂它仍生效；坚韧行已指示物化归并行会话面，引用随摘）
            var guardianRow = CardCore.Attribute.AtomicEffectTable.GetByEnumName("GrantGuardian");
            Assert(guardianRow != null
                   && CardCore.ComposerCatalog.HasMountBit(guardianRow, CardCore.MountKind.Keyword)
                   && CardCore.ComposerCatalog.CanBeTrunkRow(guardianRow)
                   && !CardCore.ComposerCatalog.IsCreatureGrantRow(guardianRow),
                   "守护关键词化：位 0 + 派生主干资格（可授予），TargetFilter 无 NoRole（结界可宿主）");
            Assert(CardCore.ComposerCatalog.IsCreatureGrantRow(
                       CardCore.Attribute.AtomicEffectTable.GetByEnumName("GrantTaunt")),
                   "生物侧可赋予：Grant 行 TargetFilter 含 NoRole（仅生物）");
            // 角色通道（2026-10-09 裁定）：关键词光环一律可作用角色——不能用光环表达的行由表
            // 「不可作为连接光环」位拉黑（守护/再生/禁魔石：配对制/角色回满太强/角色免疫洞），
            // 通道不再设防（RoleChannelBlocked 与逐条目 role 声明整体退役；属性条目仍恒仅生物）。
            Assert(!CardCore.ComposerCatalog.IsAuraMountableKeyword(CardCore.Attribute.KeywordRules.Regeneration)
                   && !CardCore.ComposerCatalog.IsAuraMountableKeyword(CardCore.Attribute.KeywordRules.Spellban)
                   && !CardCore.ComposerCatalog.IsAuraMountableKeyword(CardCore.Attribute.KeywordRules.Guardian),
                   "光环黑名单：再生/禁魔石/守护不可作光环（角色通道开放的边界——限制单源于表拉黑位）");
            // 回响行（2026-10-07 关键词→普通效果改版）：EchoCopy 普通行——退出 Grant 族（关键词目录不再合成）、
            // 无 Spell token、无挂载位声明（主干资格派生）。表价/表色不钉（单一来源，随表漂移——蓝2→蓝1 已历一改）
            var echoRow = CardCore.Attribute.AtomicEffectTable.GetByEnumName("EchoCopy");
            Assert(echoRow != null && !echoRow.EnumName.StartsWith("Grant")
                   && string.IsNullOrEmpty(echoRow.MountKinds)
                   && !CardCore.ComposerCatalog.HasFilterToken(echoRow, "Spell")
                   && CardCore.ComposerCatalog.CanBeTrunkRow(echoRow),
                   "回响行改版：EchoCopy 普通效果行（无挂载位声明、派生主干资格、Spell token 退役）");
            // 指示物行（2026-10-08 圣盾/复生/潜行指示物化入组；域随表改面漂移（存储态=自己已退役，
            // 法术护盾含无生命域）——只钉「位 1 指示物 + 授予域非空」，具体域不锚（表为单一来源））
            var convertedRows = new[] { "GrantDivineShield", "GrantReborn", "GrantStealth", "GrantSpellShield" }
                .Select(n => CardCore.Attribute.AtomicEffectTable.GetByEnumName(n)).ToList();
            Assert(convertedRows.All(r => r != null)
                   && convertedRows.All(r => CardCore.ComposerCatalog.HasMountBit(r, CardCore.MountKind.Counter))
                   && convertedRows.All(r =>
                   {
                       var k = r.GetTargetKindList();
                       return k != null && k.Count >= 1;
                   }),
                   "圣盾/复生/潜行/法术护盾指示物行：位 1 指示物 + 授予域非空（授予域由合成器覆写收窄）");
            // 光环关键词下拉数据源：位 6 行（不含消耗型；坚韧/圣盾/复生/潜行/法术护盾已指示物化、守护已配对制拉黑）
            var auraChoices = CardCore.ComposerCatalog.AuraKeywordChoices();
            Assert(!auraChoices.Any(c => c.id == "Armor") && !auraChoices.Any(c => c.id == "Guardian")
                   && !auraChoices.Any(c => c.id == "DivineShield" || c.id == "Stealth" || c.id == "Reborn" || c.id == "SpellShield"),
                   "光环关键词下拉：坚韧/圣盾/复生/潜行/法术护盾（指示物化）/守护（配对制拉黑）均不在列");

            // ---- 1c. 不可随机黑名单（2026-10-07 位 4 翻转；总数与「含 {value}」性质均归表改面漂移——
            // 系统行等无滑条行已入黑名单，{value} 不变量已废；只钉非空性质+下方七行白名单）；
            // 七个可随机行（伤害族4+回复生命+紊乱+沉睡）不标——默认可随机 ----
            var noRandomRows = CardCore.Attribute.AtomicEffectTable.GetAll()
                .Where(r => r != null && CardCore.ComposerCatalog.HasMountBit(r, CardCore.MountKind.NoRandom)).ToList();
            Assert(noRandomRows.Count >= 1,
                   $"不可随机黑名单非空（实际 {noRandomRows.Count} 行——名单内容随表漂移不锚）");
            string[] randomable = { "DealDamage", "PierceDamage", "DrainLife", "Heal", "LifeLoss", "RushSickness", "Sleep" };
            Assert(randomable.All(n => !CardCore.ComposerCatalog.HasMountBit(
                       CardCore.Attribute.AtomicEffectTable.GetByEnumName(n), CardCore.MountKind.NoRandom)),
                   "七个可随机行未标黑名单（翻转后默认可随机——滑条按 {value} 出现）");
            Assert(!(CardCore.Attribute.AtomicEffectTable.GetByEnumName("Freeze")?.Description ?? "").Contains("{value}"),
                   "冻结行无 {value} 模板——天然无滑条（旧错位「随机幅度」声明已删）");

            // ---- 1d. 位 4 SystemInternal（2026-10-04 定案；2026-10-07 位 0/3 删除后挂载资格走派生——
            // 资格保留、仅 UI 隐藏；晚间并行扩容：攻击/守卫/设置三行/宣告胜利并入系统位，共 9 行）----
            var internalRows = CardCore.Attribute.AtomicEffectTable.GetAll()
                .Where(r => r != null && CardCore.ComposerCatalog.HasMountBit(r, CardCore.MountKind.SystemInternal)).ToList();
            Assert(internalRows.Count == 7
                   && new HashSet<string>(internalRows.Select(r => r.EnumName))
                       .SetEquals(new[] { "Attack", "Guard", "SetPower", "SetLife", "SetCost",
                           "DeclareVictory", "RevealCard" }),
                   "系统位 7 行：攻击/守卫/设置攻击力/设置生命值/设置费用/对手胜利+展示（2026-10-09：展示即状态加系统位；修改三原语行已随 e9a5d96 表大改删除）");
            foreach (var row in internalRows)
                Assert(CardCore.ComposerCatalog.CanBeTrunkRow(row) && CardCore.ComposerCatalog.CanBeRewardRow(row),
                       $"系统行 {row.EnumName} 保留派生挂载资格（主干/奖励——仅 UI 不暴露）");

            // ---- 2. 槽级载荷端到端（合成器产出形态 = TestBranchEngines 数据形态）----
            // 引擎载荷（settle=3）：任意主干原子可挂，EngineKind/Then 转存进原子 Branch 槽
            var freeDef = CardCore.CardEffectConverter.ConvertOne(new CardCore.CardEffectData
            {
                Id = "VERIFY_COMPOSER_FREE",
                TriggerTiming = (int)TriggerTiming.Activate_Active, // 主动=时机系数×1——隔离「载荷豁免×1 vs 裸主干×0.8」的时机不对称
                AtomicEffects = new List<CardCore.AtomicEffectEntry>
                {
                    EngineAtom(CardCore.AtomicEffectType.DrawCard, 1, CardCore.BranchEngineKind.Clash, 2,
                        AtomRefs.New(CardCore.AtomicEffectType.DrawCard, value: 1)),
                },
            }, "VERIFY_COMPOSER_FREE");
            var plainDef = CardCore.CardEffectConverter.ConvertOne(new CardCore.CardEffectData
            {
                Id = "VERIFY_COMPOSER_PLAIN",
                TriggerTiming = (int)TriggerTiming.Activate_Active, // 同上——两口同时机，差额只剩载荷本身
                AtomicEffects = new List<CardCore.AtomicEffectEntry>
                {
                    AtomRefs.New(CardCore.AtomicEffectType.DrawCard, value: 1),
                },
            }, "VERIFY_COMPOSER_PLAIN");
            Assert(freeDef.Effects.Count == 1
                   && freeDef.Effects[0].Branch != null
                   && freeDef.Effects[0].Branch.Settle == CardCore.BranchSettleKind.Engine
                   && freeDef.Effects[0].Branch.EngineKind == CardCore.BranchEngineKind.Clash
                   && freeDef.Effects[0].Branch.EngineParam == 2
                   && freeDef.Effects[0].Branch.Then.Count == 1,
                   "引擎载荷：主干原子 Branch 槽 → Settle=Engine/Clash，奖励原子转存 Then（主序列=主干本体）");
            var freeCosts = CardCore.CostDerivationService.DeriveElementCosts(freeDef);
            var plainCosts = CardCore.CostDerivationService.DeriveElementCosts(plainDef);
            Assert(freeCosts[CardCore.ManaType.Gray] <= 0
                   && (int)freeCosts.Total == (int)plainCosts.Total,
                   $"引擎零计价：载荷/Then 不入 DeriveElementCosts（主干照常计价，拼点门槛=奖励锚价运行时判）——带载荷 {freeCosts.Total:0.###} vs 裸主干 {plainCosts.Total:0.###}");

            // 倒计时载荷（settle=3·Countdown）：初值 = Then 推导费换算回合（1费=1回合；声明 0=自动）
            var countdownDef = CardCore.CardEffectConverter.ConvertOne(new CardCore.CardEffectData
            {
                Id = "VERIFY_COMPOSER_COUNTDOWN",
                AtomicEffects = new List<CardCore.AtomicEffectEntry>
                {
                    EngineAtom(CardCore.AtomicEffectType.DrawCard, 1, CardCore.BranchEngineKind.Countdown, 0,
                        AtomRefs.New(CardCore.AtomicEffectType.DrawCard, value: 1)), // 锚价 2 → 2 回合
                },
            }, "VERIFY_COMPOSER_COUNTDOWN");
            Assert(countdownDef.Effects[0]?.Branch?.EngineKind == CardCore.BranchEngineKind.Countdown
                   && countdownDef.Effects[0].Branch.CountdownTurns == 2,
                   $"倒计时自动换算：奖励锚价 2 → 2 回合（实际 {countdownDef.Effects[0]?.Branch?.CountdownTurns}）");

            // ---- 3. 条件目录（产出条件）与预算口径 ----
            float drawCost = CardCore.CostDerivationService.RewardDerivedCost(new List<CardCore.AtomicEffectInstance>
            {
                CardCore.CardEffectConverter.ConvertAtomForUI(
                    Atom("DrawCard", 1)),
            });
            float dmgCost = CardCore.CostDerivationService.RewardDerivedCost(new List<CardCore.AtomicEffectInstance>
            {
                CardCore.CardEffectConverter.ConvertAtomForUI(
                    Atom("DealDamage", 3)),
            });
            Assert(drawCost == 2f, $"预算口径：抽1 锚价 2（≤2 过，实际 {drawCost}）");
            Assert(dmgCost == 3f, $"预算口径：3伤 锚价 3（>2 拒，实际 {dmgCost}）");
            var gate = System.Linq.Enumerable.First(CardCore.ComposerCatalog.OutcomeConditionsFor(CardCore.AtomicEffectType.DealDamage));
            Assert(gate.Id == "DmgKillsTarget" && CardCore.ComposerCatalog.GateBudget(gate) == 2
                   && CardCore.ComposerCatalog.GateLabel(gate).Contains("【奖励2】"),
                   "产出条件目录：伤害产出族 → 消灭门【奖励2】（Budget 代码常量，与 BuildGatePremium 同源）");
            Assert(!CardCore.ComposerCatalog.OutcomeConditionsFor(CardCore.AtomicEffectType.DrawCard).Any(),
                   "产出条件族匹配：抽牌非产出族 → 不可挂产出条件（空目录）");
            // ---- 3b. 局面状态门目录（2026-10-23 入表：表派生——身份/挂载/预算走原子表，
            // 判定谓词仍在 BranchConditionEvaluator；总数不锚（随表扩缩），成员锚定）----
            var drawGates = CardCore.ComposerCatalog.SituationGates;
            Assert(drawGates.Length >= 7 && drawGates.All(g => g.ProducerTag == null)
                   && drawGates.Any(g => g.Id == "FirstCardThisTurn") && drawGates.Any(g => g.Id == "DrawnInStandbyThisTurn")
                   && drawGates.Any(g => g.Id == "LifeBelowOpp") && drawGates.Any(g => g.Id == "CreaturesAboveOpp")
                   && drawGates.Any(g => g.Id == "LandsGe7") && drawGates.Any(g => g.Id == "HandEmpty")
                   && drawGates.Any(g => g.Id == "LifeLe7"),
                   "局面门目录与产出无关（任意主干原子可挂，含二批三门；总数随目录扩缩不锚）");
            // 门槛行不入主干/奖励（2026-10-23 入表：位 10 排除——防门槛行漏进原子库与卡组合）
            var gateRowsFromTable = CardCore.Attribute.AtomicEffectTable.GetAll()
                .Where(r => r != null && CardCore.ComposerCatalog.HasMountBit(r, CardCore.MountKind.SituationGate)).ToList();
            Assert(gateRowsFromTable.Count == drawGates.Length
                   && gateRowsFromTable.All(r => !CardCore.ComposerCatalog.CanBeTrunkRow(r))
                   && gateRowsFromTable.All(r => !CardCore.ComposerCatalog.CanBeRewardRow(r)),
                   "门槛行挂位 10 有限分支：不得作主干/奖励（CanBeTrunkRow/CanBeRewardRow 排除；行数与目录一致随表漂移）");
            // 动态费用行（2026-10-24 定案）：临摹/变形赋予挂位 11——补差随局内动态情况，不可作 Then 奖励；
            // 主干照旧（召唤衍生物同为动态计价但不挂此位，照旧可作奖励）
            var morphRow = CardCore.Attribute.AtomicEffectTable.GetByEnumName("Morph");
            var grantMorphRow = CardCore.Attribute.AtomicEffectTable.GetByEnumName("GrantMorph");
            var summonTokenRow = CardCore.Attribute.AtomicEffectTable.GetByEnumName("SummonToken");
            Assert(CardCore.ComposerCatalog.HasMountBit(morphRow, CardCore.MountKind.DynamicCost)
                   && CardCore.ComposerCatalog.HasMountBit(grantMorphRow, CardCore.MountKind.DynamicCost)
                   && !CardCore.ComposerCatalog.HasMountBit(summonTokenRow, CardCore.MountKind.DynamicCost),
                   "动态费用位 11：临摹/变形赋予挂位（召唤衍生物不挂——照旧可作奖励）");
            Assert(!CardCore.ComposerCatalog.CanBeRewardRow(morphRow)
                   && !CardCore.ComposerCatalog.CanBeRewardRow(grantMorphRow)
                   && CardCore.ComposerCatalog.CanBeTrunkRow(morphRow)
                   && CardCore.ComposerCatalog.CanBeTrunkRow(grantMorphRow),
                   "动态费用行不作 Then 奖励（CanBeRewardRow 排除）；主干资格保留");
            Assert(CardCore.ComposerCatalog.IsOutcomeCondition("DmgKillsTarget")
                   && CardCore.ComposerCatalog.IsOutcomeCondition("DeclareHit")
                   && !CardCore.ComposerCatalog.IsOutcomeCondition("LifeBelowOpp")
                   && CardCore.ComposerCatalog.IsSituationCondition("LifeBelowOpp")
                   && !CardCore.ComposerCatalog.IsSituationCondition("DmgKillsTarget"),
                   "条件族归类判定（converter 折叠遗留门步骤同源：产出族→Outcome、局面族→Gate）");
            // 预算三面同源（2026-10-23 入表定案）：表 Budget 列 = SituationGates.Budget = BuildGatePremium
            var gatePremium = CardCore.CostDerivationService.BuildGatePremium();
            Assert(drawGates.All(g => gateRowsFromTable.Any(r => r.GateId == g.Id && r.Budget == g.Budget))
                   && drawGates.All(g => gatePremium.TryGetValue(g.Id, out var pb) && pb == g.Budget),
                   "门槛预算三面同源：表 Budget 列 = 目录 Budget = BuildGatePremium（逐项相等——预算唯一来源=表）");
            Assert(drawGates.First(g => g.Id == "DrawnInStandbyThisTurn").Budget == 3
                   && drawGates.First(g => g.Id == "LifeBelowOpp").Budget == 1
                   && drawGates.First(g => g.Id == "FirstCardThisTurn").Budget == 1,
                   "状态门预算锚：命运一抽 3 / 一批通用门 1（表 Budget 列）");
            Assert(drawGates.First(g => g.Id == "LandsGe7").Budget == 2
                   && drawGates.First(g => g.Id == "HandEmpty").Budget == 2
                   && drawGates.First(g => g.Id == "LifeLe7").Budget == 2,
                   "状态门二批预算锚：操控地≥7/手牌=0/生命≤7 均 2（表 Budget 列）");
            // 改写门目录（2026-10-05 退役）：拦截式改写族已迁唯一光环——三族目录全清；
            // 诅咒门不还原（诅咒通道=引擎主干行 EngineCurseOnDraw，2026-10-08 接棒）
            Assert(CardCore.ComposerCatalog.OutcomeConditions.All(g => !g.Id.StartsWith("DmgRewrite"))
                   && CardCore.ComposerCatalog.SituationGates.All(g => !g.Id.StartsWith("DmgRewrite"))
                   && !gatePremium.ContainsKey("CurseOnDraw"),
                   "改写门四条已从分支目录退役（迁唯一光环；差价计价/配对守卫同批废除）；CurseOnDraw 不在预算表");

            // ---- 4. 并列保序：无抉择 Steps 平铺折叠进 def.Effects（两槽定案）----
            var parDef = CardCore.CardEffectConverter.ConvertOne(new CardCore.CardEffectData
            {
                Id = "VERIFY_COMPOSER_PAR",
                Steps = new List<CardCore.EffectStepData>
                {
                    new CardCore.EffectStepData { kind = 0, atomic = Atom("DealDamage", 1) },
                    new CardCore.EffectStepData { kind = 0, atomic = Atom("DrawCard", 1) },
                    new CardCore.EffectStepData { kind = 0, atomic = Atom("Heal", 2) },
                },
            }, "VERIFY_COMPOSER_PAR");
            Assert(parDef.Steps.Count == 0 && parDef.Effects.Count == 3
                   && parDef.Effects[0].Type == CardCore.AtomicEffectType.DealDamage
                   && parDef.Effects[1].Type == CardCore.AtomicEffectType.DrawCard
                   && parDef.Effects[2].Type == CardCore.AtomicEffectType.Heal,
                   "并列：三原子步骤平铺折叠保序（Steps 仅承载 Choice——无抉择即落主干序列）");
            // 折叠配对（converter 遗留门步骤→载荷）：产出条件归 Outcome、局面门归 Gate、elseSteps 丢弃
            var foldDef = CardCore.CardEffectConverter.ConvertOne(new CardCore.CardEffectData
            {
                Id = "VERIFY_COMPOSER_FOLD",
                Steps = new List<CardCore.EffectStepData>
                {
                    new CardCore.EffectStepData { kind = 0, atomic = Atom("DealDamage", 3) },
                    new CardCore.EffectStepData
                    {
                        kind = 1, conditionId = "DmgKillsTarget",
                        thenSteps = new List<CardCore.AtomicEffectEntry> { Atom("DrawCard", 1) },
                        elseSteps = new List<CardCore.AtomicEffectEntry> { Atom("Heal", 2) },
                    },
                },
            }, "VERIFY_COMPOSER_FOLD");
            Assert(foldDef.Steps.Count == 0 && foldDef.Effects.Count == 1
                   && foldDef.Effects[0].Branch != null
                   && foldDef.Effects[0].Branch.Settle == CardCore.BranchSettleKind.Outcome
                   && foldDef.Effects[0].Branch.OutcomeId == "DmgKillsTarget"
                   && foldDef.Effects[0].Branch.Then.Count == 1,
                   "遗留门步骤折叠：产出条件折入前原子 Branch（Outcome 族），else 奖励丢弃（无 Else 语义）");
            var foldGateDef = CardCore.CardEffectConverter.ConvertOne(new CardCore.CardEffectData
            {
                Id = "VERIFY_COMPOSER_FOLDG",
                Steps = new List<CardCore.EffectStepData>
                {
                    new CardCore.EffectStepData { kind = 0, atomic = Atom("DrawCard", 1) },
                    new CardCore.EffectStepData
                    {
                        kind = 1, conditionId = "LifeBelowOpp",
                        thenSteps = new List<CardCore.AtomicEffectEntry> { Atom("DrawCard", 1) },
                    },
                },
            }, "VERIFY_COMPOSER_FOLDG");
            Assert(foldGateDef.Effects.Count == 1 && foldGateDef.Effects[0].Branch != null
                   && foldGateDef.Effects[0].Branch.Settle == CardCore.BranchSettleKind.Gate
                   && foldGateDef.Effects[0].Branch.GateId == "LifeBelowOpp",
                   "遗留门步骤折叠：局面门折入前原子 Branch（Gate 族——达标奖励/不达标无事）");

            // ---- 5. 描述动态渲染（AtomText——SynergyUI 侧；2026-10-09 双侧域极性侧词定案：
            //      有害双侧域冠「对方」于「目标」前；区间文本已表随机，无「随机」前缀）----
            var cfg = CardCore.Attribute.AtomicEffectTable.GetByType(CardCore.AtomicEffectType.DealDamage);
            var atom = Atom("DealDamage", 3, amp: 1f);
            string rendered = SynergyUI.AtomText.Render(cfg, atom, null);
            Assert(rendered == "对对方目标造成0至6点伤害",
                   $"描述动态渲染：3±100% → 「对对方目标造成0至6点伤害」（实际 「{rendered}」）");
            atom.amp = 0f;
            Assert(SynergyUI.AtomText.Render(cfg, atom, null) == "对对方目标造成3点伤害", "描述渲染：amp=0 → 模板+侧词（无随机区间）");
            // 槽级载荷后缀（两槽定案）：引擎/产出条件/局面门三族文本
            // 引擎短式化（2026-10-09）：引擎身份+x 在正文「自由分支·{名}{x}」——后缀只接「→{奖励}」
            var engineSuffix = SynergyUI.AtomText.BranchSuffix(
                EngineAtom(CardCore.AtomicEffectType.DrawCard, 1, CardCore.BranchEngineKind.DeathToll, 2,
                    Atom("DrawCard", 1)));
            Assert(engineSuffix.StartsWith("→") && engineSuffix.Contains("抽") && !engineSuffix.Contains("["),
                   $"载荷后缀（引擎短式）：「{engineSuffix}」——只接奖励（条件在正文）");
            var engineBody = SynergyUI.AtomText.EngineShortBodyFor(
                EngineAtom(CardCore.AtomicEffectType.EngineDeathToll, 1, CardCore.BranchEngineKind.DeathToll, 2,
                    Atom("DrawCard", 1)));
            Assert(engineBody == "此卡在手牌或场上时计数双方生物死亡，计数大于等于2时触发奖励（满6不再计数，使用后清零）",
                   $"引擎短文案（2026-10-19 手牌/场上改案·死亡计数完整句）：「{engineBody}」——门槛=Then 锚价推导（DrawCard1=2），engineParam 死数据不参与");
            var outcomeSuffix = SynergyUI.AtomText.BranchSuffix(
                OutcomeAtom(CardCore.AtomicEffectType.DealDamage, 3, "DmgKillsTarget", Atom("DrawCard", 1)));
            Assert(outcomeSuffix.Contains("如果消灭了目标") && outcomeSuffix.Contains("，") && outcomeSuffix.Contains("抽"),
                   $"载荷后缀（产出条件）：「{outcomeSuffix}」——自然句式「，如果{{从句}}，{{奖励}}」");
            var gateSuffix = SynergyUI.AtomText.BranchSuffix(
                GateAtom(CardCore.AtomicEffectType.DrawCard, 1, "LifeBelowOpp", Atom("DrawCard", 1)));
            Assert(gateSuffix.Contains("如果生命值低于对手") && gateSuffix.Contains("抽")
                   && !gateSuffix.Contains("逆转") && !gateSuffix.Contains("["),
                   $"载荷后缀（局面门）：「{gateSuffix}」——与产出条件同款自然句（达标奖励/不达标无事，无逆转句）");

            // ---- 6. 哈希口径：amp / br{} 载荷段参与 HashEffect（去重不失真） ----
            var g1 = new SynergyUI.EffectGraphData("H") { header = new CardCore.CardEffectData() };
            g1.steps.Add(new CardCore.EffectStepData
            {
                kind = 0,
                atomic = Atom("DealDamage", 3),
            });
            var g2 = new SynergyUI.EffectGraphData("H") { header = new CardCore.CardEffectData() };
            g2.steps.Add(new CardCore.EffectStepData
            {
                kind = 0,
                atomic = Atom("DealDamage", 3, amp: 1f),
            });
            Assert(SynergyUI.ContentHasher.HashEffect(g1) != SynergyUI.ContentHasher.HashEffect(g2),
                   "哈希口径：RandomAmplitude 不同 → HashEffect 不同");
            CardCore.AtomicEffectEntry HostGraph(int engine) =>
                EngineAtom(CardCore.AtomicEffectType.DrawCard, 1, (CardCore.BranchEngineKind)engine, 2,
                    Atom("DrawCard", 1));
            var g3 = new SynergyUI.EffectGraphData("H")
            {
                header = new CardCore.CardEffectData
                {
                    AtomicEffects = new List<CardCore.AtomicEffectEntry> { HostGraph((int)CardCore.BranchEngineKind.Clash) },
                },
            };
            var g4 = new SynergyUI.EffectGraphData("H")
            {
                header = new CardCore.CardEffectData
                {
                    AtomicEffects = new List<CardCore.AtomicEffectEntry> { HostGraph((int)CardCore.BranchEngineKind.LuckRoll) },
                },
            };
            Assert(SynergyUI.ContentHasher.HashEffect(g3) != SynergyUI.ContentHasher.HashEffect(g4),
                   "哈希口径：槽级 branch 载荷（br{} 段）——引擎不同 → HashEffect 不同");
            var g5 = new SynergyUI.EffectGraphData("H")
            {
                header = new CardCore.CardEffectData
                {
                    AtomicEffects = new List<CardCore.AtomicEffectEntry> { Atom("DrawCard", 1) },
                },
            };
            Assert(SynergyUI.ContentHasher.HashEffect(g3) != SynergyUI.ContentHasher.HashEffect(g5),
                   "哈希口径：有无 branch 载荷 → HashEffect 不同（载荷是功能字段）");
        }

        private static void TestCostAnchors()
        {
            // ---- 表值 ----
            // 2026-10-09 清理：Heal TotalUnitCost=0.4 钉价断言删除——表价已迁绿0.5（表为单一来源，测试不钉价）
            Assert(ElementAffinities.GetAffinityForEffect(AtomicEffectType.GrantTaunt).PrimaryColor == ManaType.White,
                   "计价锚：GrantTaunt 表 White（2026-09-13 用户改表——白色防御系，与守护/禁魔石同族；锚原按 Green 已过期）");
            var dd = ValueSystemConfigManager.Instance.GetOrCreateConfig().DelayDiscountConfig;
            Assert(System.Math.Abs(dd.At(1) - 1f) < 1e-4 && System.Math.Abs(dd.At(3) - 1f) < 1e-4
                   && System.Math.Abs(dd.At(5) - 0.93333f) < 1e-3
                   && System.Math.Abs(dd.At(9) - 0.8f) < 1e-4 && System.Math.Abs(dd.At(12) - 0.75f) < 1e-4,
                   "计价锚：d(C) 分段折 d(1)=d(3)=1（C1-3 不折）/ d(5)=0.9333（4-9 线性1→0.8）/ d(9)=0.8 / ≥10 恒0.75（2026-10-10 分段定案）");

            // ---- 身材：1费 = 2点属性（灰）；2026-10-15 卡型默认分配：免费点（属性1+攻/守opt-out）
            //      只减灰桶 S（不溢出彩色）——白板生物减 1，总额与旧口径一致 ----
            Assert(DeriveTotal(MakeCostCard(Cardtype.Creature, 1, 1)) == 0, "计价锚：1/1 白板 D=0（S1×0.75→1 − 免费点1减灰桶）");
            Assert(DeriveTotal(MakeCostCard(Cardtype.Creature, 0, 2)) == 0, "计价锚：0/2 白板 D=0（同上）");
            Assert(DeriveTotal(MakeCostCard(Cardtype.Creature, 2, 2)) == 1, "计价锚：2/2 白板 D=1（S2×0.75→2 − 减1）");
            Assert(DeriveTotal(MakeCostCard(Cardtype.Creature, 3, 2)) == 1, "计价锚：3/2 白板 D=1（S2.5→3 ×0.75→2 − 减1）");

            // ---- 底盘纯函数（2026-10-15 卡型默认分配定案）：SurplusOf=免费点（生物=3−攻守→属性1+opt-out；
            //      非生物=3−效果槽）；StructuralSurcharge=超槽加价（生物=每槽；非生物=结构项>3 差额）----
            Assert(CardCompositionCost.SurplusOf(MakeCostCard(Cardtype.Creature, 1, 1)) == 1
                   && CardCompositionCost.StructuralSurcharge(MakeCostCard(Cardtype.Creature, 1, 1)) == 0,
                   "计价锚：底盘 0 效果生物 → 免费点 1（属性1）全额减灰桶 S");
            Assert(CardCompositionCost.SurplusOf(MakeCostCard(Cardtype.Creature, 1, 1, MakeEffect("Heal", 1))) == 0
                   && CardCompositionCost.StructuralSurcharge(MakeCostCard(Cardtype.Creature, 1, 1, MakeEffect("Heal", 1))) == 0,
                   "计价锚：底盘 1 效果生物 → 攻1+守1+槽1=3 项正好用满（免费点0/超槽0；总额同旧口径）");
            Assert(CardCompositionCost.StructuralSurcharge(MakeCostCard(Cardtype.Creature, 1, 1, MakeEffect("Heal", 1), MakeEffect("Heal", 1))) == 1,
                   "计价锚：底盘 2 效果生物 → 攻1+守1+槽2=4 项 → 超 1 灰（免费点0；净 +1 同旧口径）");
            Assert(CardCompositionCost.StructuralSurcharge(MakeCostCard(Cardtype.Creature, 1, 1,
                       MakeEffect("Heal", 1), MakeEffect("Heal", 1), MakeEffect("Heal", 1))) == 2,
                   "计价锚：底盘 3 效果生物 → 攻1+守1+槽3=5 项 → 2 灰（用户点名口径）");
            var noAtk = MakeCostCard(Cardtype.Creature, 1, 1);
            noAtk.NoAttack = true;
            Assert(CardCompositionCost.SurplusOf(noAtk) == 2,
                   "计价锚：底盘无攻白板 → 免费点 2（属性1+opt-out 释放，全减灰桶 S 不能用作其他）");
            var spell1 = MakeCostCard(Cardtype.Spell, null, null, MakeEffect("DealDamage", 2));
            Assert(CardCompositionCost.SurplusOf(spell1) == 2 && CardCompositionCost.PriceOffsetOf(spell1) == 0
                   && CardCompositionCost.KindSpeedBonusOf(spell1) == 2,
                   "计价锚：底盘法术 1 效果槽 → 速度 2（槽位从速度扣：3−槽数动态算，不落字段；无抵价）");
            spell1.SpeedBonus = 2; // 字段已退役（2026-10-15 改版）——脏数据写值不得影响动态速度
            Assert(CardCompositionCost.PriceOffsetOf(spell1) == 0 && CardCompositionCost.SurplusOf(spell1) == 2
                   && CardCompositionCost.KindSpeedBonusOf(spell1) == 2,
                   "计价锚：法术速度恒=3−槽数（SpeedBonus 字段退役，写值无效）");
            var spell2 = MakeCostCard(Cardtype.Spell, null, null, MakeEffect("DealDamage", 2), MakeEffect("Heal", 1));
            var spell3 = MakeCostCard(Cardtype.Spell, null, null,
                MakeEffect("DealDamage", 2), MakeEffect("Heal", 1), MakeEffect("DrawCard", 1));
            Assert(CardCompositionCost.KindSpeedBonusOf(spell2) == 1 && CardCompositionCost.KindSpeedBonusOf(spell3) == 0,
                   "计价锚：法术 2 槽=速1、3 槽=速0（用户定案：最多三槽，槽位全从速度扣）");
            var ench1 = MakeCostCard(Cardtype.Enchantment, null, null, MakeEffect("Heal", 1));
            ench1.Durability = 3;
            Assert(CardCompositionCost.PriceOffsetOf(ench1) == 0
                   && CardCompositionCost.DurabilityBonusOf(ench1) == 2
                   && CardCompositionCost.KindSpeedBonusOf(ench1) == 0,
                   "计价锚：结界免费点 2 全投耐久（+2）；速通道退役（2026-10-15 改版：结界禁主动）");
            var enchActive = MakeCostCard(Cardtype.Enchantment, null, null, MakeEffect("Heal", 1));
            enchActive.Durability = 3;
            enchActive.Effects[0].TriggerTiming = (int)TriggerTiming.Activate_Active;
            Assert(CardCompositionCost.DurabilityBonusOf(enchActive) == 2
                   && CardCompositionCost.KindSpeedBonusOf(enchActive) == 0,
                   "计价锚：结界带主动行（形状非法——装载拦截）计价式仍全投耐久/速 0（发动闸另有来源拒绝）");

            // ---- 抉择分支计槽（2026-09-21：价差溢价已废）——一个效果含 2 分支 Choice = 2 槽 ----
            var choiceSlotData = MakeCostCard(Cardtype.Spell, null, null, MakeEffect("Heal", 1));
            choiceSlotData.Effects[0].Steps = new List<EffectStepData>(); // MakeEffect 不建 Steps（默认 null）
            choiceSlotData.Effects[0].Steps.Add(new EffectStepData
            {
                kind = 2,
                choices = new List<EffectChoiceData>
                {
                    new EffectChoiceData { steps = new List<EffectStepData>() },
                    new EffectChoiceData { steps = new List<EffectStepData>() },
                },
            });
            Assert(CostDerivationService.CountEffectSlots(choiceSlotData) == 2,
                   "计价锚：抉择分支计槽——1 效果含 2 分支 Choice = 2 槽");
            Assert(CardCompositionCost.SurplusOf(choiceSlotData) == 1
                   && CardCompositionCost.KindSpeedBonusOf(choiceSlotData) == 1,
                   "计价锚：底盘 法术 2 槽（抉择分支计槽）→ 速度 1（槽位从速度扣；法术内嵌抉择为形状拦——计价式照算）");

            // ---- 法术同折（2026-10-02 定案）+ 槽位从速度扣（2026-10-15 改版：无抵价通道）----
            // 恒推导（2026-10-22 定案）：Ĉ=E=红3.2+蓝2.0（现表锚价——Draw 不吃时机折）=5.2→档5
            // → f=d(5)=0.9333，红2.99+蓝1.87=4.85→5 取整、余数红蓝各+1 → 红3蓝2；免费点全投提速不抵价。
            var fbEffect = MakeEffect("DealDamage", 4);
            fbEffect.AtomicEffects.Add(AtomRefs.New(CardCore.AtomicEffectType.DrawCard, value: 1));
            var fb = CardCostService.Derive(MakeCostCard(Cardtype.Spell, null, null, fbEffect));
            Assert((int)fb.DerivedCost[ManaType.Red] == 3 && (int)fb.DerivedCost[ManaType.Blue] == 2,
                   $"计价锚：法术 4伤+1抽 = 红3+蓝2（E=红3.2+蓝2.0→Ĉ=5.2→档5，×0.9333→4.85→5 分色；编辑器运行时已对拍）");
            Assert(fb.DerivedTier == 5 && System.Math.Abs(fb.Factor - 0.93333f) < 1e-4f,
                   "计价锚：法术档位随总额自决 Ĉ=5（f=d(5)=0.9333——恒推导定案，旧「未声明档=d(MaxTier)」口径已废）");
            var fbSpeedCard = MakeCostCard(Cardtype.Spell, null, null, fbEffect);
            fbSpeedCard.SpeedBonus = 2; // 字段退役——脏数据不得影响
            var fbSpeed = CardCostService.Derive(fbSpeedCard);
            Assert((int)fbSpeed.DerivedCost[ManaType.Red] == 3 && (int)fbSpeed.DerivedCost[ManaType.Blue] == 2,
                   $"计价锚：法术无抵价恒红3+蓝2（SpeedBonus 字段退役写值无效）");
            Assert(DeriveTotal(MakeCostCard(Cardtype.Spell, null, null, MakeEffect("Heal", 2))) == 1,
                   "计价锚：回2命 = 1费（锚价→档1 f=d(1)=1 取整 1；法术无抵价通道——2026-10-15 改版翻转旧「−抵价2→0」）");

            // ---- 整卡最后折：档位随总额自决 Ĉ=round(S+E)=round(9+2)=11 → f=d(11)=0.75（恒推导定案）----
            var bigBody = MakeCostCard(Cardtype.Creature, 9, 9, MakeEffect("DealDamage", 3));
            bigBody.Cost[(int)ManaType.Gray] = 9; // 存档快照（显示缓存）——不参与推导
            var big = CardCostService.Derive(bigBody);
            Assert(System.Math.Abs(big.Factor - 0.75f) < 1e-4 && big.DerivedTotal == 8 && big.OffsetRequirement == 0,
                   $"计价锚：9/9 挂3伤 → Ĉ=round(9+2)=11→f=d(11)=0.75，折后8.25→D=8（灰7红1；超槽+1与属性1抵扣对冲净0；DD3 现表锚价红2.0），Req=0");
            var midTier = MakeCostCard(Cardtype.Creature, 9, 9, MakeEffect("DealDamage", 3));
            midTier.Cost[(int)ManaType.Gray] = 5; // 同内容、不同存档快照
            var mid = CardCostService.Derive(midTier);
            Assert(mid.DerivedTotal == big.DerivedTotal && System.Math.Abs(mid.Factor - big.Factor) < 1e-6f,
                   "计价锚：存档费用快照不参与推导——同内容不同 costList 推导全同（恒推导定案回归锚）");

            // ---- 两效果=超 1 槽（结构项4>3 加灰）；f=d(Ĉ) ----
            // Ĉ=S2+E(红2.0+蓝2.0 现表)=6→档6 → f=d(6)=0.9，整卡×0.9=5.4→5，超1槽+1灰 → D=6=Ĉ（恰符规则一）
            var chooser = MakeCostCard(Cardtype.Creature, 2, 2, MakeEffect("DealDamage", 3), MakeEffect("DrawCard", 1));
            chooser.Cost[(int)ManaType.Gray] = 4; // 存档快照不参与
            var ch = CardCostService.Derive(chooser);
            Assert(System.Math.Abs(ch.Factor - 0.9f) < 1e-4 && ch.DerivedTotal == 6 && ch.OffsetRequirement == 0,
                   $"计价锚：两效果 Ĉ=6→档6 f=d(6)=0.9 整卡(2+4)×0.9=5.4→5，超1槽+1灰 → D=6（E=红2.0+蓝2.0 现表锚价）");

            // ---- 超槽加价入灰（1-1 挂三效果：Ĉ=S1+E(3×蓝2.0 现表)=7→档7 → f=d(7)=0.8667，
            //      蓝5.2+灰0.87=6.07→6，超2槽+2灰 → D=8 灰3蓝5）----
            var tripleMount = MakeCostCard(Cardtype.Creature, 1, 1,
                MakeEffect("DrawCard", 1), MakeEffect("DrawCard", 1), MakeEffect("DrawCard", 1));
            tripleMount.Cost[(int)ManaType.Gray] = 9; // 存档快照不参与
            var tm = CardCostService.Derive(tripleMount);
            Assert(tm.DerivedTotal == 8,
                   $"计价锚：1/1 挂三效果 Ĉ=round(1+6)=7 → f=d(7)=0.8667，6.07→6，超2槽+2灰 → D=8（灰3蓝5）");

            // ---- 规则一简化（2026-09-11 沿用）：D≤Ĉ 直判，代价不再提供抵扣当量 ----
            // 恒推导（2026-10-22）：与上方同内容 → 推导全同（D=6=Ĉ 恰符）；旧「低声明→缺口」口径退役，
            // 真超模只能来自不吃折的加价（↓ 代价栏占槽）。
            var overBaseline = MakeCostCard(Cardtype.Creature, 2, 2, MakeEffect("DealDamage", 3), MakeEffect("DrawCard", 1));
            overBaseline.Cost[(int)ManaType.Gray] = 3; // 低存档快照：不改变推导
            var ov = CardCostService.Derive(overBaseline);
            Assert(ov.DerivedTotal == ch.DerivedTotal && ov.Conformant,
                   "计价锚：低存档快照不改变推导（同内容 D=6=Ĉ 恰符规则一——无「声明缺口」概念）");
            overBaseline.Effects[0].Costs = new List<CostEntry>
            {
                new CostEntry
                {
                    CostType = (int)CostType.Payload, Value = 4,
                    payload = AtomRefs.New(AtomicEffectType.DiscardCard, value: 4, kinds: new List<int> { 5 }),
                },
            };
            var ov2 = CardCostService.Derive(overBaseline);
            Assert(ov2.DerivedTotal == ov.DerivedTotal + 1 && !ov2.Conformant,
                   "计价锚（2026-10-04 本轮定案）：挂弃4张 Payload 代价 → 代价栏占 1 效果槽（底盘再加价 1 灰 → D+1=7>Ĉ6，Req=1 真超模）；锚价仍不并入卡费（当量抵扣下线维持——全价只作地牌门槛与补偿基准）");

            // 2026-10-09 清理：关键词计价两锚（嘲讽绿桶时代口径）删除——表色已迁 White（上方现行断言已锚），
            // 关键词计价现行=行锚×max(1,层数)（2026-10-08 关键词不叠加定案）

            // ---- 推导档（恒推导定案）：5/5 挂 3伤（1 效果底盘恰耗尽）→ Ĉ=round(5+2.4)=7 → f=d(7)=0.8667，
            //      整卡×f=6.41→D=6——支付价=推导分布，无差额补灰（旧 Ĉ 搜索+补灰口径已废）----
            var noCost = MakeCostCard(Cardtype.Creature, 5, 5, MakeEffect("DealDamage", 3));
            var nc = CardCostService.Derive(noCost);
            Assert(nc.DerivedTier == 7 && nc.DerivedTotal == 6,
                   $"计价锚：无存档费推导档 Ĉ=7、D=6（f=d(7)=0.8667；实际 Ĉ={nc.DerivedTier} D={nc.DerivedTotal}）");

            // ---- 跨边当量锚点已删（2026-09-10：改由 Polarity 错边折价承担，见 TestTargetDomainModel f 段）----

            // ---- 溢出治疗（2026-10-04 光环改造：溢出→上限+1 收编为丰盈仪典规则——
            //      无光环基线=溢出纯浪费（钳上限不动）；有光环=每次溢出 LifeUp×1、剩余截断） ----
            var wastePlayer = new Player("VERIFY_WASTE", 30);
            wastePlayer.Heal(7);
            Assert(wastePlayer.MaxHealth == 30 && wastePlayer.Life == 30
                   && wastePlayer.GetCounterCount(CardCore.Attribute.CounterRules.LifeUpCounter) == 0,
                   "溢出治疗（无丰盈基线）：满血30回复7 → 纯浪费（30/30，不加上限）");
            var wasteCreature = new CardWrapper(MakeCostCard(Cardtype.Creature, 2, 5));
            wasteCreature.Heal(7);
            Assert(wasteCreature.GetMaxLife() == 5 && wasteCreature.GetLife() == 5
                   && wasteCreature.GetCounterCount(CardCore.Attribute.CounterRules.LifeUpCounter) == 0,
                   "溢出治疗（无丰盈基线·生物）：纯浪费（5/5）");
            var partialHeal = new Player("VERIFY_OVERFLOW3", 30);
            partialHeal.Life = 28;
            partialHeal.Heal(2); // 恰好补满（28+2=30，无溢出）
            Assert(partialHeal.MaxHealth == 30 && partialHeal.Life == 30
                   && partialHeal.GetCounterCount(CardCore.Attribute.CounterRules.LifeUpCounter) == 0,
                   "常规治疗：未溢出照旧封顶（不触发层）");
            // 丰盈仪典生效侧（光环内对照）：真实载体入场+激活 HealOverflow → 旧基线行为回归
            //（IsActive 实时查询要求载体在场且存活——空载体激活不生效，须真实入场的结界）
            var bloomCore = GameCore.Instance;
            var bloomP1 = bloomCore?.Player1;
            var bloomCarrier = new CardWrapper(new CardData
            {
                ID = "VERIFY_BLOOM_CARRIER", CardName = "丰盈验证载体", Supertype = Cardtype.Enchantment, Durability = 9,
            });
            bloomCarrier.SetController(bloomP1);
            var bloomFieldSave = bloomCore != null && bloomCore.ZoneManager.TryAddToBattlefield(bloomCarrier, bloomP1);
            // 保存现场（本段纯函数域，正常为无激活；2026-10-07 多槽化——Active 单槽口退役，只存/回 HealOverflow 槽）
            var bloomSaveId = CardCore.RuleAuraSystem.IsActive(CardCore.RuleAuraComponents.HealOverflow)
                ? CardCore.RuleAuraComponents.HealOverflow : null;
            var bloomSaveCarrier = CardCore.RuleAuraSystem.CarrierOf(CardCore.RuleAuraComponents.HealOverflow);
            var bloomSaveCtrl = CardCore.RuleAuraSystem.ControllerOf(CardCore.RuleAuraComponents.HealOverflow);
            CardCore.RuleAuraSystem.Activate(CardCore.RuleAuraComponents.HealOverflow, bloomCarrier, bloomP1);
            try
            {
                var overflowPlayer = new Player("VERIFY_OVERFLOW", 30);
                overflowPlayer.Heal(7);
                Assert(overflowPlayer.MaxHealth == 31 && overflowPlayer.Life == 31
                       && overflowPlayer.GetCounterCount(CardCore.Attribute.CounterRules.LifeUpCounter) == 1,
                       "溢出治疗（丰盈光环）：满血30回复7 → LifeUp×1 → 31/31（剩余浪费）");
                var evenOverflow = new Player("VERIFY_OVERFLOW2", 30);
                evenOverflow.Heal(4);
                Assert(evenOverflow.MaxHealth == 31 && evenOverflow.Life == 31
                       && evenOverflow.GetCounterCount(CardCore.Attribute.CounterRules.LifeUpCounter) == 1,
                       "溢出治疗（丰盈光环）：满血30回复4 → LifeUp×1 → 31/31（溢出量不影响力的档位数）");
                var overflowCreature = new CardWrapper(MakeCostCard(Cardtype.Creature, 2, 5));
                overflowCreature.SetController(bloomP1); // 丰盈按受疗方归属判命中（无归属=不在光环侧）
                overflowCreature.Heal(7);
                Assert(overflowCreature.GetMaxLife() == 6 && overflowCreature.GetLife() == 6
                       && overflowCreature.GetCounterCount(CardCore.Attribute.CounterRules.LifeUpCounter) == 1,
                       "溢出治疗（丰盈光环·生物）：满血5回7 → LifeUp×1 → 上限6当前6（层换区清除）");
            }
            finally
            {
                CardCore.RuleAuraSystem.Reset(); // 还原（含 _active=null；正例在 V9.i 有全流程覆盖）
                if (bloomSaveId != null) CardCore.RuleAuraSystem.Activate(bloomSaveId, bloomSaveCarrier, bloomSaveCtrl);
                if (bloomFieldSave)
                    bloomCore.ZoneManager.MoveCard(bloomCarrier, bloomP1, Zone.Battlefield, Zone.Graveyard);
            }

            // ---- 永久类指示物（2026-09-07：max 再分一类，换区不删、净化可清——框架锚）----
            // 2026-09-13 修复：样例指示物 Awakening 已随 09-11 沉睡改造删除（未登记 id 落兜底档），
            // 改用现存 Permanent 非属性指示物 ArmorCounter（护甲层，无 StatKind 同语义）
            var permCard = new CardWrapper(MakeCostCard(Cardtype.Creature, 2, 5));
            permCard.AddCounters(CardCore.Attribute.KeywordRules.ArmorCounter, 2); // 已登记 Duration=Permanent
            CardCore.Attribute.CounterRules.AddStatCounter(permCard, CardCore.Attribute.CounterRules.PowerUpCounter, 3);
            CardCore.Attribute.CounterRules.ClearAll(permCard); // 换区口径
            Assert(permCard.GetCounterCount(CardCore.Attribute.KeywordRules.ArmorCounter) == 2 && permCard.GetPower() == 2
                   && permCard.GetCounterCount(CardCore.Attribute.CounterRules.PowerUpCounter) == 0,
                   "永久类：换区清除跳过 Permanent 层（临时层照常清+反写）");
            CardCore.Attribute.CounterRules.PurgeAll(permCard); // 净化口径
            Assert(permCard.GetCounterCount(CardCore.Attribute.KeywordRules.ArmorCounter) == 0,
                   "永久类：净化全清（效果级移除是永久层唯一清除口）");

            // 2026-10-09 清理：三轨计价·轨别档位（E 锚按 Duration 档 1/4）断言删除——
            // Duration 轴计价已随三轨统一退役（档=CounterSpec 真实持久，E 锚不再读持续档）

            // ② 连接光环费 A（2026-10-07 深夜终版：stat 光环=「属性增加/减少」一般效果行的光环化——
            //    源行按 value 符号取（正→AddPlusOne 白1 / 负→AddMinusOne 黑1）× 单回合折算 0.6；
            //    keyword 走行锚价 × 0.6（坚韧/守护另乘无限次数档 4.0，见 TestLinkAura 计价锚））
            var auraCard = MakeCostCard(Cardtype.Creature, 2, 2);
            var auraBase = CardCostService.Derive(auraCard);
            auraCard.ArrowDirections = HexDirection.Up;
            auraCard.LinkAuras.Add(new LinkAuraData { stat = "Power", value = 1 });
            auraCard.LinkAuras.Add(new LinkAuraData { keyword = "Taunt" });
            var auraR = CardCostService.Derive(auraCard);
            // 2026-10-09 清理：光环档 ×0.6 单回合折算两值断言删除——持续折算口径已随 Duration 轴退役
            //（现行光环计价=行锚×max(1,层数)+作用面档，见 actuating-range 定案）；保留"并入推导费"总锚
            Assert(auraR.DerivedTotal >= auraBase.DerivedTotal,
                   "三轨计价·光环档：光环费并入推导费（不白送）");
        }

        // ======================================== 时点接线（P0）/ 衍生物（P1）/ 计数与日志（P2） ========================================

        /// <summary>合成触发卡：生物 1/1 灰 1 费，指定时点的 DrawCard 触发式（触发次数=手牌增量，可观察）。
        /// 2026-09-13 修复：显式 TriggerLimitPerTurn=-1（无限）——第十一批起触发式默认一回合一次，
        /// 本段「打出+复活」/「观察者多次」夹具在同回合依赖多次触发，不声明会被闸门拦截导致断言失真。</summary>
        private static CardData TrigData(string id, params TriggerTiming[] timings)
        {
            var data = new CardData { ID = id, CardName = id, Supertype = Cardtype.Creature, Power = 1, Life = 1 };
            data.Cost = CostOf((ManaType.Gray, 1));
            foreach (var t in timings)
            {
                data.Effects.Add(new CardEffectData
                {
                    Id = $"{id}_T{(int)t}",
                    DisplayName = id,
                    TriggerTiming = (int)t,
                    TriggerLimitPerTurn = -1,
                    AtomicEffects = new List<AtomicEffectEntry>
                    {
                        AtomRefs.New(CardCore.AtomicEffectType.DrawCard, value: 1)
                    },
                });
            }
            return data;
        }

        /// <summary>灌满 bank/上限（快照返回，结束时恢复）——出牌段落的费用前置。</summary>
        private static (Dictionary<ManaType, int> snap1, Dictionary<ManaType, int> snap2, int i1, int i2)
            FillBanks(GameCore core, Player p1, Player p2)
        {
            var pool1 = core.ElementPool.GetPool(p1);
            var pool2 = core.ElementPool.GetPool(p2);
            var snap1 = new Dictionary<ManaType, int>(pool1.TempElements);
            var snap2 = new Dictionary<ManaType, int>(pool2.TempElements);
            int i1 = pool1.GlobalTurnIndex, i2 = pool2.GlobalTurnIndex;
            pool1.GlobalTurnIndex = 9;
            pool2.GlobalTurnIndex = 9;
            foreach (var t in AllManaTypes()) { pool1.TempElements[t] = 99; pool2.TempElements[t] = 99; }
            return (snap1, snap2, i1, i2);
        }

        private static void RestoreBanks(GameCore core, Player p1, Player p2,
            (Dictionary<ManaType, int> snap1, Dictionary<ManaType, int> snap2, int i1, int i2) s)
        {
            var pool1 = core.ElementPool.GetPool(p1);
            // 段尾收口（2026-09-21）：清场残留观察者（否则下段 EnsureMainPhase 的阶段事件会让
            // 其触发式入栈、记速器抬 1 拦住 0 速宣言——12 条连锁的根因）+ 排干清栈。
            foreach (var pl in new[] { p1, p2 })
            {
                var leftovers = core.ZoneManager.GetCards(pl, Zone.Battlefield).ToList();
                foreach (var c in leftovers)
                    core.ZoneManager.GetZoneContainer(pl).Remove(c, Zone.Battlefield);
            }
            GameActions.DrainStack(core);
            if (!core.StackEngine.IsEmpty) core.StackEngine.Clear();

            var pool2 = core.ElementPool.GetPool(p2);
            pool1.GlobalTurnIndex = s.i1;
            pool2.GlobalTurnIndex = s.i2;
            pool1.TempElements.Clear();
            foreach (var kv in s.snap1) pool1.TempElements[kv.Key] = kv.Value;
            pool2.TempElements.Clear();
            foreach (var kv in s.snap2) pool2.TempElements[kv.Key] = kv.Value;
        }

        /// <summary>段落收尾：本段合成卡撤场 + 注销触发式（防遗留观察者干扰后续段落）。</summary>
        private static void RetireCards(GameCore core, Player owner, params Card[] cards)
        {
            foreach (var c in cards)
            {
                if (c == null) continue;
                core.ZoneManager.GetZoneContainer(owner)?.Remove(c, c.GetZone());
                core.TriggerEngine.UnregisterEntityEffects(c);
            }
        }

        /// <summary>
        /// 进场来源三通道（P0）：手牌打出 CastPlayed / 墓地复活 Revived / token 生成 TokenSpawned。
        /// </summary>
        private static void TestEntrySources(GameCore core, Player p1, Player p2)
        {
            EnsureMainPhase(core, p1);
            var banks = FillBanks(core, p1, p2);

            CardPlayEvent playEvt = null;
            var enterEvents = new List<CardPutToBattlefieldEvent>();
            void OnPlay(CardPlayEvent e) => playEvt = e;
            void OnEnter(CardPutToBattlefieldEvent e) => enterEvents.Add(e);
            EventManager.Instance.Subscribe<CardPlayEvent>(OnPlay);
            EventManager.Instance.Subscribe<CardPutToBattlefieldEvent>(OnEnter);
            try
            {
                // ① 手牌打出：宣言 FromZone=Hand；入场 Source=CastPlayed、FromZone=Activation
                var a = InjectCard(core, p1, TrigData("VERIFY_ES_PLAYED"));
                enterEvents.Clear();
                // 收口（2026-09-21）：EnsureMainPhase 的阶段事件会让残留触发式入栈（计=1 拦 0 速宣言）——打牌前排干
                GameActions.DrainStack(core);
                if (!core.StackEngine.IsEmpty) core.StackEngine.Clear();
                Assert(GameActions.PlayCard(core, p1, a), "来源①：手牌打出声明成功—" + PlayGateProbe(core, p1, a));
                GameActions.DrainStack(core);
                Assert(playEvt != null && playEvt.FromZone == Zone.Hand, "使用宣言 FromZone=Hand");
                Assert(enterEvents.Count == 1 && enterEvents[0].Source == EnterSource.CastPlayed
                       && enterEvents[0].FromZone == Zone.Activation,
                       "打出入场 Source=CastPlayed、FromZone=Activation");
                Assert(core.ZoneManager.IsCardInZone(a, p1, Zone.Battlefield), "打出后已入场");

                // ② 墓地复活：Source=Revived
                var b = InjectCard(core, p1, TrigData("VERIFY_ES_REVIVED"));
                core.ZoneManager.GetZoneContainer(p1).Add(b, Zone.Graveyard);
                enterEvents.Clear();
                Assert(core.ZoneManager.TryMoveToBattlefield(b, p1, Zone.Graveyard), "来源②：复活入场成功");
                Assert(enterEvents.Count == 1 && enterEvents[0].Source == EnterSource.Revived,
                       "复活入场 Source=Revived");

                // ③ token/新生：Source=TokenSpawned、FromZone=None
                var c = new CardWrapper(new CardData { ID = "VERIFY_ES_TOKEN", CardName = "来源token", Supertype = Cardtype.Creature, Power = 1, Life = 1 });
                enterEvents.Clear();
                Assert(core.ZoneManager.TryAddToBattlefield(c, p1), "来源③：token 直接入场成功");
                Assert(enterEvents.Count == 1 && enterEvents[0].Source == EnterSource.TokenSpawned
                       && enterEvents[0].FromZone == Zone.None,
                       "token 入场 Source=TokenSpawned、FromZone=None（TryAddToBattlefield 补发已接通）");

                RetireCards(core, p1, a, b, c);
            }
            finally
            {
                EventManager.Instance.Unsubscribe<CardPlayEvent>(OnPlay);
                EventManager.Instance.Unsubscribe<CardPutToBattlefieldEvent>(OnEnter);
                RestoreBanks(core, p1, p2, banks);
            }
        }

        /// <summary>
        /// payload 过滤（P0.3）：同一入场事件下 OnPlay（登场）/ OnSummon（超集）/ OnOtherCreatureEnter（观察者）互不误触。
        /// 手牌净变量 = 触发次数的计数器（打出 -1 / 每次触发 +1）。
        /// </summary>
        private static void TestTriggerPayloadFilters(GameCore core, Player p1, Player p2)
        {
            EnsureMainPhase(core, p1);
            var banks = FillBanks(core, p1, p2);
            int Hand() => core.ZoneManager.GetCards(p1, Zone.Hand).Count;

            try
            {
                // 观察者 F：他人进场时抽 1（自己入场不触发）
                var f = InjectCard(core, p1, TrigData("VERIFY_TPF_OBS", TriggerTiming.OnOtherCreatureEnter));
                int h0 = Hand();
                Assert(GameActions.PlayCard(core, p1, f), "观察者打出声明");
                GameActions.DrainStack(core);
                Assert(Hand() == h0 - 1, "OnOtherCreatureEnter 自己入场不触发（净 -1=打出无抽）");

                // D：同卡挂 OnPlay + OnSummon 双效果——打出两触发（+2），观察者 +1
                var d = InjectCard(core, p1, TrigData("VERIFY_TPF_BOTH", TriggerTiming.OnPlay, TriggerTiming.OnSummon));
                // 牌库保底：净抽 3（双时点+观察者），抽干则断言测的是牌库见底而非触发（根因 D 修复）
                PadDeck(core, p1, 3, "VERIFY_TPF_BOTHPAD");
                int h1 = Hand();
                Assert(GameActions.PlayCard(core, p1, d), "双时点卡打出声明");
                GameActions.DrainStack(core);
                Assert(Hand() == h1 - 1 + 2 + 1,
                       "打出：登场+1、进场+1（超集语义）、观察者 +1（净 +2）");

                // 复活 D：OnPlay 不触发（Source=Revived）、OnSummon 触发、观察者 +1
                // 2026-09-13 修复：前面累计抽牌可能已抽干牌库——补保底（下方 G 段同款），
                // 否则抽不到=净 0，断言测的是牌库见底而非触发
                while (core.ZoneManager.GetCards(p1, Zone.Deck).Count < 2)
                {
                    var deckFiller = new CardWrapper(new CardData
                    {
                        ID = "VERIFY_TPF_FILLR" + core.ZoneManager.GetCards(p1, Zone.Deck).Count,
                        CardName = "复活前填充", Supertype = Cardtype.Creature, Power = 1, Life = 1,
                    });
                    deckFiller.SetController(p1);
                    core.ZoneManager.GetZoneContainer(p1).Add(deckFiller, Zone.Deck);
                }
                core.ZoneManager.MoveCard(d, p1, Zone.Battlefield, Zone.Graveyard);
                int h2 = Hand();
                Assert(core.ZoneManager.TryMoveToBattlefield(d, p1, Zone.Graveyard), "复活双时点卡");
                GameActions.DrainStack(core);
                Assert(Hand() == h2 + 1 + 1,
                       "复活：登场不触发、进场 +1、观察者 +1（净 +2）");

                // OnCardPlayed 观察者 G：使用宣言时点（含法术）——与 OnPlay 分工
                var g = InjectCard(core, p1, TrigData("VERIFY_TPF_DECL", TriggerTiming.OnCardPlayed));
                Assert(GameActions.PlayCard(core, p1, g), "宣言观察者打出声明");
                GameActions.DrainStack(core);
                // G 入场：G 自身无 OnPlay/OnSummon、观察者 F 对 G 入场 +1；打出的宣言触发 G 自己的 OnCardPlayed +1
                // 牌库保底：套件运行至此 p1 牌库可能已抽干（前面的抽牌观察者累计消耗），
                // 抽不到 = 触发结算了但净 0 —— 补注入填充卡保证断言测的是触发而非牌库见底
                while (core.ZoneManager.GetCards(p1, Zone.Deck).Count < 4)
                {
                    Crumb("deck-filler iter");
                    var filler = new CardWrapper(new CardData
                    {
                        ID = "VERIFY_TPF_FILL" + core.ZoneManager.GetCards(p1, Zone.Deck).Count,
                        CardName = "填充", Supertype = Cardtype.Creature, Power = 1, Life = 1,
                    });
                    filler.SetController(p1);
                    core.ZoneManager.GetZoneContainer(p1).Add(filler, Zone.Deck);
                }
                var spell = InjectCard(core, p1, new CardData
                {
                    ID = "VERIFY_TPF_SPELL", CardName = "宣言测法术", Supertype = Cardtype.Spell,
                    Cost = CostOf((ManaType.Gray, 1)),
                });
                int h3 = Hand(); // 注入后稳态基线（原版在注入前测，漏算注入+1——账错）
                Assert(GameActions.PlayCard(core, p1, spell), "打出无效果法术");
                GameActions.DrainStack(core);
                UnityEngine.Debug.Log($"[TPFDBG] OnCardPlayed 法术宣言：净 {Hand() - h3}（期望 0）"
                    + $" 牌库余 {core.ZoneManager.GetCards(p1, Zone.Deck).Count}"
                    + $" 注册数 {core.TriggerEngine.RegisteredEffects.Count(r => r.Effect?.Id != null && r.Effect.Id.StartsWith("VERIFY_TPF_DECL"))}");
                Assert(Hand() == h3 - 1 + 1, "OnCardPlayed 在法术使用宣言触发（净 0，与登场分工）");

                RetireCards(core, p1, f, d, g);
            }
            finally
            {
                RestoreBanks(core, p1, p2, banks);
            }
        }

        /// <summary>死时点激活（P0.3）：OnAttacked/OnExile/OnTargeted 映射补全后经真实事件触发。</summary>
        private static void TestDeadTimings(GameCore core, Player p1, Player p2)
        {
            EnsureMainPhase(core, p1);
            var banks = FillBanks(core, p1, p2);

            // 映射存在性（原为 null 静默跳过）
            Assert(TriggerTimingDefaults.GetEventType(TriggerTiming.OnAttacked) == typeof(AttackDeclarationEvent),
                   "映射：OnAttacked → AttackDeclarationEvent");
            Assert(TriggerTimingDefaults.GetEventType(TriggerTiming.OnExile) == typeof(CardCore.Attribute.CardExileEvent),
                   "映射：OnExile → CardExileEvent");
            Assert(TriggerTimingDefaults.GetEventType(TriggerTiming.OnReturnFromGraveyard) == typeof(CardPutToBattlefieldEvent),
                   "映射：OnReturnFromGraveyard → CardPutToBattlefieldEvent");
            Assert(TriggerTimingDefaults.GetEventType(TriggerTiming.OnTargeted) == typeof(AtomicEffectPhaseEvent),
                   "映射：OnTargeted → AtomicEffectPhaseEvent");

            int Hand() => core.ZoneManager.GetCards(p1, Zone.Hand).Count;
            try
            {
                // OnExile：经 Exile 原子真实管线（handler 基类 PublishEvent → 统一路由）
                var x = InjectCard(core, p1, TrigData("VERIFY_DT_EXILE", TriggerTiming.OnExile));
                Assert(GameActions.PlayCard(core, p1, x), "除外观察者打出");
                GameActions.DrainStack(core);
                int h0 = Hand();
                CardCore.Attribute.EffectHandlerRegistry.ExecuteEffect(
                    new AtomicEffectInstance { Type = AtomicEffectType.Exile },
                    new EffectExecutionContext
                    {
                        Source = p1, Controller = p1,
                        Targets = new List<Entity> { x },
                        ZoneManager = core.ZoneManager, ElementPool = core.ElementPool,
                    });
                GameActions.DrainStack(core);
                Assert(Hand() == h0 + 1, "OnExile 经除外事件触发（净 +1）");
                Assert(core.ZoneManager.IsCardInZone(x, p1, Zone.Exile) || x.GetZone() == Zone.Exile, "除外原子已把目标送入除外区");

                // OnAttacked：payload 过滤单测（映射已断言；管线可达性由 OnExile 腿证明）
                var y = InjectCard(core, p1, TrigData("VERIFY_DT_ATKED", TriggerTiming.OnAttacked));
                Assert(GameActions.PlayCard(core, p1, y), "被攻击观察者打出");
                GameActions.DrainStack(core);
                var regY = new RegisteredEffect
                {
                    Effect = new EffectDefinition { Id = "VERIFY_DT_ATKED_F", TriggerTiming = TriggerTiming.OnAttacked },
                    Source = y, Controller = p1,
                };
                Assert(TriggerPayloadFilter.Matches(TriggerTiming.OnAttacked,
                           new AttackDeclarationEvent { Attacker = p2, Target = y, AttackingPlayer = p2 }, regY),
                       "OnAttacked 过滤：被指方=自己 → 通过");
                Assert(!TriggerPayloadFilter.Matches(TriggerTiming.OnAttacked,
                           new AttackDeclarationEvent { Attacker = p2, Target = p1, AttackingPlayer = p2 }, regY),
                       "OnAttacked 过滤：被指方=他人 → 拒绝");

                // OnTargeted：原子 StartApplying 且目标含自己
                var z = InjectCard(core, p1, TrigData("VERIFY_DT_TARGET", TriggerTiming.OnTargeted));
                Assert(GameActions.PlayCard(core, p1, z), "被指向观察者打出");
                GameActions.DrainStack(core);
                int h2 = Hand();
                // 经 executor 真路径：OnTargeted 挂在原子三阶段 StartApplying（PublishPhase 在执行器，
                // 注册表直连不发相位事件——与 TestAtomicPhaseRouting 同根因）
                core.StackEngine.GetExecutor().ExecuteAsync(new EffectInstance
                {
                    Definition = new EffectDefinition
                    {
                        Id = "VERIFY_DT_TARGET_ATOM",
                        ElementCostPrepaid = true,
                        Effects = new List<AtomicEffectInstance> { new AtomicEffectInstance { Type = AtomicEffectType.DealDamage, Value = 0 } },
                    },
                    Source = p2, Controller = p2,
                    Targets = new List<Entity> { z },
                }, skipElementCost: true).Forget();
                GameActions.DrainStack(core);
                Assert(Hand() == h2 + 1, "OnTargeted 经原子 StartApplying 触发（统一路由修复后可见）");

                RetireCards(core, p1, x, y, z);
            }
            finally
            {
                RestoreBanks(core, p1, p2, banks);
            }
        }

        // ======================================== SBA 窗口（2026-09-15 SBA=速度1栈对象定案） ========================================

        /// <summary>
        /// SBA=速度1栈对象（2026-09-15 定案）：死亡/判负类 SBA 在连锁排干后作为速度1伪对象入栈——
        /// 记速器抬到1、开响应窗口（回合方≥1 / 非回合方严格&gt;1 可连锁）、LIFO 救场效果先结算、
        /// SBA 到点重查（被救回则空转、亡语不触发——与预言延迟验证同哲学）。
        /// 覆盖：T1 时点过滤单测（OnDeath/OnOtherCreatureDeath payload）；
        /// T2 亡语真实管线（门禁自身死亡豁免后 OnDeath 经 SBA 送墓真实触发）；
        /// T4a 速度2治疗在 SBA 窗口救回标死宿主（亡语「对手获得胜利」不触发）；
        /// T4c 速度1治疗非回合方连锁被拒（1 不&gt;1）；
        /// T3 无人救场 → SBA 结算 → 亡语 DeclareVictory 宣告对手胜利（EffectVictory 终局）。
        /// </summary>
        private static void TestSbaWindow(GameCore core, Player p1, Player p2)
        {
            // ---- 段内夹具 ----
            CardData Vanilla(string id) => new CardData
            {
                ID = id, CardName = id, Supertype = Cardtype.Creature, Power = 1, Life = 1,
            };

            // 亡语宿主（"角色"替身）：1/1 生物，OnDeath → 指定原子
            CardData DeathrattleData(string id, AtomicEffectType atom)
            {
                var data = new CardData
                {
                    ID = id, CardName = id, Supertype = Cardtype.Creature, Power = 1, Life = 1,
                };
                data.Effects.Add(new CardEffectData
                {
                    Id = id + "_ONDEATH",
                    DisplayName = id,
                    TriggerTiming = (int)TriggerTiming.OnDeath,
                    TriggerLimitPerTurn = -1,
                    AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(atom, value: 1) },
                });
                return data;
            }

            // 定速法术（BaseSpeed 声明卡面速度）：kinds 实例域收窄 + 选一/全取选择
            CardData SpeedSpellData(string id, int baseSpeed, AtomicEffectType atom, int value,
                List<int> kinds, CardCore.SelectionMode mode = CardCore.SelectionMode.Single)
            {
                var data = new CardData { ID = id, CardName = id, Supertype = Cardtype.Spell };
                data.Effects.Add(new CardEffectData
                {
                    Id = id + "_ONPLAY",
                    DisplayName = id,
                    TriggerTiming = (int)TriggerTiming.OnPlay,
                    BaseSpeed = baseSpeed,
                    SelectionMode = (int)mode,
                    AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(atom, value: value, kinds: kinds) },
                });
                return data;
            }

            // AoE 法术：对全体敌人（kinds{2}=EnemyLivingUnit 对方生物+角色）value 伤
            CardData AoEData(string id, int value = 1)
            {
                var data = new CardData { ID = id, CardName = id, Supertype = Cardtype.Spell };
                data.Effects.Add(new CardEffectData
                {
                    Id = id + "_ONPLAY",
                    DisplayName = "对全体敌人" + value + "伤",
                    TriggerTiming = (int)TriggerTiming.OnPlay,
                    BaseSpeed = 1,
                    SelectionMode = (int)CardCore.SelectionMode.Whole,
                    AtomicEffects = new List<AtomicEffectEntry>
                    {
                        AtomRefs.New(AtomicEffectType.DealDamage, value: value, kinds: new List<int> { 2 }),
                    },
                });
                return data;
            }

            Card Spawn(Player owner, CardData data)
            {
                var card = new CardWrapper(data);
                card.SetController(owner);
                Assert(core.ZoneManager.TryAddToBattlefield(card, owner), $"SBA段合成入场（{data.CardName}）");
                card.Untap();
                return card;
            }

            int Hand(Player p) => core.ZoneManager.GetCards(p, Zone.Hand).Count;

            // ---- T1 时点过滤单测（不上桌）----
            var stubSelf = new CardWrapper(Vanilla("VERIFY_SBA_T1_SELF"));
            var stubOther = new CardWrapper(Vanilla("VERIFY_SBA_T1_OTHER"));
            Assert(TriggerTimingDefaults.GetEventType(TriggerTiming.OnOtherCreatureDeath) == typeof(CardDestroyEvent),
                   "SBA·T1 映射：OnOtherCreatureDeath → CardDestroyEvent");
            var regOnDeath = new RegisteredEffect
            {
                Effect = new EffectDefinition { Id = "VERIFY_SBA_T1_F", TriggerTiming = TriggerTiming.OnDeath },
                Source = stubSelf, Controller = p1,
            };
            Assert(TriggerPayloadFilter.Matches(TriggerTiming.OnDeath,
                       new CardDestroyEvent { DestroyedCard = stubSelf }, regOnDeath),
                   "SBA·T1 OnDeath 过滤：自己死 → 通过");
            Assert(!TriggerPayloadFilter.Matches(TriggerTiming.OnDeath,
                        new CardDestroyEvent { DestroyedCard = stubOther }, regOnDeath),
                   "SBA·T1 OnDeath 过滤：他人死 → 拒绝（default 跨火已修）");
            var regOtherDeath = new RegisteredEffect
            {
                Effect = new EffectDefinition { Id = "VERIFY_SBA_T1_G", TriggerTiming = TriggerTiming.OnOtherCreatureDeath },
                Source = stubSelf, Controller = p1,
            };
            Assert(TriggerPayloadFilter.Matches(TriggerTiming.OnOtherCreatureDeath,
                       new CardDestroyEvent { DestroyedCard = stubOther }, regOtherDeath),
                   "SBA·T1 OnOtherCreatureDeath 过滤：他人死 → 通过");
            Assert(!TriggerPayloadFilter.Matches(TriggerTiming.OnOtherCreatureDeath,
                        new CardDestroyEvent { DestroyedCard = stubSelf }, regOtherDeath),
                   "SBA·T1 OnOtherCreatureDeath 过滤：自己死 → 拒绝");

            // ---- T2 亡语真实管线（门禁豁免后：OnDeath 经 SBA 送墓真实触发）----
            // 2026-09-21 契约对齐：内容契约（2026-09-13）效果栏不可挂「有害原子+己方域」——
            // 原版 kinds={1} 打己方宿主被 converter 剔除成白板（宿主不死）。改为宿主在 p2 侧、
            // p1 以 kinds={2} 直伤对方宿主；仍测「1速直伤致死→SBA 真实送墓→亡语真实触发」。
            // 账目修正：原 h0 在注入前取，「打出-1+亡语+1=持平」在亡语未触发时才成立（反账）；
            // 现亡语抽牌归宿主持有者 p2，断言 p2 净 +1。
            EnsureMainPhase(core, p1);
            var banks2 = FillBanks(core, p1, p2);
            try
            {
                var host2 = Spawn(p2, DeathrattleData("VERIFY_SBA_T2HOST", AtomicEffectType.DrawCard));
                var bolt = InjectCard(core, p1,
                    SpeedSpellData("VERIFY_SBA_T2BOLT", 1, AtomicEffectType.DealDamage, 1, new List<int> { 2 }));
                int h2 = Hand(p2);
                Assert(PlayCardSync(core, p1, bolt, new List<Entity> { host2 }), "SBA·T2 直伤打出（打对方宿主）");
                Assert(!host2.IsAlive && core.ZoneManager.IsCardInZone(host2, p2, Zone.Graveyard),
                       "SBA·T2 门禁豁免后亡语宿主真实送墓（SBA 轮对 DrainStack 透明）");
                Assert(Hand(p2) == h2 + 1, "SBA·T2 OnDeath 亡语经真实管线结算（宿主方抽 +1）");
                RetireCards(core, p2, host2);
                RetireCards(core, p1, bolt);
            }
            finally { RestoreBanks(core, p1, p2, banks2); }

            // ---- T4a 速度2治疗在 SBA 窗口救回（核心新语义）----
            // P2 回合：P1 是非回合方（响应门槛=速度严格 >1）；P1 场上 3×1/1 + 亡语宿主，
            // P1 手持速度2治疗。AoE 结算后全场归 0 → SBA 入栈开窗 → 2 速治疗 LIFO 先结（解标死）
            // → SBA 到点重查：宿主已活（空转不送墓、亡语不触发）、三个无辜生物照旧送墓。
            EnsureMainPhase(core, p2);
            var banks4a = FillBanks(core, p1, p2);
            try
            {
                var m1 = Spawn(p1, Vanilla("VERIFY_SBA_M1"));
                var m2 = Spawn(p1, Vanilla("VERIFY_SBA_M2"));
                var m3 = Spawn(p1, Vanilla("VERIFY_SBA_M3"));
                var hero = Spawn(p1, DeathrattleData("VERIFY_SBA_HERO", AtomicEffectType.DeclareVictory));
                var heal2 = InjectCard(core, p1,
                    SpeedSpellData("VERIFY_SBA_HEAL2", 2, AtomicEffectType.Heal, 2, new List<int> { 1 }));
                var aoe = InjectCard(core, p2, AoEData("VERIFY_SBA_AOE"));

                Assert(GameActions.PlayCard(core, p2, aoe), "SBA·T4a P2 主阶段打出 AoE（对全体敌人1伤）");
                GameActions.PassPriority(core, p1);
                GameActions.PassPriority(core, p2); // 双 Pass → AoE 同步结算 → FinishResolution → SBA 入栈
                var top4a = core.StackEngine.Peek();
                Assert(top4a != null && top4a.IsSBA, "SBA·T4a AoE 结算后：SBA 伪对象在栈顶（速度1栈对象）");
                Assert(core.StackEngine.CurrentPriorityHolder == p2,
                       "SBA·T4a SBA 轮：回合方先持优先权");
                Assert(core.StackEngine.SpeedCounter.CurrentSpeed == 1,
                       "SBA·T4a 记速器被 SBA 抬到 1");
                Assert(!hero.IsAlive, "SBA·T4a 宿主伤害落血即标死（未送墓——等 SBA 到点）");

                GameActions.PassPriority(core, p2); // 回合方让权 → holder=p1
                Assert(GameActions.PlayCardInResponse(core, p1, heal2, new List<Entity> { hero }),
                       "SBA·T4a 非回合方速度2治疗响应入栈（2 > 1 达标）");
                GameActions.DrainStack(core);       // LIFO：治疗先结（解标死）→ SBA 到点重查

                Assert(hero.IsAlive && hero.GetLife() > 0
                           && core.ZoneManager.IsCardInZone(hero, p1, Zone.Battlefield),
                       "SBA·T4a 宿主被救回（治疗解标死，SBA 重查空转）");
                Assert(!core.IsGameOver, "SBA·T4a 亡语「对手获得胜利」未触发（无终局）");
                Assert(core.ZoneManager.IsCardInZone(m1, p1, Zone.Graveyard)
                           && core.ZoneManager.IsCardInZone(m2, p1, Zone.Graveyard)
                           && core.ZoneManager.IsCardInZone(m3, p1, Zone.Graveyard),
                       "SBA·T4a 三个无辜生物照旧送墓（SBA 批内各自结算）");
                RetireCards(core, p1, m1, m2, m3, hero, heal2, aoe);
            }
            finally { RestoreBanks(core, p1, p2, banks4a); }

            // ---- T4c 速度1拒连（非回合方 1 不 > 1）----
            EnsureMainPhase(core, p2);
            var banks4c = FillBanks(core, p1, p2);
            try
            {
                var m4c = Spawn(p1, Vanilla("VERIFY_SBA_T4C_M"));
                var hero4c = Spawn(p1, DeathrattleData("VERIFY_SBA_T4C_HERO", AtomicEffectType.DeclareVictory));
                // 速度1探针（2026-10-15 改版：法术施放速度=效果 BaseSpeed+底盘剩余提速=3−槽数）——
                // 双效果（2 槽）+ BaseSpeed 0 → 速度恰 1（旧「BaseSpeed 1+提速2=3」恒过门槛，测不出拒绝）
                var heal1Data = new CardData { ID = "VERIFY_SBA_HEAL1", CardName = "速1治疗", Supertype = Cardtype.Spell };
                for (int h = 0; h < 2; h++)
                    heal1Data.Effects.Add(new CardEffectData
                    {
                        Id = "VERIFY_SBA_HEAL1_" + h,
                        DisplayName = "速1治疗" + h,
                        TriggerTiming = (int)TriggerTiming.OnPlay,
                        BaseSpeed = 0,
                        SelectionMode = (int)CardCore.SelectionMode.Single,
                        AtomicEffects = new List<AtomicEffectEntry>
                            { AtomRefs.New(AtomicEffectType.Heal, value: 2, kinds: new List<int> { 1 }) },
                    });
                var heal1 = InjectCard(core, p1, heal1Data);
                var aoe4c = InjectCard(core, p2, AoEData("VERIFY_SBA_T4C_AOE"));

                Assert(GameActions.PlayCard(core, p2, aoe4c), "SBA·T4c P2 打出 AoE");
                GameActions.PassPriority(core, p1);
                GameActions.PassPriority(core, p2);
                Assert(core.StackEngine.Peek()?.IsSBA == true, "SBA·T4c SBA 在栈顶（记速器=1）");
                GameActions.PassPriority(core, p2); // holder → p1
                Assert(!GameActions.PlayCardInResponse(core, p1, heal1, new List<Entity> { hero4c }),
                       "SBA·T4c 非回合方速度1响应被拒（法术速度=3−槽数=1，1 不> 1——记速器已被 SBA 抬到 1）");
                Assert(core.ZoneManager.GetCards(p1, Zone.Hand).Contains(heal1),
                       "SBA·T4c 治疗卡留在手中（声明失败退回手）");
                Assert(core.StackEngine.Peek()?.IsSBA == true, "SBA·T4c SBA 仍在栈顶");
                // 不排干收尾（避免第二场终局撞 PublishGameOverOnce 幂等闸门）：直接清场
                core.StackEngine.Clear();
                core.SBAEngine.ClearHistory();
                RetireCards(core, p1, m4c, hero4c, heal1, aoe4c);
            }
            finally { RestoreBanks(core, p1, p2, banks4c); }

            // ---- T4d 角色判负同样可救：AoE 打角色至 0 → SBA 窗口速度2治疗救回 → 不宣判不终局 ----
            EnsureMainPhase(core, p2);
            var banks4d = FillBanks(core, p1, p2);
            try
            {
                var m4d = Spawn(p1, Vanilla("VERIFY_SBA_T4D_M"));
                var heal2d = InjectCard(core, p1,
                    SpeedSpellData("VERIFY_SBA_T4D_HEAL", 2, AtomicEffectType.Heal, 2, new List<int> { 1, 2 }));
                // 显式费绕开推导：DealDamage 30 全取推导=红120，超地牌帽(9)在 CanAfford 第一道门被拒
                var aoe4dData = AoEData("VERIFY_SBA_T4D_AOE", 30);
                aoe4dData.Cost = CostOf((ManaType.Red, 1));
                var aoe4d = InjectCard(core, p2, aoe4dData);
                // 前置修正：T4a/T4c 的 AoE(1) 已累计 chip 2 点（Life=28）——玩家落血不截断（引擎定案
                // 「Player 直接扣」，可负），AoE(30) 会打成 -2，治疗 +2 只回到 0 仍 ≤0 → 宣判。
                // 回满 30 保证 30−30=0、治疗→2 的「角色判负可救」语义被真正测到
                p1.Life = 30;

                Assert(GameActions.PlayCard(core, p2, aoe4d),
                       "SBA·T4d P2 打出 AoE(30)（角色一并归零）—门禁:" + PlayGateProbe(core, p2, aoe4d));
                GameActions.PassPriority(core, p1);
                GameActions.PassPriority(core, p2);
                Assert(core.StackEngine.Peek()?.IsSBA == true, "SBA·T4d 判负 SBA（ZeroLife）在栈顶");
                GameActions.PassPriority(core, p2); // holder → p1
                Assert(GameActions.PlayCardInResponse(core, p1, heal2d, new List<Entity> { p1 }),
                       "SBA·T4d 速度2治疗响应入栈（目标=自己的角色）");
                GameActions.DrainStack(core); // 治疗先结（角色 Life 0→2）→ SBA 到点重查：ZeroLife 空转、生物照送

                Assert(p1.Life == 2, "SBA·T4d 角色被救回（Life=2，宣判未发生）");
                Assert(!core.IsGameOver, "SBA·T4d 无终局（角色亡语未宣告）");
                Assert(core.ZoneManager.IsCardInZone(m4d, p1, Zone.Graveyard),
                       "SBA·T4d 无辜生物照旧送墓");
                RetireCards(core, p1, heal2d, aoe4d);
            }
            finally { RestoreBanks(core, p1, p2, banks4d); }

            // ---- T5 角色亡语宣告终局（判负效果化，本段最后）----
            // AoE(30) 连角色一并归零 → SBA 批 [ZeroLife, ZeroToughness×4] 到点：
            // 先宣判（RoleDeathEvent → 内置角色亡语入队）→ 四尸送墓（宿主卡亡语入队）→
            // 亡语轮：角色亡语 DeclareVictory 宣告 P2 胜（宣判即终局，宿主卡亡语同轮幂等空转）。
            EnsureMainPhase(core, p2);
            var banks3 = FillBanks(core, p1, p2);
            GameOverEvent goEvt = null;
            void OnGameOver(GameOverEvent e) => goEvt = e;
            EventManager.Instance.Subscribe<GameOverEvent>(OnGameOver);
            try
            {
                var n1 = Spawn(p1, Vanilla("VERIFY_SBA_T5_M1"));
                var n2 = Spawn(p1, Vanilla("VERIFY_SBA_T5_M2"));
                var n3 = Spawn(p1, Vanilla("VERIFY_SBA_T5_M3"));
                var hero3 = Spawn(p1, DeathrattleData("VERIFY_SBA_T5_HERO", AtomicEffectType.DeclareVictory));
                var aoe3Data = AoEData("VERIFY_SBA_T5_AOE", 30);
                aoe3Data.Cost = CostOf((ManaType.Red, 1)); // 同 T4d：显式费绕开推导（红90 超帽）
                var aoe3 = InjectCard(core, p2, aoe3Data);

                Assert(GameActions.PlayCard(core, p2, aoe3),
                       "SBA·T5 P2 打出 AoE(30)（角色+三生物+宿主全灭）—门禁:" + PlayGateProbe(core, p2, aoe3));
                GameActions.PassPriority(core, p1);
                GameActions.PassPriority(core, p2);
                Assert(core.StackEngine.Peek()?.IsSBA == true, "SBA·T5 死局 SBA 在栈顶（无人可救）");
                GameActions.DrainStack(core); // SBA 轮 → 宣判+送墓 → 亡语轮（角色亡语先宣）→ 终局

                Assert(core.ZoneManager.IsCardInZone(n1, p1, Zone.Graveyard)
                           && core.ZoneManager.IsCardInZone(n2, p1, Zone.Graveyard)
                           && core.ZoneManager.IsCardInZone(n3, p1, Zone.Graveyard)
                           && core.ZoneManager.IsCardInZone(hero3, p1, Zone.Graveyard),
                       "SBA·T5 四具尸体全部送墓（三生物 + 亡语宿主）");
                Assert(core.IsGameOver, "SBA·T5 游戏终局");
                Assert(goEvt != null && goEvt.Winner == p2 && goEvt.Reason == GameOverReason.EffectVictory,
                       "SBA·T5 角色亡语「对手获得胜利」宣告 P2 胜（EffectVictory，判负效果化）");
            }
            finally
            {
                EventManager.Instance.Unsubscribe<GameOverEvent>(OnGameOver);
                RestoreBanks(core, p1, p2, banks3);
            }
        }

        /// <summary>
        /// 三类卡型定案回归（2026-10-02，与 TideServer verify V9 段同口径）：
        /// ①法术同折——f=d(Ĉ)（推导总额所在档，2026-10-22 恒推导）对法术不再豁免（旧「打出即生效 f≡1」已废）；
        /// ②域数据——伤害族开放无生命域 3,4（法术伤害可消耐久）；冻结域收紧 1,2（对结界无效）；
        /// ③结界耐久战斗侧——入场初始化耐久、受击（战斗/法术同管线）恒 -1 不落血、
        ///   归零直送墓（Smashed 直毁不经死亡决策表）、SBA 生命管线不扫无生命单位；
        /// ④攻击目标显式化——结界=合法攻击目标；非战场卡（FieldZone 英雄技能卡）不可被攻击。
        /// 自开新局（合成卡组），不依赖卡表数据。
        /// </summary>
        private static void TestThreeCardTypes(GameCore core, Player p1, Player p2)
        {
            // ---- ① 法术同折（纯函数；2026-10-22 恒推导定案：档位=推导总额所在档 Ĉ，不经存档 costList）----
            CardCore.CardCostResult Probe(Cardtype type, int dmg)
            {
                var d = new CardData { ID = $"TT_probe_{type}_{dmg}", CardName = "三类定案折扣探针", Supertype = type };
                if (type == Cardtype.Creature) { d.Power = 2; d.Life = 2; }
                d.Effects.Add(new CardEffectData
                {
                    Id = "TT_probe_onplay",
                    TriggerTiming = (int)TriggerTiming.OnPlay,
                    // 档位由内容自决：E=红 dmg×0.8（时机系数）→ Ĉ=round(E[+S]) 所在档
                    //（放大锚距保住「高档折更深」的严格性）
                    AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DealDamage, value: dmg) },
                });
                return CardCore.CardCostService.Derive(d);
            }
            var spellCheap = Probe(Cardtype.Spell, 1);   // Ĉ=E0.8→档1 → f=d(1)=1
            var spell9 = Probe(Cardtype.Spell, 9);       // Ĉ=E7.2→档7 → f=d(7)≈0.867
            var creature9 = Probe(Cardtype.Creature, 9); // Ĉ=S2+E7.2=9.2→档9 → f=d(9)=0.8
            Assert(Mathf.Approximately(spellCheap.Factor, 1f), $"三类定案：法术低档 f=d(Ĉ1)={spellCheap.Factor:0.###}=1");
            Assert(spell9.Factor < 0.99f && spell9.Factor > 0.7f,
                   $"三类定案：法术高档同折 f=d(Ĉ7)={spell9.Factor:0.###}（旧 f≡1 豁免已废；恒推导下档位随内容自决）");
            Assert(Mathf.Abs(creature9.Factor - 0.8f) < 1e-4f && creature9.Factor < spell9.Factor,
                   $"三类定案：生物同折且随总额升档 f=d(Ĉ9)={creature9.Factor:0.###}<d(Ĉ7)（生物 S 计入 Ĉ）");
            Assert(spell9.DerivedTotal > spellCheap.DerivedTotal,
                   $"三类定案：内容决定价 D(9伤)={spell9.DerivedTotal} > D(1伤)={spellCheap.DerivedTotal}");

            // ---- ② 域数据（表级） ----
            void AssertKinds(AtomicEffectType type, int[] expect, string what)
            {
                var row = CardCore.Attribute.AtomicEffectTable.GetByType(type);
                Assert(row != null, $"{what}：表行存在");
                if (row == null) return;
                var kinds = row.GetTargetKindList().OrderBy(x => x).ToArray();
                Assert(kinds.SequenceEqual(expect.OrderBy(x => x)),
                       $"{what}：TargetKinds=[{string.Join(",", kinds)}]");
            }
            AssertKinds(AtomicEffectType.DealDamage, new[] { 1, 2, 3, 4 }, "伤害族开放无生命域·造成伤害");
            AssertKinds(AtomicEffectType.PierceDamage, new[] { 1, 2, 3, 4 }, "伤害族开放无生命域·穿透");
            AssertKinds(AtomicEffectType.DrainLife, new[] { 1, 2, 3, 4 }, "伤害族开放无生命域·吸取");
            AssertKinds(AtomicEffectType.Freeze, new[] { 1, 2 },
                "冻结域（表授予域=双方单位 1,2——对结界/角色无效；「存储态=自己」口径已随表改面退役）");

            // ---- ③④ 结界耐久全流程（自开新局，合成卡组） ----
            var filler = Enumerable.Range(0, 8).Select(i => new CardData
            {
                ID = $"TT_fill_{i}", CardName = "三类填充" + i, Supertype = Cardtype.Creature, Power = 1, Life = 1,
            }).ToList();
            core.InitGame(CardLoader.BuildDeck(filler, 2), CardLoader.BuildDeck(filler, 2));
            p1 = core.Player1;
            p2 = core.Player2;
            GameActions.SkipElementPool(core, p1);

            var ench = InjectCard(core, p1, new CardData
            {
                ID = "TT_ench", CardName = "耐久结界", Supertype = Cardtype.Enchantment, Durability = 3,
            });
            // 耐久计价（2026-10-16 回调 0.5 灰/点）：3 耐久=1.5→取整 2 灰——备灰并放开浓度上限后打出
            core.ElementPool.GetPool(p1).TempElements[ManaType.Gray] = 2;
            core.ElementPool.GetPool(p1).GlobalTurnIndex = 9;
            Assert(GameActions.PlayCard(core, p1, ench), "结界从手牌发动（耐久费 2 灰——2026-10-16 耐久计价 0.5 灰/点）");
            GameActions.DrainStack(core);
            Assert((int)core.ElementPool.GetPool(p1).TempElements[ManaType.Gray] == 0,
                   "耐久费恰好 2 灰付清（1.5 取整入档；余额归 0 钉价）");
            Assert(ench.GetZone() == Zone.Battlefield, "结界两步式：发动区→进入战场");
            // 2026-10-15 拆分定案：不装主动效果（0 槽）→ 免费点 3 全投耐久 → 3+3=6
            Assert(ench.GetCounterCount(CardCore.Attribute.CounterRules.DurabilityCounter) == 6,
                   "结界入场初始化耐久=6（印刷3+免费点3全投耐久——CardPutToBattlefieldEvent 驱动）");
            core.SBAEngine.ExecuteAll();
            Assert(ench.IsAlive && ench.GetZone() == Zone.Battlefield,
                   "SBA 不扫无生命单位（旧「结界被当 0 防御送墓」bug 回归锚）");

            int lifeBefore = core.LayerEngine.CalculateToughness(ench); // 公开口径读有效生命（internal _life 编辑器程序集不可见）
            int dealt = CardCore.Attribute.KeywordRules.ApplyDamage(p2, ench, 5, isCombat: true);
            Assert(dealt == 5, $"战斗伤害对结界全额返回（实际 {dealt}——2026-10-16 撤钳 1）");
            Assert(ench.GetCounterCount(CardCore.Attribute.CounterRules.DurabilityCounter) == 1,
                   "5 点战斗伤害全额扣 5 耐久（6→1）");
            Assert(core.LayerEngine.CalculateToughness(ench) == lifeBefore, "结界受击不落血（绕过生命管线）");

            CardCore.DestroyReason? reason = null;
            System.Action<CardCore.CardDestroyEvent> onDest = e => { if (e.DestroyedCard == ench) reason = e.Reason; };
            EventManager.Instance.Subscribe(onDest);
            try
            {
                // 剩 1 耐久吃 3 伤：法术伤害同管线全额扣（超扣按剩余钳 0）→ 直毁
                CardCore.Attribute.KeywordRules.ApplyDamage(p1, ench, 3, isCombat: false);
                Assert(ench.GetCounterCount(CardCore.Attribute.CounterRules.DurabilityCounter) == 0,
                       "法术伤害同耐久管线全额扣（1−3 超扣钳 0）");
                Assert(ench.GetZone() == Zone.Graveyard, "结界耐久归零直送墓");
                Assert(reason == CardCore.DestroyReason.Smashed,
                       $"死因=Smashed 无生命直毁（实际 {reason}），不经死亡决策表");
            }
            finally { EventManager.Instance.Unsubscribe(onDest); }

            var attacker = new CardWrapper(new CardData
            {
                ID = "TT_atk", CardName = "三类攻击者", Supertype = Cardtype.Creature, Power = 2, Life = 2,
            });
            attacker.SetController(p2);
            Assert(core.ZoneManager.TryAddToBattlefield(attacker, p2), "攻击者入场");
            attacker.Untap();
            var ench2 = new CardWrapper(new CardData
            {
                ID = "TT_ench2", CardName = "耐久结界2", Supertype = Cardtype.Enchantment, Durability = 2,
            });
            ench2.SetController(p1);
            Assert(core.ZoneManager.TryAddToBattlefield(ench2, p1), "第二张结界入场");
            Assert(core.CombatSystem.CanAttackTarget(attacker, ench2, p2), "结界=合法攻击目标");
            foreach (var fc in core.ZoneManager.GetCards(p1, Zone.FieldZone))
                Assert(!core.CombatSystem.CanAttackTarget(attacker, fc, p2), "非战场卡（技能卡）不可被攻击");

            var victim = new CardWrapper(new CardData
            {
                ID = "TT_victim", CardName = "三类受害者", Supertype = Cardtype.Creature, Power = 1, Life = 1,
            });
            victim.SetController(p1);
            Assert(core.ZoneManager.TryAddToBattlefield(victim, p1), "受害者入场");
            CardCore.Attribute.KeywordRules.ApplyDamage(attacker, victim, 1, isCombat: true);
            Assert(!victim.IsAlive, "生物 1 伤致死：标死路径不变（回归锚）");
            core.SBAEngine.ExecuteAll();
            Assert(victim.GetZone() == Zone.Graveyard, "生物死亡经 SBA 送墓（回归锚）");

            // ---- ⑤ 主动效果计价口径（2026-10-02 定案，与 TideServer V9.d 同口径） ----
            var actCard = new CardData { ID = "TT_act", CardName = "主动探针", Supertype = Cardtype.Creature, Power = 1, Life = 1 };
            actCard.Effects.Add(new CardEffectData
            {
                Id = "TT_act_ab",
                TriggerTiming = (int)TriggerTiming.Activate_Active,
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DealDamage, value: 2) },
            });
            Assert(CardCore.CardCostService.Derive(actCard).EAnchor == 0,
                   "主动效果：主动不进 E 桶（锚价运行时现付，2026-09-08 定案回归锚）");
            var mixCard = new CardData { ID = "TT_mix", CardName = "混合探针", Supertype = Cardtype.Creature, Power = 1, Life = 1 };
            mixCard.Effects.Add(new CardEffectData
            {
                Id = "TT_mix_act",
                TriggerTiming = (int)TriggerTiming.Activate_Active,
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DealDamage, value: 2) },
            });
            mixCard.Effects.Add(new CardEffectData
            {
                Id = "TT_mix_onplay",
                TriggerTiming = (int)TriggerTiming.OnPlay,
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DrawCard, value: 1) },
            });
            Assert(CardCore.CostDerivationService.CountEffectSlots(mixCard) == 2,
                   "主动效果：主动照常计效果槽（占用维持定案——AI 每卡原子数上限的可见性锚）");

            Assert(ComposerCatalog.IsKeywordStyleGrant(AtomicEffectType.GrantLifesteal)
                   && !ComposerCatalog.IsKeywordStyleGrant(AtomicEffectType.GrantDepravity),
                   "主动效果：关键词型判定（自指域 Grant=关键词型；赋予型不受限）");
            var kwAct = new CardEffectData
            {
                Id = "TT_kw_act",
                TriggerTiming = (int)TriggerTiming.Activate_Instant,
                AtomicEffects = new List<AtomicEffectEntry>
                {
                    AtomRefs.New(AtomicEffectType.GrantLifesteal),
                    AtomRefs.New(AtomicEffectType.DealDamage, value: 1),
                },
            };
            var kwDef = CardEffectConverter.ConvertOne(kwAct, "TT_src");
            Assert(!kwDef.Effects.Any(a => a.Type == AtomicEffectType.GrantLifesteal)
                   && kwDef.Effects.Any(a => a.Type == AtomicEffectType.DealDamage),
                   "主动效果：主动内关键词型 Grant 被剔除、非关键词原子保留");
            var grantDef = CardEffectConverter.ConvertOne(new CardEffectData
            {
                Id = "TT_grant_act",
                TriggerTiming = (int)TriggerTiming.Activate_Active,
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.GrantDepravity, value: 1) },
            }, "TT_src");
            Assert(grantDef.Effects.Any(a => a.Type == AtomicEffectType.GrantDepravity),
                   "主动效果：赋予型 Grant 做主动放行（赋予指示物可以；正例=反疗，微缩/放大已删 2026-10-10）");

            // ---- ⑥ 发动方式钉死（2026-10-02 定案，与 TideServer V9.e 同口径） ----
            var volBad = CardEffectConverter.ConvertOne(new CardEffectData
            {
                Id = "TT_vol_bad",
                ActivationType = (int)EffectActivationType.Voluntary,
                TriggerTiming = (int)TriggerTiming.OnPlay,
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DealDamage, value: 1) },
            }, "TT_src");
            Assert(volBad.TriggerTiming == TriggerTiming.Activate_Active && !volBad.ElementCostPrepaid,
                   "发动方式钉死：主动+触发时机 → Activate_Active 且按主动计费（锚价现付）");
            var volInstant = CardEffectConverter.ConvertOne(new CardEffectData
            {
                Id = "TT_vol_inst",
                ActivationType = (int)EffectActivationType.Voluntary,
                TriggerTiming = (int)TriggerTiming.Activate_Instant,
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DealDamage, value: 1) },
            }, "TT_src");
            Assert(volInstant.TriggerTiming == TriggerTiming.Activate_Instant,
                   "发动方式钉死：主动+Activate_Instant 合法组合原样保留");
            var actMand = CardEffectConverter.ConvertOne(new CardEffectData
            {
                Id = "TT_act_mand",
                ActivationType = (int)EffectActivationType.Mandatory,
                TriggerTiming = (int)TriggerTiming.Activate_Active,
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DealDamage, value: 1) },
            }, "TT_src");
            Assert(actMand.ActivationType == EffectActivationType.Voluntary,
                   "发动方式钉死：主动+强制 → 覆写回主动（对称校验回归锚）");
        }

        /// <summary>原子三阶段统一路由（P0.2）：每阶段恰发布一次 + OnAtomicEffectResolution 触发式可达。</summary>
        private static void TestAtomicPhaseRouting(GameCore core, Player p1, Player p2)
        {
            EnsureMainPhase(core, p1);
            var banks = FillBanks(core, p1, p2);

            var phaseCounts = new Dictionary<CardCore.AtomicEffectPhase, int>();
            // 只数被测 DealDamage 原子的相位：观察者 w 的触发效果（DrawCard）结算时
            // 同样合法发布三相位，不能进「恰一次」的账（按原子类型过滤）
            void OnPhase(AtomicEffectPhaseEvent e)
            {
                if (e.EffectInstance == null || e.EffectInstance.Type != AtomicEffectType.DealDamage) return;
                phaseCounts.TryGetValue(e.Phase, out var c);
                phaseCounts[e.Phase] = c + 1;
            }
            EventManager.Instance.Subscribe<AtomicEffectPhaseEvent>(OnPhase);
            try
            {
                var w = InjectCard(core, p1, TrigData("VERIFY_AR_RES", TriggerTiming.OnAtomicEffectResolution));
                Crumb("AR: w injected");
                Assert(GameActions.PlayCard(core, p1, w), "原子结算观察者打出");
                Crumb("AR: w played");
                GameActions.DrainStack(core);
                Crumb("AR: drain#1 done");
                // w 入场本身不发原子三阶段；清零后测一次原子执行
                phaseCounts.Clear();
                int Hand() => core.ZoneManager.GetCards(p1, Zone.Hand).Count;
                int h0 = Hand();

                // 经 executor 真路径（直调 EffectHandlerRegistry.ExecuteEffect 不发原子三阶段——
                // PublishPhase 在执行器的扁平/步骤循环里，注册表直连只是 handler 分发）
                core.StackEngine.GetExecutor().ExecuteAsync(new EffectInstance
                {
                    Definition = new EffectDefinition
                    {
                        Id = "VERIFY_AR_ATOM",
                        ElementCostPrepaid = true, // 只测相位路由，不测付费
                        Effects = new List<AtomicEffectInstance> { new AtomicEffectInstance { Type = AtomicEffectType.DealDamage, Value = 0 } },
                    },
                    Source = p1, Controller = p1,
                    Targets = new List<Entity> { p2 },
                }, skipElementCost: true).Forget();
                Crumb("AR: executor fired");
                GameActions.DrainStack(core);
                Crumb("AR: drain#2 done");

                Assert(phaseCounts.TryGetValue(CardCore.AtomicEffectPhase.Activation, out var a) && a == 1,
                       "原子三阶段 Activation 恰一次（无路由双发）");
                Assert(phaseCounts.TryGetValue(CardCore.AtomicEffectPhase.StartApplying, out var s) && s == 1,
                       "原子三阶段 StartApplying 恰一次");
                Assert(phaseCounts.TryGetValue(CardCore.AtomicEffectPhase.ResolutionComplete, out var r) && r == 1,
                       "原子三阶段 ResolutionComplete 恰一次");
                Assert(Hand() == h0 + 1, "OnAtomicEffectResolution 触发式经统一路由可达（净 +1）");

                RetireCards(core, p1, w);
            }
            finally
            {
                EventManager.Instance.Unsubscribe<AtomicEffectPhaseEvent>(OnPhase);
                RestoreBanks(core, p1, p2, banks);
            }
        }

        /// <summary>SummonToken 原子（P1）：三落区 / 实例 ID / 事件 / 满场 / 落区费用三档。</summary>
        private static void TestSummonToken(GameCore core, Player p1, Player p2)
        {
            EnsureMainPhase(core, p1);

            var template = new CardData
            {
                ID = "VERIFY_TOKEN_TPL", CardName = "验证衍生物", Supertype = Cardtype.Creature, Power = 2, Life = 2,
            };
            CardCore.Attribute.Handlers.SummonTokenHandler.ResolveTemplate = id => id == template.ID ? template : null;

            var tokens = new List<TokenCreatedEvent>();
            var enters = new List<CardPutToBattlefieldEvent>();
            var hands = new List<CardEnterHandEvent>();
            void OnToken(TokenCreatedEvent e) => tokens.Add(e);
            void OnEnter(CardPutToBattlefieldEvent e) => enters.Add(e);
            void OnHand(CardEnterHandEvent e) => hands.Add(e);
            EventManager.Instance.Subscribe<TokenCreatedEvent>(OnToken);
            EventManager.Instance.Subscribe<CardPutToBattlefieldEvent>(OnEnter);
            EventManager.Instance.Subscribe<CardEnterHandEvent>(OnHand);
            try
            {
                int Bf() => core.ZoneManager.GetCards(p1, Zone.Battlefield).Count;
                int Hand() => core.ZoneManager.GetCards(p1, Zone.Hand).Count;
                int Deck() => core.ZoneManager.GetCards(p1, Zone.Deck).Count;

                // ① 落战场 ×2：进场事件 Source=TokenSpawned、实例 ID 唯一
                int bf0 = Bf();
                SummonTokenHandlerUtil.Run(core, p1, Zone.Battlefield, 2);
                GameActions.DrainStack(core);
                Assert(Bf() == bf0 + 2, "落战场：+2 个 token 占格");
                Assert(tokens.Count == 2 && tokens.All(t => t.Card != null && t.DropZone == Zone.Battlefield),
                       "TokenCreatedEvent ×2（带实例与落区）");
                Assert(enters.Count >= 2 && enters.TakeLast(2).All(e => e.Source == EnterSource.TokenSpawned),
                       "token 进场事件 Source=TokenSpawned");
                var ids = tokens.Select(t => t.Card.ID).ToList();
                Assert(ids.All(id => id.StartsWith("VERIFY_TOKEN_TPL#")) && ids.Distinct().Count() == 2,
                       $"实例 ID = 模板#序号 且唯一（{string.Join(",", ids)}）");

                // 2026-10-09 清理：落手牌/落牌组两段删除——衍生物落区已写死=战场
                //（2026-10-05 效果通用属性定案），手牌/牌库落区路径退役

                // ④ 满场：入墓 + 失败事件
                var filler = new List<Card>();
                while (core.ZoneManager.HasBattlefieldSpace(p1))
                {
                    var c = new CardWrapper(new CardData { ID = "VERIFY_TOKEN_FILL", CardName = "占位", Supertype = Cardtype.Creature, Power = 1, Life = 1 });
                    core.ZoneManager.TryAddToBattlefield(c, p1, EnterSource.SummonedByEffect);
                    filler.Add(c);
                }
                var failed = new List<CardActivationFailedEvent>();
                void OnFail(CardActivationFailedEvent e) => failed.Add(e);
                EventManager.Instance.Subscribe<CardActivationFailedEvent>(OnFail);
                int gy0 = core.ZoneManager.GetCards(p1, Zone.Graveyard).Count;
                SummonTokenHandlerUtil.Run(core, p1, Zone.Battlefield, 1);
                EventManager.Instance.Unsubscribe<CardActivationFailedEvent>(OnFail);
                Assert(failed.Count == 1 && failed[0].Reason == "BattlefieldFull", "满场：token 入墓 + 失败事件");
                Assert(core.ZoneManager.GetCards(p1, Zone.Graveyard).Count == gy0 + 1, "满场 token 落墓");
                RetireCards(core, p1, filler.ToArray());

                // ⑤ 计价随模板（2026-10-12 终版：单价=模板内容全价（S+K+E，不乘 d(C)）−遗忘补偿按色落位；
                //    模板不可解析=0；线性 ×数量。单价随表漂移不锚——只钉线性与可解析性）
                template.Cost = CardCore.ElementCost.FromValue(ManaType.Red, 3);
                CardCore.ElementCost CostOf(int count, string tplId)
                {
                    var def = new EffectDefinition
                    {
                        Id = "VERIFY_TOKEN_COST",
                        // 手构 def 显式单次（第十四批定案）——触发档不误乘
                        TriggerLimitPerTurn = 1,
                        Effects = new List<AtomicEffectInstance>
                        {
                            new AtomicEffectInstance { Type = AtomicEffectType.SummonToken, Value = count, StringValue = tplId }
                        },
                    };
                    return CostDerivationService.DeriveElementCosts(def);
                }
                var c2 = CostOf(2, template.ID);
                var c10 = CostOf(10, template.ID);
                var cMiss = CostOf(2, "VERIFY_TOKEN_MISSING");
                Assert(c2.Total > 0 && System.Math.Abs(c10.Total - c2.Total * 5f) < 0.05f,
                       $"计价=单价×数量（线性）：×2={c2.Total:0.##}、×10={c10.Total:0.##}（单价=模板内容全价−遗忘补偿，随表漂移不锚）");
                Assert(cMiss.Total == 0, "模板不可解析=0（构筑期拦截/运行时兜底同口径）");
            }
            finally
            {
                EventManager.Instance.Unsubscribe<TokenCreatedEvent>(OnToken);
                EventManager.Instance.Unsubscribe<CardPutToBattlefieldEvent>(OnEnter);
                EventManager.Instance.Unsubscribe<CardEnterHandEvent>(OnHand);
            }
        }

        /// <summary>SummonToken 直连执行辅助（验证段内使用）。</summary>
        private static class SummonTokenHandlerUtil
        {
            public static void Run(GameCore core, Player caster, Zone dropZone, int count)
            {
                CardCore.Attribute.EffectHandlerRegistry.ExecuteEffect(
                    new AtomicEffectInstance
                    {
                        Type = AtomicEffectType.SummonToken, Value = count,
                        StringValue = "VERIFY_TOKEN_TPL",
                    },
                    new EffectExecutionContext
                    {
                        Source = caster, Controller = caster,
                        SummonDropZone = dropZone, // 2026-09-10：落区上移组合层（经 context 下发）
                        Targets = new List<Entity>(),
                        ZoneManager = core.ZoneManager, ElementPool = core.ElementPool,
                    });
            }
        }

        /// <summary>对局史计数服务（P2a）：CardsPlayed/Damage 双向统计、Custom 条件、回合/本局双 scope。</summary>
        private static void TestMatchStats(GameCore core, Player p1, Player p2)
        {
            EnsureMainPhase(core, p1);
            var stats = core.MatchStats;
            Assert(stats != null, "MatchStatsService 已注册（组合根）");

            var banks = FillBanks(core, p1, p2);
            try
            {
                int played0 = stats.GetStat(p1, MatchStatsService.CardsPlayed);
                var spell = InjectCard(core, p1, new CardData
                {
                    ID = "VERIFY_MS_SPELL", CardName = "计数法术", Supertype = Cardtype.Spell,
                    Cost = CostOf((ManaType.Gray, 1)),
                });
                Assert(GameActions.PlayCard(core, p1, spell), "打出计数法术");
                GameActions.DrainStack(core);
                Assert(stats.GetStat(p1, MatchStatsService.CardsPlayed) == played0 + 1,
                       "CardsPlayed 使用宣言计数 +1");
                Assert(stats.GetStat(p1, MatchStatsService.CardsPlayed, StatScope.ThisTurn) >= 1,
                       "ThisTurn scope 可查");

                // DamageDealt / DamageTaken 双向
                int dd0 = stats.GetStat(p1, MatchStatsService.DamageDealt);
                int dt0 = stats.GetStat(p2, MatchStatsService.DamageTaken);
                CardCore.Attribute.EffectHandlerRegistry.ExecuteEffect(
                    new AtomicEffectInstance { Type = AtomicEffectType.DealDamage, Value = 3 },
                    new EffectExecutionContext
                    {
                        Source = p1, Controller = p1, Targets = new List<Entity> { p2 },
                        ZoneManager = core.ZoneManager, ElementPool = core.ElementPool,
                    });
                Assert(stats.GetStat(p1, MatchStatsService.DamageDealt) == dd0 + 3, "DamageDealt +3");
                Assert(stats.GetStat(p2, MatchStatsService.DamageTaken) == dt0 + 3, "DamageTaken +3");

                // Custom 条件：StringValue=statId、Value=阈值
                var checker = new ConditionChecker(core.ZoneManager);
                var ctx = new ConditionCheckContext { Activator = p1, ZoneManager = core.ZoneManager };
                int playedNow = stats.GetStat(p1, MatchStatsService.CardsPlayed);
                Assert(checker.Check(new ActivationCondition
                {
                    Type = ConditionType.Custom,
                    StringValue = MatchStatsService.CardsPlayed,
                    Value = playedNow,
                }, ctx), "Custom 条件：CardsPlayed>=当前值 → 满足");
                Assert(!checker.Check(new ActivationCondition
                {
                    Type = ConditionType.Custom,
                    StringValue = MatchStatsService.CardsPlayed,
                    Value = playedNow + 99,
                }, ctx), "Custom 条件：阈值不可达 → 不满足");

                // 回合 scope：跨回合 ThisTurn 清零、ThisGame 保留
                int gamePlayed = stats.GetStat(p1, MatchStatsService.CardsPlayed, StatScope.ThisGame);
                EndTurnPumped(core, p1);
                EnsureMainPhase(core, core.TurnEngine.TurnPlayer);
                Assert(stats.GetStat(p1, MatchStatsService.CardsPlayed, StatScope.ThisTurn) == 0,
                       "跨回合 ThisTurn 清零");
                Assert(stats.GetStat(p1, MatchStatsService.CardsPlayed, StatScope.ThisGame) == gamePlayed,
                       "ThisGame 跨回合保留");
            }
            finally
            {
                RestoreBanks(core, p1, p2, banks);
            }
        }

        /// <summary>计价锚点合成卡（不入对局，只喂 Derive）。</summary>
        private static CardData MakeCostCard(Cardtype type, int? power, int? life, params CardEffectData[] effects)
        {
            var d = new CardData { ID = "VERIFY_COST", CardName = "计价锚点", Supertype = type, Power = power, Life = life };
            d.Effects.AddRange(effects);
            return d;
        }

        /// <summary>单原子效果（站场类时点——挂载判定只看卡超类，时点不影响计价）。
        /// duration：DurationType 值，默认 0=Once（2026-09-10 持续上移——档位由数据声明决定）。</summary>
        private static CardEffectData MakeEffect(string atomType, int value, int duration = 0)
        {
            var e = new CardEffectData { Id = "VERIFY_COST_EFF", TriggerTiming = (int)TriggerTiming.OnDeath, Duration = duration };
            e.AtomicEffects = new List<AtomicEffectEntry> { Atom(atomType, value) };
            return e;
        }

        private static int DeriveTotal(CardData card) => CardCostService.Derive(card).DerivedTotal;

        // ======================================== 测试卡表费用重生成 ========================================

        /// <summary>
        /// 重推导测试卡组费用 + 重写 ID：逐卡按统一计价换建议档位分布（声明价作废），
        /// 逐卡输出 S/K/E/f/D/Ĉ 明细供人工过目，回写 TestDecks 下每一套卡组 JSON（形状不变）。
        /// ID 重写为内容哈希（CardIdentityService.ContentId）：内容变 → ID 变；卡名/描述随便改
        /// 不动 ID（同内容跨文件同 ID——测试集导入训练池后天然对齐，无别名漂移）。
        /// 2026-09-11 追加：原子表（AttributeValueConfig.json）ID 列同批重推——手工改表
        /// （DisplayName 改动/增删行）后 ID 漂移，由工具统一收口。
        /// 2026-09-21：卡/效果/卡组属用户数据统一落盘（2026-10-18 热更迁移：编辑器改住
        /// Assets/CardData，经 CardDataPaths 收口，不再直连 StreamingAssets）。
        /// 2026-10-16：换号同步重映射——RegenAtomicTableIds 产出 旧ID→新ID 映射，RegenOneDeck
        /// 内重写 Effects.json/Cards.json 的 refId/effectIds 引用（此前换号不同步，悬空引用被
        /// 装载端静默剔原子）。
        /// 2026-10-20：教学副表只跟换原子 id（RemapTutorialAtomRefs）——不重推费用、不换卡/效果
        /// id（教学卡按剧本钉死，允许不符计价标准）；教学卡组文件只引卡 id，无需跟换。
        /// </summary>
        [MenuItem("Tools/重推导测试卡表费用与ID")]
        public static void RegenerateTestTableCosts()
        {
            // 2026-09-14 双表口径：唯一卡表经 CardDataPaths（2026-10-18 热更迁移后编辑器=Assets/CardData；
            // TestDecks 已随效果引用化退役）
            string path = SynergyUI.CardDataPaths.FileIn("Cards.json");
            if (!File.Exists(path))
            {
                Debug.LogError($"[Regen] 找不到卡表 {path}");
                return;
            }

            // 三层推导链（2026-09-16 定案，顺序敏感）：原子表 id（sha256(DisplayName)）→
            // 效果 id（HashEffect：文本+参数+原子引用）→ 卡 id（HashCard：文本+参数+效果id序列）。
            // 原子表必须先行——效果/卡里的 refId 按 id 引用表行（GetByHashId），原子 id 后推会引用断链
            var atomRenames = RegenAtomicTableIds();

            if (RegenOneDeck(path, atomRenames))
                RemapTutorialAtomRefs(atomRenames); // 教学副表只跟换原子 id（不重推费用——允许不符计价标准）
        }

        /// <summary>仅重写卡表费用（2026-09-20）：跳过 RegenAtomicTableIds——ID 全不动，只做
        /// RegenOneDeck 的建议价重写。2026-10-16 起 RegenOneDeck 会重读原子表并刷新效果库全量
        /// 锚价快照（RemapAndRefreshEffectLibrary），本入口=「不动 ID 只重推费用」的轻量档；
        /// 历史顾虑（换号打断既有 refId）已由重推导入口的引用重映射收口。</summary>
        [MenuItem("Tools/仅重推正式卡费用（不动ID）")]
        public static void RegenerateCardCostsOnly()
        {
            string path = SynergyUI.CardDataPaths.FileIn("Cards.json");
            if (!File.Exists(path))
            {
                Debug.LogError($"[Regen] 找不到卡表 {path}");
                return;
            }
            RegenOneDeck(path, null);
        }

        /// <summary>原子表 ID 列重推（镜像 Config/gen_effect_ids.py 口径：sha256(DisplayName.Trim())
        /// UTF-8 的前 8 位 hex——三层推导链的第一层）。ID 是引用键：Effects.json/Cards.json 的
        /// 原子 refId 经 AtomicEffectTable.GetByHashId 按行解析（2026-09-16 修正旧注释"零消费"口径）。
        /// DisplayName 改动 → ID 漂移 → 引用方需同步（文本即身份，与 python 管线同约定）。
        /// DisplayName 撞名 → ID 撞号，告警。
        /// 2026-10-16 修复：①返回 旧ID→新ID 映射——调用方据此同步重映射 Effects.json/Cards.json
        /// 的 refId/effectIds 引用（此前换号不同步，悬空引用被装载端 ToStep 静默剔原子）；
        /// ②写盘后立即 AtomicEffectTable.Reload——静态三映射不随外部改表自刷新，
        /// 不重读则下游费用推导仍按旧表计价（「重推导后费用还是旧的」根因之一）。</summary>
        private static Dictionary<string, string> RegenAtomicTableIds()
        {
            string path = Path.Combine(Application.dataPath, "Configs", "AttributeValueConfig.json");
            if (!File.Exists(path))
            {
                Debug.LogError($"[Regen] 找不到原子表 {path}");
                return null;
            }

            var root = Newtonsoft.Json.Linq.JArray.Parse(File.ReadAllText(path));
            using var sha = System.Security.Cryptography.SHA256.Create();
            var seen = new HashSet<string>();
            var renames = new Dictionary<string, string>();
            int changed = 0;
            foreach (var row in root.OfType<Newtonsoft.Json.Linq.JObject>())
            {
                var dn = row["DisplayName"]?.ToString();
                if (string.IsNullOrWhiteSpace(dn)) continue;
                string eid = System.BitConverter.ToString(
                        sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(dn.Trim())))
                    .Replace("-", "").ToLowerInvariant().Substring(0, 8);
                if (!seen.Add(eid))
                    Debug.LogWarning($"[Regen] 原子表 DisplayName 撞名 → ID 撞号 {eid}：{dn}");
                if ((string)row["ID"] != eid)
                {
                    var oldId = (string)row["ID"];
                    if (!string.IsNullOrEmpty(oldId))
                        renames[oldId] = eid; // 引用重映射键：旧ID（引用方现值）→ 新ID
                    row["ID"] = eid;
                    changed++;
                }
            }

            var sb = new System.Text.StringBuilder();
            using (var tw = new StringWriter(sb))
            using (var jw = new Newtonsoft.Json.JsonTextWriter(tw)
                   { Formatting = Newtonsoft.Json.Formatting.Indented, IndentChar = ' ', Indentation = 2 })
                root.WriteTo(jw);
            File.WriteAllText(path, sb.ToString() + "\n");
            CardCore.Attribute.AtomicEffectTable.Reload(); // 换号立即生效：下游重映射守卫/费用推导读新映射
            Debug.Log($"[Regen] 原子表 ID 重推：{root.Count} 行，变更 {changed} 条（重映射键 {renames.Count} 个）→ {path}");
            return renames;
        }

        /// <summary>
        /// 对双表卡表重推导费用并回写（2026-09-14 引用化口径）。
        /// 装载（LoadCardsFromText）已完成 effectIds 解析；2026-10-22 恒推导定案后 EnsureCost 恒刷新
        /// 推导费显示缓存（旧「清声明重推导」「抉择=最大模式费声明」两分支退役——快照本就不参与支付；
        /// ModeCostCache internal，经 EnsureCost 回填缓存）。
        /// 回写统一走 CardConfigSerializer.Save——costList / C_+HashCard 卡 id / effectIds
        ///（Effects.json 幂等 upsert）全走序列化器单源，天然按内容去重（同内容同 id），
        /// 消灭旧裸 ContentId 双轨。**先清后写**：防止重推导换 id 后旧条目残留。
        /// 返回 false=悬空中止/空表（调用方据此跳过教学副表跟换）。
        /// </summary>
        private static bool RegenOneDeck(string path, Dictionary<string, string> atomRenames)
        {
            // 防陈旧缓存（2026-10-16 修复）：原子表静态三映射不随外部直改 JSON 自刷新——不重读则
            // 费用推导（ComputeAtomCostByColor / 效果锚价快照）仍按旧表计价，即「重推导后效果表
            // 费用还是旧的」根因。原子表先行：重映射悬空守卫需按现行行 ID 校验引用。
            CardCore.Attribute.AtomicEffectTable.Reload();
            if (!RemapAndRefreshEffectLibrary(path, atomRenames)) return false; // 悬空引用中止（防整库重写剔原子坐实丢内容）
            CardCore.EffectsLibrary.Reload(); // 防陈旧缓存：外部直改 Effects.json 后编辑器静态 _byId 不会自刷新（此处亦读回上一步重写的库文件）
            var cards = CardLoader.LoadCardsFromText(File.ReadAllText(path));
            if (cards.Count == 0)
            {
                Debug.LogError($"[Regen] 卡表为空/解析失败：{path}");
                return false;
            }

            foreach (var card in cards)
            {
                card.ResetCache(); // 清费用缓存（原子表/效果库已 Reload——防旧推导驻留）
                CardCostService.EnsureCost(card); // 恒刷新推导费显示缓存（抉择=最大模式费）
                var r = CardCostService.Derive(card);
                // D=0（无身材无效果无关键词/效果被契约剔除）= 空 Cost——合法态，日志守卫防空引用
                string costStr = card.Cost != null && !card.Cost.IsZero
                    ? card.Cost.ToString()
                    : "(空 Cost——D=0 或效果全被内容契约剔除)";
                Debug.Log($"[Regen] {card.CardName} S={r.S} K={r.K} E={r.EAnchor} f={r.Factor:0.###} D={r.DerivedTotal} → 档位Ĉ={r.DerivedTier}：{costStr}");
            }

            // 清文件后逐张 Save：CardConfigSerializer 单源回写（C_+HashCard id / costList / effectIds+效果库 upsert）
            File.WriteAllText(path, "{\n    \"cards\": [],\n    \"deckConfig\": {\n        \"copiesPerCard\": 1\n    }\n}");
            foreach (var card in cards)
                SynergyUI.CardConfigSerializer.Save(card);
            Debug.Log($"[Regen] 已重写 {cards.Count} 张卡的费用与内容 ID → {path}");
            return true;
        }

        // ======================================== 效果库引用重映射 + 锚价重推导（2026-10-16 修复） ========================================

        /// <summary>
        /// 效果库原子引用重映射 + 全库锚价快照重推导 + 卡表引用重指。
        /// 根因：原子表换号（RegenAtomicTableIds）后 Effects.json/Cards.json 的 refId 引用未同步——
        /// 装载端 EffectSlim.ToStep 严格按 refId 解析，悬空即整原子剔除（静默丢内容）；且 refId 入
        /// HashEffect/HashCard 依赖链，换号连坐效果 id/卡 id，卡表 effectIds 引用须同步重指。
        /// 流程：①全库（含未挂卡条的独立条目）refId 旧→新重映射；②悬空守卫——重映射后仍有未知
        /// refId = 中止不落盘（列明细人工处置，防 ③ 整库重写把剔原子后的缺内容写死）；③清档重写：
        /// 逐条 Save（id 重算 + 锚价快照按**现行原子表**实时推导——「仅重推费用」入口亦经此刷新
        /// 全库快照；清档防旧 id 条目残留，upsert 不删旧行）；④效果 id 旧→新 + 原子旧→新映射回写
        /// Cards.json（effectIds 列=效果引用与关键词原子引用混装，按目标分表查换；payload.refId 同步）。
        /// 返回 false=存在悬空引用已中止，调用方应放弃本轮推导。
        /// </summary>
        private static bool RemapAndRefreshEffectLibrary(string cardsPath, Dictionary<string, string> atomRenames)
        {
            string fxPath = Path.Combine(Path.GetDirectoryName(cardsPath), "Effects.json");
            if (!File.Exists(fxPath)) return true; // 库文件不存在=空库（卡装载自会告警）

            var validAtomIds = new HashSet<string>(
                CardCore.Attribute.AtomicEffectTable.GetAll()
                    .Where(c => c != null && !string.IsNullOrEmpty(c.HashId))
                    .Select(c => c.HashId));

            var wrapper = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(fxPath));
            var items = wrapper["items"] as Newtonsoft.Json.Linq.JArray;
            if (items == null || items.Count == 0) return true;

            // ① refId 重映射（Descendants 深扫：主干步骤/分支 then/代价 payload 全覆盖）+ ② 悬空守卫
            int remapped = 0;
            var dangling = new List<string>();
            foreach (var item in items.OfType<Newtonsoft.Json.Linq.JObject>())
            {
                string host = item["name"]?.ToString() ?? item["id"]?.ToString() ?? "?";
                foreach (var prop in item.Descendants().OfType<Newtonsoft.Json.Linq.JProperty>()
                         .Where(p => p.Name == "refId" && p.Value.Type == Newtonsoft.Json.Linq.JTokenType.String).ToList())
                {
                    string rid = (string)prop.Value;
                    if (atomRenames != null && atomRenames.TryGetValue(rid, out var nid))
                    {
                        prop.Value = nid;
                        remapped++;
                        rid = nid;
                    }
                    if (!validAtomIds.Contains(rid)) dangling.Add($"{host}·{rid}");
                }
            }
            if (dangling.Count > 0)
            {
                Debug.LogError($"[Regen] Effects.json 悬空原子引用 {dangling.Count} 处——中止推导不落盘" +
                               $"（防装载端剔原子后整库重写坐实丢内容），请人工核对原子表行 ID：{string.Join("，", dangling.Distinct())}");
                return false;
            }
            if (remapped > 0)
                WriteJTokenIndented(fxPath, wrapper);

            // ③ 清档重写：Save 逐条重算 id（HashEffect）+ 锚价快照按现行原子表实时推导
            var graphs = SynergyUI.EffectLibrarySerializer.LoadAll();
            var effectIdMap = new Dictionary<string, string>();
            foreach (var g in graphs)
                if (g != null && !string.IsNullOrEmpty(g.id)) effectIdMap[g.id] = g.id;
            File.WriteAllText(fxPath, "{\"items\":[]}");
            int rekeyed = 0;
            foreach (var g in graphs)
            {
                string oldId = g.id;
                SynergyUI.EffectLibrarySerializer.Save(g);
                if (!string.IsNullOrEmpty(oldId) && effectIdMap.TryGetValue(oldId, out var cur) && cur != g.id)
                {
                    effectIdMap[oldId] = g.id; // 内容变 → 效果 id 变（卡表 effectIds 待重指）
                    rekeyed++;
                }
            }

            // ④ 卡表引用重指：effectIds（效果引用+关键词原子引用混装）与 payload.refId
            var cardsRoot = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(cardsPath));
            int cardsRemapped = 0;
            foreach (var card in (cardsRoot["cards"] as Newtonsoft.Json.Linq.JArray
                                  ?? new Newtonsoft.Json.Linq.JArray()).OfType<Newtonsoft.Json.Linq.JObject>())
            {
                foreach (var ids in card.Descendants().OfType<Newtonsoft.Json.Linq.JProperty>()
                         .Where(p => p.Name == "effectIds" && p.Value is Newtonsoft.Json.Linq.JArray).ToList())
                {
                    var arr = (Newtonsoft.Json.Linq.JArray)ids.Value;
                    for (int i = 0; i < arr.Count; i++)
                    {
                        if (arr[i].Type != Newtonsoft.Json.Linq.JTokenType.String) continue;
                        string s = (string)arr[i];
                        if (atomRenames != null && atomRenames.TryGetValue(s, out var na)) { arr[i] = na; cardsRemapped++; }
                        else if (effectIdMap.TryGetValue(s, out var ne) && ne != s) { arr[i] = ne; cardsRemapped++; }
                    }
                }
                foreach (var prop in card.Descendants().OfType<Newtonsoft.Json.Linq.JProperty>()
                         .Where(p => p.Name == "refId" && p.Value.Type == Newtonsoft.Json.Linq.JTokenType.String).ToList())
                {
                    string rid = (string)prop.Value;
                    if (atomRenames != null && atomRenames.TryGetValue(rid, out var na)) { prop.Value = na; cardsRemapped++; }
                }
            }
            if (cardsRemapped > 0)
            {
                WriteJTokenIndented(cardsPath, cardsRoot);
                Debug.Log($"[Regen] 卡表引用重指：{cardsRemapped} 条 → {cardsPath}");
            }

            Debug.Log($"[Regen] 效果库重推导：{graphs.Count} 条（原子引用重映射 {remapped} 处·效果 id 换号 {rekeyed} 条）→ {fxPath}");
            return true;
        }

        /// <summary>
        /// 教学副表原子引用跟换（2026-10-20 定案）：原子表换号后 TutorialEffects.json 的 refId 与
        /// TutorialEffects/TutorialCards 的 effectIds 原子段同步重映射——**只换原子 id**：教学效果 id
        ///（EffectsLibrary.MergeTutorialEffects 原样读入不换号）、卡 id 与费用一律不动（教学卡按
        /// 剧本钉死，允许不符计价标准）；教学卡组文件（basics/jinjie 等）只引卡 id，无需跟换。
        /// 悬空引用仅告警不中止——教学表人工维护，与正式池推导解耦（正式池悬空中止见
        /// RemapAndRefreshEffectLibrary）。
        /// </summary>
        private static void RemapTutorialAtomRefs(Dictionary<string, string> atomRenames)
        {
            if (atomRenames == null || atomRenames.Count == 0) return; // 无换号=无跟换
            var validAtomIds = new HashSet<string>(
                CardCore.Attribute.AtomicEffectTable.GetAll()
                    .Where(c => c != null && !string.IsNullOrEmpty(c.HashId))
                    .Select(c => c.HashId));
            foreach (string file in new[] { "TutorialEffects.json", "TutorialCards.json" })
            {
                string path = SynergyUI.CardDataPaths.TutorialFileIn(file);
                if (!File.Exists(path)) continue;
                var root = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(path));
                int remapped = 0;
                var dangling = new List<string>();
                foreach (var prop in root.Descendants().OfType<Newtonsoft.Json.Linq.JProperty>()
                         .Where(p => p.Name == "refId" && p.Value.Type == Newtonsoft.Json.Linq.JTokenType.String).ToList())
                {
                    string rid = (string)prop.Value;
                    if (atomRenames.TryGetValue(rid, out var nid)) { prop.Value = nid; remapped++; rid = nid; }
                    if (!validAtomIds.Contains(rid)) dangling.Add(rid);
                }
                // effectIds 混装（效果库 id+关键词原子 refId，同正式卡口径）：只换命中重映射键的原子段
                foreach (var ids in root.Descendants().OfType<Newtonsoft.Json.Linq.JProperty>()
                         .Where(p => p.Name == "effectIds" && p.Value is Newtonsoft.Json.Linq.JArray).ToList())
                {
                    var arr = (Newtonsoft.Json.Linq.JArray)ids.Value;
                    for (int i = 0; i < arr.Count; i++)
                    {
                        if (arr[i].Type != Newtonsoft.Json.Linq.JTokenType.String) continue;
                        if (atomRenames.TryGetValue((string)arr[i], out var na)) { arr[i] = na; remapped++; }
                    }
                }
                if (dangling.Count > 0)
                    Debug.LogWarning($"[Regen] {file} 悬空原子引用 {dangling.Count} 处（教学表不中止，请人工核对原子表行 ID）：{string.Join("，", dangling.Distinct())}");
                if (remapped > 0)
                {
                    WriteJTokenIndented(path, root);
                    Debug.Log($"[Regen] {file} 原子引用跟换 {remapped} 处 → {path}");
                }
            }
        }

        /// <summary>JToken 2 空格缩进写盘（与原子表重推/迁移工具写盘风格一致）。</summary>
        private static void WriteJTokenIndented(string path, Newtonsoft.Json.Linq.JToken token)
        {
            var sb = new System.Text.StringBuilder();
            using (var tw = new StringWriter(sb))
            using (var jw = new Newtonsoft.Json.JsonTextWriter(tw)
                   { Formatting = Newtonsoft.Json.Formatting.Indented, IndentChar = ' ', Indentation = 2 })
                token.WriteTo(jw);
            File.WriteAllText(path, sb.ToString() + "\n");
        }

        // ======================================== 两个随机（2026-09-13 定案） ========================================

        /// <summary>
        /// 两个随机端到端：
        /// ① 数值随机：RollValue 边界（3±100% 样本 ⊆[0,6] 且两端可达；幅度 0 恒名义值；
        ///    实例名义 Value 字段不随掷值漂移——计价锚点）；
        /// ② 单位随机（RandomTarget 标志，2026-09-16 自 SelectionMode 移出）：TargetCount=2 从完整候选域
        ///    种子抽取（数量=2、成员⊆候选、不弹交互）；隐密/潜行口径——手动显示域不含（弹窗不显示）、
        ///    完整候选域含、随机可命中（绕过选择）；全部档（TargetCount≤0）≡全取；选一/选多预检用显示域
        ///    （唯一候选被隐藏时不可发动；扰魔断言已随 2026-10-13 退役改隐密）。
        /// </summary>
        private static void TestRandomness(GameCore core, Player p1, Player p2)
        {
            EnsureMainPhase(core, p1);

            int seq = 0;
            Card Spawn(Player owner, int power, int life, params string[] keywords)
            {
                var data = new CardData
                {
                    ID = "VERIFY_RND_" + (++seq), CardName = "RND" + seq,
                    Supertype = Cardtype.Creature, Power = power, Life = life,
                };
                foreach (var kw in keywords) data.Keywords.Add(kw);
                var card = new CardWrapper(data);
                card.SetController(owner);
                Assert(core.ZoneManager.TryAddToBattlefield(card, owner), $"随机段合成随从入场（{data.CardName}）");
                card.Untap();
                return card;
            }

            // ---- 0. RollValue 边界 ----
            GameRng.Reseed(20260913);
            int lo = int.MaxValue, hi = int.MinValue;
            for (int i = 0; i < 500; i++)
            {
                int v = GameRng.RollValue(3, 1f);
                if (v < 0 || v > 6) { Assert(false, $"RollValue 越界: {v}（应 ⊆[0,6]）"); break; }
                lo = System.Math.Min(lo, v);
                hi = System.Math.Max(hi, v);
            }
            Assert(lo == 0 && hi == 6, $"数值随机：3±100% 样本覆盖 [0,6] 两端可达（实际 [{lo},{hi}]）");
            bool steady = true;
            for (int i = 0; i < 50; i++)
                if (GameRng.RollValue(3, 0f) != 3) { steady = false; break; }
            Assert(steady, "数值随机：幅度 0 恒名义值");

            var inst = new CardCore.AtomicEffectInstance
            {
                Type = AtomicEffectType.DealDamage,
                Value = 3,
                RandomAmplitude = 1f,
            };
            bool inRange = true;
            for (int i = 0; i < 200; i++)
            {
                int v = inst.GetRolledValue();
                if (v < 0 || v > 6) { inRange = false; break; }
            }
            Assert(inRange && inst.Value == 3, "实例掷值：GetRolledValue ⊆[0,6] 且名义 Value 恒 3（计价锚点不漂移）");

            // ---- 1. 隐密口径：完整候选域 vs 手动显示域（原扰魔断言 2026-10-13 随机制退役改隐密同口径） ----
            var hiddenUnit = Spawn(p2, 2, 2);
            hiddenUnit.AddKeyword(Attribute.KeywordRules.Concealed, KeywordLane.Setting);
            var plain = Spawn(p2, 3, 3);
            var ctx = new EffectExecutionContext
            {
                Source = p1,
                Controller = p1,
                ZoneManager = core.ZoneManager,
                ElementPool = core.ElementPool,
            };
            var kinds = new List<int> { (int)CardCore.TargetKind.EnemyLivingUnit };
            var full = CardCore.Attribute.EffectHandlerRegistry.ResolveCandidates(kinds, "", ctx);
            Assert(full.Contains(hiddenUnit) && full.Contains(plain),
                   "隐密口径：完整候选域含隐密单位（范围波及/随机可命中——域层不再剔除）");
            var manual = CardCore.TargetResolver.ExcludeUnselectable(full, p1);
            Assert(!manual.Contains(hiddenUnit) && manual.Contains(plain),
                   "隐密口径：手动显示域不含隐密（弹窗不显示，AI/无头代替选取同口径）");

            // ---- 2. 目标随机：TargetCount=2 不弹交互、从完整域抽取 ----
            var def = new CardCore.EffectDefinition
            {
                Id = "VERIFY_RANDOM_2",
                SelectionMode = CardCore.SelectionMode.Multiple, // 单范围·选多（域={EnemyLivingUnit}）
                RandomTarget = true,
                TargetCount = 2,
                TargetDomain = new List<int>(kinds),
            };
            var picked = CardCore.Attribute.EffectHandlerRegistry
                .ResolveCompositionTargetsAsync(def, ctx).GetAwaiter().GetResult();
            Assert(picked.Count == 2 && picked.All(t => full.Contains(t)),
                   $"单位随机：抽 2 且成员 ⊆ 完整候选域（实际 {picked.Count} 个）");

            // 随机可命中隐密（概率断言：候选=p2+hiddenUnit+plain 共 3，抽 2 单轮命中概率 2/3；40 轮全 miss ≈ 1e-19）
            GameRng.Reseed(777);
            bool hiddenEverHit = false;
            for (int i = 0; i < 40 && !hiddenEverHit; i++)
                hiddenEverHit = CardCore.Attribute.EffectHandlerRegistry
                    .ResolveCompositionTargetsAsync(def, ctx).GetAwaiter().GetResult()
                    .Contains(hiddenUnit);
            Assert(hiddenEverHit, "单位随机：随机可命中隐密（绕过选择——不弹弹窗不受显示域限制）");

            // 全部档（TargetCount≤0）随机 ≡ 全取
            var defAll = new CardCore.EffectDefinition
            {
                Id = "VERIFY_RANDOM_ALL",
                SelectionMode = CardCore.SelectionMode.Multiple,
                RandomTarget = true,
                TargetDomain = new List<int>(kinds),
            };
            var pickedAll = CardCore.Attribute.EffectHandlerRegistry
                .ResolveCompositionTargetsAsync(defAll, ctx).GetAwaiter().GetResult();
            Assert(pickedAll.Count == full.Count, $"单位随机：全部档 ≡ 全取（{pickedAll.Count} vs {full.Count}）");

            // ---- 3. 选一预检走显示域：唯一候选是扰魔 → 不可发动 ----
            var loneDef = new CardCore.EffectDefinition
            {
                Id = "VERIFY_RANDOM_MANUAL",
                SelectionMode = CardCore.SelectionMode.Single,
                TargetCount = 1,
                // Stealth filter = 仅指潜行中——完整候选只剩新造的潜行单位（普通/隐密被 filter 排除），
                // 而选一/选多显示域又把它隐藏 → "完整域有候选、显示域空"的正交样本
                TargetDomain = new List<int> { (int)CardCore.TargetKind.EnemyLivingUnit },
                TargetFilter = "Stealth",
            };
            var stealthUnit = Spawn(p2, 1, 1);
            stealthUnit.AddCounters(CardCore.Attribute.CounterRules.StealthCounter, 1); // 潜行已指示物化（2026-10-08）
            var stealthCtx = new EffectExecutionContext
            {
                Source = p1,
                Controller = p1,
                ZoneManager = core.ZoneManager,
                ElementPool = core.ElementPool,
            };
            // 潜行也走显示域剔除：Manual 有候选（完整域）但显示域空 → HasCandidates=false（弹窗不空弹）
            var manualPicked = CardCore.Attribute.EffectHandlerRegistry
                .ResolveCompositionTargetsAsync(loneDef, stealthCtx).GetAwaiter().GetResult();
            Assert(manualPicked.Count == 0,
                   "弹窗口径：潜行单位在 Manual 显示域外——唯一候选被隐藏则选择结果为空");
            Assert(CardCore.TargetDomainService.HasCandidates(loneDef, stealthCtx) == false,
                   "弹窗口径：Manual 预检用显示域（不可发动），潜行可被随机/全域命中");

            RetireCards(core, p1);
            RetireCards(core, p2, hiddenUnit, plain, stealthUnit);
        }

        // ======================================== 帷幕/紊乱效果目标口径（2026-09-13 修复+更名） ========================================

        /// <summary>
        /// 目标硬限制：①帷幕（原"嘲讽"2026-09-13 更名，与守卫职责冲突后收窄）=只吸引**效果**目标、
        /// 不拦攻击——攻击侧自由（守卫拦截承担强制）；效果侧选择层（Manual 显示域/Random 抽取池
        /// 收窄为帷幕卡；Full 全域不受限）+ 外给目标收口；②紊乱=不可指角色——候选域层剔除（既有）
        /// + 外给目标收口（修复）。
        /// </summary>
        private static void TestTauntAndSickness(GameCore core, Player p1, Player p2)
        {
            EnsureMainPhase(core, p1);

            int seq = 0;
            Card Spawn(Player owner, int power, int life, params string[] keywords)
            {
                var data = new CardData
                {
                    ID = "VERIFY_TS_" + (++seq), CardName = "TS" + seq,
                    Supertype = Cardtype.Creature, Power = power, Life = life,
                };
                foreach (var kw in keywords) data.Keywords.Add(kw);
                var card = new CardWrapper(data);
                card.SetController(owner);
                Assert(core.ZoneManager.TryAddToBattlefield(card, owner), $"帷幕段合成随从入场（{data.CardName}）");
                card.Untap();
                return card;
            }

            var ctx = new EffectExecutionContext
            {
                Source = p1,
                Controller = p1,
                ZoneManager = core.ZoneManager,
                ElementPool = core.ElementPool,
            };
            var enemyKinds = new List<int> { (int)CardCore.TargetKind.EnemyLivingUnit };

            // ---- 1. 帷幕：不拦攻击（攻击侧由守卫承担） ----
            var curtain = Spawn(p2, 2, 5, Attribute.KeywordRules.Taunt);
            var other = Spawn(p2, 3, 3);
            var attacker = Spawn(p1, 4, 4);
            var combat = core.CombatSystem;
            Assert(combat.CanAttackTarget(attacker, p2, p1) && combat.CanAttackTarget(attacker, other, p1)
                   && combat.CanAttackTarget(attacker, curtain, p1),
                   "帷幕（2026-09-13 更名）：不拦攻击——角色/非帷幕/帷幕随从均可指（攻击侧由守卫承担）");

            // ---- 2. 帷幕：效果侧选择层（选一/随机收窄；全取不受限） ----
            var manualDef = new CardCore.EffectDefinition
            {
                Id = "VERIFY_TAUNT_M", SelectionMode = CardCore.SelectionMode.Single,
                TargetCount = 1, TargetDomain = new List<int>(enemyKinds),
            };
            var manualPicked = CardCore.Attribute.EffectHandlerRegistry
                .ResolveCompositionTargetsAsync(manualDef, ctx).GetAwaiter().GetResult();
            Assert(manualPicked.Count == 1 && manualPicked[0] == curtain,
                   "帷幕效果侧：选一档选择收窄为帷幕卡（角色/非帷幕随从不可选）");

            var randDef = new CardCore.EffectDefinition
            {
                Id = "VERIFY_TAUNT_R", SelectionMode = CardCore.SelectionMode.Single,
                RandomTarget = true,
                TargetCount = 1, TargetDomain = new List<int>(enemyKinds),
            };
            var randPicked = CardCore.Attribute.EffectHandlerRegistry
                .ResolveCompositionTargetsAsync(randDef, ctx).GetAwaiter().GetResult();
            Assert(randPicked.Count == 1 && randPicked[0] == curtain,
                   "帷幕效果侧：随机抽取池收窄为帷幕卡（指定与随机同受限）");

            var fullDef = new CardCore.EffectDefinition
            {
                Id = "VERIFY_TAUNT_F", SelectionMode = CardCore.SelectionMode.Whole,
                TargetDomain = new List<int>(enemyKinds),
            };
            var fullPicked = CardCore.Attribute.EffectHandlerRegistry
                .ResolveCompositionTargetsAsync(fullDef, ctx).GetAwaiter().GetResult();
            Assert(fullPicked.Contains(other) && fullPicked.Contains(curtain),
                   "帷幕效果侧：全取档全域不受限（范围波及照常命中全部）");

            // ---- 3. 帷幕：外给目标收口（声明期 UI/AI 直给目标） ----
            var external = new List<Entity> { p2, other, curtain };
            var filtered = CardCore.TargetResolver.FilterPreselectedTargets(external, p1, p1, core.ZoneManager);
            Assert(filtered.Count == 1 && filtered[0] == curtain,
                   "帷幕：外给目标收口——非法目标剔除，仅留帷幕卡");

            // ---- 4. 紊乱：不可指角色（候选域 + 攻击 + 外给收口） ----
            var sick = Spawn(p1, 3, 3);
            sick.AddCounters(Attribute.KeywordRules.RushSicknessCounter, 1);
            var sickCtx = new EffectExecutionContext
            {
                Source = sick,
                Controller = p1,
                ZoneManager = core.ZoneManager,
                ElementPool = core.ElementPool,
            };
            var cands = CardCore.Attribute.EffectHandlerRegistry.ResolveCandidates(enemyKinds, "", sickCtx);
            Assert(!cands.Contains(p2) && cands.Contains(other),
                   "紊乱：效果候选域剔除角色（源紊乱的效果不可指角色）");
            Assert(!combat.CanAttackTarget(sick, p2, p1) && combat.CanAttackTarget(sick, other, p1),
                   "紊乱：攻击不可指角色、随从照常");

            p1.AddCounters(Attribute.KeywordRules.RushSicknessCounter, 1); // 施法者紊乱（法术来源=角色）
            var extSick = CardCore.TargetResolver.FilterPreselectedTargets(
                new List<Entity> { p2, curtain }, p1, p1, core.ZoneManager);
            Assert(extSick.Count == 1 && extSick[0] == curtain,
                   "紊乱：外给目标收口剔除角色（帷幕收窄同批生效）");
            p1.AddCounters(Attribute.KeywordRules.RushSicknessCounter, -1); // 清理

            RetireCards(core, p1, attacker, sick);
            RetireCards(core, p2, curtain, other);
        }

        // ======================================== 全域组合表达（2026-09-21 固有全域原子退役） ========================================

        /// <summary>
        /// 全域语义端到端（2026-09-21 定案：固有全域原子已退役，全域=组合期 TargetKinds 定域 +
        /// SelectionMode 全取档）——行为面：打出后对域内全部有生命单位（含角色）结算，无弹窗。
        /// （2026-10-09 清理：①计价锚（全取按期望目标数4=红6）删除——2026-10-05 档位化后全取档=3，旧期望4 口径过时。）
        /// </summary>
        private static void TestFullDomainCompose(GameCore core, Player p1, Player p2)
        {
            EnsureMainPhase(core, p1);

            // ---- 4. 行为端到端：对双方全部有生命单位（含角色）结算，无弹窗 ----
            int seq = 0;
            Card Spawn(Player owner, int power, int life)
            {
                var data = new CardData
                {
                    ID = "VERIFY_SWEEP_" + (++seq), CardName = "SW" + seq,
                    Supertype = Cardtype.Creature, Power = power, Life = life,
                };
                var card = new CardWrapper(data);
                card.SetController(owner);
                Assert(core.ZoneManager.TryAddToBattlefield(card, owner), $"固有全域段合成随从入场（{data.CardName}）");
                card.Untap();
                return card;
            }

            var a1 = Spawn(p1, 2, 5);
            var a2 = Spawn(p1, 3, 5);
            var b1 = Spawn(p2, 2, 5);
            var b2 = Spawn(p2, 3, 5);

            var pool1 = core.ElementPool.GetPool(p1);
            var snap = new Dictionary<ManaType, int>(pool1.TempElements);
            try
            {
                FillSixColors(pool1, 99);

                var sweepCardData = new CardData
                {
                    ID = "VERIFY_SWEEP_CR", CardName = "验证全域伤害", Supertype = Cardtype.Spell,
                    Cost = CostOf((ManaType.Red, 4)),
                };
                sweepCardData.Effects.Add(new CardEffectData
                {
                    Id = "VERIFY_SWEEP_ONPLAY",
                    TriggerTiming = (int)TriggerTiming.OnPlay,
                    SelectionMode = (int)CardCore.SelectionMode.WholeUnion, // 全域=组合期全取档（2026-09-21）
                    AtomicEffects = new List<AtomicEffectEntry>
                    {
                        // 双侧域 {1,2}（SideLock=0 不属错边）；显式费红4=1×1×期望4 口径
                        AtomRefs.New(CardCore.AtomicEffectType.DealDamage, value: 1, kinds: new List<int> { 1, 2 })
                    },
                });
                var sweepCard = InjectCard(core, p1, sweepCardData);
                int p1Life = p1.Life, p2Life = p2.Life;
                Assert(GameActions.PlayCard(core, p1, sweepCard), "打出全域伤害（红4）");
                GameActions.DrainStack(core);

                Assert(a1.GetLife() == 4 && a2.GetLife() == 4 && b1.GetLife() == 4 && b2.GetLife() == 4
                       && p1.Life == p1Life - 1 && p2.Life == p2Life - 1,
                       "全域行为：双方全部生物与角色各受 1（全取档结算，域={1,2} 含角色）");
            }
            finally
            {
                foreach (var kv in snap) pool1.TempElements[kv.Key] = kv.Value;
            }

            RetireCards(core, p1, a1, a2);
            RetireCards(core, p2, b1, b2);
        }

        // ======================================== 牺牲原子（2026-10-09 舍弃并档定案） ========================================

        /// <summary>
        /// 牺牲原子端到端：①表行锚（黑5 Polarity=-1 域 1,2,3,4 filter "Player"=仅角色）；
        /// ②候选域=双方角色（单位不入候选——Player 过滤激活）；③行为：目标玩家（持有者）自行选择
        /// 一个己方战场单位（生物+无生命单位，摒弃并池）**直送墓地**——非效果死亡：
        /// 不发 CardDestroyEvent（死亡时点/消灭替代/击杀统计不触发），不灭无从拦
        ///（非死亡路径；headless 自动选首个；多单位时走交互 Chooser=持有者）。
        /// </summary>
        private static void TestSacrificeAtom(GameCore core, Player p1, Player p2)
        {
            EnsureMainPhase(core, p1);

            // ---- 1. 表行锚 ----
            var cfg = CardCore.Attribute.AtomicEffectTable.GetByType(AtomicEffectType.Sacrifice);
            Assert(cfg != null && cfg.TotalUnitCost == 5f && cfg.Polarity == -1f
                   && cfg.GetTargetKindList().SequenceEqual(new[] { 1, 2 })
                   && (cfg.TargetFilter ?? "") == "Player"
                   && ElementAffinities.GetAffinityForEffect(AtomicEffectType.Sacrifice).PrimaryColor == ManaType.Black,
                   "牺牲表行：黑5 Pol=-1 域={1,2} filter=Player（仅角色；域随表收窄——无生命域已剔，2026-10-09 舍弃并档调价 3→5）");

            // ---- 2. 候选域=仅角色（Player 过滤激活；域=战场四单位类） ----
            var ctx = new EffectExecutionContext
            {
                Source = p1,
                Controller = p1,
                ZoneManager = core.ZoneManager,
                ElementPool = core.ElementPool,
            };
            var candidates = CardCore.Attribute.EffectHandlerRegistry.ResolveCandidates(
                new List<int> { (int)CardCore.TargetKind.OwnLivingUnit, (int)CardCore.TargetKind.EnemyLivingUnit,
                                (int)CardCore.TargetKind.OwnNonLivingUnit, (int)CardCore.TargetKind.EnemyNonLivingUnit },
                "Player", ctx);
            Assert(candidates.Contains(p1) && candidates.Contains(p2) && candidates.All(c => c is Player),
                   "牺牲候选域：仅双方角色（Player 过滤滤除单位）");

            // ---- 3. 行为：持有者交出一个单位（直送非死亡；不灭无从拦） ----
            int seq = 0;
            Card Spawn(Player owner, int power, int life, params string[] keywords)
            {
                var data = new CardData
                {
                    ID = "VERIFY_SAC_" + (++seq), CardName = "SAC" + seq,
                    Supertype = Cardtype.Creature, Power = power, Life = life,
                };
                foreach (var kw in keywords) data.Keywords.Add(kw);
                var card = new CardWrapper(data);
                card.SetController(owner);
                Assert(core.ZoneManager.TryAddToBattlefield(card, owner), $"牺牲段合成随从入场（{data.CardName}）");
                card.Untap();
                return card;
            }

            var foeIndestructible = Spawn(p2, 3, 3, Attribute.KeywordRules.Indestructible); // 唯一单位=不灭——自动选它
            var mine = Spawn(p1, 2, 2); // p1 唯一单位

            var atom = new CardCore.AtomicEffectInstance { Type = AtomicEffectType.Sacrifice, Value = 1 };
            var execCtx = new EffectExecutionContext
            {
                Source = p1,
                Controller = p1, // 施法者=p1；送墓归属=各自持有者
                ZoneManager = core.ZoneManager,
                ElementPool = core.ElementPool,
                Targets = new List<Entity> { p2, p1 }, // 作用对象=双方角色
            };
            int sacDestroyEvents = 0;
            void OnSacDestroy(CardDestroyEvent e) => sacDestroyEvents++;
            EventManager.Instance.Subscribe<CardDestroyEvent>(OnSacDestroy);
            Assert(CardCore.Attribute.EffectHandlerRegistry.ExecuteEffect(atom, execCtx), "牺牲原子执行（作用对象=双方角色）");
            EventManager.Instance.Unsubscribe<CardDestroyEvent>(OnSacDestroy);

            Assert(!foeIndestructible.IsAlive && core.ZoneManager.IsCardInZone(foeIndestructible, p2, Zone.Graveyard),
                   "牺牲：对手持有者交出唯一单位（直送非死亡——不灭无从拦）");
            Assert(!mine.IsAlive && core.ZoneManager.IsCardInZone(mine, p1, Zone.Graveyard),
                   "牺牲：己方持有者同批交出（送墓归属=各自持有者）");
            Assert(sacDestroyEvents == 0, "牺牲非死亡：全批不发 CardDestroyEvent（死亡时点不触发）");

            // ---- 4. 帷幕豁免：牺牲类选择权在目标方——帷幕只约束对手的选择 ----
            var curtain = Spawn(p2, 2, 5, Attribute.KeywordRules.Taunt);
            var p1Second = Spawn(p1, 2, 2);
            var sacDef = CardEffectConverter.ConvertOne(new CardEffectData
            {
                Id = "VERIFY_SAC_CURTAIN",
                // 2026-09-13 修复：缺省 SelectionMode=-1(None) 会让组合解析早退返回空目标——
                // 显式选多·多范围（牺牲域跨双方角色，第十三批 TDM 探针同款坑）；
                // TargetCount 缺省兜底 1 只选单目标，断言要求双方角色都在 → 显式 2
                SelectionMode = (int)CardCore.SelectionMode.MultipleUnion,
                TargetCount = 2,
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(CardCore.AtomicEffectType.Sacrifice, value: 1) },
            }, "VERIFY_SAC_CURTAIN");
            var curtainPicked = CardCore.Attribute.EffectHandlerRegistry
                .ResolveCompositionTargetsAsync(sacDef, ctx).GetAwaiter().GetResult();
            Assert(curtainPicked.Contains(p2) && curtainPicked.Contains(p1),
                   "牺牲豁免帷幕：对手有帷幕卡时角色目标照常可选（选择权在持有者）");
            var dmgDef2 = new CardCore.EffectDefinition
            {
                Id = "VERIFY_SAC_VS_DMG", SelectionMode = CardCore.SelectionMode.Single,
                TargetCount = 1,
                TargetDomain = new List<int> { (int)CardCore.TargetKind.EnemyLivingUnit },
            };
            var dmgPicked2 = CardCore.Attribute.EffectHandlerRegistry
                .ResolveCompositionTargetsAsync(dmgDef2, ctx).GetAwaiter().GetResult();
            Assert(dmgPicked2.Count == 1 && dmgPicked2[0] == curtain,
                   "对照：普通效果仍受帷幕收窄（对方侧仅帷幕卡）——豁免只给 edict");
            RetireCards(core, p1, p1Second);
            RetireCards(core, p2, curtain);

            // ---- 5. 摒弃并池（2026-10-09 舍弃并档）：无生命单位入牺牲池，直送墓 ----
            var p2Enchant = new CardData
            {
                ID = "VERIFY_SAC_ENCH", CardName = "牺牲标的结界", Supertype = Cardtype.Enchantment, Power = 0, Life = 0,
            };
            var enchant = new CardWrapper(p2Enchant);
            enchant.SetController(p2);
            Assert(core.ZoneManager.TryAddToBattlefield(enchant, p2), "牺牲段：结界入场（无生命单位）");
            var sacCtx2 = new EffectExecutionContext
            {
                Source = p1,
                Controller = p1,
                ZoneManager = core.ZoneManager,
                ElementPool = core.ElementPool,
                Targets = new List<Entity> { p2 }, // p2 唯一单位=结界——自动选它
            };
            Assert(CardCore.Attribute.EffectHandlerRegistry.ExecuteEffect(atom, sacCtx2), "牺牲原子再执行（作用对象=对方角色）");
            Assert(core.ZoneManager.IsCardInZone(enchant, p2, Zone.Graveyard),
                   "牺牲并池：持有者交出唯一无生命单位（结界直送墓——摒弃覆盖并入牺牲）");

            // ---- 6. 地牌退役（2026-10-09）：元素池地牌非"单位"——不入牺牲候选，空场空转 ----
            var landData = new CardData
            {
                ID = "VERIFY_SAC_LAND", CardName = "牺牲外之地", Supertype = Cardtype.Creature, Power = 0, Life = 1,
            };
            var land = new CardWrapper(landData);
            land.SetController(p2);
            // 正式放地路径（GameActions.AddToElementPool）是双写（池私有列表 + ElementPool 容器）——
            // 沿旧摒弃段夹具口径补容器登记
            Assert(core.ElementPool.AddCardToPool(land, p2), "牺牲段：地牌入池");
            core.ZoneManager.GetZoneContainer(p2).Add(land, Zone.ElementPool);
            CardCore.Attribute.EffectHandlerRegistry.ExecuteEffect(atom, sacCtx2); // p2 战场空·仅池内地牌——空转
            Assert(!core.ZoneManager.GetCards(p2, Zone.Graveyard).Contains(land)
                   && core.ZoneManager.GetCards(p2, Zone.ElementPool).Contains(land),
                   "牺牲候选=战场单位：地牌（元素池）不入池——旧摒弃地牌覆盖随并档退役");
        }

        // ======================================== 守护配对（2026-10-08 配对制改版） ========================================

        /// <summary>
        /// 守护=关键词（表行位 8 拉黑——退出连接光环族）：登场/授予时弹选一个己方目标（单位或角色），
        /// 其受到的伤害改写为守护者自身承受（GuardianRules 配对表，改写在 ApplyDamage 咽喉单跳）。
        /// 无限次直到守护者死亡/离场：离场即断链；墓地复活=新入场重新弹选（旧关系必然不残留，
        /// 2026-10-08 用户定案）；复生原地留场配对保持。无头验证：TargetSelectionService 未注册 UI
        /// → AutoSelect 取首候选（候选序=己方战场序+己方角色，构造确定性）。
        /// 旧三形态（箭头光环改写/扫场护角色/关键词挂角色）与初版计数体系均已退役。
        /// </summary>
        private static void TestGuardianRelay(GameCore core, Player p1, Player p2)
        {
            EnsureMainPhase(core, p1);

            // 清场隔离：配对候选=己方战场，须控制 AutoSelect 取首的确定性
            RetireCards(core, p1, core.ZoneManager.GetCards(p1, Zone.Battlefield).ToArray());
            RetireCards(core, p2, core.ZoneManager.GetCards(p2, Zone.Battlefield).ToArray());

            var used = new List<Card>();
            Card Make(Player owner, int power, int life, params string[] keywords)
            {
                var data = new CardData { ID = "VERIFY_GUARD_" + used.Count, CardName = "守" + used.Count };
                data.Supertype = Cardtype.Creature;
                data.Power = power;
                data.Life = life;
                foreach (var kw in keywords) data.Keywords.Add(kw);
                var card = new CardWrapper(data);
                card.SetController(owner);
                Assert(core.ZoneManager.TryAddToBattlefield(card, owner), "守护段入场：" + data.ID);
                used.Add(card);
                return card;
            }

            try
            {
                // ---- 1. 登场弹选建立配对（ward 先入场=首候选，守护者入场自动选它）----
                var ward = Make(p1, 1, 10);
                var guard = Make(p1, 2, 20, Attribute.KeywordRules.Guardian);
                Assert(guard.HasKeyword(Attribute.KeywordRules.Guardian), "守护：印刷关键词入场物化");
                Assert(ReferenceEquals(Attribute.GuardianRules.FindGuardian(ward), guard),
                       "守护配对：登场弹选首候选=先入场的己方单位（无头 AutoSelect 确定性）");
                Assert(ReferenceEquals(Attribute.GuardianRules.FindWard(guard), ward),
                       "守护正查：FindWard（状态段书写口 2026-10-16）=配对表如实回 ward");

                // ---- 2. ward 受伤改写为守护者承受（被守护者不掉血；无限次不设闸）----
                int wardLife = ward.GetLife();
                Attribute.KeywordRules.ApplyDamage(null, ward, 3, false);
                Attribute.KeywordRules.ApplyDamage(null, ward, 2, false);
                Assert(ward.GetLife() == wardLife && guard.GetLife() == 15,
                       "守护改写：ward 两笔 3+2 全转守护者（20−5=15，无限次、被守护者不掉血）");

                // ---- 3. 守护者离场 → 断链 → ward 恢复全额 ----
                core.ZoneManager.MoveCard(guard, p1, Zone.Battlefield, Zone.Graveyard);
                wardLife = ward.GetLife();
                Attribute.KeywordRules.ApplyDamage(null, ward, 2, false);
                Assert(ward.GetLife() == wardLife - 2 && Attribute.GuardianRules.FindGuardian(ward) == null,
                       "守护断链：守护者离场（死亡/弹回/流放统一口）配对清除，ward 恢复全额");
                Assert(Attribute.GuardianRules.FindWard(guard) == null,
                       "守护正查断链：离场后 FindWard 亦为 null（状态段不残留）");

                // ---- 4. 墓地复活=新登场重新弹选（旧关系不残留；复活后首候选仍是 ward）----
                Assert(core.ZoneManager.TryMoveToBattlefield(guard, p1, Zone.Graveyard),
                       "守护复活：苏生入场成功");
                Assert(ReferenceEquals(Attribute.GuardianRules.FindGuardian(ward), guard),
                       "守护复活重选：墓地复活=新入场事件→重新弹选（旧关系不残留，配对重建）");
                wardLife = ward.GetLife();
                int guardLife = guard.GetLife();
                Attribute.KeywordRules.ApplyDamage(null, ward, 1, false);
                Assert(ward.GetLife() == wardLife && guard.GetLife() == guardLife - 1,
                       "守护复活后再挡：新配对生效");

                // ---- 5. 单跳防链式：直接打守护者本身不再改写 ----
                guardLife = guard.GetLife();
                Attribute.KeywordRules.ApplyDamage(null, guard, 1, false);
                Assert(guard.GetLife() == guardLife - 1, "守护单跳：伤害落在守护者本身不改写（guardRerouted）");

                // ---- 6. 己方角色作为被守护目标（清至无其他己方单位→首候选=角色）----
                RetireCards(core, p1, new[] { ward, guard });
                var guard2 = Make(p1, 2, 6, Attribute.KeywordRules.Guardian);
                Assert(ReferenceEquals(Attribute.GuardianRules.FindGuardian(p1), guard2),
                       "守护角色目标：己方无其他单位时弹选首候选=角色（候选末位兜底）");
                Assert(ReferenceEquals(Attribute.GuardianRules.FindWard(guard2), p1),
                       "守护正查（角色目标）：FindWard 回 Player 实体（状态段渲染【己方/对方角色】）");
                int p1Life = p1.Life;
                int g2Life = guard2.GetLife();
                Attribute.KeywordRules.ApplyDamage(null, p1, 4, false);
                Assert(p1.Life == p1Life && guard2.GetLife() == g2Life - 4,
                       "守护改写（角色）：己方角色受伤改由守护者承受");
            }
            finally
            {
                foreach (var c in used)
                {
                    var owner = c.GetController() ?? p1;
                    core.ZoneManager.GetZoneContainer(owner).Remove(c, Zone.Battlefield);
                    core.ZoneManager.GetZoneContainer(owner).Remove(c, Zone.Graveyard);
                }
            }
        }

        // ======================================== 触发上限（2026-09-13 定案） ========================================

        /// <summary>
        /// 触发式每回合上限端到端：①表锚——坚韧行已退役（2026-09-13 光环化），触发上限不可修改位
        /// TriggerCapImmutable 亦于 2026-10-07 删除（零声明行——恒无限覆写链随坚韧光环化退役）；
        /// ②converter 默认口径——普通原子未声明=1（一回合一次）、显式 N/无限采纳；
        /// ③运行时闸门——同一效果一回合第 2 次触发被静默丢弃；显式无限（-1）不受限。
        /// 记账走既有 EffectUsageTracker（RecordActivation 全量结算记账，OnNewTurn 清零）。
        /// </summary>
        private static void TestTriggerCap(GameCore core, Player p1, Player p2)
        {
            EnsureMainPhase(core, p1);

            // ---- 1. 表锚：GrantToughness 行在表且回归指示物族（2026-10-08 坚韧指示物化——挂层走
            //      GrantToughnessHandler）；TriggerCapImmutable 位 2026-10-07 已删（零声明行），
            //      坚韧与触发上限无交集（指示物无触发概念）----
            Assert(CardCore.Attribute.AtomicEffectTable.GetByType(AtomicEffectType.GrantToughness) != null,
                   "触发上限表锚：GrantToughness 行在表（坚韧指示物化——行仅作授予原子，不再承载恒无限覆写）");

            // ---- 2. converter 默认口径 ----
            CardCore.EffectDefinition Convert(CardEffectData d) => CardEffectConverter.ConvertOne(d, "VERIFY_CAP");
            var normal = Convert(new CardEffectData
            {
                Id = "VERIFY_CAP_1",
                TriggerTiming = (int)TriggerTiming.OnDraw, // 触发式
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(CardCore.AtomicEffectType.DrawCard, value: 1) },
            });
            Assert(normal.TriggerLimitPerTurn == 1, "默认口径：普通原子触发式未声明 → 一回合一次");

            var declared = Convert(new CardEffectData
            {
                Id = "VERIFY_CAP_2",
                TriggerTiming = (int)TriggerTiming.OnDraw,
                TriggerLimitPerTurn = 2,
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(CardCore.AtomicEffectType.DrawCard, value: 1) },
            });
            Assert(declared.TriggerLimitPerTurn == 2, "可修改：组合期声明 N=2 采纳");

            var unlimited = Convert(new CardEffectData
            {
                Id = "VERIFY_CAP_3",
                TriggerTiming = (int)TriggerTiming.OnDraw,
                TriggerLimitPerTurn = -1,
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(CardCore.AtomicEffectType.DrawCard, value: 1) },
            });
            Assert(unlimited.TriggerLimitPerTurn == -1, "可修改：组合期显式无限（-1）采纳");

            var unlimitedDeclared = Convert(new CardEffectData
            {
                Id = "VERIFY_CAP_4",
                TriggerTiming = (int)TriggerTiming.OnDraw,
                TriggerLimitPerTurn = -1, // 可修改原子的显式无限（计价 ×4 无上限档）
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(CardCore.AtomicEffectType.DrawCard, value: 1) },
            });
            Assert(unlimitedDeclared.TriggerLimitPerTurn == -1, "可修改：组合期显式无限（-1）采纳");

            // ---- 2b. 触发上限计价（2026-10-05 档位化，表 PricingTier：1:1 / 2:1.5 / 3:2 / 无上限:4；
            //      原 1.2^(N-1) 连乘口径退役）----
            CardCore.EffectDefinition TrigDef(int limit) => CardEffectConverter.ConvertOne(new CardEffectData
            {
                Id = $"VERIFY_CAP_P{limit}",
                TriggerTiming = (int)TriggerTiming.OnDraw,
                TriggerLimitPerTurn = limit,
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(CardCore.AtomicEffectType.DealDamage, value: 5) },
            }, "VERIFY_CAP_P");
            int RedCostOf(CardCore.EffectDefinition d) => (int)CardCore.CostDerivationService.DeriveElementCosts(d)[CardCore.ManaType.Red];
            float RedCostF(CardCore.EffectDefinition d) => CardCore.CostDerivationService.DeriveElementCosts(d)[CardCore.ManaType.Red];
            Assert(RedCostOf(TrigDef(1)) == 5, "触发计价：N=1 不乘（红5）");
            Assert(System.Math.Abs(RedCostF(TrigDef(2)) - 7.5f) < 0.01f,
                   $"触发计价：N=2 ×1.5（5→7.5 小数直通不取整；实际 {RedCostF(TrigDef(2)):0.###}）");
            Assert(RedCostOf(TrigDef(3)) == 10, "触发计价：N=3 ×2（5→10）");
            Assert(RedCostOf(TrigDef(-1)) == 20, "触发计价：显式无限 ×4（5→20）");

            // ---- 3. 运行时闸门：同回合第 2 次触发丢弃；无限档不受限 ----
            var src = SpawnCapSource(core, p1);
            var queue = core.StackEngine;
            var cappedDef = normal; // 限 1
            queue.AddPendingEffect(MakeAutoTrigger(cappedDef, src, p1));
            queue.AddPendingEffect(MakeAutoTrigger(cappedDef, src, p1));
            core.StackEngine.ProcessTriggeredEffects();
            Assert(core.StackEngine.StackSize == 1,
                   "触发闸门：一回合一次的效果第 2 次触发被静默丢弃（栈上仅 1 个）");
            GameActions.DrainStack(core);
            Assert(core.StackEngine.IsEmpty, "触发闸门：结算排干");

            // 已触发 1 次后再来 → 仍被拦
            queue.AddPendingEffect(MakeAutoTrigger(cappedDef, src, p1));
            core.StackEngine.ProcessTriggeredEffects();
            Assert(core.StackEngine.IsEmpty, "触发闸门：本回合已用尽后再触发照拦（台账按 effect.Id 记）");

            // 显式无限档（-1，可修改原子声明）：连发 3 次全上栈
            queue.AddPendingEffect(MakeAutoTrigger(unlimitedDeclared, src, p1));
            queue.AddPendingEffect(MakeAutoTrigger(unlimitedDeclared, src, p1));
            queue.AddPendingEffect(MakeAutoTrigger(unlimitedDeclared, src, p1));
            core.StackEngine.ProcessTriggeredEffects();
            Assert(core.StackEngine.StackSize == 3, "触发闸门：显式无限档（-1）连发不受限");

            GameActions.DrainStack(core);
            RetireCards(core, p1, src);
        }

        private static Card SpawnCapSource(GameCore core, Player p1)
        {
            var data = new CardData
            {
                ID = "VERIFY_CAP_SRC", CardName = "闸门源", Supertype = Cardtype.Creature, Power = 2, Life = 5,
            };
            var card = new CardWrapper(data);
            card.SetController(p1);
            Assert(core.ZoneManager.TryAddToBattlefield(card, p1), "触发上限段：源随从入场");
            card.Untap();
            return card;
        }

        private static CardCore.PendingEffect MakeAutoTrigger(CardCore.EffectDefinition def, Card source, Player controller)
            => CardCore.PendingEffect.Create(def, source, controller, controller,
                CardCore.PhaseType.Main, 0);

        // ======================================== 分支体系正规化（2026-09-13 定案） ========================================

        /// <summary>
        /// 固定分支门附加费（伤害命中1/击杀2/宣言命中2，灰；奖励原子 0 费）+ 动态分支引擎端到端：
        /// 倒计时（奖励推导费换算回合，1费=1回合；归零发奖并重置）/ 运势（2d6 双&gt;x，x=1 钉种子 20 回合内必中）/
        /// 拼点（双方牌库各随机取样一张生物比攻击力，差额≥门槛；只读取样；无生物=0）。
        /// </summary>
        private static void TestBranchEngines(GameCore core, Player p1, Player p2)
        {
            EnsureMainPhase(core, p1);

            // ---- 1. 固定分支条件附加费（2026-10-05 载荷口径：槽级 Branch 载荷承载条件+Then）----
            CardCore.EffectDefinition GateDef(string gateId)
            {
                var data = new CardEffectData
                {
                    Id = "VERIFY_GATE_" + gateId,
                    AtomicEffects = new List<AtomicEffectEntry>
                    {
                        // 产出条件族（DmgKillsTarget/DeclareHit）走 Outcome 载荷——与 converter 折叠归类同源；
                        // 局面门走 Gate 载荷（2026-10-09 还原：达标奖励/不达标无事）
                        CardCore.ComposerCatalog.IsOutcomeCondition(gateId)
                            ? OutcomeAtom(CardCore.AtomicEffectType.DealDamage, 3, gateId,
                                AtomRefs.New(CardCore.AtomicEffectType.DrawCard, value: 1))
                            : GateAtom(CardCore.AtomicEffectType.DealDamage, 3, gateId,
                                AtomRefs.New(CardCore.AtomicEffectType.DrawCard, value: 1)),
                    },
                };
                return CardEffectConverter.ConvertOne(data, "VERIFY_GATE");
            }
            int GrayOf(CardCore.EffectDefinition d) => (int)CardCore.CostDerivationService.DeriveElementCosts(d)[CardCore.ManaType.Gray];
            int BlueOf(CardCore.EffectDefinition d) => (int)CardCore.CostDerivationService.DeriveElementCosts(d)[CardCore.ManaType.Blue];
            // 2026-09-14 用户口径定案（废除 09-13 灰费）：分支=纯校验上限，零计价——奖励免费，条件不产生任何费用
            Assert(GrayOf(GateDef("DmgKillsTarget")) == 0, "产出条件零计价：击杀门不产生灰费（奖励免费）");
            Assert(GrayOf(GateDef("DeclareHit")) == 0, "产出条件零计价：宣言门不产生灰费");
            Assert(GrayOf(GateDef("LifeBelowOpp")) == 0, "局面门零计价：有限分支还原后同口径（奖励免费）");
            Assert(BlueOf(GateDef("DmgKillsTarget")) == 0, "条件奖励免费：then 抽1（锚蓝2）不计入费用");
            CardCore.EffectDefinition SubGateDef()
            {
                var data = new CardEffectData
                {
                    Id = "VERIFY_GATE_SUB",
                    AtomicEffects = new List<AtomicEffectEntry>
                    {
                        OutcomeAtom(CardCore.AtomicEffectType.DealDamage, 3, "DmgKillsTarget",
                            AtomRefs.New(CardCore.AtomicEffectType.Heal, value: 2)), // 锚1（回2命=1费）< 门预算2，合法
                    },
                };
                return CardEffectConverter.ConvertOne(data, "VERIFY_GATE_SUB");
            }
            Assert(GrayOf(SubGateDef()) == 0 && CardCore.CostDerivationService.DeriveElementCosts(SubGateDef())
                   [CardCore.ManaType.Green] == 0,
                   "奖励低于预算同样零计价（预算只是放置校验上限，不是收费）");

            // ---- 1b. 护甲部分吸收（毒刺/冰晶改写族 2026-10-15 整体退役——「剩余改写」断言随退；
            //      本段改钉现行护甲语义：逐层吸收、剩余照常落血）----
            var stingSrc = SpawnTier(core, p1, 3, 3);
            var stingTgt = SpawnTier(core, p2, 3, 5);
            // 防护层优先：圣盾完全挡住（全额吸收、盾层消耗）
            var shielded = SpawnTier(core, p2, 3, 5);
            shielded.AddCounters(CardCore.Attribute.CounterRules.DivineShieldCounter, 1, shielded); // 圣盾已指示物化
            int shieldLife = shielded.GetLife();
            CardCore.Attribute.KeywordRules.ApplyDamage(stingSrc, shielded, 3, true);
            Assert(shielded.GetLife() == shieldLife
                   && shielded.GetCounterCount(CardCore.Attribute.CounterRules.DivineShieldCounter) == 0,
                   "防护优先：圣盾完全挡住（盾层消耗、无落血）");
            RetireCards(core, p2, shielded);

            // 部分吸收：护甲 2 挡 3 伤中的 2 → 剩余 1 照常落血（护甲耗尽）
            var armoredTgt = SpawnTier(core, p2, 3, 5);
            armoredTgt.AddCounters(CardCore.Attribute.KeywordRules.ArmorCounter, 2);
            int armoredLife = armoredTgt.GetLife();
            CardCore.Attribute.KeywordRules.ApplyDamage(stingSrc, armoredTgt, 3, true);
            Assert(armoredTgt.GetLife() == armoredLife - 1
                   && armoredTgt.GetCounterCount(CardCore.Attribute.KeywordRules.ArmorCounter) == 0,
                   $"部分吸收：护甲逐层吃掉 2、剩余 1 照常落血（护甲耗尽）——实际 life {armoredLife}→{armoredTgt.GetLife()} 护甲×{armoredTgt.GetCounterCount(CardCore.Attribute.KeywordRules.ArmorCounter)}");
            RetireCards(core, p2, armoredTgt);
            RetireCards(core, p1, stingSrc);

            var wardUnit = SpawnTier(core, p1, 2, 5);
            wardUnit.AddKeyword(CardCore.Attribute.KeywordRules.Spellban, CardCore.KeywordLane.Temp, wardUnit);
            int wardLife = wardUnit.GetLife();
            CardCore.Attribute.KeywordRules.ApplyDamage(p2, wardUnit, 3, false);
            Assert(wardUnit.GetLife() == wardLife, "禁魔石：非战斗伤害变为 0");
            CardCore.Attribute.KeywordRules.ApplyDamage(p2, wardUnit, 3, true);
            Assert(wardUnit.GetLife() == wardLife - 3, "禁魔石对照：战斗伤害照常");
            RetireCards(core, p1, wardUnit);

            // ---- 2. 倒计时（槽级载荷）：奖励推导费换算回合；归零发奖并重置 ----
            // 引擎宿主用真实生物（0/0 结界会在 DrainStack 的 SBA 泵里被"防御归零"送墓——死亡计数段同款教训）
            var cdData = new CardData
            {
                ID = "VERIFY_ENGINE_CD", CardName = "验证倒计时", Supertype = Cardtype.Creature, Power = 3, Life = 3,
            };
            cdData.Effects.Add(new CardEffectData
            {
                Id = "VERIFY_CD_MAIN",
                // 主动时机=主干不随施放/入场自动结算（主动轮询制）——引擎宿主惰性化：
                // BranchEngines 只扫 def.Effects 的 Branch 载荷，主干 DrawCard 不在场外偷跑
                TriggerTiming = (int)CardCore.TriggerTiming.Activate_Active,
                AtomicEffects = new List<AtomicEffectEntry>
                {
                    // 主干=抽1（宿主原子，不入场不结算）；倒计时载荷 Then=抽1（锚价 2 → 2 回合）
                    EngineAtom(CardCore.AtomicEffectType.DrawCard, 1, CardCore.BranchEngineKind.Countdown, 0,
                        AtomRefs.New(CardCore.AtomicEffectType.DrawCard, value: 1)),
                },
            });
            var cdCard = new CardWrapper(cdData);
            cdCard.SetController(p1);
            Assert(core.ZoneManager.TryAddToBattlefield(cdCard, p1), "倒计时：引擎宿主入场");

            int deckBefore = core.ZoneManager.GetCards(p1, Zone.Deck).Count;
            // 入场挂 2 层（奖励锚价 2 → 2 回合）→ 每回合开始 -1 → 归零发奖（抽1）→ 重置回 2。
            // 拦截器跳过回合自动化（防回合抽牌污染牌库读数——拼点段同款口径）
            {
                var cdDef = CardEffectConverter.ConvertAll(cdData.Effects, cdData.ID)
                    .FirstOrDefault(d => d?.Effects != null && d.Effects.Any(a =>
                        a?.Branch?.EngineKind == CardCore.BranchEngineKind.Countdown));
                Crumb($"cd debug: CountdownTurns={cdDef?.Effects.FirstOrDefault(a => a?.Branch != null)?.Branch.CountdownTurns} counterAtStart={cdCard.GetCounterCount(CardCore.Attribute.CounterRules.CountdownCounter)} deck={deckBefore}");
            }
            var cdShield = new TurnStartAutomationShield();
            RuleHooks.RegisterTurnStartInterceptor(cdShield);
            try
            {
                // 2026-10-26 精准切分：回合自动化（含回合抽牌）已迁 RunStandbyAutomation——合成
                // TurnStartEvent 不再触发抽牌，倒计时 tick 显式调用（隔离盾随之只挡显式自动化路径；
                // 新语义=跳过闸连倒计时一并跳，本段隔离口径已由显式 tick 取代）
                CardCore.BranchEngines.RunCountdownTick(p1);
                GameActions.DrainStack(core);
                Crumb($"cd after t1: counter={cdCard.GetCounterCount(CardCore.Attribute.CounterRules.CountdownCounter)} deckNow={core.ZoneManager.GetCards(p1, Zone.Deck).Count}");
                Assert(core.ZoneManager.GetCards(p1, Zone.Deck).Count == deckBefore
                       && cdCard.GetCounterCount(CardCore.Attribute.CounterRules.CountdownCounter) == 1,
                       "倒计时：首回合 2→1 未归零不发奖（tick 显式调用，牌库只受引擎影响）");
                CardCore.BranchEngines.RunCountdownTick(p1);
                GameActions.DrainStack(core);
            }
            finally { RuleHooks.UnregisterTurnStartInterceptor(cdShield); }
            // 2026-10-16 时机定案：倒计时归零=待发不立即发奖——奖励按宿主声明时机发射；
            // 本夹具宿主为主动时机（惰性化），回合开始不自动发奖（牌库不动），计数照常重置。
            Assert(core.ZoneManager.GetCards(p1, Zone.Deck).Count == deckBefore,
                   "倒计时：归零=待发（按声明时机发射——主动时机宿主回合开始不自动发奖，牌库不动）");
            Assert(cdCard.GetCounterCount(CardCore.Attribute.CounterRules.CountdownCounter) == 2,
                   "倒计时：归零后重置回初值（2 层——奖励锚价 2 换算）");
            core.ZoneManager.MoveCard(cdCard, p1, Zone.Battlefield, Zone.Graveyard); // 清场（换区清计数）

            // ---- 3. 运势（槽级载荷）：2d6 双 > x；x 费灰；x=1 钉种子 20 回合内必中 ----
            var luckData = new CardData
            {
                ID = "VERIFY_ENGINE_LUCK", CardName = "验证运势", Supertype = Cardtype.Creature, Power = 3, Life = 3,
            };
            luckData.Effects.Add(new CardEffectData
            {
                Id = "VERIFY_LUCK_MAIN",
                // 2026-10-16 解绑：掷骰随**声明时机**的事件驱动（OnRoutedEvent）——须 OnTurnStart
                // 才会在回合事件上评估（Activate 无对应事件永不会被掷）；主干改 ModifyPower（抽牌
                // 主干会每回合自动抽 1 污染牌库读数——OnTurnStart 宿主无法再用主动惰性化）
                TriggerTiming = (int)CardCore.TriggerTiming.OnTurnStart,
                AtomicEffects = new List<AtomicEffectEntry>
                {
                    // 干干须为**表行原子**（ModifyPower 系运行时原子无表行——refId 解析失败会被
                    // 转换器整颗剔除、载荷陪葬）；GrantTaunt 无牌库/生命可观察副作用
                    EngineAtom(CardCore.AtomicEffectType.GrantTaunt, 1, CardCore.BranchEngineKind.LuckRoll, 1,
                        AtomRefs.New(CardCore.AtomicEffectType.DrawCard, value: 1)),
                },
            });
            var luckDef = CardEffectConverter.ConvertOne(luckData.Effects[0], luckData.ID);
            int luckGray = (int)CardCore.CostDerivationService.DeriveElementCosts(luckDef)[CardCore.ManaType.Gray];
            Assert(luckGray == 0, "运势计价（2026-09-15 废灰费）：x=纯概率门槛，零计价（奖励原子 0 费）");

            var luckCard = new CardWrapper(luckData);
            luckCard.SetController(p1);
            Assert(core.ZoneManager.TryAddToBattlefield(luckCard, p1), "运势：引擎宿主入场");
            deckBefore = core.ZoneManager.GetCards(p1, Zone.Deck).Count;
            GameRng.Reseed(20260913);
            bool luckHit = false;
            var luckEvents = new List<string>();
            void OnLuckApplied(CardCore.KeywordAppliedEvent e) { if (e?.Keyword == "运势") luckEvents.Add(e.Detail); }
            int luckTurnStarts = 0; int luckAdvanceFails = 0;
            void OnLuckTurnStart(TurnStartEvent e) => luckTurnStarts++;
            EventManager.Instance.Subscribe<CardCore.KeywordAppliedEvent>(OnLuckApplied);
            EventManager.Instance.Subscribe<TurnStartEvent>(OnLuckTurnStart);
            var luckShield = new TurnStartAutomationShield();
            RuleHooks.RegisterTurnStartInterceptor(luckShield); // 隔离回合抽牌——只测引擎自身发奖
            try
            {
                for (int i = 0; i < 20 && !luckHit; i++)
                {
                    // 裸 EventManager.Publish 不走路由（GameCore.PublishEventRouted）——掷骰评估
                    // 须真实回合流转，TurnStartEvent 才会经 GameCore 路由到 BranchEngines
                    var luckTurnPlayer = core.TurnEngine.TurnPlayer;
                    GameActions.SkipElementPool(core, luckTurnPlayer);
                    if (!EndTurnPumped(core, luckTurnPlayer)) luckAdvanceFails++;
                    GameActions.DrainStack(core);
                    if (core.ZoneManager.GetCards(p1, Zone.Deck).Count < deckBefore) luckHit = true;
                }
            }
            finally
            {
                RuleHooks.UnregisterTurnStartInterceptor(luckShield);
                EventManager.Instance.Unsubscribe<CardCore.KeywordAppliedEvent>(OnLuckApplied);
                EventManager.Instance.Unsubscribe<TurnStartEvent>(OnLuckTurnStart);
            }
            Crumb($"luck diag: turnStarts={luckTurnStarts} advanceFails={luckAdvanceFails} hits={luckEvents.Count}");
            Assert(luckHit, $"运势运行时：x=1（25/36 命中）真实回合流转 20 回合内至少中一次并执行奖励（掷骰随声明时机事件——2026-10-16 解绑；诊断：回合事件×{luckTurnStarts} 推进失败×{luckAdvanceFails} 命中×{luckEvents.Count}）");
            core.ZoneManager.MoveCard(luckCard, p1, Zone.Battlefield, Zone.Graveyard);

            // ---- 4. 拼点（槽级载荷）：双方牌库各随机取样一张生物，攻击力差 ≥ 门槛；只读取样；无生物=0 ----
            var clashData = new CardData
            {
                ID = "VERIFY_ENGINE_CLASH", CardName = "验证拼点", Supertype = Cardtype.Creature, Power = 3, Life = 3,
            };
            clashData.Effects.Add(new CardEffectData
            {
                Id = "VERIFY_CLASH_MAIN",
                TriggerTiming = (int)CardCore.TriggerTiming.Activate_Active, // 引擎宿主惰性化（同倒计时段注）
                AtomicEffects = new List<AtomicEffectEntry>
                {
                    EngineAtom(CardCore.AtomicEffectType.DrawCard, 1, CardCore.BranchEngineKind.Clash, 1,
                        AtomRefs.New(CardCore.AtomicEffectType.DrawCard, value: 1)),
                },
            });
            var clashDef = CardEffectConverter.ConvertOne(clashData.Effects[0], clashData.ID);
            int clashGray = (int)CardCore.CostDerivationService.DeriveElementCosts(clashDef)[CardCore.ManaType.Gray];
            Assert(clashGray == 0, "拼点计价（2026-09-15 门槛制）：门槛=奖励锚价（运行时差额≥门槛判），零计价");

            // 2026-10-04 改版（随机生物攻击力）：随机取样需确定性夹具——清双方牌库（移入墓地；
            // 后续 2026-09-22 段前有 PadDeck 15 张兜底），各置唯一生物（p1 攻5 / p2 攻2）→
            // 库内唯一，随机必中 → 差额 5−2=3 ≥ 门槛（抽1 锚价 2）→ 必中
            foreach (var c in core.ZoneManager.GetCards(p1, Zone.Deck).ToList())
                core.ZoneManager.MoveCard(c, p1, Zone.Deck, Zone.Graveyard);
            foreach (var c in core.ZoneManager.GetCards(p2, Zone.Deck).ToList())
                core.ZoneManager.MoveCard(c, p2, Zone.Deck, Zone.Graveyard);
            var topMine = new CardData { ID = "VERIFY_CLASH_TOP_M", CardName = "拼点生物M",
                Supertype = Cardtype.Creature, Power = 5, Life = 5 };
            var topFoe = new CardData { ID = "VERIFY_CLASH_TOP_F", CardName = "拼点生物F",
                Supertype = Cardtype.Creature, Power = 2, Life = 5 };
            core.ZoneManager.GetZoneContainer(p1).Add(new CardWrapper(topMine), Zone.Deck, DeckPosition.Top);
            core.ZoneManager.GetZoneContainer(p2).Add(new CardWrapper(topFoe), Zone.Deck, DeckPosition.Top);

            var clashCard = new CardWrapper(clashData);
            clashCard.SetController(p1);
            Assert(core.ZoneManager.TryAddToBattlefield(clashCard, p1), "拼点：引擎宿主入场");
            deckBefore = core.ZoneManager.GetCards(p1, Zone.Deck).Count;
            System.Func<Card, int> powerOf = c => (c as CardWrapper)?.GetData()?.Power ?? 0;
            Crumb($"clash pre: p1deck={deckBefore} myCreature={powerOf(core.ZoneManager.GetCards(p1, Zone.Deck).First())} "
                + $"foeCreature={powerOf(core.ZoneManager.GetCards(p2, Zone.Deck).First())} "
                + $"p2deck={core.ZoneManager.GetCards(p2, Zone.Deck).Count}");
            // 2026-10-09 触发改版：拼点由攻击宣言触发（仅攻击方战场引擎卡·无限次，无回合自动化干扰）——
            // 攻击宣言无回合抽牌副作用，无需拦截器；库内唯一生物（己方 5 / 对方 2）随机必中。
            EventManager.Instance.Publish(new AttackDeclarationEvent { AttackingPlayer = p1 });
            GameActions.DrainStack(core);
            Assert(core.ZoneManager.GetCards(p1, Zone.Deck).Count == deckBefore - 1,
                   "拼点运行时（2026-10-09 触发改版：攻击宣言·仅攻击方）：己方随机生物 5 − 对方 2 = 3 ≥ 门槛 2 → 执行奖励（抽 1）");
            Assert(core.ZoneManager.GetCards(p1, Zone.Hand).Any(c =>
                       (c as CardWrapper)?.GetData()?.ID == "VERIFY_CLASH_TOP_M"),
                   "拼点：随机取样只读不移牌——奖励抽 1 抽到的正是牌库中那张生物（取样未预耗）");
            core.ZoneManager.MoveCard(clashCard, p1, Zone.Battlefield, Zone.Graveyard);

            // ======================================== 2026-09-22 定案：死亡计数 / 元素充盈 / 状态门 / 改写门 =======

            // 本段抽牌断言密集——牌库保底（见 PadDeck 注释：抽干=净 0 / 疲劳判负两类失效模式）
            PadDeck(core, p1, 15, "VERIFY_0922_P1_");
            PadDeck(core, p2, 15, "VERIFY_0922_P2_");

            // ---- 5. 死亡计数（2026-10-19 手牌/场上改案）：此卡在手牌中或场上时计数双方生物死亡——
            //      计数 ≥ 门槛（Then 锚价推导，DrawCard(1)=2 → 门槛 2）即触发奖励（每次递增都触发，
            //      触发不清零）；满值 6 达满不再计数；使用（换区）后清零。
            //      （engineParam 旧值 2 为死数据不参与判定；旧「场上·本回合合计·每回合一发」口径退役）----
            var tollData = new CardData
            {
                ID = "VERIFY_ENGINE_TOLL", CardName = "验证死亡计数", Supertype = Cardtype.Creature, Power = 3, Life = 3,
            };
            tollData.Effects.Add(new CardEffectData
            {
                Id = "VERIFY_TOLL_MAIN",
                TriggerTiming = (int)CardCore.TriggerTiming.Activate_Active, // 引擎宿主惰性化（同倒计时段注）
                AtomicEffects = new List<AtomicEffectEntry>
                {
                    EngineAtom(CardCore.AtomicEffectType.DrawCard, 1, CardCore.BranchEngineKind.DeathToll, 2,
                        AtomRefs.New(CardCore.AtomicEffectType.DrawCard, value: 1)),
                },
            });
            var tollDef = CardEffectConverter.ConvertOne(tollData.Effects[0], tollData.ID);
            Assert(tollDef.Effects.Count == 1 && tollDef.Effects[0].Branch != null
                   && tollDef.Effects[0].Branch.Settle == CardCore.BranchSettleKind.Engine
                   && tollDef.Effects[0].Branch.EngineKind == CardCore.BranchEngineKind.DeathToll
                   && tollDef.Effects[0].Branch.Then.Count == 1,
                   "死亡计数：槽级载荷 → 主干原子 Branch 引擎 + Then 奖励转存");
            int tollGray = (int)CardCore.CostDerivationService.DeriveElementCosts(tollDef)[CardCore.ManaType.Gray];
            Assert(tollGray == 0, "死亡计数计价：引擎零计价（门槛=Then 锚价推导只是判定条件，非收费）");

            var tollCard = new CardWrapper(tollData);
            tollCard.SetController(p1);
            core.ZoneManager.GetZoneContainer(p1).Add(tollCard, Zone.Hand); // 手牌改案：手牌中即计数

            var tollVictims = new Card[9];
            for (int i = 0; i < tollVictims.Length; i++)
                tollVictims[i] = i % 2 == 0 ? SpawnTier(core, p2, 1, 1) : SpawnTier(core, p1, 1, 1); // 双方交替死
            int deckAtToll = core.ZoneManager.GetCards(p1, Zone.Deck).Count;
            System.Func<int> tollCount = () =>
                tollCard.GetCounterCount(CardCore.Attribute.CounterRules.DeathTollCounter);
            System.Func<string> tollState = () =>
                $"count={tollCount()} deck={core.ZoneManager.GetCards(p1, Zone.Deck).Count} tollZone={tollCard.GetZone()}";
            void TollKill(int i)
            {
                CardCore.Attribute.KeywordRules.ApplyDamage(i % 2 == 0 ? p2 : p1, tollVictims[i], 99, false);
                core.SBAEngine.CheckAndExecute(); // 直接 ApplyDamage 不经栈——手动泵 SBA 送墓（标死→死透→CardDestroyEvent）
                Crumb($"toll kill{i + 1}: " + tollState());
            }
            TollKill(0); // 对手侧 1 死
            Assert(tollCount() == 1 && core.ZoneManager.GetCards(p1, Zone.Deck).Count == deckAtToll,
                   "死亡计数：手牌中计数——对手生物死亡 +1（1 < 门槛 2 未触发）");
            TollKill(1); // 己方侧 +1 → 累计 2（双方口径）
            Assert(core.ZoneManager.GetCards(p1, Zone.Deck).Count == deckAtToll - 1 && tollCount() == 2,
                   "死亡计数：双方生物死亡累计 2 ≥ 门槛 2（推导）触发（抽 1）——触发不清零（计数保持 2）");
            TollKill(2); // 累计 3 ≥ 2 → 再触发
            TollKill(3); // 累计 4 ≥ 2 → 再触发
            TollKill(4); // 累计 5 ≥ 2 → 再触发
            Assert(core.ZoneManager.GetCards(p1, Zone.Deck).Count == deckAtToll - 4 && tollCount() == 5,
                   "死亡计数：[x,6] 区间每次递增都触发（抽 4）——无每回合一发上限");
            TollKill(5); // 累计 6 = 满值 → 末次触发
            Assert(core.ZoneManager.GetCards(p1, Zone.Deck).Count == deckAtToll - 5 && tollCount() == 6,
                   "死亡计数：满值 6 仍触发（6 ≥ 2，抽 5）——计数停在 6");
            TollKill(6); // 满值后死亡：不再计数不再触发
            Assert(tollCount() == 6 && core.ZoneManager.GetCards(p1, Zone.Deck).Count == deckAtToll - 5,
                   "死亡计数：满值 6 达满不再计数——超出死亡不 +1 不触发");
            core.ZoneManager.MoveCard(tollCard, p1, Zone.Hand, Zone.Battlefield); // 使用（打出）→ 换区
            Assert(tollCount() == 0, "死亡计数：使用后清零（系统指示物换区清）");
            TollKill(7); // 场上计数：累计 1
            Assert(tollCount() == 1 && core.ZoneManager.GetCards(p1, Zone.Deck).Count == deckAtToll - 5,
                   "死亡计数：场上同样计数（1 < 2 未触发）——手牌/场上双面口径");
            TollKill(8); // 场上累计 2 → 触发
            Assert(core.ZoneManager.GetCards(p1, Zone.Deck).Count == deckAtToll - 6 && tollCount() == 2,
                   "死亡计数：场上达标触发（抽 6）——清零重计后周期重启");
            core.ZoneManager.MoveCard(tollCard, p1, Zone.Battlefield, Zone.Graveyard);

            // ---- 6. 元素充盈（2026-10-20 判据六 ≥x）：仅自动·仅对手回合——对手（=当前回合玩家）出牌
            //      施放结算完成后，评估**施放者对手**（引擎主）战场充盈引擎：双方剩余元素总量
            //      （GetRemainingElementTotal=未横置地牌+钱包，支付先扣后算）之差（封顶 6）**大于等于**
            //      门槛（Then 锚价推导）即触发——首次达标即发、每次达标评估都可再触发，反复触发由
            //      效果行显式 limit 声明收口（单回合上限，缺省无限）；己方回合出牌不评估；
            //      非施放支付不触发。判据五「恰好等于 ==x」/判据四「地牌上限差」均退役----
            // 引擎宿主用真实生物：0/0 结界会在出牌结算后的 SBA 泵里被"防御归零"送墓（首跑实证），引擎只活半次
            var surgeData = new CardData
            {
                ID = "VERIFY_ENGINE_SURGE", CardName = "验证元素充盈", Supertype = Cardtype.Creature, Power = 3, Life = 3,
            };
            surgeData.Effects.Add(new CardEffectData
            {
                Id = "VERIFY_SURGE_MAIN",
                TriggerTiming = (int)CardCore.TriggerTiming.Activate_Active, // 引擎宿主惰性化（同倒计时段注）
                AtomicEffects = new List<AtomicEffectEntry>
                {
                    EngineAtom(CardCore.AtomicEffectType.DrawCard, 1, CardCore.BranchEngineKind.ManaSurplus, 1,
                        AtomRefs.New(CardCore.AtomicEffectType.DrawCard, value: 1)),
                },
            });
            var surgeCard = new CardWrapper(surgeData);
            surgeCard.SetController(p1);
            Assert(core.ZoneManager.TryAddToBattlefield(surgeCard, p1), "元素充盈：引擎宿主入场");
            PadDeck(core, p1, 12, "VERIFY_SURGE_DECK_");  // 引擎侧累计抽 6（门槛2×2 发 + 门槛6×3 发）+ 回合翻转抽牌
            PadDeck(core, p2, 20, "VERIFY_SURGE_DECK2_"); // 对手载体自抽 10 发 + 回合翻转抽牌——垫牌防疲劳干扰

            // 判据供能：无地牌纯钱包口径（剩余=钱包余额；钱包任一回合结束清空——EnsureMainPhase 后重灌）
            var surgePool = core.ElementPool.GetPool(p1);
            var surgePool2 = core.ElementPool.GetPool(p2);
            surgePool.PooledCards.Clear();
            surgePool2.PooledCards.Clear();
            void SurgeWallets(int p1Red, int p2Red)
            {
                foreach (ManaType t in System.Enum.GetValues(typeof(ManaType)))
                { surgePool.TempElements[t] = 0; surgePool2.TempElements[t] = 0; }
                surgePool.TempElements[ManaType.Red] = p1Red;
                surgePool2.TempElements[ManaType.Red] = p2Red;
            }
            // 对手（p2）出牌载体：Spell+DrawCard1（老口径可打出；载体抽牌落 p2 牌库不扰 p1 计数），
            // 红费自 p2 钱包扣（先扣后算——结算后 p2 剩余=钱包−实付）
            CardData SurgeSpell(float red = 1f)
            {
                var d = new CardData
                {
                    ID = "VERIFY_SURGE_SPELL", CardName = "充盈载体", Supertype = Cardtype.Spell,
                    Cost = CostOf((ManaType.Red, red)),
                };
                d.Effects.Add(new CardEffectData
                {
                    Id = "VERIFY_SURGE_SPELL_FX",
                    AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(CardCore.AtomicEffectType.DrawCard, value: 1) },
                });
                return d;
            }
            // 拒因直读（出牌失败时面包屑留痕——上游段落状态级联排查用）
            bool SurgeCast(CardData data, string tag)
            {
                var c = InjectCard(core, p2, data);
                if (!GameActions.PlayCard(core, p2, c, null, Zone.Hand, 0, out var why))
                { Crumb($"surge {tag} reject: {why}"); return false; }
                GameActions.DrainStack(core);
                return true;
            }
            int surgeDeck = -1; // 每次 EnsureMainPhase 后重钉基数（回合翻转抽牌会漂移）
            System.Func<string> surgeState = () =>
                $"remain1={core.ElementPool.GetRemainingElementTotal(p1)} remain2={core.ElementPool.GetRemainingElementTotal(p2)} "
                + $"deck1={core.ZoneManager.GetCards(p1, Zone.Deck).Count} surgeZone={surgeCard.GetZone()} "
                + $"turn={core.TurnEngine.TurnPlayer?.Name}";

            // 对手回合阶梯（2026-10-20 判据六 ≥x 语义·教学二段六同型——limit 未声明=无限）：p1 剩 6 恒定，
            // p2 从 6 起每打一张 1 费载体剩余 6→0 → 差 1..6 逐次递进；门槛 2 宿主差 1 未到不触发，
            // 差 2 首次达标触发，差 3 仍 ≥2 每次评估都再触发（恰等 ==x 语义随判据五退役）
            EnsureMainPhase(core, p2); // 对手（p2）回合主阶段——判据六评估窗
            SurgeWallets(p1Red: 6, p2Red: 6);
            surgeDeck = core.ZoneManager.GetCards(p1, Zone.Deck).Count;
            Assert(SurgeCast(SurgeSpell(), "ladder1"), "元素充盈：对手载体①（差1）打出");
            Crumb("surge ladder1: " + surgeState());
            Assert(core.ZoneManager.GetCards(p1, Zone.Deck).Count == surgeDeck,
                   "元素充盈：差 1 < 门槛 2 不触发（≥x 语义——未达门槛）");
            Assert(SurgeCast(SurgeSpell(), "ladder2"), "元素充盈：对手载体②（差2）打出");
            Crumb("surge ladder2: " + surgeState());
            Assert(core.ZoneManager.GetCards(p1, Zone.Deck).Count == surgeDeck - 1,
                   "元素充盈：差 2 ≥ 门槛 2（Then 锚价推导）→ 首次达标触发（引擎抽1）");
            Assert(SurgeCast(SurgeSpell(), "ladder3"), "元素充盈：对手载体③（差3）打出");
            Crumb("surge ladder3: " + surgeState());
            Assert(core.ZoneManager.GetCards(p1, Zone.Deck).Count == surgeDeck - 1,
                   "元素充盈：同卡本回合已触发过——差 3 不再发奖（2026-10-22 每卡每回合 1 次）");

            // 同差重复评估（差回到 2 再出 0 费载体）：同卡每回合仅 1 次，不再触发
            SurgeWallets(p1Red: 5, p2Red: 3); // 钱包直改模拟剩余变化 → 差 = 5−3 = 2
            Assert(SurgeCast(SurgeSpell(0f), "again"), "元素充盈：0费载体（差仍2）打出");
            Crumb("surge again: " + surgeState());
            Assert(core.ZoneManager.GetCards(p1, Zone.Deck).Count == surgeDeck - 1,
                   "元素充盈：同卡本回合已触发过——同差 2 重复出牌不再发奖（每卡每回合 1 次）");

            // 2026-10-22 全局规则：所有自由分支同卡每回合 1 次——旧宿主与限一卡同场，
            // 首次达标各发一次后，第二次达标两卡都被 EngineCapReached 收口
            var cappedData = new CardData
            {
                ID = "VERIFY_ENGINE_SURGE_CAP", CardName = "验证充盈·限一", Supertype = Cardtype.Creature, Power = 3, Life = 3,
            };
            cappedData.Effects.Add(new CardEffectData
            {
                Id = "VERIFY_SURGE_CAP_MAIN",
                TriggerTiming = (int)CardCore.TriggerTiming.Activate_Active, // 引擎宿主惰性化（同上注）
                TriggerLimitPerTurn = 1, // 显式声明（转换器尊重声明——引擎头预设无限只兜底未声明行）
                AtomicEffects = new List<AtomicEffectEntry>
                {
                    EngineAtom(CardCore.AtomicEffectType.DrawCard, 1, CardCore.BranchEngineKind.ManaSurplus, 1,
                        AtomRefs.New(CardCore.AtomicEffectType.DrawCard, value: 1)),
                },
            });
            var cappedCard = new CardWrapper(cappedData);
            cappedCard.SetController(p1);
            Assert(core.ZoneManager.TryAddToBattlefield(cappedCard, p1), "元素充盈：限一宿主入场");
            int capBase = core.ZoneManager.GetCards(p1, Zone.Deck).Count;
            Assert(SurgeCast(SurgeSpell(0f), "cap1"), "元素充盈：限一段载体①（差仍2）打出");
            Crumb("surge cap1: " + surgeState());
            Assert(core.ZoneManager.GetCards(p1, Zone.Deck).Count == capBase - 1,
                   "元素充盈：限一卡首次达标发一次；旧宿主本回合已发过，不再发（每卡每回合 1 次）");
            Assert(SurgeCast(SurgeSpell(0f), "cap2"), "元素充盈：限一段载体②（差仍2）打出");
            Crumb("surge cap2: " + surgeState());
            Assert(core.ZoneManager.GetCards(p1, Zone.Deck).Count == capBase - 1,
                   "元素充盈：两卡本回合均已发过——第二次达标均收口（每卡每回合 1 次）");

            // 己方回合门：p1 自己回合出牌（差恰 2）不触发——评估观察的是施放者对手（p2 场上无引擎）
            EnsureMainPhase(core, p1);
            SurgeWallets(p1Red: 6, p2Red: 4); // 差 2
            surgeDeck = core.ZoneManager.GetCards(p1, Zone.Deck).Count;
            var ownCard = InjectCard(core, p1, SurgeSpell(0f));
            Assert(GameActions.PlayCard(core, p1, ownCard, null, Zone.Hand, 0, out var ownWhy),
                   $"元素充盈：己方0费载体打出（拒因={ownWhy}）");
            GameActions.DrainStack(core);
            Crumb("surge own-turn: " + surgeState());
            Assert(core.ZoneManager.GetCards(p1, Zone.Deck).Count == surgeDeck - 1,
                   "元素充盈：己方回合出牌不触发（仅对手回合·载体自抽1，引擎零抽）");

            // 非出牌支付（直接 PayCost）不触发：评估只在施放结算口
            core.ElementPool.TryPayCostWithAutoTap(CostOf((ManaType.Red, 2f)), p2);
            Crumb("surge manualpay: " + surgeState());
            Assert(core.ZoneManager.GetCards(p1, Zone.Deck).Count == surgeDeck - 1,
                   "元素充盈：非出牌支付（直接扣款）不触发——评估只在施放结算口");

            // 封顶 6 上界 + 六档阶梯全型（教学二段六同场景）：门槛 6 宿主（Then=3×DrawCard1 锚价 6），
            // p2 剩余 6→0、差 1..6：2026-10-22 每卡每回合 1 次——门槛 2 宿主本回合已发过，
            // 仅门槛 6 宿主恰差 6（读数封顶==6）抽 3；差 3/4/5 越过不触发
            var surgeData2 = new CardData
            {
                ID = "VERIFY_ENGINE_SURGE2", CardName = "验证充盈封顶", Supertype = Cardtype.Creature, Power = 3, Life = 3,
            };
            surgeData2.Effects.Add(new CardEffectData
            {
                Id = "VERIFY_SURGE2_MAIN",
                TriggerTiming = (int)CardCore.TriggerTiming.Activate_Active, // 引擎宿主惰性化（同上）
                AtomicEffects = new List<AtomicEffectEntry>
                {
                    EngineAtom(CardCore.AtomicEffectType.DrawCard, 1, CardCore.BranchEngineKind.ManaSurplus, 1,
                        AtomRefs.New(CardCore.AtomicEffectType.DrawCard, value: 1),
                        AtomRefs.New(CardCore.AtomicEffectType.DrawCard, value: 1),
                        AtomRefs.New(CardCore.AtomicEffectType.DrawCard, value: 1)),
                },
            });
            var surgeCard2 = new CardWrapper(surgeData2);
            surgeCard2.SetController(p1);
            Assert(core.ZoneManager.TryAddToBattlefield(surgeCard2, p1), "元素充盈：封顶宿主入场");
            EnsureMainPhase(core, p2); // 回到对手回合（回合结束清钱包——重灌）
            SurgeWallets(p1Red: 6, p2Red: 6);
            surgeDeck = core.ZoneManager.GetCards(p1, Zone.Deck).Count;
            for (int k = 1; k <= 6; k++) Assert(SurgeCast(SurgeSpell(), $"cap{k}"), $"元素充盈：对手六连载体{k}（差{k}）打出");
            Crumb("surge ladder-done: " + surgeState());
            Assert(core.ZoneManager.GetCards(p1, Zone.Deck).Count == surgeDeck - 3,
                   "元素充盈：差 1..6 六档阶梯——仅门槛6宿主恰差6（封顶读数==6）抽3（门槛2宿主本回合已发过；差3/4/5 越过不触发）");

            core.ZoneManager.MoveCard(surgeCard, p1, Zone.Battlefield, Zone.Graveyard); // 门槛2宿主退场防连带
            core.ZoneManager.MoveCard(surgeCard2, p1, Zone.Battlefield, Zone.Graveyard);
            SurgeWallets(0, 0); // 段界卫生：钱包/地牌不留段
            surgePool.PooledCards.Clear();
            surgePool2.PooledCards.Clear();

            // ---- 7. 局面状态门（评估器直测 + 口径断言；有限分支 2026-10-09 还原）----
            // 回合层清零（shield 跳过自动化防抽牌副作用；事件订阅照常分发清 _turn）
            var stateShield = new TurnStartAutomationShield();
            RuleHooks.RegisterTurnStartInterceptor(stateShield);
            try
            {
                EventManager.Instance.Publish(new TurnStartEvent { TurnPlayer = p1, TurnNumber = 700 });
                GameActions.DrainStack(core);
            }
            finally { RuleHooks.UnregisterTurnStartInterceptor(stateShield); }

            var sctx = new EffectExecutionContext
            { Controller = p1, ZoneManager = core.ZoneManager, ElementPool = core.ElementPool };
            bool EvalGate(string id) => CardCore.BranchConditionEvaluator.Evaluate(id, new CardCore.EffectOutcome(), 0, null, sctx);

            int lifeKeep1 = p1.Life, lifeKeep2 = p2.Life;
            p1.Life = 10; p2.Life = 20;
            Assert(EvalGate("LifeBelowOpp") && !EvalGate("LifeAboveOpp"), "状态门：生命 10<20 → 低于真/高于假");
            p1.Life = 25;
            Assert(EvalGate("LifeAboveOpp") && !EvalGate("LifeBelowOpp"), "状态门：生命 25>20 → 高于真");
            p1.Life = 20;
            Assert(!EvalGate("LifeAboveOpp") && !EvalGate("LifeBelowOpp"), "状态门：生命相等 → 双假（严格比较）");
            p1.Life = lifeKeep1; p2.Life = lifeKeep2;

            int deckP1 = core.ZoneManager.GetCards(p1, Zone.Deck).Count;
            int deckP2 = core.ZoneManager.GetCards(p2, Zone.Deck).Count;
            Assert(EvalGate("DeckBelowOpp") == (deckP1 < deckP2) && EvalGate("DeckAboveOpp") == (deckP1 > deckP2),
                   $"状态门：卡组剩余对比与实际一致（{deckP1} vs {deckP2}）");

            int crP1 = core.ZoneManager.GetCards(p1, Zone.Battlefield)
                .Count(c => c is CardCore.IHasSupertype st && st.Supertype == Cardtype.Creature);
            int crP2 = core.ZoneManager.GetCards(p2, Zone.Battlefield)
                .Count(c => c is CardCore.IHasSupertype st && st.Supertype == Cardtype.Creature);
            Assert(EvalGate("CreaturesBelowOpp") == (crP1 < crP2) && EvalGate("CreaturesAboveOpp") == (crP1 > crP2),
                   $"状态门：场上生物对比与实际一致（{crP1} vs {crP2}）");

            Assert(!EvalGate("FirstCardThisTurn"), "状态门：回合开始未出牌 → 非首张");
            EventManager.Instance.Publish(new CardPlayEvent { Player = p1 });
            Assert(EvalGate("FirstCardThisTurn"), "状态门：本回合首张出牌宣言 → 真（CardsPlayed ThisTurn==1）");
            EventManager.Instance.Publish(new CardPlayEvent { Player = p1 });
            Assert(!EvalGate("FirstCardThisTurn"), "状态门：第二张出牌后 → 假");

            var standbyDrawn = ZoneManagerExtensions.DrawCard(core.ZoneManager, p1, firstDrawOfTurn: true);
            var effectDrawn = ZoneManagerExtensions.DrawCard(core.ZoneManager, p1, firstDrawOfTurn: false);
            var dctxStandby = new EffectExecutionContext
            { Controller = p1, ZoneManager = core.ZoneManager, ElementPool = core.ElementPool, CastCard = standbyDrawn };
            var dctxEffect = new EffectExecutionContext
            { Controller = p1, ZoneManager = core.ZoneManager, ElementPool = core.ElementPool, CastCard = effectDrawn };
            Assert(CardCore.BranchConditionEvaluator.Evaluate("DrawnInStandbyThisTurn", new CardCore.EffectOutcome(), 0, null, dctxStandby),
                   "状态门：本回合第一张抽到的卡（FirstDrawOfTurn 自然抽）→ 真");
            Assert(!CardCore.BranchConditionEvaluator.Evaluate("DrawnInStandbyThisTurn", new CardCore.EffectOutcome(), 0, null, dctxEffect),
                   "状态门：效果抽牌（非准备阶段）→ 假");

            // ---- 7b. 局面状态门·二批（2026-09-22 追加，预算 2：操控地≥7 / 手牌=0 / 生命≤7）----
            // 生命≤7（7 真 / 8 假，恢复原值）
            int lifeKeepB = p1.Life;
            p1.Life = 7;
            Assert(EvalGate("LifeLe7"), "状态门二批：生命 7 ≤ 7 → 真");
            p1.Life = 8;
            Assert(!EvalGate("LifeLe7"), "状态门二批：生命 8 > 7 → 假");
            p1.Life = lifeKeepB;

            // 手牌=0：临时清手（手→牌库）→ 真 → 还原
            var handSnapshot = core.ZoneManager.GetCards(p1, Zone.Hand).ToList();
            Assert(EvalGate("HandEmpty") == (handSnapshot.Count == 0), "状态门二批：手牌判与实际张数一致（清空前）");
            foreach (var hc in handSnapshot)
                core.ZoneManager.MoveCard(hc, p1, Zone.Hand, Zone.Deck);
            Assert(EvalGate("HandEmpty"), "状态门二批：手牌清空 → 真");
            foreach (var hc in handSnapshot)
                core.ZoneManager.MoveCard(hc, p1, Zone.Deck, Zone.Hand);

            // 操控地≥7：红1 费生物卡垫地到 7 张（surge 段已置 GlobalTurnIndex=9 → 上限 9），结束回收
            var addedLands = new List<Card>();
            Assert(EvalGate("LandsGe7") == (core.ElementPool.GetPooledCards(p1).Count >= 7),
                   "状态门二批：地数判与实际张数一致（垫前）");
            while (core.ElementPool.GetPooledCards(p1).Count < 7)
            {
                var land = CardLoader.BuildDeck(new List<CardData>
                {
                    new CardData { ID = "VERIFY_GATE_LAND_" + addedLands.Count, CardName = "状态门垫地",
                        Supertype = Cardtype.Creature, Power = 0, Life = 1,
                        Cost = CostOf((ManaType.Red, 1f)) },
                }, 1)[0];
                land.SetController(p1);
                if (!core.ElementPool.AddCardToPool(land, p1)) break; // 上限兜底（不应发生：cap=9）
                addedLands.Add(land);
            }
            Assert(EvalGate("LandsGe7"), $"状态门二批：操控地 {core.ElementPool.GetPooledCards(p1).Count} ≥ 7 → 真");
            foreach (var land in addedLands)
                core.ElementPool.RemoveCardFromPool(land, p1);

            // ---- 8. 拦截式改写门断言已删（2026-10-05：DmgRewrite* 四门随四条改写迁唯一光环退役——
            //      差价计价 / converter 配对守卫 / 效果伤害拦截三路同批废除，无头侧锚见 V9.p；
            //      战斗伤害改写族=关键词侧（毒刺/冰晶等），另测未动）----

            // ---- 9. 团结（2026-10-20 效果序位换轴·空间轴）：与此卡相邻的单位数（生物/结界占格、角色格、
            //      非空地牌格各算一；施放占位卡不计）≥ x 时执行奖励——评估口=OnCardCastSettled（己方出牌
            //      结算后，充盈同拍），每次达标都触发；只观察己方场上引擎卡（手牌无格）；x∈[1,6] 手填；
            //      邻接计数经 UnityAdjacentCountOf 注入（编辑器无棋盘——注假计数） ----
            EnsureMainPhase(core, p1);
            PadDeck(core, p1, 6, "VERIFY_UNI_FILL_"); // 奖励观测口径=抽牌数（引擎奖励抽 2，垫足 6）
            CardData UnityCard(string id, int x)
            {
                var d = new CardData
                {
                    ID = id, CardName = "验证团结x" + x,
                    Supertype = Cardtype.Creature, Power = 2, Life = 2,
                };
                // 主干=引擎行（EngineUnity，条件载体非效果——执行按 Settle==Engine 跳过）
                // + x 手填（=所需相邻单位数）+ Then=抽 1
                d.Effects.Add(new CardEffectData
                {
                    Id = id + "_EFF",
                    TriggerTiming = (int)CardCore.TriggerTiming.OnCardPlayed,
                    AtomicEffects = new List<AtomicEffectEntry>
                    {
                        EngineAtom(CardCore.AtomicEffectType.EngineUnity, 1, CardCore.BranchEngineKind.Unity, x,
                            AtomRefs.New(CardCore.AtomicEffectType.DrawCard, value: 1)),
                    },
                });
                return d;
            }
            var uniDef = CardEffectConverter.ConvertOne(UnityCard("VERIFY_UNI_A", 2).Effects[0], "VERIFY_UNI_A");
            Assert(uniDef != null && uniDef.Effects.Count == 1 && uniDef.Effects[0].Branch != null
                   && uniDef.Effects[0].Branch.Settle == CardCore.BranchSettleKind.Engine
                   && uniDef.Effects[0].Branch.EngineKind == CardCore.BranchEngineKind.Unity
                   && uniDef.Effects[0].Branch.Then.Count == 1
                   && uniDef.TriggerTiming == CardCore.TriggerTiming.OnCardPlayed
                   && uniDef.ActivationType == CardCore.EffectActivationType.Automatic
                   && uniDef.TriggerLimitPerTurn == -1,
                   "团结：槽级载荷转存 Then + 引擎头钉死 使用卡牌时+自动+无限（2026-10-20 效果序位换轴口径）");
            Assert((int)CardCore.CostDerivationService.DeriveElementCosts(uniDef)[CardCore.ManaType.Gray] == 0,
                   "团结计价：引擎零计价（x=所需相邻单位数判定条件非收费；Then 奖励不并入卡费）");

            Card UnityToZone(CardData data, Zone zone)
            {
                var card = new CardWrapper(data);
                card.SetController(p1);
                card.IsAlive = true;
                core.ZoneManager.GetZoneContainer(p1).Add(card, zone);
                return card;
            }
            // 隔离：清 p1 战场残留（前段夹具可能留有充盈等引擎卡——同口评估会串扰抽牌计数）
            foreach (var uniStale in core.ZoneManager.GetCards(p1, Zone.Battlefield).ToList())
                if (uniStale != null) core.ZoneManager.GetZoneContainer(p1).Remove(uniStale, Zone.Battlefield);
            var uniA = UnityToZone(UnityCard("VERIFY_UNI_A", 2), Zone.Battlefield); // x=2
            var uniB = UnityToZone(UnityCard("VERIFY_UNI_B", 3), Zone.Battlefield); // x=3
            var uniC = UnityToZone(UnityCard("VERIFY_UNI_C", 1), Zone.Hand);        // 手牌载体：无格不观察
            int uniDeck0 = core.ZoneManager.GetCards(p1, Zone.Deck).Count;
            try
            {
                // 未接线（编辑器默认无棋盘）=0 恒不触发
                CardCore.BranchEngines.UnityAdjacentCountOf = null;
                CardCore.BranchEngines.OnCardCastSettled(p1);
                Assert(core.ZoneManager.GetCards(p1, Zone.Deck).Count == uniDeck0,
                       "团结：邻接计数未接线 = 0 恒不触发（无头/编辑器默认）");

                // 邻接不足（1 < 2/3）不触发；手牌载体(x=1)不受 1 达标——观察面=仅场上
                CardCore.BranchEngines.UnityAdjacentCountOf = c => 1;
                CardCore.BranchEngines.OnCardCastSettled(p1);
                Assert(core.ZoneManager.GetCards(p1, Zone.Deck).Count == uniDeck0,
                       "团结：相邻单位数 1 < x=2/3 不触发（手牌载体无格不观察）");

                // 达标 2：A(x=2) 触发抽 1（B=3 未达）
                CardCore.BranchEngines.UnityAdjacentCountOf = c => 2;
                CardCore.BranchEngines.OnCardCastSettled(p1);
                Assert(core.ZoneManager.GetCards(p1, Zone.Deck).Count == uniDeck0 - 1,
                       "团结：相邻单位数 2 ≥ x=2 → A 触发抽 1（B(x=3) 未达不触发）");

                // 达标 3：B 触发抽 1（A 每次达标都触发——同拍再抽 1）
                CardCore.BranchEngines.UnityAdjacentCountOf = c => 3;
                CardCore.BranchEngines.OnCardCastSettled(p1);
                Assert(core.ZoneManager.GetCards(p1, Zone.Deck).Count == uniDeck0 - 3,
                       "团结：相邻单位数 3 → A/B 各抽 1（每次达标都触发——充盈同纪律）");
            }
            finally { CardCore.BranchEngines.UnityAdjacentCountOf = null; } // 静态委托防跨段泄漏
            core.ZoneManager.MoveCard(uniA, p1, Zone.Battlefield, Zone.Graveyard); // 清场防跨段连带
            core.ZoneManager.MoveCard(uniB, p1, Zone.Battlefield, Zone.Graveyard);
            core.ZoneManager.MoveCard(uniC, p1, Zone.Hand, Zone.Graveyard);
        }

        /// <summary>
        /// 测试拦截器：合成 TurnStartEvent 期间跳过 GameCore 回合自动化（回合抽牌/地牌曲线/重置）——
        /// 分支引擎段断言只测量引擎奖励自身的抽牌，隔离环境副作用（2026-09-13 拼点口径修复配套）。
        /// </summary>
        private sealed class TurnStartAutomationShield : ITurnStartInterceptor
        {
            public bool ShouldSkipTurnStartAutomation(Player player) => true;
        }

        // ======================================== 档位收缩定案（2026-09-13；2026-09-16 指示物统一档追改） ========================================

        /// <summary>
        /// ①冻结：持续恒=持有者回合结束（叠层仅累计显示不再延展），持有期间无法重置；
        /// ②生物赋予关键词固定 Temp 1 回合（回合末清，魔法 Setting/光环照旧）；
        /// ③计价梯三族（2026-10-09 清理：Duration 档计价随三轨统一退役，断言已删——价梯以 CounterSpec 为准）。
        /// </summary>
        private static void TestTierConsolidation(GameCore core, Player p1, Player p2)
        {
            EnsureMainPhase(core, p1);

            // ---- 1. 冻结（2026-10-08 层即持续定案：层=持续回合，持有者回合末 −1、叠加=延长） ----
            var frozenUnit = SpawnTier(core, p1, 3, 3);
            var freezeAtom = new CardCore.AtomicEffectInstance { Type = AtomicEffectType.Freeze, Value = 0 };
            var fctx = new EffectExecutionContext
            {
                Source = p2, Controller = p2, ZoneManager = core.ZoneManager, ElementPool = core.ElementPool,
                Targets = new List<Entity> { frozenUnit },
            };
            CardCore.Attribute.EffectHandlerRegistry.ExecuteEffect(freezeAtom, fctx); // 默认 1 层
            Assert(frozenUnit.IsTapped() && frozenUnit.GetCounterCount(CardCore.Attribute.KeywordRules.FreezeCounter) == 1,
                   "冻结：默认 1 层");
            CardCore.Attribute.EffectHandlerRegistry.ExecuteEffect(freezeAtom, fctx); // 再施加 = 层数累计（延长）
            Assert(frozenUnit.GetCounterCount(CardCore.Attribute.KeywordRules.FreezeCounter) == 2,
                   "冻结叠加：层数累计（层即持续——2 层=2 回合）");
            CardCore.Attribute.CounterRules.OnTurnEnd(p2, core.ZoneManager); // 施加方回合末：不碰持有者侧
            Assert(frozenUnit.GetCounterCount(CardCore.Attribute.KeywordRules.FreezeCounter) == 2 && frozenUnit.IsTapped(),
                   "冻结：施加方回合末不倒数（持有者侧结算域，仍冻结仍横置）");
            CardCore.Attribute.CounterRules.OnTurnEnd(p1, core.ZoneManager); // 持有者回合末①：−1
            Assert(frozenUnit.GetCounterCount(CardCore.Attribute.KeywordRules.FreezeCounter) == 1 && frozenUnit.IsTapped(),
                   "冻结倒数：持有者回合末 −1（2→1，期间无法重置）");
            CardCore.Attribute.CounterRules.OnTurnEnd(p1, core.ZoneManager); // 持有者回合末②：归零解除
            Assert(frozenUnit.GetCounterCount(CardCore.Attribute.KeywordRules.FreezeCounter) == 0,
                   "冻结解除：层归零（下回合可重置）");
            RetireCards(core, p1, frozenUnit);

            // ---- 2. 关键词赋予权限三分（2026-09-14 用户定案）：他人=Temp 1 回合；自己=Setting 文本轨永久 ----
            var granter = SpawnTier(core, p1, 2, 2);
            var receiver = SpawnTier(core, p1, 2, 2);
            var grantCtx = new EffectExecutionContext
            {
                Source = granter, Controller = p1, ZoneManager = core.ZoneManager, ElementPool = core.ElementPool,
                Targets = new List<Entity> { receiver },
                Duration = DurationType.Permanent, // 故意声明永久——生物来源赋他人应被覆写为 Temp
            };
            var grantAtom = new CardCore.AtomicEffectInstance { Type = AtomicEffectType.GrantTaunt, Value = 1 };
            CardCore.Attribute.EffectHandlerRegistry.ExecuteEffect(grantAtom, grantCtx);
            // 同一来源自赋予（目标=自己）——文本轨，回合末不清
            var selfGrantCtx = new EffectExecutionContext
            {
                Source = granter, Controller = p1, ZoneManager = core.ZoneManager, ElementPool = core.ElementPool,
                Targets = new List<Entity> { granter },
                Duration = DurationType.Permanent,
            };
            CardCore.Attribute.EffectHandlerRegistry.ExecuteEffect(
                new CardCore.AtomicEffectInstance { Type = AtomicEffectType.GrantVigilance, Value = 1 }, selfGrantCtx);
            Assert(receiver.HasKeyword(CardCore.Attribute.KeywordRules.Taunt), "生物赋予：关键词生效");
            Assert(granter.HasKeyword(CardCore.Attribute.KeywordRules.Vigilance), "自赋予：关键词生效");
            EventManager.Instance.Publish(new TurnEndEvent { TurnPlayer = p1, TurnNumber = 700 });
            Assert(!receiver.HasKeyword(CardCore.Attribute.KeywordRules.Taunt),
                   "赋予他人固定 1 回合：回合末到期（声明永久被覆写为 Temp）");
            Assert(granter.HasKeyword(CardCore.Attribute.KeywordRules.Vigilance),
                   "自赋予走文本轨（Setting）：回合末不清——视同印制文本，永久");
            RetireCards(core, p1, granter, receiver);

            // 2026-10-09 清理：计价梯三族（控制权 1.2/1.6/3.0、Grant 四档、费用修改两档）断言删除——
            // Duration 档计价已随 2026-10-05 三轨统一 + 2026-10-08 指示物化退役（价梯以 CounterSpec 为准）

            // ---- 改写持有者归属路由端到端：偷取→死亡/弹回均归新主 ----
            var stealTgt = SpawnTier(core, p2, 2, 3);
            var stealAtom = new CardCore.AtomicEffectInstance { Type = AtomicEffectType.ChangeOwner, Value = 1 };
            var stealCtx = new EffectExecutionContext
            {
                Source = p1, Controller = p1, ZoneManager = core.ZoneManager, ElementPool = core.ElementPool,
                Targets = new List<Entity> { stealTgt },
            };
            Assert(CardCore.Attribute.EffectHandlerRegistry.ExecuteEffect(stealAtom, stealCtx), "改写持有者原子执行");
            Assert(stealTgt.GetController() == p1 && stealTgt.GetOwner() == p1, "改写持有者：控制权与 owner 均换到新主");
            CardCore.Attribute.DeathRules.TryKill(stealTgt, CardCore.Attribute.DeathCause.DestroyEffect, p1, core.ZoneManager);
            Assert(core.ZoneManager.GetCards(p1, Zone.Graveyard).Contains(stealTgt)
                   && !core.ZoneManager.GetCards(p2, Zone.Graveyard).Contains(stealTgt),
                   "归属路由：改写后死亡进**新主**墓地");

            var stealTgt2 = SpawnTier(core, p2, 2, 3);
            stealCtx.Targets = new List<Entity> { stealTgt2 };
            CardCore.Attribute.EffectHandlerRegistry.ExecuteEffect(stealAtom, stealCtx);
            var bounceAtom = new CardCore.AtomicEffectInstance { Type = AtomicEffectType.ReturnToHand, Value = 1 };
            var bounceCtx = new EffectExecutionContext
            {
                Source = p1, Controller = p1, ZoneManager = core.ZoneManager, ElementPool = core.ElementPool,
                Targets = new List<Entity> { stealTgt2 },
            };
            CardCore.Attribute.EffectHandlerRegistry.ExecuteEffect(bounceAtom, bounceCtx);
            Assert(core.ZoneManager.GetCards(p1, Zone.Hand).Contains(stealTgt2),
                   "归属路由：改写后弹回进**新主**手牌");

            // 对照：临时控制（非改写）死亡回原主墓地
            var tempTgt = SpawnTier(core, p2, 2, 3);
            var tempAtom = new CardCore.AtomicEffectInstance { Type = AtomicEffectType.GainControl, Value = 1 };
            var tempCtx = new EffectExecutionContext
            {
                Source = p1, Controller = p1, ZoneManager = core.ZoneManager, ElementPool = core.ElementPool,
                Targets = new List<Entity> { tempTgt },
                Duration = DurationType.UntilEndOfTurn,
            };
            CardCore.Attribute.EffectHandlerRegistry.ExecuteEffect(tempAtom, tempCtx);
            CardCore.Attribute.DeathRules.TryKill(tempTgt, CardCore.Attribute.DeathCause.DestroyEffect, p1, core.ZoneManager);
            Assert(core.ZoneManager.GetCards(p2, Zone.Graveyard).Contains(tempTgt),
                   "归属对照：临时控制（未改写 owner）死亡回**原主**墓地");

            // 2026-10-09 清理：Grant 四档/费用修改两档（Duration 档计价）随上段一并删除——
            // 关键词统一计价=行锚×max(1,层数)（2026-10-08 关键词不叠加定案），不再按持续档分梯
        }

        private static Card SpawnTier(GameCore core, Player owner, int power, int life)
        {
            var data = new CardData
            {
                ID = "VERIFY_TIER_" + System.Guid.NewGuid().ToString("N").Substring(0, 6),
                CardName = "档位段", Supertype = Cardtype.Creature, Power = power, Life = life,
            };
            var card = new CardWrapper(data);
            card.SetController(owner);
            card.SetOwner(owner); // 2026-09-13 修复：合成卡未设 owner → GetOwner() 为 null，
                                  // 临时控制后死亡按 controller 落墓（归属对照断言失真）；补 owner=归属者
            Assert(core.ZoneManager.TryAddToBattlefield(card, owner), "档位段合成随从入场");
            card.Untap();
            return card;
        }

        // ======================================== 英雄技能（2026-10-15 改版：技能=任选一张法术） ========================================

        /// <summary>技能段垫卡数（技能卡不占张数：起手 6 后牌库余 8-9，足够段内抽牌/回合推进）。</summary>
        private const int HsDeckPadCount = 14;

        /// <summary>本段技能探针法术（唯一主动效果=抽 1；2026-10-15 改版：载体结界→法术）。</summary>
        private static CardData HsSkillCardData()
        {
            var data = new CardData
            {
                ID = "VERIFY_HS_SKILL", CardName = "技能探针法术", Supertype = Cardtype.Spell,
            };
            data.Effects.Add(new CardEffectData
            {
                Id = "VERIFY_HS_SKILL_DRAW",
                TriggerTiming = (int)TriggerTiming.Activate_Active,
                AtomicEffects = new List<AtomicEffectEntry>
                    { AtomRefs.New(AtomicEffectType.DrawCard, value: 1) },
            });
            return data;
        }

        /// <summary>
        /// 法术化技能端到端（2026-10-15 改版：技能=任选一张法术，卡池生成不占卡组张数）：
        /// ①资格校验（法术+1~3 个全主动；多非主动/非法术/纯触发/超 3 个不合格）；
        /// ②InitGame 落位 FieldZone（合成卡不在卡池 → 兜底牌库抽取；未标记方无技能）；
        /// ③发动=所选主动效果走通用主动（入栈→结算抽 1、按效果派生价扣费、横置=每回合一次、SkillUse+1、无耐久层）；
        /// ④一回合一次闸门；⑤回合开始重置可再发；⑥沉默拦截；⑦费用不足拒绝（不横置不白付）；
        /// ⑧技能卡离场失效；⑨AI 自动挑卡（AutoPickSkillCard——候选无合格回落全卡池）。
        /// </summary>
        private static void TestHeroSkills(GameCore core, Player p1, Player p2)
        {
            // ---- 1. 资格校验（CanBeSkillCard / SkillIneligibleReason）----
            var skillData = HsSkillCardData();
            Assert(HeroSkillSystem.CanBeSkillCard(skillData)
                   && HeroSkillSystem.SkillIneligibleReason(skillData) == null,
                   "资格：法术+唯一主动效果合格");

            var multi = new CardData { ID = "VERIFY_HS_MULTI", CardName = "双效果法术探针", Supertype = Cardtype.Spell };
            multi.Effects.Add(new CardEffectData
            {
                Id = "VERIFY_HS_M1", TriggerTiming = (int)TriggerTiming.Activate_Active,
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DrawCard, value: 1) },
            });
            multi.Effects.Add(new CardEffectData
            {
                Id = "VERIFY_HS_M2", TriggerTiming = (int)TriggerTiming.OnPlay,
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DrawCard, value: 1) },
            });
            Assert(!HeroSkillSystem.CanBeSkillCard(multi)
                   && HeroSkillSystem.SkillIneligibleReason(multi) != null,
                   "资格：混入非主动效果不合格——法术效果须全为主动（施放时抉择其一）");

            var triple = new CardData { ID = "VERIFY_HS_TRI", CardName = "三效果法术探针", Supertype = Cardtype.Spell };
            for (int i = 0; i < 3; i++)
                triple.Effects.Add(new CardEffectData
                {
                    Id = "VERIFY_HS_T" + i, TriggerTiming = (int)TriggerTiming.Activate_Active,
                    AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DrawCard, value: 1) },
                });
            Assert(HeroSkillSystem.CanBeSkillCard(triple), "资格：法术 3 个全主动合格（抉择上限）");

            var quad = new CardData { ID = "VERIFY_HS_QUAD", CardName = "四效果法术探针", Supertype = Cardtype.Spell };
            for (int i = 0; i < 4; i++)
                quad.Effects.Add(new CardEffectData
                {
                    Id = "VERIFY_HS_Q" + i, TriggerTiming = (int)TriggerTiming.Activate_Active,
                    AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DrawCard, value: 1) },
                });
            Assert(!HeroSkillSystem.CanBeSkillCard(quad), "资格：法术 4 个主动超上限不合格（最多三）");

            var creature = new CardData { ID = "VERIFY_HS_CR", CardName = "生物探针", Supertype = Cardtype.Creature, Power = 1, Life = 1 };
            creature.Effects.Add(new CardEffectData
            {
                Id = "VERIFY_HS_CR_A", TriggerTiming = (int)TriggerTiming.Activate_Active,
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DrawCard, value: 1) },
            });
            Assert(!HeroSkillSystem.CanBeSkillCard(creature), "资格：非法术（生物）不合格");
            var enchCarrier = new CardData { ID = "VERIFY_HS_ENCH", CardName = "结界探针", Supertype = Cardtype.Enchantment };
            enchCarrier.Effects.Add(new CardEffectData
            {
                Id = "VERIFY_HS_E1", TriggerTiming = (int)TriggerTiming.Activate_Active,
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DrawCard, value: 1) },
            });
            Assert(!HeroSkillSystem.CanBeSkillCard(enchCarrier), "资格：结界（带主动行也）不合格——载体须法术");

            var passiveOnly = new CardData { ID = "VERIFY_HS_PASS", CardName = "纯触发法术探针", Supertype = Cardtype.Spell };
            passiveOnly.Effects.Add(new CardEffectData
            {
                Id = "VERIFY_HS_P1", TriggerTiming = (int)TriggerTiming.OnPlay,
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(AtomicEffectType.DrawCard, value: 1) },
            });
            Assert(!HeroSkillSystem.CanBeSkillCard(passiveOnly), "资格：效果非主动（纯触发）不合格");

            Assert(HeroSkillSystem.AutoPickSkillCard(new List<CardData>(HsDeckPadCount + 1) { skillData }) == "VERIFY_HS_SKILL",
                   "AI 自动挑卡：候选含合格法术即取（候选优先）");
            Assert(HeroSkillSystem.AutoPickSkillCard(new List<CardData> { multi, creature }) != null,
                   "AI 自动挑卡：候选无合格 → 回落全卡池锚价最低法术（2026-10-15 改版：技能不占张数）");

            // ---- 2. InitGame 抽卡落位（段内重开小局：夹具卡组，p1 标记/p2 未标记）----
            var deckCards = new List<CardData> { skillData };
            for (int i = 0; i < HsDeckPadCount; i++)
                deckCards.Add(new CardData
                {
                    ID = $"VERIFY_HS_PAD_{i}", CardName = "技能段垫卡", Supertype = Cardtype.Creature, Power = 1, Life = 1,
                });
            core.InitGame(CardLoader.BuildDeck(deckCards, 1), CardLoader.BuildDeck(deckCards, 1),
                rngSeed: 20261007, skillCardId1: "VERIFY_HS_SKILL");
            p1 = core.Player1;
            p2 = core.Player2;

            Assert(p1.HeroSkillCard != null
                   && core.ZoneManager.GetCards(p1, Zone.FieldZone).Contains(p1.HeroSkillCard)
                   && p1.HeroSkillCard is CardWrapper hsw && hsw.GetData().ID == "VERIFY_HS_SKILL",
                   "技能卡落位：开局落 FieldZone（英雄技能栏；合成卡不在卡池 → 兜底牌库抽取）");
            // 2026-10-09 清理：牌库计数等式删除——回合开始抽牌使基数随自动化口径漂移；保留"不在牌库"主张
            Assert(!core.ZoneManager.GetCards(p1, Zone.Deck)
                       .Any(c => c is CardWrapper w && w.GetData()?.ID == "VERIFY_HS_SKILL"),
                   "牌库：技能卡不在牌库（真实卡池=卡池生成不占张数；合成卡=兜底已抽出）");
            Assert(p2.HeroSkillCard == null && core.ZoneManager.GetCards(p2, Zone.FieldZone).Count == 0,
                   "未标记方：无技能（FieldZone 空，不自动指派）");

            // ---- 3. 发动 = 唯一主动效果走通用主动（入栈 → 结算）----
            EnsureMainPhase(core, p1);
            var pool1 = core.ElementPool.GetPool(p1);
            foreach (ManaType mt in System.Enum.GetValues(typeof(ManaType)))
                pool1.TempElements[mt] = 99; // 锚价色随原子表浮动——全色供足防混付干扰
            pool1.GlobalTurnIndex = 9; // 2026-10-09：支付浓度上限随地牌曲线上限（回合1=上限1 付不出 2 费技能）——
                                       // 推进到 9 费档上限，技能费可付（浓度上限=2026-10-04 使用侧定案）

            var fx = HeroSkillSystem.SkillEffectOf(p1.HeroSkillCard);
            Assert(fx != null && fx.IsActivatedEffect, "技能效果解析：唯一主动效果（转换后定义）");
            float expectCost = CostDerivationService.DeriveElementCosts(fx).Total;
            int poolBefore = HsPoolTotal(pool1);
            int handBefore = core.ZoneManager.GetCards(p1, Zone.Hand).Count;
            int deckBefore = core.ZoneManager.GetCards(p1, Zone.Deck).Count;

            Assert(GameActions.ActivateHeroSkill(core, p1).GetAwaiter().GetResult(), "技能发动成功（声明入栈）");
            GameActions.DrainStack(core);
            Assert(p1.HeroSkillCard.IsTapped(), "发动后技能卡横置（一回合一次实体闸门，声明期代价）");
            Assert(core.ZoneManager.GetCards(p1, Zone.Hand).Count == handBefore + 1
                   && core.ZoneManager.GetCards(p1, Zone.Deck).Count == deckBefore - 1,
                   "结算：抽 1（手牌 +1 / 牌库 -1）");
            float paid = poolBefore - HsPoolTotal(pool1);
            Assert(System.Math.Abs(paid - expectCost) < 0.51f,
                $"发动扣费=效果派生价（池总量 {poolBefore}→{HsPoolTotal(pool1)}，期望 -{expectCost}，实际 -{paid}）");
            Assert(HeroSkillSystem.GetTotalUses(core, p1) == 1
                   && p1.HeroSkillCard.GetCounterCount(CardCore.Attribute.CounterRules.SkillUseCounter) == 1,
                   "发动计数 =1（技能卡 SkillUse 指示物遥测）");
            Assert(p1.HeroSkillCard.GetCounterCount(CardCore.Attribute.CounterRules.DurabilityCounter) == 0,
                   "技能栏结界：无耐久层（落位不初始化、发动不消耗——卡牌化定案）");

            // ---- 4. 一回合一次闸门 ----
            Assert(!GameActions.ActivateHeroSkill(core, p1).GetAwaiter().GetResult(),
                   "横置中：同回合第二次发动拒绝");

            // ---- 5. 回合开始重置 → 可再发（计数累积）----
            EventManager.Instance.Publish(new TurnStartEvent { TurnPlayer = p1, TurnNumber = 800 });
            Assert(p1.HeroSkillCard != null && !p1.HeroSkillCard.IsTapped(), "回合开始重置技能卡横置");
            EnsureMainPhase(core, p1);
            Assert(GameActions.ActivateHeroSkill(core, p1).GetAwaiter().GetResult() && GameActions.DrainStack(core),
                   "下回合可再发动");
            Assert(HeroSkillSystem.GetTotalUses(core, p1) == 2, "发动计数累积 =2");

            // ---- 6. 沉默拦截（与永续魔法交互一致）----
            EventManager.Instance.Publish(new TurnStartEvent { TurnPlayer = p1, TurnNumber = 801 });
            EnsureMainPhase(core, p1);
            p1.HeroSkillCard.AddCounters(CardCore.Attribute.CounterRules.SilenceCounter, 1);
            Assert(!GameActions.ActivateHeroSkill(core, p1).GetAwaiter().GetResult(),
                   "沉默技能卡：不可发动主动效果");
            p1.HeroSkillCard.RemoveCounters(CardCore.Attribute.CounterRules.SilenceCounter, 1);

            // ---- 7. 费用不足拒绝（CanActivate 预检：不横置不白付）----
            if (expectCost > 0f)
            {
                foreach (ManaType mt in System.Enum.GetValues(typeof(ManaType)))
                    pool1.TempElements[mt] = 0; // 全色清空（混付可用灰/黑/白垫——须清全色）
                Assert(!GameActions.ActivateHeroSkill(core, p1).GetAwaiter().GetResult(),
                       "费用不足：拒绝发动");
                Assert(!p1.HeroSkillCard.IsTapped(), "拒绝时未横置（声明失败不付代价）");
                foreach (ManaType mt in System.Enum.GetValues(typeof(ManaType)))
                    pool1.TempElements[mt] = 99;
            }

            // ---- 8. 技能卡离场失效（被摧毁/弹回口径）----
            var takenSkill = p1.HeroSkillCard;
            core.ZoneManager.MoveCard(takenSkill, p1, Zone.FieldZone, Zone.Graveyard); // 模拟被摧毁
            p1.HeroSkillCard = takenSkill; // MoveCard 不清引用——守卫按在场判定
            Assert(!GameActions.ActivateHeroSkill(core, p1).GetAwaiter().GetResult(),
                   "技能卡离场（被摧毁/弹回）：技能随卡失效");
            core.ZoneManager.MoveCard(takenSkill, p1, Zone.Graveyard, Zone.FieldZone); // 归位

            // 段尾收口：技能路径可能留 SBA——排干防污染下段（同装备段惯例）
            GameActions.DrainStack(core);
            core.StackEngine.Clear();
            core.SBAEngine.ClearHistory();
        }

        /// <summary>元素池全色总量（技能段扣费断言用——锚价色随原子表浮动，只对总量；容差 0.5=半价档取整）。</summary>
        private static int HsPoolTotal(PlayerElementPool pool)
        {
            int sum = 0;
            if (pool?.TempElements == null) return 0;
            foreach (var kv in pool.TempElements) sum += kv.Value;
            return sum;
        }

        // ======================================== 装备系统（2026-09-13 第二十一批：箭头佩带） ========================================
        // 2026-10-07 武器面退役：驱动/武器反伤/主动攻击闸门/转移段随武器系统删除
        //（角色参战改走 HeroAttackCounter 弹药原子，见 CounterRules/CombatSystem）；
        // 装备=箭头佩带（LinkAura）与统一耐久语义（结界共用）保留。

        /// <summary>
        /// 装备端到端：佩带=箭头指向格占据者获 LinkAura 效果（认格不认主——对手占据照发）；空格=悬空。
        /// （原②③④⑤武器段：驱动/武器反伤/主动攻击闸门/转移——2026-10-07 武器系统退役，随实现一并删除。）
        /// </summary>
        private static void TestEquipment(GameCore core, Player p1, Player p2)
        {
            EnsureMainPhase(core, p1);

            // 棋盘搭桥（装备箭头依赖 LinkAuraSystem）
            GameBoard.LinkAuraSystem.Detach();
            var board = new GameBoard.BoardState(core, p1, p2,
                GameBoard.HalfFieldData.Flat(), GameBoard.HalfFieldData.Flat());
            board.EnableAutoResync();
            GameBoard.LinkAuraSystem.Attach(board);

            // ---- 1. 佩带：箭头指向格占据者获 LinkAura 效果 ----
            var mirrorData = new CardData
            {
                ID = "VERIFY_EQUIP_MIRROR", CardName = "秘银护心镜", Supertype = Cardtype.Artifact,
                Cost = CostOf((ManaType.Gray, 3f)),
            };
            mirrorData.LinkAuras.Add(new LinkAuraData { keyword = Attribute.KeywordRules.Taunt });
            var mirror = new CardWrapper(mirrorData);
            mirror.SetController(p1);
            Assert(core.ZoneManager.TryAddToBattlefield(mirror, p1), "装备段：护心镜入场");

            var bearer = SpawnTier(core, p1, 3, 4);
            BoardDirection dirM = DirBetweenV(board, mirror, bearer);
            if (dirM != 0)
            {
                mirrorData.ArrowDirections = GameBoard.BoardMath.ArrowOf(dirM);
                board.Resync();
                GameBoard.LinkAuraSystem.InvalidateCache();
                // 帷幕光环佩带 → 占据者视为持有（HasAuraKeyword 口径；坚韾示例已随 2026-10-08 指示物化退役）
                Assert(bearer.HasKeyword(Attribute.KeywordRules.Taunt),
                       "装备佩带：箭头指向格占据者获佩带效果（护心镜 LinkAura 生效——光环期间视为持有）");
            }

            // ---- 2. 武器段（驱动/反伤/主动攻击/转移）已随武器系统退役删除（2026-10-07）----
            RetireCards(core, p1, bearer, mirror);

            // 段尾收口（2026-09-21 修复惯例）：排干防污染下段（速度记数器残留会挡 0 速宣言）。
            GameActions.DrainStack(core);
            core.StackEngine.Clear();
            core.SBAEngine.ClearHistory();

            GameBoard.LinkAuraSystem.Detach();
            board.Dispose();
        }

        private static BoardDirection DirBetweenV(GameBoard.BoardState board, Card from, Card to)
        {
            board.TryGetCell(from, out int fx, out int fz);
            board.TryGetCell(to, out int tx, out int tz);
            foreach (GameBoard.BoardDirection d in System.Enum.GetValues(typeof(GameBoard.BoardDirection)))
            {
                var (nx, nz) = GameBoard.BoardMath.Neighbor(fx, fz, d);
                if (nx == tx && nz == tz) return d;
            }
            return 0;
        }

        // ---- 面包屑（冻死定位）：AppendAllText 逐条即时落盘，Console 死循环下不渲染而它能留痕 ----
        private static string _crumbPath;

        private static void Crumb(string msg)
        {
            try { if (_crumbPath != null) File.AppendAllText(_crumbPath, $"{System.Environment.TickCount} {msg}\n"); } catch { }
        }

        private static void Assert(bool condition, string label)
        {
            if (condition)
            {
                _pass++;
                Debug.Log($"[Verify] PASS — {label}");
            }
            else
            {
                _fail++;
                Debug.LogError($"[Verify] FAIL — {label}");
                // 双写文件（2026-09-20）：编辑器控制台 Info 级被过滤时失败仍可查
                try
                {
                    System.IO.File.AppendAllText(
                        System.IO.Path.Combine("Logs", "VerifyFailures.txt"),
                        $"[{System.DateTime.Now:HH:mm:ss}] FAIL — {label}\n",
                        System.Text.Encoding.UTF8);
                }
                catch (System.IO.IOException) { }
            }
        }
    }
}

// [menu-table-rebuild 2026-09-20]
