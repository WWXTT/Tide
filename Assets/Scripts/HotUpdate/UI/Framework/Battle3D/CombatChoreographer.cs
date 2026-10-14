using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using Cysharp.Threading.Tasks;
using GameBoard;
using MonsterRig;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SynergyUI
{
    /// <summary>
    /// 战斗编舞器（2026-10-13 攻击演出定案）：3D 目标选择（寻路箭头）+ 战斗演出序列。
    ///
    /// 架构背景：引擎战斗是同步结算（宣言→横置→伤害→死亡同帧发完事件），表现层
    /// BattleFieldView 是快照对账。本编舞器先于对账订阅引擎事件，在攻击宣言时即 hold
    /// 参战双方（对账跳过其位置/横置侧倾/移除销毁），缓冲伤害事件，栈清空后组装
    /// CombatPlan 跑 UniTask 演出，结束后释放 hold、刷新对账钉死终态。
    ///
    /// 任务① 目标选择：点"攻击"进入——悬停对方单位/角色格实时画寻路箭头（BFS 最短路，
    /// BattlePathfinder 同源），左键点目标=路径固定（箭头冻结渐隐）并回传目标给宣言链路
    /// （本地 DeclareAttack / 网络 intent，下游不变）；右键/ESC/点空地取消。
    /// 任务② 演出：走位到目标临格→攻击前扑→目标受击（溅射第三方原地受击；角色目标=立柱脉动）
    /// →反击（以实际伤害事件存在性判定——守方横置/先击杀自然无）→死者碎裂（FallDown 散落）
    /// →停稳后螺旋吸入己方墓地漩涡消失（BattleFieldView.PlayDeathSuctionAsync）→幸存者
    /// 原路返回待机（回程平滑补 24° 横置侧倾）。
    ///
    /// 边界：宣攻被取消/响应窗内目标被移走→释放 hold 防幽灵；模型缺失占位胶囊=只动位姿不播
    /// 动作；AI 发起的攻击同走本编舞（事件驱动）；网络模式只接目标选择（事件订阅关闭——
    /// 快照驱动无本地战斗事件）；角色为攻击者=原地不动、目标侧受击（2026-10-13 裁定②）。
    /// 生命周期：BattleScreen.OnEnter 创建、OnExit Dispose（epoch 代际使在途演出自弃）。
    /// </summary>
    public sealed class CombatChoreographer : IDisposable
    {
        // ---- 演出调参 ----
        private const float WalkSpeed = 3f;          // 世界单位/秒（格心距≈1.5-1.73，约 2 格/秒）
        private const int ImpactDelayMs = 180;       // 攻击前扑到命中节拍
        private const int AttackBeatMs = 520;        // 一次攻击完整节拍（含缓冲）
        private const float ScatterSettle = 1f;      // 倒地散落停稳等待（秒）
        private const float SuctionSeconds = 0.75f;  // 漩涡吸走时长
        private const float TiltTapped = 24f;        // 与 BattleFieldView 横置侧倾同值

        private readonly BattleFieldView _field;
        private readonly Func<BattleViewData> _view;
        private readonly Action _requestRefresh;
        private readonly Func<Player> _selfPlayer;
        private readonly bool _listenEvents;
        private readonly BattleAttackArrow _arrow = new BattleAttackArrow();
        private bool _disposed;
        private int _epoch; // Dispose/新一轮开局代际：在途循环检测即弃

        // ---- 目标选择态 ----
        private BattleCardView _picker;                    // 发起攻击的我方单位
        private Action<BattleCardView> _onPicked;          // 回调：参数 null=对方角色
        private List<(int x, int z)> _hoverPath;           // 悬停路径缓存
        private int _hoverCell = -1;                       // 缓存键（格 index）

        // picker → 宣言的路径交接（寻路固定：确认时的路径供演出直接复用）
        private List<(int x, int z)> _pickedPath;
        private uint _pathAttackerId, _pathTargetId;

        // ---- 战斗捕获态 ----
        private bool _capturing;
        private bool _resolved;                           // 攻击已结算（伤害已发）：参战离场=预期死亡
        private Entity _attackerEntity, _targetEntity;
        private uint _attackerId, _targetId;               // 0=角色
        private bool _heroAttacker, _heroTarget, _attackingIsSelf;
        private readonly List<CombatDamageEvent> _damages = new List<CombatDamageEvent>();

        public bool IsTargeting => _picker != null;

        public CombatChoreographer(BattleFieldView field, Func<BattleViewData> view,
            Action requestRefresh, bool listenEvents, Func<Player> selfPlayer)
        {
            _field = field;
            _view = view;
            _requestRefresh = requestRefresh;
            _listenEvents = listenEvents;
            _selfPlayer = selfPlayer;
            if (!listenEvents) return;

            var bus = EventManager.Instance;
            bus.Subscribe<AttackDeclarationEvent>(OnAttackDeclared);
            bus.Subscribe<BlockDeclarationEvent>(OnBlockDeclared);
            bus.Subscribe<AttackResolvedEvent>(OnAttackResolved);
            bus.Subscribe<CombatDamageEvent>(OnCombatDamage);
            bus.Subscribe<CombatCancelledEvent>(OnCombatCancelled);
            bus.Subscribe<CardZoneChangeEvent>(OnCardZoneChanged);
            bus.Subscribe<TurnStartEvent>(OnTurnStart);
            bus.Subscribe<StackEmptyEvent>(OnStackEmpty);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _epoch++;
            CancelTargeting();
            _arrow.Dispose();
            ClearCapture();
            if (!_listenEvents) return;

            var bus = EventManager.Instance;
            bus.Unsubscribe<AttackDeclarationEvent>(OnAttackDeclared);
            bus.Unsubscribe<BlockDeclarationEvent>(OnBlockDeclared);
            bus.Unsubscribe<AttackResolvedEvent>(OnAttackResolved);
            bus.Unsubscribe<CombatDamageEvent>(OnCombatDamage);
            bus.Unsubscribe<CombatCancelledEvent>(OnCombatCancelled);
            bus.Unsubscribe<CardZoneChangeEvent>(OnCardZoneChanged);
            bus.Unsubscribe<TurnStartEvent>(OnTurnStart);
            bus.Unsubscribe<StackEmptyEvent>(OnStackEmpty);
        }

        // ======================================== 任务①：3D 目标选择（寻路箭头） ========================================

        /// <summary>进入攻击目标选择：悬停合法目标格实时画寻路箭头；确认回调回传
        /// 目标视图（null=对方角色）；取消不回调。</summary>
        public void BeginTargeting(BattleCardView attacker, Action<BattleCardView> onPicked)
        {
            if (_disposed || attacker == null || onPicked == null) return;
            CancelTargeting();
            _picker = attacker;
            _onPicked = onPicked;
            _hoverCell = -1;
            _hoverPath = null;
            Debug.Log($"[CombatChoreo] 目标选择开始：{attacker.Name}（{attacker.X},{attacker.Z}）"
                      + "——悬停对方单位/角色出寻路箭头，左键确认，右键/ESC 取消");
            TargetingLoopAsync(_epoch).Forget();
        }

        public void CancelTargeting()
        {
            if (_picker == null) return;
            _picker = null;
            _onPicked = null;
            _hoverPath = null;
            _hoverCell = -1;
            _arrow.Hide();
        }

        private async UniTaskVoid TargetingLoopAsync(int epoch)
        {
            while (!_disposed && epoch == _epoch && _picker != null)
            {
                if (!BattleBoardGrid.Ready) { CancelTargeting(); return; } // 拆台即取消
                if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
                {
                    Debug.Log("[CombatChoreo] 目标选择取消（右键/ESC）");
                    CancelTargeting();
                    return;
                }

                var cam = BattleStageDirector.WorldCam;
                if (cam == null) { _arrow.Hide(); await UniTask.Yield(); continue; }

                bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
                bool hasCell = RaycastCell(cam, out int x, out int z);
                bool valid = !overUI && hasCell && TryResolveTarget(x, z, out _, out _);

                if (Input.GetMouseButtonDown(0) && !overUI)
                {
                    if (valid)
                    {
                        await ConfirmTargetAsync(x, z);
                        return;
                    }
                    Debug.Log("[CombatChoreo] 目标选择取消（点击无效格）");
                    CancelTargeting();
                    return;
                }

                if (valid)
                {
                    int key = BoardMath.Index(x, z);
                    if (key != _hoverCell || _hoverPath == null)
                    {
                        _hoverCell = key;
                        _hoverPath = PathTo(x, z);
                    }
                    if (_hoverPath != null)
                        _arrow.Show(AttackerPos(), ToWorldPath(_hoverPath));
                }
                else
                {
                    _hoverCell = -1;
                    _hoverPath = null;
                    _arrow.Hide();
                }
                await UniTask.Yield();
            }
        }

        /// <summary>确认目标：箭头冻结渐隐（路径随确认固定），回传目标给宣言链路。</summary>
        private async UniTask ConfirmTargetAsync(int x, int z)
        {
            TryResolveTarget(x, z, out var target, out bool hero);
            // 悬停缓存命中才复用（同帧"移入+点击"场景缓存可能还是上一格——现算兜底）
            var path = _hoverCell == BoardMath.Index(x, z) && _hoverPath != null ? _hoverPath : PathTo(x, z);
            var attacker = _picker;
            var cb = _onPicked;
            _pickedPath = path;
            _pathAttackerId = attacker.RuntimeId;
            _pathTargetId = hero ? 0 : target.RuntimeId;

            _picker = null; // 先退出选择态（IsTargeting 即刻为 false），再交还控制权
            _onPicked = null;
            _hoverPath = null;
            _hoverCell = -1;

            await _arrow.FadeOutAsync(0.35f);
            string pathText = path != null && path.Count > 0
                ? string.Join("→", path.Select(c => $"({c.x},{c.z})")) : "原地";
            Debug.Log($"[CombatChoreo] 目标确认：{attacker.Name} → {(hero ? "对方角色" : target.Name)}"
                      + $"（攻击者{attacker.RuntimeId % 10000}）路径[{pathText}]");
            cb?.Invoke(hero ? null : target);
        }

        private Vector3 AttackerPos()
        {
            if (_field != null && _field.TryGetUnitBody(_picker.RuntimeId, out var wrapper, out _))
                return wrapper.position;
            return BattleBoardGrid.WorldPositionOf(_picker.X, _picker.Z) + new Vector3(0f, 0.05f, 0f);
        }

        /// <summary>悬停格 → 接近路径（目标=该格单位或角色格；占用集=视图数据）。</summary>
        private List<(int x, int z)> PathTo(int x, int z)
        {
            var d = _view?.Invoke();
            if (d == null || _picker == null) return null;
            var blocked = BattlePathfinder.BlockedSetOf(d, _picker.RuntimeId);
            return BattlePathfinder.FindApproach(_picker.X, _picker.Z, x, z, blocked);
        }

        /// <summary>格 → 合法攻击目标：对方未死单位 或 对方角色格（本地视角 CharacterCell(1)）。</summary>
        private bool TryResolveTarget(int x, int z, out BattleCardView unit, out bool hero)
        {
            unit = null;
            hero = false;
            var d = _view?.Invoke();
            if (d == null) return false;
            var heroCell = BoardLayout.CharacterCell(1);
            if (heroCell.x == x && heroCell.z == z) { hero = true; return true; }
            unit = (d.OppUnits ?? new List<BattleCardView>())
                .FirstOrDefault(c => c.X == x && c.Z == z && !c.IsDead);
            return unit != null;
        }

        /// <summary>tile-{x}-{z} BoxCollider 射线（与 BattleScreen.HandleFieldClick 同口径）。</summary>
        private static bool RaycastCell(Camera cam, out int x, out int z)
        {
            x = z = -1;
            RaycastHit hit;
            if (!Physics.Raycast(cam.ScreenPointToRay(Input.mousePosition), out hit, 300f)) return false;
            var name = hit.collider != null ? hit.collider.gameObject.name : "";
            if (!name.StartsWith("tile-")) return false;
            var parts = name.Split('-');
            return parts.Length == 3 && int.TryParse(parts[1], out x) && int.TryParse(parts[2], out z)
                   && BoardMath.InBounds(x, z);
        }

        // ======================================== 任务②：战斗事件捕获 → 演出 ========================================

        private void OnAttackDeclared(AttackDeclarationEvent e)
        {
            ClearCapture(); // 防御：上一捕获未清（不应发生）
            _capturing = true;
            _attackerEntity = e.Attacker;
            _targetEntity = e.Target;
            _attackerId = IdOf(e.Attacker);
            _targetId = IdOf(e.Target);
            _heroAttacker = _attackerId == 0;
            _heroTarget = _targetId == 0;
            var self = _selfPlayer?.Invoke();
            _attackingIsSelf = self != null && ReferenceEquals(e.AttackingPlayer, self);

            // 宣言即 hold：先于一切战斗状态变化（横置/伤害/死亡）锁定对账写入
            if (!_heroAttacker) _field.HoldUnit(_attackerId);
            if (!_heroTarget) _field.HoldUnit(_targetId);
            Debug.Log($"[CombatChoreo] 攻击宣言捕获：{NameOf(e.Attacker)} → {NameOf(e.Target)}"
                      + $"（{(_attackingIsSelf ? "我方" : "对方")}发起，hold 位姿对账）");
        }

        private void OnBlockDeclared(BlockDeclarationEvent e)
        {
            if (!_capturing) return;
            // 守卫拦截：目标改写为守卫者（hold 随之换人）
            if (!_heroTarget) _field.ReleaseUnit(_targetId);
            _targetEntity = e.Blocker;
            _targetId = IdOf(e.Blocker);
            _heroTarget = _targetId == 0;
            if (!_heroTarget) _field.HoldUnit(_targetId);
            Debug.Log($"[CombatChoreo] 守卫拦截：目标改写为 {NameOf(e.Blocker)}");
        }

        private void OnAttackResolved(AttackResolvedEvent e)
        {
            // 攻击已结算（伤害已发）：此后参战单位离场=SBA 预期死亡——hold 交演出（漩涡吸走），
            // 不再走"响应窗内被移走即释放"的守卫
            if (_capturing && ReferenceEquals(e.Attacker, _attackerEntity)) _resolved = true;
        }

        private void OnCombatDamage(CombatDamageEvent e)
        {
            if (_capturing) _damages.Add(e);
        }

        private void OnCombatCancelled(CombatCancelledEvent e)
        {
            if (!_capturing || !ReferenceEquals(e.Attacker, _attackerEntity)) return;
            Debug.Log("[CombatChoreo] 攻击取消（重检失败）——释放 hold");
            ClearCapture();
        }

        private void OnCardZoneChanged(CardZoneChangeEvent e)
        {
            // 响应窗内参战单位被移走（宣攻尚未结算）：释放其 hold 防幽灵卡位；
            // 结算后（_resolved）的离场是预期死亡，归死亡演出管
            if (!_capturing || _resolved || e.OldZone != Zone.Battlefield) return;
            uint id = IdOf(e.Card);
            if ((!_heroAttacker && id == _attackerId) || (!_heroTarget && id == _targetId))
            {
                _field.ReleaseUnit(id);
                Debug.Log($"[CombatChoreo] 参战单位 {NameOf(e.Card)} 响应窗内离场——释放 hold");
            }
        }

        private void OnTurnStart(TurnStartEvent _)
        {
            CancelTargeting(); // 回合切换：遗留的目标选择失效
        }

        private void OnStackEmpty(StackEmptyEvent _)
        {
            if (!_capturing) return;
            _capturing = false;
            AssembleAndPerform();
        }

        private void ClearCapture()
        {
            if (!_heroAttacker && _attackerId != 0) _field.ReleaseUnit(_attackerId);
            if (!_heroTarget && _targetId != 0) _field.ReleaseUnit(_targetId);
            _capturing = false;
            _resolved = false;
            _attackerEntity = _targetEntity = null;
            _attackerId = _targetId = 0;
            _damages.Clear();
        }

        // ======================================== 演出计划与序列 ========================================

        private sealed class CombatPlan
        {
            public uint AttackerId, TargetId;
            public bool HeroAttacker, HeroTarget;
            public int AttackerOwner = -1;
            public List<(int x, int z)> Path;
            public bool Counter, AttackerDead, TargetDead, AttackerTapped;
            public readonly List<uint> SplashIds = new List<uint>();
            public string AttackerName = "?", TargetName = "?";
            public string Summary => $"{AttackerName}→{TargetName}";
        }

        private void AssembleAndPerform()
        {
            var plan = new CombatPlan
            {
                AttackerId = _attackerId,
                TargetId = _targetId,
                HeroAttacker = _heroAttacker,
                HeroTarget = _heroTarget,
                AttackerOwner = _attackingIsSelf ? 0 : 1,
                AttackerDead = _attackerEntity is Card ac && !ac.IsAlive,
                TargetDead = _targetEntity is Card tc && !tc.IsAlive,
                AttackerTapped = _attackerEntity != null && _attackerEntity.IsTapped(),
                Counter = _damages.Any(d => ReferenceEquals(d.Attacker, _targetEntity)
                                            && ReferenceEquals(d.Defender, _attackerEntity)),
                AttackerName = NameOf(_attackerEntity),
                TargetName = NameOf(_targetEntity),
            };

            // 溅射/第三方受害：伤害事件里非主对（攻→守、守→攻）的防守方
            foreach (var d in _damages)
            {
                bool mainHit = ReferenceEquals(d.Attacker, _attackerEntity) && ReferenceEquals(d.Defender, _targetEntity);
                bool counterHit = ReferenceEquals(d.Attacker, _targetEntity) && ReferenceEquals(d.Defender, _attackerEntity);
                if (mainHit || counterHit || !(d.Defender is Card dc)) continue;
                uint id = dc.RuntimeId;
                if (id != plan.AttackerId && id != plan.TargetId && !plan.SplashIds.Contains(id))
                    plan.SplashIds.Add(id);
            }

            // 路径：picker 交接优先（寻路固定）；否则现算（AI/守卫改写场景）
            if (plan.HeroAttacker)
            {
                plan.Path = null; // 角色攻击：原地，目标侧受击（裁定②）
            }
            else if (_pickedPath != null && _pathAttackerId == plan.AttackerId && _pathTargetId == plan.TargetId)
            {
                plan.Path = _pickedPath;
                _pickedPath = null;
            }
            else
            {
                var from = CellOf(plan.AttackerId);
                var to = plan.HeroTarget
                    ? BoardLayout.CharacterCell(1 - plan.AttackerOwner)
                    : CellOf(plan.TargetId);
                plan.Path = from.HasValue && to.HasValue && BattleBoardGrid.Ready
                    ? BattlePathfinder.FindApproach(from.Value.x, from.Value.z, to.Value.x, to.Value.z,
                        BattlePathfinder.BlockedSetOf(_view?.Invoke(), plan.AttackerId))
                    : null;
            }

            Debug.Log($"[CombatChoreo] 演出计划：{plan.Summary} 走位[{(plan.Path != null ? plan.Path.Count.ToString() : "无")}]格"
                      + $" 反击={plan.Counter} 攻死={plan.AttackerDead} 守死={plan.TargetDead} 溅射={plan.SplashIds.Count}");
            PerformAsync(plan, _epoch).Forget();
        }

        private async UniTaskVoid PerformAsync(CombatPlan plan, int epoch)
        {
            Transform attW = null, tgtW = null;
            MonsterRigAnimator attAnim = null, tgtAnim = null;
            bool attBody = !plan.HeroAttacker && _field.TryGetUnitBody(plan.AttackerId, out attW, out attAnim);
            bool tgtBody = !plan.HeroTarget && _field.TryGetUnitBody(plan.TargetId, out tgtW, out tgtAnim);
            if (!attBody && !tgtBody)
                Debug.Log("[CombatChoreo] 演出跳过：参战双方均无场上的单位实体（或已被回收）");
            try
            {
                    int targetOwner = 1 - plan.AttackerOwner;
                    Vector3 homePos = attBody ? attW.position : Vector3.zero;

                    // ① 死亡遮蔽：引擎同步结算完才轮到演出——死者先"站着"，受击时刻再倒地碎裂
                    if (plan.TargetDead && tgtBody && tgtAnim != null) { tgtAnim.Revive(); tgtAnim.Play(MonsterAction.Breath); }
                    if (plan.AttackerDead && attBody && attAnim != null) { attAnim.Revive(); attAnim.Play(MonsterAction.Breath); }

                    // ② 走位到目标临格
                    if (attBody && plan.Path != null && plan.Path.Count > 0)
                    {
                        attAnim?.Play(MonsterAction.Walk);
                        if (!await WalkAsync(attW, ToWorldPath(plan.Path), epoch)) return;
                        attAnim?.Play(MonsterAction.Breath);
                    }

                    Vector3 tgtPos = tgtBody ? tgtW.position : HeroPosOf(targetOwner);
                    if (attBody && attW != null) Face2D(attW, tgtPos);

                    // ③ 攻击：前扑→命中（目标受击/角色立柱脉动/溅射第三方原地受击）
                    attAnim?.Play(MonsterAction.Attack);
                    await UniTask.Delay(ImpactDelayMs);
                    if (Stopped(epoch, attW, tgtW)) return;
                    if (tgtBody && tgtW != null) tgtAnim?.Play(MonsterAction.Hit);
                    else _field.HeroImpactPulseAsync(targetOwner).Forget();
                    foreach (var id in plan.SplashIds)
                        if (_field.TryGetUnitBody(id, out _, out var fa)) fa?.Play(MonsterAction.Hit);
                    await UniTask.Delay(AttackBeatMs - ImpactDelayMs);
                    if (Stopped(epoch, attW, tgtW)) return;

                    // ④ 反击（以实际反击伤害事件为准——守方横置/先击杀则无）
                    if (plan.Counter && !plan.TargetDead && attBody && tgtBody && attW != null && tgtW != null)
                    {
                        Face2D(tgtW, attW.position);
                        tgtAnim?.Play(MonsterAction.Attack);
                        await UniTask.Delay(ImpactDelayMs);
                        if (Stopped(epoch, attW, tgtW)) return;
                        attAnim?.Play(MonsterAction.Hit);
                        await UniTask.Delay(AttackBeatMs - ImpactDelayMs);
                    }

                    // ⑤ 死亡：碎裂散落→停稳→螺旋吸入己方墓地漩涡
                    if (plan.TargetDead && tgtBody)
                        await _field.PlayDeathSuctionAsync(plan.TargetId, targetOwner, playFall: true,
                            ScatterSettle, SuctionSeconds);
                    if (plan.AttackerDead && attBody)
                        await _field.PlayDeathSuctionAsync(plan.AttackerId, plan.AttackerOwner, playFall: true,
                            ScatterSettle, SuctionSeconds);

                    // ⑥ 幸存者原路返回待机（回程平滑补横置侧倾）
                    if (attBody && !plan.AttackerDead && attW != null)
                    {
                        attAnim?.Play(MonsterAction.Walk);
                        var back = new List<Vector3>();
                        if (plan.Path != null)
                            for (int i = plan.Path.Count - 1; i >= 0; i--)
                                back.Add(ToWorldAnchor(plan.Path[i]));
                        back.Add(homePos);
                        if (!await WalkAsync(attW, back, epoch, plan.AttackerTapped ? TiltTapped : 0f)) return;
                        attAnim?.Play(MonsterAction.Breath);
                        if (attW != null)
                            attW.rotation = Quaternion.Euler(0f, BattleFieldView.FacingYawOf(plan.AttackerOwner),
                                plan.AttackerTapped ? TiltTapped : 0f);
                    }
                    Debug.Log($"[CombatChoreo] 战斗演出完成：{plan.Summary}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[CombatChoreo] 演出异常（不阻塞对账终态）：{ex}");
                }
                finally
                {
                    if (!plan.HeroAttacker) _field.ReleaseUnit(plan.AttackerId);
                    if (!plan.HeroTarget) _field.ReleaseUnit(plan.TargetId);
                    _requestRefresh?.Invoke(); // 对账钉死终态
                }
        }

        /// <summary>演出中止判定：Dispose/代际失效 或 参战实体已被外部销毁（拆屏）。</summary>
        private bool Stopped(int epoch, Transform a, Transform b)
            => _disposed || epoch != _epoch || (a == null && b == null);

        // ======================================== 走位 ========================================

        /// <summary>沿折线移动 wrapper（恒速）：yaw 朝移动段方向、可选侧倾渐变（回程补横置）。
        /// 返回 false=中止（Dispose/销毁），调用方应放弃后续步骤。</summary>
        private async UniTask<bool> WalkAsync(Transform wrapper, List<Vector3> waypoints, int epoch, float tiltTo = 0f)
        {
            if (waypoints == null || waypoints.Count == 0) return true;
            Vector3 start = wrapper.position;
            float total = 0f;
            var cum = new float[waypoints.Count];
            for (int i = 0; i < waypoints.Count; i++)
            {
                total += Vector3.Distance(start, waypoints[i]);
                cum[i] = total;
                start = waypoints[i];
            }
            start = wrapper.position;
            if (total < 0.01f) return true;

            float t0 = Time.realtimeSinceStartup;
            float duration = total / WalkSpeed;
            float yaw = wrapper.rotation.eulerAngles.y;
            float tilt0 = wrapper.rotation.eulerAngles.z;
            var last = waypoints[waypoints.Count - 1];
            while (true)
            {
                if (wrapper == null || _disposed || epoch != _epoch) return false;
                float t = Mathf.Min((Time.realtimeSinceStartup - t0) / duration, 1f);
                float dist = t * total;

                Vector3 pos = last, segDir = Vector3.zero;
                for (int i = 0; i < waypoints.Count; i++)
                {
                    if (dist > cum[i]) continue;
                    float before = i == 0 ? 0f : cum[i - 1];
                    float segLen = cum[i] - before;
                    float f = segLen > 1e-5f ? (dist - before) / segLen : 0f;
                    Vector3 a = i == 0 ? start : waypoints[i - 1];
                    pos = Vector3.Lerp(a, waypoints[i], f);
                    segDir = waypoints[i] - a;
                    break;
                }

                segDir.y = 0f;
                if (segDir.sqrMagnitude > 1e-4f)
                {
                    float targetYaw = Mathf.Atan2(segDir.x, segDir.z) * Mathf.Rad2Deg;
                    yaw = Mathf.MoveTowardsAngle(yaw, targetYaw, 460f * Time.deltaTime);
                }
                wrapper.position = pos;
                wrapper.rotation = Quaternion.Euler(0f, yaw, Mathf.Lerp(tilt0, tiltTo, t));
                if (t >= 1f) break;
                await UniTask.Yield();
            }
            wrapper.position = last;
            return true;
        }

        private static void Face2D(Transform wrapper, Vector3 target)
        {
            Vector3 dir = target - wrapper.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 1e-4f)
                wrapper.rotation = Quaternion.Euler(0f, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, 0f);
        }

        private Vector3 HeroPosOf(int owner)
        {
            if (_field != null && _field.TryGetHeroRoot(owner, out var root) && root != null)
                return root.position;
            var cell = BoardLayout.CharacterCell(owner);
            return BattleBoardGrid.WorldPositionOf(cell.x, cell.z);
        }

        private static List<Vector3> ToWorldPath(List<(int x, int z)> cells)
        {
            var list = new List<Vector3>(cells.Count);
            foreach (var c in cells) list.Add(ToWorldAnchor(c));
            return list;
        }

        private static Vector3 ToWorldAnchor((int x, int z) c)
            => BattleBoardGrid.WorldPositionOf(c.x, c.z) + new Vector3(0f, 0.05f, 0f);

        // ======================================== 工具 ========================================

        private static uint IdOf(Entity e) => e is Card c ? c.RuntimeId : 0u;

        private static string NameOf(Entity e) => e is Card c ? c.ToString() : "角色";

        private (int x, int z)? CellOf(uint id)
        {
            var d = _view?.Invoke();
            if (d == null || id == 0) return null;
            foreach (var c in d.SelfUnits) if (c.RuntimeId == id) return (c.X, c.Z);
            foreach (var c in d.OppUnits) if (c.RuntimeId == id) return (c.X, c.Z);
            return null;
        }
    }
}
