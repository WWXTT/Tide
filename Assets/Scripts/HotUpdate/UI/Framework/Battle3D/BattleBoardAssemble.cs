using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>
    /// 棋盘浮起拼装（2026-10-13 定案 → 2026-10-23 13:00 v3 改版：出生点从「投到视锥外」
    /// （观感=自地下/画外钻入）改为**原地自下 0.2m 轻落**，总窗口 ≤0.6s；逐格时长/到达随机。
    /// 落位钉回格心公式位姿（BoardMountSmoke 坐标对拍口径不受动画影响）。
    /// 挂点：BattleStageDirector.MountBoardAndDawnAsync 取景后启动（与日出并行，FireAndForget）；
    /// BoardMountSmoke 以 Running 门禁等落位后再对拍（Running 语义保留）。中途拆台（root 销毁）自动弃演。
    /// </summary>
    public static class BattleBoardAssemble
    {
        /// <summary>拼装窗口上限（v3：轻落总窗口 0.6s）。</summary>
        public const float WindowSeconds = 0.6f;

        private const float DurationMin = 0.32f; // 单格轻落时长下限（速度随机=时长随机）
        private const float DurationMax = 0.5f;
        private const float DropHeight = 0.2f;   // 自下轻落高度（v3：不再画外飞入）
        private const float MaxDelay = 0.12f;    // 逐格错峰上限（delay+duration ≤ 窗口）

        /// <summary>拼装进行中（冒烟等待门禁；未启动/已落位=false）。</summary>
        public static bool Running { get; private set; }

        /// <summary>逐格轻落。root 缺件直接返回（降级=静态盘，不阻塞日出）；cam 兼容保留参数（v3 不用）。</summary>
        public static async UniTask PlayAsync(Transform root, Camera cam)
        {
            if (root == null) return;

            var tiles = new List<Transform>();
            foreach (Transform child in root) tiles.Add(child);
            if (tiles.Count == 0) return;

            Running = true;
            try
            {
                var target = new Vector3[tiles.Count];
                var delay = new float[tiles.Count];
                var duration = new float[tiles.Count];
                float worstArrival = 0f;

                for (int i = 0; i < tiles.Count; i++)
                {
                    target[i] = tiles[i].localPosition; // 格心公式位（Build 摆好的终态）
                    duration[i] = Random.Range(DurationMin, DurationMax);
                    delay[i] = Random.Range(0f, Mathf.Min(MaxDelay, WindowSeconds - duration[i]));
                    worstArrival = Mathf.Max(worstArrival, delay[i] + duration[i]);
                    tiles[i].localPosition = target[i] + Vector3.down * DropHeight; // 出生=终态正下方 0.2m
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
                        float e = 1f - (1f - k) * (1f - k); // easeOut：快落缓停
                        tiles[i].localPosition = Vector3.Lerp(target[i] + Vector3.down * DropHeight, target[i], e);
                    }
                    if (done) break;
                }

                // 落位钉死：位姿回到格心公式精确值（消浮点残差，对拍口径不变）
                for (int i = 0; i < tiles.Count; i++)
                    if (tiles[i] != null)
                        tiles[i].localPosition = target[i];

                Debug.Log($"[BattleBoardAssemble][PASS] 轻落拼装 {tiles.Count} 格" +
                          $" 最晚到达={worstArrival:0.00}s（窗口 {WindowSeconds:0.0}s·v3 自下{DropHeight:0.00}m）");
            }
            finally
            {
                Running = false;
            }
        }
    }
}
