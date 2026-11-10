using System;
using System.Collections.Generic;
using CardCore.Attribute;

namespace CardCore.Damage
{
    // =====================================================================================
    // 循环链（2026-11-09《伤害管线与循环链接口草案》Phase 4：上 Chain）。
    //
    // 定位（§5）：多事件排队 + SBA checkpoint + LoopPolicy/LoopCost 收敛 + Visited/StateRepeat。
    // 不做伤害数学（DamagePipeline）、不做单事件分层；后续伤害全部经 FollowUpEvent 入队，
    // 泵内逐事件：结算 → Aftermath → 支付循环代价 → SBA checkpoint → 终局/严格循环检测。
    //
    // AOT 注记：草案 §5.1 的 Visited 用 (Entity,string) 值元组 HashSet——会新增值类型泛型
    // 实例化（HybridCLR 基线外），此处改为 Dictionary&lt;关键词, HashSet&lt;Entity&gt;&gt;（全引用型
    // 泛型，语义等价）；_seenHashes 同理用 HashSet&lt;string&gt;。
    // =====================================================================================

    public enum ChainOutcome
    {
        Resolved,       // 队列排干，正常收束
        Fizzled,        // 根事件目标非法
        Lethal,         // 根事件双方全灭
        BudgetExhausted, // MaxEvents/MaxDepth 断言命中（记日志，不静默）
        StateRepeat,    // 状态哈希重复（严格循环检测）
        Stalemate,      // 预留（v1 未用）
    }

    /// <summary>循环策略（§5.2）。OneBounce=旧卡兼容档（等价 _retaliateBounceGuard）；
    /// UntilDeath=新卡默认（设计裁决 2026-11-09：靠 LoopCost 血债自然收束，不掐死互弹）。</summary>
    public enum LoopPolicy
    {
        OneBounce,
        UntilDeath,
        Halve,
        ConsumeCounter,
        ChainBudget,
    }

    public enum LoopCostKind { None, Life, Counter, Durability }

    /// <summary>循环代价（§5.2）。v1 只实现 None/Life（Counter/Durability 预留位——接入时
    /// 须改为伤害前支付，不能沿用 Life 的后付）。</summary>
    public readonly struct LoopCost
    {
        public readonly LoopCostKind Kind;
        public readonly int Amount;
        public readonly string CounterId;

        public LoopCost(LoopCostKind kind, int amount, string counterId = null)
        {
            Kind = kind;
            Amount = amount;
            CounterId = counterId;
        }

        public static LoopCost None => new LoopCost(LoopCostKind.None, 0);
        public static LoopCost Life(int n) => new LoopCost(LoopCostKind.Life, n);
    }

    /// <summary>链策略：Loop+Cost 是玩法；MaxEvents/MaxDepth 只是安全断言（命中即记日志并结束）。
    /// 替代/转移深度不在此处（分归 ReplaceStage.MaxDepth / RedirectPolicy.MaxDepth，§5.2 注）。</summary>
    public sealed class ChainPolicy
    {
        public LoopPolicy Loop = LoopPolicy.UntilDeath;
        public LoopCost Cost = LoopCost.None;
        public int MaxEvents = 256;
        public int MaxDepth = 64;
        public bool AllowRevisit = false;     // 传播类默认 false；报复设 true（不靠 Visited）
        public bool StopOnStateRepeat = true; // 严格循环检测兜底
    }

    /// <summary>链上事件（§5.3）。根事件 Keyword=Root、Generation=0；第一代报复=1，互弹=2+。</summary>
    public sealed class FollowUpEvent
    {
        public const string RootKeyword = "Root";

        public DamageRequest Request;
        public ChainPolicy Policy;
        public Entity Owner;      // 支付 LoopCost 的实体
        public Entity Origin;     // 关键词持有者
        public string Keyword;    // Root / Retaliate / RetaliationCounter / Splash / Overwhelm …
        public int Generation;
    }

    /// <summary>链上下文（§5.1）。Visited 只给传播类关键词用；报复/荆棘靠 LoopCost/衰减收敛。</summary>
    public sealed class ChainContext
    {
        public DamageRequest Root;
        public ChainPolicy Policy;
        public int EventCount;
        public int Depth;
        public DamageResult RootResult; // 根事件的结算结果（Resolver 返回用）
        public DamageChain Chain;       // Aftermath 出口回指

        public readonly Dictionary<string, HashSet<Entity>> Visited = new Dictionary<string, HashSet<Entity>>();
        public readonly Dictionary<Entity, int> StartLife = new Dictionary<Entity, int>();
        public readonly Dictionary<Entity, int> LifePaid = new Dictionary<Entity, int>();
        public readonly List<string> Log = new List<string>();
        public ChainOutcome Outcome;

        private readonly HashSet<string> _seenHashes = new HashSet<string>();

        public bool TryVisit(Entity target, string keyword)
        {
            if (Policy.AllowRevisit) return true;
            if (!Visited.TryGetValue(keyword, out var set))
            {
                set = new HashSet<Entity>();
                Visited[keyword] = set;
            }
            return set.Add(target);
        }

        /// <summary>已访问查询（溅射选池排除已溅者——旧 splashed 局部集合的同语义）。</summary>
        public bool IsVisited(Entity target, string keyword)
            => !Policy.AllowRevisit
               && Visited.TryGetValue(keyword, out var set)
               && set.Contains(target);

        /// <summary>v1 只看根事件 Source/Target（§5.1）；边链无根参与者（Root=null）恒 false。</summary>
        public bool IsAllDead()
        {
            if (Root == null) return false;
            bool sourceDead = Root.Source == null || !Root.Source.IsAlive;
            bool targetDead = Root.Target == null || !Root.Target.IsAlive;
            return sourceDead && targetDead;
        }

        public bool HasStateRepeated(IChainStateHasher hasher)
            => !_seenHashes.Add(hasher.Hash(this));
    }

    // ============================== 协作接口（§5.6） ==============================

    /// <summary>SBA checkpoint：死亡/送墓/亡语（实现包 GameCore.SBAEngine.ExecuteAll）。</summary>
    public interface IChainSba
    {
        void Checkpoint();
    }

    /// <summary>状态哈希（§5.6）：v1 只哈希生命/指示物/区域/关键词，不含 RNG/日志/队列/LifePaid
    ///（血债是日志量不是局面量，计入会让 StateRepeat 永不命中）。</summary>
    public interface IChainStateHasher
    {
        string Hash(ChainContext ctx);
    }

    public interface IChainLog
    {
        void BudgetExhausted(ChainContext ctx);
        void DepthExhausted(ChainContext ctx, FollowUpEvent ev);
        void Fizzled(ChainContext ctx, FollowUpEvent ev);
        void StateRepeat(ChainContext ctx);
    }

    /// <summary>生命支付/流失（§3.4）：非伤害路径——不进 DamagePipeline，
    /// 不被吸血/系命/圣盾/护甲/坚韧响应；只受生命锁/上限影响。</summary>
    public interface ILifeLossService
    {
        int Apply(Entity target, int amount, DeathCause cause, Entity source);
    }

    // ============================== 泵 ==============================

    /// <summary>循环链（每次 Run 用新实例——队列与上下文是运行态，不共享）。</summary>
    public sealed class DamageChain
    {
        private readonly Queue<FollowUpEvent> _queue = new Queue<FollowUpEvent>();
        private readonly DamagePipeline _pipeline;
        private readonly IAftermathEngine _aftermath;
        private readonly IChainSba _sba;
        private readonly IChainStateHasher _hasher;
        private readonly IChainLog _log;
        private readonly ILifeLossService _lifeLoss;
        private ChainContext _current;
        private bool _terminated;

        public DamageChain(DamagePipeline pipeline, IAftermathEngine aftermath, IChainSba sba,
            IChainStateHasher hasher, IChainLog log, ILifeLossService lifeLoss)
        {
            _pipeline = pipeline;
            _aftermath = aftermath;
            _sba = sba;
            _hasher = hasher;
            _log = log;
            _lifeLoss = lifeLoss;
        }

        public ChainContext Context => _current;

        /// <summary>当前根策略（Aftermath 规则继承 Loop/Cost 用，§6.1.1）。</summary>
        public ChainPolicy Policy => _current != null ? _current.Policy : null;

        public ChainOutcome Run(DamageRequest root, ChainPolicy policy)
        {
            _queue.Clear();
            _terminated = false;
            var ctx = new ChainContext { Root = root, Policy = policy, Chain = this };
            if (root != null)
            {
                if (root.Source != null) ctx.StartLife[root.Source] = LifeOf(root.Source);
                if (root.Target != null) ctx.StartLife[root.Target] = LifeOf(root.Target);
            }
            _current = ctx;

            _queue.Enqueue(new FollowUpEvent
            {
                Request = root,
                Policy = policy,
                Owner = root != null ? root.Source : null,
                Origin = root != null ? root.Source : null,
                Keyword = FollowUpEvent.RootKeyword,
                Generation = 0,
            });
            return Pump(ctx);
        }

        /// <summary>入队门禁（§5.4）：OneBounce 兼容档=拦 Generation&gt;1 的报复，
        /// 等价 Legacy _retaliateBounceGuard（每次原始伤害至多反弹一次）。</summary>
        public bool CanEnqueue(FollowUpEvent ev)
        {
            if (ev.Policy != null
                && ev.Policy.Loop == LoopPolicy.OneBounce
                && ev.Keyword == KeywordRules.Retaliate
                && ev.Generation > 1)
                return false;
            return true;
        }

        /// <summary>Aftermath 出口：先过门禁再入队；返回 false=被拦（调用方不发事件）。</summary>
        public bool Enqueue(FollowUpEvent ev)
        {
            if (ev == null || !CanEnqueue(ev)) return false;
            _queue.Enqueue(ev);
            return true;
        }

        private ChainOutcome Pump(ChainContext ctx)
        {
            while (StepCore(ctx) == StepResult.Continue) { }
            return ctx.Outcome; // 排干=Resolved（默认值）；终局已在 StepCore 置位
        }

        private enum StepResult { Continue, Drained, Terminal }

        /// <summary>单事件一轮：预算 → 出队 → 深度 → 结算 → 后效 → 代价 → SBA → 终局/循环检测。</summary>
        private StepResult StepCore(ChainContext ctx)
        {
            if (_queue.Count == 0) return StepResult.Drained;

            if (ctx.EventCount >= ctx.Policy.MaxEvents)
            {
                ctx.Outcome = ChainOutcome.BudgetExhausted;
                _terminated = true;
                _log.BudgetExhausted(ctx);
                return StepResult.Terminal;
            }

            var ev = _queue.Dequeue();
            if (ev.Generation > ctx.Policy.MaxDepth)
            {
                ctx.Outcome = ChainOutcome.BudgetExhausted;
                _terminated = true;
                _log.DepthExhausted(ctx, ev);
                return StepResult.Terminal;
            }

            ctx.EventCount++;
            ctx.Depth = ev.Generation;
            if (ev.Keyword == FollowUpEvent.RootKeyword)
                ctx.RootResult = new DamageResult
                {
                    Source = ev.Request != null ? ev.Request.Source : null,
                    FinalTarget = ev.Request != null ? ev.Request.Target : null,
                    Requested = ev.Request != null ? ev.Request.Amount : 0,
                    Fizzled = true,
                };

            if (!IsValid(ev))
            {
                _log.Fizzled(ctx, ev);
                return StepResult.Continue;
            }

            // 1. 结算伤害（单事件全层管线）
            var result = _pipeline.Resolve(ev.Request, ev.Generation);
            if (ev.Keyword == FollowUpEvent.RootKeyword)
                ctx.RootResult = result;

            // 2. 后效：事件、吸血、荆棘/报复入队……（规则各自门禁；不许递归 ApplyDamage）
            if (!result.Fizzled)
                _aftermath.Process(result, ctx);

            // 3. 支付循环代价（伤害与吸血之后——「本次反噬已生效」口径，§6.3）
            PayLoopCost(ev, ctx);

            // 4. SBA checkpoint：死亡/送墓/亡语（Legacy 的死亡决策时点）
            _sba.Checkpoint();

            // 5. 终局与严格循环检测
            if (ctx.IsAllDead())
            {
                ctx.Outcome = ChainOutcome.Lethal;
                _terminated = true;
                return StepResult.Terminal;
            }
            if (ctx.Policy.StopOnStateRepeat && ctx.HasStateRepeated(_hasher))
            {
                ctx.Outcome = ChainOutcome.StateRepeat;
                _terminated = true;
                _log.StateRepeat(ctx);
                return StepResult.Terminal;
            }
            return StepResult.Continue;
        }

        // ============================== 边链入口（§8 传播边） ==============================

        /// <summary>开一条无边链（Root=null——碾压/溅射等传播边在主伤害链外单开；
        /// 收敛靠 Visited+量递减，策略配 StopOnStateRepeat=false）。</summary>
        public void BeginEdges(ChainPolicy policy)
        {
            _queue.Clear();
            _terminated = false;
            _current = new ChainContext { Root = null, Policy = policy, Chain = this };
        }

        /// <summary>排干队列（结算全部已入队事件及其后续）。终局见 Terminated/Context.Outcome。</summary>
        public ChainOutcome Drain() => Pump(_current);

        /// <summary>终局标记（BudgetExhausted/Lethal/StateRepeat 后不再入队结算）。</summary>
        public bool Terminated => _terminated;

        /// <summary>只校验 Target：已入队的「濒死反咬」即使 Source 已死也允许结算；
        /// 新的后续事件由 Aftermath 的 Source.IsAlive 门禁拦截（§5.5——两件事不能混）。</summary>
        private bool IsValid(FollowUpEvent ev)
        {
            return ev.Request != null
                && ev.Request.Target != null
                && ev.Request.Target.IsAlive;
        }

        /// <summary>循环代价（§5.4）：根事件不付；已死 Owner 不付（已触发的反噬仍结算，死亡后不计费）。
        /// Life=后付（伤害→吸血→血债→SBA，濒死反咬口径）；Counter/Durability 预留位 v1 不启用。</summary>
        private bool PayLoopCost(FollowUpEvent ev, ChainContext ctx)
        {
            if (ev.Keyword == FollowUpEvent.RootKeyword
                || ev.Owner == null
                || !ev.Owner.IsAlive
                || ev.Policy == null
                || ev.Policy.Cost.Kind == LoopCostKind.None)
                return true;

            switch (ev.Policy.Cost.Kind)
            {
                case LoopCostKind.Life:
                    ctx.LifePaid[ev.Owner] = LifePaidOf(ctx, ev.Owner) + ev.Policy.Cost.Amount;
                    // 生命支付，不是伤害：不进管线、不被吸血/系命/圣盾/护甲响应
                    _lifeLoss.Apply(ev.Owner, ev.Policy.Cost.Amount, DeathCause.LifePayment, ev.Origin);
                    return ev.Owner.IsAlive;

                default:
                    // Counter/Durability 预留位（§5.2）：接入时改为伤害前支付，暂按无代价放行并记账
                    ctx.Log.Add($"[LoopCost] 预留代价类型 {ev.Policy.Cost.Kind} 未启用，按 None 放行");
                    return true;
            }
        }

        private static int LifePaidOf(ChainContext ctx, Entity owner)
            => ctx.LifePaid.TryGetValue(owner, out var paid) ? paid : 0;

        private static int LifeOf(Entity e)
        {
            if (e is Card c) return c._life;
            if (e is Player p) return p.Life;
            return 0;
        }
    }
}
