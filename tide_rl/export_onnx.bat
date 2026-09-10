@echo off
title Tide ONNX Export
cd /d "%~dp0"

echo ========================================
echo   Tide Policy ONNX Export (isolated venv)
echo ========================================
echo.

if not exist ".venv-export\Scripts\python.exe" (
    echo [SETUP] Export venv missing - creating it now...
    python -m venv .venv-export
    .venv-export\Scripts\python.exe -m pip install --quiet --upgrade pip
    .venv-export\Scripts\python.exe -m pip install "jax[cpu]==0.11.1" "flax==0.12.9" jax2onnx onnxruntime
    if errorlevel 1 (
        echo [ERROR] venv setup failed.
        pause
        exit /b 1
    )
)

echo Usage  : export_onnx.bat [ckpt_arg]   (default = latest selfplay best)
echo Example: export_onnx.bat logs\tide_ppo_selfplay__42__1788965027\last\params.msgpack
echo Output : exports\tide_policy.onnx -^> Assets\StreamingAssets\
echo ========================================
echo.

if "%~1"=="" (
    .venv-export\Scripts\python.exe export_onnx.py
) else (
    .venv-export\Scripts\python.exe export_onnx.py --ckpt "%~1"
)
set EXITCODE=%ERRORLEVEL%

echo.
if "%EXITCODE%"=="0" (
    echo ========================================
    echo   Export OK. Unity menu:
    echo   Tools/AI/ONNX 策略/1. 数值对拍 (fixture)
    echo   Tools/AI/ONNX 策略/2. vs SimpleAI 20 局
    echo ========================================
) else (
    echo ========================================
    echo   Export exited with code %EXITCODE%
    echo ========================================
)
echo.
pause
