using System;
using System.Collections.Generic;
using System.Text;
using CardCore.Attribute;

namespace CardCore.Damage
{
    // =====================================================================================
    // Chain 解析器（2026-11-09 草案 §7.1）：DamagePipeline + DamageAftermath + DamageChain。
    // 默认策略=设计裁决（新卡 UntilDeath + Life(1) 血债；OneBounce 只是旧卡兼容档）。
    // 协作桥（§5.6）内嵌于此：SBA→GameCore.SBAEngine.ExecuteAll；生命支付→LifeLossHandler
    // 同口径（单位直扣+生命锁+归零 TryKill；角色扣上限）；日志→GameActions.Crumb 面包屑。
    // =====================================================================================

    public sealed class ChainDamageResolver : IDamageResolver
    {
        public static readonly ChainDamageResolver Instance = new ChainDamageResolver();

        private readonly DamagePipeline _pipeline = DamagePipelineFactory.CreateDefault();
        private readonly IAftermathEngine _aftermath = new AftermathEngine(DamageAftermathRules.CreateDefault());
        private readonly IChainSba _sba = new SbaBridge();
        private readonly IChainStateHasher _hasher = new ChainStateHasherBridge();
        private readonly IChainLog _log = new ChainLogBridge();
        private readonly ILifeLossService _lifeLoss = new LifeLossBridge();

        private ChainDamageResolver() { }

        /// <summary>新卡默认策略（设计裁决 2026-11-09）：UntilDeath + Life(1)——每次报复后付
        /// 1 点血债（吸血补不回），血量成为势函数，链必然有限。</summary>
        public static ChainPolicy DefaultPolicy()
            => new ChainPolicy
            {
                Loop = LoopPolicy.UntilDeath,
                Cost = LoopCost.Life(1),
                AllowRevisit = true,
                StopOnStateRepeat = true,
                MaxEvents = 256,
                MaxDepth = 64,
            };

        /// <summary>旧卡兼容档：与 Legacy 行为对齐（报复至多反弹一次、不付血债），golden 对拍用。</summary>
        public static ChainPolicy OneBouncePolicy()
            => new ChainPolicy
            {
                Loop = LoopPolicy.OneBounce,
                Cost = LoopCost.None,
                AllowRevisit = true,
                StopOnStateRepeat = false, // 单弹链事件极少，不开检测
                MaxEvents = 256,
                MaxDepth = 64,
            };

        /// <summary>传播边策略（§5.2 ChainBudget 档）：只靠 Visited+量递减收口；
        /// 边链无根参与者（Root=null），状态哈希恒常——必须关 StateRepeat。</summary>
        public static ChainPolicy EdgePolicy()
            => new ChainPolicy
            {
                Loop = LoopPolicy.ChainBudget,
                Cost = LoopCost.None,
                AllowRevisit = false,
                StopOnStateRepeat = false,
                MaxEvents = 256,
                MaxDepth = 64,
            };

        public DamageResult Resolve(DamageRequest request)
            => Resolve(request, DefaultPolicy());

        public DamageResult Resolve(DamageRequest request, ChainPolicy policy)
        {
            var chain = new DamageChain(_pipeline, _aftermath, _sba, _hasher, _log, _lifeLoss);
            var outcome = chain.Run(request, policy);
            var result = chain.Context != null ? chain.Context.RootResult : null;
            if (result == null)
                result = new DamageResult { Fizzled = true };
            result.Outcome = outcome;
            return result;
        }

        // ============================== 传播边（§8，Phase 5） ==============================
        // 由 CombatSystem.ResolvePair 的碾压/溅射段在**链路由时**调用（IsChainRouted：
        // ChainOptIn 显式接线才为真——默认 Legacy 走原内联段，2026-11-10 回正裁定）。
        // 边实现与内联段逐行对拍；逐目标「入队→排干」保持旧 DFS 时序：溅射受害者反噬
        // 致死攻击者时，下一跳选池必须看见死亡（BFS 会多溅一刀）；链上 Visited 承接
        // 旧 splashed 局部集合语义。

        /// <summary>碾压边：主战斗伤害 → 宣言目标左右邻格同侧占用者（生物/结界/角色格直伤），
        /// 量=已锁攻击力；Visited 每目标每链一次。邻格快照由调用方在伤害结算前物化传入。</summary>
        public void RunOverwhelmEdge(Entity attacker, Entity pivot, List<Entity> flankSnapshot, Player flankSide, int lockedPower)
        {
            if (attacker == null || !attacker.IsAlive || flankSnapshot == null || flankSide == null) return;

            var chain = new DamageChain(_pipeline, _aftermath, _sba, _hasher, _log, _lifeLoss);
            chain.BeginEdges(EdgePolicy());
            foreach (var adjacent in flankSnapshot)
            {
                if (chain.Terminated) break;
                if (adjacent == null || adjacent == attacker || ReferenceEquals(adjacent, pivot) || !adjacent.IsAlive)
                    continue; // 枢轴经正常步结算不重复吃溅射；攻击者不误伤
                if (adjacent is Card adjCard)
                {
                    if (adjCard.GetController() != flankSide)
                        continue; // 只打原目标同侧（防守方）占用者
                }
                else if (!(adjacent is Player adjRole && adjRole == flankSide))
                {
                    continue; // 邻格=防守方角色格才直伤角色（同侧兜底判），其余侧别跳过
                }
                if (!chain.Context.TryVisit(adjacent, KeywordRules.Overwhelm)) continue; // 每目标每链一次
                chain.Enqueue(MakeEdgeEvent(attacker, adjacent, lockedPower, DamageTags.Overwhelm, KeywordRules.Overwhelm));
                chain.Drain(); // DFS：本邻格命中及其反噬/荆棘全部收束后再打下一格
            }
        }

        /// <summary>溅射边：溢出递减连锁——每跳溢出量=溅射量−受害者溅前生命快照（预测口径：
        /// 护甲圣盾吸收仍算溢出），量只减不增、Visited 不复选（旧 splashed 集合语义），必然收敛。
        /// 池=防守方存活生物（含主目标侧）随机选靶（GameRng 同序）。</summary>
        public void RunSplashEdge(Entity attacker, Card mainTarget, int lockedPower, int mainLifeSnapshot)
        {
            if (attacker == null || !attacker.IsAlive || mainTarget == null) return;

            var chain = new DamageChain(_pipeline, _aftermath, _sba, _hasher, _log, _lifeLoss);
            chain.BeginEdges(EdgePolicy());
            chain.Context.TryVisit(mainTarget, KeywordRules.Splash); // 主目标已吃主伤害，不复选（含被挡幸存）
            int overflow = lockedPower - mainLifeSnapshot;
            while (overflow > 0 && attacker.IsAlive && !chain.Terminated)
            {
                var splashOwner = mainTarget.GetController();
                var pool = new List<Card>();
                var defenderField = GameCore.Instance?.ZoneManager?.GetCards(splashOwner, Zone.Battlefield);
                if (defenderField != null)
                    foreach (var c in defenderField)
                        if (c != null && c.IsAlive && !chain.Context.IsVisited(c, KeywordRules.Splash)
                            && c != attacker && c.IsLivingUnit())
                            pool.Add(c);
                if (pool.Count == 0) break;
                var victim = pool[GameRng.Next(0, pool.Count)];
                if (!chain.Context.TryVisit(victim, KeywordRules.Splash)) continue; // 双保险
                int victimLife = victim.GetLife(); // 溅前生命快照，用于算下一跳溢出
                chain.Enqueue(MakeEdgeEvent(attacker, victim, overflow, DamageTags.Splash, KeywordRules.Splash));
                chain.Drain(); // DFS：本跳及其反噬收束后再选下一靶（死亡对池可见）
                overflow -= victimLife;
            }
        }

        private static FollowUpEvent MakeEdgeEvent(Entity attacker, Entity victim, int amount, DamageTags tag, string keyword)
            => new FollowUpEvent
            {
                Request = new DamageRequest
                {
                    Source = attacker,
                    Target = victim,
                    Amount = amount,
                    Kind = DamageKind.Combat, // 反击/碾压溅射同为战斗伤害（暴击各发独立翻倍）
                    Tags = tag | DamageTags.Chain,
                    Origin = attacker,
                },
                Policy = EdgePolicy(),
                Owner = attacker,
                Origin = attacker,
                Keyword = keyword,
                Generation = 1,
            };

        // ------------------------------ 协作桥 ------------------------------

        private sealed class SbaBridge : IChainSba
        {
            public void Checkpoint() => GameCore.Instance?.SBAEngine?.ExecuteAll();
        }

        /// <summary>生命支付桥：LifeLossHandler 同口径——单位 _life 直扣+生命锁钳+归零 TryKill(LifePayment)；
        /// 角色 DecreaseMaxHealth（流失定案：角色扣上限）+生命锁钳；死亡交 SBA checkpoint。</summary>
        private sealed class LifeLossBridge : ILifeLossService
        {
            public int Apply(Entity target, int amount, DeathCause cause, Entity source)
            {
                if (target == null || amount <= 0) return 0;

                if (target is Card card)
                {
                    int before = card._life;
                    card._life -= amount;
                    int locked = ProtectionRules.ClampLife(card, card._life);
                    if (locked != card._life)
                    {
                        card._life = locked;
                        EventManager.Instance.Publish(new KeywordAppliedEvent
                        {
                            Target = card,
                            Keyword = "延时保护",
                            Detail = $"生命锁：{EffectText.Name(card)} 生命值锁定不低于 1",
                            Source = source,
                        });
                    }
                    int paid = Math.Max(0, before - card._life);
                    if (card._life <= 0)
                    {
                        card._life = 0;
                        DeathRules.TryKill(card, cause, source, GameCore.Instance?.ZoneManager);
                    }
                    return paid;
                }

                if (target is Player player)
                {
                    int before = player.Life;
                    player.DecreaseMaxHealth(amount);
                    int locked = ProtectionRules.ClampLife(player, player.Life);
                    if (locked != player.Life)
                    {
                        player.Life = locked;
                        EventManager.Instance.Publish(new KeywordAppliedEvent
                        {
                            Target = player,
                            Keyword = "延时保护",
                            Detail = $"生命锁：{EffectText.Name(player)} 生命值锁定不低于 1",
                            Source = source,
                        });
                    }
                    return Math.Max(0, before - player.Life);
                }

                return 0;
            }
        }

        /// <summary>状态哈希桥（§5.6 v1 口径）：只哈希根事件双方的生命/指示物/区域/关键词
        ///（循环参与者即根 Source/Target；第三方不随链变化）。指示物取循环相关固定清单，
        /// 不枚举注册表（确定性顺序；Counter/Durability 等无关项不进哈希）。</summary>
        private sealed class ChainStateHasherBridge : IChainStateHasher
        {
            private static readonly string[] LoopCounters =
            {
                CounterRules.RetaliationCounter,
                CounterRules.VulnerableCounter,
                CounterRules.ToughnessCounter,
                CounterRules.DivineShieldCounter,
                KeywordRules.ArmorCounter,
                CounterRules.ToxinCounter,
                CounterRules.SpellbanCounter,
                CounterRules.HalveShieldCounter,
                CounterRules.NullShieldCounter,
                CounterRules.LifeFloorCounter,
                CounterRules.RebornCounter,
            };

            public string Hash(ChainContext ctx)
            {
                var sb = new StringBuilder(64);
                Append(sb, ctx.Root != null ? ctx.Root.Source : null);
                Append(sb, ctx.Root != null ? ctx.Root.Target : null);
                return sb.ToString();
            }

            private static void Append(StringBuilder sb, Entity e)
            {
                if (e == null) { sb.Append("n;"); return; }
                if (e is Card c)
                {
                    sb.Append("C").Append(c.IsAlive ? 1 : 0).Append('|').Append((int)c.GetZone())
                      .Append('|').Append(c._life);
                    foreach (var kw in c._keywords) sb.Append('k').Append(kw);
                    sb.Append('|');
                    foreach (var id in LoopCounters) sb.Append(id).Append('=').Append(c.GetEffectiveCounterCount(id)).Append(',');
                    sb.Append(';');
                }
                else if (e is Player p)
                {
                    sb.Append("P").Append(p.IsAlive ? 1 : 0).Append('|').Append(p.Life);
                    foreach (var id in LoopCounters) sb.Append(id).Append('=').Append(p.GetEffectiveCounterCount(id)).Append(',');
                    sb.Append(';');
                }
                else sb.Append("E;");
            }
        }

        /// <summary>日志桥：面包屑（TideSim 兼容——不直接 Debug.Log）+ 链上下文留痕。</summary>
        private sealed class ChainLogBridge : IChainLog
        {
            public void BudgetExhausted(ChainContext ctx)
            {
                ctx.Log.Add($"[Chain] BudgetExhausted events={ctx.EventCount} depth={ctx.Depth}");
                GameActions.Crumb($"[Chain] BudgetExhausted events={ctx.EventCount} depth={ctx.Depth}");
            }

            public void DepthExhausted(ChainContext ctx, FollowUpEvent ev)
            {
                ctx.Log.Add($"[Chain] DepthExhausted gen={ev.Generation} kw={ev.Keyword}");
                GameActions.Crumb($"[Chain] DepthExhausted gen={ev.Generation} kw={ev.Keyword}");
            }

            public void Fizzled(ChainContext ctx, FollowUpEvent ev)
            {
                ctx.Log.Add($"[Chain] Fizzled gen={ev.Generation} kw={ev.Keyword}");
            }

            public void StateRepeat(ChainContext ctx)
            {
                ctx.Log.Add($"[Chain] StateRepeat events={ctx.EventCount} depth={ctx.Depth}");
                GameActions.Crumb($"[Chain] StateRepeat events={ctx.EventCount} depth={ctx.Depth}");
            }
        }
    }
}
