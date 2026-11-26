"""
Tide Gymnasium 环境：通过 TCP socket 与 Unity 编辑器桥接。

优点：
- Unity 只启动一次（手动打开编辑器）
- 实时日志在 Unity Console
- 调试方便
- 连接快速

使用流程：
1. 打开 Unity 编辑器，加载 Tide 项目
2. 菜单：Tools > AI > 启动训练服务器 (TCP 9999)
3. 运行 Python 训练脚本
"""

import json
import random
import socket
import numpy as np
import gymnasium as gym
from gymnasium import spaces

from tide_features import (
    N_CARD_FEATURES,
    N_GLOBAL_FEATURES,
    N_ACTION_FEATURES,
    MAX_CARDS,
    MAX_ACTIONS,
    LAYOUT_VERSION,
    N_CARD_POOL,
    N_EFFECT_TYPES,
    pad_or_truncate_actions,
)

# v3 布局握手期望值（契约 docs/action_space_v3_contract.md 第 7 节）：
# Unity reset 回显 layoutVersion/dims，与此处逐值断言——布局漂移在这里响亮失败，
# 而不是等到 obs reshape 时炸出一个难查的形状错误（2026-09-14 N_GLOBAL 事故教训）。
# P-1（2026-11-19 稳定下标）：池尺寸入握手——C# 派生池与本侧 embedding 表同代对账。
EXPECTED_DIMS = {
    "nCard": N_CARD_FEATURES,
    "nGlobal": N_GLOBAL_FEATURES,
    "nAction": N_ACTION_FEATURES,
    "maxActions": MAX_ACTIONS,
    "maxCards": MAX_CARDS,  # v3 新增入握手（卡槽总数；旧 Unity 回显缺该键也判不匹配）
    "cardPoolSize": N_CARD_POOL,
    "typePoolSize": N_EFFECT_TYPES,
}


def _assert_layout(info):
    """断言 Unity 侧回显的布局与本侧常量一致。不匹配立即抛错并指出两侧实际值。"""
    version = info.get("layoutVersion")
    if version != LAYOUT_VERSION:
        raise RuntimeError(
            f"布局版本不匹配：Unity layoutVersion={version}，Python LAYOUT_VERSION={LAYOUT_VERSION}。"
            f"两侧代码不同代——先对齐 docs/action_space_v3_contract.md 再跑。"
        )
    dims = info.get("dims") or {}
    for key, expected in EXPECTED_DIMS.items():
        actual = dims.get(key)
        if actual != expected:
            raise RuntimeError(
                f"维度不匹配 {key}: Unity={actual}，Python={expected}（layoutVersion={version}）。"
                f"检查 TideObservation.cs / tide_features.py 与契约的同步。"
            )


class TideEnvTcp(gym.Env):
    """
    Tide TCP 桥接环境（开发/调试推荐）。

    连接到 Unity 编辑器的 TCP 服务器（端口 9999）。
    """

    metadata = {"render_modes": []}

    def __init__(
        self,
        host: str = "localhost",
        port: int = 9999,
        max_steps: int = 2000,
        reward_lambda: float = 0.02,
        opponent: str = "selfplay",
        model_theme: str = "",
        model_doped: str = "",
        picks_mode: str = "macro",
        handoff_state: str = "none",
        max_opening_turns: int = 0,
        chain_lambda: float = 0.0,
    ):
        """
        Args:
            host: Unity TCP 服务器地址
            port: Unity TCP 服务器端口（默认 9999）
            max_steps: 单局最大步数
            reward_lambda: 兼容参数（塑形 λ 固定在 Unity 侧 TideHeadlessDriver.ShapingLambda）
            opponent: 对手位——"selfplay" 主题自对弈（主题整组 vs Cards 随机 30，双方模型驱动）/
                      "simpleai" 模型 vs 脚本主题卡组（SimpleAI+AutoMatch，对手回合 Unity 侧自动打，
                      obs/reward 恒为模型视角，info.theme=对手主题 key 供分主题统计）
            model_theme: vs 脚本口径下模型侧卡组固定该主题整组（red/green/blue，2026-10-30
                         红·快攻镜像训练口径；空=Cards 随机 30 保持旧行为。Unity 侧找不到主题
                         LogWarning 回落随机，info.modelTheme 回显实际命中 key 供断言）
            model_doped: 掺卡口径（2026-11-17 阶段二，AI训练说明 §0.1）：命中主题的模型侧/
                         指定侧卡组 = 整组每种 −1 张 + 训练池随机补足（红 30→25+5）；优先级高于
                         model_theme；空 = 旧行为零差。info.modelDoped 回显实际命中 key 供断言
            picks_mode: 目标拾取行模式（v22 二期 S2a，reset 可按 "picksMode" 覆盖）：
                         "macro"=多步计划只产选项提交行（默认；[20]=1，选中后 OpenAndAutoComplete
                         按共享序一次选满提交）/ "step"=A6 逐步行（首步行+闸内拾取，现状口径）/
                         "both"=并存（仅诊断，摊薄 logits 不进训练）。info.picksMode 回显供断言
        """
        super().__init__()

        self.host = host
        self.port = port
        self.max_steps = max_steps
        self.reward_lambda = reward_lambda
        self.opponent = opponent
        self.model_theme = model_theme
        self.model_doped = model_doped
        self.picks_mode = picks_mode  # v22 二期 S2a：macro/step/both（reset 可按次覆盖）
        # S3-1/S3-3（2026-11-26 二期 §5）：开手交棒与链进度势能（每局 reset 显式下发）
        self.handoff_state = handoff_state      # none/t3_lands/first_sac_done/genel_ready
        self.max_opening_turns = int(max_opening_turns)  # 交棒兜底回合（0=不兜底）
        self.chain_lambda = float(chain_lambda)  # Φ_chain 奖励系数（0=关；计划初值 0.02）
        self._last_model_theme = ""  # 最近一局实际下发的模型卡组主题（"all" 随机后的具体 key）

        self.sock = None
        self.reader = None
        self.writer = None
        self.step_count = 0
        # 最近一次 obs 的原始合法动作数（服务端上报、未截断）——audit_action_space 检测
        # MAX_ACTIONS 截断事件用；>MAX_ACTIONS 即发生截断（模型看不到尾部动作行）
        self.last_n_actions = 0

        # Observation space
        self.observation_space = spaces.Dict(
            {
                "cards_": spaces.Box(
                    low=0, high=999, shape=(MAX_CARDS, N_CARD_FEATURES), dtype=np.float32
                ),
                "global_": spaces.Box(
                    low=0, high=999, shape=(N_GLOBAL_FEATURES,), dtype=np.float32
                ),
                "actions_": spaces.Box(
                    low=0, high=999, shape=(MAX_ACTIONS, N_ACTION_FEATURES), dtype=np.float32
                ),
            }
        )

        # Action space
        self.action_space = spaces.Discrete(MAX_ACTIONS)

    def _connect(self):
        """连接到 Unity TCP 服务器。"""
        if self.sock is not None:
            return  # Already connected

        try:
            self.sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            self.sock.connect((self.host, self.port))
            self.reader = self.sock.makefile('r', encoding='utf-8')
            self.writer = self.sock.makefile('w', encoding='utf-8')
            print(f"[TideEnvTcp] 已连接到 Unity @ {self.host}:{self.port}")
        except ConnectionRefusedError:
            raise RuntimeError(
                f"无法连接到 Unity TCP 服务器 @ {self.host}:{self.port}\n"
                f"请先在 Unity 编辑器中点击菜单：Tools > AI > 启动训练服务器 (TCP 9999)"
            )

    def _send_json(self, obj):
        """发送 JSON 到 Unity。"""
        if self.writer is None:
            raise RuntimeError("Not connected")
        line = json.dumps(obj, ensure_ascii=False)
        self.writer.write(line + "\n")
        self.writer.flush()

    def _read_json(self):
        """从 Unity 读取 JSON。"""
        if self.reader is None:
            raise RuntimeError("Not connected")
        line = self.reader.readline()
        if not line:
            raise EOFError("Unity 连接已关闭")
        return json.loads(line.strip())

    def _parse_obs(self, data):
        """解析 JSON obs → numpy dict。"""
        if not isinstance(data, dict):
            raise TypeError(f"Expected dict from Unity, got {type(data).__name__}")

        if "cards" not in data or "globals" not in data or "actions" not in data:
            raise ValueError(f"Missing required keys in obs. Got keys: {list(data.keys())}")

        cards = np.array(data["cards"], dtype=np.float32).reshape(MAX_CARDS, N_CARD_FEATURES)
        global_ = np.array(data["globals"], dtype=np.float32)
        if global_.shape[0] != N_GLOBAL_FEATURES:
            raise ValueError(
                f"global_ 维度 {global_.shape[0]} != {N_GLOBAL_FEATURES}（C# NGlobal 不同步？）"
            )
        actions = np.array(data["actions"], dtype=np.float32)  # (n, N_ACTION_FEATURES)

        # Pad/truncate actions
        n_actions = data.get("nActions", len(data["actions"]) // N_ACTION_FEATURES)
        self.last_n_actions = int(n_actions)
        actions = actions.reshape(-1, N_ACTION_FEATURES)[:n_actions]
        actions = pad_or_truncate_actions(actions, MAX_ACTIONS)

        # 不携带任何掩码字段（2026-11-02 清理）：动作行 [0] 位即 valid 标志，
        # 编码器/导出图/消费脚本一律经 tide_features.action_valid_mask 自算
        # （旧 c_mask 从未实现恒全零、旧 a_mask 与 valid 位完全重复——都是死代码陷阱）
        return {
            "cards_": cards,
            "global_": global_,
            "actions_": actions,
            "h_actions_": None,
        }

    def reset(self, seed=None, options=None):
        """重置环境（开新局）。options={"opponent": ...} 可按次覆盖对手位
        （自对弈训练中途切 vs SimpleAI 评估用），缺省用 self.opponent；
        options={"theme": "red"} 指定 vs 脚本口径的对手主题（2026-09-22 临时：
        评估基准暂只认红，空/缺省=三主题随机，Unity 侧找不到回落随机）；
        options={"modelTheme": "red"} 按次覆盖模型侧卡组主题（2026-10-30 红·快攻镜像
        口径；空/缺省用 self.model_theme——仅 vs 脚本口径生效）。
        options={"modelSeat": 0|1} 钉模型座次（评估协议 v2，2026-11-22 配对终版：座位 24/24
        均分不再靠掷币凑）；options={"seed": N>0} 钉对局种子（DeckRng 抽样 + 引擎双随机流
        同种子重播——同种子同座次同卡组 = 逐局精确重播，配对评估前提）。两者服务端在
        info.modelSeat / info.seed 回显，本方法断言一致——对端 Unity 过旧静默忽略新字段时
        响亮失败（假配对比报错更糟）。
        塑形 λ 经 self.reward_lambda 每局显式下发（2026-11-02 修复：旧口径仅 λ>0 才发
        shapingLambda 字段，而 Unity 侧「缺省/0 用驱动默认 0.005」——λ=0 根本关不掉塑形。
        现在恒发 shapingSet=1 + shapingLambda（含 0），Unity 侧 0 = 关闭塑形只留终局 ±1；
        两侧代码须同版本——旧 Unity 不认识 shapingSet 会把 0 当缺省回落 0.005）。"""
        super().reset(seed=seed)

        # 连接（如果未连接）
        self._connect()

        # 发送 reset（opponent 决定对手位；shapingLambda 按局显式覆盖塑形 λ——0=关闭；
        # theme 钉死对手主题；modelTheme 钉死模型侧卡组主题——红镜像等固定口径训练）
        opponent = (options or {}).get("opponent") or self.opponent
        payload = {"op": "reset", "opponent": opponent}
        theme = (options or {}).get("theme")
        if theme:
            payload["theme"] = theme
        model_theme = (options or {}).get("modelTheme") or self.model_theme
        # "all"（2026-11-17 一期课程·三套预组都要学）：每局从三预组随机抽一套作模型卡组，
        # 显式下发具体 key（不把 "all" 发到 Unity —— 那边只认 red/green/blue，未知 key 回落随机 30）。
        if model_theme == "all":
            model_theme = random.choice(("red", "green", "blue"))
        if model_theme:
            payload["modelTheme"] = model_theme
        self._last_model_theme = model_theme or ""
        model_doped = (options or {}).get("modelDoped") or self.model_doped
        if model_doped:
            payload["modelDoped"] = model_doped
        # 评估协议 v2（2026-11-22）：modelSeat 钉座次（字符串下发——服务端 JsonUtility 缺省
        # int=0 会把老客户端误钉 P1，字符串缺省 null=掷币）；seed 钉对局种子（>0）
        seat_req = (options or {}).get("modelSeat")
        if seat_req is not None:
            payload["modelSeat"] = str(int(seat_req))
        seed_req = (options or {}).get("seed")
        if seed_req:
            payload["seed"] = int(seed_req)
        payload["shapingLambda"] = float(self.reward_lambda)
        payload["shapingSet"] = 1
        # v22 二期 S2a：目标拾取行模式（macro=默认/step=A6 现状/both=诊断）——非法值在此响亮失败
        # （打到 Unity 才回落 = 整个 run 白跑错口径）
        picks = str((options or {}).get("picksMode") or self.picks_mode or "macro")
        if picks not in ("macro", "step", "both"):
            raise ValueError(f"[TideEnv] picksMode 非法 {picks!r}（可选 macro/step/both）")
        payload["picksMode"] = picks
        # S3-3（2026-11-26 二期 §5）：链进度势能奖励系数——恒显式下发（chainSet=1），0=关奖励
        #（C# 闩位计数照常，info.chainBits 免费仪表）。缺省 0 = 旧行为；训练侧 Args 显式传 0.02。
        payload["chainLambda"] = float((options or {}).get("chainLambda", self.chain_lambda))
        payload["chainSet"] = 1
        # S3-1（2026-11-26 二期 §5）：开手交棒档位（none/t3_lands/first_sac_done/genel_ready；
        # none=关闭零行为）+ maxOpeningTurns 兜底（0=不兜底）。仅 vs 脚本口径消费。
        payload["handoff"] = str((options or {}).get("handoff") or self.handoff_state or "none")
        payload["maxOpeningTurns"] = int(self.max_opening_turns)
        self._send_json(payload)

        # 读取初始 obs
        response = self._read_json()

        if not isinstance(response, dict):
            raise TypeError(f"Expected dict response, got {type(response).__name__}: {response}")

        if "obs" not in response:
            raise ValueError(f"Missing 'obs' in response. Keys: {list(response.keys())}")

        obs = self._parse_obs(response["obs"])
        info = response.get("info", {})
        # v2 布局握手：Unity 回显 layoutVersion/dims，与本侧常量逐值断言（响亮失败替代 reshape 炸）
        _assert_layout(info)

        # 评估协议 v2 断言（2026-11-22）：座次/种子回显必须与请求一致——不一致 = 对端是
        # 未升级的 Unity（静默忽略新字段），配对地基不成立，报清晰错误而非出假配对数据
        if seat_req is not None and int(info.get("modelSeat", -1)) != int(seat_req):
            raise RuntimeError(
                f"[TideEnv] modelSeat 请求 {seat_req} 未生效（info.modelSeat="
                f"{info.get('modelSeat')}）——Unity 侧 TideHeadlessServer 过旧不认该字段，先同步双仓 C#")
        if seed_req and int(info.get("seed", 0)) != int(seed_req):
            raise RuntimeError(
                f"[TideEnv] seed 请求 {seed_req} 未生效（info.seed={info.get('seed')}）——"
                f"Unity 侧 TideHeadlessServer 过旧不认该字段，先同步双仓 C#")
        # v22 二期 S2a：picksMode 回显断言——未回显/不一致 = 对端 Unity 过旧（v21），报清晰错误
        if str(info.get("picksMode", "") or "") != picks:
            raise RuntimeError(
                f"[TideEnv] picksMode={picks!r} 未生效（info.picksMode={info.get('picksMode')!r}）——"
                f"Unity 侧还是 v21（不认该字段），先同步 C# 再跑 v22 口径")

        # 重置状态
        self.step_count = 0

        return obs, info

    def step(self, action):
        """执行动作。"""
        if self.sock is None:
            raise RuntimeError("Environment not connected. Call reset() first.")

        # 发送 step
        self._send_json({"op": "step", "action": int(action)})

        # 读取响应
        response = self._read_json()
        obs = self._parse_obs(response["obs"])
        done = response["done"]
        info = response.get("info", {})

        # reward 采用 Unity 权威值（actor-centric）：终局 ±1 归属「刚行动的一方」，
        # 非终局 λ·ΔΦ 势能塑形（λ 由 reset 协议下发，默认 0.005——2026-09-22 前固定 0.05
        # 且 Φ 含手牌，塑形累计曾淹没终局信号；2026-11-23 塑形口径 v2 起 Φ 恒含手牌
        #（includeHand:true），拖局抑制由 λ=0.005 承担，旧「剔除手牌」口径已废弃）。
        # 注意不能按 info.winner 的座次判符号——obs 视角随行动方轮换，座次判会反号。
        # info.toPlay = 下一决策点行动方座次（0/1，终局 -1）——自对弈 GAE 座次修正用。
        reward = float(response.get("reward", 0.0))

        # 平局（SimpleAI 崩溃等 winner 为空）：驱动侧 ReferenceEquals(null, me)=false 发了
        # -1，此处归零——平局按 0 回报计，不与超时/判负混在同一信号里（2026-09-29）
        if done and not str(info.get("winner", "") or ""):
            reward = 0.0

        self.step_count += 1

        # 超时判负
        if self.step_count >= self.max_steps and not done:
            done = True
            reward = -1.0
            info["timeout"] = True

        truncated = False

        return obs, reward, done, truncated, info

    def close(self):
        """关闭连接。"""
        if self.sock is not None:
            self.sock.close()
            self.sock = None
            self.reader = None
            self.writer = None

    def __del__(self):
        self.close()


def make_tide_env_tcp(**kwargs):
    """工厂函数（TCP 版本）。"""
    return TideEnvTcp(**kwargs)
