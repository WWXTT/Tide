"""
快速验证训练流程（TCP 模式）。

目标：
- 验证训练循环能跑通
- 验证 Loss 计算正确
- 验证模型能保存/加载
- 验证 episode 能真正完结（对局跨更新窗口延续，见 rollout_common）

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

from ygoai.rl.jax import clipped_surrogate_pg_loss, mse_loss, entropy_loss

from tide_env_tcp import TideEnvTcp
from tide_agent import create_tide_agent
from tide_features import init_rstate, sample_input
from rollout_common import (
    rollout_single_env,
    bootstrap_next_value,
    compute_advantages_simple,
)


@dataclass
class QuickArgs:
    """快速验证参数（少量步数）。"""
    seed: int = 42
    learning_rate: float = 2.5e-4
    num_envs: int = 1  # TCP 模式：单连接，只用 1 个环境
    num_steps: int = 128  # 每次更新步数（对局跨窗口延续，不再要求窗口内完结）
    gamma: float = 0.99
    gae_lambda: float = 0.95
    clip_coef: float = 0.2
    ent_coef: float = 0.01
    vf_coef: float = 0.5
    max_grad_norm: float = 0.5

    total_updates: int = 10  # 只训练 10 次更新
    max_episode_steps: int = 500  # 单局步数上限（超时判负，保证 episode 一定能完结）
    reward_lambda: float = 0.02  # 兼容参数（塑形 λ 固定在 Unity 侧）
    channels: int = 128
    rnn_channels: int = 512
    rnn_type: str = "gru"


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
    log_print(f"Episode 步数上限: {args.max_episode_steps}")
    log_print(f"Total steps: {args.total_updates * args.num_envs * args.num_steps}")
    log_print("")

    # 创建环境
    log_print("创建环境（TCP 连接）...")
    envs = [TideEnvTcp(reward_lambda=args.reward_lambda, max_steps=args.max_episode_steps)
            for _ in range(args.num_envs)]
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
    dummy_obs = jax.tree.map(lambda x: x[None, ...], dummy_obs)

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

    # 初始观测（对局从这里开始，跨更新窗口延续）
    carries = []  # (obs, rstate, pending_ret, pending_len)
    for env in envs:
        obs, _ = env.reset()
        carries.append([obs, init_rstate(args.rnn_type), 0.0, 0])

    # 训练循环
    log_print("开始训练...")
    log_print("")

    episode_returns = []
    episode_lengths = []
    episode_timeouts = 0

    try:
        for update in range(1, args.total_updates + 1):
            update_start = time.time()

            # ==================== Rollout ====================
            rollout_data = []

            for env_id, env in enumerate(envs):
                obs, rstate, pend_ret, pend_len = carries[env_id]
                data, obs, rstate, rng, episodes, pending = rollout_single_env(
                    train_state.apply_fn, train_state.params, env, obs, rstate,
                    args.num_steps, rng, args.rnn_type,
                    pending=(pend_ret, pend_len),
                )
                rollout_data.append(data)
                carries[env_id] = [obs, rstate, pending[0], pending[1]]

                # 窗口内完结的 episode（回报已含跨窗口累计）
                for ep_ret, ep_len, timeout in episodes:
                    episode_returns.append(ep_ret)
                    episode_lengths.append(ep_len)
                    episode_timeouts += 1 if timeout else 0

            # ==================== 计算 GAE ====================
            all_advantages = []
            all_returns = []

            for env_id, data in enumerate(rollout_data):
                next_value = bootstrap_next_value(
                    train_state.apply_fn, train_state.params,
                    carries[env_id][0], carries[env_id][1],
                )
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
                    obs_batch = jax.tree.map(lambda x: x[None, ...] if x is not None else None, obs)
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
                       f"Episodes {len(episode_returns)} (超时 {episode_timeouts}) | "
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

    finally:
        # 清理（异常/中断也保证断开连接，避免残留）
        for env in envs:
            env.close()

    log_print("")
    log_print("=" * 80)
    log_print("✓ 快速验证训练完成！")
    log_print("=" * 80)
    log_print("")
    log_print("结果摘要：")
    log_print(f"  完成 {len(episode_returns)} 个 episodes（其中超时判负 {episode_timeouts}）")
    log_print(f"  平均回报: {np.mean(episode_returns) if episode_returns else 0:.2f}")
    log_print(f"  最大回报: {np.max(episode_returns) if episode_returns else 0:.2f}")
    log_print(f"  最小回报: {np.min(episode_returns) if episode_returns else 0:.2f}")
    log_print(f"  平均长度: {np.mean(episode_lengths) if episode_lengths else 0:.1f}")
    log_print(f"  最终 Loss: {float(loss):.3f}")
    log_print("")
    log_print("判定：")
    log_print("  - Episodes > 0 → 桥接与对局推进正常 ✓")
    log_print("  - Loss 有变化 → 训练流程正常 ✓")
    log_print("  - 全为超时 → 提高 max_episode_steps 或加长训练")
    log_print("")
    log_print(f"日志已保存到: {log_file}")
    log_print("")

    # 等待用户按回车
    input("按回车键退出...")


if __name__ == "__main__":
    quick_train()
