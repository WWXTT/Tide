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
        // ======================================== 统一计价锚点（规则一·平衡） ========================================

        /// <summary>
        /// 统一计价锚点验证（纯函数，不依赖卡表/对局）：
        /// 表值（GrantTaunt Green——2026-10-28 P2a 改色 / d(C) 分段曲线）、身材 1费=2属性、法术同折、回2命=0费、
        /// 卡层组合费用（可支配底盘 2026-10-10：超槽加价+剩余抵价/提速/结界二向——原「选发额外折」
        /// 与「剩余作废」均已废）、构筑代价抵消（无"超模"态）、关键词同享 d(C)、缺省档位 Ĉ。
        /// </summary>
        // ======================================== 合成器模型（2026-09-14 合成器重做） ========================================

        /// <summary>
        /// 合成器重做模型断言（纯模型层——UI 交互留 Unity 手测；2026-10-05 两槽定案重锚）：
        /// ①MountKind 8 值重排 + 引擎参数/预算锚（引擎=槽级 Branch 载荷，主干行已删）；
        /// ②挂载位数据驱动（位 6 光环/位 4 系统内部/生物·法术可赋予行=TargetFilter 语义）；
        /// ③槽级载荷端到端（settle=3 引擎：Then 转存/零计价/倒计时换算——合成器产出形态=TestBranchEngines 数据形态）；
        /// ④条件目录三族（产出条件/局面门/引擎）与预算口径；⑤并列保序（Steps 平铺折叠）；
        /// ⑥描述动态渲染（值随机区间文本）；⑦哈希口径（amp/br{} 载荷段参与去重）。
        /// </summary>
        private static void TestComposerModel()
        {
            // ---- 1. MountKind 值域（2026-10-31 墓碑清理：原位 7/9 空洞删除，紧凑重排 0..9）----
            Assert(System.Linq.Enumerable.Range(0, 10).All(v => System.Enum.IsDefined(typeof(CardCore.MountKind), v)),
                   "MountKind 值域 0..9 连续（无空洞——退役位已实删，不留占位）");
            Assert((int)CardCore.MountKind.Keyword == 0
                   && (int)CardCore.MountKind.Counter == 1
                   && (int)CardCore.MountKind.NoRandom == 2
                   && (int)CardCore.MountKind.LinkAura == 3
                   && (int)CardCore.MountKind.SystemInternal == 4
                   && (int)CardCore.MountKind.EngineTrunk == 5
                   && (int)CardCore.MountKind.RuleAura == 6
                   && (int)CardCore.MountKind.NoLinkAura == 7
                   && (int)CardCore.MountKind.SituationGate == 8
                   && (int)CardCore.MountKind.DynamicCost == 9,
                   "MountKind 位锚：Keyword=0/Counter=1/NoRandom=2/LinkAura=3/SystemInternal=4/EngineTrunk=5/RuleAura=6/NoLinkAura=7/SituationGate=8/DynamicCost=9");
            var dualAuraKinds = CardCore.MountKindExtensions.ParseCsv("关键词,可以作为连接光环");
            var noAuraKinds = CardCore.MountKindExtensions.ParseCsv("不可作为连接光环");
            Assert(dualAuraKinds.Count == 2
                   && dualAuraKinds.Contains(CardCore.MountKind.Keyword)
                   && dualAuraKinds.Contains(CardCore.MountKind.LinkAura)
                   && noAuraKinds.Count == 1
                   && noAuraKinds.Contains(CardCore.MountKind.NoLinkAura)
                   && CardCore.MountKindExtensions.ParseCsv("7").Count == 1
                   && CardCore.MountKindExtensions.ParseCsv("7").Contains(CardCore.MountKind.NoLinkAura)
                   && CardCore.MountKindExtensions.ParseCsv("8").Count == 1
                   && CardCore.MountKindExtensions.ParseCsv("8").Contains(CardCore.MountKind.SituationGate)
                   && CardCore.MountKindExtensions.ParseCsv("9").Count == 1
                   && CardCore.MountKindExtensions.ParseCsv("9").Contains(CardCore.MountKind.DynamicCost)
                   && CardCore.MountKindExtensions.ParseCsv("有限分支").Count == 1
                   && CardCore.MountKindExtensions.ParseCsv("有限分支").Contains(CardCore.MountKind.SituationGate),
                   "MountKinds 词表：可（3）/不可（7）中文与数字双轨解析通过；数字轨 7/8/9 分别命中 NoLinkAura/有限分支/动态费用");
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
            // 光环关键词资格（2026-11-05 用户定案：位 7「不可作为连接光环」黑名单整链退役，
            // 关键词表重建后不再携带拉黑位——黑名单/守护拉黑/下拉排除三组断言同批删除，
            // 下方只保留「关键词默认可」正向锚）
            Assert(CardCore.ComposerCatalog.IsAuraMountableKeyword("Lifesteal")
                   && CardCore.ComposerCatalog.IsAuraMountableKeyword("Taunt")
                   && CardCore.ComposerCatalog.IsAuraMountableKeyword("Concealed")
                   && CardCore.ComposerCatalog.IsAuraMountableKeyword("DoubleStrike"),
                   "光环可挂判定（2026-10-07 深夜）：吸血/帷幕/隐密/连击（关键词默认可）可挂"
                   + "（坚韧已指示物化、守护已配对制拉黑——2026-10-08 双双退出，引用随摘）");
            // 光环条目资格数据（2026-10-07 深夜终版）：关键词行默认可；
            // 2026-10-08 圣盾/潜行/复生指示物化——三行退出关键词族改位 1 指示物（硬拒光环），
            // 位 7 黑名单构成归并行表改面（名单在表漂移中，本锚只钉三行不在黑名单）
            var grantRows = CardCore.Attribute.AtomicEffectTable.GetAll()
                .Where(r => r != null && !string.IsNullOrEmpty(r.EnumName) && r.EnumName.StartsWith("Grant")).ToList();
            Assert(grantRows.Where(r => CardCore.ComposerCatalog.HasMountBit(r, CardCore.MountKind.Keyword)
                                         && !CardCore.ComposerCatalog.HasMountBit(r, CardCore.MountKind.NoLinkAura))
                            .All(r => CardCore.ComposerCatalog.CanMountAsAura(r))
                   && grantRows.Where(r => CardCore.ComposerCatalog.HasMountBit(r, CardCore.MountKind.NoLinkAura))
                               .All(r => !CardCore.ComposerCatalog.CanMountAsAura(r)),
                   "默认翻转：关键词行（未拉黑）全部可作光环；拉黑行全部不可（CanMountAsAura 统一判定）");
            var convertedAuraExits = new[] { "GrantStealth", "GrantDivineShield", "GrantReborn", "GrantSpellShield", "GrantSpellban" }
                .Select(n => CardCore.Attribute.AtomicEffectTable.GetByEnumName(n)).ToList();
            Assert(convertedAuraExits.All(r => r != null)
                   && convertedAuraExits.All(r => !CardCore.ComposerCatalog.HasMountBit(r, CardCore.MountKind.NoLinkAura))
                   && convertedAuraExits.All(r => !CardCore.ComposerCatalog.HasMountBit(r, CardCore.MountKind.Keyword))
                   && convertedAuraExits.All(r => CardCore.ComposerCatalog.HasMountBit(r, CardCore.MountKind.Counter)),
                   "圣盾/复生/潜行（2026-10-08）/法术护盾（2026-10-09）/无实体（2026-11-02）指示物化：五行退出关键词族（无位 0/位 7）改位 1 指示物——光环挂载由指示物位硬拒");
            // 守护「位 0 + 位 7 拉黑」断言已删（2026-11-05 位 7 黑名单退役定案——见 1b 注）
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
            var tauntRow = CardCore.Attribute.AtomicEffectTable.GetByEnumName("GrantTaunt");
            Assert(tauntRow != null
                   && (tauntRow.TargetFilter ?? "") == ""
                   && CardCore.TargetAxisRules.TryParseDomain(tauntRow.Domain, out var tauntAxis)
                   && tauntAxis.Zh == "己方战场:生物",
                   "效果层关键词可赋予：Grant 行 Domain=己方战场:生物（2026-11-07 特判；自授予 Domain=自己 退役）");
            // 光环黑名单断言已删（2026-11-05 用户定案：位 7 黑名单整链退役；2026-10-09 角色通道
            // 裁定随「关键词=生物专属」修订一并作废——角色光环读数已下线）。
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
            // 光环关键词下拉排除断言已删（2026-11-05 位 7 黑名单退役定案——下拉名单随表自由漂浮）

            // ---- 1c. 不可随机黑名单（2026-10-07 位 4 翻转；总数与「含 {value}」性质均归表改面漂移——
            // 系统行等无滑条行已入黑名单，{value} 不变量已废；只钉非空性质+下方七行白名单）；
            // 七个可随机行（伤害族4+回复生命+紊乱+沉睡）不标——默认可随机 ----
            var noRandomRows = CardCore.Attribute.AtomicEffectTable.GetAll()
                .Where(r => r != null && CardCore.ComposerCatalog.HasMountBit(r, CardCore.MountKind.NoRandom)).ToList();
            Assert(noRandomRows.Count >= 1,
                   $"不可随机黑名单非空（实际 {noRandomRows.Count} 行——名单内容随表漂移不锚）");
            string[] randomable = { "DealDamage", "DrainLife", "Heal", "LifeLoss", "RushSickness", "Sleep" };
            Assert(randomable.All(n => !CardCore.ComposerCatalog.HasMountBit(
                       CardCore.Attribute.AtomicEffectTable.GetByEnumName(n), CardCore.MountKind.NoRandom)),
                   "六个可随机行未标黑名单（穿透档已删 2026-10-31；翻转后默认可随机——滑条按 {value} 出现）");
            // 冻结行 {value} 断言已删（2026-11-02 冻结回指示物版定案：{value}=层数合法模板；
            // 可随机性与滑条随表 NoRandom 位漂移，不再单独锚定冻结行）

            // ---- 1d. 位 4 SystemInternal（2026-10-04 定案；2026-10-07 位 0/3 删除后挂载资格走派生——
            // 资格保留、仅 UI 隐藏；晚间并行扩容：攻击/守卫/设置三行/宣告胜利并入系统位；
            // 2026-11-08 模拟/变形退役出组合：挂「系统」位整库隐藏——卡直接引用（引用卡=卡组合界面绑定）----
            var internalRows = CardCore.Attribute.AtomicEffectTable.GetAll()
                .Where(r => r != null && CardCore.ComposerCatalog.HasMountBit(r, CardCore.MountKind.SystemInternal)).ToList();
            Assert(internalRows.Count == 9
                   && new HashSet<string>(internalRows.Select(r => r.EnumName))
                       .SetEquals(new[] { "Attack", "Guard", "SetPower", "SetLife", "SetCost",
                           "DeclareVictory", "RevealCard", "Morph", "GrantMorph" }),
                   "系统位 9 行：攻击/守卫/设置攻击力/设置生命值/设置费用/对手胜利+展示+模拟/变形"
                   + "（2026-11-08 模拟/变形退役出组合——挂「系统」位整库隐藏，卡直接引用+卡组合界面绑定引用卡）");
            foreach (var row in internalRows)
            {
                bool dynamicCost = CardCore.ComposerCatalog.HasMountBit(row, CardCore.MountKind.DynamicCost);
                Assert(CardCore.ComposerCatalog.CanBeTrunkRow(row),
                       $"系统行 {row.EnumName} 保留主干挂载资格（数据可挂——仅 UI 不暴露）");
                Assert(dynamicCost || CardCore.ComposerCatalog.CanBeRewardRow(row),
                       $"系统行 {row.EnumName} 奖励挂载资格（动态费用行除外——不作 Then 奖励，2026-10-31 定案）");
            }

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
                   && drawGates.Any(g => g.Id == "LandsLe3") && drawGates.Any(g => g.Id == "HandEmpty")
                   && drawGates.Any(g => g.Id == "LifeLe7"),
                   "局面门目录与产出无关（任意主干原子可挂，含二批三门；总数随目录扩缩不锚）");
            // 门槛行不入主干/奖励（2026-10-23 入表：位 8 排除——防门槛行漏进原子库与卡组合）；
            // GateId 非空对齐目录派生口径（预言行带位 8 但走预言维度、非局面门——目录不收）
            var gateRowsFromTable = CardCore.Attribute.AtomicEffectTable.GetAll()
                .Where(r => r != null && CardCore.ComposerCatalog.HasMountBit(r, CardCore.MountKind.SituationGate)
                            && !string.IsNullOrEmpty(r.GateId)).ToList();
            Assert(gateRowsFromTable.Count == drawGates.Length
                   && gateRowsFromTable.All(r => !CardCore.ComposerCatalog.CanBeTrunkRow(r))
                   && gateRowsFromTable.All(r => !CardCore.ComposerCatalog.CanBeRewardRow(r)),
                   "门槛行挂位 8 有限分支：不得作主干/奖励（CanBeTrunkRow/CanBeRewardRow 排除；行数与目录一致随表漂移）");
            // 动态费用行（2026-10-24 定案；2026-10-31 召唤衍生物入列）：临摹/变形赋予/召唤衍生物挂位 9
            // ——费用随局内动态情况（补差/模板费随引用卡绑定浮动），不可作 Then 奖励；主干照旧
            var morphRow = CardCore.Attribute.AtomicEffectTable.GetByEnumName("Morph");
            var grantMorphRow = CardCore.Attribute.AtomicEffectTable.GetByEnumName("GrantMorph");
            var summonTokenRow = CardCore.Attribute.AtomicEffectTable.GetByEnumName("SummonToken");
            Assert(CardCore.ComposerCatalog.HasMountBit(morphRow, CardCore.MountKind.DynamicCost)
                   && CardCore.ComposerCatalog.HasMountBit(grantMorphRow, CardCore.MountKind.DynamicCost)
                   && CardCore.ComposerCatalog.HasMountBit(summonTokenRow, CardCore.MountKind.DynamicCost),
                   "动态费用位 9：临摹/变形赋予/召唤衍生物挂位（模板费随引用卡绑定浮动，不可作奖励）");
            Assert(!CardCore.ComposerCatalog.CanBeRewardRow(morphRow)
                   && !CardCore.ComposerCatalog.CanBeRewardRow(grantMorphRow)
                   && !CardCore.ComposerCatalog.CanBeRewardRow(summonTokenRow)
                   && CardCore.ComposerCatalog.CanBeTrunkRow(morphRow)
                   && CardCore.ComposerCatalog.CanBeTrunkRow(grantMorphRow)
                   && CardCore.ComposerCatalog.CanBeTrunkRow(summonTokenRow),
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
                   && drawGates.First(g => g.Id == "FirstCardThisTurn").Budget == 2,
                   "状态门预算锚：命运一抽 3 / 生命低于对手 1 / 首张出牌 2（表 Budget 列——2026-11-05 表定档）");
            Assert(drawGates.First(g => g.Id == "LandsLe3").Budget == 2
                   && drawGates.First(g => g.Id == "HandEmpty").Budget == 2
                   && drawGates.First(g => g.Id == "LifeLe7").Budget == 2,
                   "状态门二批预算锚：操控地≤3/手牌=0/生命≤7 均 2（表 Budget 列；2026-11-03 地门行≥7→≤3 随火灵改版）");
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

            // ---- 5. 描述动态渲染（AtomText——SynergyUI 侧；目标域重做 P0 2026-11-05：无新语法 Domain 行
            //      走 legacy 顿号短语（DomainPhrase）；区间文本已表随机，无「随机」前缀；
            //      2026-11-06 或连接定案：选择档多域以「或」连末段——目标从并集挑选，非逐域全打；
            //      2026-11-07 量词定案：量词标类别前（生物/结界=个）·域与角色前无量词——
            //      夹具 count 默认 -2 归一 1 →「对方战场的1个生物…」）----
            var cfg = CardCore.Attribute.AtomicEffectTable.GetByType(CardCore.AtomicEffectType.DealDamage);
            var atom = Atom("DealDamage", 3, amp: 1f);
            string rendered = SynergyUI.AtomText.Render(cfg, atom, null);
            Assert(rendered == "对对方战场的1个生物、对方战场的1个结界或对方角色造成0至6点伤害",
                   $"描述动态渲染：3±100% → 轴短语择一（伤害行 p=-1 极性定侧+或连接+量词标类别前；实际 「{rendered}」）");
            atom.amp = 0f;
            Assert(SynergyUI.AtomText.Render(cfg, atom, null) == "对对方战场的1个生物、对方战场的1个结界或对方角色造成3点伤害", "描述渲染：amp=0 → 模板+轴短语（极性定侧·无随机区间·或连接·量词标类别前）");
            // 槽级载荷后缀（两槽定案）：引擎/产出条件/局面门三族文本
            // 引擎短式（2026-10-09 短式化→后统一「，【奖励】」后缀形——与代价「，代价：」同族）：
            // 引擎身份+x 在正文，后缀只接奖励（条件在正文）
            var engineSuffix = SynergyUI.AtomText.BranchSuffix(
                EngineAtom(CardCore.AtomicEffectType.DrawCard, 1, CardCore.BranchEngineKind.DeathToll, 2,
                    Atom("DrawCard", 1)));
            Assert(engineSuffix.StartsWith("，【奖励】") && engineSuffix.Contains("抽") && !engineSuffix.Contains("["),
                   $"载荷后缀（引擎短式）：「{engineSuffix}」——只接奖励（条件在正文）");
            var engineBody = SynergyUI.AtomText.EngineShortBodyFor(
                EngineAtom(CardCore.AtomicEffectType.EngineDeathToll, 1, CardCore.BranchEngineKind.DeathToll, 2,
                    Atom("DrawCard", 1)));
            Assert(engineBody == "此卡在场上时计数双方生物死亡，计数大于等于2时触发奖励（满6不再计数，使用后清零）",
                   $"引擎短文案（2026-11-08 阶段3 只扫战场·死亡计数完整句）：「{engineBody}」——门槛=Then 锚价推导（DrawCard1=2），engineParam 死数据不参与");
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

        // ======================================== 减费三选一（2026-11-02 定案：代价并列槽位 + 三选一互斥 + 数值上限） ========================================

        /// <summary>
        /// 减费三选一（2026-11-02 定案）：① 同一效果只能三选一（有限分支/自由分支/代价）；
        /// ② 并列添加两个效果禁挂代价；③ 光环类（结界的场地光环/仪典行）禁挂代价；
        /// ④ 代价行全价 ≤4（2026-11-03 自 3 放宽回 4）；⑤ 分支减费=Then 奖励费值——有限分支 1~3、自由分支 1~6（原 1-5 同批放宽）。
        /// 判定单源=SynergyUI.EffectComposerScreen（DiscountConflictOf / BranchRewardCapViolationOf——
        /// 卡组合保存闸 / addcost 闸 / 效果合成器形态闸与本段共用）。夹具全内存合成，不动卡表。
        /// </summary>
        private static void TestDiscountExclusivity()
        {
            // ---- 夹具助手 ----
            CardEffectData ParallelFx(params AtomicEffectEntry[] atoms)
            {
                var fx = new CardEffectData { Id = "VERIFY_DE_FX", TriggerTiming = (int)TriggerTiming.Activate_Active };
                fx.Steps = new List<EffectStepData>();
                foreach (var a in atoms) fx.Steps.Add(new EffectStepData { kind = 0, atomic = a });
                return fx;
            }
            CardData SpellOf(params CardEffectData[] fxs)
            {
                var c = new CardData { ID = "VERIFY_DE_CARD", CardName = "减费三选一夹具", Supertype = Cardtype.Spell };
                c.Effects = new List<CardEffectData>(fxs);
                return c;
            }
            void SetCost(CardData c, AtomicEffectEntry a)
            {
                // 2026-11-02 代价栏翻转限制（并行定案）：代价原子必须带锁侧 kinds
                //（与效果合成器代价门候选同口——PayloadCostDomain 单源），否则
                // ConvertPayloadForDisplay 契约拒绝计 0 价
                var (lockKinds, _) = CostDerivationService.PayloadCostDomain(
                    CardCore.Attribute.AtomicEffectTable.GetByHashId(a.refId));
                if (lockKinds != null) a.kinds = lockKinds;
                // 2026-11-08 代价效果级化：代价行挂首效果 Costs（卡级行已整删）
                c.Effects[0].Costs = new List<CostEntry>
                {
                    new CostEntry
                    {
                        CostType = (int)CostType.Payload,
                        Value = 1,
                        payloads = new List<AtomicEffectEntry> { a },
                    },
                };
            }

            // ---- ① 并列两效果 + 代价 → 违规（互斥） ----
            var parallel2 = ParallelFx(Atom(AtomicEffectType.DealDamage, 1), Atom(AtomicEffectType.DrawCard, 1));
            var cardParallel = SpellOf(parallel2);
            SetCost(cardParallel, Atom(AtomicEffectType.DealDamage, 1));
            string rParallel = SynergyUI.EffectComposerScreen.DiscountConflictOf(cardParallel);
            Assert(rParallel != null && rParallel.Contains("并列添加了两个效果"),
                   $"并列两效果+代价 → 报错（实际：{rParallel ?? "未报"}）");
            // 反例：并列单原子 + 代价 → 合规（代价=并列槽位添加的合法形态）
            var cardSingle = SpellOf(ParallelFx(Atom(AtomicEffectType.DealDamage, 1)));
            SetCost(cardSingle, Atom(AtomicEffectType.DealDamage, 1));
            Assert(SynergyUI.EffectComposerScreen.DiscountConflictOf(cardSingle) == null,
                   "并列单原子+代价 → 合规（代价即并列槽位，不占灰槽位费）");

            // ---- ② 光环卡 + 代价 → 违规 ----
            var auraFx = new CardEffectData { Id = "VERIFY_DE_AU" };
            auraFx.Steps = new List<EffectStepData>
            {
                new EffectStepData { kind = 0, atomic = Atom(AtomicEffectType.ModifyGameRule, 1) },
            };
            var cardAura = new CardData { ID = "VERIFY_DE_AURA", CardName = "光环代价夹具", Supertype = Cardtype.Enchantment };
            cardAura.Effects = new List<CardEffectData> { auraFx };
            SetCost(cardAura, Atom(AtomicEffectType.DealDamage, 1));
            string rAura = SynergyUI.EffectComposerScreen.DiscountConflictOf(cardAura);
            Assert(rAura != null && rAura.Contains("光环类"),
                   $"光环卡+代价 → 报错（实际：{rAura ?? "未报"}）");

            // ---- ③ 代价超上限（常量相对：cap+1 越界报错 / 恰 cap 合规；2026-11-03 收回 3） ----
            var cardCost5 = SpellOf(ParallelFx(Atom(AtomicEffectType.DrawCard, 1)));
            SetCost(cardCost5, Atom(AtomicEffectType.DealDamage, SynergyUI.EffectComposerScreen.CostDiscountCap + 1)); // 伤害 1红/点
            float price5 = SynergyUI.EffectComposerScreen.CostRowPrice(cardCost5.Effects[0].Costs[0]);
            string rCost5 = SynergyUI.EffectComposerScreen.DiscountConflictOf(cardCost5);
            Assert(price5 > SynergyUI.EffectComposerScreen.CostDiscountCap && rCost5 != null && rCost5.Contains("超上限"),
                   $"代价 {price5:0.#} 点 → 超上限报错（实际：{rCost5 ?? "未报"}）");
            // 反例：代价恰在上限 → 合规
            var cardCost4 = SpellOf(ParallelFx(Atom(AtomicEffectType.DrawCard, 1)));
            SetCost(cardCost4, Atom(AtomicEffectType.DealDamage, SynergyUI.EffectComposerScreen.CostDiscountCap));
            float price4 = SynergyUI.EffectComposerScreen.CostRowPrice(cardCost4.Effects[0].Costs[0]);
            Assert(price4 <= SynergyUI.EffectComposerScreen.CostDiscountCap
                   && SynergyUI.EffectComposerScreen.DiscountConflictOf(cardCost4) == null,
                   $"代价 {price4:0.#} 点（≤{SynergyUI.EffectComposerScreen.CostDiscountCap}）→ 合规");

            // ---- ④ 有限分支 4 点 → 超区间（1~3） ----
            var gate4 = ParallelFx(GateAtom(AtomicEffectType.DealDamage, 1, "FirstCardThisTurn",
                Atom(AtomicEffectType.DrawCard, 3)));
            string rGate4 = SynergyUI.EffectComposerScreen.BranchRewardCapViolationOf(gate4);
            Assert(rGate4 != null && rGate4.Contains("有限分支") && rGate4.Contains("超区间"),
                   $"有限分支奖励费超 3 → 报错（实际：{rGate4 ?? "未报"}）");
            // 反例：有限分支奖励 3 内 → 合规
            var gate1 = ParallelFx(GateAtom(AtomicEffectType.DealDamage, 1, "FirstCardThisTurn",
                Atom(AtomicEffectType.DrawCard, 1)));
            string rGate1 = SynergyUI.EffectComposerScreen.BranchRewardCapViolationOf(gate1);
            Assert(rGate1 == null, $"有限分支奖励费 3 内 → 合规（实际：{rGate1 ?? "合规"}）");

            // ---- ⑤ 自由分支 7 点 → 超区间（1~6） ----
            var engine7 = ParallelFx(EngineAtom(AtomicEffectType.EngineCountdown, 0,
                CardCore.BranchEngineKind.Countdown, 0, Atom(AtomicEffectType.DrawCard, 6)));
            string rEngine7 = SynergyUI.EffectComposerScreen.BranchRewardCapViolationOf(engine7);
            Assert(rEngine7 != null && rEngine7.Contains("自由分支") && rEngine7.Contains("超区间"),
                   $"自由分支奖励费超 6 → 报错（实际：{rEngine7 ?? "未报"}）");
            // 反例：自由分支奖励 6 内 → 合规（抽牌表价 2/张：value 3 = 6 恰在界内）
            var engine4 = ParallelFx(EngineAtom(AtomicEffectType.EngineCountdown, 0,
                CardCore.BranchEngineKind.Countdown, 0, Atom(AtomicEffectType.DrawCard, 3)));
            string rEngine4 = SynergyUI.EffectComposerScreen.BranchRewardCapViolationOf(engine4);
            Assert(rEngine4 == null, $"自由分支奖励费 6 内 → 合规（实际：{rEngine4 ?? "合规"}）");

            // ---- ⑥ 三选一：分支 + 代价 → 违规 ----
            var branchCard = SpellOf(ParallelFx(GateAtom(AtomicEffectType.DealDamage, 1, "FirstCardThisTurn",
                Atom(AtomicEffectType.DrawCard, 1))));
            SetCost(branchCard, Atom(AtomicEffectType.DealDamage, 1));
            string rBranch = SynergyUI.EffectComposerScreen.DiscountConflictOf(branchCard);
            Assert(rBranch != null && rBranch.Contains("三选一"),
                   $"有限分支+代价 → 三选一报错（实际：{rBranch ?? "未报"}）");
            var engineCard = SpellOf(engine7); // 引擎分支 + 代价（奖励费另由⑤覆盖）
            SetCost(engineCard, Atom(AtomicEffectType.DealDamage, 1));
            string rEngineCard = SynergyUI.EffectComposerScreen.DiscountConflictOf(engineCard);
            Assert(rEngineCard != null && rEngineCard.Contains("三选一"),
                   $"自由分支+代价 → 三选一报错（实际：{rEngineCard ?? "未报"}）");

            // ---- ⑦ 代价不再占灰槽位费（CountEffectSlots 不计代价行） ----
            var noCost = SpellOf(ParallelFx(Atom(AtomicEffectType.DrawCard, 1)));
            int slotsBefore = CostDerivationService.CountEffectSlots(noCost);
            SetCost(noCost, Atom(AtomicEffectType.DealDamage, 1));
            int slotsAfter = CostDerivationService.CountEffectSlots(noCost);
            Assert(slotsBefore == slotsAfter && slotsAfter == 1,
                   $"挂代价不计槽（挂前 {slotsBefore} / 挂后 {slotsAfter}——灰槽位费已删）");
        }

        /// <summary>
        /// 减费三选一数据审计（2026-11-02 定案·只报不改——同 RunColorAudit 先例）：
        /// 主表+教学副表全卡扫描，四类违规逐条 Warning（卡名+效果+违规类型），收尾统计。
        /// 判定单源=SynergyUI.EffectComposerScreen（DiscountConflictOf / BranchRewardCapViolationOf）。
        /// 2026-11-03 追加：代价栏翻转限制①②③（极性0/无target/域单侧——CostDerivationService.
        /// PayloadCostViolations 单源，含卡级行覆盖）——同卡集+训练副表逐条 Warning。
        /// 2026-11-06 追加：④代价同色（基准=效果主干色——CardCostService.PayloadColorMismatches
        /// 单源；EnsureCost 期 TideLog 无 sink 不可见，此处 Debug 通道补）。
        /// </summary>
        [MenuItem("Tools/减费三选一校验（只报不改）")]
        public static void RunDiscountExclusiveAudit()
        {
            var cards = new List<CardData>();
            string mainPath = SynergyUI.CardDataPaths.FileIn("Cards.json");
            if (File.Exists(mainPath))
                cards.AddRange(CardLoader.LoadCardsFromText(File.ReadAllText(mainPath)));
            string tutPath = SynergyUI.CardDataPaths.TutorialFileIn("TutorialCards.json");
            if (File.Exists(tutPath))
                cards.AddRange(CardLoader.LoadCardsFromText(File.ReadAllText(tutPath)));

            int conflict = 0, capBad = 0, checkedCards = 0;
            foreach (var card in cards)
            {
                if (card == null) continue;
                checkedCards++;
                string r = SynergyUI.EffectComposerScreen.DiscountConflictOf(card);
                if (r != null)
                {
                    conflict++;
                    Debug.LogWarning($"[减费三选一][互斥] {r}");
                }
                foreach (var fx in card.Effects ?? new List<CardEffectData>())
                {
                    if (fx == null) continue;
                    string rc = SynergyUI.EffectComposerScreen.BranchRewardCapViolationOf(fx);
                    if (rc == null) continue;
                    capBad++;
                    Debug.LogWarning($"[减费三选一][区间] 卡「{(string.IsNullOrEmpty(card.CardName) ? card.ID : card.CardName)}」{rc}");
                }
            }
            Debug.Log($"[减费三选一] 审计 {checkedCards} 张卡：互斥/上限违规 {conflict} 张，分支费值超区间 {capBad} 条"
                      + "（只报不改——改数由人决定）");

            // 代价栏翻转限制①②③（2026-11-03 追加）：主表+教学副表+训练副表——③域单侧在存量
            // 教学/训练数据有已知命中（蓝套9张卡级 03c85bc4 抽卡@己方牌库），只报不改待表侧整改。
            int costBad = 0, costCards = 0;
            var costScan = new List<CardData>(cards);
            string trainPath = SynergyUI.CardDataPaths.TrainingFileIn("TrainingCards.json");
            if (File.Exists(trainPath))
                costScan.AddRange(CardLoader.LoadCardsFromText(File.ReadAllText(trainPath)));
            foreach (var card in costScan)
            {
                if (card == null) continue;
                costCards++;
                foreach (var v in CardCore.CostDerivationService.PayloadCostViolations(card))
                {
                    costBad++;
                    Debug.LogWarning($"[代价栏][①②③] 卡「{(string.IsNullOrEmpty(card.CardName) ? card.ID : card.CardName)}」{v}");
                }
            }
            Debug.Log($"[代价栏] 审计 {costCards} 张卡（主表+教学+训练）：代价栏违规 {costBad} 条"
                      + "（①极性0 ②无target ③域单侧——只报不改；③整改前不动装载剥除口）");

            // ④代价同色（2026-11-06 定案·同日基准收口=效果主干色）：EnsureCost 期 TideLog.Warn 在
            // 编辑器菜单域无 sink 只进 Editor.log——此处复扫走 Debug.LogWarning 补可见性；
            // 单源 CardCostService.PayloadColorMismatches（效果级行归属所在效果、卡级行归属首效果；
            // 吸取红+绿只能配红/绿，单绿效果不得以吸取作代价）。
            int colorBad = 0;
            foreach (var card in costScan)
            {
                if (card == null) continue;
                foreach (var v in CardCore.CardCostService.PayloadColorMismatches(card))
                {
                    colorBad++;
                    Debug.LogWarning($"[代价栏][④同色] 卡「{(string.IsNullOrEmpty(card.CardName) ? card.ID : card.CardName)}」{v}");
                }
            }
            Debug.Log($"[代价栏][④同色] 审计 {costCards} 张卡（主表+教学+训练）：异色代价 {colorBad} 条"
                      + "（基准=效果主干色——异色抵扣纯空转；只报不改）");
        }

        /// <summary>元素席位收缩断言（2026-10-28 P2）：ManaType 白/黑退役、Any 6→4——
        /// ① Length==5；② 7 位旧数组重排（new[4]=old[6]，禁止截断）；③ 旧白/黑位非零 → Error+丢弃；
        /// ④ 原子表 120 行全部可解析、无悬空（计价行全部落 5 位）。</summary>
        private static void TestManaSeatShrink()
        {
            // ① 枚举席位口径
            Assert(ManaType.Gray == (ManaType)0 && ManaType.Any == (ManaType)4
                   && System.Enum.GetNames(typeof(ManaType)).Length == 5
                   && ElementCost.Length == 5,
                   "席位收缩：ManaType=[灰,红,蓝,绿,任意] 5 席、Any=4、ElementCost.Length==5（白4/黑5 留空不回收）");

            // ② 7 位旧档重排（不是截断——截断会把旧白位错当新任意位）
            var r1 = new ElementCost(new float[] { 0, 1, 0, 0, 0, 0, 0 });
            Assert(r1.v.Length == 5 && r1.v[0] == 0 && r1.v[1] == 1 && r1.v[2] == 0 && r1.v[3] == 0 && r1.v[4] == 0,
                   "重排①：7 位 [0,1,0,0,0,0,0] → [0,1,0,0,0]（红份额保持红位）");
            var r2 = new ElementCost(new float[] { 0, 0, 0, 0, 0, 0, 3 });
            Assert(r2.v.Length == 5 && r2.v[0] == 0 && r2.v[1] == 0 && r2.v[2] == 0 && r2.v[3] == 0 && r2.v[4] == 3,
                   "重排②：7 位 [0,0,0,0,0,0,3] → [0,0,0,0,3]（旧 Any 位 6 → 新位 4，不截断丢弃）");
            // 4 位（原子表现行格式）补零 / 5 位原样
            var r3 = new ElementCost(new float[] { 0, 5, 0, 0 });
            Assert(r3.v.Length == 5 && r3.v[1] == 5 && r3.v[4] == 0, "补零：4 位原子表格式 → 5 位（缺省任意补 0）");

            // ③ 旧白/黑位非零 → 记 Error 并按 0 处理（无兼容层；Error 只捕获不上控制台——负向探针）
            int errCount = 0;
            var prevSink = CardCore.TideLog.Sink;
            CardCore.TideLog.Sink = (level, text) =>
            {
                if (level == CardCore.TideLogLevel.Error && text != null && text.Contains("白/黑")) errCount++;
            };
            ElementCost r4;
            try { r4 = new ElementCost(new float[] { 0, 0, 0, 0, 2, 1, 0 }); }
            finally { CardCore.TideLog.Sink = prevSink; }
            Assert(errCount == 1 && r4.v.Length == 5
                   && r4.v[0] == 0 && r4.v[1] == 0 && r4.v[2] == 0 && r4.v[3] == 0 && r4.v[4] == 0,
                   "旧白/黑非零被拒：[0,0,0,0,白2,黑1,0] → 记 1 条 Error、份额丢弃置零（其余位不受染）");

            // ④ 原子表全行可解析、无悬空（计价行全部 5 位——6/7 位残档会经重排/报错露出）
            int rows = 0, priced = 0, badRows = 0;
            foreach (var row in CardCore.Attribute.AtomicEffectTable.GetAll())
            {
                if (row == null) { badRows++; continue; }
                rows++;
                if (!System.Enum.TryParse<CardCore.AtomicEffectType>(row.EnumName, out _)) badRows++;
                if (row.ManaList != null)
                {
                    priced++;
                    if (row.ManaList.v.Length != ElementCost.Length) badRows++;
                    if (row.ManaList.v.Any(f => f < 0f)) badRows++;
                }
            }
            // 2026-11-05：不再钉死行数（表随设计增删，钉数只会反复改测试）——只断「装到行、行都能解析、
            // 计价行都是 5 位、无非负价」。行数照旧打进消息里便于对账。
            Assert(rows > 0 && badRows == 0,
                   $"原子表全部可解析、无悬空（实际 {rows} 行/坏 {badRows} 行；计价行 {priced} 全 5 位）");
        }

        private static void TestCostAnchors()
        {
            // ---- 表值 ----
            // 2026-10-09 清理：Heal TotalUnitCost=0.4 钉价断言删除——表价已迁绿0.5（表为单一来源，测试不钉价）
            // 2026-10-31 阶梯减价删除：DelayDiscountConfig 整链退役（d(C) 分段曲线断言随之删除——
            // 无前中后期卡组概念，S+E+K 全额计价不按额度打折）
            // 2026-11-03：GrantTaunt 表行现 Blue3（表侧改色）——按「表为单一来源不钉色」惯例
            //（2026-10-09 Heal 先例）改断「亲和随表走」，不再钉 Green
            var tauntRow = CardCore.Attribute.AtomicEffectTable.GetByType(AtomicEffectType.GrantTaunt);
            var tauntPrimary = ManaType.Gray;
            if (tauntRow?.ManaList != null)
                foreach (var c in tauntRow.ManaList.NonzeroColors()) { tauntPrimary = c; break; }
            Assert(tauntRow != null && tauntRow.ManaList != null && tauntRow.ManaList.Total > 0
                   && ElementAffinities.GetAffinityForEffect(AtomicEffectType.GrantTaunt).PrimaryColor == tauntPrimary,
                   "计价锚：GrantTaunt 亲和随表走（表为单一来源不钉色；2026-10-28 曾 Green，2026-11-03 表行=Blue3）");

            // ---- 身材（2026-11-03 底盘分配改版）：3 点预算优先 攻1+守1+首个效果槽，剩余 n=免费属性底
            //      n/n（攻/血各前 n 点免计）；超出部分按颜色收费（攻红/血绿，真实小数 0.5/点不参与
            //      整卡取整——显示 0.## 直显、支付 NormalizeBill 逐色进 1）。旧「免费点先红后绿扣桶」退役 ----
            Assert(CardCompositionCost.FreeAttrBaseOf(MakeCostCard(Cardtype.Creature, 1, 1)) == 1,
                   "计价锚：底盘 0 效果攻守白板 → 免费属性底 1/1");
            Assert(CardCompositionCost.FreeAttrBaseOf(MakeCostCard(Cardtype.Creature, 1, 1, MakeEffect("Heal", 1))) == 0,
                   "计价锚：底盘 1 效果生物 → 攻守+首槽=3 用满，底 0（每点属性按颜色收费）");
            Assert(DeriveTotal(MakeCostCard(Cardtype.Creature, 1, 1)) == 0, "计价锚：1/1 白板 D=0（底 1/1 内免计）");
            Assert(DeriveTotal(MakeCostCard(Cardtype.Creature, 0, 2)) == 1, "计价锚：0/2 白板 D=1（血超底 1 计 0.5 绿——DerivedTotal 四舍五入 1；旧 D=0 口径作废）");
            Assert(DeriveTotal(MakeCostCard(Cardtype.Creature, 2, 2)) == 1, "计价锚：2/2 白板 D=1（各超底 1 → 0.5红+0.5绿=1）");
            Assert(DeriveTotal(MakeCostCard(Cardtype.Creature, 3, 2)) == 2, "计价锚：3/2 白板 D=2（1红+0.5绿=1.5→2）");
            var c44 = MakeCostCard(Cardtype.Creature, 4, 4);
            var r44 = CardCostService.Derive(c44);
            Assert(System.Math.Abs(r44.DerivedCost[ManaType.Red] - 1.5f) < 1e-4f
                   && System.Math.Abs(r44.DerivedCost[ManaType.Green] - 1.5f) < 1e-4f,
                   "计价锚：4/4 白板（底1）= 红1.5+绿1.5 真实小数（2026-11-03 定案例1；支付进 1=2+2）");
            var c44s = MakeCostCard(Cardtype.Creature, 4, 4, MakeEffect("Heal", 1));
            c44s.Effects[0].TriggerTiming = (int)TriggerTiming.Activate_Active; // 主动不占卡费（只计 1 槽压底）
            var r44s = CardCostService.Derive(c44s);
            Assert(r44s.DerivedCost[ManaType.Red] == 2f && r44s.DerivedCost[ManaType.Green] == 2f,
                   "计价锚：4/4+1技能（底0）= 红2+绿2 每个属性按颜色收费（2026-11-03 定案例2）");
            var wall = MakeCostCard(Cardtype.Creature, 3, 3);
            wall.AttackAbility = wall.GuardAbility = false;
            Assert(CardCompositionCost.FreeAttrBaseOf(wall) == 3 && DeriveTotal(wall) == 0,
                   "计价锚：无攻守无技能 → 底 3/3 固定获得（3/3 整卡免费；定案例3——偏斜底 5/1 由组合器属性下限闸拦）");
            var ng2 = MakeCostCard(Cardtype.Creature, 4, 4, MakeEffect("Heal", 1), MakeEffect("Heal", 1));
            ng2.GuardAbility = false;
            ng2.Effects[0].TriggerTiming = (int)TriggerTiming.Activate_Active;
            ng2.Effects[1].TriggerTiming = (int)TriggerTiming.Activate_Active;
            var rng2 = CardCostService.Derive(ng2);
            Assert(CardCompositionCost.FreeAttrBaseOf(ng2) == 1 && rng2.DerivedCost[ManaType.Gray] == 1f
                   && System.Math.Abs(rng2.DerivedCost[ManaType.Red] - 1.5f) < 1e-4f
                   && System.Math.Abs(rng2.DerivedCost[ManaType.Green] - 1.5f) < 1e-4f,
                   "计价锚：NoGuard+2技能 → 底1（预算优先 攻+首槽）+超槽恒1灰+红1.5绿1.5（2026-11-03 超槽加价改版）");

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
            noAtk.AttackAbility = false;
            Assert(CardCompositionCost.SurplusOf(noAtk) == 2,
                   "计价锚：底盘无攻白板 → 免费点 2（属性1+资格缺位释放，全减灰桶 S 不能用作其他）");
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
                   && CardCompositionCost.FreeDurabilityBaseOf(ench1) == 4
                   && CardCompositionCost.KindSpeedBonusOf(ench1) == 0,
                   "计价锚：结界免费点 2 折免费耐久底 4（2026-11-04 碑族0费改版：免费点抵卡价，旧+2堆叠退役；速通道退役）");
            var enchActive = MakeCostCard(Cardtype.Enchantment, null, null, MakeEffect("Heal", 1));
            enchActive.Durability = 3;
            enchActive.Effects[0].TriggerTiming = (int)TriggerTiming.Activate_Active;
            Assert(CardCompositionCost.FreeDurabilityBaseOf(enchActive) == 4
                   && CardCompositionCost.KindSpeedBonusOf(enchActive) == 0,
                   "计价锚：结界带主动行（形状非法——装载拦截）计价式仍折免费耐久底/速 0（发动闸另有来源拒绝）");
            var enchBig = MakeCostCard(Cardtype.Enchantment, null, null, MakeEffect("Heal", 1));
            enchBig.Durability = 8;
            var rngBig = CardCostService.Derive(enchBig);
            Assert(System.Math.Abs(rngBig.DerivedCost[ManaType.Blue] - 2f) < 1e-4f,
                   "计价锚：结界 1 槽 耐久 8 超底 4 → 超出 4 点 ×0.5蓝 = 2蓝（底内免卡价——2026-11-04 碑族0费改版）");

            // ---- 抉择分支计槽已废（2026-10-31 P12：kind==2 整链退役）——遗留 Choice 步不再计槽 ----
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
            Assert(CostDerivationService.CountEffectSlots(choiceSlotData) == 1,
                   "计价锚：抉择分支计槽已废——遗留 Choice 步不计槽（仍 1 效果=1 槽；2026-10-31 P12）");
            Assert(CardCompositionCost.SurplusOf(choiceSlotData) == 2
                   && CardCompositionCost.KindSpeedBonusOf(choiceSlotData) == 2,
                   "计价锚：法术 1 槽（遗留 Choice 不计槽）→ 速度 2（3−槽数回归）");

            // ---- 法术全额计价（2026-10-31 阶梯减价删除：S+E+K 不再打折）+ 槽位从速度扣（2026-10-15 改版）----
            // 恒推导（2026-10-22 定案；2026-10-30 时机系数改期望次数——OnDeath ×1）：
            // Ĉ=E=红4.0+蓝2.0（现表锚价——Draw/伤害均吃同档时机）=6→档6，
            // 全额 红4+蓝2=6 分色 → 红4蓝2；免费点全投提速不抵价。
            var fbEffect = MakeEffect("DealDamage", 4);
            fbEffect.AtomicEffects.Add(AtomRefs.New(CardCore.AtomicEffectType.DrawCard, value: 1));
            var fb = CardCostService.Derive(MakeCostCard(Cardtype.Spell, null, null, fbEffect));
            Assert((int)fb.DerivedCost[ManaType.Red] == 4 && (int)fb.DerivedCost[ManaType.Blue] == 2,
                   $"计价锚：法术 4伤+1抽 = 红4+蓝2（2026-10-31 无延迟折：E=红4.0+蓝2.0→Ĉ=6 全额 6 分色）");
            Assert(fb.DerivedTier == 6,
                   "计价锚：法术档位随总额自决 Ĉ=6（恒推导定案，旧「未声明档」口径已废；档位现仅作 UI 费数字）");
            var fbSpeedCard = MakeCostCard(Cardtype.Spell, null, null, fbEffect);
            fbSpeedCard.SpeedBonus = 2; // 字段退役——脏数据不得影响
            var fbSpeed = CardCostService.Derive(fbSpeedCard);
            Assert((int)fbSpeed.DerivedCost[ManaType.Red] == 4 && (int)fbSpeed.DerivedCost[ManaType.Blue] == 2,
                   $"计价锚：法术无抵价恒红4+蓝2（SpeedBonus 字段退役写值无效）");
            Assert(DeriveTotal(MakeCostCard(Cardtype.Spell, null, null, MakeEffect("Heal", 2))) == 1,
                   "计价锚：回2命 = 1费（锚价→档1 取整 1；法术无抵价通道——2026-10-15 改版翻转旧「−抵价2→0」）");

            // ---- 全额计价：档位随总额自决 Ĉ=round(S+E)=round(9+3)=12（延迟折已删——高费卡不再打折）----
            var bigBody = MakeCostCard(Cardtype.Creature, 9, 9, MakeEffect("DealDamage", 3));
            bigBody.Cost[(int)ManaType.Gray] = 9; // 存档快照（显示缓存）——不参与推导
            var big = CardCostService.Derive(bigBody);
            Assert(big.DerivedTotal == 12 && big.OffsetRequirement == 0,
                   $"计价锚：9/9 挂3伤 → 2026-10-31 无延迟折：Ĉ=round(12)=12 全额 → D=12（灰9红3）");
            var midTier = MakeCostCard(Cardtype.Creature, 9, 9, MakeEffect("DealDamage", 3));
            midTier.Cost[(int)ManaType.Gray] = 5; // 同内容、不同存档快照
            var mid = CardCostService.Derive(midTier);
            Assert(mid.DerivedTotal == big.DerivedTotal,
                   "计价锚：存档费用快照不参与推导——同内容不同 costList 推导全同（恒推导定案回归锚）");

            // ---- 两效果=超 1 槽（结构项4>3 加灰）；全额后结构加价计入规则一锚 ----
            // Ĉ=S2+E(红3.0+蓝2.0 现表)=7，全额 7 + 超1槽1灰 → D=8；Req=max(0, 8−(7+1))=0
            var chooser = MakeCostCard(Cardtype.Creature, 2, 2, MakeEffect("DealDamage", 3), MakeEffect("DrawCard", 1));
            chooser.Cost[(int)ManaType.Gray] = 4; // 存档快照不参与
            var ch = CardCostService.Derive(chooser);
            Assert(ch.DerivedTotal == 8 && ch.OffsetRequirement == 0,
                   $"计价锚：两效果 Ĉ=7 全额(S2+E5)=7，超1槽+1灰 → D=8（灰3红3蓝2；结构费计入规则一锚 → Req=0）");

            // ---- 超槽加价入灰（1-1 挂三效果：Ĉ=S1+E(3×蓝2.0 现表)=7，
            //      全额 7 + 超2槽+2灰 → D=9 灰3蓝6；结构费入锚 → Req=0）----
            var tripleMount = MakeCostCard(Cardtype.Creature, 1, 1,
                MakeEffect("DrawCard", 1), MakeEffect("DrawCard", 1), MakeEffect("DrawCard", 1));
            tripleMount.Cost[(int)ManaType.Gray] = 9; // 存档快照不参与
            var tm = CardCostService.Derive(tripleMount);
            Assert(tm.DerivedTotal == 9,
                   $"计价锚：1/1 挂三效果 全额 E=3×蓝2.0=6→Ĉ=round(7)=7，+超2槽2灰 → D=9（灰3蓝6，Req=0——结构费计入锚）");

            // ---- 规则一（2026-10-31 阶梯减价删除后）：D≤Ĉ+结构费 直判 ----
            // 与上方同内容 → 推导全同（D=8=Ĉ7+结构1 恰符）；旧「低声明→缺口」口径退役。
            var overBaseline = MakeCostCard(Cardtype.Creature, 2, 2, MakeEffect("DealDamage", 3), MakeEffect("DrawCard", 1));
            overBaseline.Cost[(int)ManaType.Gray] = 3; // 低存档快照：不改变推导
            var ov = CardCostService.Derive(overBaseline);
            Assert(ov.DerivedTotal == ch.DerivedTotal && ov.Conformant,
                   "计价锚：低存档快照不改变推导（同内容 D=8=Ĉ7+结构1 恰符规则一——无「声明缺口」概念）");
            overBaseline.Effects[0].Costs = new List<CostEntry>
            {
                new CostEntry
                {
                    CostType = (int)CostType.Payload, Value = 4,
                    payload = AtomRefs.New(AtomicEffectType.DiscardCard, value: 4, kinds: new List<int> { 5 }),
                },
            };
            var ov2 = CardCostService.Derive(overBaseline);
            Assert(ov2.Conformant,
                   "计价锚（2026-10-31 更新）：挂弃4张 Payload 代价 → 代价栏占 1 效果槽（结构加价 +1 计入锚）+ 代价色锁抵扣——净费与 D≤Ĉ+结构费 恒符（旧「Req=1 真超模」断言随延迟折/旧 payload 计价退役）");

            // 2026-10-09 清理：关键词计价两锚（嘲讽绿桶时代口径）删除——表色现锚 Green（P2a 白→绿，上方现行断言），
            // 关键词计价现行=行锚×max(1,层数)（2026-10-08 关键词不叠加定案）

            // ---- 推导档（恒推导定案）：5/5 挂 3伤（1 效果底盘恰耗尽）→ Ĉ=round(5+3)=8，
            //      全额 D=8——支付价=推导分布，无差额补灰（旧 Ĉ 搜索+补灰口径已废）----
            var noCost = MakeCostCard(Cardtype.Creature, 5, 5, MakeEffect("DealDamage", 3));
            var nc = CardCostService.Derive(noCost);
            Assert(nc.DerivedTier == 8 && nc.DerivedTotal == 8,
                   $"计价锚：无存档费推导档 Ĉ=8、全额 D=8（实际 Ĉ={nc.DerivedTier} D={nc.DerivedTotal}）");

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
            // 用现存 Permanent 非属性指示物 ArmorCounter（护甲层，无 StatKind 同语义）
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
            //    keyword 走行锚价 × 0.6（次数档 2026-10-30 已整删，见 TestLinkAura 计价锚））
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

    }
}
