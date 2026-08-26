@echo off
chcp 65001 >nul
python "%~dp0export_to_json.py" %*
pause