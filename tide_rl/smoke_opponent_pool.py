"""对手池六局冒烟（2026-11-02 C 期验收）：6 局对局 errors=0，确认对手采样没碰坏运行。

覆盖口径（各 2 局，强制指定来源——不走随机混合，保证三来源都被打到）：
1. current   ×2：旧行为（selfplay 同协议）——theme 空串、modelSeat=-1（Unity 原生口径）；
2. themes    ×2：三预组整组轮换（red/green/blue，SimpleAI+AutoMatch）——info.theme=采样
                主题 key、modelSeat ∈ (0,1)、opponentSource=theme:<key>；
3. checkpoints×2：快照池对手（selfplay 协议 + opponentSource=ckpt:init）——壳注入
                info.modelSeat=learner_seat（胜负归属学习者视角）。

策略：随机 + 30% 概率优先 EndTurn（与 TideHeadlessServer.RunSelfTest.PickAction 同款，
保证对局推进）——只验收驱动环与对手池接线，不评棋力。快照池用占位参数树即可
（本冒烟不跑 rollout，opponent_params 只需非 None 触发座次归属路径）。

errors=0 判据：
- 六局全部到 done（Python 超时截断计 TIMEOUT，不算 error 但会列出）；
- Python 侧零异常（协议失败会直接抛）；
- Unity 日志（tide_headless_<port>.log）无 "[TideHeadless] 处理客户端时出错"/Exception。

运行（在临时工程 tide_rl 目录下，首次拉起 Unity batchmode 需 1-3 分钟）：
  python smoke_opponent_pool.py
退出码 0 = 6/6 全过。
"""

import random
import sys
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).parent))

from tide_env import TideEnv
from tide_features import ACTION_F_TYPE, action_valid_mask
from opponent_pool import CheckpointPool, OpponentSampler, OpponentPoolEnv

N_GAMES_PER_SOURCE = 2
MAX_STEPS = 800
END_TURN = 5  # TideActionType.EndTurn（契约 §1）


def pick_action(obs, rng):
    """随机策略（30% 概率优先 EndTurn 保证推进）——与 C# RunSelfTest.PickAction 同口径。"""
    actions = obs["actions_"]
    valid = int(action_valid_mask(actions).sum())
    if valid == 0:
        raise RuntimeError("合法动作数为 0（服务端 nActions=0？驱动环异常）")
    if rng.random() < 0.3:
        for i in range(valid):
            if int(actions[i][ACTION_F_TYPE]) == END_TURN:
                return i
    return rng.randrange(valid)


def run_game(env_wrapper, rng, game_no):
    """跑一局并断言对手池接线口径。返回 (是否通过, 摘要 dict)。"""
    obs, info = env_wrapper.reset()
    src = str(info.get("opponentSource", ""))
    learner_seat = env_wrapper.learner_seat
    reset_theme = str(info.get("theme", "") or "")
    done, steps = False, 0
    while not done and steps < MAX_STEPS:
        obs, reward, done, truncated, info = env_wrapper.step(pick_action(obs, rng))
        steps += 1
        done = done or truncated

    winner = str(info.get("winner", "") or "")
    timeout = bool(info.get("timeout"))
    final_theme = str(info.get("theme", "") or "")
    model_seat = info.get("modelSeat")

    errs = []
    if not src:
        errs.append("info.opponentSource 缺失（壳未标注？）")
    if src.startswith("theme:"):
        key = src.split(":", 1)[1]
        if reset_theme != key:
            errs.append(f"reset theme={reset_theme!r} != 来源 {key!r}")
        if model_seat not in (0, 1):
            errs.append(f"themes 局 modelSeat={model_seat!r} 应为 0/1（Unity 原生口径）")
    elif src.startswith("ckpt:"):
        if final_theme != "":
            errs.append(f"checkpoints 局（selfplay 协议）theme 应为空，得到 {final_theme!r}")
        if model_seat != learner_seat:
            errs.append(f"checkpoints 局 modelSeat={model_seat!r} != 学习者座次 {learner_seat!r}（归属注入失效？）")
    else:  # current
        if reset_theme != "" or model_seat != -1:
            errs.append(f"current 局应为 Unity 原生 selfplay 口径（theme 空/modelSeat=-1），"
                        f"得到 theme={reset_theme!r} seat={model_seat!r}")
    if not done:
        errs.append("done=False（循环退出仍未终局？）")

    summary = {"source": src, "steps": steps, "winner": winner or "(空)",
               "timeout": timeout, "modelSeat": model_seat}
    status = "PASS" if not errs else "FAIL"
    print(f"[{status}] 第 {game_no} 局：opponentSource={src} steps={steps} "
          f"winner={summary['winner']} timeout={timeout} modelSeat={model_seat}")
    for e in errs:
        print(f"        断言失败：{e}")
    return not errs, summary


def scan_unity_log_errors(env):
    """Unity batchmode 日志错误扫描：异常通道报错 / 未捕获 Exception 行数。"""
    log = getattr(env, "_log_file", None)
    if log is None or not Path(log).exists():
        return -1  # 日志不可读（不计 0，报告里说明）
    hits = []
    for line in Path(log).read_text(encoding="utf-8", errors="replace").splitlines():
        if ("[TideHeadless] 处理客户端时出错" in line
                or "Exception" in line and "FirstPass" not in line):
            hits.append(line.strip()[:160])
    return len(hits), hits


def main():
    rng = random.Random(20261102)
    env = TideEnv(max_steps=MAX_STEPS)  # 基座 env：三个壳共享一个 Unity 进程（一进程一局串行）
    pool = CheckpointPool(max_size=4)
    pool.add("init", {"smoke": np.zeros(1, np.float32)})  # 占位参数树（不跑 rollout）
    wrappers = {
        "current": OpponentPoolEnv(env, OpponentSampler("current", np.random.default_rng(1), pool)),
        "themes": OpponentPoolEnv(env, OpponentSampler("themes", np.random.default_rng(2), pool)),
        "checkpoints": OpponentPoolEnv(
            env, OpponentSampler("checkpoints", np.random.default_rng(3), pool)),
    }
    passed, game_no = 0, 0
    log_scan = None
    try:
        for source in ("current", "themes", "checkpoints"):
            for _ in range(N_GAMES_PER_SOURCE):
                game_no += 1
                ok, _sum = run_game(wrappers[source], rng, game_no)
                passed += 1 if ok else 0
        # 关进程前扫日志（terminate 噪声不混入错误计数）
        log_scan = scan_unity_log_errors(env)
    finally:
        env.close()

    n_errs, err_lines = (log_scan if isinstance(log_scan, tuple) else (log_scan, []))
    print(f"\nUnity 日志错误扫描（{getattr(env, '_log_file', '?')}）: "
          + (f"{n_errs} 处错误" if n_errs >= 0 else "日志不可读（跳过）"))
    for line in err_lines[:5]:
        print(f"  {line}")

    total = N_GAMES_PER_SOURCE * 3
    ok = passed == total and n_errs == 0
    print(f"\n对手池冒烟：{passed}/{total} 局通过，Unity errors={max(n_errs, 0)}"
          + ("（日志不可读不计）" if n_errs < 0 else ""))
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
