# Tide AI 训练快速开始指南

## 🎯 目标

训练一个能**稳赢 SimpleAI**（胜率 >75%）的打牌 AI。

---

## 🔧 两种桥接模式

### 模式 1：TCP 连接（推荐开发/调试）

**优点**：
- Unity 只启动一次，快速
- 实时日志在 Unity Console
- 可在 Scene 窗口观察状态
- 调试方便（断点、Inspector）

**使用步骤**：
1. 打开 Unity 编辑器，加载 Tide 项目
2. 菜单点击：`Tools > AI > 启动训练服务器 (TCP 9999)`
3. Unity Console 显示：`[TideHeadless] TCP 服务器启动在 localhost:9999，等待 Python 连接...`
4. 运行 Python 测试：`python tide_rl/test_bridge_tcp.py`
5. 或运行训练（使用 `TideEnvTcp`）

**停止服务器**：菜单点击 `Tools > AI > 停止训练服务器`

### 模式 2：Batchmode 自动启动（适合无人值守训练）

**优点**：
- Python 一键启动，自动管理 Unity 生命周期
- 适合远程服务器、多进程并行

**缺点**：
- Unity 启动慢（10-30秒）
- 日志调试不便

**使用**：原 `TideEnv` 类（当前暂时有 stdout 混乱问题，建议先用 TCP 模式）

---

## ✅ 前置条件检查

### 1. 软件环境

- [x] **Python 3.8+** 已安装
- [x] **Unity 2022.3.44f1c1** 已安装
- [x] **JAX, Flax, Optax, Gymnasium** Python 包
- [x] **ygo-agent-main** 在项目根目录

### 2. 文件检查

```
E:\UnityProject\Tide\
├── Assets/Scripts/AI/NeuralEnv/   ✅ C# 桥接（Phase 1-2）
├── tide_rl/                        ✅ Python 训练（Phase 3）
├── ygo-agent-main/                 ✅ 复用 PPO 损失
└── logs/                           （自动创建）
```

### 3. 环境变量

**Windows PowerShell**:
```powershell
$env:UNITY_PATH = "C:\Program Files\Unity\Hub\Editor\2022.3.44f1c1\Editor\Unity.exe"
$env:PYTHONPATH = "E:\UnityProject\Tide\ygo-agent-main;$env:PYTHONPATH"
```

**Linux/Mac Bash**:
```bash
export UNITY_PATH="/path/to/Unity"
export PYTHONPATH="/path/to/ygo-agent-main:$PYTHONPATH"
```

---

## 🚀 快速启动（3 步）

### 快速测试（TCP 模式 - 推荐）

**步骤**：
1. 打开 Unity 编辑器
2. 菜单：`Tools > AI > 启动训练服务器 (TCP 9999)`
3. 运行测试：
```bash
cd E:\UnityProject\Tide
python tide_rl/test_bridge_tcp.py
```

成功输出：
```
[✓] 环境创建成功
[✓] Reset 成功
  Legal actions: 12
[✓] Step 测试成功
✓ TCP 桥接测试通过！
```

### 方式 1：一键启动（推荐）

**Windows**:
```powershell
cd E:\UnityProject\Tide\tide_rl
.\start_training.ps1
```

或双击 `start_training.bat`

**Linux/Mac**:
```bash
cd /path/to/Tide/tide_rl
chmod +x start_training.sh
./start_training.sh
```

### 方式 2：手动启动

```bash
# 1. 设置环境变量（见上方）

# 2. 安装依赖
pip install jax[cpu] flax optax gymnasium numpy

# 3. 启动训练
cd E:\UnityProject\Tide\tide_rl
python train_tide_complete.py
```

---

## 📊 监控训练进度

### 实时监控（推荐）

**新开一个终端**：
```bash
cd E:\UnityProject\Tide\tide_rl
python monitor_training.py
```

会实时显示：
- 最新指标（Loss, PG Loss, VF Loss, Entropy, Episode Return/Length）
- 最近平均（最近 10 次更新）
- 评估结果（vs SimpleAI 胜率）
- 进度条（步数 / 总步数）
- ETA（预计完成时间）

### 查看日志

```bash
# 查看最新运行总结
python monitor_training.py --summary

# 查看完整日志
tail -f logs/train_YYYYMMDD_HHMMSS.log
```

---

## 🎓 训练参数说明

默认参数（`train_tide_complete.py`）：

| 参数 | 默认值 | 说明 |
|---|---|---|
| `total_timesteps` | 2,000,000 | 总训练步数（约 5~10k 局） |
| `num_envs` | 4 | 并行环境数量 |
| `num_steps` | 256 | 每次 rollout 步数 |
| `learning_rate` | 2.5e-4 | 学习率（线性衰减） |
| `reward_lambda` | 0.02 | 塑形强度（v2 全资源） |
| `eval_interval` | 5,000 | 每 5k 步评估一次 |
| `eval_episodes` | 20 | 每次评估 20 局 |
| `target_win_rate` | 0.75 | 目标胜率 75% |
| `patience` | 5 | 连续 5 次达标即停止 |

### 修改参数

编辑 `train_tide_complete.py` 的 `Args` dataclass：

```python
@dataclass
class Args:
    total_timesteps: int = 1_000_000  # 改为 100 万步（快速测试）
    reward_lambda: float = 0.01       # 降低塑形强度
    target_win_rate: float = 0.80     # 提高目标胜率到 80%
    # ...
```

或在代码末尾修改：

```python
if __name__ == "__main__":
    args = Args()
    args.total_timesteps = 1_000_000  # 快速测试
    args.target_win_rate = 0.60       # 降低目标（快速验证）
    train(args)
```

---

## 📈 训练进度示例

**正常训练输出**：
```
Update   100/7812 | Step    25600 | Loss   0.524 | PG   0.123 | VF   0.385 | Ent  2.341 | EpRet   -0.23 | EpLen  87.3 | SPS  1234 | Time 0.85s
Update   200/7812 | Step    51200 | Loss   0.412 | PG   0.089 | VF   0.312 | Ent  2.198 | EpRet    0.15 | EpLen  92.1 | SPS  1256 | Time 0.81s
...

============================================================
Evaluating 20 episodes vs SimpleAI...
============================================================
  Episode 1/20: WIN (length=134)
  Episode 2/20: LOSS (length=89)
  Episode 3/20: WIN (length=156)
  ...

============================================================
Evaluation Results:
  Win Rate: 45.0% (9/20)
  Losses: 10, Draws: 1
  Avg Episode Length: 118.3
============================================================

✓ New best model saved (win_rate=45.0%)
```

**达标停止**：
```
============================================================
Evaluation Results:
  Win Rate: 78.0% (15/20)
  Losses: 4, Draws: 1
  Avg Episode Length: 142.5
============================================================

✓ New best model saved (win_rate=78.0%)
✓ Target win rate reached! (5/5)

================================================================================
Training complete! Win rate 78.0% >= target 75.0%
================================================================================
```

---

## 🎯 评估指标解读

### 训练指标

- **Loss**：总损失（越低越好，但不是唯一标准）
- **PG Loss**：策略梯度损失（衡量策略更新幅度）
- **VF Loss**：价值函数损失（衡量价值估计准确性）
- **Entropy**：策略熵（越高越探索，训练初期应较高）
- **Ep Return**：Episode 回报（平均每局累计奖励）
  - 初期接近 0（随机策略）
  - 中期逐渐上升（学会打牌）
  - 后期稳定在正值（稳赢 SimpleAI）
- **Ep Length**：Episode 长度（平均每局步数）
  - 过短（<30）：可能过早投降
  - 过长（>300）：可能陷入循环
  - 合理范围：50~200 步

### 评估指标

- **Win Rate**：vs SimpleAI 胜率（目标 >75%）
- **Avg Episode Length**：平均对局长度
  - 稳赢时通常 100~150 步（快速压制）

---

## 🛠️ 常见问题排查

### 1. Unity 启动失败

**现象**：`Unity process stdout closed`

**原因**：
- Unity 路径错误
- Unity 未关闭编辑器（撞 UnityLockfile）
- batchmode 启动失败

**解决**：
```bash
# 检查 Unity 路径
echo $env:UNITY_PATH

# 关闭 Unity 编辑器
taskkill /IM Unity.exe /F

# 手动测试 batchmode
& "C:\...\Unity.exe" -batchmode -nographics -projectPath E:\UnityProject\Tide -executeMethod CardCore.Editor.TideHeadless.TideHeadlessServer.Main -logFile -
```

### 2. Python 导入错误

**现象**：`ModuleNotFoundError: No module named 'ygoai'`

**解决**：
```bash
# 检查 PYTHONPATH
echo $env:PYTHONPATH

# 手动设置
$env:PYTHONPATH = "E:\UnityProject\Tide\ygo-agent-main;$env:PYTHONPATH"

# 测试导入
python -c "from ygoai.rl.jax import truncated_gae; print('OK')"
```

### 3. 训练不收敛

**现象**：Loss 不下降，Ep Return 持续为 0

**可能原因**：
- λ 过大（塑形奖励主导，终局信号被掩盖）
- 学习率过高/过低
- GAE 计算错误

**解决**：
```python
# 降低塑形强度
args.reward_lambda = 0.01  # 从 0.02 降到 0.01

# 调整学习率
args.learning_rate = 1e-4  # 从 2.5e-4 降到 1e-4

# 检查奖励符号
# 在 tide_env.py step() 中打印：
print(f"Reward: {reward:.4f}, Done: {done}, Winner: {info.get('winner')}")
```

### 4. Episode 长度异常

**过短（<30）**：
- AI 过早投降 → 检查终局奖励是否正确（赢 +1，输 -1）

**过长（>300）**：
- AI 陷入循环 → 检查动作枚举是否遗漏 `PassPriority`/`EndTurn`
- 超时机制未生效 → 检查 `max_steps` 设置

---

## 💾 模型保存与加载

### 保存位置

```
logs/tide_ppo_beat_simpleai__42__1693827456/
├── config.json               # 训练配置
├── best_model/
│   └── params.msgpack        # 最优模型（胜率最高）
├── checkpoint_20000/
│   └── params.msgpack        # 中间 checkpoint
└── checkpoint_40000/
    └── params.msgpack
```

### 加载模型

```python
import flax.serialization
from tide_rl.tide_agent import create_tide_agent

# 创建模型
agent = create_tide_agent(channels=128, rnn_channels=512)

# 加载参数
with open("logs/.../best_model/params.msgpack", "rb") as f:
    params = flax.serialization.from_bytes(agent.init(...), f.read())

# 使用模型
rstate, logits, value = agent.apply(params, rstate, obs)
action = int(jnp.argmax(logits))  # greedy
```

---

## 🎮 使用训练好的模型

TODO（Phase 5）：
1. 导出模型为 Unity 可用格式（ONNX / TFLite）
2. 在 Unity 中集成推理
3. UI 选择「AI vs SimpleAI」对战

---

## 📊 预期训练时间

**硬件配置**：
- CPU: Intel i7-10700K (8 核)
- RAM: 32GB
- 无 GPU（JAX CPU 版）

**预期时间**：
- **100 万步**：约 2~3 小时
- **200 万步**：约 4~6 小时

**速度提升**：
- 增加 `num_envs`（并行环境数）
- 使用 GPU（JAX GPU 版，需 CUDA）
- 降低 `num_steps`（rollout 长度）

---

## 🔍 调试技巧

### 1. 打印 Φ 变化（验证奖励）

在 `tide_env.py` 的 `step()` 中：

```python
# 塑形奖励
current_potential = obs["global_"][28] - obs["global_"][29]
shaping = self.reward_lambda * (current_potential - self.last_potential)
print(f"[Step {self.step_count}] Φ: {self.last_potential:.2f} → {current_potential:.2f}, Shaping: {shaping:.4f}")
reward = shaping
self.last_potential = current_potential
```

### 2. 记录对局轨迹

```python
# 在 rollout_single_env() 中
trajectory = {
    "obs": obs_list,
    "actions": actions_list,
    "rewards": rewards_list,
    "potentials": [obs["global_"][28] - obs["global_"][29] for obs in obs_list],
}

# 保存到文件
import pickle
with open(f"trajectory_{episode_id}.pkl", "wb") as f:
    pickle.dump(trajectory, f)
```

### 3. 可视化训练曲线

```bash
# 使用 TensorBoard（TODO：集成）
tensorboard --logdir=logs/
```

---

## 🎯 成功标准

**Phase 4 目标达成**：
- ✅ Loss 下降（从 0.5~1.0 降到 0.1~0.3）
- ✅ Ep Return 上升（从 0 附近升到 0.5+）
- ✅ vs SimpleAI 胜率 >75%
- ✅ Episode 长度稳定（50~200 步）
- ✅ 无明显异常行为（囤资源不用、过早投降、无限循环）

**下一步（Phase 5）**：
- 完整关键词 multi-hot（18 个关键词）
- Transformer 骨干（替换 GRU + MLP）
- 目标枚举（v1 用 SimpleAI.ChooseTargets，v2 展开）
- 多机并行训练（envpool + 分布式）

---

## 📞 获取帮助

如遇问题：
1. 查看日志：`logs/train_*.log`
2. 查看 Unity Console 输出（batchmode 日志）
3. 检查 C# 编译错误：`dotnet build Assembly-CSharp.csproj`
4. 运行监控脚本：`python monitor_training.py`

---

**开始训练吧！🚀**

```powershell
cd E:\UnityProject\Tide\tide_rl
.\start_training.ps1
```
