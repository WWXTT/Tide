# AI / 脚本执行缺口修复 · 可执行计划

- 日期：2026-11-15
- **状态（2026-11-18 审计注）：五个阶段已全部落地**——阶段1 UnsupportedForAi 闸、阶段2 `SimpleAI.SettleAfterAiActionAsync`、阶段3 守卫 SourcePlayer/角色实体入栈、阶段4 VoluntaryEffect 兜底+`PickHeroSkillEffect`、阶段5 `PickPlaceCell`（`AiStrategy.cs`/`SimpleAI.cs`/`LegalActionEnumerator.cs`/`GameActions.ResponseWindow.cs` 内均有对应实现与"2026-11-15 审计 §N"注释）。正文与 §7 派活令保留作历史记录，勿再按计划重复施工。
- **2026-11-19 动作空间注**：§8 的“RL 动作空间加落格/移动选格/宣言”由
  [AI动作空间补全_修改计划](AI动作空间补全_修改计划.md) P1/P2 接手；本计划 A-1/A-2 的“跳过”口径在 P1 落地后删除。
- 依据：原《AI与脚本动作覆盖审计_2026-11-15》（已删除）（下称“审计”；**注：该文件未入库，链接失效**）
- 性质：实现计划（分阶段，一次一派）。本文件只描述要改什么、怎么验收；不改代码。
- 范围：本地人机（木桩/挑战/普通 AI）、AI-vs-AI 模拟、RL/神经动作空间；**不含联机协议与 UI 改版**（单独立项，见 §8）。

---

## 0. 前置条件与总原则

### 0.1 工作区状态（重要）

上一份计划（主动横置产色 + 横置状态）已有**未提交实现**在工作区；同时工作区正在做**充能整链删除**重构（`GameActions` 的 `DeclareCharge/CanCharge`、`SimpleAI.ChargeIdleElements`、RL 的 `Charge` 动作、网络 `IntentBuildCard`、`TideSim/ChargeProbe.cs` 等已在本轮被移出，`GameActions.cs` 已从 441 行重构到 237 行）。

因此：

1. **派活前先让用户确认当前重构已提交/冻结**；本计划不得与在途重构混提。
2. 本计划所有改动**以方法名为锚**（行号只作定位提示），ZCode 动手前必须重新读一遍当前文件；若充能相关符号已消失，跳过对应描述即可（本计划已删除对充能的依赖）。
3. 本计划的阶段不得改上一份计划涉及的文件：`GameActions.cs`、`BattleScreen.Board.cs`、`BattleScreen.cs`、`BattleInformationPanel.cs`、`ACardView.cs`、`BattleHoverInfoProvider.cs`、`BattleUI.prefab`、`RegionTargetSelector.cs`。阶段 3 只改 `GameActions.ResponseWindow.cs`（不同文件）。

### 0.2 总原则

1. 只补「AI/脚本执行路径」，**不改规则、数值、牌表、UI 版式**。
2. 一次只做一个阶段；每阶段编译 + 自测 + 贴输出，做完停，不进入下一阶段。
3. **不新建 runtime `.cs` 文件**；所有改动进既有文件（`SimpleAI.cs`、`AiStrategy.cs`、`LegalActionEnumerator.cs`、`GameActions.ResponseWindow.cs`）。
4. 不改 `PlanBill` / 支付链 / 抽牌 / 回合推进。
5. **不改** `ScriptedAi`（教学/考验剧本）：教学局保持“机器人不反制、不打断”的现状；本计划只修 `SimpleAI`（木桩/挑战/普通 AI/神经兜底）。
6. 所有“AI 不支持”的裁剪都必须留日志（调试开关或 `TideLog.Warn` 一次性），便于验收。
7. 当前工作区**已无充能机制**（2026-11-15 整链删除）：本计划不涉及充能；若后续回滚恢复充能，再单独评估。

### 0.3 缺口 → 阶段映射

| 审计项 | 阶段 |
|---|---|
| A0 AI 回合不给人开响应窗 | 阶段 2 |
| A-1 MoveUnit 白付代价 | 阶段 1 |
| A-2 卡名宣言（无预置）白付/取消 | 阶段 1 |
| B-1 角色守卫 | 阶段 3 |
| B-2 响应窗自愿待发 | 阶段 4 |
| B-3 多效果英雄技能 | 阶段 4 |
| B-4 永久物落格 | 阶段 5 |
| B-5 响应窗即时主动效果（ActivatedAbility） | **不排期**（策略债务，§8） |
| C-1 占卜排列 / C-2 虚拟手牌 | **不排期**（§8） |
| 反向 LandTrait UI / 联机缺口 | **单独立项**（§8） |

### 0.3.1 对账实测表（2026-11-15 晚，step 2 出口证据）

> 工具：训练池四张对账样板（`训练·疾行者 C_0CE6D3E4` / `训练·强夺 C_60C4C7DF` / `训练·窃取 C_FB07E117` / 池内 `训练·检索 C_D2FB9D0B`）。
> 方法：LAE 定向探针（闸卡满卡组驱动 + 动作行逐行扫描）+ 编辑器 TCP 冒烟 39 局 / 5289 步。
| 审计项 | 玩家 UI 有入口 | AI 有动作行 | 结论 |
|---|---|---|---|
| **A-1 移动（疾行者）** | ✅ CellSelectionService 弹格选格桥（与召唤落格同交互，`ThirdBatchEffectHandlers.cs:646`） | ❌ Activate 0 行（120 步探针累计 0；`UnsupportedForAi`·MoveUnit 闸；运行时兜底=handler 空转 Warn） | 形态分离成立：玩家可移动、AI 不吃不白付 |
| **A-2 宣言·强夺（DeclareHand）** | ✅ 结算期文本输入窗（`AskCardNameAsync`→`RequestTextAsync`，输入替换预置、清空=取消） | ❌ PlayCard 0 行（无预置闸）+ `[LAE]` 一次性点名日志 | 同上 |
| **A-2 宣言·窃取（DeclareDeck）** | ✅ 同上（同窗） | ❌ 同上（`AFE00EB4` 点名） | 同上 |
| **A-2 宣言·检索（SearchDeck str=""）** | ✅ 同上（旧预置回填输入窗，玩家可改） | ❌ 同上（`1632B175` 点名） | 同上；等 v0.3 接线后删装载夹具豁免（验收记录 §4-R1） |
| **A0 AI 回合响应窗** | —（人类侧入口=响应窗本身） | — | 阶段 2 已落地（`SettleAfterAiActionAsync` 在位）；本轮未单独复测 |
| **B-1 角色守卫** | ✅ 玩家可宣言守卫（拦截栈上攻击） | ✅ LAE `Guard` 行携带 `Guarder`（角色守卫 Card=null/TargetKindCode 区分） | 阶段 3 已落地，双向都有 |
| **B-2 响应窗自愿待发** | ✅ | ✅ `VoluntaryTrigger` 行 | 阶段 4 已落地，双向都有 |
| **B-3 多效果英雄技能** | ✅ | ✅ `HeroSkill` 行 + `PickHeroSkillEffect` 择效果 | 阶段 4 已落地，双向都有 |
| **B-4 永久物落格** | ✅ 玩家选格落位 | ✅（SimpleAI 兜底首格 + `PickPlaceCell` 策略口） | 阶段 5 已落地，双向都有 |
| B-5 / C-1 / C-2 | — | — | 不排期（§8），无对账项 |
| 反向 LandTrait UI / 联机 | — | — | 单独立项（§8） |

> 闸卡满手探针：起手 8 检索+2 强夺+2 窃取+2 疾行者 → 12 动作行 = 11 PlayLand + 1 EndTurn，**0 行引用任何闸卡**（放地不受闸=设计内）。冒烟 39/39 完局 0 异常（详见训练池验收记录）。

---

## 1. 阶段 1：AI 不再白付代价（MoveUnit / 卡名宣言）

**目标**：含「移动」原子、或含无预置卡名宣言原子的效果/卡牌，本地 `SimpleAI` 与 RL 枚举都不再发动/打出——从“付费后空转”变成“明确跳过”。

### 1.1 `SimpleAI.cs`：统一的不支持原子判定

在 `SimpleAI` 内新增（放在「动作来源」区，`ActivateBattlefieldAbilities` 之前）：

```csharp
/// <summary>AI 无法正确执行的原子（2026-11-15 审计）：MoveUnit 需选格交互；
/// 检索/强夺/窃取需卡名文本输入。含这些原子且无可用预置的效果，AI 不发动/不打出
/// （防付费后空转），待交互接线后逐条移除本闸。</summary>
private static bool UnsupportedForAi(EffectDefinition def)
{
    if (def?.Effects == null) return false;
    foreach (var atom in def.Effects)
    {
        if (atom == null) continue;
        if (atom.Type == AtomicEffectType.MoveUnit) return true;
        if ((atom.Type == AtomicEffectType.SearchDeck
             || atom.Type == AtomicEffectType.DeclareHand
             || atom.Type == AtomicEffectType.DeclareDeck)
            && string.IsNullOrWhiteSpace(atom.StringValue))
            return true;
    }
    return false;
}
```

接入点（四处）：
1. `ActivateBattlefieldAbilities`：在 `CanActivate` 前加 `if (UnsupportedForAi(def)) continue;`
2. `PlayBestAffordableCardAsync`：`ChooseMode` 得到 `mode` 后，取该模式的 `EffectDefinition` 再判：
   ```csharp
   var castable = GameActions.GetCastableEffectDefinitions(card);
   var chosenDef = castable != null && castable.Count > 0
       ? castable[Math.Clamp(mode, 0, castable.Count - 1)] : null;
   if (UnsupportedForAi(chosenDef)) continue;
   ```
3. `DrainVoluntaryQueue`：发动前 `if (UnsupportedForAi(effect.Effect)) continue;`
4. `UseHeroSkillAsync`：取 `HeroSkillSystem.SkillEffectsOf(...)` 后，若当前 effect 0 不支持则直接返回 false（阶段 4 接入 `PickHeroSkillEffect` 后再按所选效果判）。

调试日志（仅验证用，开关 `TIDE_DEBUG_AI=1`）：
```csharp
if (Environment.GetEnvironmentVariable("TIDE_DEBUG_AI") == "1")
    Console.WriteLine($"[dbg-ai] skip unsupported effect={def?.Id} card={(card as IHasName)?.CardName}");
```

### 1.2 `LegalActionEnumerator.cs`：同一口径的枚举裁剪

新增私有 helper（放在 `ContainsMoveUnitAtom` 旁）：

```csharp
/// <summary>与 SimpleAI.UnsupportedForAi 同口径（2026-11-15）：AI 无法执行的交互原子
/// （MoveUnit 选格 / 无预置卡名宣言）——枚举直接不产生动作行。</summary>
private static bool UnsupportedForAi(EffectDefinition def) { /* 同 §1.1 逻辑 */ }
```

接入点：
1. `EnumerateActivate`（`:491-518`）：把现有 `ContainsMoveUnitAtom(def)` 闸换成 `UnsupportedForAi(def)`（保留原注释，追加卡名类）。
2. `AddActivatedRows`（响应窗 `RespondActivate` 与主动共用）：函数头 `if (UnsupportedForAi(def)) return;`。
3. `EnumeratePlayCard`（`:441-489`）：按模式取 `GameActions.GetCastableEffectDefinitions(c)[m]`，`UnsupportedForAi` 则 `continue`；牌库顶虚拟手牌分支同。
4. `EnumerateRespondPlay`（`:763+`）：同样按模式判，跳过不支持的手牌响应卡。
5. `EnumerateHeroSkill`（`:650+`）：逐效果判，跳过不支持的技能效果行。

保留 `ContainsMoveUnitAtom` 若仍有其他调用；没有则一并删除。

### 1.3 不做

- 不给 MoveUnit 接 AI 选格（阶段 5 只做「出牌落格」，移动选格见 §8）。
- 不给卡名宣言做“智能猜名”（信息泄露风险）；只做“无预置就跳过”。
- 不改 `ScriptedAi`（剧本显式指定动作；由内容作者避免）。

### 1.4 验收

- [ ] `dotnet build TideServer/TideServer.csproj -c Release` 0 error（CardCore 共享源）。
- [ ] `dotnet build TideSim/TideSim.csproj -c Release` 0 error（SimpleAI 共享源）。
- [ ] `dotnet run --project TideSim -- --games 1 --matchup red-blue --detail` 正常完局（无卡死）。
- [ ] `dotnet run --project TideSim -- --scenario` 全过（策略闸回归）。
- [ ] 手工：构筑一张带「移动」原子的卡，放入 AI 卡组（木桩“AI”难度可选任意牌库），AI 回合不再出现 `[MoveUnit] …空转`，且不会为它付横置/费用。
- [ ] 自动化辅助（可选）：把 `UnsupportedForAi` 改成 `internal`，在 TideSim 探针里用内存合成的 `EffectDefinition` 断言：MoveUnit=true；`SearchDeck`+空 str=true；`SearchDeck`+预置=false；普通效果=false。

---

## 2. 阶段 2：AI 回合给人开响应窗（A0）

**目标**：AI 在自己回合出牌/发动主动/英雄技能后，**人类对手能响应**（与攻击窗一致）；无头/AI-vs-AI 行为不变。

### 2.1 `SimpleAI.cs`：新增统一结算口

```csharp
/// <summary>AI 动作后的结算（2026-11-15 A0）：人类对手在场时走响应窗泵（人类可响应），
/// 无头/AI-vs-AI 仍走 DrainStack（同步、零行为变化）。</summary>
private async UniTask SettleAfterAiActionAsync(GameCore core, Player me)
{
    if (core == null) return;
    bool humanOpponent = me?.Opponent != null && !me.Opponent.IsAI
                         && ResponseWindowService.HumanResponder != null;
    if (!humanOpponent || core.IsGameOver) { SettleStack(core); return; }

    await GameActions.SettleResponseWindowAsync(core);
    // 泵因并发让路/异常未收口时兜底；不要在别的泵在途时抢排
    if (!core.ResponsePumpInFlight && !core.IsGameOver
        && (!core.StackEngine.IsEmpty || core.StackEngine.HasPendingEffects))
        SettleStack(core);
}
```

### 2.2 替换调用点（以方法名为锚）

1. `PlayBestAffordableCardAsync`：`SettleStack(core)` → `await SettleAfterAiActionAsync(core, me)`（当前在 `GameActions.PlayCard` 成功、`StageCastAsync` 之后）。
2. `UseHeroSkillAsync`：`SettleStack(core)` → `await SettleAfterAiActionAsync(core, me)`（`StageHoldAsync` 之后）。
3. `ActivateBattlefieldAbilities` 改 async：
   ```csharp
   private async UniTask<bool> ActivateBattlefieldAbilitiesAsync(GameCore core, Player me)
   {
       // …原循环…
       if (GameActions.ActivateEffect(core, me, def, source))
       {
           any = true;
           await SettleAfterAiActionAsync(core, me); // 每次发动后给人响应窗
           if (core.IsGameOver) return any;
       }
       return any;
   }
   ```
   主循环调用改为：
   ```csharp
   bool progress = DrainVoluntaryQueue(core);
   if (await ActivateBattlefieldAbilitiesAsync(core, me)) progress = true;
   ```
4. 主循环末尾（`progress` 判定之后、`PaceAsync` 之前）：
   ```csharp
   if (progress) await SettleAfterAiActionAsync(core, me); // 覆盖待发自愿效果等已上栈动作
   await PaceAsync();
   ```
5. `DoCombatAsync` 已经是 `SettleResponseWindowAsync`，不动。
6. 当前无充能路径（已整链删除），不再有 `ChargeIdleElements` 之类需要保留的同步结算点。

### 2.3 无头兼容

- `AiDuelRunner` 用 `TakeTurn`（`.GetAwaiter().GetResult()`）：双方 `IsAI=true` → `humanOpponent=false` → `SettleStack` 同步路径，不引入 await 死锁。
- `TideHeadlessDriver` 同样（对手 AI）→ 不变。
- 只有 `BattleScreen` 本地人机（P2 AI，P1 `IsAI=false` 且 `HumanResponder` 已注册）才走窗。

### 2.4 验收

- [ ] 两个 `dotnet build` 通过；`TideSim --games 1 --matchup red-blue --detail` 正常完局（不卡死）。
- [ ] 木桩局（我方蓝·控制/任意带响应卡的预组，元素池充满可选）：
  - 人类回合结束 → AI 回合出牌时出现响应窗口（Region 列表或守卫窗），不再直接结算；
  - 人类让过 → AI 牌正常结算；人类用响应卡 → 生效；
  - 日志出现 `CF-pump-enter`/`ChainInquiryEvent`（说明走的是响应窗泵）。
- [ ] AI 攻击行为不变（仍走原窗）。
- [ ] 教学局（如可进）行为不变（`ScriptedAi` 未改，机器人仍被动）。
- [ ] 人类回合内 AI 的响应行为不变（人类出牌仍能引出 AI 响应窗）。

---

## 3. 阶段 3：角色守卫（AI + RL）

**目标**：AI 能用「角色弹药」拦截（`SourcePlayer` 候选），与本地人类已有能力对齐。

### 3.1 `GameActions.ResponseWindow.cs`（引擎 AI 兜底）

1. `DefaultAiRespond`（`:327` 起）：
   ```csharp
   var guards = options.Where(o => o.Kind == ResponseOption.ResponseKind.GuardAbility
                                   && (o.SourceCard != null || o.SourcePlayer != null)).ToList();
   ```
2. `PickGuard`：生命读数兼容角色：
   ```csharp
   int life = g.SourceCard != null ? g.SourceCard.GetLife() : (g.SourcePlayer?.Life ?? 0);
   ```
3. `ApplyResponse` 的 `GuardAbility` 分支：
   ```csharp
   return core.StackEngine.PushGuardDeclaration(
       opt.SourceCard != null ? (Entity)opt.SourceCard : (Entity)opt.SourcePlayer,
       opt.AttackInstance, p);
   ```
4. 保留现有“只保角色/带倒计时单位”的目标闸（设计口径，不改）。

### 3.2 `LegalActionEnumerator.cs`（RL）

1. `TideAction` 新增字段：
   ```csharp
   /// <summary>守卫者（生物=SourceCard；角色守卫=Player——2026-11-15 角色守卫入动作空间）。</summary>
   public Entity Guarder;
   ```
2. `EnumerateResponses`：条件从 `opt.SourceCard != null` 改为 `opt.SourceCard != null || opt.SourcePlayer != null`。
3. `BuildGuardAction`：`Guarder = opt.SourceCard != null ? (Entity)opt.SourceCard : (Entity)opt.SourcePlayer;`
4. `Apply` 的 `Guard` 分支：`a.Guarder != null && a.AttackInstance != null && PushGuardDeclaration(a.Guarder, ...)`。
5. `BuildFeatures` 不用改（`SourceIndex=-1`、`TargetKindCode` 已表达）。

### 3.3 不做

- 联机角色守卫（`NetGuardFlowAsync` 只有单位入口、`IntentDeclareBlock` 的 Player 解析需协议确认）：单独立项。
- 不扩大 AI 守卫目标范围（普通单位仍不拦；这是既有策略口径）。

### 3.4 验收

- [ ] 编译（TideServer + TideSim + Unity）。
- [ ] `TideSim` 全量回归（`--games 1`、`--scenario`）正常；神经动作枚举可用（可选跑 `LegalActionEnumerator` 相关验证器）。
- [ ] 手工/验证器：给 AI 角色 3 层 `HeroAttackCounter`，人类单位攻击 AI 角色 → AI 应选「角色守卫」（攻击目标改写、人类单位吃反伤）而不是直接掉血。
- [ ] 人类侧角色守卫（本地）行为不变。

---

## 4. 阶段 4：响应自愿待发 + 多效果英雄技能

**目标**：AI 不再“有可选触发放着不用”；多效果技能不再永远只选第 0 个。

### 4.1 自愿待发（VoluntaryEffect）

`AiStrategy.cs` 的 `AiStrategyRegistry.PickFor`（`:467-471`）：

```csharp
public static ResponseOption PickFor(GameCore core, Player holder, List<ResponseOption> options)
{
    if (holder == null || !_byPlayer.TryGetValue(holder, out var strategy)) return null;
    var pick = strategy.PickResponse(core, holder, options);
    if (pick != null) return pick;

    // 2026-11-15：策略不作主时，可选待发（自愿触发）默认发动——由策略闸 AllowVoluntaryActivation 放行；
    // ActivatedAbility 仍留给策略自己（不在这里兜底）。
    foreach (var o in options ?? new List<ResponseOption>())
    {
        if (o?.Kind != ResponseOption.ResponseKind.VoluntaryEffect || o.Pending?.Effect == null) continue;
        if (!strategy.AllowVoluntaryActivation(core, holder, o.SourceCard, o.Label)) continue;
        return o;
    }
    return null;
}
```

说明：这会让 AI 在对手回合也发动“可以”的触发（这正是人类已有的选择）；`AllowVoluntaryActivation` 已覆盖绿套“牺牲锁脚本”等例外。

### 4.2 多效果英雄技能

`AiStrategy.cs` 新增虚方法：

```csharp
/// <summary>多效果英雄技能的效果择一（2026-11-15）：默认 0=原行为；派生策略可覆写。</summary>
public virtual int PickHeroSkillEffect(GameCore core, Player me, IReadOnlyList<EffectDefinition> effects) => 0;
```

`SimpleAI.UseHeroSkillAsync`：

```csharp
var skillCard = HeroSkillSystem.ResolveSkillCard(core, me);
var effects = skillCard != null ? HeroSkillSystem.SkillEffectsOf(skillCard) : null;
int effectIndex = effects != null && effects.Count > 1
    ? _strategy.PickHeroSkillEffect(core, me, effects) : 0;
var ok = await GameActions.ActivateHeroSkill(core, me, effectIndex);
```

（阶段 1 的“不支持原子”检查改为按 `effectIndex` 选中的效果判断。）

### 4.3 不做

- 不给 `ActivatedAbility` 响应做通用兜底（需要策略判断费用/收益；`PickResponse` 已是扩展点，留给三套策略后续）。
- 不改三套策略的具体偏好。

### 4.4 验收

- [ ] 编译通过；`TideSim --scenario` 正常。
- [ ] 手工：构造一个带“可以……”可选触发的场面，人类做出触发动作 → AI 在响应窗选择发动（日志 `ChainInquiryEvent` 后有响应行，而非直接让过）。
- [ ] 手工/验证器：构造多效果英雄技能卡，给 AI 装备；观察 AI 调用 `PickHeroSkillEffect`（默认 0 时行为与旧一致）。
- [ ] 教学局不受影响（`ScriptedAi` 不走 `PickFor` 绑定时保持被动）。

---

## 5. 阶段 5：AI 永久物落格

**目标**：`SimpleAI` 出永久物时能选格（策略可覆写），默认仍等价 first-free；站位/邻接/攻击距离不再完全随机化。

### 5.1 `AiStrategy.cs`

```csharp
/// <summary>永久物落格（2026-11-15）：cells=己方单位区空格（BoardLayout.UnitCells 序）。
/// 默认取首个=原 first-free 行为；派生策略可按邻接/光环/攻击距离覆写。</summary>
public virtual (int x, int z) PickPlaceCell(GameCore core, Player me, Card card,
    IReadOnlyList<(int x, int z)> cells)
    => cells != null && cells.Count > 0 ? cells[0] : (-1, -1);
```

### 5.2 `SimpleAI.cs`

1. 字段/入口：
   ```csharp
   private GameBoard.BoardState _board;   // 当回合棋盘（TakeTurnAsync 入口赋值；无棋盘=不钉格）
   ```
   `TakeTurnAsync` 开头：`_board = ctrl.Board;`
2. 出牌成功后、`StageCastAsync` 之前钉格：
   ```csharp
   if (GameActions.PlayCard(core, me, card, targets, Zone.Hand, mode))
   {
       TryPinPlacement(core, me, card);
       await StageCastAsync(core, card, targets);
       await SettleAfterAiActionAsync(core, me);
       return true;
   }
   ```
3. 新增：
   ```csharp
   /// <summary>AI 落格（2026-11-15）：只对永久物（生物/结界；不含法术/场地卡）——
   /// 声明成功后钉格，与人类 PlayCardFinal 的 PinCard 同时点；无棋盘/无空格跳过。</summary>
   private void TryPinPlacement(GameCore core, Player me, Card card)
   {
       if (_board == null || card == null || card.IsSpellCard() || card.IsFieldCard()) return;
       int ownerIdx = ReferenceEquals(me, core.Player1) ? 0 : 1;
       var cells = new List<(int x, int z)>();
       foreach (var c in GameBoard.BoardLayout.UnitCells(ownerIdx))
           if (_board.CardAt(c.x, c.z) == null) cells.Add((c.x, c.z));
       if (cells.Count == 0) return;
       var pick = _strategy.PickPlaceCell(core, me, card, cells);
       if (cells.Contains(pick)) _board.PinCard(card, pick.x, pick.z);
   }
   ```
4. 无棋盘（旧探针/无头未接棋盘）自然跳过；不改变 `AiDuelRunner` 的默认落位结果（默认 `PickPlaceCell` 取首格，与 first-free 同序）。

### 5.3 不做

- RL 的动作空间加格维度（`TideAction` 加 `CellX/CellZ`、观测/掩码/训练全链）——成本高，单独立项。
- 网络先行/响应路径的落格（网络已有 `CellX/CellZ`，不在此改）。

### 5.4 验收

- [ ] 编译；`TideSim --games 1` 与 `--scenario` 正常。
- [ ] 默认行为：AI 出永久物落格与改动前一致（首空格）。
- [ ] 覆写一个策略（临时测试或 verifier）返回指定格 → AI 生物落在该格（日志/棋盘核对）。
- [ ] 人类选格、网络落格不受影响。

---

## 6. 回归清单（每个阶段都跑）

- [ ] `dotnet build TideServer/TideServer.csproj -c Release`
- [ ] `dotnet build TideSim/TideSim.csproj -c Release`
- [ ] `dotnet run --project TideSim -- --games 1 --matchup red-blue --detail`
- [ ] `dotnet run --project TideSim -- --scenario`
- [ ] Unity 编译无 CS 错误；木桩局一局完整跑完（含 AI 回合、人类响应、结束回合）。
- [ ] 人类侧已有行为：出牌/放地/攻击/守卫/响应/英雄技能 不受影响。
- [ ] 无头 AI-vs-AI：回合推进正常、无死锁、无 `[MoveUnit]` 新增告警。

---

## 7. 派活方式（每阶段一段提示词）

### 阶段 1

> 读 `docs/AI与脚本执行修复_修改计划.md` §0、§1、§6，只执行 **阶段 1**。
> 目标：`SimpleAI` 与 `LegalActionEnumerator` 都跳过「含 MoveUnit 或无预置卡名宣言原子」的效果/卡牌，不再付费空转；其余行为不变。
> 约束：只改 `SimpleAI.cs` / `LegalActionEnumerator.cs`（必要时 `AtomicEffects` 枚举只读）；不新建 runtime `.cs`；不改 `ScriptedAi`、支付链、UI、联机。
> 验收：两个 `dotnet build`、`TideSim --games 1 --matchup red-blue --detail`、`--scenario`；贴改动文件与输出；做完停。

### 阶段 2

> 读同一计划 §0、§2、§6，只执行 **阶段 2**（依赖阶段 1）。
> 目标：`SimpleAI` 在人类对手在场的本地局，出牌/发动主动/英雄技能后走 `SettleResponseWindowAsync`（人类可响应），无头/AI-vs-AI 仍 `DrainStack`；攻击行为不变。
> 约束：只改 `SimpleAI.cs`；不改 `ScriptedAi`、UI、联机；不引入同步阻塞（`AiDuelRunner` 的 `TakeTurn` 必须仍能跑完）。
> 验收：编译 + `TideSim` 回归；木桩局实测 AI 出牌时人类出现响应窗并能让过/响应；贴日志与结果；做完停。

### 阶段 3

> 读同一计划 §0、§3、§6，只执行 **阶段 3**。
> 目标：`DefaultAiRespond` 支持角色守卫（`SourcePlayer`），`ApplyResponse` 用角色实体入栈；RL `LegalActionEnumerator` 的 Guard 行支持角色守卫（`TideAction.Guarder`）。
> 约束：只改 `GameActions.ResponseWindow.cs`、`LegalActionEnumerator.cs`；不扩 AI 守卫目标范围；不做联机角色守卫。
> 验收：编译；`TideSim --scenario`；给 AI 角色弹药后人类攻击 AI 角色，AI 正确角色守卫；RL 枚举含角色守卫行；做完停。

### 阶段 4

> 读同一计划 §0、§4、§6，只执行 **阶段 4**。
> 目标：`AiStrategyRegistry.PickFor` 在策略不作主时兜底发动 `VoluntaryEffect`（经 `AllowVoluntaryActivation`）；`AiStrategy.PickHeroSkillEffect` + `SimpleAI` 使用。
> 约束：不改三套策略偏好；不做 ActivatedAbility 兜底；不新建文件。
> 验收：编译；可选触发在对手回合被 AI 发动；多效果技能 AI 可选非 0；教学不受影响；做完停。

### 阶段 5

> 读同一计划 §0、§5、§6，只执行 **阶段 5**。
> 目标：`AiStrategy.PickPlaceCell` + `SimpleAI` 出永久物后 `PinCard`；默认首格，行为不变。
> 约束：不做 RL 落格维度、不改网络；无棋盘时跳过。
> 验收：默认落格不变；覆写策略能指定格；`TideSim` 回归；做完停。

---

## 8. 已识别但不排期（明确记录，避免反复审计）

| 项 | 原因 |
|---|---|
| RL 动作空间加落格维度 | 需改 `TideAction`/观测/掩码/训练全链，成本高；先只修 `SimpleAI` |
| 响应窗 `ActivatedAbility` 通用兜底 | 涉及费用/收益判断，属策略设计；`PickResponse` 已是扩展点 |
| 卡名宣言“智能猜名” | 有信息泄露风险；只做“无预置就跳过” |
| 占卜排列策略 | 只影响占卜卡收益；默认身份已文档化 |
| `SimpleAI` 牌库顶虚拟手牌 | 目前无现役卡使用该光环；待有卡再补 |
| LandTrait 人类入口 | UI/预制体/联机 intent 三处；单独立项（反向缺口比 AI 缺动作更急，建议优先于本表其余项） |
| 联机响应窗/角色守卫/虚拟手牌/移动选格 | 协议无承载或 UI 无入口；需产品确认 + 协议版本，单独立项 |

---

## 9. 交付物自查（每阶段结束都贴）

- [ ] `git status --short` + `git diff --stat`（只含本阶段文件）
- [ ] 编译输出（dotnet + Unity Console）
- [ ] 自测命令与结果
- [ ] 未做的事（明确写出“未进入下一阶段”）
