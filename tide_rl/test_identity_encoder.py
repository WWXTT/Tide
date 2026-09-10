"""身份三通路编码器 shape 自测：常量一致性 + 前向传播 + 泛化通路差异验证。

不依赖 Unity（obs 由 sample_input 构造）。验证点：
1. 维度自洽：卡特征布局 [15..22]/[23..28]/[29..] 与 C# 常量对齐（手工断言）；
2. 前向：TideCardEncoder / TideEncoder 输出 shape 与掩码正确；
3. 泛化差异：同 EffectType 不同参数（4 伤 vs 5 伤）在通路 2+3 下表示不同、
   而精确哈希槽不同（未登记记 0）时仍有区分度——数值通路存在的意义。
"""

import numpy as np
import jax
import jax.numpy as jnp

from tide_features import (
    N_CARD_FEATURES, CARD_ID_START, N_ID_SLOTS, TYPE_ID_START, N_TYPE_SLOTS,
    ATOM_PARAM_START, ATOM_PARAM_DIM, N_CARD_POOL, N_EFFECT_TYPES, MAX_CARDS,
    sample_input,
)
from tide_encoder import TideCardEncoder, TideEncoder

# ---- 1. 布局自洽（与 TideObservation.cs 手工对齐）----
assert N_CARD_FEATURES == 71, N_CARD_FEATURES
assert CARD_ID_START + N_ID_SLOTS == TYPE_ID_START            # [15..22] 紧接 [23..]
assert TYPE_ID_START + N_TYPE_SLOTS == ATOM_PARAM_START        # [23..28] 紧接 [29..]
assert ATOM_PARAM_START + N_TYPE_SLOTS * ATOM_PARAM_DIM == N_CARD_FEATURES
print(f"[1] 布局自洽 OK: N_CARD_FEATURES={N_CARD_FEATURES}, "
      f"ids[{CARD_ID_START}..{CARD_ID_START+N_ID_SLOTS}) types[{TYPE_ID_START}..{TYPE_ID_START+N_TYPE_SLOTS}) "
      f"params[{ATOM_PARAM_START}..{N_CARD_FEATURES})")

# ---- 2. 前向 shape ----
obs = sample_input()
assert obs["cards_"].shape == (MAX_CARDS, N_CARD_FEATURES)
assert int(obs["cards_"][:, TYPE_ID_START:TYPE_ID_START+N_TYPE_SLOTS].max()) < N_EFFECT_TYPES

batch = {"cards_": obs["cards_"][None], "global_": obs["global_"][None],
         "actions_": obs["actions_"][None], "h_actions_": obs["h_actions_"][None]}
enc = TideCardEncoder(channels=128)
params = enc.init(jax.random.PRNGKey(0), batch["cards_"])
cards_enc, c_mask = enc.apply(params, batch["cards_"])
assert cards_enc.shape == (1, MAX_CARDS, 128), cards_enc.shape
assert c_mask.shape == (1, MAX_CARDS)

total = TideEncoder(channels=128)
tparams = total.init(jax.random.PRNGKey(0), batch)
out = total.apply(tparams, batch)
assert out["cards"].shape == (1, MAX_CARDS, 128)
assert out["actions"].shape == (1, 128, 128)  # MAX_ACTIONS=128, channels=128
assert out["global"].shape == (1, 128)
print(f"[2] 前向 OK: cards {out['cards'].shape}, actions {out['actions'].shape}, global {out['global'].shape}")

# ---- 3. 泛化通路：同类型不同参数 → 表示不同（数值插值存在性）----
def make_card(atom_hash_idx, type_idx, value):
    f = np.zeros(N_CARD_FEATURES, dtype=np.float32)
    f[0] = 1.0                 # valid
    f[CARD_ID_START] = atom_hash_idx   # 精确哈希（0 = 未登记）
    f[TYPE_ID_START] = type_idx        # DealDamage 类型行（共享）
    f[ATOM_PARAM_START] = value        # Value
    return f

cards = np.zeros((1, 2, N_CARD_FEATURES), dtype=np.float32)
cards[0, 0] = make_card(0, 7, 4.0)   # 造成4伤：哈希未登记（新参数值场景）→ 纯类型+参数通路
cards[0, 1] = make_card(0, 7, 5.0)   # 造成5伤：同一类型行，仅参数不同
out4, _ = enc.apply(params, cards[:, 0:1])
out5, _ = enc.apply(params, cards[:, 1:2])
diff = float(jnp.abs(out4[0, 0] - out5[0, 0]).max())
assert diff > 1e-6, "同类型不同参数应产生不同表示"
print(f"[3] 泛化通路 OK: 造成4伤 vs 造成5伤 表示差异 max|Δ|={diff:.4f}（类型行共享 + 参数线性响应）")

print("\n全部通过 ✔")
