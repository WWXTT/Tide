"""
Tide 特征维度常量与辅助函数（对应 ygo-agent 的 features.py）。

TideObservation 产出的扁平特征向量：
- cards_: (191, 158) — 每槽 158 维特征（v21 2026-11-24 卡组分布与策略分界：+[157] countInZone
  同区张数——己方牌库块每种唯一卡一槽、该维写张数，其余区恒 1；同批生物攻/守资格按「带发动条件
  的主动效果」进内容身份：原子槽 [10]=攻击/[11]=守卫（效果原子占 [0..9]）、结构哈希含其骨架、
  [7] 可攻击粗判加攻资格闸；
  v19 2026-11-20 绿套补盲：原子参数块 6→9 维 116→152
  （+settleKind/branchParam/branchBudget/costTotal，删 Mana 两死维），坐标/zoneKind 位随之后移；
  同日晚批死亡计数原子属性化再 +死亡计数维 [152]→153；v20 召唤落位/团结观测 +[153] 邻接
  己方生物数/6 与 [154..156] 引擎奖励摘要→157）；
  v15 2026-11-15 曾加 [116..119] 地牌可产掩码，同日多色回退单色作废；
  v5 2026-10-29：删苏醒立约位 116→115；
  v6 P11 盘面重塑：卡槽 92→86 = 每人 43 槽；v9 2026-11-02 牌库双通道：
  尾加 [115] zoneKind 115→116 + 尾接己方牌库块 30 槽 86→116；
  v10 2026-11-02 观测欠账四件套：墓地区 4→30、除外 8 入册、牌库顶槽 1、
  对方牌库被展示 4 → 每人 77 槽 + 尾块 35 = 189）
- global_: (100,) — 全局状态（v9：+8 对方牌库开局标签 g[50..57]；v19：+12 剩余可用元素块 g[58..69]；
  v20：+9 占用码 g[70..78] + 9 格团结邻接 g[79..87]；v21：+8 己方牌库开局标签 g[88..95]
  + g[96] 己方牌库唯一卡超 30 种的截断标记；v21 定稿 → 2026-11-26 v22：LAYOUT_VERSION 21→22、
  动作特征 20→26（二期 S2a；globals 保持 100）：+g[97]/g[98] 双方手牌上限 + g[99] 己方超上限
  剩余待弃张数——A0 逐张弃配套）
- actions_: (max_actions, N_ACTION_FEATURES) — 每动作特征维（v8=11→v14 起=14→v20 起=20→v22 起=26）
- h_actions_: (32, 14) — 历史动作（暂未实现，传 None）

v3 布局（2026-10-14 精确对齐期）权威契约见 docs/action_space_v3_contract.md——
（v2=2026-09-30，已被取代；对账依据=仓库根 AI训练准备_v3对账报告_2026-10-14.md）
改任何维度/下标前先改契约再改两侧代码；reset 时 TCP 握手逐值断言（tide_env_tcp）。
"""

import numpy as np

# ---- 维度常量（与 TideObservation.cs 同步；v15 2026-11-15 地牌多色可产掩码 116→120 同日
#   随「多色回退单色·自动横置」拍板作废——回退 v14/116（v9 牌库双通道：115→116 尾加 zoneKind）----
#   [14] LandTrait（v3 复用神佑死维）、[113][114] 格坐标 x/7、z/7（-1=未落格）、
#   [115] zoneKind 0..9（0=战场 1=元素池 2=手牌 3=墓地 4=发动区 5=场地区 6=己方牌库块
#         7=牌库顶虚拟手牌 8=对方牌库被展示 9=除外区；对方隐藏手牌=2——编码器逐卡独立
#         无槽位位置嵌入，此维是区域可分性的唯一信号）
#   （v4 2026-10-16：68→116，原子槽 6→12；v5 起立约位删除、坐标位前移一格）
#   槽位下标全部由 N_TYPE_SLOTS 派生（与 C# 派生式常量同构）——扩容只改 N_TYPE_SLOTS 一处。
N_CARD_FEATURES = 158  # v19：152→153（+死亡计数维 [152]）；v20（2026-11-20 召唤落位/团结观测）：
#   153→157（+[153] 邻接己方生物数/6（团结口径，SummonPlacementRules.AdjacentOwnCreatures 单源）
#   +[154..156] 引擎奖励摘要 EngineRewardOf（载荷数钳3/首奖励原子类型/首奖励值钳99——不进身份哈希）；
#   v21（2026-11-24 卡组分布与策略分界）：→158（+[157] countInZone 同区张数——己方牌库块=该唯一卡
#   组内张数（原始计数不归一化、钳 99）、其余区恒 1、空槽 0；不变量 valid=1 ⇔ countInZone≥1）
# 内容身份三通路（CardIdentityService 推导、TideCardIndex/原子表注册下标）：
#   1) 精确哈希 [15..28]（14 槽）：[15..26] 十二个原子内容哈希（跨效果按执行序展平，槽位即顺序，
#      参数敏感——「造成4伤」是独立行）、[27] 组合结构哈希、[28] 关键词/tag/光环组合哈希。
#      同单元跨卡共享 embedding 行——相近效果在原子层重叠，语义直接迁移。
#      容量依据 2026-10-16：正式卡满配 9 = 代价1 + 主动/自动组合各按2 + 关键词3，留 3 槽余量。
#   2) 类型下标 [29..40]（12 槽）：原子 EffectType 在冻结原子表内的排名，参数无关——
#      「造成4伤」与「造成5伤」同一行，新参数值不再纯新 token。
#   3) 参数块 [41..148]（12 槽 × 9 维浮点，v19 2026-11-20 绿套补盲扩维）：
#      [0]value（引擎主干原子=解析后门槛 x：显式 value>1 → engineParam → Then 锚价推导兜底；
#               开 deathToll 增幅的原子=本体声明值）
#      [1]settleKind（0=无分支/1=有限 Gate/2=产出 Outcome/3=事件引擎 Engine）
#      [2]branchParam（settle=2→condParam；settle=3→engineParam；死亡计数不走分支——
#               2026-11-20 原子属性化（deathToll 布尔），计数实时值在卡特征 [152]）
#      [3]branchBudget（settle=1 门槛行 Budget 列灰价） [4]kind数 [5]最小kind [6]最大kind
#      [7]amp [8]costTotal（所属效果 e.Costs 合计全价 ≤3——代价进观测）——数值插值通路。
#      （2026-09-10 的 6 维 Mana 两死维删除；顺带修掉 C# 写入器实写 7 值与 dim=6 的错位。）
# v21 战斗底盘预留槽（2026-11-24）：生物的原子槽 [10]=攻击资格原子、[11]=守卫资格原子——卡面声明的
#   攻/守资格按「带发动条件的主动效果」进内容身份（C# CardIdentityService.ChassisAttackSlot/GuardSlot：
#   攻击=Activate_Active 主动、守卫=Activate_Response 响应；类型行 AtomTypeIndex("Attack"/"Guard")），
#   效果原子占 [0..9]。三路平行：精确哈希 [25][26]、类型下标 [39][40]、参数块第 10/11 槽（[131..148]）；
#   未声明=0，非生物两槽恒 0。
# 下标槽只用作 embedding 查表（encoder masked-sum + 槽位位置标记），不进 Dense——下标不是幅值。
# 0 = 无该成分/token/未登记。
CARD_ID_START = 15
N_ID_SLOTS = 14          # [15..28] 精确身份（12 原子哈希 + 结构 + 组合）
TYPE_ID_START = 29
N_TYPE_SLOTS = 12        # [29..40] 原子 EffectType 下标（2026-10-16 6→12，正式卡满配9+余量3）
ATOM_PARAM_START = 41    # [41..] 参数块
ATOM_PARAM_DIM = 9      # 与 C# CardIdentityService.AtomParamDim 对齐（v19 2026-11-20: 6→9）
# 精确身份 embedding 表大小，与 C# TideCardIndex.Capacity 对齐（超出容量 C# 侧记 0）。
# TideCardIndex 为追加式分配 + manifest 持久化（tide_rl/card_identity_manifest.json）：
# 新哈希只在末尾续排，训练/部署同一份清单 → 已训行永不串台。
# 容量口径（2026-09-21 扩容 256→1024）：占行的是「原子实例级身份」（原子类型+参数+目标域
# 组合 / 结构骨架 / 关键词组合），不是卡——白板卡不占行、同原子跨卡共享行；
# 1024 ≈ 千张常用卡的互异效果实例余量；触顶静默降级（新哈希记 0，类型+参数通路照常）。
# ---- v3 卡特征命名下标（契约 §4 追加段；v5 2026-10-29：删 CARD_F_AWAKEN，坐标位前移）----
CARD_F_LANDTRAIT = 14   # 地牌特性（复用神佑死维）
CARD_F_POS_X = 149      # 格坐标 x/7（-1=未落格；v6 盘面重塑 Width 8→7；v19 参数块 6→9 维 113→149）
CARD_F_POS_Z = 150      # 格坐标 z/7（-1=未落格；v19：114→150）
CARD_F_ZONE = 151       # zoneKind 0..10（v9 新增；v10 扩 7/8/9；v15 扩 10=场地卡专用槽——
                        # 战场槽 0 仍只含单位；显式映射防枚举重排；契约 §4；v19：115→151）
CARD_F_DEATHTOLL = 152  # 死亡计数原始层数（2026-11-20 原子属性化补 1 维；2026-11-24：累计无上限、
                        # 去 /9 归一化——指示物 state 口径，与毒素计数同族；进战场冻结、离场清零；
                        # 增幅开关=原子 deathToll 布尔（哈希段 dt=1），计价=基价+2 开启费）
# 旧 CARD_F_AWAKEN = 113（苏醒已立约）已删（2026-10-29 P6）：立约决策退役，
# 苏醒并入 PlayCard 的 awaken 变体行（动作侧），卡特征不再编码。
# P-1（2026-11-19 C 方案稳定下标，docs/AI动作空间补全_修改计划.md §0.4）：身份下标纯派生
# index = 1 + splitmix64(hash) % (pool-1)，hash 0 → 0——与池/生成顺序/manifest 无关，
# main/train 跨进程同号；旧「manifest 追加式分配 + 未登记=0」语义废弃（card_identity_manifest.json
# 仅存档不参与训练/导出）。card embedding 1024→16384（~16× 参数）、type 256→4096。
N_CARD_POOL = 16384
# EffectType embedding 表：EffectType 枚举名 → 稳定下标（1 基，splitmix64(FNV1a64(枚举名))）。
# P-1 后与 manifest type 行无关（追加式冻结分配已废弃）；4096 = 稳定派生池（122 type 期望
# 碰撞 ~1.8，见 §0.4.5——碰撞=共享 embedding 行可接受，不做按插入序探测）。
N_EFFECT_TYPES = 4096


def _mask64(x: int) -> int:
    return x & 0xFFFFFFFFFFFFFFFF


def splitmix64(seed: int) -> int:
    """SplitMix64（标准常数）——P-1 稳定下标派生核，与 C# CardIdentityService.SplitMix64 逐位一致。"""
    z = _mask64(seed + 0x9E3779B97F4A7C15)
    z = _mask64((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9)
    z = _mask64((z ^ (z >> 27)) * 0x94D049BB133111EB)
    return _mask64(z ^ (z >> 31))


def fnv1a64(s: str) -> int:
    """FNV-1a 64（逐码元 ord——EffectType 枚举名/效果 Id 均 ASCII，与 C# 逐 UTF-16 码元同值）。"""
    if not s:
        return 0
    h = 0xCBF29CE484222325
    for ch in s:
        h = _mask64((h ^ ord(ch)) * 0x100000001B3)
    return h


def stable_index(seed: int, pool: int) -> int:
    """P-1 §0.4.2 单源公式：index = 1 + splitmix64(seed) % (pool - 1)；seed 0 → 0（无）。
    与 C# CardIdentityService.StableIndex 同式（对拍见 StableIndexVerifier）。"""
    return 0 if seed == 0 else 1 + splitmix64(seed) % (pool - 1)
MAX_CARDS = 191 # v15 场地特殊化（2026-11-18）：189→191 = 尾部场地专用块 2 槽
                #（[189]=己方场地卡 / [190]=对方场地卡，zoneKind=10，无场地=空槽恒 0）；
                # 战场槽 [0..8] 只编码单位（剔场地，槽序=单位序）、g[7]/g[8] 只数单位。
                # v10 观测欠账四件套（2026-11-02）：116→189 = 每人 77 槽×2（墓地 4→30、除外 8 入册）
                # + 尾块 35（己方牌库 30（v9，排序编码不泄露洗牌序；v21 起每槽=一种唯一卡、[157] 写
                # 张数——30 槽=唯一卡种类上限，>30 种截断并置 g[96]）+ 牌库顶 1（TopDeckAsHandAura
                # 光环命中才编码——防泄露下一抽）+ 对方牌库被展示 4（RevealRules））
                #（对方牌库本体仍不进 cards_，走 g[50..57] 开局标签通道）；旧值 189=v10..v14、
                # 116=v9 牌库双通道、86=v6 P11 盘面重塑（43/人×2）、92=46/人（战场12，v6 前）
# ---- 布局版本（v12 2026-11-06 墓碑清理：TideActionType 历史空洞（墓地出牌 2 / 立约 13）移除、
# 全枚举连续重排 0..12 并补 Charge=12——历史动作特征[1] 的 type 数值全线漂移，旧模型作废；
# v11 2026-11-05 充能系统动作化：动作空间语义变更——Charge 行从「带充能效果的卡
# 逐色一行（Color 承载选色）」改为「任意手牌卡每卡一行（免元素、选色移交引擎结算期反问）」，
# 充能从主动效果入栈改为速度 0 系统动作；观测布局（槽位/特征数）不变，旧 v10 模型对充能行
# 语义不符作废。同日 ID 冻结收口：身份哈希拆整表指纹盐、manifest 升 v2（scheme 头 + type 行
# 冻结 EffectType 下标）——表编辑不再换血，但本次切换为最后一次全量重键，旧模型全部作废；
# v10 2026-11-02 观测欠账四件套：卡槽 116→189（墓地区 4→30、除外 8 入册、
# 牌库顶槽 1=光环命中才编码防泄露下一抽、对方牌库被展示 4），NCard/NGlobal 不变（zoneKind 扩 7/8/9）；
# v9 2026-11-02 牌库双通道：卡槽 86→116（+己方牌库块 30）、卡特征 115→116
#（+zoneKind）、globals 50→58（+对方开局标签 g[50..57]，随卡组创建保留整局恒定）；
# v8 2026-11-02 LAE 多目标：动作特征 8→11 维——追加 [8]target2/[9]target3/[10]nTargets，
# 多目标组合行次目标可见；v7 2026-10-29 对座镜像：盘面坐标轴平移——双方六格行同列带 x1..x6、P2 上下镜像，
# 槽位/容量同 v6；v6 P11 盘面重塑：战场槽 12→9、卡槽 92→86；v5 P6：删卡特征[113] 116→115）----
# 契约 = docs/action_space_v3_contract.md；C# 侧 TideHeadlessServer reset 回显同值，
# 本侧 reset 逐值断言（防 2026-09-14 N_GLOBAL 32→36 那种静默漂移炸 reshape）。
# v14 2026-11-15 目标区域共享规格 v0.2：动作特征 12→14 维（[12]targetZoneRank/[13]targetSideRank，
# 首目标区域/归属，-1=无目标）；训练未开始、旧模型/ONNX/fixture 作废。
# v15 2026-11-15 地牌多色可产掩码（116→120）同日随「多色回退单色·产色不可自选·自动横置支付」
# 拍板作废——回退 v14（动作 14 维不动）；旧 ONNX/fixture 作废口径不变。
# v15 2026-11-18 场地特殊化：卡槽 189→191（尾部场地专用块 [189]/[190]，zoneKind 扩 10=场地卡）、
# 战场槽只编码单位（槽序=单位序）、g[7]/g[8] 剔场地；LAE 追加攻击场地行（目标=场地专用槽）。
# 旧 checkpoint/ONNX/fixture 全部作废（11-15 的 v15 多色掩码口径已随回退作废，无冲突）。
# v16 2026-11-19 P-1 稳定 ID 派生下标（C 方案）：身份下标语义换血——cardPool 1024→16384、
# typePool 256→4096（SplitMix64 纯派生，manifest 退役）；观测/动作特征维度不变（116/14/58），
# 但下标取值分布与 embedding 表大小变化——旧 checkpoint/ONNX/fixture 全部作废。
# v17 2026-11-19 P1 v0.3 移动/宣言：动作特征 14→17 维（+[14]moveCellX/[15]moveCellZ/
# [16]declaredNameIndex——InteractionParams 透传，LAE 移动行=目标×合法空格、宣言行=候选名）；
# v18 2026-11-19 P2 永久物落格：17→18 维（+[17]placementCell=己方 UnitCells 格下标 0..8，
# -1=非落格行——永久物 PlayCard 行×空格逐格展开，Apply 声明成功即 PinCard）。
# v19 2026-11-20 绿套补盲（docs/AI绿套学习与观测补盲_修改计划.md）：globals 58→70
#（+g[58..69] 剩余可用元素块：双方四色/Any/总量——「还差几点出大哥」「对手能否响应」可算，
# 牺牲/蛰伏的 Any 收益首次可见）；卡特征 116→152（原子参数块 6→9 维：+settleKind/
# branchParam/branchBudget/costTotal，删 Mana 两死维；分支身份进原子哈希→P-1 稳定下标重键）；
# 同日晚批死亡计数原子属性化（settle=3 engine=DeathToll 分支挂载退役→原子 deathToll 布尔）
# 再 +死亡计数维 [152]→卡特征 153（LayoutVersion 保持 19——bootcamp 副本同步由用户安排）。
# v20（2026-11-20 召唤落位+团结观测，docs/AI召唤落位与团结观测_修改计划.md）：LayoutVersion 19→20
# ——卡特征 157 / globals 88 / 动作 20（见各常量注）；召唤落位计划行（默认+锚点≤12，
# Interaction.SummonCells）、[17] 类型闸修复（Activate 行此前被折叠）、团结口径只数活生物。
# 旧 checkpoint/ONNX/fixture 全部作废（按 dims 拒载回落 SimpleAI）；副本须整体刷新后再开训。
# v21（2026-11-24 观测·卡组分布与策略分界，docs/观测_卡组分布与策略分界_实施方案.md）：LayoutVersion 20→21
# ——卡特征 158 / globals 97 / 动作 20 不变。①己方牌库块 [154..183] 语义换血：由「每张物理卡一槽」改
# 「每种唯一卡一槽 + [157] countInZone 张数」（30 槽=唯一卡种类上限；此前绿 60 张被 30 槽截断只见 2 种；
# >30 种按排序键截断并置 g[96]）。②+g[88..95] 己方牌库开局标签（与对方 g[50..57] 同函数同口径）。
# ③同批补盲（维度不变、身份换血）：生物攻/守资格按「带发动条件的主动效果」进内容身份——原子槽
# [10]=攻击/[11]=守卫、结构哈希含其骨架、卡特征 [7] 可攻击粗判加攻资格闸（此前火灵·攻/守仅攻守
# 资格不同、观测逐位相同）。旧 checkpoint/ONNX/fixture 全部作废；副本须同步后再开训。
# v22（2026-11-26 二期 S2a，docs/二期训练架构_实施计划.md §4）：**时域/粒度修复**——多步目标
# 拾取折叠为「选项行」（macro，TargetPickGate.OpenAndAutoComplete 按共享序一次完成），动作特征
# 20→26：+[20]pickMode（0=普通行/1=选项提交行/2=闸内选项步〔诊断〕）+[21..24] 效果前 4 个原子的
# AtomTypeIndex/4096（不足补 0，无效果行全 0）+[25] 原子数/16；同批 PlayLand 折叠键并来源卡身份
#（C8：放地行此前被折成 1 行、模型不能选「烧哪张」）。globals/卡特征/卡槽不变。旧 checkpoint/
# ONNX/fixture 全部作废（动作编码器输入 276→282）。
LAYOUT_VERSION = 22
# 动作截断上限 512（v2 起）：逐目标展开（出牌/发动 × 候选目标）+ 守卫/响应行。
# v3 起 C# 枚举器侧带同值守卫（超限保前 511 + 恒保末位兜底行）——本侧 pad_or_truncate_actions
# 的头截断永不会切掉 EndTurn/PassPriority（v2 理论死锁修复，契约 §6）。
MAX_ACTIONS = 512
# 动作特征 v2 6→8 维：追加 [6] effectIdentity（效果定义注册下标/1024，区分同卡多效果行）
# 与 [7] targetKind（0=无/1=卡/2=对方玩家/3=己方玩家，消除 targetIndex=-1 双义）。
# v8（2026-11-02）8→11 维：追加 [8] target2Index / [9] target3Index（多目标组合行 Targets[1]/[2]
# 的 cards_ 槽位，-1=无/玩家目标/>3 截断）与 [10] nTargets——逐原子 effCount 选 N
#（如洗回跨双墓地选 2）与 Multiple 组合行对模型可区分（旧版只编码 targets[0]，同锚组合行不可分）。
# P0（2026-11-19 无域原子目标）：[10] nTargets 钳位 0..8 → 0..16（维度不变）——牺牲/蛰伏的
# prefix-k 前缀行 k=9..16 须可分，否则「祭 9 张/11 张」特征完全相同。
# A6（2026-11-24 逐目标单选·LAYOUT_VERSION 保持 21）：**[8]/[9] 恒 -1（次目标维废止）、
# [10] 由 nTargets 改 picksRemaining 剩余待选数（含本步）**——0=无目标/提交行、1=单步行/
# 最后一步、N=纯固定计划剩余步数（≤16 钳）、**-1=计划含可变数量 spec**（牺牲/蛰伏开放步数，
# 拾取至提交行 [10]=0 收口）。组合/子集/prefix-k 行退役（每步 1 目标，TargetPickGate 累积，
# 提交按计划序拼全集=同目标集同结算）；语义变更，旧 ONNX/checkpoint/fixture 不得复用。
# v13（2026-11-08 目标选择重构步5）11→12 维：追加 [11] referenceIndex（参照角色选位镜像的
# cards_ 槽位，-1=无/玩家参照）——Reference Enumerate spec 进动作空间（活体炸弹参照选位可分）。
# v14（2026-11-15 共享规格 v0.2）12→14 维：追加 [12] targetZoneRank（首目标区域序 -1/0..9，
# 角色0→战场1→元素池2→手牌3→墓地4→牌库5→除外6→技能栏7→发动区8→其他9——C# 单源
# TargetZoneGrouping.ZoneRankOf）与 [13] targetSideRank（-1/0=己方/1=对方，枚举者视角）。
# 只编码首目标（多目标经 [8][9] 槽位+观测 zoneKind 分辨，与「只编码前 3 目标」同口径）。
# P1（2026-11-19 v0.3）14→17 维：+[14]moveCellX / [15]moveCellZ（Interaction.MoveX/Z，-1=无）
# + [16]declaredNameIndex（宣言候选序号 0..15，同效果内可比；-1=非宣言行）——移动/宣言行
# 随 InteractionParams 透传（结算期 MoveUnitHandler/ProphecyHandlerUtil 消费）。
# P2（2026-11-19）17→18 维：+[17]placementCell（永久物落格格下标 0..8；-1=非落格行）。
# v20（2026-11-20 召唤落位）18→20 维：[17] 扩义兼召唤计划行=计划首格（**类型闸修复**——
#   Activate/RespondActivate 行此前恒 -1 被 FoldEquivalentRows 折叠，战斗母巢 9 锚点行塌成 1）；
#   +[18]summonPlan（0=默认/1..9=锚点/-1=非召唤）、+[19]summonUnityHits（该计划落位后团结
#   达标数 0..6/-1，PredictUnityHits 单源——与 [5]actionCost 同层非观测泄露，拍板保留）。
# v22（2026-11-26 二期 S2a）20→26 维：+[20]pickMode（0=普通行 / 1=选项提交行〔macro，选中后
#   OpenAndAutoComplete 按共享序一次选满提交〕/ 2=闸内选项步〔step 路径的诊断标记〕）+
#   [21..24]=效果（a.Effect）前 4 个原子的 AtomTypeIndex/4096（不足补 0；无效果行全 0——
#   补盲动柞：[6]effectIdentity 是 1024 池标量，玩家自组卡组时等于随机数；原子类型对未见卡可读）、
#   [25]=原子数/16。选项行 Targets=空、[10] 语义保持（-1/FixedTotal）；[20] 进 LAE.Signature
#   （防与无目标行同键被误摘）。模式开关 reset 选项 picksMode=macro/step/both（默认 macro）。
N_ACTION_FEATURES = 26
# 全局特征 v3 49→50（重排序+归一化；对账报告 D5/D7/D14）：
#   g[15][16] 放逐计数（新增）；生命/30、回合/40、势能/30、地牌上限/14、弹药/5 归一化；
#   英雄技能块 [38..41]/[42..45]（[b+3]=角色攻击弹药）；g[47] 栈顶来源/MaxCardsTotal；
#   删 v2 g[48]（与 g[4] 重复）。
# v9（2026-11-02 牌库双通道）：50→58，尾加 g[50..57] 对方牌库开局标签——
#   [50..53] 四色张数（灰/红/蓝/绿，逐色多计 Cost[色]>0，与 CardSorter.HasCostColor 同口径）、
#   [54..56] 费用三档张数（档值=Clamp(Round(TotalCost),1,9)：0 费并入 1 档、>9 并入 9+；
#            [54]=1-2 [55]=3-6 [56]=7-9，与 DeckBuilder 费用曲线同口径）、[57] 卡组总数。
#   源=GameCore.OriginalDeckOf(opponent) 入局快照，整局恒定不随抽牌变化；原始计数不归一化。
# v19（2026-11-20 绿套补盲）：58→70，尾加 g[58..69] 剩余可用元素块（视角与 g[0]/g[1] 一致 me/opp）——
#   [58..61] 己方四色剩余（/10；=该色钱包余额+该色未横置地牌张数）、[62] 己方 Any 彩元素（/10，
#   牺牲/蛰伏入账的临时彩单独成维）、[63] 己方剩余总量（/20）、[64..67] 对方四色（/10）、
#   [68] 对方 Any（/10）、[69] 对方总量（/20）。与脚本判据（GetRemainingElementTotal 族）
#   同资源口径；观测不除钳（钱包持有 2026-11-21 起引擎侧封顶地牌上限，特征保持线性计数）。
# v20（2026-11-20 召唤落位/团结观测）70→88：+g[70..78] 己方 9 单位格占用码（0=空/1=己/2=敌，/2）、
#   +g[79..87] 各格 6 邻己方**生物**数/6（团结口径——只数活生物，与 BoardState.AdjacentOccupantCount
#   同过滤；空格邻接不可从卡特征导出，落格/召唤计划行的价值直读）。无棋盘=全 0。
# v21（2026-11-24 卡组分布与策略分界）88→97：+g[88..95] 己方牌库开局标签（源=GameCore.OriginalDeckOf(me)
#   入局快照，DeckTagsOf 与对方块 g[50..57] 同函数同口径、整局恒定、原始计数不归一化——「我是哪套」
#   显式可见，此前只有对方有）；+g[96] deckTruncated（己方牌库唯一卡 >30 种、牌库块被截断=1，否则 0）。
# v21 定稿（2026-11-24 回到一期训练目标 B0.3）97→100 → 2026-11-26 v22（二期 S2a：
# LAYOUT_VERSION 21→22、动作特征 20→26；globals 不变）：
#   +g[97] 己方手牌上限 / g[98] 对方手牌上限（RuleHooks.GetHandLimit 单源线性计数——修改链如
#   纳川仪典的额外上限直接可见）；+g[99] 己方超上限剩余待弃张数（弃牌进行中=HandLimitDiscardGate
#   快照剩余；否则己方回合 max(0, 有效手牌−上限)；对手回合恒 0——对手回合超限走入手闸改投除外区）。
#   A0 逐张弃配套：弃到几时停、要不要打牌腾手可算。
N_GLOBAL_FEATURES = 100
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
# v8 多目标（2026-11-02）：次目标槽位 + 目标数——只有目标展开四类动作
# （PlayCard/RespondPlay/Activate/RespondActivate）有效，其余类型恒 -1 / 按 targetKind 单目标计数
ACTION_F_TARGET2 = 8
ACTION_F_TARGET3 = 9
ACTION_F_NTARGETS = 10  # A6（2026-11-24 逐目标单选）：语义=nTargets→picksRemaining（见上方 A6 段注）；常量名保留（槽位不变）
# v13 参照槽位（2026-11-08 目标选择重构步5）：Reference 角色选位镜像——与 C# BuildFeatures [11] 对齐
ACTION_F_REF_INDEX = 11
# v22（2026-11-26 二期 S2a）动作语义补盲三组：
ACTION_F_PICK_MODE = 20        # [20] 0=普通行 / 1=选项提交行（macro） / 2=闸内选项步（诊断）
ACTION_F_ATOM_TYPE_FIRST = 21  # [21..24] 效果前 4 个原子的 AtomTypeIndex/4096（不足补 0；无效果=0）
ACTION_F_ATOM_COUNT = 25       # [25] 原子数/16（无效果行=0）

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

# 动作类型对照（C# TideActionType；audit_action_space / 训练统计用）。
# 2026-11-06 重排：历史空洞（墓地出牌 2 / 立约 13）移除，连续 0..12——与 C# 枚举同批。
ACTION_TYPES = {
    0: "PlayCard",
    1: "PlayLand",
    2: "Activate",
    3: "Attack",
    4: "EndTurn",
    5: "HeroSkill",
    6: "VoluntaryTrigger",
    7: "PassPriority",
    8: "RespondPlay",
    9: "RespondActivate",
    10: "Guard",
    11: "TapCreature",
    12: "Charge",
    13: "DiscardChoice",  # P3 2026-11-19 §4.1 建·A0 2026-11-24 逐张弃：手牌上限弃牌（回合末停靠决策，
    # 每步 Targets=选中 1 张——引擎有界循环连续多步弃到上限；旧「over 张组合行」口径废弃）
}
# 后续版本新增类型（v2/v3 决策点 + v11 充能；验收关注：这些行的出现率/采样率）
NEW_ACTION_TYPES = (5, 6, 7, 8, 9, 10, 11, 12, 13)
# 带目标动作（targetKind=1 时 targetIndex 语义为 cards_ 槽位）
TARGETED_ACTION_TYPES = (0, 2, 3, 8, 9, 10, 13)  # 13=DiscardChoice（A0 逐张弃：该卡槽位）

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


def action_valid_mask(actions):
    """合法动作掩码（True=合法）。动作行 [ACTION_F_VALID] 位即权威 valid 标志——
    编码器（tide_encoder）与导出图内掩码同源，均从该位自算。
    2026-11-02 起 env obs 不再单独携带 a_mask/c_mask（c_mask 从未实现、a_mask 与
    valid 位完全重复，属死代码陷阱），消费方一律改用本函数。"""
    return np.asarray(actions)[..., ACTION_F_VALID] > 0.5


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

