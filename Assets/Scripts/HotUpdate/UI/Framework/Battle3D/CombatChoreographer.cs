using System;
using System.Collections.Generic;
using System.Linq;
using CardCore;
using Cysharp.Threading.Tasks;
using GameBoard;
using MonsterRig;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>
    /// 战斗编舞器（2026-10-13 攻击演出定案）：3D 目标选择（寻路箭头）+ 战斗演出序列。
    ///
    /// 架构背景：引擎战斗是同步结算（宣言→横置→伤害→死亡同帧发完事件），表现层
    /// BattleFieldView 是快照对账。本编舞器先于对账订阅引擎事件，在攻击宣言时即 hold
    /// 参战双方（对账跳过其位置/旋转/移除销毁），缓冲伤害事件，栈清空后组装
    /// CombatPlan 跑 UniTask 演出，结束后释放 hold、刷新对账钉死终态。
    ///
    /// 任务① 目标选择：点单位直接进入（2026-10-14 定案：无操作弹层——产元素全自动）——
    /// 3D 箭头以生物为起点随鼠标动态拉长（悬停合法目标箭头变绿），左键点目标=吸附目标格
    /// 冻结渐隐并回传目标给宣言链路（本地 DeclareAttack / 网络 intent，下游不变；确认时
    /// 另算 BFS 接近路径供演出走位）；右键/ESC/点空地取消。
    /// 任务② 演出：走位到目标临格→攻击前扑→目标受击（溅射第三方原地受击；角色目标=立柱脉动）
    /// →反击（以实际伤害事件存在性判定——守方横置/先击杀自然无）→死者倒地碎裂、原地沉降
    /// 片刻即收（BattleFieldView.PlayDeathExitAsync，2026-10-17 砍掉滚墓+自旋段）
    /// →幸存者原路返回待机（准备阶段回正由对账恢复；24° 横置侧倾 2026-10-15 退役）。
    /// 守卫拦截（2026-10-15 用户定案改版，中线会合退役）：守卫者走到被攻击者那一格挡伤害
    ///（挡位点向攻击侧偏移、与被保护者错开身位），攻击者接近原目标格邻格开打，战后各自
    /// 原路归位；规划失败回落旧演出（攻击者单方走到守卫者临格开打）。
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
        private const float GuardCoverOffset = 0.7f; // 守卫挡位偏移：原目标格心向攻击侧挪——与被保护者错开身位
        // 24° 横置侧倾（TiltTapped）2026-10-15 退役：wrapper 恒零；横置当晚再裁=呼吸也不停，只由光波熄表达

        private readonly BattleFieldView _field;
        private readonly Func<BattleViewData> _view;
        private readonly Action _requestRefresh;
        private readonly Func<Player> _selfPlayer;
        private readonly bool _listenEvents;
        private readonly BattleAttackArrow _arrow = new BattleAttackArrow();
        private readonly BattleAttackArrow _guardArrow = new BattleAttackArrow(); // 守卫拦截蓝箭（2026-10-15）
        private bool _arrowLive; // 演出期箭头逐帧跟踪开关（命中节拍关断→渐隐）
        private bool _disposed;
        private int _epoch; // Dispose/新一轮开局代际：在途循环检测即弃

        // ---- 目标选择态 ----
        private BattleCardView _picker;                    // 发起攻击的我方单位
        private Action<BattleCardView> _onPicked;          // 回调：参数 null=对方角色

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

        // ---- 守卫拦截捕获态（2026-10-15 挡位演出）----
        private bool _blocked;                             // 攻击被守卫拦截（目标已改写为守卫者）
        private (int x, int z)? _originalTargetCell;       // 原攻击目标格：守卫者挡位终点（踏此格挡伤害）

        // ---- 攻击瞄准贴片（2026-10-18 晚定案：只挂攻击——放置/发动美术已够）----
        // 守卫不改写贴片（用户定案：守卫者已瞬移赴原目标格，贴片原地即=守卫者脚下，转移=画蛇添足）
        private const uint ReticleId = 0x40000000u; // 伪键：真键=RuntimeId 小整数·角色伪键=MaxValue-i，中段不撞
        private bool _reticleUp;          // 贴片在盘
        private uint _reticleSubjectId;   // 贴片主体（0=角色格口径）——并行演出 finally 对账防代拆
        private uint _declaredTargetId;   // 宣言期原目标（守卫改写不覆盖）——贴片主体，演出收尾按此对账

        // ---- 战斗注视钉（2026-10-15 头跟随）：本编舞器下发过的注视单位集（ClearCapture 兜底全清） ----
        private readonly List<uint> _gazePinned = new List<uint>();

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
            _guardArrow.Dispose();
            _arrowLive = false;
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

        /// <summary>进入攻击目标选择：箭头以生物为起点随鼠标动态拉长，悬停合法目标变绿；
        /// 确认回调回传目标视图（null=对方角色）；取消不回调。</summary>
        public void BeginTargeting(BattleCardView attacker, Action<BattleCardView> onPicked)
        {
            if (_disposed || attacker == null || onPicked == null) return;
            CancelTargeting();
            _picker = attacker;
            _onPicked = onPicked;
            Debug.Log($"[CombatChoreo] 目标选择开始：{attacker.Name}（{attacker.X},{attacker.Z}）"
                      + "——箭头随鼠标，悬停合法目标变绿，左键确认，右键/ESC 取消；触摸=长按滑动（箭头终点=手指点位），抬起确定");
            TargetingLoopAsync(_epoch).Forget();
        }

        public void CancelTargeting()
        {
            if (_picker == null) return;
            _picker = null;
            _onPicked = null;
            _arrow.Hide();
            HideReticle(); // 悬停期贴片随选择取消撤（宣言后的贴片归捕获/演出链管）
        }

        private async UniTaskVoid TargetingLoopAsync(int epoch)
        {
            bool openingFrame = true; // 首帧吞点击（2026-10-14 单位直点进攻击）：开启箭头的点击
            while (!_disposed && epoch == _epoch && _picker != null) // 落在棋盘上，同帧确认触发
            {                                                         // 恒真——不吞则首轮即按「光标压在
                if (!BattleBoardGrid.Ready) { CancelTargeting(); return; } // 自格=非法」自取消
                if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
                {
                    Debug.Log("[CombatChoreo] 目标选择取消（右键/ESC）");
                    CancelTargeting();
                    return;
                }

                var cam = BattleStageDirector.WorldCam;
                if (cam == null) { _arrow.Hide(); await UniTask.Yield(); continue; }

                // 鼠标/触摸统一（2026-10-18 安卓滑动输入·BattlePointer）：裁决/箭头终点=指针
                // 落点（触摸不偏置——手指盖住箭头时合法变绿经箭身可观察）；确认=左键按下/触摸抬起
                BattlePointer.Sample();
                bool touch = BattlePointer.TouchTracked;
                bool overUI = BattlePointer.OverUI;
                var aim = BattlePointer.ConfirmPressed ? BattlePointer.ConfirmPoint : BattlePointer.AimPoint;
                bool hasCell = RaycastCell(cam, aim, out int x, out int z);
                BattleCardView target = null; // 先声明再传 out：&& 短路时下方使用点不受未赋值限制
                bool hero = false;
                bool valid = !overUI && hasCell && TryResolveTarget(x, z, out target, out hero);

                // 箭头以生物为起点随指针动态拉长（终点=指针地面投影）；攻击=红，悬停/滑动悬停
                // 合法目标变绿+脚下瞄准贴片（2026-10-18；触摸无悬停——未滑动时全隐藏）
                if ((!touch || BattlePointer.SlideActive)
                    && MouseGroundPoint(cam, aim, out Vector3 aimPt))
                {
                    _arrow.Show(AttackerPos(), aimPt, valid
                        ? BattleAttackArrow.ValidSkin : BattleAttackArrow.AttackSkin);
                    if (valid)
                    {
                        if (hero) ShowReticleAt(0u, x, z); // 角色格=悬停格本身
                        else ShowReticleAt(target.RuntimeId, target.X, target.Z);
                    }
                    else HideReticle();
                }
                else
                {
                    _arrow.Hide();
                    HideReticle();
                }

                if (!openingFrame && BattlePointer.ConfirmPressed && !overUI)
                {
                    if (valid)
                    {
                        await ConfirmTargetAsync(x, z);
                        return;
                    }
                    if (!BattlePointer.ConfirmWasSlide)
                    {
                        Debug.Log("[CombatChoreo] 目标选择取消（点击无效格）");
                        CancelTargeting();
                        return;
                    }
                    Debug.Log("[CombatChoreo] 滑动收在非法目标——只收滑动，选择继续（触摸探索口径）");
                }
                openingFrame = false;
                await UniTask.Yield();
            }
        }

        /// <summary>确认目标：箭头吸附目标格冻结渐隐，回传目标给宣言链路；接近路径现算供演出走位。</summary>
        private async UniTask ConfirmTargetAsync(int x, int z)
        {
            TryResolveTarget(x, z, out var target, out bool hero);
            var path = PathTo(x, z);
            var attacker = _picker;
            var cb = _onPicked;
            var from = AttackerPos();
            _pickedPath = path;
            _pathAttackerId = attacker.RuntimeId;
            _pathTargetId = hero ? 0 : target.RuntimeId;

            _picker = null; // 先退出选择态（IsTargeting 即刻为 false），再交还控制权
            _onPicked = null;

            _arrow.Show(from, BattleBoardGrid.WorldPositionOf(x, z), BattleAttackArrow.ValidSkin); // 吸附目标格（选对了=绿）再冻结渐隐
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

        /// <summary>tile-{x}-{z} BoxCollider 射线（与 BattleScreen.HandleFieldClick 同口径；点=裁决落点）。</summary>
        private static bool RaycastCell(Camera cam, Vector2 screenPoint, out int x, out int z)
        {
            x = z = -1;
            RaycastHit hit;
            if (!Physics.Raycast(cam.ScreenPointToRay(screenPoint), out hit, 300f)) return false;
            var name = hit.collider != null ? hit.collider.gameObject.name : "";
            if (!name.StartsWith("tile-")) return false;
            var parts = name.Split('-');
            return parts.Length == 3 && int.TryParse(parts[1], out x) && int.TryParse(parts[2], out z)
                   && BoardMath.InBounds(x, z);
        }

        /// <summary>指针 → 棋盘地平面（y=0）世界点：箭头终点随指针动态拉长的投影源
        /// （不依赖 tile 命中——指到棋盘外也拉得出）。BattleScreen 守卫箭头共用此投影。</summary>
        public static bool MouseGroundPoint(Camera cam, Vector2 screenPoint, out Vector3 point)
        {
            point = Vector3.zero;
            var ray = cam.ScreenPointToRay(screenPoint);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (!plane.Raycast(ray, out float enter) || enter <= 0f || enter > 300f) return false;
            point = ray.GetPoint(enter);
            return true;
        }

        // ======================================== 任务②：战斗事件捕获 → 演出 ========================================

        private void OnAttackDeclared(AttackDeclarationEvent e)
        {
            ClearCapture(); // 上一捕获收尾（已交接开演的捕获属常态——并行演出，见 ClearCapture 释放守卫）
            _capturing = true;
            _attackerEntity = e.Attacker;
            _targetEntity = e.Target;
            _attackerId = IdOf(e.Attacker);
            _targetId = IdOf(e.Target);
            _heroAttacker = _attackerId == 0;
            _heroTarget = _targetId == 0;
            _declaredTargetId = _targetId; // 宣言期原目标：贴片主体（守卫改写 _targetId 不覆盖此值）
            var self = _selfPlayer?.Invoke();
            _attackingIsSelf = self != null && ReferenceEquals(e.AttackingPlayer, self);

            // 宣言即 hold：先于一切战斗状态变化（横置/伤害/死亡）锁定对账写入
            if (!_heroAttacker) _field.HoldUnit(_attackerId);
            if (!_heroTarget) _field.HoldUnit(_targetId);
            // 数值节拍（2026-10-15 定案）：攻击结算同帧改血——展示等命中时刻。宣言即冻结
            // 全部单位头顶数值（溅射/第三方受害同批伤害一起盖住），命中/反击节拍逐个释放，
            // 演出收尾与取消路径全量兜底（ClearCapture/finally）
            _field.HoldAllUnitStats();

            // 攻击目标箭头（2026-10-15 用户定案：攻击=红）：宣言即显（含对手/AI 攻击——此前只有
            // 我方选择期有箭头，对手攻击全盲）；守卫拦截时 OnBlockDeclared 改指拦截者+补蓝箭；
            // 演出走位期 TrackArrowsAsync 逐帧跟踪、命中节拍渐隐
            if (CombatPosOf(_attackerId, _heroAttacker, _attackingIsSelf ? 0 : 1, out var declFrom)
                && CombatPosOf(_targetId, _heroTarget, _attackingIsSelf ? 1 : 0, out var declTo))
                _arrow.Show(declFrom, declTo, BattleAttackArrow.AttackSkin);
            else
                Debug.Log("[CombatChoreo] 宣言箭头未放置：参战方位姿缺失（wrapper/视图格均无）——对手回合箭头投诉取证用");
            // 战斗注视（2026-10-15 头跟随）：攻守双方对视——攻击者看攻击对象（守卫拦截时
            // _targetId 已改写为拦截者，供应器动态解析即自动转看拦截者）；守方看攻击者
            if (!_heroAttacker) SetGaze(_attackerId, () => CombatGazeOf(true));
            if (!_heroTarget) SetGaze(_targetId, () => CombatGazeOf(false));
            ShowReticleAtTarget(); // 瞄准贴片钉在受击者脚下（含对手/AI 攻击）——守卫拦截不转移（守卫者瞬移赴原目标格）
            Debug.Log($"[CombatChoreo] 攻击宣言捕获：{NameOf(e.Attacker)} → {NameOf(e.Target)}"
                      + $"（{(_attackingIsSelf ? "我方" : "对方")}发起，hold 位姿对账）");
        }

        private void OnBlockDeclared(BlockDeclarationEvent e)
        {
            if (!_capturing) return;
            // 原目标格速记（改写前取，仅首次拦截记录——多次拦截共用最初目标做挡位参照）：
            // 角色目标=防守方角色格
            if (!_blocked)
            {
                int defOwner = _attackingIsSelf ? 1 : 0;
                _originalTargetCell = _heroTarget
                    ? BoardLayout.CharacterCell(defOwner)
                    : CellOf(_targetId);
                _blocked = true;
            }
            // 守卫拦截：目标改写为守卫者（hold/战斗注视随之换人）
            if (!_heroTarget)
            {
                _field.ReleaseUnit(_targetId);
                _field.ClearCombatGaze(_targetId); // 原目标（含前序拦截者）解除对视回待机
                _gazePinned.Remove(_targetId);
            }
            _targetEntity = e.Blocker;
            _targetId = IdOf(e.Blocker);
            _heroTarget = _targetId == 0;
            if (!_heroTarget)
            {
                _field.HoldUnit(_targetId);
                SetGaze(_targetId, () => CombatGazeOf(false)); // 拦截者上场即与攻击者对视
            }

            // 守卫拦截双箭（2026-10-15 定案：守卫=蓝）：红箭改指拦截者、蓝箭=守卫者→攻击者
            //（瞄准贴片不转移：守卫者瞬移赴原目标格挡打，贴片原地即=守卫者脚下）
            if (CombatPosOf(_attackerId, _heroAttacker, _attackingIsSelf ? 0 : 1, out var gFrom)
                && CombatPosOf(_targetId, _heroTarget, _attackingIsSelf ? 1 : 0, out var gTo))
            {
                _arrow.Show(gFrom, gTo, BattleAttackArrow.AttackSkin);
                _guardArrow.Show(gTo, gFrom, BattleAttackArrow.GuardSkin);
            }
            Debug.Log($"[CombatChoreo] 守卫拦截：目标改写为 {NameOf(e.Blocker)}"
                      + $"（挡位=赴原目标({_originalTargetCell?.x},{_originalTargetCell?.z})格）");
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
            if (ReplayDriven) return; // 回放导演在场（2026-10-19 逆结算步进）：捕获位保留——攻击步开演归它驱动（PerformCapturedNow 消费）
            _capturing = false;
            AssembleAndPerform();
        }

        /// <summary>回放驱动模式（2026-10-19 逆结算步进定案·战斗严格按结算序）：置位=StackEmpty
        /// 不自动开演，战斗演出改由 BattleResolutionDirector 在回放到攻击步时调 PerformCapturedNow
        /// 驱动（效果步逐个演完、轮到攻击步才播战斗）。未置位=旧行为（栈空即演）。</summary>
        public bool ReplayDriven { get; set; }

        /// <summary>回放驱动口（2026-10-19）：回放到攻击步时由导演调——把已捕获的战斗按演出计划
        /// 开演（占用视觉门，导演随后等 WaitVisualsIdle）。无捕获=空操作。</summary>
        public void PerformCapturedNow()
        {
            if (!_capturing) return;
            _capturing = false;
            AssembleAndPerform();
        }

        private void ClearCapture()
        {
            ClearGazePins(); // 战斗注视全清（取消/Dispose/新宣言防漏）
            _field.ReleaseAllHeldStats(); // 数值节拍兜底：取消路径（攻击未演出）放行冻结（OnAttackDeclared 随后重冻）
            _arrowLive = false;
            _arrow.Hide();
            _guardArrow.Hide();
            HideReticle(); // 取消/Dispose/新宣言吞并：贴片撤（新宣言随后 OnAttackDeclared 重钉）
            // hold 释放守卫（2026-10-18 并行演出修复）：仅捕获未交接（_capturing——取消/新宣言吞并
            // 尚未开演的捕获）时释放；栈空交接后（_capturing=false）hold 归在途演出的 finally 管，
            // 此处释放会偷走第二场的持有 → 其死亡目标被 RemoveStale 提前销毁（模型命中前消失）
            if (_capturing)
            {
                if (!_heroAttacker && _attackerId != 0) _field.ReleaseUnit(_attackerId);
                if (!_heroTarget && _targetId != 0) _field.ReleaseUnit(_targetId);
            }
            _capturing = false;
            _resolved = false;
            _blocked = false;
            _originalTargetCell = null;
            _attackerEntity = _targetEntity = null;
            _attackerId = _targetId = 0;
            _declaredTargetId = 0;
            _damages.Clear();
        }

        // ======================================== 攻击瞄准贴片（2026-10-18 晚） ========================================

        /// <summary>瞄准贴片钉到宣言期目标脚下（单位格或防守方角色格；视图格缺失静默跳过）。</summary>
        private void ShowReticleAtTarget()
        {
            if (_heroTarget)
            {
                var hc = BoardLayout.CharacterCell(_attackingIsSelf ? 1 : 0);
                ShowReticleAt(0u, hc.x, hc.z);
            }
            else
            {
                var cell = CellOf(_targetId);
                if (cell.HasValue) ShowReticleAt(_targetId, cell.Value.x, cell.Value.z);
            }
        }

        /// <summary>贴片上盘/挪位（主体=单位 RuntimeId 或 0=角色格；同主体幂等）。</summary>
        private void ShowReticleAt(uint subjectId, int x, int z)
        {
            if (_reticleUp && _reticleSubjectId == subjectId) return;
            BattleTargetReticle.Hide(ReticleId); // 同键早退不挪位——换主体先下盘再钉新格
            BattleTargetReticle.Show(ReticleId, x, z);
            _reticleUp = true;
            _reticleSubjectId = subjectId;
        }

        /// <summary>撤贴片（未上盘=无操作）。</summary>
        private void HideReticle()
        {
            if (!_reticleUp) return;
            BattleTargetReticle.Hide(ReticleId);
            _reticleUp = false;
        }

        /// <summary>只撤还钉在指定主体上的贴片——并行演出各自收场互不代拆（主体 0=角色格口径）。</summary>
        private void HideReticleIfSubject(uint subjectId)
        {
            if (_reticleUp && _reticleSubjectId == subjectId) HideReticle();
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

            // 守卫拦截挡位（2026-10-15 改版：中线会合退役）：守卫者踏上被攻击者那格挡打、打完原路归位
            public bool Blocked;                        // 本次攻击被守卫拦截（Target=守卫者）
            public bool TargetTapped;                   // 守卫者横置（战斗后支付——仅演出日志记录，2026-10-15 不再停呼吸）
            public List<(int x, int z)> BlockerPath;    // 守卫者→原目标格挡位（null=规划失败回落旧演出）
            public uint ReticleSubjectId;               // 瞄准贴片主体=宣言期原目标（0=角色格；守卫改写不覆盖——收尾对账用）
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
                TargetTapped = _targetEntity != null && _targetEntity.IsTapped(),
                Blocked = _blocked,
                ReticleSubjectId = _declaredTargetId,
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

            // 路径：守卫拦截=守卫者赴原目标格挡位（2026-10-15）优先；picker 交接次之（寻路固定）；
            // 否则现算（AI/未拦截场景）
            if (plan.HeroAttacker)
            {
                plan.Path = null; // 角色攻击：原地，目标侧受击（裁定②）
            }
            else if (plan.Blocked && !plan.HeroTarget
                     && TryPlanGuardCover(plan, out var guardAttPath, out var guardBlkPath))
            {
                plan.Path = guardAttPath;
                plan.BlockerPath = guardBlkPath;
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
                      + (plan.Blocked ? $" 守卫挡位[{(plan.BlockerPath != null ? plan.BlockerPath.Count.ToString() : "回落旧演出")}]格" : "")
                      + $" 反击={plan.Counter} 攻死={plan.AttackerDead} 守死={plan.TargetDead} 溅射={plan.SplashIds.Count}"
                      + $" 攻横置={plan.AttackerTapped} 守横置={plan.TargetTapped}");
            PerformAsync(plan, _epoch).Forget();
        }

        /// <summary>守卫挡位规划（2026-10-15 用户定案：不再中线会合——守卫者走到被攻击者那一格
        /// 挡伤害）：攻击者=常规接近（原目标格的空闲邻格，FindApproach）；守卫者=先走近原目标
        /// 空闲邻格、再踏上原目标格（追加终点格构造——FindApproach 的 BFS 不入占用格，途经
        /// 不穿原目标）。视图数据/参战格缺失=失败（调用方回落旧演出：攻击者单方走到守卫者
        /// 临格开打）。</summary>
        private bool TryPlanGuardCover(CombatPlan plan, out List<(int x, int z)> attPath, out List<(int x, int z)> blkPath)
        {
            attPath = blkPath = null;
            if (!_originalTargetCell.HasValue) return false;
            var d = _view?.Invoke();
            if (d == null || !BattleBoardGrid.Ready) return false;
            var fromAtt = CellOf(plan.AttackerId);
            var fromBlk = CellOf(plan.TargetId);
            if (!fromAtt.HasValue || !fromBlk.HasValue) return false;

            var victim = _originalTargetCell.Value;
            attPath = BattlePathfinder.FindApproach(fromAtt.Value.x, fromAtt.Value.z, victim.x, victim.z,
                BattlePathfinder.BlockedSetOf(d, plan.AttackerId));
            blkPath = BattlePathfinder.FindApproach(fromBlk.Value.x, fromBlk.Value.z, victim.x, victim.z,
                BattlePathfinder.BlockedSetOf(d, plan.TargetId));
            blkPath.Add(victim); // 踏上被攻击者那一格（演出终点向攻击侧偏移错开身位，见 GuardCoverPoints）
            string attEnd = attPath.Count > 0
                ? $"({attPath[attPath.Count - 1].x},{attPath[attPath.Count - 1].z})" : "原地";
            Debug.Log($"[CombatChoreo] 守卫挡位：攻击者{attEnd} 守卫者→({victim.x},{victim.z})原目标格挡伤害");
            return true;
        }

        private async UniTaskVoid PerformAsync(CombatPlan plan, int epoch)
        {
            _field.BeginVisual(); // 视觉在途门（2026-10-14 人机节奏）：演出期间人机局 AI 动作等待
            Transform attW = null, tgtW = null;
            MonsterRigAnimator attAnim = null, tgtAnim = null;
            bool attBody = !plan.HeroAttacker && _field.TryGetUnitBody(plan.AttackerId, out attW, out attAnim);
            bool tgtBody = !plan.HeroTarget && _field.TryGetUnitBody(plan.TargetId, out tgtW, out tgtAnim);
            // 宣言期箭头交接：走位/挡位移动中逐帧跟踪（红=攻击→目标，蓝=守卫→攻击者）
            _arrowLive = true;
            TrackArrowsAsync(plan, attW, tgtW, epoch).Forget();
            if (!attBody && !tgtBody)
                Debug.Log("[CombatChoreo] 演出跳过：参战双方均无场上的单位实体（或已被回收）");
                try
                {
                    int targetOwner = 1 - plan.AttackerOwner;
                    Vector3 homePos = attBody ? attW.position : Vector3.zero;
                    Vector3 blkHomePos = tgtBody ? tgtW.position : Vector3.zero; // 守卫者原位（挡位后归位）

                    // ① 死亡遮蔽：引擎同步结算完才轮到演出——死者先"站着"，受击时刻再倒地碎裂
                    if (plan.TargetDead && tgtBody && tgtAnim != null) { tgtAnim.Revive(); tgtAnim.Play(MonsterAction.Breath); }
                    if (plan.AttackerDead && attBody && attAnim != null) { attAnim.Revive(); attAnim.Play(MonsterAction.Breath); }

                    // ② 走位：守卫拦截（2026-10-17 用户定案：守卫者**瞬移**赴挡——攻击者走过去
                    //    站在原地等守卫走来的观感差，改为只有攻击者走位，守卫者直接现身挡位；
                    //    同晚补现身动画=召唤弹出 SummonPop（程序动画现成件复用：缩放从无到有+
                    //    上方速升缓落 0.9s，与攻击者走位并行，命中前等完全显形））
                    //    + 攻击者接近原目标邻格。
                    //    走位折线（2026-10-17 缝走定案：不再走空地格心——沿两格交界线）：
                    //    途经=格间缝点，末点=接近格格心（战斗站位）；起点格缺失回落旧格心折线
                    var attStartCell = CellOf(plan.AttackerId);
                    List<Vector3> attPts = plan.Path != null && plan.Path.Count > 0
                        ? (attStartCell.HasValue
                            ? SeamWaypoints(plan.Path, attStartCell.Value)
                            : ToWorldPath(plan.Path))
                        : null;
                    List<Vector3> blkPts = null; // 守卫挡位折线（⑦ 归位原路缝返回复用）
                    if (plan.Blocked && plan.BlockerPath != null && attBody && tgtBody)
                    {
                        attAnim?.Play(MonsterAction.Walk);
                        blkPts = GuardCoverPoints(plan, attW, CellOf(plan.TargetId));
                        float popDeadline = -1f;
                        if (blkPts != null && blkPts.Count > 0)
                        {
                            tgtW.position = blkPts[blkPts.Count - 1]; // 瞬移挡位（原目标格心向攻击侧偏移错身）
                            tgtAnim?.Revive();
                            tgtAnim?.Play(MonsterAction.Breath);
                            tgtAnim?.PlaySummonPop(); // 现身：召唤弹出（0.9s，随走位并行）
                            popDeadline = Time.realtimeSinceStartup + RigMotionTuning.PopDuration;
                        }
                        if (!await WalkAsync(attW, attPts, epoch,
                                modelYaw: BattleFieldView.FacingYawOf(plan.AttackerOwner)))
                        {
                            Debug.Log($"[CombatChoreo] 演出中止@攻击者走位：{plan.Summary}");
                            return;
                        }
                        attAnim?.Play(MonsterAction.Breath);
                        while (popDeadline > 0f && Time.realtimeSinceStartup < popDeadline)
                        {
                            if (Stopped(epoch, attW, tgtW)) break; // 中止/销毁不再干等
                            await UniTask.Yield();
                        }
                        if (Stopped(epoch, attW, tgtW))
                        {
                            Debug.Log($"[CombatChoreo] 演出中止@挡位就位（disposed={_disposed} epochEq={epoch == _epoch} attNull={attW == null} tgtNull={tgtW == null}）：{plan.Summary}");
                            return;
                        }
                        Face2D(tgtW, attW.position, BattleFieldView.FacingYawOf(targetOwner)); // 守卫者转面攻击者（挡位迎敌）
                    }
                    else if (attBody && attPts != null && attPts.Count > 0)
                    {
                        attAnim?.Play(MonsterAction.Walk);
                        if (!await WalkAsync(attW, attPts, epoch,
                                modelYaw: BattleFieldView.FacingYawOf(plan.AttackerOwner)))
                        {
                            Debug.Log($"[CombatChoreo] 演出中止@攻击者走位：{plan.Summary}");
                            return;
                        }
                        attAnim?.Play(MonsterAction.Breath);
                    }

                    // 参战者存活重估（2026-10-17 并行演出共享单位修复）：attBody/tgtBody 是方法开头
                    // 快照——两场演出并行时，本演出的目标可能已被另一场的死亡动画（fire-and-forget
                    // RetireUnit）销毁 wrapper——假空即降级：跳过该侧表现（受击/反击/朝向），本方照常收尾
                    attBody = attBody && attW != null;
                    tgtBody = tgtBody && tgtW != null;

                    Vector3 tgtPos = tgtBody ? tgtW.position : HeroPosOf(targetOwner);
                    if (attBody && attW != null) Face2D(attW, tgtPos, BattleFieldView.FacingYawOf(plan.AttackerOwner));

                    // ③ 攻击：前扑→命中（目标受击/角色立柱脉动/溅射第三方原地受击）
                    attAnim?.Play(MonsterAction.Attack);
                    await DelayChainAware(ImpactDelayMs); // 连锁冻结感知节拍（2026-10-17）
                    _arrowLive = false; // 命中节拍：目标箭头使命达成，跟踪器收尾渐隐
                    if (Stopped(epoch, attW, tgtW))
                    {
                        Debug.Log($"[CombatChoreo] 演出中止@命中节拍（disposed={_disposed} epochEq={epoch == _epoch} attNull={attW == null} tgtNull={tgtW == null}）：{plan.Summary}");
                        return;
                    }
                    attBody = attBody && attW != null; // 节拍中途同样可能被并行演出销毁——再重估
                    tgtBody = tgtBody && tgtW != null;
                    if (tgtBody && tgtW != null) tgtAnim?.Play(MonsterAction.Hit);
                    else if (plan.HeroTarget) _field.HeroImpactPulseAsync(targetOwner).Forget();
                    // 单位目标 wrapper 缺失（并行演出降级，见上）＝跳过该侧表现——不回落角色立柱
                    // 脉冲（2026-10-18 修：被攻击单位提前消失时角色立柱会莫名"受击"一次）
                    foreach (var id in plan.SplashIds)
                    {
                        if (_field.TryGetUnitBody(id, out _, out var fa)) fa?.Play(MonsterAction.Hit);
                        _field.ReleaseUnitStats(id); // 数值节拍：溅射方伤害随主命中落地
                    }
                    _field.ReleaseUnitStats(plan.TargetId); // 数值节拍：目标伤害此刻滚动显示
                    await DelayChainAware(AttackBeatMs - ImpactDelayMs); // 连锁冻结感知节拍（2026-10-17）
                    if (Stopped(epoch, attW, tgtW))
                    {
                        Debug.Log($"[CombatChoreo] 演出中止@攻击节拍尾：{plan.Summary}");
                        return;
                    }

                    // ④ 反击（以实际反击伤害事件为准——守方横置则无）。2026-10-17 双向同时定案：
                    // 守方被打死也照播反击（临死反击）——旧 !TargetDead 条件是顺序结算口径残留，
                    // 它把攻击者的掉血冻到归位后兜底放行（"走回去才扣血"观感）
                    if (plan.Counter && attBody && tgtBody && attW != null && tgtW != null)
                    {
                        Face2D(tgtW, attW.position, BattleFieldView.FacingYawOf(targetOwner));
                        tgtAnim?.Play(MonsterAction.Attack);
                        await DelayChainAware(ImpactDelayMs); // 连锁冻结感知节拍（2026-10-17）
                        if (Stopped(epoch, attW, tgtW))
                        {
                            Debug.Log($"[CombatChoreo] 演出中止@反击节拍：{plan.Summary}");
                            return;
                        }
                        attAnim?.Play(MonsterAction.Hit);
                        _field.ReleaseUnitStats(plan.AttackerId); // 数值节拍：反击伤害此刻滚动显示
                        await DelayChainAware(AttackBeatMs - ImpactDelayMs); // 连锁冻结感知节拍（2026-10-17）
                    }

                    // ⑤⑥⑦ 收尾段并行（2026-10-17 用户定案：表现者不是同一个单位就不排队——
                    //    归位走回互不等待；同一单位的互斥由条件天然保证（死者不归位））。
                    // ⑤ 死亡=可忽略表演（同晚定案：纯装饰不占门不等待——逻辑早已入墓，退场动画
                    //    期间后续动作/墓地相关效果照常；fire-and-forget 与一切并行）
                    if (plan.TargetDead && tgtBody)
                        _field.PlayDeathExitAsync(plan.TargetId, targetOwner).Forget();
                    if (plan.AttackerDead && attBody)
                        _field.PlayDeathExitAsync(plan.AttackerId, plan.AttackerOwner).Forget();

                    var tails = new List<UniTask>();

                    // ⑥ 幸存者原路返回待机（2026-10-15 晚裁定：横置不再停呼吸，归位恒播呼吸，
                    //    可行动性只看脚下光波）；回程解除对视（攻击已结束，头回待机注视）
                    if (attBody && !plan.AttackerDead && attW != null)
                    {
                        _field.ClearCombatGaze(plan.AttackerId);
                        var back = new List<Vector3>();
                        if (attPts != null)
                            for (int i = attPts.Count - 1; i >= 0; i--)
                                back.Add(attPts[i]); // 原路缝返回（首点=站位≈当前位）
                        back.Add(homePos);
                        tails.Add(ReturnAsync(attW, attAnim, back, epoch,
                            BattleFieldView.FacingYawOf(plan.AttackerOwner), "攻击者归位", plan.Summary));
                    }

                    // ⑦ 守卫者归位（挡位演出收尾）：幸存守卫者原路返回自格（末格=原目标格不回踏
                    //    ——被保护者仍站其上；横置不停呼吸，同⑥）；回程同解除对视
                    if (plan.Blocked && blkPts != null && tgtBody && !plan.TargetDead && tgtW != null)
                    {
                        _field.ClearCombatGaze(plan.TargetId);
                        var blkBack = new List<Vector3>();
                        for (int i = blkPts.Count - 1; i >= 0; i--)
                            blkBack.Add(blkPts[i]); // 原路缝返回（首点=挡位点≈当前位）
                        blkBack.Add(blkHomePos);
                        tails.Add(ReturnAsync(tgtW, tgtAnim, blkBack, epoch,
                            BattleFieldView.FacingYawOf(targetOwner), "守卫者归位", plan.Summary));
                    }
                    if (tails.Count > 0)
                        await UniTask.WhenAll(tails);
                    Debug.Log($"[CombatChoreo] 战斗演出完成：{plan.Summary}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[CombatChoreo] 演出异常（不阻塞对账终态）：{ex}");
                }
                finally
                {
                    _field.EndVisual(); // 视觉在途门：演出结束（含中止/异常路径）归零
                    _field.ReleaseAllHeldStats(); // 数值节拍兜底：未逐个释放的冻结全量放行（终值滚动/直落）
                    _arrowLive = false; // 箭头兜底收（正常路径命中已渐隐；中止路径即时藏）
                    _arrow.Hide();
                    _guardArrow.Hide();
                    HideReticleIfSubject(plan.ReticleSubjectId); // 演出收场撤贴片——按宣言期主体对账（守卫不改写），并行演出不代拆
                    if (!plan.HeroAttacker) _field.ClearCombatGaze(plan.AttackerId); // 注视兜底清（正常路径⑥⑦已清）
                    if (!plan.HeroTarget) _field.ClearCombatGaze(plan.TargetId);
                    if (!plan.HeroAttacker) _field.ReleaseUnit(plan.AttackerId);
                    if (!plan.HeroTarget) _field.ReleaseUnit(plan.TargetId);
                    _requestRefresh?.Invoke(); // 对账钉死终态
                }
        }

        /// <summary>演出中止判定：Dispose/代际失效 或 参战实体已被外部销毁（拆屏）。</summary>
        private bool Stopped(int epoch, Transform a, Transform b)
            => _disposed || epoch != _epoch || (a == null && b == null);

        /// <summary>幸存者归位尾段（2026-10-17 收尾并行化拆出）：走回原格→呼吸待机→wrapper 恒零。
        /// 中止（Dispose/销毁）只放弃本段——并行收尾互不牵连，finally 兜底统一收场。</summary>
        private async UniTask ReturnAsync(Transform wrapper, MonsterRigAnimator anim,
            List<Vector3> path, int epoch, float yaw, string label, string summary)
        {
            anim?.Play(MonsterAction.Walk);
            if (!await WalkAsync(wrapper, path, epoch, 0f, yaw))
            {
                Debug.Log($"[CombatChoreo] 演出中止@{label}：{summary}");
                return;
            }
            anim?.Play(MonsterAction.Breath);
            if (wrapper != null)
                wrapper.rotation = Quaternion.identity; // wrapper 恒零（2026-10-15 横置侧倾退役）
        }

        // ---- 连锁冻结门（2026-10-17 用户定案：连锁期间动画表现全部冻结，结算时恢复） ----
        // 连锁期间=响应窗口开着（StackEngine.WaitingForPlayer——等玩家/AI 决定守卫与响应）；
        // 窗口关闭进入结算/栈空=恢复。演出在窗口期停帧（走位不动、节拍不计时），
        // 防连续攻击时在途演出与交互窗口打架（六连击演出被打断不归位的乱象）。

        /// <summary>连锁窗口是否开着（演出冻结中）。</summary>
        private static bool ChainFrozen
            => CardCore.GameCore.Instance != null
               && CardCore.GameCore.Instance.StackEngine != null
               && CardCore.GameCore.Instance.StackEngine.WaitingForPlayer;

        /// <summary>冻结感知等待：连锁窗口期暂停计时（表现冻结），窗口关闭后续走剩余时长。</summary>
        private async UniTask DelayChainAware(int milliseconds)
        {
            float remain = milliseconds / 1000f;
            while (remain > 0f)
            {
                await UniTask.Yield();
                if (ChainFrozen) continue; // 冻结帧不计时（表现层停帧）
                remain -= UnityEngine.Time.deltaTime;
            }
        }

        /// <summary>连锁窗口期停帧等待（走位循环入口挂）：窗口关闭即返回。</summary>
        private async UniTask WaitChainIdleAsync()
        {
            while (ChainFrozen && !_disposed) await UniTask.Yield();
        }

        // ======================================== 战斗目标箭头（2026-10-15 攻击红/守卫蓝） ========================================

        /// <summary>演出期箭头逐帧跟踪：走位/挡位移动中红箭恒指攻击目标、蓝箭（守卫拦截局）恒指
        /// 攻击者；_arrowLive=false（命中节拍/中止/清理）退出并渐隐，Dispose/代际失效只藏
        /// （实例随 Dispose 销毁，渐隐无意义）。宣言→栈清空等待期箭头为静态（参战者被 hold 不动）。</summary>
        private async UniTaskVoid TrackArrowsAsync(CombatPlan plan, Transform attW, Transform tgtW, int epoch)
        {
            while (_arrowLive && !_disposed && epoch == _epoch)
            {
                Vector3 from = attW != null ? attW.position : HeroPosOf(plan.AttackerOwner);
                Vector3 to = tgtW != null ? tgtW.position : HeroPosOf(1 - plan.AttackerOwner);
                _arrow.Show(from, to, BattleAttackArrow.AttackSkin);
                if (plan.Blocked) _guardArrow.Show(to, from, BattleAttackArrow.GuardSkin);
                await UniTask.Yield();
            }
            if (!_arrowLive)
            {
                _arrow.FadeOutAsync(0.35f).Forget();
                _guardArrow.FadeOutAsync(0.35f).Forget();
            }
            else
            {
                _arrow.Hide();
                _guardArrow.Hide();
            }
        }

        /// <summary>参战方当前位置：单位=场上的 wrapper（缺失回落格心），角色=立柱位。
        /// 供宣言期静态箭头取点（演出期跟踪直接读 wrapper.position）。</summary>
        private bool CombatPosOf(uint id, bool hero, int owner, out Vector3 pos)
        {
            if (hero || id == 0) { pos = HeroPosOf(owner); return true; }
            if (_field != null && _field.TryGetUnitBody(id, out var w, out _) && w != null)
            {
                pos = w.position;
                return true;
            }
            var cell = CellOf(id);
            if (cell.HasValue && BattleBoardGrid.Ready)
            {
                pos = BattleBoardGrid.WorldPositionOf(cell.Value.x, cell.Value.z);
                return true;
            }
            pos = default;
            return false;
        }

        // ======================================== 走位 ========================================

        /// <summary>沿折线移动 wrapper（恒速）：模型前向朝移动段方向（wrapper 转向=世界朝向−modelYaw
        /// 补偿——朝向常驻模型层，2026-10-15）、可选侧倾渐变（现恒传 0：24° 横置侧倾同日退役）。
        /// 返回 false=中止（Dispose/销毁），调用方应放弃后续步骤。</summary>
        private async UniTask<bool> WalkAsync(Transform wrapper, List<Vector3> waypoints, int epoch,
            float tiltTo = 0f, float modelYaw = 0f)
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

            float walked = 0f; // 累计计时（2026-10-17 连锁冻结）：realtime 基准在冻结恢复后会跳帧，改累计制
            float duration = total / WalkSpeed;
            float yaw = wrapper.rotation.eulerAngles.y;
            float tilt0 = wrapper.rotation.eulerAngles.z;
            var last = waypoints[waypoints.Count - 1];
            while (true)
            {
                if (wrapper == null || _disposed || epoch != _epoch) return false;
                await WaitChainIdleAsync(); // 连锁窗口期停帧（2026-10-17 表现冻结定案）：窗口关闭续走
                if (wrapper == null || _disposed || epoch != _epoch) return false;
                float t = Mathf.Min(walked / duration, 1f);
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
                    // 模型前向领走：烘焙前向=本地 +Z（2026-10-15 用户定案：头朝向即 +Z，以攻击前拱为判据
                    // 手工校准）——期望世界朝向=Atan2，wrapper 补 modelYaw
                    float targetYaw = Mathf.Atan2(segDir.x, segDir.z) * Mathf.Rad2Deg - modelYaw;
                    yaw = Mathf.MoveTowardsAngle(yaw, targetYaw, 460f * Time.deltaTime);
                }
                wrapper.position = pos;
                wrapper.rotation = Quaternion.Euler(0f, yaw, Mathf.Lerp(tilt0, tiltTo, t));
                if (t >= 1f) break;
                await UniTask.Yield();
                if (!ChainFrozen) walked += Time.deltaTime; // 冻结帧不计走位时长
            }
            wrapper.position = last;
            return true;
        }

        private static void Face2D(Transform wrapper, Vector3 target, float modelYaw = 0f)
        {
            Vector3 dir = target - wrapper.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 1e-4f)
                wrapper.rotation = Quaternion.Euler(0f, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg - modelYaw, 0f); // 前向 +Z（2026-10-15 用户定案）：世界朝向=Atan2，wrapper 补 modelYaw
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

        /// <summary>途经格序列 → 格间缝折线（2026-10-17 用户定案：走位不再穿空地格心——
        /// 沿两格交界线走）：相邻两格共享棱中点=两格心中点（正六边形铺装恒成立），
        /// 末点=末格格心（战斗站位）。cells 不含起点格（FindApproach 口径）。</summary>
        private static List<Vector3> SeamWaypoints(List<(int x, int z)> cells, (int x, int z) startCell)
        {
            var pts = new List<Vector3>(cells.Count);
            for (int i = 0; i < cells.Count; i++)
                pts.Add(i == cells.Count - 1
                    ? ToWorldAnchor(cells[i])
                    : SeamMid(i == 0 ? startCell : cells[i - 1], cells[i]));
            return pts;
        }

        /// <summary>两格共享棱中点世界坐标（+0.05 锚高同 ToWorldAnchor）。</summary>
        private static Vector3 SeamMid((int x, int z) a, (int x, int z) b)
            => (BattleBoardGrid.WorldPositionOf(a.x, a.z) + BattleBoardGrid.WorldPositionOf(b.x, b.z)) * 0.5f
               + new Vector3(0f, 0.05f, 0f);

        /// <summary>守卫者挡位路径点（2026-10-17 缝走同口径）：途经=格间缝折线（SeamWaypoints，
        /// blkStart=守卫者起始格；缺失回落旧格心折线）；终点=原目标格心（BlockerPath 末格）
        /// 向攻击者终格方向偏移 GuardCoverOffset——挡在被保护者身前，与其模型错开不重合。
        /// 攻击者无走位（已相邻/路径空）=取其当前位姿定偏移向。</summary>
        private static List<Vector3> GuardCoverPoints(CombatPlan plan, Transform attW, (int x, int z)? blkStart)
        {
            var pts = blkStart.HasValue
                ? SeamWaypoints(plan.BlockerPath, blkStart.Value)
                : ToWorldPath(plan.BlockerPath);
            if (pts.Count == 0) return pts;
            Vector3 cover = pts[pts.Count - 1];
            Vector3 attFinal = plan.Path != null && plan.Path.Count > 0
                ? ToWorldAnchor(plan.Path[plan.Path.Count - 1])
                : (attW != null ? attW.position : cover);
            Vector3 dir = attFinal - cover;
            dir.y = 0f;
            if (dir.sqrMagnitude > 1e-4f) pts[pts.Count - 1] = cover + dir.normalized * GuardCoverOffset;
            return pts;
        }

        // ======================================== 战斗注视（2026-10-15 头跟随） ========================================

        /// <summary>战斗注视点：asAttacker=看当前攻击对象（守卫拦截后 _targetId 已是拦截者——
        /// 供应器动态解析即自动转看拦截者）；否则=看攻击者。角色目标/攻击者=立柱位；
        /// 单位已离场返回 null（回落待机注视）。捕获清零后（id=0）恒 null，闭包持有安全。</summary>
        private Vector3? CombatGazeOf(bool asAttacker)
        {
            uint id = asAttacker ? _targetId : _attackerId;
            bool hero = asAttacker ? _heroTarget : _heroAttacker;
            if (hero)
            {
                int attOwner = _attackingIsSelf ? 0 : 1;
                return HeroPosOf(asAttacker ? 1 - attOwner : attOwner);
            }
            return _field.TryGetUnitLookPoint(id, out var p) ? p : (Vector3?)null;
        }

        private void SetGaze(uint id, Func<Vector3?> resolver)
        {
            _field.SetCombatGaze(id, resolver);
            if (id != 0 && !_gazePinned.Contains(id)) _gazePinned.Add(id);
        }

        private void ClearGazePins()
        {
            for (int i = 0; i < _gazePinned.Count; i++) _field.ClearCombatGaze(_gazePinned[i]);
            _gazePinned.Clear();
        }

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
