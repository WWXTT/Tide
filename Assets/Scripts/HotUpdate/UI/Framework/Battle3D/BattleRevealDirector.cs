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

        /// <summary>对手英雄柱隐藏（2026-10-18 教学一修订：讲角色/右侧区只显自己）：对手柱**创建流程
        /// 不动**，由 BattleFieldView 建柱后按本标记 SetActive 隐藏；Solo("all")（操作预告段）/End 解除。</summary>
        public static bool HideOpponentHero { get; private set; }

        /// <summary>己方英雄柱隐藏（2026-11-09 教学一修订：basics_03 讲完角色格后，后续独占段
        ///（发动区/右列/元素池/场地）须同步藏柱——柱非 tile，_keep 不辖）：创建流程不动，由
        /// ReconcileHero 每次对账按本标记 SetActive（与 HideOpponentHero 同口）；独占段 token 置位
        ///（调用方随后 _refreshField 消费），zone:role/all/End 解除。</summary>
        public static bool HideSelfHero { get; private set; }

        /// <summary>单位出场闸（2026-10-19 教学二段一「生物出场」）：true=BattleFieldView 对账不出场
        /// 单位（存量全清——重置回介绍段也走此口径）；Solo("units")/End 解除，随后 RefreshField
        /// 建单位（入场演出随建触发）。</summary>
        public static bool SuppressUnits { get; private set; }

        /// <summary>特殊格道具闸（2026-11-21 入场拆层）：true=卡组卡堆+墓地/除外漩涡隐藏
        ///（BattleZoneProps.SetAllVisible）；Solo("props")/all/End 解除。**默认不锁**——只有
        /// scene 令牌进入拆层模式才锁（旧档零变化）。</summary>
        public static bool SuppressZoneProps { get; private set; }

        /// <summary>地牌行闸（2026-11-21 入场拆层）：true=BattleFieldView.ReconcileLands 跳过
        ///（图标/计数/glow 全熄——地牌行整体不浮现）；Solo("lands")/all/End 解除，随后对账重建。
        /// **默认不锁**（同上）。</summary>
        public static bool SuppressLands { get; private set; }

        /// <summary>优先权光条闸（2026-11-21 入场拆层）：true=fx-priority-bars 整体藏
        ///（BattlePriorityGlow.SetVisible）；介绍段无优先权语义——all/End 解除。**默认不锁**。</summary>
        public static bool SuppressPriorityGlow { get; private set; }

        private static bool _active;
        private static readonly HashSet<long> _keep = new HashSet<long>();
        // 2026-11-21 教学一改版（basics_01 前只显示场景）：scene 全藏态——_keep 语义不变（空=全显），
        // _hideAll=true 时一律藏。board/End 解除。
        private static bool _hideAll;
        // Solo 早于棋盘装配（教学开局竞速）时挂起，PumpBoardReady 在棋盘就绪后补拍（时序前提：
        // 否则 scene 全藏应用在装配前空转，tile 装出来是亮的）。
        private static bool _pendingApply;

        /// <summary>教学介绍段开始：全 tile 隐藏+闸上锁（棋盘骨架/相机/日照正常）。</summary>
        public static void BeginIntroReveal()
        {
            _active = true;
            SuppressHeroes = true;
            SuppressHandFan = true;
            HideOpponentHero = true;
            HideSelfHero = true;
            SuppressUnits = true;
            _hideAll = false;
            ApplyKeep();
        }

        /// <summary>棋盘就绪泵（BattleScreen 更新器逐帧调）：Solo 早于棋盘装配时挂起的应用
        /// 在 BattleBoardGrid.Ready 后补拍——教学一 basics_01 的 scene 全藏依赖此时序。
        /// 道具闸同步补拍（ZoneProps 随装台新建，须按闸态收敛显隐）。</summary>
        public static void PumpBoardReady()
        {
            if (!_active || !_pendingApply || !BattleBoardGrid.Ready) return;
            _pendingApply = false;
            ApplyKeep();
            BattleZoneProps.SetAllVisible(!SuppressZoneProps); // 装台新建的卡堆/漩涡按闸收敛
        }

        /// <summary>逐段独占显示（2026-10-17 定案：仅展示语音描述的目标）：全隐藏后只亮 token 区块。
        /// token（2026-10-18 教学一修订：对手读到操作预告段才显露）：board=整块棋盘总览**含双方
        /// 卡组/墓/除外右列**（总览不藏区；2026-11-21 入场拆层后 board 语义收窄=**裸格子**——
        /// 卡组堆/漩涡/地牌行各有独立闸，见 props/lands）；all=全显+解除对手柱隐藏+解锁全部拆层闸；
        /// zone:mid=中线信息条；zone:role=仅己方角色格（对手柱照常建、建后按 HideOpponentHero 隐藏）；
        /// zone:activation=己方单位区 12 格（发动区）；zone:right=仅己方卡组/墓/除外三格；
        /// zone:p1-land=己方地牌行**含行首彩元素格**（2026-11-09：AnyElementCell 随元素池亮——讲彩元素）；
        /// zone:p1-field=仅己方场地格（2026-11-09 basics_07 讲场地）；hand=解手牌闸（调用方随后
        /// RefreshFans）；units=解单位闸（2026-10-19 教学二「生物出场」——调用方随后 RefreshField 建单位）。
        /// 解闸类 token（zone:role/hand/units）不改变盘面保持。
        /// 独占段 token（activation/right/mid/p1-land/p1-field·2026-11-09）不含角色格——置
        /// HideSelfHero 藏己方柱（柱非 tile，调用方随后 _refreshField 对账消费标记）。
        /// 2026-11-21 教学一改版新token：scene=**进入入场拆层模式**（全格隐藏+锁特殊格道具+锁地牌行
        ///——basics_01 前只显示场景装饰；盘面/道具/地牌自此可按步逐层放行，后续表现替换逐层可换）；
        /// board=裸格子浮现（不动 props/lands 闸）；heroes=仅解柱闸（basics_02 确定后角色浮现：
        /// 己方柱显、对手柱照旧藏到 all 段）；props=特殊格道具浮现（卡组堆/墓/漩涡）；lands=地牌行
        /// 浮现（图标/计数/glow 随对账重建）。</summary>
        public static void Solo(string token)
        {
            if (!_active || string.IsNullOrEmpty(token)) return;
            // scene=进入全藏态；heroes/props/lands=只动各自闸（盘面保持）——其余 token 一律解除全藏
            if (token != "scene" && token != "heroes" && token != "props" && token != "lands") _hideAll = false;
            switch (token)
            {
                case "scene":
                    _hideAll = true;
                    SuppressZoneProps = true; // 入场拆层：道具/地牌/优先权光条随 scene 上锁（board 不再联动）
                    SuppressLands = true;
                    SuppressPriorityGlow = true;
                    ApplyKeep();
                    BattleZoneProps.SetAllVisible(false);
                    BattlePriorityGlow.SetVisible(false);
                    return;
                case "heroes":
                    // 只解柱闸（调用方随后 _refreshField 建柱）；不动 _keep/_hideAll/单位/手牌
                    SuppressHeroes = false;
                    HideSelfHero = false;
                    return;
                case "props":
                    // 特殊格道具浮现（卡组堆/墓/漩涡）：即刻生效（不依赖对账）
                    SuppressZoneProps = false;
                    BattleZoneProps.SetAllVisible(true);
                    return;
                case "lands":
                    // 地牌行浮现：解闸即回（调用方随后 _refreshField 对账重建图标/计数/glow）
                    SuppressLands = false;
                    return;
                case "board":
                {
                    _keep.Clear();
                    for (int x = 0; x < BoardLayout.Width; x++)
                        for (int z = 0; z < BoardLayout.Height; z++)
                            AddCell((x, z));
                    ApplyKeep();
                    return;
                }
                case "all":
                    HideOpponentHero = false; // 操作预告段：对手到此才显露（调用方随后 RefreshField 显柱）
                    HideSelfHero = false;
                    _keep.Clear(); // 空=全显
                    SuppressZoneProps = false; // 拆层闸全解（2026-11-21）：老档 all 语义=完整盘面
                    SuppressLands = false;
                    SuppressPriorityGlow = false;
                    BattleZoneProps.SetAllVisible(true);
                    BattlePriorityGlow.SetVisible(true);
                    ApplyKeep();
                    return;
                case "hand":
                    if (SuppressHandFan)
                    {
                        SuppressHandFan = false;
                    }
                    return; // 手牌扇非 tile：解闸即可，盘面保持不变
                case "units":
                    SuppressUnits = false; // 解单位闸（调用方随后 RefreshField 建单位=生物出场）
                    return; // 单位模型非 tile：解闸即可，盘面保持不变
                case "zone:role":
                    SuppressHeroes = false; // 解除英雄柱闸（调用方随后 RefreshField 建柱——对手柱建后隐藏）
                    HideSelfHero = false;
                    _keep.Clear();
                    AddCell(BoardLayout.CharacterCell(0)); // 仅己方角色格
                    ApplyKeep();
                    return;
                case "zone:activation":
                    HideSelfHero = true; // 独占段不含角色格：藏柱（对账随调用方 _refreshField）
                    _keep.Clear();
                    AddCells(BoardLayout.UnitCells(0)); // 己方单位区两行 12 格=发动区
                    ApplyKeep();
                    return;
                case "zone:right":
                    HideSelfHero = true;
                    _keep.Clear();
                    AddCell((BoardLayout.P1DeckX, BoardLayout.P1DeckZ));   // 卡组
                    AddCell((BoardLayout.P1GraveX, BoardLayout.P1GraveZ)); // 墓地
                    AddCell((BoardLayout.P1ExileX, BoardLayout.P1ExileZ)); // 除外
                    ApplyKeep();
                    return;
                case "zone:mid":
                    HideSelfHero = true;
                    _keep.Clear();
                    for (int x = 0; x < BoardLayout.Width; x++) AddCell((x, BoardLayout.MiddleZ));
                    ApplyKeep();
                    return;
                case "zone:p1-land":
                    HideSelfHero = true;
                    _keep.Clear();
                    AddCells(BoardLayout.LandCells(0));
                    AddCell(BoardLayout.AnyElementCell(0)); // 行首彩元素格（2026-11-09）：随元素池亮
                    ApplyKeep();
                    return;
                case "zone:p1-field":
                    HideSelfHero = true;
                    _keep.Clear();
                    AddCell(BoardLayout.FieldCell(0)); // 仅己方场地格（2026-11-09 basics_07）
                    ApplyKeep();
                    return;
                default:
                    Debug.Log($"[BattleRevealDirector] 未认识的演出指令：{token}（跳过）");
                    return;
            }
        }

        /// <summary>全恢复（操作预告段/第二段注入/引导退场/切屏兜底）：tile 全激活+闸全开
        ///（含拆层闸 props/lands——全恢复语义=完整盘面）。</summary>
        public static void End()
        {
            _active = false;
            SuppressHeroes = false;
            SuppressHandFan = false;
            HideOpponentHero = false;
            HideSelfHero = false;
            SuppressUnits = false;
            SuppressZoneProps = false;
            SuppressLands = false;
            SuppressPriorityGlow = false;
            _keep.Clear();
            _hideAll = false;
            _pendingApply = false;
            BattleZoneProps.SetAllVisible(true);
            BattlePriorityGlow.SetVisible(true);
            ApplyKeep();
        }

        // ======================================== 内部 ========================================

        private static void AddCell((int x, int z) c) => _keep.Add(((long)c.x << 32) | (uint)c.z);

        private static void AddCells(IReadOnlyList<(int x, int z)> cells)
        {
            foreach (var c in cells) AddCell(c);
        }

        /// <summary>按当前 _keep 应用显隐（空=全显；_hideAll=全藏）。棋盘未装配时挂起
        ///（_pendingApply），PumpBoardReady 就绪补拍。tile 坐标用 localPosition 反算（改名免疫）。</summary>
        private static void ApplyKeep()
        {
            var root = BattleBoardGrid.Root;
            if (root == null)
            {
                _pendingApply = true; // 教学开局竞速：Solo 早于棋盘装配——就绪后补拍
                return;
            }
            _pendingApply = false;
            bool all = !_hideAll && _keep.Count == 0;
            foreach (Transform tile in root)
            {
                if (!CoordOf(tile, out int x, out int z)) continue; // 非 tile 子物体（罕见）跳过
                bool show = !_hideAll && (all || _keep.Contains(((long)x << 32) | (uint)z));
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
