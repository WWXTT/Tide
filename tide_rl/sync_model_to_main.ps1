# ============================================================================
# 训练产物回拷：临时工程 -> 主工程
#
# 回拷内容：
#   - tide_rl\card_identity_manifest.json —— 训练侧为权威整份覆盖（训练开局 Register
#     全池=超集；部署链路约束「训练/部署 manifest 必须同一份」）
#   - tide_rl\logs\<最新 run>\ —— best/last checkpoint + stats.jsonl
#   - tide_rl\exports\ —— 若曾在临时工程内导出过 onnx（可选）
#   - Assets\Resources\tide_policy* —— export_onnx 默认复制的部署产物（若存在）
#
# 之后建议在主工程 tide_rl 用 .venv-export 跑 export_onnx.py 完成部署导出
# （manifest 已同步，身份一致）。训练在跑时禁止同步（manifest 并发写）。
# ============================================================================

param(
    [string]$Main = "E:\UnityProject\Tide",
    [string]$TrainRoot = "E:\UnityProject\Tide_train",
    [switch]$Force
)

$ErrorActionPreference = "Stop"

$running = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" |
    Where-Object { $_.CommandLine -like "*$TrainRoot*" -and $_.CommandLine -like "*TideHeadlessServer*" }
if ($running) {
    Write-Error ("临时工程训练仍在跑（PID " + ($running.ProcessId -join ",") +
        "），manifest 会被并发写——先停训练再同步。")
    exit 1
}

# ---- 1) manifest（训练侧权威覆盖）----
$srcManifest = "$TrainRoot\tide_rl\card_identity_manifest.json"
$dstManifest = "$Main\tide_rl\card_identity_manifest.json"
if (Test-Path $srcManifest) {
    $srcCount = (Get-Content $srcManifest | Where-Object { $_ -match '^\d+ \d+$' }).Count
    $dstCount = 0
    if (Test-Path $dstManifest) {
        $dstCount = (Get-Content $dstManifest | Where-Object { $_ -match '^\d+ \d+$' }).Count
    }
    Write-Host "manifest：训练侧 $srcCount 条 -> 主工程 $dstCount 条（训练侧为权威，整份覆盖）"
    if ($srcCount -lt $dstCount -and -not $Force) {
        Write-Warning ("训练侧条数少于主工程（主工程期间登记过新卡身份，或临时工程 manifest 从零重建）——" +
                      "覆盖会丢主工程多出的行，该模型遇到对应卡时身份要重新登记续训。确认无碍请加 -Force。")
        exit 1
    }
    Copy-Item $srcManifest $dstManifest -Force
    Write-Host "[OK] manifest 已覆盖"
}
else {
    Write-Warning "临时工程无 manifest（还没跑过训练？）"
}

# ---- 2) 最新训练 run ----
$logsDir = "$TrainRoot\tide_rl\logs"
if (Test-Path $logsDir) {
    $latest = Get-ChildItem $logsDir -Directory | Sort-Object Name -Descending | Select-Object -First 1
    if ($latest) {
        $dst = "$Main\tide_rl\logs\$($latest.Name)"
        New-Item -ItemType Directory -Force -Path $dst | Out-Null
        Copy-Item "$($latest.FullName)\*" $dst -Recurse -Force
        Write-Host "[OK] 已回拷 run：$($latest.Name) -> $dst"
    }
}

# ---- 3) exports（若在临时工程导出过）----
$srcExports = "$TrainRoot\tide_rl\exports"
if (Test-Path $srcExports) {
    New-Item -ItemType Directory -Force -Path "$Main\tide_rl\exports" | Out-Null
    Copy-Item "$srcExports\*" "$Main\tide_rl\exports" -Recurse -Force
    Write-Host "[OK] 已回拷 exports/"
}

# ---- 4) Assets\Resources 部署产物（若存在）----
$srcRes = "$TrainRoot\Assets\Resources"
if (Test-Path $srcRes) {
    Get-ChildItem $srcRes -Filter "tide_policy*" -File | ForEach-Object {
        New-Item -ItemType Directory -Force -Path "$Main\Assets\Resources" | Out-Null
        Copy-Item $_.FullName "$Main\Assets\Resources" -Force
        Write-Host "[OK] 已回拷 Resources：$($_.Name)"
    }
}

Write-Host "完成。建议随后在主工程 tide_rl 下用 .venv-export 跑 export_onnx.py 重新导出部署（manifest 已同步）。"
