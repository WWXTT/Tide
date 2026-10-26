#!/usr/bin/env bash
# ============================================================================
# Tide E2E 坐标层 —— 设计坐标(1920×1080) → 设备像素，adb input tap 驱动
#
# 坐标来源纪律（2026-10-24 建）：
#   * 一律离线推导自资产/代码正本，禁止截图猜坐标：
#     - 预制体按钮 = Assets/Art/Prefab/*.prefab 经 UnityMCP execute_code 在
#       附加空场景实例化 + LayoutGroup 直调(CalculateLayoutInput/SetLayout)后
#       GetWorldCorners 读数（VLG 运行时排布，序列化 anchoredPosition 不可信）
#     - 代码面板按钮 = 源码布局常量推算（PrivacyConsentDialog）
#     - 主菜单按钮位置随教学门槛态变化（RefreshTutorialGates 显隐 + VLG 重排），
#       本表登记「三课全胜态」（F 段注入存档后的实况）
#   * 每个坐标首次使用必须被 `[UI点击] <name>` 回响证实（tap_expect），无回响=坐标失效
#     → 先重推导再重跑，不许调参试错
#   * 画布 = CanvasScaler 1920×1080 Expand（MuMu 2560×1440 恰 16:9 → 恒 1920×1080，
#     缩放=设备短边/1080；画布原点=屏幕左上，x_dev=dx*scale, y_dev=dy*scale）
# ============================================================================
UI_SCALE=""
UI_DEV_W=0; UI_DEV_H=0; UI_ORIENT="?"

# 坐标登记表：键=脚本用名 → "dx,dy"（设计坐标系，左上原点）
declare -A UI_COORDS=(
  # ---- MainUI.prefab（主菜单·三课全胜态：match/deck/card/effect/teststart 五钮纵排）----
  # 2026-10-25 本地对战（btn-battle）退役后重推导（LoadPrefabContents+VLG 直调）：
  # menu VLG spacing=0·forceExpand·childControl=false，高 734 恒定=激活钮均分 → 五钮各 146.8。
  # 注意 btn-match 在 prefab 默认 activeSelf=0（运行时 done 态激活）——推导须显式 SetActive。
  [btn-match]="960,313"        # VLG 第1位（联机入口·done 态激活）
  [btn-deck]="960,460"         # VLG 第2位 —— F1 主角
  [btn-card]="960,607"         # VLG 第3位
  [btn-effect]="960,754"       # VLG 第4位
  [btn-teststart]="960,901"    # VLG 第5位 —— F2 主角
  # ---- DeckUI.prefab（卡组构建屏）----
  [btn-deck-back]="60,48"      # btn-back 左上角返回（88×44）
  # ---- dummy-options.prefab（木桩选项面板·盒 660×460 居中）----
  [btn-dummy-start]="822,721"  # 「开始」确认（120×60）
  [btn-dummy-back]="1080,721"  # 「返回主菜单」（172×60）
  # ---- dummy-deck.prefab（木桩选卡窗·盒 660 宽 CSF 高 438.8 中锚 y+60 → 中心(960,480)）----
  # 2026-10-24 第3阶段修正：y 原登记 857=手算错误（盒底 699.4 之外，点击落遮罩零回响——
  # run 20261024-233645 F2 步骤4 实锤）。复算法：盒 VLG(pad 20/40·spacing 12·ctrlH=False)
  # 逐行排布 + 行内 anchors(0,1)+aPos 换算；同法复算 dummy-options btn-start=(822,720.7)
  # 与实测有效值 (822,721) 严合（差 0.3px）→ 方法自证后应用。
  [btn-deckpanel-start]="822,638"  # 「开战」确认（120×60·行内 aPos -54.9,-28.7）
  [btn-deckpanel-back]="1080,638"  # 「返回主菜单」（172×60·行内 aPos 203.1,-28.7）
  [drp-deck-my]="1002,448"         # 我方卡组下拉（380×40）
  [drp-deck-ai]="1002,572"         # 对方卡组下拉（380×40）
  [btn-deckpanel-diff]="1003,510"  # 对手难度循环钮（380×40）
  # ---- PrivacyConsentDialog.cs 代码布局·execute_code 实测（2026-10-24）----
  # 真实拉起 Open() 于 1920×1080 探针画布全趟布局后读数：panel 1060×860 居中；VLG UpperLeft
  # → bar(616×44=300+16+300) 左贴 padded 区（x∈[450,1066]·y 中心 930）；btn-agree=(600,930)。
  # 教训×2：① y 坐标系方向曾算反（150→应 930）；② bar 并非居中（UpperLeft 左贴）——手算 x=802
  # 误中 btn-refuse(x∈[766,1066]) 触发「不同意并退出」。代码面板坐标必须实测，不许手算。
  [btn-agree]="600,930"
)

# 从 wm size 解析缩放（横屏游戏：宽=物理长边）。失败回落 2560×1440 并告警。
ui_resolve_scale() {
  local size w h orient
  size=$(adb_shell "wm size" | grep -m1 "Physical size:")
  w=$(printf '%s\n' "$size" | sed -n 's/.*Physical size: \([0-9]*\)x\([0-9]*\).*/\1/p')
  h=$(printf '%s\n' "$size" | sed -n 's/.*Physical size: \([0-9]*\)x\([0-9]*\).*/\2/p')
  if [ -z "$w" ] || [ -z "$h" ] || [ "$w" -eq 0 ] || [ "$h" -eq 0 ]; then
    echo "[E2E][coords] wm size 解析失败（$size）——回落假设 2560×1440" >&2
    w=2560; h=1440
  fi
  if [ "$w" -lt "$h" ]; then local t=$w; w=$h; h=$t; fi   # 横屏
  orient=$(adb_shell "dumpsys input" | grep -m1 "SurfaceOrientation" | grep -oE '[0-9]+$' | tr -d '\r')
  UI_ORIENT="${orient:-?}"
  UI_DEV_W=$w; UI_DEV_H=$h
  UI_SCALE=$(awk -v w="$w" -v h="$h" 'BEGIN{s1=w/1920; s2=h/1080; printf "%.5f", (s1<s2?s1:s2)}')
  echo "[env] 画布缩放：物理 ${w}x${h}（SurfaceOrientation=$UI_ORIENT）→ scale=$UI_SCALE（设计 1920×1080）"
}

# 设计→设备像素
ui_map_x() { awk -v d="$1" -v s="$UI_SCALE" 'BEGIN{printf "%d", int(d*s+0.5)}'; }
ui_map_y() { awk -v d="$1" -v s="$UI_SCALE" 'BEGIN{printf "%d", int(d*s+0.5)}'; }

# 点击（键=UI_COORDS 登记名）。成功后 TAP_DESC=「键 design(x,y)→device(x,y)」
tap_ui() {
  local name="$1" c dx dy x y
  [ -z "$UI_SCALE" ] && ui_resolve_scale
  c="${UI_COORDS[$name]:-}"
  if [ -z "$c" ]; then echo "[E2E][tap_ui] 未登记坐标：$name" >&2; return 1; fi
  dx=${c%,*}; dy=${c#*,}
  x=$(ui_map_x "$dx"); y=$(ui_map_y "$dy")
  adb_shell "input tap $x $y" >/dev/null
  TAP_DESC="$name design($dx,$dy)→device($x,$y)"
  echo "  [tap] $TAP_DESC"
}
