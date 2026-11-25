@echo off
rem ============================================================
rem start_teacher_run.cmd - specialist teacher run launcher
rem (Stage 1/2, framework doc section 13.1)
rem
rem usage:
rem   start_teacher_run.cmd <deck> [total_timesteps] [--dry-run]
rem     deck in red / green / blue
rem     total_timesteps default: red/blue=600000, green=800000
rem     --dry-run: only print resolved command line + env vars,
rem                do NOT start anything.
rem
rem fixed knobs: eval_interval=50000, eval_episodes=288,
rem   save_interval=20000, exp_name=tide_ppo_simpleai_<deck>_teacher,
rem   --opponent simpleai --model-theme <deck> --eval-model-theme <deck>
rem   --eval-theme "" (three script opponents)
rem   --init-params logs\tide_ppo_simpleai__42__1795540922\last\params.msgpack
rem   (in-copy relative path = shared v21 last, warm start per section 7.1)
rem
rem non-dry-run launch = house pattern (hidden powershell, detached
rem from console, output to logs\teacher_<deck>_console.log). Never
rem stop it with Ctrl+C - use the usual stop procedure instead.
rem ASCII-only body on purpose (GBK codepage safety).
rem ============================================================
setlocal enabledelayedexpansion
cd /d "%~dp0"

set "DECK="
set "STEPS=0"
set "DRYRUN=0"

rem ---- parse args: <deck> [steps] [--dry-run] (dry-run may sit anywhere) ----
:parse
if "%~1"=="" goto parsed
if /i "%~1"=="--dry-run" (set "DRYRUN=1") else if "!DECK!"=="" (set "DECK=%~1") else set "STEPS=%~1"
shift
goto parse
:parsed

if not defined DECK goto usage
if /i "%DECK%"=="red"   (if %STEPS%==0 set "STEPS=600000") else if /i "%DECK%"=="blue" (if %STEPS%==0 set "STEPS=600000") else if /i "%DECK%"=="green" (if %STEPS%==0 set "STEPS=800000") else goto usage

set "PY=C:\Users\Administrator\AppData\Local\Programs\Python\Python314\python.exe"
if not exist "%PY%" set "PY=python"

set "EXP=tide_ppo_simpleai_%DECK%_teacher"
set "INIT=logs\tide_ppo_simpleai__42__1795540922\last\params.msgpack"
set "LOG=logs\teacher_%DECK%_console.log"

rem ---- env (three-copy parallel: mem fraction per section 13.1) ----
set "TIDE_TRAIN_OVERRIDE={"exp_name":"%EXP%","total_timesteps":%STEPS%,"eval_interval":50000,"eval_episodes":288,"save_interval":20000}"
set "XLA_PYTHON_CLIENT_PREALLOCATE=false"
set "XLA_PYTHON_CLIENT_MEM_FRACTION=0.33"

set "CMDLINE=%PY% train_tide_complete.py --opponent simpleai --model-theme %DECK% --eval-model-theme %DECK% --eval-theme "" --init-params %INIT%"

echo ============================================================
echo  start_teacher_run - specialist teacher launcher
echo  deck            : %DECK%
echo  total_timesteps : %STEPS%
echo  exp_name        : %EXP%
echo  workdir         : %CD%
echo  init-params     : %INIT%
echo  console log     : %LOG%
echo ------------------------------------------------------------
echo  command line:
echo    %CMDLINE%
echo  environment variables:
echo    TIDE_TRAIN_OVERRIDE=%TIDE_TRAIN_OVERRIDE%
echo    XLA_PYTHON_CLIENT_PREALLOCATE=%XLA_PYTHON_CLIENT_PREALLOCATE%
echo    XLA_PYTHON_CLIENT_MEM_FRACTION=%XLA_PYTHON_CLIENT_MEM_FRACTION%
echo ============================================================

if "%DRYRUN%"=="1" (
  echo [dry-run] nothing started.
  endlocal
  exit /b 0
)

if not exist "%INIT%" (
  echo [!] init-params not found: %INIT%
  echo     run this launcher from a training copy whose logs\ holds the shared
  echo     v21 last checkpoint, or edit INIT in this file.
  endlocal
  exit /b 1
)

start "" powershell -NoProfile -WindowStyle Hidden -Command "& {& %PY% train_tide_complete.py --opponent simpleai --model-theme %DECK% --eval-model-theme %DECK% --eval-theme '' --init-params %INIT% *>> %LOG%}"
echo started hidden - deck=%DECK% steps=%STEPS% log=%LOG%
echo progress: monitor via monitor_training.py / stats.jsonl
endlocal
exit /b 0

:usage
echo usage: start_teacher_run.cmd ^<red^|green^|blue^> [total_timesteps] [--dry-run]
echo   defaults: red/blue 600000, green 800000
endlocal
exit /b 2
