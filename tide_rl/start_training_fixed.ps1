# Tide AI Training Launcher (Fixed)
# PowerShell script to set environment and start training

$ErrorActionPreference = "Stop"

Write-Host "===================================="
Write-Host "Tide AI Training - Fixed Launcher"
Write-Host "===================================="
Write-Host ""

# Set Unity path
$env:UNITY_PATH = "C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe"

# Set Python path to include ygo-agent-main and project root
$projectRoot = Split-Path -Parent $PSScriptRoot
$env:PYTHONPATH = "$projectRoot;$projectRoot\ygo-agent-main"

Write-Host "Environment:"
Write-Host "  Unity: $env:UNITY_PATH"
Write-Host "  Project: $projectRoot"
Write-Host "  PYTHONPATH: $env:PYTHONPATH"
Write-Host ""

# Check Unity exists
if (-not (Test-Path $env:UNITY_PATH)) {
    Write-Host "[ERROR] Unity not found at: $env:UNITY_PATH" -ForegroundColor Red
    Write-Host "Please install Unity 6000.5.10f1 or update the UNITY_PATH in this script" -ForegroundColor Red
    exit 1
}

Write-Host "[OK] Unity found" -ForegroundColor Green

# Check Python dependencies
Write-Host ""
Write-Host "Checking Python dependencies..."

try {
    python -c "import jax, flax, optax, gymnasium; print('[OK] All dependencies installed')" 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "Import failed"
    }
} catch {
    Write-Host "[ERROR] Python dependencies missing" -ForegroundColor Red
    Write-Host "Run: pip install jax==0.4.35 jaxlib==0.4.35 flax==0.8.5 optax gymnasium distrax" -ForegroundColor Yellow
    exit 1
}

Write-Host ""
Write-Host "===================================="
Write-Host "Starting Training..."
Write-Host "===================================="
Write-Host ""
Write-Host "Press Ctrl+C to stop training"
Write-Host ""

# Start training
Set-Location $PSScriptRoot
python train_tide_complete.py

Write-Host ""
Write-Host "Training finished!"
