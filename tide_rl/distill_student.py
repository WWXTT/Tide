"""蒸馏学生（Stage 4·三专精→教师→蒸馏框架 §9，第二批工具 2026-11-26）。

两个子命令（本文件不含 PPO 微调与教师 KL 锚——延后项，损失只实现 KL + 价值 MSE）：

  collect —— DAgger（4b，--actor student，默认）/ 离线 BC（4a，--actor teacher）采集：
    python distill_student.py collect \
        --teachers logs/<green_run>/teacher_manifest.json,logs/<red_run>/teacher_manifest.json \
        [--student-run logs/tide_ppo_simpleai_student__42__<ts>] [--student-ckpt <msgpack>] \
        [--actor student|teacher] [--games-per-deck 150] [--seed 20261122] \
        [--round <n>] [--allow-missing] [--unity-path ...] [--project-path ...]
    · 教师清单逐个校验 sha256/layout_version/criterion_era，不符硬错；缺整套 deck 硬错
      （--allow-missing 才放行并跳过该 deck）；
    · 学生采样行动、自维护 rstate（DAgger）；教师链同观测流前向，每决策步记录
      obs（cards 191×158 / global 100 / 动作行截到 n_actions）+ 学生 incoming rstate(512)
      + 教师 logits(512) + 教师 value + n_actions + 选中行——τ/软化的位置在 train 侧；
    · 采集权重：缺省用**学生本 run 的 last**（第 2 轮起的 DAgger 必须采当前学生），
      首轮没有学生 last 才用共享 last（自动解析，见 resolve_shared_ckpt；--student-ckpt 可覆盖）；
    · 种子方案同 eval_matrix（对偏移×1000 + 座次×500 + 序号），与教师矩阵同批对局；
    · 分片 logs/<student_run>/distill/round_<n>/shard_<k>.npz（≤2000 步/片，压缩）
      + round_meta.json（逐局行为计数 + 学生胜率 + 教师清单 + 种子口径）。

  train —— 读分片训练学生：
    python distill_student.py train --shards <round目录|npz|glob,...> \
        [--student-run ...] [--init-params <msgpack> | --scratch] \
        [--channels 128] [--rnn-channels 512] [--lr 1e-4] [--epochs 3] [--batch 64] \
        [--alpha 1.0] [--beta 0.5] [--tau 1.0]
    · 按局切 5% 验证集；损失 = α·KL(softmax(teacher_logits/τ) ‖ softmax(student)) +
      β·(V_s−V_t)²（双方 logits 均已由网络按合法行屏蔽 −1e9，非法行 p_T≈0 不参与）；
    · optax adam + 全局范数裁剪 0.5（镜像主训练）；--lr 恒定；
    · 产出 distill_train.jsonl（train 每 update / val 每 epoch 的 kl、v_mse、loss）、
      last/params.msgpack + meta.json；写/merge 学生 run 的 config.json
      （opponent=simpleai / criterion_era / layout_version 照填——certify 硬闸可用；
      distill 块记 teachers/shards/α/β/τ）。
    · 初始化：缺省=**本 run 的 last**（第 2 轮起在同一 run 上续训，不覆盖前一轮成果）；
      没有 last 时才用共享 last（自动解析），--init-params/--scratch 可覆盖。
    · 学生 run 名必须是 tide_ppo_simpleai_student__42__<ts> 形态（certify 硬闸认这个；
      首次给这个形态的新名会直接建目录）；best 选点仍归 eval_matrix 18 格（§9.6），本工具不做胜率选点。

磁盘量级：~120–160KB/决策步（cards 118KB + 动作行 + rstate/logits 各 2KB），
600 局/轮 ≈ 数 GB——分片压缩落盘，不进 git。
"""
import argparse
import hashlib
import json
import os
import re
import struct
import time
from pathlib import Path

import numpy as np
import jax
import jax.numpy as jnp
import flax.serialization
import optax

from tide_agent import create_tide_agent, merge_params_by_shape
from tide_features import (
    init_rstate, sample_input, LAYOUT_VERSION,
    MAX_CARDS, N_CARD_FEATURES, N_GLOBAL_FEATURES,
    MAX_ACTIONS, N_ACTION_FEATURES, N_RNN_CHANNELS, ACTION_F_TYPE,
)
from tide_env import TideEnv
from phase1_criterion import BASELINE_ERA
try:
    from train_tide_complete import Args
except ModuleNotFoundError as _e:
    # 环境事实（2026-11-20）：主仓根没有 ygo-agent-main，ygoai 只在训练副本里；训练脚本
    # 自己只在 <工程>/ygo-agent-main 存在时插 sys.path。这里给可操作的提示，不抛裸 traceback。
    if _e.name == "ygoai":
        raise SystemExit(
            "[!] 缺 ygoai：主仓根没有 ygo-agent-main（训练副本才有）。\n"
            "    设 PYTHONPATH=<训练副本>\\ygo-agent-main 后重跑，或用 tide_rl\\run_distill.cmd。")
    raise
from eval_checkpoint import args_from_run
from eval_matrix import pair_seed_offset
from behavior_probe import behavior_of

DECKS = ("red", "green", "blue")
STEPS_PER_SHARD = 2000
# 学生缺省权重（2026-11-27 改）：不再写死一条会随环境失效的路径。解析顺序——
#   1) --student-ckpt / --init-params；
#   2) <student_run>/last/params.msgpack（DAgger/蒸馏第 2 轮起必须从学生自己的 last 继续，
#      否则每轮都从共享 base 重来，第 1 轮训到的权重会被覆盖——4 轮链路的核心前提）；
#   3) $TIDE_SHARED_CKPT；
#   4) 主仓 logs/tide_ppo_simpleai__42__*/last/params.msgpack；
#   5) 训练副本（C:\UnityProject\Tide_train*，make_train_copy.ps1 的 Dest 口径）
#      */tide_rl/logs/tide_ppo_simpleai__42__*/last/params.msgpack
#      （环境事实：主仓根没有 ygo-agent-main，共享基线 run 也只在副本里有）。
# 共享 last 是一期/旧代权重：动作维与当前树不同，必须经 merge_params_by_shape 并树（严格 from_bytes 会拒载）。
STUDENT_RUN_GLOB = "tide_ppo_simpleai_student__42__*"
SHARED_RUN_GLOB = "tide_ppo_simpleai__42__*"
CANONICAL_SHARED_RUN = "tide_ppo_simpleai__42__1795540922"  # 文档口径的共享基线 run（§5/§9.5）
SHARED_CKPT_ENV = "TIDE_SHARED_CKPT"
STUDENT_RUN_RE = re.compile(r"^tide_ppo_simpleai_student__42__\d+$")

# ---- S3-2/S3-0c（2026-11-26 二期 §5）：脚本 BC 录制流对账常量 ----
# C# ScriptBcRecorder 的二进制记录布局（小端）：7×int32 + 16B cardId + cards + global + 行特征。
BC_BIN_HEADER_FMT = "<7i16s"
BC_BIN_HEADER_SIZE = struct.calcsize(BC_BIN_HEADER_FMT)  # 44
GENEL_CARD_ID = "C_D1A00009"  # 绿「归根」（S3-0c 闸的计数对象；与 C# IdGreenRecall 同源）
BC_TYPE_PLAYCARD = 0          # TideActionType.PlayCard（动作行 [ACTION_F_TYPE] 同值）


def sha256_file(p: Path) -> str:
    h = hashlib.sha256()
    with open(p, "rb") as f:
        for chunk in iter(lambda: f.read(65536), b""):
            h.update(chunk)
    return h.hexdigest()


def resolve_ckpt(spec: str, run_name: str = "") -> Path:
    """ckpt 路径解析：绝对/相对 CWD（tide_rl 根，'logs\\...' 形态）/相对 logs/<run>。"""
    p = Path(spec)
    if p.is_file():
        return p
    if run_name:
        q = Path("logs") / run_name / spec
        if q.is_file():
            return q
    raise SystemExit(f"[!] 找不到 checkpoint：{spec}"
                     + (f"（也试了 logs/{run_name}/{spec}）" if run_name else ""))


def load_teacher(mf_path: str):
    """teacher_manifest.json → (deck, jit_apply, params, manifest)。校验三条硬闸。"""
    p = Path(mf_path)
    if not p.exists():
        raise SystemExit(f"[!] 教师清单不存在：{p}")
    mf = json.loads(p.read_text(encoding="utf-8"))
    deck = mf.get("deck")
    if deck not in DECKS:
        raise SystemExit(f"[!] 教师清单 {p} 的 deck={deck!r} 非法")
    if mf.get("layout_version") != LAYOUT_VERSION:
        raise SystemExit(f"[!] 教师 {deck} layout_version={mf.get('layout_version')!r} ≠ "
                         f"当前 {LAYOUT_VERSION}（{p}）")
    if mf.get("criterion_era") != BASELINE_ERA:
        raise SystemExit(f"[!] 教师 {deck} criterion_era={mf.get('criterion_era')!r} ≠ "
                         f"当前 {BASELINE_ERA}（{p}）")
    ckpt = resolve_ckpt(mf.get("checkpoint") or "", mf.get("run", ""))
    sha = sha256_file(ckpt)
    if mf.get("sha256") and mf["sha256"] != sha:
        raise SystemExit(f"[!] 教师 {deck} 权重哈希不符：{ckpt}\n    manifest={mf['sha256']}"
                         f"\n    实际  ={sha}（清单与权重不同源——冻结被打破）")
    run_dir = Path("logs") / mf.get("run", "")
    if not run_dir.is_dir():
        run_dir = Path(mf.get("run", ""))
    targs, _ = args_from_run(run_dir if run_dir.is_dir() else Path("."))
    agent = create_tide_agent(channels=targs.channels, rnn_channels=targs.rnn_channels,
                              rnn_type=targs.rnn_type)
    dummy = sample_input()
    dummy = jax.tree.map(lambda x: x[None, ...] if x is not None else None, dummy)
    params = agent.init(jax.random.PRNGKey(targs.seed), init_rstate(targs.rnn_type), dummy)
    with open(ckpt, "rb") as f:
        # 跨树加载（2026-11-26 用户定，决策③）：教师 ckpt 树不同代（如 aux 2→3）时 merge 并树
        old_params = flax.serialization.from_bytes(None, f.read())
        params, _merge_report = merge_params_by_shape(params, old_params)
        # 跨树加载留痕（决策③）：loaded/fresh 打一行——教师树缺/换的模块（如 aux 头）落 fresh
        _fm = sorted({p.rsplit("/", 1)[0] for p in _merge_report["fresh"]})
        print(f"[teacher:merge] 同形状继承 {_merge_report['n_loaded_params']}/{_merge_report['n_total_params']}"
              f" 参数；fresh {_merge_report['n_new_params']}（模块: {', '.join(_fm) or '—'}）", flush=True)
    print(f"[teacher] {deck} ← {ckpt}（run={mf.get('run')} step={mf.get('step')} "
          f"tier={mf.get('tier')} sha256={sha[:12]}…）", flush=True)
    return deck, jax.jit(agent.apply), params, mf


def _params_tree_nonempty(path: Path) -> bool:
    """ckpt 是否至少有一片权重——过滤空/损坏/非参数树（merge 进去会「继承 0 参数」而不报错，
    启动看似成功、实际从随机权重开训）。"""
    try:
        with open(path, "rb") as f:
            tree = flax.serialization.from_bytes(None, f.read())
        return bool(jax.tree.leaves(tree)) if tree is not None else False
    except Exception:
        return False


def resolve_shared_ckpt(spec: str = "") -> Path:
    """共享 last（学生第一轮热启动源，§9.5）解析：显式 → 环境变量 → 主仓 logs → 训练副本。

    显式 spec 只做存在性检查（调用方给哪条就用哪条）；自动候选**文档基线 run 优先**，
    其余按 mtime 从新到旧取第一条有实权的 ckpt。全部落空时报出试过的路径，不静默从头初始化。"""
    if spec:
        p = Path(spec)
        if not p.is_file():
            raise SystemExit(f"[!] 学生初始化权重不存在：{spec}")
        return p
    env = os.environ.get(SHARED_CKPT_ENV, "").strip()
    if env:
        p = Path(env)
        if p.is_file():
            return p
        raise SystemExit(f"[!] {SHARED_CKPT_ENV}={env} 不是文件（或已随环境搬走）")
    tide_rl = Path(__file__).resolve().parent
    cands = list((tide_rl / "logs").glob(f"{SHARED_RUN_GLOB}/last/params.msgpack"))
    # 训练副本：make_train_copy.ps1 的 Dest 口径是 C:\UnityProject\Tide_train*（与本仓库不同盘区），
    # 再兜一层仓库父目录下的 */tide_rl/... 以防换机后副本跟仓库放在一起。
    for base in (Path(r"C:\UnityProject"), tide_rl.parent):
        cands += list(base.glob(f"*/tide_rl/logs/{SHARED_RUN_GLOB}/last/params.msgpack"))
    cands = sorted({c.resolve() for c in cands if c.is_file()},
                   key=lambda p: p.stat().st_mtime, reverse=True)
    # 文档口径的共享基线优先（同名的其它 run 可能是 50k 冒烟/单主题 run，不能当共享 base）；
    # 基线在副本里、主仓没有时也能命中。都不在才退到最新的同名 run，并明确提示。
    exact = [c for c in cands if c.parent.parent.name == CANONICAL_SHARED_RUN]
    ordered = exact + [c for c in cands if c not in exact]
    for i, c in enumerate(ordered):
        if _params_tree_nonempty(c):
            if i >= len(exact):
                print(f"[init] 注意：找不到文档共享基线 {CANONICAL_SHARED_RUN}，"
                      f"退用最新的同名 run（确认它确实是共享 base）", flush=True)
            print(f"[init] 共享 last 解析 → {c}", flush=True)
            return c
    raise SystemExit(
        "[!] 找不到可用的共享 last（--init-params/--student-ckpt 指定，或设 "
        f"{SHARED_CKPT_ENV}）。已试：\n    " + ("\n    ".join(str(c) for c in cands) or "（无候选）"))


def resolve_student_run(spec: str, create: bool = True) -> Path:
    """学生 run 目录：给定名/路径，或取最新的 tide_ppo_simpleai_student__42__*，
    没有则以当前时间戳新建（名字形态满足 certify 硬闸）。

    2026-11-27 改：给了**合法但尚不存在**的新名时建目录（此前只认已存在目录——首次
    `train --student-run logs/tide_...__<ts>` 会被判「不存在」）。新名必须匹配 certify
    硬闸口径，防手滑造出无法认证的 run；已有目录不过滤（诊断/临时目录先建也能用）。"""
    if spec:
        p = Path(spec)
        if p.is_dir():
            return p
        q = Path("logs") / spec
        if q.is_dir():
            return q
        if not create:
            raise SystemExit(f"[!] --student-run 不存在：{spec}（也不是 logs/{spec}）")
        if not STUDENT_RUN_RE.match(p.name):
            raise SystemExit(
                f"[!] 新学生 run 名不合法：{p.name}\n"
                f"    必须是 tide_ppo_simpleai_student__42__<时间戳>（certify 硬闸只认这个形态）")
        target = p if (p.is_absolute() or len(p.parts) > 1) else Path("logs") / p
        target.mkdir(parents=True, exist_ok=True)
        print(f"[student] 新建 run 目录 {target}", flush=True)
        return target
    cands = sorted(Path("logs").glob(STUDENT_RUN_GLOB))
    if cands:
        return cands[-1]
    ts = int(time.time())
    p = Path("logs") / f"tide_ppo_simpleai_student__42__{ts}"
    p.mkdir(parents=True)
    return p


# ==================== S3-2 脚本 BC 流解析 + S3-0c shard 闸 ====================

def parse_bc_bin(path: Path):
    """ScriptBcRecorder 二进制记录流 → 记录迭代器。

    布局（小端，见 ScriptBcRecorder 头注）：int32 chosen / int32 n_actions / int32 turn /
    int32 ot / int32 seat / int32 type / int32 mode / 16B cardId(ASCII 补零) /
    float32 cards(MAX_CARDS×N_CARD_FEATURES) / float32 global(N_GLOBAL_FEATURES) /
    float32 行特征(n_actions×N_ACTION_FEATURES)。维度对账：与 tide_features 常量不一致
    即响亮失败（防布局漂移静默出脏 shard）。"""
    data = path.read_bytes()
    off = 0
    n_card = MAX_CARDS * N_CARD_FEATURES
    while off < len(data):
        chosen, n_actions, turn, ot, seat, atype, mode = struct.unpack_from(
            BC_BIN_HEADER_FMT, data, off)[:7]
        card_id = struct.unpack_from(BC_BIN_HEADER_FMT, data, off)[7].rstrip(b"\x00").decode("ascii")
        off += BC_BIN_HEADER_SIZE
        cards = np.frombuffer(data, dtype="<f4", count=n_card, offset=off).reshape(
            MAX_CARDS, N_CARD_FEATURES).astype(np.float32)
        off += 4 * n_card
        glob = np.frombuffer(data, dtype="<f4", count=N_GLOBAL_FEATURES, offset=off).astype(np.float32)
        off += 4 * N_GLOBAL_FEATURES
        rows = np.frombuffer(data, dtype="<f4", count=n_actions * N_ACTION_FEATURES,
                             offset=off).reshape(n_actions, N_ACTION_FEATURES).astype(np.float32)
        off += 4 * n_actions * N_ACTION_FEATURES
        yield {"chosen": int(chosen), "n_actions": int(n_actions), "turn": int(turn),
               "ot": int(ot), "seat": int(seat), "type": int(atype), "mode": int(mode),
               "card_id": card_id, "cards": cards, "global": glob, "actions": rows}
    assert off == len(data), f"[bc] {path.name} 尾部残留 {len(data) - off} 字节（布局漂移？）"


def count_genel_casts(shard_files):
    """S3-0c 闸读数（2026-11-26 二期 §5）：shard 里「选中行=PlayCard(0) 且来源卡=归根」的步数。

    两代 shard 两种识别路径：
    - 脚本 BC 分片（S3-2 collect --actor script）：带 chosen_card_id 字段——直接比 ID；
    - 教师/DAgger 分片（collect --actor teacher/student）：无 card_id——按 G5 的 DeckReg
      口径识别：牌库块 [154..183] 中费特征 [5]≈8 的唯一卡=绿预组归根（11 张，红蓝 0 张费 8），
      其内容身份 [15:29] 与选中行 [2] 指向槽位的身份全等 → 计为归根施放。
    归根只在绿预组 ⇒ 任何口径下非绿局不会误计。"""
    n_total = n_genel = 0
    n_unmatched = 0
    n_by_cardid = n_by_ident = 0
    for f in shard_files:
        z = np.load(f, allow_pickle=False)
        chosen = z["chosen"]
        n_total += len(chosen)
        n_unmatched += int((chosen < 0).sum())
        cards_id = [str(x) for x in z["chosen_card_id"]] if "chosen_card_id" in z else None
        # 逐局归归根身份（无 card_id 的分片）：游戏首步的牌库块按费 8 找唯一槽
        game_ident = {}
        for i, c in enumerate(chosen):
            if c < 0:
                continue
            a, b = int(z["actions_offsets"][i]), int(z["actions_offsets"][i + 1])
            rows = z["actions_flat"][a:b]
            if int(rows[c][ACTION_F_TYPE]) != BC_TYPE_PLAYCARD:
                continue
            if cards_id is not None and cards_id[i]:
                if cards_id[i] == GENEL_CARD_ID:
                    n_genel += 1
                    n_by_cardid += 1
                continue
            # 身份路径：选中行 [2]=来源槽位 → 该槽内容身份 vs 牌库费 8 卡身份
            src = int(round(float(rows[c][2])))
            gid = int(z["game_idx"][i])
            if gid not in game_ident:
                cards_i = z["cards"][i]
                game_ident[gid] = _genel_ident_from_deck(cards_i)
            ident = game_ident.get(gid)
            if ident is None or src < 0 or src >= MAX_CARDS:
                continue
            if _cards_ident(z["cards"][i][src]) == ident:
                n_genel += 1
                n_by_ident += 1
    return {"steps_total": n_total, "genel_cast_steps": n_genel,
            "unmatched_steps": n_unmatched, "by_card_id": n_by_cardid,
            "by_deck_ident": n_by_ident}


def _cards_ident(card_feats):
    """卡槽内容身份（TideObservation [15:29] 14 维浮点——g_sampling.ident 同口径）。"""
    return tuple(np.round(card_feats[15:29], 4))


def _genel_ident_from_deck(cards):
    """牌库块找费 8 的唯一卡 → 归根内容身份（绿预组 11 张费 8；找不到=None——非绿局/掺卡异常）。"""
    hits = []
    for i in range(154, 184):
        if cards[i][0] > 0.5 and abs(float(cards[i][5]) - 8.0) < 0.01:
            hits.append(_cards_ident(cards[i]))
    return hits[0] if len(hits) == 1 else None



def cmd_verify_shard(cli):
    """S3-0c BC shard 闸（计划 §5）：shard 必须数得出「PlayCard:归根」步数；为 0 → BC
    立即作废重采。--actions-stream 提供 S3-0a 动作行流时对账脚本局施放数（量级一致性，
    供人工判读——两数据集局数不同，不设硬等式）。报告落 round 目录 shard_gate.json。"""
    files = []
    for spec in cli.shards.split(","):
        spec = spec.strip()
        if spec:
            got = expand_shards(spec)
            if not got:
                raise SystemExit(f"[!] --shards 无匹配：{spec}")
            files.extend(got)
    report = count_genel_casts(files)
    if cli.actions_stream:
        from pathlib import Path as _P
        stream_cast = stream_land = 0
        for line in _P(cli.actions_stream).read_text(encoding="utf-8").splitlines():
            try:
                rec = json.loads(line)
            except Exception:
                continue
            if rec.get("rec") != "act" or rec.get("kind") != "PlayCard":
                continue
            if rec.get("card") == "归根":
                stream_cast += 1
            elif rec.get("kind") == "PlayLand":
                stream_land += 1
        report["stream_cast_genel"] = stream_cast
        report["stream_playland"] = stream_land
    report["gate"] = "PASS" if report["genel_cast_steps"] > 0 else "FAIL(归根施放步=0——BC 数据缺施放标签，作废重采)"
    out_p = None
    shard_parent = files[0].parent if files else None
    if shard_parent is not None:
        out_p = shard_parent / "shard_gate.json"
        out_p.write_text(json.dumps(report, ensure_ascii=False, indent=1), encoding="utf-8")
    print(json.dumps(report, ensure_ascii=False, indent=1), flush=True)
    if out_p:
        print(f"[out] {out_p}")
    if report["genel_cast_steps"] <= 0:
        raise SystemExit(1)


# ==================== collect ====================

class ShardWriter:
    """分片落盘：≤2000 步/片（压缩 npz）。动作行只存合法前缀（收集时断言合法行
    恒为前缀——C# 枚举器写序约定；断言失败=约定破坏，响亮报错不静默截断）。"""

    def __init__(self, round_dir: Path):
        self.round_dir = round_dir
        self.k = 0
        self.buf = {f: [] for f in ("cards", "global", "actions_flat", "n_actions",
                                    "rstate", "teacher_logits", "teacher_value",
                                    "game_idx", "chosen", "chosen_card_id")}
        self.offsets = [0]
        self.steps = 0        # 当前分片内步数（flush 后清零）
        self.total_steps = 0  # 本轮累计步数（不随 flush 清零——收尾日志/round_meta 用它）

    def add(self, obs, rstate_in, t_logits, t_value, game_idx, chosen):
        actions = obs["actions_"]
        mask = actions[:, 0] > 0
        n = int(mask.sum())
        if not bool(mask[:n].all()):
            raise RuntimeError("[distill] 动作合法行不是前缀——枚举器写序约定被打破，"
                               "拒绝截断存储（改全量 512 行需先修此断言）")
        self.buf["cards"].append(np.asarray(obs["cards_"], dtype=np.float32))
        self.buf["global"].append(np.asarray(obs["global_"], dtype=np.float32))
        self.buf["actions_flat"].append(np.asarray(actions[:n], dtype=np.float32))
        self.offsets.append(self.offsets[-1] + n)
        self.buf["n_actions"].append(n)
        self.buf["rstate"].append(np.asarray(rstate_in, dtype=np.float32).reshape(-1))
        self.buf["teacher_logits"].append(np.asarray(t_logits, dtype=np.float32))
        self.buf["teacher_value"].append(float(t_value))
        self.buf["game_idx"].append(game_idx)
        self.buf["chosen"].append(int(chosen))
        self.steps += 1
        self.total_steps += 1
        if self.steps >= STEPS_PER_SHARD:
            self.flush()

    def add_script_bc(self, rec, game_idx):
        """S3-2 脚本 BC：ScriptBcRecorder 二进制记录 → 分片步。
        rstate 全零（逐步独立口径）；teacher_logits/teacher_value 全零占位（CE 训练路径
        用 chosen，不读教师 logits——--loss ce）。chosen=-1（引擎拒绝/未匹配）不入标签
        （仍计步数——matched 率由 round_meta 汇总）。"""
        n = rec["n_actions"]
        actions = np.zeros((MAX_ACTIONS, N_ACTION_FEATURES), dtype=np.float32)
        actions[:n] = rec["actions"]
        obs = {"cards_": rec["cards"], "global_": rec["global"], "actions_": actions}
        self.buf["cards"].append(rec["cards"])
        self.buf["global"].append(rec["global"])
        self.buf["actions_flat"].append(rec["actions"])
        self.offsets.append(self.offsets[-1] + n)
        self.buf["n_actions"].append(n)
        self.buf["rstate"].append(np.zeros((N_RNN_CHANNELS,), dtype=np.float32))
        self.buf["teacher_logits"].append(np.zeros((MAX_ACTIONS,), dtype=np.float32))
        self.buf["teacher_value"].append(0.0)
        self.buf["game_idx"].append(game_idx)
        self.buf["chosen"].append(int(rec["chosen"]))
        self.buf["chosen_card_id"].append(rec["card_id"])
        self.steps += 1
        self.total_steps += 1
        if self.steps >= STEPS_PER_SHARD:
            self.flush()

    def flush(self):
        if not self.steps:
            return
        arrs = {k: (np.array(v) if k != "actions_flat" else np.concatenate(v))
                for k, v in self.buf.items()}
        arrs["actions_offsets"] = np.asarray(self.offsets, dtype=np.int32)
        out = self.round_dir / f"shard_{self.k:04d}.npz"
        np.savez_compressed(out, **arrs)
        print(f"[shard] {out}（{self.steps} 步）", flush=True)
        self.k += 1
        self.buf = {f: [] for f in self.buf}  # chosen_card_id 同键清零（__init__ 键集）
        self.offsets = [0]
        self.steps = 0


def cmd_collect(cli):
    if cli.actor == "script" and cli.teachers:
        raise SystemExit("[!] --actor script 不吃教师清单（脚本臂即标签源）——去掉 --teachers")
    teachers = {}
    manifests = []
    teachers_by_deck = {}
    if cli.actor != "script":
        for spec in cli.teachers.split(","):
            spec = spec.strip()
            if not spec:
                continue
            deck, apply_t, params_t, mf = load_teacher(spec)
            if deck in teachers:
                raise SystemExit(f"[!] deck={deck} 有两份教师清单（{spec}）——逐 deck 只认一个教师")
            teachers[deck] = (apply_t, params_t)
            manifests.append(spec)
            teachers_by_deck[deck] = spec
        missing = [d for d in DECKS if d not in teachers]
        if missing and not cli.allow_missing:
            raise SystemExit(f"[!] 缺教师 deck：{missing}（--allow-missing 才放行跳过该 deck）")
        if missing:
            print(f"[warn] --allow-missing：跳过无教师 deck {missing}", flush=True)

    student_run = resolve_student_run(cli.student_run)
    student_cfg = student_run / "config.json"
    sargs, _ = args_from_run(student_run) if student_cfg.exists() else (Args(), None)
    if cli.unity_path:
        sargs.unity_path = cli.unity_path
    if cli.project_path:
        sargs.project_path = cli.project_path
    distill_dir = student_run / "distill"
    distill_dir.mkdir(parents=True, exist_ok=True)
    round_n = cli.round if cli.round > 0 else 1 + len(list(distill_dir.glob("round_*")))
    round_dir = distill_dir / f"round_{round_n}"
    if round_dir.exists():
        raise SystemExit(f"[!] 轮次目录已存在：{round_dir}（--round 指定新号或先归档旧轮）")
    round_dir.mkdir(parents=True)
    writer = ShardWriter(round_dir)

    if cli.actor == "script":
        # S3-2（2026-11-26 二期 §5）：脚本臂 BC——C# ScriptBcRecorder 拦截录制（拦截匹配，
        # 见其头注），本侧只读流转 shard。TIDE_PROBE_BC_DIR 必须在 TideEnv 拉起 Unity
        # **之前**进环境（batchmode 子进程继承）；C# 侧每局写 bc_<gameId>.bin + .meta.json。
        # rstate 全零=逐步独立口径（脚本标签不绑学生递归上下文——DAgger 路径才需要）。
        bc_raw = round_dir / "bc_raw"
        bc_raw.mkdir(parents=True)
        os.environ["TIDE_PROBE_BC_DIR"] = str(bc_raw)
        student_ckpt = None
        print(f"[script-bc] TIDE_PROBE_BC_DIR={bc_raw}（C# 拦截录制）", flush=True)
    else:
        # 第 2 轮起必须用学生自己的 last 采集（DAgger 的状态分布是「当前学生」的）；
        # 首轮没有学生 last 才回落到共享 last（解析顺序见 resolve_shared_ckpt）。
        run_last = student_run / "last" / "params.msgpack"
        student_ckpt = Path(cli.student_ckpt) if cli.student_ckpt else (
            run_last if run_last.is_file() else resolve_shared_ckpt())
        if not student_ckpt.is_file():
            raise SystemExit(f"[!] 学生权重不存在：{student_ckpt}（--student-ckpt 指定，"
                             f"缺省=本 run 的 last / 共享 last）")
        print(f"[student] 采集用权重 ← {student_ckpt}", flush=True)
        s_agent = create_tide_agent(channels=sargs.channels, rnn_channels=sargs.rnn_channels,
                                    rnn_type=sargs.rnn_type)
        dummy = sample_input()
        dummy = jax.tree.map(lambda x: x[None, ...] if x is not None else None, dummy)
        s_params = s_agent.init(jax.random.PRNGKey(sargs.seed), init_rstate(sargs.rnn_type), dummy)
        with open(student_ckpt, "rb") as f:
            # 跨树加载（决策③同口径）：学生 ckpt 树不同代时 merge 并树
            old_params = flax.serialization.from_bytes(None, f.read())
            s_params, _merge_report = merge_params_by_shape(s_params, old_params)
            # 跨树加载留痕（决策③同口径）：loaded/fresh 打一行
            _sm = sorted({p.rsplit("/", 1)[0] for p in _merge_report["fresh"]})
            print(f"[student:merge] 同形状继承 {_merge_report['n_loaded_params']}/{_merge_report['n_total_params']}"
                  f" 参数；fresh {_merge_report['n_new_params']}（模块: {', '.join(_sm) or '—'}）", flush=True)
        apply_s = jax.jit(s_agent.apply)
        print(f"[student] {student_ckpt} → run {student_run.name}（actor={cli.actor}）", flush=True)

    env = TideEnv(
        unity_path=sargs.unity_path,
        project_path=sargs.project_path,
        max_steps=sargs.max_episode_steps,
        reward_lambda=sargs.reward_lambda,
        opponent="script" if cli.actor == "script" else "simpleai",
        model_theme="all",
        model_doped="" if cli.actor == "script" else sargs.model_doped,
        startup_timeout=sargs.startup_timeout,
        step_timeout=sargs.step_timeout,
    )

    cells = [(opp, seat) for opp in DECKS for seat in (0, 1)]
    games_meta = []
    t0 = time.time()
    decks_run = list(DECKS) if cli.actor == "script" else sorted(teachers)
    total = len(decks_run) * cli.games_per_deck
    done_games = 0
    print(f"\n[collect] 采集：decks={decks_run} × {cli.games_per_deck} 局/deck"
          f"（对手三脚本 × 双座轮转）= {total} 局 · actor={cli.actor} · 种子基 {cli.seed}\n",
          flush=True)
    seen_bc_games = set()
    try:
        for deck in decks_run:
            apply_t, params_t = teachers.get(deck, (None, None))
            for i in range(cli.games_per_deck):
                opp, seat = cells[i % len(cells)]
                ep = i // len(cells)
                seed = cli.seed + pair_seed_offset((deck,), DECKS, deck, opp) + seat * 500 + ep
                if cli.actor == "script":
                    # S3-2：脚本局整局在 reset 内打完（scriptVsScript）——C# 侧录制，Python 收流
                    env.reset(options={"opponent": "script", "theme": opp,
                                       "modelTheme": deck, "modelSeat": seat, "seed": seed})
                    for meta_p in sorted(bc_raw.glob("bc_*.meta.json")):
                        gid = meta_p.name[: -len(".meta.json")]  # bc_<gameId>
                        if gid in seen_bc_games:
                            continue
                        seen_bc_games.add(gid)
                        # C# File.WriteAllText(UTF8) 带 BOM——utf-8-sig 容错读
                        meta = json.loads(meta_p.read_text(encoding="utf-8-sig"))
                        game_idx = len(games_meta)
                        n_unmatched = 0
                        for rec in parse_bc_bin(bc_raw / (gid + ".bin")):
                            if rec["chosen"] < 0:
                                n_unmatched += 1
                            writer.add_script_bc(rec, game_idx)
                        games_meta.append({
                            "deck": deck, "opp": opp, "seat": f"P{seat + 1}", "seed": seed,
                            "winner": str(meta.get("winner", "")),
                            "model_win": str(meta.get("winner", "")) == str(seat),
                            "steps": int(meta.get("steps", 0)),
                            "bc_unmatched": n_unmatched,
                            "bc_game": int(meta.get("game", -1)),
                            "behavior": {},
                        })
                    done_games += 1
                    if done_games % 10 == 0 or done_games == total:
                        print(f"  [collect] {done_games}/{total} 局 · 步 {writer.total_steps}"
                              f" · {time.time() - t0:.0f}s", flush=True)
                    continue
                obs, info = env.reset(options={"opponent": "simpleai", "theme": opp,
                                               "modelTheme": deck, "modelSeat": seat,
                                               "seed": seed})
                if str(info.get("modelTheme", "")) != deck:
                    raise RuntimeError(f"[collect] modelTheme 回显 {info.get('modelTheme')!r}"
                                       f" ≠ 请求 {deck!r}——教师路由拒绝继续")
                game_idx = len(games_meta)
                rstate_s = init_rstate(sargs.rnn_type)
                rstate_t = init_rstate(sargs.rnn_type)
                rng = jax.random.fold_in(jax.random.PRNGKey(0xE0A1), seed & 0xFFFFFFFF)
                steps = 0
                done = False
                while not done and steps < sargs.max_episode_steps:
                    obs_batch = jax.tree.map(
                        lambda x: x[None, ...] if x is not None else None, obs)
                    rstate_s_in = rstate_s
                    rstate_s, logits_s, _v_s = apply_s(s_params, rstate_s, obs_batch)
                    rstate_t, logits_t, value_t = apply_t(params_t, rstate_t, obs_batch)
                    rng, sub = jax.random.split(rng)
                    if cli.actor == "teacher":      # BC 4a：教师自走自标
                        a = int(jax.random.categorical(sub, logits_t[0]))
                    else:                            # DAgger 4b：学生采样行动
                        a = int(jax.random.categorical(sub, logits_s[0]))
                    writer.add(obs, rstate_s_in, logits_t[0], value_t[0], game_idx, a)
                    obs, _r, done, truncated, info = env.step(a)
                    steps += 1
                    done = done or truncated
                winner = str(info.get("winner", "") or "")
                m_seat = int(info.get("modelSeat", -1))
                games_meta.append({
                    "deck": deck, "opp": opp, "seat": f"P{seat + 1}", "seed": seed,
                    "winner": winner, "model_win": bool(winner) and m_seat in (0, 1)
                              and int(winner) == m_seat,
                    "steps": steps, "behavior": behavior_of(info),
                })
                done_games += 1
                if done_games % 10 == 0 or done_games == total:
                    print(f"  [collect] {done_games}/{total} 局 · 步 {writer.total_steps}"
                          f" · {time.time() - t0:.0f}s", flush=True)
    finally:
        writer.flush()
        env.close()

    wins_by_deck = {d: [0, 0] for d in decks_run}
    for g in games_meta:
        wins_by_deck[g["deck"]][1] += 1
        if g["model_win"]:
            wins_by_deck[g["deck"]][0] += 1
    meta_doc = {
        "round": round_n,
        "actor": cli.actor,
        "teachers": manifests,                      # 清单路径列表（deck 归属见 teachers_by_deck）
        "teachers_by_deck": teachers_by_deck,        # {deck: manifest 路径}
        "student_run": student_run.name,
        "student_ckpt": str(student_ckpt),
        "games": len(games_meta),
        "games_per_deck": cli.games_per_deck,
        "seed": cli.seed,
        "steps_total": writer.total_steps,
        "shards": sorted(p.name for p in round_dir.glob("shard_*.npz")),
        "win_rate_by_deck": {d: round(w / n, 4) if n else None
                             for d, (w, n) in wins_by_deck.items()},
        "elapsed_sec": round(time.time() - t0, 1),
        "generated_at": time.strftime("%Y-%m-%d %H:%M:%S"),
        "per_game": games_meta,
    }
    (round_dir / "round_meta.json").write_text(
        json.dumps(meta_doc, ensure_ascii=False, indent=1), encoding="utf-8")
    print(f"\n[collect] round_{round_n}：{len(games_meta)} 局 / "
          f"{meta_doc['steps_total']} 决策步 / {len(meta_doc['shards'])} 片 → {round_dir}")
    print(f"[out] {round_dir / 'round_meta.json'}")


# ==================== train ====================

def expand_shards(spec: str):
    p = Path(spec)
    if p.is_dir():
        return sorted(p.glob("shard_*.npz"))
    if p.is_file():
        return [p]
    import glob as _glob
    return [Path(x) for x in _glob.glob(spec)]


def load_dataset(shard_files):
    """全部分片 → 拼接数组 + 逐局全局局号 game_keys。

    分片内 game_idx 是 round 内局号（collect 侧序号），跨轮/跨分片拼接会撞号——
    逐分片用 np.unique(return_inverse) 重映射成全局局号（向量式，无逐步 Python 循环）。"""
    keys = ("cards", "global", "actions_flat", "n_actions", "rstate",
            "teacher_logits", "teacher_value", "game_idx", "chosen")
    parts = {k: [] for k in keys}
    offsets = None
    gkeys = []
    next_gid = 0
    for f in shard_files:
        z = np.load(f)
        for k in keys:
            parts[k].append(z[k])
        offs = z["actions_offsets"]
        if offsets is None:
            offsets = offs                    # 首片保留全长（n+1，含首个 0）
        else:
            offsets = np.concatenate([offsets, offs[1:] + offsets[-1]])
        uniq, inv = np.unique(z["game_idx"], return_inverse=True)
        gkeys.append(next_gid + inv)
        next_gid += len(uniq)
    data = {k: np.concatenate(v, axis=0) for k, v in parts.items()}
    data["actions_offsets"] = offsets
    data["game_keys"] = np.concatenate(gkeys)
    return data, list(range(next_gid))


def cmd_train(cli):
    files = []
    for spec in cli.shards.split(","):
        spec = spec.strip()
        if spec:
            got = expand_shards(spec)
            if not got:
                raise SystemExit(f"[!] --shards 无匹配：{spec}")
            files.extend(got)
    data, uniq_games = load_dataset(files)
    n = len(data["cards"])
    print(f"[data] {len(files)} 片 · {n} 决策步 · {len(uniq_games)} 局", flush=True)

    rng = np.random.default_rng(42)
    n_val = max(1, round(len(uniq_games) * 0.05)) if len(uniq_games) > 1 else 0
    val_games = set(rng.choice(uniq_games, size=n_val, replace=False)) if n_val else set()
    is_val = np.isin(data["game_keys"], list(val_games)) if val_games \
        else np.zeros(n, dtype=bool)
    tr_idx = np.where(~is_val)[0]
    va_idx = np.where(is_val)[0]
    print(f"[data] 按局切 5%：train {len(tr_idx)} 步 / val {len(va_idx)} 步"
          f"（val 局 {sorted(int(g) for g in val_games)}）", flush=True)

    args = Args()
    args.channels, args.rnn_channels = cli.channels, cli.rnn_channels
    # 学生 run 先解析（--student-run 第一次给了合法新名会建目录）：init 缺省要看它的 last。
    student_run = resolve_student_run(cli.student_run)
    agent = create_tide_agent(channels=cli.channels, rnn_channels=cli.rnn_channels,
                              rnn_type=args.rnn_type)
    dummy = sample_input()
    dummy = jax.tree.map(lambda x: x[None, ...] if x is not None else None, dummy)
    init_from = None
    if cli.scratch:
        params = agent.init(jax.random.PRNGKey(args.seed), init_rstate(args.rnn_type), dummy)
        print("[student] from scratch", flush=True)
    else:
        if cli.init_params:
            init_from = Path(cli.init_params)
            if not init_from.is_file():
                raise SystemExit(f"[!] --init-params 不存在：{init_from}")
        else:
            # 第 2 轮起从学生自己的 last 续训（否则每轮都被拉回共享 base，前一轮白学）；
            # 首轮没有学生 last 时才用共享 last（解析顺序见 resolve_shared_ckpt）。
            run_last = student_run / "last" / "params.msgpack"
            init_from = run_last if run_last.is_file() else resolve_shared_ckpt()
        params = agent.init(jax.random.PRNGKey(args.seed), init_rstate(args.rnn_type), dummy)
        with open(init_from, "rb") as f:
            # 跨树加载（决策③同口径）：init ckpt 树不同代（v21 共享 last→v22/S3）时 merge 并树
            old_params = flax.serialization.from_bytes(None, f.read())
            params, _merge_report = merge_params_by_shape(params, old_params)
        print(f"[student] init ← {init_from}", flush=True)
    if not len(tr_idx):
        raise SystemExit("[!] 训练集为空（全部局都进了 val？）——检查分片")

    def make_batch(sel):
        """索引批 → (网络输入, 教师目标)。动作行从 flat+offsets 重建到 512×26 padded
        （补零行 valid=0 → 网络侧自动屏蔽，与采集时口径一致）。脚本 BC（--loss ce）
        时教师目标为全零占位，标签走 chosen（由调用方随批透传）。"""
        acts = np.zeros((len(sel), MAX_ACTIONS, N_ACTION_FEATURES), dtype=np.float32)
        off = data["actions_offsets"]
        for j, i in enumerate(sel):
            a, b = int(off[i]), int(off[i + 1])
            acts[j, : b - a] = data["actions_flat"][a:b]
        rstate_in = data["rstate"][sel].reshape(len(sel), N_RNN_CHANNELS)  # flax GRUCell (B,features)
        return (jnp.asarray(data["cards"][sel]), jnp.asarray(data["global"][sel]),
                jnp.asarray(acts), jnp.asarray(rstate_in),
                jnp.asarray(data["teacher_logits"][sel]),
                jnp.asarray(data["teacher_value"][sel]))

    alpha, beta, tau = cli.alpha, cli.beta, cli.tau

    def loss_tuple(p, cards, glob_, acts, rstate_in, t_logits, t_value, chosen):
        obs = {"cards_": cards, "global_": glob_, "actions_": acts, "h_actions_": None}
        _, logits_s, v_s = agent.apply(p, rstate_in, obs)
        if cli.loss == "ce":
            # S3-2 脚本 BC 路径（2026-11-26 二期 §5）：标签=脚本所选行（chosen），损失=
            # 选中行交叉熵（教师 logits 占位全零不读）。chosen=-1（引擎拒绝/未匹配）的步
            # 以 0 权重剔除；value 头不训（脚本无价值标签——β 不消费）。
            logp = jax.nn.log_softmax(logits_s)                       # (B, A)
            pick = jnp.take_along_axis(logp, chosen[:, None], axis=-1)[:, 0]
            w = (chosen >= 0).astype(jnp.float32)
            ce = -(pick * w).sum() / jnp.maximum(w.sum(), 1.0)
            return ce, (ce, 0.0)
        # 非法行双方 logits 均已被网络置 −1e9（学生侧由重建 obs 的 [0]valid 列屏蔽，
        # 教师侧存储时即屏蔽）→ softmax 后 p_T≈0 不参与 KL——等价于只对合法行算
        lt = t_logits / tau
        kl = jax.nn.softmax(lt) * (jax.nn.log_softmax(lt) - jax.nn.log_softmax(logits_s))
        kl = kl.sum(-1).mean()
        v_mse = ((v_s - t_value) ** 2).mean()
        return alpha * kl + beta * v_mse, (kl, v_mse)

    grad_fn = jax.jit(jax.value_and_grad(loss_tuple, argnums=0, has_aux=True))
    loss_fn = jax.jit(loss_tuple)
    chosen_all = jnp.asarray(data["chosen"], dtype=jnp.int32)
    opt = optax.chain(optax.clip_by_global_norm(0.5), optax.adam(cli.lr))
    opt_state = opt.init(params)

    jsonl_p = student_run / "distill_train.jsonl"
    jsonl_p.parent.mkdir(parents=True, exist_ok=True)
    upd = 0
    t0 = time.time()
    loss = kl = vm = float("nan")
    with open(jsonl_p, "a", encoding="utf-8") as logf:
        for epoch in range(cli.epochs):
            perm = rng.permutation(tr_idx)
            for s in range(0, len(perm), cli.batch):
                sel = perm[s: s + cli.batch]
                (loss, (kl, vm)), grads = grad_fn(params, *make_batch(sel), chosen_all[sel])
                updates, opt_state = opt.update(grads, opt_state)
                params = optax.apply_updates(params, updates)
                upd += 1
                if not all(np.isfinite([float(loss), float(kl), float(vm)])):
                    raise SystemExit(f"[!] update {upd} 出现非有限 loss——中止落盘")
                rec = {"update": upd, "epoch": epoch + 1, "split": "train",
                       "loss": float(loss), "kl": float(kl), "v_mse": float(vm),
                       "batch": int(len(sel))}
                logf.write(json.dumps(rec, ensure_ascii=False) + "\n")
                logf.flush()
                print(f"  [train] epoch {epoch + 1} update {upd}: loss={float(loss):.6f} "
                      f"kl={float(kl):.6f} v_mse={float(vm):.6f}", flush=True)
            if len(va_idx):
                vloss, (vkl, vvm) = loss_fn(params, *make_batch(va_idx), chosen_all[va_idx])
                rec = {"update": upd, "epoch": epoch + 1, "split": "val",
                       "loss": float(vloss), "kl": float(vkl), "v_mse": float(vvm),
                       "batch": int(len(va_idx))}
                logf.write(json.dumps(rec, ensure_ascii=False) + "\n")
                logf.flush()
                print(f"  [val] epoch {epoch + 1}: loss={float(vloss):.6f} "
                      f"kl={float(vkl):.6f} v_mse={float(vvm):.6f}", flush=True)

    last_dir = student_run / "last"
    last_dir.mkdir(parents=True, exist_ok=True)
    with open(last_dir / "params.msgpack", "wb") as f:
        f.write(flax.serialization.to_bytes(params))
    (last_dir / "meta.json").write_text(json.dumps({
        "step": upd, "update": upd, "time": time.strftime("%Y-%m-%d %H:%M:%S"),
        "distill": True, "epochs": cli.epochs, "lr": cli.lr,
    }, ensure_ascii=False, indent=1), encoding="utf-8")

    cfg_p = student_run / "config.json"
    cfg = json.loads(cfg_p.read_text(encoding="utf-8")) if cfg_p.exists() else {}
    cfg.update({
        "exp_name": student_run.name, "seed": 42,
        "channels": cli.channels, "rnn_channels": cli.rnn_channels,
        "rnn_type": args.rnn_type, "max_episode_steps": args.max_episode_steps,
        "reward_lambda": args.reward_lambda,
        "opponent": "simpleai", "model_theme": "all", "model_doped": "",
        "criterion_era": BASELINE_ERA, "layout_version": LAYOUT_VERSION,
        "distill": {"teachers": None,   # train 侧不知教师清单——见 collect 侧 round_meta.json
                    "shards": [str(f) for f in files],
                    "loss": cli.loss,
                    "alpha": cli.alpha, "beta": cli.beta, "tau": cli.tau,
                    "lr": cli.lr, "epochs": cli.epochs, "updates": upd,
                    "init_params": "" if cli.scratch else str(init_from)},
    })
    cfg_p.write_text(json.dumps(cfg, ensure_ascii=False, indent=1), encoding="utf-8")
    print(f"\n[train] {upd} updates / {time.time() - t0:.0f}s → {last_dir / 'params.msgpack'}")
    print(f"[out] {jsonl_p}")
    print(f"[out] {cfg_p}")
    print("[注意] best 选点仍归 eval_matrix 18 格（§9.6）——本产物只落 last/")


def main():
    ap = argparse.ArgumentParser(
        description="蒸馏学生（Stage 4）：collect=DAgger/BC 采集分片；train=KL+价值MSE 训练"
                    "（PPO 微调+教师 KL 锚延后未实现）")
    sub = ap.add_subparsers(dest="mode", required=True)

    c = sub.add_parser("collect", help="DAgger(--actor student)/BC(--actor teacher) 采集")
    c.add_argument("--teachers", default="",
                   help="逗号分隔 teacher_manifest.json（逐 deck 一份）；--actor script 时不需要")
    c.add_argument("--student-run", default="", help="学生 run（缺省最新 student run 或新建）")
    c.add_argument("--student-ckpt", default="",
                   help="学生权重（缺省=本 run 的 last〔第 2 轮起〕；没有则共享 last（自动解析））")
    c.add_argument("--actor", default="student", choices=["student", "teacher", "script"],
                   help="student=DAgger 4b（默认）；teacher=离线 BC 4a；"
                        "script=S3-2 脚本臂 BC（C# 拦截录制，2026-11-26 二期）")
    c.add_argument("--games-per-deck", type=int, default=150)
    c.add_argument("--seed", type=int, default=20261122)
    c.add_argument("--round", type=int, default=0, help="轮次号（缺省=已有 round 数+1）")
    c.add_argument("--allow-missing", action="store_true", help="缺整套 deck 教师时放行跳过")
    c.add_argument("--unity-path", default="")
    c.add_argument("--project-path", default="",
                   help="Unity 工程根（默认脚本父目录；从主仓脚本跑副本局时必传副本根）")
    c.set_defaults(func=cmd_collect)

    t = sub.add_parser("train", help="读分片训练（KL + 价值 MSE；optax adam+clip0.5）")
    t.add_argument("--shards", required=True,
                   help="逗号分隔：round 目录 / npz 文件 / glob 模式")
    t.add_argument("--student-run", default="", help="学生 run（产物落盘处）")
    t.add_argument("--init-params", default="", help="初始权重（缺省=本 run 的 last → 共享 last；--scratch 从头）")
    t.add_argument("--scratch", action="store_true", help="from scratch（仅诊断对照）")
    t.add_argument("--channels", type=int, default=128)
    t.add_argument("--rnn-channels", type=int, default=512)
    t.add_argument("--lr", type=float, default=1e-4)
    t.add_argument("--epochs", type=int, default=3)
    t.add_argument("--batch", type=int, default=64)
    t.add_argument("--alpha", type=float, default=1.0)
    t.add_argument("--beta", type=float, default=0.5)
    t.add_argument("--tau", type=float, default=1.0)
    t.add_argument("--loss", default="kl", choices=["kl", "ce"],
                   help="kl=教师蒸馏（默认）；ce=S3-2 脚本 BC（选中行交叉熵）")
    t.set_defaults(func=cmd_train)

    v = sub.add_parser("verify-shard", help="S3-0c BC shard 闸：数「PlayCard:归根」步数（0=作废）")
    v.add_argument("--shards", required=True,
                   help="逗号分隔：round 目录 / npz 文件 / glob 模式")
    v.add_argument("--actions-stream", default="",
                   help="S3-0a 动作行流（ScriptActionProbe jsonl）——对账脚本局施放数（量级）")
    v.set_defaults(func=cmd_verify_shard)

    cli = ap.parse_args()
    cli.func(cli)


if __name__ == "__main__":
    main()
