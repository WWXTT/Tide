"""
动作空间审计（v2 布局验收利器，2026-09-30 C 期）。

用途：Unity 侧 U1-U7 落地后，跑随机策略自对弈，验证——
  1. 布局握手：reset 断言 layoutVersion/dims（tide_env_tcp._assert_layout）；
  2. 新动作类型行确实出现在动作表（HeroSkill/VoluntaryTrigger/PassPriority/Respond*/Guard）；
  3. 无 MAX_ACTIONS=512 截断事件（服务端上报 nActions > 512 = 模型看不到尾部行）；
  4. 响应窗口决策点确实停靠（global_[47]==1 的步占比）；
  5. 无死循环停靠（单局步数受 max_steps 约束自然终局/超时分布健康）；
  6. 逐目标展开生效（带 targetKind=1 的行出现率）。

用法（先在 Unity 编辑器开 Tools > AI > 启动训练服务器 TCP 9999）：
  python audit_action_space.py                  # 默认 3 局自对弈
  python audit_action_space.py --episodes 5 --seed 7
  python audit_action_space.py --json out.json  # 结果落盘（验收证据）

判读：任一"新动作类型"行出现率恒 0 → 对应枚举没接上（回查 U2/U4）；
截断事件 > 0 → MAX_ACTIONS 需上调或枚举有叉积失控；响应步占比 0 → U4 响应循环没停靠。
"""

import argparse
import json
from collections import Counter

import numpy as np

from tide_env_tcp import TideEnvTcp
from tide_features import (
    ACTION_F_VALID, ACTION_F_TYPE, ACTION_F_TARGET_KIND,
    ACTION_TYPES, NEW_ACTION_TYPES,
    GLOBAL_F_DECISION_CONTEXT,
)


def run_episode(env, rng, max_steps):
    """随机策略跑一局，返回该局的统计 dict。均匀采样合法行（按行数加权——
    分布反映「动作表面貌」而非策略偏好，与验收目的一致）。"""
    obs, info = env.reset()
    ep = {
        "steps": 0,
        "row_type_counts": Counter(),      # 所有停靠点的合法行类型分布（按行计）
        "point_type_avail": Counter(),     # 每个决策点"出现过该类型"的次数（按点计）
        "chosen_type_counts": Counter(),   # 随机选中行的类型分布
        "targeted_rows": 0, "total_rows": 0,
        "response_steps": 0,
        "max_n_actions": 0,
        "truncation_events": 0,
        "turn_max": 0,
        "done_reason": None,
        "winner": None,
    }
    done = False
    while not done and ep["steps"] < max_steps:
        valid = ~obs["a_mask"]
        n_valid = int(valid.sum())
        n_server = env.last_n_actions
        ep["max_n_actions"] = max(ep["max_n_actions"], n_server)
        if n_server > len(valid):
            ep["truncation_events"] += 1

        rows = obs["actions_"][valid]  # (n_valid, 8)
        types = rows[:, ACTION_F_TYPE].astype(int)
        ep["total_rows"] += n_valid
        for t in np.unique(types):
            ep["point_type_avail"][int(t)] += 1
        for t in types:
            ep["row_type_counts"][int(t)] += 1
        ep["targeted_rows"] += int((rows[:, ACTION_F_TARGET_KIND] == 1).sum())

        if int(obs["global_"][GLOBAL_F_DECISION_CONTEXT]) == 1:
            ep["response_steps"] += 1

        act = int(rng.integers(0, n_valid))
        chosen_t = int(types[act])
        ep["chosen_type_counts"][chosen_t] += 1

        obs, reward, done, truncated, info = env.step(act)
        done = done or truncated
        ep["steps"] += 1
        ep["turn_max"] = max(ep["turn_max"], int(info.get("turn", 0) or 0))
        ep["winner"] = info.get("winner", None)
        if info.get("timeout"):
            ep["done_reason"] = "timeout"
        elif done:
            ep["done_reason"] = str(info.get("reason", "gameover"))

    return ep


def main():
    ap = argparse.ArgumentParser(description="Tide v2 动作空间审计（随机策略自对弈）")
    ap.add_argument("--host", default="localhost")
    ap.add_argument("--port", type=int, default=9999)
    ap.add_argument("--episodes", type=int, default=3)
    ap.add_argument("--max-steps", type=int, default=2000)
    ap.add_argument("--seed", type=int, default=42)
    ap.add_argument("--opponent", default="selfplay",
                    help="selfplay（默认，响应窗口完整）/ simpleai（模型回合口径）")
    ap.add_argument("--json", type=str, default=None, help="结果 JSON 落盘路径")
    args = ap.parse_args()

    env = TideEnvTcp(host=args.host, port=args.port, max_steps=args.max_steps,
                     opponent=args.opponent)
    rng = np.random.default_rng(args.seed)

    eps = []
    try:
        for i in range(args.episodes):
            print(f"[{i+1}/{args.episodes}] 跑随机自对弈……", flush=True)
            ep = run_episode(env, rng, args.max_steps)
            eps.append(ep)
            print(f"  steps={ep['steps']} turns={ep['turn_max']} "
                  f"maxN={ep['max_n_actions']} 截断={ep['truncation_events']} "
                  f"done={ep['done_reason']} winner={ep['winner']}", flush=True)
    finally:
        env.close()

    # ==================== 汇总 ====================
    total_steps = sum(e["steps"] for e in eps)
    row_counts = Counter()
    point_avail = Counter()
    chosen = Counter()
    targeted = sum(e["targeted_rows"] for e in eps)
    total_rows = sum(e["total_rows"] for e in eps)
    resp = sum(e["response_steps"] for e in eps)
    max_n = max(e["max_n_actions"] for e in eps)
    trunc = sum(e["truncation_events"] for e in eps)
    for e in eps:
        row_counts.update(e["row_type_counts"])
        point_avail.update(e["point_type_avail"])
        chosen.update(e["chosen_type_counts"])
    total_points = total_steps

    def pct(x, d):
        return f"{(x / d * 100):.1f}%" if d else "n/a"

    lines = []
    lines.append("=" * 70)
    lines.append(f"Tide v2 动作空间审计（{args.episodes} 局随机自对弈，opponent={args.opponent}）")
    lines.append("=" * 70)
    lines.append(f"布局握手     : 通过（reset 断言 layoutVersion=2 + dims，未抛错即过）")
    lines.append(f"总决策步     : {total_steps}（响应窗口步 {resp} = {pct(resp, total_steps)}）")
    lines.append(f"动作表规模   : 单点最大 {max_n} 行（MAX_ACTIONS=512）｜截断事件 {trunc}")
    lines.append(f"带目标行占比 : {pct(targeted, total_rows)}（targetKind=1 行 / 全部行）")
    lines.append("")
    lines.append(f"{'类型':<18}{'行数':>8}{'行占比':>9}{'出现点':>8}{'点覆盖率':>10}{'随机选中':>9}")
    for t in sorted(set(list(ACTION_TYPES.keys()) + list(row_counts.keys()))):
        name = ACTION_TYPES.get(t, f"type{t}")
        rc = row_counts.get(t, 0)
        pa = point_avail.get(t, 0)
        ch = chosen.get(t, 0)
        mark = " ◀ 新" if t in NEW_ACTION_TYPES else ""
        lines.append(f"{name:<18}{rc:>8}{pct(rc, total_rows):>9}{pa:>8}{pct(pa, total_points):>10}{ch:>9}{mark}")
    lines.append("=" * 70)

    verdict = []
    if trunc > 0:
        verdict.append(f"[!] 截断事件 {trunc} 次：MAX_ACTIONS=512 不够或枚举叉积失控")
    for t in NEW_ACTION_TYPES:
        name = ACTION_TYPES.get(t, f"type{t}")
        if point_avail.get(t, 0) == 0:
            verdict.append(f"[!] {name} 行从未出现——对应枚举/决策点未接上（回查 U2/U4）")
    if args.opponent == "selfplay" and resp == 0:
        verdict.append("[!] 响应窗口步为 0——U4 响应循环未停靠（vs simpleai 口径下为预期）")
    if total_rows and targeted == 0:
        verdict.append("[!] 无任何带目标行——逐目标展开未生效（U2）")
    if not verdict:
        verdict.append("[✓] 全部检查通过")
    lines.extend(verdict)

    report = "\n".join(lines)
    print("\n" + report)

    if args.json:
        payload = {
            "episodes": args.episodes,
            "opponent": args.opponent,
            "total_steps": total_steps,
            "response_steps": resp,
            "max_n_actions": max_n,
            "truncation_events": trunc,
            "targeted_row_frac": (targeted / total_rows) if total_rows else None,
            "row_type_counts": {ACTION_TYPES.get(k, k): v for k, v in sorted(row_counts.items())},
            "point_type_avail": {ACTION_TYPES.get(k, k): v for k, v in sorted(point_avail.items())},
            "chosen_type_counts": {ACTION_TYPES.get(k, k): v for k, v in sorted(chosen.items())},
            "per_episode": [
                {"steps": e["steps"], "turns": e["turn_max"], "max_n": e["max_n_actions"],
                 "truncations": e["truncation_events"], "done": e["done_reason"],
                 "winner": e["winner"], "response_steps": e["response_steps"]}
                for e in eps
            ],
            "verdict": verdict,
        }
        with open(args.json, "w", encoding="utf-8") as f:
            json.dump(payload, f, ensure_ascii=False, indent=2)
        print(f"[✓] 结果已落盘: {args.json}")


if __name__ == "__main__":
    main()
