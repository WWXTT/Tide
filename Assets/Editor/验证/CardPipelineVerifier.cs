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
    public static partial class CardPipelineVerifier
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
                // 统一计价：全表规则一巡检（2026-09-11 简化口径；2026-10-31 阶梯减价删除后
                // 锚=Ĉ+结构费——结构加价计入锚，Req>0 只可能来自取整漂移）。
                Crumb("→规则一巡检 derive");
                var offenders = cardsData
                    .Where(c => !CardCostService.Derive(c).Conformant)
                    .ToList();
                // 不符明细（一次性诊断输出：定位 S/K/E/结构/挂载口/D 哪一环口径漂了）
                foreach (var o in offenders)
                {
                    var r = CardCostService.Derive(o);
                    Debug.LogError($"[规则一明细] {o.CardName}({o.ID}) 推导档Ĉ={r.DerivedTier} S={r.S} K={r.K} "
                        + $"E={r.EAnchor} D={r.DerivedTotal} "
                        + $"Req={r.OffsetRequirement} G=[{r.Grants}]\n  "
                        + string.Join("\n  ", r.Breakdown.Select(l => $"[{l.Stage}] {l.Label} = {l.Value}")));
                }
                Assert(offenders.Count == 0,
                       $"规则一巡检：全表 {cardsData.Count} 张 D≤Ĉ+结构费（不符 {offenders.Count}：{string.Join(",", offenders.Select(o => o.ID))}）");

                Crumb("→TestCostCeiling");
                TestCostCeiling(core, p1, p2); // 费用取整（2026-11-09 逐色进位定案）：全表整数/支付口径/两侧口径

                // 极性校验（2026-11-02 定案）：代价型原子指向自己不得计正费——效果栏一律 0 费
                //（CostDerivationService.AccumulateElementCost 极性裁定）+ 本断言拦截 0 容忍；
                // 代价语义走代价栏（payloads·色锁抵扣），打击对手改指对侧。只扫正式池（夹具是
                // 引擎单测、允许构造自伤组合）。
                // 备注（时机溢价档案·2026-11-05 两档收敛）：训练·夜汲 cost 列钉死 3蓝（手作行锚价
                //权威——直出不推导）；两档系数均 ×1 后新行不再按时机加价，存量钉死锚价快照不漂移
                //（期望次数口径 2蓝×OnTurnEnd1.5=3蓝 的推导链已随本日定案作废）。
                var polarHits = new List<string>();
                foreach (var c in cardsData.Take(catalogCount))
                    foreach (var v in CardCore.CostDerivationService.SelfCostViolations(c))
                        polarHits.Add($"{c.CardName}({c.ID}) {v}");
                foreach (var hit in polarHits)
                    Debug.LogError($"[极性校验] {hit}");
                Assert(polarHits.Count == 0,
                       $"极性校验：正式池代价型原子指向自己 0 处（命中 {polarHits.Count}，白名单见 CostDerivationService.SelfCostAtoms）");

                // 代价栏翻转限制（2026-11-02 定案）：①极性=0 ②无 {target} 参数的原子不得进代价栏——
                // 装载期 ConvertPayloadForDisplay 只剔原子留 Error 日志，违规卡本身在此点名 0 容忍
                //（卡名+效果+原子 id；与极性校验同款双挂接——重推工具 RegenOneDeck 同口上报）。
                var payloadHits = new List<string>();
                foreach (var c in cardsData.Take(catalogCount))
                    foreach (var v in CardCore.CostDerivationService.PayloadCostViolations(c))
                        payloadHits.Add($"{c.CardName}({c.ID}) {v}");
                foreach (var hit in payloadHits)
                    Debug.LogError($"[代价栏校验] {hit}");
                Assert(payloadHits.Count == 0,
                       $"代价栏翻转限制：正式池极性0/无target代价原子 0 处（命中 {payloadHits.Count}，2026-11-02 定案）");

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
                Crumb("→TestCostDeductionAndPayability");
                TestCostDeductionAndPayability(core, p1, p2);
                Crumb("→TestBenefitEnemyCostArm");
                TestBenefitEnemyCostArm(core, p1, p2);
                Crumb("→TestActiveEffectCost");
                TestActiveEffectCost(core, p1, p2);
                Crumb("→TestP9TimingTiersAndEligibility");
                TestP9TimingTiersAndEligibility(core, p1);
            Crumb("→TestSleep");
            TestSleep(core, p1, p2);
            Crumb("→TestCharge");
            TestCharge(core, p1, p2);
            Crumb("→TestP8FieldCards");
            TestP8FieldCards(core, p1, p2);
                Crumb("→TestBoard");
                TestBoard(core, p1, p2, cardsData);
                Crumb("→TestLinkAura");
                TestLinkAura(core, p1, p2, cardsData);
            }

            // 分支条件目录 + 信息族（宣言/预言）引擎流：不依赖卡表（合成卡驱动）
            Crumb("→TestCostAnchors");
            TestCostAnchors();
            Crumb("→TestManaSeatShrink");
            TestManaSeatShrink();
            Crumb("→TestComposerModel");
            TestComposerModel();
            Crumb("→TestDiscountExclusivity");
            TestDiscountExclusivity(); // 2026-11-02 减费三选一（代价并列槽位/三选一互斥/数值上限）
            // 分支目录层已于 2026-10-22 彻底删除（2026-09-10 曾以 5397a3f 暂时下架，本次定案不再恢复：
            // 配置只保留原子表 + ValueSystemConfig 两张）。原 TestBranchCatalog 收敛为
            // TestInfoAtomsAndDimensions（保留信息族原子/维度有限域断言，目录断言随表删除）。
            // TestInfoAtomsAndDimensions();
            Crumb("→TestInformationFlow");
            TestInformationFlow(core, p1, p2);
            Crumb("→TestKeywords");
            TestKeywords(core, p1, p2);
            Crumb("→TestDamageChain");
            TestDamageChain(core, p1, p2); // 伤害管线重构 Phase 2-4（2026-11-09 草案）：层序/golden 对拍/循环链
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
            Crumb("→TestS3bActivationRemoval");
            TestS3bActivationRemoval(core, p1, p2);
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
            // 2026-11-06 卫生：costList 全部 4 位现行格式；链接光环测试=Enchantment（2026-10-15
            // 连接光环专属结界定案——Creature 形态会被构筑期拦截报 Error）。
            var json = @"{
  ""cards"": [
    {
      ""cardName"": ""火球术"", ""supertype"": ""Spell"",
      ""costList"": [ 0.0, 3.0, 2.0, 0.0 ],
      ""keywords"": [], ""effects"": [
        { ""Id"": ""FIREBALL_MAIN"", ""TriggerTiming"": 19, ""SelectionMode"": 0, ""TargetCount"": 1,
          ""AtomicEffects"": [ { ""refId"": ""@DealDamage"", ""value"": 4, ""kinds"": [2] } ] },
        { ""Id"": ""FIREBALL_DRAW"", ""TriggerTiming"": 19, ""SelectionMode"": -1,
          ""AtomicEffects"": [ { ""refId"": ""@DrawCard"", ""value"": 1 } ] } ]
    },
    {
      ""cardName"": ""古树守卫"", ""supertype"": ""Creature"", ""power"": 1, ""life"": 1,
      ""costList"": [ 1.0, 0.0, 0.0, 0.0 ], ""keywords"": [], ""effects"": []
    },
    {
      ""cardName"": ""灰色哨兵"", ""supertype"": ""Creature"", ""power"": 1, ""life"": 1,
      ""costList"": [ 1.0, 0.0, 0.0, 0.0 ], ""keywords"": [], ""effects"": []
    },
    {
      ""cardName"": ""抉择试作"", ""supertype"": ""Spell"",
      ""costList"": [ 0.0, 3.0, 0.0, 0.0 ],
      ""keywords"": [], ""effects"": [
        { ""Id"": ""MODAL_FIRE"", ""TriggerTiming"": 19, ""SelectionMode"": 0, ""TargetCount"": 1,
          ""AtomicEffects"": [ { ""refId"": ""@DealDamage"", ""value"": 4, ""kinds"": [2] } ] },
        { ""Id"": ""MODAL_DRAW"", ""TriggerTiming"": 19, ""SelectionMode"": -1,
          ""AtomicEffects"": [ { ""refId"": ""@DrawCard"", ""value"": 1 } ] } ]
    },
    {
      ""cardName"": ""链接光环测试"", ""supertype"": ""Enchantment"",
      ""costList"": [ 3.0, 0.0, 0.0, 0.0 ],
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
            Assert(!core.ElementPool.AddCardToPool(land1, p1, ManaType.Any),
                   "显式非法锁色拒绝（Any 万能货币不可锁——候选恒不含 Any；2026-10-28 P2 后白/黑席位已删）——直接调池校验口");

            // 现取现用：无手动横置——付费时系统按账单自动横置（每地牌每回合一次）
            // 2026-10-16 口径：地牌上限只限元素产出（入池张数），不限使用——钱包无钳、
            // 总费用 > 上限的卡照打（旧「费用门槛」断言随 CanAfford 总闸删除退役）。
            // 2026-11-06 充能 opt-in 后弃「起手扫 1 费卡」：全池卡面费 +1 灰使原 1 费卡全变 2，
            // 卡面恰 1 的只剩「原 0 费 + 充能灰」的法术——主动效果锚价现付，实际账单 2~3，
            // T1 单地（上限 1）付不出（实测教学·43：账单 灰1红1，红无来源拒付）。
            // 改注入推导费恰 1 灰的夹具（1/1 白板 + 攻守声明 + 充能位：身材全被免费属性底吸收，
            // 账单 = 充能灰 1）。注：T2 的「灰色哨兵」夹具不可复用——costList=1 被免费底吸收后
            // 实际 0 费，打出不触发横置。
            {
                var oneFee = new CardWrapper(new CardData
                {
                    ID = "VERIFY_LAND_ONEFEE",
                    CardName = "一费哨兵",
                    Supertype = Cardtype.Creature,
                    Power = 1, Life = 1,
                    AttackAbility = true, GuardAbility = true, ChargeAbility = true,
                    Effects = new List<CardEffectData>(),
                });
                oneFee.SetController(p1);
                core.ZoneManager.GetZoneContainer(p1).Add(oneFee, Zone.Hand);
                var oneFeeBill = GameActions.GetCardCost(oneFee, 0);
                Assert((int)oneFeeBill.Total == 1 && oneFeeBill[ManaType.Gray] >= 1f,
                       "一费夹具钉死：实际施放账单恰 1 灰（防免费底口径漂移静默归零成 0 费）");
                Assert(PlayCardSync(core, p1, oneFee, null), "1 费卡打出（系统自动横置地牌支付）");
                Assert(pooled1[0].IsTapped, "付费后地牌横置（现取现用·每回合周期一次）");
                // 战场残留送墓（同 TestSleep 收尾惯例）：生物留场会污染后续段落「场上唯一候选」
                // 假设（实测挤掉定长沉睡的自动代选→0 层）；历史基线此处打出多为法术（结算即入墓）从未暴露。
                core.ZoneManager.MoveCard(oneFee, p1, Zone.Battlefield, Zone.Graveyard);
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

        /// <summary>全色灌满（2026-10-24 Any 定案配套；2026-10-28 P2 后=灰红蓝绿四色）：**Any 恒 0**——
        /// 万能货币不进「满钱包」环境（否则各链尾 Any 垫付会让「缺色拒付/优先序」类断言失真）。
        /// 清零环境仍走 AllManaTypes() 全枚举（Any 必须被清）；测 Any 的断言显式设置。</summary>
        private static void FillSixColors(PlayerElementPool pool, int n)
        {
            foreach (var t in AllManaTypes()) pool.TempElements[t] = t == ManaType.Any ? 0 : n;
        }

        /// <summary>测试费用构造（2026-10-04 位置数组化）：替代旧 Dictionary 初始化器——
        /// CostOf((ManaType.Gray, 3), (ManaType.Green, 1)) == [3,0,0,1,0]（5 位，P2 席位收缩后）。</summary>
        private static ElementCost CostOf(params (ManaType color, float amount)[] entries)
        {
            var c = new ElementCost();
            foreach (var (color, amount) in entries)
                c[color] = amount;
            return c;
        }

        /// <summary>混付定案（2026-09-14；2026-10-28 四色重构）：红/蓝/绿账单可由 [本色,任意] 垫付、
        /// 灰由 [灰,任意] 垫付（ElementAffinity.ChainFor）。「精确同色占用即拒」类断言须清空全色
        /// bank 只留被测色，否则任意货币残留会替第二张账单垫付，导致应拒变放行。</summary>
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
            // 段末清理：对照组幸存者送墓——漏清会让它以受伤状态滞留 p2 战场污染后续段
            //（资敌治疗闸按「对方有受伤单位」放行，TestActiveEffectCost 满血拦截断言被击穿）
            core.ZoneManager.MoveCard(survivor, p2, Zone.Battlefield, Zone.Graveyard);
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
            // 2026-10-31 P12 抉择退役：内嵌 kind==2 步骤告警整步跳过（不再生成 RuntimeStepKind.Choice
            //——法术模式=非充能主动效果数，GetModeCount 单源）
            Assert(choiced.Steps.Count == 0 && choiced.Effects.Count == 0,
                   "含抉择 Steps（legacy kind=2）：整步跳过不折叠（2026-10-31 P12 退役——转换器告警忽略）");

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
                    $"空战场 → HasPlayableTargets=false（激励行域=己方战场:生物+Tapped（B2：类别随 Domain 走），候选空不可发动；实际 {playable}）");
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
                   "内容契约：错边原子在效果栏被剔除（对对手有益只能进代价栏——代价契约见 TestCostDeductionAndPayability）");
            var bothDef = CardEffectConverter.ConvertOne(new CardEffectData
            {
                Id = "VERIFY_POL_B",
                AtomicEffects = new List<AtomicEffectEntry>
                {
                    AtomRefs.New(AtomicEffectType.Heal, value: 2, kinds: new List<int> { 1 }),
                },
            }, "VERIFY_POL_B");
            Assert(System.Math.Abs(CostDerivationService.DeriveElementCosts(bothDef).Total - rightCost) < 1e-4f,
                "双侧域构筑期全价（错边拆分已删——2026-10-28 P1：极性不再参与计价）");

            // g) TargetKind 重排（2026-09-11 Self 找回）：Self=0 / 单位 1-4 / 卡域 5-16；关键词域=己方战场:生物（2026-11-07 特判）
            Assert((int)CardCore.TargetKind.Self == 0
                   && (int)CardCore.TargetKind.OwnLivingUnit == 1
                   && (int)CardCore.TargetKind.EnemyActivation == 16,
                   "TargetKind 序号：Self=0、单位 1-4、卡域 5-16（整体后移）");
            var tauntCfg = CardCore.Attribute.AtomicEffectTable.GetByType(AtomicEffectType.GrantTaunt);
            var tauntKinds = tauntCfg?.GetTargetKindList();
            Assert(tauntKinds != null && tauntKinds.Count == 1 && tauntKinds[0] == (int)CardCore.TargetKind.OwnLivingUnit,
                   "关键词（嘲讽）表域 = 己方战场:生物（2026-11-07 特判：效果层关键词=赋予己方战场生物）");

            // h) 多关键词印制计价（原「黑白关键词」块，2026-10-28 P2 白/黑席位删除后改口径）：
            //    关键词照旧印制（不转主动）——原「黑白份额进费用」断言已随枚举退役删除，
            //    计价色随 P2a 原子表改面漂移（表为单一来源，不钉色）。
            var bwJson = "{\"cards\":[{\"id\":\"VERIFY_BW_KW\",\"cardName\":\"多关键词印制验证\","
                       + "\"supertype\":\"Creature\",\"power\":2,\"life\":2,"
                       + "\"keywords\":[\"DivineShield\",\"Lifesteal\",\"Taunt\",\"Armor\"]}]}";
            var bwCards = CardLoader.LoadCardsFromText(bwJson);
            Assert(bwCards.Count == 1
                   && bwCards[0].Keywords.Count == 4
                   && (bwCards[0].Effects ?? new List<CardEffectData>())
                       .TrueForAll(e => e.TriggerTiming != (int)CardCore.TriggerTiming.Activate_Active),
                   "多关键词保留印制（不转主动；无合成 ACT_ 自赋予效果）");
            CardCostService.EnsureCost(bwCards[0]);
            Assert(bwCards[0].Cost != null && !bwCards[0].Cost.IsZero && bwCards[0].Cost.v.Length == 5,
                   "多关键词生物建议费用非零且位置数组 5 位（P2 席位收缩口径）");
        }

    }
}
