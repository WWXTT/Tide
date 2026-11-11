# Tide 项目约定

## 临时文件目录（2026-11-09 定案）

- 会话临时文件（探针/审计脚本、dump、中转输出、批量操作工作文件等）一律创建到 `C:\Users\Administrator\Desktop\UnityProject\Tide\.ztmp`（已进 .gitignore）。
- 旧目录 `ztmp/`（项目根）与 `.zcode/ztmp/` 已退役：不再写入新文件；存量内容按历史归档处理，不迁移不删除。

## UI 只绑定不生成（2026-11-10 定案，用户反复强调）

- 界面元素一律烘焙进预制体/场景节点（用户在编辑器里排版可改），代码只按名绑定与填数；禁止用代码临时拼装 UI（UiKit.Row/Label/BgRow 等运行时搭节点树、拼弹窗面板）。
- 动态列表行 = 共用行预制体 Instantiate 到 content 下（先例：CostChart / stats-row / deck-row / room-row / history-row）；锚点、行高、底色、按钮、文案版式全由预制体携带，代码不改版式。
- 唯一允许的代码生成 = 实例化共用预制体（外加给烘焙节点补挂非视觉组件如 Button 监听）。交付前自查层级：发现新的临时生成 UI 元素即违规，先出预制体再接代码。
