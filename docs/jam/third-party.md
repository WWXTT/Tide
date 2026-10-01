# 第三方清单（真·第三方，非自研）

提交前补齐"凭证"列；TapTap 有权要求权属证明。字体类资产登记在 [asset-ledger.md](asset-ledger.md) 美术表。

## 随游戏分发（运行时）

| 组件 | 来源 | 授权方式 | 要点/义务 | 用途 |
|---|---|---|---|---|
| Unity 6000.5.10f1 | Unity Technologies | 订阅/许可条款 | — | 游戏引擎 |
| Unity URP 17.5 源码 | Unity Technologies | Unity Companion License | 限 Unity 依赖项目 | 渲染管线底座（dbdrp 上游链最上游） |
| DanbaidongRP（dbdrp 的上游） | github.com/danbaidong1111/DanbaidongRP | Unity Companion License（仓库 LICENSE.md，已核） | 限 Unity 依赖项目——本项目即 Unity 项目，合规 | 自定义管线基线：本体为 URP 6.3 二次开发（卡通渲染/光线追踪方向）；本工程自行升级至 URP 17.5，包身份/版本对齐官方 URP（免除 shader 转换，近期修复 VFX 贴图读取错误） |
| MemoryPack 1.21.4 | Cysharp, Inc. | MIT | 副本中保留版权与许可声明（收入第三方 Notices） | 序列化；Unity 侧本地包 Assets/Packages/MemoryPack.Core.1.21.4；与 TideServer NuGet 同版钉死（线格式互通，见 TideServer/TideServer.csproj 注释） |
| HexMap 基础类型谱系 | Catlike Coding Hex Map tutorials（Jasper Flick） | MIT-0（无署名义务） | https://catlikecoding.com/unity/tutorials/license/ ；经典系列无官方 repo（代码经教程页分发），URP 现代版官方 Bitbucket：https://bitbucket.org/catlikecoding-projects/hex-map-project/ | 六角数学与基础类型（HexDirection/HexCoordinates/HexMetrics 等）的教程来源 |
| SRP Core / VFX Graph Samples 17.5.0 | Unity 官方 Samples | Unity Companion License（同 URP 源码家族） | — | 演示资产/VFX 模板 |

## 工程内存在，参赛构建不启用（不分发）

比赛禁联机：热更与联机基础设施保留在工程但不进入参赛构建。

| 组件 | 来源 | 授权方式 | 用途 |
|---|---|---|---|
| HybridCLR | code-philosophy（gitee: focus-creative-games/hybridclr_unity） | MIT | 热更方案（标准 HybridCLR，参赛构建不启用） |
| YooAsset 3.0.6 | github.com/tuyoogame/YooAsset | Apache-2.0（版权 何冠峰/TuYoo Games；修改文件需带修改声明） | 资源热更（参赛构建不启用） |
| MemoryPack（TideServer 侧 NuGet 1.21.4） | Cysharp, Inc. | MIT | TideServer 序列化（TideServer 不随包分发） |
| Newtonsoft Json | Newtonsoft | MIT | TideServer 序列化 |
| MCP for Unity | 第三方插件 | 待核实 | 编辑器工具链（编辑器插件，不随游戏分发） |
| SmoothNormalTool | 待核实（Unity 官方工具?） | 待核实 | 美术工具 |

## 因无授权已删除（不分发）

| 组件 | 来源 | 状态 |
|---|---|---|
| Crest Ocean System | github.com/crest-ocean/crest | 授权未确认 → 运行时代码已从 Assets/Packages 删除（仅剩陈旧 csproj，Unity 下次刷新自动消失，不分发） |
| 地形插件（作者名 Toby Fredson 待核） | 待核实 | 无授权 → 已删除，不分发 |

> 区分：编辑器插件不随游戏分发；随游戏分发的运行时库授权必须允许商用。Apache-2.0 的修改声明义务仅在实际修改过其文件时触发。
