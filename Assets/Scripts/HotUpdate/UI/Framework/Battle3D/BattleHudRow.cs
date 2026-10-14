using System.Collections.Generic;
using Tide.HotUpdate;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>
    /// 功能地块行（2026-10-13 基础教学三问题·用户定案：HUD 按钮上盘——返回主菜单/结束回合等
    /// 常驻操作落成棋盘两半场接缝上的一行 3D 地块；无文字标签，身份靠悬停信息+配色）。
    ///
    /// 纯表现层：独立根 <see cref="BattleHudRowRoot"/>（不挂 BattleBoardGrid.Root 下——
    /// SelfCheck 的 54 格断言、BattleBoardAssemble 浮起动画、BoardMountSmoke 坐标对拍零耦合），
    /// 引擎 BoardLayout/BoardState 不感知本行。地块名 hud-tile-{key}（前缀区别于 tile-{x}-{z}
    /// 棋盘格），点击/悬停由 BattleScreen.PollFieldPointer 射线统一路由。
    ///
    /// 材质：实例化现有分区材质资产后着色（不新增资产；材质实例随 Unmount 显式销毁，
    /// 不进静态长持——域边界 fake-null 教训同 BattleHexBoard 注释）。
    /// </summary>
    public static class BattleHudRow
    {
        private const string TilePrefabAddress = "BattleHex";
        private const string TilePrefabPath = "Assets/Art/Battle3D/BattleHex.prefab";
        private const string BaseMatAddress = "BattleTile_Zone16";
        private const string BaseMatPath = "Assets/Art/Battle3D/Materials/BattleTile_Zone16_P1Temp.mat";

        // 接缝几何：z=2.5 行位（行 2/3 中心的连线上），奇列对齐（+0.5 格）；
        // 抬高到棋盘顶面之上悬浮——与相邻两行的六棱柱侧壁零穿插（几何解耦，纯摆位问题）
        private const float SeamZ = 3.75f;
        private const float LiftY = 0.32f;
        private const float TileScale = 0.85f;
        private const float BackCol = 2.75f;   // 悬浮列位（连续值，奇列对齐公式 x=(col+0.5)·短径·2）
        private const float EndTurnCol = 4.75f;

        private static readonly Color BackTint = new Color(0.52f, 0.56f, 0.64f);
        private static readonly Color EndTurnTint = new Color(0.42f, 0.66f, 0.46f);

        private static GameObject _root;
        private static readonly List<Material> _mats = new List<Material>();

        /// <summary>结束回合地块（null=未装台/缺件）。</summary>
        public static Transform EndTurnTile { get; private set; }

        /// <summary>返回主菜单地块（null=未装台/缺件）。</summary>
        public static Transform BackTile { get; private set; }

        /// <summary>装台幂等（BattleStageDirector.MountBoardAndDawnAsync 在 BattleZoneProps.Mount 后调）。</summary>
        public static void Mount()
        {
            if (_root != null) return;
            var prefab = HotUpdateAssets.Load<GameObject>(TilePrefabAddress, TilePrefabPath);
            if (prefab == null)
            {
                Debug.LogError("[BattleHudRow] BattleHex.prefab 缺失（" + TilePrefabPath + "）——功能地块行不装");
                return;
            }

            _root = new GameObject("BattleHudRowRoot");
            BackTile = BuildTile(prefab, "hud-tile-back", BackCol, BackTint);
            EndTurnTile = BuildTile(prefab, "hud-tile-end-turn", EndTurnCol, EndTurnTint);
            Debug.Log("[BattleHudRow][PASS] 功能地块行：hud-tile-back / hud-tile-end-turn @"
                      + $"z={SeamZ:0.00} 接缝（悬浮 y={LiftY:0.00}，缩放 {TileScale:0.00}）");
        }

        /// <summary>拆台（BattleStageDirector.DismountBoard 调）：销毁地块行与运行时材质实例。</summary>
        public static void Unmount()
        {
            if (_root == null) return;
            foreach (var m in _mats)
                if (m != null) Object.Destroy(m);
            _mats.Clear();
            Object.Destroy(_root);
            _root = null;
            BackTile = null;
            EndTurnTile = null;
        }

        private static Transform BuildTile(GameObject prefab, string name, float col, Color tint)
        {
            var tile = Object.Instantiate(prefab, _root.transform, false);
            tile.name = name;
            tile.transform.localPosition = new Vector3(
                (col + 0.5f) * BattleBoardGrid.InnerRadius * 2f, LiftY, SeamZ);
            tile.transform.localScale = Vector3.one * TileScale;

            // 着色：分区材质资产实例化后 tint（资产缺失回落预制体自带材质实例化）
            var rend = tile.GetComponent<MeshRenderer>();
            var src = HotUpdateAssets.Load<Material>(BaseMatAddress, BaseMatPath);
            var mat = Object.Instantiate(src != null ? src : rend.sharedMaterial);
            mat.color = tint;
            rend.sharedMaterial = mat;
            _mats.Add(mat);
            return tile.transform;
        }
    }
}
