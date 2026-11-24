"""
把训练 checkpoint（params.msgpack）导出为 Unity Sentis 可用的单步推理 ONNX。

模型 = TideRNNAgent（GRU Actor-Critic + 卡身份三通路编码器），推理图契约（全部静态维度、
batch 已脱皮，Unity 侧无需再压 batch 维）：

  输入（float32）
    rstate  (512,)   GRU 隐状态（局内逐步传递；新对局置零）
    cards   (191,152) TideObservation.Cards（v19 绿套补盲：参数块 6→9 维 116→152、
                      卡槽 v15 起 191；v15 多色可产掩码已随回退拍板作废）
    global  (70,)    TideObservation.Globals（v19：+12 剩余可用元素块 g[58..69]）
    actions (512,N_ACTION_FEATURES)  LegalActionEnumerator.Features 补零到 512（valid=0 → 图内掩 -1e9）
  输出（float32）
    rstate_next (512,)  写回状态，下一步作 rstate 输入
    logits      (512,)  已含合法掩码（非法 = -1e9），argmax 即选动作
    value       (1,)    Critic 估值（部署可忽略，调试/评估用）

v2 注意（2026-09-30 C 期）：源/target 槽位下标的归一化在**编码器内**（图内），
C# 部署侧照旧喂原始特征（-1..91），部署与训练逐值同口径。

用法（务必用隔离 venv，不动训练环境）：
  .venv-export/Scripts/python.exe export_onnx.py --ckpt logs/<run>/best/params.msgpack
  # 架构参数默认从 run 目录 config.json 读取，也可 --channels/--rnn-channels/--rnn-type 覆盖

产出（tide_rl/exports/）：
  tide_policy.onnx           推理图（含布局/池尺寸元数据——P-1 稳定下标，无 manifest）
  tide_policy_fixture.json   数值对拍样例（Unity 侧 Sentis vs JAX 参考输出）
并复制到 Assets/Art/AIModels/（--no-unity-copy 关闭；Unity 导入 .onnx 生成 ModelAsset，供 YooAsset 热更打包）。

数值验证：onnxruntime 逐输出对拍 JAX，非法动作掩码抽检；容差 5e-3（f32 归约顺序差异）。
"""

import argparse
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
    N_CARD_POOL, N_EFFECT_TYPES, LAYOUT_VERSION,
    CARD_ID_START, N_ID_SLOTS, TYPE_ID_START, N_TYPE_SLOTS,
    sample_input, init_rstate,
)

ROOT = Path(__file__).resolve().parent
# P-1（2026-11-19 稳定下标）：card_identity_manifest.json 已退役（仅存档）——导出不再读它
# ONNX 放 Assets/Art/AIModels/：编辑器 ScriptedImporter 转成 ModelAsset（Sentis 运行时无
# ONNX 文件解析器）；YooAsset AIModel 收集组随热更包下发，编辑器直启走 AssetDatabase 兜底
UNITY_ASSETS = ROOT.parent / "Assets" / "Art" / "AIModels"

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


def verify_export(onnx_path: Path, fixture_path: Path) -> bool:
    """部署校验（2026-11-02 训练管线加固；P-1 2026-11-19 口径更新）：导出产物三方对账——
    ① onnx 图实际输入 shape（最硬证据，metadata 忘写也拦得住）vs 本侧 tide_features 常量；
    ② onnx metadata / fixture 头的 layoutVersion 与 dims（含池尺寸）vs 本侧常量；
    ③ 池尺寸（P-1）：fixture dims.cardPoolSize/typePoolSize 与 onnx metadata 的
    tide.card_pool_size/type_pool_size vs 本侧 N_CARD_POOL/N_EFFECT_TYPES——旧 1024/256
    池模型在此被拒（manifest sha 对账已随稳定下标退役，文件仅存档）。
    任一不一致打印 ✗ 明细并返回 False（调用方据此退出非 0）——旧产物过期在哪一眼可见。
    Unity 加载侧另有 OnnxTidePolicy.ValidateModelLayout 按图输入维度 + embedding 池尺寸拦。"""
    ok = True
    model = onnx.load(str(onnx_path))
    onnx_meta = {e.key: e.value for e in model.metadata_props}
    fx = json.loads(fixture_path.read_text(encoding="utf-8"))

    # ① 图实际输入 vs 本侧常量（布局漂移的权威判据；经 ORT 读图输入 shape，
    #    动态维返回 None/字符串，同样按不匹配报）
    expect_inputs = {
        "rstate": [N_RNN_CHANNELS],
        "cards": [MAX_CARDS, N_CARD_FEATURES],
        "global": [N_GLOBAL_FEATURES],
        "actions": [MAX_ACTIONS, N_ACTION_FEATURES],
    }
    sess = ort.InferenceSession(str(onnx_path), providers=["CPUExecutionProvider"])
    actual_inputs = {i.name: list(i.shape) for i in sess.get_inputs()}
    for name, want in expect_inputs.items():
        got = actual_inputs.get(name)
        if got != want:
            print(f"[部署校验] ✗ 图输入 {name}: 实际 {got} ≠ 本侧常量 {want}（tide_features 与模型不同代）")
            ok = False
    if ok:
        print(f"[部署校验] 图输入 vs 本侧常量一致 ✓（rstate {N_RNN_CHANNELS} / cards "
              f"{MAX_CARDS}x{N_CARD_FEATURES} / global {N_GLOBAL_FEATURES} / "
              f"actions {MAX_ACTIONS}x{N_ACTION_FEATURES}）")

    # ② layoutVersion：本侧 vs onnx metadata vs fixture 头
    fx_layout = fx.get("layout_version")
    meta_layout = onnx_meta.get("tide.layout_version")
    if str(LAYOUT_VERSION) == str(meta_layout) == str(fx_layout):
        print(f"[部署校验] layoutVersion: 本侧 / onnx metadata / fixture 均 v{LAYOUT_VERSION} ✓")
    else:
        print(f"[部署校验] ✗ layoutVersion 不一致：本侧 v{LAYOUT_VERSION} / onnx metadata "
              f"{meta_layout} / fixture {fx_layout}")
        ok = False
    fx_dims = fx.get("dims") or {}
    for key, want in (("nCard", N_CARD_FEATURES), ("nGlobal", N_GLOBAL_FEATURES),
                      ("nAction", N_ACTION_FEATURES), ("maxActions", MAX_ACTIONS),
                      ("maxCards", MAX_CARDS),
                      ("cardPoolSize", N_CARD_POOL), ("typePoolSize", N_EFFECT_TYPES)):
        if fx_dims.get(key) != want:
            print(f"[部署校验] ✗ fixture dims.{key}: {fx_dims.get(key)} ≠ 本侧 {want}")
            ok = False

    # ③ 池尺寸（P-1）：onnx metadata vs 本侧常量（防旧 1024/256 池模型冒充新代）
    for meta_key, want in (("tide.card_pool_size", N_CARD_POOL),
                           ("tide.type_pool_size", N_EFFECT_TYPES)):
        got = onnx_meta.get(meta_key)
        if str(want) == str(got):
            print(f"[部署校验] {meta_key}: {got} ✓")
        else:
            print(f"[部署校验] ✗ {meta_key}: onnx metadata={got} ≠ 本侧 {want}"
                  "——旧池模型（P-1 前）作废，重导出")
            ok = False

    print(f"[部署校验] {'全部一致 ✓ 可部署' if ok else '存在不一致 ✗（产物过期，勿部署）'}")
    return ok


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
    """一组推理输入。mask_from：把 [mask_from:] 动作置为非法（验证图内掩码）。
    维度全部取自 tide_features 常量（2026-09-10 目标域模型 71→65 后不再硬编码）。"""
    rng = np.random.default_rng(seed)
    cards = rng.normal(size=(MAX_CARDS, N_CARD_FEATURES)).astype(np.float32)
    # 覆盖身份通路：id 槽合法下标、类型槽 1 基编号、参数块正常幅值
    #（下标全符号化——曾硬编码 v3 时代 15+8/23+6，槽位扩容后漂移成静默错槽，2026-11-02 v9 顺手修）
    for i in range(MAX_CARDS):
        cards[i, 0] = rng.integers(0, 2)  # valid 混合空槽
        for s in range(N_ID_SLOTS):
            cards[i, CARD_ID_START + s] = rng.integers(0, N_CARD_POOL)
        for s in range(N_TYPE_SLOTS):
            cards[i, TYPE_ID_START + s] = rng.integers(0, N_EFFECT_TYPES)
        n_type = TYPE_ID_START + N_TYPE_SLOTS
        cards[i, n_type:] = rng.normal(size=N_CARD_FEATURES - n_type).astype(np.float32)
    glob = rng.normal(size=N_GLOBAL_FEATURES).astype(np.float32)
    actions = rng.normal(size=(MAX_ACTIONS, N_ACTION_FEATURES)).astype(np.float32)
    actions[:, 0] = 1.0
    actions[:, 1] = rng.integers(0, 12, MAX_ACTIONS)  # v2：type 值域 0..11（契约第 1 节）
    actions[:, 2] = rng.integers(-1, MAX_CARDS, MAX_ACTIONS)
    actions[:, 3] = rng.integers(-1, MAX_CARDS, MAX_ACTIONS)
    actions[:, 7] = rng.integers(0, 4, MAX_ACTIONS)   # v2：targetKind 0..3
    # v14（2026-11-15 共享规格 v0.2）：目标区域/归属值域采样（-1=无目标；zoneRank 0..9；
    # sideRank -1/0/1——合成分布只求形状对拍，语义见 tide_features.N_ACTION_FEATURES 注）
    actions[:, 12] = rng.integers(-1, 10, MAX_ACTIONS)
    actions[:, 13] = rng.integers(-1, 2, MAX_ACTIONS)
    # P1（2026-11-19 v0.3）：移动格/宣言序号三列（-1=无；格 0..6；候选序号 -1..15）
    actions[:, 14] = rng.integers(-1, 7, MAX_ACTIONS)
    actions[:, 15] = rng.integers(-1, 7, MAX_ACTIONS)
    actions[:, 16] = rng.integers(-1, 16, MAX_ACTIONS)
    actions[:, 17] = rng.integers(-1, 9, MAX_ACTIONS)  # P2：落格格下标 -1..8
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
    ap.add_argument("--no-unity-copy", action="store_true", help="不复制到 Assets/Art/AIModels")
    ap.add_argument("--verify-only", action="store_true",
                    help="不导出，只校验既有产物（exports/ 下 <out>.onnx + <out>_fixture.json 与"
                         "本侧布局/池尺寸对账，打印 layout/pool 是否一致）")
    ap.add_argument("--random", action="store_true",
                    help="随机初始化导出（无训练 run 时的形状链路验证件——v3 契约验收用；"
                         "产出仅供 Sentis fixture 对拍，不可部署）")
    args = ap.parse_args()

    onnx_path = args.out.with_suffix(".onnx")
    fixture_path = args.out.with_name(args.out.name + "_fixture.json")
    if args.verify_only:
        if not onnx_path.exists() or not fixture_path.exists():
            sys.exit(f"[✗] 待校验产物不存在：{onnx_path} / {fixture_path}（先跑导出）")
        sys.exit(0 if verify_export(onnx_path, fixture_path) else 1)
    if args.random and args.ckpt is None:
        args.ckpt = None
    elif args.ckpt is None:
        args.ckpt = latest_checkpoint()
        print(f"[i] 未指定 --ckpt，自动选择: {args.ckpt}")

    arch = {"channels": 128, "rnn_channels": N_RNN_CHANNELS, "rnn_type": "gru"} if args.ckpt is None         else load_config(args.ckpt)
    for k, v in (("channels", args.channels), ("rnn_channels", args.rnn_channels), ("rnn_type", args.rnn_type)):
        if v is not None:
            arch[k] = v
    if arch["rnn_type"] != "gru":
        sys.exit(f"暂只支持 gru 导出（当前 {arch['rnn_type']}）")

    # ---- 载入参数（from_bytes 无模板即可还原完整 pytree）；--random 走随机初始化 ----
    agent = create_tide_agent(arch["channels"], arch["rnn_channels"], arch["rnn_type"])
    if args.ckpt is None:
        sample = sample_input()
        params = agent.init(jax.random.PRNGKey(0), jnp.zeros((1, arch["rnn_channels"])),
                            {k: jnp.asarray(v[None]) for k, v in sample.items()})
        print(f"[i] --random: 随机初始化（seed=0），仅作形状/fixture 链路验证")
    else:
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

    # ---- 布局兼容冒烟（2026-11-18 普查 P2）：latest 自动选 run 可能取到旧代
    # checkpoint（如 2026-09 布局权重），维度不符在此响亮失败——否则要到
    # to_onnx/ORT 对拍处才抛密码学形状报错，难归因。--random 跳过（天然同代）。
    # 输入用 build_case（与 ORT 对拍同构造、裸数组键）——勿用 tide_features.sample_input
    #（那是 obs 视图键 cards_/h_actions_，与 policy_step 的位置参数不匹配，2026-11-19 实证）。----
    if args.ckpt is not None:
        try:
            _c = build_case(0, None)
            policy_step(jnp.asarray(_c["rstate"]), jnp.asarray(_c["cards"]),
                        jnp.asarray(_c["global"]), jnp.asarray(_c["actions"]))
        except Exception as ex:
            sys.exit(f"[✗] checkpoint 与当前观测布局不兼容（{ex.__class__.__name__}: {ex}）——"
                     f"多半取到了旧代 run。新训练完成后重试，或用 --ckpt 显式指定"
                     f"与当前布局同代的 run（旧 run 已归档至 logs_archive_*，不会被 latest 选中）。")
        print("[✓] 布局冒烟通过：checkpoint 与当前观测维度兼容")

    # ---- 导出 ONNX ----
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

    # ---- 元数据：架构 + 池尺寸（P-1 稳定下标；manifest 指纹已退役——文件仅存档）----
    meta = {
        "tide.channels": str(arch["channels"]),
        "tide.rnn_channels": str(arch["rnn_channels"]),
        "tide.rnn_type": arch["rnn_type"],
        "tide.layout_version": str(LAYOUT_VERSION),
        "tide.obs_layout": f"cards({MAX_CARDS}x{N_CARD_FEATURES}) global({N_GLOBAL_FEATURES}) actions({MAX_ACTIONS}x{N_ACTION_FEATURES})",
        "tide.card_pool_size": str(N_CARD_POOL),
        "tide.type_pool_size": str(N_EFFECT_TYPES),
        "tide.ckpt": str(args.ckpt),
        "tide.exporter": "jax2onnx export_onnx.py",
    }
    for k, v in meta.items():
        entry = model.metadata_props.add()
        entry.key, entry.value = k, v
    onnx.save(model, str(onnx_path))

    # onnx.save 已把权重嵌回单文件；jax2onnx 中间步骤落下的外部权重残留要清掉，
    # 否则 exports/ 里留一个 12MB 的孤儿 .data，还容易被误当成部署必需品
    n_external = sum(1 for i in model.graph.initializer
                     if i.data_location == onnx.TensorProto.EXTERNAL)
    assert n_external == 0, "权重仍外置在 .data，Unity 单文件部署会加载失败"
    orphan_data = onnx_path.with_name(onnx_path.name + ".data")
    if orphan_data.exists():
        orphan_data.unlink()
        print(f"[✓] 已清理中间产物: {orphan_data.name}")

    # ---- ORT 数值对拍 ----
    sess = ort.InferenceSession(str(onnx_path), providers=["CPUExecutionProvider"])
    assert [i.name for i in sess.get_inputs()] == INPUT_NAMES, \
        f"输入名不符: {[i.name for i in sess.get_inputs()]}"
    cases = [build_case(0, None), build_case(1, None), build_case(2, 64), build_case(3, None)]
    # fixture 为 JsonUtility 兼容布局（Unity 侧无 Newtonsoft）：全部扁平 float[]、一层嵌套；
    # layout_version/dims（2026-11-02；P-1 2026-11-19 增池尺寸）供加载侧对账——Unity
    # NeuralDeployGuard.VerifyFixtureLayout 与 Python --verify-only 都按它拦旧产物
    fixture = {
        "layout_version": LAYOUT_VERSION,
        "dims": {
            "nCard": N_CARD_FEATURES,
            "nGlobal": N_GLOBAL_FEATURES,
            "nAction": N_ACTION_FEATURES,
            "maxActions": MAX_ACTIONS,
            "maxCards": MAX_CARDS,
            "cardPoolSize": N_CARD_POOL,
            "typePoolSize": N_EFFECT_TYPES,
        },
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

    fixture_path.write_text(json.dumps(fixture), encoding="utf-8")
    print(f"[✓] 对拍样例: {fixture_path}")

    if not args.no_unity_copy:
        UNITY_ASSETS.mkdir(parents=True, exist_ok=True)
        shutil.copy2(onnx_path, UNITY_ASSETS / onnx_path.name)
        shutil.copy2(fixture_path, UNITY_ASSETS / fixture_path.name)
        print(f"[✓] 已复制到 {UNITY_ASSETS}（Unity 下次打开时生成 .meta）")

    print("[✓] 完成。Unity 侧用法见 Assets/Scripts/HotUpdate/AI/NeuralAI/OnnxTidePolicy.cs 头注释。")

    # 部署校验（2026-11-02）：导出即对账——图输入/布局/清单任一不一致非 0 退出
    if not verify_export(onnx_path, fixture_path):
        sys.exit(1)


if __name__ == "__main__":
    main()
