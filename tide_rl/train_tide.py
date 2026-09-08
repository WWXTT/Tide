"""
Tide 最小 PPO 训练脚本（仿 cleanba.py 精简版）。

简化：
- 单机单进程，少量串行 env（不用 envpool/多机）
- 复用 ygo-agent 的 PPO 损失 / GAE / 训练循环
- 只训练打赢 SimpleAI，不做分布式
"""

import os
import time
import random
from pathlib import Path
from typing import Sequence
from dataclasses import dataclass

import numpy as np
import jax
import jax.numpy as jnp
import flax
import optax
from flax.training.train_state import TrainState

# 复用 ygo-agent PPO 损失（需要 ygo-agent 在 Python path）
import sys
ygo_agent_path = Path(__file__).parent.parent / "ygo-agent-main"
if ygo_agent_path.exists():
    sys.path.insert(0, str(ygo_agent_path))

try:
    from ygoai.rl.jax import compute_gae, ppo_loss
except ImportError:
    print("Warning: ygo-agent not found, using placeholder PPO loss")
    # 占位符（实际需要 ygo-agent）
    def compute_gae(*args, **kwargs):
        raise NotImplementedError("Install ygo-agent first")
    def ppo_loss(*args, **kwargs):
        raise NotImplementedError("Install ygo-agent first")

from tide_env import make_tide_env
from tide_agent import create_tide_agent
from tide_features import init_rstate, sample_input


@dataclass
class Args:
    """训练超参。"""
    exp_name: str = "tide_ppo_v1"
    seed: int = 42
    learning_rate: float = 2.5e-4
    num_envs: int = 4  # 串行 env 数量（CPU 限制）
    num_steps: int = 128  # 每次 rollout 步数
    num_minibatches: int = 4
    update_epochs: int = 4
    gamma: float = 0.99
    gae_lambda: float = 0.95
    clip_coef: float = 0.1
    ent_coef: float = 0.01
    vf_coef: float = 0.5
    max_grad_norm: float = 0.5
    total_timesteps: int = 1_000_000  # 100 万步（约 2~3k 局）
    eval_interval: int = 10_000  # 每 1 万步评估一次
    save_interval: int = 50_000  # 每 5 万步保存模型
    log_interval: int = 1000  # 每 1k 步打印日志

    # Tide 特定
    reward_lambda: float = 0.02  # v2 全资源势能，降低塑形强度
    channels: int = 128
    rnn_channels: int = 512
    rnn_type: str = "gru"

    # 路径
    unity_path: str = None  # 从环境变量 UNITY_PATH 读取
    project_path: str = None  # 默认当前目录的父目录


def make_env(args: Args, env_id: int):
    """创建环境（包装 seed）。"""
    def thunk():
        env = make_tide_env(
            unity_path=args.unity_path,
            project_path=args.project_path,
            reward_lambda=args.reward_lambda,
        )
        env.action_space.seed(args.seed + env_id)
        return env
    return thunk


def rollout(agent, rstate, env, num_steps, rng):
    """
    收集一次 rollout（串行 env，逐步执行）。

    Returns:
        obs, actions, logprobs, rewards, dones, values, new_rstate
    """
    obs_list = []
    actions_list = []
    logprobs_list = []
    rewards_list = []
    dones_list = []
    values_list = []

    # 重置 env
    obs, info = env.reset()

    for step in range(num_steps):
        obs_list.append(obs)

        # 前向
        rstate, logits, value = agent.apply(agent.params, rstate, obs)

        # 采样动作
        rng, subkey = jax.random.split(rng)
        action = jax.random.categorical(subkey, logits).item()
        logprob = jax.nn.log_softmax(logits)[action].item()

        # 执行
        next_obs, reward, done, truncated, info = env.step(action)

        actions_list.append(action)
        logprobs_list.append(logprob)
        rewards_list.append(reward)
        dones_list.append(done or truncated)
        values_list.append(value.item())

        obs = next_obs

        if done or truncated:
            obs, info = env.reset()
            rstate = init_rstate()  # 重置 RNN

    return (
        obs_list,
        np.array(actions_list),
        np.array(logprobs_list),
        np.array(rewards_list),
        np.array(dones_list),
        np.array(values_list),
        rstate,
        rng,
    )


def train(args: Args):
    """训练主循环。"""
    run_name = f"{args.exp_name}__{args.seed}__{int(time.time())}"
    print(f"Run: {run_name}")

    # 创建 env
    envs = [make_env(args, i)() for i in range(args.num_envs)]

    # 创建模型
    rng = jax.random.PRNGKey(args.seed)
    rng, init_rng = jax.random.split(rng)

    agent = create_tide_agent(
        channels=args.channels,
        rnn_channels=args.rnn_channels,
        rnn_type=args.rnn_type,
    )

    # 初始化参数
    dummy_rstate = init_rstate()
    dummy_obs = sample_input()
    dummy_obs = jax.tree_map(lambda x: x[None, ...], dummy_obs)  # add batch dim

    params = agent.init(init_rng, dummy_rstate, dummy_obs)

    # 优化器
    optimizer = optax.chain(
        optax.clip_by_global_norm(args.max_grad_norm),
        optax.adam(learning_rate=args.learning_rate),
    )

    train_state = TrainState.create(
        apply_fn=agent.apply,
        params=params,
        tx=optimizer,
    )

    # 训练循环
    num_updates = args.total_timesteps // (args.num_envs * args.num_steps)
    global_step = 0
    episode_returns = []
    episode_lengths = []

    print(f"Total updates: {num_updates}")
    print(f"Training for {args.total_timesteps} timesteps...")

    for update in range(1, num_updates + 1):
        # Rollout（串行收集）
        rollout_data = []
        rstates = [init_rstate() for _ in range(args.num_envs)]

        for env_id, env in enumerate(envs):
            rstate = rstates[env_id]
            obs_list, actions, logprobs, rewards, dones, values, new_rstate, rng = rollout(
                train_state, rstate, env, args.num_steps, rng
            )
            rollout_data.append({
                "obs": obs_list,
                "actions": actions,
                "logprobs": logprobs,
                "rewards": rewards,
                "dones": dones,
                "values": values,
            })
            rstates[env_id] = new_rstate

            # 统计
            for i, done in enumerate(dones):
                if done:
                    ep_return = rewards[:i+1].sum()
                    episode_returns.append(ep_return)
                    episode_lengths.append(i+1)

        global_step += args.num_envs * args.num_steps

        # TODO: 合并 rollout_data → batch → compute_gae → ppo_loss → update
        # （这里简化，实际需要完整实现 GAE + PPO update）

        # 日志
        if update % (args.log_interval // (args.num_envs * args.num_steps)) == 0:
            if episode_returns:
                mean_return = np.mean(episode_returns[-10:])
                mean_length = np.mean(episode_lengths[-10:])
                print(f"Update {update}/{num_updates} | Step {global_step} | "
                      f"EpRet {mean_return:.2f} | EpLen {mean_length:.1f}")

        # 评估
        if global_step % args.eval_interval == 0:
            print(f"[Eval @ {global_step}] TODO: vs SimpleAI")

        # 保存
        if global_step % args.save_interval == 0:
            save_path = Path(f"checkpoints/{run_name}/step_{global_step}")
            save_path.mkdir(parents=True, exist_ok=True)
            # TODO: save params

    # 清理
    for env in envs:
        env.close()

    print("Training complete!")


if __name__ == "__main__":
    args = Args()
    train(args)
