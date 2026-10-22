# TideServer —— 分离的权威服务器

> **生产退役注记（2026-10-21 P2P 定案）**：生产拓扑改为房主=进程内 NetSessionServer 的 P2P 直连
> （定案见根目录 `网络协议.md` §15）——本工程不再作为生产服务器部署，gateway/match 子命令闲置保留。
> **verify 套件仍是共享源回归门禁**；新增 `TideServer p2pcheck` = P2P 拓扑无头冒烟
> （进程内房主 + 双回环客户端整局，含 IntentActivateHeroSkill(113) 路由与端到端）。

独立于 Unity 的专用服务器进程（2026-09-27 立项，定案见根目录 `网络协议.md` §14）。
**线格式零改动**——网关曾支持「匹配界面填 `服务器IP:7777` 直连」，该链路随 `MatchScreen` 于 2026-10-18 整删；
现行 Unity 侧联机 = `LobbyScreen`（P2P 房主即服务器），本工程的 gateway 只作调试/回归用（端口占用时
`NetLobbyHost.StartServer` 直接抛出，**不再有自动退位机制**）。

## 架构

```
Unity 客户端 A ──┐                          ┌─ match 子进程 #1（NetSessionServer 宿主，0.0.0.0:随机口）
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
- **回收策略**（`GatewayHost` 泵侧实况）：开过局的房间回 Waiting 且全员离开 5s / Finished 挂空 10s / 从未开局且空 5s → 杀子进程；另有子进程 60s 无心跳。**旧文档的「空房闲置 180s」只存在于注释，无实现分支**。

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

退出码即门禁结果（0=通过）。`TideServer verify` 分段：V0-V7 对标 Unity 回环验证器，**V8-V10 为服务器侧新增**（Unity 验证器只有 1/2/3/5/6/7 段）：

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
| V9 | 引擎规则回归（三类卡型定案：结界耐久/技能/关键词口径） | ✓ |
| V10 | 战报映射（日志中间层 B1：事件→战报行） | ✓ |

断言口径逐条对标 `Assets/Editor/验证/NetProtocolLoopbackVerifier.cs`（V8/V9/V10 为服务器专属新增）；
整局驱动 = 进程内 NetSessionServer + 双 NetClientBrain（M3 骨架），纯 .NET 泵下单局 5-10 秒。

## Unity 客户端连接

P2P 生产拓扑下客户端不再连本网关：Play → 主菜单「联机对战」= `LobbyScreen`（房主即服务器）——
创建房间=本进程起 `NetSessionServer` 并回环入座，客人填房主 `IP:端口` 直连（默认端口 28000）。
调试/回归时仍可对 gateway：`LobbyScreen` 的加入口填网关 `IP:7777` 即可沿用大厅层（§13）。

## 数据路径

TidePaths 自定位：从程序目录向上探测 `Assets/Configs/AttributeValueConfig.json`
（仓库根布局）——编辑器与 TideServer 同仓免配置。正式数据只读：
`Assets/Configs/`（原子表/规则表）+ `Assets/CardData/`（玩家池卡表/效果/卡组）+ `Assets/TutorialData/`（教学卡表/效果/三套预组）。
`Assets/StreamingAssets/Card/` 已不存在（2026-10-18 换 YooAsset 后 StreamingAssets 下只剩 `yoo/`）。

## 当前边界（v1，2026-09-27）

- 断线重连 / 心跳踢人 / 服务器主动代打：M4 范畴，未做。
- 观战：经网关的观战入口未开（直连 match 子进程端口的调试观战可用）。
- 子进程监听 `IPAddress.Any:随机口`（**非 127.0.0.1 绑定**；`NetTcpHost.Start` 口径），实际可听全网卡——网关与子进程同机部署时不外露靠端口随机 + 无公开端口映射。
