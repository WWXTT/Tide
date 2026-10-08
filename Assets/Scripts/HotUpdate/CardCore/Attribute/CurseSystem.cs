using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using CardCore.Attribute;

namespace CardCore
{
    /// <summary>
    /// 诅咒系统（信息轴，2026-10-02 定案；订阅式引擎，仿 BranchEngines）：
    /// 为牌库中的卡附加「诅咒/祝福」指示物（Exception 生效自减档——换区不清，活过牌库→手牌）并在此登记载荷；
    /// 该卡被抽到时（CardDrawEvent——ZoneManagerExtensions.DrawCard 统一发布口）自动执行
    /// 载荷的分支效果并消层（一次性，用户定案）。
    ///
    /// - 2026-10-08 引擎主干化：主投放口=自由分支·引擎主干行（附加诅咒→对手牌库/附加祝福→自己牌库，
    ///   BranchEngines.OnCardCastResolved 施放结算时部署，Then=引擎奖励原子列——结算走
    ///   EffectExecutor.ExecuteThenRewardsAsync 弹选口径，与六引擎 FireRewards 同款）；
    ///   指示物（诅咒/祝福）归系统，不设玩家表行。旧 AddCurse 原子路径（表行已退役）保留代码侧
    ///   兼容手写数据：①inline 原子列（CurseOnDraw 门 Then ≤2 费预算）；②Effects.json 条目引用（str）。
    /// - 执行上下文：Controller=施加方（载荷的相对域以施加方视角解析——「敌方」=被诅玩家）；
    ///   引擎载荷 Source=来源卡（弹选/免编排）；legacy 载荷 Targets/CastCard=被抽到的卡，
    ///   TriggeringEvent=CardDrawEvent，原子各自解析目标。
    /// - 消耗前置（先消层摘载荷、再结算）：现网原子抽牌路径 CardDrawEvent 双发
    ///   （DrawCardHandler 与 ZoneManagerExtensions.DrawCard 各发一次）——第一次已消耗、第二次自然空转，
    ///   亦防载荷再抽牌的重入双触发。诅咒/祝福各自独立消耗（同一卡可同时持两类载荷，互不代扣）。
    /// - 净化（PurgeAll 清指示物）后残留的载荷注册为死重：抽到时计数=0 直接空转，局重置统一回收。
    /// - 组合根 EnsureRegistered/Reset（GameCore.Reset，幂等；跨局不残留）。
    /// </summary>
    public static class CurseSystem
    {
        private class CursePayload
        {
            public string CounterId;  // 载荷归属指示物（CurseCounter/BlessingCounter——2026-10-08 祝福并入）
            public string EffectId;   // 载荷效果 id（Effects.json 条目；inline/引擎形态为 null）
            public List<AtomicEffectInstance> InlineSteps; // 载荷原子列（legacy CurseOnDraw 门 Then / 引擎 Then 共用载体）
            public bool FromEngine;   // 引擎主干行载荷（2026-10-08）——结算走 ExecuteThenRewardsAsync 弹选口径
            public Player Caster;     // 施加方（载荷控制者）
            public Card SourceCard;   // 来源卡（可 null——归因用，卡可能已离场）
        }

        private static bool _registered;
        private static readonly Dictionary<Card, List<CursePayload>> _payloads =
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

        /// <summary>登记一条诅咒（legacy AddCurseHandler 调用，表行已退役——手写数据兼容）：
        /// 目标卡 + 载荷效果 id + 施诅方。</summary>
        public static void Attach(Card target, string effectId, Player caster, Card sourceCard)
        {
            if (target == null || string.IsNullOrEmpty(effectId)) return;
            AddPayload(target, new CursePayload
            {
                CounterId = Attribute.CounterRules.CurseCounter,
                EffectId = effectId,
                Caster = caster,
                SourceCard = sourceCard,
            });
        }

        /// <summary>登记一条 inline 诅咒（legacy 2026-10-05 合成器路径）：载荷=CurseOnDraw 门 Then 原子列
        ///（≤2 费预算由合成器校验）——每次 Attach 各自携带，天然"不同诅咒触发不同效果"。</summary>
        public static void Attach(Card target, List<AtomicEffectInstance> inlineSteps, Player caster, Card sourceCard)
        {
            if (target == null || inlineSteps == null || inlineSteps.Count == 0) return;
            AddPayload(target, new CursePayload
            {
                CounterId = Attribute.CounterRules.CurseCounter,
                InlineSteps = inlineSteps,
                Caster = caster,
                SourceCard = sourceCard,
            });
        }

        /// <summary>登记一条引擎主干行载荷（2026-10-08 自由分支化主投放口）：
        /// BranchEngines.OnCardCastResolved 部署调用——counterId 区分诅咒/祝福，
        /// then=引擎 Then 奖励原子列（发作时走 ExecuteThenRewardsAsync 弹选口径）。Then 空=只挂层不登记。</summary>
        public static void AttachEngine(Card target, string counterId,
            List<AtomicEffectInstance> then, Player caster, Card sourceCard)
        {
            if (target == null || then == null || then.Count == 0) return;
            AddPayload(target, new CursePayload
            {
                CounterId = counterId,
                InlineSteps = then,
                FromEngine = true,
                Caster = caster,
                SourceCard = sourceCard,
            });
        }

        private static void AddPayload(Card target, CursePayload payload)
        {
            if (target == null) return;
            if (!_payloads.TryGetValue(target, out var list))
            {
                list = new List<CursePayload>();
                _payloads[target] = list;
            }
            list.Add(payload);
        }

        /// <summary>查询卡上挂着的载荷（UI 展示用：载荷效果 id + 施加方——引擎/inline 形态 effectId=null）。</summary>
        public static IReadOnlyList<(string effectId, Player caster)> GetCurses(Card card)
        {
            if (card == null || !_payloads.TryGetValue(card, out var list) || list == null)
                return Array.Empty<(string, Player)>();
            return list.Select(p => (p.EffectId, p.Caster)).ToArray();
        }

        /// <summary>局重置（GameCore.Reset）：载荷注册表与定义缓存跨局不残留。</summary>
        public static void Reset()
        {
            _payloads.Clear();
            _defCache.Clear();
        }

        private static void OnCardDrawn(CardDrawEvent e)
        {
            var card = e?.DrawnCard;
            if (card == null) return;
            // 诅咒/祝福各自独立结算（同一卡两类并存互不代扣；先后=登记口径：诅咒先）
            ProcessOnDraw(card, Attribute.CounterRules.CurseCounter, "诅咒", e);
            ProcessOnDraw(card, Attribute.CounterRules.BlessingCounter, "祝福", e);
        }

        /// <summary>单指示物发作：快照 → 先消耗（消全部层+摘同类载荷——一次抽牌一次结算；
        /// 层多载荷少时多余层一并作废，用户定案一次性消耗，不留「回牌库再抽再触发」的残留；
        /// 有层无载荷=异态不白吃层）→ 播报 → 再结算。</summary>
        private static void ProcessOnDraw(Card card, string counterId, string keyword, CardDrawEvent e)
        {
            int layers = card.GetCounterCount(counterId);
            if (layers <= 0) return;
            if (!_payloads.TryGetValue(card, out var all) || all == null || all.Count == 0) return;

            var due = all.Where(p => p.CounterId == counterId).Take(layers).ToList();
            if (due.Count == 0) return;
            foreach (var p in due) all.Remove(p);
            if (all.Count == 0) _payloads.Remove(card);
            card.RemoveCounters(counterId, layers);

            EventManager.Instance.Publish(new Attribute.CounterChangedEvent
            {
                Target = card,
                CounterType = counterId,
                Amount = 0,
                Source = null,
            });
            EventManager.Instance.Publish(new KeywordAppliedEvent
            {
                Target = card,
                Keyword = keyword,
                Detail = $"{keyword}发作：抽到该卡，执行{keyword}分支效果（消耗 {due.Count} 层）",
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

        /// <summary>执行一条载荷：引擎形态（2026-10-08）=Then 奖励原子列走 ExecuteThenRewardsAsync
        /// 弹选口径（BranchEngines.FireRewards 同款——AI/无头自动选首；await 进结算链保同步续行可断言）；
        /// legacy inline 形态=原子列逐个执行（CurseOnDraw 门 Then）；
        /// legacy 引用形态=按 Steps 遍历（Atomic→注册表执行；Branch→局面门评估选 Then/Else），
        /// Steps 空退化为扁平 Effects。legacy 原子各自解析目标（免编排路径）。</summary>
        private static async UniTask ExecutePayloadAsync(
            CursePayload payload, Card drawnCard, CardDrawEvent trigger, GameCore core)
        {
            // 引擎主干行载荷（2026-10-08）：Then=奖励原子列——per 原子弹选目标（无头自动选首）
            if (payload.FromEngine)
            {
                var engCtx = new EffectExecutionContext
                {
                    Source = (Entity)payload.SourceCard ?? payload.Caster,
                    Controller = payload.Caster,
                    ZoneManager = core?.ZoneManager,
                    ElementPool = core?.ElementPool,
                    ModeIndex = -1,
                };
                await EffectExecutor.ExecuteThenRewardsAsync(payload.InlineSteps, engCtx, null);
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

            // inline 载荷（legacy 2026-10-05）：合成器门的 Then 原子列——逐个执行（原子各自解析目标）
            if (payload.InlineSteps != null)
            {
                foreach (var atom in payload.InlineSteps)
                    if (atom != null)
                        await EffectHandlerRegistry.ExecuteEffectAsync(atom, ctx);
                return;
            }

            var def = ResolveDef(payload.EffectId);
            if (def == null)
            {
                TideLog.Warn($"[CurseSystem] 诅咒载荷缺失: {payload.EffectId}（Effects.json 无此条，层已消耗）");
                return;
            }

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
