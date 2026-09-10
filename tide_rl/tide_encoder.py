"""
Tide 编码器（路线 1：MLP/GRU actor-critic + 内容身份 embedding）。

- 卡身份三通路：
  1) 精确哈希槽 [15..22] → nn.Embed(N_CARD_POOL) 查表（含槽位位置标记）masked-sum——
     同原子跨卡共享行（内容寻址，C# CardIdentityService 与费用推算同管线产出哈希）。
  2) EffectType 槽 [23..28] → nn.Embed(N_EFFECT_TYPES)——参数无关，「造成4伤」与「造成5伤」
     同一行；新参数值不再纯新 token。
  3) 参数块 [29..]（6 槽 × ATOM_PARAM_DIM 浮点）→ per-slot 共享 Dense 投影 masked-sum——
     数值插值通路，幅度变化可从已训参数外推。
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
    TYPE_ID_START,
    N_TYPE_SLOTS,
    ATOM_PARAM_START,
    ATOM_PARAM_DIM,
    N_CARD_POOL,
    N_EFFECT_TYPES,
)


default_fc_init = nn.initializers.orthogonal(scale=jnp.sqrt(2))


class TideCardEncoder(nn.Module):
    """
    卡牌编码器：内容身份三通路 + 状态特征 Dense 投影 → (80, channels)。

    通路 1 精确身份 = [15..20] 原子哈希槽（执行序，槽位=顺序）+ [21] 组合结构 + [22] 关键词/
    tag/光环组合，逐槽 nn.Embed 查表加槽位位置标记后 masked-sum——同原子（如 造成伤害4）
    跨卡共享 embedding 行，相近效果在原子层重叠、语义直接迁移；对未见参数值查表记 0（静默）。
    通路 2 类型 = [23..28] EffectType 下标（参数无关共享行）；通路 3 参数 = [29..] 浮点块
    per-slot Dense（槽位间共享权重，存在性由类型下标门控）masked-sum——「造成5伤」可从
    「造成4伤」的类型行 + 参数线性响应插值。
    状态维度（力量/费用/横置…）仍是扁平浮点 Dense 投影。四路相加后 LayerNorm。
    """
    channels: int = 128
    dtype: Optional[jnp.dtype] = None
    param_dtype: jnp.dtype = jnp.float32

    @nn.compact
    def __call__(self, x, mask=None):
        """
        Args:
            x: (batch, 80, 71) 卡牌特征（[15..22] 精确身份 8 槽、[23..28] 类型 6 槽、[29..] 参数块）
            mask: (batch, 80) bool 掩码（True=有效卡，False=空槽）

        Returns:
            f_cards: (batch, 80, channels) 编码后特征
            c_mask: (batch, 80) bool 掩码（True=空槽需要屏蔽）
        """
        c = self.channels

        # x[:, :, 0] 是 valid 标记（1=有效，0=空槽）
        valid = x[:, :, 0]  # (batch, 80)
        c_mask = jnp.asarray(valid == 0)  # True=空槽（下游池化均有 count≥1 护栏，无需占位槽）

        # 通路 1：精确内容身份（原子级拆散）——逐槽 embedding 查表 + 槽位位置标记（原子槽的
        # 槽位=执行序，位置标记让「先抽牌后伤害」≠「先伤害后抽牌」）后 masked-sum——
        # 0 = 无该成分/未登记不参与求和；同原子跨卡共享 embedding 行
        ids = x[:, :, CARD_ID_START:CARD_ID_START + N_ID_SLOTS]  # (batch, 80, 8)
        present = ids > 0
        embedding = nn.Embed(
            N_CARD_POOL, c,
            dtype=self.dtype, param_dtype=self.param_dtype,
        )(jnp.clip(ids, 0, N_CARD_POOL - 1).astype(jnp.int32))  # (batch, 80, 8, channels)
        pos = nn.Embed(
            N_ID_SLOTS, c,
            dtype=self.dtype, param_dtype=self.param_dtype,
        )(jnp.arange(N_ID_SLOTS))  # (8, channels)
        identity = jnp.where(present[..., None], embedding + pos, 0.0).sum(axis=2)  # (batch, 80, channels)

        # 通路 2：EffectType 嵌入（参数无关共享行）+ 槽位位置标记（槽位=同一执行序）后 masked-sum
        types = x[:, :, TYPE_ID_START:TYPE_ID_START + N_TYPE_SLOTS]  # (batch, 80, 6)
        t_present = types > 0
        t_embedding = nn.Embed(
            N_EFFECT_TYPES, c,
            dtype=self.dtype, param_dtype=self.param_dtype,
        )(jnp.clip(types, 0, N_EFFECT_TYPES - 1).astype(jnp.int32))  # (batch, 80, 6, channels)
        t_pos = nn.Embed(
            N_TYPE_SLOTS, c,
            dtype=self.dtype, param_dtype=self.param_dtype,
        )(jnp.arange(N_TYPE_SLOTS))  # (6, channels)
        type_sum = jnp.where(t_present[..., None], t_embedding + t_pos, 0.0).sum(axis=2)

        # 通路 3：参数数值投影——(6, ATOM_PARAM_DIM) per-slot 共享 Dense → channels，
        # 槽位存在性由类型下标门控（类型 0 = 无该原子，参数块也不可信）后 masked-sum
        params = x[:, :, ATOM_PARAM_START:ATOM_PARAM_START + N_TYPE_SLOTS * ATOM_PARAM_DIM]
        params = params.reshape(x.shape[0], x.shape[1], N_TYPE_SLOTS, ATOM_PARAM_DIM)
        p_proj = nn.Dense(
            c,
            dtype=self.dtype,
            param_dtype=self.param_dtype,
            kernel_init=default_fc_init,
        )(params)  # (batch, 80, 6, channels)
        param_sum = jnp.where(t_present[..., None], p_proj, 0.0).sum(axis=2)

        # 状态特征（[0..14]，剔除全部身份槽）Dense 投影 → channels
        rest = x[:, :, :CARD_ID_START]
        x_cards = nn.Dense(
            c,
            dtype=self.dtype,
            param_dtype=self.param_dtype,
            kernel_init=default_fc_init,
        )(rest)  # (batch, 80, channels)

        # 状态 + 三路身份合并
        x_cards = x_cards + identity + type_sum + param_sum
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
                "cards_": (batch, 80, 71),
                "global_": (batch, 32),
                "actions_": (batch, max_actions, 6),
                "h_actions_": (batch, 32, 14) or None
            }

        Returns:
            encoded: dict with encoded features
        """
        cards = x["cards_"]      # (batch, 80, 71)
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
