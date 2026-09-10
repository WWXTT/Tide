"""
Tide Gymnasium 环境：自动拉起 Unity batchmode，经 TCP 桥接（TideEnvTcp 协议）。

为什么是 TCP 而不是 stdio：实测（Unity 6000.5.8f1 -batchmode -logFile）Unity 会把
stdout 并入日志文件，管道侧收不到任何响应 → stdio 协议不可用。batchmode 入口
TideHeadlessServer.Main 起 TcpListener，端口经 -tidePort <n> 传入（本模块随机选）。

Protocol（每行一条 JSON，与编辑器 TCP 路径完全一致）:
- reset: {"op":"reset"} → obs + info（卡组与先后手由 Unity 侧随机化：
        标准池抽 30 张 + 随机换座，见 TideHeadlessServer.HandleReset）
- step:  {"op":"step","action":N} → obs, reward, done, info

reward 直接采用 Unity 响应的 reward 字段（actor-centric 权威口径：
终局 ±1 归属「刚行动的一方」；非终局 λ·ΔΦ 势能塑形，λ 固定在 Unity 侧
TideHeadlessDriver.ShapingLambda=0.05）。Python 侧只保留超时判负。

进程生命周期：
- 惰性启动：首次 reset 才拉起 Unity batchmode，之后跨局复用（协议 reset 本身
  就是完整重开一局，进程重启一次要 30~60s，纯属浪费）；
- 启动前清理上次残留：pidfile 记录 + 命令行匹配（本工程 + TideHeadlessServer
  的孤儿 Unity 进程 taskkill），避免「二次运行卡死在工程锁」；
- 连接重试带 deadline（Unity boot 需几十秒到几分钟）；断连/超时 → 杀进程 +
  打印日志尾部 + 抛明确错误；
- 同一工程同时只允许一个 TideEnv 持有进程（一进程一局，num_envs>1 请开多个
  Unity 或改用编辑器 TCP）。
"""

import os
import socket
import subprocess
import time
from pathlib import Path

from tide_env_tcp import TideEnvTcp


class TideEnv(TideEnvTcp):
    """Tide 桥接环境：Unity batchmode 常驻进程 + TCP 协议（继承 TideEnvTcp）。"""

    # 同工程活跃实例登记（project_path -> TideEnv）：一进程一局，防多实例抢工程锁
    _active_by_project: dict = {}

    def __init__(
        self,
        unity_path: str = None,
        project_path: str = None,
        max_steps: int = 1000,
        reward_lambda: float = 0.02,  # 兼容参数：塑形 λ 固定在 Unity 侧（见模块 docstring）
        opponent: str = "selfplay",   # 对手位：selfplay 自对弈 / simpleai 模型 vs SimpleAI
        startup_timeout: float = 300.0,
        step_timeout: float = 120.0,
        connect_retry_interval: float = 2.0,
    ):
        """
        Args:
            unity_path: Unity.exe 路径（默认自动发现：UNITY_PATH 环境变量 →
                        Hub 目录扫描并匹配 ProjectVersion.txt）
            project_path: Tide 工程路径（默认当前目录的父目录）
            max_steps: 单局最大步数（超时判负）
            reward_lambda: 兼容参数（Unity 侧已内置 λ=0.05，此处不生效）
            opponent: 对手位——"selfplay" 自对弈 / "simpleai" 模型 vs SimpleAI
                     （模型座次随机 = 先后手各半，obs/reward 恒为模型视角，info.modelSeat 判胜负）
            startup_timeout: Unity batchmode 启动 + 建立连接的总超时
            step_timeout: 单步 socket 读超时
        """
        # 先落本类属性再做可能抛异常的发现逻辑（__del__/close 依赖 self.process 存在）
        self.process = None
        self.startup_timeout = startup_timeout
        self.step_timeout = step_timeout
        self.connect_retry_interval = connect_retry_interval
        self.project_path = str(project_path or Path(__file__).parent.parent.resolve())
        self._log_file = Path(self.project_path) / "Logs" / "tide_headless.log"
        self._pid_file = Path(self.project_path) / "Logs" / "tide_headless.pid"
        self.unity_path = unity_path or self._find_unity()

        super().__init__(
            host="127.0.0.1",
            port=self._pick_free_port(),
            max_steps=max_steps,
            reward_lambda=reward_lambda,
            opponent=opponent,
        )

    # ===================================================== Unity 发现 =====================================================

    def _find_unity(self):
        """自动发现 Unity.exe：UNITY_PATH 环境变量 → Hub 目录扫描（优先匹配工程版本）。"""
        env_path = os.environ.get("UNITY_PATH")
        if env_path and Path(env_path).exists():
            return env_path

        hub = Path(os.environ.get("UNITY_HUB_PATH") or r"C:\Program Files\Unity\Hub\Editor")
        candidates = []
        if hub.is_dir():
            for d in hub.iterdir():
                exe = d / "Editor" / "Unity.exe"
                if exe.is_file():
                    candidates.append(exe)

        if candidates:
            want = self._project_version()
            if want:
                for exe in candidates:
                    if exe.parent.parent.name == want:
                        return str(exe)
            # 工程版本读不到时取版本号最大的（目录名排序）
            return str(sorted(candidates, key=lambda e: e.parent.parent.name)[-1])

        found = "\n".join(f"  - {p}" for p in candidates) or "  （无）"
        raise FileNotFoundError(
            "Unity.exe not found. 请设置 UNITY_PATH 环境变量，或安装 Unity Hub 到默认目录。\n"
            f"工程版本: {self._project_version() or '?'}\n"
            f"Hub 候选:\n{found}"
        )

    def _project_version(self):
        """读 ProjectSettings/ProjectVersion.txt 的 m_EditorVersion（如 6000.5.8f1）。"""
        f = Path(self.project_path) / "ProjectSettings" / "ProjectVersion.txt"
        if not f.is_file():
            return None
        try:
            for line in f.read_text(encoding="utf-8", errors="replace").splitlines():
                if line.startswith("m_EditorVersion:"):
                    return line.split(":", 1)[1].strip()
        except OSError:
            pass
        return None

    @staticmethod
    def _pick_free_port() -> int:
        """随机选一个空闲 TCP 端口（传给 Unity -tidePort；极小概率被抢占，连接重试兜底）。"""
        s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        try:
            s.bind(("127.0.0.1", 0))
            return s.getsockname()[1]
        finally:
            s.close()

    # ===================================================== 连接（重写基类） =====================================================

    def _connect(self):
        """基类 override：先确保 Unity 进程在跑，再带 deadline 重试连接。"""
        if self.sock is not None:
            return  # Already connected

        if self.process is None or self.process.poll() is not None:
            self._start_process()

        deadline = time.time() + self.startup_timeout
        last_err = None
        while True:
            try:
                self.sock = socket.create_connection((self.host, self.port), timeout=10)
                self.sock.settimeout(self.step_timeout)
                self.reader = self.sock.makefile("r", encoding="utf-8")
                self.writer = self.sock.makefile("w", encoding="utf-8")
                print(f"[TideEnv] 已连接 Unity batchmode TCP 桥接 @ {self.host}:{self.port}")
                return
            except OSError as e:
                last_err = e
                if self.process.poll() is not None:
                    self._fail(f"Unity 进程已退出（启动失败）")
                if time.time() >= deadline:
                    self._fail(f"连接 Unity TCP 桥接超时（{self.startup_timeout:.0f}s，最后错误: {last_err}）")
                time.sleep(self.connect_retry_interval)

    def _read_json(self):
        """基类 override：读失败（超时/EOF）统一走 _fail（杀进程 + 日志尾部）。"""
        try:
            return super()._read_json()
        except (EOFError, socket.timeout, OSError) as e:
            self._fail(f"读取 Unity 响应失败: {e}")

    # ===================================================== 进程管理 =====================================================

    def _start_process(self):
        """清理残留 → 启动 Unity batchmode（TCP 桥接模式）。"""
        self._kill_stale_processes()

        other = TideEnv._active_by_project.get(self.project_path)
        if other is not None and other is not self and other.process is not None and other.process.poll() is None:
            raise RuntimeError(
                f"工程 {self.project_path} 已有另一个 TideEnv 持有 batchmode 进程"
                f"（一个进程一次只跑一场对局）。请用 num_envs=1，评估复用同一 env 实例。"
            )
        TideEnv._active_by_project[self.project_path] = self

        self._log_file.parent.mkdir(exist_ok=True)
        cmd = [
            self.unity_path,
            "-batchmode",
            "-nographics",
            "-projectPath",
            self.project_path,
            "-executeMethod",
            "CardCore.Editor.TideHeadless.TideHeadlessServer.Main",
            "-logFile",
            str(self._log_file),
            "-tidePort",
            str(self.port),
        ]

        try:
            self.process = subprocess.Popen(
                cmd,
                stdin=subprocess.DEVNULL,
                stdout=subprocess.DEVNULL,
                stderr=subprocess.DEVNULL,  # 协议走 TCP，std 流全部弃置（日志在 -logFile）
                cwd=self.project_path,
            )
        except FileNotFoundError as e:
            raise RuntimeError(f"Unity.exe 启动失败（路径 {self.unity_path}）: {e}") from e

        try:
            self._pid_file.write_text(str(self.process.pid), encoding="ascii")
        except OSError:
            pass

    def _kill_stale_processes(self):
        """杀掉上次运行残留的 Unity batchmode 进程（工程锁的元凶）。"""
        # 1) pidfile 记录的上一个实例
        if self._pid_file.is_file():
            try:
                pid = int(self._pid_file.read_text(encoding="ascii").strip())
                if self._is_unity_pid(pid):
                    self._taskkill(pid)
            except (ValueError, OSError):
                pass
            try:
                self._pid_file.unlink()
            except OSError:
                pass

        # 2) 命令行匹配的孤儿（本工程 + TideHeadlessServer；编辑器实例不含 executeMethod，不会被误杀）
        try:
            script = (
                "Get-CimInstance Win32_Process -Filter \"Name='Unity.exe'\" | "
                "Where-Object { $_.CommandLine -like '*" + self.project_path + "*' -and "
                "$_.CommandLine -like '*TideHeadlessServer*' } | "
                "Select-Object -ExpandProperty ProcessId"
            )
            out = subprocess.run(
                ["powershell", "-NoProfile", "-Command", script],
                capture_output=True, text=True, timeout=60,
            )
            for line in out.stdout.split():
                if line.strip().isdigit():
                    self._taskkill(int(line))
        except (OSError, subprocess.TimeoutExpired):
            pass  # 查询失败不阻塞启动（还有连接 deadline 兜底）

    @staticmethod
    def _is_unity_pid(pid: int) -> bool:
        try:
            out = subprocess.run(
                ["tasklist", "/FI", f"PID eq {pid}", "/FO", "CSV"],
                capture_output=True, text=True, timeout=30,
            ).stdout
            return "Unity.exe" in out
        except (OSError, subprocess.TimeoutExpired):
            return False

    @staticmethod
    def _taskkill(pid: int):
        try:
            subprocess.run(["taskkill", "/F", "/T", "/PID", str(pid)],
                           capture_output=True, timeout=30)
            print(f"[TideEnv] 已清理残留 Unity 进程 PID={pid}")
        except (OSError, subprocess.TimeoutExpired):
            pass

    # ===================================================== 错误收口 =====================================================

    def _fail(self, reason: str):
        """致命错误收口：杀进程 + 日志尾部 + 可操作的提示。"""
        log_tail = self._log_tail()
        if self.process is not None and self.process.poll() is None:
            try:
                self.process.kill()
            except OSError:
                pass
        raise RuntimeError(
            f"[TideEnv] {reason}。\n"
            f"已终止 Unity 进程。日志尾部（{self._log_file}）：\n{log_tail}\n"
            "常见原因：① 工程被打开中的 Unity 编辑器占用（先关编辑器）② 上次训练的 "
            "batchmode 进程残留（已自动清理再试）③ 脚本编译错误（看上方日志）。"
        )

    def _log_tail(self, n: int = 30) -> str:
        try:
            lines = self._log_file.read_text(encoding="utf-8", errors="replace").splitlines()
            return "\n".join(lines[-n:])
        except OSError:
            return f"（无法读取 {self._log_file}）"

    # ===================================================== 清理 =====================================================

    def close(self):
        """关闭环境（断开 TCP、杀 Unity 进程、清登记、删 pidfile）。"""
        super().close()  # 断开 socket
        if self.process is not None:
            try:
                self.process.terminate()
                self.process.wait(timeout=10)
            except (OSError, subprocess.TimeoutExpired):
                try:
                    self.process.kill()
                except OSError:
                    pass
            self.process = None
        if TideEnv._active_by_project.get(self.project_path) is self:
            del TideEnv._active_by_project[self.project_path]
        try:
            self._pid_file.unlink(missing_ok=True)
        except OSError:
            pass


def make_tide_env(**kwargs):
    """工厂函数（兼容 cleanba 风格）。"""
    return TideEnv(**kwargs)
