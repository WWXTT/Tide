using UnityEngine;

namespace SynergyUI
{
    /// <summary>
    /// 战场环境风（2026-10-23 v4 §10.5 定案）：单一驱动器挂在 BattleStage 根（BattleHexBoard.Build
    /// 预制体装台后 AddComponent）——共享风向/风强（sin 时间驱动，慢起伏阵风），
    /// 植物（Env/added-plants 全部实例）按实例相位 ±3~6° 绕水平轴（垂直风向）局部摆动。
    /// 零每帧分配：Start 一次收集数组，Update 只写旋转（值类型运算，无 new/Find/LINQ）。
    /// 一键关=WindEnabled 常量置 false（Update 直退，数组保留无害）。
    ///（篝火支路随火延期整支退役——2026-10-23 用户删火调位、火后续再加；曾致
    /// "Particle Velocity curves must all be in the same mode" 刷屏：velocityOverLifetime
    /// x/z 写两常量而 y 是常量模式。火回归时若再接横向风，三轴必须同 MinMaxCurve 模式。）
    /// </summary>
    public sealed class BattleEnvWind : MonoBehaviour
    {
        /// <summary>总开关（false=静止环境，§10.5「可一键关」）。</summary>
        public const bool WindEnabled = true;

        // 共享风场：风向恒定（世界系），风强=基风+阵风 sin 包络
        private static readonly Vector2 WindDir = new Vector2(0.94f, 0.34f).normalized; // xz 平面单位向量
        private const float GustCycle = 0.31f;     // 阵风主频（rad/s）——~20s 一轮强弱
        private const float GustMin = 0.25f;       // 最弱风强（无死寂）
        private const float GustMax = 1f;          // 最强风强
        private const float PlantAmpMin = 3f;      // 植物摆幅下限（°）
        private const float PlantAmpMax = 6f;      // 植物摆幅上限（°）
        private const float PlantSwayHz = 1.7f;    // 植物摇摆角频率（rad/s）

        private Transform[] _plants;
        private Quaternion[] _plantBase; // 收集时世界旋转（摆动=绕世界水平轴的偏转×基础旋转）
        private float[] _plantPhase;
        private float[] _plantAmp;

        private void Start()
        {
            Collect();
            if (WindEnabled) Update(); // 首帧即生效（避免零帧站姿闪现）
        }

        /// <summary>收集摆动体（仅一次；层级约定=BattleStage/Env/added-plants）。</summary>
        private void Collect()
        {
            var env = transform.Find("Env");
            if (env == null) return;
            var plantsRoot = env.Find("added-plants");
            if (plantsRoot == null) return;
            int n = plantsRoot.childCount;
            _plants = new Transform[n];
            _plantBase = new Quaternion[n];
            _plantPhase = new float[n];
            _plantAmp = new float[n];
            int i = 0;
            foreach (Transform c in plantsRoot)
            {
                _plants[i] = c;
                _plantBase[i] = c.rotation;
                _plantPhase[i] = Random.value * Mathf.PI * 2f;          // 实例相位（收集时一次随机，非每帧）
                _plantAmp[i] = Random.Range(PlantAmpMin, PlantAmpMax);  // 实例摆幅 3~6°
                i++;
            }
        }

        private void Update()
        {
            if (!WindEnabled || _plants == null) return;
            float t = Time.time;
            // 阵风包络：基风之上叠两个慢 sin，避免机械节拍
            float gust = Mathf.InverseLerp(-1f, 1f, Mathf.Sin(t * GustCycle) * 0.7f + Mathf.Sin(t * GustCycle * 2.3f) * 0.3f);
            float strength = Mathf.Lerp(GustMin, GustMax, gust);

            // 水平摆动轴=上向量×风向（植物绕它向风向两侧摆；世界系一次算好）
            var axis = new Vector3(WindDir.y, 0f, -WindDir.x);
            for (int i = 0; i < _plants.Length; i++)
            {
                var p = _plants[i];
                if (p == null) continue; // 拆台竞态（数组随根一起销毁，防御 fake-null 残帧）
                float sway = Mathf.Sin(t * PlantSwayHz + _plantPhase[i]) * _plantAmp[i] * strength;
                p.rotation = Quaternion.AngleAxis(sway, axis) * _plantBase[i];
            }
        }
    }
}
