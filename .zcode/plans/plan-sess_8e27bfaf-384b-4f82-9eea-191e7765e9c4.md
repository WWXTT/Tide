# M3 端到端实现计划：双 headless 客户端经网络整局 + 同种子对拍

## 目标（项目概览.md M3 原文）
双 headless 客户端（SimpleAI 驱动）经网络打完整局，同种子与本地直连结果对拍一致。

## 架构决策

**对拍口径**：同种子跑两次完整网络局（真实 TCP）→ 服务器事件流逐值相等 + 终局快照字节相等；换种子跑第三局 → 流不同（种子敏感性，防假对拍）。"本地直连"以"同种子重复局"实现——同一套客户端大脑 + 同一服务器栈，传输层不引入扰动即等价于本地直连结果；不做独立无传输参考 harness（自带口径漂移风险）。

**客户端大脑**：快照驱动（客户端只有 snapshot+事件流，无引擎——SimpleAI 启发式移植到 DTO 上）。**修订号锁步**是确定性关键：大脑只在"收到新快照帧"或"收到新 Error 帧"时行动一次——行动序列是快照序列的纯函数，与 wall-clock/粘包无关。

**身份计数器对拍障碍**：`Entity.RuntimeId`（Entity.cs:47 static 自增）与 `GameEventBase.EventId`（GameEvents.cs:30 static 自增）跨局漂移 → 加"仅供验证器"的重置钩子，两局各自从 1 开始，可直接逐值对拍。

## 实施清单

### 1. 确定性地基（引擎小改，3 处）
- `GameCore.InitGame`：`rngSeed` 同时重播 `ZoneContainer.Reseed(rngSeed.Value)`（洗牌流——M2 明确留给 M3 的 RNG 双流债，注释注明对拍收口）。
- `Entity.ResetRuntimeIdCounterForVerification()` / `GameEventBase.ResetEventIdCounterForVerification()`：static 计数器归零，注释钉死"仅验证器局边界调用 + 须同步 NetEntityDirectory.Clear() 防旧缓存串号"。

### 2. 房间种子化（M2 代码小改）
- `NetRoom(string roomId, int? seed = null)`：seed 控制 `_rng`（先手椅位）与 `InitGame` 引擎种子；无 seed 行为不变。`NetSessionServer` 透传。

### 3. 新文件 `Assets/Scripts/CardCore/NetSession/NetClientBrain.cs`（runtime 程序集）
确定性快照决策层，决策阶梯（每次行动机会取第一可行动作）：
1. 反问应答（SelectRequest → 选前 Min 个索引）；
2. Standby+轮到我 → SkipStandby；
3. 栈非空+优先权在我 → PassPriority（响应窗口让行）；
4. Main+轮到我，按序：横置下一张未横置地（产色=RemainingLandTokens 首色）→ 放地（每回合 1 张，本地卡表判生物资格）→ 出牌（手牌序尝试，付不起由服务器 Error 拒、本回合拉黑该卡）→ 攻击（首个未横置且本回合未攻过的己方战场生物 → 对方角色）→ EndTurn。
状态机按快照 CurrentTurn 重置；修订锁步防重复行动；tried 集合防拒绝风暴。卡名/费用查本地 CardCatalog（客户端本地渲染同源）。

### 4. 验证器第七段（M3 对拍）`RunM3Section`
- 复用 SocketTestClient 挂 NetClientBrain；`RunNetworkGame(seed)`：双客户端 Join/DeckSubmit → 锁步循环到 GameOverEvent（回合上限 40 后确定性认输兜底）→ 采集服务器事件流（NetEventProjector.Events）+ 终局双视角快照字节。
- 断言组：seed=777 两局——事件流逐值相等（复用 EqualEvent；EventId 因计数器重置可直接比）+ 快照字节相等 + 整局到达终局 + 无意外断线；seed=888 第三局——事件流**不同**（种子敏感）；顺带断言两局对局回合数/胜者一致。
- 每局开局前：ResetRuntimeIdCounterForVerification + ResetEventIdCounterForVerification + NetEntityDirectory.Clear()。

### 5. 独立 headless 客户端入口 `NetClientHost.Main()`（Assets/Editor/NetSession/）
batchmode：`-executeMethod ...NetClientHost.Main -netHost <ip> -netPort <n> -nickname X -seat N` → 连接 → JoinRoom → DeckSubmit（本地卡池前 30 非重复）→ 大脑锁步跑到 Finished 退出。跨机/跨进程手工验证用；自动门禁仍用验证器第七段。

### 6. 文档回写
- `网络协议.md`：§12 增补"种子与对拍口径"（房间种子/修订锁步/身份计数器重置钩子）；§10 验证加第七段。
- `项目概览.md`：M3 勾选。

### 7. 验证
`dotnet build` 双程序集 0 错误；编辑器跑 `Tools/网络协议回环验证` 第七段全绿（与既有六段回归）。

## 明确出界
跨机部署脚本/防火墙（M4/运维）；断线重连、超时代打（M4）；客户端 UI 消费侧（后续）；观战对拍（信息口径不同，不参与一致性断言）。