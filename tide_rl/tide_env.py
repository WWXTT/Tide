"""
Tide Gymnasium 环境：通过 stdio 与 Unity batchmode 桥接。

Protocol:
- reset: {"op":"reset","deck1":[...],"deck2":[...]} → obs + info
- step:  {"op":"step","action":N} → obs, reward, done, info

Obs dict 契约（对齐 ygo-agent）:
{
    "cards_": (80, 20),
    "global_": (32,),
    "actions_": (max_actions, 6),
    "h_actions_": None or (32, 14)
}
"""

import json
import subprocess
import numpy as np
import gymnasium as gym
from gymnasium import spaces
from pathlib import Path

from tide_features import (
    N_CARD_FEATURES,
    N_GLOBAL_FEATURES,
    N_ACTION_FEATURES,
    MAX_CARDS,
    MAX_ACTIONS,
    pad_or_truncate_actions,
)


class TideEnv(gym.Env):
    """
    Tide 桥接环境（stdio JSON 协议）。

    启动 Unity -batchmode -nographics -executeMethod TideHeadlessServer.Main，
    通过 stdin/stdout 收发 JSON。
    """

    metadata = {"render_modes": []}

    def __init__(
        self,
        unity_path: str = None,
        project_path: str = None,
        deck1: list = None,
        deck2: list = None,
        max_steps: int = 500,
        reward_lambda: float = 0.02,  # 塑形强度（v2 全资源势能变化更大，降低 λ）
    ):
        """
        Args:
            unity_path: Unity.exe 路径（默认从环境变量 UNITY_PATH 读取）
            project_path: Tide 工程路径（默认当前目录的父目录）
            deck1: P1 卡组 ID 列表
            deck2: P2 卡组 ID 列表
            max_steps: 单局最大步数（超时判负）
            reward_lambda: 势能塑形 λ（建议 0.01~0.05）
        """
        super().__init__()

        self.unity_path = unity_path or self._find_unity()
        self.project_path = project_path or str(Path(__file__).parent.parent.resolve())
        self.deck1 = deck1 or self._default_deck()
        self.deck2 = deck2 or self._default_deck()
        self.max_steps = max_steps
        self.reward_lambda = reward_lambda

        self.process = None
        self.step_count = 0
        self.last_potential = 0.0

        # Observation space（符合 gymnasium 规范，但实际用 dict）
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

        # Action space（离散动作索引）
        self.action_space = spaces.Discrete(MAX_ACTIONS)

    def _find_unity(self):
        """从环境变量或默认路径查找 Unity.exe。"""
        import os

        unity_path = os.environ.get("UNITY_PATH")
        if unity_path and Path(unity_path).exists():
            return unity_path

        # 默认路径（Windows）
        default = r"C:\Program Files\Unity\Hub\Editor\2022.3.44f1c1\Editor\Unity.exe"
        if Path(default).exists():
            return default

        raise FileNotFoundError(
            "Unity.exe not found. Set UNITY_PATH env var or pass unity_path arg."
        )

    def _default_deck(self):
        """默认测试卡组（占位符，实际应从 TestDecks 加载）。"""
        # TODO: 从 Assets/Configs/TestDecks/ 读取卡组配置
        return list(range(1, 41))  # 占位符：卡 ID 1~40

    def _start_process(self):
        """启动 Unity batchmode 进程。"""
        # Create logs directory
        log_dir = Path(self.project_path) / "Logs"
        log_dir.mkdir(exist_ok=True)
        log_file = log_dir / "tide_headless.log"

        cmd = [
            self.unity_path,
            "-batchmode",
            "-nographics",
            "-projectPath",
            self.project_path,
            "-executeMethod",
            "CardCore.Editor.TideHeadless.TideHeadlessServer.Main",
            "-logFile",
            str(log_file),  # Log to file, NOT stdout (keeps stdout clean for JSON)
        ]

        self.process = subprocess.Popen(
            cmd,
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            bufsize=1,
        )

    def _send_json(self, obj):
        """发送 JSON 到 Unity stdin。"""
        if self.process is None or self.process.stdin is None:
            raise RuntimeError("Process not started")
        line = json.dumps(obj, ensure_ascii=False)
        self.process.stdin.write(line + "\n")
        self.process.stdin.flush()

    def _read_json(self, debug=False):
        """从 Unity stdout 读取 JSON（跳过引擎横幅和日志行）。"""
        if self.process is None or self.process.stdout is None:
            raise RuntimeError("Process not started")

        attempts = 0
        max_attempts = 1000  # Prevent infinite loop

        while attempts < max_attempts:
            line = self.process.stdout.readline()
            attempts += 1

            if not line:
                raise EOFError("Unity process stdout closed")

            line = line.strip()
            if not line:
                continue

            if debug:
                print(f"[DEBUG] Read line {attempts}: {line[:100]}...")

            try:
                obj = json.loads(line)
                if debug:
                    print(f"[DEBUG] Successfully parsed JSON after {attempts} attempts")
                return obj
            except json.JSONDecodeError:
                # 跳过非 JSON 行（Unity 引擎横幅、日志）
                continue

        raise TimeoutError(f"No valid JSON found after {max_attempts} lines")

    def _parse_obs(self, data):
        """解析 JSON obs → numpy dict。"""
        # Debug: check data structure
        if not isinstance(data, dict):
            raise TypeError(f"Expected dict from Unity, got {type(data).__name__}: {data}")

        if "cards" not in data or "global" not in data or "actions" not in data:
            raise ValueError(f"Missing required keys in obs. Got keys: {list(data.keys())}")

        cards = np.array(data["cards"], dtype=np.float32).reshape(MAX_CARDS, N_CARD_FEATURES)
        global_ = np.array(data["global"], dtype=np.float32)
        actions = np.array(data["actions"], dtype=np.float32)  # (n, 6)
        actions = pad_or_truncate_actions(actions, MAX_ACTIONS)

        return {
            "cards_": cards,
            "global_": global_,
            "actions_": actions,
            "h_actions_": None,  # v1 不实现历史动作
            "c_mask": np.zeros(MAX_CARDS, dtype=bool),  # TODO: compute real mask
            "a_mask": np.array([i >= len(data["actions"]) for i in range(MAX_ACTIONS)], dtype=bool),
        }

    def reset(self, seed=None, options=None):
        """重置环境（开新局）。"""
        super().reset(seed=seed)

        # 关闭旧进程
        if self.process is not None:
            self.process.terminate()
            self.process.wait(timeout=5)

        # 启动新进程
        self._start_process()

        # 发送 reset
        self._send_json({"op": "reset", "deck1": self.deck1, "deck2": self.deck2})

        # 读取初始 obs
        response = self._read_json(debug=True)

        # Debug: check response structure
        if not isinstance(response, dict):
            raise TypeError(f"Expected dict response from Unity, got {type(response).__name__}: {response}")

        if "obs" not in response:
            raise ValueError(f"Missing 'obs' in Unity response. Got keys: {list(response.keys())}")

        obs = self._parse_obs(response["obs"])
        info = response.get("info", {})

        # 重置状态
        self.step_count = 0
        self.last_potential = obs["global_"][28] - obs["global_"][29]  # Φ = me - opp

        return obs, info

    def step(self, action):
        """执行动作。"""
        if self.process is None:
            raise RuntimeError("Environment not reset")

        # 发送 step
        self._send_json({"op": "step", "action": int(action)})

        # 读取响应
        response = self._read_json()
        obs = self._parse_obs(response["obs"])
        done = response["done"]
        info = response.get("info", {})

        # 计算奖励（终局 ±1 + 塑形 λ·ΔΦ）
        reward = 0.0
        if done:
            winner = info.get("winner")
            if winner == 1:
                reward = 1.0
            elif winner == 2:
                reward = -1.0
            # else: 平局或超时，reward=0
        else:
            # 塑形奖励 λ·(Φ' - Φ)
            current_potential = obs["global_"][28] - obs["global_"][29]
            shaping = self.reward_lambda * (current_potential - self.last_potential)
            reward = shaping
            self.last_potential = current_potential

        self.step_count += 1

        # 超时判负
        if self.step_count >= self.max_steps and not done:
            done = True
            reward = -1.0
            info["timeout"] = True

        truncated = False  # Gymnasium 新协议：done=终局，truncated=超时

        return obs, reward, done, truncated, info

    def close(self):
        """关闭环境。"""
        if self.process is not None:
            self.process.terminate()
            self.process.wait(timeout=5)
            self.process = None

    def __del__(self):
        self.close()


def make_tide_env(**kwargs):
    """工厂函数（兼容 cleanba 风格）。"""
    return TideEnv(**kwargs)
