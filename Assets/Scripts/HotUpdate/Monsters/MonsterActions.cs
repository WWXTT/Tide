using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonsterRig
{
    /// <summary>动作类别（挂在模型身上的 MonsterRigAnimator.Play 播放）。</summary>
    public enum MonsterAction
    {
        Breath,   // 呼吸（默认待机：体型呼吸+轻步态）
        Walk,     // 行走（原地步态加强版；宿主移动模型时播这个）
        Pause,    // 暂停（冻结当前姿态；再播 Breath/Walk 恢复）
        Attack,   // 攻击（一次性前扑，叠在当前状态上）
        Hit,      // 受击（一次性压扁回弹，叠在当前状态上）
        FallDown, // 倒地（部件脱离父子关系随机散落；复活用 Revive()）
    }

    /// <summary>
    /// 倒地散落模拟（无 Rigidbody，确定性可回放）：所有部件平铺到模型根下（脱离原父子链），
    /// 各自带随机抛射速度/角速度做重力抛物线+落地反弹衰减，静止后保持散落。
    /// Revive 走 RigDriver：先把部件挂回原父级（保持世界位）再补间回 rest 姿态。
    /// </summary>
    public class ScatterSim
    {
        public struct Piece
        {
            public Transform T;
            public Vector3 Pos;        // 世界位（模拟用）
            public Quaternion Rot;
            public Vector3 Vel;
            public Vector3 AngAxis;    // 角速度轴（世界）
            public float AngSpeed;     // 弧度/秒
            public float Radius;       // 近似半径（着地高度）
            public bool Settled;
        }

        readonly List<Piece> _pieces = new List<Piece>();
        readonly System.Random _rng;
        readonly float _groundY;
        float _stillTime;
        const float SettleStillSeconds = 0.6f;
        const float Gravity = 14f;       // 风格化重力（稍大于真实值，碎块干脆）
        const float SleepSpeed = 0.05f;

        public bool Settled { get; private set; }
        public IReadOnlyList<Piece> Pieces => _pieces;

        ScatterSim(int seed, float groundY)
        {
            _rng = new System.Random(seed);
            _groundY = groundY;
        }

        /// <summary>开始散落：driver 需先停模块并平铺部件到根下（保持世界位），再调这里收集部件。</summary>
        public static ScatterSim Begin(IEnumerable<BoneRef> bones, Transform root, int seed)
        {
            var sim = new ScatterSim(seed, root.position.y);
            Vector3 center = root.position;
            // 中心取网格包围盒均值（比根原点更贴身体）
            int c = 0; double sx = 0, sy = 0, sz = 0;
            foreach (var b in bones)
            {
                var mf = b.T.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                Vector3 wc = b.T.localToWorldMatrix.MultiplyPoint3x4(mf.sharedMesh.bounds.center);
                sx += wc.x; sy += wc.y; sz += wc.z; c++;
            }
            if (c > 0) center = new Vector3((float)(sx / c), (float)(sy / c), (float)(sz / c));

            foreach (var b in bones)
            {
                if (b.T == root) continue;
                var rend = b.T.GetComponent<MeshRenderer>();
                var mf = b.T.GetComponent<MeshFilter>();
                if (rend == null || mf == null || mf.sharedMesh == null) continue; // 空骨不飞

                var bounds = mf.sharedMesh.bounds;
                float radius = Mathf.Max(0.03f, bounds.extents.magnitude * 0.7f);
                Vector3 pos = b.T.position;
                Vector3 dir = pos - center;
                dir.y = 0f;
                if (dir.sqrMagnitude < 1e-6f) dir = RandomDir(sim._rng);
                dir.Normalize();

                float speed = Lerp(sim._rng, 2.0f, 4.5f);
                float up = Lerp(sim._rng, 1.0f, 3.0f);
                var piece = new Piece
                {
                    T = b.T,
                    Pos = pos,
                    Rot = b.T.rotation,
                    Vel = dir * speed + Vector3.up * up,
                    AngAxis = RandomDir(sim._rng),
                    AngSpeed = Lerp(sim._rng, Mathf.PI, Mathf.PI * 4f), // 180~720°/s
                    Radius = radius,
                };
                sim._pieces.Add(piece);
            }
            return sim;
        }

        public void Tick(float dt)
        {
            if (Settled) return;
            dt = Mathf.Min(dt, 1f / 30f);
            bool anyMoving = false;
            for (int i = 0; i < _pieces.Count; i++)
            {
                var p = _pieces[i];
                if (p.Settled) continue;
                p.Vel.y -= Gravity * dt;
                p.Pos += p.Vel * dt;
                float floor = _groundY + p.Radius * 0.4f;
                if (p.Pos.y < floor)
                {
                    p.Pos.y = floor;
                    p.Vel.y = -p.Vel.y * 0.3f;
                    p.Vel.x *= 0.55f;
                    p.Vel.z *= 0.55f;
                    p.AngSpeed *= 0.45f;
                    if (p.Vel.magnitude < SleepSpeed && p.AngSpeed < SleepSpeed * 4f)
                    {
                        p.Settled = true;
                        p.Vel = Vector3.zero;
                        p.AngSpeed = 0f;
                    }
                    else anyMoving = true;
                }
                else anyMoving = true;

                if (p.AngSpeed != 0f)
                    p.Rot = Quaternion.AngleAxis(p.AngSpeed * dt * Mathf.Rad2Deg, p.AngAxis) * p.Rot;
                p.T.position = p.Pos;
                p.T.rotation = p.Rot;
                _pieces[i] = p;
            }
            if (!anyMoving)
            {
                _stillTime += dt;
                if (_stillTime >= SettleStillSeconds) Settled = true;
            }
            else _stillTime = 0f;
        }

        static Vector3 RandomDir(System.Random rng)
        {
            float a = (float)rng.NextDouble() * Mathf.PI * 2f;
            float y = (float)(rng.NextDouble() * 2.0 - 1.0);
            float r = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            return new Vector3(r * Mathf.Cos(a), y, r * Mathf.Sin(a));
        }

        static float Lerp(System.Random rng, float a, float b) => a + (b - a) * (float)rng.NextDouble();
    }
}
