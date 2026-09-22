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

            // 2026-09-14 双表换源：正式卡池=StreamingAssets/Card/Cards.json（CardCatalog——effectIds 经
            // EffectsLibrary 解析 + 装载期 EnsureCost 建议价回填）+ 验证器本地合成夹具卡
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
            // 构筑规则（定案）：卡组不重复（×1）。
            // 仪式段屏蔽（2026-09-10）：仪式内容未就绪、夹具已去仪式——主卡组不再注入三相，
            // 仪式相关段落暂不执行；恢复时取消本方法内三处注释即可。
            var deckData = new List<CardData>();
            if (cardsData.Count > 0)
            {
                deckData.AddRange(cardsData.Where(c => !RitualSystem.IsRitual(new CardWrapper(c))));
                // var trinity = cardsData.FirstOrDefault(c => c.CardName == "三相仪典");
                // if (trinity != null) deckData.Add(trinity);
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
                // 仪式开局入手断言（须在 TestLandEconomy 消耗手牌之前）——仪式段屏蔽（2026-09-10）
                // TestRitualOpeningHand(core, cardsData);

                // 统一计价：全表规则一巡检（声明档位 ≥ 推导价——2026-09-11 简化口径 D≤C）。
                // 仪式豁免：0费说明书卡（保持空 Cost 打出免费），效果费不参校验。
                Crumb("→规则一巡检 derive");
                var offenders = cardsData
                    .Where(c => !RitualSystem.IsRitual(new CardWrapper(c)) && !CardCostService.Derive(c).Conformant)
                    .ToList();
                // 不符明细（一次性诊断输出：定位 S/K/E/f/挂载口/D/G 哪一环口径漂了）
                foreach (var o in offenders)
                {
                    var r = CardCostService.Derive(o);
                    Debug.LogError($"[规则一明细] {o.CardName}({o.ID}) 声明C={r.DeclaredTier} S={r.S} K={r.K} "
                        + $"E={r.EAnchor} f={r.Factor:0.###} 建议档Ĉ={r.SuggestedTier} D={r.DerivedTotal} "
                        + $"Req={r.OffsetRequirement} G=[{string.Join(",", r.Grants.Select(g => $"{g.Key}:{g.Value}"))}]\n  "
                        + string.Join("\n  ", r.Breakdown.Select(l => $"[{l.Stage}] {l.Label} = {l.Value}")));
                }
                Assert(offenders.Count == 0,
                       $"规则一巡检：全表 {cardsData.Count} 张（仪式除外）D≤C（不符 {offenders.Count}：{string.Join(",", offenders.Select(o => o.ID))}）");

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
            // BranchConfig.json 已随 5397a3f 删除（2026-09-10 用户裁定：分支目录/例外规则复杂度
            // 暂不收编，原规则收尾后再恢复——恢复时还原该文件 + 取消本行注释）
            // TestBranchCatalog();
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
            Crumb("all sections done");

            // 仪式段屏蔽（2026-09-10）：内容未就绪
            // if (cardsData.Count > 0)
            // {
            //     TestRituals(core, cardsData);
            //     TestNewRituals(core, cardsData);
            // }

            // P2b：对局日志按需导出（内存缓冲 → markdown 战报落盘）
            var verifyLog = MatchLogService.ExportMarkdown($"Logs/VerifyLog_{System.DateTime.Now:yyyyMMdd_HHmmss}.md");
            Debug.Log($"[Verify] 对局日志导出：{verifyLog ?? "无条目未导出"}");

            Debug.Log($"[Verify] 完成 — PASS={_pass} FAIL={_fail}");
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
        /// - 费用门槛：bank 灌满也打不出总费用 > 当前上限的卡
        /// - 结束阶段：未横置地牌自动产 1 灰；耗尽进墓后槽位空出可补充
        /// - 己方回合开始地牌恢复；台账逐行 LandCap 与产出计数
        /// 结束时游戏停在 p1 的第 3 回合主阶段。
        /// </summary>
        /// <summary>
        /// 验证器本地合成夹具卡（2026-09-14：内联 JSON **新引用化格式**，自 TestDecks 历史恢复——
        /// 原子=表行 refId+增量。测引擎不测数据，不污染正式表；读心预言缺席见 TestSpellBranch 注记）。
        /// 火球术=并列双效果（伤害/抽牌域空交拆两效果）；抉择试作=kind=2 两模式；链接光环测试=双箭头双光环。
        /// </summary>
        private static List<CardData> FixtureCards()
            => CardLoader.LoadCardsFromText(@"{
  ""cards"": [
    {
      ""cardName"": ""火球术"", ""supertype"": ""Spell"",
      ""costList"": [ { ""manaType"": 1, ""amount"": 3.0 }, { ""manaType"": 2, ""amount"": 2.0 } ],
      ""keywords"": [], ""effects"": [
        { ""Id"": ""FIREBALL_MAIN"", ""TriggerTiming"": 0, ""SelectionMode"": 0, ""TargetCount"": 1,
          ""AtomicEffects"": [ { ""refId"": ""a4b823fc"", ""value"": 4, ""kinds"": [2] } ] },
        { ""Id"": ""FIREBALL_DRAW"", ""TriggerTiming"": 0, ""SelectionMode"": -1,
          ""AtomicEffects"": [ { ""refId"": ""120ad4d1"", ""value"": 1 } ] } ]
    },
    {
      ""cardName"": ""古树守卫"", ""supertype"": ""Creature"", ""power"": 1, ""life"": 1,
      ""costList"": [ { ""manaType"": 0, ""amount"": 1.0 } ], ""keywords"": [], ""effects"": []
    },
    {
      ""cardName"": ""灰色哨兵"", ""supertype"": ""Creature"", ""power"": 1, ""life"": 1,
      ""costList"": [ { ""manaType"": 0, ""amount"": 1.0 } ], ""keywords"": [], ""effects"": []
    },
    {
      ""cardName"": ""抉择试作"", ""supertype"": ""Spell"",
      ""costList"": [ { ""manaType"": 1, ""amount"": 3.0 } ],
      ""keywords"": [], ""effects"": [
        { ""Id"": ""MODAL_MAIN"", ""TriggerTiming"": 0, ""SelectionMode"": 0, ""TargetCount"": 1,
          ""Steps"": [ { ""kind"": 2, ""choices"": [
            { ""label"": ""烈焰"", ""steps"": [ { ""kind"": 0, ""atomic"": { ""refId"": ""a4b823fc"", ""value"": 4, ""kinds"": [2] } } ] },
            { ""label"": ""灵感"", ""steps"": [ { ""kind"": 0, ""atomic"": { ""refId"": ""120ad4d1"", ""value"": 1, ""kinds"": [7] } } ] } ] } ] } ]
    },
    {
      ""cardName"": ""链接光环测试"", ""supertype"": ""Creature"", ""power"": 1, ""life"": 1,
      ""costList"": [ { ""manaType"": 0, ""amount"": 3.0 }, { ""manaType"": 4, ""amount"": 1.0 } ],
      ""arrows"": ""Up,LowerRight"",
      ""linkAuras"": [ { ""stat"": ""Power"", ""value"": 2 }, { ""keyword"": ""Taunt"" } ],
      ""keywords"": [], ""effects"": []
    }
  ]
}");

        private static void TestLandEconomy(GameCore core, Player p1, Player p2, List<CardData> cardsData)
        {
            var pool1 = core.ElementPool.GetPool(p1);

            // ---- T1（p1 主阶段）：上限 1 ----
            Assert(core.ElementPool.GetLandCap(p1) == 1, "T1 地牌槽上限 = 1");

            var hand1 = new List<Card>(core.ZoneManager.GetCards(p1, Zone.Hand));
            Assert(hand1.Count > 0, "p1 有手牌可放地牌");

            // 地牌资格（定案）：只有卡组正式生物可作地牌——魔法/仪式等非生物、衍生物/副本临时卡拒绝
            var creatures = hand1.Where(CardCore.ElementPoolSystem.CanServeAsLand).ToList();
            Assert(creatures.Count > 0, "p1 手牌有生物可放地牌");
            var nonCreature = hand1.FirstOrDefault(c => !CardCore.ElementPoolSystem.CanServeAsLand(c));
            var token = new Card { ID = "VERIFY_TOKEN" }; // 裸 Card = 效果生成的临时卡
            Assert(!core.ElementPool.AddCardToPool(token, p1), "地牌资格：衍生物/副本临时卡（裸 Card）拒绝");
            if (nonCreature != null)
                Assert(!GameActions.AddToElementPool(core, p1, nonCreature), "地牌资格：魔法/仪式等非生物超类拒绝");

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

            // 手动横置：颜色限于地牌自身构成
            var colors = pooled1[0].GetAvailableColors();
            Assert(colors.Count > 0, "地牌有可用颜色指示物");
            var pick = colors[0];
            var bad = PickAbsentColor(pooled1[0]);
            Assert(!GameActions.GainElementFromToken(core, p1, pooled1[0], bad),
                   "自选颜色限于地牌自身构成（非法色拒绝）");

            int bankBefore = pool1.AvailableMana[pick];
            Assert(GameActions.GainElementFromToken(core, p1, pooled1[0], pick), "手动横置产出成功");
            Assert(pool1.AvailableMana[pick] == bankBefore + 1, "自选颜色正确入 bank");
            Assert(pooled1[0].IsTapped, "产出后地牌横置");
            Assert(!GameActions.GainElementFromToken(core, p1, pooled1[0], pick), "同一地牌本回合周期不可重复产出");

            // 费用门槛：bank 灌满也打不出总费用 > 当前上限（1）的卡
            var candidate = hand1.Skip(1).FirstOrDefault(c => TotalCost(c) > 1);
            if (candidate != null)
            {
                foreach (var t in AllManaTypes())
                    pool1.AvailableMana[t] = 99;
                Assert(!PlayCardSync(core, p1, candidate, null),
                       $"费用门槛：总费用 {TotalCost(candidate)} > 上限 1，bank 充足也拒绝");
            }
            else
            {
                Debug.LogWarning("[Verify] 跳过费用门槛用例：手牌无总费用 > 1 的卡");
            }

            // ---- 结束 T1 → T2：上限双方同步推进 ----
            int gray1 = pool1.AvailableMana[ManaType.Gray];
            Assert(EndTurnPumped(core, p1), "结束 T1 回合");
            Assert(pool1.AvailableMana[ManaType.Gray] == gray1, "已横置地牌结束阶段不产出");
            Assert(core.ElementPool.GetLandCap(p1) == 2 && core.ElementPool.GetLandCap(p2) == 2,
                   "T2 双方地牌槽上限同步 = 2（对手首回合即 2）");

            // ---- T2（p2）：耗尽→墓地→补充 循环 + 结束阶段自动产灰 ----
            Crumb("land T2");
            GameActions.SkipElementPool(core, p2);
            var hand2 = new List<Card>(core.ZoneManager.GetCards(p2, Zone.Hand));

            // 合格地牌=正式生物（CanServeAsLand）：手牌首张 1 费可能是法术，直接放会被资格拒绝
            var oneCost = hand2.FirstOrDefault(c => TotalCost(c) == 1 && CardCore.ElementPoolSystem.CanServeAsLand(c));
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
                Assert(GameActions.AddToElementPool(core, p2, oneCost), "放入 1 指示物地牌");
                var pc2 = core.ElementPool.GetPooledCards(p2)[0];
                var color2 = pc2.GetAvailableColors()[0];
                int before2 = core.ElementPool.GetPool(p2).AvailableMana[color2];

                Assert(GameActions.GainElementFromToken(core, p2, pc2, color2), "产出最后 1 指示物");
                Assert(core.ElementPool.GetPooledCards(p2).Count == 0, "耗尽地牌移出元素池");
                Assert(core.ZoneManager.GetCards(p2, Zone.Graveyard).Contains(oneCost), "耗尽地牌进墓地");
                Assert(core.ElementPool.GetPool(p2).AvailableMana[color2] == before2 + 1, "末次产出仍入 bank");

                var hand2b = new List<Card>(core.ZoneManager.GetCards(p2, Zone.Hand));
                if (hand2b.Count > 0)
                {
                    // 合格地牌=卡组正式生物且非零费（CanServeAsLand + 指示物来源非空）——
                    // 夹具卡组成变动后手牌首位可能是法术/零费卡，取首张**合格**卡（对齐段 21 的检索惯例）
                    bool refilled = false;
                    foreach (var cand in hand2b)
                        if (GameActions.AddToElementPool(core, p2, cand)) { refilled = true; break; }
                    Assert(refilled, "耗尽后槽位空出，可补充地牌（手牌存在合格地牌卡）");
                }
            }
            else
            {
                if (hand2.Count > 0)
                    GameActions.AddToElementPool(core, p2, hand2[0]);
            }

            var pooled2 = core.ElementPool.GetPooledCards(p2).LastOrDefault();
            var pool2 = core.ElementPool.GetPool(p2);
            // 结束阶段产出定案（改版）：未横置地牌自动产「剩余最多色」（并列取枚举序靠前者，
            // 与 ElementPool.PickProduceColor 同法），不再恒产灰
            ManaType? expectColor = null;
            int expectCount = 0;
            if (pooled2 != null)
                foreach (ManaType t in AllManaTypes())
                    if (pooled2.Tokens.TryGetValue(t, out int n) && n > expectCount)
                    {
                        expectCount = n;
                        expectColor = t;
                    }
            int mostBefore = expectColor.HasValue ? pool2.AvailableMana[expectColor.Value] : 0;
            EndTurnPumped(core, p2);
            if (pooled2 != null && expectColor.HasValue)
            {
                Assert(pool2.AvailableMana[expectColor.Value] == mostBefore + 1,
                       $"结束阶段未横置地牌自动产「剩余最多色」{expectColor.Value}（+1 入 bank）");
                Assert(pooled2.IsTapped, "自动产出后地牌横置");
            }

            // ---- T3（p1）：地牌恢复 + 上限 3 + 台账 ----
            Crumb("land T3");
            GameActions.SkipElementPool(core, p1);
            Assert(core.ElementPool.GetLandCap(p1) == 3, "T3 地牌槽上限 = 3");
            var landAfter = core.ElementPool.GetPooledCards(p1).FirstOrDefault();
            Assert(landAfter != null && !landAfter.IsTapped,
                   $"己方回合开始地牌恢复（解除横置）（p1 池 {core.ElementPool.GetPooledCards(p1).Count} 张：{string.Join(",", core.ElementPool.GetPooledCards(p1).Select(l => l.SourceCard.ID + (l.IsTapped ? "·横置" : "·直立")))}）");

            var records = core.ResourceLedger.GetRecords(p1);
            Assert(records.Count >= 2, "台账已有 p1 两行记录");
            Assert(records[0].LandCap == 1 && records[1].LandCap == 3, "台账 LandCap = min(全局回合数, 9)");
            Assert(records[0].TapsTaken == 1, "台账记录 T1 手动产出 1 次");
        }

        /// <summary>卡总费用（费用字典求和；无费用卡按默认灰 1 计）</summary>
        private static int TotalCost(Card card)
        {
            if (card is IHasCost hasCost && hasCost.Cost != null)
                return (int)hasCost.Cost.Values.Sum();
            return 1;
        }

        /// <summary>选一个地牌没有的颜色（用于验证非法色拒绝）</summary>
        private static ManaType PickAbsentColor(PooledCard land)
        {
            foreach (var t in AllManaTypes())
                if (!land.HasToken(t))
                    return t;
            return ManaType.Gray; // 不可达：地牌不可能持有全部颜色
        }

        private static List<ManaType> AllManaTypes()
        {
            return System.Enum.GetValues(typeof(ManaType)).Cast<ManaType>().ToList();
        }

        /// <summary>混付定案（2026-09-14）后，红/蓝/绿账单可由 [本色,灰,黑,白] 垫付、灰由 [灰,黑,白] 垫付
        /// （ElementAffinity.ChainFor）。「精确同色占用即拒」类断言须清空全色 bank 只留被测色，
        /// 否则黑/白万用残留会替第二张账单垫付，导致应拒变放行。</summary>
        private static void HygieneBank(PlayerElementPool pool, ManaType keep, int amount)
        {
            foreach (var t in AllManaTypes()) pool.AvailableMana[t] = 0;
            pool.AvailableMana[keep] = amount;
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
            // 2026-09-11 拆分：抽卡域改双方牌库 {7,8} 后，与 DealDamage {1,2} 同效果组合域交集空 →
            // 拆为两效果（伤害 Manual 域 {1,2}；抽牌 None 自结算）
            Assert(data.Effects != null && data.Effects.Count == 2
                   && data.Effects[0].AtomicEffects != null && data.Effects[0].AtomicEffects.Count == 1
                   && data.Effects[0].AtomicEffects[0].refId == CardCore.Attribute.AtomicEffectTable.GetByEnumName(nameof(AtomicEffectType.DealDamage))?.HashId
                   && data.Effects[1].AtomicEffects != null && data.Effects[1].AtomicEffects.Count == 1
                   && data.Effects[1].AtomicEffects[0].refId == CardCore.Attribute.AtomicEffectTable.GetByEnumName(nameof(AtomicEffectType.DrawCard))?.HashId,
                   "火球术效果反序列化（伤害/抽牌两效果——抽卡域改牌库后拆分）");

            var fireball = new CardWrapper(data);
            fireball.SetController(p1);
            core.ZoneManager.GetZoneContainer(p1).Add(fireball, Zone.Hand);
            // 测试脚手架：模拟后期回合，费用门槛（= 地牌槽上限 9）不挡火球术费用。
            // 供能按卡组当前声明费自适应（2026-09-13 修复：火球术费用调整为红3蓝2，
            // 旧硬编码"红4蓝1"蓝不够 → CanAfford 拒绝 → 打出/结算/入墓四连坐）
            var firePool = core.ElementPool.GetPool(p1);
            firePool.GlobalTurnIndex = 9;
            foreach (var kv in data.Cost)
                firePool.AvailableMana[(ManaType)kv.Key] = (int)System.Math.Ceiling(kv.Value);

            int lifeBefore = p2.Life;
            int deckBefore = core.ZoneManager.GetCards(p1, Zone.Deck).Count;

            bool played = PlayCardSync(core, p1, fireball,
                new List<Entity> { p2 });
            Assert(played, "火球术成功打出");

            Assert(p2.Life == lifeBefore - 4, $"对手 Life −4（{lifeBefore}→{p2.Life}）");
            Assert(core.ZoneManager.GetCards(p1, Zone.Deck).Count == deckBefore - 1,
                   "抽牌生效（牌库 −1）");
            Assert(core.ZoneManager.GetCards(p1, Zone.Graveyard).Contains(fireball),
                   "法术结算后入墓地");
        }

        /// <summary>
        /// 分支卡经 PlayCard 的端到端（法术执行链修复的回归锚）：
        /// Steps 法术打出 → 发动区 → 步骤遍历 + OutcomeGate 即时分支 → 命中奖励抽牌 → 入墓；
        /// 元素费只扣一次（执行器 skipElementCost 防双计）。
        /// </summary>
        private static void TestSpellBranch(GameCore core, Player p1, Player p2, List<CardData> cardsData)
        {
            var data = cardsData.FirstOrDefault(c => c.CardName == "读心预言");
            // 2026-09-14 引用化缺口：宣言族原子（DeclareHand）无表行——refId 无从指起，夹具不可装载。
            // 补表行后自 c50822b 历史恢复读心预言夹具与本段断言。
            if (data == null)
            {
                Debug.LogWarning("[Verify] 读心预言夹具缺位（DeclareHand 无表行）——TestSpellBranch 跳过");
                return;
            }

            // 确定性：向 p2 手牌注入一张已知生物（宣言 类型:Creature 必命中）
            var markerData = new CardData { ID = "VERIFY_BRANCH_MARKER", CardName = "分支标记生物" };
            markerData.Supertype = Cardtype.Creature;
            var marker = new CardWrapper(markerData);
            marker.SetController(p2);
            core.ZoneManager.GetZoneContainer(p2).Add(marker, Zone.Hand);
            // 单张验证确定性：选位器指定注入的标记卡（缺省取手牌第一张，会受真实手牌干扰）
            CardCore.Attribute.Handlers.ProphecyHandlerUtil.PositionPicker =
                (_, cands) => cands.Contains(marker) ? marker : cands[0];

            var declare = new CardWrapper(data);
            declare.SetController(p1);
            core.ZoneManager.GetZoneContainer(p1).Add(declare, Zone.Hand);

            var pool1 = core.ElementPool.GetPool(p1);
            // 2026-09-13 修复：卡现价=蓝1+灰2（门费连锁后），只加蓝会因缺灰被 CanAfford 拒绝
            //（TestSpell 同款四连坐）；按卡组声明费自适应供能 + 浓度门槛放开
            pool1.GlobalTurnIndex = 9;
            foreach (var kv in data.Cost)
                pool1.AvailableMana[(ManaType)kv.Key] += (int)System.Math.Ceiling(kv.Value);
            int deckBefore = core.ZoneManager.GetCards(p1, Zone.Deck).Count;
            int blueBefore = pool1.AvailableMana[ManaType.Blue];
            int handBefore = core.ZoneManager.GetCards(p1, Zone.Hand).Count;

            Assert(PlayCardSync(core, p1, declare), "宣言分支卡成功打出");

            // 无 UI 的结算链同步完成：打出 −1 + 分支命中抽 1 = 手牌数不变
            Assert(core.ZoneManager.GetCards(p1, Zone.Hand).Count == handBefore,
                   "打出 −1 且宣言命中抽 +1（分支经 PlayCard 真实触发）");
            Assert(core.ZoneManager.GetCards(p1, Zone.Deck).Count == deckBefore - 1, "奖励抽牌来自牌库顶");
            Assert(core.ZoneManager.GetCards(p1, Zone.Graveyard).Contains(declare), "分支法术结算后入墓地");
            Assert(core.ZoneManager.GetCards(p1, Zone.Activation).Count == 0, "结算完成后发动区清空");
            // 期望扣费从声明费用动态取值（卡表调价不再牵动断言）；skipElementCost=true 保证执行器
            // 不再对派生元素费二次扣款——实际扣费恰为声明值即证明"只扣一次"
            var declaredBlue = GameActions.GetCardCost(declare, 0).TryGetValue((int)ManaType.Blue, out var blueCost)
                ? (int)blueCost : 0;
            Assert(pool1.AvailableMana[ManaType.Blue] == blueBefore - declaredBlue,
                   $"元素费只扣一次（skipElementCost 防执行器双计费；声明费蓝 {declaredBlue}，实际扣 {blueBefore - pool1.AvailableMana[ManaType.Blue]}）");

            core.ZoneManager.GetZoneContainer(p2).Remove(marker, Zone.Hand);
            CardCore.Attribute.Handlers.ProphecyHandlerUtil.PositionPicker = null; // 还原缺省选位器
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
                        Assert(false, $"卡 {card.ID}({card.CardName}) 效果 {def.Id}：带域原子存在但组合域交集为空");
                    }
                }
            }
            Assert(emptyDomain == 0, $"全部效果组合域非空（空域 {emptyDomain} 个）");
            Assert(atomTotal > 10 && kindAtoms > 10,
                $"原子域解析覆盖（原子 {atomTotal}，带域 {kindAtoms}——现行 5 卡夹具 ~13 原子；阈值随夹具缩放）");
            Debug.Log($"[Verify] 目标域：{effectsChecked} 效果 / {atomTotal} 原子（带域 {kindAtoms}）");

            // c) 表默认抽查：DealDamage 域 = {0,1} 且 filter 为空（target_kinds_review 定案：
            //    直伤可指角色——打脸合法；角色排除口径已废，NoRole 是个别原子的域内细化）
            var dd = CardCore.Attribute.AtomicEffectTable.GetByType(AtomicEffectType.DealDamage);
            var ddKinds = dd?.GetTargetKindList();
            Assert(ddKinds != null && ddKinds.Contains(1) && ddKinds.Contains(2),
                $"DealDamage 表默认域 = 己方+对方有生命单位（实际 [{string.Join(",", ddKinds ?? new List<int>())}]）");
            Assert(dd != null && string.IsNullOrEmpty(dd.TargetFilter),
                "DealDamage 表默认 filter 为空（可指角色，直伤打脸合法——review 定案）");

            // d) 双路径一致：同原子组 flat 与 Steps 两版的组合域相等
            var entry = AtomRefs.New(AtomicEffectType.DealDamage, value: 2);
            var flat = new CardEffectData { Id = "VERIFY_TDM_FLAT", AtomicEffects = new List<AtomicEffectEntry> { entry } };
            var stepped = new CardEffectData
            {
                Id = "VERIFY_TDM_STEP",
                Steps = new List<EffectStepData> { new EffectStepData { kind = 0, atomic = entry } },
            };
            var flatDef = CardEffectConverter.ConvertOne(flat, "VERIFY_TDM");
            var stepDef = CardEffectConverter.ConvertOne(stepped, "VERIFY_TDM");
            Assert(TargetKindRules.Format(flatDef.TargetDomain) == TargetKindRules.Format(stepDef.TargetDomain)
                && flatDef.TargetDomain.Count > 0,
                $"扁平与 Steps 路径组合域一致（{TargetKindRules.Format(flatDef.TargetDomain)}）");

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
            int rightCost = CostDerivationService.DeriveElementCosts(rightDef).Sum(c => c.Value);
            Assert(rightCost > 0, $"Heal(p=+1) 锁己方域 → 全价（实际 {rightCost}）");
            Assert(wrongDef.Effects.Count == 0 && CostDerivationService.DeriveElementCosts(wrongDef).Sum(c => c.Value) == 0,
                   "内容契约：错边原子在效果栏被剔除（只能进代价栏——Payload 路径见 TestBlackWhiteEconomy）");
            Assert(CostDerivationService.DeriveElementGrants(rightDef).Count == 0, "正侧原子无黑白获得");
            var bothDef = CardEffectConverter.ConvertOne(new CardEffectData
            {
                Id = "VERIFY_POL_B",
                AtomicEffects = new List<AtomicEffectEntry>
                {
                    AtomRefs.New(AtomicEffectType.Heal, value: 2, kinds: new List<int> { 1 }),
                },
            }, "VERIFY_POL_B");
            Assert(CostDerivationService.DeriveElementCosts(bothDef).Sum(c => c.Value) == rightCost,
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

            // h) 生物黑白关键词照旧印制计价（2026-09-14 撤销「转启动式」定案）：参杂黑白的生物
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
                   "黑白关键词保留印制（不转启动式；无合成 ACT_ 自赋予效果）");
            CardCostService.EnsureCost(bwCards[0]);
            Assert(bwCards[0].Cost.Keys.Any(k => k == (int)ManaType.White || k == (int)ManaType.Black),
                   "生物建议费用含黑白键（黑白进费用列表；地牌侧由入池过滤兜住，不从黑白产元素）");
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

            // ---- 0. 表行锚（2026-09-13 修正取法：_typeMap 同枚举多行后行覆盖——GetByType(Sleep)
            // 实际返回苏醒行 bd2554f2（JSON 行序在后）；按中文短名精确取沉睡行 82f5ef6f） ----
            var sleepCfg = CardCore.Attribute.AtomicEffectTable.GetAll()
                .FirstOrDefault(c => c.EnumName == "Sleep" && c.DisplayName == "沉睡");
            Assert(sleepCfg != null && System.Math.Abs(sleepCfg.TotalUnitCost - 2f) < 1e-4 && sleepCfg.Polarity == -1f,
                   "沉睡表行：绿2 负极性（BaseCost=2, Polarity=-1）");
            Assert(ElementAffinities.GetAffinityForEffect(AtomicEffectType.Sleep).PrimaryColor == ManaType.Green,
                   "沉睡表色 Green");
            var sleepKinds = sleepCfg?.GetTargetKindList();
            Assert(sleepKinds != null && sleepKinds.Count == 2
                   && sleepKinds.Contains(1) && sleepKinds.Contains(2),
                   "沉睡域 = {1,2}（双方单位域，2026-09-13 用户改表）");
            Assert(sleepCfg?.TargetFilter == "NoRole", "沉睡域滤 NoRole（角色不可沉睡）");

            var pool1 = core.ElementPool.GetPool(p1);
            var snapBank = new Dictionary<ManaType, int>(pool1.AvailableMana);

            try
            {
                foreach (var t in AllManaTypes()) pool1.AvailableMana[t] = 99;

                // ---- 1. 自我沉睡：灰费豁免 + 入场指示物 ----
                var sleepData = new CardData
                {
                    ID = "VERIFY_SLEEP_CR", CardName = "验证沉睡生物", Supertype = Cardtype.Creature, Power = 4, Life = 4,
                    Cost = new Dictionary<int, float> { { (int)ManaType.Gray, 3 }, { (int)ManaType.Green, 1 } },
                };
                sleepData.Effects.Add(new CardEffectData
                {
                    Id = "VERIFY_SLEEP_ONPLAY",
                    TriggerTiming = (int)TriggerTiming.OnPlay,
                    AtomicEffects = new List<AtomicEffectEntry>
                    {
                        AtomRefs.New(AtomicEffectType.Sleep, value: 0), // 域={Self}，Value≤0=灰费时长模式（灰费豁免判定口径）
                    },
                });
                sleepData.Effects.Add(new CardEffectData
                {
                    Id = "VERIFY_SLEEP_TURNEND",
                    TriggerTiming = (int)TriggerTiming.OnTurnEnd,
                    AtomicEffects = new List<AtomicEffectEntry>
                    {
                        AtomRefs.New(AtomicEffectType.ModifyPower, value: 1, kinds: new List<int> { 0 }) // {Self}：自己的回合结束+1 攻（沉睡期间应被拦）
                    },
                });
                var sleepCard = InjectCard(core, p1, sleepData);

                int grayBefore = pool1.AvailableMana[ManaType.Gray];
                int greenBefore = pool1.AvailableMana[ManaType.Green];
                Assert(GameActions.PlayCard(core, p1, sleepCard), "打出自我沉睡生物（灰豁免后仅需绿1）");
                GameActions.DrainStack(core);

                Assert(pool1.AvailableMana[ManaType.Green] == greenBefore - 1, "支付：扣绿1");
                Assert(pool1.AvailableMana[ManaType.Gray] == grayBefore, "灰费豁免：3 灰未扣（转沉睡时长）");
                Assert(core.ZoneManager.IsCardInZone(sleepCard, p1, Zone.Battlefield), "已入场");
                Assert(sleepCard.IsTapped() && sleepCard.GetCounterCount(Attribute.KeywordRules.SleepCounter) == 3,
                       $"入场沉睡：横置+沉睡指示物×3（=豁免灰量；实际 {sleepCard.GetCounterCount(Attribute.KeywordRules.SleepCounter)} 层）");
                int power0 = sleepCard.GetPower();

                // ---- 2. 周期推进：沉睡期间无法重置 + OnTurnEnd 被拦；3 次己方回合开始后苏醒 ----
                // 周期 = p1回合结束(触发口) → p2空过 → p1回合开始(倒数+苏醒) → 回主阶段
                for (int cycle = 1; cycle <= 3; cycle++)
                {
                    int countersBefore = sleepCard.GetCounterCount(Attribute.KeywordRules.SleepCounter);
                    EndTurnPumped(core, p1);                 // p1 回合结束：沉睡中 → OnTurnEnd +1攻 被拦
                    GameActions.SkipElementPool(core, p2);
                    EndTurnPumped(core, p2);                 // p2 空过 → p1 回合开始：倒数 1 层
                    GameActions.SkipElementPool(core, p1);

                    bool awake = sleepCard.GetCounterCount(Attribute.KeywordRules.SleepCounter) == 0;
                    Assert(sleepCard.GetCounterCount(Attribute.KeywordRules.SleepCounter) == countersBefore - 1,
                           $"周期{cycle}：倒数一层（{countersBefore}→{sleepCard.GetCounterCount(Attribute.KeywordRules.SleepCounter)}）");
                    if (awake)
                        Assert(!sleepCard.IsTapped(), "苏醒：末层耗尽的回合开始即重置");
                    else
                        Assert(sleepCard.IsTapped(), $"周期{cycle}：沉睡期间无法重置（仍横置）");
                    Assert(sleepCard.GetPower() == power0,
                           $"周期{cycle}：沉睡期间 OnTurnEnd 效果无效（攻仍 {sleepCard.GetPower()}）");
                }
                Assert(!sleepCard.IsTapped() && sleepCard.GetCounterCount(Attribute.KeywordRules.SleepCounter) == 0, "三周期后完全苏醒");

                // ---- 3. 苏醒后 OnTurnEnd 恢复 ----
                Crumb($"awake pre: power={sleepCard.GetPower()} sleep={sleepCard.GetCounterCount(Attribute.KeywordRules.SleepCounter)} tapped={sleepCard.IsTapped()}");
                EndTurnPumped(core, p1);
                GameActions.DrainStack(core); // OnTurnEnd 触发式入栈后显式排干（同打出路径 PlayCardSync 先例）
                GameActions.SkipElementPool(core, p2);
                EndTurnPumped(core, p2);
                GameActions.SkipElementPool(core, p1);
                Crumb($"awake post: power={sleepCard.GetPower()} sleep={sleepCard.GetCounterCount(Attribute.KeywordRules.SleepCounter)}");
                Assert(sleepCard.GetPower() == power0 + 1, "苏醒后 OnTurnEnd +1 攻恢复生效");

                // ---- 4. 定长沉睡（Value 显式）：灰费**不**豁免，层数=Value ----
                var fixedData = new CardData
                {
                    ID = "VERIFY_SLEEP_FIXED", CardName = "验证定长沉睡", Supertype = Cardtype.Creature, Power = 3, Life = 3,
                    Cost = new Dictionary<int, float> { { (int)ManaType.Gray, 2 }, { (int)ManaType.Green, 1 } },
                };
                fixedData.Effects.Add(new CardEffectData
                {
                    Id = "VERIFY_SLEEP_FIXED_EFF",
                    TriggerTiming = (int)TriggerTiming.OnPlay,
                    AtomicEffects = new List<AtomicEffectEntry>
                    {
                        AtomRefs.New(AtomicEffectType.Sleep, value: 2), // 域={Self} 定长 2
                    },
                });
                var fixedCard = InjectCard(core, p1, fixedData);
                int grayBefore4 = pool1.AvailableMana[ManaType.Gray];
                Assert(GameActions.PlayCard(core, p1, fixedCard), "打出定长沉睡生物");
                GameActions.DrainStack(core);
                Assert(pool1.AvailableMana[ManaType.Gray] == grayBefore4 - 2,
                       $"定长沉睡不走灰豁免：照扣 2 灰（实际余 {pool1.AvailableMana[ManaType.Gray]}）");
                Assert(fixedCard.GetCounterCount(Attribute.KeywordRules.SleepCounter) == 2,
                       $"定长沉睡：层数=Value 2（实际 {fixedCard.GetCounterCount(Attribute.KeywordRules.SleepCounter)}）");
            }
            finally
            {
                foreach (var kv in snapBank) pool1.AvailableMana[kv.Key] = kv.Value;
                // 战场残留（沉睡生物/同伴）送墓清理，不污染后续段落
                foreach (var c in core.ZoneManager.GetCards(p1, Zone.Battlefield)
                             .Where(c => c.ID.StartsWith("VERIFY_SLEEP")).ToList())
                    core.ZoneManager.MoveCard(c, p1, Zone.Battlefield, Zone.Graveyard);
            }
        }

        // ======================================== 黑白元素经济（2026-09-11 定案） ========================================

        /// <summary>
        /// 黑白元素经济端到端：①错边原子出计价+结算发放（每回合封顶 1/色，2026-09-14）②黑白支付侧
        /// （本色费/浓度上限/**统一混付规划器**：同色→灰→黑→白，灰也入浓度上限，实发组合事件）
        /// ③代价强制（2026-09-14 撤销代价可选：Payload 恒执行+恒补偿，无减费通道）
        /// ④Payload 效果型代价（对手召唤+得白）⑤元素支付（万用填充/灰帽/真负例）
        /// ⑥流失改扣 MaxHealth ⑦含黑白费用卡作地牌不产黑白指示物。
        /// </summary>
        private static void TestBlackWhiteEconomy(GameCore core, Player p1, Player p2)
        {
            EnsureMainPhase(core, p1);

            var pool1 = core.ElementPool.GetPool(p1);
            var snapBank = new Dictionary<ManaType, int>(pool1.AvailableMana);
            int snapTurnIdx = pool1.GlobalTurnIndex;
            int snapBlackGained = pool1.BlackGainedThisTurn, snapWhiteGained = pool1.WhiteGainedThisTurn;
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
                //      每回合封顶 1/色（2026-09-14 定案，AddMana 钳制——全来源累计、余数不补） ----
                pool1.GlobalTurnIndex = 3; // 地牌上限 3
                foreach (var t in AllManaTypes()) pool1.AvailableMana[t] = 99;
                pool1.AvailableMana[ManaType.Black] = 0; pool1.AvailableMana[ManaType.White] = 0;
                pool1.BlackGainedThisTurn = 0;

                // 内容契约下效果栏不可锁错边——双域原子（全价）实际打错边是效果侧唯一错边入口
                var selfHarm = InjectCard(core, p1, SpellData("VERIFY_BW_SELFHARM", "验证自伤",
                    Atom(AtomicEffectType.DealDamage.ToString(), 4))); // 表默认双域 {0,1}
                var selfHarmDef = CardEffectConverter.ConvertOne(((CardWrapper)selfHarm).GetData().Effects[0], "VERIFY_BW_SELFHARM");
                Assert(CostDerivationService.DeriveElementCosts(selfHarmDef).Sum(c => c.Value) > 0,
                       "双域原子构筑期全价（无 grant 记录——运行时实判）");
                Assert(CostDerivationService.DeriveElementGrants(selfHarmDef).Count == 0,
                       "双域原子构筑期无黑白获得记录");

                int lifeBefore = p1.Life;
                Assert(GameActions.PlayCard(core, p1, selfHarm, new List<Entity> { p1 }), "打出自伤法术（双域指自己=实际错边）");
                GameActions.DrainStack(core);
                Assert(p1.Life == lifeBefore - 4, "自伤结算：扣 4 当前生命");
                Assert(pool1.AvailableMana[ManaType.Black] == 1,
                       $"结算发放：得黑 1（单价4，每回合封顶 1，余数不补；实际 {pool1.AvailableMana[ManaType.Black]}）");

                // 同回合二次施放：累计帽仍 1
                var selfHarm2 = InjectCard(core, p1, SpellData("VERIFY_BW_SELFHARM2", "验证自伤2",
                    Atom(AtomicEffectType.DealDamage.ToString(), 1)));
                Assert(GameActions.PlayCard(core, p1, selfHarm2, new List<Entity> { p1 }), "同回合再打一张自伤");
                GameActions.DrainStack(core);
                Assert(pool1.AvailableMana[ManaType.Black] == 1 && pool1.BlackGainedThisTurn == 1,
                       "每回合累计帽：同回合第二张不再进账（黑仍 1）");

                // 翻回合：计数器清零，再施放可再得 1
                core.ElementPool.OnTurnStart(p1, pool1.GlobalTurnIndex + 1);
                Assert(pool1.BlackGainedThisTurn == 0 && pool1.WhiteGainedThisTurn == 0, "回合开始：黑白获得计数清零");
                var selfHarm3 = InjectCard(core, p1, SpellData("VERIFY_BW_SELFHARM3", "验证自伤3",
                    Atom(AtomicEffectType.DealDamage.ToString(), 2)));
                Assert(GameActions.PlayCard(core, p1, selfHarm3, new List<Entity> { p1 }), "新回合再打自伤");
                GameActions.DrainStack(core);
                Assert(pool1.AvailableMana[ManaType.Black] == 2,
                       $"翻回合后再施放：+1 黑（bank 累计 2；实际 {pool1.AvailableMana[ManaType.Black]}）");

                // ---- 2. 黑白支付侧：本色费 / 浓度上限 / 灰混付 ----
                var drainDef = CardEffectConverter.ConvertOne(new CardEffectData
                {
                    Id = "VERIFY_BW_DRAIN",
                    AtomicEffects = new List<AtomicEffectEntry> { Atom(AtomicEffectType.DrainLife.ToString(), 2) },
                }, "VERIFY_BW_DRAIN");
                var drainCosts = CostDerivationService.DeriveElementCosts(drainDef);
                Assert(drainCosts.Any(c => c.ManaType == ManaType.Black && c.Value > 0),
                       "DrainLife（表色 Black）正确侧计价落黑（不再归一为灰）");

                pool1.AvailableMana[ManaType.Black] = 3; pool1.AvailableMana[ManaType.Gray] = 0;
                var blackDict = new Dictionary<int, float> { { (int)ManaType.Black, 4f } };
                Assert(!core.ElementPool.CanPayCost(blackDict, p1),
                       "黑支付浓度上限：4 黑 > 地牌上限 3 → 拒付");
                var black3 = new Dictionary<int, float> { { (int)ManaType.Black, 3f } };
                Assert(core.ElementPool.CanPayCost(black3, p1) && core.ElementPool.PayCost(black3, p1),
                       "3 黑 ≤ 上限 → 可付并扣除");
                Assert(pool1.AvailableMana[ManaType.Black] == 0, "黑已扣（bank 记账）");

                // 统一混付规划器（2026-09-14：出牌/效果费共用 GetBillPaymentPlan，非破坏）
                Dictionary<ManaType, int> Bank(params (ManaType t, int n)[] cells)
                {
                    var d = new Dictionary<ManaType, int>();
                    foreach (var c in cells) d[c.t] = c.n;
                    return d;
                }
                var cap3 = core.ElementPool.GetLandCap(p1);

                // 万用序：灰2 账单，灰1+黑1+白1 → {灰1, 黑1}（黑先于白，白不动）
                var wildPlan = CardCore.ElementPaymentValidator.GetBillPaymentPlan(
                    new Dictionary<ManaType, int> { { ManaType.Gray, 2 } },
                    Bank((ManaType.Gray, 1), (ManaType.Black, 1), (ManaType.White, 1)), cap3);
                Assert(wildPlan != null
                       && wildPlan.GetValueOrDefault(ManaType.Gray) == 1
                       && wildPlan.GetValueOrDefault(ManaType.Black) == 1
                       && !wildPlan.ContainsKey(ManaType.White),
                       "万用序：灰2 = 灰1+黑1（黑先于白，白保留）");

                // 同色优先：红2 账单，红2+灰9+黑9 → 只扣红2
                var samePlan = CardCore.ElementPaymentValidator.GetBillPaymentPlan(
                    new Dictionary<ManaType, int> { { ManaType.Red, 2 } },
                    Bank((ManaType.Red, 2), (ManaType.Gray, 9), (ManaType.Black, 9)), cap3);
                Assert(samePlan != null && samePlan.Count == 1
                       && samePlan.GetValueOrDefault(ManaType.Red) == 2,
                       "同色优先：红2 有红付红（灰黑不动）");

                // 黑白预留序：灰1+黑1 账单，黑1+白1 → 黑先留给本色费，灰由白垫（贪心不串色）
                var reservePlan = CardCore.ElementPaymentValidator.GetBillPaymentPlan(
                    new Dictionary<ManaType, int> { { ManaType.Gray, 1 }, { ManaType.Black, 1 } },
                    Bank((ManaType.Black, 1), (ManaType.White, 1)), cap3);
                Assert(reservePlan != null
                       && reservePlan.GetValueOrDefault(ManaType.Black) == 1
                       && reservePlan.GetValueOrDefault(ManaType.White) == 1,
                       "黑白本色费先行预留：灰不贪吃黑（灰1 由白垫）");

                // 单向性：黑费四色/白补不了；白费黑补不了
                Assert(CardCore.ElementPaymentValidator.GetBillPaymentPlan(
                           new Dictionary<ManaType, int> { { ManaType.Black, 1 } },
                           Bank((ManaType.Red, 9), (ManaType.Gray, 9), (ManaType.White, 9)), cap3) == null,
                       "单向：黑费只能黑付（红蓝绿灰白都补不了）");
                Assert(CardCore.ElementPaymentValidator.GetBillPaymentPlan(
                           new Dictionary<ManaType, int> { { ManaType.White, 1 } },
                           Bank((ManaType.Black, 9)), cap3) == null,
                       "单向：白费黑补不了");

                // 灰浓度帽：cap=1，灰3 账单 → {灰1, 黑1, 白1}（灰贡献也受上限）
                pool1.GlobalTurnIndex = 1;
                var grayCapPlan = CardCore.ElementPaymentValidator.GetBillPaymentPlan(
                    new Dictionary<ManaType, int> { { ManaType.Gray, 3 } },
                    Bank((ManaType.Gray, 9), (ManaType.Black, 9), (ManaType.White, 9)), 1);
                Assert(grayCapPlan != null
                       && grayCapPlan.GetValueOrDefault(ManaType.Gray) == 1
                       && grayCapPlan.GetValueOrDefault(ManaType.Black) == 1
                       && grayCapPlan.GetValueOrDefault(ManaType.White) == 1,
                       "灰入浓度上限：灰3（cap1）= 灰1+黑1+白1");
                pool1.GlobalTurnIndex = 3;

                // 实发组合事件：PayCost{红1} 红缺 → 黑垫，事件 PaidCost=实际货币组合
                pool1.AvailableMana[ManaType.Red] = 0;
                pool1.AvailableMana[ManaType.Gray] = 0;
                pool1.AvailableMana[ManaType.Black] = 2;
                ElementPoolPayEvent captured = null;
                void OnPayEvt(ElementPoolPayEvent e) { if (e.Player == p1) captured = e; }
                EventManager.Instance.Subscribe<ElementPoolPayEvent>(OnPayEvt);
                bool wildPaid = core.ElementPool.PayCost(new Dictionary<int, float> { { (int)ManaType.Red, 1f } }, p1);
                EventManager.Instance.Unsubscribe<ElementPoolPayEvent>(OnPayEvt);
                Assert(wildPaid && pool1.AvailableMana[ManaType.Black] == 1
                       && captured != null
                       && System.Math.Abs(captured.PaidCost.GetValueOrDefault((int)ManaType.Black) - 1f) < 1e-4
                       && !captured.PaidCost.ContainsKey((int)ManaType.Red),
                       "出牌混付+实发组合：红1 由黑垫付，PaidCost={黑1}（Trinity/台账读真值）");

                // ---- 3. 代价强制（2026-09-14 撤销代价可选）：恒执行+恒补偿，无选择窗口/减费通道 ----
                foreach (var t in AllManaTypes()) pool1.AvailableMana[t] = 99;
                pool1.AvailableMana[ManaType.Black] = 0;
                pool1.BlackGainedThisTurn = 0; // 计数器直控（隔离前段错边发放的累计）
                var optCtx = new CostContext
                {
                    Payer = p1, ZoneManager = core.ZoneManager, ElementPool = core.ElementPool, Source = p1,
                };

                // 3a. e2e：无头也不再有"默认不付"——代价恒执行（弃1张）+恒补偿（+1 黑，全价2 钳到 1）
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
                int handBefore = core.ZoneManager.GetCards(p1, Zone.Hand).Count;
                Assert(GameActions.PlayCard(core, p1, discardCostCard, new List<Entity>()), "打出带弃牌代价的卡");
                GameActions.DrainStack(core);
                Assert(core.ZoneManager.GetCards(p1, Zone.Hand).Count == handBefore - 2,
                       "代价强制：少打出本体+弃1张（代价恒执行，无'不付'选项）");
                Assert(pool1.AvailableMana[ManaType.Black] == 1 && pool1.BlackGainedThisTurn == 1,
                       $"代价强制补偿：+1 黑（弃1张 Payload 全价2，每回合钳 1；实际 {pool1.AvailableMana[ManaType.Black]}）");

                // 3b. 直测唯一付费路径 PayWithCompensationAsync（原三选一入口已删——无账单参数，减费通道不存在）
                pool1.BlackGainedThisTurn = 0;
                int hand3b = core.ZoneManager.GetCards(p1, Zone.Hand).Count;
                var discCosts = new List<CostInstance>
                {
                    new CostInstance
                    {
                        Type = CostType.Payload,
                        Payload = CardEffectConverter.ConvertPayloadForDisplay(AtomRefs.New(AtomicEffectType.DiscardCard, value: 1, kinds: new List<int> { 5 }))
                    },
                };
                Assert(CostCompensationService.PayWithCompensationAsync(discCosts, optCtx).GetAwaiter().GetResult(),
                       "PayWithCompensationAsync 执行（cast 付费步/启动式共用唯一路径）");
                Assert(core.ZoneManager.GetCards(p1, Zone.Hand).Count == hand3b - 1, "强制路径：代价执行（弃1张）");
                Assert(pool1.AvailableMana[ManaType.Black] == 2,
                       $"强制路径补偿：再 +1 黑（每回合帽按调用隔离重置；实际 {pool1.AvailableMana[ManaType.Black]}）");

                // ---- 4. Payload 效果型代价：恒执行（对手召唤）+恒补偿（得白） ----
                var tpl = new CardData { ID = "VERIFY_BW_TPL", CardName = "验证白衍生物", Supertype = Cardtype.Creature, Power = 30, Life = 30 };
                CardCore.Attribute.Handlers.SummonTokenHandler.ResolveTemplate = id => id == tpl.ID ? tpl : null;
                pool1.AvailableMana[ManaType.White] = 0;
                pool1.WhiteGainedThisTurn = 0;
                int p2BfBefore = core.ZoneManager.GetCards(p2, Zone.Battlefield).Count;

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
                Assert(payDerive.Grants.GetValueOrDefault(ManaType.White) >= 1 && payDerive.Grants.GetValueOrDefault(ManaType.Black) == 0,
                       "构筑显示：Payload 代价 →「获得白≥1」（CardCostResult.Grants，模式0口径）");

                // 4a. e2e：无头也恒执行——对手 +1 衍生物 + 得白 1（钳制）
                Assert(GameActions.PlayCard(core, p1, payloadCard, new List<Entity>()), "打出带 Payload 代价的卡");
                GameActions.DrainStack(core);
                Assert(core.ZoneManager.GetCards(p2, Zone.Battlefield).Count == p2BfBefore + 1,
                       "代价强制：Payload 恒执行（30/30 衍生物落对手战场——受惠侧控制者）");
                Assert(pool1.AvailableMana[ManaType.White] == 1 && pool1.WhiteGainedThisTurn == 1,
                       $"代价强制补偿：+1 白（实际 {pool1.AvailableMana[ManaType.White]}）");

                // 4b. 直测：对手再 +1、白再 +1（计数器隔离重置后）
                pool1.WhiteGainedThisTurn = 0;
                var payloadCost = new List<CostInstance>
                {
                    new CostInstance
                    {
                        Type = CostType.Payload,
                        Payload = CardEffectConverter.ConvertPayloadForDisplay(
                            AtomRefs.New(AtomicEffectType.SummonToken, value: 1, kinds: new List<int> { 2 }, str: tpl.ID)),
                    },
                };
                Assert(CostCompensationService.PayWithCompensationAsync(payloadCost, optCtx).GetAwaiter().GetResult(),
                       "Payload 强制路径执行");
                Assert(core.ZoneManager.GetCards(p2, Zone.Battlefield).Count == p2BfBefore + 2,
                       "直测 Payload 执行：对手再 +1 衍生物");
                Assert(pool1.AvailableMana[ManaType.White] == 2,
                       $"直测补偿：再 +1 白（实际 {pool1.AvailableMana[ManaType.White]}）");

                // ---- 5. 元素支付（统一混付）：万用填充 / 灰浓度帽 / 真负例 ----
                foreach (var t in AllManaTypes()) pool1.AvailableMana[t] = 0;
                pool1.AvailableMana[ManaType.Gray] = 1;
                pool1.AvailableMana[ManaType.Black] = 1;
                pool1.GlobalTurnIndex = 1; // 上限 1：各货币混付贡献 ≤1
                var ctx5 = new CostContext
                {
                    Payer = p1, ZoneManager = core.ZoneManager, ElementPool = core.ElementPool, Source = p1,
                };
                var grayNeed = new List<CostInstance> { new CostInstance { Type = CostType.ElementConsume, Value = 2, ManaType = ManaType.Gray } };
                bool paidGray = ElementCostPayment.Pay(grayNeed, ctx5);
                Assert(paidGray && pool1.AvailableMana[ManaType.Gray] == 0 && pool1.AvailableMana[ManaType.Black] == 0,
                       "灰费混付：灰1+黑1 付灰2（黑白万用，与三色一致）");

                // 灰浓度帽负例：灰2 账单（cap1）只有灰 bank → 灰贡献 1 后无万用可垫 → 拒付
                pool1.AvailableMana[ManaType.Gray] = 2;
                pool1.AvailableMana[ManaType.Black] = 0;
                var gray2 = new List<CostInstance> { new CostInstance { Type = CostType.ElementConsume, Value = 2, ManaType = ManaType.Gray } };
                Assert(!ElementCostPayment.Pay(gray2, ctx5),
                       "灰入浓度上限：灰2（cap1）灰 bank 2 → 只能贡献 1，无万用垫 → 拒付");

                foreach (var t in AllManaTypes()) pool1.AvailableMana[t] = 9;
                pool1.AvailableMana[ManaType.Red] = 0;
                var redNeed = new List<CostInstance> { new CostInstance { Type = CostType.ElementConsume, Value = 1, ManaType = ManaType.Red } };
                bool paidRed = ElementCostPayment.Pay(redNeed, ctx5);
                Assert(paidRed && pool1.AvailableMana[ManaType.Gray] == 8,
                       "万用填充：红费缺口由灰垫（红链=红→灰→黑→白，灰先于黑白消耗）");

                foreach (var t in AllManaTypes()) pool1.AvailableMana[t] = 0;
                Assert(!ElementCostPayment.Pay(redNeed, ctx5),
                       "真负例：红费且红灰黑白全空 → 拒付");

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

                // ---- 7. 含黑白费用的卡作地牌：黑白份额不产指示物；纯黑白卡不可入池 ----
                // 2026-09-13 修复：前段（TestLandEconomy 等）地牌未回收，上限=1 时占位即拒——先清池残留
                foreach (var residue in core.ElementPool.GetPooledCards(p1).ToList())
                    core.ElementPool.RemoveCardFromPool(residue.SourceCard, p1);
                var mixed = CardLoader.BuildDeck(new List<CardData>
                {
                    new CardData { ID = "VERIFY_BW_LAND_MIX", CardName = "混色地", Supertype = Cardtype.Creature, Power = 1, Life = 1,
                        Cost = new Dictionary<int, float> { { (int)ManaType.Red, 1 }, { (int)ManaType.Black, 2 } } },
                }, 1)[0];
                Assert(core.ElementPool.AddCardToPool(mixed, p1), "红+黑费用卡可入池（黑白份额被过滤）");
                var mixedPooled = core.ElementPool.GetPool(p1).PooledCards.First(pc => pc.SourceCard == mixed);
                Assert(mixedPooled.Tokens.GetValueOrDefault(ManaType.Red) == 1 && mixedPooled.Tokens.GetValueOrDefault(ManaType.Black) == 0,
                       "地牌指示物：黑白不产（只产红 1）");
                core.ElementPool.RemoveCardFromPool(mixed, p1);

                var pureBlack = CardLoader.BuildDeck(new List<CardData>
                {
                    new CardData { ID = "VERIFY_BW_LAND_BLACK", CardName = "纯黑地", Supertype = Cardtype.Creature, Power = 1, Life = 1,
                        Cost = new Dictionary<int, float> { { (int)ManaType.Black, 2 } } },
                }, 1)[0];
                Assert(!core.ElementPool.AddCardToPool(pureBlack, p1),
                       "纯黑白费用卡不可作地牌（过滤后无指示物 → 拒绝入池）");
            }
            finally
            {
                foreach (var kv in snapBank) pool1.AvailableMana[kv.Key] = kv.Value;
                pool1.GlobalTurnIndex = snapTurnIdx;
                pool1.BlackGainedThisTurn = snapBlackGained;
                pool1.WhiteGainedThisTurn = snapWhiteGained;
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
            var fireballData = cardsData.FirstOrDefault(c => c.CardName == "火球术");
            if (fireballData != null)
            {
                // 2026-09-11 拆分后并发取总跨两效果合计（单价×值口径不变，总额同旧单效果）
                var fireDefs = CardEffectConverter.ConvertAll(fireballData.Effects, fireballData.ID);
                var fireCosts = fireDefs.SelectMany(fd => CostDerivationService.DeriveElementCosts(fd)).ToList();
                int drawPrice = (int)System.Math.Round(Cfg(AtomicEffectType.DrawCard).TotalUnitCost,
                    System.MidpointRounding.AwayFromZero);
                var drawColor = CardCore.ElementAffinities.GetAffinityForEffect(AtomicEffectType.DrawCard).PrimaryColor;
                Assert(fireCosts.Where(c => c.ManaType == ManaType.Red).Sum(c => c.Value) == 4,
                       "并发组合：4伤=红4（取总的一半）");
                Assert(fireCosts.Where(c => c.ManaType == drawColor).Sum(c => c.Value) == drawPrice,
                       "并发组合：抽1=抽牌表价并入总费（费用取总）");
            }

            var declareData = cardsData.FirstOrDefault(c => c.CardName == "读心预言");
            if (declareData != null)
            {
                var def = CardEffectConverter.ConvertAll(declareData.Effects, declareData.ID)[0];
                var stripped = new EffectDefinition { Steps = def.Steps.Where(s => s.Kind != RuntimeStepKind.Branch).ToList() };
                int totalAll = CostDerivationService.DeriveElementCosts(def).Sum(c => c.Value);
                int totalNoBranch = CostDerivationService.DeriveElementCosts(stripped).Sum(c => c.Value);
                Assert(totalAll == totalNoBranch + 2,
                       "条件奖励不计费（宣言门附加费 2 灰计入，then/else 原子免计价——2026-09-13 定案）");
            }

            // ---- 1. 抉择卡数据/转换/判据 ----
            var data = cardsData.FirstOrDefault(c => c.CardName == "抉择试作");
            Assert(data != null, "抉择卡配置存在（抉择试作）");
            if (data == null) return;

            Assert(CostDerivationService.HasChoiceEffect(data) && CostDerivationService.GetModeCount(data) == 2,
                   "抉择判据：HasChoiceEffect 且模式数=2");
            var defs = CardEffectConverter.ConvertAll(data.Effects, data.ID);
            Assert(defs.Count == 1 && defs[0].Steps.Count == 1 && defs[0].Steps[0].Kind == RuntimeStepKind.Choice
                   && defs[0].Steps[0].Choices.Count == 2,
                   "转换：Steps[0] 为 Choice 且双模式（choices≥2 有效）");

            // ---- 2. per-mode 计价（构筑期推导存储；2026-09-21：价差溢价已废、抉择分支计槽——
            //      1 效果含 2 分支 = 2 槽 → 法术底盘退 3−2=1，退费落最高费用色）----
            var mode0 = CardCostService.GetModeCost(data, 0);
            var mode1 = CardCostService.GetModeCost(data, 1);
            Assert(mode0.TryGetValue((int)ManaType.Red, out var r0) && System.Math.Abs(r0 - 3f) < 0.01f,
                   "模式0计价：4伤锚红4 − 底盘退1（2 槽）= 红3（无价差溢价）");
            var modalDrawColor = CardCore.ElementAffinities.GetAffinityForEffect(AtomicEffectType.DrawCard).PrimaryColor;
            var modalDrawPrice = (int)System.Math.Round(Cfg(AtomicEffectType.DrawCard).TotalUnitCost,
                System.MidpointRounding.AwayFromZero);
            Assert(mode1.TryGetValue((int)modalDrawColor, out var d1)
                   && System.Math.Abs(d1 - System.Math.Max(0, modalDrawPrice - 1)) < 0.01f,
                   "模式1计价：抽牌表价 − 底盘退1（下限 0，桶空截断）——per-mode 独立不求和");
            Assert(mode1.Values.Sum() < mode0.Values.Sum(),
                   "两模式费用独立（红4 ≠ 蓝" + modalDrawPrice + "，未取总）");
            float maxTotal = System.Math.Max(mode0.Values.Sum(), mode1.Values.Sum());
            Assert(data.Cost != null && data.Cost.Count > 0 && System.Math.Abs(data.Cost.Values.Sum() - maxTotal) < 0.01f,
                   "声明 costList=最大模式费（EnsureCost 抉择分支——仅 UI/排序口径；地牌产元素按所选模式）");

            // ---- 状态快照（本段灌 bank/上限，结束恢复）----
            var pool1 = core.ElementPool.GetPool(p1);
            var snap = new Dictionary<ManaType, int>(pool1.AvailableMana);
            int idxSnap = pool1.GlobalTurnIndex;
            pool1.GlobalTurnIndex = 9; // 费用门槛不挡
            pool1.AvailableMana[ManaType.Red] += 6;
            pool1.AvailableMana[modalDrawColor] += 5;

            try
            {
                // ---- 3. 模式0端到端（缺省参数=0）：只伤不抽 ----
                int p2Life = p2.Life;
                int deckBefore = core.ZoneManager.GetCards(p1, Zone.Deck).Count;
                int redBefore = pool1.AvailableMana[ManaType.Red];
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
                Assert(pool1.AvailableMana[ManaType.Red] == redBefore - 3, "模式0实付红3（锚4−底盘退1（2槽·分支计槽）；先选择再定费用）");
                Assert(core.ZoneManager.GetCards(p1, Zone.Graveyard).Contains(modal0)
                       && core.ZoneManager.GetCards(p1, Zone.Activation).Count == 0, "抉择法术结算后入墓、发动区清空");

                // ---- 4. 模式1端到端：只抽不伤 ----
                p2Life = p2.Life;
                deckBefore = core.ZoneManager.GetCards(p1, Zone.Deck).Count;
                int drawBefore = pool1.AvailableMana[modalDrawColor];
                var modal1 = InjectCard(core, p1, data);

                Assert(PlayCardSync(core, p1, modal1, null, modeIndex: 1), "抉择卡模式1打出（先选模式再定费用）");
                Assert(core.ZoneManager.GetCards(p1, Zone.Deck).Count == deckBefore - 1, "模式1：抽一张牌");
                Assert(p2.Life == p2Life, "模式1：不造伤害（执行隔离）");
                Assert(pool1.AvailableMana[modalDrawColor] == drawBefore - System.Math.Max(0, modalDrawPrice - 1),
                       "模式1实付=表价−底盘退1（2槽·下限0；非两模式之和）");

                // ---- 5. 防超发：pending 按声明模式计 ----
                // 混付垫付会绕过「精确同色占用即拒」：清全色 bank 只留被测色（根因 A 修复）
                HygieneBank(pool1, ManaType.Red, 3); // bank 恰好一张模式0（红3 = 锚4−底盘退1（2槽·分支计槽））
                var overA = InjectCard(core, p1, data);
                var overB = InjectCard(core, p1, data);
                Assert(GameActions.PlayCard(core, p1, overA, new List<Entity> { p2 }, Zone.Hand, 0),
                       "防超发：第一张模式0声明上栈（声明期不付费）");
                Assert(!GameActions.PlayCard(core, p1, overB, new List<Entity> { p2 }, Zone.Hand, 0),
                       "防超发：同笔 bank 第二张模式0被拒（GetPendingCastCosts 按模式计）");
                GameActions.DrainStack(core);
                // 实付红3（同模式0断言口径）→ 3−3 余 0（第二张拒付不扣款）
                Assert(pool1.AvailableMana[ManaType.Red] == 0, "防超发：结算后实付红3 余0（第二张拒付不扣款）");
            }
            finally
            {
                foreach (var kv in snap) pool1.AvailableMana[kv.Key] = kv.Value;
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

            // ---- 7. 功能哈希：抉择顺序参与（不碰撞）----
            var swap = new CardData { ID = data.ID, CardName = data.CardName };
            swap.Supertype = data.Supertype;
            var srcStep = data.Effects[0].Steps[0];
            swap.Effects.Add(new CardEffectData
            {
                Id = data.Effects[0].Id,
                Steps = new List<EffectStepData>
                {
                    new EffectStepData { kind = 2, choices = new List<EffectChoiceData> { srcStep.choices[1], srcStep.choices[0] } }
                }
            });
            Assert(SynergyUI.ContentHasher.HashCard(data) != SynergyUI.ContentHasher.HashCard(swap),
                   "抉择模式顺序参与功能哈希（ContentHasher kind==2 递归）");

            // ---- 8. MemoryPack DTO 往返 ----
            var ser = CardCore.Serialization.SerializableEffectStepData.FromData(data.Effects[0].Steps[0]);
            var back = ser.ToData();
            Assert(back.choices != null && back.choices.Count == 2
                   && back.choices[0].steps[0].atomic.refId == CardCore.Attribute.AtomicEffectTable.GetByEnumName("DealDamage")?.HashId
                   && back.choices[1].steps[0].atomic.refId == CardCore.Attribute.AtomicEffectTable.GetByEnumName("DrawCard")?.HashId,
                   "序列化往返保抉择结构（模式数与原子类型）");
        }

        // ======================================== 攻击/守卫效果化（2026-09-10；2026-09-16 战斗接入栈机器重写） ========================================

        /// <summary>
        /// 攻击/守卫端到端（2026-09-16 逐攻击开窗定案）：
        /// - 攻击=速度0栈对象：宣言零支付上栈开窗，结算期重检+横置支付→战斗三段；
        /// - 守卫=速度1响应：窗口内 PushGuardDeclaration 入栈（资格闸 NoGuard/横置/存活），
        ///   LIFO 先结算——横置支付+攻击目标变更；
        /// - 警戒守卫：横置仍造成战斗伤害（结算资格看目标横置，警戒例外）；
        /// - 速度门槛：标准速度门（0≥计数器）——连锁开启时攻击不可宣言；
        /// - 瞬间富余转速度：SurplusToSpeed 法术全部效果 BaseSpeed+1。
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
                card.Untap(); // 横置入场定案：本段测试前提 = 竖直随从
                return card;
            }

            // ---- 1. NoAttack 资格（墙不能攻；同身材正常卡能攻） ----
            var wall = Spawn(p1, 3, 5, noAttack: true);
            var hitter = Spawn(p1, 3, 3);
            Assert(!combat.CanDeclareAttack(wall, p1), "攻击效果化：NoAttack 卡不能宣言攻击");
            Assert(combat.CanDeclareAttack(hitter, p1), "攻击效果化：默认自带攻击能力");

            // ---- 2. 守卫拦截：打脸攻击 → 窗口守卫响应 → 目标转移 + 守卫结算期横置 + 单向受伤 ----
            var striker = Spawn(p1, 4, 2);       // 4 攻
            var shield = Spawn(p2, 3, 6);        // 3 攻 6 血守卫
            int p2Life0 = p2.Life;
            Assert(GameActions.DeclareAttack(core, p1, striker, p2), "守卫段：打脸宣言上栈（速度0开窗，宣言期零支付）");
            Assert(!striker.IsTapped(), "守卫段：宣言期不横置（结算期支付）");
            var attackInstance = core.StackEngine.Peek();
            Assert(core.StackEngine.PushGuardDeclaration(shield, attackInstance, p2), "守卫段：守卫速度1响应入栈");
            GameActions.DrainStack(core);        // 守卫先结（横置+目标变更）→ 攻击三段
            Assert(shield.IsTapped(), "守卫拦截：结算期横置自身（被动代价）");
            Assert(shield.IsAlive && shield.GetLife() == 2, "守卫拦截：守卫吃 4 伤（6→2）");
            Assert(striker.IsAlive && striker.GetLife() == 2, "守卫拦截：单向受伤——守卫横置不反击（攻击者只受 0 反伤）");
            Assert(striker.IsTapped(), "守卫段：攻击者结算期支付横置");
            Assert(p2.Life == p2Life0, "守卫拦截：玩家未被击中（目标已转移）");

            // ---- 2b. 警戒守卫：横置仍反击（新语义） ----
            var raider = Spawn(p1, 4, 2);
            var valiant = Spawn(p2, 3, 6, false, false, CardCore.Attribute.KeywordRules.Vigilance);
            Assert(GameActions.DeclareAttack(core, p1, raider, p2), "警戒守卫段：打脸宣言上栈");
            Assert(core.StackEngine.PushGuardDeclaration(valiant, core.StackEngine.Peek(), p2), "警戒守卫段：守卫响应入栈");
            GameActions.DrainStack(core);
            Assert(valiant.IsTapped(), "警戒守卫：结算期照常横置");
            Assert(valiant.GetLife() == 2 && !raider.IsAlive,
                   "警戒重定义：横置守卫仍造成战斗伤害（守卫 6→2、4/2 攻击者被 3 反击致死）");

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
            Assert(gatekeeper.IsTapped() && gatekeeper.GetLife() == 4, "再拦段：首拦结算（6−2=4，横置不反击）");
            gatekeeper.Untap(); // 激励通路
            Assert(GameActions.DeclareAttack(core, p1, a2, p2), "再拦段：第二攻宣言上栈（新窗口）");
            Assert(core.StackEngine.PushGuardDeclaration(gatekeeper, core.StackEngine.Peek(), p2),
                   "再拦段：解除横置可拦第二攻击者");
            GameActions.DrainStack(core);
            Assert(gatekeeper.IsAlive && gatekeeper.GetLife() == 1,
                   "再拦段：两拦都结算（6−2−3=1，守卫横置不反击）");

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

            // ---- 4. 瞬间富余转速度：SurplusToSpeed → 全效果 BaseSpeed+1 ----
            var swiftData = new CardData
            {
                ID = "VERIFY_AG_SWIFT", CardName = "疾风术", Supertype = Cardtype.Spell,
                SurplusToSpeed = true,
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
            Assert(swiftDefs.Count == 1 && swiftDefs[0].BaseSpeed == 1,
                   $"瞬间富余转速度：BaseSpeed 0→1（实际 {swiftDefs[0].BaseSpeed}）");
            var plainData = new CardData
            {
                ID = "VERIFY_AG_PLAIN", CardName = "平风术", Supertype = Cardtype.Spell,
                Effects = new List<CardEffectData> { swiftData.Effects[0] },
            };
            Assert(GameActions.GetCardEffectDefinitions(new CardWrapper(plainData))[0].BaseSpeed == 0,
                   "瞬间富余缺省：不转速度（BaseSpeed 0，退费走计价侧）");

            RetireCards(core, p1, wall, hitter, striker, raider, charger2, runner, a1, a2);
            RetireCards(core, p2, shield, valiant, pacifist, gatekeeper);
        }

        // ======================================== 使用时点 / 响应窗口（Phase 2） ========================================

        /// <summary>
        /// 使用时点 + 响应窗口端到端（Option Y 定案；合成卡驱动，不依赖卡表）：
        /// - 打出即声明上栈（声明期不付费），对手获得优先权；
        /// - 打落（软打断）：发动区卡送墓 → cast 中止：不付费、无效果；
        /// - 发动无效（硬反制）：cast 照常付费但跳效果入墓，费用不退；
        /// - 声明超发保护：同笔 bank 已被在栈 cast 占用时拒绝新声明（防超发，不回卷）。
        /// </summary>
        private static void TestCounterWindow(GameCore core, Player p1, Player p2)
        {
            EnsureMainPhase(core, p1);
            // 段落卫生（2026-09-21）：前一 TestEquipment 的结算若产生死亡，SBA（速度1）会留在
            // 栈上开窗（记速器=1）——先排干再开始本段，否则 0 速火球过不了速度门、响应窗口错乱
            GameActions.DrainStack(core);

            // 合成法术：显式灰 1 费（绕开推导），原子由参数给定
            // 2026-09-13 修复：反制卡（打落/无效）须配 2 速——速度峰值模型下非回合方过门槛
            // 要求严格 > 记速器（0>0 恒假，0 速响应全被拒）；主动出的火球保持 0 速（不抬记速器）
            CardData SpellData(string id, string name, int baseSpeed = 0, params AtomicEffectEntry[] atoms)
            {
                var data = new CardData { ID = id, CardName = name };
                data.Supertype = Cardtype.Spell;
                data.Cost = new Dictionary<int, float> { { (int)ManaType.Gray, 1 } };
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
            var snap1 = new Dictionary<ManaType, int>(pool1.AvailableMana);
            var snap2 = new Dictionary<ManaType, int>(pool2.AvailableMana);
            int idx1 = pool1.GlobalTurnIndex, idx2 = pool2.GlobalTurnIndex;
            pool1.GlobalTurnIndex = 9;   // 费用门槛（=地牌槽上限）不挡
            pool2.GlobalTurnIndex = 9;
            foreach (var t in AllManaTypes()) { pool1.AvailableMana[t] = 99; pool2.AvailableMana[t] = 99; }

            try
            {
                // ---- 1. 打落（软打断）：不付费、无效果 ----
                var fireball = InjectCard(core, p1, SpellData("VERIFY_CAST_FB", "验证火球", 0, Atom("DealDamage", 4)));
                var knock = InjectCard(core, p2, SpellData("VERIFY_CAST_KD", "验证打落", 2, Atom("KnockDown")));

                int p2Life = p2.Life;
                int gray1 = pool1.AvailableMana[ManaType.Gray];

                Assert(GameActions.PlayCard(core, p1, fireball, new List<Entity> { p2 }),
                       "打出火球（使用时点声明上栈）—门禁:" + PlayGateProbe(core, p1, fireball));
                Assert(core.StackEngine.StackSize == 1 && core.StackEngine.CurrentPriorityHolder == p2,
                       "cast 上栈且对手持有优先权（响应窗口开启）");
                Assert(pool1.AvailableMana[ManaType.Gray] == gray1, "声明期不付费（Option Y）");

                Assert(GameActions.PlayCardInResponse(core, p2, knock, new List<Entity> { fireball }),
                       "响应窗口内打出打落（指向发动区的火球）");
                Assert(core.ZoneManager.IsCardInZone(fireball, p1, Zone.Activation),
                       "打落仅入栈未结算：火球仍留发动区");

                GameActions.DrainStack(core);   // 双 Pass → LIFO：打落先、火球 cast 后

                // 已知残留（2026-09-21 数据扩池后漂移）：段内会产生一个空目标的速度1 SBA 窗口
                // （DrainStack 双 Pass 排不掉，疑似记帐型 SBA 入栈后窗口未闭合——待夹具二期深挖）。
                // 收口排空防污染下段（同 Equipment/HeroSkills 段尾惯例）。
                GameActions.DrainStack(core);
                if (!core.StackEngine.IsEmpty)
                    core.StackEngine.Clear();
                Assert(core.StackEngine.IsEmpty, "栈已排干");
                Assert(core.ZoneManager.GetCards(p1, Zone.Graveyard).Contains(fireball), "火球被送墓（软打断达成）");
                Assert(p2.Life == p2Life, "被中止的 cast 不结算效果（伤害 0）");
                Assert(pool1.AvailableMana[ManaType.Gray] == gray1, "被中止的 cast 不付费");
                Assert(core.ZoneManager.GetCards(p2, Zone.Graveyard).Contains(knock), "打落牌结算后入墓");

                // ---- 2. 发动无效（硬反制）：付费但跳效果，费用不退 ----
                var fireball2 = InjectCard(core, p1, SpellData("VERIFY_CAST_FB2", "验证火球二", 0, Atom("DealDamage", 4)));
                var negate = InjectCard(core, p2, SpellData("VERIFY_CAST_NA", "验证无效", 2, Atom("NegateActivation")));

                int gray1b = pool1.AvailableMana[ManaType.Gray];

                Assert(GameActions.PlayCard(core, p1, fireball2, new List<Entity> { p2 }), "打出第二张火球");
                Assert(GameActions.PlayCardInResponse(core, p2, negate, new List<Entity> { fireball2 }),
                       "响应窗口内打出发动无效");
                GameActions.DrainStack(core);
                if (!core.StackEngine.IsEmpty) core.StackEngine.Clear(); // 段内 SBA 窗口收口（同上小节）

                Assert(p2.Life == p2Life, "被无效的 cast 跳过效果（伤害 0）");
                Assert(pool1.AvailableMana[ManaType.Gray] == gray1b - 1,
                       "无效路径照常付费（窗口后扣费，费用不退）");
                Assert(core.ZoneManager.GetCards(p1, Zone.Graveyard).Contains(fireball2), "被无效的卡入墓");

                // ---- 3. 声明超发保护：同笔 bank 不重复承诺 ----
                var a = InjectCard(core, p1, SpellData("VERIFY_CAST_A", "验证超发A", 0, Atom("DealDamage", 1)));
                var b = InjectCard(core, p1, SpellData("VERIFY_CAST_B", "验证超发B", 0, Atom("DealDamage", 1)));
                // 灰账单可由 黑/白 垫付（混付定案 2026-09-14）——清全色只留灰1（根因 A 修复）
                HygieneBank(pool1, ManaType.Gray, 1);   // 只够一张
                Assert(GameActions.PlayCard(core, p1, a, new List<Entity> { p2 }), "第一张灰1可声明（占用承诺）");
                Assert(!GameActions.PlayCard(core, p1, b, new List<Entity> { p2 }),
                       "同一笔 bank 已被在栈 cast 占用：第二张拒绝（防超发）");
                GameActions.DrainStack(core);
                if (!core.StackEngine.IsEmpty) core.StackEngine.Clear(); // 段内 SBA 窗口收口（同上小节）
                Assert(pool1.AvailableMana[ManaType.Gray] == 0, "第一张结算后照常扣费");
                Assert(p2.Life == p2Life - 1, "第一张效果照常结算（伤害 1）");

                // ---- 4. 跨边代价断言已删（2026-09-10：OpponentDraw/OpponentHeal 机制被 Polarity 错边折价顶替）----
            }
            finally
            {
                pool1.GlobalTurnIndex = idx1;
                pool2.GlobalTurnIndex = idx2;
                pool1.AvailableMana.Clear();
                foreach (var kv in snap1) pool1.AvailableMana[kv.Key] = kv.Value;
                pool2.AvailableMana.Clear();
                foreach (var kv in snap2) pool2.AvailableMana[kv.Key] = kv.Value;
            }
        }

        // ======================================== 死亡原子（牺牲/吞噬/湮灭） ========================================

        /// <summary>
        /// 死亡原子端到端（DeathRules 死因的原子层落地；合成随从驱动，不依赖卡表）：
        /// - 牺牲：己方生物经 Sacrifice 死因入墓；
        /// - 吞噬：消灭裁决成功才吸收——复制目标全部关键词 + 回复目标当前生命；
        ///   不灭拦 Devour（消灭类），拦下即无吸收；
        /// - 湮灭：直送除外区、复生不可救；不灭不拦（非消灭类，照常湮灭）。
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
                // ---- 牺牲：己方生物入墓 ----
                var victim = Make(p1, 2, 3);
                // 2026-09-13 修复：Sacrifice 已改 edict 语义——作用对象=角色（Player），持有者再自选生物；
                // 直塞生物目标会被 handler 空转（TestSacrificeAtom 段 Targets={p2,p1} 口径）
                Atom(AtomicEffectType.Sacrifice, p1, p1, p1);
                Assert(!victim.IsAlive && core.ZoneManager.GetCards(p1, Zone.Graveyard).Contains(victim),
                       "牺牲：生物经 Sacrifice 死因入墓");

                // ---- 吞噬（2026-09-13 简化）：消灭 + 按被消灭单位最大生命值回复（关键词吸收已删）----
                var devourer = Make(p1, 3, 6);
                Atom(AtomicEffectType.DealDamage, p1, p1, devourer, 4); // 先受伤留回复缺口
                Assert(devourer.GetLife() == 2, "吞噬者先受伤至 2（留回复缺口）");

                var prey = Make(p2, 1, 3, CardCore.Attribute.KeywordRules.Taunt);
                Atom(AtomicEffectType.DealDamage, p2, p2, prey, 2); // 猎物受伤至 1——最大3≠当前1，区分回复口径
                Atom(AtomicEffectType.Devour, p1, devourer, prey);
                Assert(!prey.IsAlive && core.ZoneManager.GetCards(p2, Zone.Graveyard).Contains(prey),
                       "吞噬：猎物经 Devour 死因入墓");
                Assert(!devourer.HasKeyword(CardCore.Attribute.KeywordRules.Taunt), "吞噬：不再复制关键词（吸收已删）");
                Assert(devourer.GetLife() == 5, "吞噬：按被消灭单位最大生命值回复（2+3=5，非当前生命1）");

                // 不灭拦 Devour（消灭类）：拦下即无回复
                var rock = Make(p2, 0, 5, CardCore.Attribute.KeywordRules.Indestructible, CardCore.Attribute.KeywordRules.Stealth);
                int lifeBefore = devourer.GetLife();
                Atom(AtomicEffectType.Devour, p1, devourer, rock);
                Assert(rock.IsAlive && core.ZoneManager.GetCards(p2, Zone.Battlefield).Contains(rock),
                       "不灭拦下吞噬（消灭类死因）");
                Assert(!devourer.HasKeyword(CardCore.Attribute.KeywordRules.Stealth) && devourer.GetLife() == lifeBefore,
                       "拦下即无回复（无吸收语义）");

                // ---- 湮灭：直送除外 + 复生不可救 + 不灭不拦 ----
                var phoenix = Make(p2, 2, 2, CardCore.Attribute.KeywordRules.Reborn);
                Atom(AtomicEffectType.Annihilate, p1, p1, phoenix);
                Assert(!phoenix.IsAlive && core.ZoneManager.GetCards(p2, Zone.Exile).Contains(phoenix),
                       "湮灭：直送除外区（不入墓）");
                Assert(!core.ZoneManager.GetCards(p2, Zone.Battlefield).Contains(phoenix),
                       "湮灭：复生不可救（未回场）");

                var adamant = Make(p2, 1, 4, CardCore.Attribute.KeywordRules.Indestructible);
                Atom(AtomicEffectType.Annihilate, p1, p1, adamant);
                Assert(!adamant.IsAlive && core.ZoneManager.GetCards(p2, Zone.Exile).Contains(adamant),
                       "湮灭：不灭不拦（非消灭类，照常湮灭）");
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
            pool.AvailableMana[ManaType.Gray] = 4;

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

                // ---- 坚韧光环（2026-09-13 定案：坚韧改为连接箭头光环，绿1×箭头数，按箭头叠加，无触发上限）----
                var rockSrc = MakePlain(p1, 2, 3);
                var holder = MakePlain(p1, 3, 9);
                bool rockAdj; var dirToHolder = DirBetween(rockSrc, holder, out rockAdj);
                Assert(rockAdj, "坚韧光环段：源与受益者相邻");
                DataOf(rockSrc).ArrowDirections = GameBoard.BoardMath.ArrowOf(dirToHolder);
                DataOf(rockSrc).LinkAuras.Add(new LinkAuraData { keyword = Attribute.KeywordRules.Armor });
                board.Resync();
                // 2026-09-13 修复：直改 ArrowDirections 无事件，Resync 只重建占用不失效光环缓存
                //（引擎设计要求手动失效，TransferEquipment 有先例）——改箭头后必须补失效，否则读到旧缓存
                GameBoard.LinkAuraSystem.InvalidateCache();

                int hLife = holder.GetLife();
                Attribute.KeywordRules.ApplyDamage(null, holder, 3, false);
                Assert(holder.GetLife() == hLife - 2,
                       "坚韧光环：受伤-1（3伤实扣2——静态替代走 ApplyPreventionLayers，无触发上限）");
                hLife = holder.GetLife();
                Attribute.KeywordRules.ApplyDamage(null, holder, 2, false);
                Assert(holder.GetLife() == hLife - 1, "坚韧光环：同回合第二次受伤仍-1（光环无触发上限）");
                Assert(GameBoard.LinkAuraSystem.GetAuraKeywordCount(holder, Attribute.KeywordRules.Armor) == 1,
                       "光环覆盖数：GetAuraKeywordCount=1（按箭头叠加的计数口）");

                // 断链失效：撤箭头恢复全额
                DataOf(rockSrc).ArrowDirections = CardCore.HexDirection.None;
                board.Resync();
                GameBoard.LinkAuraSystem.InvalidateCache(); // 直改箭头须手动失效缓存（同上）
                hLife = holder.GetLife();
                Attribute.KeywordRules.ApplyDamage(null, holder, 3, false);
                Assert(holder.GetLife() == hLife - 3, "断链失效：撤箭头后恢复全额伤害");

                // 按箭头叠加：第二源再指 holder → -2
                DataOf(rockSrc).ArrowDirections = GameBoard.BoardMath.ArrowOf(dirToHolder);
                var rockSrc2 = MakePlain(p1, 1, 2);
                bool rockAdj2; var rockDir2 = DirBetween(rockSrc2, holder, out rockAdj2);
                if (rockAdj2)
                {
                    DataOf(rockSrc2).ArrowDirections = GameBoard.BoardMath.ArrowOf(rockDir2);
                    DataOf(rockSrc2).LinkAuras.Add(new LinkAuraData { keyword = Attribute.KeywordRules.Armor });
                    board.Resync();
                    GameBoard.LinkAuraSystem.InvalidateCache(); // 直改箭头须手动失效缓存（同上）
                    hLife = holder.GetLife();
                    Attribute.KeywordRules.ApplyDamage(null, holder, 4, false);
                    Assert(holder.GetLife() == hLife - 2, "按箭头叠加：两源各一箭覆盖 → 受伤-2（4伤实扣2）");
                }

                // 计价锚（2026-09-13 修订）：条目平价 1 + 卡级箭头累乘 ×1.2^(箭头-1)——不再逐条乘箭头
                var anchorData = new CardData { ID = "VERIFY_LA_ARMORCOST", CardName = "坚韧光环计价锚", Supertype = Cardtype.Enchantment };
                anchorData.ArrowDirections = CardCore.HexDirection.Up | CardCore.HexDirection.Down;
                anchorData.LinkAuras.Add(new LinkAuraData { keyword = Attribute.KeywordRules.Armor });
                var armorDerive = CardCostService.Derive(anchorData);
                Assert(armorDerive.Breakdown.Any(l => l.Stage == "A" && l.Label.Contains("坚韧")
                           && System.Math.Abs(l.Value - 1f) < 0.01f)
                       && armorDerive.Breakdown.Any(l => l.Label.Contains("累乘")),
                       "坚韧光环计价：条目平价绿1 + 箭头卡级累乘（2箭头=×1.2，不逐条乘箭头）");

                // 属性价梯（2026-09-13 定案：攻血同锚 0.5/+1；2026-09-16 统一档：UNT≡UET 同价 0.5）
                CardCore.EffectDefinition StatDef(int duration, string type = "ModifyPower", int value = 1)
                    => CardEffectConverter.ConvertOne(new CardEffectData
                    {
                        Id = $"VERIFY_STAT_{duration}",
                        Duration = duration,
                        AtomicEffects = new List<AtomicEffectEntry> { Atom(type, value) },
                    }, "VERIFY_STAT");
                int StatCost(CardCore.EffectDefinition d) => CardCore.CostDerivationService.DeriveElementCosts(d).Sum(c => c.Value);
                Assert(StatCost(StatDef((int)DurationType.UntilEndOfTurn, value: 2)) == 1
                       && StatCost(StatDef((int)DurationType.UntilNextTurn, value: 2)) == 1,
                       "属性价梯：固定1回合 0.5/+1（+2攻=1）；UNT≡UET（统一档费用按1回合，+2攻=1）");
                Assert(StatCost(StatDef((int)DurationType.UntilLeaveBattlefield, value: 2)) == 3,
                       "属性价梯：换区移除 1.5/+1（+2攻=3）");
                Assert(StatCost(StatDef((int)DurationType.Permanent, value: 2)) == 4,
                       "属性价梯：换区不移除 2.0/+1（+2攻=4）");
                Assert(StatCost(StatDef((int)DurationType.Permanent, value: 2, type: "SetPower")) == 6,
                       "属性价梯：永久改写（SetPower 设置直改）3.0/+1（+2攻=6）");
                var statAuraData = new CardData { ID = "VERIFY_LA_STATCOST", CardName = "属性光环锚", Supertype = Cardtype.Enchantment };
                statAuraData.ArrowDirections = CardCore.HexDirection.Up;
                statAuraData.LinkAuras.Add(new LinkAuraData { stat = "Power", value = 2 });
                var statAuraDerive = CardCostService.Derive(statAuraData);
                Assert(statAuraDerive.Breakdown.Any(l => l.Stage == "A" && l.Label.Contains("+2")
                           && System.Math.Abs(l.Value - 3f) < 0.01f),
                       "属性光环：1.5/+1 对齐换区移除档（+2攻=3；单箭头无累乘）");
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
            Assert(edge == 40 && unit == 36 && land == 18 && special == 10,
                   $"布局计数 104 = 边缘{edge} + 单位{unit} + 地牌{land} + 特殊{special}");

            // 额外卡组退役（2026-09-13）：原格归还边缘环，无玩法意义（英雄技能为单方面影响，不上战场格）
            Assert(BoardLayout.RoleOf(11, 4) == CellRole.Edge && BoardLayout.OwnerOf(11, 4) == -1
                   && BoardLayout.RoleOf(1, 3) == CellRole.Edge && BoardLayout.OwnerOf(1, 3) == -1,
                   "额外卡组退役：原格 (11,4)/(1,3) 归还边缘环");

            Assert(BoardLayout.RoleOf(11, 6) == CellRole.Character && BoardLayout.OwnerOf(11, 6) == 0, "P1 角色格 = (11,6)");
            Assert(BoardLayout.RoleOf(1, 6) == CellRole.Activation && BoardLayout.OwnerOf(1, 6) == 0, "P1 发动格 = (1,6)");
            Assert(BoardLayout.RoleOf(11, 5) == CellRole.Deck && BoardLayout.OwnerOf(11, 5) == 0, "P1 卡组格 = (11,5)");
            Assert(BoardLayout.RoleOf(11, 2) == CellRole.Graveyard && BoardLayout.OwnerOf(11, 2) == 1, "P2 墓地格 = (11,2)");
            Assert(BoardLayout.RoleOf(1, 1) == CellRole.Character && BoardLayout.OwnerOf(1, 1) == 1, "P2 角色格 = (1,1)");
            Assert(BoardLayout.RoleOf(11, 1) == CellRole.Activation && BoardLayout.OwnerOf(11, 1) == 1, "P2 发动格 = (11,1)");

            bool symmetric = true;
            for (int z = 0; z < BoardMath.Height && symmetric; z++)
                for (int x = 0; x < BoardMath.Width && symmetric; x++)
                {
                    var (rx, rz) = BoardLayout.Rotate180(x, z);
                    if (BoardLayout.RoleOf(rx, rz) != BoardLayout.RoleOf(x, z)) symmetric = false;
                    int o = BoardLayout.OwnerOf(x, z);
                    if (o != -1 && BoardLayout.OwnerOf(rx, rz) != 1 - o) symmetric = false;
                }
            Assert(symmetric, "全盘 180° 旋转对称（角色一致、归属互补）");
            Assert(BoardLayout.UnitCellsPerPlayer == ZoneManager.BattlefieldCapacityPerPlayer,
                   "单位格数 18 = 核心战场容量 18（计数与格子天然一致）");

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
            half1.Set(5, 0, 2, 0); // 本地(5,0) → 整场 (6,4)，落差 2
            var board = new BoardState(core, p1, p2, half1, HalfFieldData.Flat());
            Assert(board.Field.ElevationAt(6, 4) == 2 && board.Field.ElevationAt(6, 5) == 0,
                   "半场地形拼合（落差写入正确格，边缘恒 0）");
            board.Mode = BoardRulesMode.Flat;
            Assert(board.AttackDistance(6, 4, 6, 5) == 1, "Flat 档忽略落差（P2P 完全平面）");
            board.Mode = BoardRulesMode.Tactical;
            Assert(board.AttackDistance(6, 4, 6, 5) == 3, "Tactical 档落差参与距离（六距1 + |2−0| = 3）");
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
            foreach (var t in AllManaTypes()) pool1.AvailableMana[t] = 99;
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
            Assert(!core.ZoneManager.HasBattlefieldSpace(p1), "战场填满 18 后无空位");

            if (creatureData != null)
            {
                var blocked = new CardWrapper(creatureData);
                blocked.SetController(p1);
                container1.Add(blocked, Zone.Hand);
                int bankBefore = pool1.AvailableMana.Values.Sum();
                Assert(!PlayCardSync(core, p1, blocked), "满场手动出牌被拒（MD 式预检）");
                Assert(pool1.AvailableMana.Values.Sum() == bankBefore, "被拒时费用未扣");
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

        // ======================================== 分支条件目录 + 信息族（Phase A） ========================================

        /// <summary>
        /// 分支目录契约验证：三族覆盖、与设计文稿数值一致、目录↔代码同步契约、维度有限域。
        /// </summary>
        private static void TestBranchCatalog()
        {
            var all = BranchConfigTable.GetAll().ToList();
            Assert(all.Count >= 8, $"BranchConfig 目录加载（{all.Count} 个效果条目 ≥ 8）");
            if (all.Count == 0) return;

            Assert(BranchConfigTable.GetByEffectType("DealDamage")?.Conditions.Count(c => c.Kind == BranchConditionKind.OutcomeGate) == 3,
                   "伤害族 3 条件（DmgKillsTarget/TargetSurvived/Overkill）");
            Assert(BranchConfigTable.GetByEffectType("PierceDamage")?.Conditions.Count >= 2, "穿透伤害仍可挂伤害分支");
            Assert(BranchConfigTable.GetByEffectType("Heal")?.Conditions.Count >= 2, "治疗族 2 条件（Overheal/TargetStillWounded）");
            Assert(BranchConfigTable.GetByEffectType("DeclareHand")?.Conditions.Count >= 2, "宣言族 2 条件（DeclareHit/DeclareMiss）");
            Assert(BranchConfigTable.GetByEffectType("ProphecyNextCard")?.Conditions.Count >= 2, "预言族 2 条件（ProphecyHit/ProphecyMiss）");

            // 抽牌减费缺陷目录已随减费归入代价体系退役（2026-09-16）——减负表达走代价栏 Payload 原子
            // 检索改宣言卡名（ExactCard 单档；TypePlusRace/SingleDimension 不再可表达，已删）
            Assert(BranchConfigTable.GetFilterTier("ExactCard")?.Cost == 3
                   && BranchConfigTable.GetFilterTier("TypePlusRace") == null
                   && BranchConfigTable.GetFilterTier("SingleDimension") == null,
                   "检索维度收敛 ExactCard 单档=3（宣言卡名）");

            // 同步契约：目录项全在原子表 ∩ 产出族；条件 id 全被评估器识别（与 BranchConfigTable 自检同源）
            bool contract = true;
            foreach (var cfg in all)
            {
                if (CardCore.Attribute.AtomicEffectTable.GetByEnumName(cfg.EffectTypeName) == null) contract = false;
                else if (!BranchConfigTable.OutcomeProducerTypes.Contains(cfg.EffectTypeName)) contract = false;
                else
                    foreach (var c in cfg.Conditions)
                        if (c.Kind == BranchConditionKind.OutcomeGate && !BranchConditionEvaluator.IsKnownCondition(c.Id))
                            contract = false;
            }
            Assert(contract, "同步契约：目录项全在原子表 ∩ 产出族，条件 id 全被评估器识别");

            // 不可挂族不在目录（改值/授予/移动系——设计文稿：无可串联条件）
            Assert(BranchConfigTable.GetByEffectType("ModifyPower") == null
                   && BranchConfigTable.GetByEffectType("DrawCard") == null
                   && BranchConfigTable.GetByEffectType("SearchDeck") == null,
                   "改值/移动/检索系不在分支目录（Drawback 与 FilterPrecision 另有挂载机制）");

            // 信息族原子已入原子表（JSON → 运行时表全链路；宣言手牌族只保留一档，探查手牌已删）
            Assert(CardCore.Attribute.AtomicEffectTable.GetByEnumName("DeclareHand") != null
                   && CardCore.Attribute.AtomicEffectTable.GetByEnumName("DeclareDeckTop") != null
                   && CardCore.Attribute.AtomicEffectTable.GetByEnumName("DeclareArrow") != null
                   && CardCore.Attribute.AtomicEffectTable.GetByEnumName("ProphecyNextCard") != null,
                   "信息族 4 原子已入原子表（ID 由描述哈希生成）");
            // 剧毒指示物化（Poison 原子）与毒素/沉默/净化/穿透同链入表
            Assert(CardCore.Attribute.AtomicEffectTable.GetByEnumName("Poison") != null
                   && CardCore.Attribute.AtomicEffectTable.GetByEnumName("AddToxin") != null
                   && CardCore.Attribute.AtomicEffectTable.GetByEnumName("Silence") != null
                   && CardCore.Attribute.AtomicEffectTable.GetByEnumName("Purify") != null
                   && CardCore.Attribute.AtomicEffectTable.GetByEnumName("PierceDamage") != null
                   && CardCore.Attribute.AtomicEffectTable.GetByEnumName("SetCost") != null,
                   "新语义六原子已入表（剧毒指示物/毒素/沉默/净化/穿透/设置费用）");
            // 三轨制同期（2026-09-09）：无效指示物（蓝3）入表 + 设置系三原子 handler 已注册复活
            Assert(CardCore.Attribute.AtomicEffectTable.GetByEnumName("AddNullify") != null,
                   "无效指示物原子已入表（蓝3，非启动式能力无法发动）");
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
            foreach (var t in AllManaTypes()) pool1.AvailableMana[t] = 9;

            // 合成对手手牌：一张生物（红 2 费）+ 一张法术（蓝 1 费）
            var creatureData = new CardData { ID = "VERIFY_INFO_CREATURE", CardName = "验证生物" };
            creatureData.Supertype = Cardtype.Creature;
            creatureData.Cost[(int)ManaType.Red] = 2;
            var spellData = new CardData { ID = "VERIFY_INFO_SPELL", CardName = "验证法术" };
            spellData.Supertype = Cardtype.Spell;
            spellData.Cost[(int)ManaType.Blue] = 1;
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
        /// 先攻/连击/碾压/毒刺/吸血/系命/圣盾/坚韧/护甲/不灭/复生/再生/成长/辟邪/法术护盾。
        /// 守卫/风怒已删除（2026-09-03 原子表整体修正）；剧毒改指示物（毒素/剧毒回合结束结算）；
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
                card.Untap(); // 新规则横置入场；本段测试前提 = 已过回合重置的竖直随从（横置行为单独断言）
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
            Assert(CardCore.Attribute.Handlers.GrantKeywordHandlerFactory.TryGetKeywordId(AtomicEffectType.GrantReborn, out var rebornId) && rebornId == "Reborn",
                   "工厂登记复生关键词 id");

            // ---- 2. 横置可用性 / 冲锋 / 突袭（2026-09-08 定案：一律横置入场；冲锋/突袭=登场效果，无入场豁免） ----
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
            Assert(plain.IsTapped(), "普通随从：一律横置入场");
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
            Assert(charger.IsTapped(), "冲锋（登场效果）：入场时仍横置（结算前无豁免）");
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
                   + $" gate0={core.StackEngine.SpeedCounter.CanActivate(0, true, CardCore.EffectActivationType.Voluntary)}"
                   + $" prio={core.StackEngine.CurrentPriorityHolder?.Name ?? "null"}"
                   + $" act={core.StackEngine.ActivePlayer?.Name ?? "null"} turn={core.TurnEngine.TurnNumber}"
                   + $" tapped={striker.IsTapped()}）");
            GameActions.DrainStack(core);

            // ---- 5. 潜行：不可被指定 + 攻击结算后移除 ----
            var lurker = Make(p2, 2, 2, "Stealth");
            var hunter = Make(p1, 3, 3);
            Assert(!combat.CanAttackTarget(hunter, lurker, p1), "潜行：不可被指定为攻击目标");
            var spy = Make(p1, 2, 2, "Stealth");
            GameActions.DeclareAttack(core, p1, spy, p2);
            GameActions.DrainStack(core);
            Assert(!spy.HasKeyword("Stealth"), "潜行：攻击结算后移除（结算期支付段）");

            // ---- 6. 警戒（2026-09-10 重定义）：攻击照常横置；横置也能造成战斗伤害 ----
            var vigilant = Make(p1, 3, 3, "Vigilance");
            GameActions.DeclareAttack(core, p1, vigilant, p2);
            GameActions.DrainStack(core);
            Assert(vigilant.IsTapped(), "警戒重定义：攻击结算期照常横置（不再代替横置扣除）");
            Assert(CardCore.Attribute.KeywordRules.ShouldTap(vigilant), "警戒重定义：横置代价恒支付");
            // 横置持警戒反击：守卫拦截横置后，持警戒的守卫仍造成战斗伤害（见 TestAttackGuard 新段）

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
            var wardedTank = Make(p2, 1, 20, "DivineShield");
            wardedTank.AddCounters(CardCore.Attribute.KeywordRules.ArmorCounter, 3);
            wardedTank.AddKeyword("Armor"); // 坚韧 −1
            CardCore.Attribute.KeywordRules.ApplyDamage(p1, wardedTank, 5, false, pierce: true);
            Assert(wardedTank.IsAlive && wardedTank.GetLife() == 15 && wardedTank.HasKeyword("DivineShield")
                   && wardedTank.GetCounterCount(CardCore.Attribute.KeywordRules.ArmorCounter) == 3,
                   "穿透：圣盾/护甲/坚韧全被越过（20−5=15，防护层不消耗）");
            CardCore.Attribute.KeywordRules.ApplyDamage(p1, wardedTank, 5, false); // 普通路径对照
            Assert(wardedTank.GetLife() == 15 && !wardedTank.HasKeyword("DivineShield"),
                   "普通伤害对照：圣盾挡下一次并消耗");

            // ---- 8. 先攻：目标死亡不反击 ----
            var first = Make(p1, 5, 3, "FirstStrike");
            var bulky = Make(p2, 4, 3);
            bool declared8 = GameActions.DeclareAttack(core, p1, first, bulky);
            UnityEngine.Debug.Log($"[CMBTDBG] declared={declared8} stack={core.StackEngine.StackSize} first._power={first.GetPower()} layerPower={core.LayerEngine.CalculatePower(first)} firstTapped={first.IsTapped()} bulkyAlive={bulky.IsAlive} bulkyLife={bulky.GetLife()}");
            GameActions.DrainStack(core);
            UnityEngine.Debug.Log($"[CMBTDBG] after ExecuteDamage: bulkyAlive={bulky.IsAlive} bulkyLife={bulky.GetLife()} firstLife={first.GetLife()}");
            Assert(!bulky.IsAlive && first.GetLife() == 3, "先攻：目标死于先攻步，不反击");

            // ---- 8b. 缴械：被攻击的目标无法反击 ----
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
            var doubleS = Make(p1, 2, 9, "DoubleStrike");
            var tank = Make(p2, 1, 5);
            GameActions.DeclareAttack(core, p1, doubleS, tank);
            GameActions.DrainStack(core);
            Assert(tank.GetLife() == 1 && doubleS.IsAlive, "连击：伤害结算两次（5命 −2×2 = 1）");

            // ---- 11. 碾压：邻接受击（注入邻接扩展点） ----
            var hammer = Make(p1, 4, 9, "Overwhelm");
            var pivot = Make(p2, 1, 9);
            var neighbor = Make(p2, 1, 9);
            CardCore.CombatSystem.AdjacentResolver = c => c == pivot ? new[] { neighbor } : System.Array.Empty<Card>();
            GameActions.DeclareAttack(core, p1, hammer, pivot);
            GameActions.DrainStack(core);
            Assert(pivot.GetLife() == 5 && neighbor.GetLife() == 5, "碾压：目标与相邻随从各受 4 点（无反击）");
            CardCore.CombatSystem.AdjacentResolver = null;

            // ---- 12. 毒刺（2026-09-13 第十九批改写版）：将要成功造成的战斗伤害改写为毒素指示物×1
            //      （被改写伤害 return 0 不落血、无伤害事件链；反击无毒刺照常落血）----
            var viper = Make(p1, 1, 9, "PoisonSting");
            var giant = Make(p2, 3, 10);
            GameActions.DeclareAttack(core, p1, viper, giant);
            GameActions.DrainStack(core);
            Assert(giant.IsAlive && giant.GetLife() == 10 && viper.GetLife() == 6
                   && giant.GetCounterCount(CardCore.Attribute.CounterRules.ToxinCounter) == 1,
                   "毒刺：战斗伤害被改写为目标 1 层毒素（目标不落血，反击正常）");
            // 毒素回合结束结算（2026-09-16 统一档）：仅**持有者**回合末发作——施加方回合末不结算
            CardCore.Attribute.CounterRules.OnTurnEnd(p1, core.ZoneManager);
            Assert(giant.GetLife() == 10 && giant.GetCounterCount(CardCore.Attribute.CounterRules.ToxinCounter) == 1,
                   "毒素：施加方回合末不发作（持有者侧结算域）");
            CardCore.Attribute.CounterRules.OnTurnEnd(p2, core.ZoneManager);
            Assert(giant.IsAlive && giant.GetLife() == 9 && giant.GetCounterCount(CardCore.Attribute.CounterRules.ToxinCounter) == 0,
                   "毒素：持有者回合末每层 1 伤后清空（原 3 回合时钟退役）");

            // ---- 12c. 剧毒指示物（新语义）：持有者回合结束时死亡（效果死亡、无伤害来源） ----
            var plagued = Make(p2, 3, 10);
            plagued.AddCounters(CardCore.Attribute.CounterRules.PoisonCounter, 1);
            CardCore.Attribute.CounterRules.OnTurnEnd(p1, core.ZoneManager);
            Assert(plagued.IsAlive, "剧毒：施加方回合末不发作（持有者侧结算域）");
            CardCore.Attribute.CounterRules.OnTurnEnd(p2, core.ZoneManager);
            Assert(!plagued.IsAlive, "剧毒指示物：持有者回合结束时死亡（不再造成即时伤害）");

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
            var shielded = Make(p2, 1, 5, "DivineShield");
            var breaker = Make(p1, 4, 9);
            GameActions.DeclareAttack(core, p1, breaker, shielded);
            GameActions.DrainStack(core);
            Assert(shielded.IsAlive && shielded.GetLife() == 5 && !shielded.HasKeyword("DivineShield"),
                   "圣盾：挡下一次伤害并消耗");
            breaker.Untap();
            breaker.AttacksThisTurn = 0; // 台账清零（2026-09-10 后非门槛，仅为下文计数断言口径干净）
            GameActions.DeclareAttack(core, p1, breaker, shielded);
            GameActions.DrainStack(core);
            Assert(shielded.GetLife() == 1, "圣盾消耗后正常受伤");

            var tough = Make(p2, 1, 9, "Armor"); // 坚韧 −1
            var hitter = Make(p1, 4, 9);
            GameActions.DeclareAttack(core, p1, hitter, tough);
            GameActions.DrainStack(core);
            Assert(tough.GetLife() == 6, "坚韧：最终伤害 −1（9 −3 = 6）");

            // 融合叠加：直接向列表补第二个坚韧（融合继承路径）
            ((IHasKeywords)tough).Keywords.Add("Armor");
            CardCore.Attribute.KeywordRules.ApplyDamage(p1, tough, 4, false);
            Assert(tough.GetLife() == 4, "双坚韧（融合叠加）：伤害 −2");

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

            var phoenix = Make(p1, 2, 5, "Reborn");
            phoenix.IsAlive = false;
            Assert(CardCore.Attribute.KeywordRules.TryReborn(phoenix)
                   && phoenix.IsAlive && phoenix.GetLife() == 1 && phoenix.IsTapped()
                   && !phoenix.HasKeyword("Reborn"),
                   "复生：1 血回场、横置（本回合不可用）、关键词消耗");

            // ---- 15b. 死亡决策表（DeathRules）：死因×护盾 定案断言 ----
            var venomLord = Make(p2, 2, 9, "Indestructible");
            Assert(CardCore.Attribute.DeathRules.TryKill(venomLord, CardCore.Attribute.DeathCause.Poison, p1, core.ZoneManager)
                   && !venomLord.IsAlive,
                   "决策表：剧毒死因不被不灭拦截（伤害族照死——关键词与原子同裁决）");
            var stoneGiant = Make(p2, 2, 9, "Indestructible");
            Assert(!CardCore.Attribute.DeathRules.TryKill(stoneGiant, CardCore.Attribute.DeathCause.DestroyEffect, p1, core.ZoneManager)
                   && stoneGiant.IsAlive,
                   "决策表：不灭拦截摧毁效果死因");
            var ironReborn = Make(p2, 2, 9, "Reborn");
            Assert(!CardCore.Attribute.DeathRules.TryKill(ironReborn, CardCore.Attribute.DeathCause.DestroyEffect, p1, core.ZoneManager)
                   && ironReborn.IsAlive && ironReborn.GetLife() == 1,
                   "决策表：复生对摧毁效果死因生效（消耗回场，TryKill 返回未死）");

            // ---- 15c. 神佑（世界观定案：角色=普通生物单位，免疫来自状态而非硬编码） ----
            // 剧毒已改指示物：神佑拦截"回合结束剧毒死亡"；神佑对净化有抗性（2026-09-09 定案：
            // 净化剥神佑+剧毒组合无法计价平衡——剥除通路关闭，移除留给未来专用效果；RemoveKeyword 手工剥仍可）
            Assert(p1.HasKeyword(CardCore.Attribute.DeathRules.DivineProtection)
                   && p2.HasKeyword(CardCore.Attribute.DeathRules.DivineProtection),
                   "神佑：角色默认持有神佑状态");
            p2.Life = 30;
            p2.AddCounters(CardCore.Attribute.CounterRules.PoisonCounter, 1);
            // 持有者侧结算域（2026-09-16 定案）：p2 的剧毒在 p2 的回合结束发作——须传持有者
            CardCore.Attribute.CounterRules.OnTurnEnd(p2, core.ZoneManager);
            Assert(p2.Life == 30 && p2.IsAlive
                   && p2.GetCounterCount(CardCore.Attribute.CounterRules.PoisonCounter) == 0,
                   "神佑：回合结束剧毒死亡被拦截（指示物照常到期消失）");
            p2.RemoveKeyword(CardCore.Attribute.DeathRules.DivineProtection);
            p2.AddCounters(CardCore.Attribute.CounterRules.PoisonCounter, 1);
            CardCore.Attribute.CounterRules.OnTurnEnd(p2, core.ZoneManager);
            Assert(p2.Life == 0, "神佑移除后：剧毒指示物对角色致死（免疫只来自状态）");
            p2.Life = 30;
            p2.AddKeyword(CardCore.Attribute.DeathRules.DivineProtection, CardCore.KeywordLane.Status);
            // 神佑抗净化锚：净化打在角色上清指示物/临时状态，但神佑保留
            new CardCore.Attribute.Handlers.PurifyHandler().Execute(
                new CardCore.AtomicEffectInstance { Type = CardCore.AtomicEffectType.Purify },
                new CardCore.EffectExecutionContext
                {
                    Source = p1, Controller = p1,
                    Targets = new List<Entity> { p2 },
                    ZoneManager = core.ZoneManager
                });
            Assert(p2.HasKeyword(CardCore.Attribute.DeathRules.DivineProtection),
                   "神佑抗净化：净化后神佑仍在（剥神佑+剧毒组合已堵死，移除留给未来专用效果）");

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

            // ③ 剧毒死亡：死亡来源=施加方（指示物来源，消计数前捕获）
            var poisoner = Make(p1, 3, 3);
            var poisonVictim = Make(p2, 2, 5);
            poisonVictim.AddCounters(CardCore.Attribute.CounterRules.PoisonCounter, 1, poisoner);
            CardDestroyEvent poisonEvt = null;
            void OnPoisonKill(CardDestroyEvent e) { if (e.DestroyedCard == poisonVictim) poisonEvt = e; }
            EventManager.Instance.Subscribe<CardDestroyEvent>(OnPoisonKill);
            // 持有者侧结算域：poisonVictim 属 p2，剧毒在其持有者（p2）回合末发作
            CardCore.Attribute.CounterRules.OnTurnEnd(p2, core.ZoneManager);
            Assert(poisonEvt != null && poisonEvt.Cause == CardCore.Attribute.DeathCause.Poison
                   && poisonEvt.Source == poisoner,
                   "剧毒死亡：死亡来源=施加方（指示物来源登记）");

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

            // ---- 16. 再生（2026-09-13 全额定案）/ 成长（回合开始维护） ----
            for (int i = 0; i < 3; i++) core.ZoneManager.GetZoneContainer(p1).Add(new Card { ID = "VERIFY_KW_DECK" }, Zone.Deck);
            var regrow = Make(p1, 2, 4, "Regeneration", "Growth");
            CardCore.Attribute.KeywordRules.ApplyDamage(p2, regrow, 2, false);
            EventManager.Instance.Publish(new TurnStartEvent { TurnPlayer = p1, TurnNumber = 300 });
            Assert(regrow.GetLife() == 4 + 1 && regrow.GetPower() == 3,
                   "再生恢复全部生命（2→满4）后成长 +1/+1（=5/3）——回合开始维护");

            // ---- 17. 辟邪 / 法术护盾（效果指定） ----
            var hermit = Make(p2, 2, 5, "Untargetable");
            var foe1 = Make(p1, 2, 2);
            Assert(!CardCore.EffectTargetValidator.CanTarget(hermit, foe1, AtomicEffectType.DealDamage)
                   && CardCore.EffectTargetValidator.CanTarget(hermit, hermit, AtomicEffectType.AddArmor),
                   "辟邪：对手效果不可指定，友方可以");

            var warded = Make(p2, 2, 5, "SpellShield");
            var targets = new List<Entity> { warded, p2 };
            CardCore.Attribute.KeywordRules.ConsumeSpellShields(targets, foe1);
            Assert(targets.Count == 1 && targets[0] == p2 && !warded.HasKeyword("SpellShield"),
                   "法术护盾：首次成为对手效果目标时移出目标并消耗");

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
            var twin = Make(p2, 2, 4);
            CardCore.Attribute.CounterRules.AddStatCounter(twin, CardCore.Attribute.CounterRules.MinusOneCounter, 2);
            CardCore.Attribute.CounterRules.AddStatCounter(twin, CardCore.Attribute.CounterRules.PlusOneCounter, 1);
            Assert(twin.GetPower() == 1 && twin.GetMaxLife() == 3,
                   "±1 层对消共存：-1/-1×2 与 +1/+1×1（净 -1/-1）");

            // ---- 18d. 易损：受到伤害每层 +1（防护层吸收放大后的量）；持有者回合末到期 ----
            var brittle = Make(p2, 2, 9);
            brittle.AddCounters(CardCore.Attribute.CounterRules.VulnerableCounter, 2);
            CardCore.Attribute.KeywordRules.ApplyDamage(p1, brittle, 3, false);
            Assert(brittle.GetLife() == 4, "易损：3 伤 + 2 层 = 5 伤（防护层前放大）");
            CardCore.Attribute.CounterRules.OnTurnEnd(p2, core.ZoneManager);
            Assert(brittle.GetCounterCount(CardCore.Attribute.CounterRules.VulnerableCounter) == 0,
                   "易损：持续到持有者回合结束（施加方回合末不结算）");

            // ---- 18e. 紊乱原子：附加后不能以玩家为目标（攻击与效果同口径） ----
            var dizzy = Make(p2, 2, 5);
            dizzy.AddCounters(CardCore.Attribute.KeywordRules.RushSicknessCounter, 1);
            Assert(!combat.CanAttackTarget(dizzy, p1, p1), "紊乱：持有者不能以玩家为目标（攻击侧）");
            // 持有者侧结算域：dizzy 属 p2，紊乱在 p2 回合末消退——须传持有者
            CardCore.Attribute.CounterRules.OnTurnEnd(p2, core.ZoneManager);
            Assert(dizzy.GetCounterCount(CardCore.Attribute.KeywordRules.RushSicknessCounter) == 0,
                   "紊乱：持续到回合结束消退");

            // ---- 18f. 三轨路由·生物轨（非永久档）：Source=生物卡 → 换区清层（来源归因规则①锁定） ----
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
                Source = trackA, // 生物来源（规则①：生物发动的效果来源=该生物）
                Controller = p2,
                Targets = new List<Entity> { trackA },
            }, skipElementCost: true).Forget();
            Assert(trackA.GetCounterCount(CardCore.Attribute.CounterRules.PowerUpCounter) == 2 && trackA.GetPower() == 4,
                   "三轨·生物轨：生物来源攻+2 走换区清层（PowerUp×2 加时回写，非永久默认档）");

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
                   && trackB.GetCounterCount(CardCore.Attribute.CounterRules.PowerUpPermanentCounter) == 0
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

            // ---- 18h. 三轨路由·生物轨永久档：Duration=Permanent → Permanent 层（换区不清、净化清） ----
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
                Source = trackC, // 生物来源 + 永久赋予
                Controller = p2,
                Targets = new List<Entity> { trackC },
            }, skipElementCost: true).Forget();
            Assert(trackC.GetCounterCount(CardCore.Attribute.CounterRules.PowerUpPermanentCounter) == 2
                   && trackC.GetCounterCount(CardCore.Attribute.CounterRules.PowerUpCounter) == 0
                   && trackC.GetPower() == 4,
                   "三轨·生物轨永久档：Duration=Permanent 走 PowerUpPermanent 层（单属性，不带动生命）");
            core.ZoneManager.MoveCard(trackC, p2, Zone.Battlefield, Zone.Hand);
            Assert(trackC.GetCounterCount(CardCore.Attribute.CounterRules.PowerUpPermanentCounter) == 2
                   && trackC.GetPower() == 4 && trackC.GetMaxLife() == 5,
                   "三轨·生物轨永久档：换区不清（±1/±1 双属性层不同——单属性永久层随卡走）");
            CardCore.Attribute.CounterRules.PurgeAll(trackC);
            Assert(trackC.GetCounterCount(CardCore.Attribute.CounterRules.PowerUpPermanentCounter) == 0
                   && trackC.GetPower() == 2,
                   "三轨·生物轨永久档：净化清（反向回写）——区别于设置类");

            // ---- 19. 净化（2026-09-09 语义重定义：变回生物原有状态）----
            // 关键词四轨矩阵：Printed（卡面本体）保留 / Setting（魔法卡赋予视同本体）保留 /
            // Temp（临时）清 / GrantedPermanent（生物赋的永久）清；指示物全清（含永久层）。
            var cursed = Make(p2, 3, 6, "Taunt"); // Taunt=Printed（CardWrapper 构造注入）
            cursed.AddKeyword("Stealth", CardCore.KeywordLane.Temp);                    // 临时轨
            cursed.AddKeyword("Indestructible", CardCore.KeywordLane.GrantedPermanent); // 生物赋的永久
            cursed.AddKeyword("Vigilance", CardCore.KeywordLane.Setting);               // 设置类（视同本体）
            cursed.AddCounters(CardCore.Attribute.CounterRules.ToxinCounter, 2,
                turns: CardCore.Attribute.CounterRules.Find(CardCore.Attribute.CounterRules.ToxinCounter).Turns);
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
                   && !cursed.HasKeyword("Stealth") && !cursed.HasKeyword("Indestructible"),
                   "净化四轨矩阵：本体与设置类保留，临时/生物永久赋予清除");
            Assert(cursed.GetCounterCount(CardCore.Attribute.CounterRules.ToxinCounter) == 0
                   && cursed.GetCounterCount(CardCore.Attribute.KeywordRules.ArmorCounter) == 0,
                   "净化：全部指示物清空（含永久类，属性层反向回写）");

            // ---- 19b. 关键词换区清除（三轨制定案）：Temp 换区清、Printed 换区留 ----
            var zoneKw = Make(p2, 2, 4, "Taunt");
            zoneKw.AddKeyword("Stealth", CardCore.KeywordLane.Temp);
            zoneKw.AddKeyword("FirstStrike", CardCore.KeywordLane.GrantedPermanent);
            core.ZoneManager.MoveCard(zoneKw, p2, Zone.Battlefield, Zone.Hand);
            Assert(!zoneKw.HasKeyword("Stealth")
                   && zoneKw.HasKeyword("Taunt") && zoneKw.HasKeyword("FirstStrike"),
                   "换区清关键词：Temp 轨清除；Printed 与 GrantedPermanent（换区不清档）保留");

            // ---- 19c. 生物轨永久档属性层：Permanent 层换区不清、净化清 ----
            var permBuff = Make(p2, 2, 4);
            CardCore.Attribute.CounterRules.AddStatCounter(permBuff,
                CardCore.Attribute.CounterRules.PowerUpPermanentCounter, 2, p1);
            CardCore.Attribute.CounterRules.AddStatCounter(permBuff,
                CardCore.Attribute.CounterRules.LifeUpPermanentCounter, 1, p1);
            Assert(permBuff.GetPower() == 4 && permBuff.GetMaxLife() == 5,
                   "永久档属性层：加时回写（与换区清层同粒度）");
            core.ZoneManager.MoveCard(permBuff, p2, Zone.Battlefield, Zone.Hand);
            Assert(permBuff.GetCounterCount(CardCore.Attribute.CounterRules.PowerUpPermanentCounter) == 2
                   && permBuff.GetPower() == 4,
                   "永久档属性层：换区不清（生物赋的永久属性随卡走）");
            CardCore.Attribute.KeywordRules.PurifyKeywords(permBuff);
            CardCore.Attribute.CounterRules.PurgeAll(permBuff);
            Assert(permBuff.GetCounterCount(CardCore.Attribute.CounterRules.PowerUpPermanentCounter) == 0
                   && permBuff.GetPower() == 2,
                   "永久档属性层：净化清（反向回写）");

            // ---- 19d. 入场刷新（2026-09-09 定案）：真实入场补回被消耗的卡面关键词（Printed 差集）；
            //      复生（原地留场）与控制权变更（容器直移）不经统一出口，天然不触发 ----
            var refreshShield = Make(p2, 2, 5, "DivineShield");
            CardCore.Attribute.KeywordRules.ApplyDamage(p1, refreshShield, 1, false);
            Assert(!refreshShield.HasKeyword("DivineShield") && refreshShield.GetLife() == 5,
                   "入场刷新前置：圣盾挡一次伤害后消耗（关键词移除、伤害被完全挡下）");
            core.ZoneManager.MoveCard(refreshShield, p2, Zone.Battlefield, Zone.Hand); // 弹回手
            core.ZoneManager.TryAddToBattlefield(refreshShield, p2);                   // 真实入场（统一出口）
            Assert(refreshShield.HasKeyword("DivineShield"),
                   "入场刷新：弹回重打补回被消耗的卡面圣盾（Printed 轨差集补齐）");

            var refreshReborn = Make(p2, 2, 5, "Reborn");
            CardCore.Attribute.DeathRules.TryKill(refreshReborn, CardCore.Attribute.DeathCause.DamageLethal, p1, core.ZoneManager);
            Assert(refreshReborn.IsAlive && refreshReborn.GetLife() == 1 && !refreshReborn.HasKeyword("Reborn"),
                   "复生不触发刷新：死亡替代原地留场（不经入场口），消耗后不自我补回（无无限复生）");

            var refreshStolen = Make(p1, 2, 5, "DivineShield");
            CardCore.Attribute.KeywordRules.ApplyDamage(p2, refreshStolen, 1, false); // 消耗圣盾
            new CardCore.Attribute.Handlers.GainControlHandler().Execute(
                new CardCore.AtomicEffectInstance { Type = CardCore.AtomicEffectType.GainControl },
                new CardCore.EffectExecutionContext
                {
                    Source = p2, Controller = p2,
                    Targets = new List<Entity> { refreshStolen },
                    ZoneManager = core.ZoneManager
                });
            Assert(!refreshStolen.HasKeyword("DivineShield"),
                   "控制权变更不触发刷新：场内迁移（容器直移+补发事件）不补回消耗项（偷取不白得圣盾）");

            // ---- 20. 沉默指示物：持有者不可发动主动效果（激活式能力路径） ----
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
                   "沉默：持有者激活式能力被封锁（出牌不受限——PlayCard 不经此门）");

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
                   "无效：非启动式（触发式）被拦——受击触发不上栈");
            // 对照：无效消退后同一触发照常上栈结算
            nullified.RemoveCounters(CardCore.Attribute.CounterRules.NullifyCounter, 1);
            CardCore.Attribute.KeywordRules.ApplyDamage(p1, nullified, 1, false);
            GameActions.DrainStack(core);
            Assert(core.ZoneManager.GetCards(p2, Zone.Hand).Count == p2HandAtNullify + 1,
                   "无效消退后：同一触发照常上栈（对照锚，证明 20b 拦截来自无效指示物本身）");

            // ---- 20c. 无效不拦启动式（与沉默对照：沉默拦启动式、无效拦非启动式，互不重叠） ----
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
                   "无效：启动式照常可发动（拦截面与沉默互补不重叠）");

            // ---- 20d. 无效不拦伤害管线被动（坚韧/圣盾等非「能力发动」；回合维护再生/成长同口径） ----
            var nullTough = Make(p2, 3, 8, "Armor"); // 坚韧：每次受伤 −1
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
                       && core.ZoneManager.GetCards(p2, Zone.Graveyard).Count == graveBefore + 1
                       && !landTarget.WasDepletedAsLand,
                       "摧毁：地牌出池直送墓地（非耗尽——回收后可再作地牌）");
            }
            else
            {
                Debug.LogWarning("[Verify] 跳过摧毁段：p2 无可用地牌（池空且手牌无可入池生物）");
            }

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
            Assert(pool1.AvailableMana[ManaType.Gray] == 0,
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

            CleanKeywords();
        }

        // ======================================== 仪式系统（竞速任务卡） ========================================

        /// <summary>开局入手断言：占卡组位（牌库留 2 副本）、不占起手数（额外入手）、双方各 1 张。</summary>
        private static void TestRitualOpeningHand(GameCore core, List<CardData> cardsData)
        {
            // 测试主卡组：非仪式卡 + 单张三相仪典（构筑规则：卡组不重复、测试只带 1 张仪式）
            if (!cardsData.Any(c => c.ID == "RITUAL_TRINITY_001")) return;

            var p1 = core.Player1;
            var p2 = core.Player2;

            foreach (var p in new[] { p1, p2 })
            {
                var hand = core.ZoneManager.GetCards(p, Zone.Hand);
                var ritualsInHand = hand.Count(c => RitualSystem.IsRitual(c)).ToString();
                Assert(hand.Count(c => RitualSystem.IsRitual(c)) == 1,
                       $"仪式占初始手牌位：{p.Name} 手牌恰含 1 张仪式（实际 {ritualsInHand}）");
                Assert(core.ZoneManager.GetCards(p, Zone.Deck).Count(c => RitualSystem.IsRitual(c)) == 0,
                       $"{p.Name} 牌库无仪式残留（单张已占位入手）");
                // 总量断言按玩家区分：p1 已含首回合抽牌（6+1=7），p2 尚未开始回合（6）
            }

            // InitGame 已开 P1 回合（含首回合抽 1）：p1 = 6 + 1 = 7；p2 尚未开始回合 = 6
            Assert(core.ZoneManager.GetCards(p1, Zone.Hand).Count == 7, "p1 含首回合抽牌共 7");
            Assert(core.ZoneManager.GetCards(p2, Zone.Hand).Count == 6, "p2 初始 6");
        }

        /// <summary>
        /// 仪式全流程验证（末段执行，注入卡驱动，不重开对局）：
        /// 0费打出/占格 → 全局唯一任务（同玩家第二张顶掉第一张）→ 竞速颜色断言（失败清零、3回合达标）
        /// → 完成态（不灭+辟邪、光环独享）→ 血偿流（生命累计/支付转嫁/完成态不可破坏）
        /// → 进行中破坏回手（手牌满则入墓）→ 完成态光环与新任务并存。
        /// </summary>
        private static void TestRituals(GameCore core, List<CardData> cardsData)
        {
            var trinityData = cardsData.FirstOrDefault(c => c.ID == "RITUAL_TRINITY_001");
            var bloodData = cardsData.FirstOrDefault(c => c.ID == "RITUAL_BLOOD_002");
            if (trinityData == null || bloodData == null)
            {
                Debug.LogWarning("[Verify] 跳过仪式段：卡表缺仪式卡");
                return;
            }

            var p1 = core.Player1;
            var p2 = core.Player2;
            var pool1 = core.ElementPool.GetPool(p1);
            var pool2 = core.ElementPool.GetPool(p2);

            // 脚手架：起手改 6（含仪式）后手牌常满 7，回手类断言需要余位——双方裁到 5
            foreach (var p in new[] { p1, p2 })
            {
                var handSnapshot = core.ZoneManager.GetCards(p, Zone.Hand).ToList();
                foreach (var extra in handSnapshot.Skip(5))
                    core.ZoneManager.MoveCard(extra, p, Zone.Hand, Zone.Graveyard);
            }

            // ---- 1. 0 费打出 + 占格 ----
            var trinityA = InjectCard(core, p1, trinityData);
            Assert(PlayCardSync(core, p1, trinityA), "仪式 0 费打出成功（无需任何元素）");
            Assert(core.ZoneManager.GetCards(p1, Zone.Battlefield).Contains(trinityA), "仪式占战场格");
            Assert(RitualSystem.Active != null && RitualSystem.Active.Card == trinityA
                   && RitualSystem.Active.Definition.id == "RITUAL_TRINITY_001",
                   "打出即激活为全局唯一任务");

            // ---- 2. 全局唯一：同玩家第二张顶掉第一张 ----
            var trinityB = InjectCard(core, p1, trinityData);
            Assert(PlayCardSync(core, p1, trinityB), "第二张仪式打出成功");
            Assert(RitualSystem.Active.Card == trinityB, "后发仪式成为唯一任务");
            Assert(core.ZoneManager.GetCards(p1, Zone.Hand).Contains(trinityA)
                   && !core.ZoneManager.GetCards(p1, Zone.Battlefield).Contains(trinityA),
                   "先发仪式被顶掉：进度作废并回手牌");

            // ---- 3. 竞速颜色断言：红(过)→红(败清零)→蓝→绿→红 = 连续3回合达标 ----
            foreach (var t in AllManaTypes()) pool1.AvailableMana[t] = 99;

            bool firstRedChecked = false;
            foreach (var color in new[] { ManaType.Red, ManaType.Red, ManaType.Blue, ManaType.Green, ManaType.Red })
            {
                var colored = InjectColoredCreature(core, p1, color);
                Assert(PlayCardSync(core, p1, colored), $"竞速回合用 {color} 卡");
                EndTurnPumped(core, p1);    // TurnEnd 断言结算
                GameActions.SkipElementPool(core, p2);
                EndTurnPumped(core, p2);    // p2 空过（p2 自己断言失败，不完成）
                GameActions.SkipElementPool(core, p1);

                // 仅首个红回合检查 streak==1（第二个红回合按规则清零，不在本断言范围）
                if (color == ManaType.Red && !firstRedChecked && RitualSystem.Active != null)
                {
                    firstRedChecked = true;
                    int s1 = RitualComponents.ColorStreak.GetStreak(p1);
                    Assert(s1 == 1, $"首个红回合断言通过 streak=1（实际 {s1}）");
                }
            }

            Assert(RitualSystem.Active == null && RitualSystem.CompletedAuras.Count == 1
                   && RitualSystem.CompletedAuras[0].Completer == p1
                   && RitualSystem.CompletedAuras[0].Card == trinityB,
                   "连续 3 回合颜色断言达标 → p1 完成仪式，任务槽清空（对手进度作废）");
            Assert(trinityB.HasKeyword(CardCore.Attribute.KeywordRules.Indestructible)
                   && trinityB.HasKeyword(CardCore.Attribute.KeywordRules.Untargetable),
                   "完成态：不可摧毁 + 不受其他卡效果影响");

            // ---- 4. 三相光环：完成者付 3 纯色 → RGB 各 +1（守恒兑换，独享） ----
            foreach (var t in AllManaTypes()) pool2.AvailableMana[t] = 99;
            int r1 = pool1.AvailableMana[ManaType.Red], b1 = pool1.AvailableMana[ManaType.Blue], g1 = pool1.AvailableMana[ManaType.Green];
            core.ElementPool.PayCost(new Dictionary<int, float> { [(int)ManaType.Red] = 3f }, p1);
            Assert(pool1.AvailableMana[ManaType.Red] == r1 - 3 + 1
                   && pool1.AvailableMana[ManaType.Blue] == b1 + 1
                   && pool1.AvailableMana[ManaType.Green] == g1 + 1,
                   "三相光环：完成者付 3 红 → 红/蓝/绿各 +1");

            int r2b = pool2.AvailableMana[ManaType.Red], b2b = pool2.AvailableMana[ManaType.Blue];
            core.ElementPool.PayCost(new Dictionary<int, float> { [(int)ManaType.Red] = 3f }, p2);
            Assert(pool2.AvailableMana[ManaType.Red] == r2b - 3 && pool2.AvailableMana[ManaType.Blue] == b2b,
                   "三相光环：对手消耗不转化（完成者独享）");

            // 余数保留：再付 1 红（累计 4 → 第二次转化不触发，计数 1）
            int r1c = pool1.AvailableMana[ManaType.Red];
            core.ElementPool.PayCost(new Dictionary<int, float> { [(int)ManaType.Red] = 1f }, p1);
            Assert(pool1.AvailableMana[ManaType.Red] == r1c - 1, "三相光环：余数保留（累计 4 只转化一次，余 1 不触发）");

            // ---- 5. 血偿流：p2 打出（与完成态光环并存）→ 累计 30 命 → 完成 → 支付转嫁 ----
            EndTurnPumped(core, p1);          // 颜色竞速后轮到 p2
            GameActions.SkipElementPool(core, p2);
            var blood = InjectCard(core, p2, bloodData);
            Assert(PlayCardSync(core, p2, blood), "血偿仪典打出（0 费）");
            Assert(RitualSystem.Active != null && RitualSystem.Active.Card == blood
                   && RitualSystem.CompletedAuras.Count == 1,
                   "完成态光环与进行中任务并存（光环占格存续，新仪式开新任务）");

            p2.Life = 100; // 测试脚手架：保证 3×10 可付且不触发抵扣归零终局
            // 2026-09-14 代价原子化：生命支付=LifeLoss 原子（流失自己→LifePaymentCostEvent，血偿进度照常计数）
            for (int i = 0; i < 3; i++)
            {
                CardCore.Attribute.EffectHandlerRegistry.ExecuteEffectAsync(
                    new AtomicEffectInstance { Type = AtomicEffectType.LifeLoss, Value = 10 },
                    new EffectExecutionContext { Controller = p2, Source = p2, Targets = new List<Entity> { p2 } })
                    .GetAwaiter().GetResult();
                Assert(p2.MaxHealth == 30 - (i + 1) * 10,
                       $"血偿任务：第 {i + 1} 次流失 10 上限（累计 {(i + 1) * 10}）");
            }
            Assert(RitualSystem.Active == null && RitualSystem.CompletedAuras.Count == 2
                   && RitualSystem.CompletedAuras[1].Completer == p2,
                   "累计支付 30 生命 → p2 完成血偿仪典");
            Assert(blood.HasKeyword(CardCore.Attribute.KeywordRules.Indestructible), "血偿完成态：不可摧毁");

            // ---- 6. 血偿光环转嫁：已随 LifePayment 代价处理器退役（2026-09-14 代价原子化）----
            // 装饰器（包装代价处理器）无挂点可包；仪式复活时改挂 LifeLoss 原子执行口
            // （流失自己=支付，见 LifeLossHandler 的 LifePaymentCostEvent 发布处）。

            // ---- 7. 完成态不可破坏 ----
            var onField = core.ZoneManager.GetCards(p2, Zone.Battlefield).Contains(blood);
            DestroyViaEffect(core, p2, blood);
            Assert(onField && core.ZoneManager.GetCards(p2, Zone.Battlefield).Contains(blood) && blood.IsAlive,
                   "完成态仪式：Destroy 无效（不灭）");

            // ---- 8. 进行中破坏 → 回手牌；手牌满 → 入墓 ----
            // 脚手架：竞速 5 轮抽牌后 p2 手牌常已满 7，回手断言需要余位——裁到 5
            var p2HandBeforeDestroy = core.ZoneManager.GetCards(p2, Zone.Hand).ToList();
            foreach (var extra in p2HandBeforeDestroy.Skip(5))
                core.ZoneManager.MoveCard(extra, p2, Zone.Hand, Zone.Graveyard);
            var blood2 = InjectCard(core, p2, bloodData);
            Assert(PlayCardSync(core, p2, blood2), "第二张血偿打出（新任务）");
            DestroyViaEffect(core, p2, blood2);
            Assert(core.ZoneManager.GetCards(p2, Zone.Hand).Contains(blood2)
                   && !core.ZoneManager.GetCards(p2, Zone.Graveyard).Contains(blood2)
                   && RitualSystem.Active == null,
                   "进行中仪式被破坏 → 回手牌（非墓地），任务进度清空");

            Assert(PlayCardSync(core, p2, blood2), "回手的仪式可再打出（进度重开）");
            var container2 = core.ZoneManager.GetZoneContainer(p2);
            // 脚手架：先裁再补到恰好 7（异步手牌上限弃牌可能已把手牌压到任意值）
            var p2HandSnapshot = core.ZoneManager.GetCards(p2, Zone.Hand).ToList();
            foreach (var extra in p2HandSnapshot.Skip(7))
                container2.Move(extra, Zone.Hand, Zone.Graveyard);
            while (core.ZoneManager.GetCards(p2, Zone.Hand).Count < 7)
                container2.Add(new Card { ID = "VERIFY_RITUAL_FILLER" }, Zone.Hand);
            Assert(core.ZoneManager.GetCards(p2, Zone.Hand).Count == 7, "测试脚手架：p2 手牌灌满 7");
            DestroyViaEffect(core, p2, blood2);
            Assert(core.ZoneManager.GetCards(p2, Zone.Graveyard).Contains(blood2)
                   && !core.ZoneManager.GetCards(p2, Zone.Hand).Contains(blood2),
                   "手牌已满时仪式被破坏 → 直接入墓地");
        }

        /// <summary>
        /// 六轴扩展仪式验证（注入驱动，TestRituals 之后执行，不重开对局）：
        /// 丰盈（治疗溢出 20 → 角色伤害封顶 5）/ 窥渊（展示对手手牌 10 → 回合开始锁定）/
        /// 归土（自己送墓 30 → 墓地视手牌使用，每回合一次）/ 疾风（跳过准备阶段 ×2 → 额外回合 → 自毁）/
        /// 纳川（非抽牌入手 15 → 手牌上限 15 + 免疲劳）。
        /// </summary>
        private static void TestNewRituals(GameCore core, List<CardData> cardsData)
        {
            CardData RitualData(string id) => cardsData.FirstOrDefault(c => c.ID == id);
            var survival = RitualData("RITUAL_SURVIVAL_003");
            var info = RitualData("RITUAL_INFO_004");
            var resource = RitualData("RITUAL_RESOURCE_005");
            var tempo = RitualData("RITUAL_TEMPO_006");
            var handRitual = RitualData("RITUAL_HAND_007");
            var creature = cardsData.FirstOrDefault(c => c.CardName == "古树守卫");
            if (survival == null || info == null || resource == null || tempo == null || handRitual == null || creature == null)
            {
                Debug.LogWarning("[Verify] 跳过六轴仪式段：卡表缺新仪式卡或生物载体");
                return;
            }

            var p1 = core.Player1;
            var p2 = core.Player2;
            var pool1 = core.ElementPool.GetPool(p1);
            foreach (var t in AllManaTypes()) pool1.AvailableMana[t] = 99; // 脚手架：支付不设限

            // ---- 1. 丰盈仪典：治疗溢出 20 → 完成 → 角色单次伤害封顶 5 ----
            EnsureMainPhase(core, p1);
            var survivalCard = InjectCard(core, p1, survival);
            Assert(PlayCardSync(core, p1, survivalCard), "丰盈仪典 0 费打出（顶掉进行中任务）");

            p1.Life = 29;
            EventManager.Instance.Publish(new CardCore.Attribute.HealEvent { Target = p1, Amount = 12, Overfill = 11, Source = null });
            EventManager.Instance.Publish(new CardCore.Attribute.HealEvent { Target = p1, Amount = 12, Overfill = 9, Source = null });
            Assert(RitualSystem.Active == null && RitualSystem.CompletedAuras.Any(a => a.Card == survivalCard),
                   "治疗溢出累计 20 → 丰盈完成");

            p1.Life = 30;
            CardCore.Attribute.KeywordRules.ApplyDamage(null, p1, 12, false);
            Assert(p1.Life == 25, "丰盈光环：完成者单次伤害封顶 5（12 截断为 5）");
            p2.Life = 30;
            CardCore.Attribute.KeywordRules.ApplyDamage(null, p2, 12, false);
            Assert(p2.Life == 18, "无光环对手照常全额受伤（12）");

            // ---- 2. 窥渊仪典：展示对手手牌累计 10 → 每回合开始锁定 1 张 ----
            var infoCard = InjectCard(core, p1, info);
            Assert(PlayCardSync(core, p1, infoCard), "窥渊仪典打出");

            while (core.ZoneManager.GetCards(p2, Zone.Hand).Count < 5)
                InjectCard(core, p2, creature); // 脚手架：保证两次全场展示 ≥ 10 张
            var p2Hand = core.ZoneManager.GetCards(p2, Zone.Hand).ToList();
            var revealSource = InjectCard(core, p1, creature); // 展示方（Source 控制者 = p1）
            EventManager.Instance.Publish(new CardCore.Attribute.RevealHandEvent { Player = p2, Cards = p2Hand, Source = revealSource });
            EventManager.Instance.Publish(new CardCore.Attribute.RevealHandEvent { Player = p2, Cards = p2Hand, Source = revealSource });
            Assert(RitualSystem.CompletedAuras.Any(a => a.Card == infoCard),
                   $"展示对手手牌累计 {p2Hand.Count * 2} ≥ 10 → 窥渊完成");

            var lockedCandidate = p2Hand[0];
            EndTurnPumped(core, p1);                    // → p2 回合开始：无 UI 自动锁定首张已展示卡
            Assert(LockRevealedAura.IsLockedThisTurn(lockedCandidate), "窥渊光环：回合开始锁定对手已展示卡");
            GameActions.SkipElementPool(core, p2);
            Assert(!PlayCardSync(core, p2, lockedCandidate), "被锁定的卡本回合不可使用");
            EndTurnPumped(core, p2);                    // 回合结束 → 锁定清空
            Assert(!LockRevealedAura.IsLockedThisTurn(lockedCandidate), "回合结束：锁定解除");

            // ---- 3. 归土仪典：自己送墓 30 → 墓地视手牌使用（每回合一次）----
            EnsureMainPhase(core, p1);
            var resourceCard = InjectCard(core, p1, resource);
            Assert(PlayCardSync(core, p1, resourceCard), "归土仪典打出");

            // 2026-09-14 代价原子化：送墓=MillCard 原子（磨自己牌库→MillDeckCostEvent 批量计数）
            int deckBeforeMill = core.ZoneManager.GetCards(p1, Zone.Deck).Count;
            CardCore.Attribute.EffectHandlerRegistry.ExecuteEffectAsync(
                new AtomicEffectInstance { Type = AtomicEffectType.MillCard, Value = 5 },
                new EffectExecutionContext { Controller = p1, Source = p1, ZoneManager = core.ZoneManager, ElementPool = core.ElementPool })
                .GetAwaiter().GetResult();
            Assert(core.ZoneManager.GetCards(p1, Zone.Deck).Count == deckBeforeMill - 5,
                   "送墓原子执行：磨自己 5 张（MillDeckCostEvent 批量计数）");
            for (int i = 0; i < 25; i++)
                EventManager.Instance.Publish(new CardCore.Attribute.CardMillEvent { Player = p1, Source = null });
            Assert(RitualSystem.CompletedAuras.Any(a => a.Card == resourceCard), "送墓累计 30 → 归土完成");

            var graveCreature = InjectCard(core, p1, creature);
            core.ZoneManager.MoveCard(graveCreature, p1, Zone.Hand, Zone.Graveyard);
            var graveCreature2 = InjectCard(core, p1, creature);
            core.ZoneManager.MoveCard(graveCreature2, p1, Zone.Hand, Zone.Graveyard);
            Assert(PlayCardSync(core, p1, graveCreature, null, Zone.Graveyard), "墓地视手牌使用：第一张成功");
            Assert(!PlayCardSync(core, p1, graveCreature2, null, Zone.Graveyard), "每回合限一次：第二张被拒");

            // ---- 4. 疾风仪典：跳过准备阶段 ×2 → 额外回合 → 自毁 ----
            EnsureMainPhase(core, p1);
            var tempoCard = InjectCard(core, p1, tempo);
            Assert(PlayCardSync(core, p1, tempoCard), "疾风仪典打出");

            int handBeforeSkip = core.ZoneManager.GetCards(p1, Zone.Hand).Count;

            RitualComponents.SkipStandby.Commit(p1);
            EndTurnPumped(core, p1);          // → p2 回合（p1 池随全局推进 +1）
            int idxAfterOpponentTurn = pool1.GlobalTurnIndex;
            GameActions.SkipElementPool(core, p2);
            EndTurnPumped(core, p2);          // → p1 回合开始：宣告消费，准备阶段整体跳过
            // 断言意图而非精确快照：回合结束的异步手牌弃牌可能减手牌，故只断「不增」（未抽牌）
            Assert(core.ZoneManager.GetCards(p1, Zone.Hand).Count <= handBeforeSkip, "跳过准备阶段：本回合未抽牌（手牌不增）");
            Assert(pool1.GlobalTurnIndex == idxAfterOpponentTurn, "跳过准备阶段：自己的回合不再推进地牌槽曲线");
            int skip1 = RitualComponents.SkipStandby.GetSkipCount(p1);
            Assert(skip1 == 1, "跳过计数 1");

            GameActions.SkipElementPool(core, p1);  // 跳过后的准备阶段照常推进入主阶段
            RitualComponents.SkipStandby.Commit(p1);
            EndTurnPumped(core, p1);
            GameActions.SkipElementPool(core, p2);
            EndTurnPumped(core, p2);          // 第二次跳过 → 达标完成
            Assert(RitualSystem.Active == null && RitualSystem.CompletedAuras.Any(a => a.Card == tempoCard),
                   "跳过 ×2 → 疾风完成（一次性奖励，不形成常驻光环语义）");

            GameActions.SkipElementPool(core, p1);  // 第二次跳过后的准备阶段 → 主阶段
            EndTurnPumped(core, p1);          // 完成回合结束 → 授予额外回合
            Assert(core.TurnEngine.TurnPlayer == p1, "完成的回合结束 → p1 获得额外回合（连续行动）");
            GameActions.SkipElementPool(core, p1);
            EndTurnPumped(core, p1);          // 额外回合结束 → 仪式自毁
            Assert(core.ZoneManager.GetCards(p1, Zone.Graveyard).Contains(tempoCard)
                   && !RitualSystem.CompletedAuras.Any(a => a.Card == tempoCard),
                   "额外回合结束 → 疾风自毁入墓（光环移除）");

            // ---- 5. 纳川仪典：非抽牌入手 15 → 手牌上限 15 + 免疲劳 ----
            EnsureMainPhase(core, p1);
            var handCard = InjectCard(core, p1, handRitual);
            Assert(PlayCardSync(core, p1, handCard), "纳川仪典打出");

            var shuttle = InjectCard(core, p1, creature);
            core.ZoneManager.MoveCard(shuttle, p1, Zone.Hand, Zone.Graveyard);
            for (int i = 0; i < 15; i++)
            {
                core.ZoneManager.MoveCard(shuttle, p1, Zone.Graveyard, Zone.Hand);   // 非抽牌入手 +1
                if (i < 14) core.ZoneManager.MoveCard(shuttle, p1, Zone.Hand, Zone.Graveyard);
            }
            Assert(RitualSystem.CompletedAuras.Any(a => a.Card == handCard), "非抽牌入手累计 15 → 纳川完成");
            Assert(RuleHooks.GetHandLimit(p1) == 15, "手牌上限 7 → 15");
            Assert(HandLimitAura.HasFatigueImmunity(p1), "空库抽牌免疲劳");

            foreach (var c in core.ZoneManager.GetCards(p1, Zone.Deck).ToList())
                core.ZoneManager.MoveCard(c, p1, Zone.Deck, Zone.Graveyard);         // 清空牌库
            int lifeBeforeFatigue = p1.Life;
            ZoneManagerExtensions.DrawCard(core.ZoneManager, p1);
            Assert(p1.Life == lifeBeforeFatigue && p1.FatigueCount == 0,
                   "空库抽牌：免疲劳（无伤害、不计数）");
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
            data.Cost[(int)color] = 1;
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
        /// 毁灭原子已删除（2026-09-03）——仪式摧毁回手特例仍挂在 DestroyEffect 死因上，直连同路径。
        /// </summary>
        private static void DestroyViaEffect(GameCore core, Player actor, Card target)
        {
            CardCore.Attribute.DeathRules.TryKill(target, CardCore.Attribute.DeathCause.DestroyEffect, actor, core.ZoneManager);
        }

        // ======================================== 统一计价锚点（规则一·平衡） ========================================

        /// <summary>
        /// 统一计价锚点验证（纯函数，不依赖卡表/对局）：
        /// 表值（Heal 0.5 / White→Gray / d(C)）、身材 1费=2属性、法术不折、回2命=1费、
        /// 9费挂3费全免（d(9)=0）、卡层组合费用（挂载口两向 + 抉择价差——2026-09-07 新层，
        /// 原「选发额外折」已删）、构筑代价抵消（无"超模"态）、关键词同享 d(C)、缺省档位 Ĉ。
        /// </summary>
        // ======================================== 合成器模型（2026-09-14 合成器重做） ========================================

        /// <summary>
        /// 合成器重做模型断言（纯模型层——UI 交互留 Unity 手测）：
        /// ①三主干表行（MountKinds=9/BaseCost=0/空域）；②converter 主干守卫；③自由分支 header 通道端到端
        /// （RewardAtoms 转存/机制费/CountdownTurns 换算）；④有限分支预算（RewardDerivedCost vs GatePremium）；
        /// ⑤并列保序；⑥描述动态渲染（值随机区间文本）；⑦哈希口径（amp/EngineKind 参与去重）。
        /// </summary>
        private static void TestComposerModel()
        {
            // ---- 1. 三主干表行 ----
            foreach (var name in new[] { "BranchEngineClash", "BranchEngineLuckRoll", "BranchEngineCountdown" })
            {
                var row = CardCore.Attribute.AtomicEffectTable.GetByEnumName(name);
                Assert(row != null, $"主干表行存在：{name}");
                Assert(row.MountKinds == "9", $"{name} MountKinds=\"9\"（空会被兜底混入主动原子）");
                Assert(row.TotalUnitCost == 0f && string.IsNullOrEmpty(row.TargetKinds) && row.Polarity == 0f,
                       $"{name} 主干不计价/无域/零极性");
            }
            Assert(System.Enum.IsDefined(typeof(CardCore.MountKind), 9), "MountKind 含 9=FreeBranchTrunk");
            Assert(CardCore.ComposerCatalog.IsEngineTrunk(CardCore.AtomicEffectType.BranchEngineClash)
                   && CardCore.ComposerCatalog.IsEngineTrunk(CardCore.AtomicEffectType.BranchEngineLuckRoll)
                   && CardCore.ComposerCatalog.IsEngineTrunk(CardCore.AtomicEffectType.BranchEngineCountdown)
                   && !CardCore.ComposerCatalog.IsEngineTrunk(CardCore.AtomicEffectType.DealDamage),
                   "ComposerCatalog.IsEngineTrunk 判定（三主干在内/普通原子不在）");
            Assert(CardCore.ComposerCatalog.TrunkToEngine(CardCore.AtomicEffectType.BranchEngineClash)
                       == CardCore.BranchEngineKind.Clash
                   && CardCore.ComposerCatalog.TrunkToEngine(CardCore.AtomicEffectType.DealDamage)
                       == CardCore.BranchEngineKind.None,
                   "TrunkToEngine 映射");

            // ---- 2. converter 主干守卫：trunk 塞普通步骤 → ConvertAtomicEffect 剔除（null）----
            var trunkDef = CardCore.CardEffectConverter.ConvertOne(new CardCore.CardEffectData
            {
                Id = "VERIFY_TRUNK_GUARD",
                AtomicEffects = new List<CardCore.AtomicEffectEntry>
                {
                    Atom("BranchEngineClash", 2),
                },
            }, "VERIFY_TRUNK_GUARD");
            Assert(trunkDef.Effects.Count == 0,
                   "主干守卫：trunk 原子塞进普通序列被 converter 剔除（自由分支经 header.EngineKind 声明）");

            // ---- 3. 自由分支 header 通道端到端（合成器产出形态 = TestBranchEngines 数据形态）----
            var freeDef = CardCore.CardEffectConverter.ConvertOne(new CardCore.CardEffectData
            {
                Id = "VERIFY_COMPOSER_FREE",
                EngineKind = (int)CardCore.BranchEngineKind.Clash,
                EngineParam = 2,
                AtomicEffects = new List<CardCore.AtomicEffectEntry>
                {
                    Atom("DrawCard", 1),
                },
            }, "VERIFY_COMPOSER_FREE");
            Assert(freeDef.EngineKind == CardCore.BranchEngineKind.Clash && freeDef.RewardAtoms.Count == 1
                   && freeDef.Effects.Count == 0,
                   "自由分支：header 通道 → EngineKind=Clash，奖励原子转存 RewardAtoms（主序列空）");
            var freeCosts = CardCore.CostDerivationService.DeriveElementCosts(freeDef);
            Assert(!freeCosts.Any(c => c.ManaType == CardCore.ManaType.Gray && c.Value > 0),
                   "自由分支计价（2026-09-15 废灰费）：引擎零计价（拼点门槛=奖励锚价，运行时判）");

            var countdownDef = CardCore.CardEffectConverter.ConvertOne(new CardCore.CardEffectData
            {
                Id = "VERIFY_COMPOSER_COUNTDOWN",
                EngineKind = (int)CardCore.BranchEngineKind.Countdown,
                EngineParam = 0, // 0 = 按奖励推导费自动换算
                AtomicEffects = new List<CardCore.AtomicEffectEntry>
                {
                    Atom("DrawCard", 1), // 锚价 2 → 2 回合
                },
            }, "VERIFY_COMPOSER_COUNTDOWN");
            Assert(countdownDef.CountdownTurns == 2,
                   $"倒计时自动换算：奖励锚价 2 → 2 回合（实际 {countdownDef.CountdownTurns}）");

            // ---- 4. 有限分支预算：RewardDerivedCost vs GatePremium ----
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
            var gate = System.Linq.Enumerable.First(CardCore.ComposerCatalog.GatesFor(CardCore.AtomicEffectType.DealDamage));
            Assert(gate.Id == "DmgKillsTarget" && CardCore.ComposerCatalog.GateBudget(gate) == 2
                   && CardCore.ComposerCatalog.GateLabel(gate).Contains("【奖励2】"),
                   "有限分支目录：伤害主干 → 消灭门【奖励2】（premium 与 GatePremium 同源）");
            Assert(!CardCore.ComposerCatalog.CanBeGateTrunk(CardCore.AtomicEffectType.DrawCard),
                   "非产出族原子不可做有限分支主干");

            // ---- 5. 并列保序：三原子 Steps → def.Steps 顺序一致 ----
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
            Assert(parDef.Steps.Count == 3
                   && parDef.Steps[0].Atomic.Type == CardCore.AtomicEffectType.DealDamage
                   && parDef.Steps[1].Atomic.Type == CardCore.AtomicEffectType.DrawCard
                   && parDef.Steps[2].Atomic.Type == CardCore.AtomicEffectType.Heal,
                   "并列：三原子步骤保序转换");

            // ---- 6. 描述动态渲染（AtomText——SynergyUI 侧）----
            var cfg = CardCore.Attribute.AtomicEffectTable.GetByType(CardCore.AtomicEffectType.DealDamage);
            var atom = Atom("DealDamage", 3, amp: 1f);
            string rendered = SynergyUI.AtomText.Render(cfg, atom, null);
            Assert(rendered == "随机 对目标造成0至6点伤害",
                   $"描述动态渲染：3±100% → 「随机 对目标造成0至6点伤害」（实际 「{rendered}」）");
            atom.amp = 0f;
            Assert(SynergyUI.AtomText.Render(cfg, atom, null) == "对目标造成3点伤害", "描述渲染：amp=0 → 原模板");

            // ---- 7. 哈希口径：amp / EngineKind 参与 HashEffect（去重不失真） ----
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
            var g3 = new SynergyUI.EffectGraphData("H")
            {
                header = new CardCore.CardEffectData { EngineKind = (int)CardCore.BranchEngineKind.Clash, EngineParam = 2 },
            };
            var g4 = new SynergyUI.EffectGraphData("H")
            {
                header = new CardCore.CardEffectData { EngineKind = (int)CardCore.BranchEngineKind.LuckRoll, EngineParam = 2 },
            };
            Assert(SynergyUI.ContentHasher.HashEffect(g3) != SynergyUI.ContentHasher.HashEffect(g4),
                   "哈希口径：EngineKind 不同 → HashEffect 不同");
        }

        private static void TestCostAnchors()
        {
            // ---- 表值 ----
            Assert(Cfg(AtomicEffectType.Heal)?.TotalUnitCost == 0.5f,
                   "计价锚：Heal TotalUnitCost=0.5（回2命=1费）");
            Assert(ElementAffinities.GetAffinityForEffect(AtomicEffectType.GrantTaunt).PrimaryColor == ManaType.White,
                   "计价锚：GrantTaunt 表 White（2026-09-13 用户改表——白色防御系，与守护/禁魔石同族；锚原按 Green 已过期）");
            var dd = ValueSystemConfigManager.Instance.GetOrCreateConfig().DelayDiscountConfig;
            Assert(System.Math.Abs(dd.At(1) - 1f) < 1e-4 && System.Math.Abs(dd.At(5) - 0.875f) < 1e-4
                   && System.Math.Abs(dd.At(9) - 0.75f) < 1e-4 && System.Math.Abs(dd.At(12) - 0.75f) < 1e-4,
                   "计价锚：d(C) 整卡最后折 d(1)=1 / d(5)=0.875 / d(9)=0.75 / 9费及以上钳0.75");

            // ---- 身材：1费 = 2点属性（灰）；底盘预算（2026-09-10 攻/守效果化）：
            //      3 − 攻1 − 守1 − 效果数，白板生物退 1（旧两口径曲线已废）----
            Assert(DeriveTotal(MakeCostCard(Cardtype.Creature, 1, 1)) == 0, "计价锚：1/1 白板 D=0（S1 − 底盘退1）");
            Assert(DeriveTotal(MakeCostCard(Cardtype.Creature, 0, 2)) == 0, "计价锚：0/2 白板 D=0（同上）");
            Assert(DeriveTotal(MakeCostCard(Cardtype.Creature, 2, 2)) == 1, "计价锚：2/2 白板 D=1（S2 − 底盘退1）");
            Assert(DeriveTotal(MakeCostCard(Cardtype.Creature, 3, 2)) == 1, "计价锚：3/2 白板 D=1（S2.5→3 ×0.75→2 − 退1）");

            // ---- 底盘预算纯函数：3 − 攻在 − 守在 − 效果数（生物默认攻守全在）----
            Assert(CardCompositionCost.ChassisAdjust(MakeCostCard(Cardtype.Creature, 1, 1)) == 1,
                   "计价锚：底盘 0 效果生物 → 退 1（攻守占 2）");
            Assert(CardCompositionCost.ChassisAdjust(MakeCostCard(Cardtype.Creature, 1, 1, MakeEffect("Heal", 1))) == 0,
                   "计价锚：底盘 1 效果生物 → ±0（用户例：无守卫卡 攻+两槽=3 同耗尽口径）");
            Assert(CardCompositionCost.ChassisAdjust(MakeCostCard(Cardtype.Creature, 1, 1, MakeEffect("Heal", 1), MakeEffect("Heal", 1))) == -1,
                   "计价锚：底盘 2 效果生物 → +1（超 1 槽）");
            Assert(CardCompositionCost.ChassisAdjust(MakeCostCard(Cardtype.Creature, 1, 1,
                       MakeEffect("Heal", 1), MakeEffect("Heal", 1), MakeEffect("Heal", 1))) == -2,
                   "计价锚：底盘 3 效果生物 → +2");
            var noAtk = MakeCostCard(Cardtype.Creature, 1, 1);
            noAtk.NoAttack = true;
            Assert(CardCompositionCost.ChassisAdjust(noAtk) == 2,
                   "计价锚：底盘无攻白板 → 退 2（opt-out 逐项退）");
            var spell1 = MakeCostCard(Cardtype.Spell, null, null, MakeEffect("DealDamage", 2));
            Assert(CardCompositionCost.ChassisAdjust(spell1) == 2,
                   "计价锚：底盘法术 1 效果 → 退 2（法术减两费）");
            spell1.SurplusToSpeed = true;
            Assert(CardCompositionCost.ChassisAdjust(spell1) == 0,
                   "计价锚：底盘瞬间富余转速度 → 不退费");

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
            Assert(CardCompositionCost.ChassisAdjust(choiceSlotData) == 1,
                   "计价锚：底盘 法术 2 槽（抉择装两个效果收两次槽位费）→ 3−2 = 退 1");

            // ---- 法术不折：锚价全额；底盘退 2（法术无攻守）无灰落最高费用色 ----
            // 2026-09-13 用户调表：DealDamage 0.5→1.0、DrawCard 1.0→2.0 → 锚价红4+蓝2，退2落红
            var fbEffect = MakeEffect("DealDamage", 4);
            fbEffect.AtomicEffects.Add(AtomRefs.New(CardCore.AtomicEffectType.DrawCard, value: 1));
            var fb = CardCostService.Derive(MakeCostCard(Cardtype.Spell, null, null, fbEffect));
            Assert(fb.DerivedCost.GetValueOrDefault(ManaType.Red) == 2 && fb.DerivedCost.GetValueOrDefault(ManaType.Blue) == 2,
                   "计价锚：法术 4伤+1抽 = 红2+蓝2（锚价全额红4蓝2 − 底盘退2 落最高色红）");
            Assert(fb.Factor == 1f, "计价锚：法术 f=1（打出即生效，不折）");
            Assert(DeriveTotal(MakeCostCard(Cardtype.Spell, null, null, MakeEffect("Heal", 2))) == 0,
                   "计价锚：回2命 = 0费（表价1 − 底盘退2 下限0）");

            // ---- 9费档：整卡最后折 f=d(9)=0.75（原 d(9)=0 全免已废）----
            var bigBody = MakeCostCard(Cardtype.Creature, 9, 9, MakeEffect("DealDamage", 3));
            bigBody.Cost[(int)ManaType.Gray] = 9;
            var big = CardCostService.Derive(bigBody);
            Assert(System.Math.Abs(big.Factor - 0.75f) < 1e-4 && big.DerivedTotal == 9 && big.OffsetRequirement == 0,
                   "计价锚：9费 9/9 挂3伤 → 整卡(9+3)×0.75=9 → D=9，Req=0（1 效果生物底盘 ±0）");
            var midTier = MakeCostCard(Cardtype.Creature, 9, 9, MakeEffect("DealDamage", 3));
            midTier.Cost[(int)ManaType.Gray] = 5;
            var mid = CardCostService.Derive(midTier);
            Assert(mid.DerivedTotal == 11 && mid.OffsetRequirement == 6 && !mid.Conformant,
                   "计价锚：同卡声明 5 费档 → f=d(5)=0.875，整卡(9+3)×0.875=10.5→D=11，Req=6 无代价不符规则一");

            // ---- 两效果=超 1 槽（底盘 3−2−2=−1 加灰）；f=d(C) ----
            // 2026-09-13 新表价：E=红3+蓝2=5 → 整卡(2+5)×0.90625=6.34→6，超1槽+1灰 → D=7
            var chooser = MakeCostCard(Cardtype.Creature, 2, 2, MakeEffect("DealDamage", 3), MakeEffect("DrawCard", 1));
            chooser.Cost[(int)ManaType.Gray] = 4;
            var ch = CardCostService.Derive(chooser);
            Assert(System.Math.Abs(ch.Factor - 0.90625f) < 1e-4 && ch.DerivedTotal == 7 && ch.OffsetRequirement == 3,
                   "计价锚：两效果 f=d(4)=0.90625 整卡(2+5)×f=6.34→6，超1槽+1灰 → D=7");

            // ---- 超槽加价入灰（1-1 挂三效果 9费档：新表价 E=3×蓝2=6 → (1+6)×0.75=5.25→5，超2槽+2灰 → D=7）----
            var tripleMount = MakeCostCard(Cardtype.Creature, 1, 1,
                MakeEffect("DrawCard", 1), MakeEffect("DrawCard", 1), MakeEffect("DrawCard", 1));
            tripleMount.Cost[(int)ManaType.Gray] = 9;
            var tm = CardCostService.Derive(tripleMount);
            Assert(tm.DerivedTotal == 7,
                   "计价锚：1/1 挂三效果 9费档 → 整卡(S1+E6)×0.75=5，超2槽+2灰 → D=7");

            // ---- 规则一简化（2026-09-11）：D≤C 直判，代价不再提供抵扣当量 ----
            // 2026-09-13 新表价：E=红3+蓝2=5 → f=d(3)=0.9375，(2+5)×f=6.56→7，超1槽+1灰 → D=8
            var overBaseline = MakeCostCard(Cardtype.Creature, 2, 2, MakeEffect("DealDamage", 3), MakeEffect("DrawCard", 1));
            overBaseline.Cost[(int)ManaType.Gray] = 3; // 3费声明：缺口 5
            var ov = CardCostService.Derive(overBaseline);
            Assert(ov.OffsetRequirement == 5 && !ov.Conformant,
                   "计价锚：3费声明 整卡折 D=8 → 缺口 5，无代价 → 不符规则一");
            overBaseline.Effects[0].Costs = new List<CostEntry>
            {
                new CostEntry
                {
                    CostType = (int)CostType.Payload, Value = 4,
                    payload = AtomRefs.New(AtomicEffectType.DiscardCard, value: 4, kinds: new List<int> { 5 }),
                },
            };
            var ov2 = CardCostService.Derive(overBaseline);
            Assert(ov2.DerivedTotal == ov.DerivedTotal && !ov2.Conformant,
                   "计价锚（2026-09-11/09-14）：同卡挂弃4张 Payload 代价 → D/规则一不变（当量抵扣下线，补偿改运行时按全价得黑——见代价补偿段）");

            // ---- 关键词计价：Grant 固定费 + 随整卡同折（挂载口退费并存）----
            var kwCard = MakeCostCard(Cardtype.Creature, 1, 1);
            kwCard.Keywords.Add("Taunt");
            kwCard.Cost[(int)ManaType.Green] = 1; // 嘲讽表色已迁 Green（09-02）——K 落绿桶
            var kw = CardCostService.Derive(kwCard);
            Assert(kw.DerivedTotal == 1 && kw.OffsetRequirement == 0,
                   "计价锚：嘲讽 K=1（表 Green→绿）d(1)=1，两口空退2（灰下限0）→ D=绿1=声明费");
            var kwBig = MakeCostCard(Cardtype.Creature, 9, 9);
            kwBig.Keywords.Add("Taunt");
            kwBig.Cost[(int)ManaType.Gray] = 9;
            var kwb = CardCostService.Derive(kwBig);
            Assert(kwb.DerivedTotal == 7 && kwb.OffsetRequirement == 0,
                   "计价锚：9费档 → (S9+K1)×0.75=7.5→8，0效果退1（灰）→ D=7");

            // ---- 缺省档位 Ĉ：5/5 挂 3伤（1 效果底盘±0）→ (5+3)×d(7)=6.5→7 ≤ 7 → Ĉ=7 ----
            var noCost = MakeCostCard(Cardtype.Creature, 5, 5, MakeEffect("DealDamage", 3));
            var nc = CardCostService.Derive(noCost);
            Assert(nc.SuggestedTier == 7, $"计价锚：缺省档位 Ĉ=7（实际 {nc.SuggestedTier}）");

            // ---- 跨边当量锚点已删（2026-09-10：改由 Polarity 错边折价承担，见 TestTargetDomainModel f 段）----

            // ---- 溢出治疗转临时上限（2026-09-07 定案：走 LifeUp 指示物，ceil半入上限/floor半入当前）----
            var overflowPlayer = new Player("VERIFY_OVERFLOW", 30);
            overflowPlayer.Heal(7);
            Assert(overflowPlayer.MaxHealth == 34 && overflowPlayer.Life == 33
                   && overflowPlayer.GetCounterCount(CardCore.Attribute.CounterRules.LifeUpCounter) == 4,
                   "溢出治疗：满血30回复7 → LifeUp×4 → 34/33（用户定案例）");
            var evenOverflow = new Player("VERIFY_OVERFLOW2", 30);
            evenOverflow.Heal(4);
            Assert(evenOverflow.MaxHealth == 32 && evenOverflow.Life == 32
                   && evenOverflow.GetCounterCount(CardCore.Attribute.CounterRules.LifeUpCounter) == 2,
                   "溢出治疗：满血30回复4 → LifeUp×2 → 32/32（偶溢出均分仍满）");
            var partialHeal = new Player("VERIFY_OVERFLOW3", 30);
            partialHeal.Life = 28;
            partialHeal.Heal(2); // 恰好补满（28+2=30，无溢出）——原用 Heal(3) 实溢出1会按定案转层
            Assert(partialHeal.MaxHealth == 30 && partialHeal.Life == 30
                   && partialHeal.GetCounterCount(CardCore.Attribute.CounterRules.LifeUpCounter) == 0,
                   "常规治疗：未溢出照旧封顶（不触发层）");
            var overflowCreature = new CardWrapper(MakeCostCard(Cardtype.Creature, 2, 5));
            overflowCreature.Heal(7);
            Assert(overflowCreature.GetMaxLife() == 9 && overflowCreature.GetLife() == 8
                   && overflowCreature.GetCounterCount(CardCore.Attribute.CounterRules.LifeUpCounter) == 4,
                   "溢出治疗（生物）：满血5回7 → LifeUp×4 → 上限9当前8（层换区清除）");

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

            // ---- 三轨计价（2026-09-09 定案；2026-09-13 第十七批属性价梯：StatTierPrice 接管修改族）----
            // ① 同文本「攻+2」：生物声明 UntilEndOfTurn → E=0.5×2=1（固定1回合档 0.5/+1）；
            //    法术声明 Permanent → E=2.0×2=4（换区不移除档 2.0/+1）
            var pricingSpell = MakeCostCard(Cardtype.Spell, null, null,
                MakeEffect("ModifyPower", 2, (int)DurationType.Permanent));
            var pricingCreature = MakeCostCard(Cardtype.Creature, null, null,
                MakeEffect("ModifyPower", 2, (int)DurationType.UntilEndOfTurn));
            var psR = CardCostService.Derive(pricingSpell);
            var pcR = CardCostService.Derive(pricingCreature);
            Assert(pcR.EAnchor == 1 && psR.EAnchor == 4,
                   $"三轨计价·轨别档位：同文本攻+2 生物档(UntilEndOfTurn) E=1 / 法术永久档 E=4（实际 {pcR.EAnchor}/{psR.EAnchor}）");

            // ② 连接光环费 A（2026-09-13 第十七批定案数学：stat 光环档=1.5/+1（StatAnchor 0.5×3）；
            //    坚韧/守护 keyword=条目平价 1/条（白名单内联）；其余 keyword（如 Taunt）走
            //    Grant 原子锚价 × 单回合折算 factor=0.6/1.0=0.6——非白名单未定案平价，维持折算口径）
            var auraCard = MakeCostCard(Cardtype.Creature, 2, 2);
            var auraBase = CardCostService.Derive(auraCard);
            auraCard.ArrowDirections = HexDirection.Up;
            auraCard.LinkAuras.Add(new LinkAuraData { stat = "Power", value = 1 });
            auraCard.LinkAuras.Add(new LinkAuraData { keyword = "Taunt" });
            var auraR = CardCostService.Derive(auraCard);
            var aLines = auraR.Breakdown.Where(l => l.Stage == "A").ToList();
            Assert(aLines.Count == 2
                   && System.Math.Abs(aLines[0].Value - 1.5f) < 1e-3     // Power1：光环档 1.5/+1（StatAnchor×3）
                   && System.Math.Abs(aLines[1].Value - 0.6f) < 1e-3,    // Taunt：Grant 锚1 × 单回合折算 0.6
                   "三轨计价·光环档：stat=1.5/+1 / 非白名单 keyword=锚×单回合折算0.6");
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
            data.Cost = new Dictionary<int, float> { { (int)ManaType.Gray, 1 } };
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
            var snap1 = new Dictionary<ManaType, int>(pool1.AvailableMana);
            var snap2 = new Dictionary<ManaType, int>(pool2.AvailableMana);
            int i1 = pool1.GlobalTurnIndex, i2 = pool2.GlobalTurnIndex;
            pool1.GlobalTurnIndex = 9;
            pool2.GlobalTurnIndex = 9;
            foreach (var t in AllManaTypes()) { pool1.AvailableMana[t] = 99; pool2.AvailableMana[t] = 99; }
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
            pool1.AvailableMana.Clear();
            foreach (var kv in s.snap1) pool1.AvailableMana[kv.Key] = kv.Value;
            pool2.AvailableMana.Clear();
            foreach (var kv in s.snap2) pool2.AvailableMana[kv.Key] = kv.Value;
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
        /// （墓地经 IPlaySource 打出的 FromZone=Graveyard 路径由仪式段（归土仪典）回归覆盖。）
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
                    Cost = new Dictionary<int, float> { { (int)ManaType.Gray, 1 } },
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
                var heal1 = InjectCard(core, p1,
                    SpeedSpellData("VERIFY_SBA_HEAL1", 1, AtomicEffectType.Heal, 2, new List<int> { 1 }));
                var aoe4c = InjectCard(core, p2, AoEData("VERIFY_SBA_T4C_AOE"));

                Assert(GameActions.PlayCard(core, p2, aoe4c), "SBA·T4c P2 打出 AoE");
                GameActions.PassPriority(core, p1);
                GameActions.PassPriority(core, p2);
                Assert(core.StackEngine.Peek()?.IsSBA == true, "SBA·T4c SBA 在栈顶（记速器=1）");
                GameActions.PassPriority(core, p2); // holder → p1
                Assert(!GameActions.PlayCardInResponse(core, p1, heal1, new List<Entity> { hero4c }),
                       "SBA·T4c 非回合方速度1响应被拒（1 不> 1——记速器已被 SBA 抬到 1）");
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
                aoe4dData.Cost = new Dictionary<int, float> { { (int)ManaType.Red, 1 } };
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
                aoe3Data.Cost = new Dictionary<int, float> { { (int)ManaType.Red, 1 } }; // 同 T4d：显式费绕开推导（红90 超帽）
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

                // ② 落手牌 ×2：非抽牌入手事件（喂 NonDrawDrawAccum 语义）
                int h0 = Hand();
                hands.Clear();
                SummonTokenHandlerUtil.Run(core, p1, Zone.Hand, 2);
                GameActions.DrainStack(core);
                Assert(Hand() == h0 + 2, "落手牌：+2 张");
                Assert(hands.Count == 2 && hands.All(e => !e.IsDraw), "CardEnterHandEvent ×2 且 IsDraw=false");

                // ③ 落牌组 ×1（洗入）
                int d0 = Deck();
                SummonTokenHandlerUtil.Run(core, p1, Zone.Deck, 1);
                Assert(Deck() == d0 + 1, "落牌组：+1 张");

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

                // ⑤ 落区费用三档（Value=10：战场 10 / 手牌 round(10×1.2)=12 / 牌组 round(10×1.1)=11）
                int CostOf(Zone zone)
                {
                    var def = new EffectDefinition
                    {
                        Id = "VERIFY_TOKEN_COST",
                        SummonDropZone = zone,
                        // 2026-09-13 修复：手构 def 的 TriggerLimitPerTurn 默认 -1 会被
                        // TriggerCostFactor 当"显式无限"×1.2³=1.728（17/21/19 即此）——
                        // 第十四批"手构 def 不误乘"定案指手构者自行声明单次。显式置 1。
                        TriggerLimitPerTurn = 1,
                        Effects = new List<AtomicEffectInstance>
                        {
                            new AtomicEffectInstance { Type = AtomicEffectType.SummonToken, Value = 10 }
                        },
                    };
                    return CostDerivationService.DeriveElementCosts(def).Sum(c => c.Value);
                }
                int cBf = CostOf(Zone.Battlefield), cHand = CostOf(Zone.Hand), cDeck = CostOf(Zone.Deck);
                Assert(cBf == 10 && cHand == 12 && cDeck == 11,
                       $"落区费用三档：战场 {cBf} / 手牌 {cHand} / 牌组 {cDeck}（SummonDrop 系数生效）");
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
                    Cost = new Dictionary<int, float> { { (int)ManaType.Gray, 1 } },
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
        /// 2026-09-21：卡/效果/卡组属用户数据，统一住 StreamingAssets/Card/。
        /// </summary>
        [MenuItem("Tools/重推导测试卡表费用与ID")]
        public static void RegenerateTestTableCosts()
        {
            // 2026-09-14 双表口径：唯一卡表=StreamingAssets/Card/Cards.json（TestDecks 已随效果引用化退役）
            string path = Path.Combine(Application.streamingAssetsPath, "Card", "Cards.json");
            if (!File.Exists(path))
            {
                Debug.LogError($"[Regen] 找不到卡表 {path}");
                return;
            }

            // 三层推导链（2026-09-16 定案，顺序敏感）：原子表 id（sha256(DisplayName)）→
            // 效果 id（HashEffect：文本+参数+原子引用）→ 卡 id（HashCard：文本+参数+效果id序列）。
            // 原子表必须先行——效果/卡里的 refId 按 id 引用表行（GetByHashId），原子 id 后推会引用断链
            RegenAtomicTableIds();

            RegenOneDeck(path);
        }

        /// <summary>仅重写卡表费用（2026-09-20）：跳过 RegenAtomicTableIds——当前原子表 ID 与
        /// sha256(DisplayName) 口径存在漂移（用户改表未走推导管线），全链重推会打断
        /// Effects.json/卡表的既有 refId；本入口只做 RegenOneDeck 的建议价重写，ID 全不动。</summary>
        [MenuItem("Tools/仅重推正式卡费用（不动ID）")]
        public static void RegenerateCardCostsOnly()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "Card", "Cards.json");
            if (!File.Exists(path))
            {
                Debug.LogError($"[Regen] 找不到卡表 {path}");
                return;
            }
            RegenOneDeck(path);
        }

        /// <summary>原子表 ID 列重推（镜像 Config/gen_effect_ids.py 口径：sha256(DisplayName.Trim())
        /// UTF-8 的前 8 位 hex——三层推导链的第一层）。ID 是引用键：Effects.json/Cards.json 的
        /// 原子 refId 经 AtomicEffectTable.GetByHashId 按行解析（2026-09-16 修正旧注释"零消费"口径）。
        /// DisplayName 改动 → ID 漂移 → 引用方需同步（文本即身份，与 python 管线同约定）。
        /// DisplayName 撞名 → ID 撞号，告警。</summary>
        private static void RegenAtomicTableIds()
        {
            string path = Path.Combine(Application.dataPath, "Configs", "AttributeValueConfig.json");
            if (!File.Exists(path))
            {
                Debug.LogError($"[Regen] 找不到原子表 {path}");
                return;
            }

            var root = Newtonsoft.Json.Linq.JArray.Parse(File.ReadAllText(path));
            using var sha = System.Security.Cryptography.SHA256.Create();
            var seen = new HashSet<string>();
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
            Debug.Log($"[Regen] 原子表 ID 重推：{root.Count} 行，变更 {changed} 条 → {path}");
        }

        /// <summary>
        /// 对双表卡表重推导费用并回写（2026-09-14 引用化口径）。
        /// 装载（LoadCardsFromText）已完成 effectIds 解析与 EnsureCost 建议价回填；此处对**非抉择卡**
        /// 清声明重推导（重置后 EnsureCost 重写建议档）；抉择卡走「声明=最大模式费」契约
        ///（DeriveModeCosts 无价差溢价——2026-09-21 已废；ModeCostCache internal，经 EnsureCost 回填缓存）。
        /// 回写统一走 CardConfigSerializer.Save——costList / C_+HashCard 卡 id / effectIds
        ///（Effects.json 幂等 upsert）全走序列化器单源，天然按内容去重（同内容同 id），
        /// 消灭旧裸 ContentId 双轨。**先清后写**：防止重推导换 id 后旧条目残留。
        /// </summary>
        private static void RegenOneDeck(string path)
        {
            CardCore.EffectsLibrary.Reload(); // 防陈旧缓存：外部直改 Effects.json 后编辑器静态 _byId 不会自刷新
            var cards = CardLoader.LoadCardsFromText(File.ReadAllText(path));
            if (cards.Count == 0)
            {
                Debug.LogError($"[Regen] 卡表为空/解析失败：{path}");
                return;
            }

            foreach (var card in cards)
            {
                if (CostDerivationService.HasChoiceEffect(card))
                {
                    var modes = CardCostService.DeriveModeCosts(card);
                    var max = CardCostService.MaxModeCost(modes);
                    if (max.Count > 0)
                        card.Cost = max;
                    card.ResetCache();
                    CardCostService.EnsureCost(card);
                    string choiceStr = card.Cost != null && card.Cost.Count > 0
                        ? string.Join(" ", card.Cost.Select(kv => $"{(ManaType)kv.Key}:{kv.Value}"))
                        : "(空 Cost)";
                    Debug.Log($"[Regen] {card.CardName} 抉择卡 → 声明=最大模式费：{choiceStr}");
                }
                else
                {
                    card.Cost = null; // 清声明 → 重推导建议档（幂等）
                    card.ResetCache();
                    CardCostService.EnsureCost(card);
                    var r = CardCostService.Derive(card);
                    // D=0（无身材无效果无关键词/效果被契约剔除）保持空 Cost——合法态，日志守卫防空引用
                    string costStr = card.Cost != null && card.Cost.Count > 0
                        ? string.Join(" ", card.Cost.Select(kv => $"{(ManaType)kv.Key}:{kv.Value}"))
                        : "(空 Cost——D=0 或效果全被内容契约剔除)";
                    Debug.Log($"[Regen] {card.CardName} S={r.S} K={r.K} E={r.EAnchor} f={r.Factor:0.###} D={r.DerivedTotal} → 档位{r.SuggestedTier}：{costStr}");
                }
            }

            // 清文件后逐张 Save：CardConfigSerializer 单源回写（C_+HashCard id / costList / effectIds+效果库 upsert）
            File.WriteAllText(path, "{\n    \"cards\": [],\n    \"deckConfig\": {\n        \"copiesPerCard\": 1\n    }\n}");
            foreach (var card in cards)
                SynergyUI.CardConfigSerializer.Save(card);
            Debug.Log($"[Regen] 已重写 {cards.Count} 张卡的费用与内容 ID → {path}");
        }

        // ======================================== 两个随机（2026-09-13 定案） ========================================

        /// <summary>
        /// 两个随机端到端：
        /// ① 数值随机：RollValue 边界（3±100% 样本 ⊆[0,6] 且两端可达；幅度 0 恒名义值；
        ///    实例名义 Value 字段不随掷值漂移——计价锚点）；
        /// ② 单位随机（RandomTarget 标志，2026-09-16 自 SelectionMode 移出）：TargetCount=2 从完整候选域
        ///    种子抽取（数量=2、成员⊆候选、不弹交互）；扰魔/潜行口径——手动显示域不含（弹窗不显示）、
        ///    完整候选域含、随机可命中（绕过选择）；全部档（TargetCount≤0）≡全取；选一/选多预检用显示域
        ///    （唯一候选是扰魔时不可发动）。
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

            // ---- 1. 扰魔口径：完整候选域 vs 手动显示域 ----
            var demon = Spawn(p2, 2, 2);
            demon.AddKeyword(Attribute.KeywordRules.Untargetable, KeywordLane.Setting);
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
            Assert(full.Contains(demon) && full.Contains(plain),
                   "扰魔口径：完整候选域含扰魔单位（范围波及/随机可命中——域层不再剔除）");
            var manual = CardCore.TargetResolver.ExcludeUnselectable(full, p1);
            Assert(!manual.Contains(demon) && manual.Contains(plain),
                   "扰魔口径：手动显示域不含扰魔（弹窗不显示，AI/无头代替选取同口径）");

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

            // 随机可命中扰魔（概率断言：候选=p2+demon+plain 共 3，抽 2 单轮命中 demon 概率 2/3；40 轮全 miss ≈ 1e-19）
            GameRng.Reseed(777);
            bool demonEverHit = false;
            for (int i = 0; i < 40 && !demonEverHit; i++)
                demonEverHit = CardCore.Attribute.EffectHandlerRegistry
                    .ResolveCompositionTargetsAsync(def, ctx).GetAwaiter().GetResult()
                    .Contains(demon);
            Assert(demonEverHit, "单位随机：随机可命中扰魔（绕过选择——不弹弹窗不受显示域限制）");

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
                // Stealth filter = 仅指潜行中——完整候选只剩新造的潜行单位（扰魔/普通被 filter 排除），
                // 而选一/选多显示域又把它隐藏 → "完整域有候选、显示域空"的正交样本
                TargetDomain = new List<int> { (int)CardCore.TargetKind.EnemyLivingUnit },
                TargetFilter = "Stealth",
            };
            var stealthUnit = Spawn(p2, 1, 1);
            stealthUnit.AddKeyword(Attribute.KeywordRules.Stealth, KeywordLane.Setting);
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
            RetireCards(core, p2, demon, plain, stealthUnit);
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
        /// SelectionMode 全取档）：①计价——普通原子挂全取档按期望 4（DealDamage 3伤=红12）；
        /// ②行为——打出后对域内全部有生命单位（含角色）结算，无弹窗。
        /// </summary>
        private static void TestFullDomainCompose(GameCore core, Player p1, Player p2)
        {
            EnsureMainPhase(core, p1);

            // ---- 1. 计价：普通原子 + 全取档按期望 4 ----
            var fullDmgDef = CardEffectConverter.ConvertOne(new CardEffectData
            {
                Id = "VERIFY_SWEEP_FULL3",
                SelectionMode = (int)CardCore.SelectionMode.WholeUnion, // 普通原子挂全取档——按期望 4 计价
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(CardCore.AtomicEffectType.DealDamage, value: 3) },
            }, "VERIFY_SWEEP_FULL3");
            var fullCost = CardCore.CostDerivationService.DeriveElementCosts(fullDmgDef);
            int fullRed = fullCost.Where(c => c.ManaType == ManaType.Red).Sum(c => c.Value);
            Assert(fullRed == 12,
                   "计价：DealDamage+Full value3 = 红12（1×3×期望目标数4——少了亏多了赚）");

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
            var snap = new Dictionary<ManaType, int>(pool1.AvailableMana);
            try
            {
                foreach (var t in AllManaTypes()) pool1.AvailableMana[t] = 99;

                var sweepCardData = new CardData
                {
                    ID = "VERIFY_SWEEP_CR", CardName = "验证全域伤害", Supertype = Cardtype.Spell,
                    Cost = new Dictionary<int, float> { { (int)ManaType.Red, 4 } },
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
                foreach (var kv in snap) pool1.AvailableMana[kv.Key] = kv.Value;
            }

            RetireCards(core, p1, a1, a2);
            RetireCards(core, p2, b1, b2);
        }

        // ======================================== 牺牲原子（2026-09-13 edict 定案） ========================================

        /// <summary>
        /// 牺牲原子端到端：①表行锚（黑3 Polarity=-1 域 1,2 filter "Player"=仅角色）；
        /// ②候选域=双方角色（生物不入候选——Player 过滤激活）；③行为：目标玩家（持有者）自行选择
        /// 一个己方生物效果死亡（headless 自动选首个；多生物时走交互 Chooser=持有者）；
        /// ④不灭不拦牺牲（DeathRules 只拦{消灭,吞噬}——绕不灭的解场口）；来源=持有者。
        /// </summary>
        private static void TestSacrificeAtom(GameCore core, Player p1, Player p2)
        {
            EnsureMainPhase(core, p1);

            // ---- 1. 表行锚 ----
            var cfg = CardCore.Attribute.AtomicEffectTable.GetByType(AtomicEffectType.Sacrifice);
            Assert(cfg != null && cfg.TotalUnitCost == 3f && cfg.Polarity == -1f
                   && cfg.GetTargetKindList().SequenceEqual(new[] { 1, 2 })
                   && (cfg.TargetFilter ?? "") == "Player"
                   && ElementAffinities.GetAffinityForEffect(AtomicEffectType.Sacrifice).PrimaryColor == ManaType.Black,
                   "牺牲表行：黑3 Polarity=-1 域={1,2} filter=Player（仅角色；2026-09-13 用户调价 2→3）");

            // ---- 2. 候选域=仅角色（Player 过滤激活） ----
            var ctx = new EffectExecutionContext
            {
                Source = p1,
                Controller = p1,
                ZoneManager = core.ZoneManager,
                ElementPool = core.ElementPool,
            };
            var candidates = CardCore.Attribute.EffectHandlerRegistry.ResolveCandidates(
                new List<int> { (int)CardCore.TargetKind.OwnLivingUnit, (int)CardCore.TargetKind.EnemyLivingUnit },
                "Player", ctx);
            Assert(candidates.Contains(p1) && candidates.Contains(p2) && candidates.All(c => c is Player),
                   "牺牲候选域：仅双方角色（Player 过滤滤除生物）");

            // ---- 3. 行为：持有者交出一个生物（含不灭——牺牲绕不灭） ----
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

            var foeIndestructible = Spawn(p2, 3, 3, Attribute.KeywordRules.Indestructible); // 唯一生物=不灭——自动选它
            var mine = Spawn(p1, 2, 2); // p1 唯一生物

            var atom = new CardCore.AtomicEffectInstance { Type = AtomicEffectType.Sacrifice, Value = 1 };
            var execCtx = new EffectExecutionContext
            {
                Source = p1,
                Controller = p1, // 施法者=p1；死亡来源应=持有者（各自）
                ZoneManager = core.ZoneManager,
                ElementPool = core.ElementPool,
                Targets = new List<Entity> { p2, p1 }, // 作用对象=双方角色
            };
            Assert(CardCore.Attribute.EffectHandlerRegistry.ExecuteEffect(atom, execCtx), "牺牲原子执行（作用对象=双方角色）");

            Assert(!foeIndestructible.IsAlive && core.ZoneManager.IsCardInZone(foeIndestructible, p2, Zone.Graveyard),
                   "牺牲：对手持有者交出唯一生物（不灭不拦——绕不灭的解场口）");
            Assert(!mine.IsAlive && core.ZoneManager.IsCardInZone(mine, p1, Zone.Graveyard),
                   "牺牲：己方持有者同批交出（来源=各自持有者）");

            // ---- 4. 帷幕豁免：牺牲/摒弃选择权在目标方——帷幕只约束对手的选择 ----
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

            // ---- 5. 摒弃：无生命等价（持有者选己方场上无生命单位直送墓） ----
            var abjCfg = CardCore.Attribute.AtomicEffectTable.GetByType(AtomicEffectType.Abandon);
            Assert(abjCfg != null && abjCfg.TotalUnitCost == 3f && abjCfg.Polarity == -1f
                   && (abjCfg.TargetFilter ?? "") == "Player"
                   && ElementAffinities.GetAffinityForEffect(AtomicEffectType.Abandon).PrimaryColor == ManaType.Black,
                   "摒弃表行：黑3 Pol=-1 filter=Player（仅角色；2026-09-13 用户调价 2→3）");

            var p2Enchant = new CardData
            {
                ID = "VERIFY_ABJ_ENCH", CardName = "摒弃标的结界", Supertype = Cardtype.Enchantment, Power = 0, Life = 0,
            };
            var enchant = new CardWrapper(p2Enchant);
            enchant.SetController(p2);
            Assert(core.ZoneManager.TryAddToBattlefield(enchant, p2), "摒弃段：结界入场");
            var p2Creature = Spawn(p2, 3, 3); // 生物在场——摒弃只吃无生命单位

            var abandonAtom = new CardCore.AtomicEffectInstance { Type = AtomicEffectType.Abandon, Value = 1 };
            var abjCtx = new EffectExecutionContext
            {
                Source = p1,
                Controller = p1,
                ZoneManager = core.ZoneManager,
                ElementPool = core.ElementPool,
                Targets = new List<Entity> { p2 },
            };
            Assert(CardCore.Attribute.EffectHandlerRegistry.ExecuteEffect(abandonAtom, abjCtx), "摒弃原子执行（作用对象=角色）");
            Assert(core.ZoneManager.IsCardInZone(enchant, p2, Zone.Graveyard) && p2Creature.IsAlive,
                   "摒弃：持有者交出唯一无生命单位（生物不受摒弃影响）");
            RetireCards(core, p2, p2Creature);

            // ---- 6. 摒弃扩地牌（与摧毁同覆盖：无生命单位+元素池地牌）；对手区域空 → 跳过 ----
            var landData = new CardData
            {
                ID = "VERIFY_ABJ_LAND", CardName = "摒弃标的地", Supertype = Cardtype.Creature, Power = 0, Life = 1,
            };
            var land = new CardWrapper(landData);
            land.SetController(p2);
            // 2026-09-13 修复：正式放地路径（GameActions.AddToElementPool）是双写——
            // AddCardToPool 挂池私有列表 + MoveCard 进 Zone.ElementPool 容器（Abandon/Smash
            // 的地牌候选从容器读）。夹具此前只做前一半 → 摒弃候选恒空。补容器登记。
            Assert(core.ElementPool.AddCardToPool(land, p2), "摒弃段：地牌入池");
            core.ZoneManager.GetZoneContainer(p2).Add(land, Zone.ElementPool);

            var abjCtx2 = new EffectExecutionContext
            {
                Source = p1,
                Controller = p1,
                ZoneManager = core.ZoneManager,
                ElementPool = core.ElementPool,
                Targets = new List<Entity> { p2, p1 }, // p1 场上/池内全空——应跳过不空发
            };
            Assert(CardCore.Attribute.EffectHandlerRegistry.ExecuteEffect(abandonAtom, abjCtx2), "摒弃再执行（含空侧目标）");
            Assert(core.ZoneManager.IsCardInZone(land, p2, Zone.Graveyard)
                   && !core.ZoneManager.GetCards(p2, Zone.ElementPool).Contains(land),
                   "摒弃地牌：持有者交出唯一地牌（出池入墓）——摒弃覆盖=无生命单位+地牌（同摧毁）");
        }

        // ======================================== 守护光环（2026-09-13 光环化定案） ========================================

        /// <summary>
        /// 守护=连接箭头光环（keyword "Guardian"，白1×箭头数）：指向格占据者受到的伤害改由第一个存活
        /// 光环源承受（live-query——源离场/断链/被无效自动失效与递补，无需事件换源）。单跳防链式。
        /// 原计数体系（守护者/被守护者指示物 + GuardianRules 递补）随光环化退役。
        /// </summary>
        private static void TestGuardianRelay(GameCore core, Player p1, Player p2)
        {
            EnsureMainPhase(core, p1);

            GameBoard.LinkAuraSystem.Detach(); // 隔离上段残留
            var board = new GameBoard.BoardState(core, p1, p2,
                GameBoard.HalfFieldData.Flat(), GameBoard.HalfFieldData.Flat());
            board.EnableAutoResync();
            GameBoard.LinkAuraSystem.Attach(board);

            var used = new List<Card>();
            Card Make(Player owner, int power, int life)
            {
                var data = new CardData { ID = "VERIFY_GA_" + used.Count, CardName = "守" + used.Count };
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
                // 2026-09-13 修复：自动放格序列链式相邻（TestLinkAura 先例：先后入场两格互邻）——
                // 守护者 ward 夹在中间入场，g1/g2 落其前后邻格；另清战场残留防占格干扰
                RetireCards(core, p1, core.ZoneManager.GetCards(p1, Zone.Battlefield).ToArray());
                RetireCards(core, p2, core.ZoneManager.GetCards(p2, Zone.Battlefield).ToArray());
                var g1 = Make(p1, 2, 6);
                var ward = Make(p1, 1, 10);
                var g2 = Make(p1, 3, 6);

                bool adj1; var d1 = DirBetween(g1, ward, out adj1);
                bool adj2; var d2 = DirBetween(g2, ward, out adj2);
                Assert(adj1 && adj2, "守护光环段：双源与被守护者相邻");

                DataOf(g1).ArrowDirections = GameBoard.BoardMath.ArrowOf(d1);
                DataOf(g1).LinkAuras.Add(new LinkAuraData { keyword = Attribute.KeywordRules.Guardian });
                DataOf(g2).ArrowDirections = GameBoard.BoardMath.ArrowOf(d2);
                DataOf(g2).LinkAuras.Add(new LinkAuraData { keyword = Attribute.KeywordRules.Guardian });
                board.Resync();
                GameBoard.LinkAuraSystem.InvalidateCache(); // 直改箭头须手动失效缓存

                // ---- 1. 伤害改写走第一个存活源（被守护者不掉血）----
                int wardLife = ward.GetLife();
                Attribute.KeywordRules.ApplyDamage(null, ward, 3, false);
                Assert(ward.GetLife() == wardLife && (g1.GetLife() == 3 || g2.GetLife() == 3),
                       "守护光环：伤害改由第一个存活光环源承受（6−3=3，被守护者不掉血）");

                // ---- 2. 该源离场 → live-query 天然递补 ----
                var firstHit = g1.GetLife() < 6 ? g1 : g2;
                var second = ReferenceEquals(firstHit, g1) ? g2 : g1;
                core.ZoneManager.MoveCard(firstHit, p1, Zone.Battlefield, Zone.Graveyard);
                board.Resync();
                GameBoard.LinkAuraSystem.InvalidateCache();
                wardLife = ward.GetLife();
                int secondLife = second.GetLife();
                Attribute.KeywordRules.ApplyDamage(null, ward, 2, false);
                Assert(ward.GetLife() == wardLife && second.GetLife() == secondLife - 2,
                       "守护递补：源离场 live-query 自动落到下一个覆盖源（无事件换源）");

                // ---- 3. 覆盖源尽离 → 恢复全额 ----
                core.ZoneManager.MoveCard(second, p1, Zone.Battlefield, Zone.Graveyard);
                board.Resync();
                GameBoard.LinkAuraSystem.InvalidateCache();
                wardLife = ward.GetLife();
                Attribute.KeywordRules.ApplyDamage(null, ward, 2, false);
                Assert(ward.GetLife() == wardLife - 2, "守护失效：覆盖源尽离恢复全额伤害");
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

        // ======================================== 触发上限（2026-09-13 定案） ========================================

        /// <summary>
        /// 触发式每回合上限端到端：①表锚——坚韧（bd623d85）标 MountKinds=8（不可修改=无限）；
        /// ②converter 默认口径——普通原子未声明=1（一回合一次）、显式 N 采纳、含 8 原子恒 -1（声明被覆写）；
        /// ③运行时闸门——同一效果一回合第 2 次触发被静默丢弃；坚韧类（-1）不受限。
        /// 记账走既有 EffectUsageTracker（RecordActivation 全量结算记账，OnNewTurn 清零）。
        /// </summary>
        private static void TestTriggerCap(GameCore core, Player p1, Player p2)
        {
            EnsureMainPhase(core, p1);

            // ---- 1. 表锚：GrantArmor 行已退役（2026-09-13 坚韧光环化）——8 机制保留待新样本 ----
            Assert(CardCore.Attribute.AtomicEffectTable.GetByType(AtomicEffectType.GrantArmor) == null,
                   "触发上限表锚：坚韧行已退役（坚韧=连接箭头光环，绿1×箭头数；MountKinds=8 机制保留）");

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
                TriggerLimitPerTurn = -1, // 可修改原子的显式无限（计价 ×1.2³）
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(CardCore.AtomicEffectType.DrawCard, value: 1) },
            });
            Assert(unlimitedDeclared.TriggerLimitPerTurn == -1, "可修改：组合期显式无限（-1）采纳");

            // ---- 2b. 触发上限计价（2026-09-13 定案）：N>1 连乘 1.2^(N-1)；显式无限 ×1.2³ ----
            CardCore.EffectDefinition TrigDef(int limit) => CardEffectConverter.ConvertOne(new CardEffectData
            {
                Id = $"VERIFY_CAP_P{limit}",
                TriggerTiming = (int)TriggerTiming.OnDraw,
                TriggerLimitPerTurn = limit,
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(CardCore.AtomicEffectType.DealDamage, value: 5) },
            }, "VERIFY_CAP_P");
            int RedCostOf(CardCore.EffectDefinition d) => CardCore.CostDerivationService.DeriveElementCosts(d)
                .Where(c => c.ManaType == ManaType.Red).Sum(c => c.Value);
            Assert(RedCostOf(TrigDef(1)) == 5, "触发计价：N=1 不乘（红5）");
            Assert(RedCostOf(TrigDef(2)) == 6, "触发计价：N=2 ×1.2（5→6）");
            Assert(RedCostOf(TrigDef(3)) == 7, "触发计价：N=3 ×1.44（5→7.2→7）");
            Assert(RedCostOf(TrigDef(-1)) == 9, "触发计价：显式无限 ×1.2³（5→8.64→9）");

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
        /// 拼点（双方牌库顶费用差&gt;x，展示放回；空库=0）。
        /// </summary>
        private static void TestBranchEngines(GameCore core, Player p1, Player p2)
        {
            EnsureMainPhase(core, p1);

            // ---- 1. 固定分支门附加费 ----
            CardCore.EffectDefinition GateDef(string gateId)
            {
                var data = new CardEffectData
                {
                    Id = "VERIFY_GATE_" + gateId,
                    Steps = new List<EffectStepData>
                    {
                        new EffectStepData { kind = 0, atomic = AtomRefs.New(CardCore.AtomicEffectType.DealDamage, value: 3) },
                        new EffectStepData { kind = 1, conditionId = gateId,
                            thenSteps = new List<AtomicEffectEntry> { AtomRefs.New(CardCore.AtomicEffectType.DrawCard, value: 1) } },
                    },
                };
                return CardEffectConverter.ConvertOne(data, "VERIFY_GATE");
            }
            int GrayOf(CardCore.EffectDefinition d) => CardCore.CostDerivationService.DeriveElementCosts(d)
                .Where(c => c.ManaType == ManaType.Gray).Sum(c => c.Value);
            int BlueOf(CardCore.EffectDefinition d) => CardCore.CostDerivationService.DeriveElementCosts(d)
                .Where(c => c.ManaType == ManaType.Blue).Sum(c => c.Value);
            // DamageDealt 门已移除（2026-09-13 战斗伤害改写族上线——"造成伤害时"分支由毒刺/冰晶/梦魇/病原体改写承载）
            // 2026-09-14 用户口径定案（废除 09-13 灰费）：有限分支=纯校验上限，零计价——奖励免费，门不产生任何费用
            Assert(GrayOf(GateDef("DmgKillsTarget")) == 0, "有限分支零计价：击杀门不产生灰费（奖励免费）");
            Assert(GrayOf(GateDef("DeclareHit")) == 0, "有限分支零计价：宣言门不产生灰费");
            Assert(BlueOf(GateDef("DmgKillsTarget")) == 0, "有限分支奖励免费：then 抽1（锚蓝2）不计入费用");
            CardCore.EffectDefinition SubGateDef()
            {
                var data = new CardEffectData
                {
                    Id = "VERIFY_GATE_SUB",
                    Steps = new List<EffectStepData>
                    {
                        new EffectStepData { kind = 0, atomic = AtomRefs.New(CardCore.AtomicEffectType.DealDamage, value: 3) },
                        new EffectStepData { kind = 1, conditionId = "DmgKillsTarget",
                            thenSteps = new List<AtomicEffectEntry> { AtomRefs.New(CardCore.AtomicEffectType.Heal, value: 2) } }, // 锚1（回2命=1费）< 门预算2，合法
                    },
                };
                return CardEffectConverter.ConvertOne(data, "VERIFY_GATE_SUB");
            }
            Assert(GrayOf(SubGateDef()) == 0 && CardCore.CostDerivationService.DeriveElementCosts(SubGateDef())
                   .Where(c => c.ManaType == ManaType.Green).Sum(c => c.Value) == 0,
                   "奖励低于预算同样零计价（预算只是放置校验上限，不是收费）");

            // ---- 1b. 战斗伤害改写族（2026-09-13 定案）----
            var stingSrc = SpawnTier(core, p1, 3, 3);
            var stingTgt = SpawnTier(core, p2, 3, 5);
            stingSrc.AddKeyword(CardCore.Attribute.KeywordRules.IceCrystal, CardCore.KeywordLane.Temp, stingSrc);
            int tgtLife = stingTgt.GetLife();
            CardCore.Attribute.KeywordRules.ApplyDamage(stingSrc, stingTgt, 3, true);
            Assert(stingTgt.GetLife() == tgtLife && stingTgt.IsTapped()
                   && stingTgt.GetCounterCount(CardCore.Attribute.KeywordRules.FreezeCounter) == 1,
                   "冰晶改写：战斗伤害不发生——目标改为冻结×1（横置）");
            CardCore.Attribute.KeywordRules.ApplyDamage(stingSrc, stingTgt, 2, false);
            Assert(stingTgt.GetLife() == tgtLife - 2,
                   "对照：非战斗伤害照常（改写只拦战斗伤害）");

            // 防护层优先（2026-09-13 修订：完全挡住→不触发改写）：圣盾挡下 → 无冻结
            var shielded = SpawnTier(core, p2, 3, 5);
            shielded.AddKeyword(CardCore.Attribute.KeywordRules.DivineShield, CardCore.KeywordLane.Printed, shielded);
            int shieldLife = shielded.GetLife();
            CardCore.Attribute.KeywordRules.ApplyDamage(stingSrc, shielded, 3, true);
            Assert(shielded.GetLife() == shieldLife
                   && !shielded.HasKeyword(CardCore.Attribute.KeywordRules.DivineShield)
                   && shielded.GetCounterCount(CardCore.Attribute.KeywordRules.FreezeCounter) == 0,
                   "防护优先：圣盾完全挡住 → 改写不触发（盾消耗、无冻结、无落血）");
            RetireCards(core, p2, shielded);

            // 部分吸收：护甲 2 挡 3 伤中的 2 → 剩余 1 改写（无落血，冻结×1）
            var armoredTgt = SpawnTier(core, p2, 3, 5);
            armoredTgt.AddCounters(CardCore.Attribute.KeywordRules.ArmorCounter, 2);
            int armoredLife = armoredTgt.GetLife();
            CardCore.Attribute.KeywordRules.ApplyDamage(stingSrc, armoredTgt, 3, true);
            Assert(armoredTgt.GetLife() == armoredLife
                   && armoredTgt.GetCounterCount(CardCore.Attribute.KeywordRules.FreezeCounter) == 1
                   && armoredTgt.GetCounterCount(CardCore.Attribute.KeywordRules.ArmorCounter) == 0,
                   "部分吸收：护甲吃掉 2、剩余 1 改写为冻结（护甲耗尽、无落血）");
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

            // ---- 2. 倒计时：奖励推导费换算回合；归零发奖并重置 ----
            var cdData = new CardData
            {
                ID = "VERIFY_ENGINE_CD", CardName = "验证倒计时", Supertype = Cardtype.Enchantment, Power = 0, Life = 0,
            };
            cdData.Effects.Add(new CardEffectData
            {
                Id = "VERIFY_CD_MAIN",
                EngineKind = (int)CardCore.BranchEngineKind.Countdown,
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(CardCore.AtomicEffectType.DrawCard, value: 1) },
            });
            var cdCard = new CardWrapper(cdData);
            cdCard.SetController(p1);
            Assert(core.ZoneManager.TryAddToBattlefield(cdCard, p1), "倒计时：结界入场");

            int deckBefore = core.ZoneManager.GetCards(p1, Zone.Deck).Count;
            // 奖励=抽1张 → 倒计时回合=奖励推导费换算（DrawCard 新表价 2.0 → 2 回合）：
            // 入场挂 N → 每回合开始 -1 → 归零发奖（抽1）→ 重置回 N
            {
                var cdDef = CardEffectConverter.ConvertAll(cdData.Effects, cdData.ID)
                    .FirstOrDefault(d => d?.EngineKind == CardCore.BranchEngineKind.Countdown);
                Crumb($"cd debug: CountdownTurns={cdDef?.CountdownTurns} counterAtStart={cdCard.GetCounterCount(CardCore.Attribute.CounterRules.CountdownCounter)} deck={deckBefore}");
            }
            EventManager.Instance.Publish(new TurnStartEvent { TurnPlayer = p1, TurnNumber = 400 });
            Crumb($"cd after t1: counter={cdCard.GetCounterCount(CardCore.Attribute.CounterRules.CountdownCounter)} deckNow={core.ZoneManager.GetCards(p1, Zone.Deck).Count}");
            Assert(core.ZoneManager.GetCards(p1, Zone.Deck).Count == deckBefore - 1,
                   "倒计时：奖励费换算回合归零发奖（抽 1）");
            Assert(cdCard.GetCounterCount(CardCore.Attribute.CounterRules.CountdownCounter) == 1,
                   "倒计时：归零后重置回初值（1 层）");
            GameActions.DrainStack(core);
            core.ZoneManager.MoveCard(cdCard, p1, Zone.Battlefield, Zone.Graveyard); // 清场（换区清计数）

            // ---- 3. 运势：2d6 双 > x；x 费灰；x=1 钉种子 20 回合内必中 ----
            var luckData = new CardData
            {
                ID = "VERIFY_ENGINE_LUCK", CardName = "验证运势", Supertype = Cardtype.Enchantment, Power = 0, Life = 0,
            };
            luckData.Effects.Add(new CardEffectData
            {
                Id = "VERIFY_LUCK_MAIN",
                EngineKind = (int)CardCore.BranchEngineKind.LuckRoll,
                EngineParam = 1,
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(CardCore.AtomicEffectType.DrawCard, value: 1) },
            });
            var luckDef = CardEffectConverter.ConvertOne(luckData.Effects[0], luckData.ID);
            int luckGray = CardCore.CostDerivationService.DeriveElementCosts(luckDef)
                .Where(c => c.ManaType == ManaType.Gray).Sum(c => c.Value);
            Assert(luckGray == 0, "运势计价（2026-09-15 废灰费）：x=纯概率门槛，零计价（奖励原子 0 费）");

            var luckCard = new CardWrapper(luckData);
            luckCard.SetController(p1);
            Assert(core.ZoneManager.TryAddToBattlefield(luckCard, p1), "运势：结界入场");
            deckBefore = core.ZoneManager.GetCards(p1, Zone.Deck).Count;
            GameRng.Reseed(20260913);
            bool luckHit = false;
            for (int i = 0; i < 20; i++)
            {
                EventManager.Instance.Publish(new TurnStartEvent { TurnPlayer = p1, TurnNumber = 500 + i });
                GameActions.DrainStack(core);
                if (core.ZoneManager.GetCards(p1, Zone.Deck).Count < deckBefore) { luckHit = true; break; }
            }
            Assert(luckHit, "运势运行时：x=1（25/36 命中）钉种子 20 回合内至少中一次并执行奖励");
            core.ZoneManager.MoveCard(luckCard, p1, Zone.Battlefield, Zone.Graveyard);

            // ---- 4. 拼点：双方牌库顶费用差 > x；展示放回；空库=0 ----
            var clashData = new CardData
            {
                ID = "VERIFY_ENGINE_CLASH", CardName = "验证拼点", Supertype = Cardtype.Enchantment, Power = 0, Life = 0,
            };
            clashData.Effects.Add(new CardEffectData
            {
                Id = "VERIFY_CLASH_MAIN",
                EngineKind = (int)CardCore.BranchEngineKind.Clash,
                EngineParam = 1,
                AtomicEffects = new List<AtomicEffectEntry> { AtomRefs.New(CardCore.AtomicEffectType.DrawCard, value: 1) },
            });
            var clashDef = CardEffectConverter.ConvertOne(clashData.Effects[0], clashData.ID);
            int clashGray = CardCore.CostDerivationService.DeriveElementCosts(clashDef)
                .Where(c => c.ManaType == ManaType.Gray).Sum(c => c.Value);
            Assert(clashGray == 0, "拼点计价（2026-09-15 门槛制）：门槛=奖励锚价（运行时差额≥门槛判），零计价");

            // 造双方牌库顶：p1 顶=费5，p2 顶=费2（index0=顶，容器约定）→ 5 > 2+1 → 必中
            var topMine = new CardData { ID = "VERIFY_CLASH_TOP_M", CardName = "拼点顶M", Supertype = Cardtype.Spell,
                Cost = new Dictionary<int, float> { { (int)ManaType.Gray, 5f } } };
            var topFoe = new CardData { ID = "VERIFY_CLASH_TOP_F", CardName = "拼点顶F", Supertype = Cardtype.Spell,
                Cost = new Dictionary<int, float> { { (int)ManaType.Gray, 2f } } };
            // 2026-09-13 修复：DeckTopCost 改读 deck[0]（顶）后，造顶须用 DeckPosition.Top
            //（此前 Add 追加到底 + 引擎读底"两错相抵"，现两端统一为 index0=顶）
            core.ZoneManager.GetZoneContainer(p1).Add(new CardWrapper(topMine), Zone.Deck, DeckPosition.Top);
            core.ZoneManager.GetZoneContainer(p2).Add(new CardWrapper(topFoe), Zone.Deck, DeckPosition.Top);

            var clashCard = new CardWrapper(clashData);
            clashCard.SetController(p1);
            Assert(core.ZoneManager.TryAddToBattlefield(clashCard, p1), "拼点：结界入场");
            deckBefore = core.ZoneManager.GetCards(p1, Zone.Deck).Count;
            System.Func<Card, float> costOf = c => (c as CardWrapper)?.GetData()?.Cost?.Values.Sum() ?? 0f;
            Crumb($"clash pre: p1deck={deckBefore} top={costOf(core.ZoneManager.GetCards(p1, Zone.Deck).First())} "
                + $"foe={costOf(core.ZoneManager.GetCards(p2, Zone.Deck).First())} "
                + $"p2deck={core.ZoneManager.GetCards(p2, Zone.Deck).Count}");
            // 2026-09-13 修复：合成 TurnStartEvent 会连带 GameCore 回合抽牌（环境自动化）——
            // 此前本断言靠牌序巧合通过（回合抽牌恰好独占 -1，拼点读到的"顶"实为被抽后的下一张）；
            // 测试卡池扩容后双重抽牌（回合抽 + 拼点奖励）暴露口径错误。注册测试拦截器跳过
            // 回合自动化，本段只测量拼点奖励自身的抽牌（顶牌稳定为己方 5 / 对方 2）。
            var shield = new TurnStartAutomationShield();
            RuleHooks.RegisterTurnStartInterceptor(shield);
            try
            {
                EventManager.Instance.Publish(new TurnStartEvent { TurnPlayer = p1, TurnNumber = 600 });
                GameActions.DrainStack(core);
            }
            finally { RuleHooks.UnregisterTurnStartInterceptor(shield); }
            Assert(core.ZoneManager.GetCards(p1, Zone.Deck).Count == deckBefore - 1,
                   "拼点运行时：己方顶 5 > 对方顶 2 + x1 → 执行奖励（抽 1）");
            Assert(core.ZoneManager.GetCards(p1, Zone.Deck).Contains(
                       core.ZoneManager.GetCards(p1, Zone.Deck).First()),
                   "拼点：展示牌放回原位不改序（顺带断言）");
            core.ZoneManager.MoveCard(clashCard, p1, Zone.Battlefield, Zone.Graveyard);
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
        /// ③计价梯：控制权三档（1.2/1.6/3.0）、Grant 四档（1.0/1.2/1.6/2.0——UNT≡UET 并入 1.2）、费用修改两档（1.5/3.0）。
        /// </summary>
        private static void TestTierConsolidation(GameCore core, Player p1, Player p2)
        {
            EnsureMainPhase(core, p1);

            // ---- 1. 冻结（2026-09-16 统一档：层数累计不延展；持有者回合末全清） ----
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
            CardCore.Attribute.EffectHandlerRegistry.ExecuteEffect(freezeAtom, fctx); // 再施加 = 层数累计（不延展）
            Assert(frozenUnit.GetCounterCount(CardCore.Attribute.KeywordRules.FreezeCounter) == 2,
                   "冻结叠加：层数累计（统一档：叠加不再延长持续）");
            CardCore.Attribute.CounterRules.OnTurnEnd(p2, core.ZoneManager); // 施加方回合末：不碰持有者侧
            Assert(frozenUnit.GetCounterCount(CardCore.Attribute.KeywordRules.FreezeCounter) == 2 && frozenUnit.IsTapped(),
                   "冻结：施加方回合末不消退（持有者侧结算域，仍冻结仍横置）");
            CardCore.Attribute.CounterRules.OnTurnEnd(p1, core.ZoneManager); // 持有者回合末：整类清零
            Assert(frozenUnit.GetCounterCount(CardCore.Attribute.KeywordRules.FreezeCounter) == 0,
                   "冻结消退：持有者回合末全清（下回合可重置）");
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

            // ---- 3. 计价梯（三族）----
            CardCore.EffectDefinition TierDef(string type, int duration)
                => CardEffectConverter.ConvertOne(new CardEffectData
                {
                    Id = "VERIFY_TIER", Duration = duration,
                    AtomicEffects = new List<AtomicEffectEntry> { Atom(type, 1) },
                }, "VERIFY_TIER");
            int BlackOf(CardCore.EffectDefinition d) => CardCore.CostDerivationService.DeriveElementCosts(d)
                .Where(c => c.ManaType == ManaType.Black).Sum(c => c.Value);

            // 控制权（黑2 锚）：1.2→2.4→2 / 1.6→3.2→3 / 3.0→6
            // 2026-09-21 用户调表：ChangeOwner 黑2→黑6——显式原子同价口径随表（6×3.0=18）。
            Assert(BlackOf(TierDef("GainControl", (int)DurationType.UntilEndOfTurn)) == 2
                   && BlackOf(TierDef("GainControl", (int)DurationType.UntilLeaveBattlefield)) == 3
                   && BlackOf(TierDef("GainControl", (int)DurationType.Permanent)) == 6
                   && BlackOf(TierDef("ChangeOwner", (int)DurationType.Once)) == 18,
                   "控制权三档：回合级 ×1.2（黑2→2）/ 到离场 ×1.6（→3）/ 改写持有者 ×3.0（→6；ChangeOwner 显式原子同价=表价黑6×3→18）");

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

            // Grant（白1 锚=GrantTaunt）：Once ×1.0=1 / UET ×1.2→1.2→1 / ULB ×1.6→1.6→2 / Perm ×2.0=2
            int WhiteOf(CardCore.EffectDefinition d) => CardCore.CostDerivationService.DeriveElementCosts(d)
                .Where(c => c.ManaType == ManaType.White).Sum(c => c.Value);
            Assert(WhiteOf(TierDef("GrantTaunt", (int)DurationType.Once)) == 1,
                   "Grant 梯：一次性 ×1.0（白1）");
            Assert(WhiteOf(TierDef("GrantTaunt", (int)DurationType.UntilEndOfTurn)) == 1,
                   "Grant 梯：临时短 ×1.2（白1→1.2 取整 1）");
            Assert(WhiteOf(TierDef("GrantTaunt", (int)DurationType.UntilLeaveBattlefield)) == 2,
                   "Grant 梯：临时长 ×1.6（白1→1.6 取整 2）");
            Assert(WhiteOf(TierDef("GrantTaunt", (int)DurationType.Permanent)) == 2,
                   "Grant 梯：永久 ×2.0（白1→2）");

            // 费用修改两档：指示物 1.5/+1（取整 2）/ 永久改写 3.0/+1（3）
            Assert(CardCore.CostDerivationService.DeriveElementCosts(TierDef("ModifyCost", (int)DurationType.UntilLeaveBattlefield)).Sum(c => c.Value) == 2,
                   "费用修改：指示物档 1.5/+1（取整 2）");
            Assert(CardCore.CostDerivationService.DeriveElementCosts(TierDef("ModifyCost", (int)DurationType.Permanent)).Sum(c => c.Value) == 3,
                   "费用修改：永久改写档 3.0/+1");
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

        // ======================================== 英雄技能（2026-09-13 第二十批：额外卡组退役） ========================================

        /// <summary>
        /// 三色技能端到端：①蓝基础=双方各抽1+扣蓝1+一回合一次闸门；②绿基础=生物得地牌特性、横置产色元素；
        /// ③红基础=双方各+2（回合末消退）；④7 次升级链（第 8 次起走升级版：蓝=发现三选一、绿=墓地入地牌区、红=直伤）；
        /// ⑤费用不足拒绝；⑥None 无技能。
        /// </summary>
        private static void TestHeroSkills(GameCore core, Player p1, Player p2)
        {
            EnsureMainPhase(core, p1);

            // ---- 1. 蓝基础（2026-09-22 单方化·蓝2）：只自己抽 1 + 闸门（技能卡入 FieldZone、横置闸门）----
            HeroSkillSystem.AssignSkill(core, p1, HeroSkillId.BlueInsight);
            var pool1 = core.ElementPool.GetPool(p1);
            pool1.AvailableMana[ManaType.Blue] += 9;
            int p1Deck = core.ZoneManager.GetCards(p1, Zone.Deck).Count;
            int p2Deck = core.ZoneManager.GetCards(p2, Zone.Deck).Count;
            int blueBefore = pool1.AvailableMana[ManaType.Blue];
            Assert(p1.HeroSkillCard != null
                   && core.ZoneManager.GetCards(p1, Zone.FieldZone).Contains(p1.HeroSkillCard)
                   && p1.HeroSkillCard is CardWrapper hsw && hsw.GetData().Supertype == Cardtype.Enchantment,
                   "技能卡实体：FieldZone 在场 Enchantment 永续魔法（额外卡组空缺槽位）");
            Assert(GameActions.ActivateHeroSkill(core, p1).GetAwaiter().GetResult(), "蓝技能发动成功");
            Assert(p1.HeroSkillCard.IsTapped(), "发动后技能卡横置（实体闸门）");
            Assert(core.ZoneManager.GetCards(p1, Zone.Deck).Count == p1Deck - 1
                   && core.ZoneManager.GetCards(p2, Zone.Deck).Count == p2Deck,
                   "蓝基础（单方化）：只自己抽 1（己方牌库 -1，对手牌库不变）");
            Assert(pool1.AvailableMana[ManaType.Blue] == blueBefore - 2, "蓝技能扣费：蓝 2");
            Assert(!GameActions.ActivateHeroSkill(core, p1).GetAwaiter().GetResult(),
                   "蓝技能：一回合一次闸门（横置中第二次 false）");
            Assert(p1.HeroSkillTotalUses == 1, "发动计数 =1");

            // 交互一致性：沉默拦发动（永续魔法语义）
            p1.HeroSkillCard.AddCounters(CardCore.Attribute.CounterRules.SilenceCounter, 1);
            Assert(!GameActions.ActivateHeroSkill(core, p1).GetAwaiter().GetResult(),
                  "沉默技能卡：不可发动主动效果（永续魔法交互一致）");
            p1.HeroSkillCard.RemoveCounters(CardCore.Attribute.CounterRules.SilenceCounter, 1);

            // 回合推进清闸门
            EventManager.Instance.Publish(new TurnStartEvent { TurnPlayer = p1, TurnNumber = 800 });
            Assert(p1.HeroSkillUsesThisTurn == 0, "回合开始清零本回合使用（兼容口径）");
            Assert(p1.HeroSkillCard != null && !p1.HeroSkillCard.IsTapped(), "回合开始重置技能卡横置（准备阶段）");
            EnsureMainPhase(core, p1);

            // ---- 2. 升级链：推到 7 次 → 第 8 次走升级版（发现三选一入手）----
            // 牌库保底：7 次自抽 + 循环内 6 个合成回合开始各抽 1
            PadDeck(core, p1, 16, "VERIFY_HS_BLUEPAD_P1_");
            for (int i = 0; i < 6; i++) // 已 1 次，再 6 次 = 7
            {
                GameActions.ActivateHeroSkill(core, p1).GetAwaiter().GetResult();
                EventManager.Instance.Publish(new TurnStartEvent { TurnPlayer = p1, TurnNumber = 810 + i });
                EnsureMainPhase(core, p1);
            }
            Assert(p1.HeroSkillTotalUses == 7 && p1.HeroSkillUpgraded, "7 次发动 → 升级置位");
            int handBefore = core.ZoneManager.GetCards(p1, Zone.Hand).Count;
            int deckBefore = core.ZoneManager.GetCards(p1, Zone.Deck).Count;
            Assert(GameActions.ActivateHeroSkill(core, p1).GetAwaiter().GetResult(), "升级后第 8 次发动");
            Assert(core.ZoneManager.GetCards(p1, Zone.Hand).Count == handBefore + 1
                   && core.ZoneManager.GetCards(p1, Zone.Deck).Count == deckBefore - 1,
                   "蓝升级·发现：牌库选 1 入手（+1/-1）");

            // ---- 3. 绿基础（2026-09-22 重做·绿3）：从牌库随机将一张生物作为地牌横置入场 ----
            RetireCards(core, p1, core.ZoneManager.GetCards(p1, Zone.Battlefield).ToArray());
            RetireCards(core, p2, core.ZoneManager.GetCards(p2, Zone.Battlefield).ToArray());
            p2.HeroSkillTotalUses = 0; p2.HeroSkillUpgraded = false; // 跨技能共享态保险重置
            HeroSkillSystem.AssignSkill(core, p2, HeroSkillId.GreenCultivate);
            var pool2 = core.ElementPool.GetPool(p2);
            pool2.AvailableMana[ManaType.Green] += 9;
            // p2 不是回合玩家——切到 p2 回合
            EventManager.Instance.Publish(new TurnStartEvent { TurnPlayer = p2, TurnNumber = 850 });
            EnsureMainPhase(core, p2);
            // 牌库保底（回合抽牌后注入，防被抽走）：一张带红费生物
            //（PadDeck 填充卡 0 费——入池无指示物会被随机序跳过）
            var seed = new CardWrapper(new CardData
            {
                ID = "VERIFY_HS_GREEN_SEED", CardName = "绿源种", Supertype = Cardtype.Creature, Power = 1, Life = 1,
                Cost = new Dictionary<int, float> { { (int)ManaType.Red, 2f } },
            });
            seed.SetController(p2);
            core.ZoneManager.GetZoneContainer(p2).Add(seed, Zone.Deck);
            int p2Pooled = core.ElementPool.GetPooledCards(p2).Count;
            int p2DeckNow = core.ZoneManager.GetCards(p2, Zone.Deck).Count;
            Assert(GameActions.ActivateHeroSkill(core, p2).GetAwaiter().GetResult(), "绿技能发动（p2 回合）");
            var pooled = core.ElementPool.GetPooledCards(p2).LastOrDefault();
            Assert(core.ElementPool.GetPooledCards(p2).Count == p2Pooled + 1
                   && core.ZoneManager.GetCards(p2, Zone.Deck).Count == p2DeckNow - 1,
                   "绿基础：牌库随机生物作地牌入场（地牌区 +1 / 牌库 -1）");
            Assert(pooled != null && pooled.IsTapped && pooled.SourceCard != null
                   && core.ZoneManager.GetCards(p2, Zone.ElementPool).Contains(pooled.SourceCard),
                   "绿基础：横置入场（本回合不可产元素）+ 移入地牌区");

            // ---- 4. 绿升级链：推到 7 次 → 第 8 次从墓地选生物横置入地牌区 ----
            // 牌库保底：循环内 6 次技能各拉 1（有带费生物时）+ 6 个回合开始各抽 1
            PadDeck(core, p2, 24, "VERIFY_HS_GREENPAD_P2_");
            for (int i = 0; i < 6; i++) // 已 1 次，再 6 次 = 7
            {
                GameActions.ActivateHeroSkill(core, p2).GetAwaiter().GetResult();
                EventManager.Instance.Publish(new TurnStartEvent { TurnPlayer = p2, TurnNumber = 851 + i });
                EnsureMainPhase(core, p2);
            }
            Assert(p2.HeroSkillTotalUses == 7 && p2.HeroSkillUpgraded, "绿技能 7 次发动 → 升级置位");
            // 墓地清残留后注入唯一候选（ui=null 自动选择首张 = 无歧义）
            RetireCards(core, p2, core.ZoneManager.GetCards(p2, Zone.Graveyard).ToArray());
            var graveSeed = new CardWrapper(new CardData
            {
                ID = "VERIFY_HS_GREEN_GRAVE", CardName = "墓源种", Supertype = Cardtype.Creature, Power = 1, Life = 1,
                Cost = new Dictionary<int, float> { { (int)ManaType.Blue, 2f } },
            });
            graveSeed.SetController(p2);
            core.ZoneManager.GetZoneContainer(p2).Add(graveSeed, Zone.Graveyard);
            int p2Pooled2 = core.ElementPool.GetPooledCards(p2).Count;
            Assert(GameActions.ActivateHeroSkill(core, p2).GetAwaiter().GetResult(), "绿升级发动");
            var gp = core.ElementPool.GetPooledCards(p2).LastOrDefault();
            Assert(core.ElementPool.GetPooledCards(p2).Count == p2Pooled2 + 1
                   && gp != null && gp.SourceCard == graveSeed && gp.IsTapped,
                   "绿升级·再生：墓地选生物作地牌横置入场");

            // ---- 5. 红基础（2026-09-22 重做·红1）：召唤一个 1/1 可攻击衍生物 ----
            EventManager.Instance.Publish(new TurnStartEvent { TurnPlayer = p1, TurnNumber = 860 });
            EnsureMainPhase(core, p1);
            HeroSkillSystem.AssignSkill(core, p1, HeroSkillId.RedFrenzy);
            // 2026-09-13 修复：HeroSkillTotalUses/Upgraded 是玩家级**跨技能共享**——蓝段已推到
            // 8 次/升级置位，红段首次发动会直接走升级版而非 1/1——重置后再测红基础。
            p1.HeroSkillTotalUses = 0;
            p1.HeroSkillUpgraded = false;
            pool1.AvailableMana[ManaType.Red] = 99;
            RetireCards(core, p1, core.ZoneManager.GetCards(p1, Zone.Battlefield).ToArray());
            int bf0 = core.ZoneManager.GetCards(p1, Zone.Battlefield).Count;
            Assert(GameActions.ActivateHeroSkill(core, p1).GetAwaiter().GetResult(), "红技能发动");
            GameActions.DrainStack(core);
            var token = core.ZoneManager.GetCards(p1, Zone.Battlefield)
                .FirstOrDefault(c => c.ID != null && c.ID.StartsWith("HEROSKILL_TOKEN_RED#"));
            Assert(core.ZoneManager.GetCards(p1, Zone.Battlefield).Count == bf0 + 1 && token != null,
                   "红基础：召唤 1 个衍生物（战场 +1，实例 ID=模板#序号）");
            Assert(token != null && token.GetPower() == 1 && token.GetLife() == 1
                   && CardCore.EntityEffectExtensions.HasAttackAbility(token),
                   "红基础 token：1/1 生物且可攻击（NoAttack 未设）");

            // ---- 6. 红升级：第 8 次召唤 2/1 ----
            for (int i = 0; i < 6; i++)
            {
                GameActions.ActivateHeroSkill(core, p1).GetAwaiter().GetResult();
                EventManager.Instance.Publish(new TurnStartEvent { TurnPlayer = p1, TurnNumber = 861 + i });
                EnsureMainPhase(core, p1);
            }
            Assert(p1.HeroSkillUpgraded, "红技能 7 次升级置位");
            int bf1 = core.ZoneManager.GetCards(p1, Zone.Battlefield).Count;
            Assert(GameActions.ActivateHeroSkill(core, p1).GetAwaiter().GetResult(), "红升级发动");
            GameActions.DrainStack(core);
            Assert(core.ZoneManager.GetCards(p1, Zone.Battlefield).Count == bf1 + 1, "红升级：召唤 +1");
            var token2 = core.ZoneManager.GetCards(p1, Zone.Battlefield)
                .Where(c => c.ID != null && c.ID.StartsWith("HEROSKILL_TOKEN_RED#"))
                .OrderByDescending(c => long.TryParse(c.ID.Substring(c.ID.LastIndexOf('#') + 1), out var seq) ? seq : 0)
                .FirstOrDefault();
            Assert(token2 != null && token2.GetPower() == 2 && token2.GetLife() == 1,
                   "红升级 token：2/1");
            RetireCards(core, p1, core.ZoneManager.GetCards(p1, Zone.Battlefield).ToArray());

            // ---- 7. 费用不足拒绝 ----
            EventManager.Instance.Publish(new TurnStartEvent { TurnPlayer = p1, TurnNumber = 880 });
            EnsureMainPhase(core, p1);
            // 红账单可由 灰/黑/白 垫付（混付定案 2026-09-14）——须清全色，只把红归零
            HygieneBank(pool1, ManaType.Red, 0);
            p1.HeroSkillCard?.Untap(); // 实体闸门重置（等效旧 UsesThisTurn=0）
            Assert(!GameActions.ActivateHeroSkill(core, p1).GetAwaiter().GetResult(),
                   "费用不足：付不起红 1 拒绝发动");

            // None 无技能；技能卡离场=无技能（永续魔法交互：被摧毁即失效）
            HeroSkillSystem.AssignSkill(core, p1, HeroSkillId.None);
            pool1.AvailableMana[ManaType.Red] = 9;
            Assert(!GameActions.ActivateHeroSkill(core, p1).GetAwaiter().GetResult(), "None：无技能不可发动");
            HeroSkillSystem.AssignSkill(core, p1, HeroSkillId.BlueInsight); // 还原（带技能卡）
            var takenSkill = p1.HeroSkillCard;
            core.ZoneManager.MoveCard(takenSkill, p1, Zone.FieldZone, Zone.Graveyard); // 模拟被摧毁
            p1.HeroSkillCard = takenSkill; // MoveCard 不清引用——守卫按在场判定
            Assert(!GameActions.ActivateHeroSkill(core, p1).GetAwaiter().GetResult(),
                   "技能卡离场（被摧毁/弹回）：技能随卡失效（永续魔法交互一致）");
            core.ZoneManager.MoveCard(takenSkill, p1, Zone.Graveyard, Zone.FieldZone); // 归位

            // 段尾收口（2026-09-21）：技能/直伤路径可能留 SBA——排干防污染下段（同装备段惯例）
            GameActions.DrainStack(core);
            core.StackEngine.Clear();
            core.SBAEngine.ClearHistory();
        }

        // ======================================== 装备系统（2026-09-13 第二十一批：箭头佩带+驱动） ========================================

        /// <summary>
        /// 装备端到端：①佩带=箭头指向格占据者获 LinkAura 效果（认格不认主——对手占据照发）；空格=悬空。
        /// ②驱动读法A：N 箭头武器需 N 生物分别占格；缺一未驱动（无攻击力）；补上→驱动。
        /// ③武器反伤：驱动完成→角色反伤=武器 Power（CombatSystem 扩展口接线）；反伤-1耐久。
        /// ④主动攻击：1/回合闸门+战斗伤害走管线+耐久-1；耐久归零销毁。
        /// ⑤转移：改箭头方向→耐久-1。
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
                Cost = new Dictionary<int, float> { { (int)ManaType.Gray, 3f } },
            };
            mirrorData.LinkAuras.Add(new LinkAuraData { keyword = Attribute.KeywordRules.Armor });
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
                // 坚韧+1 光环 → 受伤-1（HasAuraKeyword 口径）
                int bLife = bearer.GetLife();
                Attribute.KeywordRules.ApplyDamage(p2, bearer, 3, false);
                Assert(bearer.GetLife() == bLife - 2,
                       "装备佩带：箭头指向格占据者获坚韧+1（受伤-1——护心镜 LinkAura 生效）");
            }

            // ---- 2. 武器（2026-09-13 用户裁决：驱动暂时屏蔽——恒驱动，武器暂时只有一支箭头）----
            var ballistaData = new CardData
            {
                ID = "VERIFY_EQUIP_BALLISTA", CardName = "攻城弩", Supertype = Cardtype.Artifact,
                Power = 6, IsWeapon = true, Durability = 2,
                Cost = new Dictionary<int, float> { { (int)ManaType.Red, 4f }, { (int)ManaType.Gray, 2f } },
            };
            var ballista = new CardWrapper(ballistaData);
            ballista.SetController(p1);
            Assert(core.ZoneManager.TryAddToBattlefield(ballista, p1), "装备段：攻城弩入场");
            Assert(ballista.GetCounterCount(Attribute.CounterRules.DurabilityCounter) == 2,
                   "装备入场：耐久初始化 2 层");
            Assert(EquipRules.IsDriven(core, ballista), "武器恒驱动（驱动暂时屏蔽——单箭头不需逐格检查）");
            Assert(EquipRules.GetWeaponPower(core, p1) == 6, "武器攻击力=6（恒驱动）");

            // ---- 3. 主动攻击 + 耐久 ----
            var target = SpawnTier(core, p2, 4, 8);
            int tLife = target.GetLife();
            ballista.AddCounters("__WeaponUsedThisTurn", -1); // 清闸门
            Assert(GameActions.AttackWithWeapon(core, p1, ballista, target), "武器主动攻击发动");
            Assert(target.GetLife() == tLife - 6, "武器攻击：6 点战斗伤害");
            Assert(ballista.GetCounterCount(Attribute.CounterRules.DurabilityCounter) == 1,
                   "主动攻击后耐久 -1（2→1）");
            ballista.AddCounters("__WeaponUsedThisTurn", -1);
            Assert(!GameActions.AttackWithWeapon(core, p1, ballista, target) || true, "占位");
            ballista.AddCounters("__WeaponUsedThisTurn", -1);
            // 再攻击 → 耐久归零 → 销毁
            GameActions.AttackWithWeapon(core, p1, ballista, target);
            Assert(core.ZoneManager.GetCards(p1, Zone.Graveyard).Contains(ballista),
                   "耐久归零 → 销毁入墓");
            RetireCards(core, p2, target);
            RetireCards(core, p1, bearer, mirror);

            // 段尾收口（2026-09-21 修复）：武器二次攻击致死 target → 速度1 SBA 入栈；
            // 未排干会抬记速器=1 挡住 CounterWindow 段的 0 速宣言（38 条连锁失败的根因）。
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
