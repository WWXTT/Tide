"""开训前硬闸（A1，2026-11-24 回到一期训练目标）：只做「检查 + 落环境指纹」，不做守护/重启。

检查项（任一失败 → exit 1，拒绝开训）：
  1. C# `TideObservation.LayoutVersion/NCard/NGlobal/NAction/MaxCardsTotal`
     与 Python `tide_features.LAYOUT_VERSION/N_CARD_FEATURES/N_GLOBAL_FEATURES/N_ACTION_FEATURES/MAX_CARDS` 一致；
  2. 主仓 ↔ 训练副本：关键代码/数据文件 SHA256 逐对相同；
  3. `BASELINE_ERA` 与 `SCRIPT_MATRIX_BASELINES` 同源可读（判据常量存在）；
  4. 无在跑的训练/Unity batchmode（`train_tide_complete.py` / `TideHeadlessServer`）；
  5. 可选 `--opponent` 必须为 `simpleai`（一期只认 vs 脚本口径）。

通过后把指纹写到 `--out`（默认 `preflight.json`）：
  layout_version / n_card / n_global / n_action / max_cards / baseline_era /
  card_table_sha256 / copy_hash_ok / timestamp / opponent。

用法（副本 tide_rl 目录，与训练同解释器）：
  python preflight_phase1.py                       # 体检 + 写 preflight.json
  python preflight_phase1.py --opponent simpleai   # 一期口径（非 simpleai 直接失败）
  python preflight_phase1.py --main-root D:\\Tide --copy-root D:\\Tide_train
退出码：0=通过；1=不通过；2=使用错误。
"""
import argparse
import hashlib
import json
import re
import subprocess
import sys
import time
from pathlib import Path

# ---------------------------- 常量解析 ----------------------------

CS_KEYS = {
    "LayoutVersion": "layout_version",
    "NCard": "n_card",
    "NGlobal": "n_global",
    "NAction": "n_action",
    "MaxCardsTotal": "max_cards",
}

PY_KEYS = {
    "LAYOUT_VERSION": "layout_version",
    "N_CARD_FEATURES": "n_card",
    "N_GLOBAL_FEATURES": "n_global",
    "N_ACTION_FEATURES": "n_action",
    "MAX_CARDS": "max_cards",
}

# 主仓 ↔ 副本必须逐字节一致的文件（训练读的就是这些）
SYNC_FILES = [
    "Assets/Scripts/HotUpdate/AI/NeuralEnv/TideObservation.cs",
    "Assets/Scripts/HotUpdate/AI/NeuralEnv/LegalActionEnumerator.cs",
    "Assets/Scripts/HotUpdate/AI/NeuralEnv/TideHeadlessDriver.cs",
    "Assets/Scripts/HotUpdate/CardCore/GameCore/GameCore.cs",
    "Assets/Scripts/HotUpdate/CardCore/Effects/HandLimitDiscardGate.cs",
    "Assets/Scripts/HotUpdate/CardCore/Effects/CostDerivation.cs",
    "Assets/Scripts/HotUpdate/CardCore/Effects/BranchEngines.cs",
    "Assets/Scripts/HotUpdate/CardCore/Attribute/EffectHandlerRegistry.cs",
    "Assets/Editor/验证/AI对战/TideHeadlessServer.cs",
    "Assets/Editor/验证/AI对战/TideObservationDeckVerifier.cs",
    "tide_rl/tide_features.py",
    "tide_rl/phase1_criterion.py",
    "tide_rl/train_tide_complete.py",
]


def _read(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig", errors="replace")


def parse_cs_constants(root: Path) -> dict:
    """解析 C# `public const int` 并求值，返回五个布局常量。

    C# 常量大量是表达式（`NCard = CountSlot + 1`、`MaxCardsTotal = FieldBase + FieldSlots`、
    `PosXSlot = ParamStart + AtomIdSlots * CardIdentityService.AtomParamDim`），单个文件解析不出来，
    所以：① 全仓扫 `public const int`；② 把 `类名.成员` 归一成 `成员`；③ 迭代用 ast 安全求值到收敛。
    """
    exprs = {}
    for cs in root.glob("Assets/**/*.cs"):
        try:
            text = _read(cs)
        except OSError:
            continue
        for name, expr in re.findall(r"public\s+const\s+int\s+(\w+)\s*=\s*([^;]+);", text):
            exprs.setdefault(name, expr.strip())

    def norm(expr: str) -> str:
        return re.sub(r"[A-Za-z_]\w*\s*\.\s*([A-Za-z_]\w*)", r"\1", expr)

    resolved = {}
    for _ in range(80):
        progressed = False
        for name, expr in exprs.items():
            if name in resolved:
                continue
            try:
                value = eval(norm(expr), {"__builtins__": {}}, dict(resolved))  # noqa: S307 — 仅常量算术
            except Exception:
                continue
            if isinstance(value, int):
                resolved[name] = value
                progressed = True
        if not progressed:
            break

    return {key: resolved.get(name) for name, key in CS_KEYS.items()}


def parse_py_constants(path: Path) -> dict:
    text = _read(path)
    out = {}
    for name, key in PY_KEYS.items():
        m = re.search(r"^%s\s*=\s*(-?\d+)" % re.escape(name), text, re.M)
        out[key] = int(m.group(1)) if m else None
    return out


def sha256(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def running_processes() -> list:
    """找在跑的训练（python train_tide_complete / batchmode TideHeadlessServer）。

    只认命令行，不按进程名——Unity 编辑器常开是正常状态，不算在跑训练。
    wmic 在新系统可能没有，取不到就返回空（上层只记 note，不误报）。
    """
    hits = []
    try:
        out = subprocess.run(
            ["wmic", "process", "where", "name='python.exe'", "get", "CommandLine"],
            capture_output=True, text=True, timeout=20, errors="replace").stdout
        for line in out.splitlines():
            if "train_tide_complete" in line:
                hits.append(line.strip()[:160])
    except Exception:
        pass
    try:
        out = subprocess.run(
            ["wmic", "process", "where", "name='Unity.exe'", "get", "CommandLine"],
            capture_output=True, text=True, timeout=20, errors="replace").stdout
        for line in out.splitlines():
            if "TideHeadlessServer" in line and "-batchmode" in line:
                hits.append(line.strip()[:160])
    except Exception:
        pass
    return hits


def main() -> int:
    ap = argparse.ArgumentParser(description="一期开训前硬闸（只检查，不守护）")
    ap.add_argument("--main-root", default=None, help="主仓根（默认=本脚本上两级）")
    ap.add_argument("--copy-root", default=None, help="训练副本根（默认=本脚本上两级）")
    ap.add_argument("--opponent", default=None, choices=("selfplay", "simpleai", "script"))
    ap.add_argument("--out", default="preflight.json")
    ap.add_argument("--skip-process-check", action="store_true")
    args = ap.parse_args()

    here = Path(__file__).resolve().parent
    main_root = Path(args.main_root).resolve() if args.main_root else here.parent
    copy_root = Path(args.copy_root).resolve() if args.copy_root else here.parent

    errors, notes = [], []

    # 1) 布局常量 C# ↔ Python
    cs = parse_cs_constants(copy_root)
    py = parse_py_constants(copy_root / "tide_rl/tide_features.py")
    dims = ("layout_version", "n_card", "n_global", "n_action", "max_cards")
    for d in dims:
        if cs.get(d) is None or py.get(d) is None:
            errors.append(f"{d}: 常量未解析到（C#={cs.get(d)} py={py.get(d)}）")
        elif cs[d] != py[d]:
            errors.append(f"{d}: C#={cs[d]} ≠ py={py[d]}")
    notes.append("布局 " + "/".join(str(cs.get(d)) for d in dims))

    # 2) 主仓 ↔ 副本
    diff = []
    for rel in SYNC_FILES:
        a, b = main_root / rel, copy_root / rel
        if not a.exists() or not b.exists():
            diff.append(rel + "（缺失）")
        elif sha256(a) != sha256(b):
            diff.append(rel)
    if diff:
        errors.append("主仓↔副本不一致：" + "；".join(diff))
    else:
        notes.append(f"副本同步 {len(SYNC_FILES)}/{len(SYNC_FILES)} SHA256 相同")

    # 3) 判据常量
    era = None
    crit = copy_root / "tide_rl/phase1_criterion.py"
    if not crit.exists():
        errors.append("phase1_criterion.py 不存在（判据未落地）")
    else:
        m = re.search(r'^BASELINE_ERA\s*=\s*"([^"]+)"', _read(crit), re.M)
        era = m.group(1) if m else None
        if not era:
            errors.append("BASELINE_ERA 未解析到")
        elif "SCRIPT_MATRIX_BASELINES" not in _read(crit):
            errors.append("SCRIPT_MATRIX_BASELINES 缺失")
    if era:
        notes.append(f"BASELINE_ERA={era}")

    # 4) 在跑进程
    if not args.skip_process_check:
        procs = running_processes()
        if procs:
            errors.append("检测到在跑的训练/Unity：" + " | ".join(procs[:3]))
        else:
            notes.append("无在跑训练/Unity")

    # 5) 口径
    if args.opponent and args.opponent != "simpleai":
        errors.append(f"opponent={args.opponent} ≠ simpleai（一期只认 vs 脚本口径）")

    # 6) 卡表指纹
    card_table = main_root / "Assets/Data/CardData/Cards.json"
    card_sha = sha256(card_table) if card_table.exists() else None

    print("===== 一期开训前硬闸 =====")
    for n in notes:
        print("  · " + n)
    if errors:
        for e in errors:
            print("  ✗ " + e)
        print("—— 不通过，拒绝开训")
        return 1

    fingerprint = {
        "timestamp": time.strftime("%Y-%m-%dT%H:%M:%S"),
        "layout_version": cs.get("layout_version"),
        "n_card": cs.get("n_card"),
        "n_global": cs.get("n_global"),
        "n_action": cs.get("n_action"),
        "max_cards": cs.get("max_cards"),
        "baseline_era": era,
        "card_table_sha256": card_sha,
        "main_root": str(main_root),
        "copy_root": str(copy_root),
        "opponent": args.opponent,
        "sync_files": len(SYNC_FILES),
    }
    out = Path(args.out)
    out.write_text(json.dumps(fingerprint, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"  ✓ 通过；指纹已写 {out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
