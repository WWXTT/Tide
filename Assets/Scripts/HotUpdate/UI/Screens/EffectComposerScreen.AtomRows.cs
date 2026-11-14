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
    public sealed partial class EffectComposerScreen : UIScreen
    {
        // ======================================== 每原子行与卡面 des（2026-10-09 下沉定案） ========================================

        /// <summary>原子表行四参数 des 文本绑定（2026-10-09 定案格式——卡面常显，槽/奖励/光环卡共用；
        /// 行缺失 cfg=null 显「—」）。atom=条目实例（2026-11-06 实例域修复）：作用范围按实例显式
        /// kinds/domain 收窄——代价门锁侧镜像（如对方牌库）、组合器收窄不再被极性收窄吞掉；
        /// 不传/实例无显式域=旧行为（极性收窄/原文），主干/奖励/光环行不受影响。</summary>
        private static void BindAtomDes(RectTransform host, AtomicEffectConfig cfg, AtomicEffectEntry atom = null)
        {
            var des = UiKit.FindDeep(host, "des")?.GetComponent<TMP_Text>();
            if (des == null)
            {
                Debug.LogError("[EffectComposer] 卡面缺 des 参数文本——检查 EffectUI.prefab");
                return;
            }
            des.text = cfg != null ? AtomParamLine(cfg, atom) : "—";
            UiKit.Size(des, fw: 1f);
        }

        /// <summary>原子表行四参数文本（2026-10-09 定案格式；P2-1 域优先）：
        /// 作用范围=cfg.Domain 原文（多 token 原样 CSV）优先、空则旧 TargetKinds 原文（逐原子自身域）、
        /// 筛选标签=TargetFilter 原文（域内属性筛选）、极性=±1/0、费用基准=ManaList 非零色文本化
        ///（保留小数锚）；空项显「无」。</summary>
        private static string AtomParamLine(AtomicEffectConfig cfg, AtomicEffectEntry atom = null)
        {
            // 作用范围（2026-11-06 极性定案）：p≠0 行显示极性收窄后的**有效**作用域（行域「双方」
            // 只是双极性并集声明——非场地效果按极性定侧，与 converter 候选同口径）；p=0 显原文。
            // 场地光环行（2026-11-07 定案）：**不读 Domain**——作用方向由仪典步 value（RuleAuraScope）
            // 承载（己方/对方/双方），这里直接显示所选范围。
            // 实例域优先（2026-11-06 代价门第二槽修复）：实例显式 domain/kinds（代价锁侧镜像、
            // 组合器收窄）= 真实候选域，优先于极性收窄——与 AtomText.TargetNoun/converter 同口径
            //（kinds 收窄空回落极性收窄）。
            bool auraRow = Enum.TryParse<AtomicEffectType>(cfg?.EnumName ?? "", out var auraType)
                && RuleAuraComponents.IsAuraLike(auraType);
            string scope;
            if (auraRow)
                scope = RuleAuraSystem.RuleAuraScopeZh(atom?.value ?? (int)RuleAuraScope.Both);
            else if (!string.IsNullOrEmpty(cfg?.Domain) && TargetAxisRules.TryParseDomain(cfg.Domain, out var scopeAxis))
            {
                if (!string.IsNullOrEmpty(atom?.domain))
                    scope = TargetAxisRules.TryParseDomain(atom.domain, out var instAxis) ? instAxis.Zh : atom.domain;
                else if (!string.IsNullOrEmpty(cfg.RefDomain))
                    // 参照域行（步4）：kinds 不收窄受体域——作用范围恒为极性收窄后的行 Domain
                    scope = TargetAxisRules.NarrowByPolarity(scopeAxis, cfg.Polarity).Zh;
                else if (atom?.kinds != null && atom.kinds.Count > 0)
                {
                    var narrowed = TargetAxisRules.NarrowByKinds(scopeAxis, atom.kinds);
                    scope = narrowed != null && narrowed.Selectors.Count > 0 ? narrowed.Zh
                            : TargetAxisRules.NarrowByPolarity(scopeAxis, cfg.Polarity).Zh;
                }
                else scope = TargetAxisRules.NarrowByPolarity(scopeAxis, cfg.Polarity).Zh;
            }
            else scope = !string.IsNullOrEmpty(cfg?.Domain) ? cfg.Domain : "无";
            string filter = string.IsNullOrEmpty(cfg?.TargetFilter) ? "无" : cfg.TargetFilter;
            float p = UnityEngine.Mathf.Clamp(cfg?.Polarity ?? 0f, -1f, 1f);
            string polarity = p > 0.5f ? "+1" : p < -0.5f ? "−1" : "0";
            var cost = cfg?.ManaList;
            string mana = cost == null || cost.IsZero ? "无"
                : string.Join("", cost.NonzeroColors().Select(c => $"{cost[c]:0.#}{ColorZh(c)}"));
            // 参照域独立行（步4 §2.1：参照/受体各自独立——合成器只读显示，目标下拉只用 Domain）
            string refSeg = !string.IsNullOrEmpty(cfg?.RefDomain)
                ? $" 参照域：{cfg.RefDomain}（{(string.IsNullOrEmpty(cfg.RefPick) ? "Cast" : cfg.RefPick)}）"
                : "";
            return $"作用范围：{scope}{refSeg} 筛选标签：{filter} 极性：{polarity} 费用基准：{mana}";
        }

        /// <summary>dd-domain 选项集（2026-11-07 极性单选定案，用户定案）：Domain 逐段出选项，一段一项：
        /// ① 已是单侧的段（己方/对方…）**原样一项、不按极性解析**——单侧写法是刻意的：单侧域不可作
        /// 代价（代价只收「Domain 支持双方、能用域解析出执行方」的行），把它补成双方会误开代价口；
        /// ② 双方段按行极性**单选**解析：p&gt;0 → 己方对应项、p&lt;0 → 对方对应项、p=0 → 保持双方
        ///（极性是单选不是并集——满足 1 时不再执行 0 的双方项；例：移除全部指示物 p=0 只给双方战场:生物）；
        /// ③ 参照域（活体炸弹的己方生物参照等）不进目标下拉——参照段由表域侧维护（表行 Domain
        /// 现只写目标域），此口不做类型特判，避免把同形的“己方战场:生物”目标段误删。
        /// 场地光环/连接光环行（IsAuraLike）不适用（2026-11-07 定案）：光环不需要 Domain——
        /// 场地光环方向由作用范围下拉（己方/对方/双方，极性门控）承载、连接光环只取箭头指向单位；
        /// 本口对光环行一律返回空（dd-domain 隐藏）。
        /// 授予行保底三侧生物授予域**只对行域没有实际目标（空域/仅 Self 的旧存储态行）补**；
        /// 行域已声明手牌/牌库/战场等实际区域时按 Domain 解析，不再补战场三侧。
        /// 返回实际域选项表（空实例域=占位显示首项不写回；一项也显示——域必须可见，
        /// 空 Domain 行由 BindAtomTargetRow 的 hasDomain 整行隐藏）。</summary>
        internal static List<string> DomainOptionsOf(AtomicEffectConfig cfg, bool grantRow)
        {
            float rowPol = UnityEngine.Mathf.Clamp(cfg?.Polarity ?? 0f, -1f, 1f);
            bool auraRow = Enum.TryParse<AtomicEffectType>(cfg?.EnumName ?? "", out var auraType)
                && RuleAuraComponents.IsAuraLike(auraType);
            var domOptions = new List<string>();
            // 光环不需要 Domain（2026-11-07 定案）：场地光环方向由作用范围下拉（己方/对方/双方）承载，
            // 连接光环只取箭头指向单位——本口对光环行一律不产选项（dd-domain 隐藏）。
            if (auraRow) return domOptions;
            TargetAxisRules.TryParseDomain(cfg?.Domain, out var rowAxis); // 失败=null
            if (rowAxis != null)
                foreach (var selector in rowAxis.Selectors)
                {
                    if (selector == null) continue;
                    // 单段轴：单侧 selector 原样；Both selector 按极性收窄成单侧（p=0 原样）。
                    // 不再产 己方/对方/双方 三个变体——双方段只出极性那一侧，单侧段不出对侧。
                    var single = new TargetAxis();
                    single.Selectors.Add(selector);
                    string eff = TargetAxisRules.NarrowByPolarity(single, rowPol).Zh;
                    if (!domOptions.Contains(eff)) domOptions.Add(eff);
                }
            // 授予行保底三侧：只在行域**没有实际目标**（空域/仅 Self 的旧存储态行）时补——
            // 行域已声明手牌/牌库/战场等实际区域时按 Domain 解析，不再补「己方/对方/双方战场:生物」
            //（费用增减等双方区域行按极性收窄到单侧手牌，不补战场）。
            bool rowHasRealTarget = rowAxis != null && rowAxis.Selectors.Any(s =>
                s != null && s.Region != TargetRegion.Self);
            if (grantRow && !rowHasRealTarget)
                foreach (var g in new[] { "己方战场:生物", "对方战场:生物", "双方战场:生物" })
                {
                    string eff = rowPol != 0f && TargetAxisRules.TryParseDomain(g, out var gAxis)
                        ? TargetAxisRules.NarrowByPolarity(gAxis, rowPol).Zh : g;
                    if (!domOptions.Contains(eff)) domOptions.Add(eff);
                }
            return domOptions;
        }

        /// <summary>旧数量域闸（2026-11-07 按**区域**判定后不再调用，保留兼容）：以 牌库/角色/手牌/墓地/除外区
        /// 为对象的域目标数量恒 1——只有场上单位（战场域）才可多选（2/3）与域全选（全部档）。有效域取值序：
        /// ①实例域（dd-domain 所选）；②**下拉占位首项**（实例域未声明时 dd-domain 占位显示首项、
        /// 不写回——闸随显示域判，否则行域含角色会误锁与所见矛盾，2026-11-07 晚修复）；③行 Domain
        ///（无选项=下拉隐藏，如光环 p≠0）。区域收窄/极性收窄只改侧别不改区域——判区域无须收窄。
        /// 域不可解析（legacy/空）= 不锁（沿旧档——战场行为多数，不误伤）。返回 true=纯战场域（可多选）。</summary>
        internal static bool BattlefieldOnlyDomainOf(AtomicEffectConfig cfg, AtomicEffectEntry atom)
        {
            TargetAxis axis = null;
            if (!string.IsNullOrEmpty(atom?.domain) && TargetAxisRules.TryParseDomain(atom.domain, out var instAxis))
                axis = instAxis;
            else
            {
                var mounts = MountKindExtensions.ParseCsv(cfg?.MountKinds ?? "");
                bool grant = mounts.Contains(MountKind.Keyword) || mounts.Contains(MountKind.Counter);
                var opts = DomainOptionsOf(cfg, grant);
                if (opts.Count > 0 && TargetAxisRules.TryParseDomain(opts[0], out var firstAxis)) axis = firstAxis;
                else if (TargetAxisRules.TryParseDomain(cfg?.Domain, out var rowAxis)) axis = rowAxis;
            }
            if (axis == null || axis.Selectors.Count == 0) return false;
            foreach (var s in axis.Selectors)
                if (s == null || TargetRegionRules.ZoneOf(s.Region) != Zone.Battlefield)
                    return false; // 任一非战场 selector（手牌/牌库/墓地/除外区/角色/自己）→ 锁 1
            return true;
        }

        /// <summary>数量档判定用有效轴（2026-11-07 定案）：实例域（dd-domain 换选）优先 → 行 Domain；
        /// 与显示/运行时候选同源——全域只看区域（战场/手牌 vs 牌库/墓地/除外区/元素池）。</summary>
        private static TargetAxis CountAxisOf(AtomicEffectConfig cfg, AtomicEffectEntry atom)
        {
            if (!string.IsNullOrEmpty(atom?.domain)
                && TargetAxisRules.TryParseDomain(atom.domain, out var inst)) return inst;
            if (!string.IsNullOrEmpty(cfg?.Domain)
                && TargetAxisRules.TryParseDomain(cfg.Domain, out var row)) return row;
            return null;
        }

        /// <summary>每原子目标行（placeholder：dd-num 目标数量 1/2/3 + rand 随机目标 Toggle——2026-10-09
        /// 数量/随机下沉每原子；2026-10-20 增 dd-select 目标选择方式=预选/结算期）：写 atom.count/atom.rand
        /// /atom.defer（未声明=首档占位/显 off 不写回——回落效果级）；
        /// 零域原子（引擎主干/条件行）无目标可选，整行隐藏。槽卡与奖励卡共用。
        /// 2026-10-09 自由分支定案两钉：①隐蔽域强制随机——表行**原生** TargetKinds 含 对方手牌/对方牌库
        ///（隐蔽信息不可指定，随机必须开启不可改；己方牌库可自查不入列；指示物授予注入的 {1..8} 全域
        /// 不参与判定——单位指示物仍可手指定；已展示卡运行时经预选透传点名）；②pinCountOne——
        /// 费用减少指示物奖励数量钉 1（分支奖励计价=REWARD_SHIM TargetCount=1，数量本不改价——钉 1 使
        /// UI=计价=行为）。
        /// 2026-11-14 动态数量（用户定案）：dd-num 在 1/2/3/全部 之外增「动态」档——仅**主干原子**
        /// 开放（allowDynamic=true 由槽位传入；奖励/代价原子不收）；动态=任意数量目标窗（1..候选数），
        /// 费用=基准×实际目标数（不享数量档减免）。选中时若效果还是自动，自动切「主动」并提示
        /// （动态必须发动时计费；结界/光环/引擎钉档拒绝）。allowDynamic=false 不出现该档。</summary>
        private void BindAtomTargetRow(RectTransform host, AtomicEffectEntry atom, Action refresh,
            bool pinCountOne = false, bool costGate = false, bool allowDynamic = false)
        {
            var row = UiKit.FindDeep(host, "placeholder");
            if (row == null)
            {
                Debug.LogError("[EffectComposer] atom-card 缺 placeholder 行（目标数量/随机目标）——检查 EffectUI.prefab");
                return;
            }
            var cfg = AtomicEffectTable.GetByHashId(atom?.refId);
            bool hasDomain = (cfg?.GetTargetKindList()?.Count ?? 0) > 0;
            row.gameObject.SetActive(hasDomain && atom != null);
            if (!hasDomain || atom == null) return;

            var kindList = cfg.GetTargetKindList();
            bool hiddenDomain = kindList.Contains((int)TargetKind.EnemyHand) || kindList.Contains((int)TargetKind.EnemyDeck);
            if (hiddenDomain) atom.rand = 1;
            if (pinCountOne) atom.count = 1;
            // 代价禁整体档（A2 定案 2026-11-05 · 2026-11-06 创作期补闸）：count=0 只允许单目标多选——
            // 与转换器 ConvertPayloadForDisplay 硬拒同口径，此前 UI 四档可选、保存后装载期才被剔。
            if (costGate && atom.count == 0)
            {
                Debug.LogWarning($"[EffectComposer] 代价原子 {cfg.EnumName}（{atom.refId}）不可开「全部」档"
                               + "（代价只允许单目标多选——A2 定案）——数量已收为 1");
                atom.count = 1;
            }

            // 「全部」档开放面（2026-11-07 关键词特判修订）：关键词行=赋予己方战场生物——开放 0 档
            // 数量档（2026-11-07 定案，按**作用域**不按原子族）：战场/手牌允许 1/2/3/全部（全域）；
            // 牌库/墓地/除外区/元素池只允许 1/2/3（全域=超模，如磨牌全磨对手牌库）；Self 只 1。
            // NoWhole 行仍禁 0。指示物/关键词/一般效果同口径——不再按“赋予形态”区分。
            var mounts = MountKindExtensions.ParseCsv(cfg.MountKinds);
            bool isKeywordRow = mounts.Contains(MountKind.Keyword);
            bool isCounterRow = !isKeywordRow && mounts.Contains(MountKind.Counter);
            bool grantRow = isKeywordRow || isCounterRow;
            // 动态数量开放面（2026-11-14）：主干原子（槽位 allowDynamic）+ 非召唤行
            //（召唤数量由自身 num 数量档承载）+ 非 NoWhole（动态同样能选到全取，等价整体档）。
            // 是否「主动效果」不在选项面拦——选中动态时若效果还是自动，自动切主动并提示
            //（动态数量必须发动时计费；结界宿主的宿主形状闸仍会拒绝）。
            bool summonRow = cfg.EnumName == nameof(AtomicEffectType.SummonToken);
            bool dynamicAllowed = allowDynamic && !summonRow
                                  && !IsAuraMode(_mode) // 光环载体入场即生效，无发动时计费
                                  && !hiddenDomain // 隐蔽域强制随机，不能手动选数量
                                  && !mounts.Contains(MountKind.NoWhole)
                                  && !pinCountOne && !costGate;
            // 目标数量按名取 dd-num（行内现有多枚下拉：dd-num/dd-select——不再靠「首个下拉」隐式命中）
            var ddNumNode = UiKit.FindDeep(row, "dd-num");
            if (ddNumNode == null)
                Debug.LogError("[EffectComposer] placeholder 行缺 dd-num 下拉（目标数量不可编）——检查 EffectUI.prefab");

            // 数量域闸（2026-11-07 定案）：有效域=实例域（dd-domain 换选）→行域；全域只看区域，
            // 战场/手牌可 0；牌库/墓地/除外区/元素池 0 → 收 1；Self 2/3/0 → 收 1。dd-domain 换选经
            // RebindCount 重绑（RefreshCard 只刷文本不重绑行——联动自带）。
            TMP_Dropdown ddCount = null;
            void BindCountCore()
            {
                var countAxis = CountAxisOf(cfg, atom);
                bool allowsAll = !mounts.Contains(MountKind.NoWhole)
                                 && TargetAxisRules.AllowsAllTarget(countAxis);
                bool allowsMulti = countAxis != null && countAxis.Selectors.Any(s =>
                    s != null && s.Region != TargetRegion.Self
                    && !TargetRegionRules.IsCharacter(s.Region)); // 角色域只 1（2026-11-07 定案）
                if (!allowsAll && atom.count == 0)
                {
                    Debug.LogWarning($"[EffectComposer] 原子 {cfg.EnumName}（{atom.refId}）不可开「全部」档"
                                   + "（NoWhole 或域含牌库/墓地/除外区/元素池/角色/Self——全域仅限战场/手牌）：数量已收为 1");
                    atom.count = 1;
                }
                if (!allowsMulti && (atom.count == 2 || atom.count == 3))
                {
                    Debug.LogWarning($"[EffectComposer] 原子 {cfg.EnumName}（{atom.refId}）域仅 Self——目标数量已收为 1");
                    atom.count = 1;
                }
                if (atom.count == AtomicEffectInstance.DynamicTargetCount && (!allowsMulti || !dynamicAllowed))
                {
                    Debug.LogWarning($"[EffectComposer] 原子 {cfg.EnumName}（{atom.refId}）不可用动态数量"
                                   + "（仅主动效果主干/非召唤/非不可全体/可多选域）——数量已收为 1");
                    atom.count = 1;
                }
                int[] tiers = costGate ? new[] { 1, 2, 3 } // 代价门恒 1/2/3（禁整体档——A2 定案，创作期即拦）
                    : pinCountOne ? new[] { 1 }
                    : allowsAll ? (dynamicAllowed ? new[] { 1, 2, 3, 0, AtomicEffectInstance.DynamicTargetCount } : CountTiers)
                    : allowsMulti ? (dynamicAllowed ? new[] { 1, 2, 3, AtomicEffectInstance.DynamicTargetCount } : new[] { 1, 2, 3 })
                    : new[] { 1 };
                ddCount = BindTierDropdown(ddNumNode, tiers, atom.count,
                    v =>
                    {
                        atom.count = v;
                        if (v == AtomicEffectInstance.DynamicTargetCount)
                        {
                            // 动态数量=声明期选目标再计费：随机/结算期选择均冲突（转换器同口径归一）
                            atom.rand = 0;
                            atom.defer = 0;
                            // 动态数量只在主动效果成立（费用发动时现付）：自动效果选中即自动切主动
                            if (_graph?.header != null
                                && _graph.header.ActivationType != (int)EffectActivationType.Voluntary)
                            {
                                var actPin = EngineActivationPin(); // 引擎主干钉档：自动钉死的行不可切主动
                                if (HostForbidsVoluntary || IsAuraMode(_mode)
                                    || (actPin != null && actPin.Value != EffectActivationType.Voluntary))
                                {
                                    ShowToast("该效果不能改为主动（宿主/引擎/光环形态）——动态数量不可用（已保持 1 个目标）");
                                    atom.count = 1;
                                    if (ddCount != null)
                                    {
                                        ddCount.SetValueWithoutNotify(Mathf.Max(0, Array.IndexOf(tiers, 1)));
                                        ddCount.RefreshShownValue();
                                    }
                                }
                                else
                                {
                                    _graph.header.ActivationType = (int)EffectActivationType.Voluntary;
                                    _activationDropdown?.SetIndex(
                                        ActivationIndex((int)EffectActivationType.Voluntary));
                                    SyncActivationVisibility();
                                    ShowToast("动态数量需发动时计费——已把效果发动方式切换为「主动」");
                                }
                                refresh?.Invoke();
                                return;
                            }
                        }
                        refresh?.Invoke(); // 数量影响计价/摘要——随改随刷
                    },
                    "目标数量：这个原子发动时选取几个目标——点选项看各档说明。"
                    + "「动态」=发动时自由选目标数，费用按基准×实际目标数收（不享数量档减免，仅主动效果开放）。",
                    labelOf: v => v == AtomicEffectInstance.DynamicTargetCount ? "动态"
                                : v == 0 ? "全部" : v.ToString(),
                    optionDesc: v => v switch
                    {
                        AtomicEffectInstance.DynamicTargetCount =>
                            "动态数量：发动时弹窗自由选 1~候选数个目标，选几个就收几份基础费（1:1，不享数量档减免）；仅主动效果（法术/主动技能）可用。",
                        0 => "域内全取：不弹窗，作用域内所有面积目标（仅战场/手牌开放；费用 3 倍）。",
                        1 => "发动时弹窗由你选 1 个目标（基础价）。",
                        2 => "弹窗由你选 2 个目标（费用 1.5 倍）。",
                        _ => "弹窗由你选 3 个目标（费用 2 倍）。",
                    });
                if (ddCount != null)
                    ddCount.interactable = allowsMulti && !pinCountOne; // 可改=非 Self 域可多选；钉 1/锁域=不可改
            }
            BindCountCore();

            // 通用作用域下拉 dd-domain（2026-11-06 改版：P2-2 关键词专用 → 全行可选·新选择器语法；
            // 2026-11-07 极性单选定案（用户定案，替代「极性三则」）：选项集走 DomainOptionsOf 单源——
            /// Domain 逐段一项：①已是单侧的段原样一项、不按极性解析（单侧域=刻意不可作代价）；
            /// ②双方段按行极性单选：p>0 己方侧 / p<0 对方侧 / p=0 保持双方；③参照域不进目标下拉。
            /// 场地光环行不适用（方向由仪典步 RuleAuraScope 承载，保持原逻辑）。空实例域=占位显示
            /// 首项不写回（改选才落值，与 timing 下拉同口径）；**一项也显示**（域要可见，如占卜
            /// 双方牌库:卡 → 己方牌库:卡），空 Domain 行由 hasDomain 整行隐藏。
            /// 切换随 refresh 刷摘要/价签。
            TMP_Dropdown domDdHook = null; // 域换选→数量档重绑钩子（监听挂接在 tg/ddDefer 声明后——见 RebindCount）
            var domNode = UiKit.FindDeep(row, "dd-domain");
            if (domNode == null)
                Debug.LogWarning("[EffectComposer] placeholder 行缺 dd-domain 下拉（作用域不可选）——检查 EffectUI.prefab");
            else
            {
                var domOptions = DomainOptionsOf(cfg, grantRow);
                domNode.gameObject.SetActive(domOptions.Count > 0);
                if (domOptions.Count > 0)
                {
                    var domDd = DdOf(domNode);
                    domDdHook = domDd;
                    if (domDd == null)
                        Debug.LogWarning("[EffectComposer] dd-domain 节点缺 TMP_Dropdown——作用域不可选");
                    else
                    {
                        domDd.ClearOptions();
                        domDd.AddOptions(domOptions);
                        int domCur = domOptions.IndexOf(atom.domain); // 空/legacy/词表外 → -1 → 占位首项不写回
                        domDd.SetValueWithoutNotify(Mathf.Max(0, domCur));
                        domDd.RefreshShownValue();
                        domDd.onValueChanged.RemoveAllListeners();
                        domDd.onValueChanged.AddListener(i =>
                        {
                            atom.domain = domOptions[Mathf.Clamp(i, 0, domOptions.Count - 1)];
                            refresh?.Invoke(); // 域影响候选/摘要/价签——随改随刷
                        });
                        UiKit.DescribedOptions(domDd,
                            "作用域：实例域覆盖表行默认域——结算候选只在所选域内（域+数量+极性共同确定效果范围）。",
                            i =>
                            {
                                var v = domOptions[Mathf.Clamp(i, 0, domOptions.Count - 1)];
                                return $"实例域改为「{v}」——该原子结算只在所选目标域内取候选（覆盖表行域）。";
                            });
                    }
                }
            }

            var tgNode = UiKit.FindDeep(row, "rand");
            var tg = tgNode != null
                ? tgNode.GetComponent<Toggle>() ?? tgNode.GetComponentInChildren<Toggle>(true)
                : null;
            if (tg == null) Debug.LogError("[EffectComposer] placeholder 行缺 rand Toggle——随机目标不可编；检查 EffectUI.prefab");
            else
            {
                tg.SetIsOnWithoutNotify(atom.rand == 1);
                tg.onValueChanged.RemoveAllListeners();
                if (hiddenDomain) tg.interactable = false; // 隐蔽域：随机必须开启，不可修改
                else tg.onValueChanged.AddListener(on =>
                {
                    atom.rand = on ? 1 : 0; // 随机=正交标志——不弹窗从完整候选域抽取（潜行挡不住）
                    refresh?.Invoke();
                });
                UiKit.Described(tg,
                    "目标随机：开启后这个原子不再弹窗选目标，改为在其作用范围内随机抽取（潜行挡不住随机）。");
            }

            // 目标选择方式（2026-10-20 用户定案）：预选=发动前声明期选（默认·可取消撤销发动）/
            // 结算期=效果处理时选（必须选不可取消，类自动效果）。prefab 静态下拉 dd-select
            //（槽/奖励两模板 placeholder 行各一枚；dd-num=目标数量是独立有效参数不可挪用）；
            // 与随机正交——随机开启=无弹选语境，本下拉禁用。
            TMP_Dropdown ddDefer = null;
            var deferNode = UiKit.FindDeep(row, "dd-select");
            if (deferNode == null)
                Debug.LogError("[EffectComposer] placeholder 行缺 dd-select 下拉（预选/结算期不可编）——检查 EffectUI.prefab");
            else
            {
                ddDefer = BindTierDropdown(deferNode, new[] { 0, 1 }, atom.defer,
                    v =>
                    {
                        atom.defer = v;
                        refresh?.Invoke();
                    },
                    "目标选择：预选=发动前先选好目标，选择时可取消=撤销本次发动（默认）；结算期=扣费后效果处理时才选，必须选不可取消。",
                    labelOf: v => v == 0 ? "预选" : "结算期",
                    optionDesc: v => v == 0
                        ? "预选（默认）：发动宣言前先选目标——发动区就位→选目标（可取消撤销）→连锁检测→扣费。"
                        : "结算期：效果处理时才选目标——类似自动效果必须选、不可取消（费已付卡已承诺）。");
                if (ddDefer != null) ddDefer.interactable = !hiddenDomain && atom.rand != 1;
            }
            if (tg != null && ddDefer != null)
                tg.onValueChanged.AddListener(on => ddDefer.interactable = !hiddenDomain && !on);

            // 「全部」档联动（P2-1）：count=0=域内全取——随机（全取无抽取语境）与结算期选择
            //（全取不弹选）双双禁用；切回 1/2/3 恢复。局部函数捕获 tg/ddDefer——声明于两变量之后，
            // dd-num 的档位联动经下拉第二监听挂接（首监听=BindTierDropdown 的写回，顺序保证同步于写后）。
            void SyncWholeDependents()
            {
                bool whole = atom.count == 0;
                bool dynamic = atom.count == AtomicEffectInstance.DynamicTargetCount;
                if ((whole || dynamic) && atom.rand == 1)
                {
                    atom.rand = 0;
                    if (tg != null) tg.SetIsOnWithoutNotify(false);
                }
                if (dynamic && atom.defer != 0)
                {
                    atom.defer = 0;
                    if (ddDefer != null)
                    {
                        ddDefer.SetValueWithoutNotify(0);
                        ddDefer.RefreshShownValue();
                    }
                }
                if (tg != null && !hiddenDomain) tg.interactable = !whole && !dynamic;
                if (ddDefer != null) ddDefer.interactable = !hiddenDomain && atom.rand != 1 && !whole && !dynamic;
            }
            SyncWholeDependents(); // 存量 count=0 初次绑定即生效
            if (ddCount != null) ddCount.onValueChanged.AddListener(_ => SyncWholeDependents());

            // 数量域闸联动（2026-11-07 用户定案）：dd-domain 换选 → 数量档随有效域重算
            //（锁域收 1+禁改 / 战场域重铺 1/2/3/全部档）。RefreshCard 只刷文本不重绑行——重挂监听
            // 在 tg/ddDefer 声明之后（局部函数对后声明捕获变量的定赋值检查——与全部档第二监听同位）。
            void RebindCount()
            {
                BindCountCore(); // 重绑清空监听——全部档第二监听随此重挂
                if (ddCount != null) ddCount.onValueChanged.AddListener(_ => SyncWholeDependents());
                SyncWholeDependents();
            }
            if (domDdHook != null) domDdHook.onValueChanged.AddListener(_ => RebindCount());
        }

        /// <summary>每原子引用卡行（derivative）——按原子表「参数」列分流：
        /// CardRef（召唤衍生物）＝「选择卡牌」控件（点按开搜索+列表弹层，仅真实生物/结界卡
        /// ——召唤只列生物），选中写卡 ID 进 str、已选显卡名缩略；
        /// CardRef 的变形/模拟（2026-10-30 定案·2026-11-08 落地）＝行隐藏——引用卡与发动时机
        /// 都在卡牌合成界面设置（本屏动态主干提示已注明），效果组合期不填；
        /// EffectRef（2026-11-06 附加诅咒/祝福载荷效果化）＝「选择载荷效果」控件（点按开效果库弹层
        /// ——只列主动效果且极性匹配引擎行的组合效果，资格单源 EffectEligibility.CursePayloadRejectReason），
        /// 选中写效果 id 进 str；
        /// CardName/Dimension（检索/强夺/窃取/筛选展示/预言两行——押注行 2026-11-02 同收紧类别档）
        /// ＝战斗期弹窗接管（本行隐藏；旧预置 str 保留兼容＝战斗弹窗默认值，不再在此输入）。
        /// 召唤衍生物保留 num 数量下拉（1-3）。槽卡与奖励卡共用。</summary>
        private void BindAtomDerivativeRow(RectTransform host, AtomicEffectEntry atom)
        {
            var row = UiKit.FindDeep(host, "derivative");
            if (row == null)
            {
                Debug.LogError("[EffectComposer] atom-card 缺 derivative 行（引用卡ID/召唤数量）——检查 EffectUI.prefab");
                return;
            }
            bool isSummon = IsSummonTokenEntry(atom);
            var cfg = AtomicEffectTable.GetByHashId(atom?.refId);
            bool isCardRef = cfg != null && cfg.Param == AtomicParamKind.CardRef;
            bool isEffectRef = cfg != null && cfg.Param == AtomicParamKind.EffectRef;
            bool isDeclareStr = IsDeclarationStrEntry(atom);
            // 变形/模拟（2026-10-30 定案）：引用卡在卡牌合成界面选择——本屏行隐藏（CardRef 行中
            // 只有召唤衍生物保留选择控件；恒 1 收值与 num 隐藏照走下方公共段）
            bool refInCardComposer = IsGrantMorphEntry(atom) || IsImitateEntry(atom);

            if (isCardRef && !refInCardComposer)
            {
                row.gameObject.SetActive(true);
                BindCardRefField(row, atom, isSummon);
            }
            else if (isEffectRef)
            {
                // 2026-11-10：诅咒/祝福载荷效果改由奖励槽「载荷效果」行承载（原子+效果双通道同区）；
                // 主原子卡的旧 EffectRef 输入行隐藏，避免两套入口。
                row.gameObject.SetActive(false);
            }
            else
            {
                // CardName/Dimension 行（2026-10-31 阶段C）：合成器停用输入——战斗结算期弹窗
                //（卡名文本窗/维度选择窗）；旧 str 只作默认值保留。非参数行照旧隐藏。
                row.gameObject.SetActive(false);
            }

            var numNode = UiKit.FindDeep(row, "num");
            // 数量下拉仅召唤显示（2026-10-13 变形赋予恒 1；宣言/维度行无数量——隐藏）
            if (numNode != null) numNode.gameObject.SetActive(isCardRef && isSummon);
            if (!isSummon)
            {
                if (isCardRef) atom.value = 1; // 变形/模拟恒 1（原口径）
                return;
            }

            var numDd = numNode?.GetComponent<TMP_Dropdown>();
            if (numDd == null) Debug.LogError("[EffectComposer] derivative 行缺 num 下拉——召唤数量不可编；检查 EffectUI.prefab");
            else
            {
                // 档位收窄 1-3（2026-10-10 定案，原 1-5）：旧档 4/5 落数据绑定时归一（>3 收 3、
                // 其余非法值落 1），显示与数据一致后才挂监听
                var tiers = new List<int> { 1, 2, 3 };
                int idx = tiers.IndexOf(atom.value);
                if (idx < 0)
                {
                    atom.value = atom.value > 3 ? 3 : 1;
                    idx = tiers.IndexOf(atom.value);
                }
                numDd.ClearOptions();
                numDd.AddOptions(tiers.Select(v => v.ToString()).ToList());
                numDd.SetValueWithoutNotify(idx);
                numDd.RefreshShownValue();
                numDd.onValueChanged.RemoveAllListeners();
                numDd.onValueChanged.AddListener(i =>
                {
                    atom.value = tiers[Mathf.Clamp(i, 0, tiers.Count - 1)];
                    RefreshSlots();
                });
                UiKit.Described(numDd, "召唤数量（1-3）：一次召唤几个衍生物——每个都是独立实例，战场满位时后续入墓。");
            }
        }

        /// <summary>只读输入位点按接线（2026-11-08 修复）：InputField 节点已挂 TMP_InputField
        ///（Selectable 子类），Button 同为 Selectable——DisallowMultipleComponent 令
        /// AddComponent&lt;Button&gt; 被引擎拒收并返回 null（曾致「Can't add 'Button' to InputField」
        /// 后紧跟 NRE）。改挂 EventTrigger（普通 MonoBehaviour 无 Selectable 冲突）承载点按；
        /// interactable=false 不影响其触发（不走 Selectable 状态机）。重绑先清旧 PointerClick
        /// 条目（等价原 RemoveAllListeners）。</summary>
        private static void BindInputFieldTap(TMP_InputField field, Action onTap)
        {
            var trig = field.gameObject.GetComponent<EventTrigger>();
            if (trig == null) trig = field.gameObject.AddComponent<EventTrigger>();
            trig.triggers ??= new List<EventTrigger.Entry>();
            trig.triggers.RemoveAll(e => e != null && e.eventID == EventTriggerType.PointerClick);
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            entry.callback.AddListener(_ => onTap());
            trig.triggers.Add(entry);
        }

        /// <summary>CardRef 行选择控件（2026-10-31 阶段C）：InputField 退化为只读显示位
        ///（已选=卡名缩略+ID，未选=占位提示），点按本体开卡牌选择弹层。</summary>
        private void BindCardRefField(RectTransform row, AtomicEffectEntry atom, bool creatureOnly)
        {
            var idField = UiKit.FindDeep(row, "InputField")?.GetComponentInChildren<TMP_InputField>(true);
            if (idField == null)
            {
                Debug.LogError("[EffectComposer] derivative 行缺 InputField——引用卡不可选；检查 EffectUI.prefab");
                return;
            }
            idField.interactable = false;
            idField.readOnly = true;
            RefreshCardRefField(idField, atom.str, creatureOnly);
            BindInputFieldTap(idField, () => OpenCardRefPicker(atom, creatureOnly,
                () => RefreshCardRefField(idField, atom.str, creatureOnly)));
            UiKit.Described(idField, creatureOnly
                ? "选择卡牌（召唤衍生物）：指向一张真实生物卡——实例=该卡全参数复制、恒落战场。"
                  + "未选则保存被拦（与装载期校验同口径）。"
                : "选择卡牌（变形/模拟/诅咒/祝福载荷）：指向一张真实生物或结界卡——选中写卡 ID 进原子。"
                  + "未选则保存被拦（与装载期校验同口径）。");
        }

        /// <summary>引用卡显示位刷新：已选=卡名缩略（ID 备查·断链照实报）；未选=占位提示。</summary>
        private static void RefreshCardRefField(TMP_InputField field, string str, bool creatureOnly)
        {
            CardData picked = string.IsNullOrEmpty(str) ? null : CardCatalog.GetById(str);
            field.SetTextWithoutNotify(picked != null
                ? $"已选：{(string.IsNullOrEmpty(picked.CardName) ? picked.ID : picked.CardName)}"
                : "");
            if (field.placeholder is TMP_Text ph)
                ph.text = string.IsNullOrEmpty(str)
                    ? (creatureOnly ? "（未选——点此选择生物卡）" : "（未选——点此选择生物/结界卡）")
                    : (picked == null ? $"[断链] {str}" : $"（点此重选·现 {str}）");
        }

        /// <summary>卡牌选择弹层（2026-10-31 阶段C）：搜索 + 列表（仅真实生物/结界卡——
        /// 剔除衍生物模板与教学卡；召唤只列生物）。选中写 atom.str 并刷新槽位摘要。</summary>
        private UiKit.Modal _cardRefModal;

        private void OpenCardRefPicker(AtomicEffectEntry atom, bool creatureOnly, Action onPicked)
        {
            if (_cardRefModal != null) { _cardRefModal.Close(); _cardRefModal = null; } // 重复点开=换卡窗
            // 召唤/变形槽原子（2026-10-31 三条过滤）：候选叠加费≥1·无代价·不嵌套（与卡面衍生物槽
            // 选卡器同源 DeckTokenSupport.TokenTemplateCandidateOk——两入口口径一致；诅咒/祝福载荷不动）
            bool tokenSlotAtom = IsSummonTokenEntry(atom) || IsGrantMorphEntry(atom);
            var rowName = AtomicEffectTable.GetByHashId(atom?.refId)?.DisplayName ?? "引用卡";
            var modal = UiKit.ModalBox(Root, $"选择卡牌——{rowName}{(creatureOnly ? "（仅生物）" : "（生物/结界）")}",
                width: 660f, height: 600f);
            _cardRefModal = modal;

            var searchRow = UiKit.Row("search", modal.Panel, spacing: 8f);
            UiKit.Size(UiKit.Label("lbl", searchRow, "搜索：", 13, UiStyle.TextSecondary), w: 40f);
            var searchField = UiKit.InputField("input", searchRow, "卡名 / ID 关键词", "",
                width: 400f);
            UiKit.Button("btn-close", searchRow, "关闭", () =>
            {
                _cardRefModal?.Close();
                _cardRefModal = null;
            }, height: 30f, fontSize: 13);

            var scroll = UiKit.ScrollColumn("list", modal.Panel);
            UiKit.Size(scroll.Rect, fw: 1f, fh: 1f);

            void Rebuild()
            {
                for (int i = scroll.Content.childCount - 1; i >= 0; i--)
                    UnityEngine.Object.Destroy(scroll.Content.GetChild(i).gameObject);
                string key = (searchField.text ?? "").Trim();
                var candidates = CardSorter.Sort(CardCatalog.LoadAll()
                        .Where(c => CardRefCandidateOk(c, creatureOnly))
                        .Where(c => !tokenSlotAtom || DeckTokenSupport.TokenTemplateCandidateOk(c, creatureOnly))
                        .Where(c => string.IsNullOrEmpty(key)
                                    || (c.CardName ?? "").Contains(key, StringComparison.OrdinalIgnoreCase)
                                    || (c.ID ?? "").Contains(key, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
                const int cap = 150;
                int shown = 0;
                foreach (var card in candidates)
                {
                    if (shown++ >= cap) break;
                    var pick = card;
                    var line = UiKit.Row("card-" + pick.ID, scroll.Content, spacing: 8f);
                    UiKit.Size(UiKit.Label("cost", line, ((int)pick.TotalCost).ToString(), 13, UiStyle.TextFaint), w: 30f);
                    UiKit.Size(UiKit.Label("name", line,
                        string.IsNullOrEmpty(pick.CardName) ? pick.ID : pick.CardName, 13), w: 200f);
                    UiKit.Size(UiKit.Label("meta", line, CardRefMetaOf(pick), 12, UiStyle.TextFaint), fw: 1f);
                    bool current = atom.str == pick.ID;
                    UiKit.Button("btn-pick", line, current ? "已选 ✓" : "选择", () =>
                    {
                        atom.str = pick.ID;
                        RefreshSlots(); // 摘要（{引用卡}→卡名）与装载校验随写刷新
                        onPicked?.Invoke();
                        _cardRefModal?.Close();
                        _cardRefModal = null;
                    }, bg: current ? UiStyle.SelectedRowBg : null, height: 30f, fontSize: 13);
                }
                UiKit.Label("count", scroll.Content,
                    candidates.Count == 0
                        ? "（无匹配卡——换关键词）"
                        : shown < candidates.Count ? $"共 {candidates.Count} 张，仅列前 {cap} 张——用搜索缩小范围"
                        : $"共 {candidates.Count} 张", 12, UiStyle.TextHint);
            }

            searchField.onValueChanged.RemoveAllListeners();
            searchField.onValueChanged.AddListener(_ => Rebuild());
            Rebuild();
        }

        /// <summary>选择器候选资格（2026-10-31 阶段C）：真实生物/结界卡——剔除衍生物模板
        ///（IsTokenTemplate）、教学卡与训练卡（2026-11-03 分家：训练池不可见）；召唤衍生物只收
        /// 生物（装载期 ValidateMountHosts 同口径）。</summary>
        private static bool CardRefCandidateOk(CardData card, bool creatureOnly)
        {
            if (card == null || string.IsNullOrEmpty(card.ID) || card.IsTokenTemplate) return false;
            if (CardCatalog.IsTeachingCard(card)) return false;
            if (CardCatalog.IsTrainingCard(card)) return false;
            return creatureOnly
                ? card.Supertype == Cardtype.Creature
                : card.Supertype == Cardtype.Creature || card.Supertype == Cardtype.Enchantment;
        }

        // ======================================== 附加诅咒/祝福·载荷效果（2026-11-06 载荷效果化） ========================================

        /// <summary>当前主干是否为附加诅咒/附加祝福引擎（载荷=效果引用通道）。</summary>
        private static bool IsCurseEngine(BranchEngineKind kind)
            => kind == BranchEngineKind.CurseOnDraw || kind == BranchEngineKind.BlessingOnDraw;

        /// <summary>附加诅咒/祝福载荷校验（2026-11-10 双通道）：原子 Then（≤1）与主动·并列效果引用
        /// 至少一个；效果引用须解析成功、主动、极性匹配（祝福+1/诅咒−1）、并列形状。null=合规。
        /// 槽区校验与 ZonesAllValid 共用；装载期 ValidateAtomParams 同口径兜底直改 JSON。</summary>
        private static string CursePayloadError(AtomicEffectEntry atom, BranchEngineKind kind)
        {
            bool blessing = kind == BranchEngineKind.BlessingOnDraw;
            var b = atom?.branch;
            var fx = !string.IsNullOrEmpty(b?.effectId) ? b.effectId : atom?.str;
            bool hasAtom = b?.then != null && b.then.Count > 0;
            if (!hasAtom && string.IsNullOrEmpty(fx))
                return blessing
                    ? "附加祝福：载荷=单个原子 和/或 主动·极性+1 的并列效果——点本槽选原子，或点「载荷效果」行选组合效果"
                    : "附加诅咒：载荷=单个原子 和/或 主动·极性−1 的并列效果——点本槽选原子，或点「载荷效果」行选组合效果";
            if (b?.then != null && b.then.Count > 1)
                return $"载荷原子 {b.then.Count} 个——附加诅咒/祝福只收单个原子（并列组合请用效果引用）";
            if (hasAtom)
            {
                var row = AtomicEffectTable.GetByHashId(b.then[0]?.refId);
                if (row == null) return $"载荷原子 {b.then[0]?.refId} 不在原子表——请改选";
                if (!ComposerCatalog.CanBeRewardRow(row)) return $"载荷原子 {row.DisplayName} 无奖励资格——请改选";
                bool positive = row.Polarity >= 0.5f, negative = row.Polarity <= -0.5f;
                if ((blessing && !positive) || (!blessing && !negative))
                    return $"载荷原子 {row.DisplayName} 极性不匹配（祝福只收 +1 / 诅咒只收 −1）——请改选";
            }
            if (string.IsNullOrEmpty(fx)) return null;
            var data = EffectsLibrary.Resolve(fx);
            if (data == null) return $"载荷效果 '{fx}' 不在效果库（悬空——效果可能已被删除），请改选";
            var rowCfg = AtomicEffectTable.GetByHashId(atom.refId);
            var reason = EffectEligibility.CursePayloadRejectReason(data, rowCfg?.Polarity ?? 0f)
                         ?? EffectEligibility.BranchRewardRejectReason(data);
            return reason != null ? $"载荷效果 '{fx}' {reason}——请改选" : null;
        }

        /// <summary>EffectRef 行选择控件（2026-11-06 载荷效果化）：InputField 只读显示位
        ///（已选=效果名+ID，未选=占位提示），点按开载荷效果选择弹层。资格单源
        /// EffectEligibility.CursePayloadRejectReason——只列主动效果且极性匹配引擎行
        ///（附加祝福=+1 给自己 / 附加诅咒=−1 给对手）。</summary>
        private void BindEffectRefField(RectTransform row, AtomicEffectEntry atom)
        {
            var idField = UiKit.FindDeep(row, "InputField")?.GetComponentInChildren<TMP_InputField>(true);
            if (idField == null)
            {
                Debug.LogError("[EffectComposer] derivative 行缺 InputField——载荷效果不可选；检查 EffectUI.prefab");
                return;
            }
            var cfg = AtomicEffectTable.GetByHashId(atom?.refId);
            float polarity = cfg?.Polarity ?? 0f;
            idField.interactable = false;
            idField.readOnly = true;
            RefreshEffectRefField(idField, atom.str, polarity);
            BindInputFieldTap(idField, () => OpenEffectRefPicker(atom,
                () => RefreshEffectRefField(idField, atom.str, polarity)));
            UiKit.Described(idField, polarity >= 0.5f
                ? "选择载荷效果（附加祝福→给自己牌库）：只列主动效果且极性 +1 的组合效果——抽到该卡时以你为控制者自动结算（一次性）。"
                  + "未选则保存被拦（与装载期校验同口径）。"
                : "选择载荷效果（附加诅咒→给对手牌库）：只列主动效果且极性 −1 的组合效果——对手抽到该卡时以你为控制者自动结算（一次性）。"
                  + "未选则保存被拦（与装载期校验同口径）。");
        }

        /// <summary>载荷效果显示位刷新：已选=效果名缩略（ID 备查·断链照实报）；未选=占位提示。</summary>
        private static void RefreshEffectRefField(TMP_InputField field, string str, float polarity)
        {
            var picked = string.IsNullOrEmpty(str) ? null : EffectsLibrary.Get(str);
            field.SetTextWithoutNotify(picked != null ? $"已选：{picked.name}" : "");
            if (field.placeholder is TMP_Text ph)
                ph.text = string.IsNullOrEmpty(str)
                    ? (polarity >= 0.5f ? "（未选——点此选择极性+1主动效果）" : "（未选——点此选择极性−1主动效果）")
                    : (picked == null ? $"[断链] {str}" : $"（点此重选·现 {str}）");
        }

        /// <summary>载荷效果选择弹层（2026-11-06 载荷效果化）：搜索 + 效果库列表（主∪教学——
        /// 训练表不随包下发不入；重复 id 保主表，与光环效果库同口径）。只列主动效果且极性匹配
        /// 引擎行的组合效果（资格单源 CursePayloadRejectReason）。选中写 atom.str=效果 id。</summary>
        private UiKit.Modal _effectRefModal;

        private void OpenEffectRefPicker(AtomicEffectEntry atom, Action onPicked)
        {
            if (_effectRefModal != null) { _effectRefModal.Close(); _effectRefModal = null; } // 重复点开=换窗
            var cfg = AtomicEffectTable.GetByHashId(atom?.refId);
            float polarity = cfg?.Polarity ?? 0f;
            bool blessing = polarity >= 0.5f;
            var rowName = cfg?.DisplayName ?? "载荷效果";
            var modal = UiKit.ModalBox(Root,
                $"选择载荷效果——{rowName}{(blessing ? "（主动·极性+1·给自己）" : "（主动·极性−1·给对手）")}",
                width: 660f, height: 600f);
            _effectRefModal = modal;

            var searchRow = UiKit.Row("search", modal.Panel, spacing: 8f);
            UiKit.Size(UiKit.Label("lbl", searchRow, "搜索：", 13, UiStyle.TextSecondary), w: 40f);
            var searchField = UiKit.InputField("input", searchRow, "效果名 / ID 关键词", "", width: 400f);
            UiKit.Button("btn-close", searchRow, "关闭", () =>
            {
                _effectRefModal?.Close();
                _effectRefModal = null;
            }, height: 30f, fontSize: 13);

            var scroll = UiKit.ScrollColumn("list", modal.Panel);
            UiKit.Size(scroll.Rect, fw: 1f, fh: 1f);

            void Rebuild()
            {
                for (int i = scroll.Content.childCount - 1; i >= 0; i--)
                    UnityEngine.Object.Destroy(scroll.Content.GetChild(i).gameObject);
                string key = (searchField.text ?? "").Trim();
                // 资格判定走运行时同源通道：EffectsLibrary.Resolve 还原完整 Steps（EffectGraphData.header
                // 只带头部字段不带原子——直接喂 header 会极性判定恒空）；EffectId/结算/装载校验同链。
                var candidates = EffectLibrarySerializer.LoadAll()               // 主表
                    .Concat(EffectLibrarySerializer.LoadAll(tutorial: true))      // 教学副表（重复 id 保主表）
                    .GroupBy(g => g.id).Select(grp => grp.First())
                    .Where(g => g != null && !string.IsNullOrEmpty(g.id))
                    .Select(g => new { graph = g, data = EffectsLibrary.Resolve(g.id) })
                    .Where(t => t.data != null
                                && EffectEligibility.CursePayloadRejectReason(t.data, polarity) == null
                                && EffectEligibility.BranchRewardRejectReason(t.data) == null)
                    .Where(t => string.IsNullOrEmpty(key)
                                || (t.graph.name ?? "").Contains(key, StringComparison.OrdinalIgnoreCase)
                                || (t.graph.id ?? "").Contains(key, StringComparison.OrdinalIgnoreCase))
                    .Select(t => t.graph)
                    .ToList();
                const int cap = 150;
                int shown = 0;
                foreach (var g in candidates)
                {
                    if (shown++ >= cap) break;
                    var pick = g;
                    var timing = (TriggerTiming)pick.header.TriggerTiming;
                    string timingZh = AtomText.TimingZh.TryGetValue(timing, out var tz) ? tz : timing.ToString();
                    int atomN = 0;
                    foreach (var s in pick.steps ?? new List<EffectStepData>())
                        if (s?.kind == 0 && s.atomic != null) atomN++;
                    var line = UiKit.Row("fx-" + pick.id, scroll.Content, spacing: 8f);
                    UiKit.Size(UiKit.Label("name", line,
                        string.IsNullOrEmpty(pick.name) ? pick.id : pick.name, 13), w: 220f);
                    UiKit.Size(UiKit.Label("meta", line,
                        $"{timingZh}·主动·{(blessing ? "极性+1" : "极性−1")}·{atomN} 原子", 12, UiStyle.TextFaint), fw: 1f);
                    bool current = atom.str == pick.id;
                    UiKit.Button("btn-pick", line, current ? "已选 ✓" : "选择", () =>
                    {
                        atom.str = pick.id;
                        RefreshSlots(); // 摘要/校验/附加费预览随写刷新
                        onPicked?.Invoke();
                        _effectRefModal?.Close();
                        _effectRefModal = null;
                    }, bg: current ? UiStyle.SelectedRowBg : null, height: 30f, fontSize: 13);
                }
                UiKit.Label("count", scroll.Content,
                    candidates.Count == 0
                        ? $"（无可选效果——只列主动·{(blessing ? "极性+1" : "极性−1")}的组合效果，先在效果表组合）"
                        : shown < candidates.Count ? $"共 {candidates.Count} 条，仅列前 {cap} 条——用搜索缩小范围"
                        : $"共 {candidates.Count} 条", 12, UiStyle.TextHint);
            }

            searchField.onValueChanged.RemoveAllListeners();
            searchField.onValueChanged.AddListener(_ => Rebuild());
            Rebuild();
        }

        /// <summary>候选行 meta 文本：类型 + 攻/血（生物，缺项显 ?）或类型标注。</summary>
        private static string CardRefMetaOf(CardData card)
            => card.Supertype == Cardtype.Creature
                ? $"生物 {(card.Power.HasValue ? card.Power.Value.ToString() : "?")}/{(card.Life.HasValue ? card.Life.Value.ToString() : "?")}"
                : card.Supertype == Cardtype.Enchantment ? "结界" : card.Supertype.ToString();

        // ======================================== 分支奖励效果（2026-11-07 分支奖励效果化） ========================================

        /// <summary>槽内「奖励效果」选择行（2026-11-07 分支奖励效果化；**2026-11-10 R4 修订**：
        /// 引擎分支与有限分支共用同一双通道——奖励=组合效果引用+原子并存，效果经弹层选择（右栏库仍
        /// 只列原子），原子照旧点库落槽；两通道费合计并入分支预算（超限禁存）与引擎门槛/倒计时换算
        /// 推导。2026-11-08 阶段4 的「引擎奖励单通道/时机锁定」文案随 R4 退役——弹层统一按
        /// BranchRewardRejectReason 单谓词过滤（只列并列、无代价、无嵌套分支、无无域原子的效果，
        /// 任意发动档；自动三评估窗口=外层时机由玩家在发动条选）。</summary>
        private void MakeRewardFxRow(RectTransform area, BranchEntryData b, RectTransform head, Action refresh)
        {
            var row = UiKit.Row("fx-reward-ref", area, spacing: 6f);
            if (head != null) row.SetSiblingIndex(head.GetSiblingIndex() + 1); // 紧随摘要行
            UiKit.Size(UiKit.Label("lbl", row, "载荷效果：", 12, UiStyle.TextSecondary), w: 62f);
            var state = UiKit.Label("state", row, "", 12);
            UiKit.Size(state, fw: 1f);
            void RefreshState()
            {
                var fxg = string.IsNullOrEmpty(b.effectId) ? null : EffectsLibrary.Get(b.effectId);
                state.text = string.IsNullOrEmpty(b.effectId)
                    ? "（未选——可选主动·并列组合效果；与原子载荷可并存）"
                    : fxg != null ? $"已选：{fxg.name}（{b.effectId}）" : $"[断链] {b.effectId}";
            }
            RefreshState();
            UiKit.Button("btn-pick", row, "选择", () => OpenRewardFxPicker(b), height: 26f, fontSize: 12);
            UiKit.Button("btn-clear", row, "清除", () =>
            {
                b.effectId = null;
                RefreshState();
                refresh?.Invoke();
            }, height: 26f, fontSize: 12);
            UiKit.Described(state,
                "选择载荷效果（2026-11-10）：只列主动发动、并列形状（1~2 原子、无代价/无嵌套分支）且极性匹配的组合效果；"
                + "抽到被诅咒/祝福的卡时先结算该效果、再结算原子载荷；附加费=载荷费减半入卡价。");
        }

        /// <summary>分支奖励效果校验（2026-11-07 分支奖励效果化——保存闸）：须在效果库解析成功、
        /// 通过奖励资格（R4 单谓词 EffectEligibility.BranchRewardRejectReason——并列/无代价/无嵌套
        /// 分支/无无域原子/非引擎主干/非光环）、且不得引用本效果自身（自引用环）。null=合规。
        /// 与装载期 ValidateAtomParams 同口径（自由/有限/引擎分支统一）。</summary>
        private string BranchRewardFxError(BranchEntryData b)
        {
            var fxId = b?.effectId;
            if (string.IsNullOrEmpty(fxId)) return null;
            if (fxId == _graph?.id) return "分支奖励效果不能引用本效果自身（自引用环）——请改选";
            var data = EffectsLibrary.Resolve(fxId);
            if (data == null) return $"奖励效果 '{fxId}' 不在效果库（悬空——效果可能已被删除），请改选";
            var reason = EffectEligibility.BranchRewardRejectReason(data);
            return reason != null ? $"奖励效果 '{fxId}' {reason}——请改选" : null;
        }

        /// <summary>分支奖励效果选择弹层（2026-11-07 分支奖励效果化；**2026-11-10 R4**：自由分支与
        /// 有限分支共用同一选择器）：搜索 + 效果库列表（主∪教学——训练表不随包下发不入，重复 id 保
        /// 主表，与载荷效果弹层同口径）。只列奖励资格通过的组合效果（资格单源 R4 单谓词
        /// BranchRewardRejectReason——只列并列、无代价、无嵌套分支、无无域原子的效果，任意发动档）；
        /// 排除本效果自身防自引用环。选中写 b.effectId——不清 then（双通道并存）。</summary>
        private UiKit.Modal _rewardFxModal;

        private void OpenRewardFxPicker(BranchEntryData b)
        {
            if (_rewardFxModal != null) { _rewardFxModal.Close(); _rewardFxModal = null; } // 重复点开=换窗
            var selfId = _graph?.id;
            var trunkCfg = AtomicEffectTable.GetByHashId(FirstKind0Atom()?.refId);
            float cursePolarity = trunkCfg?.Polarity ?? 0f;
            var modal = UiKit.ModalBox(Root, "选择载荷效果（附加诅咒/祝福·主动·并列效果）", width: 660f, height: 600f);
            _rewardFxModal = modal;

            var searchRow = UiKit.Row("search", modal.Panel, spacing: 8f);
            UiKit.Size(UiKit.Label("lbl", searchRow, "搜索：", 13, UiStyle.TextSecondary), w: 40f);
            var searchField = UiKit.InputField("input", searchRow, "效果名 / ID 关键词", "", width: 400f);
            UiKit.Button("btn-close", searchRow, "关闭", () =>
            {
                _rewardFxModal?.Close();
                _rewardFxModal = null;
            }, height: 30f, fontSize: 13);

            var scroll = UiKit.ScrollColumn("list", modal.Panel);
            UiKit.Size(scroll.Rect, fw: 1f, fh: 1f);

            void Rebuild()
            {
                for (int i = scroll.Content.childCount - 1; i >= 0; i--)
                    UnityEngine.Object.Destroy(scroll.Content.GetChild(i).gameObject);
                string key = (searchField.text ?? "").Trim();
                // 资格判定走运行时同源通道：EffectsLibrary.Resolve 还原完整 Steps（header 不带原子）
                var candidates = EffectLibrarySerializer.LoadAll()               // 主表
                    .Concat(EffectLibrarySerializer.LoadAll(tutorial: true))      // 教学副表（重复 id 保主表）
                    .GroupBy(g => g.id).Select(grp => grp.First())
                    .Where(g => g != null && !string.IsNullOrEmpty(g.id) && g.id != selfId)
                    .Select(g => new { graph = g, data = EffectsLibrary.Resolve(g.id) })
                    .Where(t => t.data != null
                                && EffectEligibility.CursePayloadRejectReason(t.data, cursePolarity) == null
                                && EffectEligibility.BranchRewardRejectReason(t.data) == null)
                    .Where(t => string.IsNullOrEmpty(key)
                                || (t.graph.name ?? "").Contains(key, StringComparison.OrdinalIgnoreCase)
                                || (t.graph.id ?? "").Contains(key, StringComparison.OrdinalIgnoreCase))
                    .Select(t => new { t.graph, cost = CostDerivationService.ReferencedEffectCostByColor(t.graph.id).Total })
                    .ToList();
                const int cap = 150;
                int shown = 0;
                foreach (var c in candidates)
                {
                    if (shown++ >= cap) break;
                    var pick = c.graph;
                    var timing = (TriggerTiming)pick.header.TriggerTiming;
                    string timingZh = AtomText.TimingZh.TryGetValue(timing, out var tz) ? tz : timing.ToString();
                    int atomN = 0;
                    foreach (var s in pick.steps ?? new List<EffectStepData>())
                        if (s?.kind == 0 && s.atomic != null) atomN++;
                    var line = UiKit.Row("fx-" + pick.id, scroll.Content, spacing: 8f);
                    UiKit.Size(UiKit.Label("name", line,
                        string.IsNullOrEmpty(pick.name) ? pick.id : pick.name, 13), w: 220f);
                    UiKit.Size(UiKit.Label("meta", line,
                        $"{timingZh}·{c.cost:0.#} 费·{atomN} 原子", 12, UiStyle.TextFaint), fw: 1f);
                    bool current = b.effectId == pick.id;
                    UiKit.Button("btn-pick", line, current ? "已选 ✓" : "选择", () =>
                    {
                        b.effectId = pick.id;
                        RefreshSlots(); // 摘要/校验/门槛预览随写刷新（then 保留——双通道并存）
                        _rewardFxModal?.Close();
                        _rewardFxModal = null;
                    }, bg: current ? UiStyle.SelectedRowBg : null, height: 30f, fontSize: 13);
                }
                UiKit.Label("count", scroll.Content,
                    candidates.Count == 0
                        ? "（无可选效果——奖励只收并列·无代价·无嵌套分支的组合效果，先在效果表组合）"
                        : shown < candidates.Count ? $"共 {candidates.Count} 条，仅列前 {cap} 条——用搜索缩小范围"
                        : $"共 {candidates.Count} 条", 12, UiStyle.TextHint);
            }

            searchField.onValueChanged.RemoveAllListeners();
            searchField.onValueChanged.AddListener(_ => Rebuild());
            Rebuild();
        }


        /// <summary>仪典作用方向可选集（方向码：1=己方/2=双方/3=对方；2026-11-06 定案翻转）：
        /// 极性 0 行只能双方（表价即实价，无单侧档）；±1 行=极性单侧+双方两档
        ///（缺省双方=表价档，单侧计价 ×RuleAuraSingleSideFactor）。仅仪典卡作用面下拉消费
        ///（连接光环条目作用范围已删、场地属性降低条目恒对方）。</summary>
        private static List<int> AllowedScopesOfPolarity(float p)
            => p >= 0.5f ? new List<int> { 1, 2 } : p <= -0.5f ? new List<int> { 3, 2 } : new List<int> { 2 };

        /// <summary>档位下拉静态绑定（两段式描述）：desc=头部总述；optionDesc=选中项介绍
        ///（index→文本，null/空＝保持总述）。标签由档位值生成（默认 -1/0=「全部」；可传 labelOf
        /// 直显数字）；现值不在档内=首档占位显示不写回（存量兼容——改选才落值，
        /// 同发动方式的系统强制档占位策略）。</summary>
        private static TMP_Dropdown BindTierDropdown(RectTransform node, int[] tiers, int current,
            Action<int> write, string desc, Func<int, string> labelOf = null, Func<int, string> optionDesc = null)
        {
            var dd = DdOf(node);
            if (dd == null) return null;
            labelOf ??= v => v == -1 || v == 0 ? "全部" : v.ToString();
            dd.ClearOptions();
            dd.AddOptions(tiers.Select(labelOf).ToList());
            dd.SetValueWithoutNotify(Mathf.Max(0, Array.IndexOf(tiers, current)));
            dd.RefreshShownValue();
            dd.onValueChanged.RemoveAllListeners();
            dd.onValueChanged.AddListener(i => write(tiers[Mathf.Clamp(i, 0, tiers.Length - 1)]));
            UiKit.DescribedOptions(dd, desc, optionDesc != null
                ? i => optionDesc(tiers[Mathf.Clamp(i, 0, tiers.Length - 1)]) : null);
            return dd;
        }

        /// <summary>当前效果引擎种类（与 InferMode 同口径：**仅引擎主干行**算引擎在案——行身份∪其槽级
        /// 引擎载荷；非引擎行挂的旧引擎载荷按并列单行展示（2026-10-26 定案），不再触发效果头锁定。
        /// null=无引擎）。</summary>
        private BranchEngineKind? EngineKindOfGraph()
        {
            var atom = _graph?.steps?.FirstOrDefault(s => s?.kind == 0 && s.atomic != null)?.atomic;
            if (atom == null) return null;
            var cfg = AtomicEffectTable.GetByHashId(atom.refId);
            if (!ComposerCatalog.IsEngineTrunkRow(cfg)) return null;
            if (atom.branch != null && (BranchSettleKind)atom.branch.settle == BranchSettleKind.Engine
                && Enum.IsDefined(typeof(BranchEngineKind), atom.branch.engine))
                return (BranchEngineKind)atom.branch.engine;
            if (cfg != null && Enum.TryParse<AtomicEffectType>(cfg.EnumName, out var t)) // 行身份兜底（引擎行落槽即挂载荷——双保险）
            {
                var k = ComposerCatalog.EngineKindOf(t);
                if (k != BranchEngineKind.None) return k;
            }
            return null;
        }

        /// <summary>自由分支主干发动方式单值钉（2026-11-08 规则地基定案，取代 2026-10-30 双选组）：
        /// 主动行（附加诅咒/附加祝福）=仅主动；其余引擎行（倒计时/运势/拼点/
        /// 死亡计数/元素充盈/团结）=仅自动；非引擎主干（并列/有限分支/光环）=null（宿主形状闸照旧管辖）。
        ///（模拟/变形 2026-11-08 退役出组合——不再经此口。）
        /// 钉值表单源=ComposerCatalog.EngineActivationPinOf（与验证器共用，图形态判定在调用侧）。
        /// 消费：ApplyEngineHeaderLock（钉值+锁下拉）、发动方式下拉 Changed 闸（非法选择弹回）。</summary>
        private EffectActivationType? EngineActivationPin()
        {
            var atom = _graph?.steps?.FirstOrDefault(s => s?.kind == 0 && s.atomic != null)?.atomic;
            if (atom == null) return null;
            var cfg = AtomicEffectTable.GetByHashId(atom.refId);
            if (!ComposerCatalog.IsEngineTrunkRow(cfg)) return null;
            return Enum.TryParse<AtomicEffectType>(cfg.EnumName, out var t)
                ? ComposerCatalog.EngineActivationPinOf(t)
                : null;
        }

        /// <summary>引擎效果头锁定（2026-10-09 自由分支九项定案；2026-11-08 规则地基定案修订；
        /// **2026-11-10 R4 修订**）：首槽引擎在案 → 速度/生效次数按 ComposerCatalog.EngineHeaderPresetOf
        /// 归一；发动方式按 EngineActivationPinOf 单值钉（四类主动行=仅主动、其余引擎行=仅自动——下拉锁定）；
        /// 时机——主动钉 Activate_*；自动固定组（倒计时/死亡计数/元素充盈）钉预设时机；
        /// 自然缺省组（运势/拼点/团结，FreeTiming）时机可改：外层时机=引擎评估窗口由玩家选
        ///（2026-11-08 阶段4「时机随奖励效果钉」随 R4 退役——主动效果作奖励时引用时机反钉会令
        /// 事件类型永不匹配；预设 Timing 仅作落槽瞬间且图值缺省 OnPlay(0) 时的占位缺省），
        /// 无奖励/有奖励均解锁下拉（时机影响 BranchEngines.OnRoutedEvent 的评估窗口）。
        /// 引擎主干零计价——limit=-1 档无费用影响。
        /// 消费点：RefreshSlots 尾部（覆盖引擎落槽/换引擎/载入/选中奖励四路径）。</summary>
        private BranchEngineKind? _lastEngineLockKind; // 落槽种类跟踪：FreeTiming 缺省只在切换瞬间补，刷新不覆写用户值

        private void ApplyEngineHeaderLock()
        {
            var h = _graph?.header;
            if (h == null) return;
            var engKind = EngineKindOfGraph();
            var preset = engKind != null ? ComposerCatalog.EngineHeaderPresetOf(engKind.Value) : null;
            bool locked = preset != null;
            bool freeTiming = locked && preset.Value.FreeTiming;
            bool justLanded = engKind != _lastEngineLockKind; // null→kind 落槽 / kindA→kindB 换引擎
            _lastEngineLockKind = engKind;

            // 发动方式单值钉（2026-11-08 规则地基）：四类主动行=主动、其余引擎行=自动；null=非引擎主干
            var actPin = EngineActivationPin();

            if (locked)
            {
                if (actPin != null)
                {
                    // 钉死组（全部在册引擎行）：发动方式恒钉值；时机=主动钉 Activate_*、自动固定组钉预设；
                    // 自然缺省组时机可改——只在落槽瞬间补占位缺省，其余刷新不动用户值
                    //（2026-11-10 R4：不再随奖励效果时机反钉——评估窗口由玩家在此选）
                    h.ActivationType = (int)actPin.Value;
                    if (freeTiming)
                    {
                        if (justLanded && h.TriggerTiming == 0) h.TriggerTiming = (int)preset.Value.Timing;
                    }
                    else
                    {
                        h.TriggerTiming = actPin == EffectActivationType.Voluntary
                            ? (int)TriggerTiming.Activate_Active : (int)preset.Value.Timing;
                    }
                }
                else
                {
                    // 未知引擎行兜底（表新增未登记行）：预设 Activation 仅落槽缺省——合法声明值保留
                    if (justLanded || (h.ActivationType != (int)EffectActivationType.Automatic
                                     && h.ActivationType != (int)EffectActivationType.Voluntary))
                        h.ActivationType = (int)preset.Value.Activation;
                    bool vol = h.ActivationType == (int)EffectActivationType.Voluntary;
                    h.TriggerTiming = (int)(vol ? TriggerTiming.Activate_Active : preset.Value.Timing);
                }
                h.BaseSpeed = preset.Value.Speed;
                h.TriggerLimitPerTurn = preset.Value.Limit;
            }

            // 值对齐先行，禁用态最后落——防同步链覆写锁定
            _activationDropdown?.SetIndex(ActivationIndex(h.ActivationType));
            SyncActivationVisibility();
            SyncTimingDropdown();

            // 禁用面（2026-11-08 规则地基修订；2026-11-10 R4）：发动下拉=引擎主干一律锁定
            //（主动行/自动引擎行均单值钉）；时机=自动固定组锁、自然缺省组（运势/拼点/团结）解锁
            //——评估窗口=外层时机由玩家选（2026-11-08 阶段4「先选奖励以确定时机」占位锁随 R4 退役）；
            // 速度组已退役无控件；次数选项已整删。
            if (_activationDropdown != null)
                _activationDropdown.Interactable = actPin == null;
            if (_timingDropdown != null)
                _timingDropdown.Interactable = !locked || freeTiming;
        }

        /// <summary>费用指示物行判定（四色增减 8 行，2026-10-31 四色化）——奖励槽数量钉 1 定案配套
        ///（BindAtomTargetRow；handler 恒 1 层，数量档对全族无效）。</summary>
        private static bool IsAddCostDownRow(AtomicEffectConfig cfg)
            => cfg != null && Enum.TryParse<AtomicEffectType>(cfg.EnumName, out var t)
               && (t == AtomicEffectType.AddCostUpGray || t == AtomicEffectType.AddCostUpRed
                   || t == AtomicEffectType.AddCostUpBlue || t == AtomicEffectType.AddCostUpGreen
                   || t == AtomicEffectType.AddCostDownGray || t == AtomicEffectType.AddCostDownRed
                   || t == AtomicEffectType.AddCostDownBlue || t == AtomicEffectType.AddCostDownGreen);

        /// <summary>prefab 静态下拉取件（节点在而组件缺=数据层可见错误，不静默）。</summary>
        private static TMP_Dropdown DdOf(RectTransform node)
        {
            var dd = node?.GetComponent<TMP_Dropdown>() ?? node?.GetComponentInChildren<TMP_Dropdown>(true);
            if (node != null && dd == null)
                Debug.LogError($"[EffectComposer] 节点 {node.name} 缺 TMP_Dropdown 组件（2026-10-05 prefab 静态化）");
            return dd;
        }

    }
}
