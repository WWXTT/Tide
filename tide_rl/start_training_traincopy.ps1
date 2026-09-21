# ============================================================================
# 在临时工程上启动训练（不存在则先复制；主工程完全不被触碰）
#
# 用法：
#   powershell -ExecutionPolicy Bypass -File start_training_traincopy.ps1
#   powershell -ExecutionPolicy Bypass -File start_training_traincopy.ps1 -- -h   # 透传给训练脚本
#
# 训练进程用 -projectPath 指向临时工程；tide_rl 的 logs/manifest/Unity 日志全部
# 落在临时工程内。主工程 Unity 编辑器可同时开着（不同工程锁互不影响）。
# ============================================================================

param(
    [string]$TrainRoot = "E:\UnityProject\Tide_train",
    [switch]$NoCopy
)

$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path

# 临时工程不存在 -> 先复制一份
if (-not (Test-Path "$TrainRoot\tide_rl\train_tide_complete.py")) {
    if ($NoCopy) { Write-Error "临时工程不存在：$TrainRoot（先跑 make_train_copy.ps1）"; exit 1 }
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $here "make_train_copy.ps1")
    if ($LASTEXITCODE -ne 0) { Write-Error "复制临时工程失败"; exit $LASTEXITCODE }
}

# 在跑检测：stdio 桥一个工程同时只允许一个 batchmode（TideEnv 也会拦，这里先给清晰报错）
$running = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" |
    Where-Object { $_.CommandLine -like "*$TrainRoot*" -and $_.CommandLine -like "*TideHeadlessServer*" }
if ($running) {
    Write-Error ("临时工程已有训练 batchmode 在跑（PID " + ($running.ProcessId -join ",") +
        "）——一进程一局。等它结束或手动 taskkill。")
    exit 1
}

Push-Location "$TrainRoot\tide_rl"
try {
    Write-Host "== 训练工作目录：$TrainRoot\tide_rl（工程/日志/manifest/模型 全在临时工程）=="
    python train_tide_complete.py @args
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
