# AI 动作空间补全 · 修改计划

- 日期：2026-11-19
- 状态：**用户 2026-11-19 拍板不回退，当前目标 v18**：P-1 + P0 + P1（v17 历史）+ P2（v18）+
  P3.1（弃牌）+ P3.2（预言）+ P4-5.3（训练池 87 = TrainingCards 84 + 教学双标签 3）**已实现**；P3.3 handler 反问、B-5/C-1/C-2 **不在本轮**。
  布局 v18 / 动作 18 维 / 动作类型 14 种（含 `DiscardChoice=13`）；P2/P3 专项验收见
  [AI训练说明 §4.5](AI训练说明.md)，**P2/P3/P4 专项已通过（LaeMultiTargetVerifier 4/92 仅剩旧 ③④；100 局 audit 0 截断）**；重训待用户开跑（v18）。
- 依据：[AI训练说明 §4](AI训练说明.md)（2026-11-19 全量对账）、[目标域契约](目标域契约.md) §5、
  [目标候选区域信息_共享规格_v0.2](目标候选区域信息_共享规格_v0.2.md)、
  [AI动作空间_移动与宣言_规格_v0.3](AI动作空间_移动与宣言_规格_v0.3.md)、
  [AI与脚本执行修复_修改计划](AI与脚本执行修复_修改计划.md) §0.3.1。
- 范围：本地 AI / RL 动作空间（`LegalActionEnumerator` + 动作特征 + 驱动/AI 策略）；
  **不含** UI 改版、联机协议、卡表/数值/奖励改动。
- 性质：实现计划（分阶段、一次一派、做完停）。本文件只描述要改什么、怎么验收；不改代码。

---

## 0. 总则与阶段映射

### 0.1 总原则

1. 一次只做一个阶段；每阶段编译 + 自测 + 贴输出，做完停，不进入下一阶段。
2. 不改卡表/原子表/数值/奖励/观测（**除动作特征按本计划升版**）；换环境必须重训。
3. **任意数量不枚举 2^N**：无域原子用 `prefix-k`（前 k 张，k 可学）；其他效果保持现状子集行。
4. **单一来源**：无域原子的作用域/候选顺序只写一处（`NoDomainTargetScopes` / `HeroSkillTargetPolicy`）；
   `GreenRampStrategy` 与 `LegalActionEnumerator` 共用，不得各写一份。
5. 代码行为/注释/文档同批更新（AGENTS.md 纪律）；旧口径注释删除或标“已废弃（日期+原因+替代口径）”。
6. 不新建 runtime `.cs` 之外的工程结构；新增文件只放在既有 `CardCore` / `AI` 目录下。

### 0.2 缺口 → 阶段

| 阶段 | 缺口 | 目标 | 依赖 | 影响 |
|---|---|---|---|---|
| **P-1** | manifest 分叉 / 下标漂移 | 见 §0.4：**稳定 ID 派生下标（C）** | 无（先于 P0） | **重训/导出的身份一致性** |
| **P0** | 无域原子目标（绿牺牲/蓝蛰伏） | 见 §1：LAE 产出 prefix-k 行，模型能选“祭几张” | P-1 | **绿/P1 当前格** |
| **P1** | 移动选格 + 宣言卡名 | 见 §2：v0.3 落地（17 维/v17） | P-1（重训前） | 二期扩池/对账卡 |
| **P2** | 永久物落格 | 见 §3：**已实现 v18；专项已通过** | P1（v17→v18） | 二期站位 |
| **P3** | 手牌上限弃牌 / 预言类别 / handler 反问 | 见 §4：**4.1/4.2 已实现；4.3 部分（无卡）/专项已通过** | P1 | 二期/三期 |
| **P4** | B-5 / C-1 / C-2 / 训练池放开 | 见 §5：**5.3 已实现（87 张 = TrainingCards 84 + 教学双标签 3）；B-5/C-1/C-2 不在本轮** | P1/P3 | 后期 |

### 0.3 明确不做（本计划内）

| 项 | 原因 |
|---|---|
| 通用 2^N 子集枚举 | 动作表爆炸；只对无域 `Sacrifice/Dormancy` 做 prefix-k |
| 改卡表/数值/奖励/费用 | 换环境，超出动作空间范围 |
| UI / 联机协议 / 教学 `ScriptedAi` | 单独立项；本计划只动 AI/RL 路径 |
| 通用 `ActivatedAbility` 响应兜底 | 策略债务（B-5），P4 只补测试卡/信号 |
| 占卜排列策略（C-1） | 现行无卡/收益低；P4 记录，不实现 |
| `SimpleAI` 策略强度改版 | P1/P3 只接交互参数（PickMoveCell/PickDeclaration/弃牌停靠），不改选牌策略；v18 预设镜像 600 局逐局与 P0 前一致 |

### 0.4 P-1：稳定 ID 派生下标（C 方案，取代 manifest 追加式）

> **2026-11-19 拍板：选 C。** 当前 main/train 的 manifest 复制/合并（122 type / 239 hash，
> `E19CB310…`）只作为**过渡快照**保留；新方案不再依赖 manifest 的分配顺序。
> 旧 119/226 与当前 122/239 的 checkpoint/ONNX 均作废，按 C 从头重训。

#### 0.4.1 为什么 C

- 现行模型输入两层整数：`[15..28]` 精确身份 hash→index、`[29..40]` type→index。
- 现行 `TideCardIndex` 由 manifest 分配；空表时按排序 hash/枚举名播种 → main/train 两次生成下标不同
  （212 common hash 中 209 不同、119 common type 中 86 不同）。
- `原子表ID别名` 只解决 refId→现行原子行，不解决整数表。
- C：index 由**稳定 ID/hash 直接派生**，与池/生成顺序/manifest 无关 → main/train 天然同号。

#### 0.4.2 映射规则

- 卡身份（原子内容 / 结构 / 组合）：
  `index = 1 + SplitMix64(hash) % (N_CARD_POOL - 1)`；`0 = 无`。
- 类型：`index = 1 + SplitMix64(FNV1a64(EnumName)) % (N_EFFECT_TYPES - 1)`。
- 动作 effectIdentity：`index = 1 + SplitMix64(FNV1a64(def.Id)) % (N_EFFECT_IDENTITY_POOL - 1)`；
  动作特征 `[6]` 仍 `/ N_EFFECT_IDENTITY_POOL`（1024）。
- 不再调用 `TideCardIndex.Register/IndexOfOrRegister/ConfigureManifest/SaveManifest`；
  不再需要 `card_identity_manifest.json`（文件保留为 legacy 快照，不参与训练/导出）。
- hash 0 → index 0；所有下标 1..Pool-1。**未登记 hash 不再返回 0**（直接落稳定桶）；
  旧“未登记=0”语义废弃，0 只表示 hash=0/无成分。

#### 0.4.3 参数与版本

| 常量 | 现 | C |
|---|---|---|
| `N_CARD_POOL` / `TideCardIndex.Capacity` | 1024 | **16384**（碰撞多可升 32768） |
| `N_EFFECT_TYPES` | 256 | **4096** |
| `N_EFFECT_IDENTITY_POOL` | 1024 | 1024（不变） |
| `LayoutVersion` | 15 | **16** |
| 动作维 | 14 | 14（C 不动动作） |
| 卡特征 | 116 | 116（只改下标取值/embedding 表大小） |

- **v16 bump 原因**：card/type pool 尺寸与下标语义变化（不是观测特征维变化）；旧 1024/256 模型必须被拒。
- **P1 v0.3（动作 17 维）顺延为 v17**；若 C 与 P1 一起做，合并成一次 v17 重训，避免两次。
  v0.3 文档里“用 v16”的审计注由本条取代，执行 P1 时同批改。
- 文档同批：`AI训练说明` §4.3 现写“v0.3 17 维/v16”，P-1/P1 落地时改为“C v16、P1 v17”；v0.3 文档同批。
- `CardIdentityService.MaxTypeIndex` 255→**4095**。
- `tide_features.py`：`N_CARD_POOL=16384`、`N_EFFECT_TYPES=4096`、`LAYOUT_VERSION=16`；
  `tide_encoder` 两个 `nn.Embed` 尺寸同步；`export_onnx` fixture/元数据补 `cardPoolSize/typePoolSize`。
- `OnnxTidePolicy.ValidateModelLayout` 增 `cardPoolSize/typePoolSize` 校验（防旧 1024 模型静默 clip）。
- 代价：card embedding 1024→16384（约 16×，约 2.1M 参数）、type 256→4096（约 16×，约 0.5M）；
  训练显存/ONNX 体积上升，首轮先看吞吐与显存。

#### 0.4.4 代码改动

- `CardIdentityService`：`AtomTypeIndex` 改为稳定派生；删 `_typeRegistry`/`AdoptTypeIndexes`/manifest type 依赖；
  `MaxTypeIndex=4095`；保留 `AtomContentHashes`/结构/组合 hash 计算。
- `TideCardIndex`：`IndexOf`/`IndexOfOrRegister` 改为稳定派生；`Capacity=16384`；
  `ConfigureManifest`/`SaveManifest`/`Register` 标 legacy/no-op（或删除调用）；
  `IndexOfEffectDefinition` 改稳定 1024。
- `TideObservation`：不新增特征维；`LayoutVersion=16`；断言写入值 < Pool。
- `TideHeadlessServer`：删 `CardManifestPath`/`ConfigureManifest`/`RegisterIdentities`；
  日志打印 pool sizes / layout；不再读写 manifest。
- `tide_rl`：`tide_features.py` / `tide_encoder.py` / `export_onnx.py`；模型/ONNX/fixture 全部重导。
- `make_train_copy.ps1`：manifest 不再必须复制（可继续排除，文件仅存档）。
- `SpecMasterVerifier`：新增 `StableIndexVerifier`：同 ID→同 index、范围、C#/Python 常量、碰撞报告；
  旧 `ManifestVerifier`/sync 不再需要。

#### 0.4.5 碰撞

- 239 身份 → 16384：期望 ~1.7 碰撞；122 type → 4096：~1.8；可接受。
- 碰撞 = 两个不同 ID 共享 embedding 行；`CollisionReport` 列出 `index → IDs`。
- 若高频身份碰撞/训练效果差：`N_CARD_POOL` 升 32768、`N_EFFECT_TYPES` 升 8192，重训；
  **不做**“按插入顺序探测”（会重新引入顺序依赖）。
- 0 保留为“无/未命中”。

#### 0.4.6 验收

> **2026-11-19 执行记录：全过**（StableIndexVerifier 10/10、规范总校验 PASS 16/FAIL 0 含 F 段、
> audit 100 局 v16 握手+池尺寸断言通过 0 截断、TideSim --scenario 通过、export --random 冒烟
> v16/16384/4096 元数据全绿、旧 v15 部署件被 fixture 自检明确拒绝）。重训待用户开跑。

- [x] C# `TideCardIndex.IndexOf`/`AtomTypeIndex` 对同一 ID 跨进程/跨 main/train 同值
  （纯函数确定性 + C#/Python 跨运行时逐值对拍——StableIndexVerifier 内嵌 Python 预计算样本）；
  `LayoutVersion=16`、`N_CARD_POOL=16384`、`N_EFFECT_TYPES=4096` 双侧一致（规范总校验 A2）。
- [x] `OnnxTidePolicy` 旧 1024 模型被拒（ValidateModelLayout 扫图常量：缺 16384/4096 embedding 表
  + 旧 1024/256 表识别报错；fixture 自检对现役 v15 部署件实测拒），新模型导出链通过
  （export --random --no-unity-copy 冒烟——随机件不可部署，真模型加载随重训后导出）。
- [x] 碰撞报告：卡身份 186 身份碰撞 1（期望 1.1）/ EffectType 119 碰撞 4（期望 1.7）/
  效果 Id 171 碰撞 16（期望 14.3）；最大组全部 2，预算=生日期望+3σ+2 全在内。
- [x] `export_onnx` fixture/ONNX 元数据含 pool sizes（tide.card_pool_size/type_pool_size + dims
  两键；verify_export 三方对账全绿）；`OnnxFixtureSelfCheck`/`NeuralDeployGuard` 按 v16+池尺寸
  对账（旧件拒=PASS 判据成立；新件 PASS 待真模型重导出）。
- [x] `audit_action_space --episodes 100` 0 截断（布局握手 v16+cardPoolSize/typePoolSize 两侧断言）；
  `TideSim --scenario` 通过；TCP 100 局完局 0 错误。
- [x] 旧 119/226/122/239 checkpoint/ONNX 全部不 resume（v16 拒载实证）；从 C 从头重训（待用户开跑）。
- [ ] A1 `e0eebf27` 文档更新（独立项——P-1 未动卡表，A1 哈希未变，随下次卡表变更一并更新）。

#### 0.4.7 不做（P-1 内）

- 不做 manifest sync / append-only 守卫（C 不再依赖 manifest；过渡快照保留）。
- 不做 hash alias/remap（原子表锁定；无内容漂移）。
- 不改动作空间 / 奖励 / 卡表 / 数值。

---

## 1. P0：无域原子目标（绿牺牲 / 蓝蛰伏）

### 1.1 现状（证据）

- 绿技能 `牺牲`（`C_AC7FC01F` / `FXGREEN08`）：`selection=Multiple`、`count=-1`、**`domain=[]`**；
  `PerAtomTargets=True`、`TargetDomain=0`、原子 `Sacrifice` 的 `TargetKinds=0`。
- 蓝技能 `蛰伏`（`C_23EE2486`）：原子 `Dormancy` 同样无 Domain，`TargetKinds=0`。
- LAE：`CollectAtomTargetSpecs` 对 `kinds.Count==0` 直接 `continue`（[EffectHandlerRegistry.cs:651-652](../Assets/Scripts/HotUpdate/CardCore/Attribute/EffectHandlerRegistry.cs#L651-L652)）
  → `ExpandPerAtomTargets` 0 行 → `EnumerateHeroSkill` 落一条 `Targets=null` 行。
- 结算：`ResolveAtomTargetsAsync` 对“无域 + 无预选”返回空（[同文件:463-470](../Assets/Scripts/HotUpdate/CardCore/Attribute/EffectHandlerRegistry.cs#L463-L470)）
  → `SacrificeHandler` 直接 return（空放）；`DormancyHandler` 无预选则自动全取
  （[BuiltinEffectHandlers.cs:240-251](../Assets/Scripts/HotUpdate/CardCore/Attribute/Handlers/BuiltinEffectHandlers.cs#L240-L251)）。
- 脚本侧：`GreenRampStrategy.HeroSkillTargets` / `HeroSkillTargetCap` 用预选列表绕过这个缺口
  （[GreenRampStrategy.cs:483-526](../Assets/Scripts/HotUpdate/AI/GreenRampStrategy.cs#L483-L526)）——**模型路径没有这个口**。

> 结论：模型点牺牲 = 花 2 费空放；蓝技能能用但只能“全取”，不能选张数。绿/P1 55% vs 脚本 77.2% 的直接原因。

### 1.2 方案：prefix-k + 共享候选顺序

**不改特征维度/布局号**；只改 LAE 行集 + 一处共享 policy。动作空间变化仍按《AI训练说明》§5 必须重训。

1. **无域作用域表**（新文件 `Assets/Scripts/HotUpdate/CardCore/Effects/NoDomainTargetScopes.cs`）：
   ```csharp
   public enum TargetRowMode { Subset, Prefix }

   public static class NoDomainTargetScopes
   {
       // 显式白名单：无 Domain 原子 → (域串, filter, 行模式)。
       // 必须用域串（TargetAxisRules 解析），不能用 TargetKind.OwnLivingUnit——
       // 那个枚举含“己方角色”，而牺牲的作用域是“己方战场:生物”（不含角色）。
       // Sacrifice: "己方战场:生物;己方战场:结界;己方元素池:卡"
       // Dormancy : "己方元素池:卡"，filter=Untapped（或排序时过滤）
       public static bool TryGet(AtomicEffectType type, out string domain, out string filter, out TargetRowMode mode);
       // 注意要带 def：原子自身 count 常为 -2，-1 在效果级 def.TargetCount；
       // 固定数量的 Sacrifice（如 count=1）不得走 Prefix。
       public static bool IsPrefixAtom(AtomicEffectInstance atom, EffectDefinition def);
   }
   ```
   - 只对 `Sacrifice`/`Dormancy` 返回 true；未知无域原子仍走原 `continue`（不静默改行为）。
   - `TargetRowMode.Prefix` 只用于这两个原子；其余 `-1` 效果保持子集行。
2. **`CollectAtomTargetSpecs`**：`kinds.Count==0` 时先查 `NoDomainTargetScopes.TryGet(atom.Type, ...)`；
   命中 → `TargetAxisRules.TryParseDomain(domain, out var axis)` 得到 axis/kinds（与 `CardEffectConverter`
   同口），用该 axis/filter 解析候选，建 spec（`EffCount=-1`，`AiMode=Enumerate`）；未命中 → 原 `continue`。
   **不要**直接把 `[OwnLivingUnit(1), ...]` 塞进 kinds：`OwnLivingUnit` 含己方角色，会把英雄选进牺牲名单。
3. **`ExpandPerAtomTargets`**：若 `NoDomainTargetScopes.IsPrefixAtom(spec.Atom, def)` → 先按 atom 类型
   调 `HeroSkillTargetPolicy.OrderSacrifice/OrderDormancy` 重排候选，再 `AddPrefixRows`：
   ```csharp
   // k=1..N，每行 Targets = candidates.Take(k)（候选顺序由 §1.2.4 的 policy 决定）
   // 行数 ≤ MaxPrefixRows=24；N>24 时只发 1..23 + 全取行（与现有 cap 口径一致）
   ```
   不再走 `AddSubsetRows`。
4. **共享候选顺序**（新文件 `Assets/Scripts/HotUpdate/AI/HeroSkillTargetPolicy.cs`，`CardCore.AI`）：
   ```csharp
   public static List<Entity> OrderSacrifice(GameCore core, Player me, List<Entity> candidates);
   public static List<Entity> OrderDormancy(GameCore core, Player me, List<Entity> candidates);
   ```
   - `OrderSacrifice`：先“场上单位/结界，排除 `归根` 与场上的 `阿格洛斯`”，再“元素池全部（含池里的阿格洛斯）”，
     组内保持引擎候选序、去重保序——与 `GreenRampStrategy.HeroSkillTargets` 现有顺序一致。
   - `OrderDormancy`：只留“己方元素池未横置的地”，引擎序。**2026-11-20 追加（已落地）**：
     拆出 `OrderDormancy(ElementPoolSystem, Player, List<Entity>)` 单源重载，声明窗 spec 与拒发闸
     候选口同走该过滤（`EffectHandlerRegistry.NoDomainScopeCandidates`）——全横置=候选空=拒发不扣费
     （「没有可横置的地牌无法发动」定案），人类窗口也只列未横置地；牺牲照旧原始候选。
   - `GreenRampStrategy.HeroSkillTargets` 改为调用 `OrderSacrifice`（行为不变，A/B 验证）；
     `HeroSkillTargetCap` 不动。
5. **`TideAction` 不新增字段**：`Targets` 列表长度即 k；动作特征 `[10]nTargets` 需从 **0..8 钳位提到 0..16**
   （`LegalActionEnumerator.BuildFeatures` 的 `Math.Min(..., 8)`；`tide_features.py` 注释同步）。
   否则 k=9..16 特征完全相同（前 3 目标也相同），模型分不出“祭 9 张 / 11 张”。
   `[3]` 首目标 + `[12]/[13]` 区域/归属可区分起点；第 4+ 目标不可见——prefix 行靠 `nTargets` 区分，够用。
6. **`Apply` 不改**：`a.Targets` 原样交给 `ActivateHeroSkill`；`SacrificeHandler`/`DormancyHandler` 直接消费。

### 1.3 文件清单

| 文件 | 改动 |
|---|---|
| `CardCore/Effects/NoDomainTargetScopes.cs` | 新增：无域作用域/行模式白名单 |
| `CardCore/Attribute/EffectHandlerRegistry.cs` | `CollectAtomTargetSpecs` 无域分支接白名单 |
| `AI/NeuralEnv/LegalActionEnumerator.cs` | `ExpandPerAtomTargets` 接 `Prefix`；新增 `AddPrefixRows`；`MaxPrefixRows` 常量；`nTargets` 钳位 8→16 |
| `AI/NeuralEnv/TideObservation.cs` | 仅注释：`[10]nTargets` 0..8 → 0..16（维度不变） |
| `tide_rl/tide_features.py` | 仅注释：`[10] nTargets` 0..8 → 0..16（维度不变） |
| `AI/HeroSkillTargetPolicy.cs` | 新增：`OrderSacrifice` / `OrderDormancy` |
| `AI/GreenRampStrategy.cs` | `HeroSkillTargets` 改调 `OrderSacrifice`（行为不变） |
| `TideSim/TideSim.csproj` | 必须加 `AI/HeroSkillTargetPolicy.cs`（`GreenRampStrategy` 引用；不引 NeuralEnv） |
| `Assets/Editor/验证/AI对战/LaeMultiTargetVerifier.cs` | 新增 P0 场景断言（见 §1.4） |
| `docs/AI训练说明.md` | §4.2/§4.5 更新“P0 已落地”状态（完成后） |

> 不新建 runtime 目录；`NoDomainTargetScopes` 放 `CardCore/Effects`，`HeroSkillTargetPolicy` 放 `AI`（与策略同层）。

### 1.4 验收

**A. 编译**

```powershell
dotnet build Tide.HotUpdate.csproj
dotnet build TideSim/TideSim.csproj
dotnet build TideServer/TideServer.csproj -c Release
```

**B. LAE 场景（扩展 `LaeMultiTargetVerifier`；Editor 菜单“Tools/验证/LAE多目标组合枚举”）**

- **T5 盘面**：绿技能卡 `C_AC7FC01F` 在技能栏；元素池 11 张地（直接入池，绕槽上限——夹具播种）；
  场上无单位。枚举 LAE：
  - HeroSkill 行数 = **11**（prefix k=1..11），无 `Targets=null` 行；
  - 第 k 行 `Targets.Count == k`，特征 `[10] == k`，`[12]=2/[13]=0`（首目标=元素池）；
  - 应用 k=11：11 张地入墓、`TempElements[Any]==11`、池 0；技能横置。
    **（2026-11-21 已废弃：临时元素持有钳定案——Any 入账封顶地牌上限，临时彩==min(k, cap)；
    LaeMultiTargetVerifier 现版断言 min(11,cap)，本行为 P0 验收时点的历史记录）**
- **T7 盘面**：场上 1 张 `阿格洛斯` + 元素池 11 张地（**不摆小精灵**，保证候选只有池地）；枚举：
  - 任何行的 `Targets` 不含场上的 `阿格洛斯`（`OrderSacrifice` 排除）；
  - HeroSkill 行数 = **11**（k=1..11）；应用 k=6：6 张池地入墓、阿格洛斯仍在场、`TempElements[Any]==6`。
- **蓝蛰伏**：蓝技能卡 `C_23EE2486` 在技能栏；元素池 3 张未横置地；枚举：
  - HeroSkill 行数 = 3（k=1..3）；应用 k=2：恰 2 张横置 + 2 层 `DormancyLayers`，第 3 张不动。
- **无目标行回归**：EndTurn 行 `[10]=0`、`[12]=[13]=-1`。

**C. 脚本 A/B（行为不变）**

- `dotnet run --project TideSim -- --games 200 --matchup mirror --seed 20261119`：
  P0 前/后绿镜像胜率差 ≤ 统计噪声（±3pp）；`--scenario` 全过。
- 目的：证明 `HeroSkillTargets` 抽 policy 是纯重构，不改脚本打法。

**D. 训练侧**

- `python tide_rl/audit_action_space.py --episodes 100`（需 Unity TCP）：
  HeroSkill 行数在绿局显著上升（从 ~1 行/点 → 11~16 行/点），0 截断；带目标行占比上升。
- 重训：P0 改变了 LAE 行集（即使 14 维不变），按《AI训练说明》§5 必须重训；旧 checkpoint 可加载但不建议续训。

### 1.5 风险与回退

- **脚本行为漂移**：`HeroSkillTargets` 重构必须逐行等价（A/B 200 局）；不等价则回退 policy 调用、保留原逻辑。
- **prefix 行数**：N≤16，行数可控；`MaxPrefixRows=24` 防异常。
- **候选顺序**：`OrderSacrifice` 依赖卡名 `归根`/`阿格洛斯`；卡名改名时同批改常量并 grep（AGENTS.md 数值/常量纪律）。
- **其他无域原子**：白名单只开 `Sacrifice`/`Dormancy`；未知原子维持原跳过，避免静默行为变化。

---

## 2. P1：移动选格 + 宣言卡名（v0.3 落地）

### 2.1 目标

按 [AI动作空间_移动与宣言_规格_v0.3](AI动作空间_移动与宣言_规格_v0.3.md) 实现：

- `InteractionParams`（`DeclaredName` / `MoveX/Z`）随发动入栈；
- LAE 删除 `UnsupportedForAi` 对 `MoveUnit`/`SearchDeck`/`DeclareHand`/`DeclareDeck` 的裁剪；
  移动行 = 目标 × 合法空格；宣言行 = 候选名（≤16）；
- 动作特征 14→**17**（`+[14]moveCellX/[15]moveCellZ/[16]declaredNameIndex`），`LayoutVersion=17`；
- `SimpleAI` 接 `PickMoveCell`/`PickDeclaration`；`ScriptedAi` 不改；
- Python `tide_features.py`：`N_ACTION_FEATURES=17`、`LAYOUT_VERSION=17`；`export_onnx.py` fixture 补三列。

### 2.2 与 v0.3 的差异（实施注）

- v0.3 原文写 `LayoutVersion=15`；v15 已被场地特殊化占用；**C（P-1）占 v16** →
  **P1 用 v17**（v0.3 的“用 v16”审计注由本条取代；执行 P1 时同批改 v0.3 文档）。
- v0.3 的“宣言候选只使用已读到的卡名”口径不变；`SearchDeck` 候选=己方牌库去重名（己方信息天然可读）。
- 不把“无域技能目标”塞进 v0.3（已由 P0 处理）；v0.3 只做移动/宣言两类参数。

### 2.3 验收

> **2026-11-19 晚执行记录：全过**——LaeMultiTargetVerifier ⑥ 场景（移动 8 行=空格数/[14][15] 坐标/应用落格/
> 满盘 0 行/PickMoveCell 首格；宣言强夺/窃取/检索行数=候选数、[16] 互异、命中迁移、无展示 0 行、
> 手牌/牌库候选不混区、SimpleAI 空集不发动）；TideSim --scenario + red-blue 1 局；SpecMaster
> PASS 16/FAIL 0（A2 v17/17 对账）；audit 100 局 v17 握手 0 截断；export --random 冒烟 512x17 全绿。
> **注**：MoveUnit/宣言行在 audit 随机池不出现——P2-1..4 仍在对账样板未放池（P4 §5.3 放开后复跑）。
> 关键实现补遗：① cast 结算 `ResolveSpellEffectsAsync` 新建 EffectInstance 须抄 `Interaction`（原断链）；
> ② `ActivateWithTargets` targets==null 早退分支须带参；③ 移动行 AutoTake 免弹档（Targets=null）经
> spec 计划候选定落格单位。

- v0.3 §6.2/6.3/6.4/6.5 全过：
  - 移动：指定空格成功、非法格不移动/不白付、无空格不发动；LAE 行数=目标×空格（≤18）；
  - 宣言：有展示候选、无展示不发动、候选不混区；LAE 行数=候选名数（≤16），特征 `[16]` 互异；
  - 编译 + `TideSim --scenario` + `--games 1`；
  - `SpecMasterVerifier`：C#/Python `17/17` 对账；Python 握手/冒烟 39 局。
- `python tide_rl/audit_action_space.py --episodes 100`：`MoveUnit`/`Declare` 行出现、0 截断、0 白付。

### 2.4 影响

- 含 MoveUnit/宣言的卡从“死牌”变为可打；二期训练池可放开 P2-1..4（见 §5.3）。
- 旧 checkpoint/ONNX/fixture 全部作废（v17；P2 再升 v18）。

---

## 3. P2：永久物落格（RL cell 维度）

> 现状：`TideAction` 无 `CellX/CellZ`；模型能出永久物，落点由引擎/首格决定。`SimpleAI` 已有
> `PickPlaceCell`（[AiStrategy.cs](../Assets/Scripts/HotUpdate/AI/AiStrategy.cs)），RL 未接。

### 3.1 设计（已实施，v18）

- **方案 A（推荐）**：`TideAction` 加 `int PlacementCell`（0..8，-1=无）；动作特征加 1 维
  `[17]placementCell`；`LayoutVersion=18`（17→18 维）。
- LAE `EnumeratePlayCard`：仅永久物（生物/结界）枚举合法空格；每张卡×模式×目标集×格 = 行；
  每卡上限 `MaxPlaceRows=9`，总行数 cap 继续走 `MaxActions`。
- `Apply`：`a.PlacementCell` 需在 `PlayCard` 声明前 `PinCard`——`LegalActionEnumerator.Apply` 是静态、
  无棋盘引用；建议加 `RuleHooks.PlacementPin`（由驱动/`AiDuelRunner` 注入 `BoardState.PinCard`），
  `Apply` 调它；无棋盘（headless 未接）自然跳过。
- 方案 B：出牌后单独“落格”决策点（两步动作）——不改现有 row 维度，但改驱动循环；本计划不选。

### 3.2 验收（已实施，v18）

> **2026-11-19 晚执行记录（方案 A 已实施）**：LAE 永久物 PlayCard/RespondPlay 行×空格逐格展开
>（PlacementCell=UnitCells(己方) 下标 0..8，MaxPlaceRows=9；法术/场地行 -1 单行）；特征 [17]、
> NAction=18、v18；Apply 声明成功即 PinCard——**不经 RuleHooks 注入**，直接取
> `MoveUnitRules.Current`（BattleController/AiDuelRunner 既有装配口，免新钩子；无棋盘自然跳过，
> 竞态占格跳过走引擎 first-free 兜底）。探针：生物 9 行（cell 0..8）、应用 cell 8 → 实际落
> UnitCells[8]=(5,1)、特征 [17] 正确；TideSim --scenario+1 局过；SpecMaster PASS 16/FAIL 0
>（A2 v18/18）；export --random 冒烟 512x18 全绿（旧 v17 冒烟件被 verify 正确拒）；
> SimpleAI 行为不变（自带 TryPinPlacement 不经新口）。

- LAE：永久物行携带 `placementCell`；空格/非法格/满区；默认首格与改前一致。
- `Apply`：指定格落位；无棋盘跳过；`SimpleAI` 行为不变。
- 布局 v18：Python/ONNX/fixture 同步；旧模型作废。

> 2026-11-19 用户拍板不回退，P2 保留；当前缺口仅是**专项 verifier**（行数/Apply/满区），
> 不是实现缺失。P2 验收完成前不得报全绿。

---

## 4. P3：结算期选择（弃牌 / 预言 / handler 反问）

> **2026-11-19 晚执行记录（全部落地/裁定）**：
> **4.2 预言类别**——LAE 对含 ProphecyNextCard 的行展开 3 行（DeclaredName=Type:Creature/Spell/
> Enchantment，特征 [16]=0/1/2；InteractionVariants 内与移动/宣言并存的笛卡尔为防御路径，现表无并存卡）；
> `AskDimensionAsync` 顶部读 `context.Interaction?.DeclaredName` 优先返回（无参走原默认，AI/无头不变）。
> **4.1 手牌上限弃牌**——`TideActionType.DiscardChoice=13`（Python ACTION_TYPES 同步）；
> `HandLimitDiscardGate`（新文件）：模型座 EnforceHandLimitAsync 改 Park+await，非模型座回落
> RequestAsync 旧行为；`LegalActionEnumerator.EnumerateDiscardChoices` 独立枚举口（over=1 逐张/
> over=2 C(n,2)/over≥3 截断+默认组，MaxDiscardRows=24，恒 ≥1 行）；TideHeadlessDriver 绑门
>（自对弈双座/vs 模型座）+ AdvanceToDecision 循环顶停靠（g[49]=窗口语义）+ Step 应用（Complete
> 内联跑余段）+ 强收/终局/Dispose 兜底释放。弃牌触发面=2026-11-18 后仅「上限缩水」兜底（罕见）。
> **4.3 handler 反问**——核查裁定：DiscardCard 原子行 43ac095e **有域**（双方手牌:卡）→ 主序列
> 使用已由既有 spec 机制覆盖（P0 白名单只管无域）；代价栏弃牌（训练·弃置）属代价目标轴（既有
> payload gate 预检+AI 代选），不在本计划改动面。RedirectTarget/DiscoverCard **表内无行无卡**——
> 记录为「有卡时单独评估」（原计划口径）。

> 这些决策目前走 `TargetSelectionService.RequestAsync` 自动代选；模型没有动作行。
> 目标域契约 §5 已登记债务：`RedirectTarget / DiscardCard / Sacrifice / DiscoverCard / MoveUnit`。
> `Sacrifice` 由 P0 处理，`MoveUnit` 由 P1 处理；本节处理其余。

### 4.1 手牌上限弃牌

- 现状：`GameCore.EnforceHandLimitAsync` 直接 `RequestAsync`（[GameCore.cs:333-377](../Assets/Scripts/HotUpdate/CardCore/GameCore/GameCore.cs#L333-L377)）；
  AI/无头在 [TargetSelection.cs:182-197](../Assets/Scripts/HotUpdate/CardCore/Effects/TargetSelection.cs#L182-L197) 自动选前 N。
- 实现（已实施，v18）：
  - 新增 `TideActionType.DiscardChoice`（枚举尾部追加；Python `ACTION_TYPES` 同步；不改特征维度）；
  - `TideAction.Targets` = 选中的 `over` 张；行 = `over` 张组合（`over` 通常 1~2；上限 24 行，
    `over>3` 时只发“按策略排序的前 N 组 + 默认组”）；
  - `EnforceHandLimitAsync` 在模型座位改为“等待驱动决策”：驱动在回合末停靠，枚举 `DiscardChoice` 行；
    应用后移墓/除外（按现规则）。
  - `SimpleAI`/无头保持自动；`PendingRequests` / End→Standby 折返门不变。
- 验收：`over=2`、5 张手牌 → 10 行；应用后恰 2 张入墓；不自动；`--scenario`/`--games 1` 正常。
- 风险：回合末时序/折返门；已用 `HandLimitDiscardGate` 的 TCS 停靠+强收/终局兜底实现，
  **但 over≥2 应用/入墓/无泄漏尚无专项断言**。

### 4.2 预言类别宣言（3 选 1）

- 复用 P1 的 `Interaction.DeclaredName`：LAE 对含 `ProphecyNextCard` 的 `PlayCard` 行展开 3 行，
  `DeclaredName = "Type:Creature" / "Type:Spell" / "Type:Enchantment"`，特征 `[16]=0/1/2`；
- `ProphecyHandlerUtil.AskDimensionAsync`（[ProphecyHandlers.cs:383-390](../Assets/Scripts/HotUpdate/CardCore/Attribute/Handlers/ProphecyHandlers.cs#L383-L390)）
  优先读 `context.Interaction?.DeclaredName`；无则原默认值（AI/无头行为不变）；
- 验收：3 行；分别应用后 `ProphecyRegisteredEvent` 类别与所选一致；不选时默认值不变。

### 4.3 handler 内反问（DiscardCard / RedirectTarget / DiscoverCard）

- 目标域契约 §5 债务；逐个迁入 `CollectSpecPlan` 或复用 `TargetRowMode.Prefix`/语义宏：
  1. `DiscardCard`（弃牌）：候选=手牌/对方手牌；有域的可直接进 spec 计划；无域/隐藏区先按“候选名/前缀”；
  2. `RedirectTarget`（重定向）：结算期换目标；需要“目标窗”动作行，建议单独评估；
  3. `DiscoverCard`（发现/检索选牌）：候选=牌库；v0.3 的 `DeclaredName` 只代理“按名检索”，
     真正的“从 N 张选 1”需候选行（隐藏信息按已展示处理）。
- 每个 handler 单独验收；不要求一次全做。

### 4.4 占卜排列（C-1）

- 现行无卡/收益低；本计划**只记录不实现**。若后续入池，按“看顶 N 张排序”单独立项。

---

## 5. P4：补信号 / 放开池

> **2026-11-19 晚执行记录**：5.1 B-5——不写新代码维持；RespondActivate 行在 EnumerateResponses
> 既有，audit 0 出现归因池无响应窗主动卡，SimpleAI 兜底维持不排期（策略债务）。5.2 C-2——
> 记录为「模型有（牌库顶 PlayCard 行）、脚本无」，不改。5.3 训练池放开——UnsupportedForAi 闸
> 已随 P1 删除；P2-1..3（训练·疾行者/强夺/窃取）tags 加「训练池」入随机池（P2-4 预言无卡跳过）；
> 100 局 v18 audit 已复跑（15,487 步/0 截断；DiscardChoice 3,005 行），覆盖矩阵已同步
> 87 张（TrainingCards 84 + 教学双标签 3）；随机 30 抽样 1000 次全覆盖。P4-5.3 验收通过。

### 5.1 B-5 响应主动效果

- LAE 已有 `RespondActivate` 行（[LegalActionEnumerator.cs:227-229](../Assets/Scripts/HotUpdate/AI/NeuralEnv/LegalActionEnumerator.cs#L227-L229)）；
  `AI与脚本执行修复` 计划 §8 标“不排期（策略债务）”。
- 动作：不写新代码；造一张能开“响应窗主动效果”的测试卡/场景，验证 LAE 行出现 + `Apply` 成功；
  再评估是否给 `SimpleAI` 加兜底（单独策略项）。

### 5.2 C-2 牌库顶虚拟手牌

- 模型已有 `PlayCard from Deck top` 行（[LegalActionEnumerator.cs:516-539](../Assets/Scripts/HotUpdate/AI/NeuralEnv/LegalActionEnumerator.cs#L516-L539)）；
  `SimpleAI` 缺（计划 §8 说不排期）。本计划不改 SimpleAI；记录为“模型有、脚本无”。

### 5.3 训练池放开

- 已实施（2026-11-19）：`UnsupportedForAi` 闸已随 P1 清零；P2-1..3（疾行者/强夺/窃取）已加
  `训练池` tag 入随机池（P2-4 预言无卡）；当前池 87 张（TrainingCards 84 + 教学双标签 3）。
- 已完成：100 局 `audit_action_space`（15,487 步/0 截断，DiscardChoice 3,005 行）；
  [训练池覆盖矩阵 §P2](训练池覆盖矩阵.md) 已改为 87 张/已入池；随机 30 抽样 1000 次全覆盖；
  `SpecMasterVerifier` PASS 16/0。
- 剩余：`TideServer` 存量红、`AIModels` 旧件、4 项旧 ③④ verifier 红、v18 真模型导出/重训。
  `SimpleAI` 多目标移动边界、`TideObservation.cs:663`/`BattleDeckSources.cs:33` 旧注释已修。

---

## 6. 回归清单（每阶段都跑）

> **2026-11-19 v18 当前回归状态**：HotUpdate/TideSim build 0；v18 预设镜像 600 局逐局一致
>（红 66/134、蓝 97/103、绿 188/12）；`SpecMasterVerifier` PASS 16/0（A2 18/18、F1 10/10）；
> `LaeMultiTargetVerifier` 4/92（P0/P1/P2/P3.1/P3.2 全过；4 项旧 ③④ 漂移）；
> 100 局 audit 0 截断；`TideServer` 仍 15 个既有错误（`FiresOncePerPaidLifetime`，非本轮）。

- [x] `dotnet build Tide.HotUpdate.csproj`（0 错误）
- [x] `dotnet build TideSim/TideSim.csproj`（0 错误）
- [ ] `dotnet build TideServer/TideServer.csproj -c Release`（既有基线红：FiresOncePerPaidLifetime）
- [x] `dotnet run --project TideSim -- --scenario`（通过）
- [x] `dotnet run --project TideSim -- --games 1 --matchup red-blue --detail`（0 错误 0 未完）
- [x] P2 落格专项（行数/Apply/满区）——LaeMultiTargetVerifier ⑩ 通过
- [x] P3.1 弃牌专项（over=2/3、应用入墓、事件）——⑧ 通过
- [x] P3.2 预言三选一专项——⑨ 通过
- [x] `python tide_rl/audit_action_space.py --episodes 100`（v18：15,487 步/0 截断）
- [x] `SpecMasterVerifier` PASS 16/0；P2/P3 行为由 LaeMultiTargetVerifier 覆盖
- [x] `git status --short` + `git diff --stat`（工作区含本轮全部改动，逐项汇报）
- [x] 代码/注释/文档同批——`TideObservation.cs:663`/`BattleDeckSources.cs:33` 已清，SimpleAI 多目标已加闸

---

## 7. 派活提示词（ZCode，历史记录，**已执行**）

> 以下 P-1/P0/P1/P2/P3 提示词是 2026-11-19 执行时原文，保留作回溯；
> 当前契约是 v18（见文件头与 [AI训练说明 §4](AI训练说明.md)），**不要再按 P2“只设计”/P3“先 spike”派活**。
> 后续只派“补 P2/P3 专项断言”“补 100 局 v18 audit”这类验收阶段。

### P-1

```
读取 docs/AI动作空间补全_修改计划.md §0.4（P-1：稳定 ID 派生下标 C），只做 P-1。
目标：TideCardIndex/CardIdentityService 改为 SplitMix64 稳定派生；N_CARD_POOL=16384 /
N_EFFECT_TYPES=4096 / N_EFFECT_IDENTITY_POOL=1024；LayoutVersion=16；Onnx pool size 校验；
CollisionReport。
约束：只改 §0.4.4 文件；不改动作/卡表/数值；不依赖 manifest（文件仅存档）；旧 checkpoint/ONNX
不 resume；P1 v0.3 顺延 v17，不同批做。
验收：§0.4.6；贴同 ID 跨进程同值、pool 常量、碰撞报告、ONNX fixture、audit 100 局。做完停，不进入 P0。
```

### P0

```
读取 docs/AI动作空间补全_修改计划.md §1（P0：无域原子目标），只做 P0。
目标：绿技能「牺牲」/蓝技能「蛰伏」的 LAE 产出 prefix-k 行（k=1..N），模型能选“祭几张”；
候选顺序由 CardCore.AI.HeroSkillTargetPolicy 单源提供，GreenRampStrategy.HeroSkillTargets 改调它（行为不变）。
约束：只改 §1.3 文件；不改特征维度/布局号；不枚举 2^N；未知无域原子保持原跳过；
不碰 UI/联机/ScriptedAi/卡表数值。先读当前文件的 2026-11-19 注释与 §4 口径。
验收：§1.4 A/B/C；贴 LAE 行数（T5=11、T7 排除场上阿格洛斯、蛰伏=3）、k=11/6/2 应用结果、
TideSim mirror 200 局 A/B、编译输出。做完停，不进入 P1。
```

### P1

```
读取 docs/AI动作空间_移动与宣言_规格_v0.3.md 与 docs/AI动作空间补全_修改计划.md §2，只做 P1。
目标：移动选格 + 宣言卡名 + 动作 17 维 / LayoutVersion=17；删除 UnsupportedForAi 对 MoveUnit/宣言的裁剪；
SimpleAI 接 PickMoveCell/PickDeclaration；Python 17/16 同步。
约束：v0.3 §0 文件清单；不改 ScriptedAi/联机/卡表；特征维一次升到 17；旧 ONNX/fixture 作废。
验收：v0.3 §6.2/6.3/6.4/6.5 + SpecMasterVerifier + audit_action_space 100 局；贴输出。做完停。
```

### P2

```
先只做设计评审（不写代码）：读 docs/AI动作空间补全_修改计划.md §3，给出 RL 落格方案 A/B 的
行数/特征维/Apply 注入点/对 cap 的影响，评审通过后再排实现。
```

### P3

```
按 docs/AI动作空间补全_修改计划.md §4 逐项做：先 4.2 预言类别（复用 P1 DeclaredName），
再 4.1 手牌上限弃牌（先 spike 决策点），最后 4.3 handler 反问（逐个）。每次只做一个子项，做完停。
```

---

## 8. 交付物

| # | 交付 | 说明 |
|---|---|---|
| 1 | 代码 | P-1：`CardIdentityService`/`TideCardIndex`/`TideObservation`/`TideHeadlessServer`/`StableIndexVerifier`；P0：`NoDomainTargetScopes`/`HeroSkillTargetPolicy`；P1/P3.2：`InteractionParams`；P3.1：`HandLimitDiscardGate`；P2/P3.1 各自 §文件清单 |
| 2 | 文档 | `docs/AI训练说明.md` §4 状态更新；v0.3 文档“已实施”标注；训练池覆盖矩阵更新；本计划勾选进度；**任何文档改动按 AGENTS.md 通知用户** |
| 3 | 证据 | P-1：同 ID 跨进程/main-train 同值、pool 常量、碰撞报告、ONNX fixture/self-check；P0/P1/P2/P3：编译输出、LAE 场景断言、A/B 600 局、`SpecMasterVerifier`、LaeMultiTargetVerifier 4/92（仅旧 ③④） |
| 4 | 未做项 | P3.3 handler 反问（Redirect/Discover 无卡）、B-5、C-1、C-2（SimpleAI 侧）；`TideServer` 存量红、`AIModels` 旧件、4 项旧 ③④、v18 真模型导出/重训 |
| 5 | 当前契约 | `LayoutVersion=18` / `N_ACTION_FEATURES=18` / 14 动作类型（`DiscardChoice=13`）/ 训练池 87（TrainingCards 84 + 教学双标签 3）；v16/v17 为历史版本 |

---

## 9. 与既有文档的关系

- **取代/收口**：`AI与脚本执行修复_修改计划` §8 中“RL 动作空间加落格/移动选格/宣言”条目——
  本计划 P1/P2 接手；该计划的 A-1/A-2“跳过”口径在 P1 落地后删除。
- **依赖**：P-1（C 稳定 ID 派生下标，v16）先于 P0/P1；P1 依赖 v0.3 规格（顺延 v17）；
  P0 依赖 P-1（不再换身份表）。
- **不重复**：动作空间的**当前口径**以 `AI训练说明` §4 为单一来源；本计划只写“要改什么/怎么验收”，
  完成后把状态回写 §4。


