# 聚光灯 GameJam 2026 参赛台账

- 赛事：TapTap 聚光灯 21 天 GameJam（第三届），主题「涌现」，2026-10-01 公布并起算
- 仓库基线：tag `jam-baseline-20261001` = 全部赛前工作快照（提交 f6ac0ed）
- 划分规则：tag 之前 = 赛前基础；tag 之后 = 比赛期工作。commit 日期即工作量划分证据

## 申报口径（三档）

| 档 | 内容 | 清单 |
|---|---|---|
| 第三方 | Unity/URP、Crest、官方 Samples、Asset Store 资产 | [third-party.md](third-party.md) |
| 赛前自研基础 | Tide 引擎层、HexMap、dbdrp、TideServer、tide_rl | [pre-existing-base.md](pre-existing-base.md) |
| 赛内新增 | 参赛游戏本体：玩法、卡牌内容、表现、打磨 | `git log jam-baseline-20261001..` |

## 原则

1. **声明必须与 git 历史对得上**：历史里看得到的（原型、卡牌配置、测试）一律写进"赛前基础"，不写"仅引擎"。
2. **剥离是口径上的剥离**：基线是快照，不从基线里物理删除代码；参赛游戏本体（玩法/内容/表现）由 tag 之后的提交构成，赛前基础只作为引擎/框架/中间件被使用。
3. **AI 使用全登记**：资产生成、辅助编程逐项记入 [ai-usage.md](ai-usage.md) 与 [asset-ledger.md](asset-ledger.md)，提交时汇总为官方要求的说明文档（未声明 = 放弃所有奖项评选）。
4. **创意主导证据**：核心设计出自根目录《设计文稿.md》《项目概览.md》（时间早于比赛）；比赛期开发日志见 [devlog/](devlog/)。
5. **赛制约束——禁联机**：参赛构建为单机构建。联机/热更基础设施（TideServer、HybridCLR/YooAsset 接入层）保留在工程但不启用、不随包分发。
6. **内部研究资料不外流**：三份效果列表（`Config/*效果列表*.md`）、`Config/yugioh_data`、`ygo-agent-main` 仅限设计研究，不进参赛构建；如需对外提供仓库快照（孤儿分支），必须先剔除上述目录。
