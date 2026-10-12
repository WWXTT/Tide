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
        public const int KSnake = 2;    // 蛇形行波 6s

        // 呼吸族
        public const float BreathAmpTailDeg = 6f;
        public const float AbdomenScaleAmp = 0.03f;

        // 体型族（2026-10-12 用户验收：左右摇摆幅度×2）
        public const float SnakeWaveDeg = 14f;
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
            tr.localPosition = _root.RestLocalPos + _upLocal * (bob + popY) + atkOff;
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

    /// <summary>沿链行进横波（蛇形）：每段绕根系 up 轴做相位延迟正弦摆动，幅度向尾端渐增。</summary>
    public class SpineWaveModule : IMotionModule, IGaitBlend
    {
        readonly List<BoneRef> _segments;
        readonly List<Vector3> _upAxes;    // 各段父系内的摆动轴
        readonly float _ampDeg;
        readonly int _harmonic;
        public float GaitBlend { get; set; }

        public SpineWaveModule(List<BoneRef> segments, Vector3 rootUpWorld, float ampDeg, int harmonic)
        {
            _segments = segments;
            _ampDeg = ampDeg;
            _harmonic = harmonic;
            _upAxes = new List<Vector3>(_segments.Count);
            for (int i = 0; i < _segments.Count; i++)
                _upAxes.Add(_segments[i].WorldAxisInParent(rootUpWorld));
        }

        public float MaxAngularDeltaDeg => _ampDeg;

        public void Tick(float t, float dt)
        {
            float g = RigMotionTuning.GlobalAmplitude * RigMotionTuning.GaitAmp(GaitBlend);
            int n = _segments.Count;
            for (int i = 0; i < n; i++)
            {
                float taper = n <= 1 ? 1f : 0.4f + 0.6f * i / (n - 1);
                float angle = RigMotionTuning.MirrorX * _ampDeg * g * taper
                    * Mathf.Sin(RigMotionTuning.Phase(t, _harmonic) - i * RigMotionTuning.SnakeSegDelay);
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
    /// 2026-10-12 用户验收）——频率渐变到步态谐波、幅度 WalkFlapBoost 增强。</summary>
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

    /// <summary>四足：对角步相位原地踏步（只抬不落 = mark-time）+ 躯干缓慢重心摆。</summary>
    public class QuadrupedWeightModule : IMotionModule, IGaitBlend
    {
        readonly List<BoneRef> _legRoots;
        readonly List<Vector3> _legAxes;    // 抬腿俯仰轴（根右向）
        readonly List<float> _phases;
        readonly BoneRef _body;
        readonly Vector3 _upWorld;
        readonly float _maxDeg;
        public float GaitBlend { get; set; }

        public QuadrupedWeightModule(List<List<BoneRef>> legChains, List<float> phases, BoneRef body,
            Vector3 rootUpWorld, Vector3 rootRightWorld)
        {
            _body = body;
            _upWorld = rootUpWorld;
            _legRoots = new List<BoneRef>(legChains.Count);
            _legAxes = new List<Vector3>(legChains.Count);
            _phases = phases;
            for (int i = 0; i < legChains.Count; i++)
            {
                _legRoots.Add(legChains[i][0]);
                _legAxes.Add(legChains[i][0].WorldAxisInParent(rootRightWorld));
            }
            _maxDeg = Mathf.Max(RigMotionTuning.QuadLegLiftDeg, RigMotionTuning.QuadBodyRollDeg);
        }

        public float MaxAngularDeltaDeg => _maxDeg;

        public void Tick(float t, float dt)
        {
            float g = RigMotionTuning.GlobalAmplitude * RigMotionTuning.GaitAmp(GaitBlend);
            float gait = RigMotionTuning.Phase(t, RigMotionTuning.KGait);
            for (int i = 0; i < _legRoots.Count; i++)
            {
                float lift = RigMotionTuning.QuadLegLiftDeg * g * Mathf.Max(0f, Mathf.Sin(gait + _phases[i]));
                _legRoots[i].T.localRotation = Quaternion.AngleAxis(lift, _legAxes[i]) * _legRoots[i].RestLocal;
            }
            if (_body != null)
            {
                float roll = RigMotionTuning.MirrorX * RigMotionTuning.QuadBodyRollDeg * g
                    * Mathf.Sin(RigMotionTuning.Phase(t, RigMotionTuning.KSway));
                var axis = _body.WorldAxisInParent(_upWorld);
                _body.T.localRotation = Quaternion.AngleAxis(roll, axis) * _body.RestLocal;
            }
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
