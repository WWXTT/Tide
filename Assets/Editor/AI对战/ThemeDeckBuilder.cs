using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CardCore;
using CardCore.AI;
using UnityEditor;
using UnityEngine;

namespace CardCore.Editor.Tests
{
    /// <summary>
    /// 三色主题卡组构建器（2026-09-21 策略模式配套）：一次性/幂等生成红·赤红疾袭、绿·翠绿深林、
    /// 蓝·苍蓝壁垒三套 30 张互不重复的主题卡组，落盘为**用户数据**（README 数据分层定案）：
    /// · 卡 → CardConfigSerializer.Save 逐张 upsert 进 StreamingAssets/Card/Cards.json
    ///   （effectIds 引用 + Effects.json upsert——三层引用链，不手写 JSON、不进 Configs）；
    /// · 卡组 → DeckSerializer.Save 落 StreamingAssets/Card/&lt;name&gt;.json（DeckData=卡 ID 引用数组）。
    /// 幂等：主题卡全部带 Theme* 标签——重跑先剔除旧主题卡再写新（改设计不残留旧卡）；
    /// 内容哈希 ID 天然按功能去重。构筑合规：原子全用表行 refId 引用、内容契约（错边只进代价栏、
    /// 代价 ≤1 费）、声明费缺省走 EnsureCost 建议档（规则一 D≤C 由全表巡检验证）。
    /// </summary>
    public static class ThemeDeckBuilder
    {
        public const string RedDeckName = "赤红疾袭（红·快攻）";
        public const string GreenDeckName = "翠绿深林（绿·慢速）";
        public const string BlueDeckName = "苍蓝壁垒（蓝·控制）";

        private const string RedTag = "ThemeRed";
        private const string GreenTag = "ThemeGreen";
        private const string BlueTag = "ThemeBlue";

        // 召唤衍生物模板占位（构建两遍式：先建身份再解析为卡 ID）
        private const string HoundToken = "TOKEN_HOUND";   // 红：灰烬猎犬
        private const string GoblinToken = "TOKEN_GOBLIN"; // 红：火花哥布林

        [MenuItem("Tools/构建三色主题卡组")]
        public static void Build()
        {
            var red = BuildRedDeck();
            var green = BuildGreenDeck();
            var blue = BuildBlueDeck();

            // ---- 1) 逐卡建议费（EnsureCost 写 ID/Cost；内容身份哈希同管线建立）----
            foreach (var c in red.Concat(green).Concat(blue))
            {
                c.Cost = null; // 建议档口径（声明优先惯例不适用于构建器——重跑以最新推导为准）
                CardCostService.EnsureCost(c);
            }

            // ---- 2) 衍生物模板占位 → 真实卡 ID（C_+HashCard，与 CardConfigSerializer.ToEntry 同源）----
            ResolveTokenTemplate(red, HoundToken, "灰烬猎犬");
            ResolveTokenTemplate(red, GoblinToken, "火花哥布林");

            // ---- 3) 落盘：剔旧主题卡 → 保留其余 → 全量重写（RegenOneDeck 同模式）----
            string cardsPath = Path.Combine(Application.streamingAssetsPath, "Card", "Cards.json");
            var keep = SynergyUI.CardConfigSerializer.LoadAll()
                .Where(c => c.Tags == null || !c.Tags.Any(t => t == RedTag || t == GreenTag || t == BlueTag))
                .ToList();
            File.WriteAllText(cardsPath, "{\n    \"cards\": [],\n    \"deckConfig\": {\n        \"copiesPerCard\": 1\n    }\n}");
            foreach (var c in keep.Concat(red).Concat(green).Concat(blue))
                SynergyUI.CardConfigSerializer.Save(c);

            // ---- 4) 卡组落盘（DeckData=卡 ID 引用数组）----
            SaveDeck(RedDeckName, red);
            SaveDeck(GreenDeckName, green);
            SaveDeck(BlueDeckName, blue);

            // ---- 5) 读侧缓存刷新（2026-09-21 修复：写盘后静态缓存不失效——同 domain 内
            //      EffectsLibrary/CardCatalog 仍解析旧条目，卡变白板）----
            CardCore.EffectsLibrary.Reload();
            SynergyUI.CardCatalog.Invalidate();

            Debug.Log($"[主题卡组] 完成：红{red.Count}/绿{green.Count}/蓝{blue.Count} 张（保留既有非主题卡 {keep.Count} 张）；" +
                      $"卡表 {cardsPath}；卡组目录 StreamingAssets/Card/。曲线见上方逐卡日志。");
        }

        // ======================================== 红 · 赤红疾袭（快攻） ========================================

        /// <summary>整体低费生物、高攻低命；激励重置横置实现多次攻击；全体加攻；代价铺场；直伤收尾。</summary>
        private static List<CardData> BuildRedDeck()
        {
            var deck = new List<CardData>();
            void Creature(string name, int power, int life, params string[] keywords)
                => deck.Add(MakeCreature(name, power, life, RedTag, keywords));

            // —— 低费高攻低命生物（曲线 1-3，数值梯度互异）——
            Creature("灰烬猎犬", 2, 1);                 // 兼衍生物模板（召唤产它）
            Creature("火花哥布林", 3, 1);               // 兼衍生物模板
            Creature("烬鳞蜥", 3, 2);
            Creature("血怒新兵", 2, 1, "FirstStrike");
            Creature("灼锋斥候", 3, 1, "FirstStrike");
            Creature("狂焰斗士", 4, 2, "Disarm");
            Creature("血牙狼人", 3, 2, "Lifesteal");
            Creature("赤鬃掠夺者", 4, 1);
            Creature("熔岩突击兵", 5, 2);
            Creature("烬痕刺客", 3, 1, "Lifesteal");
            Creature("焚风骑士", 4, 3);
            Creature("火焰先驱", 3, 2, "FirstStrike");
            Creature("暴怒角蜥", 5, 1);
            Creature("裂地獒", 4, 2, "FirstStrike");
            Creature("焰尾狐", 3, 1, "FirstStrike");
            Creature("焰爪狼", 2, 1, "FirstStrike");       // 补足 30：梯度变体
            Creature("赤鳞迅猛龙", 4, 2, "Disarm");

            // —— 战吼：全体加攻（登场即涨，铺场后一波推脸）——
            deck.Add(MakeCreature("战嚎统领", 4, 3, RedTag,
                onPlay: MakeWholeBuff("R_WARCRY", 1)));

            // —— 激励法术（Untap 重置横置——让攻击力最高的生物多次攻击；策略 RedAggro 择目标）——
            deck.Add(MakeSpell("激励·冲锋号角", RedTag, MakeEffect("R_INSPIRE_1",
                AtomRefs.New(AtomicEffectType.Untap))));
            deck.Add(MakeSpell("激励·战鼓", RedTag, MakeEffect("R_INSPIRE_2",
                AtomRefs.New(AtomicEffectType.Untap))));
            deck.Add(MakeSpell("激励·血战到底", RedTag, MakeEffect("R_INSPIRE_3",
                AtomRefs.New(AtomicEffectType.Untap))));

            // —— 整体加攻（全取档·己方域）——
            deck.Add(MakeSpell("全军突击", RedTag, MakeWholeBuff("R_RALLY_1", 1)));
            deck.Add(MakeSpell("血性狂热", RedTag, MakeWholeBuff("R_RALLY_2", 1)));
            deck.Add(MakeSpell("战意勃发", RedTag, MakeEffect("R_BUFF_SINGLE",
                AtomRefs.New(AtomicEffectType.AddPowerUp, 2, Kinds(1))))); // 单体 +2 攻

            // —— 代价铺场（效果=召唤己方；代价=≤1 费错边原子，强制执行+黑白补偿）——
            deck.Add(MakeSpell("献祭召唤", RedTag, MakeEffect("R_TOKEN_SWARM",
                AtomRefs.New(AtomicEffectType.SummonToken, 2, str: HoundToken)),
                payload: AtomRefs.New(AtomicEffectType.AddToxin, 1, Kinds(1)))); // 给己方单位上毒（有害锁己=错边 0.5费）
            deck.Add(MakeSpell("急行军召援", RedTag, MakeEffect("R_TOKEN_REINFORCE",
                AtomRefs.New(AtomicEffectType.SummonToken, 1, str: GoblinToken)),
                payload: AtomRefs.New(AtomicEffectType.Freeze, 1, Kinds(1)))); // 冻结己方单位（有害锁己=错边 1费）

            // —— 直伤（打脸/补刀；策略在斩杀窗口提最高优先）——
            deck.Add(MakeSpell("烈焰箭", RedTag, MakeEffect("R_BOLT_2",
                AtomRefs.New(AtomicEffectType.DealDamage, 2, Kinds(2)))));
            deck.Add(MakeSpell("焚城", RedTag, MakeEffect("R_BOLT_3",
                AtomRefs.New(AtomicEffectType.DealDamage, 3, Kinds(2)))));
            deck.Add(MakeSpell("致命齐射", RedTag, MakeEffect("R_BOLT_4",
                AtomRefs.New(AtomicEffectType.DealDamage, 4, Kinds(2)))));
            deck.Add(MakeSpell("灼烧之触", RedTag, MakeEffect("R_BOLT_5",
                AtomRefs.New(AtomicEffectType.DealDamage, 5, Kinds(2)))));

            return deck;
        }

        // ======================================== 绿 · 翠绿深林（慢速） ========================================

        /// <summary>回血护甲存活、休眠/攒费、大生物（碾压 AOE）逆转场面。</summary>
        private static List<CardData> BuildGreenDeck()
        {
            var deck = new List<CardData>();
            void Creature(string name, int power, int life, params string[] keywords)
                => deck.Add(MakeCreature(name, power, life, GreenTag, keywords));

            // —— 前中期墙与回血生物 ——
            Creature("苔壳龟", 1, 4);
            Creature("藤蔓守望者", 2, 4, "Growth");
            Creature("森林贤者", 2, 5);
            Creature("春藤蔓延者", 2, 3, "Growth");
            Creature("蜜露祭司", 2, 4, "Lifelink");
            Creature("古木卫士", 3, 6);
            Creature("荆棘树妖", 3, 5, "Growth");
            Creature("沼泽医者", 3, 5, "Lifelink");
            Creature("苍翠巨熊", 5, 5);
            Creature("岩背巨龟", 4, 7);

            // —— 大生物终结（碾压=攻击溅射相邻=AOE 踩场；不灭=站场）——
            Creature("翠冠鹿王", 5, 6, "Lifelink");
            Creature("蛮荒树人", 6, 7);
            Creature("荆棘岭巨兽", 6, 8, "Overwhelm");
            Creature("荒野震颤者", 7, 7, "Overwhelm");
            Creature("沼泽泰坦", 7, 8, "Indestructible");
            Creature("林地始祖", 8, 9, "Overwhelm");
            Creature("世界树之影", 9, 9, "Overwhelm", "Indestructible");

            // —— 回血/护甲法术（策略 GreenRamp 低血时提最高优先；治疗指向受伤最重己方）——
            deck.Add(MakeSpell("晨露", GreenTag, MakeEffect("G_HEAL_3",
                AtomRefs.New(AtomicEffectType.Heal, 3, Kinds(1)))));
            deck.Add(MakeSpell("治愈之息", GreenTag, MakeEffect("G_HEAL_4",
                AtomRefs.New(AtomicEffectType.Heal, 4, Kinds(1)))));
            deck.Add(MakeSpell("自然抚慰", GreenTag, MakeEffect("G_HEAL_6",
                AtomRefs.New(AtomicEffectType.Heal, 6, Kinds(1)))));
            deck.Add(MakeSpell("生命涌泉", GreenTag, MakeEffect("G_HEAL_8",
                AtomRefs.New(AtomicEffectType.Heal, 8, Kinds(1)))));
            deck.Add(MakeSpell("木甲术", GreenTag, MakeEffect("G_ARMOR_2",
                AtomRefs.New(AtomicEffectType.AddArmor, 2, Kinds(1)))));
            deck.Add(MakeSpell("磐石庇护", GreenTag, MakeEffect("G_ARMOR_3",
                AtomRefs.New(AtomicEffectType.AddArmor, 3, Kinds(1)))));
            deck.Add(MakeSpell("荒野回响", GreenTag, MakeEffect("G_HEAL_5",
                AtomRefs.New(AtomicEffectType.Heal, 5, Kinds(1)))));
            deck.Add(MakeSpell("活体盔甲", GreenTag, MakeEffect("G_LIFEUP_2",
                AtomRefs.New(AtomicEffectType.AddLifeUp, 2, Kinds(1)))));

            // —— 攒费（光合=得 1 绿元素；采掘=地牌换元素；苏醒=灰费转沉睡攒大生物）——
            deck.Add(MakeSpell("光合滋养", GreenTag, MakeEffect("G_PHOTO",
                AtomRefs.New(AtomicEffectType.AdditionalEnergy)))); // 反查末行=光合（得 1 绿）
            deck.Add(MakeSpell("深林苏醒", GreenTag, MakeEffect("G_SLEEP_SAVE",
                AtomRefs.New(AtomicEffectType.Sleep)))); // 反查末行=苏醒（自身灰费转沉睡）
            deck.Add(MakeSpell("采掘", GreenTag, MakeEffect("G_MINE",
                new AtomicEffectEntry { refId = "4fd4c954", value = 1 }))); // 手写行 ID：同枚举多行，AtomRefs 取末行拿不到采掘

            // —— 清场（大生物站稳后收掉对面铺场；费用须 ≤ 地牌上限 9，超出即死卡）——
            deck.Add(MakeSpell("荆棘风暴", GreenTag, MakeEffect("G_SWEEP_2",
                AtomRefs.New(AtomicEffectType.DealDamage, 2, Kinds(2)),
                whole: true)));
            deck.Add(MakeSpell("藤蔓绞杀", GreenTag, MakeEffect("G_SWEEP_1",
                AtomRefs.New(AtomicEffectType.DealDamage, 1, Kinds(2)),
                whole: true)));

            return deck;
        }

        // ======================================== 蓝 · 苍蓝壁垒（控制） ========================================

        /// <summary>有害指示物控/灭对面大生物；中等生物换小生物；抽牌发现续资源；弹回解场。</summary>
        private static List<CardData> BuildBlueDeck()
        {
            var deck = new List<CardData>();
            void Creature(string name, int power, int life, params string[] keywords)
                => deck.Add(MakeCreature(name, power, life, BlueTag, keywords));

            // —— 中等生物（警戒/法术护盾站场；攻击策略 BlueControl=中等换小）——
            Creature("霜翼哨兵", 2, 3, "Vigilance");
            Creature("寒潮术士", 2, 4);
            Creature("潮汐预言者", 2, 4, "Vigilance");
            Creature("深海密探", 3, 3, "Vigilance");
            Creature("迷雾行者", 3, 4, "Vigilance");
            Creature("秘法守望者", 3, 5);
            Creature("冰川执政官", 4, 5, "SpellShield");
            Creature("苍蓝哨兵", 4, 6);
            Creature("霜语者", 4, 4, "Vigilance");
            Creature("星辉贤者", 3, 6, "SpellShield");
            Creature("凛冬领主", 5, 5);
            Creature("深渊凝视者", 5, 6);
            Creature("秘法织网者", 3, 4, "SpellShield");   // 补足 30：梯度变体
            Creature("潮汐守护者", 4, 5, "Vigilance");

            // —— 有害指示物（策略 BlueControl 对面有生物时最高优先；目标=威胁降序=大生物）——
            deck.Add(MakeSpell("剧毒之触", BlueTag, MakeEffect("B_POISON",
                AtomRefs.New(AtomicEffectType.Poison, 1, Kinds(2)))));       // 剧毒：回合末死亡（灭大生物）
            deck.Add(MakeSpell("蔓延毒雾", BlueTag, MakeEffect("B_TOXIN_2",
                AtomRefs.New(AtomicEffectType.AddToxin, 2, Kinds(2)))));
            deck.Add(MakeSpell("蚀骨毒潭", BlueTag, MakeEffect("B_TOXIN_3",
                AtomRefs.New(AtomicEffectType.AddToxin, 3, Kinds(2)))));
            deck.Add(MakeSpell("寒冰锁链", BlueTag, MakeEffect("B_FREEZE_1",
                AtomRefs.New(AtomicEffectType.Freeze, 1, Kinds(2)))));
            deck.Add(MakeSpell("冰封禁锢", BlueTag, MakeEffect("B_FREEZE_2",
                AtomRefs.New(AtomicEffectType.Freeze, 1, Kinds(2)))));
            deck.Add(MakeSpell("沉默诅咒", BlueTag, MakeEffect("B_SILENCE",
                AtomRefs.New(AtomicEffectType.Silence, 1, Kinds(2)))));
            deck.Add(MakeSpell("混乱低语", BlueTag, MakeEffect("B_DISARRAY_1",
                AtomRefs.New(AtomicEffectType.RushSickness, 1, Kinds(2))))); // 紊乱：不能以角色为目标
            deck.Add(MakeSpell("迷失心智", BlueTag, MakeEffect("B_DISARRAY_2",
                AtomRefs.New(AtomicEffectType.RushSickness, 2, Kinds(2)))));
            deck.Add(MakeSpell("破甲标记", BlueTag, MakeEffect("B_VULN_1",
                AtomRefs.New(AtomicEffectType.AddVulnerable, 1, Kinds(2)))));
            deck.Add(MakeSpell("易伤诅咒", BlueTag, MakeEffect("B_VULN_2",
                AtomRefs.New(AtomicEffectType.AddVulnerable, 1, Kinds(2)))));

            // —— 弹回解场（大生物回手拖延节奏）——
            deck.Add(MakeSpell("回流", BlueTag, MakeEffect("B_BOUNCE_1",
                AtomRefs.New(AtomicEffectType.ReturnToHand, 1, Kinds(2)))));
            deck.Add(MakeSpell("回溯之潮", BlueTag, MakeEffect("B_BOUNCE_2",
                AtomRefs.New(AtomicEffectType.ReturnToHand, 1, Kinds(2)))));

            // —— 资源（策略手牌吃紧时提分）——
            deck.Add(MakeSpell("深海汲取", BlueTag, MakeEffect("B_DRAW_1",
                AtomRefs.New(AtomicEffectType.DrawCard, 1))));
            deck.Add(MakeSpell("奥术智慧", BlueTag, MakeEffect("B_DRAW_2",
                AtomRefs.New(AtomicEffectType.DrawCard, 2))));
            deck.Add(MakeSpell("禁忌知识", BlueTag, MakeEffect("B_DRAW_2B",
                AtomRefs.New(AtomicEffectType.DrawCard, 2))));
            deck.Add(MakeSpell("星图研读", BlueTag, MakeEffect("B_DISCOVER",
                AtomRefs.New(AtomicEffectType.DiscoverCard, 3))));

            return deck;
        }

        // ======================================== 构卡小工厂 ========================================

        private static List<int> Kinds(params int[] kinds) => kinds.ToList();

        private static CardData MakeCreature(string name, int power, int life, string tag,
            string[] keywords = null, CardEffectData onPlay = null)
        {
            var data = new CardData
            {
                CardName = name,
                Supertype = Cardtype.Creature,
                Power = power,
                Life = life,
                Keywords = keywords != null && keywords.Length > 0 ? keywords.ToList() : new List<string>(),
                Tags = new List<string> { tag },
            };
            if (onPlay != null) data.Effects.Add(onPlay);
            return data;
        }

        private static CardData MakeSpell(string name, string tag, CardEffectData effect,
            AtomicEffectEntry payload = null)
        {
            if (payload != null)
            {
                // 代价栏（单卡单条·≤1 费错边）：付费步强制执行 + 黑/白全价补偿
                effect.Costs = new List<CostEntry>
                {
                    new CostEntry { CostType = (int)CostType.Payload, payload = payload },
                };
            }
            var data = new CardData
            {
                CardName = name,
                Supertype = Cardtype.Spell,
                Tags = new List<string> { tag },
            };
            data.Effects.Add(effect);
            return data;
        }

        /// <summary>OnPlay 法术效果（Duration=Once 即时结算）。</summary>
        private static CardEffectData MakeEffect(string id, AtomicEffectEntry atom, bool whole = false)
        {
            return new CardEffectData
            {
                Id = id,
                DisplayName = id,
                TriggerTiming = (int)TriggerTiming.OnPlay,
                Duration = (int)DurationType.Once,
                SelectionMode = whole ? (int)SelectionMode.Whole : (int)SelectionMode.Single,
                TargetCount = 1,
                AtomicEffects = new List<AtomicEffectEntry> { atom },
            };
        }

        /// <summary>全取档己方全体加攻（Whole+域{1}）。</summary>
        private static CardEffectData MakeWholeBuff(string id, int amount)
            => MakeEffect(id, AtomRefs.New(AtomicEffectType.AddPowerUp, amount, Kinds(1)), whole: true);

        /// <summary>衍生物模板占位替换为真实卡 ID（C_+HashCard——与 CardConfigSerializer.ToEntry 同源）。</summary>
        private static void ResolveTokenTemplate(List<CardData> deck, string placeholder, string templateName)
        {
            var template = deck.FirstOrDefault(c => c.CardName == templateName);
            if (template == null)
            {
                Debug.LogError($"[主题卡组] 衍生物模板卡缺失：{templateName}");
                return;
            }
            string templateId = "C_" + SynergyUI.ContentHasher.HashCard(template);
            foreach (var card in deck)
            {
                if (card.Effects == null) continue;
                foreach (var eff in card.Effects)
                {
                    if (eff?.AtomicEffects == null) continue;
                    foreach (var atom in eff.AtomicEffects)
                        if (atom?.str == placeholder) atom.str = templateId;
                }
            }
        }

        private static void SaveDeck(string name, List<CardData> cards)
        {
            // 卡组=卡 ID 引用数组（DeckData）；逐卡打印建议费供曲线核对
            var deck = new SynergyUI.DeckData(name)
            {
                cardIds = cards.Select(c => "C_" + SynergyUI.ContentHasher.HashCard(c)).ToList(),
            };
            SynergyUI.DeckSerializer.Save(deck);
            var curve = cards.Select(c => c.Cost != null ? (int)c.Cost.Values.Sum() : 0)
                .OrderBy(v => v).ToList();
            Debug.Log($"[主题卡组] {name}：{cards.Count} 张；费用曲线 {string.Join("/", curve)}；" +
                      $"明细：{string.Join("、", cards.Select(c => $"{c.CardName}({(c.Cost != null && c.Cost.Count > 0 ? string.Join("+", c.Cost.Select(kv => $"{(ManaType)kv.Key}{kv.Value:0}")) : "0")})"))}");
        }
    }
}
