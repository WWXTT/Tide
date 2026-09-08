@echo off
echo Starting Tide RL Training...
echo.

python train_tide_complete.py 2>&1 | tee training.log

echo.
echo Training finished or crashed.
echo Check training.log for details.
pause
