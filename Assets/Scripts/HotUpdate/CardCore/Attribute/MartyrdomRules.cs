using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace CardCore.Attribute
{
    /// <summary>
    /// 舍身配对系统（2026-10-16 关键词化——原舍身仪典规则光环退役；守护 GuardianRules 的进攻向对偶）：
    /// 登场时选择一个对方目标（单位或角色），自己造成的战斗伤害改写为对目标造成等量效果伤害
    ///（非战斗路径——不触发战斗反击链、被禁魔石清零；原目标免受）。
    ///
    /// 绑定语义（镜像 GuardianRules）：
    /// - 配对表：持有者 runtimeId → (持有者, 转投目标)；重复选择取最新覆盖（叠层授予即重选）；
    /// - 无限次改写直到持有者死亡/离场——离场即清配对（死亡/弹回/流放/牺牲统一口）；
    ///   墓地复活=新入场事件→重新选择（旧关系必然不存在）；复生 Reborn=原地留场不离场→配对保持；
    /// - 查询时活性实时校验（存活 && 在场 && 未被「缄默」压制——FindVictim 持有者侧检查）——无事件注销、懒清扫；
    /// - 转投目标死亡不清配对：苏生复活同实例（runtimeId 不变）转投延续——断链判据只有持有者侧。
    /// - 选目标走 TargetSelectionService：UI 弹选 / AI 与无头自动取首候选；候选=对方战场单位+对方角色；
    ///   空候选跳过（无转投对象=关键词空转，战斗伤害照常结算）。
    /// </summary>
    public static class MartyrdomRules
    {
        /// <summary>配对表：键=持有者 runtimeId（局内唯一）；值含持有者引用供反查。</summary>
        private static readonly Dictionary<uint, (Card holder, Entity victim)> _bonds
            = new Dictionary<uint, (Card, Entity)>();

        /// <summary>接线（GameCore.Initialize 调用；静态方法组委托等值可退订——先退再订幂等，防多局重复订阅）。</summary>
        public static void EnsureSubscribed()
        {
            EventManager.Instance.Unsubscribe<CardPutToBattlefieldEvent>(OnEnterBattlefield);
            EventManager.Instance.Unsubscribe<CardLeaveBattlefieldEvent>(OnLeaveBattlefield);
            EventManager.Instance.Subscribe<CardPutToBattlefieldEvent>(OnEnterBattlefield);
            EventManager.Instance.Subscribe<CardLeaveBattlefieldEvent>(OnLeaveBattlefield);
        }

        /// <summary>新局清档（GameCore.Reset 级联——转投目标引用跨局失效，必须清）。</summary>
        public static void Reset() => _bonds.Clear();

        /// <summary>入场钩子：持有舍身的卡入场（打出/召唤/苏生/衍生物）→ 立即弹选转投目标。</summary>
        public static void OnEnterBattlefield(CardPutToBattlefieldEvent e)
        {
            var card = e?.Card;
            if (card == null || !card.IsAlive || !card.HasKeyword(KeywordRules.Martyrdom)) return;
            SelectAndBindAsync(card, e.Controller ?? card.GetController()).Forget();
        }

        /// <summary>离场钩子：断链统一口（死亡/弹回/流放/牺牲）——复活后旧舍身关系必然不存在。</summary>
        public static void OnLeaveBattlefield(CardLeaveBattlefieldEvent e)
        {
            if (e?.Card == null) return;
            _bonds.Remove(e.Card.RuntimeId);
        }

        /// <summary>授予即选入口（2026-10-16 定案，镜像守护）：GrantMartyrdom 落到已在场的卡 → 立即弹选。
        /// 印刷入场不走此口（入场事件负责；RefreshPrintedKeywordsOnEntry 不经 GrantKeywordHandler，无双选）。</summary>
        public static void OnGrantedInPlay(Card target)
        {
            if (target == null || !target.IsAlive) return;
            if (target.GetZone() != Zone.Battlefield) return;
            SelectAndBindAsync(target, target.GetController()).Forget();
        }

        /// <summary>反查：持有者的活性转投目标（无则 null）。改写咽喉（KeywordRules.ApplyDamage）调用。</summary>
        public static Entity FindVictim(Card holder)
        {
            if (holder == null || _bonds.Count == 0) return null;
            if (!_bonds.TryGetValue(holder.RuntimeId, out var bond)) return null;
            if (holder.GetCounterCount(CounterRules.NullifyCounter) > 0) return null; // 「缄默」=压制口（镜像守护 FindGuardian）
            var victim = bond.victim;
            if (victim == null || !victim.IsAlive) return null;
            if (victim is Card vc && vc.GetZone() != Zone.Battlefield) return null;
            return victim;
        }

        /// <summary>弹选并登记（fire-and-forget：无头同步完成；UI 弹窗异步收口后覆盖登记）。</summary>
        private static async UniTaskVoid SelectAndBindAsync(Card holder, Player controller)
        {
            var core = CardCore.GameCore.Instance;
            var zm = core?.ZoneManager;
            if (holder == null || !holder.IsAlive || zm == null || controller == null) return;
            var foe = controller.Opponent;
            if (foe == null) return;

            var candidates = new List<Entity>();
            foreach (var c in zm.GetCards(foe, Zone.Battlefield))
                if (c != null && c.IsAlive) candidates.Add(c);
            candidates.Add(foe); // 对方角色（与守护的己方域对偶：护己方 vs 打对方）
            if (candidates.Count == 0) return;

            var picked = await TargetSelectionService.RequestAsync(new TargetSelectionRequest
            {
                Candidates = candidates,
                MinCount = 1,
                MaxCount = 1,
                Chooser = controller,
                Title = "舍身：选择伤害转投的目标",
                Hint = "该单位造成的战斗伤害将改写为对所选目标造成等量效果伤害，直到其死亡或离场",
            });
            var victim = picked != null && picked.Count > 0 ? picked[0] : null;
            if (victim == null) return;

            _bonds[holder.RuntimeId] = (holder, victim);
            EventManager.Instance.Publish(new KeywordAppliedEvent
            {
                Target = victim,
                Keyword = KeywordRules.Martyrdom,
                Detail = $"舍身结成：{EffectText.Name(holder)} 的战斗伤害转投 {EffectText.Name(victim)}",
                Source = holder,
            });
        }
    }
}
