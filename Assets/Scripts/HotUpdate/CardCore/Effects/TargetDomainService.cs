using System.Collections.Generic;
using System.Linq;
using CardCore.Attribute;

namespace CardCore
{
    /// <summary>
    /// 组合目标域服务（2026-09-10 目标域模型）：
    /// 运行时「候选空 → 不可发动」的引擎侧预检（PlayCard / CanActivate 两链路）。
    /// 域来自 converter 预计算（def.TargetDomain / ChoiceDomains）；
    /// 候选解析复用 EffectHandlerRegistry.ResolveCandidates（同结算口径）。
    /// 注：SimpleAI / BattleScreen / LegalActionEnumerator 的同口径接线属 AI/UI 后续任务。
    /// 2026-11-02 多目标定案：可施放口径从「每个效果/原子都要有候选」放宽为
    /// 「至少 1 个目标即可施放，候选=0（全部目标要求落空）才拦」——目标不足按实有数结算
    ///（ResolveAtomTargetsAsync MinCount 收口），不再整卡拦死。
    /// </summary>
    public static class TargetDomainService
    {
        /// <summary>目标探针三态：NoRequirement=无可见域目标要求（无目标/全取/隐藏区原子）；
        /// Available=至少 1 个候选可用；Empty=有目标要求但候选全空。</summary>
        private enum TargetsProbe
        {
            NoRequirement,
            Available,
            Empty,
        }

        /// <summary>卡以 modeIndex 模式打出时是否可施放（2026-11-02 定案：至少 1 个目标即可；
        /// 全部目标要求落空才拦）。法术（2026-10-15 深夜改版）：只挂 1~3 个主动效果、
        /// 施放时抉择其一——modeIndex=效果序号，只检所选那条的域（其余效果本局不发动，不该拦出牌）。</summary>
        public static bool HasPlayableTargets(GameCore core, Player player, Card card, int modeIndex = 0)
        {
            if (core == null || player == null || card == null) return true;
            var data = card is CardWrapper wrapper ? wrapper.GetData() : null;
            if (data?.Effects == null || data.Effects.Count == 0) return true;

            var ctx = new EffectExecutionContext
            {
                Source = card.IsSpellCard() ? (Entity)player : card,
                Controller = player,
                ZoneManager = core.ZoneManager,
                ElementPool = core.ElementPool,
                ModeIndex = modeIndex,
            };

            // 登场时序定案（卡先入场→登场效果后发动）：非法术卡的全部效果都在它入场之后结算——
            // 声明期预检须把「即将入场的这张卡」视作己侧单位域候选（空场首发自指合法，
            // 如「登场时获得潜行」选自己；场上已有候选时本兜底自然不介入）。
            Card prospective = card.IsSpellCard() ? null : card;

            // 法术：只检施放时抉择的那条效果（modeIndex=效果序号；效果内逐原子聚合）
            if (card.IsSpellCard())
            {
                var idx = System.Math.Clamp(modeIndex, 0, data.Effects.Count - 1);
                var spellDef = CardEffectConverter.ConvertOne(data.Effects[idx], data.ID);
                return HasCandidates(spellDef, ctx, null);
            }

            // 跨效果聚合（2026-11-02）：任一效果有可用目标 → 可施放（其余落空效果按实有数=0 结算）；
            // 有目标要求的效果全部落空（且无任何可用目标）→ 拦；全无目标要求 → 恒可施放。
            bool anyRequires = false, anyAvailable = false;
            foreach (var effectData in data.Effects)
            {
                if (effectData == null) continue;
                var def = CardEffectConverter.ConvertOne(effectData, data.ID);
                switch (ProbeTargets(def, ctx, prospective))
                {
                    case TargetsProbe.Available: anyAvailable = true; break;
                    case TargetsProbe.Empty: anyRequires = true; break;
                }
            }
            return !anyRequires || anyAvailable;
        }

        /// <summary>主动能力（def 已转换）：组合域候选非空预检——CanActivate 第 5 步。
        /// 2026-11-02 多目标定案：效果内逐原子聚合（至少 1 个候选即通过，全落空才 false）。</summary>
        public static bool HasCandidates(EffectDefinition def, EffectExecutionContext ctx, Card prospective = null)
            => ProbeTargets(def, ctx, prospective) != TargetsProbe.Empty;

        /// <summary>效果目标探针（HasPlayableTargets/HasCandidates 共用核心）：
        /// 逐原子目标制（2026-10-09）=每个有可见域的主序列原子独立探候选，任一非空=Available；
        /// 共享域（header 声明）=组合域单次探候选。全取档（IsTakeAll/数量 0=全部）与
        /// 隐藏区/无域原子=无目标要求（NoRequirement）——空域全取照常发动（取 0 个）。</summary>
        private static TargetsProbe ProbeTargets(EffectDefinition def, EffectExecutionContext ctx, Card prospective)
        {
            if (def == null) return TargetsProbe.NoRequirement;

            // 逐原子目标制（2026-10-09）：域=各原子极性过滤域；选择层过滤按档位
            //（全取=全域、随机=帷幕、选档=两道）。
            if (def.PerAtomTargets)
            {
                IEnumerable<AtomicEffectInstance> atoms =
                    def.Steps != null && def.Steps.Count > 0
                        ? CardEffectConverter.EnumerateMainSequenceAtoms(def.Steps, ctx.ModeIndex)
                        : def.Effects ?? new List<AtomicEffectInstance>();
                bool anyRequires = false, anyAvailable = false;
                foreach (var atom in atoms)
                {
                    if (atom == null) continue;
                    var kinds = atom.TargetKinds;
                    if (kinds == null || kinds.Count == 0) continue;
                    if (TargetKindRules.AllHiddenZone(kinds)) continue; // 隐藏信息不进选择——handler 自裁
                    if (SelectionModeRules.IsTakeAll(def.SelectionMode) || atom.EffectiveTargetCount(def) == 0)
                        continue; // 全取档用全域判定——空域全取不拦（取 0 个照常结算）
                    var atomCands = EffectHandlerRegistry.ResolveCandidates(kinds, atom.Filter ?? "", ctx, atom.Axis);
                    bool atomEdict = TargetResolver.IsEdict(atom.Type);
                    atomCands = atom.EffectiveRandomTarget(def)
                        ? TargetResolver.ApplyTauntRestriction(atomCands, ctx, atomEdict)
                        : TargetResolver.ExcludeUnselectable(
                            TargetResolver.ApplyTauntRestriction(atomCands, ctx, atomEdict), ctx.Controller);
                    if (atomCands.Count > 0) { anyAvailable = true; continue; }
                    // 即将入场的自己兜底（声明期卡还不在战场——结算期它已入候选，此处补齐同口径）
                    if (ProspectiveCoversUnitKind(kinds, prospective)) { anyAvailable = true; continue; }
                    anyRequires = true; // 该原子落空——卡是否可施放取决于有无其它可用目标
                }
                if (anyAvailable) return TargetsProbe.Available;
                return anyRequires ? TargetsProbe.Empty : TargetsProbe.NoRequirement;
            }

            // 区域自结算类（SelectionMode=None，2026-09-11）：无候选要求——
            // 空牌库抽卡走疲劳、空库磨牌空转等由 handler 自裁，不在预检拦
            if (def.SelectionMode == SelectionMode.None) return TargetsProbe.NoRequirement;

            var domain = def.TargetDomain;
            if (def.ChoiceDomains != null && ctx.ModeIndex >= 0
                && ctx.ModeIndex < def.ChoiceDomains.Length
                && def.ChoiceDomains[ctx.ModeIndex] != null
                && def.ChoiceDomains[ctx.ModeIndex].Count > 0)
            {
                domain = def.ChoiceDomains[ctx.ModeIndex];
            }
            if (domain == null || domain.Count == 0) return TargetsProbe.NoRequirement; // 无目标效果不拦

            var candidates = EffectHandlerRegistry.ResolveCandidates(domain, def.TargetFilter, ctx);
            // 选择层口径（2026-09-13；2026-09-16 六值迁移）：选一/选多以"可选"判定——
            // 帷幕收窄（只吸引效果目标）+ 潜行隐藏（唯一候选被滤空则不可发动）；
            // RandomTarget 受帷幕收窄但潜行可命中；全取档（Whole/WholeUnion）用全域判定。
            // edict 原子（牺牲类——选择权在目标方）豁免帷幕。
            bool edictExempt = def.Effects != null
                && def.Effects.Any(a => a != null && TargetResolver.IsEdict(a.Type));
            if (SelectionModeRules.IsPickOne(def.SelectionMode) || SelectionModeRules.IsPickMany(def.SelectionMode))
            {
                candidates = def.RandomTarget
                    ? TargetResolver.ApplyTauntRestriction(candidates, ctx, edictExempt)
                    : TargetResolver.ExcludeUnselectable(
                        TargetResolver.ApplyTauntRestriction(candidates, ctx, edictExempt), ctx.Controller);
            }
            return candidates.Count > 0 || ProspectiveCoversUnitKind(domain, prospective)
                ? TargetsProbe.Available
                : TargetsProbe.Empty;
        }

        /// <summary>即将入场的卡是否覆盖该单位域：生物=己方有生命单位、结界=己方无生命单位
        ///（法术不入单位域，调用方已排除）。只做种类归属粗判——filter 细则（如 Untapped）
        /// 留给结算期实际解析，预检宁可放行不误伤合法首发。</summary>
        private static bool ProspectiveCoversUnitKind(List<int> kinds, Card prospective)
        {
            if (prospective == null || kinds == null || kinds.Count == 0) return false;
            var supertype = (prospective as CardWrapper)?.GetData()?.Supertype;
            if (supertype == Cardtype.Enchantment)
                return kinds.Contains((int)TargetKind.OwnNonLivingUnit);
            return kinds.Contains((int)TargetKind.OwnLivingUnit);
        }
    }
}
