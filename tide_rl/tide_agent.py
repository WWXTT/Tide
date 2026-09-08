"""
Tide 简化 Actor-Critic 模型（路线 1：MLP/GRU）。

复用 ygo-agent 的 RNN/Critic 骨干 + Tide 编码器。
简化版：不用 Transformer/FiLM，只用 GRU + MLP。
"""

from typing import Tuple, Optional
from functools import partial

import jax
import jax.numpy as jnp
import flax.linen as nn

from tide_encoder import TideEncoder


default_fc_init = nn.initializers.orthogonal(scale=jnp.sqrt(2))


class TideActor(nn.Module):
    """
    简化 Actor（不用 Transformer/FiLM，直接 MLP）。

    架构：
    1. Encoder 编码 cards/global/actions
    2. 全局特征 + cards 池化 → MLP
    3. Actions 逐动作打分 → softmax
    """
    channels: int = 128
    dtype: Optional[jnp.dtype] = None
    param_dtype: jnp.dtype = jnp.float32

    @nn.compact
    def __call__(self, encoded):
        """
        Args:
            encoded: dict from TideEncoder {
                "cards": (batch, 80, channels),
                "global": (batch, channels),
                "actions": (batch, max_actions, channels),
                "c_mask": (batch, 80),
                "a_mask": (batch, max_actions)
            }

        Returns:
            logits: (batch, max_actions)
        """
        c = self.channels
        mlp = partial(
            nn.Dense,
            dtype=self.dtype,
            param_dtype=self.param_dtype,
            kernel_init=default_fc_init,
        )

        cards = encoded["cards"]      # (batch, 80, channels)
        global_feat = encoded["global"]  # (batch, channels)
        actions = encoded["actions"]  # (batch, max_actions, channels)
        c_mask = encoded["c_mask"]    # (batch, 80)
        a_mask = encoded["a_mask"]    # (batch, max_actions)

        # Cards 池化（mean，屏蔽空槽）
        c_mask_expanded = c_mask[..., None]  # (batch, 80, 1)
        cards_masked = jnp.where(c_mask_expanded, 0, cards)
        cards_sum = cards_masked.sum(axis=1)  # (batch, channels)
        cards_count = (~c_mask).sum(axis=1, keepdims=True)  # (batch, 1)
        cards_count = jnp.maximum(cards_count, 1)  # 避免除 0
        cards_pooled = cards_sum / cards_count  # (batch, channels)

        # 全局上下文 = global + cards 池化
        context = global_feat + cards_pooled  # (batch, channels)

        # MLP 提取上下文特征
        context = mlp(c)(context)
        context = nn.relu(context)
        context = mlp(c)(context)
        context = nn.relu(context)

        # 逐动作打分：actions 与 context 点积
        context_expanded = context[:, None, :]  # (batch, 1, channels)
        logits = (actions * context_expanded).sum(axis=-1)  # (batch, max_actions)

        # 屏蔽非法动作（-inf）
        logits = jnp.where(a_mask, -1e9, logits)

        return logits


class TideCritic(nn.Module):
    """
    简化 Critic（MLP）。

    输入全局上下文 → MLP → 标量价值。
    """
    channels: int = 128
    dtype: Optional[jnp.dtype] = None
    param_dtype: jnp.dtype = jnp.float32

    @nn.compact
    def __call__(self, encoded):
        """
        Args:
            encoded: dict from TideEncoder

        Returns:
            value: (batch,) 标量价值
        """
        c = self.channels
        mlp = partial(
            nn.Dense,
            dtype=self.dtype,
            param_dtype=self.param_dtype,
            kernel_init=default_fc_init,
        )

        cards = encoded["cards"]
        global_feat = encoded["global"]
        c_mask = encoded["c_mask"]

        # Cards 池化
        c_mask_expanded = c_mask[..., None]
        cards_masked = jnp.where(c_mask_expanded, 0, cards)
        cards_sum = cards_masked.sum(axis=1)
        cards_count = (~c_mask).sum(axis=1, keepdims=True)
        cards_count = jnp.maximum(cards_count, 1)
        cards_pooled = cards_sum / cards_count

        # 全局上下文
        context = global_feat + cards_pooled

        # MLP
        x = mlp(c)(context)
        x = nn.relu(x)
        x = mlp(c)(x)
        x = nn.relu(x)
        x = mlp(1)(x)  # (batch, 1)

        value = x.squeeze(-1)  # (batch,)

        return value


class TideRNNAgent(nn.Module):
    """
    Tide RNN Actor-Critic（简化版，GRU + MLP）。

    对应 ygo-agent 的 RNNAgent，但用简化编码器和简化 Actor/Critic。
    """
    channels: int = 128
    rnn_channels: int = 512
    rnn_type: str = "gru"  # "gru" or "lstm"
    dtype: Optional[jnp.dtype] = None
    param_dtype: jnp.dtype = jnp.float32

    @nn.compact
    def __call__(self, rstate, x):
        """
        Args:
            rstate: (h, c) or h — RNN 隐状态
            x: obs_dict

        Returns:
            new_rstate, logits, value
        """
        c = self.channels

        # 1. 编码
        encoder = TideEncoder(c, self.dtype, self.param_dtype)
        encoded = encoder(x)

        # 2. Cards 池化作为 RNN 输入
        cards = encoded["cards"]
        c_mask = encoded["c_mask"]
        c_mask_expanded = c_mask[..., None]
        cards_masked = jnp.where(c_mask_expanded, 0, cards)
        cards_sum = cards_masked.sum(axis=1)
        cards_count = (~c_mask).sum(axis=1, keepdims=True)
        cards_count = jnp.maximum(cards_count, 1)
        cards_pooled = cards_sum / cards_count  # (batch, channels)

        # 3. RNN
        if self.rnn_type == "gru":
            rnn_cell = nn.GRUCell(
                features=self.rnn_channels,
                dtype=self.dtype,
                param_dtype=self.param_dtype,
            )
            new_rstate, rnn_out = rnn_cell(rstate, cards_pooled)
        elif self.rnn_type == "lstm":
            rnn_cell = nn.LSTMCell(
                features=self.rnn_channels,
                dtype=self.dtype,
                param_dtype=self.param_dtype,
            )
            new_rstate, rnn_out = rnn_cell(rstate, cards_pooled)
        else:
            raise ValueError(f"Unknown rnn_type: {self.rnn_type}")

        # 4. RNN 输出并入 global 特征
        global_feat = encoded["global"]
        enhanced_global = global_feat + nn.Dense(
            c, dtype=self.dtype, param_dtype=self.param_dtype, kernel_init=default_fc_init
        )(rnn_out)
        encoded["global"] = enhanced_global

        # 5. Actor & Critic
        actor = TideActor(c, self.dtype, self.param_dtype)
        critic = TideCritic(c, self.dtype, self.param_dtype)

        logits = actor(encoded)  # (batch, max_actions)
        value = critic(encoded)  # (batch,)

        return new_rstate, logits, value


def create_tide_agent(
    channels: int = 128,
    rnn_channels: int = 512,
    rnn_type: str = "gru",
):
    """工厂函数：创建 Tide RNN Agent。"""
    return TideRNNAgent(
        channels=channels,
        rnn_channels=rnn_channels,
        rnn_type=rnn_type,
    )
