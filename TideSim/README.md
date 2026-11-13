# TideSim —— 三套预组无头对战（单局/批量同源）

2026-11-04 新建，用来替代旧的 `Tools/AI 自动对战` 作为**批量统计**入口。

## 一致性约定（重要）

Unity 窗口 `Tools/AI 自动对战（选卡组）` 的**单局与批量**、以及本工程的**批量**，
现在都调用同一份执行器：

`Assets/Scripts/HotUpdate/AI/AiDuelRunner.cs`

它统一负责：

- `InitGame`：卡组顺序、`skillCardId`、`tokenBindings`、**seed**
- 双方 `IsAI=true`、P2 后手护甲
- `AiStrategyRegistry.Bind` + `ResponseWindowService.AiResponder`（响应窗策略）
- `SimpleAI` 回合循环 + `TurnEngine.CheckPhaseTransition()`（End→Standby 补泵）
- 收尾：解绑策略、清响应窗、清 `TargetSelectionService.AiOrder`、清召唤模板、拆棋盘

因此：**同卡组 + 同技能 + 同策略 + 同 seed = 同一局**。
Unity 窗口的 `seed` 输入框：单局直接用该 seed；批量第 i 局用 `seed+i`，
所以**批量第 1 局与同 seed 的单局是同一局**。

> 神经网络（ONNX）座位仍走旧的 `AiBattleDriver` 神经路径，不在本一致性约定内；
> 三套预组的脚本局全部走共享 runner。

## 用法

```powershell
# 先单局 dry-run（含每回合手牌/出牌/技能/结果）
dotnet run --project TideSim -- --games 1 --matchup red-blue --detail --seed 20261104

# 同时导出与 Unity 窗口同源的 MatchLog（逐行对账）
dotnet run --project TideSim -- --games 1 --matchup red-blue --detail --match-log --seed 20261104

# 批量（每组对阵 N 局，换边轮替先手）
dotnet run --project TideSim -- --games 100 --matchup all --seed 20261104

# 只跑某组
dotnet run --project TideSim -- --games 50 --matchup red-blue --seed 20261104
```

参数：

- `--games N`：每个对阵的局数（默认 20）
- `--seed S`：seed 基数（默认 20261104；第 i 局 = S+i）
- `--max-turns N`：每方单回合上限（默认 200）
- `--matchup all|cross|mirror|red-blue|red-green|blue-green`
- `--detail`：逐回合/逐出牌/技能/结果日志
- `--quiet`：只打汇总

回归入口（不跑对局）：

```powershell
dotnet run --project TideSim -- --scenario          # 红快攻策略闸（血偿/活体炸弹准入 + 拆波）
dotnet run --project TideSim -- --tutorial-scenario # 固定盘面（红快攻9场景 + 蓝充盈 + 自由分支）
dotnet run --project TideSim -- --chain-cast        # 连锁直伤：两张法术各付一次代价（1 生物时第二张效果无效）+ 参照快照
dotnet run --project TideSim -- --deck-rule         # 卡组校验：代价×门奖励互斥（含改版前活体炸弹负例）
```

## 预检（跑批量前自动执行）

- 三套预组可读、卡表 ID 全部解析
- 技能卡解析（红=激励 / 蓝=隐匿 / 绿=回收）
- 策略**按预组名显式绑定**（红→RedAggro / 绿→GreenRamp / 蓝→BlueControl），不走 AutoMatch 猜测
- 单色套非主色费告警（例如某张牌实际费用落在套牌付不起的颜色）

## 产物

- `Logs/TideSim_<时间>.csv`：逐局 seed / 双方卡组 / 策略 / 技能 / 胜者 / 回合 / 生命 / 技能发动 / 出牌数
- `Logs/TideSim_<时间>.md`：预检 + 汇总 + 逐局表
- `Logs/MatchLog_*.md`：Unity 端单局战报（由窗口驱动导出）

## 备注

- 规则直接编译 `Assets/Scripts/HotUpdate/CardCore/**` 与游戏同源，不复制规则。
- 单局/批量若出现结果不同，先核对：seed、双方卡组（含技能/衍生物）、策略 key、`max-turns`、P2 护甲。
