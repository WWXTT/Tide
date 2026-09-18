# HexMap 特征生成实施计划：河流 / 道路 / 植被散布 / 编辑器工具 / 美术迁移

https://github.com/Matteotti/Hex-Map-Project
> 目标：在现有六边形 ECS 地形上补齐 MapMagic2 风格的世界生成特征（河流+水面、道路+缎带网格、
> 植被 ECS 实例化、POI 放置与重生成工具、美术资产自包含迁移），随后删除 MapMagic 插件。
> 约束：**只借鉴技术思路，零引用 MapMagic 代码**；资产文件按普通资源复制（保留 .meta/GUID）。
> 河流与道路 = **cell 序列**，直接**修改 cell 整数高程**（走现有脏管线重建网格）。

---

## 0. 现状事实核对（已读源码核实，实施前不必重查）

| 事实 | 出处 |
|---|---|
| Cell = ECS 实体，`HexCellData{Coordinates(axial), Position(world,y含扰动), Elevation(int), TerrainIndex}`；`-1` 为未生成哨兵 | `Assets/Scripts/HexMap/Components/HexCellData.cs:10` |
| 邻居 buffer `Neighbors`（6 路，按 HexDirection 序号），`CellDirty` 为 enableable 组件 | `Components/Neighbors.cs`、`HexChunkStreamingSystem.OnCreate` archetype |
| Cell 世界坐标：`x=(ox+odd*0.5)*2*IR, z=oz*1.5*OR`；常量 OR=10, IR=8.660, SolidFactor=0.8, ElevationStep=3 | `HexChunkStreamingSystem.cs:127-129`、`HexMapAuthoring.cs:63-66` |
| 地形生成：噪声 blob 采样，`elevation=round(noise.w*MaxElevation)`，边界圈恒 0；生成后写回 Position.y 并标脏 | `HexTerrainGenerationSystem.cs:91-117` |
| 网格：板（逐边内缩凸六边形）/坡（高格内缩 SlopeInset=IR*(1-sf)≈1.73，5×5 网格，行向 lerp）/桥（等高平铺）/角落吸收；顶点 x/z 扰动（`Perturb`，噪声 x/z 通道×幅度，幅度贴边衰减） | `HexMeshJob.Part1.cs:221-276,438-450`、`HexMeshJob.Part2.cs:69-242` |
| 板内缩规则 `GetEdgeInset`：桥=IR*sf=0.8IR；SlopeHigher/Boundary=IR−d；SlopeLower/Streaming=IR | `HexMetricsBlob.cs:117-127` |
| 脏管线：`HexMeshWriteSystem`（PresentationSystemGroup）每帧预算 `MaxMeshBuildsPerFrame` 重建，`RegisterMesh/RegisterMaterial + RenderMeshUtility.AddComponents`，`MeshReference` 托管组件复用 Mesh | `HexMeshWriteSystem.cs:40-226` |
| 编辑：射线步进 `PickCell`（步长 0.5）拾取 → `ApplyElevation`（clamp 0..Max，重采样噪声算 y）→ `MarkCellAndNeighborsDirty` | `HexMapEditingSystem.cs:113-204` |
| Authoring 模式：可序列化 struct（如 `HexMeshRewriteSettings`）→ `HexMapConfigBuilder.Build` 灌 blob；Install()/Baker 双路径 | `HexMapAuthoring.cs:28-151,159-308` |
| 贴图数组：`TextureArrayGeneratorEditor` 接受任意 `Texture2D`（**TIFF 可直接用**，内部缩放输出 PNG 再建 `.texture2darray`，含 sRGB/法线/绿反等按类型导入设置） | `Assets/Editor/TextureTools/Other/TextureArrayGeneratorEditor.cs:214-330` |
| 场景实况：`cellCountX=10, cellCountZ=20, maxElevation=10`（任务书写的 6 是旧值） | `Assets/Scenes/*.unity` 中 HexMapAuthoring 字段 |
| 现有验证菜单名：`Tools/验证HexMap网格`（`HexMeshVerifier`，重合顶点法线夹角 ≤2° 等水密检查） | `Assets/Editor/检查/HexMeshVerifier.cs:23` |
| 贴图数组产物目录 `Assets/UI/HexMap/_Materials/GeneratedTextures`；shader 在 `Assets/UI/HexMap/Shaders/`（HexTerrain 自定义三平面） | 目录列表 |
| MapMagic 演示资产（待复制源）：LandTextures 11 反照率+7 法线 TIFF；Trees/Birch(10 FBX+10 prefab+Birch.mat+3 贴图)、Trees/Pine；Stones(3 prefab+Meshes/Textures/Materials)；Grass(Meshes/Textures) | `Assets/MapMagic/Demo/` 目录列表 |
| 流式：loadRadius 15 / unloadRadius 30，地图 10×20 → 进 Play 后数帧内全图加载完成；卸载仅当超出 unload 矩形 | `HexChunkStreamingSystem.cs:76-95`、`HexMapAuthoring` 默认值 |

---

## 1. 总体架构

### 1.1 生成管线与系统排序

```
SimulationSystemGroup（既有顺序不变）:
  HexChunkStreamingSystem          创建/销毁 cell 实体
  → HexTerrainGenerationSystem     新 cell 高程（噪声）
  → 【新】HexFeatureOverlaySystem  流式回归的 cell 重新套用特征覆写（高程/标签）
  → 【新】HexFeatureGenerationSystem  等全图就绪 → 河流→湖泊→道路→特征网格→触发植被
  → 【新】HexVegetationSpawnSystem 预算化批量生成植被实体
PresentationSystemGroup（既有）:
  HexMeshWriteSystem               脏 cell 重建（特征只负责标脏，不碰网格代码）
```

关键决策——**特征一次性整图生成，而非逐 chunk**：
- 河流/道路需要全图高程视野；当前地图仅 200 cell，loadRadius=15 覆盖全图，进 Play 几帧内全部就绪。
- `HexFeatureGenerationSystem` 每帧检查：`streaming.CellLookup` 数量 == `CellCount.x*CellCount.y` 且无
  `TerrainPending` 实体 → 执行一次生成，随后 `Enabled=false` 等待下次请求。
- 若 loadRadius 小于地图（用户改配置），生成挂起并每秒警告一次；**重生成菜单**会调用新增的
  `HexChunkStreamingSystem.EnsureAllLoaded()`（公共方法：无视半径/预算，同步补建全图 cell）。
- 流式卸载往返（大地图未来场景）：cell 被销毁重建后高程回到噪声值、特征标签丢失 →
  `HexFeatureOverlaySystem` 用特征状态里的 `ElevationOverrides/riverCell/roadCell/lakeCell` 表幂等补挂
  （按 offset 每帧对账，只在 cell 刚创建时套用一次，用户后续手编不被覆盖，见 2.2）。

### 1.2 生成快照架构（重要）

特征算法不直接改 ECS，先做**整图快照 → 算法 → 一次性提交**：

```
提交前: int[,] snapElev (从所有 cell 读), bool[,] riverBed/riverBank/lakeCell, ...
河流/湖泊/道路全部在快照上行走、雕刻、整平（含被丢弃河流的回滚 = 不提交）
提交: 逐 cell 比对快照 vs 原值 → HexMapCellEditUtil.ApplyElevation(...)（复用编辑系统的公式）
      + 打 HexRiverCell/HexRoadCell/HexLakeCell 标签 + MarkCellAndNeighborsDirty
```

好处：被 `minLength` 拒绝的河流天然回滚；Dijkstra/DP 在稳定数据上运行；脏标记只在提交时发生一次。

### 1.3 新增文件总表

| 文件 | 内容 |
|---|---|
| `Assets/Scripts/HexMap/HexMapCellEditUtil.cs` | 静态工具：`ApplyElevation` / `MarkCellAndNeighborsDirty`（自 HexMapEditingSystem 提取） |
| `Assets/Scripts/HexMap/HexTerrainHeightSampler.cs` | 世界坐标→地表 y 的解析采样器（板/坡/桥分类 + 扰动逆解） |
| `Assets/Scripts/HexMap/HexMapTerrainMath.cs` | 噪声→高程公式提取（`ElevationFromNoise`，与 TerrainGenerationJob 共用）+ 轴距 `HexDistance` |
| `Assets/Scripts/HexMap/Components/HexFeatureComponents.cs` | `HexRiverCell / HexRoadCell / HexLakeCell / HexScatterInstance / HexScatterPrototype(IShared) / HexWaterBody / HexRoadMesh / HexFeatureConfig / HexFeatureRegenerateRequest` |
| `Assets/Scripts/HexMap/Authoring/HexMapFeatureSettings.cs` | 特征配置 ScriptableObject（含 POI 列表、散布规则、材质引用） |
| `Assets/Scripts/HexMap/Systems/HexFeatureOverlaySystem.cs` | 流式回归套用 |
| `Assets/Scripts/HexMap/Systems/HexFeatureGenerationSystem.cs` | 编排：快照→河流→湖泊→道路→提交→水面/道路网格→植被请求 |
| `Assets/Scripts/HexMap/Features/HexRiverGenerator.cs` | 河流行走 + 湖泊泛洪（纯 C# 静态类，输入快照输出路径） |
| `Assets/Scripts/HexMap/Features/HexRoadGenerator.cs` | 连接选择 + Dijkstra + DP 整平 |
| `Assets/Scripts/HexMap/Features/HexWaterMeshBuilder.cs` | 河流/湖泊水面网格构建 |
| `Assets/Scripts/HexMap/Features/HexRoadMeshBuilder.cs` | 道路缎带网格构建 |
| `Assets/Scripts/HexMap/Systems/HexVegetationSpawnSystem.cs` | 散布 + ECS 实例化 |
| `Assets/UI/HexMap/Shaders/HexWater.shader` | 简单透明水面 |
| `Assets/Editor/HexMap/HexMapPoiPlacementTool.cs` | Scene 视图 POI 放置模式 |
| `Assets/Editor/HexMap/HexMapFeatureGizmos.cs` | POI/河/路/植被计数 gizmo 组件 |
| `Assets/Editor/HexMap/HexMapFeatureMenus.cs` | `Tools/HexMap/重新生成地形特征` 等菜单 |
| `Assets/Art/HexMap/**` | 迁移美术资产（见第 8 节） |

### 1.4 确定性

- 所有随机量走 `Unity.Mathematics.Random`（seed 由 `math.hash(坐标/序号, featureSeed)` 派生），
  禁用 `System.Random`（噪声采样窗口沿用 blob 内既有 origin，不新增）。
- 泉眼/POI 排序用 (elevation desc, offset 字典序)；Dijkstra 平手按 offset 字典序破平。
- 验证手段：重生成两次 → 对 `HexFeatureState` 求哈希（路径 cell 序列 + 高程覆写表拼接哈希），必须相等。

---

## 2. 数据模型：组件 / 单例 / 配置

### 2.1 cell 级标签组件（gameplay 可查询）

```csharp
// Assets/Scripts/HexMap/Components/HexFeatureComponents.cs
/// 河床/河岸 cell（河岸 = 加宽后并入的侧翼格）
public struct HexRiverCell : IComponentData {
    public int RiverId;      // 所属河流序号（合并河段取下游 id）
    public float WaterY;     // 该格水面世界 y（板面 y − ε，含扰动）
    public byte IsBank;      // 1=加宽侧翼格（水浅/可涉水，gameplay 区分用）
}
/// 湖泊 cell
public struct HexLakeCell : IComponentData { public int LakeId; public float WaterY; }
/// 道路 cell（路径格，缎带穿过其板面）
public struct HexRoadCell : IComponentData { public int RoadId; }
/// 植被实例标签（重生成时整批销毁）
public struct HexScatterInstance : ITagComponentData { }
/// 植被原型分组（按规则销毁/过滤）
public struct HexScatterPrototype : ISharedComponentData { public int PrototypeIndex; }
/// 水体/道路网格渲染实体标签
public struct HexWaterBody : IComponentData { public int BodyId; }   // 挂 MeshReference
public struct HexRoadMesh  : IComponentData { public int RoadId;  }  // 挂 MeshReference
```

### 2.2 地图级单例（托管，主线程专用——地图小，不进 Burst/blob）

```csharp
/// 特征配置单例（Install 时由 authoring 写入；全是主线程只读引用）
public class HexFeatureConfig : IComponentData {
    public HexMapFeatureSettings Settings;   // ScriptableObject 直引用
    public Material WaterMaterial;
    public Material RoadMaterial;
    public bool EnableRivers, EnableRoads, EnableVegetation;
}
/// 重生成请求（tag，挂到 HexMapConfig 实体上；菜单/放置工具写入，特征系统消费后移除）
public struct HexFeatureRegenerateRequest : IComponentData { public bool ResetElevation; }

/// 特特征运行状态（生成结果，供 gizmo/网格构建/overlay 套用/游戏逻辑查询）
public class HexFeatureState : IComponentData {
    public Dictionary<int2,int> ElevationOverrides;      // 雕刻后高程（overlay 幂等套用）
    public List<RiverPath> Rivers;                       // 含 WaterY 序列
    public List<LakeData> Lakes;
    public List<RoadPath> Roads;
    public Dictionary<int2,int> RiverDist, RoadDist;     // 多源 BFS 距离图（植被过滤用）
    public List<Entity> FeatureMeshEntities;             // 水体+道路网格实体（重生成销毁）
    public HashSet<int2> OverlayApplied;                 // overlay 幂等标记（cell 卸载时移除）
    public bool VegetationDirty;
}
public class RiverPath { public List<int2> Cells; public List<float> WaterY; public RiverEndKind End; }
public class LakeData  { public List<int2> Cells; public int2 SpillCell; public float WaterY; public int Level; }
public class RoadPath  { public int FromPoi, ToPoi; public List<int2> Cells; public List<int> Elevations; }
public enum RiverEndKind { Sea, Lake, DryUp, Joined }
```

`OverlayApplied` 对账规则：每帧遍历 `ElevationOverrides` 键——cell 不存在（已卸载）→ 从 Applied 移除；
cell 存在、Elevation≥0、未 Applied → 套用高程+标签+标脏、加入 Applied。**用户手编之后不会被回滚**
（Applied 已标记），直到该 cell 卸载重建。

### 2.3 配置：HexMapFeatureSettings（ScriptableObject）

放 ScriptableObject 而非直接挂 HexMapAuthoring 字段的原因：**POI 在 Play 模式下放置**
（只有 Play 中才有 cell 实体可拾取），场景组件在 Play 中的修改退出即还原，资产修改可持久。

```csharp
// Assets/Scripts/HexMap/Authoring/HexMapFeatureSettings.cs
[CreateAssetMenu(menuName="HexMap/Feature Settings")]
public class HexMapFeatureSettings : ScriptableObject {
    public uint featureSeed = 0x51VE2u;

    [Header("河流")] public HexRiverSettings rivers = HexRiverSettings.Default;
    [Header("道路")] public HexRoadSettings  roads  = HexRoadSettings.Default;
    [Header("植被")] public List<HexScatterRule> scatterRules = new();

    [Header("POI（放置模式编辑）")] public List<HexPoiData> pois = new();
    public Material waterMaterial;   // 缺省时用 HexWater.mat
    public Material roadMaterial;    // 缺省时用 HexRoad.mat
}

[Serializable] public struct HexPoiData {
    public PoiType Type;        // RiverSpring / RoadNode
    public int2 CellOffset;     // 主数据（拾取时写入）
    public float3 WorldPos;     // 辅助显示/半径盘参考
    public float Radius;        // gizmo 半径盘；泉水=水源范围，路点=端点缓冲
    public string Note;
}
public enum PoiType { RiverSpring, RoadNode }

[Serializable] public struct HexRiverSettings {
    public int springNoiseThresholdElevation; // 泉眼最小高程（默认 ceil(MaxElevation*0.7)）
    public int springMinSpacing;              // 泉眼最小间距（cell，默认 4）
    public int maxSprings;                    // 程序泉眼上限（默认 3）
    public int initialCarveAllowance;         // 起步挖掘预算（高程步，默认 1）
    public int maxCarveAllowance;             // 成熟河挖掘预算（默认 3）
    public float widthGrowPerStep;            // 每步宽度增速（默认 0.06）
    public float widthTwoCellThreshold;       // ≥此宽度标记 2 格宽（默认 1.6）
    public float dryUpWidth;                  // < 此宽度干涸（默认 0.5）
    public int minRiverLength;                // < 此长度丢弃（默认 6）
    public int maxLakeCells;                  // 湖泊面积上限（默认 40）
    public int maxElevationDropPerStep;       // 河床相邻格最大落差（默认 1）
    public int riverBedTerrainIndex;          // 河床地形索引，-1=不改（默认 -1）
}
[Serializable] public struct HexRoadSettings {
    public int connectionsPerNode;   // 每节点连接数（默认 2）
    public int maxConnectionsTotal;  // 单节点连接上限（默认 3）
    public float minConnectionAngle; // 同节点两路最小夹角（度，默认 60）
    public float maxHeightCost;      // |Δe| 权重（默认 2）
    public float riverCrossCost;     // 穿河代价（默认 40）
    public float boundaryCost;       // 走边界圈代价（默认 5）
    public float roadHalfWidth;      // 缎带半宽（默认 0.5*InnerRadius）
    public float sampleStep;         // 缎带纵向前进步长（默认 2.0）
    public float uvScale;            // 纵向 UV 密度（默认 0.1 /世界单位）
}
[Serializable] public class HexScatterRule {
    public string name;
    public Mesh mesh; public Material material;       // 原型（mesh+material，非 prefab）
    public List<float> densityPerTerrain;             // 按地形索引的每 cell 期望株数（0=该地形不放）
    public Vector2 scaleRange = new(0.8f, 1.3f);
    public float yOffset = 0f;
    public bool randomYRotation = true;
    public int elevationMin = 0, elevationMax = 99;   // cell 高程过滤
    public int maxNeighborElevationDiff = 2;          // 坡度代理（cell 级）
    public int riverMarginCells = 1, roadMarginCells = 1; // 距河/路最小 BFS 距离
    public float clumpNoiseThreshold = 0f;            // >0 时聚簇噪声过滤
    public float clumpNoiseScale = 1f;
}
```

`HexMapAuthoring` 增量修改：新增 `[SerializeField] HexMapFeatureSettings featureSettings;`
`[SerializeField] bool enableFeatures = true;`。`Install()` 里当 `enableFeatures && featureSettings != null`
时创建 `HexFeatureConfig`/`HexFeatureState` 单例（含 Baker 路径，`DependsOn(featureSettings)`）。
注意：`HexMapConfig`（struct）不动——特征配置全在托管单例，避免 blob 反复扩字段。

---

## 3. 共享基础设施

### 3.1 HexMapCellEditUtil（提取自 HexMapEditingSystem，三处共用）

```csharp
// Assets/Scripts/HexMap/HexMapCellEditUtil.cs
public static class HexMapCellEditUtil {
    /// 与 HexMapEditingSystem.ApplyElevation 完全同公式：clamp 0..MaxElevation，
    /// SampleNoise(当前位置).y → ElevationToY → 写回 + 标脏（cell+6 邻居）
    public static void ApplyElevation(EntityManager em, Entity cellEntity,
        int newElevation, ref HexMapConfigBlob blob);
    public static void MarkCellAndNeighborsDirty(EntityManager em, Entity cellEntity);
    /// 射线步进拾取（提取自 PickCell；编辑系统、POI 放置工具共用）
    public static bool PickCell(Ray ray, EntityManager em, ref HexMapConfigBlob blob,
        in HexMetrics metrics, NativeHashMap<int2, Entity> lookup, out Entity cellEntity, out int2 offset);
}
```

实施时把 `HexMapEditingSystem` 内的对应私有方法改为调用本工具（行为零变化），不是复制。
`PickCell` 增加输出 `offset`（POI 需要）。`HexMapTerrainMath.ElevationFromNoise(ref blob, float3 pos)`
返回 `(int elevation, float y)`，`HexTerrainGenerationSystem` 的 Job 与重生成重置路径共用同一公式
（Job 内改调静态方法，公式漂移 = 重生成结果与初次生成不一致）。
另加 `HexMapTerrainMath.HexDistance(int2 a, int2 b)`（轴向坐标立方距离 (|dx|+|dy|+|dz|)/2，回路防护用）。

### 3.2 HexTerrainHeightSampler（道路缎带 / 植被落点 / 未来玩法拾取共用）

`Assets/Scripts/HexMap/HexTerrainHeightSampler.cs`，纯 float 静态类（无托管引用，可被 Burst 调用；
cell 数据由调用方通过 `in HexCellData` 传入）：

```csharp
public static class HexTerrainHeightSampler {
    /// 世界点 → 地表 y。owner = 点所在名义六边形的 cell（HexCoordinates.FromPosition）。
    /// 精度目标：±0.05（坡带 1.73 宽上可接受；缎带贴地、植被不悬空即达标）
    public static float WorldHeight(float3 worldPos, in HexCellData owner,
        in HexMetrics metrics, ref HexMapConfigBlob blob, HexCellDataSource grid);

    /// 扰动逆解：mesh 顶点是 nominal + Perturb(x/z)；查询点在扰动后空间。
    /// 迭代 2 次：p' = p − PerturbOffset(p')，收敛误差 < 0.01（扰动是平滑噪声场的平移）
    public static float2 Unperturb(float2 xz, ref HexMapConfigBlob blob);

    /// 单边带内高度：点位于 cell 沿方向 d 越过板缘的坡带内。
    /// t = (dist − inset) / bandWidth（垂直边法线方向度量），y = lerp(plateY_top, plateY_bottom, t)
    /// 与 HexMeshJob.BuildSlope 的列向 lerp 差异：列端扰动导致的梯形歪斜，误差 < inset 的横摆幅
    private static float EdgeBandHeight(float3 p, in HexCellData cell, in HexCellData neighbor,
        HexDirection d, in HexMetrics metrics, ref HexMapConfigBlob blob);
}
/// 主线程 cell 数据源（封 NativeHashMap+ComponentLookup；Burst 场景未来再泛型化）
public struct HexCellDataSource {
    public bool TryGetCell(int2 offset, out HexCellData data);   // 越界 → false
    public bool TryGetNeighbor(int2 offset, HexDirection d, out HexCellData data); // 地图外→false
}
```

分类逻辑（与 `ClassifyEdge`/`GetEdgeInset` 同一套规则，**不复制公式、直接调
`HexMetrics.GetEdgeInset` / `GetEdgeNormal` / `GetPlateCorner`**）：

```
WorldHeight(p):
  owner = grid[FromPosition(Unperturb(p.xz))]
  若 owner 缺失（流式洞/图外）→ 返回 owner 板 y 兜底
  对 6 边算溢出量 o_d = dot(p.xz − c.xz, n_d) − inset_d   // inset_d = GetEdgeInset(边类型)
  maxO = max(o_d)；若 maxO ≤ 0 → 点在板内，y = owner 板 y（= Position.y）
  d* = argmax o_d（角落区两邻边任取，两侧带在端点值一致，误差为吸收三角面积级，可忽略）
  按 d* 边类型：
    Bridge（等高）      → y = owner 板 y（桥面即板面高度）
    SlopeHigher（本格高）→ EdgeBandHeight(p, owner, neighbor_d*, d*)   // 坡占本格面积
    SlopeLower（邻居高） → 递归 EdgeBandHeight(p, neighbor_d*, owner, d*.Opposite())
                           // 带在邻居名义六边形内，从邻居视角是 SlopeHigher
    Boundary            → 不进（图内查询不会越界；防御性返回板 y）
```

边界确认（风险 H 中"采样器只需板+坡"）：道路缎带与植被都限制在图内 cell 板 ± 半宽内，
板/桥/坡覆盖了除「角落吸收三角形」外的全部图内表面；角落三角形被端带吸收、与邻带共享端点值，
取任一邻边带即可——**无需实现角落闭合采样**。桥带（等高）面积也由板 y 覆盖。

---

## 4. 河流系统

### 4.1 泉眼选择

```
泉眼 = 手动 POI(RiverSpring) ∪ 程序泉眼：
  程序候选 = { cell | Elevation ≥ springNoiseThresholdElevation
                     且 为 6 邻域严格局部最大（含平顶：无更高邻居）}
  按 (Elevation desc, offset 字典序) 排序，贪心取 ≤ maxSprings 个，
  两两间距（HexDistance）≥ springMinSpacing，且与手动 POI 也保持间距
  全部转 axial 后进 springs 列表（确定性顺序）
```

### 4.2 河流行走（快照上运行）

MapMagic 技术的六边形移植——贪心液滴 + 预算雕刻 + 回路防护：

```
状态: cur=spring, prevDir=null, width=1.0, history=最近 12 格的「到头距离」列表,
      path=[spring], carve 记账进 snapElev
每步:
  1. 候选 = 6 个方向，排除: 图外 / 本河已访问(除非 JoinRiver) /
     回路防护: 候选到 history 各格的 HexDistance < cur 到对应格的距离 + 0 → 跳过
       （简化实现: 对 history 最近 6 格，若 HexDistance(cand, h) ≤ HexDistance(cur, h) 则视为回头）
  2. 剩余候选按与 prevDir 的相对转角排序（0=直行, ±1=60°, ±2=120°, 3=掉头），
     在转角 ≤ maxTurn（默认 2，即 120° 锥；掉头仅当无其他候选）内选【高程最低】者
  3. 选中 cand:
     allowance = round(lerp(initialCarveAllowance, maxCarveAllowance, (width−1)/(widthMax−1)))
     若 snapElev[cand] > snapElev[cur] + allowance:
        若 allowance ≥ snapElev[cand] − snapElev[cur] 且 budget 允许 → 雕刻:
            snapElev[cand] = snapElev[cur]（挖到当前河床）, 记 carved, width 每挖 1 层 −0.15
        否则 → 该候选不可行，取次优候选；全部不可行 → 卡死 → 转湖泊(4.3)
     若 snapElev[cand] < snapElev[cur] − maxElevationDropPerStep:
        河床落差过大 → cand 先被垫高到 cur−1?? 否——自然地形陡降是合理的:
        直接接受（waterY 沿途下降），仅当 cand 为 0 高程 → 到海，结束(Sea)
     若 cand 已是其它河河床:
        JoinRiver 校验: |waterY 差| ≤ 1 个台阶 且 转角 ≤ 2 → 接入:
            合并 path 尾段进下游河（下游 width += 0.3 提升其挖掘能力），结束(Joined)
        校验失败 → 视为不可行候选
  4. 接受 cand: path += cand; 标 riverBed;
     水面约束: waterY[cand] = min(waterY[cur], candPlateY) − 0.1   // 单调不升 + 贴床
     剖面约束: |snapElev[cand] − snapElev[cur]| ≤ maxElevationDropPerStep
        （若自然高差 >1 则把高的一侧向低侧削平到差=1——「阶梯河」，水面瀑布=落差裙边，见 4.5）
     width += widthGrowPerStep − carve 惩罚; clamp [0, 2.2]
     若 width ≥ widthTwoCellThreshold: 加宽——把 A→B 共享边的两侧相邻格
        （A 的 d.Previous() 与 d.Next() 邻居）标 riverBank，snapElev 对齐到河床，waterY 同值
     若 width < dryUpWidth → 结束(DryUp)
  5. 终止: Elevation==0 或到边界圈 → Sea；path.Length > 200 保险丝 → DryUp
验收: path.Length ≥ minRiverLength 且 End != DryUp（DryUp 且长度不足 → 整条丢弃，快照回滚即不提交）
      连续 DryUp 但长度足够 → 保留（末端细流消失，自然）
提交: path 逐格写 ElevationOverrides（若与原高程不同），riverCell 标签数据，Rivers 列表
```

**平地地图退化**（风险 H）：全 0 高程时泉眼集为空（threshold 过滤）→ 无河。若用户仍想要河，
把 `springNoiseThresholdElevation` 设 0：河从平地起步、`initialCarveAllowance` 起步即挖出峡谷河床
（每格下挖 allowance 层直到 0）。剖面单调不升 + clamp ≥0 保证收敛。计划默认值不为此调优，文档标注。

### 4.3 湖泊（卡死时泛洪，快照上运行）

```
从卡死格 start:
  L = snapElev[start]
  loop:
    flooded = BFS(start, 可通过条件: 图内 且 snapElev ≤ L 且 非既有湖格)
    border  = flooded 的邻居中 snapElev > L 的格集合
    if border 为空 → 封闭盆地 → 湖定稿（Level=L）
    B = min{ snapElev[b] | b ∈ border }        // 最低溢流候选
    if B ≤ L + 1:
        溢流点 spill = 该 border 格 → 湖定稿（Level=L），河从 spill 继续行走（返回 4.2，水出湖）
        若 spill 邻接边界圈/Elevation==0 区域 → 直接 Sea 结束
    if flooded.Count ≥ maxLakeCells → 定稿于当前 L（超限湖，不再抬升）
    if flooded 触到既有湖 → 合并: 湖格并入既有湖（取两湖较高水位），河结束(Joined)
    L += 1  // 抬一级水位继续泛洪（增量：从上一轮 flooded 外圈继续，无需重扫）
湖水位 y = (L + 0.5) * ElevationStep
  // 推导: 板面 y ∈ [3L−0.6, 3L+0.6]（扰动 ±0.2×3）；(L+0.5)×3 = 3L+1.5
  // 必然盖住所有 ≤L 的板面，又低于 (L+1) 板面底 3L+2.4 → 不淹高台。湖泊不削地形（水 sitting on plates）
lakeCells = flooded（Level<L 的格子淹没在水下，==L 的格子在岸边齐水线）
```

### 4.4 河床地形（可选）

`riverBedTerrainIndex ≥ 0` 时，河床/河岸 cell 的 `TerrainIndex` 一并改写（如指向沙层），
走与 ApplyElevation 相同的标脏路径。默认 −1 关闭，待第 8 节贴图数组扩层后再开。

### 4.5 水面网格（HexWaterMeshBuilder）

- **河流**：每条河一个合并 Mesh、一个渲染实体（`HexWaterBody` + `MeshReference` +
  `RenderMeshUtility.AddComponents`，注册模式照抄 `HexMeshWriteSystem.RebuildCellMesh`）。
  - 顶面：逐河床格取**板多边形**（`HexMetrics.GetPlateCorner` 按该格实际边类型内缩——与地形板
    逐点同名函数一致，水陆边界天然贴合）抬到该格 `WaterY`；河岸格同法并入。
  - 落差裙边：相邻河床格 `|Δe| == 1` 的共享边上，从高侧水面向低侧水面发竖直四边形（瀑布阶梯面）。
  - 边界裙边：河床格与非河格共享边，若岸格板面低于水面 → 向下发裙边到岸格板 y（防悬空水片）。
- **湖泊**：每湖一实体。顶面 = 全部湖格板多边形 @ 湖水位 y 的并集（逐格发面即可，内部共边
  重复面无碍——透明材质 zwrite off；后续可做 stencil 合并，先不做）；外圈裙边同河流。
- 顶点流：仅需 Position/Normal(+Y)/UV0（UV0 = 世界 xz，shader 里做简单波纹/深度渐变备用）。
- **材质**：`Assets/UI/HexMap/Shaders/HexWater.shader`（新写，URP 语法，参考 HexTerrain 的
  HLSL 包含结构但独立简单）：透明混合、ZWrite Off、Cull Off、基色+世界 UV 微波纹
  （sin 扰动法线可选）、接收阴影关闭。产物材质 `Assets/UI/HexMap/_Materials/HexWater.mat`。
- 重建时机：特征生成/重生成时销毁 `FeatureMeshEntities` 重建；**手动高程编辑不重建水面**
  （已知限制，记录在风险 10.5）。

### 4.6 与脏管线的交互

提交阶段对每个高程被改/地形被改的 cell 调 `HexMapCellEditUtil.ApplyElevation`（内含
`MarkCellAndNeighborsDirty`），`HexMeshWriteSystem` 按既有预算逐帧重建——雕刻出的阶梯
坡面由现有 SlopeHigher/SlopeLower 几何自动表达，**网格代码零改动**。

---

## 5. 道路系统

### 5.1 连接选择（ConnectionFactory 思路简化）

```
输入: RoadNode POI 列表 N（< 2 个 → 跳过道路）
候选对 = 所有两两组合，按 HexDistance 升序（平手按 offset 字典序）
贪心接受一个 (a,b) 若:
  · 连接数[a] < maxConnectionsTotal 且 连接数[b] < maxConnectionsTotal
  · a、b 已有连接的方向与 (a→b) 夹角均 ≥ minConnectionAngle（60°）
  · (a,b) 尚未连接
  直到每个节点连接数 ≥ connectionsPerNode（默认 2）或候选耗尽
每条接受的对 → Dijkstra 寻路；寻路失败（图不连通不可能，cell 图恒连通）→ 放弃该对
```

### 5.2 Dijkstra 寻路（快照 + 河流标记上运行）

```
cost(步进 cur→next) = 1                                  // 长度（六边邻接等长）
                   + maxHeightCost * max(0, |Δe|−1)²     // 陡步重罚（软约束，不硬禁——整平会善后）
                   + maxHeightCost * 0.5 * |Δe|          // 轻度高度变化偏好
                   + riverCell[next] ? riverCrossCost : 0 // 穿河重罚但可行（桥/浅滩）
                   + 边界圈(next) ? boundaryCost : 0      // 避开外圈
终点 POI 格: 在 POI 半径 Radius 内的格 cost 额外 −2（吸向 POI，对应 inside POI 反向用法）
优先队列: 按总代价，平手按 offset 字典序（确定性）
输出: 有序 cell 序列 path
```

无硬性坡度禁令：DP 整平保证最终剖面 |Δe|≤1，搜索期的陡步只反映「要搬多少土」。
若路径穿过河床格 → 记 `crossings`（该格 riverCell=true），缎带在该格抬到水面（5.4）。

### 5.3 高度整平（沿路径 DP）

目标剖面 `e'`：`|e'ᵢ₊₁−e'ᵢ| ≤ 1`，`clamp 0..MaxElevation`，端点锁定 `e'₀=e₀, e'ₙ=eₙ`
（POI 站台不动），**河床格强制 `e'ᵢ = snapElevᵢ`（河流已定稿，不许道路毁河）**，
最小化 `Σ (|e'ᵢ−eᵢ| + λ·|e'ᵢ₊₁−e'ᵢ|)`，λ=0.1（轻微偏好平滑）：

```
dp[i][h] = min over h' ∈ {h−1,h,h+1} of dp[i−1][h'] + |h−eᵢ| + λ|h−h'|
初值: dp[0][e₀]=0 其余 ∞；河床格: 仅 dp[i][snapElevᵢ] 可行
回溯得 e'；写回 snapElev → 提交时 ApplyElevation
复杂度 O(n × MaxElevation × 3)，n≤200 → 微不足道
```

多路交叉：道路按接受顺序串行生成；后生成的路整平时改了先前路的 cell → 先前缎带 y 是
**采样时实算**的（5.4），自动贴新地形，不需回溯（先前路的 |Δe|≤1 剖面可能被破坏——仅影响
视觉平滑，缎带贴地不悬空即可，记录为已知项）。

### 5.4 道路缎带网格（HexRoadMeshBuilder）

每条路一实体（`HexRoadMesh` + `MeshReference` + 渲染注册，同 4.5 模式）：

```
中心线 = path 各格中心（cell.Position.xz）连折线；相邻共线段合并
逐段细分: 沿段按 sampleStep(2.0) 前进，得到采样点序列（含段端点）
每采样点:
  dir   = 该处切向（段内常量；顶点处取两段角平分 miter，转角 >150° 插双点防尖刺）
  left/right = p ± perp(dir) * roadHalfWidth      // 半宽默认 0.5*IR ≈ 4.33
  y = HexTerrainHeightSampler.WorldHeight(点) + 0.08   // 采样器含坡带 → 道路平滑跨坡
  河床格修正: y = max(y, WaterY + 0.1)                // 桥面抬到水面之上（简化桥）
  UV: u = 累计弧长 × uvScale, v = 0/1
网格: 采样点列 × {左,右} 的三角带；顶点流 Position/Normal(采样器返回近似法线: 板=+Y,
      坡=SlopeNormal)/UV0；Normal 简化为 +Y 亦可接受（首版），列 +Y 为准
材质: HexRoad.mat —— URP Lit（或 Danbaidong 等效 Lit，见 8.3），灰色 + 低光滑度，
      预留 _BaseMap 槽（无道路贴图资产，程序条纹可后续在 shader 做，不在本期）
路径格全部标 HexRoadCell{RoadId}（缎带半宽 < 板半宽，路径格即占用格）
```

---

## 6. 植被散布（ECS 实例化）

### 6.1 散布算法（逐 cell 哈希驱动）

```
前置: RiverDist/RoadDist 多源 BFS 距离图（HexFeatureState，河/湖格一起作河源）
对每个图内 cell c（TerrainIndex=t）:
  对每条规则 r（densityPerTerrain.Count > t 且 > 0）:
    期望 λ = densityPerTerrain[t]
    count = floor(λ) + (hash01(c,i=0,seed) < frac(λ) ? 1 : 0)
    对 i in 0..count−1:
      rng = Random(math.hash(uint3(offset, ruleIndex*256+i, featureSeed)))
      极坐标采样: 半径 ρ = sqrt(rng) * 0.75*IR，角 θ 均匀
        // 0.75*IR 圆盘 ⊂ 最小板内缩 0.8*IR 的六边形 → 落点恒在板面，不落坡带
      过滤（任一失败丢弃）:
        elevationMin ≤ c.Elevation ≤ elevationMax
        max(|c.Elev − 邻居Elev|) ≤ maxNeighborElevationDiff     // 坡度代理
        RiverDist[c] ≥ riverMarginCells 且 RoadDist[c] ≥ roadMarginCells
        clumpNoiseThreshold ≤ 0 或
          SampleNoise(pos * clumpNoiseScale).x ≥ clumpNoiseThreshold  // 聚簇
          // 用 .x 通道 + 位置预缩放实现独立频率（.w 已被高程占用）
      生成实例: pos=(采样点x, WorldHeight(板y)+yOffset, z), rotY=rng, scale=lerp(scaleRange)
```

不做 MapMagic 的松弛 pass（relaxation）：cell 尺度小（板内 ≤ 数株），格内哈希抖动 + 聚簇噪声
已足够自然；松弛列为后续可选（记录在开放问题）。

### 6.2 ECS 实例化（HexVegetationSpawnSystem）

```
原型缓存: Dictionary<(Mesh,Material),(BatchMaterialID,BatchMeshID)> —
  EntityManager.World.GetExistingSystemManaged<EntitiesGraphicsSystem>().RegisterMesh/Material
触发: HexFeatureState.VegetationDirty == true（特征生成完成 / 重生成 / 手动菜单设置）
流程（预算 maxSpawnsPerFrame=500，地图小通常一帧完）:
  1. 查询销毁: Entities.WithAll<HexScatterInstance>().DestroyEntity(query)  // 整批重建
  2. 对每条规则: CreateArchetype(LocalTransform, HexScatterInstance,
       HexScatterPrototype{PrototypeIndex}, + RenderMeshUtility 所需组件)
     RenderMeshUtility.AddComponents(首实体, desc{ShadowCasting On}, mmi) 后
     EntityManager.CreateEntity(archetype, count) 批量 + SetComponentData(LocalTransform)
     （照抄 HexMeshWriteSystem 的注册顺序坑: AddComponents 后 LocalToWorld 可能是零矩阵 →
       LocalTransform 路径下由 TransformSystemGroup 统一算，验证首帧不出现零矩阵闪烁，
       若有则同法手动 SetComponentData<LocalToWorld>）
  3. RenderBounds = 原型 mesh.bounds 按 scale 上限放大
  4. 完成 → VegetationDirty = false
```

重生成触发汇总：初始特征生成后一次；`Tools/HexMap/重新生成植被` 菜单；
**不**挂接高程编辑（手编后植被陈旧——已知项，后续做「编辑格植被局部刷新」，
数据结构已支持按 cell 查询 HexScatterInstance 所属格：实例上挂
`HexScatterCell{int2 Offset}` tag 便于未来局部销毁）。

### 6.3 美术原型接线

散布规则的 mesh/material 来自第 8 节迁移的资产（Birch/Pine FBX + URP 材质）。
FBX 直接作为 `Mesh` 引用可用（网格在 FBX 子资产里）；**不用 prefab**（ECS 只要 mesh+material）。
树网格单位确认：MapMagic 演示树按世界尺寸建模（约 8–20 单位高），与 OR=10 的 cell 匹配度
在实施第 8 步时实测，不匹配则调 scaleRange。

---

## 7. 编辑器工具

### 7.1 POI 放置模式（HexMapPoiPlacementTool）

```
入口: [MenuItem("Tools/HexMap/POI放置模式")] 切换静态 bool + SceneView 工具栏按钮
      （SceneView.duringSceneGui 里 GUILayout.Toggle，快捷键 P）
前置: 仅 Play 模式生效（Edit 模式无 cell 实体——放置工具提示并引导进 Play；
      POI 存 ScriptableObject，Play 中的修改持久，见 2.3 说明）
交互（duringSceneGui, EventType.Layout/Repaint/MouseDown）:
  · 左键: HandleUtility.GUIPointToWorldRay → HexMapCellEditUtil.PickCell
      → Undo.RecordObject(settings, "Place POI") → pois.Add({Type=当前类型,
        CellOffset=命中格, WorldPos=命中点, Radius=默认}) → EditorUtility.SetDirty
  · Alt+左键: 删除拾取点附近最近 POI（半径内）
  · 类型切换: 工具栏弹窗（RiverSpring / RoadNode）+ 半径滑条
  · 热格高亮: Handles.DrawWireDisc(命中点, up, Radius) 预览盘
  · 放置后不自动重生成——弹提示「按 R 或菜单重生成」；快捷键 R =
      写 HexFeatureRegenerateRequest{ResetElevation=false}（只重跑特征，保留手编地形）
```

放置点精度：射线步进拾取与游玩时笔刷同一套（步长 0.5），命中即格中心 + 高程面。

### 7.2 Gizmos（HexMapFeatureGizmos）

`ExecuteAlways` MonoBehaviour（场景可选挂载，或由放置工具自动 Ensure）：
- POI：类型色盘（泉水=蓝、路点=橙）`Handles.DrawWireDisc(pos, Vector3.up, radius)` +
  `Handles.Label`（类型+序号）。
- 河流：Play 中遍历 `HexFeatureState.Rivers`，河床格画半透明蓝色小盘 @WaterY；湖泊画外轮廓线。
- 道路：路径折线（黄色）。
- 植被：选中格时 Label 显示该格实例数（统计 HexScatterCell 同格 tag）。
- 显示级别下拉：All / POI / Rivers / Roads / None（instance ID 过滤，避免全图 gizmo 噪声）。

### 7.3 菜单（HexMapFeatureMenus）

```
[MenuItem("Tools/HexMap/重新生成地形特征")]  // 完整管线（仅 Play）:
    1. streaming.EnsureAllLoaded()（新增公共方法，同步补建全图 cell）
    2. 全部 cell 高程重置回噪声值（HexMapTerrainMath.ElevationFromNoise，边界圈=0）+ 标脏
    3. 清 HexFeatureState / 销毁 FeatureMeshEntities / 销毁 HexScatterInstance
       / 移除全部 HexRiverCell/HexRoadCell/HexLakeCell 标签
    4. 写 HexFeatureRegenerateRequest{ResetElevation=false} → HexFeatureGenerationSystem 接管
[MenuItem("Tools/HexMap/重新生成植被")]       // 只重散布（不动地形/特征）
[MenuItem("Tools/HexMap/验证特征确定性")]     // 连跑两次生成，比对状态哈希，Console 报告
Edit 模式点击 → 提示「需在 Play 模式执行」（cell 实体不存在）
```

### 7.4 Inspector

`HexMapAuthoring` 走默认 Inspector（现有 public 字段 + Header 风格不变），新增
`enableFeatures` 开关 + `featureSettings` 资产引用两行即可；特征细节全部在
`HexMapFeatureSettings` 资产 Inspector 里编辑（ScriptableObject 默认 Inspector +
`[Tooltip]`，`HexMeshRewriteSettings` 同款风格；不做 attribute 驱动自定义 UI——超范围）。
防呆钳制不放 Builder（无 blob），放 `HexFeatureGenerationSystem` 读取时的归一化
（负密度截 0、margin≥0、threshold≤MaxElevation 等），并在资产 OnValidate 里 clamp。

---

## 8. 美术资产迁移（自包含，GUID 保留）

### 8.1 文件复制映射（PowerShell Copy-Item 连 .meta 一起，GUID 不变）

| 源（Assets/MapMagic/Demo/…） | 目标（Assets/Art/HexMap/…） | 内容 |
|---|---|---|
| `LandTextures/*.tif(+.meta)` | `LandTextures/` | 11 反照率 + 7 法线（CliffBright/Dark/Pink/Red、Dirt、GrassGreen/Yellow、Gravel、Sand、SandCracks、Snow） |
| `Trees/Birch/{Meshes,Textures}/**` | `Trees/Birch/` | 10 FBX + Birch.tif/Birch_n.tif/Birch_sssv.tif |
| `Trees/Pine/{Meshes,Textures}/**` | `Trees/Pine/` | 同构（实施时清点数量） |
| `Stones/{Meshes,Textures}/**` | `Stones/` | 石头 FBX + 贴图 |
| `Grass/{Meshes,Textures}/**` | `Grass/` | 草片网格 + 贴图 |

**不复制**：任何 `.cs`、`Materials/*.mat`（引用 MapMagic 自带 Tree.shader，删插件即断）、
prefab（引用旧 .mat）、Shaders/、Graphs/、场景文件。prefab 仅在需要 LOD 结构时
（Stone01loded）手工重建为新 prefab 引用新材质。天空盒全景图：**跳过**（Crest/UniStorm 已在管）。

### 8.2 地形贴图数组扩层

- 走既有 `TextureArrayGeneratorEditor`（Tools/美术/创建贴图数组）：**TIFF 直接拖入**
  （生成器内部 `MakeReadable` → 缩放输出 PNG → 统一导入设置 → 建 `.texture2darray`，
  任何 Texture2D 可导入格式都接受，无需预转 PNG）。
- 生成两数组：AlbedoMaps（反照率 11+现有层）、NormalMaps（法线 7+现有层），
  目标尺寸 1024（MapMagic 源 768–1024，统一 1024）。
- **TerrainIndex 映射约定**（数组层序 = TerrainIndex，全工程唯一表，写进
  `HexMapFeatureSettings` 注释与生成器窗口备注）：

| Index | 层 | Index | 层 |
|---|---|---|---|
| 0 | （现有层1，保留在首位防旧场景漂移） | 6 | Gravel |
| 1 | （现有层2） | 7 | Sand |
| 2 | GrassGreen | 8 | SandCracks |
| 3 | GrassYellow | 9 | Snow |
| 4 | Dirt | 10 | CliffBright |
| 5 | CliffDark | 11 | CliffRed / CliffPink（预留，二选一或都进） |

- 法线绿反（DX/OpenGL）与 sRGB 处理沿用生成器既有按类型管线（记忆库已定案），无需改代码。
- 高度图数组不建（MapMagic 地表无独立高度图；HeightBlend3 的高度项已有既有关键字开关）。
- 笔刷上限自动适配：`HexMapEditingSystem.ResolveTerrainLimit` 读数组 depth，扩层后 0–N 全可用。
- **风险对冲**：数组扩层只改材质绑定的 `.texture2darray` 资产，HexTerrain.shader 数组深度
  无编译期上限；重生成数组会覆盖 `GeneratedTextures/` 下同名文件——现有 2 层先备份
  （复制为 `*_Backup2Layer.texture2darray`）。

### 8.3 树木材质转换（URP → DanbaidongRP）

- 每树型新建材质：`Assets/Art/HexMap/Materials/Birch_Lit.mat`、`Pine_Lit.mat`、
  `Stone_Lit.mat`、`Grass_Card.mat`（草片后续双面/透明裁切：alpha clip + Cull Off）。
- Shader 选择：项目管线是 DanbaidongRP（URP 17.5 同 GUID 顶替）——**不要手写 URP/Lit 字符串**，
  实施时先运行现有工具 `Tools/标准urpshader自动替换引用到Danbaidong格式`
  （`DanbaidongShaderConverterWindow`）或参照 `Assets/UI/HexMap/_Materials/New Material.mat`
  实际使用的 Lit shader GUID 建材质；贴图槽映射 `_MainTex→_BaseMap`、`_BumpMap→_BumpMap`。
- 风/SSS（Tree.shader 的特色）**不在本期**：先纯 Lit 上屏验证不粉；风摆留作后续
  （做法备忘：顶点动画材质或在 DanbaidongRP 里加 instanced 风参数，另开计划）。
- 桩/根系贴图（Birch_sssv 之类 SSS/风权重图）暂不接（Lit 用不上），文件仍迁移保存。

### 8.4 新材质/Shader 清单（全部新建于 Assets/UI/HexMap/ 或 Assets/Art/HexMap/Materials）

| 资产 | 用途 |
|---|---|
| `Shaders/HexWater.shader` + `_Materials/HexWater.mat` | 河湖水面（透明） |
| `_Materials/HexRoad.mat` | 道路缎带（Lit 灰） |
| `Art/HexMap/Materials/Birch_Lit.mat` 等 4 个 | 植被原型 |

---

## 9. 分步实施（每步门禁：`dotnet build` HexMap.ECS.csproj + Assembly-CSharp-Editor.csproj 0 错误）

### 步骤 0：美术迁移 + 贴图数组扩层（纯资产，无代码）
- 8.1 复制（含 .meta）、8.2 重生成两数组（先备份现数组）、8.3 建 4 材质 + 跑转换工具。
- **验证**：地图现有两 TerrainIndex 显示不变；新材质球预览不粉；Inspector 拖 Mesh/Material 引用不丢。
- 门禁：资产刷新后两 csproj 编译仍 0 错误（TIFF 导入警告允许）。

### 步骤 1：共享基础设施（重构不改行为）
- `HexMapCellEditUtil`（ApplyElevation/MarkDirty/PickCell 提取，编辑系统改调用）+
  `HexMapTerrainMath`（ElevationFromNoise/HexDistance，地形生成 Job 改共用）+
  `HexTerrainHeightSampler`（含 Unperturb）+ 组件/单例定义（2.1/2.2）+
  `HexMapFeatureSettings` 资产 + authoring 字段 + Install/Baker 接线 +
  `HexFeatureOverlaySystem` 空转 + `HexFeatureGenerationSystem` 骨架（等待逻辑 + 空生成）+
  `HexChunkStreamingSystem.EnsureAllLoaded()`。
- **验证**：Play 进图与现状完全一致（无特征生成）；Console 出现「特征系统就绪」日志一次；
  Edit 手编高程行为不变；`Tools/验证HexMap网格` 通过。

### 步骤 2：河流生成（快照 + 行走 + 湖泊 + 提交）
- `HexRiverGenerator` 全量 + 编排接线 + 标签写入 + 距离图 BFS。
- **验证**：Play 中生成后 Console 打印各河（起终点/长度/结束类型）；河床高程单调不升
  （临时验证代码断言）；被拒河流无残留；`Tools/验证HexMap网格` **在雕刻后仍通过**（关键回归）；
  手动数字键刷地形后网格无裂缝。

### 步骤 3：水面渲染
- `HexWaterMeshBuilder` + `HexWater.shader` + `.mat` + 水体实体注册/销毁。
- **验证**：河谷可见半透明水面、无 z-fight（ε=0.1 下板面不闪）；落差裙边可见；
  湖泊水面不淹高台格；远处水面不遮挡排序异常（透明队列）；重生成无水体残留实体
  （EntityHierarchy 查 HexWaterBody 计数）。

### 步骤 4：道路生成 + 缎带
- `HexRoadGenerator`（连接 + Dijkstra + DP 整平）+ `HexRoadMeshBuilder` + `HexRoad.mat`。
- **验证**：道路贴地跨坡不悬空/不穿插（沿坡带采样正确性目测）；相邻格 |Δe|≤1 断言；
  穿河处缎带在水面上方；POI 端点对齐；重生成道路确定性一致。

### 步骤 5：植被散布
- `HexVegetationSpawnSystem` + 规则配置默认值（Birch/Pine/Stone/Grass 各一条示例规则）。
- **验证**：进 Play 后树/石/草出现在板面（不悬空、不入坡）；河旁路旁无植被（margin 生效）；
  高程过滤生效（雪线之上无树等）；重生成植被实体数恒定（同 seed）；帧率无异常
  （Entities Graphics 批渲染，<2k 实例应无感）。

### 步骤 6：编辑器工具
- POI 放置模式 + gizmos + 三个菜单 + 确定性验证菜单。
- **验证**：Play 中放泉水 POI → R 重生成 → 河从该泉出发；放路点 → 道路连接；
  退出 Play 后 POI 仍在（资产持久）；`验证特征确定性` 报告两次哈希相等。

### 步骤 7：收尾
- `riverBedTerrainIndex` 接线（沙层河床）+ 文档性注释 + 删除 MapMagic 插件目录前的
  引用扫描（grep `MapMagic` 于 Assets/Scripts 与场景）——**删除插件另起任务**，本计划止步于零依赖。
- **验证**：全菜单回归一遍 + 每步验证项抽查。

依赖顺序：0 可与 1 并行；2 依赖 1；3 依赖 2；4 依赖 1（采样器）+2（河距离图）；5 依赖 2/4；6 依赖全部。

---

## 10. 风险与开放问题

1. **流式 vs 整图特征**（最大架构风险）：特征需全图高程，loadRadius < 地图时挂起——已用
   `EnsureAllLoaded` + 警告兜底；真正的大地图流式特征（分块 + 边界缝合）明确不在本期。
   cell 卸载往返的高程/标签恢复由 OverlaySystem 幂等对账保证，但**用户手编后的卸载重建会丢手编**
   （回退到特征覆写值）——记录为已知语义。
2. **采样器精度**：坡带 t 参数按垂直边法线度量 vs 实际列向 lerp，存在梯形歪斜误差（<SlopeInset 量级
   的横向错位 → y 误差 <0.3）；扰动逆解 2 次迭代收敛 (<0.01)。缎带 +0.08 抬升掩盖残余。
   若目测穿模，备选方案：缎带 y 改为 max(采样y, 前后采样点 y+容差) 的单调平滑。
3. **平地图无河**：全 0 高程 + 阈值默认值 → 无泉眼（设计如此；阈值 0 + 挖掘起步可造峡谷河，文档化）。
4. **湖泊水位窗**：水面 y=(L+0.5)·Step 依赖扰动幅度 ≤0.2×Step（现 0.8–1.2 默认成立）；
   若用户把 ElevationPerturbRange 拉到极端（0.5/1.5），水位可能淹板或露底 → OnValidate 限制
   或运行时按实际 range 重算水位偏移（实施时取后者，水位 = L·Step + maxPerturb·Step·0.5）。
5. **手编后特征资产陈旧**：水面网格、植被不随手编刷新（仅重生成按钮触发）；编辑格植被局部刷新
   留待后续（HexScatterCell tag 已预留）。
6. **ECS 实体churn**：植被整批销毁重建（<2k 实体，无性能顾虑）；水体/道路实体同理。
   重生成期间若 Entities Graphics 批 ID 未释放——RegisterMesh 按 BatchMeshID 引用计数，销毁实体即可。
7. **DanbaidongRP 材质**：URP Lit 在该管线下的实际可用 shader 需实施时确认（转换工具/现有材质取 GUID），
   避免粉屏；水面 shader 用 URP 语法新写，需在 DanbaidongRP 下跑通透明队列（若管线改写了
   渲染 pass 注册，HexTerrain.shader 是现成参照）。
8. **贴图数组覆盖**：生成器输出路径固定，重生成会覆盖现数组（已发生过一次外部覆盖事故——记忆库）；
   步骤 0 强制备份，且数组层序表写死在两处注释。
9. **MapMagic 许可**：只复制资产文件（用户已确认资产可用），不复制任何 .cs/.shader/.mat 引用链；
   删除插件前 grep 扫描确认 Assets/Scripts 与场景零引用。
10. **未决小项**：草片双面/风摆、树 LOD（Stone01loded 结构参考）、程序道路贴图、
    植被松弛 pass、手编格特征局部刷新——全部记为后续迭代，不阻塞本期。

---

## 附：关键既有代码接触点（改动清单汇总）

| 文件 | 改动 |
|---|---|
| `HexMapEditingSystem.cs` | 私有方法改调 HexMapCellEditUtil（行为不变） |
| `HexTerrainGenerationSystem.cs` | Job 内高程公式改调 HexMapTerrainMath（行为不变） |
| `HexChunkStreamingSystem.cs` | +`EnsureAllLoaded()` 公共方法 |
| `HexMapAuthoring.cs` | +enableFeatures/+featureSettings 字段与 Install/Baker 接线 |
| `HexMeshJob*` / `HexMeshWriteSystem` | **零改动**（特征只写 cell 数据 + 标脏） |




