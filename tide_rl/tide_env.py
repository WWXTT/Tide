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
终局 ±1 归属「刚行动的一方」；非终局 λ·ΔΦ 势能塑形，λ 经 reset 协议
shapingLambda 下发——训练 Args.reward_lambda=0.005，Unity 侧缺省同值）。
Python 侧只保留超时判负。

进程生命周期：
- 惰性启动：首次 reset 才拉起 Unity batchmode，之后跨局复用（协议 reset 本身
  就是完整重开一局，进程重启一次要 30~60s，纯属浪费）；
- 启动前只清「孤儿」残留（本工程 TideHeadlessServer 无头 Unity 里父进程已死者，
  工程锁元凶）；其它活实例（父进程仍在服务）不杀——强杀会把对方训练打断成
  WinError 10054 且日志被新实例覆盖、极难排查（2026-09-30 互杀修复），
  改为直接报错退出；
- 日志/pid 按端口隔离（tide_headless_<port>.log/.pid）：并发实例互不覆盖，
  _fail 打出的日志尾部才是本实例自己的；
- 连接重试带 deadline（Unity boot 需几十秒到几分钟）；断连/超时 → 杀进程 +
  打印日志尾部 + 抛明确错误；
- 同一工程同时只允许一个 TideEnv 持有进程（一进程一局，num_envs>1 请开多个
  Unity 或改用编辑器 TCP）。
"""

import os
import re
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
        max_steps: int = 2000,  # v2：响应窗口决策点使每局步数变长（train_tide_complete 显式传参同值）
        reward_lambda: float = 0.005,  # 塑形 λ（reset 协议 shapingLambda 下发 Unity）
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
            reward_lambda: 塑形 λ，经 reset 协议 shapingLambda 下发（Unity 侧缺省 0.005）
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
        # 日志/pid 按端口隔离（2026-09-30 串台修复）：旧固定名 tide_headless.log/.pid
        # 会被并发实例互相覆盖——_fail 打出的「日志尾部」可能根本不是本实例的
        port = self._pick_free_port()
        self._log_file = Path(self.project_path) / "Logs" / f"tide_headless_{port}.log"
        self._pid_file = Path(self.project_path) / "Logs" / f"tide_headless_{port}.pid"
        self.unity_path = unity_path or self._find_unity()

        super().__init__(
            host="127.0.0.1",
            port=port,
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
        """只清「孤儿」残留（2026-09-30 互杀修复）。

        旧逻辑按命令行匹配即 taskkill，分不清孤儿与服务中的活实例——只要两次
        训练启动重叠，新启动就会把上一只还在服务的 Unity 杀掉，对方随即以
        WinError 10054 崩溃，且其错误信息引用的日志已被新实例覆盖，极难排查。
        新口径按「无头 Unity 的父进程」分类：
        - 属于本进程内其它活 TideEnv（评估/多 env 场景）→ 保护，交给 _launch 的
          _active_by_project 守卫去报错；
        - 父进程已死（上次训练崩溃留下的孤儿，工程锁元凶）→ 杀；
        - 父进程是本进程（同进程内重试的上一只，且无人认领）→ 杀；
        - 父进程是别的活进程（另一个训练正在跑）→ 不杀，报错退出。
        """
        protected = {
            e.process.pid
            for e in TideEnv._active_by_project.values()
            if e is not self and e.process is not None and e.process.poll() is None
        }
        try:
            script = (
                "Get-CimInstance Win32_Process -Filter \"Name='Unity.exe'\" | "
                "Where-Object { $_.CommandLine -like '*" + self.project_path + "*' -and "
                "$_.CommandLine -like '*TideHeadlessServer*' } | "
                "ForEach-Object { \"$($_.ProcessId)`t$($_.ParentProcessId)`t$($_.CommandLine)\" }"
            )
            out = subprocess.run(
                ["powershell", "-NoProfile", "-Command", script],
                capture_output=True, text=True, timeout=60,
                # 中文 Windows 控制台输出是 GBK：UTF-8 模式下严格解码会炸 reader 线程 → stdout 变 None
                encoding="utf-8", errors="replace",
            )
        except (OSError, subprocess.TimeoutExpired):
            return  # 查询失败不阻塞启动（工程锁/连接 deadline 报错兜底）

        live_foreign = []
        for line in (out.stdout or "").splitlines():
            parts = line.strip().split("\t", 2)
            if len(parts) != 3 or not parts[0].strip().isdigit():
                continue
            pid, ppid, cmdline = int(parts[0]), int(parts[1]), parts[2]
            if pid in protected:
                continue  # 本进程内别的 TideEnv 持有——不是残留
            if ppid != os.getpid() and self._pid_alive(ppid):
                m = re.search(r"-tidePort\s+(\d+)", cmdline)
                live_foreign.append((pid, ppid, m.group(1) if m else "?"))
                continue
            self._taskkill(pid)  # 孤儿（父进程已死）/ 本进程重试遗留 → 清掉

        if live_foreign:
            desc = "、".join(f"PID={p}（端口 {pt}，属父进程 {pp}）" for p, pp, pt in live_foreign)
            raise RuntimeError(
                f"工程 {self.project_path} 已有正在运行的训练无头实例：{desc}。\n"
                "新实例会被工程锁挡住，强杀则会打断对方训练——请等它跑完，"
                "或确认放弃后手动执行 taskkill /F /T /PID <PID> 再重试。"
            )

    @staticmethod
    def _pid_alive(pid: int) -> bool:
        """进程是否还活着（/NH 无表头 CSV：无匹配时输出本地化提示行，不以引号开头）。"""
        if pid <= 0:
            return False
        try:
            out = subprocess.run(
                ["tasklist", "/FI", f"PID eq {pid}", "/FO", "CSV", "/NH"],
                capture_output=True, text=True, timeout=30,
                # 表头/提示均为本地化文本（GBK）：errors=replace 保证解码不炸；
                # 判定只看「有没有以引号开头的 CSV 数据行」，替换字符不影响
                encoding="utf-8", errors="replace",
            ).stdout
            return any(ln.startswith('"') for ln in (out or "").splitlines())
        except (OSError, subprocess.TimeoutExpired):
            return True  # 查不动时按「活着」处理——宁可误报不误杀

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
            f"已终止 Unity 进程。日志尾部（{self._log_file}，本实例按端口隔离的独立日志）：\n{log_tail}\n"
            "常见原因：① 工程被打开中的 Unity 编辑器占用（先关编辑器）② 对局中服务端抛异常"
            "（TideHeadlessServer 异常通道已恢复日志——查上方/日志里的 [TideHeadless] 报错）"
            "③ 脚本编译错误（看上方日志）。"
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
