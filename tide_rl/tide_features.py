"""
Tide 特征维度常量与辅助函数（对应 ygo-agent 的 features.py）。

TideObservation 产出的扁平特征向量：
- cards_: (92, 116) — 每槽 116 维特征
- global_: (50,) — 全局状态
- actions_: (max_actions, 8) — 每动作 8 维特征
- h_actions_: (32, 14) — 历史动作（暂未实现，传 None）

v3 布局（2026-10-14 精确对齐期）权威契约见 docs/action_space_v3_contract.md——
（v2=2026-09-30，已被取代；对账依据=仓库根 AI训练准备_v3对账报告_2026-10-14.md）
改任何维度/下标前先改契约再改两侧代码；reset 时 TCP 握手逐值断言（tide_env_tcp）。
"""

import numpy as np

# ---- 维度常量（与 TideObservation.cs 同步；v4 2026-10-16：68→116，原子槽 6→12）----
#   [14] LandTrait（v3 复用神佑死维）、[113] AwakenCommitted、[114][115] 格坐标 x/8、z/7（-1=未落格）
#   槽位下标全部由 N_TYPE_SLOTS 派生（与 C# 派生式常量同构）——扩容只改 N_TYPE_SLOTS 一处。
N_CARD_FEATURES = 116
# 内容身份三通路（CardIdentityService 推导、TideCardIndex/原子表注册下标）：
#   1) 精确哈希 [15..28]（14 槽）：[15..26] 十二个原子内容哈希（跨效果按执行序展平，槽位即顺序，
#      参数敏感——「造成4伤」是独立行）、[27] 组合结构哈希、[28] 关键词/tag/光环组合哈希。
#      同单元跨卡共享 embedding 行——相近效果在原子层重叠，语义直接迁移。
#      容量依据 2026-10-16：正式卡满配 9 = 代价1 + 主动/自动组合各按2 + 关键词3，留 3 槽余量。
#   2) 类型下标 [29..40]（12 槽）：原子 EffectType 在冻结原子表内的排名，参数无关——
#      「造成4伤」与「造成5伤」同一行，新参数值不再纯新 token。
#   3) 参数块 [41..112]（12 槽 × 6 维浮点，2026-09-10 目标域模型重定义）：
#      [0]Value / [1]Mana总量 / [2]Mana色数 / [3]kind数 / [4]最小kind / [5]最大kind——数值插值通路。
# 下标槽只用作 embedding 查表（encoder masked-sum + 槽位位置标记），不进 Dense——下标不是幅值。
# 0 = 无该成分/token/未登记。
CARD_ID_START = 15
N_ID_SLOTS = 14          # [15..28] 精确身份（12 原子哈希 + 结构 + 组合）
TYPE_ID_START = 29
N_TYPE_SLOTS = 12        # [29..40] 原子 EffectType 下标（2026-10-16 6→12，正式卡满配9+余量3）
ATOM_PARAM_START = 41    # [41..] 参数块
ATOM_PARAM_DIM = 6      # 与 C# CardIdentityService.AtomParamDim 对齐（2026-09-10: 7→6）
# 精确身份 embedding 表大小，与 C# TideCardIndex.Capacity 对齐（超出容量 C# 侧记 0）。
# TideCardIndex 为追加式分配 + manifest 持久化（tide_rl/card_identity_manifest.json）：
# 新哈希只在末尾续排，训练/部署同一份清单 → 已训行永不串台。
# 容量口径（2026-09-21 扩容 256→1024）：占行的是「原子实例级身份」（原子类型+参数+目标域
# 组合 / 结构骨架 / 关键词组合），不是卡——白板卡不占行、同原子跨卡共享行；
# 1024 ≈ 千张常用卡的互异效果实例余量；触顶静默降级（新哈希记 0，类型+参数通路照常）。
# ---- v3 卡特征命名下标（契约 §4 追加段）----
CARD_F_LANDTRAIT = 14   # 地牌特性（复用神佑死维）
CARD_F_AWAKEN = 113     # 苏醒已立约
CARD_F_POS_X = 114      # 格坐标 x/8（-1=未落格）
CARD_F_POS_Z = 115      # 格坐标 z/7（-1=未落格）
N_CARD_POOL = 1024
# EffectType embedding 表：C# 原子表 97 条（2026-09-10 攻/守效果化 +2）按枚举名排序 1 基编号，
# 256 = 余量（2026-09-21 扩容 128→256，规划原子表将来扩到 256 种）。
# 表冻结（表指纹已混入所有哈希）；表一旦变更 → 全体身份换血，需重训（容量≠免重训）。
N_EFFECT_TYPES = 256
MAX_CARDS = 92  # v3：46/人（战场12+元素池14+手牌12+墓地4+发动区3+场地区1）×2
# ---- 布局版本（v4 2026-10-16：原子槽 6→12、卡特征 68→116）----
# 契约 = docs/action_space_v3_contract.md；C# 侧 TideHeadlessServer reset 回显同值，
# 本侧 reset 逐值断言（防 2026-09-14 N_GLOBAL 32→36 那种静默漂移炸 reshape）。
LAYOUT_VERSION = 4
# 动作截断上限 512（v2 起）：逐目标展开（出牌/发动 × 候选目标）+ 守卫/响应行。
# v3 起 C# 枚举器侧带同值守卫（超限保前 511 + 恒保末位兜底行）——本侧 pad_or_truncate_actions
# 的头截断永不会切掉 EndTurn/PassPriority（v2 理论死锁修复，契约 §6）。
MAX_ACTIONS = 512
# 动作特征 v2 6→8 维：追加 [6] effectIdentity（效果定义注册下标/1024，区分同卡多效果行）
# 与 [7] targetKind（0=无/1=卡/2=对方玩家/3=己方玩家，消除 targetIndex=-1 双义）。
N_ACTION_FEATURES = 8
# 全局特征 v3 49→50（重排序+归一化；对账报告 D5/D7/D14）：
#   g[15][16] 放逐计数（新增）；生命/30、回合/40、势能/30、地牌上限/14、弹药/5 归一化；
#   英雄技能块 [38..41]/[42..45]（[b+3]=角色攻击弹药）；g[47] 栈顶来源/92；
#   删 v2 g[48]（与 g[4] 重复）。
N_GLOBAL_FEATURES = 50
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

# ---- 全局特征命名下标（v3 契约 §5）----
GLOBAL_F_IS_MY_TURN = 4         # 决策座次是否回合玩家（v3 删 g[48] 冗余，本位即权威）
GLOBAL_F_EXILE_ME = 15          # 放逐计数（v3 新增）
GLOBAL_F_EXILE_OPP = 16
GLOBAL_F_SKILL_P1 = 38          # g[38..41] P1 英雄技能 [has, tapped, uses/10, 弹药/5]
GLOBAL_F_SKILL_P2 = 42          # g[42..45] P2 同上
GLOBAL_F_STACK_DEPTH = 46
GLOBAL_F_STACK_TOP_SRC = 47
GLOBAL_F_STACK_TOP_EFFECT = 48
GLOBAL_F_DECISION_CONTEXT = 49  # 0=己方 Main，1=响应窗口

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
    12: "TapCreature",    # v3：横置地牌特性生物产 1 元素（对账 D10）
    13: "CommitAwaken",   # v3：苏醒立约——灰份额免付转沉睡层数（对账 D11）
}
# v2+v3 新增类型（验收关注：这些行的出现率/采样率；v3 期重点 12/13）
NEW_ACTION_TYPES = (6, 7, 8, 9, 10, 11, 12, 13)
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

