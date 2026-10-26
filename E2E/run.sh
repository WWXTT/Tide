#!/usr/bin/env bash
# ============================================================================
# Tide 模拟器 E2E 验收全量跑 —— bash E2E/run.sh
# 产出：E2E/reports/<时间戳>/report.md（附冷启/热启完整 logcat）
# 退出码：0=全部硬检查通过；1=有 FAIL；2=环境故障
# 注意：跑之前会重启模拟器里的游戏，别在手动测试中途跑
# ============================================================================
set -u
cd "$(dirname "$0")"
. ./common.sh
. ./coords.sh
trap stop_capture EXIT

TS=$(date +%Y%m%d-%H%M%S)
OUT="$E2E_ROOT/reports/$TS"
mkdir -p "$OUT"
REPORT="$OUT/report.md"

# ---------------- 阶段 0：环境 ----------------
find_adb || die "未找到 adb（PATH 或 Android SDK platform-tools）"
connect_device || die "MuMu $DEVICE 连接失败（也无法自动拉起）"
ensure_app_installed

DEV_ANDROID=$(adb_shell "getprop ro.build.version.release")
DEV_MODEL=$(adb_shell "getprop ro.product.model")
WMSIZE=$(adb_shell "wm size")
WMDENSITY=$(adb_shell "wm density" | head -1 | tr -d '\r')
APP_VER=$(adb_shell "dumpsys package $PKG" | grep -m1 "versionName=")
ui_resolve_scale

APK_PATH=$(latest_apk)
APK_INFO="未找到"; APK_SHA="—"
if [ -n "$APK_PATH" ]; then
  APK_INFO="$(basename "$APK_PATH") | $(stat -c '%y' "$APK_PATH" | cut -c1-19) | $(stat -c '%s' "$APK_PATH") bytes"
  APK_SHA=$(sha256sum "$APK_PATH" | cut -d' ' -f1)
fi
GIT_HEAD=$(git -C "$E2E_ROOT/.." rev-parse --short HEAD 2>/dev/null || echo "?")
GIT_DIRTY_N=$(git -C "$E2E_ROOT/.." status --porcelain 2>/dev/null | wc -l | tr -d ' ')

{
  echo "# Tide 模拟器 E2E 验收报告"
  echo ""
  echo "- 时间：$TS"
  echo "- 设备：MuMu $DEVICE（$DEV_MODEL · Android $DEV_ANDROID · $WMSIZE · $WMDENSITY）"
  echo "- 应用：$PKG $APP_VER"
  echo "- 本地 APK：$APK_INFO"
  echo "- APK sha256：$APK_SHA"
  echo "- git：HEAD=$GIT_HEAD dirty=$GIT_DIRTY_N"
  echo "- 产物目录：$OUT"
} > "$REPORT"
echo "== 阶段0 环境 OK：$DEVICE / $APP_VER =="

# ---------------- 阶段 1：冷启动 ----------------
echo "== 阶段1 冷启动 =="
adb_shell "am force-stop $PKG" >/dev/null
sleep 2
start_capture "$OUT/cold.log"
T0=$(now_ms)
adb_shell "am start -n $ACT" >/dev/null
if ! wait_for "[Launch] 启动完成" "$OUT/cold.log" 120; then
  # 首试失败必须留崩溃证据（2026-10-24 第3阶段：22:45 首试实为 SIGSEGV 原生崩溃，曾被写成
  # 「环境起步抖动」）。A0r 只留证不判定——首试崩溃由 N1 统一判 FAIL；重试日志续入 cold.log，
  # A4-A7 判据以重试段为准。
  A0R_EV=$(native_crash_evidence "$OUT/cold.log")
  report_info A0r "冷启动首试未达启动完成——重试一次（判据以重试段为准）" "120s 未见 [Launch] 启动完成"$'\n'"首试段原生崩溃证据："$'\n'"$A0R_EV"
  adb_shell "am force-stop $PKG" >/dev/null
  sleep 3
  T0=$(now_ms)
  adb_shell "am start -n $ACT" >/dev/null
  wait_for "[Launch] 启动完成" "$OUT/cold.log" 120
fi
T1=$(now_ms); COLD_MS=$((T1-T0))

check_contains A1 "Unity 引擎创建" "$OUT/cold.log" "UnityApplication::CreateInstance"
check_contains A2 "应用身份行" "$OUT/cold.log" "ApplicationInfo 'com.WwtT.Tide'"
check_stage   A3 "资源包初始化" "$OUT/cold.log" "[Launch] 初始化资源包" "初始化资源包失败"
RESVER=$(grep -m1 -F "[Launch] 资源版本：" "$OUT/cold.log" 2>/dev/null | sed 's/.*资源版本：//')
if [ -n "$RESVER" ]; then _report PASS A4 "资源版本获取" "资源版本：$RESVER"
else _report FAIL A4 "资源版本获取" "未出现 [Launch] 资源版本："; fi
check_stage   A5 "资源清单加载" "$OUT/cold.log" "[Launch] 加载资源清单" "加载资源清单失败"
check_stage   A6 "热更程序集加载" "$OUT/cold.log" "[Launch] 加载热更程序集" "热更 DLL 加载失败"
# 主菜单就绪代理标记：启动完成=Main 场景激活（UIBootstrap.OnEnable 同步装 MainMenuScreen）。
# 注：MainMenuScreen:OnEnter 栈标记只在首装首跑（TutorialProgress 建档日志）出现，不作硬判据。
if grep -qF "[Launch] 启动完成" "$OUT/cold.log" 2>/dev/null; then
  _report PASS A7 "主菜单就绪（启动完成代理标记）" "$(grep -m1 -F '[Launch] 启动完成' "$OUT/cold.log")"
else _report FAIL A7 "主菜单就绪（启动完成代理标记）" "120s 内未出现 [Launch] 启动完成"; fi
report_info A8 "冷启动耗时（am start→启动完成）" "${COLD_MS}ms"

# ---------------- 阶段 2：菜单驻留 60s ----------------
echo "== 阶段2 菜单驻留 60s =="
IDLE_FROM=$(wc -l < "$OUT/cold.log")
i=0
while [ $i -lt 60 ]; do
  sleep 10; i=$((i+10))
  grep -qF "FATAL EXCEPTION" "$OUT/cold.log" 2>/dev/null && break
done
tail -n +$((IDLE_FROM+1)) "$OUT/cold.log" > "$OUT/idle.log"

# 污染检测（2026-10-24 第2阶段）：驻留段脚本零操作——窗口内任何点击/屏幕切换/
# 外部强停（fully stop … by user request）/包替换安装 = 段无效（不判 PASS/FAIL，重跑）
TAPS_ISSUED=(); SCREENS_EXPECTED=()
if ! scan_pollution 0 "$OUT/idle.log"; then
  _report INVALID B0 "驻留段被外部污染（B1-B4 不判定，建议重跑）" "$POLLUTE_EVIDENCE"
else
  PID=$(adb_shell "pidof $PKG")
  if [ -n "$PID" ]; then _report PASS B1 "驻留后进程存活" "pid=$PID"
  else _report FAIL B1 "驻留后进程存活" "pidof 为空（进程已死）"; fi
  check_absent B2 "驻留期无崩溃" "$OUT/idle.log" "FATAL EXCEPTION"
  UNK_E_IDLE=$(unity_msg_lines E "$OUT/idle.log" | filter_known "${KNOWN_E[@]}")
  if [ -z "$UNK_E_IDLE" ]; then _report PASS B3 "驻留期无白名单外 E 级" "（驻留段零未知 E）"
  else _report FAIL B3 "驻留期无白名单外 E 级" "$UNK_E_IDLE"; fi
  if [ -n "$PID" ]; then
    MEM=$(read_pss "$OUT/meminfo.txt")
    report_info B4 "驻留内存读数（PSS/RSS，趋势记录）" "${MEM:-（meminfo 无 TOTAL 行，原始见 meminfo.txt）}"
  else
    report_warn B4 "驻留内存读数" "进程已死无法读取（见 B1）"
  fi
fi

# ---------------- 阶段 3：热重启 ----------------
echo "== 阶段3 热重启 =="
stop_capture
start_capture "$OUT/warm.log"
adb_shell "am force-stop $PKG" >/dev/null
sleep 2
T0=$(now_ms)
adb_shell "am start -n $ACT" >/dev/null
wait_for "[Launch] 启动完成" "$OUT/warm.log" 90
T1=$(now_ms); WARM_MS=$((T1-T0))

if grep -qF "[Launch] 启动完成" "$OUT/warm.log" 2>/dev/null; then
  _report PASS C1 "热重启完成" "$(grep -m1 -F '[Launch] 启动完成' "$OUT/warm.log")"
else _report FAIL C1 "热重启完成" "90s 内未出现 [Launch] 启动完成"; fi
report_info C1t "热重启耗时" "${WARM_MS}ms"
if grep -qF "首次登录建档" "$OUT/warm.log" 2>/dev/null; then
  _report FAIL C2 "教学进度档案复用（二启不再建档）" "$(grep -m1 -F '首次登录建档' "$OUT/warm.log")"
else
  TP=$(grep -m1 -F "[TutorialProgress]" "$OUT/warm.log" 2>/dev/null)
  _report PASS C2 "教学进度档案复用（二启不再建档）" "二启 TutorialProgress 日志：${TP:-（无=静默读取既有档案）}"
fi
check_absent C3 "热启段无崩溃" "$OUT/warm.log" "FATAL EXCEPTION"
RESVER_W=$(grep -m1 -F "[Launch] 资源版本：" "$OUT/warm.log" 2>/dev/null | sed 's/.*资源版本：//')
if [ -n "$RESVER" ] && [ "$RESVER" = "$RESVER_W" ]; then
  _report PASS C4 "冷/热启资源版本一致" "$RESVER"
else _report FAIL C4 "冷/热启资源版本一致" "冷=$RESVER 热=$RESVER_W"; fi

# ---------------- 阶段 4：BACK 键退出行为（观察项）----------------
echo "== 阶段4 BACK 键行为（观察） =="
adb_shell "input keyevent 4" >/dev/null
sleep 3
PID_BK=$(adb_shell "pidof $PKG")
TOP=$(adb_shell "dumpsys activity activities" | grep -m1 "topResumedActivity")
report_info D1 "BACK 键行为观察" "按 BACK 后 pid=${PID_BK:-（进程已退出）}  顶层=${TOP:-?}"

# ---------------- 阶段 4.5：交互流（F 段·2026-10-24 第2阶段） ----------------
# 判据纪律：每步「点击坐标 + 点击回响行 + 屏幕切换行」三件套齐备才 PASS；
# 无回响的操作不得判过。段末做污染检测：窗口内非脚本点击/切换/外部强停/装包 → 全段 INVALID。
echo "== 阶段F 交互流 =="
stop_capture
adb_shell "am force-stop $PKG" >/dev/null
sleep 2

# F0-注入：三课全胜教学存档（btn-deck 有教学二完成门槛，不注入则按钮被门禁隐藏）
SAVE_SRC="$E2E_ROOT/assets/TutorialProgress.json"
SAVE_DST="/sdcard/Android/data/$PKG/files/TutorialProgress.json"
adb_shell "mkdir -p /sdcard/Android/data/$PKG/files" >/dev/null 2>&1
if MSYS_NO_PATHCONV=1 "$ADB" -s "$DEVICE" push "$(cygpath -w "$SAVE_SRC")" "$SAVE_DST" >/dev/null 2>&1; then
  LOC_MD5=$(md5sum "$SAVE_SRC" | cut -d' ' -f1)
  DEV_MD5=$(adb_shell "md5sum $SAVE_DST" | tr -d '\r' | cut -d' ' -f1)
  if [ -n "$DEV_MD5" ] && [ "$DEV_MD5" = "$LOC_MD5" ]; then
    echo "[F0] 存档注入 OK（md5=$LOC_MD5）"
  else
    _report FAIL F0 "三课全胜存档注入校验" "push 返回成功但 md5 不符：loc=$LOC_MD5 dev=${DEV_MD5:-（设备不可读）}"
  fi
else
  _report FAIL F0 "三课全胜存档注入" "adb push 失败：$SAVE_SRC → $SAVE_DST"
fi

start_capture "$OUT/f.log"
seg_log "$OUT/f.log"
TAPS_ISSUED=(); SCREENS_EXPECTED=(MainMenuScreen)
adb_shell "am start -n $ACT" >/dev/null

# F0-冷启到主菜单；首装/清数据场景会先弹隐私同意 → 点同意再进主菜单
F_CONSENT=0; t=0
while [ $t -lt 120 ]; do
  log_slice | grep -qF "[PrivacyConsent] 首启隐私同意弹窗已展示" && { F_CONSENT=1; break; }
  log_slice | grep -qF "[UINav] Show<MainMenuScreen>" && break
  sleep 2; t=$((t+2))
done
F0_PREP_OK=1
if [ "$F_CONSENT" = "1" ]; then
  sleep 1
  echo "[F0] 检出隐私同意弹窗 → 点击 btn-agree"
  if tap_expect btn-agree btn-agree 10 && expect_screen MainMenuScreen 60; then
    report_info F0c "隐私弹窗→同意→主菜单" "回响+切换：$LAST_MATCH"
  else
    _report FAIL F0c "隐私弹窗同意链路" "$LAST_MISS"; F0_PREP_OK=0
  fi
elif log_slice | grep -qF "[UINav] Show<MainMenuScreen>"; then
  TOP_RESUMED=$(adb_shell "dumpsys activity activities" | grep -m1 "topResumedActivity")
  report_info F0 "交互段就绪（存档注入+冷启主菜单）" "topResumed：${TOP_RESUMED:-?}"
else
  _report FAIL F0m "F 段冷启到主菜单" "120s 内未见 [UINav] Show<MainMenuScreen>"; F0_PREP_OK=0
fi

# ---- F1：主菜单 → btn-deck（DeckBuilderScreen）→ btn-back 返回 ----
F1_RES=""; F1_EV=""
E1=""; S1=""; E2L=""; S2L=""
if [ "$F0_PREP_OK" = "1" ]; then
  log_rebase
  tap_expect btn-deck btn-deck 15; R=$?; TD1="$TAP_DESC"
  [ $R -eq 0 ] && E1="$LAST_MATCH" || E1="（回响缺失）$LAST_MISS"
  expect_screen DeckBuilderScreen 15; R=$?
  [ $R -eq 0 ] && S1="$LAST_MATCH" || S1="（未切换）$LAST_MISS"
  F1_EV="步骤1 btn-deck：$TD1"$'\n'"  回响 $E1"$'\n'"  屏幕 $S1"
  if [ $R -eq 0 ]; then
    SCREENS_EXPECTED+=(DeckBuilderScreen)
    tap_expect btn-deck-back btn-back 15; R=$?; TD2="$TAP_DESC"
    [ $R -eq 0 ] && E2L="$LAST_MATCH" || E2L="（回响缺失）$LAST_MISS"
    expect_screen DeckBuilderScreen 15; R=$?
    [ $R -eq 0 ] && S2L="$LAST_MATCH" || S2L="（未返回）$LAST_MISS"
    F1_EV="$F1_EV"$'\n'"步骤2 btn-back：$TD2"$'\n'"  回响 $E2L"$'\n'"  屏幕 $S2L"
  fi
  case "$E1$S1$E2L$S2L" in *缺失*|*未切换*|*未返回*) F1_RES="FAIL";; *) F1_RES="PASS";; esac
fi

# ---- F2：主菜单 → btn-teststart → 选项面板 btn-start → 选卡窗 btn-start → 段五开局 ----
# 注：`[木桩] 选项面板已显示（等待确认）` 在 await 之前打印，不作面板存在证据；
#     面板存在唯一可靠证据=btn-start 点击回响。当前包有 AOT 泛型缺陷（详见附录）→ FAIL+根因。
F2_RES=""; F2_EV=""; F2_AOT=0
if [ "$F0_PREP_OK" = "1" ]; then
  log_rebase
  F2_BASE=$CUR_BASE   # AOT 崩溃行与 Show<BattleScreen> 仅隔 ~1s：检测窗口锚在点击前，防 rebase 竞态吞行
  if tap_expect btn-teststart btn-teststart 15; then
    ET="$LAST_MATCH"; TDT="$TAP_DESC"
    if expect_screen BattleScreen 30; then
      ST="$LAST_MATCH"; SCREENS_EXPECTED+=(BattleScreen)
      sleep 3
      CUR_BASE=$F2_BASE
      if expect_in_log "AOT generic method not instantiated in aot" 10; then
        F2_AOT=1
        CRASH_LINE=$(grep -m1 -F "AOT generic method not instantiated in aot" "$OUT/f.log")
        CRASH_STACK=$(grep -A5 -m1 -F "MissingMethodException: AOT generic" "$OUT/f.log" | sed 's/^10-[0-9]* [0-9:.]* [EW]\/Unity *( *[0-9]*): //')
        F2_RES="FAIL"
        F2_EV="步骤1 btn-teststart：$TDT"$'\n'"  回响 $ET"$'\n'"  屏幕 $ST"$'\n'"异常 $CRASH_LINE"$'\n'"调用链"$'\n'"$CRASH_STACK"$'\n'"→ 木桩选项面板弹不出（btn-start 不可达），后续步骤无法执行"
      else
        # 修复包路径：选项面板确认 → 段三掷币 → 选卡窗开战 → 段四/段五
        EO=""; SO=""; S3L=""; ED=""; S4L=""; S5L=""
        tap_expect btn-dummy-start btn-start 20; R=$?; TDO="$TAP_DESC"
        [ $R -eq 0 ] && EO="$LAST_MATCH" || EO="（回响缺失）$LAST_MISS"
        expect_in_log "\[BattleScreen\] 木桩选项：" 10; R=$?
        [ $R -eq 0 ] && SO="$LAST_MATCH" || SO="（未见 木桩选项 行）$LAST_MISS"
        expect_in_log "\[BattleOpening\] 段三" 60; R=$?
        [ $R -eq 0 ] && S3L="$LAST_MATCH" || S3L="（未见 段三）$LAST_MISS"
        tap_expect btn-deckpanel-start btn-start 20; R=$?; TDD="$TAP_DESC"
        [ $R -eq 0 ] && ED="$LAST_MATCH" || ED="（回响缺失）$LAST_MISS"
        expect_in_log "\[BattleOpening\] 段四（木桩弹窗）" 15; R=$?
        [ $R -eq 0 ] && S4L="$LAST_MATCH" || S4L="（未见 段四）$LAST_MISS"
        expect_in_log "\[BattleOpening\] 段五" 40; R=$?
        [ $R -eq 0 ] && S5L="$LAST_MATCH" || S5L="（未见 段五·开局未交班）$LAST_MISS"
        F2_EV="步骤1 btn-teststart：$TDT"$'\n'"  回响 $ET"$'\n'"  屏幕 $ST"$'\n'"步骤2 选项面板 btn-start：$TDO"$'\n'"  回响 $EO"$'\n'"  选项 $SO"$'\n'"步骤3 掷币段：$S3L"$'\n'"步骤4 选卡窗 btn-start：$TDD"$'\n'"  回响 $ED"$'\n'"  段四 $S4L"$'\n'"  段五 $S5L"
        case "$EO$SO$S3L$ED$S4L$S5L" in *缺失*|*未见*) F2_RES="FAIL";; *) F2_RES="PASS";; esac
      fi
    else
      F2_RES="FAIL"
      F2_EV="步骤1 btn-teststart：$TDT"$'\n'"  回响 $ET"$'\n'"  屏幕（未切换）$LAST_MISS"
    fi
  else
    F2_RES="FAIL"
    F2_EV="步骤1 btn-teststart：$TAP_DESC"$'\n'"  （回响缺失）$LAST_MISS"
  fi
fi

# ---- F 段污染检测 → 落判（污染=全段 INVALID，不判 PASS/FAIL） ----
# 无条件执行（含 F0 失败的残局）：误击他钮/串台点击/外部干预必须被抓出来——
# 2026-10-24 实录：坐标推错误点 btn-refuse 触发「不同意并退出」，靠的就是回响身份比对。
if ! scan_pollution 0 "$OUT/f.log"; then
  _report INVALID Fp "F 段被外部污染或出现非预期回响（F1/F2 判 INVALID，建议重跑）" "$POLLUTE_EVIDENCE"
  if [ "$F0_PREP_OK" = "1" ]; then
    [ -n "$F1_RES" ] && _report INVALID F1 "卡组编辑器进出（段污染·不判定）" "$F1_EV"
    [ -n "$F2_RES" ] && _report INVALID F2 "木桩战开局（段污染·不判定）" "$F2_EV"
  fi
else
  if [ "$F0_PREP_OK" = "1" ]; then
    [ -n "$F1_RES" ] && _report "$F1_RES" F1 "卡组编辑器进出（主菜单→DeckBuilderScreen→返回）" "$F1_EV"
    [ -n "$F2_RES" ] && _report "$F2_RES" F2 "木桩战开局（btn-teststart→btn-start×2→段五交班）" "$F2_EV"
  fi
fi

# ---------------- 阶段 5：全局健康汇总 ----------------
echo "== 阶段5 全局健康 =="
stop_capture

check_absent G1 "全程无 FATAL（冷启+驻留）" "$OUT/cold.log" "FATAL EXCEPTION"
check_absent G1b "全程无 FATAL（热启）" "$OUT/warm.log" "FATAL EXCEPTION"
[ -f "$OUT/f.log" ] && check_absent G1c "全程无 FATAL（交互段）" "$OUT/f.log" "FATAL EXCEPTION"
# N1 原生崩溃判据（2026-10-24 第3阶段）：E/CRASH / Fatal signal / signal 11 / backtrace: /
# tombstone / Process com.WwtT.Tide (pid N) has died 任一命中即 FAIL——Java FATAL 查不全原生
# 死（22:45 两起实锤：首启 SIGSEGV + 启动完成后 54ms 崩，G1/B2/C3 全漏）。idle.log ⊂ cold.log
# 不重复扫。入册例外唯一口径见 CHECKLIST 台账规则（libtcb/houdini + 干净重启不可复现）。
N1_N=0; N1_EV=""
for lg in cold warm f; do
  L="$OUT/$lg.log"; [ -f "$L" ] || continue
  N1C=$(native_crash_count "$L")
  if [ "${N1C:-0}" != "0" ]; then
    N1_N=$((N1_N+N1C))
    N1_EV="${N1_EV}—— $lg.log 命中 $N1C 行"$'\n'"$(native_crash_evidence "$L")"$'\n'
  fi
done
if [ "$N1_N" = "0" ]; then
  _report PASS N1 "全程无原生崩溃（E/CRASH/信号/转储/has died 全零）" "（cold+warm+f 合扫；模式噪音底=21:25 干净跑全零标定）"
else
  _report FAIL N1 "全程无原生崩溃" "命中 $N1_N 行"$'\n'"$N1_EV"
fi
check_absent G2 "无 ANR 记录（冷启）" "$OUT/cold.log" "ANR in "
check_absent G2b "无 ANR 记录（热启）" "$OUT/warm.log" "ANR in "
ANR_FILES=$(adb_shell "ls /data/anr/")
report_info G2c "ANR 目录观察" "/data/anr：${ANR_FILES:-（空或不可读）}"

# E 级白名单外（冷+热+交互段合并；f.log 缺失容忍）
UNK_E=$(cat <(unity_msg_lines E "$OUT/cold.log") <(unity_msg_lines E "$OUT/warm.log") <(unity_msg_lines E "$OUT/f.log" 2>/dev/null) | filter_known "${KNOWN_E[@]}")
if [ -z "$UNK_E" ]; then _report PASS G3 "E 级全部命中已知噪音台账" "（白名单外 E 为零）"
else _report FAIL G3 "E 级全部命中已知噪音台账" "$UNK_E"; fi

# W 级签名分析（冷+热+交互段合并）
WM="$OUT/wmsgs.txt"
cat <(unity_msg_lines W "$OUT/cold.log") <(unity_msg_lines W "$OUT/warm.log") <(unity_msg_lines W "$OUT/f.log" 2>/dev/null) > "$WM"
WTOT=$(wc -l < "$WM")
WUNKNOWN=$(filter_known "${KNOWN_W[@]}" < "$WM" | head -40)
if [ -z "$WUNKNOWN" ]; then _report PASS G4 "W 级签名全部命中已知噪音台账" "消息总数 $WTOT"
else _report FAIL G4 "W 级签名全部命中已知噪音台账" "$WUNKNOWN"; fi
if [ "$WTOT" -le "$W_TOTAL_WARN" ]; then _report PASS G5 "W 消息总量 ≤ $W_TOTAL_WARN" "实际 $WTOT"
else _report FAIL G5 "W 消息总量 ≤ $W_TOTAL_WARN" "实际 $WTOT（超阈值，检查是否新增噪音源）"; fi

# 缺失原子效果处理器种数（回归盯防）——先截「处理器 N 种」片段再取数，防吃到时间戳数字。
# 警告行缺失=PASS（2026-10-24 白名单补登 11 个退役墓碑位后，新包应零警告；旧包仍打 9）
HN=$(grep -m1 -oE "缺失原子效果处理器 [0-9]+ 种" "$OUT/cold.log" 2>/dev/null | grep -oE "[0-9]+" | head -1)
HLINE=$(grep -m1 -F "缺失原子效果处理器" "$OUT/cold.log" 2>/dev/null)
if [ -z "$HN" ]; then
  _report PASS G6 "处理器覆盖率干净（无警告行）" "（GameCore 已初始化前提下警告缺席=全覆盖）"
elif [ "$HN" -le "$HANDLER_MISSING_BASE" ]; then
  _report PASS G6 "缺失处理器种数 ≤ 基线 $HANDLER_MISSING_BASE" "实际 $HN（基线 $HANDLER_MISSING_BASE）$([ "$HN" -lt "$HANDLER_MISSING_BASE" ] && echo '—— 减少，请更新 common.sh 基线')"
  echo "$HLINE" >> "$REPORT"
else
  _report FAIL G6 "缺失处理器种数 ≤ 基线 $HANDLER_MISSING_BASE" "实际 $HN > 基线（新增缺失=回归）"$'\n'"$HLINE"
fi

# ---------------- AOT 阻塞缺陷附录（真缺陷·不入噪音台账·修复方案等拍板） ----------------
if [ "$F2_AOT" = "1" ] || grep -qF "AOT generic method not instantiated in aot" "$OUT/f.log" 2>/dev/null; then
  {
    echo ""
    echo "## F2 阻塞缺陷附录（等拍板，未入台账）"
    echo ""
    echo "- **异常原文**：MissingMethodException: AOT generic method not instantiated in aot. assembly:UniTask.dll, method:AsyncUniTaskMethodBuilder\`1[System.Nullable\`1[SynergyUI.DummyOptionsPanel+Options]]::Create()"
    echo "- **调用链**：DummyOptionsPanel.ShowAsync ← BattleScreen.StartDummyOpeningAsync ← StartLocalFan ← BattleScreen.OnEnter ← UIScreen.Mount"
    echo "- **APK**：$(basename "$APK_PATH") ｜ $(stat -c '%y' "$APK_PATH" | cut -c1-19) ｜ sha256 $APK_SHA"
    echo "- **复现日志**：本目录 f.log（F2 段）"
    echo "- **定性**：UniTask\<热更 struct 的 Nullable\> 泛型 builder 在 AOT 程序集无实例化；link.xml 对 UniTask preserve=all 不生成跨程序集泛型实例化，HybridCLR 补不出。同族隐患：DummyDeckPanel.ShowAsync 返回 UniTask\<Pick\>（非 Nullable 但同为热更 struct 泛型实参）——修 Options 后必炸，须一并处理。"
    echo "- **最小修复候选（等用户拍板，本阶段不改游戏代码）**："
    echo "  1. 改签名走引用类型结果：OptionsResult / PickResult class（带 HasValue/Cancelled + 字段），ShowAsync 返回 UniTask\<OptionsResult\>——AOT 型实参天然可用；热更程序集内全面排查 UniTask\<热更struct\> / UniTaskCompletionSource\<热更struct\> / Nullable\<热更struct\> 泛型实参清零。"
    echo "  2. AOT 侧补泛型实例锚（AOTGenericReferences）——热更 struct 在 AOT 程序集引用不到，实际不可行，仅列作对照。"
    echo "- **E2E 口径**：G3 对该 E 级判 FAIL（真缺陷跟踪，不加白名单）。"
  } >> "$REPORT"
fi

# ---------------- 日志体积上限（判据已全部跑完，截断只影响存档体积） ----------------
for lg in cold idle warm f; do [ -f "$OUT/$lg.log" ] && trim_log "$OUT/$lg.log"; done

# ---------------- 总览与退出 ----------------
{
  echo ""
  echo "---"
  echo ""
  echo "## 结果总览"
  echo ""
  echo "| 检查项 | 结果 | 描述 |"
  echo "|---|---|---|"
  for s in "${SUMMARY[@]}"; do
    IFS='|' read -r rid rres rdesc <<< "$s"
    echo "| $rid | $rres | $rdesc |"
  done
  echo ""
  echo "**PASS=$PASS_N  FAIL=$FAIL_N  WARN=$WARN_N  INFO=$INFO_N  INVALID=$INVALID_N**"
  echo ""
  echo "判据与台账见 E2E/CHECKLIST.md；原始日志：cold.log / idle.log / warm.log / f.log / wmsgs.txt"
} >> "$REPORT"

echo ""
echo "================ E2E 完成 ================"
echo "报告：$REPORT"
echo "PASS=$PASS_N  FAIL=$FAIL_N  WARN=$WARN_N  INFO=$INFO_N  INVALID=$INVALID_N"
if [ "$FAIL_N" = "0" ] && [ "$INVALID_N" = "0" ]; then exit 0; else exit 1; fi
