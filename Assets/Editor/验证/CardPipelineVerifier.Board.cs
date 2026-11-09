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

                // ---- 3. 受益者豁免：净化/沉默/失效打在受益者身上，光环不变 ----
                CardCore.Attribute.KeywordRules.PurifyKeywords(ben);
                ben.AddCounters(CardCore.Attribute.CounterRules.SilenceCounter, 1);
                ben.AddCounters(CardCore.Attribute.CounterRules.NullifyCounter, 1);
                GameBoard.LinkAuraSystem.InvalidateCache();
                Assert(ben.GetPower() == 5 && ben.HasKeyword("Taunt"),
                       "受益者不可被干扰：净化/沉默/失效都不压光环（光环不物化、不读持有者状态）");
                ben.RemoveCounters(CardCore.Attribute.CounterRules.SilenceCounter, 1);
                ben.RemoveCounters(CardCore.Attribute.CounterRules.NullifyCounter, 1);

                // ---- 4. 失效不压来源（2026-10-31 失效改名定案：光环域整体退出压制——
                //      来源被失效（原缄默，id 仍 Nullify）光环照常投递）；净化不压箭头（箭头=卡面数据）----
                src.AddCounters(CardCore.Attribute.CounterRules.NullifyCounter, 1, p1);
                GameBoard.LinkAuraSystem.InvalidateCache();
                Assert(ben.GetPower() == 5 && ben.HasKeyword("Taunt"),
                       "失效不压光环来源：加成照常（2026-10-31 定案——失效只压关键词与触发式）");
                src.RemoveCounters(CardCore.Attribute.CounterRules.NullifyCounter, 1);
                GameBoard.LinkAuraSystem.InvalidateCache();
                Assert(ben.GetPower() == 5 && ben.HasKeyword("Taunt"),
                       "对照：解除后光环仍常（上断言非偶然；净化也不压箭头——箭头是卡面数据）");

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

                // ---- 4e. 守护（2026-11-05 简化定案）：固定护角色——裸持有即挡刀（无弹选/配对门槛，
                //      AddKeyword 直挂同样生效；行为全量锚见 TestGuardianRelay 七段）----
                var roleGuard = MakePlain(p1, 2, 6);
                roleGuard.AddKeyword("Guardian", CardCore.KeywordLane.Setting, null, 1, 1);
                int p1LifeBefore = p1.Life;
                Attribute.KeywordRules.ApplyDamage(p2, p1, 4, false);
                Assert(p1.Life == p1LifeBefore && roleGuard.GetLife() == 2,
                       "守护简化：裸持有即挡刀（固定护角色——角色 4 伤全转守护者 6→2；旧配对制弹选门槛退役）");

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
                // 效果光环（2026-11-05）：effectRef 非空才追加 '#id'——分叉可辨且存量零漂移
                noAuraData.LinkAuras.Add(new LinkAuraData { effectRef = "VERIFY_AURAFX" });
                string hashWithFx = SynergyUI.ContentHasher.HashCard(noAuraData);
                Assert(hashWithFx != hashNoAura && hashWithFx != hashWithAura,
                       "效果光环内容哈希：effectRef 条目影响 ID（LA: 段 '#id' 追加——与 stat 条目亦分叉）");
                noAuraData.LinkAuras.Clear();
                Assert(SynergyUI.ContentHasher.HashCard(noAuraData) == hashNoAura,
                       "效果光环内容哈希：清空回落原值（纯 effectRef 条目不残留段——存量零漂移）");
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

                // ---- 9. 效果光环（2026-11-05）：effectRef 条目——覆盖期间受益者「视为持有」该触发效果。
                //      通道=关键词光环 live-query 同构：事件命中时触发引擎实时枚举合成条目
                //     （Source=受益单位——载荷过滤/{target} 自参照零改动对准受益者；受益者失效/沉睡照发、
                //      仅绝缘体特判剔除；不含自己=ReferenceEquals 闸继承；结界耐久不耗=Source 非结界载体）。----
                var drawRow = CardCore.Attribute.AtomicEffectTable.GetByEnumName("DrawCard")?.HashId ?? "";
                Assert(drawRow.Length > 0, "效果光环段前提：DrawCard 原子行在表");
                EffectSlimDto GrantDto(string id, int timing, int activation, List<CostRef> costs = null)
                    => new EffectSlimDto
                    {
                        id = id, name = id, timing = timing, activation = activation, costs = costs,
                        steps = new List<StepRef>
                        {
                            new StepRef { kind = 0, atom = new AtomicEffectEntry { refId = drawRow, value = 1 } },
                        },
                    };
                Assert(EffectEligibility.AuraGrantRejectReason(EffectSlim.ToCardEffect(
                           GrantDto("VERIFY_AU_OK", (int)TriggerTiming.OnTap, 1))) == null,
                       "效果光环资格谓词：横置族多次档·自动·无代价——可授予");
                Assert(EffectEligibility.AuraGrantRejectReason(EffectSlim.ToCardEffect(
                           GrantDto("VERIFY_AU_ONCE", (int)TriggerTiming.OnPlay, 1))) != null,
                       "效果光环资格谓词：单次时机（OnPlay）拒——只授予多次档");
                Assert(EffectEligibility.AuraGrantRejectReason(EffectSlim.ToCardEffect(
                           GrantDto("VERIFY_AU_PERIOD", (int)TriggerTiming.OnTurnEnd, 1))) != null,
                       "效果光环资格谓词：周期族（OnTurnEnd）拒——受益者=生物，生物臂结界专属口径");
                Assert(EffectEligibility.AuraGrantRejectReason(EffectSlim.ToCardEffect(
                           GrantDto("VERIFY_AU_ACT", (int)TriggerTiming.Activate_Active, 2))) != null,
                       "效果光环资格谓词：主动效果拒");
                Assert(EffectEligibility.AuraGrantRejectReason(EffectSlim.ToCardEffect(
                           GrantDto("VERIFY_AU_VOL", (int)TriggerTiming.OnTap, 2))) != null,
                       "效果光环资格谓词：可选触发（Voluntary）拒——授予=自动");
                Assert(EffectEligibility.AuraGrantRejectReason(EffectSlim.ToCardEffect(
                           GrantDto("VERIFY_AU_COST", (int)TriggerTiming.OnTap, 1,
                               new List<CostRef> { new CostRef { type = 1, value = 1 } }))) != null,
                       "效果光环资格谓词：带代价行拒——授予=免费自动效果（2026-11-05 拍板）");

                // 库注入（token _byId 反射注入同款先例；段末 finally 撤销防跨段泄漏）
                var byIdField = typeof(EffectsLibrary).GetField("_byId",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                var itemsField = typeof(EffectsLibrary).GetField("_items",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                Assert(byIdField != null && itemsField != null, "效果光环段前提：EffectsLibrary 私有表可反射");
                var grantDto = GrantDto("VERIFY_AURAFX", (int)TriggerTiming.OnTap, 1);
                var libById = (Dictionary<string, EffectSlimDto>)byIdField.GetValue(null);
                var libItems = (List<EffectSlimDto>)itemsField.GetValue(null);
                libById[grantDto.id] = grantDto;
                libItems.Add(grantDto);
                try
                {
                    Assert(EffectsLibrary.Resolve("VERIFY_AURAFX")?.Id == "VERIFY_AURAFX",
                           "效果光环库注入：VERIFY_AURAFX 可解析");

                    // 装载闸：悬空剔除（effectIds 同模式 Warn+跳过）+ 可授予存活
                    var fxLoad = CardLoader.LoadCardsFromText(@"{ ""cards"": [
  { ""cardName"": ""悬空效果光环"", ""supertype"": ""Enchantment"", ""arrows"": ""Up"",
    ""linkAuras"": [ { ""effectRef"": ""VERIFY_NOPE"" } ], ""keywords"": [], ""effects"": [] },
  { ""cardName"": ""生效效果光环"", ""supertype"": ""Enchantment"", ""arrows"": ""Up"",
    ""linkAuras"": [ { ""effectRef"": ""VERIFY_AURAFX"" } ], ""keywords"": [], ""effects"": [] } ] }");
                    var danglingLoaded = fxLoad.FirstOrDefault(c => c.CardName == "悬空效果光环");
                    var liveLoaded = fxLoad.FirstOrDefault(c => c.CardName == "生效效果光环");
                    Assert(danglingLoaded != null && danglingLoaded.LinkAuras.Count == 0,
                           "效果光环装载闸：悬空 effectRef 条目 Warn+剔除（三空外唯一剔除路径）");
                    Assert(liveLoaded != null && liveLoaded.LinkAuras.Count == 1
                           && liveLoaded.LinkAuras[0].effectRef == "VERIFY_AURAFX",
                           "效果光环装载闸：可授予效果引用条目经资格检查存活");

                    // 战场：载体（箭头+effectRef 条目）→ 箭头指向的受益生物
                    var fxBen = MakePlain(p1, 2, 4);
                    var fxSrc = MakePlain(p1, 1, 4);
                    var fxDir = DirBetween(fxSrc, fxBen, out bool fxAdj);
                    Assert(fxAdj, "效果光环段：载体与受益者落位相邻前提");
                    DataOf(fxSrc).LinkAuras.Add(new LinkAuraData { effectRef = "VERIFY_AURAFX" });
                    DataOf(fxSrc).ArrowDirections = GameBoard.BoardMath.ArrowOf(fxDir);
                    GameBoard.LinkAuraSystem.InvalidateCache();
                    Assert(GameBoard.LinkAuraSystem.AnyEffectGrantSources(), "效果光环：效果授予源可检出（触发引擎早退口）");
                    Assert(GameBoard.LinkAuraSystem.GetGrantedEffectAuras(fxBen).Count == 1
                           && GameBoard.LinkAuraSystem.GetGrantedEffectAuras(fxSrc).Count == 0,
                           "效果光环覆盖判定：箭头指向的受益者命中 1 条；不含自己（ReferenceEquals 闸继承）");

                    var publishRouted = typeof(GameCore).GetMethod("PublishEventRouted",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    Assert(publishRouted != null, "效果光环段前提：GameCore.PublishEventRouted 可反射（internal 事件路由单口）");
                    void FireTap(Card c) => publishRouted.Invoke(core,
                        new object[] { new TapEvent { TappedEntity = c, IsUntapping = false }, false });
                    int HandOf(Player p) => core.ZoneManager.GetCards(p, Zone.Hand).Count;
                    Assert(core.ZoneManager.GetCards(p1, Zone.Deck).Count > 0, "效果光环段前提：p1 牌库非空（抽牌断言前提）");

                    // E2E：受益者横置 → 授予效果入栈结算（控制者=受益者控制者；元素费不逐次收=ElementCostPrepaid）
                    int h0 = HandOf(p1);
                    FireTap(fxBen);
                    GameActions.DrainStack(core);
                    Assert(HandOf(p1) == h0 + 1,
                           "效果光环E2E：箭头指向的单位横置→视为持有「横置时抽1」入栈结算");

                    // 压制照发：受益者带失效+沉睡仍触发（FromAuraGrant 豁免 IsAutoSuppressed 闸——2026-11-05 拍板）
                    fxBen.AddCounters(Attribute.CounterRules.NullifyCounter, 1);
                    fxBen.AddCounters(Attribute.KeywordRules.SleepCounter, 1);
                    int h1 = HandOf(p1);
                    FireTap(fxBen);
                    GameActions.DrainStack(core);
                    Assert(HandOf(p1) == h1 + 1,
                           "效果光环压制语义：受益者失效/沉睡照发（仅绝缘体特判）");
                    fxBen.RemoveCounters(Attribute.CounterRules.NullifyCounter, 1);
                    fxBen.RemoveCounters(Attribute.KeywordRules.SleepCounter, 1);

                    // 重复授予=各触发一次（每源声明条数×箭头叠加——与关键词口径一致）
                    DataOf(fxSrc).LinkAuras.Add(new LinkAuraData { effectRef = "VERIFY_AURAFX" });
                    GameBoard.LinkAuraSystem.InvalidateCache();
                    Assert(GameBoard.LinkAuraSystem.GetGrantedEffectAuras(fxBen).Count == 2,
                           "效果光环重复授予：同源双条目=命中 2 条");
                    int h2 = HandOf(p1);
                    FireTap(fxBen);
                    GameActions.DrainStack(core);
                    Assert(HandOf(p1) == h2 + 2, "效果光环重复授予：双条目各触发一次（抽 2）");

                    // 绝缘体特判：不受施加的效果影响——整卡不收效果授予（Temp 轨挂载便于净化还原）
                    fxBen.AddKeyword(Attribute.KeywordRules.Insulator, CardCore.KeywordLane.Temp, null, 1, 1);
                    Assert(GameBoard.LinkAuraSystem.GetGrantedEffectAuras(fxBen).Count == 0,
                           "效果光环绝缘体特判：绝缘体受益者不收效果授予（stat/keyword 条目不受此判）");
                    int h3 = HandOf(p1);
                    FireTap(fxBen);
                    GameActions.DrainStack(core);
                    Assert(HandOf(p1) == h3, "效果光环绝缘体特判：横置不触发授予效果");
                    Attribute.KeywordRules.PurifyKeywords(fxBen); // 清 Temp 轨绝缘体（净化不动光环 live-query）
                    Assert(GameBoard.LinkAuraSystem.GetGrantedEffectAuras(fxBen).Count == 2,
                           "效果光环对照：绝缘体净化后授予恢复（非偶然）");

                    // 断链停止（live-query 无物化）：载体离场即停授
                    core.ZoneManager.MoveCard(fxSrc, p1, Zone.Battlefield, Zone.Graveyard);
                    Assert(GameBoard.LinkAuraSystem.GetGrantedEffectAuras(fxBen).Count == 0,
                           "效果光环断链：载体离场=零授予（live-query 无物化可失联）");
                    int h4 = HandOf(p1);
                    FireTap(fxBen);
                    GameActions.DrainStack(core);
                    Assert(HandOf(p1) == h4, "效果光环断链：无覆盖时横置不触发");
                }
                finally
                {
                    libById.Remove(grantDto.id);
                    libItems.Remove(grantDto);
                }
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
            Assert(edge == 6 && unit == 18 && land == 8 && special == 17,
                   $"布局计数 49 = 边缘{edge}（x0 列四格 z0/z2/z4/z6 + 中线两端 (0,3)/(6,3)） + 单位{unit}（每方 4+5） + 地牌{land}（四色带×2） + 特殊{special}（含中线行5、角色/场地/墓地/除外/卡组/万能格各×2；2026-10-29 P11 盘面重塑 6765676→当晚对座镜像修订）");

            // 特殊格（2026-10-29 对座镜像：六格行同列带 x1..x6——左列 x0 只剩角色/墓地两格，
            // 行尾 x6=场地斜上邻/除外/卡组收边；P2 = MirrorSeat 逐一对应；万能格=Any、场地格=Field）
            Assert(BoardLayout.RoleOf(0, 1) == CellRole.Character && BoardLayout.OwnerOf(0, 1) == 0, "P1 角色格 = (0,1)");
            Assert(BoardLayout.RoleOf(1, 2) == CellRole.Field && BoardLayout.OwnerOf(1, 2) == 0, "P1 场地格 = (1,2)（第三行首格·角色斜上邻·规则归 P8）");
            Assert(BoardLayout.RoleOf(6, 2) == CellRole.Exile && BoardLayout.OwnerOf(6, 2) == 0, "P1 除外格 = (6,2)（墓地正上邻）");
            Assert(BoardLayout.RoleOf(6, 1) == CellRole.Graveyard && BoardLayout.OwnerOf(6, 1) == 0, "P1 墓地格 = (6,1)");
            Assert(BoardLayout.RoleOf(6, 0) == CellRole.Deck && BoardLayout.OwnerOf(6, 0) == 0, "P1 卡组格 = (6,0)");
            Assert(BoardLayout.RoleOf(1, 0) == CellRole.Any && BoardLayout.OwnerOf(1, 0) == 0, "P1 万能展示格 = (1,0)（Any·只做展示）");
            Assert(BoardLayout.RoleOf(6, 5) == CellRole.Character && BoardLayout.OwnerOf(6, 5) == 1, "P2 角色格 = (6,5)");
            Assert(BoardLayout.RoleOf(6, 4) == CellRole.Field && BoardLayout.OwnerOf(6, 4) == 1, "P2 场地格 = (6,4)");
            Assert(BoardLayout.RoleOf(1, 4) == CellRole.Exile && BoardLayout.OwnerOf(1, 4) == 1, "P2 除外格 = (1,4)");
            Assert(BoardLayout.RoleOf(0, 5) == CellRole.Graveyard && BoardLayout.OwnerOf(0, 5) == 1, "P2 墓地格 = (0,5)");
            Assert(BoardLayout.RoleOf(1, 6) == CellRole.Deck && BoardLayout.OwnerOf(1, 6) == 1, "P2 卡组格 = (1,6)");
            Assert(BoardLayout.RoleOf(6, 6) == CellRole.Any && BoardLayout.OwnerOf(6, 6) == 1, "P2 万能展示格 = (6,6)");
            Assert(BoardLayout.RoleOf(0, 0) == CellRole.Edge && BoardLayout.OwnerOf(0, 0) == -1, "P1 六格行行首格 (0,0) = 缺口（无 tile）");
            Assert(BoardLayout.RoleOf(0, 2) == CellRole.Edge && BoardLayout.OwnerOf(0, 2) == -1, "P1 六格行行首格 (0,2) = 缺口");
            Assert(BoardLayout.RoleOf(0, 4) == CellRole.Edge && BoardLayout.OwnerOf(0, 4) == -1, "P2 六格行行首格 (0,4) = 缺口");
            Assert(BoardLayout.RoleOf(0, 6) == CellRole.Edge && BoardLayout.OwnerOf(0, 6) == -1, "P2 六格行行首格 (0,6) = 缺口");
            bool middleOk = true;
            for (int x = 0; x < BoardMath.Width; x++)
            {
                var expect = x >= BoardLayout.MiddleStartX && x < BoardLayout.MiddleStartX + BoardLayout.MiddleCellCount
                    ? CellRole.Middle : CellRole.Edge;
                if (BoardLayout.RoleOf(x, 3) != expect || BoardLayout.OwnerOf(x, 3) != -1) middleOk = false;
            }
            Assert(middleOk, "中线行 z3 = 5 格 Middle（x1..x5 居中）+ x0/x6 缺口 Edge·归属-1（P11）");

            bool symmetric = true;
            for (int z = 0; z < BoardMath.Height && symmetric; z++)
                for (int x = 0; x < BoardMath.Width && symmetric; x++)
                {
                    if (z == BoardLayout.MiddleZ) continue; // 中线状态行=刻意豁免（表现层，玩法归属恒 -1）
                    // 缺口格（x0 列/中线两端）不在六格行带 x1..x6 内——MirrorSeat 的 x→7−x 只对
                    // 带内有定义（x0 会映到 7 越界）；缺口的对座像=同列 z 翻转（x0 列缺口两侧自对称）
                    var (rx, rz) = BoardLayout.NoTileAt(x, z) ? (x, BoardMath.Height - 1 - z) : BoardLayout.MirrorSeat(x, z);
                    if (BoardLayout.RoleOf(rx, rz) != BoardLayout.RoleOf(x, z)) symmetric = false;
                    int o = BoardLayout.OwnerOf(x, z);
                    if (o != -1 && BoardLayout.OwnerOf(rx, rz) != 1 - o) symmetric = false;
                }
            Assert(symmetric, "全盘对座镜像对称（MirrorSeat：中线行豁免；角色一致、归属互补——2026-10-29 晚定案取代 180° 旋转）");
            Assert(BoardLayout.UnitCellsPerPlayer == ZoneManager.BattlefieldCapacityPerPlayer,
                   "单位格数 9 = 核心战场容量 9（计数与格子天然一致）");
            var unitCells0 = BoardLayout.UnitCells(0);
            Assert(unitCells0.Count == 9
                   && unitCells0[0] == (2, 2) && unitCells0[3] == (5, 2)
                   && unitCells0[4] == (1, 1) && unitCells0[8] == (5, 1)
                   && BoardLayout.LandCellsPerPlayer == 4
                   && BoardLayout.LandCells(0)[0].Item1 == BoardLayout.LandBandStartX
                   && BoardLayout.LandCells(0)[3].Item1 == BoardLayout.LandBandStartX + 3,
                   "容量与几何一致（单位两行 z2 x2..x5 + z1 x1..x5=9；地牌四色带 x2..x5——2026-10-29 对座镜像，配置漂移即红）");

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

            // ---- 5. 施放流转（S3b 2026-11-04 删发动区口径）：事件序 / 终态 ----
            // 卡施放中**留在来源区**（手牌），「施放中」由栈条目表示；Enter/Leave Activation 事件
            // 已整链退役（定义已删）——出牌全程只发 CardPlayEvent（使用宣言）。
            var pool1 = core.ElementPool.GetPool(p1);
            FillSixColors(pool1, 99);
            var creatureData = cardsData.FirstOrDefault(c => c.CardName == "古树守卫");
            if (creatureData != null)
            {
                var creature = new CardWrapper(creatureData);
                creature.SetController(p1);
                core.ZoneManager.GetZoneContainer(p1).Add(creature, Zone.Hand);

                var order = new List<string>();
                System.Action<CardPlayEvent> onPlay = _ => order.Add("Play");
                EventManager.Instance.Subscribe(onPlay);
                try
                {
                    Assert(PlayCardSync(core, p1, creature), "棋盘段生物成功打出");
                }
                finally
                {
                    EventManager.Instance.Unsubscribe(onPlay);
                }

                Assert(order.SequenceEqual(new[] { "Play" }),
                       $"S3b 施放事件序只有 Play（Enter/Leave 已随发动区退役；实际 {string.Join("→", order)}）");
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
                Assert(!core.StackEngine.IsPendingCast(spell), "法术结算后不再施放中（栈条目已消费）");
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
            Assert(!core.ZoneManager.HasBattlefieldSpace(p1), "战场填满 9 后无空位（P11 容量）");

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
            // 信息族原子已入原子表（JSON → 运行时表全链路；2026-10-30 族改版：箭头宣言表行退役——
            // DeclareHand=强夺 / DeclareDeckTop=预言 改版行仍在，DeclareArrow 表行已删；
            // 2026-10-31 窃取 DeclareDeck 入伙=四原子）
            Assert(CardCore.Attribute.AtomicEffectTable.GetByEnumName("DeclareHand") != null
                   && CardCore.Attribute.AtomicEffectTable.GetByEnumName("DeclareDeck") != null
                   && CardCore.Attribute.AtomicEffectTable.GetByEnumName("DeclareDeckTop") != null
                   && CardCore.Attribute.AtomicEffectTable.GetByEnumName("DeclareArrow") == null
                   && CardCore.Attribute.AtomicEffectTable.GetByEnumName("ProphecyNextCard") != null,
                   "信息族 4 原子在表 + 箭头宣言退役（2026-10-30 强夺/预言改版·10-31 窃取入伙）");
            Assert(CardCore.Attribute.AtomicEffectTable.GetByHashId("b4d07877")?.ManaList != null
                   && CardCore.Attribute.AtomicEffectTable.GetByHashId("b4d07877").ManaList[ManaType.Blue] == 5f
                   && CardCore.Attribute.AtomicEffectTable.GetByHashId("5d05c712")?.DisplayName == "窃取"
                   && CardCore.Attribute.AtomicEffectTable.GetByHashId("5d05c712").ManaList[ManaType.Blue] == 3f,
                   "窃取入表（ID 5d05c712·蓝3）+ 强夺改价蓝5（2026-10-31）");
            Assert(CardCore.Attribute.EffectHandlerRegistry.TryGetHandler(CardCore.AtomicEffectType.DeclareDeck, out _),
                   "窃取 handler 已注册（DeclareDeckHandler）");
            // 剧毒（2026-10-08 转关键词 GrantVenom）与毒素/沉默/净化同链入表
            //（穿透伤害行已删 2026-10-31 定案——绕过伤害生态位由 LifeDown 降上限承担）
            Assert(CardCore.Attribute.AtomicEffectTable.GetByEnumName("GrantVenom") != null
                   && CardCore.Attribute.AtomicEffectTable.GetByEnumName("AddToxin") != null
                   && CardCore.Attribute.AtomicEffectTable.GetByEnumName("Silence") != null
                   && CardCore.Attribute.AtomicEffectTable.GetByEnumName("Purify") != null
                   && CardCore.Attribute.AtomicEffectTable.GetByEnumName("SetCost") != null,
                   "新语义五原子已入表（剧毒关键词/毒素/沉默/净化/设置费用）");
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
            Assert(!ProphecyDimension.IsValidValue("LinkArrow", "Up"), "维度 LinkArrow 已退役（2026-11-03 定案——不再设计猜箭头的卡）");
            Assert(ProphecyDimension.TryParse("Type:Creature", out var dim, out var val) && dim == "Type" && val == "Creature", "编码解析 维度:值");
        }

        /// <summary>
        /// 信息族引擎流验证（合成卡，不依赖卡表；2026-10-30 族改版随更——箭头宣言退役）：
        /// - 强夺：DeclareHand（Name:卡名）→ 命中=同名卡跨玩家迁入己方手牌+持有者永久改写，
        ///   未命中=空手而归（DeclareHit 分支即时结算照挂）；
        /// - 窃取（2026-10-31 新原子）：DeclareDeck（Name:卡名）→ 同构强夺、源区换对手牌库；
        /// - 预言（改版原子）：DeclareDeckTop（隐秘类别押注）→ 对手下回合首张同类卡
        ///   = 内置发动无效（CardPlayEvent 时点卡在发动区 → 直送墓地=cast 消费点中止）；
        /// - 卡名预言（引擎主干）：ProphecyNextCard + ProphecyHit 门 → 延迟注册（押注隐藏），
        ///   对手下回合首张出牌验证命中/未命中；整回合未出牌作未命中走 else
        ///   （2026-11-02 押注行收紧：只收类别三选一，奖励预算 3——夹具随改 Type 押注）。
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
            // 裸 CardWrapper 不经装载管线，运行时 Cost 恒 0（Color 维度匹配读运行时费用——
            // 2026-10-30 预言段翻新补设；Type/Name 走数据面不受影响）
            ((IHasCost)foeCreature).Cost[ManaType.Red] = 2;
            ((IHasCost)foeSpell).Cost[ManaType.Blue] = 1;

            // p1 牌库放 3 张白卡（DrawCard 奖励的可观测载体）
            for (int i = 0; i < 3; i++) container1.Add(new Card { ID = "VERIFY_INFO_DECK" }, Zone.Deck);

            var executor = core.StackEngine.GetExecutor();

            // ---- A. 强夺（2026-10-30 改版）：宣言 Name:验证生物 → 命中=夺取+持有者改写，分支抽 1 ----
            var declareDef = new EffectDefinition
            {
                Id = "VERIFY_DECLARE",
                TriggerTiming = TriggerTiming.Activate_Active,
                Steps = new List<RuntimeEffectStep>
                {
                    new RuntimeEffectStep
                    {
                        Kind = RuntimeStepKind.Atomic,
                        Atomic = new AtomicEffectInstance { Type = AtomicEffectType.DeclareHand, StringValue = "Name:验证生物" },
                    },
                    new RuntimeEffectStep
                    {
                        Kind = RuntimeStepKind.Branch,
                        ConditionId = "DeclareHit",
                        Then = { new AtomicEffectInstance { Type = AtomicEffectType.DrawCard, Value = 1 } },
                    },
                },
            };

            int p1HandBefore = core.ZoneManager.GetCards(p1, Zone.Hand).Count;
            int p2HandBefore = core.ZoneManager.GetCards(p2, Zone.Hand).Count;
            executor.ExecuteAsync(new EffectInstance
            {
                Definition = declareDef,
                Source = p1,
                Controller = p1,
                Targets = new List<Entity>(),
            }).Forget();
            Assert(core.ZoneManager.GetCards(p2, Zone.Hand).Count == p2HandBefore - 1
                   && core.ZoneManager.GetCards(p1, Zone.Hand).Contains(foeCreature),
                   "强夺命中：对手手牌同名卡迁入己方手牌");
            Assert(foeCreature.GetOwner() == p1 && foeCreature.GetController() == p1,
                   "强夺夺取=永久改写持有者（弹回/洗回/死亡归新主）");
            Assert(core.ZoneManager.GetCards(p1, Zone.Hand).Count == p1HandBefore + 2,
                   "强夺命中（夺取 1 + DeclareHit 分支抽 1）→ 手牌 +2");

            // ---- A2. 强夺未命中：宣言对手手牌没有的卡名 → 空手而归、分支不结算 ----
            var declareMissDef = new EffectDefinition
            {
                Id = "VERIFY_DECLARE_MISS",
                TriggerTiming = TriggerTiming.Activate_Active,
                Steps = new List<RuntimeEffectStep>
                {
                    new RuntimeEffectStep
                    {
                        Kind = RuntimeStepKind.Atomic,
                        Atomic = new AtomicEffectInstance { Type = AtomicEffectType.DeclareHand, StringValue = "Name:无此卡" },
                    },
                    new RuntimeEffectStep
                    {
                        Kind = RuntimeStepKind.Branch,
                        ConditionId = "DeclareHit",
                        Then = { new AtomicEffectInstance { Type = AtomicEffectType.DrawCard, Value = 1 } },
                    },
                },
            };

            p1HandBefore = core.ZoneManager.GetCards(p1, Zone.Hand).Count;
            p2HandBefore = core.ZoneManager.GetCards(p2, Zone.Hand).Count;
            executor.ExecuteAsync(new EffectInstance
            {
                Definition = declareMissDef,
                Source = p1,
                Controller = p1,
                Targets = new List<Entity>(),
            }).Forget();
            Assert(core.ZoneManager.GetCards(p1, Zone.Hand).Count == p1HandBefore
                   && core.ZoneManager.GetCards(p2, Zone.Hand).Count == p2HandBefore,
                   "强夺未命中：空手而归，无夺取无奖励");

            // ---- A3. 窃取（2026-10-31 新原子 DeclareDeck）：宣言 Name → 命中=对手牌库同名卡
            //      迁入己方手牌+持有者永久改写；未命中=空手而归（牌库同构强夺的源区换库） ----
            var treasureData = new CardData { ID = "VERIFY_INFO_TREASURE", CardName = "验证秘藏" };
            treasureData.Supertype = Cardtype.Spell;
            treasureData.Cost[ManaType.Blue] = 1;
            var foeTreasure = new CardWrapper(treasureData);
            foeTreasure.SetController(p2);
            container2.Add(foeTreasure, Zone.Deck);

            var stealDef = new EffectDefinition
            {
                Id = "VERIFY_STEAL",
                TriggerTiming = TriggerTiming.Activate_Active,
                Steps = new List<RuntimeEffectStep>
                {
                    new RuntimeEffectStep
                    {
                        Kind = RuntimeStepKind.Atomic,
                        Atomic = new AtomicEffectInstance { Type = AtomicEffectType.DeclareDeck, StringValue = "Name:验证秘藏" },
                    },
                    new RuntimeEffectStep
                    {
                        Kind = RuntimeStepKind.Branch,
                        ConditionId = "DeclareHit",
                        Then = { new AtomicEffectInstance { Type = AtomicEffectType.DrawCard, Value = 1 } },
                    },
                },
            };

            p1HandBefore = core.ZoneManager.GetCards(p1, Zone.Hand).Count;
            int p2DeckBefore = core.ZoneManager.GetCards(p2, Zone.Deck).Count;
            executor.ExecuteAsync(new EffectInstance
            {
                Definition = stealDef,
                Source = p1,
                Controller = p1,
                Targets = new List<Entity>(),
            }).Forget();
            Assert(core.ZoneManager.GetCards(p2, Zone.Deck).Count == p2DeckBefore - 1
                   && core.ZoneManager.GetCards(p1, Zone.Hand).Contains(foeTreasure),
                   "窃取命中：对手牌库同名卡迁入己方手牌");
            Assert(foeTreasure.GetOwner() == p1 && foeTreasure.GetController() == p1,
                   "窃取=永久改写持有者（与强夺同款 _owner 随迁）");
            Assert(core.ZoneManager.GetCards(p1, Zone.Hand).Count == p1HandBefore + 2,
                   "窃取命中（窃取 1 + DeclareHit 分支抽 1）→ 手牌 +2");

            // ---- A4. 窃取未命中：宣言对手牌库没有的卡名 → 空手而归、分支不结算 ----
            var stealMissDef = new EffectDefinition
            {
                Id = "VERIFY_STEAL_MISS",
                TriggerTiming = TriggerTiming.Activate_Active,
                Steps = new List<RuntimeEffectStep>
                {
                    new RuntimeEffectStep
                    {
                        Kind = RuntimeStepKind.Atomic,
                        Atomic = new AtomicEffectInstance { Type = AtomicEffectType.DeclareDeck, StringValue = "Name:无此卡" },
                    },
                    new RuntimeEffectStep
                    {
                        Kind = RuntimeStepKind.Branch,
                        ConditionId = "DeclareHit",
                        Then = { new AtomicEffectInstance { Type = AtomicEffectType.DrawCard, Value = 1 } },
                    },
                },
            };

            p1HandBefore = core.ZoneManager.GetCards(p1, Zone.Hand).Count;
            p2DeckBefore = core.ZoneManager.GetCards(p2, Zone.Deck).Count;
            executor.ExecuteAsync(new EffectInstance
            {
                Definition = stealMissDef,
                Source = p1,
                Controller = p1,
                Targets = new List<Entity>(),
            }).Forget();
            Assert(core.ZoneManager.GetCards(p1, Zone.Hand).Count == p1HandBefore
                   && core.ZoneManager.GetCards(p2, Zone.Deck).Count == p2DeckBefore,
                   "窃取未命中：空手而归，无窃取无奖励");

            // ---- B0. 预言（2026-10-30 改版原子 DeclareDeckTop）：隐秘押注 类别:法术 →
            //      对手下回合首张同类卡（法术）→ 内置发动无效（发动区送墓=cast 消费点中止） ----
            var prophecyAtomDef = new EffectDefinition
            {
                Id = "VERIFY_PROPHECY_ATOM",
                TriggerTiming = TriggerTiming.Activate_Active,
                Steps = new List<RuntimeEffectStep>
                {
                    new RuntimeEffectStep
                    {
                        Kind = RuntimeStepKind.Atomic,
                        Atomic = new AtomicEffectInstance { Type = AtomicEffectType.DeclareDeckTop, StringValue = "Type:Spell" },
                    },
                },
            };

            ProphecySystem.Reset();
            int handBefore = core.ZoneManager.GetCards(p1, Zone.Hand).Count;
            executor.ExecuteAsync(new EffectInstance
            {
                Definition = prophecyAtomDef,
                Source = p1,
                Controller = p1,
                Targets = new List<Entity>(),
            }).Forget();
            Assert(ProphecySystem.Pending.Count == 1, "预言（改版原子）已注册为待验证押注");

            // 模拟对手回合（S3b 口径 2026-11-05）：真实施放声明——卡留手牌+cast 上栈；
            // CardPlayEvent 验证命中 → 内置发动无效打标 cast（RespondedEntry 绑定被验证卡）→
            // DrainStack 结算时消费点中止：不付费、手牌直送墓地
            EnsureMainPhase(core, p2);
            Assert(GameActions.PlayCard(core, p2, foeSpell), "预言验证：对手首张法术真实施放声明");
            GameActions.DrainStack(core);
            Assert(core.ZoneManager.GetCards(p2, Zone.Graveyard).Contains(foeSpell),
                   "预言命中：对手首张同类卡（法术）发动无效——cast 消费点中止送墓（S3b：不经发动区）");
            Assert(ProphecySystem.Pending.Count == 0, "预言（改版原子）验证后出列");

            // ---- B. 预言·押注行（2026-11-02 收紧类别档·延迟验证）：押注 类别:生物，命中走 then 抽 1 ----
            var prophecyDef = new EffectDefinition
            {
                Id = "VERIFY_PROPHECY",
                TriggerTiming = TriggerTiming.Activate_Active,
                Steps = new List<RuntimeEffectStep>
                {
                    new RuntimeEffectStep
                    {
                        Kind = RuntimeStepKind.Atomic,
                        Atomic = new AtomicEffectInstance { Type = AtomicEffectType.ProphecyNextCard, StringValue = "Type:Creature" },
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

            // 模拟对手回合开始 → 首张打出生物 → 验证命中
            EventManager.Instance.Publish(new TurnStartEvent { TurnPlayer = p2, TurnNumber = 99 });
            EventManager.Instance.Publish(new CardPlayEvent { Player = p2, PlayedCard = foeCreature });
            Assert(core.ZoneManager.GetCards(p1, Zone.Hand).Count == handBefore + 1,
                   "对手首张出生物 → 预言（类别:生物）命中 → then 抽 1（验证时刻结算）");
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
                        Atomic = new AtomicEffectInstance { Type = AtomicEffectType.ProphecyNextCard, StringValue = "Type:Spell" },
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

            // ---- D. 预言隐秘性（2026-11-02 定案）：宣言时刻类别不可见（模板/产出不回显），
            //      登记行不含押注内容；结算时刻战报公开押注类别与结果 ----
            Assert(Attribute.EffectHandlerRegistry.TryGetHandler(AtomicEffectType.ProphecyNextCard, out var prophecyHandler),
                   "预言押注行 handler 已注册（隐秘性断言前置）");
            var hiddenCtx = new EffectExecutionContext { Controller = p1 };
            hiddenCtx.LastOutcome.Declaration = "Type:Creature";
            hiddenCtx.LastOutcome.HiddenDeclaration = true;
            string castText = prophecyHandler.GetDescription(
                new AtomicEffectInstance { Type = AtomicEffectType.ProphecyNextCard, StringValue = "Type:Creature" },
                hiddenCtx);
            Assert(!castText.Contains("Type:") && !castText.Contains("Creature"),
                   $"预言施放描述不回显押注类别（隐秘：『{castText}』）");
            string regLine = MatchLogRenderer.Render(new ProphecyRegisteredEvent { Declarer = p1 });
            Assert(regLine.Contains("预言") && !regLine.Contains("Type:") && !regLine.Contains("生物"),
                   $"预言登记行不含押注内容（『{regLine}』）");
            string hitLine = MatchLogRenderer.Render(new ProphecyResolvedEvent
                { Declarer = p1, Declaration = "Type:Creature", Hit = true, VerifiedCard = foeCreature });
            Assert(hitLine.Contains("类别：生物") && hitLine.Contains("命中") && hitLine.Contains("验证生物"),
                   $"预言揭晓行公开押注类别与验证卡（『{hitLine}』）");
            string missLine = MatchLogRenderer.Render(new ProphecyResolvedEvent
                { Declarer = p1, Declaration = "Type:Spell", Hit = false, VerifiedCard = null });
            Assert(missLine.Contains("类别：法术") && missLine.Contains("未出牌"),
                   $"预言到期行=押注类别+整回合未出牌（『{missLine}』）");

            // 清理：合成卡不残留（强夺后生物/窃取后秘藏在 p1 手牌；预言无效后法术在 p2 墓地）
            container1.Remove(foeCreature, Zone.Hand);
            container1.Remove(foeTreasure, Zone.Hand);
            container2.Remove(foeSpell, Zone.Graveyard);
            ProphecySystem.Reset();
        }

    }
}
