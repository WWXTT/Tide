#!/usr/bin/env bash
# ============================================================================
# 联机流驱动原语（阶段1 半自动用；流程稳定后固化进 run.sh N 段）
# 每次调用独立（无跨调用状态）：判据走 logcat 缓冲 dump（-d 非破坏式），
# 点击前后按行数基线差分，防旧日志误报。
#
# 用法：bash E2E/net.sh <命令> <A|B> [参数...]
#   boot  A|B            — 确保模拟器实例在线（B 不存在时用 MuMuManager 拉起实例1）
#   launch/stop A|B      — 启动/强停 Tide
#   save-tutorial A|B    — 注入三课全胜教学存档（须先 stop）
#   tap  A|B x y         — 点击
#   text A|B "字符串"     — 输入文本（先 tap 输入框聚焦）
#   key  A|B <keycode>   — 按键（4=BACK）
#   echo A|B "标记" [行基线] — dump 缓冲 grep 标记（打最后一条命中）
#   tap-echo A|B x y "标记" [超时秒] — 点击并轮询等日志回响（E2E 铁律：操作→回响成对）
#   addr A|B             — 取 [Lobby] 本机地址 行（建房后查房主 IP）
#   pidof A|B            — Tide 进程号
# ============================================================================
set -u
cd "$(dirname "$0")"
. ./common.sh
find_adb || die "未找到 adb"

CMD="${1:-}"; WHO="${2:-}"
case "$WHO" in
  A) DEV="127.0.0.1:16384"; IDX=0;;
  B) DEV="127.0.0.1:16416"; IDX=1;;
  *) die "第二参数须为 A|B";;
esac
adbsh() { "$ADB" -s "$DEV" shell "$@" 2>/dev/null | tr -d '\r'; }
ready() { "$ADB" connect "$DEV" >/dev/null 2>&1; "$ADB" devices | grep -qE "^${DEV}[[:space:]]+device$"; }

case "$CMD" in
  boot)
    if ! ready; then
      if [ -f "$MUMU_MANAGER" ]; then
        ( cd "$(dirname "$MUMU_MANAGER")" && ./MuMuManager.exe control -v "$IDX" launch >/dev/null 2>&1 )
        local_i=0
        while [ $local_i -lt 36 ]; do
          sleep 5; local_i=$((local_i+5)); ready && break
        done
      fi
    fi
    ready || die "$DEV 不在线"
    echo "OK $DEV $(adbsh getprop ro.build.version.release)"
    ;;
  launch) ready || die "$DEV 不在线"; adbsh "am start -n $ACT" >/dev/null; echo "launched on $DEV";;
  stop)   ready || die "$DEV 不在线"; adbsh "am force-stop $PKG" >/dev/null; echo "stopped on $DEV";;
  save-tutorial)
    ready || die "$DEV 不在线"
    adbsh "am force-stop $PKG" >/dev/null; sleep 1
    # 本地路径转 Windows 形式 + MSYS_NO_PATHCONV=1 防 /sdcard 远端参数被改写（两者缺一不可）
    local_win=$(cygpath -w "$E2E_ROOT/assets/TutorialProgress.json")
    MSYS_NO_PATHCONV=1 "$ADB" -s "$DEV" push "$local_win" "/sdcard/Android/data/$PKG/files/TutorialProgress.json" >/dev/null || die "存档 push 失败"
    echo "教程存档已注入 $DEV"
    ;;
  tap)  ready || die "$DEV 不在线"; adbsh "input tap $3 $4"; echo "tap $3 $4 @ $DEV";;
  text) ready || die "$DEV 不在线"; adbsh "input text '$3'"; echo "text '$3' @ $DEV";;
  key)  ready || die "$DEV 不在线"; adbsh "input keyevent $3"; echo "key $3 @ $DEV";;
  echo)
    ready || die "$DEV 不在线"
    "$ADB" -s "$DEV" logcat -d -v time 2>/dev/null | grep -F "$3" | tail -3
    ;;
  tap-echo)
    ready || die "$DEV 不在线"
    PAT="$5"; TMO="${6:-15}"
    BASE=$("$ADB" -s "$DEV" logcat -d -v time 2>/dev/null | wc -l)
    adbsh "input tap $3 $4" >/dev/null 2>&1
    W=0
    while [ "$W" -lt "$TMO" ]; do
      HIT=$("$ADB" -s "$DEV" logcat -d -v time 2>/dev/null | tail -n +$((BASE+1)) | grep -F "$PAT" | tail -1)
      if [ -n "$HIT" ]; then echo "$HIT"; exit 0; fi
      sleep 2; W=$((W+2))
    done
    echo "TIMEOUT：tap($3,$4) 后 ${TMO}s 未见回响：$PAT" >&2; exit 1
    ;;
  addr)
    "$ADB" -s "$DEV" logcat -d -v time 2>/dev/null | grep -F "[Lobby] 本机地址" | tail -1
    ;;
  pidof) adbsh "pidof $PKG";;
  *) die "未知命令：$CMD（见文件头注释）";;
esac
