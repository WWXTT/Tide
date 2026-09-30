# TideServer —— 分离的权威服务器

独立于 Unity 的专用服务器进程（2026-09-27 立项，定案见根目录 `网络协议.md` §14）。
**线格式零改动、Unity 客户端零改动**——客户端在匹配界面连 `服务器IP:7777` 即可（编辑器
NetLobbyHost 因端口被占自动退位为纯客户端，现成机制）。

## 架构

```
Unity 客户端 A ──┐                          ┌─ match 子进程 #1（NetSessionServer 宿主，127.0.0.1:随机口）
                 ├─ gateway 网关（公网口 7777：
Unity 客户端 B ──┤   大厅消息 + 房间列表 + 匹配队列 +        └─ match 子进程 #2（并发第二局）
                 │   子进程生命周期 + 字节中继隧道 + AI 填位）
```

- **每局一进程**：引擎静态单例（GameCore/EventManager 进程级）由独立子进程天然满足
  （网络协议.md §13.1 预留方向的落地）。子进程 stdout 行协议上报 `READY/PHASE/BEAT/EXIT`。
- **字节中继隧道**：入座后客户端连接与子进程连接做帧级转发（上行解帧→原样重封保证入座
  切换的帧原子性，下行原始字节泵）。网关不解析对局流量——协议演进零感知。
- **AI 填位**：网关内 `BrainClient`（NetClientBrain 修订锁步）直连子进程，走完整握手链路。
- **崩溃安全**：Windows Job Object（KILL_ON_JOB_CLOSE）——网关死亡连带终止全部子进程，
  无孤儿对局进程。
- **回收策略**：终局 + 全员断开（5s 宽限）或空房闲置 180s 或子进程 60s 无心跳 → 杀子进程。

## 构建

```bash
dotnet build TideServer/TideServer.csproj      # .NET 10；共享源编译（无代码复制）
```

共享源 = `Assets/Scripts/HotUpdate/CardCore/**` + `Assets/Scripts/HotUpdate/AI/NeuralEnv/{LegalActionEnumerator,TideObservation,FieldValueReward}.cs`
+ `Assets/Scripts/HotUpdate/UI/Data/{CardCatalog,CardDataPaths}.cs`（2026-09-30 热更重排后路径，csproj 同步更新），
与 Unity 侧编译同一批文件（Phase 1 已完成去 Unity 化，见网络协议.md §14.3）。
版本钉死：MemoryPack 1.21.4（与 Assets/Packages 同版，MemoryPackOrder 标签生成一致）。

## 运行

```bash
dotnet run --project TideServer -- gateway               # 网关（默认 7777）
dotnet run --project TideServer -- gateway --port 8888
TideServer.exe gateway                                   # 发布产物同款参数

dotnet run --project TideServer -- match --port 0        # 单局子进程（通常由网关 spawn；独立跑=直连调试）
dotnet run --project TideServer -- selftest              # 极简冒烟（双 AI 整局×2 + 回收）
dotnet run --project TideServer -- loadcheck             # 数据装载自检
```

通用参数：`--port N`（gateway 默认 7777；match 默认 0=随机）、`--seed N`（钉局种子，
对拍用）、`--tick-ms N`（泵节奏，默认 15 ≈60Hz）。

## 端到端验证门禁（每次修改完、实际运行前跑）

```powershell
TideServer/verify.ps1              # 快档：三工程编译 + loadcheck + verify --quick（约 1 分钟）
TideServer/verify.ps1 -Full        # 全量档：含 V7 对拍 + V8 并发/断线（约 2-3 分钟）
TideServer/verify.ps1 -WithUnity   # 附加慢档：串行 Unity 批处理跑三验证器（十几分钟；改共享源用）
dotnet run --project TideServer -- verify --sections V1,V5   # 按段过滤（调试迭代用）
```

退出码即门禁结果（0=通过）。`TideServer verify` 分段与 Unity 回环验证器编号对齐：

| 段 | 内容 | quick |
|---|---|---|
| V0 | 数据装载自检（TideJson 读正式数据文件） | ✓ |
| V1 | 整局事件流与快照：投影完整性/线上去文本/帧半包粘包往返/快照双视角五路对账/字节确定性/中段栈采样 | ✓ |
| V2 | intent 通道：LegalActionEnumerator 枚举→信封→NetworkIntentApplier，四类动作覆盖+认输终局 | ✓ |
| V3 | 反问回环：LoopbackTargetSelector 全链路同步回环+真实引擎调用点（手牌上限弃牌）+headless 恢复 | ✓ |
| V5 | 开局握手：摘要确定性+正例往返+五反例（缺卡/重复/摘要漂移/缺摘要/回显篡改） | ✓ |
| V6 | 真实 socket 会话：分座/抢座拒绝/观战/握手双向/拒绝 Error 帧/脚本化整局/隐藏过滤/观战全信息 | ✓ |
| V7 | 同种子对拍：同进程三局+Reset 三件套，事件流逐值相等+终局快照字节相等+换种子发散 | ✗ |
| V8 | 网关/子进程（跨进程）：大厅反例/AI 填位整局+回收/双房并发/对局中断线作废/网关强杀 Job Object | 快子集 |

断言口径逐条对标 `Assets/Editor/验证/NetProtocolLoopbackVerifier.cs`（V8 为服务器专属新增）；
整局驱动 = 进程内 NetSessionServer + 双 NetClientBrain（M3 骨架），纯 .NET 泵下单局 5-10 秒。

## Unity 客户端连接

Play → MatchScreen → 服务器地址填网关 IP:7777 → 建房/匹配/AI 填位照旧。
同机测试：先起 gateway，再进 Play（编辑器自动起服会因 7777 被占静默退位为纯客户端）。

## 数据路径

TidePaths 自定位：从程序目录向上探测 `Assets/Configs/AttributeValueConfig.json`
（仓库根布局）——编辑器与 TideServer 同仓免配置。正式数据只读：
`Assets/Configs/`（原子表）+ `Assets/StreamingAssets/Card/`（卡表/效果/卡组）。

## 当前边界（v1，2026-09-27）

- 断线重连 / 心跳踢人 / 服务器主动代打：M4 范畴，未做。
- 观战：经网关的观战入口未开（直连 match 子进程端口的调试观战可用）。
- 子进程绑 127.0.0.1——子进程不跨机部署（网关可跨机）。
