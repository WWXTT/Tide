"""
Tide RL — 路线 1：简单 MLP/GRU Actor-Critic

复用 ygo-agent 的 PPO/GAE 损失，只换编码器。
不做 YGO 式的 embedding lookup，直接 Dense 投影扁平特征。
"""

from tide_features import (
    N_CARD_FEATURES,
    N_GLOBAL_FEATURES,
    N_ACTION_FEATURES,
    MAX_CARDS,
    MAX_ACTIONS,
    sample_input,
    init_rstate,
    pad_or_truncate_actions,
)

from tide_encoder import (
    TideCardEncoder,
    TideGlobalEncoder,
    TideActionEncoder,
    TideEncoder,
)

from tide_agent import (
    TideActor,
    TideCritic,
    TideRNNAgent,
    create_tide_agent,
)

from tide_env import (
    TideEnv,
    make_tide_env,
)

__version__ = "0.1.0"

__all__ = [
    # Features
    "N_CARD_FEATURES",
    "N_GLOBAL_FEATURES",
    "N_ACTION_FEATURES",
    "MAX_CARDS",
    "MAX_ACTIONS",
    "sample_input",
    "init_rstate",
    "pad_or_truncate_actions",
    # Encoder
    "TideCardEncoder",
    "TideGlobalEncoder",
    "TideActionEncoder",
    "TideEncoder",
    # Agent
    "TideActor",
    "TideCritic",
    "TideRNNAgent",
    "create_tide_agent",
    # Env
    "TideEnv",
    "make_tide_env",
]
