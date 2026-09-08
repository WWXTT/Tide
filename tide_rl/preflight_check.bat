@echo off
REM Tide AI 训练前置条件检查脚本

echo ========================================
echo Tide AI Training Pre-Flight Check
echo ========================================
echo.

set "PASSED=0"
set "FAILED=0"

REM 1. Python 检查
echo [1/7] Checking Python...
where python >nul 2>nul
if %ERRORLEVEL% EQU 0 (
    python --version
    echo   [OK] Python found
    set /a PASSED+=1
) else (
    echo   [FAIL] Python not found
    set /a FAILED+=1
)
echo.

REM 2. Unity 检查
echo [2/7] Checking Unity...
set "UNITY_PATH=C:\Program Files\Unity\Hub\Editor\2022.3.44f1c1\Editor\Unity.exe"
if exist "%UNITY_PATH%" (
    echo   [OK] Unity found at %UNITY_PATH%
    set /a PASSED+=1
) else (
    echo   [FAIL] Unity not found at default path
    echo   Please set UNITY_PATH environment variable
    set /a FAILED+=1
)
echo.

REM 3. ygo-agent 检查
echo [3/7] Checking ygo-agent...
if exist "%~dp0..\ygo-agent-main" (
    echo   [OK] ygo-agent-main found
    set /a PASSED+=1
) else (
    echo   [FAIL] ygo-agent-main not found in project root
    set /a FAILED+=1
)
echo.

REM 4. Python 依赖检查
echo [4/7] Checking Python dependencies...
python -c "import jax, flax, optax, gymnasium" 2>nul
if %ERRORLEVEL% EQU 0 (
    echo   [OK] All dependencies installed
    set /a PASSED+=1
) else (
    echo   [WARN] Some dependencies missing
    echo   Run: pip install jax[cpu] flax optax gymnasium numpy
    set /a FAILED+=1
)
echo.

REM 5. C# 编译检查
echo [5/7] Checking C# compilation...
cd /d "%~dp0.."
dotnet build Assembly-CSharp.csproj --no-restore >nul 2>nul
if %ERRORLEVEL% EQU 0 (
    echo   [OK] C# compilation successful
    set /a PASSED+=1
) else (
    echo   [WARN] C# compilation has warnings/errors
    echo   Run: dotnet build Assembly-CSharp.csproj
    set /a FAILED+=1
)
echo.

REM 6. Unity 编辑器检查
echo [6/7] Checking Unity Editor status...
tasklist /FI "IMAGENAME eq Unity.exe" 2>NUL | find /I /N "Unity.exe">NUL
if %ERRORLEVEL% EQU 0 (
    echo   [WARN] Unity Editor is running
    echo   Please close Unity Editor to avoid UnityLockfile conflict
    set /a FAILED+=1
) else (
    echo   [OK] Unity Editor not running
    set /a PASSED+=1
)
echo.

REM 7. 磁盘空间检查
echo [7/7] Checking disk space...
for /f "tokens=3" %%a in ('dir /-c ^| find "bytes free"') do set FREE=%%a
if %FREE% GTR 1000000000 (
    echo   [OK] Sufficient disk space (^>1GB free)
    set /a PASSED+=1
) else (
    echo   [WARN] Low disk space (^<1GB free)
    set /a FAILED+=1
)
echo.

REM 总结
echo ========================================
echo Pre-Flight Check Summary
echo ========================================
echo   Passed: %PASSED%/7
echo   Failed: %FAILED%/7
echo.

if %FAILED% EQU 0 (
    echo [SUCCESS] All checks passed!
    echo Ready to start training.
    echo.
    echo Run: .\start_training.ps1
    echo.
) else (
    echo [WARNING] Some checks failed
    echo Please fix the issues above before training.
    echo.
)

pause
