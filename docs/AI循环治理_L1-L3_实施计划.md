# AI 循环治理 L1–L3 实施计划（ZCode 执行）

> 状态：**四阶段已全部实施并入提交**（预算/指纹/自检开关：`TideHeadlessDriver.cs`；L3-A 协议：`TideHeadlessServer.cs` + `rollout_common.py`；L3-B 折叠：`LegalActionEnumerator.cs`；见 `27f7c2f6d`/`60a517d33`。2026-11-18 审计注）
> 依据：[AI循环与空转治理方案.md](AI循环与空转治理方案.md) §5.2–§5.6（问题、根因、指纹/预算/剪枝/折叠的定义都在那边，本文件只写"怎么做、怎么验收"）
> 前置：L0 充盈触发=**第三版「己方释放法术后·仅法术」**（`Assets/Scripts/HotUpdate/CardCore/Effects/BranchEngines.cs`，`27f7c2f6d` 定稿；当日早先的「仅对手回合」第二版已被取代）
> 执行方式：**一次只做一个阶段**。做完立即停手，交付「改动文件清单 + 验收输出 + 疑问」，等验收通过再进下一阶段。

---

## 0. 目标

让 AI 对局在**合法循环**（净零循环 / 增长循环）下不再空转：

1. 单回合动作数有界（预算 60），不会撞 256 上限烧满 2000 步；
2. 净零循环（如绿套：牺牲地→回收墓地→铺地→再牺牲）被指纹识别并**收口本回合**；
3. 空转步**不进 PPO 批次**（复用 `learner` 掩码）；
4. 动作表不再被同一张牌的重复行撑爆（深潭碑 169 行 → 1 行）；
5. `stats.jsonl` 能看到循环事件数，未来新循环能第一时间冒头。

---

## 1. 红线（违反即返工）

- ❌ 不改奖励函数 / λ / GAE（终局 ±1 + 塑形 0.005 保持原样）
- ❌ 不改 obs 维度、动作维度、布局版本（v14 / 116 / 14 冻结）
- ❌ 不改卡表 / 效果表 / 卡身份（不碰 `TutorialCards.json` / `TutorialEffects.json` 的卡内容）
- ❌ 不改 `BranchEngines.cs`（L0 已定稿）
- ❌ 不加"判和"逻辑（循环只收口本回合，不判和——避免"劣势方拖和"）
- ❌ 不碰 `C:\UnityProject\Tide_train`（训练副本，由 DSH 统一刷新）
- ❌ 不做无关重构、不改无关文件、不顺手升级依赖

---

## 2. 仓库与验证方式

- **主工程（唯一改动目标）**：`C:\Users\Administrator\Desktop\UnityProject\Tide`
- **探针**：`C:\Users\Administrator\Desktop\UnityProject\Tide\.ztmp\probe_action_table.py`（DSH 维护，可直接用）
  - 用法：`python probe_action_table.py --project C:\Users\Administrator\Desktop\UnityProject\Tide --episodes 4 --max-steps 300 --policy playcard --big 90 --out probe_stageN.json`
  - 它会自己起 Unity batchmode（首次编译约 5~8 分钟）→ 跑局 → 打印每局 `steps/turns/max_valid/playcard/endturn` → 落 JSON
- **不要在训练运行时跑探针**（同一工程起第二个 Unity 实例会抢工程锁）。
- 验收输出统一贴：探针最后 20 行 + 你新增的 `[LoopGuard]` 日志（若有）+ 改动的 diff 摘要。

---

## 3. 阶段 1：L2 回合预算 60

### 目标
单回合动作数达到 60 就强制收口本回合（不判和、不改奖励），把"空转烧满 2000 步"的损失封顶到 60 步。

### 改动文件
`Assets/Scripts/HotUpdate/AI/NeuralEnv/TideHeadlessDriver.cs`

### 实现要点
1. 常量：`private const int TurnActionBudget = 60;`（放在 `MaxActionsPerTurn` 旁边，注释写明"模拟真人墙钟，AI 专用，不进正式规则"）
2. 在 `Step()` 里，`_actionsThisTurn++;` 之后、现有 `if (_actionsThisTurn > MaxActionsPerTurn)` 之前插入：

```csharp
if (_actionsThisTurn >= TurnActionBudget && !_budgetClosedThisTurn)
{
    _budgetClosedThisTurn = true;
    _loopBudgetCount++;
    if (!_gameOver && !_core.StackEngine.IsEmpty) GameActions.DrainStack(_core, 64);
    if (!_gameOver && _core.TurnEngine.CurrentPhase?.Phase == PhaseType.Main && _core.StackEngine.IsEmpty)
        GameActions.EndTurn(_core, _core.TurnEngine.TurnPlayer);
    Console.WriteLine($"[LoopGuard] budget turn={_core.TurnEngine.TurnNumber} seat={SeatOf(_decider)} actions={_actionsThisTurn} n={_legal?.Count ?? 0} hand={_core.ZoneManager.GetCards(_decider, Zone.Hand)?.Count ?? 0}");
}
```

3. `_budgetClosedThisTurn` 在回合切换处（`BuildResult` 里 `_banTurnStamp != turn` 分支）复位为 `false`；`_loopBudgetCount` 只增不清。
4. **不要删 `MaxActionsPerTurn = 256`**：它保留为硬保险（防实现 bug 导致完全无法收口）。
5. 注意 `EndTurn` 可能被引擎拒绝（阶段/栈态）；本阶段只要求"尽力收口 + 打日志"，不要求 100% 成功。

### 验收
- Unity 编译通过（探针能连上、能跑局）
- 探针 4 局：`max_valid` 正常、没有 2000 步级长局、对局都能自然终局
- `[LoopGuard] budget` 日志中出现 `actions>=60` 的记录（若 60 局内一次没触发，也算通过，但要贴出"未触发"的说明）

### 停止
完成上述即停，不要进入阶段 2。

---

## 4. 阶段 2：L1 无进展指纹

### 目标
识别"合法净零循环"：同一回合内局面回到之前出现过的状态，或连续多步局面完全不变 → 收口本回合。

### 改动文件
`TideHeadlessDriver.cs`（同一文件）

### 实现要点
1. 指纹（每步 `BuildResult` 之后算一次，只在 `!_responseStop` 时）：

```csharp
private ulong ComputeFingerprint()
{
    // 只取"净变化"项：回合号/双方生命/战场名字多重集(排序)/双方 手牌·牌库·墓地·除外 张数/元素池张数与横置数
    var sb = new StringBuilder(); // using System.Text
    sb.Append(_core.TurnEngine.TurnNumber).Append('|');
    sb.Append(_core.Player1.Life).Append(',').Append(_core.Player2.Life).Append('|');
    foreach (var p in new[] { _core.Player1, _core.Player2 })
    {
        foreach (var z in new[] { Zone.Hand, Zone.Deck, Zone.Graveyard, Zone.Exile })
            sb.Append(zm.GetCards(p, z).Count).Append(',');
        var names = zm.GetCards(p, Zone.Battlefield).Select(CardNameOf).OrderBy(x => x, StringComparer.Ordinal);
        sb.Append(string.Join("/", names)).Append(',');
        sb.Append(_core.ElementPool.GetPooledCards(p).Count).Append(',').Append(_core.ElementPool.GetPooledCards(p).Count(x => x.IsTapped)).Append('|');
    }
    return Fnv1a64(sb.ToString()); // 自己实现 20 行 FNV-1a；不要引依赖
}
```

> 说明：`Zone.Exile` 若不存在就跳过该区域；`CardNameOf` 可复用 `(c as CardWrapper)?.GetData()?.CardName ?? "?"`。卡面名字相同即视为同物（不需要实例 ID）。

2. 状态：`private readonly HashSet<ulong> _turnFingerprints = new();`、`private int _stallCount;`、`private int _loopCycleCount; private int _loopStallCount;`
3. 判定（收口动作与阶段 1 完全一致，抽成私有方法 `CloseTurnByLoopGuard(string reason)` 复用）：
   - **回环**：`!_turnFingerprints.Add(fp)` → 已存在 → `reason="cycle"`；
   - **停滞**：`fp == _lastFingerprint` 连续 `StallThreshold = 8` 次 → `reason="stall"`；否则 `_stallCount=0`。
   - 回合切换时清空 `_turnFingerprints`、`_stallCount`。
4. 收口时把当前动作 `Signature` 加进 `_bannedThisTurn`（复用现有字段）并 `_legal.RemoveAll(_bannedThisTurn)`。
5. 日志：`[LoopGuard] cycle ...` / `[LoopGuard] stall ...`（带 turn/seat/actions/n/hand）。
6. 自检开关：`private const bool LoopGuardSelfTest = false;` —— 置 true 时 `StallThreshold = 1`，用于验证检测器真的会触发（验收后改回 false）。

### 验收
- 探针 6 局（正常模式）：`[LoopGuard] cycle/stall` **0 次**（不许误报）
- 探针 1 局（`LoopGuardSelfTest = true`）：能触发一次收口并打印日志，随后把开关改回 `false`
- 编译通过、对局正常终局

### 停止
完成上述即停。

---

## 5. 阶段 3：L3-A 空转步剪枝

### 目标
被收口的那一回合的所有决策步，从 PPO 批次里剔除（复用现成的 `learner` 掩码通道）。

### 改动文件
1. `TideHeadlessDriver.cs` + `Assets/Editor/验证/AI对战/TideHeadlessServer.cs`（协议字段）
2. `tide_rl/rollout_common.py`、`tide_rl/train_tide_complete.py`（Python 侧）

### 实现要点
1. `TideStepResult` 增：`public bool LoopPruned;`（本次是收口那一步）、`public int TurnActions;`（本回合已用动作数）。
2. `TideHeadlessServer` 的 `HeadlessInfo` 增 `public bool loopPruned; public int turnActions;` 并赋值。
3. `rollout_common.py`：记录每回合起点下标 `turn_start`（用 `info["turn"]` 变化检测）；回合结束/对局结束时，
   若该回合内出现过 `info["loopPruned"]`，则 `learner[turn_start:end] = 0`（`learner` 是 bool 列表/数组）。
4. `train_tide_complete.py`：从 rollout 数据统计 `loop_events`（cycle/stall 次数）、`loop_budget`、
   `pruned_steps`（被置 0 的步数）、`max_turn_actions`，写进 `stats.jsonl` 并打印在 update 行尾。
5. 配置：`Args` 加 `prune_loop_steps: bool = True`，CLI `--no-prune` 可关（回滚用）。

### 验收
- 训练 10 分钟：`stats.jsonl` 出现四个新字段；`pruned_steps` 为 0（环境已修，正常不该有）
- 人为把 `TurnActionBudget` 临时改成 5 → `pruned_steps > 0`、`loop_budget > 0`，PPO 正常更新（loss 不 NaN/Inf）
- 改回 60，重跑 2 分钟确认恢复正常

### 停止
完成上述即停。

---

## 6. 阶段 4：L3-B 动作表折叠

### 目标
同一张牌在同模式下产生的多条**完全等价**动作行（只差"手牌第几格"）折叠成一行。

### 改动文件
`Assets/Scripts/HotUpdate/AI/NeuralEnv/LegalActionEnumerator.cs`

### 实现要点
1. 在 `Enumerate()` 里 `BuildFeatures()` **之前**、`EnforceActionCap(...)` **之后**（或之前，二者皆可，但必须保证 Features 与 Actions 同步重建）做去重：
   - 键 = 该行特征去掉 `[2] sourceIndex` 后的字符串/ulong（用 `Features` 数组拼，注意 double 格式化用 `R`）。
   - 同键只保留第一行（保留哪一行无所谓，模型不可区分）。
2. `EnumerateResponses` **不要改**（响应行数量本来就小）。
3. 去重后若 `Actions.Count` 变少，`Features` 必须按新表重建（调用 `BuildFeatures()` 一次即可）。
4. 日志（可选，便于验收）：`[ActionFold] raw={n} folded={m}`（仅在 `raw != m` 时打印，避免刷屏）。

### 验收
- 探针 6 局：`n_valid` 峰值比改动前下降（深潭碑类局面应显著下降）
- `audit_action_space.py`（若可运行）100 局：0 截断、动作类型分布没有异常变空
- 训练 10 分钟曲线正常

### 停止
完成上述即停，交付总结。

---

## 7. 交付模板（每个阶段都要给）

```
阶段：N
改动文件：
  - path/to/A.cs（+12 -0）
  - path/to/B.py（+30 -2）
实现摘要：3~5 行
验收输出：
  - 探针：<最后 20 行>
  - 日志：<[LoopGuard] 样例>
  - 训练（阶段 3/4）：<stats 新字段样例>
疑问 / 风险：
  - ...
```

---

## 8. 已知坑（提前说明）

1. **别在训练运行时起探针**：同工程两个 Unity 实例会抢锁。
2. `BuildFeatures()` 依赖 `_core/_me`；去重后务必重建，否则特征和动作表错位（会直接毁训练）。
3. `EndTurn` 可能被引擎拒绝（栈非空/相位不对）；收口路径要"尽力而为 + 打日志"，不要抛异常中断对局。
4. 指纹不要包含实例 ID / 卡编号 / 时间戳，否则永远不相等。
5. `learner` 掩码是**逐步**的，剪枝要在 rollout 记录阶段就置 0，不要等 PPO 更新时再过滤。
6. Python 侧改动只改主工程的 `tide_rl/`，`Tide_train` 由 DSH 刷新。
