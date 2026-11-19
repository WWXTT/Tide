# AI 动作空间·移动与宣言 规格 v0.3（2026-11-15）

> **2026-11-19 晚状态：本规格已实施（P1，[AI动作空间补全_修改计划](AI动作空间补全_修改计划.md) §2）**：
> `InteractionParams` 透传链落地（声明入栈→结算 handler 消费）、LAE 移动行=目标×合法空格（MaxMoveRows=18）
> /宣言行=候选名（MaxDeclareRows=16）、`UnsupportedForAi` 对 MoveUnit/SearchDeck/DeclareHand/DeclareDeck
> 的裁剪删除、SimpleAI 接 `PickMoveCell`/`PickDeclaration`、动作 **14→17 维**（`[14]moveCellX/[15]moveCellZ/
> [16]declaredNameIndex`）。**布局号 v17**（v16 已被 P-1 稳定下标占用——本文「用 v16」审计注作废）。
> 验收：LaeMultiTargetVerifier ⑥ 场景（§6.2/6.3/6.4）全过；§6.5 回归+SpecMaster+audit 100 局全绿。
> 旧口径（NAction=14/LAYOUT_VERSION=15/无 InteractionParams）已失效。

> 给 ZCode 的实现规格。承 [目标候选区域信息_共享规格_v0.2](目标候选区域信息_共享规格_v0.2.md)。
> v0.2 只把「候选区域」变成数据；本规格补上 **AI/训练缺的两类交互参数**：
> **移动选格**、**宣言卡名**。脚本（`ScriptedAi`）不学这两项（用户定案），只做 `SimpleAI` + `LegalActionEnumerator`。
>
> **顺序**：v0.2 先落（它改 `LegalActionEnumerator.cs`、`TideObservation.cs`、Python 常量）；
> 本规格在它之后做，避免同一文件并发。特征维一次算到 **17**（v0.2 的 12→14 + 本规格 +3）。

---

## 0. 派活摘要

| 项 | 内容 |
|---|---|
| 目标 | `SimpleAI` 与 RL/训练都能做「移动选哪格」「宣言哪个卡名」，不再整体跳过这两个决策 |
| 口径 | 移动：选合法空格；宣言：**只使用已读到的卡名**（`ExposedCounter` 展示指示物），没读到→不发动 |
| 新增 | `CardCore/Effects/InteractionParams.cs`（参数通道载体） |
| 改 | `EffectExecutionContext` / `EffectInstance` / `PendingEffect` / `EffectExecutionEngine` / `GameActions.{PlayCard,CombatTurn,Costs}` / `MoveUnitHandler` / `ProphecyHandlerUtil.AskCardNameAsync` / `LegalActionEnumerator` / `TideObservation` / `AiStrategy` / `SimpleAI` / `tide_rl` 常量与 fixture |
| 不改 | `ScriptedAi`（教程剧本）、联机协议、卡表/数值、UI（除 v0.2 已定的） |
| 特征 | `NAction` 14→**17**，`LayoutVersion` 升一号（**2026-11-18 审计注：原计划的 v15 已被「场地特殊化拆槽」占用〔v15/116/191，动作仍 14 维〕，本规格实施时须改用 **v16**） |
| 验收 | 编译 + 无头探针（有展示/无展示、有空格/无空格、行枚举、SimpleAI 不白付） |

---

## 1. 口径（用户确认）

### 1.1 移动
- 目标：`MoveUnit` 原子把单位移到**己方单位区空格**（`MoveUnitRules.LegalDestinations(unit)`）。
- AI：从合法空格里选一格（策略可覆写，默认首格）；无空格 → 不发动/空转。
- 人类路径（`CellSelectionService` UI 桥）不变。

### 1.2 宣言卡名
- **AI 允许用对手卡组信息，但只认「已读到的」**：目标区里带 `ExposedCounter`（展示指示物）的卡，
  换区即清（`RevealRules` 既有生命周期）。
- **按区间隔，不混**：强夺只从「对手手牌中已展示的卡」取名；窃取只从「对手牌库中已展示的卡」取名。
- 检索（己方牌库）：用**己方牌库去重卡名**（己方信息，天然全可读）。
- **候选为空 → 不发动**（不是发动后空转）。

| 原子 | 候选名来源 | 空集 |
|---|---|---|
| `SearchDeck`（检索） | 己方牌库 `distinct CardName` | 不发动 |
| `DeclareHand`（强夺） | `RevealRules.GetExposedCards(zm, opponent, Zone.Hand)` 的 distinct `CardName` | 不发动 |
| `DeclareDeck`（窃取） | `RevealRules.GetExposedCards(zm, opponent, Zone.Deck)` 的 distinct `CardName` | 不发动 |

> 因此 AI 宣言**不会空放**：候选都来自"此刻还在该区、且已展示"的卡，必命中（除非同时点被换区，
> 换区即清展示——以结算期实际区为准，属正常竞态）。

### 1.3 脚本
- `ScriptedAi` 不改：`targets` 仍是卡 ID/hero/auto；这两类效果在剧本里不展开。

---

## 2. 引擎参数通道（两条共用）

### 2.1 载体

新增 `CardCore/Effects/InteractionParams.cs`：

```csharp
namespace CardCore
{
    /// <summary>声明期交互参数（2026-11-15 移动/宣言）：随发动入栈、结算期由 handler 消费。
    /// null=无（行为与旧版完全一致，UI 桥/预置照旧）。</summary>
    public sealed class InteractionParams
    {
        public string DeclaredName;   // 宣言卡名（原始卡名，不含 "Name:" 前缀）
        public int MoveX = -1;        // 移动落格（-1=未指定）
        public int MoveZ = -1;
        public bool HasMoveCell => MoveX >= 0 && MoveZ >= 0;
    }
}
```

### 2.2 透传链

```
TideAction.Interaction（LAE）/ SimpleAI 构造
  → GameActions.PlayCard / PlayCardInResponse / ActivateEffect / ActivateHeroSkill（新增 optional 参数）
  → PendingEffect.Interaction / EffectInstance.Interaction（cast 亦同）
  → EffectExecutionEngine 执行时写入 EffectExecutionContext.Interaction
  → 消费：MoveUnitHandler（格子）/ ProphecyHandlerUtil.AskCardNameAsync（卡名）
```

- `EffectExecutionContext` 加 `public InteractionParams Interaction;`（null=旧行为）。
- `EffectInstance` 加 `public InteractionParams Interaction { get; set; }`；`FromPendingEffect` 复制。
- `PendingEffect` 加同名字段；`Create` 不动，赋值同 `SelectedTargets`。
- `PushCardCast(...)` 增 optional `InteractionParams interaction = null`，写进 cast 的 `EffectInstance`。
- 所有新增参数一律 **trailing optional**，旧调用点零改动。

### 2.3 handler 消费

- `ProphecyHandlerUtil.AskCardNameAsync`（`ProphecyHandlers.cs:450`）：
  ```csharp
  string preset = effect.StringValue;
  // 2026-11-15：声明期参数优先（AI/训练）——跳过文本窗，按同一编码写回
  if (!string.IsNullOrEmpty(context.Interaction?.DeclaredName))
  {
      effect.StringValue = nameEncoded
          ? ProphecyDimension.Name + ":" + context.Interaction.DeclaredName
          : context.Interaction.DeclaredName;
      return true;
  }
  ...原文本窗逻辑不动...
  ```
  （沿用现有「写回 `effect.StringValue`」口径，检索/强夺/窃取三个 handler 零改动。）
- `MoveUnitHandler.ExecuteAsync`：
  ```csharp
  var asked = context.Interaction;
  if (asked != null && asked.HasMoveCell)
  {
      if (!cells.Any(c => c.x == asked.MoveX && c.z == asked.MoveZ)) continue; // 非法格=跳过
      if (!GameBoard.MoveUnitRules.TryMove(unit, asked.MoveX, asked.MoveZ)) continue;
      // 事件照发（与现有 KeywordAppliedEvent 同段）
      continue;
  }
  ...原 CellSelectionService 分支不动...
  ```

---

## 3. 移动：LAE 行 + SimpleAI

### 3.1 LAE 行（`LegalActionEnumerator`）

- `TideAction` 加 `public InteractionParams Interaction;`。
- **删除** `UnsupportedForAi` 对 `MoveUnit` 的裁剪；改为展开：
  - 在 `ExpandDefinitionTargets`/`AddActivatedRows` 之后，对含 `MoveUnit` 的原子：
    - 取该原子解析出的目标（通常 1 个单位）；
    - `var cells = GameBoard.MoveUnitRules.LegalDestinations(unit)`；
    - 每个 `(target, cell)` 一行（单目标 v1；多目标移动 → 该效果仍按旧口径**不枚举**，见 §8）。
  - 行数上限：每效果 `MaxMoveRows = 18`（9 格 × 2 目标以内），超出截断。
- `Apply`：`a.Interaction` 随 `PlayCard/ActivateEffect/...` 传入。

### 3.2 SimpleAI

- `AiStrategy` 加：
  ```csharp
  /// <summary>移动落格（2026-11-15）：cells=合法空格；默认首格（与 first-free 同序）。</summary>
  public virtual (int x, int z) PickMoveCell(GameCore core, Player me, Card unit,
      IReadOnlyList<(int x, int z)> cells)
      => cells != null && cells.Count > 0 ? cells[0] : (-1, -1);
  ```
- `SimpleAI`：含 `MoveUnit` 的效果不再被 `UnsupportedForAi` 跳过；发动前构造
  `InteractionParams { MoveX/MoveZ = _strategy.PickMoveCell(...) }` 并传 `ActivateEffect(..., interaction)`；
  合法空格=0 → 跳过（记一次 `LogUnsupportedSkip` 同款日志）。

---

## 4. 宣言：候选名 + LAE 行 + SimpleAI

### 4.1 候选名单源

在 `CardCore` 加（放 `InteractionParams.cs` 同文件或 `TargetZoneGroups.cs` 旁）：

```csharp
public static class DeclarationCandidates
{
    /// <summary>AI/训练可用的宣言候选（2026-11-15 用户口径：只使用已读到的）：
    /// SearchDeck→己方牌库去重名；DeclareHand/DeclareDeck→对手手牌/牌库中已展示卡的去重名。
    /// 其他原子/无候选=空表。</summary>
    public static List<string> For(GameCore core, Player me, AtomicEffectInstance atom);
}
```

- 实现：`me` / `me.Opponent` + `ZoneManager` + `RevealRules.GetExposedCards`；`distinct`（保序）。
- 去重键=`CardName`（空名跳过）。
- 结果缓存不必要（每决策算一次）。

### 4.2 LAE 行

- `UnsupportedForAi` 删掉 `SearchDeck/DeclareHand/DeclareDeck` 的静态裁剪；改为按原子展开：
  - 每个候选名一行，`Interaction.DeclaredName = name`；上限 `MaxDeclareRows = 16`（超出按候选序截断）。
  - 候选为空 → 该效果**不产生行**（=AI 不发动）。
- 特征：见 §5（`DeclaredNameIndex` 用候选表序号 0..15）。

### 4.3 SimpleAI

- `AiStrategy` 加：
  ```csharp
  /// <summary>宣言卡名（2026-11-15）：candidates=已读到的卡名（非空）；默认取首。</summary>
  public virtual string PickDeclaration(GameCore core, Player me, Card source, string atomType,
      IReadOnlyList<string> candidates)
      => candidates != null && candidates.Count > 0 ? candidates[0] : null;
  ```
- `SimpleAI`：含宣言原子的效果发动前取候选；**空集 → 不发动**（`LogUnsupportedSkip` 记一条
  「无已读卡名」）；非空 → 传 `InteractionParams { DeclaredName = pick }`。

---

## 5. 动作特征（最终 17 维）

| 维 | 名称 | 取值 | 来源 |
|---|---|---|---|
| 0-11 | 既有 | 不变 | v13 布局 |
| 12 | `targetZoneRank` | -1 / 0..9 | v0.2 |
| 13 | `targetSideRank` | -1 / 0 / 1 | v0.2 |
| **14** | `moveCellX` | -1 / 0..宽 | 本规格：`Interaction.MoveX` |
| **15** | `moveCellZ` | -1 / 0..高 | 本规格：`Interaction.MoveZ` |
| **16** | `declaredNameIndex` | -1 / 0..15 | 本规格：候选表序号（同效果内可比；空=-1） |

- `TideObservation.NAction` → **17**；`LayoutVersion` 升一号（**2026-11-18 审计注：v15 号已被场地特殊化占用，实施时用 v16**；当前基线=v15/191/动作 14 维）。
- Python：`tide_rl/tide_features.py`：`LAYOUT_VERSION` 同步升号、`N_ACTION_FEATURES = 17`；
  `export_onnx.py` fixture 补 14/15/16 三列。
- `docs/AI训练说明.md`：冻结面同步改口径；历史沿革加一行（移动/宣言参数 + 区域特征）。

---

## 6. 验收

### 6.1 编译

```powershell
dotnet build Tide.HotUpdate.csproj
dotnet build TideSim/TideSim.csproj
```

0 错误。

### 6.2 移动

- 无头探针（TideSim 临时探针或现有场景）：
  - 盘面：己方 1 单位 + 1 空格；`MoveUnit` 效果发动，`Interaction.MoveX/Z` 指定空格 → 单位实际移动，
    事件 `KeywordAppliedEvent` 的 Detail 含目标格；
  - 指定非法格 → 不移动、不白付（有日志）；
  - 无空格 → 不发动。
- `SimpleAI`：含 MoveUnit 的效果不再出现 `[MoveUnit] …空转`，而是移动成功/无空格跳过。

### 6.3 宣言

- 盘面 A：对手手牌 1 张卡挂 `ExposedCounter` → `强夺` 候选=该卡名；发动后命中并夺取；
- 盘面 B：对手手牌无展示 → 候选空 → AI 不发动（日志「无已读卡名」）；
- 盘面 C：对手牌库展示 1 张 → `窃取` 候选=该卡名；
- 盘面 D：检索：候选=己方牌库去重名，发动后检索到该卡；
- 候选不混区：手牌展示的卡不应出现在窃取候选里，反之亦然（断言两条候选集合互不包含）。

### 6.4 LAE

- 有展示的强夺效果：动作行数 = 候选名数（≤16），每行特征 [16] 互不相同；
- 无展示：该效果 0 行；
- MoveUnit：动作行数 = 目标数 × 空格数（≤18），[14]/[15] 与格坐标一致；
- 无空格：0 行。

### 6.5 回归

- `TideSim --scenario` 全过；`--games 1 --matchup red-blue` 正常完局；
- `ScriptedAi` 教学剧本行为不变（basics 全段）；
- `SpecMasterVerifier`（C#/Python 常量对账）通过。

---

## 7. 约束

1. 不改 `ScriptedAi`；不改联机协议（网络 AI 的移动/宣言参数另立项）。
2. 不改卡表/数值/目标域；`UnsupportedForAi` 只删「MoveUnit/宣言」两类，其余（未来新增）保持。
3. 参数一律 trailing optional，旧调用点零改动；`InteractionParams == null` 时行为与现在**逐字节一致**。
4. 宣言候选只来自 `RevealRules`/己方牌库——**禁止**枚举对手未展示卡名、全池卡名。
5. 与 v0.2 串行；两者都改 `LegalActionEnumerator.cs`，不要并发。
6. 特征维只升一次（17/15），Python 同批同步。

---

## 8. 不做

| 项 | 原因 |
|---|---|
| 多目标 MoveUnit 的逐单位落格 | handler 是逐 target 循环；v1 只枚举单目标，多目标仍不产生行 |
| 联机携带 InteractionParams | 协议 v20 只带 ZoneGroups；网络 AI 另立项 |
| 脚本学移动/宣言 | 用户定案：教程剧本不需要 |
| 宣言“猜未读卡” | 用户定案：只用读到的，没读到不发动 |

---

## 9. 交付物

| # | 交付 | 说明 |
|---|---|---|
| 1 | 代码 | 新增 `InteractionParams.cs`（含 `DeclarationCandidates`）；改 §0 列出的引擎/AI/观测文件 |
| 2 | Python | `tide_features.py`（15/17）、`export_onnx.py` fixture；`docs/AI训练说明.md` |
| 3 | 证据 | 编译输出；§6.2/6.3/6.4 探针输出（移动坐标、候选集合互斥、行数、空集）；§6.5 回归 |
| 4 | 说明 | 改动文件清单；`InteractionParams` 透传链；未做项（§8） |

---

## 10. 给 ZCode 的提示词（可直接复制）

```
读取 docs/AI动作空间_移动与宣言_规格_v0.3.md（依赖 docs/目标候选区域信息_共享规格_v0.2.md 已落），
实现「移动选格 + 宣言卡名」进 SimpleAI 与 LAE 动作空间。

口径（用户确认）：移动选合法空格；宣言只使用已读到的卡名——强夺只取对手手牌中带 ExposedCounter
的卡名、窃取只取对手牌库中带 ExposedCounter 的卡名、检索取己方牌库去重卡名；候选为空=不发动。
按区间隔，不混。脚本 ScriptedAi 不改。

约束：
- 只改规格 §0 列出的文件；不碰 ScriptedAi、联机协议、卡表数值；
- InteractionParams 为 trailing optional，null 时行为与现状完全一致；
- 禁止枚举对手未展示卡名/全池卡名；
- 与 v0.2 串行（都改 LegalActionEnumerator.cs），先确认 v0.2 已收口；
- 特征维一次升到 17、LayoutVersion 15，Python 同批同步。

完成后提交：① 改动文件与方法清单；② 编译输出；③ §6.2/6.3/6.4 证据（含「手牌展示名不进窃取候选」
的断言）；④ 未做项（应为 §8 列表）。
```
