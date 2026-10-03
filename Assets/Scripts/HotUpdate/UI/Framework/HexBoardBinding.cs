using System.Text;
using GameBoard;
using HexMap;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace SynergyUI
{
    /// <summary>
    /// 战场实际格子 ↔ 代码抽象区域绑定层（2026-10-03）。
    ///
    /// 坐标同构（零换算）：BoardMath 逻辑棋盘 13×8 odd-r 与 HexMap offset 坐标系按约定完全一致
    /// （邻居向量/轴换算逐行相同），故 逻辑格(x,z) = 地图格 − 棋盘矩形原点，无需旋转/翻折。
    /// 职责：逻辑格 → 3D 格实体（HexCellData）与世界坐标的查询；装台自检（平整/纯色/区格全解析）
    /// 供 3D 层（放卡/标记/选格）消费。占用语义仍在 BoardState（派生层单向读核心区列表），
    /// 本类不持有卡牌数据——绑定的是「格子」，不是「占用」。
    /// 生命周期：BattleStageDirector 装台完成时 Capture 置 Current，拆台时清空。
    /// </summary>
    public sealed class HexBoardBinding
    {
        /// <summary>当前活动绑定（装台后非空；世界地图局/未装台为 null）。</summary>
        public static HexBoardBinding Current { get; internal set; }

        private readonly World _world;

        /// <summary>棋盘矩形（地图 offset 坐标系，含边界）。</summary>
        public int2 BoardMin { get; }
        public int2 BoardMax { get; }

        /// <summary>棋盘格统一贴图数组层（纯色层）。</summary>
        public int BoardTerrainIndex { get; }

        public int Width => BoardMax.x - BoardMin.x + 1;
        public int Height => BoardMax.y - BoardMin.y + 1;

        private EntityManager Em => _world.EntityManager;
        private HexChunkStreamingSystem Streaming =>
            _world != null && _world.IsCreated ? _world.GetExistingSystemManaged<HexChunkStreamingSystem>() : null;

        private HexBoardBinding(World world, int2 min, int2 max, int terrainIndex)
        {
            _world = world;
            BoardMin = min;
            BoardMax = max;
            BoardTerrainIndex = terrainIndex;
        }

        /// <summary>从活动 world + HexMapRuntime 快照建绑定；未装台/世界未就绪返回 null。</summary>
        public static HexBoardBinding Capture()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated || !HexMapRuntime.HasBoard)
                return null;
            if (world.GetExistingSystemManaged<HexChunkStreamingSystem>() == null)
                return null;
            return new HexBoardBinding(world, HexMapRuntime.BoardRectMin, HexMapRuntime.BoardRectMax,
                HexMapRuntime.BoardTerrainIndex);
        }

        /// <summary>逻辑格 → 地图 offset 格。</summary>
        public int2 MapOffsetOf(int logicalX, int logicalZ) => new int2(logicalX + BoardMin.x, logicalZ + BoardMin.y);

        /// <summary>地图 offset 格 → 逻辑格（不在矩形内返回 false）。</summary>
        public bool TryGetLogical(int2 mapOffset, out int2 logical)
        {
            if (math.all(mapOffset >= BoardMin) && math.all(mapOffset <= BoardMax))
            {
                logical = mapOffset - BoardMin;
                return true;
            }
            logical = default;
            return false;
        }

        /// <summary>逻辑格 → 3D 格实体（格未生成/未装载返回 false）。</summary>
        public bool TryGetCell(int logicalX, int logicalZ, out Entity cell)
        {
            var streaming = Streaming;
            if (streaming == null || !streaming.CellLookup.IsCreated)
            {
                cell = Entity.Null;
                return false;
            }
            return streaming.CellLookup.TryGetValue(MapOffsetOf(logicalX, logicalZ), out cell);
        }

        /// <summary>逻辑格世界坐标（板面中心；格不存在返回 Vector3.zero——调用方先 TryGetCell）。</summary>
        public Vector3 WorldPositionOf(int logicalX, int logicalZ)
        {
            return TryGetCell(logicalX, logicalZ, out var e)
                ? (Vector3)Em.GetComponentData<HexCellData>(e).Position
                : Vector3.zero;
        }

        /// <summary>棋盘全部格子是否已生成（矩形 × CellLookup）。</summary>
        public bool AllCellsReady
        {
            get
            {
                for (int z = 0; z < Height; z++)
                    for (int x = 0; x < Width; x++)
                        if (!TryGetCell(x, z, out _))
                            return false;
                return true;
            }
        }

        /// <summary>装台自检（数据层证据）：矩形 104 格全部就绪/平整/纯色层，
        /// 双方单位区(2×9)/地牌行(9)/角色格/发动格 100% 可解析。返回是否通过，明细走日志。</summary>
        public bool SelfCheck()
        {
            var sb = new StringBuilder("[HexBoardBinding]");
            bool shapeOk = Width == BoardLayout.Width && Height == BoardLayout.Height;
            sb.Append($" 矩形={Width}×{Height}（逻辑棋盘 {BoardLayout.Width}×{BoardLayout.Height} {(shapeOk ? "一致" : "不一致!")}）");

            int total = 0, missing = 0, notFlat = 0, wrongTerrain = 0;
            for (int z = 0; z < Height; z++)
            {
                for (int x = 0; x < Width; x++)
                {
                    total++;
                    if (!TryGetCell(x, z, out var e))
                    {
                        missing++;
                        continue;
                    }
                    var cd = Em.GetComponentData<HexCellData>(e);
                    if (cd.Elevation != 0) notFlat++;
                    if (cd.TerrainIndex != BoardTerrainIndex) wrongTerrain++;
                }
            }
            sb.Append($" 格子={total} 缺失={missing} 不平整={notFlat} 非纯色层={wrongTerrain}");

            int unitMissing = MissingOf(BoardLayout.UnitCells(0)) + MissingOf(BoardLayout.UnitCells(1));
            int landMissing = MissingOf(BoardLayout.LandCells(0)) + MissingOf(BoardLayout.LandCells(1));
            int specialMissing = 0;
            for (int p = 0; p < 2; p++)
            {
                var (cx, cz) = BoardLayout.CharacterCell(p);
                var (ax, az) = BoardLayout.ActivationCell(p);
                if (!TryGetCell(cx, cz, out _)) specialMissing++;
                if (!TryGetCell(ax, az, out _)) specialMissing++;
            }
            sb.Append($" 单位区缺={unitMissing}/36 地牌行缺={landMissing}/18 特殊格缺={specialMissing}/4");

            bool ok = shapeOk && missing == 0 && notFlat == 0 && wrongTerrain == 0
                      && unitMissing == 0 && landMissing == 0 && specialMissing == 0;
            Debug.Log(ok ? $"[PASS]{sb}" : $"[FAIL]{sb}");
            return ok;
        }

        private int MissingOf(System.Collections.Generic.IReadOnlyList<(int x, int z)> cells)
        {
            int n = 0;
            foreach (var (x, z) in cells)
                if (!TryGetCell(x, z, out _))
                    n++;
            return n;
        }
    }
}
