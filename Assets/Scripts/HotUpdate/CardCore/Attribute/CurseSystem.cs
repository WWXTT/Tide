using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using CardCore.Attribute;

namespace CardCore
{
    /// <summary>
    /// 诅咒系统（信息轴，2026-10-02 定案；订阅式引擎，仿 BranchEngines）：
    /// AddCurse 原子为对手的卡附加「诅咒」指示物（CurseCounter，Permanent）并在此登记载荷；
    /// 对手抽到该卡时（CardDrawEvent——ZoneManagerExtensions.DrawCard 统一发布口）自动执行
    /// 载荷的分支效果并消层（一次性，用户定案）。
    ///
    /// - 载荷 = 开放式分支效果：Effects.json 条目（AddCurse.str 引用 id）的 Steps——
    ///   Atomic 原子 + 条件分支（Then/Else，条件走 BranchConditionEvaluator 局面状态族门；
    ///   抽牌时点无上游产出，产出族门恒否——设计载荷时应使用局面状态族条件）；
    ///   Steps 空 时退化为扁平 AtomicEffects（与执行器同款向后兼容口径）。
    /// - 执行上下文：Controller=施诅方（载荷的相对域以施诅方视角解析——「敌方」=被诅玩家），
    ///   Targets/CastCard=被抽到的卡，TriggeringEvent=CardDrawEvent；原子各自解析目标（免编排路径）。
    /// - 消耗前置（先消层摘载荷、再结算）：现网原子抽牌路径 CardDrawEvent 双发
    ///   （DrawCardHandler 与 ZoneManagerExtensions.DrawCard 各发一次）——第一次已消耗、第二次自然空转，
    ///   亦防载荷再抽牌的重入双触发。
    /// - 净化（PurgeAll 清 Curse 指示物）后残留的载荷注册为死重：抽到时计数=0 直接空转，局重置统一回收。
    /// - 组合根 EnsureRegistered/Reset（GameCore.Reset，幂等；跨局不残留）。
    /// </summary>
    public static class CurseSystem
    {
        private class CursePayload
        {
            public string EffectId;   // 载荷效果 id（Effects.json 条目）
            public Player Caster;     // 施诅方（载荷控制者）
            public Card SourceCard;   // 施咒来源卡（可 null——归因用，卡可能已离场）
        }

        private static bool _registered;
        private static readonly Dictionary<Card, List<CursePayload>> _curses =
            new Dictionary<Card, List<CursePayload>>();
        // 载荷效果定义缓存（含负缓存——缺失告警一次）
        private static readonly Dictionary<string, EffectDefinition> _defCache =
            new Dictionary<string, EffectDefinition>();

        public static void EnsureRegistered()
        {
            if (_registered) return;
            _registered = true;
            EventManager.Instance.Subscribe<CardDrawEvent>(OnCardDrawn);
        }

        /// <summary>登记一条诅咒（AddCurseHandler 调用）：目标卡 + 载荷 + 施诅方。</summary>
        public static void Attach(Card target, string effectId, Player caster, Card sourceCard)
        {
            if (target == null || string.IsNullOrEmpty(effectId)) return;
            if (!_curses.TryGetValue(target, out var list))
            {
                list = new List<CursePayload>();
                _curses[target] = list;
            }
            list.Add(new CursePayload { EffectId = effectId, Caster = caster, SourceCard = sourceCard });
        }

        /// <summary>查询卡上挂着的诅咒载荷（UI 展示用：载荷效果 id + 施诅方）。</summary>
        public static IReadOnlyList<(string effectId, Player caster)> GetCurses(Card card)
        {
            if (card == null || !_curses.TryGetValue(card, out var list) || list == null)
                return Array.Empty<(string, Player)>();
            return list.Select(p => (p.EffectId, p.Caster)).ToArray();
        }

        /// <summary>局重置（GameCore.Reset）：载荷注册表与定义缓存跨局不残留。</summary>
        public static void Reset()
        {
            _curses.Clear();
            _defCache.Clear();
        }

        private static void OnCardDrawn(CardDrawEvent e)
        {
            var card = e?.DrawnCard;
            if (card == null) return;
            int layers = card.GetCounterCount(Attribute.CounterRules.CurseCounter);
            if (layers <= 0) return;
            if (!_curses.TryGetValue(card, out var payloads) || payloads == null || payloads.Count == 0) return;

            // 快照 → 先消耗（消全部层+摘全部载荷——一次抽牌一次结算；层多载荷少时多余层一并作废，
            // 用户定案一次性消耗，不留「回牌库再抽再触发」的残留）→ 播报 → 再结算
            var due = payloads.Take(layers).ToList();
            _curses.Remove(card);
            card.RemoveCounters(Attribute.CounterRules.CurseCounter, layers);

            EventManager.Instance.Publish(new Attribute.CounterChangedEvent
            {
                Target = card,
                CounterType = Attribute.CounterRules.CurseCounter,
                Amount = 0,
                Source = null,
            });
            EventManager.Instance.Publish(new KeywordAppliedEvent
            {
                Target = card,
                Keyword = "诅咒",
                Detail = $"诅咒发作：抽到该卡，执行诅咒分支效果（消耗 {due.Count} 层）",
            });

            var core = GameCore.Instance;
            SettleAsync(due, card, e, core).Forget();
        }

        /// <summary>逐条结算载荷（原子顺序 await——载荷内部的先后依赖成立；整体免编排后台执行）。</summary>
        private static async UniTask SettleAsync(
            List<CursePayload> due, Card drawnCard, CardDrawEvent trigger, GameCore core)
        {
            foreach (var payload in due)
                await ExecutePayloadAsync(payload, drawnCard, trigger, core);
        }

        /// <summary>执行一条诅咒载荷：按 Steps 遍历（Atomic→注册表执行；Branch→局面门评估选 Then/Else）；
        /// Steps 空退化为扁平 Effects。原子各自解析目标（BranchEngines.FireRewards 同款免编排路径）。</summary>
        private static async UniTask ExecutePayloadAsync(
            CursePayload payload, Card drawnCard, CardDrawEvent trigger, GameCore core)
        {
            var def = ResolveDef(payload.EffectId);
            if (def == null)
            {
                TideLog.Warn($"[CurseSystem] 诅咒载荷缺失: {payload.EffectId}（Effects.json 无此条，层已消耗）");
                return;
            }

            var ctx = new EffectExecutionContext
            {
                Source = (Entity)payload.SourceCard ?? payload.Caster,
                Controller = payload.Caster,
                Targets = new List<Entity> { drawnCard },
                TriggeringEvent = trigger,
                ZoneManager = core?.ZoneManager,
                ElementPool = core?.ElementPool,
                CastCard = drawnCard,
            };

            if (def.Steps != null && def.Steps.Count > 0)
            {
                foreach (var step in def.Steps)
                {
                    if (step == null) continue;
                    if (step.Kind == RuntimeStepKind.Atomic && step.Atomic != null)
                    {
                        await EffectHandlerRegistry.ExecuteEffectAsync(step.Atomic, ctx);
                    }
                    else if (step.Kind == RuntimeStepKind.Branch)
                    {
                        // 局面状态族门在此可评估（读 ctx）；产出族门无上游产出恒否（见类注）
                        bool hit = BranchConditionEvaluator.Evaluate(
                            step.ConditionId, ctx.LastOutcome, step.ConditionParam, step.ConditionStringParam, ctx);
                        if (step.Negate) hit = !hit;
                        var branch = hit ? step.Then : step.Else;
                        if (branch == null) continue;
                        foreach (var atom in branch)
                            if (atom != null)
                                await EffectHandlerRegistry.ExecuteEffectAsync(atom, ctx);
                    }
                    else if (step.Kind == RuntimeStepKind.Choice)
                    {
                        TideLog.Warn($"[CurseSystem] 载荷 {payload.EffectId} 含抉择步骤——抽牌时点自动执行不支持玩家选择，跳过");
                    }
                }
                return;
            }

            foreach (var atom in def.Effects)
                if (atom != null)
                    await EffectHandlerRegistry.ExecuteEffectAsync(atom, ctx);
        }

        /// <summary>载荷效果 id → EffectDefinition（EffectsLibrary 瘦条目 → CardEffectConverter 转换；缓存含负缓存）。</summary>
        private static EffectDefinition ResolveDef(string effectId)
        {
            if (_defCache.TryGetValue(effectId, out var cached)) return cached;
            var data = EffectsLibrary.Resolve(effectId);
            var def = data != null ? CardEffectConverter.ConvertOne(data, effectId) : null;
            _defCache[effectId] = def;
            return def;
        }
    }
}
