using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>
    /// 棋盘浮起拼装（2026-10-13 定案：对战开始棋格从相机视角外浮起组成棋盘，
    /// 逐格速度（时长）与到达时间随机，2s 窗口内全部落位）。
    ///
    /// 出生点=目标点投到相机系后在该深度视锥外接圆外取随机角（数据层保证首帧必在画外）；
    /// 落位钉回格心公式位姿（BoardMountSmoke 坐标对拍口径不受动画影响）。
    /// 挂点：BattleStageDirector.MountBoardAndDawnAsync 取景后启动（与日出并行，FireAndForget）；
    /// BoardMountSmoke 以 Running 门禁等落位后再对拍。中途拆台（root 销毁）自动弃演。
    /// </summary>
    public static class BattleBoardAssemble
    {
        /// <summary>拼装窗口上限：逐格 delay+duration ≤ 此值（定案 2s 内组装完毕）。</summary>
        public const float WindowSeconds = 2f;

        private const float DurationMin = 0.9f; // 单格飞行时长下限（速度随机=时长随机）
        private const float DurationMax = 1.4f;
        private const float DepthJitter = 2f;   // 出生深度抖动（相机系 z ±）
        private const float RingMin = 1.05f;    // 出生环半径系数（×视锥外接圆）
        private const float RingMax = 1.45f;
        private const float TiltDegrees = 35f;  // 出生俯/侧倾幅度
        private const float SpinDegrees = 90f;  // 出生自旋幅度

        /// <summary>拼装进行中（冒烟等待门禁；未启动/已落位=false）。</summary>
        public static bool Running { get; private set; }

        /// <summary>逐格浮起拼装。root/cam 缺件直接返回（降级=静态盘，不阻塞日出）。</summary>
        public static async UniTask PlayAsync(Transform root, Camera cam)
        {
            if (root == null || cam == null) return;

            var tiles = new List<Transform>();
            foreach (Transform child in root) tiles.Add(child);
            if (tiles.Count == 0) return;

            Running = true;
            try
            {
                var worldToCam = cam.worldToCameraMatrix;
                var target = new Vector3[tiles.Count];
                var spawn = new Vector3[tiles.Count];
                var spawnRot = new Quaternion[tiles.Count];
                var delay = new float[tiles.Count];
                var duration = new float[tiles.Count];
                float worstArrival = 0f;

                for (int i = 0; i < tiles.Count; i++)
                {
                    target[i] = tiles[i].localPosition; // 格心公式位（Build 摆好的终态）
                    duration[i] = Random.Range(DurationMin, DurationMax);
                    delay[i] = Random.Range(0f, WindowSeconds - duration[i]);
                    worstArrival = Mathf.Max(worstArrival, delay[i] + duration[i]);
                    spawn[i] = root.InverseTransformPoint(SpawnWorld(tiles[i].position, cam, worldToCam));
                    spawnRot[i] = Quaternion.Euler(
                        Random.Range(-TiltDegrees, TiltDegrees),
                        Random.Range(-SpinDegrees, SpinDegrees),
                        Random.Range(-TiltDegrees, TiltDegrees));
                    tiles[i].localPosition = spawn[i];
                    tiles[i].localRotation = spawnRot[i];
                }

                float t0 = Time.realtimeSinceStartup;
                while (true)
                {
                    await UniTask.Yield();
                    if (root == null) return; // 中途拆台：实例已销毁，弃演

                    float now = Time.realtimeSinceStartup - t0;
                    bool done = true;
                    for (int i = 0; i < tiles.Count; i++)
                    {
                        if (tiles[i] == null) continue;
                        float k = Mathf.Clamp01((now - delay[i]) / duration[i]);
                        if (k < 1f) done = false;
                        float e = 1f - (1f - k) * (1f - k) * (1f - k); // easeOutCubic：快进缓停=浮起落定
                        tiles[i].localPosition = Vector3.Lerp(spawn[i], target[i], e);
                        tiles[i].localRotation = Quaternion.Slerp(spawnRot[i], Quaternion.identity, e);
                    }
                    if (done) break;
                }

                // 落位钉死：位姿回到格心公式精确值（消浮点残差，对拍口径不变）
                for (int i = 0; i < tiles.Count; i++)
                    if (tiles[i] != null)
                    {
                        tiles[i].localPosition = target[i];
                        tiles[i].localRotation = Quaternion.identity;
                    }

                Debug.Log($"[BattleBoardAssemble][PASS] 浮起拼装 {tiles.Count} 格" +
                          $" 最晚到达={worstArrival:0.00}s（窗口 {WindowSeconds:0.0}s）");
            }
            finally
            {
                Running = false;
            }
        }

        /// <summary>出生点：目标投到相机系，在该深度视锥外接圆外取随机角——出外接圆必在画外。</summary>
        private static Vector3 SpawnWorld(Vector3 worldTarget, Camera cam, Matrix4x4 worldToCam)
        {
            var vp = worldToCam.MultiplyPoint3x4(worldTarget);
            float z = vp.z + Random.Range(-DepthJitter, DepthJitter);
            float halfH = cam.orthographic
                ? cam.orthographicSize
                : -z * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float halfW = halfH * cam.aspect;
            float ringR = Mathf.Sqrt(halfW * halfW + halfH * halfH); // 外接圆：圆外点必在矩形外
            float a = Random.Range(0f, Mathf.PI * 2f);
            float r = ringR * Random.Range(RingMin, RingMax);
            return cam.cameraToWorldMatrix.MultiplyPoint3x4(
                new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, z));
        }
    }
}
