using System.Collections.Generic;

namespace GameBoard
{
    /// <summary>格子语义角色</summary>
    public enum CellRole
    {
        /// <summary>边缘环（无玩法意义，视觉边框）</summary>
        Edge,

        /// <summary>单位区（随从/衍生物/结界共用，每方 2×6=12；与核心战场容量 12 对应）</summary>
        Unit,

        /// <summary>地牌行（每方 1×6；2026-10-14 晚定案：一种元素占一格——颜色按首现序占格、同色叠同一格）</summary>
        Land,

        /// <summary>卡组格</summary>
        Deck,

        /// <summary>墓地格</summary>
        Graveyard,

        /// <summary>除外格</summary>
        Exile,

        /// <summary>角色格（玩家生命在棋盘上的可交互位置；指向玩家 = 指向此格）</summary>
        Character,

        /// <summary>发动格（2026-10-15 拆除·墓碑保序）：固定发动格退役——施放中的卡改在
        /// 2×6 单位区空格上落格呈现（整个单位区兼任发动区，落格映射在 BoardState 施放占格）。
        /// 不再有任何格持有本角色。</summary>
        Activation,

        /// <summary>技能格（2026-10-16 撤销·墓碑保序）：英雄技能不再单独占格——技能介绍并入
        /// 角色格悬停、发动入口=角色格格前三键（Attack/Defense/Activate 与生物同款）。
        /// 原技能格位 (0,0)/(7,6) 回落边缘装饰格。不再有任何格持有本角色。</summary>
        Skill,

        /// <summary>地牌上限格（2026-10-14 晚退役·墓碑保序）：地牌行缩为整 6 格（一元素一格）
        /// 后无空位——上限数值改由地牌格悬停信息与技能格数字条承载。不再有任何格持有本角色。</summary>
        LandCap,

        /// <summary>中线行（2026-10-14 加行·当晚定为战场状态行·本批镜像纠正+阶段格）：两半场之间 7 格
        /// （x0..x6，归属 -1）——画面左→右（画面左=x0）：阶段显示 / 己方剩余操作时间 / 空 / 连锁速度 / 空 /
        /// 对手剩余操作时间 / 回合结束（贴己方右手边——原 lx0 放反已纠）
        /// （BattleHudRow 认领功能与状态地块；x7 槽位=odd-r 居中缺口，无实体格）。</summary>
        Middle,

        /// <summary>额外区格（2026-10-22 衍生物模板区）：衍生物模板卡开局落 Zone.Extra（不进牌库），
        /// 点击本格弹区域面板列出模板卡供识别。格位=原技能格位回收（P1 (0,0)／P2 旋转 (7,6)）。</summary>
        Extra,
    }

    /// <summary>
    /// 8×7 对战棋盘布局常量表（唯一真相源；2026-10-13 缩盘 9×6 → 10-14 中线加行 9×7 → 当晚缩盘 8×7
    /// → 当晚半场镜像修正）。
    ///
        /// 56 = 中线 7 + 可用 36（单位 2×6×2 + 地牌 6×2）+ 特殊格 8（每方 4：
        /// 角色/除外/墓地/卡组，入盘两侧列）+ 原发动格位 2（2026-10-15 发动格拆除，
        /// (0,2)/(7,4) 回落边缘装饰格——施放卡改在单位区空格落格）+ 原技能格位 2
        ///（2026-10-16 技能格撤销，(0,0)/(7,6) 回落边缘装饰格）+ 缺口 1（中线 x7 槽无格）；
    /// 双方 180° 旋转对称：rotate(c,r) = (7−c, 6−r)——中线状态行刻意不对称（7 格居中缺口），
    /// 玩法归属恒 -1，对称性验证对中线行豁免。
    /// 归属以玩家序号表达（0 = P1 下半场 / 1 = P2 上半场，中线 = -1），与具体 Player 对象解耦，
    /// 由 BoardState 建立 Player ↔ 序号的映射。
    ///
    /// 画面方位（2026-10-14 晚镜像修正：战场相机 (7,8,-3) 在 -z 侧朝 +z 俯拍——
    /// **画面底边=z0 贴玩家、画面左=x0**；原表把 P1 半场排在 z4..z6=画面上方，己方/对手整体装反，
    /// 镜像翻转修正）。玩家自身视角半场（8×3）：
    /// <code>
        ///        x0            x1 …………… x6          x7
        ///  z2   （边缘装饰）    ▓ 单位区（近中线）      除外(7,2)
        ///  z1   角色(0,1)      ▓ 单位区               墓地(7,1)
        ///  z0   （边缘装饰·    ▓ 地牌行×6（一元素一格） 卡组(7,0)
        ///       原技能格）
        /// </code>
        /// 施放中的卡（发动区占位期）落在单位区空格上呈现（2026-10-15 定案：整个 2×6 兼任发动区，
        /// 永久物=玩家所选格原地转正，法术=第一空格结算后离场）——原固定发动格(0,2)已拆为边缘装饰格；
        /// 原技能格(0,0) 2026-10-16 撤销回落边缘装饰格（技能介绍=角色格悬停、发动=角色格三键）。
        /// 中线行 z3 共 7 格（x0..x6，归属 -1）：lx0=阶段显示、lx1=己方剩余操作时间、lx3=连锁速度、
        /// lx5=对手剩余操作时间、lx2/lx4=空、lx6=回合结束（贴己方右手边——原 lx0 放反已纠，2026-10-14）；
        /// lx7 槽位=缺口（无 tile）。
    /// </summary>
    public static class BoardLayout
    {
        public const int Width = BoardMath.Width;    // 8
        public const int Height = BoardMath.Height;  // 7

        // ---- 可用列带 x1..x6（6 列；x0/x7 为两侧特殊格列）----
        public const int ColMin = 1;
        public const int ColMax = 6;

        // ---- P1 半场行（下半场=z0..z2 贴玩家；P2 = 180° 旋转=上半场；2026-10-14 晚镜像修正）----
        public const int P1UnitFarZ = 2;   // P1 单位区·近中线行
        public const int P1UnitNearZ = 1;  // P1 单位区·近己方行
        public const int P1LandZ = 0;      // P1 地牌行（贴己方边缘）
        public const int P2UnitFarZ = 4;   // P2 单位区·近中线行（P1 视角下）
        public const int P2UnitNearZ = 5;
        public const int P2LandZ = 6;
        public const int MiddleZ = 3;      // 中线行（2026-10-14 状态行定案：7 格，x7 槽=缺口）
        public const int MiddleCellCount = 7; // 中线行格数（x0..x6；odd-r +0.5 偏移下与 8 列偶行同轴心）

        /// <summary>无实体格缺口（当前仅中线 x7 一处）：BattleHexBoard 不拼 tile、
        /// BattlePathfinder 视作占用（演出不可借道驻留）。</summary>
        public static bool NoTileAt(int x, int z) => z == MiddleZ && x >= MiddleCellCount;

        // ---- P1 特殊格（2026-10-14 晚镜像修正：画面左=x0 列——角色落玩家左手 x0 列（z1），
        // 除外/墓地/卡组落右手 x7 列；P2 = Rotate180 后逐一对应。
        // 地牌上限格 2026-10-14 晚退役——地牌行整 6 格一元素一格；
        // 发动格 2026-10-15 拆除——施放卡改在单位区空格落格，(0,2) 回落边缘装饰格；
        // 技能格 2026-10-16 撤销——(0,0) 回落边缘装饰格（技能=角色格悬停+三键）----
        public const int P1CharX = 0,   P1CharZ = 1;  // 角色格：左列中间（格前三键=攻击/守卫/发动技能）
        public const int P1ExileX = 7,  P1ExileZ = 2; // 除外格：右列近中线
        public const int P1GraveX = 7,  P1GraveZ = 1; // 墓地格：右列中间
        public const int P1DeckX = 7,   P1DeckZ = 0;  // 卡组格：右列贴边（自身视角右下角）
        public const int P1ExtraX = 0,  P1ExtraZ = 0; // 额外区格：原技能格位回收（P2=旋转 (7,6)）——
                                                      // 衍生物模板卡展示入口（点击弹区域面板）
        // 角色格邻接：奇行 (0,1) 六邻圈=(1,1) 单位格 + (0,0) 额外区格（原技能格位回收，2026-10-22）+
        // (0,2) 边缘装饰格（原发动格位）——关键词光环经箭头投递角色格需要至少一个邻接单位格
        //（V9.s 几何扫描锚须随 8×7 几何更新）。

        // 容量单源（2026-10-13）：单位格数直接引用 ZoneManager 的表驱动容量，消灭手工双写
        public static int UnitCellsPerPlayer => CardCore.ZoneManager.BattlefieldCapacityPerPlayer;
        public const int LandCellsPerPlayer = 6;  // 1×6 —— 地牌行整行即 6 格（2026-10-14 晚定案：
                                                  // 一种元素占一格；灰/红/蓝/绿 4 色用不满，黑白不产地牌）

        /// <summary>地牌行固定元素序（2026-10-14 晚定案，玩家自身视角左→右）：灰红蓝绿黑白——
        /// LandCells(player)[i] 的元素身份恒为本序第 i 位（引擎落格/UI 图标/悬停文案共用单源）。
        /// 黑白只进钱包不产地牌：格常空但身份常显，格面数字=钱包余额。</summary>
        public static readonly CardCore.ManaType[] LandCellElementOrder =
        {
            CardCore.ManaType.Gray, CardCore.ManaType.Red, CardCore.ManaType.Blue,
            CardCore.ManaType.Green, CardCore.ManaType.Black, CardCore.ManaType.White,
        };

        /// <summary>固定元素格映射：元素 → 该玩家地牌行格（LandCellElementOrder 序）。
        /// 未知色回落序首（灰格）；黑白命中固定格但引擎永不入池（UI 侧常显身份）。</summary>
        public static (int x, int z) LandCellOfElement(int player, CardCore.ManaType element)
        {
            var cells = LandCells(player);
            if (cells.Count == 0) return (-1, -1);
            int i = System.Array.IndexOf(LandCellElementOrder, element);
            if (i < 0 || i >= cells.Count) i = 0;
            return cells[i];
        }

        // ---- 表（静态构造一次）----
        private static readonly CellRole[] _roles = new CellRole[Width * Height];
        private static readonly int[] _owners = new int[Width * Height]; // 0/1，-1 = 边缘/中线无归属

        static BoardLayout()
        {
            for (int i = 0; i < _roles.Length; i++)
            {
                _roles[i] = CellRole.Edge;
                _owners[i] = -1;
            }

            // P1 半场（归属 0），P2 半场由旋转生成（归属 1）——对称性由构造保证
            SetSpecial(CellRole.Character, P1CharX, P1CharZ);
            SetSpecial(CellRole.Exile, P1ExileX, P1ExileZ);
            SetSpecial(CellRole.Graveyard, P1GraveX, P1GraveZ);
            SetSpecial(CellRole.Deck, P1DeckX, P1DeckZ);
            SetSpecial(CellRole.Extra, P1ExtraX, P1ExtraZ); // 额外区格（原技能格位回收，2026-10-22）
            for (int x = ColMin; x <= ColMax; x++)
            {
                Set(CellRole.Unit, x, P1UnitFarZ, 0);
                Set(CellRole.Unit, x, P1UnitNearZ, 0);
                Set(CellRole.Land, x, P1LandZ, 0);
            }
            for (int x = 0; x < MiddleCellCount; x++)
                Set(CellRole.Middle, x, MiddleZ, -1); // 中线状态行 7 格（x7 槽=缺口，保持 Edge）

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

        /// <summary>格子归属玩家序号（0/1；边缘/中线 = -1）</summary>
        public static int OwnerOf(int x, int z) => _owners[BoardMath.Index(x, z)];

        /// <summary>玩家序号 → 角色格坐标（玩家生命在棋盘上的位置；核心不存坐标，这是布局常量）</summary>
        public static (int x, int z) CharacterCell(int player)
        {
            return player == 0 ? (P1CharX, P1CharZ) : Rotate180(P1CharX, P1CharZ);
        }

        /// <summary>玩家序号 → 地牌行格序列（该玩家自身视角左→右=x6..x1；2026-10-14 晚定案：
        /// 固定元素格——第 i 格元素身份恒为 LandCellElementOrder[i]，同色全部叠进自己那格）。</summary>
        public static IReadOnlyList<(int x, int z)> LandCells(int player) => player == 0 ? _landCells0 : _landCells1;

        /// <summary>
        /// 玩家序号 → 单位区格序列（确定性 first-free 分配的固定顺序：
        /// 按该玩家自身视角「近中线行 → 近己方行」、左→右）。
        /// </summary>
        public static IReadOnlyList<(int x, int z)> UnitCells(int player) => player == 0 ? _unitCells0 : _unitCells1;

        private static readonly (int x, int z)[] _unitCells0 = BuildUnitCells(P1UnitFarZ, P1UnitNearZ);
        private static readonly (int x, int z)[] _unitCells1 = BuildUnitCells(P2UnitFarZ, P2UnitNearZ);
        private static readonly (int x, int z)[] _landCells0 = BuildLandCells();
        private static readonly (int x, int z)[] _landCells1 = RotateRow(_landCells0);

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

        /// <summary>P1 地牌格序列：玩家自身视角左→右（画面左=x0 → x 递增），x1..x6 共 6 格（一元素一格）。</summary>
        private static (int x, int z)[] BuildLandCells()
        {
            var cells = new (int x, int z)[LandCellsPerPlayer];
            for (int i = 0; i < LandCellsPerPlayer; i++)
                cells[i] = (ColMin + i, P1LandZ);
            return cells;
        }

        /// <summary>P2 地牌格序列 = P1 序列的 180° 旋转像（自身视角左→右自动成立）。</summary>
        private static (int x, int z)[] RotateRow((int x, int z)[] source)
        {
            var cells = new (int x, int z)[source.Length];
            for (int i = 0; i < source.Length; i++)
                cells[i] = Rotate180(source[i].x, source[i].z);
            return cells;
        }
    }
}
