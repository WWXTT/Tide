using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace CardCore.Attribute
{
    /// <summary>
    /// 守护配对系统（2026-10-08 配对制改版）——效果固定：登场时选择一个己方目标（单位或角色），
    /// 其受到的伤害改写为守护者自身承受。替代旧三形态（箭头光环改写/关键词扫场护角色/关键词挂角色空转）。
    ///
    /// 绑定语义：
    /// - 配对表：守护者 runtimeId → (守护者, 被守护者)；重复选择取最新覆盖（叠层授予即重选）；
    /// - 无限次改写直到守护者死亡/离场——离场即清配对（死亡/弹回/流放/牺牲统一口）；
    ///   墓地复活=新入场事件→重新选择（旧关系必然不存在，2026-10-08 用户定案）；
    ///   复生 Reborn=原地留场不离场→配对保持（不经换区出口）；
    /// - 查询时活性实时校验（存活 && 在场 && 未被「无效」压制）——RuleAuraSystem.Matches 同款，
    ///   无事件注销、懒清扫；
    /// - 被守护者死亡不清配对：苏生复活同实例（runtimeId 不变）保护延续——断链判据只有守护者侧。
    /// - 选目标走 TargetSelectionService：UI 弹选 / AI 与无头自动取首候选；候选=己方战场单位
    ///  （排除自己）+己方角色；空候选跳过。
    /// </summary>
    public static class GuardianRules
    {
        /// <summary>配对表：键=守护者 runtimeId（局内唯一）；值含守护者引用供反查。</summary>
        private static readonly Dictionary<uint, (Card guardian, Entity ward)> _bonds
            = new Dictionary<uint, (Card, Entity)>();

        /// <summary>接线（GameCore.Initialize 调用；静态方法组委托等值可退订——先退再订幂等，防多局重复订阅）。</summary>
        public static void EnsureSubscribed()
        {
            EventManager.Instance.Unsubscribe<CardPutToBattlefieldEvent>(OnEnterBattlefield);
            EventManager.Instance.Unsubscribe<CardLeaveBattlefieldEvent>(OnLeaveBattlefield);
            EventManager.Instance.Subscribe<CardPutToBattlefieldEvent>(OnEnterBattlefield);
            EventManager.Instance.Subscribe<CardLeaveBattlefieldEvent>(OnLeaveBattlefield);
        }

        /// <summary>新局清档（GameCore.Reset 级联——被守护者引用跨局失效，必须清）。</summary>
        public static void Reset() => _bonds.Clear();

        /// <summary>入场钩子：持有守护的卡入场（打出/召唤/苏生/衍生物）→ 立即弹选保护目标。</summary>
        public static void OnEnterBattlefield(CardPutToBattlefieldEvent e)
        {
            var card = e?.Card;
            if (card == null || !card.IsAlive || !card.HasKeyword(KeywordRules.Guardian)) return;
            SelectAndBindAsync(card, e.Controller ?? card.GetController()).Forget();
        }

        /// <summary>离场钩子：断链统一口（死亡/弹回/流放/牺牲）——复活后旧守护关系必然不存在。</summary>
        public static void OnLeaveBattlefield(CardLeaveBattlefieldEvent e)
        {
            if (e?.Card == null) return;
            _bonds.Remove(e.Card.RuntimeId);
        }

        /// <summary>授予即选入口（2026-10-08 定案）：GrantGuardian 落到已在场的卡 → 立即弹选。
        /// 印刷入场不走此口（入场事件负责；RefreshPrintedKeywordsOnEntry 不经 GrantKeywordHandler，无双选）。</summary>
        public static void OnGrantedInPlay(Card target)
        {
            if (target == null || !target.IsAlive) return;
            if (target.GetZone() != Zone.Battlefield) return;
            SelectAndBindAsync(target, target.GetController()).Forget();
        }

        /// <summary>反查：被守护目标的活性守护者（无则 null）。改写咽喉（KeywordRules.ApplyDamage）调用。</summary>
        public static Card FindGuardian(Entity ward)
        {
            if (ward == null || _bonds.Count == 0) return null;
            foreach (var bond in _bonds.Values)
            {
                if (!ReferenceEquals(bond.ward, ward)) continue;
                var g = bond.guardian;
                if (g == null || !g.IsAlive || g.GetZone() != Zone.Battlefield) continue;
                if (g.GetCounterCount(CounterRules.NullifyCounter) > 0) continue; // 「无效」=压制口（沿旧形态口径）
                return g;
            }
            return null;
        }

        /// <summary>弹选并登记（fire-and-forget：无头同步完成；UI 弹窗异步收口后覆盖登记）。</summary>
        private static async UniTaskVoid SelectAndBindAsync(Card guardian, Player controller)
        {
            var core = CardCore.GameCore.Instance;
            var zm = core?.ZoneManager;
            if (guardian == null || !guardian.IsAlive || zm == null || controller == null) return;

            var candidates = new List<Entity>();
            foreach (var c in zm.GetCards(controller, Zone.Battlefield))
                if (c != null && c.IsAlive && !ReferenceEquals(c, guardian)) candidates.Add(c);
            candidates.Add(controller); // 己方角色（2026-10-08 定案：己方单位+角色）
            if (candidates.Count == 0) return;

            var picked = await TargetSelectionService.RequestAsync(new TargetSelectionRequest
            {
                Candidates = candidates,
                MinCount = 1,
                MaxCount = 1,
                Chooser = controller,
                Title = "守护：选择要保护的目标",
                Hint = "所选目标受到的伤害将改写为该守护者自身承受，直到守护者死亡或离场",
            });
            var ward = picked != null && picked.Count > 0 ? picked[0] : null;
            if (ward == null) return;

            _bonds[guardian.RuntimeId] = (guardian, ward);
            EventManager.Instance.Publish(new KeywordAppliedEvent
            {
                Target = ward,
                Keyword = KeywordRules.Guardian,
                Detail = $"守护结成：{guardian} 保护 {ward}",
                Source = guardian,
            });
        }
    }
}
