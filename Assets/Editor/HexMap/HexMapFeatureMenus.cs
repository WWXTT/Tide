using Unity.Entities;
using UnityEditor;
using UnityEngine;

namespace HexMap.EditorTools
{
    /// <summary>
    /// 特征菜单（计划 7.3）。全部仅 Play 模式（cell 实体不存在于 Edit 模式）。
    /// POI 在 Play 中放置、资产持久（HexMapFeatureSettings 是 ScriptableObject）。
    /// </summary>
    public static class HexMapFeatureMenus
    {
        [MenuItem("Tools/HexMap/重新生成地形特征")]
        public static void RegenerateFeatures()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[HexMap] 需在 Play 模式执行（Edit 模式无 cell 实体）");
                return;
            }

            var world = World.DefaultGameObjectInjectionWorld;
            var em = world.EntityManager;

            // 全图补建（无视半径/帧预算），随后请求完整重生成（高程也重置回噪声值）
            world.GetExistingSystemManaged<HexChunkStreamingSystem>().EnsureAllLoaded();

            var configEntity = em.CreateEntityQuery(typeof(HexMapConfig)).GetSingletonEntity();
            if (em.HasComponent<HexFeatureRegenerateRequest>(configEntity))
                em.SetComponentData(configEntity, new HexFeatureRegenerateRequest { ResetElevation = true });
            else
                em.AddComponentData(configEntity, new HexFeatureRegenerateRequest { ResetElevation = true });

            Debug.Log("[HexMap] 已请求完整重生成（高程重置 + 特征重跑）");
        }

        [MenuItem("Tools/HexMap/重新生成植被")]
        public static void RegenerateVegetation()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[HexMap] 需在 Play 模式执行");
                return;
            }

            var world = World.DefaultGameObjectInjectionWorld;
            var query = world.EntityManager.CreateEntityQuery(typeof(HexFeatureState));
            if (query.IsEmpty)
            {
                Debug.LogWarning("[HexMap] 特征系统未安装（HexMapFeatureSettings.enableFeatures 或 HexMapAuthoring.featureSettings 未接线）");
                return;
            }
            world.EntityManager.GetComponentData<HexFeatureState>(query.GetSingletonEntity()).VegetationDirty = true;
            Debug.Log("[HexMap] 已请求植被重散布");
        }

        // ── 确定性验证：连跑两次生成比对状态哈希 ───────────────────

        private static int _awaitSerial;
        private static uint _firstHash;
        private static bool _running;

        [MenuItem("Tools/HexMap/验证特征确定性")]
        public static void VerifyDeterminism()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[HexMap] 需在 Play 模式执行");
                return;
            }
            if (_running)
                return;

            _running = true;
            _awaitSerial = CurrentSerial() + 1;   // 等第一次重生成完成
            RequestRegenerate();
            EditorApplication.update += WaitForFirst;
            Debug.Log("[HexMap] 确定性验证：第 1/2 次生成中…");
        }

        private static void WaitForFirst()
        {
            if (CurrentSerial() < _awaitSerial)
                return;

            EditorApplication.update -= WaitForFirst;
            _firstHash = HashState();

            _awaitSerial = CurrentSerial() + 1;
            RequestRegenerate();
            EditorApplication.update += WaitForSecond;
            Debug.Log("[HexMap] 确定性验证：第 2/2 次生成中…");
        }

        private static void WaitForSecond()
        {
            if (CurrentSerial() < _awaitSerial)
                return;

            EditorApplication.update -= WaitForSecond;
            _running = false;

            uint second = HashState();
            if (_firstHash == second)
                Debug.Log($"[HexMap] 确定性验证通过：两次生成哈希相等 0x{_firstHash:X8}");
            else
                Debug.LogError($"[HexMap] 确定性验证失败：哈希不等 0x{_firstHash:X8} vs 0x{second:X8}");
        }

        private static void RequestRegenerate()
        {
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var configEntity = em.CreateEntityQuery(typeof(HexMapConfig)).GetSingletonEntity();
            if (em.HasComponent<HexFeatureRegenerateRequest>(configEntity))
                em.SetComponentData(configEntity, new HexFeatureRegenerateRequest { ResetElevation = false });
            else
                em.AddComponentData(configEntity, new HexFeatureRegenerateRequest { ResetElevation = false });
        }

        private static int CurrentSerial()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            var query = world.EntityManager.CreateEntityQuery(typeof(HexFeatureState));
            return query.IsEmpty ? -1
                : world.EntityManager.GetComponentData<HexFeatureState>(query.GetSingletonEntity()).GenerationSerial;
        }

        /// <summary>状态哈希：路径 cell 序列 + 水面 + 高程覆写表拼接（确定性验证口径，计划 1.4）</summary>
        private static uint HashState()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            var query = world.EntityManager.CreateEntityQuery(typeof(HexFeatureState));
            if (query.IsEmpty)
                return 0;
            var state = world.EntityManager.GetComponentData<HexFeatureState>(query.GetSingletonEntity());

            unchecked
            {
                uint h = 0x9E3779B9u;
                foreach (var river in state.Rivers)
                {
                    h = h * 31u + (uint)river.RiverId;
                    h = h * 31u + (uint)river.End;
                    for (int i = 0; i < river.Cells.Count; i++)
                    {
                        h = h * 31u + (uint)river.Cells[i].x;
                        h = h * 31u + (uint)river.Cells[i].y;
                        h = h * 31u + (uint)math_round(river.WaterY[i] * 10f);
                    }
                }
                foreach (var lake in state.Lakes)
                {
                    h = h * 31u + (uint)lake.Level;
                    h = h * 31u + (uint)lake.Cells.Count;
                }
                foreach (var road in state.Roads)
                {
                    h = h * 31u + (uint)road.RoadId;
                    h = h * 31u + (uint)road.Cells.Count;
                }
                foreach (var kv in state.ElevationOverrides)
                {
                    h = h * 31u + (uint)kv.Key.x;
                    h = h * 31u + (uint)kv.Key.y;
                    h = h * 31u + (uint)kv.Value;
                }
                return h;
            }
        }

        private static uint math_round(float v)
        {
            // float→uint 稳定口径（远离 0 取整），避免平台浮点差异
            return (uint)Mathf.RoundToInt(v);
        }
    }
}
