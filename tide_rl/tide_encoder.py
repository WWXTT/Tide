"""
Tide 编码器（路线 1：MLP/GRU actor-critic + 内容身份 embedding）。

- 卡身份：原子内容哈希/组合结构/关键词组合槽（[15..23]）→ nn.Embed 查表（含槽位位置标记）
  masked-sum——同原子跨卡共享行（内容寻址，C# CardIdentityService 与费用推算同管线产出哈希）。
- 动作编码：6 维动作特征 + 按 source/targetIndex gather 的来源/目标卡编码 concat 后投影
  ——动作打分能读到它指向的那张卡（对齐 ygo-agent 的 spec gather 思路）。
- 状态特征（力量/费用/横置…）仍是扁平浮点 Dense 投影。
复用 ygo-agent 的 PPO/GAE 损失与 obs-dict 契约。
"""

from typing import Optional
from functools import partial

import jax.numpy as jnp
import flax.linen as nn

from tide_features import (
    N_CARD_FEATURES,
    N_GLOBAL_FEATURES,
    N_ACTION_FEATURES,
    CARD_ID_START,
    N_ID_SLOTS,
    N_CARD_POOL,
)


default_fc_init = nn.initializers.orthogonal(scale=jnp.sqrt(2))


class TideCardEncoder(nn.Module):
    """
    卡牌编码器：内容身份 embedding（原子级拆散）+ 状态特征 Dense 投影 → (80, channels)。

    身份 = [15..20] 原子哈希槽（执行序，槽位=顺序）+ [21] 组合结构 + [22] 关键词/tag/光环
    组合，逐槽 nn.Embed 查表加槽位位置标记后 masked-sum——同原子（如 造成伤害4）跨卡
    共享 embedding 行，相近效果在原子层重叠、语义直接迁移。
    状态维度（力量/费用/横置…）仍是扁平浮点 Dense 投影。两路相加后 LayerNorm。
    """
    channels: int = 128
    dtype: Optional[jnp.dtype] = None
    param_dtype: jnp.dtype = jnp.float32

    @nn.compact
    def __call__(self, x, mask=None):
        """
        Args:
            x: (batch, 80, 24) 卡牌特征（[15..23] = 内容身份下标九槽：6 原子 + 结构 + 组合 + 预留）
            mask: (batch, 80) bool 掩码（True=有效卡，False=空槽）

        Returns:
            f_cards: (batch, 80, channels) 编码后特征
            c_mask: (batch, 80) bool 掩码（True=空槽需要屏蔽）
        """
        c = self.channels

        # x[:, :, 0] 是 valid 标记（1=有效，0=空槽）
        valid = x[:, :, 0]  # (batch, 80)
        c_mask = jnp.asarray(valid == 0)  # True=空槽（下游池化均有 count≥1 护栏，无需占位槽）

        # 内容身份（原子级拆散）：逐槽 embedding 查表 + 槽位位置标记（原子槽的槽位=执行序，
        # 位置标记让「先抽牌后伤害」≠「先伤害后抽牌」）后 masked-sum——
        # 0 = 无该成分不参与求和；同原子跨卡共享 embedding 行
        ids = x[:, :, CARD_ID_START:CARD_ID_START + N_ID_SLOTS]  # (batch, 80, S)
        present = ids > 0
        embedding = nn.Embed(
            N_CARD_POOL, c,
            dtype=self.dtype, param_dtype=self.param_dtype,
        )(jnp.clip(ids, 0, N_CARD_POOL - 1).astype(jnp.int32))  # (batch, 80, S, channels)
        pos = nn.Embed(
            N_ID_SLOTS, c,
            dtype=self.dtype, param_dtype=self.param_dtype,
        )(jnp.arange(N_ID_SLOTS))  # (S, channels)
        identity = jnp.where(present[..., None], embedding + pos, 0.0).sum(axis=2)  # (batch, 80, channels)

        # 状态特征（[0..14]，剔除身份槽）Dense 投影 → channels
        rest = x[:, :, :CARD_ID_START]
        x_cards = nn.Dense(
            c,
            dtype=self.dtype,
            param_dtype=self.param_dtype,
            kernel_init=default_fc_init,
        )(rest)  # (batch, 80, channels)

        # 状态 + 身份合并
        x_cards = x_cards + identity
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
    动作编码器：Dense 投影 (max_actions, 6 + 2*channels) → (max_actions, channels)。

    输入 = 6 维动作特征 concat 来源/目标卡的编码（TideEncoder 按 sourceIndex/targetIndex
    gather，-1 = 无来源/打脸 → 遮蔽为 0）——动作的表示包含它指向的卡的状态，
    打分（Actor 点积）才有「这张攻击者/防守者」的特异性。
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

        # 动作打分能看见卡（P0-1）：按 sourceIndex/targetIndex gather 来源/目标卡编码，
        # 与 6 维动作特征 concat 后投影。-1 = 无来源/目标为玩家 → gather 结果遮蔽为 0。
        def gather_card(idx):
            present = idx >= 0
            gi = jnp.clip(idx, 0, cards_enc.shape[1] - 1).astype(jnp.int32)
            gathered = jnp.take_along_axis(cards_enc, gi[..., None], axis=1)  # (batch, A, channels)
            return jnp.where(present[..., None], gathered, 0.0)

        act_in = jnp.concatenate(
            [actions, gather_card(actions[:, :, 2]), gather_card(actions[:, :, 3])],
            axis=-1,
        )  # (batch, max_actions, 6 + 2*channels)
        actions_enc = action_encoder(act_in)       # (batch, max_actions, channels)

        # 动作掩码：actions[:, :, 0] 是 valid 标记（1=合法，0=非法）
        a_mask = actions[:, :, 0] == 0

        return {
            "cards": cards_enc,
            "global": global_enc,
            "actions": actions_enc,
            "c_mask": c_mask,
            "a_mask": a_mask,
        }
