using System.Collections.Generic;
using System.IO;
using SynergyUI;
using UnityEditor;
using UnityEngine;

namespace Tide.验证
{
    /// <summary>
    /// 战场预制体烘焙器（2026-10-23 13:00 v3·B 主任务）：Play 内按运行时拼装结果
    /// （BattleHexBoard.BuildLegacy + BattleArenaStage.Mount）烘成
    /// Assets/Art/Battle3D/Prefab/BattleStage.prefab，层级（v3 口径）：
    ///   BattleStage/Board/tile-x-z ×55（位置/缩放/同名拾取代理原样；分区材质=资产引用）
    ///   BattleStage/Env/...（地面件/崖壁/细节/法阵/兜底/台心暖点光；运行时材质实例随 prefab 存为子资产）
    ///   BattleStage/Stairs/...（近端台阶独立节点）
    /// 此后 BattleHexBoard.Build() 走预制体加载——美术在 Prefab 里拖动任何件即生效。
    /// 用法：进 Play（主菜单即可，勿在对局中）执行 Tools/战场/生成战场预制体；一次性工具，重跑覆盖。
    /// </summary>
    public static class BattleStagePrefabBaker
    {
        private const string MenuPath = "Tools/战场/生成战场预制体";
        private const string OutPath = "Assets/Art/Battle3D/Prefab/BattleStage.prefab";

        [MenuItem(MenuPath)]
        public static void Generate()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning($"[StageBaker] 需在 Play 模式（主菜单）执行：{MenuPath}");
                return;
            }
            if (BattleBoardGrid.Ready)
            {
                Debug.LogWarning("[StageBaker] 对局进行中（棋盘已装台）——请退回主菜单再烘，防串台");
                return;
            }

            var stage = new GameObject("BattleStage");
            int tiles = 0, envCount = 0, stairCount = 0, stripped = 0;
            try
            {
                // Board：运行时拼装（legacy）——tile-x-z 命名/缩格/同名拾取代理与运行时口径一致
                var board = BattleHexBoard.BuildLegacy();
                if (board == null) { Debug.LogError("[StageBaker] BuildLegacy 失败（BattleHex.prefab 缺失？）——中止"); return; }
                board.name = "Board";
                board.transform.SetParent(stage.transform, true);
                tiles = board.transform.childCount;

                // Env：legacy 地编铺装（材质实例/点光随 SaveAsPrefabAsset 存为 prefab 子资产）
                var bakeMap = new GameObject("Map-StageBake");
                BattleArenaStage.Mount(bakeMap.transform);
                var env = bakeMap.transform.Find("EnvRoot");
                if (env == null) { Debug.LogError("[StageBaker] EnvRoot 未建（Mount 失败）——中止"); return; }
                env.name = "Env";

                // Stairs：从 Env 拆独立节点（v3 层级口径）
                var stairs = new GameObject("Stairs").transform;
                stairs.SetParent(stage.transform, false);
                for (int i = env.childCount - 1; i >= 0; i--)
                {
                    var c = env.GetChild(i);
                    if (c.name.StartsWith("stairs-")) c.SetParent(stairs, true);
                }
                stairCount = stairs.childCount;
                env.SetParent(stage.transform, true);
                envCount = env.childCount;

                // 碰撞体即剥（Mount/BuildLegacy 内 Destroy 是帧末延迟——烘焙须当场清）：
                // Env/Stairs 全剥（红线②零碰撞）；Board 只剥格根（BuildLegacy 已 Destroy 根 MeshCollider，
                // 此处 DestroyImmediate 补刀），保留同名拾取代理子 MeshCollider 与隐藏动作键 BoxCollider（原行为）
                foreach (var col in env.GetComponentsInChildren<Collider>(true)) { Object.DestroyImmediate(col); stripped++; }
                foreach (var col in stairs.GetComponentsInChildren<Collider>(true)) { Object.DestroyImmediate(col); stripped++; }
                foreach (Transform tile in board.transform)
                {
                    var rootCols = tile.GetComponents<Collider>();
                    for (int i = 0; i < rootCols.Length; i++) { Object.DestroyImmediate(rootCols[i]); stripped++; }
                }

                // 运行时材质实例（压暗克隆/法阵白金/兜底近黑）不会被 SaveAsPrefabAsset 序列化，
                // 引用会写成 null——首烘 112 渲染器材质全空的根因。烘前固化成 Materials/ 下
                // 真 .mat 资产（按路径复用保 GUID 稳定），渲染器换绑资产后再存。
                int bakedMats = PersistRuntimeMaterials(stage);

                Directory.CreateDirectory(Path.GetDirectoryName(OutPath));
                PrefabUtility.SaveAsPrefabAsset(stage, OutPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[StageBaker][PASS] 烘焙 {OutPath}：Board×{tiles} Env×{envCount} Stairs×{stairCount}" +
                          $" 剥碰撞体={stripped} 固化材质={bakedMats}（点光随 prefab；材质=Materials/ 资产引用；重跑覆盖）");
            }
            finally
            {
                BattleArenaStage.Unmount(); // 独立 EnvRoot 态：销毁 Env+材质实例（prefab 已存自己的一份）
                Object.DestroyImmediate(stage);
                var bakeMap = GameObject.Find("Map-StageBake");
                if (bakeMap != null) Object.DestroyImmediate(bakeMap);
            }
        }

        /// <summary>把树内运行时材质实例固化为 Materials/ 下 .mat 资产并换绑。
        /// 角色命名：法阵=StageCircle_+源名 / 兜底=StageBacking_Dark / 压暗=StageDim_+源名；
        /// 同键多实例（法阵逐渲染器克隆）共用一份资产。已是资产的材质（tile 分区/按钮）不动。</summary>
        private static int PersistRuntimeMaterials(GameObject stage)
        {
            const string matDir = "Assets/Art/Battle3D/Environment/Materials/";
            Directory.CreateDirectory(matDir);
            var assets = new Dictionary<string, Material>();
            foreach (var r in stage.GetComponentsInChildren<MeshRenderer>(true))
            {
                var m = r.sharedMaterial;
                if (m == null || AssetDatabase.Contains(m)) continue;

                string srcName = m.name.Replace(" (Clone)", "").Replace(" (Instance)", "").Trim();
                string stem = IsUnder(r.transform, "arena-circle") ? "StageCircle_" + srcName
                    : r.name == "arena-backing" ? "StageBacking_Dark"
                    : "StageDim_" + srcName;
                if (!assets.TryGetValue(stem, out var asset))
                {
                    string path = matDir + stem + ".mat";
                    asset = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (asset == null) AssetDatabase.CreateAsset(asset = new Material(m), path);
                    else asset.CopyPropertiesFromMaterial(m); // 重跑：保 GUID，覆写属性
                    asset.name = stem;
                    EditorUtility.SetDirty(asset);
                    assets[stem] = asset;
                }
                r.sharedMaterial = asset;
            }
            return assets.Count;
        }

        private static bool IsUnder(Transform t, string name)
        {
            for (var p = t; p != null; p = p.parent)
                if (p.name == name) return true;
            return false;
        }
    }
}
