using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonsterRig
{
    /// <summary>
    /// 程序动画调参（2026-10-12 美术待办§二）。
    /// 所有幅度/方向符号集中于此：坐标转换修正重烘后若横向语义镜像，改 MirrorX 一处即可全局翻转；
    /// 铰链轴/头朝向等逐模型数据在目录重生成时自动重算。
    ///
    /// 频率规约：所有周期动作 = 基频 2π*k/BasePeriod（k 为整数谐波）——整个系统严格以
    /// BasePeriod 为公倍周期（验证器依赖 t 与 t+BasePeriod 状态逐位一致做确定性断言）。
    /// </summary>
    public static class RigMotionTuning
    {
        public const float BasePeriod = 12f;

        /// <summary>横向摆动全局符号（坐标系重烘后如左右镜像改 -1）。</summary>
        public static float MirrorX = 1f;
        /// <summary>总幅度倍率（预览窗口滑杆；验证时保持 1）。</summary>
        public static float GlobalAmplitude = 1f;

        // 谐波次数（2026-10-12 用户验收：行走左右摇摆频率×2）
        public const int KBreath = 3;   // 呼吸 4s
        public const int KSway = 2;     // 重心/缓摆 6s（原 12s）
        public const int KGait = 8;     // 步态 1.5s（原 3s）
        public const int KRipple = 12;  // 节肢涟漪 1s（原 2s）
        public const int KSnake = 2;    // 软体触手行波 6s
        public const int KSlither = 6;  // 蛇形爬行 2s（2026-10-13 用户验收：速度×3；触手不受牵连）

        // 呼吸族
        public const float BreathAmpTailDeg = 6f;
        public const float AbdomenScaleAmp = 0.03f;

        // 体型族（2026-10-12 用户验收：左右摇摆幅度×2）
        public const float SnakeWaveDeg = 42f;  // 2026-10-13 用户验收：幅度×3（原 14）
        public const float SnakeSegDelay = 0.55f;   // 相邻节相位延迟（弧度）
        public const float BipedArmSwingDeg = 8f;
        public const float BipedBodyRollDeg = 4f;
        public const float QuadLegLiftDeg = 3f;
        public const float QuadBodyRollDeg = 3f;
        public const float SpiderRippleDeg = 7f;
        public const float SoftWaveDeg = 5f;

        // 根
        public const float RootBobAmp = 0.025f;
        public const float RootSwayDeg = 0.8f;

        // 头注视：最近敌方单位 → 对方角色 → 无目标则完全不控头
        public const float LookYawClampDeg = 40f;
        public const float LookPitchClampDeg = 25f;
        public const float LookHeadShare = 0.5f;   // 头骨承担份额，其余颈链均摊
        public const float LookSpeed = 6f;         // 平滑速率（1/s，2026-10-12 用户验收提速）
        public const float LookRecenterEpsDeg = 0.5f;

        // 召唤起落（2026-10-12 用户重定）：原点上方升 2 单位——0.3s 速升 + 0.6s 缓落，
        // 单起落、无落地回弹、无缩放弹出
        public const float PopRiseHeight = 2f;
        public const float PopRiseTime = 0.3f;
        public const float PopFallTime = 0.6f;
        public const float PopDuration = 0.9f;

        // 受击压扁：阻尼弹簧回弹
        public const float HitDecayRate = 8f;
        public const float HitOscFreq = 20f;
        public const float HitAmp = 0.25f;

        // 攻击（一次性前扑）
        public const float AttackDuration = 0.45f;
        public const float AttackPushDist = 0.3f;
        public const float AttackPitchDeg = 10f;

        // 动作状态（呼吸↔行走）
        public const float IdleGaitShare = 0.4f;  // 呼吸态步态幅度占比（行走=1）
        public const float WalkBobBoost = 1.4f;   // 行走时根浮动增幅
        public const float StateBlendSpeed = 3f;  // 状态幅度过渡速率（1/s）
        public const float ReviveDuration = 0.5f; // 倒地→复位补间时长

        /// <summary>有翅生物行走=振翅：幅度增益（2026-10-12 用户验收：移动方式是翅膀）。</summary>
        public const float WalkFlapBoost = 1.5f;
        /// <summary>飞行振翅的同步上下起伏增幅（根浮动额外倍率）。</summary>
        public const float FlightBobBoost = 1.8f;

        // 腿步态 IK（2026-10-13 规范参考定案：行走腿垂直于地面，脚位不从 idle 推导）
        public const float LegStrideShare = 0.30f;  // 步幅（前后摆幅）= 全腿长 × 此值
        public const float LegLiftShare = 0.13f;    // 摆动相抬脚高度 = 全腿长 × 此值
        public const float LegReachShare = 0.98f;   // 腿伸展上限（膝常留弯，防锁直/穿地）

        /// <summary>步态幅度系数：呼吸态 IdleGaitShare → 行走 1。</summary>
        public static float GaitAmp(float blend) => IdleGaitShare + (1f - IdleGaitShare) * blend;

        public static float Phase(float t, int k) => Mathf.PI * 2f * k * t / BasePeriod;
        public static float EaseOutBack(float u)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float v = u - 1f;
            return 1f + c3 * v * v * v + c1 * v * v;
        }
    }

    /// <summary>头注视目标链：最近敌方单位 → 敌方角色 → null（默认不控头）。
    /// 纯函数，战场宿主层接入时喂数据即可（敌方单位世界坐标集 + 对方角色坐标）。</summary>
    public static class MonsterLookTarget
    {
        public static Vector3? Resolve(Vector3 self, IReadOnlyList<Vector3> enemyUnits, Vector3? enemyHero)
        {
            Vector3? n = Nearest(self, enemyUnits);
            if (n.HasValue) return n;
            return enemyHero;
        }

        public static Vector3? Nearest(Vector3 self, IReadOnlyList<Vector3> candidates)
        {
            if (candidates == null || candidates.Count == 0) return null;
            float best = float.MaxValue;
            Vector3 bestPos = candidates[0];
            for (int i = 0; i < candidates.Count; i++)
            {
                float d = (candidates[i] - self).sqrMagnitude;
                if (d < best) { best = d; bestPos = candidates[i]; }
            }
            return bestPos;
        }
    }

    /// <summary>骨骼引用：捕获 rest 姿态（烘焙 idle）+ 父系换算基准。程序动画一律
    /// 「在 rest 上叠加增量」，绝不停留在增量姿态（ApplyRest 可精确还原）。
    /// OriginalParent 供倒地散落（平铺到根）后复活时挂回原层级。</summary>
    public class BoneRef
    {
        public Transform T;
        public Quaternion RestLocal;
        public Vector3 RestLocalPos;
        public Vector3 RestLocalScale;
        /// <summary>捕获时父世界旋转（世界轴→父系轴换算基准，重捕获即随放置更新）。</summary>
        public Quaternion ParentRestWorld;
        /// <summary>捕获时的原父级（倒地平铺前的层级）。</summary>
        public Transform OriginalParent;

        public Vector3 WorldAxisInParent(Vector3 worldAxis)
            => Quaternion.Inverse(ParentRestWorld) * worldAxis;

        public void ApplyRest()
        {
            if (T == null) return;
            T.localRotation = RestLocal;
            T.localPosition = RestLocalPos;
            T.localScale = RestLocalScale;
        }
    }

    public interface IMotionModule
    {
        void Tick(float t, float dt);
        /// <summary>本模块对所辖骨骼的最大旋转偏移（度）——验证器钳制断言用。</summary>
        float MaxAngularDeltaDeg { get; }
    }

    /// <summary>受「呼吸↔行走」状态混合影响的模块（幅度随 blend 0→1 增强）。</summary>
    public interface IGaitBlend
    {
        float GaitBlend { get; set; }
    }

    // ============================ 根：浮动 + 反应 ============================

    /// <summary>根模块：浮动（上下呼吸浮动+微摆，行走时增强）与反应包络
    /// （召唤冒出/受击压扁/攻击前扑）。只碰根 Transform，不碰任何骨骼。</summary>
    public class RootIdleModule : IMotionModule, IGaitBlend
    {
        readonly BoneRef _root;
        readonly Vector3 _upLocal;      // 世界 up 在根父系的表达
        readonly Vector3 _fwdLocal;     // 根前向（攻击前扑方向，父系表达）
        readonly Vector3 _rightLocal;   // 根右向（攻击俯冲轴，父系表达）
        float _popT = -1f;              // <0 = 未激活
        float _hitT = -1f;
        float _atkT = -1f;
        public float GaitBlend { get; set; }
        /// <summary>有翅生物（移动方式=翅膀）：行走态根浮动与振翅同频且增幅（上下起伏）。</summary>
        public bool WingedFlight;
        /// <summary>行走态根抬升（米）：腿步态切到垂直伸直参考后身体需抬高使脚尖贴地
        /// （LegGaitModule 构造期算出；装配层喂入。模型直立、根仅偏航，up≈世界 Y）。</summary>
        public float WalkRootLift;

        public RootIdleModule(BoneRef root, Vector3 rootUpWorld, Vector3 rootFwdWorld, Vector3 rootRightWorld)
        {
            _root = root;
            _upLocal = root.WorldAxisInParent(Vector3.up);
            _fwdLocal = root.WorldAxisInParent(rootFwdWorld);
            _rightLocal = root.WorldAxisInParent(rootRightWorld);
        }

        public float MaxAngularDeltaDeg => RigMotionTuning.RootSwayDeg + RigMotionTuning.AttackPitchDeg;

        public void PlaySummonPop() => _popT = 0f;
        public void PlayHit() => _hitT = 0f;
        public void PlayAttack() => _atkT = 0f;

        public void Tick(float t, float dt)
        {
            float g = RigMotionTuning.GlobalAmplitude;
            // 根浮动：有翅生物行走（飞行）时与振翅同频（波形交叉淡化，勿相位混合——同 WingBreath 注）、
            // 幅度增强=上下起伏
            float bobBoost = 1f + RigMotionTuning.WalkBobBoost * GaitBlend
                * (WingedFlight ? RigMotionTuning.FlightBobBoost : 1f);
            float bobWave = WingedFlight
                ? Mathf.Sin(RigMotionTuning.Phase(t, RigMotionTuning.KBreath)) * (1f - GaitBlend)
                  + Mathf.Sin(RigMotionTuning.Phase(t, RigMotionTuning.KGait)) * GaitBlend
                : Mathf.Sin(RigMotionTuning.Phase(t, RigMotionTuning.KBreath));
            float bob = RigMotionTuning.RootBobAmp * bobBoost * g * bobWave;
            float sway = RigMotionTuning.MirrorX * RigMotionTuning.RootSwayDeg * g
                * Mathf.Sin(RigMotionTuning.Phase(t, RigMotionTuning.KSway));

            // 攻击前扑：正弦包络前推+俯冲（一次性，叠在当前状态上）
            Vector3 atkOff = Vector3.zero;
            float atkPitch = 0f;
            if (_atkT >= 0f)
            {
                _atkT += dt;
                float u = _atkT / RigMotionTuning.AttackDuration;
                if (u >= 1f) _atkT = -1f;
                else
                {
                    float atkS = Mathf.Sin(Mathf.PI * u);
                    atkOff = _fwdLocal * (RigMotionTuning.AttackPushDist * atkS);
                    atkPitch = RigMotionTuning.AttackPitchDeg * atkS;
                }
            }

            // 召唤起落：原点上方 2 单位——速升 0.3s（easeOutCubic）后缓落 0.6s（easeInOutSine），
            // 单起落、无落地回弹；全程伴随「从小到大」缩放过渡（easeOutBack 轻过冲）
            float popY = 0f;
            float popScaleF = 1f;
            if (_popT >= 0f)
            {
                _popT += dt;
                if (_popT >= RigMotionTuning.PopDuration) { _popT = -1f; popScaleF = 1f; }
                else
                {
                    popScaleF = Mathf.Lerp(0.05f, 1f,
                        RigMotionTuning.EaseOutBack(Mathf.Clamp01(_popT / RigMotionTuning.PopDuration)));
                    if (_popT < RigMotionTuning.PopRiseTime)
                    {
                        float u = _popT / RigMotionTuning.PopRiseTime;
                        float e = 1f - (1f - u) * (1f - u) * (1f - u);
                        popY = RigMotionTuning.PopRiseHeight * e;
                    }
                    else
                    {
                        float u = (_popT - RigMotionTuning.PopRiseTime) / RigMotionTuning.PopFallTime;
                        float e = 0.5f - 0.5f * Mathf.Cos(Mathf.PI * u);
                        popY = RigMotionTuning.PopRiseHeight * (1f - e);
                    }
                }
            }

            // 受击压扁：阻尼弹簧回弹
            Vector3 hitScale = Vector3.one;
            if (_hitT >= 0f)
            {
                _hitT += dt;
                float decay = RigMotionTuning.HitAmp * Mathf.Exp(-RigMotionTuning.HitDecayRate * _hitT)
                    * Mathf.Cos(RigMotionTuning.HitOscFreq * _hitT);
                if (Mathf.Abs(decay) < 0.005f) { _hitT = -1f; }
                else hitScale = new Vector3(1f + decay, 1f - decay, 1f + decay);
            }

            var tr = _root.T;
            tr.localPosition = _root.RestLocalPos + _upLocal * (bob + popY + WalkRootLift * GaitBlend) + atkOff;
            tr.localRotation = Quaternion.AngleAxis(sway, _upLocal)
                * Quaternion.AngleAxis(atkPitch, _rightLocal)
                * _root.RestLocal;
            Vector3 s = _root.RestLocalScale;
            tr.localScale = new Vector3(
                s.x * popScaleF * hitScale.x,
                s.y * popScaleF * hitScale.y,
                s.z * popScaleF * hitScale.z);
        }
    }

    // ============================ 链条类（蛇身/尾/软体） ============================

    /// <summary>沿链行进横波（蛇形）：每段绕根系 up 轴做相位延迟正弦摆动，幅度向尾端渐增。
    /// straighten=true（蛇形移动用，2026-10-13 定案）：先每段对齐根前向把身体拉成直线
    /// （权重=GaitAmp，idle 回盘曲 rest）再叠加行波——运动参考不取 idle 盘曲姿态；
    /// false（软体触手等呼吸链）维持原 rest 上叠加。</summary>
    public class SpineWaveModule : IMotionModule, IGaitBlend
    {
        readonly List<BoneRef> _segments;
        readonly List<Vector3> _upAxes;    // 各段父系内的摆动轴
        readonly float _ampDeg;
        readonly int _harmonic;
        readonly bool _straighten;
        readonly List<Quaternion> _straightWorld; // 各段拉直增量（世界系；空=不拉直）
        readonly float _maxStraightDeg;
        readonly Vector3 _upWorld;                // 行波轴（世界系，拉直模式实时共轭用）
        public float GaitBlend { get; set; }

        public SpineWaveModule(List<BoneRef> segments, Vector3 rootUpWorld, float ampDeg, int harmonic,
            bool straighten = false, Vector3 rootFwdWorld = default(Vector3))
        {
            _segments = segments;
            _ampDeg = ampDeg;
            _harmonic = harmonic;
            _straighten = straighten;
            _upAxes = new List<Vector3>(_segments.Count);
            for (int i = 0; i < _segments.Count; i++)
                _upAxes.Add(_segments[i].WorldAxisInParent(rootUpWorld));

            _straightWorld = new List<Quaternion>(_segments.Count);
            _upWorld = rootUpWorld;
            if (straighten)
            {
                // rest 各段出向（末段沿用前段）→ 对齐 root 前向；逐关节旋转后整链即成直线
                var fwd = rootFwdWorld.sqrMagnitude > 1e-6f ? rootFwdWorld.normalized : Vector3.forward;
                Vector3 prev = fwd;
                for (int i = 0; i < _segments.Count; i++)
                {
                    Vector3 dir = prev;
                    if (i < _segments.Count - 1)
                    {
                        Vector3 d = _segments[i + 1].T.position - _segments[i].T.position;
                        if (d.sqrMagnitude > 1e-8f) dir = d.normalized;
                    }
                    prev = dir;
                    _straightWorld.Add(Quaternion.FromToRotation(dir, fwd));
                    _maxStraightDeg = Mathf.Max(_maxStraightDeg,
                        Quaternion.Angle(Quaternion.identity, _straightWorld[i]));
                }
            }
        }

        public float MaxAngularDeltaDeg => _ampDeg + _maxStraightDeg;

        public void Tick(float t, float dt)
        {
            float g = RigMotionTuning.GlobalAmplitude * RigMotionTuning.GaitAmp(GaitBlend);
            int n = _segments.Count;
            for (int i = 0; i < n; i++)
            {
                float taper = n <= 1 ? 1f : 0.4f + 0.6f * i / (n - 1);
                float wave = Mathf.Sin(RigMotionTuning.Phase(t, _harmonic) - i * RigMotionTuning.SnakeSegDelay);
                if (_straighten)
                {
                    // 直化与行波权重=裸 blend（2026-10-13 用户验收：爬完必须回 idle 盘曲；
                    // GaitAmp(0)=0.4 会永久残留 40% 直化）。idle=精确 rest，行走=满幅。
                    // 世界语义写入（实时父旋转共轭）：段世界旋转=qWave·qStraight·restWorld，
                    // 出向=qWave·fwd。串行链下父级已被本模块写过，用捕获的静止父旋转共轭
                    // 会逐级累积父增量（2026-10-13 蛇拉直失效根因）。
                    float angleS = RigMotionTuning.MirrorX * _ampDeg * RigMotionTuning.GlobalAmplitude
                        * GaitBlend * taper * wave;
                    var qStraight = Quaternion.Slerp(Quaternion.identity, _straightWorld[i], GaitBlend);
                    var restWorld = _segments[i].ParentRestWorld * _segments[i].RestLocal;
                    var parentNow = _segments[i].T.parent != null ? _segments[i].T.parent.rotation : Quaternion.identity;
                    _segments[i].T.localRotation = Quaternion.Inverse(parentNow)
                        * Quaternion.AngleAxis(angleS, _upWorld) * qStraight * restWorld;
                    continue;
                }
                float angle = RigMotionTuning.MirrorX * _ampDeg * g * taper * wave;
                _segments[i].T.localRotation = Quaternion.AngleAxis(angle, _upAxes[i]) * _segments[i].RestLocal;
            }
        }
    }

    /// <summary>尾巴呼吸：慢速横向摆+轻微俯仰（非蛇形体型挂在尾巴上的待机呼吸）。</summary>
    public class TailSwayModule : IMotionModule
    {
        readonly List<BoneRef> _chain;
        readonly List<Vector3> _upAxes, _rightAxes;
        readonly Vector3 _rootRightWorld;

        public TailSwayModule(List<BoneRef> chain, Vector3 rootUpWorld, Vector3 rootRightWorld)
        {
            _chain = chain;
            _rootRightWorld = rootRightWorld;
            _upAxes = new List<Vector3>(chain.Count);
            _rightAxes = new List<Vector3>(chain.Count);
            for (int i = 0; i < chain.Count; i++)
            {
                _upAxes.Add(chain[i].WorldAxisInParent(rootUpWorld));
                _rightAxes.Add(chain[i].WorldAxisInParent(rootRightWorld));
            }
        }

        public float MaxAngularDeltaDeg => RigMotionTuning.BreathAmpTailDeg;

        public void Tick(float t, float dt)
        {
            float g = RigMotionTuning.GlobalAmplitude;
            float yaw = RigMotionTuning.MirrorX * RigMotionTuning.BreathAmpTailDeg * g
                * Mathf.Sin(RigMotionTuning.Phase(t, RigMotionTuning.KBreath));
            float pitch = 0.3f * RigMotionTuning.BreathAmpTailDeg * g
                * Mathf.Sin(RigMotionTuning.Phase(t, RigMotionTuning.KBreath) + 1.1f);
            int n = _chain.Count;
            for (int i = 0; i < n; i++)
            {
                float taper = n <= 1 ? 1f : 0.5f + 0.5f * i / (n - 1);
                _chain[i].T.localRotation =
                    Quaternion.AngleAxis(yaw * taper, _upAxes[i]) *
                    Quaternion.AngleAxis(pitch * taper, _rightAxes[i]) *
                    _chain[i].RestLocal;
            }
        }
    }

    // ============================ 翅/腹呼吸 ============================

    /// <summary>翅/鳍：呼吸态=绕目录铰链轴慢速收展；行走态=**振翅**（有翅生物的移动方式，
    /// 2026-10-12 用户验收）——频率渐变到步态谐波、幅度 WalkFlapBoost 增强。
    /// （2026-10-13 曾试「先展平水平再绕体轴扇动」的规范参考改法，用户验收后撤销——
    /// 回到铰链轴口径，勿再引入水平展平。）</summary>
    public class WingBreathModule : IMotionModule, IGaitBlend
    {
        public struct Wing
        {
            public BoneRef Root;
            public Vector3 HingeLocal;   // 父系内铰链轴
            public float AmpDeg;
            public float Phase;
        }

        readonly List<Wing> _wings;
        readonly float _maxAmp;
        public float GaitBlend { get; set; }
        /// <summary>行走=振翅（仅飞行行走方式）；步行怪的翅膀行走时保持呼吸开合不增强。</summary>
        public bool WalkFlapEnabled = true;

        public WingBreathModule(List<Wing> wings)
        {
            _wings = wings;
            _maxAmp = 0f;
            for (int i = 0; i < wings.Count; i++) _maxAmp = Mathf.Max(_maxAmp, wings[i].AmpDeg);
        }

        public float MaxAngularDeltaDeg => _maxAmp * (WalkFlapEnabled ? 1f + RigMotionTuning.WalkFlapBoost : 1f);

        public void Tick(float t, float dt)
        {
            float g = RigMotionTuning.GlobalAmplitude;
            float flapBoost = WalkFlapEnabled ? RigMotionTuning.WalkFlapBoost * GaitBlend : 0f;
            float ampScale = 1f + flapBoost;
            for (int i = 0; i < _wings.Count; i++)
            {
                var w = _wings[i];
                // 波形交叉淡化（勿用相位混合：b 渐变且 t 大时瞬时频率会爆出 2π·Δk·t/T·db/dt 尖峰，
                // 表现为切换瞬间超快扇翅）。恒定 b 下仍是整数谐波线性组合，周期性断言不受影响。
                float breath = Mathf.Sin(RigMotionTuning.Phase(t, RigMotionTuning.KBreath) + w.Phase);
                float wave = WalkFlapEnabled
                    ? breath * (1f - GaitBlend)
                      + Mathf.Sin(RigMotionTuning.Phase(t, RigMotionTuning.KGait) + w.Phase) * GaitBlend
                    : breath;
                float angle = w.AmpDeg * ampScale * g * wave;
                w.Root.T.localRotation = Quaternion.AngleAxis(angle, w.HingeLocal) * w.Root.RestLocal;
            }
        }
    }

    /// <summary>腹部呼吸：整体缩放脉动（昆虫/节肢腹部胀缩）。</summary>
    public class AbdomenPulseModule : IMotionModule
    {
        readonly BoneRef _abdomen;
        public AbdomenPulseModule(BoneRef abdomen) => _abdomen = abdomen;
        public float MaxAngularDeltaDeg => 0f; // 纯缩放，无旋转

        public void Tick(float t, float dt)
        {
            float s = 1f + RigMotionTuning.AbdomenScaleAmp * RigMotionTuning.GlobalAmplitude
                * Mathf.Sin(RigMotionTuning.Phase(t, RigMotionTuning.KBreath));
            var r = _abdomen.RestLocalScale;
            _abdomen.T.localScale = new Vector3(r.x * s, r.y * s, r.z * s);
        }
    }

    // ============================ 体型步态 ============================

    /// <summary>双足：重心缓摆 + 双臂反相小幅摆动（肘部半幅跟随）。</summary>
    public class BipedSwayModule : IMotionModule, IGaitBlend
    {
        readonly List<BoneRef> _armRoots;
        readonly List<Vector3> _armAxes;
        readonly List<List<BoneRef>> _armChains;
        readonly BoneRef _body;
        readonly Vector3 _rightWorld, _upWorld;
        readonly float _maxDeg;
        public float GaitBlend { get; set; }

        public BipedSwayModule(List<List<BoneRef>> armChains, BoneRef body, Vector3 rootUpWorld, Vector3 rootRightWorld)
        {
            _armChains = armChains;
            _body = body;
            _upWorld = rootUpWorld;
            _rightWorld = rootRightWorld;
            _armRoots = new List<BoneRef>(armChains.Count);
            _armAxes = new List<Vector3>(armChains.Count);
            for (int i = 0; i < armChains.Count; i++)
            {
                _armRoots.Add(armChains[i][0]);
                _armAxes.Add(armChains[i][0].WorldAxisInParent(rootRightWorld));
            }
            _maxDeg = Mathf.Max(RigMotionTuning.BipedArmSwingDeg, RigMotionTuning.BipedBodyRollDeg);
        }

        public float MaxAngularDeltaDeg => _maxDeg;

        public void Tick(float t, float dt)
        {
            float g = RigMotionTuning.GlobalAmplitude * RigMotionTuning.GaitAmp(GaitBlend);
            float gait = RigMotionTuning.Phase(t, RigMotionTuning.KGait);
            for (int i = 0; i < _armRoots.Count; i++)
            {
                // 相位已含左右反相（limb phase），肘部半幅跟随
                float angle = RigMotionTuning.MirrorX * RigMotionTuning.BipedArmSwingDeg * g * Mathf.Sin(gait + ArmPhase(i));
                _armRoots[i].T.localRotation = Quaternion.AngleAxis(angle, _armAxes[i]) * _armRoots[i].RestLocal;
                var chain = _armChains[i];
                for (int j = 1; j < chain.Count && j <= 2; j++)
                {
                    var axis = chain[j].WorldAxisInParent(_rightWorld);
                    chain[j].T.localRotation = Quaternion.AngleAxis(angle * 0.5f, axis) * chain[j].RestLocal;
                }
            }
            if (_body != null)
            {
                float roll = RigMotionTuning.MirrorX * RigMotionTuning.BipedBodyRollDeg * g
                    * Mathf.Sin(RigMotionTuning.Phase(t, RigMotionTuning.KSway));
                var axis = _body.WorldAxisInParent(_upWorld);
                _body.T.localRotation = Quaternion.AngleAxis(roll, axis) * _body.RestLocal;
            }
        }

        float ArmPhase(int i) => i * Mathf.PI; // 双臂反相（左右各一）
    }

    /// <summary>四足：躯干缓慢重心摆（2026-10-13 瘦身——腿部步态全归 LegGaitModule，
    /// 原抬腿 mark-time 退役）。</summary>
    public class QuadrupedWeightModule : IMotionModule, IGaitBlend
    {
        readonly BoneRef _body;
        readonly Vector3 _upWorld;
        public float GaitBlend { get; set; }

        public QuadrupedWeightModule(BoneRef body, Vector3 rootUpWorld)
        {
            _body = body;
            _upWorld = rootUpWorld;
        }

        public float MaxAngularDeltaDeg => RigMotionTuning.QuadBodyRollDeg;

        public void Tick(float t, float dt)
        {
            if (_body == null) return;
            float g = RigMotionTuning.GlobalAmplitude * RigMotionTuning.GaitAmp(GaitBlend);
            float roll = RigMotionTuning.MirrorX * RigMotionTuning.QuadBodyRollDeg * g
                * Mathf.Sin(RigMotionTuning.Phase(t, RigMotionTuning.KSway));
            var axis = _body.WorldAxisInParent(_upWorld);
            _body.T.localRotation = Quaternion.AngleAxis(roll, axis) * _body.RestLocal;
        }
    }

    // ============================ 腿步态（规范参考姿态 IK） ============================

    /// <summary>双足/四足腿步态：两骨解析 IK（移植 Synergy TwoBoneIK 的余弦定理解算）。
    /// 2026-10-13 规范参考定案：行走脚位不从 idle 推导——腿链从髋**垂直向下**伸直
    /// （留膝弯余量），脚沿「支撑贴地后移 ↔ 摆动抬脚前迈」轨迹原地踏步（模型不位移）。
    /// 膝弯方向（正反关节）由 rest 几何检测：膝相对髋→脚连线的垂线分量（Synergy
    /// initialKneeDirection 同式），规范到 ±根前向——人形前膝/龙鸟反膝各自保真。
    /// blend→0 时所有增量 Slerp 回 identity（idle=rest 原姿态，脚不必沾地）。</summary>
    public class LegGaitModule : IMotionModule, IGaitBlend
    {
        class Leg
        {
            public List<BoneRef> Chain;
            public Vector3 Hip, FootBase;
            public Vector3 ThighDir0, ShinDir0;  // rest 方向（单骨腿 ShinDir0=ThighDir0）
            public Quaternion RestWorldThigh;    // 大腿 rest 世界旋转（膝位重算基准）
            public Vector3 MiddleLocalPos;       // 膝在大腿局部系的 rest 偏移
            public Vector3 HintDir;              // 规范膝极向（±根前向，世界）
            public float L1, L2, Span;
            public float MinDist, MaxDist;
            public float Phase;
            public int Side;
            public bool TwoBone;
            public Renderer[] Renderers;         // 腿子树网格（脚端/贴地测量用）
        }

        readonly List<Leg> _legs = new List<Leg>();
        readonly Vector3 _fwd, _up;
        readonly float _boundDeg;
        public float GaitBlend { get; set; }
        /// <summary>腿伸直后根需抬高的量（米）——垂直伸直腿若比 rest 髋高更长会穿地，
        /// 装配层把此值喂给 RootIdleModule.WalkRootLift（模型直立、根仅偏航，Y 即垂直）。</summary>
        public readonly float RootLift;

        public LegGaitModule(List<List<BoneRef>> legChains, List<float> phases, List<int> sides,
            Vector3 rootUpWorld, Vector3 rootFwdWorld)
        {
            _up = rootUpWorld.normalized;
            _fwd = rootFwdWorld.normalized;
            float reach = RigMotionTuning.LegReachShare;
            float stride = RigMotionTuning.LegStrideShare;
            // 垂直落差份额：给迈步水平偏移留几何余量（支撑两端 D=√(V²+S²)≤reach·span）
            float vShare = Mathf.Sqrt(Mathf.Max(0.04f, reach * reach - stride * stride));

            float groundY = float.MaxValue;
            for (int i = 0; i < legChains.Count; i++)
            {
                var chain = legChains[i];
                var hipTr = chain[0].T;
                var hip = hipTr.position;
                var legRenderers = hipTr.GetComponentsInChildren<Renderer>();

                // 脚端 = 腿子树网格最低点（骨枢是关节，网格才是着地物；无网格回退末骨枢）
                bool hasBounds = false;
                var bounds = new Bounds(hip, Vector3.zero);
                for (int r = 0; r < legRenderers.Length; r++)
                {
                    if (!hasBounds) { bounds = legRenderers[r].bounds; hasBounds = true; }
                    else bounds.Encapsulate(legRenderers[r].bounds);
                }
                var footEnd = hasBounds
                    ? new Vector3(bounds.center.x, bounds.min.y, bounds.center.z)
                    : chain[chain.Count - 1].T.position;

                var leg = new Leg
                {
                    Chain = chain,
                    Hip = hip,
                    Phase = phases[i],
                    Side = sides != null && i < sides.Count ? sides[i] : 1,
                    Renderers = legRenderers,
                };
                var restWorldThigh = chain[0].ParentRestWorld * chain[0].RestLocal;
                leg.RestWorldThigh = restWorldThigh;

                float l1 = chain.Count >= 2 ? Vector3.Distance(chain[1].T.position, hip) : 0f;
                float l2 = chain.Count >= 2 ? Vector3.Distance(footEnd, chain[1].T.position) : 0f;
                leg.TwoBone = chain.Count >= 2 && l1 > 1e-3f && l2 > 1e-3f;
                if (leg.TwoBone)
                {
                    leg.L1 = l1;
                    leg.L2 = l2;
                    leg.Span = l1 + l2;
                    leg.ThighDir0 = (chain[1].T.position - hip).normalized;
                    leg.ShinDir0 = (footEnd - chain[1].T.position).normalized;
                    leg.MiddleLocalPos = Quaternion.Inverse(restWorldThigh) * (chain[1].T.position - hip);
                    leg.MinDist = Mathf.Abs(l1 - l2) * 1.05f;
                    leg.MaxDist = leg.Span * reach;
                    // 膝极向：膝相对髋→脚连线的垂线分量 → 投影到根前向取侧（正反关节）
                    var hipFoot = (footEnd - hip).normalized;
                    var hint0 = leg.ThighDir0 - hipFoot * Vector3.Dot(leg.ThighDir0, hipFoot);
                    float side = Vector3.Dot(hint0.sqrMagnitude > 1e-6f ? hint0.normalized : _fwd, _fwd);
                    leg.HintDir = side >= 0f ? _fwd : -_fwd;
                }
                else
                {
                    // 单骨腿：整段指向脚端，无膝——只做髋摆（弧线迈步）
                    leg.L1 = Vector3.Distance(footEnd, hip);
                    leg.L2 = 0f;
                    leg.Span = leg.L1;
                    leg.ThighDir0 = (footEnd - hip).normalized;
                    leg.ShinDir0 = leg.ThighDir0;
                    leg.MiddleLocalPos = Vector3.zero;
                    leg.MinDist = 0f;
                    leg.MaxDist = leg.Span * reach;
                    leg.HintDir = _fwd;
                }
                leg.FootBase = hip - _up * (vShare * leg.Span);
                _legs.Add(leg);
                groundY = Mathf.Min(groundY, footEnd.y);
            }

            // 双足强制左右反相：目录 biped 条目双腿常同相位（=兔子跳），按 side 分配 0/π；
            // 四足保留目录相位（侧序步态，前左/前右差 π/2 是合法步态）
            if (_legs.Count == 2)
            {
                float p0 = _legs[0].Side >= 0 ? 0f : Mathf.PI;
                float p1 = _legs[1].Side >= 0 ? 0f : Mathf.PI;
                if (Mathf.Approximately(p0, p1)) p1 = p0 + Mathf.PI; // side 异常时退回索引反相
                _legs[0].Phase = p0;
                _legs[1].Phase = p1;
            }

            // 根抬升（实测法，2026-10-13）：满幅步态扫一个周期取腿网格最低点——比「垂直
            // 落差公式」准（网格随姿态旋转后角点会下探）。构造期临时写姿态，测完精确还原 rest；
            // 采样间隙留 2cm 裕量（运行时任意 φ 不落回采样点上）
            float measuredMin = float.MaxValue;
            for (int s = 0; s < 32; s++)
            {
                float phi = Mathf.PI * 2f * s / 32f;
                for (int i = 0; i < _legs.Count; i++) ApplyLeg(_legs[i], phi + _legs[i].Phase, 1f);
                for (int i = 0; i < _legs.Count; i++)
                {
                    var rs = _legs[i].Renderers;
                    for (int r = 0; r < rs.Length; r++)
                        measuredMin = Mathf.Min(measuredMin, rs[r].bounds.min.y);
                }
            }
            for (int i = 0; i < _legs.Count; i++)
                for (int j = 0; j < _legs[i].Chain.Count; j++) _legs[i].Chain[j].ApplyRest();
            RootLift = Mathf.Clamp(groundY - measuredMin + 0.02f, 0f, 1f);

            // 钳制上限：对 φ 全周期用 Tick 同式采样真实最大偏移（w=1）
            float maxDeg = 0f;
            for (int s = 0; s < 64; s++)
            {
                float phi = Mathf.PI * 2f * s / 64f;
                for (int i = 0; i < _legs.Count; i++)
                {
                    Quaternion dT;
                    Vector3 effTarget;
                    SolveFull(_legs[i], phi, out dT, out effTarget);
                    maxDeg = Mathf.Max(maxDeg, Quaternion.Angle(Quaternion.identity, dT));
                    if (_legs[i].TwoBone)
                    {
                        var kneeNow = _legs[i].Hip + (dT * _legs[i].RestWorldThigh) * _legs[i].MiddleLocalPos;
                        Vector3 sd = effTarget - kneeNow;
                        if (sd.sqrMagnitude > 1e-10f)
                            maxDeg = Mathf.Max(maxDeg, Quaternion.Angle(Quaternion.identity,
                                Quaternion.FromToRotation(_legs[i].ShinDir0, sd.normalized)));
                    }
                }
            }
            _boundDeg = maxDeg + 1.5f;
        }

        public float MaxAngularDeltaDeg => _boundDeg;

        public void Tick(float t, float dt)
        {
            float w = Mathf.Clamp01(RigMotionTuning.GaitAmp(GaitBlend) * RigMotionTuning.GlobalAmplitude);
            float gait = RigMotionTuning.Phase(t, RigMotionTuning.KGait);
            for (int i = 0; i < _legs.Count; i++) ApplyLeg(_legs[i], gait + _legs[i].Phase, w);
        }

        /// <summary>单腿写入：满幅解算 → w 权重 Slerp → 世界增量共轭到父系乘 rest。
        /// 小腿指向「按 slerp 后大腿实际位置重算的膝位 → 有效目标」，blend 中途不发散。</summary>
        void ApplyLeg(Leg leg, float phi, float w)
        {
            Quaternion dT;
            Vector3 effTarget;
            SolveFull(leg, phi, out dT, out effTarget);
            var qT = Quaternion.Slerp(Quaternion.identity, dT, w);
            WriteLocal(leg.Chain[0], qT);
            if (!leg.TwoBone) return;

            var thighWorldNow = qT * leg.RestWorldThigh;
            var kneeNow = leg.Hip + thighWorldNow * leg.MiddleLocalPos;
            Vector3 sd = effTarget - kneeNow;
            var dS = sd.sqrMagnitude > 1e-10f
                ? Quaternion.FromToRotation(leg.ShinDir0, sd.normalized)
                : Quaternion.identity;
            WriteLocal(leg.Chain[1], Quaternion.Slerp(Quaternion.identity, dS, w));
            // 链≥3 骨（踝/趾骨）不另写增量：保持 rest 局部姿态刚体随小腿——曾试 0.5 跟随
            // 份额，会破坏 IK 闭合（脚尖落点偏目标→膝侧几何翻转，2026-10-13 ArmoredRat 后腿实测）
        }

        /// <summary>满幅解算：φ → 大腿增量 + 有效目标点（距离钳制后）。
        /// 目标 = 垂直基准 + 前后步幅 + 摆动相抬脚（max(0,sin)² 平滑触地）。
        /// 余弦定理 + 绕 cross(targetDir,hint) 正向旋转恒把膝推向极向侧
        /// （Synergy 教训：法向已含迈步方向，不能再乘方向符号）。</summary>
        void SolveFull(Leg leg, float phi, out Quaternion deltaThigh, out Vector3 effTarget)
        {
            float span = leg.Span;
            float swing = Mathf.Max(0f, Mathf.Sin(phi));
            Vector3 target = leg.FootBase
                + _fwd * (-RigMotionTuning.LegStrideShare * span * Mathf.Cos(phi))
                + _up * (RigMotionTuning.LegLiftShare * span * swing * swing);
            Vector3 toT = target - leg.Hip;
            float c = Mathf.Clamp(toT.magnitude, leg.MinDist, leg.MaxDist);
            Vector3 targetDir = toT.sqrMagnitude > 1e-10f ? toT.normalized : -_up;
            effTarget = leg.Hip + targetDir * c;

            if (!leg.TwoBone)
            {
                deltaThigh = Quaternion.FromToRotation(leg.ThighDir0, targetDir);
                return;
            }
            float cosU = Mathf.Clamp(
                (leg.L1 * leg.L1 + c * c - leg.L2 * leg.L2) / (2f * leg.L1 * c), -1f, 1f);
            float upper = Mathf.Acos(cosU) * Mathf.Rad2Deg;
            Vector3 n = Vector3.Cross(targetDir, leg.HintDir);
            if (n.sqrMagnitude < 1e-4f)
            {
                n = Vector3.Cross(targetDir, _up);
                if (n.sqrMagnitude < 1e-4f) n = Vector3.Cross(targetDir, _fwd);
            }
            Vector3 thighDir = Quaternion.AngleAxis(upper, n) * targetDir;
            deltaThigh = Quaternion.FromToRotation(leg.ThighDir0, thighDir);
        }

        /// <summary>世界增量 → 实时父旋转共轭写入（父级可能已被本模块/其他模块本次写过：
        /// 串行链下用捕获的静止父旋转会多乘一份父增量，逐级累积把姿态转飞——2026-10-13
        /// 蛇拉直失效根因）。local = Inv(parentNow)·delta·restWorld，纯世界语义。</summary>
        static void WriteLocal(BoneRef b, Quaternion worldDelta)
        {
            var parentNow = b.T.parent != null ? b.T.parent.rotation : Quaternion.identity;
            var restWorld = b.ParentRestWorld * b.RestLocal;
            b.T.localRotation = Quaternion.Inverse(parentNow) * worldDelta * restWorld;
        }
    }

    // ============================ 头注视 ============================

    /// <summary>头部注视：最近敌方单位 → 对方角色 → 无目标。
    /// 默认完全不控头（idle 态不写任何骨骼，保持烘焙 idle 姿态）；有目标时头+颈链
    /// 按份额分担钳制后的偏转（头 LookHeadShare，颈均摊），目标消失平滑回 rest 后停写。</summary>
    public class HeadLookAtModule : IMotionModule
    {
        readonly List<BoneRef> _chain;      // 头在前，颈近头在前
        readonly List<float> _shares;
        readonly Quaternion _rootRestWorld;
        readonly Vector3 _restForward;      // 根空间头朝向（目录 headForward）
        Func<Vector3?> _supplier;

        float _curYaw, _curPitch;           // 当前平滑值（度，根空间）
        bool _engaged;                      // 有目标或正在回中
        bool _idle = true;                  // 默认态：不写头/颈

        public HeadLookAtModule(List<BoneRef> chain, Quaternion rootRestWorld, Vector3 restForwardRoot)
        {
            _chain = chain;
            _rootRestWorld = rootRestWorld;
            _restForward = restForwardRoot.sqrMagnitude > 1e-6f ? restForwardRoot.normalized : Vector3.forward;
            _shares = new List<float>(chain.Count);
            int neckCount = chain.Count - 1;
            for (int i = 0; i < chain.Count; i++)
                _shares.Add(i == 0
                    ? RigMotionTuning.LookHeadShare
                    : (1f - RigMotionTuning.LookHeadShare) / Mathf.Max(1, neckCount));
        }

        public void SetSupplier(Func<Vector3?> supplier) => _supplier = supplier;
        public bool IsIdle => _idle;

        public float MaxAngularDeltaDeg
            => RigMotionTuning.LookYawClampDeg + RigMotionTuning.LookPitchClampDeg;

        public void Tick(float t, float dt)
        {
            Vector3? target = null;
            if (_supplier != null) target = _supplier();
            bool valid = target.HasValue && (target.Value - _chain[0].T.position).sqrMagnitude > 1e-6f;

            if (valid)
            {
                Decompose(target.Value, out float yaw, out float pitch);
                yaw = Mathf.Clamp(yaw, -RigMotionTuning.LookYawClampDeg, RigMotionTuning.LookYawClampDeg);
                pitch = Mathf.Clamp(pitch, -RigMotionTuning.LookPitchClampDeg, RigMotionTuning.LookPitchClampDeg);
                SmoothTowards(yaw, pitch, dt);
                _engaged = true;
                _idle = false;
            }
            else if (_engaged)
            {
                SmoothTowards(0f, 0f, dt);
                if (Mathf.Abs(_curYaw) < RigMotionTuning.LookRecenterEpsDeg
                    && Mathf.Abs(_curPitch) < RigMotionTuning.LookRecenterEpsDeg)
                {
                    for (int i = 0; i < _chain.Count; i++) _chain[i].ApplyRest();
                    _curYaw = _curPitch = 0f;
                    _engaged = false;
                    _idle = true;
                    return;
                }
            }
            else return; // 默认不控头

            WriteRotation();
        }

        /// <summary>目标方向 → 根空间相对 restForward 的偏航/俯仰（度）。</summary>
        void Decompose(Vector3 target, out float yaw, out float pitch)
        {
            Vector3 dirRoot = Quaternion.Inverse(_rootRestWorld) * (target - _chain[0].T.position);
            Vector3 restHoriz = new Vector3(_restForward.x, 0f, _restForward.z);
            Vector3 dirHoriz = new Vector3(dirRoot.x, 0f, dirRoot.z);
            if (restHoriz.sqrMagnitude < 1e-6f) restHoriz = Vector3.forward;
            if (dirHoriz.sqrMagnitude < 1e-6f) dirHoriz = restHoriz; // 目标正上方/下方：俯仰表达
            yaw = Vector3.SignedAngle(restHoriz, dirHoriz, Vector3.up);
            float restPitch = Mathf.Atan2(_restForward.y, restHoriz.magnitude);
            float dirPitch = Mathf.Atan2(dirRoot.y, dirHoriz.magnitude);
            pitch = (dirPitch - restPitch) * Mathf.Rad2Deg;
        }

        void SmoothTowards(float yaw, float pitch, float dt)
        {
            float k = 1f - Mathf.Exp(-RigMotionTuning.LookSpeed * Mathf.Max(dt, 1e-4f));
            _curYaw = Mathf.LerpAngle(_curYaw, yaw, k);
            _curPitch = Mathf.LerpAngle(_curPitch, pitch, k);
        }

        void WriteRotation()
        {
            Vector3 restHoriz = new Vector3(_restForward.x, 0f, _restForward.z);
            if (restHoriz.sqrMagnitude < 1e-6f) restHoriz = Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, restHoriz).normalized;
            Quaternion qRoot = Quaternion.AngleAxis(_curYaw, Vector3.up) * Quaternion.AngleAxis(_curPitch, right);
            Quaternion invRoot = Quaternion.Inverse(_rootRestWorld);
            for (int i = 0; i < _chain.Count; i++)
            {
                Quaternion worldDelta = _rootRestWorld * Quaternion.Slerp(Quaternion.identity, qRoot, _shares[i]) * invRoot;
                Quaternion localDelta = Quaternion.Inverse(_chain[i].ParentRestWorld) * worldDelta * _chain[i].ParentRestWorld;
                _chain[i].T.localRotation = localDelta * _chain[i].RestLocal;
            }
        }
    }

    /// <summary>多节肢：逐腿相位涟漪（各腿绕根 up 微摆+轻微抬落），身体低频轻浮由根模块负责。</summary>
    public class SpiderRippleModule : IMotionModule, IGaitBlend
    {
        readonly List<BoneRef> _legRoots;
        readonly List<Vector3> _upAxes, _rightAxes;
        readonly List<float> _phases;
        public float GaitBlend { get; set; }

        public SpiderRippleModule(List<List<BoneRef>> legChains, List<float> phases,
            Vector3 rootUpWorld, Vector3 rootRightWorld)
        {
            _legRoots = new List<BoneRef>(legChains.Count);
            _upAxes = new List<Vector3>(legChains.Count);
            _rightAxes = new List<Vector3>(legChains.Count);
            _phases = phases;
            for (int i = 0; i < legChains.Count; i++)
            {
                _legRoots.Add(legChains[i][0]);
                _upAxes.Add(legChains[i][0].WorldAxisInParent(rootUpWorld));
                _rightAxes.Add(legChains[i][0].WorldAxisInParent(rootRightWorld));
            }
        }

        public float MaxAngularDeltaDeg => RigMotionTuning.SpiderRippleDeg;

        public void Tick(float t, float dt)
        {
            float g = RigMotionTuning.GlobalAmplitude * RigMotionTuning.GaitAmp(GaitBlend);
            float ripple = RigMotionTuning.Phase(t, RigMotionTuning.KRipple);
            for (int i = 0; i < _legRoots.Count; i++)
            {
                float sway = RigMotionTuning.MirrorX * RigMotionTuning.SpiderRippleDeg * g * Mathf.Sin(ripple + _phases[i]);
                float lift = (RigMotionTuning.SpiderRippleDeg / 3f) * g * Mathf.Max(0f, Mathf.Sin(ripple + _phases[i]));
                _legRoots[i].T.localRotation =
                    Quaternion.AngleAxis(sway, _upAxes[i]) *
                    Quaternion.AngleAxis(lift, _rightAxes[i]) *
                    _legRoots[i].RestLocal;
            }
        }
    }
}
