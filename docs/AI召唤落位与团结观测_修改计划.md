# AI 召唤落位与团结观测 · 修改计划

- 日期：2026-11-20
- 状态：**计划（未实现；用户 2026-11-20 提出并拍板 §10 四项）**。本文只描述改什么、怎么验收，不改代码。
- 依据：用户 2026-11-20 口径（预选目标/落位必须确定后才算发动完成、才发事件；结算期选择才自动选/
  空发）；[目标域契约](目标域契约.md) §4/§5；[AI 动作空间·移动与宣言 规格 v0.3](AI动作空间_移动与宣言_规格_v0.3.md)；
  [AI 动作空间补全 修改计划](AI动作空间补全_修改计划.md) §1–§3；[AI 训练说明](AI训练说明.md) §4。
- 范围：本地 AI / RL **动作空间 + 观测**、召唤衍生物的**声明期落位**、**团结邻接**观测、人类 UI 的
  取消/回退契约。**不含**联机协议、卡表/数值/奖励、教学剧本 `ScriptedAi`、UI 美术改版。
- 性质：分阶段实现计划（一次一派、做完停；任一阶段未验收不得报全绿）。

---

## 0. 派活摘要

| 阶段 | 目标 | 主要改动 | 验收 |
|---|---|---|---|
| **1. 引擎 + 人类 UI** | 召唤落位变成声明期预选；未确定不发动、不发事件；取消/回退干净 | `InteractionParams.SummonCells`、`DeclarationTargets.Interaction`、`SummonPlacementRules`、`SummonTokenHandler`、`BattleScreen` 落位窗 | 无头探针（落位/取消 0 事件/触发式默认不变）+ 手操 |
| **2. 行动空间** | LAE/SimpleAI/训练环境能选召唤落位；无头装配补齐 | `LegalActionEnumerator`、`TideAction.Signature`、`SimpleAI`、`TideHeadlessDriver`（`MoveUnitRules.Attach`） | LaeMultiTargetVerifier 固定盘面 + 无头选格探针 + `--scenario`（audit 留阶段 4，池内暂无召唤卡） |
| **3. 观测** | 棋盘 9 格占用/邻接、团结邻接数、团结奖励摘要可见 | `TideObservation`、`CardIdentityService.EngineRewardOf`、Python 常量 | C#/Python 对账 + 观测探针 + SpecMaster A2 |
| **4. 训练池 + 全量验收** | 池内含召唤/团结卡；v20 定版重训 | `TrainingCards/TrainingEffects`（或标签）、覆盖矩阵、ONNX/fixture | audit 100 局、600 局镜像 A/B、重训交接 |

> 版本只在**阶段 3 收口时一次升到 v20**（阶段 1/2 单独验收时**不开放训练**）。
> 阶段 3 依赖阶段 2 定下的动作行；两项**同批验收、一次重训**，但派活仍按「一次一派、做完停」
> 串行（先 2 后 3），不在同一阶段里混改观测。

---

## 1. 用户口径（2026-11-20 定案，按原文落到契约）

### 1.1 预选目标 / 预选落位 = 发动前置（声明期）

- 目标/落位/宣言名/预言类别等**需要玩家在发动前确定**的交互，统一为「声明期预选」。
- **未确定之前不算发动完成**：不发对应事件（`CardPlayEvent` / 主动效果发动事件）、不扣费、不入栈。
- UI 在选择期间**持续显示取消/回退**；取消 = 撤销发动（零引擎状态、零事件）；回退 = 撤掉最近一次选择，
  继续选择。
- **只有确定后**才调用 `PlayCard` / `ActivateEffect` / `ActivateHeroSkill`（事件在引擎内照常发）。

### 1.2 执行结算中的目标选择 = 结算期

- 结算期弹选（`handler` 内反问 / `defer` 原子 / 触发式目标 / 奖励效果目标 / 弃牌·发现·重定向）：
  **不选就自动选**，**没有合法目标就空发**（不退款、不回卷）。
- 判定单源沿用 [目标域契约](目标域契约.md) §4（`CollectSpecPlan` 的 `AiMode`）：`Enumerate` = 声明期；
  `Auto` = 结算期。新增交互必须显式归入其一，**不得**在 `handler` 内对预选类交互发起反问
  （§5 铁律）。

### 1.3 事件时序（必须可断言）

| 场景 | 选择时机 | 取消/未选 | 确定后 |
|---|---|---|---|
| 法术有目标 | 声明期（`CollectSpellDeclarationTargetsAsync`） | 不调 `PlayCard`：0 事件/0 费 | `PlayCard` → `CardPlayEvent` 1 次 → 结算 |
| 主动效果/英雄技能有目标 | 声明期（`CollectEffectDeclarationTargetsAsync`） | 不调发动：0 事件/0 费 | 发动 → 入栈事件 1 次 |
| 永久物打出落格 | 声明期（UI 选格单 → `PlayCardFinal`） | 选格取消：手牌恢复、0 事件 | `PlayCard` → `CardPlayEvent` → Pin |
| **效果召唤衍生物落位（本计划）** | **声明期（新增）** | **不调发动：0 事件/0 费** | **确定后发动 → `CardPlayEvent`/发动事件 1 次 → 结算期落位** |
| 触发式/奖励召唤（无声明期） | 结算期 | 不选=自动（引擎默认计划） | 照常 `TokenCreatedEvent` |
| 结算期目标（弃牌/发现/重定向/移动） | 结算期 | 自动选/空发（既有） | 照常 |

### 1.4 「全部预选目标」现状审计（结论）

| 交互 | 现状实现 | 结论 |
|---|---|---|
| 法术目标 | `CollectSpellDeclarationTargetsAsync`（`AllowCancel=true`） | **已合规** |
| 主动效果目标 | `CollectEffectDeclarationTargetsAsync`（`AllowCancel=true`） | **已合规** |
| 英雄技能目标 | 同上（`PreselectHeroSkillAsync` 链） | **已合规** |
| 永久物打出落格 | UI 先选格（`BeginUseCellPicking`）→ `PlayCardFinal` | **已合规** |
| 宣言卡名 / 预言类别 | `InteractionParams.DeclaredName`（声明期） | **已合规** |
| **召唤衍生物落位** | **无预选**：`SummonTokenHandler` 结算期默认排布 | **缺口（本计划）** |
| 移动选格（人类） | 结算期 `CellSelectionService` 反问；AI 声明期 `Interaction.MoveX/Z` | 结算期口径，**允许**；与 AI 口径不一致，列为后续统一 |
| 触发式目标 | 结算期自动/`AiOrder` 代选 | 结算期口径，**允许** |
| `DiscardCard/RedirectTarget/DiscoverCard` | 结算期反问/自动 | 结算期口径，**允许**（§4.3 既有记录） |

> 结论：本计划的「全部预选目标」不是重写所有路径，而是 **补齐唯一缺口（召唤落位）**，并把
> `AI训练说明` §4.1 第 8 条的错误 ✅ 改回来（见 §11 文档变更）。

---

## 2. 现状与证据

### 2.1 召唤衍生物现在为什么是「默认排布」

- `SummonTokenHandler.Execute` 在结算期自行决定落格：`战斗母巢`/5 只走 2×3 六格块，其他走
  「邻接己方生物数最高」的贪心，最后 `PinCard`。
  （[SummonTokenHandler.cs:162-214](../Assets/Scripts/HotUpdate/CardCore/Attribute/Handlers/SummonTokenHandler.cs#L162-L214)）
- 声明期没有任何落位交互：`SummonToken` 是**无域原子**，`CollectSpecPlan` 不产生目标 spec；
  `LegalActionEnumerator.InteractionVariants` 只处理移动/宣言/预言三类，不处理召唤。
  （[LegalActionEnumerator.cs:1342-1464](../Assets/Scripts/HotUpdate/AI/NeuralEnv/LegalActionEnumerator.cs#L1342-L1464)）
- 结果：人类无位置选择；AI 只能选「发动/不发动」，落位由引擎决定 → 「团结4 触发两次」是引擎
  写死的场景，不是 AI 学会的站位。

### 2.2 预选路径本身已具备「取消=撤销发动」

- `CollectDeclarationTargetsAsync` 的 `AllowCancel=true`，返回 `null` = 取消；
  调用方（`SpellDeclarationFlow`）只清理预览/手牌，不调 `PlayCard`。
  （[EffectHandlerRegistry.cs:938-1038](../Assets/Scripts/HotUpdate/CardCore/Attribute/EffectHandlerRegistry.cs#L938-L1038)、
  [BattleScreen.Fans.cs:311-347](../Assets/Scripts/HotUpdate/UI/Screens/BattleScreen.Fans.cs#L311-L347)）
- 因此落位交互只要**并入这条声明期收集链**，取消/事件语义自动成立，不需要新造一套。

### 2.3 团结（引擎）的评估与观测现状

- 评估：`BranchEngines` 在 `CardPlayEvent/CardCastSettledEvent/OnTurnEnd` 窗口按 `UnityAdjacentCountOf(card)`
  读「6 邻己方**生物**数」（不含自身、不含结界/敌方），≥ `engineParam` 发奖励。
  （[BranchEngines.cs:323-340](../Assets/Scripts/HotUpdate/CardCore/Effects/BranchEngines.cs#L323-L340)、
  [BoardState.cs:365-381](../Assets/Scripts/HotUpdate/CardCore/Board/BoardState.cs#L365-L381)）
- 观测：门槛 x 已可见（卡特征 `[0]value` / `[2]branchParam`，v19）；**当前邻接数不可见**；
  **奖励载荷（`branch.then`）不可见**——`CollectAtoms` 只展平 `Steps`/`AtomicEffects`，不展平
  `branch.then`，而 `BranchIdentity` 也不含 then 内容。
  （[CardIdentityService.cs:127-153](../Assets/Scripts/HotUpdate/CardCore/Effects/CardIdentityService.cs#L127-L153)、
  [CardIdentityService.cs:239-250](../Assets/Scripts/HotUpdate/CardCore/Effects/CardIdentityService.cs#L239-L250)）
- 结论：AI 看得到「团结4」，看不到「我现在邻接几个」「达标后给什么」——站位决策没有可学的信号。

### 2.4 训练/无头装配缺口（重要）

- `AiDuelRunner` 与 `BattleController` 都 `MoveUnitRules.Attach(_board)`；**`TideHeadlessDriver` 没有**。
  （[AiDuelRunner.cs:234](../Assets/Scripts/HotUpdate/AI/AiDuelRunner.cs#L234)、
  [BattleController.cs:196](../Assets/Scripts/HotUpdate/UI/Screens/BattleController.cs#L196)、
  [TideHeadlessDriver.cs:210-219](../Assets/Scripts/HotUpdate/AI/NeuralEnv/TideHeadlessDriver.cs#L210-L219)）
- 后果（训练环境）：
  1. `SummonTokenHandler` 读不到 `MoveUnitRules.Current` → 召唤回落到 first-free 一排（不是聚集块）；
  2. `LegalActionEnumerator.PlacementCellChoices` 退化为「几何全格」（不筛占用），
     `TryPinPlacementCell` 也读不到棋盘 → **P2 的永久物落格动作在训练中实际不生效**；
  3. `MoveUnitRules.LegalDestinations` 恒空 → 移动行在训练中为 0。
- 这会直接让本计划的「AI 能选落位」在训练中变成空谈。**阶段 2 必须先补 Attach/Detach**，并用
  验收断言「选格 → 实际落格」。

### 2.5 训练池缺口

- 训练池 84 张（`TrainingCards.json`）+ 教学双标签 3 张（教学·1 / 教学·5 / 预组·21）中，
  **没有一张含 `SummonToken` 或团结引擎**（`TrainingEffects.json` 无 `f3ca462e`/`83c150f2`）。
- 含召唤/团结的卡全在教程/预组：战斗母巢、系命母巢、工蜂母巢、战斗衍生物·血等。
- 不扩池 → AI 在随机池遇不到这些卡，本计划的动作/观测只能在对局脚本/评估场景里生效，训练学不到。

### 2.6 文档错误（同批修正）

- `AI训练说明` §4.1 第 8 条：「永久物/召唤落格 … ✅ P2（v18）」——P2 只覆盖**打出永久物**的落格，
  不覆盖**效果召唤衍生物**的落位。应改为「永久物 ✅；效果召唤落位 ❌（本计划 v20）」。
  （[AI训练说明.md:305](AI训练说明.md#L305)）
- `AI训练说明` 两处引用 `AI绿套学习与观测补盲_修改计划`，该文件**不存在**（`docs` 无此文件、
  git 历史无记录）——悬空链接，应改为实际文件或删除。

---

## 3. 方案总览

### 3.1 引擎：把「召唤落位」做成声明期交互参数

- `InteractionParams` 新增：

  ```csharp
  /// <summary>召唤落位（声明期，2026-11-20）：按召唤顺序的己方单位格下标（BoardLayout.UnitCells
  /// 的 0..8）；null/空=无（触发式/奖励/旧路径走默认计划）。</summary>
  public List<int> SummonCells;
  public bool HasSummonCells => SummonCells != null && SummonCells.Count > 0;
  ```

- `DeclarationTargets` 新增 `public InteractionParams Interaction;`（人类声明期收集结果携带落位；
  AI 走 LAE 直接写 `TideAction.Interaction`）。
- 新单源 `CardCore/Board/SummonPlacementRules.cs`（与 `MoveUnitRules` 同层）：
  - `DefaultCells(...)`：把 `SummonTokenHandler` 现有 2×3 块/贪心算法**原样搬入**（行为逐字节不变）；
  - `FreeCells(...)`：己方单位区空格（按 `UnitCells` 序）；
  - `BuildCandidates(...)`：生成 AI/UI 候选计划（见 §3.3）；
  - `PredictUnityHits(...)`：给定落位计划，预测落位完成后「己方团结卡达标数」（供特征与 UI 提示）；
  - `ValidateCells(...)`：格合法（己方单位区、空格、数量≤N）。
- `SummonTokenHandler`：
  1. 若 `context.Interaction?.HasSummonCells`：按序落位（逐只 `PinCard`）；声明期已校验，结算期
     仅做竞态兜底（格被占→取该格最近空格；仍然落不下→该只走原满场逻辑/入墓）；
  2. 否则 `DefaultCells(...)`（触发式/奖励/旧路径，行为不变）；
  3. 落位发生在 `TokenCreatedEvent`/团结评估之前（现有顺序保持）。

### 3.2 人类 UI：持续取消/回退，确认后才发事件

- 入口：
  - 法术：`SpellDeclarationFlow`（目标收集之后）；
  - 主动效果/英雄技能：`BattleScreen.Info/Board` 的 `CollectEffectDeclarationTargetsAsync` 调用点之后。
- 新 `PickSummonPlacementAsync(card, effectDef, count)`：
  - 候选 = `SummonPlacementRules.FreeCells`；用 `BattleCellPicker` 的**多选模式逐只点选**
    （已拍板 2026-11-20 §10-3）；
  - 窗内恒显示「取消」「回退上一只」「自动填充」「确认」；
  - 「取消」→ 返回 `null`，调用方走既有取消清理（预览拆、手牌恢复、`_pendingPins` 作废）；
  - 「回退」→ 只撤最近一只，不关闭窗；
  - 「确认」→ 写 `decl.Interaction.SummonCells`，再 `PlayCard/Activate`（此时才发事件）。
- 无空格/数量不足：提示并允许「按可选格数确认」或取消；不得静默改默认。
- **触发式/奖励召唤不弹窗**（无声明期，按 §1.2 结算期自动）。
- 联机：本轮不携带落位参数；联机局保持引擎默认计划（见 §9）。

### 3.3 AI 行动空间：召唤行 = 「锚点 + 确定性填充」计划

- `LegalActionEnumerator`：
  - `InteractionVariants` 新增召唤分支：若效果含 `SummonToken` 原子（且为主动/施放路径），
    生成落位计划候选：
    1. **默认计划**（`DefaultCells`，1 行）；
    2. **锚点计划**（每个空格作为第一只，其余按确定性规则填充；≤9 行）；
    3. 合计 ≤ `MaxSummonRows = 12`；
  - 锚点填充规则（单源写在 `SummonPlacementRules`）：
    - 已选格集合 S；下一只优先取「与 S 中任一格 6 邻」的空格；
    - 同层按「6 邻己方生物数降序 → UnitCells 序」；
    - 不足以填满 N 时，按 UnitCells 序补空格；仍不足则计划只含可落格数（剩余只结算期照原满场逻辑）。
  - 候选去重：相同 `SummonCells` 序列只留首个；`SummonCells` 按召唤顺序写入。
  - 行只在该效果**可发动**时产生（费用/时点预检照旧）；**候选为空（无空格）= 0 行 = 不发动**。
- `TideAction` 新增（仅特征/签名用，Apply 不读）：
  ```csharp
  public int SummonPlan;       // 0=默认，1..9=锚点计划，-1=非召唤行
  public int SummonUnityHits;  // 该计划落位后己方团结达标数 0..6（-1=非召唤/不可估）
  ```
- **`Signature` 必须加 `SummonCells`/`SummonPlan`**：否则等价折叠
  （`FoldEquivalentRows`）会把不同落位计划误并成一行（与 P1 的 `DeclaredName/MoveX/Z` 同批教训）。
- 特征（v20）：
  - `[17]placementCell` 复用为「落位格/锚点格下标」：永久物=落格格；召唤=计划首格；-1=无；
  - `[18]summonPlan`：0=默认 / 1..9=锚点 / -1=非召唤；
  - `[19]summonUnityHits`：0..6 / -1（**已拍板保留**，2026-11-20 §10-2；模型直接把站位计划与
    团结收益关联，属动作特征而非观测泄露）。
- `SimpleAI` 新增策略口（默认不改变现有打法）：
  ```csharp
  /// <summary>召唤落位（2026-11-20）：plans=候选计划；默认取 DefaultCells（旧行为）。</summary>
  public virtual List<int> PickSummonPlacement(GameCore core, Player me, Card source,
      EffectDefinition effect, int count, IReadOnlyList<IReadOnlyList<int>> plans);
  ```
  `SimpleAI` 默认返回默认计划；想利用团结时可覆写（本轮不改策略强度）。
- `TideHeadlessDriver`：补 `GameBoard.MoveUnitRules.Attach(_board)` / `Detach()`（与 `AiDuelRunner`
  同装配口），并在 `Reset`/`Dispose` 成对清理。**否则阶段 2 的落位在训练中不生效。**

### 3.4 观测：让「格子/邻接/团结」直接可见

- `TideObservation`（v20）：
  - **globals +18**（`g[70..87]`，`NGlobal 70→88`）：
    - `g[70..78]`：己方单位区 9 格的占用码（0=空 / 1=己方 / 2=敌方，`/2`；无棋盘=0）；
    - `g[79..87]`：上述 9 格各自的「6 邻己方**生物**数」`/6`（团结口径；无棋盘=0）。
  - **卡特征 +4**（`NCard 153→157`）：
    - `[153]adjacentOwn/6`：战场卡的 6 邻己方生物数（非战场=0）；
    - `[154]engineRewardCount`：该卡引擎分支奖励载荷数（`branch.then` 原子数 + `effectId` 计 1；无=0，钳 3）；
    - `[155]engineRewardType0`：首奖励原子的 `AtomTypeIndex`（`effectId` 引用=0；无=0）；
    - `[156]engineRewardValue0`：首奖励原子的 value（`effectId` 引用=0，钳 99）。
  - `WriteCard` 增加 `BoardState board` 参数（`Build` 已有）；`WriteGlobals` 增加 board 参数。
- `CardIdentityService.EngineRewardOf(CardData, out count, out type0, out value0)`：只读摘要，
  **不改 `AtomHash`/`ContentId`**（身份哈希仍是 v19 口径；奖励内容只进观测维）。
- 目标：AI 能直接读「某格落子后团结差多少」，不必从 191 个卡槽的 `posX/posZ` 自己反推。

### 3.5 训练池：让召唤/团结卡真正进池（已拍板：新建 2 张训练卡）

- 现状：池内 0 张召唤/团结卡（§2.5）。
- 已拍板（2026-11-20 §10-1）：**新建 2 张可抽训练卡 + 1 张衍生物模板**（模板带「衍生物」tag，
  不进随机池——沿用 `CardCatalog.IsTokenCard` 口径；`TrainingPool()` 只取「训练池」tag）：
  1. `训练·母巢`（Creature，tags=训练池）：主动效果「召唤 5 个 `训练·战斗衍生物`」
     （`SummonToken` 原子 `f3ca462e`，value=5，str=模板 ID）；
  2. `训练·团结卫队`（Creature，tags=训练池）：自带「团结3：回合结束全体攻击+1」
     （`83c150f2`，settle=3/engine=8/engineParam=3，then=`39a3106c` value=1 kinds=[1]）——
     非召唤场景也能出现团结；
  3. `训练·战斗衍生物`（Creature 1/1 守护，tags=衍生物）：自带「团结4：回合结束全体生命+2」
     （`83c150f2`，engineParam=4，then=`7a97f666` value=2 kinds=[1] count=10——照教程 D1A00012）。
- **费用必须走推导**（AGENTS.md「新卡费用必须走推导」）：两张主卡的 `costList` 由
  `CardCostService`/`CostDerivationService` 从卡内容生成，禁止手填；推导不符合预期时改内容/原子，
  不得硬编码费用。模板卡按现有「衍生物」口径（0 费、不入池）。
- 覆盖矩阵同步；audit 必须看到 `SummonToken` 行与团结评估。

---

## 4. 阶段拆分

### 阶段 1：引擎 + 人类 UI（召唤落位预选）

**目标**：召唤落位在声明期确定，未确定不发动/不发事件；取消/回退干净；触发式/奖励保持默认。

**改动文件**

| 文件 | 改动 |
|---|---|
| `CardCore/Effects/InteractionParams.cs` | 加 `SummonCells` / `HasSummonCells` |
| `CardCore/Effects/DeclarationTargets.cs` | 加 `Interaction` |
| `CardCore/Board/SummonPlacementRules.cs` | **新增**：默认计划/空格/候选/校验/团结预测 |
| `CardCore/Attribute/Handlers/SummonTokenHandler.cs` | 优先消费 `Interaction.SummonCells`；否则默认计划（抽到单源） |
| `CardCore/GameCore/GameActions.PlayCard.cs` | `CollectSpellDeclarationTargetsAsync` 结果可携带 `Interaction`（透传，不改签名语义） |
| `UI/Screens/BattleScreen.Fans.cs` | `SpellDeclarationFlow` 目标收集后调用落位窗；确认后带 `Interaction` 进 `PlayCardFinal` |
| `UI/Screens/BattleScreen.Info.cs` / `BattleScreen.Board.cs` | 主动效果发动前同样接落位窗 |
| `UI/Screens/BattleScreen.Fans.cs`（`PickSummonPlacementAsync`） | 多选/取消/回退/自动填充/确认；复用 `BattleCellPicker` |

**验收**

- 无头探针（TideSim 临时场景或 Editor 验证器）：
  1. 指定 `SummonCells` 落位 5 只 → 实际格与计划一致（逐只断言 `TryGetCell`）；
  2. 不传 `SummonCells`（触发式/奖励）→ 与旧版默认计划逐格一致（A/B 对照）；
  3. 取消路径：不发动 → `CardPlayEvent`/发动事件 0 次、费用无变化、卡仍在原区；
  4. 确定路径：事件 1 次，且 `TokenCreatedEvent` 在事件之后；
  5. 无空格/数量不足：按 §3.2 处理，不静默默认。
- 手操：法术/主动效果弹落位窗，取消回退三次不残留（预览/手牌/钉子）。

### 阶段 2：行动空间（LAE + SimpleAI + 无头装配）

**目标**：AI 能选到落位计划；训练环境与脚本对局装配一致。

**改动文件**

| 文件 | 改动 |
|---|---|
| `AI/NeuralEnv/LegalActionEnumerator.cs` | `InteractionVariants` 召唤分支；`MaxSummonRows=12`；`TideAction.SummonPlan/SummonUnityHits`；`Signature` 加 `SummonCells/Plan`；`[17]` 语义扩展 |
| `AI/SimpleAI.cs` / `AI/AiStrategy.cs` | `PickSummonPlacement`（默认旧行为） |
| `AI/NeuralEnv/TideHeadlessDriver.cs` | `MoveUnitRules.Attach/Detach` + Dispose 清理 |
| `Assets/Editor/验证/AI对战/LaeMultiTargetVerifier.cs` | 召唤落位场景断言 |

**验收**

- LAE：召唤效果行数 = 候选计划数（默认 1 + 空格锚点 ≤9，总 ≤12）；`[18]` 互异；`[19]` 与
  `PredictUnityHits` 一致；无空格 = 0 行；法术召唤行带 `SummonCells`。
- Apply：锚点 8 的计划 → 实际落位以 8 为首（训练/无头同样生效——`MoveUnitRules` 已 Attach）；
  不同计划不被 `FoldEquivalentRows` 误并。
- `SimpleAI`：默认计划不变（600 局镜像逐局 A/B 与阶段 1 前一致）；无落位行时不白付。
- `audit_action_space.py --episodes 100`：**阶段 4 池放开后**复跑——阶段 2 时随机池还没有召唤/团结卡，
  audit 看不到召唤行属正常；阶段 2 以固定盘面 LAE 场景为准。

### 阶段 3：观测（棋盘/团结）

**目标**：AI 能直接看到 9 格占用/邻接、团结邻接数、团结奖励摘要。

**改动文件**

| 文件 | 改动 |
|---|---|
| `AI/NeuralEnv/TideObservation.cs` | `NGlobal 70→88`、`NCard 153→157`、`NAction 18→20`；写 9 格块/邻接/奖励摘要 |
| `CardCore/Effects/CardIdentityService.cs` | `EngineRewardOf`（只读摘要，不改哈希） |
| `tide_rl/tide_features.py` | 常量与注释：`LAYOUT_VERSION=20`、`N_CARD_FEATURES=157`、`N_GLOBAL_FEATURES=88`、`N_ACTION_FEATURES=20`、`MAX_CARDS=191` |
| `tide_rl/export_onnx.py` / `tide_encoder.py` | fixture 补列/dims 自适应（按常量） |
| `Assets/Editor/验证/.../SpecMasterVerifier.cs` | A2 对账 v20/20、157/88 |

**验收**

- 观测维度断言：`NCard=157`、`NGlobal=88`、`NAction=20`；Python 同值（SpecMaster A2）。
- 盘面对拍：给定 5 只衍生物的 2×3 块，C# 与 Python 的 `[153]`/`g[79..87]` 同值；
  战斗衍生物·血（团结4 + 生命+2 奖励）的 `[154..156]` = 1/生命原子类型/2。
- 无棋盘（服务器/纯核心）不炸：块写 0，邻接写 0。

### 阶段 4：训练池 + 全量验收 + 重训

**目标**：池内含召唤/团结卡；v20 定版；旧权重作废；重训交接。

**改动文件**

| 文件 | 改动 |
|---|---|
| `Assets/Data/TrainingData/TrainingCards.json` / `TrainingEffects.json` | 新增 `训练·母巢`、`训练·战斗衍生物`、`训练·团结卫队`（ID/hash 按现有流程） |
| `docs/训练池覆盖矩阵.md` | 入池记录 + 覆盖断言 |
| `docs/AI训练说明.md` | §4.1/§4.3/§4.4/§4.5 更新（v20、召唤落位、团结观测） |
| `docs/AI动作空间补全_修改计划.md` | §3 P2 注：只覆盖打出永久物；召唤落位见本计划 |

**验收**

- `dotnet build Tide.HotUpdate.csproj` / `TideSim/TideSim.csproj` / `TideServer`（后者存量红另记）。
- `dotnet run --project TideSim -- --scenario` 全过；`--games 1 --matchup red-blue --detail` 0 错误。
- `audit_action_space.py --episodes 100`：召唤行出现、0 截断、0 白付；v20 握手。
- `SpecMasterVerifier` PASS（A2 v20/20、157/88、pool 16384/4096）。
- 600 局预设镜像逐局 A/B：阶段 4 前/后（脚本默认计划不变，差异仅来自无头 Attach 修复）。
- 导出链：ONNX fixture/元数据 v20；`OnnxTidePolicy` 拒旧 v19 件；重训待用户开跑。

---

## 5. 版本与特征（v20）

| 项 | v19（现） | v20（本计划） | 备注 |
|---|---|---|---|
| `LayoutVersion` | 19 | **20** | 一次升版；旧 checkpoint/ONNX/fixture 全废 |
| 动作特征 `NAction` | 18 | **20** | +`[18]summonPlan` +`[19]summonUnityHits`；`[17]` 复用 |
| 卡特征 `NCard` | 153 | **157** | +邻接己方数 +奖励计数/类型/值 |
| globals `NGlobal` | 70 | **88** | +9 格占用 +9 格邻接 |
| 卡槽 `MAX_CARDS` | 191 | 191 | 不变 |
| 动作类型 | 14 | 14 | 不变 |
| 卡身份池/类型池 | 16384/4096 | 不变 | 原子哈希不改（奖励摘要不进哈希） |

> 说明：`[19]summonUnityHits` 是「该落位计划落位后立即的团结达标数」——属动作特征（与
> `[5]actionCost` 同层），不是观测泄露；它让模型能把「站位计划」与「团结收益」直接关联。
> **2026-11-20 已拍板保留**（§10-2）；不再保留「只给 `[18]`」的退化分支。

---

## 6. 文件清单（汇总）

| 类别 | 文件 |
|---|---|
| 新增 | `CardCore/Board/SummonPlacementRules.cs` |
| 引擎 | `InteractionParams.cs`、`DeclarationTargets.cs`、`SummonTokenHandler.cs`、`GameActions.PlayCard.cs`（声明收集透传） |
| UI | `BattleScreen.Fans.cs`、`BattleScreen.Info.cs`、`BattleScreen.Board.cs`（落位窗；复用 `BattleCellPicker`） |
| AI | `LegalActionEnumerator.cs`、`SimpleAI.cs`、`AiStrategy.cs`、`TideHeadlessDriver.cs` |
| 观测 | `TideObservation.cs`、`CardIdentityService.cs` |
| Python | `tide_features.py`、`export_onnx.py`、`tide_encoder.py`（按常量自适应） |
| 验证 | `LaeMultiTargetVerifier.cs`、`SpecMasterVerifier.cs`、TideSim 场景 |
| 数据 | `TrainingCards.json` / `TrainingEffects.json`（阶段 4） |
| 文档 | 本文件；`AI训练说明.md`；`AI动作空间补全_修改计划.md`；`训练池覆盖矩阵.md` |

---

## 7. 验收矩阵（最小集）

| # | 断言 | 阶段 | 工具 |
|---|---|---|---|
| 1 | 指定 `SummonCells` → 逐只落位一致 | 1 | TideSim 探针 / Editor |
| 2 | 无 `SummonCells` → 与旧默认逐格一致 | 1 | A/B 探针 |
| 3 | 取消 → 0 事件/0 费/卡留原区 | 1 | Editor 探针 |
| 4 | 确定 → 事件 1 次且先于 `TokenCreatedEvent` | 1 | Editor 探针 |
| 5 | LAE 召唤行数/`[18]`/`[19]`；无空格 0 行 | 2 | LaeMultiTargetVerifier |
| 6 | Apply 落位 + 不同计划不折叠 | 2 | LaeMultiTargetVerifier |
| 7 | `TideHeadlessDriver` 下选格生效 | 2 | 无头探针 |
| 8 | 观测 9 格/邻接/奖励摘要 | 3 | 观测探针 + Python 对拍 |
| 9 | SpecMaster A2 v20/20、157/88 | 3 | SpecMasterVerifier |
| 10 | audit 100 局 0 截断、召唤行出现 | 4 | `audit_action_space.py` |
| 11 | `--scenario` / `--games 1` / 600 局 A/B | 4 | TideSim |
| 12 | 训练池覆盖（召唤/团结各 ≥1 张） | 4 | 覆盖矩阵 + audit |

---

## 8. 风险与回退

| 风险 | 影响 | 处置 |
|---|---|---|
| `Signature` 漏 `SummonCells` | 不同计划被折叠/摘除误并 | 阶段 2 必改并加断言（同 P1 教训） |
| `TideHeadlessDriver` 补 Attach | 训练环境行为变化（P2 落格/召唤聚集真正生效） | 预期正向；600 局 A/B + 重训；若异常可先只 Attach 召唤不接移动（不推荐） |
| 行数膨胀 | 每召唤效果 ≤12 行；多效果/多模式叠加 | `MaxActions=512` 既有 cap；`MaxExpansionRows` 仍兜底 |
| 结算期格被抢 | 竞态下计划格被占 | 最近空格兜底 + 日志；人类声明期已校验 |
| 无棋盘环境 | 服务器/纯核心无法落位 | 无棋盘=默认计划（现有行为）；联机保持默认 |
| 观测/动作一次升版 | 旧权重全废 | 用户已知；一次重训，不做 v20/v21 两次 |
| 训练池改卡 | 卡表 hash/身份变化 | 新建训练卡（不改教程卡）；按现有生成流程算 hash |
| 团结奖励摘要只给首奖励 | 多奖励卡信息不全 | 先 4 维；需要时再扩（不阻塞本轮） |

---

## 9. 不做 / 后续

| 项 | 原因 |
|---|---|
| 联机协议携带 `SummonCells` | 协议 v20 只带 ZoneGroups；联机召唤保持默认计划（另立项） |
| 触发式/奖励召唤预选 | 无声明期；按用户口径属结算期自动 |
| 移动选格口径统一（人类结算期 → 声明期） | 现状允许；另立项，避免与召唤改动同批 |
| 「团结优先」自动计划（除锚点外） | 锚点计划 + `summonUnityHits` 已覆盖学习信号；后续看训练效果再评估 |
| `ScriptedAi` 学落位 | 教学剧本不需要（沿用 v0.3 §8 口径） |
| UI 美术/新预制体 | 复用 `BattleCellPicker`；只加多选/按钮状态 |

---

## 10. 已拍板（2026-11-20）

| # | 项 | 决定 | 落地 |
|---|---|---|---|
| 1 | 训练池来源 | **新建 2 张训练卡**（`训练·母巢` / `训练·团结卫队`）+ 1 张衍生物模板（不入池），不动教程卡 | §3.5 / 阶段 4 |
| 2 | 动作特征 `[19]summonUnityHits` | **保留** | §3.3 / §5 |
| 3 | 人类 UI 粒度 | **逐只点选**（窗内恒有取消/回退/自动填充/确认） | §3.2 / 阶段 1 |
| 4 | 观测奖励摘要 | **本轮一起做**（卡特征 +4 维：邻接数 / 奖励计数 / 首奖励类型 / 首奖励值） | §3.4 / 阶段 3 |

> 此前 §10 的「待拍板」四项已全部收口；实现按以上决定执行，不再在阶段内二次询问。

---

## 11. 文档变更通知（同批，按 AGENTS.md）

| 文件 | 节 | 改什么 | 为什么 |
|---|---|---|---|
| `docs/AI召唤落位与团结观测_修改计划.md` | 全文 | **新增**本计划 | 用户 2026-11-20 要求出计划 |
| `docs/AI训练说明.md` | §4.1 第 8 条 | 「永久物/召唤落格 ✅」改为「永久物 ✅；效果召唤落位 ❌（本计划 v20）」 | 现状与实现不符（P2 只覆盖打出永久物） |
| `docs/AI训练说明.md` | §4.3 | 版本链加 v20 条目（动作 20/卡特征 157/globals 88；召唤落位 + 团结观测） | 契约单一来源 |
| `docs/AI训练说明.md` | §4.4/§4.5 | 冻结面与状态表同步 v20；召唤落位/团结观测标「待实现」→ 验收后「已实现」 | 文档与实现一致 |
| `docs/AI训练说明.md` | §4 引用 | 删除/修正两处悬空链接 `AI绿套学习与观测补盲_修改计划`（文件不存在） | 悬空引用误导 |
| `docs/AI动作空间补全_修改计划.md` | §3 P2 | 加注：P2 落格只覆盖「打出永久物」；效果召唤落位见本计划 | 防止再次误标 ✅ |
| `docs/训练池覆盖矩阵.md` | 新增卡节 | 阶段 4 入池后更新覆盖表 | 训练池变更留痕 |

> 以上除本文件外，其余文档改动在**对应阶段落地时**同批提交；本计划先登记「要改什么」。

---

## 12. 交付物

| # | 交付 | 说明 |
|---|---|---|
| 1 | 代码 | 阶段 1–3 文件清单（§6）；`SummonPlacementRules` 单源；`TideHeadlessDriver` 装配修复 |
| 2 | 数据 | 训练卡 2 张（`训练·母巢`/`训练·团结卫队`）+ 衍生物模板 1 张（不入池）+ 覆盖矩阵 |
| 3 | Python | `tide_features.py`/`export_onnx.py`/`tide_encoder.py` v20 同步 |
| 4 | 证据 | 验收矩阵 12 项输出：探针、LAE 行、观测对拍、SpecMaster、audit 100 局、600 局 A/B |
| 5 | 文档 | 本文件 + §11 所列更新；改完按「文档变更通知」汇报 |

---

## 13. 给 ZCode 的提示词（可直接复制；一次只派一个阶段）

### 阶段 1

```
读取 docs/AI召唤落位与团结观测_修改计划.md §3.1/§3.2/§4 阶段1，只做阶段1（引擎+人类UI）。
目标：效果召唤衍生物的落位改为声明期预选——InteractionParams 加 SummonCells（己方 UnitCells 下标），
DeclarationTargets 携带 Interaction；SummonTokenHandler 优先按 SummonCells 逐只落位，无参回落默认计划；
新增 CardCore/Board/SummonPlacementRules.cs（默认计划抽离、空格、校验、团结预测）；UI 在法术/主动效果
声明期弹落位窗（逐只点选/取消/回退/自动填充/确认），取消=撤销发动（0事件/0费），确认后才 PlayCard/Activate。
约束：触发式/奖励召唤不弹窗、走默认；不改联机协议/卡表/数值；默认计划与旧版逐格一致；不改 AI 动作空间。
验收：§7 第1–4项；贴探针输出（逐只落位、取消0事件、事件先于 TokenCreatedEvent、无参A/B一致）。
做完停，不进入阶段2。
```

### 阶段 2

```
读取 docs/AI召唤落位与团结观测_修改计划.md §3.3/§4 阶段2，只做阶段2（行动空间）。
目标：LAE 对含 SummonToken 的主动/施放效果生成落位计划行（默认+锚点，≤12行）；TideAction 加
SummonPlan/SummonUnityHits；Signature 加 SummonCells/Plan；特征 [17] 复用锚点、新增 [18][19]；
SimpleAI 加 PickSummonPlacement（默认旧行为）；TideHeadlessDriver 补 MoveUnitRules.Attach/Detach。
约束：不改联机协议/卡表；无空格=0行；不同计划不得被折叠误并；阶段1须已验收。
验收：§7 第5–7项（LAE行数/坐标/Apply/折叠/无头选格生效）+ 固定盘面场景；audit 留阶段4。
做完停，不进入阶段3。
```

### 阶段 3

```
读取 docs/AI召唤落位与团结观测_修改计划.md §3.4/§5/§4 阶段3，只做阶段3（观测）。
目标：TideObservation 增 9 格占用/邻接块（globals 70→88）、卡特征邻接数+奖励摘要（153→157）、
NAction 18→20；CardIdentityService 加 EngineRewardOf（只读，不改 AtomHash/ContentId）；
Python 常量 v20 同步；SpecMaster A2 对账。
约束：不改动作行集（阶段2已定）；奖励摘要不进身份哈希；无棋盘写0；不改联机/卡表数值。
验收：§7 第8–9项（C#/Python 对拍、团结卡邻接/奖励摘要、SpecMaster PASS 16/0）。
做完停，不进入阶段4。
```

### 阶段 4

```
读取 docs/AI召唤落位与团结观测_修改计划.md §3.5/§4 阶段4，只做阶段4（训练池+全量验收）。
目标：TrainingCards/TrainingEffects 新增训练·母巢/训练·战斗衍生物/训练·团结卫队（含 SummonToken 与
团结4）；覆盖矩阵更新；跑 audit 100 局、TideSim --scenario、600 局镜像 A/B、SpecMaster、导出链；
确认 v20 握手、旧 ONNX 拒载。
约束：不动教程卡 hash；不改卡表数值平衡；联机不携带落位参数。
验收：§7 第10–12项 + 重训交接清单。做完停，等用户开训。
```
