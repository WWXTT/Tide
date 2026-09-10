# Tide RL —— Phase 3 Python 侧（路线 1：简单 MLP/GRU）

## 文件清单

Phase 3 Python 侧代码已完成，位于 `tide_rl/` 目录：

```
tide_rl/
├── tide_features.py     # 维度常量与辅助函数
├── tide_encoder.py      # 简单 Dense 编码器（3 个）
├── tide_env.py          # Gymnasium 环境（stdio 桥接）
├── tide_agent.py        # 简化 RNN Actor-Critic
├── train_tide.py        # 最小 PPO 训练脚本
└── __init__.py          # 模块初始化
```

---

## 1. `tide_features.py` — 维度常量

对应 ygo-agent 的 `features.py`，定义 Tide 特征维度：

- `N_CARD_FEATURES = 65`（2026-09-10 目标域模型 71→65）（不是 YGO 的 41；含内容身份三通路：精确哈希/EffectType/参数块）
- `N_GLOBAL_FEATURES = 32` （不是 YGO 的 23）
- `N_ACTION_FEATURES = 6` （不是 YGO 的 12）
- `MAX_CARDS = 80`
- `MAX_ACTIONS = 24`

辅助函数：
- `sample_input()` — 生成样本输入（模型初始化用）
- `init_rstate()` — 初始化 RNN 状态
- `pad_or_truncate_actions()` — 动作列表 pad 到固定长度

---

## 2. `tide_encoder.py` — 简单编码器

**关键设计（路线 1）**：不做 YGO 式的 embedding lookup / spec gather，直接 Dense 投影扁平特征。

### 三个编码器：

1. **`TideCardEncoder`**
   - 输入：`(batch, 80, 20)` 卡牌特征
   - 输出：`(batch, 80, channels)` 编码后特征
   - 实现：直接 `Dense(channels)` 投影 20 维 → channels

2. **`TideGlobalEncoder`**
   - 输入：`(batch, 32)` 全局特征
   - 输出：`(batch, channels)` 编码后特征
   - 实现：直接 `Dense(channels)` 投影

3. **`TideActionEncoder`**
   - 输入：`(batch, max_actions, 6)` 动作特征
   - 输出：`(batch, max_actions, channels)` 编码后特征
   - 实现：直接 `Dense(channels)` 投影

### `TideEncoder` — 总编码器

输入 `obs_dict` → 三路编码 → 输出编码特征 + 掩码：

```python
{
    "cards": (batch, 80, channels),
    "global": (batch, channels),
    "actions": (batch, max_actions, channels),
    "c_mask": (batch, 80),      # True=空槽需屏蔽
    "a_mask": (batch, max_actions)  # True=非法动作需屏蔽
}
```

---

## 3. `tide_env.py` — Gymnasium 环境

通过 stdio 与 Unity batchmode 桥接的 Gymnasium 环境。

### 协议

**Reset**:
```json
发送: {"op":"reset","deck1":[...],"deck2":[...]}
接收: {"obs":{...},"info":{...}}
```

**Step**:
```json
发送: {"op":"step","action":N}
接收: {"obs":{...},"done":false,"info":{...}}
```

### 奖励计算

```python
# 终局奖励（主导信号）
if done:
    if winner == 1: reward = +1.0
    elif winner == 2: reward = -1.0
    else: reward = 0.0  # 平局/超时

# 塑形奖励（引导信号）
else:
    Φ_current = obs["global_"][28] - obs["global_"][29]  # TotalValue(me) - TotalValue(opp)
    reward = lambda * (Φ_current - Φ_last)
```

**λ 建议值**：`0.01~0.05`（v2 全资源势能变化更大，需降低 λ）

### 启动 Unity

```python
env = TideEnv(
    unity_path=r"C:\...\Unity.exe",  # 或从环境变量 UNITY_PATH 读取
    project_path=r"E:\UnityProject\Tide",
    reward_lambda=0.02,
)
```

Unity 命令行：
```bash
Unity.exe -batchmode -nographics \
  -projectPath E:\UnityProject\Tide \
  -executeMethod CardCore.Editor.TideHeadless.TideHeadlessServer.Main \
  -logFile -
```

---

## 4. `tide_agent.py` — 简化 Actor-Critic

路线 1 简化架构（不用 Transformer/FiLM）：

```
Encoder → Cards池化 → GRU → 增强Global特征 → Actor/Critic
```

### `TideActor`

- 输入：编码后特征（cards/global/actions + 掩码）
- 输出：`(batch, max_actions)` logits
- 实现：
  1. Cards 池化（mean，屏蔽空槽）
  2. 全局上下文 = global + cards_pooled
  3. MLP 提取上下文特征
  4. 逐动作打分：actions 与 context 点积
  5. 屏蔽非法动作（-inf）

### `TideCritic`

- 输入：编码后特征
- 输出：`(batch,)` 标量价值
- 实现：global + cards_pooled → MLP → 标量

### `TideRNNAgent`

- 完整模型（对应 ygo-agent 的 `RNNAgent`）
- RNN 类型：`gru` 或 `lstm`（默认 GRU）
- 调用：`new_rstate, logits, value = agent(rstate, obs_dict)`

---

## 5. `train_tide.py` — 最小 PPO 训练

仿 cleanba.py 精简版（单机串行 env）：

### 超参（`Args`）

```python
learning_rate: 2.5e-4
num_envs: 4                # 串行 env 数量（CPU 限制）
num_steps: 128             # 每次 rollout 步数
gamma: 0.99
gae_lambda: 0.95
clip_coef: 0.1
ent_coef: 0.01
vf_coef: 0.5
total_timesteps: 1_000_000  # 100 万步（约 2~3k 局）
reward_lambda: 0.02         # v2 全资源势能，降低塑形强度
```

### 训练循环

```
for update in 1..num_updates:
    # 1. Rollout（串行收集）
    for env_id, env in envs:
        obs, actions, rewards, dones, values = rollout(env, num_steps)

    # 2. Compute GAE（复用 ygo-agent）
    advantages, returns = compute_gae(rewards, dones, values, gamma, gae_lambda)

    # 3. PPO update（复用 ygo-agent）
    loss = ppo_loss(logits, actions, advantages, returns, values, old_logprobs)
    train_state = train_state.apply_gradients(grads=grad(loss))

    # 4. 日志 / 评估 / 保存
```

### 依赖 ygo-agent PPO 损失

```python
from ygoai.rl.jax import compute_gae, ppo_loss
```

需要把 `ygo-agent-main` 加入 Python path：
```python
sys.path.insert(0, "E:/UnityProject/Tide/ygo-agent-main")
```

---

## 环境变量设置

### Windows (PowerShell)

```powershell
$env:UNITY_PATH = "C:\Program Files\Unity\Hub\Editor\2022.3.44f1c1\Editor\Unity.exe"
$env:PYTHONPATH = "E:\UnityProject\Tide\ygo-agent-main;$env:PYTHONPATH"
```

### Linux / Mac (Bash)

```bash
export UNITY_PATH="/path/to/Unity"
export PYTHONPATH="/path/to/ygo-agent-main:$PYTHONPATH"
```

---

## 安装依赖

```bash
pip install jax[cpu] flax optax gymnasium numpy
```

**注意**：JAX CPU 版足够（GPU 版需 CUDA，但训练规模不大时 CPU 即可）

---

## 运行训练

```bash
cd E:\UnityProject\Tide
python tide_rl/train_tide.py
```

---

## 导出 ONNX 到 Unity（Sentis 部署链路）

训练 checkpoint → 单步推理 ONNX → Unity Sentis（包 `com.unity.ai.inference`，命名空间
`Unity.InferenceEngine`）本地推理。**导出全程用隔离 venv `.venv-export`，不碰训练环境。**

### 一次性准备

```bash
cd tide_rl
python -m venv .venv-export
.venv-export/Scripts/python.exe -m pip install "jax[cpu]==0.11.1" "flax==0.12.9" jax2onnx onnxruntime
```

### 导出（训练中随时可跑，默认取最新 selfplay run 的 best）

```bash
cd tide_rl
.venv-export/Scripts/python.exe export_onnx.py                # best checkpoint
.venv-export/Scripts/python.exe export_onnx.py --ckpt logs/<run>/last/params.msgpack
```

产出（自动复制到 `Assets/StreamingAssets/`）：
- `tide_policy.onnx` —— 推理图（batch 已脱皮：rstate 512 / cards 80×65 / global 32 /
  actions 128×6 → rstate_next / logits(非法已掩 -1e9) / value），manifest 指纹等元数据在
  model metadata；opset 23，全标准算子，3.3MB
- `tide_policy_fixture.json` —— 数值对拍样例（JAX 参考输出）

脚本内置两道验证：onnxruntime vs JAX 逐输出对拍（容差 5e-3，实测 ~2e-6）+ 非法动作
掩码抽检。已知坑：jax2onnx 0.16.1 的 JitPlugin 与 jax 0.11.1 的 `Var` 内部签名不兼容，
`export_onnx.py` 头部已带猴子补丁（新 jax 自动退位）；给 `to_onnx` 传的函数不能自带
`@jax.jit`。

### Unity 侧验证（编辑器菜单）

- `Tools/AI/ONNX 策略/1. 数值对拍 (fixture)` —— Sentis 后端 vs JAX 参考输出
- `Tools/AI/ONNX 策略/2. vs SimpleAI 20 局` —— 口径镜像训练评估（模型三色随机 /
  SimpleAI 恒红、随机座次、洗牌抽 30），胜率可与训练日志 eval win_rate 直接对照

### Unity 侧代码

| 文件 | 角色 |
|---|---|
| `Assets/Scripts/AI/NeuralAI/OnnxTidePolicy.cs` | Sentis 推理器（rstate 管理 + argmax） |
| `Assets/Scripts/AI/NeuralAI/NeuralAI.cs` | 整回合驱动器（SimpleAI 同形，`TakeTurn(BattleController)` 可直接替换 `BattleController` 里的 SimpleAI） |
| `Assets/Editor/NeuralAI/OnnxPolicyMenu.cs` | 上面两个验证菜单 |

活体对局接入：`BattleController.RunAiTurn` 里把 `_ai.TakeTurn(this)` 换成
`_neural.TakeTurn(this)`，对局开始时调 `ResetEpisode()`（GRU 状态归零）。

### 部署约束（改任何一处都要重导出/重训）

- **卡身份 manifest 必须同一份**：`TideCardIndex` 绑定 `tide_rl/card_identity_manifest.json`
  （追加式），embedding 行号才不串台。onnx metadata 里存了导出时的 manifest sha256。
- 观测/动作布局（65/32/6 维、MAX_ACTIONS=128）改了 → 重导出；原子表（97 条 EffectType）
  变更 → 全体身份换血，需重训。
- 自对弈权重是「当前回合玩家」视角：部署喂模型自己回合的决策点即可（与训练评估口径一致）。

---

## 已知 TODO（train_tide.py 占位符）

当前 `train_tide.py` 是**骨架**，以下部分是占位符（需完整实现）：

1. **Rollout 合并 → Batch**
   - 当前是逐 env 串行收集，需合并成 batch

2. **GAE 计算**
   - 调用 `ygoai.rl.jax.compute_gae`

3. **PPO Loss + Update**
   - 调用 `ygoai.rl.jax.ppo_loss`
   - 计算梯度并更新参数

4. **评估 vs SimpleAI**
   - 定期跑 N 局 vs SimpleAI，统计胜率

5. **模型保存/加载**
   - 保存 params 到 checkpoint

**推荐优先级**：先完成 1-3（训练循环闭环），再做 4-5（评估/保存）。

---

## Phase 3 下一步

### ✅ 已完成
- 维度常量与辅助函数 ✅
- 三个简单 Dense 编码器 ✅
- Gymnasium 环境（stdio 桥接）✅
- 简化 RNN Actor-Critic ✅
- 训练脚本骨架 ✅

### 🔧 待完成（补全训练循环）
1. 实现完整 rollout → batch 合并
2. 调用 ygo-agent 的 `compute_gae`
3. 调用 ygo-agent 的 `ppo_loss` + 梯度更新
4. 评估 vs SimpleAI（胜率统计）
5. 模型保存/加载

### 🎯 验证目标
- Loss 下降（PPO loss, value loss, entropy）
- vs SimpleAI 胜率 >50%
- 对局长度稳定（不过早投降、不无限循环）
- 不出现囤积资源不用的行为

---

## 与 ygo-agent 差异对比

| 项 | ygo-agent | Tide (路线 1) |
|---|---|---|
| **特征维度** | cards 41, global 23, actions 12 | cards 20, global 32, actions 6 |
| **编码器** | YGO 专属（code_id embedding, attribute/race 分桶） | 简单 Dense 投影（不做 embedding lookup） |
| **模型骨干** | Transformer + RNN + FiLM | GRU + MLP（简化） |
| **规则引擎** | C++ (ygopro-core) + pybind11 | Unity C# + batchmode stdio |
| **PPO 损失** | 复用 ygoai.rl.jax | 复用 ygoai.rl.jax |
| **训练规模** | 多机 + envpool（并行） | 单机 + 串行 env（简化） |

---

## 文件结构总览

```
E:\UnityProject\Tide\
├── Assets/Scripts/AI/NeuralEnv/
│   ├── TideObservation.cs           # ✅ Phase 1（80×20 + 32）
│   ├── LegalActionEnumerator.cs     # ✅ Phase 1（动作枚举 + 6 槽）
│   ├── FieldValueReward.cs          # ✅ Phase 1（v2 全资源动态势能）
│   ├── TideHeadlessDriver.cs        # ✅ Phase 2（reset/step 驱动器）
│   └── TideHeadlessServer.cs        # ✅ Phase 2（batchmode stdio 入口）
├── tide_rl/
│   ├── tide_features.py             # ✅ Phase 3（维度常量）
│   ├── tide_encoder.py              # ✅ Phase 3（简单编码器）
│   ├── tide_env.py                  # ✅ Phase 3（Gymnasium 环境）
│   ├── tide_agent.py                # ✅ Phase 3（RNN Actor-Critic）
│   ├── train_tide.py                # 🔧 Phase 3（训练脚本骨架）
│   └── __init__.py
├── ygo-agent-main/                  # 复用 PPO 损失
└── FIELD_VALUE_REWARD_V2.md         # 文档
```
