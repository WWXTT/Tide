"""actor 初始熵回归测试：点积打分 ÷√channels 后，初始化策略应接近合法动作均匀分布。

背景（2026-09-09）：裸点积（LayerNorm 后两侧每维 ~N(0,1)，128 维）幅度 ≈ √128 ≈ 11，
初始化即近 argmax——实测初始熵 0.48（10 合法动作均匀熵 ln10≈2.30），训练 7 个 update
内熵精确归零、探索死亡（三轮复现，139k 步不恢复）。修复 = logits / sqrt(channels)。
"""

import numpy as np
import jax
import jax.numpy as jnp

from tide_features import sample_input, MAX_ACTIONS
from tide_encoder import TideEncoder
from tide_agent import TideActor

N_LEGAL = 10  # 典型回合合法动作数（手牌 5-7 + 出元素 + EndTurn 量级）

obs = sample_input()
batch = {"cards_": obs["cards_"][None], "global_": obs["global_"][None],
         "actions_": obs["actions_"][None], "h_actions_": obs["h_actions_"][None]}

enc = TideEncoder(channels=128)
actor = TideActor(channels=128)

enc_params = enc.init(jax.random.PRNGKey(1), batch)
encoded = enc.apply(enc_params, batch)
# 只开前 N_LEGAL 个动作为合法（a_mask 语义：True = 非法屏蔽；enc.apply 出来是 numpy 数组）
a_mask = np.asarray(encoded["a_mask"]).copy()
a_mask[0, :] = True
a_mask[0, :N_LEGAL] = False
encoded["a_mask"] = jnp.asarray(a_mask)

params = actor.init(jax.random.PRNGKey(0), encoded)

logits = actor.apply(params, encoded)[0]  # (max_actions,)
legal = ~encoded["a_mask"][0]
logp = jax.nn.log_softmax(jnp.where(legal, logits, -1e9))
p = jnp.exp(logp[legal])
entropy = float(-(p * logp[legal]).sum())

uniform = float(np.log(N_LEGAL))
print(f"初始熵 = {entropy:.3f} nats（均匀 = ln{N_LEGAL} = {uniform:.3f}）")
assert entropy > 0.8 * uniform, (
    f"初始熵 {entropy:.3f} 远低于均匀 {uniform:.3f}——温度缩放失效，探索会再次塌缩"
)
print("通过 ✔（策略从近均匀起步，探索存活）")
