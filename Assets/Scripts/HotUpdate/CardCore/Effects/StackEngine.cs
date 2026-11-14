using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using CardCore.Attribute;
using CardCore.Attribute.Handlers;

namespace CardCore
{
    /// <summary>
    /// 栈引擎
    /// 管理效果入栈、轮询、结算
    ///
    /// 流程：
    /// 1. 条件发动（事件触发）自动入栈，每个 counter++
    /// 2. 轮询阶段：双方交替，速度发动 speed > counter 才能入栈，counter++
    /// 3. 双方 Pass → 结算（LIFO），结算中不轮询，每结算一个 counter--
    /// 4. counter 归0 → 检查延迟触发 → 有则开始新一轮
    /// </summary>
    public partial class StackEngine
    {
        private Stack<EffectInstance> _stack = new Stack<EffectInstance>();
        private SpeedCounter _speedCounter = new SpeedCounter();
        private PendingEffectQueue _pendingQueue;
        private EffectExecutor _executor;
        // 充能每回合规额台账（2026-11-05 系统动作化改版）：单卡一回合一次——宣入即记账、
        // 结算期不再记；回合开始清零（OnTurnStart）。旧口径键=充能效果Id@卡（效果台账）已随
        // 充能效果链退役，充能不再是卡上效果，改按卡实例直记。
        private readonly HashSet<Card> _chargedThisTurn = new HashSet<Card>();

        private Player _activePlayer;
        private Player _priorityHolder;
        private bool _waitingForPlayer = false;
        private int _consecutivePassCount = 0;
        private PhaseType _currentPhase;
        private bool _isResolving = false;

        public SpeedCounter SpeedCounter => _speedCounter;
        public PendingEffectQueue PendingQueue => _pendingQueue;
        public int StackSize => _stack.Count;
        public bool IsEmpty => _stack.Count == 0;
        /// <summary>栈顶未结算项（2026-10-16 lbl-toast 定案：响应窗提示「在响应什么」用；
        /// 空栈=null，只读不弹——CurrentPhase 死字段勿读的教训不适用于此只读口）。</summary>
        public EffectInstance PeekStack() => _stack.Count > 0 ? _stack.Peek() : null;

        /// <summary>栈内容只读快照（2026-10-19 连锁链 UI 数据源）：返回即时拷贝（顶→底序），
        /// 防外部枚举期间栈变动；BattleViewData 据此构建 ChainCards。</summary>
        public List<EffectInstance> SnapshotStack() => new List<EffectInstance>(_stack);

        /// <summary>「发动中」源卡清单（E1-1 修订·**链展示，非目标域**——反制=响应绑定 RespondedEntry，
        /// 不走 15/16 候选）：栈上待结算条目的源卡（任意来源），按条目 Controller 分侧。
        /// 排除 SBA/阶段窗/攻击·守卫宣言与已标 IsActivationNegated；不查卡所在区。底→顶稳定序。
        /// 消费口：BattleViewData 连锁链/UI 展示。</summary>
        public List<Card> GetEngagedCards(Player side)
        {
            var result = new List<Card>();
            foreach (var e in GetStackContents()) // 底→顶稳定序
            {
                if (e == null || e.IsSBA || e.IsPhaseWindow || e.IsAttackDeclaration || e.IsGuardDeclaration) continue;
                if (e.IsActivationNegated) continue;
                if (!(e.Source is Card card)) continue;
                if (side != null && !ReferenceEquals(e.Controller, side)) continue;
                if (!result.Contains(card)) result.Add(card); // 同卡多条目（理论上不可见）去重
            }
            return result;
        }

        /// <summary>「发动中」条目按源卡反查（**链展示，非目标**——UI 卡+效果名/验证器对拍）：
        /// 返回该卡在栈上最近的（最晚入栈）条目；无=null。排除口径与 GetEngagedCards 同源。</summary>
        public EffectInstance GetEngagedEntryOf(Card card)
        {
            if (card == null) return null;
            EffectInstance found = null;
            foreach (var e in GetStackContents()) // 底→顶：末次命中=最近（最顶）条目
            {
                if (e == null || e.IsSBA || e.IsPhaseWindow || e.IsAttackDeclaration || e.IsGuardDeclaration) continue;
                if (e.IsActivationNegated) continue;
                if (e.Source is Card src && ReferenceEquals(src, card)) found = e;
            }
            return found;
        }

        /// <summary>验证器/探针直挂条目（E1-1 发动中夹具）：绕过专用 Push 的字段组装与门槛——
        /// 生产流转一律走 PushCardCast/TryActivateEffect/PushAttackDeclaration 等专用口
        ///（本口不设速度门槛、不触发入栈联动；DrainStack 会照常结算它）。</summary>
        public void PushRaw(EffectInstance entry) => PushWithResponse(entry);

        /// <summary>验证器探针口：直接弹出栈顶（不结算——仅测试夹具清栈用；生产流转走 DrainStack）。</summary>
        public EffectInstance PopForTest() => _stack.Count > 0 ? _stack.Pop() : null;

        /// <summary>施放中卡清单（S3b A 切源 2026-11-04）：栈上 IsCardCast 条目的源卡（底→顶稳定序），
        /// 按条目 Controller 分侧。**占位/链展示数据源**——替代 Zone.Activation 区查询
        ///（区仍填=A 阶段零行为；B 阶段删区后本口成唯一源）。不滤已标（展示口径含被无效待结算的）。</summary>
        public List<Card> GetPendingCastCards(Player side)
        {
            var result = new List<Card>();
            foreach (var e in GetStackContents()) // 底→顶稳定序
            {
                if (e == null || !e.IsCardCast) continue;
                if (!(e.Source is Card card)) continue;
                if (side != null && !ReferenceEquals(e.Controller, side)) continue;
                if (!result.Contains(card)) result.Add(card);
            }
            return result;
        }

        /// <summary>卡是否施放中（S3b 单一判定口 2026-11-04）：该卡有 IsCardCast 条目在栈上。
        /// **全域剔除消费口**——手牌域候选（弃牌/手牌破坏/展示/代价弃牌）、手牌上限、UI 手牌扇、
        /// AI 候选、观测手牌计数：施放中的卡留在手牌但**不算手牌可用卡**（区域效果指不到它=
        /// 变相无限反制规则洞的防线）。不滤 IsActivationNegated（已发动无效的 cast 结算前仍占位）。</summary>
        public bool IsPendingCast(Card card)
        {
            if (card == null) return false;
            foreach (var e in GetStackContents())
                if (e != null && e.IsCardCast && e.Source is Card src && ReferenceEquals(src, card))
                    return true;
            return false;
        }

        /// <summary>入栈统一口（E1-1 响应绑定）：**Push 前捕获当前栈顶**写入 RespondedEntry
        ///（空栈=null）——随谁入栈就绑谁。全部专用 Push 与 PushRaw 走此口；链条 RespondedEntry
        /// 引用链即连锁链（A←B←C）。捕获在记速器/优先权切换**之前**——绑定的是入栈瞬间的栈顶。</summary>
        private void PushWithResponse(EffectInstance entry)
        {
            if (entry == null) return;
            entry.RespondedEntry = _stack.Count > 0 ? _stack.Peek() : null;
            _stack.Push(entry);
        }
        public Player CurrentPriorityHolder => _priorityHolder;
        public Player ActivePlayer => _activePlayer;
        public bool WaitingForPlayer => _waitingForPlayer;
        public PhaseType CurrentPhase => _currentPhase;
        /// <summary>结算进行中（含等待 UI 的异步原子效果）。主循环据此避免重入。</summary>
        public bool IsResolving => _isResolving;

        public StackEngine(EffectExecutor executor)
        {
            _executor = executor;
            _pendingQueue = new PendingEffectQueue(_speedCounter);
        }

        public void Initialize(Player startingPlayer)
        {
            _activePlayer = startingPlayer;
            _priorityHolder = startingPlayer;
            _speedCounter.Reset();
            _pendingQueue.Clear();
            _stack.Clear();
            _chargedThisTurn.Clear();
            _waitingForPlayer = false;
            _consecutivePassCount = 0;
            _isResolving = false; // 跨局残留清算（2026-10-17）：上局结算中被重开打断时，finally 不会跑——
                                  // 残留 true 会闭死帧循环驱动与窗口收集（同族病灶：跨Play单例成对重置）
        }

        /// <summary>
        /// 设置当前阶段（阶段变更时调用）
        /// </summary>
        public void SetPhase(PhaseType phase)
        {
            _currentPhase = phase;
        }

        /// <summary>登场族时机（2026-10-10 耐久即次数池定案配套）：入场结算属出牌/入场流程，
        /// 不消耗耐久——OnPlay=打出登场、OnSummon=任意进场超集（含效果召唤/复活/token）、
        /// OnReturnFromGraveyard=墓地回场。</summary>
        private static bool IsEntranceTiming(TriggerTiming t)
            => t == TriggerTiming.OnPlay || t == TriggerTiming.OnSummon || t == TriggerTiming.OnReturnFromGraveyard;

        /// <summary>
        /// 处理条件发动效果（强制/自动，不走速度，自动入栈）
        /// 每轮轮询前调用；2026-09-13 定案：条件发动不参与速度比较、不抬升记速器
        /// </summary>
        public void ProcessTriggeredEffects()
        {
            while (true)
            {
                var next = _pendingQueue.GetNextEffect();
                if (next == null) break;
                if (next.Effect == null) continue; // 无定义条目丢弃（防记账空引用；旧闸门同口径兜底）

                // 自馈闸门（2026-10-30）：同一效果每回合超 30 次视为自循环，静默丢弃。
                // 不是「一回合一次」限制——常规触发远低于上限；引擎/耐久另有天然上限。
                if (_executor.TriggerLoopGuardReached(next.Effect, next.Source))
                {
                    TideLog.Warn($"[LoopGuard] 自循环截断：effect={next.Effect.Id} source={next.Source?.RuntimeId} "
                                 + $"已达每回合 {EffectExecutor.TriggerLoopGuardLimit} 次");
                    continue;
                }

                // §11 每卡过载闸（2026-11-09 裁定）：触发式按**源卡**预算（光环合成条目 Source=受益者
                // → 计入受益者，§11.2.2）；过载即丢弃（强制触发也丢，MandatorySkipped 口径），
                // 不入栈、不扣耐久（§11.7：失败不扣耐久）。玩家/系统源不预算（默认跳过）。
                if (next.Source is Card budgetSrc
                    && !CardEffectBudget.Instance.CanCommit(budgetSrc, out var overloadReason))
                {
                    TideLog.Warn($"[Overload] 触发式丢弃（MandatorySkipped）：src={budgetSrc.ID} effect={next.Effect.Id} {overloadReason}");
                    continue;
                }

                // 触发式无每回合生效次数上限（2026-10-30 定案）：满足时点的每次触发都入栈。
                // 自由分支引擎奖励 2026-11-13 起无每回合上限（频率由耐久/挂载价付费），不在此闸。
                var instance = EffectInstance.FromPendingEffect(next);
                PushWithResponse(instance);
                next.IsOnStack = true;

                // §11.3：触发式在入栈口计数（本次结算不受自己刚挂的过载影响——第 30 次照常结算）
                if (next.Source is Card committedSrc)
                    CardEffectBudget.Instance.Commit(committedSrc, CardEffectCommitKind.Triggered, out _);

                // 入栈即记账（2026-09-13 修复；2026-10-30 起仅作读数——条件计数/卡面统计，无闸门）：
                // 同批多个触发出队时，结算期记账会让读数滞后。
                _executor.RecordQueuedActivation(next.Effect, next.Source);

                // 结界被动触发消耗（2026-11-14 档位制「结界用耐久买次数」）：**每次触发扣 1 点耐久**
                // （固定值；时机系数 f 只用于生物挂载价，不进耐久）——一回合多次档只有结界能挂，
                // 耐久即该档的次数池（D 点耐久 = 再触发 D 次）。
                // 豁免：登场族（OnPlay/OnSummon/OnReturnFromGraveyard——入场结算属出牌/入场流程，
                // 仪典载体不受影响）；光环条目=存在经济常驻，不经触发路径；离场族触发时源卡已离场
                //（下方战场判定不过）→ 自然不消耗，兼防"耗尽→销毁→亡语→再耗"递归。
                if (next.Effect != null
                    && next.Source is Card trigCard
                    && Attribute.CounterRules.UsesDurabilityEconomy(trigCard)
                    && trigCard.GetZone() == Zone.Battlefield
                    && !IsEntranceTiming(next.Effect.TriggerTiming))
                {
                    Attribute.CounterRules.ConsumeTriggerDurability(
                        GameCore.Instance, trigCard, "被动触发（耐久−1/次）");
                }

                EventManager.Instance.Publish(new StackAddEvent
                {
                    AddedObject = instance,
                    AddingPlayer = next.Controller
                });
            }
        }

        /// <summary>
        /// 尝试发动效果（速度发动入口）
        /// 门槛（2026-11-01 统一口径，与 PushCardCast/CollectAvailableResponses 同款）：
        /// ① 空栈首动（新连锁第一个发动/主阶段出牌）免门槛——0 速主动效果主阶段照常；
        /// ② 非空栈：速≥1 且 速≥记速器（同速可接）——旧「全严格大于」与手牌整卡口径不一致
        ///    （1 速主动效果接不住 1 速记速器），0 速主动效果仍锁在自家主阶段；
        /// ③ 条件发动（强制/自动触发）不走速度、恒可入栈（原 SpeedCounter.CanActivate 口径保持）。
        /// 入栈时记速器只在更高速度时抬升（RaiseTo，非无条件 +1）
        /// </summary>
        public bool TryActivateEffect(PendingEffect pending)
        {
            bool gate;
            if (pending.ActivationType != EffectActivationType.Voluntary)
                gate = true; // 条件发动：不参与速度比较（原 CanActivate 首位口径，含结算中——触发批有自己的排水口）
            else if (_speedCounter.IsResolving)
                gate = false; // 结算中不允许速度发动入栈
            else if (_stack.Count == 0)
                gate = true; // 空栈首动免门槛
            else
                gate = pending.ActivationSpeed >= 1 && pending.ActivationSpeed >= _speedCounter.CurrentSpeed;
            GameActions.Crumb($"CF-activate-try def={pending?.Effect?.Id} speed={pending?.ActivationSpeed} by={pending?.Controller?.Name} ctr={_speedCounter.CurrentSpeed} gate={gate}");
            if (!gate)
                return false;

            _speedCounter.RaiseTo(pending.ActivationSpeed); // ★ 记速器：更高才抬升

            var instance = EffectInstance.FromPendingEffect(pending);
            PushWithResponse(instance);
            pending.IsOnStack = true;

            _priorityHolder = pending.Controller.Opponent;
            _consecutivePassCount = 0;
            _waitingForPlayer = true;

            EventManager.Instance.Publish(new StackAddEvent
            {
                AddedObject = instance,
                AddingPlayer = pending.Controller
            });
            GameActions.Crumb($"CF-activate-ok def={pending.Effect?.Id} stack={_stack.Count} prio={_priorityHolder?.Name}");

            return true;
        }

        /// <summary>
        /// 整卡施放入栈（使用时点声明）：打出的卡已移入发动区，本对象代表「此卡被使用」，
        /// 对手获得优先权——响应窗口内可经 GameActions.PlayCardInResponse 打出 发动无效/效果无效 等。
        /// 2026-10-18 定案：整卡施放同为速度发动——速度=卡面声明（效果 BaseSpeed 最大值，缺省 0）；
        /// 门槛=全严格大于（同速连锁一律禁止，连锁对象无关归属），空栈首动（主阶段出牌）免门槛，
        /// 0 速卡无法在对手回合响应打出。
        /// 结算消费点见 GameActions.ResolveCardCastAsync（Option Y：扣费在响应窗口之后）。
        /// </summary>
        public bool PushCardCast(Card card, Player controller, List<Entity> targets, int modeIndex = 0,
            List<Entity> references = null, IReadOnlyList<int> dynamicCounts = null)
        {
            if (card == null || controller == null) return false;
            if (_isResolving) return false; // 结算中不可声明（与 SpeedCounter.CanActivate 同口径）
            SyncActivePlayerFromTurn();
            int castSpeed = SpeedCalculator.GetCardCastSpeed(card);
            // 2026-11-01 用户定案：整卡施放的门槛=①空栈首动免门槛（主阶段出牌照旧）
            // ②非空栈：速≥1 且 速≥记速器（法术同速可连锁，见 CollectAvailableResponses 同款口径）。
            // 旧的 CanActivate 全严格大于会把「对手 1 槽法术(速2) → 我的 1 槽法术(速2)」卡死。
            // SBA 窗例外（2026-09-15 SbaStackSpeed 定案仍有效）：SBA 伪对象在栈顶时非回合方
            // 严格大于（≥2）——同速可连锁只放开 cast↔cast 链，角色相对门槛照旧
            //（速度2治疗救回 = 非回合方对 SBA 的既定达标路径）。
            if (_stack.Count > 0 && PeekStack()?.IsSBA == true
                && controller != GameCore.Instance?.TurnEngine?.TurnPlayer
                && castSpeed <= _speedCounter.CurrentSpeed)
                return false;
            if (_stack.Count > 0 && (castSpeed < 1 || castSpeed < _speedCounter.CurrentSpeed))
                return false;

            var instance = new EffectInstance
            {
                IsCardCast = true,
                Source = card,
                Controller = controller,
                ActivationSpeed = castSpeed,
                Targets = targets != null ? new List<Entity>(targets) : new List<Entity>(),
                Refs = references != null && references.Count > 0 ? new List<Entity>(references) : null,
                DynamicCounts = dynamicCounts != null && dynamicCounts.Count > 0
                    ? new List<int>(dynamicCounts) : null, // 动态数量（2026-11-14）：扣费/执行同表
                ModeIndex = modeIndex, // 抉择：声明期选定模式（随 cast 上栈，对手可见、结算按此付费）
            };

            _speedCounter.RaiseTo(castSpeed); // ★ 记速器：更高才抬升

            PushWithResponse(instance);
            _priorityHolder = controller.Opponent;
            _consecutivePassCount = 0;
            _waitingForPlayer = true;

            EventManager.Instance.Publish(new StackAddEvent
            {
                AddedObject = instance,
                AddingPlayer = controller
            });

            return true;
        }

        /// <summary>SBA 栈对象速度（2026-09-15 定案）：SBA=速度1——回合方可连锁（≥1）、非回合方严格大于（≥2）。</summary>
        public const int SbaStackSpeed = 1;

        /// <summary>
        /// SBA 伪对象入栈（2026-09-15 定案：SBA=速度1栈对象）。
        /// 由 FinishResolution 检出关键 SBA（尸体送墓/判负）后调用：不携带记录载荷（到点重查，
        /// 与预言延迟验证同哲学），记速器抬到 1，回合方先获优先权——LIFO 使窗口内的救场效果
        /// （如速度2治疗）先结算，SBA 到点重查被救回则空转、亡语不触发。
        /// 注意：调用点在 ResolveStack 的 try 块内（引擎 _isResolving 仍 true），故不设
        /// PushCardCast 的结算中拒入闸——那是给玩家声明用的；SpeedCounter 已在 FinishResolution
        /// 开头 Reset，RaiseTo(1) 恒 0→1。
        /// </summary>
        public bool PushStateAction()
        {
            if (_stack.Count > 0) return false; // 栈上有待响应对象时不插（理论不可达：调用点栈必空）

            var instance = new EffectInstance
            {
                IsSBA = true,
                ActivationSpeed = SbaStackSpeed,
                // Source/Controller/Definition 恒 null：SBA 是规则动作，无施放者、无费用、无执行定义
                Targets = new List<Entity>()
            };

            _speedCounter.RaiseTo(SbaStackSpeed); // ★ 记速器：SBA 抬到 1（本连锁的最高速度基线）

            PushWithResponse(instance);
            _priorityHolder = _activePlayer;   // 回合方先响应（与触发式轮统一）
            _consecutivePassCount = 0;
            _waitingForPlayer = true;

            EventManager.Instance.Publish(new StackAddEvent
            {
                AddedObject = instance,
                AddingPlayer = _activePlayer
            });

            return true;
        }

        /// <summary>阶段窗伪对象速度（2026-11-01 定案）：0 速——不抬记速器，窗口内响应门槛
        /// =速≥1 且 速≥0（1 速手牌整卡/主动效果可入，与空栈首动同基线；0 速主动效果照旧不可入）。</summary>
        public const int PhaseWindowStackSpeed = 0;

        /// <summary>
        /// 阶段优先权窗伪对象入栈（2026-11-01 定案：0 速伪栈对象开窗，仿 PushStateAction）。
        /// 时点（2026-11-01 同日修订，双窗）：①「结束阶段开始」——回合方声明结束、结束阶段系统
        /// 行为（TurnEndEvent 订阅链）之前（GameActions.EndTurn 驱动）；②「主阶段开始」——
        /// 新回合折返落主阶段时（GameCore.OnMainPhaseStartedWindow 订阅 PhaseStartEvent(Main) 驱动；
        /// 原「回合结束前」时点废弃）。跳过主要阶段档无主阶段开始事件=自然无窗②。
        /// 伪对象不参与结算（ResolveStack 的 IsPhaseWindow 分支到点自动消失）；双方 Pass 即让路，
        /// 阶段推进由窗口外的调用链/帧泵承担；无候选时响应窗泵自动双 Pass（AI/无头局不卡死）。
        /// 优先权起点=回合方（与 SBA/触发轮同口径）。label=窗口时点名（描述层显示，可空）。
        /// </summary>
        public bool PushPhaseWindow(Player firstResponder, string label = null)
        {
            if (firstResponder == null) return false;
            if (_stack.Count > 0) return false; // 栈上有待响应对象时不插（调用点栈必空）
            if (_speedCounter.IsResolving) return false; // 结算中不开窗（与玩家声明口同口径）
            SyncActivePlayerFromTurn();

            var instance = new EffectInstance
            {
                IsPhaseWindow = true,
                PhaseWindowLabel = label,
                ActivationSpeed = PhaseWindowStackSpeed, // 0 速：RaiseTo(0) 无操作，记速器保持 0
                Controller = firstResponder,             // 优先权起点（描述层用；结算无消费）
                // Source/Definition 恒 null：阶段窗是规则动作，无施放者、无费用、无执行定义
                Targets = new List<Entity>()
            };

            PushWithResponse(instance);
            _priorityHolder = firstResponder; // 回合方先响应（与 SBA 轮同口径）
            _consecutivePassCount = 0;
            _waitingForPlayer = true;

            EventManager.Instance.Publish(new StackAddEvent
            {
                AddedObject = instance,
                AddingPlayer = firstResponder
            });
            GameActions.Crumb($"CF-phase-window-push({label ?? "?"}) by={firstResponder.Name} stack={_stack.Count}");

            return true;
        }

        /// <summary>攻击宣言栈对象速度（2026-09-16 战斗接入栈机器定案）：攻击=速度0——
        /// 宣言本就要求空栈（连锁中不可宣），走空栈首动豁免（2026-10-18），门槛恒过。
        /// 宣言期零支付（不横置不扣费）——横置在结算时支付（到点重查）。</summary>
        public const int AttackStackSpeed = 0;

        /// <summary>回合归属对账（2026-09-20 修复）：_activePlayer 是回合开始事件的缓存副本——
        /// 合成 TurnStartEvent（验证器 shield 段）/非标准回合推进会让它与 TurnEngine 撕裂
        /// （曾见 TurnPlayer=P1 而 _activePlayer=P2，攻击宣言被「非回合方 0&gt;0」静默拒绝、
        /// 后续断言整体级联）。栈空且无等待窗口=连锁间歇，此时以 TurnEngine 为唯一真值回填。</summary>
        private void SyncActivePlayerFromTurn()
        {
            if (_stack.Count > 0) return; // 连锁进行中不作对账（等待窗口必然有栈对象）
            // 栈空却 _waitingForPlayer=true = 上一轮非正常收尾的悬置脏态——对账时一并复位
            var turnPlayer = GameCore.Instance?.TurnEngine?.TurnPlayer;
            if (turnPlayer != null && !ReferenceEquals(turnPlayer, _activePlayer))
            {
                _activePlayer = turnPlayer;
                _priorityHolder = turnPlayer;
                _consecutivePassCount = 0;
                _waitingForPlayer = false;
            }
        }

        /// <summary>守卫拦截速度（2026-09-16 定案）：守卫=速度1响应（原2速阻挡阶段退役）——
        /// 防守方（非回合方）严格 &gt; 计数器：1 &gt; 0 ✓；入栈后计数器抬到 1，
        /// 攻方须 &gt;1 再连锁（2026-10-18 全严格大于口径，旧「攻方可 ≥1 再连锁」退役），
        /// 防守方守卫全 1 速无法再响应（1 不 &gt; 1）→ 双 Pass 结算。</summary>
        public const int GuardStackSpeed = 1;

        /// <summary>
        /// 攻击宣言入栈（逐攻击开窗，2026-09-16 定案）。速度门=标准口径（0 ≥ 计数器），
        /// 栈非空（连锁中）不可宣；宣言期零支付；发 AttackDeclarationEvent（宣言时点，触发器可见）；
        /// 防守方先获响应机会。结算消费见 ResolveStack 的 IsAttackDeclaration 分支。
        ///（疾风仪典宣言速度+1 已随 2026-10-31 整链删除。）
        /// </summary>
        public bool PushAttackDeclaration(Entity attacker, Entity target, Player controller)
        {
            if (attacker == null || target == null || controller == null) return false;
            if (_stack.Count > 0) return false; // 连锁中不可宣（速度门 0≥计数器的等价直查）
            SyncActivePlayerFromTurn();
            int speed = AttackStackSpeed;
            if (!_speedCounter.CanActivate(speed, EffectActivationType.Voluntary, _stack.Count == 0))
                return false;

            var instance = new EffectInstance
            {
                IsAttackDeclaration = true,
                Source = attacker,
                Controller = controller,
                ActivationSpeed = speed, // 基速0——攻击宣言不抬计数器（无 RaiseTo 调用）
                Targets = new List<Entity> { target },
                OriginalAttackTarget = target, // 原目标存档（2026-10-16 碾压改版：守卫改写后溅射枢轴仍取原目标）
            };

            PushWithResponse(instance);
            _priorityHolder = controller.Opponent; // 防守方先响应（发动弹窗候选：守卫/响应卡）
            _consecutivePassCount = 0;
            _waitingForPlayer = true;
            GameActions.Crumb($"CF-attack-push atk={(attacker as Card)?.ID ?? attacker.ToString()} tgt={(target as Card)?.ID ?? (target as Player)?.Name ?? target.ToString()} speed={speed} ctr={_speedCounter.CurrentSpeed} stack={_stack.Count}");

            var declaration = new AttackDeclarationEvent
            {
                Attacker = attacker,
                Target = target,
                AttackingPlayer = controller
            };
            if (GameCore.Instance != null) GameCore.Instance.PublishEvent(declaration);
            else EventManager.Instance.Publish(declaration);

            EventManager.Instance.Publish(new StackAddEvent { AddedObject = instance, AddingPlayer = controller });
            return true;
        }

        /// <summary>充能宣言栈对象速度（2026-11-05 充能系统动作化改版）：充能=速度0规则动作（同攻击）——
        /// 宣言本就要求空栈（连锁中不可宣），宣言期零支付零选色，颜色在结算期反问。</summary>
        public const int ChargeStackSpeed = 0;

        /// <summary>
        /// 充能宣言入栈（2026-11-05 充能系统动作化改版，仿 PushAttackDeclaration）：充能=速度0
        /// 系统动作，任意手牌卡一回合一次可宣（资格/每回合规额闸在 GameActions.DeclareCharge 单源）。
        /// 宣言期零支付零选色——对手获响应窗口（标准双 Pass 流）；结算消费见 ResolveStack 的
        /// IsChargeDeclaration 分支（GameActions.ResolveChargeDeclarationAsync：反问元素类别→
        /// +1 层同色费用降低指示物→展示，**不再支付元素**——旧 P12 现付 1 点制随改版退役）。
        /// 入栈成功即记本回合规额（MarkChargeUsed——结算期不再记）。
        /// </summary>
        public bool PushChargeDeclaration(Card card, Player controller)
        {
            if (card == null || controller == null) return false;
            if (_stack.Count > 0) return false; // 连锁中不可宣（速度0=空栈首动，同攻击宣言）
            if (_isResolving) return false;     // 结算中不可声明（与玩家声明口同口径）
            SyncActivePlayerFromTurn();
            if (!_speedCounter.CanActivate(ChargeStackSpeed, EffectActivationType.Voluntary, _stack.Count == 0))
                return false;

            var instance = new EffectInstance
            {
                IsChargeDeclaration = true,
                Source = card,
                Controller = controller,
                ActivationSpeed = ChargeStackSpeed, // 基速0——不抬记速器（同攻击宣言，无 RaiseTo 调用）
                Targets = new List<Entity>(),
            };

            PushWithResponse(instance);
            _priorityHolder = controller.Opponent; // 对手先获响应机会（同攻宣防守方先响应口径）
            _consecutivePassCount = 0;
            _waitingForPlayer = true;
            GameActions.Crumb($"CF-charge-push card={card.ID} by={controller.Name} stack={_stack.Count}");

            EventManager.Instance.Publish(new StackAddEvent { AddedObject = instance, AddingPlayer = controller });
            return true;
        }

        /// <summary>充能每回合规额记账（入栈成功口——GameActions.DeclareCharge 调用；结算期不记）。</summary>
        public void MarkChargeUsed(Card card)
        {
            if (card != null) _chargedThisTurn.Add(card);
        }

        /// <summary>该卡本回合是否已充能（一张卡一回合只能发动一次；回合开始清零）。</summary>
        public bool IsChargeUsed(Card card) => card != null && _chargedThisTurn.Contains(card);

        /// <summary>
        /// 守卫拦截入栈（速度1响应，2026-09-16 定案）：Targets=[被拦截的攻击宣言对象]。
        /// 守卫者=未横置守卫生物，或角色（2026-10-16 角色守卫：资格=攻击弹药&gt;0）。
        /// 宣言期零支付（横置在结算时支付）；结算时改写攻击目标（CombatSystem.ResolveGuardDeclaration）。
        /// </summary>
        public bool PushGuardDeclaration(Entity guarder, EffectInstance attackInstance, Player controller)
            => PushGuardDeclaration(guarder, attackInstance, controller, out _);

        /// <summary>带原因版（2026-11-03 失败提示细化）：资格/速度门拒绝逐条可读（网络 Error 帧透传）。</summary>
        public bool PushGuardDeclaration(Entity guarder, EffectInstance attackInstance, Player controller, out string reason)
        {
            reason = null;
            if (guarder == null || attackInstance == null || controller == null) { reason = "内部错误：空参数"; return false; }
            if (!_stack.Contains(attackInstance))
            { reason = "该攻击宣言已不在栈上（响应窗口已关闭？）"; return false; } // 拦截对象须在栈上
            // 资格闸（2026-09-16；2026-11-06 opt-in）：守卫资格（未声明 GuardAbility 不能拦）/存活/未横置——横置支付在结算期，
            // 此处拦的是"已横置不能再宣"。角色（玩家）守卫（2026-10-16 定案）：无守卫关键词、
            // 无横置概念——资格=有攻击弹药（弹药即攻击力）；守卫=把攻击改写为打角色，
            // 反伤/弹药烧除走既有 Player 目标路径（ResolvePair 反伤步全烧——与被打同口径）
            bool guardOk;
            if (guarder is Card guardCard)
                guardOk = guardCard.HasGuardAbility() && guardCard.IsAlive && !guardCard.IsTapped();
            else if (guarder is Player heroGuard)
                guardOk = heroGuard.IsAlive && heroGuard.GetPower() > 0
                          && !RuleAuraSystem.ScopeHits(RuleAuraComponents.NoGuardAura, heroGuard);
            else
                guardOk = false;
            if (!guardOk)
            {
                if (guarder is Card gc)
                {
                    if (!gc.IsAlive) reason = "该单位已死亡";
                    else if (gc.IsTapped()) reason = "该单位已横置（守卫须未横置）";
                    else reason = RuleAuraSystem.ScopeHits(RuleAuraComponents.NoGuardAura, gc.GetController())
                        ? "禁守令生效——守卫能力失效"
                        : "该单位不具备守卫能力";
                }
                else if (guarder is Player hp)
                {
                    reason = hp.GetPower() <= 0
                        ? "角色无弹药（攻击力 0）——不能守卫"
                        : "禁守令生效——角色不能守卫";
                }
                else reason = "守卫者无效";
                return false;
            }
            SyncActivePlayerFromTurn();
            if (!_speedCounter.CanActivate(GuardStackSpeed, EffectActivationType.Voluntary, _stack.Count == 0))
            { reason = "守卫速度门拒绝（记速器已高于守卫速度）"; return false; }

            // §11 过载闸（2026-11-09 裁定）：守卫宣言按主动效果处理——过载卡拒绝宣言
            //（角色守卫无预算——预算只挂卡实例）
            if (guarder is Card guardBudget && !CardEffectBudget.Instance.CanCommit(guardBudget, out var guardOverload))
            { reason = guardOverload; return false; }

            _speedCounter.RaiseTo(GuardStackSpeed); // ★ 记速器抬到 1：攻方须 >1 连锁、守方 1 速不能再响应

            var instance = new EffectInstance
            {
                IsGuardDeclaration = true,
                Source = guarder,
                Controller = controller,
                ActivationSpeed = GuardStackSpeed,
                InterceptedAttack = attackInstance, // 拦截载荷（Targets 装不下栈对象，专用字段）
            };

            PushWithResponse(instance);
            _priorityHolder = controller.Opponent;
            _consecutivePassCount = 0;
            _waitingForPlayer = true;
            GameActions.Crumb($"CF-guard-push guarder={(guarder as Card)?.ID ?? (guarder as Player)?.Name} stack={_stack.Count} ctr={_speedCounter.CurrentSpeed}");

            // §11.3：守卫宣言成功后计数
            if (guarder is Card guardCommitted)
                CardEffectBudget.Instance.Commit(guardCommitted, CardEffectCommitKind.Guard, out _);

            EventManager.Instance.Publish(new StackAddEvent { AddedObject = instance, AddingPlayer = controller });
            return true;
        }

        /// <summary>
        /// 添加待发效果到队列
        /// </summary>
        public void AddPendingEffect(PendingEffect effect)
        {
            _pendingQueue.AddPendingEffect(effect);
        }

        /// <summary>待发队列是否有自动（触发式）效果——真实对局由 GameLoopController 帧循环泵上栈；
        /// 空栈上的待发在无游戏循环场景（编辑器验证/headless）需由排干口处理。</summary>
        public bool HasPendingEffects => _pendingQueue.HasAutoEffects;

        /// <summary>
        /// 处理待发效果（条件发动自动入栈）
        /// </summary>
        public void ProcessPendingEffects()
        {
            ProcessTriggeredEffects();
        }

    }
}
