using GameBoard;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>
    /// 对战棋盘分区编码（2026-10-13 战场重做）：CellRole×Owner → 分区 id（0..15）。
    /// 通路：BoardLayout → BattleHexBoard（运行时按 ZoneOf 取材质色逐格装配）→
    /// BattleStageDirector 装台 Mount。分区 id 语义在本类声明（单一事实源）；
    /// 配色=DefaultColor 运行时建 URP Lit 材质（后续材质资产化后归 Inspector 管）。
    /// </summary>
    public static class BattleBoardZones
    {
        /// <summary>盘外背景（无盘内格子，不产 subMesh；保留编号占位）。</summary>
        public const byte OuterBackdrop = 0;
        /// <summary>边缘环（未知角色回落；2026-10-15 发动格拆除后 (0,2)/(7,4) 两格回落本分区——
        /// 施放中的卡改在单位区空格落格呈现，固定发动格退役；原 Zone08/15 材质资产留盘不再消费）。</summary>
        public const byte EdgeRing = 1;

        // 己方 P1(owner0)：2..8；对方 P2(owner1)：9..15。槽序=Unit/Land/Deck/Grave/Exile/Char/Act
        //（Act 槽=8/15，2026-10-15 发动格拆除后无格映射，材质资产留盘不再消费、编号不回收）
        public const byte Owner0Base = 2;
        public const byte Owner1Base = 9;

        // 临时元素格（2026-10-13 临时元素上盘）：分区号只增不改（0..15 与已落盘材质资产
        // BattleTile_Zone{NN}_* 一一对应，插队会全体错位）。2026-10-14 临时元素格退役改技能格，
        // 16/17 材质资产留盘不再消费（编号不回收）。
        public const byte P1TempElement = 16;
        public const byte P2TempElement = 17;

        // 2026-10-14 战场四项改版追加（分区号只增不改）：18/19=技能格（原临时元素格位）、
        // 20=中线行（9×7 加行→当晚 8×7 状态行）、21/22=地牌上限格（当晚缩盘退役：
        // 地牌行整 6 格一元素一格无空位）。2026-10-16 技能格撤销：18/19 与 16/17、21/22
        // 同口径——材质资产留盘不再消费、编号不回收。
        public const byte P1Skill = 18;
        public const byte P2Skill = 19;
        public const byte MiddleRow = 20;
        public const byte P1LandCap = 21;
        public const byte P2LandCap = 22;

        // 2026-10-22 衍生物额外区追加（分区号只增不改）：23/24=额外区格（原技能格位回收——
        // 衍生物模板卡开局落 Zone.Extra，点击格子弹区域面板识别）。
        // 2026-10-29 P11：CellRole.Extra 随额外区格整删——23/24 改挂**万能展示格**（CellRole.Any·
        // 只做展示）：分区号/材质资产沿用不回收（FileLabel 仍 P1Extra/P2Extra——与已落盘
        // BattleTile_Zone23/24_*.mat 文件名对应），中文语义已随迁万能格。
        public const byte P1Extra = 23;
        public const byte P2Extra = 24;

        // 2026-10-29 P11 盘面重塑追加（分区号只增不改）：25/26=场地格（CellRole.Field 空角色位
        // ——规则归 P8；材质资产未生成，ZoneMaterial 回落预制体自带材质，生成后自动上色）。
        public const byte P1Field = 25;
        public const byte P2Field = 26;

        private static readonly string[] Labels =
        {
            "盘外背景", "边缘环",
            "己方单位区", "己方地牌行", "己方卡组", "己方墓地", "己方除外", "己方角色", "己方发动",
            "对方单位区", "对方地牌行", "对方卡组", "对方墓地", "对方除外", "对方角色", "对方发动",
            "己方临时元素", "对方临时元素",
            "己方技能格", "对方技能格", "中线行", "己方地牌上限", "对方地牌上限",
            "己方万能格", "对方万能格",
            "己方场地格", "对方场地格",
        };

        private static readonly string[] FileLabels =
        {
            "Backdrop", "Edge",
            "P1Unit", "P1Land", "P1Deck", "P1Grave", "P1Exile", "P1Char", "P1Act",
            "P2Unit", "P2Land", "P2Deck", "P2Grave", "P2Exile", "P2Char", "P2Act",
            "P1Temp", "P2Temp",
            "P1Skill", "P2Skill", "Middle", "P1LandCap", "P2LandCap",
            "P1Extra", "P2Extra",
            "P1Field", "P2Field",
        };

        // 生成器默认色板（材质资产落盘后由用户在 Inspector 调色，此处仅首建初值）
        // 2026-10-22 E1 玩家反馈「场地做成绿的/草地」：公共底盘三分区（Backdrop/Edge/Middle）
        // 换草绿系；双方单位/地牌等功能区保留敌我辨识色不动语义。
        // 2026-10-29 P11 追加：25/26 场地格首建初值（土棕系——区别于四色地牌行与草绿底盘）。
        private static readonly Color[] DefaultColors =
        {
            new Color(0.045f, 0.085f, 0.045f), new Color(0.13f, 0.24f, 0.10f),
            new Color(0.09f, 0.21f, 0.36f), new Color(0.07f, 0.27f, 0.17f),
            new Color(0.13f, 0.15f, 0.28f), new Color(0.11f, 0.09f, 0.19f),
            new Color(0.08f, 0.08f, 0.10f), new Color(0.09f, 0.25f, 0.28f),
            new Color(0.20f, 0.23f, 0.08f), new Color(0.40f, 0.12f, 0.09f),
            new Color(0.32f, 0.19f, 0.07f), new Color(0.30f, 0.13f, 0.16f),
            new Color(0.20f, 0.08f, 0.15f), new Color(0.12f, 0.08f, 0.08f),
            new Color(0.32f, 0.10f, 0.25f), new Color(0.30f, 0.21f, 0.07f),
            new Color(0.08f, 0.34f, 0.38f), new Color(0.40f, 0.14f, 0.32f),
            new Color(0.10f, 0.30f, 0.42f), new Color(0.42f, 0.16f, 0.34f),
            new Color(0.19f, 0.31f, 0.13f),
            new Color(0.20f, 0.24f, 0.14f), new Color(0.26f, 0.20f, 0.10f),
            new Color(0.05f, 0.27f, 0.30f), new Color(0.31f, 0.10f, 0.27f),
            new Color(0.24f, 0.19f, 0.11f), new Color(0.28f, 0.15f, 0.09f),
        };

        /// <summary>分区 id → 中文名（日志/自检用）。</summary>
        public static string ZoneLabel(byte zone)
            => zone < Labels.Length ? Labels[zone] : "未知" + zone;

        /// <summary>分区 id → 文件名段（材质资产 Zone{NN}_{段}.mat）。</summary>
        public static string ZoneFileLabel(byte zone)
            => zone < FileLabels.Length ? FileLabels[zone] : "Unknown";

        /// <summary>分区 id → 材质默认色（首建初值，落盘后归 Inspector 管）。</summary>
        public static Color DefaultColor(byte zone)
            => zone < DefaultColors.Length ? DefaultColors[zone] : Color.magenta;

        /// <summary>角色×归属 → 分区 id（越界/未知组合回落边缘环）。
        /// LandCap 分支已退役（2026-10-14 晚缩盘：地牌上限格不再存在，角色成墓碑永不命中）；
        /// Activation 分支同口径退役（2026-10-15 发动格拆除，角色成墓碑永不命中）；
        /// Skill 分支同口径退役（2026-10-16 技能格撤销，角色成墓碑永不命中）；
        /// Extra 分支已随 CellRole.Extra 整删（2026-10-29 P11）——23/24 改挂万能展示格（Any）；
        /// Field 分支（P11）：25/26 新增，材质资产未生成回落预制体自带材质。</summary>
        public static byte ZoneOf(CellRole role, int owner)
        {
            if (role == CellRole.Edge) return EdgeRing;
            if (role == CellRole.Middle) return MiddleRow;
            if (role == CellRole.Any) return owner == 0 ? P1Extra : P2Extra; // 万能格（沿用 23/24 材质资产）
            if (role == CellRole.Field) return owner == 0 ? P1Field : P2Field;
            int slot;
            switch (role)
            {
                case CellRole.Unit: slot = 0; break;
                case CellRole.Land: slot = 1; break;
                case CellRole.Deck: slot = 2; break;
                case CellRole.Graveyard: slot = 3; break;
                case CellRole.Exile: slot = 4; break;
                case CellRole.Character: slot = 5; break;
                default: return EdgeRing;
            }
            return (byte)((owner == 0 ? Owner0Base : Owner1Base) + slot);
        }
    }
}
