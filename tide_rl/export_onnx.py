"""
把训练 checkpoint（params.msgpack）导出为 Unity Sentis 可用的单步推理 ONNX。

模型 = TideRNNAgent（GRU Actor-Critic + 卡身份三通路编码器），推理图契约（全部静态维度、
batch 已脱皮，Unity 侧无需再压 batch 维）：

  输入（float32）
    rstate  (512,)   GRU 隐状态（局内逐步传递；新对局置零）
    cards   (80,65)  TideObservation.Cards
    global  (32,)    TideObservation.Globals
    actions (128,6)  LegalActionEnumerator.Features 补零到 128（valid=0 → 图内掩 -1e9）
  输出（float32）
    rstate_next (512,)  写回状态，下一步作 rstate 输入
    logits      (128,)  已含合法掩码（非法 = -1e9），argmax 即选动作
    value       (1,)    Critic 估值（部署可忽略，调试/评估用）

用法（务必用隔离 venv，不动训练环境）：
  .venv-export/Scripts/python.exe export_onnx.py --ckpt logs/<run>/best/params.msgpack
  # 架构参数默认从 run 目录 config.json 读取，也可 --channels/--rnn-channels/--rnn-type 覆盖

产出（tide_rl/exports/）：
  tide_policy.onnx           推理图（含 manifest 指纹等元数据）
  tide_policy_fixture.json   数值对拍样例（Unity 侧 Sentis vs JAX 参考输出）
并复制到 Assets/Resources/（--no-unity-copy 关闭；Unity 导入 .onnx 生成 ModelAsset）。

数值验证：onnxruntime 逐输出对拍 JAX，非法动作掩码抽检；容差 5e-3（f32 归约顺序差异）。
"""

import argparse
import hashlib
import json
import shutil
import sys
from pathlib import Path

import numpy as np

import jax
import jax.numpy as jnp
import flax.serialization
import onnx
import onnxruntime as ort
from jax2onnx import to_onnx

from tide_agent import create_tide_agent
from tide_features import (
    MAX_CARDS, N_CARD_FEATURES, N_GLOBAL_FEATURES,
    MAX_ACTIONS, N_ACTION_FEATURES, N_RNN_CHANNELS,
    sample_input, init_rstate,
)

ROOT = Path(__file__).resolve().parent
DEFAULT_RUN = ROOT / "logs" / "tide_ppo_selfplay__42__1788965027"
MANIFEST = ROOT / "card_identity_manifest.json"
# ONNX 必须放 Resources/：Sentis 运行时无 ONNX 文件解析器（ModelLoader.Load(path) 只认
# .sentis 格式），编辑器 ScriptedImporter 会把 Resources 下的 .onnx 转成 ModelAsset 供加载
UNITY_ASSETS = ROOT.parent / "Assets" / "Resources"

INPUT_NAMES = ["rstate", "cards", "global", "actions"]
OUTPUT_NAMES = ["rstate_next", "logits", "value"]


def _patch_jax2onnx_jit_plugin():
    """
    jax2onnx 0.16.1 × jax 0.11.1 兼容补丁：JitPlugin._fresh_var 以
    Var(aval, initial_qdd, final_qdd) 重建变量，而 0.11.1 的 Var 只收 aval
    （qdd 是更新 jax 的内部属性）。jit eqn 无处不在——jax 0.11 起 jnp.clip 等
    库函数本身即 jit 包装，不补丁导出必 TypeError。新 jax 装回 3 参签名时补丁自动退位。
    """
    from jax._src import core as jcore
    from jax2onnx.plugins.jax.core import jit as jit_plugin

    def _freshen_closed_jaxpr(closed):
        inner = getattr(closed, "jaxpr", closed)
        consts = getattr(closed, "consts", ())
        var_map = {}

        def fresh(v):
            if not isinstance(v, jcore.Var):
                return v
            if v not in var_map:
                try:
                    var_map[v] = jcore.Var(v.aval, getattr(v, "initial_qdd", None),
                                           getattr(v, "final_qdd", None))
                except TypeError:  # jax 0.11.x: Var 只接受 aval
                    var_map[v] = jcore.Var(v.aval)
            return var_map[v]

        def mapped(seq):
            return [fresh(v) for v in seq]

        cloned = jcore.Jaxpr(
            constvars=mapped(inner.constvars),
            invars=mapped(inner.invars),
            outvars=mapped(inner.outvars),
            eqns=[e.replace(invars=mapped(e.invars), outvars=mapped(e.outvars))
                  for e in inner.eqns],
            effects=inner.effects,
            debug_info=inner.debug_info,
            is_high=getattr(inner, "is_high", False),
        )
        return jcore.ClosedJaxpr(cloned, consts)

    jit_plugin.JitPlugin._freshen_closed_jaxpr = staticmethod(_freshen_closed_jaxpr)


_patch_jax2onnx_jit_plugin()


def sha256_file(path: Path) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def latest_checkpoint() -> Path:
    """logs/ 下按目录修改时间取最新 run，优先 best（评估最优）无则 last（最新步数）。"""
    runs = [d for d in (ROOT / "logs").iterdir() if d.is_dir()] if (ROOT / "logs").is_dir() else []
    if not runs:
        sys.exit(f"[✗] {ROOT / 'logs'} 下没有训练 run，请先训练或用 --ckpt 指定路径")
    latest = max(runs, key=lambda d: d.stat().st_mtime)
    for sub in ("best", "last"):
        p = latest / sub / "params.msgpack"
        if p.exists():
            return p
    sys.exit(f"[✗] 最新 run {latest} 没有 best/last checkpoint，请用 --ckpt 指定路径")


def load_config(ckpt: Path) -> dict:
    """架构参数优先取 run 目录 config.json（训练真实口径），缺省回落硬编码默认。"""
    cfg_path = ckpt.parent.parent / "config.json"
    cfg = {}
    if cfg_path.exists():
        cfg = json.loads(cfg_path.read_text(encoding="utf-8"))
    return {
        "channels": int(cfg.get("channels", 128)),
        "rnn_channels": int(cfg.get("rnn_channels", N_RNN_CHANNELS)),
        "rnn_type": str(cfg.get("rnn_type", "gru")),
    }


def build_case(seed: int, mask_from: int | None) -> dict:
    """一组推理输入。mask_from：把 [mask_from:] 动作置为非法（验证图内掩码）。"""
    rng = np.random.default_rng(seed)
    cards = rng.normal(size=(MAX_CARDS, N_CARD_FEATURES)).astype(np.float32)
    # 覆盖身份通路：id 槽填 0..255 合法下标、类型槽 0..83、参数块正常幅值
    for i in range(MAX_CARDS):
        cards[i, 0] = rng.integers(0, 2)  # valid 混合空槽
        for s in range(8):
            cards[i, 15 + s] = rng.integers(0, 256)
        for s in range(6):
            cards[i, 23 + s] = rng.integers(0, 84)
        cards[i, 29:] = rng.normal(size=42).astype(np.float32)
    glob = rng.normal(size=N_GLOBAL_FEATURES).astype(np.float32)
    actions = rng.normal(size=(MAX_ACTIONS, N_ACTION_FEATURES)).astype(np.float32)
    actions[:, 0] = 1.0
    actions[:, 1] = rng.integers(0, 6, MAX_ACTIONS)
    actions[:, 2] = rng.integers(-1, MAX_CARDS, MAX_ACTIONS)
    actions[:, 3] = rng.integers(-1, MAX_CARDS, MAX_ACTIONS)
    if mask_from is not None:
        actions[mask_from:, 0] = 0.0
    rstate = rng.normal(size=N_RNN_CHANNELS).astype(np.float32) * 0.1
    return {"rstate": rstate, "cards": cards, "global": glob, "actions": actions}


def main():
    ap = argparse.ArgumentParser(description="导出 Tide 策略为单步推理 ONNX")
    ap.add_argument("--ckpt", type=Path, default=None,
                    help="params.msgpack 路径（默认：logs/ 下最新 run 的 best，无 best 则 last）")
    ap.add_argument("--out", type=Path, default=ROOT / "exports" / "tide_policy",
                    help="输出前缀（生成 <out>.onnx 与 <out>_fixture.json）")
    ap.add_argument("--channels", type=int, default=None, help="覆盖 config.json 的 channels")
    ap.add_argument("--rnn-channels", type=int, default=None, help="覆盖 config.json 的 rnn_channels")
    ap.add_argument("--rnn-type", type=str, default=None, choices=["gru", "lstm"],
                    help="覆盖 config.json 的 rnn_type（lstm 暂未适配导出）")
    ap.add_argument("--opset", type=int, default=23, help="ONNX opset（Sentis 2.6 支持 7..25）")
    ap.add_argument("--tol", type=float, default=5e-3, help="ORT vs JAX 最大绝对误差告警线")
    ap.add_argument("--no-unity-copy", action="store_true", help="不复制到 Assets/StreamingAssets")
    args = ap.parse_args()
    if args.ckpt is None:
        args.ckpt = latest_checkpoint()
        print(f"[i] 未指定 --ckpt，自动选择: {args.ckpt}")

    arch = load_config(args.ckpt)
    for k, v in (("channels", args.channels), ("rnn_channels", args.rnn_channels), ("rnn_type", args.rnn_type)):
        if v is not None:
            arch[k] = v
    if arch["rnn_type"] != "gru":
        sys.exit(f"暂只支持 gru 导出（当前 {arch['rnn_type']}）")

    # ---- 载入参数（from_bytes 无模板即可还原完整 pytree）----
    agent = create_tide_agent(arch["channels"], arch["rnn_channels"], arch["rnn_type"])
    params = flax.serialization.from_bytes(None, args.ckpt.read_bytes())
    params = jax.tree_util.tree_map(jnp.asarray, params)
    n_params = sum(p.size for p in jax.tree_util.tree_leaves(params))
    print(f"[i] ckpt   : {args.ckpt}")
    print(f"[i] 架构   : channels={arch['channels']} rnn={arch['rnn_channels']} gru 参数量={n_params:,}")

    # ---- 单步推理包装：batch 脱皮（Unity 喂 2 维张量即可）----
    # 注意：传给 to_onnx 的必须是未 jit 的原函数——jax2onnx 的 jit 插件与 jax 0.11.1
    # 内部 Var API 不兼容（initial_qdd/final_qdd 属性缺失会 TypeError）。
    def policy_step(rstate, cards, global_, actions):
        obs = {"cards_": cards[None], "global_": global_[None], "actions_": actions[None]}
        new_rstate, logits, value = agent.apply(params, rstate[None], obs)
        return new_rstate[0], logits[0], value[:1]

    ref_step = jax.jit(policy_step)  # 对拍参考（含真实推理语义）

    # ---- 导出 ONNX ----
    onnx_path = args.out.with_suffix(".onnx")
    args.out.parent.mkdir(parents=True, exist_ok=True)
    f32 = jnp.float32
    to_onnx(
        policy_step,
        inputs=[
            jax.ShapeDtypeStruct((arch["rnn_channels"],), f32),
            jax.ShapeDtypeStruct((MAX_CARDS, N_CARD_FEATURES), f32),
            jax.ShapeDtypeStruct((N_GLOBAL_FEATURES,), f32),
            jax.ShapeDtypeStruct((MAX_ACTIONS, N_ACTION_FEATURES), f32),
        ],
        input_names=INPUT_NAMES,
        output_names=OUTPUT_NAMES,
        opset=args.opset,
        model_name="tide_policy_step",
        return_mode="file",
        output_path=str(onnx_path),
    )
    print(f"[✓] ONNX 已导出: {onnx_path} ({onnx_path.stat().st_size / 1e6:.1f} MB)")

    # ---- 算子面审计：无自定义域（PythonOp 类回退在 Sentis 无法执行）----
    model = onnx.load(str(onnx_path))
    ops = sorted({n.op_type for n in model.graph.node})
    domains = sorted({n.domain for n in model.graph.node})
    print(f"[i] opset {model.opset_import[0].version} | 算子: {', '.join(ops)}")
    assert all(d == "" for d in domains), f"存在自定义域算子（Sentis 不支持）: {domains}"

    # ---- 元数据：架构 + manifest 指纹（Unity 侧对表校验）----
    manifest_sha = sha256_file(MANIFEST) if MANIFEST.exists() else "missing"
    meta = {
        "tide.channels": str(arch["channels"]),
        "tide.rnn_channels": str(arch["rnn_channels"]),
        "tide.rnn_type": arch["rnn_type"],
        "tide.obs_layout": f"cards({MAX_CARDS}x{N_CARD_FEATURES}) global({N_GLOBAL_FEATURES}) actions({MAX_ACTIONS}x{N_ACTION_FEATURES})",
        "tide.manifest_sha256": manifest_sha,
        "tide.ckpt": str(args.ckpt),
        "tide.exporter": "jax2onnx export_onnx.py",
    }
    for k, v in meta.items():
        entry = model.metadata_props.add()
        entry.key, entry.value = k, v
    onnx.save(model, str(onnx_path))

    # ---- ORT 数值对拍 ----
    sess = ort.InferenceSession(str(onnx_path), providers=["CPUExecutionProvider"])
    assert [i.name for i in sess.get_inputs()] == INPUT_NAMES, \
        f"输入名不符: {[i.name for i in sess.get_inputs()]}"
    cases = [build_case(0, None), build_case(1, None), build_case(2, 64), build_case(3, None)]
    # fixture 为 JsonUtility 兼容布局（Unity 侧无 Newtonsoft）：全部扁平 float[]、一层嵌套
    fixture = {
        "manifest_sha256": manifest_sha,
        "tol": args.tol,
        "channels": arch["channels"],
        "rnn_channels": arch["rnn_channels"],
        "case_count": len(cases),
        "cases": [],
    }
    worst = 0.0
    for ci, case in enumerate(cases):
        feeds = {k: case[k] for k in INPUT_NAMES}
        jax_out = ref_step(case["rstate"], case["cards"], case["global"], case["actions"])
        ort_out = sess.run(OUTPUT_NAMES, feeds)
        diffs = [float(np.abs(j.astype(np.float32) - o).max()) for j, o in zip(jax_out, ort_out)]
        worst = max(worst, max(diffs))
        argmax = int(np.argmax(ort_out[1]))
        print(f"[对拍 {ci}] max|Δ| rstate={diffs[0]:.2e} logits={diffs[1]:.2e} value={diffs[2]:.2e} "
              f"| argmax={argmax}")
        fixture["cases"].append({
            "rstate": case["rstate"].round(6).tolist(),
            "cards_flat": case["cards"].reshape(-1).round(6).tolist(),
            "global_flat": case["global"].round(6).tolist(),
            "actions_flat": case["actions"].reshape(-1).round(6).tolist(),
            # 参考输出基于取整后的输入重算（Unity 读同一 JSON，逐位一致），见下方回填
            "expect_rstate_flat": None,
            "expect_logits": None,
            "expect_value": None,
            "argmax": argmax,
        })
        # 掩码抽检：case2 的 [64:] 全非法 → logits 必须压到 -1e9
        if ci == 2:
            bad = ort_out[1][64:]
            assert float(bad.max()) < -9e8, f"非法动作掩码失效: max={bad.max()}"
            print(f"[✓] 非法动作掩码: [64:] logits max = {bad.max():.3e}")

    # 用取整输入重算参考输出（Unity 侧对拍的基准必须与 JSON 内输入逐位一致）
    for fc in fixture["cases"]:
        ref = ref_step(
            np.array(fc["rstate"], np.float32), np.array(fc["cards_flat"], np.float32).reshape(MAX_CARDS, N_CARD_FEATURES),
            np.array(fc["global_flat"], np.float32), np.array(fc["actions_flat"], np.float32).reshape(MAX_ACTIONS, N_ACTION_FEATURES))
        fc["expect_rstate_flat"] = np.asarray(ref[0]).reshape(-1).round(5).tolist()
        fc["expect_logits"] = np.asarray(ref[1]).round(5).tolist()
        fc["expect_value"] = np.asarray(ref[2]).round(5).tolist()

    if worst > args.tol:
        sys.exit(f"[✗] 数值偏差超容差: {worst:.2e} > {args.tol}")
    print(f"[✓] ORT vs JAX 对拍通过（max|Δ|={worst:.2e} ≤ {args.tol}）")

    fixture_path = args.out.with_name(args.out.name + "_fixture.json")
    fixture_path.write_text(json.dumps(fixture), encoding="utf-8")
    print(f"[✓] 对拍样例: {fixture_path}")

    if not args.no_unity_copy:
        UNITY_ASSETS.mkdir(parents=True, exist_ok=True)
        shutil.copy2(onnx_path, UNITY_ASSETS / onnx_path.name)
        shutil.copy2(fixture_path, UNITY_ASSETS / fixture_path.name)
        print(f"[✓] 已复制到 {UNITY_ASSETS}（Unity 下次打开时生成 .meta）")

    print("[✓] 完成。Unity 侧用法见 Assets/Scripts/AI/NeuralAI/OnnxTidePolicy.cs 头注释。")


if __name__ == "__main__":
    main()
