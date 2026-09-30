# Tide 动作空间 v2 契约（C 期：效果发动时机 + 目标选择）

定案日期：2026-09-30（用户批准的实施计划）。本文档是 C#（Unity）与 Python（训练）两侧实现的**唯一权威契约**，
两侧任何常量/布局改动必须先改这里再改代码。版本号 `LAYOUT_VERSION = 2`。

## 0. 目标与范围

把「效果发动时机 + 目标选择」纳入模型动作空间：

- 指向性效果目标：模型显式选择（此前 targets=null → 引擎 `AutoSelect` 取候选前 N 个）。
- 自愿触发效果（Voluntary 桶）：模型决定发/不发（此前整局滞留队列，只有 SimpleAI 会发）。
- 英雄技能：模型主动发动（此前只有脚本会用）。
- **对方回合响应窗口**：守卫宣言 / 响应出牌 / 响应发动 / 响应期自愿触发——模型在对方回合也有决策点。

明确不做：网络对战 IntentActivateHeroSkill 协议增补（表现层后续）；墓地出牌回动作空间（标准池无墓地出牌来源卡）。

## 1. 动作类型 TideActionType（C# `LegalActionEnumerator.cs`）

| 值 | 类型 | 语义 | Apply 映射 |
|---|---|---|---|
| 0 | PlayCard | 手牌打出（含抉择模式），**开始带目标** | `GameActions.PlayCard(core, me, card, targets, Zone.Hand, mode)` |
| 1 | PlayLand | 手牌放入元素池 | `GameActions.AddToElementPool` |
| 2 | （保留缺口） | 墓地出牌，2026-09-09 移除，勿复用 | — |
| 3 | Activate | 战场卡激活式能力，**开始带目标** | 带目标路径：`PendingEffect.SelectedTargets = targets` → `StackEngine.PlayerActivateVoluntary`（镜像 `NetworkIntentApplier.ActivateWithTargets`） |
| 4 | Attack | 攻击宣言（attacker → 对方随从/玩家） | `GameActions.DeclareAttack` |
| 5 | EndTurn | 结束回合（己方 Main 专属，恒排动作表末位） | `GameActions.EndTurn` |
| 6 | HeroSkill | 己方回合发动英雄技能；**modeIndex 字段承载 HeroSkillId（1/2/3）** | `GameActions.ActivateHeroSkill`（不走栈、立即结算；异步需在无头环境同步收敛） |
| 7 | VoluntaryTrigger | 发动自愿桶触发效果 | `StackEngine.PlayerActivateVoluntary` |
| 8 | PassPriority | 响应窗口让过/放弃（**仅响应窗口出现**，Main 阶段不出现；恒排响应表末位） | `GameActions.PassPriority` |
| 9 | RespondPlay | 响应出牌（带目标展开） | `GameActions.PlayCardInResponse` |
| 10 | RespondActivate | 响应发动（带目标） | 响应发动路径（同 3 的带目标口径） |
| 11 | Guard | 守卫宣言 | `GameActions.ApplyResponse` 守卫分支（以引擎 `CollectAvailableResponses` 实际形态为准） |

## 2. 动作特征 6 维 → 8 维（`TideObservation.NAction = 8`）

```
[0] valid        恒 1
[1] type         0..11
[2] sourceIndex  cards_ 80 槽下标（-1=无）
[3] targetIndex  cards_ 80 槽下标（-1=无目标或玩家目标）
[4] modeIndex    PlayCard 抉择下标 / HeroSkill=skillId / 其余 0
[5] actionCost   费用总和浮点——Activate/Voluntary/HeroSkill 也要填（不再只 PlayCard）
[6] effectIdentity 效果定义哈希在 TideCardIndex 的注册下标 / 1024.0（追加式注册，不动表指纹）；
                  PlayCard/PlayLand/Attack/EndTurn/PassPriority = 0。
                  解决"同一卡多效果行特征重合"的既有缺陷（Activate 多效果时 6 维布局下各行完全相同）。
[7] targetKind   0=无目标/区域自结算，1=卡目标，2=对方玩家，3=己方玩家
```

Python 侧命名下标：`ACTION_F_VALID=0, ACTION_F_TYPE=1, ACTION_F_SOURCE=2, ACTION_F_TARGET=3,
ACTION_F_MODE=4, ACTION_F_COST=5, ACTION_F_EFFECT_ID=6, ACTION_F_TARGET_KIND=7`。

## 3. 观测 globals 36 → 49 维（`TideObservation.NGlobal = 49`，追加不改前 36 维）

```
g[36..39]  P1 英雄技能 [hasSkill(0/1), 本回合已用 tapped(0/1), 累计使用数/10, 已升级(0/1)]
g[40..43]  P2 同上
g[44]      栈深度 /16
g[45]      栈顶来源卡槽下标 /80（无栈=0）
g[46]      栈顶效果 effectIdentity（同第 2 节 [6] 口径；无栈=0）
g[47]      决策上下文（0=己方 Main，1=响应窗口）
g[48]      决策座次是否回合玩家（0/1）
```

### 观测视角参数化

`TideObservation.Build(core)` → `Build(core, seat)`：seat=当前**决策座次**（响应窗口时=响应方而非回合玩家）。
手牌隐藏等隐藏信息按"seat 的对手"计算。`TideStepResult` 与 TCP 协议 `info.toPlay` 必须报真实决策座次
（Python GAE 座次符号修正沿用现有逐步 toPlay 机制，算法不改）。

## 4. 容量与预算

- `MAX_ACTIONS = 512`（三处同步：Python `tide_features.py` / C# `OnnxTidePolicy.MaxActions` / 服务端按实际行数序列化不变）。
  EndTurn（Main 表）与 PassPriority（响应表）恒排各自动作表**末位**——截断兜底依赖这一点。
- `MaxActionsPerTurn = 256`（`TideHeadlessDriver`，防死循环保险）。
- 多目标（Multiple/MultipleUnion）：按引擎候选序枚举组合，**每效果上限 24 组**截断；Single/SingleUnion 逐目标全展开；
  None/Whole/Random 单行。
- Python `max_episode_steps = 2000`（响应停靠仅在候选>0 时发生，步数增长有界）。

## 5. TCP 协议版本握手（防静默布局漂移炸 reshape）

reset/step 响应 `info` 追加：

```json
{"layoutVersion": 2, "dims": {"nCard": 65, "nGlobal": 49, "nAction": 8, "maxActions": 512}, "decisionSeat": 0}
```

Python 侧 reset 时**逐值断言**相等，不等立即报清晰错误（替代旧 N_GLOBAL 32→36 时的静默 reshape 炸）。
C# 侧常量必须从同一组数值出发（建议 C# 内也定义 `LayoutVersion=2` 常量并回显）。

## 6. 口径决策（已拍板）

1. **vs-SimpleAI 模式响应窗口双侧自动让过**：SimpleAI 整回合自动保持现状、模型在 SimpleAI 回合不获决策点
   ——vs 红轴 0.75 早停口径与旧数字可比。响应能力由**自对弈**训练，评估看自对弈胜率对称性 + 新动作类型使用率。
2. 自对弈模式：两座次都由模型驱动，响应窗口对优先权持有方停靠（候选>0 才停）。
3. 墓地出牌维持移除；卡身份 manifest（`card_identity_manifest.json`）不动——卡特征 65 维与原子表不变，
   effectIdentity 走 TideCardIndex 追加式注册（**不得**混入会改变表指纹的字段）。
4. 旧 checkpoint / fixture / onnx 全部作废（动作编码器输入维 6→8、全局 36→49），新训练目录标签 `v2c`。

## 7. 双侧同步检查清单（验收用）

| 项 | C# | Python | 期望值 |
|---|---|---|---|
| 布局版本 | TideHeadlessServer 回显 | `LAYOUT_VERSION` | 2 |
| 动作上限 | `OnnxTidePolicy.MaxActions` | `MAX_ACTIONS` | 512 |
| 动作特征维 | `TideObservation.NAction` | `N_ACTION_FEATURES` | 8 |
| 全局维 | `TideObservation.NGlobal` | `N_GLOBAL_FEATURES` | 49 |
| 卡特征维 | `TideObservation.NCard` | `N_CARD_FEATURES` | 65（不变） |
| 每回合动作上限 | `TideHeadlessDriver.MaxActionsPerTurn` | —（不进模型） | 256 |
| 动作类型值域 | `TideActionType` | 审计脚本对照表 | 0..11 见第 1 节 |

## 8. 已知限制（验收报告须重申）

- vs-SimpleAI 评估不演练模型响应能力（第 6.1 条）。
- 多目标组合 >24 截断（极端场面下模型看不到部分组合）。
- 响应步计入 λ·ΔΦ 塑形，弧长变长——λ 先维持 0.005 观察曲线。
- 网络对战模式英雄技能按钮维持禁用（协议缺口本期不补）。

## 9. Unity 侧实施任务清单（U1-U7，另一会话执行；2026-09-30 分工定案）

> 分工：Python 侧（tide_rl/*.py）由训练会话完成并已落地；Unity 侧（Assets/**）由对战会话执行；
> 文件避让：对战会话只动 `Assets/**`，`tide_rl/**` 归训练会话。最终验收（刷新/编译/审计/冒烟/汇总）
> 由训练会话执行。**注意：Python 侧已切换 v2 常量，Unity 侧未完成 U5 前，TCP 一连上就会在
> reset 布局断言处响亮报错——这是预期行为（防止两侧不同代静默混跑）。**

- **U1 常量与枚举**（只依赖本契约）：`LegalActionEnumerator.cs` TideActionType 增 6..11（第 1 节）；
  `TideObservation.NAction=8`、`NGlobal=49`；`TideHeadlessDriver.MaxActionsPerTurn=256`；
  `OnnxTidePolicy.MaxActions=512`。
- **U2 LegalActionEnumerator 扩展**（依赖 U1）：
  - 逐目标展开：PlayCard 按 (卡, 模式, 目标) 展开（候选枚举镜像 `SimpleAI.ChooseTargets` 的
    `EffectHandlerRegistry.ResolveCandidates` 口径），Activate 同；None/Whole/Random 单行；
    Multiple/MultipleUnion 按候选序组合上限 24。
  - 新类型行：HeroSkill（modeIndex=skillId）、VoluntaryTrigger、响应窗口行
    （`GameActions.CollectAvailableResponses` 映射 + PassPriority 恒排响应表末位）。
  - `BuildFeatures` 8 维（第 2 节布局）；`Apply` 全类型分派（带目标 Activate 走
    `PendingEffect.SelectedTargets` 口径，镜像 `NetworkIntentApplier.ActivateWithTargets`）。
  - `Signature` 纳入目标与决策上下文（防误摘除）。
- **U3 TideObservation**（依赖 U1）：`Build(core, seat)` 决策座次视角参数化（隐藏信息按 seat 的对手算）；
  globals 追加 g[36..48]（第 3 节布局）；effectIdentity 注册走 TideCardIndex 追加式（不动表指纹）。
- **U4 TideHeadlessDriver 决策点泛化**（依赖 U2/U3，工程量最大）：Step 内 DrainStack 的双 Pass
  替换为响应循环——栈非空时对优先权方收集候选，候选>0 停靠问模型（自对弈两座次都问；
  vs-SimpleAI 双侧自动让过保持现口径，第 6.1 条）；决策座次写进 TideStepResult 与 info.toPlay；
  防死循环兜底（速度门 + MaxActionsPerTurn=256 + EndTurn/Pass 恒在表末）。
- **U5 TideHeadlessServer**（依赖 U4）：reset/step 响应 info 加 `layoutVersion=2`、
  `dims={nCard:65,nGlobal:49,nAction:8,maxActions:512}`、`decisionSeat`（与 toPlay 同值）。
- **U6 跟随检查**（依赖 U2-U4）：NeuralAI / OnnxBatchBattle / AiBattleE2E 编译与跑通（复用同一枚举器应自动跟随）。
- **U7 OnnxFixtureSelfCheck**：旧 fixture 作废口径更新（等 Python 侧重导出后闭环）。

Unity 完成后验收入口（训练会话执行）：`python tide_rl/audit_action_space.py`（编辑器 TCP 9999）→
`quick_train_tcp.py` 短训 → 汇总。
