#!/usr/bin/env bash
# ============================================================================
# 联机冒烟门禁（2026-11-09 阶段D，«AOT泛型门禁_实施计划» §2D）
#
# 定位：出包成功后的下一步——本脚本红 = 该包不可交付（不接受口头豁免）。
# 复用 E2E/online.sh 的拓扑与取证手段（TideServer match 无头房主 + aiguest 回环客人 +
# MuMu APK 自动发现直入；logcat/stdout 双侧取证，禁截图），判据只保留最小集：
#   G1  match READY（无头房主监听就绪）
#   G2  设备侧 [Lobby] 已连接，等待进房（联机 TCP 直入成功——RunOnThreadPool<bool> 类
#       AOT 缺口在此现形；A2 修复后若仍失败，[Lobby] 连接异常/[FirstChance] AOT 行给出真因）
#   G3  match PHASE Playing（双座位齐开局——卡组提交/入座流程走通但不单独判分）
#   G4  全程无 FATAL/原生崩溃
# 不判：N2a 自动发现、N3 预组文案、N5 战斗屏、N6 终局（完整回归用 bash E2E/online.sh）。
#
# 用法：bash E2E/gate_online.sh
# 退出码：0=可交付 1=不可交付 2=环境故障
# ============================================================================
set -u
cd "$(dirname "$0")"
. ./common.sh
. ./coords.sh
find_adb || die "未找到 adb"

TS="C:/Users/Administrator/Desktop/UnityProject/Tide/TideServer/bin/Debug/net10.0/TideServer.exe"
STAMP=$(date +%Y%m%d-%H%M%S)
REPORT_DIR="$E2E_ROOT/reports/gate-online-$STAMP"
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

# ---- 1. 无头房主（信标随房起）→ G1 ----
rm -f "$MATCH_LOG" "$AIGUEST_LOG"
"$TS" match --port 28000 > "$MATCH_LOG" 2>&1 &
MATCH_PID=$!
for _ in $(seq 1 20); do grep -q "^READY" "$MATCH_LOG" 2>/dev/null && break; sleep 1; done
grep -q "^READY" "$MATCH_LOG" && ok "G1 match READY：$(head -1 "$MATCH_LOG")" || bad "G1 match 未就绪"

# ---- 2. 设备侧：存档注入→冷启→主菜单→自动发现加入 → G2 ----
adb_shell "am force-stop $PKG" >/dev/null 2>&1; sleep 1
adb_shell "mkdir -p /sdcard/Android/data/$PKG/files" >/dev/null 2>&1
MSYS_NO_PATHCONV=1 "$ADB" -s "$DEVICE" push "$(cygpath -w "$E2E_ROOT/assets/TutorialProgress.json")" "/sdcard/Android/data/$PKG/files/TutorialProgress.json" >/dev/null || die "存档注入失败"
start_capture "$DEV_LOG"
adb_shell "am start -n $ACT" >/dev/null
wait_for "[Launch] 启动完成" "$DEV_LOG" 60 || die "冷启未完成"

tap_expect btn-match 15 || bad "btn-match 无回响"
tap_expect btn-join 15 || bad "btn-join 无回响"
wait_for "发现房间" "$DEV_LOG" 25 || note "未发现房间（不影响 G2：自动发现失败后仍可手输路径）"
if wait_for "已连接，等待进房" "$DEV_LOG" 30; then
  ok "G2 设备侧已连接等待进房"
else
  bad "G2 联机直入失败"
  note "取证：[Lobby] 尾行=$(grep -E '\[Lobby\]' "$DEV_LOG" | tail -4 | tr '\n' ' | ')"
  note "取证：[FirstChance] $(grep -c '\[FirstChance\] AOT' "$DEV_LOG") 条 AOT 异常（>0=热更踩 AOT 泛型缺口，查 Config/aot_generic_baseline.txt 与锚点）"
fi

# ---- 3. AI 座位入座 + 预组提交 → G3（开局判分，提交流程只走不判）----
"$TS" aiguest --port 28000 > "$AIGUEST_LOG" 2>&1 &
AIGUEST_PID=$!
tap_expect btn-deck-pick 15 || note "btn-deck-pick 无回响"
tap_expect btn-deck-submit 15 || note "btn-deck-submit 无回响"
for _ in $(seq 1 40); do grep -q "^PHASE Playing" "$MATCH_LOG" && break; sleep 1; done
grep -q "^PHASE Playing" "$MATCH_LOG" && ok "G3 双座位齐开局（PHASE Playing）" || bad "G3 未开局（match：$(grep '^PHASE' "$MATCH_LOG" | tail -1)）"

# ---- 4. 健康与收尾 → G4 ----
if ! grep -qE "FATAL EXCEPTION|E/CRASH|Fatal signal|has died" "$DEV_LOG"; then
  ok "G4 全程无崩溃"
else
  bad "G4 检出崩溃"
fi

stop_capture
kill "$MATCH_PID" "$AIGUEST_PID" 2>/dev/null
wait "$MATCH_PID" 2>/dev/null; wait "$AIGUEST_PID" 2>/dev/null
adb_shell "am force-stop $PKG" >/dev/null 2>&1

echo "================ gate-online 完成 = PASS=$PASS FAIL=$FAIL ================"
echo "报告目录：$REPORT_DIR"
if [ "$FAIL" -eq 0 ]; then
  echo "[GATE] 绿——本包联机冒烟通过（可交付）"
  exit 0
fi
echo "[GATE] 红——本包不可交付（不接受口头豁免；完整回归 bash E2E/online.sh 定位）"
exit 1
