"""
Tide 简单编码器（路线 1：MLP/GRU actor-critic）。

不做 YGO 式的 embedding lookup / spec gather，直接 Dense 投影扁平特征。
复用 ygo-agent 的 PPO/GAE 损失与 obs-dict 契约，只换编码器。
"""

from typing import Optional
from functools import partial

import jax.numpy as jnp
import flax.linen as nn

from tide_features import N_CARD_FEATURES, N_GLOBAL_FEATURES, N_ACTION_FEATURES


default_fc_init = nn.initializers.orthogonal(scale=jnp.sqrt(2))


class TideCardEncoder(nn.Module):
    """
    卡牌编码器（简化版）：直接 Dense 投影 (80, 20) → (80, channels)。

    不做 YGO 的 code_id embedding lookup / spec gather / attribute 分桶。
    TideObservation 已把卡牌特征扁平化为 20 维浮点数。
    """
    channels: int = 128
    dtype: Optional[jnp.dtype] = None
    param_dtype: jnp.dtype = jnp.float32

    @nn.compact
    def __call__(self, x, mask=None):
        """
        Args:
            x: (batch, 80, 20) 卡牌特征
            mask: (batch, 80) bool 掩码（True=有效卡，False=空槽）

        Returns:
            f_cards: (batch, 80, channels) 编码后特征
            c_mask: (batch, 80) bool 掩码（True=空槽需要屏蔽）
        """
        batch_size = x.shape[0]
        c = self.channels

        # x[:, :, 0] 是 valid 标记（1=有效，0=空槽）
        valid = x[:, :, 0]  # (batch, 80)
        c_mask = valid == 0
        c_mask = jnp.asarray(c_mask).at[:, 0].set(False)  # 转为 JAX array，保留第 0 槽（全局槽/占位符）

        # 直接 Dense 投影 20 维 → channels
        x_cards = nn.Dense(
            c,
            dtype=self.dtype,
            param_dtype=self.param_dtype,
            kernel_init=default_fc_init,
        )(x)  # (batch, 80, channels)

        # LayerNorm
        x_cards = nn.LayerNorm(use_scale=True, use_bias=True, dtype=self.dtype)(x_cards)

        return x_cards, c_mask


class TideGlobalEncoder(nn.Module):
    """
    全局状态编码器：直接 Dense 投影 (32,) → (channels,)。

    不做 YGO 的 LP/turn/phase/计数 分桶 embedding。
    """
    channels: int = 128
    dtype: Optional[jnp.dtype] = None
    param_dtype: jnp.dtype = jnp.float32

    @nn.compact
    def __call__(self, x):
        """
        Args:
            x: (batch, 32) 全局特征

        Returns:
            (batch, channels)
        """
        c = self.channels

        # 直接 Dense 投影
        x = nn.Dense(
            c,
            dtype=self.dtype,
            param_dtype=self.param_dtype,
            kernel_init=default_fc_init,
        )(x)

        # LayerNorm
        x = nn.LayerNorm(use_scale=True, use_bias=True, dtype=self.dtype)(x)

        return x


class TideActionEncoder(nn.Module):
    """
    动作编码器：直接 Dense 投影 (max_actions, 6) → (max_actions, channels)。

    不做 YGO 的 msg/act/effect/phase/position/place/attrib 分桶 embedding。
    """
    channels: int = 128
    dtype: Optional[jnp.dtype] = None
    param_dtype: jnp.dtype = jnp.float32

    @nn.compact
    def __call__(self, x):
        """
        Args:
            x: (batch, max_actions, 6) 动作特征

        Returns:
            (batch, max_actions, channels)
        """
        c = self.channels

        # 直接 Dense 投影
        x = nn.Dense(
            c,
            dtype=self.dtype,
            param_dtype=self.param_dtype,
            kernel_init=default_fc_init,
        )(x)

        # LayerNorm
        x = nn.LayerNorm(use_scale=True, use_bias=True, dtype=self.dtype)(x)

        return x


class TideEncoder(nn.Module):
    """
    Tide 总编码器（胶水层，对应 ygo-agent 的 Encoder.__call__）。

    输入 obs_dict → 三路编码 → concat。
    """
    channels: int = 128
    dtype: Optional[jnp.dtype] = None
    param_dtype: jnp.dtype = jnp.float32

    @nn.compact
    def __call__(self, x):
        """
        Args:
            x: obs_dict = {
                "cards_": (batch, 80, 20),
                "global_": (batch, 32),
                "actions_": (batch, max_actions, 6),
                "h_actions_": (batch, 32, 14) or None
            }

        Returns:
            encoded: dict with encoded features
        """
        cards = x["cards_"]      # (batch, 80, 20)
        global_ = x["global_"]   # (batch, 32)
        actions = x["actions_"]  # (batch, max_actions, 6)

        # 编码
        card_encoder = TideCardEncoder(self.channels, self.dtype, self.param_dtype)
        global_encoder = TideGlobalEncoder(self.channels, self.dtype, self.param_dtype)
        action_encoder = TideActionEncoder(self.channels, self.dtype, self.param_dtype)

        cards_enc, c_mask = card_encoder(cards)    # (batch, 80, channels), (batch, 80)
        global_enc = global_encoder(global_)       # (batch, channels)
        actions_enc = action_encoder(actions)      # (batch, max_actions, channels)

        # 动作掩码：actions[:, :, 0] 是 valid 标记（1=合法，0=非法）
        a_mask = actions[:, :, 0] == 0

        return {
            "cards": cards_enc,
            "global": global_enc,
            "actions": actions_enc,
            "c_mask": c_mask,
            "a_mask": a_mask,
        }
