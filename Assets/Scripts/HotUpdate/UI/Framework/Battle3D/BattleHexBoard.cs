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
    /// 2026-10-13 晚 材质资产化改版（用户定案）：格子=Assets/Art/Battle3D/BattleHex.prefab
    /// 预制体实例（自带 mesh 引用 + 材质资产引用 + BoxCollider）——运行时 new Material 配色
    /// 整链退役（材质实例会被 Unity 在域边界销毁而静态字典跨边界残留 fake-null，重开工程后
    /// 全盘粉红；资产材质引用永生）。分区配色（BattleBoardZones 15 区）待材质资产化方案回归。
    ///
    /// 规格=全盘 9×6（2026-10-13 缩盘定案二段）：BoardLayout 全盘 54 格——双方各 7×3
    /// 可用带 + 两侧列特殊格（发动/卡组/墓地/除外/角色）与 2 空格全部入盘拼装，角色柱不再悬空。
    /// BoardLayout 常量重映射后拼装自动跟随（遍历全盘常量而非硬编码坐标）。
    ///
    /// 通路：BattleStageDirector 装台③ → Build() → BattleBoardGrid.Mount；
    /// 格心公式单一事实源=BattleBoardGrid.CellCenterLocal。
    /// </summary>
    public static class BattleHexBoard
    {
        private const string TilePrefabAddress = "BattleHex";
        private const string TilePrefabPath = "Assets/Art/Battle3D/BattleHex.prefab";

        // 分区材质（2026-10-13 配色恢复·资产化）：Materials/BattleTile_Zone{NN}_{段}.mat
        //（一次性生成落盘，Inspector 调色归用户）。资产引用跨域边界永生；fake-null 防御同 ZoneMaterial 语义。
        private static readonly Dictionary<byte, Material> _zoneMats = new Dictionary<byte, Material>();

        private static Material ZoneMaterial(byte zone)
        {
            if (_zoneMats.TryGetValue(zone, out var mat) && mat != null) return mat; // fake-null 判死重载
            mat = HotUpdateAssets.Load<Material>(
                "BattleTile_Zone" + zone.ToString("00"),
                $"Assets/Art/Battle3D/Materials/BattleTile_Zone{zone:00}_{BattleBoardZones.ZoneFileLabel(zone)}.mat");
            if (mat != null) _zoneMats[zone] = mat; // 未生成不入缓存（生成后下次装载即命中）
            return mat; // null=资产未生成：回落预制体自带材质
        }

        /// <summary>拼装全盘棋盘（每格一枚 BattleHex 预制体实例，含两侧特殊格与空格）。
        /// 预制体缺失返回 null（装台降级，不阻塞日出）；分区材质资产缺失回落预制体自带材质。</summary>
        public static GameObject Build()
        {
            var prefab = HotUpdateAssets.Load<GameObject>(TilePrefabAddress, TilePrefabPath);
            if (prefab == null)
            {
                Debug.LogError("[BattleHexBoard] BattleHex.prefab 缺失（" + TilePrefabPath
                               + "）——棋盘拼装降级，不阻塞日出");
                return null;
            }

            var root = new GameObject("BattleBoardRoot");
            int count = 0, zoned = 0;
            for (int z = 0; z < BoardLayout.Height; z++)
                for (int x = 0; x < BoardLayout.Width; x++)
                {
                    var tile = Object.Instantiate(prefab, root.transform, false);
                    tile.name = $"tile-{x}-{z}";
                    tile.transform.localPosition = BattleBoardGrid.CellCenterLocal(x, z);
                    var zoneMat = ZoneMaterial(BattleBoardZones.ZoneOf(BoardLayout.RoleOf(x, z), BoardLayout.OwnerOf(x, z)));
                    if (zoneMat != null)
                    {
                        tile.GetComponent<MeshRenderer>().sharedMaterial = zoneMat;
                        zoned++;
                    }
                    count++;
                }

            Debug.Log($"[BattleHexBoard] 拼装 9×6：{count} 格（BattleHex.prefab 实例，分区配色 {zoned}/{count}"
                      + "，未配格=预制体自带材质）");
            return root;
        }
    }
}
