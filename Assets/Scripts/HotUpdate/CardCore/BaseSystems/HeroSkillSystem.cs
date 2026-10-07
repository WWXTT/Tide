using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using CardCore.Attribute;

namespace CardCore
{
    /// <summary>
    /// 英雄技能系统（2026-10-07 卡牌化改版：旧三色硬编码技能蓝洞察/绿培育/红狂热与
    /// 「7 次发动升级」机制整体退役——技能不再由代码定义，改为**卡组构筑标记的一张结界**）：
    /// - **资格**：结界（Enchantment）且卡上**只有一个效果、该效果为主动效果**
    ///   （TriggerTiming ∈ {Activate_Active, Activate_Instant}，口径同 CardData.HasActiveEffect——
    ///   响应式 Activate_Response 不算，避免主阶段技能按钮发动不了的死角）；
    ///   不允许另带被动/触发效果（它们在技能栏本就不生效，带=白带）；
    /// - **落位**：开局 InitGame 从牌库抽出标记卡直接放入 FieldZone（英雄技能栏，
    ///   原额外卡组退役后的空缺槽位）——牌库少一张；未标记/不合格=本局无技能；
    /// - **发动**：走通用启动式路径（GameActions.ActivateEffect）：声明→入栈→可被响应/
    ///   两层无效；费用=效果派生元素价（执行器结算路径扣除）；横置=声明期代价
    ///   （一回合一次，回合开始重置——GameCore.OnTurnStarted）；
    /// - **耐久**：技能栏落位不初始化耐久层（EquipRules 只在入战场时挂），
    ///   发动=横置不碰耐久——技能栏结界不存在耐久消耗（结构性成立，无需特判）；
    /// - AI 随机卡组（挑战/RL 自博弈）经 AutoPickSkillCard 自动挑一张合格结界充当技能。
    /// 接线：GameActions.ActivateHeroSkill（声明口，签名不变——AI/脚本/UI 调用方零改动）。
    /// </summary>
    public static class HeroSkillSystem
    {
        // ======================================== 资格与解析 ========================================

        /// <summary>技能卡资格（构筑标记门槛 + InitGame 落位守卫共用）：
        /// 结界 && 恰好一个效果 && 该效果为主动效果（19/20，响应式不算）。</summary>
        public static bool CanBeSkillCard(CardData data)
        {
            if (data == null || data.Supertype != Cardtype.Enchantment) return false;
            if (data.Effects == null || data.Effects.Count != 1) return false;
            int timing = data.Effects[0]?.TriggerTiming ?? -1;
            return timing == (int)TriggerTiming.Activate_Active
                   || timing == (int)TriggerTiming.Activate_Instant;
        }

        /// <summary>不合格原因（构筑 UI toast / 握手校验日志用）；合格返回 null。</summary>
        public static string SkillIneligibleReason(CardData data)
        {
            if (data == null) return "卡不存在";
            if (data.Supertype != Cardtype.Enchantment) return "只有结界可以作为英雄技能";
            int count = data.Effects?.Count ?? 0;
            if (count != 1) return $"技能卡须恰好一个主动效果（现 {count} 个效果）";
            int timing = data.Effects[0]?.TriggerTiming ?? -1;
            if (timing != (int)TriggerTiming.Activate_Active && timing != (int)TriggerTiming.Activate_Instant)
                return "技能卡的唯一效果必须是主动效果（不能是被动/触发/响应式）";
            return null;
        }

        /// <summary>技能卡的唯一主动效果（运行时口径：转换后的 EffectDefinition）。
        /// 防御性：0 个或 &gt;1 个主动效果 → null（资格校验之外的兜底，如卡面被运行时改写）。</summary>
        public static EffectDefinition SkillEffectOf(Card skillCard)
        {
            if (skillCard == null) return null;
            var activated = GameActions.GetCardEffectDefinitions(skillCard)
                ?.Where(d => d.IsActivatedEffect).ToList();
            return activated != null && activated.Count == 1 ? activated[0] : null;
        }

        /// <summary>同上（CardData 口径，构筑期 UI 预览用）。</summary>
        public static EffectDefinition SkillEffectOf(CardData data)
            => data == null ? null : SkillEffectOf(new CardWrapper(data));

        // ======================================== 开局落位 ========================================

        /// <summary>从卡组列表抽出标记卡（InitGame 填牌库前调用）：找到即移除并返回；
        /// 未标记/找不到/不合格 → null（不合格在抽取口一并拦截——网络等外部来源的 id 不信任）。</summary>
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

        /// <summary>AI 随机卡组自动挑技能：第一张合格结界的 ID（无合格卡 → null=无技能）。</summary>
        public static string AutoPickSkillCard(IEnumerable<CardData> deck)
            => deck?.FirstOrDefault(CanBeSkillCard)?.ID;

        /// <summary>技能卡落位 FieldZone 并写 player 字段；null = 清场无技能。
        /// 重复调用幂等（换技能/重开局安全）。FieldZone 现在只有技能卡——清残留按全区清。</summary>
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
        /// 发动英雄技能（主阶段声明口，2026-10-07 卡牌化改版）= 发动技能卡的唯一主动效果。
        /// 守卫：回合玩家 + 主阶段 + 技能卡在 FieldZone 且未横置且未被沉默 + 效果 CanActivate 预检
        /// （时点/条件/费用可付性含自动横置/目标域非空——TryActivateEffect 只查速度门，预检在此补）。
        /// 成功 → 通用 ActivateEffect：入栈（可被响应/两层无效）→ 横置=声明期代价（不重置）→
        /// SkillUse 计数 +1（遥测）→ 发 HeroSkillActivatedEvent。结算在栈排空时（费用由执行器扣）。
        /// </summary>
        public static async UniTask<bool> ActivateAsync(GameCore core, Player player)
        {
            if (core == null || player == null) return false;
            if (core.TurnEngine.TurnPlayer != player) return false;
            if (core.TurnEngine.CurrentPhase?.Phase != PhaseType.Main) return false;

            var skillCard = ResolveSkillCard(core, player); // 含快照恢复后的 lazy 回填
            if (skillCard == null) return false; // 未标记技能卡
            if (!core.ZoneManager.GetCards(player, Zone.FieldZone).Contains(skillCard))
                return false; // 已被摧毁/弹回/移场——技能随卡离场失效
            if (skillCard.IsTapped()) return false; // 本回合已发动（回合开始重置）
            if (skillCard.GetCounterCount(CounterRules.SilenceCounter) > 0)
                return false; // 沉默：不可发动主动效果（与永续魔法交互一致）

            var effect = SkillEffectOf(skillCard);
            if (effect == null) return false; // 卡面无唯一主动效果（防御性）

            // CanActivate 预检（费用可付性含自动横置地牌、目标域非空）——
            // 栈机器 TryActivateEffect 只管速度门，此处不过会在不可付时照样入栈白横置
            var executor = core.StackEngine.GetExecutor();
            if (executor == null || !executor.CanActivate(effect, skillCard, player,
                    core.TurnEngine.TurnPlayer,
                    core.TurnEngine.CurrentPhase?.Phase ?? PhaseType.Standby,
                    core.TurnEngine.TurnNumber))
                return false;

            if (!GameActions.ActivateEffect(core, player, effect, skillCard)) return false;

            // 遥测：发动计数（升级机制已退役；快照/RL 观察口径保留）
            int totalUses = skillCard.GetCounterCount(CounterRules.SkillUseCounter) + 1;
            skillCard.AddCounters(CounterRules.SkillUseCounter, 1);
            core.PublishEvent(new HeroSkillActivatedEvent
            {
                Player = player,
                SkillCard = skillCard,
                TotalUses = totalUses,
            });
            await UniTask.CompletedTask;
            return true;
        }
    }

    /// <summary>英雄技能发动事件（播报/观察用；2026-10-07 卡牌化：技能=标记结界卡）。</summary>
    public class HeroSkillActivatedEvent : GameEventBase
    {
        public Player Player;
        public Card SkillCard;
        public int TotalUses;
    }
}
