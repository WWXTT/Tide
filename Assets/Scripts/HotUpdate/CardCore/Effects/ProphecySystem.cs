using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using CardCore.Attribute;
using CardCore.Attribute.Handlers;

namespace CardCore
{
    /// <summary>
    /// 待验证预言（隐藏押注）。由执行引擎在延迟分支处打包注册：
    /// 押注内容（Declaration）来自信息族原子写入的 LastOutcome.Declaration，
    /// 奖励（Then/Else 原子表）来自紧随其后的分支步骤。
    /// </summary>
    public class PendingProphecy
    {
        public Player Declarer;
        public Entity Source;
        public string Declaration;          // "维度:值" 编码（见 ProphecyDimension）
        public string ConditionId;          // ProphecyHit / ProphecyMiss
        public int ConditionParam;
        public string ConditionStringParam;
        public List<AtomicEffectInstance> Then = new List<AtomicEffectInstance>();
        public List<AtomicEffectInstance> Else = new List<AtomicEffectInstance>();

        // 验证时刻执行奖励所需的系统引用（登记时从执行上下文快照）
        public ZoneManager ZoneManager;
        public ElementPoolSystem ElementPool;

        internal bool Armed; // 对手回合开始时置 true；其首张出牌触发验证
    }

    /// <summary>
    /// 预言系统：延迟验证的登记 / 验证 / 到期。
    ///
    /// 语义（定案）：
    /// - 押注隐藏——ProphecyRegisteredEvent 不含宣言内容，对手只知道"预言已立"；
    /// - 验证时机 = 对手下回合的**首张**出牌（TurnStart 武装、首张 CardPlay 触发）；
    /// - 对手整回合未出牌 → 到期作未命中，走 else 分支；
    /// - 多条预言同时在场：同一次首张出牌逐条验证（各自独立评估同一张卡）。
    /// GameCore.Reset 时清空（跨局不残留）。
    /// </summary>
    public static class ProphecySystem
    {
        private static readonly List<PendingProphecy> _pending = new List<PendingProphecy>();
        private static bool _subscribed;

        /// <summary>当前待验证预言（UI/调试用）</summary>
        public static IReadOnlyList<PendingProphecy> Pending => _pending;

        public static void Register(PendingProphecy prophecy)
        {
            if (prophecy == null || prophecy.Declarer == null) return;
            EnsureSubscribed();
            prophecy.Armed = false;
            _pending.Add(prophecy);

            if (GameCore.Instance != null)
                GameCore.Instance.PublishEvent(new ProphecyRegisteredEvent
                {
                    Declarer = prophecy.Declarer,
                    Source = prophecy.Source,
                });
            else
                EventManager.Instance.Publish(new ProphecyRegisteredEvent
                {
                    Declarer = prophecy.Declarer,
                    Source = prophecy.Source,
                });
        }

        /// <summary>清空全部待验证预言（新对局 Reset 调用）</summary>
        public static void Reset()
        {
            _pending.Clear();
        }

        private static void EnsureSubscribed()
        {
            if (_subscribed) return;
            _subscribed = true;
            EventManager.Instance.Subscribe<TurnStartEvent>(OnTurnStart);
            EventManager.Instance.Subscribe<CardPlayEvent>(OnCardPlayed);
            EventManager.Instance.Subscribe<TurnEndEvent>(OnTurnEnd);
        }

        private static void OnTurnStart(TurnStartEvent e)
        {
            foreach (var p in _pending)
                if (p.Declarer != null && e.TurnPlayer == p.Declarer.Opponent)
                    p.Armed = true;
        }

        private static void OnCardPlayed(CardPlayEvent e)
        {
            // 倒序遍历：验证即移除；仅对手的「首张」出牌（武装位在首次命中后随移除消失）
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                var p = _pending[i];
                if (!p.Armed || p.Declarer == null || e.Player != p.Declarer.Opponent) continue;
                _pending.RemoveAt(i);

                bool hit = e.PlayedCard is Card card
                           && ProphecyDimension.TryParse(p.Declaration, out var dim, out var val)
                           && ProphecyDimension.IsValidValue(dim, val)
                           && ProphecyDimension.Matches(card, dim, val);
                ResolveAsync(p, hit, e.PlayedCard).Forget();
            }
        }

        private static void OnTurnEnd(TurnEndEvent e)
        {
            // 对手回合结束仍未验证（整回合没出牌）→ 未命中走 else
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                var p = _pending[i];
                if (!p.Armed || p.Declarer == null || e.TurnPlayer != p.Declarer.Opponent) continue;
                _pending.RemoveAt(i);
                ResolveAsync(p, false, null).Forget();
            }
        }

        /// <summary>验证结算：公开押注与结果，然后执行对应分支的奖励原子</summary>
        private static async UniTask ResolveAsync(PendingProphecy p, bool hit, Entity verifiedCard)
        {
            var resolved = new ProphecyResolvedEvent
            {
                Declarer = p.Declarer,
                Declaration = p.Declaration,
                Hit = hit,
                VerifiedCard = verifiedCard,
            };
            if (GameCore.Instance != null) GameCore.Instance.PublishEvent(resolved);
            else EventManager.Instance.Publish(resolved);

            var rewards = hit ? p.Then : p.Else;
            if (rewards == null || rewards.Count == 0) return;

            var context = new EffectExecutionContext
            {
                Source = p.Source,
                Controller = p.Declarer,
                Targets = verifiedCard != null ? new List<Entity> { verifiedCard } : new List<Entity>(),
                ZoneManager = p.ZoneManager,
                ElementPool = p.ElementPool,
            };

            foreach (var atom in rewards)
                await EffectHandlerRegistry.ExecuteEffectAsync(atom, context);
        }
    }
}
