# DBDRP 版本契约（DanbaidongRP 魔改 URP）

> 目的：这份 fork 是"URP 底座 + 自研 HDRP 级特性"（HQF Bloom / 屏幕空间光追阴影 / SSR / ShadowScatter / GPU 光列表 Cluster Deferred）。
> 2026-09-30 的 17.5 基线合并只升了 shader 侧、C# 侧被 R8 回退，造成 GBuffer 布局两代并存，2026-10-22 定位为"D3D12 下 Lit 材质不受光"的根因。
> 本契约用于防止同类事故复发：**ABI 四组永远同版本**，自研层与上游层分离。

---

## 1. 版本矩阵（当前锁定态，2026-10-22）

| 层 | 版本 | 说明 |
|---|---|---|
| `ShaderLibrary/**` | **17.5** | 上游库（含光路 Lighting/Shadows/GlobalIllumination/Input） |
| `Runtime/**`（C#） | **fork 17.3 血统 + 少量 17.5** | R8/R11 精确对齐 fork 原版；`DeferredLights`、`GPULights` 均为 fork 版 |
| `Editor/**` | **17.5** | ShaderGraph Target（含 `LightmapBicubicSampling`）、ShaderGUI、VFX 绑定器 |
| VFX（`Editor/VFXGraph` + `Runtime/VFXGraph` + 模板） | **17.5** | 必须与 `UniversalTarget` 同版本（`CoreKeywordDescriptors` 依赖） |
| `package.json` | **17.5.0** | 依赖 core/shadergraph 17.5.0，与 `Packages/manifest.json` 一致 |
| **Lit 家族 5 件**（`Lit.shader`、`LitInput.hlsl`、`LitForwardPass.hlsl`、`LitGBufferPass.hlsl`、`LitDepthNormalsPass.hlsl`） | **fork 17.3** | **GBuffer ABI 冻结在此**，见 §2 |
| `Shaders/Lighting/DeferredLighting.compute`、`Shaders/Utils/CopyDepthPass.hlsl` | 17.5 | compute 仅 AOR 改动；读取端仍需 `UnityGBuffer.hlsl` 宏 |
| 其余 103 个非 GBuffer shader | fork 17.3 | 2D/Terrain/Particles/Nature/PostProcessing/XR/Debug/Utils、Unlit/SimpleLit/BakedLit/ComplexLit |

---

## 2. GBuffer ABI 四组（必须同版本，禁止单边升级）

| 组 | 文件 | 关键内容 |
|---|---|---|
| ① 写出端 | `Shaders/LitGBufferPass.hlsl` 及同类 `SimpleLit*/BakedLit*/ComplexLit/Unlit*` 的 GBuffer pass | 用 `BRDFDataToGbuffer()` + `UnityGBuffer.hlsl`（fork 17.3），渲染层写 **float 归一化** |
| ② 分配端 | `Runtime/DeferredLights.cs` | `GBufferSliceCount = 4 + (UseFramebufferFetch?1:0) + shadowMask + renderingLayers`；`UseFramebufferFetch=false`（本工程 `m_UseNativeRenderPass: 0`）→ 5 张 RT，渲染层在 **index 4**、格式 `R8_UNorm` |
| ③ 读取端 | `Shaders/Lighting/DeferredLighting.compute` | `#include UnityGBuffer.hlsl` + `MERGE_NAME(_, GBUFFER_LIGHT_LAYERS)` + `DecodeMeshRenderingLayer()`（`*255+0.5`） |
| ④ 宏定义 | `ShaderLibrary/GBufferCommon.hlsl` + `ShaderLibrary/UnityGBuffer.hlsl` | `GBUFFER_FEATURE_*` / `GBUFFER_IDX_*` / `GBUFFER_OPTIONAL_SLOT_*` |

**事故形态**：只升 ① 到 17.5（`GBufferFragOutput` + `PackGBuffersBRDFData` + uint 编码），②③④ 仍是 17.3 → 渲染层切片解码为 0 → `IsMatchingLightLayer(1,0)` 恒 false → 方向光全跳过 → 只剩 SH 环境光（"深蓝平板/纯色无过渡"）。

**为什么不做"全 17.5"**：那要把 ②（`DeferredLights` 的 depth slice / `UseFramebufferFetch` / MRT 索引）一起升，而这正是 R8 为编译稳定回退的部分；且 17.5 新布局是为 native render pass / framebuffer fetch 服务，本工程 `UseFramebufferFetch=false`，**收益为零**。

---

## 3. 自研层保护清单（升级时只允许改清单外的文件）

```
Runtime/ScreenSpaceLighting/**             SSR / 屏幕空间光
Runtime/RendererFeatures/ScreenSpaceShadows.cs
Runtime/ScreenSpaceLighting/ScreenSpaceDirectionalShadowsPass.cs
Runtime/Lighting/DeferredLighting.cs       + Shaders/Lighting/DeferredLighting.compute
Runtime/Lights/GPULights*.cs               + Shaders/Utils/ClusterDeferred*
Runtime/Overrides/Shadows.cs               ShadowScatter / RT 阴影开关
ShaderLibrary/Shadows.hlsl                 ShadowRamp / scatter 参数
Shaders/Material/PBRToon/**                （若后续启用）
```

这三项核心能力的实际入口（保持不变）：
- **HQF Bloom**：`PostProcessing/**` + Volume `Bloom.highQualityFiltering`；
- **光追阴影**：`Shadows.rayTracing` → `ScreenSpaceDirectionalShadowsPass`（屏幕空间 ray-march，非硬件 RT）；
- **SSR**：Volume `ScreenSpaceReflection` → `ScreenSpaceReflectionPass`。

---

## 4. 检查与验证

### 4.1 一键检查
菜单 **Tide/Dbdrp/检查版本契约**（`Assets/Editor/DbdrpContractCheck.cs`，快捷键 Ctrl/Cmd+Shift+D）。
输出：Console + `Logs/DbdrpContractCheck.txt`。
检查项：
1. ①②③④ 布局是否一致（写出端必须 `BRDFDataToGbuffer`、读取端必须 `UnityGBuffer.hlsl`）；
2. 17.5 独有、但被依赖的文件是否存在（`SurfaceType.hlsl`、`GBufferOutputFormat.hlsl`、`UnityGBuffer.deprecated.hlsl`、Terrain/UITK Target、`VFXPassDepthCommonFragmentLit.template`）；
3. 关键 shader 与 `Assets/Art/Shaders/**` 全部无编译错误（`ShaderUtil.ShaderHasError`）。

### 4.2 每次改动后的验证矩阵
| 项 | 命令/方式 | 通过标准 |
|---|---|---|
| 契约 | 上述菜单 | PASS |
| Console | 清空后重编 | 零 `Shader error` |
| DX12 编辑器 | `Unity.exe -force-d3d12` | 地块/角色柱有光影过渡、跟随主光强度 |
| 三项能力 | 固定测试场景 | Bloom/RT 阴影/SSR 开关效果可辨、无粉屏 |
| Android Vulkan | 出包实测 | Low/High 档位正常、无掉帧 |

---

## 5. 升级 playbook

1. **冻结 Unity 版本**：6000.5.10f1；大版本升级单独开分支。
2. 升级 URP 上游时**只动**：`ShaderLibrary/*`（非 GBuffer 相关）、`Shaders/{2D,Terrain,Particles,Nature,PostProcessing,Utils}`、`Editor/*`、VFX 模板。
3. **永不动** §2 的四组（除非四组一起动，且 §4.2 全绿）。
4. 升级后顺序：契约检查 → Console → DX12 编辑器 → 三项能力基线 → Android 包。
5. 新增自研 pass 时：只写在 §3 保护清单目录内，并补一条基线用例。

---

## 6. P2 执行清单（剩余 103 个文件，交给他方执行时的硬约束）

**禁止升级（必须留在 fork 17.3）**——凡是带 `UniversalGBuffer` pass 的 shader，都属于 §2 的 ①写出端：
```
Shaders/SimpleLit.shader + SimpleLitInput/SimpleLitForwardPass/SimpleLitGBufferPass/SimpleLitDepthNormalsPass.hlsl
Shaders/BakedLit.shader + BakedLitInput/BakedLitForwardPass/BakedLitGBufferPass/BakedLitDepthNormalsPass.hlsl(+meta)
Shaders/ComplexLit.shader
Shaders/Unlit.shader + UnlitInput/UnlitForwardPass/UnlitGBufferPass/UnlitDepthNormalsPass.hlsl
Shaders/Lit*.hlsl / Lit.shader（已完成升级的 5 件里，LitGBufferPass 已钉回 17.3，其余保持）
```

**允许升级（不写 GBuffer）**：
```
Shaders/2D/**（28）  Shaders/Terrain/**（13）  Shaders/Particles/**（11）  Shaders/Nature/**（6）
Shaders/PostProcessing/**（6）  Shaders/XR/**（3）  Shaders/Debug/**（2）  Shaders/Decal.shadergraph
Shaders/DepthNormalsPass.hlsl  Shaders/ObjectMotionVectorFallback.shader
Shaders/Utils/**（13，其中 StencilDeferred.shader 与 StencilDeferred.hlsl 必须成对；ClusterDeferred.shader/.hlsl、FogDeferred.hlsl 同来源成组）
```

**执行规则**：
1. 每批 ≤ 20 个文件，按目录成组；
2. 每批后必须：Unity 重编译 → Console 无 `Shader error` → 跑一次 §4.1 契约检查（PASS）→ 进下一批；
3. 任何一批出现 include 缺失，先补被引用的 17.5 新文件（如 `SurfaceType.hlsl` 那样），**不许改代码绕过**；
4. 严禁触碰 §2 四组与 §3 保护清单；如需动，先停下来走 §5 playbook。

---

## 7. 历史事故索引

| 日期 | 现象 | 根因 | 处置 |
|---|---|---|---|
| 2026-10-18 | D3D12 包内 Lit 全部"深蓝平板" | 图形 API 钉了 D3D12，fork 的 Lit 在该 API 下异常（当时未定性） | 临时钉 D3D11 出包 |
| 2026-10-21 | Forward 管线每帧 NRE | `m_DeferredLighting.OnCameraCleanup` 在 Deferred 闸门外 | 挪进 `if (Deferred)`（**未提交，见备注**） |
| 2026-10-22 | 同上（D3D12 不受光） | **GBuffer ABI 四组版本错配**（①升 17.5，②③④仍 17.3） | `LitGBufferPass.hlsl` 钉回 17.3（`3ab5a2fa`） |
| 2026-10-22 | 补 VFX 时 `CS0117 LightmapBicubicSampling` | VFX 绑定器 17.5 依赖 `UniversalTarget` 17.5 | VFX 与 Editor 同批补 |
| 2026-10-22 | `Couldn't open include SurfaceType.hlsl` | `LitInput` 17.5 引用 17.5 新增文件，未同批补 | 补 `Shaders/Utils/SurfaceType.hlsl` |

> 备注：2026-10-21 那条 Forward NRE 修复在回退 Dbdrp 时被撤销（`git checkout`），当前 HEAD 的 `UniversalRendererRenderGraph.cs:844` 仍是无条件调用。若打开 Crest 等 Forward 管线场景会重现 NRE，重放补丁即可（对 Deferred 路径零影响）。
