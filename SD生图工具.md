# SD 生图工具使用规则

工具目录：`SD-UI-Kit/`（本仓库根下，Assets 外）。
定位：把"我想改某个界面的美术"翻译成生图工具（Aki A1111, http://127.0.0.1:7860）真正可执行的组件级指令。
**不生成整屏复合 UI**，按 Unity 组件类型逐类出候选料，尺寸对齐布局真实值，人工挑选拼装。


## 每步规则（执行约定）

1. **提清单**：`python SD-UI-Kit/read_unity_ui.py <prefab路径>`（只读解析，不动 Unity 资产）→ 筛掉 TMP 文本、下拉运行时模板、shader 件，保留视觉件（面板/按钮/行/槽/输入条）。
2. **写 manifest**：`SD-UI-Kit/manifests/<任务名>.json`——组件 id + 类型 + Unity 尺寸 + 风格名 + 变体数。
3. **生成**：`python SD-UI-Kit/dispatch.py manifests/<任务名>.json`（断点续跑；`--scan` 扫参数；`--dry-run` 预演尺寸）。
4. **交付**：候选图 + `output/<任务名>/index.html` 联络表（浏览器打开，每图带 seed 可复现）。
5. **挑选由人做**，挑中的报编号/seed 给助手，由助手抠图导入 Assets 接线（BiRefNet 抠图路线已有），或自行拼装。

## 已校准的控制方法（2026-10-10，anything-v5/SD1.5）

- **生成方式**：图生图 + 染色形状模板。风格底色画进模板（styles.json 每风格 `tint_bg/tint_face/tint_edge` 三色），提示词只管纹饰——灰模板永远出灰图（低 denoise 保留 init 低频色，实测饱和度差 10 倍）。
- **denoise 区间**：几何件（按钮/面板/行/输入条）0.55；要纹饰上限 0.65；0.75+ 仅 icon/背景高自由度件（形状开始崩，edge-corr < 0.88）。
- **尺寸换算**：Unity 尺寸 × pad 外扩 → 长宽比 clamp [1/2.8, 2.8] → 面积归一（组件 512/面板 768 基准）→ 64 对齐 → 边限 [256,1024]。超长条（如 718×60 行）出图后拉伸/九切片。
- **速度**：~4.4s/张（SD1.5）。

## 2026-10-11 追加

- **item 级覆盖**：manifest 每项可覆盖 `style`（一个 manifest 横评多 LoRA）/ `tint_bg|tint_face|tint_edge`（六色元素等）/ `flat`（短边<50 默认素面条，图标类设 false 强制 SD）/ `denoise`/`seed`。
- **新组件类型**：`frame_ring`（结界圆环）/`frame_tri`（法术三角，圆角折线+鎏金中线）/`frame_square`（生物方框）/`banner`（卡名横幅）/`kwgem`（元素宝珠——tint_edge 是唯一区分度必须画进 init，`kwicon`(plain) 只染底色不适用）。
- **gfx1030 + SDXL img2img 间歇性 Unet NaN**：与 LoRA/尺寸无关、txt2img 不触发、模型热后消失；`call_img2img` 已内置隔 3s 重试一次。密集复发时重启 webui 加 `--no-half-vae`。

## 2026-10-11 深夜补：SD1.5(anything-v5) 校准带实测

- **SD1.5 对负面服从度极高但风格化保守**：形状分普遍 0.88-0.99（evaluate.py 目标带=0.60-0.85 风格化区间）。要进风格化带须把 denoise 拉到 0.70-0.75（SDXL 时代 0.58 锁形点不迁移）。几何件在 SD1.5 上的工作带整体后移 ≈+0.10。
- 石板大理石批（activation-slab-marble）四档实测：0.55/0.65＝模板复读机；**0.70-0.75 进目标带**（0.68-0.91），高光斑全 0%。

## 2026-10-11 深夜追加

- **底模判死（用户定性裁决）**：`sd_xl_base_1.0` 完全不适合做 UI，只适合暗黑风格徽章——10-10 起候选一直差强人意的根因。styles.json 全部条目 `checkpoint` 已置 **null**：批次不再强制加载底模，出图模型=webui 当前加载的模型；跑批前先在 webui 里切好模型。模型文件留盘。
- **新底模接入三步**：模型落 `models/Stable-diffusion/` → styles 条目 `checkpoint` 填 `路径 [hash]`（hash 查 `/sdapi/v1/sd-models`，[hash] 可省=按文件名匹配）→ tint 三色与 denoise 区间全套重扫（换底模=此前校准作废）。

## 2026-10-12 追加

- **新组件类型 `elem_glyph`**（六元素线条图案）：图案几何程序化画进 init（`glyph` 参数走 manifest item 级覆盖），tint 语义=face 晕光环色/edge 核心线条色/bg 暗底；颜色锚=游戏内 UiStyle.Dot*；配套风格 `elem-line`。r3 起支持**变宽笔锋**（书法式粗细流转）；r5 起字形来源=**甲骨文**（huo/shui/mu/ri/yue/xing + 黑意象 hei/ming/luan/xuan，旧具象六形保留在注册表）。详见决策账 #10。
- **新组件类型 `card_back`**（统一卡背，2026-10-12）：金饰边带+深暗内场+中心 44% 宽符号槽（四角金点对位标，元素符号引擎后期合成）；配套风格 `cardback-void`；中心纪律甜点=dn 0.65-0.70（0.75 SD 污染中心）。详见决策账 #11。

## 文件地图

| 路径 | 内容 |
|---|---|
| `SD-UI-Kit/read_unity_ui.py` | prefab → 组件清单（Image GUID 按前缀 fe87c0e1cc204ed48ad3b3 识别，兼容两个尾段变体） |
| `SD-UI-Kit/dispatch.py` | manifest → 模板 → img2img → PNG+联络表 |
| `SD-UI-Kit/component_specs.json` | 组件类型定义（形状/提示词/denoise/pad） |
| `SD-UI-Kit/styles.json` | 风格预设（prefix/negative/采样/tint 三色/checkpoint） |
| `SD-UI-Kit/evaluate.py` | 形状保持量化（边缘相关+明度结构） |
| `SD-UI-Kit/manifests/` | 任务清单 |
| `SD-UI-Kit/output/<任务>/` | 候选图 + index.html + shape_scores.json |
| `SD-UI-Kit/美术决策账.md` | 美术决策账：每轮裁决+原因+保留 seed；锚点=2026-10-11 流动背景定档（深空×暗夜鎏金+配合法则） |

## FLUX.2 支线（独立于上方 A1111 流水线，2026-10-11）

`C:\ComfyUI`：FLUX.2 klein 4B（Apache 2.0 可商用）跑在 RX 6900 XT 上，AMD 原生 ROCm。**启动用 `C:\ComfyUI\launch_flux2.bat`**（参数 `--disable-async-offload --disable-pinned-memory --reserve-vram 0.9 --cpu-vae` 缺一不可，GPU VAE 在 gfx1030 上崩）。文生图工作流模板 `C:\ComfyUI\flux2_klein_t2i.json`，API 端口 8188。约 3 分钟/张 1024²。定位=通用写实向（文字渲染/复杂版式），不承载二次元厚涂主线。

## 待定项

1. ~~改 `styles.json`：`checkpoint` 字段 + prefix 里加 `<lora:名:权重>`~~ —— 已有 5 条 `cosmic-*` 条目（2026-10-11 宇宙系五 LoRA 横评，tint=暗夜鎏金三色锚）。
2. 五 LoRA 横评出料（`output/icons-cosmic5/` + `output/cardbg-shapes5/`）→ 用户挑选 → 胜者定为主力风格，必要时 `--scan` 重校区间。
3. 之后按"屏幕 + 风格"直接出正式候选料。
