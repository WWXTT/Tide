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
    pad_or_truncate_actions,
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
        max_steps: int = 500,
        reward_lambda: float = 0.02,
    ):
        """
        Args:
            host: Unity TCP 服务器地址
            port: Unity TCP 服务器端口（默认 9999）
            max_steps: 单局最大步数
            reward_lambda: 势能塑形 λ
        """
        super().__init__()

        self.host = host
        self.port = port
        self.max_steps = max_steps
        self.reward_lambda = reward_lambda

        self.sock = None
        self.reader = None
        self.writer = None
        self.step_count = 0
        self.last_potential = 0.0

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
        actions = np.array(data["actions"], dtype=np.float32)  # (n, 6)

        # Pad/truncate actions
        n_actions = data.get("nActions", len(data["actions"]) // N_ACTION_FEATURES)
        actions = actions.reshape(-1, N_ACTION_FEATURES)[:n_actions]
        actions = pad_or_truncate_actions(actions, MAX_ACTIONS)

        # Compute masks
        c_mask = np.zeros(MAX_CARDS, dtype=bool)  # TODO: real card mask
        a_mask = np.array([i >= n_actions for i in range(MAX_ACTIONS)], dtype=bool)

        return {
            "cards_": cards,
            "global_": global_,
            "actions_": actions,
            "h_actions_": None,
            "c_mask": c_mask,
            "a_mask": a_mask,
        }

    def reset(self, seed=None, options=None):
        """重置环境（开新局）。"""
        super().reset(seed=seed)

        # 连接（如果未连接）
        self._connect()

        # 发送 reset
        self._send_json({"op": "reset"})

        # 读取初始 obs
        response = self._read_json()

        if not isinstance(response, dict):
            raise TypeError(f"Expected dict response, got {type(response).__name__}: {response}")

        if "obs" not in response:
            raise ValueError(f"Missing 'obs' in response. Keys: {list(response.keys())}")

        obs = self._parse_obs(response["obs"])
        info = response.get("info", {})

        # 重置状态
        self.step_count = 0
        self.last_potential = obs["global_"][28] - obs["global_"][29]  # Φ = me - opp

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

        # 计算奖励
        reward = 0.0
        if done:
            winner = info.get("winner", "")
            if winner == "0":  # P1 (我方)
                reward = 1.0
            elif winner == "1":  # P2 (对方)
                reward = -1.0
            # else: 平局/超时，reward=0
        else:
            # 塑形奖励
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
