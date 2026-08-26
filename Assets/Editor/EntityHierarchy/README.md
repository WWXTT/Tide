# Entity Hierarchy 编辑器工具

为 Unity ECS 项目提供树状分组的 Entity 管理面板。

## 功能

1. **按逻辑分组显示 Entity**：以 `EntityDebugLabel.Group` 为节点组织树状结构
2. **点击面板 → 相机聚焦**：单击选中 Entity 到 Inspector，双击让 Scene 相机拉到 Entity 位置
3. **Scene 点击 → 选中 Entity**：在 Scene 中直接点选 ECS 渲染的 Entity（像素级拾取），Inspector / 面板 / Gizmo 三方联动
4. **Scene 视觉反馈**：选中 Entity 时在 Scene 中绘制包围盒和位置标记

## 使用方法

### 1. 启用调试标签（可选）

在项目设置中添加编译符号 `ENABLE_HEX_DEBUG_LABEL`：
- **Player Settings** → **Other Settings** → **Scripting Define Symbols**
- 添加 `ENABLE_HEX_DEBUG_LABEL`

未启用时，面板会显示所有 Entity 但不分组。

### 2. 打开面板

菜单：**Window → ECS → Authoring Hierarchy**

### 3. 进入 Play 模式

Entity 只在运行时存在，因此必须：
- 进入 Play Mode，或
- 启用 LiveLink（Entities → Baking → LiveLink Mode → LiveConversionInEditMode）

### 4. 交互

- **单击** Entity 行：在 Inspector 中显示详细信息
- **双击** Entity 行：Scene 相机拉到该 Entity 位置
- **Scene 左键单击**：点在 ECS 渲染的 Entity 上（无 GameObject 遮挡）即选中它，面板自动展开并高亮对应行

### Scene 点选的触发条件

`EntityScenePicking` 刻意做得保守，避免干扰原生交互：

- 仅**左键且无修饰键**（Ctrl/Shift/Alt 点击不触发）
- 该点命中 **GameObject 时原生选择优先**（ECS 拾取只作为"点空"时的兜底）
- MouseDown 立即选中；**MouseUp 被吞掉**，防止 Scene 默认的"点击空白取消选择"清掉刚选中的 entity
- **拖框不受影响**：按下到抬起位移超过阈值时交回默认处理
  （代价：从 Entity 上起手拖框会短暂选中一次，抬手后被框选结果覆盖）
- 选中后面板自动展开对应分组并高亮该行

## 实现细节

### 标签组件：`EntityDebugLabel`

```csharp
// HexMap/Components/EntityDebugLabel.cs
public struct EntityDebugLabel : IComponentData
{
    public FixedString64Bytes Group; // 分组名称
}
```

### 标记位置

#### Baker 中（SubScene 烘焙）
```csharp
public class MyAuthoringBaker : Baker<MyAuthoring>
{
    public override void Bake(MyAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.Dynamic);
        
        #if ENABLE_HEX_DEBUG_LABEL
        AddComponent(entity, new EntityDebugLabel { Group = "MyGroup" });
        #endif
    }
}
```

#### 运行时创建
```csharp
var entity = em.CreateEntity(archetype);

#if ENABLE_HEX_DEBUG_LABEL
em.SetComponentData(entity, new EntityDebugLabel { Group = "RuntimeEntities" });
#endif
```

### 已集成位置

本项目已在以下位置添加标签：

1. **HexChunkStreamingSystem.cs**：所有 hex cell → `"HexCell"`
2. **HexMapAuthoring.cs**：
   - HexMapConfig 单例 → `"HexMapConfig"`
   - HexMapCameraData 单例 → `"HexMapCamera"`

## 架构

```
Assets/Scripts/Editor/EntityHierarchy/
├─ AuthoringHierarchyWindow.cs  # 主窗口
├─ AuthoringTreeView.cs         # TreeView 实现
├─ EntityTreeViewItem.cs        # 树节点数据
├─ EntitySelectionBridge.cs     # 反射封装（访问 internal EntitySelectionProxy）
├─ EntitySceneGizmos.cs         # Scene 视觉反馈（选中包围盒 + 位置标记）
├─ EntityScenePicking.cs        # Scene 点选拾取（像素级颜色编码）
└─ EntityPickShader.shader      # 拾取用 Unlit 纯色 shader

Assets/Scriptes/HexMap/Components/
└─ EntityDebugLabel.cs          # 标签组件（运行时 assembly）
```

### Scene 点选实现（`EntityScenePicking`）

参考 [io.github.jonasdem.entityselection](https://github.com/jonasdem/EntitySelection)
（Entities 0.11 时代的拾取方案），适配 Entities 1.4 / Entities Graphics / URP：

1. 点击时收集所有 World 中带 `MaterialMeshInfo` + `LocalToWorld` 的可渲染 entity，
   通过 `EntitiesGraphicsSystem.GetMesh(BatchMeshID)`（运行时注册，HexMap cell 走这条）
   或 `RenderMeshArray.GetMesh`（烘焙静态下标）还原出 `UnityEngine.Mesh`
2. 用 `EntityPickShader` 把每个 mesh 画进一张与 Scene 相机同分辨率的离屏 RT
   （Linear 读写），每个 entity 编码成唯一颜色 = 候选下标 +1（0 为背景）
3. 鼠标坐标经 `GUIPointToScreenPixelCoordinate` → 相机 viewport → RT 像素，
   读回 1px 颜色反查 entity，走 `EntitySelectionBridge.SelectEntity` 选中

选中后由已有的 `Selection.selectionChanged` 链路联动：面板高亮 + Gizmo 绘制。

## 性能

- **增量刷新**：只在 Entity 数量变化时重建树（`World.EntityManager.Debug.EntityCount`）
- **条件编译**：`ENABLE_HEX_DEBUG_LABEL` 未定义时，标签组件不会添加到 archetype

## 扩展

### 添加右键菜单
在 `AuthoringTreeView.cs` 覆写 `ContextClickedItem`：
```csharp
protected override void ContextClickedItem(int id)
{
    var item = FindItem(id, rootItem) as EntityTreeViewItem;
    if (item?.entity == null) return;
    
    var menu = new GenericMenu();
    menu.AddItem(new GUIContent("Destroy Entity"), false, () => {
        item.world.EntityManager.DestroyEntity(item.entity);
    });
    menu.ShowAsContext();
}
```

### 添加搜索
在 `AuthoringHierarchyWindow.cs` 添加 `SearchField`：
```csharp
private SearchField _searchField;
private string _searchString = "";

void OnEnable()
{
    _searchField = new SearchField();
    // ...
}

void OnGUI()
{
    _searchString = _searchField.OnGUI(_searchString);
    // 传递给 TreeView 过滤
}
```

## 故障排查

### 面板为空
- 确认已进入 Play Mode 或启用了 LiveLink
- 检查是否定义了 `ENABLE_HEX_DEBUG_LABEL`
- Console 中查看 `[EntityHierarchy]` 前缀的警告

### Scene 点击选不中 Entity
- Entity 必须有 `MaterialMeshInfo` + `LocalToWorld`（即由 Entities Graphics 渲染）；
  无渲染体的 entity（如 HexMapConfig 单例）无法被像素拾取，请在面板中选择
- 该点命中了 GameObject 时原生选择优先，属于预期行为
- 检查 Console 是否有「找不到拾取用 shader」警告（EntityPickShader 未导入时会回退内置 Unlit/Color）
- 从 Entity 上起手拖框会被原生框选覆盖，重新单击即可

### Inspector 不同步
- 检查 Console 是否有 `无法解析 EntitySelectionProxy` 警告
- Entities 包版本是否 >= 1.0

## 依赖

- Unity 6000.5+
- com.unity.entities 1.4.6+
- com.unity.entities.graphics 1.4.19+
