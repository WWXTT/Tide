# Tide 模拟器 E2E 验收清单（固定检查项）

> 用法：`bash E2E/run.sh` —— 全量跑，报告落 `E2E/reports/<时间戳>/report.md`（附完整 logcat）。
> 判据只走数据层证据（logcat 断言 / 进程 / 状态读数）；截图仅当用户点名时存档，不作判据。
>
> **验收纪律：每次验收必须全量跑一遍；FAIL 项不修不放过；新增功能必须同步登记新检查点。**

## 检查项

### 阶段1 冷启动（A）
| 项 | 判据 |
|---|---|
| A1 | logcat 出现 `UnityApplication::CreateInstance` |
| A2 | 出现 `ApplicationInfo 'com.WwtT.Tide'` |
| A3 | `[Launch] 初始化资源包` 出现且无 `初始化资源包失败` |
| A4 | `[Launch] 资源版本：<非空>`（记录版本号） |
| A5 | `[Launch] 加载资源清单` 出现且无失败 |
| A6 | `[Launch] 加载热更程序集` 出现且无 `热更 DLL 加载失败` |
| A7 | 主菜单就绪：`[Launch] 启动完成`（Main 场景激活=UIBootstrap 同步装 MainMenuScreen 的代理标记；`MainMenuScreen:OnEnter` 栈标记仅首装首跑出现，不作硬判据） |
| A8 | 冷启动耗时读数（am start→启动完成，趋势记录不判死） |

### 阶段2 菜单驻留 60s（B）
| 项 | 判据 |
|---|---|
| B0 | 污染检测：驻留段（脚本零操作）出现任何 `[UI点击]` / `[UINav]` / `fully stop com.WwtT.Tide`（外部强停）/ `reported as REPLACED`（包替换）→ **全段 INVALID**（B1-B4 不判定，建议重跑） |
| B1 | `pidof com.WwtT.Tide` 非空 |
| B2 | 驻留段无 `FATAL EXCEPTION` |
| B3 | 驻留段无白名单外 E 级日志 |
| B4 | 内存读数（Android 13+ 单包口径 `TOTAL PSS:`/`TOTAL RSS:`，进程已死则 WARN 跳过；原始 dumpsys 存 meminfo.txt） |

### 阶段3 热重启（C）
| 项 | 判据 |
|---|---|
| C1 | force-stop 后重启到 `启动完成`（90s 内），记录耗时 |
| C2 | 二启不再 `首次登录建档`（教学进度档案复用；二启 TutorialProgress 静默读取属正常） |
| C3 | 热启段无 `FATAL EXCEPTION` |
| C4 | 冷/热启资源版本一致 |

### 阶段4 退出（D，观察项）
| 项 | 记录 |
|---|---|
| D1 | BACK 键后进程状态 + 顶层 Activity |

### 全局健康（G）
| 项 | 判据 |
|---|---|
| G1/G1b/G1c | 冷启+驻留 / 热启 / 交互段全程无 `FATAL EXCEPTION` |
| G2/G2b/G2c | 无 ANR（logcat `ANR in` + /data/anr 目录观察） |
| G3 | E 级消息全部命中已知噪音台账（冷+热+交互段合并；F2 AOT 崩溃=真缺陷判 FAIL，**不入台账**） |
| G4 | W 级消息签名全部命中已知噪音台账（冷+热+交互段合并） |
| G5 | W 消息行总量（冷+热+交互，栈帧行已剔除）≤ 1500（基线 ~330/次启动） |
| G6 | 处理器覆盖率：警告行**缺失=PASS**；存在则 `缺失原子效果处理器 N 种` 的 N ≤ 基线 9（9=旧包值；2026-10-24 已白名单补登 11 个退役墓碑位，新包验证后基线改 0 盯回归） |
| N1 | **全程无原生崩溃**（2026-10-24 第3阶段新增）：`E/CRASH` / `Fatal signal` / `signal 11` / `backtrace:` / `tombstone` / `Process com.WwtT.Tide (pid N) has died` 任一命中即 FAIL（cold+warm+f 合扫，idle⊂cold 不重复）。动因：22:45 跑两起漏判——首启 SIGSEGV（E/CRASH 转储刷 59058 行）与「启动完成后 54ms 即崩」，均无 Java FATAL，G1/B2/C3 全瞎。A0r 重试必须附首试崩溃证据（块数/pid/信号/Cause/栈首帧），不许只写「起步抖动」。模式噪音底=21:25 干净跑全零标定；`has died` 按包名+pid 格式限定防他 app 死亡误伤 |

### 交互流（F 段 —— 2026-10-24 第2阶段启用）
设备准备（F0）：force-stop 后 adb push `assets/TutorialProgress.json`（三课全胜存档，解锁 btn-deck
教学门槛）到 `/sdcard/Android/data/com.WwtT.Tide/files/`，md5 校验落地；冷启到主菜单
（首装/清数据会先弹隐私同意 → 点 btn-agree 再进，链路有回响断言）。
坐标一律离线推导（`coords.sh` 登记表：预制体=execute_code 附加场景布局直调读数；
代码面板=源码布局常量推算），设计 1920×1080 → 设备像素按 wm size 短边/1080 换算。
**每步必须三件套齐备：点击坐标 + `[UI点击] <name>` 回响行 + `[UINav]` 屏幕切换行**；
回响缺失即 FAIL 并落 dumpsys window + 截图存档（存档不作判据）。

| 项 | 流程 | 判据 |
|---|---|---|
| F1 | 主菜单 → btn-deck → DeckBuilderScreen → btn-back 返回 | 两次点击各有回响；`Show<DeckBuilderScreen>` 与 `Back（弹 DeckBuilderScreen）` 各就位 |
| F2 | 主菜单 → btn-teststart → 木桩选项面板 btn-start → 段三掷币 → 选卡窗 btn-start → 开局 | `Show<BattleScreen>` → `[UI点击] btn-start`（选项）→ `[BattleScreen] 木桩选项：` → `[BattleOpening] 段三` → `[UI点击] btn-start`（选卡）→ `段四（木桩弹窗）` → `段五 交班开战`。**注意**：`[木桩] 选项面板已显示（等待确认）` 在 await 前打印，不作面板存在证据；当前包 AOT 泛型缺陷（`UniTask<Nullable<Options>>` builder 未实例化）导致面板弹不出 → F2=FAIL+根因行+附录，**不加白名单**（见报告「F2 阻塞缺陷附录」） |
| Fp | F 段污染检测 | f.log 窗口内非脚本点击/切换/外部强停/装包 → F1/F2 全判 INVALID（建议重跑） |

INVALID 语义：段被外部干预污染，结果不可信——不计 PASS/FAIL，退出码非 0，提示重跑。

## 已知噪音台账（与 common.sh 基线常量同源，改一处必改另一处）

E 级：
1. `AssetPackManager` ClassNotFoundException —— Play Core 探测，MuMu 无 Play 服务，每次启动必现
2. `Location is invalid: 'netstandard.dll'` —— **已修复待新包验证移除**（2026-10-24 移出 PatchAotAssemblies：.NET Framework 档 AOT 裁剪产物本就无 netstandard 门面；另加 IsLocationValid 前置守卫）

W 级：
1. `Location already exists:` ×~4.6k —— YooAsset 内置清单地址重复（美术包重复收录；要降噪须整清单地址去重，暂记账）
2. `缺失原子效果处理器 9 种` —— **已修复待新包验证移除**（2026-10-24 白名单补登 11 个退役枚举墓碑位，含旧包未见的 GrantPhotosynthesis/SituationGate；G6 已改缺失=PASS）
3. `[Launch] AOT 补充 netstandard 未打包` —— 同 E 级第 2 条
4. `Failed to map location to asset path: 'netstandard.dll'` —— 同上家族
5. `The referenced script on this Behaviour (Game Object 'Camera') is missing!` —— **旧版包问题待新包验证移除**（2026-10-25 磁盘核验当前 Main.unity 零丢脚本引用；10-21 旧包历史残留，用户拍板新包不会出现）
6. `No action required on overwrite installation.` —— **2026-10-24 入册（良性）**：覆盖安装（adb install -r 保留数据重装）时框架打印的一次性提示，与游戏行为无关；只在装包后的首次启动出现
7. `[EffectsLibrary] 效果引用缺失: 073f18c7（Effects.json 无此条）` —— **2026-10-24 入册（良性）**：effectIds 三路解析（主效果表→教学表→原子表）的首路 miss 告警；073f18c7=「先攻 GrantFirstStrike」行在原子表 AttributeValueConfig 有定义（引用卡 C_D706FDA0·教学表），回落原子表解析成功——勿误判悬空（同 2026-10-21 教学表分家定案口径）。F 段进卡组编辑器首现
8. `[CardCostService] 卡 … 存档费用 … 与推导 … 不一致` —— **2026-10-24 入册（良性）**：费用恒推导定案（2026-10-22）的自愈告警——存档费与推导费不一致时按推导刷新显示缓存；教学表卡带手写费触发，重存卡表可消。F 段进卡组编辑器首现
9. `[CardEffectConverter] … 时机=回合开始…已归一为 准备阶段` —— **2026-10-24 入册（良性）**：「回合开始」2026-10-20 起系统专用，转换器自动归一为准备阶段并留痕（自愈设计）。F 段进卡组编辑器首现
10. `The character with Unicode value \uXXXX was not found in the [PZD] font asset` —— **2026-10-24 入册（良性·字集缺口用户自理）**：PZD 静态字库缺字形（本次 \u783A 砺→□；同 2026-10-20 功/败/胜缺字家族）。F 段进卡组编辑器（卡名含砺）首现

## 台账维护规则
- 跑出白名单外 E/W → 先定性（良性/缺陷）：良性才入台账并在本文件登记出处证据；缺陷走修复，不入台账
- 台账条目修复后：从 common.sh 的 KNOWN_* 与本文件同时移除，并连续两次全量跑验证不再出现
- 基线数值变化（处理器种数、W 总量）→ 更新 common.sh 常量并在提交说明里注明依据
- **原生崩溃（N1）入册例外唯一口径（2026-10-24 第3阶段）**：仅当栈含 `libtcb.so`/`houdini`（MuMu 翻译层）**且**干净重启模拟器后不可复现，才允许入册为已知噪音，并写清复现条件；可稳定复现的原生崩溃按缺陷查，禁止入册
- 日志超 20MB 只保留头尾 3000 行+崩溃段（命中行±200/300，合并取前 5 段）——判据先按原文件执行，截断只影响存档体积
