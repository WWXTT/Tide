using System.Collections.Generic;

namespace GameBoard
{
    /// <summary>格子语义角色</summary>
    public enum CellRole
    {
        /// <summary>边缘环（无玩法意义，视觉边框）</summary>
        Edge,

        /// <summary>单位区（随从/衍生物/结界共用，每方 2×9=18；与核心战场容量 18 对应）</summary>
        Unit,

        /// <summary>地牌行（每方 1×9；与地牌槽上限 9 对应）</summary>
        Land,

        /// <summary>卡组格</summary>
        Deck,

        /// <summary>额外卡组格</summary>
        ExtraDeck,

        /// <summary>墓地格</summary>
        Graveyard,

        /// <summary>除外格</summary>
        Exile,

        /// <summary>角色格（玩家生命在棋盘上的可交互位置；指向玩家 = 指向此格）</summary>
        Character,

        /// <summary>发动格（万能结算位；允许多张叠放 = 栈的物理呈现）</summary>
        Activation,
    }

    /// <summary>
    /// 13×8 对战棋盘布局常量表（唯一真相源）。
    ///
    /// 104 = 54 核心（9×6，双方各 9×3）+ 12 特殊格（每方 6）+ 38 边缘环；
    /// 双方 180° 旋转对称：rotate(c,r) = (12−c, 7−r)。
    /// 归属以玩家序号表达（0 = P1 下半场 / 1 = P2 上半场），与具体 Player 对象解耦，
    /// 由 BoardState 建立 Player ↔ 序号的映射。
    ///
    /// 玩家自身视角（非旋转）下的半场（11×3）：
    /// <code>
    ///        lx0      lx1 ……………… lx9      lx10
    ///  ly0   除外     ▓ 单位区（近中线）      额外
    ///  ly1   墓地     ▓ 单位区               卡组
    ///  ly2   发动     ▓ 地牌行（贴己方边缘）  角色
    /// </code>
    /// </summary>
    public static class BoardLayout
    {
        public const int Width = BoardMath.Width;    // 13
        public const int Height = BoardMath.Height;  // 8

        // ---- 核心列带 c2..c10（9 列）----
        public const int ColMin = 2;
        public const int ColMax = 10;

        // ---- P1 半场行（下半场；P2 = 180° 旋转）----
        public const int P1UnitFarZ = 4;   // P1 单位区·近中线行
        public const int P1UnitNearZ = 5;  // P1 单位区·近己方行
        public const int P1LandZ = 6;      // P1 地牌行（贴己方边缘）
        public const int P2UnitFarZ = 3;   // P2 单位区·近中线行（P1 视角下）
        public const int P2UnitNearZ = 2;
        public const int P2LandZ = 1;

        // ---- P1 特殊格（P2 = Rotate180 后逐一对应）----
        public const int P1ExileX = 1,  P1ExileZ = 4;
        public const int P1GraveX = 1,  P1GraveZ = 5;
        public const int P1ActX = 1,    P1ActZ = 6;
        public const int P1ExtraX = 11, P1ExtraZ = 4;
        public const int P1DeckX = 11,  P1DeckZ = 5;
        public const int P1CharX = 11,  P1CharZ = 6;

        public const int UnitCellsPerPlayer = 18; // 2×9 —— 须与 CardCore.ZoneManager.BattlefieldCapacityPerPlayer 一致
        public const int LandCellsPerPlayer = 9;  // 1×9 —— 与地牌槽上限 9 一致

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
            SetSpecial(CellRole.Exile, P1ExileX, P1ExileZ);
            SetSpecial(CellRole.Graveyard, P1GraveX, P1GraveZ);
            SetSpecial(CellRole.Activation, P1ActX, P1ActZ);
            SetSpecial(CellRole.ExtraDeck, P1ExtraX, P1ExtraZ);
            SetSpecial(CellRole.Deck, P1DeckX, P1DeckZ);
            SetSpecial(CellRole.Character, P1CharX, P1CharZ);
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
