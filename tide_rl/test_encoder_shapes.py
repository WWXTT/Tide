"""二期 S1 表示层自测（形状 / 掩码 / 有限性 / 梯度 / batch 独立性 / 标签回填 / 权重合并）。

对应 docs/二期训练架构_实施计划.md §3 验收表 A1（2026-11-26）：
  前向形状 (B,191,128)、mask 生效、输出有限、jax.grad 非 NaN。
不依赖 Unity；主段也不依赖 ygo-agent——直接 `python tide_rl/test_encoder_shapes.py` 即可。
仅 §11（PPO 更新链路）要 import train_tide_complete → ygoai，缺依赖时打印 SKIPPED 而非失败；
启用方式：PYTHONPATH 追加 <训练副本根>/ygo-agent-main（主仓根没有该目录）。

分段：
  [1]  布局常量自洽
  [2]  卡编码器 / 总编码器 / Agent 前向形状；__call__ 与 forward_with_aux 同树同输出
  [3]  掩码生效（缺席原子位 / 空卡槽 / 非法动作 / 集合编码器内部 mask）
  [4]  初始化等价性：池化 key 零初始化 ⇒ query-attention 池化 = 一期的 masked mean，
       logits = (dot(a, ctx) + dot(a, 有效槽均值)) / √c
  [5]  batch 内独立（B=3；查跨样本广播——二维 query 进 matmul 会静默串批）
  [6]  输出有限（全空卡槽 / 全非法动作 / 极端参数 / 身份 dropout 乘子）
  [7]  jax.grad 有限，且二期新增模块都拿得到非零梯度
  [8]  三路权重 α：初值 (1,1,1)、和为 3
  [9]  身份 dropout 乘子：全 1 = 关闭；全 0 = 身份通路整体置零会改变输出
  [10] 辅助头标签按局回填 + dropout 掩码记录（假环境，两窗口，含跨窗口续跑的局）
  [11] PPO 更新链路（合成批）：复用采样掩码 ⇒ ratio 首个 epoch 精确为 1 —— 可选
  [12] 形状匹配合并：loaded / fresh / reshaped / dropped / 零匹配拒载
  [13] 初始熵（10 个合法动作时接近均匀，探索不从 argmax 起步）
  [14] 牌库集合编码（S1b）：deck_vec 随牌库块变化、不受其他槽/空牌库槽影响、注入层零初始化
       时牌库路径恒为 0、梯度可达、牌库块下标与 MAX_CARDS 尾块算术对账
"""

import sys

import numpy as np
import jax
import jax.numpy as jnp
import flax
import flax.linen as nn

from tide_features import (
    MAX_CARDS, MAX_ACTIONS, N_CARD_FEATURES, N_GLOBAL_FEATURES, N_ACTION_FEATURES,
    CARD_ID_START, N_ID_SLOTS, TYPE_ID_START, N_TYPE_SLOTS, ATOM_PARAM_START, ATOM_PARAM_DIM,
    N_CARD_POOL, N_EFFECT_TYPES, ACTION_TYPES, sample_input, init_rstate,
)
from tide_encoder import (
    TideCardEncoder, TideEncoder, AtomSetEncoder, pathway_weights,
    DECK_BASE, DECK_END, DECK_SLOTS,
)
from tide_agent import (
    TideActor, TideCritic, TideRNNAgent, create_tide_agent,
    merge_params_by_shape, AUX_REMAIN_STEPS_DIV, _deck_read,
)
from rollout_common import rollout_single_env, compute_advantages_simple

CHANNELS = 128
TAIL_START = ATOM_PARAM_START + N_TYPE_SLOTS * ATOM_PARAM_DIM  # 149：参数块之后的尾部状态维起点
CARD_F_ZONE, CARD_F_POS_X = 151, 149     # v21 卡特征尾部下标（zoneKind / 格坐标 x；沿革见 tide_features）
CARD_F_COUNT = N_CARD_FEATURES - 1       # [157] countInZone（同区张数；己方牌库块=该唯一卡的张数）
# 二期 S1 + S1b + S2a 参数量（一期 4,188,545：−Embed_3 1,536 + S1 148,997 + S1b 66,048
# + S2a 768 = 动作编码器输入 20→26 维（276→282）的 kernel 增量）
N_TOTAL_PARAMS = 4_402_822

# 二期 S1 / S1b 新增模块（--init-params 形状匹配合并时应全部落在 fresh）
NEW_MODULES = {
    "params/Dense_1", "params/Dense_2",                                        # 辅助头
    "params/TideActor_0/Dense_2", "params/TideActor_0/Dense_3",                # 池化 key / 动作行 key
    "params/TideCritic_0/Dense_3",                                             # 池化 key
    "params/TideEncoder_0/TideCardEncoder_0/AtomSetEncoder_0",
    "params/TideActor_0/Dense_4", "params/TideActor_0/Dense_5",                # S1b：牌库 key / 注入
    "params/TideCritic_0/Dense_4", "params/TideCritic_0/Dense_5",              # S1b：牌库 key / 注入
}
# 「下游层零初始化 ⇒ 本层起步梯度恒 0」的模块对：{本层模块: 下游零初始化模块}。
# 辅助头隐层 → 输出层；牌库 key → 注入层。下游层一动，本层就必须有梯度（[7] / [14] 各验各的）。
ZERO_AT_INIT_DOWNSTREAM = {
    "params/Dense_1": "params/Dense_2",
    "params/TideActor_0/Dense_4": "params/TideActor_0/Dense_5",
    "params/TideCritic_0/Dense_4": "params/TideCritic_0/Dense_5",
}
CARD_ENC = "params/TideEncoder_0/TideCardEncoder_0"
NEW_LEAVES = {CARD_ENC + "/alpha"}
RETIRED_LEAF = CARD_ENC + "/Embed_3/embedding"      # 一期类型槽位置标记（二期退役）


# ---------------------------------------------------------------- 合成观测（不依赖 Unity）

def synth_cards(rng, n_valid):
    """前 n_valid 槽为有效卡，其余空槽（全 0）。有效卡：效果原子占 [0..9]（缺席位 id/type/参数全 0，
    与 C# 写入器一致）、[10]/[11]=底盘攻/守资格原子随机出现、[27]/[28]=结构/关键词哈希。"""
    cards = np.zeros((MAX_CARDS, N_CARD_FEATURES), dtype=np.float32)
    for i in range(n_valid):
        cards[i, 0] = 1.0
        slots = list(range(int(rng.integers(0, 10))))
        if rng.random() < 0.3:
            slots.append(10)
        if rng.random() < 0.3:
            slots.append(11)
        for s in slots:
            cards[i, CARD_ID_START + s] = rng.integers(1, N_CARD_POOL)
            cards[i, TYPE_ID_START + s] = rng.integers(1, N_EFFECT_TYPES)
            lo = ATOM_PARAM_START + s * ATOM_PARAM_DIM
            cards[i, lo:lo + ATOM_PARAM_DIM] = rng.normal(size=ATOM_PARAM_DIM)
        cards[i, CARD_ID_START + 12] = rng.integers(0, N_CARD_POOL)
        cards[i, CARD_ID_START + 13] = rng.integers(0, N_CARD_POOL)
        cards[i, 1:CARD_ID_START] = rng.normal(size=CARD_ID_START - 1)
        cards[i, TAIL_START:] = rng.normal(size=N_CARD_FEATURES - TAIL_START)
    return cards


def synth_actions(rng, n_valid, n_legal):
    """前 n_legal 行合法；来源/目标槽位只指向有效卡槽或 -1（非法行全 0）。"""
    a = np.zeros((MAX_ACTIONS, N_ACTION_FEATURES), dtype=np.float32)
    a[:n_legal, 0] = 1.0
    a[:n_legal, 1] = rng.integers(0, len(ACTION_TYPES), n_legal)
    a[:n_legal, 2] = rng.integers(-1, n_valid, n_legal)
    a[:n_legal, 3] = rng.integers(-1, n_valid, n_legal)
    a[:n_legal, 4:] = rng.normal(size=(n_legal, N_ACTION_FEATURES - 4))
    return a


def synth_obs(rng, n_valid=40, n_legal=12):
    return {
        "cards_": synth_cards(rng, n_valid),
        "global_": rng.normal(size=N_GLOBAL_FEATURES).astype(np.float32),
        "actions_": synth_actions(rng, n_valid, n_legal),
        "h_actions_": None,
    }


def synth_obs_with_deck(rng, n_valid=30, n_deck=20, n_legal=12):
    """synth_obs + 非空己方牌库块：前 n_deck 个牌库槽 [154..] 填唯一卡（zoneKind=6、未落格、
    [157]=1..4 张），其余牌库槽留空。战场/手牌等前 n_valid 槽照常。"""
    obs = synth_obs(rng, n_valid=n_valid, n_legal=n_legal)
    assert n_deck <= DECK_SLOTS
    deck = synth_cards(rng, n_deck)[:n_deck]
    deck[:, CARD_F_ZONE] = 6.0
    deck[:, CARD_F_POS_X] = deck[:, CARD_F_POS_X + 1] = -1.0
    deck[:, CARD_F_COUNT] = rng.integers(1, 5, n_deck)
    obs["cards_"][DECK_BASE:DECK_BASE + n_deck] = deck
    return obs


def to_batch(obs, n=1):
    """obs dict → (n, ...) 批；None 叶子保持 None。"""
    return {k: (None if v is None else jnp.asarray(np.repeat(v[None], n, axis=0)))
            for k, v in obs.items()}


def flat(tree):
    return flax.traverse_util.flatten_dict(tree, sep="/")


def max_abs(a, b):
    return float(jnp.max(jnp.abs(jnp.asarray(a) - jnp.asarray(b))))


class _FakeEnv:
    """脚本化假环境（接口同 tide_env_tcp：reset/step）。第 k 局局长 ep_lens[k % len]；
    偶数局终局报 timeout=True、奇数局 False；终局步奖励 1.0，其余 0.01。
    reset 只清局内步数——局计数跨窗口延续（真实训练里 env 实例也跨窗口复用）。"""

    def __init__(self, rng, ep_lens=(5, 8, 20)):
        self.rng, self.ep_lens = rng, list(ep_lens)
        self.t_in_ep, self.ep = 0, 0
        self.obs = synth_obs(rng)

    def reset(self, options=None):
        self.t_in_ep = 0
        self.obs = synth_obs(self.rng)
        return self.obs, {"toPlay": 0}

    def step(self, action):
        self.t_in_ep += 1
        done = self.t_in_ep >= self.ep_lens[self.ep % len(self.ep_lens)]
        info = {"toPlay": 0, "turn": 1, "winner": "",
                "timeout": bool(done and self.ep % 2 == 0)}
        reward = 1.0 if done else 0.01
        if done:
            self.ep += 1
            self.obs = synth_obs(self.rng)  # 下一局首观测（rollout 在 done 后会再调 reset）
        return self.obs, reward, done, False, info


def _entropy(logits, n_legal):
    """前 n_legal 个合法动作上的策略熵（nats）。"""
    legal = jnp.arange(logits.shape[-1]) < n_legal
    logp = jax.nn.log_softmax(jnp.where(legal, logits, -1e9))[legal]
    return float(-(jnp.exp(logp) * logp).sum())


# ---------------------------------------------------------------- 各段

def test_layout():
    assert N_CARD_FEATURES == 158 and MAX_CARDS == 191 and N_ACTION_FEATURES == 26
    assert N_GLOBAL_FEATURES == 100 and N_ID_SLOTS == 14 and N_TYPE_SLOTS == 12 and ATOM_PARAM_DIM == 9
    assert CARD_ID_START + N_ID_SLOTS == TYPE_ID_START
    assert TYPE_ID_START + N_TYPE_SLOTS == ATOM_PARAM_START
    assert TAIL_START == 149
    print(f"[1] 布局常量自洽 OK: cards 191x158 / global 100 / actions 512x26 / "
          f"ids[{CARD_ID_START}..{CARD_ID_START + N_ID_SLOTS}) "
          f"types[{TYPE_ID_START}..{TYPE_ID_START + N_TYPE_SLOTS})")


def test_shapes():
    rng = np.random.default_rng(0)
    obs = synth_obs(rng, n_valid=40, n_legal=10)
    cards3 = to_batch(obs, 3)["cards_"]
    assert cards3.shape == (3, MAX_CARDS, N_CARD_FEATURES)

    card_enc = TideCardEncoder(channels=CHANNELS)
    cp = card_enc.init(jax.random.PRNGKey(0), cards3)
    cards_enc, c_mask = card_enc.apply(cp, cards3)
    assert cards_enc.shape == (3, MAX_CARDS, CHANNELS), cards_enc.shape
    assert c_mask.shape == (3, MAX_CARDS) and c_mask.dtype == jnp.bool_
    print(f"[2] 卡编码器前向 OK: cards {cards_enc.shape} c_mask {c_mask.shape}")

    batch = to_batch(obs)
    enc = TideEncoder(channels=CHANNELS)
    ep = enc.init(jax.random.PRNGKey(0), batch)
    out = enc.apply(ep, batch)
    assert out["cards"].shape == (1, MAX_CARDS, CHANNELS)
    assert out["global"].shape == (1, CHANNELS)
    assert out["actions"].shape == (1, MAX_ACTIONS, CHANNELS)
    assert out["c_mask"].shape == (1, MAX_CARDS) and out["a_mask"].shape == (1, MAX_ACTIONS)
    print(f"[2] 总编码器前向 OK: cards {out['cards'].shape} global {out['global'].shape} "
          f"actions {out['actions'].shape}")

    agent = create_tide_agent()
    key = jax.random.PRNGKey(0)
    params = agent.init(key, init_rstate(), batch)
    params_fwd = agent.init(key, init_rstate(), batch, method=TideRNNAgent.forward_with_aux)
    # __call__ 与 forward_with_aux 构造同一棵参数树（部署导出走 __call__，训练走后者）
    assert jax.tree_util.tree_structure(params) == jax.tree_util.tree_structure(params_fwd)
    assert list(flat(params)) == list(flat(params_fwd))
    assert all(np.array_equal(np.asarray(a), np.asarray(b))
               for a, b in zip(jax.tree_util.tree_leaves(params), jax.tree_util.tree_leaves(params_fwd)))
    n_params = sum(int(v.size) for v in flat(params).values())
    assert n_params == N_TOTAL_PARAMS, n_params

    rstate, logits, value = agent.apply(params, init_rstate(), batch)
    assert rstate.shape == (1, 512) and logits.shape == (1, MAX_ACTIONS) and value.shape == (1,)
    full = agent.apply(params, init_rstate(), batch, method=TideRNNAgent.forward_with_aux)
    assert len(full) == 4 and full[3].shape == (1, 2)
    assert all(max_abs(a, b) == 0.0 for a, b in zip((rstate, logits, value), full))
    assert bool((full[3][:, 1] > 0).all() and (full[3][:, 1] < 1).all()), "aux[1] 应为 sigmoid 概率"
    print(f"[2] Agent 前向 OK: __call__ → rstate {rstate.shape} logits {logits.shape} value {value.shape}；"
          f"forward_with_aux → +aux {full[3].shape} | 参数量 {n_params:,}（{len(flat(params))} 叶子）")


def test_masks():
    rng = np.random.default_rng(1)
    card_enc = TideCardEncoder(channels=CHANNELS)
    cards3 = to_batch(synth_obs(rng), 3)["cards_"]
    cp = card_enc.init(jax.random.PRNGKey(0), cards3)

    # (a) 缺席原子位（哈希=0 且类型=0）：参数块哪怕是脏数据也不影响卡向量
    a = synth_cards(rng, 40)
    absent = [s for s in range(N_TYPE_SLOTS)
              if a[9, CARD_ID_START + s] == 0 and a[9, TYPE_ID_START + s] == 0]
    assert absent, "合成卡应至少有一个缺席原子位"
    b = a.copy()
    lo = ATOM_PARAM_START + absent[0] * ATOM_PARAM_DIM
    b[9, lo:lo + ATOM_PARAM_DIM] = 123.0
    d = max_abs(card_enc.apply(cp, a[None])[0][9], card_enc.apply(cp, b[None])[0][9])
    assert d == 0.0, f"缺席原子位的参数不应进入卡向量，max|Δ|={d}"

    # (b) 空卡槽：valid=0 → c_mask=True
    _, c_mask = card_enc.apply(cp, a[None])
    assert bool(c_mask[0, 40:].all()) and not bool(c_mask[0, :40].any())

    # (c) 集合编码器内部 mask：present=False 的 token 塞脏数据不改变输出；全空集合有限
    ase = AtomSetEncoder(CHANNELS)
    tok = rng.normal(size=(1, 4, N_TYPE_SLOTS, CHANNELS)).astype(np.float32)
    pres = rng.random((1, 4, N_TYPE_SLOTS)) < 0.5
    pres[:, :, 0] = True
    pres[:, 0, :] = False                       # 卡 0 = 全空集合
    ap = ase.init(jax.random.PRNGKey(0), tok, pres)
    tok2 = tok.copy()
    tok2[np.broadcast_to(~pres[..., None], tok.shape)] = 999.0
    o1, o2 = ase.apply(ap, tok, pres), ase.apply(ap, tok2, pres)
    assert max_abs(o1, o2) == 0.0 and bool(jnp.isfinite(o1).all())
    empty_vec = np.asarray(o1[0, 0])
    assert float(np.linalg.norm(np.asarray(o1[0, 1]) - empty_vec)) > 1e-3, "非空集合不应与空集合同向量"
    print(f"[3] 掩码 OK: 缺席原子位脏参数 max|Δ|=0 | 空卡槽 c_mask 全 True | "
          f"集合编码器屏蔽位脏 token max|Δ|=0（全空集合有限：输出=池化投影偏置，初始化为 0）")

    # (d) Actor / Critic：空卡槽的卡向量被扰动不影响 logits / value；非法动作 = -1e9
    obs = synth_obs(rng, n_valid=25, n_legal=12)
    batch = to_batch(obs)
    enc = TideEncoder(channels=CHANNELS)
    ep = enc.init(jax.random.PRNGKey(0), batch)
    encoded = enc.apply(ep, batch)
    actor, critic = TideActor(CHANNELS), TideCritic(CHANNELS)
    ap2 = actor.init(jax.random.PRNGKey(2), encoded)
    vp = critic.init(jax.random.PRNGKey(3), encoded)
    perturbed = dict(encoded)
    perturbed["cards"] = jnp.where(encoded["c_mask"][..., None], encoded["cards"] + 100.0, encoded["cards"])
    dl = max_abs(actor.apply(ap2, encoded), actor.apply(ap2, perturbed))
    dv = max_abs(critic.apply(vp, encoded), critic.apply(vp, perturbed))
    assert dl < 1e-5 and dv < 1e-5, f"空卡槽不应影响打分: logits max|Δ|={dl} value max|Δ|={dv}"
    logits = actor.apply(ap2, encoded)
    assert bool((logits[0, 12:] == -1e9).all()), "非法动作应被屏蔽为 -1e9"
    assert bool(jnp.isfinite(logits[0, :12]).all())
    print(f"[3] 掩码 OK: 空卡槽扰动 → logits max|Δ|={dl:.1e} value max|Δ|={dv:.1e} | "
          "非法 500 行 logits 全 -1e9")


def test_init_equivalence():
    """二期 S1-3 的起步口径：池化 key 投影零初始化 ⇒ 注意力在有效槽上均匀 = masked mean，
    继承自一期的 Actor/Critic MLP 看到的上下文与一期逐值相同。"""
    rng = np.random.default_rng(4)
    batch = to_batch(synth_obs(rng, n_valid=25, n_legal=12))
    enc = TideEncoder(channels=CHANNELS)
    ep = enc.init(jax.random.PRNGKey(0), batch)
    encoded = enc.apply(ep, batch)
    cards, c_mask, g, acts = encoded["cards"], encoded["c_mask"], encoded["global"], encoded["actions"]
    c = CHANNELS
    n_valid = jnp.sum(~c_mask).astype(jnp.float32)
    mean_pool = jnp.where(c_mask[..., None], 0.0, cards).sum(axis=1) / n_valid  # (B, c) 一期的均值池化

    dense = lambda p, x, n=c: nn.Dense(n).apply({"params": p}, x)
    actor = TideActor(CHANNELS)
    ap = actor.init(jax.random.PRNGKey(2), encoded)["params"]
    relu = jax.nn.relu
    ctx = relu(dense(ap["Dense_0"], g + mean_pool))
    ctx = relu(dense(ap["Dense_1"], ctx))
    card_ctx = jnp.broadcast_to(mean_pool[:, None, :], acts.shape)  # 动作行 key 零初始化 → 有效槽均值
    manual = ((acts * ctx[:, None, :]).sum(axis=-1) + (acts * card_ctx).sum(axis=-1)) / jnp.sqrt(c)
    manual = jnp.where(encoded["a_mask"], -1e9, manual)
    d = max_abs(actor.apply({"params": ap}, encoded), manual)
    assert d < 1e-5, f"初始化时 Actor 应等价于「均值池化 + 文档公式」，max|Δ|={d}"

    critic = TideCritic(CHANNELS)
    vp = critic.init(jax.random.PRNGKey(3), encoded)["params"]
    x = relu(dense(vp["Dense_0"], g + mean_pool))
    x = relu(dense(vp["Dense_1"], x))
    manual_v = dense(vp["Dense_2"], x, 1)[..., 0]
    dv = max_abs(critic.apply({"params": vp}, encoded), manual_v)
    assert dv < 1e-5, f"初始化时 Critic 应等价于「均值池化」口径，max|Δ|={dv}"
    print(f"[4] 初始化等价性 OK: Actor vs「均值池化 + (dot(a,ctx)+dot(a,mean))/√c」max|Δ|={d:.1e} | "
          f"Critic vs「均值池化」max|Δ|={dv:.1e}")


def test_batch_independence():
    rng = np.random.default_rng(5)
    agent = create_tide_agent()
    params = agent.init(jax.random.PRNGKey(0), init_rstate(), to_batch(synth_obs(rng)))

    def three(seeds):
        cd = np.stack([synth_cards(np.random.default_rng(s), 20 + 5 * i) for i, s in enumerate(seeds)])
        ac = np.stack([synth_actions(np.random.default_rng(s + 100), 20, 10) for s in seeds])
        rs = np.stack([np.random.default_rng(s).normal(size=512).astype(np.float32) * 0.1 for s in seeds])
        gl = np.stack([np.random.default_rng(s + 200).normal(size=N_GLOBAL_FEATURES).astype(np.float32) for s in seeds])
        return jnp.asarray(rs), {"cards_": jnp.asarray(cd), "global_": jnp.asarray(gl),
                                 "actions_": jnp.asarray(ac), "h_actions_": None}

    fwd = jax.jit(lambda r, o: agent.apply(params, r, o))
    out0 = [np.asarray(x) for x in fwd(*three((1, 2, 3)))]
    out1 = [np.asarray(x) for x in fwd(*three((7, 2, 3)))]          # 只换样本 0
    assert out0[1].shape == (3, MAX_ACTIONS) and out0[2].shape == (3,)
    ch = [[float(np.abs(o[s] - p[s]).max()) for o, p in zip(out0, out1)] for s in range(3)]
    assert max(ch[1]) < 1e-5 and max(ch[2]) < 1e-5, f"改样本 0 不应影响样本 1/2: {ch}"
    assert max(ch[0]) > 1e-3, "对照失效：换样本 0 的输入本应改变样本 0 的输出"
    print(f"[5] batch 独立 OK (B=3): 改样本 0 → 样本 1 max|Δ|={max(ch[1]):.1e} "
          f"样本 2 max|Δ|={max(ch[2]):.1e}（对照：样本 0 自身 max|Δ|={max(ch[0]):.3f}）")


def test_finite():
    agent = create_tide_agent()
    rng = np.random.default_rng(6)
    params = agent.init(jax.random.PRNGKey(0), init_rstate(), to_batch(synth_obs(rng)))
    fwd = jax.jit(lambda r, o, k: agent.apply(params, r, o, id_keep=k, method=TideRNNAgent.forward_with_aux))
    plain = jax.jit(agent.apply)

    def run(obs, keep=None):
        if keep is None:
            outs = plain(params, init_rstate(), to_batch(obs))
        else:
            outs = fwd(init_rstate(), to_batch(obs), jnp.asarray(keep))
        return [np.asarray(x) for x in outs]

    # 全空卡槽 + 全非法动作
    empty = {"cards_": np.zeros((MAX_CARDS, N_CARD_FEATURES), np.float32),
             "global_": np.zeros(N_GLOBAL_FEATURES, np.float32),
             "actions_": np.zeros((MAX_ACTIONS, N_ACTION_FEATURES), np.float32),
             "h_actions_": None}
    o = run(empty)
    assert all(np.isfinite(x).all() for x in o), "全空输入出现 NaN/inf"
    assert bool((o[1] == -1e9).all()), "全非法动作 logits 应全 -1e9"
    # 满槽 191 + 512 合法 + 参数块 1e4 + global 50
    extreme = synth_obs(rng, n_valid=191, n_legal=512)
    extreme["cards_"][:, ATOM_PARAM_START:TAIL_START] = 1e4
    extreme["global_"][:] = 50.0
    o2 = run(extreme)
    assert all(np.isfinite(x).all() for x in o2), "极端输入出现 NaN/inf"
    # 身份 dropout 乘子：全 0（身份通路整体置零）/ 3.0，都不得出 NaN（带辅助头的 4 元组路径）
    o3 = run(extreme, np.zeros((1, MAX_CARDS, N_ID_SLOTS), np.float32))
    o4 = run(extreme, np.full((1, MAX_CARDS, N_ID_SLOTS), 3.0, np.float32))
    assert all(np.isfinite(x).all() for x in o3 + o4), "身份乘子路径出现 NaN/inf"
    print(f"[6] 输出有限 OK: 全空卡槽+全非法动作（logits 全 -1e9）/ 满槽 191 + 512 合法 + 参数 1e4 + "
          f"global 50 / 身份乘子 0 与 3.0 | max|rstate|={np.abs(o2[0]).max():.2f} "
          f"max|logit|={np.abs(o2[1]).max():.2f} max|value|={np.abs(o2[2]).max():.2f}")


def test_grad():
    agent = create_tide_agent()
    rng = np.random.default_rng(8)
    batch = to_batch(synth_obs_with_deck(rng, n_valid=40, n_deck=20, n_legal=12))  # 牌库块非空，牌库路径才活跃
    params = agent.init(jax.random.PRNGKey(0), init_rstate(), batch)
    a_mask = batch["actions_"][:, :, 0] == 0

    def loss_fn(p):
        _, logits, value, aux = agent.apply(p, init_rstate(), batch, method=TideRNNAgent.forward_with_aux)
        return (logits * jnp.where(a_mask, 0.0, 1.0)).sum() + value.sum() + aux.sum()

    grads = flat(jax.grad(loss_fn)(params))
    nonfinite = [k for k, g in grads.items() if not bool(jnp.isfinite(g).all())]
    assert not nonfinite, f"梯度出现 NaN/inf: {nonfinite[:5]}"
    assert set(grads) == set(flat(params)), "梯度树与参数树叶子集不一致"

    def mod_grad(gmap, mod):
        return sum(float(jnp.abs(g).sum()) for k, g in gmap.items() if k.startswith(mod + "/"))

    # 新模块梯度：下游层零初始化的模块起步梯度恒 0（构造使然，见 ZERO_AT_INIT_DOWNSTREAM），其余必须非零
    for mod in sorted(NEW_MODULES):
        gsum = mod_grad(grads, mod)
        if mod in ZERO_AT_INIT_DOWNSTREAM:
            assert gsum == 0.0, f"下游零初始化的模块起步梯度应恒 0: {mod} Σ|g|={gsum}"
        else:
            assert gsum > 0, f"新模块拿不到梯度（零初始化卡死？）: {mod}"
    assert float(jnp.abs(grads[CARD_ENC + "/alpha"]).sum()) > 0, "α 拿不到梯度"
    k_grad = float(jnp.abs(grads[CARD_ENC + "/AtomSetEncoder_0/Dense_1/kernel"]).max())
    assert k_grad > 0, "集合编码器 k 投影零初始化后梯度仍为 0（注意力学不起来）"
    # 下游层离开零点后，上述模块必须立刻有梯度
    nudged = dict(flat(params))
    for down in sorted(set(ZERO_AT_INIT_DOWNSTREAM.values())):
        nudged[down + "/kernel"] = jnp.full_like(nudged[down + "/kernel"], 1e-3)
    grads2 = flat(jax.grad(loss_fn)(flax.traverse_util.unflatten_dict(nudged, sep="/")))
    after = {mod: mod_grad(grads2, mod) for mod in ZERO_AT_INIT_DOWNSTREAM}
    assert all(g > 0 for g in after.values()), f"下游层=1e-3 后仍有模块无梯度: {after}"
    print(f"[7] 梯度 OK: {len(grads)} 个叶子全部有限 | 新模块（池化 key/动作 key/AtomSetEncoder/α/"
          f"辅助头输出层/牌库注入层）梯度均非零 | 零初始化的 k 投影梯度 max={k_grad:.2e} | "
          f"辅助头隐层/牌库 key：下游零初始化时梯度=0（构造使然），下游=1e-3 后 Σ|g|="
          + " ".join(f"{k.replace('params/', '')}→Σ|g|={v:.2e}" for k, v in after.items()))


def test_alpha():
    agent = create_tide_agent()
    rng = np.random.default_rng(9)
    params = agent.init(jax.random.PRNGKey(0), init_rstate(), to_batch(synth_obs(rng)))
    w0 = pathway_weights(params)
    assert all(abs(w - 1.0) < 1e-6 for w in w0), f"α 初值应为 (1,1,1)，得到 {w0}"
    moved = dict(flat(params))
    moved[CARD_ENC + "/alpha"] = jnp.array([2.0, 0.0, 0.0], jnp.float32)
    w1 = pathway_weights(flax.traverse_util.unflatten_dict(moved, sep="/"))
    assert w1[0] > w1[1] and abs(w1[1] - w1[2]) < 1e-6 and abs(sum(w1) - 3.0) < 1e-5
    print(f"[8] 三路权重 OK: 初值 {tuple(round(x, 4) for x in w0)} | α=(2,0,0) → "
          f"{tuple(round(x, 4) for x in w1)}（和为 3，身份通路被加权）")


def test_dropout():
    agent = create_tide_agent()
    rng = np.random.default_rng(10)
    batch = to_batch(synth_obs(rng, n_valid=40, n_legal=12))
    params = agent.init(jax.random.PRNGKey(0), init_rstate(), batch)
    method = TideRNNAgent.forward_with_aux
    off = agent.apply(params, init_rstate(), batch, method=method)
    ones = agent.apply(params, init_rstate(), batch, id_keep=jnp.ones((1, MAX_CARDS, N_ID_SLOTS)), method=method)
    zeros = agent.apply(params, init_rstate(), batch, id_keep=jnp.zeros((1, MAX_CARDS, N_ID_SLOTS)), method=method)
    d_same = max(max_abs(a, b) for a, b in zip(off, ones))
    d_zero = max_abs(off[1], zeros[1])
    assert d_same < 1e-6, f"id_keep 全 1 应等价于关闭 dropout，max|Δ|={d_same}"
    assert d_zero > 1e-3, "身份通路整体置零应改变 logits"
    print(f"[9] 身份 dropout OK: 乘子全 1 = 关闭（max|Δ|={d_same:.1e}）| 全 0 → logits max|Δ|={d_zero:.3f}")


def test_rollout_labels():
    agent = create_tide_agent()
    params = agent.init(jax.random.PRNGKey(0), init_rstate(), to_batch(synth_obs(np.random.default_rng(11))))
    jit_apply = jax.jit(agent.apply)
    seen = []

    def recording_apply(p, rstate, obs_batch, **kw):
        """记录每次前向收到的身份 dropout 乘子（None=未带）。"""
        if "id_keep" in kw:
            seen.append(np.asarray(kw["id_keep"])[0].copy())
            return jit_apply(p, rstate, obs_batch, id_keep=kw["id_keep"])
        seen.append(None)
        return jit_apply(p, rstate, obs_batch)

    env = _FakeEnv(np.random.default_rng(12))          # 局长 5 / 8 / 20 轮转；局 0 超时
    # 窗口 1（10 步）：局 A（局长 5）在 step 5 终局；局 B 从 step 6 起、跨窗口
    data1, obs1, rstate1, _rng1, eps1, pend1 = rollout_single_env(
        recording_apply, params, env, synth_obs(np.random.default_rng(13)), init_rstate(),
        10, jax.random.PRNGKey(1), identity_dropout=0.1)
    assert len(eps1) == 1 and eps1[0].len == 5 and eps1[0].timeout
    assert abs(eps1[0].ret - 1.04) < 1e-6 and abs(eps1[0].shaping_ret - 0.04) < 1e-6
    assert data1["aux_mask"].tolist() == [1.0] * 5 + [0.0] * 5
    np.testing.assert_allclose(data1["aux_labels"][:5, 0], np.arange(5, 0, -1) / AUX_REMAIN_STEPS_DIV, rtol=0, atol=1e-7)
    assert data1["aux_labels"][:5, 1].tolist() == [1.0] * 5
    assert not data1["aux_labels"][5:].any()
    assert tuple(round(x, 6) for x in pend1) == (0.05, 5.0, 0.05), pend1
    idk = data1["id_keep"]
    assert idk.shape == (10, MAX_CARDS, N_ID_SLOTS) and idk.dtype == np.float32
    uniq = np.unique(idk)
    assert len(uniq) == 2 and uniq[0] == 0.0 and abs(uniq[1] * 0.9 - 1.0) < 1e-6, uniq
    keep_frac = float((idk > 0).mean())
    assert 0.88 < keep_frac < 0.92, f"保留率应≈1-p=0.9，得到 {keep_frac}"
    assert all(s is not None and np.array_equal(s, idk[t]) for t, s in enumerate(seen)), \
        "data[id_keep] 必须逐位等于前向时实际使用的乘子"
    # 窗口 2（8 步，同一 env 续跑局 B）：局 B 在窗口第 3 步终局，总局长 8（窗口 1 走了 5 步）
    seen.clear()
    data2, *_rest, eps2, pend2 = rollout_single_env(
        recording_apply, params, env, obs1, rstate1, 8, jax.random.PRNGKey(2), pending=pend1,
        identity_dropout=0.1)
    assert len(eps2) == 1 and eps2[0].len == 8 and not eps2[0].timeout
    np.testing.assert_allclose(data2["aux_labels"][:3, 0], np.array([3, 2, 1]) / AUX_REMAIN_STEPS_DIV, rtol=0, atol=1e-7)
    assert data2["aux_labels"][:3, 1].tolist() == [0.0] * 3
    assert data2["aux_mask"].tolist() == [1.0] * 3 + [0.0] * 5
    assert pend2[1] == 5, "窗口 2 末尾未完结局（局长 20）已走 5 步"
    assert all(s is not None and np.array_equal(s, data2["id_keep"][t]) for t, s in enumerate(seen))
    # identity_dropout=0（默认）：逐字走旧调用形态，不产 id_keep
    seen.clear()
    data3, *_ = rollout_single_env(recording_apply, params, env, obs1, rstate1, 4, jax.random.PRNGKey(3))
    assert data3["id_keep"] is None and seen == [None] * 4
    print(f"[10] 标签回填 OK: 窗口1 局A(5步,超时) 标签 {np.round(data1['aux_labels'][:5, 0] * AUX_REMAIN_STEPS_DIV, 3).tolist()}/512 "
          f"timeout=1、尾 5 步 mask=0 | 窗口2 跨窗局B 标签 {np.round(data2['aux_labels'][:3, 0] * AUX_REMAIN_STEPS_DIV, 3).tolist()}/512 "
          f"timeout=0 | dropout 乘子取值 0 与 1/(1-p)、保留率 {keep_frac:.4f}、与实际前向逐位一致 | p=0 时不产 id_keep")


def test_ppo_update():
    try:
        import train_tide_complete as ttc
    except ModuleNotFoundError as exc:
        print(f"[11] PPO 更新链路 SKIPPED：缺依赖（{exc}）。"
              "设 PYTHONPATH=<训练副本根>/ygo-agent-main 后重跑启用")
        return
    import optax
    from flax.training.train_state import TrainState

    args = ttc.Args()
    assert args.aux_coef == 0.1 and args.identity_dropout == 0.1
    agent = create_tide_agent()
    params = agent.init(jax.random.PRNGKey(0), init_rstate(), to_batch(synth_obs(np.random.default_rng(15))))
    jit_apply = jax.jit(agent.apply)
    data, *_ = rollout_single_env(jit_apply, params, _FakeEnv(np.random.default_rng(17), ep_lens=(8,)),
                                  synth_obs(np.random.default_rng(16)), init_rstate(), 16,
                                  jax.random.PRNGKey(3), identity_dropout=args.identity_dropout)
    assert data["aux_mask"].all(), "两局都在窗口内完结 → 每步都应有标签"
    advantages, returns = compute_advantages_simple(
        data["values"], data["rewards"], data["dones"], 0.0, args.gamma, args.gae_lambda)
    advantages = (advantages - advantages.mean()) / (advantages.std() + 1e-8)
    ts = TrainState.create(apply_fn=agent.apply, params=params, tx=optax.adam(args.learning_rate))
    ppo_update = ttc.make_ppo_update(args)

    def run(keep):
        new_ts, loss, diag = ppo_update(
            ts, ttc.stack_obs(data["obs"]), ttc.stack_rstates(data["rstates"]),
            jnp.asarray(data["actions"]), jnp.asarray(data["logprobs"]),
            jnp.asarray(advantages), jnp.asarray(returns),
            jnp.asarray(data["aux_labels"]), jnp.asarray(data["aux_mask"]), keep)
        return new_ts, float(loss), [float(x) for x in diag]

    ts1, loss, (pg, vf, ent, kl, clip, aux) = run(jnp.asarray(data["id_keep"]))
    assert np.isfinite(loss) and all(np.isfinite(x) for x in (pg, vf, ent, kl, clip, aux))
    assert abs(kl) < 1e-6 and clip == 0.0, f"复用采样掩码时首个 epoch 的 ratio 应精确为 1: kl={kl} clip={clip}"
    assert aux > 0, "标签齐全时辅助头损失应为正"
    g = flat(ts1.params)
    assert all(bool(jnp.isfinite(v).all()) for v in g.values()), "更新后参数出现 NaN/inf"
    # 一步更新后辅助头输出层（Dense_2）必须已动；隐层 Dense_1 这一步梯度恒 0（输出层零初始化），不动属正常
    assert not np.array_equal(np.asarray(g["params/Dense_2/kernel"]),
                              np.asarray(flat(params)["params/Dense_2/kernel"])), "辅助头输出层未被更新"
    assert np.array_equal(np.asarray(g["params/Dense_1/kernel"]),
                          np.asarray(flat(params)["params/Dense_1/kernel"]))
    perm = np.random.default_rng(18).permutation(len(data["actions"]))
    _, _, (_, _, _, kl_other, _, _) = run(jnp.asarray(data["id_keep"])[perm])
    _, _, (_, _, _, kl_off, _, _) = run(None)
    assert kl_other > 50 * max(kl, 1e-12) and kl_off > 50 * max(kl, 1e-12), (kl, kl_other, kl_off)
    print(f"[11] PPO 更新链路 OK: loss={loss:.4f} aux_loss={aux:.4f} ent={ent:.3f} | 复用采样掩码 KL={kl:.2e} "
          f"clip=0（ratio≡1）| 对照：换成别人的掩码 KL={kl_other:.2e}、关闭 dropout KL={kl_off:.2e} "
          "| 更新后参数全有限、辅助头输出层已动")


def test_merge():
    agent = create_tide_agent()
    target = agent.init(jax.random.PRNGKey(0), init_rstate(), to_batch(synth_obs(np.random.default_rng(19))))
    ft = flat(target)
    old = {}
    for path, v in ft.items():
        if any(path.startswith(nm + "/") for nm in NEW_MODULES) or path in NEW_LEAVES:
            continue                                   # 一期旧树没有的叶子（AtomSetEncoder_0 按前缀）
        old[path] = np.asarray(v) + 1.0                # 旧值 = 初始化值 +1（可辨识）
    old[RETIRED_LEAF] = np.zeros((N_TYPE_SLOTS, CHANNELS), np.float32)
    old["params/Dense_0/kernel"] = np.zeros((256, CHANNELS), np.float32)   # 同名异形 → reshaped
    # 走一遍真实 checkpoint 的序列化路径（to_bytes → from_bytes）
    source = flax.serialization.from_bytes(None, flax.serialization.to_bytes(
        flax.traverse_util.unflatten_dict(old, sep="/")))
    fs = flat(source)
    logs = []
    merged, report = merge_params_by_shape(target, source, log=logs.append)
    fm = flat(merged)
    for p in report["loaded"]:
        assert np.array_equal(np.asarray(fm[p]), np.asarray(fs[p])), p
    for p in report["fresh"]:
        assert np.array_equal(np.asarray(fm[p]), np.asarray(ft[p])), p
    assert report["reshaped"] == [("params/Dense_0/kernel", [256, CHANNELS], [512, CHANNELS])]
    assert report["dropped"] == [RETIRED_LEAF]
    # fresh 恰为二期新增模块（AtomSetEncoder_0 下还有 Dense_*/LayerNorm_0 一层，按前缀对账）+ α
    # （α 是 TideCardEncoder_0 自己的参数，与该模块下一批 loaded 叶子同父，单独放行）
    fresh_mods = {p.rsplit("/", 1)[0] for p in report["fresh"]}
    leaf_parents = {p.rsplit("/", 1)[0] for p in NEW_LEAVES}
    assert all(m in leaf_parents or any(m == nm or m.startswith(nm + "/") for nm in NEW_MODULES)
               for m in fresh_mods), fresh_mods
    assert all(any(p.startswith(nm + "/") for p in report["fresh"]) for nm in NEW_MODULES), NEW_MODULES
    assert NEW_LEAVES <= set(report["fresh"])
    assert report["n_total_params"] == N_TOTAL_PARAMS
    assert np.array_equal(np.asarray(fm["params/Dense_0/kernel"]), np.asarray(ft["params/Dense_0/kernel"]))
    assert set(fm) == set(ft), "合并结果与新树叶子集不一致"
    # 零匹配 → 拒载（SystemExit）
    try:
        merge_params_by_shape(target, {"params": {"NoSuchModule": {"w": np.zeros(3, np.float32)}}},
                              log=lambda *_: None)
        raise AssertionError("零匹配应 SystemExit")
    except SystemExit:
        pass
    print(f"[12] 形状匹配合并 OK: loaded {len(report['loaded'])} 叶子 {report['n_loaded_params']:,} 参数 / "
          f"fresh {len(report['fresh'])} 叶子 / reshaped {len(report['reshaped'])} / dropped {len(report['dropped'])} "
          "| loaded 取旧值、fresh 保初始化 | 零匹配拒载 ✓ | 报告样例：")
    for line in logs:
        print("      " + line)


def test_entropy():
    agent = create_tide_agent()
    uniform = float(np.log(10))
    for label, obs in (("合成观测 40 槽", synth_obs(np.random.default_rng(20), n_valid=40, n_legal=10)),
                       ("sample_input 满槽", sample_input())):
        obs = dict(obs)
        obs["actions_"] = obs["actions_"].copy()
        obs["actions_"][10:, 0] = 0.0                  # 只留前 10 个合法动作
        ents = []
        # v22 注（2026-11-26）：动作编码器输入 20→26 后初始化分布略移——3 种子取 min 是掷硬币口径
        # （S1 时 20 种子实测左尾 min 1.31/一期 1.79），改 5 种子「均值>0.8·ln10 且 min>0.7·ln10」：
        # 守卫本意（不 argmax 塌缩起步）不变，去掉阈值擦线抖动
        for seed in (0, 1, 2, 3, 4):
            params = agent.init(jax.random.PRNGKey(seed), init_rstate(), to_batch(obs))
            ents.append(_entropy(agent.apply(params, init_rstate(), to_batch(obs))[1][0], 10))
        assert sum(ents) / len(ents) > 0.8 * uniform and min(ents) > 0.7 * uniform, (
            f"{label}: 初始熵 {['%.3f' % e for e in ents]} 低于门槛（均值>0.8·ln10={0.8 * uniform:.3f}"
            f" 且 min>0.7·ln10={0.7 * uniform:.3f}）")
        print(f"[13] 初始熵 OK（{label}）: " + " / ".join(f"{e:.3f}" for e in ents)
              + f" nats（均值 {sum(ents) / len(ents):.3f}，均匀 ln10={uniform:.3f}，门槛 均值>{0.8 * uniform:.3f}/min>{0.7 * uniform:.3f}）")


def test_deck_set():
    """二期 S1b：牌库块 [154..183] 单独读成 deck_vec → 零初始化 Dense 并入 Actor/Critic 的 context。"""
    # (a) 布局对账：牌库块下标与 C# TideObservation 派生式同构（DeckBase = 77×2，其后牌库顶 1 +
    #     对方被展示 4 + 场地 2 正好到 MAX_CARDS——导入期断言兜底，这里再显式核一遍数值）
    assert DECK_SLOTS == 30 and DECK_BASE == 2 * 77 == 154 and DECK_END == 184
    assert MAX_CARDS - DECK_END == 1 + 4 + 2

    rng = np.random.default_rng(21)
    obs = synth_obs_with_deck(rng, n_valid=30, n_deck=20)
    assert obs["cards_"][DECK_BASE + 19, 0] == 1 and obs["cards_"][DECK_BASE + 20, 0] == 0
    batch = to_batch(obs)
    enc = TideEncoder(CHANNELS)
    ep = enc.init(jax.random.PRNGKey(0), batch)
    encoded = enc.apply(ep, batch)
    values = jnp.where(encoded["c_mask"][..., None], 0.0, encoded["cards"])
    valid = ~encoded["c_mask"]
    actor, critic = TideActor(CHANNELS), TideCritic(CHANNELS)
    ap = actor.init(jax.random.PRNGKey(2), encoded)
    vp = critic.init(jax.random.PRNGKey(3), encoded)
    deck_key_of = lambda p, name: (lambda x: nn.Dense(CHANNELS).apply({"params": p[name]}, x))

    def deck_vec(enc2, p, name="Dense_4"):
        v = jnp.where(enc2["c_mask"][..., None], 0.0, enc2["cards"])
        return _deck_read(v, ~enc2["c_mask"], enc2["global"], deck_key_of(p, name), CHANNELS)[0]

    a_key, c_key = ap["params"], vp["params"]
    dv_a, dv_c = deck_vec(encoded, a_key), deck_vec(encoded, c_key)
    assert dv_a.shape == (CHANNELS,) and bool(jnp.isfinite(dv_a).all()) and bool(jnp.isfinite(dv_c).all())

    # (b) 只改牌库块（换成另一批唯一卡）→ deck_vec 变；只改非牌库槽 → deck_vec 逐位不变
    other = dict(obs)
    other["cards_"] = obs["cards_"].copy()
    deck2 = synth_cards(np.random.default_rng(22), 20)[:20]
    deck2[:, CARD_F_ZONE] = 6.0
    deck2[:, CARD_F_POS_X] = deck2[:, CARD_F_POS_X + 1] = -1.0
    other["cards_"][DECK_BASE:DECK_BASE + 20] = deck2
    enc_other = enc.apply(ep, to_batch(other))
    d_deck = float(jnp.max(jnp.abs(deck_vec(enc_other, a_key) - dv_a)))
    other2 = dict(obs)
    other2["cards_"] = obs["cards_"].copy()
    other2["cards_"][5] = synth_cards(np.random.default_rng(23), 1)[0]       # 只换一张战场卡
    enc_other2 = enc.apply(ep, to_batch(other2))
    d_nondeck = float(jnp.max(jnp.abs(deck_vec(enc_other2, a_key) - dv_a)))
    assert d_deck > 1e-4, f"牌库块变了 deck_vec 应改变，max|Δ|={d_deck}"
    assert d_nondeck == 0.0, f"非牌库槽不应影响 deck_vec，max|Δ|={d_nondeck}"

    # (c) 空牌库槽（valid=0）的卡向量被扰动不影响 deck_vec；牌库块全空 → deck_vec = 0
    v_pert = np.asarray(values).copy()
    v_pert[0, DECK_BASE + 20:DECK_END] = 777.0            # 这些槽 c_mask=True（空）
    dv_pert = _deck_read(jnp.asarray(v_pert), valid, encoded["global"], deck_key_of(a_key, "Dense_4"), CHANNELS)[0]
    assert max_abs(dv_pert, dv_a) == 0.0, "空牌库槽不应漏进 deck_vec"
    no_deck = enc.apply(ep, to_batch(synth_obs(rng, n_valid=30)))          # 牌库块全空
    assert float(jnp.max(jnp.abs(deck_vec(no_deck, a_key)))) == 0.0, "牌库块全空时 deck_vec 应为 0"

    # (d) 注入层（Dense_5）零初始化 ⇒ 牌库路径对 logits/value 的贡献恒为 0（起步中性）
    inj_a = nn.Dense(CHANNELS).apply({"params": a_key["Dense_5"]}, dv_a)
    inj_c = nn.Dense(CHANNELS).apply({"params": c_key["Dense_5"]}, dv_c)
    assert float(jnp.max(jnp.abs(inj_a))) == 0.0 and float(jnp.max(jnp.abs(inj_c))) == 0.0

    # (e) 注入层离开零点后，只改牌库块就会经牌库路径改变 logits / value；key 投影随即有梯度
    nudge = 1e-2 * jnp.ones((CHANNELS, CHANNELS), jnp.float32)
    a2 = {"params": {**a_key, "Dense_5": {**a_key["Dense_5"], "kernel": nudge}}}
    v2 = {"params": {**c_key, "Dense_5": {**c_key["Dense_5"], "kernel": nudge}}}
    dl = max_abs(actor.apply(a2, encoded), actor.apply(a2, enc_other))
    dv = max_abs(critic.apply(v2, encoded), critic.apply(v2, enc_other))
    assert dl > 1e-5 and dv > 1e-5, f"牌库路径应已接到 logits/value: {dl:.2e} {dv:.2e}"

    def head_loss(p_head, mod, enc2):
        out = mod.apply({"params": p_head}, enc2)
        return jnp.sum(out * jnp.where(enc2["a_mask"], 0.0, 1.0)) if mod is actor else jnp.sum(out)

    g_init = flat(jax.grad(head_loss)(a_key, actor, encoded))
    g_key_init = sum(float(jnp.abs(g).sum()) for k, g in g_init.items() if k.startswith("Dense_4/"))
    g_inj_init = sum(float(jnp.abs(g).sum()) for k, g in g_init.items() if k.startswith("Dense_5/"))
    assert g_key_init == 0.0 and g_inj_init > 0, "起步：注入层应有梯度、key 投影梯度应为 0"
    g_after = flat(jax.grad(head_loss)(a2["params"], actor, encoded))
    g_key_after = sum(float(jnp.abs(g).sum()) for k, g in g_after.items() if k.startswith("Dense_4/"))
    assert g_key_after > 0, "注入层离开零点后牌库 key 投影仍无梯度"
    print(f"[14] 牌库集合 OK: 布局 DeckBase={DECK_BASE} DeckSlots={DECK_SLOTS}（尾块 {MAX_CARDS - DECK_END}）| "
          f"只改牌库块 → deck_vec max|Δ|={d_deck:.3f}；只改非牌库槽 → {d_nondeck:.1e} | 空牌库槽不漏入、"
          f"全空牌库 → 0 | 注入层零初始化：牌库路径贡献=0 | 注入层=1e-2 后 只改牌库块 → logits "
          f"max|Δ|={dl:.2e} value max|Δ|={dv:.2e} | 梯度：注入层 Σ|g|={g_inj_init:.2e}（起步）→ key 投影 "
          f"Σ|g|={g_key_after:.2e}")


def main():
    for test in (test_layout, test_shapes, test_masks, test_init_equivalence, test_batch_independence,
                 test_finite, test_grad, test_alpha, test_dropout, test_rollout_labels, test_ppo_update,
                 test_merge, test_entropy, test_deck_set):
        test()
        sys.stdout.flush()
    print("\n全部通过 ✔")


if __name__ == "__main__":
    main()
