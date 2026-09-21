using UnityEngine;

namespace HexMap
{
    /// <summary>
    /// 植被踩踏交互驱动（交互区域控制）：把最近活跃玩家的位置/扰动写入 shader 全局
    /// （_PlayerPosition/_BendRadius/_BendAmountGrass/_BendDirection/_Disturbance），
    /// HexGrass 顶点按球形遮罩弯曲。语义对齐 TTFE PlantInteractionShader 的简化版，
    /// 但走 Shader.SetGlobal——Entities Graphics(BRG) 批次无需逐材质同步。
    ///
    /// players 留空 = 关闭踩踏（_BendAmountGrass 恒 0，只剩全局风）；
    /// 当前没有角色实体，字段预留给将来的玩家 Transform。
    /// </summary>
    public class HexPlayerBendDriver : MonoBehaviour
    {
        [Header("交互源（留空 = 关闭踩踏）")]
        [Tooltip("踩踏源 Transform（玩家/单位）。留空时只保留全局风摆")]
        public Transform[] players;

        [Header("弯曲参数")]
        public float bendRadius = 2f;
        [Range(0f, 1f)] public float bendAmount = 0.5f;
        [Tooltip("推开方向（世界空间，会被归一化）")]
        public Vector3 bendDirection = new(1f, 0.15f, 1f);
        [Tooltip("扰动衰减速度（每秒）")]
        [Range(0.1f, 5f)] public float decayRate = 1.5f;
        [Tooltip("判定『活跃』的最小位移（米/帧）")]
        public float movementThreshold = 0.05f;

        private static readonly int PlayerPositionID = Shader.PropertyToID("_PlayerPosition");
        private static readonly int BendRadiusID = Shader.PropertyToID("_BendRadius");
        private static readonly int BendAmountID = Shader.PropertyToID("_BendAmountGrass");
        private static readonly int BendDirectionID = Shader.PropertyToID("_BendDirection");
        private static readonly int DisturbanceID = Shader.PropertyToID("_Disturbance");

        private Vector3[] _lastPositions = System.Array.Empty<Vector3>();
        private float[] _disturbance = System.Array.Empty<float>();

        private void OnEnable()
        {
            // 进 Play / 组件启用即写一次安全缺省（关闭弯曲），避免残留旧全局值
            Shader.SetGlobalFloat(BendAmountID, 0f);
        }

        private void LateUpdate()
        {
            SyncPlayerBuffers();

            float bestDisturbance = 0f;
            Transform bestPlayer = null;

            for (int i = 0; i < players.Length; i++)
            {
                var p = players[i];
                if (p == null)
                    continue;

                bool moved = (p.position - _lastPositions[i]).sqrMagnitude
                             > movementThreshold * movementThreshold;
                if (moved)
                    _disturbance[i] = 1f;
                else
                    _disturbance[i] = Mathf.Max(0f, _disturbance[i] - decayRate * Time.deltaTime);
                _lastPositions[i] = p.position;

                if (_disturbance[i] > bestDisturbance)
                {
                    bestDisturbance = _disturbance[i];
                    bestPlayer = p;
                }
            }

            if (bestPlayer == null)
            {
                Shader.SetGlobalFloat(BendAmountID, 0f);
                return;
            }

            Shader.SetGlobalVector(PlayerPositionID, bestPlayer.position);
            Shader.SetGlobalFloat(BendRadiusID, Mathf.Max(0.01f, bendRadius));
            Shader.SetGlobalFloat(BendAmountID, bendAmount);
            Shader.SetGlobalVector(BendDirectionID, bendDirection == Vector3.zero
                ? new Vector3(1f, 0.15f, 1f)
                : bendDirection.normalized);
            Shader.SetGlobalFloat(DisturbanceID, bestDisturbance);
        }

        private void SyncPlayerBuffers()
        {
            int n = players?.Length ?? 0;
            if (_lastPositions.Length != n)
            {
                _lastPositions = new Vector3[n];
                _disturbance = new float[n];
                for (int i = 0; i < n; i++)
                {
                    _lastPositions[i] = players[i] != null ? players[i].position : Vector3.zero;
                    _disturbance[i] = 0f;
                }
            }
        }
    }
}
