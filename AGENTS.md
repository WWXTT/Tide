# Tide 项目约定

## 临时文件目录（2026-11-09 定案）
- 会话临时文件（探针/审计脚本、dump、中转输出、批量操作工作文件等）一律创建到 `C:\Users\Administrator\Desktop\UnityProject\Tide\.ztmp`（已进 .gitignore）。

## UI 只绑定不生成（2026-11-10 定案，用户反复强调）

- 界面元素一律烘焙进预制体/场景节点（用户在编辑器里排版可改），代码只按名绑定与填数；禁止用代码临时拼装 UI（UiKit.Row/Label/BgRow 等运行时搭节点树、拼弹窗面板）。
- 动态列表行 = 共用行预制体 Instantiate 到 content 下（先例：CostChart / stats-row / deck-row / room-row / history-row）；锚点、行高、底色、按钮、文案版式全由预制体携带，代码不改版式。
- 唯一允许的代码生成 = 实例化共用预制体（外加给烘焙节点补挂非视觉组件如 Button 监听）。交付前自查层级：发现新的临时生成 UI 元素即违规，先出预制体再接代码。

## 新卡费用必须走推导（2026-11-16 定案，用户强调）

- 新卡/新效果的费用只允许由 `CardCostService` / `CostDerivationService` 从卡内容（身材、关键词、原子、代价）推导。
- 禁止手填 `costList` 当权威价；禁止在 `CardCostService` 里按卡 ID 硬编码费用绕过推导。
- 推导结果不符合预期时，改卡内容/原子/组合/关键词或补正式计费规则，不得直接给一个“看起来合理”的费用。
