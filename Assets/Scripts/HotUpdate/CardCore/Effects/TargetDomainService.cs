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
    /// </summary>
    public static class TargetDomainService
    {
        /// <summary>卡以 modeIndex 模式打出时，全部非无目标效果是否都有可作用候选。
        /// 法术（2026-10-15 深夜改版）：只挂 1~3 个启动式效果、施放时抉择其一——modeIndex=效果序号，
        /// 只检所选那条的域（其余效果本局不发动，不该拦出牌）。</summary>
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

            // 法术：只检施放时抉择的那条效果（modeIndex=效果序号）
            if (card.IsSpellCard())
            {
                var idx = System.Math.Clamp(modeIndex, 0, data.Effects.Count - 1);
                var spellDef = CardEffectConverter.ConvertOne(data.Effects[idx], data.ID);
                return HasCandidates(spellDef, ctx, null);
            }

            foreach (var effectData in data.Effects)
            {
                if (effectData == null) continue;
                var def = CardEffectConverter.ConvertOne(effectData, data.ID);
                if (!HasCandidates(def, ctx, prospective)) return false;
            }
            return true;
        }

        /// <summary>激活式能力（def 已转换）：组合域候选非空预检——CanActivate 第 5 步。</summary>
        public static bool HasCandidates(EffectDefinition def, EffectExecutionContext ctx, Card prospective = null)
        {
            if (def == null) return true;

            // 逐原子目标制（2026-10-09）：每个有可见域的主序列原子都须有候选（严格口径）——
            // 隐藏区/无域原子自结算豁免；域=各原子极性过滤域；选择层过滤按档位（全取=全域、随机=帷幕、选档=两道）。
            if (def.PerAtomTargets)
            {
                IEnumerable<AtomicEffectInstance> atoms =
                    def.Steps != null && def.Steps.Count > 0
                        ? CardEffectConverter.EnumerateMainSequenceAtoms(def.Steps, ctx.ModeIndex)
                        : def.Effects ?? new List<AtomicEffectInstance>();
                foreach (var atom in atoms)
                {
                    if (atom == null) continue;
                    var kinds = atom.TargetKinds;
                    if (kinds == null || kinds.Count == 0) continue;
                    if (TargetKindRules.AllHiddenZone(kinds)) continue; // 隐藏信息不进选择——handler 自裁
                    var atomCands = EffectHandlerRegistry.ResolveCandidates(kinds, atom.Filter ?? "", ctx);
                    if (SelectionModeRules.IsTakeAll(def.SelectionMode) || atom.EffectiveTargetCount(def) == 0)
                        continue; // 全取档用全域判定
                    bool atomEdict = TargetResolver.IsEdict(atom.Type);
                    atomCands = atom.EffectiveRandomTarget(def)
                        ? TargetResolver.ApplyTauntRestriction(atomCands, ctx, atomEdict)
                        : TargetResolver.ExcludeUnselectable(
                            TargetResolver.ApplyTauntRestriction(atomCands, ctx, atomEdict), ctx.Controller);
                    if (atomCands.Count == 0)
                    {
                        // 即将入场的自己兜底（声明期卡还不在战场——结算期它已入候选，此处补齐同口径）
                        if (ProspectiveCoversUnitKind(kinds, prospective)) continue;
                        return false;
                    }
                }
                return true;
            }

            // 区域自结算类（SelectionMode=None，2026-09-11）：无候选要求——
            // 空牌库抽卡走疲劳、空库磨牌空转等由 handler 自裁，不在预检拦
            if (def.SelectionMode == SelectionMode.None) return true;

            var domain = def.TargetDomain;
            if (def.ChoiceDomains != null && ctx.ModeIndex >= 0
                && ctx.ModeIndex < def.ChoiceDomains.Length
                && def.ChoiceDomains[ctx.ModeIndex] != null
                && def.ChoiceDomains[ctx.ModeIndex].Count > 0)
            {
                domain = def.ChoiceDomains[ctx.ModeIndex];
            }
            if (domain == null || domain.Count == 0) return true; // 无目标效果不拦

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
            return candidates.Count > 0 || ProspectiveCoversUnitKind(domain, prospective);
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
