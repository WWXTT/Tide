#!/usr/bin/env bash
# ============================================================================
# 联机必测点（2026-10-28 新出包自动化必测项；独立入口，稳定后并入 run.sh N 段）
#
# 拓扑（无编辑器依赖，新包装机后即可跑）：
#   TideServer match  --port 28000   —— 无头房主（NetSessionServer + LanRoomBeacon 局域网发现信标）
#   TideServer aiguest --port 28000  —— 回环 AI 客人（占一座位，替真人房主出牌）
#   MuMu APK 客人                     —— 自动发现直入（LanRoomScanner 探 10.0.2.2，slirp NAT 达宿主回环）
#
# 判据（全程 logcat/子进程 stdout 双侧取证，禁截图）：
#   N1  match READY 行（无头房主监听就绪）
#   N2  设备侧 [Lobby] 发现房间 + 已连接等待进房（自动发现直入，无手输 IP）
#   N3  设备侧 已提交卡组「…·预组」且无 DeckSubmit 拒绝（随机卡组在主表清空态为空集，必选预组）
#   N4  match stdout PHASE Playing（双座位齐开局）
#   N5  设备侧 [UINav] Show<BattleScreen>（网络分支进战斗屏）
#   N6  match stdout PHASE Finished（设备侧客人只结束回合，AI 打到终局）
#   N7  全程无 FATAL/原生崩溃
#
# 用法：bash E2E/online.sh
# 退出码：0=全过 1=有 FAIL 2=环境故障
# ============================================================================
set -u
cd "$(dirname "$0")"
. ./common.sh
. ./coords.sh
find_adb || die "未找到 adb"

TS="C:/Users/Administrator/Desktop/UnityProject/Tide/TideServer/bin/Debug/net10.0/TideServer.exe"
STAMP=$(date +%Y%m%d-%H%M%S)
REPORT_DIR="$E2E_ROOT/reports/online-$STAMP"
mkdir -p "$REPORT_DIR"
MATCH_LOG="$REPORT_DIR/match.log"
AIGUEST_LOG="$REPORT_DIR/aiguest.log"
DEV_LOG="$REPORT_DIR/device.log"

PASS=0; FAIL=0
ok()  { echo "[PASS] $*"; PASS=$((PASS+1)); }
bad() { echo "[FAIL] $*"; FAIL=$((FAIL+1)); }
note(){ echo "[INFO] $*"; }

# ---- 0. 环境：模拟器在线 ----
adb connect "$DEVICE" >/dev/null 2>&1
"$ADB" -s "$DEVICE" get-state >/dev/null 2>&1 || die "$DEVICE 不在线（先 bash E2E/net.sh boot A）"

# ---- 1. 无头房主（信标随房起） ----
rm -f "$MATCH_LOG" "$AIGUEST_LOG"
"$TS" match --port 28000 > "$MATCH_LOG" 2>&1 &
MATCH_PID=$!
for _ in $(seq 1 20); do grep -q "^READY" "$MATCH_LOG" 2>/dev/null && break; sleep 1; done
grep -q "^READY" "$MATCH_LOG" && ok "N1 match READY：$(head -1 "$MATCH_LOG")" || { bad "N1 match 未就绪"; cat "$MATCH_LOG" 2>/dev/null; }

# ---- 2. 设备侧：存档注入→冷启→主菜单→自动发现加入 ----
adb_shell "am force-stop $PKG" >/dev/null 2>&1; sleep 1
adb_shell "mkdir -p /sdcard/Android/data/$PKG/files" >/dev/null 2>&1
MSYS_NO_PATHCONV=1 "$ADB" -s "$DEVICE" push "$(cygpath -w "$E2E_ROOT/assets/TutorialProgress.json")" "/sdcard/Android/data/$PKG/files/TutorialProgress.json" >/dev/null || die "存档注入失败"
start_capture "$DEV_LOG"
adb_shell "am start -n $ACT" >/dev/null
wait_for "[Launch] 启动完成" "$DEV_LOG" 60 || die "冷启未完成"

tap_expect btn-match 15 || bad "btn-match 无回响"
tap_expect btn-join 15 || bad "btn-join 无回响"
if wait_for "发现房间" "$DEV_LOG" 25; then ok "N2a 自动发现房间"; else bad "N2a 未发现房间（信标/扫描链路）"; fi
if wait_for "已连接，等待进房" "$DEV_LOG" 25; then ok "N2b 直入成功"; else bad "N2b 直入失败"; fi

# ---- 3. AI 座位入座（预组提交前后皆可；晚于设备侧避免抢座位锁） ----
"$TS" aiguest --port 28000 > "$AIGUEST_LOG" 2>&1 &
AIGUEST_PID=$!

# ---- 4. 选预组并提交（随机卡组=主表清空态空集，必须轮选预组） ----
tap_expect btn-deck-pick 15 || bad "btn-deck-pick 无回响"
tap_expect btn-deck-submit 15 || bad "btn-deck-submit 无回响"
sleep 3
if tail -200 "$DEV_LOG" | grep -F "已提交卡组" | tail -1 | grep -q "预组"; then
  ok "N3 已提交预组卡组"
else
  bad "N3 卡组提交异常（非预组或被拒）"
  grep -E "\[Lobby\]" "$DEV_LOG" | tail -6
fi

# ---- 5. 开局与终局 ----
for _ in $(seq 1 40); do grep -q "^PHASE Playing" "$MATCH_LOG" && break; sleep 1; done
grep -q "^PHASE Playing" "$MATCH_LOG" && ok "N4 双座位齐开局（PHASE Playing）" || bad "N4 未开局（match：$(grep '^PHASE' "$MATCH_LOG" | tail -1)）"
if wait_for "Show<BattleScreen>" "$DEV_LOG" 40; then ok "N5 设备侧进战斗屏"; else bad "N5 未进战斗屏"; fi

# 设备侧只结束回合（AI 对手打到终局；btn-endturn 每轮一次，40 轮兜底）
for _turn in $(seq 1 40); do
  grep -q "^PHASE Finished" "$MATCH_LOG" && break
  sleep 6
  tap_ui btn-endturn >/dev/null 2>&1 || true
done
grep -q "^PHASE Finished" "$MATCH_LOG" && ok "N6 对局到终局（PHASE Finished）" || bad "N6 未到终局（超时；aiguest 尾行：$(tail -2 "$AIGUEST_LOG" 2>/dev/null | head -1)）"

# ---- 6. 健康与收尾 ----
if ! grep -qE "FATAL EXCEPTION|E/CRASH|Fatal signal|has died" "$DEV_LOG"; then
  ok "N7 全程无崩溃"
else
  bad "N7 检出崩溃"
fi

stop_capture
kill "$MATCH_PID" "$AIGUEST_PID" 2>/dev/null
wait "$MATCH_PID" 2>/dev/null; wait "$AIGUEST_PID" 2>/dev/null
adb_shell "am force-stop $PKG" >/dev/null 2>&1

echo "================ online 段完成 = PASS=$PASS FAIL=$FAIL ================"
echo "报告目录：$REPORT_DIR"
[ "$FAIL" -eq 0 ] && exit 0 || exit 1
