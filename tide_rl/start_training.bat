@echo off
title Tide AI Training (train copy)
echo ========================================
echo   Tide AI Training Launcher
echo   (2026-11-17 起训练走临时工程 C:\UnityProject\Tide_train,
echo    主工程不再承载训练;隐藏窗口启动,防误触 Ctrl+C)
echo   进度: C:\UnityProject\Tide_train\tide_rl\logs\relaunch_console.log
echo ========================================
echo.
start "" C:\UnityProject\Tide_train\relaunch_training.cmd
echo 已交给副本启动器(隐藏窗口)。本窗口可关闭。
pause
