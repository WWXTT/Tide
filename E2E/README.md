# Tide 模拟器 E2E（MuMu + adb）

用 adb 驱动 MuMu 模拟器里的 Tide：边操作边看日志，按固定检查项出验收报告。
相当于安卓包的端到端冒烟——编辑器内 Play 冒烟不替代本链路（打包收集、IL2CPP、HybridCLR、
离线资源寻址这类问题只有真包环境暴露）。

## 跑法

```bash
bash E2E/run.sh
```

- 需要 MuMu 模拟器（127.0.0.1:16384，实例 0）；没开会自动拉起并等开机
- 设备上没装/装旧了 Tide 时自动从 `Builds/Android/` 装最新现成包（sha256 对拍，旧了自动重装）
- **出包仍须用户确认（2026-10-18 定案）：本脚本不打包，只装现成 APK**
- 报告：`E2E/reports/<时间戳>/report.md`（含冷启/驻留/热启/交互段完整 logcat 与 W 消息清单；
  报告头带 APK sha256、git HEAD/dirty、设备分辨率/DPI）
- 退出码 0=全过；1=有 FAIL 或 INVALID；2=环境故障。跑前会重启模拟器里的游戏，
  **跑的过程中不要并行 adb 操作/手动点模拟器**（会被污染检测判 INVALID）

## 阶段结构

- 阶段 0 环境（设备/包版本/APK sha/git 态/画布缩放）
- 阶段 1 冷启动（A）→ 阶段 2 菜单驻留 60s（B，段首污染检测）
- 阶段 3 热重启（C）→ 阶段 4 BACK 键观察（D）
- 阶段 4.5 交互流（F）：F0 注入三课全胜存档+冷启主菜单 → F1 卡组编辑器进出 →
  F2 木桩开局 → 段末污染检测（污染=全段 INVALID，不判 PASS/FAIL）
- 阶段 5 全局健康（G，E/W 台账合并冷+热+交互段）

## 结构

- `CHECKLIST.md` —— 固定检查项清单（人读版验收对照表 + 已知噪音台账）
- `run.sh` —— 全量跑入口（阶段 0 环境 / 1 冷启动 / 2 驻留 / 3 热重启 / 4 退出观察 / 4.5 交互流 F / 5 全局健康）
- `common.sh` —— adb 发现、MuMu 拉起、日志捕获、Unity 消息行抽取、断言与报告引擎、
  基线台账常量；F 段断言原语（`expect_screen`/`expect_click`/`tap_expect`·行基线防旧日志误命中、
  失败落 dumpsys window+截图存档）、污染检测（`scan_pollution`）、meminfo 新口径（`read_pss`）
- `coords.sh` —— 坐标层：设计 1920×1080 → 设备像素换算（wm size 短边/1080）+ 坐标登记表
  （来源=预制体 execute_code 布局直调读数 / 代码布局常量推算，禁止截图猜坐标）
- `assets/TutorialProgress.json` —— 三课全胜教学存档（F0 注入用，解锁 btn-deck 教学门槛）
- `reports/` —— 报告产物（gitignore，不入库）

## 新增功能 → 新增检查点（工作流）

1. 功能落地时确定其**稳定日志标记**：现有 TideLog/Debug.Log 里找；没有合适标记就补一条
   有信息量的日志（日志即探针）
2. 在 `CHECKLIST.md` 登记检查项（编号按段顺延：交互流用 F1、F2…）
3. 在 `run.sh` 对应阶段加 check 调用（`check_contains` / `check_absent` / `check_stage`）
4. 全量跑一遍：新项 PASS 且报告里带原始日志证据 → 检查点生效
5. 跑出新噪音 → 按 `CHECKLIST.md` 台账维护规则定性登记（良性入册 / 缺陷修复）

## 判据纪律

- 只走数据层证据：logcat 断言、进程/内存/状态读数（截图+视觉模型做 UI 验收是被禁的，2026-10-03 定案）
- 每条交互必须「操作 → 日志回响」成对；没有回响的操作要么补日志要么不进清单
- UI 布局/坐标类验证仍在编辑器侧走（get_hierarchy / execute_code），本链路只验包内行为
- 模拟器侧状态波动（启动耗时抖动等）只记录不判死；判死项=崩溃/ANR/链路断裂/白名单外错误
