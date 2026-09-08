# Tide AI 训练启动脚本（PowerShell）

Write-Host "========================================"
Write-Host "Tide AI Training Launcher"
Write-Host "========================================"
Write-Host ""

# 检查 Python
Write-Host "[1/4] Checking Python installation..."
try {
    $pythonVersion = python --version 2>&1
    Write-Host "  $pythonVersion"
} catch {
    Write-Host "Error: Python not found!" -ForegroundColor Red
    Write-Host "Please install Python 3.8+ and add it to PATH."
    Read-Host "Press Enter to exit"
    exit 1
}
Write-Host ""

# 检查依赖
Write-Host "[2/4] Checking dependencies..."
$depsCheck = python -c "import jax, flax, optax, gymnasium" 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "Warning: Some dependencies missing!" -ForegroundColor Yellow
    Write-Host "Installing required packages..."
    pip install jax[cpu] flax optax gymnasium numpy
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Error: Failed to install dependencies" -ForegroundColor Red
        Read-Host "Press Enter to exit"
        exit 1
    }
}
Write-Host "  Dependencies OK" -ForegroundColor Green
Write-Host ""

# 设置环境变量
Write-Host "[3/4] Setting up environment..."

# Unity 路径（从环境变量或默认位置）
if ($env:UNITY_PATH) {
    $unityPath = $env:UNITY_PATH
} else {
    $unityPath = "C:\Program Files\Unity\Hub\Editor\6000.5.8f1\Editor\Unity.exe"
}

if (-not (Test-Path $unityPath)) {
    Write-Host "Warning: Unity not found at default path" -ForegroundColor Yellow
    $unityPath = Read-Host "Enter Unity.exe path"
}
$env:UNITY_PATH = $unityPath

# Tide 项目路径
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectPath = Split-Path -Parent $scriptDir
$env:TIDE_PROJECT_PATH = $projectPath

Write-Host "  Unity: $unityPath"
Write-Host "  Project: $projectPath"
Write-Host ""

# 检查 ygo-agent
$ygoAgentPath = Join-Path $projectPath "ygo-agent-main"
if (-not (Test-Path $ygoAgentPath)) {
    Write-Host "Error: ygo-agent-main not found!" -ForegroundColor Red
    Write-Host "Please ensure ygo-agent-main is in the project root."
    Read-Host "Press Enter to exit"
    exit 1
}

$env:PYTHONPATH = "$ygoAgentPath;$env:PYTHONPATH"
Write-Host "  PYTHONPATH: $env:PYTHONPATH"
Write-Host ""

# 创建日志目录
$logsDir = Join-Path $projectPath "logs"
if (-not (Test-Path $logsDir)) {
    New-Item -ItemType Directory -Path $logsDir | Out-Null
}

Write-Host "[4/4] Starting training..."
Write-Host ""
Write-Host "========================================"
Write-Host "Training will begin in 3 seconds..."
Write-Host "Press Ctrl+C to cancel"
Write-Host "========================================"
Start-Sleep -Seconds 3

# 启动训练
Set-Location $scriptDir

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$logFile = Join-Path $logsDir "train_$timestamp.log"

Write-Host ""
Write-Host "Log file: $logFile"
Write-Host ""

# 运行训练并同时输出到控制台和日志文件
python train_tide_complete.py 2>&1 | Tee-Object -FilePath $logFile

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "========================================"
    Write-Host "Training completed successfully!" -ForegroundColor Green
    Write-Host "========================================"
} else {
    Write-Host ""
    Write-Host "========================================"
    Write-Host "Training failed with error code $LASTEXITCODE" -ForegroundColor Red
    Write-Host "========================================"
}

Read-Host "Press Enter to exit"
