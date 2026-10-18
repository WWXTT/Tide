using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonsterRig
{
    /// <summary>
    /// 生物程序动画驱动：把目录条目解析成骨骼引用 + 模块集合。纯 C# 核心（不依赖
    /// MonoBehaviour 帧驱动）——游戏内由 MonsterRigAnimator.LateUpdate 驱动，
    /// 编辑器预览由 EditorApplication.update 驱动同一实例（UiKit.Updater 双驱动惯例）。
    ///
    /// 姿态契约：所有模块一律「捕获的 rest 姿态 × 增量」，ApplyRestAll() 可逐位还原
    /// （验证器依赖）。捕获发生在 Build 时——宿主须先摆好根位置/朝向再 Build/AddComponent，
    /// 或放置变化后调用 RecaptureRest()。
    ///
    /// 动作状态机：基础态 Breath/Walk（幅度混合过渡）+ 一次性反应（Attack/Hit 叠加）+
    /// 倒地散落（FallDown：部件平铺到根下随机散落；Revive 挂回原层级补间复位）。
    /// </summary>
    public class RigDriver
    {
        readonly Transform _root;
        readonly MonsterRigEntry _entry;
        readonly List<IMotionModule> _modules = new List<IMotionModule>();
        readonly List<BoneRef> _bones = new List<BoneRef>();
        BoneRef _rootRef;
        RootIdleModule _rootModule;
        HeadLookAtModule _lookModule;

        // 动作状态
        float _gaitBlend;          // 0=呼吸 1=行走（平滑）
        float _gaitTarget;
        ScatterSim _scatter;       // 非 null = 倒地散落中/已散落
        bool _fallen;
        bool _reviving;
        float _reviveT;
        List<Transform> _revivePieces;
        List<Vector3> _reviveFromPos;
        List<Quaternion> _reviveFromRot;
        List<Vector3> _reviveToPos;
        List<Quaternion> _reviveToRot;
        int _scatterSeedSeq;

        string _moveOverride;   // 行走方式覆盖（预览切换/宿主指定，优先于目录）

        public string Name => _entry != null ? _entry.name : (_root != null ? _root.name : "?");
        /// <summary>行走方式（覆盖 > 目录 > 漂浮）。</summary>
        public string MoveType => _moveOverride ?? (_entry != null ? _entry.moveType : MonsterRigCatalog.MoveFloat);
        public int ResolvedBones { get; private set; }
        public int MissingRefs { get; private set; }
        public bool HasHead => _lookModule != null;
        public bool Fallen => _fallen;
        public ScatterSim Scatter => _scatter;
        /// <summary>呼吸↔行走混合进度（0..1，验证器与预览读数用）。</summary>
        public float GaitBlend => _gaitBlend;
        public IReadOnlyList<IMotionModule> Modules => _modules;
        public IReadOnlyList<BoneRef> Bones => _bones;

        RigDriver(Transform root, MonsterRigEntry entry)
        {
            _root = root;
            _entry = entry;
        }

        /// <summary>解析目录条目并捕获 rest 姿态。目录缺失/条目缺失时只建根浮动（优雅降级）；
        /// moveTypeOverride 可临时改行走方式（预览试摆用，不落盘）。</summary>
        public static RigDriver Build(Transform root, string catalogNameOverride = null, string moveTypeOverride = null)
        {
            string name = string.IsNullOrEmpty(catalogNameOverride) ? root.name : catalogNameOverride;
            var entry = MonsterRigCatalog.Get(name);
            var driver = new RigDriver(root, entry);
            driver._moveOverride = moveTypeOverride;
            driver.BuildCore(entry);
            return driver;
        }

        void BuildCore(MonsterRigEntry entry)
        {
            var byName = new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);
            var all = _root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
                if (all[i] != _root && !byName.ContainsKey(all[i].name))
                    byName[all[i].name] = all[i];
            var seen = new HashSet<Transform>();

            BoneRef Ref(string boneName)
            {
                if (string.IsNullOrEmpty(boneName)) return null;
                Transform tr;
                if (!byName.TryGetValue(boneName, out tr)) { MissingRefs++; return null; }
                if (!seen.Add(tr)) { ResolvedBones++; return _bones.Find(b => b.T == tr); }
                var r = new BoneRef
                {
                    T = tr,
                    RestLocal = tr.localRotation,
                    RestLocalPos = tr.localPosition,
                    RestLocalScale = tr.localScale,
                    ParentRestWorld = tr.parent != null ? tr.parent.rotation : Quaternion.identity,
                    OriginalParent = tr.parent,
                };
                _bones.Add(r);
                ResolvedBones++;
                return r;
            }

            var rootRef = new BoneRef
            {
                T = _root,
                RestLocal = _root.localRotation,
                RestLocalPos = _root.localPosition,
                RestLocalScale = _root.localScale,
                ParentRestWorld = _root.parent != null ? _root.parent.rotation : Quaternion.identity,
                OriginalParent = _root.parent,
            };
            _rootRef = rootRef;
            _bones.Add(rootRef);

            Vector3 rootUpWorld = _root.rotation * Vector3.up;
            Vector3 rootRightWorld = _root.rotation * Vector3.right;
            Vector3 rootFwdWorld = _root.rotation * Vector3.forward;
            Quaternion rootRestWorld = _root.rotation;

            // ---- 根模块（恒有）----
            // 移动方向统一=根前向 +Z（2026-10-14 用户定案：头朝向已手工烘进 prefab，
            // 目录 headForward 已过期不作方向源）——攻击前扑与蛇形爬行同轴，方向恒一致。
            _rootModule = new RootIdleModule(rootRef, rootUpWorld, rootFwdWorld, rootRightWorld);
            _modules.Add(_rootModule);

            if (entry == null) return; // 目录缺失：仅根浮动（调用方已警告）

            // ---- 部位解析 ----
            var headRef = Ref(entry.head);
            var neckRefs = new List<BoneRef>();
            for (int i = 0; i < entry.neck.Count; i++)
            {
                var r = Ref(entry.neck[i]);
                if (r != null) neckRefs.Add(r);
            }
            if (headRef != null)
            {
                var chain = new List<BoneRef> { headRef };
                chain.AddRange(neckRefs);
                _lookModule = new HeadLookAtModule(chain, _root, rootRestWorld, entry.headForward);
                _modules.Add(_lookModule);
            }

            var tailRefs = new List<BoneRef>();
            for (int i = 0; i < entry.tail.Count; i++)
            {
                var r = Ref(entry.tail[i]);
                if (r != null) tailRefs.Add(r);
            }
            var bodyRefs = new List<BoneRef>();
            for (int i = 0; i < entry.bodyChain.Count; i++)
            {
                var r = Ref(entry.bodyChain[i]);
                if (r != null) bodyRefs.Add(r);
            }
            var bodyMainRef = !string.IsNullOrEmpty(entry.bodyMain) ? Ref(entry.bodyMain)
                : (bodyRefs.Count > 0 ? bodyRefs[0] : null);

            List<List<BoneRef>> LimbChains(List<LimbRef> limbs, List<float> phases, List<int> sides = null)
            {
                var chains = new List<List<BoneRef>>();
                for (int i = 0; i < limbs.Count; i++)
                {
                    var chain = new List<BoneRef>();
                    for (int j = 0; j < limbs[i].bones.Count; j++)
                    {
                        var r = Ref(limbs[i].bones[j]);
                        if (r != null) chain.Add(r);
                    }
                    if (chain.Count > 0)
                    {
                        chains.Add(chain);
                        phases.Add(limbs[i].phase);
                        if (sides != null) sides.Add(limbs[i].side);
                    }
                }
                return chains;
            }

            var legPhases = new List<float>();
            var legSides = new List<int>();
            var legChains = LimbChains(entry.legs, legPhases, legSides);
            var armPhases = new List<float>();
            var armChains = LimbChains(entry.arms, armPhases);

            var wings = new List<WingBreathModule.Wing>();
            for (int i = 0; i < entry.wings.Count; i++)
            {
                var w = entry.wings[i];
                var root = Ref(w.bones.Count > 0 ? w.bones[0] : null);
                if (root == null) continue;
                wings.Add(new WingBreathModule.Wing
                {
                    Root = root,
                    HingeLocal = root.WorldAxisInParent(rootRestWorld * w.hingeAxis),
                    AmpDeg = Mathf.Clamp(w.amplitudeDeg, 0f, 25f),
                    Phase = w.phase,
                });
            }
            var abdomenRef = Ref(entry.abdomen);

            var softPhases = new List<float>();
            var softChains = LimbChains(entry.soft, softPhases);

            // ---- 行走方式模块（骨骼所有权互斥；头/颈只归注视模块）----
            // 飞行=翅膀是移动方式（振翅+同频起伏），不装躯干步态；其余按目录 moveType 装对应步态
            string move = MoveType;
            bool flight = move == MonsterRigCatalog.MoveFlight;
            if (!flight)
            {
                switch (move)
                {
                    case MonsterRigCatalog.MoveSlither:
                    {
                        // 波形链=命名为 tail 的链（2026-10-14 用户验收）：头与躯干前端
                        // （body*，含挂进 bodyChain 的装饰件如 AquaSnake 的 ball）保持
                        // rest 不动，行波+拉直只作用尾链；无尾蛇（Yormungarde 等整身
                        // body 链、tail 为空）回落躯干链。拉直后链沿 -rootFwd 延伸——
                        // 行波向尾传播、位移向头，移动方向恒根前向 +Z（与攻击前扑同轴）。
                        var spine = tailRefs.Count > 0
                            ? new List<BoneRef>(tailRefs)
                            : new List<BoneRef>(bodyRefs);
                        if (spine.Count > 0)
                            _modules.Add(new SpineWaveModule(spine, rootUpWorld,
                                RigMotionTuning.SnakeWaveDeg, RigMotionTuning.KSlither,
                                true, rootFwdWorld)); // 拉直参考（2026-10-13）：运动参考不取 idle 盘曲；速度×3=KSlither
                        break;
                    }
                    case MonsterRigCatalog.MoveBiped:
                        // 顺序契约：身体/手臂模块先 tick，腿最后——LegGaitModule 的实时父旋转
                        // 读取才能拿到同 tick 姿态（否则耦合上一 tick 的 t，整周期确定性断言破）
                        if (armChains.Count > 0 || bodyMainRef != null)
                            _modules.Add(new BipedSwayModule(armChains, bodyMainRef, rootUpWorld, rootRightWorld));
                        if (legChains.Count > 0)
                        {
                            var legModule = new LegGaitModule(legChains, legPhases, legSides, rootUpWorld, rootFwdWorld);
                            _modules.Add(legModule);
                            _rootModule.WalkRootLift = legModule.RootLift;
                        }
                        break;
                    case MonsterRigCatalog.MoveQuadruped:
                        if (bodyMainRef != null)
                            _modules.Add(new QuadrupedWeightModule(bodyMainRef, rootUpWorld));
                        if (legChains.Count > 0)
                        {
                            var legModule = new LegGaitModule(legChains, legPhases, legSides, rootUpWorld, rootFwdWorld);
                            _modules.Add(legModule);
                            _rootModule.WalkRootLift = legModule.RootLift;
                        }
                        break;
                    case MonsterRigCatalog.MoveSpider:
                        if (legChains.Count > 0)
                            _modules.Add(new SpiderRippleModule(legChains, legPhases, rootUpWorld, rootRightWorld));
                        break;
                    default: // Float 与未知值：仅根浮动+呼吸族
                        break;
                }
            }

            // 尾呼吸：蛇形蠕动的尾已并入行波，其余行走方式挂呼吸摆
            if (tailRefs.Count > 0 && move != MonsterRigCatalog.MoveSlither)
                _modules.Add(new TailSwayModule(tailRefs, rootUpWorld, rootRightWorld));
            if (wings.Count > 0)
            {
                var wingModule = new WingBreathModule(wings)
                {
                    WalkFlapEnabled = flight, // 只有飞行行走=振翅；步行怪的翅膀行走时保持呼吸开合
                };
                _modules.Add(wingModule);
            }
            _rootModule.WingedFlight = flight; // 飞行：根浮动与振翅同频+增幅（上下起伏）
            if (abdomenRef != null) _modules.Add(new AbdomenPulseModule(abdomenRef));
            for (int i = 0; i < softChains.Count; i++)
                _modules.Add(new SpineWaveModule(softChains[i], rootUpWorld,
                    RigMotionTuning.SoftWaveDeg, RigMotionTuning.KSnake));
        }

        // ================= 动作 API =================

        /// <summary>基础态切换：Breath（混合→0）/ Walk（混合→1）。攻击受击为叠加，倒地见 PlayFallDown。</summary>
        public void SetBaseState(MonsterAction action)
        {
            if (_fallen) return;
            _gaitTarget = action == MonsterAction.Walk ? 1f : 0f;
        }

        public void PlaySummonPop()
        {
            if (!_fallen) _rootModule?.PlaySummonPop();
        }

        public void PlayHit()
        {
            if (!_fallen) _rootModule?.PlayHit();
        }

        public void PlayAttack()
        {
            if (!_fallen) _rootModule?.PlayAttack();
        }

        /// <summary>倒地：部件脱离父子关系平铺到根下，随机抛射散落（确定性种子）。</summary>
        public void PlayFallDown(int seed)
        {
            if (_fallen) return;
            _fallen = true;
            _gaitTarget = 0f;
            // 平铺：全部骨骼挂到根下（保持世界位，脱离原层级）
            for (int i = 0; i < _bones.Count; i++)
                if (_bones[i].T != _root && _bones[i].T.parent != _root)
                    _bones[i].T.SetParent(_root, true);
            _scatter = ScatterSim.Begin(_bones, _root, seed);
        }

        /// <summary>复活：部件挂回原父级（保持世界位），补间回 rest 姿态后恢复动画。</summary>
        public bool Revive()
        {
            if (!_fallen) return false;
            _scatter = null;

            // 挂回原层级（保持世界位），随后在「原父级 rest 帧的世界位」之间补间
            _revivePieces = new List<Transform>();
            _reviveFromPos = new List<Vector3>();
            _reviveFromRot = new List<Quaternion>();
            _reviveToPos = new List<Vector3>();
            _reviveToRot = new List<Quaternion>();

            var byTransform = new Dictionary<Transform, BoneRef>(_bones.Count);
            for (int i = 0; i < _bones.Count; i++)
                if (_bones[i].T != _root)
                    byTransform[_bones[i].T] = _bones[i];

            var restWorldCache = new Dictionary<Transform, KeyValuePair<Vector3, Quaternion>>();
            for (int i = 0; i < _bones.Count; i++)
            {
                if (_bones[i].T == _root) continue;
                var b = _bones[i];
                if (b.T.parent != b.OriginalParent)
                    b.T.SetParent(b.OriginalParent, true); // 世界位不变
                _revivePieces.Add(b.T);
                _reviveFromPos.Add(b.T.position);
                _reviveFromRot.Add(b.T.rotation);

                var rw = RestWorldOf(b, byTransform, restWorldCache);
                _reviveToPos.Add(rw.Key);
                _reviveToRot.Add(rw.Value);
            }
            _reviveT = 0f;
            _reviving = true;
            _fallen = false;
            return true;
        }

        /// <summary>骨骼在「原始层级全 rest」下的世界位姿（沿 OriginalParent 链向上推）。</summary>
        KeyValuePair<Vector3, Quaternion> RestWorldOf(BoneRef b,
            Dictionary<Transform, BoneRef> byTransform,
            Dictionary<Transform, KeyValuePair<Vector3, Quaternion>> cache)
        {
            KeyValuePair<Vector3, Quaternion> cached;
            if (cache.TryGetValue(b.T, out cached)) return cached;

            Vector3 pp; Quaternion pr;
            var parent = b.OriginalParent;
            if (parent == null || parent == _root)
            {
                pp = _root.parent != null ? _root.parent.TransformPoint(_rootRef.RestLocalPos) : _rootRef.RestLocalPos;
                pr = (_root.parent != null ? _root.parent.rotation : Quaternion.identity) * _rootRef.RestLocal;
            }
            else
            {
                BoneRef pb;
                if (byTransform.TryGetValue(parent, out pb))
                {
                    var prw = RestWorldOf(pb, byTransform, cache);
                    pp = prw.Key; pr = prw.Value;
                }
                else
                {
                    pp = parent.position; pr = parent.rotation;
                }
            }
            var result = new KeyValuePair<Vector3, Quaternion>(
                pp + pr * b.RestLocalPos, pr * b.RestLocal);
            cache[b.T] = result;
            return result;
        }

        // ================= 驱动 =================

        public void Tick(float t, float dt)
        {
            if (_scatter != null) { _scatter.Tick(dt); return; }

            if (_reviving)
            {
                _reviveT += dt / RigMotionTuning.ReviveDuration;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_reviveT));
                for (int i = 0; i < _revivePieces.Count; i++)
                {
                    _revivePieces[i].position = Vector3.Lerp(_reviveFromPos[i], _reviveToPos[i], k);
                    _revivePieces[i].rotation = Quaternion.Slerp(_reviveFromRot[i], _reviveToRot[i], k);
                }
                if (_reviveT >= 1f)
                {
                    _reviving = false;
                    _revivePieces = null;
                    ApplyRestAll(); // 补间收口=精确 rest（局部系逐位写回）
                }
                return;
            }

            // 呼吸↔行走幅度混合
            _gaitBlend = Mathf.MoveTowards(_gaitBlend, _gaitTarget, RigMotionTuning.StateBlendSpeed * dt);
            for (int i = 0; i < _modules.Count; i++)
                if (_modules[i] is IGaitBlend gb) gb.GaitBlend = _gaitBlend;

            for (int i = 0; i < _modules.Count; i++) _modules[i].Tick(t, dt);
        }

        /// <summary>精确还原捕获的 rest 姿态（含根 position/rotation/scale）。
        /// 倒地/复活中间态下先恢复原层级再写 rest。</summary>
        public void ApplyRestAll()
        {
            if (_scatter != null || _reviving)
            {
                RestoreParenting();
                _scatter = null;
                _reviving = false;
                _revivePieces = null;
                _fallen = false;
            }
            for (int i = 0; i < _bones.Count; i++) _bones[i].ApplyRest();
        }

        void RestoreParenting()
        {
            for (int i = 0; i < _bones.Count; i++)
                if (_bones[i].T != _root && _bones[i].OriginalParent != null
                    && _bones[i].T.parent != _bones[i].OriginalParent)
                    _bones[i].T.SetParent(_bones[i].OriginalParent, true);
        }

        public void SetLookTarget(Func<Vector3?> supplier) => _lookModule?.SetSupplier(supplier);
        public bool LookIdle => _lookModule == null || _lookModule.IsIdle;

        /// <summary>重捕获（宿主改放置后调用；模块内部轴基准随捕获更新需 Rebuild）。</summary>
        public static RigDriver Rebuild(Transform root, string catalogNameOverride = null)
            => Build(root, catalogNameOverride);
    }

    /// <summary>
    /// 运行时临时挂载的程序动画组件（美术待办§二：不烘焙进 prefab，召唤到场时挂到模型根）。
    /// 动作 API：Play(MonsterAction.…)—— Breath 呼吸 / Walk 行走 / Pause 暂停（冻结，再播
    /// Breath/Walk 恢复）/ Attack 攻击前扑 / Hit 受击压扁 / FallDown 倒地散落（Revive() 复活）。
    /// 使用约定：宿主先实例化并摆好根位置/朝向，再 AddComponent（OnEnable 即捕获 rest）；
    /// 之后移动根须调用 Recapture()。战场宿主层接入时按动作事件调 Play/Revive。
    /// </summary>
    [AddComponentMenu("Tide/Monster Rig Animator")]
    public class MonsterRigAnimator : MonoBehaviour
    {
        /// <summary>当前活跃实例（预览/注视目标解析用；OnDisable 自动除名）。</summary>
        public static readonly List<MonsterRigAnimator> Active = new List<MonsterRigAnimator>();

        [Tooltip("目录条目名（默认=根物体名=prefab 名）")]
        [SerializeField] private string _catalogName = "";

        RigDriver _driver;
        float _phase;
        bool _paused;
        MonsterAction _base = MonsterAction.Breath;
        Func<Vector3?> _lookTarget;      // 组件层持有：驱动（重）构建后重挂，防静默丢失
        string _moveTypeOverride;        // 行走方式覆盖（预览试摆；SetMoveType）

        public RigDriver Driver => _driver;
        public bool Built => _driver != null;
        public string CatalogName => _catalogName;
        public MonsterAction BaseAction => _base;
        /// <summary>当前行走方式（覆盖 > 目录）。</summary>
        public string MoveType => _moveTypeOverride
            ?? (Driver != null ? Driver.MoveType : MonsterRigCatalog.MoveFloat);

        /// <summary>当前生效动作：暂停 > 倒地 > 基础态。</summary>
        public MonsterAction CurrentAction
            => _paused ? MonsterAction.Pause
            : (_driver != null && _driver.Fallen ? MonsterAction.FallDown : _base);

        void OnEnable()
        {
            BuildDriver();
            Active.Add(this);
        }

        void OnDisable()
        {
            Active.Remove(this);
            if (_driver != null) _driver.ApplyRestAll();
            _driver = null;
        }

        void LateUpdate()
        {
            if (!_paused && _driver != null) _driver.Tick(Time.time + _phase, Time.deltaTime);
        }

        /// <summary>编辑器预览驱动（edit mode 下 OnEnable 不执行，此处懒构建；暂停冻结）。</summary>
        public void EditorTick(float t, float dt)
        {
            if (_driver == null) BuildDriver();
            if (!_paused && _driver != null) _driver.Tick(t + _phase, dt);
        }

        /// <summary>按动作类别播放。Breath/Walk=基础态切换（并解除暂停）；Pause=冻结当前姿态；
        /// Attack/Hit=一次性反应叠加；FallDown=倒地散落（Revive() 复位复活）。</summary>
        public void Play(MonsterAction action)
        {
            switch (action)
            {
                case MonsterAction.Breath:
                case MonsterAction.Walk:
                    _paused = false;
                    _base = action;
                    _driver?.SetBaseState(action);
                    break;
                case MonsterAction.Pause:
                    _paused = true;
                    break;
                case MonsterAction.Attack:
                    if (_driver == null) BuildDriver();
                    _driver?.PlayAttack();
                    break;
                case MonsterAction.Hit:
                    if (_driver == null) BuildDriver();
                    _driver?.PlayHit();
                    break;
                case MonsterAction.FallDown:
                    if (_driver == null) BuildDriver();
                    _driver?.PlayFallDown((_phaseSeq++ * 7919) & 0x7fffffff);
                    break;
            }
        }

        /// <summary>倒地复活：部件归位补间回 rest，恢复基础态动画。</summary>
        public void Revive()
        {
            _paused = false;
            _driver?.Revive();
        }

        public void PlaySummonPop()
        {
            if (_driver == null) BuildDriver();
            _driver?.PlaySummonPop();
        }

        /// <summary>注视目标供应器（最近敌方单位→对方角色→null）。组件层持有，
        /// 驱动重建（Recapture 等）后自动重挂。</summary>
        public void SetLookTarget(Func<Vector3?> supplier)
        {
            _lookTarget = supplier;
            if (_driver == null) BuildDriver();
            _driver?.SetLookTarget(supplier);
        }

        /// <summary>运行时注入目录条目名（战场宿主层用）：Instantiate 产物根名带 "(Clone)"
        /// 会导致目录 miss——必须赶在组件激活构建前调用（宿主先 SetActive(false)→AddComponent→
        /// 本方法→摆好根位姿→SetActive(true)，OnEnable 的 BuildDriver 才能用正确名字捕获）。</summary>
        public void SetCatalogName(string name) => _catalogName = name;

        /// <summary>行走方式覆盖（预览试摆/宿主指定）：立即重建驱动生效，不落盘目录。
        /// 传 null 回退目录值。</summary>
        public void SetMoveType(string moveType)
        {
            if (_driver != null && _driver.Fallen) Revive();
            _moveTypeOverride = moveType;
            if (_driver != null) _driver.ApplyRestAll();
            BuildDriver();
        }

        /// <summary>宿主移动/旋转根之后调用：重建驱动（重捕获 rest 与轴向基准）。
        /// 倒地散落状态下禁用（先 Revive）。</summary>
        public void Recapture()
        {
            if (_driver != null && _driver.Fallen) return;
            if (_driver != null) _driver.ApplyRestAll();
            BuildDriver();
        }

        void BuildDriver()
        {
            _driver = RigDriver.Build(transform, _catalogName, _moveTypeOverride);
            // 相位随机（防全场同步呼吸）：黄金比例序列散布，不依赖过时的 GetInstanceID
            _phase = (float)((_phaseSeq++ * 0.6180339887498949) % 1.0) * RigMotionTuning.BasePeriod;
            if (_lookTarget != null) _driver?.SetLookTarget(_lookTarget);
        }

        static int _phaseSeq = System.Environment.TickCount & 0x7fffffff;

        public string DebugSummary => _driver == null
            ? $"{name}: 未构建"
            : $"{name}: {MonsterRigCatalog.MoveLabel(MoveType)}，{_driver.ResolvedBones} 骨骼，{_driver.Modules.Count} 模块"
                + (_driver.HasHead ? "，含头注视" : "，无头");
    }
}
