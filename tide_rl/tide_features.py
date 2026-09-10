"""
Tide 特征维度常量与辅助函数（对应 ygo-agent 的 features.py）。

TideObservation 产出的扁平特征向量：
- cards_: (80, 71) — 每槽 71 维特征
- global_: (32,) — 全局状态
- actions_: (max_actions, 6) — 每动作 6 维特征
- h_actions_: (32, 14) — 历史动作（暂未实现，传 None）
"""

import numpy as np

# ---- 维度常量（与 TideObservation.cs 同步）----
N_CARD_FEATURES = 71
# 内容身份三通路（CardIdentityService 推导、TideCardIndex/原子表注册下标）：
#   1) 精确哈希 [15..22]（8 槽）：[15..20] 六个原子内容哈希（跨效果按执行序展平，槽位即顺序，
#      参数敏感——「造成4伤」是独立行）、[21] 组合结构哈希、[22] 关键词/tag/光环组合哈希。
#      同单元跨卡共享 embedding 行——相近效果在原子层重叠，语义直接迁移。
#   2) 类型下标 [23..28]（6 槽）：原子 EffectType 在冻结原子表内的排名，参数无关——
#      「造成4伤」与「造成5伤」同一行，新参数值不再纯新 token。
#   3) 参数块 [29..70]（6 槽 × 7 维浮点）：Value/Value2/ManaTypeParam/Duration/
#      DurationValue/TargetCountOverride/DynamicTargetCount——数值插值通路。
# 下标槽只用作 embedding 查表（encoder masked-sum + 槽位位置标记），不进 Dense——下标不是幅值。
# 0 = 无该成分/token/未登记。
CARD_ID_START = 15
N_ID_SLOTS = 8          # [15..22] 精确身份（6 原子哈希 + 结构 + 组合）
TYPE_ID_START = 23
N_TYPE_SLOTS = 6        # [23..28] 原子 EffectType 下标
ATOM_PARAM_START = 29   # [29..] 参数块
ATOM_PARAM_DIM = 7      # 与 C# CardIdentityService.AtomParamDim 对齐
# 精确身份 embedding 表大小，与 C# TideCardIndex.Capacity 对齐（超出容量 C# 侧记 0）。
# TideCardIndex 为追加式分配 + manifest 持久化（tide_rl/card_identity_manifest.json）：
# 新哈希只在末尾续排，训练/部署同一份清单 → 已训行永不串台。
N_CARD_POOL = 256
# EffectType embedding 表：C# 原子表 83 条按枚举名排序 1 基编号，128 = 余量。
# 表冻结（表指纹已混入所有哈希）；表一旦变更 → 全体身份换血，需重训。
N_EFFECT_TYPES = 128
MAX_CARDS = 80
# 动作截断上限：攻击动作 = 攻击方 × (对方随从 + 玩家)，8v8 场面就 ~72 个，64 会截掉
# 真实动作（含末位的 EndTurn——Unity 侧 MaxActionsPerTurn 强制收口兜底但不干净）；
# 128 覆盖 10v10 以内（超出部分仅不可选，不影响正确性）。Unity 侧发全量无截断。
MAX_ACTIONS = 128
N_ACTION_FEATURES = 6
N_GLOBAL_FEATURES = 32
N_HISTORY_ACTIONS = 32
H_ACTIONS_FEATS = 14
N_RNN_CHANNELS = 512

H_ACTIONS_SHAPE = (N_HISTORY_ACTIONS, H_ACTIONS_FEATS)


def sample_input():
    """生成样本输入（用于模型初始化）。卡身份槽填示例下标/参数，覆盖全部通路。"""
    history_actions = np.zeros(H_ACTIONS_SHAPE, dtype=np.float32)
    cards = np.zeros((MAX_CARDS, N_CARD_FEATURES), dtype=np.float32)
    for i in range(MAX_CARDS):
        b = i * N_CARD_FEATURES
        cards.flat[b + 0] = 1.0  # valid
        for s in range(N_ID_SLOTS):
            cards.flat[b + CARD_ID_START + s] = 1 + (i + s) % (N_CARD_POOL - 1)
        for s in range(N_TYPE_SLOTS):
            cards.flat[b + TYPE_ID_START + s] = 1 + (i + s) % 83
        for s in range(N_TYPE_SLOTS):
            for p in range(ATOM_PARAM_DIM):
                cards.flat[b + ATOM_PARAM_START + s * ATOM_PARAM_DIM + p] = float(s + p)
    global_ = np.zeros(N_GLOBAL_FEATURES, dtype=np.float32)
    legal_actions = np.zeros((MAX_ACTIONS, N_ACTION_FEATURES), dtype=np.float32)
    legal_actions[:, 0] = 1.0  # valid
    return {
        "cards_": cards,
        "global_": global_,
        "actions_": legal_actions,
        "h_actions_": history_actions,
    }


def init_rstate(rnn_type="gru"):
    """
    初始化 RNN 状态。

    Args:
        rnn_type: "gru" 或 "lstm"

    Returns:
        GRU: (1, 512) 单个状态
        LSTM: ((1, 512), (1, 512)) 元组 (h, c)
    """
    if rnn_type == "gru":
        return np.zeros((1, N_RNN_CHANNELS), dtype=np.float32)
    elif rnn_type == "lstm":
        return (
            np.zeros((1, N_RNN_CHANNELS), dtype=np.float32),
            np.zeros((1, N_RNN_CHANNELS), dtype=np.float32),
        )
    else:
        raise ValueError(f"Unknown rnn_type: {rnn_type}")


def pad_or_truncate_actions(actions, max_actions=MAX_ACTIONS):
    """
    动作列表 pad 到固定长度（非法位 valid=0 掩码）。

    Args:
        actions: (n, N_ACTION_FEATURES) 数组
        max_actions: 目标长度

    Returns:
        (max_actions, N_ACTION_FEATURES) 数组
    """
    n = actions.shape[0]
    if n >= max_actions:
        return actions[:max_actions]
    else:
        padded = np.zeros((max_actions, N_ACTION_FEATURES), dtype=np.float32)
        padded[:n] = actions
        return padded
