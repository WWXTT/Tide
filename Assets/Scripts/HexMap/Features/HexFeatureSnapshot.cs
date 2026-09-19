using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HexMap
{
    /// <summary>
    /// 整图特征快照（计划 1.2）：特征算法不直接改 ECS，先做整图快照 →
    /// 河流/湖泊/道路在快照上行走、雕刻、整平 → 提交时逐 cell 比对一次性写回。
    /// 被丢弃的河流天然回滚（恢复 Snap = Elev，不提交）；脏标记只在提交时发生一次。
    /// 地图仅数百 cell，全部用稠密数组。
    /// </summary>
    public class HexFeatureSnapshot
    {
        public int2 CellCount;
        public int MaxElevation;
        public float ElevationStep;

        /// <summary>原始高程（提交比对/丢弃回滚基准）</summary>
        public int[,] Elev;
        /// <summary>工作副本（算法雕刻后的高程）</summary>
        public int[,] Snap;
        /// <summary>原始地形索引</summary>
        public int[,] Terrain;
        /// <summary>板面 y − elev×Step（每格常数：噪声 y 扰动项；carve 后推算新板面用）</summary>
        public float[,] PerturbY;
        /// <summary>cell 世界中心 xz（与 HexChunkStreamingSystem.CreateCell 同式，注意 y/2 整数除法）</summary>
        public float2[,] Center;

        public bool[,] RiverBed;
        public bool[,] RiverBank;
        public bool[,] LakeCell;
        public bool[,] RoadCell;
        /// <summary>河床格所属河流序号，-1 = 非河床</summary>
        public int[,] RiverIdOf;
        /// <summary>湖格所属湖序号，-1 = 非湖</summary>
        public int[,] LakeIdOf;
        /// <summary>道路格所属道路序号，-1 = 非道路</summary>
        public int[,] RoadIdOf;
        /// <summary>河床/河岸格水面 y（float.NaN = 未设）</summary>
        public float[,] RiverWaterY;
        /// <summary>湖格水面 y（float.NaN = 未设）</summary>
        public float[,] LakeWaterY;

        public bool InBounds(int2 o)
            => o.x >= 0 && o.x < CellCount.x && o.y >= 0 && o.y < CellCount.y;

        public int GetElev(int2 o) => Snap[o.x, o.y];

        /// <summary>指定高程下该格的板面世界 y（同 ElevationToY 公式的增量形式）</summary>
        public float PlateY(int2 o, int elevation) => elevation * ElevationStep + PerturbY[o.x, o.y];

        public static HexFeatureSnapshot Build(EntityManager em,
            NativeHashMap<int2, Entity> lookup, ref HexMapConfigBlob blob)
        {
            int w = blob.CellCount.x, h = blob.CellCount.y;
            var s = new HexFeatureSnapshot
            {
                CellCount = blob.CellCount,
                MaxElevation = blob.MaxElevation,
                ElevationStep = blob.ElevationStep,
                Elev = new int[w, h],
                Snap = new int[w, h],
                Terrain = new int[w, h],
                PerturbY = new float[w, h],
                Center = new float2[w, h],
                RiverBed = new bool[w, h],
                RiverBank = new bool[w, h],
                LakeCell = new bool[w, h],
                RoadCell = new bool[w, h],
                RiverIdOf = new int[w, h],
                LakeIdOf = new int[w, h],
                RoadIdOf = new int[w, h],
                RiverWaterY = new float[w, h],
                LakeWaterY = new float[w, h],
            };

            for (int z = 0; z < h; z++)
            {
                for (int x = 0; x < w; x++)
                {
                    var cell = em.GetComponentData<HexCellData>(lookup[new int2(x, z)]);
                    s.Elev[x, z] = cell.Elevation;
                    s.Snap[x, z] = cell.Elevation;
                    s.Terrain[x, z] = cell.TerrainIndex;
                    s.PerturbY[x, z] = cell.Position.y - cell.Elevation * blob.ElevationStep;
                    // 与 HexChunkStreamingSystem.CreateCell 逐位一致：offset.y/2 是整数除法
                    s.Center[x, z] = new float2(
                        (x + z * 0.5f - z / 2) * (blob.InnerRadius * 2f),
                        z * (blob.OuterRadius * 1.5f));
                    s.RiverIdOf[x, z] = -1;
                    s.LakeIdOf[x, z] = -1;
                    s.RoadIdOf[x, z] = -1;
                    s.RiverWaterY[x, z] = float.NaN;
                    s.LakeWaterY[x, z] = float.NaN;
                }
            }
            return s;
        }
    }
}
