using System.Collections.Generic;
using GameBoard;
using Tide.HotUpdate;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>
    /// 六边形棋盘运行时拼装器（2026-10-13 战场重做二段定案：BattleHexMeshBuilder 只产一枚
    /// 六边形 mesh 资产，摆位归运行时装配——整盘烘焙路线两硬伤已弃：多 subMesh 落盘丢区、
    /// 绕序反了俯视被剔）。
    ///
    /// 2026-10-13 晚 材质资产化改版（用户定案）：格子=Assets/Art/Prefab/BattleHex.prefab
    /// 预制体实例（自带 mesh 引用 + 材质资产引用 + BoxCollider）——运行时 new Material 配色
    /// 整链退役（材质实例会被 Unity 在域边界销毁而静态字典跨边界残留 fake-null，重开工程后
    /// 全盘粉红；资产材质引用永生）。分区配色（BattleBoardZones 15 区）待材质资产化方案回归。
    ///
    /// 规格=全盘 7×7 盘形 6765676（2026-10-29 P11 盘面重塑→当晚对座镜像修订）：BoardLayout
    /// 全盘实拼 43 tile（每半场行格数 6/7/6——六格行同列带 x1..x6 + 中线 5 格居中；
    /// 缺口 6=x0 列四格（z0/z2/z4/z6 行首）+ 中线两端 (0,3)/(6,3)）——双方可用带 + 中线行 +
    /// 两侧特殊格（角色/场地/墓地/除外/卡组/万能）全部入盘拼装，角色柱不再悬空。
    /// BoardLayout 常量重映射后拼装自动跟随（遍历全盘常量而非硬编码坐标）。
    /// 2026-10-29 BattleStage.prefab 已重烘焙对齐（43 格·对座镜像盘形，场地格 Zone25/26 黑色）；
    /// 再改盘形须同步重烘焙（编辑器侧反射 BoardLayout 单源拼装，勿手摆格子）。
    ///
    /// 通路：BattleStageDirector 装台③ → Build() → BattleBoardGrid.Mount；
    /// 格心公式单一事实源=BattleBoardGrid.CellCenterLocal。
    /// </summary>
    public static class BattleHexBoard
    {
        private const string TilePrefabAddress = "BattleHex";
        private const string TilePrefabPath = "Assets/Art/Prefab/BattleHex.prefab";

        // v3 预制体化（2026-10-23 13:00）：战场=可编辑资产 BattleStage.prefab
        //（Board 50 格+Env+Stairs 预烘焙——盘形随 BoardLayout 定案手工维护/编辑器内改）
        private const string StagePrefabAddress = "BattleStage";
        private const string StagePrefabPath = "Assets/Art/Battle3D/Prefab/BattleStage.prefab";

        // 分区材质（2026-10-13 配色恢复·资产化）：Materials/BattleTile_Zone{NN}_{段}.mat
        //（一次性生成落盘，Inspector 调色归用户）。资产引用跨域边界永生；fake-null 防御同 ZoneMaterial 语义。
        private static readonly Dictionary<byte, Material> _zoneMats = new Dictionary<byte, Material>();

        private static Material ZoneMaterial(byte zone)
        {
            if (_zoneMats.TryGetValue(zone, out var mat) && mat != null) return mat; // fake-null 判死重载
            mat = HotUpdateAssets.Load<Material>(
                "BattleTile_Zone" + zone.ToString("00"),
                $"Assets/Art/Materials/BattleTile_Zone{zone:00}_{BattleBoardZones.ZoneFileLabel(zone)}.mat");
            if (mat != null) _zoneMats[zone] = mat; // 未生成不入缓存（生成后下次装载即命中）
            return mat; // null=资产未生成：回落预制体自带材质
        }

        /// <summary>拼装战场（v3 预制体化）：优先加载 BattleStage.prefab（美术可在 Prefab 里直接
        /// 拖动编辑任何件），缺失回落运行时拼装 BuildLegacy。返回=战场总根：prefab 态=BattleStage
        ///（Board/Env/Stairs 为其子节点，由 BattleStageDirector 认领）；回落态=棋盘根（导演层另行铺环境）。</summary>
        public static GameObject Build()
        {
            var prefab = HotUpdateAssets.Load<GameObject>(StagePrefabAddress, StagePrefabPath);
            if (prefab != null)
            {
                var stage = Object.Instantiate(prefab);
                stage.name = "BattleStage";
                // 环境风（2026-10-23 v4 §10.5）：单一驱动器挂战场根——植物摆动+篝火倾斜/横向风
                //（组件走运行时挂载：prefab 保持零脚本引用，热更程序集无序列化耦合）
                stage.AddComponent<BattleEnvWind>();
                var board = stage.transform.Find("Board");
                Debug.Log($"[BattleHexBoard] 战场预制体装台：{StagePrefabPath}" +
                          $"（Board格={board?.childCount ?? 0} + Env/Stairs 预烘焙 + BattleEnvWind）");
                return stage;
            }
            Debug.LogWarning("[BattleHexBoard] BattleStage.prefab 缺失——回落运行时拼装（生成菜单 Tools/战场/生成战场预制体；P3 打包前须生成）");
            return BuildLegacy();
        }

        /// <summary>运行时逐格拼装（生成器/回落路径·2026-10-23 v3 前的 Build 原体）：
        /// 每格一枚 BattleHex 预制体实例（tile-x-z 命名+缩格+同名拾取代理），分区配色资产引用。
        /// 预制体缺失返回 null（装台降级，不阻塞日出）。</summary>
        public static GameObject BuildLegacy()
        {
            var prefab = HotUpdateAssets.Load<GameObject>(TilePrefabAddress, TilePrefabPath);
            if (prefab == null)
            {
                Debug.LogError("[BattleHexBoard] BattleHex.prefab 缺失（" + TilePrefabPath
                               + "）——棋盘拼装降级，不阻塞日出");
                return null;
            }

            var root = new GameObject("BattleBoardRoot");
            // 2026-10-30 格距×1.1：绕盘心等比扩、世界盘心不动（与 BattleStage.prefab 烘焙口径一致）——
            // 新盘面 x 0..12.383 中心 6.192，root 0.187 复位旧盘心 x=6.379；z 0..9.9 中心 4.95，root −1.2 复位 3.75。
            root.transform.position = new Vector3(0.1871f, 0f, -1.2f);
            int count = 0, zoned = 0;
            for (int z = 0; z < BoardLayout.Height; z++)
                for (int x = 0; x < BoardLayout.Width; x++)
                {
                    if (BoardLayout.NoTileAt(x, z)) continue; // 缺口（六格行尾格 + 中线两端）不拼格
                    var tile = Object.Instantiate(prefab, root.transform, false);
                    tile.name = $"tile-{x}-{z}";
                    tile.transform.localPosition = BattleBoardGrid.CellCenterLocal(x, z);
                    // P0 缩格（2026-10-23 战场美化）：格心不变，视觉缩 0.82 露缝。
                    // 点选补偿（红线：拾取=单发 Physics.Raycast 认 tile- 名）：预制体根实况=六边形
                    // MeshCollider（非 BoxCollider——随根缩会缩水 18%，BoxCollider.size 补偿不适用）。
                    // 方案=同名子代理缩放 CellFillScale（铺满整格到格界：2026-10-30 连成一片定案，
                    // 点击无死区；相邻格代理边贴边共格界，无重叠歧义）；
                    // 根 MeshCollider 销毁防双份。代理与格同名 tile-{x}-{z}，射线名字路由无感。
                    // 中线功能行（z3）例外：不加代理不毁根碰撞体——BattleHudRow 认领改名 hud-tile-*
                    // 后仍靠根碰撞体路由点击（结束回合/悬停），其面积随视觉缩属预期。
                    tile.transform.localScale = Vector3.one * BattleBoardGrid.TileScale;
                    if (z != BoardLayout.MiddleZ)
                    {
                        var mf = tile.GetComponent<MeshFilter>();
                        var rootCol = tile.GetComponent<Collider>();
                        if (mf != null && mf.sharedMesh != null && rootCol is MeshCollider)
                        {
                            var pick = new GameObject(tile.name); // 与格同名：名字路由零改动
                            pick.transform.SetParent(tile.transform, false);
                            pick.transform.localPosition = Vector3.zero;
                            pick.transform.localScale = Vector3.one * BattleBoardGrid.CellFillScale;
                            var mc = pick.AddComponent<MeshCollider>();
                            mc.sharedMesh = mf.sharedMesh;
                            mc.convex = ((MeshCollider)rootCol).convex;
                            Object.Destroy(rootCol);
                        }
                    }
                    var zoneMat = ZoneMaterial(BattleBoardZones.ZoneOf(BoardLayout.RoleOf(x, z), BoardLayout.OwnerOf(x, z)));
                    if (zoneMat != null)
                    {
                        tile.GetComponent<MeshRenderer>().sharedMaterial = zoneMat;
                        zoned++;
                    }
                    count++;
                }

            Debug.Log($"[BattleHexBoard] 拼装 {BoardLayout.Width}×{BoardLayout.Height}（盘形6765676·缺口6）：{count} 格"
                      + $"（BattleHex.prefab 实例，分区配色 {zoned}/{count}"
                      + "，未配格=预制体自带材质；root (+0.75,-0.75) 居中）");
            return root;
        }
    }
}
