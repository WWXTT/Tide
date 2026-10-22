using System.Collections.Generic;
using System.Linq;
using CardCore;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>
    /// 测试开局（2026-10-16）：把教学「预设场面直入」管线（TutorialScenarioSeeder）解耦为
    /// 通用测试功能——主菜单一键进入预设局面，供人工实测卡牌效果。入口只写 BattleEntry
    /// （同 TutorialFlow 口径），跳转由调用方（MainMenuScreen）负责。
    /// 首个预设「地牌充满」：双方各 40 张锁色地牌（灰/红/蓝/绿各 10）+ 黑白钱包 5白5黑
    /// （黑白费只能从临时钱包出——地牌锁色不含黑白；钱包回合末清空），起跳回合 10 主阶段。
    /// 地牌素材=专用四色卡「试作·元素井」：AddCardToPool 显式锁色必须在卡自身费用色候选内，
    /// 卡池原无四色卡故新建（费用不入内容哈希，ID=ContentHasher.HashCard 口径）。
    /// 上限：自然曲线封顶 10 之外走 TutorialSideState.landCapBonus（成长同口径 LandCapBonus）。
    /// </summary>
    public static class TestStartFlow
    {
        /// <summary>起跳全局回合号（≥10 即达自然曲线封顶，之后恒 10+加值）。</summary>
        public const int StartTurn = 10;

        /// <summary>地牌槽上限加值：自然 10 + 30 = 40（入池闸/支付浓度/黑白生成钳同随）。</summary>
        public const int LandCapBonus = 30;

        /// <summary>每种颜色的锁色地牌张数（灰/红/蓝/绿四色）。</summary>
        public const int LandsPerColor = 10;

        /// <summary>地牌素材卡：试作·元素井（2026-10-22 恒推导改版：1灰1红1蓝2绿 1/1 警戒+入场直伤/潜行，
        /// 落教学表 TutorialCards.json——主表用户池被并行重建不再承载；四色候选任选锁色）。</summary>
        public const string LandMaterialCardId = "C_50E2F45D";

        /// <summary>预置临时钱包 [灰,红,蓝,绿,白,黑]（回合末清空——黑白费只能从钱包出）。</summary>
        public static readonly int[] SeedBank = { 0, 0, 0, 0, 5, 5 };

        /// <summary>测试卡组引用文件（2026-10-17 卡组文件化）：StreamingAssets/Tutorial/测试卡组.json
        /// （{name, cardIds} 红·快攻同款格式——id 引用即是卡组，可重复）。原 30 张入组口径（2026-10-16，
        /// 随数组退役移此备查）：全部带效果、效果互不重复（共用同一效果 id 的多卡只取其一；同触发+同原子+
        /// 同目标域仅数值不同的缩放版视为重复）；构成=关键词系 18 + 效果生物 8 + 结界 1 + 主动法术 3；
        /// 落选说明：末日降临=24灰超单色地牌支付上限且与再生共用原子；双策术=火花+洞察的复合；
        /// 火弹兽/烈焰涌兽=毁灭箭兽（T_BOLT4）的纯数值缩放。</summary>
        public const string TestDeckFile = "测试卡组";

        /// <summary>解析测试卡组（读引用文件；缺卡记警告、返回已解析子集）。</summary>
        public static List<CardData> ResolveTestDeck()
        {
            var ids = TutorialLibrary.LoadDeckIds(TestDeckFile);
            var cards = new List<CardData>(ids.Count);
            foreach (var id in ids)
            {
                var card = CardCatalog.GetById(id);
                if (card != null) cards.Add(card);
                else TideLog.Warn("[TestStartFlow] 测试卡组缺卡：" + id + "（以已解析 " + cards.Count + " 张继续）");
            }
            return cards;
        }

        /// <summary>「测试开局·地牌充满」入口：专用测试卡组（引用文件 30 张效果全覆盖，缺卡回落
        /// 默认镜像回退链）洗牌 + 40 张素材卡组队，双方镜像开局；手牌=卡组前
        /// OpeningHandSize 张，素材卡全入地牌。</summary>
        public static void EnterFullLandsTest()
        {
            var deck = ResolveTestDeck();
            if (deck.Count < GameCore.OpeningHandSize)
            {
                TideLog.Warn("[TestStartFlow] 测试卡组不完整（" + deck.Count
                    + "）——回落默认镜像卡组回退链");
                deck = TutorialCreationFlow.ResolveDefaultMirrorDeck();
            }
            var shuffled = new List<CardData>(deck);
            Shuffle(shuffled);
            var material = CardCatalog.GetById(LandMaterialCardId);
            if (material == null)
            {
                TideLog.Error("[TestStartFlow] 地牌素材卡缺失：" + LandMaterialCardId + "（不设 Scenario，回落普通镜像局）");
                BattleEntry.Mode = BattleMode.LocalAI;
                BattleEntry.StagedOpening = false; // 测试路径=立即启动（2026-10-19 防五段开场旗标残留）
                BattleEntry.DummyTest = false;     // 同上口径：本入口非木桩
                BattleEntry.MirrorDeck = shuffled;
                BattleEntry.MirrorSkillCardId = null;
                return;
            }
            BattleEntry.Mode = BattleMode.LocalAI;
            BattleEntry.StagedOpening = false; // 测试路径=立即启动（2026-10-19 防五段开场旗标残留）
            BattleEntry.DummyTest = false;     // 同上口径：本入口非木桩（防上次木桩局残留串模式）
            BattleEntry.MirrorDeck = BuildTestDeck(shuffled, material);
            BattleEntry.MirrorSkillCardId = HeroSkillSystem.AutoPickSkillCard(deck);
            BattleEntry.Scenario = BuildFullLandsScenario(shuffled);
        }

        /// <summary>测试卡组 = 洗牌后的用户卡组 + 40 张素材卡（卡组可重复，引擎侧无张数上限）。</summary>
        private static List<CardData> BuildTestDeck(List<CardData> shuffledUserCards, CardData material)
        {
            var deck = new List<CardData>(shuffledUserCards);
            for (int i = 0; i < LandsPerColor * 4; i++) deck.Add(material);
            return deck;
        }

        /// <summary>「地牌充满」预设：双方对称同构；手牌/剩余牌库=测试卡组，地牌=素材卡四色各 10。</summary>
        private static TutorialScenario BuildFullLandsScenario(List<CardData> shuffledUserCards)
        {
            int handSize = Mathf.Max(1, GameCore.OpeningHandSize);
            return new TutorialScenario
            {
                startTurn = StartTurn,
                firstPlayer = "P1",
                player = BuildSide(shuffledUserCards, handSize),
                ai = BuildSide(shuffledUserCards, handSize),
            };
        }

        private static TutorialSideState BuildSide(List<CardData> shuffledDeck, int handSize)
        {
            return new TutorialSideState
            {
                bank = (int[])SeedBank.Clone(),
                landCapBonus = LandCapBonus,
                hand = shuffledDeck.Take(handSize).Select(c => c.ID).ToList(),
                deck = shuffledDeck.Skip(handSize).Select(c => c.ID).ToList(),
                lands = BuildLandSpecs(),
            };
        }

        private static List<TutorialLandSpec> BuildLandSpecs()
        {
            var lands = new List<TutorialLandSpec>();
            foreach (var color in new[] { "Gray", "Red", "Blue", "Green" })
                for (int i = 0; i < LandsPerColor; i++)
                    lands.Add(new TutorialLandSpec { card = LandMaterialCardId, tokenType = color });
            return lands;
        }

        /// <summary>Fisher-Yates 洗牌（测试局每局随机，不钉种子）。公有=木桩测试 DummyTestFlow 复用。</summary>
        public static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }

    /// <summary>
    /// 木桩驱动器（2026-10-22 木桩测试）：对方回合全程不动作——跳产元素阶段、排干栈、直接结束
    /// 回合（收尾镜像 ScriptedAi「无剧本」口径：栈未空时 EndTurn 被栈空守卫拒绝，故先排干）。
    /// 不出牌/不攻击/不放地；回合开始触发式效果照常由引擎事件驱动（真实条件不受木桩影响）。
    /// 响应窗口全让过（守卫/连锁永不反制）由 BattleController.EnablePassiveAiResponder 承担
    ///（PassOption 哨兵——返回 null 会回落内置守卫启发）。
    /// </summary>
    public sealed class DummyAi : IAiTurnDriver
    {
        /// <summary>同步入口（无头验证同型驱动口径）。全链同步——木桩无任何异步动作。</summary>
        public void TakeTurn(BattleController ctrl)
            => TakeTurnAsync(ctrl).GetAwaiter().GetResult();

        public Cysharp.Threading.Tasks.UniTask TakeTurnAsync(BattleController ctrl)
        {
            var core = ctrl.Core;
            var me = ctrl.TurnPlayer;
            if (core != null && me != null && me == core.TurnEngine.TurnPlayer)
            {
                GameActions.SkipElementPool(core, me); // 跳准备阶段进主阶段（回合推进必需）
                GameActions.DrainStack(core);          // 排干在途触发（EndTurn 前置契约）
                if (!core.IsGameOver)
                    GameActions.EndTurn(core, me);
            }
            return Cysharp.Threading.Tasks.UniTask.CompletedTask;
        }
    }

    /// <summary>
    /// 木桩测试开局装配（2026-10-22）：把五段式开局段五交班的双卡组按面板选项组装成引擎参数。
    /// 元素池充满=TestStartFlow「地牌充满」预设的按卡组版——双方独立（各用其所选卡组组场，
    /// 不再钉死测试卡组）；洗牌开关注入前自洗（场景管线 InitGame 恒锁牌库序，序由场景
    /// hand/deck 列表自身决定——镜像 EnterFullLandsTest 先洗后建场景口径）。
    /// </summary>
    public static class DummyTestFlow
    {
        /// <summary>充满元素池开局组装：out 场景 + 已并 40 素材地的双方卡组（SeedSide 按此取卡）。
        /// 素材卡缺失返回 false（调用方回落常规开局路径，不炸局）。playerFirst 编入场景
        /// firstPlayer（场景管线不走 StartNewGame 的 playerFirst 参数）。
        /// 洗牌=同一份洗好的列表双用（场景 hand/deck 序 + 引擎卡组序）——只洗场景副本会让
        /// 剩余牌库保持原文件序（SeedSide 按牌库实例序取卡），洗牌开关失效。</summary>
        public static bool TryBuildFullPool(List<CardData> myDeck, List<CardData> aiDeck, bool shuffle,
            bool playerFirst, out TutorialScenario scenario,
            out List<CardData> seededMyDeck, out List<CardData> seededAiDeck)
        {
            scenario = null;
            seededMyDeck = null;
            seededAiDeck = null;
            var material = CardCatalog.GetById(TestStartFlow.LandMaterialCardId);
            if (material == null)
            {
                TideLog.Error("[DummyTestFlow] 地牌素材卡缺失：" + TestStartFlow.LandMaterialCardId
                    + "（元素池充满回落常规开局）");
                return false;
            }
            var myList = new List<CardData>(myDeck);
            var aiList = new List<CardData>(aiDeck);
            if (shuffle)
            {
                TestStartFlow.Shuffle(myList);
                TestStartFlow.Shuffle(aiList);
            }
            scenario = new TutorialScenario
            {
                startTurn = TestStartFlow.StartTurn,
                firstPlayer = playerFirst ? "P1" : "P2",
                player = BuildSide(myList),
                ai = BuildSide(aiList),
            };
            seededMyDeck = WithMaterials(myList, material);
            seededAiDeck = WithMaterials(aiList, material);
            return true;
        }

        /// <summary>单侧状态：手牌/剩余牌库=（已按开关洗好的）所选卡组前后段，地牌=素材卡
        /// 四色各 10，钱包 5白5黑 + 地牌槽上限 30 加值（EnterFullLandsTest 同款对称口径）。</summary>
        private static TutorialSideState BuildSide(List<CardData> orderedDeck)
        {
            int handSize = Mathf.Max(1, GameCore.OpeningHandSize);
            return new TutorialSideState
            {
                bank = (int[])TestStartFlow.SeedBank.Clone(),
                landCapBonus = TestStartFlow.LandCapBonus,
                hand = orderedDeck.Take(handSize).Select(c => c.ID).ToList(),
                deck = orderedDeck.Skip(handSize).Select(c => c.ID).ToList(),
                lands = BuildLands(),
            };
        }

        /// <summary>场景管线卡组=所选卡组 + 40 张素材地（SeedSide 从牌库按 id 取卡：手牌/牌库/地牌全在此）。</summary>
        private static List<CardData> WithMaterials(List<CardData> deck, CardData material)
        {
            var seeded = new List<CardData>(deck);
            for (int i = 0; i < TestStartFlow.LandsPerColor * 4; i++) seeded.Add(material);
            return seeded;
        }

        private static List<TutorialLandSpec> BuildLands()
        {
            var lands = new List<TutorialLandSpec>();
            foreach (var color in new[] { "Gray", "Red", "Blue", "Green" })
                for (int i = 0; i < TestStartFlow.LandsPerColor; i++)
                    lands.Add(new TutorialLandSpec { card = TestStartFlow.LandMaterialCardId, tokenType = color });
            return lands;
        }
    }
}
