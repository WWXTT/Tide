using System.Collections.Generic;

namespace GameBoard
{
    /// <summary>格子语义角色</summary>
    public enum CellRole
    {
        /// <summary>边缘环（无玩法意义，视觉边框）</summary>
        Edge,

        /// <summary>单位区（随从/衍生物/结界共用，每方近中线行 4 + 近己方行 5 = 9；与核心战场容量 9 对应）</summary>
        Unit,

        /// <summary>地牌行（每方 1×4；灰红蓝绿四色一元素一格，万能展示格见 AnyElementCell）。</summary>
        Land,

        /// <summary>卡组格</summary>
        Deck,

        /// <summary>墓地格</summary>
        Graveyard,

        /// <summary>除外格</summary>
        Exile,

        /// <summary>角色格（玩家生命在棋盘上的可交互位置；指向玩家 = 指向此格）</summary>
        Character,

        /// <summary>场地格（2026-10-29 P11 盘面重塑新建·空角色位）：P1 (1,2)／P2=镜像像 (6,4)。
        /// 本包只建角色位——不产卡、不接规则（场地规则归 P8），点击/悬停暂无行为。</summary>
        Field,

        // 发动格（Activation·2026-10-15 拆除）/技能格（Skill·2026-10-16 撤销）/地牌上限格
        //（LandCap·2026-10-14 晚退役）三角色位退役——不再有任何格持有；枚举位已随
        // 2026-10-30 枚举收拢删除。

        /// <summary>中线行（2026-10-29 P11 缩 5 格居中）：两半场之间 5 格（x1..x5，归属 -1）
        /// ——画面左→右（画面左=x0）：阶段显示(x1) / 可用时间(x2·当前操作方合一格) /
        /// 连锁速度(x3) / 空(x4·不认领) / 回合结束(x5，贴己方右手边)；x0/x6 槽位=缺口，无实体格。</summary>
        Middle,

        /// <summary>万能元素展示格（2026-10-29 P11 改独立角色·只做展示；当晚对座镜像修订迁 (1,0)）：
        /// P1 (1,0)／P2=镜像像 (6,6)。任意元素余额的图标/数字/描边环展示位（BattleFieldView.ReconcileLands）——
        /// 旧 CellRole.Extra（额外区格·点击开额外区面板）已随 P11 定案整删：额外区无盘面入口，
        /// 衍生物模板区 Zone.Extra 本身保留（P9b 域，无嵌套校验亦不在本包）。</summary>
        Any,
    }

    /// <summary>
    /// 7×7 对战棋盘布局常量表（唯一真相源；盘形沿革：9×6 → 10-14 中线加行 9×7 → 当晚 8×7
    /// → 2026-10-28 元素缩编 7876787 → 2026-10-29 P11 盘面重塑 6-7-6 → 当晚对座镜像修订：
    /// 双方六格行同列带（x1..x6），P2 由 180° 旋转像改上下镜像像 MirrorSeat）。
    ///
    /// 43 = 中线 5 + 单位区 18（每方 5+4=9）+ 地牌 8（每方 4）+ 特殊格 6（每方 3：
    /// 角色 (0,1)/(6,5)、除外 (6,2)/(1,4)、墓地 (6,1)/(0,5)、卡组 (6,0)/(1,6)）+
    /// 万能展示格 2（(1,0)/(6,6)）+ 场地格 2（(1,2)/(6,4)·P11 空角色位）；
    /// 缺口 6（7×7=49 格位数 − 43 实格）：x0 列四格（z0/z2/z4/z6 六格行行首）
    /// + 中线两端 (0,3)/(6,3)；
    /// 对称=对座镜像（MirrorSeat：z→6−z；六格行 x→7−x、七格行/中线 x→6−x）——
    /// 双方半场画面上同形同列带对齐（旧 180° 旋转在 odd-r 奇行 +0.5 偏移下会使
    /// 两半场六格行错开一列，2026-10-29 用户定案废弃），中线 5 格自对称
    ///（(1,3)↔(5,3)、(3,3) 自映），玩法归属恒 -1。
    /// 归属以玩家序号表达（0 = P1 下半场 / 1 = P2 上半场，中线 = -1），与具体 Player 对象解耦，
    /// 由 BoardState 建立 Player ↔ 序号的映射。
    ///
    /// 画面方位（战场相机在 -z 侧朝 +z 俯拍——**画面底边=z0 贴玩家、画面左=x0**）。
    /// 玩家自身视角半场（行格数 6/7/6，六格行自第二列 x1 起）：
    /// <code>
    ///        x0        x1        x2 …… x5      x6
    ///  z2   （缺口）   场地(1,2)  单位 单位 单位 除外(6,2)
    ///  z1   角色(0,1)  单位 单位 单位 单位 单位  墓地(6,1)
    ///  z0   （缺口）   万能(1,0)  灰(2,0)…绿(5,0) 卡组(6,0)
    /// </code>
    /// 中线行 z3 共 5 格（x1..x5，归属 -1）：lx1=阶段显示、lx2=可用时间（当前操作方合一格）、
    /// lx3=连锁速度（掷币落点，世界位与旧版重合）、lx4=空（不认领）、lx5=回合结束（贴己方右手边）；
    /// lx0/lx6 槽位=缺口（无 tile）。
    /// 施放中的卡（发动区占位期）落在单位区空格上呈现（2026-10-15 定案沿用）。
    /// </summary>
    public static class BoardLayout
    {
        public const int Width = BoardMath.Width;    // 7
        public const int Height = BoardMath.Height;  // 7

        // ---- P1 半场行（下半场=z0..z2 贴玩家；P2 = 对座镜像=上半场）----
        public const int P1UnitFarZ = 2;   // P1 单位区·近中线行（4 格 x2..x5——x1=场地格、x6=除外格）
        public const int P1UnitNearZ = 1;  // P1 单位区·近己方行（5 格 x1..x5——x0=角色格、x6=墓地格）
        public const int P1LandZ = 0;      // P1 地牌行（贴己方边缘；6 格 x1..x6——x1=万能、x2..x5=四色、x6=卡组、x0=缺口）
        public const int P2UnitFarZ = 4;   // P2 单位区·近中线行（P1 视角下；镜像像 4 格 x2..x5）
        public const int P2UnitNearZ = 5;  // P2 单位区·近己方行（镜像像 5 格 x1..x5）
        public const int P2LandZ = 6;
        public const int MiddleZ = 3;      // 中线行（P11：5 格居中）
        public const int MiddleStartX = 1;     // 中线行首格（x1；5 格 x1..x5 居中——镜像自对称）
        public const int MiddleCellCount = 5;  // 中线行格数（x0/x6 槽=缺口）

        /// <summary>无实体格缺口（2026-10-29 对座镜像盘形）：x0 列四格（z0/z2/z4/z6 六格行行首）
        /// + 中线两端 (0,3)/(6,3)：BattleHexBoard 不拼 tile、BattlePathfinder 视作占用
        ///（演出不可借道驻留）。</summary>
        public static bool NoTileAt(int x, int z)
            => (z == MiddleZ && (x < MiddleStartX || x >= MiddleStartX + MiddleCellCount))
            || (x == 0 && (z == P1LandZ || z == P1UnitFarZ || z == P2LandZ || z == P2UnitFarZ));

        // ---- P1 特殊格（P2 = MirrorSeat 后逐一对应。
        // 地牌上限格 2026-10-14 晚退役；发动格 2026-10-15 拆除；技能格 2026-10-16 撤销；
        // 额外区格 2026-10-29 P11 整删（Zone.Extra 保留、无盘面入口），
        // 万能展示格改独立 Any 角色只做展示；场地格 Field 空角色位（规则归 P8） ----
        public const int P1CharX = 0,   P1CharZ = 1;  // 角色格：左列中间（七格行独占 x0——六格行 x0 皆缺口）
        public const int P1FieldX = 1,  P1FieldZ = 2; // 场地格：第三行首格 x1（角色格斜上邻·P11 空角色位·规则归 P8）
        public const int P1ExileX = 6,  P1ExileZ = 2; // 除外格：六格行尾（近中线行 x6——墓地 (6,1) 正上邻）
        public const int P1GraveX = 6,  P1GraveZ = 1; // 墓地格：七格行尾（近己方行 x6）
        public const int P1DeckX = 6,   P1DeckZ = 0;  // 卡组格：地盘行尾（x6）
        public const int P1AnyX = 1,    P1AnyZ = 0;   // 万能元素展示格：地盘行首格（Any 角色·只做展示，
                                                      // 余额图标/数字/描边环，BattleFieldView.ReconcileLands）
        // 角色格邻接（2026-10-29 对座镜像重推导）：奇行 (0,1) 六邻圈内带 tile 的邻格=单位 (1,1) +
        // 场地 (1,2)/万能 (1,0)（(0,0)/(0,2) 已收边缺口）——「至少一个邻接单位格」约束成立（一格），
        // 几何扫描锚随本表。

        // 容量单源（2026-10-13）：单位格数直接引用 ZoneManager 的表驱动容量，消灭手工双写
        public static int UnitCellsPerPlayer => CardCore.ZoneManager.BattlefieldCapacityPerPlayer;
        public const int LandCellsPerPlayer = 4;  // 1×4 —— 灰红蓝绿四色一元素一格（x2..x5）
        public const int LandBandStartX = 2;      // 地牌色带起点（P1 半场；万能 (1,0) 占行首、卡组 (6,0) 占行尾）

        /// <summary>地牌行固定元素序（玩家自身视角左→右）：灰红蓝绿——
        /// LandCells(player)[i] 的元素身份恒为本序第 i 位（引擎落格/UI 图标/悬停文案共用单源）。
        /// 任意元素（Any）→ 万能展示格（AnyElementCell——视觉叠加展示，不入四色序/不占 Land 格角色）。</summary>
        public static readonly CardCore.ManaType[] LandCellElementOrder =
        {
            CardCore.ManaType.Gray, CardCore.ManaType.Red,
            CardCore.ManaType.Blue, CardCore.ManaType.Green,
        };

        /// <summary>固定元素格映射：元素 → 该玩家地牌行格（LandCellElementOrder 序）。
        /// 未知色回落序首（灰格——防御口径）；
        /// 任意元素（Any）→ 万能展示格（AnyElementCell——视觉叠加展示，不入 LandCellElementOrder
        /// 四色序/不占 Land 格角色）。</summary>
        public static (int x, int z) LandCellOfElement(int player, CardCore.ManaType element)
        {
            if (element == CardCore.ManaType.Any) return AnyElementCell(player);
            var cells = LandCells(player);
            if (cells.Count == 0) return (-1, -1);
            int i = System.Array.IndexOf(LandCellElementOrder, element);
            if (i < 0 || i >= cells.Count) i = 0;
            return cells[i];
        }

        /// <summary>万能元素展示格：地盘行 (1,0)／P2=镜像像 (6,6)。任意元素不由地牌产出、
        /// 永不叠地牌卡：该格只作视觉叠加（图标+钱包余额数字+余额&gt;0 描边环，
        /// BattleFieldView.ReconcileLands）；格角色=Any（只做展示，无点击行为——
        /// 旧「点击开额外区面板」口径已随 P11 额外区格整删退役）。</summary>
        public static (int x, int z) AnyElementCell(int player)
            => player == 0 ? (P1AnyX, P1AnyZ) : MirrorSeat(P1AnyX, P1AnyZ);

        /// <summary>场地格（2026-10-29 P8 规则接入）：场地卡（结界+场地标记）固定落位——
        /// P1=(1,2) 第三行首格（角色格斜上邻）、P2=镜像像 (6,4)。每方一格；只能被自己顶替（旧场地入墓）。
        /// 场地卡不计单位区容量（HasBattlefieldSpace 滤除）；普通卡不可落此格（UnitCells 天然不含）。</summary>
        public static (int x, int z) FieldCell(int player)
            => player == 0 ? (P1FieldX, P1FieldZ) : MirrorSeat(P1FieldX, P1FieldZ);

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

            // P1 半场（归属 0），P2 半场由对座镜像生成（归属 1）——同列带对齐由构造保证
            SetSpecial(CellRole.Character, P1CharX, P1CharZ);
            SetSpecial(CellRole.Field, P1FieldX, P1FieldZ);   // 场地格空角色位（P11·规则归 P8）
            SetSpecial(CellRole.Exile, P1ExileX, P1ExileZ);
            SetSpecial(CellRole.Graveyard, P1GraveX, P1GraveZ);
            SetSpecial(CellRole.Deck, P1DeckX, P1DeckZ);
            SetSpecial(CellRole.Any, P1AnyX, P1AnyZ);         // 万能展示格（Any 角色·只做展示）
            for (int x = UnitFarXStart; x <= UnitFarXEnd; x++)
                Set(CellRole.Unit, x, P1UnitFarZ, 0);
            for (int x = UnitNearXStart; x <= UnitNearXEnd; x++)
                Set(CellRole.Unit, x, P1UnitNearZ, 0);
            for (int x = LandBandStartX; x < LandBandStartX + LandCellsPerPlayer; x++)
                Set(CellRole.Land, x, P1LandZ, 0);
            for (int x = MiddleStartX; x < MiddleStartX + MiddleCellCount; x++)
                Set(CellRole.Middle, x, MiddleZ, -1); // 中线状态行 5 格（x0/x6 槽=缺口，保持 Edge）

            // P2 = 对座镜像：把归属 0 的每个格子经 MirrorSeat 复制为归属 1
            for (int z = 0; z < Height; z++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int i = BoardMath.Index(x, z);
                    if (_owners[i] != 0) continue;
                    var (mx, mz) = MirrorSeat(x, z);
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

        /// <summary>对座镜像（2026-10-29 晚定案：P2 半场=上下镜像像，不再是网格 180° 旋转像——
        /// odd-r 奇行 +0.5 偏移下旋转会使两半场六格行错开一列）：z→6−z；
        /// x 按行带宽翻转——六格行（z0/z2/z4/z6）x→7−x、七格行/中线（z1/z3/z5）x→6−x。</summary>
        public static (int x, int z) MirrorSeat(int x, int z)
        {
            int mx = (z == P1LandZ || z == P1UnitFarZ || z == P2LandZ || z == P2UnitFarZ)
                ? Width - x
                : Width - 1 - x;
            return (mx, Height - 1 - z);
        }

        public static CellRole RoleOf(int x, int z) => _roles[BoardMath.Index(x, z)];

        /// <summary>格子归属玩家序号（0/1；边缘/中线 = -1）</summary>
        public static int OwnerOf(int x, int z) => _owners[BoardMath.Index(x, z)];

        /// <summary>玩家序号 → 角色格坐标（玩家生命在棋盘上的位置；核心不存坐标，这是布局常量）</summary>
        public static (int x, int z) CharacterCell(int player)
        {
            return player == 0 ? (P1CharX, P1CharZ) : MirrorSeat(P1CharX, P1CharZ);
        }

        /// <summary>玩家序号 → 地牌行格序列（P1=x2..x5（自身视角左→右），P2=镜像像；固定元素格——
        /// 第 i 格元素身份恒为 LandCellElementOrder[i]，同色全部叠进自己那格）。</summary>
        public static IReadOnlyList<(int x, int z)> LandCells(int player) => player == 0 ? _landCells0 : _landCells1;

        /// <summary>
        /// 玩家序号 → 单位区格序列（确定性 first-free 分配的固定顺序：
        /// 按该玩家自身视角「近中线行 → 近己方行」、左→右）。
        /// </summary>
        public static IReadOnlyList<(int x, int z)> UnitCells(int player) => player == 0 ? _unitCells0 : _unitCells1;

        private static readonly (int x, int z)[] _unitCells0 = BuildUnitCells(P1UnitFarZ, P1UnitNearZ);
        private static readonly (int x, int z)[] _unitCells1 = MirrorRow(_unitCells0); // P2=镜像像（自身视角左→右自动成立）
        private static readonly (int x, int z)[] _landCells0 = BuildLandCells();
        private static readonly (int x, int z)[] _landCells1 = MirrorRow(_landCells0);

        // P1 单位区行内列带（2026-10-29 对座镜像）：近中线行 4 格（x1=场地格、x6=除外格让位两端），
        // 近己方行 5 格（x0=角色格、x6=墓地格让位两端）——两行列带不同宽，不再有统一可用列带
        private const int UnitFarXStart = 2, UnitFarXEnd = 5;   // z2：4 格
        private const int UnitNearXStart = 1, UnitNearXEnd = 5; // z1：5 格

        private static (int x, int z)[] BuildUnitCells(int farZ, int nearZ)
        {
            // 防呆（沿用 2026-10-13）：容量配置与单位区几何必须一致，否则格序列出现空尾 null 格
            int geometryCells = (UnitFarXEnd - UnitFarXStart + 1) + (UnitNearXEnd - UnitNearXStart + 1);
            if (UnitCellsPerPlayer != geometryCells)
                throw new System.InvalidOperationException(
                    $"战场容量配置 {UnitCellsPerPlayer} 与单位区几何 {geometryCells}（4+5 两行）不一致——" +
                    "须同步改 ValueSystemConfig.GameRules.BattlefieldCapacityPerPlayer");
            var cells = new (int x, int z)[UnitCellsPerPlayer];
            int i = 0;
            foreach (int z in new[] { farZ, nearZ })
            {
                int x0 = z == farZ ? UnitFarXStart : UnitNearXStart;
                int x1 = z == farZ ? UnitFarXEnd : UnitNearXEnd;
                for (int x = x0; x <= x1; x++)
                    cells[i++] = (x, z);
            }
            return cells;
        }

        /// <summary>P1 地牌格序列：玩家自身视角左→右（x 递增）x2..x5 共 4 格（一元素一格，
        /// 万能 (1,0) 占行首、卡组 (6,0) 占行尾）。</summary>
        private static (int x, int z)[] BuildLandCells()
        {
            var cells = new (int x, int z)[LandCellsPerPlayer];
            for (int i = 0; i < LandCellsPerPlayer; i++)
                cells[i] = (LandBandStartX + i, P1LandZ);
            return cells;
        }

        /// <summary>P2 格序列 = P1 序列的对座镜像像（自身视角左→右自动成立）。</summary>
        private static (int x, int z)[] MirrorRow((int x, int z)[] source)
        {
            var cells = new (int x, int z)[source.Length];
            for (int i = 0; i < source.Length; i++)
                cells[i] = MirrorSeat(source[i].x, source[i].z);
            return cells;
        }
    }
}
