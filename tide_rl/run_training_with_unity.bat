@echo off
REM 设置 Unity 和项目路径
set UNITY_PATH=C:\Program Files\Unity\Hub\Editor\6000.5.8f1\Editor\Unity.exe
set TIDE_PROJECT_PATH=E:\UnityProject\Tide

echo Unity Path: %UNITY_PATH%
echo Project Path: %TIDE_PROJECT_PATH%
echo.
echo Starting Tide RL Training...
echo.

python train_tide_complete.py

echo.
echo Training finished or crashed.
pause
