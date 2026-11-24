"""
红·快攻镜像冒烟（训练桥 v7 对齐验收，2026-10-30 第二阶段）：
模型侧固定红主题整组（model_theme="red"）vs SimpleAI 红主题（theme="red"），跑 3 局。

验收断言（每局）：
  info.modelTheme == "red"   —— 模型侧卡组=红主题整组（协议回显实际命中 key）
  info.theme == "red"        —— 对手=红主题 SimpleAI（AutoMatch 红快攻策略）
  info.modelSeat in (0, 1)   —— 随机模型座次（先后手各半）
  done 且 winner 非空         —— 真实分出胜负（超时判负 winner 为空会在此响亮失败）
  2 <= steps < 600            —— max_steps=600 超时兜底未触发；下界 2 拦截「reset 即终局」坏局
                               （红·快攻镜像实测真实终局 4~36 步 LifeZero——双快攻互rush+随机策略
                               30% EndTurn 放空，4 步终局正常；2026-10-30 两轮实跑定界）

策略：随机 + 30% 概率优先 EndTurn（与 TideHeadlessServer.RunSelfTest.PickAction 同款，
保证对局推进）——本脚本只验收驱动环与契约口径，不评棋力。

运行（临时工程 tide_rl 目录下，首次会拉起 Unity batchmode，3-5 分钟）：
  python smoke_red_mirror.py
退出码 0 = 3/3 全过；非 0 = 存在失败局（输出逐局断言明细）。
"""

import random
import sys

from tide_env import TideEnv
from tide_features import ACTION_F_TYPE, action_valid_mask

N_GAMES = 3
MAX_STEPS = 600
END_TURN = 5  # TideActionType.EndTurn（契约 §1）


def pick_action(obs, rng):
    """随机策略（30% 概率优先 EndTurn 保证推进）——与 C# RunSelfTest.PickAction 同口径。"""
    actions = obs["actions_"]
    valid = int(action_valid_mask(actions).sum())  # 合法行数（valid 位自算）
    if valid == 0:
        raise RuntimeError("合法动作数为 0（服务端 nActions=0？驱动环异常）")
    if rng.random() < 0.3:
        for i in range(valid):
            if int(actions[i][ACTION_F_TYPE]) == END_TURN:
                return i
    return rng.randrange(valid)


def run_game(env, rng, game):
    """跑一局并断言镜像口径。返回 (是否通过, 局末 info 快照)。"""
    obs, info = env.reset(options={"theme": "red"})  # 对手钉红；模型侧 model_theme 已在 env 上
    done = False
    steps = 0
    while not done and steps < MAX_STEPS:
        action = pick_action(obs, rng)
        obs, reward, done, truncated, info = env.step(action)
        steps += 1
        done = done or truncated

    theme = str(info.get("theme", "") or "")
    model_theme = str(info.get("modelTheme", "") or "")
    model_seat = info.get("modelSeat")
    winner = str(info.get("winner", "") or "")
    reason = str(info.get("reason", "") or "")
    timeout = bool(info.get("timeout"))

    errs = []
    if model_theme != "red":
        errs.append(f"modelTheme={model_theme!r} != 'red'（模型侧卡组未固定红主题？）")
    if theme != "red":
        errs.append(f"theme={theme!r} != 'red'（对手主题未钉红？）")
    if model_seat not in (0, 1):
        errs.append(f"modelSeat={model_seat!r} 不在 (0, 1)")
    if not done:
        errs.append("done=False（循环退出但仍未终局？）")
    if not winner:
        errs.append(f"winner 为空（reason={reason!r}, timeout={timeout}——超时/平局不算通过）")
    if not (2 <= steps < MAX_STEPS):
        errs.append(f"steps={steps} 不在 [2, {MAX_STEPS}) 区间")

    status = "PASS" if not errs else "FAIL"
    print(f"[{status}] 第 {game}/{N_GAMES} 局：theme={theme} modelTheme={model_theme} "
          f"modelSeat={model_seat} steps={steps} winner={winner or '(空)'} "
          f"reason={reason or '(无)'}")
    for e in errs:
        print(f"        断言失败：{e}")
    return not errs, info


def main():
    rng = random.Random(20261030)
    env = TideEnv(opponent="simpleai", model_theme="red", max_steps=MAX_STEPS)
    passed = 0
    failed_games = []
    try:
        for game in range(1, N_GAMES + 1):
            ok, _info = run_game(env, rng, game)
            if ok:
                passed += 1
            else:
                failed_games.append(game)
    finally:
        env.close()

    print(f"\n红·快攻镜像冒烟：{passed}/{N_GAMES} 通过"
          + ("" if not failed_games else f"（失败局：{failed_games}）"))
    return 0 if passed == N_GAMES else 1


if __name__ == "__main__":
    sys.exit(main())
