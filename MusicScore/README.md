# MusicScore — 潮汐 BGM 曲谱

FL Studio Trial 存不了工程，写进 FL 的音符只活在内存里。这个目录是曲谱的真源：
**JSON 是源文件，MIDI 是从它派生的通用备份。** 工程丢了，从这里就能原样重来。

约定：

- **先落盘再写 FL**：曲谱 JSON 写好、用 `tools/` 里的校验脚本验过之后，才往 FL 里重放。
- **每改一段并写进 FL，同一轮就更新对应 JSON**，再跑一次 `build_midi.py`。
- 被替代的版本挪进 `archive/`，不删——用户没有 FL 会员，这些文件就是他的全部工程。

## 目录

```
MusicScore/
  build_midi.py        JSON -> MIDI（需要装了 mido 的 Python）
  score/               现行曲谱，一个段落一个文件：<Pattern>.json
  archive/             被替代的历史版本：<Pattern>.v0.json / .v1.json / ...
  midi/                由 build_midi.py 生成，不要手改
  tools/               每段的生成脚本(曲谱的来历)和校验脚本
```

## 响度标尺（用户 2026-11-23 定，全曲通用）

用户不懂乐理，但用一把相对尺把要求说得很清楚，后面每一段都照它排：

- **响度 0..1，1 = 后续战斗段最响的地方。**
- **主界面这段在 0..0.4**，最响不超过 0.4。
- 最响的 0.4 是**两层各 0.2 的起伏叠出来的**，不是一层硬冲上去。
- 曲线是平滑地涨、平滑地落（「sinx 的曲线」），中间不停。

落到音符力度上：主界面所有音符力度不超过 0.42；两层各不超过 0.2；战斗段写的时候再往 1 去。
力度不等于听感响度（还取决于音色本身和同时响的音数），所以同时响的音数也要一起控制。

**主界面的三层结构**（用户原话归纳）：

| 层 | 用哪组乐器 | 要求 |
|---|---|---|
| 底 | 音调最低的那组 FLEX | 延绵不断，整段不停 |
| 中 | 两组不同音高的 FLEX | 像鱼鳞片一样此起彼伏，一层高时另一层低 |
| 顶 | 最亮的那组 + 钢琴 | 只在中段撑起最响的部分 |

**整段是一次大起伏**（用户在三张曲线示意图里选的）：开头最轻 → 第 16 小节两层叠满到 0.4 → 平滑回落 → 结尾和开头一样轻。

2026-11-24 重建后只剩四个乐器，这套三层落到它们身上：底 = `bass_sub`，中 = `bass_acid`，
顶 = `strings` + `piano`（见下面「主界面 v4.0」一节）。

`HOME_Lazy` v3.0（已归档，被 v4 取代）的做法，公式在 `tools/gen_home_v3.py`：

```
E(t) = sin(pi·t/32)                 大包络，峰在第 16 小节
u(t) = (1 - cos(2·pi·t/8)) / 2       8 小节一次的小涌浪
a(t) = 对齐度：第 12-20 小节为 1，向两侧 4 小节线性降到 0
管风琴层力度 = 0.2 · E · (a + (1-a) · (0.5 + 0.5·u))
弦乐层力度   = 0.2 · E · (a + (1-a) · (0.5 + 0.5·u(t-4)))     与管风琴反相
```

两层之和：两头是 0.30·E（一高一低错着来），第 12–20 小节是 0.40·E（叠满）。
每层每 2 小节换一个和弦音、相邻音重叠 0.3 小节，所以不断开；管风琴在偶数小节起音、弦乐在奇数小节。

改这段时跑 `tools/verify_home.py`，它把上面这些逐条验一遍（峰高、峰位、头尾轻重、与正弦的相关、
两层是否轮换、底是否不断、力度上限、有没有越过循环点、有没有和钢琴同度同响）。
其中「换音时的力度台阶 ≤ 0.08」是我自己拍的参考线，不是用户的要求。
**v4 改用 `tools/verify_home_v4.py`**（上面那套是按管风琴/两 FLEX 写的，对 v4 不适用）。

## 重新生成 MIDI

```
C:\Users\Administrator\Tools\flstudio-mcp\.venv\Scripts\python.exe MusicScore\build_midi.py
```

- 默认转 `score/` 全部，并把它们按 `SONG_ORDER` 首尾相接生成 `midi\Tide_All_in_One.mid`（段间留 2 小节），用来一次听完「像不像一首歌」。
- 加 `--archive` 连历史版本一起转。只想转某几份就把 JSON 路径直接跟在后面。
- 只有文字记录、没有音符的曲谱不会导出；没有音符的槽位也不出空轨。

## 曲谱 JSON 格式（schema `tide-musicscore/1`）

```json
{
 "pattern": "BROWSE_Calm", "bpm": 130, "key": "A minor", "bars": 32,
 "status": "current | archived",
 "harmony": "人类可读的和声进行",
 "tracks": [
  {"slot": "pad", "instrument": "PAD Blicloud",
   "notes": [[57, 0, 4, 0.16], [64, 0, 4, 0.16]]}
 ]
}
```

`notes` 的每个元素是 `[MIDI 音高, 起始(小节), 时值(小节), 力度 0..1]`，4/4 拍，全曲 130 BPM。
这个格式和 FL MCP 写音符工具的参数一一对应，可以原样重放。
`HOME_Lazy.json` 另外带 `loudness_scale`、`envelope`、`layers` 三个说明性字段，记录它怎么符合上面的响度标尺。

## 乐器槽位

曲谱只记 `slot`（乐器槽位），不写死通道号，所以乐器加载顺序变了也不用改曲谱。

**当前工程（2026-11-24 重建，只有 4 个通道）**——`HOME_Lazy` v4.0 就是照这四个写的：

| slot | 通道(0 基) | FL 里显示的通道名 / 插件 | 用途 |
|---|---|---|---|
| `piano` | 0 | Compressed Piano / FL Keys | 主角，弹「潮汐动机」 |
| `bass_acid` | 1 | BAS Fantinal Acid / FLEX | 层 1 引力脉冲：A2 的短音脉冲，中段加密 |
| `bass_sub` | 2 | BAS Cymas / FLEX | 底：A1 横跨全段的长音，不断的那个 |
| `strings` | 3 | Slow PWM Strings / Kepler 36 | 层 2 日出：长音和弦，第 16 小节叠 A5 |

**旧工程（v1–v3 用的，现在没有装）**：`pad` = PAD Blicloud、`organ` = Church organ、
`strings_short` = String Pizzicato、`bass` = BooBass、`drums` = FPC、`lead` = 3x Osc #2。
重放 `archive/` 里的旧版本要先在界面上把对应的音色装回来；现行 v4 只认上面四个 slot。

**通道映射**：重放前先读一次 FL 的通道表核对（`fl_get_channel_state`），不要凭这张表硬编码
通道号——每次重启重新加载乐器，顺序都可能不一样（已经变过一次）。

| 通道(0 基) | 通道机架行 | 乐器 | 槽位 |
|---|---|---|---|
| 0 | 1 | Compressed Piano | `piano` |
| 1 | 2 | BAS Fantinal Acid | `bass_acid` |
| 2 | 3 | BAS Cymas | `bass_sub` |
| 3 | 4 | Slow PWM Strings | `strings` |

用户数通道用的是通道机架从上往下的行号（1 起）：他说的「通道 1」是钢琴。
写 v4 时（2026-11-24）`bass_sub` 和 `strings` 两个通道在 FL 里是静音状态，重放后要自己打开。

**预设不在曲谱里，也不归 MCP 管**：加载插件、选预设只能人在界面上做，
读写插件参数还会把 FL 弄挂死（见下面的操作纪律）。
**怎么知道现在选的是什么预设**：选了预设之后 FL 会把通道名改成预设名，
所以读通道表（`fl_get_channel_state`）就能看到——这个操作是安全的。钢琴的通道音量被用户调到约 +1 dB。

**音色来源的限度**：现在这四组 FLEX 都是 General MIDI Library 级的音色，类型对，但不是电影级。
试用版下不了额外的 FLEX 包（2026-11-23 查官方手册确认）：FLEX 商店里标着「免费」的包
（Core Strings、Core Pianos、Core Brass 等）「仅对已注册的 FL Studio 用户免费」，要先购买许可才能下载，
手册通篇没提试用版。所以能用的只有预装的 8 个包
（`FL Studio 2026\Data\Patches\Packs\FLEX\Packs\`）：Arksun Cityscape、Core 808s、Core Synths、
General Midi Library、Melodic Techno、Mobile Synth Pluck、Mobile Tuned 808 Bass、Olbaid Compendium。
包文件是专有二进制容器，预设名要进了 FL 的 FLEX 浏览器才看得到。

想要更好的只能从 FL 之外找合法免费的（都要自己装插件，MCP 代劳不了）：
- Splice INSTRUMENT：Spitfire 的免费采样音源（LABS）2026-10-31 起迁到这里，官方说原免费的包仍然免费、
  上线时有 500 多个免费预设；需装 Splice 的软件并注册账号，是否绑卡、哪些库免费官方文章没写。
- SoundFont（`.sf2`）：拖到通道机架会自动用 DirectWave 打开。FluidR3_GM 是 MIT 许可、可商用的一整套 GM 音色。
- 用户提过 vsthouse.ru、livekong 等是破解插件的分发站，不用。

Mixer 的 Master 槽 0 上手动加了 Fruity Reeverb 2（长混响，空间感的来源），用户自己调过衰减。

## 怎么恢复进 FL

**A. 原样重放（精确）**——需要 FL 开着、MCP 通、钢琴卷帘跑过一次 `MCP_Apply`：
对每个段落 `fl_pattern_select`，再对每条 track 调 `fl_write_raga_chords`
（`channel` 换算成当时的通道号，`notes` 的四元组转成
`{pitch, time_bars, length_bars, velocity}`，`mode` 用 `replace`）。
不让某轨响就传空的 `notes: []`。写完用 `fl_get_step_grid` 读回核对。

**B. 自己拖 MIDI（不依赖 MCP）**：
把 `midi\<Pattern>.mid` 拖进 FL，会生成对应的通道和 pattern；
把每个通道的乐器换成上面槽位表里的插件即可，音符和力度都在。
缺点是轨道名要自己对照，乐器预设也要自己选。

## MCP 环境与接线（一次装好；复装/换机照抄）

编曲生产线 = AI 操控 FL Studio 自产（flstudio-mcp，MIT；2026-11-22 拍板）。
原计划文档 `docs/音效系统_修改计划.md`、`docs/音频素材清单.md` 于 2026-11-24 删除，
还活着的 MCP 用法收在这里（历史版本查 git）。

| 件 | 位置/说明 |
|---|---|
| flstudio-mcp 仓库 | `C:\Users\Administrator\Tools\flstudio-mcp`（含控制器 / pyscript 源码，排查写入行为读它） |
| Python 3.12 | uv 托管，venv 钉在仓库 `.venv`（不混 Anaconda / 系统 Python） |
| MCP 服务器 | 已注册 `~/.zcode/cli/config.json`（备份 `.bak-flstudio-20261122`）；ZCode 保存配置即热加载拉起，无需新开会话 |
| FL Studio | 本机 2026 **Trial v26.1.7**：不能存/开 .flp → 一曲一会话、当场导出 wav |
| loopMIDI | 1.0.16.27，端口 `FLStudioMCP RX` / `FLStudioMCP TX`（用户手动装的，winget 静默装全被 0x80070656 挡） |

接线步骤（2026-11-22 实测）：

1. loopMIDI 建两端口（名字精确）→ 开一次 FL Studio → 重跑 `scripts\install_windows.bat`（`FLMCP_EXTRAS=none`）。
2. FL MIDI Settings：Input `FLStudioMCP RX` Enable + Controller type=`FLStudioMCP` + Port=42；
   **Port 42 必须填在 Output 列表的 `TX` 行**（误填 RX 行 → 心跳全回流 RX 线缆、doctor 报 NO_HEARTBEAT；
   双口抓包 RX 19 条/10s、TX 0 条定位）。Output 的 RX 行 Port 清 0。
3. 输出端口在 FL 启动后才改的，重启 FL 一次，脚本才拿得到输出通道。
4. 升级服务器依赖前先杀 fl-studio-mcp 进程树（热加载常驻会锁 exe），否则 pip 失败。
5. 每会话首次写音符前，Piano roll → Tools → Scripting → MCP → `MCP_Apply` 布防一次。

自检：doctor 六项全绿 = READY（心跳 = sysex `F0 7D "MCP" 03…` 每 0.5s 一条）。

## 操作纪律（两次把 FL 弄挂死之后定的）

通过 MCP **读写插件参数**会让这台 FL（2026 Trial，中文界面）挂死，已经发生两次：
一次是并发读 FLEX 和 BooBass 的参数，一次是校准混响的衰减时间。FL 进程还活着但不再应答，
内存里的工程全丢。所以：

- 不用 MCP 碰任何插件参数（`fl_plugin_get_params` / `set_param*` / `calibrate_*` / `apply_*_intent` 一律不用）。
  音色、混响、通道音量由人在界面上调。
- 只用这几类从没出过问题的操作：写音符、建/选/改名 pattern、读通道表与路由、读步进（`fl_get_step_grid`）。
- **请求一律串行**，一次只发一个。
- **先落盘再写 FL**：曲谱 JSON 写好并校验之后，才往 FL 里重放。
- 重启后必须先用「写一个音 + `fl_get_step_grid` 读回」验证写入通道已布防；工具回报的 `wrote` 不作数。
- **写入落点 = 钢琴卷帘当前正在编辑的通道，不是机架选中的通道**（2026-11-24 实证，当天两次写偏）：
  卷帘关着时第一次写入不做重定目标（写进卷帘上次编辑的通道，replace 也只清那个通道）；
  卷帘开着时每次带 `channel` 的写入会 hide+show 卷帘完成重定。多轨填充配方 = 卷帘开着，
  逐通道 `fl_write_raga_chords(channel=N, mode=replace)`——replace 只清目标通道自己的 score，
  逐轨 replace 顺带清掉写偏的残骸；写入回报里 `piano_roll_retargeted:true` 才算定向成功。
- `fl_get_step_grid` 的 bit = 音符**起始 16 分槽**（0.6 小节 → 第 9 槽、2.55 小节 → 第 40 槽），
  长音不会铺满整段；写 A 通道后读相邻通道仍原样，可证明没有写串。
- `fl_get_step_grid` 的 `length` 上限 256 步（16 小节），读不到后半段。
- `fl_pattern_get_length` 报的「beats」其实是 16 分步数，÷4 才是拍。
- `fl_apply_reverb_intent` 这类按英文参数名找旋钮的工具在中文界面上直接报错，本来就不可用。
- 我写音符时钢琴卷帘会抢到前台：用户正在 FL 里操作时先说一声，写完就停手。
- **2026-11-24 补充**：这天加了 `fl_instrument_*`（通道机架乐器参数）工具，在 FLEX 上实测过一次
  读+写（0.5→0.62→回读一致）。但本机历史上两次挂死都发生在参数操作上，所以这条纪律不变：
  **参数、音色、混音由人在界面上调，MCP 只碰音符和结构**。

## 主界面 v4.0「双黑洞」（2026-11-24）

用户改主意：主界面画面是**两个相互牵引的黑洞**，参考《星际穿越》的变调 +《Daylight》0-90s。
乐器只用当前 FL 里实际装着的四个（见上面的「当前工程」表）。生成脚本 `tools/gen_home_v4.py`，
校验脚本 `tools/verify_home_v4.py`。

### 画面 → 音乐

| 画面 | 音乐上落到哪 |
|---|---|
| 两个黑洞相互牵引 | 低音 A1 是**不动的引力**；酸贝斯 A2 是**轨道上的脉冲**，一紧一松；两条在第 16 小节叠满 |
| 迷茫的黑夜（Daylight 0-90s 前半） | 0-8 小节只有 A1 底 + 钢琴两个单音；弦乐第 8 小节才进，且很暗（力度 0.03-0.09） |
| 渐渐焦躁 | 12-19 小节：酸贝斯从「每 2 小节一次」加密到「每 2 拍一次」，力度 0.10 -> 0.18 |
| 太阳升起（Daylight 90-110s） | 第 16 小节：弦乐叠 A5 打开、钢琴到 A5、两层叠满 0.40 |
| 循环回黑夜 | 20 小节后退回每小节一次，28 小节酸贝斯**完全停**，弦乐降到 0.03，末和弦收在 32 小节整 |

### 乐器 → 旋律（谁在弹什么）

| slot | 乐器 | 写的是什么 | 小节 |
|---|---|---|---|
| `piano` | Compressed Piano | 潮汐动机：A4 长音起，长—短—长级进下行，接中音回应；第 16 小节冲到 A5 再落回来；22 音 | 0.6 起，到 31.6 |
| `bass_acid` | BAS Fantinal Acid | A2 的引力脉冲：前半每 2 小节一次(0.05-0.09) → 12-19 小节每 2 拍一次(0.10-0.18) → 20-27 每小节一次(0.07) → 28 起停；30 音 | 0-27 |
| `bass_sub` | BAS Cymas | 底：A1 一次写完 32 小节不断(0.05)，第 8/16/24 小节各挂一个 8 小节的 E2(0.04)；4 音 | 0-32 |
| `strings` | Slow PWM Strings | 每 2 小节一个和弦(Am9/Fmaj7/Dm9/Em7sus4/Gadd9)，第 16 小节叠 A5；力度 = 0.20·sin(πt/32)，两端 0.03；37 音 | 8-32 |

和声（2 小节一个，压在 A 上不解决）：Am9 · Fmaj7 · Dm9 · Em7sus4 ·
Am9 · Fmaj7 · Dm9 · Em7sus4 · Am9 · Fmaj7 · Gadd9 · Am9 · Fmaj7 · Dm9 · Em7sus4 · Am9。

### 怎么验、怎么听

```
# 校验（响度上限、峰值位置、底层不断、不越过循环点、酸贝斯 28 后静音、弦乐 8 才进、16 有 A5）
C:\Users\Administrator\Tools\flstudio-mcp\.venv\Scripts\python.exe MusicScore\tools\verify_home_v4.py <Tide 工程根>
# JSON -> MIDI（HOME_Lazy.mid + 接起来的 Tide_All_in_One.mid）
C:\Users\Administrator\Tools\flstudio-mcp\.venv\Scripts\python.exe MusicScore\build_midi.py
```
当前结果：16 项全过，93 音，最重处 0.42（钢琴 @14.1 小节附近），弦乐层峰 0.199，
酸贝斯层峰 0.18，底层覆盖 0..32 无缺口。

听的时候：`midi\HOME_Lazy.mid` 拖进 FL 就是四轨；`midi\Tide_All_in_One.mid` 是主界面接组合界面的试听版。
重放前记得把 `bass_sub`、`strings` 两个通道从静音打开。

## 现状（2026-11-24）

| 段落 | 文件 | 小节 | 状态 |
|---|---|---|---|
| `HOME_Lazy` 主界面 v4.0 | `score/HOME_Lazy.json` | 32 | 现行：双黑洞（四个当前乐器，响度按标尺，93 音，峰在第 16 小节 0.40），MIDI 已重生成，**待用户评价** |
| `HOME_Lazy` 主界面 v3.0 | `archive/HOME_Lazy.v3.0.json` | 32 | 旧方向（星际穿越去掉滴答 + 两组 FLEX 鱼鳞式起伏，78 音），被 v4 取代，用户还没评价 |
| `HOME_Lazy` 主界面 v2.3 | `archive/HOME_Lazy.v2.3.json` | 32 | 把伴奏全切成带空隙的碎块；用户否：「先停，全部断开也不对」 |
| `HOME_Lazy` 主界面 v2.2 | `archive/HOME_Lazy.v2.2.json` | 32 | 厚度加回；换成真持续音色后被评「太重太响，一段接着一段」 |
| `HOME_Lazy` 主界面 v2.1 | `archive/HOME_Lazy.v2.1.json` | 32 | 钢琴当主角 + 全程最多两个 FLEX 同响；减薄是因为当时四个 FLEX 都是默认音色 |
| `HOME_Lazy` 主界面 v2 | `archive/HOME_Lazy.v2.json` | 32 | 用户评价「空旷寂静和孤独感有了」，但 FLEX 叠加占最高位置不合适、钢琴应多用 |
| `HOME_Lazy` 主界面 v1 | `archive/HOME_Lazy.v1.json` | 32 | 已否定：电钢九和弦版 |
| `BROWSE_Calm` 组合界面 | `score/BROWSE_Calm.json` | 32 | 旧的铺满写法(长垫力度 0.4)，**不符合响度标尺**；已在 FL 里但用户还没评价，待按标尺重做 |
| `BATTLE_A_Early` 备战 | `archive/BATTLE_A_Early.v0.json` | 8 | 只存了和声与节奏型（没有旋律） |
| `BATTLE_B_Mid` 中期 | `archive/BATTLE_B_Mid.v0.json` | 8 | 存了 lead 动机；切分伴奏未逐音符保存 |
| `BATTLE_C_Late` 决胜 | `archive/BATTLE_C_Late.v0.json` | 8 | 存了主题旋律与 pad；切分伴奏未逐音符保存 |
| `END_Victory` 胜利 | `archive/END_Victory.v0.json` | 8 | 存了五声旋律；柱式与贝斯未逐音符保存 |
| `END_Defeat` 失败 | `archive/END_Defeat.v0.json` | 8 | 全部音符已保存 |

战斗三段与结算两段的 v0 是电子风初版；已定案整曲统一到管风琴 + 弦乐的影视配乐风，
每段扩到 32 小节（约 59 秒）并做段内变化，重写时沿用 v0 里存下的旋律，响度按上面的标尺排。
