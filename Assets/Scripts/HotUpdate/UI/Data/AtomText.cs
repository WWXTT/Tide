using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using CardCore.Attribute;

namespace SynergyUI
{
    /// <summary>
    /// 原子描述动态渲染（2026-09-14 合成器重做）——槽内摘要 / 检查器实时描述 / 卡级挂载预览共用。
    ///
    /// 模板源 = 原子表 DisplayName 列（装载后落在 config.Description——注意 config.DisplayName
    /// 是 EnumName 列的中文短名，见 AtomicEffectTable.BuildConfig）。模板含 {value} 占位：
    ///   - 普通渲染：{value} → Value；
    ///   - 数值随机（RandomAmplitude&gt;0）：{value} → 区间文本「x至x」即随机语义
    ///     （span=round(|Value|×幅度)，3 伤 ±100% → "对目标造成0至6点伤害"——2026-10-09 句式定案不加前缀）；
    ///   - 目标随机（header.RandomTarget，2026-09-16 自 SelectionMode 移出为正交标志）：
    ///     正文第一个「目标」前插「随机」（"对随机目标造成3点伤害"；域名次冠名次前）。
    /// 计价/构筑读名义 Value 不变——此处只做展示层渲染（口径对齐 2026-09-13 两个随机定案）。
    ///
    /// {target} 占位（2026-09-22 五轮）：按实例域 kinds 推导——null/多值 → 「目标」；
    /// 单值 → TargetKind 中文名（"对己方单位造成1点伤害"）——目标域收窄后描述实时反映作用对象。
    /// </summary>
    public static class AtomText
    {
        /// <summary>TriggerTiming → 中文短名（全量表·单一来源，2026-10-09 时机前缀定案）——
        /// 效果描述开头前缀与合成器时机下拉共用；含系统自用时点（OnAtomicEffect*/OnSummon/OnPhase*，
        /// 下拉侧过滤不开放玩家选择）与主动三档以外的全部枚举值。</summary>
        public static readonly Dictionary<TriggerTiming, string> TimingZh = new Dictionary<TriggerTiming, string>
        {
            { TriggerTiming.OnPlay, "登场时" },
            { TriggerTiming.OnDeath, "死亡时" },
            { TriggerTiming.OnDraw, "抽牌时" },
            { TriggerTiming.OnDealDamage, "造成伤害时" },
            { TriggerTiming.OnTakeDamage, "受到伤害时" },
            { TriggerTiming.OnTurnStart, "回合开始时" }, // 2026-11-14 档位制起开放玩家挂载（一回合一次档，与准备阶段并列）
            { TriggerTiming.OnStandbyPhase, "准备阶段" }, // 系统处理完成后的回合内窗口（与「回合开始」并列、先后不同）
            { TriggerTiming.OnTurnEnd, "回合结束时" },
            { TriggerTiming.OnAttack, "攻击宣言时" },
            { TriggerTiming.OnAttacked, "被攻击时" },
            { TriggerTiming.OnSummon, "召唤进场时" },
            { TriggerTiming.OnTargeted, "被指定为目标时" },
            { TriggerTiming.OnOtherCreatureEnter, "其他生物进场时" },
            { TriggerTiming.OnSpellCast, "施放法术时" },
            { TriggerTiming.OnTap, "横置时" },
            { TriggerTiming.OnUntap, "重置时" },
            { TriggerTiming.OnDestroy, "被破坏时" },
            { TriggerTiming.OnExile, "被除外时" },
            { TriggerTiming.OnReturnFromGraveyard, "从墓地回到战场时" },
            { TriggerTiming.OnLeaveBattlefield, "离场时" },
            { TriggerTiming.OnPhaseStart, "阶段开始时" },
            { TriggerTiming.OnPhaseEnd, "阶段结束时" },
            { TriggerTiming.OnBlockDeclare, "阻拦宣言时" },
            { TriggerTiming.OnCardPlayed, "使用卡牌时" },
            { TriggerTiming.OnGameStart, "游戏开始时" },
            { TriggerTiming.OnAtomicEffectActivation, "原子效果发动时" },
            { TriggerTiming.OnAtomicEffectStartApplying, "原子效果开始作用时" },
            { TriggerTiming.OnAtomicEffectResolution, "原子效果结算完成时" },
            { TriggerTiming.OnOtherCreatureDeath, "其他生物死亡时" },
            { TriggerTiming.OnRoleDeath, "角色死亡时" },
            { TriggerTiming.OnScry, "占卜时" }, // 2026-10-29 阶段3c 信息轴：占卜结算观察（双方占卜都触发）
            { TriggerTiming.OnCardCastSettled, "效果结算后" }, // 2026-11-06 判据七：任一方施放结算·连锁终点（双方施放都观察）
        };

        /// <summary>TargetKind → 中文名（2026-10-06 单一来源迁 CardCore：TargetKindRules.ZhNameOf——
        /// 表列 TargetKinds 中文 CSV 与 UI 显示同源，防两套口径）。</summary>
        public static string TargetKindZhOf(TargetKind k)
            => TargetKindRules.ZhNameOf(k);

        /// <summary>{target} 占位的名词：单值实例域 → 中文名；null（表默认）/多值 → 「目标」。
        /// 例外（2026-10-04 关键词行修复，2026-10-07 扩指示物行）：关键词/指示物行的 kinds=[Self]
        /// 是「本体挂自己」存储态（合成器 AtomZh 两态命名、运行时自授予同源），而该行模板的
        /// {target} 是叙事名次——受击对手或字面「目标」（"不会成为效果{target}"），不随域渲染，
        /// 否则出"不会成为攻击和效果的自己"病句——回退「目标」。非授予类行（如沉睡复合域收窄到 Self）
        /// 仍显「自己」，那是真实选择。
        /// 玩家滤器特化（2026-10-13 定案，代码侧统一）：行 TargetFilter=Player（RoleOnlyFilter）且
        /// 有效域单值为 己方单位/对方单位 → 名词=「己方玩家/对方玩家」（表书写保持 {target} 统一，
        /// "对方单位玩家"病句与硬写规避都在此收敛）。实例域未声明时回落表行默认域。</summary>
        public static string TargetNoun(AtomicEffectConfig cfg, AtomicEffectEntry atom, int count = -1)
        {
            // 目标域重做 P0（2026-11-05·契约）：行 Domain 为**新选择器语法**且实例无显式域/kinds
            // → selector 短语（「双方战场的生物或双方角色」），优先于 legacy Domain 顿号 / 旧 kinds 侧词。
            // 2026-11-06 极性定案：非场地效果按极性定侧（p<0→对方/p>0→己方）——P0 短语同步收窄，
            // 文案与实际候选域一致（「对对方战场的生物或对方角色造成伤害」）。
            // 2026-11-06 或连接定案：选择档（单选/多选·count!=0）多域以「或」连接——目标是诸域
            // 并集里挑 N 个，顿号并例会读成「对每个域都造成伤害」；全部档（count=0）保持顿号。
            // 注：实例显式 kinds 条目（组合器收窄）的候选走区域收窄轴（converter），
            // 渲染保持旧侧词——文案与候选来源的统一留 P2 渲染收口。
            bool p1bEntryDomain = !string.IsNullOrEmpty(atom?.domain);
            bool p1bEntryKinds = atom != null && atom.kinds != null && atom.kinds.Count > 0;
            bool sel = SelectionOf(atom);
            if (!p1bEntryDomain && !p1bEntryKinds && !string.IsNullOrEmpty(cfg?.Domain)
                && TargetAxisRules.TryParseDomain(cfg.Domain, out var p0Axis))
                return AxisPhraseOf(TargetAxisRules.NarrowByPolarity(p0Axis, cfg?.Polarity ?? 0f), sel, count);

            // P2-2 域优先（2026-11-04）：实例显式 token（关键词授予域等）→ 表行 Domain（多 token 原样 CSV）
            // → 旧 kinds 口径。域显式即真实选择域——不受关键词 [Self] 存储态回退影响（授予卡显示所选域）。
            // 2026-11-06：实例域为新选择器语法（dd-domain 通用化写入）→ 两轴短语（AxisPhrase）渲染；
            // legacy token 多值在选择档同按「或」连末段（DomainPhrase·运行时已不解析，仅存量展示）。
            if (!string.IsNullOrEmpty(atom?.domain))
                return TargetAxisRules.TryParseDomain(atom.domain, out var entryAxis)
                    ? AxisPhraseOf(entryAxis, sel, count) : DomainPhrase(atom.domain, sel);
            // 行域新语法 + 实例 kinds 单值 → 按 converter 同口径收窄（NarrowByKinds）后两轴短语
            //（2026-11-06 收口：此前回显行 Domain 原文——"双方战场:生物;双方战场:结界; 双方角色"
            // 病句源；kinds 收窄语义与 CardEffectConverter 产轴一致）。
            // 参照域行（步4 2026-11-08）：kinds 不再收窄受体域——{target} 恒为整行 Domain 短语
            //（参照域独立，{reference} 走 RefDomain）。
            if (!string.IsNullOrEmpty(cfg?.Domain) && atom?.kinds != null && atom.kinds.Count > 0
                && TargetAxisRules.TryParseDomain(cfg.Domain, out var rowAxis))
            {
                if (!string.IsNullOrEmpty(cfg.RefDomain))
                    return AxisPhraseOf(rowAxis, sel, count);
                var narrowed = TargetAxisRules.NarrowByKinds(rowAxis, atom.kinds);
                return narrowed != null && narrowed.Selectors.Count > 0
                    ? AxisPhraseOf(narrowed, sel, count) : AxisPhraseOf(rowAxis, sel, count);
            }
            if (!string.IsNullOrEmpty(cfg?.Domain)) return DomainPhrase(cfg.Domain, sel);
            if (atom?.kinds != null && atom.kinds.Count == 1)
            {
                var m = MountKindExtensions.ParseCsv(cfg?.MountKinds ?? "");
                bool selfStorage = (TargetKind)atom.kinds[0] == TargetKind.Self
                    && (m.Contains(MountKind.Keyword) || m.Contains(MountKind.Counter));
                if (!selfStorage)
                    return PlayerNounOf(cfg, atom.kinds[0]) ?? TargetKindZhOf((TargetKind)atom.kinds[0]);
            }
            // 实例域未声明（kinds null）→ 表行默认域单值时同样参与玩家特化（跳过回合行先例）
            if (atom?.kinds == null)
            {
                var tableKinds = cfg?.GetTargetKindList();
                if (tableKinds != null && tableKinds.Count == 1)
                    return PlayerNounOf(cfg, tableKinds[0]) ?? "目标";
            }
            // 双侧同域对（2026-11-02 洗回改版）：恰成一对的同区域两侧种类 →「双方墓地」统称
            //（实例域优先；kinds 未声明回落表行默认域同判）
            var pairZh = TargetKindRules.BothSidesNoun(atom?.kinds)
                         ?? TargetKindRules.BothSidesNoun(cfg?.GetTargetKindList());
            if (pairZh != null) return pairZh;
            return "目标";
        }

        /// <summary>玩家滤器名词（null=非该口径，调用方走默认）：Filter=Player + 单值单位域
        ///（己方单位/对方单位）→ 己方玩家/对方玩家。</summary>
        private static string PlayerNounOf(AtomicEffectConfig cfg, int kind)
        {
            if (cfg == null || cfg.TargetFilter != "Player") return null;
            if (kind == (int)TargetKind.OwnLivingUnit) return "己方玩家";
            if (kind == (int)TargetKind.EnemyLivingUnit) return "对方玩家";
            return null;
        }

        /// <summary>域词表 CSV → 可读短语（legacy 渲染·S3b 收口 2026-11-05 临时映射）：token 本身已是中文
        ///（EffectDomain 词表），分隔逗号换顿号——「双方,双方角色」→「双方、双方角色」；选择档 orJoin=true
        /// 时末段换「或」（2026-11-06 或连接定案，见 JoinZh）。P1-b 起仅服务未声明两轴列的 legacy 行
        ///（Region 行走 AxisPhrase）。</summary>
        private static string DomainPhrase(string domainCsv, bool orJoin = false)
        {
            if (string.IsNullOrEmpty(domainCsv)) return domainCsv;
            return JoinZh(domainCsv.Split(',').ToList(), orJoin);
        }

        /// <summary>中文并例/择一连接（2026-11-06 或连接定案）：选择档（单选/多选）的多个目标域以
        ///「或」连接最后一段——「A、B或C」＝目标从 A/B/C 并集中挑选，顿号并例会读成「对每个域都
        /// 造成伤害」；全部档（orJoin=false）保持顿号（真对每个域都生效）。单段不受影响。</summary>
        private static string JoinZh(List<string> parts, bool orJoin)
        {
            if (parts == null || parts.Count == 0) return "";
            if (!orJoin || parts.Count == 1) return string.Join("、", parts);
            return string.Join("、", parts.Take(parts.Count - 1)) + "或" + parts[parts.Count - 1];
        }

        /// <summary>条目是否选择档（2026-11-06 或连接定案）：count!=0（-2 未声明/1/2/3）=单选/多选——
        /// 目标域短语按「或」连接；count=0 全部档对每个域全取，保持顿号并例。</summary>
        private static bool SelectionOf(AtomicEffectEntry atom)
            => atom == null || atom.count != 0;

        /// <summary>两轴短语（目标域重做 P0）：selector 列表 →「双方战场的生物、双方角色」
        ///——selector 内区域+类别顿号（卡类别=「…的卡」），selector 间顿号；类别缺省（自己/角色）=区域本身。
        /// 选择档 orJoin=true（2026-11-06 或连接定案）：selector 间与 selector 内多类别均以「或」连末段
        ///（「对方战场的生物或结界」——目标从并集挑，非逐域全打）。
        /// 量词定案（2026-11-07）：量词只标在**类别前**——生物/结界「N个」、法术/卡「N张」、
        /// 全部档「全部」（同位）；域（区域侧别词）前与角色 selector 恒无量词；手牌/牌库域
        /// =纯区域词（类别恒卡，模板自带「张牌/的卡」语境——「对方牌库顶的2张牌」/「对方手牌中的卡」）。
        /// count=-1（调用方未传，战报等旧口径）=无量词原短语。</summary>
        private static string AxisPhraseOf(TargetAxis axis, bool orJoin = false, int count = -1)
        {
            var parts = new List<string>();
            foreach (var s in axis.Selectors)
            {
                var region = TargetRegionRules.ZhNameOf(s.Region);
                if (s.Region == TargetRegion.Self || TargetRegionRules.IsCharacter(s.Region))
                { parts.Add(region); continue; }             // 自己 / X方角色（无类别段·无量词）
                var zone = TargetRegionRules.ZoneOf(s.Region);
                if (zone == Zone.Hand || zone == Zone.Deck)
                { parts.Add(region); continue; }             // 手牌/牌库：纯区域词（量词与「的卡」由模板语境承载）
                string cats = JoinZh(TargetCategoryRules.ZhOf(s.Categories).Split(',').ToList(), orJoin);
                string quant = count < 0 ? "" : count == 0 ? "全部" : count + MeasureWordOf(s.Categories);
                parts.Add(region + "的" + quant + cats);
            }
            return JoinZh(parts, orJoin);
        }

        /// <summary>类别量词（2026-11-07 定案）：生物/结界=「个」、法术/卡=「张」
        ///（卡位=区域内全选标志——出现即「张」；生物系位优先于法术位）。</summary>
        private static string MeasureWordOf(TargetCategory categories)
            => (categories & (TargetCategory.Creature | TargetCategory.Enchantment)) != 0
               && (categories & TargetCategory.Card) == 0 ? "个" : "张";

        /// <summary>条目有效轴（展示口径，2026-11-06 极性定案）：实例 domain（新语法）优先 →
        /// 行 Domain；kinds 显式 → NarrowByKinds（实例收窄/代价锁侧）；否则极性收窄
        ///（NarrowByPolarity——非场地效果按极性定侧，与 converter 产轴同口径）。
        /// 参照域行（步4 2026-11-08）：kinds 不承载受体域语义——恒极性收窄整行 Domain
        ///（参照域独立走 {reference}→RefDomain，不再有「kinds 收窄空挂 null」形态）。</summary>
        private static TargetAxis EffectiveAxisOf(AtomicEffectConfig cfg, AtomicEffectEntry atom)
        {
            TargetAxis axis = null;
            if (!string.IsNullOrEmpty(atom?.domain) && TargetAxisRules.TryParseDomain(atom.domain, out var domAxis))
                axis = domAxis;
            else if (!string.IsNullOrEmpty(cfg?.Domain) && TargetAxisRules.TryParseDomain(cfg.Domain, out var rowAxis))
                axis = rowAxis;
            if (axis == null) return null;
            if (!string.IsNullOrEmpty(cfg?.RefDomain))
                return TargetAxisRules.NarrowByPolarity(axis, cfg?.Polarity ?? 0f);
            if (atom?.kinds != null && atom.kinds.Count > 0)
            {
                var narrowed = TargetAxisRules.NarrowByKinds(axis, atom.kinds);
                return narrowed != null && narrowed.Selectors.Count > 0 ? narrowed : null;
            }
            return TargetAxisRules.NarrowByPolarity(axis, cfg?.Polarity ?? 0f);
        }

        /// <summary>全取面积轴（全部档名词用）：有效轴剔 自己/角色 selector（角色默认不参与
        /// 全取——与 ResolveAtomTargetsAsync 全取路径同口径）；无面积 selector 返回 null。</summary>
        private static TargetAxis AreaAxisOf(AtomicEffectConfig cfg, AtomicEffectEntry atom)
        {
            var eff = EffectiveAxisOf(cfg, atom);
            if (eff == null) return null;
            var area = new TargetAxis { Filter = eff.Filter };
            foreach (var s in eff.Selectors)
                if (s != null && s.Region != TargetRegion.Self && !TargetRegionRules.IsCharacter(s.Region))
                    area.Selectors.Add(s);
            return area.Selectors.Count > 0 ? area : null;
        }

        /// <summary>效果级作用范围版（2026-10-04 相同目标定案）：header.TargetKinds 单值优先——
        /// 并列全体原子共享同一作用范围，{target} 按效果级域渲染（关键词/指示物行 [Self] 存储态例外同口径）；
        /// 未声明/多值回落实例域口径。</summary>
        public static string TargetNoun(AtomicEffectConfig cfg, AtomicEffectEntry atom, CardEffectData header,
            int count = -1)
        {
            if (!string.IsNullOrEmpty(atom?.domain)) // 实例域最高优先（2026-11-06：新语法按两轴短语渲染；
                // 选择档按「或」连接多域——同 TargetNoun 两参重载口径）
                return TargetAxisRules.TryParseDomain(atom.domain, out var entryAxis)
                    ? AxisPhraseOf(entryAxis, SelectionOf(atom), count) : atom.domain;
            if (header?.TargetKinds != null && header.TargetKinds.Count == 1)
            {
                var m = MountKindExtensions.ParseCsv(cfg?.MountKinds ?? "");
                // 关键词（2026-11-07 特判）：效果层关键词=赋予己方战场生物——header 旧 [Self]
                // 存储态不再回退「自己/目标」，改走行域名词（己方战场的…生物）。
                if (!m.Contains(MountKind.Keyword))
                {
                    bool selfStorage = (TargetKind)header.TargetKinds[0] == TargetKind.Self
                        && m.Contains(MountKind.Counter);
                    return selfStorage ? "目标"
                        : PlayerNounOf(cfg, header.TargetKinds[0]) ?? TargetKindZhOf((TargetKind)header.TargetKinds[0]);
                }
            }
            return TargetNoun(cfg, atom, count);
        }

        /// <summary>渲染单原子描述。cfg 为空（无表行 fallback 原子）时回退 refId。</summary>
        public static string Render(AtomicEffectConfig cfg, AtomicEffectEntry atom, CardEffectData header)
            => RenderCore(cfg, atom, header, false);

        /// <summary>奖励原子渲染：Then 奖励结算在合法范围重新弹选目标
        ///（ExecuteThenRewardsAsync→ResolveRewardTargetsAsync），不沿用主干目标。
        /// 2026-10-20 用户定案：名次**不再冠「新的」**（冗余——【奖励】段已区分）。</summary>
        public static string RenderReward(AtomicEffectConfig cfg, AtomicEffectEntry atom)
            => RenderCore(cfg, atom, null, true);

        private static string RenderCore(AtomicEffectConfig cfg, AtomicEffectEntry atom, CardEffectData header,
            bool newTargetNoun)
        {
            if (atom == null || string.IsNullOrEmpty(atom.refId)) return "原子效果";
            string tpl = cfg != null && !string.IsNullOrEmpty(cfg.Description) ? cfg.Description : atom.refId;
            // 2026-11-10 amplification：{value} 处标注死亡计数增幅（不把层数烘进基础值）。
            if (atom.branch != null
                && (BranchSettleKind)atom.branch.settle == BranchSettleKind.Engine
                && (BranchEngineKind)atom.branch.engine == BranchEngineKind.DeathToll
                && atom.branch.engineParam >= 9)
                tpl = tpl.Replace("{value}", "{value}（+死亡计数增幅）");

            int v = atom.value;
            int span = atom.amp > 0f ? TideMath.RoundToInt(Math.Abs(v) * atom.amp) : 0;
            string number = span > 0 ? $"{Math.Max(0, v - span)}至{v + span}" : v.ToString();
            // {target} → 实例域名次（2026-09-22 五轮：单值域显作用对象；表默认/多值保持「目标」；
            // 2026-10-04 补：关键词行 [Self] 存储态回退「目标」，见 TargetNoun；
            // 2026-10-04 相同目标定案：header 效果级作用范围单值优先；
            // 2026-10-20 定案：奖励名次不再冠「新的」——【奖励】段已区分，冗余词删除）
            // 量词定案（2026-11-07）：整体名词前缀「N个/全部」退役——量词只标在**类别前**
            //（AxisPhraseOf 内嵌：生物/结界「N个」/法术·卡「N张」/全部档「全部」），域（区域侧别词）
            // 前与角色恒无量词；手牌/牌库域=纯区域词（模板自带「张牌/的卡」语境——"将对方牌库顶的
            // 2张牌送入墓地"）。非轴回退名词（「目标」）保旧口径「N个目标」；玩家名词（角色类）不冠词。
            var cfgKinds = cfg?.GetTargetKindList();
            // 有效数量（2026-11-06 数量词全局显式定案）：-2（未声明）→ header.TargetCount 回落
            // → 仍 -2 归一 1（converter FallbackTargetCount 恒 1 同口径——代价/奖励原子 header=null
            // 走此归一）；-1（任意数量）不冠词。
            int effCount = atom.count;
            if (effCount == -2)
            {
                int hc = header != null ? header.TargetCount : -2;
                effCount = hc == 0 || (hc >= 1 && hc <= 3) ? hc : 1;
            }
            bool quantified = effCount >= 1 && effCount <= 3;
            string noun;
            if (atom.count == 0)
            {
                // 全部档=域内全取：名词=面积轴短语（角色/自己不参与全取——AreaAxisOf 同口径），
                // 「全部」同标在类别前；无面积 selector（角色专属域）保持原名词（角色无量词）。
                var area = AreaAxisOf(cfg, atom);
                noun = area != null ? AxisPhraseOf(area, false, 0) : TargetNoun(cfg, atom, header);
            }
            else
            {
                noun = TargetNoun(cfg, atom, header, quantified ? effCount : -1);
                if (noun == "目标" && (cfgKinds?.Count ?? 0) > 0 && quantified)
                    noun = effCount + "个目标"; // 回退名词非域非类别——保旧口径（2026-11-06「2个目标」）
            }
            // 费用增减指示物（2026-11-07 定案）：{target} 解析为「x张 / 全部 单侧手牌」——
            // 域=极性解析后的单一手牌，全域=该手牌下全部卡（表模板「为{target}中的卡添加…」）。
            if (Enum.TryParse<AtomicEffectType>(cfg?.EnumName ?? "", out var costType)
                && TargetAxisRules.IsCostIndicator(costType) && noun.Length > 0)
            {
                if (effCount == 0) noun = "全部" + noun;
                else if (quantified) noun = effCount + "张" + noun;
            }
            string body = tpl.Replace("{value}", number).Replace("{target}", noun);
            // {reference} → 参照域短语（步4 §2.1：参照域独立于 Domain；存量模板不含此占位符——
            // 本口为零影响扩展，新模板可写「以{reference}为参照」）。
            if (body.Contains("{reference}"))
            {
                string refNoun = "参照";
                if (!string.IsNullOrEmpty(cfg?.RefDomain)
                    && TargetAxisRules.TryParseDomain(cfg.RefDomain, out var refAxis))
                    refNoun = AxisPhraseOf(refAxis, true, -1);
                body = body.Replace("{reference}", refNoun);
            }
            // {衍生物} → 模板卡名（2026-10-09 衍生物召唤配套）：SummonToken 的 str 指向真实生物卡——
            // 未填/未解析显「未指定模板」/原样 ID；表行括注「{衍生物}=字符串参数指向…」整段折叠为卡名
            if (body.Contains("{衍生物}"))
            {
                var tplCard = CardCatalog.GetById(atom.str);
                string tokenName = tplCard != null ? tplCard.CardName
                    : string.IsNullOrEmpty(atom.str) ? "未指定模板" : atom.str;
                body = body.Replace("{衍生物}=字符串参数指向一张真实生物卡，实例=该卡全参数复制", tokenName)
                           .Replace("{衍生物}", tokenName);
            }
            // 数值随机（RandomAmplitude&gt;0）：区间文本「x至x」已表达随机语义（2026-10-09 句式定案）——不再冠「随机」前缀
            // 目标随机（2026-10-09 下沉每原子+句式定案）：条目声明优先（1=随机抽/0=弹窗），
            // 未声明回落效果级 RandomTarget；奖励渲染（header=null）=条目声明直接生效。
            // 极性收缩侧词已删（2026-11-02 用户定案：取消并列效果收缩作用域）——运行时候选域
            // 从不做极性侧别过滤（逐原子目标制「禁极性过滤」定案，见 EffectHandlerRegistry.ResolveCandidates
            // 头注），双侧域原子两侧可选；文案若按极性冠「己方/对方」会窄于实际可选域，误导目标选择。
            bool rand = atom.rand != -1 ? atom.rand == 1
                : (header != null && header.RandomTarget != 0);
            if (rand && atom.count != 0) // 全部档=域内全取无抽取语境——随机前缀不冠（UI 已强制关，防御）
            {
                int idx = body.IndexOf("目标", StringComparison.Ordinal);
                if (idx < 0 && noun.Length > 0) idx = body.IndexOf(noun, StringComparison.Ordinal);
                if (idx >= 0) body = body.Insert(idx, "随机");
            }
            // 特殊列（2026-10-31 阶段B）：文案没说、但引擎实际这么做的规则——括注追加在描述后。
            // 纯展示层（本渲染口=合成器效果描述/卡面 tooltip/挂载预览共用），不动模板与行 ID。
            if (!string.IsNullOrEmpty(cfg?.Special))
                body = body + "（" + cfg.Special.TrimEnd('。') + "）";
            return body;
        }

        // 双侧域极性收缩侧词（原 DualSideScopeWord）已随 2026-11-02「取消并列效果收缩作用域」定案整删：
        // 运行时候选域不做极性侧别过滤，文案不再按极性冠「己方/对方」——双侧域名次（BothSidesNoun
        // 「双方墓地」等）与实例收窄域名次照常自显作用面。


        /// <summary>按引用取表行渲染（便捷重载——refId 直查）。</summary>
        public static string Render(AtomicEffectEntry atom, CardEffectData header)
            => Render(AtomicEffectTable.GetByHashId(atom?.refId), atom, header);

        /// <summary>效果整体预览（两槽定案）：并列主序列=逐原子分号连接，原子带槽级 branch 载荷时
        /// 追加分支后缀（产出条件/局面门=「，如果…，奖励」自然句；引擎=正文短式「自由分支·{名}{x}」+「→奖励」）；
        /// 遗留 kind=1 门步骤与抉择步骤照旧渲染。
        /// 开头时机前缀（2026-10-09 定案；2026-11-06 速度显示改版：主动效果无前缀——速度不再属于
        /// 效果文字，归卡面 effect 行 lbl-skill-speed 独显（CardOverlayItem.EffectSpeed 按卡类别：
        /// 生物0/结界无/法术底盘分配））：非主动=「{时机}，」（"登场时，…"）；空效果不加前缀；
        /// 时机不在中文表（异常组合）无前缀。</summary>
        public static string RenderEffectSummary(EffectGraphData graph, CardData owner = null)
        {
            if (graph?.header == null) return "";
            var h = graph.header;
            var parts = new System.Collections.Generic.List<string>();
            if (graph.steps != null)
            {
                foreach (var s in graph.steps)
                {
                    if (s == null) continue;
                    if (s.kind == 0 && s.atomic != null)
                    {
                        // 引擎主干行短式正文（2026-10-09 八引擎定案「自由分支·{名}{x}」）；非引擎行照表行模板
                        string body = EngineShortBody(s.atomic, (TriggerTiming)h.TriggerTiming) ?? RenderAtomEntry(s.atomic, h);
                        var suffix = BranchSuffix(s.atomic);
                        // 条件载体行（2026-11-01）：宿主原子行本身就是同一条局面门时，正文=条件从句本身，
                        // 直接以分支后缀成句（「如果生命值≤7，…」），不重复渲染前置条件（旧文本会出现
                        // 「生命值≤7，如果生命值≤7，…」的双写）。用于「仅当X时」型效果的主序列占位原子。
                        if (suffix.Length > 0 && IsSituationGateCarrier(s.atomic))
                            parts.Add(suffix.TrimStart('，'));
                        else
                            parts.Add(suffix.Length > 0 ? $"{body}{suffix}" : body);
                    }
                    else if (s.kind == 1)
                    {
                        // 遗留门步骤：条件中文（ComposerCatalog 同源——产出条件 ∪ 局面门）；未知条件回退原 id
                        var spec = ComposerCatalog.OutcomeConditions.FirstOrDefault(g => g.Id == s.conditionId)
                                   ?? ComposerCatalog.SituationGates.FirstOrDefault(g => g.Id == s.conditionId);
                        string gate = spec != null ? ComposerCatalog.GateLabel(spec)
                            : (string.IsNullOrEmpty(s.conditionId) ? "?" : s.conditionId);
                        string reward = s.thenSteps != null && s.thenSteps.Count > 0
                            ? RenderRewardAtomEntry(s.thenSteps[0]) : "（未设奖励）";
                        parts.Add($"[{gate}]→{reward}");
                    }
                    else if (s.kind == 2) parts.Add($"抉择（{s.choices?.Count ?? 0} 模式）");
                }
            }
            string joined = string.Join("；", parts);
            if (joined.Length == 0) return "";
            // 代价后缀（2026-10-31 代价指向化定案：代价描述文本附加进文字表述）：
            // 效果级代价行=指向本效果的代价——正文追加「，【代价】…」；行内多原子以「＋」连接
            //（与合成器代价文案同口径）；原子按自身域渲染（代价独立执行，不随主干目标）。
            // 2026-11-06：标记与引擎分支【奖励】同式【】——有限分支写法统一。
            if (h.Costs != null)
            {
                var costParts = new List<string>();
                foreach (var ce in h.Costs)
                {
                    if (ce == null) continue;
                    foreach (var pa in ce.AllPayloads())
                        if (pa != null && !string.IsNullOrEmpty(pa.refId))
                            costParts.Add(RenderCostAtomEntry(pa));
                }
                if (costParts.Count > 0)
                    joined = joined + "，【代价】" + string.Join("＋", costParts);
            }
            // 引擎主干卡（自由分支·2026-10-20 用户定案）：不加时机/发动前缀——引擎身份+参数即正文首
            //（"自由分支·倒计时2，【奖励】…"）；常规效果照旧前缀。
            bool hasEngineTrunk = false;
            if (graph.steps != null)
                foreach (var s in graph.steps)
                    if (s?.kind == 0 && s.atomic?.branch != null
                        && (BranchSettleKind)s.atomic.branch.settle == BranchSettleKind.Engine)
                    { hasEngineTrunk = true; break; }
            return hasEngineTrunk ? joined : ActivationPrefix(h, owner) + joined;
        }

        // 卡级代价行文字口（CardCostLines/CardCostSuffixText）已随 2026-11-06「代价全面并入效果级」
        // 定案退役——效果级 Costs 走 RenderEffectSummary「，【代价】…」句内标记，卡级 payloads 遗留
        // 数据仅存 EffectComposer 代价门编辑兼容口（SynthesizeCostGateView，自然空转）。

        /// <summary>效果描述开头前缀（2026-11-06 速度显示改版：主动效果不再带速度描述——速度归卡
        /// 类别，由卡面 effect 行 lbl-skill-speed 独显（CardOverlayItem.EffectSpeed），主动前缀退役为空。
        /// 旧「发动速度x，」句首=BaseSpeed+KindSpeedBonusOf 口径已随本改版整删）；
        /// 非主动（强制/自动）=「{时机中文}，」——时机查 TimingZh 全量表（含系统自用时点）。</summary>
        private static string ActivationPrefix(CardEffectData h, CardData owner)
        {
            if (h.ActivationType == (int)EffectActivationType.Voluntary)
            {
                //（P12 充能 0 速前缀分支已随 2026-11-05 充能系统动作化改版删除——充能不再是卡上效果。）
                //（主动速度前缀 2026-11-06 删除——速度不再属于效果文字，见 summary。）
                return "";
            }
            return TimingZh.TryGetValue((TriggerTiming)h.TriggerTiming, out var zh) ? zh + "，" : "";
        }

        /// <summary>槽级 branch 载荷的后缀文本（两槽定案）：无载荷返回空串。
        /// 产出条件（Outcome）与局面门（Gate，2026-10-09 还原）=自然句式「，如果{从句}，{奖励}」
        /// （2026-10-05 文本表述定案；局面门不达标无事——纯条件奖励，无逆转句）；
        /// 引擎（2026-10-09 短式化）=「→{奖励}」——引擎身份+参数在正文「自由分支·{名}{x}」（EngineShortBody）。</summary>
        public static string BranchSuffix(AtomicEffectEntry atom)
        {
            var b = atom?.branch;
            if (BranchEntryRules.IsPhantom(b)) return ""; // 含 null；JsonUtility 幽灵 settle 不当真分支
            if ((BranchSettleKind)b.settle == BranchSettleKind.Engine
                && (BranchEngineKind)b.engine == BranchEngineKind.DeathToll)
                return ""; // amplification 标记不是自由分支奖励
            string reward = b.then != null && b.then.Count > 0
                ? RenderRewardAtomEntry(b.then[0]) : "（未设奖励）";
            switch ((BranchSettleKind)b.settle)
            {
                case BranchSettleKind.Engine:
                    // 引擎身份+参数已在正文短式（自由分支·{名}{x}）——后缀只接奖励。
                    // 2026-10-20 用户定案格式：「，【奖励】{奖励}」；声明主动的奖励附使用档「(本回合0/1)」
                    //（一次/待发周期）；自动/未声明不附。倒计时固定触发（同日定案）恒不附——主干/奖励不拆开。
                    // 主动运势改式（同日修订）：「，则【{奖励}(本回合0/1)】」使用档入方括号——
                    // 与正文「点击掷两个骰子，运势一」成句；其余引擎照定案格式。
                    if (b.engine == (int)BranchEngineKind.LuckRoll && b.tactivate == 2)
                        return $"，则【{reward}(本回合0/1)】";
                    return b.tactivate == 2 && (BranchEngineKind)b.engine != BranchEngineKind.Countdown
                        ? $"，【奖励】{reward}(本回合0/1)"
                        : $"，【奖励】{reward}";
                case BranchSettleKind.Gate:
                    // 代价门（2026-11-06 展示期合成——不落盘）：句式「，【代价】{原子}」——
                    // 与引擎分支【奖励】同式【】标记（2026-11-06 定案：有限分支写法统一；
                    // 减免读数不随文案标出——费用/代价信息归 CostSquares 双组）。
                    if (b.gateId == ComposerCatalog.CostGateId)
                    {
                        string costAtom = b.then != null && b.then.Count > 0
                            ? RenderCostAtomEntry(b.then[0]) : "（未设代价）";
                        string more = b.then != null && b.then.Count > 1 ? $" 等{b.then.Count}个原子" : "";
                        return $"，【代价】{costAtom}{more}";
                    }
                    // 局面门（有限分支）：DisplayName 即自然从句（如"生命值低于对手"）
                    var sc = ComposerCatalog.SituationGates.FirstOrDefault(g => g.Id == b.gateId);
                    string scond = sc != null ? sc.DisplayName
                        : (string.IsNullOrEmpty(b.gateId) ? "？" : b.gateId);
                    return $"，如果{scond}，{reward}";
                case BranchSettleKind.Outcome:
                    var oc = ComposerCatalog.OutcomeConditions.FirstOrDefault(g => g.Id == b.outcomeId);
                    string clause;
                    if (oc != null)
                        clause = string.IsNullOrEmpty(oc.IfClause) ? oc.DisplayName.TrimEnd('时') : oc.IfClause;
                    else
                        clause = string.IsNullOrEmpty(b.outcomeId) ? "？" : b.outcomeId;
                    return $"，如果{clause}，{reward}";
                default:
                    return ""; // 越界 settle——IsPhantom 已滤，防御兜底
            }
        }

        /// <summary>条件载体行判定（2026-11-01）：「仅当X时…」型效果的主序列占位原子——宿主原子行
        /// 本身就是局面门行（MountKinds 含「有限分支」位），且槽级 gateId 与行 GateId 同一条。
        /// 该形态下正文=条件从句（BranchSuffix 会整句重写），不与后缀重复。</summary>
        private static bool IsSituationGateCarrier(AtomicEffectEntry atom)
        {
            var branch = atom?.branch;
            if (branch == null || (BranchSettleKind)branch.settle != BranchSettleKind.Gate) return false;
            var row = AtomicEffectTable.GetByHashId(atom.refId);
            return row != null && !string.IsNullOrEmpty(row.GateId) && row.GateId == branch.gateId;
        }

        /// <summary>条目级渲染（refId → 表行 → Render；行缺失回退 refId）。词条步（@）走投影分支。</summary>
        public static string RenderAtomEntry(AtomicEffectEntry atom)
        {
            if (atom == null || string.IsNullOrEmpty(atom.refId)) return "原子";
            var composite = RenderCompositeEntry(atom);
            if (composite != null) return composite;
            return Render(AtomicEffectTable.GetByHashId(atom.refId), atom, null);
        }

        /// <summary>代价原子渲染（2026-11-06 手写代价批修复）：资敌代价（SideLock=+1 纯锁对方侧）
        /// 由受惠侧执行——执行控制器翻转为对手（CostSystem.ExecutePayloadAtomAsync 同口径），
        /// 文案必须显主语，与战报 DrawCardHandler.DescribeTemplate「对手抽 N 张牌」同源。
        /// 表行模板含 {target} 时名词已带侧别（「对方牌库」）不冠；模板无 {target}（如「抽{value}张卡」）
        /// 时裸文读作自己——冠「对手」。己方侧代价不冠（卡面默认主语=自己）。</summary>
        public static string RenderCostAtomEntry(AtomicEffectEntry atom)
        {
            var text = RenderAtomEntry(atom);
            var cfg = AtomicEffectTable.GetByHashId(atom?.refId);
            bool tplHasTarget = cfg != null && (cfg.Description ?? "").Contains("{target}");
            if (!tplHasTarget && CostDerivationService.SideLock(atom?.kinds) == 1)
                text = "对手" + text;
            return text;
        }

        /// <summary>条目级渲染·效果级作用范围版（2026-10-04 相同目标定案）：并列主序列原子传
        /// header——{target} 按效果级域渲染；奖励原子目标各自解析，不传 header（用单参重载）。
        /// 词条引用步（@ 前缀·G5）：「【词条·名】参数=N：展开体逐原子文案」——展开体走既有
        /// RenderAtomEntry 链（handler 描述同源聚合，不改结构）。</summary>
        public static string RenderAtomEntry(AtomicEffectEntry atom, CardEffectData header)
        {
            if (atom == null || string.IsNullOrEmpty(atom.refId)) return "原子";
            var composite = RenderCompositeEntry(atom);
            if (composite != null) return composite;
            return Render(AtomicEffectTable.GetByHashId(atom.refId), atom, header);
        }

        /// <summary>词条引用步渲染（G5 投影）：定义可解析 → 词条名+参数绑定值+展开体逐原子文案；
        /// 非 词条步/定义缺失 → null（回落常规渲染——缺表词条的卡在 G4 已整卡剔除，此处只见旁路）。</summary>
        private static string RenderCompositeEntry(AtomicEffectEntry atom)
        {
            if (atom.refId == null || !atom.refId.StartsWith("@", StringComparison.Ordinal)) return null;
            var def = CardCore.CompositeAtomTable.Get(atom.refId);
            if (def == null) return null;
            var entries = CardCore.CompositeExpander.BindBody(def, atom);
            string body = entries == null
                ? "（展开失败——见 Console）"
                : string.Join("；", entries.Select(e => RenderAtomEntry(e)));
            string param = string.IsNullOrEmpty(atom.str) ? $"参数={atom.value}" : $"参数={atom.value}·{atom.str}";
            return $"【词条·{def.name}】{param}：{body}";
        }

        /// <summary>条目级奖励渲染（2026-10-08「新的目标」定案）：分支 Then 奖励统一走此口径——
        /// 产出条件/引擎/遗留门步骤的奖励文案与运行时弹选语义对齐（合法范围另选目标）。</summary>
        public static string RenderRewardAtomEntry(AtomicEffectEntry atom)
        {
            if (atom == null || string.IsNullOrEmpty(atom.refId)) return "原子";
            return RenderReward(AtomicEffectTable.GetByHashId(atom.refId), atom);
        }

        /// <summary>引擎主干行短式正文（2026-10-09 八引擎统一定案）：「自由分支·{中文名}{x}」——
        /// 派生门槛引擎（拼点/死亡计数/元素充盈，自平衡统一）x=奖励锚价推导门槛、倒计时声明 0 时
        /// x=按奖励费换算回合（均随 Then 奖励实时变，公式经 CostDerivationService 与运行时判定同源）；
        /// 其余引擎（运势/团结/附加诅咒/祝福）x=engineParam（EngineParamRange 钳制显示）。
        /// 非引擎行/引擎身份未定义返回 null（调用方回退表行长模板——原子库列表行长文案保留完整玩法说明）。</summary>
        /// <summary>引擎短文案公开口（验证器锚定用；合成器摘要走 Render 主路径消费同函数）。</summary>
        public static string EngineShortBodyFor(AtomicEffectEntry atom,
            TriggerTiming timing = TriggerTiming.Activate_Active) => EngineShortBody(atom, timing);

        private static string EngineShortBody(AtomicEffectEntry atom, TriggerTiming timing)
        {
            var cfg = AtomicEffectTable.GetByHashId(atom.refId);
            if (!ComposerCatalog.IsEngineTrunkRow(cfg)) return null;
            var kind = BranchEngineKind.None;
            if (atom.branch != null && Enum.IsDefined(typeof(BranchEngineKind), atom.branch.engine))
                kind = (BranchEngineKind)atom.branch.engine;
            if (kind == BranchEngineKind.None
                && Enum.TryParse<AtomicEffectType>(cfg.EnumName, out var t))
                kind = ComposerCatalog.EngineKindOf(t);
            if (kind == BranchEngineKind.None) return null;

            var then = atom.branch?.then;
            var fxId = atom.branch?.effectId; // 奖励效果引用（2026-11-07）：门槛/换算并入引用效果费——与运行时判定同源
            int x;
            switch (kind)
            {
                // 派生门槛引擎（IsDerivedThresholdEngine 单源：拼点/死亡计数/元素充盈）
                // ——2026-10-09 自平衡统一：x=Then 奖励锚价推导，随奖励计费实时变
                case BranchEngineKind.DeathToll:
                    // 2026-11-10 改版：只在手牌累计双方生物死亡（满6）；进战场冻结，离场清零。
                    return "手牌累计双方生物死亡（满6/增幅9）；此卡 {value} 增幅（1死亡=1费，离场清零）";
                case BranchEngineKind.ManaSurplus:
                    // 2026-11-13：条件开放为「≥ 门槛」——完整句同步（旧「恰==x」退役）
                    return $"任一方使用卡牌、整条连锁结算完成后，双方剩余元素的差达到或超过{CostDerivationService.EngineRewardThreshold(ToInstances(then), timing)}时触发奖励（仅自动，差额封顶6）";
                case BranchEngineKind.Clash:
                    x = CostDerivationService.EngineRewardThreshold(ToInstances(then), timing);
                    break;
                case BranchEngineKind.Countdown:
                    x = CostDerivationService.CountdownTurnsOf(atom.branch?.engineParam ?? 0, ToInstances(then), fxId, timing);
                    break;
                default: // 运势点数线/团结邻位数/附加诅咒·祝福张数——手填参数（EngineParamRange 钳制显示）
                    ComposerCatalog.EngineParamRange(kind, out var mn, out var mx);
                    x = Math.Clamp(atom.branch?.engineParam ?? mn, mn, mx);
                    break;
            }
            // 主动运势短式（2026-10-20 修订）：奖励声明主动（tactivate=2）→「点击掷两个骰子，运势{中文}」
            //（教学二新流程——掷骰随发动键走）；自动/未声明照旧「自由分支·{名}{x}」。
            if (kind == BranchEngineKind.LuckRoll && atom.branch?.tactivate == 2)
                return $"点击掷两个骰子，运势{ZhDigit(x)}";
            return $"自由分支·{ComposerCatalog.EngineZhOf(kind)}{x}";
        }

        /// <summary>1-6 中文数字（主动运势短文案「运势一」用；越界回落阿拉伯数字）。</summary>
        private static string ZhDigit(int n) => n >= 1 && n <= 6 ? "一二三四五六"[n - 1].ToString() : n.ToString();

        /// <summary>UI 奖励原子 → 运行时实例（与 EffectComposerScreen.RewardCost 同口径：
        /// ConvertAtomForUI 逐原子转换，行缺失/转换失败剔除）。</summary>
        private static List<AtomicEffectInstance> ToInstances(List<AtomicEffectEntry> then)
            => then?.Select(CardEffectConverter.ConvertAtomForUI).Where(i => i != null).ToList()
               ?? new List<AtomicEffectInstance>();
    }
}
