using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Tide.RenderFX
{
    /// <summary>
    /// 教学高亮·后处理选择描边 RendererFeature（2026-11-21 遮罩/高亮/暂停计划 v0.3 §4）：
    /// 目标 renderer 逐个 DrawRenderer 平涂进半分辨率蒙版 RT（Tide/TutorialOutlineFlat）→
    /// 全屏 quad additive 叠回相机色（Tide/TutorialOutlineEdge：8 向膨胀取边·HDR 色·_Time 脉动）。
    /// 注入点 BeforeRenderingPostProcessing——bloom（阈值 0.8）把边缘晕开成"边缘光"。
    /// · 目标登记=TutorialOutlineRegistry（热更侧写入；空=本 feature 不挂 pass，**平时零开销**）；
    /// · 拼装刚体模型/3D TMP 文字/格面 mesh 同一口径（DrawRenderer 免逐部件套壳、不新建预制体）；
    /// · 多条目（多目标/不同色宽）逐条独立蒙版+合成；
    /// · shader 引用走序列化字段（feature 资产常驻→shader 随资产进包，不经 YooAsset 变体剥离）。
    /// RenderGraph 路径（Unity 6 默认；本 fork 的 UniversalRenderPipelineRenderGraph 为正源）——
    /// 若工程切回 Compatibility Mode 本 feature 静默不渲染（教学高亮缺位不炸局）。
    /// 注册：DbdURP_Renderer.asset（Tools/教学表现层/BakeOutlineFeature 或手拖）。
    /// </summary>
    [DisallowMultipleRendererFeature("Tutorial Outline")]
    public sealed class TutorialOutlineFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader m_MaskShader; // Tide/TutorialOutlineFlat（注册资产时由编辑器烘焙）
        [SerializeField] private Shader m_EdgeShader; // Tide/TutorialOutlineEdge

        private Material _maskMaterial;
        private Material _edgeMaterial;
        private Mesh _quad;
        private TutorialOutlinePass _pass;

        public override void Create()
        {
            if (_maskMaterial == null && m_MaskShader != null)
                _maskMaterial = CoreUtils.CreateEngineMaterial(m_MaskShader);
            if (_edgeMaterial == null && m_EdgeShader != null)
                _edgeMaterial = CoreUtils.CreateEngineMaterial(m_EdgeShader);
            if (_quad == null) _quad = BuildFullscreenQuad();
            if (_pass == null) _pass = new TutorialOutlinePass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            // 平时零开销（§4）：登记表空/材质缺/非游戏相机一律不挂 pass
            if (!TutorialOutlineRegistry.HasTargets || _maskMaterial == null || _edgeMaterial == null)
                return;
            if (renderingData.cameraData.cameraType != CameraType.Game) return; // SceneView/Preview 不描
            _pass.Setup(_maskMaterial, _edgeMaterial, _quad);
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            _pass = null;
            CoreUtils.Destroy(_maskMaterial);
            CoreUtils.Destroy(_edgeMaterial);
            _maskMaterial = _edgeMaterial = null;
            if (_quad != null)
            {
                Object.DestroyImmediate(_quad);
                _quad = null;
            }
        }

        /// <summary>全屏 quad（位置即裁剪空间 -1..1·uv 左下原点，与蒙版 RT 同向免翻转）——
        /// 自建免依赖包内 internal 工具（RenderingUtils/Blitter 均不可从 Assets 程序集触达）。</summary>
        private static Mesh BuildFullscreenQuad()
        {
            var mesh = new Mesh { name = "TutorialOutlineFullscreenQuad" };
            mesh.vertices = new[]
            {
                new Vector3(-1f, -1f, 0f), new Vector3(1f, -1f, 0f),
                new Vector3(-1f, 1f, 0f), new Vector3(1f, 1f, 0f),
            };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            return mesh;
        }
    }

    /// <summary>描边 pass：每条目=蒙版 raster pass + 合成 raster pass（BeforeRenderingPostProcessing）。</summary>
    internal sealed class TutorialOutlinePass : ScriptableRenderPass
    {
        private Material _maskMaterial;
        private Material _edgeMaterial;
        private Mesh _quad;

        internal void Setup(Material maskMaterial, Material edgeMaterial, Mesh quad)
        {
            _maskMaterial = maskMaterial;
            _edgeMaterial = edgeMaterial;
            _quad = quad;
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing; // 早于后处理：bloom 晕开边缘
        }

        private sealed class MaskPassData
        {
            internal TextureHandle Mask;
            internal Renderer[] Renderers;
            internal Material MaskMaterial;
        }

        private sealed class EdgePassData
        {
            internal TextureHandle Mask;
            internal Color Color;
            internal float Hdr;
            internal float WidthTexel;
            internal float PulseSpeed;
            internal float PulseAmp;
            internal Material EdgeMaterial;
            internal Mesh Quad;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();
            var cameraData = frameData.Get<UniversalCameraData>();
            var entries = TutorialOutlineRegistry.Entries;
            if (entries.Count == 0 || _maskMaterial == null || _edgeMaterial == null) return;

            var desc = cameraData.cameraTargetDescriptor;
            desc.depthStencilFormat = GraphicsFormat.None; // 蒙版无深度（fork ScreenSpaceShadows 同款属性名）
            desc.msaaSamples = 1;
            desc.graphicsFormat = SystemInfo.IsFormatSupported(GraphicsFormat.R8_UNorm, GraphicsFormatUsage.Render)
                ? GraphicsFormat.R8_UNorm
                : GraphicsFormat.B8G8R8A8_UNorm;
            int camW = desc.width, camH = desc.height;
            desc.width = Mathf.Max(8, camW / 2); // 半分辨率蒙版（描边 2~8px 档足够）
            desc.height = Mathf.Max(8, camH / 2);

            for (int i = 0; i < entries.Count && i < 4; i++) // 上限 4 条（配置审计同口径）
            {
                var entry = entries[i];

                var mask = renderGraph.CreateTexture(new TextureDesc(desc.width, desc.height)
                {
                    colorFormat = desc.graphicsFormat,
                    clearBuffer = true,
                    clearColor = Color.clear,
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    name = $"TutorialOutlineMask_{i}",
                });

                // ①蒙版：目标平涂（DrawRenderer 直绘免 culling/免逐部件套壳）
                using (var builder = renderGraph.AddRasterRenderPass<MaskPassData>(
                           $"TutorialOutline.Mask{i}", out var maskData, new ProfilingSampler($"TutorialOutline Mask {i}")))
                {
                    maskData.Mask = mask;
                    maskData.Renderers = entry.Renderers;
                    maskData.MaskMaterial = _maskMaterial;
                    builder.SetRenderAttachment(mask, 0, AccessFlags.Write);
                    builder.SetRenderFunc((MaskPassData data, RasterGraphContext ctx) =>
                    {
                        for (int r = 0; r < data.Renderers.Length; r++)
                        {
                            var rend = data.Renderers[r];
                            if (rend == null) continue; // Unity fake-null（死亡换区/拆台）
                            int subCount = rend.materials.Length; // 多材质=多 submesh 全涂（轮廓完整）
                            for (int sub = 0; sub < subCount; sub++)
                                ctx.cmd.DrawRenderer(rend, data.MaskMaterial, sub, 0);
                        }
                    });
                }

                // ②合成：膨胀取边 addtive 叠回相机色（HDR 色×脉动，bloom 随后晕开）
                using (var builder = renderGraph.AddRasterRenderPass<EdgePassData>(
                           $"TutorialOutline.Edge{i}", out var edgeData, new ProfilingSampler($"TutorialOutline Edge {i}")))
                {
                    edgeData.Mask = mask;
                    edgeData.Color = entry.Color;
                    edgeData.Hdr = entry.Hdr;
                    edgeData.WidthTexel = Mathf.Max(1f, entry.WidthPx * 0.5f); // 屏幕px→半分辨率 texel
                    edgeData.PulseSpeed = entry.PulseSpeed;
                    edgeData.PulseAmp = entry.PulseAmp;
                    edgeData.EdgeMaterial = _edgeMaterial;
                    edgeData.Quad = _quad;
                    builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.Write);
                    builder.UseTexture(mask, AccessFlags.Read);
                    builder.AllowGlobalStateModification(true); // 蒙版经 SetGlobalTexture 绑 _MaskTex（RasterCommandBuffer 无 per-material TextureHandle 重载）
                    builder.SetRenderFunc((EdgePassData data, RasterGraphContext ctx) =>
                    {
                        var m = data.EdgeMaterial;
                        m.SetColor(PropColor, data.Color);
                        m.SetFloat(PropHdr, data.Hdr);
                        m.SetFloat(PropWidth, data.WidthTexel);
                        m.SetFloat(PropPulseSpeed, data.PulseSpeed);
                        m.SetFloat(PropPulseAmp, data.PulseAmp);
                        ctx.cmd.SetGlobalTexture(PropMask, data.Mask);
                        ctx.cmd.DrawMesh(data.Quad, Matrix4x4.identity, m, 0, 0);
                    });
                }
            }
        }

        private static readonly int PropMask = Shader.PropertyToID("_MaskTex");
        private static readonly int PropColor = Shader.PropertyToID("_OutlineColor");
        private static readonly int PropHdr = Shader.PropertyToID("_Hdr");
        private static readonly int PropWidth = Shader.PropertyToID("_WidthTexel");
        private static readonly int PropPulseSpeed = Shader.PropertyToID("_PulseSpeed");
        private static readonly int PropPulseAmp = Shader.PropertyToID("_PulseAmp");
    }
}
