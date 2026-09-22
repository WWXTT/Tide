"""
共享 rollout 工具（quick_train_tcp / train_tide_complete 共用）。

核心约定：对局跨更新窗口延续。
之前的问题：每次 rollout 开头 env.reset() 把进行中的对局丢掉——128 步窗口远短于
一局对局（数百步），step_count 又随 reset 清零，500 步超时永不触发 → done 永远
观察不到 → episode 统计恒 0。修复后 (obs, rstate) 由调用方跨更新携带，只在
episode 结束时 reset。
"""

import numpy as np
import jax

from tide_features import init_rstate


def rollout_single_env(agent_apply, params, env, obs, rstate, num_steps, rng, rnn_type="gru",
                       pending=(0.0, 0), log_every_steps=0, seat=None):
    """从 (obs, rstate) 续跑 num_steps；episode 结束自动 reset 并重置 rstate。

    Args:
        obs / rstate: 上一窗口末尾的观测与 RNN 状态（首次由调用方 env.reset() 获得）
        pending: (return, length) 上一窗口末尾未完结 episode 的累计（本窗口接着累计）
        log_every_steps: >0 时每 N 步打印一次窗口内进度（心跳）
        seat: 传入 obs 的行动方座次（info.toPlay，0/1）——自对弈 GAE 座次修正用；
              None（旧协议/调用方未传）则整窗不记录座次，退回单代理口径

    Returns:
        data: dict{obs, rstates, actions, logprobs, rewards, dones, values,
                   seats(可 None), next_seat(可 None)}
              rstates = 每步前向实际使用的 incoming rstate（更新期重算 logprob 用，
              与采样口径一致——不能在更新期用重置态重算，否则 ratio 错位）
              seats = 每步行动方座次；next_seat = 窗口末携带 obs 的行动方座次
              （bootstrap 符号用）；中途 toPlay 缺失则两者置 None
        next_obs, next_rstate: 窗口末尾状态（交给下一窗口续跑）
        rng
        episodes: 本窗口内完结 episode 的 [(return, length, timeout), ...]（含跨窗口累计）
        pending: (return, length) 窗口末尾仍未完结的累计
    """
    obs_list = []
    rstates_list = []
    actions_list = []
    logprobs_list = []
    rewards_list = []
    dones_list = []
    values_list = []
    seats_list = [] if seat is not None else None
    episodes = []
    ep_ret, ep_len = pending

    for step in range(num_steps):
        # 转为 batch（h_actions_ 为 None 的树节点保持 None）
        obs_batch = jax.tree.map(lambda x: x[None, ...] if x is not None else None, obs)

        # 前向（记录 incoming rstate 供更新期同口径重算）
        rstates_list.append(rstate)
        rstate, logits, value = agent_apply(params, rstate, obs_batch)
        logits = logits[0]
        value = value[0]

        # 采样动作（logits 已在模型内屏蔽非法动作：-1e9）
        rng, subkey = jax.random.split(rng)
        action = jax.random.categorical(subkey, logits)
        action_np = int(action)
        logprob = jax.nn.log_softmax(logits)[action]

        # 记录
        obs_list.append(obs)
        actions_list.append(action_np)
        logprobs_list.append(float(logprob))
        values_list.append(float(value))
        if seats_list is not None:
            seats_list.append(seat)

        # 执行
        next_obs, reward, done, truncated, info = env.step(action_np)

        rewards_list.append(reward)
        dones_list.append(done or truncated)
        ep_ret += reward
        ep_len += 1

        obs = next_obs
        if done or truncated:
            episodes.append((ep_ret, ep_len, bool(info.get("timeout", False))))
            ep_ret, ep_len = 0.0, 0
            obs, info = env.reset()
            rstate = init_rstate(rnn_type)

        # 行动方座次推进：step/reset 响应的 info.toPlay 即下一决策点行动方
        # （done 后 info 来自 reset 响应）。中途缺失 → 整窗退回单代理口径。
        if seats_list is not None:
            to_play = info.get("toPlay")
            seat = int(to_play) if isinstance(to_play, int) and to_play >= 0 else None
            if seat is None:
                seats_list = None

        if log_every_steps and (step + 1) % log_every_steps == 0:
            print(f"  [rollout] step {step + 1}/{num_steps} episodes={len(episodes)}", flush=True)

    data = {
        "obs": obs_list,
        "rstates": rstates_list,
        "actions": np.array(actions_list, dtype=np.int32),
        "logprobs": np.array(logprobs_list, dtype=np.float32),
        "rewards": np.array(rewards_list, dtype=np.float32),
        "dones": np.array(dones_list, dtype=np.bool_),
        "values": np.array(values_list, dtype=np.float32),
        "seats": np.array(seats_list, dtype=np.int32) if seats_list is not None else None,
        "next_seat": seat if seats_list is not None else None,
    }
    return data, obs, rstate, rng, episodes, (ep_ret, ep_len)


def bootstrap_next_value(agent_apply, params, obs, rstate):
    """对窗口末尾的当前观测做价值 bootstrap（GAE 的 next_value）。

    注意用「携带的 rstate + 当前 obs」——用新初始化 rstate 或窗口内最后一个
    pre-action obs 都是口径漂移（done 尾步的 bootstrap 会被 GAE 的 non_terminal 权重清零）。
    """
    obs_batch = jax.tree.map(lambda x: x[None, ...] if x is not None else None, obs)
    _, _, value = agent_apply(params, rstate, obs_batch)
    return float(value[0])


def compute_advantages_simple(values, rewards, dones, next_value, gamma, gae_lambda,
                              seats=None, next_seat=None):
    """简化版 GAE（单环境序列）。

    seats 给出每步行动方座次（info.toPlay）时启用零和自对弈修正（2026-09-22）：
    自对弈双方决策在同一条轨迹交替，obs / reward / critic 输出全是「当前行动方」视角。
    零和势能 Φ_opp(s) ≡ −Φ_me(s) ⟹ 对手行动步的价值/奖励相对本方反号——旧口径把
    两个座次的流当单代理回溯，advantage 系统性有偏。符号变换法修正：
      sign[0]=+1，行动方换人处翻号（episode 边界重置 +1）；
      r̂=sign·r、V̂=sign·V、bootstrap 按 next_seat 是否换手定号，跑标准 GAE 得 Â；
      换回原空间 A=sign·Â、R=sign·(Â+V̂)=A+V——等价于对每个行动方计算
      「己方交替折扣回报」（对手步的奖励在本方回报里带负号）的 GAE。
    终局 ±1 是 actor-centric（胜者行动步 +1 / 败者行动步 −1），与该约定自洽。
    seats=None 走旧单代理口径（simpleai 训练 / quick 实验兼容；simpleai 模式下
    所有决策点都是模型座次，符号永不翻转，两条路径数值一致）。
    """
    if seats is None:
        advantages = []
        gae = 0.0
        for t in reversed(range(len(rewards))):
            if t == len(rewards) - 1:
                next_val = next_value
            else:
                next_val = values[t + 1]

            non_terminal = 1.0 - float(dones[t])
            delta = rewards[t] + gamma * next_val * non_terminal - values[t]
            gae = delta + gamma * gae_lambda * non_terminal * gae
            advantages.insert(0, gae)

        advantages = np.array(advantages, dtype=np.float32)
        returns = advantages + values
        return advantages, returns

    n = len(rewards)
    values = np.asarray(values, dtype=np.float32)
    rewards = np.asarray(rewards, dtype=np.float32)

    # 符号序列：新 episode 从 +1 起步，行动方换人翻号（同座次连续动作不翻——
    # 一个回合内多个决策步属同一行动方）
    sign = np.ones(n, dtype=np.float32)
    for t in range(1, n):
        if dones[t - 1]:
            sign[t] = 1.0
        elif seats[t] != seats[t - 1]:
            sign[t] = -sign[t - 1]
        else:
            sign[t] = sign[t - 1]

    # bootstrap 符号：窗口末携带 obs 的行动方（next_seat）相对最后一步是否换手
    flip = next_seat is not None and next_seat >= 0 and next_seat != seats[n - 1]
    nv_hat = (-sign[n - 1] if flip else sign[n - 1]) * next_value

    r_hat = sign * rewards
    v_hat = sign * values

    adv_hat = []
    gae = 0.0
    for t in reversed(range(n)):
        next_val = nv_hat if t == n - 1 else v_hat[t + 1]
        non_terminal = 1.0 - float(dones[t])
        delta = r_hat[t] + gamma * next_val * non_terminal - v_hat[t]
        gae = delta + gamma * gae_lambda * non_terminal * gae
        adv_hat.insert(0, gae)

    adv_hat = np.array(adv_hat, dtype=np.float32)
    advantages = sign * adv_hat
    returns = advantages + values

    return advantages, returns
