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

`HOME_Lazy` v3.0 的做法（公式在 `tools/gen_home_v3.py`）：

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

| slot | 现在的乐器（预设名） | 用途 |
|---|---|---|
| `piano` | Compressed Piano | 钢琴，主角 |
| `pad` | PAD Blicloud | 音调最低的一组 FLEX，主界面里当不断的底 |
| `organ` | Church organ | 起伏层 1（中低音区） |
| `strings` | Slow PWM Strings | 起伏层 2（高音区，最亮） |
| `strings_short` | String Pizzicato | 短促的弦乐拨弦，留给战斗段做脉动 |
| `bass` | BooBass | 低音（主界面不用） |
| `drums` | FPC | 鼓，音符音高 = FPC 的 pad |
| `lead` | 3x Osc #2 | 只在 `archive/` 的历史版本里出现；该音源已不用 |

**通道映射**：重放前先读一次 FL 的通道表核对，不要凭这张表硬编码通道号——
每次重启重新加载乐器，顺序都可能不一样（已经变过一次）。

| 通道(0 基) | 通道机架行 | 乐器 | 槽位 |
|---|---|---|---|
| 0 | 1 | Compressed Piano | `piano` |
| 1 | 2 | FPC | `drums` |
| 2 | 3 | PAD Blicloud | `pad` |
| 3 | 4 | Church organ | `organ` |
| 4 | 5 | Slow PWM Strings | `strings` |
| 5 | 6 | String Pizzicato | `strings_short` |
| 6 | 7 | BooBass | `bass` |

用户数通道用的是通道机架从上往下的行号（1 起）：他说的「通道 1」是钢琴。

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
- `fl_get_step_grid` 的 `length` 上限 256 步（16 小节），读不到后半段。
- `fl_pattern_get_length` 报的「beats」其实是 16 分步数，÷4 才是拍。
- `fl_apply_reverb_intent` 这类按英文参数名找旋钮的工具在中文界面上直接报错，本来就不可用。
- 我写音符时钢琴卷帘会抢到前台：用户正在 FL 里操作时先说一声，写完就停手。

## 现状（2026-11-23）

| 段落 | 文件 | 小节 | 状态 |
|---|---|---|---|
| `HOME_Lazy` 主界面 | `score/HOME_Lazy.json` | 32 | 现行 v3.0：按响度标尺重做(一次大起伏，两层各 0.2 叠满 0.4，底不断，两组 FLEX 此起彼伏)，78 音，已在 FL 里，**待用户评价** |
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
