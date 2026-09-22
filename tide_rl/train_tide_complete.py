"""
Tide 完整 PPO 训练脚本（stdio batchmode 桥接）。

目标：主题自对弈训练（默认 opponent="selfplay"——双方同一策略驱动，obs/reward 按
「当前行动方」视角轮换；卡组口径 2026-09-21 主题卡组迁移：一侧随机用三套主题卡组
之一整组，另一侧 Cards.json 全池随机抽 30 张，随机换座）。
奖励（2026-09-22 修复）：塑形 λ 经 reset 协议下发（默认 0.005）且 Unity 侧势能剔除
手牌——旧口径（λ=0.05 硬编码 + 势能含手牌）塑形累计淹没终局 ±1，学成拖局；
自对弈 GAE 按 info.toPlay 行动方座次做零和符号修正（对手步价值/奖励反号）。
评估双口径（自对弈模式每次评估都跑两组）：
- 自对弈先手座次胜率：P1 恒先手，偏离 50% 的幅度 = 先后手失衡度量（非棋力）；
- vs 脚本主题卡组模型胜率（模型 Cards 随机 30 vs SimpleAI+AutoMatch 主题整组，座次随机、
  按 info.modelSeat 判）——真实棋力指标，另按 info.theme（red/green/blue）分主题统计；
  best 保存与早停均按此口径（总胜率）。
opponent="simpleai" 可切回「打脚本主题」训练（评估只剩单一口径）。
策略：
1. 使用 ygo-agent 的 PPO 损失（jit + 整批前向，rollout 记录 rstate 保证更新期口径一致）
2. 对局跨更新窗口延续（rollout_common），episode 统计真实完结
3. 训练统计每个 update 落盘 stats.jsonl（loss/ep_ret/ep_len/sps，可 tail 实时跟踪）
4. YOLO 风格模型保存：best/（评估刷新纪录时覆盖）+ last/（周期覆盖，退出时也存）

运行（推荐临时工程隔离训练，见 make_train_copy.ps1 / start_training_traincopy.ps1）：
  python tide_rl/train_tide_complete.py
  （Unity.exe 自动发现：UNITY_PATH 环境变量 → Hub 扫描匹配工程版本；
    进程常驻跨局复用，启动前自动清理上次残留的 batchmode 进程）

重要约束：stdio 桥接一个 Unity 进程一次只跑一场对局 → num_envs 必须为 1。
"""

import os
import time
import json
from pathlib import Path
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

from ygoai.rl.jax import clipped_surrogate_pg_loss, mse_loss, entropy_loss

from tide_env import TideEnv
from tide_agent import create_tide_agent
from tide_features import init_rstate, sample_input
from rollout_common import (
    rollout_single_env,
    bootstrap_next_value,
    compute_advantages_simple,
)


def stack_obs(obs_list):
    """N 个 obs dict → 整批 (N, ...)；None 叶子（h_actions_）保持 None。"""
    batched = {}
    for k, first in obs_list[0].items():
        batched[k] = None if first is None else np.stack([np.asarray(o[k]) for o in obs_list])
    return batched


def stack_rstates(rstates):
    """N 个 (1, D) rstate → (N, D)；LSTM 元素组按叶堆叠。"""
    return jax.tree.map(lambda *leaves: np.stack(leaves).reshape(len(leaves), -1), *rstates)


def _seat_of(info):
    """reset/step 响应 info 里的行动方座次（info.toPlay，0/1）；缺失/非法返回 None。"""
    to_play = (info or {}).get("toPlay")
    return int(to_play) if isinstance(to_play, int) and to_play >= 0 else None


@dataclass
class Args:
    """训练超参。"""
    exp_name: str = "tide_ppo_selfplay_theme"
    seed: int = 42
    learning_rate: float = 2.5e-4
    anneal_lr: bool = True  # 学习率线性衰减

    # stdio 桥接一个 Unity 进程一场对局 → 恒为 1（评估复用同一 env）
    num_envs: int = 1
    num_steps: int = 256  # 每次 rollout 步数（对局跨窗口延续）
    update_epochs: int = 1  # 简化：单次更新（minibatch/多轮 TODO）

    gamma: float = 0.99
    gae_lambda: float = 0.95
    clip_coef: float = 0.2
    ent_coef: float = 0.05  # 熵系数（0.01 时点积 actor 熵 7 个 update 内塌零，已加温度修复后仍提到 0.05 保险）
    vf_coef: float = 0.5
    max_grad_norm: float = 0.5

    total_timesteps: int = 2_000_000
    eval_interval: int = 50_000  # 每 5 万步评估
    eval_episodes: int = 50  # 每次评估局数（2026-09-22 20→50：20 局二项噪声 σ≈0.11，best 选点运气成分过大）
    save_interval: int = 100_000  # 每 10 万步保存
    log_interval: int = 2_560  # 每 2560 步打印（对齐 update 粒度）

    # Tide 特定
    max_episode_steps: int = 1000  # 单局步数上限（超时判负；可选操作变多后 500 会提前截断对局）
    # 塑形 λ（2026-09-22 起经 reset 协议真正下发到 Unity）。旧口径 λ=0.05 且势能含手牌：
    # 双方各自整局塑形累计 ≈ +3.1 淹没终局 ±1，自对弈收敛到拖局（局长 90→267、
    # 对脚本胜率 0.35→0.1）——现降 10 倍，且 Unity 侧奖励势能已剔除手牌。
    reward_lambda: float = 0.005
    opponent: str = "selfplay"   # 对手位：selfplay = 主题自对弈（主题整组 vs Cards 随机 30）；simpleai = 模型 vs 脚本主题卡组（座次随机=先后手各半）
    # 评估基准主题（2026-09-22 临时定案：绿/蓝脚本待重设计——随机基线绿 100%/蓝 55% 白送分
    # 污染总胜率口径，重设计期只以红·快攻验证进步；重设计完成后改回空串=三主题随机）
    eval_theme: str = "red"
    channels: int = 128
    rnn_channels: int = 512
    rnn_type: str = "gru"

    # 路径（None = 自动发现：UNITY_PATH → Hub 扫描匹配 ProjectVersion.txt）
    unity_path: str = None
    project_path: str = None

    # 早停
    target_win_rate: float = 0.75
    patience: int = 5  # 连续 5 次评估都达标即停止

    # 看门狗
    startup_timeout: float = 300.0  # Unity batchmode 启动+首个响应
    step_timeout: float = 120.0


def evaluate_greedy(agent_apply, params, env, args, num_episodes=20, opponent=None, theme=None):
    """贪心策略评估（复用训练 env——stdio 桥接一进程一局，不能另开）。

    opponent=None 时用 args.opponent（训练口径）；自对弈训练中途可传 "simpleai"
    切换评估对手（reset 协议按次传 opponent，无需另开 env）。
    theme 可钉死 vs 脚本口径的对手主题（args.eval_theme，2026-09-22 临时只评红）。

    对手位口径（2026-09-21 主题卡组迁移）：
    - simpleai：模型（Cards.json 随机 30）vs 脚本主题卡组（SimpleAI+AutoMatch 主题整组），
      座次随机，按 info.modelSeat 判模型胜负——真实棋力指标；info.theme（red/green/blue）
      另做分主题统计（对手是哪套主题）；
    - selfplay：双方同一策略按座次报告——P1 恒先手，即「先手座次胜率」（衡量先手优势）。

    返回 (win_rate, wins, losses, draws, avg_length, theme_stats)；
    theme_stats = {theme_key: (wins, games)}（仅 simpleai 口径有值，selfplay 为空 dict）。
    """
    vs_script = (opponent or args.opponent) == "simpleai"
    label = "vs 脚本主题卡组（模型胜率）" if vs_script else "self-play（先手座次胜率）"

    print(f"\n{'='*60}")
    print(f"Evaluating {num_episodes} episodes (greedy, {label})")
    print(f"{'='*60}")

    wins = losses = draws = 0
    episode_lengths = []
    theme_stats = {}  # theme key -> [wins, games]（vs 脚本口径）

    for ep in range(num_episodes):
        # 按次传对手位（2026-09-11 修复：此前裸调用 env.reset()，options 缺省回落
        # self.opponent=selfplay——自对弈训练期间"vs SimpleAI"评估实际在跑自对弈，
        # modelSeat=-1 落进 winner=="0" 分支，统计的是 P1 座次胜率而非模型棋力，
        # best/早停全程选在幻影指标上）；theme 钉死对手主题（临时只评红）
        options = {"opponent": opponent or args.opponent}
        if theme:
            options["theme"] = theme
        obs, info = env.reset(options=options)
        rstate = init_rstate(args.rnn_type)
        done = False
        step_count = 0

        while not done and step_count < args.max_episode_steps:
            obs_batch = jax.tree.map(lambda x: x[None, ...] if x is not None else None, obs)
            rstate, logits, _ = agent_apply(params, rstate, obs_batch)
            action = int(jnp.argmax(logits[0]))  # 贪心（logits 已屏蔽非法动作）
            obs, reward, done, truncated, info = env.step(action)
            step_count += 1
            done = done or truncated

        episode_lengths.append(step_count)

        # 协议 info.winner 为字符串："0"=P1（先手）、"1"=P2；超时/平局为空
        # vs 脚本主题（info.modelSeat=0/1）按模型座次判；自对弈（-1）按先手座次判
        winner = str(info.get("winner", ""))
        model_seat = int(info.get("modelSeat", -1))
        model_win = False
        if info.get("timeout"):
            draws += 1
            result = "TIMEOUT"
        elif winner and model_seat >= 0:
            if int(winner) == model_seat:
                wins += 1
                model_win = True
                result = "WIN(model)"
            else:
                losses += 1
                result = "LOSS(theme)"
        elif winner == "0":
            wins += 1
            model_win = True
            result = "WIN(P1)"
        elif winner == "1":
            losses += 1
            result = "LOSS(P2)"
        else:
            draws += 1
            result = "DRAW"

        # 分主题统计（vs 脚本口径：info.theme = 对手主题 key red/green/blue）
        theme = str(info.get("theme", "") or "")
        if vs_script and theme:
            tally = theme_stats.setdefault(theme, [0, 0])
            tally[1] += 1
            if model_win:
                tally[0] += 1

        print(f"  Episode {ep+1}/{num_episodes}: {result}"
              + (f" [theme={theme}]" if vs_script and theme else "")
              + f" (length={step_count})")

    win_rate = wins / num_episodes
    avg_length = float(np.mean(episode_lengths))

    print(f"\n{'='*60}")
    print(f"Evaluation Results ({label}):")
    print(f"  Win Rate: {win_rate*100:.1f}% ({wins}/{num_episodes})")
    print(f"  Losses: {losses}, Draws/Timeouts: {draws}")
    if theme_stats:
        per_theme = ", ".join(
            f"{key} {t[0]}/{t[1]} ({t[0]/t[1]*100:.0f}%)"
            for key, t in sorted(theme_stats.items()) if t[1] > 0
        )
        print(f"  Per-theme: {per_theme}")
    print(f"  Avg Episode Length: {avg_length:.1f}")
    print(f"{'='*60}\n")

    return win_rate, wins, losses, draws, avg_length, theme_stats


def train(args: Args):
    """训练主循环。"""
    run_name = f"{args.exp_name}__{args.seed}__{int(time.time())}"
    log_dir = Path(f"logs/{run_name}")
    log_dir.mkdir(parents=True, exist_ok=True)

    print(f"\n{'='*80}")
    print(f"Run: {run_name}")
    if args.opponent == "selfplay":
        print(f"主题自对弈 PPO（主题整组 vs Cards 随机 30；评估双口径：先手座次胜率 + vs 脚本主题卡组模型胜率，best/早停按后者）")
    else:
        print(f"PPO vs 脚本主题卡组（模型 Cards 随机 30，座次随机=先后手各半；评估口径：模型胜率，按 info.modelSeat 判）")
    print(f"{'='*80}\n")

    # 保存配置
    with open(log_dir / "config.json", "w") as f:
        json.dump(vars(args), f, indent=2)

    # 创建 env（进程惰性启动：首次 reset 才拉起 Unity batchmode）
    assert args.num_envs == 1, "stdio 桥接一个 Unity 进程一场对局，num_envs 必须为 1"
    env = TideEnv(
        unity_path=args.unity_path,
        project_path=args.project_path,
        max_steps=args.max_episode_steps,
        reward_lambda=args.reward_lambda,
        opponent=args.opponent,
        startup_timeout=args.startup_timeout,
        step_timeout=args.step_timeout,
    )
    envs = [env]
    print("Environment created（Unity 进程将在首个 reset 拉起）.\n")

    # 创建模型
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

    def save_model(dir_name, meta):
        """YOLO 风格模型落盘：固定目录覆盖保存（best/ = 评估最优，last/ = 最新）。"""
        save_path = log_dir / dir_name
        save_path.mkdir(exist_ok=True)
        with open(save_path / "params.msgpack", "wb") as f:
            f.write(flax.serialization.to_bytes(train_state.params))
        meta = {"time": time.strftime("%Y-%m-%d %H:%M:%S"), **meta}
        with open(save_path / "meta.json", "w", encoding="utf-8") as f:
            json.dump(meta, f, ensure_ascii=False, indent=2)

    # rollout / bootstrap / 评估共用的 jit 前向（整批编译一次，后续调用微秒级）
    jit_apply = jax.jit(agent.apply)

    # PPO 更新：jit + 整批前向（N 个样本一次算，替代旧版逐样本 eager 前向——
    # 那是单 update 数分钟的瓶颈）。rollout 已记录每步 incoming rstate，
    # 更新期用同一 rstate 重算 logprob/value，与采样口径一致（ratio 初始为 1）。
    @jax.jit
    def ppo_update(ts, b_obs, b_rstates, b_actions, b_old_logprobs, b_advantages, b_returns):
        def loss_fn(params):
            _, logits, values = ts.apply_fn(params, b_rstates, b_obs)
            logp_all = jax.nn.log_softmax(logits)
            logp_a = logp_all[jnp.arange(b_actions.shape[0]), b_actions]
            ratios = jnp.exp(logp_a - b_old_logprobs)
            pg_loss = clipped_surrogate_pg_loss(ratios, b_advantages, args.clip_coef).mean()
            v_loss = mse_loss(b_returns, values).mean()
            entropy = entropy_loss(logits).mean()
            loss = pg_loss + args.vf_coef * v_loss - args.ent_coef * entropy
            return loss, (pg_loss, v_loss, entropy)

        (loss, aux), grads = jax.value_and_grad(loss_fn, has_aux=True)(ts.params)
        ts = ts.apply_gradients(grads=grads)
        return ts, loss, aux

    # 训练循环
    num_updates = args.total_timesteps // (args.num_envs * args.num_steps)
    global_step = 0
    num_updates_reached = 0  # finally 里 exit-save 记录用
    log_every_updates = max(1, args.log_interval // (args.num_envs * args.num_steps))
    # 触发粒度按 update 数对齐：global_step 只取 num_steps 的倍数（256），
    # 直接 % 50000/% 100000 永远不命中（LCM=800000），eval/save 会一直不触发
    eval_every_updates = max(1, round(args.eval_interval / (args.num_envs * args.num_steps)))
    save_every_updates = max(1, round(args.save_interval / (args.num_envs * args.num_steps)))

    episode_returns = deque(maxlen=100)
    episode_lengths = deque(maxlen=100)
    episode_timeouts = 0

    best_win_rate = 0.0
    patience_counter = 0
    loss = 0.0

    print(f"Starting training for {num_updates} updates ({args.total_timesteps:,} timesteps)...")
    print(f"统计落盘: {log_dir / 'stats.jsonl'}（每个 update 一行，可 Get-Content -Wait 实时跟踪）")
    print(f"模型保存: {log_dir / 'best'}（评估刷新纪录时覆盖） / {log_dir / 'last'}（每 {args.save_interval:,} 步覆盖，退出时也存）")
    print(f"首个 update 会触发 jit 编译（约 0.5~2 分钟），之后每个 update 应在秒级\n")

    start_time = time.time()

    try:
        # 初始观测（对局从这里开始，跨更新窗口延续）；carries[4] = 行动方座次
        carries = []
        for e in envs:
            obs, info = e.reset()
            carries.append([obs, init_rstate(args.rnn_type), 0.0, 0, _seat_of(info)])

        for update in range(1, num_updates + 1):
            update_start = time.time()
            num_updates_reached = update

            # ==================== Rollout ====================
            rollout_data = []

            for env_id, e in enumerate(envs):
                obs, rstate, pend_ret, pend_len, seat = carries[env_id]
                data, obs, rstate, rng, episodes, pending = rollout_single_env(
                    jit_apply, train_state.params, e, obs, rstate,
                    args.num_steps, rng, args.rnn_type,
                    pending=(pend_ret, pend_len),
                    log_every_steps=64,
                    seat=seat,
                )
                rollout_data.append(data)
                carries[env_id] = [obs, rstate, pending[0], pending[1], data["next_seat"]]

                for ep_ret, ep_len, timeout in episodes:
                    episode_returns.append(ep_ret)
                    episode_lengths.append(ep_len)
                    episode_timeouts += 1 if timeout else 0

            global_step += args.num_envs * args.num_steps

            # ==================== GAE（逐环境，简化单序列；seats 非空时做自对弈座次修正） ====================
            all_advantages = []
            all_returns = []

            for env_id, data in enumerate(rollout_data):
                next_value = bootstrap_next_value(
                    jit_apply, train_state.params,
                    carries[env_id][0], carries[env_id][1],
                )
                advantages, returns = compute_advantages_simple(
                    data["values"], data["rewards"], data["dones"],
                    next_value, args.gamma, args.gae_lambda,
                    seats=data.get("seats"), next_seat=data.get("next_seat"),
                )
                all_advantages.append(advantages)
                all_returns.append(returns)

            b_obs = stack_obs([obs for data in rollout_data for obs in data["obs"]])
            b_rstates = stack_rstates([r for data in rollout_data for r in data["rstates"]])
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

            # ==================== PPO Update（jit 整批，update 1 编译一次后亚秒级） ====================
            train_state, loss, (pg_loss, v_loss, entropy) = ppo_update(
                train_state, b_obs, b_rstates, b_actions,
                b_old_logprobs, b_advantages, b_returns,
            )

            update_time = time.time() - update_start

            # ==================== 统计落盘（每个 update 一行，stats.jsonl 可 tail） ====================
            mean_return = float(np.mean(episode_returns)) if episode_returns else 0.0
            mean_length = float(np.mean(episode_lengths)) if episode_lengths else 0.0
            elapsed = time.time() - start_time
            sps = global_step / max(elapsed, 1e-8)

            stats = {
                "update": update,
                "step": global_step,
                "loss": float(loss),
                "pg_loss": float(pg_loss),
                "vf_loss": float(v_loss),
                "entropy": float(entropy),
                "ep_ret": mean_return,
                "ep_len": mean_length,
                "episodes": len(episode_returns),
                "timeouts": episode_timeouts,
                "sps": sps,
                "update_time": update_time,
                "elapsed": elapsed,
            }
            with open(log_dir / "stats.jsonl", "a", encoding="utf-8") as f:
                f.write(json.dumps(stats, ensure_ascii=False) + "\n")

            # ==================== 控制台日志 ====================
            if update % log_every_updates == 0 or update == 1:
                print(f"Update {update:5d}/{num_updates} | Step {global_step:8d} | "
                      f"Loss {float(loss):7.3f} | PG {float(pg_loss):7.3f} | "
                      f"VF {float(v_loss):7.3f} | Ent {float(entropy):6.3f} | "
                      f"EpRet {mean_return:7.2f} | EpLen {mean_length:5.1f} | "
                      f"Eps {len(episode_returns)} (超时 {episode_timeouts}) | "
                      f"SPS {sps:5.0f} | Time {update_time:.2f}s", flush=True)

            # ==================== 评估（复用训练 env；进行中的对局被打断） ====================
            if update % eval_every_updates == 0:
                sp_win_rate = None
                if args.opponent == "selfplay":
                    # 口径 1：自对弈先手座次胜率（先后手失衡度量，非棋力）
                    sp_win_rate, *_ = evaluate_greedy(
                        jit_apply, train_state.params, env, args,
                        args.eval_episodes, opponent="selfplay",
                    )
                # 口径 2（自对弈训练时）/ 唯一口径（simpleai 训练时）：vs 脚本主题卡组模型胜率
                win_rate, wins, losses_, draws, avg_length, theme_stats = evaluate_greedy(
                    jit_apply, train_state.params, env, args, args.eval_episodes,
                    opponent="simpleai" if args.opponent == "selfplay" else None,
                    theme=args.eval_theme or None,
                )
                # 评估结果单独一行落盘（stats 写在 eval 前，混行 jsonl 不影响 tail）
                with open(log_dir / "stats.jsonl", "a", encoding="utf-8") as f:
                    f.write(json.dumps({
                        "update": update, "step": global_step, "eval": True,
                        "win_rate_vs_theme": win_rate,
                        "theme_win_rates": {k: (t[0] / t[1]) for k, t in theme_stats.items() if t[1] > 0},
                        "sp_win_rate": sp_win_rate,
                        "avg_length": avg_length,
                    }, ensure_ascii=False) + "\n")

                # 评估抢占了进行中的对局 → 所有 env 强制重开
                for env_id, e in enumerate(envs):
                    obs, info = e.reset()
                    carries[env_id] = [obs, init_rstate(args.rnn_type), 0.0, 0, _seat_of(info)]

                if win_rate > best_win_rate:
                    best_win_rate = win_rate
                    save_model("best", {
                        "step": global_step, "update": update,
                        "win_rate": win_rate, "wins": wins, "draws": draws,
                        "avg_length": avg_length,
                    })
                    print(f"✓ New best model saved → {log_dir / 'best'} (win_rate={win_rate*100:.1f}%)\n", flush=True)

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

            # ==================== 保存 last（固定目录覆盖，不再堆积 checkpoint_N） ====================
            if update % save_every_updates == 0:
                save_model("last", {"step": global_step, "update": update,
                                    "episodes": len(episode_returns)})
                print(f"✓ Last model saved → {log_dir / 'last'} (step {global_step})\n", flush=True)

    except KeyboardInterrupt:
        print("\n[!] 用户中断（Ctrl+C）——正在关闭 Unity 进程...")
        raise
    finally:
        # 任何退出路径都收口：留存最新权重、杀 Unity 进程、清 pidfile（否则下次运行卡工程锁）
        if global_step > 0:
            save_model("last", {"step": global_step, "update": num_updates_reached,
                                "episodes": len(episode_returns), "note": "exit-save"})
        for e in envs:
            e.close()
        print("[✓] Environments closed.", flush=True)

    # 最终评估
    print(f"\n{'='*80}")
    print(f"Final Evaluation (100 episodes):")
    print(f"{'='*80}")
    # 训练 env 已关闭 → 临时再开一个（进程会重新拉起）
    eval_env = TideEnv(
        unity_path=args.unity_path,
        project_path=args.project_path,
        max_steps=args.max_episode_steps,
        opponent=args.opponent,
        startup_timeout=args.startup_timeout,
        step_timeout=args.step_timeout,
    )
    try:
        if args.opponent == "selfplay":
            evaluate_greedy(
                jit_apply, train_state.params, eval_env, args,
                num_episodes=20, opponent="selfplay",
            )
        final_win_rate, wins, losses_, draws, avg_length, _theme_stats = evaluate_greedy(
            jit_apply, train_state.params, eval_env, args, num_episodes=100,
            opponent="simpleai" if args.opponent == "selfplay" else None,
            theme=args.eval_theme or None,
        )
    finally:
        eval_env.close()

    elapsed_total = time.time() - start_time
    skill_label = "vs 脚本主题卡组棋力口径" if args.opponent == "selfplay" else args.opponent
    print(f"\n{'='*80}")
    print(f"Training finished!")
    print(f"  Total time: {elapsed_total/3600:.2f} hours")
    print(f"  Best win rate ({skill_label}): {best_win_rate*100:.1f}%")
    print(f"  Final win rate ({skill_label}): {final_win_rate*100:.1f}%")
    print(f"{'='*80}\n")


if __name__ == "__main__":
    args = Args()

    # 从环境变量读取（如果有）
    args.unity_path = os.environ.get("UNITY_PATH") or args.unity_path
    args.project_path = os.environ.get("TIDE_PROJECT_PATH") or args.project_path

    # 超参覆盖（JSON dict 合并进 Args，键须与 dataclass 字段同名）——
    # verify 短跑 / 超参实验不再临时手改数据类（此前 verify_small 即手改产物）：
    #   $env:TIDE_TRAIN_OVERRIDE='{"exp_name":"verify_reward_fix","total_timesteps":300000}'
    overrides = json.loads(os.environ.get("TIDE_TRAIN_OVERRIDE") or "{}")
    for k, v in overrides.items():
        if not hasattr(args, k):
            raise SystemExit(f"[!] TIDE_TRAIN_OVERRIDE 含未知字段: {k}")
        setattr(args, k, v)

    print(f"\nConfiguration:")
    print(f"  Unity: {args.unity_path or 'auto-detect（UNITY_PATH → Hub 扫描匹配工程版本）'}")
    print(f"  Project: {args.project_path or 'auto-detect'}")
    print(f"  Max episode steps: {args.max_episode_steps}")
    print(f"  Total timesteps: {args.total_timesteps:,}")
    print(f"  Opponent: {args.opponent}")
    print(f"  Eval theme: {args.eval_theme or '三主题随机'}（2026-09-22 临时：绿/蓝脚本待重设计）")
    print(f"  Reward lambda: {args.reward_lambda}（经 reset 协议下发；Unity 奖励势能已剔除手牌）")
    if overrides:
        print(f"  Overrides: {overrides}")
    print(f"  Eval: vs 脚本主题卡组模型胜率（早停目标 {args.target_win_rate*100:.0f}%，分主题 red/green/blue 统计）"
          + ("；自对弈另报先手座次胜率" if args.opponent == "selfplay" else "") + "\n")

    train(args)
