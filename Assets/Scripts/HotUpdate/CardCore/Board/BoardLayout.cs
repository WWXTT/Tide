using System.Collections.Generic;

namespace GameBoard
{
    /// <summary>格子语义角色</summary>
    public enum CellRole
    {
        /// <summary>边缘环（无玩法意义，视觉边框）</summary>
        Edge,

        /// <summary>单位区（随从/衍生物/结界共用，每方 2×7=14；与核心战场容量 14 对应）</summary>
        Unit,

        /// <summary>地牌行（每方 1×7；与地牌槽上限 7 对应）</summary>
        Land,

        /// <summary>卡组格</summary>
        Deck,

        /// <summary>墓地格</summary>
        Graveyard,

        /// <summary>除外格</summary>
        Exile,

        /// <summary>角色格（玩家生命在棋盘上的可交互位置；指向玩家 = 指向此格）</summary>
        Character,

        /// <summary>发动格（万能结算位；允许多张叠放 = 栈的物理呈现）</summary>
        Activation,

        /// <summary>临时元素格（临时钱包在棋盘上的落位，地牌行左端；余额由 BattleFieldView 对账刷新）</summary>
        TempElement,
    }

    /// <summary>
    /// 9×6 对战棋盘布局常量表（唯一真相源；2026-10-13 缩盘定案：13×8→9×6，
    /// 两侧列吸收原特殊格条，可用带收窄到 7 列）。
    ///
    /// 54 = 42 可用（7×6，双方各 7×3）+ 12 特殊格（每方 6：发动/角色/临时元素/除外/墓地/卡组，
    /// 入盘两侧列；2026-10-13 特殊格重排定案：临时元素上盘吸收旧 (0,2)/(8,3) 空格，全盘无空格）；
    /// 双方 180° 旋转对称：rotate(c,r) = (8−c, 5−r)。
    /// 归属以玩家序号表达（0 = P1 下半场 / 1 = P2 上半场），与具体 Player 对象解耦，
    /// 由 BoardState 建立 Player ↔ 序号的映射。
    ///
    /// 玩家自身视角（非旋转）下的半场（9×3；战场相机自 P1 侧俯拍、画面左=+x——
    /// 玩家看的「左列」=lx8=发动/角色/临时元素，「右列」=lx0=除外/墓地/卡组）：
    /// <code>
    ///        lx0      lx1 …………… lx7      lx8
    ///  ly0   除外     ▓ 单位区（近中线）      发动
    ///  ly1   墓地     ▓ 单位区               角色/技能
    ///  ly2   卡组     ▓ 地牌行（贴己方边缘）  临时元素
    /// </code>
    /// </summary>
    public static class BoardLayout
    {
        public const int Width = BoardMath.Width;    // 9
        public const int Height = BoardMath.Height;  // 6

        // ---- 可用列带 c1..c7（7 列；x0/x8 为两侧特殊格列）----
        public const int ColMin = 1;
        public const int ColMax = 7;

        // ---- P1 半场行（下半场；P2 = 180° 旋转）----
        public const int P1UnitFarZ = 3;   // P1 单位区·近中线行
        public const int P1UnitNearZ = 4;  // P1 单位区·近己方行
        public const int P1LandZ = 5;      // P1 地牌行（贴己方边缘）
        public const int P2UnitFarZ = 2;   // P2 单位区·近中线行（P1 视角下）
        public const int P2UnitNearZ = 1;
        public const int P2LandZ = 0;

        // ---- P1 特殊格（2026-10-13 重排定案·晚修正左右：战场相机自 P1 侧俯拍、画面左=+x——
        // 发动/角色/临时元素落 lx8（玩家看=左列）、除外/墓地/卡组落 lx0（右列）；P2 = Rotate180 后逐一对应）----
        public const int P1ActX = 8,    P1ActZ = 3;   // 发动格：左列近中线
        public const int P1CharX = 8,   P1CharZ = 4;  // 角色格：左列中间（角色/技能同区，2026-10-13 定案）
        public const int P1TempX = 8,   P1TempZ = 5;  // 临时元素格：左列贴边（与地牌同行，钱包就地展示）
        public const int P1ExileX = 0,  P1ExileZ = 3; // 除外格：右列近中线
        public const int P1GraveX = 0,  P1GraveZ = 4; // 墓地格：右列中间
        public const int P1DeckX = 0,   P1DeckZ = 5;  // 卡组格：右下角
        // 角色格邻接：偶行 (8,4) 六邻圈=(7,3)/(7,4) 两单位格 + 发动/临时元素/地牌行——
        // 关键词光环经箭头投递角色格需要至少一个邻接单位格（V9.s 几何扫描锚），双单位格满足。

        // 容量单源（2026-10-13）：单位格数直接引用 ZoneManager 的表驱动容量，消灭手工双写
        public static int UnitCellsPerPlayer => CardCore.ZoneManager.BattlefieldCapacityPerPlayer;
        public const int LandCellsPerPlayer = 7;  // 1×7 —— 地牌行格数（槽上限 7 × 每格 2 层=14，见 LandStackPerCell）
        public const int LandStackPerCell = 2;    // 每地牌格叠放层数（2026-10-09 地牌槽提升定案：上限翻倍封顶 14=7格×2层）

        // ---- 表（静态构造一次）----
        private static readonly CellRole[] _roles = new CellRole[Width * Height];
        private static readonly int[] _owners = new int[Width * Height]; // 0/1，-1 = 边缘无归属

        static BoardLayout()
        {
            for (int i = 0; i < _roles.Length; i++)
            {
                _roles[i] = CellRole.Edge;
                _owners[i] = -1;
            }

            // P1 半场（归属 0），P2 半场由旋转生成（归属 1）——对称性由构造保证
            SetSpecial(CellRole.Activation, P1ActX, P1ActZ);
            SetSpecial(CellRole.Character, P1CharX, P1CharZ);
            SetSpecial(CellRole.TempElement, P1TempX, P1TempZ);
            SetSpecial(CellRole.Exile, P1ExileX, P1ExileZ);
            SetSpecial(CellRole.Graveyard, P1GraveX, P1GraveZ);
            SetSpecial(CellRole.Deck, P1DeckX, P1DeckZ);
            for (int x = ColMin; x <= ColMax; x++)
            {
                Set(CellRole.Unit, x, P1UnitFarZ, 0);
                Set(CellRole.Unit, x, P1UnitNearZ, 0);
                Set(CellRole.Land, x, P1LandZ, 0);
            }

            // P2 = 旋转镜像：把归属 0 的每个格子经 Rotate180 复制为归属 1
            for (int z = 0; z < Height; z++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int i = BoardMath.Index(x, z);
                    if (_owners[i] != 0) continue;
                    var (mx, mz) = Rotate180(x, z);
                    int mi = BoardMath.Index(mx, mz);
                    _roles[mi] = _roles[i];
                    _owners[mi] = 1;
                }
            }
        }

        private static void Set(CellRole role, int x, int z, int owner)
        {
            int i = BoardMath.Index(x, z);
            _roles[i] = role;
            _owners[i] = owner;
        }

        private static void SetSpecial(CellRole role, int x, int z) => Set(role, x, z, 0);

        /// <summary>180° 旋转（双方半场对称映射）</summary>
        public static (int x, int z) Rotate180(int x, int z) => (Width - 1 - x, Height - 1 - z);

        public static CellRole RoleOf(int x, int z) => _roles[BoardMath.Index(x, z)];

        /// <summary>格子归属玩家序号（0/1；边缘 = -1）</summary>
        public static int OwnerOf(int x, int z) => _owners[BoardMath.Index(x, z)];

        /// <summary>玩家序号 → 角色格坐标（玩家生命在棋盘上的位置；核心不存坐标，这是布局常量）</summary>
        public static (int x, int z) CharacterCell(int player)
        {
            return player == 0 ? (P1CharX, P1CharZ) : Rotate180(P1CharX, P1CharZ);
        }

        /// <summary>玩家序号 → 发动格坐标</summary>
        public static (int x, int z) ActivationCell(int player)
        {
            return player == 0 ? (P1ActX, P1ActZ) : Rotate180(P1ActX, P1ActZ);
        }

        /// <summary>玩家序号 → 临时元素格坐标（临时钱包的棋盘落位，地牌行左端）</summary>
        public static (int x, int z) TempElementCell(int player)
        {
            return player == 0 ? (P1TempX, P1TempZ) : Rotate180(P1TempX, P1TempZ);
        }

        /// <summary>
        /// 玩家序号 → 单位区格序列（确定性 first-free 分配的固定顺序：
        /// 按该玩家自身视角「近中线行 → 近己方行」、左→右）。
        /// </summary>
        public static IReadOnlyList<(int x, int z)> UnitCells(int player) => player == 0 ? _unitCells0 : _unitCells1;

        /// <summary>玩家序号 → 地牌行格序列（左→右）</summary>
        public static IReadOnlyList<(int x, int z)> LandCells(int player) => player == 0 ? _landCells0 : _landCells1;

        private static readonly (int x, int z)[] _unitCells0 = BuildUnitCells(P1UnitFarZ, P1UnitNearZ);
        private static readonly (int x, int z)[] _unitCells1 = BuildUnitCells(P2UnitFarZ, P2UnitNearZ);
        private static readonly (int x, int z)[] _landCells0 = BuildRow(P1LandZ);
        private static readonly (int x, int z)[] _landCells1 = BuildRow(P2LandZ);

        private static (int x, int z)[] BuildUnitCells(int farZ, int nearZ)
        {
            // 防呆（2026-10-13 缩盘）：容量配置与单位区几何必须一致，否则格序列出现空尾 null 格
            int geometryCells = (ColMax - ColMin + 1) * 2;
            if (UnitCellsPerPlayer != geometryCells)
                throw new System.InvalidOperationException(
                    $"战场容量配置 {UnitCellsPerPlayer} 与单位区几何 {geometryCells}（2×可用列数）不一致——" +
                    "须同步改 ValueSystemConfig.GameRules.BattlefieldCapacityPerPlayer");
            var cells = new (int x, int z)[UnitCellsPerPlayer];
            int i = 0;
            foreach (int z in new[] { farZ, nearZ })
                for (int x = ColMin; x <= ColMax; x++)
                    cells[i++] = (x, z);
            return cells;
        }

        private static (int x, int z)[] BuildRow(int z)
        {
            var cells = new (int x, int z)[LandCellsPerPlayer];
            for (int x = ColMin; x <= ColMax; x++)
                cells[x - ColMin] = (x, z);
            return cells;
        }
    }
}
