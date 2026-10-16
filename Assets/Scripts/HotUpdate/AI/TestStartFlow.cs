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

        /// <summary>地牌素材卡：试作·元素井（1灰1红1蓝1绿 白板 1/1，四色候选任选锁色）。</summary>
        public const string LandMaterialCardId = "C_50E2F45D";

        /// <summary>预置临时钱包 [灰,红,蓝,绿,白,黑]（回合末清空——黑白费只能从钱包出）。</summary>
        public static readonly int[] SeedBank = { 0, 0, 0, 0, 5, 5 };

        /// <summary>专用测试卡组 30 张（2026-10-16）：入组口径=全部带效果、效果互不重复
        /// （共用同一效果 id 的多卡只取其一；同触发+同原子+同目标域仅数值不同的缩放版
        /// 视为重复）。构成：关键词系 18 + 效果生物 8 + 结界 1 + 主动法术 3。
        /// 落选说明：末日降临=24灰超单色地牌支付上限且与再生共用原子；双策术=火花+洞察
        /// 的复合（两效果各自入组）；火弹兽/烈焰涌兽=毁灭箭兽（T_BOLT4）的纯数值缩放。</summary>
        public static readonly string[] TestDeckCardIds =
        {
            // ---- 关键词系·原子直挂（登场生效，13）----
            "C_CA8D2651", // 试作·先攻（073f18c7 四卡代表：先攻/锋卫/剑士/刺客共用）
            "C_8BF4D663", // 试作·吸血（f7119394 四卡代表：吸血/血蚊/掠食狼/百夫长共用）
            "C_396A5A59", // 试作·缴械（c253788f 两卡代表：缴械/斗棍兵共用）
            "C_8DA3BBB6", // 试作·警戒（4cf22304 四卡代表：警戒/哨卫/骑手/盾卫共用）
            "C_9FAE2DD3", // 试作·连击
            "C_F370EA65", // 试作·隐密
            "C_BD21221A", // 试作·系命
            "C_C9B350E9", // 试作·再生
            "C_DFCFCB22", // 试作·守护
            "C_461950EF", // 试作·帷幕
            "C_DDA93C8E", // 试作·剧毒
            "C_CD5504C2", // 试作·禁魔石
            "C_77F4841E", // 试作·不灭
            // ---- 关键词系·库效果（登场时获得，5）----
            "C_8A711BD9", // 试作·坚韧
            "C_43AEF621", // 试作·潜行
            "C_EE336818", // 试作·法术护盾
            "C_B15EAE30", // 试作·圣盾
            "C_63F323B5", // 试作·复生
            // ---- 效果生物（8）----
            "C_40BA1423", // 活体火焰：死亡时抽1卡
            "C_89D96ECB", // 试作·毁灭箭兽：登场伤害4（T_BOLT4）
            "C_2BB8E3FF", // 试作·战号兽：重置横置（T_INSPIRE）
            "C_A2CFE97D", // 试作·集结号令兽：己方加攻指示物（T_RALLY）
            "C_349C1706", // 试作·磨刀兽：加攻指示物+2（T_BUFF2）
            "C_9CCA2EE2", // 试作·情报兽：抽卡（T_DRAW2）
            "C_37EDBD92", // 试作·急救兽：回血（T_HEAL4）
            "C_239B5411", // 试作·演武场兽：主动·伤害+加攻（T_EVODROME）
            // ---- 结界（1）----
            "C_57A3ADD6", // 纳川仪典：手牌上限15+免疫疲劳
            // ---- 主动法术（3）----
            "C_0CF04C1E", // 火花术：对敌方单位1伤
            "C_BD523DFD", // 强击术：己方单位攻击+1
            "C_D4FE2EF6", // 洞察术：抽1张牌
        };

        /// <summary>解析测试卡组（缺卡记警告、返回已解析子集；30 张全在池=常态）。</summary>
        public static List<CardData> ResolveTestDeck()
        {
            var cards = new List<CardData>(TestDeckCardIds.Length);
            foreach (var id in TestDeckCardIds)
            {
                var card = CardCatalog.GetById(id);
                if (card != null) cards.Add(card);
                else TideLog.Warn("[TestStartFlow] 测试卡组缺卡：" + id + "（以已解析 " + cards.Count + " 张继续）");
            }
            return cards;
        }

        /// <summary>「测试开局·地牌充满」入口：专用测试卡组（30 张效果全覆盖，缺卡回落
        /// 默认镜像回退链）洗牌 + 40 张素材卡组队，双方镜像开局；手牌=卡组前
        /// OpeningHandSize 张，素材卡全入地牌。</summary>
        public static void EnterFullLandsTest()
        {
            var deck = ResolveTestDeck();
            if (deck.Count < GameCore.OpeningHandSize)
            {
                TideLog.Warn("[TestStartFlow] 测试卡组不完整（" + deck.Count + "/" + TestDeckCardIds.Length
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
                BattleEntry.MirrorDeck = shuffled;
                BattleEntry.MirrorSkillCardId = null;
                return;
            }
            BattleEntry.Mode = BattleMode.LocalAI;
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

        /// <summary>Fisher-Yates 洗牌（测试局每局随机，不钉种子）。</summary>
        private static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
