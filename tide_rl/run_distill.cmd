@echo off
rem ============================================================
rem run_distill.cmd - distill student CLI launcher (collect / train / verify-shard)
rem
rem Why this wrapper (2026-11-27):
rem   the main repo root has NO ygo-agent-main (env fact 2026-11-20); only the
rem   training copies have it. distill_student.py -> train_tide_complete -> ygoai
rem   therefore needs ygoai on the path, otherwise: ModuleNotFoundError: ygoai.
rem
rem ygo-agent-main lookup order:
rem   1) %TIDE_YGOAI%
rem   2) <repo root>\ygo-agent-main
rem   3) C:\UnityProject\Tide_train\ygo-agent-main (default training copy)
rem
rem usage (from tide_rl):
rem   run_distill.cmd collect --teachers logs\<green>\teacher_manifest.json,...
rem   run_distill.cmd train --shards logs\<student>\distill\round_1 --epochs 3
rem   run_distill.cmd --help
rem ASCII-only body on purpose (GBK codepage safety), same as start_teacher_run.cmd.
rem ============================================================
setlocal
cd /d "%~dp0"

set "PY=C:\Users\Administrator\AppData\Local\Programs\Python\Python314\python.exe"
if not exist "%PY%" set "PY=python"

set "YGO="
if defined TIDE_YGOAI if exist "%TIDE_YGOAI%\ygoai" set "YGO=%TIDE_YGOAI%"
if not defined YGO if exist "%~dp0..\ygo-agent-main\ygoai" set "YGO=%~dp0..\ygo-agent-main"
if not defined YGO if exist "C:\UnityProject\Tide_train\ygo-agent-main\ygoai" set "YGO=C:\UnityProject\Tide_train\ygo-agent-main"
if not defined YGO (
    echo [run_distill] cannot find ygo-agent-main: main repo has none, training copies do.
    echo                set TIDE_YGOAI=^<copy^>\ygo-agent-main and retry.
    exit /b 1
)

set "PYTHONPATH=%YGO%;%PYTHONPATH%"
set "PYTHONIOENCODING=utf-8"
"%PY%" distill_student.py %*
exit /b %ERRORLEVEL%
