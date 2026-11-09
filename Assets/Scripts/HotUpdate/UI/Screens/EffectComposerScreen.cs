using Cysharp.Threading.Tasks;
using CardCore;
using CardCore.Attribute;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UInputField = TMPro.TMP_InputField;

namespace SynergyUI
{
    /// <summary>
    /// 效果合成界面（2026-10-01 预制体化：静态层级来自 Assets/Art/Prefab/EffectUI.prefab，
    /// Build 深度按名绑定+闭包接线；槽位区/原子库/效果表/光环面板运行时重建。
    /// 交互/文案/校验语义与历史 UITK 版对齐（修订史见 tag uitk-ui-final 版头注释）；
        /// 2026-10-02 起 uGUI-native：排版按 25645c0^ 的 EffectComposer.uxml/Common.uss 历史稿还原，
        /// 左=编辑栏（模式条+槽位区）/ 右=展示区（原子库 / 效果表双模式）+ 筛选。
        /// 2026-10-05 设置盒/数值区 prefab 静态化（tpl-slot 模板克隆）；2026-10-09 设置盒拆除：
        /// 发动参数=activate-bar 静态组（发动速度组 2026-10-15 退役删除——恒 0 不可改，速度语义
        /// 移交法术提速份额/引擎光环），作用范围/目标数量/目标随机随逐原子目标制下沉每原子卡面行
        ///（header.TargetKinds 恒空——各原子按自身表域独立解析）；选择模式/持续档/落区收口不再手编。
        /// 2026-10-07 持续行预制体化：atom-card/reward-slot 烘焙 aura-duration 行（「持续时间」+三档下拉），
        /// 仅赋予类（关键词/指示物原子）显示；代码生成 dd-dur 与光环槽持续回合数行退役。
        /// 2026-10-07 光环条目迁入槽下作卡（aura-entry-card——克隆 atom-card，编辑走卡内 editor）；
        /// 条目列表区就地编辑与面板 aura-cost 费用预览退役（费用顶栏同源独占）。
        /// 2026-10-07 兜底构建全退（全屏统一）：缺节点只 LogError 不代码创建（槽/奖励区/条目卡/
        /// row 模板/guide-frame/描述条）；复合检索退役（BindDropdown 单名 TMP、BindableButton 单名、
        /// row 模板单名、FillRowParts 按名不按位、费用/计数不再父级回退）。
        /// 2026-10-08 右栏行定案不显示名字：FillRowParts 只填 meta 单列（name 节点绑定与 title 形参退役）。
    /// 2026-10-23 五分类定案：并列/自由分支/有限分支/连接光环/规则光环 五形态（ComposeMode 五值）——
    /// 分支两形态从并列槽级载荷升为独立形态（并列回归纯两原子）；有限分支门槛卡=克隆 aura-card 点选
    ///（dd-trunk 下拉路径退役删除）；产出条件不再提供新建入口（历史 settle=2 载入只读展示）；
    /// 光环拆连接（箭头+条目）/范围（仪典步）两面，aura 判定统一走 IsAuraMode。
    /// 2026-10-23 布局对齐卡组 v2（EffectUI.prefab 同批手术）：左900/右950、面板 Rounded8+1px 边框、
    /// 形态 chips 独立行（14号/间距8/标签「形态」）、右栏行模板化 44 高双行（费用26+名称14+标签12+
    /// 操作钮，次行描述小字）、顶栏费用=徽标+一行摘要（明细悬浮进描述条——desc-strip 单行滚动）、
    /// 空槽常驻「从右侧选原子填入」占位。
    /// 2026-10-05 两槽定案：形态只余 并列（两槽）/光环——自由分支·事件引擎、有限分支·产出条件
    /// 折叠进并列形态的槽级 atomic.branch 载荷（结算方式下拉+条件/引擎参数+Then 奖励槽）；
    /// 2026-10-09 并列/分支互斥：分支编辑区仅第一槽——任一分支已开则第二槽隐藏、第二槽有原子则分支区隐藏。
    /// 核心定案：槽位选中制（右侧点击=替换选中槽）；组合形态由 InferMode 推断防互串；
    /// 校验实时化（区域闪烁红框+保存禁用）；属性描述选中才出现（描述条）；
    /// 光环=箭头+条目随效果合成（挂卡并集，效果层只预选不计箭头费）；
    /// 代价栏已上移卡组合层（错边原子在卡编辑界面填装）。
    /// </summary>
    public sealed partial class EffectComposerScreen : UIScreen
    {
        // ======================================== 组合形态 ========================================

        /// <summary>组合形态（2026-10-23 五分类定案）：并列（两原子）/ 自由分支（引擎主干+Then 奖励）/
        /// 有限分支（普通原子+局面门槛+Then 奖励）/ 连接光环（箭头+条目）/ 规则光环（仪典步）。
        /// 前三类共享槽级 branch 载荷（settle=Engine/Gate/Outcome），后两类共享光环面板。</summary>
        public enum ComposeMode { Parallel, FreeBranch, LimitedBranch, LinkAura, RangeAura }

        /// <summary>光环双形态统一判定（连接∪范围）——本屏显隐与 CardComposerScreen 五处调用共用。</summary>
        public static bool IsAuraMode(ComposeMode mode)
            => mode == ComposeMode.LinkAura || mode == ComposeMode.RangeAura;

        /// <summary>右栏双模式：原子库（点击装配）/ 效果表（点击载入编辑）。</summary>
        private enum RightMode { Atoms, Effects }

        /// <summary>由数据推断组合形态（2026-10-23 五分类，先光环后分支）：
        /// ①箭头/增益·关键词条目→连接光环；②仪典步或属性降低条目（2026-10-30 入场地光环）→场地光环；
        /// ③首原子引擎主干行→自由分支（模拟/变形 2026-11-08 退役出组合——挂「系统」位整库隐藏，
        /// 引用卡绑定走卡牌组合界面）；
        /// ④首原子 Gate/历史 Outcome 载荷→有限分支（产出条件不再新建，载入只读保留）；
        /// ⑤非引擎行挂引擎载荷→并列单行只读（2026-10-26 定案：五分类前旧形状——现值=关键词获得
        /// 「自由分支·拼点→奖励」三条；自由分支主干已收窄为引擎行，此形状入自由分支必报
        /// 「主干必须是引擎行」——改按并列单行展示不报错，载荷不拆（摘要照显奖励段，保存原样往返））；
        /// ⑥其余→并列。条目归属（2026-10-30）：负属性条目=场地光环数据、其余=连接光环——
        /// 单槽整组替换制下新数据不混合；混合旧档以连接内容优先。</summary>
        public static ComposeMode InferMode(EffectGraphData g)
        {
            if (g?.header == null) return ComposeMode.Parallel;
            bool hasFieldStat = g.header.LinkAuras != null && g.header.LinkAuras.Any(
                a => a != null && !string.IsNullOrEmpty(a.stat) && a.value < 0);
            bool hasLinkContent = g.header.ArrowDirections != 0
                || (g.header.LinkAuras != null && g.header.LinkAuras.Any(
                    a => a != null && (string.IsNullOrEmpty(a.stat) || a.value > 0)));
            if (hasLinkContent) return ComposeMode.LinkAura;
            if (HasRuleAuraStep(g) || hasFieldStat) return ComposeMode.RangeAura;
            var first = g.steps?.FirstOrDefault(s => s?.kind == 0 && s.atomic != null)?.atomic;
            if (first != null)
            {
                if (ComposerCatalog.IsEngineTrunkRow(AtomicEffectTable.GetByHashId(first.refId)))
                    return ComposeMode.FreeBranch;
                if (first.branch != null && (BranchSettleKind)first.branch.settle == BranchSettleKind.Engine)
                    return ComposeMode.Parallel; // 引擎载荷只配引擎主干行——非引擎行的旧载荷按并列单行展示
                if (first.branch != null && !BranchEntryRules.IsPhantom(first.branch))
                    return ComposeMode.LimitedBranch;
            }
            return ComposeMode.Parallel;
        }

        /// <summary>组合形态中文短名（2026-11-05：效果表行 tag 用——与模式条五形态同词表）。</summary>
        private static string ModeZhOf(ComposeMode mode)
        {
            switch (mode)
            {
                case ComposeMode.FreeBranch: return "自由分支";
                case ComposeMode.LimitedBranch: return "有限分支";
                case ComposeMode.LinkAura: return "连接光环";
                case ComposeMode.RangeAura: return "场地光环";
                default: return "并列";
            }
        }

        /// <summary>效果表行 tag（2026-11-05 定案）：组成原子所在原子表并集 + 组合方式（InferMode 同口径）。
        /// 不再显示组成简称/预设卡组名（出处仍在搜索语料与 Meta 里）；原子表侧行 tag 则直接显示所在表
        /// （AtomicEffectTable.CategoryZhOf）——Tags 的表色回填色名（植物/火…）只供色匹配，不上 tag。</summary>
        private static string EffectTableTag(EffectGraphData fx, List<AtomicEffectConfig> parts)
        {
            var tables = string.Join("·", parts.Select(c => AtomicEffectTable.CategoryZhOf(c.TableCategory)).Distinct());
            var mode = ModeZhOf(InferMode(fx));
            return string.IsNullOrEmpty(tables) ? mode : tables + "·" + mode;
        }

        // ======================================== 减费三选一（2026-11-02 定案） ========================================

        /// <summary>代价减费上限：每条代价行全价 ≤3（2026-11-03 当日两改：先放宽回 4，同日收回 3——
        /// 寒锥/退潮/缓兵/蚀忆四张 4 点行随收回报超限，降回 ≤3 由表侧跟进）。
        /// 口径=代价行全价（逐原子 PayloadUnitGrant 求和）——抵扣色容量另有运行时钳，本闸管构筑期。</summary>
        public const int CostDiscountCap = 3;
        /// <summary>有限分支减费区间上限：Then 奖励费值 1~3（门槛行 Budget 1/2/3 均在此内——本值为全局上界）。</summary>
        public const int LimitedBranchFeeCap = 3;
        /// <summary>自由分支减费区间上限：Then 奖励费值 1~6（原 1-5 同批放宽一档）。</summary>
        public const int FreeBranchFeeCap = 6;

        /// <summary>branch 是否代价门载荷（settle=Gate + ComposerCatalog.CostGateId 哨兵——
        /// 2026-11-06 代价挪入有限分支：展示期合成/保存回写专用，永不落盘）。</summary>
        private static bool IsCostGate(BranchEntryData b)
            => b != null && (BranchSettleKind)b.settle == BranchSettleKind.Gate
               && b.gateId == ComposerCatalog.CostGateId;

        /// <summary>代价原子全价（单原子 PayloadUnitGrant——ConvertPayloadForDisplay 代价契约口，
        /// 转换失败回落 ConvertAtomForUI）。</summary>
        private static float CostAtomPrice(AtomicEffectEntry a)
        {
            if (a == null || string.IsNullOrEmpty(a.refId)) return 0f;
            var inst = CardEffectConverter.ConvertPayloadForDisplay(a) ?? CardEffectConverter.ConvertAtomForUI(a);
            return inst != null ? CostDerivationService.PayloadUnitGrant(inst) : 0f;
        }

        /// <summary>代价门候选（2026-11-06）：一般效果行 + 有计价色 + PayloadCostDomain 可入
        ///（p≠0·双侧域——规则本体单源 CostDerivationService.PayloadCostDomain，与
        /// CardComposerScreen.CostEligibleRow 同口径；错边单侧行天然出局）+ **与效果同色**
        ///（2026-11-06 定案：代价只能添加和效果同色的原子——异色抵扣纯空转，候选直接不列。
        /// 同日基准收口：比对效果主干色而非卡有效色——吸取 红+绿 只能从红/绿里选代价，
        /// 单绿效果不得以吸取作代价）。</summary>
        private static bool CostGateCandidate(LibPayload lp, HashSet<ManaType> effectColors)
        {
            var cfg = lp?.Cfg;
            if (cfg == null || string.IsNullOrEmpty(cfg.HashId)) return false;
            if (cfg.TableCategory != AtomicEffectTable.CategoryGeneral) return false;
            if (cfg.ManaList == null || cfg.ManaList.IsZero) return false;
            if (!CardCostService.PayloadColorAllowed(cfg.ManaList, effectColors)) return false;
            return CostDerivationService.PayloadCostDomain(cfg).eligible;
        }

        /// <summary>当前编辑效果的主干色（2026-11-06 同色定案·同日基准收口：效果色取代卡有效色）：
        /// 代价门候选过滤/落槽防御共用。枚举单源=CardCostService.TrunkColorsOfSteps（与费用推导报警
        /// PayloadColorMismatches 同一基准，勿在此复制逻辑）。</summary>
        private HashSet<ManaType> CostGateColors()
            => CardCostService.TrunkColorsOfSteps(_graph.steps);

        /// <summary>效果主干原子数（kind==0 计数——「并列添加两个效果」判定口径）。</summary>
        public static int TrunkAtomCount(CardEffectData fx)
        {
            if (fx?.Steps == null) return 0;
            int n = 0;
            foreach (var s in fx.Steps)
                if (s != null && s.kind == 0 && s.atomic != null) n++;
            return n;
        }

        /// <summary>效果是否分支形态（有限/自由——三选一的分支两形态）。</summary>
        public static bool IsBranchEffect(CardEffectData fx)
        {
            if (fx == null) return false;
            var mode = InferMode(new EffectGraphData(fx.DisplayName ?? "") { header = fx, steps = fx.Steps });
            return mode == ComposeMode.LimitedBranch || mode == ComposeMode.FreeBranch;
        }

        /// <summary>效果是否「并列添加了两个效果」（Parallel 形态且 ≥2 颗主干原子）。</summary>
        public static bool IsParallelTwoEffect(CardEffectData fx)
        {
            if (fx == null) return false;
            var mode = InferMode(new EffectGraphData(fx.DisplayName ?? "") { header = fx, steps = fx.Steps });
            return mode == ComposeMode.Parallel && TrunkAtomCount(fx) >= 2;
        }

        /// <summary>效果是否光环类（结界的场地光环/仪典行——连接/规则双形态）。</summary>
        public static bool IsAuraEffectData(CardEffectData fx)
        {
            if (fx == null) return false;
            return IsAuraMode(InferMode(new EffectGraphData(fx.DisplayName ?? "") { header = fx, steps = fx.Steps }));
        }

        /// <summary>卡是否挂了代价（任一效果级行——2026-11-08 效果级化：卡级行已整删）。</summary>
        public static bool CardHasCost(CardData card)
        {
            if (card == null) return false;
            if (card.Effects != null)
                foreach (var fx in card.Effects)
                {
                    if (fx?.Costs == null) continue;
                    foreach (var ce in fx.Costs)
                        if (ce != null && ce.AllPayloads().Count > 0) return true;
                }
            return false;
        }

        /// <summary>代价行全价（逐原子 PayloadUnitGrant 求和——CardCostService.RowPayloadPrice 同口径，
        /// 单行版：上限 ≤<see cref="CostDiscountCap"/> 的判定数。转换走 ConvertPayloadForDisplay
        ///（代价契约口——ConvertAtomForUI 对代价条目返回 null 会漏计）。</summary>
        public static float CostRowPrice(CardCore.CostEntry row)
        {
            if (row == null) return 0f;
            float pts = 0f;
            foreach (var a in row.AllPayloads())
            {
                var inst = CardEffectConverter.ConvertPayloadForDisplay(a);
                if (inst != null) pts += CostDerivationService.PayloadUnitGrant(inst);
            }
            return pts;
        }

        /// <summary>减费三选一冲突检测（2026-11-08 代价效果级化修订·单源：卡组合保存闸/效果合成器
        /// 保存闸/CardPipelineVerifier 共用）。效果级语义（代价行挂 CardEffectData.Costs）：
        /// ④ 代价行全价 ≤3（2026-11-03 收回——当日放宽 4 作废）；①②③ 同一效果只能三选一
        ///（有限分支/自由分支/代价——带代价行的效果不得同时是分支/并列两效果/光环形态）。
        /// 返回 null=合规；非空=违规描述（卡名+效果+违规类型）。</summary>
        public static string DiscountConflictOf(CardData card)
        {
            if (card == null) return null;
            string name = string.IsNullOrEmpty(card.CardName) ? card.ID : card.CardName;
            foreach (var fx in card.Effects ?? new List<CardEffectData>())
            {
                var v = EffectCostConflictOf(fx, name);
                if (v != null) return v;
            }
            return null;
        }

        /// <summary>单效果代价冲突（2026-11-08 效果级化单源——合成器保存闸直调，卡级聚合口逐效果转发）：
        /// ④ 逐行全价 ≤3（CostRowPrice 同源）；①②③ 带代价行的效果须为「单主干+支付代价门」形态。
        /// 正常保存链路（代价门剥除后单主干无分支）天然合规——本口拦手写数据/旁路。</summary>
        public static string EffectCostConflictOf(CardEffectData fx, string cardName = null)
        {
            if (fx == null || fx.Costs == null) return null;
            string fxName = string.IsNullOrEmpty(fx.DisplayName) ? fx.Id : fx.DisplayName;
            string who = string.IsNullOrEmpty(cardName)
                ? $"效果「{fxName}」"
                : $"卡「{cardName}」效果「{fxName}」";
            bool hasCost = false;
            foreach (var row in fx.Costs)
            {
                if (row == null || row.AllPayloads().Count == 0) continue;
                hasCost = true;
                float pts = CostRowPrice(row);
                if (pts > CostDiscountCap)
                    return $"{who}：代价行减费 {pts:0.#} 点超上限（≤{CostDiscountCap}）";
            }
            if (!hasCost) return null;
            if (IsBranchEffect(fx))
                return $"{who}：已挂代价，不能再选分支——同一效果只能三选一（有限分支/自由分支/代价）";
            if (IsParallelTwoEffect(fx))
                return $"{who}：并列添加了两个效果，不允许再挂代价";
            if (IsAuraEffectData(fx))
                return $"{who}：光环类（结界的场地光环/仪典行）不允许挂代价";
            return null;
        }

        /// <summary>分支奖励费值超限检测（2026-11-02 减费三选一·分支=Then 奖励费值口径）：
        /// 有限分支（Gate/Outcome）奖励费值须在 1~<see cref="LimitedBranchFeeCap"/>、
        /// 自由分支（Engine）在 1~<see cref="FreeBranchFeeCap"/>（下界=空/零费奖励另有未填闸，
        /// 此处拦 &lt;1 的零费档与超上限档）。返回 null=合规；非空=违规描述。</summary>
        public static string BranchRewardCapViolationOf(CardEffectData fx)
        {
            if (fx?.Steps == null) return null;
            string fxName = string.IsNullOrEmpty(fx.DisplayName) ? fx.Id : fx.DisplayName;
            foreach (var s in fx.Steps)
            {
                var b = s?.atomic?.branch;
                if (b?.then == null || b.then.Count == 0) continue;
                if (IsCostGate(b)) continue; // 代价门（2026-11-06）：展示期合成载荷，全价闸在槽区/DiscountConflictOf
                var insts = new List<CardCore.AtomicEffectInstance>();
                foreach (var a in b.then)
                {
                    var inst = CardEffectConverter.ConvertAtomForUI(a);
                    if (inst != null) insts.Add(inst);
                }
                if (insts.Count == 0) continue;
                float fee = CostDerivationService.RewardDerivedCost(insts);
                var settle = (BranchSettleKind)b.settle;
                if (settle == BranchSettleKind.Gate || settle == BranchSettleKind.Outcome)
                {
                    if (fee > LimitedBranchFeeCap)
                        return $"效果「{fxName}」：有限分支减费 {fee:0.#} 超区间（1~{LimitedBranchFeeCap}）";
                    if (fee < 1f)
                        return $"效果「{fxName}」：有限分支减费 {fee:0.#} 低于区间下界（1~{LimitedBranchFeeCap}）";
                }
                else if (settle == BranchSettleKind.Engine)
                {
                    if (fee > FreeBranchFeeCap)
                        return $"效果「{fxName}」：自由分支减费 {fee:0.#} 超区间（1~{FreeBranchFeeCap}）";
                    if (fee < 1f)
                        return $"效果「{fxName}」：自由分支减费 {fee:0.#} 低于区间下界（1~{FreeBranchFeeCap}）";
                }
            }
            return null;
        }

        // ---- 规则级光环（2026-10-03 定案；2026-10-04 光环改造后 9 行——全局唯一、作用双方、无箭头；普通光环才开箭头预选。
        // 引擎合约=ModifyGameRule 原子挂载体登场效果（str=规则短名），不占 LinkAura 连接位、不写 arrows） ----

        /// <summary>仪典表行 EnumName → 规则短名（RuleAuraComponents 常量，与原子表规则光环族行一一对应；
        /// 2026-10-04 光环改造：丰盈→HealOverflow、蚕褪承接 DamageCap；
        /// 蚕褪→离散改名（2026-10-04 伤害二值离散化，短名承袭）；舍身仪典 2026-10-16 关键词化出族；
        /// 时光仪典（原轮回，DoubleTurn）2026-10-29 阶段3a 删除——额外回合改 TakeExtraTurn 原子行）。
        /// 疾风仪典 2026-10-31 整链删除。</summary>
        private static readonly Dictionary<string, string> RuleAuraIds = new Dictionary<string, string>
        {
            { "三相仪典", RuleAuraComponents.ElementConversion },
            { "苦痛仪典", RuleAuraComponents.BloodPact },     // 2026-10-07 用户改名（原血偿仪典）
            { "丰盈仪典", RuleAuraComponents.HealOverflow },
            { "离散仪典", RuleAuraComponents.DamageCap },
            { "归土仪典", RuleAuraComponents.GraveyardPlay },
            // 疾风仪典已删（2026-10-31 整链退役——常量/枚举位/算速口同批）
            { "纳川仪典", RuleAuraComponents.HandLimitNoFatigue },
            // 涌泉仪典（疲劳免疫）2026-10-30 表行删除——常量代码侧保留专供教学直挂，不在合成器投放
            // 毒蚀/霜蚀仪典 2026-11-02 随负面仪典族整链删除
            // 舍身仪典已删（2026-10-16 关键词化：舍身改关键词族投放（GrantBattleMage——2026-10-30 战斗法师改版），不在仪典目录）
            { "放血仪典", RuleAuraComponents.HealInversion },  // 2026-10-09 治疗转伤害（受光环影响一方的治疗改写为等量伤害；表行 79ef61f3；2026-10-30 用户更名——历名暗牧仪典/惩戒仪典）
            // 禁令族（2026-10-29）：行为限制光环——受光环影响方按 ScopeHits 判命中（单位/角色同门）
            { "禁攻令", RuleAuraComponents.NoAttackAura },
            { "禁守令", RuleAuraComponents.NoGuardAura },
            { "禁自动令", RuleAuraComponents.NoActivateAura },  // 2026-11-06 改版（原禁发令：拦主动→拦自动；表行 EnumName 同步更名，短名不换）
            { "禁地令", RuleAuraComponents.NoLandAura },
            // 禁令族 1b 扩充（2026-10-29；2026-10-30 起随极性钉对方单侧；禁祭令表行已删）
            { "禁疗令", RuleAuraComponents.NoHealAura },
            // 禁寻令 2026-11-02 整链删除（用户定案无特色）
            { "禁魔令", RuleAuraComponents.NoSpellAura },
            { "禁召令", RuleAuraComponents.NoCreatureAura },
            { "禁抽令", RuleAuraComponents.NoDrawAura },
            // 语料原生禁令族（2026-10-29 阶段2；禁非生物令 2026-10-30 表行删除）
            { "禁低费令", RuleAuraComponents.NoLowCostAura },
            { "禁高费令", RuleAuraComponents.NoHighCostAura },
            // 禁指示物令已删（2026-10-31 用户定案：指示物承载了大部分游戏规则，不能禁止）
            // 禁扰令已删（2026-10-31 用户定案：弃改版整链退役）
            // 信息轴光环（2026-10-29 阶段3c，改写族——范围三档可选，不入固定双方名单）
            // 占卜加成 2026-11-02 整链删除（用户定案无特色——ScryBonus/PeekTop extra 同批退役）
            { "灵视", RuleAuraComponents.RevealedPunishAura },  // 使用已展示手牌者受2伤且其对手回2
            // 阶段3b 改写光环（2026-10-29，改写族——范围三档可选，不入固定双方名单）
            { "延迟死亡", RuleAuraComponents.DelayedDeathAura },       // 单位死亡→下个回合结束时死亡（DeathRules 改写）
            { "牌库顶当手牌", RuleAuraComponents.TopDeckAsHandAura },  // 牌库顶当手牌使用（TopDeckPlaySource 授权）
            { "铭刻", RuleAuraComponents.InscriptionAura },            // 单位被添加指示物时层数+1（AddCounters 咽喉）
            { "破败仪典", RuleAuraComponents.ArmorDecay },             // 护甲转易损（AddCounters 一口；易损生效全移 2026-11-09 随改版转全局默认，ApplyDamage 覆盖口退役）
        };

        /// <summary>保护行 EnumName → 档位 str（2026-10-30 指示物化定案：目标=己方单位含角色
        ///〔数量档 1/N/全 由效果级承载〕；持续=减半/全免/生命锁护盾指示物层数——持有者回合末−1
        /// 归零，换区清）：表行本身不带 str——合成落槽时按行写入 AtomicEffectEntry.str（Mode，
        /// GrantProtectionHandler 消费）。全体档「·全体→Mode|All」三键已删（2026-10-30 用户定案：
        /// 指示物没有全体的说法，只作用于挂载单位）。</summary>
        private static readonly Dictionary<string, string> ProtectionIds = new Dictionary<string, string>
        {
            { "减半", "Halve" },
            { "免疫", "Null" },
            { "生命锁", "Floor" },
        };

        /// <summary>跳过三阶段行 EnumName（=装载后 DisplayName）→ 相位档 str（2026-10-29 阶段3a，
        /// 同 RuleAuraIds 做法）：SkipTurnHandler 按 str 分派 Whole/Standby/Main/End 档；
        /// 整回合行「跳过回合」不在字典——str 空=Whole 档。</summary>
        private static readonly Dictionary<string, string> SkipTurnIds = new Dictionary<string, string>
        {
            { "跳过结束阶段", "End" },
            { "跳过准备阶段", "Standby" },
            { "跳过主要阶段", "Main" },
        };

        /// <summary>规则光环默认范围中文（行级——右栏库行说明用；2026-11-06 定案翻转）：
        /// 极性 0 行只能作用于双方（表价即实价）；极性 ±1 行作用方向可选——缺省对双方（表价档），
        /// 卡面可收窄极性单侧（+1 仅己方 / −1 仅对方，计价 ×RuleAuraSingleSideFactor）。
        /// 实际生效范围逐步存于仪典步 entry.value（AutoName/设置盒按步读取，2026-10-07）。
        /// 键=DisplayName（中文短名——装载后 EnumName 列落在 DisplayName，英文枚举名在 EnumName）。</summary>
        private static string RuleAuraScopeOf(AtomicEffectConfig cfg)
        {
            if (cfg == null) return "对双方生效";
            return cfg.Polarity <= -0.5f ? "缺省对双方（卡面可改仅对方，计价 ×1.5）"
                : cfg.Polarity >= 0.5f ? "缺省对双方（卡面可改仅己方，计价 ×1.5）"
                : "对双方生效";
        }

        /// <summary>规则光环行识别（2026-10-05 统一标记定案）：原子表 MountKinds 位 RuleAura 驱动——
        /// 双方仪典+改写仪典（疫蚀 2026-10-09 随剧毒指示物删除退役；眠蚀 2026-10-31、
        /// 毒蚀/霜蚀 2026-11-02 整链删除）；RuleAuraIds 字典只剩 str 短名映射职责。</summary>
        private static bool IsRuleAuraRow(AtomicEffectConfig cfg)
            => ComposerCatalog.HasMountBit(cfg, MountKind.RuleAura);

        /// <summary>steps 中的光环投放原子步（光环形态专有——SwitchMode 切出即清，形态互斥）。
        /// 2026-10-07 改写降级：改写行 MountKinds=连接光环——步识别改走 EffectType（全部 ModifyGameRule 行）。</summary>
        private static bool HasRuleAuraStep(EffectGraphData g)
        {
            if (g?.steps == null) return false;
            foreach (var s in g.steps)
            {
                if (s?.kind != 0 || s.atomic == null) continue;
                if (IsRitualStepRow(AtomicEffectTable.GetByHashId(s.atomic.refId))) return true;
            }
            return false;
        }

        /// <summary>光环投放步行识别（仪典+改写；2026-10-31 阶段D：行类型派生——
        /// Aura* 新型 ∪ 未翻新旧共享型 ModifyGameRule，RuleAuraComponents.IsAuraLike 单源；
        /// MorphSystem.IsRuleAuraEntry 同口径）。</summary>
        private static bool IsRitualStepRow(AtomicEffectConfig cfg)
            => cfg != null
               && Enum.TryParse<AtomicEffectType>(cfg.EnumName ?? "", out var t)
               && RuleAuraComponents.IsAuraLike(t);

        // ---- 状态 ----
        private EffectGraphData _graph = new EffectGraphData("新效果");
        private ComposeMode _mode = ComposeMode.Parallel;
        private RightMode _rightMode = RightMode.Atoms;

        // 槽内卡片展开状态（代价栏已上移卡组合层）
        // 2026-10-05 两槽定案：原 GateTrunk/GateReward/FreeTrunk/FreeReward 并入 Parallel（主干槽）
        // 与 Reward（槽内 Then 奖励选中态——_selRewardSlot 标记当前编辑哪槽的奖励）
        // 2026-10-23 五分类：Gate=有限分支门槛卡选中态（右栏切 11 条门槛列表）；分支恒挂首主干
        private enum SelKind { None, Parallel, Reward, Gate }

        // 展开态集合（2026-10-06 定案：多卡独立展开——点击其他卡不再收起已展开的卡）：
        // 键=对象身份——换序/删除免下标维护，ClampSelection 渲染前剪除悬挂条目
        private readonly HashSet<AtomicEffectEntry> _expandedAtoms = new HashSet<AtomicEffectEntry>();
        private readonly HashSet<BranchEntryData> _expandedRewards = new HashSet<BranchEntryData>();

        // 槽位选中：右侧点击=替换选中槽；默认原子1（首个槽）
        private SelKind _selSlot = SelKind.Parallel;
        private int _selSlotIndex = 0;

        // 奖励槽选中态（SelKind.Reward）：当前编辑哪槽的 Then 奖励（0/1——两槽）
        private int _selRewardSlot;

        /// <summary>并列槽位数（2026-10-05 两槽定案）。</summary>
        private const int ParallelSlotCount = 2;

        // 卡编辑会话（null=效果库模式——保存到效果库文件）
        private CardData _editingCard;
        private int _editingIndex = -1;

        // 从效果表载入的源效果 id（编辑换内容=换 id——保存后清旧档防重复；null=非效果表载入）
        private string _loadedFromLibraryId;

        // 预设效果只读载入源（2026-10-28 定案）：从效果表「预设卡组效果」段载入时记源 id——
        // 保存恒另存新条目（同内容拦存防跨表同 id 冗余）、源教学表不可覆盖不可删
        //（删除钮随 _loadedFromLibraryId=null 隐藏；首次成功保存后才转为常规库条目可改可删）
        private string _presetSourceId;

        // 预设载入快照（2026-10-28）：无文名内容哈希——DisplayName 载入后会被 OnSave 重渲
        //（老预设文案模板 vs 新渲染口径天然漂移），身份比对须剔除名字段防「未改也判改」
        private string _presetSnapshotHash;

        /// <summary>库效果编辑会话回跳标记（2026-11-06 通道补全）：从卡组合界面右栏「编辑」跳入时置位
        ///——库模式保存成功后自动 Back 回卡组合界面（ComposerSession 原设计承诺，此前过渡态只跳转不载入）。</summary>
        private bool _returnToCardComposer;

        /// <summary>预设内容指纹：header 克隆后清 DisplayName 再哈希——载入/保存两次同口径，
        /// 文案重渲漂移不参与「是否修改」判定。代价门哨兵分支（展示期合成物）在两侧同口径剔除
        ///（进屏合成/保存剥除后比对恒等——2026-11-08 效果级化配套）。</summary>
        private static string PresetContentHash(EffectGraphData g)
        {
            var header = JsonUtility.FromJson<CardEffectData>(JsonUtility.ToJson(g.header));
            header.DisplayName = "";
            var steps = g.steps != null
                ? JsonUtility.FromJson<ListStepWrap>(JsonUtility.ToJson(new ListStepWrap { items = g.steps })).items
                : new List<EffectStepData>();
            if (steps != null)
                foreach (var s in steps)
                    if (s?.kind == 0 && s.atomic?.branch != null && IsCostGate(s.atomic.branch))
                        s.atomic.branch = null;
            return ContentHasher.HashEffect(new EffectGraphData("") { header = header, steps = steps });
        }

        private TipPromptHost _tips; // TipPanel 提示弹窗宿主（2026-10-18 统一定案；惰性建——字段初始化器不能引用实例 Root）
        private TipPromptHost Tips => _tips ??= new TipPromptHost(() => Root);

        // ---- 校验区：不合规设置=GuideFrame 遮罩高亮框（shader 红边闪烁）+错误文本上描述条，保存按钮禁用 ----
        private sealed class ZoneEntry
        {
            public RectTransform Zone;
            public Func<string> Check; // null/空=合规；否则=错误提示文本
            public bool Invalid;
        }
        private readonly List<ZoneEntry> _slotZones = new List<ZoneEntry>();
        private Button _saveBtn;
        private bool _zoneErrShown; // 描述条当前展示的是校验错误（转合规时清一次——不误清控件描述）

        /// <summary>引导高亮框（单例）：prefab 烘焙的 guide-frame 节点（默认在 EffectUI 根下）；
        /// 提示时临时挂到目标区下拉伸填充，无违规回根下隐藏——闪烁由 SynergyUI/GuideFrame shader 自驱。</summary>
        private Image _guideFrame;

        // ---- 属性描述条：描述只在控件选中时出现——不常驻 ----
        private TMP_Text _descStrip;
        private RectTransform _descStripNode;

        // 光环箭头六按钮表（2026-10-06 arrow-picker 预制体化）：按钮简称 T/D/L/R=上/下/左/右，
        // 映射=棋盘六邻接方位名（持卡者视角）：Up=右上(R-T)、UpperRight=右(R)、LowerRight=右下(R-D)、
        // Down=左下(L-D)、LowerLeft=左(L)、UpperLeft=左上(L-T)；三角朝向已按方位烘焙在预制体内
        private static readonly (HexDirection dir, string node)[] ArrowButtons =
        {
            (HexDirection.Up, "R-T"), (HexDirection.UpperRight, "R"),
            (HexDirection.LowerRight, "R-D"), (HexDirection.Down, "L-D"),
            (HexDirection.LowerLeft, "L"), (HexDirection.UpperLeft, "L-T"),
        };

        // ---- UI 引用 ----
        private UiKit.Scroll _slotArea, _libraryList, _effectsList;

        // ---- 右栏两列表（2026-10-21 回收式虚拟化）：条目装配 + 行池复用 ----
        private UiKit.RecycledList _atomRows;     // list-library=原子表/光环条目库
        private UiKit.RecycledList _effectRows;   // list-effects=效果表
        private readonly List<TableRowData> _atomItems = new List<TableRowData>();
        private readonly List<TableRowData> _effectItems = new List<TableRowData>();

        /// <summary>右栏列表条目（原子行/光环行/效果行共用形态；IsHint=提示行）。
        /// 2026-10-24 改版：行尾操作钮退役——整行点击即装载；Meta 为单行小字（溢出省略，
        /// 悬浮整行经描述条看全文）。2026-11-06：行名称文本退役——效果无名字（只有卡有名字），
        /// row 模板 name 节点已删，首行只剩费用方格+标签(16 右对齐)。</summary>
        private sealed class TableRowData
        {
            public bool IsHint;
            public string Tag;
            public string Meta;
            public Color Dot;
            public ElementCost Costs;   // null=清空方格
            public Action OnClick;
            public float H;
        }
        private RectTransform _modeBar, _timingRow;
        private TMP_Text _nameLabel;
        private TMP_Text _costLabel; // 顶栏费用文本 lbl-effect-cost（2026-10-30 区域扩容改版：多行明细——构成/合计+实付/代价→净费分行直显；悬浮描述条留全文）
        private RectTransform _costBadgeNode; // 费用徽标 cost-badge（2026-10-26 改版：Circle32 合计徽标退役——公用 CostSquares 预制体实例逐色块展示；无费用隐藏）
        private CostSquaresView _costBadgeView;
        private Button _deleteBtn;            // 删除当前库效果（仅效果表载入态可用）
        private UInputField _filterName;
        private UiKit.Dropdown _timingDropdown, _activationDropdown;
        // 发动参数（2026-10-09 prefab 重排·activate-bar 静态组 way/timing——speed 组
        // 2026-10-15 随发动速度退役删除，BaseSpeed 恒 0 不可改；**take-effect 次数组 2026-10-29 P9
        // 次数档软删除役**——节点常驻 prefab 不动、整组隐藏）：节点常驻不随克隆重建——
        // 写入走当前 _graph.header（次数选项已删，值对齐只剩方式/时机下拉）
        private RectTransform _activateBar, _wayGroup;
        private RectTransform _limitGroup; // P9：仅剩「隐藏 take-effect 组」职责（次数下拉/参数行已删）
        // P5 主动效果代价行（2026-10-29 建 · 2026-10-30 用户定案整删）：activate-bar 第二行
        // 代码建组退役——效果级代价不再在此编辑（header.Costs 存量数据保留，顶栏净费段照显）
        private TMP_Text _tableToggleLabel;      // 合并表切换按钮的标签（原 btn-reload-atoms/btn-load-effects）
        private List<TriggerTiming> _timings;

        // 筛选状态（2026-10-13 分类下拉退役：dropdown-filter-type 节点已删，pool-filter 颜色行接管）：
        // 颜色=单选（选色即筛费用含该色的原子，默认「全部」；激活态=chip Outline 开关）。
        // 近似搜索（2026-10-13 多词AND+语料扩展）：
        // 原子库=名∪描述∪Tags∪MountKinds∪TargetKinds；效果表=名∪id∪组成简称∪摘要全文∪引用卡(ID·名·2026-10-26)；光环库=标题∪meta
        private UIColor _colorFilter = UIColor.All;
        private readonly Dictionary<UIColor, Button> _filterChips = new Dictionary<UIColor, Button>();
        private RectTransform _poolFilter; // 颜色行容器（chip-{Color} 预制体烘焙，Build 一次接线）

        // 发动方式（2026-10-04 定案：强制退出玩家选择——留系统自用[分支/光环/宣判锁档]，
        // 玩家只能在 自动/主动 上选；数据层 Mandatory 照旧装载）。
        // EffectActivationType 原值：0=强制(系统) 1=自动 2=主动——下拉显示序与枚举解耦。
        private static readonly (string label, int value)[] ActivationChoices =
        {
            ("自动", (int)EffectActivationType.Automatic),
            ("主动", (int)EffectActivationType.Voluntary),
        };

        /// <summary>ActivationType → 下拉显示序（系统强制档显示为"自动"占位——不写回，改选才落值）。</summary>
        private static int ActivationIndex(int activationType)
        {
            for (int i = 0; i < ActivationChoices.Length; i++)
                if (ActivationChoices[i].value == activationType) return i;
            return 0;
        }

        // 触发时机中文：单一来源=AtomText.TimingZh 全量表（2026-10-09 时机前缀定案配套——
        // 描述开头前缀与本下拉共用一份）；下拉滤掉系统自用七值（不开放玩家选择：OnAtomicEffect*
        // 三段/OnSummon 召唤进场/OnPhase* 阶段开始结束，及 OnGameStart 游戏开始——2026-10-09
        // 同日收编系统自用）与主动三档（主动不设时机），另排除观察者/角色时点
        // （OnOtherCreatureDeath/OnRoleDeath——旧下拉本就未开放），下拉维持 20 项面
        private static readonly HashSet<TriggerTiming> SystemOnlyTimings = new HashSet<TriggerTiming>
        {
            TriggerTiming.OnAtomicEffectActivation,
            TriggerTiming.OnAtomicEffectStartApplying,
            TriggerTiming.OnAtomicEffectResolution,
            TriggerTiming.OnSummon,
            TriggerTiming.OnPhaseStart,
            TriggerTiming.OnPhaseEnd,
            TriggerTiming.OnGameStart,
            TriggerTiming.OnOtherCreatureDeath,
            TriggerTiming.OnRoleDeath,
        };

        private static readonly Dictionary<TriggerTiming, string> TimingZh =
            AtomText.TimingZh.Where(kv => !SystemOnlyTimings.Contains(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value);

        /// <summary>时机逐项一句话介绍（两段式描述——选中才显示；语义按 TriggerTiming 枚举文档）。</summary>
        private static string TimingDescZh(TriggerTiming t) => t switch
        {
            TriggerTiming.OnPlay => "登场时：这张卡通过发动区检验、进入战场的那一刻（被无效则不触发）。",
            TriggerTiming.OnDeath => "死亡时：这张卡死亡进墓地（亡语）。",
            TriggerTiming.OnDestroy => "被破坏时：这张卡因「破坏」死亡。",
            TriggerTiming.OnExile => "被除外时：这张卡被除外。",
            TriggerTiming.OnReturnFromGraveyard => "从墓地回到战场时（复活）。",
            TriggerTiming.OnLeaveBattlefield => "离场时：从战场去任何其他区域。",
            TriggerTiming.OnDraw => "抽牌时：你抽到牌的那一刻。",
            TriggerTiming.OnStandbyPhase => "准备阶段：回合开始系统处理（自动抽牌/倒计时等）之后的第一个阶段（2026-10-20 定案：回合开始收归系统专用，效果时点挂这里）。",
            TriggerTiming.OnDealDamage => "造成伤害时：这张卡每次造成伤害。",
            TriggerTiming.OnTakeDamage => "受到伤害时：这张卡每次受到伤害。",
            TriggerTiming.OnTurnStart => "回合开始时：每回合的系统起点（系统专用——效果时点请挂准备阶段）。",
            TriggerTiming.OnTurnEnd => "回合结束时：每个回合结束的时点。",
            TriggerTiming.OnAttack => "攻击宣言时：这张卡宣言攻击（生物专属自动效果，计费 ×1.5）。",
            TriggerTiming.OnAttacked => "被攻击时：这张卡被指定为攻击目标（生物专属自动效果，计费 ×1.5）。",
            TriggerTiming.OnBlockDeclare => "阻拦宣言时：阻拦被宣言（生物专属自动效果，计费 ×1.5）。",
            TriggerTiming.OnCardPlayed => "使用卡牌时：使用宣言的时点（付费前，含法术）。",
            TriggerTiming.OnSpellCast => "施放法术时：有法术被使用。",
            TriggerTiming.OnTap => "横置时：这张卡被横置（生物专属自动效果，计费 ×1.5）。",
            TriggerTiming.OnUntap => "重置时：这张卡被重置（生物专属自动效果，计费 ×1.5）。",
            TriggerTiming.OnTargeted => "被指定为目标时：有效果以这张卡为目标。",
            TriggerTiming.OnOtherCreatureEnter => "其他生物进场时（这张卡自己进场不算）。",
            TriggerTiming.OnScry => "占卜时：任一玩家结算占卜（查看并排列牌库顶）的那一刻——双方占卜都触发。",
            TriggerTiming.OnCardCastSettled => "效果结算后：任一方施放的卡成功结算、且整条连锁结算完成的那一刻（双方施放都触发；被无效的施放不算）。",
            _ => null,
        };

        // 原子目标数量档（2026-10-09 逐原子目标制；P2-1 2026-11-04）：1/2/3/0——0=「全部」档回归
        //（域内全取、费用×3=TargetCountAll；BindAtomTargetRow 按行 NoWhole/关键词/指示物位过滤 0）。
        // 选择模式由 目标数×各原子自身域 推导（CardEffectConverter）——不再手选。
        // 发动速度档 2023-10-15 退役：activate-bar/speed 组删除，新组合 BaseSpeed 恒 0
        //（引擎 0 速语义完备见 SpeedSystem；速度>0 只剩法术提速份额与存量数据）
        // 2026-10-29 P9 作用次数下拉退役；2026-10-30 定案：效果无每回合生效次数上限，
        // 组合阶段不再有次数选项（take-effect 组隐藏）——2026-11-05 时机两档收敛（均 ×1），
        // 2026-11-09 再拆生物专属档（攻/守/横置 ×1.5）；周期多次的价值由结界耐久经济承担，
        // 生物专属多次由宿主自身行动承担，计价均不按次数加价。
        private static readonly int[] CountTiers = { 1, 2, 3, 0 };

        // 描述条文案（原子/奖励原子两处共用——面向新玩家白话口径，不谈名义值/锚点等内部概念）
        private const string ValueFieldDesc =
            "数值：效果的强度，比如伤害点数、抽牌张数。费用按这里填的数值计算；开了「数值随机」后，实际结算会在数值上下浮动。";
        private const string AmpSliderDesc =
            "数值随机：实际结算的数值会在填写的数值上下随机浮动，比例越大浮动越大。费用仍按填写的数值计算，不会因为随机而变贵。";

        /// <summary>固定战斗原子：攻击/守卫=生物卡默认携带——移出原子库不可组合。</summary>
        private static bool IsFixedBattleAtom(string enumName)
            => enumName == "Attack" || enumName == "Guard";

        /// <summary>锁定关键词行（移出原子库、不可组合、不展示）：攻击/守卫
        /// + 位 6 系统内部行（2026-10-04：修改攻击力/生命值/费用——留给系统，不暴露给玩家组合）。
        /// 引擎主干行（位 5，2026-10-05 回表）**不在锁定期**——玩家直接在原子库选，填槽即自由分支。</summary>
        private static bool IsLockedKeywordRow(AtomicEffectConfig r)
            => r != null && (IsFixedBattleAtom(r.EnumName)
                || ComposerCatalog.HasMountBit(r, MountKind.SystemInternal));

    }
}
