# ============================================================================
# 已退役（2026-11-18 普查 P0 收口）——与 start_training.ps1 同款旧路径：
# PYTHONPATH 依赖已不存在的 ygo-agent-main，且在主工程 tide_rl 直跑训练
# （主工程 Unity 编辑器常开=工程锁冲突）。
#
# 正确入口（副本隔离训练，2026-11-17 定案）：
#   start_training.bat              —— 常用：隐藏窗口拉起 C:\UnityProject\Tide_train 启动器
#   start_training_traincopy.ps1    —— 前台跑副本（-TrainRoot 可改副本位置）
# ============================================================================

Write-Host "此脚本已退役：旧 ygo-agent 启动路径（依赖缺失 + 主工程编辑器工程锁）。" -ForegroundColor Red
Write-Host "请改用：  start_training.bat（隐藏窗口拉起副本）  或  start_training_traincopy.ps1（前台，副本 C:\UnityProject\Tide_train）"
exit 1
