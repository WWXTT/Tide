using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

namespace HexMap
{
    // ── cell 级标签（gameplay 可查询） ──────────────────────────────

    /// <summary>河床/河岸 cell。河岸 = 加宽后并入的侧翼格（水浅/可涉水，gameplay 区分用）</summary>
    public struct HexRiverCell : IComponentData
    {
        public int RiverId;   // 所属河流序号（合并河段取下游 id）
        public float WaterY;  // 该格水面世界 y（板面 y − ε，含扰动）
        public byte IsBank;   // 1=加宽侧翼格
    }

    /// <summary>湖泊 cell</summary>
    public struct HexLakeCell : IComponentData
    {
        public int LakeId;
        public float WaterY;
    }

    /// <summary>道路 cell（路径格，缎带穿过其板面）</summary>
    public struct HexRoadCell : IComponentData
    {
        public int RoadId;
    }

    /// <summary>植被实例标签（重生成时整批销毁）。零尺寸 struct 即 tag。</summary>
    public struct HexScatterInstance : IComponentData { }

    /// <summary>植被实例所属格（未来「编辑格植被局部刷新」按格销毁用）</summary>
    public struct HexScatterCell : IComponentData
    {
        public int2 Offset;
    }

    /// <summary>植被原型分组（按规则销毁/过滤）</summary>
    public struct HexScatterPrototype : ISharedComponentData
    {
        public int PrototypeIndex;
    }

    /// <summary>水体网格渲染实体标签（挂 MeshReference）</summary>
    public struct HexWaterBody : IComponentData
    {
        public int BodyId;
    }

    /// <summary>道路缎带网格渲染实体标签（挂 MeshReference）</summary>
    public struct HexRoadMesh : IComponentData
    {
        public int RoadId;
    }

    // ── 地图级单例（托管，主线程专用——地图小，不进 Burst/blob） ──────

    /// <summary>
    /// 特征配置单例（Install 时由 authoring 写入；全是主线程只读引用）。
    /// HexMapConfig（struct blob）不动——特征配置全在托管单例，避免 blob 反复扩字段。
    /// </summary>
    public class HexFeatureConfig : IComponentData
    {
        public HexMapFeatureSettings Settings;   // ScriptableObject 直引用
        public UnityEngine.Material WaterMaterial;
        public UnityEngine.Material RoadMaterial;
        public bool EnableRivers = true;
        public bool EnableRoads = true;
        public bool EnableVegetation = true;
    }

    /// <summary>重生成请求（挂到 HexMapConfig 实体上；菜单/放置工具写入，特征系统消费后移除）</summary>
    public struct HexFeatureRegenerateRequest : IComponentData
    {
        public bool ResetElevation;   // true=高程也重置回噪声值（完整管线）；false=保留手编地形只重跑特征
    }

    // ── 生成结果数据（HexFeatureState 内的载荷类型） ─────────────────

    public enum RiverEndKind { Sea, Lake, DryUp, Joined }

    public class RiverPath
    {
        public List<int2> Cells;      // 含泉眼在内的完整河床格序列
        public List<float> WaterY;    // 与 Cells 等长，单调不升
        public RiverEndKind End;
        public int RiverId;
        /// <summary>加宽侧翼格（IsBank=1，高程已对齐河床）——overlay 恢复标签用</summary>
        public List<int2> BankCells;
        public List<float> BankWaterY;
    }

    public class LakeData
    {
        public List<int2> Cells;      // 全部淹没格（Level<L 水下，==L 岸边齐水线）
        public int2 SpillCell;        // 溢流格（河从这出湖；封闭湖 = 无效值）
        public float WaterY;
        public int Level;             // 定稿水位（高程台阶数）
        public int LakeId;
    }

    public class RoadPath
    {
        public int FromPoi, ToPoi;    // 连接的两个 RoadNode POI 索引
        public List<int2> Cells;      // 有序 cell 路径
        public List<int> Elevations;  // 整平后剖面（与 Cells 等长）
        public int RoadId;
    }

    /// <summary>
    /// 特征运行状态（生成结果）：供 gizmo / 网格构建 / overlay 套用 / 游戏逻辑查询。
    /// 生成系统独占写入，其余系统只读。
    /// </summary>
    public class HexFeatureState : IComponentData
    {
        /// <summary>雕刻后高程（overlay 对已卸载重建的 cell 幂等补挂）</summary>
        public Dictionary<int2, int> ElevationOverrides = new Dictionary<int2, int>();

        public List<RiverPath> Rivers = new List<RiverPath>();
        public List<LakeData> Lakes = new List<LakeData>();
        public List<RoadPath> Roads = new List<RoadPath>();

        /// <summary>多源 BFS 距离图（河/湖格一起作河源；植被过滤用）</summary>
        public Dictionary<int2, int> RiverDist = new Dictionary<int2, int>();
        public Dictionary<int2, int> RoadDist = new Dictionary<int2, int>();

        /// <summary>水体+道路网格实体（重生成时销毁重建）</summary>
        public List<Entity> FeatureMeshEntities = new List<Entity>();

        /// <summary>overlay 幂等标记：已套用覆写的 cell（卸载时移除，重建后重套）。
        /// 用户手编后 Applied 已标记 → 不会被回滚，直到该 cell 卸载重建。</summary>
        public HashSet<int2> OverlayApplied = new HashSet<int2>();

        public bool VegetationDirty;

        /// <summary>生成代数（每次 GenerateFeatures 完成自增）——编辑器等待生成完成的信号</summary>
        public int GenerationSerial;
    }
}
