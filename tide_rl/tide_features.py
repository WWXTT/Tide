"""
Tide 特征维度常量与辅助函数（对应 ygo-agent 的 features.py）。

TideObservation 产出的扁平特征向量：
- cards_: (80, 20) — 每槽 20 维特征
- global_: (32,) — 全局状态
- actions_: (max_actions, 6) — 每动作 6 维特征
- h_actions_: (32, 14) — 历史动作（暂未实现，传 None）
"""

import numpy as np

# ---- 维度常量（与 TideObservation.cs 同步）----
N_CARD_FEATURES = 24
# 内容身份下标槽（原子级拆散，TideObservation [15..23]）：[15..20] 六个原子内容哈希下标
# （跨效果按执行序展平，槽位即顺序）、[21] 组合结构哈希、[22] 关键词/tag/光环组合哈希、
# [23] 预留（CardIdentityService 推导、TideCardIndex 注册）。0 = 无该成分/token/未登记。
# 只用作 embedding 查表下标（encoder masked-sum + 槽位位置标记组合），不进 Dense——
# 下标不是幅值。同原子（如 造成伤害4）跨卡共享 embedding 行——相近效果在原子层重叠。
CARD_ID_START = 15
N_ID_SLOTS = 9
# 身份 embedding 表大小，与 C# TideCardIndex.Capacity 对齐（超出容量 C# 侧记 0）
N_CARD_POOL = 256
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
    """生成样本输入（用于模型初始化）。"""
    history_actions = np.zeros(H_ACTIONS_SHAPE, dtype=np.float32)
    cards = np.zeros((MAX_CARDS, N_CARD_FEATURES), dtype=np.float32)
    global_ = np.zeros(N_GLOBAL_FEATURES, dtype=np.float32)
    legal_actions = np.zeros((MAX_ACTIONS, N_ACTION_FEATURES), dtype=np.float32)
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
