# D2d 对手回合响应停靠 · 实施方案（2026-11-22）

> 目标：让**模型**在**脚本回合内**的响应窗里拿到决策权（与脚本座同权），
> 消除「模型执蓝三格对脚本不公平」的偏差（`红绿0胜归因_修改计划.md` §D2c 遗留）。
> 一次性落地：引擎异步口 + 驱动停靠/续跑 + 测试矩阵。**表现层（TapTap/UI）不在范围内。**

---

## 0. 已验前提（不必再验）

| 前提 | 证据 |
|---|---|
| TCS 完成即**内联续跑**，不依赖 player loop（主线程全程阻塞也成立） | `.ztmp/tcs_continuation_probe.json`：C1 跨线程 / C2 同线程 / D 引擎级 `SimpleAI.TakeTurnAsync` 停-续全绿，`lastTurnDoneTid=72` |
| 引擎本来就 `await` 人类响应口 | `GameActions.ResponseWindow.cs` L246-248：`choice = await ResponseWindowService.HumanResponder(holder, options)` |
| 但 AI 座被 `IsAI` 挡在异步门外 | 同文件 L243：`if (holder.IsAI) choice = AiResponder(...) ?? DefaultAiRespond(...)`（同步） |
| 驱动双方都是 `IsAI = true` | `TideHeadlessDriver.ResetCore` L262-263 |
| 响应出牌会消费 `ResponseOption.Targets` | 同文件 L339：`PlayCardInResponse(core, p, opt.SourceCard, opt.Targets, ...)` |

**已证伪的旧路线（不要再走）**：
- 用 `SimpleAI.TakeTurn()`（同步包装）驱动可暂停回合 → `InvalidOperationException: Not yet completed`。
- 用 `PacingWait` 当停靠点 → 它在**动作之间**，而响应窗在**动作内部**。

---

## 1. 引擎改动（两处，均为「未设置时逐位不变」）

### 1.1 `ResponseWindow.cs` 新增异步 AI 响应口

```csharp
// 2026-11-22 D2d：AI 座异步响应口。null = 旧行为（AiResponder ?? DefaultAiRespond）逐位不变。
// 返回值语义：null = 「不作主」→ 引擎回落旧链；非 null = 该选项即为本回合响应决策。
public static Func<GameCore, Player, List<ResponseOption>, UniTask<ResponseOption>> AiAsyncResponder;
```

### 1.2 `GameActions.ResponseWindow.cs` L243 分支

```csharp
if (holder.IsAI)
{
    var asyncAi = ResponseWindowService.AiAsyncResponder;
    ResponseOption r = asyncAi != null ? await asyncAi(core, holder, options) : null;
    choice = r ?? ResponseWindowService.AiResponder?.Invoke(core, holder, options)
                  ?? DefaultAiRespond(core, holder, options);
}
```

> 关键语义：**返回值 null = 不作主 → 回落 `AiResponder ?? DefaultAiRespond`**。
> 这样驱动只需对「模型座」返回非 null，脚本座原样返回 null，D2c 的脚本响应行为逐位保留。

### 1.3 `GameActions.ResponseWindow.cs` L255：尊重已给定目标

```csharp
// 原：HandCard 且是法术 → 一律引擎收集声明期目标
// 改：已带 Targets（模型侧 LAE 行给的目标）则跳过收集，直接用它
if (choice != null && choice.Kind == ResponseOption.ResponseKind.HandCard
    && choice.SourceCard != null && choice.SourceCard.IsSpellCard()
    && choice.Targets == null)              // ← 新增条件
{
    var pre = await CollectSpellDeclarationTargetsAsync(choice.SourceCard, holder, choice.ModeIndex);
    ...
    choice.Targets = pre.Targets;
    choice.DynamicCounts = pre.DynamicCounts;
}
```

- 人类路径不受影响（`HumanResponder` 返回的 option `Targets` 恒为 null → 仍走收集）。
- 模型侧若该行**没有**目标（无交互原子）→ `Targets` 为 null → 仍走引擎收集，行为不变。
- 动态数量法术若行里没带 `DynamicCounts`，同样让它走引擎收集（见 §2.4 的填充规则）。

### 1.4 `LegalActionEnumerator.TideAction` 增加 `Option` 字段（**必须**，否则映射不可靠）

`EnumerateResponses` **不是**「1 option = 1 row」的一一映射：它过滤部分 option、
对 Activate 类按目标展开多行（`AddActivatedRows`）、再追加 `EnumerateRespondPlay` 的手牌行，
最后 `EnforceActionCap` 截断。所以**不能用下标对 `options` 反查**。做法：

```csharp
public struct TideAction
{
    ...
    /// <summary>D2d：本行来源的响应候选（响应表专用；其余表为 null）——
    /// 引擎异步响应口回传的就是它，避免用下标反查 options（行数/顺序均不一一对应）。</summary>
    public ResponseOption Option;
}
```

在四处构造点赋值：`BuildGuardAction`（GuardAbility，形参已是 `ResponseOption opt`）、
`BuildVoluntaryAction`（VoluntaryEffect，`opt.Pending`）、
`AddActivatedRows`（ActivatedAbility，多行共用同一 option）、
`EnumerateRespondPlay`（HandCard——**该路不经 `CollectAvailableResponses` 的 HandCard 分支**
（L255 的 switch 没有 HandCard case），需就地合成 option）：

```csharp
// EnumerateRespondPlay 内，行构造处
var opt = new ResponseOption
{
    Kind = ResponseOption.ResponseKind.HandCard,
    SourceCard = c,
    ModeIndex = m,
    Targets = set,          // 该行展开出来的目标集（无目标=null）
    Label = EffectText.Name(c),
};
// row.Option = opt; row.Targets = set; row.DynamicCounts = ...（若该行按动态数量展开）
```



---

## 2. 驱动改动（`TideHeadlessDriver`）

### 2.1 新增状态

```csharp
private UniTask _scriptTurnTask;                       // 脚本回合链（不阻塞持有）
private bool _scriptTurnRunning;                       // 链在途
private UniTaskCompletionSource<ResponseOption> _parked; // 当前停靠
private Player _parkedHolder;                          // 停靠座（日志/断言用）
```

### 2.2 注册异步响应口（`ResetCore` vs 分支，与 D2c 的 PickFor 同处）

```csharp
ResponseWindowService.AiAsyncResponder = (core, holder, opts) =>
{
    // 脚本座：不作主 → 回落 PickFor/Default（D2c 行为逐位不变）
    if (!ReferenceEquals(holder, _modelPlayer)) return UniTask.FromResult<ResponseOption>(null);
    if (opts == null || opts.Count == 0) return UniTask.FromResult<ResponseOption>(null);

    _parkedHolder = holder;
    _parked = new UniTaskCompletionSource<ResponseOption>();
    _decider = holder;
    _responseStop = true;
    return _parked.Task;      // ← 引擎在这里挂起，控制权回到 Step 调用栈
};
```

`Dispose`/`finally` 里置 null（与 `AiResponder` 同批还原）。

### 2.3 脚本回合改为「持有不阻塞」

```csharp
private void StartScriptTurn()
{
    _scriptTurnRunning = true;
    _scriptTurnTask = _simpleAI.TakeTurnAsync(_aiCtrl);   // 不 GetResult()
    // 若链路未停靠（同步跑完），TakeTurnAsync 返回已完成 UniTask，状态位随即可清
    if (_scriptTurnTask.Status.IsCompleted()) FinishScriptTurn();
}
```

- 旧 `RunSimpleAiTurn()`（`TakeTurn()` 阻塞版）**删除**，全部改走 `StartScriptTurn`。
- 停靠后由 §2.4 的 `Step` 续跑；跑完后 `FinishScriptTurn()` 做原来的收尾：
  `if (!_gameOver) _core.TurnEngine.CheckPhaseTransition();`（End→Standby 补泵，与旧时序一致）。

### 2.4 `Step(action)` 的续跑分支（放在越界检查之后、常规动作之前）

```csharp
if (_parked != null)
{
    var row = (actionIndex >= 0 && actionIndex < _legal.Count) ? _legal.Actions[actionIndex] : (TideAction?)null;
    ResponseOption picked = null;
    if (row.HasValue && row.Value.Type != TideActionType.PassPriority)
    {
        picked = row.Value.Option;                    // §1.4：行自带 option，不做下标反查
        if (picked != null
            && picked.Kind == ResponseOption.ResponseKind.HandCard
            && row.Value.Targets != null && row.Value.Targets.Count > 0)
        {
            picked.Targets = row.Value.Targets;       // 引擎遇 Targets!=null 跳过收集（§1.3）
            picked.DynamicCounts = row.Value.DynamicCounts; // 行里没有就保持 null → 引擎自行收集
        }
    }

    var park = _parked;
    _parked = null; _responseStop = false; _decider = null;
    park.TrySetResult(picked);            // picked==null → 引擎按「不作主」回落（=让过）

    if (_scriptTurnTask.Status.IsCompleted()) FinishScriptTurn();
    if (_parked != null) return BuildResult(0f);   // 又停在新窗口 → 直接返回新决策点
    AdvanceToDecision();
    return BuildResult(0f);
}
```

### 2.5 互斥守卫

- `_scriptTurnRunning == true` 期间：**不要**走 `TryResolveResponseWindow()` 的模型停靠分支
  （窗口由引擎泵独占，否则双停靠）。
  实现：`AdvanceToDecision` 入口加 `if (_scriptTurnRunning) return;`（链在跑时没有驱动侧推进）。
- `_parked != null` 时禁止 `reset/close`（Python 侧不应发生；驱动侧返回同一决策点兜底）。

### 2.6 与 D2c 的分工（务必都保留）

| 窗口出现在 | 由谁处理 | 机制 |
|---|---|---|
| **模型自己回合**的结算窗 | 驱动 `TryResolveResponseWindow`（D2c） | `_decider/_responseStop` 停靠 |
| **脚本回合内**的响应窗 | 引擎泵 + `AiAsyncResponder`（D2d） | TCS 挂起 + `Step` 续跑 |

两者都用 `_legal` + `TideAction`，动作空间/观测不变，`g[49]=1` 语义一致。

---

## 3. Python 侧

**不改**。动作空间不变（`EnumerateResponses` 已产出 RespondPlay/Guard/PassPriority 行），
`resp_step_frac` / `decisionContext=1` 已可观测；GAE 座次修正沿用 `info.toPlay`/`decisionSeat`。

唯一新增观测需求：`info.responseSeat`（可选）——停靠方座次，便于日志区分「模型回合窗」与「对手回合窗」。

---

## 4. 测试矩阵（一次性跑完）

| # | 用例 | 判据 |
|---|---|---|
| T1 | **零回归**：`AiAsyncResponder = null`，`opponent="script"` 20 局固定 (seat, seed) | 与 D2c 基线逐位一致（`(winner, turn)` 全同） |
| T2 | **脚本座不受影响**：注册开启但 holder=脚本座 | 20 局与 T1 逐位一致（回调返回 null → 回落 PickFor） |
| T3 | **对手回合停靠**：vs 蓝 20 局 | 出现 holder=模型座、`g[49]=1`、发生在**脚本回合内**的停靠；选择后回合跑完；无死锁 |
| T4 | **等价性**：同 20 局，(a) 停靠全选 Pass vs (b) responder 恒 null | `(winner, turn)` 逐位一致（证明停靠本身不改变对局语义） |
| T5 | **多停靠**：构造一回合 ≥3 次停靠 + 多候选 | 全部正确停/续，回合正常收尾 |
| T6 | **长跑**：2k 步 smoke（模型蓝 vs 脚本三主题） | 无死锁/无 EnvTimeout、`ep_len` p95 < 1600、`resp_step_frac` 上升、`cap_hits=0` |
| T7 | **线程**：记录起链线程与续跑线程 | 续跑恒在完成线程（内联）；引擎状态无跨线程并发（日志断言） |

**T3/T4 是本方案的核心证据**：T4 用「全 Pass 停靠 vs 完全不停靠」逐位一致，证明新增的挂起/续跑
不改变引擎语义，只是把决策权交出去。

---

## 5. 回滚

- 引擎两处均为「null 时旧行为」，删掉注册即回到 D2c 状态。
- 驱动保留开关：`TIDE_ASYNC_RESPONSE=0`（环境变量）→ 不注册 `AiAsyncResponder`。

## 6. 风险

| 风险 | 处置 |
|---|---|
| 续跑换了线程（起链线程 ≠ 完成线程） | T7 断言 + 续跑期间不持任何锁；若发现线程亲和问题，改为 `WorkerThread` 单一执行线程 |
| 回声/双应用：引擎拿到 option 后自行应用，驱动不得再 `Apply` | §2.4 只 `TrySetResult`，**绝不**自己 Apply |
| 响应法术目标被引擎覆盖 | §1.3 的 `Targets == null` 条件已处理；行无目标时回落引擎收集 |
| 死锁：`TrySetResult` 在持锁区调用 | 续跑分支放在 socket 写回之前、无锁区内 |
