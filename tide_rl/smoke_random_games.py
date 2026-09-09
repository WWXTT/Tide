"""冒烟测试：随机策略跑完整对局，验证 reset 随机化 / step / 终局 / 进程跨局复用。"""
import sys
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).parent.parent))
sys.path.insert(0, str(Path(__file__).parent))

from tide_env import TideEnv


def main():
    rng = np.random.default_rng(0)
    env = TideEnv(max_steps=600)

    try:
        for game in range(3):
            obs, info = env.reset()
            n_legal = int((~obs["a_mask"]).sum())
            print(f"\n=== Game {game+1} === 初始合法动作 {n_legal}, "
                  f"牌库 me/opp = {obs['global_'][13]:.0f}/{obs['global_'][14]:.0f}, "
                  f"总价值 me/opp = {obs['global_'][28]:.1f}/{obs['global_'][29]:.1f}")

            done, step, total_r = False, 0, 0.0
            while not done:
                legal = np.flatnonzero(~obs["a_mask"])
                action = int(rng.choice(legal))
                obs, reward, done, trunc, info = env.step(action)
                step += 1
                total_r += reward
                done = done or trunc

            print(f"    终局: steps={step}, 累计reward={total_r:.3f}, "
                  f"winner={info.get('winner', '?')}, reason={info.get('reason', '?')}, "
                  f"timeout={info.get('timeout', False)}, turn={info.get('turn', '?')}")
    finally:
        env.close()
        print("\n[OK] 冒烟测试完成（进程跨局复用 + 终局收口验证）")


if __name__ == "__main__":
    main()
