"""
训练进度监控（2026-09-29 重写：stats.jsonl 口径）。

旧版解析控制台文本 + 找 train.log/checkpoint_* 目录——两者都不存在（现行产物是
stats.jsonl + best/ + last/），实际永远显示不出任何东西。现直接 tail stats.jsonl：
每个 update 一行训练指标，评估另起一行（"eval": true，win_rate_vs_theme 等）。

用法：
  python monitor_training.py               # 盯最新 run，5s 刷新
  python monitor_training.py --run <name>  # 指定 run 目录名
  python monitor_training.py --refresh 2
  python monitor_training.py --summary     # 打印一次快照即退出（不循环刷屏）
"""

import argparse
import json
import time
from pathlib import Path


def find_latest_run(log_dir="logs"):
    log_path = Path(log_dir)
    if not log_path.exists():
        return None
    runs = [d for d in log_path.iterdir() if d.is_dir()]
    return max(runs, key=lambda d: d.stat().st_mtime) if runs else None


def load_stats(run_dir: Path):
    """读全量 stats.jsonl → (train_rows, eval_rows)。坏行跳过（写入中途的半行）。"""
    train_rows, eval_rows = [], []
    stats_file = run_dir / "stats.jsonl"
    if not stats_file.exists():
        return train_rows, eval_rows
    for line in stats_file.read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if not line:
            continue
        try:
            row = json.loads(line)
        except json.JSONDecodeError:
            continue
        (eval_rows if row.get("eval") else train_rows).append(row)
    return train_rows, eval_rows


def fmt_duration(seconds: float) -> str:
    h, rem = divmod(int(seconds), 3600)
    m, s = divmod(rem, 60)
    return f"{h:02d}:{m:02d}:{s:02d}"


def spark(values, width=40, lo=None, hi=None):
    """迷你趋势条（unicode 块高程）。"""
    if not values:
        return ""
    lo = min(values) if lo is None else lo
    hi = max(values) if hi is None else hi
    span = (hi - lo) or 1.0
    blocks = " ▁▂▃▄▅▆▇█"
    scaled = [int((v - lo) / span * (len(blocks) - 1)) for v in values]
    step = max(1, len(scaled) // width)
    return "".join(blocks[scaled[i]] for i in range(0, len(scaled), step))


def render(run_dir: Path):
    train_rows, eval_rows = load_stats(run_dir)
    lines = ["=" * 78, f"Tide Training Monitor — {run_dir.name}", "=" * 78]

    if not train_rows:
        lines.append("（暂无训练行——等首个 update 完成后刷新）")
        return "\n".join(lines)

    last = train_rows[-1]
    total = 2_000_000
    cfg = {}
    cfg_file = run_dir / "config.json"
    if cfg_file.exists():
        try:
            cfg = json.loads(cfg_file.read_text(encoding="utf-8"))
            total = int(cfg.get("total_timesteps", total))
        except json.JSONDecodeError:
            pass

    n = min(len(train_rows), 100)
    recent = train_rows[-n:]
    lines.append(
        f"Update {last.get('update', '?')} | Step {last.get('step', 0):,}/{total:,} "
        f"({last.get('step', 0) / total * 100:.1f}%) | SPS {last.get('sps', 0):.0f} "
        f"| 已跑 {fmt_duration(last.get('elapsed', 0))}"
        + (f" | ETA {fmt_duration(max(0.0, (total - last.get('step', 0)) / max(last.get('sps', 1), 1)))}"
           if last.get("sps") else "")
    )
    ent = recent[-1].get("entropy")
    lines.append(
        f"损失  pg {recent[-1].get('pg_loss', 0):+.4f}  vf {recent[-1].get('vf_loss', 0):.4f}  "
        f"熵 {ent:.3f}" if ent is not None else ""
    )
    kl = recent[-1].get("approx_kl")
    if kl is not None:
        lines.append(
            f"PPO   approx_kl {kl:.4f}  clipfrac {recent[-1].get('clipfrac', 0):.1%} "
            f"（KL 持续 >0.02 → 降 update_epochs 或学习率）"
        )
    lines.append(
        f"对局  ep_ret {sum(r.get('ep_ret', 0) for r in recent) / n:+.2f}  "
        f"ep_len {sum(r.get('ep_len', 0) for r in recent) / n:.1f}  "
        f"完结 {last.get('episodes', 0)} 局（超时累计 {last.get('timeouts', 0)}）"
    )
    lines.append("熵趋势   " + spark([r.get("entropy", 0) for r in train_rows]))
    lines.append("回报趋势 " + spark([r.get("ep_ret", 0) for r in train_rows]))

    if eval_rows:
        lines.append("-" * 78)
        wins = [r.get("win_rate_vs_theme", 0) for r in eval_rows]
        best = max(wins)
        lines.append(f"评估（vs 脚本主题，共 {len(eval_rows)} 次）：最新 {wins[-1]:.1%} | best {best:.1%}")
        lines.append("胜率趋势 " + spark(wins, lo=0.0, hi=1.0))
        for r in eval_rows[-5:]:
            themes = " ".join(f"{k} {v:.0%}" for k, v in (r.get("theme_win_rates") or {}).items())
            sp = r.get("sp_win_rate")
            lines.append(
                f"  step {r.get('step', 0):>9,}: vs主题 {r.get('win_rate_vs_theme', 0):.1%} "
                + (f"| 自弈先手 {sp:.1%} " if sp is not None else "") + (f"| {themes}" if themes else "")
            )

    lines.append("=" * 78)
    return "\n".join(lines)


def main():
    ap = argparse.ArgumentParser(description="监控 Tide 训练（stats.jsonl 口径）")
    ap.add_argument("--run", type=str, help="run 目录名（默认 logs/ 下最新）")
    ap.add_argument("--refresh", type=int, default=5, help="刷新间隔秒（默认 5）")
    ap.add_argument("--summary", action="store_true", help="打印一次快照后退出")
    args = ap.parse_args()

    run_dir = (Path("logs") / args.run) if args.run else find_latest_run()
    if run_dir is None or not run_dir.exists():
        print("logs/ 下没有训练 run")
        return

    if args.summary:
        print(render(run_dir))
        return

    print(f"监控 {run_dir.resolve()}（Ctrl+C 退出）")
    try:
        while True:
            print("\033[2J\033[H" + render(run_dir), flush=True)  # ANSI 清屏归位（Windows Terminal/PS 7 支持）
            time.sleep(args.refresh)
    except KeyboardInterrupt:
        print("\n监控结束。")


if __name__ == "__main__":
    main()
