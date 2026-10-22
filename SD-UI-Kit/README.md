# SD-UI-Kit —— 意图 → 生图指令 的组件级流水线

把"我想改某个界面的美术"翻译成生图工具真正可执行的指令。
**不做一次性生成整屏复合 UI**；按 Unity 组件逐类出候选料（面板/按钮/行/槽/输入条…），
尺寸对齐 Unity 布局真实值，用户挑选拼装。

## 工作流

```
你: "把 EffectComposerScreen 换成暗金魔法风"
 │
 1. read_unity_ui.py <prefab>        → 组件清单（名称/类型/尺寸）
 2. （我）筛视觉件 → 写 manifests/<task>.json（尺寸档 + 组件类型 + 风格名）
 3. dispatch.py manifests/<task>.json → 形状模板底图 → img2img → output/<task>/*.png + index.html
 4. 你在 index.html 里挑选 → 告诉我选中的编号/seed
 5. 我把选中的 PNG 处理（抠图/九切片导入）拷入 Assets 接线，或你自己拼
```

## 文件

| 文件 | 作用 |
|---|---|
| `read_unity_ui.py` | 解析 prefab YAML（只读）→ Image/Button/Raw 组件清单 JSON |
| `dispatch.py` | manifest → 批量 img2img；断点续跑（已存在跳过）；`--scan` 做 denoise 扫描；`--dry-run` 只算尺寸 |
| `component_specs.json` | 组件类型 → 形状模板（凸面/凹面/空心/平铺）+ 提示词片段 + denoise/pad 默认 |
| `styles.json` | 风格预设：prefix(可含 `<lora:名:权重>`) / negative / sampler / steps / cfg / checkpoint |
| `evaluate.py` | 形状保持量化：输出图 vs 模板的结构相关分（挑 denoise 区间用） |
| `manifests/` | 每次任务的清单（组件尺寸来自步骤 1，风格/变体数来自你的意图） |
| `output/<task>/` | 候选 PNG（`<id>_vN.png` / 扫描 `<id>_dnX.XX_vN.png`）+ `index.html` 联络表 + `style_snapshot.json` 当轮参数快照（风格/规格/denoise 档全量，换风格实验可复现可对比） |

## 尺寸换算（dispatch.py 内建）

- Unity 布局尺寸 × pad 外扩（组件默认 1.1~1.2，面板 1.06）
- 长宽比 clamp 到 [1/2.8, 2.8]（SD1.5 超过会畸变；超长条出图后拉伸/九切片）
- 面积归一到 base_px²（全线 1024，SDXL 档），64 对齐，边限 [256,1024]

## 校准结论

**核心机制：风格底色必须画进模板（tint），不能指望提示词。**
img2img 低 denoise 保留 init 图的低频信息（颜色布局）——灰模板永远出灰图，
提示词只负责纹饰细节。`styles.json` 每风格配 `tint_bg/tint_face/tint_edge` 三色。
（2026-10-10 深夜追加：tint_edge 换暖色即可承载"点缀色"——暗红 edge 落位 red% 按钮 13-14%/面板 4%/行 2%；
负面词写 gold/golden 会泛化压制整个暖色系，杀点缀色。）

### 当前定档（2026-10-10，SDXL base + game-icon-diablo@0.7）

| denoise | 形状保持(edge-corr) | 适用 |
|---|---|---|
| ≤0.55 | 0.9+ | 僵硬无风格，不用 |
| **0.65** | **0.76~0.97** | **几何件定档（七类已写入 component_specs）** |
| 0.70 | 边界 | 工作带上限 |
| 0.75+ | 0.24~0.84 崩 | 禁用于几何件 |

- 定稿风格 `silver-crimson`：银灰 bg/face + 暗红 edge、LoRA 0.7、DPM++ 2M SDE / 30 步 / CFG 6
- 出图 ~35-40s/张（1024 基准，RX 6900 XT）；工作带 0.60-0.70
- 边缘-only 浮雕：prompt "confined to edges and border" + 负面 "central ornament" 生效（edge/center 细节密度比 1.4~2.7）
- icon(0.80)/background(0.85) 未在 SDXL 重校准，首用需自扫
- 评估脚本：`python evaluate.py output/<task>`（形状相关分）；色彩/边缘密度量化脚本见 memory（sat/warm%/red%/edge-center）

> 历史注记：anything-v5 (SD1.5) 时代区间为 0.55 几何 / 0.65 上限 / 4.4s/张，已被上表取代。

## 状态与待定项

- **主模型/LoRA 已定（2026-10-10）**：SDXL base（Open RAIL++-M 可商用）+ game-icon-diablo LoRA@0.7（同许可）。
  定稿风格 `silver-crimson`（快照见 `output/calibration-silver2/style_snapshot.json`）。
  **试新风格标准动作**：styles.json 加条目（tint 三色/prefix/LoRA 权重）→ 复制 manifest 改 task+style → 跑 dispatch，
  参数快照自动落该风格自己的文件夹。
- 透明通道：出图为纯底 PNG，挑选后走抠图（BiRefNet 路线已有）或按九切片直用
- seed 复现：`index.html` 每图记录 seed；manifest 里 `"seed": <int>` 固定种子（缺省 -1 随机）
- TMP 文本、guide-frame shader 件、下拉运行时模板不在生图范围
