"""对手池（2026-11-02 C 期：对手分布扩展——只改「和谁打」，不动 obs/奖励/网络/卡表）。

动机：默认 selfplay 的对手分布太窄（同策略 + 单主题评估）——「信号分布问题」而非算法问题。
本模块提供三件事：

1. CheckpointPool —— 历史策略参数快照池（环形保留最近 N 份，msgpack 字节态）；
2. OpponentSampler —— 每次 reset 按模式采一个对手来源：
   - current     旧行为（selfplay 同策略 / simpleai 脚本，由 env 构造参数决定）＝默认；
   - themes      三预组整组轮换（red/green/blue，SimpleAI+AutoMatch 配套策略脚本，
                 经 reset 协议 opponent=simpleai + theme=<key> 下发，Unity 侧零改）；
   - checkpoints 历史快照当对手（selfplay 协议 + Python 侧对手座次由旧参数驱动）；
   - mixed       三来源按权重混合（默认 current 0.5 / themes 0.25 / checkpoints 0.25）；
3. OpponentPoolEnv —— TideEnv 薄壳：reset 无 options 时接管采样并注入来源，
   info 标注 opponentSource / fsp /（checkpoints 来源）modelSeat=学习者座次；
   reset 带 options（评估等显式口径）时直通，不掺和。

FSP（fictitious self-play，--fsp 开关）：对手=历史策略池的平均行为的实用近似——
每局从 checkpoint 池**均匀**随机采一份旧策略当对手（并排除最新一份=当前策略自身），
默认关闭；开启后 info.fsp=True、日志标注 fsp=true。

采样点：每次 reset()（训练 rollout 内部的自动 reset 也走本壳，见 rollout_common
对 env.opponent_params / env.learner_seat 的读取）。
"""

import numpy as np
import flax.serialization

# themes 来源轮换的预组 key（与 TideHeadlessServer 侧主题卡组 key 一致）
THEME_KEYS = ("red", "green", "blue")

POOL_MODES = ("current", "themes", "checkpoints", "mixed")

# mixed 默认权重（顺序：current, themes, checkpoints）——保持 50% 同策略保证
# 自对弈主信号不被稀释，主题/快照各 25% 引入分布多样性；CLI --pool-weights 可覆盖
DEFAULT_MIXED_WEIGHTS = (0.5, 0.25, 0.25)


class CheckpointPool:
    """历史策略快照池：环形保留最近 max_size 份（含训练初始随机策略 seed 用的 "init"）。

    存 msgpack 字节而非参数树——反序列化按需做（from_bytes 以当前参数树为模板，
    结构由冻结的网络结构决定，不会漂移）。采样权重两种：
    - uniform ：等权（FSP 的「平均行为」近似口径）；
    - recency ：越新越可能被抽中（权重 ∝ 序号 1..n，防过旧弱策略占比过大）。
    """

    def __init__(self, max_size: int = 8):
        self.max_size = max_size
        self.entries = []  # [(label, msgpack_bytes)]，append 序即时间序

    def __len__(self):
        return len(self.entries)

    def add(self, label: str, params) -> str:
        """快照入池（超容量挤掉最旧）。返回 label 便于日志引用。"""
        self.entries.append((label, flax.serialization.to_bytes(params)))
        if len(self.entries) > self.max_size:
            self.entries.pop(0)
        return label

    def sample(self, rng, template_params, weighting: str = "uniform", exclude_latest: bool = False):
        """采一份快照 → (label, params)；池空返回 (None, None)。

        template_params：当前参数树（结构模板，from_bytes 用）。
        exclude_latest：FSP 口径——排除最新一份（= 刚入池的当前策略，打它等于 selfplay）。
        """
        entries = self.entries[:-1] if exclude_latest and len(self.entries) > 1 else self.entries
        if not entries:
            return None, None
        if weighting == "recency" and len(entries) > 1:
            w = np.linspace(1.0, float(len(entries)), num=len(entries))
            idx = int(rng.choice(len(entries), p=w / w.sum()))
        else:
            idx = int(rng.integers(len(entries)))
        label, blob = entries[idx]
        return label, flax.serialization.from_bytes(template_params, blob)


class _Spec:
    """一次采样的结果：来源 + reset options 增量 + 对手参数（checkpoints 才有）。"""

    __slots__ = ("source", "label", "options", "opponent_params")

    def __init__(self, source, label, options, opponent_params=None):
        self.source = source          # current / themes / checkpoints（池口径，日志聚合用）
        self.label = label            # 细粒度标签：current / theme:red / ckpt:step50000
        self.options = options        # 合并进 reset options 的增量（current 为空 dict）
        self.opponent_params = opponent_params


class OpponentSampler:
    """按模式在每次 reset 时采一个对手来源（独立 numpy rng，不影响 jax 训练rng）。"""

    def __init__(self, mode: str, rng, ckpt_pool: CheckpointPool,
                 weights=DEFAULT_MIXED_WEIGHTS, fsp: bool = False,
                 ckpt_weighting: str = "uniform", template_params=None):
        assert mode in POOL_MODES, f"未知 opponent-pool 模式: {mode}（可选 {POOL_MODES}）"
        assert abs(sum(weights) - 1.0) < 1e-6 and len(weights) == 3 and min(weights) >= 0, \
            f"pool_weights 须为三项非负且和为 1（current,themes,checkpoints），得到 {weights}"
        self.mode = mode
        self.rng = rng
        self.pool = ckpt_pool
        self.weights = tuple(weights)
        self.fsp = fsp
        self.ckpt_weighting = ckpt_weighting
        self.template_params = template_params  # 结构模板（每次采样时用最新参数树更稳，见 bind）
        # FSP 强制 checkpoints 口径（均匀 + 排除最新）——见模块 docstring
        if fsp:
            self.mode = "checkpoints"
            self.ckpt_weighting = "uniform"

    def bind(self, template_params):
        """训练参数树就绪后绑定结构模板（init 后调用一次）。"""
        self.template_params = template_params
        return self

    def _sample_checkpoint(self):
        label, params = self.pool.sample(
            self.rng, self.template_params,
            weighting=self.ckpt_weighting, exclude_latest=self.fsp)
        if params is None:  # 池空（训练循环总会 seed "init"，这里是防御）→ 回落 current
            return _Spec("current", "current", {})
        return _Spec("checkpoints", f"ckpt:{label}", {"opponent": "selfplay"}, params)

    def sample(self) -> _Spec:
        mode = self.mode
        if mode == "mixed":
            mode = str(self.rng.choice(("current", "themes", "checkpoints"), p=self.weights))
        if mode == "themes":
            key = THEME_KEYS[int(self.rng.integers(len(THEME_KEYS)))]
            return _Spec("themes", f"theme:{key}", {"opponent": "simpleai", "theme": key})
        if mode == "checkpoints":
            return self._sample_checkpoint()
        return _Spec("current", "current", {})


class OpponentPoolEnv:
    """TideEnv 对手池壳（鸭子类型：reset/step/close 直通，其余属性透传给内层 env）。

    接管规则：reset **不带 options**（训练 rollout 的自动 reset / 训练循环的常规重开）
    → 对手池采样接管；reset **带 options**（评估钉死 opponent/theme 等显式口径）→
    直通内层 env，不采样不注入——评估口径不受训练对手池影响。

    checkpoints 来源时暴露两个属性供 rollout_single_env 消费（局内有效，下次 reset 刷新）：
    - opponent_params：本局对手参数树（None = 本局无快照对手，全部决策点走当前策略）；
    - learner_seat   ：本局学习者座次（0/1；对手固定在另一座次）。
    对 selfplay 协议 Unity 恒报 modelSeat=-1，本壳在 step/reset 响应里注入
    info.modelSeat=learner_seat——训练循环的 W/L/D 与胜负归属自动落到学习者视角。
    """

    def __init__(self, env, sampler: OpponentSampler):
        self.env = env
        self.sampler = sampler
        self.opponent_params = None
        self.learner_seat = None
        self.source_counts = {}   # label -> 累计 reset 次数（stats/日志用）
        self.last_source = None

    # ------------------------------------------------- 采样与标注

    def _begin_episode(self):
        """采样新局对手：更新 opponent_params / learner_seat，返回来源 label。"""
        spec = self.sampler.sample()
        self.opponent_params = spec.opponent_params
        # 快照对手在座：学习者座次掷硬币（先后手各半）；无快照对手则无归属（全程学习者）
        self.learner_seat = int(self.rng_choice(2)) if spec.opponent_params is not None else None
        self.last_source = spec.label
        self.source_counts[spec.label] = self.source_counts.get(spec.label, 0) + 1
        return spec

    def rng_choice(self, n):
        return self.sampler.rng.integers(n)

    def _annotate(self, info):
        """reset/step 响应 info 注入来源标注（checkpoints 来源再补 modelSeat）。"""
        info["opponentSource"] = self.last_source
        if self.sampler.fsp:
            info["fsp"] = True
        if self.opponent_params is not None and self.learner_seat is not None:
            info["modelSeat"] = self.learner_seat
        return info

    # ------------------------------------------------- gym 接口

    def reset(self, seed=None, options=None):
        if options:  # 显式口径（评估）→ 直通，不采样
            obs, info = self.env.reset(seed=seed, options=options)
            return obs, info
        spec = self._begin_episode()
        obs, info = self.env.reset(seed=seed, options=spec.options or None)
        return obs, self._annotate(info)

    def step(self, action):
        obs, reward, done, truncated, info = self.env.step(action)
        self._annotate(info)
        return obs, reward, done, truncated, info

    def close(self):
        self.env.close()

    def __getattr__(self, name):
        # 未显式定义的属性/方法透传内层 env（max_steps/opponent/model_theme/last_n_actions 等）
        return getattr(self.env, name)
