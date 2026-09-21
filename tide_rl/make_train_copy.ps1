# ============================================================================
# 训练临时工程复制脚本：主工程 -> E:\UnityProject\Tide_train
#
# 用法：
#   powershell -ExecutionPolicy Bypass -File make_train_copy.ps1            # 不存在则创建
#   powershell -ExecutionPolicy Bypass -File make_train_copy.ps1 -Refresh   # 镜像刷新（更新代码/资源）
#
# 设计要点：
#   - 排除 .git/Library/Temp/Logs/obj/.vs/.zcode/.codegraph/UserSettings——
#     Library 不复制：首次 batchmode 启动会重新导入（一次性，几分钟）；
#   - 训练产物（tide_rl\logs、exports）与 card_identity_manifest.json 是临时工程
#     自有状态：/MIR 刷新时排除项不动，跨刷新保留（manifest 只在临时工程内追加）；
#   - 主工程改了卡/效果表后：先跑 Tools/构建三色主题卡组 重建引用链，再 -Refresh。
#     -Refresh 会终止依赖旧代码的在跑训练（检测到训练进程会警告并中止，-Force 跳过）。
# ============================================================================

param(
    [string]$Src = "E:\UnityProject\Tide",
    [string]$Dest = "E:\UnityProject\Tide_train",
    [switch]$Refresh,
    [switch]$Force
)

$ErrorActionPreference = "Stop"

$excludeDirs = @(
    "$Src\.git", "$Src\Library", "$Src\Temp", "$Src\Logs", "$Src\obj", "$Src\.vs",
    "$Src\.zcode", "$Src\.codegraph", "$Src\UserSettings",
    "$Src\tide_rl\logs", "$Src\tide_rl\exports", "$Src\tide_rl\.venv-export"
)
$excludeFiles = @(
    "$Src\tide_rl\card_identity_manifest.json",
    "$Src\tide_rl\card_identity_manifest.json.tmp"
)

# 在跑训练检测：Unity batchmode 指向临时工程 = 有训练在用旧快照
$running = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" |
    Where-Object { $_.CommandLine -like "*$Dest*" -and $_.CommandLine -like "*TideHeadlessServer*" }
if ($running) {
    if (-not $Force) {
        Write-Error ("临时工程上有训练 batchmode 正在运行（PID " + ($running.ProcessId -join ",") +
            "）。-Refresh 会杀掉它——先停训练，或加 -Force 强制（风险自担）。")
        exit 1
    }
    Write-Warning "强制模式：跳过在跑训练检查（训练进程可能行为异常）"
}

if ((Test-Path "$Dest\Assets") -and -not $Refresh) {
    Write-Host "临时工程已存在：$Dest（更新请加 -Refresh）"
    exit 0
}

if ($Refresh) { Write-Host "镜像刷新：$Src -> $Dest（排除项保留不动）" }
else          { Write-Host "首次复制：$Src -> $Dest（不含 Library，首次启动会重新导入）" }

# /MIR 镜像同步；/XD //XF 排除项在目标侧同样不动（robocopy 语义）
& robocopy $Src $Dest /MIR /R:2 /W:2 /NFL /NDL /NP /XD $excludeDirs /XF $excludeFiles
$rc = $LASTEXITCODE
if ($rc -ge 8) { Write-Error "robocopy 失败，退出码 $rc（>=8 才是失败，1-7 为成功/跳过）"; exit $rc }

Write-Host "[OK] 临时工程就绪：$Dest（robocopy 代码 $rc）"
if (-not (Test-Path "$Dest\Library")) {
    Write-Host "提示：Library 未复制，首个 batchmode 启动会重新导入资产（一次性，几分钟到十几分钟）"
}
Write-Host "下一步：powershell -ExecutionPolicy Bypass -File start_training_traincopy.ps1"
