#!/usr/bin/env bash
# ============================================================================
# Tide 模拟器 E2E 公共库 —— adb 驱动 MuMu 模拟器里的 Tide，日志断言出验收报告
# 环境：Windows + Git Bash + adb（Android SDK platform-tools）
# 约定：
#   * adb shell 远端命令一律整体加引号（防 Git Bash 把 /data/... 改写成本地路径）
#   * 判据只走数据层证据（logcat 断言 / 进程 / 状态读数）；截图仅存档不作判据
#   * 基线台账改动必须同步 CHECKLIST.md「已知噪音台账」
# ============================================================================

# ---------------- 基线台账（2026-10-24 首跑建立） ----------------
HANDLER_MISSING_BASE=9    # [BuiltinHandlerBootstrap] 缺失原子效果处理器 种数基线
W_TOTAL_WARN=1500         # W/Unity 消息行总量告警线（2026-10-24 校准：~330 条/次启动×冷+热≈660；原始行口径曾误标 7500）

# 已知 E 级噪音（固定子串匹配；新噪音须先定性再入册）
KNOWN_E=(
  "com.google.android.play.core.assetpacks.AssetPackManager"   # Play Core 探测 CNFE（MuMu 无 Play 服务）
  "Location is invalid: 'netstandard.dll'"                     # AOT 补充未打包，HotUpdateLoader 已兜底跳过
)

# 已知 W 级噪音（固定子串匹配）
KNOWN_W=(
  "Location already exists:"                                   # YooAsset 内置清单地址重复（美术包重复收录，~4.6k 条）
  "缺失原子效果处理器"                                          # BuiltinHandlerBootstrap 覆盖率警告（种数由 G6 盯回归）
  "[Launch] AOT 补充"                                          # AOT 补充跳过告警（netstandard 家族）
  "Failed to map location to asset path: 'netstandard.dll'"    # netstandard 家族
  "The referenced script on this Behaviour"                    # Main 场景 Camera 丢脚本引用（2026-10-24 发现，待修）
  "No action required on overwrite installation."              # 2026-10-24 入册：覆盖安装提示（包重复安装时框架打印，良性，见 CHECKLIST W-6）
  "[EffectsLibrary] 效果引用缺失"                                # 2026-10-24 入册：effectIds 三路解析（效果表→教学表→原子表）首路 miss 告警；073f18c7=先攻行在原子表有定义（引用卡 C_D706FDA0），回落原子表解析成功，勿误判悬空（同 2026-10-21 教学表分家定案）。F 段进卡组编辑器首现
  "[CardCostService] 卡 "                                      # 2026-10-24 入册：费用恒推导定案（2026-10-22）自愈告警——存档费与推导费不一致时按推导刷新显示缓存；教学表卡带手写费触发，重存卡表可消。F 段进卡组编辑器首现
  "[CardEffectConverter]"                                      # 2026-10-24 入册：时机归一告警——「回合开始」2026-10-20 起系统专用，转换器自动归一为准备阶段并留痕（自愈设计）。F 段进卡组编辑器首现
  "was not found in the [PZD] font asset"                      # 2026-10-24 入册：PZD 静态字库缺字形（砺\u783A→□ 替换占位）——字集缺口用户自理（同 2026-10-20 功/败/胜缺字家族）。F 段进卡组编辑器首现
)

# ---------------- 目标常量 ----------------
PKG="com.WwtT.Tide"
ACT="com.WwtT.Tide/com.unity3d.player.UnityPlayerGameActivity"
DEVICE="127.0.0.1:16384"
MUMU_MANAGER="C:/Program Files/Netease/MuMu/nx_main/MuMuManager.exe"
ADB=""

E2E_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# 现成 APK 发现（Tide-Local.apk=本地单机风味 / Tide.apk=TapTap 联机风味；包名相同互斥同装）。
# 缺省取 mtime 最新；要锁风味用环境变量 TIDE_APK 指定文件名。
latest_apk() {
  if [ -n "${TIDE_APK:-}" ] && [ -f "$E2E_ROOT/../Builds/Android/$TIDE_APK" ]; then
    echo "$E2E_ROOT/../Builds/Android/$TIDE_APK"; return
  fi
  ls -t "$E2E_ROOT/../Builds/Android/"Tide*.apk 2>/dev/null | head -1
}

die() { echo "[E2E][FATAL] $*" >&2; exit 2; }

find_adb() {
  command -v adb >/dev/null 2>&1 && { ADB="adb"; return 0; }
  local p
  for p in "C:/Users/$USERNAME/AppData/Local/Android/Sdk/platform-tools/adb.exe" \
           "$LOCALAPPDATA/Android/Sdk/platform-tools/adb.exe" \
           "C:/Program Files/Netease/MuMu/nx_main/adb.exe"; do
    if [ -n "$p" ] && [ -x "$p" ]; then ADB="$p"; return 0; fi
  done
  return 1
}

# 远端命令：整体引号传入（例：adb_shell "getprop sys.boot_completed"）
adb_shell() { "$ADB" -s "$DEVICE" shell "$@" 2>/dev/null | tr -d '\r'; }

connect_device() {
  "$ADB" connect "$DEVICE" >/dev/null 2>&1
  if "$ADB" devices | grep -qE "^${DEVICE}[[:space:]]+device$"; then return 0; fi
  if [ -f "$MUMU_MANAGER" ]; then
    echo "[env] MuMu 未连接，尝试启动实例 0 …"
    ( cd "$(dirname "$MUMU_MANAGER")" && ./MuMuManager.exe control -v 0 launch >/dev/null 2>&1 )
    local i boot
    for i in $(seq 1 36); do
      sleep 5
      "$ADB" connect "$DEVICE" >/dev/null 2>&1
      boot=$(adb_shell "getprop sys.boot_completed")
      [ "$boot" = "1" ] && return 0
    done
  fi
  return 1
}

# 已装则比新（2026-10-24 第2阶段）：优先 sha256 对拍（pm path→sha256sum），读不到回落
# mtime（本地 APK 比设备 lastUpdateTime 新→重装）。覆盖安装保留应用数据。
ensure_app_installed() {
  local apk; apk=$(latest_apk)
  [ -z "$apk" ] && die "设备上没有 $PKG，且 Builds/Android 下找不到 Tide*.apk"
  if ! adb_shell "pm list packages" | grep -q "package:$PKG"; then
    echo "[env] 安装 $apk …"
    "$ADB" -s "$DEVICE" install -r "$apk" >/dev/null 2>&1 || die "APK 安装失败：$apk"
    return 0
  fi
  local dev_path dev_sha loc_sha
  dev_path=$(adb_shell "pm path $PKG" | head -1 | sed 's/^package://' | tr -d '\r')
  loc_sha=$(sha256sum "$apk" 2>/dev/null | cut -d' ' -f1)
  dev_sha=""
  [ -n "$dev_path" ] && dev_sha=$(adb_shell "sha256sum $dev_path" | tr -d '\r' | cut -d' ' -f1)
  if [ -n "$dev_sha" ] && [ -n "$loc_sha" ]; then
    if [ "$dev_sha" = "$loc_sha" ]; then
      echo "[env] 已装=最新（sha256 ${loc_sha:0:12}…）"
      return 0
    fi
    echo "[env] 设备包与本地 APK 不一致（dev=${dev_sha:0:12} loc=${loc_sha:0:12}）→ 重装 $apk"
    "$ADB" -s "$DEVICE" install -r "$apk" >/dev/null 2>&1 || die "APK 升级安装失败：$apk"
    return 0
  fi
  # sha 读不到（权限）→ mtime 兜底（设备 lastUpdateTime 时区与主机不同会误判，仅兜底用）
  local lastupd apkm dev_epoch
  lastupd=$(adb_shell "dumpsys package $PKG" | grep -m1 "lastUpdateTime=" | sed 's/.*lastUpdateTime=//' | tr -d '\r')
  apkm=$(stat -c '%Y' "$apk" 2>/dev/null)
  dev_epoch=""
  [ -n "$lastupd" ] && dev_epoch=$(date -d "$lastupd" +%s 2>/dev/null)
  if [ -n "$dev_epoch" ] && [ -n "$apkm" ] && [ "$apkm" -gt "$dev_epoch" ]; then
    echo "[env] 本地 APK（$(stat -c '%y' "$apk" | cut -c1-19)）比设备安装时间（$lastupd）新 → 重装"
    "$ADB" -s "$DEVICE" install -r "$apk" >/dev/null 2>&1 || die "APK 升级安装失败：$apk"
    return 0
  fi
  echo "[env] 已装版本无法比对（sha/mtime 均不可读），沿用设备现装包"
}

# ---------------- 日志捕获 ----------------
CAP_PID=""
start_capture() { # $1=输出文件；先清缓冲再开流（应用在流开后才启动，无丢失窗口）
  CAP_FILE="$1"
  "$ADB" -s "$DEVICE" logcat -c 2>/dev/null
  "$ADB" -s "$DEVICE" logcat -v time > "$CAP_FILE" 2>/dev/null &
  CAP_PID=$!
}
stop_capture() {
  if [ -n "$CAP_PID" ]; then kill "$CAP_PID" 2>/dev/null; wait "$CAP_PID" 2>/dev/null; CAP_PID=""; fi
}

wait_for() { # $1=固定串 $2=日志文件 $3=超时秒 → 0=出现
  local pat="$1" file="$2" tmo="${3:-120}" waited=0
  while [ "$waited" -lt "$tmo" ]; do
    grep -qF "$pat" "$file" 2>/dev/null && return 0
    sleep 2; waited=$((waited+2))
  done
  grep -qF "$pat" "$file" 2>/dev/null
}

now_ms() { local t; t=$(date +%s%3N 2>/dev/null); case "$t" in *N*) date +%s;; *) echo "$t";; esac; }

# ---------------- Unity 消息行抽取 ----------------
# Unity 每条日志 = 消息行 + 若干栈帧行 + 空行；只取消息本体。
# 双保险：块起点（上一行非本级/空行）或「长得不像栈帧」都算消息行——
# 无空行结尾的块（如 Java 侧 CNFE）不会吞掉下一条消息。
# 注意：-v 不能传 "/W/Unity" 这种串——MSYS 会把 lv=/W/Unity 当路径改写，
# 模式必须在 BEGIN 里拼（BEGIN 内赋值不经参数改写）。
# 2026-10-24 补：Unity 安卓侧会把超长日志行拆成多条 logcat 条目——栈帧行被拦腰
# 拆开后的后半段（如 "UnityEngine.EventSystems.ExecuteEv"）无括号无空格，
# 原栈帧正则放行 → 假"消息行"污染 G4。补第二判定：纯标识符链（无空格/括号）=碎片。
unity_msg_lines() { # $1=级别(I/W/E) $2=日志文件
  local lvl="$1" file="$2"
  awk -v lvl="$lvl" '
    BEGIN { lv = lvl "/Unity" }
    {
      if ($0 !~ lv) { prev = 0; next }
      msg = $0; sub(/^.*\/Unity +\([ 0-9]+\): /, "", msg)
      if (msg == "") { prev = 0; next }
      stacklike = (msg ~ /^[A-Za-z_][A-Za-z0-9_.<>`]+(\.[A-Za-z0-9_<>`]+)*(:[<A-Za-z_.][A-Za-z0-9_<>`.]*)?\(.*\)$/) || (msg ~ /^[A-Za-z_][A-Za-z0-9_.<>`]*:?$/)
      if (!prev || !stacklike) print
      prev = 1
    }' "$file"
}

filter_known() { # stdin=消息行；参数=白名单子串 → 输出未命中白名单的行
  local pats=("$@") line keep
  while IFS= read -r line; do
    keep=1
    for p in "${pats[@]}"; do case "$line" in *"$p"*) keep=0; break;; esac; done
    [ "$keep" = "1" ] && printf '%s\n' "$line"
  done
}

# ---------------- 原生崩溃判据（N1·2026-10-24 第3阶段） ----------------
# 背景：22:45 跑两起漏判实锤——首启 SIGSEGV（E/CRASH 转储刷 59058 行）与「启动完成后 54ms
# 即崩」（warm pid 5437），均无 Java FATAL EXCEPTION，G1/B2/C3 全瞎。五模式在 21:25 干净跑
# 里全零噪音底（MuMu 其他进程不产这些行），故全局匹配；has died 是 ActivityManager 行、
# 按包名+pid 格式限定（防同模拟器其他 app 死亡误伤）。
NATIVE_CRASH_PATS=("E/CRASH" "Fatal signal" "signal 11" "backtrace:" "tombstone")

native_crash_count() { # $1=文件 → stdout=命中行数（崩溃管线模式并集 + 本包 has died）
  local f="$1" p n c
  [ -f "$f" ] || { echo 0; return; }
  n=0
  for p in "${NATIVE_CRASH_PATS[@]}"; do
    c=$(grep -cF "$p" "$f" 2>/dev/null); c=${c:-0}; n=$((n+c))
  done
  c=$(grep -cE "com\.WwtT\.Tide \(pid [0-9]+\) has died" "$f" 2>/dev/null); c=${c:-0}
  echo $((n+c))
}

native_crash_evidence() { # $1=文件 → 多行证据：块数/进程行/信号/Cause/栈首帧/has died/翻译层标记
  local f="$1" line
  [ -f "$f" ] || { echo "（文件缺失）"; return 0; }
  echo "本包崩溃块数：$(grep -cF '>>> com.WwtT.Tide <<<' "$f" 2>/dev/null)"
  line=$(grep -m1 -F ">>> com.WwtT.Tide <<<" "$f" 2>/dev/null); [ -n "$line" ] && echo "进程行：$line"
  line=$(grep -m1 -E "Fatal signal [0-9]+|signal [0-9]+ \(SIG" "$f" 2>/dev/null); [ -n "$line" ] && echo "信号行：$line"
  line=$(grep -m1 -F "Cause:" "$f" 2>/dev/null); [ -n "$line" ] && echo "Cause：$line"
  line=$(grep -m1 -E "#[0-9]+ pc " "$f" 2>/dev/null); [ -n "$line" ] && echo "栈首帧：$line"
  line=$(grep -m1 -E "com\.WwtT\.Tide \(pid [0-9]+\) has died" "$f" 2>/dev/null); [ -n "$line" ] && echo "进程死亡：$line"
  grep -qE "E/CRASH .*(houdini|libtcb\.so)" "$f" 2>/dev/null && echo "翻译层标记：E/CRASH 转储含 houdini/libtcb.so（入册例外须同时满足：栈含 libtcb/houdini + 干净重启后不可复现；MuMu 系统进程的 I/houdini、E/HP 补丁器闲聊行不算）"
  return 0
}

# ---------------- 日志体积上限（20MB·2026-10-24 第3阶段） ----------------
# 崩溃转储风暴可把单日志刷到几十 MB（22:45 cold.log 59058 行 E/CRASH）。所有判据先按原文件
# 执行，截断只影响存档体积：保留头 3000 + 尾 3000 + 崩溃段（命中行前 200 后 300，合并后取
# 前 5 段防风暴全文回填）。
LOG_MAX_BYTES=$((20*1024*1024))
trim_log() { # $1=文件 → 超限截断（原地替换）
  local f="$1" size total
  size=$(stat -c '%s' "$f" 2>/dev/null) || return 0
  [ -z "$size" ] && return 0
  [ "$size" -le "$LOG_MAX_BYTES" ] && return 0
  total=$(wc -l < "$f")
  local head_n=3000 tail_n=3000 ranges="" p ln s e merged tail_start
  for p in "${NATIVE_CRASH_PATS[@]}" "FATAL EXCEPTION" ">>> com.WwtT.Tide <<<"; do
    while IFS= read -r ln; do
      [ -z "$ln" ] && continue
      s=$((ln-200)); [ "$s" -lt 1 ] && s=1
      e=$((ln+300)); [ "$e" -gt "$total" ] && e=$total
      ranges+="$s $e"$'\n'
    done < <(grep -nF "$p" "$f" 2>/dev/null | cut -d: -f1)
  done
  merged=$(printf '%s' "$ranges" | awk 'NF==2' | sort -n -k1,1 -k2,2 |
    awk '{if(NR==1){cs=$1;ce=$2;next} if($1<=ce+1){if($2>ce)ce=$2} else {print cs,ce; cs=$1;ce=$2}} END{if(NR>0)print cs,ce}' | head -5 | tr '\n' ';')
  tail_start=$((total - tail_n + 1)); [ "$tail_start" -lt 1 ] && tail_start=1
  awk -v head="$head_n" -v tn="$tail_n" -v ts="$tail_start" -v total="$total" -v size="$size" -v rng="$merged" '
    BEGIN {
      n = split(rng, R, /[; ]+/)
      printf "[E2E][trim] 原文件 %d 行 / %d 字节超 20MB——保留头 %d 行+尾 %d 行+崩溃段(%s)；所有判据已按原文件执行。\n", total, size, head, tn, (rng==""?"无":rng)
    }
    { if (NR <= head || NR >= ts) { print; next }
      for (i = 1; i < n; i += 2) {
        if (R[i] != "" && NR >= R[i]+0 && NR <= R[i+1]+0) {
          if (kept[i] < 800) { kept[i]++; print }
          else if (!marked[i]) { marked[i]=1; print "[E2E][trim] —— 崩溃段超 800 行，段内后续省略（判据已按原文件执行）——" }
          break
        }
      }
    }' "$f" > "$f.trim" && mv "$f.trim" "$f"
  echo "  [trim] $(basename "$f")：$size → $(stat -c '%s' "$f") 字节"
}

# ---------------- 断言与报告 ----------------
PASS_N=0; FAIL_N=0; WARN_N=0; INFO_N=0; INVALID_N=0
SUMMARY=()   # "id|结果|描述"

_report() { # 结果 id 描述 证据文本
  local res="$1" id="$2" desc="$3" ev="${4:-}"
  ev=${ev//$'\r'/}
  case "$res" in
    PASS) PASS_N=$((PASS_N+1));; FAIL) FAIL_N=$((FAIL_N+1));;
    WARN) WARN_N=$((WARN_N+1));; INFO) INFO_N=$((INFO_N+1));;
    INVALID) INVALID_N=$((INVALID_N+1));;
  esac
  SUMMARY+=("$id|$res|$desc")
  {
    echo ""
    echo "### [$res] $id $desc"
    if [ -n "$ev" ]; then echo '```'; echo "$ev"; echo '```'; fi
  } >> "$REPORT"
  printf '[%s] %-4s %s\n' "$res" "$id" "$desc"
}

check_contains() { # id 描述 文件 固定串
  local id="$1" desc="$2" file="$3" pat="$4" line
  line=$(grep -m1 -F "$pat" "$file" 2>/dev/null)
  if [ -n "$line" ]; then _report PASS "$id" "$desc" "$line"
  else _report FAIL "$id" "$desc" "未出现：$pat（$(basename "$file")）"; fi
}

check_absent() { # id 描述 文件 固定串 → 不得出现
  local id="$1" desc="$2" file="$3" pat="$4" n ev
  n=$(grep -cF "$pat" "$file" 2>/dev/null); n=${n:-0}
  if [ "$n" = "0" ]; then _report PASS "$id" "$desc" "（全程未出现 $pat）"
  else ev=$(printf '出现 %s 次\n%s' "$n" "$(grep -m1 -F "$pat" "$file")"); _report FAIL "$id" "$desc" "$ev"; fi
}

check_stage() { # id 描述 文件 成功标记 失败标记（成功标记出现且失败标记零次）
  local id="$1" desc="$2" file="$3" ok="$4" bad="$5" okline n
  okline=$(grep -m1 -F "$ok" "$file" 2>/dev/null)
  n=$(grep -cF "$bad" "$file" 2>/dev/null); n=${n:-0}
  if [ -n "$okline" ] && [ "$n" = "0" ]; then _report PASS "$id" "$desc" "$okline"
  elif [ -n "$okline" ]; then _report FAIL "$id" "$desc" "完成但出现失败标记×$n：$bad"
  else _report FAIL "$id" "$desc" "未出现成功标记：$ok（可能卡在前一阶段）"; fi
}

report_info() { _report INFO "$1" "$2" "${3:-}"; }
report_warn() { _report WARN "$1" "$2" "${3:-}"; }

# ---------------- 日志窗口断言（F 段交互流用·2026-10-24 第2阶段） ----------------
# 行基线机制：expect_* 只认「基线之后」的新日志，防旧日志误命中；
# 每次命中后基线自动推进到当前行数，同名断言（如两次 btn-start）互不串线。
CUR_LOG=""; CUR_BASE=0
LAST_MATCH=""; LAST_MISS=""

seg_log() { # 切换当前观察日志并落基线
  CUR_LOG="$1"
  CUR_BASE=$(wc -l < "$CUR_LOG" 2>/dev/null || echo 0)
}
log_rebase() { CUR_BASE=$(wc -l < "$CUR_LOG" 2>/dev/null || echo 0); }
log_slice() { tail -n +$((CUR_BASE+1)) "$CUR_LOG" 2>/dev/null; }

# 期望正则出现在基线之后（$1=ERE 模式 $2=超时秒）→ 0；LAST_MATCH=命中行
# 基线推进钉在「命中行」而非文件尾——命中行与检查时刻之间新到的行（如点击回响 1ms 后的
# 屏幕切换行）不得被吞，否则后续断言永远找不到（2026-10-24 F1 竞态实录）。
expect_in_log() {
  local pat="$1" tmo="${2:-15}" t=0 hit abs line
  while :; do
    hit=$(log_slice | grep -n -m1 -E "$pat" | cut -d: -f1)
    if [ -n "$hit" ]; then
      abs=$((CUR_BASE+hit))
      line=$(tail -n +$abs "$CUR_LOG" 2>/dev/null | head -1)
      LAST_MATCH="$line"
      CUR_BASE=$abs
      return 0
    fi
    [ "$t" -ge "$tmo" ] && break
    sleep 1; t=$((t+1))
  done
  LAST_MISS="期望未出现：$pat（等待 ${tmo}s）｜实际（当前窗口末 12 行）：$(log_slice | tail -n 12 | tr '\n' '⏎')"
  return 1
}

# 屏幕切换断言：`[UINav] Show<Name>` 或 `[UINav] Back（弹 Name）`
expect_screen() {
  local name="$1" tmo="${2:-15}"
  expect_in_log "\[UINav\] Show<${name}>|\[UINav\] Back（弹 ${name}）" "$tmo"
}

# 点击回响断言：`[UI点击] <name>`（行尾锚定，防 btn-back 误吃 btn-lobby-back 类前缀）
expect_click() {
  local name="$1" tmo="${2:-15}"
  expect_in_log "\[UI点击\] ${name}\$|\[UI点击\] ${name}[^0-9a-zA-Z_-]" "$tmo"
}

# 失败存档（只存档不作判据）：dumpsys window + 截图
fail_archive() {
  local tag="$1"
  [ -n "${OUT:-}" ] || return 0
  adb_shell "dumpsys window" > "$OUT/fail_${tag}.window.txt" 2>/dev/null
  "$ADB" -s "$DEVICE" exec-out screencap -p > "$OUT/fail_${tag}.png" 2>/dev/null
  echo "  [fail-archive] $OUT/fail_${tag}.{window.txt,png}"
}

# 点击→回响成对断言：$1=坐标键 $2=回响按钮名（缺省=$1） $3=超时秒
# 失败时输出期望/实际/耗时并落存档。TAPS_ISSUED 记账供污染检测比对。
TAPS_ISSUED=()
tap_expect() {
  local key="$1" ui="${2:-$1}" tmo="${3:-15}" t0 t1
  t0=$(now_ms)
  log_rebase                       # 基线=点击前（防旧日志误命中）
  tap_ui "$key" || { LAST_MISS="tap_ui 无坐标：$key"; fail_archive "tap_${key}"; return 1; }
  TAPS_ISSUED+=("$ui")
  if expect_click "$ui" "$tmo"; then
    t1=$(now_ms)
    echo "  [expect] [UI点击] $ui 回响 OK（$((t1-t0))ms）"
    return 0
  fi
  t1=$(now_ms)
  echo "  [expect] [UI点击] $ui 回响缺失（耗时 $((t1-t0))ms）"
  fail_archive "tap_${key}"
  return 1
}

# ---------------- 污染检测（2026-10-24 第2阶段） ----------------
# 窗口内出现：①非脚本发起的 [UI点击]（比对 TAPS_ISSUED 身份）
#             ②非预期 [UINav] 屏幕切换（比对 SCREENS_EXPECTED）
#             ③外部 force-stop（fully stop … by user request）/ 包替换安装
# 命中 → POLLUTE_EVIDENCE 非空；调用方判段 INVALID（不判 PASS/FAIL，提示重跑）。
SCREENS_EXPECTED=()
scan_pollution() { # $1=起始行(0=全文件) $2=日志文件
  local from="$1" file="$2" hits="" line ui ok
  while IFS= read -r line; do
    [ -z "$line" ] && continue
    case "$line" in
      *"fully stop com.WwtT.Tide"*|*"reported as REPLACED"*)
        hits+="外部干预：$line"$'\n' ;;
      *"[UI点击]"*)
        ui=$(printf '%s' "$line" | sed 's/.*\[UI点击\] //' | awk '{print $1}')
        ok=0
        for e in "${TAPS_ISSUED[@]:-}"; do [ "$e" = "$ui" ] && { ok=1; break; }; done
        [ "$ok" = "0" ] && hits+="非脚本点击（$ui）：$line"$'\n' ;;
      *"[UINav]"*)
        ok=0
        for e in "${SCREENS_EXPECTED[@]:-}"; do
          case "$line" in *"Show<$e>"*|*"Back（弹 $e）"*|*"Replace<$e>"*) ok=1; break;; esac
        done
        [ "$ok" = "0" ] && hits+="非脚本屏幕切换：$line"$'\n' ;;
    esac
  done < <(tail -n +$((from+1)) "$file" 2>/dev/null)
  POLLUTE_EVIDENCE="$hits"
  [ -z "$hits" ]
}

# ---------------- meminfo 读取（B4·2026-10-24 修：Android 15 口径） ----------------
# Android 13+ 单包 dumpsys meminfo 的总行是 "TOTAL PSS:"/"TOTAL RSS:"，
# 旧口径 grep "TOTAL" 在 15 上取不到；进程已死时 dumpsys 输出为空——先判活。
read_pss() { # $1=输出存档文件 → stdout=总行（空=读取失败）
  local out="$1" mem
  [ -n "${OUT:-}" ] && adb_shell "dumpsys meminfo $PKG" > "$out" 2>/dev/null
  mem=$(grep -m1 -E "TOTAL PSS:|TOTAL RSS:" "$out" 2>/dev/null)
  [ -z "$mem" ] && mem=$(grep -m1 -E "TOTAL" "$out" 2>/dev/null)
  printf '%s' "$mem"
}
