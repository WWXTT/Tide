"""
Tide 完整 PPO 训练脚本（TCP batchmode 桥接；旧 stdio 口径 2026-10 起弃用）。

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
  best 保存与早停按选择口径（2026-11-22 B4 起=18 格矩阵最小 margin，见下方选择口径注释）。
opponent="simpleai" 可切回「打脚本主题」训练（评估只剩单一口径）。

对手池（2026-11-02 C 期，--opponent-pool，默认 current=旧行为不变）：只改「和谁打」，
不动 obs/奖励/网络/卡表/布局。current=同策略自对弈；themes=三预组整组轮换（协议层
opponent=simpleai+theme，Unity 零改）；checkpoints=历史快照池当对手（对手座次由旧参数
驱动，对手步经 learner 掩码剔除出 PPO 批次）；mixed=按 --pool-weights 加权混合
（默认 0.5/0.25/0.25）。--fsp=FSP 口径（快照池均匀随机旧策略、排除最新）。采样点=
每次 reset()，info.opponentSource 标注。评估扩三主题（--eval-theme，默认空=三主题
各 50 局，分主题胜率必印，早停/best 按选择口径：B4（2026-11-22）起默认 18 格矩阵
最小 margin，矩阵无数据回落 6 格/总胜率）。
评估模型侧卡组由 --eval-model-theme 决定（2026-11-21 起**与 --model-theme 解耦**；默认
"all"=三预组都要评一遍 → 6 格标尺全填，绿 bootcamp 这类单主题训练也出 6 格；这类 run 的
红/蓝格 = 未训练 floor，只作参照、不作验收）。
策略：
1. 使用 ygo-agent 的 PPO 损失（jit + 整批前向，rollout 记录 rstate 保证更新期口径一致）
2. 对局跨更新窗口延续（rollout_common），episode 统计真实完结
3. 训练统计每个 update 落盘 stats.jsonl（loss/ep_ret/ep_len/sps，可 tail 实时跟踪）
4. YOLO 风格模型保存：best/（评估刷新纪录时覆盖）+ last/（周期覆盖，退出时也存）

运行（推荐临时工程隔离训练，见 make_train_copy.ps1 / start_training_traincopy.ps1）：
  python tide_rl/train_tide_complete.py
  （Unity.exe 自动发现：UNITY_PATH 环境变量 → Hub 扫描匹配工程版本；
    进程常驻跨局复用；启动前只清「孤儿」残留——有别的活训练实例时报错退出、不杀）

重要约束：TCP 桥接一个 Unity 进程一次只跑一场对局 → num_envs 必须为 1。
"""

import os
import time
import json
import statistics
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
from tide_features import (
    init_rstate, sample_input,
    ACTION_F_TYPE, ACTION_F_TARGET_KIND, ACTION_TYPES, NEW_ACTION_TYPES,
    ACTION_F_NTARGETS, ACTION_F_COST,
    GLOBAL_F_DECISION_CONTEXT, LAYOUT_VERSION,
)
from rollout_common import (
    rollout_single_env,
    bootstrap_next_value,
    compute_advantages_simple,
)
from opponent_pool import (
    CheckpointPool, OpponentSampler, OpponentPoolEnv,
    POOL_MODES, DEFAULT_MIXED_WEIGHTS, THEME_KEYS,
)

# ---- 判据单源（A2 2026-11-24 回到一期训练目标）：基线表/纪元/判定/选点全部迁至
# phase1_criterion.py（纯标准库，训练/评估/认证三方共用）；本文件 import 转发旧名，
# eval_checkpoint 等调用方不受影响。口径详见该文件头注（认证=每格 Δ≥0+中位数≥+15pp+
# 饱和格不退步；best 选点=Δ 最小 Wilson 下界；训练内候选门 Δ≥−5pp 只作预警）。----
from phase1_criterion import (  # noqa: F401 —— 再导出旧名（外部脚本引用不变）
    PRESET_SEAT_BASELINES, RED_CELL_BASELINES, SCRIPT_MATRIX_BASELINES, BASELINE_ERA,
    matrix_cell_threshold, cell_threshold, judge, select_value, candidate_gate, training_gate,
)


def parse_eval_themes(spec: str):
    """eval_theme 字符串 → 主题列表。""→三主题全量（red/green/blue 各跑 eval_episodes 局）；
    "red"→单主题；"red,green"→逗号列表。非法主题名在此响亮失败（打到 Unity 才回落随机就晚了）。"""
    themes = [t.strip() for t in (spec or "").split(",") if t.strip()]
    if not themes:
        return list(THEME_KEYS)
    bad = [t for t in themes if t not in THEME_KEYS]
    if bad:
        raise SystemExit(f"[!] eval_theme 含非法主题 {bad}（可选 {list(THEME_KEYS)}，逗号分隔；空=三主题）")
    return themes


def eval_model_themes(args):
    """评估局模型侧卡组列表（2026-11-21 起与训练口径解耦）。

    - `args.eval_model_theme == "all"`（默认）→ ("red","green","blue")：三预组都评一遍，
      6 格标尺（模型执该套 × 该座位）才会填满；绿 bootcamp（--model-theme green）也用这个，
      此时红/蓝两套是未训练策略，红/蓝格只是 floor 参照，不作验收。
    - 空串 → 回落 args.model_theme（旧行为：单主题训练只评该套，只填两格）。
    - 单个主题名 → 只评该套（调试用）。

    回落顺序：eval_model_theme → model_theme → None（None = 模型侧 Cards 随机 30，旧口径）。
    """
    emt = (getattr(args, "eval_model_theme", "") or "").strip()
    if emt == "all":
        return ("red", "green", "blue")
    return ((emt or getattr(args, "model_theme", "") or "") or None,)


def action_usage_stats(rollout_data):
    """v2 动作类型使用统计（「学会了没有」的可观测信号，进 stats.jsonl）。

    rollout 记录的 obs 是**行动前**观测（actions_ 为 C# 原始特征、未过编码器归一化），
    故可按下标直读所选动作行的 type/targetKind，global_[47] 即该步决策上下文。
    v19 绿套补盲（2026-11-20；**A6 2026-11-24 口径更新**）：同趟追加绿专项信号——
      sacrifice_rows = 牺牲拾取行（type=HeroSkill 且 [10]==-1——A6 起牺牲/蛰伏=可变数量计划，
      拾取步行 [10]=-1、每行恰 1 个牺牲目标；提交行 [10]=0 不计。旧「[10]nTargets>0」
      随 prefix-k 组合行退役废弃）；
      any_gain      = 牺牲目标数合计（=拾取行数——SacrificeHandler 逐目标 +1 Any 的代理量；
                      蛰伏地牌层兑现不走动作行，此值为下界）；
      big_plays     = 大哥出牌行（PlayCard/RespondPlay 且 [5]actionCost>=12——绿 T5 阿格洛斯
                      推导费 14，与沃土 6/归根 8 清晰分档）。
    返回 (type_counts, new_type_frac, targeted_frac, response_step_frac, green_stats dict)。
    """
    type_counts = {}
    n = new_type = targeted = response_steps = 0
    sacrifice_rows = any_gain = big_plays = 0
    for data in rollout_data:
        obs_list = data["obs"]
        actions = data["actions"]
        # 对手池（2026-11-02 C 期）：快照对手座次的动作不计入「学会了没有」——那是旧策略的行为
        learner = data.get("learner")
        pairs = zip(obs_list, actions) if learner is None else (
            (o, a) for o, a, m in zip(obs_list, actions, learner) if m)
        for obs, act in pairs:
            n += 1
            row = obs["actions_"][act]
            t = int(row[ACTION_F_TYPE])
            name = ACTION_TYPES.get(t, f"type{t}")
            type_counts[name] = type_counts.get(name, 0) + 1
            if t in NEW_ACTION_TYPES:
                new_type += 1
            if int(row[ACTION_F_TARGET_KIND]) == 1:
                targeted += 1
            if int(obs["global_"][GLOBAL_F_DECISION_CONTEXT]) == 1:
                response_steps += 1
            # 绿专项（A6 2026-11-24）：牺牲拾取行 = HeroSkill 且 [10]==-1（可变数量计划拾取步，
            # 每行恰 1 个牺牲目标；提交行 [10]=0 不计）；any_gain 代理量=拾取行数
            if t == 5 and int(row[ACTION_F_NTARGETS]) == -1:
                sacrifice_rows += 1
                any_gain += 1
            # 绿专项：大哥出牌（阿格洛斯 14 费档——actionCost>=12 与中费曲线分档）
            if t in (0, 8) and float(row[ACTION_F_COST]) >= 12.0:
                big_plays += 1
    green = {
        "sacrifice_rows": sacrifice_rows,
        "sacrifice_frac": sacrifice_rows / n if n else 0.0,
        "any_gain": any_gain,
        "big_plays": big_plays,
        "big_play_frac": big_plays / n if n else 0.0,
    }
    if n == 0:
        return {}, 0.0, 0.0, 0.0, green
    return type_counts, new_type / n, targeted / n, response_steps / n, green


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
    # v2c（2026-09-30 C 期动作空间）：效果时机+目标选择+响应窗口进动作空间，
    # 旧 checkpoint/fixture/onnx 全作废（动作编码器输入 6→8、global 36→49）。
    exp_name: str = "tide_ppo_selfplay_v2c"
    seed: int = 42
    learning_rate: float = 2.5e-4
    anneal_lr: bool = True  # 学习率线性衰减

    # TCP 桥接一个 Unity 进程一场对局 → 恒为 1（评估复用同一 env）
    num_envs: int = 1
    num_steps: int = 256  # 每次 rollout 步数（对局跨窗口延续）
    # 2026-09-29 起 4 epoch 复用同批样本：此前恒 1，ratio 恒为 1、clip 永不触发，
    # PPO 实际退化为单步归一化优势策略梯度。epoch1 ratio=1，epoch≥2 clip 生效。
    # num_envs=1 无环境轴可切 minibatch，全批 256 样本直接多轮（梯度步很小，够用）。
    update_epochs: int = 4

    # 2026-09-29 对齐 ygo-agent 口径：零和回合制 + 终局 ±1 + 势能塑形（γ=1 下严格
    # 策略不变），γ=1.0 不再对未来终局奖励打折（旧 0.99 无依据地压低长线回报）。
    gamma: float = 1.0
    gae_lambda: float = 0.95
    clip_coef: float = 0.2
    dual_clip_coef: float = 3.0  # JueWu dual-clip（负优势下 ratio 下限）——ygo 默认开
    vloss_clip: float = 1.0      # 价值损失单样本封顶 min(0.5·SE, clip)——ygo 口径，防离群回报拉飞 critic
    ent_coef: float = 0.05  # 熵系数（0.01 时点积 actor 熵 7 个 update 内塌零，已加温度修复后仍提到 0.05 保险）
    vf_coef: float = 0.5
    max_grad_norm: float = 0.5
    use_upgo: bool = True  # UPGO 优势项（ygo 默认开；自对弈座次修正口径下生效，不稳可关掉消融）

    total_timesteps: int = 2_000_000
    eval_interval: int = 50_000  # 每 5 万步评估
    eval_episodes: int = 50  # 每次评估局数（2026-09-22 20→50：20 局二项噪声 σ≈0.11，best 选点运气成分过大）
    # 每 2 万步保存（2026-11-18 循环治理 §6：run 1794909970 一次崩溃丢 88k 步 ≈2.6h——
    # 10 万间隔过大；2 万=崩溃损失上限 ~35min，保存开销占比可忽略）
    save_interval: int = 20_000
    log_interval: int = 2_560  # 每 2560 步打印（对齐 update 粒度）

    # Tide 特定
    # v2 1000→2000：响应窗口决策点使每局步数变长（停靠仅在候选>0 时发生，增长有界）
    max_episode_steps: int = 2000  # 单局步数上限（超时判负）
    # 塑形 λ（2026-09-22 起经 reset 协议真正下发到 Unity）。旧口径 λ=0.05 且势能含手牌：
    # 双方各自整局塑形累计 ≈ +3.1 淹没终局 ±1，自对弈收敛到拖局（局长 90→267、
    # 对脚本胜率 0.35→0.1）——现降 10 倍，且 Unity 侧奖励势能已剔除手牌。
    reward_lambda: float = 0.005
    # 对手位口径（A1 2026-11-24 回到一期训练目标起 CLI --opponent 显式指定、优先于 TIDE_TRAIN_OVERRIDE）：
    # selfplay = 主题自对弈（**非一期验收口径**，诊断/掺卡实验用）；simpleai = 模型 vs 脚本主题卡组
    #（座次随机=先后手各半，**一期只认这个**——preflight/certify 硬闸断言）。默认仍 selfplay 保持
    # 旧行为不变，但启动横幅会点名警示；run 名/config 判据块随口径落盘（漂移第一眼可见）。
    opponent: str = "selfplay"
    # 模型侧卡组主题（2026-10-30 起红·快攻镜像训练口径；2026-11-17 一期课程扩为三套）：
    # vs 脚本口径下模型侧固定该主题整组（red/green/blue）；**"all"=每局从三预组随机抽一套**
    # （一期目标：AI 执三套都比脚本强）；空 = Cards 随机 30（旧行为）。
    # CLI --model-theme，评估 options 同步下发。
    model_theme: str = ""
    # 掺卡口径主题（2026-11-17 阶段二，AI训练说明 §0.1）：命中主题一侧卡组 = 整组每种 −1 张 +
    # 训练池随机补足（红 30→25+5）；vs 脚本=模型侧、自对弈=一侧（另一侧主题整组/随机 30 各半）；
    # 优先级高于 model_theme；空 = 旧行为零差。CLI --model-doped，info.modelDoped 回显断言。
    model_doped: str = ""
    # 评估基准主题（2026-11-02 C 期起三主题口径）：空串 = red/green/blue 三主题各跑
    # eval_episodes 局、输出分主题胜率（早停/best 仍按总胜率）；"red" 单主题；
    # "red,green" 逗号列表。CLI --eval-theme。旧默认 "red"（2026-09-22 临时只评红，
    # 绿/蓝脚本重设计完成后已切回三主题）。
    eval_theme: str = ""
    # 评估局模型侧卡组（2026-11-21，AI训练说明 §3.4）：**与训练口径解耦**——
    # "all"（默认）=三预组都评一遍，6 格标尺（模型执该套 × 该座位）才填得满；
    # 绿 bootcamp（--model-theme green）也走这个，此时红/蓝格=未训练 floor，只作参照、
    # 不作验收。空串=回落 model_theme（旧行为，单主题训练只出两格）；单主题名=只评该套。
    # CLI --eval-model-theme；解析在 eval_model_themes()。
    eval_model_theme: str = "all"
    # 热启动（2026-11-20，环境修正批次）：从既有 params.msgpack 载入权重继续训练
    # （不恢复优化器状态——Adam 从零累计）。CLI --init-params，接受 run 目录（取其 params.msgpack）
    # 或直接给 .msgpack 文件。用途：后手护甲规则补入（AI训练说明 §4.6）后保留权重续训。
    init_params: str = ""

    # 对手池（2026-11-02 C 期）：只改「和谁打」，不动 obs/奖励/网络/卡表/布局。
    # current=旧行为（selfplay 同策略）；themes=三预组整组轮换（SimpleAI+AutoMatch
    # 配套策略脚本）；checkpoints=历史快照池当对手（对手座次由旧参数驱动）；
    # mixed=三来源按 pool_weights 加权混合。采样点=每次 reset()，info.opponentSource 标注。
    opponent_pool: str = "current"
    pool_weights: tuple = DEFAULT_MIXED_WEIGHTS  # mixed 权重（current,themes,checkpoints）
    checkpoint_pool_size: int = 8    # 快照池容量（环形保留最近 N 份；每个评估点入池一次）
    checkpoint_weighting: str = "uniform"  # 快照采样权重：uniform / recency（越新越易抽中）
    fsp: bool = False                # fictitious self-play：对手=快照池均匀随机旧策略（排除最新）
    # L3-A 循环空转步剪枝（2026-11-18 循环治理 §5.4）：Unity 侧 L1/L2 收口的回合，
    # 其决策步不进 PPO 批次（learner 掩码置 0，与对手步剔除同通道；GAE 仍在全序列算）。
    # CLI --no-prune 关闭（回滚开关——环境异常误报时可退回旧行为）。
    prune_loop_steps: bool = True
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


def evaluate_greedy(agent_apply, params, env, args, num_episodes=20, opponent=None, theme=None,
                    model_theme=None, seat=None, seed_base=None):
    """贪心策略评估（复用训练 env——TCP 桥接一进程一局，不能另开）。

    opponent=None 时用 args.opponent（训练口径）；自对弈训练中途可传 "simpleai"
    切换评估对手（reset 协议按次传 opponent，无需另开 env）。
    theme 可钉死 vs 脚本口径的对手主题（args.eval_theme，2026-09-22 临时只评红）。
    model_theme 可按次覆盖模型侧卡组主题（缺省回落 args.model_theme——红·快攻镜像
    口径评估与训练同卡组；仅 vs 脚本口径下发，自对弈 reset 不带该字段）。
    seat/seed_base（评估协议 v2，2026-11-22 配对终版）：seat=0/1 钉模型座次（仅 vs 脚本
    口径——自对弈 modelSeat=-1 无座可钉；座位 24/24 均分由调用方拆两半实现）；
    seed_base 钉每局种子，第 ep 局 = seed_base+ep——同 (seed_base, ep, seat) 跨检查点
    = 同一批对局（真配对：SimpleAI 无随机源、技能卡挑选确定性、引擎双随机流同种子
    重播，对局差异只来自模型决策）。缺省 None = 旧行为掷币+环境流。

    对手位口径（2026-09-21 主题卡组迁移）：
    - simpleai：模型（Cards.json 随机 30）vs 脚本主题卡组（SimpleAI+AutoMatch 主题整组），
      座次随机，按 info.modelSeat 判模型胜负——真实棋力指标；info.theme（red/green/blue）
      另做分主题统计（对手是哪套主题）；
    - selfplay：双方同一策略按座次报告——P1 恒先手，即「先手座次胜率」（衡量先手优势）。

    返回 (win_rate, wins, losses, draws, avg_length, theme_stats, cell_stats)；
    theme_stats = {theme_key: [wins, losses, draws, games, len_sum]}（仅 simpleai 口径有值，
    selfplay 空 dict；len_sum=该主题累计决策步，B2 2026-11-22——theme 行 avg_len 真值落盘用）；
    cell_stats = {(theme_key, model_seat): [wins, games, len_sum]}（2026-11-17 6 格口径：vs 脚本且
    info.modelSeat=0/1 时逐格累计——P1/P2 × 对手主题；selfplay 空 dict；len_sum 同上）。
    """
    vs_script = (opponent or args.opponent) == "simpleai"
    label = "vs 脚本主题卡组（模型胜率）" if vs_script else "self-play（先手座次胜率）"

    print(f"\n{'='*60}")
    print(f"Evaluating {num_episodes} episodes (greedy, {label}"
          + (f", theme={theme}" if theme else "") + ")")
    print(f"{'='*60}")

    wins = losses = draws = 0
    episode_lengths = []
    theme_stats = {}  # theme key -> [wins, losses, draws, games, len_sum]（vs 脚本口径）
    cell_stats = {}   # (theme key, model_seat) -> [wins, games, len_sum]（6 格口径，vs 脚本）

    for ep in range(num_episodes):
        # 按次传对手位（2026-09-11 修复：此前裸调用 env.reset()，options 缺省回落
        # self.opponent=selfplay——自对弈训练期间"vs SimpleAI"评估实际在跑自对弈，
        # modelSeat=-1 落进 winner=="0" 分支，统计的是 P1 座次胜率而非模型棋力，
        # best/早停全程选在幻影指标上）；theme 钉死对手主题（临时只评红）；
        # modelTheme 钉死模型侧卡组主题（红·快攻镜像口径，与训练同卡组）
        options = {"opponent": opponent or args.opponent}
        if theme:
            options["theme"] = theme
        eff_model_theme = model_theme or args.model_theme
        if vs_script and eff_model_theme:
            options["modelTheme"] = eff_model_theme
        # 阶段二掺卡口径（2026-11-17）：评估随训练口径下发（vs 脚本位才有模型侧卡组概念）
        if vs_script and getattr(args, "model_doped", ""):
            options["modelDoped"] = args.model_doped
        # 评估协议 v2（2026-11-22 配对终版）：seat 钉座次 / seed_base 逐局钉种子
        # （env 层收到响应后断言 info 回显一致——对端 Unity 过旧时报清晰错误）
        if vs_script and seat is not None:
            options["modelSeat"] = int(seat)
        if seed_base is not None:
            options["seed"] = int(seed_base) + ep
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

        # 分主题统计（vs 脚本口径：info.theme = 对手主题 key red/green/blue）。
        # 注意不能重名遮蔽入参 theme——曾因此从第 2 局起把上一局主题写进 reset 请求
        # （单主题评估自洽无症状，多主题恢复时会静默钉死单一主题）。
        ep_theme = str(info.get("theme", "") or "")
        if vs_script and ep_theme:
            tally = theme_stats.setdefault(ep_theme, [0, 0, 0, 0, 0])
            tally[3] += 1
            tally[4] += step_count  # B2：theme 行 avg_len 真值（决策步）
            if model_win:
                tally[0] += 1
            elif result.startswith("LOSS"):
                tally[1] += 1
            else:
                tally[2] += 1

        # 6 格累计（2026-11-17 一期课程）：vs 脚本口径按 (模型执的预组, 模型座次) 拆格——
        # 模型卡组=本局实际下发的 modelTheme（"all" 时每局随机；info.modelTheme 回显命中 key）。
        # 超时/平局计入 games 不计 wins（胜率被压低与主题口径一致）。
        ep_model_theme = str(info.get("modelTheme", "") or eff_model_theme or "")
        if vs_script and ep_model_theme in ("red", "green", "blue") and model_seat in (0, 1):
            cell = cell_stats.setdefault((ep_model_theme, model_seat), [0, 0, 0])
            cell[1] += 1
            cell[2] += step_count  # B2：cell 行 avg_len 真值（决策步）
            if model_win:
                cell[0] += 1

        print(f"  Episode {ep+1}/{num_episodes}: {result}"
              + (f" [theme={ep_theme}]" if vs_script and ep_theme else "")
              + f" (length={step_count})")

    win_rate = wins / num_episodes
    avg_length = float(np.mean(episode_lengths))

    print(f"\n{'='*60}")
    print(f"Evaluation Results ({label})" + (f" [theme={theme}]" if theme else "") + ":")
    print(f"  Win Rate: {win_rate*100:.1f}% ({wins}/{num_episodes})")
    print(f"  Losses: {losses}, Draws/Timeouts: {draws}")
    if theme_stats:
        per_theme = ", ".join(
            f"{key} {t[0]}/{t[3]} ({t[0]/t[3]*100:.0f}%)"
            for key, t in sorted(theme_stats.items()) if t[3] > 0
        )
        print(f"  Per-theme: {per_theme}")
    if cell_stats:
        per_cell = ", ".join(
            f"{k[0]}/P{k[1]+1} {v[0]}/{v[1]}"
            for k, v in sorted(cell_stats.items()) if v[1] > 0
        )
        print(f"  Per-cell(6格): {per_cell}")
    print(f"  Avg Episode Length: {avg_length:.1f}")
    print(f"{'='*60}\n")

    return win_rate, wins, losses, draws, avg_length, theme_stats, cell_stats


def evaluate_greedy_multi(agent_apply, params, env, args, num_episodes, opponent=None, themes=None,
                          model_themes=None):
    """三主题（或多主题）评估聚合器（2026-11-02 C 期）：每个主题各跑 num_episodes 局
    （贪心），输出分主题胜率 + 总胜率。早停/best 选点消费**总胜率**，但分主题必印
    ——否则看不出对某主题的退化被总胜率掩盖。

    2026-11-17 一期课程：模型侧要执**三套预组**（red/green/blue）分别评测——
    model_themes 缺省时走 eval_model_themes(args)（2026-11-21：由 args.eval_model_theme
    决定，默认 "all"=三套，**不再**跟随 args.model_theme——否则绿 bootcamp 只评出两格）。
    多模型主题时每组合局数 = max(10, num_episodes // len(model_themes))。
    **注意**：多模型主题 × 多对手主题时总对局数 = per_combo × 对手主题数 × 模型卡组数
    （3×3×16=144 局），不是 num_episodes——调用方报局数一律用 wins+losses+draws 实计。

    返回 (总胜率, wins, losses, draws, avg_length, theme_stats, cell_stats, matrix_stats)；
    theme_stats = {对手主题: [wins, losses, draws, games, len_sum]}；
    cell_stats = {(模型执的预组, model_seat): [wins, games, len_sum]}（6 格口径）；
    matrix_stats = {(模型执的预组, 对手主题): [wins, games, P1胜, P1局, P2胜, P2局, len_sum]}
    （2026-11-21 矩阵口径：联合维度在 6 格/主题两切片里被聚合丢弃——停训后矩阵评估
    实证聚合 plateau 可能掩盖对局级大变动〔红镜像 0→54%〕；B4 2026-11-22 起选点改按
    18 格矩阵 margin（见 selector 注释），本口径不再只是观察；len_sum=B2 矩阵行 avg_len
    真值落盘用，单位=决策步）。
    """
    themes = themes or list(THEME_KEYS)
    if model_themes is None:
        model_themes = eval_model_themes(args)
    per_combo = num_episodes
    if len(model_themes) > 1:
        # C3（2026-11-22 红绿0胜归因计划）：每 (模型卡组×对手主题) 组合局数下限 10→48——
        # 18 格矩阵每格（=对×座位）≈24 局，B4 selector 的 margin 不再被 ±17pp 单格噪声抖动；
        # 默认 eval_episodes=50 下实际取 48（3×3×48=432 局/次评估，时长约 3× 旧 144 局口径）。
        per_combo = max(48, num_episodes // len(model_themes))
    total_games = wins = losses = draws = 0
    all_lengths = []
    theme_stats = {}
    cell_stats = {}
    matrix_stats = {}

    for mt in model_themes:
        for theme in themes:
            _, w, l, d, avg_len, ts, cells = evaluate_greedy(
                agent_apply, params, env, args, num_episodes=per_combo,
                opponent=opponent, theme=theme, model_theme=mt,
            )
            wins += w
            losses += l
            draws += d
            total_games += per_combo
            all_lengths.append(avg_len * per_combo)
            for key, t in ts.items():  # 单主题调用内即本 theme；聚合防漏
                agg = theme_stats.setdefault(key, [0, 0, 0, 0, 0])
                for i in range(5):
                    agg[i] += t[i]
            for key, c in cells.items():
                agg = cell_stats.setdefault(key, [0, 0, 0])
                agg[0] += c[0]
                agg[1] += c[1]
                agg[2] += c[2]  # B2：len_sum 聚合
            # 矩阵口径（2026-11-21）：固定 (模型卡组, 对手主题) 对的分座位胜负——
            # 本调用 cells 键恒为 (mt, 0)/(mt, 1)，原样保留即 18 格联合维度
            p1 = list(cells.get((mt, 0), [0, 0, 0]))
            p2 = list(cells.get((mt, 1), [0, 0, 0]))
            matrix_stats[(mt, theme)] = [w, per_combo, p1[0], p1[1], p2[0], p2[1],
                                         avg_len * per_combo]  # 末位=B2 矩阵行 len_sum

    win_rate = wins / total_games
    avg_length = float(np.sum(all_lengths) / total_games)
    per_theme = " | ".join(
        f"{key} {t[0]}/{t[3]} ({t[0]/t[3]*100:.1f}%)"
        for key, t in sorted(theme_stats.items()) if t[3] > 0
    )
    print(f"{'='*60}")
    print(f"Multi-theme Evaluation（模型卡组 {model_themes}；每组合 {per_combo} 局）:")
    print(f"  Per-theme Win Rate: {per_theme}")
    print(f"  TOTAL Win Rate: {win_rate*100:.1f}% ({wins}/{total_games}) "
          f"(losses {losses}, draws/timeouts {draws})")
    print(f"  Avg Episode Length: {avg_length:.1f}")
    # 6 格口径块（2026-11-17 一期课程，AI训练说明 §0.2）：模型执三套预组 × 先/后手，逐格对基线+15pp
    if cell_stats:
        print("  6 格口径（模型执该预组 × 该座位；Δ 口径：门槛=同格脚本基线+15pp 不封顶，AI训练说明 §0.2）:")
        for (tk, seat), c in sorted(cell_stats.items()):
            if c[1] == 0:
                continue
            thr = cell_threshold(tk, seat)
            wr = c[0] / c[1]
            mark = "--"
            if thr is not None:
                mark = "达标" if wr >= thr else "未达"
                print(f"    [{mark}] {tk}/P{seat+1}: {wr*100:.0f}% ({c[0]}/{c[1]})"
                      + (f" ≥ {thr*100:.0f}%" if thr is not None else ""))
    # 矩阵口径块（2026-11-21）：模型执色 × 对手主题，逐行打印对局级胜率——
    # 聚合 TOTAL 平不代表结构稳定（例：红镜像 +50pp 与绿 -10pp 互相抵消）
    if matrix_stats:
        print("  矩阵口径（模型执色 × 对手主题）:")
        for mt in model_themes:
            parts = []
            for opp in themes:
                m = matrix_stats.get((mt, opp))
                parts.append(f"vs{opp} {m[0]}/{m[1]}" if m else f"vs{opp} —")
            print(f"    执{mt}: " + " | ".join(parts))
    print(f"{'='*60}\n", flush=True)
    return win_rate, wins, losses, draws, avg_length, theme_stats, cell_stats, matrix_stats


def train(args: Args):
    """训练主循环。"""
    run_name = f"{args.exp_name}__{args.seed}__{int(time.time())}"
    log_dir = Path(f"logs/{run_name}")
    log_dir.mkdir(parents=True, exist_ok=True)

    print(f"\n{'='*80}")
    print(f"Run: {run_name}")
    # A1（2026-11-24 回到一期训练目标）口径横幅：run 名即口径；selfplay 未掺卡点名「非一期验收口径」
    print(f"口径横幅: opponent={args.opponent}"
          + ("（一期验收口径）" if args.opponent == "simpleai" else "（自对弈——非一期验收口径）"))
    if args.opponent == "selfplay":
        print(f"主题自对弈 PPO（主题整组 vs Cards 随机 30；评估双口径：先手座次胜率 + vs 脚本主题卡组模型胜率，best/早停按后者）")
        if not args.model_doped:
            print(f"[口径警示] 自对弈口径——非一期验收口径（一期只认 --opponent simpleai；诊断用）")
    else:
        print(f"PPO vs 脚本主题卡组（模型 Cards 随机 30，座次随机=先后手各半；评估口径：模型胜率，按 info.modelSeat 判）")
    # 对手池摘要（2026-11-02 C 期）：current=旧行为零差异；非默认时列出权重/口径
    if args.opponent_pool == "current" and not args.fsp:
        print(f"对手池: current（默认，等价旧 selfplay——同策略自对弈）")
    else:
        weights_desc = (f"，权重 current/themes/checkpoints = "
                        + "/".join(f"{w:.2f}" for w in args.pool_weights)) if args.opponent_pool == "mixed" else ""
        print(f"对手池: {args.opponent_pool}{weights_desc}（每次 reset 采样，info.opponentSource 标注）")
    if args.fsp:
        print(f"FSP: true（对手=快照池均匀随机旧策略，排除最新一份；日志标注 fsp=true）")
    print(f"快照池: 容量 {args.checkpoint_pool_size}（每个评估点入池一次），采样权重 {args.checkpoint_weighting}")
    print(f"{'='*80}\n")

    # 保存配置（A1 2026-11-24 回到一期训练目标：附判据块——opponent/criterion_era/layout_version
    # 三字段随 run 落盘；认证时三者与 run 目录名/代码常量一致才认。era 由 phase1_criterion.
    # BASELINE_ERA 单源供值——B2 基线重测后同批换新纪元，旧 run 的 config 即刻失配可辨）
    cfg = dict(vars(args))
    cfg["criterion_era"] = BASELINE_ERA
    cfg["layout_version"] = LAYOUT_VERSION
    with open(log_dir / "config.json", "w") as f:
        json.dump(cfg, f, indent=2)

    # 创建 env（进程惰性启动：首次 reset 才拉起 Unity batchmode）
    assert args.num_envs == 1, "TCP 桥接一个 Unity 进程一场对局，num_envs 必须为 1"
    env = TideEnv(
        unity_path=args.unity_path,
        project_path=args.project_path,
        max_steps=args.max_episode_steps,
        reward_lambda=args.reward_lambda,
        opponent=args.opponent,
        model_theme=args.model_theme,
        model_doped=args.model_doped,
        startup_timeout=args.startup_timeout,
        step_timeout=args.step_timeout,
    )
    # 对手池壳（2026-11-02 C 期）：reset 无 options 时由池采样对手来源并标注
    # info.opponentSource；评估等显式 options 直通不受影响。current 模式零行为差。
    ckpt_pool = CheckpointPool(max_size=args.checkpoint_pool_size)
    sampler = OpponentSampler(
        args.opponent_pool, np.random.default_rng(args.seed + 1), ckpt_pool,
        weights=tuple(args.pool_weights), fsp=args.fsp,
        ckpt_weighting=args.checkpoint_weighting,
    )
    pool_env = OpponentPoolEnv(env, sampler)
    envs = [pool_env]
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

    # 热启动（2026-11-20）：载入既有权重继续训练——环境修正（后手护甲，AI训练说明 §4.6）后
    # 不丢已学策略。优化器状态不落盘，Adam 从零累计；lr 退火按本次 total_timesteps 重新计。
    if args.init_params:
        _init_path = Path(args.init_params)
        if _init_path.is_dir():
            _init_path = _init_path / "params.msgpack"
        if not _init_path.exists():
            raise SystemExit(f"[!] --init-params 找不到参数文件：{_init_path}")
        with open(_init_path, "rb") as f:
            params = flax.serialization.from_bytes(params, f.read())
        print(f"[init] 热启动：已载入参数 {_init_path}（优化器状态从零开始）", flush=True)

    # 快照池种子：初始（随机）策略入池——checkpoints/fsp 从第 0 步起就有历史对手可打；
    # 结构模板绑定当前参数树（网络结构冻结，树形不会漂移）
    sampler.bind(params)
    ckpt_pool.add("init", params)

    # 优化器
    if args.anneal_lr:
        num_updates = args.total_timesteps // (args.num_envs * args.num_steps)
        # 2026-11-20 修复（原写法 transition_steps=num_updates 是 bug）：optax 的 count 是
        # **优化器步数**，而每个 update 在 PPO 段连调 update_epochs 次 apply_gradients（下方
        # `for _ in range(args.update_epochs)`）——原参数让 lr 在 total_timesteps/update_epochs
        # 处就归零，恰好是总步数 25%：300k run → u294/step 75,264；2M run → u1954/step 500,224。
        # 此后 Adam 步长 ×0，策略冻结但训练循环照跑：归零后 pg_loss≈1e-8、approx_kl≈1e-10、
        # clipfrac=0（三个 run 同症状，评估胜率三个点一条直线）。
        # transition_steps 必须按优化器步数计（num_updates × update_epochs），退火才覆盖整程。
        lr_schedule = optax.linear_schedule(
            init_value=args.learning_rate,
            end_value=0.0,
            transition_steps=num_updates * args.update_epochs,
        )
        optimizer = optax.chain(
            optax.clip_by_global_norm(args.max_grad_norm),
            optax.adam(learning_rate=lr_schedule, eps=1e-5),  # ygo 口径 eps（默认 1e-8 易受小梯度噪声干扰）
        )
    else:
        optimizer = optax.chain(
            optax.clip_by_global_norm(args.max_grad_norm),
            optax.adam(learning_rate=args.learning_rate, eps=1e-5),
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
            logratio = logp_a - b_old_logprobs
            ratios = jnp.exp(logratio)
            pg_loss = clipped_surrogate_pg_loss(ratios, b_advantages, args.clip_coef,
                                                dual_clip_coef=args.dual_clip_coef).mean()
            # 价值损失单样本封顶（ygo 口径的 cap，非 PPO value-clip）
            v_loss = jnp.minimum(mse_loss(b_returns, values), args.vloss_clip).mean()
            entropy = entropy_loss(logits).mean()
            loss = pg_loss + args.vf_coef * v_loss - args.ent_coef * entropy
            # 诊断量（不进梯度）：k3 KL 估计（恒非负）与 clip 触发率——epoch≥2 才有意义
            approx_kl = ((ratios - 1.0) - logratio).mean()
            clipfrac = (jnp.abs(ratios - 1.0) > args.clip_coef).mean()
            return loss, (pg_loss, v_loss, entropy, approx_kl, clipfrac)

        (loss, aux), grads = jax.value_and_grad(loss_fn, has_aux=True)(ts.params)
        ts = ts.apply_gradients(grads=grads)
        return ts, loss, aux

    # eval.jsonl 追加行（2026-11-02 训练管线加固）：每次评估每个口径一行——
    # win_rate 均为该口径自身数值；best 选点只消费 vs SimpleAI（vs_script）口径，
    # 自对弈先手座次胜率（selfplay_first_seat）只记录不参与选点；
    # extra（2026-11-17）：附加字段并进行内（6 格行的 threshold/pass 用）
    # 口径族：vs_script（对手主题聚合）/ vs_script_cell（6 格=执色×座位）/
    # vs_script_matrix（2026-11-21 矩阵=执色×对手主题，extra.p1/p2 分座位）。
    # avg_len（B2，2026-11-22）：全口径真值落盘（该行平均决策步）——此前 theme/cell/matrix
    # 行恒 0.0，最有诊断价值的信号（红镜像 74 步→26 步=「打法变了」）只在控制台；单位=决策步。
    def write_eval_row(metric, theme, wr, wins, losses, draws, avg_len, episodes, update_no,
                       extra=None):
        row = {
            "step": global_step, "update": update_no, "metric": metric, "theme": theme,
            "win_rate": wr, "wins": wins, "losses": losses, "draws": draws,
            "avg_len": round(float(avg_len), 1), "episodes": episodes,
        }
        if extra:
            row.update(extra)
        with open(log_dir / "eval.jsonl", "a", encoding="utf-8") as f:
            f.write(json.dumps(row, ensure_ascii=False) + "\n")

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
    green_episode_lengths = deque(maxlen=100)  # v19 绿套补盲：绿局（模型侧卡组=green）局长
    episode_timeouts = 0
    # 塑形在回报中的占比（2026-11-02）：每完结局 |非终局奖励| / (|非终局| + |终局|)，
    # 近 100 局均值——λ·ΔΦ 分量逼近/超过终局 ±1 时该值 → 0.5+，是「塑形淹没终局」
    # 的直接监控量（2026-09-11 事故局 λ=0.02 时 ep_ret 冲到 +161 即此症）
    episode_shaping_shares = deque(maxlen=100)

    # best 选点初值：**必须比任何合法选择值都小**。选择值在 6 格口径下 = min(格胜率 − 门槛)，
    # 只要有任一格没过门槛就是负数（2026-11-20 实测 −0.28 ~ −0.59）；初值写 0.0 会让
    # `select_rate > best_win_rate` 恒不成立 → best 永不落盘（2026-11-17 起 4 个 run 全无 best/）。
    # 旧口径（镜像/总胜率，恒 ≥0）不受影响：−inf 只是让首个评估点无条件存一次。
    best_win_rate = -float("inf")
    patience_counter = 0
    loss = 0.0

    print(f"Starting training for {num_updates} updates ({args.total_timesteps:,} timesteps)...")
    print(f"统计落盘: {log_dir / 'stats.jsonl'}（每个 update 一行，可 Get-Content -Wait 实时跟踪）")
    print(f"评估落盘: {log_dir / 'eval.jsonl'}（每次评估每口径一行；best 只按 vs_script 口径选点）")
    print(f"模型保存: {log_dir / 'best'}（评估刷新纪录时覆盖） / {log_dir / 'last'}（每 {args.save_interval:,} 步覆盖，退出时也存）")
    print(f"首个 update 会触发 jit 编译（约 0.5~2 分钟），之后每个 update 应在秒级\n")

    start_time = time.time()

    try:
        # 初始观测（对局从这里开始，跨更新窗口延续）；carries[4] = 行动方座次
        carries = []
        for e in envs:
            obs, info = e.reset()
            # v19 绿套补盲：首局主题登记（rollout 内 reset 后由 rollout_single_env 维护）
            try:
                e.carry_ep_theme = str(getattr(e, "_last_model_theme", "") or "")
            except AttributeError:
                pass
            carries.append([obs, init_rstate(args.rnn_type), 0.0, 0, 0.0, _seat_of(info)])

        for update in range(1, num_updates + 1):
            update_start = time.time()
            num_updates_reached = update

            # 本 update 窗口内完结对局的 W/L/D（视角口径与 evaluate_greedy 一致：
            # vs 脚本训练按模型座次判、自对弈训练按先手座次判）
            win_window = loss_window = draw_window = 0
            # 2026-11-23 B4 行为仪表（用户口径）：本窗口模型侧「牺牲 + 回收」与 14 费大哥落地。
            # 数据源=终局 info 的 mSac/mRec/mChain/mAgros/mAgrosTurn（driver TrackModelBehavior，
            # Apply 成功才计）——与 action_usage_stats 的「选中了哪行」口径互补。
            behav_win = {"games": 0, "green_games": 0, "sac": 0, "rec": 0, "chain_games": 0,
                         "agros": 0, "agros_games": 0, "agros_t5_games": 0, "agros_turn_min": 0}

            # ==================== Rollout ====================
            rollout_data = []

            for env_id, e in enumerate(envs):
                obs, rstate, pend_ret, pend_len, pend_shaping, seat = carries[env_id]
                data, obs, rstate, rng, episodes, pending = rollout_single_env(
                    jit_apply, train_state.params, e, obs, rstate,
                    args.num_steps, rng, args.rnn_type,
                    pending=(pend_ret, pend_len, pend_shaping),
                    log_every_steps=64,
                    seat=seat,
                    prune_loop_steps=args.prune_loop_steps,
                )
                rollout_data.append(data)
                carries[env_id] = [obs, rstate, pending[0], pending[1], pending[2], data["next_seat"]]

                for ep in episodes:
                    episode_returns.append(ep.ret)
                    episode_lengths.append(ep.len)
                    if getattr(ep, "theme", "") == "green":
                        green_episode_lengths.append(ep.len)  # v19 绿专项：绿局平均局长
                    episode_timeouts += 1 if ep.timeout else 0
                    if ep.winner == "":
                        draw_window += 1
                    else:
                        model_won = (ep.winner == str(ep.model_seat)
                                     if ep.model_seat in (0, 1) else ep.winner == "0")
                        win_window += 1 if model_won else 0
                        loss_window += 0 if model_won else 1
                    # 塑形占比分量拆分：终局步奖励 = ret − shaping_ret（±1 / 超时 -1 / 平 0）
                    shaping_abs = abs(ep.shaping_ret)
                    terminal_abs = abs(ep.ret - ep.shaping_ret)
                    episode_shaping_shares.append(shaping_abs / max(shaping_abs + terminal_abs, 1e-8))

                    # 2026-11-23 B4：本局行为计数（老协议无 behavior → 空 dict，按 0 计）
                    b = getattr(ep, "behavior", None) or {}
                    if b:
                        behav_win["games"] += 1
                        behav_win["sac"] += int(b.get("sac", 0))
                        behav_win["rec"] += int(b.get("rec", 0))
                        if int(b.get("chain", 0)) > 0:
                            behav_win["chain_games"] += 1
                        n_agros = int(b.get("agros", 0))
                        t_agros = int(b.get("agros_turn", 0))
                        if getattr(ep, "theme", "") == "green":
                            behav_win["green_games"] += 1
                        if n_agros > 0:
                            behav_win["agros"] += n_agros
                            behav_win["agros_games"] += 1
                            # 绿自家 T5 ≈ 全局第 9(P1)/10(P2) 回合 → 用 ≤10 作「T5 档前落地」
                            if 0 < t_agros <= 10:
                                behav_win["agros_t5_games"] += 1
                            if behav_win["agros_turn_min"] == 0 or 0 < t_agros < behav_win["agros_turn_min"]:
                                behav_win["agros_turn_min"] = t_agros
                        # 2026-11-23 追加：绿成长（沃土打出 / T1·T3 地数）+ 红法术线
                        _th = getattr(ep, "theme", "")
                        if _th == "green":
                            behav_win["grow"] = behav_win.get("grow", 0) + int(b.get("grow", 0))
                            for k in ("land_t1", "land_t3"):
                                v = int(b.get(k, -1) or -1)
                                if v >= 0:
                                    behav_win.setdefault(k + "_sum", 0)
                                    behav_win.setdefault(k + "_n", 0)
                                    behav_win[k + "_sum"] += v
                                    behav_win[k + "_n"] += 1
                        elif _th == "red":
                            behav_win["blood"] = behav_win.get("blood", 0) + int(b.get("blood", 0))
                            behav_win["bomb"] = behav_win.get("bomb", 0) + int(b.get("bomb", 0))
                            if int(b.get("combo", 0)) > 0:
                                behav_win["combo_games"] = behav_win.get("combo_games", 0) + 1
                        # 2026-11-23 考察项（全主题）：攻击目标分类——safe=打已横置且无警戒（不吃反击）
                        behav_win["atk_safe"] = behav_win.get("atk_safe", 0) + int(b.get("atk_safe", 0))
                        behav_win["atk_risky"] = behav_win.get("atk_risky", 0) + int(b.get("atk_risky", 0))
                        behav_win["atk_face"] = behav_win.get("atk_face", 0) + int(b.get("atk_face", 0))
                        behav_win["atk_any"] = behav_win.get("atk_any", 0) + int(b.get("atk_any", 0))

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
                    use_upgo=args.use_upgo,
                )
                all_advantages.append(advantages)
                all_returns.append(returns)

            # 学习者掩码过滤（2026-11-02 C 期）：checkpoints/fsp 来源的对局里，对手座次
            # 的步由快照参数决策——其 logprob 相对当前策略无意义（ratio 错），必须从
            # PPO 批次剔除；GAE 仍在全序列上算（对手步的 critic 值照记，座次修正递推
            # 完整），只在入批时过滤行。无快照对手时掩码恒 True，零行为差。
            b_actions = np.concatenate([data["actions"][data["learner"]] for data in rollout_data])
            if b_actions.shape[0] == 0:
                # 极端窗口：全是对手步（快照对手局横跨整窗且轮到对手连打）——没有
                # 学习者样本可更新，跳过本窗口 PPO（梯度无定义），其余统计照记
                print(f"[!] update {update}: 本窗口无学习者决策步，跳过 PPO 更新", flush=True)
                loss = float("nan")
                pg_loss = v_loss = entropy = approx_kl = clipfrac = 0.0
                explained_var = 0.0
                skip_update = True
            else:
                skip_update = False
                b_obs = stack_obs(
                    [o for data in rollout_data for o, m in zip(data["obs"], data["learner"]) if m])
                b_rstates = stack_rstates(
                    [r for data in rollout_data for r, m in zip(data["rstates"], data["learner"]) if m])
            b_old_logprobs = np.concatenate([data["logprobs"][data["learner"]] for data in rollout_data])
            b_advantages = np.concatenate(
                [a[data["learner"]] for a, data in zip(all_advantages, rollout_data)])
            b_returns = np.concatenate(
                [r[data["learner"]] for r, data in zip(all_returns, rollout_data)])

            # 归一化 advantages
            if not skip_update:
                b_advantages = (b_advantages - b_advantages.mean()) / (b_advantages.std() + 1e-8)

            # explained_var（2026-11-02 补）：rollout 期 critic 估值 vs GAE 回报目标——
            # 1.0=完美拟合 / 0=不比均值好 / <0=比均值还差（critic 学崩的直接信号）
            b_values = np.concatenate([d["values"][d["learner"]] for d in rollout_data])
            if not skip_update:
                var_returns = float(np.var(b_returns))
                explained_var = (1.0 - float(np.var(b_returns - b_values)) / var_returns
                                 if var_returns > 1e-8 else 0.0)
                b_actions = jnp.array(b_actions, dtype=jnp.int32)
                b_old_logprobs = jnp.array(b_old_logprobs, dtype=jnp.float32)
                b_advantages = jnp.array(b_advantages, dtype=jnp.float32)
                b_returns = jnp.array(b_returns, dtype=jnp.float32)

            # ==================== PPO Update（jit 整批，update 1 编译一次后亚秒级） ====================
            # 多 epoch 复用同批：epoch1 ratio=1（params 未动，等价纯 PG），epoch≥2 偏离
            # 采样口径、clip 窗口接管——单 epoch 时代 clip 恒不触发（2026-09-29 修正）。
            if not skip_update:
                for _ in range(args.update_epochs):
                    train_state, loss, (pg_loss, v_loss, entropy, approx_kl, clipfrac) = ppo_update(
                        train_state, b_obs, b_rstates, b_actions,
                        b_old_logprobs, b_advantages, b_returns,
                    )

            update_time = time.time() - update_start

            # ==================== v2 动作类型使用统计（C期能力「学会了没有」的信号） ====================
            type_counts, new_type_frac, targeted_frac, resp_step_frac, green_stats = action_usage_stats(rollout_data)

            # ==================== L3-A 循环治理指标（2026-11-18 方案 §5.6，健康值见下） ====================
            # loop_events（L1 收口，健康 0）/ loop_budget（L2 收口，健康 0）/
            # pruned_steps（剪枝步占比，健康 <1%）/ max_turn_actions（回合步数峰值，健康 ≤60）
            loop_events = sum(d["loop_stats"]["events"] for d in rollout_data)
            loop_budget = sum(d["loop_stats"]["budget"] for d in rollout_data)
            loop_pruned_total = sum(d["loop_stats"]["pruned"] for d in rollout_data)
            loop_steps_total = sum(d["loop_stats"]["steps"] for d in rollout_data)
            pruned_frac = loop_pruned_total / max(loop_steps_total, 1)
            max_turn_actions = max((d["loop_stats"]["max_turn_actions"] for d in rollout_data),
                                   default=0)

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
                "approx_kl": float(approx_kl),
                "clipfrac": float(clipfrac),
                "explained_var": round(explained_var, 4),
                # 塑形配置与占比（2026-11-02 补）：λ=0 基线跑出的行 share 恒 0 可直接对账
                "shaping_lambda": args.reward_lambda,
                "shaping_share": round(float(np.mean(episode_shaping_shares)), 4)
                                 if episode_shaping_shares else 0.0,
                "ep_ret": mean_return,
                "ep_len": mean_length,
                "episodes": len(episode_returns),
                "timeouts": episode_timeouts,
                # 本 update 窗口完结对局 W/L/D（vs 脚本训练=模型视角 / 自对弈训练=先手座次视角）
                "wins": win_window,
                "losses": loss_window,
                "draws": draw_window,
                "sps": sps,
                "update_time": update_time,
                "elapsed": elapsed,
                # v2：动作类型分布 + 新能力使用率（new_type=HeroSkill/VoluntaryTrigger/Pass/
                # Respond*/Guard；targeted=所选动作带卡目标；resp=响应窗口决策步占比）
                "act_type": type_counts,
                "new_type_frac": round(new_type_frac, 4),
                "targeted_frac": round(targeted_frac, 4),
                "resp_step_frac": round(resp_step_frac, 4),
                # 对手池（2026-11-02 C 期）：累计 reset 来源分布（label 粒度，含主题 key /
                # 快照 label），与 info.opponentSource 同口径——日志/stats 双通道可对账
                "oppPool": dict(pool_env.source_counts),
                "fsp": bool(args.fsp),
                # L3-A 循环治理（2026-11-18 方案 §5.6）：L1/L2 收口次数 + 剪枝占比 +
                # 回合步数峰值——全 0/低位=环境健康；loop_events 突然飙高=新卡引入新循环，
                # 停训检查（方案 §9.2）
                "loop_events": loop_events,
                "loop_budget": loop_budget,
                "pruned_steps": round(pruned_frac, 4),
                "max_turn_actions": max_turn_actions,
                # v19 绿套补盲（2026-11-20，绿套学习与观测补盲计划 §3）：绿专项统计点——
                # 牺牲行使用率/Any 获取代理量/大哥出牌率/绿局平均局长（近 100 绿局滚动）。
                # 「不再为 0」= 绿的关键线（牺牲→Any→T5 大哥）开始被采样；bootcamp（1a）期
                # 模型恒绿，ep_len 即绿局长；混回三套（1b）后按 theme 键归属。
                "green": {
                    **green_stats,
                    "sacrifice_frac": round(green_stats["sacrifice_frac"], 4),
                    "big_play_frac": round(green_stats["big_play_frac"], 4),
                    "ep_len": round(float(np.mean(green_episode_lengths)), 1)
                              if green_episode_lengths else None,
                    "episodes": len(green_episode_lengths),
                },
                # 2026-11-23 B4（用户口径）：「模型是否学会牺牲+回收 / 能否 T5 落地 14 费大哥」。
                # 口径=Apply 成功才计（driver TrackModelBehavior），不是"选中了某行"。
                # chain_games / green_games = 走通回收链的绿局占比；agros_t5_games = T5 档（≤全局 10 回合）落地局数。
                "model_behavior": behav_win,
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
                      f"SPS {sps:5.0f} | Time {update_time:.2f}s | "
                      f"新类型 {new_type_frac:.0%} 带目标 {targeted_frac:.0%} 响应步 {resp_step_frac:.0%} | "
                      f"绿[牺牲 {green_stats['sacrifice_frac']:.1%}·Any {green_stats['any_gain']}·"
                      f"大哥 {green_stats['big_play_frac']:.1%}] | "
                      f"循环 L1 {loop_events} L2 {loop_budget} 剪 {pruned_frac:.1%} 峰值 {max_turn_actions}", flush=True)
                # 对手池轮换证据（2026-11-02 C 期）：累计来源分布单独成行——分主题/分快照
                # 可直接对账（与 stats.jsonl 的 oppPool 字段、info.opponentSource 同口径）
                if args.opponent_pool != "current" or args.fsp:
                    dist = " ".join(f"{k}×{v}" for k, v in sorted(pool_env.source_counts.items()))
                    print(f"  [对手池] opponentSource 累计: {dist}"
                          + (" | fsp=true" if args.fsp else ""), flush=True)

            # ==================== 评估（复用训练 env；进行中的对局被打断） ====================
            if update % eval_every_updates == 0:
                # 快照池入池（2026-11-02 C 期）：每个评估点把当前策略参数快照进对手池
                # （环形保留最近 checkpoint_pool_size 份）——checkpoints/fsp 的历史对手来源
                ckpt_pool.add(f"step{global_step}", train_state.params)
                sp_win_rate = None
                if args.opponent == "selfplay":
                    # 口径 1：自对弈先手座次胜率（先后手失衡度量，非棋力——只记录不选点）
                    sp_win_rate, sp_wins, sp_losses, sp_draws, sp_avg_len, _, _ = evaluate_greedy(
                        jit_apply, train_state.params, env, args,
                        args.eval_episodes, opponent="selfplay",
                    )
                    write_eval_row("selfplay_first_seat", "", sp_win_rate, sp_wins, sp_losses,
                                   sp_draws, sp_avg_len, args.eval_episodes, update)
                # 口径 2（自对弈训练时）/ 唯一口径（simpleai 训练时）：vs 脚本主题卡组模型胜率
                # ——三主题口径（2026-11-02 C 期）：每主题各 eval_episodes 局，分主题胜率
                # 必印（防单主题退化被总胜率掩盖）；best 保存与早停按选择口径（下方 B4 块）
                # 评估模型侧卡组（2026-11-21）：默认 all=三预组都评 → 6 格标尺（模型执该套 ×
                # 该座位）全填；与 args.model_theme（训练口径）解耦，绿 bootcamp 也出 6 格，
                # 此时红/蓝格=未训练 floor，只作参照、不作验收
                eval_themes = parse_eval_themes(args.eval_theme)
                model_themes = eval_model_themes(args)
                eval_opponent = "simpleai" if args.opponent == "selfplay" else None
                if len(eval_themes) == 1 and len(model_themes) == 1:
                    win_rate, wins, losses_, draws, avg_length, theme_stats, cell_stats = evaluate_greedy(
                        jit_apply, train_state.params, env, args, args.eval_episodes,
                        opponent=eval_opponent, theme=eval_themes[0],
                        model_theme=model_themes[0],
                    )
                    # 单对分支：手工构造与 multi 同构的矩阵（键=唯一 (模型卡组, 对手主题) 对；
                    # 末位 len_sum 与 multi 同构，B2 矩阵行 avg_len 用）
                    _n = wins + losses_ + draws
                    _p1 = list(cell_stats.get((model_themes[0], 0), [0, 0, 0]))
                    _p2 = list(cell_stats.get((model_themes[0], 1), [0, 0, 0]))
                    matrix_stats = {(model_themes[0], eval_themes[0]):
                                    [wins, _n, _p1[0], _p1[1], _p2[0], _p2[1],
                                     avg_length * _n]}
                else:
                    win_rate, wins, losses_, draws, avg_length, theme_stats, cell_stats, matrix_stats = evaluate_greedy_multi(
                        jit_apply, train_state.params, env, args, args.eval_episodes,
                        opponent=eval_opponent, themes=eval_themes, model_themes=model_themes,
                    )
                for theme_key, t in sorted(theme_stats.items()):
                    if t[3] > 0:
                        write_eval_row("vs_script", theme_key, t[0] / t[3], t[0], t[1], t[2],
                                       t[4] / t[3], t[3], update)
                # 6 格行（2026-11-17 两阶段课程）：metric=vs_script_cell，theme=<key>/P1|P2，
                # 附门槛 threshold 与是否达标 pass——缺格不写（eval-theme 只评部分主题时按实有格计）
                cells_ok = None
                if cell_stats:
                    filled = {k: v for k, v in cell_stats.items() if v[1] > 0}
                    for (tk, seat), c in sorted(filled.items()):
                        thr = cell_threshold(tk, seat)
                        wr = c[0] / c[1]
                        write_eval_row("vs_script_cell", f"{tk}/P{seat+1}", wr, c[0], c[1] - c[0],
                                       0, c[2] / c[1], c[1], update,
                                       extra={"threshold": thr, "pass": None if thr is None else wr >= thr})
                # 矩阵行（2026-11-21）：metric=vs_script_matrix，theme=<模型色>|<对手色>，
                # extra 带 P1/P2 分座位 [胜, 局]——绿对红等单点对局训练内直接可追
                #（16 局/对，±12pp 噪声，看趋势不看点；粗测用 eval_matrix.py 200 局/对带 CI95）
                # avg_len：B2（2026-11-22）起写真值（该对平均决策步），不再恒 0.0
                for (mt_k, opp_k), m in sorted(matrix_stats.items()):
                    if m[1] > 0:
                        write_eval_row("vs_script_matrix", f"{mt_k}|{opp_k}", m[0] / m[1], m[0],
                                       m[1] - m[0], 0, m[6] / m[1], m[1], update,
                                       extra={"p1": [m[2], m[3]], "p2": [m[4], m[5]]})
                    cells_ok = (len(filled) == len(RED_CELL_BASELINES)
                                and all(c[0] / c[1] >= cell_threshold(*k) for k, c in filled.items()))
                # 实计局数：多模型卡组时 args.eval_episodes 只是每个 (模型卡组×对手主题)
                # 组合的基数，真实局数 = per_combo × 对手主题数 × 模型卡组数（旧写法
                # eval_episodes×主题数 在 3 模型卡组下会少报 2/3，6 格 n 与之一并不符）
                total_eval_eps = wins + losses_ + draws
                write_eval_row("vs_script", "ALL", win_rate, wins, losses_, draws,
                               avg_length, total_eval_eps, update)
                # 评估结果单独一行落盘（stats 写在 eval 前，混行 jsonl 不影响 tail）
                with open(log_dir / "stats.jsonl", "a", encoding="utf-8") as f:
                    f.write(json.dumps({
                        "update": update, "step": global_step, "eval": True,
                        "win_rate": win_rate,  # = vs SimpleAI 总胜率（选点口径）；自对弈口径见 sp_win_rate
                        "wins": wins, "losses": losses_, "draws": draws,
                        "win_rate_vs_theme": win_rate,
                        "theme_win_rates": {k: (t[0] / t[3]) for k, t in theme_stats.items() if t[3] > 0},
                        "cell_win_rates": {f"{k[0]}/P{k[1]+1}": (v[0] / v[1])
                                           for k, v in (cell_stats or {}).items() if v[1] > 0},
                        "cells_pass": cells_ok,  # 6 格全实有且全达标才 True；None=无格数据
                        "sp_win_rate": sp_win_rate,
                        "avg_length": avg_length,
                    }, ensure_ascii=False) + "\n")

                # 评估抢占了进行中的对局 → 所有 env 强制重开
                for env_id, e in enumerate(envs):
                    obs, info = e.reset()
                    carries[env_id] = [obs, init_rstate(args.rnn_type), 0.0, 0, 0.0, _seat_of(info)]

                # 选择口径（A2 2026-11-24 判据单源 phase1_criterion；沿革见该文件头注）：
                # **18 格矩阵 Δ 的最小 Wilson 下界**（best 选点——旧「最小 margin=点估计差」
                # 在 24 局/格 σ_Δ≈14pp 下等于在噪声格里挑最幸运的，2026-11-24 用户口径改下界）；
                # 早停门 = training_gate（2026-11-22 稳健版原值迁入：覆盖齐 18 格且 Δ 中位数
                # ≥+5pp 且 Δ≥−5pp 格数 ≥16/18）；候选门 Δ≥−5pp 只作预警（extra.candidate_below）。
                # 认证口径（每格 Δ≥0+中位数≥+15pp+饱和格不退步）不在训练内跑——停训后
                # certify_phase1 按 200 局/格走 judge()。
                # 矩阵/基线无数据时回落旧链：6 格 Δ → 镜像 → 总胜率（行为同 2026-11-17 版）。
                # 选择值+门控分量（extra.cells/worst/delta_median/robust_cells）落 eval.jsonl。
                select_rate = win_rate
                select_label = "三主题总胜率"
                select_extra = None
                target_hit = select_rate >= args.target_win_rate
                matrix_cells = {}
                for (mt_k, opp_k), m in matrix_stats.items():
                    for seat, w_i, n_i in ((0, m[2], m[3]), (1, m[4], m[5])):
                        if n_i > 0 and (mt_k, opp_k, seat) in SCRIPT_MATRIX_BASELINES:
                            matrix_cells[(mt_k, opp_k, seat)] = (w_i, n_i)
                if matrix_cells:
                    sel_val, worst_key = select_value(matrix_cells)
                    gate = training_gate(matrix_cells)
                    cand = candidate_gate(matrix_cells)
                    select_rate = sel_val if sel_val is not None else win_rate
                    select_label = "18格矩阵最小Wilson下界Δ"
                    target_hit = gate["pass"]
                    select_extra = {"cells": len(matrix_cells),
                                    "worst": (f"{worst_key[0]}|{worst_key[1]}/P{worst_key[2]+1}"
                                              if worst_key else None),
                                    "delta_median": (round(gate["median_delta"], 4)
                                                     if gate["median_delta"] is not None else None),
                                    "robust_cells": gate["robust_cells"],
                                    "candidate_below": [f"{k[0]}|{k[1]}/P{k[2]+1}" for k in cand["below"]],
                                    "gate": gate["reason"]}
                    med_txt = (f"{gate['median_delta']*100:+.1f}pp" if gate["median_delta"] is not None else "—")
                    print(f"  门控（训练内稳健口径·不作达标依据）: Δ中位数 {med_txt}、"
                          f"Δ≥−5pp 格数 {gate['robust_cells']}/{len(matrix_cells)}"
                          f"；候选门预警 {len(cand['below'])} 格")
                elif cell_stats:
                    # 6 格回落：门槛=同格脚本基线（A2 同步，Δ≥0 语义；旧 +15pp 口径废弃）
                    margins = [c[0] / c[1] - cell_threshold(*k)
                               for k, c in cell_stats.items()
                               if c[1] > 0 and cell_threshold(*k) is not None]
                    if margins:
                        select_rate = min(margins)
                        select_label = "6格最小Δ"
                    target_hit = cells_ok is True  # 六格全实有且全达标才算命中
                elif args.model_theme in theme_stats and theme_stats[args.model_theme][3] > 0:
                    t = theme_stats[args.model_theme]
                    select_rate = t[0] / t[3]
                    select_label = f"镜像({args.model_theme})胜率"
                    target_hit = select_rate >= args.target_win_rate
                write_eval_row("selector", select_label, select_rate, 0, 0, 0, 0.0, 0, update,
                               extra=select_extra)
                print(f"  选点口径: {select_label} = {select_rate*100:.1f}%"
                      f"（target {'命中' if target_hit else '未达'} {args.patience} 连击早停）")

                if select_rate > best_win_rate:
                    best_win_rate = select_rate
                    save_model("best", {
                        "step": global_step, "update": update,
                        "win_rate": select_rate, "select_label": select_label,
                        "wins": wins, "draws": draws,
                        "avg_length": avg_length,
                        "baseline_era": BASELINE_ERA,  # 判据/基线纪元戳（旧 era 的 best 作废）
                    })
                    print(f"✓ New best model saved → {log_dir / 'best'} ({select_label}={select_rate*100:.1f}%)\n", flush=True)

                # 早停检查（命中口径见上方 select 计算；阶段二=六格全达标，阶段一=镜像 ≥75%）
                if target_hit:
                    patience_counter += 1
                    print(f"✓ Target reached! ({patience_counter}/{args.patience})\n")
                    if patience_counter >= args.patience:
                        print(f"{'='*80}")
                        print(f"Training complete! {select_label} = {select_rate:.3f} 达标 ×{args.patience}")
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

    # 最终评估（三主题口径：每主题 50 局；单主题口径保持 100 局）
    # 评估模型侧卡组同训练内口径（2026-11-21）：eval_model_themes(args)，默认三套都评
    eval_themes = parse_eval_themes(args.eval_theme)
    model_themes = eval_model_themes(args)
    final_eps = 50 if len(eval_themes) > 1 else 100
    print(f"\n{'='*80}")
    print(f"Final Evaluation ({final_eps} episodes/theme × {len(eval_themes)} themes"
          f" × {len(model_themes)} model decks):")
    print(f"{'='*80}")
    # 训练 env 已关闭 → 临时再开一个（进程会重新拉起）
    eval_env = TideEnv(
        unity_path=args.unity_path,
        project_path=args.project_path,
        max_steps=args.max_episode_steps,
        opponent=args.opponent,
        model_theme=args.model_theme,
        startup_timeout=args.startup_timeout,
        step_timeout=args.step_timeout,
    )
    try:
        if args.opponent == "selfplay":
            sp_wr, sp_w, sp_l, sp_d, sp_len, _, _ = evaluate_greedy(
                jit_apply, train_state.params, eval_env, args,
                num_episodes=20, opponent="selfplay",
            )
            write_eval_row("final_selfplay_first_seat", "", sp_wr, sp_w, sp_l, sp_d,
                           sp_len, 20, num_updates_reached)
        eval_opponent = "simpleai" if args.opponent == "selfplay" else None
        if len(eval_themes) == 1 and len(model_themes) == 1:
            # 单主题分支：evaluate_greedy 返回 7 元组（无 matrix_stats）——本分支原本正确，
            # 勿随 multi 分支的 8 元组修复改动（2026-11-24 A2 曾误改后回退）。
            final_win_rate, wins, losses_, draws, avg_length, _theme_stats, _cells = evaluate_greedy(
                jit_apply, train_state.params, eval_env, args, num_episodes=final_eps,
                opponent=eval_opponent, theme=eval_themes[0],
                model_theme=model_themes[0],
            )
        else:
            # A2（2026-11-24）修复收尾解包 bug：evaluate_greedy_multi 返回 8 元组（含
            # matrix_stats），旧按 7 元组解包——任何跑完的 run 都在收尾评估处抛 ValueError。
            (final_win_rate, wins, losses_, draws, avg_length,
             _theme_stats, _cells, _matrix) = evaluate_greedy_multi(
                jit_apply, train_state.params, eval_env, args, num_episodes=final_eps,
                opponent=eval_opponent, themes=eval_themes, model_themes=model_themes,
            )
        # 实计局数（多模型卡组时 final_eps 只是每组合基数；旧写法会少报局数）
        write_eval_row("final_vs_script", "ALL", final_win_rate, wins, losses_, draws,
                       avg_length, wins + losses_ + draws, num_updates_reached)
        for (tk, seat), c in sorted((_cells or {}).items()):
            if c[1] > 0:
                thr = cell_threshold(tk, seat)
                write_eval_row("final_vs_script_cell", f"{tk}/P{seat+1}", c[0] / c[1], c[0],
                               c[1] - c[0], 0, 0.0, c[1], num_updates_reached,
                               extra={"threshold": thr, "pass": None if thr is None else c[0] / c[1] >= thr})
    finally:
        eval_env.close()

    elapsed_total = time.time() - start_time
    skill_label = "vs 脚本主题卡组棋力口径" if args.opponent == "selfplay" else args.opponent
    print(f"\n{'='*80}")
    print(f"Training finished!")
    print(f"  Total time: {elapsed_total/3600:.2f} hours")
    # best_win_rate 初值 = −inf（2026-11-20）：本次 run 没跑到任何评估点时不能打印 −inf%
    best_txt = "无评估点，未存 best" if best_win_rate == -float("inf") else f"{best_win_rate*100:.1f}%"
    print(f"  Best win rate ({skill_label}): {best_txt}")
    print(f"  Final win rate ({skill_label}): {final_win_rate*100:.1f}%")
    print(f"{'='*80}\n")


if __name__ == "__main__":
    args = Args()

    # CLI（2026-10-30 新增 --model-theme：红·快攻镜像训练口径；2026-11-02 新增
    # --shaping-lambda：λ=0 基线开关 + C 期对手池/评估三主题四参数；
    # 其余超参仍走 TIDE_TRAIN_OVERRIDE 环境变量 JSON，见下）
    import argparse
    parser = argparse.ArgumentParser(description="Tide PPO 训练（桥接 Unity batchmode）")
    parser.add_argument("--model-theme", default="",
                        help="vs 脚本口径下模型侧卡组：red/green/blue 固定该主题整组；"
                             "all=每局三预组随机（2026-11-17 一期课程：AI 执三套都比脚本强）；"
                             "空=Cards 随机 30")
    parser.add_argument("--model-doped", default="",
                        help="掺卡口径（2026-11-17 阶段二）：命中主题一侧 = 整组每种 −1 + 训练池随机补足"
                             "（红 30→25+5）；优先级高于 --model-theme；空=旧行为")
    parser.add_argument("--shaping-lambda", type=float, default=None,
                        help="塑形 λ 显式覆盖（默认不变）：0 = 关闭塑形——非终局奖励恒 0，"
                             "只留终局 ±1（λ=0 基线消融。旧口径 λ=0 发不下去、Unity 缺省回落 "
                             "0.005，塑形关不掉；现经 shapingSet=1 显式协议下发，含 0）")
    parser.add_argument("--opponent-pool", default=None, choices=POOL_MODES,
                        help="对手池（2026-11-02 C 期，只改「和谁打」不动 obs/奖励/网络/卡表）："
                             "current=旧行为同策略自对弈（默认）；themes=三预组整组轮换"
                             "（SimpleAI+AutoMatch 配套策略脚本）；checkpoints=历史快照池当对手；"
                             "mixed=按 --pool-weights 加权混合。采样点=每次 reset，"
                             "info.opponentSource 标注来源")
    parser.add_argument("--pool-weights", default=None,
                        help="mixed 模式权重，逗号三项 current,themes,checkpoints（默认 0.5,0.25,0.25）")
    parser.add_argument("--fsp", action="store_true",
                        help="fictitious self-play：对手=快照池均匀随机旧策略（排除最新一份），"
                             "等价 --opponent-pool checkpoints 的 FSP 口径；日志标注 fsp=true")
    parser.add_argument("--eval-theme", default=None,
                        help="评估基准主题：空串=三主题各 50 局输出分主题胜率（默认）；"
                             "red=单主题；red,green=逗号列表。早停/best 按选择口径（B4=18格矩阵margin）")
    parser.add_argument("--eval-model-theme", default=None,
                        help="评估局模型侧卡组（2026-11-21 与训练口径解耦）：all=三预组都评"
                             "（默认，6 格标尺全填，绿 bootcamp 也出 6 格）；"
                             "空串=回落 --model-theme（旧行为）；red/green/blue=只评该套")
    parser.add_argument("--no-prune", action="store_true",
                        help="关闭 L3-A 循环空转步剪枝（循环治理 §5.4 回滚开关："
                             "收口回合的步重新进 PPO 批次——环境异常误报时退回旧行为用）")
    parser.add_argument("--init-params", default="",
                        help="热启动参数：run 目录（取其 params.msgpack）或 .msgpack 路径；"
                             "只载权重、不载优化器状态（2026-11-20 §4.6 环境修正后续训用）")
    parser.add_argument("--opponent", default=None, choices=("selfplay", "simpleai", "script"),
                        help="对手位口径（A1 2026-11-24 回到一期训练目标）：一期验收只认 simpleai"
                             "（script=simpleai 别名，进协议前归一）；优先于 TIDE_TRAIN_OVERRIDE——"
                             "11-23 起 17 个 run 全带 --model-theme all 却唯独缺这个参数，人眼看 "
                             "config 看不出漂移，只能显式指定+落判据块+硬闸断言")
    cli = parser.parse_args()
    args.model_theme = cli.model_theme
    args.model_doped = cli.model_doped
    if cli.init_params:
        args.init_params = cli.init_params
    if cli.no_prune:
        args.prune_loop_steps = False

    # 对手池/评估 CLI 解析（在 TIDE_TRAIN_OVERRIDE 之后应用：命令行显式指定优先）
    if cli.opponent_pool is not None:
        args.opponent_pool = cli.opponent_pool
    if cli.pool_weights is not None:
        try:
            args.pool_weights = tuple(float(x) for x in cli.pool_weights.split(","))
        except ValueError:
            raise SystemExit(f"[!] --pool-weights 须为逗号分隔三项数字，得到 {cli.pool_weights!r}")
    if cli.eval_theme is not None:
        args.eval_theme = cli.eval_theme
    if cli.eval_model_theme is not None:
        args.eval_model_theme = cli.eval_model_theme
        # 非法值在启动时就响亮失败（否则一路跑到 Unity 侧才回落成随机卡组）
        emt = args.eval_model_theme.strip()
        if emt not in ("", "all") and emt not in THEME_KEYS:
            raise SystemExit(f"[!] --eval-model-theme 只能是 all / 空串 / {list(THEME_KEYS)}，"
                             f"得到 {cli.eval_model_theme!r}")
    if cli.fsp:
        args.fsp = True
    if args.opponent_pool not in POOL_MODES:
        raise SystemExit(f"[!] 未知 opponent-pool: {args.opponent_pool}（可选 {POOL_MODES}）")
    w = tuple(args.pool_weights)
    if len(w) != 3 or abs(sum(w) - 1.0) > 1e-6 or min(w) < 0:
        raise SystemExit(f"[!] pool_weights 须为三项非负且和为 1（current,themes,checkpoints），得到 {w}")

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

    # --shaping-lambda 在 overrides 之后应用：命令行显式指定优先于环境变量 JSON
    if cli.shaping_lambda is not None:
        args.reward_lambda = cli.shaping_lambda

    # A1（2026-11-24 回到一期训练目标）：口径显式化——命令行 --opponent 优先于 TIDE_TRAIN_OVERRIDE
    #（口径不是超参：环境变量 JSON 只许改超参，不许无感换口径）。script=simpleai 别名，进协议前归一。
    if cli.opponent is not None:
        if cli.opponent == "script":
            print(f"[口径] --opponent script → simpleai（别名归一，config/协议按归一值落盘）")
            args.opponent = "simpleai"
        else:
            args.opponent = cli.opponent

    # 默认 run 名带口径（A1）：未被 TIDE_TRAIN_OVERRIDE 覆盖时由写死的 tide_ppo_selfplay_v2c
    # 改为 tide_ppo_<opponent>——run 名即口径，自对弈/vs脚本 一眼可分。
    if args.exp_name == "tide_ppo_selfplay_v2c":
        args.exp_name = f"tide_ppo_{args.opponent}"

    print(f"\nConfiguration:")
    print(f"  Unity: {args.unity_path or 'auto-detect（UNITY_PATH → Hub 扫描匹配工程版本）'}")
    print(f"  Project: {args.project_path or 'auto-detect'}")
    print(f"  Max episode steps: {args.max_episode_steps}")
    print(f"  Total timesteps: {args.total_timesteps:,}")
    print(f"  Opponent: {args.opponent}")
    if args.opponent == "simpleai":
        print(f"  [口径] 一期验收口径（对手=脚本；preflight_phase1/certify_phase1 只认 simpleai）")
    elif args.opponent == "selfplay":
        print(f"  [口径] 自对弈口径——非一期验收口径（一期只认 --opponent simpleai）"
              + ("；掺卡诊断用" if args.model_doped else ""))
    print(f"  Opponent pool: {args.opponent_pool}"
          + (f"（mixed 权重 current/themes/checkpoints = " + "/".join(f"{x:.2f}" for x in args.pool_weights) + ")"
             if args.opponent_pool == "mixed" else ""))
    if args.fsp:
        print(f"  FSP: true（对手=快照池均匀随机旧策略，排除最新；info.fsp 标注）")
    print(f"  Model theme: {args.model_theme or 'Cards 随机 30（不固定）'}"
          + ("（all=每局三预组随机，一期课程）" if args.model_theme == "all"
             else "（模型侧=该主题整组）" if args.model_theme else ""))
    print(f"  Eval themes: {', '.join(parse_eval_themes(args.eval_theme))}"
          f"（每主题 {args.eval_episodes} 局；6 格=模型执三套 × 先/后手；早停/best 选择口径"
          f"= 18 格矩阵最小 margin〔B4 2026-11-22〕，矩阵无数据回落 6 格 margin）")
    # 评估模型侧卡组与实计局数（2026-11-21）：多卡组时 eval_episodes 只是每组合基数，
    # 真实每次评估局数 = 每组合局数 × 对手主题数 × 模型卡组数（C3 后三套×三主题×48=432 局）
    _emt = eval_model_themes(args)
    _emt_num = len(_emt)
    _per_combo = max(48, args.eval_episodes // _emt_num) if _emt_num > 1 else args.eval_episodes
    print(f"  Eval model decks: {', '.join(str(m) for m in _emt)}"
          f"（每组合 {_per_combo} 局 → 每次评估约 "
          f"{_per_combo * len(parse_eval_themes(args.eval_theme)) * _emt_num} 局）"
          + ("；单主题训练时红/蓝格=未训练 floor，只作参照" if args.model_theme and args.model_theme != "all" else ""))
    if args.reward_lambda > 0:
        print(f"  Reward lambda: {args.reward_lambda} → shaping=on（λ·ΔΦ 势能塑形，经 reset 协议显式下发）")
    else:
        print(f"  Reward lambda: {args.reward_lambda} → shaping=off（λ=0 基线：非终局奖励恒 0，纯终局 ±1）")
    if overrides:
        print(f"  Overrides: {overrides}")
    print(f"  Eval: vs 脚本主题卡组模型胜率（早停目标 {args.target_win_rate*100:.0f}%，分主题 red/green/blue 统计）"
          + ("；自对弈另报先手座次胜率" if args.opponent == "selfplay" else "") + "\n")

    train(args)
