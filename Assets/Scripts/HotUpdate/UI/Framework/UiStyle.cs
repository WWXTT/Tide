using UnityEngine;

namespace SynergyUI
{
    /// <summary>
    /// 主题常量（2026-10-01 UITK→UGUI 全量替换）——自 Common.uss 深色实用主题提取。
    /// 观感对标旧 USS：信息架构与配色调性保持，不做像素级还原。
    /// </summary>
    public static class UiStyle
    {
        // ---------- 背景 ----------
        public static readonly Color ScreenBg = Rgb(24, 27, 33);    // .screen
        public static readonly Color PanelBg = Rgb(33, 37, 45);     // .panel / .battle-zone
        public static readonly Color ListBg = Rgb(28, 31, 38);      // .list / .card-row / .battle-log
        public static readonly Color RowBg = Rgb(40, 45, 55);       // .list-row
        public static readonly Color RowHoverBg = Rgb(50, 56, 68);  // .list-row:hover
        public static readonly Color CellBg = Rgb(26, 29, 36);      // .hex-cell
        public static readonly Color SlotBg = Rgb(30, 33, 41);      // 卡牌占位槽
        public static readonly Color FieldBg = Rgb(40, 45, 55);     // 输入框底

        // ---------- 边框 ----------
        public static readonly Color Border = Rgb(46, 51, 61);      // .panel border
        public static readonly Color BorderOpponent = Rgb(120, 70, 70);
        public static readonly Color BorderSelf = Rgb(64, 100, 160);
        public static readonly Color SelectedRowBg = Rgb(46, 78, 132); // .list-row--selected

        // ---------- 按钮 ----------
        public static readonly Color BtnBg = Rgb(54, 60, 72);           // .btn
        public static readonly Color BtnPrimary = Rgb(64, 132, 240);   // .btn--primary
        public static readonly Color BtnPrimaryHover = Rgb(86, 150, 250);
        public static readonly Color BtnDanger = Rgb(196, 72, 72);     // .btn--danger
        public static readonly Color White = new Color(1f, 1f, 1f, 1f);

        // ---------- 文本 ----------
        public static readonly Color TextPrimary = Rgb(238, 240, 245); // .screen__title
        public static readonly Color TextBody = Rgb(225, 229, 236);    // .btn / .list-row__name
        public static readonly Color TextSecondary = Rgb(210, 215, 224); // .panel__header
        public static readonly Color TextDim = Rgb(180, 186, 198);     // .battle-meta
        public static readonly Color TextFaint = Rgb(150, 156, 168);   // .screen__subtitle
        public static readonly Color TextHint = Rgb(140, 146, 158);    // .hint
        public static readonly Color ToastGreen = Rgb(120, 210, 140);  // .toast
        public static readonly Color ErrorRed = Rgb(235, 82, 82);      // .zone--invalid
        public static readonly Color DescStrip = Rgb(172, 202, 238);   // .desc-strip

        // ---------- 战报/强调 ----------
        public static readonly Color LogBase = Rgb(176, 182, 194);    // .log-line
        public static readonly Color LogTurn = Rgb(148, 212, 255);     // .log-line--turn
        public static readonly Color LogCombat = Rgb(235, 150, 130);   // .log-line--combat
        public static readonly Color LogSystem = Rgb(120, 200, 150);   // .log-line--system
        public static readonly Color AccentBlue = Rgb(86, 150, 250);

        // ---------- 色点（USS 提亮版——深底上小圆点需加饱和） ----------
        public static readonly Color DotRed = Rgb(232, 82, 82);
        public static readonly Color DotBlue = Rgb(84, 138, 242);
        public static readonly Color DotGreen = Rgb(74, 200, 108);
        public static readonly Color DotGray = Rgb(148, 154, 166);
        public static readonly Color DotBlack = Rgb(20, 21, 26);
        public static readonly Color DotWhite = Rgb(240, 243, 248);
        public static readonly Color DotBorder = new Color(0.92f, 0.94f, 0.96f, 0.45f);

        // ---------- 遮罩 ----------
        public static readonly Color OverlayDim = new Color(10f / 255f, 12f / 255f, 16f / 255f, 0.72f);

        // ---------- 字号 ----------
        public const int TitleSize = 28;    // .screen__title
        public const int SubtitleSize = 14; // .screen__subtitle
        public const int BodySize = 14;     // .btn
        public const int HeaderSize = 16;   // .panel__header
        public const int SmallSize = 12;    // .list-row__meta
        public const int MiniSize = 11;     // .battle-card__name
        public const int TinySize = 10;     // .land-tokens

        // ---------- 尺寸 ----------
        public const float Pad = 16f;       // .screen padding
        public const float BtnHeight = 36f; // .btn
        public const float MiniBtnHeight = 26f;
        public const float RowHeight = 40f; // .list-row
        public const float FieldHeight = 32f;

        private static Color Rgb(int r, int g, int b) =>
            new Color(r / 255f, g / 255f, b / 255f, 1f);
    }
}
