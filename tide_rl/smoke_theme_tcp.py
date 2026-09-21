"""主题口径冒烟（编辑器 TCP 模式）：验证 2026-09-21 新卡组口径与协议字段。

前置：Unity 编辑器已打开工程，且 TCP 服务器已启动（StartTcpServer，端口 9999——
菜单入口已删，可经 execute_code / 测试代码调用）。

验证点：
1. selfplay：主题整组（三套随机其一）vs Cards 随机 30，随机换座——对局能完结，
   info.theme 为空、modelSeat=-1；
2. simpleai：模型 Cards 随机 30 vs 脚本主题卡组——info.theme ∈ {red,green,blue}、
   modelSeat ∈ {0,1}，对局能完结（对手回合由 SimpleAI+AutoMatch 自动打）。
"""
import sys
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).parent))

from tide_env_tcp import TideEnvTcp


def run_game(env, rng, opponent, max_steps=600):
    obs, info = env.reset(options={"opponent": opponent})
    theme = str(info.get("theme", ""))
    seat = info.get("modelSeat")
    done, step, total_r = False, 0, 0.0
    while not done:
        legal = np.flatnonzero(~obs["a_mask"])
        if len(legal) == 0:
            break
        obs, reward, done, trunc, info = env.step(int(rng.choice(legal)))
        step += 1
        total_r += reward
        done = done or trunc
        if step >= max_steps:
            break
    return theme, seat, info, step, total_r


def main():
    rng = np.random.default_rng(0)
    env = TideEnvTcp(max_steps=600)
    try:
        ok = True
        for g in range(2):
            theme, seat, info, step, total_r = run_game(env, rng, "selfplay")
            good = theme == "" and seat == -1
            ok &= good
            print(f"[selfplay {g+1}] theme={theme!r} seat={seat} steps={step} ret={total_r:+.2f} "
                  f"winner={info.get('winner')} reason={info.get('reason')} "
                  f"timeout={info.get('timeout', False)} {'OK' if good else 'FAIL: theme/seat 应为空/-1'}")

        for g in range(2):
            theme, seat, info, step, total_r = run_game(env, rng, "simpleai")
            good = theme in ("red", "green", "blue") and seat in (0, 1)
            ok &= good
            print(f"[vs脚本 {g+1}] theme={theme!r} seat={seat} steps={step} ret={total_r:+.2f} "
                  f"winner={info.get('winner')} reason={info.get('reason')} "
                  f"timeout={info.get('timeout', False)} {'OK' if good else 'FAIL: theme/seat 口径异常'}")

        print("\n[OK] 主题口径冒烟通过" if ok else "\n[FAIL] 存在口径异常，见上")
        sys.exit(0 if ok else 1)
    finally:
        env.close()


if __name__ == "__main__":
    main()
