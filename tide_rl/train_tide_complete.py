"""
Tide 完整 PPO 训练脚本（补全版）。

目标：稳赢 SimpleAI
策略：
1. 使用 ygo-agent 的 GAE + PPO 损失
2. 增加训练步数和评估频率
3. 实时监控训练进度（loss、胜率、episode 长度）
4. 自动保存最优模型
"""

import os
import time
import random
import json
from pathlib import Path
from typing import Sequence
from dataclasses import dataclass
from collections import deque

import numpy as np
import jax
import jax.numpy as jnp
import flax
import optax
from flax.training.train_state import TrainState

# 复用 ygo-agent PPO 损失
import sys
ygo_agent_path = Path(__file__).parent.parent / "ygo-agent-main"
if ygo_agent_path.exists():
    sys.path.insert(0, str(ygo_agent_path))

from ygoai.rl.jax import truncated_gae, clipped_surrogate_pg_loss, mse_loss, entropy_loss

from tide_env import make_tide_env
from tide_agent import create_tide_agent
from tide_features import init_rstate, sample_input, MAX_ACTIONS


@dataclass
class Args:
    """训练超参。"""
    exp_name: str = "tide_ppo_beat_simpleai"
    seed: int = 42
    learning_rate: float = 2.5e-4
    anneal_lr: bool = True  # 学习率线性衰减

    num_envs: int = 4  # 并行 env 数量
    num_steps: int = 256  # 每次 rollout 步数（增加以获得更长轨迹）
    num_minibatches: int = 4
    update_epochs: int = 4

    gamma: float = 0.99
    gae_lambda: float = 0.95
    clip_coef: float = 0.2
    ent_coef: float = 0.01
    vf_coef: float = 0.5
    max_grad_norm: float = 0.5

    total_timesteps: int = 2_000_000  # 200 万步（约 5~10k 局）
    eval_interval: int = 5_000  # 每 5k 步评估
    eval_episodes: int = 20  # 每次评估 20 局
    save_interval: int = 20_000  # 每 2 万步保存
    log_interval: int = 500  # 每 500 步打印

    # Tide 特定
    reward_lambda: float = 0.02  # v2 全资源势能
    channels: int = 128
    rnn_channels: int = 512
    rnn_type: str = "gru"

    # 路径
    unity_path: str = None
    project_path: str = None

    # 早停
    target_win_rate: float = 0.75  # 胜率 >75% 即认为稳赢
    patience: int = 5  # 连续 5 次评估都达标即停止


def linear_schedule(initial_value: float):
    """线性衰减学习率。"""
    def func(progress):
        return initial_value * (1 - progress)
    return func


def rollout_single_env(agent_apply, params, rstate, env, num_steps, rng):
    """
    单个 env 收集 rollout。

    Returns:
        dict with obs, actions, logprobs, rewards, dones, values (all arrays)
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
        # 转换 obs 为 batch (add batch dim)
        obs_batch = jax.tree_map(lambda x: x[None, ...], obs)

        # 前向
        rstate, logits, value = agent_apply(params, rstate, obs_batch)
        logits = logits[0]  # remove batch dim
        value = value[0]

        # 采样动作
        rng, subkey = jax.random.split(rng)
        action = jax.random.categorical(subkey, logits)
        action_np = int(action)
        logprob = jax.nn.log_softmax(logits)[action]

        # 记录
        obs_list.append(obs)
        actions_list.append(action_np)
        logprobs_list.append(float(logprob))
        values_list.append(float(value))

        # 执行
        next_obs, reward, done, truncated, info = env.step(action_np)

        rewards_list.append(reward)
        dones_list.append(done or truncated)

        obs = next_obs

        if done or truncated:
            obs, info = env.reset()
            rstate = init_rstate()

    return {
        "obs": obs_list,
        "actions": np.array(actions_list, dtype=np.int32),
        "logprobs": np.array(logprobs_list, dtype=np.float32),
        "rewards": np.array(rewards_list, dtype=np.float32),
        "dones": np.array(dones_list, dtype=np.bool_),
        "values": np.array(values_list, dtype=np.float32),
    }, rstate, rng


def obs_list_to_batch(obs_list, num_envs, num_steps):
    """将 obs list 转为 batch (num_envs, num_steps, ...)。"""
    # obs_list 是 list of list of dict
    # 需要转为 dict of arrays (num_envs, num_steps, ...)

    batch = {}
    for key in obs_list[0][0].keys():
        if key == "h_actions_" and obs_list[0][0][key] is None:
            batch[key] = None
            continue

        # 收集所有 obs
        arrays = []
        for env_id in range(num_envs):
            env_arrays = [obs_list[env_id][step][key] for step in range(num_steps)]
            arrays.append(np.stack(env_arrays, axis=0))  # (num_steps, ...)

        batch[key] = np.stack(arrays, axis=0)  # (num_envs, num_steps, ...)

    return batch


def evaluate_vs_simpleai(agent_apply, params, args, num_episodes=20):
    """评估 vs SimpleAI 胜率。"""
    print(f"\n{'='*60}")
    print(f"Evaluating {num_episodes} episodes vs SimpleAI...")
    print(f"{'='*60}")

    env = make_tide_env(
        unity_path=args.unity_path,
        project_path=args.project_path,
        reward_lambda=args.reward_lambda,
    )

    wins = 0
    losses = 0
    draws = 0
    episode_lengths = []

    rng = jax.random.PRNGKey(args.seed + 999)

    for ep in range(num_episodes):
        obs, info = env.reset()
        rstate = init_rstate()
        done = False
        step_count = 0

        while not done and step_count < 500:
            obs_batch = jax.tree_map(lambda x: x[None, ...], obs)
            rstate, logits, value = agent_apply(params, rstate, obs_batch)
            logits = logits[0]

            # 评估时用 greedy（不采样）
            action = int(jnp.argmax(logits))

            obs, reward, done, truncated, info = env.step(action)
            step_count += 1
            done = done or truncated

        episode_lengths.append(step_count)

        if done:
            winner = info.get("winner", 0)
            if winner == 1:
                wins += 1
                result = "WIN"
            elif winner == 2:
                losses += 1
                result = "LOSS"
            else:
                draws += 1
                result = "DRAW"
        else:
            draws += 1
            result = "TIMEOUT"

        print(f"  Episode {ep+1}/{num_episodes}: {result} (length={step_count})")

    env.close()

    win_rate = wins / num_episodes
    avg_length = np.mean(episode_lengths)

    print(f"\n{'='*60}")
    print(f"Evaluation Results:")
    print(f"  Win Rate: {win_rate*100:.1f}% ({wins}/{num_episodes})")
    print(f"  Losses: {losses}, Draws: {draws}")
    print(f"  Avg Episode Length: {avg_length:.1f}")
    print(f"{'='*60}\n")

    return win_rate, wins, losses, draws, avg_length


def train(args: Args):
    """训练主循环。"""
    run_name = f"{args.exp_name}__{args.seed}__{int(time.time())}"
    log_dir = Path(f"logs/{run_name}")
    log_dir.mkdir(parents=True, exist_ok=True)

    print(f"\n{'='*80}")
    print(f"Run: {run_name}")
    print(f"Target: Win Rate > {args.target_win_rate*100:.0f}% vs SimpleAI")
    print(f"{'='*80}\n")

    # 保存配置
    with open(log_dir / "config.json", "w") as f:
        json.dump(vars(args), f, indent=2)

    # 创建 envs
    print(f"Creating {args.num_envs} environments...")
    envs = [make_tide_env(
        unity_path=args.unity_path,
        project_path=args.project_path,
        reward_lambda=args.reward_lambda,
    ) for i in range(args.num_envs)]
    print("Environments created.\n")

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
    dummy_obs = jax.tree_map(lambda x: x[None, ...], dummy_obs)

    params = agent.init(init_rng, dummy_rstate, dummy_obs)

    # 优化器
    if args.anneal_lr:
        num_updates = args.total_timesteps // (args.num_envs * args.num_steps)
        lr_schedule = optax.linear_schedule(
            init_value=args.learning_rate,
            end_value=0.0,
            transition_steps=num_updates,
        )
        optimizer = optax.chain(
            optax.clip_by_global_norm(args.max_grad_norm),
            optax.adam(learning_rate=lr_schedule),
        )
    else:
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

    episode_returns = deque(maxlen=100)
    episode_lengths = deque(maxlen=100)

    best_win_rate = 0.0
    patience_counter = 0

    print(f"Starting training for {num_updates} updates ({args.total_timesteps} timesteps)...\n")

    start_time = time.time()

    for update in range(1, num_updates + 1):
        update_start = time.time()

        # ==================== Rollout ====================
        rollout_data = []
        rstates = [init_rstate() for _ in range(args.num_envs)]

        for env_id, env in enumerate(envs):
            rstate = rstates[env_id]
            data, new_rstate, rng = rollout_single_env(
                train_state.apply_fn, train_state.params, rstate, env, args.num_steps, rng
            )
            rollout_data.append(data)
            rstates[env_id] = new_rstate

            # 统计 episode 信息
            for i, done in enumerate(data["dones"]):
                if done:
                    ep_return = data["rewards"][:i+1].sum()
                    episode_returns.append(ep_return)
                    episode_lengths.append(i+1)

        global_step += args.num_envs * args.num_steps

        # ==================== 合并 batch ====================
        obs_batch = obs_list_to_batch([d["obs"] for d in rollout_data], args.num_envs, args.num_steps)
        actions = np.stack([d["actions"] for d in rollout_data], axis=0)  # (num_envs, num_steps)
        old_logprobs = np.stack([d["logprobs"] for d in rollout_data], axis=0)
        rewards = np.stack([d["rewards"] for d in rollout_data], axis=0)
        dones = np.stack([d["dones"] for d in rollout_data], axis=0)
        values = np.stack([d["values"] for d in rollout_data], axis=0)

        # 转为 JAX arrays
        obs_batch = jax.tree_map(lambda x: jnp.array(x) if x is not None else None, obs_batch)
        actions = jnp.array(actions, dtype=jnp.int32)
        old_logprobs = jnp.array(old_logprobs, dtype=jnp.float32)
        rewards = jnp.array(rewards, dtype=jnp.float32)
        dones = jnp.array(dones, dtype=jnp.bool_)
        values = jnp.array(values, dtype=jnp.float32)

        # ==================== GAE ====================
        # 计算 next_value（最后一步的 value）
        last_obs_batch = jax.tree_map(lambda x: x[:, -1] if x is not None else None, obs_batch)
        _, _, next_values = jax.vmap(lambda rs, obs: train_state.apply_fn(
            train_state.params, rs, obs[None, ...]
        ))(jnp.array([init_rstate()[0] for _ in range(args.num_envs)]), last_obs_batch)

        # 展平为 (num_envs * num_steps,)
        b_values = values.reshape(-1)
        b_rewards = rewards.reshape(-1)
        b_dones = dones.reshape(-1)

        # GAE（简化版，不考虑 mains）
        advantages = []
        returns = []
        gae = 0
        for t in reversed(range(args.num_steps)):
            for e in range(args.num_envs):
                idx = e * args.num_steps + t
                if t == args.num_steps - 1:
                    next_value = next_values[e]
                    next_non_terminal = 1.0 - b_dones[idx]
                else:
                    next_value = b_values[idx + 1]
                    next_non_terminal = 1.0 - b_dones[idx]

                delta = b_rewards[idx] + args.gamma * next_value * next_non_terminal - b_values[idx]
                gae = delta + args.gamma * args.gae_lambda * next_non_terminal * gae
                advantages.insert(0, gae)
                returns.insert(0, gae + b_values[idx])

        b_advantages = jnp.array(advantages, dtype=jnp.float32)
        b_returns = jnp.array(returns, dtype=jnp.float32)

        # 归一化 advantages
        b_advantages = (b_advantages - b_advantages.mean()) / (b_advantages.std() + 1e-8)

        # ==================== PPO Update ====================
        # TODO: 完整实现 minibatch + multi-epoch 更新
        # 当前简化为单次更新

        # 展平 actions, old_logprobs
        b_actions = actions.reshape(-1)
        b_old_logprobs = old_logprobs.reshape(-1)

        def loss_fn(params):
            # 前向（需要重新计算 logits）
            # 简化：这里需要批量前向，暂时用循环（TODO: vmap 优化）
            all_logits = []
            all_values = []
            for e in range(args.num_envs):
                env_obs = jax.tree_map(lambda x: x[e] if x is not None else None, obs_batch)
                rstate = init_rstate()
                for t in range(args.num_steps):
                    step_obs = jax.tree_map(lambda x: x[t][None, ...] if x is not None else None, env_obs)
                    rstate, logits, value = train_state.apply_fn(params, rstate, step_obs)
                    all_logits.append(logits[0])
                    all_values.append(value[0])

            b_logits = jnp.stack(all_logits, axis=0)  # (num_envs * num_steps, MAX_ACTIONS)
            b_new_values = jnp.stack(all_values, axis=0)  # (num_envs * num_steps,)

            # Policy loss
            new_logprobs = jax.nn.log_softmax(b_logits)
            new_logprobs = new_logprobs[jnp.arange(len(b_actions)), b_actions]

            ratios = jnp.exp(new_logprobs - b_old_logprobs)
            pg_loss = clipped_surrogate_pg_loss(ratios, b_advantages, args.clip_coef)
            pg_loss = pg_loss.mean()

            # Value loss
            v_loss = mse_loss(b_returns, b_new_values).mean()

            # Entropy
            entropy = entropy_loss(b_logits).mean()

            # Total loss
            loss = pg_loss + args.vf_coef * v_loss - args.ent_coef * entropy

            return loss, (pg_loss, v_loss, entropy)

        # 计算梯度并更新
        (loss, (pg_loss, v_loss, entropy)), grads = jax.value_and_grad(loss_fn, has_aux=True)(train_state.params)
        train_state = train_state.apply_gradients(grads=grads)

        update_time = time.time() - update_start

        # ==================== 日志 ====================
        if update % (args.log_interval // (args.num_envs * args.num_steps)) == 0 or update == 1:
            mean_return = np.mean(episode_returns) if episode_returns else 0.0
            mean_length = np.mean(episode_lengths) if episode_lengths else 0.0
            elapsed = time.time() - start_time
            sps = global_step / elapsed  # steps per second

            print(f"Update {update:5d}/{num_updates} | Step {global_step:8d} | "
                  f"Loss {float(loss):7.3f} | PG {float(pg_loss):7.3f} | "
                  f"VF {float(v_loss):7.3f} | Ent {float(entropy):6.3f} | "
                  f"EpRet {mean_return:7.2f} | EpLen {mean_length:5.1f} | "
                  f"SPS {sps:5.0f} | Time {update_time:.2f}s")

        # ==================== 评估 ====================
        if global_step % args.eval_interval == 0:
            win_rate, wins, losses, draws, avg_length = evaluate_vs_simpleai(
                train_state.apply_fn, train_state.params, args, args.eval_episodes
            )

            # 保存最优模型
            if win_rate > best_win_rate:
                best_win_rate = win_rate
                save_path = log_dir / "best_model"
                save_path.mkdir(exist_ok=True)
                with open(save_path / "params.msgpack", "wb") as f:
                    f.write(flax.serialization.to_bytes(train_state.params))
                print(f"✓ New best model saved (win_rate={win_rate*100:.1f}%)\n")

            # 早停检查
            if win_rate >= args.target_win_rate:
                patience_counter += 1
                print(f"✓ Target win rate reached! ({patience_counter}/{args.patience})\n")
                if patience_counter >= args.patience:
                    print(f"{'='*80}")
                    print(f"Training complete! Win rate {win_rate*100:.1f}% >= target {args.target_win_rate*100:.1f}%")
                    print(f"{'='*80}\n")
                    break
            else:
                patience_counter = 0

        # ==================== 保存 checkpoint ====================
        if global_step % args.save_interval == 0:
            save_path = log_dir / f"checkpoint_{global_step}"
            save_path.mkdir(exist_ok=True)
            with open(save_path / "params.msgpack", "wb") as f:
                f.write(flax.serialization.to_bytes(train_state.params))
            print(f"✓ Checkpoint saved at step {global_step}\n")

    # 最终评估
    print(f"\n{'='*80}")
    print(f"Final Evaluation (100 episodes):")
    print(f"{'='*80}")
    final_win_rate, wins, losses, draws, avg_length = evaluate_vs_simpleai(
        train_state.apply_fn, train_state.params, args, num_episodes=100
    )

    # 清理
    for env in envs:
        env.close()

    elapsed_total = time.time() - start_time
    print(f"\n{'='*80}")
    print(f"Training finished!")
    print(f"  Total time: {elapsed_total/3600:.2f} hours")
    print(f"  Best win rate: {best_win_rate*100:.1f}%")
    print(f"  Final win rate: {final_win_rate*100:.1f}%")
    print(f"{'='*80}\n")


if __name__ == "__main__":
    args = Args()

    # 从环境变量读取（如果有）
    args.unity_path = os.environ.get("UNITY_PATH", args.unity_path)
    args.project_path = os.environ.get("TIDE_PROJECT_PATH", args.project_path)

    # 如果环境变量没设置，使用硬编码路径
    if args.unity_path is None:
        args.unity_path = r"C:\Program Files\Unity\Hub\Editor\6000.5.8f1\Editor\Unity.exe"
    if args.project_path is None:
        args.project_path = r"E:\UnityProject\Tide"

    print(f"\nConfiguration:")
    print(f"  Unity: {args.unity_path or 'from default'}")
    print(f"  Project: {args.project_path or 'auto-detect'}")
    print(f"  Reward lambda: {args.reward_lambda}")
    print(f"  Total timesteps: {args.total_timesteps:,}")
    print(f"  Target win rate: {args.target_win_rate*100:.0f}%\n")

    train(args)
