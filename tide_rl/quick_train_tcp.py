"""
快速验证训练流程（TCP 模式）。

目标：
- 验证训练循环能跑通
- 验证 Loss 计算正确
- 验证模型能保存/加载

运行前：
1. Unity 编辑器打开
2. 菜单：Tools > AI > 启动训练服务器 (TCP 9999)
3. python tide_rl/quick_train_tcp.py
"""

import sys
import time
from pathlib import Path
from dataclasses import dataclass
from datetime import datetime

import numpy as np
import jax
import jax.numpy as jnp
import flax
import optax
from flax.training.train_state import TrainState

# Setup paths
project_root = Path(__file__).parent.parent
sys.path.insert(0, str(project_root))
sys.path.insert(0, str(project_root / "ygo-agent-main"))

from ygoai.rl.jax import truncated_gae, clipped_surrogate_pg_loss, mse_loss, entropy_loss

from tide_env_tcp import TideEnvTcp
from tide_agent import create_tide_agent
from tide_features import init_rstate, sample_input, MAX_ACTIONS


@dataclass
class QuickArgs:
    """快速验证参数（少量步数）。"""
    seed: int = 42
    learning_rate: float = 2.5e-4
    num_envs: int = 1  # TCP 模式：单连接，只用 1 个环境
    num_steps: int = 128  # 增加到 128 步补偿环境数减少
    gamma: float = 0.99
    gae_lambda: float = 0.95
    clip_coef: float = 0.2
    ent_coef: float = 0.01
    vf_coef: float = 0.5
    max_grad_norm: float = 0.5

    total_updates: int = 10  # 只训练 10 次更新（约 1280 步）
    reward_lambda: float = 0.02
    channels: int = 128
    rnn_channels: int = 512
    rnn_type: str = "gru"


def rollout_single_env(agent_apply, params, env, num_steps, rng, rnn_type="gru"):
    """收集单个环境的 rollout。"""
    obs_list = []
    actions_list = []
    logprobs_list = []
    rewards_list = []
    dones_list = []
    values_list = []

    obs, info = env.reset()
    rstate = init_rstate(rnn_type)

    for step in range(num_steps):
        # 转为 batch
        obs_batch = jax.tree_map(lambda x: x[None, ...] if x is not None else None, obs)

        # 前向
        rstate, logits, value = agent_apply(params, rstate, obs_batch)
        logits = logits[0]
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
            rstate = init_rstate(rnn_type)

    return {
        "obs": obs_list,
        "actions": np.array(actions_list, dtype=np.int32),
        "logprobs": np.array(logprobs_list, dtype=np.float32),
        "rewards": np.array(rewards_list, dtype=np.float32),
        "dones": np.array(dones_list, dtype=np.bool_),
        "values": np.array(values_list, dtype=np.float32),
    }, rstate, rng


def compute_advantages_simple(values, rewards, dones, next_value, gamma, gae_lambda):
    """简化版 GAE 计算（单环境序列）。"""
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


def quick_train():
    """快速验证训练。"""
    args = QuickArgs()

    # 创建日志目录和文件
    log_dir = Path("logs/quick_train_tcp")
    log_dir.mkdir(parents=True, exist_ok=True)
    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    log_file = log_dir / f"train_{timestamp}.log"

    def log_print(msg):
        """同时打印到控制台和文件。"""
        print(msg)
        with open(log_file, "a", encoding="utf-8") as f:
            f.write(msg + "\n")

    log_print("=" * 80)
    log_print("Tide 快速验证训练（TCP 模式）")
    log_print("=" * 80)
    log_print(f"时间: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    log_print(f"日志文件: {log_file}")
    log_print(f"Updates: {args.total_updates}")
    log_print(f"Envs: {args.num_envs}")
    log_print(f"Steps per update: {args.num_steps}")
    log_print(f"Total steps: {args.total_updates * args.num_envs * args.num_steps}")
    log_print("")

    # 创建环境
    log_print("创建环境（TCP 连接）...")
    envs = [TideEnvTcp(reward_lambda=args.reward_lambda) for _ in range(args.num_envs)]
    log_print(f"[✓] 创建了 {args.num_envs} 个环境")
    log_print("")

    # 创建模型
    log_print("初始化模型...")
    rng = jax.random.PRNGKey(args.seed)
    rng, init_rng = jax.random.split(rng)

    agent = create_tide_agent(
        channels=args.channels,
        rnn_channels=args.rnn_channels,
        rnn_type=args.rnn_type,
    )

    dummy_rstate = init_rstate(args.rnn_type)
    dummy_obs = sample_input()
    dummy_obs = jax.tree_map(lambda x: x[None, ...], dummy_obs)

    params = agent.init(init_rng, dummy_rstate, dummy_obs)
    log_print("[✓] 模型初始化完成")
    log_print("")

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
    log_print("开始训练...")
    log_print("")

    episode_returns = []
    episode_lengths = []

    for update in range(1, args.total_updates + 1):
        update_start = time.time()

        # ==================== Rollout ====================
        rollout_data = []

        for env_id, env in enumerate(envs):
            data, _, rng = rollout_single_env(
                train_state.apply_fn, train_state.params, env, args.num_steps, rng, args.rnn_type
            )
            rollout_data.append(data)

            # 统计 episode
            for i, done in enumerate(data["dones"]):
                if done:
                    ep_return = data["rewards"][:i+1].sum()
                    episode_returns.append(ep_return)
                    episode_lengths.append(i+1)

        # ==================== 计算 GAE ====================
        # 简化版：逐环境计算 advantages
        all_advantages = []
        all_returns = []

        for env_id, data in enumerate(rollout_data):
            # 计算 next_value（最后一步的 value 估计）
            last_obs = data["obs"][-1]
            last_obs_batch = jax.tree_map(lambda x: x[None, ...] if x is not None else None, last_obs)
            rstate = init_rstate(args.rnn_type)
            _, _, next_value = train_state.apply_fn(train_state.params, rstate, last_obs_batch)
            next_value = float(next_value[0])

            # GAE
            advantages, returns = compute_advantages_simple(
                data["values"], data["rewards"], data["dones"],
                next_value, args.gamma, args.gae_lambda
            )

            all_advantages.append(advantages)
            all_returns.append(returns)

        # 合并所有环境的数据
        b_obs = [obs for data in rollout_data for obs in data["obs"]]
        b_actions = np.concatenate([data["actions"] for data in rollout_data])
        b_old_logprobs = np.concatenate([data["logprobs"] for data in rollout_data])
        b_advantages = np.concatenate(all_advantages)
        b_returns = np.concatenate(all_returns)

        # 归一化 advantages
        b_advantages = (b_advantages - b_advantages.mean()) / (b_advantages.std() + 1e-8)

        b_actions = jnp.array(b_actions, dtype=jnp.int32)
        b_old_logprobs = jnp.array(b_old_logprobs, dtype=jnp.float32)
        b_advantages = jnp.array(b_advantages, dtype=jnp.float32)
        b_returns = jnp.array(b_returns, dtype=jnp.float32)

        # ==================== PPO Update ====================
        def loss_fn(params):
            # 前向（简化：逐样本循环）
            all_logits = []
            all_values = []

            for obs in b_obs:
                obs_batch = jax.tree_map(lambda x: x[None, ...] if x is not None else None, obs)
                rstate = init_rstate(args.rnn_type)
                _, logits, value = train_state.apply_fn(params, rstate, obs_batch)
                all_logits.append(logits[0])
                all_values.append(value[0])

            b_logits = jnp.stack(all_logits, axis=0)
            b_new_values = jnp.stack(all_values, axis=0)

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
        mean_return = np.mean(episode_returns[-10:]) if episode_returns else 0.0
        mean_length = np.mean(episode_lengths[-10:]) if episode_lengths else 0.0

        log_msg = (f"Update {update:3d}/{args.total_updates} | "
                   f"Loss {float(loss):7.3f} | PG {float(pg_loss):7.3f} | "
                   f"VF {float(v_loss):7.3f} | Ent {float(entropy):6.3f} | "
                   f"EpRet {mean_return:7.2f} | EpLen {mean_length:5.1f} | "
                   f"Time {update_time:.2f}s")
        log_print(log_msg)

    # 保存模型
    log_print("")
    log_print("保存模型...")
    save_dir = Path("logs/quick_train_tcp")
    save_dir.mkdir(parents=True, exist_ok=True)
    with open(save_dir / "params.msgpack", "wb") as f:
        f.write(flax.serialization.to_bytes(train_state.params))
    log_print(f"[✓] 模型已保存到: {save_dir}")

    # 清理
    for env in envs:
        env.close()

    log_print("")
    log_print("=" * 80)
    log_print("✓ 快速验证训练完成！")
    log_print("=" * 80)
    log_print("")
    log_print("结果摘要：")
    log_print(f"  完成 {len(episode_returns)} 个 episodes")
    log_print(f"  平均回报: {np.mean(episode_returns) if episode_returns else 0:.2f}")
    log_print(f"  最大回报: {np.max(episode_returns) if episode_returns else 0:.2f}")
    log_print(f"  最小回报: {np.min(episode_returns) if episode_returns else 0:.2f}")
    log_print(f"  平均长度: {np.mean(episode_lengths) if episode_lengths else 0:.1f}")
    log_print(f"  最终 Loss: {float(loss):.3f}")
    log_print("")
    log_print("下一步：")
    log_print("  - 如果 Loss 下降 → 训练流程正常 ✓")
    log_print("  - 如果 Episode 完成 → 桥接正常 ✓")
    log_print("  - 可以开始完整训练了！")
    log_print("")
    log_print(f"日志已保存到: {log_file}")
    log_print("")

    # 等待用户按回车
    input("按回车键退出...")


if __name__ == "__main__":
    quick_train()
