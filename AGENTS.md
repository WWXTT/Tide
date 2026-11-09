# Tide 项目约定

## 临时文件目录（2026-11-09 定案）

- 会话临时文件（探针/审计脚本、dump、中转输出、批量操作工作文件等）一律创建到 `C:\Users\Administrator\Desktop\UnityProject\Tide\.ztmp`（已进 .gitignore）。
- 旧目录 `ztmp/`（项目根）与 `.zcode/ztmp/` 已退役：不再写入新文件；存量内容按历史归档处理，不迁移不删除。
