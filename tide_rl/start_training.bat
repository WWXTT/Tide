@echo off
REM Tide AI 训练启动脚本（Windows）
REM
REM 使用方法：
REM   1. 双击运行（使用默认配置）
REM   2. 或在 PowerShell 中运行并传参：
REM      .\start_training.bat --steps 1000000

echo ========================================
echo Tide AI Training Launcher
echo ========================================
echo.

REM 检查 Python
where python >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo Error: Python not found!
    echo Please install Python 3.8+ and add it to PATH.
    pause
    exit /b 1
)

echo [1/4] Checking Python installation...
python --version
echo.

REM 检查依赖
echo [2/4] Checking dependencies...
python -c "import jax, flax, optax, gymnasium" 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo Warning: Some dependencies missing!
    echo Installing required packages...
    pip install jax[cpu] flax optax gymnasium numpy
    if %ERRORLEVEL% NEQ 0 (
        echo Error: Failed to install dependencies
        pause
        exit /b 1
    )
)
echo Dependencies OK
echo.

REM 设置环境变量
echo [3/4] Setting up environment...

REM Unity 路径（请根据实际情况修改）
set UNITY_PATH=C:\Program Files\Unity\Hub\Editor\6000.5.8f1\Editor\Unity.exe
if not exist "%UNITY_PATH%" (
    echo Warning: Unity not found at default path
    echo Please set UNITY_PATH environment variable manually
    echo.
    set /p UNITY_PATH="Enter Unity.exe path: "
)

REM Tide 项目路径
set TIDE_PROJECT_PATH=%~dp0..
echo   Unity: %UNITY_PATH%
echo   Project: %TIDE_PROJECT_PATH%
echo.

REM 检查 ygo-agent
if not exist "%~dp0..\ygo-agent-main" (
    echo Error: ygo-agent-main not found!
    echo Please ensure ygo-agent-main is in the project root.
    pause
    exit /b 1
)

set PYTHONPATH=%~dp0..\ygo-agent-main;%PYTHONPATH%
echo   PYTHONPATH: %PYTHONPATH%
echo.

REM 创建日志目录
if not exist "%~dp0..\logs" mkdir "%~dp0..\logs"

echo [4/4] Starting training...
echo.
echo ========================================
echo Training will begin in 3 seconds...
echo Press Ctrl+C to cancel
echo ========================================
timeout /t 3 >nul

REM 启动训练
cd /d "%~dp0"

REM 创建日志目录
if not exist "..\logs" mkdir "..\logs"

echo Starting training...
echo Log will be saved to: ..\logs\training.log
echo.

python train_tide_complete.py

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ========================================
    echo Training completed successfully!
    echo ========================================
) else (
    echo.
    echo ========================================
    echo Training failed with error code %ERRORLEVEL%
    echo ========================================
)

pause
