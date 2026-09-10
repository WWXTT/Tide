@echo off
title Tide AI Training
cd /d "%~dp0"

echo ========================================
echo   Tide AI Training Launcher
echo ========================================
echo.

where python >nul 2>nul
if errorlevel 1 (
    echo [ERROR] Python not found in PATH.
    echo         Install Python and add it to PATH, then retry.
    echo         Or run manually: python train_tide_complete.py
    pause
    exit /b 1
)

echo Running: python train_tide_complete.py
echo Logs   : logs\stats.jsonl
echo Ctrl+C : stop training
echo ========================================
echo.

python train_tide_complete.py
set EXITCODE=%ERRORLEVEL%

echo.
if "%EXITCODE%"=="0" (
    echo ========================================
    echo   Training finished.
    echo ========================================
) else (
    echo ========================================
    echo   Training exited with code %EXITCODE%
    echo ========================================
)
echo.
pause
