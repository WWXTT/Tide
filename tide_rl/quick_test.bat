@echo off
REM 快速测试脚本 - 验证环境配置

echo ========================================
echo Tide AI Quick Test
echo ========================================
echo.

REM 设置环境变量
set "UNITY_PATH=C:\Program Files\Unity\Hub\Editor\6000.5.8f1\Editor\Unity.exe"
set "TIDE_PROJECT_PATH=%~dp0.."
set "PYTHONPATH=%~dp0..\ygo-agent-main;%PYTHONPATH%"

echo Environment:
echo   UNITY_PATH: %UNITY_PATH%
echo   PROJECT: %TIDE_PROJECT_PATH%
echo   PYTHONPATH: %PYTHONPATH%
echo.

echo [1/5] Testing Python imports...
python -c "from tide_rl import TideEnv, create_tide_agent; print('  [OK] Tide RL imports')"
if %ERRORLEVEL% NEQ 0 (
    echo   [FAIL] Import error
    pause
    exit /b 1
)

python -c "from ygoai.rl.jax import truncated_gae; print('  [OK] ygo-agent imports')"
if %ERRORLEVEL% NEQ 0 (
    echo   [FAIL] ygo-agent import error
    echo   Check PYTHONPATH setting
    pause
    exit /b 1
)
echo.

echo [2/5] Testing model creation...
python -c "from tide_rl import create_tide_agent, sample_input, init_rstate; import jax; agent = create_tide_agent(); rstate = init_rstate(); obs = sample_input(); obs = jax.tree_map(lambda x: x[None, ...] if x is not None else None, obs); rng = jax.random.PRNGKey(0); params = agent.init(rng, rstate, obs); print('  [OK] Model created')"
if %ERRORLEVEL% NEQ 0 (
    echo   [FAIL] Model creation failed
    pause
    exit /b 1
)
echo.

echo [3/5] Testing Unity path...
if exist "%UNITY_PATH%" (
    echo   [OK] Unity found
) else (
    echo   [WARN] Unity not found at default path
    echo   Please edit this script and set correct UNITY_PATH
)
echo.

echo [4/5] Testing C# compilation...
cd /d "%TIDE_PROJECT_PATH%"
dotnet build Assembly-CSharp.csproj --no-restore >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo   [OK] C# builds successfully
) else (
    echo   [WARN] C# build has errors
    echo   Run: dotnet build Assembly-CSharp.csproj
)
echo.

echo [5/5] Checking Unity Editor status...
tasklist /FI "IMAGENAME eq Unity.exe" 2>NUL | find /I /N "Unity.exe">NUL
if %ERRORLEVEL% EQU 0 (
    echo   [WARN] Unity Editor is running
    echo   Recommendation: Close Unity Editor before training
) else (
    echo   [OK] Unity Editor not running
)
echo.

echo ========================================
echo Quick Test Complete
echo ========================================
echo.
echo If all checks passed, you can start training with:
echo   start_training.bat
echo.

pause
