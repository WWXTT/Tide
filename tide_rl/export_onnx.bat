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
echo Example: export_onnx.bat logs\tide_ppo_selfplay_theme__42__xxxx\best\params.msgpack
echo Output : exports\tide_policy.onnx -^> Assets\Art\AIModels\（onnx+fixture 成对复制）
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
    echo   Tools/AI/模型fixture自检
    echo   Tools/AI/模型vs脚本主题批量对局
    echo ========================================
) else (
    echo ========================================
    echo   Export exited with code %EXITCODE%
    echo ========================================
)
echo.
pause
