"""
训练进度监控脚本。

实时监控训练日志，提供可视化进度和关键指标。
"""

import time
import json
from pathlib import Path
from collections import deque
import sys


def find_latest_run(log_dir="logs"):
    """找到最新的训练运行目录。"""
    log_path = Path(log_dir)
    if not log_path.exists():
        return None

    runs = sorted(log_path.glob("tide_ppo_*"), key=lambda x: x.stat().st_mtime, reverse=True)
    return runs[0] if runs else None


def parse_log_line(line):
    """解析训练日志行。"""
    if "Update" not in line or "Step" not in line:
        return None

    try:
        parts = line.split("|")
        info = {}

        for part in parts:
            part = part.strip()
            if "Update" in part:
                update_str = part.split()[1].split("/")[0]
                info["update"] = int(update_str)
            elif "Step" in part:
                info["step"] = int(part.split()[1])
            elif "Loss" in part and "PG" not in part:
                info["loss"] = float(part.split()[1])
            elif "PG" in part:
                info["pg_loss"] = float(part.split()[1])
            elif "VF" in part:
                info["vf_loss"] = float(part.split()[1])
            elif "Ent" in part:
                info["entropy"] = float(part.split()[1])
            elif "EpRet" in part:
                info["ep_return"] = float(part.split()[1])
            elif "EpLen" in part:
                info["ep_length"] = float(part.split()[1])
            elif "SPS" in part:
                info["sps"] = float(part.split()[1])

        return info if info else None
    except:
        return None


def parse_eval_result(lines):
    """解析评估结果。"""
    result = {}
    for line in lines:
        if "Win Rate:" in line:
            try:
                win_rate_str = line.split("Win Rate:")[1].split("%")[0].strip()
                result["win_rate"] = float(win_rate_str) / 100
            except:
                pass
        elif "Avg Episode Length:" in line:
            try:
                length_str = line.split("Avg Episode Length:")[1].strip()
                result["avg_length"] = float(length_str)
            except:
                pass
    return result if result else None


def format_duration(seconds):
    """格式化时长。"""
    hours = int(seconds // 3600)
    mins = int((seconds % 3600) // 60)
    secs = int(seconds % 60)
    return f"{hours:02d}:{mins:02d}:{secs:02d}"


def monitor_training(log_file=None, refresh_interval=5):
    """
    监控训练进度。

    Args:
        log_file: 日志文件路径（None = 自动查找最新）
        refresh_interval: 刷新间隔（秒）
    """
    if log_file is None:
        run_dir = find_latest_run()
        if run_dir is None:
            print("Error: No training run found in logs/")
            return
        log_file = run_dir / "train.log"
        if not log_file.exists():
            print(f"Warning: Log file not found at {log_file}")
            print("Training output will be monitored from stdout instead.")
            log_file = None

    print(f"\n{'='*80}")
    print(f"Tide AI Training Monitor")
    print(f"{'='*80}\n")

    if log_file:
        print(f"Monitoring: {log_file}\n")
    else:
        print(f"Monitoring: stdout (real-time)\n")

    training_history = deque(maxlen=100)
    eval_history = []

    start_time = time.time()
    last_update = 0
    last_eval_win_rate = 0.0
    best_win_rate = 0.0

    try:
        if log_file:
            with open(log_file, "r") as f:
                # 读取已有内容
                lines = f.readlines()
                for line in lines:
                    info = parse_log_line(line)
                    if info:
                        training_history.append(info)

                # 实时监控新内容
                while True:
                    line = f.readline()
                    if line:
                        info = parse_log_line(line)
                        if info:
                            training_history.append(info)
                            last_update = info.get("update", last_update)

                        # 检查评估结果
                        if "Win Rate:" in line:
                            eval_result = parse_eval_result([line])
                            if eval_result:
                                eval_history.append(eval_result)
                                last_eval_win_rate = eval_result["win_rate"]
                                if last_eval_win_rate > best_win_rate:
                                    best_win_rate = last_eval_win_rate

                        # 实时显示进度
                        display_progress(
                            training_history, eval_history,
                            start_time, last_update,
                            last_eval_win_rate, best_win_rate
                        )
                    else:
                        time.sleep(refresh_interval)
        else:
            # 实时监控 stdout（适用于训练脚本直接运行时）
            print("Press Ctrl+C to stop monitoring.\n")
            while True:
                time.sleep(refresh_interval)

    except KeyboardInterrupt:
        print("\n\nMonitoring stopped.")
    except Exception as e:
        print(f"\nError: {e}")


def display_progress(history, eval_history, start_time, last_update, last_eval_win_rate, best_win_rate):
    """显示训练进度（清屏刷新）。"""
    import os

    # 清屏（Windows: cls, Linux/Mac: clear）
    os.system('cls' if os.name == 'nt' else 'clear')

    elapsed = time.time() - start_time

    print(f"\n{'='*80}")
    print(f"Tide AI Training Monitor - {format_duration(elapsed)} elapsed")
    print(f"{'='*80}\n")

    if not history:
        print("Waiting for training data...\n")
        return

    recent = list(history)[-10:]

    # 最新指标
    latest = recent[-1]
    print(f"Latest Metrics (Update {latest.get('update', 0)}, Step {latest.get('step', 0)}):")
    print(f"  Loss:         {latest.get('loss', 0):.4f}")
    print(f"  PG Loss:      {latest.get('pg_loss', 0):.4f}")
    print(f"  Value Loss:   {latest.get('vf_loss', 0):.4f}")
    print(f"  Entropy:      {latest.get('entropy', 0):.4f}")
    print(f"  Ep Return:    {latest.get('ep_return', 0):.2f}")
    print(f"  Ep Length:    {latest.get('ep_length', 0):.1f}")
    print(f"  SPS:          {latest.get('sps', 0):.0f}\n")

    # 平均指标（最近 10 次更新）
    if len(recent) > 1:
        avg_loss = sum(x.get('loss', 0) for x in recent) / len(recent)
        avg_return = sum(x.get('ep_return', 0) for x in recent) / len(recent)
        avg_length = sum(x.get('ep_length', 0) for x in recent) / len(recent)

        print(f"Recent Average (last {len(recent)} updates):")
        print(f"  Loss:         {avg_loss:.4f}")
        print(f"  Ep Return:    {avg_return:.2f}")
        print(f"  Ep Length:    {avg_length:.1f}\n")

    # 评估结果
    if eval_history:
        print(f"Evaluation vs SimpleAI:")
        print(f"  Latest Win Rate:  {last_eval_win_rate*100:.1f}%")
        print(f"  Best Win Rate:    {best_win_rate*100:.1f}%")
        print(f"  Evaluations:      {len(eval_history)}\n")

        # 显示最近 5 次评估
        print(f"Recent Evaluations:")
        for i, result in enumerate(eval_history[-5:], 1):
            wr = result.get('win_rate', 0) * 100
            al = result.get('avg_length', 0)
            print(f"  {i}. Win Rate: {wr:5.1f}% | Avg Length: {al:5.1f}")
        print()

    # 进度条（假设 200 万步总目标）
    total_steps = 2_000_000
    current_step = latest.get('step', 0)
    progress = min(current_step / total_steps, 1.0)
    bar_length = 50
    filled = int(bar_length * progress)
    bar = '█' * filled + '░' * (bar_length - filled)

    print(f"Progress:")
    print(f"  [{bar}] {progress*100:.1f}% ({current_step:,}/{total_steps:,} steps)")

    # ETA
    if current_step > 0 and elapsed > 0:
        sps = current_step / elapsed
        remaining_steps = total_steps - current_step
        eta_seconds = remaining_steps / sps if sps > 0 else 0
        print(f"  ETA: {format_duration(eta_seconds)}")

    print(f"\n{'='*80}")
    print(f"Press Ctrl+C to stop monitoring")
    print(f"{'='*80}\n")


def show_summary(run_dir):
    """显示训练运行总结。"""
    config_file = run_dir / "config.json"
    if not config_file.exists():
        print(f"Error: Config file not found in {run_dir}")
        return

    with open(config_file, "r") as f:
        config = json.load(f)

    print(f"\n{'='*80}")
    print(f"Training Run Summary: {run_dir.name}")
    print(f"{'='*80}\n")

    print(f"Configuration:")
    print(f"  Seed:             {config.get('seed')}")
    print(f"  Learning Rate:    {config.get('learning_rate')}")
    print(f"  Envs:             {config.get('num_envs')}")
    print(f"  Steps per Rollout: {config.get('num_steps')}")
    print(f"  Reward Lambda:    {config.get('reward_lambda')}")
    print(f"  Total Timesteps:  {config.get('total_timesteps'):,}")
    print(f"  Target Win Rate:  {config.get('target_win_rate')*100:.0f}%\n")

    # 检查是否有最优模型
    best_model = run_dir / "best_model" / "params.msgpack"
    if best_model.exists():
        print(f"✓ Best model saved")
        print(f"  Size: {best_model.stat().st_size / 1024 / 1024:.2f} MB\n")

    # 检查 checkpoints
    checkpoints = sorted(run_dir.glob("checkpoint_*"))
    if checkpoints:
        print(f"Checkpoints: {len(checkpoints)}")
        for cp in checkpoints:
            step = cp.name.split("_")[1]
            print(f"  - Step {step}")

    print(f"\n{'='*80}\n")


if __name__ == "__main__":
    import argparse

    parser = argparse.ArgumentParser(description="Monitor Tide AI training progress")
    parser.add_argument("--log-file", type=str, help="Path to log file (default: auto-detect latest)")
    parser.add_argument("--refresh", type=int, default=5, help="Refresh interval in seconds (default: 5)")
    parser.add_argument("--summary", action="store_true", help="Show summary of latest run and exit")

    args = parser.parse_args()

    if args.summary:
        run_dir = find_latest_run()
        if run_dir:
            show_summary(run_dir)
        else:
            print("Error: No training run found in logs/")
    else:
        log_file = Path(args.log_file) if args.log_file else None
        monitor_training(log_file, args.refresh)
