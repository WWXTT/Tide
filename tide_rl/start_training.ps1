# ============================================================================
# 已退役（2026-11-18 普查 P0 收口）——不要用本脚本在主工程上直跑训练。
#
# 旧路径两处硬伤：
#   1) 依赖项目根 ygo-agent-main（本机已不存在）；
#   2) 主工程 Unity 编辑器常开（工程锁），训练/batchmode 直接撞锁。
#
# 正确入口（副本隔离训练，2026-11-17 定案）：
#   start_training.bat              —— 常用：隐藏窗口拉起 C:\UnityProject\Tide_train 启动器
#   start_training_traincopy.ps1    —— 前台跑副本（-TrainRoot 可改副本位置）
#   make_train_copy.ps1 -Refresh    —— 重拷/刷新副本代码与资源
# ============================================================================

Write-Host "此脚本已退役：不要在主工程上直跑训练（ygo-agent-main 缺失 + 主工程编辑器工程锁）。" -ForegroundColor Red
Write-Host "请改用：  start_training.bat（隐藏窗口拉起副本）  或  start_training_traincopy.ps1（前台，副本 C:\UnityProject\Tide_train）"
exit 1
