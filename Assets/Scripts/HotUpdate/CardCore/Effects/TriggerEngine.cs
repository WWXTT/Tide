using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using CardCore.Attribute;
using CardCore.Attribute.Handlers;

namespace CardCore
{
    /// <summary>
    /// 触发引擎
    /// 处理条件发动效果（基于事件触发）
    /// </summary>
    public class TriggerEngine
    {
        private List<RegisteredEffect> _registeredEffects = new List<RegisteredEffect>();
        private StackEngine _stackEngine;

        // 光环效果授予解析缓存（2026-11-05 效果光环）：effectRef → (库 DTO 实例, 转换定义)。
        // 按库 DTO 实例引用判失效——EffectsLibrary reload 换实例即重转（mtime 盯盘在库内）。
        // def=null 表示悬空或资格不符（EffectEligibility.AuraGrantRejectReason 单源，装载闸
        // 同口径）——运行时静默跳过，防直改表夹带上栈。
        private readonly Dictionary<string, (EffectSlimDto dto, EffectDefinition def)> _auraGrantCache
            = new Dictionary<string, (EffectSlimDto, EffectDefinition)>();

        // 触发条件校验（由 GameCore 注入 ZoneManager 后可用）。未注入时跳过条件校验，
        // 便于在缺少区域系统的独立测试中仅按时点匹配。
        private ConditionChecker _conditionChecker;
        private ZoneManager _zoneManager;

        public List<RegisteredEffect> RegisteredEffects => _registeredEffects;

        public TriggerEngine(StackEngine stackEngine)
        {
            _stackEngine = stackEngine;
        }

        /// <summary>
        /// 注入区域系统以启用触发条件校验。
        /// </summary>
        public void AttachConditionContext(ZoneManager zoneManager)
        {
            _zoneManager = zoneManager;
            _conditionChecker = new ConditionChecker(zoneManager);
        }

        /// <summary>
        /// 注册效果（时点接线定案：统一出口在 TryMoveToBattlefield/TryAddToBattlefield，
        /// 经 GameActions.RegisterCardTriggeredEffects 调入；幂等去重防回手重打/二次复活重复注册）
        /// </summary>
        public void RegisterEffect(EffectDefinition effect, Entity source, Player controller)
        {
            if (!effect.IsTriggeredEffect)
                return;

            // 引擎主干不入触发管线（2026-10-20 定案）：Branch 载荷 Settle==Engine 的效果是
            // BranchEngines 自订阅评估的引擎机械（倒计时递减/掷骰判定等），不该走「时机事件→
            // 待发队列→推栈」管线——推上栈后无人结算成孤儿项（GameLoopController 只在栈空时
            // 驱动优先权），堵死 EndTurn 栈空闸（Play 实测：教学倒计时龟 AC0306F3 在回合开始
            // 入栈后整局卡死）。
            if (CardEffectConverter.EngineKindOfDefinition(effect) != BranchEngineKind.None)
                return;

            // 幂等：同一来源实体 + 同一效果 Id 只注册一次
            // （同模板两张不同实例 Source 不同、各自注册，正确；死亡期靠 Source.IsAlive 门禁兜底）
            for (int i = 0; i < _registeredEffects.Count; i++)
            {
                var r = _registeredEffects[i];
                if (ReferenceEquals(r.Source, source) && r.Effect != null && r.Effect.Id == effect.Id)
                    return;
            }

            _registeredEffects.Add(new RegisteredEffect
            {
                Effect = effect,
                Source = source,
                Controller = controller
            });
        }

        /// <summary>
        /// 注销效果（预留 API：当前无调用方——注册后不注销，死卡由 Source.IsAlive 门禁挡住；
        /// 复活语义将来若需要"注销+重注册"路径时启用）
        /// </summary>
        public void UnregisterEffect(EffectDefinition effect)
        {
            _registeredEffects.RemoveAll(r => r.Effect == effect);
        }

        /// <summary>
        /// 注销实体的所有效果（预留 API，同 UnregisterEffect）
        /// </summary>
        public void UnregisterEntityEffects(Entity source)
        {
            _registeredEffects.RemoveAll(r => r.Source == source);
        }

        /// <summary>
        /// 处理游戏事件
        /// 匹配触发时点 → 创建 PendingEffect → 加入待发队列
        /// </summary>
        public void OnEvent(IGameEvent gameEvent)
        {
            var matchingEffects = FindMatchingEffects(gameEvent);

            foreach (var match in matchingEffects)
            {
                var pending = PendingEffect.Create(
                    match.Effect,
                    match.Source,
                    match.Controller,
                    _stackEngine.ActivePlayer,
                    _stackEngine.CurrentPhase,
                    triggeringEvent: gameEvent
                );

                _stackEngine.AddPendingEffect(pending);
            }
        }

        /// <summary>
        /// 查找匹配的效果
        /// </summary>
        private List<RegisteredEffect> FindMatchingEffects(IGameEvent gameEvent)
        {
            var result = new List<RegisteredEffect>();
            var eventType = gameEvent.GetType();

            // 候选=物化注册表 + 光环效果授予合成条目（2026-11-05 效果光环 live-query——事件命中时
            // 实时枚举不物化注册，断链/离场自动失效；合成条目 Source=受益单位 ⇒ 载荷过滤与
            // {target} 自参照零改动对准受益者，下方判定链全程共用零分叉）
            foreach (var registered in EnumerateRegisteredAndAuraGrants())
            {
                var effect = registered.Effect;

                // 检查触发时点
                var expectedType = TriggerTimingDefaults.GetEventType(effect.TriggerTiming);
                if (expectedType != eventType)
                    continue;

                // 时点接线定案：类型匹配后做事件载荷级过滤（self/other、进场来源、施受区分）
                if (!TriggerPayloadFilter.Matches(effect.TriggerTiming, gameEvent, registered))
                    continue;

                // 检查来源是否在场；亡语豁免（2026-09-15 修复）：自己的 CardDestroyEvent 不受此拦——
                // 伤害落血即置 IsAlive=false（KeywordRules.ApplyDamage），一刀切会拦死全部亡语
                //（TryKill 在发布 CardDestroyEvent 前已置 IsAlive=false）。
                // 其后的无效指示物/沉睡/TriggerConditions 门禁照常拦截（蓝无效仍拦亡语）。
                // 墓地亡灵闸（2026-11-05）：裸 MoveCard 入墓不清 IsAlive（验证器清理/部分工具口径），
                // 已入墓的源不得再开火——同享亡语豁免（自己的摧毁事件放行）。
                if (registered.Source is Card srcCard && srcCard.GetZone() == Zone.Graveyard
                    && !(gameEvent is CardDestroyEvent deG && ReferenceEquals(deG.DestroyedCard, registered.Source)))
                    continue;
                if (registered.Source != null && !registered.Source.IsAlive)
                {
                    if (!(gameEvent is CardDestroyEvent de && ReferenceEquals(de.DestroyedCard, registered.Source)))
                        continue;
                }

                // 自动效果压制（2026-09-09 缄默定案·2026-10-31 改名「失效」；沉睡同日并入=
                // SuppressRules 统一读数）：
                // 拦全部触发式（事件匹配后、上栈前）——含登场 OnPlay 族触发；只拦「注册来源自身」的能力。
                // 与沉默（拦主动，CanActivate）对称——缄默不拦主动，沉睡另在 CanActivate 0.6 拦主动。
                // 本体关键词另经 HasKeyword 收口同被压制（再生=GameCore 直连，读数走 HasKeyword 一并失效）；
                // 伤害管线被动（坚韧/圣盾/护甲——指示物，走 ApplyDamage 管线）不归此管，天然不拦。
                // 入场窗口自身的 OnPlay 不受影响（入场事件发布先于任何入场后状态挂层——匹配先于挂层）。
                // 光环授予合成条目豁免本闸（2026-11-05 拍板：受益者带失效/沉睡照发，仅绝缘体
                // 特判——绝缘体受益者已在授予枚举处剔除；来源侧无效不压光环=2026-10-31 定案不变）
                if (!registered.FromAuraGrant
                    && registered.Source != null
                    && SuppressRules.IsAutoSuppressed(registered.Source))
                    continue;

                // §11 过载（2026-11-10）：过载源的触发式全拦——**光环授予合成条目不豁免**
                //（§11.2.2：受益者过载→其身上的授予效果失效；计数/闸在 ProcessTriggeredEffects
                // 入栈口按受益者走 CanCommit，此处为枚举期前置丢弃，双保险）。
                // 注意：只拦触发式——静态关键词（守护/警戒/先攻）与指示物不经本口（§11.5）。
                if (registered.Source != null && SuppressRules.IsOverloaded(registered.Source))
                    continue;

                // 禁自动令光环（2026-11-06 改版，原禁发令=拦主动发动）：受光环影响的单位与角色
                // 不能发动自动效果——触发式全拦（同失效口径：登场/回合末/亡语等一律）；
                // 作用面按源卡控制者一侧判（固定双方规则=Scope 恒 Both，两侧同拦）；
                // 光环授予合成条目豁免（压制不进光环域——与失效 2026-10-31 定案同源）。
                if (!registered.FromAuraGrant)
                {
                    var autoSide = registered.Source is Player asp ? asp
                        : (registered.Source as Card)?.GetController();
                    if (autoSide != null
                        && RuleAuraSystem.ScopeHits(RuleAuraComponents.NoActivateAura, autoSide))
                        continue;
                }

                // 检查触发条件（intervening "if" 条件）：仅在已注入区域系统时校验。
                if (_conditionChecker != null &&
                    effect.TriggerConditions != null && effect.TriggerConditions.Count > 0)
                {
                    var conditionContext = new ConditionCheckContext
                    {
                        Activator = registered.Controller,
                        ActivePlayer = _stackEngine.ActivePlayer,
                        CurrentPhase = _stackEngine.CurrentPhase,
                        TurnNumber = GameCore.Instance?.TurnEngine?.TurnNumber ?? 0,
                        Source = registered.Source,
                        ZoneManager = _zoneManager
                    };
                    conditionContext.FillDamageAggregates(); // P2a：伤害聚合死条件修复

                    if (!_conditionChecker.CheckAll(effect.TriggerConditions, conditionContext))
                        continue;
                }

                result.Add(registered);
            }

            return result;
        }

        /// <summary>候选枚举=物化注册表 + 光环效果授予（live-query 合成条目，2026-11-05 效果光环）。</summary>
        private IEnumerable<RegisteredEffect> EnumerateRegisteredAndAuraGrants()
        {
            for (int i = 0; i < _registeredEffects.Count; i++)
                yield return _registeredEffects[i];
            foreach (var granted in EnumerateAuraGrantedEffects())
                yield return granted;
        }

        /// <summary>
        /// 光环效果授予枚举（live-query）：双方战场逐单位 × 当前命中的 effectRef 光环条目
        /// （LinkAuraSystem.GetGrantedEffectAuras 单源判定链——绝缘体受益者已在链内剔除）→
        /// 合成 RegisteredEffect{Effect=解析定义, Source=受益单位, Controller=受益者控制者}。
        /// Source=受益单位 ⇒ TriggerPayloadFilter 的 self 比对与 {target} 自参照零改动对准受益者。
        /// 重复授予（两条箭头/两个源授同一效果）=各一条候选（与关键词按箭头叠加口径一致）。
        /// 结界耐久不消耗：光环=持续授予，非载体自身触发文（与光环计价「恒持续不收持续费」同口径）。
        /// 结算计费：ConvertOne 对触发式设 ElementCostPrepaid=true——元素费不逐次向受益者控制者
        /// 收取（锚价×作用面已随载体卡价收讫，防双计；资格谓词已剔代价行，无特殊代价）。
        /// </summary>
        private IEnumerable<RegisteredEffect> EnumerateAuraGrantedEffects()
        {
            if (!GameBoard.LinkAuraSystem.AnyEffectGrantSources()) yield break; // 无源早退（逐事件高频口）
            var core = GameCore.Instance;
            var zoneManager = _zoneManager ?? core?.ZoneManager;
            if (zoneManager == null) yield break;
            foreach (var player in new[] { core.Player1, core.Player2 })
            {
                if (player == null) continue;
                var battlefield = zoneManager.GetCards(player, Zone.Battlefield);
                for (int i = 0; i < battlefield.Count; i++)
                {
                    var beneficiary = battlefield[i];
                    if (beneficiary == null || !beneficiary.IsAlive) continue;
                    var grants = GameBoard.LinkAuraSystem.GetGrantedEffectAuras(beneficiary);
                    for (int g = 0; g < grants.Count; g++)
                    {
                        var def = ResolveAuraGrant(grants[g].aura.effectRef);
                        if (def == null) continue;
                        yield return new RegisteredEffect
                        {
                            Effect = def,
                            Source = beneficiary,
                            Controller = beneficiary.GetController(),
                            FromAuraGrant = true,
                        };
                    }
                }
            }
        }

        /// <summary>effectRef → 运行时定义（缓存按库 DTO 实例失效，见 _auraGrantCache）：
        /// 悬空（装载期已 Warn）静默跳过；资格不符（AuraGrantRejectReason 单源，装载闸同口径）
        /// 在此兜底——防直改表夹带上栈成孤儿项。</summary>
        private EffectDefinition ResolveAuraGrant(string effectRef)
        {
            if (string.IsNullOrEmpty(effectRef)) return null;
            var dto = EffectsLibrary.Get(effectRef);
            if (dto == null) return null;
            if (_auraGrantCache.TryGetValue(effectRef, out var cached)
                && ReferenceEquals(cached.dto, dto))
                return cached.def;
            var data = EffectSlim.ToCardEffect(dto);
            var def = EffectEligibility.AuraGrantRejectReason(data) == null
                ? CardEffectConverter.ConvertOne(data, data.Id ?? "")
                : null;
            _auraGrantCache[effectRef] = (dto, def);
            return def;
        }

        /// <summary>
        /// 将待发触发效果放入栈。
        /// ⚠ 强制批拆轮（2026-09-16）仅在 FinishResolution 口径内生效（开窗/合成双 Pass）；
        /// 本路径（GameLoopController.Update 帧泵，空栈+可动阶段）推出的纯强制批仍走
        /// ProcessPriority（有动作则等）——未覆盖，口径见 StackEngine.FinishResolution。
        /// </summary>
        public void PutTriggersOnStack()
        {
            _stackEngine.ProcessTriggeredEffects();
        }

        public void OnTriggerResolved(EffectInstance effect)
        {
            // 触发效果结算后的清理
        }

        public void Clear()
        {
            _registeredEffects.Clear();
        }

        public void ClearAll()
        {
            Clear();
        }
    }

    /// <summary>
    /// 已注册的效果
    /// </summary>
    public class RegisteredEffect
    {
        public EffectDefinition Effect { get; set; }
        public Entity Source { get; set; }
        public Player Controller { get; set; }
        /// <summary>光环效果授予合成条目（2026-11-05 效果光环）：live-query 现算，非物化注册。
        /// true=受益者带失效/沉睡仍照发（拍板：光环域退出压制，仅绝缘体特判——绝缘体受益者
        /// 已在授予枚举处剔除，见 LinkAuraSystem.GetGrantedEffectAuras）。</summary>
        public bool FromAuraGrant;
    }
}
