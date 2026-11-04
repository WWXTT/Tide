using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using CardCore.Attribute;

namespace CardCore
{
    /// <summary>
    /// 英雄技能系统（2026-10-15 深夜三类卡改版：技能载体从结界改为**法术**）：
    /// - **资格**：法术（Spell）且挂 1~3 个效果、全部为主动效果
    ///   （TriggerTiming ∈ {Activate_Active, Activate_Instant}——响应式不算，主阶段按钮发不出去）；
    ///   旧「结界+恰好1主动+耐久≥1」资格随结界禁主动整体退役；
    /// - **选卡来源**：构筑时任选全卡池一张法术（skillCardId），**不占卡组 30-60 张、不进牌库**——
    ///   开局 InitGame 从卡池生成实例直接放入 FieldZone（英雄技能栏）；
    /// - **发动经济**：每回合限 1 次（横置闸——发动成功经 ActivateEffect 通用路径横置，
    ///   回合开始 ResetSkillForTurn 解横置；接替旧耐久池−1/归零休眠/+1回充，耐久层整体退役）；
    ///   费用=**锚价现付**（2026-11-01 定案，推翻 2026-10-18「免锚价+抉择另收1灰」口径）：
    ///   与法术施放/主动效果同一价源（CostDerivationService.DeriveElementCosts），
    ///   多效果（抉择）技能按所选效果锚价付（UI 技能栏费用显示本就是锚价口径——两端口径就此合流）；
    ///   发动速度恒 0（不吃法术槽位盈余）；卡费(出牌费)不付；
    /// - **抉择**：挂 >1 效果时发动同样弹窗三选一（UI 侧 ShowChoiceOverlay；effectIndex 参数传递）；
    /// - **发动**：走通用主动路径（GameActions.ActivateEffect）：声明→入栈→可被响应/两层无效。
    /// 接线：GameActions.ActivateHeroSkill（声明口，签名扩展 effectIndex——AI/脚本/UI 兼容缺省 0）。
    /// </summary>
    public static class HeroSkillSystem
    {
        // ======================================== 资格与解析 ========================================

        /// <summary>技能卡资格（构筑标记门槛 + InitGame 落位守卫共用）：
        /// 法术 && 1~3 个效果 && 全部为主动效果（19/20，响应式不算）
        /// && **不带代价**（2026-11-01 定案，与「主动效果禁代价」同口径——技能栏不开放
        /// EffectActivationGate 净费闸，代价行在技能发动路径不会被正确执行/抵扣）
        /// && **不带分支载荷**（2026-11-02 用户定案：「手牌为0抽1」这类门奖励、产出条件、
        /// 引擎奖励一律不能挂到角色技能上——技能只允许效果本体）
        /// && **效果数值上限 value=1**（2026-11-02 定案：value≥2 的原子不能挂成技能——
        /// 威力档超技能定位；速度侧由技能源基速恒 0 已钉死）。
        /// 与法术形状校验（CardLoader.ValidateSpellShape）同口径。</summary>
        public static bool CanBeSkillCard(CardData data)
        {
            if (data == null || data.Supertype != Cardtype.Spell) return false;
            if (data.Effects == null || data.Effects.Count < 1 || data.Effects.Count > 3) return false;
            foreach (var e in data.Effects)
            {
                int timing = e?.TriggerTiming ?? -1;
                if (timing != (int)TriggerTiming.Activate_Active
                    && timing != (int)TriggerTiming.Activate_Instant)
                    return false;
            }
            if (CardCostService.HasPayloadCost(data)) return false;
            if (SkillBranchViolations(data).Count > 0) return false;
            if (SkillValueViolations(data).Count > 0) return false;
            return true;
        }

        /// <summary>不合格原因（构筑 UI toast / 握手校验日志用）；合格返回 null。</summary>
        public static string SkillIneligibleReason(CardData data)
        {
            if (data == null) return "卡不存在";
            if (data.Supertype != Cardtype.Spell) return "只有法术可以作为英雄技能";
            int count = data.Effects?.Count ?? 0;
            if (count < 1 || count > 3) return $"技能卡须挂 1~3 个主动效果（现 {count} 个效果）";
            foreach (var e in data.Effects)
            {
                int timing = e?.TriggerTiming ?? -1;
                if (timing != (int)TriggerTiming.Activate_Active && timing != (int)TriggerTiming.Activate_Instant)
                    return "技能卡的效果必须全部为主动效果（不能是被动/触发/响应式）";
            }
            if (CardCostService.HasPayloadCost(data)) return "技能卡不能带代价（主动效果禁代价同理）";
            var branchHits = SkillBranchViolations(data);
            if (branchHits.Count > 0)
                return $"{data.CardName} 技能效果带了分支载荷：{string.Join("；", branchHits)}"
                       + "——角色技能不能挂门奖励/产出条件/引擎奖励（如「手牌为0抽1」），请用效果本体";
            var valueHits = SkillValueViolations(data);
            if (valueHits.Count > 0)
                return $"{data.CardName} 技能效果数值超限：{string.Join("；", valueHits)}"
                       + "——技能效果只允许 value=1（2026-11-02 定案），请降低数值档或另选技能卡";
            return null;
        }

        // ======================================== 角色技能禁分支载荷（2026-11-02 用户定案） ========================================

        /// <summary>技能卡分支载荷违规明细（空=合规）：主序列原子 + 步骤原子的槽级 Branch 一律禁止。
        /// 理由（用户定案）：角色技能是**0 速、每回合一次的固定效果**——「手牌为0抽1」这类门奖励、
        /// 产出条件、引擎奖励会把它变成条件引擎，与技能定位冲突。
        /// 消费方：CanBeSkillCard / SkillIneligibleReason / 重推工具与验证器。</summary>
        public static List<string> SkillBranchViolations(CardData data)
        {
            var result = new List<string>();
            if (data?.Effects == null) return result;
            foreach (var fx in data.Effects)
            {
                if (fx == null) continue;
                ScanSkillBranches(fx, fx.AtomicEffects, result);
                if (fx.Steps != null)
                    foreach (var step in fx.Steps)
                        if (step?.atomic != null)
                            ScanSkillBranches(fx, new[] { step.atomic }, result);
            }
            return result;
        }

        private static void ScanSkillBranches(CardEffectData fx, IEnumerable<AtomicEffectEntry> entries, List<string> result)
        {
            if (entries == null) return;
            foreach (var e in entries)
            {
                if (e == null || BranchEntryRules.IsPhantom(e.branch)) continue;
                result.Add($"效果[{fx.DisplayName ?? fx.Id}] 原子#{e.refId} 带槽级分支载荷");
            }
        }

        // ======================================== 效果数值上限（2026-11-02 定案：技能只允许 value=1） ========================================

        /// <summary>技能效果数值违规明细（数据层扫描：主序列原子 + 步骤原子 + 分支奖励原子；
        /// 每条含效果名 + 原子行 id + value）。空 = 合规。
        /// 消费方：CanBeSkillCard（资格闸）/ SkillIneligibleReason（构筑期报错）/ 重推工具与验证器。
        /// 口径：value≥2 拒绝；value=0/1 放行（0=无值参数原子，1=标准档）。</summary>
        public static List<string> SkillValueViolations(CardData data)
        {
            var result = new List<string>();
            if (data?.Effects == null) return result;
            foreach (var fx in data.Effects)
            {
                if (fx == null) continue;
                CheckSkillValueEntries(fx, fx.AtomicEffects, result);
                if (fx.Steps != null)
                {
                    foreach (var step in fx.Steps)
                    {
                        if (step?.atomic != null) CheckSkillValueEntries(fx, new[] { step.atomic }, result);
                    }
                }
            }
            return result;
        }

        private static void CheckSkillValueEntries(CardEffectData fx, IEnumerable<AtomicEffectEntry> entries, List<string> result)
        {
            if (entries == null) return;
            foreach (var e in entries)
            {
                if (e == null) continue;
                if (e.value >= 2)
                {
                    var cfg = Attribute.AtomicEffectTable.GetByHashId(e.refId);
                    result.Add($"效果[{fx.DisplayName ?? fx.Id}] 原子[{cfg?.DisplayName ?? e.refId}#{e.refId}] value={e.value}");
                }
                if (e.branch?.then != null) CheckSkillValueEntries(fx, e.branch.then, result); // 分支奖励同口径
            }
        }

        /// <summary>运行时口（CanActivate 预检用）：转换后效果内任一原子 value≥2 → 违规明细；
        /// 合规返回 null。扫主序列原子（含子效果与分支奖励——全部随技能发动结算）。</summary>
        public static string EffectValueViolation(EffectDefinition def)
        {
            if (def?.Effects == null) return null;
            foreach (var atom in def.Effects)
            {
                var v = AtomValueViolation(def, atom);
                if (v != null) return v;
            }
            return null;
        }

        private static string AtomValueViolation(EffectDefinition def, AtomicEffectInstance atom)
        {
            if (atom == null) return null;
            if (atom.Value >= 2)
                return $"效果[{def.DisplayName ?? def.Id}] 原子[{atom.Type}#{atom.RowHashId}] value={atom.Value}"
                       + "——技能效果只允许 value=1（2026-11-02 定案）";
            foreach (var sub in atom.SubEffects ?? new List<AtomicEffectInstance>())
            {
                var v = AtomValueViolation(def, sub);
                if (v != null) return v;
            }
            if (atom.Branch?.Then != null)
            {
                foreach (var reward in atom.Branch.Then)
                {
                    var v = AtomValueViolation(def, reward);
                    if (v != null) return v;
                }
            }
            return null;
        }

        /// <summary>技能卡的全部主动效果（运行时口径：转换后的 EffectDefinition，1~3 条）。
        /// 空 = 无可发动效果（防御性：资格校验之外的兜底，如卡面被运行时改写）。
        /// P12（2026-10-31）：充能效果排除——充能只能从手牌发动，技能栏（FieldZone）源恒不可发动。</summary>
        public static List<EffectDefinition> SkillEffectsOf(Card skillCard)
            => skillCard == null
                ? new List<EffectDefinition>()
                : GameActions.GetCardEffectDefinitions(skillCard)
                    ?.Where(d => d != null && d.IsActivatedEffect && !ComposerCatalog.IsChargeEffect(d)).ToList()
                ?? new List<EffectDefinition>();

        /// <summary>技能源判定：技能栏（FieldZone）里的法术卡。
        /// 消费口=发动速度（PendingEffect.Create：技能源基速恒 0——法术宿主效果 BaseSpeed
        /// 含槽位盈余烘焙，技能口不取）。费用不再经此判（2026-11-01 锚价现付定案：
        /// EffectExecutor 两付费点对技能源走通用 DeriveElementCosts，无专属豁免）。</summary>
        public static bool IsSkillSource(Entity source)
            => source is Card c
               && c.GetZone() == Zone.FieldZone
               && (c as CardWrapper)?.GetData()?.Supertype == Cardtype.Spell;

        /// <summary>单效果便捷口（构筑期 UI 预览/费用显示用）：恰好一个主动效果时返回它；
        /// 0 个或多个（需弹窗抉择）返回 null。</summary>
        public static EffectDefinition SkillEffectOf(Card skillCard)
        {
            var activated = SkillEffectsOf(skillCard);
            return activated.Count == 1 ? activated[0] : null;
        }

        /// <summary>同上（CardData 口径，构筑期 UI 预览用）。</summary>
        public static EffectDefinition SkillEffectOf(CardData data)
            => data == null ? null : SkillEffectOf(new CardWrapper(data));

        // ======================================== 开局落位（卡池任选，不占卡组张数） ========================================

        /// <summary>从卡池生成技能卡实例（InitGame 调用）：按 skillCardId 查卡表，
        /// 资格不过/查无此卡 → null（外部来源的 id 不信任——网络等路径同门）。
        /// 不再从牌库抽取（2026-10-15 改版：技能不占卡组张数、不进牌库）。
        /// 合成卡兜底：卡池未命中时回落 ExtractSkillCard（验证器/无头夹具的合成卡组不经卡表——
        /// 真实卡池恒命中，此回落只服务合成卡路径）。</summary>
        public static Card CreateSkillCard(List<Card> deck, string skillCardId)
        {
            if (string.IsNullOrEmpty(skillCardId)) return null;
            var data = SynergyUI.CardCatalog.GetById(skillCardId);
            if (CanBeSkillCard(data)) return new CardWrapper(data);
            return ExtractSkillCard(deck, skillCardId); // 合成卡回落（老路径：牌库内查找抽取）
        }

        /// <summary>牌库内查找并抽出标记卡（合成卡兜底路径——真实卡池不占张数不进牌库）：
        /// 找到即移除并返回；未标记/找不到/不合格 → null。</summary>
        public static Card ExtractSkillCard(List<Card> deck, string skillCardId)
        {
            if (deck == null || string.IsNullOrEmpty(skillCardId)) return null;
            int idx = deck.FindIndex(c => c is CardWrapper w
                && w.GetData()?.ID == skillCardId && CanBeSkillCard(w.GetData()));
            if (idx < 0) return null;
            var card = deck[idx];
            deck.RemoveAt(idx);
            return card;
        }

        /// <summary>AI 自动挑技能：候选列表（缺省全卡池玩家可用面）里合格法术中**锚价最低**的一张
        ///（锚价=各效果锚价总额的最低那条——挑最便宜的发动路径；无合格卡 → null=无技能）。
        /// RL/挑战传卡组列表时优先在卡组内挑（卡组没法术再回落全池——技能不占张数，卡组照常完整）。</summary>
        public static string AutoPickSkillCard(IEnumerable<CardData> source = null)
        {
            IEnumerable<CardData> candidates = source ?? DefaultSkillCandidates();
            CardData best = null;
            float bestAnchor = float.MaxValue;
            foreach (var c in candidates)
            {
                if (!CanBeSkillCard(c)) continue;
                float anchor = MinEffectAnchor(c);
                if (anchor < bestAnchor) { bestAnchor = anchor; best = c; }
            }
            if (best == null && source != null)
                return AutoPickSkillCard(); // 卡组内无合格法术——回落全卡池
            return best?.ID;
        }

        /// <summary>默认技能候选（2026-10-22 三套预组迁教学）：玩家可用面 LoadPlayPool 可为空
        ///（主题卡全带 tags=教学）——为空时回落全卡池，保证仍有合格主动法术可挑。
        /// 注：战斗入口（挑战/木桩/联机）对预组卡组直接传卡组自带 skillCardId，不依赖本回落；
        /// 本类在 TideServer 共享编译（CardCore 目录），不得引用 UI 层 DeckCatalog。</summary>
        private static IEnumerable<CardData> DefaultSkillCandidates()
        {
            var pool = SynergyUI.CardCatalog.LoadPlayPool();
            if (pool != null && pool.Count > 0) return pool;
            return SynergyUI.CardCatalog.LoadAll();
        }

        /// <summary>卡上各效果锚价总额的最小值（AutoPick 挑选键；P12：充能效果排除——0 费行）。</summary>
        private static float MinEffectAnchor(CardData data)
        {
            float best = float.MaxValue;
            foreach (var def in GameActions.GetCardEffectDefinitions(new CardWrapper(data)))
            {
                if (def == null || !def.IsActivatedEffect || ComposerCatalog.IsChargeEffect(def)) continue;
                best = Math.Min(best, CostDerivationService.DeriveElementCosts(def, 0)?.Total ?? 0f);
            }
            return best == float.MaxValue ? 0f : best;
        }

        /// <summary>技能卡落位 FieldZone 并写 player 字段；null = 清场无技能。
        /// 重复调用幂等（换技能/重开局安全）。FieldZone 现在只有技能卡——清残留按全区清。
        /// 耐久初始化已随耐久池退役（2026-10-15 改版：每回合一次=横置闸，无耐久层）。</summary>
        public static void AssignSkill(GameCore core, Player player, Card skillCard)
        {
            if (core == null || player == null) return;
            player.HeroSkillCard = null;
            var container = core.ZoneManager.GetZoneContainer(player);
            foreach (var old in core.ZoneManager.GetCards(player, Zone.FieldZone).ToList())
                container.Remove(old, Zone.FieldZone);

            if (skillCard == null) return;
            skillCard.SetController(player);
            skillCard.SetOwner(player);
            container.Add(skillCard, Zone.FieldZone);
            player.HeroSkillCard = skillCard;
        }

        /// <summary>解析技能卡实体：优先运行时引用；丢失时（快照/存档恢复）按 FieldZone
        /// 现场 lazy 回填（FieldZone 现在只有技能卡——首卡即技能卡）。</summary>
        public static Card ResolveSkillCard(GameCore core, Player player)
        {
            if (player == null) return null;
            if (player.HeroSkillCard != null) return player.HeroSkillCard;
            if (core == null) return null;
            player.HeroSkillCard = core.ZoneManager.GetCards(player, Zone.FieldZone)
                .FirstOrDefault();
            return player.HeroSkillCard;
        }

        /// <summary>此技能累计发动次数（读技能卡 SkillUse 指示物；无技能卡=0）。
        /// 升级机制已退役（2026-10-07）——纯遥测/观察口径保留。</summary>
        public static int GetTotalUses(GameCore core, Player player)
            => ResolveSkillCard(core, player)?.GetCounterCount(CounterRules.SkillUseCounter) ?? 0;

        // ======================================== 发动 ========================================

        /// <summary>
        /// 发动英雄技能（主阶段声明口）= 发动技能卡的主动效果之一（>1 效果时由 UI 弹窗择定，effectIndex 传递）。
        /// 守卫：回合玩家 + 主阶段 + 技能卡在 FieldZone 且**未横置**（每回合一次闸，回合开始解横置）
        /// + 未被沉默 + 效果 CanActivate 预检（时点/条件/费用可付性含目标域非空）。
        /// 成功 → 通用 ActivateEffect：入栈（可被响应/两层无效）→ 横置=声明期代价（通用 ShouldTap 路径）
        /// → SkillUse 计数 +1（遥测）→ 发 HeroSkillActivatedEvent。结算在栈排空时（锚价由执行器现付）。
        /// </summary>
        public static async UniTask<bool> ActivateAsync(GameCore core, Player player, int effectIndex = 0)
            => TryActivateSkill(core, player, effectIndex, out _);

        /// <summary>带原因版（2026-11-03 失败提示细化）：同步核（守卫+预检+ActivateEffect 全链同步，
        /// 伪异步仅签名兼容）——UI toast / 网络泵线程直读拒绝文案。</summary>
        public static bool TryActivateSkill(GameCore core, Player player, int effectIndex, out string reason)
        {
            reason = null;
            if (core == null || player == null) { reason = "内部错误：空参数"; return false; }
            if (core.TurnEngine.TurnPlayer != player) { reason = "只能在你的回合发动技能"; return false; }
            if (core.TurnEngine.CurrentPhase?.Phase != PhaseType.Main)
            { reason = $"技能发动只能在你的主阶段（当前：{core.TurnEngine.CurrentPhase?.Phase}）"; return false; }

            var skillCard = ResolveSkillCard(core, player); // 含快照恢复后的 lazy 回填
            if (skillCard == null) { reason = "本局无技能卡"; return false; } // 未标记技能卡
            if (!core.ZoneManager.GetCards(player, Zone.FieldZone).Contains(skillCard))
            { reason = "技能卡已离场——技能不可用"; return false; } // 已被摧毁/弹回/移场——技能随卡离场失效
            // 每回合一次闸（2026-10-15 改版）：横置=本回合已发动；回合开始 ResetSkillForTurn 解横置。
            if (skillCard.IsTapped()) { reason = "技能本回合已发动"; return false; }
            if (skillCard.GetCounterCount(CounterRules.SilenceCounter) > 0)
            { reason = "技能卡被沉默——不能发动"; return false; } // 沉默：不可发动主动效果

            var effects = SkillEffectsOf(skillCard);
            if (effects.Count == 0) { reason = "技能卡无可发动效果"; return false; } // 卡面无主动效果（防御性）
            var effect = effects[Math.Clamp(effectIndex, 0, effects.Count - 1)];

            // CanActivate 预检（费用可付性含自动横置地牌、目标域非空）——
            // 栈机器 TryActivateEffect 只管速度门，此处不过会在不可付时照样入栈白横置
            var executor = core.StackEngine.GetExecutor();
            if (executor == null)
            { reason = "执行器未就绪"; return false; }
            if (!executor.TryActivate(effect, skillCard, player,
                    core.TurnEngine.TurnPlayer,
                    core.TurnEngine.CurrentPhase?.Phase ?? PhaseType.Standby,
                    core.TurnEngine.TurnNumber, out reason))
                return false;

            if (!GameActions.ActivateEffect(core, player, effect, skillCard, 0, out reason)) return false;

            // 遥测：发动计数（升级机制已退役；快照/RL 观察口径保留）
            int totalUses = skillCard.GetCounterCount(CounterRules.SkillUseCounter) + 1;
            skillCard.AddCounters(CounterRules.SkillUseCounter, 1);
            core.PublishEvent(new HeroSkillActivatedEvent
            {
                Player = player,
                SkillCard = skillCard,
                TotalUses = totalUses,
            });
            return true;
        }

        // ======================================== 回合开始重置 ========================================

        /// <summary>
        /// 回合开始技能重置（2026-10-15 改版：每回合一次=横置闸）：解横置即可发动。
        /// 接线：GameCore.OnTurnStarted（引擎簿记区——跳过准备阶段也执行，位置同旧 RechargeSkill）。
        /// 返回是否真实解横置（2026-10-26 战报重置账目：StandbyResetEvent.SkillReset 数据源）。
        /// </summary>
        public static bool ResetSkillForTurn(GameCore core, Player player)
        {
            var skillCard = ResolveSkillCard(core, player);
            if (skillCard == null || !skillCard.IsTapped()) return false;
            skillCard.Untap();
            return true;
        }
    }

    /// <summary>英雄技能发动事件（播报/观察用；2026-10-07 卡牌化：技能=标记卡）。</summary>
    public class HeroSkillActivatedEvent : GameEventBase
    {
        public Player Player;
        public Card SkillCard;
        public int TotalUses;
    }
}
