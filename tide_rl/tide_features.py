"""
Tide 特征维度常量与辅助函数（对应 ygo-agent 的 features.py）。

TideObservation 产出的扁平特征向量：
- cards_: (80, 65) — 每槽 65 维特征
- global_: (49,) — 全局状态（2026-09-30 v2 起 49 维：追加英雄技能状态×2 + 栈/决策上下文 5 维）
- actions_: (max_actions, 8) — 每动作 8 维特征（v2：追加 effectIdentity + targetKind）
- h_actions_: (32, 14) — 历史动作（暂未实现，传 None）

v2 布局（2026-09-30 C 期）权威契约见 docs/action_space_v2_contract.md——
改任何维度/下标前先改契约再改两侧代码；reset 时 TCP 握手逐值断言（tide_env_tcp）。
"""

import numpy as np

# ---- 维度常量（与 TideObservation.cs 同步；2026-09-10 目标域模型 71→65）----
N_CARD_FEATURES = 65
# 内容身份三通路（CardIdentityService 推导、TideCardIndex/原子表注册下标）：
#   1) 精确哈希 [15..22]（8 槽）：[15..20] 六个原子内容哈希（跨效果按执行序展平，槽位即顺序，
#      参数敏感——「造成4伤」是独立行）、[21] 组合结构哈希、[22] 关键词/tag/光环组合哈希。
#      同单元跨卡共享 embedding 行——相近效果在原子层重叠，语义直接迁移。
#   2) 类型下标 [23..28]（6 槽）：原子 EffectType 在冻结原子表内的排名，参数无关——
#      「造成4伤」与「造成5伤」同一行，新参数值不再纯新 token。
#   3) 参数块 [29..64]（6 槽 × 6 维浮点，2026-09-10 目标域模型重定义）：
#      [0]Value / [1]Mana总量 / [2]Mana色数 / [3]kind数 / [4]最小kind / [5]最大kind——数值插值通路。
# 下标槽只用作 embedding 查表（encoder masked-sum + 槽位位置标记），不进 Dense——下标不是幅值。
# 0 = 无该成分/token/未登记。
CARD_ID_START = 15
N_ID_SLOTS = 8          # [15..22] 精确身份（6 原子哈希 + 结构 + 组合）
TYPE_ID_START = 23
N_TYPE_SLOTS = 6        # [23..28] 原子 EffectType 下标
ATOM_PARAM_START = 29   # [29..] 参数块
ATOM_PARAM_DIM = 6      # 与 C# CardIdentityService.AtomParamDim 对齐（2026-09-10: 7→6）
# 精确身份 embedding 表大小，与 C# TideCardIndex.Capacity 对齐（超出容量 C# 侧记 0）。
# TideCardIndex 为追加式分配 + manifest 持久化（tide_rl/card_identity_manifest.json）：
# 新哈希只在末尾续排，训练/部署同一份清单 → 已训行永不串台。
# 容量口径（2026-09-21 扩容 256→1024）：占行的是「原子实例级身份」（原子类型+参数+目标域
# 组合 / 结构骨架 / 关键词组合），不是卡——白板卡不占行、同原子跨卡共享行；
# 1024 ≈ 千张常用卡的互异效果实例余量；触顶静默降级（新哈希记 0，类型+参数通路照常）。
N_CARD_POOL = 1024
# EffectType embedding 表：C# 原子表 97 条（2026-09-10 攻/守效果化 +2）按枚举名排序 1 基编号，
# 256 = 余量（2026-09-21 扩容 128→256，规划原子表将来扩到 256 种）。
# 表冻结（表指纹已混入所有哈希）；表一旦变更 → 全体身份换血，需重训（容量≠免重训）。
N_EFFECT_TYPES = 256
MAX_CARDS = 80
# ---- v2 布局版本（2026-09-30 C 期：效果发动时机 + 目标选择进动作空间）----
# 契约 = docs/action_space_v2_contract.md；C# 侧 TideHeadlessServer reset 回显同值，
# 本侧 reset 逐值断言（防 2026-09-14 N_GLOBAL 32→36 那种静默漂移炸 reshape）。
LAYOUT_VERSION = 2
# 动作截断上限 v2 128→512：逐目标展开（出牌/发动 × 候选目标）+ 守卫/响应行后，
# 攻击叉积 18×19≈342 就曾偶发超 128；512 覆盖最坏场面（截断只丢可选性，EndTurn/PassPriority
# 恒排末位保证回合/窗口必然流动）。Unity 侧 OnnxTidePolicy.MaxActions 同值。
MAX_ACTIONS = 512
# 动作特征 v2 6→8 维：追加 [6] effectIdentity（效果定义注册下标/1024，区分同卡多效果行）
# 与 [7] targetKind（0=无/1=卡/2=对方玩家/3=己方玩家，消除 targetIndex=-1 双义）。
N_ACTION_FEATURES = 8
# 全局特征 v2 36→49：追加 g[36..43] 双方英雄技能状态、g[44..46] 栈深度/栈顶来源/栈顶效果、
# g[47] 决策上下文（0=己方Main/1=响应窗口）、g[48] 决策座次是否回合玩家。
N_GLOBAL_FEATURES = 49
N_HISTORY_ACTIONS = 32
H_ACTIONS_FEATS = 14
N_RNN_CHANNELS = 512

# ---- 动作特征命名下标（与 C# LegalActionEnumerator.BuildFeatures 槽位一一对应）----
ACTION_F_VALID = 0
ACTION_F_TYPE = 1
ACTION_F_SOURCE = 2
ACTION_F_TARGET = 3
ACTION_F_MODE = 4
ACTION_F_COST = 5
ACTION_F_EFFECT_ID = 6
ACTION_F_TARGET_KIND = 7

# ---- 全局特征命名下标（v2 追加段，契约第 3 节）----
GLOBAL_F_SKILL_P1 = 36   # g[36..39] P1 英雄技能 [has, tapped, uses/10, upgraded]
GLOBAL_F_SKILL_P2 = 40   # g[40..43] P2 同上
GLOBAL_F_STACK_DEPTH = 44
GLOBAL_F_STACK_TOP_SRC = 45
GLOBAL_F_STACK_TOP_EFFECT = 46
GLOBAL_F_DECISION_CONTEXT = 47  # 0=己方 Main，1=响应窗口
GLOBAL_F_IS_TURN_PLAYER = 48    # 决策座次是否回合玩家

# 动作类型对照（C# TideActionType；audit_action_space / 训练统计用）
ACTION_TYPES = {
    0: "PlayCard",
    1: "PlayLand",
    3: "Activate",
    4: "Attack",
    5: "EndTurn",
    6: "HeroSkill",
    7: "VoluntaryTrigger",
    8: "PassPriority",
    9: "RespondPlay",
    10: "RespondActivate",
    11: "Guard",
}
# v2 新增类型（C 期验收关注：这些行的出现率/采样率）
NEW_ACTION_TYPES = (6, 7, 8, 9, 10, 11)
# 带目标动作（targetKind=1 时 targetIndex 语义为 cards_ 槽位）
TARGETED_ACTION_TYPES = (0, 3, 4, 9, 10, 11)

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
            cards.flat[b + TYPE_ID_START + s] = 1 + (i + s) % 97
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
