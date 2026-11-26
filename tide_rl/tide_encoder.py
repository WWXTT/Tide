"""
Tide 编码器（路线 1：MLP/GRU actor-critic + 内容身份 embedding）。

卡内容身份 = 三条通路（槽位下标全部派生自 tide_features 常量，契约见
tide_rl/docs/action_space_v3_contract.md）：
  1) 精确哈希槽 [15..28]（N_ID_SLOTS=14 = [15..26] 十二个原子内容哈希 + [27] 组合结构 +
     [28] 关键词/tag/光环组合）→ nn.Embed(N_CARD_POOL) 查表（C# CardIdentityService 与费用
     推算同管线产出哈希；参数敏感：「造成4伤」≠「造成5伤」是两行）。同原子实例跨卡共享
     embedding 行——内容寻址。
  2) 类型槽 [29..40]（N_TYPE_SLOTS=12）→ nn.Embed(N_EFFECT_TYPES)——参数无关，「造成4伤」与
     「造成5伤」同一行；新参数值不再纯新 token。
  3) 参数块 [41..]（12 槽 × ATOM_PARAM_DIM 浮点）→ per-slot 共享 Dense——数值插值通路，
     幅度变化可从已训参数外推。

二期 S1（2026-11-26，docs/二期训练架构_实施计划.md §3 S1-1/S1-2）：一期把三路各自 masked-sum
后相加——原子的顺序/重复/参数耦合全丢在求和里。现行口径 = **原子集合编码**（AtomSetEncoder）：
  token_s = α_id·身份行 + α_type·类型行 + α_param·参数投影 + 槽位位置（s=0..11）
  → 1 层单头 self-attention + 残差 + LayerNorm → masked_mean‖masked_max → Dense → 集合向量
  [27] 组合结构 / [28] 关键词仍走身份 Embed（不进 token），直接加到卡向量。
  α = 3·softmax(可学习标量)，初值 (1,1,1)——训练后可直接读出「模型多信身份 vs 类型 vs 参数」。
  身份通路训练期 dropout（乘子 id_keep，eval 关闭），逼网络依赖类型/参数通路。

部署约束（改本文件前必读）：ONNX 由 tide_agent.TideRNNAgent.__call__ 导出，C# OnnxTidePolicy
只按输入名/dims/池表校验、不认算子；attention 一律手工 matmul→scale→mask→softmax→matmul
（不用 nn.MultiHeadDotProductAttention，为 jax2onnx 稳定）；屏蔽用 -1e9 而非 finfo.min
（全屏蔽行的 softmax 仍有限，不出 NaN）。

- 动作编码：N_ACTION_FEATURES 维动作特征 + 按 source/targetIndex gather 的来源/目标卡编码
  concat 后投影——动作打分能读到它指向的那张卡。source/target 槽位下标在编码器内归一化到
  [0,1)（-1=无 → 0）；归一化必须在模型内做，保证导出的 ONNX 与 C# 部署侧喂原始特征逐值一致
  （部署奇偶性铁律：特征变换只准在图内或 C# 侧）。
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
    MAX_CARDS,
    ACTION_F_SOURCE,
    ACTION_F_TARGET,
    CARD_ID_START,
    N_ID_SLOTS,
    TYPE_ID_START,
    N_TYPE_SLOTS,
    ATOM_PARAM_START,
    ATOM_PARAM_DIM,
    N_CARD_POOL,
    N_EFFECT_TYPES,
)

# token 槽 = 类型槽/参数槽对齐的原子槽（[0..N_TYPE_SLOTS)），身份哈希取同序的原子槽
# （[15..26]）；身份槽尾部的 [27] 结构 / [28] 关键词不进 token。布局扩容若打破该对齐，
# 在导入期即失败，而不是静默错槽。
assert N_TYPE_SLOTS <= N_ID_SLOTS, "token 槽数不得超过身份槽（结构/关键词占身份槽尾部）"

# ---- 牌库块（二期 S1b，2026-11-26）----
# v21 起己方牌库以「每种唯一卡一槽 + [157] 张数」尾接在卡槽数组里（C# TideObservation：
# MaxCardsPerPlayer=77、DeckSlots=30、DeckBase = MaxCardsPerPlayer*2 = 154——全局块，不走
# ZoneLayout 的每玩家偏移，恒为决策座次视角的己方；牌库卡 zoneKind=6）。这些槽位本来就在
# cards_ 里并照常过 TideCardEncoder；S1b 只是让 Actor/Critic 把它当一个集合单独读
# （tide_agent._deck_read）。下标按 C# 派生式同构写出，不另立魔数；尾块算术在导入期断言——
# 牌库块之后依次是 牌库顶 1 + 对方被展示 4 + 场地 2，加起来必须正好到 MAX_CARDS。
CARDS_PER_PLAYER = 77
DECK_SLOTS = 30
DECK_BASE = CARDS_PER_PLAYER * 2       # [154]
DECK_END = DECK_BASE + DECK_SLOTS      # 184（不含）
_DECK_TOP_SLOTS, _OPP_DECK_EXPOSED_SLOTS, _FIELD_SLOTS = 1, 4, 2
assert DECK_END + _DECK_TOP_SLOTS + _OPP_DECK_EXPOSED_SLOTS + _FIELD_SLOTS == MAX_CARDS, (
    "牌库块下标与 MAX_CARDS 尾块算术不一致（C# TideObservation 的 DeckBase/DeckSlots/尾块变了？）")

# attention 屏蔽用大负数：不用 finfo.min——全屏蔽行 softmax(全 -1e9) 退化为均匀分布而非 0/0
_NEG_INF = -1e9

default_fc_init = nn.initializers.orthogonal(scale=jnp.sqrt(2))


class AtomSetEncoder(nn.Module):
    """
    原子集合编码（二期 S1-1）：一张卡的 12 个原子 token → 1 层单头 self-attention
    （手工 matmul→scale→mask→softmax→matmul）+ 残差 + LayerNorm →
    masked_mean‖masked_max → Dense → 集合向量。

    参数树命名（构造顺序即命名，--init-params 形状匹配合并按路径对账）：
      Dense_0=q 投影 / Dense_1=k 投影 / Dense_2=v 投影 / LayerNorm_0 / Dense_3=池化投影
    ——全部是二期新增模块，一期权重无可继承项（必须重训）。

    初始化口径：k 投影零初始化 → 打分恒 0 → 注意力在有效 token 上均匀。即 t=0 时本模块
    等价于「token-wise Dense + masked mean‖max」（计划 §3 的降级形态），注意力权重由训练
    打开；k=0 时梯度非零（均匀权重下 d(out)/d(score_j) ∝ v_j − out），可学。
    """
    channels: int = 128
    dtype: Optional[jnp.dtype] = None
    param_dtype: jnp.dtype = jnp.float32

    @nn.compact
    def __call__(self, tokens, present):
        """
        Args:
            tokens: (batch, MAX_CARDS, N_TYPE_SLOTS, channels) 原子 token（空原子位已置 0）
            present: (batch, MAX_CARDS, N_TYPE_SLOTS) bool（True=该原子存在，参与注意力和池化）

        Returns:
            (batch, MAX_CARDS, channels) 集合向量；全空的卡（无原子）返回池化投影的偏置项
            （有限值，不产生 NaN/inf）。
        """
        c = self.channels
        dense = partial(
            nn.Dense,
            dtype=self.dtype,
            param_dtype=self.param_dtype,
        )
        # 子模块按构造顺序命名（见类注释），与下方调用顺序无关
        q_proj = dense(c, kernel_init=default_fc_init)
        k_proj = dense(c, kernel_init=nn.initializers.zeros)
        v_proj = dense(c, kernel_init=default_fc_init)
        post_norm = nn.LayerNorm(use_scale=True, use_bias=True, dtype=self.dtype)
        pool_proj = dense(c, kernel_init=default_fc_init)

        # 单头 self-attention：scores = q·kᵀ/√c，键侧屏蔽空原子位
        q = q_proj(tokens)                                              # (B, N, S, c)
        k = k_proj(tokens)
        v = v_proj(tokens)
        scores = jnp.matmul(q, jnp.swapaxes(k, -1, -2)) / jnp.sqrt(c)   # (B, N, S, S)
        key_ok = present[..., None, :]                                  # (B, N, 1, S)
        attn = nn.softmax(jnp.where(key_ok, scores, _NEG_INF), axis=-1)
        attended = jnp.matmul(attn, v)                                  # (B, N, S, c)
        out = post_norm(tokens + attended)                              # 残差 + LayerNorm
        out = jnp.where(present[..., None], out, 0.0)                   # 空原子位不进池化

        # 池化 = masked_mean ‖ masked_max（count≥1 护栏防除 0；全空集合的 max 的大负数归 0）
        p = present[..., None]                                          # (B, N, S, 1)
        count = jnp.maximum(present.sum(axis=-1, keepdims=True), 1).astype(out.dtype)  # (B, N, 1)
        mean = out.sum(axis=-2) / count                                  # (B, N, c)
        peak = jnp.where(p, out, _NEG_INF).max(axis=-2)                 # (B, N, c)
        peak = jnp.where(present.any(axis=-1, keepdims=True), peak, 0.0)
        return pool_proj(jnp.concatenate([mean, peak], axis=-1))        # (B, N, c)


class TideCardEncoder(nn.Module):
    """
    卡牌编码器：内容身份（原子集合编码 + 结构/关键词身份行）+ 状态特征 Dense 投影 →
    (MAX_CARDS, channels)，末位 LayerNorm。

    二期 S1 起三路身份不再 masked-sum：每个原子槽 s 拼成一个 token
        token_s = α_id·身份行[s] + α_type·类型行[s] + α_param·Dense(参数块[s]) + 槽位位置(s)
    （存在性 mask = 类型>0 或 哈希>0），交给 AtomSetEncoder 得到集合向量；[27] 组合结构哈希 /
    [28] 关键词哈希不进 token，仍走身份 Embed + 槽位位置后直接加到卡向量。

    三路权重（S1-2）：α_id/α_type/α_param 为可学习标量，w = 3·softmax(α)，初值 (1,1,1)
    ——α_id 作用于全部身份 Embed 查表（12 个原子 token + 结构 + 关键词）。
    读出：tide_encoder.pathway_weights(params)。

    身份通路 dropout（S1-2）：训练期对身份 Embed 查表逐槽乘 id_keep（保留 = 1/(1-p)、
    丢弃 = 0，期望保持），eval/部署不传（恒等）。掩码由 rollout 生成并随样本记录、更新期
    复用同一个（而不是更新期重新采样）——同一步的采样 logprob 与首个 epoch 的重算 logprob
    同口径，PPO ratio 精确为 1；否则 ratio 从第一个 epoch 起就带 dropout 噪声。

    参数树命名（构造顺序即命名）：Embed_0=身份 / Embed_1=槽位位置 / Embed_2=类型 /
    Dense_0=参数投影 / Dense_1=状态特征 / AtomSetEncoder_0 / LayerNorm_0——前五个与末位
    LayerNorm 和一期旧树同名同形状，--init-params 形状匹配合并可继承；AtomSetEncoder_0 为
    二期新增。一期 Embed_3（类型槽位置标记）已退役（2026-11-26：token 槽位与身份槽同序，
    位置标记只留 Embed_1；旧权重该叶子在合并报告里列为「旧树多余」）。
    """
    channels: int = 128
    dtype: Optional[jnp.dtype] = None
    param_dtype: jnp.dtype = jnp.float32

    @nn.compact
    def __call__(self, x, mask=None, id_keep=None):
        """
        Args:
            x: (batch, MAX_CARDS, N_CARD_FEATURES) 卡牌特征（v21：191×158；[15..28] 精确身份
               14 槽、[29..40] 类型 12 槽、[41..148] 参数块、[149..] 尾部状态维）。
               x[:, :, 0] 是 valid 标记（1=有效，0=空槽）
            mask: 兼容旧签名的占位参数，未使用——有效位一律取 x[:, :, 0]
            id_keep: (batch, MAX_CARDS, N_ID_SLOTS) 身份通路乘子（float，已含 1/(1-p) 缩放）；
                     None = 关闭 dropout（eval / ONNX 部署）

        Returns:
            f_cards: (batch, MAX_CARDS, channels) 编码后特征
            c_mask: (batch, MAX_CARDS) bool 掩码（True=空槽；下游池化均有 count≥1 护栏）
        """
        c = self.channels
        n_atom = N_TYPE_SLOTS

        # x[:, :, 0] 是 valid 标记（1=有效，0=空槽）
        valid = x[:, :, 0]  # (batch, MAX_CARDS)
        c_mask = jnp.asarray(valid == 0)  # True=空槽

        ids = x[:, :, CARD_ID_START:CARD_ID_START + N_ID_SLOTS]        # (batch, MAX_CARDS, 14)
        types = x[:, :, TYPE_ID_START:TYPE_ID_START + N_TYPE_SLOTS]    # (batch, MAX_CARDS, 12)
        params = x[:, :, ATOM_PARAM_START:ATOM_PARAM_START + N_TYPE_SLOTS * ATOM_PARAM_DIM]
        params = params.reshape(x.shape[0], x.shape[1], N_TYPE_SLOTS, ATOM_PARAM_DIM)

        # 子模块按构造顺序命名（见类注释）。P-1（2026-11-19 稳定下标）：下标由 C# SplitMix64
        # 纯派生，表大小随 tide_features.N_CARD_POOL / N_EFFECT_TYPES 自动同步；越界下标
        # clip 到表内（空槽/未登记=0 行）。
        id_embedding = nn.Embed(
            N_CARD_POOL, c,
            dtype=self.dtype, param_dtype=self.param_dtype,
        )
        slot_pos_embedding = nn.Embed(
            N_ID_SLOTS, c,
            dtype=self.dtype, param_dtype=self.param_dtype,
        )
        type_embedding = nn.Embed(
            N_EFFECT_TYPES, c,
            dtype=self.dtype, param_dtype=self.param_dtype,
        )
        param_proj = nn.Dense(
            c,
            dtype=self.dtype,
            param_dtype=self.param_dtype,
            kernel_init=default_fc_init,
        )
        state_proj = nn.Dense(
            c,
            dtype=self.dtype,
            param_dtype=self.param_dtype,
            kernel_init=default_fc_init,
        )

        # 三路权重 w = 3·softmax(α)，初值 (1,1,1)
        w = nn.softmax(self.param("alpha", nn.initializers.zeros, (3,))) * 3.0
        w_id, w_type, w_param = w[0], w[1], w[2]

        # 身份通路：逐槽查表（14 行：12 原子 + 结构 + 关键词）+ 训练期 dropout 乘子
        id_rows = id_embedding(jnp.clip(ids, 0, N_CARD_POOL - 1).astype(jnp.int32))  # (B,N,14,c)
        if id_keep is not None:
            id_rows = id_rows * id_keep[..., None]

        # token = α_id·身份行 + α_type·类型行 + α_param·参数投影 + 槽位位置；mask = 类型>0 或 哈希>0
        # （v21 战斗底盘：[10]/[11]=攻击/守卫资格原子，三路平行，同样进 token）
        atom_present = (types > 0) | (ids[..., :n_atom] > 0)                           # (B,N,12)
        type_rows = type_embedding(jnp.clip(types, 0, N_EFFECT_TYPES - 1).astype(jnp.int32))
        param_rows = param_proj(params)                                                # (B,N,12,c)
        pos_rows = slot_pos_embedding(jnp.arange(N_ID_SLOTS))                          # (14,c)
        token = (w_id * id_rows[..., :n_atom, :] + w_type * type_rows
                 + w_param * param_rows + pos_rows[None, None, :n_atom, :])
        token = jnp.where(atom_present[..., None], token, 0.0)
        set_vec = AtomSetEncoder(
            self.channels, self.dtype, self.param_dtype,
        )(token, atom_present)                                                         # (B,N,c)

        # [27] 组合结构 / [28] 关键词：仍走身份 Embed（不进 token），加槽位位置后加到卡向量
        tail_present = ids[..., n_atom:] > 0                                           # (B,N,2)
        tail_vec = jnp.where(
            tail_present[..., None],
            w_id * id_rows[..., n_atom:, :] + pos_rows[None, None, n_atom:, :],
            0.0,
        ).sum(axis=2)                                                                  # (B,N,c)

        # 状态特征（[0..14]，剔除全部身份槽）+ 尾部追加维（[149..] 坐标/zoneKind/死亡计数/
        # 团结邻接/引擎奖励摘要/同区张数）Dense 投影。尾部切片按参数块终点起算——契约在卡
        # 特征末尾追加新维时自动纳入，不静默丢弃。
        rest = jnp.concatenate(
            [x[:, :, :CARD_ID_START],
             x[:, :, ATOM_PARAM_START + N_TYPE_SLOTS * ATOM_PARAM_DIM:]],
            axis=-1)

        # 状态特征 + 原子集合 + 结构/关键词身份 合并后 LayerNorm
        x_cards = state_proj(rest) + set_vec + tail_vec
        x_cards = nn.LayerNorm(use_scale=True, use_bias=True, dtype=self.dtype)(x_cards)

        return x_cards, c_mask


def pathway_weights(params):
    """读出三路权重 3·softmax(α) → (w_id, w_type, w_param)（float 三元组，和恒为 3）。

    Args:
        params: TideRNNAgent 的变量树（TrainState.params 形态，即带 "params" 集合）。

    二期 S1-2：训练后用它直接读「模型多信身份 vs 类型 vs 参数」。初值 (1, 1, 1)。
    """
    node = params
    for key in ("params", "TideEncoder_0", "TideCardEncoder_0", "alpha"):
        node = node[key]
    w = 3.0 * nn.softmax(jnp.asarray(node))
    return float(w[0]), float(w[1]), float(w[2])


class TideGlobalEncoder(nn.Module):
    """
    全局状态编码器：直接 Dense 投影 (N_GLOBAL_FEATURES,) → (channels,)。

    不做 YGO 的 LP/turn/phase/计数 分桶 embedding。维度沿革（v21 现值见
    tide_features.N_GLOBAL_FEATURES 段注）口径不变：始终整体 Dense。
    """
    channels: int = 128
    dtype: Optional[jnp.dtype] = None
    param_dtype: jnp.dtype = jnp.float32

    @nn.compact
    def __call__(self, x):
        """
        Args:
            x: (batch, N_GLOBAL_FEATURES) 全局特征

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
    动作编码器：Dense 投影 (max_actions, N_ACTION_FEATURES + 2*channels) → (max_actions, channels)。

    输入 = N_ACTION_FEATURES 维动作特征 concat 来源/目标卡的编码（TideEncoder 按
    sourceIndex/targetIndex gather，-1 = 无来源/玩家目标 → 遮蔽为 0；玩家目标由 targetKind
    区分）——动作的表示包含它指向的卡的状态，打分（Actor 点积）才有「这张攻击者/防守者」
    的特异性。
    二期 S1-3 起动作向量还作为「动作行 attend 卡槽」的 query/value（见 tide_agent.TideActor）。
    """
    channels: int = 128
    dtype: Optional[jnp.dtype] = None
    param_dtype: jnp.dtype = jnp.float32

    @nn.compact
    def __call__(self, x):
        """
        Args:
            x: (batch, max_actions, N_ACTION_FEATURES + 2*channels) 动作特征 + 来源/目标卡编码

        Returns:
            (batch, max_actions, channels)
        """
        # 直接 Dense 投影
        x = nn.Dense(
            self.channels,
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

    输入 obs_dict → 三路编码（cards / global / actions）+ 两个掩码。
    """

    channels: int = 128
    dtype: Optional[jnp.dtype] = None
    param_dtype: jnp.dtype = jnp.float32

    @nn.compact
    def __call__(self, x, id_keep=None):
        """
        Args:
            x: obs_dict = {
                "cards_": (batch, MAX_CARDS, N_CARD_FEATURES),       # v21：(B, 191, 158)
                "global_": (batch, N_GLOBAL_FEATURES),               # v21：(B, 100)
                "actions_": (batch, max_actions, N_ACTION_FEATURES), # v22：(B, 512, 26)
                "h_actions_": (batch, N_HISTORY_ACTIONS, H_ACTIONS_FEATS) or None  # 暂未实现，传 None
            }
            id_keep: 见 TideCardEncoder（None = 关闭身份 dropout）

        Returns:
            encoded: dict with keys cards / global / actions / c_mask / a_mask
        """
        cards = x["cards_"]      # (batch, MAX_CARDS, N_CARD_FEATURES)
        global_ = x["global_"]   # (batch, N_GLOBAL_FEATURES)
        actions = x["actions_"]  # (batch, max_actions, N_ACTION_FEATURES)

        # 编码
        card_encoder = TideCardEncoder(self.channels, self.dtype, self.param_dtype)
        global_encoder = TideGlobalEncoder(self.channels, self.dtype, self.param_dtype)
        action_encoder = TideActionEncoder(self.channels, self.dtype, self.param_dtype)

        cards_enc, c_mask = card_encoder(cards, id_keep=id_keep)  # (B, MAX_CARDS, c), (B, MAX_CARDS)
        global_enc = global_encoder(global_)       # (batch, channels)
        # (TideRNNAgent 会在 RNN 之后把 Dense(rnn_out) 并进这个 global)

        # 动作打分能看见卡：按 sourceIndex/targetIndex gather 来源/目标卡编码，与动作特征
        # concat 后投影。-1 = 无来源/目标为玩家 → gather 结果遮蔽为 0（玩家目标语义由
        # targetKind 特征承载）。槽位下标归一化必须在**模型内**做（(idx+1)/(MAX_CARDS+1) →
        # [0,1)，-1→0）——导出的 ONNX 与 C# 部署侧喂原始特征才逐值一致（部署奇偶性：变换
        # 只准在图内）。jnp.asarray：obs 可能是 numpy 数组（.at[].set() 只对 jax 数组可用）
        src_idx = jnp.asarray(actions[:, :, ACTION_F_SOURCE])
        tgt_idx = jnp.asarray(actions[:, :, ACTION_F_TARGET])
        actions_feat = jnp.asarray(actions)
        actions_feat = actions_feat.at[..., ACTION_F_SOURCE].set((src_idx + 1.0) / (MAX_CARDS + 1.0))
        actions_feat = actions_feat.at[..., ACTION_F_TARGET].set((tgt_idx + 1.0) / (MAX_CARDS + 1.0))

        def gather_card(idx):
            present = idx >= 0
            gi = jnp.clip(idx, 0, cards_enc.shape[1] - 1).astype(jnp.int32)
            gathered = jnp.take_along_axis(cards_enc, gi[..., None], axis=1)  # (batch, A, channels)
            return jnp.where(present[..., None], gathered, 0.0)

        act_in = jnp.concatenate(
            [actions_feat, gather_card(src_idx), gather_card(tgt_idx)],
            axis=-1,
        )  # (batch, max_actions, N_ACTION_FEATURES + 2*channels)
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
