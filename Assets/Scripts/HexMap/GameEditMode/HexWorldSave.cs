using System;
using Unity.Mathematics;

namespace HexMap
{
    // ── 存档 DTO（JsonUtility 口径）────────────────────────────────
    //
    // 运行态的 RiverPath/LakeData/RoadPath 是普通 class（无 [Serializable]）且含
    // List<int2>（Unity.Mathematics 结构，JsonUtility 不可靠）→ 必须经本 DTO 层转储。
    // 约定：enum 显式转 int；int2/float3 拆分量或用 HexCellCoordDto；
    // Dictionary（ElevationOverrides/RiverDist/RoadDist/OverlayApplied）一律重算不存。

    /// <summary>int2 的 JsonUtility 安全替身（offset 坐标）</summary>
    [Serializable]
    public struct HexCellCoordDto
    {
        public int x, z;

        public static HexCellCoordDto Of(int2 o) => new HexCellCoordDto { x = o.x, z = o.y };
        public int2 ToInt2() => new int2(x, z);
    }

    /// <summary>世界存档：地形全量 + POI + 特征生成结果 + 配置指纹</summary>
    [Serializable]
    public class HexWorldSave
    {
        /// <summary>存档格式版本（当前 1；不符拒载）</summary>
        public int version = 1;

        public int cellCountX;
        public int cellCountZ;
        /// <summary>生成配置指纹（8 位 hex，HexWorldFingerprint.Compute；漂移仅警告不拒载）</summary>
        public string settingsFingerprint = "";
        public string name = "";
        public long savedAtUnixSeconds;

        /// <summary>全图高程（row-major：index = z * cellCountX + x）</summary>
        public int[] elevations;
        /// <summary>全图地块索引（同布局）</summary>
        public int[] terrainIndices;

        public HexPoiDto[] pois;
        public HexRiverDto[] rivers;
        public HexLakeDto[] lakes;
        public HexRoadDto[] roads;
    }

    [Serializable]
    public struct HexPoiDto
    {
        public int type;                 // (int)PoiType
        public int cellX, cellZ;
        public float worldX, worldY, worldZ;
        public float radius;
        public string note;

        public static HexPoiDto Of(in HexPoiData p) => new HexPoiDto
        {
            type = (int)p.Type,
            cellX = p.CellOffset.x,
            cellZ = p.CellOffset.y,
            worldX = p.WorldPos.x,
            worldY = p.WorldPos.y,
            worldZ = p.WorldPos.z,
            radius = p.Radius,
            note = p.Note ?? "",
        };

        public HexPoiData ToData() => new HexPoiData
        {
            Type = (PoiType)type,
            CellOffset = new int2(cellX, cellZ),
            WorldPos = new float3(worldX, worldY, worldZ),
            Radius = radius,
            Note = note ?? "",
        };
    }

    [Serializable]
    public class HexRiverDto
    {
        public int riverId;
        public int endKind;              // (int)RiverEndKind
        public HexCellCoordDto[] cells;  // 与 waterY 等长
        public float[] waterY;
        /// <summary>加宽侧翼格（可为 null——窄河无岸格，转换层判空）</summary>
        public HexCellCoordDto[] bankCells;
        public float[] bankWaterY;
    }

    [Serializable]
    public class HexLakeDto
    {
        public int lakeId;
        public int level;
        public float waterY;
        public int spillX, spillZ;       // 封闭湖 = 生成器留下的无效值，原样存取
        public HexCellCoordDto[] cells;
    }

    [Serializable]
    public class HexRoadDto
    {
        public int roadId;
        public int fromPoi, toPoi;       // RoadNode POI 在 pois 列表中的索引
        public HexCellCoordDto[] cells;
        public int[] elevations;         // 与 cells 等长（整平后剖面）
    }
}
