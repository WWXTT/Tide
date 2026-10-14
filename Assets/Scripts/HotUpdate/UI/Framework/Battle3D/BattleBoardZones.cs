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
        /// <summary>边缘环（未知角色回落；2026-10-13 特殊格重排后全盘 54 格皆有角色，本分区无实格——
        /// 保留回落语义与已落盘材质资产）。</summary>
        public const byte EdgeRing = 1;

        // 己方 P1(owner0)：2..8；对方 P2(owner1)：9..15。槽序=Unit/Land/Deck/Grave/Exile/Char/Act
        public const byte Owner0Base = 2;
        public const byte Owner1Base = 9;

        // 临时元素格（2026-10-13 临时元素上盘）：分区号只增不改（0..15 与已落盘材质资产
        // BattleTile_Zone{NN}_* 一一对应，插队会全体错位），双方临时元素追加 16/17。
        public const byte P1TempElement = 16;
        public const byte P2TempElement = 17;

        private static readonly string[] Labels =
        {
            "盘外背景", "边缘环",
            "己方单位区", "己方地牌行", "己方卡组", "己方墓地", "己方除外", "己方角色", "己方发动",
            "对方单位区", "对方地牌行", "对方卡组", "对方墓地", "对方除外", "对方角色", "对方发动",
            "己方临时元素", "对方临时元素",
        };

        private static readonly string[] FileLabels =
        {
            "Backdrop", "Edge",
            "P1Unit", "P1Land", "P1Deck", "P1Grave", "P1Exile", "P1Char", "P1Act",
            "P2Unit", "P2Land", "P2Deck", "P2Grave", "P2Exile", "P2Char", "P2Act",
            "P1Temp", "P2Temp",
        };

        // 生成器默认色板（材质资产落盘后由用户在 Inspector 调色，此处仅首建初值）
        private static readonly Color[] DefaultColors =
        {
            new Color(0.045f, 0.052f, 0.068f), new Color(0.115f, 0.125f, 0.145f),
            new Color(0.09f, 0.21f, 0.36f), new Color(0.07f, 0.27f, 0.17f),
            new Color(0.13f, 0.15f, 0.28f), new Color(0.11f, 0.09f, 0.19f),
            new Color(0.08f, 0.08f, 0.10f), new Color(0.09f, 0.25f, 0.28f),
            new Color(0.20f, 0.23f, 0.08f), new Color(0.40f, 0.12f, 0.09f),
            new Color(0.32f, 0.19f, 0.07f), new Color(0.30f, 0.13f, 0.16f),
            new Color(0.20f, 0.08f, 0.15f), new Color(0.12f, 0.08f, 0.08f),
            new Color(0.32f, 0.10f, 0.25f), new Color(0.30f, 0.21f, 0.07f),
            new Color(0.08f, 0.34f, 0.38f), new Color(0.40f, 0.14f, 0.32f),
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

        /// <summary>角色×归属 → 分区 id（越界/未知组合回落边缘环）。</summary>
        public static byte ZoneOf(CellRole role, int owner)
        {
            if (role == CellRole.Edge) return EdgeRing;
            if (role == CellRole.TempElement) return owner == 0 ? P1TempElement : P2TempElement;
            int slot;
            switch (role)
            {
                case CellRole.Unit: slot = 0; break;
                case CellRole.Land: slot = 1; break;
                case CellRole.Deck: slot = 2; break;
                case CellRole.Graveyard: slot = 3; break;
                case CellRole.Exile: slot = 4; break;
                case CellRole.Character: slot = 5; break;
                case CellRole.Activation: slot = 6; break;
                default: return EdgeRing;
            }
            return (byte)((owner == 0 ? Owner0Base : Owner1Base) + slot);
        }
    }
}
