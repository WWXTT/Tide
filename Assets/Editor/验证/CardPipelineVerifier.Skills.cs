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
    public static partial class CardPipelineVerifier
    {
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
