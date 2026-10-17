using System.Collections.Generic;
using GameBoard;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>
    /// 教学介绍段语音联动地块显隐（2026-10-17 用户定案：介绍段=配音卡片+画面仅展示语音描述的区块）：
    /// BeginIntroReveal=开局全 tile 隐藏+英雄柱/手牌扇对账闸；Solo(token)=逐段独占显示（全隐藏后只亮
    /// token 区块——讲哪个亮哪个）；End=全恢复（操作预告段/第二段注入/切屏兜底）。
    /// 格子定位用 localPosition 反算坐标（CellCenterLocal 公式逆运算）——中线格被 BattleHudRow
    /// 认领改名后按名 Find 失效，位置法不受影响。ZoneProps 卡堆/漩涡与 HudRow 认领格都是 tile
    /// 本体或子物体，随 SetActive 同显隐。逻辑层零改动（TutorialGuide 只透传 reveal 串）；
    /// 非教学局零影响。
    /// </summary>
    public static class BattleRevealDirector
    {
        /// <summary>英雄柱对账闸（true=BattleFieldView.ReconcileHero 跳过——介绍到角色格前不建柱）。</summary>
        public static bool SuppressHeroes { get; private set; }

        /// <summary>手牌扇刷新闸（true=BattleScreen.RefreshFans 跳过——介绍到费用段前手牌区空）。</summary>
        public static bool SuppressHandFan { get; private set; }

        private static bool _active;
        private static readonly HashSet<long> _keep = new HashSet<long>();

        /// <summary>教学介绍段开始：全 tile 隐藏+两闸上锁（棋盘骨架/相机/日照正常）。</summary>
        public static void BeginIntroReveal()
        {
            _active = true;
            SuppressHeroes = true;
            SuppressHandFan = true;
            ApplyKeep();
        }

        /// <summary>逐段独占显示（2026-10-17 定案：仅展示语音描述的目标）：全隐藏后只亮 token 区块。
        /// token：board=棋盘总览但**右列三区不亮**（2026-10-17 晚定案：卡组/墓/除外讲到 zone:right
        /// 再显示——开场总览不亮卡组）；all=全显；zone:mid=中线信息条；zone:role=双方角色格；
        /// zone:activation=己方单位区 12 格（发动区）；zone:right=卡组/墓/除外三格；
        /// zone:p1-land=己方地牌行；hand=解手牌闸（调用方随后 RefreshFans）。
        /// 解闸类 token（zone:role/hand）不改变盘面保持。</summary>
        public static void Solo(string token)
        {
            if (!_active || string.IsNullOrEmpty(token)) return;
            switch (token)
            {
                case "board":
                {
                    _keep.Clear();
                    for (int x = 0; x < BoardLayout.Width; x++)
                        for (int z = 0; z < BoardLayout.Height; z++)
                            AddCell((x, z));
                    foreach (var c in RightColumnCells()) // 右列三区讲到 zone:right 再亮
                        _keep.Remove(((long)c.x << 32) | (uint)c.z);
                    ApplyKeep();
                    return;
                }
                case "all":
                    _keep.Clear(); // 空=全显
                    ApplyKeep();
                    return;
                case "hand":
                    if (SuppressHandFan)
                    {
                        SuppressHandFan = false;
                    }
                    return; // 手牌扇非 tile：解闸即可，盘面保持不变
                case "zone:role":
                    SuppressHeroes = false; // 解除英雄柱闸（调用方随后 RefreshField 建柱）
                    _keep.Clear();
                    AddCell(BoardLayout.CharacterCell(0));
                    AddCell(BoardLayout.CharacterCell(1));
                    ApplyKeep();
                    return;
                case "zone:activation":
                    _keep.Clear();
                    AddCells(BoardLayout.UnitCells(0)); // 己方单位区两行 12 格=发动区
                    ApplyKeep();
                    return;
                case "zone:right":
                    _keep.Clear();
                    foreach (var c in RightColumnCells()) AddCell(c);
                    ApplyKeep();
                    return;
                case "zone:mid":
                    _keep.Clear();
                    for (int x = 0; x < BoardLayout.Width; x++) AddCell((x, BoardLayout.MiddleZ));
                    ApplyKeep();
                    return;
                case "zone:p1-land":
                    _keep.Clear();
                    AddCells(BoardLayout.LandCells(0));
                    ApplyKeep();
                    return;
                default:
                    Debug.Log($"[BattleRevealDirector] 未认识的演出指令：{token}（跳过）");
                    return;
            }
        }

        /// <summary>全恢复（操作预告段/第二段注入/引导退场/切屏兜底）：tile 全激活+闸全开。</summary>
        public static void End()
        {
            _active = false;
            SuppressHeroes = false;
            SuppressHandFan = false;
            _keep.Clear();
            ApplyKeep();
        }

        // ======================================== 内部 ========================================

        private static void AddCell((int x, int z) c) => _keep.Add(((long)c.x << 32) | (uint)c.z);

        private static void AddCells(IReadOnlyList<(int x, int z)> cells)
        {
            foreach (var c in cells) AddCell(c);
        }

        /// <summary>右列三区格（双方卡组/墓/除外共 6 格）——zone:right 独占显示与 board 总览排除共用。</summary>
        private static IEnumerable<(int x, int z)> RightColumnCells()
        {
            yield return (BoardLayout.P1DeckX, BoardLayout.P1DeckZ);   // 卡组
            yield return (BoardLayout.P1GraveX, BoardLayout.P1GraveZ); // 墓地
            yield return (BoardLayout.P1ExileX, BoardLayout.P1ExileZ); // 除外
            yield return BoardLayout.Rotate180(BoardLayout.P1DeckX, BoardLayout.P1DeckZ);
            yield return BoardLayout.Rotate180(BoardLayout.P1GraveX, BoardLayout.P1GraveZ);
            yield return BoardLayout.Rotate180(BoardLayout.P1ExileX, BoardLayout.P1ExileZ);
        }

        /// <summary>按当前 _keep 应用显隐（空=全显）。tile 坐标用 localPosition 反算（改名免疫）。</summary>
        private static void ApplyKeep()
        {
            var root = BattleBoardGrid.Root;
            if (root == null) return;
            bool all = _keep.Count == 0;
            foreach (Transform tile in root)
            {
                if (!CoordOf(tile, out int x, out int z)) continue; // 非 tile 子物体（罕见）跳过
                bool show = all || _keep.Contains(((long)x << 32) | (uint)z);
                if (tile.gameObject.activeSelf != show)
                    tile.gameObject.SetActive(show);
            }
        }

        /// <summary>tile localPosition → 格坐标（CellCenterLocal 逆运算：
        /// z=round(local.z/1.5)、x=round(local.x/(2·InnerRadius)−(z&amp;1)·0.5)）。奇偶行半格偏移在
        /// round 容差内稳定还原。</summary>
        private static bool CoordOf(Transform tile, out int x, out int z)
        {
            x = z = -1;
            var lp = tile.localPosition;
            z = Mathf.RoundToInt(lp.z / (BattleBoardGrid.OuterRadius * 1.5f));
            float xr = lp.x / (BattleBoardGrid.InnerRadius * 2f) - (z & 1) * 0.5f;
            x = Mathf.RoundToInt(xr);
            if (Mathf.Abs(xr - x) > 0.25f) return false; // 非格心物体（防误判）
            if (x < 0 || x >= BoardLayout.Width || z < 0 || z >= BoardLayout.Height) return false;
            return true;
        }
    }
}
