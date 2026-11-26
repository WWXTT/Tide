"""
Tide 简化 Actor-Critic 模型（路线 1：MLP/GRU）。

复用 ygo-agent 的 RNN/Critic 骨干 + Tide 编码器。
简化版：不用 Transformer/FiLM，只用 GRU + MLP。

二期 S1（2026-11-26，docs/二期训练架构_实施计划.md §3 S1-3/S1-4）：
- Actor / Critic 的卡槽池化由「全部卡槽求均值」改为 **query-attention 池化**
  （query = global，TideRNNAgent 已并入 Dense(rnn_out)）——一期的均值池化把 191 槽压成
  1 个向量，「我有几张沃土、还差几费」这类计划信息不在池化里；
- logits 增加一项 **动作行 attend 卡槽**（动作行对全部卡槽的一次 attention 读出，不再只靠
  gather 源/目标两张卡）；
- 辅助头 aux = [剩余步数/512, timeout 概率, 到达链目标态]（训练期 MSE，不进 reward、不改最优策略；第三维=S3-3 2026-11-26）。

二期 S1b（2026-11-26，同计划 §3 S1-3b）牌库集合编码：v21 的己方牌库块（cards[154..183]，每种唯一卡
一槽 + [157] 张数）一直在观测里，但被上面的卡槽池化和其他 160 多个槽混在一起读。现在 Actor / Critic
**各自**再对这个 30 槽的块做一次 query=global 的 attention → deck_vec → Dense 并入自己的 context，
让「我满编了哪几种卡、单例几张」这种原型信号有独立通路；g[88..95]（开局牌库标签）仍在 global 里
原样保留。新通路的 key 投影与注入 Dense 都零初始化——起步时牌库路径恒为 0，S1 的起步口径不变。

部署面（S1-6，改本文件前必读）：ONNX 由 __call__ 导出，输出必须仍是
rstate_next / logits / value 三个；export_onnx.py 及全部评估/探针/蒸馏调用方都按 3 元组解包
agent.apply——所以辅助头不进 __call__ 的返回值，训练侧用 method=TideRNNAgent.forward_with_aux
取带 aux 的 4 元组。两条路径构造的参数树相同（__call__ 也构造辅助头，只是丢弃其输出，
导出图里被死码消除）。
"""

from typing import Optional
from functools import partial

import numpy as np
import jax.numpy as jnp
import flax
import flax.linen as nn

from tide_encoder import TideEncoder, DECK_BASE, DECK_END


default_fc_init = nn.initializers.orthogonal(scale=jnp.sqrt(2))

# attention 屏蔽用大负数（同 tide_encoder._NEG_INF：不用 finfo.min，全屏蔽行 softmax 仍有限）
_MASK = -1e9

# 辅助头（S1-4；S3-3 2026-11-26 扩第三维）：aux = [剩余步数 / AUX_REMAIN_STEPS_DIV,
# timeout 概率, 到达链目标态概率]。除数 512 为计划 §3 S1-4 定值；标签侧
#（rollout_common.aux 标签回填）与这里同用这一个常量，两侧不得各写一份。
# 第三维=「本局是否到达链目标态」（S3-3 aux，标签源 info.chainDone——绿=大哥落地 /
# 红=combo / 蓝=潮痕碑召唤）。旧 2 维 checkpoint 热启动：aux 头形状 2≠3 →
# merge_params_by_shape 落 fresh（零初始化，起步输出 (0,0.5,0.5)）。
AUX_DIM = 3
AUX_REMAIN_STEPS_DIV = 512.0


def _attend(query, values, valid, key_proj, c):
    """单头 attention 读出（手工 matmul→scale→mask→softmax→matmul，为 jax2onnx 稳定）。

    Args:
        query: (batch, Q, c) 查询向量——**必须是三维**。二维 query 进 matmul 会被 numpy 语义
                提升成 (1, batch, Q, c)，与 (batch, N, c) 的 key 广播出跨样本注意力
                （batch=1 时只表现为多出一维，batch>1 才静默串批），故此处静态断言拦住
        values: (batch, N, c) 值向量——**调用方须已把无效槽置 0**（否则空槽的
                LayerNorm(偏置) 向量会漏进读出）
        valid: (batch, N) bool（True=有效槽）
        key_proj: nn.Dense(c)——key 投影；零初始化时打分恒 0 → 有效槽上均匀注意力
                  = masked mean（与一期的均值池化逐值相同），注意力由训练打开
        c: 通道数（缩放 1/√c）

    Returns:
        (batch, Q, c) 有效槽向量的注意力加权和；全屏蔽时为 0。
    """
    assert query.ndim == 3 and values.ndim == 3, (
        f"_attend 要求三维 query/values，得到 query.ndim={query.ndim} values.ndim={values.ndim}")
    keys = key_proj(values)                                                # (B, N, c)
    scores = jnp.matmul(query, jnp.swapaxes(keys, -1, -2)) / jnp.sqrt(c)   # (B, Q, N)
    attn = nn.softmax(jnp.where(valid[:, None, :], scores, _MASK), axis=-1)
    return jnp.matmul(attn, values)                                        # (B, Q, c)


def _deck_read(values, valid, query, key_proj, c):
    """牌库集合读出（二期 S1b）：对己方牌库块 [DECK_BASE, DECK_END) 做一次 query → 牌库 的单头
    attention（_attend 的薄封装；牌库块下标见 tide_encoder「牌库块」段注）。

    Args:
        values: (batch, MAX_CARDS, c) 全部卡向量，**无效槽已置 0**（同 _attend）
        valid:  (batch, MAX_CARDS) bool
        query:  (batch, c) 查询向量（global，已并入 Dense(rnn_out)）
        key_proj: 牌库块的 key 投影；零初始化 → 有效牌库槽上均匀注意力 = 牌库槽均值
        c: 通道数

    Returns:
        (batch, c) deck_vec；牌库块全空（无牌库）时为 0。
    """
    return _attend(query[:, None, :], values[:, DECK_BASE:DECK_END],
                   valid[:, DECK_BASE:DECK_END], key_proj, c)[:, 0, :]


class TideActor(nn.Module):
    """
    简化 Actor（不用 Transformer/FiLM，直接 MLP）。

    架构：
    1. Encoder 编码 cards/global/actions
    2. 卡槽 query-attention 池化 + 牌库集合读出（S1b）+ global → MLP → 上下文
    3. 逐动作打分 → softmax

    参数树命名（构造顺序即命名）：Dense_0/Dense_1 = 上下文 MLP（与一期旧树同名同形状，
    --init-params 形状匹配合并可继承）；Dense_2 = 卡槽池化 key 投影、Dense_3 = 动作行
    attend 卡槽的 key 投影（二期 S1 新增，零初始化）；Dense_4 = 牌库集合 key 投影、
    Dense_5 = deck_vec 注入 context 的投影（二期 S1b 新增，零初始化——起步时牌库路径恒为 0）。
    """
    channels: int = 128
    dtype: Optional[jnp.dtype] = None
    param_dtype: jnp.dtype = jnp.float32

    @nn.compact
    def __call__(self, encoded):
        """
        Args:
            encoded: dict from TideEncoder {
                "cards": (batch, MAX_CARDS, channels),
                "global": (batch, channels),
                "actions": (batch, max_actions, channels),
                "c_mask": (batch, MAX_CARDS),
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

        cards = encoded["cards"]      # (batch, MAX_CARDS, channels)
        global_feat = encoded["global"]  # (batch, channels)
        actions = encoded["actions"]  # (batch, max_actions, channels)
        c_mask = encoded["c_mask"]    # (batch, MAX_CARDS)
        a_mask = encoded["a_mask"]    # (batch, max_actions)

        # 子模块按构造顺序命名（见类注释），与下方调用顺序无关
        ctx_d0 = mlp(c)
        ctx_d1 = mlp(c)
        pool_key = nn.Dense(
            c, dtype=self.dtype, param_dtype=self.param_dtype,
            kernel_init=nn.initializers.zeros,
        )
        act_key = nn.Dense(
            c, dtype=self.dtype, param_dtype=self.param_dtype,
            kernel_init=nn.initializers.zeros,
        )
        deck_key = nn.Dense(
            c, dtype=self.dtype, param_dtype=self.param_dtype,
            kernel_init=nn.initializers.zeros, bias_init=nn.initializers.zeros,
        )
        deck_proj = nn.Dense(
            c, dtype=self.dtype, param_dtype=self.param_dtype,
            kernel_init=nn.initializers.zeros, bias_init=nn.initializers.zeros,
        )

        # 空槽的值向量置 0（空槽的卡向量 = LayerNorm(偏置) 并非 0，不置 0 会漏进注意力读出）
        valid = ~c_mask                                        # (batch, MAX_CARDS)
        values = jnp.where(c_mask[..., None], 0.0, cards)

        # 卡槽 query-attention 池化（S1-3）：query = global（TideRNNAgent 已并入 Dense(rnn_out)），
        # 对 MAX_CARDS 槽做单头 attention → pooled；上下文 = global + pooled + 牌库项（一期是
        # global + 全槽均值——pool_key 零初始化时 pooled 恰等于该均值，继承权重起步口径不变）
        pooled = _attend(global_feat[:, None, :], values, valid, pool_key, c)[:, 0, :]  # (batch, channels)
        # 牌库集合（S1b）：对己方牌库块单独读一次 → deck_vec → 零初始化的 Dense 并入 context
        # （deck_proj=0 时这一项恒为 0；梯度第一步就推动 deck_proj，key 投影随后跟上）
        deck_vec = _deck_read(values, valid, global_feat, deck_key, c)    # (batch, channels)
        context = global_feat + pooled + deck_proj(deck_vec)

        # MLP 提取上下文特征
        context = nn.relu(ctx_d0(context))
        context = nn.relu(ctx_d1(context))

        # 逐动作打分，两项都 ÷√channels 温度缩放：
        #   ① dot(action_i, context)                 —— 一期原项
        #   ② dot(action_i, attn(action_i → cards)) —— 二期 S1 新增：动作行对全部卡槽的一次
        #      attention 读出（act_key 零初始化 → 有效槽均值，随训练学会按动作行取相关卡）
        # 温度缩放的原因（2026-09-09）：两侧均 LayerNorm（每维 ~N(0,1)），裸点积幅度 ≈ √c ≈ 11
        # → 初始化即近 argmax（实测初始熵 0.48 vs 10 合法动作均匀熵 2.3），7 个 update 内熵
        # 精确归零、探索死亡（三轮复现）。÷√c 让初始 logit 离散度 ≈ 1，softmax 从近均匀起步；
        # 策略变自信仍可通过 LayerNorm 增益正常长尖。
        card_ctx = _attend(actions, values, valid, act_key, c)      # (batch, max_actions, channels)
        logits = ((actions * context[:, None, :]).sum(axis=-1)
                  + (actions * card_ctx).sum(axis=-1)) / jnp.sqrt(c)  # (batch, max_actions)

        # 屏蔽非法动作（-1e9）
        logits = jnp.where(a_mask, _MASK, logits)

        return logits


class TideCritic(nn.Module):
    """
    简化 Critic（MLP）。

    输入全局上下文（global + 卡槽 query-attention 池化 + 牌库集合项〔S1b〕）→ MLP → 标量价值。

    参数树命名（构造顺序即命名）：Dense_0/Dense_1/Dense_2 = MLP（与一期旧树同名同形状，
    可继承）；Dense_3 = 卡槽池化 key 投影（二期 S1 新增，零初始化）；Dense_4 = 牌库集合
    key 投影、Dense_5 = deck_vec 注入 context 的投影（二期 S1b 新增，零初始化）。
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

        # 子模块按构造顺序命名（见类注释），与下方调用顺序无关
        ctx_d0 = mlp(c)
        ctx_d1 = mlp(c)
        head = mlp(1)
        pool_key = nn.Dense(
            c, dtype=self.dtype, param_dtype=self.param_dtype,
            kernel_init=nn.initializers.zeros,
        )
        deck_key = nn.Dense(
            c, dtype=self.dtype, param_dtype=self.param_dtype,
            kernel_init=nn.initializers.zeros, bias_init=nn.initializers.zeros,
        )
        deck_proj = nn.Dense(
            c, dtype=self.dtype, param_dtype=self.param_dtype,
            kernel_init=nn.initializers.zeros, bias_init=nn.initializers.zeros,
        )

        # 卡槽 query-attention 池化（S1-3，同 TideActor；pool_key 零初始化 = 一期的全槽均值）
        # + 牌库集合项（S1b，同 TideActor；deck_proj 零初始化 → 起步恒为 0）
        valid = ~c_mask
        values = jnp.where(c_mask[..., None], 0.0, cards)
        context = global_feat + _attend(
            global_feat[:, None, :], values, valid, pool_key, c)[:, 0, :]
        context = context + deck_proj(_deck_read(values, valid, global_feat, deck_key, c))

        # MLP
        x = nn.relu(ctx_d0(context))
        x = nn.relu(ctx_d1(x))
        x = head(x)  # (batch, 1)

        value = x.squeeze(-1)  # (batch,)

        return value


class TideRNNAgent(nn.Module):
    """
    Tide RNN Actor-Critic（简化版，GRU + MLP）。

    对应 ygo-agent 的 RNNAgent，但用简化编码器和简化 Actor/Critic。
    二期 S1-4 起带辅助头（aux = [剩余步数/512, timeout 概率, 到达链目标态]——第三维 S3-3 2026-11-26），经 forward_with_aux 取用。

    参数树命名（构造顺序即命名）：TideEncoder_0 / GRUCell_0 / Dense_0（rnn 输出并入 global；
    与一期旧树同名同形状）/ TideActor_0 / TideCritic_0；Dense_1 / Dense_2 = 辅助头（二期新增）。
    """
    channels: int = 128
    rnn_channels: int = 512
    rnn_type: str = "gru"  # "gru" or "lstm"
    dtype: Optional[jnp.dtype] = None
    param_dtype: jnp.dtype = jnp.float32

    @nn.compact
    def __call__(self, rstate, x, id_keep=None):
        """部署/评估入口：返回 3 元组（不带 aux）——ONNX 导出与全部既有调用方走这条
        （输出 rstate_next / logits / value 数量不变）。辅助头仍在本方法内构造（与
        forward_with_aux 同一棵参数树），aux 输出丢弃，导出图里被死码消除。

        Args / Returns: 见 forward_with_aux（少最后一项 aux）。
        """
        new_rstate, logits, value, _ = self.forward_with_aux(rstate, x, id_keep)
        return new_rstate, logits, value

    @nn.compact
    def forward_with_aux(self, rstate, x, id_keep=None):
        """训练用前向：带辅助头输出。

        Args:
            rstate: (h, c) or h — RNN 隐状态
            x: obs_dict（见 tide_encoder.TideEncoder）
            id_keep: 身份通路 dropout 乘子（None = 关闭；训练期 rollout 生成、更新期复用，
                     见 tide_encoder.TideCardEncoder）

        Returns:
            new_rstate, logits, value, aux
              logits: (batch, max_actions)
              value:  (batch,)
              aux:    (batch, AUX_DIM=3) = [剩余步数/512（线性）, timeout 概率（sigmoid）,
                      到达链目标态概率（sigmoid）——S3-3 2026-11-26 扩第三维]
        """
        c = self.channels

        # 1. 编码
        encoder = TideEncoder(c, self.dtype, self.param_dtype)
        encoded = encoder(x, id_keep=id_keep)

        # 2. GRU 输入：卡槽按槽序对半各取均值 + global（旧版只吃全部卡槽混合均值——
        #    全局量进不了记忆）。
        #    注意（2026-11-26 S1 注释勘误，代码口径未动）：自 v10 卡槽扩成「每人 77 槽×2 +
        #    尾块 35 + 场地 2」后，191//2=95 的对半切分已**不等于**己方/对方——前半 [0..94]
        #    含对方区的前 18 槽（[77..94]），后半 [95..190] 含对方余下槽 + 己方牌库块 + 场地。
        #    旧注释「己方 0..39 / 对方 40..79」是 80 槽时代的口径，已过期。本批不改 GRU 输入
        #    （改动会换掉可继承的 GRU 输入权重语义），如需按归属重切另立变更。
        cards = encoded["cards"]
        c_mask = encoded["c_mask"]

        def _masked_pool(enc, msk):
            summed = jnp.where(msk[..., None], 0, enc).sum(axis=1)
            count = jnp.maximum((~msk).sum(axis=1, keepdims=True), 1)
            return summed / count

        half = cards.shape[1] // 2
        rnn_in = jnp.concatenate(
            [
                _masked_pool(cards[:, :half], c_mask[:, :half]),    # 前半区 [0..half)
                _masked_pool(cards[:, half:], c_mask[:, half:]),    # 后半区 [half..MAX_CARDS)
                encoded["global"],                                 # 全局状态（生命/法力/回合…）
            ],
            axis=-1,
        )  # (batch, 3*channels)

        # 3. RNN
        if self.rnn_type == "gru":
            rnn_cell = nn.GRUCell(
                features=self.rnn_channels,
                dtype=self.dtype,
                param_dtype=self.param_dtype,
            )
            new_rstate, rnn_out = rnn_cell(rstate, rnn_in)
        elif self.rnn_type == "lstm":
            rnn_cell = nn.LSTMCell(
                features=self.rnn_channels,
                dtype=self.dtype,
                param_dtype=self.param_dtype,
            )
            new_rstate, rnn_out = rnn_cell(rstate, rnn_in)
        else:
            raise ValueError(f"Unknown rnn_type: {self.rnn_type}")

        # 4. RNN 输出并入 global 特征（Dense_0；Actor/Critic 的池化 query 即这一份）
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

        # 6. 辅助头（S1-4）：读与 Actor/Critic 同一份 global+Dense(rnn_out)。
        #    aux[0] 线性 = 剩余步数/512（可 >1，单局上限内不设上界）；aux[1] sigmoid = timeout 概率。
        #    只做辅助任务（训练 loss 加 aux_coef·MSE），不进 reward、不改最优策略；
        #    __call__ 路径下本头照样构造、输出丢弃。
        #    输出层零初始化：起步输出恒 (0, 0.5)，辅助损失从 (label_rem)² + (0.5−label_to)² 起步——
        #    正交初始化时线性输出量级远大于目标（实测 8 步局 aux_loss≈13），会以大幅梯度冲击主干。
        aux_hidden = nn.Dense(
            c, dtype=self.dtype, param_dtype=self.param_dtype, kernel_init=default_fc_init
        )(enhanced_global)
        aux_raw = nn.Dense(
            AUX_DIM, dtype=self.dtype, param_dtype=self.param_dtype,
            kernel_init=nn.initializers.zeros, bias_init=nn.initializers.zeros,
        )(nn.relu(aux_hidden))
        # 维 0 线性（剩余步数），维 1..AUX_DIM-1 sigmoid（概率型：timeout / 到达链目标态）
        aux = jnp.concatenate([aux_raw[:, :1], nn.sigmoid(aux_raw[:, 1:])], axis=-1)  # (batch, AUX_DIM)

        return new_rstate, logits, value, aux


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


def merge_params_by_shape(target, source, log=print):
    """--init-params 的形状匹配合并（二期 S1-5；train_tide_complete 的热启动入口）。

    一期走 flax.serialization.from_bytes(params, bytes) 的**严格结构匹配**——网络一加新模块，
    旧 run 的权重就整体拒载。现行口径：新树逐叶子判断——
      - 与旧树**同名且同形状同 dtype** → 载入旧值（loaded：一期已训的模块可热启）；
      - 旧树没有的叶子 → 保留新树初始化（fresh：新架构新增模块，必须重训）；
      - 同名但形状/dtype 已变 → 保留新树初始化（reshaped）；
      - 旧树有、新树没有 → 丢弃（dropped：新架构已退役的叶子）。
    返回 (merged_params, report)；report 四张清单按模块分组打印，「旧模块可热启 / 新模块必须
    重训」一眼可辨。零匹配（loaded 为空）直接 SystemExit——热启动一个叶子都没载入多半是
    --init-params 指错了文件，静默从随机权重开训会误导读数。

    语义边界：同名同形状只保证**张量形状兼容**，不保证语义逐值等价。S1 表示层刻意让语义
    未变的叶子沿用一期路径（身份/类型/槽位 Embed、参数/状态 Dense、卡向量 LayerNorm、GRU、
    Actor/Critic MLP、动作/全局编码器、rnn→global 的 Dense_0），新模块全部落在新路径下
    （AtomSetEncoder_0、alpha、辅助头 Dense_1/Dense_2、Actor Dense_2/Dense_3、Critic Dense_3）。
    日后若改了某个同名叶子的**输入含义**，必须同时改它的名字或形状，否则旧权重会被静默错载。

    Args:
        target: 新架构的初始化参数树（带 "params" 集合的变量树）
        source: 旧 checkpoint 反序列化出的变量树（同形态）
        log:    输出函数（默认 print）

    Returns:
        (merged_params, report)：report = {loaded, fresh, reshaped, dropped,
        n_total_params, n_loaded_params, n_new_params}（路径为 "/" 分隔的扁平键）
    """
    flat_target = flax.traverse_util.flatten_dict(target, sep="/")
    flat_source = flax.traverse_util.flatten_dict(source, sep="/")

    merged = {}
    loaded, fresh, reshaped = [], [], []
    for path, new_leaf in flat_target.items():
        old_leaf = flat_source.get(path)
        new_shape = tuple(new_leaf.shape)
        if old_leaf is None:
            merged[path] = new_leaf
            fresh.append(path)
        elif tuple(old_leaf.shape) != new_shape or old_leaf.dtype != new_leaf.dtype:
            merged[path] = new_leaf
            reshaped.append((path, tuple(old_leaf.shape), new_shape))
        else:
            merged[path] = jnp.asarray(old_leaf)
            loaded.append(path)
    dropped = [p for p in flat_source if p not in flat_target]

    def n_params(paths):
        return sum(int(np.prod(flat_target[p].shape)) if flat_target[p].shape else 1 for p in paths)

    def module_of(path):
        return path.rsplit("/", 1)[0]

    def print_group(title, paths):
        log(f"[init] {title}：{len(paths)} 叶子 / {n_params(paths):,} 参数")
        by_module = {}
        for p in paths:
            by_module.setdefault(module_of(p), []).append(p.rsplit("/", 1)[1])
        for m in sorted(by_module):
            log(f"[init]       {m}: {', '.join(sorted(by_module[m]))}")

    total = sum(int(v.size) for v in flat_target.values())
    n_loaded = n_params(loaded)
    n_new = n_params(fresh) + n_params(p for p, _, _ in reshaped)
    print_group("loaded（同名同形状，旧权重继承）", loaded)
    print_group("fresh（新架构新增，保留初始化——必须重训）", fresh)
    if reshaped:
        log(f"[init] reshaped（同名但形状/dtype 已变，保留初始化）：{len(reshaped)} 叶子")
        for path, old_shape, new_shape in reshaped:
            log(f"[init]       {path}  旧 {old_shape} → 新 {new_shape}")
    if dropped:
        log(f"[init] 旧树多余（新架构不存在，丢弃）：{len(dropped)} 叶子 / "
            f"{sum(int(np.prod(flat_source[p].shape)) if flat_source[p].shape else 1 for p in dropped):,} 参数")
        for p in dropped:
            log(f"[init]       {p}  {tuple(flat_source[p].shape)}")
    log(f"[init] 合并汇总：继承 {n_loaded:,} / {total:,} 参数"
        f"（{100.0 * n_loaded / total:.1f}%），新训 {n_new:,}（{100.0 * n_new / total:.1f}%）")
    if not loaded:
        raise SystemExit(
            "[!] --init-params 旧树与新树零匹配（loaded=0）——多半是指错了文件，热启动无意义；"
            "请确认给的是 Tide 训练 run 的 params.msgpack")

    report = {
        "loaded": list(loaded), "fresh": list(fresh),
        "reshaped": [(p, list(a), list(b)) for p, a, b in reshaped],
        "dropped": list(dropped),
        "n_total_params": total, "n_loaded_params": n_loaded, "n_new_params": n_new,
    }
    merged_params = flax.traverse_util.unflatten_dict(merged, sep="/")
    return merged_params, report
